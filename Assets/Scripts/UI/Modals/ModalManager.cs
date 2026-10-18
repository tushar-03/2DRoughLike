using Roughlike.Player;
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
    public sealed partial class ModalManager : MonoBehaviour
    {
        sealed class Window
        {
            public GameObject Root;
            public Action Cancel;
            public Selectable First;
            public bool Pauses;
        }

        UiRoot _root;
        PlayerInventory _inventory;
        Transform _player;
        readonly Stack<Window> _stack = new Stack<Window>();

        public bool IsOpen => _stack.Count > 0;
        public int Depth => _stack.Count;
        /// <summary>When the last menu closed (so the same Esc press does not immediately reopen the pause menu).</summary>
        public float LastEmptiedAt { get; private set; } = -10f;

        bool _pauseGame = true;

        /// <summary>
        /// Creates the menu stack. In gameplay <paramref name="inventory"/> is the player's (for shops) and menus pause the
        /// game; the main menu passes null and false.
        /// </summary>
        public static ModalManager Create(UiRoot root, PlayerInventory inventory, bool pauseGame = true)
        {
            var go = new GameObject("Modal Manager");
            go.transform.SetParent(root.transform, false);
            var m = go.AddComponent<ModalManager>();
            m._root = root;
            m._inventory = inventory;
            m._player = inventory != null ? inventory.transform : null;
            m._pauseGame = pauseGame;
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
            if (cancel && !ControlsView.IsRebinding && Time.unscaledTime - _openedAt > 0.15f) _stack.Peek().Cancel?.Invoke();

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
            Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.UiOpen, 0.6f);
            var sounds = _root.GetComponent<UiSounds>();
            if (sounds != null) sounds.Quiet();

            var dim = UiKit.Image(_root.ModalLayer, "Modal", new Color(0f, 0f, 0f, _pauseGame ? 0.72f : 0.9f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);

            RectTransform inner = UiKit.Panel(dim.transform, "Window", size);
            UiKit.Place((RectTransform)inner.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            var t = UiKit.Label(inner, "Title", title, 36, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-30f, 52f));

            var w = new Window { Root = dim.gameObject, Cancel = cancel, Pauses = _pauseGame };
            _stack.Push(w);
            if (w.Pauses) PauseGate.Push();
            if (_stack.Count == 1 && EventSystem.current != null) _backgroundSelection = EventSystem.current.currentSelectedGameObject;
            SetBackgroundInteractive(false);
            _openedAt = Time.unscaledTime;   // the key press that opened a menu must not also close it
            content = inner;
            return w;
        }

        float _openedAt = -1f;
        GameObject _backgroundSelection;

        /// <summary>
        /// While a menu is open the screen behind it (the main menu's buttons, the HUD) must not be reachable: automatic
        /// navigation with a controller or the arrow keys would otherwise jump to the nearest button, which could be behind the
        /// window. A non-interactable canvas group takes its buttons out of navigation.
        /// </summary>
        void SetBackgroundInteractive(bool on)
        {
            if (_root == null || _root.HudLayer == null) return;
            if (!_root.HudLayer.TryGetComponent<CanvasGroup>(out var g)) g = _root.HudLayer.gameObject.AddComponent<CanvasGroup>();
            g.interactable = on;
        }

        void Focus(Window w, Selectable first)
        {
            MenuNav.AutoLink(w.Root.transform);
            w.First = first;
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            Window w = _stack.Pop();
            Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.UiBack, 0.6f);
            w.Root.SetActive(false);   // gone at once; destroyed at the end of the frame
            Destroy(w.Root);
            if (w.Pauses) PauseGate.Pop();
            InputGate.Block(3);
            if (_stack.Count == 0)
            {
                LastEmptiedAt = Time.unscaledTime;
                SetBackgroundInteractive(true);
                // hand the controller back to whatever was selected before the first menu opened
                if (EventSystem.current != null && _backgroundSelection != null) EventSystem.current.SetSelectedGameObject(_backgroundSelection);
                _backgroundSelection = null;
            }
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
            public Image Icon;
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

                row.Icon = UiKit.Image(row.Button.transform, "Icon", Color.white);
                UiKit.Place(row.Icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(46f, 46f));
                row.Icon.raycastTarget = false;
                row.Title = UiKit.Label(row.Button.transform, "Title", "", 26, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Place(row.Title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                row.Title.rectTransform.offsetMin = new Vector2(72f, 0f);
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
            var closeText = UiKit.Label(close.transform, "Text", $"Leave  ({InputDevices.BackName})", 24, Color.white, TextAnchor.MiddleCenter, true);
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

                ItemInstance shown = o.Kind == OfferKind.Item ? o.Item : o.Kind == OfferKind.UpgradeSlot ? inv.Weapons[o.Slot] : null;
                r.Icon.enabled = shown != null;
                if (shown != null) UiKit.FillIcon(r.Icon, shown);
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

            bool full = e.Inventory.Mutations.Count >= PlayerInventory.MaxMutations;
            var hint = UiKit.Label(content, "Hint", full
                ? "All slots are full: choose one, then pick which mutation it replaces"
                : $"A permanent perk for this run  ({e.Inventory.Mutations.Count}/{PlayerInventory.MaxMutations} slots used)", 22, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-40f, 32f));

            Selectable first = null;
            for (int i = 0; i < e.Choices.Count; i++)
            {
                int index = i;
                MutationData m = e.Choices[i];
                var b = UiKit.Button(content, "Choice", new Vector2(820f, 110f), () =>
                {
                    Pop();
                    if (full && e.ResolveReplace != null) OpenReplaceMutation(e, index);
                    else e.Resolve(index);
                });
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f - i * 124f), new Vector2(820f, 110f));

                var swatch = UiKit.Image(b.transform, "Swatch", m.tint);
                UiKit.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 80f));
                var mutIcon = Roughlike.Utils.GameIcons.Sprite("mut_" + m.name);
                if (mutIcon != null)
                {
                    swatch.sprite = mutIcon;
                    swatch.color = Color.white;
                    swatch.preserveAspect = true;
                    UiKit.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(88f, 88f));
                }
                var name = UiKit.Label(b.transform, "Name", m.displayName, 32, m.tint, TextAnchor.UpperLeft, true);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                name.rectTransform.offsetMin = new Vector2(112f, 12f);
                name.rectTransform.offsetMax = new Vector2(-16f, -10f);
                var desc = UiKit.Label(b.transform, "Desc", m.description, 22, Color.white, TextAnchor.LowerLeft);
                UiKit.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                desc.rectTransform.offsetMin = new Vector2(112f, 10f);
                desc.rectTransform.offsetMax = new Vector2(-16f, -48f);
                if (first == null) first = b;
            }

            var cancel = UiKit.Button(content, "Cancel", new Vector2(240f, 52f), () => { Pop(); e.Resolve(-1); });
            UiKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 52f));
            var ct = UiKit.Label(cancel.transform, "Text", $"Not now  ({InputDevices.BackName})", 22, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(ct.rectTransform);

            Focus(w, first != null ? first : cancel);
        }

        /// <summary>With every mutation slot full, asks which one the new mutation takes the place of.</summary>
        void OpenReplaceMutation(OpenMutationEvent e, int choice)
        {
            MutationData incoming = e.Choices[choice];
            Window w = null;
            w = Push("Replace which mutation?", new Vector2(900f, 620f), () => { Pop(); e.ResolveReplace(-1, -1); }, out RectTransform content);

            var hint = UiKit.Label(content, "Hint", $"{incoming.displayName} takes the place of one of yours for the rest of the run", 22, incoming.tint, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(-40f, 32f));

            Selectable first = null;
            for (int i = 0; i < e.Inventory.Mutations.Count; i++)
            {
                int slot = i;
                MutationData m = e.Inventory.Mutations[i];
                var b = UiKit.Button(content, "Slot", new Vector2(820f, 110f), () => { Pop(); e.ResolveReplace(choice, slot); });
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f - i * 124f), new Vector2(820f, 110f));
                var icon = UiKit.Image(b.transform, "Icon", m.tint);
                icon.preserveAspect = true;
                var art = Roughlike.Utils.GameIcons.Sprite("mut_" + m.name);
                if (art != null) { icon.sprite = art; icon.color = Color.white; }
                UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(88f, 88f));
                var name = UiKit.Label(b.transform, "Name", m.displayName, 32, m.tint, TextAnchor.UpperLeft, true);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                name.rectTransform.offsetMin = new Vector2(112f, 12f);
                name.rectTransform.offsetMax = new Vector2(-16f, -10f);
                var desc = UiKit.Label(b.transform, "Desc", m.description, 22, Color.white, TextAnchor.LowerLeft);
                UiKit.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                desc.rectTransform.offsetMin = new Vector2(112f, 10f);
                desc.rectTransform.offsetMax = new Vector2(-16f, -48f);
                if (first == null) first = b;
            }

            var cancel = UiKit.Button(content, "Cancel", new Vector2(240f, 52f), () => { Pop(); e.ResolveReplace(-1, -1); });
            UiKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 52f));
            var ct = UiKit.Label(cancel.transform, "Text", $"Keep mine  ({InputDevices.BackName})", 22, Color.white, TextAnchor.MiddleCenter, true);
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
            var ct = UiKit.Label(cancel.transform, "Text", $"Keep what I have  ({InputDevices.BackName})", 22, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(ct.rectTransform);

            Focus(w, first);
        }
    }
}

