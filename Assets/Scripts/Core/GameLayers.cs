using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>
    /// Single source of truth for physics layer names and the masks built from them.
    /// Layers are created by Roughlike/Setup/Create Layers (see Editor/ProjectSetup).
    /// </summary>
    public static class GameLayers
    {
        public const string GroundName = "Ground";
        public const string OneWayName = "OneWay";
        public const string PlayerName = "Player";
        public const string EnemyName = "Enemy";
        public const string LadderName = "Ladder";
        public const string HazardName = "Hazard";
        public const string PickupName = "Pickup";

        public static readonly string[] AllNames =
        {
            GroundName, OneWayName, PlayerName, EnemyName, LadderName, HazardName, PickupName
        };

        /// <summary>First user layer index; Ground is 8, OneWay 9, and so on in AllNames order.</summary>
        public const int FirstUserLayer = 8;

        public static int Ground => Index(GroundName);
        public static int OneWay => Index(OneWayName);
        public static int Player => Index(PlayerName);
        public static int Enemy => Index(EnemyName);
        public static int Ladder => Index(LadderName);

        /// <summary>Solid, blocks movement from every direction.</summary>
        public static LayerMask GroundMask => Mask(GroundName);

        /// <summary>Platforms that can be jumped up through and dropped through.</summary>
        public static LayerMask OneWayMask => Mask(OneWayName);

        /// <summary>Everything the player can stand on.</summary>
        public static LayerMask WalkableMask => Mask(GroundName, OneWayName);

        public static LayerMask LadderMask => Mask(LadderName);

        static int Index(string layerName)
        {
            int i = LayerMask.NameToLayer(layerName);
            if (i < 0) Debug.LogError($"Physics layer '{layerName}' is missing. Run Roughlike/Setup/Create Layers.");
            return i;
        }

        static LayerMask Mask(params string[] names) => LayerMask.GetMask(names);
    }
}
