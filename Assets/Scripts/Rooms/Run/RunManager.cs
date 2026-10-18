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
        public int Bosses;
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
        [Tooltip("The levels of a full run, in order. Empty means the single biome above.")]
        [SerializeField] BiomeData[] biomes;
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

        /// <summary>One level of a run: which biome, whether it ends in the boss, and where it sits in its biome.</summary>
        public readonly struct Stage
        {
            public readonly BiomeData Biome;
            public readonly int Area, Number, Total;     // area index, 1-based level number within the biome, levels in the biome
            public readonly bool Boss;
            public Stage(BiomeData biome, int area, int number, int total, bool boss) { Biome = biome; Area = area; Number = number; Total = total; Boss = boss; }
        }

        readonly List<Stage> _plan = new List<Stage>();

        /// <summary>The levels of the run in order: each biome plays its ordinary levels, then its boss level.</summary>
        public IReadOnlyList<Stage> Plan { get { if (_plan.Count == 0) BuildPlan(); return _plan; } }

        void BuildPlan()
        {
            _plan.Clear();
            var all = biomes != null && biomes.Length > 0 ? biomes : new[] { biome };
            for (int a = 0; a < all.Length; a++)
            {
                if (all[a] == null) continue;
                int ordinary = all[a].boss != null ? Mathf.Max(0, all[a].levelsBeforeBoss) : 0;
                for (int i = 0; i <= ordinary; i++) _plan.Add(new Stage(all[a], a, i + 1, ordinary + 1, i == ordinary));
            }
        }

        public Stage CurrentStage => Plan[Mathf.Clamp(LevelIndex, 0, Plan.Count - 1)];
        public bool IsBossLevel => CurrentStage.Boss && CurrentStage.Biome != null && CurrentStage.Biome.boss != null;
        /// <summary>"Ember Catacombs  2/3" (and "Boss" on the last).</summary>
        public string LevelLabel
        {
            get
            {
                var s = CurrentStage;
                string name = s.Biome != null ? s.Biome.displayName : "";
                return s.Total <= 1 ? name : $"{name}  {s.Number}/{s.Total}" + (IsBossLevel ? "  -  Boss" : "");
            }
        }
        /// <summary>How deep into the game this level is, for scaling enemies: the area, plus a fraction for how far through it.</summary>
        public float Depth => CurrentStage.Area + (CurrentStage.Total <= 1 ? 0f : 0.6f * (CurrentStage.Number - 1) / CurrentStage.Total);

        public BiomeData Biome => biome;
        /// <summary>0 for the first level; each biome cleared moves it up.</summary>
        public int LevelIndex { get; private set; }
        public int LevelCount => Plan.Count;
        public Difficulty Difficulty => DifficultyService.Current;

        /// <summary>The seed of a given level of a run: the first level uses the run seed itself.</summary>
        public static uint LevelSeed(uint runSeed, int levelIndex)
            => levelIndex <= 0 ? runSeed : SeedCode.Hash(runSeed + "-level-" + levelIndex);
        public bool AutoStart { get => autoStart; set => autoStart = value; }
        public RunState State { get; private set; } = RunState.Idle;
        public uint Seed { get; private set; }
        public LevelLayout Layout { get; private set; }
        public BuiltLevel Level { get; private set; }
        public RunStats Stats { get; internal set; } = new RunStats();
        public readonly HashSet<Vector2Int> Visited = new HashSet<Vector2Int>();
        public Vector2Int CurrentRoom { get; internal set; }
        /// <summary>Dev toggle (F12): the minimap shows every room.</summary>
        public static bool RevealMap;
        /// <summary>True once the end-of-run panel should be showing.</summary>
        public bool EndScreenVisible => (State == RunState.Dead || State == RunState.Complete) && _endTimer >= deathScreenDelay;

        public event Action<RunState> StateChanged;

        public PlayerInventory Inventory => _inventory;
        public LootDirector Loot => lootDirector;
        /// <summary>The boss of the current level has been beaten.</summary>
        public bool BossDefeated { get; internal set; }
        public bool IsFinalLevel => LevelIndex + 1 >= LevelCount;

        public void Bind(BiomeData[] levels, PlayerController p, Unity.Cinemachine.CinemachineCamera camera = null, LootDirector director = null)
        {
            Bind(levels != null && levels.Length > 0 ? levels[0] : null, p, camera, director);
            biomes = levels;
            _plan.Clear();
        }

        public void Bind(BiomeData biomeData, PlayerController p, Unity.Cinemachine.CinemachineCamera camera = null, LootDirector director = null)
        {
            biome = biomeData;
            biomes = null;
            _plan.Clear();
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
            EventBus<BossDefeatedEvent>.Subscribe(OnBossDefeated);
        }

        void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnPlayerDied;
                _health.HitTaken -= OnPlayerHit;
            }
            EventBus<EnemyDiedEvent>.Unsubscribe(OnEnemyDied);
            EventBus<BossDefeatedEvent>.Unsubscribe(OnBossDefeated);
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
                DifficultyService.Current = GameSession.Difficulty;
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
            BossDefeated = false;
            LevelIndex = restore != null ? Mathf.Clamp(restore.levelIndex, 0, LevelCount - 1) : 0;
            if (restore != null) DifficultyService.Current = (Difficulty)Mathf.Clamp(restore.difficulty, 0, 3);
            biome = Plan[LevelIndex].Biome;
            BuildCurrentLevel();
            if (biome.art != null && _atmosphere == null) _atmosphere = LevelAtmosphere.Create(biome.art, player.transform);

            Stats = new RunStats();
            Visited.Clear();
            CurrentRoom = Layout.Start.Pos;
            Visited.Add(CurrentRoom);
            Stats.RoomsVisited = 1;
            _endTimer = 0f;
            if (lootDirector != null) lootDirector.Loot = biome.loot;

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

        /// <summary>Generates and builds the current biome's level from the run seed.</summary>
        void BuildCurrentLevel()
        {
            biome = Plan[LevelIndex].Biome;
            bool bossLevel = IsBossLevel;
            // every level is generated fresh from its own seed; the boss level is a shorter walk to the arena
            var settings = bossLevel ? biome.generator.ForBossLevel() : biome.generator;
            Layout = LevelGenerator.Generate(LevelSeed(Seed, LevelIndex), settings);
            Level = LevelBuilder.Build(Layout, biome, true, Depth, bossLevel);
            BossDefeated = false;
            Level.Exit.Activated += OnExitReached;
            if (biome.art != null)
            {
                if (_atmosphere != null) Destroy(_atmosphere.gameObject);
                _atmosphere = LevelAtmosphere.Create(biome.art, player.transform);
            }
        }

        /// <summary>The boss is down and the portal taken: on to the next biome, keeping everything you carry.</summary>
        void AdvanceLevel()
        {
            LevelIndex++;
            BossDefeated = false;
            biome = Plan[LevelIndex].Biome;
            Level?.Destroy();
            HitStop.Cancel();
            BuildCurrentLevel();

            Visited.Clear();
            CurrentRoom = Layout.Start.Pos;
            Visited.Add(CurrentRoom);
            Stats.RoomsVisited++;
            if (lootDirector != null) { lootDirector.BeginRun(LevelSeed(Seed, LevelIndex)); lootDirector.Loot = biome.loot; }

            var status = player.GetComponent<StatusEffects>();
            if (status != null) status.ClearAll();
            _health.Heal(_health.Max * 0.4f);
            player.ResetTo(Level.PlayerSpawn);
            if (_defense != null) _defense.GrantInvulnerability(1.5f);
            if (vcam != null) vcam.PreviousStateIsValid = false;
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = LevelLabel, Colour = new Color(1f, 0.85f, 0.5f) });
            SaveNow();
        }

        void OnExitReached()
        {
            if (State != RunState.Running) return;
            if (LevelIndex + 1 < LevelCount) { AdvanceLevel(); return; }
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

        void OnBossDefeated(BossDefeatedEvent e)
        {
            if (State != RunState.Running) return;
            Stats.Bosses++;
            BossDefeated = true;
            // a boss is a checkpoint: health is restored, everything else carries over, and the run is saved as the start of the next area
            _health.ResetToFull();
            StartCoroutine(CheckpointAfterBoss());
        }

        System.Collections.IEnumerator CheckpointAfterBoss()
        {
            yield return new WaitForSecondsRealtime(0.8f);   // the reward has dropped by now
            if (State != RunState.Running) yield break;
            _health.ResetToFull();   // anything that landed during the death scene is healed too
            SaveNow();
            EventBus<ToastEvent>.Raise(new ToastEvent
            {
                Text = IsFinalLevel ? "Checkpoint saved" : "Checkpoint saved - the way to the next area is open",
                Colour = new Color(0.55f, 1f, 0.7f)
            });
        }

        /// <summary>The boss's reward if it is still lying on the floor, otherwise null.</summary>
        public ItemInstance PendingBossItem
        {
            get
            {
                var item = lootDirector != null ? lootDirector.LastBossItem : null;
                if (item == null) return null;
                foreach (var w in FindObjectsByType<WorldItem>()) if (w.Item == item) return item;
                return null;
            }
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
                    if (kb.rKey.wasPressedThisFrame)
                    {
                        if (State == RunState.Complete) StartNewGameFromEnd(); else StartRandomRun();
                    }
                    if (kb.tKey.wasPressedThisFrame && State == RunState.Dead) RestartSameSeed();
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

        /// <summary>After the game is won: a whole new game (practice room first, then a fresh seed).</summary>
        public static void StartNewGameFromEnd()
        {
            GameSession.StartNew(0, DifficultyService.Current);
            LoadoutSelection.Index = -1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.LoadoutScene);
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





