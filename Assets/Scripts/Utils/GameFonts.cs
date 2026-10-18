using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// The game's typefaces, loaded from Resources/Fonts so they ship in every build (including the browser, which has no
    /// installed fonts). Cinzel Decorative for titles and buttons, Almendra for reading text. Both are SIL OFL licensed
    /// (the licence files sit next to them). Falls back to the built-in font if a file is missing.
    /// </summary>
    public static class GameFonts
    {
        static Font _heading, _display, _body, _bodyBold, _fallback;

        static Font Fallback => _fallback != null ? _fallback : (_fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        static Font Load(string name, ref Font cache)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Font>("Fonts/" + name);
            return cache != null ? cache : Fallback;
        }

        /// <summary>Titles, buttons, numbers that should feel engraved.</summary>
        public static Font Heading => Load("Heading", ref _heading);
        /// <summary>The logo and other very large display text.</summary>
        public static Font Display => Load("Display", ref _display);
        /// <summary>Descriptions, tooltips, tutorial text.</summary>
        public static Font Body => Load("Body", ref _body);
        public static Font BodyBold => Load("BodyBold", ref _bodyBold);
    }
}
