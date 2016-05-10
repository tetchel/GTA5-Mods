using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;

namespace KingOfTheBikes {
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
