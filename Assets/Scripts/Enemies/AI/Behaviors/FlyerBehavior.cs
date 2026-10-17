using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Hovers above the player. When it attacks it shows a line to where you are, tracks you for most of the telegraph,
    /// then dives down that line. Roll out of the line (or parry the dive); afterwards it climbs back, exposed.
    /// </summary>
    public class FlyerBehavior : EnemyBehavior
    {
        Vector2 _diveDir = Vector2.down;

        public override bool Flying => true;

        static Vector2 Steer(Vector2 from, Vector2 to, float speed) => Vector2.ClampMagnitude((to - from) * 4f, speed);

        Vector2 BodyCentre => E.Position + new Vector2(0f, D.size.y * 0.5f);

        public override Vector2 Patrol(float dt)
        {
            Vector2 home = E.Home;
            float x = home.x + Mathf.Sin(E.Age * 0.7f + E.Phase) * D.patrolRadius;
            float y = home.y + Mathf.Sin(E.Age * 1.4f + E.Phase) * 0.35f;
            return Steer(E.Position, new Vector2(x, y), D.patrolSpeed);
        }

        public override Vector2 Chase(float dt)
        {
            Vector2 target = E.TargetFeet;
            float x = target.x + Mathf.Sin(E.Age * 1.1f + E.Phase) * 2.2f;
            float y = target.y + D.hoverHeight + Mathf.Sin(E.Age * 2f + E.Phase) * 0.3f;

            if (E.HorizontalDistance < 5f && E.CanAttack && E.HasLineOfSightToTarget) E.BeginTelegraph();
            return Steer(E.Position, new Vector2(x, y), D.chaseSpeed);
        }

        void Aim(out Vector2 centre, out float length, out float angle)
        {
            Vector2 from = BodyCentre;
            Vector2 to = E.TargetCentre;
            Vector2 delta = to - from;
            _diveDir = delta.sqrMagnitude < 0.01f ? Vector2.down : delta.normalized;
            length = delta.magnitude + 1.5f;
            centre = from + _diveDir * (length * 0.5f);
            angle = Mathf.Atan2(_diveDir.y, _diveDir.x) * Mathf.Rad2Deg;
        }

        public override TelegraphSpec Telegraph()
        {
            Aim(out Vector2 centre, out float length, out float angle);
            return new TelegraphSpec { Centre = centre, Size = new Vector2(length, 0.3f), Angle = angle };
        }

        public override void TelegraphTick(float progress01)
        {
            // keeps tracking until 60% of the way through, then the line is locked
            if (progress01 > 0.6f) return;
            Aim(out Vector2 centre, out float length, out float angle);
            E.RetargetTelegraph(centre, new Vector2(length, 0.3f), angle);
        }

        public override Vector2 AttackTick(float dt)
        {
            E.StrikeBox(BodyCentre, D.size, E.MakeAttack(DamageTags.Melee));
            return _diveDir * D.diveSpeed;
        }

        public override bool AttackFinished(float stateTime)
        {
            var c = E.Motor.Collisions;
            return stateTime >= D.activeTime || c.below || c.left || c.right || c.above;
        }

        public override Vector2 Recover(float dt) => new Vector2(0f, 3.5f);
    }
}
