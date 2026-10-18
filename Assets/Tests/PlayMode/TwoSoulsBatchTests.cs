using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
    /// <summary>Sound, fonts and logo, the bow, the witch's dive, the practice-room loadouts, touch layout and the boss-bar reset.</summary>
    public class TwoSoulsBatchTests
    {
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Sfx.Muted = false;
            LoadoutSelection.Index = -1;
            foreach (var p in UnityEngine.Object.FindObjectsByType<Projectile>()) UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _owned.Clear();
        }

        T Own<T>(T o) where T : UnityEngine.Object { _owned.Add(o); return o; }

        // ------------------------------------------------------------------ sound

        [Test]
        public void EverySoundIsSynthesisedAndAudible()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var clip = Sfx.Clip(id);
                Assert.NotNull(clip, id.ToString());
                Assert.Greater(clip.length, 0.03f, id + " has length");
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                foreach (float f in data) peak = Mathf.Max(peak, Mathf.Abs(f));
                Assert.Greater(peak, 0.2f, id + " is not silent");
                Assert.LessOrEqual(peak, 1.0001f, id + " does not clip");
            }
            Assert.Greater(Sfx.Clip(SfxId.AmbientCatacombs).length, 4f, "ambience loops are long");
        }

        [Test]
        public void GameEventsMakeTheMatchingSounds()
        {
            Sfx.Muted = true;     // keep the test silent; requests are still counted
            var go = new GameObject("Audio");
            _spawned.Add(go);
            GameAudio.Create(go.transform, null);

            EventBus<ToastEvent>.Raise(new ToastEvent { Text = "x", Colour = Color.white });
            Assert.AreEqual(SfxId.Toast, Sfx.LastId);
            EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent { Position = Vector2.zero });
            Assert.AreEqual(SfxId.EnemyDeath, Sfx.LastId);
            EventBus<RoomClearedEvent>.Raise(new RoomClearedEvent());
            Assert.AreEqual(SfxId.RoomClear, Sfx.LastId);
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = Vector2.zero, Radius = 2f, Colour = Color.red });
            Assert.AreEqual(SfxId.Explosion, Sfx.LastId);
            EventBus<BossDefeatedEvent>.Raise(new BossDefeatedEvent());
            Assert.AreEqual(SfxId.BossDeath, Sfx.LastId);
        }

        [Test]
        public void UiButtonsClick()
        {
            Sfx.Muted = true;
            var root = new GameObject("p", typeof(RectTransform));
            _spawned.Add(root);
            var b = UiKit.Button(root.transform, "B", new Vector2(100f, 40f), null);
            b.onClick.Invoke();
            Assert.AreEqual(SfxId.UiClick, Sfx.LastId);
        }

        // ------------------------------------------------------------------ fonts and logo

        [Test]
        public void GameFontsLoadAndUiUsesThem()
        {
            Assert.NotNull(GameFonts.Heading);
            Assert.NotNull(GameFonts.Body);
            Assert.AreNotSame(GameFonts.Heading, GameFonts.Body);
            StringAssert.Contains("Heading", GameFonts.Heading.name, "the engraved face is the real font, not the fallback");
            StringAssert.Contains("Body", GameFonts.Body.name);

            var root = new GameObject("p", typeof(RectTransform));
            _spawned.Add(root);
            var title = UiKit.Label(root.transform, "t", "x", 30, Color.white, TextAnchor.MiddleCenter, true);
            var para = UiKit.Label(root.transform, "p", "x", 30, Color.white);
            Assert.AreSame(GameFonts.Heading, title.font);
            Assert.AreSame(GameFonts.Body, para.font);
        }

        [Test]
        public void TitleLogoSaysTwoSouls()
        {
            var ui = UiRoot.Create();
            _spawned.Add(ui.gameObject);
            TitleLogo.Create(ui.HudLayer, -40f);
            var texts = ui.GetComponentsInChildren<Text>().Select(t => t.text).ToList();
            CollectionAssert.Contains(texts, "TWO SOULS");
            Assert.IsFalse(texts.Any(t => t.Contains("DROWNED")), "the old name is gone");
        }

        // ------------------------------------------------------------------ telegraphs

        [Test]
        public void MeleeTelegraphIsOneArcAndLasersGetAnArrowhead()
        {
            var go = new GameObject("T");
            _spawned.Add(go);
            var t = go.AddComponent<TelegraphIndicator>();

            t.Begin(Vector2.zero, new Vector2(4f, 2f), 1, 1f);
            var dots = go.GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.gameObject.activeSelf).Select(sr => (Vector2)sr.transform.localPosition).ToList();
            // one arc: every dot sits on the same half-ellipse (x from the attacker side out to the far edge)
            foreach (var d in dots)
            {
                float u = (d.x + 2f) / 4f;                       // 0 at the attacker's edge .. 1 at the far edge
                float expected = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                Assert.AreEqual(expected, Mathf.Abs(d.y), 0.05f, "dot " + d + " is on the arc");
            }
            Assert.Greater(dots.Count, 8);

            t.Begin(Vector2.zero, new Vector2(8f, 0.1f), 1, 1f);
            var line = go.GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.gameObject.activeSelf).Select(sr => (Vector2)sr.transform.localPosition).ToList();
            Assert.IsTrue(line.Any(d => d.x > 3.5f && Mathf.Abs(d.y) > 0.1f), "an arrowhead flares at the far end");
        }

        // ------------------------------------------------------------------ bow

        [Test]
        public void ArrowsPointAlongTheirFlightAndPassThroughEnemies()
        {
            Physics2D.SyncTransforms();
            var d1 = EnemyFactory.BuildDummy(new Vector2(4f, 1f));
            var d2 = EnemyFactory.BuildDummy(new Vector2(7f, 1f));
            var d3 = EnemyFactory.BuildDummy(new Vector2(10f, 1f));
            _spawned.Add(d1.gameObject); _spawned.Add(d2.gameObject); _spawned.Add(d3.gameObject);
            Physics2D.SyncTransforms();

            var arrow = Projectile.Spawn(new Vector2(0f, 1.9f), new Vector2(30f, 0f), 12f, Team.Player, 3f, 8f, 3f, 0.25f, Color.white);
            arrow.autoTick = false;
            arrow.SetArrow(1);                                   // pierces one enemy
            for (int i = 0; i < 60 && arrow.IsAlive; i++) { arrow.Tick(0.02f); Physics2D.SyncTransforms(); }

            Assert.AreEqual(1, d1.Hits, "the first enemy was hit");
            Assert.AreEqual(1, d2.Hits, "and the arrow carried on into the second");
            Assert.AreEqual(0, d3.Hits, "but it stops after piercing one");
        }

        [Test]
        public void AWeaponOfKindBowIsTheHeldWeaponAndShieldsAreNot()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var bow = Own(ScriptableObject.CreateInstance<WeaponData>());
            bow.kind = WeaponKind.Bow;
            bow.steps = new[] { new AttackStep() };
            var shield = Own(ScriptableObject.CreateInstance<WeaponData>());
            shield.kind = WeaponKind.Shield;
            shield.steps = new AttackStep[0];
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            var combat = PlayerCombatSetup.Attach(pc, shield, bow);
            _spawned.Add(pc.gameObject);
            Assert.AreSame(bow, combat.HeldMelee);
            Assert.AreSame(shield, combat.HeldShield);
        }

        // ------------------------------------------------------------------ witch

        [Test]
        public void ALandedWitchTakesExtraDamageForAWhile()
        {
            var def = Own(ScriptableObject.CreateInstance<BossDefinition>());
            def.kind = BossKind.Witch; def.maxHealth = 500f; def.size = new Vector2(2f, 3f); def.damage = 10f;
            var boss = BossFactory.Build(def, new Vector2(10f, 4f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            _spawned.Add(boss.gameObject);
            boss.autoTick = false;

            var info = new DamageInfo { Amount = 10f, SourceTeam = Team.Player };
            boss.Filter(ref info);
            Assert.AreEqual(10f, info.Amount, 0.001f, "normal damage while she floats");

            boss.Expose(2f);
            Assert.IsTrue(boss.Exposed);
            info = new DamageInfo { Amount = 10f, SourceTeam = Team.Player };
            boss.Filter(ref info);
            Assert.AreEqual(15f, info.Amount, 0.001f, "extra damage while she is on the floor");

            boss.Tick(0.05f); boss.Tick(0.05f);
            for (int i = 0; i < 45; i++) boss.Tick(0.05f);
            Assert.IsFalse(boss.Exposed, "the opening closes");
        }

        // ------------------------------------------------------------------ loadouts

        LootDatabase DatabaseWithLoadouts(out LoadoutData[] loadouts)
        {
            var db = Own(DefaultContent.CreateDatabase());
            WeaponData W(string n) => db.weapons.First(w => w.name == n);
            loadouts = new[]
            {
                new LoadoutData { id = "bow", displayName = "Bow", slot1 = W("HuntersBow"), slot2 = W("RustedDagger") },
                new LoadoutData { id = "dual", displayName = "Dual", slot1 = W("TwinBlades"), slot2 = W("Buckler") },
                new LoadoutData { id = "great", displayName = "Great", slot1 = W("Greatsword"), slot2 = W("RustedDagger") },
                new LoadoutData { id = "sword_shield", displayName = "Sword & Shield", slot1 = W("BellringerSword"), slot2 = W("TowerShield") }
            };
            db.loadouts = loadouts;
            return db;
        }

        [Test]
        public void TheChosenLoadoutBecomesTheRunsStartingWeapons()
        {
            var db = DatabaseWithLoadouts(out var loadouts);
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(pc, null, null);
            PlayerItemsSetup.Attach(pc);
            _spawned.Add(pc.gameObject);
            var inv = pc.GetComponent<PlayerInventory>();

            LoadoutSelection.Index = -1;
            inv.ResetForRun(db);
            Assert.AreEqual("BellringerSword", inv.Weapons[0].Weapon.name, "no pick: the default");

            LoadoutSelection.Index = 0;
            inv.ResetForRun(db);
            Assert.AreEqual(WeaponKind.Bow, inv.Weapons[0].Weapon.kind);
            Assert.AreEqual("RustedDagger", inv.Weapons[1].Weapon.name);

            LoadoutSelection.Index = 1;
            inv.ResetForRun(db);
            Assert.IsTrue(inv.Weapons[0].Weapon.dual, "dual swords");
            Assert.AreEqual(WeaponKind.Shield, inv.Weapons[1].Weapon.kind);
        }

        [UnityTest]
        public IEnumerator PracticeRoomEquipsEachLoadoutAndOffersAPortal()
        {
            var db = DatabaseWithLoadouts(out var loadouts);
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            biome.loot = db;
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            PlayerCombatSetup.Attach(pc, null, null);
            PlayerItemsSetup.Attach(pc);
            _spawned.Add(pc.gameObject);

            var go = new GameObject("Loadout");
            _spawned.Add(go);
            var director = go.AddComponent<LoadoutDirector>();
            director.Bind(biome, pc);
            yield return null;

            Assert.AreEqual(4, director.Options.Count);
            Assert.AreEqual(3, director.Selected, "sword and shield is the default");
            Assert.AreEqual(2, UnityEngine.Object.FindObjectsByType<TrainingDummy>().Length, "two dummies to practise on");

            var inv = pc.GetComponent<PlayerInventory>();
            director.Select(0);
            Assert.AreEqual(WeaponKind.Bow, inv.Weapons[0].Weapon.kind);
            Assert.AreEqual(0, LoadoutSelection.Index, "the choice is remembered for the run");
            director.Next(1);
            Assert.IsTrue(inv.Weapons[0].Weapon.dual);
            director.Next(-1);
            Assert.AreEqual(WeaponKind.Bow, inv.Weapons[0].Weapon.kind);

            bool begun = false;
            director.Completed += () => begun = true;
            var door = UnityEngine.Object.FindFirstObjectByType<ExitDoor>();
            Assert.NotNull(door, "the portal that starts the run");
            door.Trigger();
            Assert.IsTrue(begun);
            Assert.IsTrue(director.Finished);
        }

        // ------------------------------------------------------------------ touch layout

        [Test]
        public void TouchLayoutIsSavedAndAppliedToTheControls()
        {
            SettingsService.Unload();
            SettingsService.ResetToDefaults();
            var s = SettingsService.Current;
            var spec = TouchControls.Specs.First(x => x.Id == "attack");
            var go = new GameObject("c", typeof(RectTransform));
            _spawned.Add(go);
            var rt = (RectTransform)go.transform;

            TouchControls.Place(rt, spec, s);
            Assert.AreEqual(spec.Position, rt.anchoredPosition, "default place");

            var item = s.GetOrAddTouch("attack", spec.Position.x, spec.Position.y);
            item.x = -500f; item.y = 600f; item.scale = 1.5f;
            s.touchScale = 1.2f;
            TouchControls.Place(rt, spec, s);
            Assert.AreEqual(new Vector2(-500f, 600f), rt.anchoredPosition);
            Assert.AreEqual(1.8f, rt.localScale.x, 0.001f, "own size times global size");

            // survives a save and reload
            var clone = s.Clone();
            Assert.AreEqual(-500f, clone.FindTouch("attack").x, 0.001f);
            Assert.IsNull(clone.FindTouch("jump"), "only edited controls are stored");

            clone.touchOpacity = 5f; clone.touchScale = 9f;
            clone.Sanitise();
            Assert.AreEqual(1f, clone.touchOpacity, 0.001f);
            Assert.AreEqual(1.5f, clone.touchScale, 0.001f);
            SettingsService.ResetToDefaults();
        }

        [Test]
        public void TheLayoutEditorMovesAndResizesControlsAndCanReset()
        {
            SettingsService.Unload();
            SettingsService.ResetToDefaults();
            var ui = UiRoot.Create();
            _spawned.Add(ui.gameObject);
            bool closed = false;
            var editor = TouchLayoutEditor.Open(ui, () => closed = true);

            var spec = TouchControls.Specs.First(x => x.Id == "jump");
            Vector2 before = editor.Copy("jump").anchoredPosition;
            editor.Moved(spec, new Vector2(-120f, 80f));
            Vector2 after = editor.Copy("jump").anchoredPosition;
            Assert.AreEqual(before.x - 120f, after.x, 0.01f);
            Assert.AreEqual(before.y + 80f, after.y, 0.01f);
            Assert.AreEqual(after.x, SettingsService.Current.FindTouch("jump").x, 0.01f, "saved as it moves");

            editor.Moved(spec, new Vector2(-99999f, 99999f));
            Vector2 clamped = editor.Copy("jump").anchoredPosition;
            Assert.Greater(clamped.x, -1920f, "stays on screen");
            Assert.Less(clamped.y, 1080f);

            editor.Select("jump");
            Assert.AreEqual("jump", editor.Selected);

            editor.ResetLayout();
            Assert.AreEqual(spec.Position, editor.Copy("jump").anchoredPosition, "reset puts it back");
            Assert.AreEqual(0, SettingsService.Current.touchLayout.Length);

            editor.Close();
            Assert.IsTrue(closed);
            SettingsService.ResetToDefaults();
        }

        [Test]
        public void SettingsHasATouchTab()
        {
            CollectionAssert.Contains(SettingsView.TabNames, "Touch");
        }

        // ------------------------------------------------------------------ boss bar

        [UnityTest]
        public IEnumerator TheBossBarHidesWhenTheBossIsGoneEvenIfTheLevelIsRebuilt()
        {
            var ui = UiRoot.Create();
            _spawned.Add(ui.gameObject);
            var bar = BossHealthBar.Create(ui);

            var def = Own(ScriptableObject.CreateInstance<BossDefinition>());
            def.displayName = "Test"; def.kind = BossKind.Warden; def.maxHealth = 100f; def.size = new Vector2(2f, 3f); def.damage = 5f;
            var boss = BossFactory.Build(def, new Vector2(10f, 0.05f), EnemyScaling.One, new Rect(0f, 0f, 28f, 14f));
            boss.autoTick = false;
            EventBus<BossSpawnedEvent>.Raise(new BossSpawnedEvent { Boss = boss });
            yield return null;
            Assert.IsTrue(bar.Showing, "shown during the fight");

            UnityEngine.Object.Destroy(boss.gameObject);      // a restart tears the whole level down, boss included
            yield return null;
            yield return null;
            Assert.IsFalse(bar.Showing, "gone with the boss");
        }
    }
}
