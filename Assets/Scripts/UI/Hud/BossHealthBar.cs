using UnityEngine;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Enemies;

namespace Roughlike.UI
{
    /// <summary>The big health bar across the top while a boss fights: its name, a draining bar with a pale trailing damage, and a tick at each phase.</summary>
    public sealed class BossHealthBar : MonoBehaviour
    {
        BossController _boss;
        RectTransform _panel, _fill, _lag;
        Text _name;
        float _lagValue = 1f;
        readonly System.Collections.Generic.List<Image> _ticks = new System.Collections.Generic.List<Image>();

        public bool Showing => _panel != null && _panel.gameObject.activeSelf;

        public static BossHealthBar Create(UiRoot root)
        {
            var go = new GameObject("Boss Health Bar");
            go.transform.SetParent(root.HudLayer, false);
            var bar = go.AddComponent<BossHealthBar>();
            bar.Build();
            return bar;
        }

        void Build()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1000f, 90f));
            // the visuals live in a child so hiding the bar never disables this component (it must keep listening for the next boss)
            _panel = UiKit.Node("Panel", rt);
            UiKit.Stretch(_panel);

            _name = UiKit.Label(_panel, "Name", "", 34, new Color(1f, 0.82f, 0.6f), TextAnchor.UpperCenter, true);
            UiKit.Place(_name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 42f));
            var shadow = _name.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var frame = UiKit.Image(_panel, "Frame", new Color(0.78f, 0.58f, 0.25f, 1f));
            UiKit.Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(0f, 30f));
            var back = UiKit.Image(frame.transform, "Back", new Color(0.07f, 0.05f, 0.07f, 0.96f));
            UiKit.Stretch(back.rectTransform, 3f);

            _lag = Bar(back.transform, "Lag", new Color(1f, 0.92f, 0.85f, 0.85f));
            _fill = Bar(back.transform, "Fill", new Color(0.82f, 0.14f, 0.16f, 1f));
            _panel.gameObject.SetActive(false);
        }

        static RectTransform Bar(Transform parent, string name, Color colour)
        {
            var img = UiKit.Image(parent, name, colour);
            var r = img.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        void OnEnable() => EventBus<BossSpawnedEvent>.Subscribe(OnBoss);
        void OnDisable() => EventBus<BossSpawnedEvent>.Unsubscribe(OnBoss);

        void OnBoss(BossSpawnedEvent e)
        {
            _boss = e.Boss;
            _name.text = _boss.Definition.displayName;
            _lagValue = 1f;
            SetFill(_fill, 1f);
            SetFill(_lag, 1f);

            foreach (var t in _ticks) if (t != null) Destroy(t.gameObject);
            _ticks.Clear();
            var back = _fill.parent;
            foreach (float threshold in _boss.Definition.phaseThresholds)
            {
                var tick = UiKit.Image(back, "Phase", new Color(1f, 1f, 1f, 0.8f));
                var r = tick.rectTransform;
                r.anchorMin = new Vector2(threshold, 0f);
                r.anchorMax = new Vector2(threshold, 1f);
                r.sizeDelta = new Vector2(3f, 0f);
                _ticks.Add(tick);
            }
            _panel.gameObject.SetActive(true);
        }

        static void SetFill(RectTransform r, float value) => r.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);

        void Update()
        {
            if (_boss == null) return;
            if (_boss.Dead || _boss.Health == null)
            {
                if (_panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
                _boss = null;
                return;
            }
            float f = _boss.Health.Fraction;
            SetFill(_fill, f);
            _lagValue = Mathf.MoveTowards(_lagValue, f, Time.unscaledDeltaTime * 0.35f);
            if (_lagValue < f) _lagValue = f;
            SetFill(_lag, _lagValue);
        }
    }
}
