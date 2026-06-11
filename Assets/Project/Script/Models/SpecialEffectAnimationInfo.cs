using System.Collections.Generic;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Models {
    public class SpecialEffectAnimationInfo {
        public Vector2Int Origin { get; set; }
        public TileSpecialType SpecialType { get; set; }
        public List<Vector2Int> AffectedPositions { get; set; }
    }
}
