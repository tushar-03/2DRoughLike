using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
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
            PlayerItemsSetup.Attach(pc);
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

        // ------------------------------------------------------------------ loot in levels

        LootDatabase UseLoot()
        {
            var db = DefaultContent.CreateDatabase();
            _biome.loot = db;
            _owned.Add(db);
            return db;
        }

        [Test]
        public void LevelsContainOneShopChestsForTreasureRoomsAndMutationAltars()
        {
            UseLoot();
            for (uint seed = 300; seed < 320; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, _biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, _biome, true);

                int treasureRooms = layout.Cells.FindAll(c => c.Role == RoomRole.Treasure).Count;
                Assert.AreEqual(1, level.Shops.Count, $"seed {seed}");
                Assert.AreEqual(treasureRooms, level.Chests.Count, $"seed {seed}");
                Assert.That(level.Altars.Count, Is.InRange(1, LevelBuilder.AltarProgress.Length), $"seed {seed}");
                foreach (var altar in level.Altars) Assert.AreEqual(3, altar.Choices.Count);
                foreach (var chest in level.Chests) Assert.IsNotNull(chest.Contents);

                // an altar sits on the main path, away from the entrance and the exit
                foreach (var altar in level.Altars)
                {
                    var cell = ChunkMetrics.CellAt(altar.transform.position + Vector3.up);
                    var c = layout.Get(cell);
                    Assert.IsTrue(c.OnMainPath && c.Role == RoomRole.Combat, $"seed {seed}");
                }
                DestroyLevel(level);
            }
        }

        [Test]
        public void LootPlacementIsDeterministicPerSeed()
        {
            UseLoot();
            string Describe(uint seed)
            {
                BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(seed, _biome.generator), _biome, false);
                var parts = new List<string>();
                foreach (var c in level.Chests) parts.Add("chest:" + c.Contents.DisplayName + c.Contents.Rarity);
                foreach (var s in level.Shops) foreach (var o in s.Stock.Offers) parts.Add("shop:" + o.Kind + (o.Item != null ? o.Item.DisplayName : o.Colour.ToString()));
                foreach (var a in level.Altars) foreach (var m in a.Choices) parts.Add("altar:" + m.displayName);
                DestroyLevel(level);
                return string.Join("|", parts);
            }
            Assert.AreEqual(Describe(901), Describe(901));
            Assert.AreNotEqual(Describe(901), Describe(902));
        }

        [Test]
        public void ClearingARoomRaisesTheRoomClearedEventOnce()
        {
            UseLoot();
            LevelLayout layout = LevelGenerator.Generate(77, _biome.generator);
            BuiltLevel level = LevelBuilder.Build(layout, _biome, true);
            var events = new List<RoomClearedEvent>();
            System.Action<RoomClearedEvent> on = e => events.Add(e);
            EventBus<RoomClearedEvent>.Subscribe(on);
            try
            {
                var tracker = level.Trackers.Find(t => t.Alive >= 1);
                Assert.IsNotNull(tracker);
                int index = tracker.Cell.Index;
                var victims = level.Enemies.FindAll(e => e.GetComponentInParent<RoomClearTracker>() == tracker);
                Assert.AreEqual(tracker.Alive, victims.Count);

                for (int i = 0; i < victims.Count; i++)
                {
                    Assert.AreEqual(0, events.Count, "not cleared until the last enemy dies");
                    victims[i].GetComponent<Health>().TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Player });
                }
                Assert.AreEqual(1, events.Count);
                Assert.AreEqual(index, events[0].CellIndex);
                Assert.IsTrue(tracker.Cleared);
            }
            finally { EventBus<RoomClearedEvent>.Unsubscribe(on); DestroyLevel(level); }
        }

        [Test]
        public void RunsBankCellsAtTheExitAndLoseThemOnDeath()
        {
            UseLoot();
            var (run, pc, health, defense) = MakeRun();
            var inv = pc.GetComponent<PlayerInventory>();

            run.StartRun(31);
            inv.AddCells(4);
            inv.AddGold(120);
            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Complete, run.State);
            Assert.AreEqual(4, run.Stats.Cells);
            Assert.IsTrue(run.Stats.CellsBanked, "the exit door banks Cells");
            Assert.AreEqual(120, run.Stats.Gold);

            run.StartRun(31);
            Assert.AreEqual(0, inv.Cells, "a new run starts empty");
            Assert.AreEqual(0, inv.Gold);
            inv.AddCells(2);
            defense.Tick(5f);
            health.TakeHit(new DamageInfo { Amount = 9999f, SourceTeam = Team.Enemy });
            Assert.AreEqual(RunState.Dead, run.State);
            Assert.AreEqual(2, run.Stats.Cells);
            Assert.IsFalse(run.Stats.CellsBanked, "unbanked Cells are lost on death");
        }

        [Test]
        public void NewRunsResetTheInventoryAndGiveTheStartingLoadout()
        {
            var db = UseLoot();
            var (run, pc, health, defense) = MakeRun();
            var inv = pc.GetComponent<PlayerInventory>();

            run.StartRun(5);
            Assert.AreEqual(db.startingWeapon1, inv.Weapons[0].Weapon);
            inv.AddScroll(Roughlike.Core.StatColor.Moss);
            inv.AddMutation(db.mutations[0]);

            run.StartRun(6);
            Assert.AreEqual(0, inv.TotalScrolls);
            Assert.AreEqual(0, inv.Mutations.Count);
            Assert.AreEqual(2, inv.FlaskCharges);
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

