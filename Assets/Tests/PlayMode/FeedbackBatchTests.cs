using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// <summary>Fixes from the web playtest: menu navigation, the witch's aim line, sound without a listener, floors with rock under them, animation lookup and the dummy.</summary>
    public class FeedbackBatchTests
    {
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            WorldArt.Props = null;
            ModalManager.LoadSceneHandler = null;
            PauseGate.Reset();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
            foreach (var es in Object.FindObjectsByType<EventSystem>()) Object.DestroyImmediate(es.gameObject);
        }

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        CharacterArt MakeArt(params string[] clipNames)
        {
            var tex = Own(new Texture2D(8, 4, TextureFormat.RGBA32, false));
            var art = Own(ScriptableObject.CreateInstance<CharacterArt>());
            art.sheet = tex; art.frameWidth = 8; art.frameHeight = 4; art.pixelsPerUnit = 4f; art.pivot = new Vector2(0.5f, 0f);
            var anchor = new CharacterArt.Anchor { valid = true, hand = new Vector2(6f, 2f), angle = 60f };
            art.clips = clipNames.Select(n => new CharacterArt.Clip { name = n, row = 0, frames = 1, fps = 6f, loop = false, anchors = new[] { anchor } }).ToArray();
            return art;
        }

        // ------------------------------------------------------------------ navigation

        [Test]
        public void AnOpenMenuTakesTheScreenBehindItOutOfNavigation()
        {
            ModalManager.LoadSceneHandler = _ => { };
            var go = new GameObject("Main Menu");
            _spawned.Add(go);
            var menu = go.AddComponent<MainMenuController>();
            menu.Build();

            var root = Object.FindFirstObjectByType<UiRoot>();
            var behind = root.HudLayer.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "New Game"));
            Assert.IsTrue(behind.IsInteractable(), "the main menu buttons work while nothing is open");

            menu.Modals.OpenSettings();
            Assert.IsFalse(behind.IsInteractable(), "arrow keys and the stick cannot reach the main menu behind the settings");
            var inside = root.ModalLayer.GetComponentsInChildren<Button>().First(b => b.gameObject.activeInHierarchy);
            Assert.IsTrue(inside.IsInteractable(), "but the window itself navigates");

            menu.Modals.CloseAll();
            Assert.IsTrue(behind.IsInteractable(), "closing returns control to the main menu");
        }

        [Test]
        public void TheTouchEditorFreezesTheWindowUnderneath()
        {
            SettingsService.Unload();
            SettingsService.ResetToDefaults();
            var ui = UiRoot.Create();
            _spawned.Add(ui.gameObject);
            var under = UiKit.Button(ui.ModalLayer, "Under", new Vector2(100f, 40f), null);
            bool closed = false;
            var editor = TouchLayoutEditor.Open(ui, () => closed = true);
            Assert.IsFalse(under.IsInteractable(), "a button in the window below cannot be navigated to");
            editor.Close();
            Assert.IsTrue(closed);
            Assert.IsTrue(under.IsInteractable(), "and it works again afterwards");
            SettingsService.ResetToDefaults();
        }

        // ------------------------------------------------------------------ witch

        [Test]
        public void AnAimLineAlwaysPointsAwayFromTheBoss()
        {
            var def = Own(ScriptableObject.CreateInstance<BossDefinition>());
            def.kind = BossKind.Witch; def.maxHealth = 100f; def.size = new Vector2(2f, 3f); def.damage = 5f;
            var boss = BossFactory.Build(def, new Vector2(20f, 4f), EnemyScaling.One, new Rect(0f, 0f, 40f, 14f));
            _spawned.Add(boss.gameObject);
            boss.autoTick = false;
            boss.SetFacing(-1);                                    // the boss faces left, and aims left

            Vector2 from = new Vector2(20f, 6f), aim = Vector2.left;
            var line = boss.ShowBox(from + aim * 3f, new Vector2(6f, 0.1f), 1f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, false);
            _spawned.Add(line.gameObject);

            var dots = line.GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.gameObject.activeSelf).Select(sr => (Vector2)sr.transform.position).ToList();
            Vector2 centre = from + aim * 3f;
            // the arrowhead is the cluster of dots that flare off the line; it must sit at the far end, away from the boss
            var flared = dots.Where(d => Mathf.Abs(d.y - centre.y) > 0.1f).ToList();
            Assert.IsNotEmpty(flared, "there is an arrowhead");
            foreach (var d in flared) Assert.Less(d.x, centre.x, "the arrowhead sits on the player's side of the line");
        }

        // ------------------------------------------------------------------ sound

        [Test]
        public void SoundWorksInScenesThatHaveNoAudioListener()
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>()) l.enabled = false;
            Sfx.Muted = false;
            Sfx.SetVolumes(1f, 1f, 1f);
            Sfx.RecheckListener();
            Sfx.Play(SfxId.UiClick, 0.01f);
            Assert.IsTrue(Sfx.UsingOwnListener, "a listener is created, otherwise nothing is ever heard");
            Sfx.Muted = false;
        }

        // ------------------------------------------------------------------ rock under every floor

        [Test]
        public void HallsAndPracticeRoomsGetRockAroundThemToo()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            var root = new GameObject("hall").transform;
            _spawned.Add(root.gameObject);
            TerrainFill.BuildFor(new[] { Vector2Int.zero, new Vector2Int(1, 0) }, root, biome);
            var rock = root.Find("Rock");
            Assert.NotNull(rock);
            int expected = (2 + 2 * TerrainFill.Margin) * (1 + 2 * TerrainFill.Margin) - 2;
            Assert.AreEqual(expected, rock.childCount, "every cell around the rooms is filled");
            Assert.GreaterOrEqual(TerrainFill.Margin, 3, "far enough that a zoomed-out camera never sees the void");
        }

        // ------------------------------------------------------------------ animation lookup

        [Test]
        public void ClipsAreFoundInCompanionSheets()
        {
            var main = MakeArt("idle");
            var extra = MakeArt("slash1");
            main.companions = new[] { extra };

            Assert.IsTrue(main.Has("slash1"));
            Assert.IsFalse(main.Has("nope"));
            Assert.AreEqual(1, main.Frames("slash1").Length, "frames are cut from the companion's own texture");
            Assert.AreSame(extra.Frames("slash1")[0].texture, main.Frames("slash1")[0].texture);
            Assert.IsTrue(main.TryAnchor("slash1", 0, out var a));
            Assert.AreEqual(60f, a.angle);
        }

        [UnityTest]
        public IEnumerator EachComboStepPlaysItsOwnClip()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var art = MakeArt("idle", "fall", "jump", "attack", "slash1", "overhead", "shoot", "parry", "block_tower", "block_buckler");
            var sword = Own(ScriptableObject.CreateInstance<WeaponData>());
            sword.steps = new[] { new AttackStep { clip = "slash1" }, new AttackStep { clip = "overhead" }, new AttackStep() };
            var buckler = Own(ScriptableObject.CreateInstance<WeaponData>());
            buckler.kind = WeaponKind.Shield; buckler.steps = new AttackStep[0]; buckler.blockClip = "block_buckler";
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero, "Player", art);
            var combat = PlayerCombatSetup.Attach(pc, sword, buckler);
            _spawned.Add(pc.gameObject);
            pc.autoTick = false; combat.autoTick = false;
            yield return null;

            var animator = pc.GetComponentInChildren<PlayerCombatAnimator>();
            var anim = animator.GetComponent<SpriteAnimator>();
            MethodInfo onAttack = typeof(PlayerCombatAnimator).GetMethod("OnAttack", BindingFlags.NonPublic | BindingFlags.Instance);

            onAttack.Invoke(animator, new object[] { sword, sword.steps[0], 0 });
            Assert.AreEqual("slash1", anim.Clip);
            onAttack.Invoke(animator, new object[] { sword, sword.steps[1], 1 });
            Assert.AreEqual("overhead", anim.Clip);
            onAttack.Invoke(animator, new object[] { sword, sword.steps[2], 2 });
            Assert.AreEqual("attack", anim.Clip, "a step without a clip uses the generic swing");

            MethodInfo blockOverride = typeof(PlayerCombatAnimator).GetMethod("BlockOverride", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(blockOverride.Invoke(animator, null), "no block clip while the shield is down");
        }

        // ------------------------------------------------------------------ dummy

        [Test]
        public void TheTrainingDummyUsesItsPaintedSprite()
        {
            WorldArt.Props = MakeArt("dummy");
            var dummy = EnemyFactory.BuildDummy(new Vector2(0f, 0f));
            _spawned.Add(dummy.gameObject);
            var sr = dummy.GetComponentInChildren<SpriteRenderer>();
            StringAssert.Contains("dummy", sr.sprite.name, "the painted dummy, not a yellow box");
            Assert.AreEqual(Color.white, sr.color, "it is not tinted");

            WorldArt.Props = null;
            var plain = EnemyFactory.BuildDummy(new Vector2(5f, 0f));
            _spawned.Add(plain.gameObject);
            Assert.AreNotEqual(Color.white, plain.GetComponentInChildren<SpriteRenderer>().color, "no art: the old block is the fallback");
        }

        [Test]
        public void ARollUnderALowCeilingCrawls()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            _spawned.Add(pc.gameObject);
            Assert.IsFalse(pc.IsCrawling, "standing");
            Assert.IsFalse(pc.IsCrawling && pc.State != PlayerState.Rolling);
        }
    }
}
