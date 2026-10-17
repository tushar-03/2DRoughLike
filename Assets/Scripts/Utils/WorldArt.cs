namespace Roughlike.Utils
{
    /// <summary>
    /// The painted set pieces (door, chest, altar, merchant) for the level being built. Set by the level builder from the biome's
    /// art; when null the factories fall back to their graybox shapes, so the code never depends on art existing.
    /// </summary>
    public static class WorldArt
    {
        public static CharacterArt Props;

        public static bool Has(string clip) => Props != null && Props.Has(clip);
    }
}
