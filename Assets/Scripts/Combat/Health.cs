using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Combat
{
    /// <summary>
    /// Hit points plus the defensive filter chain. Anything that deals damage calls <see cref="TakeHit"/>;
    /// filters (i-frames, shield) decide whether and how much damage lands. Feedback (hitstop, VFX, audio,
    /// UI) listens to <see cref="HitEvent"/> on the event bus, so combat code never touches presentation.
    /// </summary>
    public class Health : MonoBehaviour
    {
        /// <summary>The player's health, for enemies to target. Singleplayer only.</summary>
        public static Health Player { get; private set; }

        [SerializeField] Team team = Team.Neutral;
        [SerializeField] float maxHealth = 100f;

        readonly List<IHitFilter> _filters = new List<IHitFilter>();
        Collider2D _collider;

        public Team Team => team;
        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Fraction => maxHealth <= 0f ? 0f : Current / maxHealth;

        public event Action<HitReport> HitTaken;   // every hit, including blocked / parried / ignored
        public event Action Died;

        public Vector2 Center
        {
            get
            {
                if (_collider == null) _collider = GetComponent<Collider2D>();
                return _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;
            }
        }

        void Awake()
        {
            Current = maxHealth;
        }

        void OnEnable()
        {
            if (team == Team.Player) Player = this;
        }

        void OnDisable()
        {
            if (Player == this) Player = null;
        }

        public void Initialize(Team newTeam, float max)
        {
            team = newTeam;
            maxHealth = max;
            Current = max;
            IsDead = false;
            if (team == Team.Player && isActiveAndEnabled) Player = this;
        }

        public void ResetToFull()
        {
            Current = maxHealth;
            IsDead = false;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void AddFilter(IHitFilter filter)
        {
            if (_filters.Contains(filter)) return;
            _filters.Add(filter);
            _filters.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void RemoveFilter(IHitFilter filter) => _filters.Remove(filter);

        /// <summary>Applies a hit through the filter chain.</summary>
        public HitResult TakeHit(in DamageInfo incoming)
        {
            if (IsDead) return HitResult.Ignored;
            if (incoming.SourceTeam == team && team != Team.Neutral) return HitResult.Ignored;

            DamageInfo info = incoming;
            HitResult result = HitResult.Hit;

            for (int i = 0; i < _filters.Count; i++)
            {
                HitResult r = _filters[i].Filter(ref info);
                if (r == HitResult.Hit) continue;
                result = r;
                if (r == HitResult.Ignored || r == HitResult.Parried) break;
            }

            float applied = result == HitResult.Ignored || result == HitResult.Parried ? 0f : Mathf.Max(0f, info.Amount);
            if (applied > 0f) Current = Mathf.Max(0f, Current - applied);
            bool killed = applied > 0f && Current <= 0f;

            var report = new HitReport
            {
                Target = this,
                Result = result,
                Amount = applied,
                Info = info,
                Position = info.HitPoint != Vector2.zero ? info.HitPoint : Center,
                Killed = killed
            };

            HitTaken?.Invoke(report);
            EventBus<HitEvent>.Raise(new HitEvent { Report = report });

            if (killed)
            {
                IsDead = true;
                Died?.Invoke();
            }
            return result;
        }
    }
}
