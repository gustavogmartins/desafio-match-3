using System.Collections.Generic;
using Gazeus.DesafioMatch3.Models;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Core {
    public class GameService {
        private const int EmptyTileType = -1;

        private List<List<Tile>> _boardTiles;
        private List<int> _tilesTypes;
        private int _tileCount;

        public bool IsValidMovement(int fromX, int fromY, int toX, int toY) {
            Vector2Int from = new(fromX, fromY);
            Vector2Int to = new(toX, toY);

            if (IsSpecialTileOnBoard(_boardTiles, from) || IsSpecialTileOnBoard(_boardTiles, to)) {
                return true;
            }

            List<List<Tile>> newBoard = CopyBoard(_boardTiles);
            SwapTiles(newBoard, from, to);

            return FindMatchGroups(newBoard).Count > 0;
        }

        public bool IsSpecialTile(int x, int y) {
            return IsSpecialTileOnBoard(_boardTiles, new Vector2Int(x, y));
        }

        public List<List<Tile>> StartGame(int boardWidth, int boardHeight) {
            _tilesTypes = new List<int> { 0, 1, 2, 3 };
            _boardTiles = CreateBoard(boardWidth, boardHeight, _tilesTypes);

            return _boardTiles;
        }

        public List<BoardSequence> SwapTile(int fromX, int fromY, int toX, int toY) {
            Vector2Int from = new(fromX, fromY);
            Vector2Int to = new(toX, toY);
            List<List<Tile>> newBoard = CopyBoard(_boardTiles);

            Tile fromTile = newBoard[from.y][from.x];
            Tile toTile = newBoard[to.y][to.x];

            SwapTiles(newBoard, from, to);

            List<SpecialActivation> initialSpecialActivations = new();
            if (fromTile.SpecialType != TileSpecialType.None) {
                initialSpecialActivations.Add(new SpecialActivation(to, fromTile.SpecialType));
            }

            if (toTile.SpecialType != TileSpecialType.None) {
                initialSpecialActivations.Add(new SpecialActivation(from, toTile.SpecialType));
            }

            List<BoardSequence> boardSequences = ResolveBoard(newBoard, initialSpecialActivations, to);
            _boardTiles = newBoard;

            return boardSequences;
        }

        public List<BoardSequence> ActivateSpecialTile(int x, int y) {
            Vector2Int position = new(x, y);
            List<List<Tile>> newBoard = CopyBoard(_boardTiles);
            List<SpecialActivation> initialSpecialActivations = new();

            if (IsSpecialTileOnBoard(newBoard, position)) {
                initialSpecialActivations.Add(new SpecialActivation(position,
                    newBoard[position.y][position.x].SpecialType));
            }

            List<BoardSequence> boardSequences = ResolveBoard(newBoard, initialSpecialActivations, position);
            _boardTiles = newBoard;

            return boardSequences;
        }

#if UNITY_INCLUDE_TESTS
        public void SetBoardForTests(List<List<Tile>> board, List<int> tileTypes = null) {
            _boardTiles = CopyBoard(board);
            _tilesTypes = tileTypes != null ? new List<int>(tileTypes) : new List<int> { 0, 1, 2, 3 };
            _tileCount = GetNextTileId(_boardTiles);
        }

        public List<List<Tile>> GetBoardForTests() {
            return CopyBoard(_boardTiles);
        }
#endif

        private List<BoardSequence> ResolveBoard(
            List<List<Tile>> board,
            List<SpecialActivation> initialSpecialActivations,
            Vector2Int preferredSpecialPosition) {
            List<BoardSequence> boardSequences = new();
            List<SpecialActivation> pendingSpecialActivations = initialSpecialActivations;

            while (true) {
                List<MatchGroup> matchGroups = FindMatchGroups(board);
                BoardSequence sequence = ResolveBoardWave(
                    board,
                    matchGroups,
                    pendingSpecialActivations,
                    preferredSpecialPosition);

                pendingSpecialActivations = null;
                if (sequence == null) {
                    break;
                }

                boardSequences.Add(sequence);
            }

            return boardSequences;
        }

        private BoardSequence ResolveBoardWave(
            List<List<Tile>> board,
            List<MatchGroup> matchGroups,
            List<SpecialActivation> pendingSpecialActivations,
            Vector2Int preferredSpecialPosition) {
            List<SpecialActivation> specialActivations = new();
            if (pendingSpecialActivations != null) {
                specialActivations.AddRange(pendingSpecialActivations);
            }

            specialActivations.AddRange(GetSpecialActivationsFromMatches(board, matchGroups));

            HashSet<Vector2Int> removedPositions = CollectSpecialEffectPositions(board, specialActivations);
            List<MatchGroup> normalMatchGroups = GetNormalMatchGroups(board, matchGroups);
            Dictionary<Vector2Int, SpecialTileCreationInfo> specialTilesToCreate =
                GetSpecialTilesToCreate(normalMatchGroups, preferredSpecialPosition, removedPositions);

            for (int i = 0; i < normalMatchGroups.Count; i++) {
                MatchGroup matchGroup = normalMatchGroups[i];
                for (int j = 0; j < matchGroup.Positions.Count; j++) {
                    Vector2Int position = matchGroup.Positions[j];
                    if (!specialTilesToCreate.ContainsKey(position)) {
                        removedPositions.Add(position);
                    }
                }
            }

            if (removedPositions.Count == 0 && specialTilesToCreate.Count == 0) {
                return null;
            }

            List<Vector2Int> sortedRemovedPositions = SortPositions(removedPositions);
            for (int i = 0; i < sortedRemovedPositions.Count; i++) {
                Vector2Int position = sortedRemovedPositions[i];
                board[position.y][position.x] = CreateEmptyTile();
            }

            List<CreatedSpecialTileInfo> createdSpecialTiles = CreateSpecialTiles(board, specialTilesToCreate);
            CollapseAndFillBoard(board, sortedRemovedPositions, out List<MovedTileInfo> movedTiles,
                out List<AddedTileInfo> addedTiles);

            return new BoardSequence {
                MatchGroups = matchGroups,
                RemovedPositions = sortedRemovedPositions,
                MovedTiles = movedTiles,
                AddedTiles = addedTiles,
                CreatedSpecialTiles = createdSpecialTiles
            };
        }

        private HashSet<Vector2Int> CollectSpecialEffectPositions(
            List<List<Tile>> board,
            List<SpecialActivation> initialSpecialActivations) {
            HashSet<Vector2Int> removedPositions = new();
            Queue<SpecialActivation> pendingActivations = new();
            HashSet<int> queuedTileIds = new();
            HashSet<int> activatedTileIds = new();

            for (int i = 0; i < initialSpecialActivations.Count; i++) {
                SpecialActivation activation = initialSpecialActivations[i];
                TryQueueSpecialActivation(board, activation, pendingActivations, queuedTileIds, activatedTileIds);
            }

            while (pendingActivations.Count > 0) {
                SpecialActivation activation = pendingActivations.Dequeue();
                if (!IsValidPosition(board, activation.Position)) {
                    continue;
                }

                Tile activatingTile = board[activation.Position.y][activation.Position.x];
                if (activatingTile.Type == EmptyTileType ||
                    activatingTile.SpecialType == TileSpecialType.None ||
                    activatedTileIds.Contains(activatingTile.Id)) {
                    continue;
                }

                activatedTileIds.Add(activatingTile.Id);
                List<Vector2Int> effectPositions =
                    GetSpecialEffectPositions(board, activation.Position, activation.SpecialType);

                for (int i = 0; i < effectPositions.Count; i++) {
                    Vector2Int affectedPosition = effectPositions[i];
                    if (!IsValidPosition(board, affectedPosition)) {
                        continue;
                    }

                    Tile affectedTile = board[affectedPosition.y][affectedPosition.x];
                    if (affectedTile.Type == EmptyTileType) {
                        continue;
                    }

                    removedPositions.Add(affectedPosition);
                    if (affectedTile.SpecialType == TileSpecialType.None) {
                        continue;
                    }

                    TileSpecialType chainedSpecialType =
                        GetChainedSpecialType(activation.SpecialType, affectedTile.SpecialType);
                    TryQueueSpecialActivation(
                        board,
                        new SpecialActivation(affectedPosition, chainedSpecialType),
                        pendingActivations,
                        queuedTileIds,
                        activatedTileIds);
                }
            }

            return removedPositions;
        }

        private static void TryQueueSpecialActivation(
            List<List<Tile>> board,
            SpecialActivation activation,
            Queue<SpecialActivation> pendingActivations,
            HashSet<int> queuedTileIds,
            HashSet<int> activatedTileIds) {
            if (!IsValidPosition(board, activation.Position)) {
                return;
            }

            Tile tile = board[activation.Position.y][activation.Position.x];
            if (tile.Type == EmptyTileType ||
                tile.SpecialType == TileSpecialType.None ||
                queuedTileIds.Contains(tile.Id) ||
                activatedTileIds.Contains(tile.Id)) {
                return;
            }

            queuedTileIds.Add(tile.Id);
            pendingActivations.Enqueue(activation);
        }

        private static TileSpecialType GetChainedSpecialType(
            TileSpecialType triggeringSpecialType,
            TileSpecialType targetSpecialType) {
            if (triggeringSpecialType == TileSpecialType.ClearHorizontal &&
                targetSpecialType == TileSpecialType.ClearHorizontal) {
                return TileSpecialType.ClearVertical;
            }

            if (triggeringSpecialType == TileSpecialType.ClearVertical &&
                targetSpecialType == TileSpecialType.ClearVertical) {
                return TileSpecialType.ClearHorizontal;
            }

            return targetSpecialType;
        }

        private static List<Vector2Int> GetSpecialEffectPositions(
            List<List<Tile>> board,
            Vector2Int position,
            TileSpecialType specialType) {
            List<Vector2Int> positions = new();

            switch (specialType) {
                case TileSpecialType.ClearHorizontal:
                    for (int x = 0; x < board[position.y].Count; x++) {
                        positions.Add(new Vector2Int(x, position.y));
                    }

                    break;

                case TileSpecialType.ClearVertical:
                    for (int y = 0; y < board.Count; y++) {
                        positions.Add(new Vector2Int(position.x, y));
                    }

                    break;

                case TileSpecialType.ClearArea:
                    for (int y = position.y - 1; y <= position.y + 1; y++) {
                        for (int x = position.x - 1; x <= position.x + 1; x++) {
                            Vector2Int areaPosition = new(x, y);
                            if (IsValidPosition(board, areaPosition)) {
                                positions.Add(areaPosition);
                            }
                        }
                    }

                    break;
            }

            return positions;
        }

        private static List<SpecialActivation> GetSpecialActivationsFromMatches(
            List<List<Tile>> board,
            List<MatchGroup> matchGroups) {
            List<SpecialActivation> specialActivations = new();
            HashSet<int> activatedTileIds = new();

            for (int i = 0; i < matchGroups.Count; i++) {
                MatchGroup matchGroup = matchGroups[i];
                for (int j = 0; j < matchGroup.Positions.Count; j++) {
                    Vector2Int position = matchGroup.Positions[j];
                    Tile tile = board[position.y][position.x];
                    if (tile.SpecialType == TileSpecialType.None || activatedTileIds.Contains(tile.Id)) {
                        continue;
                    }

                    activatedTileIds.Add(tile.Id);
                    specialActivations.Add(new SpecialActivation(position, tile.SpecialType));
                }
            }

            return specialActivations;
        }

        private static List<MatchGroup> GetNormalMatchGroups(List<List<Tile>> board, List<MatchGroup> matchGroups) {
            List<MatchGroup> normalMatchGroups = new();
            for (int i = 0; i < matchGroups.Count; i++) {
                if (!MatchGroupContainsSpecial(board, matchGroups[i])) {
                    normalMatchGroups.Add(matchGroups[i]);
                }
            }

            return normalMatchGroups;
        }

        private static bool MatchGroupContainsSpecial(List<List<Tile>> board, MatchGroup matchGroup) {
            for (int i = 0; i < matchGroup.Positions.Count; i++) {
                Vector2Int position = matchGroup.Positions[i];
                if (board[position.y][position.x].SpecialType != TileSpecialType.None) {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<Vector2Int, SpecialTileCreationInfo> GetSpecialTilesToCreate(
            List<MatchGroup> matchGroups,
            Vector2Int preferredSpecialPosition,
            HashSet<Vector2Int> removedBySpecialEffects) {
            Dictionary<Vector2Int, SpecialTileCreationInfo> specialTilesToCreate = new();
            for (int i = 0; i < matchGroups.Count; i++) {
                MatchGroup matchGroup = matchGroups[i];
                TileSpecialType specialType = GetSpecialType(matchGroup);

                if (specialType == TileSpecialType.None) {
                    continue;
                }

                Vector2Int specialPosition = GetSpecialTilePosition(matchGroup, preferredSpecialPosition);
                if (removedBySpecialEffects.Contains(specialPosition)) {
                    continue;
                }

                specialTilesToCreate[specialPosition] = new SpecialTileCreationInfo {
                    Type = matchGroup.Type,
                    SpecialType = specialType
                };
            }

            return specialTilesToCreate;
        }

        private List<CreatedSpecialTileInfo> CreateSpecialTiles(
            List<List<Tile>> board,
            Dictionary<Vector2Int, SpecialTileCreationInfo> specialTilesToCreate) {
            List<CreatedSpecialTileInfo> createdSpecialTiles = new();
            foreach (KeyValuePair<Vector2Int, SpecialTileCreationInfo> pair in specialTilesToCreate) {
                Vector2Int position = pair.Key;
                SpecialTileCreationInfo specialTile = pair.Value;

                createdSpecialTiles.Add(new CreatedSpecialTileInfo {
                    Position = position,
                    Type = specialTile.Type,
                    SpecialType = specialTile.SpecialType
                });

                board[position.y][position.x] = CreateSpecialTile(specialTile);
            }

            return createdSpecialTiles;
        }

        private void CollapseAndFillBoard(
            List<List<Tile>> board,
            List<Vector2Int> removedPositions,
            out List<MovedTileInfo> movedTilesList,
            out List<AddedTileInfo> addedTiles) {
            Dictionary<int, MovedTileInfo> movedTiles = new();
            movedTilesList = new List<MovedTileInfo>();

            for (int i = 0; i < removedPositions.Count; i++) {
                int x = removedPositions[i].x;
                int y = removedPositions[i].y;
                if (y <= 0) {
                    continue;
                }

                for (int j = y; j > 0; j--) {
                    Tile movedTile = board[j - 1][x];
                    board[j][x] = movedTile;
                    if (movedTile.Type == EmptyTileType) {
                        continue;
                    }

                    if (movedTiles.TryGetValue(movedTile.Id, out MovedTileInfo movedTileInfo)) {
                        movedTileInfo.To = new Vector2Int(x, j);
                    } else {
                        movedTileInfo = new MovedTileInfo {
                            From = new Vector2Int(x, j - 1),
                            To = new Vector2Int(x, j)
                        };
                        movedTiles.Add(movedTile.Id, movedTileInfo);
                        movedTilesList.Add(movedTileInfo);
                    }
                }

                board[0][x] = CreateEmptyTile();
            }

            addedTiles = FillEmptyTiles(board);
        }

        private List<AddedTileInfo> FillEmptyTiles(List<List<Tile>> board) {
            List<AddedTileInfo> addedTiles = new();
            for (int y = board.Count - 1; y > -1; y--) {
                for (int x = board[y].Count - 1; x > -1; x--) {
                    if (board[y][x].Type != EmptyTileType) {
                        continue;
                    }

                    int tileType = Random.Range(0, _tilesTypes.Count);
                    Tile tile = board[y][x];
                    tile.Id = _tileCount++;
                    tile.Type = _tilesTypes[tileType];
                    tile.SpecialType = TileSpecialType.None;
                    addedTiles.Add(new AddedTileInfo {
                        Position = new Vector2Int(x, y),
                        Type = tile.Type
                    });
                }
            }

            return addedTiles;
        }

        private static List<List<Tile>> CopyBoard(List<List<Tile>> boardToCopy) {
            List<List<Tile>> newBoard = new(boardToCopy.Count);
            for (int y = 0; y < boardToCopy.Count; y++) {
                newBoard.Add(new List<Tile>(boardToCopy[y].Count));
                for (int x = 0; x < boardToCopy[y].Count; x++) {
                    Tile tile = boardToCopy[y][x];
                    newBoard[y].Add(new Tile { Id = tile.Id, Type = tile.Type, SpecialType = tile.SpecialType });
                }
            }

            return newBoard;
        }

        private List<List<Tile>> CreateBoard(int width, int height, List<int> tileTypes) {
            List<List<Tile>> board = new(height);
            _tileCount = 0;
            for (int y = 0; y < height; y++) {
                board.Add(new List<Tile>(width));
                for (int x = 0; x < width; x++) {
                    board[y].Add(CreateEmptyTile());
                }
            }

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    List<int> noMatchTypes = new(tileTypes.Count);
                    for (int i = 0; i < tileTypes.Count; i++) {
                        noMatchTypes.Add(tileTypes[i]);
                    }

                    if (x > 1 &&
                        board[y][x - 1].Type == board[y][x - 2].Type) {
                        noMatchTypes.Remove(board[y][x - 1].Type);
                    }

                    if (y > 1 &&
                        board[y - 1][x].Type == board[y - 2][x].Type) {
                        noMatchTypes.Remove(board[y - 1][x].Type);
                    }

                    board[y][x].Id = _tileCount++;
                    board[y][x].Type = noMatchTypes[Random.Range(0, noMatchTypes.Count)];
                    board[y][x].SpecialType = TileSpecialType.None;
                }
            }

            return board;
        }

        private static List<MatchGroup> FindMatchGroups(List<List<Tile>> board) {
            List<MatchGroup> matchGroups = new();

            for (int y = 0; y < board.Count; y++) {
                int x = 0;

                while (x < board[y].Count) {
                    int startX = x;
                    int type = board[y][x].Type;

                    while (x < board[y].Count && board[y][x].Type == type) {
                        x++;
                    }

                    int size = x - startX;

                    if (type > EmptyTileType && size >= 3) {
                        List<Vector2Int> positions = new();

                        for (int matchX = startX; matchX < x; matchX++) {
                            positions.Add(new Vector2Int(matchX, y));
                        }

                        matchGroups.Add(new MatchGroup {
                            Type = type,
                            Direction = MatchDirection.Horizontal,
                            Positions = positions
                        });
                    }
                }
            }

            int width = board[0].Count;

            for (int x = 0; x < width; x++) {
                int y = 0;

                while (y < board.Count) {
                    int startY = y;
                    int type = board[y][x].Type;

                    while (y < board.Count && board[y][x].Type == type) {
                        y++;
                    }

                    int size = y - startY;

                    if (type > EmptyTileType && size >= 3) {
                        List<Vector2Int> positions = new();

                        for (int matchY = startY; matchY < y; matchY++) {
                            positions.Add(new Vector2Int(x, matchY));
                        }

                        matchGroups.Add(new MatchGroup {
                            Type = type,
                            Direction = MatchDirection.Vertical,
                            Positions = positions
                        });
                    }
                }
            }

            return matchGroups;
        }

        private static List<Vector2Int> SortPositions(IEnumerable<Vector2Int> positions) {
            List<Vector2Int> sortedPositions = new(positions);
            sortedPositions.Sort((a, b) => {
                int yComparison = a.y.CompareTo(b.y);
                return yComparison != 0 ? yComparison : a.x.CompareTo(b.x);
            });

            return sortedPositions;
        }

        private static TileSpecialType GetSpecialType(MatchGroup matchGroup) {
            if (matchGroup.Size < 4) {
                return TileSpecialType.None;
            }

            if (matchGroup.Size >= 5) {
                return TileSpecialType.ClearArea;
            }

            return matchGroup.Direction == MatchDirection.Horizontal
                ? TileSpecialType.ClearHorizontal
                : TileSpecialType.ClearVertical;
        }

        private static Vector2Int GetSpecialTilePosition(MatchGroup matchGroup, Vector2Int preferredPosition) {
            if (matchGroup.Positions.Contains(preferredPosition)) {
                return preferredPosition;
            }

            return matchGroup.Positions[matchGroup.Positions.Count / 2];
        }

        private static void SwapTiles(List<List<Tile>> board, Vector2Int from, Vector2Int to) {
            (board[to.y][to.x], board[from.y][from.x]) = (board[from.y][from.x], board[to.y][to.x]);
        }

        private static bool IsSpecialTileOnBoard(List<List<Tile>> board, Vector2Int position) {
            if (!IsValidPosition(board, position)) {
                return false;
            }

            return board[position.y][position.x].SpecialType != TileSpecialType.None;
        }

        private static bool IsValidPosition(List<List<Tile>> board, Vector2Int position) {
            return board != null &&
                   position.y >= 0 &&
                   position.y < board.Count &&
                   position.x >= 0 &&
                   position.x < board[position.y].Count;
        }

        private static Tile CreateEmptyTile() {
            return new Tile {
                Id = EmptyTileType,
                Type = EmptyTileType,
                SpecialType = TileSpecialType.None
            };
        }

        private Tile CreateSpecialTile(SpecialTileCreationInfo specialTile) {
            return new Tile {
                Id = _tileCount++,
                Type = specialTile.Type,
                SpecialType = specialTile.SpecialType
            };
        }

        private static int GetNextTileId(List<List<Tile>> board) {
            int nextTileId = 0;
            for (int y = 0; y < board.Count; y++) {
                for (int x = 0; x < board[y].Count; x++) {
                    Tile tile = board[y][x];
                    if (tile.Id >= nextTileId) {
                        nextTileId = tile.Id + 1;
                    }
                }
            }

            return nextTileId;
        }

        private readonly struct SpecialActivation {
            public SpecialActivation(Vector2Int position, TileSpecialType specialType) {
                Position = position;
                SpecialType = specialType;
            }

            public Vector2Int Position { get; }
            public TileSpecialType SpecialType { get; }
        }
    }
}