using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;

namespace Roughlike.Rooms
{
    public enum RunState { Idle, Running, Dead, Complete }

    public sealed class RunStats
    {
        public int Kills;
        public float Time;
        public float DamageTaken;
        public int RoomsVisited;
        public int Gold;
        /// <summary>Cells carried at the end of the run. Banked (kept) only when the biome is cleared; lost on death.</summary>
        public int Cells;
        public bool CellsBanked;
    }

    /// <summary>
    /// Owns one run at a time: generates the level from a seed, places the player at the start, tracks stats
    /// and rooms visited, and handles death (permadeath: the run ends) and reaching the exit. Starting a
    /// new run or retrying the same seed rebuilds everything from scratch, resetting the player's loadout state.
    /// Debug keys: F9 new random seed, F10 retry this seed.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [SerializeField] BiomeData biome;
        [SerializeField] PlayerController player;
        [SerializeField] float deathScreenDelay = 1.2f;
        [SerializeField] Unity.Cinemachine.CinemachineCamera vcam;
        [Tooltip("Start a random run automatically on Play.")]
        [SerializeField] bool autoStart = true;

        Health _health;
        PlayerCombat _combat;
        PlayerDefense _defense;
        PlayerInventory _inventory;
        [SerializeField] LootDirector lootDirector;
        SpriteRenderer _playerSprite;
        float _endTimer;

        public BiomeData Biome => biome;
        public bool AutoStart { get => autoStart; set => autoStart = value; }
        public RunState State { get; private set; } = RunState.Idle;
        public uint Seed { get; private set; }
        public LevelLayout Layout { get; private set; }
        public BuiltLevel Level { get; private set; }
        public RunStats Stats { get; internal set; } = new RunStats();
        public readonly HashSet<Vector2Int> Visited = new HashSet<Vector2Int>();
        public Vector2Int CurrentRoom { get; internal set; }
        /// <summary>True once the end-of-run panel should be showing.</summary>
        public bool EndScreenVisible => (State == RunState.Dead || State == RunState.Complete) && _endTimer >= deathScreenDelay;

        public event Action<RunState> StateChanged;

        public PlayerInventory Inventory => _inventory;

        public void Bind(BiomeData biomeData, PlayerController p, Unity.Cinemachine.CinemachineCamera camera = null, LootDirector director = null)
        {
            biome = biomeData;
            player = p;
            vcam = camera;
            lootDirector = director;
        }

        void Awake() { CachePlayer(); }

        void CachePlayer()
        {
            if (player == null) return;
            _health = player.GetComponent<Health>();
            _combat = player.GetComponent<PlayerCombat>();
            _defense = player.GetComponent<PlayerDefense>();
            _inventory = player.GetComponent<PlayerInventory>();
            _playerSprite = player.GetComponentInChildren<SpriteRenderer>();
        }

        void OnEnable()
        {
            CachePlayer();
            if (_health != null)
            {
                _health.Died += OnPlayerDied;
                _health.HitTaken += OnPlayerHit;
            }
            EventBus<EnemyDiedEvent>.Subscribe(OnEnemyDied);
        }

        void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnPlayerDied;
                _health.HitTaken -= OnPlayerHit;
            }
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
        }

        void Start()
        {
            // the main menu hands over its choice through GameSession
            SessionMode mode = GameSession.Mode;
            uint requestedSeed = GameSession.Seed;
            GameSession.Clear();

            if (mode == SessionMode.Continue && RunSave.TryLoad(out RunSave save))
            {
                StartRun((uint)save.seed, save);
                return;
            }
            if (mode == SessionMode.NewRun)
            {
                StartRun(requestedSeed != 0 ? requestedSeed : RandomSeed());
                return;
            }
            if (autoStart && State == RunState.Idle) StartRandomRun();
        }

        // =====================================================================
        // Saving
        // =====================================================================

        public Health PlayerHealth => _health;
        public PlayerController Player => player;

        /// <summary>Writes the current run to disk so the main menu can Continue it. Safe to call any time during a run.</summary>
        public void SaveNow()
        {
            if (State != RunState.Running || Level == null || _health == null || _health.IsDead) return;
            RunSaver.Capture(this).Write();
        }

        // =====================================================================
        // Run lifecycle
        // =====================================================================

        public static uint RandomSeed()
        {
            long t = DateTime.UtcNow.Ticks;
            return SeedCode.Hash(t.ToString()) ^ (uint)Environment.TickCount;
        }

        public void StartRandomRun() => StartRun(RandomSeed());
        public void RestartSameSeed() => StartRun(Seed);

        public void StartRunFromText(string text)
        {
            if (SeedCode.TryParse(text, out uint seed)) StartRun(seed);
        }

        /// <summary>Starts a run. With a <paramref name="restore"/> save the level is rebuilt from the seed and the save is applied on top.</summary>
        LevelAtmosphere _atmosphere;

        public void StartRun(uint seed, RunSave restore = null)
        {
            if (player == null || biome == null) { Debug.LogError("RunManager needs a biome and a player."); return; }
            if (restore == null) RunSave.Delete();   // starting fresh abandons the old saved run
            if (_health == null) { CachePlayer(); OnEnable(); }

            Level?.Destroy();
            HitStop.Cancel();

            Seed = seed;
            Layout = LevelGenerator.Generate(seed, biome.generator);
            Level = LevelBuilder.Build(Layout, biome);
            Level.Exit.Activated += OnExitReached;
            if (biome.art != null && _atmosphere == null) _atmosphere = LevelAtmosphere.Create(biome.art, player.transform);

            Stats = new RunStats();
            Visited.Clear();
            CurrentRoom = Layout.Start.Pos;
            Visited.Add(CurrentRoom);
            Stats.RoomsVisited = 1;
            _endTimer = 0f;

            // permadeath: a fresh inventory and starting loadout, then full health on top of its stats
            if (_inventory != null) _inventory.ResetForRun(biome.loot);
            if (lootDirector != null) lootDirector.BeginRun(seed);
            var status = player.GetComponent<StatusEffects>();
            if (status != null) status.ClearAll();
            _health.ResetToFull();
            player.ResetTo(Level.PlayerSpawn);
            player.Abilities = MovementAbilities.None;
            if (_combat != null) _combat.Interrupt();
            if (_defense != null) _defense.GrantInvulnerability(1f);
            SetPlayerActive(true);
            if (vcam != null) vcam.PreviousStateIsValid = false;   // snap the camera instead of panning across the map

            SetState(RunState.Running);
            if (restore != null) RunSaver.Apply(this, restore);
            EventBus<RunStartedEvent>.Raise(new RunStartedEvent { Seed = seed });
            SaveNow();
        }

        void SetState(RunState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }

        void SetPlayerActive(bool active)
        {
            player.autoTick = active;
            if (_combat != null) _combat.autoTick = active;
            if (_defense != null) _defense.autoTick = active;
            if (_playerSprite != null) _playerSprite.enabled = active;
        }

        void CaptureCurrency(bool bank)
        {
            if (_inventory == null) return;
            Stats.Gold = _inventory.Gold;
            Stats.Cells = _inventory.Cells;
            Stats.CellsBanked = bank;
        }

        void OnPlayerDied()
        {
            if (State != RunState.Running) return;
            CaptureCurrency(false);   // unbanked Cells are lost on death
            RunSave.Delete();         // permadeath: there is nothing to continue
            _endTimer = 0f;
            SetPlayerActive(false);
            SetState(RunState.Dead);
        }

        void OnExitReached()
        {
            if (State != RunState.Running) return;
            CaptureCurrency(true);    // the exit door banks your Cells
            RunSave.Delete();
            _endTimer = 0f;
            SetPlayerActive(false);
            SetState(RunState.Complete);
        }

        void OnPlayerHit(HitReport r)
        {
            if (State == RunState.Running && r.Amount > 0f) Stats.DamageTaken += r.Amount;
        }

        void OnEnemyDied(EnemyDiedEvent e)
        {
            if (State == RunState.Running) Stats.Kills++;
        }

        // =====================================================================
        // Per-frame
        // =====================================================================

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f9Key.wasPressedThisFrame) StartRandomRun();
                if (kb.f10Key.wasPressedThisFrame && State != RunState.Idle) RestartSameSeed();
                if (EndScreenVisible)
                {
                    if (kb.rKey.wasPressedThisFrame) StartRandomRun();
                    if (kb.tKey.wasPressedThisFrame) RestartSameSeed();
                    if (kb.mKey.wasPressedThisFrame) UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.MenuScene);
                }
            }

            if (State == RunState.Running)
            {
                Stats.Time += Time.deltaTime;
                TrackRoom();
            }
            else if (State == RunState.Dead || State == RunState.Complete)
            {
                _endTimer += Time.unscaledDeltaTime;
            }
        }

        /// <summary>Checks which room the player is in (runs each frame; public so tests can drive it).</summary>
        public void RefreshRoom() => TrackRoom();

        void TrackRoom()
        {
            Vector2Int cell = ChunkMetrics.CellAt((Vector2)player.transform.position + Vector2.up);
            if (cell == CurrentRoom || !Layout.Contains(cell)) return;
            CurrentRoom = cell;
            if (Visited.Add(cell)) Stats.RoomsVisited++;
            if (lootDirector != null && Layout.TryGet(cell, out var c)) lootDirector.Progress = c.Progress;
            SaveNow();   // autosave on every room change
        }

        void OnDestroy()
        {
            Level?.Destroy();
        }
    }
}





