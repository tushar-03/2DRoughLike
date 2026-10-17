using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Rooms
{
    /// <summary>
    /// Packs solid rock around and between the rooms so the level reads as a cave carved into the ground, not slabs floating in
    /// the void: every grid cell near the level that is not a room becomes a dark block of stone (visual only, rooms already
    /// have their own colliders). Painted levels use the dark stone tile; graybox levels a dark flat colour.
    /// </summary>
    public static class TerrainFill
    {
        /// <summary>How many empty cells of rock to draw beyond the outermost rooms.</summary>
        public const int Margin = 2;

        public static void Build(LevelLayout layout, Transform parent, BiomeData biome)
        {
            if (layout == null || layout.Cells.Count == 0) return;

            var rooms = new HashSet<Vector2Int>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in layout.Cells)
            {
                rooms.Add(c.Pos);
                minX = Mathf.Min(minX, c.Pos.x); maxX = Mathf.Max(maxX, c.Pos.x);
                minY = Mathf.Min(minY, c.Pos.y); maxY = Mathf.Max(maxY, c.Pos.y);
            }

            var root = new GameObject("Rock").transform;
            root.SetParent(parent, false);

            Sprite tile = biome.art != null ? (biome.art.rock != null ? biome.art.rock : biome.art.wall) : null;
            Color tint = tile != null ? new Color(0.5f, 0.5f, 0.58f) : biome.ground * 0.45f;
            Rect bounds;

            for (int y = minY - Margin; y <= maxY + Margin; y++)
            for (int x = minX - Margin; x <= maxX + Margin; x++)
            {
                var pos = new Vector2Int(x, y);
                if (rooms.Contains(pos)) continue;
                bounds = ChunkMetrics.Bounds(pos);

                var go = new GameObject($"Rock {x},{y}");
                go.transform.SetParent(root, false);
                go.transform.position = bounds.center;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -7;
                if (tile != null)
                {
                    sr.sprite = tile;
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.tileMode = SpriteTileMode.Continuous;
                    sr.size = bounds.size;
                    sr.color = tint;
                }
                else
                {
                    sr.sprite = Roughlike.Utils.ProceduralSprites.Square;
                    sr.color = tint;
                    go.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
                }
            }
        }
    }
}
