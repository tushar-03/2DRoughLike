using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>Holds a distance band, shows a short aim line, then fires one projectile straight ahead.</summary>
    public class RangedBehavior : EnemyBehavior
    {
        bool _fired;

        public override Vector2 Chase(float dt)
        {
            float vx = HoldDistance(D.preferredMinDistance, D.preferredMaxDistance, E.Spd(D.chaseSpeed));
            bool inBand = vx == 0f;
            if (inBand && E.CanAttack && E.TargetOnSameLevel && E.HasLineOfSightToTarget) E.BeginTelegraph();
            return new Vector2(vx, 0f);
        }

        public override TelegraphSpec Telegraph()
            => new TelegraphSpec
            {
                Centre = E.Muzzle + new Vector2(E.Facing * D.telegraphSize.x * 0.5f, 0f),
                Size = D.telegraphSize
            };

        public override void AttackBegin()
        {
            _fired = false;
        }

        public override Vector2 AttackTick(float dt)
        {
            if (_fired) return Vector2.zero;
            _fired = true;
            Projectile.Spawn(E.Muzzle, new Vector2(E.Facing * D.projectileSpeed, 0f), E.Damage, Team.Enemy, D.knockback.x, D.stagger);
            return Vector2.zero;
        }
    }
}
