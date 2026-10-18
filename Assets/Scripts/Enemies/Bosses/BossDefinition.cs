using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    public enum BossKind { Witch, Warden }

    /// <summary>A boss: who it is, how tough, what it looks like. Its moves live in a <see cref="BossBrain"/> chosen by <see cref="kind"/>.</summary>
    [CreateAssetMenu(menuName = "Roughlike/Enemies/Boss Definition", fileName = "Boss")]
    public class BossDefinition : ScriptableObject
    {
        public string displayName = "Boss";
        public BossKind kind = BossKind.Witch;

        [Header("Body")]
        public float maxHealth = 1400f;
        public Vector2 size = new Vector2(2.2f, 3.4f);
        public CharacterArt art;
        public Color tint = Color.white;

        [Header("Fight")]
        [Tooltip("Base damage of the boss's attacks; each attack multiplies it.")]
        public float damage = 18f;
        [Tooltip("Health fractions at which it enters its next phase (more and faster attacks).")]
        public float[] phaseThresholds = { 0.66f, 0.33f };
        [Tooltip("How many health bars the fight has. Each bar is one phase: emptying it starts the next, and no hit can skip a bar.")]
        [Min(1)] public int healthBars = 1;
        [Tooltip("Optional look per phase (index = phase); an empty slot keeps the current look. The Warden grows wings in the last one.")]
        public CharacterArt[] phaseArt;
        [Tooltip("Minions the witch calls.")]
        public EnemyDefinition minion;

        [Header("Reward")]
        public int goldReward = 120;
    }
}
