using System;
using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Player
{
    public enum PlayerState { Grounded, Airborne, WallSlide, Rolling, Ladder, LedgeHang, LedgeClimb }

    /// <summary>Traversal rune abilities. None are enabled in the base kit; meta-progression turns them on (M8).</summary>
    [Flags]
    public enum MovementAbilities
    {
        None = 0,
        DoubleJump = 1 << 0,
        WallJump = 1 << 1,
        AirDash = 1 << 2,    // reserved for M8
        WallClimb = 1 << 3   // reserved for M8
    }

    /// <summary>
    /// Player movement state machine. Reads intent, owns velocity and timers, asks the
    /// <see cref="KinematicMotor2D"/> to move. Combat, audio and VFX hook into the events below.
    /// All tuning values come from <see cref="MovementConfig"/>.
    /// </summary>
    [RequireComponent(typeof(KinematicMotor2D))]
    public class PlayerController : MonoBehaviour
    {
        const float MaxDeltaTime = 1f / 20f;

        [SerializeField] MovementConfig config;
        [SerializeField] MovementAbilities abilities = MovementAbilities.None;
        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        // ---- public state ----
        public PlayerState State { get; private set; } = PlayerState.Airborne;
        public Vector2 Velocity => _vel;
        public int Facing { get; private set; } = 1;
        public bool IsInvulnerable { get; private set; }
        public bool PassesThroughEnemies => State == PlayerState.Rolling;
        public float RollProgress => State == PlayerState.Rolling ? Mathf.Clamp01(_rollTimer / config.rollDuration) : 0f;
        public MovementConfig Config => config;
        public KinematicMotor2D Motor => _motor;
        public MovementAbilities Abilities { get => abilities; set => abilities = value; }
        public bool Grounded => State == PlayerState.Grounded;

        /// <summary>The intent read this tick (before any control lock). Combat reads this after the controller ticks.</summary>
        public PlayerIntent LastIntent { get; private set; }
        public bool IsControlLocked => _controlLock > 0f;
        public int ExtraRollsAvailable => _airRollsLeft;

        /// <summary>Scales run speed while an action (attack, block) is in progress. 1 = normal.</summary>
        public float MoveSpeedScale { get; set; } = 1f;
        /// <summary>When true the facing direction does not follow input (during attack windups and swings).</summary>
        public bool FacingLocked { get; set; }

        // ---- hooks (audio / VFX / combat subscribe) ----
        public event Action<PlayerState, PlayerState> StateChanged;
        public event Action Jumped;
        public event Action DoubleJumped;
        public event Action WallJumped;
        public event Action<float> Landed;       // impact speed (positive)
        public event Action RollStarted;
        public event Action RollEnded;
        public event Action LedgeGrabbed;
        public event Action LadderEntered;
        public event Action DroppedThrough;

        KinematicMotor2D _motor;
        IIntentSource _source;
        Vector2 _vel;
        float _moveX;
        bool _jumpHeld;

        // timers
        float _coyote, _jumpBuffer, _rollBuffer, _rollCooldown, _dropTimer, _wallLock, _controlLock;
        float _ladderCooldown, _ledgeCooldown;
        int _airJumpsLeft, _airRollsLeft;

        // roll
        float _rollTimer, _rollScale;
        int _rollDir;

        // ladder / ledge / platform
        Ladder _ladder;
        LedgeInfo _ledge;
        float _climbTimer;
        MovingPlatform _ridingPlatform;
        Collider2D _ridingCollider;
        int _wallDir;

        void Awake()
        {
            _motor = GetComponent<KinematicMotor2D>();
            _source = GetComponent<IIntentSource>();
            if (config != null)
            {
                _motor.ConfigureBody(config.colliderSize);
                ResetAirResources();
            }
        }

        void ResetAirResources()
        {
            _airRollsLeft = config.airRollsPerAirtime;
            _airJumpsLeft = AirJumpsAllowed();
        }

        public void Configure(MovementConfig newConfig)
        {
            config = newConfig;
            if (_motor == null) _motor = GetComponent<KinematicMotor2D>();
            _motor.ConfigureBody(config.colliderSize);
            ResetAirResources();
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        /// <summary>Teleport and reset to a clean airborne state (respawn, tests).</summary>
        public void ResetTo(Vector2 feetPosition)
        {
            if (State == PlayerState.Rolling) _motor.RestoreFullHeight();
            transform.position = feetPosition;
            Physics2D.SyncTransforms();
            _vel = Vector2.zero;
            IsInvulnerable = false;
            _ladder = null;
            _motor.IgnoreOneWay = false;
            ResetAirResources();
            SetState(PlayerState.Airborne);
        }

        // =====================================================================
        // External forces (combat)
        // =====================================================================

        /// <summary>Adds to the current velocity (attack lunges, shield pushback).</summary>
        public void AddImpulse(Vector2 impulse) => _vel += impulse;

        /// <summary>
        /// Knocks the player back and removes control for <paramref name="lockTime"/> seconds.
        /// Breaks out of rolls, ladders and ledges.
        /// </summary>
        public void ApplyHitReaction(Vector2 knockback, float lockTime)
        {
            if (State == PlayerState.Rolling) EndRoll(false);
            else if (State == PlayerState.Ladder) ExitLadder(PlayerState.Airborne);
            else if (State == PlayerState.LedgeHang || State == PlayerState.LedgeClimb) SetState(PlayerState.Airborne);
            else if (State == PlayerState.WallSlide) SetState(PlayerState.Airborne);

            _vel = knockback;
            _coyote = 0f;
            _controlLock = Mathf.Max(_controlLock, lockTime);
            _jumpBuffer = 0f;
            _rollBuffer = 0f;
            if (knockback.y > 0.1f && State == PlayerState.Grounded) SetState(PlayerState.Airborne);
        }

        // =====================================================================
        // Tick
        // =====================================================================

        public void Tick(float dt)
        {
            if (config == null || _motor == null) return;
            dt = Mathf.Min(dt, MaxDeltaTime);
            if (dt <= 0f) return;

            PlayerIntent intent = _source != null ? _source.Read() : default;
            LastIntent = intent;
            if (_controlLock > 0f)
            {
                _controlLock -= dt;
                intent = default; // stunned: input ignored
            }
            _moveX = Mathf.Abs(intent.Move.x) < config.stickDeadzone ? 0f : Mathf.Sign(intent.Move.x);
            _jumpHeld = intent.JumpHeld;

            TickTimers(intent, dt);
            _motor.IgnoreOneWay = _dropTimer > 0f || State == PlayerState.Ladder;

            switch (State)
            {
                case PlayerState.Grounded: TickGrounded(intent, dt); break;
                case PlayerState.Airborne: TickAirborne(intent, dt); break;
                case PlayerState.WallSlide: TickWallSlide(intent, dt); break;
                case PlayerState.Rolling: TickRolling(intent, dt); break;
                case PlayerState.Ladder: TickLadder(intent, dt); break;
                case PlayerState.LedgeHang: TickLedgeHang(intent); break;
                case PlayerState.LedgeClimb: TickLedgeClimb(dt); break;
            }
        }

        void TickTimers(in PlayerIntent intent, float dt)
        {
            _jumpBuffer = intent.JumpPressed ? config.jumpBufferTime : Mathf.Max(0f, _jumpBuffer - dt);
            _rollBuffer = intent.RollPressed ? config.rollBufferTime : Mathf.Max(0f, _rollBuffer - dt);
            _rollCooldown = Mathf.Max(0f, _rollCooldown - dt);
            _dropTimer = Mathf.Max(0f, _dropTimer - dt);
            _wallLock = Mathf.Max(0f, _wallLock - dt);
            _ladderCooldown = Mathf.Max(0f, _ladderCooldown - dt);
            _ledgeCooldown = Mathf.Max(0f, _ledgeCooldown - dt);
            if (State != PlayerState.Grounded) _coyote = Mathf.Max(0f, _coyote - dt);

            if (_moveX != 0f && !FacingLocked && (State == PlayerState.Grounded || State == PlayerState.Airborne) && _wallLock <= 0f)
                Facing = (int)_moveX;
        }

        // =====================================================================
        // Grounded
        // =====================================================================

        void TickGrounded(in PlayerIntent intent, float dt)
        {
            _coyote = config.coyoteTime;
            _airJumpsLeft = AirJumpsAllowed();
            _airRollsLeft = config.airRollsPerAirtime;

            if (_jumpBuffer > 0f && intent.DownHeld() && _motor.OnOneWay)
            {
                _jumpBuffer = 0f;
                _dropTimer = config.dropThroughTime;
                _motor.IgnoreOneWay = true;
                _vel.y = -3f;
                _coyote = 0f;
                SetState(PlayerState.Airborne);
                DroppedThrough?.Invoke();
                Step(dt, 1f, intent, false);
                return;
            }

            if (TryStartJump()) { Step(dt, 1f, intent, true); return; }
            if (TryStartRoll(intent)) { TickRolling(intent, dt); return; }
            if (TryEnterLadder(intent)) { TickLadder(intent, dt); return; }

            _vel.x = Accelerate(_vel.x, _moveX * config.runSpeed * MoveSpeedScale, config.groundAcceleration,
                config.groundDeceleration, config.groundTurnAcceleration, dt);
            Step(dt, 1f, intent, true);
        }

        // =====================================================================
        // Airborne
        // =====================================================================

        void TickAirborne(in PlayerIntent intent, float dt)
        {
            if (_wallLock <= 0f)
            {
                _vel.x = Accelerate(_vel.x, _moveX * config.runSpeed * MoveSpeedScale, config.airAcceleration,
                    config.airDeceleration, config.airAcceleration, dt);
            }

            if (TryStartJump()) { Step(dt, 1f, intent, true); return; }
            if (TryStartRoll(intent)) { TickRolling(intent, dt); return; }
            if (TryEnterLadder(intent)) { TickLadder(intent, dt); return; }
            if (TryGrabLedge()) return;
            if (TryStartWallSlide()) { TickWallSlide(intent, dt); return; }

            Step(dt, 1f, intent, true);
        }

        // =====================================================================
        // Jump
        // =====================================================================

        int AirJumpsAllowed() => (abilities & MovementAbilities.DoubleJump) != 0 ? config.extraAirJumps : 0;

        bool TryStartJump()
        {
            if (_jumpBuffer <= 0f) return false;

            if (_coyote > 0f)
            {
                _jumpBuffer = 0f;
                _coyote = 0f;
                _vel.y = config.JumpVelocity;
                SetState(PlayerState.Airborne);
                Jumped?.Invoke();
                return true;
            }

            if (State == PlayerState.Airborne && _airJumpsLeft > 0)
            {
                _jumpBuffer = 0f;
                _airJumpsLeft--;
                _vel.y = config.JumpVelocity * config.extraJumpHeightFactor;
                DoubleJumped?.Invoke();
                return true;
            }
            return false;
        }

        // =====================================================================
        // Roll
        // =====================================================================

        bool TryStartRoll(in PlayerIntent intent)
        {
            if (_rollBuffer <= 0f || _rollCooldown > 0f) return false;

            bool groundLike = State == PlayerState.Grounded || _coyote > 0f;
            if (!groundLike && _airRollsLeft <= 0) return false;

            _rollBuffer = 0f;
            if (!groundLike) _airRollsLeft--;

            _rollDir = _moveX != 0f ? (int)_moveX : Facing;
            Facing = _rollDir;
            _rollTimer = 0f;
            _rollScale = config.RollSpeedScale();
            _motor.SetHeight(config.rollColliderHeight);

            if (!groundLike) _vel.y = Mathf.Clamp(_vel.y, -3f, 4f);

            SetState(PlayerState.Rolling);
            RollStarted?.Invoke();
            return true;
        }

        void TickRolling(in PlayerIntent intent, float dt)
        {
            _rollTimer += dt;
            IsInvulnerable = _rollTimer >= config.rollInvulnStart && _rollTimer <= config.rollInvulnEnd;

            bool finished = _rollTimer >= config.rollDuration;
            float progress = _rollTimer / config.rollDuration;

            if (config.jumpCancelsRoll && _jumpBuffer > 0f && (_motor.Grounded || _coyote > 0f))
            {
                EndRoll(true);
                TryStartJump();
                Step(dt, 1f, intent, true);
                return;
            }

            _vel.x = finished ? _rollDir * config.runSpeed * 0.5f : _rollDir * config.RollSpeedAt(progress, _rollScale);

            if (_motor.Grounded) _coyote = config.coyoteTime;
            float gravityScale = _motor.Grounded ? 1f : config.airRollGravityScale;
            Step(dt, gravityScale, intent, false);

            bool hitWall = _rollTimer > 0.08f && ((_rollDir > 0 && _motor.Collisions.right) || (_rollDir < 0 && _motor.Collisions.left));
            bool canStand = _motor.HasRoomForFullHeight();

            if (hitWall && !canStand)
            {
                // Wedged crouched against a wall under a low ceiling: crawl back out the way we came.
                _rollDir = -_rollDir;
                _rollTimer = Mathf.Max(_rollTimer, config.rollDuration);
            }
            else if ((finished || hitWall) && canStand)
            {
                EndRoll(!hitWall);
            }
        }

        void EndRoll(bool keepMomentum)
        {
            _motor.RestoreFullHeight();
            IsInvulnerable = false;
            _rollCooldown = config.rollCooldown;
            _vel.x = keepMomentum ? _rollDir * config.runSpeed * config.rollExitSpeedFraction : 0f;
            SetState(_motor.Grounded ? PlayerState.Grounded : PlayerState.Airborne);
            RollEnded?.Invoke();
        }

        // =====================================================================
        // Ladder
        // =====================================================================

        bool TryEnterLadder(in PlayerIntent intent)
        {
            if (_ladderCooldown > 0f) return false;
            bool up = intent.UpHeld();
            bool down = intent.DownHeld() && State == PlayerState.Grounded;
            if (!up && !down) return false;

            Vector2 feet = transform.position;
            Vector2 probe = up ? feet + Vector2.up * (config.colliderSize.y * 0.5f) : feet + Vector2.down * 0.15f;
            Collider2D col = Physics2D.OverlapPoint(probe, GameLayers.LadderMask);
            if (col == null || !col.TryGetComponent(out Ladder ladder)) return false;

            if (up && ladder.TopY < feet.y + 0.1f) return false;                  // already at the top
            if (down && ladder.TopY - feet.y > 0.4f) return false;                // only from the top edge

            _ladder = ladder;
            _vel = Vector2.zero;
            _jumpBuffer = 0f;
            _airRollsLeft = config.airRollsPerAirtime;
            _airJumpsLeft = AirJumpsAllowed();
            SetState(PlayerState.Ladder);
            LadderEntered?.Invoke();
            return true;
        }

        void TickLadder(in PlayerIntent intent, float dt)
        {
            if (_ladder == null) { ExitLadder(PlayerState.Airborne); return; }
            _motor.IgnoreOneWay = true;

            if (_jumpBuffer > 0f)
            {
                _jumpBuffer = 0f;
                _vel.y = config.JumpVelocity * config.ladderJumpOffMultiplier;
                _vel.x = _moveX * config.runSpeed;
                _ladderCooldown = config.ladderRegrabCooldown;
                _coyote = 0f;
                ExitLadder(PlayerState.Airborne);
                Jumped?.Invoke();
                return;
            }

            float vy = Mathf.Abs(intent.Move.y) > 0.3f ? Mathf.Sign(intent.Move.y) * config.ladderSpeed : 0f;
            float x = transform.position.x;
            float dx = Mathf.MoveTowards(x, _ladder.CenterX, config.ladderSnapSpeed * dt) - x;
            _vel = new Vector2(0f, vy);
            _motor.Move(new Vector2(dx, vy * dt));

            float feetY = transform.position.y;
            if (vy > 0f && feetY >= _ladder.TopY - 0.02f)
            {
                transform.position = new Vector3(transform.position.x, Mathf.Max(feetY, _ladder.TopY), 0f);
                _vel = Vector2.zero;
                _ladderCooldown = config.ladderRegrabCooldown;
                ExitLadder(PlayerState.Grounded);
            }
            else if (vy < 0f && _motor.Collisions.below)
            {
                _vel = Vector2.zero;
                ExitLadder(PlayerState.Grounded);
            }
            else if (vy < 0f && feetY <= _ladder.BottomY)
            {
                _ladderCooldown = config.ladderRegrabCooldown;
                ExitLadder(PlayerState.Airborne);
            }
        }

        void ExitLadder(PlayerState next)
        {
            _ladder = null;
            _motor.IgnoreOneWay = _dropTimer > 0f;
            SetState(next);
        }

        // =====================================================================
        // Ledge
        // =====================================================================

        bool TryGrabLedge()
        {
            if (!config.ledgeGrabEnabled || _ledgeCooldown > 0f) return false;
            if (_vel.y > 1.5f || _moveX == 0f) return false;
            if (!LedgeDetector.TryFind(_motor, config, (int)_moveX, out LedgeInfo info)) return false;

            _ledge = info;
            Facing = info.Dir;
            _vel = Vector2.zero;
            _airRollsLeft = config.airRollsPerAirtime;
            _airJumpsLeft = AirJumpsAllowed();
            transform.position = new Vector3(info.HangFeet.x, info.HangFeet.y, 0f);
            Physics2D.SyncTransforms();
            SetState(PlayerState.LedgeHang);
            LedgeGrabbed?.Invoke();
            return true;
        }

        void TickLedgeHang(in PlayerIntent intent)
        {
            if (_jumpBuffer > 0f || intent.UpHeld())
            {
                _jumpBuffer = 0f;
                _climbTimer = 0f;
                SetState(PlayerState.LedgeClimb);
                return;
            }

            bool away = _moveX != 0f && (int)_moveX == -_ledge.Dir;
            if (intent.DownHeld() || away)
            {
                _ledgeCooldown = config.ledgeRegrabCooldown;
                _coyote = 0f;
                SetState(PlayerState.Airborne);
            }
        }

        void TickLedgeClimb(float dt)
        {
            _climbTimer += dt;
            float t = Mathf.Clamp01(_climbTimer / config.ledgeClimbTime);
            Vector2 start = _ledge.HangFeet;
            Vector2 mid = new Vector2(start.x, _ledge.ClimbFeet.y);
            Vector2 end = _ledge.ClimbFeet;

            Vector2 p = t < 0.6f
                ? Vector2.Lerp(start, mid, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Vector2.Lerp(mid, end, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));

            transform.position = new Vector3(p.x, p.y, 0f);
            Physics2D.SyncTransforms();

            if (t >= 1f)
            {
                _vel = Vector2.zero;
                _ledgeCooldown = config.ledgeRegrabCooldown;
                SetState(PlayerState.Grounded);
            }
        }

        // =====================================================================
        // Wall slide / wall jump (rune)
        // =====================================================================

        bool TryStartWallSlide()
        {
            if ((abilities & MovementAbilities.WallJump) == 0) return false;
            if (_vel.y > 0f || _moveX == 0f) return false;
            bool touching = (_moveX < 0f && _motor.Collisions.left) || (_moveX > 0f && _motor.Collisions.right);
            if (!touching) return false;

            _wallDir = (int)_moveX;
            Facing = _wallDir;
            _airRollsLeft = config.airRollsPerAirtime;
            _airJumpsLeft = AirJumpsAllowed();
            SetState(PlayerState.WallSlide);
            return true;
        }

        void TickWallSlide(in PlayerIntent intent, float dt)
        {
            if (_jumpBuffer > 0f)
            {
                _jumpBuffer = 0f;
                _vel = new Vector2(-_wallDir * config.wallJumpVelocity.x, config.wallJumpVelocity.y);
                Facing = -_wallDir;
                _wallLock = config.wallJumpInputLock;
                SetState(PlayerState.Airborne);
                WallJumped?.Invoke();
                Step(dt, 1f, intent, true);
                return;
            }

            if (TryStartRoll(intent)) { TickRolling(intent, dt); return; }

            // keep pushing into the wall so the contact is re-detected each tick
            _vel.x = _wallDir * 1f;
            float vyOld = _vel.y;
            _vel.y = Mathf.Max(vyOld - config.Gravity * dt, -config.wallSlideSpeed);
            _motor.Move(new Vector2(_vel.x * dt, (vyOld + _vel.y) * 0.5f * dt));

            bool still = (_wallDir < 0 && _motor.Collisions.left) || (_wallDir > 0 && _motor.Collisions.right);
            bool away = _moveX != 0f && (int)_moveX != _wallDir;
            if (_motor.Collisions.below)
            {
                _vel = Vector2.zero;
                SetState(PlayerState.Grounded);
                Landed?.Invoke(0f);
            }
            else if (!still || away || _moveX == 0f)
            {
                SetState(PlayerState.Airborne);
            }
        }

        // =====================================================================
        // Shared integration
        // =====================================================================

        /// <summary>
        /// Applies gravity, moves the motor (including moving-platform carry), then resolves contacts
        /// and ground/air transitions.
        /// </summary>
        void Step(float dt, float gravityScale, in PlayerIntent intent, bool allowStateChange)
        {
            // Carry from a moving platform we stood on last tick.
            Vector2 carry = Vector2.zero;
            if (State == PlayerState.Grounded || State == PlayerState.Rolling)
            {
                if (_ridingPlatform != null) carry = _ridingPlatform.FrameDelta;
            }
            if (carry != Vector2.zero) _motor.Move(carry);

            float vyOld = _vel.y;
            float g = config.Gravity * GravityMultiplier(intent) * gravityScale;
            float maxFall = intent.DownHeld() ? config.fastFallSpeed : config.maxFallSpeed;
            _vel.y = Mathf.Max(vyOld - g * dt, -maxFall);

            Vector2 delta = new Vector2(_vel.x * dt, (vyOld + _vel.y) * 0.5f * dt);
            _motor.Move(delta);

            var c = _motor.Collisions;
            float impact = -_vel.y;
            if (c.above && _vel.y > 0f) _vel.y = 0f;
            if (c.below && _vel.y < 0f) _vel.y = 0f;
            if ((c.left && _vel.x < 0f) || (c.right && _vel.x > 0f)) _vel.x = 0f;

            TrackPlatform(c);

            if (!allowStateChange) return;

            if (State == PlayerState.Grounded && !c.below)
            {
                SetState(PlayerState.Airborne);
            }
            else if (State == PlayerState.Airborne && c.below && vyOld <= 0.01f)
            {
                _airJumpsLeft = AirJumpsAllowed();
                _airRollsLeft = config.airRollsPerAirtime;
                SetState(PlayerState.Grounded);
                Landed?.Invoke(Mathf.Max(0f, impact));
            }
        }

        float GravityMultiplier(in PlayerIntent intent)
        {
            if (_vel.y > 0f)
            {
                if (!_jumpHeld) return config.jumpCutGravityMultiplier;
                if (_vel.y < config.apexHangSpeed) return config.apexHangGravityMultiplier;
                return 1f;
            }
            return intent.DownHeld() ? config.fastFallGravityMultiplier * config.fallGravityMultiplier
                                     : config.fallGravityMultiplier;
        }

        static float Accelerate(float current, float target, float accel, float decel, float turnAccel, float dt)
        {
            float rate;
            if (Mathf.Approximately(target, 0f)) rate = decel;
            else if (!Mathf.Approximately(current, 0f) && Mathf.Sign(current) != Mathf.Sign(target)) rate = turnAccel;
            else rate = accel;
            return Mathf.MoveTowards(current, target, rate * dt);
        }

        void TrackPlatform(in KinematicMotor2D.CollisionInfo c)
        {
            Collider2D ground = c.below ? c.groundCollider : null;
            if (ground == _ridingCollider) return;
            _ridingCollider = ground;
            _ridingPlatform = ground != null && ground.TryGetComponent(out MovingPlatform mp) ? mp : null;
        }

        void SetState(PlayerState next)
        {
            if (next == State) return;
            PlayerState prev = State;
            State = next;
            if (prev == PlayerState.Rolling && next != PlayerState.Rolling) IsInvulnerable = false;
            StateChanged?.Invoke(prev, next);
        }

        public string DebugSummary()
        {
            return $"{State}  v=({_vel.x:0.0},{_vel.y:0.0})  face={Facing}  coyote={_coyote:0.00}\n" +
                   $"airJumps={_airJumpsLeft} airRolls={_airRollsLeft}  invuln={IsInvulnerable}  abilities={abilities}";
        }
    }
}


