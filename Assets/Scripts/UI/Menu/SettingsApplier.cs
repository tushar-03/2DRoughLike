using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>
    /// Pushes <see cref="GameSettings"/> into the live game: window, vsync, audio volume, accessibility sliders and the
    /// control bindings. Put one in every scene (<see cref="SettingsBootstrap"/>); it re-applies whenever a setting changes.
    /// </summary>
    public static class SettingsApplier
    {
        public static void Apply(GameSettings s)
        {
            // accessibility and feel
            HitStop.Scale = s.hitStop;
            ScreenShake.Scale = s.screenShake;
            HitReactionVisual.FlashScale = s.flashIntensity;
            CombatFeedbackHub.DamageNumbersEnabled = s.damageNumbers;

            // audio (a mixer with per-group volumes arrives with the audio pass; master works now)
            AudioListener.volume = s.master;

            // keep controllers working when the window or browser tab loses focus for a moment
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            Haptics.RefreshTouchHaptics();

            // video: browsers and phones own the window, so only desktop builds change it
            if (WebBridge.IsWebGL || Application.isMobilePlatform) return;
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            if (s.qualityLevel >= 0 && s.qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != s.qualityLevel)
                QualitySettings.SetQualityLevel(s.qualityLevel, true);

            var mode = ModeFor(s.fullscreenMode);
            if (s.resolutionWidth > 0 && s.resolutionHeight > 0)
                Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, mode);
            else if (Screen.fullScreenMode != mode)
                Screen.fullScreenMode = mode;
        }

        public static FullScreenMode ModeFor(int mode)
        {
            switch (mode)
            {
                case 1: return FullScreenMode.ExclusiveFullScreen;
                case 2: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }
    }

    /// <summary>Applies the saved settings when the scene starts and whenever they change.</summary>
    public sealed class SettingsBootstrap : MonoBehaviour
    {
        void Awake() => SettingsApplier.Apply(SettingsService.Current);
        void OnEnable() => SettingsService.Changed += OnChanged;
        void OnDisable() => SettingsService.Changed -= OnChanged;
        void OnChanged() => SettingsApplier.Apply(SettingsService.Current);
    }
}
