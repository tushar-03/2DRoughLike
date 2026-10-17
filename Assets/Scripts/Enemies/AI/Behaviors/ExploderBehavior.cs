using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Enemies
{
    /// <summary>
    /// A swollen drowned thing that runs at you, inflates through its telegraph, and bursts in a ring. Back off or roll
    /// through it. Killing it from range is safest, but it also bursts when it dies, so do not be next to it.
    /// </summary>
    public class ExploderBehavior : EnemyBehavior
    {
        bool _exploded;

        public override Vector2 Chase(float dt)
        {
            int dir = E.DirToTarget;
            if (E.HorizontalDistance <= 2.3f && E.TargetOnSameLevel && E.CanAttack) { E.BeginTelegraph(); return Vector2.zero; }
            return new Vector2(WalkToward(dir, D.chaseSpeed), 0f);
        }

        public override TelegraphSpec Telegraph()
            => new TelegraphSpec
            {
                Centre = E.Position + new Vector2(0f, D.size.y * 0.5f),
                Size = new Vector2(D.explosionRadius * 2f, D.explosionRadius * 2f),
                Unparryable = true,
                Circle = true
            };

        public override void TelegraphTick(float progress01)
        {
            // swells up and trembles
            float k = 1f + 0.5f * progress01 + 0.04f * Mathf.Sin(E.Age * 60f);
            E.SetVisualScale(new Vector2(k, k));
        }

        public override void AttackBegin() => Explode();

        public override void OnDied()
        {
            if (!_exploded) Explode();
        }

        void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector2 centre = E.Position + new Vector2(0f, D.size.y * 0.5f);
            DamageInfo info = E.MakeAttack(DamageTags.Hazard);
            info.Knockback = new Vector2(D.knockback.x, D.knockback.y + 2f);
            AreaStrike.Circle(centre, D.explosionRadius, LayerMask.GetMask(GameLayers.PlayerName), info);
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = centre, Radius = D.explosionRadius, Colour = D.tint });
            E.SetVisualScale(Vector2.one);

            if (!E.Health.IsDead)
                E.Health.TakeHit(new DamageInfo { Amount = 1_000_000f, SourceTeam = Team.Neutral });
        }
    }
}
