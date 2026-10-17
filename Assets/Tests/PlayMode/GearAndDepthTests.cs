using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Items;
using Roughlike.Player;
using Roughlike.Rooms;
using Roughlike.UI;
using Roughlike.Utils;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Parallax planes, grounded torches, and weapon art in the hand, in inventory icons and on the ground.</summary>
    public class GearAndDepthTests
    {
        readonly List<Object> _owned = new List<Object>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var o in _owned) if (o != null) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        Sprite MakeSprite(int w, int h, Vector2? pivot = null)
        {
            var tex = Own(new Texture2D(w, h, TextureFormat.RGBA32, false));
            return Own(Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect));
        }

        // ------------------------------------------------------------------ parallax

        [UnityTest]
        public IEnumerator ForegroundPlanesMoveAgainstTheCameraAndFarPlanesWithIt()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            camGo.tag = "MainCamera";
            _spawned.Add(camGo);

            var art = Own(ScriptableObject.CreateInstance<EnvironmentArt>());
            art.moteCount = 6;
            art.backdrop = new[]
            {
                new EnvironmentArt.BackdropLayer { name = "far", sprite = MakeSprite(400, 4), follow = 0.9f, followY = 1f, sortingOrder = -60 },
                new EnvironmentArt.BackdropLayer { name = "front", sprite = MakeSprite(400, 4), follow = -0.4f, followY = 1f, sortingOrder = 60 }
            };
            var backdrop = ParallaxBackdrop.Create(art);
            _spawned.Add(backdrop.gameObject);

            camGo.transform.position = new Vector3(0f, 0f, -10f);
            yield return null;
            float far0 = backdrop.transform.Find("far 1").position.x, front0 = backdrop.transform.Find("front 1").position.x;

            camGo.transform.position = new Vector3(10f, 0f, -10f);
            yield return null;
            float farDelta = backdrop.transform.Find("far 1").position.x - far0;
            float frontDelta = backdrop.transform.Find("front 1").position.x - front0;

            Assert.AreEqual(9f, farDelta, 0.05f, "a far plane follows the camera almost completely");
            Assert.AreEqual(-4f, frontDelta, 0.05f, "a foreground plane slides the other way, faster than the world");
            Assert.AreEqual(6, backdrop.GetComponentsInChildren<Transform>().Count(t => t.name == "Mote"), "drifting motes spawned");
        }

        // ------------------------------------------------------------------ torches

        [Test]
        public void WallTorchesStandOnStonePosts()
        {
            var biome = Own(ScriptableObject.CreateInstance<BiomeData>());
            var art = Own(ScriptableObject.CreateInstance<EnvironmentArt>());
            art.wall = MakeSprite(4, 4); art.floorTrim = MakeSprite(4, 4); art.platform = MakeSprite(4, 4);
            var tex = Own(new Texture2D(8, 8, TextureFormat.RGBA32, false));
            var props = Own(ScriptableObject.CreateInstance<CharacterArt>());
            props.sheet = tex; props.frameWidth = 4; props.frameHeight = 4; props.pixelsPerUnit = 4f;
            props.clips = new[]
            {
                new CharacterArt.Clip { name = "torch", row = 0, frames = 2, fps = 8f, loop = true },
                new CharacterArt.Clip { name = "brazier", row = 1, frames = 2, fps = 8f, loop = true }
            };
            art.props = props;
            biome.art = art;

            BuiltLevel level = LevelBuilder.Build(LevelGenerator.Generate(3, biome.generator), biome, false);
            _spawned.Add(level.Root);

            var posts = level.Root.GetComponentsInChildren<Transform>().Where(t => t.name == "Torch post").ToList();
            var torches = level.Root.GetComponentsInChildren<Transform>().Where(t => t.name == "torch").ToList();
            Assert.AreEqual(torches.Count, posts.Count, "every torch has a post under it");
            foreach (var torch in torches)
            {
                var post = posts.First(p => p.parent == torch.parent && Mathf.Approximately(p.localPosition.x, torch.localPosition.x));
                Assert.AreEqual(ChunkMetrics.FloorTop, post.localPosition.y, 0.001f, "the post starts on the floor");
                Assert.AreEqual(torch.localPosition.y, post.localPosition.y + post.localScale.y * 3.125f - 0.05f, 0.02f, "and reaches the torch");
            }
        }

        // ------------------------------------------------------------------ held weapons and icons

        WeaponData MakeWeapon(WeaponKind kind, Sprite sprite, float scale = 1f)
        {
            var w = Own(ScriptableObject.CreateInstance<WeaponData>());
            w.kind = kind; w.sprite = sprite; w.heldScale = scale; w.displayName = kind.ToString();
            w.steps = kind == WeaponKind.Melee ? new[] { new AttackStep() } : new AttackStep[0];
            return w;
        }

        CharacterArt PlayerArt(bool shieldUp)
        {
            var tex = Own(new Texture2D(8, 4, TextureFormat.RGBA32, false));
            var art = Own(ScriptableObject.CreateInstance<CharacterArt>());
            art.sheet = tex; art.frameWidth = 8; art.frameHeight = 4; art.pixelsPerUnit = 4f; art.pivot = new Vector2(0.5f, 0f);
            var anchor = new CharacterArt.Anchor { valid = true, hand = new Vector2(6f, 2f), angle = 60f, shieldUp = shieldUp, shield = new Vector2(7f, 2f), stow = new Vector2(1f, 2f) };
            // a player standing in mid-air picks the fall clip, so every locomotion clip carries the same anchors
            art.clips = new[] { "idle", "fall", "jump" }
                .Select(n => new CharacterArt.Clip { name = n, row = 0, frames = 1, fps = 6f, loop = true, anchors = new[] { anchor } }).ToArray();
            return art;
        }

        [UnityTest]
        public IEnumerator PlayerShowsTheWeaponAndShieldItCarries()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var swordSprite = MakeSprite(8, 24, new Vector2(0.5f, 0.2f));
            var shieldSprite = MakeSprite(16, 20);
            var sword = MakeWeapon(WeaponKind.Melee, swordSprite, 1.25f);
            var shield = MakeWeapon(WeaponKind.Shield, shieldSprite, 0.5f);
            shield.shield = new ShieldSettings();

            var art = PlayerArt(false);
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero, "Player", art);
            var combat = PlayerCombatSetup.Attach(pc, sword, shield);
            _spawned.Add(pc.gameObject);
            pc.autoTick = false; combat.autoTick = false;

            yield return null;
            yield return null;
            var gear = pc.GetComponentInChildren<HeldGear>();
            Assert.NotNull(gear, "the combat animator adds the gear renderer");
            Assert.AreSame(swordSprite, gear.ShownWeapon);
            Assert.AreSame(shieldSprite, gear.ShownShield);
            Assert.IsFalse(gear.ShieldRaised, "carried on the back until raised");

            var weaponT = gear.transform.Find("Held Weapon");
            Assert.AreEqual(-30f, Mathf.DeltaAngle(0f, weaponT.localEulerAngles.z), 0.5f, "a blade pointing up at 60 degrees is turned 30 degrees clockwise from the sprite's own up");
            Assert.AreEqual(1.25f, weaponT.localScale.x, 0.001f);
            var stowed = gear.transform.Find("Held Shield");
            Assert.AreEqual(0.35f, stowed.localScale.x, 0.001f, "the stowed shield is smaller");

            // swapping the equipped weapon changes what is drawn
            var dagger = MakeWeapon(WeaponKind.Melee, MakeSprite(6, 12), 1f);
            combat.SetWeapon(0, dagger);
            yield return null;
            Assert.AreSame(dagger.sprite, gear.ShownWeapon, $"clip={gear.GetComponent<SpriteAnimator>().Clip} melee={combat.HeldMelee}");
            combat.SetWeapon(0, null);
            yield return null;
            Assert.IsNull(gear.ShownWeapon, "an empty hand shows no weapon");
        }

        [UnityTest]
        public IEnumerator ARaisedShieldIsHeldInFront()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var shield = MakeWeapon(WeaponKind.Shield, MakeSprite(16, 20), 1f);
            shield.shield = new ShieldSettings();
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero, "Player", PlayerArt(true));
            var combat = PlayerCombatSetup.Attach(pc, MakeWeapon(WeaponKind.Melee, MakeSprite(8, 24)), shield);
            _spawned.Add(pc.gameObject);
            pc.autoTick = false; combat.autoTick = false;
            yield return null;
            yield return null;
            var gear = pc.GetComponentInChildren<HeldGear>();
            var an = gear.GetComponent<SpriteAnimator>();
            Assert.IsTrue(gear.ShieldRaised, $"clip={an.Clip} frame={an.FrameIndex} state={pc.State}");
            var t = gear.transform.Find("Held Shield");
            Assert.AreEqual(1f, t.localScale.x, 0.001f, "full size when raised");
            Assert.Greater(t.localPosition.x, 0f, "out in front of the body");
        }

        [Test]
        public void HeldMeleePrefersTheWeaponUsedLastAndSkipsShields()
        {
            Physics2D.SyncTransforms();
            var cfg = Own(ScriptableObject.CreateInstance<MovementConfig>());
            var a = MakeWeapon(WeaponKind.Melee, null);
            var b = MakeWeapon(WeaponKind.Melee, null);
            var s = MakeWeapon(WeaponKind.Shield, null);
            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero);
            var combat = PlayerCombatSetup.Attach(pc, s, b);
            _spawned.Add(pc.gameObject);
            Assert.AreSame(b, combat.HeldMelee, "a shield in slot 1 is never the 'held weapon'");
            Assert.AreSame(s, combat.HeldShield);
            combat.SetWeapon(0, a);
            Assert.AreSame(a, combat.HeldMelee, "with two melee weapons the first slot is shown until the other is used");
            combat.SetWeapon(1, null);
            Assert.AreSame(a, combat.HeldMelee);
            Assert.IsNull(combat.HeldShield);
        }

        [Test]
        public void InventoryIconsShowTheWeaponSprite()
        {
            var sprite = MakeSprite(8, 24);
            var sword = MakeWeapon(WeaponKind.Melee, sprite);
            var shieldSprite = MakeSprite(16, 20);
            var shield = MakeWeapon(WeaponKind.Shield, shieldSprite);

            var go = new GameObject("Icon", typeof(RectTransform));
            _spawned.Add(go);
            var img = go.AddComponent<Image>();

            UiKit.FillIcon(img, ItemInstance.ForWeapon(sword));
            Assert.AreSame(sprite, img.sprite);
            Assert.IsTrue(img.preserveAspect);
            Assert.AreEqual(-45f, Mathf.DeltaAngle(0f, img.rectTransform.localEulerAngles.z), 0.5f, "blades lie diagonally");

            UiKit.FillIcon(img, ItemInstance.ForWeapon(shield));
            Assert.AreSame(shieldSprite, img.sprite);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, img.rectTransform.localEulerAngles.z), 0.5f, "shields stay upright and the rotation is reset");

            UiKit.FillIcon(img, null);
            Assert.IsFalse(img.preserveAspect);
            Assert.AreEqual(Vector3.one, img.rectTransform.localScale);
        }

        [Test]
        public void DroppedWeaponsLieOnTheGroundAsTheirPaintedSprite()
        {
            var sprite = MakeSprite(8, 24);
            var sword = MakeWeapon(WeaponKind.Melee, sprite);
            var item = ItemInstance.ForWeapon(sword);
            var drop = LootFactory.DropItem(item, Vector2.zero);
            _spawned.Add(drop.gameObject);
            Assert.IsTrue(drop.GetComponentsInChildren<SpriteRenderer>().Any(sr => sr.sprite == sprite), "the drop uses the weapon art");
        }
    }
}
