using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;

namespace Roughlike.Items
{
    /// <summary>
    /// Decides what drops: gold (and the odd Cell) from enemies, a scroll now and then when a room is cleared.
    /// Room drops are seeded per room, so a seed fixes which rooms reward scrolls and in what colour.
    /// One per scene.
    /// </summary>
    public sealed class LootDirector : MonoBehaviour
    {
        [SerializeField] float scrollChance = 0.45f;
        [SerializeField] float cellChance = 0.12f;

        SeededRng _rng = new SeededRng(1);
        uint _seed = 1;
        PlayerStats _stats;

        /// <summary>0..1 position along the level; raises gold and quality of drops a little.</summary>
        public float Progress { get; set; }

        public void BeginRun(uint seed)
        {
            _seed = seed;
            _rng = new SeededRng(seed).Fork(777);
            Progress = 0f;
        }

        void OnEnable()
        {
            EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
            EventBus<RoomClearedEvent>.Subscribe(OnRoomCleared);
        }

        void OnDisable()
        {
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
            EventBus<RoomClearedEvent>.Unsubscribe(OnRoomCleared);
        }

        PlayerStats Stats
        {
            get
            {
                if (_stats == null && Health.Player != null) _stats = Health.Player.GetComponent<PlayerStats>();
                return _stats;
            }
        }

        void OnEnemyDied(EnemyDiedEvent e)
        {
            float reward = e.Definition != null ? e.Definition.rewardMultiplier : 1f;
            if (reward <= 0f) return;   // summoned minions drop nothing
            float goldFind = (Stats != null ? Stats.GoldFind : 1f) * reward;
            int total = Mathf.RoundToInt(_rng.Range(4, 10) * (1f + 0.5f * Progress) * goldFind);
            int coins = Mathf.Clamp(total / 4, 1, 3);
            int each = Mathf.Max(1, total / coins);
            for (int i = 0; i < coins; i++)
                LootFactory.SpawnGold(e.Position + new Vector2(0f, 0.8f), each);

            if (_rng.Chance(cellChance)) LootFactory.SpawnCell(e.Position + new Vector2(0f, 0.8f), 1);
        }

        void OnRoomCleared(RoomClearedEvent e)
        {
            var rng = new SeededRng(_seed).Fork(2000 + e.CellIndex);
            if (!rng.Chance(scrollChance)) return;
            LootFactory.SpawnScroll(e.Position, LootRoller.RollScrollColour(rng));
        }
    }
}
