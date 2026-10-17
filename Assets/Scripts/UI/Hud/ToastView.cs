using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Items;

namespace Roughlike.UI
{
    /// <summary>Short messages at the top of the screen ("+1 Ember scroll", an item's name when you pick it up).</summary>
    public sealed class ToastView : MonoBehaviour
    {
        const float Life = 2.4f;
        const int MaxVisible = 5;

        sealed class Toast { public Text Text; public float Age; }

        readonly List<Toast> _toasts = new List<Toast>();
        RectTransform _container;

        public static ToastView Create(UiRoot root)
        {
            var go = new GameObject("Toasts", typeof(RectTransform));
            go.transform.SetParent(root.HudLayer, false);
            var v = go.AddComponent<ToastView>();
            v._container = (RectTransform)go.transform;
            UiKit.Place(v._container, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 300f));
            return v;
        }

        void OnEnable() => EventBus<ToastEvent>.Subscribe(OnToast);
        void OnDisable() => EventBus<ToastEvent>.Unsubscribe(OnToast);

        void OnToast(ToastEvent e)
        {
            if (_container == null) return;
            var t = UiKit.Label(_container, "Toast", e.Text, 28, e.Colour, TextAnchor.UpperCenter, true);
            t.rectTransform.sizeDelta = new Vector2(700f, 34f);
            _toasts.Add(new Toast { Text = t });
            while (_toasts.Count > MaxVisible)
            {
                Destroy(_toasts[0].Text.gameObject);
                _toasts.RemoveAt(0);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                Toast t = _toasts[i];
                t.Age += dt;
                if (t.Age >= Life)
                {
                    Destroy(t.Text.gameObject);
                    _toasts.RemoveAt(i);
                    continue;
                }
                float fade = Mathf.Clamp01((Life - t.Age) / 0.6f);
                Color c = t.Text.color; c.a = fade; t.Text.color = c;
            }

            // newest at the top
            for (int i = 0; i < _toasts.Count; i++)
            {
                int fromTop = _toasts.Count - 1 - i;
                var rt = _toasts[i].Text.rectTransform;
                UiKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -fromTop * 36f), new Vector2(700f, 34f));
            }
        }
    }
}

