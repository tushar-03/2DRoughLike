using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Items;

namespace Roughlike.UI
{
    /// <summary>Tells a menu row which item to preview when it gets keyboard/controller focus or the mouse hovers it.</summary>
    sealed class FocusRelay : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        public Action OnFocus;
        public void OnSelect(BaseEventData eventData) => OnFocus?.Invoke();
        public void OnPointerEnter(PointerEventData eventData) => OnFocus?.Invoke();
    }

    /// <summary>
    /// Opens and stacks the full-screen menus: the shop, the mutation altar and the replace-slot dialog. Each pauses the
    /// game, is fully navigable with keyboard, mouse or gamepad (stick/D-pad, South confirms, East or Esc cancels) and
    /// reacts to events raised by the world, so gameplay code never knows the UI exists.
    /// </summary>
    public sealed class ModalManager : MonoBehaviour
    {
        sealed class Window
        {
            public GameObject Root;
            public Action Cancel;
            public Selectable First;
        }

        UiRoot _root;
        PlayerInventory _inventory;
        Transform _player;
        readonly Stack<Window> _stack = new Stack<Window>();

        public bool IsOpen => _stack.Count > 0;
        public int Depth => _stack.Count;

        public static ModalManager Create(UiRoot root, PlayerInventory inventory)
        {
            var go = new GameObject("Modal Manager");
            go.transform.SetParent(root.transform, false);
            var m = go.AddComponent<ModalManager>();
            m._root = root;
            m._inventory = inventory;
            m._player = inventory.transform;
            return m;
        }

        void OnEnable()
        {
            EventBus<OpenShopEvent>.Subscribe(OpenShop);
            EventBus<OpenMutationEvent>.Subscribe(OpenMutation);
            EventBus<ReplaceItemRequest>.Subscribe(OpenReplace);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        }

        void OnRunStarted(RunStartedEvent e) => CloseAll();

        void OnDisable()
        {
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<OpenShopEvent>.Unsubscribe(OpenShop);
            EventBus<OpenMutationEvent>.Unsubscribe(OpenMutation);
            EventBus<ReplaceItemRequest>.Unsubscribe(OpenReplace);
            while (_stack.Count > 0) Pop();
        }

        void Update()
        {
            if (_stack.Count == 0) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool cancel = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (cancel) _stack.Peek().Cancel?.Invoke();

            // keep something selected so the controller always has focus (a mouse click on empty space clears it)
            if (_stack.Count > 0 && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(_stack.Peek().First != null ? _stack.Peek().First.gameObject : null);
        }

        // =====================================================================
        // Stack
        // =====================================================================

        Window Push(string title, Vector2 size, Action cancel, out RectTransform content)
        {
            if (_stack.Count > 0) _stack.Peek().Root.SetActive(false);

            var dim = UiKit.Image(_root.ModalLayer, "Modal", new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);

            RectTransform inner = UiKit.Panel(dim.transform, "Window", size);
            UiKit.Place((RectTransform)inner.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            var t = UiKit.Label(inner, "Title", title, 36, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-30f, 52f));

            var w = new Window { Root = dim.gameObject, Cancel = cancel };
            _stack.Push(w);
            PauseGate.Push();
            content = inner;
            return w;
        }

        void Focus(Window w, Selectable first)
        {
            w.First = first;
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            Window w = _stack.Pop();
            Destroy(w.Root);
            PauseGate.Pop();
            if (_stack.Count > 0)
            {
                Window prev = _stack.Peek();
                prev.Root.SetActive(true);
                if (EventSystem.current != null && prev.First != null) EventSystem.current.SetSelectedGameObject(prev.First.gameObject);
            }
        }

        /// <summary>Closes every open menu (end of run, scene change).</summary>
        public void CloseAll()
        {
            while (_stack.Count > 0) Pop();
        }

        // =====================================================================
        // Shop
        // =====================================================================

        sealed class ShopRow
        {
            public ShopOffer Offer;
            public Button Button;
            public Text Title, Price;
        }

        void OpenShop(OpenShopEvent e)
        {
            var closed = e.Closed;
            Window w = null;
            w = Push("Salvage Merchant", new Vector2(1180f, 760f), () => { Pop(); closed?.Invoke(); }, out RectTransform content);

            var gold = UiKit.Label(content, "Gold", "", 30, UiKit.Gold, TextAnchor.MiddleRight, true);
            UiKit.Place(gold.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(300f, 40f));

            var list = UiKit.Node("Offers", content);
            UiKit.Place(list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -80f), new Vector2(640f, 600f));

            var card = ItemCard.Create(content, new Vector2(440f, 420f));
            UiKit.Place((RectTransform)card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(440f, 420f));
            card.ShowText("Wares", UiKit.Muted, "Select an item to see its details.\n\nGold is spent on the spot. Upgrades make a weapon stronger for good, this run.");

            var rows = new List<ShopRow>();
            float y = 0f;
            foreach (var offer in e.Stock.Offers)
            {
                var row = new ShopRow { Offer = offer };
                row.Button = UiKit.Button(list, "Offer", new Vector2(640f, 62f), null);
                UiKit.Place((RectTransform)row.Button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(640f, 62f));
                y -= 70f;

                row.Title = UiKit.Label(row.Button.transform, "Title", "", 26, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Place(row.Title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                row.Title.rectTransform.offsetMin = new Vector2(16f, 0f);
                row.Title.rectTransform.offsetMax = new Vector2(-130f, 0f);
                row.Price = UiKit.Label(row.Button.transform, "Price", "", 26, UiKit.Gold, TextAnchor.MiddleRight, true);
                UiKit.Place(row.Price.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(120f, 0f));

                var captured = row;
                var relay = row.Button.gameObject.AddComponent<FocusRelay>();
                relay.OnFocus = () => ShowOffer(card, captured.Offer, e.Inventory);
                row.Button.onClick.AddListener(() =>
                {
                    var result = e.Stock.TryBuy(captured.Offer, e.Inventory, _player.position, r => RefreshShop(rows, gold, e.Inventory));
                    if (result == BuyResult.NotEnoughGold)
                        EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Not enough gold", Colour = UiKit.Danger });
                    else if (result == BuyResult.NotAvailable)
                        EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Nothing to do here", Colour = UiKit.Muted });
                    RefreshShop(rows, gold, e.Inventory);
                });
                rows.Add(row);
            }

            var close = UiKit.Button(content, "Close", new Vector2(220f, 56f), () => { Pop(); closed?.Invoke(); });
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(220f, 56f));
            var closeText = UiKit.Label(close.transform, "Text", "Leave  (Esc / B)", 24, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(closeText.rectTransform);

            RefreshShop(rows, gold, e.Inventory);
            Focus(w, rows.Count > 0 ? rows[0].Button : close);
            if (rows.Count > 0) ShowOffer(card, rows[0].Offer, e.Inventory);
        }

        static void RefreshShop(List<ShopRow> rows, Text gold, PlayerInventory inv)
        {
            gold.text = "Gold  " + inv.Gold;
            foreach (var r in rows)
            {
                var o = r.Offer;
                int price = o.PriceFor(inv);
                bool soldOut = o.Sold;
                bool unavailable = (o.Kind == OfferKind.FlaskRefill && inv.FlaskCharges >= inv.MaxFlaskCharges)
                                || (o.Kind == OfferKind.UpgradeSlot && (inv.Weapons[o.Slot] == null || !inv.Weapons[o.Slot].CanUpgrade));
                bool afford = inv.Gold >= price;

                r.Title.text = o.Title(inv);
                r.Title.color = soldOut || unavailable ? UiKit.Muted : o.TitleColour;
                r.Price.text = soldOut ? "SOLD" : unavailable ? "-" : price.ToString();
                r.Price.color = soldOut || unavailable ? UiKit.Muted : (afford ? UiKit.Gold : UiKit.Danger);
            }
        }

        static void ShowOffer(ItemCard card, ShopOffer offer, PlayerInventory inv)
        {
            switch (offer.Kind)
            {
                case OfferKind.Item:
                    card.Show(offer.Item);
                    break;
                case OfferKind.FlaskRefill:
                    card.ShowText("Refill flask", new Color(0.5f, 1f, 0.6f), $"Restores your flask to full ({inv.FlaskCharges}/{inv.MaxFlaskCharges}).\nEach charge heals 45% of your health.");
                    break;
                case OfferKind.Scroll:
                    card.ShowText(offer.Colour + " scroll", StatNames.Colour(offer.Colour),
                        $"+1 {offer.Colour} level: more damage for every {offer.Colour} weapon and skill, and +{PlayerStats.HealthPerScroll:0} max health.");
                    break;
                case OfferKind.UpgradeSlot:
                    var w = inv.Weapons[offer.Slot];
                    card.ShowText(offer.Title(inv), Color.white, w == null
                        ? "Nothing equipped in this slot."
                        : (w.CanUpgrade ? $"Raises {w.DisplayName} to tier {w.Tier + 1}: +18% base damage per tier." : "Already at the highest tier."));
                    break;
            }
        }

        // =====================================================================
        // Mutation altar
        // =====================================================================

        void OpenMutation(OpenMutationEvent e)
        {
            Window w = null;
            w = Push("Choose a Mutation", new Vector2(900f, 620f), () => { Pop(); e.Resolve(-1); }, out RectTransform content);

            var hint = UiKit.Label(content, "Hint", $"A permanent perk for this run  ({e.Inventory.Mutations.Count}/{PlayerInventory.MaxMutations} slots used)", 22, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-40f, 32f));

            Selectable first = null;
            for (int i = 0; i < e.Choices.Count; i++)
            {
                int index = i;
                MutationData m = e.Choices[i];
                var b = UiKit.Button(content, "Choice", new Vector2(820f, 110f), () => { Pop(); e.Resolve(index); });
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f - i * 124f), new Vector2(820f, 110f));

                var swatch = UiKit.Image(b.transform, "Swatch", m.tint);
                UiKit.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 80f));
                var name = UiKit.Label(b.transform, "Name", m.displayName, 32, m.tint, TextAnchor.UpperLeft, true);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                name.rectTransform.offsetMin = new Vector2(52f, 12f);
                name.rectTransform.offsetMax = new Vector2(-16f, -10f);
                var desc = UiKit.Label(b.transform, "Desc", m.description, 22, Color.white, TextAnchor.LowerLeft);
                UiKit.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                desc.rectTransform.offsetMin = new Vector2(52f, 10f);
                desc.rectTransform.offsetMax = new Vector2(-16f, -48f);
                if (first == null) first = b;
            }

            var cancel = UiKit.Button(content, "Cancel", new Vector2(240f, 52f), () => { Pop(); e.Resolve(-1); });
            UiKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 52f));
            var ct = UiKit.Label(cancel.transform, "Text", "Not now  (Esc / B)", 22, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(ct.rectTransform);

            Focus(w, first != null ? first : cancel);
        }

        // =====================================================================
        // Replace which slot?
        // =====================================================================

        void OpenReplace(ReplaceItemRequest r)
        {
            Window w = null;
            w = Push("Replace which slot?", new Vector2(1100f, 560f), () => { Pop(); r.Resolve(-1); }, out RectTransform content);

            var card = ItemCard.Create(content, new Vector2(420f, 360f));
            UiKit.Place((RectTransform)card.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(420f, 360f));
            card.Show(r.Item);

            ItemInstance[] current = r.Item.Kind == ItemKind.Weapon ? r.Inventory.Weapons : r.Inventory.Skills;
            string[] keys = r.Item.Kind == ItemKind.Weapon ? new[] { "J", "K" } : new[] { "U", "I" };

            Selectable first = null;
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var b = UiKit.Button(content, "Slot", new Vector2(560f, 96f), () => { Pop(); r.Resolve(slot); });
                UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -90f - i * 112f), new Vector2(560f, 96f));

                ItemInstance item = current[i];
                var edge = UiKit.Image(b.transform, "Edge", item != null ? item.RarityColour : UiKit.Muted);
                UiKit.Place(edge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));
                var label = UiKit.Label(b.transform, "Label", $"Slot {i + 1}  [{keys[i]}]", 20, UiKit.Muted, TextAnchor.UpperLeft);
                UiKit.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                label.rectTransform.offsetMin = new Vector2(24f, 8f); label.rectTransform.offsetMax = new Vector2(-12f, -8f);
                var name = UiKit.Label(b.transform, "Name", item != null ? item.DisplayName : "(empty)", 30, item != null ? item.RarityColour : UiKit.Muted, TextAnchor.LowerLeft, true);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                name.rectTransform.offsetMin = new Vector2(24f, 10f); name.rectTransform.offsetMax = new Vector2(-12f, -8f);
                if (first == null) first = b;
            }

            var cancel = UiKit.Button(content, "Cancel", new Vector2(300f, 56f), () => { Pop(); r.Resolve(-1); });
            UiKit.Place((RectTransform)cancel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(300f, 56f));
            var ct = UiKit.Label(cancel.transform, "Text", "Keep what I have  (Esc / B)", 22, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(ct.rectTransform);

            Focus(w, first);
        }
    }
}
