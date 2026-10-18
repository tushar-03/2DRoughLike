using UnityEngine;
using UnityEngine.UI;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// The "TWO SOULS" logo, built from text and two procedurally drawn soul-flames (one warm, one cool) that flank the title and
    /// flicker on their own rhythm, over an ornamental divider. Everything is generated, so it needs no image files.
    /// </summary>
    public sealed class TitleLogo : MonoBehaviour
    {
        RectTransform _left, _right;
        Image _leftGlow, _rightGlow;
        float _t;

        public static TitleLogo Create(RectTransform parent, float top)
        {
            var root = UiKit.Node("Logo", parent);
            UiKit.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(1400f, 300f));
            var logo = root.gameObject.AddComponent<TitleLogo>();
            logo.Build(root);
            return logo;
        }

        void Build(RectTransform root)
        {
            Color gold = new Color(1f, 0.86f, 0.5f);

            // text: dark extrusion below, bright face on top, a thin outline for crispness
            for (int i = 0; i < 2; i++)
            {
                var t = UiKit.Label(root, i == 0 ? "Title Depth" : "Title", "TWO SOULS", 150, i == 0 ? new Color(0.45f, 0.24f, 0.08f) : gold, TextAnchor.MiddleCenter);
                t.font = GameFonts.Display;
                t.fontStyle = FontStyle.Normal;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, i == 0 ? 24f : 32f), new Vector2(1300f, 200f));
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.95f);
                outline.effectDistance = new Vector2(3f, -3f);
                if (i == 1)
                {
                    var glow = t.gameObject.AddComponent<Shadow>();
                    glow.effectColor = new Color(1f, 0.6f, 0.2f, 0.35f);
                    glow.effectDistance = new Vector2(0f, 0f);
                }
            }

            // soul flames either side of the title
            _left = Flame(root, "Soul Warm", new Color(1f, 0.62f, 0.2f), new Vector2(-770f, 40f), out _leftGlow);
            _right = Flame(root, "Soul Cool", new Color(0.4f, 0.9f, 1f), new Vector2(770f, 40f), out _rightGlow);

            // divider: a line, a diamond, and two small dots
            var line = UiKit.Image(root, "Divider", new Color(1f, 0.82f, 0.45f, 0.75f));
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(760f, 3f));
            var diamond = UiKit.Image(root, "Diamond", new Color(1f, 0.9f, 0.6f));
            UiKit.Place(diamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(26f, 26f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            foreach (float x in new[] { -60f, 60f })
            {
                var dot = UiKit.Image(root, "Dot", new Color(1f, 0.82f, 0.45f));
                dot.sprite = ProceduralSprites.Circle;
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -82f), new Vector2(10f, 10f));
            }
            // the two ends of the divider carry the two soul colours
            var warm = UiKit.Image(root, "Warm End", new Color(1f, 0.62f, 0.2f, 0.9f));
            UiKit.Place(warm.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-385f, -82f), new Vector2(14f, 14f));
            warm.sprite = ProceduralSprites.Circle;
            var cool = UiKit.Image(root, "Cool End", new Color(0.4f, 0.9f, 1f, 0.9f));
            UiKit.Place(cool.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(385f, -82f), new Vector2(14f, 14f));
            cool.sprite = ProceduralSprites.Circle;
        }

        RectTransform Flame(RectTransform root, string name, Color colour, Vector2 pos, out Image glow)
        {
            var holder = UiKit.Node(name, root);
            UiKit.Place(holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(150f, 220f));

            glow = UiKit.Image(holder, "Glow", new Color(colour.r, colour.g, colour.b, 0.55f));
            glow.sprite = SoftGlow();
            UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 260f));

            var body = UiKit.Image(holder, "Flame", Color.white);
            body.sprite = FlameSprite(colour);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 220f));
            return holder;
        }

        static Sprite _softGlow;

        /// <summary>A round glow that fades smoothly to nothing at the edge.</summary>
        static Sprite SoftGlow()
        {
            if (_softGlow != null) return _softGlow;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f)) / (N * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply(false, true);
            return _softGlow = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A teardrop soul-flame with a bright core, drawn into a small texture.</summary>
        static Sprite FlameSprite(Color colour)
        {
            const int W = 96, H = 144;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color core = Color.Lerp(colour, Color.white, 0.85f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);                       // 0 bottom .. 1 tip
                // half width of a teardrop: round belly low down, narrowing to a curled tip
                float belly = Mathf.Sin(Mathf.Clamp01(v / 0.55f) * Mathf.PI * 0.5f);
                float half = (v < 0.55f ? belly : Mathf.Pow(1f - (v - 0.55f) / 0.45f, 1.6f)) * 0.46f * W;
                float centre = W * 0.5f + Mathf.Sin(v * 5f) * 4f * v;
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Abs(x - centre);
                    float edge = half - d;
                    float a = Mathf.Clamp01(edge / 4f);
                    Color c = Color.Lerp(colour * 0.85f, colour, v);
                    float coreAmt = Mathf.Clamp01(1f - d / Mathf.Max(1f, half * 0.55f)) * Mathf.Clamp01(1.15f - v * 1.2f);
                    c = Color.Lerp(c, core, coreAmt);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            Flicker(_left, _leftGlow, 0f);
            Flicker(_right, _rightGlow, 1.7f);
        }

        void Flicker(RectTransform r, Image glow, float offset)
        {
            if (r == null) return;
            float a = Mathf.Sin(_t * 3.1f + offset), b = Mathf.Sin(_t * 7.3f + offset * 2f);
            r.localScale = new Vector3(1f + 0.05f * b, 1f + 0.08f * a + 0.03f * b, 1f);
            r.localPosition = new Vector3(r.localPosition.x, r.localPosition.y + 0f, 0f);
            r.localRotation = Quaternion.Euler(0f, 0f, a * 2.5f);
            if (glow != null)
            {
                var c = glow.color;
                c.a = 0.32f + 0.12f * (0.5f + 0.5f * Mathf.Sin(_t * 2.2f + offset));
                glow.color = c;
            }
        }
    }
}
