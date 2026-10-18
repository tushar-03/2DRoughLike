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
            [Tooltip("Optional, one per frame: where the hand and shield are, so gear can be drawn on top of the body.")]
            public Anchor[] anchors;
        }

        /// <summary>Per-frame attachment points for held gear. Pixels in the frame, origin at the bottom left.</summary>
        [Serializable]
        public struct Anchor
        {
            public bool valid;
            public Vector2 hand;
            [Tooltip("Direction the held weapon points, degrees from +x (forward).")]
            public float angle;
            [Tooltip("The off hand, for dual-wielded weapons.")]
            public Vector2 handB;
            public float angleB;
            public bool shieldUp;
            public Vector2 shield;
            [Tooltip("Where a lowered shield is carried (on the back).")]
            public Vector2 stow;
        }

        public Texture2D sheet;
        public int frameWidth = 256;
        public int frameHeight = 256;
        [Tooltip("Pixels per world unit.")]
        public float pixelsPerUnit = 160f;
        [Tooltip("Normalised pivot inside a frame; (0.5, 0) puts the feet at the bottom centre.")]
        public Vector2 pivot = new Vector2(0.5f, 0.06f);
        public Clip[] clips = new Clip[0];
        [Tooltip("More sheets with the same frame size and pivot. Clips not found here are looked up in them (keeps each texture under the 4096 pixel limit).")]
        public CharacterArt[] companions;

        readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        public bool Has(string clip) => Find(clip) != null;

        public Clip Find(string clip)
        {
            if (clips != null)
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i].name == clip) return clips[i];
            return Companion(clip)?.Find(clip);
        }

        /// <summary>The companion sheet that holds <paramref name="clip"/> (null when this sheet has it, or nobody does).</summary>
        CharacterArt Companion(string clip)
        {
            if (companions == null) return null;
            foreach (var c in companions)
            {
                if (c == null || c == this) continue;
                if (c.Find(clip) != null) return c;
            }
            return null;
        }

        bool Own(string clip)
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++) if (clips[i].name == clip) return true;
            return false;
        }

        /// <summary>The sprites of a clip (cached). Empty when the clip or the sheet is missing.</summary>
        public Sprite[] Frames(string clip)
        {
            if (_cache.TryGetValue(clip, out var cached) && cached != null && (cached.Length == 0 || cached[0] != null)) return cached;

            if (!Own(clip))
            {
                var other = Companion(clip);
                return _cache[clip] = other != null ? other.Frames(clip) : Array.Empty<Sprite>();
            }
            Clip c = Find(clip);
            if (c == null || sheet == null) return _cache[clip] = Array.Empty<Sprite>();

            var list = new Sprite[Mathf.Max(1, c.frames)];
            int top = sheet.height - (c.row + 1) * frameHeight;
            if (top < 0 || c.frames * frameWidth > sheet.width)
            {
                // the sheet is smaller than the clip list says (usually a texture shrunk by its import size limit)
                Debug.LogError($"CharacterArt '{name}': clip '{clip}' does not fit the {sheet.width}x{sheet.height} sheet. Raise the texture's max size or shrink the sheet.", this);
                return _cache[clip] = Array.Empty<Sprite>();
            }
            for (int i = 0; i < list.Length; i++)
            {
                var rect = new Rect(i * frameWidth, top, frameWidth, frameHeight);
                list[i] = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                list[i].name = $"{name}_{clip}_{i}";
            }
            return _cache[clip] = list;
        }

        public bool TryAnchor(string clip, int frame, out Anchor anchor)
        {
            anchor = default;
            Clip c = Find(clip);
            if (c == null || c.anchors == null || frame < 0 || frame >= c.anchors.Length) return false;
            anchor = c.anchors[frame];
            return anchor.valid;
        }

        /// <summary>Converts a point in frame pixels to a local position relative to the sprite's pivot, in world units.</summary>
        public Vector2 PixelToLocal(Vector2 px)
        {
            var pivotPx = new Vector2(pivot.x * frameWidth, pivot.y * frameHeight);
            return (px - pivotPx) / pixelsPerUnit;
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
