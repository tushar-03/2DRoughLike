using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Rooms
{
    /// <summary>
    /// Dev HUD for runs: seed code, current room, stats, a fog-of-war minimap of the generated room graph,
    /// a seed entry box (F11) and the end-of-run panel. Replaced by the real uGUI HUD later (M4+); the minimap
    /// doubles as the level-generator visualiser (F12 reveals the whole graph). Compiled out of release builds.
    /// </summary>
    public class RunHud : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] RunManager run;
        [SerializeField] Health playerHealth;

        bool _showSeedBox, _revealAll;
        string _seedText = "";
        GUIStyle _label, _big, _small;

        public void Bind(RunManager manager, Health health) { run = manager; playerHealth = health; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f11Key.wasPressedThisFrame) { _showSeedBox = !_showSeedBox; _seedText = run != null && run.Seed != 0 ? SeedCode.Encode(run.Seed) : ""; }
            if (kb.f12Key.wasPressedThisFrame) _revealAll = !_revealAll;
        }

        void OnGUI()
        {
            if (run == null || run.Layout == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.85f) } };
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            DrawTopBar();
            DrawMinimap(10f, 160f);
            if (_showSeedBox) DrawSeedBox();
            if (run.EndScreenVisible) DrawEndScreen();
        }

        void DrawTopBar()
        {
            float w = 560f;
            Rect r = new Rect((Screen.width - w) * 0.5f, 8f, w, 52f);
            GUI.Box(r, GUIContent.none);
            var s = run.Stats;
            string role = run.Layout.TryGet(run.CurrentRoom, out var cell) ? cell.Role.ToString() : "-";
            GUI.Label(new Rect(r.x + 8, r.y + 3, w - 16, 24),
                $"{run.Biome.displayName}   Seed {SeedCode.Encode(run.Seed)}   Room {run.CurrentRoom.x},{run.CurrentRoom.y} ({role})", _label);
            GUI.Label(new Rect(r.x + 8, r.y + 27, w - 16, 22),
                $"Kills {s.Kills}   Rooms {s.RoomsVisited}/{run.Layout.Cells.Count}   Time {(int)s.Time / 60}:{(int)s.Time % 60:00}   Damage taken {s.DamageTaken:0}   [F9] new  [F10] retry  [F11] seed  [F12] map", _small);
        }

        void DrawMinimap(float x, float y)
        {
            const float cell = 24f, gap = 3f;
            var layout = run.Layout;
            int maxY = 0, maxX = 0;
            foreach (var c in layout.Cells) { maxY = Mathf.Max(maxY, c.Pos.y); maxX = Mathf.Max(maxX, c.Pos.x); }

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, (maxX + 1) * cell + 12, (maxY + 1) * cell + 12), Texture2D.whiteTexture);

            foreach (var (a, b) in layout.Edges)
            {
                bool known = _revealAll || (run.Visited.Contains(a) && run.Visited.Contains(b)) ||
                             run.Visited.Contains(a) || run.Visited.Contains(b);
                if (!known) continue;
                Rect ra = CellRect(x, y, cell, maxY, a), rb = CellRect(x, y, cell, maxY, b);
                Vector2 mid = (ra.center + rb.center) * 0.5f;
                GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                GUI.DrawTexture(new Rect(mid.x - cell * 0.28f, mid.y - 2f, cell * 0.56f, 4f), Texture2D.whiteTexture);
                if (a.x == b.x) GUI.DrawTexture(new Rect(mid.x - 2f, mid.y - cell * 0.28f, 4f, cell * 0.56f), Texture2D.whiteTexture);
            }

            foreach (var c in layout.Cells)
            {
                bool visited = run.Visited.Contains(c.Pos);
                bool adjacent = false;
                foreach (var (a, b) in layout.Edges)
                    if ((a == c.Pos && run.Visited.Contains(b)) || (b == c.Pos && run.Visited.Contains(a))) adjacent = true;
                if (!_revealAll && !visited && !adjacent) continue;

                Rect r = CellRect(x, y, cell, maxY, c.Pos);
                r = new Rect(r.x + gap * 0.5f, r.y + gap * 0.5f, r.width - gap, r.height - gap);
                Color col = visited ? new Color(0.45f, 0.5f, 0.65f) : new Color(0.25f, 0.27f, 0.33f);
                if (c.Role == RoomRole.Start) col = visited ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.2f, 0.4f, 0.25f);
                if (c.Role == RoomRole.Exit) col = new Color(0.9f, 0.4f, 0.35f);
                if (c.Role == RoomRole.Treasure) col = new Color(0.95f, 0.8f, 0.3f);
                if (c.Pos == run.CurrentRoom) col = Color.white;
                GUI.color = col;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        static Rect CellRect(float x, float y, float cell, int maxY, Vector2Int pos)
            => new Rect(x + pos.x * cell, y + (maxY - pos.y) * cell, cell, cell);

        void DrawSeedBox()
        {
            Rect r = new Rect(Screen.width * 0.5f - 190f, 70f, 380f, 92f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 10, r.y + 4, 360, 22), "Seed (code or any text) and press Start:", _label);
            _seedText = GUI.TextField(new Rect(r.x + 10, r.y + 30, 250, 26), _seedText, 24);
            if (GUI.Button(new Rect(r.x + 270, r.y + 30, 100, 26), "Start run"))
            {
                run.StartRunFromText(_seedText);
                _showSeedBox = false;
            }
            GUI.Label(new Rect(r.x + 10, r.y + 60, 360, 24), "Same seed = same level layout and enemy placement.", _small);
        }

        void DrawEndScreen()
        {
            bool dead = run.State == RunState.Dead;
            Rect panel = new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 150f, 520f, 300f);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _big.normal.textColor = dead ? new Color(1f, 0.45f, 0.4f) : new Color(0.5f, 1f, 0.7f);
            GUI.Label(new Rect(panel.x, panel.y + 12, panel.width, 50), dead ? "YOU DIED" : "BIOME CLEARED", _big);

            var s = run.Stats;
            string summary =
                $"Seed {SeedCode.Encode(run.Seed)}\n" +
                $"Rooms explored  {s.RoomsVisited} / {run.Layout.Cells.Count}\n" +
                $"Enemies slain  {s.Kills}\n" +
                $"Damage taken  {s.DamageTaken:0}\n" +
                "Gold  " + s.Gold + "     Cells  " + s.Cells + (s.CellsBanked ? " (banked)" : " (lost)") + "\n" +
                $"Time  {(int)s.Time / 60}:{(int)s.Time % 60:00}";
            GUI.Label(new Rect(panel.x + 90, panel.y + 80, panel.width - 120, 130), summary, _label);

            if (!dead)
                GUI.Label(new Rect(panel.x + 20, panel.y + 205, panel.width - 40, 24), "(The next biome and Cell banking arrive in later milestones.)", _small);

            GUI.Label(new Rect(panel.x + 20, panel.y + 240, panel.width - 40, 48),
                "[R] new run, new seed     [T] retry this seed     [M] main menu\n(permadeath: weapons, health and progress reset)", _label);
        }
#endif
    }
}

