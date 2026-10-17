using UnityEngine;

namespace Roughlike.Enemies
{
    /// <summary>A boss fight has started: the UI shows its health bar.</summary>
    public struct BossSpawnedEvent
    {
        public BossController Boss;
    }

    /// <summary>A boss died: loot drops, doors open.</summary>
    public struct BossDefeatedEvent
    {
        public Vector2 Position;
        public BossDefinition Definition;
    }
}
