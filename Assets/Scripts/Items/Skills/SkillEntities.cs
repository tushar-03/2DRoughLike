using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Items
{
    /// <summary>Numbers a spawned skill entity needs, already scaled by the player's stats.</summary>
    public struct SkillPayload
    {
        public SkillData Data;
        public float Damage;
        public Color Tint;
    }

    static class SkillVisuals
    {
        public static SpriteRenderer Sprite(Transform parent, Sprite sprite, Color colour, Vector3 localScale, Vector3 localPos, int order)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
            return sr;
        }

        public static DamageInfo Info(SkillPayload p, Vector2 knockback, float stagger)
            => new DamageInfo
            {
                Amount = p.Damage,
                SourceTeam = Team.Player,
                Knockback = knockback,
                Stagger = stagger,
                HitStop = 0.06f,
                Shake = 0.35f,
                Tags = DamageTags.Hazard,
                OnHitStatus = p.Data.status,
                StatusChance = p.Data.status == StatusType.None ? 0f : 1f,
                StatusDuration = p.Data.statusDuration,
                StatusPotency = p.Data.statusPotency
            };
    }

    /// <summary>A thrown bomb: arcs under gravity, explodes on contact with ground or an enemy, or when its fuse ends.</summary>
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        const float Gravity = 26f;
        const float Fuse = 1.6f;

        public bool autoTick = true;
        SkillPayload _p;
        Vector2 _velocity;
        float _age;
        bool _exploded;

        public static GrenadeProjectile Throw(Vector2 position, Vector2 velocity, SkillPayload payload)
        {
            var go = new GameObject("Grenade");
            go.transform.position = position;
            var g = go.AddComponent<GrenadeProjectile>();
            g._p = payload;
            g._velocity = velocity;
            SkillVisuals.Sprite(go.transform, ProceduralSprites.Circle, payload.Tint, Vector3.one * 0.45f, Vector3.zero, 35);
            return g;
        }

        void Update() { if (autoTick) Tick(Time.deltaTime); }

        public void Tick(float dt)
        {
            if (_exploded) return;
            _age += dt;
            _velocity.y -= Gravity * dt;
            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, -360f * dt);

            bool hitGround = Physics2D.OverlapCircle(transform.position, 0.22f, GameLayers.WalkableMask) != null && _velocity.y <= 0f;
            bool hitEnemy = Physics2D.OverlapCircle(transform.position, 0.3f, LayerMask.GetMask(GameLayers.EnemyName)) != null;
            if (hitGround || hitEnemy || _age >= Fuse) Explode();
        }

        void Explode()
        {
            _exploded = true;
            Vector2 pos = transform.position;
            AreaStrike.Circle(pos, _p.Data.radius, LayerMask.GetMask(GameLayers.EnemyName), SkillVisuals.Info(_p, new Vector2(7f, 5f), 22f));
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = pos, Radius = _p.Data.radius, Colour = _p.Tint });
            Destroy(gameObject);
        }
    }

    /// <summary>A stationary turret that shoots the nearest enemy in range for a limited time.</summary>
    public sealed class TurretEntity : MonoBehaviour
    {
        const float Range = 11f;

        public bool autoTick = true;
        SkillPayload _p;
        float _life, _cooldown;
        Transform _barrel;

        public float Life => _life;
        /// <summary>True once the turret has run out of time (it is destroyed at the end of the frame).</summary>
        public bool Expired { get; private set; }

        public static TurretEntity Place(Vector2 feetPosition, SkillPayload payload)
        {
            var go = new GameObject("Bell Turret");
            go.transform.position = feetPosition;
            var t = go.AddComponent<TurretEntity>();
            t._p = payload;
            t._life = payload.Data.duration;
            SkillVisuals.Sprite(go.transform, ProceduralSprites.Square, payload.Tint * 0.7f, new Vector3(0.8f, 0.5f, 1f), new Vector3(0f, 0.25f, 0f), 12);
            var dome = SkillVisuals.Sprite(go.transform, ProceduralSprites.Circle, payload.Tint, new Vector3(0.7f, 0.7f, 1f), new Vector3(0f, 0.6f, 0f), 13);
            t._barrel = SkillVisuals.Sprite(go.transform, ProceduralSprites.Square, Color.white, new Vector3(0.6f, 0.14f, 1f), new Vector3(0.3f, 0.7f, 0f), 14).transform;
            return t;
        }

        void Update() { if (autoTick) Tick(Time.deltaTime); }

        public void Tick(float dt)
        {
            if (Expired) return;
            _life -= dt;
            if (_life <= 0f) { Expired = true; Destroy(gameObject); return; }

            _cooldown -= dt;
            Health target = FindTarget();
            if (target == null) return;

            Vector2 muzzle = (Vector2)transform.position + new Vector2(0f, 0.7f);
            Vector2 dir = (target.Center - muzzle).normalized;
            _barrel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            if (_cooldown <= 0f)
            {
                _cooldown = 1f / Mathf.Max(0.1f, _p.Data.fireRate);
                var proj = Projectile.Spawn(muzzle + dir * 0.5f, dir * 16f, _p.Damage, Team.Player, 3f, 6f, 1.5f, 0.16f);
                proj.autoTick = autoTick;
            }
        }

        static readonly Collider2D[] Buffer = new Collider2D[24];

        Health FindTarget()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask(GameLayers.EnemyName), useTriggers = true };
            int n = Physics2D.OverlapCircle(transform.position, Range, filter, Buffer);
            Health best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Health h = Buffer[i].GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                float d = ((Vector2)transform.position - h.Center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = h; }
            }
            return best;
        }
    }

    /// <summary>A floor trap: arms after a moment, then springs on the first enemy that steps on it.</summary>
    public sealed class TrapEntity : MonoBehaviour
    {
        const float ArmTime = 0.5f;
        const float MaxLife = 40f;

        public bool autoTick = true;
        SkillPayload _p;
        float _age;
        SpriteRenderer _sr;

        public bool Armed => _age >= ArmTime;
        /// <summary>True once the trap has sprung or timed out (it is destroyed at the end of the frame).</summary>
        public bool Spent { get; private set; }

        public static TrapEntity Place(Vector2 feetPosition, SkillPayload payload)
        {
            var go = new GameObject("Snare Trap");
            go.transform.position = feetPosition;
            var t = go.AddComponent<TrapEntity>();
            t._p = payload;
            t._sr = SkillVisuals.Sprite(go.transform, ProceduralSprites.Spark, payload.Tint, new Vector3(1.1f, 0.5f, 1f), new Vector3(0f, 0.12f, 0f), 11);
            return t;
        }

        void Update() { if (autoTick) Tick(Time.deltaTime); }

        public void Tick(float dt)
        {
            if (Spent) return;
            _age += dt;
            if (_age > MaxLife) { Spent = true; Destroy(gameObject); return; }

            Color c = _p.Tint;
            c.a = Armed ? 0.9f : 0.35f;
            if (_sr != null) _sr.color = c;
            if (!Armed) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask(GameLayers.EnemyName), useTriggers = true };
            var buffer = new Collider2D[4];
            if (Physics2D.OverlapBox((Vector2)transform.position + new Vector2(0f, 0.3f), new Vector2(1.2f, 0.8f), 0f, filter, buffer) == 0) return;

            Spent = true;
            Vector2 pos = transform.position;
            AreaStrike.Circle(pos + Vector2.up * 0.5f, _p.Data.radius, LayerMask.GetMask(GameLayers.EnemyName),
                SkillVisuals.Info(_p, new Vector2(2f, 3f), 25f));
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = pos + Vector2.up * 0.4f, Radius = _p.Data.radius * 0.7f, Colour = _p.Tint });
            Destroy(gameObject);
        }
    }
}
