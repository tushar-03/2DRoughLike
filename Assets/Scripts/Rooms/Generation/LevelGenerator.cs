using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Rooms
{
    [Serializable]
    public class GeneratorSettings
    {
        [Header("Grid")]
        public int width = 8;
        public int height = 4;

        [Header("Main path (start to exit)")]
        public int minMainPath = 7;
        public int maxMainPath = 10;
        [Tooltip("The exit must be at least this many columns from the start.")]
        public int minExitColumn = 5;
        [Range(0f, 1f), Tooltip("Chance of moving up or down instead of right at each step.")]
        public float verticalChance = 0.35f;

        [Header("Branches (optional dead-end paths)")]
        public int minBranches = 2;
        public int maxBranches = 3;
        public int minBranchLength = 1;
        public int maxBranchLength = 3;

        [Header("Rooms")]
        [Tooltip("Number of interior layouts to choose from for each room.")]
        public int variantCount = 4;
    }

    /// <summary>
    /// Builds a level layout on a grid from a seed: a main path from Start to Exit that moves right and
    /// sometimes up or down, plus dead-end branches with a Treasure room at the end. Pure logic (no scene
    /// objects), fully deterministic for a given seed and settings.
    /// </summary>
    public static class LevelGenerator
    {
        const int MaxAttempts = 64;

        public static LevelLayout Generate(uint seed, GeneratorSettings settings)
        {
            var rng = new SeededRng(seed);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                LevelLayout layout = TryGenerate(seed, settings, rng.Fork(attempt));
                if (layout != null) return layout;
            }
            return Fallback(seed, settings);
        }

        static LevelLayout TryGenerate(uint seed, GeneratorSettings s, SeededRng rng)
        {
            var visited = new HashSet<Vector2Int>();
            var path = new List<Vector2Int>();

            Vector2Int cur = new Vector2Int(0, rng.Range(0, s.height));
            path.Add(cur);
            visited.Add(cur);
            int target = rng.Range(s.minMainPath, s.maxMainPath + 1);

            while (path.Count < target)
            {
                var options = new List<Vector2Int>(3);
                bool wantVertical = rng.Chance(s.verticalChance);

                Vector2Int right = cur + Vector2Int.right;
                Vector2Int up = cur + Vector2Int.up;
                Vector2Int down = cur + Vector2Int.down;
                bool canRight = right.x < s.width && !visited.Contains(right);
                bool canUp = up.y < s.height && !visited.Contains(up);
                bool canDown = down.y >= 0 && !visited.Contains(down);

                if (wantVertical)
                {
                    if (canUp) options.Add(up);
                    if (canDown) options.Add(down);
                }
                if (options.Count == 0 && canRight) options.Add(right);
                if (options.Count == 0)
                {
                    if (canUp) options.Add(up);
                    if (canDown) options.Add(down);
                }
                if (options.Count == 0) return null;   // boxed in: try again

                cur = rng.Pick(options);
                path.Add(cur);
                visited.Add(cur);
            }

            Vector2Int exitPos = path[path.Count - 1];
            if (exitPos.x - path[0].x < s.minExitColumn) return null;

            var layout = new LevelLayout { Seed = seed, MainPathLength = path.Count };
            var edges = new List<(Vector2Int, Vector2Int)>();
            var roles = new Dictionary<Vector2Int, RoomRole>();
            var progress = new Dictionary<Vector2Int, float>();
            var mainSet = new HashSet<Vector2Int>(path);

            for (int i = 0; i < path.Count; i++)
            {
                roles[path[i]] = i == 0 ? RoomRole.Start : (i == path.Count - 1 ? RoomRole.Exit : RoomRole.Combat);
                progress[path[i]] = path.Count <= 1 ? 0f : i / (float)(path.Count - 1);
                if (i > 0) edges.Add((path[i - 1], path[i]));
            }

            // Branches: dead ends off the main path, ending in a Treasure room.
            int branchCount = rng.Range(s.minBranches, s.maxBranches + 1);
            bool shopAssigned = false;
            var dirs = new List<Vector2Int> { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            for (int b = 0; b < branchCount; b++)
            {
                if (path.Count < 3) break;
                for (int tries = 0; tries < 8; tries++)
                {
                    int anchorIndex = rng.Range(1, path.Count - 1);
                    Vector2Int anchor = path[anchorIndex];
                    rng.Shuffle(dirs);
                    bool placed = false;

                    foreach (var d in dirs)
                    {
                        Vector2Int first = anchor + d;
                        if (!InBounds(first, s) || visited.Contains(first)) continue;

                        int length = rng.Range(s.minBranchLength, s.maxBranchLength + 1);
                        var branch = new List<Vector2Int> { first };
                        visited.Add(first);
                        Vector2Int tip = first;
                        for (int k = 1; k < length; k++)
                        {
                            rng.Shuffle(dirs);
                            bool extended = false;
                            foreach (var d2 in dirs)
                            {
                                Vector2Int next = tip + d2;
                                if (!InBounds(next, s) || visited.Contains(next)) continue;
                                branch.Add(next);
                                visited.Add(next);
                                tip = next;
                                extended = true;
                                break;
                            }
                            if (!extended) break;
                        }

                        Vector2Int prev = anchor;
                        for (int k = 0; k < branch.Count; k++)
                        {
                            // the first branch ends at the merchant, later ones at treasure
                            RoomRole endRole = shopAssigned ? RoomRole.Treasure : RoomRole.Shop;
                            roles[branch[k]] = k == branch.Count - 1 ? endRole : RoomRole.Combat;
                            progress[branch[k]] = progress[anchor];
                            edges.Add((prev, branch[k]));
                            prev = branch[k];
                        }
                        shopAssigned = true;
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
            }

            // every level has a merchant: if no branch could be placed, a room on the main path becomes the shop
            if (!shopAssigned && path.Count >= 3) roles[path[path.Count / 2]] = RoomRole.Shop;

            // Cells and masks from edges.
            var masks = new Dictionary<Vector2Int, Openings>();
            foreach (var (a, b) in edges)
            {
                masks.TryGetValue(a, out Openings ma);
                masks.TryGetValue(b, out Openings mb);
                masks[a] = ma | LevelLayout.DirectionTo(a, b);
                masks[b] = mb | LevelLayout.DirectionTo(b, a);
                layout.Edges.Add((a, b));
            }

            // Deterministic order: main path first, then the rest sorted by position.
            var ordered = new List<Vector2Int>(path);
            var rest = new List<Vector2Int>();
            foreach (var p in visited) if (!mainSet.Contains(p)) rest.Add(p);
            rest.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            ordered.AddRange(rest);

            foreach (var pos in ordered)
            {
                RoomRole role = roles[pos];
                bool plain = role != RoomRole.Combat;
                var cell = new LevelCell
                {
                    Pos = pos,
                    Mask = masks.TryGetValue(pos, out Openings m) ? m : Openings.None,
                    Role = role,
                    OnMainPath = mainSet.Contains(pos),
                    Progress = progress[pos],
                    Variant = plain ? 0 : rng.Range(0, Math.Max(1, s.variantCount))
                };
                layout.Add(cell);
            }

            layout.Start = layout.Get(path[0]);
            layout.Exit = layout.Get(path[path.Count - 1]);
            return layout;
        }

        static bool InBounds(Vector2Int p, GeneratorSettings s) => p.x >= 0 && p.x < s.width && p.y >= 0 && p.y < s.height;

        /// <summary>A straight corridor, used only if random generation repeatedly fails (never in practice).</summary>
        static LevelLayout Fallback(uint seed, GeneratorSettings s)
        {
            var layout = new LevelLayout { Seed = seed, MainPathLength = Math.Max(2, s.minExitColumn + 1) };
            int n = layout.MainPathLength;
            for (int i = 0; i < n; i++)
            {
                Openings mask = (i > 0 ? Openings.Left : 0) | (i < n - 1 ? Openings.Right : 0);
                layout.Add(new LevelCell
                {
                    Pos = new Vector2Int(i, 0), Mask = mask, OnMainPath = true, Progress = i / (float)(n - 1),
                    Role = i == 0 ? RoomRole.Start : (i == n - 1 ? RoomRole.Exit : RoomRole.Combat)
                });
                if (i > 0) layout.Edges.Add((new Vector2Int(i - 1, 0), new Vector2Int(i, 0)));
            }
            layout.Start = layout.Cells[0];
            layout.Exit = layout.Cells[n - 1];
            return layout;
        }
    }
}

