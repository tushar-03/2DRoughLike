using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Items
{
    public enum SkillKind { Grenade, Turret, Trap }

    /// <summary>A cooldown ability: grenade, turret or trap. Two skill slots.</summary>
    [CreateAssetMenu(menuName = "Roughlike/Items/Skill", fileName = "Skill")]
    public class SkillData : ScriptableObject
    {
        public string displayName = "Skill";
        public SkillKind kind = SkillKind.Grenade;
        [TextArea] public string description;
        public Color tint = Color.white;
        public StatColor affinity = StatColor.Brine;

        [Header("Numbers")]
        public float cooldown = 10f;
        public float damage = 30f;
        public float radius = 2.2f;
        [Tooltip("Turret lifetime in seconds.")]
        public float duration = 8f;
        [Tooltip("Turret shots per second.")]
        public float fireRate = 2f;

        [Header("Status applied to enemies hit")]
        public StatusType status = StatusType.None;
        public float statusDuration = 3f;
        public float statusPotency = 1f;
    }
}
