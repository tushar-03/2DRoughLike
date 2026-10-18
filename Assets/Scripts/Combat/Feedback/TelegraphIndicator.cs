using System.Collections.Generic;
using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Consistent visual language for incoming attacks, all made of dots that pop in, shimmer and light up as the hit approaches
    /// and flash white exactly when it lands. Melee swings are ONE dotted arc around the reach of the blow, sweeping from top to
    /// bottom like the slash itself; lasers and shots are a dotted line that ends in an arrowhead; explosions are a dotted ring
    /// that fills inward-out. Readable by shape and motion, not only by colour (colourblind-safe), and it hides far less of
    /// the scene than a filled box. The same indicator is used by every enemy and boss.
    /// </summary>
    public class TelegraphIndicator : MonoBehaviour
    {
        public static readonly Color StandardColour = new Color(1f, 0.55f, 0.1f);
        public static readonly Color UnparryableColour = new Color(0.8f, 0.3f, 1f);

        const float Spacing = 0.3f;
        const float DotSize = 0.12f;
        const int MaxDots = 220;

        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
        readonly List<Vector2> _points = new List<Vector2>();     // local positions
        readonly List<float> _phase = new List<float>();          // 0..1: when each dot lights up
        readonly List<float> _wave = new List<float>();           // position used for the travelling shimmer
        readonly List<float> _big = new List<float>();            // 1 for the arrowhead dots, which are drawn larger
        Vector2 _size;
        int _facing = 1;
        float _duration, _elapsed;
        bool _running, _circle;
        Color _colour = StandardColour;

        void Awake() => Build();

        bool _built;

        void Build()
        {
            if (_built) return;
            _built = true;
            gameObject.SetActive(false);
        }

        /// <summary>Creates dots on demand, so a pool of indicators costs nothing until a telegraph actually needs them.</summary>
        void EnsureDots(int count)
        {
            while (_dots.Count < count && _dots.Count < MaxDots)
            {
                var go = new GameObject("Dot");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralSprites.Circle;
                sr.sortingOrder = 40;
                go.SetActive(false);
                _dots.Add(sr);
            }
        }

        /// <summary>Starts a telegraph covering <paramref name="size"/> centred on <paramref name="centre"/>.</summary>
        public void Begin(Vector2 centre, Vector2 size, int facing, float duration, bool unparryable = false, float angleDeg = 0f, bool circle = false)
        {
            Build();
            _circle = circle;
            transform.position = centre;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            _size = size;
            _facing = facing;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _running = true;
            _colour = unparryable ? UnparryableColour : StandardColour;
            gameObject.SetActive(true);
            Layout();
            Apply(0f);
        }

        /// <summary>Moves and resizes the danger area while it is still running (aim lines that track the player).</summary>
        public void Retarget(Vector2 centre, Vector2 size, float angleDeg)
        {
            if (!_running) return;
            transform.position = centre;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            if ((size - _size).sqrMagnitude > 0.0004f) { _size = size; Layout(); }
            Apply(Mathf.Clamp01(_elapsed / _duration));
        }

        public void Hide()
        {
            _running = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_running) return;
            _elapsed += Time.deltaTime;
            Apply(Mathf.Clamp01(_elapsed / _duration));
        }

        // ------------------------------------------------------------------ layout

        void Layout()
        {
            _points.Clear();
            _phase.Clear();
            _wave.Clear();
            _big.Clear();
            if (_circle) LayoutRing(); else if (_size.y < 0.45f) LayoutLine(); else LayoutArc();
            EnsureDots(_points.Count);
        }

        void Add(Vector2 p, float phase, float wave = -1f, float big = 0f)
        {
            if (_points.Count >= MaxDots) return;
            _points.Add(p);
            _phase.Add(Mathf.Clamp01(phase));
            _wave.Add(wave < 0f ? phase : wave);
            _big.Add(big);
        }

        /// <summary>When a point along the attack direction lights up: the attacker's side first.</summary>
        float AlongPhase(float x)
        {
            float u = (x * _facing + _size.x * 0.5f) / Mathf.Max(0.01f, _size.x);
            return u;
        }

        void LayoutLine()
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(_size.x / Spacing));
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(-_size.x * 0.5f, _size.x * 0.5f, i / (float)n);
                Add(new Vector2(x, 0f), AlongPhase(x));
            }
            // arrowhead pointing the way the shot travels
            float tip = _size.x * 0.5f * _facing;
            for (int k = 1; k <= 3; k++)
            {
                float back = -k * 0.2f * _facing;
                Add(new Vector2(tip + back, k * 0.13f), 1f, 1f, 1f);
                Add(new Vector2(tip + back, -k * 0.13f), 1f, 1f, 1f);
            }
        }

        /// <summary>A single dotted crescent covering the blow's reach, drawn top to bottom like the swing.</summary>
        void LayoutArc()
        {
            float hw = _size.x * 0.5f, hh = _size.y * 0.5f;
            // half an ellipse that starts at the attacker's edge: x from -hw out to +hw at the middle, y over the full height
            float length = Mathf.PI * Mathf.Sqrt((hw * hw + hh * hh) * 0.5f * 2f) * 0.5f * 1.1f;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / Spacing), 8, 70);
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, u);
                float x = (-hw + 2f * hw * Mathf.Cos(a)) * _facing;
                Add(new Vector2(x, hh * Mathf.Sin(a)), u);
            }
        }

        void LayoutRing()
        {
            float r = Mathf.Max(_size.x, _size.y) * 0.5f;
            int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r / Spacing), 12, 90);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0f, i / (float)n);
            }
            // an inner ring that sweeps outward as the blast charges (phase = its radius fraction)
            for (float f = 0.25f; f < 0.99f; f += 0.25f)
            {
                int m = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r * f / (Spacing * 1.3f)));
                for (int i = 0; i < m; i++)
                {
                    float a = i / (float)m * Mathf.PI * 2f;
                    Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * f, f);
                }
            }
        }

        // ------------------------------------------------------------------ drawing

        void Apply(float t)
        {
            float clock = Time.time;
            float pop = Mathf.Clamp01(_elapsed / 0.18f);
            float popScale = pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.5f) * (1f + 0.35f * Mathf.Sin(pop * Mathf.PI)) : 1f;
            for (int i = 0; i < _dots.Count; i++)
            {
                var dot = _dots[i];
                if (i >= _points.Count)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);

                float phase = _phase[i];
                bool inner = _circle && phase > 0.01f;
                float lit = Mathf.Clamp01((t - phase) * 8f + 1f);       // 0 = not reached yet, 1 = fully lit
                if (_circle) lit = inner ? (t >= phase ? 1f : 0.12f) : Mathf.Lerp(0.45f, 1f, t);

                // a shimmer travels along the dots toward the target; it speeds up as the hit gets close
                float speed = Mathf.Lerp(7f, 15f, t);
                float wave = 0.5f + 0.5f * Mathf.Sin(clock * speed - _wave[i] * (_circle ? 18f : 11f));
                // the leading edge of the fill glows
                float head = _circle ? 0f : Mathf.Clamp01(1f - Mathf.Abs(t - phase) * 7f);

                float size = DotSize * Mathf.Lerp(1f, 1.45f, lit) * (1f + 0.4f * wave * lit) * (1f + 0.55f * head);
                if (_big[i] > 0f) size *= 1.25f;
                if (inner) size *= 0.8f;
                if (t > 0.92f) size *= 1f + (t - 0.92f) * 9f;                // the dots swell as the blow lands
                dot.transform.localPosition = _points[i];
                dot.transform.localScale = Vector3.one * size * popScale;

                Color c = _colour;
                c.a = Mathf.Lerp(0.22f, 0.95f, lit) * Mathf.Lerp(0.8f, 1f, wave);
                c = Color.Lerp(c, Color.white, head * 0.6f);
                if (t > 0.92f) c = Color.Lerp(c, Color.white, (t - 0.92f) * 8f * 0.8f);   // flash white right before the hit
                c.a = Mathf.Clamp01(c.a);
                dot.color = c;
            }
        }
    }
}
