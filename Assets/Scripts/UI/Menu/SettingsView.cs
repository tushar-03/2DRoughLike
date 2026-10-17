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
        public static readonly string[] TabNames = { "Video", "Audio", "Gameplay" };

        readonly RectTransform _rows;
        readonly List<Button> _tabs = new List<Button>();
        readonly List<Selectable> _first = new List<Selectable>();
        int _tab;

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
            // tab strip
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var b = MenuKit.ListButton(window, TabNames[i], null, new Vector2(240f, 48f), () => ShowTab(index), 26);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((i - 1) * 256f, -70f), new Vector2(240f, 48f));
                _tabs.Add(b);
            }

            _rows = UiKit.Node("Rows", window);
            UiKit.Place(_rows, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rows.offsetMin = new Vector2(70f, 100f);
            _rows.offsetMax = new Vector2(-70f, -135f);

            var reset = MenuKit.Action(window, "Reset to defaults", new Vector2(300f, 52f), () => { SettingsService.ResetToDefaults(); ShowTab(_tab); });
            UiKit.Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(300f, 52f));
            var done = MenuKit.Action(window, "Back  (Esc / B)", new Vector2(300f, 52f), () => back());
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
                default: BuildGameplay(); break;
            }
            FirstSelectable = _first.Count > 0 ? _first[0] : _tabs[_tab];
        }

        static float RowY(int i) => -i * (MenuKit.RowSize.y + 8f);

        // ------------------------------------------------------------------ video

        void BuildVideo()
        {
            var s = SettingsService.Current;
            int row = 0;

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

        // ------------------------------------------------------------------ audio

        void BuildAudio()
        {
            var s = SettingsService.Current;
            AddSlider("Master volume", 0, s.master, v => { s.master = v; SettingsService.NotifyChanged(); });
            AddSlider("Music", 1, s.music, v => { s.music = v; SettingsService.NotifyChanged(); });
            AddSlider("Sound effects", 2, s.sfx, v => { s.sfx = v; SettingsService.NotifyChanged(); });
            AddSlider("Interface", 3, s.ui, v => { s.ui = v; SettingsService.NotifyChanged(); });
            AddSlider("Ambience", 4, s.ambient, v => { s.ambient = v; SettingsService.NotifyChanged(); });

            var note = UiKit.Label(_rows, "Note", "Master volume applies now. Music, effects, interface and ambience take effect when the audio pass lands.", 20, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RowY(5) - 6f), new Vector2(-20f, 56f));
        }

        // ------------------------------------------------------------------ gameplay / accessibility

        void BuildGameplay()
        {
            var s = SettingsService.Current;
            AddSlider("Screen shake", 0, s.screenShake, v => { s.screenShake = v; SettingsService.NotifyChanged(); });
            AddSlider("Hit-stop (freeze on impact)", 1, s.hitStop, v => { s.hitStop = v; SettingsService.NotifyChanged(); });
            AddSlider("Flash intensity", 2, s.flashIntensity, v => { s.flashIntensity = v; SettingsService.NotifyChanged(); });

            var slot = MenuKit.Row(_rows, "Damage numbers", RowY(3), out _);
            AddToggle(slot, s.damageNumbers, v => { s.damageNumbers = v; SettingsService.NotifyChanged(); });
            slot = MenuKit.Row(_rows, "Toggle shield (press to raise and lower)", RowY(4), out _);
            AddToggle(slot, s.toggleBlock, v => { s.toggleBlock = v; SettingsService.NotifyChanged(); });
            slot = MenuKit.Row(_rows, "Assist mode (enemies deal 40% less)", RowY(5), out _);
            AddToggle(slot, s.assistMode, v => { s.assistMode = v; SettingsService.NotifyChanged(); });
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

