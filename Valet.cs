using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.Math;

public class Valet : Script {

    private Ped valet;

    public Valet() {
        this.Tick += onTick;
        // DISABLE THIS SCRIPT
        //this.KeyUp += onKeyUp;
        this.KeyDown += onKeyDown;
    }

    private void onTick(object sender, EventArgs e) {
        //valet.Task.VehicleChase
    }

    private void onKeyDown(object sender, KeyEventArgs e) {
    }

    private void onKeyUp(object sender, KeyEventArgs e) {
         if (e.KeyCode == Keys.X) {
            //whistle for car :D

            if(Game.Player.Character.CurrentVehicle != null) {
                UI.Notify("You need to get out of your car first");
            }
            else {
                //TODO last vehicle persists thru death
                Vehicle prev = Function.Call<Vehicle>(Hash.GET_PLAYERS_LAST_VEHICLE);
                String name = prev.DisplayName;
                if (name.Equals("CARNOTFOUND")) {
                    UI.Notify("No previous vehicle");
                }
                else {
                    UI.Notify("Your " + name + " is being retrieved!");
                }

                Blip prev_blip = new Blip(prev.Handle);
                //UI.Notify("blip: " + prev_blip.Position.ToString());
                Vector3 vehicle_loc = prev.GetOffsetInWorldCoords(new Vector3(0, 0, 50));
                var modelnum = new Model(PedHash.Chimp);
                Ped valet = World.CreatePed(modelnum, vehicle_loc);
                Function.Call(Hash.SET_ENTITY_AS_NO_LONGER_NEEDED, valet);
                //Ped chauf = Function.Call<Ped>(Hash.CREATE_PED_INSIDE_VEHICLE, prev, -1, modelnum, -1, false, true);

                //UI.Notify(chauf.Gender.ToString());
                Function.Call(Hash.SET_PED_INTO_VEHICLE, valet, prev, -1);
                //crazy driver (these are probably overridden by DrivingStyle anyway)
                Function.Call(Hash.SET_PED_STEERS_AROUND_OBJECTS, valet, true);
                Function.Call(Hash.SET_PED_STEERS_AROUND_VEHICLES, valet, true);
                Function.Call(Hash.SET_PED_STEERS_AROUND_PEDS, valet, false);

                
                //chauf.GiveHelmet(false, HelmetType.RegularMotorcycleHelmet, 0);                      
                Vector3 pos = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, Game.Player.Character, true);
                UI.Notify("Your pos is " + pos);
                //valet.Task.DriveTo(prev, pos, 5, 50, (int)DrivingStyle.AvoidTrafficExtremely);
                valet.Task.VehicleChase(Game.Player.Character);
                valet.Task.LeaveVehicle();
                valet.Task.FleeFrom(Game.Player.Character);                
            }
       }
    }
}