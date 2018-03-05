using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using System.Linq;
using System.Windows.Forms;
using GTA.Math;
using System.Drawing;

class VehicleSpawner : Script {

    private Ped player;

    public VehicleSpawner() {
        Tick += onTick;
        KeyUp += onKeyUp;

        player = Game.Player.Character;
        //UI.Notify("Init killCounter");
    }

    public void onTick(object sender, EventArgs e) {
   
    }

    private void onKeyUp(object sender, KeyEventArgs e) {
        if (e.KeyCode == Keys.End) {
            spawnThrone();
        }
        else if(e.KeyCode == Keys.Pause) {
            Vehicle copter = World.CreateVehicle(VehicleHash.Buzzard, posInFrontOfPlayer(10));
            doVehicleStuff(copter);
        }
        else if (e.KeyCode == Keys.PageUp) {
            Vehicle plane = World.CreateVehicle(VehicleHash.Besra, posInFrontOfPlayer(10));
            doVehicleStuff(plane);
        }
        else if (e.KeyCode == Keys.PageDown) {
            spawnHummer();
        }
    }

    // add vehicle mods to motorcycle, probably need to change this for cars
    private void doVehicleMods(Vehicle v, string lp) {
        v.DirtLevel = 0f;
        v.NumberPlate = lp;

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

        doVehicleStuff(v);
    }

    // do the things you always do after placing a new vehicle
    private void doVehicleStuff(Vehicle v) {
        v.CanTiresBurst = false;
        v.CanWheelsBreak = false;
        v.EngineCanDegrade = false;
        //v.EnginePowerMultiplier = 1.5f;

        v.PlaceOnGround();
        v.MarkAsNoLongerNeeded();
    }

    private Vector3 posInFrontOfPlayer(float distance) {
        return player.Position + player.ForwardVector * distance;
    }

    private void spawnHummer() {
        Vehicle v = World.CreateVehicle(VehicleHash.Patriot, posInFrontOfPlayer(5));
        doVehicleMods(v, "WATCHOUT");
    }

    private void spawnThrone() {
        Vehicle v = World.CreateVehicle(VehicleHash.Bati, posInFrontOfPlayer(5));
        doVehicleMods(v, "THE KING");
        v.CustomPrimaryColor = Color.FromArgb(0, 51, 204);
        v.CustomSecondaryColor = Color.Gold;
    }
}
