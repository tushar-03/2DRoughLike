using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>The painted icon sheets (items, pickups) the runtime draws from. Lives in Resources so every scene and build can reach it.</summary>
    [CreateAssetMenu(menuName = "Roughlike/Art/Icon Set", fileName = "IconSet")]
    public class GameIconSet : ScriptableObject
    {
        public CharacterArt[] sheets;
    }
}
