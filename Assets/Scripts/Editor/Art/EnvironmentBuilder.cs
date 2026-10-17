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

        public static EnvironmentArt Load() => AssetDatabase.LoadAssetAtPath<EnvironmentArt>(AssetPath);

        [MenuItem("Roughlike/Art/Generate Environment")]
        public static EnvironmentArt Build()
        {
            Directory.CreateDirectory(Folder);
            CharacterSheetBuilder.EnsureAll();

            Sprite wall = Write("Wall", EnvironmentPainter.Wall(), 256, 256, 128f, false);
            Write("WallDark", EnvironmentPainter.WallDark(), 256, 256, 128f, false);
            Sprite trim = Write("FloorTrim", EnvironmentPainter.FloorTrim(), 256, 64, 128f, false);
            Sprite platform = Write("Platform", EnvironmentPainter.Platform(), 256, 48, 128f, false);
            Sprite far = Write("BackdropFar", EnvironmentPainter.BackdropFar(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite mid = Write("BackdropMid", EnvironmentPainter.BackdropMid(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);
            Sprite near = Write("BackdropNear", EnvironmentPainter.BackdropNear(), EnvironmentPainter.BackW, EnvironmentPainter.BackH, 32f, true);

            var art = Load();
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<EnvironmentArt>();
            art.wall = wall;
            art.floorTrim = trim;
            art.platform = platform;
            art.props = CharacterSheetBuilder.Load("Props");
            art.worldProps = CharacterSheetBuilder.Load("WorldProps");
            art.ambientIntensity = 0.7f;
            art.backdrop = new[]
            {
                new EnvironmentArt.BackdropLayer { name = "Far haze and windows", sprite = far, follow = 0.96f, followY = 0.92f, yOffset = 1.5f, sortingOrder = -60 },
                new EnvironmentArt.BackdropLayer { name = "Colonnade", sprite = mid, follow = 0.84f, followY = 0.86f, yOffset = 0.5f, sortingOrder = -50 },
                new EnvironmentArt.BackdropLayer { name = "Hanging silhouettes", sprite = near, follow = 0.64f, followY = 0.74f, yOffset = 0f, sortingOrder = -40 }
            };
            if (created) AssetDatabase.CreateAsset(art, AssetPath);
            else EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
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
