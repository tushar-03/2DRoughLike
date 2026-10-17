using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Roughlike.UI;

namespace Roughlike.Editor
{
    /// <summary>Builds the main menu scene and keeps the Build Settings scene list in order (menu first).</summary>
    public static class MenuSceneBuilder
    {
        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Roughlike/Build/Main Menu")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // a camera is needed for the overlay canvas to render onto something
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.04f, 0.06f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var menu = new GameObject("Main Menu").AddComponent<MainMenuController>();
            menu.Art = Roughlike.Editor.Art.EnvironmentBuilder.Load() ?? Roughlike.Editor.Art.EnvironmentBuilder.Build();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Menu] Main menu scene built: " + MenuScenePath);
        }

        /// <summary>The menu is scene 0, the playable run is scene 1, the development test scenes follow.</summary>
        [MenuItem("Roughlike/Build/Update Build Settings")]
        public static void UpdateBuildSettings()
        {
            string[] order =
            {
                MenuScenePath,
                "Assets/Scenes/M4_LootRun.unity",
                TutorialSceneBuilder.ScenePath,
                "Assets/Scenes/M3_Run.unity",
                "Assets/Scenes/M2_CombatArena.unity",
                "Assets/Scenes/M1_Graybox.unity"
            };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in order)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Open the menu scene and press Play, the way a player would start the game.</summary>
        [MenuItem("Roughlike/Play From Main Menu")]
        public static void PlayFromMenu()
        {
            if (!File.Exists(MenuScenePath)) Build();
            EditorSceneManager.OpenScene(MenuScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
