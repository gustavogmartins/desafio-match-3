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
        [SerializeField] private Image _selectionHighlight;
        private Tween _selectionTween;
        
        private int _x;
        private int _y;

        #region Unity
        private void Awake()
        {
            _button.onClick.AddListener(OnTileClick);
            SetSelected(false);
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

        public void SetSelected(bool selected) {
            if (_selectionHighlight == null) return;

            _selectionTween?.Kill();
            _selectionHighlight.transform.DOKill();
            _selectionHighlight.DOKill();
            if (!selected) {
                Color hiddenColor = _selectionHighlight.color;
                hiddenColor.a = 0f;
                _selectionHighlight.color = hiddenColor;
                _selectionHighlight.transform.localScale = Vector3.one;
                _selectionHighlight.gameObject.SetActive(false);
                return;
            }

            _selectionHighlight.gameObject.SetActive(true);
            _selectionHighlight.transform.localScale = Vector3.one;

            Color visibleColor = _selectionHighlight.color;
            visibleColor.a = 0.55f;
            _selectionHighlight.color = visibleColor;

            _selectionTween = DOTween.Sequence()
                .Join(_selectionHighlight.transform.DOScale(1.08f, 0.35f))
                .Join(_selectionHighlight.DOFade(0.85f, 0.35f))
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
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
