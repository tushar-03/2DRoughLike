using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Regression tests for the menu-stacking bug, the traversability rules, haptics, touch controls and the tutorial.</summary>
    public class FixesAndFeaturesTests
    {
        const float Dt = 1f / 60f;

        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            InputGate.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            Haptics.Observer = null;
            TutorialDirector.StartGameOverride = null;
            WorldArt_Reset();
            foreach (var d in Object.FindObjectsByType<TrainingDummy>()) Object.DestroyImmediate(d.gameObject);
            foreach (var e in Object.FindObjectsByType<EnemyController>()) Object.DestroyImmediate(e.gameObject);
            foreach (var go in Object.FindObjectsByType<Collectible>()) Object.DestroyImmediate(go.gameObject);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        static void WorldArt_Reset() => Roughlike.Utils.WorldArt.Props = null;

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        void Floor(float width = 400f)
        {
            var go = new GameObject("Floor");
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(width * 0.5f - 20f, -1f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, 2f);
            _spawned.Add(go);
        }

        PlayerController SpawnPlayer(LootDatabase db = null)
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            var weapon = Own(ScriptableObject.CreateInstance<WeaponData>());
            weapon.kind = WeaponKind.Shield;
            weapon.steps = new AttackStep[0];
            weapon.shield = new ShieldSettings();
            var combat = PlayerCombatSetup.Attach(pc, weapon, weapon);
            PlayerItemsSetup.Attach(pc);
            pc.autoTick = false;
            combat.autoTick = false;
            pc.GetComponent<PlayerDefense>().autoTick = false;
            PlayerItemsSetup.DisableAutoTick(pc.gameObject);
            _spawned.Add(pc.gameObject);
            if (db != null) pc.GetComponent<PlayerInventory>().ResetForRun(db);
            return pc;
        }

        // ------------------------------------------------------------------ menus stacking bug

        [Test]
        public void APausedControllerForgetsItsLastIntent()
        {
            Floor();
            var pc = SpawnPlayer();
            var input = pc.GetComponent<ScriptedIntentSource>();
            input.PressInteract();
            pc.Tick(Dt);
            Assert.IsTrue(pc.LastIntent.InteractPressed);

            pc.Tick(0f);   // paused
            Assert.IsFalse(pc.LastIntent.InteractPressed, "a stale press must not survive a paused frame");
        }

        [Test]
        public void OpeningAnAltarWhilePausedDoesNotReopenItEveryFrame()
        {
            Floor();
            var db = Own(DefaultContent.CreateDatabase());
            var pc = SpawnPlayer(db);
            var sensor = pc.GetComponent<InteractionSensor>();
            sensor.autoTick = false;
            var choices = LootRoller.RollMutationChoices(db, new SeededRng(3), 3, null);
            var altar = LootFactory.SpawnAltar(new Vector2(0.3f, 0f), choices);
            _spawned.Add(altar.gameObject);

            int opened = 0;
            System.Action<OpenMutationEvent> count = e => opened++;
            EventBus<OpenMutationEvent>.Subscribe(count);
            try
            {
                pc.GetComponent<ScriptedIntentSource>().PressInteract();
                pc.Tick(Dt);
                sensor.Tick();
                Assert.AreEqual(1, opened, "one press opens it once");

                Time.timeScale = 0f;                 // the menu pauses the game
                for (int i = 0; i < 10; i++) { pc.Tick(0f); sensor.Tick(); }
                Assert.AreEqual(1, opened, "no extra menus while it is open");
            }
            finally { EventBus<OpenMutationEvent>.Unsubscribe(count); }
        }

        [Test]
        public void TheInputGateBlocksForTheRequestedFrames()
        {
            InputGate.Clear();
            Assert.IsFalse(InputGate.Blocked);
            InputGate.Block(2);
            Assert.IsTrue(InputGate.Blocked);
            InputGate.Clear();
            Assert.IsFalse(InputGate.Blocked);
        }

        // ------------------------------------------------------------------ traversability

        [Test]
        public void EveryGeneratedLevelCanBeFinishedWithJumpingAlone()
        {
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            for (uint seed = 0; seed < 60; seed++)
            {
                BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(seed, biome.generator), biome, false);
                _spawned.Add(level.Root);
                var report = TraversalValidator.Validate(level, cfg);
                Assert.IsTrue(report.Ok, $"seed {seed}: {report}");
                Object.DestroyImmediate(level.Root);
            }
        }

        [Test]
        public void TheValidatorCatchesAnImpossibleGap()
        {
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(5, biome.generator), biome, false);
            _spawned.Add(level.Root);
            Assert.IsTrue(TraversalValidator.Validate(level, cfg).Ok);

            // wall the exit off completely: no jump can pass a floor-to-ceiling barrier
            Vector2 exit = level.Exit.transform.position;
            var wall = new GameObject("Impossible wall");
            wall.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            wall.transform.position = exit + new Vector2(-2.5f, 8f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 40f);
            _spawned.Add(wall);

            Assert.IsFalse(TraversalValidator.Validate(level, cfg).ExitReachable, "an unreachable exit must be reported");
        }

        [Test]
        public void LootAndEnemiesNeverSitOverAFloorHole()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            var grunt = Own(ScriptableObject.CreateInstance<EnemyDefinition>());
            biome.enemyPool = new[] { grunt };
            biome.loot = Own(DefaultContent.CreateDatabase());
            for (uint seed = 0; seed < 30; seed++)
            {
                var layout = LevelGenerator.Generate(seed, biome.generator);
                BuiltLevel level = LevelBuilder.Build(layout, biome, true);
                _spawned.Add(level.Root);
                foreach (var chunk in level.Chunks.Values)
                {
                    if ((chunk.Cell.Mask & Openings.Down) == 0) continue;
                    float local = chunk.LootAnchor.x - chunk.WorldBounds.x;
                    Assert.IsFalse(local > ChunkMetrics.ShaftMin - 0.5f && local < ChunkMetrics.ShaftMax + 0.5f, $"seed {seed}: loot anchor over the hole");
                    foreach (var spawn in chunk.EnemySpawns)
                    {
                        float x = spawn.x - chunk.WorldBounds.x;
                        Assert.IsFalse(x > ChunkMetrics.ShaftMin - 0.5f && x < ChunkMetrics.ShaftMax + 0.5f && spawn.y < chunk.WorldBounds.y + ChunkMetrics.FloorTop + 0.1f,
                            $"seed {seed}: enemy spawn over the hole");
                    }
                }
                Object.DestroyImmediate(level.Root);
            }
        }

        // ------------------------------------------------------------------ haptics

        [Test]
        public void HapticsRespectTheSettingsAndScaleWithStrength()
        {
            var seen = new List<(HapticKind, float)>();
            Haptics.Observer = (k, s) => seen.Add((k, s));

            SettingsService.Current.haptics = true;
            SettingsService.Current.hapticStrength = 0.5f;
            Haptics.Play(HapticKind.Heavy);
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(0.5f, seen[0].Item2, 0.001f);

            SettingsService.Current.haptics = false;
            Haptics.Play(HapticKind.Heavy);
            Assert.AreEqual(1, seen.Count, "off means off");

            SettingsService.Current.haptics = true;
            SettingsService.Current.hapticStrength = 0f;
            Haptics.Play(HapticKind.Heavy);
            Assert.AreEqual(1, seen.Count, "zero strength is the same as off");
        }

        [Test]
        public void CombatEventsProduceTheRightRumble()
        {
            SettingsService.Current.haptics = true;
            SettingsService.Current.hapticStrength = 1f;
            var seen = new List<HapticKind>();
            Haptics.Observer = (k, s) => seen.Add(k);

            var hub = new GameObject("Hub").AddComponent<CombatFeedbackHub>();
            _spawned.Add(hub.gameObject);

            var player = new GameObject("P");
            _spawned.Add(player);
            var ph = player.AddComponent<Health>();
            ph.Initialize(Team.Player, 100f);
            var foe = new GameObject("E");
            _spawned.Add(foe);
            var eh = foe.AddComponent<Health>();
            eh.Initialize(Team.Enemy, 100f);

            ph.TakeHit(new DamageInfo { Amount = 30f, SourceTeam = Team.Enemy });
            CollectionAssert.Contains(seen, HapticKind.Heavy, "a big hit on the player");

            seen.Clear();
            eh.TakeHit(new DamageInfo { Amount = 5f, SourceTeam = Team.Player });
            CollectionAssert.Contains(seen, HapticKind.Light, "landing a hit is a light tick");
        }

        [Test]
        public void NewSettingsSurviveASaveAndReload()
        {
            var s = SettingsService.Current;
            s.haptics = false; s.hapticStrength = 0.3f; s.touchControls = 1; s.tutorialSeen = true;
            SettingsService.Save();
            SettingsService.Unload();
            var loaded = SettingsService.Current;
            Assert.IsFalse(loaded.haptics);
            Assert.AreEqual(0.3f, loaded.hapticStrength, 0.001f);
            Assert.AreEqual(1, loaded.touchControls);
            Assert.IsTrue(loaded.tutorialSeen);
        }

        // ------------------------------------------------------------------ touch controls

        [Test]
        public void TouchControlsFollowTheSettingAndDriveAVirtualGamepad()
        {
            SettingsService.Current.touchControls = 2;
            Assert.IsFalse(TouchControls.ShouldShow(), "off");
            SettingsService.Current.touchControls = 1;
            Assert.IsTrue(TouchControls.ShouldShow(), "always on");
            SettingsService.Current.touchControls = 0;
            Assert.IsFalse(TouchControls.ShouldShow(), "automatic: a desktop is not a touch device");

            SettingsService.Current.touchControls = 1;
            var root = UiRoot.Create();
            _spawned.Add(root.gameObject);
            var touch = TouchControls.Create(root);
            touch.SendMessage("Start");

            Assert.AreEqual(9, touch.GetComponentsInChildren<OnScreenButton>(true).Length, "attack, jump, roll, shield, two skills, use, flask, pause");
            Assert.AreEqual(1, touch.GetComponentsInChildren<OnScreenStick>(true).Length);
            Assert.IsTrue(touch.Visible);
            Assert.IsTrue(InputDevices.TouchUiActive);
        }

        [Test]
        public void PromptsNameTheControlsOfTheActiveDevice()
        {
            InputDevices.Set(InputScheme.KeyboardMouse);
            Assert.AreEqual("E", InputDevices.Prompt("Interact"));
            InputDevices.Set(InputScheme.Gamepad);
            Assert.AreEqual("LB", InputDevices.Prompt("Interact"));
            Assert.AreEqual("A", InputDevices.Prompt("Jump"));
            InputDevices.Set(InputScheme.Touch);
            Assert.AreEqual("USE", InputDevices.Prompt("Interact"));
            Assert.AreEqual("Tap", InputDevices.PressVerb);
            InputDevices.Set(InputScheme.KeyboardMouse);
        }

        // ------------------------------------------------------------------ tutorial

        TutorialDirector BuildTutorial(out PlayerController pc)
        {
            var db = Own(DefaultContent.CreateDatabase());
            pc = SpawnPlayer(db);
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            biome.loot = db;
            var grunt = Own(ScriptableObject.CreateInstance<EnemyDefinition>());

            var go = new GameObject("Tutorial");
            go.SetActive(false);
            var director = go.AddComponent<TutorialDirector>();
            director.Bind(biome, pc, grunt);
            go.SetActive(true);
            director.Begin();
            _spawned.Add(go);
            _spawned.Add(GameObject.Find("Tutorial Hall"));
            return director;
        }

        [Test]
        public void TheTutorialTeachesEachSkillAndKeepsItsGateShutUntilThen()
        {
            var director = BuildTutorial(out var pc);
            var input = pc.GetComponent<ScriptedIntentSource>();
            Assert.AreEqual("Move", director.Current.Title);
            var gate = director.Current.Gate;
            Assert.IsNotNull(gate, "a gate blocks the way on");

            // 1 move: walking to the marker is enough
            pc.ResetTo(new Vector2(25f, 3.05f));
            director.SendMessage("Update");
            Assert.AreEqual("Jump", director.Current.Title);
            Assert.IsFalse(gate.activeSelf, "the first gate opens");

            // 2 jump: actually jump, then get past the block
            for (int i = 0; i < 20; i++) pc.Tick(Dt);
            input.PressJump();
            pc.Tick(Dt);
            pc.ResetTo(new Vector2(52f, 3.05f));
            director.SendMessage("Update");
            Assert.AreEqual("Roll", director.Current.Title);

            // 3 roll
            for (int i = 0; i < 20; i++) pc.Tick(Dt);
            input.PressRoll();
            pc.Tick(Dt);
            pc.ResetTo(new Vector2(84f, 3.05f));
            director.SendMessage("Update");
            Assert.AreEqual("Attack", director.Current.Title);

            // 4 attack: three hits on the dummy
            var dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.IsNotNull(dummy);
            var dh = dummy.GetComponent<Health>();
            for (int i = 0; i < 3; i++) dh.TakeHit(new DamageInfo { Amount = 1f, SourceTeam = Team.Player });
            director.SendMessage("Update");
            Assert.AreEqual("Parry and block", director.Current.Title);
            Assert.IsNotNull(Object.FindAnyObjectByType<EnemyController>(), "the practice grunt appears");
        }

        [Test]
        public void SkippingTheTutorialMarksItSeenAndStartsTheGame()
        {
            var director = BuildTutorial(out _);
            string started = null;
            TutorialDirector.StartGameOverride = () => started = "game";
            SettingsService.Current.tutorialSeen = false;

            director.Skip();

            Assert.IsTrue(SettingsService.Current.tutorialSeen);
            Assert.AreEqual("game", started);
            Assert.AreEqual(SessionMode.NewRun, GameSession.Mode);
            GameSession.Clear();
        }

        [Test]
        public void TutorialTextFollowsTheInputDevice()
        {
            var director = BuildTutorial(out _);
            director.SendMessage("Update");
            // jump to the jump step text directly
            InputDevices.Set(InputScheme.KeyboardMouse);
            string kb = director.Steps[1].Body();
            InputDevices.Set(InputScheme.Touch);
            string touch = director.Steps[1].Body();
            InputDevices.Set(InputScheme.KeyboardMouse);
            StringAssert.Contains("Space", kb);
            StringAssert.Contains("JUMP", touch);
        }
    }
}
