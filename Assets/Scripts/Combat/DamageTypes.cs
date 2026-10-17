using System;
using UnityEngine;

namespace Roughlike.Combat
{
    public enum Team { Neutral, Player, Enemy }

    [Flags]
    public enum DamageTags
    {
        None = 0,
        Melee = 1 << 0,
        Projectile = 1 << 1,
        Unblockable = 1 << 2,
        Unparryable = 1 << 3,
        Hazard = 1 << 4
    }

    public enum HitResult
    {
        /// <summary>Damage applied.</summary>
        Hit,
        /// <summary>Reduced damage applied (shield).</summary>
        Blocked,
        /// <summary>No damage; the attacker is punished (shield, timed window).</summary>
        Parried,
        /// <summary>No damage (invulnerable, dead, same team).</summary>
        Ignored
    }

    /// <summary>Something that can be parried: an enemy, a projectile.</summary>
    public interface IAttacker
    {
        Team Team { get; }
        Transform Transform { get; }
        /// <summary>The attack was parried at <paramref name="point"/>. Stagger, reflect, cancel, etc.</summary>
        void OnAttackParried(Vector2 point, float stunDuration);
    }

    /// <summary>A defensive layer that can ignore, reduce or punish an incoming hit. Lower priority runs first.</summary>
    public interface IHitFilter
    {
        int Priority { get; }
        /// <summary>May modify <paramref name="info"/> (damage, knockback). Return Hit to pass the hit on unchanged.</summary>
        HitResult Filter(ref DamageInfo info);
    }

    /// <summary>Everything the defender needs to know about an incoming hit. Data-driven: weapons and enemies fill it from assets.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Team SourceTeam;
        public IAttacker Attacker;
        public Vector2 SourcePosition;
        public Vector2 HitPoint;
        /// <summary>World-space knockback velocity to apply to the victim.</summary>
        public Vector2 Knockback;
        /// <summary>Poise damage; enough of it staggers the victim.</summary>
        public float Stagger;
        public float HitStop;
        public float Shake;
        public DamageTags Tags;
        public bool IsCrit;
    }

    /// <summary>What happened when a hit landed. Raised on the victim and on the global event bus.</summary>
    public struct HitReport
    {
        public Health Target;
        public HitResult Result;
        public float Amount;          // damage actually applied
        public DamageInfo Info;       // after filters
        public Vector2 Position;
        public bool Killed;
    }

    public struct HitEvent
    {
        public HitReport Report;
    }
}
