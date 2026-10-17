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
    /// <summary>One scenario per enemy archetype: the behaviour that defines it, with a stationary player as the target.</summary>
    public class EnemyTests
    {
        const float Dt = 1f / 60f;

        MovementConfig _cfg;
        PlayerController _pc;
        Health _health;
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Object> _owned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _owned.Add(_cfg);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<Projectile>()) Object.DestroyImmediate(p.gameObject);
            foreach (var e in Object.FindObjectsByType<EnemyController>()) if (e != null) Object.DestroyImmediate(e.gameObject);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        // ------------------------------------------------------------------ helpers

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        void Floor()
        {
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(0f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(80f, 2f);
            _spawned.Add(go);
        }

        void Wall(float x)
        {
            var go = new GameObject("Wall");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(x, 4f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 10f);
            _spawned.Add(go);
        }

        void SpawnPlayer()
        {
            Physics2D.SyncTransforms();
            _pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            _pc.autoTick = false;
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            weapon.kind = WeaponKind.Shield;
            weapon.steps = new AttackStep[0];
            weapon.shield = new ShieldSettings();
            var combat = PlayerCombatSetup.Attach(_pc, weapon, weapon);
            combat.autoTick = false;
            _pc.GetComponent<PlayerDefense>().autoTick = false;
            _health = _pc.GetComponent<Health>();
            _spawned.Add(_pc.gameObject);
        }

        EnemyController Spawn(EnemyDefinition def, float x, float y = 0f)
        {
            Physics2D.SyncTransforms();
            def.rearAwareness = 40f;   // the player stands behind enemies that face right by default
            var e = EnemyFactory.Build(Own(def), new Vector2(x, y), false);
            e.autoTick = false;
            Physics2D.SyncTransforms();
            return e;
        }

        /// <summary>Ticks the player, every enemy (minions included) and every projectile. Stops early when <paramref name="until"/> is true.</summary>
        bool Run(float seconds, System.Func<bool> until = null)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                _pc.Tick(Dt);
                foreach (var e in Object.FindObjectsByType<EnemyController>()) if (e != null) e.Tick(Dt);
                foreach (var p in Object.FindObjectsByType<Projectile>()) p.Tick(Dt);
                if (until != null && until()) return true;
            }
            return until == null;
        }

        static DamageInfo PlayerHit(float amount, float fromX, float stagger = 5f)
            => new DamageInfo { Amount = amount, SourceTeam = Team.Player, SourcePosition = new Vector2(fromX, 1f), Stagger = stagger, Tags = DamageTags.Melee };

        // ------------------------------------------------------------------ flyer

        [Test]
        public void FlyerHoversAboveThePlayerAndDivesIntoThem()
        {
            Floor();
            SpawnPlayer();
            var wisp = Spawn(DefaultEnemies.BellWisp(), 6f, 3f);

            Run(1.2f);
            Assert.Greater(wisp.Position.y, 1.5f, "it hovers instead of falling");
            Assert.IsTrue(wisp.Behavior.Flying);

            bool dived = false;
            Run(6f, () => { dived |= wisp.State == EnemyState.Attack; return _health.Current < _health.Max; });
            Assert.IsTrue(dived, "it telegraphed and dived");
            Assert.Less(_health.Current, _health.Max, "a stationary player gets hit by the dive");
        }

        // ------------------------------------------------------------------ shield bearer

        [Test]
        public void ShieldBlocksFrontHitsAndBackstabsHurtMore()
        {
            Floor();
            var def = DefaultEnemies.ShellGuard();
            var guard = Spawn(def, 0f);

            float before = guard.Health.Current;
            var r = guard.Health.TakeHit(PlayerHit(20f, 3f));      // it faces right: that is the front
            Assert.AreEqual(HitResult.Blocked, r);
            Assert.AreEqual(20f * (1f - def.shieldReduction), before - guard.Health.Current, 0.01f);

            before = guard.Health.Current;
            r = guard.Health.TakeHit(PlayerHit(20f, -3f));         // from behind
            Assert.AreEqual(HitResult.Hit, r);
            Assert.AreEqual(20f * def.backstabMultiplier, before - guard.Health.Current, 0.01f);
        }

        [Test]
        public void HeavyBlowsAndParriesBreakTheShield()
        {
            Floor();
            var def = DefaultEnemies.ShellGuard();
            var guard = Spawn(def, 0f);
            var shield = (ShieldBehavior)guard.Behavior;

            Assert.IsTrue(shield.ShieldUp);
            guard.Health.TakeHit(PlayerHit(10f, 3f, def.shieldBreakStagger + 5f));
            Assert.IsTrue(shield.ShieldBroken, "a heavy blow breaks it");
            Assert.IsFalse(shield.ShieldUp);

            // while broken, a front hit lands for full damage once the shield flinch is over
            SpawnPlayer();
            Run(1f);
            float before = guard.Health.Current;
            guard.Health.TakeHit(PlayerHit(10f, 3f, 1f));
            Assert.AreEqual(10f, before - guard.Health.Current, 0.01f);

            var guard2 = Spawn(DefaultEnemies.ShellGuard(), 30f);
            ((IAttacker)guard2).OnAttackParried(Vector2.zero, 1.2f);
            Assert.IsTrue(((ShieldBehavior)guard2.Behavior).ShieldBroken, "a parry shatters it");
        }

        // ------------------------------------------------------------------ charger

        [Test]
        public void ChargerRunsDownThePlayerAndIsStunnedByAWall()
        {
            Floor();
            Wall(-5f);
            SpawnPlayer();
            var boar = Spawn(DefaultEnemies.Tideboar(), 9f);

            bool charged = false;
            Run(5f, () => { charged |= boar.State == EnemyState.Attack; return _health.Current < _health.Max; });
            Assert.IsTrue(charged);
            Assert.Less(_health.Current, _health.Max, "the charge hit the player");

            bool stunned = Run(4f, () => boar.State == EnemyState.Stagger);
            Assert.IsTrue(stunned, "it ran into the wall");
            Assert.AreEqual(boar.Definition.wallStunTime, boar.StaggerDuration, 0.001f);
        }

        // ------------------------------------------------------------------ summoner

        [Test]
        public void SummonerCallsMinionsUpToItsCapAndTheyDieWithIt()
        {
            Floor();
            SpawnPlayer();
            var wretch = DefaultEnemies.DrownedWretch();
            var shade = Spawn(DefaultEnemies.ReliquaryShade(Own(wretch)), 8f);
            var summoner = (SummonerBehavior)shade.Behavior;

            int spawnedEvents = 0;
            System.Action<MinionSpawnedEvent> onSpawn = e => spawnedEvents++;
            EventBus<MinionSpawnedEvent>.Subscribe(onSpawn);
            try
            {
                Run(8f, () => summoner.LiveMinions >= 2);
                Assert.AreEqual(2, summoner.LiveMinions);
                Assert.AreEqual(2, spawnedEvents);
                Assert.LessOrEqual(summoner.LiveMinions, shade.Definition.maxMinions);

                var minions = new List<EnemyController>(summoner.Minions);
                shade.Health.TakeHit(PlayerHit(1000f, 7f));
                Assert.IsTrue(shade.Health.IsDead);
                foreach (var m in minions) Assert.IsTrue(m.Health.IsDead, "minions fall with the summoner");
            }
            finally { EventBus<MinionSpawnedEvent>.Unsubscribe(onSpawn); }
        }

        [Test]
        public void MinionsCountTowardsTheRoomBeingClear()
        {
            Floor();
            SpawnPlayer();
            var shade = Spawn(DefaultEnemies.ReliquaryShade(Own(DefaultEnemies.DrownedWretch())), 8f);

            var tracker = new GameObject("Room").AddComponent<RoomClearTracker>();
            _spawned.Add(tracker.gameObject);
            tracker.Register(shade.Health);
            Assert.AreEqual(1, tracker.Alive);

            var summoner = (SummonerBehavior)shade.Behavior;
            Run(8f, () => summoner.LiveMinions >= 2);
            Assert.AreEqual(1 + summoner.LiveMinions, tracker.Alive, "summoned minions must be killed before the room clears");
        }

        // ------------------------------------------------------------------ exploder

        [Test]
        public void ExploderBurstsNextToThePlayer()
        {
            Floor();
            SpawnPlayer();
            var bloat = Spawn(DefaultEnemies.Bloat(), 4f);

            bool dead = Run(5f, () => bloat.Health.IsDead);
            Assert.IsTrue(dead, "it burst");
            Assert.Less(_health.Current, _health.Max, "and the player was in range");
        }

        [Test]
        public void ExploderAlsoBurstsWhenKilledButNotFarAway()
        {
            Floor();
            SpawnPlayer();
            var near = Spawn(DefaultEnemies.Bloat(), 1.2f);
            near.Health.TakeHit(PlayerHit(1000f, 0f));
            Assert.Less(_health.Current, _health.Max, "popping it next to you hurts");

            _health.ResetToFull();
            var far = Spawn(DefaultEnemies.Bloat(), 20f);
            far.Health.TakeHit(PlayerHit(1000f, 0f));
            Assert.AreEqual(_health.Max, _health.Current, "popping it from range is safe");
        }

        // ------------------------------------------------------------------ sniper

        [Test]
        public void SniperLocksItsAimThenFiresAFastShot()
        {
            Floor();
            SpawnPlayer();
            var eel = Spawn(DefaultEnemies.EelArcher(), 12f);

            bool telegraphed = Run(6f, () => eel.State == EnemyState.Telegraph);
            Assert.IsTrue(telegraphed, "the laser line appears first");

            bool fired = Run(3f, () => Object.FindObjectsByType<Projectile>().Length > 0);
            Assert.IsTrue(fired);
            var shot = Object.FindObjectsByType<Projectile>()[0];
            Assert.Greater(shot.Velocity.magnitude, 20f, "a sniper shot is fast");

            Run(2f, () => _health.Current < _health.Max);
            Assert.Less(_health.Current, _health.Max, "a stationary player gets hit");
        }

        // ------------------------------------------------------------------ spawn rules

        [Test]
        public void LevelsRespectMinProgressAndPerRoomCaps()
        {
            var early = Own(DefaultEnemies.DrownedWretch());
            early.displayName = "Early"; early.rewardMultiplier = 1f; early.maxPerRoom = 2;
            var late = Own(DefaultEnemies.Bloat());
            late.displayName = "Late"; late.minProgress = 0.9f; late.maxPerRoom = 1;

            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            biome.enemyPool = new[] { early, late };

            for (uint seed = 0; seed < 8; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, biome, true);
                _spawned.Add(level.Root);

                foreach (var chunkPair in level.Chunks)
                {
                    int lates = 0, earlies = 0;
                    foreach (var e in chunkPair.Value.GetComponentsInChildren<EnemyController>(true))
                    {
                        if (e.Definition == late) lates++; else earlies++;
                    }
                    var cell = layout.Cells.Find(c => c.Pos == chunkPair.Key);
                    if (cell.Progress < 0.9f) Assert.AreEqual(0, lates, $"seed {seed}: late enemy too early");
                    Assert.LessOrEqual(lates, 1, $"seed {seed}: per-room cap");
                    Assert.LessOrEqual(earlies, 2, $"seed {seed}: per-room cap");
                }
                Object.DestroyImmediate(level.Root);
            }
        }
    }
}
