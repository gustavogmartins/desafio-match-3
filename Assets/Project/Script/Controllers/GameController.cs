using System;
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
        }
        #endregion

        private void AnimateBoard(List<BoardSequence> boardSequences, Action onComplete)
        {
            if (boardSequences.Count == 0)
            {
                onComplete();
                return;
            }

            AnimateBoard(boardSequences, 0, onComplete);
        }

        private void AnimateBoard(List<BoardSequence> boardSequences, int index, Action onComplete)
        {
            BoardSequence boardSequence = boardSequences[index];

            Sequence sequence = DOTween.Sequence();
            sequence.Append(_boardView.DestroyTiles(boardSequence.RemovedPositions));
            sequence.Append(_boardView.MarkSpecialTiles(boardSequence.CreatedSpecialTiles));
            sequence.Append(_boardView.MoveTiles(boardSequence.MovedTiles));
            sequence.Append(_boardView.CreateTile(boardSequence.AddedTiles));

            index += 1;
            if (index < boardSequences.Count)
            {
                sequence.onComplete += () => AnimateBoard(boardSequences, index, onComplete);
            }
            else
            {
                sequence.onComplete += () => onComplete();
            }
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
                    _selectedX = -1;
                    _selectedY = -1;
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
                        _selectedX = -1;
                        _selectedY = -1;
                    };
                }
            }
            else
            {
                _selectedX = x;
                _selectedY = y;
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
            _selectedX = -1;
            _selectedY = -1;
            _lastClickedX = -1;
            _lastClickedY = -1;
            _lastClickTime = -1f;

            List<BoardSequence> boardSequences = _gameService.ActivateSpecialTile(x, y);
            AnimateBoard(boardSequences, () => _isAnimating = false);
        }

        private void TrackLastClick(int x, int y)
        {
            _lastClickedX = x;
            _lastClickedY = y;
            _lastClickTime = Time.unscaledTime;
        }
    }
}
