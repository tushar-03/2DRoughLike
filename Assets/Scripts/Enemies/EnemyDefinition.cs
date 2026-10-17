using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    public enum EnemyArchetype
    {
        /// <summary>Walks up and swings after a telegraph.</summary>
        MeleeGrunt,
        /// <summary>Keeps its distance and shoots a projectile.</summary>
        RangedShooter,
        /// <summary>Hovers over the player and dives at where they stood when it telegraphed.</summary>
        Flyer,
        /// <summary>Frontal shield soaks hits; heavy blows and parries break it, attacks from behind hurt.</summary>
        ShieldBearer,
        /// <summary>Paws the ground, then charges in a straight line; slams into walls and is left stunned.</summary>
        Charger,
        /// <summary>Hangs back and calls minions; they fall with it.</summary>
        Summoner,
        /// <summary>Runs at you, swells, then bursts; also bursts when killed.</summary>
        Exploder,
        /// <summary>Far away, paints a laser line, then fires one fast, hard shot.</summary>
        Sniper
    }

    /// <summary>Everything that differs between enemy types. New enemies are new assets, not new code (until a new archetype is needed).</summary>
    [CreateAssetMenu(menuName = "Roughlike/Enemies/Enemy Definition", fileName = "Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Enemy";
        public EnemyArchetype archetype = EnemyArchetype.MeleeGrunt;

        [Header("Body")]
        public float maxHealth = 40f;
        public Vector2 size = new Vector2(0.9f, 1.5f);
        [Tooltip("Animated art. When set it replaces the sprite and the tint.")]
        public CharacterArt art;
        [Tooltip("Optional. Falls back to a tinted block when empty. Pivot at the feet.")]
        public Sprite sprite;
        public Color tint = new Color(0.85f, 0.35f, 0.3f);

        [Header("Spawning")]
        [Range(0f, 1f), Tooltip("Only appears in rooms at least this far along the level (0 = from the start).")]
        public float minProgress = 0f;
        [Min(1), Tooltip("Most of this enemy in a single room.")]
        public int maxPerRoom = 3;
        [Tooltip("Gold dropped is multiplied by this.")]
        public float rewardMultiplier = 1f;

        [Header("Movement")]
        public float patrolSpeed = 2f;
        public float chaseSpeed = 4.5f;
        public float patrolRadius = 4f;

        [Header("Senses")]
        public float sightRange = 9f;
        public float loseSightRange = 14f;
        public float verticalSight = 2.5f;
        [Tooltip("Awareness radius behind the enemy.")]
        public float rearAwareness = 1.5f;

        [Header("Attack timing (the telegraph is the windup)")]
        [Tooltip("Distance at which a melee enemy starts attacking.")]
        public float attackRange = 1.6f;
        public float telegraphTime = 0.55f;
        [Tooltip("Active time of the attack. For chargers and divers this is the longest the rush or dive can last.")]
        public float activeTime = 0.12f;
        [Tooltip("Vulnerable punish window after the attack.")]
        public float recoverTime = 0.6f;
        public float attackCooldown = 0.8f;

        [Header("Attack data")]
        public float damage = 12f;
        public Vector2 knockback = new Vector2(7f, 4f);
        public float stagger = 10f;
        [Tooltip("Melee hitbox: x forward, y up from the feet.")]
        public Vector2 hitOffset = new Vector2(1.0f, 0.75f);
        public Vector2 hitSize = new Vector2(1.6f, 1.3f);

        [Header("Ranged / sniper")]
        public float preferredMinDistance = 5f;
        public float preferredMaxDistance = 8f;
        public float projectileSpeed = 11f;
        public Vector2 muzzleOffset = new Vector2(0.6f, 0.9f);
        public Vector2 telegraphSize = new Vector2(2.2f, 0.2f);
        [Range(0.3f, 1f), Tooltip("Sniper: fraction of the telegraph during which the laser still tracks the player; it locks after that.")]
        public float aimLockFraction = 0.7f;

        [Header("Flyer")]
        [Tooltip("Height above the player's feet it hovers at.")]
        public float hoverHeight = 3.2f;
        public float diveSpeed = 15f;

        [Header("Shield bearer")]
        [Range(0f, 1f), Tooltip("Fraction of damage the front shield absorbs.")]
        public float shieldReduction = 0.85f;
        [Tooltip("A single hit with at least this much stagger (or a parry) breaks the shield.")]
        public float shieldBreakStagger = 20f;
        public float shieldBreakTime = 3f;
        [Tooltip("Damage multiplier for hits from behind.")]
        public float backstabMultiplier = 1.3f;

        [Header("Charger")]
        public float chargeSpeed = 13f;
        [Tooltip("Stun after slamming into a wall.")]
        public float wallStunTime = 1.6f;

        [Header("Summoner")]
        public EnemyDefinition minion;
        [Min(1)] public int summonCount = 2;
        [Min(1)] public int maxMinions = 4;

        [Header("Exploder")]
        public float explosionRadius = 2.6f;

        [Header("Poise")]
        [Tooltip("Accumulated stagger from hits needed to flinch.")]
        public float staggerThreshold = 18f;
        public float staggerTime = 0.45f;
        [Range(0f, 1f)] public float knockbackResistance = 0f;
        [Tooltip("Damage multiplier while stunned (parry reward).")]
        public float stunnedDamageMultiplier = 1.5f;
    }
}
