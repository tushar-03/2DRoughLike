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

        bool _showSeedBox;
        string _seedText = "";
        GUIStyle _label, _small;

        public void Bind(RunManager manager, Health health) { run = manager; playerHealth = health; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f11Key.wasPressedThisFrame) { _showSeedBox = !_showSeedBox; _seedText = run != null && run.Seed != 0 ? SeedCode.Encode(run.Seed) : ""; }
            if (kb.f12Key.wasPressedThisFrame) RunManager.RevealMap = !RunManager.RevealMap;
        }

        void OnGUI()
        {
            if (run == null || run.Layout == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.85f) } };
            if (_showSeedBox) DrawSeedBox();
        }

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
#endif
    }
}

