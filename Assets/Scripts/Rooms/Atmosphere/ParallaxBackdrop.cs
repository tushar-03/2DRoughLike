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

        Layer[] _layers = new Layer[0];
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
        }
    }
}
