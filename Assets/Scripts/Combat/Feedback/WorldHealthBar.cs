using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>Small health bar above a character that appears when it takes damage and fades after a few seconds.</summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Vector2 size = new Vector2(1.1f, 0.12f);
        [SerializeField] float visibleSeconds = 3f;

        SpriteRenderer _back, _front;
        float _visible;

        public void Bind(Health h, float height, float width = 1.1f)
        {
            health = h;
            size.x = width;
            transform.localPosition = new Vector3(0f, height, 0f);
        }

        void Awake()
        {
            _back = MakeChild("Back", 60, new Color(0.05f, 0.03f, 0.05f, 0.9f));
            _front = MakeChild("Front", 61, new Color(0.9f, 0.25f, 0.2f, 1f));
            SetAlpha(0f);
        }

        SpriteRenderer MakeChild(string childName, int order, Color colour)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.sortingOrder = order;
            sr.color = colour;
            return sr;
        }

        void OnEnable()
        {
            if (health != null) health.HitTaken += OnHit;
        }

        void OnDisable()
        {
            if (health != null) health.HitTaken -= OnHit;
        }

        void OnHit(HitReport r)
        {
            if (r.Amount > 0f) _visible = visibleSeconds;
        }

        void LateUpdate()
        {
            if (health == null) return;
            _visible -= Time.deltaTime;
            float a = Mathf.Clamp01(_visible / 0.5f);
            SetAlpha(health.IsDead ? 0f : a);

            // The parent may be flipped by a facing scale; keep the bar upright and unflipped.
            Vector3 ls = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(ls.x < 0f ? -1f : 1f, 1f, 1f);

            float pad = 0.04f;
            _back.transform.localScale = new Vector3(size.x + pad, size.y + pad, 1f);
            float f = Mathf.Clamp01(health.Fraction);
            float w = Mathf.Max(0.0001f, size.x * f);
            _front.transform.localScale = new Vector3(w, size.y, 1f);
            _front.transform.localPosition = new Vector3(-size.x * 0.5f + w * 0.5f, 0f, 0f);
        }

        void SetAlpha(float a)
        {
            if (_back == null) return;
            Color b = _back.color; b.a = 0.9f * a; _back.color = b;
            Color f = _front.color; f.a = a; _front.color = f;
        }
    }
}
