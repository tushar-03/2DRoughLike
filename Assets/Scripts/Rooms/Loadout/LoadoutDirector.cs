using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>
    /// The practice room shown once before a new run: a quiet hall with two training dummies where the player tries each starting
    /// loadout (bow, dual swords, greatsword, sword and shield), picks one, then steps into the portal to begin the run. Nothing
    /// here counts toward the run: no timer, no enemies, no loot.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class LoadoutDirector : MonoBehaviour
    {
        const float Floor = ChunkMetrics.FloorTop;

        [SerializeField] BiomeData biome;
        [SerializeField] PlayerController player;

        PlayerInventory _inventory;
        Health _health;
        bool _started;
        bool _finished;
        ExitDoor _door;

        public IReadOnlyList<LoadoutData> Options => biome != null && biome.loot != null && biome.loot.loadouts != null ? biome.loot.loadouts : Array.Empty<LoadoutData>();
        public int Selected { get; private set; } = -1;
        public bool Finished => _finished;
        public Vector2 SpawnPoint => new Vector2(4f, Floor);

        public event Action SelectionChanged;
        /// <summary>The player stepped into the portal: load the real run.</summary>
        public event Action Completed;

        public void Bind(BiomeData b, PlayerController p)
        {
            biome = b;
            player = p;
        }

        void Start() => Begin();

        public void Begin()
        {
            if (_started || player == null || biome == null) return;
            _started = true;

            WorldArt.Props = biome.art != null ? biome.art.worldProps : null;
            _health = player.GetComponent<Health>();
            _inventory = player.GetComponent<PlayerInventory>();

            BuildRoom();
            if (biome.art != null) LevelAtmosphere.Create(biome.art, player.transform);
            if (_inventory != null) _inventory.ResetForRun(biome.loot);
            if (_health != null) _health.ResetToFull();
            player.ResetTo(SpawnPoint);

            Select(DefaultIndex());
        }

        int DefaultIndex()
        {
            var opts = Options;
            if (LoadoutSelection.Index >= 0 && LoadoutSelection.Index < opts.Count) return LoadoutSelection.Index;
            for (int i = 0; i < opts.Count; i++) if (opts[i].id == "sword_shield") return i;
            return 0;
        }

        void BuildRoom()
        {
            var root = new GameObject("Practice Room").transform;
            var cell = new LevelCell { Pos = Vector2Int.zero, Index = 0, Role = RoomRole.Start, Mask = (Openings)0, OnMainPath = true, Progress = 0f };
            RoomChunk chunk = ChunkBuilder.Build(cell, root, biome);
            RoomDecor.Populate(chunk, cell, biome);
            Physics2D.SyncTransforms();

            EnemyFactory.BuildDummy(new Vector2(13f, Floor));
            EnemyFactory.BuildDummy(new Vector2(21f, Floor));

            _door = ExitDoor.Create(transform, new Vector2(27f, Floor), biome.accent);
            _door.Activated += () => { if (_finished) return; _finished = true; Completed?.Invoke(); };
        }

        /// <summary>Equips loadout <paramref name="index"/> (the player can swap as often as they like).</summary>
        public void Select(int index)
        {
            var opts = Options;
            if (opts.Count == 0) return;
            index = Mathf.Clamp(index, 0, opts.Count - 1);
            Selected = index;
            LoadoutSelection.Index = index;

            if (_inventory != null)
            {
                LoadoutData l = opts[index];
                _inventory.EquipWeapon(0, l.slot1 != null ? ItemInstance.ForWeapon(l.slot1) : null);
                _inventory.EquipWeapon(1, l.slot2 != null ? ItemInstance.ForWeapon(l.slot2) : null);
            }
            Sfx.Play(SfxId.ItemPickup, 0.6f);
            SelectionChanged?.Invoke();
        }

        public void Next(int step)
        {
            int n = Mathf.Max(1, Options.Count);
            Select((Selected + step + n) % n);
        }

        void Update()
        {
            // practice makes no mess: never let the player die here
            if (_health != null && _health.IsDead) { _health.ResetToFull(); player.ResetTo(SpawnPoint); }
            else if (_health != null && _health.Fraction < 0.5f) _health.ResetToFull();
        }
    }
}
