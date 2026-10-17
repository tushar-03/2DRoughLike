using System.Collections;
using UnityEngine;

namespace Roughlike.Enemies
{
    /// <summary>
    /// What a boss does: movement between attacks and the attacks themselves, written as coroutines that use the
    /// <see cref="BossController"/> helpers (telegraphs, strikes, projectiles, waiting). The controller owns health, phases,
    /// stun, death and visuals.
    /// </summary>
    public abstract class BossBrain
    {
        protected BossController B { get; private set; }
        protected BossDefinition D => B.Definition;

        public void Bind(BossController controller) { B = controller; OnBind(); }
        protected virtual void OnBind() { }

        /// <summary>True when the boss flies (no gravity, moves on both axes).</summary>
        public virtual bool Flying => false;

        /// <summary>Seconds to rest between attacks in the given phase.</summary>
        public abstract float Rest(int phase);

        /// <summary>Choose and run the next attack.</summary>
        public abstract IEnumerator NextAttack(int phase);

        /// <summary>Motion while no attack is running (hovering, walking toward the player).</summary>
        public abstract Vector2 IdleVelocity(float dt);

        /// <summary>The boss just entered a new phase: a roar, minions, a rage. Runs between attacks.</summary>
        public virtual IEnumerator PhaseShout(int phase) { yield return B.WaitSeconds(0.8f); }

        /// <summary>Called when the boss dies (clean up anything it created).</summary>
        public virtual void OnDied() { }
    }
}
