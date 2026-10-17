using System;
using UnityEngine;

namespace Roughlike.Utils
{
    /// <summary>
    /// The look of a biome: tiling sprites for walls, floor trim and platforms, the parallax backdrop layers and the lighting mood.
    /// Swap the sprites (or the whole asset) to re-skin a biome; geometry and gameplay never read from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Roughlike/Art/Environment Art", fileName = "EnvironmentArt")]
    public class EnvironmentArt : ScriptableObject
    {
        [Serializable]
        public class BackdropLayer
        {
            public string name = "Layer";
            public Sprite sprite;
            [Range(-1f, 1f), Tooltip("0 = fixed in the world, 1 = glued to the camera (very far away). Negative values move faster than the world: a foreground layer in front of the player.")]
            public float follow = 0.8f;
            [Range(0f, 1f), Tooltip("Same, for vertical movement.")]
            public float followY = 0.85f;
            [Tooltip("World height of the layer's centre relative to the camera at rest.")]
            public float yOffset = 0f;
            public Color tint = Color.white;
            public int sortingOrder = -40;
        }

        [Header("Tiles (seamless, 128 pixels per unit)")]
        public Sprite wall;
        [Tooltip("Darker stone for the solid rock packed around the rooms.")]
        public Sprite rock;
        public Sprite floorTrim;
        public Sprite platform;

        [Header("Backdrop (back to front)")]
        public BackdropLayer[] backdrop = new BackdropLayer[0];

        [Header("Lighting mood")]
        public Color ambient = new Color(0.42f, 0.46f, 0.62f);
        [Range(0f, 1.5f)] public float ambientIntensity = 0.7f;
        public Color playerLight = new Color(1f, 0.82f, 0.55f);
        public Color torchLight = new Color(1f, 0.6f, 0.28f);

        [Header("Post processing")]
        [Range(0f, 3f)] public float bloom = 0.9f;
        [Range(0f, 1f)] public float vignette = 0.38f;
        [Range(0f, 1f)] public float grain = 0.12f;
        public Color colourFilter = new Color(1f, 0.97f, 0.94f);
        [Range(-100f, 100f)] public float saturation = 14f;
        [Range(-100f, 100f)] public float contrast = 12f;

        [Header("Drifting motes (dust, embers, fireflies)")]
        public Color moteColour = new Color(0.7f, 0.9f, 1f, 0.6f);
        [Range(0, 80)] public int moteCount = 36;

        [Header("Decor")]
        public CharacterArt props;
        [Tooltip("Exit door, chest, altar and merchant.")]
        public CharacterArt worldProps;
    }
}
