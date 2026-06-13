using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Gazeus.DesafioMatch3.Core;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.Views;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Controllers
{
    public class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private int _boardHeight = 10;
        [SerializeField] private int _boardWidth = 10;
        [SerializeField] private float _doubleClickInterval = 0.3f;
        [SerializeField] private SceneFaderController _sceneFaderController;
        private GameService _gameService;
        private bool _isAnimating;
        private int _selectedX = -1;
        private int _selectedY = -1;
        private int _lastClickedX = -1;
        private int _lastClickedY = -1;
        private float _lastClickTime = -1f;

        #region Unity
        private void Awake()
        {
            _gameService = new GameService();
            _boardView.TileClicked += OnTileClick;
        }

        private void OnDestroy()
        {
            _boardView.TileClicked -= OnTileClick;
        }

        private void Start()
        {
            List<List<Tile>> board = _gameService.StartGame(_boardWidth, _boardHeight);
            _boardView.CreateBoard(board);
            _sceneFaderController.StartFadeIn();
        }
        #endregion

        private void AnimateBoard(List<BoardSequence> boardSequences, Action onComplete)
        {
            StartCoroutine(AnimateBoardRoutine(boardSequences, onComplete));
        }
        
        private IEnumerator AnimateBoardRoutine(List<BoardSequence> boardSequences, Action onComplete)
        {
            for (int i = 0; i < boardSequences.Count; i++)
            {
                BoardSequence boardSequence = boardSequences[i];

                yield return _boardView.PlaySpecialEffects(boardSequence.SpecialEffects).WaitForCompletion();
                yield return _boardView.DestroyTiles(boardSequence.RemovedPositions).WaitForCompletion();
                yield return _boardView.MarkSpecialTiles(boardSequence.CreatedSpecialTiles).WaitForCompletion();
                yield return _boardView.MoveTiles(boardSequence.MovedTiles).WaitForCompletion();
                yield return _boardView.CreateTile(boardSequence.AddedTiles).WaitForCompletion();
            }

            onComplete?.Invoke();
        }

        public bool TryCreateSpecialTileAtSelection(TileSpecialType specialType)
        {
            if (_isAnimating || _selectedX < 0 || _selectedY < 0)
            {
                return false;
            }

            if (!_gameService.TryCreateSpecialTile(
                    _selectedX,
                    _selectedY,
                    specialType,
                    out CreatedSpecialTileInfo createdSpecialTile))
            {
                return false;
            }

            _isAnimating = true;
            ClearSelection();
            ResetLastClick();

            Tween markSpecialTileTween = _boardView.MarkSpecialTiles(new List<CreatedSpecialTileInfo>
            {
                createdSpecialTile
            });
            markSpecialTileTween.onComplete += () => _isAnimating = false;

            return true;
        }

        private void OnTileClick(int x, int y)
        {
            if (_isAnimating) return;
            if (IsSpecialTileDoubleClick(x, y))
            {
                PlaySpecialTileActivation(x, y);
                return;
            }

            TrackLastClick(x, y);

            if (_selectedX > -1 && _selectedY > -1)
            {
                
                if (_selectedX == x && _selectedY == y)  {
                    ClearSelection();
                    return;
                }
                
                if (Mathf.Abs(_selectedX - x) + Mathf.Abs(_selectedY - y) != 1)
                {
                    _selectedX = -1;
                    _selectedY = -1;
                }
                else
                {
                    _isAnimating = true;
                    int fromX = _selectedX;
                    int fromY = _selectedY;
                    int toX = x;
                    int toY = y;
                    _boardView.SwapTiles(fromX, fromY, toX, toY).onComplete += () =>
                    {
                        bool isValid = _gameService.IsValidMovement(fromX, fromY, toX, toY);
                        if (isValid)
                        {
                            List<BoardSequence> swapResult = _gameService.SwapTile(fromX, fromY, toX, toY);
                            AnimateBoard(swapResult, () => _isAnimating = false);
                        }
                        else
                        {
                            _boardView.SwapTiles(toX, toY, fromX, fromY).onComplete += () => _isAnimating = false;
                        }
                        ClearSelection();
                    };
                }
            }
            else
            {
                _selectedX = x;
                _selectedY = y;
                _boardView.SelectTile(x, y);
            }
        }

        private bool IsSpecialTileDoubleClick(int x, int y)
        {
            return _gameService.IsSpecialTile(x, y) &&
                   _lastClickedX == x &&
                   _lastClickedY == y &&
                   Time.unscaledTime - _lastClickTime <= _doubleClickInterval;
        }

        private void PlaySpecialTileActivation(int x, int y)
        {
            _isAnimating = true;
            ClearSelection();
            ResetLastClick();

            List<BoardSequence> boardSequences = _gameService.ActivateSpecialTile(x, y);
            AnimateBoard(boardSequences, () => _isAnimating = false);
        }

        private void TrackLastClick(int x, int y)
        {
            _lastClickedX = x;
            _lastClickedY = y;
            _lastClickTime = Time.unscaledTime;
        }

        private void ClearSelection()
        {
            _selectedX = -1;
            _selectedY = -1;
            _boardView.ClearSelectedTile();
        }

        private void ResetLastClick()
        {
            _lastClickedX = -1;
            _lastClickedY = -1;
            _lastClickTime = -1f;
        }
    }
}
