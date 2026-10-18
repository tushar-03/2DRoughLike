using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>Procedural tiles and backdrop layers for the Ember Catacombs: cool carved stone lit by warm ember glow.</summary>
    public static class EnvironmentPainter
    {
        /// <summary>The colours a biome's tiles are painted with.</summary>
        public struct Palette
        {
            public Color Mortar, Stone, StoneHi, Moss, Ember;

            /// <summary>Cool carved blue-grey stone, green moss, ember mortar seams.</summary>
            public static Palette Catacombs => new Palette
            {
                Mortar = new Color(0.07f, 0.08f, 0.12f), Stone = new Color(0.30f, 0.33f, 0.43f), StoneHi = new Color(0.52f, 0.57f, 0.70f),
                Moss = new Color(0.20f, 0.40f, 0.36f), Ember = new Color(1f, 0.52f, 0.18f)
            };

            /// <summary>Rust-brown basalt, dry orange lichen, bright cracks: a ruined city at sunset.</summary>
            public static Palette Promenade => new Palette
            {
                Mortar = new Color(0.09f, 0.055f, 0.06f), Stone = new Color(0.36f, 0.26f, 0.25f), StoneHi = new Color(0.72f, 0.5f, 0.40f),
                Moss = new Color(0.62f, 0.34f, 0.14f), Ember = new Color(1f, 0.72f, 0.28f)
            };
        }

        /// <summary>Set before painting a biome's tiles.</summary>
        public static Palette Current = Palette.Catacombs;

        static Color Mortar => Current.Mortar;
        static Color Stone => Current.Stone;
        static Color StoneHi => Current.StoneHi;
        static Color Moss => Current.Moss;
        static Color Ember => Current.Ember;

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

        // ------------------------------------------------------------------ Ashen Promenade backdrops

        static void Over(Color32[] dst, Color32[] src)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                var c = src[i];
                if (c.a == 0) continue;
                float a = c.a / 255f;
                var d = dst[i];
                dst[i] = new Color32((byte)(c.r * a + d.r * (1 - a)), (byte)(c.g * a + d.g * (1 - a)), (byte)(c.b * a + d.b * (1 - a)), 255);
            }
        }

        /// <summary>A huge low sun over hazy, ruined towers at the end of the day.</summary>
        public static Color32[] BackdropFarPromenade()
        {
            var px = new Color32[BackW * BackH];
            for (int y = 0; y < BackH; y++)
            {
                float v = y / (float)BackH;
                Color top = new Color(0.20f, 0.08f, 0.22f), mid = new Color(0.78f, 0.28f, 0.2f), horizon = new Color(1f, 0.74f, 0.34f), floor = new Color(0.45f, 0.18f, 0.16f);
                Color c = v > 0.55f ? Color.Lerp(mid, top, (v - 0.55f) / 0.45f)
                        : v > 0.28f ? Color.Lerp(horizon, mid, (v - 0.28f) / 0.27f)
                        : Color.Lerp(floor, horizon, v / 0.28f);
                for (int x = 0; x < BackW; x++)
                {
                    float u = x / (float)BackW;
                    float haze = Fbm(u * 2f, v * 3f, 4, 91);
                    Color pc = c * (0.88f + haze * 0.3f);
                    // the sun and its glow (kept inside the tile so the layer repeats cleanly)
                    float dx = Mathf.Min(Mathf.Abs(x - 380f), BackW - Mathf.Abs(x - 380f)), dy = y - 0.30f * BackH;
                    float d2 = dx * dx + dy * dy;
                    float glow = Mathf.Exp(-d2 / (2f * 170f * 170f));
                    pc += new Color(1f, 0.72f, 0.3f) * glow * 0.55f;
                    if (d2 < 110f * 110f) pc = Color.Lerp(pc, new Color(1f, 0.95f, 0.7f), 0.9f);
                    // streaky clouds
                    float cloud = Noise(u * 6f, v * 24f, 24, 92) * Mathf.Clamp01(1f - Mathf.Abs(v - 0.62f) * 4f);
                    if (cloud > 0.62f) pc = Color.Lerp(pc, new Color(1f, 0.6f, 0.4f), (cloud - 0.62f) * 1.4f);
                    pc.a = 1f;
                    px[y * BackW + x] = To32(pc);
                }
            }

            // distant towers, dark and purple
            var p = new Painter(BackW, BackH);
            Mat far = Mat.Glow(new Color(0.34f, 0.14f, 0.24f));
            for (int k = 0; k < 2; k++)
            {
                float o = k * 640f;
                p.Poly(new[] { new Vector2(o + 110f, 0f), new Vector2(o + 205f, 0f), new Vector2(o + 200f, 330f), new Vector2(o + 188f, 350f), new Vector2(o + 180f, 318f), new Vector2(o + 150f, 360f), new Vector2(o + 126f, 330f), new Vector2(o + 112f, 340f) }, far);
                p.Poly(new[] { new Vector2(o + 300f, 0f), new Vector2(o + 372f, 0f), new Vector2(o + 366f, 230f), new Vector2(o + 340f, 262f), new Vector2(o + 318f, 214f), new Vector2(o + 304f, 240f) }, far);
                p.Poly(new[] { new Vector2(o + 470f, 0f), new Vector2(o + 560f, 0f), new Vector2(o + 548f, 290f), new Vector2(o + 520f, 306f), new Vector2(o + 498f, 270f), new Vector2(o + 474f, 300f) }, far);
                p.Poly(new[] { new Vector2(o + 205f, 150f), new Vector2(o + 300f, 160f), new Vector2(o + 300f, 188f), new Vector2(o + 205f, 176f) }, far);       // a bridge between towers
            }
            Over(px, p.Render(0f, Color.black));
            return px;
        }

        /// <summary>Ruined gate towers, broken arches and a leaning bell tower, with a few windows still lit.</summary>
        public static Color32[] BackdropMidPromenade()
        {
            var p = new Painter(BackW, BackH);
            Mat dark = Mat.Of(new Color(0.16f, 0.09f, 0.11f), new Color(0.9f, 0.5f, 0.3f), 5f);
            Mat darker = Mat.Of(new Color(0.11f, 0.06f, 0.08f), new Color(0.6f, 0.3f, 0.2f), 4f);
            Mat lit = Mat.Glow(new Color(1f, 0.7f, 0.3f));
            for (int k = 0; k < 2; k++)
            {
                float o = k * 640f;
                // a broken gate tower
                p.Poly(new[] { new Vector2(o + 60f, 0f), new Vector2(o + 190f, 0f), new Vector2(o + 184f, 520f), new Vector2(o + 168f, 548f), new Vector2(o + 150f, 500f), new Vector2(o + 124f, 556f), new Vector2(o + 100f, 508f), new Vector2(o + 70f, 540f) }, dark);
                p.Poly(new[] { new Vector2(o + 112f, 330f), new Vector2(o + 134f, 330f), new Vector2(o + 134f, 392f), new Vector2(o + 123f, 408f), new Vector2(o + 112f, 392f) }, lit);
                p.Poly(new[] { new Vector2(o + 112f, 180f), new Vector2(o + 134f, 180f), new Vector2(o + 134f, 230f), new Vector2(o + 123f, 242f), new Vector2(o + 112f, 230f) }, lit);
                // an arch leaning against it, half collapsed
                p.Poly(new[] { new Vector2(o + 200f, 0f), new Vector2(o + 250f, 0f), new Vector2(o + 254f, 300f), new Vector2(o + 300f, 380f), new Vector2(o + 360f, 410f), new Vector2(o + 372f, 366f), new Vector2(o + 316f, 330f), new Vector2(o + 292f, 250f), new Vector2(o + 288f, 0f), new Vector2(o + 258f, 0f) }, darker);
                // a leaning bell tower
                p.Poly(new[] { new Vector2(o + 420f, 0f), new Vector2(o + 520f, 0f), new Vector2(o + 548f, 430f), new Vector2(o + 560f, 470f), new Vector2(o + 484f, 520f), new Vector2(o + 440f, 470f), new Vector2(o + 428f, 400f) }, dark);
                p.Ellipse(new Vector2(o + 500f, 446f), 22f, 28f, 0f, Mat.Of(new Color(0.95f, 0.7f, 0.3f), Color.white, 4f));      // the bell
                p.Poly(new[] { new Vector2(o + 458f, 250f), new Vector2(o + 482f, 250f), new Vector2(o + 484f, 306f), new Vector2(o + 470f, 322f), new Vector2(o + 456f, 306f) }, lit);
                // fallen blocks
                p.Poly(new[] { new Vector2(o + 372f, 0f), new Vector2(o + 410f, 52f), new Vector2(o + 448f, 30f), new Vector2(o + 440f, 0f) }, darker);
            }
            Color32[] raw = p.Render(0f, Color.black);
            // sunset haze: the base of everything fades toward the warm fog
            for (int y = 0; y < BackH; y++)
            {
                float fog = Mathf.Clamp01(y / 260f);
                for (int x = 0; x < BackW; x++)
                {
                    var c = raw[y * BackW + x];
                    if (c.a == 0) continue;
                    float k = Mathf.Lerp(0.4f, 1f, fog);
                    c.r = (byte)Mathf.Lerp(c.r, 190, (1f - k) * 0.7f); c.g = (byte)Mathf.Lerp(c.g, 100, (1f - k) * 0.7f); c.b = (byte)Mathf.Lerp(c.b, 70, (1f - k) * 0.7f);
                    raw[y * BackW + x] = c;
                }
            }
            return raw;
        }

        /// <summary>Near silhouettes: broken beams, hanging chains, torn flags and drifting rubble.</summary>
        public static Color32[] BackdropNearPromenade()
        {
            var p = new Painter(BackW, BackH);
            Mat sil = Mat.Glow(new Color(0.07f, 0.035f, 0.05f));
            Mat flag = Mat.Of(new Color(0.42f, 0.1f, 0.08f), new Color(1f, 0.5f, 0.3f), 3f);
            for (int k = 0; k < 2; k++)
            {
                float o = k * 640f;
                p.Poly(new[] { new Vector2(o + 0f, BackH), new Vector2(o + 260f, BackH), new Vector2(o + 250f, BackH - 70f), new Vector2(o + 170f, BackH - 96f), new Vector2(o + 60f, BackH - 60f) }, sil);                      // broken beam top left
                p.Poly(new[] { new Vector2(o + 400f, BackH), new Vector2(o + 640f, BackH), new Vector2(o + 640f, BackH - 56f), new Vector2(o + 520f, BackH - 110f), new Vector2(o + 440f, BackH - 70f) }, sil);
                for (int j = 0; j < 7; j++) p.Capsule(new Vector2(o + 330f, BackH - 80f - j * 26f), new Vector2(o + 330f, BackH - 100f - j * 26f), 4f, sil);       // a chain
                p.Poly(new[] { new Vector2(o + 322f, BackH - 270f), new Vector2(o + 372f, BackH - 262f), new Vector2(o + 366f, BackH - 372f), new Vector2(o + 346f, BackH - 344f), new Vector2(o + 326f, BackH - 384f) }, flag);   // a torn flag
                p.Poly(new[] { new Vector2(o + 40f, 0f), new Vector2(o + 70f, 70f), new Vector2(o + 130f, 40f), new Vector2(o + 170f, 0f) }, sil);                                                              // rubble
                p.Poly(new[] { new Vector2(o + 470f, 0f), new Vector2(o + 500f, 52f), new Vector2(o + 560f, 26f), new Vector2(o + 590f, 0f) }, sil);
                p.Poly(new[] { new Vector2(o + 240f, 420f), new Vector2(o + 274f, 440f), new Vector2(o + 262f, 470f), new Vector2(o + 230f, 452f) }, sil);                                                       // floating debris
                p.Poly(new[] { new Vector2(o + 520f, 560f), new Vector2(o + 548f, 574f), new Vector2(o + 538f, 598f), new Vector2(o + 512f, 584f) }, sil);
            }
            var raw = p.Render(0f, Color.black);
            for (int i = 0; i < raw.Length; i++) if (raw[i].a > 0) raw[i].a = (byte)(raw[i].a * 0.92f);
            return raw;
        }

        // ------------------------------------------------------------------ foreground and extra depth layers

        static Mat Dark(float tone = 1f) => Mat.Glow(new Color(0.025f, 0.025f, 0.045f) * tone);

        static Color32[] Fade(Color32[] raw, float alpha)
        {
            for (int i = 0; i < raw.Length; i++) if (raw[i].a > 0) raw[i].a = (byte)(raw[i].a * alpha);
            return raw;
        }

        /// <summary>Catacombs foreground, bottom edge: jagged rubble and stalagmites, passing in front of the player.</summary>
        public static Color32[] ForegroundBottomCatacombs()
        {
            var p = new Painter(BackW, BackH);
            Mat rock = Dark();
            float[] xs = { 70f, 210f, 380f, 520f, 760f, 900f, 1060f, 1190f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], h = 50f + (i * 37 % 60), w = 40f + (i * 23 % 40);
                p.Poly(new[] { new Vector2(x - w, 0f), new Vector2(x - w * 0.3f, h * 0.7f), new Vector2(x + 4f, h), new Vector2(x + w * 0.5f, h * 0.55f), new Vector2(x + w, 0f) }, rock);
                if (i % 2 == 0) p.Poly(new[] { new Vector2(x + w, 0f), new Vector2(x + w * 1.4f, h * 0.4f), new Vector2(x + w * 2f, 0f) }, rock);
            }
            p.Capsule(new Vector2(0f, 10f), new Vector2(BackW, 10f), 12f, rock);
            return Fade(p.Render(0f, Color.black), 0.96f);
        }

        /// <summary>Catacombs foreground, top edge: hanging roots, chains and stalactites.</summary>
        public static Color32[] ForegroundTopCatacombs()
        {
            var p = new Painter(BackW, BackH);
            Mat rock = Dark();
            Mat chain = Mat.Of(new Color(0.07f, 0.07f, 0.1f), new Color(0.3f, 0.32f, 0.45f), 3f);
            p.Capsule(new Vector2(0f, BackH - 8f), new Vector2(BackW, BackH - 8f), 14f, rock);
            float[] xs = { 120f, 330f, 560f, 830f, 1050f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], len = 40f + (i * 29 % 50);
                p.Poly(new[] { new Vector2(x - 30f, BackH), new Vector2(x + 30f, BackH), new Vector2(x + 4f, BackH - len - 30f) }, rock);
                // a hanging chain, each with a lantern or a hook
                float cx = x + 80f;
                for (int j = 0; j < 4 + i % 3; j++) p.Capsule(new Vector2(cx, BackH - 20f - j * 18f), new Vector2(cx, BackH - 34f - j * 18f), 3.5f, chain);
            }
            // roots
            for (int i = 0; i < 6; i++)
            {
                float x = 40f + i * 220f;
                p.Poly(new[] { new Vector2(x, BackH), new Vector2(x + 14f, BackH), new Vector2(x + 30f + (i % 2) * 20f, BackH - 70f - i * 5f), new Vector2(x + 20f, BackH - 96f) }, rock);
            }
            return Fade(p.Render(0f, Color.black), 0.94f);
        }

        /// <summary>Catacombs: small hanging lanterns drifting in the middle distance, glowing through the haze.</summary>
        public static Color32[] BackdropLanterns()
        {
            var p = new Painter(BackW, BackH);
            Mat chain = Mat.Glow(new Color(0.09f, 0.1f, 0.16f));
            Mat glow = Mat.Glow(new Color(1f, 0.62f, 0.28f));
            Mat halo = Mat.Glow(new Color(0.55f, 0.3f, 0.2f, 0.16f));
            float[] xs = { 90f, 260f, 440f, 640f, 800f, 980f, 1160f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], y = 330f + (i * 53 % 330);
                p.Capsule(new Vector2(x, BackH), new Vector2(x, y + 24f), 2f, chain);
                p.Circle(new Vector2(x, y), 26f, halo);
                p.Poly(new[] { new Vector2(x - 12f, y + 20f), new Vector2(x + 12f, y + 20f), new Vector2(x + 16f, y - 16f), new Vector2(x - 16f, y - 16f) }, glow);
                p.Capsule(new Vector2(x - 18f, y + 22f), new Vector2(x + 18f, y + 22f), 3f, chain);
            }
            var raw = p.Render(0f, Color.black);
            // soften: the halo is only a faint glow, the lantern itself stays crisp
            for (int i = 0; i < raw.Length; i++) if (raw[i].a > 0) raw[i].a = (byte)(raw[i].a * 0.55f);
            return raw;
        }

        /// <summary>Promenade foreground, bottom edge: broken fence posts, dead grass tufts and rubble.</summary>
        public static Color32[] ForegroundBottomPromenade()
        {
            var p = new Painter(BackW, BackH);
            Mat sil = Dark();
            Mat ember = Mat.Glow(new Color(1f, 0.55f, 0.2f));
            float[] posts = { 100f, 330f, 610f, 880f, 1130f };
            for (int i = 0; i < posts.Length; i++)
            {
                float x = posts[i], h = 70f + (i * 31 % 50);
                p.Poly(new[] { new Vector2(x - 11f, 0f), new Vector2(x + 11f, 0f), new Vector2(x + 8f, h), new Vector2(x - 2f, h + 14f), new Vector2(x - 10f, h - 8f) }, sil);
                if (i % 2 == 0) p.Capsule(new Vector2(x, h * 0.6f), new Vector2(x + 130f, h * 0.45f), 4f, sil);
            }
            for (int i = 0; i < 26; i++)
            {
                float x = i * 50f + (i * 17 % 30), h = 26f + (i * 13 % 36);
                p.Poly(new[] { new Vector2(x - 7f, 0f), new Vector2(x + (i % 2 == 0 ? 10f : -10f), h), new Vector2(x + 7f, 0f) }, sil);
            }
            for (int i = 0; i < 7; i++) p.Circle(new Vector2(60f + i * 190f, 20f + (i * 41 % 50)), 2.6f, ember);   // embers caught in the weeds
            p.Capsule(new Vector2(0f, 8f), new Vector2(BackW, 8f), 10f, sil);
            return Fade(p.Render(0f, Color.black), 0.97f);
        }

        /// <summary>Promenade foreground, top edge: torn banners, beams and hanging chains.</summary>
        public static Color32[] ForegroundTopPromenade()
        {
            var p = new Painter(BackW, BackH);
            Mat sil = Dark();
            Mat flag = Mat.Of(new Color(0.18f, 0.04f, 0.04f), new Color(0.8f, 0.3f, 0.2f), 3f);
            p.Poly(new[] { new Vector2(0f, BackH), new Vector2(BackW, BackH), new Vector2(BackW, BackH - 22f), new Vector2(900f, BackH - 34f), new Vector2(480f, BackH - 18f), new Vector2(0f, BackH - 30f) }, sil);
            float[] xs = { 160f, 470f, 760f, 1080f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i];
                p.Poly(new[] { new Vector2(x - 24f, BackH - 20f), new Vector2(x + 24f, BackH - 20f), new Vector2(x + 20f, BackH - 110f - i * 8f), new Vector2(x, BackH - 86f - i * 8f), new Vector2(x - 20f, BackH - 120f - i * 8f) }, flag);
                for (int j = 0; j < 5; j++) p.Capsule(new Vector2(x + 52f, BackH - 22f - j * 18f), new Vector2(x + 52f, BackH - 36f - j * 18f), 3.2f, sil);
            }
            p.Poly(new[] { new Vector2(300f, BackH), new Vector2(380f, BackH), new Vector2(350f, BackH - 70f) }, sil);
            return Fade(p.Render(0f, Color.black), 0.95f);
        }

        /// <summary>Promenade: floating chunks of ruin and dust clouds in the distant sky.</summary>
        public static Color32[] BackdropIslands()
        {
            var p = new Painter(BackW, BackH);
            Mat island = Mat.Glow(new Color(0.30f, 0.12f, 0.2f));
            float[] xs = { 140f, 420f, 700f, 1010f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], y = 430f + (i * 67 % 200), w = 70f + (i * 29 % 40);
                p.Poly(new[] { new Vector2(x - w, y), new Vector2(x + w, y), new Vector2(x + w * 0.6f, y - 30f), new Vector2(x + w * 0.2f, y - 80f - i * 8f), new Vector2(x - w * 0.3f, y - 52f), new Vector2(x - w * 0.8f, y - 22f) }, island);
                p.Poly(new[] { new Vector2(x - w * 0.5f, y), new Vector2(x + w * 0.3f, y + 24f), new Vector2(x + w * 0.1f, y + 60f) }, island);   // ruined spire on top
            }
            return Fade(p.Render(0f, Color.black), 0.7f);
        }

        // ------------------------------------------------------------------ more planes of depth

        /// <summary>Soft horizontal bands of mist that tile sideways. Layered between the other planes they separate near from far.</summary>
        public static Color32[] FogBands(Color tint, float strength, int seed)
        {
            var px = new Color32[BackW * BackH];
            var rng = new System.Random(seed);
            // a few bands at different heights, each with its own slow ripple that repeats exactly across the tile width
            int bands = 4;
            float[] y = new float[bands], thick = new float[bands], phase = new float[bands], amp = new float[bands];
            for (int b = 0; b < bands; b++)
            {
                y[b] = Mathf.Lerp(190f, 560f, (b + (float)rng.NextDouble() * 0.6f) / bands);
                thick[b] = 34f + (float)rng.NextDouble() * 38f;
                phase[b] = (float)rng.NextDouble() * 6.28f;
                amp[b] = 0.55f + (float)rng.NextDouble() * 0.45f;
            }
            for (int py = 0; py < BackH; py++)
                for (int pxx = 0; pxx < BackW; pxx++)
                {
                    float a = 0f;
                    float u = pxx / (float)BackW * Mathf.PI * 2f;
                    for (int b = 0; b < bands; b++)
                    {
                        float wobble = Mathf.Sin(u * (2 + b) + phase[b]) * 18f + Mathf.Sin(u * (5 + b * 2) + phase[b] * 2f) * 7f;
                        float d = (py - y[b] - wobble) / thick[b];
                        float body = Mathf.Exp(-d * d);
                        float density = 0.5f + 0.5f * Mathf.Sin(u * (3 + b) + phase[b] * 3f);
                        a += body * Mathf.Lerp(0.35f, 1f, density) * amp[b];
                    }
                    a = Mathf.Clamp01(a * strength);
                    px[py * BackW + pxx] = new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), (byte)(a * 255f));
                }
            return px;
        }

        /// <summary>Catacombs: a distant arcade of pointed arches, dark blue against the haze.</summary>
        public static Color32[] BackdropArches()
        {
            var p = new Painter(BackW, BackH);
            Mat stone = Mat.Glow(new Color(0.10f, 0.12f, 0.2f));
            Mat rib = Mat.Glow(new Color(0.13f, 0.15f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                float x = i * 320f;
                p.Capsule(new Vector2(x + 24f, 130f), new Vector2(x + 24f, 430f), 22f, 18f, stone);
                p.Capsule(new Vector2(x + 24f, 430f), new Vector2(x + 24f + 140f, 560f), 12f, 7f, rib);
                p.Capsule(new Vector2(x + 24f + 280f, 430f), new Vector2(x + 24f + 140f, 560f), 12f, 7f, rib);
                p.Capsule(new Vector2(x + 24f + 280f, 130f), new Vector2(x + 24f + 280f, 430f), 22f, 18f, stone);
                p.Circle(new Vector2(x + 164f, 330f), 34f, rib);                                  // rose window ghost
            }
            return Fade(p.Render(0f, Color.black), 0.8f);
        }

        /// <summary>Catacombs: thick near-black columns that sweep past in front of the player, fading toward the sides.</summary>
        public static Color32[] ForegroundPillarsCatacombs()
        {
            var p = new Painter(BackW, BackH);
            Mat col = Dark(0.8f);
            float[] xs = { 150f, 760f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], w = 54f + i * 14f;
                p.Poly(new[] { new Vector2(x - w, 0f), new Vector2(x + w, 0f), new Vector2(x + w * 0.85f, BackH), new Vector2(x - w * 0.85f, BackH) }, col);
                p.Poly(new[] { new Vector2(x - w * 1.5f, 120f), new Vector2(x + w * 1.5f, 120f), new Vector2(x + w, 170f), new Vector2(x - w, 170f) }, col);   // base block
                p.Poly(new[] { new Vector2(x - w * 1.5f, BackH - 150f), new Vector2(x + w * 1.5f, BackH - 150f), new Vector2(x + w, BackH - 200f), new Vector2(x - w, BackH - 200f) }, col);   // capital
            }
            return Fade(p.Render(0f, Color.black), 0.62f);
        }

        /// <summary>Promenade: a skyline of broken columns and leaning arches far behind the ruins.</summary>
        public static Color32[] BackdropBrokenColumns()
        {
            var p = new Painter(BackW, BackH);
            Mat far = Mat.Glow(new Color(0.34f, 0.14f, 0.18f));
            float[] xs = { 90f, 250f, 470f, 640f, 860f, 1060f, 1210f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], top = 330f + (i * 71 % 180);
                p.Poly(new[] { new Vector2(x - 24f, 100f), new Vector2(x + 24f, 100f), new Vector2(x + 20f, top), new Vector2(x + 6f, top + 22f), new Vector2(x - 4f, top + 6f), new Vector2(x - 20f, top + 30f) }, far);
                if (i % 2 == 0) p.Capsule(new Vector2(x + 24f, top - 6f), new Vector2(x + 120f, top - 52f), 10f, 6f, far);   // a fallen architrave leaning on its neighbour
            }
            return Fade(p.Render(0f, Color.black), 0.75f);
        }

        /// <summary>Promenade: heavy broken columns in the foreground, edges catching the sunset.</summary>
        public static Color32[] ForegroundPillarsPromenade()
        {
            var p = new Painter(BackW, BackH);
            Mat col = Dark(1.1f);
            Mat edge = Mat.Glow(new Color(0.5f, 0.2f, 0.1f));
            float[] xs = { 260f, 900f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], w = 50f + i * 18f, top = BackH - 130f - i * 60f;
                p.Poly(new[] { new Vector2(x - w, 0f), new Vector2(x + w, 0f), new Vector2(x + w * 0.9f, top), new Vector2(x + w * 0.4f, top + 40f), new Vector2(x - w * 0.2f, top - 20f), new Vector2(x - w * 0.9f, top + 30f) }, col);
                p.Capsule(new Vector2(x - w * 0.95f, 40f), new Vector2(x - w * 0.88f, top + 20f), 3f, edge);    // sunlit rim
                p.Poly(new[] { new Vector2(x - w * 1.5f, 90f), new Vector2(x + w * 1.5f, 90f), new Vector2(x + w, 150f), new Vector2(x - w, 150f) }, col);
            }
            return Fade(p.Render(0f, Color.black), 0.66f);
        }
    }
}
