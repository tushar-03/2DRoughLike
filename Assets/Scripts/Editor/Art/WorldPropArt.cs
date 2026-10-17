using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>The set pieces: exit door with a shimmering portal, treasure chest, mutation altar and the salvage merchant.</summary>
    public static class WorldPropArt
    {
        public const int W = 320, H = 448;
        public const float Floor = 28f;
        const float CX = 160f;
        static readonly Color Ink = new Color(0.07f, 0.05f, 0.1f);

        static Mat Stone(float tone = 1f) => Mat.Of(new Color(0.3f, 0.33f, 0.45f) * tone, new Color(0.65f, 0.7f, 0.9f), 5f);
        static Mat Gold => Mat.Of(new Color(0.95f, 0.74f, 0.3f), Color.white, 4f);
        static Mat Wood => Mat.Of(new Color(0.5f, 0.32f, 0.2f), new Color(1f, 0.7f, 0.4f), 4f);
        static Mat Iron => Mat.Of(new Color(0.2f, 0.21f, 0.28f), new Color(0.7f, 0.74f, 0.9f), 3f);

        // ------------------------------------------------------------------ door

        public static Color32[] Door(int i, int n)
        {
            var p = new Painter(W, H);
            float ph = (float)i / n * Mathf.PI * 2f;
            Mat stone = Stone(), stoneDark = Stone(0.7f);
            float baseY = Floor;

            // portal glow behind the opening (drawn first so the frame overlaps it)
            Mat teal = Mat.Glow(new Color(0.12f, 0.45f, 0.5f));
            Mat tealMid = Mat.Glow(new Color(0.22f, 0.72f, 0.74f));
            Mat tealHi = Mat.Glow(new Color(0.7f, 0.98f, 0.95f));
            p.Poly(new[] { new Vector2(CX - 62f, baseY), new Vector2(CX + 62f, baseY), new Vector2(CX + 62f, baseY + 250f), new Vector2(CX + 38f, baseY + 310f), new Vector2(CX, baseY + 340f), new Vector2(CX - 38f, baseY + 310f), new Vector2(CX - 62f, baseY + 250f) }, teal);
            p.Ellipse(new Vector2(CX, baseY + 170f), 44f, 130f, 0f, tealMid);
            p.Ellipse(new Vector2(CX + Mathf.Sin(ph) * 5f, baseY + 170f + Mathf.Cos(ph) * 8f), 22f, 90f, 0f, tealHi);
            for (int k = 0; k < 3; k++)
            {
                float y = baseY + 60f + k * 80f + Mathf.Sin(ph + k * 2f) * 14f;
                p.Capsule(new Vector2(CX - 40f, y), new Vector2(CX + 40f, y + Mathf.Sin(ph * 2f + k) * 16f), 2.6f, tealHi);
            }

            // pillars with capitals, plinths and a pointed arch
            foreach (float s in new[] { -1f, 1f })
            {
                float x = CX + s * 86f;
                p.Poly(new[] { new Vector2(x - 26f, baseY), new Vector2(x + 26f, baseY), new Vector2(x + 22f, baseY + 270f), new Vector2(x - 22f, baseY + 270f) }, stone);
                p.Poly(new[] { new Vector2(x - 34f, baseY), new Vector2(x + 34f, baseY), new Vector2(x + 30f, baseY + 26f), new Vector2(x - 30f, baseY + 26f) }, stoneDark);
                p.Poly(new[] { new Vector2(x - 36f, baseY + 262f), new Vector2(x + 36f, baseY + 262f), new Vector2(x + 28f, baseY + 296f), new Vector2(x - 28f, baseY + 296f) }, stoneDark);
                // a carved rune on each pillar
                p.Circle(new Vector2(x, baseY + 150f), 9f, Mat.Glow(new Color(0.3f, 0.85f, 0.85f)));
            }
            EnvironmentPainter.ArchHalf(p, CX - 112f, CX, baseY + 286f, 78f, 40f, true, stone);
            EnvironmentPainter.ArchHalf(p, CX - 112f, CX, baseY + 286f, 78f, 40f, false, stone);
            p.Poly(new[] { new Vector2(CX - 14f, baseY + 352f), new Vector2(CX + 14f, baseY + 352f), new Vector2(CX, baseY + 384f) }, Gold);   // keystone
            return p.Render(3.2f, Ink);
        }

        // ------------------------------------------------------------------ chest

        public static Color32[] Chest(bool open)
        {
            var p = new Painter(W, H);
            float y = Floor;
            Mat body = Wood, band = Iron, lock_ = Gold;
            Mat inside = Mat.Of(new Color(0.18f, 0.1f, 0.08f));

            p.Poly(new[] { new Vector2(CX - 74f, y), new Vector2(CX + 74f, y), new Vector2(CX + 70f, y + 74f), new Vector2(CX - 70f, y + 74f) }, body);
            p.Capsule(new Vector2(CX - 74f, y + 38f), new Vector2(CX + 74f, y + 38f), 4f, 4f, Mat.Of(new Color(0.34f, 0.2f, 0.12f), null));   // plank seam
            foreach (float s in new[] { -1f, 1f }) p.Poly(new[] { new Vector2(CX + s * 52f - 9f, y), new Vector2(CX + s * 52f + 9f, y), new Vector2(CX + s * 52f + 9f, y + 74f), new Vector2(CX + s * 52f - 9f, y + 74f) }, band);

            if (!open)
            {
                p.Ellipse(new Vector2(CX, y + 74f), 72f, 34f, 0f, body);
                p.Poly(new[] { new Vector2(CX - 72f, y + 74f), new Vector2(CX + 72f, y + 74f), new Vector2(CX + 72f, y + 66f), new Vector2(CX - 72f, y + 66f) }, body);
                foreach (float s in new[] { -1f, 1f }) p.Ellipse(new Vector2(CX + s * 52f, y + 82f), 9f, 30f, 0f, band);
                p.Circle(new Vector2(CX, y + 70f), 13f, lock_);
                p.Circle(new Vector2(CX, y + 70f), 4f, Mat.Glow(Ink));
            }
            else
            {
                // lid thrown back, golden light spilling out
                p.Ellipse(new Vector2(CX, y + 76f), 66f, 20f, 0f, inside);
                p.Ellipse(new Vector2(CX, y + 96f), 56f, 40f, 0f, Mat.Glow(new Color(1f, 0.85f, 0.4f)));
                p.Ellipse(new Vector2(CX, y + 100f), 34f, 24f, 0f, Mat.Glow(new Color(1f, 0.97f, 0.8f)));
                p.Poly(new[] { new Vector2(CX - 66f, y + 82f), new Vector2(CX + 66f, y + 82f), new Vector2(CX + 60f, y + 140f), new Vector2(CX + 38f, y + 156f), new Vector2(CX - 38f, y + 156f), new Vector2(CX - 60f, y + 140f) }, body);
                p.Ellipse(new Vector2(CX, y + 126f), 52f, 22f, 0f, Mat.Of(new Color(0.34f, 0.2f, 0.12f), null));
                p.Circle(new Vector2(CX, y + 64f), 10f, lock_);
            }
            return p.Render(3.2f, Ink);
        }

        // ------------------------------------------------------------------ altar

        public static Color32[] Altar()
        {
            var p = new Painter(W, H);
            float y = Floor;
            Mat stone = Stone(), dark = Stone(0.72f);
            Mat rune = Mat.Glow(new Color(0.78f, 0.45f, 1f));

            // stepped base, pillar, bowl
            p.Poly(new[] { new Vector2(CX - 80f, y), new Vector2(CX + 80f, y), new Vector2(CX + 70f, y + 30f), new Vector2(CX - 70f, y + 30f) }, dark);
            p.Poly(new[] { new Vector2(CX - 60f, y + 30f), new Vector2(CX + 60f, y + 30f), new Vector2(CX + 50f, y + 56f), new Vector2(CX - 50f, y + 56f) }, stone);
            p.Poly(new[] { new Vector2(CX - 28f, y + 56f), new Vector2(CX + 28f, y + 56f), new Vector2(CX + 22f, y + 120f), new Vector2(CX - 22f, y + 120f) }, stone);
            p.Poly(new[] { new Vector2(CX - 54f, y + 140f), new Vector2(CX + 54f, y + 140f), new Vector2(CX + 30f, y + 112f), new Vector2(CX - 30f, y + 112f) }, dark);
            p.Capsule(new Vector2(CX - 56f, y + 142f), new Vector2(CX + 56f, y + 142f), 6f, Gold);
            // glowing runes
            foreach (float x in new[] { -34f, 0f, 34f }) p.Circle(new Vector2(CX + x, y + 14f), 6f, rune);
            p.Capsule(new Vector2(CX, y + 66f), new Vector2(CX, y + 104f), 3f, rune);
            return p.Render(3.2f, Ink);
        }

        // ------------------------------------------------------------------ merchant

        public static Color32[] Merchant()
        {
            var p = new Painter(W, H);
            float y = Floor;
            Mat robe = Mat.Of(new Color(0.22f, 0.45f, 0.5f), new Color(0.7f, 1f, 1f), 5f);
            Mat robeDark = Mat.Of(new Color(0.13f, 0.28f, 0.34f));
            Mat skin = Mat.Of(new Color(0.55f, 0.7f, 0.62f), new Color(0.8f, 1f, 0.9f), 4f);
            Mat counter = Wood;
            Mat glow = Mat.Glow(new Color(1f, 0.82f, 0.35f));

            // the merchant, standing behind the counter, with a heavy pack
            p.Ellipse(new Vector2(CX - 70f, y + 110f), 38f, 48f, 0f, Mat.Of(new Color(0.4f, 0.3f, 0.2f), new Color(1f, 0.7f, 0.4f), 4f));   // pack
            p.Poly(new[] { new Vector2(CX - 70f, y), new Vector2(CX - 8f, y), new Vector2(CX - 22f, y + 150f), new Vector2(CX - 54f, y + 150f) }, robe);
            p.Circle(new Vector2(CX - 40f, y + 178f), 28f, robe);
            p.Ellipse(new Vector2(CX - 32f, y + 176f), 19f, 22f, 0f, Mat.Of(new Color(0.07f, 0.05f, 0.1f), null));
            p.Ellipse(new Vector2(CX - 24f, y + 180f), 7f, 5f, 12f, glow);
            p.Capsule(new Vector2(CX - 20f, y + 130f), new Vector2(CX + 22f, y + 90f), 9f, robeDark);       // arm over the counter
            p.Circle(new Vector2(CX + 26f, y + 88f), 8f, skin);

            // counter with goods
            p.Poly(new[] { new Vector2(CX - 30f, y), new Vector2(CX + 120f, y), new Vector2(CX + 120f, y + 78f), new Vector2(CX - 30f, y + 78f) }, counter);
            p.Capsule(new Vector2(CX - 34f, y + 80f), new Vector2(CX + 124f, y + 80f), 7f, Mat.Of(new Color(0.62f, 0.42f, 0.26f), new Color(1f, 0.8f, 0.5f), 4f));
            p.Capsule(new Vector2(CX - 30f, y + 40f), new Vector2(CX + 120f, y + 40f), 3f, Mat.Of(new Color(0.34f, 0.2f, 0.12f), null));
            // bottles, a gem and a lantern on top
            p.Capsule(new Vector2(CX + 10f, y + 90f), new Vector2(CX + 10f, y + 120f), 8f, Mat.Of(new Color(0.4f, 0.8f, 0.9f), Color.white, 3f));
            p.Capsule(new Vector2(CX + 44f, y + 90f), new Vector2(CX + 44f, y + 112f), 9f, Mat.Of(new Color(0.85f, 0.4f, 0.5f), Color.white, 3f));
            p.Poly(new[] { new Vector2(CX + 78f, y + 88f), new Vector2(CX + 92f, y + 106f), new Vector2(CX + 78f, y + 124f), new Vector2(CX + 64f, y + 106f) }, Mat.Glow(new Color(0.5f, 0.95f, 1f)));
            p.Capsule(new Vector2(CX + 106f, y + 88f), new Vector2(CX + 106f, y + 112f), 8f, Iron);
            p.Circle(new Vector2(CX + 106f, y + 122f), 11f, glow);
            return p.Render(3.2f, Ink);
        }
    }
}
