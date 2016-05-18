using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;

namespace KingOfTheBikes {
    class EnemySpawner {
        private static Random rng = new Random(DateTime.Now.Millisecond);

        //compress these into one data structure
        static int[] level_mins = { 1, 1, 2, 2 },
                level_maxs = { 2, 3, 4, 4 },
                power_pts = { 100, 150, 200, 250 };

        //maybe try putting this array in KOTB or another class entirely
        public void howDoYouInitializeThisArray() {
            Model[][] possible_foes = new Model[KingOfTheBikes.NUM_LEVELS][];

            possible_foes[0] = { PedHash.BallaEast01GMY, PedHash.BallaOrig01GMY };
        }

        public static Target[] spawn_level(int level) {
            switch(level) {
                case 1:
                    int numfoes = rng.Next(level_mins[level], level_maxs[level]+1);
                    //take away 50 pp for each extra enemy spawned
                    int pp = power_pts[level] - (numfoes - level_mins[level]) * 50;
                    return init_foes(numfoes, pp);
                //wont happen
                default:
                    return null;
            }
        }

        /*private static Target[] level_1() {
            const int NUM_ENEMIES = 2;
            Target[] ret = new Target[NUM_ENEMIES];
            var ballamodel = new GTA.Model(PedHash.BallaEast01GMY);
            var vmodel = new GTA.Model(VehicleHash.PCJ);

            //ballas are uncoordinated - they spawn in different spots!
            for (int i = 0; i < ret.Length; i++) {
                Vector3 loc = getFoeSpawnLoc();
                //spawn em i meters apart so they don't get stuck in each other!
                if(i % 2 == 0)
                    loc.X += i;
                else 
                    loc.X -= i;
                                
                Ped p = GTA.World.CreatePed(ballamodel, loc);
                Vehicle v = GTA.World.CreateVehicle(vmodel, loc);
                ret[i] = applyFoeSettings(p, v, 0);
            }
    
            return ret;
        }

        private static Target[] level_2() {
            const int NUM_ENEMIES = 4;
            Target[] ret = new Target[NUM_ENEMIES];
            var ballamodel = new GTA.Model(PedHash.BallaEast01GMY);
            var vmodel = new GTA.Model(VehicleHash.PCJ);

            //ballas are uncoordinated - they spawn in different spots!
            for (int i = 0; i < ret.Length; i++) {
                Vector3 loc = getFoeSpawnLoc();
                Ped p = GTA.World.CreatePed(ballamodel, loc);
                Vehicle v = GTA.World.CreateVehicle(vmodel, loc);
                ret[i] = applyFoeSettings(p, v, 0);
            }

            return ret;
        }*/

        private static Target[] init_foes(int numfoes, int power_points) {
            Target[] ret = new Target[numfoes];

            //foes are coordinated for now.
            Vector3 loc = getFoeSpawnLoc();
            for(int i = 0; i < ret.Length; i++) { 
                power_points = applyFoeSettings(ref ret[i], loc, power_points);
            }

        }

        private static int applyFoeSettings(ref Target t, Vector3 loc, int power_points) {
            //any foe has these settings
            Ped foe = World.CreatePed()
            foe.IsEnemy = true;
            foe.CanSwitchWeapons = true;
            foe.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);
            foe.DrivingStyle = DrivingStyle.AvoidTrafficExtremely;


            return power_points;
        }

        //power points to be used for dynamic enemy generation
        /*private static Target applyFoeSettings(Ped foe, Vector3 loc, int power_points) {
            foe.Weapons.Give(GTA.Native.WeaponHash.MicroSMG, 1000, true, true);
            foe.IsEnemy = true;
            foe.CanSwitchWeapons = true;
            foe.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);
            foe.DrivingStyle = DrivingStyle.AvoidTrafficExtremely;

            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, foe.Handle, KingOfTheBikes.foegroup);

            var vmodel = new GTA.Model(VehicleHash.PCJ);
            Vehicle v = GTA.World.CreateVehicle(vmodel, loc);

            Function.Call(Hash.SET_PED_INTO_VEHICLE, foe, v, -1);
            foe.Task.FightAgainst(Game.Player.Character);

            GTA.Blip b = foe.AddBlip();
            b.Color = BlipColor.Red;

            return new Target(foe, v, b, true);
        }*/

        //spawn randomizer constants
        private const int   MIN_FROM_PLAYER_DIST = 150,
                            MAX_FROM_PLAYER_DIST = 250,
                            //take the reciprocal to get chance enemies spawn in front of player
                            FRONT_SPAWN_CHANCE = 3;

        //returns a randomized location MIN-MAX distance behind player, with a 1/FRONT_SPAWN_CHANCE chance to be in front of player
        public static GTA.Math.Vector3 getFoeSpawnLoc() {
            //enemies spawn this many - this many units behind the player
            float dist_from_player = -rng.Next(MIN_FROM_PLAYER_DIST, MAX_FROM_PLAYER_DIST);
            //if they're coordinated
            if (rng.Next(0, FRONT_SPAWN_CHANCE) == 0) {
                dist_from_player = -dist_from_player;
            }
            //TODO if they're not, do more randomization
            Ped player = Game.Player.Character;

            GTA.Math.Vector3 spawn_loc = player.Position + player.ForwardVector * dist_from_player;

            return getVehicleSafeCoord(spawn_loc);
        }

        //accepts a position and returns the position of closest vehicle node to that position
        //will prefer major nodes (paved roads) but will default to regular nodes if the major node is too far.
        private static GTA.Math.Vector3 getVehicleSafeCoord(GTA.Math.Vector3 pos) {
            OutputArgument oa = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_CLOSEST_MAJOR_VEHICLE_NODE, pos.X, pos.Y, pos.Z, oa, 3.0, 2)) {
                //if the node is too far, should get a regular vehicle node
                Vector3 result = oa.GetResult<GTA.Math.Vector3>();
                float diff = Vector3.Distance(result, pos);
                //could define another constant for this
                if (diff > MAX_FROM_PLAYER_DIST*2) {
                    if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE, pos.X, pos.Y, pos.Z, oa, 1, 3.0, 0)) {
                        return oa.GetResult<GTA.Math.Vector3>();
                    }
                    else {
                        Logger.log("ERROR getting vehicle safe coord");
                        return GTA.Math.Vector3.Zero;
                    }
                }
                else {
                    return result;
                }
            }
            else {
                Logger.log("ERROR2 getting vehicle safe coord");
                return GTA.Math.Vector3.Zero;
            }
        }
    }

    class Target {
        //convenience data type class
        //holds ped, their vehicle, their blip. makes iterating through enemies and removing dead ones etc. easier

        public Ped p { get; }
        public Vehicle v { get; }
        public Blip b { get; }
        public bool isAggro { get; }

        public Target(Ped p, Vehicle v, Blip b, bool isAggro) {
            this.p = p;
            this.v = v;
            this.b = b;
            this.isAggro = isAggro;
        }
    }

}
