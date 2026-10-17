using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Enemies
{
    /// <summary>The danger area an enemy shows while telegraphing. Angle rotates it (laser lines, dive paths).</summary>
    public struct TelegraphSpec
    {
        public Vector2 Centre;
        public Vector2 Size;
        public float Angle;
        public bool Unparryable;
        /// <summary>Draw a circle (explosions) instead of a box.</summary>
        public bool Circle;
    }

    /// <summary>
    /// What makes an archetype different: how it moves while chasing, when it attacks, what its telegraph looks like, and
    /// what the attack does. <see cref="EnemyController"/> owns the shared state machine (patrol, chase, telegraph, attack,
    /// recover, stagger, dead), poise, knockback and visuals; behaviours plug in at those points.
    /// </summary>
    public abstract class EnemyBehavior
    {
        protected EnemyController E { get; private set; }
        protected EnemyDefinition D => E.Definition;

        public void Bind(EnemyController controller)
        {
            E = controller;
            OnBind();
        }

        protected virtual void OnBind() { }

        /// <summary>Flying enemies ignore gravity and move on both axes.</summary>
        public virtual bool Flying => false;

        /// <summary>Desired velocity while patrolling. Ground enemies pace around their home point.</summary>
        public virtual Vector2 Patrol(float dt) => new Vector2(E.GroundPatrol(dt), 0f);

        /// <summary>Desired velocity while chasing. Call <see cref="EnemyController.BeginTelegraph"/> to start an attack.</summary>
        public abstract Vector2 Chase(float dt);

        /// <summary>The danger area to show (also used for hit detection by melee archetypes).</summary>
        public abstract TelegraphSpec Telegraph();

        /// <summary>Called every frame of the telegraph with its progress 0..1 (aim tracking, inflating...).</summary>
        public virtual void TelegraphTick(float progress01) { }

        /// <summary>The telegraph finished and the attack starts.</summary>
        public virtual void AttackBegin() { }

        /// <summary>Per-frame during the attack. Returns the velocity to move with; call <see cref="EnemyController.EndAttack"/> to finish early.</summary>
        public virtual Vector2 AttackTick(float dt) => Vector2.zero;

        /// <summary>True when the attack should end. By default it lasts <see cref="EnemyDefinition.activeTime"/>.</summary>
        public virtual bool AttackFinished(float stateTime) => stateTime >= D.activeTime;

        /// <summary>Velocity during the recover window.</summary>
        public virtual Vector2 Recover(float dt) => Vector2.zero;

        /// <summary>Gets first look at every hit (shields, armour). Return Hit to pass it on.</summary>
        public virtual HitResult Filter(ref DamageInfo info) => HitResult.Hit;

        /// <summary>Called every frame the enemy is alive, whatever its state (timers).</summary>
        public virtual void Tick(float dt) { }

        public virtual void OnStaggered() { }
        public virtual void OnDied() { }
        public virtual void OnHit(HitReport report) { }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>Walk toward or away from the target on the ground, stopping at walls and ledges.</summary>
        protected float WalkToward(int dir, float speed) => E.BlockedAhead(dir) ? 0f : dir * speed;

        /// <summary>Keep within [min, max] of the target; returns the walking velocity, or 0 when in the band.</summary>
        protected float HoldDistance(float min, float max, float speed)
        {
            int dir = E.DirToTarget;
            float dist = E.HorizontalDistance;
            if (dist < min) return E.BlockedAhead(-dir) ? 0f : -dir * speed * 0.9f;
            if (dist > max) return E.BlockedAhead(dir) ? 0f : dir * speed;
            return 0f;
        }
    }
}
