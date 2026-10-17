using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>One frame of player intent, independent of the device that produced it.</summary>
    public struct PlayerIntent
    {
        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool RollPressed;
        public bool Attack1Pressed;
        public bool Attack1Held;
        public bool Attack2Pressed;
        public bool Attack2Held;
        public bool Skill1Pressed;
        public bool Skill2Pressed;
        public bool InteractPressed;
        public bool FlaskPressed;

        public bool DownHeld(float threshold = 0.5f) => Move.y < -threshold;
        public bool UpHeld(float threshold = 0.5f) => Move.y > threshold;
    }

    /// <summary>Anything that can drive the player: real input, a test script, a replay or an AI.</summary>
    public interface IIntentSource
    {
        /// <summary>Called once per controller tick. One-shot flags (pressed) must be consumed here.</summary>
        PlayerIntent Read();
    }
}

