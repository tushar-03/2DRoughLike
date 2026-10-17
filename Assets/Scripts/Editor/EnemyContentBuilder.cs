using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Editor.Art;
using Roughlike.Enemies;

namespace Roughlike.Editor
{
    /// <summary>Creates the enemy definition assets (the two originals come from the arena builder) and returns the biome pool.</summary>
    public static class EnemyContentBuilder
    {
        const string Folder = "Assets/Data/Enemies";

        static T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Folder}/{name}.asset");

        static EnemyDefinition LoadOrCreate(string asset, System.Func<EnemyDefinition> make)
        {
            Directory.CreateDirectory(Folder);
            var existing = Load<EnemyDefinition>(asset);
            if (existing != null) return existing;
            var e = make();
            AssetDatabase.CreateAsset(e, $"{Folder}/{asset}.asset");
            return e;
        }

        static void AssignArt(EnemyDefinition def)
        {
            var art = CharacterSheetBuilder.Load(def.name);
            if (art == null || def.art == art) return;
            def.art = art;
            EditorUtility.SetDirty(def);
        }

        /// <summary>All enemies a biome can spawn, in a fixed order.</summary>
        public static EnemyDefinition[] BuildPool()
        {
            var grunt = Load<EnemyDefinition>("DrownedGrunt");
            var spitter = Load<EnemyDefinition>("DrownedSpitter");
            var wretch = LoadOrCreate("DrownedWretch", DefaultEnemies.DrownedWretch);

            var pool = new[]
            {
                grunt,
                spitter,
                LoadOrCreate("BellWisp", DefaultEnemies.BellWisp),
                LoadOrCreate("ShellGuard", DefaultEnemies.ShellGuard),
                LoadOrCreate("Tideboar", DefaultEnemies.Tideboar),
                LoadOrCreate("Bloat", DefaultEnemies.Bloat),
                LoadOrCreate("EelArcher", DefaultEnemies.EelArcher),
                LoadOrCreate("ReliquaryShade", () => DefaultEnemies.ReliquaryShade(wretch))
            };

            // an existing shade asset made before the wretch existed still needs its minion
            var shade = pool[pool.Length - 1];
            if (shade.minion == null) { shade.minion = wretch; EditorUtility.SetDirty(shade); }

            // painted art: every enemy asset gets the sheet that shares its name
            CharacterSheetBuilder.EnsureAll();
            foreach (var def in pool) AssignArt(def);
            AssignArt(wretch);

            AssetDatabase.SaveAssets();
            return pool;
        }
    }
}
