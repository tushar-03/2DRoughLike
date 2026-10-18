using System.IO;
using UnityEditor;
using UnityEngine;
using Roughlike.Utils;

namespace Roughlike.Editor.Art
{
    /// <summary>Paints every character in <see cref="CharacterLibrary"/> into a sheet PNG and a <see cref="CharacterArt"/> asset next to it.</summary>
    public static class CharacterSheetBuilder
    {
        public const string Folder = "Assets/Art/Characters";

        public static string SheetPath(string name) => $"{Folder}/{name}.png";
        public static string AssetPath(string name) => $"{Folder}/{name}.asset";
        public static CharacterArt Load(string name) => AssetDatabase.LoadAssetAtPath<CharacterArt>(AssetPath(name));

        [MenuItem("Roughlike/Art/Generate Characters")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            foreach (var plan in CharacterLibrary.All()) Build(plan);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LinkCompanions();
            Debug.Log("[Art] characters generated in " + Folder);
        }

        /// <summary>Paints whichever characters have no asset yet, so scene builders can rely on the art existing.</summary>
        public static void EnsureAll()
        {
            Directory.CreateDirectory(Folder);
            bool any = false;
            foreach (var plan in CharacterLibrary.All())
            {
                if (Load(plan.Name) != null) continue;
                Build(plan);
                any = true;
            }
            if (any)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            LinkCompanions();
        }

        /// <summary>Paints a single character by name (fast iteration) and writes a contact sheet PNG for review when a path is given.</summary>
        public static void BuildOne(string name, string previewPath = null)
        {
            Directory.CreateDirectory(Folder);
            foreach (var plan in CharacterLibrary.All())
            {
                if (plan.Name != name) continue;
                Build(plan, previewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                LinkCompanions();
                return;
            }
            Debug.LogError("[Art] no character named " + name);
        }

        /// <summary>The sheet composited over a dark backdrop and shrunk by <paramref name="shrink"/>, for reviewing in an image viewer.</summary>
        static byte[] Preview(Texture2D sheet, int shrink)
        {
            int w = sheet.width / shrink, h = sheet.height / shrink;
            var src = sheet.GetPixels32();
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0;
                for (int j = 0; j < shrink; j++)
                for (int i = 0; i < shrink; i++)
                {
                    Color32 c = src[(y * shrink + j) * sheet.width + x * shrink + i];
                    float a = c.a / 255f;
                    float cell = ((x * shrink + i) / 160 + (y * shrink + j) / 160) % 2 == 0 ? 62f : 52f;
                    r += c.r * a + cell * (1f - a); g += c.g * a + (cell - 6f) * (1f - a); b += c.b * a + (cell + 8f) * (1f - a);
                }
                float k = 1f / (shrink * shrink);
                dst[y * w + x] = new Color32((byte)(r * k), (byte)(g * k), (byte)(b * k), 255);
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(dst);
            byte[] png = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            return png;
        }

        /// <summary>Points each sheet at the companion sheets its plan lists (done once every sheet exists).</summary>
        public static void LinkCompanions()
        {
            foreach (var plan in CharacterLibrary.All())
            {
                if (plan.Companions == null) continue;
                var art = Load(plan.Name);
                if (art == null) continue;
                var list = new System.Collections.Generic.List<CharacterArt>();
                foreach (var n in plan.Companions) { var c = Load(n); if (c != null) list.Add(c); }
                art.companions = list.ToArray();
                EditorUtility.SetDirty(art);
            }
            AssetDatabase.SaveAssets();
        }

        static void Build(CharacterPlan plan, string previewPath = null)
        {
            int cols = 1;
            foreach (var c in plan.Clips) cols = Mathf.Max(cols, c.Frames);
            int rows = plan.Clips.Count;
            int fw = plan.FrameW, fh = plan.FrameH;
            var tex = new Texture2D(cols * fw, rows * fh, TextureFormat.RGBA32, false);
            var clear = new Color32[tex.width * tex.height];
            tex.SetPixels32(clear);

            var anchors = new CharacterArt.Anchor[rows][];
            for (int r = 0; r < rows; r++)
            {
                var clip = plan.Clips[r];
                anchors[r] = new CharacterArt.Anchor[clip.Frames];
                for (int f = 0; f < clip.Frames; f++)
                {
                    HumanoidArt.HasAnchor = false;
                    Color32[] px = clip.Paint(f, clip.Frames);
                    tex.SetPixels32(f * fw, (rows - 1 - r) * fh, fw, fh, px);
                    if (plan.Anchors && HumanoidArt.HasAnchor) anchors[r][f] = HumanoidArt.LastAnchor;
                }
            }
            tex.Apply();

            string sheet = SheetPath(plan.Name);
            File.WriteAllBytes(sheet, tex.EncodeToPNG());
            if (!string.IsNullOrEmpty(previewPath)) File.WriteAllBytes(previewPath, Preview(tex, 2));
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(sheet, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(AssetPath(plan.Name));
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<CharacterArt>();
            art.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet);
            art.frameWidth = fw;
            art.frameHeight = fh;
            art.pixelsPerUnit = 160f;
            art.pivot = plan.Pivot;
            art.clips = new CharacterArt.Clip[plan.Clips.Count];
            for (int r = 0; r < plan.Clips.Count; r++)
            {
                var c = plan.Clips[r];
                art.clips[r] = new CharacterArt.Clip { name = c.Name, row = r, frames = c.Frames, fps = c.Fps, loop = c.Loop, anchors = plan.Anchors ? anchors[r] : null };
            }
            if (created) AssetDatabase.CreateAsset(art, AssetPath(plan.Name));
            else EditorUtility.SetDirty(art);
        }
    }
}
