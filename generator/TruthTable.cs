using System;
using System.Collections.Generic;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class TruthTable {
        public List<Pin> Inputs;
        public Pin Output;
        public Dictionary<bool[], bool> Table = new Dictionary<bool[], bool>(new BoolArrayEqualityComparer());
    }
}
