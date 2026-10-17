using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>Walks up to the player, telegraphs a swing in front, then hits everything in the swing box once.</summary>
    public class MeleeBehavior : EnemyBehavior
    {
        public override Vector2 Chase(float dt)
        {
            int dir = E.DirToTarget;
            float dist = E.HorizontalDistance;

            if (dist <= D.attackRange && E.TargetOnSameLevel && E.CanAttack) { E.BeginTelegraph(); return Vector2.zero; }
            if (dist <= D.attackRange * 0.7f) return Vector2.zero;
            return new Vector2(WalkToward(dir, D.chaseSpeed), 0f);
        }

        public override TelegraphSpec Telegraph()
            => new TelegraphSpec { Centre = E.MeleeCentre(), Size = D.hitSize };

        public override Vector2 AttackTick(float dt)
        {
            E.StrikeBox(E.MeleeCentre(), D.hitSize, E.MakeAttack(DamageTags.Melee));
            return Vector2.zero;
        }
    }
}
