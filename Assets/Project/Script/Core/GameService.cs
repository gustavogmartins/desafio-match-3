using System.Collections.Generic;
using Gazeus.DesafioMatch3.Models;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Core
{
    public class GameService
    {
        private List<List<Tile>> _boardTiles;
        private List<int> _tilesTypes;
        private int _tileCount;

        public bool IsValidMovement(int fromX, int fromY, int toX, int toY)
        {
            List<List<Tile>> newBoard = CopyBoard(_boardTiles);

            (newBoard[toY][toX], newBoard[fromY][fromX]) = (newBoard[fromY][fromX], newBoard[toY][toX]);
            return FindMatchGroups(newBoard).Count > 0;
        }

        public List<List<Tile>> StartGame(int boardWidth, int boardHeight)
        {
            _tilesTypes = new List<int> { 0, 1, 2, 3 };
            _boardTiles = CreateBoard(boardWidth, boardHeight, _tilesTypes);

            return _boardTiles;
        }

        public List<BoardSequence> SwapTile(int fromX, int fromY, int toX, int toY)
        {
            List<List<Tile>> newBoard = CopyBoard(_boardTiles);

            (newBoard[toY][toX], newBoard[fromY][fromX]) = (newBoard[fromY][fromX], newBoard[toY][toX]);
            Vector2Int preferredSpecialPosition = new Vector2Int(toX, toY);
            
            List<BoardSequence> boardSequences = new();

            List<MatchGroup> matchGroups = FindMatchGroups(newBoard);
            
            while (matchGroups.Count > 0)
            {
                List<Vector2Int> matchedPosition = GetUniqueMatchedPositions(matchGroups);

                //Creating special tiles
                Dictionary<Vector2Int, SpecialTileCreationInfo> specialTilesToCreate = new();
                for (int i = 0; i < matchGroups.Count; i++) {
                    MatchGroup matchGroup = matchGroups[i];
                    TileSpecialType specialType = GetSpecialType(matchGroup);

                    if (specialType == TileSpecialType.None) {
                        continue;
                    }

                    Vector2Int specialPosition = GetSpecialTilePosition(matchGroup, preferredSpecialPosition);

                    specialTilesToCreate[specialPosition] = new SpecialTileCreationInfo {
                        Type = matchGroup.Type,
                        SpecialType = specialType
                    };
                }
                
                List<Vector2Int> removedPositions = new();
                for (int i = 0; i < matchedPosition.Count; i++) {
                    Vector2Int position = matchedPosition[i];
                    if (specialTilesToCreate.ContainsKey(position)) {
                        continue;
                    }

                    removedPositions.Add(position);
                }

                for (int i = 0; i < removedPositions.Count; i++) {
                    Vector2Int position = removedPositions[i];

                    newBoard[position.y][position.x] = new Tile {
                        Id = -1,
                        Type = -1,
                        SpecialType = TileSpecialType.None
                    };
                }

                foreach (KeyValuePair<Vector2Int, SpecialTileCreationInfo> pair in specialTilesToCreate) {
                    Vector2Int position = pair.Key;
                    SpecialTileCreationInfo specialTile = pair.Value;

                    newBoard[position.y][position.x] = new Tile {
                        Id = _tileCount++,
                        Type = specialTile.Type,
                        SpecialType = specialTile.SpecialType
                    };
                }

                // Dropping the tiles
                Dictionary<int, MovedTileInfo> movedTiles = new();
                List<MovedTileInfo> movedTilesList = new();
                for (int i = 0; i < removedPositions.Count; i++)
                {
                    int x = removedPositions[i].x;
                    int y = removedPositions[i].y;
                    if (y > 0)
                    {
                        for (int j = y; j > 0; j--)
                        {
                            Tile movedTile = newBoard[j - 1][x];
                            newBoard[j][x] = movedTile;
                            if (movedTile.Type > -1)
                            {
                                if (movedTiles.ContainsKey(movedTile.Id))
                                {
                                    movedTiles[movedTile.Id].To = new Vector2Int(x, j);
                                }
                                else
                                {
                                    MovedTileInfo movedTileInfo = new()
                                    {
                                        From = new Vector2Int(x, j - 1),
                                        To = new Vector2Int(x, j)
                                    };
                                    movedTiles.Add(movedTile.Id, movedTileInfo);
                                    movedTilesList.Add(movedTileInfo);
                                }
                            }
                        }

                        newBoard[0][x] = new Tile
                        {
                            Id = -1,
                            Type = -1
                        };
                    }
                }

                // Filling the board
                List<AddedTileInfo> addedTiles = new();
                for (int y = newBoard.Count - 1; y > -1; y--)
                {
                    for (int x = newBoard[y].Count - 1; x > -1; x--)
                    {
                        if (newBoard[y][x].Type == -1)
                        {
                            int tileType = Random.Range(0, _tilesTypes.Count);
                            Tile tile = newBoard[y][x];
                            tile.Id = _tileCount++;
                            tile.Type = _tilesTypes[tileType];
                            addedTiles.Add(new AddedTileInfo
                            {
                                Position = new Vector2Int(x, y),
                                Type = tile.Type
                            });
                        }
                    }
                }

                BoardSequence sequence = new()
                {
                    MatchGroups = matchGroups,
                    RemovedPositions = removedPositions,
                    MovedTiles = movedTilesList,
                    AddedTiles = addedTiles
                };

                boardSequences.Add(sequence);
                matchGroups = FindMatchGroups(newBoard);
            }

            _boardTiles = newBoard;
            return boardSequences;
        }

        private static List<List<Tile>> CopyBoard(List<List<Tile>> boardToCopy)
        {
            List<List<Tile>> newBoard = new(boardToCopy.Count);
            for (int y = 0; y < boardToCopy.Count; y++)
            {
                newBoard.Add(new List<Tile>(boardToCopy[y].Count));
                for (int x = 0; x < boardToCopy[y].Count; x++)
                {
                    Tile tile = boardToCopy[y][x];
                    newBoard[y].Add(new Tile { Id = tile.Id, Type = tile.Type, SpecialType = tile.SpecialType });
                }
            }

            return newBoard;
        }

        private List<List<Tile>> CreateBoard(int width, int height, List<int> tileTypes)
        {
            List<List<Tile>> board = new(height);
            _tileCount = 0;
            for (int y = 0; y < height; y++)
            {
                board.Add(new List<Tile>(width));
                for (int x = 0; x < width; x++)
                {
                    board[y].Add(new Tile { Id = -1, Type = -1 });
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    List<int> noMatchTypes = new(tileTypes.Count);
                    for (int i = 0; i < tileTypes.Count; i++)
                    {
                        noMatchTypes.Add(_tilesTypes[i]);
                    }

                    if (x > 1 &&
                        board[y][x - 1].Type == board[y][x - 2].Type)
                    {
                        noMatchTypes.Remove(board[y][x - 1].Type);
                    }

                    if (y > 1 &&
                        board[y - 1][x].Type == board[y - 2][x].Type)
                    {
                        noMatchTypes.Remove(board[y - 1][x].Type);
                    }

                    board[y][x].Id = _tileCount++;
                    board[y][x].Type = noMatchTypes[Random.Range(0, noMatchTypes.Count)];
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

                    if (type > -1 && size >= 3) {
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

                    if (type > -1 && size >= 3) {
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

        private static List<Vector2Int> GetUniqueMatchedPositions(List<MatchGroup> matchGroups) {
            HashSet<Vector2Int> uniquePositions = new();

            for (int i = 0; i < matchGroups.Count; i++) {
                MatchGroup matchGroup = matchGroups[i];

                for (int j = 0; j < matchGroup.Positions.Count; j++) {
                    uniquePositions.Add(matchGroup.Positions[j]);
                }
            }

            //Ordering the positions to avoid processing the drop of tiles incorrectly when multiple tiles are removed
            List<Vector2Int> positions = new(uniquePositions);
            positions.Sort((a, b) =>
            {
                int yComparison = a.y.CompareTo(b.y);
                return yComparison != 0 ? yComparison : a.x.CompareTo(b.x);
            });

            return positions;
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
    }
}
