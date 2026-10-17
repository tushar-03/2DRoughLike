using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Plods toward you, paws the ground (the telegraph shows the whole charge lane), then rushes in a straight line.
    /// Dodge by rolling through it or jumping over; if it slams into a wall it is stunned and wide open.
    /// </summary>
    public class ChargerBehavior : EnemyBehavior
    {
        int _dir = 1;

        public override Vector2 Chase(float dt)
        {
            int dir = E.DirToTarget;
            float dist = E.HorizontalDistance;
            if (dist >= 3f && dist <= 11f && E.TargetOnSameLevel && E.HasLineOfSightToTarget && E.CanAttack && !E.BlockedAhead(dir))
            {
                E.BeginTelegraph();
                return Vector2.zero;
            }
            if (dist < 2.2f) return Vector2.zero;
            return new Vector2(WalkToward(dir, E.Spd(D.chaseSpeed)), 0f);
        }

        public override TelegraphSpec Telegraph()
        {
            float length = Mathf.Min(E.Spd(D.chargeSpeed) * D.activeTime, 12f);
            return new TelegraphSpec
            {
                Centre = E.Position + new Vector2(E.Facing * (length * 0.5f + D.size.x * 0.4f), D.size.y * 0.45f),
                Size = new Vector2(length, D.size.y * 0.85f)
            };
        }

        public override void AttackBegin() => _dir = E.Facing;

        public override Vector2 AttackTick(float dt)
        {
            E.StrikeBox(E.Position + new Vector2(_dir * D.size.x * 0.3f, D.size.y * 0.5f), D.size + new Vector2(0.3f, 0f), E.MakeAttack(DamageTags.Melee));

            var c = E.Motor.Collisions;
            bool wall = (_dir > 0 && c.right) || (_dir < 0 && c.left);
            if (wall)
            {
                E.EnterStagger(D.wallStunTime);          // slammed into a wall: stunned and vulnerable
                return Vector2.zero;
            }
            if (E.LedgeAhead(_dir))
            {
                E.EndAttack();                           // a drop ahead: pull up rather than run off it
                return Vector2.zero;
            }
            return new Vector2(_dir * E.Spd(D.chargeSpeed), 0f);
        }
    }
}
