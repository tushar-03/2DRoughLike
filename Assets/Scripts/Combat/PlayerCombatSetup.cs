using UnityEngine;
using Roughlike.Player;

namespace Roughlike.Combat
{
    /// <summary>Adds the combat layer (health, defense, weapons, hit feedback) to a built player. Used by the prefab builder and tests.</summary>
    public static class PlayerCombatSetup
    {
        public static PlayerCombat Attach(PlayerController player, WeaponData slot1, WeaponData slot2, float maxHealth = 100f)
        {
            GameObject go = player.gameObject;
            bool wasActive = go.activeSelf;
            go.SetActive(false);   // so Awake/OnEnable see the final wiring

            var health = go.AddComponent<Health>();
            health.Initialize(Team.Player, maxHealth);
            go.AddComponent<PlayerDefense>();
            var combat = go.AddComponent<PlayerCombat>();
            SetWeapons(combat, slot1, slot2);

            var visual = go.GetComponentInChildren<PlayerVisual>();
            if (visual != null)
            {
                var sr = visual.GetComponent<SpriteRenderer>();
                var react = visual.gameObject.AddComponent<HitReactionVisual>();
                react.Bind(sr, health, false);
            }

            go.SetActive(wasActive);
            return combat;
        }

        /// <summary>Sets the serialized loadout (before Awake) or the live one.</summary>
        static void SetWeapons(PlayerCombat combat, WeaponData a, WeaponData b)
        {
            combat.SetWeaponsUnsafe(a, b);
        }
    }
}
