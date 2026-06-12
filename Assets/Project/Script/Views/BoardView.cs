using System;
using System.Collections.Generic;
using DG.Tweening;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.ScriptableObjects;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views {
    public class BoardView : MonoBehaviour {
        private const int DestroyTileVfxIndex = 0;
        private const int SpecialTileAppearVfxIndex = 1;

        public event Action<int, int> TileClicked;
        [SerializeField] private RectTransform _boardContainerRect;
        [SerializeField] private GridLayoutGroup _boardContainer;
        [SerializeField] private TilePrefabRepository _tilePrefabRepository;
        [SerializeField] private VfxPrefabRepository _vfxPrefabRepository;
        [SerializeField] private TileSpotView _tileSpotPrefab;
        [SerializeField] private SpecialTileSpotView _specialTileSpot;
        [SerializeField] private float _dissolveDuration = 0.5f;

        private GameObject[][] _tiles;
        private TileSpotView[][] _tileSpots;
        private ObjectPool<GameObject>[] _tilePools;
        private ObjectPool<GameObject>[] _vfxPools;
        private readonly Dictionary<GameObject, int> _tilePoolIndexes = new();
        private readonly Dictionary<GameObject, int> _vfxPoolIndexes = new();
        private readonly Vector3[] _rectWorldCorners = new Vector3[4];
        private Transform _tilePoolRoot;
        private Transform _vfxPoolRoot;

        public void CreateBoard(List<List<Tile>> board) {
            _boardContainer.constraintCount = board[0].Count;
            UpdateCellSize();

            InitializeTilePools(board.Count * board[0].Count);
            InitializeVfxPools(board.Count * board[0].Count);

            _tiles = new GameObject[board.Count][];
            _tileSpots = new TileSpotView[board.Count][];
            for (int y = 0; y < board.Count; y++) {
                _tiles[y] = new GameObject[board[0].Count];
                _tileSpots[y] = new TileSpotView[board[0].Count];
                for (int x = 0; x < board[0].Count; x++) {
                    TileSpotView tileSpot = Instantiate(_tileSpotPrefab, _boardContainer.transform, false);
                    tileSpot.SetPosition(x, y);
                    tileSpot.Clicked += TileSpot_Clicked;

                    _tileSpots[y][x] = tileSpot;
                    int tileTypeIndex = board[y][x].Type;

                    if (tileTypeIndex > -1) {
                        GameObject tile = GetTileFromPool(tileTypeIndex);
                        tileSpot.SetTile(tile);
                        int tileColorIndex = (x + y) % 2 == 0 ? 0 : 1;
                        tileSpot.SetTileColor(tileColorIndex);
                        _tiles[y][x] = tile;
                    }
                }
            }
        }

        private void UpdateCellSize() {
            float containerWidth = _boardContainerRect.rect.width;
            float containerHeight = _boardContainerRect.rect.height;

            // Calculate available cell size space discounting the GridLayout padding
            float totalSpacingX = _boardContainer.spacing.x * (_boardContainer.constraintCount - 1);
            float totalSpacingY = _boardContainer.spacing.y * (_boardContainer.constraintCount - 1);

            float availableWidth = containerWidth - _boardContainer.padding.left - _boardContainer.padding.right -
                                   totalSpacingX;
            float availableHeight = containerHeight - _boardContainer.padding.top - _boardContainer.padding.bottom -
                                    totalSpacingY;

            // Calculate the ideal sell size considering square cells
            float cellSizeByWidth = availableWidth / _boardContainer.constraintCount;
            float cellSizeByHeight = availableHeight / _boardContainer.constraintCount;

            float finalCellSize = Mathf.Min(cellSizeByWidth, cellSizeByHeight);

            _boardContainer.cellSize = new Vector2(finalCellSize, finalCellSize);
            _specialTileSpot.SetSpecialTile(_boardContainer);
        }

        public Tween CreateTile(List<AddedTileInfo> addedTiles) {
            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < addedTiles.Count; i++) {
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

        public Tween DestroyTiles(List<Vector2Int> matchedPosition) {
            Sequence sequence = DOTween.Sequence();
            bool hasDissolveTween = false;

            for (int i = 0; i < matchedPosition.Count; i++) {
                Vector2Int position = matchedPosition[i];
                GameObject tile = _tiles[position.y][position.x];

                if (tile != null) {
                    Vector3 vfxPosition = GetTransformWorldCenter(tile.transform);
                    TileDissolveView dissolveView = EnsureTileDissolveView(tile);
                    Tween dissolveTween = dissolveView.PlayDissolve(_dissolveDuration);

                    if (dissolveTween != null) {
                        hasDissolveTween = true;
                        sequence.Join(dissolveTween.OnComplete(() => {
                            PlayVfx(DestroyTileVfxIndex, vfxPosition);
                            ReleaseTileToPool(tile);
                        }));
                    } else {
                        PlayVfx(DestroyTileVfxIndex, vfxPosition);
                        ReleaseTileToPool(tile);
                    }
                }

                _tiles[position.y][position.x] = null;
            }

            if (!hasDissolveTween) {
                sequence.AppendInterval(_dissolveDuration);
            }

            return sequence;
        }

        public Tween MoveTiles(List<MovedTileInfo> movedTiles) {
            GameObject[][] tiles = new GameObject[_tiles.Length][];
            for (int y = 0; y < _tiles.Length; y++) {
                tiles[y] = new GameObject[_tiles[y].Length];
                for (int x = 0; x < _tiles[y].Length; x++) {
                    tiles[y][x] = _tiles[y][x];
                }
            }

            HashSet<Vector2Int> destinationPositions = new();
            for (int i = 0; i < movedTiles.Count; i++) {
                destinationPositions.Add(movedTiles[i].To);
            }

            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < movedTiles.Count; i++) {
                MovedTileInfo movedTileInfo = movedTiles[i];

                Vector2Int from = movedTileInfo.From;
                Vector2Int to = movedTileInfo.To;
                GameObject movedTile = _tiles[from.y][from.x];

                sequence.Join(_tileSpots[to.y][to.x].AnimatedSetTile(movedTile));

                tiles[to.y][to.x] = movedTile;
            }

            for (int i = 0; i < movedTiles.Count; i++) {
                Vector2Int from = movedTiles[i].From;
                if (!destinationPositions.Contains(from)) {
                    tiles[from.y][from.x] = null;
                }
            }

            _tiles = tiles;

            return sequence;
        }

        public Tween SwapTiles(int fromX, int fromY, int toX, int toY) {
            Sequence sequence = DOTween.Sequence();
            sequence.Append(_tileSpots[fromY][fromX].AnimatedSetTile(_tiles[toY][toX]));
            sequence.Join(_tileSpots[toY][toX].AnimatedSetTile(_tiles[fromY][fromX]));

            (_tiles[toY][toX], _tiles[fromY][fromX]) = (_tiles[fromY][fromX], _tiles[toY][toX]);

            return sequence;
        }

        public Tween MarkSpecialTiles(List<CreatedSpecialTileInfo> createdSpecialTiles) {
            Sequence sequence = DOTween.Sequence();

            for (int i = 0; i < createdSpecialTiles.Count; i++) {
                CreatedSpecialTileInfo specialTile = createdSpecialTiles[i];
                Vector2Int position = specialTile.Position;

                GameObject tile = _tiles[position.y][position.x];

                int specialTilePrefabIndex = GetSpecialTilePrefabIndex(specialTile.SpecialType);
                if (tile == null || specialTilePrefabIndex < 0) {
                    continue;
                }

                ReleaseTileToPool(tile);

                TileSpotView tileSpot = _tileSpots[position.y][position.x];
                GameObject specialTileObject = GetTileFromPool(specialTilePrefabIndex);
                specialTileObject.name = $"Special_{specialTile.SpecialType}_{specialTileObject.name}";

                tileSpot.SetTile(specialTileObject);
                _tiles[position.y][position.x] = specialTileObject;

                specialTileObject.transform.localScale = Vector3.zero;
                specialTileObject.transform.localRotation = GetSpecialTileRotation(specialTile.SpecialType);

                Vector3 vfxPosition = GetTransformWorldCenter(specialTileObject.transform);
                PlayVfx(SpecialTileAppearVfxIndex, vfxPosition);
                sequence.Append(CreateSpecialTileAnimation(specialTileObject, tileSpot, specialTile.SpecialType));
            }

            return sequence;
        }

        private Tween CreateSpecialTileAnimation(GameObject specialTileObject, TileSpotView tileSpot,
            TileSpecialType specialType) {
            Transform specialTileTransform = specialTileObject.transform;
            specialTileTransform.SetParent(_specialTileSpot.transform, true);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(
                specialTileTransform
                    .DOScale(2.35f, 0.18f)
                    .SetEase(Ease.OutBack)
            );

            sequence.Append(
                specialTileTransform
                    .DOScale(1f, 0.08f)
                    .SetEase(Ease.InOutSine)
            );

            sequence.Join(
                specialTileTransform
                    .DOPunchRotation(new Vector3(0f, 0f, 24f), 0.25f, 8, 0.5f)
            );

            sequence.AppendCallback(() => RestoreSpecialTileToSpot(
                specialTileObject,
                tileSpot,
                specialType));

            return sequence;
        }

        private static void RestoreSpecialTileToSpot(GameObject specialTileObject, TileSpotView tileSpot,
            TileSpecialType specialType) {
            Transform specialTileTransform = specialTileObject.transform;
            tileSpot.SetTile(specialTileObject);
            specialTileTransform.localScale = Vector3.one;
            specialTileTransform.localRotation = GetSpecialTileRotation(specialType);
        }

        private static Quaternion GetSpecialTileRotation(TileSpecialType specialType) {
            return specialType == TileSpecialType.ClearHorizontal
                ? Quaternion.Euler(0f, 0f, -90f)
                : Quaternion.identity;
        }

        private static int GetSpecialTilePrefabIndex(TileSpecialType specialType) {
            return specialType switch {
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

        private void InitializeVfxPools(int boardTileCount) {
            if (_vfxPools != null || _vfxPrefabRepository == null) {
                return;
            }

            GameObject[] vfxPrefabs = _vfxPrefabRepository.VfxPrefabList;
            if (vfxPrefabs == null || vfxPrefabs.Length == 0) {
                return;
            }

            EnsureVfxPoolRoot();

            int poolSize = Mathf.Max(1, boardTileCount);
            _vfxPools = new ObjectPool<GameObject>[vfxPrefabs.Length];

            for (int i = 0; i < vfxPrefabs.Length; i++) {
                int prefabIndex = i;
                if (vfxPrefabs[prefabIndex] == null) {
                    continue;
                }

                _vfxPools[prefabIndex] = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledVfx(prefabIndex),
                    actionOnGet: vfx => OnGetPooledVfx(vfx, prefabIndex),
                    actionOnRelease: OnReleasePooledVfx,
                    actionOnDestroy: OnDestroyPooledVfx,
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

        private void EnsureVfxPoolRoot() {
            if (_vfxPoolRoot != null) {
                return;
            }

            GameObject poolRoot = new("VFXPoolRoot");
            _vfxPoolRoot = poolRoot.transform;
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

        private void PlayVfx(int prefabIndex, Vector3 worldPosition) {
            if (_vfxPools == null || prefabIndex < 0 || prefabIndex >= _vfxPools.Length) {
                return;
            }

            ObjectPool<GameObject> vfxPool = _vfxPools[prefabIndex];
            if (vfxPool == null) {
                return;
            }

            GameObject vfx = vfxPool.Get();
            PooledParticleVfx pooledParticleVfx = vfx.GetComponent<PooledParticleVfx>();
            if (pooledParticleVfx == null) {
                ReleaseVfxToPool(vfx);
                return;
            }

            pooledParticleVfx.PlayAt(worldPosition);
        }

        private void ReleaseVfxToPool(GameObject vfx) {
            if (vfx == null) {
                return;
            }

            if (!_vfxPoolIndexes.TryGetValue(vfx, out int prefabIndex)) {
                Debug.LogWarning($"VFX '{vfx.name}' was not created by the BoardView pool.");
                return;
            }

            if (_vfxPools == null || prefabIndex < 0 || prefabIndex >= _vfxPools.Length ||
                _vfxPools[prefabIndex] == null) {
                return;
            }

            _vfxPools[prefabIndex].Release(vfx);
        }

        private GameObject CreatePooledTile(int prefabIndex) {
            GameObject prefab = _tilePrefabRepository.TileTypePrefabList[prefabIndex];
            GameObject tile = Instantiate(prefab, _tilePoolRoot, false);
            tile.name = prefab.name;
            EnsureTileDissolveView(tile).ResetDissolve();
            tile.SetActive(false);
            _tilePoolIndexes[tile] = prefabIndex;

            return tile;
        }

        private GameObject CreatePooledVfx(int prefabIndex) {
            GameObject prefab = _vfxPrefabRepository.VfxPrefabList[prefabIndex];
            GameObject vfx = Instantiate(prefab, _vfxPoolRoot, false);
            vfx.name = prefab.name;

            PooledParticleVfx pooledParticleVfx = vfx.GetComponent<PooledParticleVfx>();
            if (pooledParticleVfx == null) {
                pooledParticleVfx = vfx.AddComponent<PooledParticleVfx>();
            }

            pooledParticleVfx.Initialize(ReleaseVfxToPool);
            vfx.SetActive(false);
            _vfxPoolIndexes[vfx] = prefabIndex;

            return vfx;
        }

        private void OnGetPooledTile(GameObject tile, int prefabIndex) {
            GameObject prefab = _tilePrefabRepository.TileTypePrefabList[prefabIndex];
            tile.name = prefab.name;

            Transform tileTransform = tile.transform;
            tileTransform.DOKill();
            tileTransform.localScale = Vector3.one;
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localPosition = Vector3.zero;
            EnsureTileDissolveView(tile).ResetDissolve();
            tile.SetActive(true);
        }

        private void OnGetPooledVfx(GameObject vfx, int prefabIndex) {
            GameObject prefab = _vfxPrefabRepository.VfxPrefabList[prefabIndex];
            vfx.name = prefab.name;

            Transform vfxTransform = vfx.transform;
            vfxTransform.SetParent(_vfxPoolRoot, false);
            vfxTransform.localScale = Vector3.one;
            vfxTransform.localRotation = Quaternion.identity;
            vfxTransform.localPosition = Vector3.zero;
        }

        private void OnReleasePooledTile(GameObject tile) {
            Transform tileTransform = tile.transform;
            tileTransform.DOKill();
            tileTransform.SetParent(_tilePoolRoot, false);
            tileTransform.localScale = Vector3.one;
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localPosition = Vector3.zero;
            EnsureTileDissolveView(tile).ResetDissolve();
            tile.SetActive(false);
        }

        private void OnReleasePooledVfx(GameObject vfx) {
            PooledParticleVfx pooledParticleVfx = vfx.GetComponent<PooledParticleVfx>();
            if (pooledParticleVfx != null) {
                pooledParticleVfx.StopAndClear();
            }

            Transform vfxTransform = vfx.transform;
            vfxTransform.SetParent(_vfxPoolRoot, false);
            vfxTransform.localScale = Vector3.one;
            vfxTransform.localRotation = Quaternion.identity;
            vfxTransform.localPosition = Vector3.zero;
            vfx.SetActive(false);
        }

        private void OnDestroyPooledTile(GameObject tile) {
            _tilePoolIndexes.Remove(tile);
            Destroy(tile);
        }

        private void OnDestroyPooledVfx(GameObject vfx) {
            _vfxPoolIndexes.Remove(vfx);
            Destroy(vfx);
        }

        private Vector3 GetTransformWorldCenter(Transform target) {
            if (target is not RectTransform rectTransform) {
                return target.position;
            }

            rectTransform.GetWorldCorners(_rectWorldCorners);
            return (_rectWorldCorners[0] + _rectWorldCorners[2]) * 0.5f;
        }

        private static TileDissolveView EnsureTileDissolveView(GameObject tile) {
            TileDissolveView dissolveView = tile.GetComponent<TileDissolveView>();
            if (dissolveView != null) {
                return dissolveView;
            }

            dissolveView = tile.GetComponentInChildren<TileDissolveView>(true);
            if (dissolveView != null) {
                return dissolveView;
            }

            return tile.AddComponent<TileDissolveView>();
        }

        #region Events

        private void TileSpot_Clicked(int x, int y) {
            TileClicked(x, y);
        }

        #endregion

        private void OnRectTransformDimensionsChange() {
            UpdateCellSize();
        }
    }
}