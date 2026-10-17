using UnityEngine;

namespace Roughlike.Items
{
    public enum MutationEffect
    {
        None,
        /// <summary>Heal this many HP on every kill.</summary>
        HealOnKill,
        /// <summary>After a perfect parry, the next swing deals this multiple of damage.</summary>
        ParryRiposte,
        /// <summary>Each kill adds this much damage for a few seconds, stacking up to 5 times.</summary>
        Momentum,
        /// <summary>Once per run, a lethal hit leaves you at 1 HP and heals you to this fraction of max health.</summary>
        SecondWind
    }

    /// <summary>A passive perk chosen at altars. Limited slots.</summary>
    [CreateAssetMenu(menuName = "Roughlike/Items/Mutation", fileName = "Mutation")]
    public class MutationData : ScriptableObject
    {
        public string displayName = "Mutation";
        [TextArea] public string description;
        public Color tint = new Color(0.8f, 0.5f, 1f);
        public StatEntry[] modifiers;
        public MutationEffect effect = MutationEffect.None;
        public float effectValue;
    }
}
