using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Roughlike.UI
{
    /// <summary>
    /// The one overlay canvas (1920x1080 reference, scales to any resolution) plus the event system with the
    /// Input System UI module, which gives keyboard, mouse and gamepad navigation (stick/D-pad to move, South to
    /// confirm, East to cancel) with no extra setup.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform ModalLayer { get; private set; }

        public static UiRoot Create()
        {
            var go = new GameObject("UI Root");
            var root = go.AddComponent<UiRoot>();
            root.Build();
            return root;
        }

        void Build()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.AddComponent<UiSounds>();

            HudLayer = UiKit.Node("HUD", transform);
            UiKit.Stretch(HudLayer);
            ModalLayer = UiKit.Node("Modals", transform);
            UiKit.Stretch(ModalLayer);

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        void OnDestroy() => PauseGate.Reset();
    }
}
