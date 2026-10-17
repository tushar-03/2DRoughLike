using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>The tutorial's instruction panel: step number, title, what to press for the device in use, and progress dots.</summary>
    public sealed class TutorialHud : MonoBehaviour
    {
        TutorialDirector _director;
        Text _counter, _title, _body;
        readonly List<Image> _dots = new List<Image>();
        RectTransform _panel;
        float _pulse;
        string _lastBody;

        public static TutorialHud Create(UiRoot root, TutorialDirector director)
        {
            var go = new GameObject("Tutorial HUD");
            go.transform.SetParent(root.HudLayer, false);
            var h = go.AddComponent<TutorialHud>();
            h._director = director;
            h.Build();
            return h;
        }

        void Build()
        {
            var size = new Vector2(840f, 215f);
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), size);
            _panel = rt;

            RectTransform inner = UiKit.Panel(rt, "Panel", size);
            var edge = (RectTransform)inner.parent;
            UiKit.Stretch(edge);

            _counter = UiKit.Label(inner, "Counter", "", 20, UiKit.Gold, TextAnchor.UpperLeft, true);
            UiKit.Place(_counter.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(300f, 28f));
            _title = UiKit.Label(inner, "Title", "", 40, Color.white, TextAnchor.UpperLeft, true);
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -34f), new Vector2(-36f, 50f));
            _body = UiKit.Label(inner, "Body", "", 25, new Color(0.9f, 0.93f, 1f), TextAnchor.UpperLeft);
            UiKit.Place(_body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _body.rectTransform.offsetMin = new Vector2(18f, 12f);
            _body.rectTransform.offsetMax = new Vector2(-18f, -84f);

            _dotsRoot = UiKit.Node("Dots", inner);
            UiKit.Place(_dotsRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(300f, 20f));
        }

        RectTransform _dotsRoot;

        /// <summary>One dot per step; rebuilt if the director finished building its steps after this HUD appeared.</summary>
        void BuildDots()
        {
            foreach (var d in _dots) if (d != null) Destroy(d.gameObject);
            _dots.Clear();
            int n = _director.Steps.Count;
            for (int i = 0; i < n; i++)
            {
                var d = UiKit.Image(_dotsRoot, "Dot", new Color(1f, 1f, 1f, 0.25f), Roughlike.Utils.ProceduralSprites.Circle);
                UiKit.Place(d.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(n - 1 - i) * 22f, 0f), new Vector2(14f, 14f));
                _dots.Add(d);
            }
        }

        void OnEnable() { if (_director != null) _director.StepChanged += OnStepChanged; }
        void OnDisable() { if (_director != null) _director.StepChanged -= OnStepChanged; }
        void OnStepChanged() => _pulse = 1f;

        void Update()
        {
            if (_director == null) return;
            var step = _director.Current;
            if (step == null) return;

            int total = _director.Steps.Count;
            if (_dots.Count != total) BuildDots();
            _counter.text = _director.Finished ? "TUTORIAL COMPLETE" : $"TUTORIAL   {_director.Index + 1} / {total}";
            _title.text = step.Title;
            string body = step.Body();
            if (body != _lastBody) { _body.text = body; _lastBody = body; }

            for (int i = 0; i < _dots.Count; i++)
            {
                Color c = i < _director.Index || _director.Finished ? new Color(0.5f, 1f, 0.7f, 0.95f)
                        : i == _director.Index ? UiKit.Gold : new Color(1f, 1f, 1f, 0.25f);
                _dots[i].color = c;
            }

            // a little pop when a new step appears
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.unscaledDeltaTime * 3f);
            float k = 1f + 0.05f * _pulse * _pulse;
            _panel.localScale = new Vector3(k, k, 1f);
        }
    }
}
