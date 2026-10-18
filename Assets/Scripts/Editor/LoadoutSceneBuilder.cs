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
    /// <summary>Builds the practice room scene shown once before a new run, where the starting loadout is chosen.</summary>
    public static class LoadoutSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Loadout.unity";

        [MenuItem("Roughlike/Build/Loadout Practice Room")]
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

            var director = new GameObject("Loadout").AddComponent<LoadoutDirector>();
            director.Bind(biome, controller);

            var ui = new GameObject("UI").AddComponent<UiBootstrap>();
            ui.Bind(controller, null, null, director);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuSceneBuilder.UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Loadout] scene built: " + ScenePath);
        }
    }
}
