using UnityEngine;

namespace Roughlike.Items
{
    public enum GearSlot { Amulet, Outfit }

    /// <summary>An amulet or outfit: always-on modifiers while equipped. One of each slot.</summary>
    [CreateAssetMenu(menuName = "Roughlike/Items/Gear", fileName = "Gear")]
    public class GearData : ScriptableObject
    {
        public string displayName = "Gear";
        public GearSlot slot = GearSlot.Amulet;
        [TextArea] public string description;
        public Color tint = Color.white;
        public StatEntry[] modifiers;
    }
}
