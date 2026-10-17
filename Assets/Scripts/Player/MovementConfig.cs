using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Every movement tuning value lives here. Edit the asset at runtime in Play Mode to tune live
    /// (changes made in Play Mode to a ScriptableObject persist, so revert if unwanted).
    /// Units: 1 world unit is roughly 1 metre; the player is 1.6 units tall.
    /// </summary>
    [CreateAssetMenu(menuName = "Roughlike/Player/Movement Config", fileName = "PlayerMovement")]
    public class MovementConfig : ScriptableObject
    {
        [Header("Body")]
        public Vector2 colliderSize = new Vector2(0.7f, 1.6f);
        [Tooltip("Collider height while rolling (lets the roll pass under low gaps).")]
        public float rollColliderHeight = 0.8f;

        [Header("Run")]
        public float runSpeed = 8f;
        [Tooltip("Units/s^2 when speeding up on the ground.")]
        public float groundAcceleration = 110f;
        [Tooltip("Units/s^2 when no input is held.")]
        public float groundDeceleration = 130f;
        [Tooltip("Units/s^2 when input opposes current velocity (snappy turn-around).")]
        public float groundTurnAcceleration = 170f;
        public float airAcceleration = 70f;
        public float airDeceleration = 35f;
        [Range(0f, 0.9f)] public float stickDeadzone = 0.25f;

        [Header("Jump")]
        [Tooltip("Apex height of a fully held jump, in units.")]
        public float jumpHeight = 3.2f;
        [Tooltip("Seconds from takeoff to apex for a fully held jump. Together with height this defines gravity.")]
        public float timeToApex = 0.38f;
        [Tooltip("Gravity multiplier when falling.")]
        public float fallGravityMultiplier = 1.5f;
        [Tooltip("Extra gravity multiplier when the jump button is released while rising (variable jump height).")]
        public float jumpCutGravityMultiplier = 3f;
        [Tooltip("Below this |vertical speed| with jump held, gravity is reduced for a floaty apex.")]
        public float apexHangSpeed = 1.5f;
        [Range(0.1f, 1f)] public float apexHangGravityMultiplier = 0.75f;
        public float maxFallSpeed = 22f;
        [Tooltip("Max fall speed while holding down (fast fall).")]
        public float fastFallSpeed = 32f;
        public float fastFallGravityMultiplier = 1.4f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;

        [Header("Roll (works on the ground and in mid-air)")]
        public float rollDistance = 5.2f;
        public float rollDuration = 0.45f;
        [Tooltip("Normalised speed over roll progress (0 to 1). Scaled at runtime so total distance equals rollDistance.")]
        public AnimationCurve rollSpeedCurve = new AnimationCurve(new Keyframe(0f, 1.4f), new Keyframe(1f, 0.6f));
        [Tooltip("Invulnerable from this time after the roll starts...")]
        public float rollInvulnStart = 0.03f;
        [Tooltip("...until this time (seconds).")]
        public float rollInvulnEnd = 0.38f;
        public float rollCooldown = 0.1f;
        public float rollBufferTime = 0.12f;
        [Tooltip("Rolls allowed per airtime. Resets on landing, ladder, ledge or wall contact.")]
        public int airRollsPerAirtime = 1;
        [Range(0f, 1f)] public float airRollGravityScale = 0.3f;
        [Tooltip("Fraction of run speed kept when the roll ends.")]
        [Range(0f, 1.5f)] public float rollExitSpeedFraction = 0.9f;
        [Tooltip("Pressing jump during a ground roll cancels it into a jump that keeps roll momentum.")]
        public bool jumpCancelsRoll = true;

        [Header("Ladder")]
        public float ladderSpeed = 5f;
        public float ladderSnapSpeed = 14f;
        public float ladderJumpOffMultiplier = 0.65f;
        public float ladderRegrabCooldown = 0.25f;

        [Header("Ledge grab")]
        public bool ledgeGrabEnabled = true;
        [Tooltip("Horizontal distance from the body to look for a wall.")]
        public float ledgeReach = 0.2f;
        [Tooltip("Free space required above the ledge corner.")]
        public float ledgeClearance = 0.5f;
        [Tooltip("How far above/below the ledge top the head may be when grabbing.")]
        public float ledgeGrabWindow = 0.35f;
        public float ledgeClimbTime = 0.3f;
        public float ledgeRegrabCooldown = 0.3f;

        [Header("Traversal rune abilities (off in the base kit)")]
        public float wallSlideSpeed = 3f;
        public Vector2 wallJumpVelocity = new Vector2(8.5f, 14.5f);
        [Tooltip("Air control is suspended this long after a wall jump so the kick-off carries you away from the wall.")]
        public float wallJumpInputLock = 0.12f;
        [Tooltip("A jump pressed this soon after leaving (or before touching) a wall still wall-jumps.")]
        public float wallCoyoteTime = 0.12f;
        [Tooltip("How long you cling to a wall after releasing the stick before dropping.")]
        public float wallStickTime = 0.2f;
        [Tooltip("Extra jumps available with the Double Jump rune.")]
        public int extraAirJumps = 1;
        [Range(0.3f, 1f)] public float extraJumpHeightFactor = 0.85f;

        [Header("One-way platforms")]
        public float dropThroughTime = 0.3f;

        // ---- derived ----
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpVelocity => 2f * jumpHeight / timeToApex;

        /// <summary>Roll speed scale so that the integral of speed over the roll equals rollDistance.</summary>
        public float RollSpeedScale()
        {
            const int samples = 24;
            float sum = 0f;
            for (int i = 0; i < samples; i++) sum += Mathf.Max(0f, rollSpeedCurve.Evaluate((i + 0.5f) / samples));
            float mean = sum / samples;
            return mean <= 0.0001f ? 0f : rollDistance / (rollDuration * mean);
        }

        public float RollSpeedAt(float progress01, float scale)
            => Mathf.Max(0f, rollSpeedCurve.Evaluate(Mathf.Clamp01(progress01))) * scale;

        void OnValidate()
        {
            timeToApex = Mathf.Max(0.05f, timeToApex);
            jumpHeight = Mathf.Max(0.1f, jumpHeight);
            rollDuration = Mathf.Max(0.05f, rollDuration);
            rollColliderHeight = Mathf.Clamp(rollColliderHeight, 0.2f, colliderSize.y);
            rollInvulnEnd = Mathf.Min(rollInvulnEnd, rollDuration);
        }
    }
}
