using UnityEngine;
using UnityEngine.EventSystems;
using Roughlike.Utils;

namespace Roughlike.UI
{
    /// <summary>Ticks whenever the highlighted menu item changes (keyboard, controller or touch navigation).</summary>
    public sealed class UiSounds : MonoBehaviour
    {
        GameObject _last;
        float _quietUntil;

        /// <summary>Silences the next few moves (a menu that has just opened selects its first item).</summary>
        public void Quiet(float seconds = 0.15f) => _quietUntil = Time.unscaledTime + seconds;

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var now = es.currentSelectedGameObject;
            if (now != _last)
            {
                if (now != null && _last != null && Time.unscaledTime > _quietUntil) Sfx.Play(SfxId.UiMove, 0.5f, 1f, 0.03f);
                _last = now;
            }
        }
    }
}
