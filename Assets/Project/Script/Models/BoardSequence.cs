using System.Collections.Generic;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Models
{
    public class BoardSequence
    {
        public List<MovedTileInfo> MovedTiles { get; set; }
        public List<AddedTileInfo> AddedTiles { get; set; }
        public List<Vector2Int> RemovedPositions { get; set; }
        public List<MatchGroup> MatchGroups { get; set; }
        public List<CreatedSpecialTileInfo> CreatedSpecialTiles { get; set; }
    }
}
