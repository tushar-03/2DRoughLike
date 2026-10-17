using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roughlike.Combat;
using Roughlike.Enemies;
using Roughlike.Player;
using Roughlike.Rooms;

namespace Roughlike.Editor
{
    /// <summary>Builds the M3 scene: a generated run (biome asset, player, camera, run manager and HUD).</summary>
    public static class RunSceneBuilder
    {
        const string BiomePath = "Assets/Data/Biomes/EmberCatacombs.asset";
        const string PrefabPath = "Assets/Prefabs/Player/PlayerCombat.prefab";
        const string ScenePath = "Assets/Scenes/M3_Run.unity";

        [MenuItem("Roughlike/Build/M3 Generated Run")]
        public static void Build()
        {
            // The combat builder creates the weapon/enemy assets and the player prefab if they are missing.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedGrunt.asset") == null)
                CombatArenaBuilder.Build();

            var biome = LoadOrCreateBiome();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            playerGo.transform.position = new Vector3(5f, 3f, 0f);
            var controller = playerGo.GetComponent<PlayerController>();

            var vcam = CombatArenaBuilder.BuildCamera(controller);

            var feedback = new GameObject("Combat Feedback");
            feedback.AddComponent<CombatFeedbackHub>();

            var runGo = new GameObject("Run");
            var run = runGo.AddComponent<RunManager>();
            run.Bind(biome, controller, vcam);
            runGo.AddComponent<RunHud>().Bind(run, playerGo.GetComponent<Health>());

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Run] M3 scene built: " + ScenePath);
        }

        static BiomeData LoadOrCreateBiome()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BiomeData>(BiomePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(BiomePath));
            var biome = ScriptableObject.CreateInstance<BiomeData>();
            biome.displayName = "Ember Catacombs";
            biome.enemyPool = new[]
            {
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedGrunt.asset"),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedSpitter.asset")
            };
            AssetDatabase.CreateAsset(biome, BiomePath);
            return biome;
        }
    }
}
