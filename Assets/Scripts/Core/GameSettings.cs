using System;
using UnityEngine;

namespace Roughlike.Core
{
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

        // ---- controls: Input System binding overrides as JSON
        public string bindingOverrides = "";

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
