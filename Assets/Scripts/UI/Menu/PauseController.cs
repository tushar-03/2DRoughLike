using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>Opens the pause menu on Esc / Start during a run.</summary>
    public sealed class PauseController : MonoBehaviour
    {
        ModalManager _modals;
        RunManager _run;

        public static PauseController Create(Transform parent, ModalManager modals, RunManager run)
        {
            var go = new GameObject("Pause Controller");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PauseController>();
            p._modals = modals;
            p._run = run;
            return p;
        }

        void Update()
        {
            if (_modals == null || _run == null || _modals.IsOpen) return;
            if (_run.State != RunState.Running) return;
            if (Time.unscaledTime - _modals.LastEmptiedAt < 0.25f) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (pressed) _modals.OpenPause(_run);
        }
    }
}
