using System;
using System.Collections.Generic;

namespace Roughlike.Core
{
    /// <summary>
    /// Deterministic random number generator (SplitMix64). All procedural generation uses this, never
    /// UnityEngine.Random, so the same seed always produces the same run on every machine.
    /// <see cref="Fork"/> derives independent sub-streams (per room, per system) that do not depend on how
    /// many numbers other systems consumed.
    /// </summary>
    public sealed class SeededRng
    {
        ulong _state;

        public SeededRng(ulong seed) { _state = seed; }

        public ulong NextULong()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        /// <summary>Float in [0, 1).</summary>
        public float Value => (NextULong() >> 40) / (float)(1 << 24);

        public float Range(float min, float max) => min + (max - min) * Value;

        public bool Chance(float probability) => Value < probability;

        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>A new generator derived from this one's current state and a salt. Does not advance this generator.</summary>
        public SeededRng Fork(int salt)
        {
            var mixer = new SeededRng(_state + (ulong)(uint)salt * 0xD1B54A32D192ED03UL);
            return new SeededRng(mixer.NextULong());
        }
    }
}
