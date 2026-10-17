using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Combat;
using Roughlike.Player;

namespace Roughlike.Enemies
{
    /// <summary>
    /// Dev-only combat sandbox: player health and loadout readout, weapon cycling, enemy spawning, god mode.
    /// Replaced by the real HUD (M4) and debug console (later). Compiled out of release builds.
    /// </summary>
    public class CombatSandboxOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] PlayerCombat combat;
        [SerializeField] PlayerController player;
        [SerializeField] WeaponData[] weaponLibrary;
        [SerializeField] EnemyDefinition[] spawnables;

        Health _health;
        PlayerDefense _defense;
        GUIStyle _label, _small;
        readonly int[] _cycle = new int[2];

        public void Bind(PlayerCombat c, PlayerController p, WeaponData[] weapons, EnemyDefinition[] enemies)
        {
            combat = c; player = p; weaponLibrary = weapons; spawnables = enemies;
        }

        void Start()
        {
            if (combat == null) return;
            _health = combat.GetComponent<Health>();
            _defense = combat.GetComponent<PlayerDefense>();
            for (int s = 0; s < 2; s++) _cycle[s] = System.Array.IndexOf(weaponLibrary, combat.GetWeapon(s));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || combat == null) return;

            if (kb.f4Key.wasPressedThisFrame) CycleWeapon(0);
            if (kb.f5Key.wasPressedThisFrame) CycleWeapon(1);
            if (kb.f6Key.wasPressedThisFrame && spawnables.Length > 0) Spawn(spawnables[0]);
            if (kb.f7Key.wasPressedThisFrame && spawnables.Length > 1) Spawn(spawnables[1]);
            if (kb.f8Key.wasPressedThisFrame && _defense != null) _defense.GodMode = !_defense.GodMode;
        }

        void CycleWeapon(int slot)
        {
            if (weaponLibrary == null || weaponLibrary.Length == 0) return;
            _cycle[slot] = (_cycle[slot] + 1) % weaponLibrary.Length;
            combat.SetWeapon(slot, weaponLibrary[_cycle[slot]]);
        }

        void Spawn(EnemyDefinition def)
        {
            Vector2 pos = (Vector2)player.transform.position + new Vector2(player.Facing * 9f, 0.5f);
            EnemyFactory.Build(def, pos);
        }

        void OnGUI()
        {
            if (combat == null || _health == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };

            float w = 420f, x = Screen.width - w - 8f, y = 8f;
            GUI.Box(new Rect(x, y, w, 176f), GUIContent.none);

            // health bar
            Rect bar = new Rect(x + 10, y + 10, w - 20, 22);
            GUI.color = new Color(0.15f, 0.05f, 0.05f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.2f, 0.2f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * _health.Fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x + 6, bar.y - 1, bar.width, 24), $"HP {_health.Current:0}/{_health.Max:0}" + (_defense != null && _defense.GodMode ? "  GOD" : ""), _label);

            GUI.Label(new Rect(x + 10, y + 38, w - 20, 60), combat.DebugSummary(), _label);
            GUI.Label(new Rect(x + 10, y + 98, w - 20, 80),
                "J / LMB / X: slot 1    K / RMB / Y: slot 2    L / Shift / B: roll\n" +
                "Shield: tap = parry window, hold = block\n" +
                "[F4/F5] cycle weapon  [F6] grunt  [F7] shooter  [F8] god mode", _small);
        }
#endif
    }
}
