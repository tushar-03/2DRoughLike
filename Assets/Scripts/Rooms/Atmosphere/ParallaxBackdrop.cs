using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>
    /// Endless parallax scenery behind the level. Each layer is three copies of a tileable sprite that follow the camera by a
    /// fraction (far layers follow closely, so they barely move) and wrap, so the backdrop never ends and never needs to match
    /// the room grid. Runs after the camera has moved this frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ParallaxBackdrop : MonoBehaviour
    {
        sealed class Layer
        {
            public EnvironmentArt.BackdropLayer Def;
            public Transform[] Copies;
            public float TileW;
        }

        sealed class Mote
        {
            public Transform T;
            public Vector2 Home;     // position inside the tile of the field, in world units
            public float Depth, Phase, Speed, Size;
        }

        const float FieldW = 30f, FieldH = 16f;

        Layer[] _layers = new Layer[0];
        Mote[] _motes = new Mote[0];
        Camera _cam;

        public static ParallaxBackdrop Create(EnvironmentArt art)
        {
            var go = new GameObject("Parallax Backdrop");
            var b = go.AddComponent<ParallaxBackdrop>();
            b.Build(art);
            return b;
        }

        void Build(EnvironmentArt art)
        {
            var list = new System.Collections.Generic.List<Layer>();
            foreach (var def in art.backdrop)
            {
                if (def == null || def.sprite == null) continue;
                var layer = new Layer { Def = def, Copies = new Transform[3], TileW = def.sprite.bounds.size.x };
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject($"{def.name} {i}");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = def.sprite;
                    sr.color = def.tint;
                    sr.sortingOrder = def.sortingOrder;
                    layer.Copies[i] = go.transform;
                }
                list.Add(layer);
            }
            _layers = list.ToArray();
            BuildMotes(art);
        }

        /// <summary>Dust, embers or fireflies at several depths: closer ones sweep past faster and look bigger.</summary>
        void BuildMotes(EnvironmentArt art)
        {
            if (art.moteCount <= 0) return;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 8f)) / 8f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            var rng = new System.Random(7);
            var list = new Mote[art.moteCount];
            for (int i = 0; i < list.Length; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 55;
                float depth = (float)rng.NextDouble();      // 0 far .. 1 near
                var c = art.moteColour;
                c.a *= Mathf.Lerp(0.5f, 1f, depth);
                sr.color = c;
                list[i] = new Mote
                {
                    T = go.transform,
                    Home = new Vector2((float)rng.NextDouble() * FieldW, (float)rng.NextDouble() * FieldH),
                    Depth = depth,
                    Phase = (float)rng.NextDouble() * 6.28f,
                    Speed = 0.4f + (float)rng.NextDouble() * 0.8f,
                    Size = Mathf.Lerp(0.07f, 0.22f, depth)
                };
                go.transform.localScale = Vector3.one * list[i].Size * 2f;
            }
            _motes = list;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            Vector3 cam = _cam.transform.position;

            foreach (var l in _layers)
            {
                float offset = -Mathf.Repeat(cam.x * (1f - l.Def.follow), l.TileW);
                // drift slowly as the camera climbs, but never far enough to slide off the layer
                float delta = Mathf.Clamp((1f - l.Def.followY) * cam.y, -3.5f, 3.5f);
                float y = cam.y - delta + l.Def.yOffset;
                for (int i = 0; i < l.Copies.Length; i++)
                    l.Copies[i].position = new Vector3(cam.x + offset + (i - 1) * l.TileW, y, 0f);
            }

            float t = Time.time;
            foreach (var m in _motes)
            {
                // nearer motes slide against the camera faster (follow < 0 is in front of the action)
                float follow = Mathf.Lerp(0.6f, -0.45f, m.Depth);
                float x = Mathf.Repeat(m.Home.x + t * m.Speed * 0.3f - cam.x * (1f - follow), FieldW) - FieldW * 0.5f;
                float yy = Mathf.Repeat(m.Home.y + t * m.Speed * 0.15f - cam.y * (1f - Mathf.Lerp(0.8f, 0.2f, m.Depth)), FieldH) - FieldH * 0.5f;
                yy += Mathf.Sin(t * m.Speed + m.Phase) * 0.25f;
                m.T.position = new Vector3(cam.x + x, cam.y + yy, 0f);
            }
        }
    }
}
