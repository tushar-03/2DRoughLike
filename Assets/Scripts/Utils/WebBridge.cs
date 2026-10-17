using System.Runtime.InteropServices;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// The few things a browser build needs from JavaScript: vibration on phones, "is this a touch device", and flushing saved
    /// files to the browser's IndexedDB. Everywhere else (editor, desktop, native phones) every call is a harmless no-op or uses
    /// the platform's own API. The JavaScript side is <c>Assets/Plugins/WebGL/Roughlike.jslib</c>.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Roughlike_Vibrate(int milliseconds);
        [DllImport("__Internal")] static extern int Roughlike_IsTouchDevice();
        [DllImport("__Internal")] static extern int Roughlike_IsIOS();
        [DllImport("__Internal")] static extern void Roughlike_SyncFS();
        [DllImport("__Internal")] static extern void Roughlike_SetTouchHaptics(int enabled);
#endif

        public static bool IsWebGL => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>True when the browser reports a coarse pointer (finger) as the primary input: phones and tablets.</summary>
        public static bool IsTouchDevice
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return Roughlike_IsTouchDevice() != 0; } catch { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>iPhone, iPad or iPod in a browser (they have no Vibration API).</summary>
        public static bool IsIOSBrowser
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return Roughlike_IsIOS() != 0; } catch { return false; }
#else
                return false;
#endif
            }
        }

        public static void Vibrate(int milliseconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Roughlike_Vibrate(milliseconds); } catch { }
#endif
        }

        /// <summary>On iOS Safari the only haptic available is a tick on a real touch, so the page fires one on every touch while this is on.</summary>
        public static void SetTouchHaptics(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Roughlike_SetTouchHaptics(enabled ? 1 : 0); } catch { }
#endif
        }

        /// <summary>Writes pending file changes to IndexedDB so saves survive closing the tab. Call after every save.</summary>
        public static void SyncFileSystem()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Roughlike_SyncFS(); } catch { }
#endif
        }
    }
}
