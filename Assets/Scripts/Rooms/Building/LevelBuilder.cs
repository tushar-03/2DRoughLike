using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>A level instantiated in the scene.</summary>
    public sealed class BuiltLevel
    {
        public GameObject Root;
        public LevelLayout Layout;
        public readonly Dictionary<Vector2Int, RoomChunk> Chunks = new Dictionary<Vector2Int, RoomChunk>();
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public readonly List<RoomClearTracker> Trackers = new List<RoomClearTracker>();
        public readonly List<MutationAltar> Altars = new List<MutationAltar>();
        public readonly List<LootChest> Chests = new List<LootChest>();
        public readonly List<ShopStand> Shops = new List<ShopStand>();
        public Vector2 PlayerSpawn;
        public ExitDoor Exit;

        public void Destroy()
        {
            if (Root == null) return;
            Root.SetActive(false);   // colliders vanish now, not at end of frame, so a new level can be built in place
            if (Application.isPlaying) Object.Destroy(Root); else Object.DestroyImmediate(Root);
            Root = null;
        }
    }

    /// <summary>
    /// Turns a <see cref="LevelLayout"/> into scene objects: rooms, enemies, the exit door, shops, chests and mutation
    /// altars. Population is deterministic: each room draws from its own fork of the run seed.
    /// </summary>
    public static class LevelBuilder
    {
        /// <summary>Progress points along the main path where a mutation altar appears.</summary>
        public static readonly float[] AltarProgress = { 0.25f, 0.5f, 0.75f };

        public static BuiltLevel Build(LevelLayout layout, BiomeData biome, bool spawnEnemies = true)
        {
            var built = new BuiltLevel { Layout = layout, Root = new GameObject($"Level {layout.Seed}") };
            var masterRng = new SeededRng(layout.Seed);
            LootDatabase loot = biome.loot;

            HashSet<int> altarCells = loot != null ? PickAltarCells(layout) : new HashSet<int>();

            foreach (var cell in layout.Cells)
            {
                RoomChunk chunk = ChunkBuilder.Build(cell, built.Root.transform, biome);
                built.Chunks[cell.Pos] = chunk;

                switch (cell.Role)
                {
                    case RoomRole.Start:
                        built.PlayerSpawn = chunk.PlayerSpawn;
                        break;
                    case RoomRole.Exit:
                        built.Exit = ExitDoor.Create(chunk.transform, chunk.ExitAnchor, biome.accent);
                        break;
                    case RoomRole.Treasure:
                        if (loot != null) PlaceChest(masterRng.Fork(4000 + cell.Index), loot, cell, chunk, built);
                        else CreateTreasureMarker(chunk);
                        break;
                    case RoomRole.Shop:
                        if (loot != null) PlaceShop(masterRng.Fork(3000 + cell.Index), loot, cell, chunk, built);
                        break;
                }

                bool hasAltar = altarCells.Contains(cell.Index);
                if (hasAltar) PlaceAltar(masterRng.Fork(5000 + cell.Index), loot, chunk, built);

                if (spawnEnemies && cell.Role == RoomRole.Combat)
                    SpawnEnemies(masterRng.Fork(1000 + cell.Index), cell, chunk, biome, built, hasAltar ? chunk.ExitAnchor : (Vector2?)null);
            }
            return built;
        }

        /// <summary>One main-path combat room per altar threshold (the first one at or past it).</summary>
        static HashSet<int> PickAltarCells(LevelLayout layout)
        {
            var picked = new HashSet<int>();
            foreach (float threshold in AltarProgress)
            {
                foreach (var c in layout.Cells)
                {
                    if (!c.OnMainPath || c.Role != RoomRole.Combat || picked.Contains(c.Index)) continue;
                    if (c.Progress < threshold) continue;
                    picked.Add(c.Index);
                    break;
                }
            }
            return picked;
        }

        static void PlaceChest(SeededRng rng, LootDatabase loot, LevelCell cell, RoomChunk chunk, BuiltLevel built)
        {
            ItemInstance contents = LootRoller.Roll(loot, rng, cell.Progress, null, 0.6f);
            var chest = LootFactory.SpawnChest(chunk.LootAnchor, contents);
            chest.transform.SetParent(chunk.transform, true);
            built.Chests.Add(chest);
        }

        static void PlaceShop(SeededRng rng, LootDatabase loot, LevelCell cell, RoomChunk chunk, BuiltLevel built)
        {
            var stock = ShopStock.Generate(loot, rng, cell.Progress);
            var shop = LootFactory.SpawnShop(chunk.LootAnchor, stock);
            shop.transform.SetParent(chunk.transform, true);
            built.Shops.Add(shop);
        }

        static void PlaceAltar(SeededRng rng, LootDatabase loot, RoomChunk chunk, BuiltLevel built)
        {
            var choices = LootRoller.RollMutationChoices(loot, rng, 3, null);
            if (choices.Count == 0) return;
            var altar = LootFactory.SpawnAltar(chunk.ExitAnchor, choices);
            altar.transform.SetParent(chunk.transform, true);
            built.Altars.Add(altar);
        }

        static void SpawnEnemies(SeededRng rng, LevelCell cell, RoomChunk chunk, BiomeData biome, BuiltLevel built, Vector2? keepClear)
        {
            if (biome.enemyPool == null || biome.enemyPool.Length == 0) return;

            int count = Mathf.RoundToInt(Mathf.Lerp(biome.minEnemies, biome.maxEnemies, cell.Progress));
            var spots = new List<Vector2>(chunk.EnemySpawns);
            if (keepClear.HasValue) spots.RemoveAll(p => Mathf.Abs(p.x - keepClear.Value.x) < 3f);
            rng.Shuffle(spots);
            count = Mathf.Min(count, spots.Count);

            var tracker = chunk.gameObject.AddComponent<RoomClearTracker>();
            tracker.Init(cell, chunk);
            built.Trackers.Add(tracker);

            for (int i = 0; i < count; i++)
            {
                EnemyDefinition def = rng.Pick(biome.enemyPool);
                EnemyController e = EnemyFactory.Build(def, spots[i] + Vector2.up * 0.05f);
                e.transform.SetParent(chunk.transform, true);
                built.Enemies.Add(e);
                tracker.Register(e.GetComponent<Health>());
            }
        }

        /// <summary>Marker used only when a biome has no loot database.</summary>
        static void CreateTreasureMarker(RoomChunk chunk)
        {
            var go = new GameObject("Treasure");
            go.transform.SetParent(chunk.transform, false);
            go.transform.position = chunk.LootAnchor;
            LootFactory.Label(go.transform, "TREASURE", new Color(1f, 0.9f, 0.5f, 0.9f), 0.1f, new Vector3(0f, 2.2f, 0f));
        }
    }
}
