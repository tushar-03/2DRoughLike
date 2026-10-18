using Roughlike.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Rooms;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// The main menu scene: title over an animated lantern-lit cathedral backdrop, with Continue, New Game, New Game from a
    /// seed, Settings, Controls and Quit. Fully controller navigable. Everything is generated in code.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        sealed class Ember { public RectTransform Rt; public Image Img; public float Speed, Sway, Phase, Size; }

        UiRoot _root;
        ModalManager _modals;
        RectTransform _backdrop;

        /// <summary>When set, the menu uses the biome's painted backdrop layers instead of the code-drawn columns.</summary>
        public EnvironmentArt Art;
        readonly List<Ember> _embers = new List<Ember>();
        readonly List<Image> _lanterns = new List<Image>();
        readonly List<float> _lanternPhase = new List<float>();
        readonly List<RectTransform> _parallax = new List<RectTransform>();
        readonly List<float> _parallaxSpeed = new List<float>();
        Button _continue, _newGame, _newSeed, _settings, _controls, _quit;

        public Button ContinueButton => _continue;
        public Button NewGameButton => _newGame;
        public ModalManager Modals => _modals;

        void Awake()
        {
            gameObject.AddComponent<SettingsBootstrap>();
        }

        void Start() => Build();

        public void Build()
        {
            if (_root != null) return;

            _root = UiRoot.Create();
            _root.transform.SetParent(transform, false);
            _modals = ModalManager.Create(_root, null, false);

            BuildBackdrop(_root.HudLayer);
            BuildTitle(_root.HudLayer);
            BuildButtons(_root.HudLayer);
            BuildFooter(_root.HudLayer);
        }

        // =====================================================================
        // Backdrop
        // =====================================================================

        void BuildBackdrop(RectTransform parent)
        {
            _backdrop = UiKit.Node("Backdrop", parent);
            UiKit.Stretch(_backdrop);

            // vertical gradient: deep teal at the top fading to near-black, with a warm glow low down
            var gradient = UiKit.Image(_backdrop, "Gradient", Color.white, MakeGradient());
            UiKit.Stretch(gradient.rectTransform);

            if (Art != null && Art.backdrop != null && Art.backdrop.Length > 0)
            {
                // the painted layers, far to near, each drifting a little faster than the last
                for (int i = 0; i < Art.backdrop.Length; i++)
                    if (Art.backdrop[i] != null && Art.backdrop[i].sprite != null) BuildPaintedLayer(Art.backdrop[i], 10f + i * 16f);
            }
            else
            {
                // three layers of drowned cathedral columns and arches, nearer layers darker and faster
                BuildColumns(_backdrop, 9, 0.18f, new Color(0.10f, 0.16f, 0.20f, 1f), 14f, 1.00f);
                BuildColumns(_backdrop, 7, 0.28f, new Color(0.07f, 0.11f, 0.14f, 1f), 26f, 1.25f);
                BuildColumns(_backdrop, 5, 0.40f, new Color(0.035f, 0.055f, 0.07f, 1f), 44f, 1.55f);
            }

            // hanging lanterns
            for (int i = 0; i < 7; i++)
            {
                var l = UiKit.Image(_backdrop, "Lantern", new Color(1f, 0.7f, 0.3f, 0.9f), ProceduralSprites.Circle);
                float x = Mathf.Lerp(0.08f, 0.92f, i / 6f) + ((i * 37) % 11) * 0.004f;
                float y = 0.22f + ((i * 53) % 7) * 0.045f;   // low, so the title stays clear
                UiKit.Place(l.rectTransform, new Vector2(x, y), new Vector2(x, y), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (60f + (i % 3) * 18f));
                _lanterns.Add(l);
                _lanternPhase.Add(i * 1.7f);
            }

            // drifting embers
            var rng = new SeededRng(7);
            for (int i = 0; i < 46; i++)
            {
                var img = UiKit.Image(_backdrop, "Ember", new Color(1f, 0.6f, 0.25f, 0.8f), ProceduralSprites.Circle);
                var e = new Ember
                {
                    Rt = img.rectTransform, Img = img,
                    Speed = rng.Range(14f, 48f), Sway = rng.Range(10f, 40f), Phase = rng.Range(0f, 6.28f), Size = rng.Range(3f, 9f)
                };
                UiKit.Place(e.Rt, new Vector2(rng.Value, 0f), new Vector2(rng.Value, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, rng.Range(0f, 1080f)), Vector2.one * e.Size);
                e.Rt.anchorMax = e.Rt.anchorMin;
                _embers.Add(e);
            }

            // vignette
            var vignette = UiKit.Image(_backdrop, "Vignette", new Color(0f, 0f, 0f, 0.55f), MakeVignette());
            UiKit.Stretch(vignette.rectTransform);
        }

        /// <summary>Three copies of a tileable backdrop sprite side by side, so the drift never shows an edge.</summary>
        void BuildPaintedLayer(EnvironmentArt.BackdropLayer def, float speed)
        {
            var layer = UiKit.Node("Painted " + def.name, _backdrop);
            UiKit.Place(layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _parallax.Add(layer);
            _parallaxSpeed.Add(speed);

            float h = 1240f;
            float w = h * def.sprite.rect.width / def.sprite.rect.height;
            for (int i = -1; i <= 1; i++)
            {
                var img = UiKit.Image(layer, "Copy", def.tint, def.sprite);
                UiKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i * w, 0f), new Vector2(w, h));
            }
        }

        void BuildColumns(RectTransform parent, int count, float widthFraction, Color colour, float speed, float heightScale)
        {
            var layer = UiKit.Node("Columns", parent);
            UiKit.Place(layer, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            layer.offsetMin = new Vector2(-300f, 0f);
            layer.offsetMax = new Vector2(300f, 0f);
            _parallax.Add(layer);
            _parallaxSpeed.Add(speed);

            float spacing = 1.0f / (count - 1);
            for (int i = 0; i < count; i++)
            {
                float x = i * spacing;
                float h = 0.62f * heightScale;
                var col = UiKit.Image(layer, "Column", colour);
                UiKit.Place(col.rectTransform, new Vector2(x, 0f), new Vector2(x, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(70f * widthFraction * 5f, 1080f * h));
                // arch between this column and the next
                if (i < count - 1)
                {
                    var arch = UiKit.Image(layer, "Arch", colour, ProceduralSprites.Circle);
                    float cx = x + spacing * 0.5f;
                    UiKit.Place(arch.rectTransform, new Vector2(cx, 0f), new Vector2(cx, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 1080f * h), new Vector2(spacing * 1920f * 0.95f, spacing * 1920f * 0.95f * 0.9f));
                }
            }
        }

        static Sprite MakeGradient()
        {
            const int h = 128;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);                       // 0 bottom, 1 top
                Color top = new Color(0.05f, 0.13f, 0.17f);
                Color mid = new Color(0.03f, 0.06f, 0.09f);
                Color glow = new Color(0.16f, 0.09f, 0.05f);        // warm lantern glow at the bottom
                Color c = t > 0.45f ? Color.Lerp(mid, top, (t - 0.45f) / 0.55f) : Color.Lerp(glow, mid, t / 0.45f);
                tex.SetPixel(0, y, c);
            }
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Sprite MakeVignette()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.9f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
            }
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // =====================================================================
        // Title and buttons
        // =====================================================================

        void BuildTitle(RectTransform parent)
        {
            TitleLogo.Create(parent, -40f);

            var sub = UiKit.Label(parent, "Subtitle", "two souls, one sunken city, and one more try", 36, new Color(0.75f, 0.85f, 0.9f, 0.9f), TextAnchor.MiddleCenter);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(1500f, 50f));
            var s2 = sub.gameObject.AddComponent<Shadow>();
            s2.effectColor = new Color(0f, 0f, 0f, 0.8f);
            s2.effectDistance = new Vector2(2f, -2f);
        }

        void BuildButtons(RectTransform parent)
        {
            var column = UiKit.Node("Buttons", parent);
            UiKit.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -156f), new Vector2(620f, 640f));

            bool hasSave = RunSave.TryLoad(out RunSave save);
            string summary = hasSave ? (string.IsNullOrEmpty(save.summary) ? "Resume your run" : save.summary) : "No saved run";

            float y = 0f;
            _continue = Add(column, "Continue", summary, ref y, OnContinue, 86f);
            _continue.interactable = hasSave;
            _newGame = Add(column, "New Game", null, ref y, OnNewGame);
            _tutorial = Add(column, "Tutorial", "Learn to move, fight, loot and survive", ref y, OnTutorial, 86f);
            _newSeed = Add(column, "New Game from Seed", null, ref y, OnNewSeed);
            _settings = Add(column, "Settings", null, ref y, () => _modals.OpenSettings());
            _controls = Add(column, "Controls", null, ref y, () => _modals.OpenControls());
            if (!Roughlike.Utils.WebBridge.IsWebGL && !Application.isMobilePlatform)
                _quit = Add(column, "Quit", null, ref y, ModalManager.QuitApplication);

            // keep the vertical order explicit so a disabled Continue is skipped by the controller
            var first = hasSave ? _continue : _newGame;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        Button Add(RectTransform column, string title, string subtitle, ref float y, UnityEngine.Events.UnityAction onClick, float height = 72f)
        {
            var b = MenuKit.ListButton(column, title, subtitle, new Vector2(620f, height), onClick, 34);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, height));
            y -= height + 14f;
            return b;
        }

        static string HintLine()
        {
            switch (InputDevices.Scheme)
            {
                case InputScheme.Gamepad: return "Move: stick / D-pad     Confirm: A     Back: B";
                case InputScheme.Touch: return "Tap to choose";
                default: return "Move: arrows / stick     Confirm: Enter / A     Back: Esc / B";
            }
        }

        void BuildFooter(RectTransform parent)
        {
            var hint = UiKit.Label(parent, "Hint", HintLine(), 24, new Color(0.8f, 0.85f, 0.9f, 0.7f), TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1200f, 36f));

            var version = UiKit.Label(parent, "Version", "v" + Application.version + "  -  alpha build", 20, new Color(0.8f, 0.85f, 0.9f, 0.45f), TextAnchor.LowerRight);
            UiKit.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(500f, 30f));
        }

        // =====================================================================
        // Actions
        // =====================================================================

        public void OnContinue()
        {
            GameSession.StartContinue();
            ModalManager.LoadScene(GameSession.GameplayScene);
        }

        Button _tutorial;

        /// <summary>Starts the tutorial level.</summary>
        public void OnTutorial() => ModalManager.LoadScene(GameSession.TutorialScene);

        public void OnNewGame()
        {
            // first time: offer the tutorial before the first run
            if (!SettingsService.Current.tutorialSeen)
            {
                _modals.OpenConfirm("New here?", "Learn the basics in a short guided level first? You can replay it any time from the main menu.",
                    "Play tutorial", "Skip it",
                    () => OnTutorial(),
                    () => { SettingsService.Current.tutorialSeen = true; SettingsService.NotifyChanged(true); ConfirmThenStart(0); });
                return;
            }
            ConfirmThenStart(0);
        }

        public void OnNewSeed()
        {
            _modals.OpenSeedEntry("", text =>
            {
                SeedCode.TryParse(text, out uint seed);   // empty text means a random seed
                ConfirmThenStart(seed);
            });
        }

        void ConfirmThenStart(uint seed)
        {
            System.Action pick = () => _modals.OpenDifficulty(d => StartNew(seed, d));
            if (!RunSave.Exists) { pick(); return; }
            _modals.OpenConfirm("Start a new run?", "You have a saved run. Starting a new one will erase it.",
                "Start new run", "Keep my run", pick);
        }

        static void StartNew(uint seed, Difficulty difficulty)
        {
            GameSession.StartNew(seed, difficulty);
            Roughlike.Items.LoadoutSelection.Index = -1;
            ModalManager.LoadScene(GameSession.LoadoutScene);   // practice room first; its portal starts the run
        }

        // =====================================================================
        // Animation
        // =====================================================================

        void Update()
        {
            if (_backdrop == null) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            foreach (var e in _embers)
            {
                var p = e.Rt.anchoredPosition;
                p.y += e.Speed * dt;
                p.x = Mathf.Sin(t * 0.6f + e.Phase) * e.Sway;
                if (p.y > 1180f) p.y = -40f;
                e.Rt.anchoredPosition = p;
                var c = e.Img.color; c.a = 0.35f + 0.5f * Mathf.Abs(Mathf.Sin(t * 1.3f + e.Phase)); e.Img.color = c;
            }

            for (int i = 0; i < _lanterns.Count; i++)
            {
                float flicker = 0.7f + 0.25f * Mathf.Sin(t * 3.1f + _lanternPhase[i]) + 0.05f * Mathf.Sin(t * 11f + _lanternPhase[i] * 3f);
                var c = _lanterns[i].color; c.a = flicker * 0.65f; _lanterns[i].color = c;
            }

            for (int i = 0; i < _parallax.Count; i++)
            {
                var r = _parallax[i];
                float offset = Mathf.Sin(t * 0.07f + i) * _parallaxSpeed[i] * 6f;
                r.anchoredPosition = new Vector2(offset, 0f);
            }
        }
    }
}

