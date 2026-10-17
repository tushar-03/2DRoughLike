using UnityEngine;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Stat blocks for the enemies beyond the first two. One place that says what each archetype's numbers are, shared by the
    /// editor content builder and the tests, so the assets and the tests cannot drift apart.
    /// </summary>
    public static class DefaultEnemies
    {
        static EnemyDefinition New(string name, EnemyArchetype archetype, float health, Vector2 size, Color tint)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.name = name;
            e.displayName = name;
            e.archetype = archetype;
            e.maxHealth = health;
            e.size = size;
            e.tint = tint;
            return e;
        }

        /// <summary>Hovers above you and dives along a line that tracks you, then locks.</summary>
        public static EnemyDefinition BellWisp()
        {
            var e = New("Bell-Wisp", EnemyArchetype.Flyer, 22f, new Vector2(0.9f, 0.9f), new Color(0.6f, 0.85f, 1f));
            e.minProgress = 0.1f; e.maxPerRoom = 2;
            e.patrolSpeed = 2f; e.chaseSpeed = 5f; e.patrolRadius = 3.5f;
            e.sightRange = 12f; e.loseSightRange = 16f; e.verticalSight = 9f; e.rearAwareness = 12f;
            e.telegraphTime = 0.8f; e.activeTime = 0.6f; e.recoverTime = 0.9f; e.attackCooldown = 1.8f;
            e.damage = 12f; e.knockback = new Vector2(6f, 3f); e.stagger = 10f;
            e.hoverHeight = 3.2f; e.diveSpeed = 15f;
            e.staggerThreshold = 12f; e.staggerTime = 0.5f;
            return e;
        }

        /// <summary>Frontal shield; break it with a heavy blow or a parry, or hit it from behind.</summary>
        public static EnemyDefinition ShellGuard()
        {
            var e = New("Shell Guard", EnemyArchetype.ShieldBearer, 90f, new Vector2(1.1f, 1.6f), new Color(0.5f, 0.6f, 0.75f));
            e.minProgress = 0.25f; e.maxPerRoom = 2; e.rewardMultiplier = 1.5f;
            e.patrolSpeed = 1.2f; e.chaseSpeed = 2.6f; e.patrolRadius = 3f;
            e.attackRange = 1.8f; e.telegraphTime = 0.8f; e.activeTime = 0.15f; e.recoverTime = 0.8f; e.attackCooldown = 1.4f;
            e.damage = 20f; e.knockback = new Vector2(9f, 4f); e.stagger = 20f;
            e.hitOffset = new Vector2(1.1f, 0.8f); e.hitSize = new Vector2(1.8f, 1.4f);
            e.shieldReduction = 0.85f; e.shieldBreakStagger = 18f; e.shieldBreakTime = 3f; e.backstabMultiplier = 1.3f;
            e.staggerThreshold = 40f; e.staggerTime = 0.5f; e.knockbackResistance = 0.6f;
            return e;
        }

        /// <summary>Paws the ground, then charges the length of the room; wall slams stun it.</summary>
        public static EnemyDefinition Tideboar()
        {
            var e = New("Tideboar", EnemyArchetype.Charger, 70f, new Vector2(1.5f, 1.1f), new Color(0.55f, 0.4f, 0.3f));
            e.minProgress = 0.3f; e.maxPerRoom = 2;
            e.patrolSpeed = 1.8f; e.chaseSpeed = 3f; e.patrolRadius = 4f; e.sightRange = 12f; e.loseSightRange = 16f;
            e.telegraphTime = 0.9f; e.activeTime = 1.1f; e.recoverTime = 0.6f; e.attackCooldown = 1.5f;
            e.damage = 22f; e.knockback = new Vector2(10f, 5f); e.stagger = 25f;
            e.chargeSpeed = 13f; e.wallStunTime = 1.6f;
            e.staggerThreshold = 30f; e.staggerTime = 0.5f; e.knockbackResistance = 0.4f;
            return e;
        }

        /// <summary>The thing a Reliquary Shade calls. Weak, fast, worth nothing.</summary>
        public static EnemyDefinition DrownedWretch()
        {
            var e = New("Drowned Wretch", EnemyArchetype.MeleeGrunt, 14f, new Vector2(0.7f, 1.1f), new Color(0.45f, 0.55f, 0.5f));
            e.rewardMultiplier = 0f;
            e.patrolSpeed = 2f; e.chaseSpeed = 5f; e.sightRange = 14f; e.loseSightRange = 18f;
            e.attackRange = 1.3f; e.telegraphTime = 0.4f; e.activeTime = 0.1f; e.recoverTime = 0.5f; e.attackCooldown = 0.8f;
            e.damage = 7f; e.knockback = new Vector2(4f, 2f); e.stagger = 6f;
            e.hitOffset = new Vector2(0.8f, 0.55f); e.hitSize = new Vector2(1.2f, 1f);
            e.staggerThreshold = 8f; e.staggerTime = 0.35f;
            return e;
        }

        /// <summary>Hangs back and keeps summoning minions. Kill it and they die too.</summary>
        public static EnemyDefinition ReliquaryShade(EnemyDefinition minion)
        {
            var e = New("Reliquary Shade", EnemyArchetype.Summoner, 50f, new Vector2(0.9f, 1.7f), new Color(0.6f, 0.4f, 0.8f));
            e.minProgress = 0.45f; e.maxPerRoom = 1; e.rewardMultiplier = 1.5f;
            e.patrolSpeed = 1.2f; e.chaseSpeed = 2.5f; e.patrolRadius = 3f; e.sightRange = 13f; e.loseSightRange = 17f;
            e.preferredMinDistance = 6f; e.preferredMaxDistance = 10f;
            e.telegraphTime = 1.2f; e.activeTime = 0.3f; e.recoverTime = 0.8f; e.attackCooldown = 4f;
            e.minion = minion; e.summonCount = 2; e.maxMinions = 4;
            e.staggerThreshold = 20f; e.staggerTime = 0.5f;
            return e;
        }

        /// <summary>Runs at you, swells, bursts. Also bursts when killed.</summary>
        public static EnemyDefinition Bloat()
        {
            var e = New("Bloat", EnemyArchetype.Exploder, 24f, new Vector2(1f, 1f), new Color(0.75f, 0.8f, 0.35f));
            e.minProgress = 0.2f; e.maxPerRoom = 2;
            e.patrolSpeed = 1.8f; e.chaseSpeed = 3.6f; e.sightRange = 11f;
            e.telegraphTime = 0.9f; e.activeTime = 0.1f; e.recoverTime = 0.4f; e.attackCooldown = 1f;
            e.damage = 24f; e.knockback = new Vector2(9f, 6f); e.stagger = 25f; e.explosionRadius = 2.6f;
            e.staggerThreshold = 20f; e.staggerTime = 0.4f;
            return e;
        }

        /// <summary>Far away; a laser line tracks you, locks, then a fast bolt follows it.</summary>
        public static EnemyDefinition EelArcher()
        {
            var e = New("Eel Archer", EnemyArchetype.Sniper, 28f, new Vector2(0.7f, 1.5f), new Color(0.3f, 0.5f, 0.75f));
            e.minProgress = 0.5f; e.maxPerRoom = 2; e.rewardMultiplier = 1.25f;
            e.patrolSpeed = 1.2f; e.chaseSpeed = 3f; e.patrolRadius = 2.5f;
            e.sightRange = 18f; e.loseSightRange = 22f; e.verticalSight = 6f;
            e.preferredMinDistance = 9f; e.preferredMaxDistance = 14f;
            e.muzzleOffset = new Vector2(0.5f, 1f);
            e.telegraphTime = 1.4f; e.aimLockFraction = 0.7f; e.activeTime = 0.1f; e.recoverTime = 0.8f; e.attackCooldown = 2.5f;
            e.damage = 22f; e.projectileSpeed = 24f; e.knockback = new Vector2(7f, 3f); e.stagger = 14f;
            e.staggerThreshold = 14f; e.staggerTime = 0.5f;
            return e;
        }
    }
}
