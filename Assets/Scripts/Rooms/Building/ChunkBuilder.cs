using UnityEngine;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>Fixed room dimensions shared by the generator, builder and tests. Everything is derived from these.</summary>
    public static class ChunkMetrics
    {
        public const float Width = 30f;
        public const float Height = 18f;
        public const float FloorTop = 3f;
        /// <summary>Side doors span from the floor up to this height (6 units, comfortably taller than the player).</summary>
        public const float DoorTop = 9f;
        public const float ShaftMin = 12f;
        public const float ShaftMax = 18f;

        public static Vector2 Origin(Vector2Int cell) => new Vector2(cell.x * Width, cell.y * Height);
        public static Rect Bounds(Vector2Int cell) => new Rect(Origin(cell), new Vector2(Width, Height));
        public static Vector2Int CellAt(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x / Width), Mathf.FloorToInt(world.y / Height));
    }

    /// <summary>
    /// Builds a graybox room from a layout cell: slabs with door and shaft openings that match the cell's mask,
    /// a stair of one-way platforms in rooms that open upward, an interior variant, and marker points.
    /// Every opening is guaranteed passable with the base movement kit (no runes needed).
    /// </summary>
    public static class ChunkBuilder
    {
        static readonly Color Dim = new Color(0.72f, 0.72f, 0.8f);

        public const int VariantFlat = 0, VariantBlocks = 1, VariantPlatforms = 2, VariantTunnel = 3;

        sealed class Ctx
        {
            public Transform Root;
            public BiomeData Biome;
            public Vector2 Origin;
            public int Counter;
        }

        public static RoomChunk Build(LevelCell cell, Transform parent, BiomeData biome)
        {
            Vector2 origin = ChunkMetrics.Origin(cell.Pos);
            var go = new GameObject($"Room {cell.Pos.x},{cell.Pos.y} ({cell.Role})");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;

            var chunk = go.AddComponent<RoomChunk>();
            chunk.Cell = cell;
            chunk.WorldBounds = ChunkMetrics.Bounds(cell.Pos);

            var c = new Ctx { Root = go.transform, Biome = biome, Origin = origin };
            Openings m = cell.Mask;
            bool up = (m & Openings.Up) != 0, down = (m & Openings.Down) != 0;
            bool left = (m & Openings.Left) != 0, right = (m & Openings.Right) != 0;
            bool vertical = up || down;

            const float W = ChunkMetrics.Width, H = ChunkMetrics.Height, F = ChunkMetrics.FloorTop, D = ChunkMetrics.DoorTop;
            float s0 = ChunkMetrics.ShaftMin, s1 = ChunkMetrics.ShaftMax;

            // backdrop panel so room bounds read clearly (painted levels have a parallax backdrop instead)
            if (biome.art == null) Panel(c, "Backdrop", 0f, W, 0f, H, biome.background, -20);

            // floor (hole when the room opens downward)
            if (down) { Slab(c, "Floor L", 0f, s0, 0f, F); Slab(c, "Floor R", s1, W, 0f, F); }
            else Slab(c, "Floor", 0f, W, 0f, F);

            // ceiling (hole when the room opens upward)
            if (up) { Slab(c, "Ceiling L", 0f, s0, H - 1f, H, Dim); Slab(c, "Ceiling R", s1, W, H - 1f, H, Dim); }
            else Slab(c, "Ceiling", 0f, W, H - 1f, H, Dim);

            // side walls with 6-unit doors at floor level
            if (left) Slab(c, "Wall L", 0f, 1f, D, H - 1f, Dim); else Slab(c, "Wall L", 0f, 1f, F, H - 1f, Dim);
            if (right) Slab(c, "Wall R", W - 1f, W, D, H - 1f, Dim); else Slab(c, "Wall R", W - 1f, W, F, H - 1f, Dim);

            // vertical connections
            if (up)
            {
                // a diagonal stair of one-way platforms, each at most 2.6 above the last (jump height is 3.2) and overlapping the
                // previous one horizontally, so every step is a straight hop up through the platform above
                OneWay(c, "Stair 1", 5f, 11f, 5.6f);
                OneWay(c, "Stair 2", 9f, 15f, 8.2f);
                OneWay(c, "Stair 3", 13f, 19f, 10.8f);
                OneWay(c, "Stair 4", 17f, 23f, 13.4f);
                OneWay(c, "Stair 5", 12.5f, 17.5f, 15.8f);
            }
            if (down)
            {
                // landing step inside the hole so you can climb out of the shaft from the room below
                OneWay(c, "Hole step", 13f, 17f, 0.35f);
            }

            // interior variants (only in rooms without vertical openings, which already have their own geometry)
            bool plain = cell.Role != RoomRole.Combat;
            int variant = vertical || plain ? VariantFlat : cell.Variant;
            switch (variant)
            {
                case VariantBlocks:
                    Slab(c, "Block A", 8f, 11f, F, F + 2.2f);
                    Slab(c, "Block B", 19f, 22f, F, F + 2.8f);
                    break;
                case VariantPlatforms:
                    OneWay(c, "Platform A", 4f, 9f, 5.6f);
                    OneWay(c, "Platform B", 11.5f, 18.5f, 8.2f);
                    OneWay(c, "Platform C", 21f, 26f, 5.6f);
                    break;
                case VariantTunnel:
                    // 1.3-high gap above the floor: too low to walk, just right for a roll (the top is a normal walkable ledge too)
                    Slab(c, "Low ceiling", 10f, 20f, F + 1.3f, F + 2.5f);   // roll under it, or hop on top and walk across
                    break;
            }

            // markers
            chunk.PlayerSpawn = origin + new Vector2(5f, F);
            chunk.ExitAnchor = origin + new Vector2(25f, F);
            chunk.LootAnchor = origin + new Vector2(down ? 21f : 15f, F);   // never over the floor hole

            chunk.EnemySpawns.Add(origin + new Vector2(6f, F));
            chunk.EnemySpawns.Add(origin + new Vector2(15f, F));
            chunk.EnemySpawns.Add(origin + new Vector2(24f, F));
            if (variant == VariantPlatforms) chunk.EnemySpawns.Add(origin + new Vector2(15f, 8.2f));
            if (variant == VariantTunnel) chunk.EnemySpawns.RemoveAt(1);   // not inside the roll tunnel
            if (down) chunk.EnemySpawns.RemoveAll(sp => Mathf.Abs(sp.x - origin.x - 15f) < 4f && sp.y < origin.y + F + 0.1f);   // nothing spawns over the hole

            return chunk;
        }

        // ------------------------------------------------------------------ primitives

        static GameObject Block(Ctx c, string name, float x0, float x1, float y0, float y1, string layer, Color col, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.Root, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            go.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
            if (layer != null) go.layer = LayerMask.NameToLayer(layer);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.color = col;
            sr.sortingOrder = order;
            if (layer != null) go.AddComponent<BoxCollider2D>();
            return go;
        }

        /// <summary>A collider-only block (no sprite), for geometry whose painted look is drawn separately.</summary>
        static GameObject Solid(Ctx c, string name, float x0, float x1, float y0, float y1, string layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.Root, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            go.layer = LayerMask.NameToLayer(layer);
            go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, y1 - y0);
            return go;
        }

        /// <summary>A sprite tiled across a rectangle (no collider).</summary>
        static GameObject Tiled(Ctx c, string name, Sprite sprite, float x0, float x1, float y0, float y1, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.Root, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(x1 - x0, y1 - y0);
            sr.color = tint;
            sr.sortingOrder = order;
            return go;
        }

        static void Panel(Ctx c, string name, float x0, float x1, float y0, float y1, Color col, int order)
            => Block(c, name, x0, x1, y0, y1, null, col, order);

        /// <summary>Solid slab. Graybox: a flat block with a lighter top edge. Painted: tiled stone with a mossy lip along the top.</summary>
        static void Slab(Ctx c, string name, float x0, float x1, float y0, float y1) => Slab(c, name, x0, x1, y0, y1, Color.white);

        static void Slab(Ctx c, string name, float x0, float x1, float y0, float y1, Color tint)
        {
            var art = c.Biome.art;
            if (art != null && art.wall != null)
            {
                Solid(c, name, x0, x1, y0, y1, GameLayers.GroundName);
                Tiled(c, name + " stone", art.wall, x0, x1, y0, y1, tint, -5);
                if (art.floorTrim != null && y1 - y0 > 0.4f && tint == Color.white)
                    Tiled(c, name + " lip", art.floorTrim, x0, x1, y1 - 0.44f, y1 + 0.06f, Color.white, -4);
                return;
            }
            Block(c, name, x0, x1, y0, y1, GameLayers.GroundName, c.Biome.ground, -5);
            if (y1 - y0 > 0.4f) Panel(c, name + " trim", x0, x1, y1 - 0.14f, y1, c.Biome.trim, -4);
        }

        static void OneWay(Ctx c, string name, float x0, float x1, float top)
        {
            var art = c.Biome.art;
            if (art != null && art.platform != null)
            {
                Solid(c, name, x0, x1, top - 0.3f, top, GameLayers.OneWayName);
                Tiled(c, name + " ledge", art.platform, x0, x1, top - 0.34f, top + 0.04f, Color.white, -3);
                return;
            }
            Block(c, name, x0, x1, top - 0.3f, top, GameLayers.OneWayName, c.Biome.platform, -3);
        }
    }
}
