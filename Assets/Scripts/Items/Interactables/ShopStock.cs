using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Items
{
    public enum OfferKind { Item, FlaskRefill, UpgradeSlot, Scroll }

    public enum BuyResult { Bought, NotEnoughGold, SoldOut, NotAvailable }

    /// <summary>One thing for sale. Items and scrolls sell once; flask refills and upgrades can be bought repeatedly while useful.</summary>
    public sealed class ShopOffer
    {
        public OfferKind Kind;
        public ItemInstance Item;
        public int Slot;
        public StatColor Colour;
        public int FixedPrice;
        public bool Sold;

        public int PriceFor(PlayerInventory inv)
        {
            switch (Kind)
            {
                case OfferKind.Item: return Item.Price;
                case OfferKind.UpgradeSlot: return ItemPricing.UpgradePrice(inv.Weapons[Slot]);
                default: return FixedPrice;
            }
        }

        public string Title(PlayerInventory inv)
        {
            switch (Kind)
            {
                case OfferKind.Item: return Item.DisplayName;
                case OfferKind.FlaskRefill: return "Refill flask";
                case OfferKind.Scroll: return Colour + " scroll";
                case OfferKind.UpgradeSlot:
                    var w = inv.Weapons[Slot];
                    return w == null ? $"Upgrade slot {Slot + 1} (empty)" : $"Upgrade {w.DisplayName} to tier {w.Tier + 1}";
                default: return "?";
            }
        }

        public Color TitleColour => Kind == OfferKind.Item ? Item.RarityColour
            : Kind == OfferKind.Scroll ? StatNames.Colour(Colour) : Color.white;
    }

    /// <summary>
    /// A shop's inventory and the buying rules. Generated from the run seed, so the same seed has the same shops.
    /// Pure logic: the UI only displays offers and calls <see cref="TryBuy"/>.
    /// </summary>
    public sealed class ShopStock
    {
        public readonly List<ShopOffer> Offers = new List<ShopOffer>();

        public static ShopStock Generate(LootDatabase db, SeededRng rng, float progress)
        {
            var stock = new ShopStock();

            // three items: a weapon, a skill and a piece of gear
            AddItem(stock, LootRoller.Roll(db, rng, progress, ItemKind.Weapon, 0.15f));
            AddItem(stock, LootRoller.Roll(db, rng, progress, ItemKind.Skill, 0.15f));
            AddItem(stock, LootRoller.Roll(db, rng, progress, rng.Chance(0.5f) ? ItemKind.Amulet : ItemKind.Outfit, 0.15f));

            stock.Offers.Add(new ShopOffer { Kind = OfferKind.Scroll, Colour = LootRoller.RollScrollColour(rng), FixedPrice = ItemPricing.ScrollPrice });
            stock.Offers.Add(new ShopOffer { Kind = OfferKind.FlaskRefill, FixedPrice = ItemPricing.FlaskRefillPrice });
            stock.Offers.Add(new ShopOffer { Kind = OfferKind.UpgradeSlot, Slot = 0 });
            stock.Offers.Add(new ShopOffer { Kind = OfferKind.UpgradeSlot, Slot = 1 });
            return stock;
        }

        static void AddItem(ShopStock stock, ItemInstance item)
        {
            if (item != null) stock.Offers.Add(new ShopOffer { Kind = OfferKind.Item, Item = item });
        }

        /// <summary>
        /// Buys an offer. Items go through the normal pick-up flow (which may ask which slot to replace); if the
        /// player cancels that, the gold is refunded and the item stays for sale.
        /// </summary>
        public BuyResult TryBuy(ShopOffer offer, PlayerInventory inv, Vector2 dropAt, Action<BuyResult> onFinished = null)
        {
            BuyResult Finish(BuyResult r) { onFinished?.Invoke(r); return r; }

            if (offer.Sold) return Finish(BuyResult.SoldOut);

            switch (offer.Kind)
            {
                case OfferKind.UpgradeSlot:
                {
                    ItemInstance w = inv.Weapons[offer.Slot];
                    if (w == null || !w.CanUpgrade) return Finish(BuyResult.NotAvailable);
                    if (!inv.TrySpend(offer.PriceFor(inv))) return Finish(BuyResult.NotEnoughGold);
                    w.Upgrade();
                    inv.NotifyChanged();
                    return Finish(BuyResult.Bought);
                }

                case OfferKind.FlaskRefill:
                    if (inv.FlaskCharges >= inv.MaxFlaskCharges) return Finish(BuyResult.NotAvailable);
                    if (!inv.TrySpend(offer.FixedPrice)) return Finish(BuyResult.NotEnoughGold);
                    inv.RefillFlask();
                    return Finish(BuyResult.Bought);

                case OfferKind.Scroll:
                    if (!inv.TrySpend(offer.FixedPrice)) return Finish(BuyResult.NotEnoughGold);
                    inv.AddScroll(offer.Colour);
                    offer.Sold = true;
                    return Finish(BuyResult.Bought);

                case OfferKind.Item:
                {
                    int price = offer.Item.Price;
                    if (!inv.TrySpend(price)) return Finish(BuyResult.NotEnoughGold);
                    offer.Sold = true;
                    inv.PickUp(offer.Item, dropAt, accepted =>
                    {
                        if (!accepted)
                        {
                            offer.Sold = false;
                            inv.AddGold(price);   // cancelled the slot choice: refund
                        }
                        onFinished?.Invoke(accepted ? BuyResult.Bought : BuyResult.NotAvailable);
                    });
                    return BuyResult.Bought;
                }
            }
            return Finish(BuyResult.NotAvailable);
        }
    }
}
