using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Items
{
    /// <summary>
    /// Everything the player carries for the current run: two weapon slots, two skill slots, an amulet, an outfit,
    /// up to three mutations, scroll levels, gold, Cells and flask charges. Permadeath resets it all
    /// (<see cref="ResetForRun"/>); meta-progression (M8) decides the starting loadout.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int MaxMutations = 3;

        public readonly ItemInstance[] Weapons = new ItemInstance[2];
        public readonly ItemInstance[] Skills = new ItemInstance[2];
        public ItemInstance Amulet { get; private set; }
        public ItemInstance Outfit { get; private set; }
        public readonly List<MutationData> Mutations = new List<MutationData>();
        public readonly int[] Scrolls = new int[3];   // Ember, Brine, Moss

        public int Gold { get; private set; }
        public int Cells { get; private set; }
        public int FlaskCharges { get; private set; }
        public int MaxFlaskCharges { get; set; } = 2;

        public event Action Changed;
        /// <summary>Raised when a new run wipes the inventory (mutation runtime state, skill cooldowns, etc. reset on it).</summary>
        public event Action RunReset;

        PlayerCombat _combat;

        void Awake() => _combat = GetComponent<PlayerCombat>();

        PlayerCombat CombatRef => _combat != null ? _combat : (_combat = GetComponent<PlayerCombat>());

        public static int ColourIndex(StatColor c)
        {
            switch (c)
            {
                case StatColor.Ember: return 0;
                case StatColor.Brine: return 1;
                case StatColor.Moss: return 2;
                default: return -1;
            }
        }

        public int ScrollLevel(StatColor c) { int i = ColourIndex(c); return i < 0 ? 0 : Scrolls[i]; }

        /// <summary>Sum of the scroll levels in every colour of <paramref name="affinity"/>.</summary>
        public int ScrollLevels(StatColor affinity)
        {
            int total = 0;
            if ((affinity & StatColor.Ember) != 0) total += Scrolls[0];
            if ((affinity & StatColor.Brine) != 0) total += Scrolls[1];
            if ((affinity & StatColor.Moss) != 0) total += Scrolls[2];
            return total;
        }

        public int TotalScrolls => Scrolls[0] + Scrolls[1] + Scrolls[2];

        // ------------------------------------------------------------------ currency

        /// <summary>Call after mutating an owned item (upgrade tier) so stats and the HUD refresh.</summary>
        public void NotifyChanged() => Changed?.Invoke();

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddCells(int amount)
        {
            if (amount <= 0) return;
            Cells += amount;
            Changed?.Invoke();
        }

        public void AddScroll(StatColor colour)
        {
            int i = ColourIndex(colour);
            if (i < 0) return;
            Scrolls[i]++;
            Changed?.Invoke();
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = $"+1 {colour} scroll", Colour = StatNames.Colour(colour) });
        }

        // ------------------------------------------------------------------ flask

        public void RefillFlask()
        {
            FlaskCharges = MaxFlaskCharges;
            Changed?.Invoke();
        }

        /// <summary>Drinks a flask charge: heals a fraction of max health. Returns false if empty, full health or dead.</summary>
        public bool UseFlask(Health health, float healFraction = 0.45f)
        {
            if (FlaskCharges <= 0 || health == null || health.IsDead || health.Current >= health.Max - 0.01f) return false;
            FlaskCharges--;
            health.Heal(health.Max * healFraction);
            Changed?.Invoke();
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Flask", Colour = new Color(0.5f, 1f, 0.6f) });
            return true;
        }

        // ------------------------------------------------------------------ equipment

        public ItemInstance EquipWeapon(int slot, ItemInstance item)
        {
            ItemInstance old = Weapons[slot];
            Weapons[slot] = item;
            CombatRef?.SetWeapon(slot, item?.Weapon);
            Changed?.Invoke();
            return old;
        }

        public ItemInstance EquipSkill(int slot, ItemInstance item)
        {
            ItemInstance old = Skills[slot];
            Skills[slot] = item;
            Changed?.Invoke();
            return old;
        }

        public ItemInstance EquipGear(ItemInstance item)
        {
            ItemInstance old;
            if (item.Kind == ItemKind.Amulet) { old = Amulet; Amulet = item; }
            else { old = Outfit; Outfit = item; }
            Changed?.Invoke();
            return old;
        }

        public bool AddMutation(MutationData m)
        {
            if (m == null || Mutations.Count >= MaxMutations || Mutations.Contains(m)) return false;
            Mutations.Add(m);
            Changed?.Invoke();
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = "Mutation: " + m.displayName, Colour = m.tint });
            return true;
        }

        /// <summary>
        /// Picks an item up. Gear equips straight away (the old piece drops at <paramref name="dropAt"/>); weapons and skills
        /// fill an empty slot, or ask which slot to replace. <paramref name="onResolved"/> reports whether the item was taken.
        /// </summary>
        public void PickUp(ItemInstance item, Vector2 dropAt, Action<bool> onResolved = null)
        {
            if (item == null) { onResolved?.Invoke(false); return; }

            if (item.Kind == ItemKind.Amulet || item.Kind == ItemKind.Outfit)
            {
                ItemInstance old = EquipGear(item);
                if (old != null) LootFactory.DropItem(old, dropAt);
                Announce(item);
                onResolved?.Invoke(true);
                return;
            }

            ItemInstance[] slots = item.Kind == ItemKind.Weapon ? Weapons : Skills;
            for (int i = 0; i < 2; i++)
            {
                if (slots[i] != null) continue;
                Place(item, i);
                Announce(item);
                onResolved?.Invoke(true);
                return;
            }

            // both slots full: ask the UI (or replace slot 1 if nothing is listening, e.g. in tests)
            void Resolve(int slot)
            {
                if (slot < 0 || slot > 1) { onResolved?.Invoke(false); return; }
                ItemInstance old = Place(item, slot);
                if (old != null) LootFactory.DropItem(old, dropAt);
                Announce(item);
                onResolved?.Invoke(true);
            }

            if (EventBus<ReplaceItemRequest>.SubscriberCount == 0) { Resolve(0); return; }
            EventBus<ReplaceItemRequest>.Raise(new ReplaceItemRequest { Item = item, Inventory = this, Resolve = Resolve });
        }

        ItemInstance Place(ItemInstance item, int slot)
            => item.Kind == ItemKind.Weapon ? EquipWeapon(slot, item) : EquipSkill(slot, item);

        void Announce(ItemInstance item)
        {
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = item.DisplayName, Colour = item.RarityColour });
            EventBus<ItemAcquiredEvent>.Raise(new ItemAcquiredEvent { Item = item });
        }

        // ------------------------------------------------------------------ run reset

        /// <summary>Permadeath: wipe everything and hand out the starting loadout.</summary>
        public void ResetForRun(LootDatabase db)
        {
            for (int i = 0; i < 2; i++) { Weapons[i] = null; Skills[i] = null; }
            Amulet = null;
            Outfit = null;
            Mutations.Clear();
            Scrolls[0] = Scrolls[1] = Scrolls[2] = 0;
            Gold = 0;
            Cells = 0;
            FlaskCharges = MaxFlaskCharges;

            if (db != null)
            {
                if (db.startingWeapon1 != null) Weapons[0] = ItemInstance.ForWeapon(db.startingWeapon1);
                if (db.startingWeapon2 != null) Weapons[1] = ItemInstance.ForWeapon(db.startingWeapon2);
                if (db.startingOutfit != null) Outfit = ItemInstance.ForGear(db.startingOutfit);
            }

            if (CombatRef != null)
            {
                CombatRef.SetWeapon(0, Weapons[0]?.Weapon);
                CombatRef.SetWeapon(1, Weapons[1]?.Weapon);
            }
            RunReset?.Invoke();
            Changed?.Invoke();
        }
    }
}
