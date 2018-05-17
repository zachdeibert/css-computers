using System;
using System.Collections.Generic;
using System.Linq;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class BoolArrayEqualityComparer : IEqualityComparer<bool[]> {
        public bool Equals(bool[] x, bool[] y) {
            return x.Length == y.Length && x.SequenceEqual(y);
        }

        public int GetHashCode(bool[] obj) {
            return obj.Select(o => o.GetHashCode()).Aggregate(17, (a, b) => a * 23 + b);
        }
    }
}
