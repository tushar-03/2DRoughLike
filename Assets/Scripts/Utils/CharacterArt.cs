using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// The art for one character: a sprite sheet with one row per clip. This is the swap-in point for final art: replace the
    /// sheet texture (same layout) or point the asset at a new sheet and nothing else needs to change. Frames are cut at runtime,
    /// so the sheet is a plain texture with no sprite-editor slicing.
    /// </summary>
    [CreateAssetMenu(menuName = "Roughlike/Art/Character Art", fileName = "CharacterArt")]
    public class CharacterArt : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            public string name;
            [Tooltip("Row in the sheet, counted from the top.")]
            public int row;
            public int frames = 1;
            public float fps = 10f;
            public bool loop = true;
        }

        public Texture2D sheet;
        public int frameWidth = 256;
        public int frameHeight = 256;
        [Tooltip("Pixels per world unit.")]
        public float pixelsPerUnit = 160f;
        [Tooltip("Normalised pivot inside a frame; (0.5, 0) puts the feet at the bottom centre.")]
        public Vector2 pivot = new Vector2(0.5f, 0.06f);
        public Clip[] clips = new Clip[0];

        readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        public bool Has(string clip) => Find(clip) != null;

        public Clip Find(string clip)
        {
            if (clips == null) return null;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].name == clip) return clips[i];
            return null;
        }

        /// <summary>The sprites of a clip (cached). Empty when the clip or the sheet is missing.</summary>
        public Sprite[] Frames(string clip)
        {
            if (_cache.TryGetValue(clip, out var cached) && cached != null && (cached.Length == 0 || cached[0] != null)) return cached;

            Clip c = Find(clip);
            if (c == null || sheet == null) return _cache[clip] = Array.Empty<Sprite>();

            var list = new Sprite[Mathf.Max(1, c.frames)];
            int top = sheet.height - (c.row + 1) * frameHeight;
            for (int i = 0; i < list.Length; i++)
            {
                var rect = new Rect(i * frameWidth, top, frameWidth, frameHeight);
                list[i] = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                list[i].name = $"{name}_{clip}_{i}";
            }
            return _cache[clip] = list;
        }

        /// <summary>First frame of a clip, for single-sprite props such as a shield.</summary>
        public Sprite Prop(string clip)
        {
            var f = Frames(clip);
            return f.Length > 0 ? f[0] : null;
        }

        void OnValidate() => _cache.Clear();
    }
}
