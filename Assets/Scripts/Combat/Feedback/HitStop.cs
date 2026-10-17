using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>
    /// Freeze-frame on impact. Sets Time.timeScale to 0 for a few frames, restored by a hidden runner using
    /// unscaled time. Accessibility: <see cref="Scale"/> 0 disables it.
    /// </summary>
    public static class HitStop
    {
        const float MaxDuration = 0.18f;

        public static float Scale = 1f;
        public static float BaseTimeScale = 1f;

        static Runner _runner;
        static float _remaining;

        public static bool Active => _remaining > 0f;

        public static void Request(float seconds)
        {
            seconds = Mathf.Min(seconds * Scale, MaxDuration);
            if (seconds <= 0f) return;

            if (_runner == null)
            {
                var go = new GameObject("[HitStop]") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            _remaining = Mathf.Max(_remaining, seconds);
            Time.timeScale = 0f;
        }

        /// <summary>Abort immediately (scene change, pause).</summary>
        public static void Cancel()
        {
            _remaining = 0f;
            Time.timeScale = BaseTimeScale;
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (_remaining <= 0f) return;
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    _remaining = 0f;
                    Time.timeScale = BaseTimeScale;
                }
            }
        }
    }
}
