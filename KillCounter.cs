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

    private int defaultHighscore = 0;
    private Dictionary<string, int> altHighscores = new Dictionary<string, int>();

    private const int INTERVAL = 1000;                //time between ticks in ms
    private const int RADIUS = 500;

    private HashSet<Ped> killedPeds = new HashSet<Ped>();

    private bool enabled = true;

    private const string FILENAME = "highscore";
    private const string FILE_EXT = ".txt";

    public Func<Ped, int> pedKillValueFunction { get; set; }
    public Func<string> subtitleFunction { get; set; }

    public static KillCounter instance() {
        if(_instance == null) {
            Logger.log("Error: KillCounter was null");
        }
        return _instance;
    }

    // Don't use constructor, use singleton instance (but constructor must be public for script purposes)
    public KillCounter() {
        Logger.log("Init KillCounter");

        _instance = this;
        Tick += onTick;
        KeyUp += onKeyUp;

        // Assign the default kill value function
        pedKillValueFunction = pedKillValueDefault;
        subtitleFunction = subtitleDefault;

        player = Game.Player.Character;

        string hs = "";
        try {
            hs = File.ReadAllText(getHighscoreFilename());
        }
        catch(FileNotFoundException) {
            // doesn't matter
        }

        if (!int.TryParse(hs, out defaultHighscore)) {
            defaultHighscore = 0;
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
                //UI.Notify("A KILL!");
                // got a kill?
                onKill(p);
            }
        }
    }

    public void onScoreChanged() {
        // Logger.log("Showing score of " + score);
        //UI.Notify(score + "");
        UI.ShowSubtitle(subtitleFunction(), INTERVAL*30);
    }

    public string subtitleDefault() {
        int civKills = killCount - policeKillCount;
        return "~r~" + civKills + "~b~  " + policeKillCount + "~g~  " + score;
    }

    private static PedHash[] COP_HASHES = new PedHash[] {
        PedHash.Cop01SFY, PedHash.Cop01SMY, PedHash.Hwaycop01SMY, PedHash.Snowcop01SMM,
        PedHash.Sheriff01SFY, PedHash.Sheriff01SMY,
        PedHash.Swat01SMY, PedHash.FibSec01, PedHash.FibSec01SMM
    };

    public void givePoints(int points) {
        score += points;
        Logger.log("Giving " + points + ", now have " + score);
        // Update the UI whenever your score changes
        onScoreChanged();
    }

    public void losePoints(int points) {
        if(score - points > 0) {
            score -= points;
        }
        else {
            score = 0;
        }
        Logger.log("Taking " + points + ", now have " + score);
        // Update the UI whenever your score changes
        onScoreChanged();
    }

    public int getScore() {
        return score;
    }

    public int getCivKills() {
        return killCount - policeKillCount;
    }

    // call when you kill p
    private void onKill(Ped p) {
        killCount++;
        killedPeds.Add(p);

        // update score
        givePoints(pedKillValueFunction(p));

        //UI.Notify("U mucked");
    }

    public int pedKillValueDefault(Ped p) {
        if(isCop(p)) {
            policeKillCount++;
            return 50;
        }
        else {
            if(isArmed(p)) {
                return 500;
            }
            else if(p.IsInVehicle()) {
                return 300;
            }
            else {
                return 100;
            }
        }
        //UI.Notify("GetKillValue Error on ped " + p.Model.ToString());
    }

    public static bool isCop(Ped p) {
        // check if it's a police officer you killed
        foreach (int copHash in COP_HASHES) {
            if (Function.Call<bool>(Hash.IS_PED_MODEL, p, copHash)) {
                // they r a cop
                return true;
            }
        }
        return false;
    }

    public void resetScore() {
        Logger.log("Reset score");
        UI.Notify("Score reset");
        score = 0;
        killCount = 0;
        policeKillCount = 0;
        onScoreChanged();
    }

    public void onPlayerDeath() {
        if(killCount != 0) {
            string notif = "You got " + killCount + " kills wow good job and " +
                    (killCount - policeKillCount) + " were civilians.\nFinal Score: " + score;

            string label = pedKillValueFunction.Method.Name.Substring("pedKillValue".Length);
            string newhighscore = "You set a new " + label + " highscore!";

            // assume the name of the pedKVF is like "pedKillValueKOTB" and extract the highscore "name" from it.
            string labelFirstUpper = label.ToLower().First().ToString().ToUpper() + label.ToLower().Substring(1);
            string notNewhighscore = labelFirstUpper + " High Score: " + defaultHighscore;

            int hs = defaultHighscore;
            if(!isInDefaultMode()) {
                altHighscores.TryGetValue(label, out hs);
            }
            if(score > hs) {
                hs = score;
                altHighscores.Remove(label);
                altHighscores.Add(label, hs);

                UI.Notify(notif + "\n" + newhighscore);

                string fn = getHighscoreFilename();
                // update high score
                File.WriteAllText(fn, hs + "");
                Logger.log("recording highscore " + hs + " into " + fn);
            }
            else {
                UI.Notify(notif + "\n" + notNewhighscore);
            }
        }

        // Reset score if using regular KC mode, else leave the other script to do it
        //if(!isInDefaultMode()) {
        resetScore();
        //}
    }

    private string getHighscoreFilename() {
        string fn = FILENAME;
        if (!isInDefaultMode()) {
            fn += pedKillValueFunction.Method.Name;
        }
        fn += FILE_EXT;
        return fn;
    }

    private bool isInDefaultMode() {
        return pedKillValueFunction == pedKillValueDefault;
    }

    public static bool isArmed(Ped p) {
        return p.Weapons.BestWeapon.Hash != WeaponHash.Unarmed;
    }
}
