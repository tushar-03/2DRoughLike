using UnityEngine;
using Roughlike.Combat;

namespace Roughlike.Items
{
    /// <summary>Everything the loot roller draws from. One asset per game (biomes can filter later).</summary>
    [CreateAssetMenu(menuName = "Roughlike/Items/Loot Database", fileName = "LootDatabase")]
    public class LootDatabase : ScriptableObject
    {
        public WeaponData[] weapons;
        public SkillData[] skills;
        public GearData[] amulets;
        public GearData[] outfits;
        public AffixData[] affixes;
        public MutationData[] mutations;

        [Header("Starting loadout")]
        public WeaponData startingWeapon1;
        public WeaponData startingWeapon2;
        public GearData startingOutfit;

        [Header("Rolling")]
        [Range(0f, 1f), Tooltip("Chance that a non-common item is cursed (a bad affix in exchange for stronger stats).")]
        public float curseChance = 0.08f;
    }
}
