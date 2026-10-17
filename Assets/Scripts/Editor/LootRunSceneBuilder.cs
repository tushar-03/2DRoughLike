using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roughlike.Combat;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;

namespace Roughlike.Editor
{
    /// <summary>Builds the M4 scene: a generated run with loot, shops, mutation altars, skills and the full uGUI HUD.</summary>
    public static class LootRunSceneBuilder
    {
        const string PrefabPath = "Assets/Prefabs/Player/PlayerLoot.prefab";
        const string ScenePath = "Assets/Scenes/M4_LootRun.unity";

        [MenuItem("Roughlike/Build/M4 Loot Run")]
        public static void Build()
        {
            // weapon/enemy assets and the movement config come from the earlier builders when missing
            if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedGrunt.asset") == null ||
                AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Player/PlayerMovement.asset") == null)
                CombatArenaBuilder.Build();

            Roughlike.Editor.Art.CharacterSheetBuilder.EnsureAll();
            LootDatabase db = ItemContentBuilder.Build();
            BiomeData biome = LoadOrCreateBiome(db);
            GameObject prefab = BuildPlayerPrefab(db);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            playerGo.transform.position = new Vector3(5f, 3f, 0f);
            var controller = playerGo.GetComponent<PlayerController>();

            var vcam = CombatArenaBuilder.BuildCamera(controller);

            new GameObject("Combat Feedback").AddComponent<CombatFeedbackHub>();
            var director = new GameObject("Loot Director").AddComponent<LootDirector>();

            var runGo = new GameObject("Run");
            var run = runGo.AddComponent<RunManager>();
            run.Bind(biome, controller, vcam, director);
            runGo.AddComponent<RunHud>().Bind(run, playerGo.GetComponent<Health>());

            var ui = new GameObject("UI").AddComponent<UiBootstrap>();
            ui.Bind(controller, run);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Loot] M4 scene built: " + ScenePath);
        }

        internal static BiomeData LoadOrCreateBiome(LootDatabase db)
        {
            const string path = "Assets/Data/Biomes/EmberCatacombs.asset";
            var biome = AssetDatabase.LoadAssetAtPath<BiomeData>(path);
            if (biome == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                biome = ScriptableObject.CreateInstance<BiomeData>();
                biome.displayName = "Ember Catacombs";
                AssetDatabase.CreateAsset(biome, path);
            }
            biome.enemyPool = EnemyContentBuilder.BuildPool();
            Roughlike.Editor.Art.EnvironmentBuilder.Apply(biome);
            biome.loot = db;
            EditorUtility.SetDirty(biome);
            return biome;
        }

        internal static GameObject BuildPlayerPrefab(LootDatabase db)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Player/PlayerMovement.asset");
            var art = Roughlike.Editor.Art.CharacterSheetBuilder.Load("BellRinger");
            var controller = PlayerFactory.Build(config, PlayerFactory.InputKind.Device, PlaceholderArt.LoadPlayer(), null, "Player", art);
            PlayerCombatSetup.Attach(controller, db.startingWeapon1, db.startingWeapon2);
            PlayerItemsSetup.Attach(controller);
            GameObject go = controller.gameObject;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
