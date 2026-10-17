using System.Collections.Generic;
using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Consistent visual language for incoming attacks: a dotted outline of the danger area. The dots light up from the attacker
    /// toward the far edge and are all bright exactly when the hit lands. Lasers are a single dotted line, explosions a dotted
    /// ring that fills inward-out. Readable by shape and motion, not only by colour (colourblind-safe), and it hides far less of
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
            if (_circle) LayoutRing(); else if (_size.y < 0.45f) LayoutLine(); else LayoutRectangle();
            EnsureDots(_points.Count);
        }

        void Add(Vector2 p, float phase)
        {
            if (_points.Count >= MaxDots) return;
            _points.Add(p);
            _phase.Add(Mathf.Clamp01(phase));
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
        }

        void LayoutRectangle()
        {
            float hw = _size.x * 0.5f, hh = _size.y * 0.5f;
            int nx = Mathf.Max(2, Mathf.CeilToInt(_size.x / Spacing));
            int ny = Mathf.Max(2, Mathf.CeilToInt(_size.y / Spacing));
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)nx);
                Add(new Vector2(x, hh), AlongPhase(x));
                Add(new Vector2(x, -hh), AlongPhase(x));
            }
            for (int j = 1; j < ny; j++)
            {
                float y = Mathf.Lerp(-hh, hh, j / (float)ny);
                Add(new Vector2(-hw, y), AlongPhase(-hw));
                Add(new Vector2(hw, y), AlongPhase(hw));
            }
        }

        void LayoutRing()
        {
            float r = Mathf.Max(_size.x, _size.y) * 0.5f;
            int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r / Spacing), 12, 90);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0f);
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
            for (int i = 0; i < _dots.Count; i++)
            {
                var dot = _dots[i];
                if (i >= _points.Count)
                {
                    if (dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
                    continue;
                }
                if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
                dot.transform.localPosition = _points[i];

                float lit = Mathf.Clamp01((t - _phase[i]) * 8f + 1f);   // 0 = not reached yet, 1 = fully lit
                bool inner = _circle && _phase[i] > 0.01f;
                float size = DotSize * Mathf.Lerp(1f, 1.5f, lit);
                if (inner) size *= 0.8f;
                dot.transform.localScale = Vector3.one * size;

                Color c = _colour;
                if (_circle) lit = inner ? (t >= _phase[i] ? 1f : 0.12f) : Mathf.Lerp(0.45f, 1f, t);
                c.a = Mathf.Lerp(0.25f, 0.95f, lit);
                if (t > 0.92f) c = Color.Lerp(c, Color.white, (t - 0.92f) * 8f * 0.7f);   // flash white right before the hit
                dot.color = c;
            }
        }
    }
}
