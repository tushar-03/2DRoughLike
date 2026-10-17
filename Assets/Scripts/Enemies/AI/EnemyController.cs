using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;

namespace Roughlike.Enemies
{
    public enum EnemyState { Patrol, Chase, Telegraph, Attack, Recover, Stagger, Dead }

    public struct EnemyDiedEvent
    {
        public Vector2 Position;
        public EnemyDefinition Definition;
    }

    /// <summary>Raised when an enemy creates another (a summoner's minions) so room tracking can pick it up.</summary>
    public struct MinionSpawnedEvent
    {
        public EnemyController Minion;
        public EnemyController Summoner;
    }

    /// <summary>
    /// Enemy brain: the shared state machine (patrol, chase, telegraph, attack, recover, stagger, dead), poise, knockback,
    /// perception and death. What makes each archetype different lives in an <see cref="EnemyBehavior"/> strategy picked from
    /// the definition. Every attack goes through a telegraph so threats are always readable.
    /// </summary>
    [RequireComponent(typeof(KinematicMotor2D), typeof(Health))]
    public class EnemyController : MonoBehaviour, IAttacker, IHitFilter
    {
        const float Gravity = 45f;
        const float MaxFall = 25f;

        [SerializeField] EnemyDefinition definition;
        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        KinematicMotor2D _motor;
        Health _health;
        TelegraphIndicator _telegraph;
        HitReactionVisual _react;
        StatusEffects _status;
        Transform _visualRoot;
        EnemyBehavior _behavior;

        Vector2 _vel;
        Vector2 _knock;
        float _stateTimer, _cooldown, _poise, _patrolWait, _lostSightTimer, _staggerDuration, _age;
        bool _unparryable;
        int _patrolDir = 1;
        Vector2 _home;
        Vector2 _visualScale = Vector2.one;
        readonly HashSet<Health> _hitSet = new HashSet<Health>();

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public int Facing { get; private set; } = 1;
        public EnemyDefinition Definition => definition;
        /// <summary>Difficulty and depth multipliers (health is applied when the enemy is built).</summary>
        public EnemyScaling Scaling { get; set; } = EnemyScaling.One;
        public float Damage => definition.damage * Scaling.Damage;
        public float TelegraphTime => definition.telegraphTime * Scaling.Telegraph;
        public float RecoverTime => definition.recoverTime * Scaling.Telegraph;
        public float AttackCooldown => definition.attackCooldown * Scaling.Cooldown;
        /// <summary>A speed from the definition with the difficulty's speed multiplier.</summary>
        public float Spd(float speed) => speed * Scaling.Speed;
        public EnemyBehavior Behavior => _behavior;
        public float StateTime => _stateTimer;
        public float StaggerDuration => _staggerDuration;

        Team IAttacker.Team => Team.Enemy;
        Transform IAttacker.Transform => transform;
        public int Priority => 0;

        // ---- what behaviours can read ------------------------------------------------------
        public KinematicMotor2D Motor => _motor;
        public Health Health => _health;
        public Transform Visual => _visualRoot;
        public Vector2 Position => transform.position;
        public Vector2 Home => _home;
        public float Age => _age;
        /// <summary>How fast it actually moved last frame, in units per second (drives walk animations).</summary>
        public float Speed { get; private set; }
        public HitReactionVisual React => _react;
        /// <summary>A stable per-enemy offset so groups do not move in lockstep.</summary>
        public float Phase { get; private set; }
        public bool CanAttack => _cooldown <= 0f;
        public Vector2 Muzzle => (Vector2)transform.position + new Vector2(definition.muzzleOffset.x * Facing, definition.muzzleOffset.y);

        Health PlayerHealth => Health.Player != null && !Health.Player.IsDead ? Health.Player : null;
        public bool HasTarget => PlayerHealth != null;
        public Vector2 TargetCentre => PlayerHealth != null ? PlayerHealth.Center : Position + new Vector2(Facing, 0f);
        public Vector2 TargetFeet => PlayerHealth != null ? (Vector2)PlayerHealth.transform.position : Position;
        public int DirToTarget => TargetCentre.x >= transform.position.x ? 1 : -1;
        public float HorizontalDistance => Mathf.Abs(TargetCentre.x - transform.position.x);
        public bool TargetOnSameLevel => Mathf.Abs(TargetCentre.y - (transform.position.y + definition.size.y * 0.5f)) < 1.6f;
        public bool HasLineOfSightToTarget => HasLineOfSight(TargetCentre);

        public void Initialize(EnemyDefinition def, TelegraphIndicator telegraph, HitReactionVisual react, Transform visualRoot)
        {
            definition = def;
            _telegraph = telegraph;
            _react = react;
            _visualRoot = visualRoot;
            _behavior = EnemyBehaviors.For(def.archetype);
            _behavior.Bind(this);
        }

        void Awake()
        {
            _motor = GetComponent<KinematicMotor2D>();
            _health = GetComponent<Health>();
            _status = GetComponent<StatusEffects>();
            _home = transform.position;
            Phase = Mathf.Abs(_home.x * 7.31f + _home.y * 3.17f) % 6.28f;
        }

        void OnEnable()
        {
            _health.HitTaken += OnHitTaken;
            _health.Died += OnDied;
            _health.AddFilter(this);
        }

        void OnDisable()
        {
            _health.HitTaken -= OnHitTaken;
            _health.Died -= OnDied;
            _health.RemoveFilter(this);
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        /// <summary>Wakes the enemy up and has it start chasing (minions, being hit).</summary>
        public void Alert()
        {
            if (State == EnemyState.Patrol) SetState(EnemyState.Chase);
        }

        // =====================================================================
        // Tick
        // =====================================================================

        public void Tick(float dt)
        {
            if (definition == null || _behavior == null || State == EnemyState.Dead) return;
            dt = Mathf.Min(dt, 1f / 20f);

            _age += dt;
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _poise = Mathf.Max(0f, _poise - 8f * dt);
            _stateTimer += dt;
            _behavior.Tick(dt);

            // stun status: act like a stagger for as long as it lasts
            if (_status != null && _status.IsStunned && State != EnemyState.Stagger)
                EnterStagger(_status.StunRemaining);

            Vector2 desired = Vector2.zero;

            switch (State)
            {
                case EnemyState.Patrol: desired = TickPatrol(dt); break;
                case EnemyState.Chase: desired = TickChase(dt); break;
                case EnemyState.Telegraph: TickTelegraph(); break;
                case EnemyState.Attack: desired = TickAttack(dt); break;
                case EnemyState.Recover: desired = TickRecover(dt); break;
                case EnemyState.Stagger: TickStagger(); break;
            }

            if (State == EnemyState.Dead) return;
            if (_status != null && State != EnemyState.Attack) desired *= _status.SpeedMultiplier;
            Move(desired, dt);
        }

        // ---- patrol -----------------------------------------------------------------

        Vector2 TickPatrol(float dt)
        {
            if (CanSeeTarget(false)) { SetState(EnemyState.Chase); return Vector2.zero; }

            Vector2 v = _behavior.Patrol(dt);
            if (_behavior.Flying && Mathf.Abs(v.x) > 0.3f) SetFacing(v.x > 0f ? 1 : -1);
            return v;
        }

        /// <summary>The default ground patrol: pace back and forth around the home point, turning at walls and ledges.</summary>
        public float GroundPatrol(float dt)
        {
            if (_patrolWait > 0f) { _patrolWait -= dt; return 0f; }

            float fromHome = transform.position.x - _home.x;
            bool atRadius = (_patrolDir > 0 && fromHome > definition.patrolRadius) || (_patrolDir < 0 && fromHome < -definition.patrolRadius);
            if (atRadius || BlockedAhead(_patrolDir))
            {
                _patrolDir = -_patrolDir;
                _patrolWait = 0.7f;
                SetFacing(_patrolDir);
                return 0f;
            }

            SetFacing(_patrolDir);
            return _patrolDir * Spd(definition.patrolSpeed);
        }

        // ---- chase ------------------------------------------------------------------

        Vector2 TickChase(float dt)
        {
            if (!HasTarget) { SetState(EnemyState.Patrol); return Vector2.zero; }

            SetFacing(DirToTarget);

            if (!CanSeeTarget(true))
            {
                _lostSightTimer += dt;
                if (_lostSightTimer > 2f) { _lostSightTimer = 0f; SetState(EnemyState.Patrol); return Vector2.zero; }
            }
            else _lostSightTimer = 0f;

            return _behavior.Chase(dt);
        }

        // ---- telegraph / attack / recover -----------------------------------------------

        /// <summary>Starts the attack windup. Behaviours call this from Chase when they decide to attack.</summary>
        public void BeginTelegraph()
        {
            if (State == EnemyState.Telegraph) return;
            SetState(EnemyState.Telegraph);

            TelegraphSpec spec = _behavior.Telegraph();
            _unparryable = spec.Unparryable;
            if (_telegraph != null)
            {
                int facing = Mathf.Abs(spec.Angle) > 0.01f ? 1 : Facing;
                _telegraph.Begin(spec.Centre, spec.Size, facing, TelegraphTime, spec.Unparryable, spec.Angle, spec.Circle);
            }
        }

        /// <summary>Lets a behaviour move the danger area while it is still tracking (laser, dive line).</summary>
        public void RetargetTelegraph(Vector2 centre, Vector2 size, float angleDeg)
        {
            if (_telegraph != null) _telegraph.Retarget(centre, size, angleDeg);
        }

        void TickTelegraph()
        {
            float t = Mathf.Clamp01(_stateTimer / TelegraphTime);
            if (_react != null) _react.SetWarning(t);
            _behavior.TelegraphTick(t);
            if (_stateTimer >= TelegraphTime) SetState(EnemyState.Attack);
        }

        Vector2 TickAttack(float dt)
        {
            Vector2 v = _behavior.AttackTick(dt);
            if (State != EnemyState.Attack) return Vector2.zero;   // the behaviour ended or interrupted it
            if (_behavior.AttackFinished(_stateTimer)) SetState(EnemyState.Recover);
            return v;
        }

        /// <summary>Ends the attack early and enters the recover window.</summary>
        public void EndAttack()
        {
            if (State == EnemyState.Attack) SetState(EnemyState.Recover);
        }

        Vector2 TickRecover(float dt)
        {
            if (_stateTimer >= RecoverTime)
            {
                _cooldown = AttackCooldown;
                SetState(EnemyState.Chase);
                return Vector2.zero;
            }
            return _behavior.Recover(dt);
        }

        void TickStagger()
        {
            if (_stateTimer >= _staggerDuration) SetState(EnemyState.Chase);
        }

        // ---- attack helpers for behaviours ------------------------------------------------

        public Vector2 MeleeCentre()
            => (Vector2)transform.position + new Vector2(definition.hitOffset.x * Facing, definition.hitOffset.y);

        /// <summary>A damage template filled from the definition. Knockback points the way the enemy faces.</summary>
        public DamageInfo MakeAttack(DamageTags tags)
        {
            return new DamageInfo
            {
                Amount = Damage,
                SourceTeam = Team.Enemy,
                Attacker = this,
                SourcePosition = transform.position,
                Knockback = new Vector2(definition.knockback.x * Facing, definition.knockback.y),
                Stagger = definition.stagger,
                HitStop = 0.07f,
                Shake = 0.3f,
                Tags = _unparryable ? tags | DamageTags.Unparryable : tags
            };
        }

        /// <summary>Hits everything on the player's side inside the box, once per attack.</summary>
        public int StrikeBox(Vector2 centre, Vector2 size, DamageInfo info)
            => MeleeStrike.Strike(centre, size, LayerMask.GetMask(GameLayers.PlayerName), info, _hitSet);

        /// <summary>Creates another enemy next to this one, in the same room, already hunting the player.</summary>
        public EnemyController SpawnMinion(EnemyDefinition def, Vector2 position)
        {
            if (def == null) return null;
            EnemyController m = EnemyFactory.Build(def, position);
            m.transform.SetParent(transform.parent, true);
            m.autoTick = autoTick;
            m.Alert();
            EventBus<MinionSpawnedEvent>.Raise(new MinionSpawnedEvent { Minion = m, Summoner = this });
            return m;
        }

        public void SetVisualScale(Vector2 scale)
        {
            _visualScale = scale;
            ApplyVisualScale();
        }

        // =====================================================================
        // Movement
        // =====================================================================

        void Move(Vector2 desired, float dt)
        {
            Vector2 before = transform.position;
            MoveBody(desired, dt);
            Speed = dt > 0f ? ((Vector2)transform.position - before).magnitude / dt : 0f;
        }

        void MoveBody(Vector2 desired, float dt)
        {
            if (_behavior.Flying)
            {
                Vector2 v = desired + _knock;
                _knock = Vector2.MoveTowards(_knock, Vector2.zero, 28f * dt);
                _motor.Move(v * dt);
                _vel = Vector2.zero;
                return;
            }

            float vx = desired.x + _knock.x;
            _knock.x = Mathf.MoveTowards(_knock.x, 0f, 28f * dt);

            float vyOld = _vel.y;
            _vel.y = Mathf.Max(vyOld - Gravity * dt, -MaxFall);

            _motor.Move(new Vector2(vx * dt, (vyOld + _vel.y) * 0.5f * dt));
            var c = _motor.Collisions;
            if (c.below && _vel.y < 0f) _vel.y = 0f;
            if (c.above && _vel.y > 0f) _vel.y = 0f;
        }

        /// <summary>True when a wall or a drop is in the way of walking in <paramref name="dir"/>.</summary>
        public bool BlockedAhead(int dir)
        {
            var c = _motor.Collisions;
            if ((dir > 0 && c.right) || (dir < 0 && c.left)) return true;
            return LedgeAhead(dir);
        }

        /// <summary>True when there is no floor just ahead of the front foot.</summary>
        public bool LedgeAhead(int dir)
        {
            Vector2 pos = transform.position;
            Vector2 origin = pos + new Vector2(dir * (definition.size.x * 0.5f + 0.25f), 0.3f);
            return Physics2D.Raycast(origin, Vector2.down, 1.1f, GameLayers.WalkableMask).collider == null;
        }

        // =====================================================================
        // Perception
        // =====================================================================

        bool CanSeeTarget(bool alreadyChasing)
        {
            if (!HasTarget) return false;

            Vector2 tp = TargetCentre;
            Vector2 eye = (Vector2)transform.position + new Vector2(0f, definition.size.y * 0.8f);
            float dx = tp.x - eye.x;
            float dist = Mathf.Abs(dx);
            float range = alreadyChasing ? definition.loseSightRange : definition.sightRange;
            if (dist > range || Mathf.Abs(tp.y - eye.y) > definition.verticalSight) return false;

            bool inFront = (int)Mathf.Sign(dx) == Facing;
            if (!alreadyChasing && !inFront && dist > definition.rearAwareness) return false;

            return HasLineOfSight(tp);
        }

        bool HasLineOfSight(Vector2 targetPoint)
        {
            Vector2 eye = (Vector2)transform.position + new Vector2(0f, definition.size.y * 0.8f);
            return Physics2D.Linecast(eye, targetPoint, GameLayers.GroundMask).collider == null;
        }

        // =====================================================================
        // State helpers
        // =====================================================================

        void SetState(EnemyState next)
        {
            if (State == EnemyState.Telegraph)
            {
                if (_telegraph != null) _telegraph.Hide();
                if (_react != null) _react.SetWarning(0f);
                if (next != EnemyState.Attack) SetVisualScale(Vector2.one);
            }

            State = next;
            _stateTimer = 0f;
            if (next == EnemyState.Attack)
            {
                _hitSet.Clear();
                _behavior.AttackBegin();
            }
        }

        void SetFacing(int dir)
        {
            if (dir == 0) return;
            Facing = dir;
            ApplyVisualScale();
        }

        void ApplyVisualScale()
        {
            if (_visualRoot != null) _visualRoot.localScale = new Vector3(Facing * _visualScale.x, _visualScale.y, 1f);
        }

        public void EnterStagger(float duration)
        {
            if (State == EnemyState.Dead) return;
            _staggerDuration = duration;
            SetState(EnemyState.Stagger);
            _behavior.OnStaggered();
        }

        // =====================================================================
        // Combat interfaces
        // =====================================================================

        void IAttacker.OnAttackParried(Vector2 point, float stunDuration)
        {
            EnterStagger(stunDuration);
            _poise = 0f;
        }

        /// <summary>The behaviour sees the hit first (shields, armour); stunned enemies take bonus damage, the reward for parrying.</summary>
        public HitResult Filter(ref DamageInfo info)
        {
            HitResult r = _behavior != null ? _behavior.Filter(ref info) : HitResult.Hit;
            if (r != HitResult.Hit) return r;

            if (State == EnemyState.Stagger && _staggerDuration > definition.staggerTime + 0.05f)
                info.Amount *= definition.stunnedDamageMultiplier;
            return HitResult.Hit;
        }

        void OnHitTaken(HitReport r)
        {
            if (r.Result != HitResult.Hit || r.Amount <= 0f || State == EnemyState.Dead) return;

            Vector2 kb = r.Info.Knockback * (1f - definition.knockbackResistance);
            _knock.x = kb.x;
            if (_behavior.Flying) _knock.y = kb.y;
            else if (kb.y > 0.1f) _vel.y = kb.y;

            _behavior.OnHit(r);

            _poise += r.Info.Stagger;
            if (_poise >= definition.staggerThreshold)
            {
                _poise = 0f;
                EnterStagger(definition.staggerTime);
            }
            else if (State == EnemyState.Patrol)
            {
                SetState(EnemyState.Chase);   // being hit alerts it
            }
        }

        void OnDied()
        {
            SetState(EnemyState.Dead);
            if (_telegraph != null) _telegraph.Hide();
            if (_react != null) _react.SetWarning(0f);
            _behavior.OnDied();
            EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent { Position = transform.position, Definition = definition });
            StartCoroutine(DeathSequence());
        }

        System.Collections.IEnumerator DeathSequence()
        {
            // Disable the hurtbox immediately so corpses cannot be hit or block anything.
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (_react != null) _react.enabled = false;   // stop it overwriting the fade
            var sr = GetComponentInChildren<SpriteRenderer>();
            float t = 0f;
            Vector3 start = transform.position;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float k = t / 0.45f;
                if (sr != null) { var c = sr.color; c.a = 1f - k; sr.color = c; }
                transform.position = start + new Vector3(-Facing * 0.8f * k, 0.3f * Mathf.Sin(k * Mathf.PI), 0f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
