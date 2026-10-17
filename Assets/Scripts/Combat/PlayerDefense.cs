using UnityEngine;
using Roughlike.Player;

namespace Roughlike.Combat
{
    /// <summary>
    /// The player's first defensive layer: roll i-frames, post-hit invulnerability and the hit reaction
    /// (knockback, brief control loss). Shield logic lives in <see cref="PlayerCombat"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(Health))]
    public class PlayerDefense : MonoBehaviour, IHitFilter
    {
        [SerializeField] float postHitInvulnerability = 0.8f;
        [SerializeField] float hitControlLock = 0.2f;
        [SerializeField, Min(0f)] float knockbackScale = 1f;
        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        PlayerController _pc;
        Health _health;
        PlayerCombat _combat;
        HitReactionVisual _visual;
        float _invuln;

        public int Priority => 0;
        /// <summary>Debug: ignore all damage.</summary>
        public bool GodMode { get; set; }
        public bool IsInvulnerable => GodMode || _invuln > 0f || (_pc != null && _pc.IsInvulnerable);
        public float RemainingInvulnerability => _invuln;

        void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            _combat = GetComponent<PlayerCombat>();
            _visual = GetComponentInChildren<HitReactionVisual>();
        }

        void OnEnable()
        {
            _health.AddFilter(this);
            _health.HitTaken += OnHitTaken;
        }

        void OnDisable()
        {
            _health.RemoveFilter(this);
            _health.HitTaken -= OnHitTaken;
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt) => _invuln = Mathf.Max(0f, _invuln - dt);

        public void GrantInvulnerability(float seconds)
        {
            _invuln = Mathf.Max(_invuln, seconds);
            if (_visual != null) _visual.Blink(seconds);
        }

        public HitResult Filter(ref DamageInfo info)
            => IsInvulnerable ? HitResult.Ignored : HitResult.Hit;

        void OnHitTaken(HitReport report)
        {
            if (report.Result != HitResult.Hit || report.Amount <= 0f) return;

            GrantInvulnerability(postHitInvulnerability);
            if (_combat != null) _combat.Interrupt();
            _pc.ApplyHitReaction(report.Info.Knockback * knockbackScale, hitControlLock);
        }
    }
}

