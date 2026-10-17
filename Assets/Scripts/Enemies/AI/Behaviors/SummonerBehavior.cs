using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Keeps its distance and calls minions from the water after a long telegraph. It never has more than its cap alive,
    /// and when it dies its minions die with it. Kill it first.
    /// </summary>
    public class SummonerBehavior : EnemyBehavior
    {
        readonly List<EnemyController> _minions = new List<EnemyController>();

        public int LiveMinions
        {
            get
            {
                _minions.RemoveAll(m => m == null || m.State == EnemyState.Dead || m.Health.IsDead);
                return _minions.Count;
            }
        }

        public IReadOnlyList<EnemyController> Minions => _minions;

        public override Vector2 Chase(float dt)
        {
            float vx = HoldDistance(D.preferredMinDistance, D.preferredMaxDistance, E.Spd(D.chaseSpeed));
            if (E.CanAttack && D.minion != null && LiveMinions < D.maxMinions && E.HorizontalDistance < D.sightRange)
            {
                E.BeginTelegraph();
                return Vector2.zero;
            }
            return new Vector2(vx, 0f);
        }

        public override TelegraphSpec Telegraph()
            => new TelegraphSpec { Centre = E.Position + new Vector2(0f, 0.2f), Size = new Vector2(4.2f, 0.4f), Unparryable = true };

        public override void AttackBegin()
        {
            if (D.minion == null) return;
            int room = D.maxMinions - LiveMinions;
            int count = Mathf.Min(D.summonCount, room);
            for (int i = 0; i < count; i++)
            {
                float side = (i % 2 == 0 ? -1f : 1f) * (1.4f + 0.9f * (i / 2));
                var m = E.SpawnMinion(D.minion, E.Position + new Vector2(side, 0.1f));
                if (m != null) _minions.Add(m);
            }
        }

        public override void OnDied()
        {
            foreach (var m in _minions)
                if (m != null && !m.Health.IsDead)
                    m.Health.TakeHit(new DamageInfo { Amount = 1_000_000f, SourceTeam = Team.Neutral });
            _minions.Clear();
        }
    }
}
