using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roughlike.UI
{
    /// <summary>
    /// Keeps the control that has keyboard or gamepad focus inside a scrolling list, so navigating with a stick or the arrow keys
    /// never leaves the selection off-screen.
    /// </summary>
    public sealed class ScrollToSelected : MonoBehaviour
    {
        ScrollRect _scroll;
        RectTransform _viewport, _content;
        GameObject _last;

        public static void Attach(ScrollRect scroll)
        {
            var s = scroll.gameObject.AddComponent<ScrollToSelected>();
            s._scroll = scroll;
            s._viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            s._content = scroll.content;
        }

        void Update()
        {
            if (EventSystem.current == null || _content == null) return;
            GameObject sel = EventSystem.current.currentSelectedGameObject;
            if (sel == null || sel == _last || !sel.transform.IsChildOf(_content)) return;
            _last = sel;

            // the child of the content that holds the selection (a whole row)
            Transform t = sel.transform;
            while (t.parent != _content) t = t.parent;
            var row = (RectTransform)t;

            float rowTop = -row.anchoredPosition.y;                 // distances measured downward from the top of the content
            float rowBottom = rowTop + row.rect.height;
            float view = _viewport.rect.height;
            float offset = _content.anchoredPosition.y;
            if (rowTop < offset) offset = rowTop - 8f;
            else if (rowBottom > offset + view) offset = rowBottom - view + 8f;
            float max = Mathf.Max(0f, _content.rect.height - view);
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, Mathf.Clamp(offset, 0f, max));
        }
    }
}
