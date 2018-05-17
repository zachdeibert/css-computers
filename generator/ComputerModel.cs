using System;
using System.Collections.Generic;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class ComputerModel {
        public string Name;
        public List<Pin> Pins = new List<Pin>();
        public List<TruthTable> TruthTables = new List<TruthTable>();
    }
}
