using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// The point the Cinemachine camera follows. It leads the player in the facing direction, looks
    /// further ahead while running, drops when falling fast and when the player holds down on a ledge.
    /// Cinemachine adds damping on top of this.
    /// </summary>
    public class PlayerCameraTarget : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float headHeight = 1.0f;
        [SerializeField] float lookAheadDistance = 2.4f;
        [SerializeField] float lookAheadSmoothing = 4f;
        [SerializeField] float fallLookDown = 2.5f;
        [SerializeField] float fallLookSpeed = 14f;
        [SerializeField] float verticalSmoothing = 6f;

        float _lookX;
        float _lookY;

        public void SetPlayer(PlayerController p) { player = p; }

        void LateUpdate()
        {
            if (player == null) return;
            float dt = Time.deltaTime;

            float runFactor = Mathf.Clamp01(Mathf.Abs(player.Velocity.x) / Mathf.Max(0.01f, player.Config.runSpeed));
            float targetX = player.Facing * lookAheadDistance * Mathf.Lerp(0.35f, 1f, runFactor);
            _lookX = Mathf.Lerp(_lookX, targetX, 1f - Mathf.Exp(-lookAheadSmoothing * dt));

            float targetY = 0f;
            if (player.State == PlayerState.Airborne && player.Velocity.y < -fallLookSpeed * 0.5f)
                targetY = -fallLookDown * Mathf.Clamp01((-player.Velocity.y - fallLookSpeed * 0.5f) / fallLookSpeed);
            _lookY = Mathf.Lerp(_lookY, targetY, 1f - Mathf.Exp(-verticalSmoothing * dt));

            Vector3 p = player.transform.position;
            transform.position = new Vector3(p.x + _lookX, p.y + headHeight + _lookY, 0f);
        }
    }
}

