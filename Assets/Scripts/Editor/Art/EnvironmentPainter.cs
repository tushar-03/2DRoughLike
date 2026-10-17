using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>Procedural tiles and backdrop layers for the Ember Catacombs: cool carved stone lit by warm ember glow.</summary>
    public static class EnvironmentPainter
    {
        static readonly Color Mortar = new Color(0.07f, 0.08f, 0.12f);
        static readonly Color Stone = new Color(0.30f, 0.33f, 0.43f);
        static readonly Color StoneHi = new Color(0.52f, 0.57f, 0.70f);
        static readonly Color Moss = new Color(0.20f, 0.40f, 0.36f);
        static readonly Color Ember = new Color(1f, 0.52f, 0.18f);

        // ------------------------------------------------------------------ noise (periodic so tiles repeat seamlessly)

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Smooth value noise on a lattice that wraps every <paramref name="period"/> cells.</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period), ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Fbm(float u, float v, int basePeriod, int seed)
        {
            float sum = 0f, amp = 0.5f;
            int period = basePeriod;
            for (int o = 0; o < 3; o++)
            {
                sum += Noise(u * period, v * period, period, seed + o * 17) * amp;
                period *= 2; amp *= 0.5f;
            }
            return sum;   // 0..~0.875
        }

        static Color32 To32(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));

        // ------------------------------------------------------------------ wall

        /// <summary>256x256 tile = 2x2 world units: four courses of staggered bricks, bevelled, mossy, with a few glowing cracks.</summary>
        public static Color32[] Wall()
        {
            const int N = 256, rowH = 64, brickW = 128;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int row = y / rowH;
                int shift = (row % 2) * (brickW / 2);
                int bx = Mod(x + shift, N);
                int brick = bx / brickW;
                int lx = bx % brickW, ly = y % rowH;           // local position inside the brick cell
                float u = x / (float)N, v = y / (float)N;
                float id = Hash(brick + row * 7, row, 3);

                Color c;
                bool mortar = lx < 4 || ly < 4;
                if (mortar)
                {
                    c = Mortar * (0.85f + Noise(u * 40f, v * 40f, 40, 5) * 0.4f);
                    // a few mortar seams glow like embers under the stone
                    if (Hash(brick, row, 9) > 0.86f && ly < 4 && Noise(u * 24f, v * 24f, 24, 11) > 0.55f) c = Color.Lerp(c, Ember * 0.9f, 0.7f);
                }
                else
                {
                    float tone = 0.86f + id * 0.3f;
                    float grain = Fbm(u, v, 6, 21);
                    c = Stone * tone * (0.82f + grain * 0.45f);
                    float fine = Noise(u * 120f, v * 120f, 120, 31);
                    c *= 0.95f + fine * 0.1f;

                    // bevel: lit from the upper left
                    if (ly > rowH - 8) c = Color.Lerp(c, StoneHi, 0.34f * (ly - (rowH - 8)) / 8f);
                    if (lx < 9) c = Color.Lerp(c, StoneHi, 0.22f * (9 - lx) / 5f);
                    if (ly < 11) c *= 0.74f + 0.26f * (ly - 4) / 7f;
                    if (lx > brickW - 9) c *= 0.82f + 0.18f * (brickW - lx) / 9f;

                    // moss creeping up from the bottom of some bricks
                    float mossN = Fbm(u + 0.31f, v + 0.17f, 5, 41);
                    if (mossN > 0.5f && ly < 22 && id > 0.45f) c = Color.Lerp(c, Moss * (0.8f + fine * 0.5f), Mathf.Clamp01((mossN - 0.5f) * 5f) * 0.8f);

                    // hairline cracks
                    float crack = Noise(u * 18f + id * 9f, v * 18f, 18, 51);
                    if (Mathf.Abs(crack - 0.5f) < 0.012f && id > 0.55f) c *= 0.55f;
                }
                c.a = 1f;
                px[y * N + x] = To32(c);
            }
            return px;
        }

        /// <summary>Slightly darker, plainer variant for ceilings and deep walls.</summary>
        public static Color32[] WallDark()
        {
            var px = Wall();
            for (int i = 0; i < px.Length; i++)
            {
                px[i].r = (byte)(px[i].r * 0.62f); px[i].g = (byte)(px[i].g * 0.64f); px[i].b = (byte)(px[i].b * 0.72f);
            }
            return px;
        }

        // ------------------------------------------------------------------ floor trim

        /// <summary>256x64 strip (2 x 0.5 units) laid along the top of floors: a lit stone lip over a ragged, mossy fringe.</summary>
        public static Color32[] FloorTrim()
        {
            const int W = 256, H = 64;
            var px = new Color32[W * H];
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                // ragged lower edge
                float fringe = 22f + Noise(u * 14f, 0.5f, 14, 61) * 18f + Noise(u * 40f, 0.2f, 40, 62) * 7f;
                for (int y = 0; y < H; y++)
                {
                    int fromTop = H - 1 - y;
                    if (fromTop > H - 1 - (H - fringe)) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }

                    float n = Noise(u * 60f, y / (float)H * 8f, 60, 63);
                    Color c;
                    if (fromTop < 3) c = StoneHi * 1.15f;                                  // bright top edge
                    else if (fromTop < 14) c = Color.Lerp(StoneHi, Stone, (fromTop - 3) / 11f) * (0.95f + n * 0.1f);
                    else c = Stone * (0.72f - (fromTop - 14) * 0.004f) * (0.9f + n * 0.2f);

                    // flagstone seams
                    if (fromTop > 3 && fromTop < 30 && (x % 128 < 3)) c *= 0.55f;
                    // moss dripping off the front
                    float mossN = Noise(u * 22f, 0.3f, 22, 64);
                    if (fromTop > 10 && mossN > 0.52f && fromTop < 10 + (mossN - 0.5f) * 90f) c = Color.Lerp(c, Moss, 0.75f);
                    // lower rim shadow
                    if (fromTop > H - 1 - (H - fringe) - 5) c *= 0.55f;
                    c.a = 1f;
                    px[y * W + x] = To32(c);
                }
            }
            return px;
        }

        // ------------------------------------------------------------------ platform

        /// <summary>256x48 strip (2 x 0.375 units): a carved stone ledge with a gold inlay and rivets.</summary>
        public static Color32[] Platform()
        {
            const int W = 256, H = 48;
            var px = new Color32[W * H];
            Color gold = new Color(0.95f, 0.72f, 0.28f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int fromTop = H - 1 - y;
                float u = x / (float)W;
                float n = Noise(u * 50f, y / (float)H * 6f, 50, 71);
                Color c;
                if (fromTop < 4) c = StoneHi * 1.2f;
                else if (fromTop < 9) c = gold * (0.85f + 0.15f * Mathf.Sin(u * Mathf.PI * 2f * 8f));   // inlay
                else if (fromTop < 30) c = Stone * (0.95f - (fromTop - 9) * 0.01f) * (0.92f + n * 0.16f);
                else c = Stone * 0.5f * (0.9f + n * 0.2f);
                if (fromTop >= 9 && fromTop < 30 && x % 64 < 2) c *= 0.6f;
                // rivets
                float dx = (x % 64) - 32f, dy = fromTop - 18f;
                if (dx * dx + dy * dy < 14f) c = Color.Lerp(c, StoneHi * 1.1f, 0.85f);
                if (fromTop >= H - 3) c *= 0.5f;
                // rounded underside
                float edge = fromTop > 36 ? (fromTop - 36) * 0.9f : 0f;
                float alpha = fromTop > 44 ? 0f : 1f;
                c.a = alpha;
                px[y * W + x] = To32(c);
            }
            return px;
        }

        // ------------------------------------------------------------------ backdrop layers (1280x768, horizontally tiling, 32 px per unit)

        public const int BackW = 1280, BackH = 768;

        static float Periodic(float x, float period) => Mathf.Repeat(x, period);

        /// <summary>Furthest layer: indigo haze with a low ember glow and tall pointed windows letting in soft light shafts.</summary>
        public static Color32[] BackdropFar()
        {
            var px = new Color32[BackW * BackH];
            for (int y = 0; y < BackH; y++)
            {
                float v = y / (float)BackH;                 // 0 = bottom
                Color top = new Color(0.05f, 0.05f, 0.11f), mid = new Color(0.11f, 0.12f, 0.24f), low = new Color(0.34f, 0.17f, 0.15f);
                Color c = v > 0.45f ? Color.Lerp(mid, top, (v - 0.45f) / 0.55f) : Color.Lerp(low, mid, v / 0.45f);
                for (int x = 0; x < BackW; x++)
                {
                    float u = x / (float)BackW;
                    float haze = Fbm(u, v * 0.6f, 4, 81);
                    Color pc = c * (0.85f + haze * 0.35f);

                    // tall pointed windows every 320 px, cool moonlight with a warm glow where it meets the floor
                    float wx = Mathf.Abs(Periodic(x, 320f) - 160f);
                    float winTop = 0.64f + 0.13f * (1f - wx / 50f);
                    bool inWindow = wx < 50f && v > 0.30f && v < winTop;
                    if (inWindow)
                    {
                        float t = Mathf.Clamp01((v - 0.30f) / 0.34f);
                        Color moon = Color.Lerp(new Color(0.55f, 0.42f, 0.38f), new Color(0.30f, 0.42f, 0.68f), t);
                        pc = Color.Lerp(pc, moon, 0.7f);
                        float frame = Mathf.Min(50f - wx, Mathf.Abs(v - 0.30f) * 700f);
                        if (frame < 7f || wx < 2.5f || Mathf.Abs(v - 0.47f) < 0.005f || Mathf.Abs(v - 0.56f) < 0.004f) pc *= 0.28f;      // stone frame and mullions
                    }
                    // faint god-rays leaning from each window
                    float ray = Mathf.Clamp01(1f - Mathf.Abs(Periodic(x - (v - 0.3f) * -260f, 320f) - 160f) / 90f) * Mathf.Clamp01((0.62f - v) * 2f) * 0.10f;
                    pc += new Color(0.9f, 0.65f, 0.4f) * ray;
                    pc.a = 1f;
                    px[y * BackW + x] = To32(pc);
                }
            }
            return px;
        }

        /// <summary>One half of a pointed arch as a ring segment. The arc is centred on the springline so the two halves meet at a point.</summary>
        public static void ArchHalf(Painter p, float x0, float xm, float ys, float h, float thick, bool left, Mat m)
        {
            float cx = (xm * xm - x0 * x0 + h * h) / (2f * (xm - x0));
            float r = Mathf.Abs(cx - x0);
            float a0 = Mathf.Atan2(0f, x0 - cx), a1 = Mathf.Atan2(h, xm - cx);
            const int N = 14;
            var pts = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= N; i++)
            {
                float a = Mathf.LerpAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg, i / (float)N) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, ys + Mathf.Sin(a) * r));
            }
            for (int i = N; i >= 0; i--)
            {
                float a = Mathf.LerpAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg, i / (float)N) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * (r - thick), ys + Mathf.Sin(a) * (r - thick)));
            }
            if (!left)
            {
                // mirror around the apex x
                for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(2f * xm - pts[i].x, pts[i].y);
            }
            p.Poly(pts.ToArray(), m);
        }

        /// <summary>Middle layer: a colonnade of fluted columns joined by pointed arches, fading into fog at the bottom.</summary>
        public static Color32[] BackdropMid()
        {
            var p = new Painter(BackW, BackH);
            Mat col = Mat.Of(new Color(0.16f, 0.18f, 0.28f), new Color(0.5f, 0.55f, 0.75f), 6f);
            Mat capital = Mat.Of(new Color(0.22f, 0.24f, 0.36f), new Color(0.6f, 0.62f, 0.8f), 5f);
            Mat slim = Mat.Of(new Color(0.12f, 0.14f, 0.22f));
            for (int k = -1; k <= 2; k++)
            {
                float cx = 160f + k * 640f, nx = cx + 640f;
                // main column with capital and plinth
                p.Poly(new[] { new Vector2(cx - 50f, 0f), new Vector2(cx + 50f, 0f), new Vector2(cx + 42f, 470f), new Vector2(cx - 42f, 470f) }, col);
                for (int f = -1; f <= 1; f++) p.Capsule(new Vector2(cx + f * 22f, 30f), new Vector2(cx + f * 19f, 450f), 2.5f, Mat.Of(new Color(0.1f, 0.12f, 0.2f), null));   // fluting
                p.Poly(new[] { new Vector2(cx - 76f, 456f), new Vector2(cx + 76f, 456f), new Vector2(cx + 56f, 516f), new Vector2(cx - 56f, 516f) }, capital);
                p.Poly(new[] { new Vector2(cx - 68f, 0f), new Vector2(cx + 68f, 0f), new Vector2(cx + 60f, 44f), new Vector2(cx - 60f, 44f) }, capital);
                // pointed arch springing from the capital to the next column, doubled for depth
                float x0 = cx + 62f, x1 = nx - 62f, xm = (x0 + x1) * 0.5f;
                ArchHalf(p, x0, xm, 504f, 190f, 42f, true, capital);
                ArchHalf(p, x0, xm, 504f, 190f, 42f, false, capital);
                // a slim secondary column and a little tracery oculus under each arch
                float mx = cx + 320f;
                p.Poly(new[] { new Vector2(mx - 20f, 0f), new Vector2(mx + 20f, 0f), new Vector2(mx + 15f, 330f), new Vector2(mx - 15f, 330f) }, slim);
            }
            Color32[] raw = p.Render(0f, Color.black);

            // fog at the foot of the columns and a slight overall transparency
            for (int y = 0; y < BackH; y++)
            {
                float fog = Mathf.Clamp01(y / 240f);
                for (int x = 0; x < BackW; x++)
                {
                    var c = raw[y * BackW + x];
                    if (c.a == 0) continue;
                    float k = Mathf.Lerp(0.45f, 1f, fog);
                    c.r = (byte)Mathf.Lerp(c.r, 50, 1f - k); c.g = (byte)Mathf.Lerp(c.g, 42, 1f - k); c.b = (byte)Mathf.Lerp(c.b, 60, 1f - k);
                    c.a = (byte)(c.a * 0.94f);
                    raw[y * BackW + x] = c;
                }
            }
            return raw;
        }

        /// <summary>Nearest layer: near-black silhouettes of hanging chains, banners and stalactites framing the top, rubble at the bottom.</summary>
        public static Color32[] BackdropNear()
        {
            var p = new Painter(BackW, BackH);
            Mat dark = Mat.Of(new Color(0.045f, 0.04f, 0.08f));
            Mat banner = Mat.Of(new Color(0.22f, 0.07f, 0.1f), new Color(0.7f, 0.3f, 0.25f), 4f);
            Mat link = Mat.Of(new Color(0.08f, 0.08f, 0.12f), new Color(0.35f, 0.38f, 0.5f), 3f);
            for (int k = 0; k < 2; k++)
            {
                float o = k * 640f;
                // stalactites
                float[] sx = { 60f, 190f, 330f, 470f, 560f };
                float[] sl = { 150f, 90f, 200f, 70f, 120f };
                for (int i = 0; i < sx.Length; i++)
                    p.Poly(new[] { new Vector2(o + sx[i] - 42f, BackH), new Vector2(o + sx[i] + 42f, BackH), new Vector2(o + sx[i] + 6f * (i % 2 == 0 ? 1 : -1), BackH - sl[i]) }, dark);
                // hanging chain with a banner
                float cx = o + 250f;
                for (int j = 0; j < 6; j++) p.Capsule(new Vector2(cx, BackH - 20f - j * 26f), new Vector2(cx, BackH - 40f - j * 26f), 5f, 5f, link);
                p.Poly(new[] { new Vector2(cx - 30f, BackH - 190f), new Vector2(cx + 30f, BackH - 190f), new Vector2(cx + 30f, BackH - 330f), new Vector2(cx, BackH - 298f), new Vector2(cx - 30f, BackH - 330f) }, banner);
                p.Capsule(new Vector2(cx - 38f, BackH - 190f), new Vector2(cx + 38f, BackH - 190f), 4f, link);
                // rubble at the bottom
                p.Poly(new[] { new Vector2(o + 400f, 0f), new Vector2(o + 440f, 56f), new Vector2(o + 500f, 38f), new Vector2(o + 560f, 70f), new Vector2(o + 620f, 0f) }, dark);
                p.Poly(new[] { new Vector2(o + 20f, 0f), new Vector2(o + 70f, 44f), new Vector2(o + 130f, 22f), new Vector2(o + 160f, 0f) }, dark);
            }
            var raw = p.Render(0f, Color.black);
            for (int i = 0; i < raw.Length; i++) if (raw[i].a > 0) raw[i].a = (byte)(raw[i].a * 0.9f);
            return raw;
        }
    }
}
