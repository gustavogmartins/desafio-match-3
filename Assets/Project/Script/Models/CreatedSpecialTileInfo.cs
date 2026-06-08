using UnityEngine;

namespace Gazeus.DesafioMatch3.Models {
    public struct CreatedSpecialTileInfo {
        public Vector2Int Position { get; set; }
        public int Type { get; set; }
        public TileSpecialType SpecialType { get; set; }
    }
}