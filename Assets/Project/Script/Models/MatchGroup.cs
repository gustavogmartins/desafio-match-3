using System.Collections.Generic;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Models {
    public class MatchGroup {
        public int Type { get; set; }
        public MatchDirection Direction { get; set; }
        public List<Vector2Int> Positions { get; set; }
        public int Size => Positions?.Count ?? 0;
        public bool IsSpecial => Size > 3;
    }
}