using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Items
{
    /// <summary>
    /// A concrete item the player can own: a base definition (weapon, skill or gear) plus a rolled rarity, tier and affixes.
    /// Two swords are different instances with different stats. Plain data, easy to test and to save (M8).
    /// </summary>
    public sealed class ItemInstance
    {
        public const int MaxTier = 4;

        public ItemKind Kind;
        public WeaponData Weapon;
        public SkillData Skill;
        public GearData Gear;
        public ItemRarity Rarity;
        public int Tier = 1;
        public bool Cursed;
        public readonly List<AffixInstance> Affixes = new List<AffixInstance>();

        public static ItemInstance ForWeapon(WeaponData w, ItemRarity rarity = ItemRarity.Common, int tier = 1)
            => new ItemInstance { Kind = ItemKind.Weapon, Weapon = w, Rarity = rarity, Tier = tier };

        public static ItemInstance ForSkill(SkillData s, ItemRarity rarity = ItemRarity.Common, int tier = 1)
            => new ItemInstance { Kind = ItemKind.Skill, Skill = s, Rarity = rarity, Tier = tier };

        public static ItemInstance ForGear(GearData g, ItemRarity rarity = ItemRarity.Common, int tier = 1)
            => new ItemInstance { Kind = g.slot == GearSlot.Amulet ? ItemKind.Amulet : ItemKind.Outfit, Gear = g, Rarity = rarity, Tier = tier };

        public string BaseName
        {
            get
            {
                switch (Kind)
                {
                    case ItemKind.Weapon: return Weapon != null ? Weapon.displayName : "Weapon";
                    case ItemKind.Skill: return Skill != null ? Skill.displayName : "Skill";
                    default: return Gear != null ? Gear.displayName : "Gear";
                }
            }
        }

        public string DisplayName => (Cursed ? "Cursed " : "") + BaseName;

        public Color RarityColour => Items.Rarity.Colour(Rarity);

        public StatColor Affinity
        {
            get
            {
                if (Kind == ItemKind.Weapon && Weapon != null) return Weapon.affinity;
                if (Kind == ItemKind.Skill && Skill != null) return Skill.affinity;
                return StatColor.None;
            }
        }

        /// <summary>Damage multiplier from upgrade tier (+18% per tier above 1).</summary>
        public float TierMultiplier => 1f + 0.18f * (Tier - 1);

        /// <summary>Cursed items trade a bad affix for a power bonus.</summary>
        public float CurseBonus => Cursed ? 1.2f : 1f;

        /// <summary>Combined base-power multiplier: rarity, tier and curse.</summary>
        public float Power => Items.Rarity.PowerMultiplier(Rarity) * TierMultiplier * CurseBonus;

        public bool CanUpgrade => Tier < MaxTier;

        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            Tier++;
            return true;
        }

        /// <summary>Stat modifiers an amulet or outfit grants (base values scaled by rarity and tier, plus gear affixes).</summary>
        public IEnumerable<StatModifier> GearModifiers(object source)
        {
            if (Gear == null) yield break;
            float scale = Power;
            foreach (var e in Gear.modifiers)
            {
                // flat bonuses scale too; percentages scale gently so common gear stays useful
                yield return new StatModifier(e.stat, e.op, e.value * scale, source);
            }
            foreach (var a in Affixes)
            {
                if (a.data.kind != AffixKind.Stat) continue;
                yield return new StatModifier(a.data.stat, a.data.op, a.value, source);
            }
        }

        public int Price => ItemPricing.Price(this);

        /// <summary>Lines for an item card.</summary>
        public List<string> DescribeLines()
        {
            var lines = new List<string>();
            switch (Kind)
            {
                case ItemKind.Weapon when Weapon != null:
                    lines.Add(Weapon.kind == WeaponKind.Shield ? "Shield" : $"{Weapon.steps.Length}-hit combo");
                    if (Weapon.kind == WeaponKind.Melee && Weapon.steps.Length > 0)
                        lines.Add($"Damage {Mathf.RoundToInt(Weapon.steps[0].damage * Power)} per hit");
                    break;
                case ItemKind.Skill when Skill != null:
                    lines.Add($"{Skill.kind}  cooldown {Skill.cooldown:0}s");
                    lines.Add($"Damage {Mathf.RoundToInt(Skill.damage * Power)}");
                    break;
                case ItemKind.Amulet:
                case ItemKind.Outfit:
                    if (Gear != null) foreach (var e in Gear.modifiers) lines.Add(new StatEntry(e.stat, e.op, e.value * Power).Describe());
                    break;
            }
            foreach (var a in Affixes) lines.Add(a.Describe());
            if (Affinity != StatColor.None) lines.Add("Scales with " + Affinity);
            if (Tier > 1) lines.Add("Tier " + Tier);
            return lines;
        }
    }

    public static class ItemPricing
    {
        public static int Price(ItemInstance item)
        {
            int baseCost;
            switch (item.Kind)
            {
                case ItemKind.Weapon: baseCost = 70; break;
                case ItemKind.Skill: baseCost = 80; break;
                default: baseCost = 90; break;
            }
            float p = baseCost * Rarity.PriceMultiplier(item.Rarity) * (1f + 0.35f * (item.Tier - 1));
            if (item.Cursed) p *= 0.6f;
            return Mathf.RoundToInt(p / 5f) * 5;
        }

        public static int UpgradePrice(ItemInstance item) => item == null ? 0 : 60 + 70 * item.Tier;
        public const int FlaskRefillPrice = 40;
        public const int ScrollPrice = 90;
    }
}
