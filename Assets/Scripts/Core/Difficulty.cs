using UnityEngine;

namespace Roughlike.Core
{
    public enum Difficulty { Story, Normal, Hard, Nightmare }

    /// <summary>How hard the enemies hit back. Chosen when a new game starts and stored with the run.</summary>
    public struct DifficultyScaling
    {
        public float Health;      // enemy and boss health
        public float Damage;      // damage they deal
        public float Speed;       // movement and charge speed
        public float Telegraph;   // wind-up time (lower is less time to react)
        public float Cooldown;    // time between attacks (lower is more aggressive)
        public int ExtraEnemies;  // added to every combat room
        public float Gold;        // gold and Cells drop multiplier
    }

    public static class DifficultyService
    {
        /// <summary>The difficulty of the run in progress (set by the run manager; tests and tools may set it directly).</summary>
        public static Difficulty Current = Difficulty.Normal;

        public static DifficultyScaling For(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Story:
                    return new DifficultyScaling { Health = 0.85f, Damage = 0.6f, Speed = 0.95f, Telegraph = 1.25f, Cooldown = 1.2f, ExtraEnemies = 0, Gold = 1f };
                case Difficulty.Hard:
                    return new DifficultyScaling { Health = 1.9f, Damage = 1.7f, Speed = 1.08f, Telegraph = 0.88f, Cooldown = 0.8f, ExtraEnemies = 2, Gold = 1.25f };
                case Difficulty.Nightmare:
                    return new DifficultyScaling { Health = 2.6f, Damage = 2.3f, Speed = 1.15f, Telegraph = 0.78f, Cooldown = 0.7f, ExtraEnemies = 3, Gold = 1.6f };
                default:
                    return new DifficultyScaling { Health = 1.4f, Damage = 1.3f, Speed = 1f, Telegraph = 1f, Cooldown = 0.9f, ExtraEnemies = 1, Gold = 1f };
            }
        }

        public static DifficultyScaling Scaling => For(Current);

        public static string Name(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Story: return "Story";
                case Difficulty.Hard: return "Hard";
                case Difficulty.Nightmare: return "Nightmare";
                default: return "Normal";
            }
        }

        public static string Description(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Story: return "For learning the game. Enemies are weaker and slower to strike.";
                case Difficulty.Hard: return "Tougher, faster enemies, shorter warnings, more of them. For players who know the game.";
                case Difficulty.Nightmare: return "Brutal. Everything hits hard and fast. Rewards are bigger.";
                default: return "The intended challenge. Enemies hit hard; learn their telegraphs.";
            }
        }

        /// <summary>
        /// Enemies get tougher deeper into a level and in later biomes, on top of the chosen difficulty.
        /// <paramref name="progress01"/> is the room's position along the level; <paramref name="levelIndex"/> is 0 for the first biome.
        /// </summary>
        public static float DepthHealth(float progress01, float levelIndex) => (1f + 0.5f * Mathf.Clamp01(progress01)) * (1f + 0.6f * levelIndex);
        public static float DepthDamage(float progress01, float levelIndex) => (1f + 0.25f * Mathf.Clamp01(progress01)) * (1f + 0.3f * levelIndex);
    }
}
