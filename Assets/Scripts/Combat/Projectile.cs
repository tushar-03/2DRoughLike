using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Pooled projectile. Blocked by ground, damages the opposing team through the normal hit pipeline,
    /// and is reflected (not destroyed) when parried.
    /// </summary>
    public class Projectile : MonoBehaviour, IAttacker
    {
        static ComponentPool<Projectile> _pool;
        static readonly Collider2D[] Buffer = new Collider2D[8];

        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        SpriteRenderer _sr;
        Vector2 _velocity;
        float _damage, _knockback, _stagger, _life, _radius;
        Team _team;
        readonly HashSet<Health> _hit = new HashSet<Health>();
        LayerMask _groundMask;
        Color? _tint;
        float _turnRate, _homeUntil;
        int _pierce;
        bool _arrow, _bolt;
        static Sprite _arrowSprite;

        public Vector2 Velocity => _velocity;
        public Team Team => _team;
        Team IAttacker.Team => _team;
        Transform IAttacker.Transform => transform;
        public bool IsAlive => isActiveAndEnabled;

        static Projectile Create()
        {
            var go = new GameObject("Projectile");
            var p = go.AddComponent<Projectile>();
            p._sr = go.AddComponent<SpriteRenderer>();
            p._sr.sprite = ProceduralSprites.Circle;
            p._sr.sortingOrder = 30;
            return p;
        }

        public static Projectile Spawn(Vector2 position, Vector2 velocity, float damage, Team team,
            float knockback = 4f, float stagger = 10f, float lifetime = 4f, float radius = 0.2f, Color? tint = null)
        {
            _pool ??= new ComponentPool<Projectile>(Create);
            Projectile p = _pool.Get();
            p.transform.position = position;
            p._velocity = velocity;
            p._damage = damage;
            p._team = team;
            p._knockback = knockback;
            p._stagger = stagger;
            p._life = lifetime;
            p._radius = radius;
            p._tint = tint;
            p._turnRate = 0f;
            p._homeUntil = 0f;
            p._pierce = 0;
            p._arrow = false;
            p._bolt = velocity.sqrMagnitude > 18f * 18f && team != Team.Player;   // fast shots are streaks, slow ones are orbs
            p._sr.sprite = p._bolt ? ProceduralSprites.Bolt : ProceduralSprites.Orb;
            p.transform.rotation = Quaternion.identity;
            p._hit.Clear();
            if (team != Team.Player) Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.EnemyShoot, position, 0.55f);
            p._groundMask = GameLayers.GroundMask;
            p.transform.localScale = Vector3.one * radius * (p._bolt ? 3.4f : 2.8f);
            p.Recolour();
            p.Face();
            return p;
        }

        /// <summary>Turns the projectile into an arrow that points along its flight and passes through <paramref name="pierce"/> enemies.</summary>
        public void SetArrow(int pierce)
        {
            _arrow = true;
            _pierce = pierce;
            _sr.sprite = ArrowSprite();
            transform.localScale = Vector3.one * 0.9f;
            Recolour();
            Face();
        }

        void Face()
        {
            if (!(_arrow || _bolt) || _velocity.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg);
        }

        static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int W = 40, H = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, clear);
            Color shaft = new Color(0.8f, 0.62f, 0.38f), steel = new Color(0.86f, 0.9f, 0.96f), fletch = new Color(0.9f, 0.25f, 0.2f);
            for (int x = 4; x < 32; x++) { tex.SetPixel(x, 5, shaft); tex.SetPixel(x, 4, shaft * 0.8f); }
            for (int i = 0; i < 8; i++)                                  // steel head
                for (int y = 5 - (7 - i) / 2; y <= 4 + (7 - i) / 2; y++) tex.SetPixel(32 + i, y, steel);
            for (int i = 0; i < 6; i++)                                  // fletching
            {
                tex.SetPixel(1 + i, 5 + 1 + i / 2, fletch); tex.SetPixel(1 + i, 4 - 1 - i / 2, fletch);
                tex.SetPixel(2 + i, 5 + (i / 2), fletch); tex.SetPixel(2 + i, 4 - (i / 2), fletch);
            }
            tex.Apply(false, true);
            return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }

        void Recolour()
        {
            if (_arrow) { _sr.color = Color.white; return; }
            _sr.color = _team == Team.Player ? new Color(1f, 0.9f, 0.4f) : (_tint ?? new Color(1f, 0.45f, 0.15f));
        }

        /// <summary>Makes the projectile curve toward the player for a while (the witch's soul orbs).</summary>
        public void SetHoming(float degreesPerSecond, float seconds)
        {
            _turnRate = degreesPerSecond;
            _homeUntil = _life - seconds;   // _life counts down; homing lasts until this much life remains
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            _life -= dt;
            if (_life <= 0f) { Despawn(); return; }

            if (_turnRate > 0f && _life > _homeUntil && _team == Team.Enemy && Health.Player != null && !Health.Player.IsDead)
            {
                Vector2 want = (Health.Player.Center - (Vector2)transform.position).normalized;
                float speed = _velocity.magnitude;
                float angle = Vector2.SignedAngle(_velocity, want);
                float turn = Mathf.Clamp(angle, -_turnRate * dt, _turnRate * dt);
                _velocity = (Vector2)(Quaternion.Euler(0f, 0f, turn) * _velocity.normalized) * speed;
            }
            transform.position += (Vector3)(_velocity * dt);
            Face();

            LayerMask targets = LayerMask.GetMask(_team == Team.Player ? GameLayers.EnemyName : GameLayers.PlayerName);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = _groundMask | targets, useTriggers = true };
            int count = Physics2D.OverlapCircle(transform.position, _radius, filter, Buffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = Buffer[i];
                if (((1 << col.gameObject.layer) & _groundMask) != 0) { Despawn(); return; }

                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead || !_hit.Add(h)) continue;

                Vector2 dir = _velocity.sqrMagnitude > 0.001f ? _velocity.normalized : Vector2.right;
                var info = new DamageInfo
                {
                    Amount = _damage,
                    SourceTeam = _team,
                    Attacker = this,
                    SourcePosition = transform.position,
                    HitPoint = transform.position,
                    Knockback = new Vector2(dir.x * _knockback, 1.5f),
                    Stagger = _stagger,
                    HitStop = 0.05f,
                    Shake = 0.2f,
                    Tags = DamageTags.Projectile
                };

                Team before = _team;
                HitResult result = h.TakeHit(info);
                if (result == HitResult.Parried || _team != before) return;   // reflected, keep flying
                if (result != HitResult.Ignored)
                {
                    if (_pierce > 0) { _pierce--; continue; }
                    Despawn();
                    return;
                }
            }
        }

        void IAttacker.OnAttackParried(Vector2 point, float stunDuration)
        {
            // Reflect: send it back faster as the player's projectile.
            _velocity = -_velocity * 1.4f;
            _team = Team.Player;
            _damage *= 2f;
            _life = Mathf.Max(_life, 2f);
            _hit.Clear();
            Recolour();
        }

        void Despawn() => _pool.Release(this);
    }
}

