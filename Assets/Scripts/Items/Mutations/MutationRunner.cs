using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;

namespace Roughlike.Items
{
    /// <summary>
    /// Runtime behaviour of mutations that react to events: parry riposte, momentum and second wind.
    /// Stat-only mutations (extra health, speed, heal on kill) need no runtime and live in <see cref="PlayerStats"/>.
    /// </summary>
    [DefaultExecutionOrder(6)]
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerStats))]
    public sealed class MutationRunner : MonoBehaviour, IHitFilter
    {
        const int MaxMomentumStacks = 5;
        const float MomentumDuration = 5f;

        PlayerInventory _inventory;
        PlayerStats _stats;
        PlayerCombat _combat;
        Health _health;

        bool _secondWindUsed, _healPending;
        float _healFraction;
        int _stacks;
        float _stackTimer;

        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        public int Priority => 20;
        public int MomentumStacks => _stacks;

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _stats = GetComponent<PlayerStats>();
            _combat = GetComponent<PlayerCombat>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            EventBus<ParryEvent>.Subscribe(OnParry);
            EventBus<EnemyDiedEvent>.Subscribe(OnKill);
            _health.AddFilter(this);
            _health.HitTaken += OnHitTaken;
            _inventory.RunReset += ResetRunState;
        }

        void OnDisable()
        {
            EventBus<ParryEvent>.Unsubscribe(OnParry);
            EventBus<EnemyDiedEvent>.Unsubscribe(OnKill);
            _health.RemoveFilter(this);
            _health.HitTaken -= OnHitTaken;
            _inventory.RunReset -= ResetRunState;
        }

        MutationData Find(MutationEffect effect)
        {
            foreach (var m in _inventory.Mutations) if (m.effect == effect) return m;
            return null;
        }

        void ResetRunState()
        {
            _secondWindUsed = false;
            _healPending = false;
            _stacks = 0;
            _stackTimer = 0f;
        }

        void OnParry(ParryEvent e)
        {
            var m = Find(MutationEffect.ParryRiposte);
            if (m != null && _combat != null) _combat.PendingHitMultiplier = Mathf.Max(1f, m.effectValue);
        }

        void OnKill(EnemyDiedEvent e)
        {
            var m = Find(MutationEffect.Momentum);
            if (m == null) return;
            _stacks = Mathf.Min(MaxMomentumStacks, _stacks + 1);
            _stackTimer = MomentumDuration;
            _stats.SetMomentum(_stacks, m.effectValue);
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (_stacks <= 0) return;
            _stackTimer -= dt;
            if (_stackTimer <= 0f)
            {
                _stacks = 0;
                _stats.SetMomentum(0, 0f);
            }
        }

        /// <summary>Second Wind: a lethal hit leaves 1 HP (once per run), then heals.</summary>
        public HitResult Filter(ref DamageInfo info)
        {
            var m = Find(MutationEffect.SecondWind);
            if (m == null || _secondWindUsed) return HitResult.Hit;
            if ((info.Tags & DamageTags.Dot) != 0) return HitResult.Hit;
            if (info.Amount < _health.Current) return HitResult.Hit;

            info.Amount = Mathf.Max(0f, _health.Current - 1f);
            _secondWindUsed = true;
            _healPending = true;
            _healFraction = m.effectValue;
            return HitResult.Hit;
        }

        void OnHitTaken(HitReport r)
        {
            if (!_healPending) return;
            _healPending = false;
            _health.Heal(_health.Max * _healFraction);
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Second Wind!", Colour = new Color(1f, 0.9f, 0.5f) });
        }
    }
}
