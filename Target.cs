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
        public static int NUM_LEVELS { get; } = 5;


        private static readonly int CHEAPEST_UPGRADE = 10,
                                    ENEMY_COST = 100,
                                    BASE_WEAPON_COST = 20,
                                    VEH_UPGRADE_COST = 2 * CHEAPEST_UPGRADE;

        private static Random rng = new Random(DateTime.Now.Millisecond);

        //compress these into one data structure
        private static readonly int[]   level_mins = { 1, 1, 1, 2, 2 },
                                        level_maxs = { 2, 3, 4, 3, 4 },
                                        //power_pts_values[i] must be g.t. level_maxs[i]*ENEMY_COST
                                        power_pts_values = { 250, 350, 450, 550, 650};

        private static readonly Model[][] possible_foes = new Model[][] {
            new Model[] { PedHash.Vagos01GFY, PedHash.VagosFun01 },
            new Model[] { PedHash.BallaOrig01GMY, PedHash.BallaEast01GMY, PedHash.Ballas01GFY, PedHash.BallaSout01GMY },
            new Model[] { PedHash.Vagos01GFY, PedHash.VagosFun01 },
            new Model[] { PedHash.BallaOrig01GMY, PedHash.BallaEast01GMY, PedHash.Ballas01GFY, PedHash.BallaSout01GMY },
            new Model[] { PedHash.Vagos01GFY, PedHash.VagosFun01 }
        };

        private static readonly WeaponHash[] possible_driveby_weapons = { WeaponHash.CombatPistol, WeaponHash.SawnOffShotgun,
                                                                          WeaponHash.APPistol, WeaponHash.MicroSMG };
        //private static readonly WeaponHash[] possible_onfoot_weapons =  {   WeaponHash.SMG, WeaponHash.PumpShotgun, WeaponHash.AssaultRifle,
        //                                                                    WeaponHash.GrenadeLauncher, WeaponHash.RPG };

        private static readonly VehicleHash[] possible_bikes = {    VehicleHash.Bagger, VehicleHash.PCJ, VehicleHash.Hexer,
                                                                    VehicleHash.Daemon, VehicleHash.Vader, VehicleHash.Double, VehicleHash.Bati };

        private static Dictionary<WeaponHash, int> weapon_costs = new Dictionary<WeaponHash, int>();
        //private static Dictionary<VehicleHash, int> vehicle_costs;
        //private static Dictionary<int, int> armor_costs;

        static EnemySpawner() {
            int[] weapon_costs_array = { 0, BASE_WEAPON_COST + 20, BASE_WEAPON_COST + 30, BASE_WEAPON_COST };
            //initialize the map for easier cost lookup
            for (int i = 0; i < possible_driveby_weapons.Length; i++) {
                weapon_costs.Add(possible_driveby_weapons[i], weapon_costs_array[i]);
            }
        }

        public static Target[] spawn_level(int level) {
            int numfoes = rng.Next(level_mins[level], level_maxs[level]+1);
            //take away ENEMY_COST pp for each enemy spawned
            int pp = power_pts_values[level] - (numfoes) * ENEMY_COST;
            Logger.log("init pps: " + power_pts_values[level] + " corrected: " + pp);
            return init_foes(numfoes, possible_foes[level], pp);
        }


        private static Target[] init_foes(int numfoes, Model[] possible_foes, int power_points) {
            //first, distribute power points to each foe
            int[] assigned_pps = new int[numfoes];
            int j = 0;
            for (j = 0; ; j++, j %= assigned_pps.Length) {
                if (power_points < CHEAPEST_UPGRADE * 2) {
                    assigned_pps[j] += power_points;
                    break;
                }
                //each foe can only take half the remaining power points
                int r = rng.Next(CHEAPEST_UPGRADE, (power_points / 2)+1);
                //round up to nearest CHEAPEST
                int mod = r % CHEAPEST_UPGRADE;
                if(mod != 0) {
                    r += CHEAPEST_UPGRADE - mod;
                }
                assigned_pps[j] += r;
                power_points -= r;
            }

            Logger.log("Took " + j + " iterations to distribute points");
            Logger.log("The assigned pps are :");
            for(int i = 0; i < assigned_pps.Length; i++) {
                Logger.log("" + assigned_pps[i]);
            }

            //list of foes
            Target[] ret = new Target[numfoes];
            //foes are coordinated for now.
            Vector3 loc = getFoeSpawnLoc();
            //loop thru foes and randomize their models and equipment
            for(int i = 0; i < ret.Length; i++) {
                //pick a random model for the current enemy from the current level's list of enemies
                Model m = possible_foes[rng.Next(possible_foes.Length)];

                //spawn em i meters apart so they don't get stuck!
                if (i % 2 == 0)
                    loc.X += i;
                else
                    loc.X -= i;

                power_points = applyFoeSettings(ref ret[i], m, loc, assigned_pps[i]);
                if(power_points > 0) {
                    //give em to the next guy if possible. if it's the last guy they go to waste for now
                    if(i < assigned_pps.Length - 1)
                        assigned_pps[i + 1] += power_points;
                }
            }

            return ret;
        }

        private static int applyFoeSettings(ref Target t, Model m, Vector3 loc, int power_points) {
            //any foe has these settings
            Ped foe = World.CreatePed(m, loc);
            foe.IsEnemy = true;
            foe.CanSwitchWeapons = true;
            foe.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);
            foe.DrivingStyle = DrivingStyle.AvoidTrafficExtremely;
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, foe.Handle, KingOfTheBikes.foegroup);
            Blip b = foe.AddBlip();
            b.Color = BlipColor.Red;

            //free weapon
            int driveby_index = 0;
            //int onfoot_weapon = 0;
            //free bike
            Model vmodel = new Model(VehicleHash.Sanchez);
            //tracks how many vehicle upgrades have gone down (constant cost for vehicle upgrades)
            int vehicle_index = 0;
            int armor = 0;

            int count = 0;

            //don't try and upgrade weapon if can't afford it
            bool can_upgrade_weapon = BASE_WEAPON_COST <= power_points,
                can_upgrade_vehicle = true,
                can_upgrade_armor = true;
            //loop until they run out of PP
            while (power_points >= CHEAPEST_UPGRADE) {
                Logger.log("PPs: " + power_points);
                int weapon_upgrade_cost = -1;                
                if(can_upgrade_weapon) {
                    //check if already has best weapon
                    if(driveby_index == possible_driveby_weapons.Length - 1) {
                        can_upgrade_weapon = false;
                    }
                    //if not best weapon compute if can afford next upgrade
                    else {
                        //compute the cost of an upgrade (cost of new weapon - cost of old weapon)
                        int old_cost, new_cost;
                        weapon_costs.TryGetValue(possible_driveby_weapons[driveby_index], out old_cost);
                        weapon_costs.TryGetValue(possible_driveby_weapons[driveby_index+1], out new_cost);
                        weapon_upgrade_cost = new_cost - old_cost;
                        //can you afford it?
                        can_upgrade_weapon = weapon_upgrade_cost <= power_points;
                        Logger.log("weapon upgrade would cost " + weapon_upgrade_cost);
                    }
                }
                if (can_upgrade_vehicle)
                    can_upgrade_vehicle = VEH_UPGRADE_COST <= power_points && vehicle_index < possible_bikes.Length - 1;
                if (can_upgrade_armor)
                    can_upgrade_armor = armor < 100;

                Logger.log("can_upgrades: " + can_upgrade_weapon + " " + can_upgrade_vehicle + " " + can_upgrade_armor);

                int min_value = 0;
                int roll = rng.Next(min_value,3);
                //upgrade weapon
                if(roll == 0 && can_upgrade_weapon) {
                    driveby_index++;
                    power_points -= weapon_upgrade_cost;
                    Logger.log("Upgraded weapon, PPs now " + power_points);
                }
                //upgrade vehicle
                else if(roll == 1 && can_upgrade_armor) {
                    //can always afford vehicle upgrade
                    vehicle_index++;
                    power_points -= VEH_UPGRADE_COST;
                    Logger.log("Upgraded bike, PPs now " + power_points);
                }
                //upgrade armor
                else if (can_upgrade_armor) {
                    armor += 25;
                    power_points -= CHEAPEST_UPGRADE;
                    Logger.log("Upgraded armor, PPs now " + power_points);
                }
                else {
                    //no more upgrades to be had
                    break;
                }
                count++;
            }

            Logger.log("Took " + count + " iterations to generate enemy");
            int ammo_to_give = 2000;
            //give gun(s)
            foe.Weapons.Give(possible_driveby_weapons[driveby_index], ammo_to_give, true, true);

            //modify ammo_to_give for onfoot weapon too
            /*if (onfoot_weapon != 0) {
                //less ammo for explosives
                if (onfoot_weapon == WeaponHash.GrenadeLauncher || onfoot_weapon == WeaponHash.RPG)
                    ammo_to_give = 10;

                foe.Weapons.Give(onfoot_weapon, ammo_to_give, true, true);
            }*/
            //give vehicle
            Vehicle v = World.CreateVehicle(possible_bikes[vehicle_index], loc);
            Function.Call(Hash.SET_PED_INTO_VEHICLE, foe, v, -1);

            foe.Task.FightAgainst(Game.Player.Character);

            //create the Target object from the generated enemy
            t = new Target(foe, v, b, true);
            Logger.log("Enemy created :D");

            return power_points;
        }

        //spawn randomizer constants
        private const int   MIN_FROM_PLAYER_DIST = 150,
                            MAX_FROM_PLAYER_DIST = 250,
                            //take the reciprocal to get chance enemies spawn in front of player
                            FRONT_SPAWN_CHANCE = 3;

        //returns a randomized location MIN-MAX distance behind player, with a 1/FRONT_SPAWN_CHANCE chance to be in front of player
        public static Vector3 getFoeSpawnLoc() {
            //enemies spawn this many - this many units behind the player
            float dist_from_player = -rng.Next(MIN_FROM_PLAYER_DIST, MAX_FROM_PLAYER_DIST);
            //if they're coordinated
            if (rng.Next(0, FRONT_SPAWN_CHANCE) == 0) {
                dist_from_player = -dist_from_player;
            }
            //TODO if they're not, do more randomization
            Ped player = Game.Player.Character;

            Vector3 spawn_loc = player.Position + player.ForwardVector * dist_from_player;

            return getVehicleSafeCoord(spawn_loc);
        }

        //accepts a position and returns the position of closest vehicle node to that position
        //will prefer major nodes (paved roads) but will default to regular nodes if the major node is too far.
        private static Vector3 getVehicleSafeCoord(Vector3 pos) {
            OutputArgument oa = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_CLOSEST_MAJOR_VEHICLE_NODE, pos.X, pos.Y, pos.Z, oa, 3.0, 2)) {
                //if the node is too far, should get a regular vehicle node
                Vector3 result = oa.GetResult<Vector3>();
                float diff = Vector3.Distance(result, pos);
                //could define another constant for this
                if (diff > MAX_FROM_PLAYER_DIST*2) {
                    if (Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE, pos.X, pos.Y, pos.Z, oa, 1, 3.0, 0)) {
                        return oa.GetResult<Vector3>();
                    }
                    else {
                        Logger.log("ERROR getting vehicle safe coord");
                        return Vector3.Zero;
                    }
                }
                else {
                    return result;
                }
            }
            else {
                Logger.log("ERROR2 getting vehicle safe coord");
                return Vector3.Zero;
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
