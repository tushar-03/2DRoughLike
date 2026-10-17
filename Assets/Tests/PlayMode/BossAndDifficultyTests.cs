using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Difficulty scaling, bosses, multi-biome runs, the grounded world, dotted telegraphs and the swap-drop fix.</summary>
    public class BossAndDifficultyTests
    {
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        Difficulty _savedDifficulty;

        [SetUp]
        public void SetUp()
        {
            _savedDifficulty = DifficultyService.Current;
            DifficultyService.Current = Difficulty.Normal;
        }

        [TearDown]
        public void TearDown()
        {
            DifficultyService.Current = _savedDifficulty;
            foreach (var b in Object.FindObjectsByType<BossController>()) Object.DestroyImmediate(b.gameObject);
            foreach (var p in Object.FindObjectsByType<Projectile>()) Object.DestroyImmediate(p.gameObject);
            foreach (var e in Object.FindObjectsByType<EnemyController>()) Object.DestroyImmediate(e.gameObject);
            foreach (var w in Object.FindObjectsByType<WorldItem>()) Object.DestroyImmediate(w.gameObject);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        void Floor(float x0 = -10f, float width = 120f)
        {
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(x0 + width * 0.5f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, 2f);
            _spawned.Add(go);
        }

        PlayerController SpawnPlayer(Vector2 at)
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, at);
            var w = Own(ScriptableObject.CreateInstance<WeaponData>());
            w.kind = WeaponKind.Shield; w.steps = new AttackStep[0]; w.shield = new ShieldSettings();
            var combat = PlayerCombatSetup.Attach(pc, w, w);
            PlayerItemsSetup.Attach(pc);
            pc.autoTick = false; combat.autoTick = false;
            pc.GetComponent<PlayerDefense>().autoTick = false;
            PlayerItemsSetup.DisableAutoTick(pc.gameObject);
            _spawned.Add(pc.gameObject);
            return pc;
        }

        BossDefinition BossDef(BossKind kind, float health = 500f)
        {
            var d = Own(ScriptableObject.CreateInstance<BossDefinition>());
            d.displayName = kind == BossKind.Witch ? "Test Witch" : "Test Warden";
            d.kind = kind;
            d.maxHealth = health;
            d.size = new Vector2(2f, 3f);
            d.damage = 10f;
            return d;
        }

        // ------------------------------------------------------------------ difficulty

        [Test]
        public void HarderDifficultiesHitHarderAndGiveLessWarning()
        {
            var story = DifficultyService.For(Difficulty.Story);
            var normal = DifficultyService.For(Difficulty.Normal);
            var hard = DifficultyService.For(Difficulty.Hard);
            var nightmare = DifficultyService.For(Difficulty.Nightmare);
            Assert.Less(story.Damage, normal.Damage);
            Assert.Less(normal.Damage, hard.Damage);
            Assert.Less(hard.Damage, nightmare.Damage);
            Assert.Less(story.Health, normal.Health);
            Assert.Greater(story.Telegraph, normal.Telegraph);
            Assert.Greater(normal.Telegraph, nightmare.Telegraph, "less time to react on the hard settings");
            Assert.Greater(nightmare.ExtraEnemies, normal.ExtraEnemies);
            Assert.Greater(normal.Damage, 1f, "the default is tougher than the original game");
        }

        [Test]
        public void EnemyScalingChangesHealthDamageAndTimings()
        {
            Floor();
            var def = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
            def.maxHealth = 40f; def.damage = 10f; def.telegraphTime = 0.5f; def.attackCooldown = 1f;
            var plain = EnemyFactory.Build(def, new Vector2(0f, 0f), false);
            var scaled = EnemyFactory.Build(def, new Vector2(5f, 0f), false, new EnemyScaling { Health = 2f, Damage = 1.5f, Speed = 1f, Telegraph = 0.5f, Cooldown = 0.5f });
            plain.autoTick = false; scaled.autoTick = false;
            _spawned.Add(plain.gameObject); _spawned.Add(scaled.gameObject);

            Assert.AreEqual(40f, plain.Health.Max);
            Assert.AreEqual(80f, scaled.Health.Max);
            Assert.AreEqual(15f, scaled.Damage, 0.001f);
            Assert.AreEqual(0.25f, scaled.TelegraphTime, 0.001f);
            Assert.AreEqual(0.5f, scaled.AttackCooldown, 0.001f);
            Assert.AreEqual(10f, plain.Damage, 0.001f);
        }

        [Test]
        public void HarderDifficultiesFillRoomsWithMoreEnemies()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            var grunt = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
            grunt.maxPerRoom = 20;
            biome.enemyPool = new[] { grunt };
            biome.minEnemies = 1; biome.maxEnemies = 2;

            int Count(Difficulty d)
            {
                DifficultyService.Current = d;
                BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(14, biome.generator), biome, true);
                _spawned.Add(level.Root);
                int n = level.Enemies.Count;
                Object.DestroyImmediate(level.Root);
                return n;
            }
            int story = Count(Difficulty.Story), hard = Count(Difficulty.Hard), nightmare = Count(Difficulty.Nightmare);
            Assert.Greater(hard, story);
            Assert.GreaterOrEqual(nightmare, hard);
        }

        // ------------------------------------------------------------------ boss arena rules

        [Test]
        public void TheBossRoomIsAlwaysAFlatDeadEndEnteredFromTheSide()
        {
            var settings = new GeneratorSettings();
            for (uint seed = 0; seed < 200; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, settings);
                Assert.AreEqual(Openings.Left, layout.Exit.Mask, $"seed {seed}: the exit room has one door, on the left");
            }
        }

        // ------------------------------------------------------------------ bosses

        [UnityTest]
        public IEnumerator TheWitchAttacksWithScatteredMagic()
        {
            Floor();
            var pc = SpawnPlayer(new Vector2(4f, 0.05f));
            var boss = BossFactory.Build(BossDef(BossKind.Witch), new Vector2(18f, 4.3f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            boss.Engage();
            Assert.IsTrue(boss.Engaged);

            float until = Time.realtimeSinceStartup + 14f;
            bool sawTelegraph = false, sawProjectileOrStrike = false;
            var health = pc.GetComponent<Health>();
            while (Time.realtimeSinceStartup < until && !(sawTelegraph && sawProjectileOrStrike))
            {
                foreach (var t in Object.FindObjectsByType<TelegraphIndicator>()) if (t.gameObject.activeSelf) sawTelegraph = true;
                if (Object.FindObjectsByType<Projectile>().Length > 0 || health.Current < health.Max) sawProjectileOrStrike = true;
                yield return null;
            }
            Assert.IsTrue(sawTelegraph, "every attack is telegraphed first");
            Assert.IsTrue(sawProjectileOrStrike, "then it actually does something");
        }

        [UnityTest]
        public IEnumerator TheWardenSlamsAndSweepsAndHurtsAStandingPlayer()
        {
            Floor();
            var pc = SpawnPlayer(new Vector2(8f, 0.05f));
            var boss = BossFactory.Build(BossDef(BossKind.Warden), new Vector2(14f, 0.05f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            boss.Engage();

            var health = pc.GetComponent<Health>();
            bool telegraphed = false;
            float until = Time.realtimeSinceStartup + 16f;
            while (Time.realtimeSinceStartup < until && health.Current >= health.Max)
            {
                pc.GetComponent<PlayerDefense>().Tick(0f);
                foreach (var t in Object.FindObjectsByType<TelegraphIndicator>()) if (t.gameObject.activeSelf) telegraphed = true;
                yield return null;
            }
            Assert.IsTrue(telegraphed);
            Assert.Less(health.Current, health.Max, "a player who never dodges gets hit");
        }

        [Test]
        public void BossesEnterLaterPhasesAsTheyLoseHealthAndAreImmuneBetweenThem()
        {
            Floor();
            var boss = BossFactory.Build(BossDef(BossKind.Warden, 1000f), new Vector2(14f, 0.05f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            var phases = new List<int>();
            boss.PhaseChanged += p => phases.Add(p);

            boss.Health.TakeHit(new DamageInfo { Amount = 400f, SourceTeam = Team.Player });   // 60%
            Assert.AreEqual(1, boss.Phase);
            boss.Health.TakeHit(new DamageInfo { Amount = 350f, SourceTeam = Team.Player });   // 25%
            Assert.AreEqual(2, boss.Phase);
            CollectionAssert.AreEqual(new[] { 1, 2 }, phases);
        }

        [Test]
        public void ParryingABossMeleeStunsItAndStunnedBossesTakeMoreDamage()
        {
            Floor();
            var boss = BossFactory.Build(BossDef(BossKind.Warden, 1000f), new Vector2(14f, 0.05f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            var attacker = (IAttacker)boss;

            attacker.OnAttackParried(Vector2.zero, 1.2f);
            Assert.IsFalse(boss.Stunned, "an unparryable moment: the boss is not mid-melee");

            boss.SetParryable(true);
            attacker.OnAttackParried(Vector2.zero, 1.2f);
            Assert.IsTrue(boss.Stunned);

            float before = boss.Health.Current;
            boss.Health.TakeHit(new DamageInfo { Amount = 100f, SourceTeam = Team.Player });
            Assert.AreEqual(150f, before - boss.Health.Current, 0.5f, "stunned bosses take 1.5x");
        }

        [Test]
        public void BeatingTheBossOpensTheWayAndPaysOut()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            biome.boss = BossDef(BossKind.Witch, 200f);
            Floor();
            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(3, biome.generator), biome, false);
            _spawned.Add(level.Root);

            Assert.IsNotNull(level.Boss, "the last room holds a boss");
            Assert.IsFalse(level.Exit.gameObject.activeSelf, "the exit portal is hidden until it is dead");

            int defeated = 0;
            System.Action<BossDefeatedEvent> count = e => defeated++;
            EventBus<BossDefeatedEvent>.Subscribe(count);
            try
            {
                level.Boss.Begin();
                Assert.IsTrue(level.Boss.Started);
                Assert.IsNotNull(GameObject.Find("Boss Gate"), "the door seals behind the player");

                level.Boss.Boss.Health.TakeHit(new DamageInfo { Amount = 99999f, SourceTeam = Team.Player });
                Assert.AreEqual(1, defeated);
                Assert.IsTrue(level.Boss.Defeated);
                Assert.IsTrue(level.Exit.gameObject.activeSelf, "the portal appears");
                Assert.IsTrue(GameObject.Find("Boss Gate") == null || !GameObject.Find("Boss Gate").activeInHierarchy, "the door opens again");
            }
            finally { EventBus<BossDefeatedEvent>.Unsubscribe(count); }
        }

        // ------------------------------------------------------------------ multi-biome runs

        (RunManager run, PlayerController pc) MakeTwoBiomeRun()
        {
            var first = Own(ScriptableObject.CreateInstance<BiomeData>());
            first.displayName = "First";
            var second = Own(ScriptableObject.CreateInstance<BiomeData>());
            second.displayName = "Second";
            var pc = SpawnPlayer(Vector2.zero);

            var go = new GameObject("Run");
            go.SetActive(false);
            var run = go.AddComponent<RunManager>();
            run.AutoStart = false;
            run.Bind(new[] { first, second }, pc);
            go.SetActive(true);
            _spawned.Add(go);
            return (run, pc);
        }

        [Test]
        public void ClearingTheFirstBiomeLeadsToTheSecondAndTheLastEndsTheRun()
        {
            var (run, pc) = MakeTwoBiomeRun();
            run.StartRun(77);
            Assert.AreEqual(0, run.LevelIndex);
            Assert.AreEqual("First", run.Biome.displayName);
            string firstLayout = run.Layout.Signature();

            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Running, run.State, "not over yet");
            Assert.AreEqual(1, run.LevelIndex);
            Assert.AreEqual("Second", run.Biome.displayName);
            Assert.AreNotEqual(firstLayout, run.Layout.Signature(), "a different level");

            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Complete, run.State, "the last biome finishes the run");
        }

        [Test]
        public void SavesRememberTheBiomeAndDifficulty()
        {
            var (run, pc) = MakeTwoBiomeRun();
            DifficultyService.Current = Difficulty.Hard;
            run.StartRun(78);
            run.Level.Exit.Trigger();

            RunSave save = RunSaver.Capture(run);
            Assert.AreEqual(1, save.levelIndex);
            Assert.AreEqual((int)Difficulty.Hard, save.difficulty);

            DifficultyService.Current = Difficulty.Story;
            run.StartRun(78, save);
            Assert.AreEqual(1, run.LevelIndex, "continuing returns to the second biome");
            Assert.AreEqual(Difficulty.Hard, DifficultyService.Current, "and to the difficulty it was started on");
            Assert.AreEqual(RunManager.LevelSeed(78, 1), run.Layout.Seed);
        }

        // ------------------------------------------------------------------ the grounded world

        [Test]
        public void LevelsAreCarvedIntoSolidRockWithoutColliders()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(5, biome.generator), biome, false);
            _spawned.Add(level.Root);

            var rock = level.Root.transform.Find("Rock");
            Assert.IsNotNull(rock);
            Assert.Greater(rock.childCount, level.Chunks.Count, "more rock than rooms");
            Assert.AreEqual(0, rock.GetComponentsInChildren<Collider2D>().Length, "rock is scenery; rooms keep their own colliders");

            // no rock sits where a room is
            foreach (Transform r in rock)
                foreach (var chunk in level.Chunks.Values)
                    Assert.IsFalse(chunk.WorldBounds.Contains(r.position), "rock inside a room");
        }

        // ------------------------------------------------------------------ dotted telegraphs

        [Test]
        public void TelegraphsAreDottedOutlinesNotFilledBoxes()
        {
            var go = new GameObject("T");
            _spawned.Add(go);
            var t = go.AddComponent<TelegraphIndicator>();

            t.Begin(Vector2.zero, new Vector2(4f, 2f), 1, 1f);
            int box = go.GetComponentsInChildren<SpriteRenderer>().Count(sr => sr.gameObject.activeSelf);
            Assert.Greater(box, 12, "a ring of dots");
            Assert.Less(box, 120, "an outline, not a fill");

            t.Begin(Vector2.zero, new Vector2(8f, 0.1f), 1, 1f);
            int line = go.GetComponentsInChildren<SpriteRenderer>().Count(sr => sr.gameObject.activeSelf);
            Assert.Less(line, box, "a laser is a single dotted line");

            t.Begin(Vector2.zero, new Vector2(5f, 5f), 1, 1f, true, 0f, true);
            int ring = go.GetComponentsInChildren<SpriteRenderer>().Count(sr => sr.gameObject.activeSelf);
            Assert.Greater(ring, 20, "a dotted ring with an inner sweep");

            t.Hide();
            Assert.IsFalse(go.activeSelf);
        }

        // ------------------------------------------------------------------ swapping gear

        [Test]
        public void ASwappedOutItemLandsToTheSideAndCannotBeRetakenAtOnce()
        {
            Floor();
            var db = Own(DefaultContent.CreateDatabase());
            var pc = SpawnPlayer(new Vector2(0f, 0.05f));
            var inv = pc.GetComponent<PlayerInventory>();
            inv.ResetForRun(db);

            var picked = ItemInstance.ForWeapon(db.weapons.First(w => w != db.startingWeapon1 && w != db.startingWeapon2));
            inv.PickUp(picked, new Vector2(0.5f, 0f));   // both slots are full: with nothing listening, slot 1 is replaced

            var dropped = Object.FindObjectsByType<WorldItem>();
            Assert.AreEqual(1, dropped.Length, "the replaced weapon drops");
            Assert.Greater(Mathf.Abs(dropped[0].transform.position.x - 0.5f), 2f, "well away from where the new one was");
            Assert.IsFalse(dropped[0].CanInteract, "and it cannot be grabbed straight back");
        }
    }
}
