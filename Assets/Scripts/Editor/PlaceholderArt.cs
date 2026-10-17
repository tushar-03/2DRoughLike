using System.IO;
using UnityEditor;
using UnityEngine;

namespace Roughlike.Editor
{
    /// <summary>
    /// Generates placeholder sprites procedurally (no external art). Registered in ART_TODO.md.
    /// Bilinear filtered, no mipmaps, high-res: the same rules final art will follow.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Folder = "Assets/Art/Placeholder";
        public const string SquarePath = Folder + "/Square.png";
        public const string PlayerPath = Folder + "/PlayerBody.png";
        public const string GruntPath = Folder + "/EnemyGrunt.png";
        public const string ShooterPath = Folder + "/EnemyShooter.png";
        public const string DummyPath = Folder + "/Dummy.png";

        [MenuItem("Roughlike/Setup/Generate Placeholder Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            WriteSquare();

            // Player: warm cream with a gold rim light.
            WriteCapsule(PlayerPath, 128, 256, 52f, 7f,
                new Color(0.93f, 0.90f, 0.82f), new Color(0.66f, 0.62f, 0.62f), new Color(1f, 0.85f, 0.45f), 20f, 78f);
            // Enemies are drawn light so the definition's tint colours them.
            WriteCapsule(GruntPath, 144, 240, 62f, 7f,
                new Color(0.95f, 0.95f, 0.95f), new Color(0.62f, 0.62f, 0.66f), new Color(1f, 1f, 1f), 24f, 70f);
            WriteCapsule(ShooterPath, 112, 240, 46f, 7f,
                new Color(0.95f, 0.95f, 0.95f), new Color(0.6f, 0.62f, 0.66f), new Color(1f, 1f, 1f), 16f, 60f);
            WriteCapsule(DummyPath, 144, 272, 58f, 7f,
                new Color(0.95f, 0.9f, 0.8f), new Color(0.7f, 0.62f, 0.5f), new Color(1f, 1f, 1f), 0f, -999f);

            AssetDatabase.Refresh();
            ImportSprite(SquarePath, 64f, new Vector2(0.5f, 0.5f));
            ImportSprite(PlayerPath, 160f, new Vector2(0.5f, 0f));
            ImportSprite(GruntPath, 160f, new Vector2(0.5f, 0f));
            ImportSprite(ShooterPath, 160f, new Vector2(0.5f, 0f));
            ImportSprite(DummyPath, 160f, new Vector2(0.5f, 0f));
        }

        public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        public static Sprite LoadSquare() => Load(SquarePath);
        public static Sprite LoadPlayer() => Load(PlayerPath);

        static void WriteSquare()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Capsule body with thick dark outline, two-step cel shade, rim light and an eye on the facing (right) side.</summary>
        static void WriteCapsule(string path, int w, int h, float radius, float outline,
            Color baseCol, Color shadeCol, Color rimCol, float eyeX, float eyeFromTop)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var outlineCol = new Color(0.07f, 0.05f, 0.10f, 1f);
            Vector2 cBottom = new Vector2(w * 0.5f, radius + outline);
            Vector2 cTop = new Vector2(w * 0.5f, h - radius - outline);
            Vector2 eye = new Vector2(w * 0.5f + eyeX, h - eyeFromTop);
            bool hasEye = eyeFromTop > -900f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float cy = Mathf.Clamp(p.y, cBottom.y, cTop.y);
                float d = Vector2.Distance(p, new Vector2(w * 0.5f, cy));
                float outer = radius + outline;

                Color c = Color.clear;
                if (d <= outer)
                {
                    if (d > radius) c = outlineCol;
                    else
                    {
                        Vector2 n = new Vector2((p.x - w * 0.5f) / radius, (p.y - cy) / radius);
                        float lit = Vector2.Dot(n, new Vector2(-0.6f, 0.8f));
                        c = lit > -0.15f ? baseCol : shadeCol;
                        if (d > radius - 7f && n.x > 0.35f) c = rimCol;
                        if (hasEye && Vector2.Distance(p, eye) < 11f) c = outlineCol;
                        if (hasEye && Vector2.Distance(p, eye + new Vector2(3f, 3f)) < 3.5f) c = Color.white;
                    }
                    c.a *= Mathf.Clamp01(outer - d);
                }
                px[y * w + x] = c;
            }

            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void ImportSprite(string path, float ppu, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
