using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>
    /// Briefly swallows gameplay input. Closing a menu with a button that also does something in the game (A confirms and jumps,
    /// B cancels and rolls) would otherwise fire both on the same frame.
    /// </summary>
    public static class InputGate
    {
        static int _untilFrame = -1;

        public static bool Blocked => Time.frameCount <= _untilFrame;

        /// <summary>Ignore gameplay input for this frame and the next <paramref name="frames"/> - 1.</summary>
        public static void Block(int frames) => _untilFrame = Mathf.Max(_untilFrame, Time.frameCount + frames);

        public static void Clear() => _untilFrame = -1;
    }
}
