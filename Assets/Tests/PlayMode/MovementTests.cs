using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Tests.PlayMode
{
    /// <summary>
    /// Deterministic movement tests: the controller is ticked manually at 60 Hz with a scripted input source
    /// inside small hand-built worlds. They guard the feel-critical numbers (jump height, roll distance, etc.).
    /// </summary>
    public class MovementTests
    {
        const float Dt = 1f / 60f;

        MovementConfig _cfg;
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<MovingPlatform> _platforms = new List<MovingPlatform>();
        PlayerController _p;
        ScriptedIntentSource _in;

        [SetUp]
        public void SetUp()
        {
            _cfg = ScriptableObject.CreateInstance<MovementConfig>();
            _platforms.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            UnityEngine.Object.DestroyImmediate(_cfg);
        }

        // ------------------------------------------------------------------ helpers

        GameObject Box(string name, Vector2 centre, Vector2 size, string layer = GameLayers.GroundName, float angle = 0f)
        {
            var go = new GameObject(name);
            go.layer = LayerMask.NameToLayer(layer);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.AddComponent<BoxCollider2D>().size = size;
            _spawned.Add(go);
            return go;
        }

        /// <summary>Solid ground whose top surface is at y = 0.</summary>
        void Floor(float x0 = -30f, float x1 = 30f) => Box("Floor", new Vector2((x0 + x1) * 0.5f, -1f), new Vector2(x1 - x0, 2f));

        void Ramp(float x, float y, float run, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float length = Mathf.Abs(run / Mathf.Cos(rad));
            Vector2 start = new Vector2(x, y);
            Vector2 end = start + new Vector2(run, run * Mathf.Tan(rad));
            Vector2 normal = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            Box("Ramp", (start + end) * 0.5f - normal, new Vector2(length, 2f), GameLayers.GroundName, angleDeg);
        }

        void SpawnPlayer(Vector2 feet, MovementAbilities abilities = MovementAbilities.None)
        {
            Physics2D.SyncTransforms();
            _p = PlayerFactory.Build(_cfg, PlayerFactory.InputKind.Scripted, null, feet);
            _p.autoTick = false;
            _p.Abilities = abilities;
            _in = _p.GetComponent<ScriptedIntentSource>();
            _spawned.Add(_p.gameObject);
        }

        void Run(float seconds, Action each = null)
        {
            int frames = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                foreach (var mp in _platforms) mp.Tick(Dt);
                _p.Tick(Dt);
                each?.Invoke();
            }
        }

        void Settle() => Run(0.4f);

        // ------------------------------------------------------------------ ground / jump

        [Test]
        public void FallsAndLandsOnGround()
        {
            Floor();
            SpawnPlayer(new Vector2(0f, 3f));
            Run(1.5f);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(0f, _p.transform.position.y, 0.05f);
        }

        [Test]
        public void RunReachesConfiguredSpeed()
        {
            Floor();
            SpawnPlayer(Vector2.zero);
            Settle();
            _in.Move = Vector2.right;
            Run(0.5f);
            Assert.AreEqual(_cfg.runSpeed, _p.Velocity.x, 0.01f);
            _in.Move = Vector2.zero;
            Run(0.4f);
            Assert.AreEqual(0f, _p.Velocity.x, 0.01f);
        }

        [Test]
        public void FullJumpReachesConfiguredHeight()
        {
            Floor();
            SpawnPlayer(Vector2.zero);
            Settle();
            float maxY = 0f;
            _in.PressJump();
            Run(1.2f, () => maxY = Mathf.Max(maxY, _p.transform.position.y));
            Assert.AreEqual(_cfg.jumpHeight, maxY, _cfg.jumpHeight * 0.12f);
        }

        [Test]
        public void TappedJumpIsMuchLowerThanHeldJump()
        {
            Floor();
            SpawnPlayer(Vector2.zero);
            Settle();
            float maxY = 0f;
            _in.PressJump();
            Run(Dt);
            _in.ReleaseJump();
            Run(1f, () => maxY = Mathf.Max(maxY, _p.transform.position.y));
            Assert.Less(maxY, _cfg.jumpHeight * 0.6f);
            Assert.Greater(maxY, 0.3f);
        }

        [Test]
        public void CoyoteTimeAllowsLateJump()
        {
            Floor(-30f, 0f);
            SpawnPlayer(new Vector2(-1f, 0f));
            Settle();
            bool jumped = false;
            _p.Jumped += () => jumped = true;
            _in.Move = Vector2.right;
            int guard = 0;
            while (_p.State != PlayerState.Airborne && guard++ < 300) Run(Dt);
            Assert.AreEqual(PlayerState.Airborne, _p.State, "never left the ledge");
            Run(0.05f); // still inside coyote window
            _in.PressJump();
            Run(Dt * 2);
            Assert.IsTrue(jumped, "coyote jump did not fire");
        }

        [Test]
        public void JumpPressedJustBeforeLandingIsBuffered()
        {
            Floor();
            SpawnPlayer(new Vector2(0f, 0.3f));
            bool jumped = false;
            _p.Jumped += () => jumped = true;
            _in.PressJump(); // pressed while still airborne, lands within the buffer window
            _in.ReleaseJump();
            _in.JumpHeld = true;
            Run(0.6f);
            Assert.IsTrue(jumped);
        }

        // ------------------------------------------------------------------ roll

        [Test]
        public void RollTravelsConfiguredDistanceWithInvulnerability()
        {
            Floor();
            SpawnPlayer(Vector2.zero);
            Settle();
            float x0 = _p.transform.position.x;
            bool sawInvuln = false;
            _in.PressRoll();
            int guard = 0;
            do
            {
                Run(Dt, () => sawInvuln |= _p.IsInvulnerable);
            } while (_p.State == PlayerState.Rolling && guard++ < 200);

            float travelled = _p.transform.position.x - x0;
            Assert.AreEqual(_cfg.rollDistance, travelled, _cfg.rollDistance * 0.1f);
            Assert.IsTrue(sawInvuln, "no i-frames during roll");
            Assert.IsFalse(_p.IsInvulnerable, "i-frames leaked after the roll");
            Assert.AreEqual(_cfg.colliderSize.y, _p.Motor.Box.size.y, 0.001f, "collider height not restored");
        }

        [Test]
        public void RollWorksInMidAirOncePerAirtime()
        {
            Floor();
            SpawnPlayer(new Vector2(0f, 8f));
            Run(0.1f);
            Assert.AreEqual(PlayerState.Airborne, _p.State);

            _in.PressRoll();
            Run(Dt);
            Assert.AreEqual(PlayerState.Rolling, _p.State, "air roll should start");

            int guard = 0;
            while (_p.State == PlayerState.Rolling && guard++ < 100) Run(Dt);
            Assert.AreEqual(PlayerState.Airborne, _p.State);

            Run(_cfg.rollCooldown + 0.05f);
            _in.PressRoll();
            Run(Dt);
            Assert.AreNotEqual(PlayerState.Rolling, _p.State, "second air roll should be refused");

            // Landing refunds it.
            guard = 0;
            while (_p.State != PlayerState.Grounded && guard++ < 600) Run(Dt);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Run(0.2f);
            _in.PressRoll();
            Run(Dt);
            Assert.AreEqual(PlayerState.Rolling, _p.State);
        }

        [Test]
        public void RollPassesUnderLowCeilingAndStandsUpAfter()
        {
            Floor();
            Box("Ceiling", new Vector2(6f, 2.2f), new Vector2(8f, 2f)); // bottom at y = 1.2, x 2..10
            SpawnPlayer(new Vector2(-1f, 0f));
            Settle();
            _in.PressRoll();
            Run(5f);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.Greater(_p.transform.position.x, 10.3f, "player did not make it through the tunnel");
            Assert.AreEqual(_cfg.colliderSize.y, _p.Motor.Box.size.y, 0.001f);
        }

        [Test]
        public void WalkingIntoLowCeilingIsBlocked()
        {
            Floor();
            Box("Ceiling", new Vector2(6f, 2.2f), new Vector2(8f, 2f));
            SpawnPlayer(Vector2.zero);
            Settle();
            _in.Move = Vector2.right;
            Run(1.5f);
            Assert.Less(_p.transform.position.x, 2f);
        }

        // ------------------------------------------------------------------ slopes / platforms

        [Test]
        public void StaysGroundedOverSlopeUpAndDown()
        {
            Floor();
            Ramp(4f, 0f, 6f, 30f);
            float top = 6f * Mathf.Tan(30f * Mathf.Deg2Rad);
            Ramp(10f, top, 6f, -30f);
            SpawnPlayer(Vector2.zero);
            Settle();
            _in.Move = Vector2.right;

            int airFrames = 0, worstAir = 0;
            float maxY = 0f;
            string airInfo = "";
            Run(3f, () =>
            {
                maxY = Mathf.Max(maxY, _p.transform.position.y);
                airFrames = _p.State == PlayerState.Grounded ? 0 : airFrames + 1;
                if (airFrames > worstAir) airInfo = $"x={_p.transform.position.x:0.00} y={_p.transform.position.y:0.00} state={_p.State}";
                worstAir = Mathf.Max(worstAir, airFrames);
            });
            Assert.Greater(maxY, top - 0.3f, "did not climb the slope");
            Assert.LessOrEqual(worstAir, 2, "left the ground on a slope at " + airInfo);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(0f, _p.transform.position.y, 0.1f);
        }

        [Test]
        public void JumpsUpThroughOneWayAndDropsBackDown()
        {
            Floor();
            Box("OneWay", new Vector2(0f, 2.35f), new Vector2(6f, 0.3f), GameLayers.OneWayName); // top at 2.5
            SpawnPlayer(Vector2.zero);
            Settle();
            _in.PressJump();
            Run(1.5f);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(2.5f, _p.transform.position.y, 0.08f, "should have landed on top of the platform");

            _in.JumpHeld = false;
            _in.Move = Vector2.down;
            _in.PressJump();
            Run(0.1f);
            _in.Move = Vector2.zero;
            _in.ReleaseJump();
            Run(1.5f);
            Assert.AreEqual(0f, _p.transform.position.y, 0.05f, "drop-through did not reach the floor");
        }

        [Test]
        public void MovingPlatformCarriesPlayer()
        {
            var go = new GameObject("Mover");
            go.SetActive(false);
            go.layer = LayerMask.NameToLayer(GameLayers.GroundName);
            go.transform.position = new Vector2(0f, -0.25f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(4f, 0.5f);
            var mp = go.AddComponent<MovingPlatform>();
            mp.autoTick = false;
            mp.SetWaypoints(new[] { Vector2.zero, new Vector2(20f, 0f) }, 2f);
            go.SetActive(true);
            _spawned.Add(go);
            _platforms.Add(mp);

            SpawnPlayer(new Vector2(0f, 0f));
            Run(1f);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(2f, _p.transform.position.x, 0.3f, "player did not ride the platform");
        }

        // ------------------------------------------------------------------ ladder / ledge

        [Test]
        public void ClimbsLadderThroughFloorAndBackDown()
        {
            Floor();
            Box("Deck L", new Vector2(-5.5f, 7.5f), new Vector2(10f, 1f));
            Box("Deck R", new Vector2(5.5f, 7.5f), new Vector2(10f, 1f));
            Box("Cap", new Vector2(0f, 7.925f), new Vector2(1f, 0.15f), GameLayers.OneWayName);
            var lad = Box("Ladder", new Vector2(0f, 4f), new Vector2(1f, 8f), GameLayers.LadderName);
            lad.GetComponent<BoxCollider2D>().isTrigger = true;
            lad.AddComponent<Ladder>();

            SpawnPlayer(Vector2.zero);
            Settle();

            _in.Move = Vector2.up;
            bool sawLadder = false;
            Run(3f, () => sawLadder |= _p.State == PlayerState.Ladder);
            Assert.IsTrue(sawLadder, "never grabbed the ladder");
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(8f, _p.transform.position.y, 0.15f);

            _in.Move = Vector2.down;
            Run(3f);
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(0f, _p.transform.position.y, 0.1f);
        }

        [Test]
        public void GrabsAndClimbsLedge()
        {
            Floor();
            Box("Block", new Vector2(6f, 2f), new Vector2(6f, 4f)); // x 3..9, top y = 4
            SpawnPlayer(new Vector2(0f, 0f));
            Settle();

            _in.Move = Vector2.right;
            _in.PressJump();
            bool sawHang = false;
            Run(4f, () =>
            {
                if (_p.State == PlayerState.LedgeHang)
                {
                    sawHang = true;
                    _in.Move = new Vector2(1f, 1f);
                }
                else if (sawHang && _p.State == PlayerState.Grounded)
                {
                    _in.Move = Vector2.zero;
                }
            });
            Assert.IsTrue(sawHang, "never grabbed the ledge");
            Assert.AreEqual(PlayerState.Grounded, _p.State);
            Assert.AreEqual(4f, _p.transform.position.y, 0.1f);
            Assert.Greater(_p.transform.position.x, 3f);
        }

        // ------------------------------------------------------------------ runes

        [Test]
        public void ExtraJumpOnlyWithDoubleJumpRune()
        {
            float Apex(MovementAbilities abilities)
            {
                TearDown(); SetUp();
                Floor();
                SpawnPlayer(Vector2.zero, abilities);
                Settle();
                _in.PressJump();
                Run(0.3f);
                _in.PressJump();
                float maxY = 0f;
                Run(1.5f, () => maxY = Mathf.Max(maxY, _p.transform.position.y));
                return maxY;
            }

            float baseApex = Apex(MovementAbilities.None);
            float doubleApex = Apex(MovementAbilities.DoubleJump);
            Assert.Greater(doubleApex, baseApex + 1f);
        }

        [Test]
        public void WallJumpWorksWithoutSlidingFirstWhileRising()
        {
            Box("Wall L", new Vector2(-3f, 10f), new Vector2(2f, 40f)); // right face x = -2
            Box("Wall R", new Vector2(3f, 10f), new Vector2(2f, 40f));  // left face x = 2
            Floor(-2f, 2f);
            SpawnPlayer(new Vector2(0f, 0f), MovementAbilities.WallJump);
            Settle();

            // jump up while pressing toward the right wall, then tap jump again as soon as we touch it
            _in.Move = Vector2.right;
            _in.PressJump();
            bool kicked = false;
            _p.WallJumped += () => kicked = true;
            Run(0.9f, () => { if (!kicked && _p.State != PlayerState.Grounded) _in.PressJump(); });
            Assert.IsTrue(kicked, "wall jump should fire from a touching wall even while rising or falling");
        }

        [Test]
        public void ChainedWallJumpsClimbAThreeWideShaft()
        {
            // same geometry as the M1 graybox shaft: 3 wide, 11 tall, with an exit ledge on the right wall
            Floor(-10f, 12f);
            Box("Shaft L", new Vector2(0.5f, 4f), new Vector2(1f, 14f));       // x 0..1, y -3..11
            Box("Shaft R", new Vector2(4.5f, 4f), new Vector2(1f, 14f));       // x 4..5
            Box("Exit ledge", new Vector2(6.5f, 10.5f), new Vector2(3f, 1f));  // x 5..8, y 10..11
            SpawnPlayer(new Vector2(2.5f, 0f), MovementAbilities.WallJump);
            Settle();

            _in.Move = Vector2.left;
            float t = 0f, maxY = 0f;
            var trace = new System.Text.StringBuilder();
            Run(7f, () =>
            {
                t += Dt;
                maxY = Mathf.Max(maxY, _p.transform.position.y);
                if (Mathf.Repeat(t, 0.25f) < Dt) trace.Append($"[{t:0.0} x{_p.transform.position.x:0.0} y{_p.transform.position.y:0.0} {_p.State}] ");
                bool nearTop = _p.transform.position.y > 9f;
                if (!nearTop && Mathf.Repeat(t, 0.2f) < Dt) _in.PressJump();
                // near the top, stop kicking and lean toward the exit wall so the ledge grab triggers
                if (nearTop) { _in.Move = Vector2.right; _in.ReleaseJump(); }
                if (_p.State == PlayerState.LedgeHang) _in.Move = new Vector2(1f, 1f);
                if (_p.State == PlayerState.Grounded && _p.transform.position.y > 10.5f) _in.Move = Vector2.zero;
            });

            Assert.Greater(maxY, 9f, "chained wall jumps should climb the shaft");
            Assert.AreEqual(PlayerState.Grounded, _p.State, trace.ToString());
            Assert.AreEqual(11f, _p.transform.position.y, 0.25f, "should end up standing on the exit ledge");
        }

        [Test]
        public void WallSlideAndWallJumpWithRune()
        {
            Box("Wall L", new Vector2(-3f, 10f), new Vector2(2f, 40f)); // right face x = -2
            Box("Wall R", new Vector2(3f, 10f), new Vector2(2f, 40f));  // left face x = 2
            SpawnPlayer(new Vector2(1.5f, 12f), MovementAbilities.WallJump);
            _in.Move = Vector2.right;
            Run(1f);
            Assert.AreEqual(PlayerState.WallSlide, _p.State);
            Assert.GreaterOrEqual(_p.Velocity.y, -_cfg.wallSlideSpeed - 0.01f);

            _in.PressJump();
            Run(Dt * 2);
            Assert.Less(_p.Velocity.x, 0f, "wall jump should kick away from the wall");
            Assert.Greater(_p.Velocity.y, 0f);
        }
    }
}

