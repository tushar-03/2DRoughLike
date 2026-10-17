using UnityEngine;

namespace Roughlike.Items
{
    public enum ItemRarity { Common, Rare, Epic, Legendary }

    public enum ItemKind { Weapon, Skill, Amulet, Outfit }

    /// <summary>Rarity presentation and balance numbers in one place.</summary>
    public static class Rarity
    {
        public static Color Colour(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Rare: return new Color(0.35f, 0.65f, 1f);
                case ItemRarity.Epic: return new Color(0.75f, 0.4f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.72f, 0.2f);
                default: return new Color(0.9f, 0.9f, 0.9f);
            }
        }

        public static string Label(ItemRarity r) => r.ToString();

        /// <summary>Multiplier on an item's base damage / base stat values.</summary>
        public static float PowerMultiplier(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Rare: return 1.2f;
                case ItemRarity.Epic: return 1.45f;
                case ItemRarity.Legendary: return 1.75f;
                default: return 1f;
            }
        }

        /// <summary>How many random affixes an item of this rarity rolls.</summary>
        public static int AffixCount(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Rare: return 1;
                case ItemRarity.Epic: return 2;
                case ItemRarity.Legendary: return 3;
                default: return 0;
            }
        }

        public static float PriceMultiplier(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Rare: return 1.8f;
                case ItemRarity.Epic: return 3.2f;
                case ItemRarity.Legendary: return 5.5f;
                default: return 1f;
            }
        }
    }
}
