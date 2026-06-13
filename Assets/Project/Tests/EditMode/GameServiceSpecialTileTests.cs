using System.Collections.Generic;
using Gazeus.DesafioMatch3.Core;
using Gazeus.DesafioMatch3.Models;
using NUnit.Framework;
using UnityEngine;

namespace Gazeus.DesafioMatch3.Tests
{
    public class GameServiceSpecialTileTests
    {
        [SetUp]
        public void SetUp()
        {
            Random.InitState(0);
        }

        [Test]
        public void SwapWithSpecial_IsValidWithoutMatch()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(0, 0, TileSpecialType.ClearHorizontal));

            Assert.IsTrue(service.IsValidMovement(0, 0, 1, 0));
        }

        [Test]
        public void ActivateSpecialTile_ClearHorizontal_RemovesWholeRow()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(2, 2, TileSpecialType.ClearHorizontal));

            List<BoardSequence> sequences = service.ActivateSpecialTile(2, 2);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Row(2));
        }

        [Test]
        public void ActivateSpecialTile_ClearVertical_RemovesWholeColumn()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(2, 2, TileSpecialType.ClearVertical));

            List<BoardSequence> sequences = service.ActivateSpecialTile(2, 2);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Column(2));
        }

        [Test]
        public void ActivateSpecialTile_ClearArea_RemovesCenteredThreeByThree()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(2, 2, TileSpecialType.ClearArea));

            List<BoardSequence> sequences = service.ActivateSpecialTile(2, 2);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Area3x3(2, 2));
            AssertSpecialEffect(sequences[0], new Vector2Int(2, 2), TileSpecialType.ClearArea, Area3x3(2, 2));
        }

        [Test]
        public void ActivateSpecialTile_ClearAreaInCorner_ReportsOnlyValidSpecialEffectPositions()
        {
            List<Vector2Int> expectedArea = new()
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1)
            };

            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(0, 0, TileSpecialType.ClearArea));

            List<BoardSequence> sequences = service.ActivateSpecialTile(0, 0);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], expectedArea);
            AssertSpecialEffect(sequences[0], new Vector2Int(0, 0), TileSpecialType.ClearArea, expectedArea);
        }

        [Test]
        public void MatchContainingSpecial_ActivatesSpecialInsteadOfRemovingAsNormalTile()
        {
            int[,] types = CreateBaseTypes();
            types[2, 0] = 1;
            types[2, 1] = 1;
            types[2, 2] = 2;
            types[2, 3] = 1;
            types[2, 4] = 3;
            GameService service = CreateService(
                types,
                new SpecialPlacement(0, 2, TileSpecialType.ClearVertical));

            List<BoardSequence> sequences = service.SwapTile(2, 2, 3, 2);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Column(0));
        }

        [Test]
        public void HorizontalSpecialHittingHorizontalSpecial_ActivatesSecondAsVertical()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(0, 2, TileSpecialType.ClearHorizontal),
                new SpecialPlacement(3, 2, TileSpecialType.ClearHorizontal));

            List<BoardSequence> sequences = service.ActivateSpecialTile(0, 2);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Union(Row(2), Column(3)));
        }

        [Test]
        public void VerticalSpecialHittingVerticalSpecial_ActivatesSecondAsHorizontal()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(2, 0, TileSpecialType.ClearVertical),
                new SpecialPlacement(2, 3, TileSpecialType.ClearVertical));

            List<BoardSequence> sequences = service.ActivateSpecialTile(2, 0);

            Assert.Greater(sequences.Count, 0);
            AssertRemovedPositions(sequences[0], Union(Column(2), Row(3)));
        }

        [Test]
        public void SpecialActivation_AppliesGravityFillAndResolvesRemainingMatches()
        {
            GameService service = CreateService(
                CreateBaseTypes(),
                new SpecialPlacement(2, 3, TileSpecialType.ClearHorizontal));

            List<BoardSequence> sequences = service.ActivateSpecialTile(2, 3);

            Assert.Greater(sequences.Count, 0);
            Assert.Greater(sequences[0].MovedTiles.Count, 0);
            Assert.AreEqual(5, sequences[0].AddedTiles.Count);
            AssertBoardHasNoEmptyTiles(service.GetBoardForTests());
            AssertBoardHasNoMatches(service.GetBoardForTests());
        }

        private static GameService CreateService(int[,] types, params SpecialPlacement[] specialPlacements)
        {
            GameService service = new();
            service.SetBoardForTests(CreateBoard(types, specialPlacements), new List<int> { 0, 1, 2, 3 });
            return service;
        }

        private static List<List<Tile>> CreateBoard(int[,] types, params SpecialPlacement[] specialPlacements)
        {
            List<List<Tile>> board = new();
            int tileId = 0;
            int height = types.GetLength(0);
            int width = types.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                board.Add(new List<Tile>(width));
                for (int x = 0; x < width; x++)
                {
                    board[y].Add(new Tile
                    {
                        Id = tileId++,
                        Type = types[y, x],
                        SpecialType = TileSpecialType.None
                    });
                }
            }

            for (int i = 0; i < specialPlacements.Length; i++)
            {
                SpecialPlacement placement = specialPlacements[i];
                board[placement.Y][placement.X].SpecialType = placement.SpecialType;
            }

            return board;
        }

        private static int[,] CreateBaseTypes()
        {
            return new[,]
            {
                { 0, 1, 2, 3, 0 },
                { 1, 2, 3, 0, 1 },
                { 2, 3, 0, 1, 2 },
                { 3, 0, 1, 2, 3 },
                { 0, 1, 2, 3, 0 }
            };
        }

        private static List<Vector2Int> Row(int y)
        {
            List<Vector2Int> positions = new();
            for (int x = 0; x < 5; x++)
            {
                positions.Add(new Vector2Int(x, y));
            }

            return positions;
        }

        private static List<Vector2Int> Column(int x)
        {
            List<Vector2Int> positions = new();
            for (int y = 0; y < 5; y++)
            {
                positions.Add(new Vector2Int(x, y));
            }

            return positions;
        }

        private static List<Vector2Int> Area3x3(int centerX, int centerY)
        {
            List<Vector2Int> positions = new();
            for (int y = centerY - 1; y <= centerY + 1; y++)
            {
                for (int x = centerX - 1; x <= centerX + 1; x++)
                {
                    positions.Add(new Vector2Int(x, y));
                }
            }

            return positions;
        }

        private static List<Vector2Int> Union(List<Vector2Int> first, List<Vector2Int> second)
        {
            HashSet<Vector2Int> positions = new(first);
            for (int i = 0; i < second.Count; i++)
            {
                positions.Add(second[i]);
            }

            return new List<Vector2Int>(positions);
        }

        private static void AssertRemovedPositions(BoardSequence sequence, List<Vector2Int> expectedPositions)
        {
            HashSet<Vector2Int> expected = new(expectedPositions);
            HashSet<Vector2Int> actual = new(sequence.RemovedPositions);

            Assert.AreEqual(expected.Count, actual.Count);
            foreach (Vector2Int position in expected)
            {
                Assert.IsTrue(actual.Contains(position), $"Expected removed position {position}.");
            }
        }

        private static void AssertSpecialEffect(
            BoardSequence sequence,
            Vector2Int expectedOrigin,
            TileSpecialType expectedSpecialType,
            List<Vector2Int> expectedAffectedPositions)
        {
            Assert.IsNotNull(sequence.SpecialEffects);
            Assert.AreEqual(1, sequence.SpecialEffects.Count);

            SpecialEffectAnimationInfo specialEffect = sequence.SpecialEffects[0];
            Assert.AreEqual(expectedOrigin, specialEffect.Origin);
            Assert.AreEqual(expectedSpecialType, specialEffect.SpecialType);

            HashSet<Vector2Int> expected = new(expectedAffectedPositions);
            HashSet<Vector2Int> actual = new(specialEffect.AffectedPositions);

            Assert.AreEqual(expected.Count, actual.Count);
            foreach (Vector2Int position in expected)
            {
                Assert.IsTrue(actual.Contains(position), $"Expected special effect position {position}.");
            }
        }

        private static void AssertBoardHasNoEmptyTiles(List<List<Tile>> board)
        {
            for (int y = 0; y < board.Count; y++)
            {
                for (int x = 0; x < board[y].Count; x++)
                {
                    Assert.GreaterOrEqual(board[y][x].Type, 0, $"Expected tile at {x}, {y} to be filled.");
                }
            }
        }

        private static void AssertBoardHasNoMatches(List<List<Tile>> board)
        {
            for (int y = 0; y < board.Count; y++)
            {
                for (int x = 0; x < board[y].Count - 2; x++)
                {
                    Assert.IsFalse(
                        board[y][x].Type == board[y][x + 1].Type &&
                        board[y][x].Type == board[y][x + 2].Type,
                        $"Unexpected horizontal match at {x}, {y}.");
                }
            }

            for (int x = 0; x < board[0].Count; x++)
            {
                for (int y = 0; y < board.Count - 2; y++)
                {
                    Assert.IsFalse(
                        board[y][x].Type == board[y + 1][x].Type &&
                        board[y][x].Type == board[y + 2][x].Type,
                        $"Unexpected vertical match at {x}, {y}.");
                }
            }
        }

        private readonly struct SpecialPlacement
        {
            public SpecialPlacement(int x, int y, TileSpecialType specialType)
            {
                X = x;
                Y = y;
                SpecialType = specialType;
            }

            public int X { get; }
            public int Y { get; }
            public TileSpecialType SpecialType { get; }
        }
    }
}
