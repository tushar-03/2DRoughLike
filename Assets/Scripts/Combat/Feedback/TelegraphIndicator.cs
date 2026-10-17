using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Consistent visual language for incoming attacks: a bright-outlined box showing the danger area that
    /// fills from the attacker toward the far edge, finishing exactly when the hit lands. Readable by shape
    /// and motion, not only by colour (colourblind-safe). The same indicator is used by every enemy.
    /// </summary>
    public class TelegraphIndicator : MonoBehaviour
    {
        public static readonly Color StandardColour = new Color(1f, 0.55f, 0.1f);
        public static readonly Color UnparryableColour = new Color(0.8f, 0.3f, 1f);

        SpriteRenderer _outline, _fill;
        Vector2 _size;
        int _facing = 1;
        float _duration, _elapsed;
        bool _running;

        void Awake() => Build();

        void Build()
        {
            if (_outline != null) return;
            _outline = MakeChild("Outline", 40);
            _outline.color = new Color(0.1f, 0.02f, 0.02f, 0.85f);
            _fill = MakeChild("Fill", 41);
            gameObject.SetActive(false);
        }

        SpriteRenderer MakeChild(string childName, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Starts a telegraph covering <paramref name="size"/> centred on <paramref name="centre"/>.</summary>
        bool _circle;

        public void Begin(Vector2 centre, Vector2 size, int facing, float duration, bool unparryable = false, float angleDeg = 0f, bool circle = false)
        {
            Build();
            _circle = circle;
            var shape = circle ? ProceduralSprites.Circle : ProceduralSprites.Square;
            _outline.sprite = shape;
            _fill.sprite = shape;
            transform.position = centre;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            _size = size;
            _facing = facing;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _running = true;
            _fill.color = unparryable ? UnparryableColour : StandardColour;
            gameObject.SetActive(true);
            Apply(0f);
        }

        /// <summary>Moves and resizes the danger area while it is still running (aim lines that track the player).</summary>
        public void Retarget(Vector2 centre, Vector2 size, float angleDeg)
        {
            if (!_running) return;
            transform.position = centre;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            _size = size;
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

        void Apply(float t)
        {
            const float pad = 0.1f;
            if (_circle)
            {
                // a disc that grows from the centre to the full blast radius
                float d = Mathf.Max(_size.x, _size.y);
                float unit = 1f / ProceduralSprites.Circle.bounds.size.x;   // sprite is 1 unit wide at scale 1
                _outline.transform.localScale = new Vector3((d + pad) * unit, (d + pad) * unit, 1f);
                _outline.transform.localPosition = Vector3.zero;
                float r = Mathf.Max(0.01f, d * t) * unit;
                _fill.transform.localScale = new Vector3(r, r, 1f);
                _fill.transform.localPosition = Vector3.zero;
                Color cc = _fill.color;
                cc.a = Mathf.Lerp(0.25f, 0.75f, t);
                _fill.color = cc;
                return;
            }
            _outline.transform.localScale = new Vector3(_size.x + pad, _size.y + pad, 1f);
            _outline.transform.localPosition = Vector3.zero;

            float w = Mathf.Max(0.01f, _size.x * t);
            _fill.transform.localScale = new Vector3(w, _size.y, 1f);
            _fill.transform.localPosition = new Vector3(_facing * (-_size.x * 0.5f + w * 0.5f), 0f, 0f);

            Color c = _fill.color;
            c.a = Mathf.Lerp(0.25f, 0.75f, t);
            _fill.color = c;
        }
    }
}
