using System;
using GTA;
using GTA.Native;
using GTA.Math;

class EnemySpawner {
    public static int NUM_LEVELS { get; } = 10;

    private static Random rng = new Random(DateTime.Now.Millisecond);

    private static readonly Level[] LEVELS = new Level[NUM_LEVELS];

    //group that enemies belong to so they don't attack each other
    private static readonly int foegroup = World.AddRelationshipGroup("KOTB_FOES");

    static EnemySpawner() {
        //initialize foegroup
        World.SetRelationshipBetweenGroups(Relationship.Hate, foegroup, Game.Player.Character.RelationshipGroup);
        World.SetRelationshipBetweenGroups(Relationship.Hate, Game.Player.Character.RelationshipGroup, foegroup);

        //initialize all level data structures here
        LEVELS[0] = new Level(1, Level.POSSIBLE_VEHICLES.Sanchez,
            WeaponHash.CombatPistol, Level.POSSIBLE_FOES.Vagos, 50);

        LEVELS[1] = new Level(1, Level.POSSIBLE_VEHICLES.Bagger, 
            WeaponHash.CombatPistol, WeaponHash.APPistol, 5, Level.POSSIBLE_FOES.Ballas, 60);

        LEVELS[2] = new Level(1, Level.POSSIBLE_VEHICLES.Hexer,
            WeaponHash.CombatPistol, WeaponHash.SawnOffShotgun, 2, Level.POSSIBLE_FOES.Lost, 70);

        LEVELS[3] = new Level(2, Level.POSSIBLE_VEHICLES.PCJ, 
            WeaponHash.CombatPistol, WeaponHash.SawnOffShotgun, 4, Level.POSSIBLE_FOES.Korean, 70);

        LEVELS[4] = new Level(2, Level.POSSIBLE_VEHICLES.Daemon, 
            WeaponHash.SawnOffShotgun, WeaponHash.APPistol, 4, Level.POSSIBLE_FOES.Vagos, 75);

        LEVELS[5] = new Level(2, Level.POSSIBLE_VEHICLES.PCJ, 
            WeaponHash.APPistol, Level.POSSIBLE_FOES.Ballas, 80);

        LEVELS[6] = new Level(3, Level.POSSIBLE_VEHICLES.Hexer, 
            WeaponHash.SawnOffShotgun, WeaponHash.APPistol, 4, Level.POSSIBLE_FOES.Lost, 75);

        LEVELS[7] = new Level(3, Level.POSSIBLE_VEHICLES.Daemon, 
            WeaponHash.APPistol, Level.POSSIBLE_FOES.Korean, 80);

        LEVELS[8] = new Level(3, Level.POSSIBLE_VEHICLES.Daemon, 
            WeaponHash.MicroSMG, Level.POSSIBLE_FOES.Vagos, 80);

        LEVELS[9] = new Level(4, Level.POSSIBLE_VEHICLES.Daemon, 
            WeaponHash.MicroSMG, Level.POSSIBLE_FOES.Ballas, 80);
    }

    public static int get_foes_to_levelup(int level_num) {
        return LEVELS[level_num].num_enemies;
    }

    public static Target[] spawn_level(int level_num) {
        Level level = LEVELS[level_num];

        //list of foes
        Target[] ret = new Target[level.num_enemies];
        //foes are coordinated for now.
        Vector3 loc = getFoeSpawnLoc();
        //loop thru foes and randomize their models and equipment
        for(int i = 0; i < ret.Length; i++) {
            //pick a random model for the current enemy from the current level's list of enemies
            Model m = level.possible_models[rng.Next(level.possible_models.Length)];

            //space them out so they don't get stuck
            if (i % 2 == 0)
                loc.X += 2*i;
            else
                loc.X -= 2*i;

            ret[i] = applyFoeSettings(m, loc, level.vehicle, level.rollForWeapon(), level.accuracy);
        }

        return ret;
    }

    private static Target applyFoeSettings(Model ped_model, Vector3 loc, Model vehicle_model, WeaponHash weapon, 
            int accuracy) {

        //any foe has these settings
        Ped foe = World.CreatePed(ped_model, loc);
        foe.IsEnemy = true;
        foe.CanSwitchWeapons = true;
        foe.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);
        foe.DrivingStyle = DrivingStyle.AvoidTrafficExtremely;
        Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, foe.Handle, foegroup);
        Blip b = foe.AddBlip();
        b.Color = BlipColor.Red;

        Vehicle v = World.CreateVehicle(vehicle_model, loc);
        //random color on vehicle
        Array colors = Enum.GetValues(typeof(VehicleColor));
        v.PrimaryColor = (VehicleColor)colors.GetValue(rng.Next(colors.Length));
        Function.Call(Hash.SET_PED_INTO_VEHICLE, foe, v, -1);

        foe.Weapons.Give(weapon, 2000, true, true);

        foe.Task.FightAgainst(Game.Player.Character);

        foe.Accuracy = accuracy;

        //create the Target object from the generated enemy
        return new Target(foe, v, b, true);
    }

    /*public static Target debugSpawnFoe(int level_num) {
        Level level = LEVELS[level_num];

        return applyFoeSettings(level.possible_models[rng.Next(level.possible_models.Length)], 
            Game.Player.Character.ForwardVector * 5.0f, level.vehicle, level.rollForWeapon());
    }*/

    //spawn randomizer constants
    private const int   MIN_FROM_PLAYER_DIST = 150,
                        MAX_FROM_PLAYER_DIST = 250,
                        //take the reciprocal to get chance enemies spawn in front of player
                        INVERSE_FRONT_SPAWN_CHANCE = 3;

    //returns a randomized location MIN-MAX distance behind player, with a 1/FRONT_SPAWN_CHANCE chance to be in front of player
    public static Vector3 getFoeSpawnLoc() {
        //enemies spawn min - max units behind the player
        float dist_from_player = -rng.Next(MIN_FROM_PLAYER_DIST, MAX_FROM_PLAYER_DIST);
        // Roll to see if will spawn behind or in front of player
        if (rng.Next(0, INVERSE_FRONT_SPAWN_CHANCE) == 0) {
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
    //holds ped, their vehicle, their blip. makes iterating through enemies and removing dead ones etc. easier in KOTB

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

//data type that holds relevant info for each level
class Level {
    private static Random rng = new Random(DateTime.Now.Millisecond);

    public enum POSSIBLE_FOES { Vagos, Ballas, Korean, Lost };
    private static readonly Model[][] possible_foes = new Model[][] {
        new Model[] { PedHash.Vagos01GFY, PedHash.VagosFun01 },
        new Model[] { PedHash.BallaOrig01GMY, PedHash.BallaEast01GMY, PedHash.Ballas01GFY, PedHash.BallaSout01GMY },
        new Model[] { PedHash.KorBoss01GMM, PedHash.KorLieut01GMY, PedHash.Korean01GMY, PedHash.Korean02GMY },
        new Model[] { PedHash.Lost01GFY, PedHash.Lost01GMY, PedHash.Lost02GMY, PedHash.Lost03GMY },
    };

    public enum POSSIBLE_VEHICLES { Sanchez, Bagger, PCJ, Hexer, Daemon, DoubleT, Bati };
    private static readonly Model[] possible_vehicles = new Model[] {
        new Model(VehicleHash.Sanchez), new Model(VehicleHash.Bagger), new Model(VehicleHash.PCJ),
        new Model(VehicleHash.Hexer), new Model(VehicleHash.Daemon), new Model(VehicleHash.Double),
        new Model(VehicleHash.Bati)
    };

    public int num_enemies { get; }         //per wave

    public Model[] possible_models { get; } //each ped model is selected randomly from this list. a different list for each level
    public Model vehicle { get; }           //everyone in a level comes with the same bike

    public WeaponHash freeWeapon { get; }   //Either they get this weapon or the rngWeapon, depending on a roll (see rollForWeapon)
    private int inverseChanceOfRngWeapon;
    public WeaponHash rngWeapon { get; }

    public int accuracy { get; }

    public Level(int num_enemies_, POSSIBLE_VEHICLES vehicleTypeIndex, WeaponHash freeWeapon_, POSSIBLE_FOES foesTypeIndex, int accuracy_) {
        num_enemies = num_enemies_;
        vehicle = possible_vehicles[(int)vehicleTypeIndex];
        freeWeapon = freeWeapon_;
        rngWeapon = 0;
        possible_models = possible_foes[(int)foesTypeIndex];
        accuracy = accuracy_;
    }

    public Level(int num_enemies_, POSSIBLE_VEHICLES vehicleTypeIndex, WeaponHash freeWeapon_, WeaponHash rngWeapon_, int inverseChanceOfRngWeapon_,
            POSSIBLE_FOES foesTypeIndex, int accuracy_) {
        num_enemies = num_enemies_;
        vehicle = possible_vehicles[(int)vehicleTypeIndex];
        freeWeapon = freeWeapon_;
        rngWeapon = rngWeapon_;
        inverseChanceOfRngWeapon = inverseChanceOfRngWeapon_;
        possible_models = possible_foes[(int)foesTypeIndex];
    }

    //if an rngWeapon exists, roll for it and return the result. if it doesn't exist return the free weapon
    public WeaponHash rollForWeapon() {
        if (rngWeapon != 0) {
            if (rng.Next(0, inverseChanceOfRngWeapon) == 0) {
                return rngWeapon;
            }
        }
        return freeWeapon;
    }
}
