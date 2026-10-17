using Roughlike.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// The in-run HUD (uGUI): health, flask charges, mutations, gold and Cells, scroll stats, both weapon slots and both
    /// skill slots with cooldowns, the interaction prompt with an item card, and toasts. Rebuilt from data each frame
    /// (cheap); there is no state to keep in sync.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        sealed class Slot
        {
            public Image Border, Icon, Cooldown;
            public Text Name, Key;
        }

        PlayerInventory _inv;
        PlayerStats _stats;
        Health _health;
        SkillRunner _skills;
        PlayerCombat _combat;

        Image _healthFill, _healthLag;
        Text _healthText, _goldText, _cellText, _promptText;
        readonly List<Image> _flaskPips = new List<Image>();
        readonly List<Image> _mutationBoxes = new List<Image>();
        readonly List<Text> _mutationLetters = new List<Text>();
        readonly Text[] _scrollTexts = new Text[3];
        readonly Slot[] _weaponSlots = new Slot[2];
        readonly Slot[] _skillSlots = new Slot[2];
        ItemCard _promptCard;
        RectTransform _promptRoot;
        float _lagFraction = 1f;

        public static HudView Create(UiRoot root, PlayerInventory inv, PlayerStats stats, Health health, SkillRunner skills, PlayerCombat combat)
        {
            var go = new GameObject("HUD View");
            go.transform.SetParent(root.HudLayer, false);
            var v = go.AddComponent<HudView>();
            v._inv = inv; v._stats = stats; v._health = health; v._skills = skills; v._combat = combat;
            v.Build(root.HudLayer);
            return v;
        }

        void OnEnable()
        {
            EventBus<InteractPromptEvent>.Subscribe(OnPrompt);
        }

        void OnDisable()
        {
            EventBus<InteractPromptEvent>.Unsubscribe(OnPrompt);
        }

        // =====================================================================
        // Layout
        // =====================================================================

        void Build(RectTransform hud)
        {
            // ---- top left: health, flask, mutations
            var topLeft = UiKit.Node("Top Left", hud);
            UiKit.Place(topLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(520f, 140f));

            var hpBack = UiKit.Image(topLeft, "HP Back", new Color(0.04f, 0.03f, 0.05f, 0.9f));
            UiKit.Place(hpBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(480f, 34f));
            _healthLag = UiKit.Image(hpBack.transform, "HP Lag", new Color(0.95f, 0.85f, 0.7f, 0.9f));
            UiKit.Stretch(_healthLag.rectTransform, 3f);
            _healthLag.type = Image.Type.Filled; _healthLag.fillMethod = Image.FillMethod.Horizontal;
            _healthFill = UiKit.Image(hpBack.transform, "HP Fill", new Color(0.86f, 0.2f, 0.22f));
            UiKit.Stretch(_healthFill.rectTransform, 3f);
            _healthFill.type = Image.Type.Filled; _healthFill.fillMethod = Image.FillMethod.Horizontal;
            _healthText = UiKit.Label(hpBack.transform, "HP Text", "", 22, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(_healthText.rectTransform);

            for (int i = 0; i < 4; i++)
            {
                var pip = UiKit.Image(topLeft, "Flask " + i, new Color(0.5f, 1f, 0.6f), ProceduralSprites.Circle);
                UiKit.Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 34f, -44f), new Vector2(28f, 28f));
                _flaskPips.Add(pip);
            }

            for (int i = 0; i < PlayerInventory.MaxMutations; i++)
            {
                var box = UiKit.Image(topLeft, "Mutation " + i, new Color(1f, 1f, 1f, 0.1f));
                UiKit.Place(box.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f + i * 44f, -42f), new Vector2(38f, 38f));
                var letter = UiKit.Label(box.transform, "Letter", "", 24, new Color(0.05f, 0.05f, 0.08f), TextAnchor.MiddleCenter, true);
                UiKit.Stretch(letter.rectTransform);
                _mutationBoxes.Add(box);
                _mutationLetters.Add(letter);
            }

            // ---- top right: gold, cells, scroll stats
            var topRight = UiKit.Node("Top Right", hud);
            UiKit.Place(topRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(360f, 150f));

            var coin = UiKit.Image(topRight, "Coin", UiKit.Gold, ProceduralSprites.Circle);
            UiKit.Place(coin.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-130f, -2f), new Vector2(30f, 30f));
            _goldText = UiKit.Label(topRight, "Gold", "0", 32, UiKit.Gold, TextAnchor.MiddleRight, true);
            UiKit.Place(_goldText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(120f, 34f));

            var cell = UiKit.Image(topRight, "Cell", new Color(0.4f, 0.95f, 1f), ProceduralSprites.Spark);
            UiKit.Place(cell.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-130f, -40f), new Vector2(34f, 34f));
            _cellText = UiKit.Label(topRight, "Cells", "0", 28, new Color(0.4f, 0.95f, 1f), TextAnchor.MiddleRight, true);
            UiKit.Place(_cellText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -38f), new Vector2(120f, 34f));

            StatColor[] colours = { StatColor.Ember, StatColor.Brine, StatColor.Moss };
            for (int i = 0; i < 3; i++)
            {
                Color c = StatNames.Colour(colours[i]);
                var box = UiKit.Image(topRight, "Scroll " + colours[i], new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.95f));
                UiKit.Place(box.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-(2 - i) * 78f, -84f), new Vector2(70f, 52f));
                var edge = UiKit.Image(box.transform, "Edge", c);
                UiKit.Place(edge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 5f));
                _scrollTexts[i] = UiKit.Label(box.transform, "Level", "0", 30, c, TextAnchor.MiddleCenter, true);
                UiKit.Stretch(_scrollTexts[i].rectTransform);
            }

            // ---- bottom left: weapon and skill slots
            var bottomLeft = UiKit.Node("Bottom Left", hud);
            UiKit.Place(bottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 30f), new Vector2(560f, 150f));
            string[] weaponKeys = { "J", "K" };
            string[] skillKeys = { "U", "I" };
            for (int i = 0; i < 2; i++)
            {
                _weaponSlots[i] = BuildSlot(bottomLeft, "Weapon " + i, new Vector2(i * 150f, 0f), new Vector2(140f, 120f), weaponKeys[i], false);
                _skillSlots[i] = BuildSlot(bottomLeft, "Skill " + i, new Vector2(330f + i * 110f, 0f), new Vector2(100f, 100f), skillKeys[i], true);
            }

            // ---- bottom center: interaction prompt and item card
            _promptRoot = UiKit.Node("Prompt", hud);
            UiKit.Place(_promptRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 360f));
            _promptText = UiKit.Label(_promptRoot, "Prompt Text", "", 28, Color.white, TextAnchor.LowerCenter, true);
            UiKit.Place(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(600f, 44f));
            _promptCard = ItemCard.Create(_promptRoot, new Vector2(400f, 290f));
            UiKit.Place((RectTransform)_promptCard.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(400f, 290f));
            _promptCard.Hide();
            _promptRoot.gameObject.SetActive(false);
        }

        Slot BuildSlot(Transform parent, string name, Vector2 pos, Vector2 size, string key, bool round)
        {
            var s = new Slot();
            s.Border = UiKit.Image(parent, name, new Color(0.3f, 0.3f, 0.35f));
            UiKit.Place(s.Border.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), pos, size);
            var fill = UiKit.Image(s.Border.transform, "Fill", new Color(0.07f, 0.08f, 0.11f, 0.95f));
            UiKit.Stretch(fill.rectTransform, 4f);

            s.Icon = UiKit.Image(fill.transform, "Icon", Color.white);
            UiKit.Place(s.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, round ? 4f : 12f), Vector2.one * (round ? 52f : 56f));

            s.Name = UiKit.Label(fill.transform, "Name", "", round ? 14 : 16, Color.white, TextAnchor.LowerCenter);
            UiKit.Place(s.Name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 40f));

            s.Key = UiKit.Label(s.Border.transform, "Key", key, 20, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Place(s.Key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 30f));
            var keyBack = UiKit.Image(s.Key.transform, "KeyBack", new Color(0f, 0f, 0f, 0.85f));
            UiKit.Stretch(keyBack.rectTransform);
            keyBack.transform.SetAsFirstSibling();

            s.Cooldown = UiKit.Image(fill.transform, "Cooldown", new Color(0f, 0f, 0f, 0.7f), round ? ProceduralSprites.Circle : ProceduralSprites.Square);
            UiKit.Stretch(s.Cooldown.rectTransform);
            s.Cooldown.type = Image.Type.Filled;
            s.Cooldown.fillMethod = round ? Image.FillMethod.Radial360 : Image.FillMethod.Vertical;
            s.Cooldown.fillAmount = 0f;
            return s;
        }

        // =====================================================================
        // Per-frame refresh
        // =====================================================================

        void Update() => Refresh();

        /// <summary>Re-reads the data and updates every element. Called each frame; public so tests can drive it.</summary>
        public void Refresh()
        {
            if (_health == null || _inv == null) return;

            float f = _health.Fraction;
            _healthFill.fillAmount = f;
            _lagFraction = f < _lagFraction ? Mathf.MoveTowards(_lagFraction, f, 0.6f * Time.unscaledDeltaTime) : f;
            _healthLag.fillAmount = _lagFraction;
            _healthText.text = $"{Mathf.CeilToInt(_health.Current)} / {Mathf.CeilToInt(_health.Max)}";

            for (int i = 0; i < _flaskPips.Count; i++)
            {
                bool exists = i < _inv.MaxFlaskCharges;
                _flaskPips[i].gameObject.SetActive(exists);
                if (!exists) continue;
                _flaskPips[i].color = i < _inv.FlaskCharges ? new Color(0.5f, 1f, 0.6f) : new Color(0.25f, 0.3f, 0.27f, 0.8f);
            }

            for (int i = 0; i < _mutationBoxes.Count; i++)
            {
                bool has = i < _inv.Mutations.Count;
                _mutationBoxes[i].color = has ? _inv.Mutations[i].tint : new Color(1f, 1f, 1f, 0.1f);
                _mutationLetters[i].text = has ? _inv.Mutations[i].displayName.Substring(0, 1) : "";
            }

            _goldText.text = _inv.Gold.ToString();
            _cellText.text = _inv.Cells.ToString();
            for (int i = 0; i < 3; i++) _scrollTexts[i].text = _inv.Scrolls[i].ToString();

            for (int i = 0; i < 2; i++)
            {
                FillSlot(_weaponSlots[i], _inv.Weapons[i]);
                FillSlot(_skillSlots[i], _inv.Skills[i]);
                _skillSlots[i].Cooldown.fillAmount = _skills != null && _inv.Skills[i] != null ? _skills.CooldownFraction(i) : 0f;
            }
        }

        static void FillSlot(Slot s, ItemInstance item)
        {
            s.Border.color = item != null ? item.RarityColour : new Color(0.3f, 0.3f, 0.35f);
            UiKit.FillIcon(s.Icon, item);
            s.Name.text = item == null ? "empty" : item.BaseName + (item.Tier > 1 ? " +" + (item.Tier - 1) : "");
            s.Name.color = item == null ? UiKit.Muted : Color.white;
        }

        // =====================================================================
        // Prompt
        // =====================================================================

        void OnPrompt(InteractPromptEvent e)
        {
            if (_promptRoot == null) return;
            _promptRoot.gameObject.SetActive(e.Visible);
            if (!e.Visible) return;

            _promptText.text = "[" + InputDevices.Prompt("Interact") + "] " + e.Text;
            _promptText.color = e.Colour;
            if (e.Item != null) _promptCard.Show(e.Item); else _promptCard.Hide();
        }
    }
}


