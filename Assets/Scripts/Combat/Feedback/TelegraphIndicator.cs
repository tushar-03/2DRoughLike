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
        public void Begin(Vector2 centre, Vector2 size, int facing, float duration, bool unparryable = false)
        {
            Build();
            transform.position = centre;
            _size = size;
            _facing = facing;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _running = true;
            _fill.color = unparryable ? UnparryableColour : StandardColour;
            gameObject.SetActive(true);
            Apply(0f);
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
