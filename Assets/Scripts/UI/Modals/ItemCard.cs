using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Items;

namespace Roughlike.UI
{
    /// <summary>A tooltip-style card describing an item: name in rarity colour, kind, stats and affixes (curses in red).</summary>
    public sealed class ItemCard : MonoBehaviour
    {
        Text _title, _subtitle, _body;
        Image _edge, _icon;

        public static ItemCard Create(Transform parent, Vector2 size)
        {
            RectTransform inner = UiKit.Panel(parent, "Item Card", size);
            var card = inner.parent.gameObject.AddComponent<ItemCard>();
            card._edge = inner.parent.GetComponent<Image>();

            card._icon = UiKit.Image(inner, "Icon", Color.white);
            UiKit.Place(card._icon.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(84f, 84f));
            card._icon.raycastTarget = false;
            card._title = UiKit.Label(inner, "Title", "", 30, Color.white, TextAnchor.UpperLeft, true);
            UiKit.Place(card._title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-50f, -14f), new Vector2(-132f, 40f));
            card._subtitle = UiKit.Label(inner, "Subtitle", "", 20, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(card._subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-50f, -54f), new Vector2(-132f, 28f));
            card._body = UiKit.Label(inner, "Body", "", 22, Color.white, TextAnchor.UpperLeft);
            UiKit.Place(card._body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(-32f, -100f));
            card._body.rectTransform.offsetMin = new Vector2(16f, 12f);
            card._body.rectTransform.offsetMax = new Vector2(-16f, -88f);
            return card;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Shows plain text in the card (for offers that are not items: flask refills, upgrades, scrolls).</summary>
        public void ShowText(string title, Color colour, string body)
        {
            gameObject.SetActive(true);
            _edge.color = colour;
            _title.text = title;
            _title.color = colour;
            _icon.enabled = false;
            _subtitle.text = "";
            _body.supportRichText = true;
            _body.text = body;
        }

        public void Show(ItemInstance item)
        {
            if (item == null) { Hide(); return; }
            gameObject.SetActive(true);

            Color rc = item.RarityColour;
            _edge.color = rc;
            _title.text = item.DisplayName;
            _title.color = rc;
            _icon.enabled = true;
            UiKit.FillIcon(_icon, item);
            _subtitle.text = $"{item.Rarity} {KindLabel(item)}" + (item.Tier > 1 ? $"   Tier {item.Tier}" : "");

            var curses = new HashSet<string>();
            foreach (var a in item.Affixes) if (a.data.isCurse) curses.Add(a.Describe());

            var sb = new StringBuilder();
            string description = Description(item);
            if (!string.IsNullOrEmpty(description)) sb.AppendLine($"<color=#{UiKit.Hex(UiKit.Muted)}><i>{description}</i></color>");
            foreach (var line in item.DescribeLines())
            {
                string colour = curses.Contains(line) ? UiKit.Hex(UiKit.Danger) : "FFFFFF";
                sb.AppendLine($"<color=#{colour}>{line}</color>");
            }
            _body.supportRichText = true;
            _body.text = sb.ToString();
        }

        static string KindLabel(ItemInstance item)
        {
            switch (item.Kind)
            {
                case ItemKind.Weapon: return item.Weapon != null && item.Weapon.kind == Combat.WeaponKind.Shield ? "shield" : "weapon";
                case ItemKind.Skill: return "skill";
                case ItemKind.Amulet: return "amulet";
                default: return "outfit";
            }
        }

        static string Description(ItemInstance item)
        {
            switch (item.Kind)
            {
                case ItemKind.Weapon: return item.Weapon != null ? item.Weapon.description : "";
                case ItemKind.Skill: return item.Skill != null ? item.Skill.description : "";
                default: return item.Gear != null ? item.Gear.description : "";
            }
        }
    }
}
