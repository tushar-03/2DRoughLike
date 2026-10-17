using System.Text;

namespace Roughlike.Core
{
    /// <summary>
    /// Human-shareable run seeds. A seed is a 32-bit number shown as a short Crockford base-32 code such as
    /// "7QM-39XA2" (ambiguous letters removed). Any text typed in is accepted: valid codes decode exactly,
    /// anything else is hashed, so players can use words as seeds.
    /// </summary>
    public static class SeedCode
    {
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        const int Length = 7;   // 7 x 5 bits = 35 bits, enough for 32

        public static string Encode(uint seed)
        {
            var chars = new char[Length];
            ulong v = seed;
            for (int i = Length - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(v & 31UL)];
                v >>= 5;
            }
            return new string(chars, 0, 3) + "-" + new string(chars, 3, 4);
        }

        /// <summary>Parses a code or free text into a seed. Empty input returns false.</summary>
        public static bool TryParse(string text, out uint seed)
        {
            seed = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var clean = new StringBuilder();
            bool allValid = true;
            foreach (char raw in text)
            {
                if (raw == '-' || raw == ' ' || raw == '_') continue;
                char c = char.ToUpperInvariant(raw);
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                if (Alphabet.IndexOf(c) < 0) allValid = false;
                clean.Append(c);
            }

            if (allValid && clean.Length == Length)
            {
                ulong v = 0;
                for (int i = 0; i < clean.Length; i++) v = (v << 5) | (uint)Alphabet.IndexOf(clean[i]);
                if (v <= uint.MaxValue)
                {
                    seed = (uint)v;
                    return true;
                }
            }

            seed = Hash(text.Trim());
            return true;
        }

        /// <summary>FNV-1a, stable across platforms.</summary>
        public static uint Hash(string text)
        {
            uint h = 2166136261u;
            foreach (char c in text)
            {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }
    }
}
