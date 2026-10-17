using System;
using System.Collections.Generic;
using Roughlike.Combat;

namespace Roughlike.Items
{
    [Serializable]
    public class AffixSave
    {
        public string id;
        public float value;
    }

    /// <summary>
    /// A saved item. References base items and affixes by asset name so saves survive rebalancing.
    /// <c>kind = -1</c> means an empty slot (JsonUtility cannot store nulls in arrays).
    /// </summary>
    [Serializable]
    public class ItemSave
    {
        public int kind = -1;
        public string baseName;
        public int rarity;
        public int tier = 1;
        public bool cursed;
        public List<AffixSave> affixes = new List<AffixSave>();

        public static ItemSave Empty => new ItemSave();

        public static ItemSave From(ItemInstance item)
        {
            if (item == null) return Empty;
            var s = new ItemSave
            {
                kind = (int)item.Kind,
                rarity = (int)item.Rarity,
                tier = item.Tier,
                cursed = item.Cursed,
                baseName = item.Kind == ItemKind.Weapon ? item.Weapon.name
                         : item.Kind == ItemKind.Skill ? item.Skill.name : item.Gear.name
            };
            foreach (var a in item.Affixes) s.affixes.Add(new AffixSave { id = a.data.id, value = a.value });
            return s;
        }

        /// <summary>Rebuilds the item from the database. Returns null for an empty slot or if the base item no longer exists.</summary>
        public ItemInstance ToInstance(LootDatabase db)
        {
            if (kind < 0 || db == null) return null;

            ItemInstance item = null;
            var k = (ItemKind)kind;
            var r = (ItemRarity)Math.Max(0, Math.Min(3, rarity));
            switch (k)
            {
                case ItemKind.Weapon:
                    var w = Array.Find(db.weapons, x => x != null && x.name == baseName);
                    if (w != null) item = ItemInstance.ForWeapon(w, r, tier);
                    break;
                case ItemKind.Skill:
                    var sk = Array.Find(db.skills, x => x != null && x.name == baseName);
                    if (sk != null) item = ItemInstance.ForSkill(sk, r, tier);
                    break;
                case ItemKind.Amulet:
                    var am = Array.Find(db.amulets, x => x != null && x.name == baseName);
                    if (am != null) item = ItemInstance.ForGear(am, r, tier);
                    break;
                default:
                    var ou = Array.Find(db.outfits, x => x != null && x.name == baseName);
                    if (ou != null) item = ItemInstance.ForGear(ou, r, tier);
                    break;
            }
            if (item == null) return null;

            item.Cursed = cursed;
            foreach (var a in affixes)
            {
                var data = Array.Find(db.affixes, x => x != null && x.id == a.id);
                if (data != null) item.Affixes.Add(new AffixInstance(data, a.value));
            }
            return item;
        }
    }
}
