using UnityEngine;

namespace Roughlike.Enemies
{
    public enum EnemyArchetype { MeleeGrunt, RangedShooter }

    /// <summary>Everything that differs between enemy types. New enemies are new assets, not new code (until a new archetype is needed).</summary>
    [CreateAssetMenu(menuName = "Roughlike/Enemies/Enemy Definition", fileName = "Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Enemy";
        public EnemyArchetype archetype = EnemyArchetype.MeleeGrunt;

        [Header("Body")]
        public float maxHealth = 40f;
        public Vector2 size = new Vector2(0.9f, 1.5f);
        [Tooltip("Optional. Falls back to a tinted block when empty. Pivot at the feet.")]
        public Sprite sprite;
        public Color tint = new Color(0.85f, 0.35f, 0.3f);

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

        [Header("Ranged")]
        public float preferredMinDistance = 5f;
        public float preferredMaxDistance = 8f;
        public float projectileSpeed = 11f;
        public Vector2 muzzleOffset = new Vector2(0.6f, 0.9f);
        public Vector2 telegraphSize = new Vector2(2.2f, 0.2f);

        [Header("Poise")]
        [Tooltip("Accumulated stagger from hits needed to flinch.")]
        public float staggerThreshold = 18f;
        public float staggerTime = 0.45f;
        [Range(0f, 1f)] public float knockbackResistance = 0f;
        [Tooltip("Damage multiplier while stunned (parry reward).")]
        public float stunnedDamageMultiplier = 1.5f;
    }
}
