using System;
using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>Pooled burst: a spark that pops, spins slightly and fades. Placeholder for flipbook VFX.</summary>
    public class SparkVfx : MonoBehaviour
    {
        SpriteRenderer _sr;
        Action<SparkVfx> _release;
        float _t, _life, _size;
        Color _colour;
        float _spin;

        public void Init(Action<SparkVfx> release)
        {
            _release = release;
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = ProceduralSprites.Spark;
            _sr.sortingOrder = 45;
        }

        public void Play(Vector2 position, float size, Color colour, float life = 0.18f)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 45f));
            _size = size; _colour = colour; _life = life; _t = 0f;
            _spin = UnityEngine.Random.Range(-90f, 90f);
            Apply();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life) { _release(this); return; }
            Apply();
        }

        void Apply()
        {
            float k = _t / _life;
            float s = _size * Mathf.Lerp(0.5f, 1.4f, Mathf.Sqrt(k));
            transform.localScale = new Vector3(s, s, 1f);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            Color c = _colour; c.a = 1f - k * k;
            _sr.color = c;
        }
    }

    /// <summary>Pooled crescent swing trail shown while a weapon's hitbox is live.</summary>
    public class SlashVfx : MonoBehaviour
    {
        SpriteRenderer _sr;
        Action<SlashVfx> _release;
        float _t, _life;
        Vector2 _size;
        Color _colour;
        int _facing;

        public void Init(Action<SlashVfx> release)
        {
            _release = release;
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = ProceduralSprites.Slash;
            _sr.sortingOrder = 44;
        }

        public void Play(Vector2 position, int facing, Vector2 size, Color colour, float life)
        {
            transform.position = position;
            _facing = facing; _size = size; _colour = colour; _life = Mathf.Max(0.05f, life); _t = 0f;
            Apply();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life) { _release(this); return; }
            Apply();
        }

        void Apply()
        {
            float k = _t / _life;
            // The crescent sprite is 1.28 units wide at scale 1; stretch it to the hitbox and sweep it forward.
            float sweep = Mathf.Lerp(0.8f, 1.15f, k);
            transform.localScale = new Vector3(_facing * _size.x * 0.85f * sweep, _size.y * 0.95f, 1f);
            Color c = Color.Lerp(Color.white, _colour, 0.5f);
            c.a = Mathf.Lerp(0.95f, 0f, k * k);
            _sr.color = c;
        }
    }

    /// <summary>Pooled floating damage number.</summary>
    public class DamageNumber : MonoBehaviour
    {
        TextMesh _text;
        Action<DamageNumber> _release;
        float _t, _life;
        Vector2 _velocity;
        Color _colour;

        public void Init(Action<DamageNumber> release)
        {
            _release = release;
            _text = gameObject.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontSize = 64;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 50;
        }

        public void Play(Vector2 position, string text, Color colour, float size, float life = 0.7f)
        {
            transform.position = position + new Vector2(UnityEngine.Random.Range(-0.25f, 0.25f), 0f);
            _text.text = text;
            _text.characterSize = size;
            _colour = colour; _life = life; _t = 0f;
            _velocity = new Vector2(UnityEngine.Random.Range(-0.8f, 0.8f), 3.2f);
            _text.color = colour;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life) { _release(this); return; }
            _velocity.y -= 6f * Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            float k = _t / _life;
            float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.2f);
            transform.localScale = Vector3.one * Mathf.Max(0.8f, pop);
            Color c = _colour; c.a = Mathf.Clamp01((1f - k) * 2.5f);
            _text.color = c;
        }
    }
}
