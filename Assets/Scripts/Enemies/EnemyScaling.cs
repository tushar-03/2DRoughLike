using Roughlike.Core;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Multipliers applied on top of an enemy's definition: the chosen difficulty plus how deep in the level and biome it is.
    /// The definition stays the authored baseline; tests and tools build enemies with <see cref="One"/> (no scaling).
    /// </summary>
    public struct EnemyScaling
    {
        public float Health, Damage, Speed, Telegraph, Cooldown;

        public static EnemyScaling One => new EnemyScaling { Health = 1f, Damage = 1f, Speed = 1f, Telegraph = 1f, Cooldown = 1f };

        public static EnemyScaling Create(float progress01, float levelIndex)
        {
            DifficultyScaling d = DifficultyService.Scaling;
            return new EnemyScaling
            {
                Health = d.Health * DifficultyService.DepthHealth(progress01, levelIndex),
                Damage = d.Damage * DifficultyService.DepthDamage(progress01, levelIndex),
                Speed = d.Speed,
                Telegraph = d.Telegraph,
                Cooldown = d.Cooldown
            };
        }
    }
}
