using System;
using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>One customised on-screen control: its anchored position (1920x1080 reference units) and its own size factor.</summary>
    [Serializable]
    public class TouchItem
    {
        public string id;
        public float x, y;
        public float scale = 1f;
    }

    /// <summary>Everything the player can change in the settings menu. Plain serializable data with sane defaults.</summary>
    [Serializable]
    public class GameSettings
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;

        // ---- video
        /// <summary>0 means the current desktop resolution.</summary>
        public int resolutionWidth;
        public int resolutionHeight;
        /// <summary>0 borderless fullscreen, 1 exclusive fullscreen, 2 windowed.</summary>
        public int fullscreenMode = 0;
        public bool vsync = true;
        /// <summary>-1 keeps the project's quality level.</summary>
        public int qualityLevel = -1;

        // ---- audio (0..1)
        public float master = 1f;
        public float music = 0.8f;
        public float sfx = 1f;
        public float ui = 1f;
        public float ambient = 0.8f;

        // ---- gameplay and accessibility
        [Range(0f, 1f)] public float screenShake = 1f;
        [Range(0f, 1f)] public float hitStop = 1f;
        [Range(0f, 1f)] public float flashIntensity = 1f;
        public bool damageNumbers = true;
        /// <summary>Shield: press once to raise, press again to lower (instead of holding).</summary>
        public bool toggleBlock;
        /// <summary>Assist mode: enemies deal 40% less damage.</summary>
        public bool assistMode;

        // ---- device
        /// <summary>Controller rumble and phone vibration.</summary>
        public bool haptics = true;
        [Range(0f, 1f)] public float hapticStrength = 0.8f;
        /// <summary>On-screen touch controls: 0 automatic (touch devices only), 1 always, 2 never.</summary>
        public int touchControls = 0;
        /// <summary>The tutorial has been offered or played once.</summary>
        public bool tutorialSeen;
        /// <summary>The difficulty last picked for a new game (0 Story, 1 Normal, 2 Hard, 3 Nightmare).</summary>
        public int difficulty = 1;

        /// <summary>Where the player moved each on-screen touch control, and how big they made it (only controls they changed are stored).</summary>
        public TouchItem[] touchLayout = new TouchItem[0];
        /// <summary>Opacity of the on-screen controls, 0.25 to 1.</summary>
        public float touchOpacity = 1f;
        /// <summary>Size of all on-screen controls, 0.7 to 1.5.</summary>
        public float touchScale = 1f;

        // ---- controls: Input System binding overrides as JSON
        public string bindingOverrides = "";

        public TouchItem FindTouch(string id)
        {
            if (touchLayout != null) foreach (var t in touchLayout) if (t != null && t.id == id) return t;
            return null;
        }

        /// <summary>Returns the saved entry for a control, adding one (at its default position) when it has none yet.</summary>
        public TouchItem GetOrAddTouch(string id, float x, float y)
        {
            var t = FindTouch(id);
            if (t != null) return t;
            t = new TouchItem { id = id, x = x, y = y, scale = 1f };
            var list = new System.Collections.Generic.List<TouchItem>(touchLayout ?? new TouchItem[0]) { t };
            touchLayout = list.ToArray();
            return t;
        }

        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));

        /// <summary>Brings every value back into a valid range (hand-edited or older files).</summary>
        public void Sanitise()
        {
            master = Mathf.Clamp01(master);
            music = Mathf.Clamp01(music);
            sfx = Mathf.Clamp01(sfx);
            ui = Mathf.Clamp01(ui);
            ambient = Mathf.Clamp01(ambient);
            screenShake = Mathf.Clamp01(screenShake);
            hitStop = Mathf.Clamp01(hitStop);
            flashIntensity = Mathf.Clamp01(flashIntensity);
            fullscreenMode = Mathf.Clamp(fullscreenMode, 0, 2);
            hapticStrength = Mathf.Clamp01(hapticStrength);
            touchControls = Mathf.Clamp(touchControls, 0, 2);
            touchOpacity = Mathf.Clamp(touchOpacity, 0.25f, 1f);
            touchScale = Mathf.Clamp(touchScale, 0.7f, 1.5f);
            if (touchLayout == null) touchLayout = new TouchItem[0];
            difficulty = Mathf.Clamp(difficulty, 0, 3);
            resolutionWidth = Mathf.Max(0, resolutionWidth);
            resolutionHeight = Mathf.Max(0, resolutionHeight);
            if (bindingOverrides == null) bindingOverrides = "";
            version = CurrentVersion;
        }
    }

    /// <summary>Loads, holds and saves the current settings. Anything that reacts to a change subscribes to <see cref="Changed"/>.</summary>
    public static class SettingsService
    {
        public const string SaveName = "settings";

        static GameSettings _current;

        public static event Action Changed;

        public static GameSettings Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        public static void Load()
        {
            _current = new GameSettings();
            if (SaveService.TryRead(SaveName, out string json, text => JsonUtility.FromJson<GameSettings>(text) != null))
            {
                try { JsonUtility.FromJsonOverwrite(json, _current); }
                catch (Exception e) { Debug.LogWarning("Settings file was unreadable, using defaults: " + e.Message); _current = new GameSettings(); }
            }
            _current.Sanitise();
        }

        public static void Save()
        {
            Current.Sanitise();
            SaveService.Write(SaveName, JsonUtility.ToJson(Current, true));
        }

        /// <summary>Call after editing <see cref="Current"/>: applies and saves.</summary>
        public static void NotifyChanged(bool save = true)
        {
            Current.Sanitise();
            if (save) Save();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            string overrides = Current.bindingOverrides;
            _current = new GameSettings { bindingOverrides = overrides };
            NotifyChanged();
        }

        /// <summary>Forget the in-memory copy (tests, or after the save folder changes).</summary>
        public static void Unload() => _current = null;
    }
}
