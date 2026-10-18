using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Items
{
    /// <summary>Short message for the HUD ("+1 Ember scroll", "Picked up ...").</summary>
    public struct ToastEvent
    {
        public string Text;
        public Color Colour;
    }

    /// <summary>The interaction prompt the player should currently see (or hide).</summary>
    public struct InteractPromptEvent
    {
        public bool Visible;
        public string Text;
        public Color Colour;
        /// <summary>Set for floor items so the HUD can show a detail card.</summary>
        public ItemInstance Item;
    }

    /// <summary>A weapon or skill was picked up with both slots full: the UI asks which slot to replace.</summary>
    public struct ReplaceItemRequest
    {
        public ItemInstance Item;
        public PlayerInventory Inventory;
        /// <summary>Call with 0 or 1 to replace that slot, or -1 to cancel.</summary>
        public Action<int> Resolve;
    }

    public struct OpenShopEvent
    {
        public ShopStock Stock;
        public PlayerInventory Inventory;
        public Action Closed;
    }

    public struct OpenMutationEvent
    {
        public IList<MutationData> Choices;
        public PlayerInventory Inventory;
        /// <summary>Choose an option (index) or -1 to cancel.</summary>
        public Action<int> Resolve;
        /// <summary>Used when every slot is full: the chosen option (index) and the slot it replaces (-1 cancels).</summary>
        public Action<int, int> ResolveReplace;
    }

    /// <summary>All enemies in a combat room are dead.</summary>
    public struct RoomClearedEvent
    {
        public Vector2Int Cell;
        public Vector2 Position;
        public float Progress;
        public int CellIndex;
    }

    /// <summary>A new run began (menus close, per-run UI resets).</summary>
    public struct RunStartedEvent
    {
        public uint Seed;
    }

    public struct ItemAcquiredEvent
    {
        public ItemInstance Item;
    }
}


