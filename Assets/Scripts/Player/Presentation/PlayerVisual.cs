using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Procedural animation for the player sprite: facing flip, squash and stretch on jump/land,
    /// roll spin and a breathing idle. Pure presentation; it only listens to controller events,
    /// so a rigged character can replace it without touching movement.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [SerializeField] PlayerController controller;
        [Tooltip("Child transform that holds the sprite. Pivot should be at the feet.")]
        [SerializeField] Transform body;
        [SerializeField] float squashRecovery = 14f;
        [SerializeField] float breatheAmount = 0.02f;
        [SerializeField] float breatheSpeed = 2.4f;

        Vector2 _squash = Vector2.one;
        float _spin;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (body == null) body = transform;
        }

        void OnEnable()
        {
            if (controller == null) return;
            controller.Jumped += OnJump;
            controller.DoubleJumped += OnJump;
            controller.WallJumped += OnJump;
            controller.Landed += OnLand;
            controller.RollStarted += OnRollStart;
        }

        void OnDisable()
        {
            if (controller == null) return;
            controller.Jumped -= OnJump;
            controller.DoubleJumped -= OnJump;
            controller.WallJumped -= OnJump;
            controller.Landed -= OnLand;
            controller.RollStarted -= OnRollStart;
        }

        void OnJump() => _squash = new Vector2(0.78f, 1.28f);

        void OnLand(float impact)
        {
            float k = Mathf.Clamp01(impact / 22f);
            _squash = new Vector2(1f + 0.35f * k, 1f - 0.4f * k);
        }

        void OnRollStart() => _squash = new Vector2(1.15f, 0.85f);

        void LateUpdate()
        {
            if (controller == null || body == null) return;
            float dt = Time.deltaTime;

            _squash = Vector2.Lerp(_squash, Vector2.one, 1f - Mathf.Exp(-squashRecovery * dt));

            float breathe = 1f + Mathf.Sin(Time.time * breatheSpeed) * breatheAmount;
            bool idle = controller.State == PlayerState.Grounded && Mathf.Abs(controller.Velocity.x) < 0.1f;
            float by = idle ? breathe : 1f;

            // Stretch with vertical speed while airborne.
            float stretch = controller.State == PlayerState.Airborne ? Mathf.Clamp(Mathf.Abs(controller.Velocity.y) / 40f, 0f, 0.12f) : 0f;

            body.localScale = new Vector3(_squash.x * (1f - stretch) * controller.Facing, _squash.y * by * (1f + stretch), 1f);

            if (controller.State == PlayerState.Rolling)
            {
                _spin = -360f * controller.RollProgress * controller.Facing;
                // Spin about the rolled body's centre (the sprite pivot is at the feet).
                Quaternion rot = Quaternion.Euler(0f, 0f, _spin);
                Vector3 centre = new Vector3(0f, controller.Config.rollColliderHeight * 0.5f, 0f);
                body.localRotation = rot;
                body.localPosition = centre - rot * centre;
                body.localScale = new Vector3(controller.Facing * 0.8f, 0.5f, 1f);
            }
            else
            {
                body.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
            }
        }
    }
}
