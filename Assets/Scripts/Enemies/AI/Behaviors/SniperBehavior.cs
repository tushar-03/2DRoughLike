using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Stays far away. The telegraph is a laser line that follows you, then locks; the shot goes down that line, fast and
    /// hard. Break the line (roll, duck behind cover) after it locks, or parry the bolt to send it back.
    /// </summary>
    public class SniperBehavior : EnemyBehavior
    {
        Vector2 _aimDir = Vector2.right;
        bool _fired;

        public override Vector2 Chase(float dt)
        {
            float vx = HoldDistance(D.preferredMinDistance, D.preferredMaxDistance, E.Spd(D.chaseSpeed));
            bool inBand = vx == 0f;
            if (inBand && E.CanAttack && E.HasLineOfSightToTarget && E.HorizontalDistance < D.sightRange) E.BeginTelegraph();
            return new Vector2(vx, 0f);
        }

        void Aim(out Vector2 centre, out float length, out float angle)
        {
            Vector2 from = E.Muzzle;
            Vector2 delta = E.TargetCentre - from;
            _aimDir = delta.sqrMagnitude < 0.01f ? new Vector2(E.Facing, 0f) : delta.normalized;
            length = Mathf.Min(delta.magnitude + 3f, 22f);
            centre = from + _aimDir * (length * 0.5f);
            angle = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
        }

        public override TelegraphSpec Telegraph()
        {
            Aim(out Vector2 centre, out float length, out float angle);
            return new TelegraphSpec { Centre = centre, Size = new Vector2(length, 0.12f), Angle = angle };
        }

        public override void TelegraphTick(float progress01)
        {
            if (progress01 > D.aimLockFraction) return;     // locked: the line stops following you
            Aim(out Vector2 centre, out float length, out float angle);
            E.RetargetTelegraph(centre, new Vector2(length, 0.12f), angle);
        }

        public override void AttackBegin() => _fired = false;

        public override Vector2 AttackTick(float dt)
        {
            if (_fired) return Vector2.zero;
            _fired = true;
            Projectile.Spawn(E.Muzzle, _aimDir * D.projectileSpeed, E.Damage, Team.Enemy, D.knockback.x, D.stagger, 3f, 0.16f);
            return Vector2.zero;
        }
    }

    /// <summary>Maps an archetype to its behaviour.</summary>
    public static class EnemyBehaviors
    {
        public static EnemyBehavior For(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.RangedShooter: return new RangedBehavior();
                case EnemyArchetype.Flyer: return new FlyerBehavior();
                case EnemyArchetype.ShieldBearer: return new ShieldBehavior();
                case EnemyArchetype.Charger: return new ChargerBehavior();
                case EnemyArchetype.Summoner: return new SummonerBehavior();
                case EnemyArchetype.Exploder: return new ExploderBehavior();
                case EnemyArchetype.Sniper: return new SniperBehavior();
                default: return new MeleeBehavior();
            }
        }
    }
}
