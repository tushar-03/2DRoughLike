using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>
    /// Turns the inventory into numbers. Owns the player's <see cref="StatSheet"/>: gear, scrolls and mutations feed
    /// modifiers into it, and it pushes the results to health, movement and combat. Implements
    /// <see cref="IPlayerModifiers"/> so combat can ask for per-weapon damage, crit, status procs and speed.
    /// </summary>
    [DefaultExecutionOrder(5)]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerStats : MonoBehaviour, IPlayerModifiers
    {
        /// <summary>Each scroll adds this much max health (and one stat level).</summary>
        public const float HealthPerScroll = 8f;
        /// <summary>Damage bonus per scroll level in a weapon's colour.</summary>
        public const float DamagePerScrollLevel = 0.12f;

        public StatSheet Sheet { get; } = new StatSheet();

        PlayerInventory _inventory;
        Health _health;
        PlayerController _controller;
        PlayerCombat _combat;
        int _momentumStacks;
        float _momentumValue;

        public PlayerInventory Inventory => _inventory;

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<Health>();
            _controller = GetComponent<PlayerController>();
            _combat = GetComponent<PlayerCombat>();
            if (_combat != null) _combat.Modifiers = this;
        }

        void OnEnable()
        {
            _inventory.Changed += Recalculate;
            EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
            if (_combat != null) _combat.Modifiers = this;
            Recalculate();
        }

        void OnDisable()
        {
            _inventory.Changed -= Recalculate;
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
            if (_combat != null && ReferenceEquals(_combat.Modifiers, this)) _combat.Modifiers = null;
        }

        // =====================================================================
        // Recalculation
        // =====================================================================

        public void Recalculate()
        {
            if (_inventory == null) return;
            Sheet.ClearModifiers();

            // scrolls
            Sheet.Add(new StatModifier(StatKind.MaxHealth, ModOp.Flat, HealthPerScroll * _inventory.TotalScrolls, this));

            // equipped gear (amulet, outfit) and its affixes
            if (_inventory.Amulet != null) Sheet.AddRange(new List<StatModifier>(_inventory.Amulet.GearModifiers(_inventory.Amulet)));
            if (_inventory.Outfit != null) Sheet.AddRange(new List<StatModifier>(_inventory.Outfit.GearModifiers(_inventory.Outfit)));

            // mutations
            foreach (var m in _inventory.Mutations)
            {
                if (m.modifiers != null)
                    foreach (var e in m.modifiers) Sheet.Add(new StatModifier(e.stat, e.op, e.value, m));
                if (m.effect == MutationEffect.HealOnKill)
                    Sheet.Add(new StatModifier(StatKind.HealOnKill, ModOp.Flat, m.effectValue, m));
            }

            if (_momentumStacks > 0)
                Sheet.Add(new StatModifier(StatKind.DamageDealt, ModOp.PercentAdd, _momentumStacks * _momentumValue, this));

            Apply();
        }

        void Apply()
        {
            if (_health != null) _health.SetMax(Sheet.Get(StatKind.MaxHealth), true);
            if (_controller != null) _controller.SpeedBonus = Sheet.Get(StatKind.MoveSpeed);
        }

        /// <summary>Used by the Momentum mutation.</summary>
        public void SetMomentum(int stacks, float valuePerStack)
        {
            if (stacks == _momentumStacks && Mathf.Approximately(valuePerStack, _momentumValue)) return;
            _momentumStacks = stacks;
            _momentumValue = valuePerStack;
            Recalculate();
        }

        // =====================================================================
        // IPlayerModifiers
        // =====================================================================

        public float AttackSpeed => Sheet.Get(StatKind.AttackSpeed);
        public float DamageTakenMultiplier => Sheet.Get(StatKind.DamageTaken);
        public float ParryWindowMultiplier => Sheet.Get(StatKind.ParryWindow);

        /// <summary>Damage multiplier from scroll levels for an item's colour affinity.</summary>
        public float ScrollScale(StatColor affinity)
        {
            int levels = affinity == StatColor.None ? Mathf.RoundToInt(_inventory.TotalScrolls * 0.3f) : _inventory.ScrollLevels(affinity);
            return 1f + DamagePerScrollLevel * levels;
        }

        public WeaponMods GetWeaponMods(int slot)
        {
            var mods = WeaponMods.Neutral;
            ItemInstance item = slot >= 0 && slot < 2 ? _inventory.Weapons[slot] : null;

            mods.DamageMultiplier = Sheet.Get(StatKind.DamageDealt);
            mods.CritChance = Sheet.Get(StatKind.CritChance);
            mods.CritMultiplier = Sheet.Get(StatKind.CritDamage);
            if (item == null) return mods;

            float damage = item.Power * ScrollScale(item.Affinity);
            float affixDamage = 0f;
            foreach (var a in item.Affixes)
            {
                if (a.data.kind == AffixKind.OnHitStatus)
                {
                    if (a.value > mods.StatusChance)
                    {
                        mods.Status = a.data.status;
                        mods.StatusChance = a.value;
                        mods.StatusDuration = a.data.statusDuration;
                        mods.StatusPotency = a.data.statusPotency;
                    }
                    continue;
                }
                switch (a.data.stat)
                {
                    case StatKind.DamageDealt: affixDamage += a.value; break;
                    case StatKind.CritChance: mods.CritChance = Mathf.Clamp01(mods.CritChance + a.value); break;
                    case StatKind.CritDamage: mods.CritMultiplier += a.value; break;
                }
            }

            mods.DamageMultiplier *= damage * (1f + affixDamage);
            return mods;
        }

        /// <summary>Damage for a skill in a slot: base x rarity/tier x scroll scaling x global damage x affixes.</summary>
        public float SkillDamage(int slot)
        {
            ItemInstance item = _inventory.Skills[slot];
            if (item == null) return 0f;
            float affix = 0f;
            foreach (var a in item.Affixes)
                if (a.data.kind == AffixKind.Stat && a.data.stat == StatKind.DamageDealt) affix += a.value;
            return item.Skill.damage * item.Power * ScrollScale(item.Affinity) * Sheet.Get(StatKind.DamageDealt) * (1f + affix);
        }

        /// <summary>Cooldown in seconds for a skill slot after cooldown-rate stats and affixes.</summary>
        public float SkillCooldown(int slot)
        {
            ItemInstance item = _inventory.Skills[slot];
            if (item == null) return 0f;
            float rate = Sheet.Get(StatKind.CooldownRate);
            foreach (var a in item.Affixes)
                if (a.data.kind == AffixKind.Stat && a.data.stat == StatKind.CooldownRate) rate += a.value;
            return item.Skill.cooldown / Mathf.Max(0.25f, rate);
        }

        public float GoldFind => Sheet.Get(StatKind.GoldFind);

        // =====================================================================
        // Kills
        // =====================================================================

        void OnEnemyDied(EnemyDiedEvent e)
        {
            if (_health == null || _health.IsDead) return;
            float heal = Sheet.Get(StatKind.HealOnKill);
            if (heal > 0f) _health.Heal(heal);

            int gold = Mathf.RoundToInt(Sheet.Get(StatKind.GoldOnKill));
            if (gold > 0) _inventory.AddGold(gold);
        }
    }
}
