using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views
{
    public class TileSpotView : MonoBehaviour
    {
        public event Action<int, int> Clicked;

        [SerializeField] private Button _button;
        [SerializeField] private Color32[] _tileColors; 
        [SerializeField] private Image _tileImage;

        private int _x;
        private int _y;

        #region Unity
        private void Awake()
        {
            _button.onClick.AddListener(OnTileClick);
        }
        #endregion

        public Tween AnimatedSetTile(GameObject tile)
        {
            Transform tileTransform = tile.transform;
            Sequence sequence = DOTween.Sequence();
            sequence.AppendCallback(() => tileTransform.SetParent(transform, true));
            sequence.Append(tileTransform.DOMove(transform.position, 0.3f));

            return sequence;
        }

        public void SetPosition(int x, int y)
        {
            _x = x;
            _y = y;
        }

        public void SetTile(GameObject tile)
        {
            tile.transform.SetParent(transform, false);
            tile.transform.position = transform.position;
        }
        
        public void SetTileColor(int index) {
            _tileImage.color = _tileColors[index];
        }

        private void OnTileClick()
        {
            Clicked?.Invoke(_x, _y);
        }
    }
}
