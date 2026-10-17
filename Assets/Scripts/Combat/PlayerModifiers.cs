namespace Roughlike.Combat
{
    /// <summary>Per-weapon numbers the item system computes for a slot (rarity, tier, scroll scaling, affixes, mutations).</summary>
    public struct WeaponMods
    {
        public float DamageMultiplier;
        public float CritChance;
        public float CritMultiplier;
        public StatusType Status;
        public float StatusChance;
        public float StatusDuration;
        public float StatusPotency;

        public static WeaponMods Neutral => new WeaponMods { DamageMultiplier = 1f, CritChance = 0f, CritMultiplier = 1.5f };
    }

    /// <summary>
    /// Everything the combat layer needs to know from the item/stat system, without depending on it.
    /// Implemented by <c>PlayerStats</c> in the Items module; when absent, combat uses neutral values.
    /// </summary>
    public interface IPlayerModifiers
    {
        WeaponMods GetWeaponMods(int slot);
        /// <summary>1 = normal; scales windup, active and recovery speed.</summary>
        float AttackSpeed { get; }
        float DamageTakenMultiplier { get; }
        float ParryWindowMultiplier { get; }
    }
}
