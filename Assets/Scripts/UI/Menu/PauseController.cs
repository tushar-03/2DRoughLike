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
        System.Action _skipTutorial;

        public static PauseController Create(Transform parent, ModalManager modals, RunManager run, System.Action skipTutorial = null)
        {
            var go = new GameObject("Pause Controller");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<PauseController>();
            p._modals = modals;
            p._run = run;
            p._skipTutorial = skipTutorial;
            return p;
        }

        void Update()
        {
            if (_modals == null || _modals.IsOpen) return;
            if (_run != null && _run.State != RunState.Running) return;
            if (Time.unscaledTime - _modals.LastEmptiedAt < 0.25f) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (pressed) _modals.OpenPause(_run, _skipTutorial);
        }
    }
}
