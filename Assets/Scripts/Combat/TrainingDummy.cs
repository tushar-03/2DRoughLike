using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>
    /// Punching bag that never dies. Shows the last hit, a rolling total and DPS so weapon tuning is measurable.
    /// Also acts as a stationary attacker target for knockback testing.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] float resetAfterSeconds = 3f;

        Health _health;
        TextMesh _label;
        float _total, _lastHit, _firstHitTime, _lastHitTime;
        int _hits;

        public float TotalDamage => _total;
        public float LastHit => _lastHit;
        public int Hits => _hits;
        public float Dps => _hits == 0 ? 0f : _total / Mathf.Max(0.25f, _lastHitTime - _firstHitTime);

        void Awake() => _health = GetComponent<Health>();

        void OnEnable() => _health.HitTaken += OnHit;
        void OnDisable() => _health.HitTaken -= OnHit;

        public void AttachLabel(TextMesh label) => _label = label;

        void OnHit(HitReport r)
        {
            if (r.Amount <= 0f) return;
            if (_hits == 0) _firstHitTime = Time.time;
            _hits++;
            _total += r.Amount;
            _lastHit = r.Amount;
            _lastHitTime = Time.time;
            _health.ResetToFull();   // never dies
        }

        void Update()
        {
            if (_hits > 0 && Time.time - _lastHitTime > resetAfterSeconds)
            {
                _hits = 0; _total = 0f; _lastHit = 0f;
            }
            if (_label != null)
                _label.text = _hits == 0 ? "TRAINING DUMMY" : $"last {_lastHit:0}   total {_total:0}   dps {Dps:0}";
        }
    }
}
