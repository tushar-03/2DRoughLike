using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// Looks up painted icons by clip name: <c>skill_PowderKeg</c>, <c>gear_EmberSigil</c>, <c>mut_Gilded</c>, <c>coin</c>, <c>cell</c>,
    /// <c>scroll_Ember</c>, <c>flask_full</c>... Every caller falls back to a plain shape when an icon is missing, so the game
    /// never depends on the art existing.
    /// </summary>
    public static class GameIcons
    {
        static GameIconSet _set;
        static float _nextTry;

        public static GameIconSet Set
        {
            get
            {
                // a missing set is retried now and then (the first build of the art may happen while the editor is running)
                if (_set == null && Time.realtimeSinceStartup >= _nextTry)
                {
                    _set = Resources.Load<GameIconSet>("Art/IconSet");
                    _nextTry = Time.realtimeSinceStartup + 2f;
                }
                return _set;
            }
        }

        /// <summary>Drops the cached set (tests, or after the sheets are rebuilt).</summary>
        public static void Reload() { _set = null; _nextTry = 0f; }

        public static bool Has(string clip) => Owner(clip) != null;

        static CharacterArt Owner(string clip)
        {
            var set = Set;
            if (set == null || set.sheets == null) return null;
            foreach (var s in set.sheets) if (s != null && s.Has(clip)) return s;
            return null;
        }

        public static Sprite Sprite(string clip) => Owner(clip)?.Prop(clip);

        public static Sprite[] Frames(string clip)
        {
            var owner = Owner(clip);
            return owner != null ? owner.Frames(clip) : System.Array.Empty<Sprite>();
        }

        /// <summary>The sheet that holds <paramref name="clip"/>, for a SpriteAnimator to play it.</summary>
        public static CharacterArt Sheet(string clip) => Owner(clip);
    }
}
