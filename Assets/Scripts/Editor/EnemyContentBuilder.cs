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

        /// <summary>A promenade enemy: the same behaviour as <paramref name="source"/> in a new body, a little tougher.</summary>
        static EnemyDefinition Variant(string asset, EnemyDefinition source, string display, Color tint, float health = 1.2f, float damage = 1.1f)
        {
            return LoadOrCreate(asset, () =>
            {
                var e = Object.Instantiate(source);
                e.name = asset;
                e.displayName = display;
                e.tint = tint;
                e.art = null;
                e.maxHealth = Mathf.Round(source.maxHealth * health);
                e.damage = Mathf.Round(source.damage * damage * 10f) / 10f;
                return e;
            });
        }

        /// <summary>The Ashen Promenade's enemies: ember and ash counterparts of the eight archetypes.</summary>
        public static EnemyDefinition[] BuildPromenadePool()
        {
            var catacombs = BuildPool();
            EnemyDefinition C(string name) { foreach (var d in catacombs) if (d != null && d.name == name) return d; return null; }

            var wretch = LoadOrCreate("DrownedWretch", DefaultEnemies.DrownedWretch);
            var husk = Variant("AshHusk", wretch, "Ash Husk", new Color(0.55f, 0.5f, 0.48f), 1.2f, 1.1f);

            var priest = Variant("AshPriest", C("ReliquaryShade"), "Ash Priest", new Color(0.7f, 0.3f, 0.2f));
            if (priest.minion != husk) { priest.minion = husk; EditorUtility.SetDirty(priest); }

            var pool = new[]
            {
                Variant("AshFootman", C("DrownedGrunt"), "Ash Footman", new Color(0.6f, 0.3f, 0.25f)),
                Variant("EmberImp", C("DrownedSpitter"), "Ember Imp", new Color(0.9f, 0.4f, 0.2f)),
                Variant("EmberWisp", C("BellWisp"), "Ember Wisp", new Color(1f, 0.55f, 0.2f)),
                Variant("RustSentinel", C("ShellGuard"), "Rust Sentinel", new Color(0.6f, 0.42f, 0.32f)),
                Variant("CinderBoar", C("Tideboar"), "Cinder Boar", new Color(0.4f, 0.3f, 0.3f)),
                Variant("AshBomber", C("Bloat"), "Ash Bomber", new Color(0.5f, 0.42f, 0.4f)),
                Variant("BoneArcher", C("EelArcher"), "Bone Archer", new Color(0.9f, 0.86f, 0.75f)),
                priest
            };

            CharacterSheetBuilder.EnsureAll();
            foreach (var def in pool) AssignArt(def);
            AssignArt(husk);
            AssetDatabase.SaveAssets();
            return pool;
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
