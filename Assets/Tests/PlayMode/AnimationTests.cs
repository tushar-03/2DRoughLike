using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Enemies;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>Sprite animation: clip cutting, playback modes, and the enemy and player drivers that pick clips from game state.</summary>
    public class AnimationTests
    {
        const float Dt = 1f / 60f;

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

        /// <summary>A sheet where every cell is a different flat colour, so tests can tell which frame is showing.</summary>
        CharacterArt MakeArt(params (string name, int frames, bool loop)[] clips)
        {
            const int cell = 8;
            int cols = 4, rows = clips.Length;
            var tex = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int r = 0; r < rows; r++)
            for (int f = 0; f < cols; f++)
            {
                var c = new Color32((byte)(40 + f * 50), (byte)(40 + r * 60), 200, 255);
                int top = tex.height - (r + 1) * cell;
                var px = new Color32[cell * cell];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                tex.SetPixels32(f * cell, top, cell, cell, px);
            }
            tex.Apply();
            _owned.Add(tex);

            var art = ScriptableObject.CreateInstance<CharacterArt>();
            art.sheet = tex;
            art.frameWidth = cell; art.frameHeight = cell; art.pixelsPerUnit = cell;
            art.pivot = new Vector2(0.5f, 0f);
            art.clips = new CharacterArt.Clip[clips.Length];
            for (int i = 0; i < clips.Length; i++)
                art.clips[i] = new CharacterArt.Clip { name = clips[i].name, row = i, frames = clips[i].frames, fps = 10f, loop = clips[i].loop };
            _owned.Add(art);
            return art;
        }

        static int FrameIndex(SpriteRenderer sr) => int.Parse(sr.sprite.name.Substring(sr.sprite.name.LastIndexOf('_') + 1));

        (SpriteRenderer sr, SpriteAnimator anim) MakeSprite(CharacterArt art)
        {
            var go = new GameObject("Sprite");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var anim = go.AddComponent<SpriteAnimator>();
            anim.Bind(art);
            return (sr, anim);
        }

        [Test]
        public void ClipsAreCutFromTheirRowWithTheRightPivot()
        {
            var art = MakeArt(("a", 4, true), ("b", 2, false));
            Sprite[] a = art.Frames("a"), b = art.Frames("b");
            Assert.AreEqual(4, a.Length);
            Assert.AreEqual(2, b.Length);
            Assert.AreEqual(0f, a[0].pivot.y, 0.001f, "pivot sits at the feet");
            Assert.AreEqual(0, a[0].rect.x);
            Assert.AreEqual(16, a[2].rect.x, "frames advance left to right");
            Assert.Greater(a[0].rect.y, b[0].rect.y, "row 0 is the top of the sheet");
            Assert.AreEqual(0, art.Frames("missing").Length);
        }

        [Test]
        public void LoopingClipsCycleAndOneShotsHoldTheirLastFrame()
        {
            var art = MakeArt(("loop", 4, true), ("once", 3, false));
            var (sr, anim) = MakeSprite(art);

            anim.Loop("loop");
            Assert.AreEqual(0, FrameIndex(sr));
            anim.Advance(0.11f); Assert.AreEqual(1, FrameIndex(sr));
            anim.Advance(0.2f); Assert.AreEqual(3, FrameIndex(sr));
            anim.Advance(0.1f); Assert.AreEqual(0, FrameIndex(sr), "wraps around");

            anim.PlayOnce("once", 0.3f);
            Assert.IsTrue(anim.IsPlayingOnce);
            anim.Advance(0.15f);
            Assert.AreEqual(1, FrameIndex(sr), "halfway through the duration shows the middle frame");
            anim.Advance(0.5f);
            Assert.AreEqual(2, FrameIndex(sr), "holds the last frame");
            Assert.IsFalse(anim.IsPlayingOnce);
        }

        [Test]
        public void ScrubbingShowsTheFrameForTheGivenProgress()
        {
            var art = MakeArt(("pose", 4, false));
            var (sr, anim) = MakeSprite(art);
            anim.Scrub("pose", 0f); Assert.AreEqual(0, FrameIndex(sr));
            anim.Scrub("pose", 0.5f); Assert.AreEqual(2, FrameIndex(sr));
            anim.Scrub("pose", 1f); Assert.AreEqual(3, FrameIndex(sr));
            anim.Advance(5f);
            Assert.AreEqual(3, FrameIndex(sr), "time does not move a scrubbed clip");
        }

        // ------------------------------------------------------------------ enemies

        EnemyController BuildEnemy(EnemyDefinition def, out SpriteRenderer sr)
        {
            Physics2D.SyncTransforms();
            var e = EnemyFactory.Build(def, new Vector2(0f, 0f), false);
            e.autoTick = false;
            _spawned.Add(e.gameObject);
            sr = e.GetComponentInChildren<SpriteRenderer>();
            return e;
        }

        [Test]
        public void EnemiesWithArtGetAnAnimatorAndDriverAndShowTheirTelegraphPose()
        {
            var art = MakeArt(("idle", 2, true), ("walk", 2, true), ("telegraph", 4, false), ("attack", 3, false), ("hurt", 2, false));
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            _owned.Add(def);
            def.art = art;
            def.telegraphTime = 0.8f;

            var e = BuildEnemy(def, out var sr);
            var anim = sr.GetComponent<SpriteAnimator>();
            Assert.IsNotNull(anim);
            Assert.IsNotNull(sr.GetComponent<EnemyAnimDriver>());
            Assert.AreEqual(Color.white, sr.color, "painted art is not tinted");

            // force the telegraph through the real state machine
            e.BeginTelegraph();
            Assert.AreEqual(EnemyState.Telegraph, e.State);
            sr.GetComponent<EnemyAnimDriver>().SendMessage("LateUpdate");
            Assert.AreEqual("telegraph", anim.Clip);
            Assert.AreEqual(0, FrameIndex(sr), "the telegraph starts on its first pose");

            for (int i = 0; i < 40; i++) e.Tick(Dt);              // 0.67 s of 0.8 s
            sr.GetComponent<EnemyAnimDriver>().SendMessage("LateUpdate");
            Assert.AreEqual(3, FrameIndex(sr), "close to the end of the windup it shows the raised pose");
        }

        [Test]
        public void MissingClipsFallBackInsteadOfBreaking()
        {
            var art = MakeArt(("idle", 2, true));
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            _owned.Add(def);
            def.art = art;

            var e = BuildEnemy(def, out var sr);
            e.BeginTelegraph();
            sr.GetComponent<EnemyAnimDriver>().SendMessage("LateUpdate");
            Assert.AreEqual("idle", sr.GetComponent<SpriteAnimator>().Clip, "no telegraph clip, so it stays on idle");
        }

        [Test]
        public void ShellGuardUsesItsShieldPropAndSwapsItWhenBroken()
        {
            var art = MakeArt(("idle", 1, true), ("shield", 1, false), ("shield_broken", 1, false));
            var def = DefaultEnemies.ShellGuard();
            _owned.Add(def);
            def.art = art;

            var e = BuildEnemy(def, out _);
            var shieldTf = e.Visual.Find("Shield");
            Assert.IsNotNull(shieldTf);
            var shieldSr = shieldTf.GetComponent<SpriteRenderer>();
            var behaviour = (ShieldBehavior)e.Behavior;
            Assert.IsTrue(shieldSr.sprite.name.EndsWith("shield_0"));

            e.Health.TakeHit(new DamageInfo { Amount = 5f, SourceTeam = Team.Player, SourcePosition = new Vector2(3f, 1f), Stagger = def.shieldBreakStagger + 1f });
            e.Tick(Dt);
            Assert.IsTrue(behaviour.ShieldBroken);
            Assert.IsTrue(shieldSr.sprite.name.Contains("shield_broken"), "the broken shield sprite is shown");
        }

        // ------------------------------------------------------------------ player

        [Test]
        public void PlayerVisualPicksLocomotionClipsFromControllerState()
        {
            var art = MakeArt(("idle", 1, true), ("run", 2, true), ("jump", 1, false), ("fall", 1, false), ("roll", 2, true));
            var cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _owned.Add(cfg);

            var floor = new GameObject("Floor");
            _spawned.Add(floor);
            floor.layer = LayerMask.NameToLayer(Roughlike.Core.GameLayers.GroundName);
            floor.transform.position = new Vector2(0f, -1f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 2f);
            Physics2D.SyncTransforms();

            var pc = PlayerFactory.Build(cfg, PlayerFactory.InputKind.Scripted, null, Vector2.zero, "Player", art);
            pc.autoTick = false;
            _spawned.Add(pc.gameObject);
            var anim = pc.GetComponentInChildren<SpriteAnimator>();
            var visual = pc.GetComponentInChildren<PlayerVisual>();
            var input = pc.GetComponent<ScriptedIntentSource>();
            Assert.IsNotNull(anim);

            for (int i = 0; i < 20; i++) pc.Tick(Dt);
            visual.SendMessage("LateUpdate");
            Assert.AreEqual("idle", anim.Clip, "standing still");

            input.Move = new Vector2(1f, 0f);
            for (int i = 0; i < 20; i++) pc.Tick(Dt);
            visual.SendMessage("LateUpdate");
            Assert.AreEqual("run", anim.Clip, "running");

            input.Move = Vector2.zero;
            input.PressJump();
            for (int i = 0; i < 6; i++) pc.Tick(Dt);
            visual.SendMessage("LateUpdate");
            Assert.AreEqual("jump", anim.Clip, "rising");
        }
    }
}
