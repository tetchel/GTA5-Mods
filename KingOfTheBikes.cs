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
        //set this to false to make police fear the king
        private bool can_be_wanted = false;
        //shorthand for Game.Player.Character
        private Ped player;
        //if the player is king ATM
        private bool king = false;

        //these fields are reset each round
        //in ticks
        private long time_king = 0;
        private int clock = 0,
                    kills = 0,
                    score = 0,
                    peasant_kills = 0,
                    current_level = -1,
                    get_on_bike_timer = GET_ON_BIKE_TIME;

        private List<Target> foes = new List<Target>(50);
        //private float enemy_accuracy_mult = ENEMY_ACCURACY_INIT;

        private const int INTERVAL = 1000,                //time between ticks in ms
                            TICKS_PER_LEVEL = 100,          //level duration in units of INTERVAL
                            GET_ON_BIKE_TIME = 21,          //amount of time player has to get back on bike until loses points
                            GET_ON_BIKE_FAIL_TIME = 10,     //starts after GET_ON_BIKE_TIME expires 
                                                            //(length of grace period during which player rapidly loses points)
                            LONG_MESSAGE_DURATION = 5000,
                            //points values - could vary by level or something
                            //TODO persistant points in the top corner
                            ENEMY_KILL_VALUE = 100,
                            PEASANT_KILL_VALUE = 1000,
                            POINTS_PER_SECOND = 1,
                            POINTS_LOST_OFFBIKE_PER_SECOND = 100;

        private const float FIND_PEASANT_RADIUS = 100f,     //performance impact ?
                            PEASANT_ESCAPE_RADIUS = 1000f;
        //vary by difficulty
        //private const float ENEMY_ACCURACY_INIT = 0.75f;
                

        public KingOfTheBikes() {
            Interval = INTERVAL;
            Tick += onTick;
            KeyUp += onKeyUp;

            player = Game.Player.Character;
        }

        private void onTick(object sender, EventArgs e) {
            if (king) {
                //player.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 1);

                time_king++;
                //HACK (this shouldn't need to be done more than once!)
                //maybe there is a way to do this using relationship groups
                if (clock % 5 == 0 && !can_be_wanted) {
                    Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, player);
                    Game.MaxWantedLevel = 0;
                }

                //  UI.ShowSubtitle("~b~" + score + "~s~       ~r~" + kills + " ~s~rebels destroyed, ~g~" + 
                //      peasant_kills + " ~s~peasants quelled", INTERVAL);
                UI.ShowSubtitle("~b~" + score, INTERVAL);

                //player can't be off bike for more than GET_ON_BIKE_TIME ticks
                //should instead subtract points quickly after a certain amount of time, then fail after 30s ?
                if (!isOnBike(player)) {
                    get_on_bike_timer--;
                    //turn the score red or something to indicate losing points
                    if (get_on_bike_timer < GET_ON_BIKE_TIME && player.IsAlive) {
                        string color = get_on_bike_timer <= GET_ON_BIKE_FAIL_TIME ? "~r~ " : "~b~ ";
                        UI.ShowSubtitle(color + score + " ~s~GET ON A BIKE!! ~r~" + get_on_bike_timer, INTERVAL);
                    }
                    
                    if (get_on_bike_timer <= GET_ON_BIKE_FAIL_TIME) {
                        if (score >= POINTS_LOST_OFFBIKE_PER_SECOND)
                            score -= POINTS_LOST_OFFBIKE_PER_SECOND;
                        else
                            score = 0;

                        if(get_on_bike_timer <= 0) {
                            reignEnded();
                            UI.ShowSubtitle("~r~You should have gotten back on a bike.", LONG_MESSAGE_DURATION);
                            return;
                        }
                    }
                }
                else {
                    //points only if on bike
                    score += POINTS_PER_SECOND*(current_level+1);
                }

                //reset timer when player gets back on a bike
                if (get_on_bike_timer != GET_ON_BIKE_TIME && isOnBike(player)) {
                    get_on_bike_timer = GET_ON_BIKE_TIME;
                }
                
                //Mark nearby on-bike peds as peasants
                //TODO unmarked peds will not count as kills (if the player is too fast)
                Ped[] nearby = World.GetNearbyPeds(player, FIND_PEASANT_RADIUS);
                foreach (Ped p in nearby) {
                    if (p.IsInVehicle() && isOnBike(p) && !isFoe(p)) {
                        //give enemies a blip - for some reason they were being removed before.
                        //non-hostile peasants no longer have blips.
                        Blip b;
                        if (p.Weapons.BestWeapon.Hash != WeaponHash.Unarmed) {
                            b = p.AddBlip();
                            b.Color = BlipColor.Red;
                        }
                        else {
                            b = p.CurrentBlip;
                        }
                        p.IsEnemy = true;
                        p.IsPersistent = true;
                        foes.Add(new Target(p, p.CurrentVehicle, b, false));
                    }
                }

                Vector2 player2d = new Vector2(player.Position.X, player.Position.Y);
                //update foes
                for (int i = foes.Count - 1; i >= 0; i--) { 
                    //check for and remove dead enemies
                    if (!foes[i].p.IsAlive) {
                        if (foes[i].isAggro) {
                            kills++;
                            score += ENEMY_KILL_VALUE;
                            givePlayerAmmo();
                        }
                        else {
                            peasant_kills++;
                            score += PEASANT_KILL_VALUE;
                        }

                        removeFoe(foes[i]);
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

                if(clock == 0) {
                    //level is over
                    current_level++;
                    UI.Notify("Level " + (current_level+1), true);
                    player.Armor = 100;
                    player.Health = player.MaxHealth;
                }

                const int SPAWN_INTERVAL = 20;
                if(clock % SPAWN_INTERVAL == 0) {
                    Target[] newfoes = EnemySpawner.spawn_level(current_level);

                    //add the new enemies to the tracked peds list
                    foreach (Target t in newfoes) {
                        foes.Add(t);
                    }
                }

                //tick the clock
                clock++;
                clock %= TICKS_PER_LEVEL;
                if (!player.IsAlive) {
                    reignEnded();
                }
            }
        }

        //gives the player ammo for current weapon each time they get a kill
        //usually does not work properly for explosives cause you can have time to switch weapons but whatever
        private void givePlayerAmmo() {
            OutputArgument oa = new OutputArgument();
            Function.Call(Hash.GET_CURRENT_PED_WEAPON, player, oa, true);
            int weapon_int = oa.GetResult<int>();
            WeaponHash wp = (WeaponHash)weapon_int;

            int ammo_to_give = 1;
            //you get more ammo for the driveby guns
            if (wp == WeaponHash.MicroSMG || wp == WeaponHash.APPistol || wp == WeaponHash.CombatPistol ||
                wp == WeaponHash.Pistol || wp == WeaponHash.SawnOffShotgun || wp == WeaponHash.HeavyPistol) {
                ammo_to_give = 50;
            }
            Function.Call(Hash.ADD_AMMO_TO_PED, player, (int)wp, ammo_to_give);
        }
        
        //perform cleanup on dead enemy or on all foes when dethroned
        private void removeFoe(Target e) {
            Logger.log("Removing foe: " + e.p.Model);
            //e.p.Task.ClearAllImmediately();
            e.p.Task.ClearAll();
            e.p.MarkAsNoLongerNeeded();
            e.v.MarkAsNoLongerNeeded();
            //if(e.b != null)
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
        private bool isOnBike(Ped p) {
            Vehicle v = p.CurrentVehicle;
            return  v != null &&
                    (v.ClassType == VehicleClass.Motorcycles || v.ClassType == VehicleClass.Cycles);
        }

        //called when you turn off king mode or die
        //outputs the scores of your previous run and resets variables
        private void reignEnded() {
            UI.Notify("Your reign has come to an end. ~n~Time: " + secsToTime(time_king) + "~n~Level: " + (current_level+1) + 
                "~n~Foe Kills: " + kills + "~n~Peasant Kills: " + peasant_kills + "~n~~n~Score: " + score);
            king = false;
            time_king = 0;
            clock = 0;
            kills = 0;
            score = 0;
            peasant_kills = 0;
            get_on_bike_timer = GET_ON_BIKE_TIME;
            current_level = -1;
            //Rage.Game.RawFrameRender -= drawKingUI;
            Game.MaxWantedLevel = 5;

            for (int i = foes.Count - 1; i >= 0; i--) {
                removeFoe(foes[i]);
            }
        }
        
        private static string secsToTime(long secs) {
            //format this nicer
            return TimeSpan.FromSeconds(secs).ToString(@"mm\:ss");
        }

        private void onKeyUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.NumPad3) {
                if (!king) {
                    if (isOnBike(player)) {
                        //new king code
                        UI.Notify("You are now the King of the Bikes. Hunt down and destroy those who oppose you!");
                        king = true;
                        player.Armor = 100;
                        //stop player from switching characters somehow would be good
                        if (!can_be_wanted) {
                            Game.MaxWantedLevel = 0;
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
            Vehicle v = World.CreateVehicle(VehicleHash.Bati, (player.Position + player.ForwardVector * 5.0f));

            v.DirtLevel = 0f;
            v.CustomPrimaryColor = Color.FromArgb(0, 51, 204);
            v.CustomSecondaryColor = Color.Gold;
            v.NumberPlate = "THE KING";

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
