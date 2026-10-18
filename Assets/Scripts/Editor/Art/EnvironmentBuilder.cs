using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Rooms;
using Roughlike.Utils;

namespace Roughlike.Editor.Art
{
    /// <summary>Paints the Ember Catacombs tiles and backdrop into PNGs and wires them into an <see cref="EnvironmentArt"/> asset.</summary>
    public static class EnvironmentBuilder
    {
        public const string Folder = "Assets/Art/Environment";
        public const string AssetPath = Folder + "/EmberCatacombsArt.asset";
        public const string BiomePath = "Assets/Data/Biomes/EmberCatacombs.asset";
        public const string PromenadeAssetPath = Folder + "/AshenPromenadeArt.asset";

        public static EnvironmentArt LoadPromenade() => AssetDatabase.LoadAssetAtPath<EnvironmentArt>(PromenadeAssetPath);

        public static EnvironmentArt Load() => AssetDatabase.LoadAssetAtPath<EnvironmentArt>(AssetPath);

        [MenuItem("Roughlike/Art/Generate Environment")]
        public static EnvironmentArt Build()
        {
            Directory.CreateDirectory(Folder);
            CharacterSheetBuilder.EnsureAll();
            EnvironmentPainter.Current = EnvironmentPainter.Palette.Catacombs;

            Sprite wall = Write("Wall", EnvironmentPainter.Wall(), 256, 256, 128f, false);
            Sprite rock = Write("WallDark", EnvironmentPainter.WallDark(), 256, 256, 128f, false);
            Sprite trim = Write("FloorTrim", EnvironmentPainter.FloorTrim(), 256, 64, 128f, false);
            Sprite platform = Write("Platform", EnvironmentPainter.Platform(), 256, 48, 128f, false);
            Sprite far = Write("BackdropFar", EnvironmentPainter.BackdropFar(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite mid = Write("BackdropMid", EnvironmentPainter.BackdropMid(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite near = Write("BackdropNear", EnvironmentPainter.BackdropNear(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite lanterns = Write("BackdropLanterns", EnvironmentPainter.BackdropLanterns(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite fgBottom = Write("ForegroundBottom", EnvironmentPainter.ForegroundBottomCatacombs(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite fgTop = Write("ForegroundTop", EnvironmentPainter.ForegroundTopCatacombs(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite arches = Write("BackdropArches", EnvironmentPainter.BackdropArches(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite fogFar = Write("FogFar", EnvironmentPainter.FogBands(new Color(0.45f, 0.55f, 0.75f), 0.45f, 11), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite fogNear = Write("FogNear", EnvironmentPainter.FogBands(new Color(0.3f, 0.36f, 0.5f), 0.5f, 23), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite pillars = Write("ForegroundPillars", EnvironmentPainter.ForegroundPillarsCatacombs(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);

            var art = Load();
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<EnvironmentArt>();
            art.wall = wall;
            art.rock = rock;
            art.floorTrim = trim;
            art.platform = platform;
            art.props = CharacterSheetBuilder.Load("Props");
            art.worldProps = CharacterSheetBuilder.Load("WorldProps");
            art.ambientIntensity = 0.7f;
            art.backdrop = new[]
            {
                new EnvironmentArt.BackdropLayer { name = "Far haze and windows", sprite = far, follow = 0.96f, followY = 0.92f, yOffset = 1.5f, sortingOrder = -60 },
                new EnvironmentArt.BackdropLayer { name = "Colonnade", sprite = mid, follow = 0.84f, followY = 0.86f, yOffset = 0.5f, sortingOrder = -50 },
                new EnvironmentArt.BackdropLayer { name = "Hanging silhouettes", sprite = near, follow = 0.64f, followY = 0.74f, yOffset = 0f, sortingOrder = -40 },
                new EnvironmentArt.BackdropLayer { name = "Lanterns", sprite = lanterns, follow = 0.55f, followY = 0.7f, yOffset = 0f, sortingOrder = -35 },
                new EnvironmentArt.BackdropLayer { name = "Distant arches", sprite = arches, follow = 0.93f, followY = 0.9f, yOffset = 0.6f, sortingOrder = -57 },
                new EnvironmentArt.BackdropLayer { name = "Far mist", sprite = fogFar, follow = 0.8f, followY = 0.85f, yOffset = 0f, sortingOrder = -55 },
                new EnvironmentArt.BackdropLayer { name = "Near mist", sprite = fogNear, follow = 0.5f, followY = 0.7f, yOffset = -0.5f, sortingOrder = -38 },
                new EnvironmentArt.BackdropLayer { name = "Foreground columns", sprite = pillars, follow = -0.85f, followY = 1f, yOffset = 0f, sortingOrder = 58 },
                new EnvironmentArt.BackdropLayer { name = "Foreground rubble", sprite = fgBottom, follow = -0.35f, followY = 1f, yOffset = 5.3f, sortingOrder = 60 },
                new EnvironmentArt.BackdropLayer { name = "Foreground roots and chains", sprite = fgTop, follow = -0.5f, followY = 1f, yOffset = -5.1f, sortingOrder = 61 }
            };
            art.moteColour = new Color(0.65f, 0.9f, 1f, 0.55f);
            art.moteCount = 50;
            if (created) AssetDatabase.CreateAsset(art, AssetPath);
            else EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        /// <summary>The second biome: rust-brown ruins at sunset, orange light, a warm haze.</summary>
        [MenuItem("Roughlike/Art/Generate Second Biome")]
        public static EnvironmentArt BuildPromenade()
        {
            Directory.CreateDirectory(Folder);
            CharacterSheetBuilder.EnsureAll();
            EnvironmentPainter.Current = EnvironmentPainter.Palette.Promenade;
            try
            {
                Sprite wall = Write("PromenadeWall", EnvironmentPainter.Wall(), 256, 256, 128f, false);
                Sprite rock = Write("PromenadeRock", EnvironmentPainter.WallDark(), 256, 256, 128f, false);
                Sprite trim = Write("PromenadeFloorTrim", EnvironmentPainter.FloorTrim(), 256, 64, 128f, false);
                Sprite platform = Write("PromenadePlatform", EnvironmentPainter.Platform(), 256, 48, 128f, false);
                Sprite far = Write("PromenadeBackdropFar", EnvironmentPainter.BackdropFarPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite mid = Write("PromenadeBackdropMid", EnvironmentPainter.BackdropMidPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite near = Write("PromenadeBackdropNear", EnvironmentPainter.BackdropNearPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite islands = Write("PromenadeIslands", EnvironmentPainter.BackdropIslands(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite fgBottom = Write("PromenadeForegroundBottom", EnvironmentPainter.ForegroundBottomPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite fgTop = Write("PromenadeForegroundTop", EnvironmentPainter.ForegroundTopPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite columns = Write("PromenadeColumns", EnvironmentPainter.BackdropBrokenColumns(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite fogFar = Write("PromenadeFogFar", EnvironmentPainter.FogBands(new Color(1f, 0.55f, 0.35f), 0.4f, 31), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite fogNear = Write("PromenadeFogNear", EnvironmentPainter.FogBands(new Color(0.75f, 0.32f, 0.22f), 0.45f, 43), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
                Sprite pillars = Write("PromenadeForegroundPillars", EnvironmentPainter.ForegroundPillarsPromenade(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);

                var art = LoadPromenade();
                bool created = art == null;
                if (created) art = ScriptableObject.CreateInstance<EnvironmentArt>();
                art.wall = wall;
                art.rock = rock;
                art.floorTrim = trim;
                art.platform = platform;
                art.props = CharacterSheetBuilder.Load("Props");
                art.worldProps = CharacterSheetBuilder.Load("WorldProps");
                art.ambient = new Color(1f, 0.78f, 0.62f);
                art.ambientIntensity = 0.6f;
                art.playerLight = new Color(1f, 0.85f, 0.6f);
                art.torchLight = new Color(1f, 0.62f, 0.26f);
                art.bloom = 0.85f;
                art.vignette = 0.34f;
                art.grain = 0.1f;
                art.colourFilter = new Color(1f, 0.94f, 0.88f);
                art.saturation = 22f;
                art.contrast = 12f;
                art.backdrop = new[]
                {
                    new EnvironmentArt.BackdropLayer { name = "Sunset and distant towers", sprite = far, follow = 0.97f, followY = 0.93f, yOffset = 1.2f, sortingOrder = -60, tint = new Color(0.72f, 0.66f, 0.7f) },
                    new EnvironmentArt.BackdropLayer { name = "Ruined gates", sprite = mid, follow = 0.85f, followY = 0.86f, yOffset = 0.4f, sortingOrder = -50 },
                    new EnvironmentArt.BackdropLayer { name = "Beams and chains", sprite = near, follow = 0.64f, followY = 0.74f, yOffset = 0f, sortingOrder = -40 },
                    new EnvironmentArt.BackdropLayer { name = "Floating ruins", sprite = islands, follow = 0.91f, followY = 0.88f, yOffset = 0.5f, sortingOrder = -58, tint = new Color(0.85f, 0.7f, 0.72f) },
                    new EnvironmentArt.BackdropLayer { name = "Broken columns", sprite = columns, follow = 0.94f, followY = 0.9f, yOffset = 0.4f, sortingOrder = -57, tint = new Color(0.9f, 0.8f, 0.8f) },
                    new EnvironmentArt.BackdropLayer { name = "Far haze", sprite = fogFar, follow = 0.8f, followY = 0.85f, yOffset = 0f, sortingOrder = -55 },
                    new EnvironmentArt.BackdropLayer { name = "Near haze", sprite = fogNear, follow = 0.5f, followY = 0.7f, yOffset = -0.5f, sortingOrder = -38 },
                    new EnvironmentArt.BackdropLayer { name = "Foreground columns", sprite = pillars, follow = -0.85f, followY = 1f, yOffset = 0f, sortingOrder = 58 },
                    new EnvironmentArt.BackdropLayer { name = "Foreground weeds and posts", sprite = fgBottom, follow = -0.35f, followY = 1f, yOffset = 5.3f, sortingOrder = 60 },
                    new EnvironmentArt.BackdropLayer { name = "Foreground banners", sprite = fgTop, follow = -0.5f, followY = 1f, yOffset = -5.1f, sortingOrder = 61 }
                };
                art.moteColour = new Color(1f, 0.6f, 0.25f, 0.7f);
                art.moteCount = 56;
                if (created) AssetDatabase.CreateAsset(art, PromenadeAssetPath);
                else EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
                return art;
            }
            finally { EnvironmentPainter.Current = EnvironmentPainter.Palette.Catacombs; }
        }

        /// <summary>Makes sure a biome asset uses the environment art, building the art first when needed.</summary>
        public static void Apply(BiomeData biome)
        {
            var art = Load() ?? Build();
            if (biome.art == art) return;
            biome.art = art;
            EditorUtility.SetDirty(biome);
        }

        static Sprite Write(string name, Color32[] pixels, int w, int h, float ppu, bool backdrop)
        {
            string path = $"{Folder}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = backdrop ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.textureCompression = backdrop ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
