using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Builds generated levels in the scene and checks the geometry, population and run flow.</summary>
    public class RunTests
    {
        BiomeData _biome;
        MovementConfig _cfg;
        WeaponData _weapon;
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _biome = ScriptableObject.CreateInstance<BiomeData>();
            var grunt = ScriptableObject.CreateInstance<EnemyDefinition>();
            grunt.displayName = "TestGrunt";
            var spitter = ScriptableObject.CreateInstance<EnemyDefinition>();
            spitter.displayName = "TestSpitter";
            spitter.archetype = EnemyArchetype.RangedShooter;
            _biome.enemyPool = new[] { grunt, spitter };
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _weapon = ScriptableObject.CreateInstance<WeaponData>();
            _owned.AddRange(new Object[] { _biome, grunt, spitter, _cfg, _weapon });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        static void DestroyLevel(BuiltLevel level)
        {
            if (level.Root != null) Object.DestroyImmediate(level.Root);
        }

        // ------------------------------------------------------------------ geometry

        [Test]
        public void EachLevelBuildsOneRoomPerCellWithStartExitAndSolidSpawn()
        {
            for (uint seed = 0; seed < 25; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, _biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, _biome, false);
                Physics2D.SyncTransforms();

                Assert.AreEqual(layout.Cells.Count, level.Chunks.Count, $"seed {seed}");
                Assert.IsNotNull(level.Exit, $"seed {seed}");

                RaycastHit2D ground = Physics2D.Raycast(level.PlayerSpawn + Vector2.up * 1f, Vector2.down, 2f, GameLayers.GroundMask);
                Assert.IsNotNull(ground.collider, $"seed {seed}: no floor under the player spawn");
                Assert.AreEqual(level.PlayerSpawn.y, ground.point.y, 0.05f, $"seed {seed}");

                Rect startBounds = level.Chunks[layout.Start.Pos].WorldBounds;
                Assert.IsTrue(startBounds.Contains(level.PlayerSpawn), $"seed {seed}");

                DestroyLevel(level);
            }
        }

        [Test]
        public void EveryConnectionIsPhysicallyOpen()
        {
            for (uint seed = 100; seed < 130; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, _biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, _biome, false);
                Physics2D.SyncTransforms();
                int solid = GameLayers.GroundMask;

                foreach (var (a, b) in layout.Edges)
                {
                    Vector2 oa = ChunkMetrics.Origin(a), ob = ChunkMetrics.Origin(b);
                    if (a.y == b.y)
                    {
                        // side door: three heights across the wall plane must be clear
                        Vector2Int left = a.x < b.x ? a : b;
                        float planeX = ChunkMetrics.Origin(left).x + ChunkMetrics.Width;
                        float baseY = ChunkMetrics.Origin(left).y + ChunkMetrics.FloorTop;
                        foreach (float h in new[] { 0.6f, 3f, 5.4f })
                        {
                            var hit = Physics2D.Linecast(new Vector2(planeX - 3f, baseY + h), new Vector2(planeX + 3f, baseY + h), solid);
                            Assert.IsNull(hit.collider, $"seed {seed}: door {a}-{b} blocked at height {h} by {hit.collider?.name}");
                        }
                    }
                    else
                    {
                        // shaft: a vertical line through the hole must be clear from the lower floor to the upper floor
                        Vector2Int lower = a.y < b.y ? a : b;
                        float x = ChunkMetrics.Origin(lower).x + 15f;
                        float y0 = ChunkMetrics.Origin(lower).y + ChunkMetrics.FloorTop + 0.5f;
                        float y1 = y0 + ChunkMetrics.Height;
                        var hit = Physics2D.Linecast(new Vector2(x, y0), new Vector2(x, y1), solid);
                        Assert.IsNull(hit.collider, $"seed {seed}: shaft {a}-{b} blocked by {hit.collider?.name}");
                    }
                }
                DestroyLevel(level);
            }
        }

        [Test]
        public void EnemyPlacementIsDeterministicPerSeed()
        {
            List<string> Describe(uint seed)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, _biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, _biome, true);
                var list = new List<string>();
                foreach (var e in level.Enemies) list.Add($"{e.Definition.displayName}@{e.transform.position.x:0.0},{e.transform.position.y:0.0}");
                list.Sort();
                DestroyLevel(level);
                return list;
            }

            var first = Describe(777);
            var second = Describe(777);
            Assert.Greater(first.Count, 0);
            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreNotEqual(first, Describe(778));
        }

        [Test]
        public void LaterRoomsHoldAtLeastAsManyEnemiesAsEarlierOnes()
        {
            _biome.minEnemies = 1;
            _biome.maxEnemies = 3;
            LevelLayout layout = LevelGenerator.Generate(5, _biome.generator);
            BuiltLevel level = LevelBuilder.Build(layout, _biome, true);
            int early = 0, late = 0;
            foreach (var e in level.Enemies)
            {
                var cell = ChunkMetrics.CellAt(e.transform.position + Vector3.up);
                if (!layout.TryGet(cell, out var c)) continue;
                if (c.Progress < 0.3f) early++;
                if (c.Progress > 0.7f) late++;
            }
            DestroyLevel(level);
            Assert.GreaterOrEqual(late, early, "difficulty should ramp toward the exit");
        }

        // ------------------------------------------------------------------ run flow

        (RunManager run, PlayerController player, Health health, PlayerDefense defense) MakeRun()
        {
            Physics2D.SyncTransforms();
            var pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(pc, _weapon, _weapon);
            pc.autoTick = false;
            _spawned.Add(pc.gameObject);

            var go = new GameObject("Run");
            go.SetActive(false);
            var run = go.AddComponent<RunManager>();
            run.AutoStart = false;
            run.Bind(_biome, pc);
            go.SetActive(true);
            _spawned.Add(go);
            return (run, pc, pc.GetComponent<Health>(), pc.GetComponent<PlayerDefense>());
        }

        [Test]
        public void RunStartsDiesAndRestartsWithTheSameOrANewSeed()
        {
            var (run, pc, health, defense) = MakeRun();

            run.StartRun(4242);
            Assert.AreEqual(RunState.Running, run.State);
            Assert.AreEqual(4242u, run.Seed);
            Assert.AreEqual((Vector2)pc.transform.position, run.Level.PlayerSpawn);
            Assert.Greater(run.Level.Enemies.Count, 0);
            string signature = run.Layout.Signature();

            // permadeath: dying ends the run
            defense.Tick(5f);
            health.TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Enemy });
            Assert.AreEqual(RunState.Dead, run.State);

            run.RestartSameSeed();
            Assert.AreEqual(RunState.Running, run.State);
            Assert.AreEqual(signature, run.Layout.Signature(), "same seed must rebuild the same level");
            Assert.AreEqual(health.Max, health.Current, "health restored for the new run");
            Assert.IsFalse(health.IsDead);

            run.StartRun(4243);
            Assert.AreNotEqual(signature, run.Layout.Signature());
            Assert.AreEqual(4243u, run.Seed);
        }

        [Test]
        public void ReachingTheExitCompletesTheRun()
        {
            var (run, pc, health, defense) = MakeRun();
            run.StartRun(11);
            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Complete, run.State);
            Assert.IsFalse(pc.autoTick, "player input is frozen on the end screen");
        }

        [Test]
        public void KillingEnemiesCountsInRunStats()
        {
            var (run, pc, health, defense) = MakeRun();
            run.StartRun(21);
            Assert.Greater(run.Level.Enemies.Count, 1);
            foreach (var e in run.Level.Enemies.GetRange(0, 2))
                e.GetComponent<Health>().TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Player });
            Assert.AreEqual(2, run.Stats.Kills);
        }

        [Test]
        public void TypedSeedTextStartsTheMatchingRun()
        {
            var (run, pc, health, defense) = MakeRun();
            run.StartRunFromText(SeedCode.Encode(31337));
            Assert.AreEqual(31337u, run.Seed);
            run.StartRunFromText("drowned bells");
            Assert.IsTrue(SeedCode.TryParse("drowned bells", out uint expected));
            Assert.AreEqual(expected, run.Seed);
        }
    }
}
