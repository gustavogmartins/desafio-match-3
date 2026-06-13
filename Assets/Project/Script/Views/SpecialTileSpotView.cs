using UnityEngine;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views {
    public class SpecialTileSpotView : MonoBehaviour {
        [SerializeField] RectTransform _specialTileSpot;

        public void SetSpecialTile(GridLayoutGroup grid) {
            _specialTileSpot.sizeDelta = grid.cellSize;
        }
    }
}