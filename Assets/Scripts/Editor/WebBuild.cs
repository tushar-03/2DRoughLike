using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Roughlike.Editor
{
    /// <summary>
    /// Builds the game for the browser (WebGL) into <c>Builds/WebGL</c>, ready to drop on a static host such as Vercel:
    /// a custom page template (touch friendly, rotate prompt, fullscreen button), Brotli compression, hashed file names for
    /// long caching, and a <c>vercel.json</c> that sets the headers Brotli needs. Run from the menu or with
    /// <c>-executeMethod Roughlike.Editor.WebBuild.BuildFromCommandLine -batchmode -quit</c>.
    /// </summary>
    public static class WebBuild
    {
        public const string OutputFolder = "Builds/WebGL";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/M4_LootRun.unity",
            "Assets/Scenes/Tutorial.unity",
            "Assets/Scenes/Loadout.unity"
        };

        /// <summary>The same build without the confirmation dialog, for scripted builds.</summary>
        [MenuItem("Roughlike/Build/WebGL (no prompt)")]
        public static void BuildNoPrompt() => Build(switchBack: true);

        [MenuItem("Roughlike/Build/WebGL (for hosting)")]
        public static void BuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Build for the web", "This switches the editor to WebGL, builds into Builds/WebGL, then switches back. It can take several minutes.", "Build", "Cancel")) return;
            Build(switchBack: true);
        }

        public static void BuildFromCommandLine()
        {
            bool ok = Build(switchBack: false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Build(bool switchBack)
        {
            EnsureScenes();
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);

            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

                ConfigurePlayerSettings();

                string output = Path.GetFullPath(OutputFolder);
                // empty the folder rather than deleting it: a terminal sitting in it (to run the hosting CLI) would block the delete
                if (Directory.Exists(output))
                {
                    foreach (var file in Directory.GetFiles(output)) File.Delete(file);
                    foreach (var dir in Directory.GetDirectories(output)) Directory.Delete(dir, true);
                }
                Directory.CreateDirectory(output);

                var options = new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                bool ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log($"[WebBuild] {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB in {report.summary.totalTime.TotalSeconds:0}s -> {output}");
                if (ok) WriteHostingFiles(output);
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogError("[WebBuild] failed: " + e);
                return false;
            }
            finally
            {
                if (switchBack && previousTarget != BuildTarget.WebGL)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
            }
        }

        static void EnsureScenes()
        {
            if (!File.Exists(Scenes[0])) MenuSceneBuilder.Build();
            if (!File.Exists(Scenes[1])) LootRunSceneBuilder.Build();
            if (!File.Exists(Scenes[2])) TutorialSceneBuilder.Build();
            if (!File.Exists(Scenes[3])) LoadoutSceneBuilder.Build();
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Two Souls";
            PlayerSettings.WebGL.template = "PROJECT:Roughlike";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;                    // no pale splash flash before the game
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.03f, 0.035f, 0.06f);

            // FSR upscaling is not available in browsers; plain linear keeps the post-processing passes running
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.upscalingFilter = UnityEngine.Rendering.Universal.UpscalingFilterSelection.Linear;
                EditorUtility.SetDirty(urp);
            }
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
        }

        /// <summary>
        /// vercel.json: long caching for the hashed build files, and the Content-Encoding / Content-Type headers Brotli files need.
        /// The build can also be hosted anywhere that serves static files with those headers.
        /// </summary>
        static void WriteHostingFiles(string output)
        {
            const string vercel = @"{
  ""cleanUrls"": true,
  ""headers"": [
    { ""source"": ""/Build/(.*)"", ""headers"": [ { ""key"": ""Cache-Control"", ""value"": ""public, max-age=31536000, immutable"" } ] },
    { ""source"": ""/Build/(.*)\\.wasm\\.br"", ""headers"": [ { ""key"": ""Content-Encoding"", ""value"": ""br"" }, { ""key"": ""Content-Type"", ""value"": ""application/wasm"" } ] },
    { ""source"": ""/Build/(.*)\\.js\\.br"", ""headers"": [ { ""key"": ""Content-Encoding"", ""value"": ""br"" }, { ""key"": ""Content-Type"", ""value"": ""application/javascript"" } ] },
    { ""source"": ""/Build/(.*)\\.data\\.br"", ""headers"": [ { ""key"": ""Content-Encoding"", ""value"": ""br"" }, { ""key"": ""Content-Type"", ""value"": ""application/octet-stream"" } ] },
    { ""source"": ""/(.*)"", ""headers"": [ { ""key"": ""X-Content-Type-Options"", ""value"": ""nosniff"" } ] }
  ]
}
";
            File.WriteAllText(Path.Combine(output, "vercel.json"), vercel);
        }
    }
}
