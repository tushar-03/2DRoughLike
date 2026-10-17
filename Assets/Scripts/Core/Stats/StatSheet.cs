using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>The three scroll colours. Weapons and skills have colour affinities that decide which scroll stats scale them.</summary>
    [Flags]
    public enum StatColor
    {
        None = 0,
        /// <summary>Red: heavy, brutal damage.</summary>
        Ember = 1 << 0,
        /// <summary>Cyan: precision, traps, ranged.</summary>
        Brine = 1 << 1,
        /// <summary>Green: survival, shields.</summary>
        Moss = 1 << 2
    }

    public enum StatKind
    {
        MaxHealth,
        /// <summary>Multiplier on all damage dealt (1 = normal).</summary>
        DamageDealt,
        /// <summary>Multiplier on damage taken (lower is tougher).</summary>
        DamageTaken,
        MoveSpeed,
        AttackSpeed,
        CritChance,
        /// <summary>Crit damage multiplier (1.5 = +50%).</summary>
        CritDamage,
        GoldFind,
        HealOnKill,
        ParryWindow,
        /// <summary>Skill cooldown recovery rate.</summary>
        CooldownRate,
        GoldOnKill
    }

    public enum ModOp
    {
        /// <summary>Added to the base value.</summary>
        Flat,
        /// <summary>Summed with other PercentAdd modifiers, then multiplies the total (+10% and +15% = +25%).</summary>
        PercentAdd,
        /// <summary>Each multiplies the total separately.</summary>
        PercentMult
    }

    /// <summary>One change to a stat. <see cref="Source"/> identifies who added it so it can be removed together.</summary>
    public readonly struct StatModifier
    {
        public readonly StatKind Stat;
        public readonly ModOp Op;
        public readonly float Value;
        public readonly object Source;

        public StatModifier(StatKind stat, ModOp op, float value, object source = null)
        {
            Stat = stat; Op = op; Value = value; Source = source;
        }
    }

    /// <summary>
    /// Base values plus a list of modifiers. value = (base + flat) x (1 + sum of percentAdd) x product of (1 + percentMult).
    /// Used for the player; enemies can reuse it for elite modifiers.
    /// </summary>
    public sealed class StatSheet
    {
        static readonly int Count = Enum.GetValues(typeof(StatKind)).Length;

        readonly float[] _base = new float[Count];
        readonly List<StatModifier> _mods = new List<StatModifier>();

        public event Action Changed;

        public StatSheet()
        {
            _base[(int)StatKind.MaxHealth] = 100f;
            _base[(int)StatKind.DamageDealt] = 1f;
            _base[(int)StatKind.DamageTaken] = 1f;
            _base[(int)StatKind.MoveSpeed] = 1f;
            _base[(int)StatKind.AttackSpeed] = 1f;
            _base[(int)StatKind.CritChance] = 0.05f;
            _base[(int)StatKind.CritDamage] = 1.5f;
            _base[(int)StatKind.GoldFind] = 1f;
            _base[(int)StatKind.ParryWindow] = 1f;
            _base[(int)StatKind.CooldownRate] = 1f;
        }

        public void SetBase(StatKind stat, float value)
        {
            _base[(int)stat] = value;
            Changed?.Invoke();
        }

        public float GetBase(StatKind stat) => _base[(int)stat];

        public void Add(StatModifier mod)
        {
            _mods.Add(mod);
            Changed?.Invoke();
        }

        public void AddRange(IEnumerable<StatModifier> mods)
        {
            foreach (var m in mods) _mods.Add(m);
            Changed?.Invoke();
        }

        public int RemoveBySource(object source)
        {
            int removed = _mods.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        public void ClearModifiers()
        {
            if (_mods.Count == 0) return;
            _mods.Clear();
            Changed?.Invoke();
        }

        public float Get(StatKind stat)
        {
            float flat = 0f, pctAdd = 0f, mult = 1f;
            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                if (m.Stat != stat) continue;
                switch (m.Op)
                {
                    case ModOp.Flat: flat += m.Value; break;
                    case ModOp.PercentAdd: pctAdd += m.Value; break;
                    case ModOp.PercentMult: mult *= 1f + m.Value; break;
                }
            }
            float v = (_base[(int)stat] + flat) * (1f + pctAdd) * mult;
            return Clamp(stat, v);
        }

        static float Clamp(StatKind stat, float v)
        {
            switch (stat)
            {
                case StatKind.MaxHealth: return Mathf.Max(10f, v);
                case StatKind.CritChance: return Mathf.Clamp01(v);
                case StatKind.DamageTaken: return Mathf.Max(0.1f, v);
                case StatKind.AttackSpeed: return Mathf.Clamp(v, 0.4f, 3f);
                case StatKind.MoveSpeed: return Mathf.Clamp(v, 0.5f, 2f);
                case StatKind.CooldownRate: return Mathf.Clamp(v, 0.25f, 4f);
                case StatKind.ParryWindow: return Mathf.Clamp(v, 0.5f, 3f);
                default: return Mathf.Max(0f, v);
            }
        }
    }
}
