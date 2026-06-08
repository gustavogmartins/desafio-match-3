using System;
using System.Collections.Generic;
using DG.Tweening;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.ScriptableObjects;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views
{
    public class BoardView : MonoBehaviour
    {
        public event Action<int, int> TileClicked;

        [SerializeField] private GridLayoutGroup _boardContainer;
        [SerializeField] private TilePrefabRepository _tilePrefabRepository;
        [SerializeField] private TileSpotView _tileSpotPrefab;

        private GameObject[][] _tiles;
        private TileSpotView[][] _tileSpots;
        private ObjectPool<GameObject>[] _tilePools;
        private readonly Dictionary<GameObject, int> _tilePoolIndexes = new();
        private Transform _tilePoolRoot;

        public void CreateBoard(List<List<Tile>> board)
        {
            _boardContainer.constraintCount = board[0].Count;
            InitializeTilePools(board.Count * board[0].Count);

            _tiles = new GameObject[board.Count][];
            _tileSpots = new TileSpotView[board.Count][];

            for (int y = 0; y < board.Count; y++)
            {
                _tiles[y] = new GameObject[board[0].Count];
                _tileSpots[y] = new TileSpotView[board[0].Count];

                for (int x = 0; x < board[0].Count; x++)
                {
                    TileSpotView tileSpot = Instantiate(_tileSpotPrefab);
                    tileSpot.transform.SetParent(_boardContainer.transform, false);
                    tileSpot.SetPosition(x, y);
                    tileSpot.Clicked += TileSpot_Clicked;

                    _tileSpots[y][x] = tileSpot;

                    int tileTypeIndex = board[y][x].Type;
                    if (tileTypeIndex > -1)
                    {
                        GameObject tile = GetTileFromPool(tileTypeIndex);
                        tileSpot.SetTile(tile);

                        _tiles[y][x] = tile;
                    }
                }
            }
        }

        public Tween CreateTile(List<AddedTileInfo> addedTiles)
        {
            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < addedTiles.Count; i++)
            {
                AddedTileInfo addedTileInfo = addedTiles[i];
                Vector2Int position = addedTileInfo.Position;

                TileSpotView tileSpot = _tileSpots[position.y][position.x];

                GameObject tile = GetTileFromPool(addedTileInfo.Type);
                tileSpot.SetTile(tile);

                _tiles[position.y][position.x] = tile;

                tile.transform.localScale = Vector2.zero;
                sequence.Join(tile.transform.DOScale(1.0f, 0.2f));
            }

            return sequence;
        }

        public Tween DestroyTiles(List<Vector2Int> matchedPosition)
        {
            for (int i = 0; i < matchedPosition.Count; i++)
            {
                Vector2Int position = matchedPosition[i];
                ReleaseTileToPool(_tiles[position.y][position.x]);
                _tiles[position.y][position.x] = null;
            }

            return DOVirtual.DelayedCall(0.2f, () => { });
        }

        public Tween MoveTiles(List<MovedTileInfo> movedTiles)
        {
            GameObject[][] tiles = new GameObject[_tiles.Length][];
            for (int y = 0; y < _tiles.Length; y++)
            {
                tiles[y] = new GameObject[_tiles[y].Length];
                for (int x = 0; x < _tiles[y].Length; x++)
                {
                    tiles[y][x] = _tiles[y][x];
                }
            }

            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < movedTiles.Count; i++)
            {
                MovedTileInfo movedTileInfo = movedTiles[i];

                Vector2Int from = movedTileInfo.From;
                Vector2Int to = movedTileInfo.To;

                sequence.Join(_tileSpots[to.y][to.x].AnimatedSetTile(_tiles[from.y][from.x]));

                tiles[to.y][to.x] = _tiles[from.y][from.x];
            }

            _tiles = tiles;

            return sequence;
        }

        public Tween SwapTiles(int fromX, int fromY, int toX, int toY)
        {
            Sequence sequence = DOTween.Sequence();
            sequence.Append(_tileSpots[fromY][fromX].AnimatedSetTile(_tiles[toY][toX]));
            sequence.Join(_tileSpots[toY][toX].AnimatedSetTile(_tiles[fromY][fromX]));

            (_tiles[toY][toX], _tiles[fromY][fromX]) = (_tiles[fromY][fromX], _tiles[toY][toX]);

            return sequence;
        }
        
        public Tween MarkSpecialTiles(List<CreatedSpecialTileInfo> createdSpecialTiles)
        {
            Sequence sequence = DOTween.Sequence();

            for (int i = 0; i < createdSpecialTiles.Count; i++)
            {
                CreatedSpecialTileInfo specialTile = createdSpecialTiles[i];
                Vector2Int position = specialTile.Position;

                GameObject tile = _tiles[position.y][position.x];

                int specialTilePrefabIndex = GetSpecialTilePrefabIndex(specialTile.SpecialType);
                if (tile == null || specialTilePrefabIndex < 0)
                {
                    continue;
                }

                ReleaseTileToPool(tile);

                TileSpotView tileSpot = _tileSpots[position.y][position.x];
                GameObject specialTileObject = GetTileFromPool(specialTilePrefabIndex);
                specialTileObject.name = $"Special_{specialTile.SpecialType}_{specialTileObject.name}";

                tileSpot.SetTile(specialTileObject);
                _tiles[position.y][position.x] = specialTileObject;
                
                specialTileObject.transform.localScale = Vector2.zero;
                sequence.Join(specialTileObject.transform.DOScale(1f, 0.15f));
            }

            return sequence;
        }

        private static int GetSpecialTilePrefabIndex(TileSpecialType specialType)
        {
            return specialType switch
            {
                TileSpecialType.ClearHorizontal => 4,
                TileSpecialType.ClearVertical => 5,
                TileSpecialType.ClearArea => 6,
                _ => -1
            };
        }

        private void InitializeTilePools(int boardTileCount) {
            if (_tilePools != null) {
                return;
            }

            EnsureTilePoolRoot();

            GameObject[] tilePrefabs = _tilePrefabRepository.TileTypePrefabList;
            int poolSize = Mathf.Max(1, boardTileCount);
            _tilePools = new ObjectPool<GameObject>[tilePrefabs.Length];

            for (int i = 0; i < tilePrefabs.Length; i++) {
                int prefabIndex = i;
                _tilePools[prefabIndex] = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledTile(prefabIndex),
                    actionOnGet: tile => OnGetPooledTile(tile, prefabIndex),
                    actionOnRelease: OnReleasePooledTile,
                    actionOnDestroy: OnDestroyPooledTile,
                    collectionCheck: true,
                    defaultCapacity: poolSize,
                    maxSize: poolSize);
            }
        }

        private void EnsureTilePoolRoot() {
            if (_tilePoolRoot != null) {
                return;
            }

            GameObject poolRoot = new("TilePoolRoot");
            Transform boardContainerTransform = _boardContainer.transform;
            Transform poolParent = boardContainerTransform.parent;
            poolRoot.transform.SetParent(poolParent, false);
            _tilePoolRoot = poolRoot.transform;
        }

        private GameObject GetTileFromPool(int prefabIndex) {
            if (prefabIndex < 0 || _tilePools == null || prefabIndex >= _tilePools.Length) {
                throw new ArgumentOutOfRangeException(nameof(prefabIndex), prefabIndex, "Invalid tile prefab index.");
            }

            return _tilePools[prefabIndex].Get();
        }

        private void ReleaseTileToPool(GameObject tile) {
            if (tile == null) {
                return;
            }

            if (!_tilePoolIndexes.TryGetValue(tile, out int prefabIndex)) {
                Debug.LogWarning($"Tile '{tile.name}' was not created by the BoardView pool.");
                return;
            }

            _tilePools[prefabIndex].Release(tile);
        }

        private GameObject CreatePooledTile(int prefabIndex) {
            GameObject prefab = _tilePrefabRepository.TileTypePrefabList[prefabIndex];
            GameObject tile = Instantiate(prefab, _tilePoolRoot, false);
            tile.name = prefab.name;
            tile.SetActive(false);
            _tilePoolIndexes[tile] = prefabIndex;

            return tile;
        }

        private void OnGetPooledTile(GameObject tile, int prefabIndex) {
            GameObject prefab = _tilePrefabRepository.TileTypePrefabList[prefabIndex];
            tile.name = prefab.name;

            Transform tileTransform = tile.transform;
            tileTransform.DOKill();
            tileTransform.localScale = Vector3.one;
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localPosition = Vector3.zero;
            tile.SetActive(true);
        }

        private void OnReleasePooledTile(GameObject tile) {
            Transform tileTransform = tile.transform;
            tileTransform.DOKill();
            tileTransform.SetParent(_tilePoolRoot, false);
            tileTransform.localScale = Vector3.one;
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localPosition = Vector3.zero;
            tile.SetActive(false);
        }

        private void OnDestroyPooledTile(GameObject tile) {
            _tilePoolIndexes.Remove(tile);
            Destroy(tile);
        }

        #region Events
        private void TileSpot_Clicked(int x, int y)
        {
            TileClicked(x, y);
        }
        #endregion
    }
}
