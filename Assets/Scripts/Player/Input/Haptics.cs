using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Player
{
    public enum HapticKind { Light, Medium, Heavy, Parry, Roll, Explosion, Death }

    /// <summary>
    /// Rumble and vibration. A real gamepad gets motor speeds (low and high frequency) for a short time; a phone or tablet in the
    /// browser gets <c>navigator.vibrate</c> (Android) or a tick on touch (iOS); native phones use the system vibrator.
    /// Strength and an on/off switch come from the settings. Game code only says what happened (<see cref="HapticKind"/>).
    /// </summary>
    public static class Haptics
    {
        struct Preset
        {
            public float Low, High, Seconds;
            public int Millis;
        }

        static readonly Preset[] Presets =
        {
            new Preset { Low = 0.00f, High = 0.30f, Seconds = 0.06f, Millis = 14 },   // Light: you landed a hit
            new Preset { Low = 0.35f, High = 0.55f, Seconds = 0.10f, Millis = 30 },   // Medium
            new Preset { Low = 0.90f, High = 0.80f, Seconds = 0.22f, Millis = 70 },   // Heavy: you took a big hit
            new Preset { Low = 0.15f, High = 1.00f, Seconds = 0.12f, Millis = 40 },   // Parry: a sharp snap
            new Preset { Low = 0.10f, High = 0.10f, Seconds = 0.05f, Millis = 8 },    // Roll
            new Preset { Low = 1.00f, High = 0.60f, Seconds = 0.32f, Millis = 90 },   // Explosion
            new Preset { Low = 0.70f, High = 0.50f, Seconds = 0.65f, Millis = 220 },  // Death
        };

        /// <summary>Tests (and anything else that wants to listen) see every request with its final strength.</summary>
        public static Action<HapticKind, float> Observer;

        static Runner _runner;
        static float _stopAt;
        static Gamepad _active;

        public static bool Enabled => SettingsService.Current.haptics && SettingsService.Current.hapticStrength > 0.01f;

        public static void Play(HapticKind kind)
        {
            if (!Enabled) return;
            float strength = SettingsService.Current.hapticStrength;
            Observer?.Invoke(kind, strength);

            Preset p = Presets[(int)kind];

            // 1. a real controller
            Gamepad pad = Gamepad.current;
            if (pad != null && !pad.description.empty && InputDevices.Scheme == InputScheme.Gamepad)
            {
                pad.SetMotorSpeeds(Mathf.Clamp01(p.Low * strength), Mathf.Clamp01(p.High * strength));
                _active = pad;
                _stopAt = Time.unscaledTime + p.Seconds;
                EnsureRunner();
                return;
            }

            // 2. a phone or tablet
            if (InputDevices.Scheme == InputScheme.Touch || Application.isMobilePlatform)
            {
                int ms = Mathf.Max(5, Mathf.RoundToInt(p.Millis * (0.4f + strength)));
#if UNITY_ANDROID || UNITY_IOS
                if (!WebBridge.IsWebGL) { Handheld.Vibrate(); return; }
#endif
                WebBridge.Vibrate(ms);
            }
        }

        /// <summary>Stops any rumble that is still running (pausing, quitting, changing scene).</summary>
        public static void StopAll()
        {
            if (_active != null) { _active.ResetHaptics(); _active = null; }
            _stopAt = 0f;
        }

        /// <summary>Tells the browser page whether to tick on touches (the only haptic iOS Safari allows).</summary>
        public static void RefreshTouchHaptics() => WebBridge.SetTouchHaptics(Enabled && WebBridge.IsIOSBrowser);

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("Haptics") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (_active != null && Time.unscaledTime >= _stopAt) StopAll();
            }

            void OnApplicationFocus(bool focus) { if (!focus) StopAll(); }
            void OnDestroy() => StopAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _runner = null;
            _active = null;
            _stopAt = 0f;
            Observer = null;
        }
    }
}
