using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Editor.Art;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Rooms;

namespace Roughlike.Editor
{
    /// <summary>Creates the two biomes (Ember Catacombs and Ashen Promenade) and their bosses, and keeps their assets up to date.</summary>
    public static class BiomeContentBuilder
    {
        const string BossFolder = "Assets/Data/Bosses";
        const string BiomeFolder = "Assets/Data/Biomes";

        public static BossDefinition LoadOrCreateWitch()
        {
            var def = LoadOrCreateBoss("HagOfBells", d =>
            {
                d.displayName = "The Hag of Bells";
                d.kind = BossKind.Witch;
                d.maxHealth = 1000f;
                d.size = new Vector2(2.0f, 3.2f);
                d.damage = 17f;
                d.phaseThresholds = new[] { 0.66f, 0.33f };
                d.goldReward = 140;
            });
            def.art = CharacterSheetBuilder.Load("HagOfBells");
            def.minion = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedWretch.asset");
            EditorUtility.SetDirty(def);
            return def;
        }

        public static BossDefinition LoadOrCreateWarden()
        {
            var def = LoadOrCreateBoss("RustWarden", d =>
            {
                d.displayName = "The Rust Warden";
                d.kind = BossKind.Warden;
                d.maxHealth = 1400f;
                d.size = new Vector2(2.4f, 3.0f);
                d.damage = 22f;
                d.phaseThresholds = new[] { 0.66f, 0.33f };
                d.goldReward = 220;
            });
            def.art = CharacterSheetBuilder.Load("RustWarden");
            EditorUtility.SetDirty(def);
            return def;
        }

        static BossDefinition LoadOrCreateBoss(string name, System.Action<BossDefinition> init)
        {
            Directory.CreateDirectory(BossFolder);
            string path = $"{BossFolder}/{name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<BossDefinition>();
            init(def);   // stats come from code every build, so tuning here always reaches the asset
            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>The first biome: damp catacombs, ending with the witch.</summary>
        public static BiomeData BuildCatacombs(LootDatabase db)
        {
            var biome = LoadOrCreateBiome("EmberCatacombs", "Ember Catacombs");
            biome.enemyPool = EnemyContentBuilder.BuildPool();
            EnvironmentBuilder.Apply(biome);
            biome.loot = db;
            biome.boss = LoadOrCreateWitch();
            biome.minEnemies = 2;
            biome.maxEnemies = 5;
            biome.accent = new Color(0.35f, 0.85f, 0.85f);
            EditorUtility.SetDirty(biome);
            return biome;
        }

        /// <summary>The second biome: a ruined promenade at sunset, longer and busier, ending with the greatsword knight.</summary>
        public static BiomeData BuildPromenade(LootDatabase db)
        {
            var biome = LoadOrCreateBiome("AshenPromenade", "Ashen Promenade");
            biome.enemyPool = EnemyContentBuilder.BuildPool();
            var art = EnvironmentBuilder.LoadPromenade() ?? EnvironmentBuilder.BuildPromenade();
            biome.art = art;
            biome.loot = db;
            biome.boss = LoadOrCreateWarden();
            biome.minEnemies = 3;
            biome.maxEnemies = 6;
            biome.generator.minExitColumn = 6;
            biome.accent = new Color(1f, 0.6f, 0.3f);
            biome.ground = new Color(0.30f, 0.22f, 0.22f);
            biome.trim = new Color(0.55f, 0.38f, 0.3f);
            biome.platform = new Color(0.95f, 0.6f, 0.25f);
            biome.background = new Color(0.20f, 0.09f, 0.12f);
            EditorUtility.SetDirty(biome);
            return biome;
        }

        public static BiomeData[] BuildAll(LootDatabase db) => new[] { BuildCatacombs(db), BuildPromenade(db) };

        static BiomeData LoadOrCreateBiome(string file, string displayName)
        {
            string path = $"{BiomeFolder}/{file}.asset";
            var biome = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
            if (biome != null) return biome;
            Directory.CreateDirectory(BiomeFolder);
            biome = ScriptableObject.CreateInstance<BiomeData>();
            biome.displayName = displayName;
            AssetDatabase.CreateAsset(biome, path);
            return biome;
        }
    }
}
