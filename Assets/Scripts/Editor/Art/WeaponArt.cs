using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Editor.Art
{
    /// <summary>The colours a weapon is painted in. Characters pass their own palette; the standalone sprites use <see cref="Steel"/>.</summary>
    public struct WeaponPalette
    {
        public Mat Metal, Trim, Wood, Leather, Field, Glow;

        /// <summary>Plain European kit: bright steel, brass fittings, dark leather and ash wood.</summary>
        public static WeaponPalette Steel => new WeaponPalette
        {
            Metal = Mat.Of(new Color(0.76f, 0.8f, 0.88f), Color.white, 3.5f),
            Trim = Mat.Of(new Color(0.92f, 0.72f, 0.28f), Color.white, 3f),
            Wood = Mat.Of(new Color(0.5f, 0.34f, 0.2f), new Color(0.9f, 0.7f, 0.4f), 3f),
            Leather = Mat.Of(new Color(0.3f, 0.17f, 0.13f)),
            Field = Mat.Of(new Color(0.2f, 0.33f, 0.6f), new Color(0.6f, 0.75f, 1f), 3f),
            Glow = Mat.Glow(new Color(1f, 0.85f, 0.5f))
        };
    }

    /// <summary>
    /// European arms painted with the cel painter: arming sword, rondel dagger, longsword, warhammer, halberd-like boathook,
    /// flanged mace, bow, and heater, kite and round shields. One drawing routine serves the standalone item sprites and the
    /// weapons baked into enemy sheets, so a weapon looks the same wherever it appears.
    /// </summary>
    public static class WeaponArt
    {
        static Vector2 P(Vector2 hand, Vector2 d, float along, float across = 0f)
        {
            Vector2 nrm = new Vector2(-d.y, d.x);
            return hand + d * along + nrm * across;
        }

        /// <summary>Paints <paramref name="kind"/> held at <paramref name="hand"/> pointing along <paramref name="d"/> (unit vector).</summary>
        public static void Draw(Painter pt, string kind, Vector2 hand, Vector2 d, float len, WeaponPalette c, float draw = 0f)
        {
            switch (kind)
            {
                case "sword": Sword(pt, hand, d, len, c, 1f, 1f); break;
                case "greatsword": Sword(pt, hand, d, len, c, 1.7f, 1.35f); break;
                case "dagger": Dagger(pt, hand, d, len, c); break;
                case "warhammer": Warhammer(pt, hand, d, len, c); break;
                case "polearm": Polearm(pt, hand, d, len, c); break;
                case "mace": Mace(pt, hand, d, len, c); break;
                case "club": Club(pt, hand, d, len, c); break;
                case "staff": Staff(pt, hand, d, c); break;
                case "bow": Bow(pt, hand, c, draw); break;
            }
        }

        static void Sword(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c, float width, float guard)
        {
            Mat steel = c.Metal;
            Mat fuller = Mat.Of(new Color(0.42f, 0.47f, 0.58f));
            // grip, wrapped in leather bands
            pt.Capsule(P(hand, d, -22f * guard), P(hand, d, 2f), 4.6f * width, c.Leather);
            for (int i = 0; i < 3; i++) pt.Capsule(P(hand, d, -18f * guard + i * 7f, 4f * width), P(hand, d, -16f * guard + i * 7f, -4f * width), 1.3f, Mat.Glow(new Color(0.16f, 0.08f, 0.06f)));
            pt.Circle(P(hand, d, -26f * guard), 6.8f * width, c.Trim);                       // wheel pommel
            pt.Circle(P(hand, d, -26f * guard), 2.6f * width, c.Metal);
            // blade, tapering to a point, with a fuller and a bright edge
            pt.Poly(new[]
            {
                P(hand, d, 6f, 9f * width), P(hand, d, len * 0.7f, 7.6f * width), P(hand, d, len - 9f, 4f * width), P(hand, d, len, 0f),
                P(hand, d, len - 9f, -4f * width), P(hand, d, len * 0.7f, -7.6f * width), P(hand, d, 6f, -9f * width)
            }, steel);
            pt.Capsule(P(hand, d, 12f), P(hand, d, len * 0.76f), 2.3f * width, 1.2f * width, fuller);
            pt.Capsule(P(hand, d, 10f, 6.4f * width), P(hand, d, len * 0.68f, 5.2f * width), 1.1f, Mat.Glow(new Color(0.95f, 0.98f, 1f)));
            // crossguard with drooping quillons, and a brass collar
            pt.Capsule(P(hand, d, 5f, 25f * guard), P(hand, d, 5f, -25f * guard), 4.4f * width, c.Trim);
            pt.Circle(P(hand, d, 3f, 25f * guard), 5.6f * width, c.Trim);
            pt.Circle(P(hand, d, 3f, -25f * guard), 5.6f * width, c.Trim);
            pt.Circle(P(hand, d, 5f), 7f * width, c.Trim);
        }

        static void Dagger(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c)
        {
            float L = Mathf.Max(40f, len * 0.7f);
            pt.Capsule(P(hand, d, -16f), P(hand, d, 0f), 4f, c.Leather);
            pt.Circle(P(hand, d, -20f), 5.4f, c.Trim);                                         // pommel
            pt.Poly(new[] { P(hand, d, 4f, 7.5f), P(hand, d, L * 0.6f, 6.5f), P(hand, d, L, 0f), P(hand, d, L * 0.6f, -6.5f), P(hand, d, 4f, -7.5f) }, c.Metal);
            pt.Capsule(P(hand, d, 8f), P(hand, d, L * 0.7f), 1.7f, Mat.Of(new Color(0.42f, 0.47f, 0.58f)));
            pt.Capsule(P(hand, d, 7f, 5f), P(hand, d, L * 0.58f, 4.2f), 0.9f, Mat.Glow(new Color(0.95f, 0.98f, 1f)));
            pt.Ellipse(P(hand, d, 3f), 3.4f, 12f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, c.Trim);   // rondel guard, edge-on
            pt.Circle(P(hand, d, 4f), 5f, c.Trim);
        }

        static void Warhammer(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c)
        {
            float L = Mathf.Max(70f, len);
            pt.Capsule(P(hand, d, -34f), P(hand, d, L), 4.6f, c.Wood);                          // haft
            for (int i = 0; i < 4; i++) pt.Capsule(P(hand, d, -26f + i * 6f, 4.4f), P(hand, d, -24f + i * 6f, -4.4f), 1.4f, c.Leather);
            pt.Capsule(P(hand, d, L - 54f), P(hand, d, L - 10f), 6f, 6f, c.Metal);              // langets
            pt.Poly(new[] { P(hand, d, L - 16f, -14f), P(hand, d, L + 20f, -14f), P(hand, d, L + 20f, 14f), P(hand, d, L - 16f, 14f) }, c.Metal);   // head block
            pt.Poly(new[] { P(hand, d, L - 20f, 14f), P(hand, d, L + 24f, 14f), P(hand, d, L + 24f, 32f), P(hand, d, L - 20f, 32f) }, c.Metal);    // striking face
            pt.Poly(new[] { P(hand, d, L - 8f, -14f), P(hand, d, L + 14f, -14f), P(hand, d, L + 2f, -46f) }, c.Metal);                               // back spike
            pt.Poly(new[] { P(hand, d, L + 20f, -7f), P(hand, d, L + 20f, 7f), P(hand, d, L + 46f, 0f) }, c.Metal);                                  // top spike
            pt.Capsule(P(hand, d, L - 12f, 26f), P(hand, d, L + 20f, 26f), 1.6f, Mat.Glow(new Color(0.95f, 0.98f, 1f)));
            pt.Circle(P(hand, d, L + 2f), 5f, c.Trim);
        }

        static void Polearm(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c)
        {
            float L = Mathf.Max(110f, len);
            pt.Capsule(P(hand, d, -72f), P(hand, d, L), 3.8f, c.Wood);
            pt.Capsule(P(hand, d, L - 70f), P(hand, d, L - 8f), 5.2f, 5.2f, c.Metal);           // langets
            pt.Poly(new[] { P(hand, d, L - 6f, 7f), P(hand, d, L + 14f, 8.5f), P(hand, d, L + 46f, 0f), P(hand, d, L + 14f, -8.5f), P(hand, d, L - 6f, -7f) }, c.Metal);   // spear point
            pt.Poly(new[] { P(hand, d, L - 44f, 4f), P(hand, d, L - 4f, 4f), P(hand, d, L - 6f, 34f), P(hand, d, L - 24f, 38f), P(hand, d, L - 38f, 22f) }, c.Metal);   // axe blade
            pt.Capsule(P(hand, d, L - 36f, -3f), P(hand, d, L - 22f, -22f), 3.2f, c.Metal);      // hook, as on a boathook
            pt.Capsule(P(hand, d, L - 22f, -22f), P(hand, d, L - 8f, -26f), 3f, 2.4f, c.Metal);
            pt.Capsule(P(hand, d, L - 8f, -26f), P(hand, d, L + 0f, -16f), 2.4f, 1.8f, c.Metal);
            pt.Circle(P(hand, d, L - 16f), 5.6f, c.Trim);
            pt.Circle(P(hand, d, -74f), 4.6f, c.Trim);                                          // butt cap
        }

        static void Mace(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c)
        {
            float L = Mathf.Max(60f, len * 0.85f);
            pt.Capsule(P(hand, d, -14f), P(hand, d, L), 5f, c.Wood);
            pt.Circle(P(hand, d, -16f), 5.6f, c.Trim);
            Vector2 head = P(hand, d, L + 8f);
            for (int i = 0; i < 6; i++)                                                         // flanges all around
            {
                float a = i * 60f * Mathf.Deg2Rad + Mathf.Atan2(d.y, d.x);
                Vector2 o = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 side = new Vector2(-o.y, o.x);
                pt.Poly(new[] { head + o * 9f + side * 6f, head + o * 24f + side * 2.4f, head + o * 24f - side * 2.4f, head + o * 9f - side * 6f }, c.Metal);
            }
            pt.Circle(head, 12f, c.Metal);
            pt.Circle(head, 4.6f, c.Trim);
        }

        static void Club(Painter pt, Vector2 hand, Vector2 d, float len, WeaponPalette c)
        {
            pt.Capsule(P(hand, d, -12f), P(hand, d, len), 6f, 13f, c.Wood);
            pt.Circle(P(hand, d, len - 6f), 15f, c.Wood);
            for (int i = 0; i < 5; i++)                                                          // iron nails driven in
            {
                float a = (i * 72f) * Mathf.Deg2Rad;
                Vector2 o = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                pt.Poly(new[] { P(hand, d, len - 6f + o.x * 11f, o.y * 11f + 3f), P(hand, d, len - 6f + o.x * 11f, o.y * 11f - 3f), P(hand, d, len - 6f + o.x * 22f, o.y * 22f) }, c.Metal);
            }
        }

        static void Staff(Painter pt, Vector2 hand, Vector2 d, WeaponPalette c)
        {
            pt.Capsule(P(hand, d, -70f), P(hand, d, 110f), 5f, 5f, c.Wood);
            pt.Capsule(P(hand, d, 96f, 10f), P(hand, d, 110f, 16f), 3f, c.Wood);                 // curled prongs
            pt.Capsule(P(hand, d, 96f, -10f), P(hand, d, 110f, -16f), 3f, c.Wood);
            pt.Circle(P(hand, d, 118f), 11f, c.Glow);
        }

        static void Bow(Painter pt, Vector2 hand, WeaponPalette c, float draw)
        {
            Mat string_ = Mat.Glow(new Color(0.9f, 0.9f, 0.85f));
            c.Wood = Mat.Of(new Color(0.6f, 0.4f, 0.22f), new Color(1f, 0.82f, 0.5f), 3.2f);   // pale yew so the bow reads against the dark body
            Vector2 top = hand + new Vector2(-4f, 66f), bottom = hand + new Vector2(-4f, -66f);
            Vector2 upper = hand + new Vector2(14f, 34f), lower = hand + new Vector2(14f, -34f);
            Vector2 mid = hand + new Vector2(20f, 0f);
            pt.Capsule(top, upper, 2.6f, 4.2f, c.Wood);                                         // recurved tips curl back toward the archer
            pt.Capsule(upper, mid, 4.2f, 5.6f, c.Wood);
            pt.Capsule(mid, lower, 5.6f, 4.2f, c.Wood);
            pt.Capsule(lower, bottom, 4.2f, 2.6f, c.Wood);
            pt.Capsule(hand + new Vector2(20f, 9f), hand + new Vector2(20f, -9f), 6.4f, c.Leather);   // wrapped grip
            pt.Circle(top, 3.4f, c.Trim);
            pt.Circle(bottom, 3.4f, c.Trim);
            Vector2 pull = hand + new Vector2(-8f - draw * 60f, 0f);
            pt.Capsule(top, pull, 1.3f, string_);
            pt.Capsule(pull, bottom, 1.3f, string_);
            if (draw > 0.05f || draw < -0.5f) Arrow(pt, pull, hand + new Vector2(46f, 0f), c);   // nocked arrow
        }

        public static void Arrow(Painter pt, Vector2 tail, Vector2 tip, WeaponPalette c)
        {
            Vector2 d = (tip - tail).normalized;
            pt.Capsule(tail, tip - d * 8f, 1.8f, c.Wood);
            pt.Poly(new[] { tip, tip - d * 10f + new Vector2(-d.y, d.x) * 4.4f, tip - d * 10f - new Vector2(-d.y, d.x) * 4.4f }, c.Metal);
            for (int i = 0; i < 2; i++)
            {
                Vector2 f = tail + d * (3f + i * 5f);
                pt.Poly(new[] { f, f - d * 7f + new Vector2(-d.y, d.x) * 5f, f - d * 3f }, Mat.Of(new Color(0.8f, 0.2f, 0.2f)));
                pt.Poly(new[] { f, f - d * 7f - new Vector2(-d.y, d.x) * 5f, f - d * 3f }, Mat.Of(new Color(0.8f, 0.2f, 0.2f)));
            }
        }

        // ------------------------------------------------------------------ shields

        /// <summary>A heater-shaped shield centred at <paramref name="c"/> with a painted cross; w and h are half extents.</summary>
        public static void Heater(Painter pt, Vector2 c, float w, float h, WeaponPalette pal, bool cross = true)
        {
            Vector2[] Outline(float k) => new[]
            {
                c + new Vector2(-w * k, h * 0.5f * k), c + new Vector2(w * k, h * 0.5f * k), c + new Vector2(w * k, h * 0.08f * k),
                c + new Vector2(w * 0.62f * k, -h * 0.34f * k), c + new Vector2(0f, -h * 0.52f * k), c + new Vector2(-w * 0.62f * k, -h * 0.34f * k),
                c + new Vector2(-w * k, h * 0.08f * k)
            };
            pt.Poly(Outline(1f), pal.Trim);                         // steel-bound rim
            pt.Poly(Outline(0.84f), pal.Field);                     // painted face
            if (cross)
            {
                pt.Capsule(c + new Vector2(0f, h * 0.38f), c + new Vector2(0f, -h * 0.4f), w * 0.1f, pal.Trim);
                pt.Capsule(c + new Vector2(-w * 0.7f, h * 0.2f), c + new Vector2(w * 0.7f, h * 0.2f), w * 0.1f, pal.Trim);
            }
            pt.Circle(c + new Vector2(0f, h * 0.2f), w * 0.26f, pal.Metal);   // boss
            pt.Circle(c + new Vector2(-w * 0.07f, h * 0.23f), w * 0.07f, Mat.Glow(new Color(1f, 1f, 0.95f)));
            for (int i = 0; i < 3; i++)                              // rivets down the edge
            {
                pt.Circle(c + new Vector2(-w * 0.92f, h * (0.38f - i * 0.2f)), 2.2f, pal.Metal);
                pt.Circle(c + new Vector2(w * 0.92f, h * (0.38f - i * 0.2f)), 2.2f, pal.Metal);
            }
        }

        public static void Round(Painter pt, Vector2 c, float r, WeaponPalette pal)
        {
            pt.Circle(c, r, pal.Metal);
            pt.Circle(c, r * 0.82f, pal.Leather);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                pt.Circle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.9f, 2.4f, pal.Trim);   // studs
            }
            pt.Circle(c, r * 0.38f, pal.Metal);                                                       // domed boss
            pt.Circle(c + new Vector2(-r * 0.1f, r * 0.12f), r * 0.14f, Mat.Glow(new Color(1f, 1f, 0.95f)));
            pt.Circle(c, r * 0.1f, pal.Trim);
        }
    }

    /// <summary>Paints the item sprites for the six starting weapons and wires them into the weapon assets.</summary>
    public static class WeaponArtBuilder
    {
        public const string Folder = "Assets/Art/Weapons";
        const string DataFolder = "Assets/Data/Weapons";

        struct Plan
        {
            public string Asset, File, Kind, HeldFile;
            public float Len, Scale, Held;
            public bool Shield;
        }

        static readonly Plan[] Plans =
        {
            new Plan { Asset = "RustedDagger",    File = "Dagger",     Kind = "dagger",    Len = 62f,  Scale = 1f, Held = 1.2f },
            new Plan { Asset = "BellringerSword", File = "ArmingSword", Kind = "sword",     Len = 104f, Scale = 1f, Held = 1.25f },
            new Plan { Asset = "TideHammer",      File = "Warhammer",   Kind = "warhammer", Len = 96f,  Scale = 1f, Held = 1.1f },
            new Plan { Asset = "Boathook",        File = "Boathook",    Kind = "polearm",   Len = 150f, Scale = 1f, Held = 0.95f },
            new Plan { Asset = "TowerShield",     File = "TowerShield", Kind = "tower",     Scale = 1f, Held = 0.78f, Shield = true },
            new Plan { Asset = "Buckler",         File = "Buckler",     Kind = "buckler",   Scale = 1f, Held = 1.15f, Shield = true },
            new Plan { Asset = "HuntersBow",      File = "HuntersBow",  Kind = "bow",       Len = 130f, Scale = 1f, Held = 1.25f },
            new Plan { Asset = "TwinBlades",      File = "TwinBlades",  Kind = "twin",      Len = 96f,  Scale = 1f, Held = 1.15f, HeldFile = "TwinBlade" },
            new Plan { Asset = "Greatsword",      File = "Greatsword",  Kind = "greatsword", Len = 150f, Scale = 1f, Held = 1.15f }
        };

        /// <summary>A lighter, bluer sword for the twin blades.</summary>
        static WeaponPalette Twin()
        {
            var p = WeaponPalette.Steel;
            p.Metal = Mat.Of(new Color(0.66f, 0.76f, 0.9f), Color.white, 3.5f);
            p.Trim = Mat.Of(new Color(0.82f, 0.86f, 0.95f), Color.white, 3f);
            p.Leather = Mat.Of(new Color(0.16f, 0.2f, 0.34f));
            return p;
        }

        [MenuItem("Roughlike/Art/Generate Weapon Sprites")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            foreach (var plan in Plans)
            {
                var pal = WeaponPalette.Steel;
                const int W = 260, H = 380;
                var pt = new Painter(W, H);
                Vector2 hand = new Vector2(W * 0.5f, 130f);
                Vector2 pivot = hand;
                switch (plan.Kind)
                {
                    case "tower":
                        pivot = new Vector2(W * 0.5f, H * 0.5f);
                        WeaponArt.Heater(pt, pivot, 44f, 150f, pal);
                        break;
                    case "buckler":
                        pivot = new Vector2(W * 0.5f, H * 0.5f);
                        WeaponArt.Round(pt, pivot, 44f, pal);
                        break;
                    case "twin":
                    {
                        // item icon: two blades crossed
                        var tp = Twin();
                        WeaponArt.Draw(pt, "sword", hand, new Vector2(Mathf.Sin(-0.4f), Mathf.Cos(-0.4f)), plan.Len, tp);
                        WeaponArt.Draw(pt, "sword", hand, new Vector2(Mathf.Sin(0.4f), Mathf.Cos(0.4f)), plan.Len, tp);
                        break;
                    }
                    default:
                        WeaponArt.Draw(pt, plan.Kind, hand, Vector2.up, plan.Len, pal);
                        break;
                }
                Color32[] px = pt.Render(3.2f, new Color(0.07f, 0.05f, 0.1f));
                Write(plan.File, px, W, H, pivot);

                if (plan.HeldFile != null)
                {
                    // what is actually carried: a single slim blade, pivot at the grip
                    var single = new Painter(W, H);
                    WeaponArt.Draw(single, "sword", hand, Vector2.up, plan.Len, Twin());
                    Write(plan.HeldFile, single.Render(3.2f, new Color(0.07f, 0.05f, 0.1f)), W, H, hand);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Assigns the painted sprites to the weapon assets (painting them first if they do not exist yet).</summary>
        public static void Assign()
        {
            bool missing = false;
            foreach (var plan in Plans) if (!File.Exists($"{Folder}/{plan.File}.png") || (plan.HeldFile != null && !File.Exists($"{Folder}/{plan.HeldFile}.png"))) missing = true;
            if (missing) Build();

            foreach (var plan in Plans)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataFolder}/{plan.Asset}.asset");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{plan.File}.png");
                if (weapon == null || sprite == null) continue;
                weapon.sprite = sprite;
                weapon.heldScale = plan.Held;
                weapon.heldSprite = plan.HeldFile != null ? AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{plan.HeldFile}.png") : null;
                EditorUtility.SetDirty(weapon);
            }
            AssetDatabase.SaveAssets();
        }

        static void Write(string file, Color32[] px, int w, int h, Vector2 pivotPx)
        {
            // crop to the painted area so the sprite (and the icon) is as tight as possible
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 6) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            minX = Mathf.Max(0, minX - 2); minY = Mathf.Max(0, minY - 2); maxX = Mathf.Min(w - 1, maxX + 2); maxY = Mathf.Min(h - 1, maxY + 2);
            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            var crop = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++) crop[y * cw + x] = px[(y + minY) * w + x + minX];

            string path = $"{Folder}/{file}.png";
            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            tex.SetPixels32(crop);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 160f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            importer.SetTextureSettings(settings);
            importer.spritePivot = new Vector2((pivotPx.x - minX) / cw, (pivotPx.y - minY) / ch);
            importer.SaveAndReimport();
        }
    }
}
