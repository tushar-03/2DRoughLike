using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Player;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>
    /// Deterministic combat tests. Everything is ticked manually at 60 Hz with scripted input:
    /// player, combat, defense, enemies and projectiles, in the same order the game runs them.
    /// </summary>
    public class CombatTests
    {
        const float Dt = 1f / 60f;

        sealed class FakeAttacker : IAttacker
        {
            public Team Team => Team.Enemy;
            public Transform Transform => null;
            public bool Parried;
            public float Stun;
            public void OnAttackParried(Vector2 point, float stunDuration) { Parried = true; Stun = stunDuration; }
        }

        MovementConfig _cfg;
        WeaponData _sword, _shield;
        EnemyDefinition _gruntDef;
        PlayerController _pc;
        PlayerCombat _combat;
        PlayerDefense _def;
        Health _health;
        ScriptedIntentSource _in;
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<EnemyController> _enemies = new List<EnemyController>();

        [SetUp]
        public void SetUp()
        {
            PlayerCombat.ChargeAttacks = false;   // these tests press buttons and never let go
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _sword = MakeSword();
            _shield = ScriptableObject.CreateInstance<WeaponData>();
            _shield.kind = WeaponKind.Shield;
            _shield.steps = new AttackStep[0];
            _shield.shield = new ShieldSettings();
            _gruntDef = ScriptableObject.CreateInstance<EnemyDefinition>();
            _gruntDef.archetype = EnemyArchetype.MeleeGrunt;
            _gruntDef.maxHealth = 40f;
            _enemies.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerCombat.ChargeAttacks = true;
            foreach (var p in Object.FindObjectsByType<Projectile>()) Object.DestroyImmediate(p.gameObject);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            _enemies.Clear();
            Object.DestroyImmediate(_cfg);
            Object.DestroyImmediate(_sword);
            Object.DestroyImmediate(_shield);
            Object.DestroyImmediate(_gruntDef);
        }

        static WeaponData MakeSword()
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.displayName = "Test Sword";
            w.kind = WeaponKind.Melee;
            w.comboResetTime = 0.5f;
            AttackStep S(float dmg, float windup, float active, float recovery) => new AttackStep
            {
                damage = dmg, windup = windup, active = active, recovery = recovery, chainFraction = 0.4f,
                hitOffset = new Vector2(1.0f, 0.9f), hitSize = new Vector2(1.9f, 1.3f),
                knockback = new Vector2(5f, 2f), stagger = 10f, lunge = 0f, moveScale = 0.35f, hitStop = 0f, shake = 0f
            };
            w.steps = new[] { S(14f, 0.09f, 0.08f, 0.20f), S(14f, 0.08f, 0.08f, 0.22f), S(24f, 0.14f, 0.10f, 0.38f) };
            return w;
        }

        // ------------------------------------------------------------------ world helpers

        void Floor()
        {
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(0f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(60f, 2f);
            _spawned.Add(go);
        }

        void SpawnPlayer(WeaponData slot1 = null, WeaponData slot2 = null)
        {
            Physics2D.SyncTransforms();
            _pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            _combat = PlayerCombatSetup.Attach(_pc, slot1 ?? _sword, slot2 ?? _shield);
            _def = _pc.GetComponent<PlayerDefense>();
            _health = _pc.GetComponent<Health>();
            _in = _pc.GetComponent<ScriptedIntentSource>();
            _pc.autoTick = false;
            _combat.autoTick = false;
            _def.autoTick = false;
            _spawned.Add(_pc.gameObject);
        }

        EnemyController SpawnGrunt(float x)
        {
            Physics2D.SyncTransforms();
            var e = EnemyFactory.Build(_gruntDef, new Vector2(x, 0f), false);
            e.autoTick = false;
            _enemies.Add(e);
            _spawned.Add(e.gameObject);
            Physics2D.SyncTransforms();
            return e;
        }

        TrainingDummy SpawnDummy(float x)
        {
            Physics2D.SyncTransforms();
            var d = EnemyFactory.BuildDummy(new Vector2(x, 0f));
            _spawned.Add(d.gameObject);
            Physics2D.SyncTransforms();
            return d;
        }

        void Tick(float seconds, System.Action each = null)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                _pc.Tick(Dt);
                _combat.Tick(Dt);
                _def.Tick(Dt);
                foreach (var e in _enemies) if (e != null) e.Tick(Dt);
                foreach (var p in Object.FindObjectsByType<Projectile>()) p.Tick(Dt);
                each?.Invoke();
            }
        }

        DamageInfo EnemyHit(float amount, Vector2 from, IAttacker attacker = null, DamageTags tags = DamageTags.Melee)
            => new DamageInfo
            {
                Amount = amount, SourceTeam = Team.Enemy, Attacker = attacker, SourcePosition = from,
                HitPoint = _pc.transform.position + Vector3.up, Knockback = new Vector2(-6f, 3f), Stagger = 10f, Tags = tags
            };

        // ------------------------------------------------------------------ health

        [Test]
        public void SameTeamHitsAreIgnoredAndLethalHitFiresDied()
        {
            var go = new GameObject("H");
            _spawned.Add(go);
            var h = go.AddComponent<Health>();
            h.Initialize(Team.Enemy, 10f);
            bool died = false;
            h.Died += () => died = true;

            Assert.AreEqual(HitResult.Ignored, h.TakeHit(new DamageInfo { Amount = 5f, SourceTeam = Team.Enemy }));
            Assert.AreEqual(10f, h.Current);

            Assert.AreEqual(HitResult.Hit, h.TakeHit(new DamageInfo { Amount = 12f, SourceTeam = Team.Player }));
            Assert.IsTrue(died);
            Assert.IsTrue(h.IsDead);
            Assert.AreEqual(HitResult.Ignored, h.TakeHit(new DamageInfo { Amount = 1f, SourceTeam = Team.Player }));
        }

        // ------------------------------------------------------------------ player weapons

        [Test]
        public void SwordComboChainsThroughThreeStepsInOrder()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);

            var started = new List<int>();
            _combat.AttackStarted += (w, step, index) => started.Add(index);

            float t = 0f;
            Tick(1.8f, () =>
            {
                t += Dt;
                if (Mathf.Repeat(t, 0.08f) < Dt) _in.PressAttack(0);   // mash
            });

            Assert.GreaterOrEqual(started.Count, 3);
            Assert.AreEqual(0, started[0]);
            Assert.AreEqual(1, started[1]);
            Assert.AreEqual(2, started[2]);
            if (started.Count > 3) Assert.AreEqual(0, started[3], "combo should restart after the finisher");
            Assert.GreaterOrEqual(dummy.TotalDamage, 14f + 14f + 24f - 0.01f);
        }

        [Test]
        public void SingleSwingHitsEachTargetOnce()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            Tick(0.6f);
            Assert.AreEqual(1, dummy.Hits);
            Assert.AreEqual(14f, dummy.TotalDamage, 0.01f);
        }

        [Test]
        public void SwingOutOfRangeHitsNothing()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(6f);
            Tick(0.3f);
            _in.PressAttack(0);
            Tick(0.6f);
            Assert.AreEqual(0, dummy.Hits);
        }

        [Test]
        public void RollCancelsAttackRecovery()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            int guard = 0;
            while (_combat.Phase != AttackPhase.Recovery && guard++ < 120) Tick(Dt);
            Assert.AreEqual(AttackPhase.Recovery, _combat.Phase);

            _in.PressRoll();
            Tick(Dt * 2);
            Assert.AreEqual(PlayerState.Rolling, _pc.State);
            Assert.AreEqual(AttackPhase.None, _combat.Phase);
        }

        // ------------------------------------------------------------------ shield

        [Test]
        public void ParryInsideWindowNegatesDamageAndStunsAttacker()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(1);   // slot 2 is the shield
            Tick(Dt * 3);
            Assert.AreEqual(ShieldPhase.Parry, _combat.Shield);

            var attacker = new FakeAttacker();
            HitResult r = _health.TakeHit(EnemyHit(20f, new Vector2(2f, 0f), attacker));

            Assert.AreEqual(HitResult.Parried, r);
            Assert.AreEqual(_health.Max, _health.Current);
            Assert.IsTrue(attacker.Parried);
            Assert.AreEqual(_shield.shield.parryStun, attacker.Stun, 0.001f);
            Assert.Greater(_def.RemainingInvulnerability, 0f, "perfect parry grants brief invulnerability");
        }

        [Test]
        public void BlockAfterParryWindowReducesDamage()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(1);
            Tick(_shield.shield.parryWindow + 0.1f);
            Assert.AreEqual(ShieldPhase.Block, _combat.Shield);

            HitResult r = _health.TakeHit(EnemyHit(20f, new Vector2(2f, 0f), new FakeAttacker()));
            Assert.AreEqual(HitResult.Blocked, r);
            float expected = 20f * (1f - _shield.shield.blockDamageReduction);
            Assert.AreEqual(_health.Max - expected, _health.Current, 0.01f);
        }

        [Test]
        public void ShieldDoesNotCoverHitsFromBehindOrUnblockableAttacks()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(1);
            Tick(Dt * 3);

            Assert.AreEqual(HitResult.Hit, _health.TakeHit(EnemyHit(10f, new Vector2(-2f, 0f), new FakeAttacker())), "from behind");
            Assert.AreEqual(90f, _health.Current, 0.01f);

            _def.Tick(5f);   // clear post-hit invulnerability
            Assert.AreEqual(HitResult.Hit, _health.TakeHit(EnemyHit(10f, new Vector2(2f, 0f), new FakeAttacker(), DamageTags.Melee | DamageTags.Unblockable)));
        }

        [Test]
        public void ShieldIsLoweredWhenButtonReleased()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(1);
            Tick(0.3f);
            _in.ReleaseAttack(1);
            Tick(_shield.shield.lowerTime + 0.1f);
            Assert.AreEqual(ShieldPhase.None, _combat.Shield);
        }

        // ------------------------------------------------------------------ defense

        [Test]
        public void RollIFramesIgnoreDamageAndPostHitInvulnerabilityProtectsNextHit()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);

            _in.PressRoll();
            Tick(0.2f);
            Assert.AreEqual(PlayerState.Rolling, _pc.State);
            Assert.IsTrue(_pc.IsInvulnerable);
            Assert.AreEqual(HitResult.Ignored, _health.TakeHit(EnemyHit(10f, new Vector2(2f, 0f))));
            Assert.AreEqual(_health.Max, _health.Current);

            Tick(1f);   // roll over
            Assert.AreEqual(HitResult.Hit, _health.TakeHit(EnemyHit(10f, new Vector2(2f, 0f))));
            Assert.AreEqual(HitResult.Ignored, _health.TakeHit(EnemyHit(10f, new Vector2(2f, 0f))), "post-hit i-frames");
            Assert.AreEqual(90f, _health.Current, 0.01f);
        }

        [Test]
        public void TakingAHitKnocksPlayerBackAndInterruptsAttack()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);
            _in.PressAttack(0);
            Tick(Dt * 2);
            Assert.AreNotEqual(AttackPhase.None, _combat.Phase);

            _health.TakeHit(EnemyHit(10f, new Vector2(2f, 0f)));
            Tick(Dt);
            Assert.AreEqual(AttackPhase.None, _combat.Phase);
            Assert.IsTrue(_pc.IsControlLocked);
            Assert.Less(_pc.Velocity.x, 0f, "knocked away from the attacker");
        }

        // ------------------------------------------------------------------ enemies

        [Test]
        public void MeleeGruntTelegraphsBeforeItsHitLands()
        {
            Floor();
            SpawnPlayer();
            var grunt = SpawnGrunt(-2.0f);   // left of the player, facing right: the player is in its view
            Tick(Dt);

            float telegraphStart = -1f, hitTime = -1f, clock = 0f, hpAtFirstHit = -1f;
            var seen = new List<EnemyState>();
            Tick(3f, () =>
            {
                clock += Dt;
                if (seen.Count == 0 || seen[seen.Count - 1] != grunt.State) seen.Add(grunt.State);
                if (telegraphStart < 0f && grunt.State == EnemyState.Telegraph) telegraphStart = clock;
                if (hitTime < 0f && _health.Current < _health.Max) { hitTime = clock; hpAtFirstHit = _health.Current; }
            });

            Assert.GreaterOrEqual(telegraphStart, 0f, "grunt never telegraphed");
            Assert.GreaterOrEqual(hitTime, 0f, "grunt never landed its attack");
            Assert.GreaterOrEqual(hitTime - telegraphStart, _gruntDef.telegraphTime - Dt * 2f, "damage landed before the telegraph finished");
            Assert.AreEqual(_health.Max - _gruntDef.damage, hpAtFirstHit, 0.01f);
            int tel = seen.IndexOf(EnemyState.Telegraph), atk = seen.IndexOf(EnemyState.Attack);
            Assert.Less(tel, atk, "state order: telegraph before attack");
        }

        [Test]
        public void ParryingTheGruntsRealSwingStaggersItAndPunishesWithBonusDamage()
        {
            Floor();
            SpawnPlayer();
            var grunt = SpawnGrunt(1.5f);
            Tick(Dt);

            int guard = 0;
            while (grunt.State != EnemyState.Telegraph && guard++ < 300) Tick(Dt);
            Assert.AreEqual(EnemyState.Telegraph, grunt.State);

            // raise the shield shortly before the swing lands so the parry window covers it
            while (grunt.StateTime < _gruntDef.telegraphTime - 0.07f) Tick(Dt);
            _in.PressAttack(1);
            Tick(0.3f);

            Assert.AreEqual(_health.Max, _health.Current, 0.01f, "parry should negate the hit");
            Assert.AreEqual(EnemyState.Stagger, grunt.State);

            // stunned enemies take bonus damage
            var hp = grunt.GetComponent<Health>();
            float before = hp.Current;
            hp.TakeHit(new DamageInfo { Amount = 10f, SourceTeam = Team.Player });
            Assert.AreEqual(before - 10f * _gruntDef.stunnedDamageMultiplier, hp.Current, 0.01f);
        }

        [Test]
        public void AccumulatedPoiseDamageStaggersAnEnemy()
        {
            Floor();
            SpawnPlayer();
            var grunt = SpawnGrunt(8f);
            Tick(Dt);
            var hp = grunt.GetComponent<Health>();

            hp.TakeHit(new DamageInfo { Amount = 1f, SourceTeam = Team.Player, Stagger = 10f, Knockback = new Vector2(4f, 0f) });
            Tick(Dt);
            Assert.AreNotEqual(EnemyState.Stagger, grunt.State);
            hp.TakeHit(new DamageInfo { Amount = 1f, SourceTeam = Team.Player, Stagger = 10f });
            Tick(Dt);
            Assert.AreEqual(EnemyState.Stagger, grunt.State);
        }

        [Test]
        public void ShooterProjectileHurtsPlayerAndParryReflectsIt()
        {
            Floor();
            SpawnPlayer();
            Tick(0.3f);

            // 1. unshielded: projectile damages the player
            var p = Projectile.Spawn(new Vector2(3f, 0.9f), new Vector2(-10f, 0f), 10f, Team.Enemy);
            p.autoTick = false;
            for (int i = 0; i < 40; i++) p.Tick(Dt);
            Assert.AreEqual(90f, _health.Current, 0.01f);

            // 2. parry window open: reflected, no damage
            _def.Tick(5f);
            Tick(0.4f);   // let the hit-reaction control lock expire
            _in.PressAttack(1);
            Tick(Dt * 3);
            Assert.AreEqual(ShieldPhase.Parry, _combat.Shield);
            var q = Projectile.Spawn(new Vector2(2f, 0.9f), new Vector2(-10f, 0f), 10f, Team.Enemy);
            q.autoTick = false;
            for (int i = 0; i < 20; i++) q.Tick(Dt);
            Assert.AreEqual(90f, _health.Current, 0.01f, "parried projectile must not damage");
            Assert.AreEqual(Team.Player, q.Team);
            Assert.Greater(q.Velocity.x, 0f, "reflected away from the player");
        }

        [Test]
        public void ChasingEnemyDoesNotWalkOffLedge()
        {
            // floor ends at x = 3: an enemy chasing a player standing beyond the edge must stop at the lip
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(-12f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(30f, 2f);   // x from -27 to 3
            _spawned.Add(go);
            SpawnPlayer();
            _pc.ResetTo(new Vector2(8f, 8f));      // hovering far away over the gap, in sight
            var grunt = SpawnGrunt(0f);
            Tick(3f, () => _pc.transform.position = new Vector3(8f, 1.5f, 0f));
            Assert.Less(grunt.transform.position.x, 3f);
            Assert.GreaterOrEqual(grunt.transform.position.y, -0.1f, "enemy fell off the ledge");
        }
        // ------------------------------------------------------------------ hold to charge and auto aim

        [Test]
        public void HoldingAttackChargesAndReleasingHitsHarder()
        {
            PlayerCombat.ChargeAttacks = true;
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            _in.PressAttack(0);                    // pressed and held
            Tick(1.0f);
            Assert.IsTrue(_combat.Charging, "the blow is drawn back and held");
            Assert.Greater(_combat.ChargeLevel, 0.5f);
            Assert.AreEqual(0, dummy.Hits, "nothing has landed yet");

            _in.ReleaseAttack(0);
            Tick(0.6f);
            Assert.AreEqual(1, dummy.Hits);
            Assert.Greater(dummy.TotalDamage, 14f * 1.5f, "a charged hit does much more than the base 14");
        }

        [Test]
        public void TappingAttackIsInstantAndNotCharged()
        {
            PlayerCombat.ChargeAttacks = true;
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            Tick(0.6f);
            Assert.AreEqual(1, dummy.Hits);
            Assert.AreEqual(14f, dummy.TotalDamage, 0.01f, "a quick tap is a normal hit");
        }

        [Test]
        public void ANeverReleasedChargeThrowsItselfAtFullPower()
        {
            PlayerCombat.ChargeAttacks = true;
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            _in.PressAttack(0);
            Tick(2.2f);
            Assert.AreEqual(1, dummy.Hits);
            Assert.AreEqual(28f, dummy.TotalDamage, 0.5f, "a full charge doubles the damage");
        }

        [Test]
        public void MeleeSwingsTurnTowardTheNearestEnemy()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(-1.6f);         // behind a player who faces right
            Tick(0.3f);
            Assert.AreEqual(1, _pc.Facing);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            Tick(0.6f);
            Assert.AreEqual(1, dummy.Hits, "the swing turned around instead of cutting empty air");
        }

        [Test]
        public void ArrowsAutoAimAtTheNearestEnemyEvenWhenItIsAbove()
        {
            Floor();
            var bow = ScriptableObject.CreateInstance<WeaponData>();
            bow.kind = WeaponKind.Bow;
            bow.range = 18f;
            bow.projectileSpeed = 28f;
            bow.steps = new[] { new AttackStep { damage = 10f, windup = 0.05f, active = 0.02f, recovery = 0.1f, knockback = Vector2.zero, stagger = 0f, hitStop = 0f, shake = 0f } };
            SpawnPlayer(bow, null);
            var high = SpawnDummy(9f);
            high.transform.position = new Vector3(9f, 3.5f, 0f);       // well above where a straight arrow would fly
            Physics2D.SyncTransforms();
            Tick(0.3f);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            Tick(1.2f);
            Assert.AreEqual(1, high.Hits, "the arrow bent toward its target");
            Object.DestroyImmediate(bow);
        }

        [Test]
        public void ABowSeesFartherThanAMeleeWeaponAndStopsBeyondItsRange()
        {
            Floor();
            var bow = ScriptableObject.CreateInstance<WeaponData>();
            bow.kind = WeaponKind.Bow;
            bow.range = 12f;
            bow.projectileSpeed = 28f;
            bow.steps = new[] { new AttackStep { damage = 10f, windup = 0.05f, active = 0.02f, recovery = 0.1f, knockback = Vector2.zero, stagger = 0f, hitStop = 0f, shake = 0f } };
            SpawnPlayer(bow, null);
            var far = SpawnDummy(25f);
            Tick(0.3f);
            _in.PressAttack(0);
            _in.ReleaseAttack(0);
            Tick(1.6f);
            Assert.AreEqual(0, far.Hits, "beyond the bow's range the arrow runs out");
            Object.DestroyImmediate(bow);
        }
    }
}
