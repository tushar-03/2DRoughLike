using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Main menu, settings, controls and pause menu, driven through their buttons. Scene loads are intercepted.</summary>
    public class MenuTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<string> _loaded = new List<string>();
        MainMenuController _menu;

        [SetUp]
        public void SetUp()
        {
            RunSave.Delete();
            SettingsService.Unload();
            SettingsService.ResetToDefaults();
            GameSession.Clear();
            _loaded.Clear();
            ModalManager.LoadSceneHandler = name => _loaded.Add(name);
        }

        [TearDown]
        public void TearDown()
        {
            ModalManager.LoadSceneHandler = null;
            PauseGate.Reset();
            RunSave.Delete();
            GameSession.Clear();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var es in Object.FindObjectsByType<EventSystem>()) Object.DestroyImmediate(es.gameObject);
            SettingsService.ResetToDefaults();
            GameInput.ResetBindingsForTests();
            // restore anything the settings applier touched
            AudioListener.volume = 1f;
            HitStop.Scale = 1f;
            ScreenShake.Scale = 1f;
            HitReactionVisual.FlashScale = 1f;
            CombatFeedbackHub.DamageNumbersEnabled = true;
        }

        MainMenuController BuildMenu()
        {
            var go = new GameObject("Main Menu");
            _spawned.Add(go);
            _menu = go.AddComponent<MainMenuController>();
            _menu.Build();
            return _menu;
        }

        static Button FindButton(string name)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)) if (b.gameObject.name == name) return b;
            return null;
        }

        static List<Button> ModalButtons(ModalManager m)
        {
            var list = new List<Button>();
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)) list.Add(b);
            return list;
        }

        // ------------------------------------------------------------------ main menu

        [Test]
        public void MenuHasEveryEntryAndContinueNeedsASave()
        {
            var menu = BuildMenu();
            foreach (var name in new[] { "Continue", "New Game", "New Game from Seed", "Settings", "Controls", "Quit" })
                Assert.IsNotNull(FindButton(name), name);

            Assert.IsFalse(menu.ContinueButton.interactable, "no save, nothing to continue");
            Assert.IsNotNull(EventSystem.current);
            Assert.AreEqual(menu.NewGameButton.gameObject, EventSystem.current.currentSelectedGameObject, "controller focus starts on New Game");

            Object.DestroyImmediate(menu.gameObject);
            foreach (var es in Object.FindObjectsByType<EventSystem>()) Object.DestroyImmediate(es.gameObject);
            new RunSave { seed = 4242, summary = "Seed TEST - 5 rooms explored" }.Write();
            var menu2 = BuildMenu();
            Assert.IsTrue(menu2.ContinueButton.interactable);
            bool shown = false;
            foreach (var t in menu2.ContinueButton.GetComponentsInChildren<Text>()) if (t.text.Contains("5 rooms explored")) shown = true;
            Assert.IsTrue(shown, "Continue shows what the saved run looks like");
            Assert.AreEqual(menu2.ContinueButton.gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [Test]
        public void ContinueLoadsTheRunSceneInContinueMode()
        {
            new RunSave { seed = 99 }.Write();
            var menu = BuildMenu();
            menu.ContinueButton.onClick.Invoke();
            Assert.AreEqual(SessionMode.Continue, GameSession.Mode);
            CollectionAssert.AreEqual(new[] { GameSession.GameplayScene }, _loaded);
        }

        [Test]
        public void NewGameStartsImmediatelyWithoutASaveAndAsksFirstWithOne()
        {
            var menu = BuildMenu();
            menu.NewGameButton.onClick.Invoke();
            Assert.AreEqual(SessionMode.NewRun, GameSession.Mode);
            Assert.AreEqual(0u, GameSession.Seed, "random seed");
            Assert.AreEqual(1, _loaded.Count);

            // with a save: a confirmation first, answering no changes nothing
            GameSession.Clear(); _loaded.Clear();
            new RunSave { seed = 7 }.Write();
            menu.NewGameButton.onClick.Invoke();
            Assert.AreEqual(1, menu.Modals.Depth, "confirmation window");
            Assert.AreEqual(0, _loaded.Count);
            Assert.AreEqual(SessionMode.None, GameSession.Mode);

            var buttons = ModalButtons(menu.Modals);
            FindButton("Keep my run").onClick.Invoke();
            Assert.AreEqual(0, menu.Modals.Depth);
            Assert.AreEqual(0, _loaded.Count);
            Assert.IsTrue(RunSave.Exists, "the saved run is untouched");

            menu.NewGameButton.onClick.Invoke();
            FindButton("Start new run").onClick.Invoke();
            Assert.AreEqual(SessionMode.NewRun, GameSession.Mode);
            Assert.AreEqual(1, _loaded.Count);
        }

        [Test]
        public void SeedEntryPassesTheParsedSeedToTheRun()
        {
            var menu = BuildMenu();
            menu.OnNewSeed();
            Assert.AreEqual(1, menu.Modals.Depth);

            var field = Object.FindAnyObjectByType<InputField>();
            Assert.IsNotNull(field);
            field.text = SeedCode.Encode(123456u);
            FindButton("Start run").onClick.Invoke();

            Assert.AreEqual(SessionMode.NewRun, GameSession.Mode);
            Assert.AreEqual(123456u, GameSession.Seed);
            Assert.AreEqual(0, menu.Modals.Depth);

            // any text works as a seed
            GameSession.Clear();
            menu.OnNewSeed();
            Object.FindAnyObjectByType<InputField>().text = "drowned bells";
            FindButton("Start run").onClick.Invoke();
            SeedCode.TryParse("drowned bells", out uint expected);
            Assert.AreEqual(expected, GameSession.Seed);
        }

        // ------------------------------------------------------------------ settings

        [Test]
        public void SettingsScreenChangesAreSavedAndApplied()
        {
            var menu = BuildMenu();
            menu.Modals.OpenSettings();
            Assert.AreEqual(1, menu.Modals.Depth);

            // Gameplay tab: sliders and toggles
            FindButton("Gameplay").onClick.Invoke();
            var sliders = new List<Slider>(Object.FindObjectsByType<Slider>(FindObjectsInactive.Exclude));
            sliders.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            Assert.AreEqual(3, sliders.Count, "shake, hit-stop, flash");

            sliders[0].value = 0.25f;     // screen shake
            sliders[1].value = 0f;        // hit-stop off
            Assert.AreEqual(0.25f, SettingsService.Current.screenShake, 0.001f);
            Assert.AreEqual(0.25f, ScreenShake.Scale, 0.001f, "applied to the live game");
            Assert.AreEqual(0f, HitStop.Scale, 0.001f);

            var toggles = new List<Toggle>(Object.FindObjectsByType<Toggle>(FindObjectsInactive.Exclude));
            toggles.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            Assert.AreEqual(3, toggles.Count, "damage numbers, toggle shield, assist");
            toggles[0].isOn = false;
            toggles[1].isOn = true;
            toggles[2].isOn = true;
            Assert.IsFalse(CombatFeedbackHub.DamageNumbersEnabled);
            Assert.IsTrue(SettingsService.Current.toggleBlock);
            Assert.IsTrue(SettingsService.Current.assistMode);

            // Audio tab
            FindButton("Audio").onClick.Invoke();
            var audio = new List<Slider>(Object.FindObjectsByType<Slider>(FindObjectsInactive.Exclude));
            audio.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            audio[0].value = 0.4f;
            Assert.AreEqual(0.4f, AudioListener.volume, 0.001f, "master volume is applied");

            // it was all written to disk
            SettingsService.Unload();
            Assert.AreEqual(0.4f, SettingsService.Current.master, 0.001f);
            Assert.AreEqual(0.25f, SettingsService.Current.screenShake, 0.001f);
            Assert.IsTrue(SettingsService.Current.toggleBlock);

            // reset puts defaults back
            FindButton("Reset to defaults").onClick.Invoke();
            Assert.AreEqual(1f, SettingsService.Current.master, 0.001f);
            Assert.IsFalse(SettingsService.Current.assistMode);
            Assert.AreEqual(1f, AudioListener.volume, 0.001f);
        }

        [Test]
        public void VideoSettingsOfferResolutionsAndDisplayModes()
        {
            var menu = BuildMenu();
            menu.Modals.OpenSettings();
            var cycles = Object.FindObjectsByType<CycleButton>(FindObjectsInactive.Exclude);
            Assert.GreaterOrEqual(cycles.Length, 3, "resolution, display mode, quality");

            CycleButton display = null;
            foreach (var c in cycles) if (c.Options.Contains("Windowed")) display = c;
            Assert.IsNotNull(display);
            display.Step(1);
            Assert.AreEqual(1, SettingsService.Current.fullscreenMode);
            display.Step(1);
            Assert.AreEqual(2, SettingsService.Current.fullscreenMode);
            display.Step(1);
            Assert.AreEqual(0, SettingsService.Current.fullscreenMode, "wraps around");
        }

        // ------------------------------------------------------------------ controls

        [Test]
        public void ControlsScreenListsBindingsAndResetClearsOverrides()
        {
            var menu = BuildMenu();
            menu.Modals.OpenControls();
            Assert.AreEqual(1, menu.Modals.Depth);

            var texts = new List<string>();
            foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude)) texts.Add(t.text);
            CollectionAssert.Contains(texts, "Space");
            CollectionAssert.Contains(texts, "Health flask");
            CollectionAssert.Contains(texts, "Weapon slot 1");
            Assert.IsFalse(ControlsView.IsRebinding);

            // a saved override shows up the next time the screen is opened
            var jump = GameInput.Jump;
            jump.ApplyBindingOverride(GameInput.FindBinding(jump, GameInput.KeyboardGroup), "<Keyboard>/z");
            SettingsService.Current.bindingOverrides = GameInput.ExportOverrides();
            SettingsService.NotifyChanged();
            Assert.AreEqual("Z", GameInput.DisplayFor(GameInput.Jump, GameInput.KeyboardGroup));

            FindButton("Reset to defaults").onClick.Invoke();
            Assert.AreEqual("Space", GameInput.DisplayFor(GameInput.Jump, GameInput.KeyboardGroup));
            Assert.AreEqual("", SettingsService.Current.bindingOverrides);
        }

        // ------------------------------------------------------------------ pause menu in a run

        [Test]
        public void PauseMenuResumesOpensSettingsAndSavesBeforeQuitting()
        {
            var db = DefaultContent.CreateDatabase();
            var biome = ScriptableObject.CreateInstance<BiomeData>();
            biome.loot = db;
            biome.enemyPool = new[] { ScriptableObject.CreateInstance<EnemyDefinition>() };
            var cfg = ScriptableObject.CreateInstance<MovementConfig>();

            try
            {
                Physics2D.SyncTransforms();
                var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
                PlayerCombatSetup.Attach(pc, null, null);
                PlayerItemsSetup.Attach(pc);
                pc.autoTick = false;
                _spawned.Add(pc.gameObject);

                var runGo = new GameObject("Run");
                runGo.SetActive(false);
                var run = runGo.AddComponent<RunManager>();
                run.AutoStart = false;
                run.Bind(biome, pc);
                runGo.SetActive(true);
                _spawned.Add(runGo);
                run.StartRun(321);
                RunSave.Delete();

                var uiGo = new GameObject("UI");
                uiGo.SetActive(false);
                var ui = uiGo.AddComponent<UiBootstrap>();
                ui.Bind(pc, run);
                uiGo.SetActive(true);
                ui.Build();
                _spawned.Add(uiGo);

                ui.Modals.OpenPause(run);
                Assert.AreEqual(1, ui.Modals.Depth);
                Assert.AreEqual(0f, Time.timeScale, "paused");

                FindButton("Settings").onClick.Invoke();
                Assert.AreEqual(2, ui.Modals.Depth);
                ui.Modals.Pop();
                Assert.AreEqual(1, ui.Modals.Depth);

                FindButton("Resume").onClick.Invoke();
                Assert.AreEqual(0, ui.Modals.Depth);
                Assert.AreEqual(1f, Time.timeScale);

                ui.Modals.OpenPause(run);
                FindButton("Save and quit to menu").onClick.Invoke();
                Assert.IsTrue(RunSave.Exists, "quitting saves the run");
                CollectionAssert.Contains(_loaded, GameSession.MenuScene);
            }
            finally
            {
                Object.DestroyImmediate(db);
                Object.DestroyImmediate(biome);
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
