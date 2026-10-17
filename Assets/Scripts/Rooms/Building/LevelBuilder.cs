using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Enemies;
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
    /// Turns a <see cref="LevelLayout"/> into scene objects: rooms, enemies, the exit door and treasure markers.
    /// Population is deterministic: each room draws from its own fork of the run seed.
    /// </summary>
    public static class LevelBuilder
    {
        public static BuiltLevel Build(LevelLayout layout, BiomeData biome, bool spawnEnemies = true)
        {
            var built = new BuiltLevel { Layout = layout, Root = new GameObject($"Level {layout.Seed}") };
            var masterRng = new SeededRng(layout.Seed);

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
                        CreateTreasureMarker(chunk);
                        break;
                }

                if (spawnEnemies && cell.Role == RoomRole.Combat) SpawnEnemies(masterRng.Fork(1000 + cell.Index), cell, chunk, biome, built);
            }
            return built;
        }

        static void SpawnEnemies(SeededRng rng, LevelCell cell, RoomChunk chunk, BiomeData biome, BuiltLevel built)
        {
            if (biome.enemyPool == null || biome.enemyPool.Length == 0) return;

            int count = Mathf.RoundToInt(Mathf.Lerp(biome.minEnemies, biome.maxEnemies, cell.Progress));
            var spots = new List<Vector2>(chunk.EnemySpawns);
            rng.Shuffle(spots);
            count = Mathf.Min(count, spots.Count);

            for (int i = 0; i < count; i++)
            {
                EnemyDefinition def = rng.Pick(biome.enemyPool);
                EnemyController e = EnemyFactory.Build(def, spots[i] + Vector2.up * 0.05f);
                e.transform.SetParent(chunk.transform, true);
                built.Enemies.Add(e);
            }
        }

        static void CreateTreasureMarker(RoomChunk chunk)
        {
            var go = new GameObject("Treasure (loot arrives in M4)");
            go.transform.SetParent(chunk.transform, false);
            go.transform.position = chunk.LootAnchor;

            var body = new GameObject("Chest");
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(1.3f, 0.9f, 1f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square; sr.sortingOrder = 3;
            sr.color = new Color(0.9f, 0.7f, 0.25f);

            var lid = new GameObject("Lid");
            lid.transform.SetParent(go.transform, false);
            lid.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            lid.transform.localScale = new Vector3(1.4f, 0.25f, 1f);
            var lsr = lid.AddComponent<SpriteRenderer>();
            lsr.sprite = ProceduralSprites.Square; lsr.sortingOrder = 4;
            lsr.color = new Color(0.65f, 0.45f, 0.15f);

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var tm = label.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.fontSize = 48; tm.characterSize = 0.1f; tm.anchor = TextAnchor.MiddleCenter;
            tm.text = "TREASURE"; tm.color = new Color(1f, 0.9f, 0.5f, 0.9f);
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.GetComponent<MeshRenderer>().sortingOrder = 6;
        }
    }
}

