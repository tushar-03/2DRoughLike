using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;

namespace Roughlike.Rooms
{
    /// <summary>Counts the enemies spawned in a room and raises <see cref="RoomClearedEvent"/> when the last one dies.</summary>
    public sealed class RoomClearTracker : MonoBehaviour
    {
        int _alive;
        bool _cleared;

        public LevelCell Cell { get; private set; }
        public RoomChunk Chunk { get; private set; }
        public bool Cleared => _cleared;
        public int Alive => _alive;

        public void Init(LevelCell cell, RoomChunk chunk)
        {
            Cell = cell;
            Chunk = chunk;
        }

        public void Register(Health enemy)
        {
            _alive++;
            enemy.Died += OnEnemyDied;
        }

        void OnEnemyDied()
        {
            if (--_alive > 0 || _cleared) return;
            _cleared = true;

            Vector2 pos = new Vector2(Chunk.WorldBounds.center.x, Chunk.WorldBounds.y + ChunkMetrics.FloorTop);
            EventBus<RoomClearedEvent>.Raise(new RoomClearedEvent
            {
                Cell = Cell.Pos,
                Position = pos,
                Progress = Cell.Progress,
                CellIndex = Cell.Index
            });
        }
    }
}
