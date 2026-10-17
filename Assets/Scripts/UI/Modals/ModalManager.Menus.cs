using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>The menu-style windows: settings, controls, confirmations, seed entry and the pause menu.</summary>
    public sealed partial class ModalManager
    {
        public void OpenSettings()
        {
            var w = Push("Settings", new Vector2(1100f, 740f), () => Pop(), out RectTransform content);
            var view = SettingsView.Build(content, () => Pop());
            Focus(w, view.FirstSelectable);
        }

        public void OpenControls()
        {
            ControlsView view = null;
            Action close = () => { view?.Cancel(); Pop(); };
            var w = Push("Controls", new Vector2(1100f, 780f), close, out RectTransform content);
            view = ControlsView.Build(content, close);
            Focus(w, view.FirstSelectable);
        }

        /// <summary>A yes/no question. Cancel (Esc / B) answers no.</summary>
        public void OpenConfirm(string title, string message, string yes, string no, Action onYes, Action onNo = null)
        {
            var w = Push(title, new Vector2(820f, 380f), () => { Pop(); onNo?.Invoke(); }, out RectTransform content);

            var text = UiKit.Label(content, "Message", message, 28, Color.white, TextAnchor.MiddleCenter);
            UiKit.Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.rectTransform.offsetMin = new Vector2(50f, 120f);
            text.rectTransform.offsetMax = new Vector2(-50f, -80f);

            var noButton = MenuKit.Action(content, no, new Vector2(300f, 60f), () => { Pop(); onNo?.Invoke(); });
            UiKit.Place((RectTransform)noButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 30f), new Vector2(300f, 60f));
            var yesButton = MenuKit.Action(content, yes, new Vector2(300f, 60f), () => { Pop(); onYes?.Invoke(); });
            UiKit.Place((RectTransform)yesButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 30f), new Vector2(300f, 60f));
            Focus(w, noButton);   // the safe answer is focused first
        }

        /// <summary>Asks for a seed code or any text. <paramref name="onStart"/> receives what was typed.</summary>
        public void OpenSeedEntry(string initial, Action<string> onStart)
        {
            var w = Push("Run from a seed", new Vector2(820f, 440f), () => Pop(), out RectTransform content);

            var hint = UiKit.Label(content, "Hint", "Type a seed code like 3J7-XQS0, or any words. The same seed always builds the same level.", 24, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(-80f, 80f));

            var fieldRoot = UiKit.Node("Seed Field", content);
            UiKit.Place(fieldRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(560f, 64f));
            var bg = UiKit.Image(fieldRoot, "Background", new Color(0.04f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;
            UiKit.Stretch(bg.rectTransform);
            var text = UiKit.Label(fieldRoot, "Text", "", 32, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            text.supportRichText = false;
            var placeholder = UiKit.Label(fieldRoot, "Placeholder", "random seed", 30, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleLeft);
            UiKit.Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-16f, 0f);

            var input = fieldRoot.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 24;
            input.text = initial ?? "";
            MenuKit.VerticalNav(input);

            var cancel = MenuKit.Action(content, "Cancel  (Esc)", new Vector2(300f, 60f), () => Pop());
            UiKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 30f), new Vector2(300f, 60f));
            var start = MenuKit.Action(content, "Start run", new Vector2(300f, 60f), () => { string s = input.text; Pop(); onStart?.Invoke(s); });
            UiKit.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 30f), new Vector2(300f, 60f));
            Focus(w, input);
        }

        // =====================================================================
        // Pause menu (in a run)
        // =====================================================================

        public void OpenPause(RunManager run)
        {
            var w = Push("Paused", new Vector2(620f, 640f), () => Pop(), out RectTransform content);

            string[] labels = { "Resume", "Settings", "Controls", "Save and quit to menu", "Quit to desktop" };
            Action[] actions =
            {
                () => Pop(),
                () => OpenSettings(),
                () => OpenControls(),
                () => { if (run != null) run.SaveNow(); LoadScene(GameSession.MenuScene); },
                () => { if (run != null) run.SaveNow(); QuitApplication(); }
            };

            Selectable first = null;
            for (int i = 0; i < labels.Length; i++)
            {
                var action = actions[i];
                var b = MenuKit.ListButton(content, labels[i], null, new Vector2(460f, 72f), () => action(), 30);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f - i * 92f), new Vector2(460f, 72f));
                if (first == null) first = b;
            }
            Focus(w, first);
        }

        // =====================================================================
        // Scene and application helpers (shared with the main menu)
        // =====================================================================

        /// <summary>Tests replace this to observe scene changes without actually loading scenes.</summary>
        public static Action<string> LoadSceneHandler;

        public static void LoadScene(string sceneName)
        {
            PauseGate.Reset();
            HitStop.Cancel();
            if (LoadSceneHandler != null) { LoadSceneHandler(sceneName); return; }
            SceneManager.LoadScene(sceneName);
        }

        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

