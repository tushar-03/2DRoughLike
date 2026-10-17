using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Items
{
    /// <summary>
    /// Builds the graybox look and behaviour of everything lying in the world: dropped items, coins, scrolls, chests,
    /// shops and altars. All visuals are placeholder shapes tinted by rarity or colour; real art replaces them later.
    /// </summary>
    public static class LootFactory
    {
        public static TextMesh Label(Transform parent, string text, Color colour, float size, Vector3 localPos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = colour;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 55;
            return tm;
        }

        static SpriteRenderer Shape(Transform parent, Sprite sprite, Color colour, Vector2 scale, Vector2 pos, int order, float rotation = 0f)
        {
            var go = new GameObject("Shape");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A painted prop from the world sheet, pivoted at its feet like every other character.</summary>
        static SpriteRenderer Painted(Transform parent, string clip, Vector2 pos, int order, float scale = 1f)
        {
            var go = new GameObject(clip);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WorldArt.Props.Prop(clip);
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ dropped items

        public static WorldItem DropItem(ItemInstance item, Vector2 position)
        {
            var go = new GameObject("Item: " + item.DisplayName);
            go.transform.position = position + Vector2.up * 0.1f;
            var wi = go.AddComponent<WorldItem>();

            Color rc = item.RarityColour;
            Shape(go.transform, ProceduralSprites.Square, new Color(rc.r, rc.g, rc.b, 0.22f), new Vector2(0.35f, 3.4f), new Vector2(0f, 1.7f), -1);
            Shape(go.transform, ProceduralSprites.Circle, new Color(rc.r, rc.g, rc.b, 0.5f), new Vector2(1.3f, 0.35f), new Vector2(0f, 0.05f), 0);

            var icon = new GameObject("Icon").transform;
            icon.SetParent(go.transform, false);
            icon.localPosition = new Vector3(0f, 0.9f, 0f);
            switch (item.Kind)
            {
                case ItemKind.Weapon:
                    Color wt = item.Weapon != null ? item.Weapon.tint : Color.white;
                    if (item.Weapon != null && item.Weapon.kind == WeaponKind.Shield)
                        Shape(icon, ProceduralSprites.Square, wt, new Vector2(0.65f, 0.85f), Vector2.zero, 6);
                    else
                    {
                        Shape(icon, ProceduralSprites.Square, wt, new Vector2(0.14f, 0.95f), Vector2.zero, 6, 25f);
                        Shape(icon, ProceduralSprites.Square, new Color(0.5f, 0.35f, 0.2f), new Vector2(0.4f, 0.1f), new Vector2(-0.18f, -0.34f), 7, 25f);
                    }
                    break;
                case ItemKind.Skill:
                    Shape(icon, ProceduralSprites.Circle, item.Skill != null ? item.Skill.tint : Color.white, Vector2.one * 0.6f, Vector2.zero, 6);
                    break;
                case ItemKind.Amulet:
                    Shape(icon, ProceduralSprites.Spark, item.Gear != null ? item.Gear.tint : Color.white, Vector2.one * 0.85f, Vector2.zero, 6);
                    break;
                default:
                    Shape(icon, ProceduralSprites.Square, item.Gear != null ? item.Gear.tint : Color.white, new Vector2(0.55f, 0.65f), Vector2.zero, 6);
                    break;
            }
            Label(go.transform, item.DisplayName, rc, 0.1f, new Vector3(0f, 1.9f, 0f));

            wi.Init(item, icon);
            return wi;
        }

        // ------------------------------------------------------------------ currency drops

        public static Collectible SpawnGold(Vector2 position, int value)
        {
            var go = new GameObject("Gold");
            go.transform.position = position;
            Shape(go.transform, ProceduralSprites.Circle, new Color(1f, 0.82f, 0.25f), Vector2.one * 0.32f, Vector2.zero, 20);
            var c = go.AddComponent<Collectible>();
            c.Init(CollectibleKind.Gold, value, StatColor.None, true);
            return c;
        }

        public static Collectible SpawnCell(Vector2 position, int value)
        {
            var go = new GameObject("Cell");
            go.transform.position = position;
            Shape(go.transform, ProceduralSprites.Spark, new Color(0.4f, 0.95f, 1f), Vector2.one * 0.55f, Vector2.zero, 20);
            var c = go.AddComponent<Collectible>();
            c.Init(CollectibleKind.Cell, value, StatColor.None, true);
            return c;
        }

        public static Collectible SpawnScroll(Vector2 position, StatColor colour)
        {
            var go = new GameObject(colour + " Scroll");
            go.transform.position = position + Vector2.up * 0.4f;
            Color c = StatNames.Colour(colour);
            Shape(go.transform, ProceduralSprites.Square, new Color(0.95f, 0.9f, 0.75f), new Vector2(0.38f, 0.65f), Vector2.zero, 20);
            Shape(go.transform, ProceduralSprites.Square, c, new Vector2(0.44f, 0.12f), new Vector2(0f, 0.3f), 21);
            Shape(go.transform, ProceduralSprites.Square, c, new Vector2(0.44f, 0.12f), new Vector2(0f, -0.3f), 21);
            Shape(go.transform, ProceduralSprites.Circle, new Color(c.r, c.g, c.b, 0.35f), Vector2.one * 1.4f, Vector2.zero, 19);
            Label(go.transform, colour.ToString(), c, 0.09f, new Vector3(0f, 0.85f, 0f));
            var col = go.AddComponent<Collectible>();
            col.Init(CollectibleKind.Scroll, 1, colour, false);
            return col;
        }

        // ------------------------------------------------------------------ chests, shops, altars

        public static LootChest SpawnChest(Vector2 feet, ItemInstance contents)
        {
            var go = new GameObject("Chest");
            go.transform.position = feet;
            if (WorldArt.Has("chest"))
            {
                var painted = Painted(go.transform, "chest", Vector2.zero, 8, 1.5f);
                var pc = go.AddComponent<LootChest>();
                pc.Init(contents, null);
                pc.UsePaintedSprites(painted, WorldArt.Props.Prop("chest_open"));
                return pc;
            }
            var body = Shape(go.transform, ProceduralSprites.Square, new Color(0.62f, 0.42f, 0.18f), new Vector2(1.4f, 0.9f), new Vector2(0f, 0.45f), 8);
            var lid = Shape(go.transform, ProceduralSprites.Square, new Color(0.8f, 0.58f, 0.25f), new Vector2(1.5f, 0.3f), new Vector2(0f, 1.0f), 9);
            Shape(go.transform, ProceduralSprites.Square, new Color(1f, 0.85f, 0.35f), new Vector2(0.18f, 0.28f), new Vector2(0f, 0.8f), 10);
            var chest = go.AddComponent<LootChest>();
            chest.Init(contents, lid.transform);
            return chest;
        }

        public static ShopStand SpawnShop(Vector2 feet, ShopStock stock)
        {
            var go = new GameObject("Salvage Merchant");
            go.transform.position = feet;
            if (WorldArt.Has("merchant"))
            {
                Painted(go.transform, "merchant", Vector2.zero, 8, 1.2f);
                Label(go.transform, "SALVAGE", new Color(1f, 0.9f, 0.6f), 0.11f, new Vector3(0f, 2.9f, 0f));
                var ps = go.AddComponent<ShopStand>();
                ps.Init(stock);
                return ps;
            }
            Shape(go.transform, ProceduralSprites.Square, new Color(0.3f, 0.55f, 0.6f), new Vector2(0.9f, 1.5f), new Vector2(-0.9f, 0.75f), 8);
            Shape(go.transform, ProceduralSprites.Square, new Color(0.45f, 0.3f, 0.2f), new Vector2(2.2f, 0.8f), new Vector2(0.4f, 0.4f), 9);
            Shape(go.transform, ProceduralSprites.Square, new Color(0.9f, 0.7f, 0.3f), new Vector2(0.3f, 0.25f), new Vector2(0.1f, 0.95f), 10);
            Shape(go.transform, ProceduralSprites.Square, new Color(0.5f, 0.8f, 0.9f), new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.95f), 10);
            Label(go.transform, "SALVAGE", new Color(1f, 0.9f, 0.6f), 0.11f, new Vector3(0f, 2.4f, 0f));
            var shop = go.AddComponent<ShopStand>();
            shop.Init(stock);
            return shop;
        }

        public static MutationAltar SpawnAltar(Vector2 feet, System.Collections.Generic.List<MutationData> choices)
        {
            var go = new GameObject("Mutation Altar");
            go.transform.position = feet;
            Color c = new Color(0.78f, 0.45f, 1f);
            if (WorldArt.Has("altar")) Painted(go.transform, "altar", Vector2.zero, 8, 1.25f);
            else Shape(go.transform, ProceduralSprites.Square, new Color(0.3f, 0.22f, 0.4f), new Vector2(0.9f, 1.2f), new Vector2(0f, 0.6f), 8);
            var glow = Shape(go.transform, ProceduralSprites.Circle, new Color(c.r, c.g, c.b, 0.55f), Vector2.one * 1.2f, new Vector2(0f, 1.6f), 9);
            Shape(go.transform, ProceduralSprites.Spark, c, Vector2.one * 0.8f, new Vector2(0f, 1.6f), 10);
            Label(go.transform, "MUTATION ALTAR", c, 0.1f, new Vector3(0f, 2.7f, 0f));
            var altar = go.AddComponent<MutationAltar>();
            altar.Init(choices, glow);
            return altar;
        }
    }
}
