using System;
using System.IO;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using System.Linq;
using System.Windows.Forms;

class KillCounter : Script {

    private static KillCounter _instance;

    private Ped player;

    private int killCount = 0;
    private int policeKillCount = 0;
    private int score = 0;

    private int highScore = 0;

    private const int INTERVAL = 1000;                //time between ticks in ms
    private const int RADIUS = 500;

    private HashSet<Ped> killedPeds = new HashSet<Ped>();

    private bool enabled = true;

    private bool gotKill = false;

    private const string FILENAME = "highscore.txt";

    public static KillCounter instance() {
        if(_instance == null) {
            _instance = new KillCounter();
        }
        return _instance;
    }

    public KillCounter() {

        Tick += onTick;
        KeyUp += onKeyUp;

        player = Game.Player.Character;

        string hs = "";
        try {
            hs = File.ReadAllText(FILENAME);
        }
        catch(FileNotFoundException e) {
            // doesn't matter
        }

        if (!int.TryParse(hs, out highScore)) {
            highScore = 0;
        }
        //UI.Notify("Init killCounter");
    }

    public void enable() {
        if(!enabled) {
            UI.Notify("Enable KillCounter");
        }
        enabled = true;
    }

    public void disable() {
        if (enabled) {
            UI.Notify("Disable KillCounter");
        }
        enabled = false;
    }

    public void toggle() {
        if(enabled) {
            disable();
        }
        else {
            enable();
        }
    }

    private void onKeyUp(object sender, KeyEventArgs e) {
        if (e.KeyCode == Keys.NumPad0) {
            toggle();
        }
    }

    public void onTick(object sender, EventArgs e) {
        if(!enabled) {
            //UI.Notify("Not enabled");
            return;
        }

        //UI.Notify("Yes!");
        if (!player.IsAlive) {
            // could add a flag to call this just once per death
            onPlayerDeath();
            return;
        }

        Ped[] nearby = World.GetNearbyPeds(player, RADIUS);
        // UI.Notify(nearby.Length + " mans");
        foreach (Ped p in nearby) {

            if(!p.IsAlive && !killedPeds.Contains(p)) {
                // got a kill?
                onKill(p);
            }
        }

        if(gotKill) {
            // Only update the UI if the kill count has changed.

            int civKills = killCount - policeKillCount;
            //double score = civKills + (0.01 * policeKillCount);
            UI.ShowSubtitle("~r~" + civKills + "~b~  " + policeKillCount + "~g~  " + score, INTERVAL*1000);
            gotKill = false;
        }
    }

    private PedHash[] COP_HASHES = new PedHash[] {
        PedHash.Cop01SFY, PedHash.Cop01SMY, PedHash.Hwaycop01SMY, PedHash.Snowcop01SMM,
        PedHash.Sheriff01SFY, PedHash.Sheriff01SMY,
        PedHash.Swat01SMY, PedHash.FibSec01, PedHash.FibSec01SMM
    };

    // call when you kill p
    private void onKill(Ped p) {
        killCount++;
        killedPeds.Add(p);

        bool isCop = false;
        // check if it's a police officer you killed
        foreach (int copHash in COP_HASHES) {
            if(Function.Call<bool>(Hash.IS_PED_MODEL, p, copHash)) {
                // they r a cop
                policeKillCount++;
                isCop = true;
                break;
            }
        }

        // update score
        if(isCop) {
            score += 50;
        }
        else {
            if(p.IsInVehicle()) {
                score += 300;
            }
            else {
                score += 100;
            }
        }

        //UI.Notify("U mucked");
        gotKill = true;
    }

    public void resetScore() {
        UI.Notify("Resetting score");
        score = 0;
    }

    private void onPlayerDeath() {
        if(killCount != 0) {
            string notif = "You got " + killCount + " kills wow good job and " +
                    (killCount - policeKillCount) + " were civilians.\nFinal Score: " + score;

            string newhighscore = "You set a new highscore!";
            string notNewhighscore = "High Score: " + highScore;

            if(score > highScore) {
                highScore = score;
                UI.Notify(notif + "\n" + newhighscore);

                // update high score
                File.WriteAllText(FILENAME, highScore + ""); 
            }
            else {
                UI.Notify(notif + "\n" + notNewhighscore);
            }
        }
        killCount = 0;
        policeKillCount = 0;
        score = 0;
    }
}
