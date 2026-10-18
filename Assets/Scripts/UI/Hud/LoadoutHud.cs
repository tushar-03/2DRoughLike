using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>
    /// The loadout picker shown in the practice room: a row of cards across the top (click, tap, number keys or shoulder buttons
    /// to equip one) and a prompt about the portal.
    /// </summary>
    public sealed class LoadoutHud : MonoBehaviour
    {
        sealed class Card
        {
            public Button Button;
            public Image Edge, Icon, Icon2;
            public Text Name, Detail;
        }

        LoadoutDirector _director;
        readonly List<Card> _cards = new List<Card>();
        Text _hint;

        public static LoadoutHud Create(UiRoot root, LoadoutDirector director)
        {
            var go = new GameObject("Loadout HUD");
            go.transform.SetParent(root.HudLayer, false);
            var h = go.AddComponent<LoadoutHud>();
            h._director = director;
            h.Build();
            return h;
        }

        void Build()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(rt);

            var title = UiKit.Label(rt, "Title", "CHOOSE YOUR LOADOUT", 40, new Color(1f, 0.88f, 0.55f), TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1200f, 56f));

            var opts = _director.Options;
            float w = 360f, gap = 12f;
            float total = opts.Count * w + (opts.Count - 1) * gap;
            for (int i = 0; i < opts.Count; i++)
            {
                int index = i;
                var card = new Card();
                var b = UiKit.Button(rt, "Card " + i, new Vector2(w, 160f), () => _director.Select(index));
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), -108f), new Vector2(w, 160f));
                card.Button = b;
                card.Edge = b.GetComponent<Image>();

                card.Icon = UiKit.Image(b.transform, "Icon", Color.white);
                UiKit.Place(card.Icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 14f), new Vector2(86f, 86f));
                card.Icon2 = UiKit.Image(b.transform, "Icon2", Color.white);
                UiKit.Place(card.Icon2.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, -62f), new Vector2(54f, 54f));

                card.Name = UiKit.Label(b.transform, "Name", $"{i + 1}.  {opts[i].displayName}", 30, Color.white, TextAnchor.UpperLeft, true);
                UiKit.Place(card.Name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(54f, -12f), new Vector2(-124f, 44f));
                card.Detail = UiKit.Label(b.transform, "Detail", opts[i].description, 19, UiKit.Muted, TextAnchor.UpperLeft);
                UiKit.Place(card.Detail.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                card.Detail.rectTransform.offsetMin = new Vector2(112f, 10f);
                card.Detail.rectTransform.offsetMax = new Vector2(-12f, -54f);
                // keep a clicked card from stealing the movement keys afterwards
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                _cards.Add(card);
            }

            _hint = UiKit.Label(rt, "Hint", "", 28, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1500f, 90f));
            var sh = _hint.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);

            _director.SelectionChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_director != null) _director.SelectionChanged -= Refresh;
        }

        void Refresh()
        {
            var opts = _director.Options;
            for (int i = 0; i < _cards.Count && i < opts.Count; i++)
            {
                bool on = i == _director.Selected;
                _cards[i].Edge.color = on ? new Color(0.75f, 0.55f, 0.18f, 1f) : new Color(0.14f, 0.16f, 0.22f, 1f);
                _cards[i].Name.color = on ? new Color(1f, 0.9f, 0.55f) : Color.white;
                SetIcon(_cards[i].Icon, opts[i].slot1);
                SetIcon(_cards[i].Icon2, opts[i].slot2);
            }
            var cur = _director.Selected >= 0 && _director.Selected < opts.Count ? opts[_director.Selected] : null;
            string pick = InputDevices.Scheme == InputScheme.Touch ? "Tap a card" : "Press 1-4, click a card or use the shoulder buttons";
            _hint.text = cur == null ? "" : $"{cur.displayName} equipped. Try it on the dummies.\n{pick}. Walk into the portal on the right and push UP when you are ready.";
        }

        static void SetIcon(Image img, Roughlike.Combat.WeaponData w)
        {
            if (w == null || w.sprite == null) { img.enabled = false; return; }
            img.enabled = true;
            UiKit.FillIcon(img, ItemInstance.ForWeapon(w));
        }

        void Update()
        {
            if (_director == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) _director.Select(0);
                if (kb.digit2Key.wasPressedThisFrame) _director.Select(1);
                if (kb.digit3Key.wasPressedThisFrame) _director.Select(2);
                if (kb.digit4Key.wasPressedThisFrame) _director.Select(3);
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftShoulder.wasPressedThisFrame) _director.Next(-1);
                if (pad.rightShoulder.wasPressedThisFrame) _director.Next(1);
            }
        }
    }
}
