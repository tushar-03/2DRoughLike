using System;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Items
{
    public enum AffixKind
    {
        /// <summary>Changes a stat.</summary>
        Stat,
        /// <summary>Adds a chance to apply a status effect on hit (weapons only).</summary>
        OnHitStatus
    }

    [Flags]
    public enum AffixTarget
    {
        None = 0,
        Weapon = 1 << 0,
        Gear = 1 << 1,
        Skill = 1 << 2,
        Any = Weapon | Gear | Skill
    }

    /// <summary>
    /// A random modifier an item can roll. Weapon and skill affixes apply to that item only; gear affixes apply to the
    /// player. Curses are negative affixes that only appear on cursed items, in exchange for stronger stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Roughlike/Items/Affix", fileName = "Affix")]
    public class AffixData : ScriptableObject
    {
        public string id = "affix";
        [Tooltip("Shown on item cards. {0} is replaced by the rolled value.")]
        public string label = "{0} damage";
        public AffixKind kind = AffixKind.Stat;
        public AffixTarget targets = AffixTarget.Any;
        public bool isCurse;
        [Tooltip("Show flat values as percentages (crit chance 0.06 shows as 6%).")]
        public bool showAsPercent;
        [Min(1)] public int weight = 10;

        [Header("Stat affix")]
        public StatKind stat = StatKind.DamageDealt;
        public ModOp op = ModOp.PercentAdd;

        [Header("Value range (rolled between min and max, then scaled by rarity)")]
        public float min = 0.05f;
        public float max = 0.15f;

        [Header("On-hit status affix (value is the proc chance)")]
        public StatusType status = StatusType.None;
        public float statusDuration = 3f;
        public float statusPotency = 1f;

        /// <summary>True when the value is shown as a percentage.</summary>
        public bool IsPercent => showAsPercent || kind == AffixKind.OnHitStatus || op != ModOp.Flat;
    }

    /// <summary>A rolled affix on a specific item.</summary>
    [Serializable]
    public sealed class AffixInstance
    {
        public AffixData data;
        public float value;

        public AffixInstance(AffixData data, float value)
        {
            this.data = data;
            this.value = value;
        }

        public string Describe()
        {
            string number;
            if (data.IsPercent)
            {
                float pct = value * 100f;
                number = (data.isCurse || pct < 0f ? "" : "+") + Mathf.RoundToInt(pct) + "%";
                if (data.kind == AffixKind.OnHitStatus) number = Mathf.RoundToInt(pct) + "%";
            }
            else
            {
                number = (value < 0f ? "" : "+") + Mathf.RoundToInt(value);
            }
            return string.Format(data.label, number);
        }
    }
}

