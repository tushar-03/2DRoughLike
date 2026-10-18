using UnityEngine;
using Roughlike.Enemies;

namespace Roughlike.Rooms
{
    /// <summary>Per-biome generation and population rules plus the graybox palette. One asset per biome (M9 adds more).</summary>
    [CreateAssetMenu(menuName = "Roughlike/Rooms/Biome", fileName = "Biome")]
    public class BiomeData : ScriptableObject
    {
        public string displayName = "Ember Catacombs";

        public GeneratorSettings generator = new GeneratorSettings();

        [Header("Loot")]
        [Tooltip("Items, skills, gear, affixes and mutations for chests, shops and altars. Leave empty for a loot-free level.")]
        public Roughlike.Items.LootDatabase loot;

        [Header("Population")]
        public EnemyDefinition[] enemyPool;
        [Tooltip("Enemies in a combat room at the start of the level.")]
        public int minEnemies = 1;
        [Tooltip("Enemies in a combat room near the exit. Rooms in between scale linearly.")]
        public int maxEnemies = 3;

        [Header("Boss")]
        [Tooltip("The boss waiting in the final room. Leave empty for a plain exit.")]
        public BossDefinition boss;
        [Tooltip("Ambient sound: false is the low cavern drone, true is wind over open ruins.")]
        public bool windyAmbience;

        [Header("Art")]
        [Tooltip("Tiles, backdrop, lighting and post-processing. Leave empty for the plain graybox look.")]
        public Roughlike.Utils.EnvironmentArt art;

        [Header("Graybox palette")]
        public Color ground = new Color(0.20f, 0.23f, 0.30f);
        public Color trim = new Color(0.34f, 0.38f, 0.48f);
        public Color platform = new Color(0.95f, 0.70f, 0.25f);
        public Color background = new Color(0.10f, 0.12f, 0.17f);
        public Color accent = new Color(0.35f, 0.85f, 0.85f);
    }
}
