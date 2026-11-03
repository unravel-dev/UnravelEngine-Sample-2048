using System;
using System.Collections.Generic;
using Unravel.Core;

namespace Sample2048
{
    /// <summary>The way tiles slide. Up is towards row 0, the board's far edge.</summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>One numbered tile and the cell it sits in.</summary>
    public sealed class Tile
    {
        public int Value;
        public int Row;
        public int Col;

        public Tile(int value, int row, int col)
        {
            Value = value;
            Row = row;
            Col = col;
        }
    }

    /// <summary>Two tiles that became one: the survivor holds the doubled value, the consumed one slid into it.</summary>
    public struct Merge
    {
        public Tile Survivor;
        public Tile Consumed;
    }

    /// <summary>What one move did, for the view to animate.</summary>
    public sealed class MoveResult
    {
        public bool Moved;
        public int ScoreGained;
        /// <summary>Tiles whose cell changed; a survivor that slid before merging is here too.</summary>
        public readonly List<Tile> Slides = new List<Tile>();
        public readonly List<Merge> Merges = new List<Merge>();
    }

    /// <summary>
    /// The 2048 rules on a 4 x 4 grid: every tile slides as far as it can, two equal tiles merge once per move,
    /// and the game ends when no move changes the board. Row 0 is the far edge, column 0 the left one.
    /// </summary>
    public sealed class Board
    {
        public const int Size = 4;
        private const float FourChance = 0.1f;
        private readonly Tile[,] cells = new Tile[Size, Size];

        public Tile Get(int row, int col)
        {
            return cells[row, col];
        }

        public int MaxValue
        {
            get
            {
                int max = 0;
                foreach (var tile in cells)
                {
                    max = tile != null ? Math.Max(max, tile.Value) : max;
                }
                return max;
            }
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
        }

        /// <summary>Puts a 2 (or, one time in ten, a 4) on a random empty cell; null when the board is full.</summary>
        public Tile Spawn()
        {
            var empty = new List<int>();
            for (int index = 0; index < Size * Size; ++index)
            {
                if (cells[index / Size, index % Size] == null)
                {
                    empty.Add(index);
                }
            }
            if (empty.Count == 0)
            {
                return null;
            }
            int cell = empty[Random.Range(0, empty.Count)];
            var tile = new Tile(Random.Float() < FourChance ? 4 : 2, cell / Size, cell % Size);
            cells[tile.Row, tile.Col] = tile;
            return tile;
        }

        /// <summary>Slides every line towards @p direction, merging equal neighbours once each.</summary>
        public MoveResult Move(Direction direction)
        {
            var result = new MoveResult();
            for (int line = 0; line < Size; ++line)
            {
                SlideLine(direction, line, result);
            }
            return result;
        }

        /// <summary>Whether any move would change the board.</summary>
        public bool CanMove()
        {
            for (int row = 0; row < Size; ++row)
            {
                for (int col = 0; col < Size; ++col)
                {
                    var tile = cells[row, col];
                    bool equalsRight = col + 1 < Size && cells[row, col + 1] != null && tile != null && cells[row, col + 1].Value == tile.Value;
                    bool equalsBelow = row + 1 < Size && cells[row + 1, col] != null && tile != null && cells[row + 1, col].Value == tile.Value;
                    if (tile == null || equalsRight || equalsBelow)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void SlideLine(Direction direction, int line, MoveResult result)
        {
            Tile last = null;
            bool lastMerged = false;
            int target = 0;
            for (int step = 0; step < Size; ++step)
            {
                CellAt(direction, line, step, out int row, out int col);
                var tile = cells[row, col];
                if (tile == null)
                {
                    continue;
                }
                cells[row, col] = null;
                if (last != null && !lastMerged && last.Value == tile.Value)
                {
                    last.Value *= 2;
                    result.ScoreGained += last.Value;
                    tile.Row = last.Row;
                    tile.Col = last.Col;
                    result.Merges.Add(new Merge { Survivor = last, Consumed = tile });
                    result.Moved = true;
                    lastMerged = true;
                    continue;
                }
                CellAt(direction, line, target++, out int toRow, out int toCol);
                if (toRow != tile.Row || toCol != tile.Col)
                {
                    tile.Row = toRow;
                    tile.Col = toCol;
                    result.Slides.Add(tile);
                    result.Moved = true;
                }
                cells[toRow, toCol] = tile;
                last = tile;
                lastMerged = false;
            }
        }

        /// <summary>The cell @p step places from the leading edge of @p line, for a move towards @p direction.</summary>
        private static void CellAt(Direction direction, int line, int step, out int row, out int col)
        {
            switch (direction)
            {
                case Direction.Up:
                    row = step;
                    col = line;
                    break;
                case Direction.Down:
                    row = Size - 1 - step;
                    col = line;
                    break;
                case Direction.Left:
                    row = line;
                    col = step;
                    break;
                default:
                    row = line;
                    col = Size - 1 - step;
                    break;
            }
        }
    }
}
