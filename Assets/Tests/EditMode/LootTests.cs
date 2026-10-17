using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;

namespace Roughlike.Tests.EditMode
{
    /// <summary>Pure-logic tests for rarity, affixes, loot rolling, pricing and shop stock. Everything is seeded.</summary>
    public class LootTests
    {
        LootDatabase _db;

        [SetUp]
        public void SetUp() => _db = DefaultContent.CreateDatabase();

        [Test]
        public void SameSeedRollsTheSameItems()
        {
            string Describe(ulong seed)
            {
                var rng = new SeededRng(seed);
                var parts = new List<string>();
                for (int i = 0; i < 30; i++)
                {
                    var it = LootRoller.Roll(_db, rng, 0.5f);
                    parts.Add($"{it.DisplayName}|{it.Rarity}|{string.Join(",", it.Affixes.ConvertAll(a => a.data.id + a.value.ToString("0.000")))}");
                }
                return string.Join(";", parts);
            }
            Assert.AreEqual(Describe(42), Describe(42));
            Assert.AreNotEqual(Describe(42), Describe(43));
        }

        [Test]
        public void RarityImprovesWithProgressAndChestBonus()
        {
            int Count(float progress, float bonus, ItemRarity atLeast)
            {
                var rng = new SeededRng(7);
                int n = 0;
                for (int i = 0; i < 3000; i++) if (LootRoller.RollRarity(rng, progress, bonus) >= atLeast) n++;
                return n;
            }

            Assert.Greater(Count(1f, 0f, ItemRarity.Epic), Count(0f, 0f, ItemRarity.Epic) * 1.5f, "later rooms drop better loot");
            Assert.Greater(Count(0.5f, 1f, ItemRarity.Rare), 2990, "a max-bonus chest never gives common");
            Assert.Greater(Count(0f, 0f, ItemRarity.Rare), 0);
            Assert.Less(Count(0f, 0f, ItemRarity.Legendary), Count(1f, 0f, ItemRarity.Legendary));
        }

        [Test]
        public void AffixCountFollowsRarityAndNeverDuplicates()
        {
            var rng = new SeededRng(99);
            for (int i = 0; i < 400; i++)
            {
                var it = LootRoller.Roll(_db, rng, 0.6f, allowCurse: false);
                Assert.AreEqual(Rarity.AffixCount(it.Rarity), it.Affixes.Count, it.Rarity.ToString());
                var seen = new HashSet<AffixData>();
                foreach (var a in it.Affixes) Assert.IsTrue(seen.Add(a.data), "duplicate affix");
            }
        }

        [Test]
        public void AffixesOnlyRollOnMatchingItemKinds()
        {
            var rng = new SeededRng(5);
            for (int i = 0; i < 400; i++)
            {
                var it = LootRoller.Roll(_db, rng, 1f, null, 0.5f);
                AffixTarget need = it.Kind == ItemKind.Weapon ? AffixTarget.Weapon : it.Kind == ItemKind.Skill ? AffixTarget.Skill : AffixTarget.Gear;
                foreach (var a in it.Affixes) Assert.AreNotEqual(AffixTarget.None, a.data.targets & need, $"{a.data.id} on {it.Kind}");
            }
        }

        [Test]
        public void CursedItemsCarryACurseButArePowerful()
        {
            var rng = new SeededRng(1);
            _db.curseChance = 1f;
            int cursed = 0;
            for (int i = 0; i < 200; i++)
            {
                var it = LootRoller.Roll(_db, rng, 0.8f, ItemKind.Weapon, 0.5f);
                if (!it.Cursed) continue;
                cursed++;
                Assert.IsTrue(it.Affixes.Exists(a => a.data.isCurse), "cursed item must have a curse affix");
                // a cursed item gets one extra good affix on top of its rarity's, plus the curse itself
                Assert.AreEqual(Rarity.AffixCount(it.Rarity) + 2, it.Affixes.Count);
                Assert.Greater(it.CurseBonus, 1f);
                StringAssert.StartsWith("Cursed", it.DisplayName);
            }
            Assert.Greater(cursed, 100);

            _db.curseChance = 0f;
            for (int i = 0; i < 100; i++) Assert.IsFalse(LootRoller.Roll(_db, rng, 0.8f).Cursed);

            // common items are never cursed
            _db.curseChance = 1f;
            var common = ItemInstance.ForWeapon(_db.weapons[0]);
            LootRoller.RollAffixes(_db, new SeededRng(2), common, false);
            Assert.IsFalse(common.Cursed);
        }

        [Test]
        public void TiersScaleAndCapOut()
        {
            var it = ItemInstance.ForWeapon(_db.weapons[1]);
            float p1 = it.Power;
            Assert.IsTrue(it.Upgrade());
            Assert.AreEqual(p1 * 1.18f, it.Power, 0.001f);
            it.Upgrade(); it.Upgrade();
            Assert.AreEqual(ItemInstance.MaxTier, it.Tier);
            Assert.IsFalse(it.Upgrade());
            Assert.IsFalse(it.CanUpgrade);
        }

        [Test]
        public void PricesRiseWithRarityAndTierAndCursesDiscount()
        {
            var w = _db.weapons[1];
            var common = ItemInstance.ForWeapon(w);
            var rare = ItemInstance.ForWeapon(w, ItemRarity.Rare);
            var legendary = ItemInstance.ForWeapon(w, ItemRarity.Legendary);
            Assert.Less(common.Price, rare.Price);
            Assert.Less(rare.Price, legendary.Price);

            var tier2 = ItemInstance.ForWeapon(w, ItemRarity.Common, 2);
            Assert.Greater(tier2.Price, common.Price);

            var cursed = ItemInstance.ForWeapon(w, ItemRarity.Rare);
            cursed.Cursed = true;
            Assert.Less(cursed.Price, rare.Price);
            Assert.Greater(ItemPricing.UpgradePrice(tier2), ItemPricing.UpgradePrice(common));
        }

        [Test]
        public void GearModifiersScaleWithRarityAndIncludeAffixes()
        {
            var coin = _db.amulets[0];
            var common = ItemInstance.ForGear(coin);
            var epic = ItemInstance.ForGear(coin, ItemRarity.Epic);
            epic.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "g_health"), 20f));

            var s = new StatSheet();
            s.AddRange(new List<StatModifier>(common.GearModifiers(this)));
            float commonGold = s.Get(StatKind.GoldFind);
            s.ClearModifiers();
            s.AddRange(new List<StatModifier>(epic.GearModifiers(this)));
            Assert.Greater(s.Get(StatKind.GoldFind), commonGold);
            Assert.AreEqual(120f, s.Get(StatKind.MaxHealth), 0.01f, "affix flat health applies");
        }

        [Test]
        public void ItemCardsDescribeAffixesInReadableForm()
        {
            var it = ItemInstance.ForWeapon(_db.weapons[1], ItemRarity.Rare);
            it.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "w_damage"), 0.12f));
            it.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "w_burn"), 0.25f));
            it.Affixes.Add(new AffixInstance(System.Array.Find(_db.affixes, a => a.id == "w_crit"), 0.06f));
            var lines = it.DescribeLines();
            CollectionAssert.Contains(lines, "+12% damage");
            CollectionAssert.Contains(lines, "25% chance to burn");
            CollectionAssert.Contains(lines, "+6% crit chance");
            Assert.IsTrue(lines.Exists(l => l.Contains("Ember")), "colour affinity is shown");
        }

        [Test]
        public void MutationChoicesAreDistinctSeededAndSkipOwned()
        {
            var a = LootRoller.RollMutationChoices(_db, new SeededRng(3), 3, null);
            var b = LootRoller.RollMutationChoices(_db, new SeededRng(3), 3, null);
            Assert.AreEqual(3, a.Count);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AllItemsAreUnique(a);

            var owned = new List<MutationData> { a[0], a[1] };
            var c = LootRoller.RollMutationChoices(_db, new SeededRng(3), 3, owned);
            foreach (var m in c) CollectionAssert.DoesNotContain(owned, m);
        }

        [Test]
        public void ShopStockIsDeterministicAndWellFormed()
        {
            ShopStock Gen(ulong seed) => ShopStock.Generate(_db, new SeededRng(seed), 0.5f);
            var s1 = Gen(11);
            var s2 = Gen(11);
            Assert.AreEqual(s1.Offers.Count, s2.Offers.Count);
            for (int i = 0; i < s1.Offers.Count; i++)
            {
                Assert.AreEqual(s1.Offers[i].Kind, s2.Offers[i].Kind);
                if (s1.Offers[i].Item != null) Assert.AreEqual(s1.Offers[i].Item.DisplayName, s2.Offers[i].Item.DisplayName);
            }

            int items = s1.Offers.FindAll(o => o.Kind == OfferKind.Item).Count;
            Assert.AreEqual(3, items);
            Assert.IsTrue(s1.Offers.Exists(o => o.Kind == OfferKind.FlaskRefill));
            Assert.IsTrue(s1.Offers.Exists(o => o.Kind == OfferKind.Scroll));
            Assert.AreEqual(2, s1.Offers.FindAll(o => o.Kind == OfferKind.UpgradeSlot).Count);
        }
    }
}
