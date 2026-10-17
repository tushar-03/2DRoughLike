using UnityEngine;
using UnityEngine.InputSystem;

namespace Roughlike.Player
{
    /// <summary>
    /// On-screen movement debug HUD (F1 toggles) with ability toggles (F2 double jump, F3 wall jump),
    /// reset-to-start (R). Dev only; compiled out of release builds.
    /// </summary>
    public class MovementDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] PlayerController player;
        [SerializeField] bool visible = true;
        Vector2 _start;
        GUIStyle _style;

        public void SetPlayer(PlayerController p) { player = p; }

        void Start()
        {
            if (player != null) _start = player.transform.position;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || player == null) return;
            if (kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (kb.f2Key.wasPressedThisFrame) player.Abilities ^= MovementAbilities.DoubleJump;
            if (kb.f3Key.wasPressedThisFrame) player.Abilities ^= MovementAbilities.WallJump;
            if (kb.rKey.wasPressedThisFrame) player.ResetTo(_start);
        }

        void OnGUI()
        {
            if (!visible || player == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            GUI.Box(new Rect(8, 8, 520, 130), GUIContent.none);
            GUI.Label(new Rect(16, 12, 510, 120),
                player.DebugSummary() +
                "\n[F1] hide  [F2] double jump  [F3] wall jump  [R] reset" +
                "\nMove WASD/arrows/stick  Jump Space/C/A  Roll Shift/L/B  Drop: Down+Jump  Ladder: Up/Down", _style);
        }
#endif
    }
}
