using System;
using UnityEngine;

namespace Roughlike.Combat
{
    public enum WeaponKind { Melee, Shield, Bow }

    /// <summary>One hit of a combo. All timings in seconds, all offsets in world units relative to the player's feet.</summary>
    [Serializable]
    public class AttackStep
    {
        public string name = "Slash";
        [Tooltip("Which player animation clip plays for this hit (empty = the generic swing).")]
        public string clip = "";
        public float damage = 10f;

        [Header("Timing")]
        [Tooltip("Anticipation before the hitbox goes live. Facing is locked.")]
        public float windup = 0.08f;
        [Tooltip("How long the hitbox stays live.")]
        public float active = 0.08f;
        [Tooltip("Recovery after the swing. A queued attack or roll can cut it short.")]
        public float recovery = 0.18f;
        [Range(0f, 1f), Tooltip("Fraction of recovery that must pass before the next combo step can start.")]
        public float chainFraction = 0.4f;

        [Header("Hitbox (x is forward, y is up from the feet)")]
        public Vector2 hitOffset = new Vector2(0.9f, 0.9f);
        public Vector2 hitSize = new Vector2(1.5f, 1.2f);

        [Header("Impact")]
        [Tooltip("Knockback velocity; x is multiplied by the attack direction.")]
        public Vector2 knockback = new Vector2(5f, 2f);
        [Tooltip("Poise damage dealt to the target.")]
        public float stagger = 10f;
        public float hitStop = 0.06f;
        public float shake = 0.25f;

        [Header("Movement")]
        [Tooltip("Forward speed added when the hitbox goes live.")]
        public float lunge = 3f;
        [Range(0f, 1f), Tooltip("Run speed multiplier during this attack.")]
        public float moveScale = 0.35f;
    }

    [Serializable]
    public class ShieldSettings
    {
        [Tooltip("After raising the shield, hits inside this window are parried.")]
        public float parryWindow = 0.18f;
        [Range(0f, 1f), Tooltip("Fraction of damage removed by a normal block.")]
        public float blockDamageReduction = 0.85f;
        [Range(0f, 1f)] public float moveScale = 0.4f;
        [Tooltip("How long a parried enemy is stunned.")]
        public float parryStun = 1.2f;
        [Tooltip("Brief invulnerability after a perfect parry.")]
        public float parryInvulnerability = 0.35f;
        public float blockPushback = 4f;
        public float parryHitStop = 0.12f;
        public float parryShake = 0.5f;
        [Tooltip("Time to lower the shield after release.")]
        public float lowerTime = 0.08f;
    }

    /// <summary>
    /// Data for one weapon. Everything about how it plays lives here: no per-weapon code.
    /// Melee weapons use <see cref="steps"/>; shields use <see cref="shield"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Roughlike/Combat/Weapon", fileName = "Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Weapon";
        public WeaponKind kind = WeaponKind.Melee;
        [Tooltip("Slash trail and hit spark tint.")]
        public Color tint = Color.white;
        [Tooltip("Which scroll stats scale this weapon's damage. None means weakly scaled by all of them.")]
        public Roughlike.Core.StatColor affinity = Roughlike.Core.StatColor.None;
        [TextArea, Tooltip("Flavour text shown in item cards.")]
        public string description;
        [Tooltip("Painted art. Melee: blade pointing up, pivot at the grip. Shield: face on, pivot at the centre. Used in the hand, in inventory and on the ground.")]
        public Sprite sprite;
        [Tooltip("Size of the sprite when carried (1 = as painted).")]
        public float heldScale = 1f;
        [Tooltip("What is drawn in the hand when it differs from the item art (the twin blades are one icon but two swords).")]
        public Sprite heldSprite;
        [Tooltip("A second copy is drawn in the off hand.")]
        public bool dual;

        [Header("Melee")]
        public AttackStep[] steps = { new AttackStep() };
        [Tooltip("Idle time after the last swing before the combo restarts from the first hit.")]
        public float comboResetTime = 0.45f;

        [Header("Bow")]
        [Tooltip("Arrow speed in units per second (bows only).")]
        public float projectileSpeed = 26f;
        [Tooltip("How far a bow looks for something to aim at, and how far its arrows fly.")]
        public float range = 18f;
        [Tooltip("How many enemies an arrow passes through before it stops.")]
        public int pierce;

        [Header("Shield")]
        public ShieldSettings shield = new ShieldSettings();
        [Tooltip("Animation played while this shield is raised.")]
        public string blockClip = "block_tower";
    }
}
