using System;
using System.Windows.Forms;
using System.Drawing;
using GTA;
using GTA.Native;
using GTA.Math;
//using Rage;
using System.Collections.Generic;
//using RAGENativeUI.Elements;
using System.Linq;

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
                peasant_kills = 0,
                current_level = -1,
                get_on_bike_timer = GET_ON_BIKE_TIME;

    private List<Target> foes = new List<Target>(50);
    //private float enemy_accuracy_mult = ENEMY_ACCURACY_INIT;

    private KillCounter killCounter;

    public static readonly int INTERVAL = 1000;                //time between ticks in ms
    private const int
                        TICKS_PER_LEVEL = 100,          //level duration in ticks
                        GET_ON_BIKE_TIME = 26,          //amount of time player has to get back on bike until loses
                        GET_ON_BIKE_FAIL_TIME = 10,     //this many or fewer seconds remain, lose points each tick
                        LONG_MESSAGE_DURATION = 5000,
                        //points values - could vary by level or something
                        POINTS_PER_TICK = 1,
                        POINTS_LOST_OFFBIKE_PER_SECOND = 100,
                        SPAWN_INTERVAL = 10;

    private const float FIND_PEASANT_RADIUS = 100f,     //performance impact ?
                        PEASANT_ESCAPE_RADIUS = 1000f,
                        HEADSHOT_BONUS_RATIO = 2f;

    public KingOfTheBikes() {
        Interval = INTERVAL;
        Tick += onTick;
        KeyUp += onKeyUp;

        player = Game.Player.Character;

        killCounter = KillCounter.instance();
    }

    public bool isKing() {
        return king;
    }

    private void onTick(object sender, EventArgs e) {
        if (king) {
            // if just became king
            if(clock == 0) {
                Logger.log("I'm king now");
                killCounter.pedKillValueFunction = pedKillValueKOTB;
                killCounter.subtitleFunction = getSubtitleKOTB;
            }

            //player.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 1);

            time_king++;
            // for some reason player can still get a wanted level even though it's supposed to be disabled
            // so call this constantly to make sure the wanted level is removed - because no cops come after KOTB
            Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, 0, false);

            //UI.ShowSubtitle("~b~" + score, INTERVAL);

            //player can't be off bike for more than GET_ON_BIKE_TIME ticks
            if (!isOnBike(player)) {
                get_on_bike_timer--;
                //turn the score red or something to indicate losing points
                if (get_on_bike_timer < GET_ON_BIKE_TIME && player.IsAlive) {
                    string color = get_on_bike_timer <= GET_ON_BIKE_FAIL_TIME ? "~r~ " : "~b~ ";
                    UI.ShowSubtitle(color + killCounter.getScore() + " ~s~GET ON A BIKE!! ~r~" + get_on_bike_timer, 
                        INTERVAL);
                }

                if (get_on_bike_timer <= GET_ON_BIKE_FAIL_TIME) {
                    //loses points each tick
                    killCounter.losePoints(POINTS_LOST_OFFBIKE_PER_SECOND);

                    if (get_on_bike_timer <= 0) {
                        reignEnded();
                        UI.ShowSubtitle("~r~You should have gotten back on a bike.", LONG_MESSAGE_DURATION);
                        return;
                    }
                }
            }
            else {
                //points pt only if on bike
                killCounter.givePoints(POINTS_PER_TICK * (current_level + 1));
            }

            //reset timer when player gets back on a bike
            if (get_on_bike_timer != GET_ON_BIKE_TIME && isOnBike(player)) {
                get_on_bike_timer = GET_ON_BIKE_TIME;
            }

            //Mark nearby on-bike peds as peasants
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
                        givePlayerAmmo();
                    }
                    else {
                        peasant_kills++;
                    }

                    removeFoe(foes[i]);
                }
                //check for and remove peasants that are too far away
                else if (!foes[i].isAggro) {
                    Vector2 peasant2d = new Vector2(foes[i].p.Position.X, foes[i].p.Position.Y);
                    if (Vector2.Distance(player2d, peasant2d) > PEASANT_ESCAPE_RADIUS) {
                        removeFoe(foes[i]);
                        //UI.ShowSubtitle("~r~You let a peasant escape your kingdom.", LONG_MESSAGE_DURATION);
                        //reignEnded();
                        //return;
                    }
                }
            }

            if (clock == 0) {
                //level is over
                current_level++;
                UI.Notify("Level " + (current_level + 1), true);
                player.Armor = 100;
                player.Health = player.MaxHealth;
            }
          
            // if all the aggressive foes are dead, decrement the clock so that it's now time for the next wave
            if(foes.All((f) => !f.isAggro)) {
                UI.Notify("No angry foes remain");
                for(int i = 0; i < SPAWN_INTERVAL; i++) {
                    if((clock - i) % SPAWN_INTERVAL == 0) {
                        clock -= i;
                    }
                }
            }
            // now check the clock to see if it's time to spawn enemies
            if (clock % SPAWN_INTERVAL == 0) {
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

    private bool isHeadshot(Ped p) {
        bool isHeadshot = false;
        OutputArgument oa = new OutputArgument();
        if (Function.Call<bool>(Hash.GET_PED_LAST_DAMAGE_BONE, p, oa)) {
            Bone outbone = (Bone)oa.GetResult<int>();
            isHeadshot = outbone == Bone.IK_Head || outbone == Bone.SKEL_Head;
        }
        return isHeadshot;
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
        else if (wp == WeaponHash.StickyBomb || wp == WeaponHash.Grenade) {
            ammo_to_give = 2;
        }

        UI.Notify("+ " + ammo_to_give + " " + wp.ToString());
        Function.Call(Hash.ADD_AMMO_TO_PED, player, (int)wp, ammo_to_give);
    }

    //perform cleanup on dead enemy or on all foes when dethroned
    private void removeFoe(Target targ) {
        //Logger.log("Removing foe: " + e.p.Model);
        //e.p.Task.ClearAllImmediately();
        targ.p.Task.ClearAll();
        targ.p.MarkAsNoLongerNeeded();
        targ.v.MarkAsNoLongerNeeded();
        //if(e.b != null)
        targ.b.Remove();
        foes.Remove(targ);
    }

    // returns if a ped is in the list of Targets
    private bool isFoe(Ped p) {
        foreach (Target e in foes) {
            if (e.p.Equals(p)) {
                return true;
            }
        }
        return false;
    }

    // self documenting
    public static bool isOnBike(Ped p) {
        Vehicle v = p.CurrentVehicle;
        return v != null &&
                (v.ClassType == VehicleClass.Motorcycles || v.ClassType == VehicleClass.Cycles);
    }

    // called when you turn off king mode or die
    // outputs the scores of your previous run and resets variables
    private void reignEnded() {
        Logger.log("reignEnded");
        UI.Notify("Your reign has come to an end. ~n~Time: " + secsToTime(time_king) + "~n~Level: "
            + (current_level + 1) + "~n~Foe Kills: " + kills + "~n~Peasant Kills: " + peasant_kills
            + "~n~Civ kills: " + killCounter.getCivKills() + "~n~~n~Score: " + killCounter.getScore());
        king = false;
        time_king = 0;
        clock = 0;
        kills = 0;
        peasant_kills = 0;
        get_on_bike_timer = GET_ON_BIKE_TIME;
        current_level = -1;
        Game.MaxWantedLevel = 5;
        killCounter.resetScore();
        killCounter.pedKillValueFunction = killCounter.pedKillValueDefault;
        killCounter.subtitleFunction = killCounter.subtitleFunction;

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
                    killCounter.resetScore();
                    //stop player from switching characters somehow would be good
                    
                    // Make the police ignore you - this does not work always, so Eplayer wanted level is also set to
                    // 0 on each frame.
                    if (!can_be_wanted) {
                        Game.MaxWantedLevel = 0;
                        Function.Call(Hash.SET_DISPATCH_COPS_FOR_PLAYER, Game.Player.Character, false);
                        Function.Call(Hash.SET_POLICE_IGNORE_PLAYER, Game.Player.Character, true);
                        Function.Call(Hash.CLEAR_PLAYER_WANTED_LEVEL, Game.Player.Character);
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, 0, false);
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, Game.Player.Character, false);
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

        // The rest of the code paths are not directly related to KOTB

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
        
    // Used by KillCounter
    private int pedKillValueKOTB(Ped p) {
        int value = 0;
        if(isOnBike(p)) {
            value = 5000;
        }
        else if(isFoe(p)) {
            value = (current_level + 1) * 100;
            if(isHeadshot(p)) {
                value *= 2;
            }
        }
        else if(p.IsInVehicle()) {
            value = 500;        
        }
        else {
            // just a regular guy
            value = 200;
        }
        return value;
    }

    private string getSubtitleKOTB() {
        return "~b~" + killCounter.getScore() + "~s~    ~r~" + kills + "  ~g~" + peasant_kills + "  ~s~" + 
            killCounter.getCivKills();
    }
}