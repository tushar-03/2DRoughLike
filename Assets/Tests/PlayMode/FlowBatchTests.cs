using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Enemies;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;
using Roughlike.Utils;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>The whole game flow: bosses as checkpoints, continuing, the frozen-game fix, hold-to-charge feedback and menu navigation.</summary>
    public class FlowBatchTests
    {
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            RunSave.Delete();
            HitStop.Scale = 1f;
            HitStop.BaseTimeScale = 1f;
            HitStop.Cancel();
        }

        [TearDown]
        public void TearDown()
        {
            RunSave.Delete();
            HitStop.Cancel();
            Time.timeScale = 1f;
            foreach (var b in Object.FindObjectsByType<BossController>()) Object.DestroyImmediate(b.gameObject);
            foreach (var w in Object.FindObjectsByType<WorldItem>()) Object.DestroyImmediate(w.gameObject);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
            foreach (var es in Object.FindObjectsByType<EventSystem>()) Object.DestroyImmediate(es.gameObject);
        }

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        BossDefinition BossDef(BossKind kind)
        {
            var d = Own(ScriptableObject.CreateInstance<BossDefinition>());
            d.displayName = "Test boss"; d.kind = kind; d.maxHealth = 200f; d.size = new Vector2(2f, 3f); d.damage = 5f;
            return d;
        }

        (RunManager run, PlayerController pc, PlayerInventory inv, LootDatabase db) MakeRun(bool withBosses)
        {
            var db = Own(DefaultContent.CreateDatabase());
            var first = Own(ScriptableObject.CreateInstance<BiomeData>());
            first.displayName = "First"; first.loot = db;
            var second = Own(ScriptableObject.CreateInstance<BiomeData>());
            second.displayName = "Second"; second.loot = db;
            if (withBosses) { first.boss = BossDef(BossKind.Witch); second.boss = BossDef(BossKind.Warden); }

            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(pc, null, null);
            var inv = PlayerItemsSetup.Attach(pc);
            pc.autoTick = false;
            _spawned.Add(pc.gameObject);

            var go = new GameObject("Run");
            go.SetActive(false);
            var run = go.AddComponent<RunManager>();
            run.AutoStart = false;
            run.Bind(new[] { first, second }, pc, null, null);
            go.SetActive(true);
            _spawned.Add(go);
            return (run, pc, inv, db);
        }

        static void DefeatBoss(RunManager run)
            => EventBus<BossDefeatedEvent>.Raise(new BossDefeatedEvent { Position = run.Player.transform.position, Definition = run.Level.Boss != null && run.Level.Boss.Boss != null ? run.Level.Boss.Boss.Definition : null });

        // ------------------------------------------------------------------ the freeze after a boss

        [UnityTest]
        public IEnumerator ADyingBossFinishesEvenWhileTheGameIsFrozen()
        {
            var def = BossDef(BossKind.Witch);
            var boss = BossFactory.Build(def, new Vector2(10f, 4f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            _spawned.Add(boss.gameObject);
            boss.autoTick = false;
            boss.Health.TakeHit(new DamageInfo { Amount = 99999f, SourceTeam = Team.Player });

            Time.timeScale = 0f;                       // a freeze-frame at the moment of death used to stall the whole scene
            yield return new WaitForSecondsRealtime(2.6f);
            Time.timeScale = 1f;
            Assert.IsTrue(boss == null, "the death scene runs on real time and ends by itself");
        }

        [Test]
        public void FreezeFramesAreNeverChainedAndDeathFireworksDoNotFreeze()
        {
            HitStop.Cancel();
            HitStop.Request(0.05f);
            Assert.IsTrue(HitStop.Active);
            Assert.AreEqual(0f, Time.timeScale);
            HitStop.Cancel();
            Assert.AreEqual(1f, Time.timeScale);

            var hub = new GameObject("Hub").AddComponent<CombatFeedbackHub>();
            _spawned.Add(hub.gameObject);
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = Vector2.zero, Radius = 1f, Colour = Color.red, Cosmetic = true });
            Assert.IsFalse(HitStop.Active, "show-only explosions never stop time");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator ABurstOfFreezesCannotHoldTheGameStill()
        {
            HitStop.Cancel();
            HitStop.Request(0.05f);
            while (HitStop.Active) yield return null;
            Assert.AreEqual(1f, Time.timeScale, "the first freeze ended");
            HitStop.Request(0.05f);                    // right after: inside the gap, ignored
            Assert.AreEqual(1f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.15f);
            HitStop.Request(0.05f);
            Assert.AreEqual(0f, Time.timeScale, "later freezes work again");
            HitStop.Cancel();
        }

        // ------------------------------------------------------------------ bosses as checkpoints

        [UnityTest]
        public IEnumerator BeatingTheFirstBossHealsAndSavesTheStartOfTheNextArea()
        {
            var (run, pc, inv, db) = MakeRun(true);
            run.StartRun(555);
            inv.AddGold(120);
            run.PlayerHealth.SetCurrent(20f);

            DefeatBoss(run);
            Assert.IsTrue(run.BossDefeated);
            Assert.AreEqual(run.PlayerHealth.Max, run.PlayerHealth.Current, 0.01f, "the checkpoint heals");

            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));
            Assert.AreEqual(1, save.levelIndex, "the save is the start of the second area");
            Assert.AreEqual(120, save.gold, "everything else carries over");
            Assert.IsEmpty(save.clearedRooms);
            Assert.IsEmpty(save.visited);
            Assert.AreEqual(save.health, run.PlayerHealth.Max, 0.01f);
            Assert.IsFalse(save.bossDefeated, "a new area, not a dead boss");

            // continuing lands at the entrance of area two
            run.StartRun(555, save);
            Assert.AreEqual(1, run.LevelIndex);
            Assert.AreEqual(RunState.Running, run.State);
            Assert.AreEqual(120, run.Inventory.Gold);
            Assert.IsNotNull(run.Level.Boss, "the second boss is waiting");
            Assert.IsFalse(run.Level.Boss.Defeated);
            Assert.AreEqual(run.Layout.Start.Pos, run.CurrentRoom);
        }

        [UnityTest]
        public IEnumerator TheBossRewardOnTheFloorIsNotLostIfYouQuitBeforePickingItUp()
        {
            var (run, pc, inv, db) = MakeRun(true);
            var director = new GameObject("Loot").AddComponent<LootDirector>();
            _spawned.Add(director.gameObject);
            director.Loot = db;
            run.Bind(new[] { run.Biome, run.Biome }, pc, null, director);   // same biome twice is enough here
            run.StartRun(777);
            director.Loot = db;

            DefeatBoss(run);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.NotNull(director.LastBossItem, "a reward was rolled");
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));
            Assert.GreaterOrEqual(save.pendingItem.kind, 0, "it is written to the save while it lies on the floor");

            foreach (var w in Object.FindObjectsByType<WorldItem>()) Object.DestroyImmediate(w.gameObject);   // the scene is torn down
            run.StartRun(777, save);
            Assert.IsTrue(Object.FindObjectsByType<WorldItem>().Length >= 1, "and it drops again when the run is continued");
        }

        [UnityTest]
        public IEnumerator AfterTheFinalBossTheSaveReturnsToAnOpenArena()
        {
            var (run, pc, inv, db) = MakeRun(true);
            run.StartRun(556);
            run.Level.Exit.Trigger();                 // into the final area
            Assert.IsTrue(run.IsFinalLevel);
            Assert.IsNotNull(run.Level.Boss);

            DefeatBoss(run);
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsTrue(RunSave.TryLoad(out RunSave save));
            Assert.AreEqual(1, save.levelIndex);
            Assert.IsTrue(save.bossDefeated, "the last area stays the last area, with its boss dead");

            run.StartRun(556, save);
            Assert.IsTrue(run.BossDefeated);
            Assert.IsTrue(run.Level.Boss.Defeated, "no boss to fight again");
            Assert.IsNull(run.Level.Boss.Boss);
            Assert.IsTrue(run.Level.Exit.gameObject.activeSelf, "the exit is open");

            run.Level.Exit.Trigger();
            Assert.AreEqual(RunState.Complete, run.State, "the game ends");
            Assert.IsFalse(RunSave.Exists, "a finished game leaves nothing to continue");
        }

        // ------------------------------------------------------------------ menu navigation

        [Test]
        public void ControllersMoveAlongRowsAndBetweenThem()
        {
            var root = new GameObject("w", typeof(RectTransform)).GetComponent<RectTransform>();
            _spawned.Add(root.gameObject);
            Button At(string n, float x, float y, float w = 200f)
            {
                var b = UiKit.Button(root, n, new Vector2(w, 50f), null);
                UiKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, 50f));
                return b;
            }
            var a = At("a", -150f, 200f);
            var b2 = At("b", 150f, 200f);
            var c = At("c", 0f, 100f, 700f);
            var d = At("d", -150f, 0f);
            var e = At("e", 150f, 0f);
            MenuNav.AutoLink(root);

            Assert.AreSame(b2, a.navigation.selectOnRight);
            Assert.AreSame(a, b2.navigation.selectOnLeft);
            Assert.IsNull(a.navigation.selectOnLeft, "nothing beyond the ends of a row");
            Assert.AreSame(c, a.navigation.selectOnDown);
            Assert.AreSame(c, b2.navigation.selectOnDown);
            Assert.AreSame(d, c.navigation.selectOnDown, "from a wide control down to the nearest one");
            Assert.IsNull(c.navigation.selectOnLeft, "a lone wide control has no left or right: sliders use those keys themselves");
            Assert.AreSame(c, e.navigation.selectOnUp);
            Assert.AreSame(e, d.navigation.selectOnRight);
        }

        [Test]
        public void SettingsTabsAreReachableWithLeftAndRight()
        {
            SettingsService.Unload();
            SettingsService.ResetToDefaults();
            var ui = UiRoot.Create();
            _spawned.Add(ui.gameObject);
            var window = UiKit.Node("w", ui.ModalLayer);
            UiKit.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 740f));
            var view = SettingsView.Build(window, () => { });
            var tabs = window.GetComponentsInChildren<Button>().Where(b => SettingsView.TabNames.Contains(b.GetComponentInChildren<Text>().text)).ToList();
            Assert.AreEqual(SettingsView.TabNames.Length, tabs.Count);
            tabs.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            for (int i = 0; i + 1 < tabs.Count; i++)
                Assert.AreSame(tabs[i + 1], tabs[i].navigation.selectOnRight, "right moves to the next tab");

            var first = view.FirstSelectable;
            Assert.IsNotNull(first.navigation.selectOnUp, "up from the first setting reaches the tabs");
            SettingsService.ResetToDefaults();
        }

        // ------------------------------------------------------------------ the end of the game

        [Test]
        public void TheTutorialHandsOverToThePracticeRoom()
        {
            string loaded = null;
            TutorialDirector.StartGameOverride = () => loaded = "override";
            TutorialDirector.StartGame();
            TutorialDirector.StartGameOverride = null;
            Assert.AreEqual("override", loaded);
            Assert.AreEqual(SessionMode.NewRun, GameSession.Mode);
            GameSession.Clear();
            StringAssert.Contains("Loadout", GameSession.LoadoutScene);
        }
    }
}
