using UnityEngine;
using Roughlike.Combat;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>Adds the item layer (inventory, stats, skills, mutations, interaction, flask) to a combat-ready player.</summary>
    public static class PlayerItemsSetup
    {
        public static PlayerInventory Attach(PlayerController player)
        {
            GameObject go = player.gameObject;
            bool wasActive = go.activeSelf;
            go.SetActive(false);   // wire everything before Awake/OnEnable run

            var inventory = go.AddComponent<PlayerInventory>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<MutationRunner>();
            go.AddComponent<SkillRunner>();
            go.AddComponent<InteractionSensor>();
            go.AddComponent<FlaskUser>();

            go.SetActive(wasActive);
            return inventory;
        }

        /// <summary>For tests: stop every item component from ticking itself so the test drives them.</summary>
        public static void DisableAutoTick(GameObject player)
        {
            var mr = player.GetComponent<MutationRunner>(); if (mr != null) mr.autoTick = false;
            var sr = player.GetComponent<SkillRunner>(); if (sr != null) sr.autoTick = false;
            var isn = player.GetComponent<InteractionSensor>(); if (isn != null) isn.autoTick = false;
            var fu = player.GetComponent<FlaskUser>(); if (fu != null) fu.autoTick = false;
            var se = player.GetComponent<StatusEffects>(); if (se != null) se.autoTick = false;
        }
    }
}

