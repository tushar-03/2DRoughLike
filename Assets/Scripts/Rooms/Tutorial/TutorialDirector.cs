using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Rooms
{
    /// <summary>
    /// The tutorial: one long hall of ten rooms with a station in each. Each station teaches one thing (move, jump, roll, attack,
    /// parry, ledges, loot, skills, mutations, the exit), shows what to press for whatever device is in use, and keeps a gate shut
    /// until you have done it. Built from the same room builder, art and lighting as a real run.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TutorialDirector : MonoBehaviour
    {
        public sealed class Step
        {
            public string Title;
            public Func<string> Body;
            public Action Begin;
            public Func<bool> Done;
            public float GateX = -1f;
            public GameObject Gate;
            public Action End;
        }

        const float RoomWidth = ChunkMetrics.Width;
        const float Floor = ChunkMetrics.FloorTop;
        const float Top = ChunkMetrics.Height - 1f;
        const int Rooms = 10;

        [SerializeField] BiomeData biome;
        [SerializeField] PlayerController player;
        [SerializeField] EnemyDefinition gruntDefinition;

        readonly List<Step> _steps = new List<Step>();
        Health _health;
        PlayerCombat _combat;
        PlayerInventory _inventory;
        TrainingDummy _dummy;
        EnemyController _grunt;
        LootChest _chest;
        MutationAltar _altar;
        ExitDoor _exit;
        GameObject _marker;
        int _jumps, _rolls, _dummyHits, _stage;
        bool _parried, _skillUsed, _exitUsed;
        int _index;
        bool _started;

        public IReadOnlyList<Step> Steps => _steps;
        public int Index => _index;
        public Step Current => _index >= 0 && _index < _steps.Count ? _steps[_index] : null;
        public bool Finished { get; private set; }
        public Vector2 SpawnPoint => new Vector2(4f, Floor);

        public event Action StepChanged;
        /// <summary>Raised once when the whole tutorial is complete (before the game starts).</summary>
        public event Action Completed;

        public void Bind(BiomeData b, PlayerController p, EnemyDefinition grunt)
        {
            biome = b;
            player = p;
            gruntDefinition = grunt;
        }

        // =====================================================================
        // Setup
        // =====================================================================

        void Start()
        {
            if (_started) return;
            Begin();
        }

        public void Begin()
        {
            if (_started || player == null || biome == null) return;
            _started = true;

            WorldArt.Props = biome.art != null ? biome.art.worldProps : null;
            _health = player.GetComponent<Health>();
            _combat = player.GetComponent<PlayerCombat>();
            _inventory = player.GetComponent<PlayerInventory>();

            BuildHall();
            BuildSteps();

            if (biome.art != null) LevelAtmosphere.Create(biome.art, player.transform);
            if (_inventory != null) _inventory.ResetForRun(biome.loot);
            if (_health != null) _health.ResetToFull();
            player.ResetTo(SpawnPoint);

            Subscribe();
            ShowStep(0);
        }

        void BuildHall()
        {
            var root = new GameObject("Tutorial Hall").transform;
            for (int i = 0; i < Rooms; i++)
            {
                var cell = new LevelCell
                {
                    Pos = new Vector2Int(i, 0),
                    Index = i,
                    Role = i == 0 ? RoomRole.Start : (i == Rooms - 1 ? RoomRole.Exit : RoomRole.Combat),
                    Mask = (i > 0 ? Openings.Left : (Openings)0) | (i < Rooms - 1 ? Openings.Right : (Openings)0),
                    OnMainPath = true,
                    Progress = 0f
                };
                RoomChunk chunk = ChunkBuilder.Build(cell, root, biome);
                RoomDecor.Populate(chunk, cell, biome);
            }
            Physics2D.SyncTransforms();
        }

        string P(string action) => InputDevices.Prompt(action);

        void BuildSteps()
        {
            // 0 move
            Add("Move", () => $"Move with {InputDevices.MoveName}. Walk to the glowing marker.",
                begin: () => _marker = Marker(24f),
                done: () => player.transform.position.x > 23f, gate: 28f);

            // 1 jump
            Add("Jump", () => $"{InputDevices.PressVerb} {P("Jump")} to jump. Hold it longer to jump higher. Hop over the block.",
                begin: () => { _jumps = 0; Block(40f, 43f, Floor, Floor + 2.2f); _marker = Marker(52f); },
                done: () => _jumps > 0 && player.transform.position.x > 51f, gate: 58f);

            // 2 roll
            Add("Roll", () => $"{InputDevices.PressVerb} {P("Roll")} to roll. You cannot be hit for a moment, and you fit through low gaps. You can even roll in mid-air. Roll through the gap.",
                begin: () => { _rolls = 0; Block(70f, 80f, Floor + 1.3f, Top); _marker = Marker(84f); },
                done: () => _rolls > 0 && player.transform.position.x > 83f, gate: 88f);

            // 3 attack
            Add("Attack", () => $"{InputDevices.PressVerb} {P("Attack1")} to swing your sword. Press it again in rhythm for a three-hit combo. Hit the dummy: {Mathf.Min(_dummyHits, 3)} / 3.",
                begin: () => { _dummyHits = 0; _dummy = EnemyFactory.BuildDummy(new Vector2(105f, Floor)); },
                done: () => _dummyHits >= 3, gate: 118f);

            // 4 parry
            Add("Parry and block", () => $"Enemies show an orange box before they strike. As it fills, {InputDevices.PressVerb.ToLower()} {P("Attack2")} to PARRY: no damage, and they are stunned. Hold {P("Attack2")} to block instead. Parry the Drowned Grunt.",
                begin: SpawnGrunt,
                done: () => _parried || _grunt == null || _grunt.Health.IsDead, gate: 148f);

            // 5 ledges
            Add("Ledges", () => _stage == 0
                    ? "The ledges let you jump up through them from below. Climb to the top one."
                    : (InputDevices.Scheme == InputScheme.Touch
                        ? $"To drop down, pull the stick DOWN and tap {P("Jump")}. Drop back to the floor."
                        : $"To drop down, hold DOWN and press {P("Jump")}. Drop back to the floor."),
                begin: () =>
                {
                    _stage = 0;
                    OneWay(156f, 162f, 5.6f);
                    OneWay(163f, 169f, 8.2f);
                    _marker = Marker(166f, 8.2f);
                },
                done: () =>
                {
                    float x = player.transform.position.x, y = player.transform.position.y;
                    if (_stage == 0 && x > 162.5f && x < 169.5f && y > 8f) { _stage = 1; Remove(_marker); StepChanged?.Invoke(); }
                    return _stage == 1 && y < Floor + 0.5f && x > 150f;
                }, gate: 178f);

            // 6 loot
            if (biome.loot != null)
                Add("Loot", () => $"Stand by the chest and {InputDevices.PressVerb.ToLower()} {P("Interact")} to open it, then {P("Interact")} again to pick up what drops. Better gear means a stronger build.",
                    begin: () =>
                    {
                        var item = LootRoller.Roll(biome.loot, new SeededRng(11), 0.3f, null, 0.6f);
                        _chest = LootFactory.SpawnChest(new Vector2(195f, Floor), item);
                    },
                    done: () => _chest != null && _chest.Opened && FindAnyObjectByType<WorldItem>() == null, gate: 208f);

            // 7 skills and flask
            Add("Skills and flask", () => $"Skills run on cooldowns. {InputDevices.PressVerb} {P("Skill1")} to throw your Powder Keg. Your health flask ({P("Flask")}) heals you; merchants refill it.",
                begin: () =>
                {
                    _skillUsed = false;
                    if (biome.loot != null && biome.loot.skills != null && biome.loot.skills.Length > 0 && _inventory != null)
                        _inventory.EquipSkill(0, ItemInstance.ForSkill(biome.loot.skills[0], ItemRarity.Common));
                },
                done: () => _skillUsed, gate: 238f);

            // 8 mutations
            if (biome.loot != null)
                Add("Mutations", () => $"Altars offer a permanent perk for the run. Walk up, {InputDevices.PressVerb.ToLower()} {P("Interact")}, and choose a mutation.",
                    begin: () =>
                    {
                        var choices = LootRoller.RollMutationChoices(biome.loot, new SeededRng(21), 3, null);
                        if (choices.Count > 0) _altar = LootFactory.SpawnAltar(new Vector2(255f, Floor), choices);
                    },
                    done: () => _altar == null || _altar.Used, gate: 268f);

            // 9 exit
            Add("Begin your run", () => $"That is everything. Scrolls level up your gear, merchants sell upgrades, and death resets the run. Step into the portal and push UP to begin.",
                begin: () =>
                {
                    _exitUsed = false;
                    _exit = ExitDoor.Create(transform, new Vector2(285f, Floor), biome.accent);
                    _exit.Activated += () => _exitUsed = true;
                },
                done: () => _exitUsed, gate: -1f);
        }

        void Add(string title, Func<string> body, Action begin, Func<bool> done, float gate)
        {
            _steps.Add(new Step { Title = title, Body = body, Begin = begin, Done = done, GateX = gate });
        }

        // =====================================================================
        // Flow
        // =====================================================================

        void Update()
        {
            if (!_started || Finished) return;

            // the tutorial is a safe place: never let health run out
            if (_health != null && !_health.IsDead && _health.Fraction < 0.35f) _health.ResetToFull();
            if (_health != null && _health.IsDead) { _health.ResetToFull(); player.ResetTo(SpawnPoint); }

            if (player != null && player.LastIntent.Skill1Pressed) _skillUsed = true;

            Step step = Current;
            if (step != null && step.Done != null && step.Done()) Advance();
        }

        void Advance()
        {
            Step step = Current;
            if (step == null) return;

            Remove(step.Gate);
            Remove(_marker);
            step.End?.Invoke();
            EventBus<ToastEvent>.Raise(new ToastEvent { Text = step.Title + " - done", Colour = new Color(0.55f, 1f, 0.7f) });

            if (_index >= _steps.Count - 1) { Finish(); return; }
            ShowStep(_index + 1);
        }

        void ShowStep(int i)
        {
            _index = i;
            Step step = _steps[i];
            step.Begin?.Invoke();
            if (step.GateX >= 0f) step.Gate = Gate(step.GateX);
            StepChanged?.Invoke();
        }

        void Finish()
        {
            Finished = true;
            MarkSeen();
            Completed?.Invoke();
            StepChanged?.Invoke();
        }

        /// <summary>Leaves the tutorial for the real game (pause menu "Skip tutorial", or the exit portal).</summary>
        public void Skip()
        {
            MarkSeen();
            StartGame();
        }

        public static void MarkSeen()
        {
            if (SettingsService.Current.tutorialSeen) return;
            SettingsService.Current.tutorialSeen = true;
            SettingsService.NotifyChanged(true);
        }

        /// <summary>Tests replace this to observe the hand-over to the game without loading a scene.</summary>
        public static Action StartGameOverride;

        public static void StartGame()
        {
            GameSession.StartNew();
            if (StartGameOverride != null) { StartGameOverride(); return; }
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.GameplayScene);
        }

        /// <summary>Gone this frame (colliders included), not at the end of it.</summary>
        static void Remove(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            Destroy(go);
        }

        void OnDestroy() => Unsubscribe();

        // =====================================================================
        // Events
        // =====================================================================

        void Subscribe()
        {
            player.Jumped += OnJump;
            player.RollStarted += OnRoll;
            if (_combat != null) _combat.Parried += OnParried;
            EventBus<HitEvent>.Subscribe(OnHit);
        }

        void Unsubscribe()
        {
            if (player != null) { player.Jumped -= OnJump; player.RollStarted -= OnRoll; }
            if (_combat != null) _combat.Parried -= OnParried;
            EventBus<HitEvent>.Unsubscribe(OnHit);
        }

        void OnJump() => _jumps++;
        void OnRoll() => _rolls++;
        void OnParried() => _parried = true;

        void OnHit(HitEvent e)
        {
            HitReport r = e.Report;
            if (r.Target != null && _dummy != null && r.Target.gameObject == _dummy.gameObject && r.Result == HitResult.Hit) _dummyHits++;
        }

        // =====================================================================
        // World pieces
        // =====================================================================

        GameObject Marker(float x, float y = Floor)
        {
            var go = new GameObject("Marker");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, 0f);
            var beam = new GameObject("Beam");
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            beam.transform.localScale = new Vector3(1.4f, 3.2f, 1f);
            var sr = beam.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.color = new Color(0.4f, 1f, 0.85f, 0.22f);
            sr.sortingOrder = -2;
            var ring = new GameObject("Ring");
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            ring.transform.localScale = new Vector3(1.6f, 0.3f, 1f);
            var r = ring.AddComponent<SpriteRenderer>();
            r.sprite = ProceduralSprites.Circle;
            r.color = new Color(0.5f, 1f, 0.9f, 0.7f);
            r.sortingOrder = -1;
            return go;
        }

        /// <summary>A shut gate: a glowing barrier you cannot pass until the step is done.</summary>
        GameObject Gate(float x)
        {
            var go = new GameObject("Gate");
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            float h = Top - Floor;
            go.transform.position = new Vector3(x, Floor + h * 0.5f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, h);
            var vis = new GameObject("Glow");
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(0.5f, h, 1f);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square;
            sr.color = new Color(0.45f, 0.95f, 1f, 0.45f);
            sr.sortingOrder = 6;
            Physics2D.SyncTransforms();
            return go;
        }

        void Block(float x0, float x1, float y0, float y1)
        {
            var go = new GameObject("Block");
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, y1 - y0);
            Tile(go.transform, biome.art != null ? biome.art.wall : null, x1 - x0, y1 - y0, biome.ground);
            Physics2D.SyncTransforms();
        }

        void OneWay(float x0, float x1, float top)
        {
            var go = new GameObject("Ledge");
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer(GameLayers.OneWayName);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, top - 0.15f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, 0.3f);
            Tile(go.transform, biome.art != null ? biome.art.platform : null, x1 - x0, 0.38f, biome.platform);
            Physics2D.SyncTransforms();
        }

        static void Tile(Transform parent, Sprite sprite, float w, float h, Color fallback)
        {
            var vis = new GameObject("Look");
            vis.transform.SetParent(parent, false);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -4;
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(w, h);
            }
            else
            {
                sr.sprite = ProceduralSprites.Square;
                sr.color = fallback;
                vis.transform.localScale = new Vector3(w, h, 1f);
            }
        }

        void SpawnGrunt()
        {
            _parried = false;
            if (gruntDefinition == null) return;
            var def = Instantiate(gruntDefinition);
            def.telegraphTime = 1.1f;
            def.recoverTime = 1.5f;
            def.attackCooldown = 1.2f;
            def.damage = 6f;
            def.rearAwareness = 60f;
            def.sightRange = 24f;
            def.loseSightRange = 40f;
            def.patrolRadius = 1f;
            _grunt = EnemyFactory.Build(def, new Vector2(138f, Floor));
        }
    }
}
