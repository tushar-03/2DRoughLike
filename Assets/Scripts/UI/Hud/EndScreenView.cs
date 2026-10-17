using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>
    /// The run-over panel (YOU DIED / BIOME CLEARED) with real buttons, so it works with a mouse, a gamepad and a finger and exists
    /// in release builds. R, T and M still work as keyboard shortcuts (see <see cref="RunManager"/>).
    /// </summary>
    public sealed class EndScreenView : MonoBehaviour
    {
        RunManager _run;
        UiRoot _root;
        GameObject _screen;

        public bool Showing => _screen != null;

        public static EndScreenView Create(UiRoot root, RunManager run)
        {
            var go = new GameObject("End Screen");
            go.transform.SetParent(root.transform, false);
            var v = go.AddComponent<EndScreenView>();
            v._root = root;
            v._run = run;
            return v;
        }

        void Update()
        {
            if (_run == null) return;
            bool want = _run.EndScreenVisible;
            if (want && _screen == null) Build();
            else if (!want && _screen != null) Hide();
        }

        void Hide()
        {
            _screen.SetActive(false);
            Destroy(_screen);
            _screen = null;
        }

        void Build()
        {
            bool dead = _run.State == RunState.Dead;

            var dim = UiKit.Image(_root.ModalLayer, "End Screen", new Color(0f, 0f, 0f, 0.78f));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            _screen = dim.gameObject;

            var size = new Vector2(760f, 700f);
            RectTransform inner = UiKit.Panel(dim.transform, "Window", size);
            UiKit.Place((RectTransform)inner.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            var title = UiKit.Label(inner, "Title", dead ? "YOU DIED" : "BIOME CLEARED", 56, dead ? new Color(1f, 0.45f, 0.4f) : new Color(0.5f, 1f, 0.7f), TextAnchor.MiddleCenter, true);
            UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-30f, 70f));

            var s = _run.Stats;
            string summary =
                $"Seed  {SeedCode.Encode(_run.Seed)}\n" +
                $"Rooms explored  {s.RoomsVisited} / {_run.Layout.Cells.Count}\n" +
                $"Enemies slain  {s.Kills}\n" +
                $"Damage taken  {s.DamageTaken:0}\n" +
                $"Gold  {s.Gold}     Cells  {s.Cells}{(s.CellsBanked ? " (banked)" : " (lost)")}\n" +
                $"Time  {(int)s.Time / 60}:{(int)s.Time % 60:00}";
            var text = UiKit.Label(inner, "Summary", summary, 30, Color.white, TextAnchor.UpperCenter);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(-60f, 230f));

            Button first = Add(inner, "New run  (new seed)", "R", 0, () => _run.StartRandomRun());
            Add(inner, "Retry this seed", "T", 1, () => _run.RestartSameSeed());
            Add(inner, "Main menu", "M", 2, () => ModalManager.LoadScene(GameSession.MenuScene));

            var note = UiKit.Label(inner, "Note", "Permadeath: weapons, health and progress reset with a new run.", 20, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-40f, 30f));

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        static Button Add(RectTransform parent, string label, string key, int index, UnityEngine.Events.UnityAction onClick)
        {
            var b = MenuKit.ListButton(parent, label, null, new Vector2(520f, 76f), onClick, 32);
            UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 262f - index * 90f), new Vector2(520f, 76f));
            return b;
        }
    }
}
