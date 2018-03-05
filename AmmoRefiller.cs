using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using System.Linq;
using System.Windows.Forms;

// Press B to refill ammo for current weapon
class AmmoRefiller : Script {

    private Ped player;

    public AmmoRefiller() {
        Tick += onTick;
        KeyUp += onKeyUp;

        player = Game.Player.Character;
        //UI.Notify("Init killCounter");
    }

    public void onTick(object sender, EventArgs e) {
   
    }

    private void onKeyUp(object sender, KeyEventArgs e) {
        if(e.KeyCode == Keys.B) {
            UI.Notify("Refilled ammo");
            Weapon w = player.Weapons.Current;
            w.Ammo = w.MaxAmmo;

            KillCounter.instance().resetScore();
        }

    }
}
