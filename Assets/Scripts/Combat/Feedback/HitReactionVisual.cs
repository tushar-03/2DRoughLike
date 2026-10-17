using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>
    /// Sprite-level hit feedback: white flash on hit, gold flash on parry, blink while invulnerable,
    /// a warning tint while an attack is telegraphed, and an optional squash-and-recoil.
    /// Colour only (the player's scale belongs to PlayerVisual). Swap for a shader-based flash in M5.
    /// </summary>
    public class HitReactionVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Health health;
        [SerializeField] bool squashOnHit = true;
        [SerializeField] float flashDuration = 0.09f;
        [SerializeField] Color warningColour = new Color(1f, 0.75f, 0.2f);

        /// <summary>Scale 0..1 for photosensitivity accessibility (flash slider).</summary>
        public static float FlashScale = 1f;

        Color _base = Color.white;
        Vector3 _baseScale = Vector3.one;
        float _flash, _blink, _warning, _punch;
        Color _flashColour = Color.white;

        public void Bind(SpriteRenderer sr, Health h, bool squash)
        {
            sprite = sr;
            health = h;
            squashOnHit = squash;
        }

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (health == null) health = GetComponentInParent<Health>();
            if (sprite != null) { _base = sprite.color; _baseScale = sprite.transform.localScale; }
        }

        void OnEnable()
        {
            if (health != null) health.HitTaken += OnHit;
        }

        void OnDisable()
        {
            if (health != null) health.HitTaken -= OnHit;
            if (sprite != null) { sprite.color = _base; sprite.transform.localScale = _baseScale; }
        }

        public void SetBaseColour(Color c) { _base = c; if (sprite != null) sprite.color = c; }
        public void Blink(float duration) => _blink = Mathf.Max(_blink, duration);

        /// <summary>0 = none, 1 = full warning. Pulses so it reads without relying on colour alone.</summary>
        public void SetWarning(float intensity01) => _warning = Mathf.Clamp01(intensity01);

        void OnHit(HitReport r)
        {
            switch (r.Result)
            {
                case HitResult.Hit when r.Amount > 0f:
                    _flash = flashDuration; _flashColour = Color.white;
                    if (squashOnHit) _punch = 1f;
                    break;
                case HitResult.Parried:
                    _flash = flashDuration * 2f; _flashColour = new Color(1f, 0.9f, 0.4f);
                    break;
                case HitResult.Blocked:
                    _flash = flashDuration * 0.6f; _flashColour = new Color(0.7f, 0.85f, 1f);
                    break;
            }
        }

        void LateUpdate()
        {
            if (sprite == null) return;
            float dt = Time.deltaTime;

            Color c = _base;
            if (_warning > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 28f);
                c = Color.Lerp(c, warningColour, _warning * (0.45f + 0.45f * pulse));
            }

            if (_flash > 0f)
            {
                _flash -= dt;
                c = Color.Lerp(c, _flashColour, Mathf.Clamp01(_flash / flashDuration) * FlashScale);
            }

            if (_blink > 0f)
            {
                _blink -= dt;
                bool off = Mathf.FloorToInt(_blink * 16f) % 2 == 0;
                c.a = off ? 0.35f : 1f;
            }

            sprite.color = c;

            if (squashOnHit)
            {
                _punch = Mathf.MoveTowards(_punch, 0f, dt * 7f);
                float k = _punch * _punch;
                sprite.transform.localScale = new Vector3(_baseScale.x * (1f + 0.22f * k), _baseScale.y * (1f - 0.18f * k), _baseScale.z);
            }
        }
    }
}
