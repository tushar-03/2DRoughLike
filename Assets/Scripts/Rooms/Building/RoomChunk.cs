using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Rooms
{
    /// <summary>
    /// A built room. Holds the cell it came from and the marker points other systems use (enemy spawns, the
    /// player start, the exit and loot spots). Hand-authored chunk prefabs use this same component and markers,
    /// so a designer's prefab can replace a procedural one without code changes.
    /// </summary>
    public class RoomChunk : MonoBehaviour
    {
        [System.NonSerialized] public LevelCell Cell;
        public Rect WorldBounds;
        public List<Vector2> EnemySpawns = new List<Vector2>();
        public Vector2 PlayerSpawn;
        public Vector2 ExitAnchor;
        public Vector2 LootAnchor;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size);
            Gizmos.color = Color.red;
            foreach (var p in EnemySpawns) Gizmos.DrawWireSphere(p + Vector2.up * 0.5f, 0.4f);
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(PlayerSpawn + Vector2.up * 0.5f, 0.4f);
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(ExitAnchor + Vector2.up * 0.5f, 0.4f);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(LootAnchor + Vector2.up * 0.5f, 0.4f);
        }
    }
}

