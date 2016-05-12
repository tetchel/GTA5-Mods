[assembly: Rage.Attributes.Plugin("King of the Bikes", Description = "Rule the Road", Author = "TimE")]

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
        //these fields are reset each game
        //in secs
        private long time_king = 0;
        //in ticks
        private int clock = -1;
        private int kills = 0;
        private int peasant_kills = 0;
        private List<Target> foes = new List<Target>(50);
        private int get_on_bike_timer = GET_ON_BIKE_TIME;

        private Random rng = new Random();

        private int foegroup;

        private const int INTERVAL = 1000;
        private const int GET_ON_BIKE_TIME = 16;
        private const int LONG_MESSAGE_DURATION = 5000;
        private const float FIND_PEASANT_RADIUS = 100f;
        private const float PEASANT_ESCAPE_RADIUS = 2000f;


        private int current_level = 0;

        public KingOfTheBikes() {
            //ticks every 5s
            this.Interval = INTERVAL;
            this.Tick += onTick;
            this.KeyUp += onKeyUp;
            this.KeyDown += onKeyDown;

            player = GTA.Game.Player.Character;

            foegroup = GTA.World.AddRelationshipGroup("KOTB_FOES");
            GTA.World.SetRelationshipBetweenGroups(GTA.Relationship.Hate, foegroup, player.RelationshipGroup);
            GTA.World.SetRelationshipBetweenGroups(GTA.Relationship.Hate, player.RelationshipGroup, foegroup);
        }

        private void onTick(object sender, EventArgs e) {
            if(king) {
                clock++;                                
                clock %= 10;        //# ticks in a cycle

                time_king++;
                //TODO hack (this shouldn't need to be done more than once!)
                if (clock % 5 == 0)
                    GTA.Game.MaxWantedLevel = 0;

                UI.ShowSubtitle("~b~" + secsToTime(time_king) + "~s~, ~r~" + kills + " ~s~rebels destroyed, ~g~" + peasant_kills + " ~s~peasants put down", INTERVAL);

                //player can't be off bike for more than GET_ON_BIKE_TIME ticks
                if (!isOnBike(player)) {
                    if(get_on_bike_timer < GET_ON_BIKE_TIME && player.IsAlive)
                        UI.ShowSubtitle("GET BACK ON YOUR BIKE!! ~r~" + get_on_bike_timer, INTERVAL);

                    get_on_bike_timer--;

                    if (get_on_bike_timer == 0) {
                        reignEnded();
                        UI.ShowSubtitle("~r~You should have gotten back on your bike.", LONG_MESSAGE_DURATION);
                    }
                }
                //reset timer when player gets back on a bike
                if (get_on_bike_timer != GET_ON_BIKE_TIME && isOnBike(player)) {
                    get_on_bike_timer = GET_ON_BIKE_TIME;
                }

                //only check every other second. idk if this makes a significant difference.
                if (clock % 2 == 0) {
                    GTA.Ped[] nearby = GTA.World.GetNearbyPeds(player, FIND_PEASANT_RADIUS);
                    foreach (GTA.Ped p in nearby) {
                        if (p.IsInVehicle() && isOnBike(p) && !isFoe(p)) {
                            GTA.Blip b = p.AddBlip();
                            b.Color = BlipColor.Green;
                            p.IsEnemy = true;
                            p.IsPersistent = true;
                            foes.Add(new Target(p, p.CurrentVehicle, b, false));
                        }
                    }
                }

                Vector2 player2d = new Vector2(player.Position.X, player.Position.Y);
                //check for and remove dead enemies
                for (int i = foes.Count - 1; i >= 0; i--) {
                    if (!foes[i].p.IsAlive) {
                        if (foes[i].isAggro)
                            kills++;
                        else
                            peasant_kills++;

                        removeFoe(foes[i]);
                        // you get bullets for killing someone. why not
                        //AP pistol would work too
                        if(player.Weapons.HasWeapon(GTA.Native.WeaponHash.MicroSMG)) {
                            Function.Call(Hash.ADD_AMMO_TO_PED, player, (int)WeaponHash.MicroSMG, 50);
                        }
                    }
                    else if(!foes[i].isAggro) {
                        Vector2 peasant2d = new Vector2(foes[i].p.Position.X, foes[i].p.Position.Y);                        
                        if (Vector2.Distance(player2d, peasant2d) > PEASANT_ESCAPE_RADIUS) {
                            removeFoe(foes[i]);
                            //UI.ShowSubtitle("~r~You let a peasant escape your kingdom.", 2000);
                            //reignEnded();
                        }
                    }
                }

                //enemy spawn code
                if (spawn_foes && clock == 0) {
                    GTA.Math.Vector3 spawn_loc = getFoeSpawnLoc();

                    var vmodel = new GTA.Model(VehicleHash.PCJ);
                    GTA.Vehicle v = GTA.World.CreateVehicle(vmodel, spawn_loc);
                    
                    var ballamodel = new GTA.Model(PedHash.BallaEast01GMY);
                    GTA.Ped foe = GTA.World.CreatePed(ballamodel, spawn_loc);
                    foe.Weapons.Give(GTA.Native.WeaponHash.MicroSMG, 1000, true, true);
                    foe.IsEnemy = true;
                    foe.CanSwitchWeapons = true;
                    foe.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);
                    foe.DrivingStyle = DrivingStyle.AvoidTrafficExtremely;

                    Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, foe.Handle, foegroup);

                    Function.Call(Hash.SET_PED_INTO_VEHICLE, foe, v, -1);
                    foe.Task.FightAgainst(player);

                    GTA.Blip b = foe.AddBlip();
                    b.Color = BlipColor.Red;

                    foes.Add(new Target(foe, v, b, true));
                }
            }

            if(king && !player.IsAlive) {
                reignEnded();
            }
        }
        
        private void removeFoe(Target e) {
            e.p.MarkAsNoLongerNeeded();
            e.v.MarkAsNoLongerNeeded();
            e.b.Remove();
            foes.Remove(e);
        }

        private bool isFoe(Ped p) {
            foreach(Target e in foes) {
                if(e.p.Equals(p)) {
                    return true;
                }
            }
            return false;
        }

        /*static void drawKingUI(object sender, GraphicsEventArgs e) {
            //king UI here
            //e.Graphics.DrawText(secsToTime(time_king), "Arial", 28f, new PointF(Rage.Game.Resolution.Width/60, Rage.Game.Resolution.Height/60), Color.FloralWhite);
        }*/

        private void onKeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
        }

        private void onKeyUp(object sender, System.Windows.Forms.KeyEventArgs e) {
            if (e.KeyCode == Keys.NumPad3) {
                if (!king) {
                    if (isOnBike(player)) {
                        UI.Notify("You are now the King of the Bikes. Hunt down and destroy those who oppose you!");
                        king = true;
                        clock = 0;
                        player.Armor = 100;
                        //stop player from switching characters somehow
                        //Rage.Game.RawFrameRender += drawKingUI;
                        if (!cops)
                            GTA.Game.MaxWantedLevel = 0;
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
        }

        private bool isOnBike(GTA.Ped p) {
            GTA.Vehicle v = p.CurrentVehicle;
            return  v != null &&
                    (v.ClassType == VehicleClass.Motorcycles || v.ClassType == VehicleClass.Cycles);
        }

        //called when you turn off king mode or die
        private void reignEnded() {
            UI.Notify("Your reign has come to an end. ~n~Time: " + secsToTime(time_king) + "~n~Foe Kills: " + kills + "~n~Peasant Kills: " + peasant_kills);
            king = false;
            time_king = 0;
            clock = -1;
            kills = 0;
            peasant_kills = 0;
            //Rage.Game.RawFrameRender -= drawKingUI;
            GTA.Game.MaxWantedLevel = 5;

            for (int i = foes.Count - 1; i >= 0; i--) {
                removeFoe(foes[i]);
            }
        }

        private GTA.Math.Vector3 getFoeSpawnLoc() {
            float dist_from_player = -rng.Next(150, 250);
            //sometimes enemy spawns in front of you. maybe could make this more interesting
            if(rng.Next(0,3) == 0) {
                dist_from_player = -dist_from_player;
            }
            GTA.Math.Vector3 spawn_loc = player.Position + player.ForwardVector * dist_from_player;

            return getVehicleSafeCoord(spawn_loc, true, 0);
        }

        private GTA.Math.Vector3 getVehicleSafeCoord(GTA.Math.Vector3 pos, bool onGround, int flags) {
            OutputArgument oa = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE, pos.X, pos.Y, pos.Z, oa, 1, 3.0, 0)) {
                return oa.GetResult<GTA.Math.Vector3>();
            }
            else {
                return GTA.Math.Vector3.Zero;
            }
        }

        private static void log(string s) {
            using (System.IO.StreamWriter file =
                        new System.IO.StreamWriter(@"E:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\scripts\A LOG.txt", true)) {
                file.WriteLine(s);
            }
        }

        private static void log(GTA.Math.Vector3 v, string label) {
            string output = string.Format("{0}: X: {1} Y: {2} Z: {3}", label.ToUpper(), v.X, v.Y, v.Z);
            log(output);
        }

        private static String secsToTime(long secs) {
            //format this nicer
            return TimeSpan.FromSeconds(secs).ToString(@"mm\:ss");
        }

    }
}
