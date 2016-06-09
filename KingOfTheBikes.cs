//[assembly: Rage.Attributes.Plugin("King of the Bikes", Description = "Rule the Road", Author = "TimE")]

namespace KingOfTheBikes {
    using System;
    using System.Windows.Forms;
    using System.Drawing;
    using GTA;
    using GTA.Native;
    using GTA.Math;
    //using Rage;
    using System.Collections.Generic;
    //using RAGENativeUI.Elements;

    public class KingOfTheBikes : Script {
        private bool cops = false;
        private bool spawn_foes = true;
        private GTA.Ped player;
        
        private bool king = false;
        private bool ready_to_spawn = true;

        //these fields are reset each game
        //in secs
        private long time_king = 0;
        //in ticks
        private int clock = -1;
        private int kills = 0;
        private int peasant_kills = 0;
        private List<Target> foes = new List<Target>(50);
        private int get_on_bike_timer = GET_ON_BIKE_TIME;
        private int num_of_living_enemies = 0;
        private int clock_to_spawn_enemies_at = -1;
        private float enemy_accuracy_mult = ENEMY_ACCURACY_INIT;
        
        //public get, private set, how do?
        public static int foegroup { get; set; }

        private const int INTERVAL = 1000;
        private const int GET_ON_BIKE_TIME = 16;
        private const int LONG_MESSAGE_DURATION = 5000;
        private const float FIND_PEASANT_RADIUS = 100f;
        private const float PEASANT_ESCAPE_RADIUS = 1000f;
        //vary by difficulty
        private const float ENEMY_ACCURACY_INIT = 0.75f;
                
        private int current_level = 0;

        public KingOfTheBikes() {
            this.Interval = INTERVAL;
            this.Tick += onTick;
            this.KeyUp += onKeyUp;

            player = GTA.Game.Player.Character;

            foegroup = GTA.World.AddRelationshipGroup("KOTB_FOES");
            GTA.World.SetRelationshipBetweenGroups(GTA.Relationship.Hate, foegroup, player.RelationshipGroup);
            GTA.World.SetRelationshipBetweenGroups(GTA.Relationship.Hate, player.RelationshipGroup, foegroup);
        }

        private void onTick(object sender, EventArgs e) {
            if(king) {
                player.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 1);
                clock++;                                
                clock %= 10;        //# ticks in a cycle

                time_king++;
                //HACK (this shouldn't need to be done more than once!)
                if (clock % 5 == 0) {
                    Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, player);
                    GTA.Game.MaxWantedLevel = 0;
                }

                UI.ShowSubtitle("~b~" + secsToTime(time_king) + "~s~, ~r~" + kills + " ~s~rebels destroyed, ~g~" + peasant_kills + " ~s~peasants put down", INTERVAL);

                //player can't be off bike for more than GET_ON_BIKE_TIME ticks
                if (!isOnBike(player)) {
                    get_on_bike_timer--;
                    if (get_on_bike_timer < GET_ON_BIKE_TIME && player.IsAlive)
                        UI.ShowSubtitle("GET ON A BIKE!! ~r~" + get_on_bike_timer, INTERVAL);
                    
                    if (get_on_bike_timer == 0) {
                        reignEnded();
                        UI.ShowSubtitle("~r~You should have gotten back on a bike.", LONG_MESSAGE_DURATION);
                    }
                }
                //reset timer when player gets back on a bike
                if (get_on_bike_timer != GET_ON_BIKE_TIME && isOnBike(player)) {
                    get_on_bike_timer = GET_ON_BIKE_TIME;
                }
                
                //Mark nearby on-bike peds as peasants
                //TODO unmarked peds will not count as kills (if the player is too fast)
                GTA.Ped[] nearby = GTA.World.GetNearbyPeds(player, FIND_PEASANT_RADIUS);
                foreach (GTA.Ped p in nearby) {
                    if (p.IsInVehicle() && isOnBike(p) && !isFoe(p)) {
                        //need to mark aggressive peds. for some reason this removes their blip.
                        //could remove blip for more difficulty
                        /*Blip b;
                        if ((b = p.CurrentBlip) != null) {
                            b = p.AddBlip();
                            b.Color = BlipColor.Green;
                        }*/
                        p.IsEnemy = true;
                        p.IsPersistent = true;
                        foes.Add(new Target(p, p.CurrentVehicle, p.CurrentBlip, false));
                    }
                }

                Vector2 player2d = new Vector2(player.Position.X, player.Position.Y);
                //update foes
                for (int i = foes.Count - 1; i >= 0; i--) { 
                    //check for and remove dead enemies
                    if (!foes[i].p.IsAlive) {
                        if (foes[i].isAggro) {
                            kills++;
                            num_of_living_enemies--;
                            //and you get a new vest every 10 foe kills (could be every level or smtg)
                            if (kills % 10 == 0) {
                                player.Armor = 100;
                                UI.Notify("Armor repaired");
                            }
                        }
                        else
                            peasant_kills++;

                        removeFoe(foes[i]);
                        // you get ammo for killing someone. why not
                        OutputArgument oa = new OutputArgument();
                        Function.Call(Hash.GET_CURRENT_PED_WEAPON, player, oa, true);
                        int weapon_int = oa.GetResult<int>();
                        WeaponHash wp = (WeaponHash)weapon_int;

                        int ammo_to_give = 1;
                        //you get more ammo for the driveby guns
                        if (wp == WeaponHash.MicroSMG   || wp == WeaponHash.APPistol        || wp == WeaponHash.CombatPistol || 
                            wp == WeaponHash.Pistol     || wp == WeaponHash.SawnOffShotgun  || wp == WeaponHash.HeavyPistol) {
                            ammo_to_give = 50;
                        }
                        Function.Call(Hash.ADD_AMMO_TO_PED, player, (int)wp, ammo_to_give);
                    }
                    //check for and remove peasants that are too far away
                    else if(!foes[i].isAggro) {
                        Vector2 peasant2d = new Vector2(foes[i].p.Position.X, foes[i].p.Position.Y);                        
                        if (Vector2.Distance(player2d, peasant2d) > PEASANT_ESCAPE_RADIUS) {
                            removeFoe(foes[i]);
                            //UI.ShowSubtitle("~r~You let a peasant escape your kingdom.", LONG_MESSAGE_DURATION);
                            //reignEnded();
                        }
                    }
                }
                
                //enemy spawn code
                if (spawn_foes && ready_to_spawn && num_of_living_enemies == 0) {
                    player.Health = player.MaxHealth;
                    ready_to_spawn = false;
                    if (clock >= 5)
                        clock_to_spawn_enemies_at = clock - 5;
                    else
                        clock_to_spawn_enemies_at = clock + 5;                    
                }

                if(clock == clock_to_spawn_enemies_at) {
                    UI.Notify("Level " + (current_level+1), true);
                    ready_to_spawn = true;
                    clock_to_spawn_enemies_at = -1;

                    Target[] newfoes = EnemySpawner.spawn_level(current_level);

                    //should rethink the way levels are selected (eg. last a certain amount of time & spawn enemies every x seconds)
                    if (current_level < EnemySpawner.NUM_LEVELS-1)
                        current_level++;

                    foreach (Target t in newfoes) {
                        foes.Add(t);
                        num_of_living_enemies++;
                    }
                }

                if (!player.IsAlive) {
                    reignEnded();
                }
            }
        }
        
        //perform cleanup on dead enemy or on all foes when dethroned
        private void removeFoe(Target e) {
            //e.p.Task.ClearAllImmediately();
            e.p.Task.ClearAll();
            e.p.MarkAsNoLongerNeeded();
            e.v.MarkAsNoLongerNeeded();
            if(e.b != null)
                e.b.Remove();
            foes.Remove(e);
        }

        //returns if a ped is in the list of Targets
        private bool isFoe(Ped p) {
            foreach(Target e in foes) {
                if(e.p.Equals(p)) {
                    return true;
                }
            }
            return false;
        }

        //this better be self documenting
        private bool isOnBike(GTA.Ped p) {
            GTA.Vehicle v = p.CurrentVehicle;
            return  v != null &&
                    (v.ClassType == VehicleClass.Motorcycles || v.ClassType == VehicleClass.Cycles);
        }

        //called when you turn off king mode or die
        //outputs the scores of your previous run and resets variables
        private void reignEnded() {
            UI.Notify("Your reign has come to an end. ~n~Time: " + secsToTime(time_king) + "~n~Foe Kills: " + kills + "~n~Peasant Kills: " + peasant_kills);
            king = false;
            ready_to_spawn = true;
            time_king = 0;
            clock = -1;
            kills = 0;
            peasant_kills = 0;
            get_on_bike_timer = GET_ON_BIKE_TIME;
            num_of_living_enemies = 0;
            current_level = 0;
            //Rage.Game.RawFrameRender -= drawKingUI;
            GTA.Game.MaxWantedLevel = 5;

            for (int i = foes.Count - 1; i >= 0; i--) {
                removeFoe(foes[i]);
            }
        }
        
        private static String secsToTime(long secs) {
            //format this nicer
            return TimeSpan.FromSeconds(secs).ToString(@"mm\:ss");
        }

        private void onKeyUp(object sender, System.Windows.Forms.KeyEventArgs e) {
            if (e.KeyCode == Keys.NumPad3) {
                if (!king) {
                    if (isOnBike(player)) {
                        //new king code
                        UI.Notify("You are now the King of the Bikes. Hunt down and destroy those who oppose you!");
                        king = true;
                        player.Armor = 100;
                        //stop player from switching characters somehow
                        //Rage.Game.RawFrameRender += drawKingUI;
                        if (!cops) {
                            GTA.Game.MaxWantedLevel = 0;
                            Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, player);
                        }
                    }
                    else {
                        UI.Notify("You can't be King of the Bikes if you aren't on a bike...");
                    }
                }
                else {
                    reignEnded();
                }
            }
            else if (e.KeyCode == Keys.NumPad0) {
                UI.Notify("The script has not yet crashed");
                Function.Call(Hash.DESTROY_MOBILE_PHONE);
            }

            //fun stuff
            else if (e.KeyCode == Keys.End) {
                spawnThrone();
            }

            else if (e.KeyCode == Keys.NumPad5) {
                UI.Notify("Wanted level removed");
                Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, 0, false);
            }

            else if (e.KeyCode == Keys.NumPad9) {
                //toggle invincibility
                bool isInvincible = Function.Call<bool>(Hash.GET_PLAYER_INVINCIBLE, Game.Player);
                UI.Notify("You are " + (isInvincible ? "no longer" : "now") + " invincible");
                Function.Call(Hash.SET_PLAYER_INVINCIBLE, Game.Player, !isInvincible);
            }
        }

        private void spawnThrone() {
            Vehicle v = World.CreateVehicle(VehicleHash.Bati, player.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));

            v.DirtLevel = 0f;
            v.CustomPrimaryColor = Color.FromArgb(0, 51, 204);
            v.CustomSecondaryColor = Color.Gold;
            v.NumberPlate = "NEED4SPD";

            v.InstallModKit();
            v.SetMod(VehicleMod.Armor, 5, true);
            v.SetMod(VehicleMod.Brakes, 3, true);
            v.SetMod(VehicleMod.Horns, 1, true);
            v.SetMod(VehicleMod.PlateHolder, 4, true);
            v.SetMod(VehicleMod.Transmission, 3, true);
            //v.SetMod(VehicleMod., 5, true);           //turbo?

            v.SetMod(VehicleMod.FrontWheels, 9, true);
            v.SetMod(VehicleMod.BackWheels, 9, true);
            //v.SetMod(VehicleMod.Ti)                   //tires?

            v.CanTiresBurst = false;
            v.CanWheelsBreak = false;
            v.EngineCanDegrade = false;
            //v.EnginePowerMultiplier = 1.5f;

            v.PlaceOnGround();
            v.MarkAsNoLongerNeeded();
        }
    }
}
