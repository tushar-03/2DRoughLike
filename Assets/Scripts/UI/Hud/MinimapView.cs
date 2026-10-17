using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Roughlike.Core;
using Roughlike.Rooms;

namespace Roughlike.UI
{
    /// <summary>
    /// Fog-of-war map of the generated rooms plus the run's seed and the room you are in. Drawn with uGUI so it exists in release
    /// builds (the old IMGUI one was editor-only). Rebuilds when a new level starts and recolours when you change room.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        const float Cell = 26f, Gap = 4f;

        RunManager _run;
        RectTransform _map;
        Text _label;
        LevelLayout _built;
        int _visitedCount = -1;
        Vector2Int _room = new Vector2Int(int.MinValue, 0);
        bool _lastReveal;
        readonly List<Image> _edgeImages = new List<Image>();
        readonly Dictionary<Vector2Int, Image> _cells = new Dictionary<Vector2Int, Image>();
        readonly List<(Vector2Int a, Vector2Int b, Image img)> _edges = new List<(Vector2Int, Vector2Int, Image)>();

        public static MinimapView Create(UiRoot root, RunManager run)
        {
            var go = new GameObject("Minimap");
            go.transform.SetParent(root.HudLayer, false);
            var v = go.AddComponent<MinimapView>();
            v._run = run;
            v.Setup();
            return v;
        }

        void Setup()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -150f), new Vector2(400f, 220f));

            _label = UiKit.Label(rt, "Seed", "", 20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperLeft);
            UiKit.Place(_label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 54f));
            _map = UiKit.Node("Map", rt);
            UiKit.Place(_map, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -58f), new Vector2(380f, 180f));
        }

        void Update()
        {
            if (_run == null || _run.Layout == null) return;
            if (_built != _run.Layout) Rebuild();

            if (_run.CurrentRoom == _room && _run.Visited.Count == _visitedCount && RunManager.RevealMap == _lastReveal) return;
            _room = _run.CurrentRoom;
            _visitedCount = _run.Visited.Count;
            _lastReveal = RunManager.RevealMap;
            Refresh();
        }

        void Rebuild()
        {
            foreach (Transform c in _map) Destroy(c.gameObject);
            _cells.Clear();
            _edges.Clear();
            _built = _run.Layout;

            int maxY = 0;
            foreach (var c in _built.Cells) maxY = Mathf.Max(maxY, c.Pos.y);

            foreach (var (a, b) in _built.Edges)
            {
                var img = UiKit.Image(_map, "Edge", new Color(0.8f, 0.8f, 0.8f, 0.9f));
                Vector2 pa = CellCentre(a, maxY), pb = CellCentre(b, maxY);
                Vector2 mid = (pa + pb) * 0.5f;
                bool vertical = a.x == b.x;
                UiKit.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(mid.x, -mid.y),
                    vertical ? new Vector2(4f, Cell * 0.7f) : new Vector2(Cell * 0.7f, 4f));
                _edges.Add((a, b, img));
            }
            foreach (var c in _built.Cells)
            {
                var img = UiKit.Image(_map, "Room", Color.white);
                Vector2 p = CellCentre(c.Pos, maxY);
                UiKit.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(p.x, -p.y), new Vector2(Cell - Gap, Cell - Gap));
                _cells[c.Pos] = img;
            }
            _visitedCount = -1;
        }

        static Vector2 CellCentre(Vector2Int pos, int maxY) => new Vector2(pos.x * Cell + Cell * 0.5f, (maxY - pos.y) * Cell + Cell * 0.5f);

        void Refresh()
        {
            _label.text = $"{_run.Biome.displayName}  -  {DifficultyService.Name(_run.Difficulty)}\nSeed {SeedCode.Encode(_run.Seed)}   Room {_run.CurrentRoom.x},{_run.CurrentRoom.y}";

            foreach (var e in _edges)
            {
                bool known = RunManager.RevealMap || _run.Visited.Contains(e.a) || _run.Visited.Contains(e.b);
                e.img.gameObject.SetActive(known);
            }

            foreach (var c in _built.Cells)
            {
                var img = _cells[c.Pos];
                bool visited = _run.Visited.Contains(c.Pos);
                bool adjacent = false;
                foreach (var (a, b) in _built.Edges)
                    if ((a == c.Pos && _run.Visited.Contains(b)) || (b == c.Pos && _run.Visited.Contains(a))) { adjacent = true; break; }
                img.gameObject.SetActive(RunManager.RevealMap || visited || adjacent);

                Color col = visited ? new Color(0.45f, 0.5f, 0.65f) : new Color(0.25f, 0.27f, 0.33f);
                if (c.Role == RoomRole.Start) col = visited ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.2f, 0.4f, 0.25f);
                if (c.Role == RoomRole.Exit) col = new Color(0.9f, 0.4f, 0.35f);
                if (c.Role == RoomRole.Treasure) col = new Color(0.95f, 0.8f, 0.3f);
                if (c.Pos == _run.CurrentRoom) col = Color.white;
                img.color = col;
            }
        }
    }
}
