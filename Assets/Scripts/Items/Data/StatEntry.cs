using System;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Items
{
    /// <summary>One stat change as authored on gear and mutations.</summary>
    [Serializable]
    public struct StatEntry
    {
        public StatKind stat;
        public ModOp op;
        public float value;

        public StatEntry(StatKind stat, ModOp op, float value)
        {
            this.stat = stat; this.op = op; this.value = value;
        }

        public string Describe()
        {
            bool pct = op != ModOp.Flat;
            string v = pct ? (value >= 0f ? "+" : "") + Mathf.RoundToInt(value * 100f) + "%" : (value >= 0f ? "+" : "") + Mathf.RoundToInt(value);
            return v + " " + StatNames.Label(stat);
        }
    }

    public static class StatNames
    {
        public static string Label(StatKind s)
        {
            switch (s)
            {
                case StatKind.MaxHealth: return "max health";
                case StatKind.DamageDealt: return "damage";
                case StatKind.DamageTaken: return "damage taken";
                case StatKind.MoveSpeed: return "move speed";
                case StatKind.AttackSpeed: return "attack speed";
                case StatKind.CritChance: return "crit chance";
                case StatKind.CritDamage: return "crit damage";
                case StatKind.GoldFind: return "gold find";
                case StatKind.HealOnKill: return "health per kill";
                case StatKind.ParryWindow: return "parry window";
                case StatKind.CooldownRate: return "skill cooldown rate";
                case StatKind.GoldOnKill: return "gold per kill";
                default: return s.ToString();
            }
        }

        public static Color Colour(StatColor c)
        {
            switch (c)
            {
                case StatColor.Ember: return new Color(0.95f, 0.3f, 0.25f);
                case StatColor.Brine: return new Color(0.3f, 0.8f, 0.95f);
                case StatColor.Moss: return new Color(0.45f, 0.85f, 0.4f);
                default: return Color.white;
            }
        }
    }
}
