using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;

namespace Roughlike.Editor
{
    /// <summary>Builds the tutorial scene: the guided hall, with the player, camera, HUD and menus of a real run.</summary>
    public static class TutorialSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tutorial.unity";

        [MenuItem("Roughlike/Build/Tutorial")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedGrunt.asset") == null ||
                AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Data/Player/PlayerMovement.asset") == null)
                CombatArenaBuilder.Build();

            Roughlike.Editor.Art.CharacterSheetBuilder.EnsureAll();
            LootDatabase db = ItemContentBuilder.Build();
            BiomeData biome = LootRunSceneBuilder.LoadOrCreateBiome(db);
            GameObject prefab = LootRunSceneBuilder.BuildPlayerPrefab(db);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            playerGo.transform.position = new Vector3(4f, 3f, 0f);
            var controller = playerGo.GetComponent<PlayerController>();

            CombatArenaBuilder.BuildCamera(controller);
            new GameObject("Combat Feedback").AddComponent<CombatFeedbackHub>();
            new GameObject("Loot Director").AddComponent<LootDirector>();

            var tutorial = new GameObject("Tutorial").AddComponent<TutorialDirector>();
            tutorial.Bind(biome, controller, AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/DrownedGrunt.asset"));

            var ui = new GameObject("UI").AddComponent<UiBootstrap>();
            ui.Bind(controller, null, tutorial);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuSceneBuilder.UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Tutorial] scene built: " + ScenePath);
        }
    }
}
