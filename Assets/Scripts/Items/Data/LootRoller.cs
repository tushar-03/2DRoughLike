using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Items
{
    /// <summary>
    /// Deterministic item generation. Everything draws from a <see cref="SeededRng"/>, so a seed fixes what every
    /// chest, shop and drop contains. Rarity improves with run progress; cursed items trade a bad affix for power.
    /// </summary>
    public static class LootRoller
    {
        /// <summary>Picks a rarity. <paramref name="progress"/> 0..1 along the level; <paramref name="bonus"/> 0..1 shifts toward rare (chests).</summary>
        public static ItemRarity RollRarity(SeededRng rng, float progress, float bonus = 0f)
        {
            progress = Mathf.Clamp01(progress);
            float common = Mathf.Max(5f, 62f - 36f * progress);
            float rare = 28f + 6f * progress;
            float epic = 8f + 22f * progress;
            float legendary = 2f + 10f * progress;

            float boost = 1f + 3f * Mathf.Clamp01(bonus);
            rare *= boost; epic *= boost; legendary *= boost;
            if (bonus >= 1f) common = 0f;

            float total = common + rare + epic + legendary;
            float roll = rng.Value * total;
            if ((roll -= common) < 0f) return ItemRarity.Common;
            if ((roll -= rare) < 0f) return ItemRarity.Rare;
            if ((roll -= epic) < 0f) return ItemRarity.Epic;
            return ItemRarity.Legendary;
        }

        public static ItemKind RollKind(SeededRng rng)
        {
            float r = rng.Value;
            if (r < 0.40f) return ItemKind.Weapon;
            if (r < 0.65f) return ItemKind.Skill;
            if (r < 0.83f) return ItemKind.Amulet;
            return ItemKind.Outfit;
        }

        public static StatColor RollScrollColour(SeededRng rng)
        {
            switch (rng.Range(0, 3))
            {
                case 0: return StatColor.Ember;
                case 1: return StatColor.Brine;
                default: return StatColor.Moss;
            }
        }

        /// <summary>Rolls a complete item. Returns null if the database has nothing for the requested kind.</summary>
        public static ItemInstance Roll(LootDatabase db, SeededRng rng, float progress, ItemKind? forceKind = null,
            float rarityBonus = 0f, bool allowCurse = true)
        {
            ItemKind kind = forceKind ?? RollKind(rng);
            ItemRarity rarity = RollRarity(rng, progress, rarityBonus);

            ItemInstance item;
            switch (kind)
            {
                case ItemKind.Weapon:
                    if (db.weapons == null || db.weapons.Length == 0) return null;
                    item = ItemInstance.ForWeapon(rng.Pick(db.weapons), rarity);
                    break;
                case ItemKind.Skill:
                    if (db.skills == null || db.skills.Length == 0) return null;
                    item = ItemInstance.ForSkill(rng.Pick(db.skills), rarity);
                    break;
                case ItemKind.Amulet:
                    if (db.amulets == null || db.amulets.Length == 0) return null;
                    item = ItemInstance.ForGear(rng.Pick(db.amulets), rarity);
                    break;
                default:
                    if (db.outfits == null || db.outfits.Length == 0) return null;
                    item = ItemInstance.ForGear(rng.Pick(db.outfits), rarity);
                    break;
            }

            bool cursed = allowCurse && rarity != ItemRarity.Common && rng.Chance(db.curseChance);
            RollAffixes(db, rng, item, cursed);
            return item;
        }

        /// <summary>Adds the rarity's number of random affixes, plus a curse (and one extra affix) if cursed.</summary>
        public static void RollAffixes(LootDatabase db, SeededRng rng, ItemInstance item, bool cursed)
        {
            item.Affixes.Clear();
            item.Cursed = false;
            if (db.affixes == null || db.affixes.Length == 0) return;

            AffixTarget target = item.Kind == ItemKind.Weapon ? AffixTarget.Weapon
                               : item.Kind == ItemKind.Skill ? AffixTarget.Skill
                               : AffixTarget.Gear;

            var pool = new List<AffixData>();
            var curses = new List<AffixData>();
            foreach (var a in db.affixes)
            {
                if (a == null || (a.targets & target) == 0) continue;
                (a.isCurse ? curses : pool).Add(a);
            }

            int count = Rarity.AffixCount(item.Rarity) + (cursed ? 1 : 0);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                AffixData pick = PickWeighted(pool, rng);
                pool.Remove(pick);   // no duplicates
                item.Affixes.Add(new AffixInstance(pick, RollValue(pick, rng, item.Rarity)));
            }

            if (cursed && curses.Count > 0)
            {
                AffixData curse = PickWeighted(curses, rng);
                item.Affixes.Add(new AffixInstance(curse, RollValue(curse, rng, ItemRarity.Common)));
                item.Cursed = true;
            }
        }

        static AffixData PickWeighted(List<AffixData> list, SeededRng rng)
        {
            int total = 0;
            foreach (var a in list) total += Mathf.Max(1, a.weight);
            int roll = rng.Range(0, total);
            foreach (var a in list)
            {
                roll -= Mathf.Max(1, a.weight);
                if (roll < 0) return a;
            }
            return list[list.Count - 1];
        }

        static float RollValue(AffixData a, SeededRng rng, ItemRarity rarity)
        {
            float t = rng.Value;
            float v = Mathf.Lerp(a.min, a.max, t);
            // higher rarity pushes the roll up a little; curses and chances are not rarity-scaled
            if (!a.isCurse && a.kind == AffixKind.Stat) v *= 1f + 0.12f * (int)rarity;
            if (a.kind == AffixKind.OnHitStatus) v = Mathf.Clamp(v * (1f + 0.1f * (int)rarity), 0f, 0.9f);
            if (!a.IsPercent) v = Mathf.Round(v);
            return v;
        }

        /// <summary>Distinct mutation choices for an altar (excluding ones already owned).</summary>
        public static List<MutationData> RollMutationChoices(LootDatabase db, SeededRng rng, int count, ICollection<MutationData> owned)
        {
            var pool = new List<MutationData>();
            if (db.mutations != null)
                foreach (var m in db.mutations)
                    if (m != null && (owned == null || !owned.Contains(m))) pool.Add(m);

            var picks = new List<MutationData>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var m = rng.Pick(pool);
                pool.Remove(m);
                picks.Add(m);
            }
            return picks;
        }
    }
}
