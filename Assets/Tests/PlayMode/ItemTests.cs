using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>
    /// Integration tests for the item layer: stats and scaling, status effects, skills, pickups, shops, altars,
    /// mutations, flask and drops. Everything is ticked manually and deterministically.
    /// </summary>
    public class ItemTests
    {
        const float Dt = 1f / 60f;

        sealed class FakeAttacker : IAttacker
        {
            public Team Team => Team.Enemy;
            public Transform Transform => null;
            public void OnAttackParried(Vector2 point, float stunDuration) { }
        }

        LootDatabase _db;
        MovementConfig _cfg;
        PlayerController _pc;
        PlayerCombat _combat;
        PlayerDefense _def;
        Health _health;
        PlayerInventory _inv;
        PlayerStats _stats;
        SkillRunner _skills;
        InteractionSensor _sensor;
        FlaskUser _flask;
        MutationRunner _mutations;
        ScriptedIntentSource _in;
        EnemyDefinition _gruntDef;
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<EnemyController> _enemies = new List<EnemyController>();

        [SetUp]
        public void SetUp()
        {
            _db = DefaultContent.CreateDatabase();
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _gruntDef = ScriptableObject.CreateInstance<EnemyDefinition>();
            _gruntDef.maxHealth = 40f;
            _enemies.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in UnityEngine.Object.FindObjectsByType<Collectible>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in UnityEngine.Object.FindObjectsByType<WorldItem>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in UnityEngine.Object.FindObjectsByType<Projectile>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in UnityEngine.Object.FindObjectsByType<GrenadeProjectile>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in UnityEngine.Object.FindObjectsByType<TurretEntity>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in UnityEngine.Object.FindObjectsByType<TrapEntity>()) UnityEngine.Object.DestroyImmediate(go.gameObject);
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            _enemies.Clear();
            UnityEngine.Object.DestroyImmediate(_cfg);
            UnityEngine.Object.DestroyImmediate(_gruntDef);
        }

        // ------------------------------------------------------------------ world helpers

        void Floor()
        {
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(0f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(80f, 2f);
            _spawned.Add(go);
        }

        void SpawnPlayer()
        {
            Physics2D.SyncTransforms();
            _pc = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            _combat = PlayerCombatSetup.Attach(_pc, null, null);
            _inv = PlayerItemsSetup.Attach(_pc);
            _def = _pc.GetComponent<PlayerDefense>();
            _health = _pc.GetComponent<Health>();
            _stats = _pc.GetComponent<PlayerStats>();
            _skills = _pc.GetComponent<SkillRunner>();
            _sensor = _pc.GetComponent<InteractionSensor>();
            _flask = _pc.GetComponent<FlaskUser>();
            _mutations = _pc.GetComponent<MutationRunner>();
            _in = _pc.GetComponent<ScriptedIntentSource>();

            _pc.autoTick = false;
            _combat.autoTick = false;
            _def.autoTick = false;
            PlayerItemsSetup.DisableAutoTick(_pc.gameObject);
            _spawned.Add(_pc.gameObject);

            _inv.ResetForRun(_db);
            _stats.Sheet.SetBase(StatKind.CritChance, 0f);   // no random crits unless a test wants them
        }

        EnemyController SpawnGrunt(float x)
        {
            Physics2D.SyncTransforms();
            var e = EnemyFactory.Build(_gruntDef, new Vector2(x, 0f), false);
            e.autoTick = false;
            e.GetComponent<StatusEffects>().autoTick = false;
            _enemies.Add(e);
            _spawned.Add(e.gameObject);
            Physics2D.SyncTransforms();
            return e;
        }

        TrainingDummy SpawnDummy(float x)
        {
            Physics2D.SyncTransforms();
            var d = EnemyFactory.BuildDummy(new Vector2(x, 0f));
            d.GetComponent<StatusEffects>().autoTick = false;
            _spawned.Add(d.gameObject);
            Physics2D.SyncTransforms();
            return d;
        }

        void Tick(float seconds, Action each = null)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                _pc.Tick(Dt);
                _combat.Tick(Dt);
                _def.Tick(Dt);
                _skills.Tick(Dt);
                _sensor.Tick();
                _flask.Tick();
                _mutations.Tick(Dt);
                foreach (var e in _enemies) if (e != null) e.Tick(Dt);
                foreach (var s in UnityEngine.Object.FindObjectsByType<StatusEffects>()) s.Tick(Dt);
                foreach (var p in UnityEngine.Object.FindObjectsByType<Projectile>()) p.Tick(Dt);
                foreach (var g in UnityEngine.Object.FindObjectsByType<GrenadeProjectile>()) g.Tick(Dt);
                foreach (var t in UnityEngine.Object.FindObjectsByType<TurretEntity>()) t.Tick(Dt);
                foreach (var t in UnityEngine.Object.FindObjectsByType<TrapEntity>()) t.Tick(Dt);
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collectible>()) c.Tick(Dt);
                each?.Invoke();
            }
        }

        MutationData Mutation(string name) => Array.Find(_db.mutations, m => m.name == name);
        GearData Amulet(string name) => Array.Find(_db.amulets, g => g.name == name);
        AffixData Affix(string id) => Array.Find(_db.affixes, a => a.id == id);
        SkillData SkillAsset(string name) => Array.Find(_db.skills, s => s.name == name);
        WeaponData WeaponAsset(string name) => Array.Find(_db.weapons, w => w.name == name);

        DamageInfo EnemyHit(float amount, Vector2 from, IAttacker attacker = null)
            => new DamageInfo { Amount = amount, SourceTeam = Team.Enemy, Attacker = attacker, SourcePosition = from, HitPoint = _pc.transform.position + Vector3.up, Knockback = Vector2.zero, Tags = DamageTags.Melee };

        void Swing(int slot = 0)
        {
            _in.PressAttack(slot);
            _in.ReleaseAttack(slot);
            Tick(0.7f);
        }

        // ------------------------------------------------------------------ loadout and stats

        [Test]
        public void RunStartsWithTheStartingLoadoutAndOutfitHealth()
        {
            Floor();
            SpawnPlayer();
            Assert.AreEqual(_db.startingWeapon1, _inv.Weapons[0].Weapon);
            Assert.AreEqual(_db.startingWeapon2, _inv.Weapons[1].Weapon);
            Assert.AreEqual(_db.startingWeapon1, _combat.Slot1);
            Assert.AreEqual(_db.startingWeapon2, _combat.Slot2);
            Assert.AreEqual(110f, _health.Max, 0.01f, "Apprentice Robes add 10 health");
            Assert.AreEqual(1f, _pc.SpeedBonus, 0.001f);
        }

        [Test]
        public void ScrollsAddHealthAndScaleOnlyMatchingWeapons()
        {
            Floor();
            SpawnPlayer();
            for (int i = 0; i < 3; i++) _inv.AddScroll(StatColor.Ember);

            Assert.AreEqual(110f + 3 * PlayerStats.HealthPerScroll, _health.Max, 0.01f);
            Assert.AreEqual(1f + 3 * PlayerStats.DamagePerScrollLevel, _stats.GetWeaponMods(0).DamageMultiplier, 0.001f, "Ember sword scales with Ember scrolls");
            Assert.AreEqual(1f, _stats.GetWeaponMods(1).DamageMultiplier, 0.001f, "Moss shield does not");

            _inv.AddScroll(StatColor.Moss);
            Assert.AreEqual(1f + PlayerStats.DamagePerScrollLevel, _stats.GetWeaponMods(1).DamageMultiplier, 0.001f);

            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            Swing();
            Assert.AreEqual(14f * (1f + 3 * PlayerStats.DamagePerScrollLevel), dummy.LastHit, 0.05f, "damage dealt reflects the scroll scaling");
        }

        [Test]
        public void MultiColourWeaponsScaleWithEachColour()
        {
            Floor();
            SpawnPlayer();
            _inv.EquipWeapon(0, ItemInstance.ForWeapon(WeaponAsset("Boathook")));   // Brine + Moss
            _inv.AddScroll(StatColor.Brine);
            _inv.AddScroll(StatColor.Moss);
            _inv.AddScroll(StatColor.Ember);
            Assert.AreEqual(1f + 2 * PlayerStats.DamagePerScrollLevel, _stats.GetWeaponMods(0).DamageMultiplier, 0.001f);
        }

        [Test]
        public void GearChangesSpeedAttackSpeedAndDamageTaken()
        {
            Floor();
            SpawnPlayer();
            _inv.PickUp(ItemInstance.ForGear(Amulet("LanternCharm")), Vector2.zero);
            Assert.AreEqual(1.10f, _pc.SpeedBonus, 0.001f);
            Assert.AreEqual(1.05f, _stats.AttackSpeed, 0.001f);

            _inv.PickUp(ItemInstance.ForGear(Amulet("CrackedReliquary")), new Vector2(3f, 0f));
            Assert.AreEqual(1f, _pc.SpeedBonus, 0.001f, "the old amulet is replaced, its bonus is gone");
            Assert.AreEqual(135f, _health.Max, 0.01f);
            Assert.AreEqual(1.10f, _stats.DamageTakenMultiplier, 0.001f);

            _def.Tick(5f);
            _health.TakeHit(EnemyHit(10f, new Vector2(5f, 0f)));
            Assert.AreEqual(135f - 11f, _health.Current, 0.01f, "Reliquary: 10% more damage taken");

            bool oldAmuletDropped = false;
            foreach (var w in UnityEngine.Object.FindObjectsByType<WorldItem>()) if (w.Item.BaseName == "Lantern Charm") oldAmuletDropped = true;
            Assert.IsTrue(oldAmuletDropped, "replaced gear drops on the floor");
        }

        [Test]
        public void WeaponAffixesGiveCritAndOnHitStatus()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);

            var sword = ItemInstance.ForWeapon(_db.startingWeapon1);
            sword.Affixes.Add(new AffixInstance(Affix("w_crit"), 1f));
            sword.Affixes.Add(new AffixInstance(Affix("w_burn"), 1f));
            _inv.EquipWeapon(0, sword);

            Swing();
            Assert.AreEqual(14f * 1.5f, dummy.LastHit, 0.05f, "guaranteed crit deals 1.5x");
            Assert.IsTrue(dummy.GetComponent<StatusEffects>().Has(StatusType.Burn), "100% burn chance");
        }

        [Test]
        public void RarityAndTierRaiseWeaponDamage()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            Tick(0.3f);
            _inv.EquipWeapon(0, ItemInstance.ForWeapon(_db.startingWeapon1, ItemRarity.Epic, 2));
            Swing();
            Assert.AreEqual(14f * 1.45f * 1.18f, dummy.LastHit, 0.1f);
        }

        // ------------------------------------------------------------------ status effects

        (Health health, StatusEffects status) MakeTarget()
        {
            var go = new GameObject("Target");
            go.SetActive(false);
            var h = go.AddComponent<Health>();
            h.Initialize(Team.Enemy, 100f);
            var s = go.AddComponent<StatusEffects>();
            s.autoTick = false;
            go.SetActive(true);
            _spawned.Add(go);
            return (h, s);
        }

        [Test]
        public void DamageOverTimeStatusesTickAndStackByRefreshing()
        {
            var (h, s) = MakeTarget();
            s.Apply(StatusType.Burn, 2f);
            for (int i = 0; i < 130; i++) s.Tick(Dt);
            Assert.AreEqual(10f, 100f - h.Current, 1.5f, "burn does about 5 damage per second");
            Assert.IsFalse(s.Has(StatusType.Burn), "expired");

            var (h2, s2) = MakeTarget();
            s2.Apply(StatusType.Poison, 3f);
            for (int i = 0; i < 60; i++) s2.Tick(Dt);
            s2.Apply(StatusType.Poison, 3f);   // refresh
            Assert.AreEqual(3f, s2.Remaining(StatusType.Poison), 0.05f);
            Assert.AreEqual(1, s2.ActiveCount);
        }

        [Test]
        public void SlowStunAndWeakenModifyTheTarget()
        {
            var (h, s) = MakeTarget();
            Assert.AreEqual(1f, s.SpeedMultiplier);
            s.Apply(StatusType.Slow, 2f);
            Assert.Less(s.SpeedMultiplier, 0.6f);

            s.Apply(StatusType.Stun, 1f);
            Assert.IsTrue(s.IsStunned);
            for (int i = 0; i < 70; i++) s.Tick(Dt);
            Assert.IsFalse(s.IsStunned);

            s.Apply(StatusType.Weaken, 5f);
            h.TakeHit(new DamageInfo { Amount = 10f, SourceTeam = Team.Player });
            Assert.AreEqual(100f - 12.5f, h.Current, 0.01f, "weakened targets take 25% more");
        }

        [Test]
        public void StatusDamageCreditsKillsAndBypassesInvulnerability()
        {
            Floor();
            SpawnPlayer();
            var grunt = SpawnGrunt(10f);
            int kills = 0;
            Action<EnemyDiedEvent> onDied = e => kills++;
            EventBus<EnemyDiedEvent>.Subscribe(onDied);
            try
            {
                grunt.GetComponent<Health>().TakeHit(new DamageInfo { Amount = 35f, SourceTeam = Team.Player });
                grunt.GetComponent<StatusEffects>().Apply(StatusType.Burn, 5f, 2f, Team.Player);
                Tick(3f);
                Assert.IsTrue(grunt.GetComponent<Health>().IsDead, "burn finished it off");
                Assert.AreEqual(1, kills);
            }
            finally { EventBus<EnemyDiedEvent>.Unsubscribe(onDied); }

            // the player's own burning ignores i-frames and shields
            _def.GrantInvulnerability(5f);
            float before = _health.Current;
            _pc.GetComponent<StatusEffects>().Apply(StatusType.Burn, 2f, 1f, Team.Enemy);
            Tick(1.2f);
            Assert.Less(_health.Current, before);
        }

        [Test]
        public void StunnedEnemiesStaggerAndSlowedEnemiesMoveSlower()
        {
            Floor();
            SpawnPlayer();
            var grunt = SpawnGrunt(6f);
            Tick(0.5f);
            Assert.AreNotEqual(EnemyState.Stagger, grunt.State);
            grunt.GetComponent<StatusEffects>().Apply(StatusType.Stun, 1f);
            Tick(Dt * 3);
            Assert.AreEqual(EnemyState.Stagger, grunt.State);

            // slow: compare distance covered chasing over the same time
            float Chase(bool slowed)
            {
                TearDown(); SetUp();
                Floor(); SpawnPlayer();
                var g = SpawnGrunt(-8f);   // facing right, so the player at x=0 is in view and it gives chase
                if (slowed) g.GetComponent<StatusEffects>().Apply(StatusType.Slow, 5f);
                float x0 = g.transform.position.x;
                Tick(1.0f);
                return Mathf.Abs(g.transform.position.x - x0);
            }
            Assert.Less(Chase(true), Chase(false) * 0.8f);
        }

        // ------------------------------------------------------------------ skills

        [Test]
        public void GrenadeExplodesDamagingAndBurningNearbyEnemiesOnly()
        {
            Floor();
            SpawnPlayer();
            _inv.EquipSkill(0, ItemInstance.ForSkill(SkillAsset("PowderKeg")));
            var near = SpawnDummy(5.5f);
            var far = SpawnDummy(-12f);
            Tick(0.3f);

            Assert.IsTrue(_skills.TryUse(0));
            Tick(2f);
            Assert.Greater(near.TotalDamage, 30f, "grenade damage 38");
            Assert.AreEqual(0f, far.TotalDamage);
            Assert.IsTrue(near.GetComponent<StatusEffects>().Has(StatusType.Burn) || near.TotalDamage > 38f);
        }

        [Test]
        public void TurretShootsNearbyEnemiesAndExpires()
        {
            Floor();
            SpawnPlayer();
            _inv.EquipSkill(1, ItemInstance.ForSkill(SkillAsset("BellTurret")));
            var dummy = SpawnDummy(7f);
            Tick(0.3f);
            Assert.IsTrue(_skills.TryUse(1));
            Tick(2f);
            Assert.Greater(dummy.Hits, 2, "turret keeps firing");
            Tick(10f);
            foreach (var t in UnityEngine.Object.FindObjectsByType<TurretEntity>()) Assert.IsTrue(t.Expired, "turret expires");
        }

        [Test]
        public void TrapArmsThenStunsTheFirstEnemyToStepOnIt()
        {
            Floor();
            SpawnPlayer();
            _inv.EquipSkill(0, ItemInstance.ForSkill(SkillAsset("SnareTrap")));
            var dummy = SpawnDummy(0.8f);   // exactly where the trap lands
            Tick(0.3f);
            Assert.IsTrue(_skills.TryUse(0));
            Tick(0.2f);
            Assert.AreEqual(0f, dummy.TotalDamage, "not armed yet");
            Tick(1f);
            Assert.Greater(dummy.TotalDamage, 0f);
            Assert.IsTrue(dummy.GetComponent<StatusEffects>().IsStunned);
            foreach (var t in UnityEngine.Object.FindObjectsByType<TrapEntity>()) Assert.IsTrue(t.Spent, "single use");
        }

        [Test]
        public void SkillsHaveCooldownsThatStatsAndAffixesShorten()
        {
            Floor();
            SpawnPlayer();
            var keg = ItemInstance.ForSkill(SkillAsset("PowderKeg"));
            _inv.EquipSkill(0, keg);
            Tick(0.2f);
            Assert.IsTrue(_skills.TryUse(0));
            Assert.IsFalse(_skills.TryUse(0), "on cooldown");
            Assert.AreEqual(9f, _skills.CooldownRemaining(0), 0.1f);
            Assert.AreEqual(1f, _skills.CooldownFraction(0), 0.01f);

            Tick(9.2f);
            Assert.IsTrue(_skills.TryUse(0), "ready again");

            keg.Affixes.Add(new AffixInstance(Affix("s_cooldown"), 0.5f));
            Assert.AreEqual(9f / 1.5f, _stats.SkillCooldown(0), 0.01f);
        }

        [Test]
        public void SkillDamageScalesWithScrollsAndRarity()
        {
            Floor();
            SpawnPlayer();
            _inv.EquipSkill(0, ItemInstance.ForSkill(SkillAsset("PowderKeg"), ItemRarity.Rare));   // Ember
            _inv.AddScroll(StatColor.Ember);
            _inv.AddScroll(StatColor.Ember);
            float expected = 38f * 1.2f * (1f + 2 * PlayerStats.DamagePerScrollLevel);
            Assert.AreEqual(expected, _stats.SkillDamage(0), 0.1f);
        }

        // ------------------------------------------------------------------ pickups and interaction

        [Test]
        public void InteractingPicksUpFloorItemsAndPublishesThePrompt()
        {
            Floor();
            SpawnPlayer();
            var prompts = new List<InteractPromptEvent>();
            Action<InteractPromptEvent> onPrompt = p => prompts.Add(p);
            EventBus<InteractPromptEvent>.Subscribe(onPrompt);
            try
            {
                var hammer = ItemInstance.ForWeapon(WeaponAsset("TideHammer"), ItemRarity.Rare);
                LootFactory.DropItem(hammer, new Vector2(0.6f, 0f));
                Tick(0.2f);
                Assert.IsNotNull(_sensor.Current);
                var shown = prompts.FindLast(p => p.Visible);
                StringAssert.Contains("Tide Hammer", shown.Text);
                Assert.AreSame(hammer, shown.Item);

                // both weapon slots are full and nothing listens for the replace dialog: slot 1 is replaced
                _in.PressInteract();
                Tick(0.2f);
                Assert.AreSame(hammer, _inv.Weapons[0]);
                Assert.AreEqual(hammer.Weapon, _combat.Slot1, "combat uses the new weapon");

                bool oldSwordOnFloor = false;
                foreach (var w in UnityEngine.Object.FindObjectsByType<WorldItem>()) if (w.Item.BaseName == "Bellringer's Sword") oldSwordOnFloor = true;
                Assert.IsTrue(oldSwordOnFloor);

                _pc.ResetTo(new Vector2(30f, 0f));
                Tick(0.2f);
                Assert.IsFalse(prompts[prompts.Count - 1].Visible, "prompt hides when nothing is in reach");
            }
            finally { EventBus<InteractPromptEvent>.Unsubscribe(onPrompt); }
        }

        [Test]
        public void ReplaceDialogLetsThePlayerChooseTheSlotOrCancel()
        {
            Floor();
            SpawnPlayer();
            int choice = 1;
            Action<ReplaceItemRequest> handler = r => r.Resolve(choice);
            EventBus<ReplaceItemRequest>.Subscribe(handler);
            try
            {
                var dagger = ItemInstance.ForWeapon(WeaponAsset("RustedDagger"));
                _inv.PickUp(dagger, Vector2.zero);
                Assert.AreSame(dagger, _inv.Weapons[1], "chose slot 2");
                Assert.AreEqual(dagger.Weapon, _combat.Slot2);

                choice = -1;
                var hammer = ItemInstance.ForWeapon(WeaponAsset("TideHammer"));
                bool accepted = true;
                _inv.PickUp(hammer, Vector2.zero, ok => accepted = ok);
                Assert.IsFalse(accepted, "cancelled");
                Assert.AreNotSame(hammer, _inv.Weapons[0]);
                Assert.AreNotSame(hammer, _inv.Weapons[1]);
            }
            finally { EventBus<ReplaceItemRequest>.Unsubscribe(handler); }
        }

        [Test]
        public void ChestOpensOnceAndDropsItsContents()
        {
            Floor();
            SpawnPlayer();
            var contents = ItemInstance.ForGear(Amulet("EmberSigil"), ItemRarity.Epic);
            var chest = LootFactory.SpawnChest(new Vector2(0.5f, 0f), contents);
            _spawned.Add(chest.gameObject);
            Tick(0.2f);
            _in.PressInteract();
            Tick(0.1f);
            Assert.IsTrue(chest.Opened);

            WorldItem dropped = null;
            foreach (var w in UnityEngine.Object.FindObjectsByType<WorldItem>()) if (w.Item == contents) dropped = w;
            Assert.IsNotNull(dropped);
            _in.PressInteract();
            Tick(0.1f);   // picks up the dropped amulet (nearest interactable), the chest is spent
            Assert.AreSame(contents, _inv.Amulet);
        }

        [Test]
        public void GoldAndScrollsAreCollectedByWalkingNearThem()
        {
            Floor();
            SpawnPlayer();
            var coin = LootFactory.SpawnGold(new Vector2(1.5f, 0.6f), 12);
            LootFactory.SpawnScroll(new Vector2(0.3f, 0f), StatColor.Brine);
            Tick(1.5f);
            Assert.AreEqual(12, _inv.Gold, "the coin flew to the player");
            Assert.AreEqual(1, _inv.Scrolls[1]);
            Assert.IsTrue(coin == null || coin.Collected);
        }

        // ------------------------------------------------------------------ flask

        [Test]
        public void FlaskHealsFortyFivePercentAndRunsOut()
        {
            Floor();
            SpawnPlayer();
            Assert.AreEqual(2, _inv.FlaskCharges);

            Assert.IsFalse(_inv.UseFlask(_health), "no charge wasted at full health");
            Assert.AreEqual(2, _inv.FlaskCharges);

            _health.TakeHit(EnemyHit(60f, new Vector2(5f, 0f)));
            Tick(0.3f);   // the hit stuns you briefly; wait it out
            float before = _health.Current;
            _in.PressFlask();
            Tick(Dt * 2);
            Assert.AreEqual(Mathf.Min(_health.Max, before + _health.Max * 0.45f), _health.Current, 0.5f);
            Assert.AreEqual(1, _inv.FlaskCharges);

            _inv.UseFlask(_health);
            _health.TakeHit(EnemyHit(5f, new Vector2(5f, 0f)));
            _def.Tick(5f);
            _health.TakeHit(EnemyHit(50f, new Vector2(5f, 0f)));
            Assert.IsFalse(_inv.UseFlask(_health), "empty");

            _inv.RefillFlask();
            Assert.AreEqual(2, _inv.FlaskCharges);
        }

        // ------------------------------------------------------------------ mutations

        [Test]
        public void MutationSlotsAreLimitedAndAltarsGrantThem()
        {
            Floor();
            SpawnPlayer();
            var altar = LootFactory.SpawnAltar(new Vector2(0.5f, 0f), new List<MutationData> { Mutation("TemperedFrame"), Mutation("Gilded"), Mutation("Bloodletter") });
            _spawned.Add(altar.gameObject);

            Assert.IsTrue(altar.Choose(_inv, 0));
            Assert.IsTrue(altar.Used);
            Assert.AreEqual((110f) * 1.25f, _health.Max, 0.5f, "Tempered Frame +25% max health");
            Assert.IsFalse(altar.Choose(_inv, 1), "single use");

            Assert.IsTrue(_inv.AddMutation(Mutation("Gilded")));
            Assert.IsTrue(_inv.AddMutation(Mutation("Bloodletter")));
            Assert.IsFalse(_inv.AddMutation(Mutation("Recklessness")), "only three slots");
            Assert.AreEqual(PlayerInventory.MaxMutations, _inv.Mutations.Count);
            Assert.AreEqual(1.3f, _stats.GoldFind, 0.001f);
        }

        [Test]
        public void BloodletterHealsOnKillAndRecklessnessTradesDefenseForDamage()
        {
            Floor();
            SpawnPlayer();
            _inv.AddMutation(Mutation("Bloodletter"));
            _health.TakeHit(EnemyHit(50f, new Vector2(5f, 0f)));
            float before = _health.Current;

            var grunt = SpawnGrunt(12f);
            grunt.GetComponent<Health>().TakeHit(new DamageInfo { Amount = 999f, SourceTeam = Team.Player });
            Assert.AreEqual(before + 3f, _health.Current, 0.01f);

            _inv.AddMutation(Mutation("Recklessness"));
            Assert.AreEqual(1.25f, _stats.GetWeaponMods(0).DamageMultiplier, 0.001f);
            Assert.AreEqual(1.15f, _stats.DamageTakenMultiplier, 0.001f);
        }

        [Test]
        public void CounterstrikeBoostsTheSwingAfterAPerfectParry()
        {
            Floor();
            SpawnPlayer();
            var dummy = SpawnDummy(1.6f);
            _inv.AddMutation(Mutation("Counterstrike"));
            Tick(0.3f);

            _in.PressAttack(1);          // raise the shield (slot 2)
            Tick(Dt * 3);
            Assert.AreEqual(HitResult.Parried, _health.TakeHit(EnemyHit(20f, new Vector2(2f, 0f), new FakeAttacker())));
            Assert.AreEqual(2.5f, _combat.PendingHitMultiplier, 0.001f);

            _in.ReleaseAttack(1);
            Tick(0.5f);
            Swing();
            Assert.AreEqual(14f * 2.5f, dummy.LastHit, 0.1f);
            Assert.AreEqual(1f, _combat.PendingHitMultiplier, "riposte is consumed");
        }

        [Test]
        public void MomentumStacksOnKillsAndFadesAway()
        {
            Floor();
            SpawnPlayer();
            _inv.AddMutation(Mutation("Momentum"));
            for (int i = 0; i < 2; i++)
                SpawnGrunt(20f + i * 3f).GetComponent<Health>().TakeHit(new DamageInfo { Amount = 999f, SourceTeam = Team.Player });

            Assert.AreEqual(2, _mutations.MomentumStacks);
            Assert.AreEqual(1f + 2 * 0.08f, _stats.GetWeaponMods(0).DamageMultiplier, 0.001f);

            Tick(5.5f);
            Assert.AreEqual(0, _mutations.MomentumStacks);
            Assert.AreEqual(1f, _stats.GetWeaponMods(0).DamageMultiplier, 0.001f);
        }

        [Test]
        public void SecondWindSavesYouOncePerRun()
        {
            Floor();
            SpawnPlayer();
            _inv.AddMutation(Mutation("SecondWind"));

            _health.TakeHit(EnemyHit(9999f, new Vector2(5f, 0f)));
            Assert.IsFalse(_health.IsDead, "survived");
            Assert.AreEqual(1f + _health.Max * 0.3f, _health.Current, 0.5f, "left at 1 health, then healed 30% of max");

            _def.Tick(5f);
            _health.TakeHit(EnemyHit(9999f, new Vector2(5f, 0f)));
            Assert.IsTrue(_health.IsDead, "only once");

            // a new run re-arms it
            _health.ResetToFull();
            _inv.ResetForRun(_db);
            _inv.AddMutation(Mutation("SecondWind"));
            _def.Tick(5f);
            _health.TakeHit(EnemyHit(9999f, new Vector2(5f, 0f)));
            Assert.IsFalse(_health.IsDead);
        }

        // ------------------------------------------------------------------ shops

        [Test]
        public void ShopSellsItemsOnceTakesGoldAndRefundsCancelledPurchases()
        {
            Floor();
            SpawnPlayer();
            var stock = ShopStock.Generate(_db, new SeededRng(4), 0.4f);
            var weaponOffer = stock.Offers.Find(o => o.Kind == OfferKind.Item);

            Assert.AreEqual(BuyResult.NotEnoughGold, stock.TryBuy(weaponOffer, _inv, Vector2.zero));
            Assert.IsFalse(weaponOffer.Sold);

            _inv.AddGold(2000);
            int before = _inv.Gold;
            Assert.AreEqual(BuyResult.Bought, stock.TryBuy(weaponOffer, _inv, Vector2.zero));
            Assert.AreEqual(before - weaponOffer.Item.Price, _inv.Gold);
            Assert.IsTrue(weaponOffer.Sold);
            Assert.AreEqual(BuyResult.SoldOut, stock.TryBuy(weaponOffer, _inv, Vector2.zero));

            // cancelling the replace dialog refunds the gold and keeps the item for sale
            var other = stock.Offers.Find(o => o.Kind == OfferKind.Item && !o.Sold && (o.Item.Kind == ItemKind.Weapon || o.Item.Kind == ItemKind.Skill));
            if (other != null)
            {
                // fill both slots of that kind first
                if (other.Item.Kind == ItemKind.Skill)
                {
                    _inv.EquipSkill(0, ItemInstance.ForSkill(SkillAsset("PowderKeg")));
                    _inv.EquipSkill(1, ItemInstance.ForSkill(SkillAsset("BellTurret")));
                }
                Action<ReplaceItemRequest> cancel = r => r.Resolve(-1);
                EventBus<ReplaceItemRequest>.Subscribe(cancel);
                try
                {
                    int gold = _inv.Gold;
                    stock.TryBuy(other, _inv, Vector2.zero);
                    Assert.AreEqual(gold, _inv.Gold, "refunded");
                    Assert.IsFalse(other.Sold);
                }
                finally { EventBus<ReplaceItemRequest>.Unsubscribe(cancel); }
            }
        }

        [Test]
        public void ShopUpgradesRefillsAndSellsScrolls()
        {
            Floor();
            SpawnPlayer();
            var stock = ShopStock.Generate(_db, new SeededRng(9), 0.2f);
            _inv.AddGold(3000);

            var up = stock.Offers.Find(o => o.Kind == OfferKind.UpgradeSlot && o.Slot == 0);
            int price = up.PriceFor(_inv);
            int gold = _inv.Gold;
            Assert.AreEqual(BuyResult.Bought, stock.TryBuy(up, _inv, Vector2.zero));
            Assert.AreEqual(2, _inv.Weapons[0].Tier);
            Assert.AreEqual(gold - price, _inv.Gold);
            Assert.Greater(up.PriceFor(_inv), price, "next tier costs more");

            var flask = stock.Offers.Find(o => o.Kind == OfferKind.FlaskRefill);
            Assert.AreEqual(BuyResult.NotAvailable, stock.TryBuy(flask, _inv, Vector2.zero), "flask already full");
            _health.TakeHit(EnemyHit(50f, new Vector2(5f, 0f)));
            _inv.UseFlask(_health);
            Assert.AreEqual(BuyResult.Bought, stock.TryBuy(flask, _inv, Vector2.zero));
            Assert.AreEqual(_inv.MaxFlaskCharges, _inv.FlaskCharges);

            var scroll = stock.Offers.Find(o => o.Kind == OfferKind.Scroll);
            Assert.AreEqual(BuyResult.Bought, stock.TryBuy(scroll, _inv, Vector2.zero));
            Assert.AreEqual(1, _inv.TotalScrolls);
        }

        // ------------------------------------------------------------------ drops and run reset

        [Test]
        public void EnemiesDropGoldAndTheDirectorPlacesScrollsDeterministically()
        {
            Floor();
            SpawnPlayer();
            var director = new GameObject("Loot Director").AddComponent<LootDirector>();
            _spawned.Add(director.gameObject);
            director.BeginRun(555);

            var grunt = SpawnGrunt(15f);
            grunt.GetComponent<Health>().TakeHit(new DamageInfo { Amount = 999f, SourceTeam = Team.Player });
            var coins = UnityEngine.Object.FindObjectsByType<Collectible>();
            int value = 0;
            foreach (var c in coins) if (c.Kind == CollectibleKind.Gold) value += c.Value;
            Assert.GreaterOrEqual(value, 3, "an enemy drops gold");

            // room-clear scroll drops follow the per-room seed
            for (int cell = 0; cell < 12; cell++)
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collectible>()) if (c.Kind == CollectibleKind.Scroll) UnityEngine.Object.DestroyImmediate(c.gameObject);
                EventBus<RoomClearedEvent>.Raise(new RoomClearedEvent { CellIndex = cell, Position = new Vector2(cell * 3f, 0f) });
                bool expected = new SeededRng(555).Fork(2000 + cell).Chance(0.45f);
                int scrolls = 0;
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collectible>()) if (c.Kind == CollectibleKind.Scroll) scrolls++;
                Assert.AreEqual(expected ? 1 : 0, scrolls, $"cell {cell}");
            }
        }

        [Test]
        public void NewRunWipesGoldScrollsMutationsAndRestoresTheLoadout()
        {
            Floor();
            SpawnPlayer();
            _inv.AddGold(500);
            _inv.AddScroll(StatColor.Ember);
            _inv.AddMutation(Mutation("Gilded"));
            _inv.EquipWeapon(0, ItemInstance.ForWeapon(WeaponAsset("TideHammer"), ItemRarity.Legendary));
            _inv.PickUp(ItemInstance.ForGear(Amulet("EmberSigil")), Vector2.zero);

            _inv.ResetForRun(_db);
            Assert.AreEqual(0, _inv.Gold);
            Assert.AreEqual(0, _inv.TotalScrolls);
            Assert.AreEqual(0, _inv.Mutations.Count);
            Assert.IsNull(_inv.Amulet);
            Assert.AreEqual(_db.startingWeapon1, _inv.Weapons[0].Weapon);
            Assert.AreEqual(_db.startingWeapon1, _combat.Slot1);
            Assert.AreEqual(110f, _health.Max, 0.01f);
        }
    }
}
