using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>
    /// Status effects on a character: Burn, Poison, Bleed (damage over time), Slow, Stun and Weaken.
    /// Reapplying refreshes duration and keeps the stronger potency. Damage over time bypasses shields and
    /// i-frames; Weaken is a hit filter that adds damage taken. Enemies and the player can both carry these.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class StatusEffects : MonoBehaviour, IHitFilter
    {
        /// <summary>Damage per second at potency 1.</summary>
        static readonly Dictionary<StatusType, float> DotRate = new Dictionary<StatusType, float>
        {
            { StatusType.Burn, 5f }, { StatusType.Poison, 3.5f }, { StatusType.Bleed, 4f }
        };

        const float TickInterval = 0.5f;

        sealed class Active
        {
            public float Remaining;
            public float Potency;
            public float TickTimer;
            public Team Source;
        }

        readonly Dictionary<StatusType, Active> _active = new Dictionary<StatusType, Active>();
        readonly List<StatusType> _expired = new List<StatusType>();
        Health _health;
        HitReactionVisual _visual;

        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        public event Action<StatusType> Applied;

        public int Priority => 5;
        public bool IsStunned => Has(StatusType.Stun);
        public float StunRemaining => Has(StatusType.Stun) ? _active[StatusType.Stun].Remaining : 0f;

        /// <summary>Movement speed multiplier (1 = normal).</summary>
        public float SpeedMultiplier => Has(StatusType.Slow) ? 1f / (1f + 0.8f * _active[StatusType.Slow].Potency) : 1f;

        void Awake()
        {
            _health = GetComponent<Health>();
            _visual = GetComponentInChildren<HitReactionVisual>();
        }

        void OnEnable() => _health.AddFilter(this);

        void OnDisable()
        {
            _health.RemoveFilter(this);
            _active.Clear();
            if (_visual != null) _visual.SetStatusTint(Color.clear);
        }

        public bool Has(StatusType type) => _active.ContainsKey(type);
        public float Remaining(StatusType type) => _active.TryGetValue(type, out var a) ? a.Remaining : 0f;
        public int ActiveCount => _active.Count;

        public void Apply(StatusType type, float duration, float potency = 1f, Team source = Team.Neutral)
        {
            if (type == StatusType.None || duration <= 0f || _health.IsDead) return;

            if (_active.TryGetValue(type, out Active a))
            {
                a.Remaining = Mathf.Max(a.Remaining, duration);
                a.Potency = Mathf.Max(a.Potency, potency);
            }
            else
            {
                _active[type] = new Active { Remaining = duration, Potency = potency, TickTimer = TickInterval, Source = source };
            }
            Applied?.Invoke(type);
        }

        public void ClearAll()
        {
            _active.Clear();
            if (_visual != null) _visual.SetStatusTint(Color.clear);
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (_active.Count == 0 || dt <= 0f) return;
            if (_health.IsDead) { ClearAll(); return; }

            _expired.Clear();
            foreach (var kv in _active)
            {
                Active a = kv.Value;
                a.Remaining -= dt;

                if (DotRate.TryGetValue(kv.Key, out float rate))
                {
                    a.TickTimer -= dt;
                    while (a.TickTimer <= 0f && !_health.IsDead)
                    {
                        a.TickTimer += TickInterval;
                        _health.ApplyDot(rate * a.Potency * TickInterval, a.Source);
                    }
                }
                if (a.Remaining <= 0f) _expired.Add(kv.Key);
            }
            foreach (var t in _expired) _active.Remove(t);

            UpdateTint();
        }

        void UpdateTint()
        {
            if (_visual == null) return;
            Color c = Color.clear;
            if (Has(StatusType.Burn)) c = new Color(1f, 0.5f, 0.1f, 1f);
            else if (Has(StatusType.Poison)) c = new Color(0.5f, 0.9f, 0.3f, 1f);
            else if (Has(StatusType.Bleed)) c = new Color(0.9f, 0.15f, 0.2f, 1f);
            else if (Has(StatusType.Slow)) c = new Color(0.4f, 0.7f, 1f, 1f);
            else if (Has(StatusType.Stun)) c = new Color(1f, 0.95f, 0.4f, 1f);
            else if (Has(StatusType.Weaken)) c = new Color(0.7f, 0.4f, 0.9f, 1f);
            _visual.SetStatusTint(c);
        }

        /// <summary>Weaken: the target takes extra damage.</summary>
        public HitResult Filter(ref DamageInfo info)
        {
            if (Has(StatusType.Weaken) && (info.Tags & DamageTags.Dot) == 0)
                info.Amount *= 1f + 0.25f * _active[StatusType.Weaken].Potency;
            return HitResult.Hit;
        }
    }
}
