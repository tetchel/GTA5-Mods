using System;
using System.Windows.Forms;
using System.IO;

using GTA;
using GTA.Native;

public class WeaponRemover : Script {
    private const int NUMBER_OF_WEAPONS = 256;              //should be enough

    WeaponHash[] _weapons_to_drop = new WeaponHash[NUMBER_OF_WEAPONS];       
    WeaponHash[] _weapons_to_hide = new WeaponHash[NUMBER_OF_WEAPONS];
    //mods on hidden weapons
    WeaponComponent[][] _components = new WeaponComponent[NUMBER_OF_WEAPONS][];
    int[] _hidden_weapons_ammo = new int[NUMBER_OF_WEAPONS];
    //user settings
    Keys _drop_key;       
    bool _drop_only_on_press = false;

    //is currently in car and weapons are being hidden
    bool _is_missing_car_weapons = false;

    const string FILEPATH = "scripts/unwanted_weapons.txt";

    public WeaponRemover() {
        Interval = 500;    //ms
        Tick += onTick;
        KeyUp += onKeyUp;

        string[] contents = null;
        try {
            contents = File.ReadAllLines(FILEPATH);
        }
        catch(Exception e) {
            UI.Notify("Couldn't open ~n~" + FILEPATH + "~n~" + e.Message + "~n~Fix the issue and press INSERT to reload the script.", true);
            //kill the script until reload
            Tick -= onTick;
            KeyUp -= onKeyUp;
        }
        //first parse user options
        string drop_only_on_press = contents[0].Trim().ToLower().Substring("droponlyonpress=".Length);
        if (drop_only_on_press.Equals("true", StringComparison.InvariantCultureIgnoreCase)) {
            //UI.Notify("DropOnlyOnPress is TRUE!");
            _drop_only_on_press = true;
        }
        else if(drop_only_on_press.Equals("false", StringComparison.InvariantCultureIgnoreCase)) {
            //UI.Notify("DropOnlyOnPress is FALSE!");
            _drop_only_on_press = false;
        }
        else {
            UI.Notify("DropOnlyOnPress must be set to \"true\" or \"false\", instead is set to:~n~" + drop_only_on_press, true);
            UI.Notify("Please check that the correct format is followed for the input file");
        }

        if(_drop_only_on_press) {
            string keycode_str = contents[1].Trim().Substring("DropKey=".Length);

            if (Enum.TryParse(keycode_str, true, out _drop_key)) {
                UI.Notify("Drop weapon key set to " + keycode_str);
            }
            else {
                UI.Notify("Drop weapon key could not be set, invalid key:~n~" + keycode_str, true);
                UI.Notify("Please check that the correct format is followed for the input file", true);
            }
        }

        //whether we are processing drop-on-foot or hide-in-car weapons (the former is done first)
        bool in_car = false;
        //iterators for weapons_to_hide and weapons_to_drop
        int i = 0, j = 0;
        //ignore first three lines
        for(int k = 3; k < contents.Length; k++) {
            string s = contents[k].Trim();
            if (s.Equals(""))
                continue;
            if(s.Equals("~Weapons to hide when in car:", StringComparison.InvariantCultureIgnoreCase)) {
                in_car = true;
                continue;
            }
            s = s.ToUpper();
            WeaponHash w;
            if (Enum.TryParse(s, true, out w)) {
                if (in_car) {
                    _weapons_to_hide[i++] = w;
                    UI.Notify("Gonna hide a " + w.ToString());
                }
                else
                    _weapons_to_drop[j++] = w;
            }
            else {
                UI.Notify("Invalid Weapon: line #" + (k + 1) + " in unwanted_weapons.txt: ~n~" + s, true);
            }
        }
    }

    private void onTick(object sender, EventArgs e) {
        if (!_is_missing_car_weapons && Game.Player.Character.IsInVehicle()) {
            _is_missing_car_weapons = true;
            for(int i = 0; i < _weapons_to_hide.Length; i++) {
                WeaponHash w = _weapons_to_hide[i];
                if (w != 0 && Game.Player.Character.Weapons.HasWeapon(w)) {
                    WeaponComponent[] current_components = Weapon.GetComponentsFromHash(w);
                    _components[i] = new WeaponComponent[current_components.Length];

                    //go through all possible components and add to _components if player has that mod
                    for (int j = 0; j < current_components.Length; j++) {
                        //UI.Notify("Component: " + _components[i][j].ToString());
                        bool result = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, Game.Player.Character, (int)w, (int)current_components[j]);
                        if (result) {
                            _components[i][j] = current_components[j];
                        }
                    }

                    _hidden_weapons_ammo[i] = Function.Call<int>(Hash.GET_AMMO_IN_PED_WEAPON, Game.Player.Character, (int)w);
                    //UI.Notify("Removed a " + w.ToString() + " with " + _hidden_weapons_ammo[i] + " rounds");
                    Game.Player.Character.Weapons.Remove(w);
                }
            }
        }
        //restore hidden weapons after they leave car
        else if (_is_missing_car_weapons) {
            _is_missing_car_weapons = false;
            
            for (int i = 0; i < _weapons_to_hide.Length; i++) {
                WeaponHash w = _weapons_to_hide[i];
                if (w == 0)
                    continue;

                //UI.Notify("Returning a " + w);
                Game.Player.Character.Weapons.Give(w, 0, false, false);
                //restore weapon mods for this weapon
                foreach (WeaponComponent wc in _components[i]) {
                    if (wc == 0)
                        continue;
                    Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, Game.Player.Character, (int)w, (int)wc);
                }
            }
        }
        //drop constantly - doesn't work on combat pistol ?!
        if(!_drop_only_on_press) {
            foreach (WeaponHash w in _weapons_to_drop) {
                if (w != 0 && Game.Player.Character.Weapons.HasWeapon(w)) {
                    Game.Player.Character.Weapons.Remove(w);
                    //UI.Notify("Dropped " + w.ToString());
                }
            }
        }
    }

    private void onKeyUp(object sender, KeyEventArgs e) {
        //player pressed drop key
        if(e.KeyCode == _drop_key) {
            int count = 0;
            foreach (WeaponHash w in _weapons_to_drop) {
                if (w != 0 && Game.Player.Character.Weapons.HasWeapon(w)) {
                    count++;
                    Game.Player.Character.Weapons.Remove(w);
                    UI.Notify("Dropped your " + w.ToString());
                }
            }
            if(count == 0) {
                UI.Notify("Dropped no weapons.");
            }
        }
    }
}
