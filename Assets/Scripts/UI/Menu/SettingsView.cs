using Roughlike.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Core;

namespace Roughlike.UI
{
    /// <summary>
    /// The settings screen: Video, Audio and Gameplay/Accessibility tabs. Every change is applied immediately and saved
    /// (see <see cref="SettingsService"/> and <see cref="SettingsApplier"/>). Fully navigable with a controller.
    /// </summary>
    public sealed class SettingsView
    {
        public static readonly string[] TabNames = { "Video", "Audio", "Gameplay", "Touch" };

        readonly RectTransform _rows;
        readonly RectTransform _window;
        readonly List<Button> _tabs = new List<Button>();
        readonly List<Selectable> _first = new List<Selectable>();
        int _tab;
        ScrollRect _scroll;

        public int Tab => _tab;
        public Selectable FirstSelectable { get; private set; }
        public RectTransform Rows => _rows;

        /// <summary>Builds the settings UI inside <paramref name="window"/>. Returns the view; <c>FirstSelectable</c> is the control to focus.</summary>
        public static SettingsView Build(RectTransform window, System.Action back)
        {
            var v = new SettingsView(window, back);
            return v;
        }

        SettingsView(RectTransform window, System.Action back)
        {
            _window = window;
            // tab strip
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var b = MenuKit.ListButton(window, TabNames[i], null, new Vector2(240f, 48f), () => ShowTab(index), 26);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((i - (TabNames.Length - 1) * 0.5f) * 256f, -70f), new Vector2(240f, 48f));
                _tabs.Add(b);
                // moving onto a tab with left/right shows it, so a controller never needs a separate confirm
                var trigger = b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Select };
                entry.callback.AddListener(_ => { if (_tab != index) ShowTab(index); });
                trigger.triggers.Add(entry);
            }

            // the rows live in a scrolling list, so any number of settings fits and a controller can reach all of them
            var viewport = UiKit.Node("Viewport", window);
            UiKit.Place(viewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(60f, 100f);
            viewport.offsetMax = new Vector2(-60f, -128f);
            viewport.gameObject.AddComponent<RectMask2D>();

            _rows = UiKit.Node("Rows", viewport);
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            _rows.anchoredPosition = Vector2.zero;
            _rows.sizeDelta = new Vector2(0f, 400f);

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _rows;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 45f;
            ScrollToSelected.Attach(_scroll);

            var reset = MenuKit.Action(window, "Reset to defaults", new Vector2(300f, 52f), () => { SettingsService.ResetToDefaults(); ShowTab(_tab); });
            UiKit.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(300f, 52f));
            var done = MenuKit.Action(window, $"Back  ({InputDevices.BackName})", new Vector2(300f, 52f), () => back());
            UiKit.Place((RectTransform)done.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(300f, 52f));

            ShowTab(0);
        }

        public void ShowTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, TabNames.Length - 1);
            for (int i = _rows.childCount - 1; i >= 0; i--)
            {
                var old = _rows.GetChild(i).gameObject;
                old.SetActive(false);   // hidden at once, destroyed at the end of the frame
                Object.Destroy(old);
            }
            _rows.DetachChildren();
            _first.Clear();

            for (int i = 0; i < _tabs.Count; i++)
            {
                var img = _tabs[i].GetComponent<Image>();
                img.color = i == _tab ? new Color(0.35f, 0.3f, 0.15f, 1f) : new Color(0.14f, 0.16f, 0.22f, 1f);
            }

            switch (_tab)
            {
                case 0: BuildVideo(); break;
                case 1: BuildAudio(); break;
                case 2: BuildGameplay(); break;
                default: BuildTouch(); break;
            }
            _rows.sizeDelta = new Vector2(0f, Mathf.Max(300f, _rows.childCount * (MenuKit.RowSize.y + 8f) + 20f));
            _rows.anchoredPosition = Vector2.zero;
            FirstSelectable = _first.Count > 0 ? _first[0] : _tabs[_tab];
            MenuNav.AutoLink(_window);
        }

        static float RowY(int i) => -i * (MenuKit.RowSize.y + 8f);

        // ------------------------------------------------------------------ video

        void BuildVideo()
        {
            var s = SettingsService.Current;
            int row = 0;

            if (Roughlike.Utils.WebBridge.IsWebGL || Application.isMobilePlatform)
            {
                BuildVideoForBrowser(s);
                return;
            }

            // resolution
            var resolutions = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            var labels = new List<string> { "Desktop resolution" };
            foreach (var r in resolutions) labels.Add(r.x + " x " + r.y);
            int current = 0;
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].x == s.resolutionWidth && resolutions[i].y == s.resolutionHeight) current = i + 1;

            var slot = MenuKit.Row(_rows, "Resolution", RowY(row++), out _);
            AddCycle(slot, labels, current, i =>
            {
                s.resolutionWidth = i == 0 ? 0 : resolutions[i - 1].x;
                s.resolutionHeight = i == 0 ? 0 : resolutions[i - 1].y;
                SettingsService.NotifyChanged();
            });

            slot = MenuKit.Row(_rows, "Display mode", RowY(row++), out _);
            AddCycle(slot, new[] { "Borderless fullscreen", "Exclusive fullscreen", "Windowed" }, s.fullscreenMode, i =>
            {
                s.fullscreenMode = i;
                SettingsService.NotifyChanged();
            });

            slot = MenuKit.Row(_rows, "Vertical sync", RowY(row++), out _);
            AddToggle(slot, s.vsync, v => { s.vsync = v; SettingsService.NotifyChanged(); });

            var qualityLabels = new List<string> { "Project default" };
            qualityLabels.AddRange(QualitySettings.names);
            slot = MenuKit.Row(_rows, "Quality", RowY(row++), out _);
            AddCycle(slot, qualityLabels, s.qualityLevel + 1, i => { s.qualityLevel = i - 1; SettingsService.NotifyChanged(); });

            var note = UiKit.Label(_rows, "Note", "Resolution and display mode apply in a built game; in the editor use the Game view.", 20, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RowY(row) - 6f), new Vector2(-20f, 40f));
        }

        /// <summary>Browsers and phones own the window, so the only video choices are fullscreen and quality.</summary>
        void BuildVideoForBrowser(GameSettings s)
        {
            var slot = MenuKit.Row(_rows, "Fullscreen", RowY(0), out _);
            AddToggle(slot, Screen.fullScreen, v => Screen.fullScreen = v);

            var qualityLabels = new List<string> { "Project default" };
            qualityLabels.AddRange(QualitySettings.names);
            slot = MenuKit.Row(_rows, "Quality", RowY(1), out _);
            AddCycle(slot, qualityLabels, s.qualityLevel + 1, i => { s.qualityLevel = i - 1; SettingsService.NotifyChanged(); });

            var note = UiKit.Label(_rows, "Note", "Your browser controls the window size. Fullscreen needs a click or tap to take effect.", 20, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RowY(2) - 6f), new Vector2(-20f, 40f));
        }

        // ------------------------------------------------------------------ audio

        void BuildAudio()
        {
            var s = SettingsService.Current;
            AddSlider("Master volume", 0, s.master, v => { s.master = v; SettingsService.NotifyChanged(); });
            AddSlider("Music", 1, s.music, v => { s.music = v; SettingsService.NotifyChanged(); });
            AddSlider("Sound effects", 2, s.sfx, v => { s.sfx = v; SettingsService.NotifyChanged(); });
            AddSlider("Interface", 3, s.ui, v => { s.ui = v; SettingsService.NotifyChanged(); });
            AddSlider("Ambience", 4, s.ambient, v => { s.ambient = v; SettingsService.NotifyChanged(); });

            var note = UiKit.Label(_rows, "Note", "Sound effects, interface sounds and ambience each have their own volume. Music is not in the game yet.", 20, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RowY(5) - 6f), new Vector2(-20f, 56f));
        }

        // ------------------------------------------------------------------ gameplay / accessibility

        void BuildGameplay()
        {
            var s = SettingsService.Current;
            AddSlider("Screen shake", 0, s.screenShake, v => { s.screenShake = v; SettingsService.NotifyChanged(); });
            AddSlider("Hit-stop (impact freeze)", 1, s.hitStop, v => { s.hitStop = v; SettingsService.NotifyChanged(); });
            AddSlider("Flash intensity", 2, s.flashIntensity, v => { s.flashIntensity = v; SettingsService.NotifyChanged(); });

            var slot = MenuKit.Row(_rows, "Damage numbers", RowY(3), out _);
            AddToggle(slot, s.damageNumbers, v => { s.damageNumbers = v; SettingsService.NotifyChanged(); });
            slot = MenuKit.Row(_rows, "Toggle shield (press to raise)", RowY(4), out _);
            AddToggle(slot, s.toggleBlock, v => { s.toggleBlock = v; SettingsService.NotifyChanged(); });
            slot = MenuKit.Row(_rows, "Assist mode (-40% damage taken)", RowY(5), out _);
            AddToggle(slot, s.assistMode, v => { s.assistMode = v; SettingsService.NotifyChanged(); });

            slot = MenuKit.Row(_rows, "Vibration (rumble / haptics)", RowY(6), out _);
            AddToggle(slot, s.haptics, v => { s.haptics = v; SettingsService.NotifyChanged(); if (v) Roughlike.Player.Haptics.Play(Roughlike.Player.HapticKind.Medium); });
            AddSlider("Vibration strength", 7, s.hapticStrength, v => { s.hapticStrength = v; SettingsService.NotifyChanged(); });
            slot = MenuKit.Row(_rows, "Touch controls", RowY(8), out _);
            AddCycle(slot, new[] { "Automatic", "Always on", "Off" }, s.touchControls, i => { s.touchControls = i; SettingsService.NotifyChanged(); });
        }

        // ------------------------------------------------------------------ touch

        void BuildTouch()
        {
            var s = SettingsService.Current;
            var slot = MenuKit.Row(_rows, "Touch controls", RowY(0), out _);
            AddCycle(slot, new[] { "Automatic", "Always on", "Off" }, s.touchControls, i => { s.touchControls = i; SettingsService.NotifyChanged(); });
            AddSlider("Button size", 1, Mathf.InverseLerp(0.7f, 1.5f, s.touchScale), v => { s.touchScale = Mathf.Lerp(0.7f, 1.5f, v); SettingsService.NotifyChanged(); });
            AddSlider("Opacity", 2, Mathf.InverseLerp(0.25f, 1f, s.touchOpacity), v => { s.touchOpacity = Mathf.Lerp(0.25f, 1f, v); SettingsService.NotifyChanged(); });

            slot = MenuKit.Row(_rows, "Move and resize buttons", RowY(3), out _);
            var edit = MenuKit.Action(slot, "Edit layout", new Vector2(240f, 48f), OpenLayoutEditor);
            UiKit.Place((RectTransform)edit.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(240f, 48f));
            _first.Add(edit);
            slot = MenuKit.Row(_rows, "Default positions", RowY(4), out _);
            var reset = MenuKit.Action(slot, "Reset layout", new Vector2(240f, 48f), () =>
            {
                s.touchLayout = new TouchItem[0]; s.touchScale = 1f; s.touchOpacity = 1f;
                SettingsService.NotifyChanged();
                ShowTab(_tab);
            });
            UiKit.Place((RectTransform)reset.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(240f, 48f));
            _first.Add(reset);

            var note = UiKit.Label(_rows, "Note", "Edit layout shows every button; drag to move, tap to select and resize. The controls appear on touch screens, or switch Touch controls to Always on to try it with a mouse.", 22, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RowY(5) - 6f), new Vector2(-20f, 84f));
        }

        void OpenLayoutEditor()
        {
            var root = _rows.GetComponentInParent<UiRoot>();
            if (root == null) return;
            TouchLayoutEditor.Open(root, () => { if (_rows != null) ShowTab(_tab); });
        }

        // ------------------------------------------------------------------ row helpers

        void AddSlider(string label, int row, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var slot = MenuKit.Row(_rows, label, RowY(row), out _);
            var slider = MenuKit.Slider(slot, value, onChanged);
            _first.Add(slider);
        }

        void AddToggle(RectTransform slot, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var t = MenuKit.Toggle(slot, value, onChanged);
            _first.Add(t);
        }

        void AddCycle(RectTransform slot, IList<string> options, int index, System.Action<int> onChanged)
        {
            var c = MenuKit.Cycle(slot, options, index, onChanged);
            _first.Add(c.GetComponent<Button>());
        }
    }
}

