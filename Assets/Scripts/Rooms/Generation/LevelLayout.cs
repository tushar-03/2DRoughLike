using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Roughlike.Rooms
{
    [Flags]
    public enum Openings
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Up = 1 << 2,
        Down = 1 << 3
    }

    public enum RoomRole
    {
        Start,
        Exit,
        Combat,
        /// <summary>Dead-end branch reward room (loot arrives in M4).</summary>
        Treasure
    }

    /// <summary>One room on the generator grid. Plain data, no Unity objects, so layouts can be tested and visualised cheaply.</summary>
    public sealed class LevelCell
    {
        public Vector2Int Pos;
        public Openings Mask;
        public RoomRole Role;
        public int Variant;
        public int Index;
        public bool OnMainPath;
        /// <summary>0..1 position along the main path (branches inherit their parent's value). Drives difficulty.</summary>
        public float Progress;
    }

    /// <summary>The result of generation: which rooms exist, where, and how they connect.</summary>
    public sealed class LevelLayout
    {
        public uint Seed;
        public readonly List<LevelCell> Cells = new List<LevelCell>();
        public readonly List<(Vector2Int a, Vector2Int b)> Edges = new List<(Vector2Int, Vector2Int)>();
        readonly Dictionary<Vector2Int, LevelCell> _byPos = new Dictionary<Vector2Int, LevelCell>();

        public LevelCell Start { get; internal set; }
        public LevelCell Exit { get; internal set; }
        public int MainPathLength { get; internal set; }

        public bool TryGet(Vector2Int pos, out LevelCell cell) => _byPos.TryGetValue(pos, out cell);
        public LevelCell Get(Vector2Int pos) => _byPos[pos];
        public bool Contains(Vector2Int pos) => _byPos.ContainsKey(pos);

        internal void Add(LevelCell cell)
        {
            cell.Index = Cells.Count;
            Cells.Add(cell);
            _byPos[cell.Pos] = cell;
        }

        /// <summary>Stable text form used by tests to compare layouts for equality.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            foreach (var c in Cells)
                sb.Append(c.Pos.x).Append(',').Append(c.Pos.y).Append(':').Append((int)c.Mask).Append(':')
                  .Append((int)c.Role).Append(':').Append(c.Variant).Append(';');
            return sb.ToString();
        }

        public static Openings DirectionTo(Vector2Int from, Vector2Int to)
        {
            Vector2Int d = to - from;
            if (d == Vector2Int.right) return Openings.Right;
            if (d == Vector2Int.left) return Openings.Left;
            if (d == Vector2Int.up) return Openings.Up;
            if (d == Vector2Int.down) return Openings.Down;
            return Openings.None;
        }

        public static Vector2Int Offset(Openings dir)
        {
            switch (dir)
            {
                case Openings.Left: return Vector2Int.left;
                case Openings.Right: return Vector2Int.right;
                case Openings.Up: return Vector2Int.up;
                case Openings.Down: return Vector2Int.down;
                default: return Vector2Int.zero;
            }
        }

        public static Openings Opposite(Openings dir)
        {
            switch (dir)
            {
                case Openings.Left: return Openings.Right;
                case Openings.Right: return Openings.Left;
                case Openings.Up: return Openings.Down;
                case Openings.Down: return Openings.Up;
                default: return Openings.None;
            }
        }
    }
}
