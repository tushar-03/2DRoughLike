using System;
using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Items
{
    /// <summary>One starting loadout offered before a new run: what goes in each weapon slot.</summary>
    [Serializable]
    public class LoadoutData
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public WeaponData slot1;
        public WeaponData slot2;
    }

    /// <summary>The loadout picked in the practice room for the run that is about to start (-1 means the database default).</summary>
    public static class LoadoutSelection
    {
        public static int Index = -1;

        public static LoadoutData Get(LootDatabase db)
        {
            if (db == null || db.loadouts == null || Index < 0 || Index >= db.loadouts.Length) return null;
            return db.loadouts[Index];
        }
    }
}
