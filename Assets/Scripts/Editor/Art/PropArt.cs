using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>Animated dungeon props painted into the shared "Props" sheet: wall torch and floor brazier.</summary>
    public static class PropArt
    {
        public const int W = 128, H = 192;
        static readonly Color Ink = new Color(0.07f, 0.05f, 0.1f);

        static void Flame(Painter p, Vector2 baseCentre, float scale, float phase)
        {
            Mat outer = Mat.Glow(new Color(1f, 0.46f, 0.12f));
            Mat mid = Mat.Glow(new Color(1f, 0.74f, 0.22f));
            Mat core = Mat.Glow(new Color(1f, 0.96f, 0.7f));
            float sway = Mathf.Sin(phase) * 6f * scale;
            float sway2 = Mathf.Sin(phase * 2f + 1f) * 4f * scale;

            p.Poly(new[] { baseCentre + new Vector2(-16f * scale, 0f), baseCentre + new Vector2(-9f * scale + sway2, 44f * scale), baseCentre + new Vector2(sway, 74f * scale), baseCentre + new Vector2(12f * scale + sway2, 40f * scale), baseCentre + new Vector2(16f * scale, 0f) }, outer);
            p.Poly(new[] { baseCentre + new Vector2(-10f * scale, 0f), baseCentre + new Vector2(-4f * scale + sway2, 36f * scale), baseCentre + new Vector2(sway * 0.8f, 56f * scale), baseCentre + new Vector2(8f * scale + sway2, 30f * scale), baseCentre + new Vector2(10f * scale, 0f) }, mid);
            p.Poly(new[] { baseCentre + new Vector2(-5f * scale, 0f), baseCentre + new Vector2(sway * 0.5f, 28f * scale), baseCentre + new Vector2(5f * scale, 0f) }, core);
            // two stray sparks
            p.Circle(baseCentre + new Vector2(Mathf.Sin(phase * 1.3f) * 14f * scale, (50f + 16f * Mathf.Repeat(phase / 6.28f, 1f)) * scale), 2.4f * scale, core);
        }

        public static Color32[] Torch(int i, int n)
        {
            var p = new Painter(W, H);
            float ph = (float)i / n * Mathf.PI * 2f;
            Mat iron = Mat.Of(new Color(0.16f, 0.16f, 0.22f), new Color(0.6f, 0.62f, 0.8f), 3f);
            Mat wood = Mat.Of(new Color(0.34f, 0.2f, 0.14f), new Color(0.9f, 0.5f, 0.3f), 3f);
            // bracket, handle, cup
            p.Poly(new[] { new Vector2(54f, 4f), new Vector2(74f, 4f), new Vector2(70f, 40f), new Vector2(58f, 40f) }, iron);
            p.Capsule(new Vector2(64f, 24f), new Vector2(64f, 92f), 8f, 6f, wood);
            p.Poly(new[] { new Vector2(40f, 98f), new Vector2(88f, 98f), new Vector2(78f, 76f), new Vector2(50f, 76f) }, iron);
            Flame(p, new Vector2(64f, 96f), 1f, ph);
            return p.Render(2.6f, Ink);
        }

        /// <summary>A free-standing fire for the top of a stone post: a bed of embers and a tall flame.</summary>
        public static Color32[] Flame(int i, int n)
        {
            var p = new Painter(W, H);
            float ph = (float)i / n * Mathf.PI * 2f;
            Mat ember = Mat.Of(new Color(0.9f, 0.34f, 0.1f), new Color(1f, 0.8f, 0.4f), 3f);
            Mat coal = Mat.Of(new Color(0.22f, 0.1f, 0.1f), new Color(0.9f, 0.4f, 0.2f), 2.5f);
            p.Ellipse(new Vector2(64f, 6f), 30f, 9f, 0f, coal);
            p.Circle(new Vector2(52f, 8f), 8f, ember);
            p.Circle(new Vector2(72f, 9f), 9f, ember);
            Flame(p, new Vector2(64f, 8f), 1.9f, ph);
            return p.Render(2.6f, Ink);
        }

        public static Color32[] Brazier(int i, int n)
        {
            var p = new Painter(W, H);
            float ph = (float)i / n * Mathf.PI * 2f;
            Mat iron = Mat.Of(new Color(0.18f, 0.18f, 0.25f), new Color(0.65f, 0.67f, 0.85f), 3f);
            Mat gold = Mat.Of(new Color(0.9f, 0.68f, 0.28f), Color.white, 3f);
            // legs, stem, bowl
            p.Capsule(new Vector2(40f, 4f), new Vector2(56f, 52f), 5f, iron);
            p.Capsule(new Vector2(88f, 4f), new Vector2(72f, 52f), 5f, iron);
            p.Capsule(new Vector2(64f, 10f), new Vector2(64f, 58f), 7f, iron);
            p.Poly(new[] { new Vector2(26f, 78f), new Vector2(102f, 78f), new Vector2(90f, 50f), new Vector2(38f, 50f) }, iron);
            p.Capsule(new Vector2(24f, 80f), new Vector2(104f, 80f), 5f, gold);
            Flame(p, new Vector2(64f, 80f), 1.35f, ph);
            return p.Render(2.6f, Ink);
        }
    }
}
