using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;

namespace Roughlike.Rooms
{
    /// <summary>Counts the enemies spawned in a room and raises <see cref="RoomClearedEvent"/> when the last one dies.</summary>
    public sealed class RoomClearTracker : MonoBehaviour
    {
        int _alive;
        readonly HashSet<Health> _tracked = new HashSet<Health>();
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

        void OnEnable() => EventBus<MinionSpawnedEvent>.Subscribe(OnMinionSpawned);
        void OnDisable() => EventBus<MinionSpawnedEvent>.Unsubscribe(OnMinionSpawned);

        public void Register(Health enemy)
        {
            if (!_tracked.Add(enemy)) return;
            _alive++;
            enemy.Died += OnEnemyDied;
        }

        /// <summary>A summoner in this room made a minion: the room is not clear until that dies too.</summary>
        void OnMinionSpawned(MinionSpawnedEvent e)
        {
            if (_cleared || e.Minion == null || e.Summoner == null) return;
            if (_tracked.Contains(e.Summoner.Health)) Register(e.Minion.Health);
        }

        /// <summary>Loading a save: this room was already cleared, so remove its enemies without raising events or drops.</summary>
        public void RestoreCleared()
        {
            _cleared = true;
            _alive = 0;
            foreach (var e in Chunk.GetComponentsInChildren<Roughlike.Enemies.EnemyController>(true))
                if (e != null)
                {
                    e.gameObject.SetActive(false);   // gone immediately (Destroy waits for the end of the frame)
                    Destroy(e.gameObject);
                }
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

