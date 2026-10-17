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

    /// <summary>
    /// Enemy brain: a small explicit state machine (patrol, chase, telegraph, attack, recover, stagger, dead)
    /// driven by an <see cref="EnemyDefinition"/>. Melee and ranged enemies share it; only the attack
    /// execution differs. Every attack goes through a telegraph so threats are always readable.
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

        Vector2 _vel;
        Vector2 _knock;
        float _stateTimer, _cooldown, _poise, _patrolWait, _lostSightTimer;
        bool _attackFired;
        int _patrolDir = 1;
        Vector2 _home;
        readonly HashSet<Health> _hitSet = new HashSet<Health>();

        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public int Facing { get; private set; } = 1;
        public EnemyDefinition Definition => definition;
        public float StateTime => _stateTimer;

        Team IAttacker.Team => Team.Enemy;
        Transform IAttacker.Transform => transform;
        public int Priority => 0;

        public void Initialize(EnemyDefinition def, TelegraphIndicator telegraph, HitReactionVisual react, Transform visualRoot)
        {
            definition = def;
            _telegraph = telegraph;
            _react = react;
            _visualRoot = visualRoot;
        }

        void Awake()
        {
            _motor = GetComponent<KinematicMotor2D>();
            _health = GetComponent<Health>();
            _status = GetComponent<StatusEffects>();
            _home = transform.position;
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

        // =====================================================================
        // Tick
        // =====================================================================

        public void Tick(float dt)
        {
            if (definition == null || State == EnemyState.Dead) return;
            dt = Mathf.Min(dt, 1f / 20f);

            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _poise = Mathf.Max(0f, _poise - 8f * dt);
            _stateTimer += dt;

            // stun status: act like a stagger for as long as it lasts
            if (_status != null && _status.IsStunned && State != EnemyState.Stagger)
                EnterStagger(_status.StunRemaining);

            float desiredVx = 0f;

            switch (State)
            {
                case EnemyState.Patrol: desiredVx = TickPatrol(dt); break;
                case EnemyState.Chase: desiredVx = TickChase(dt); break;
                case EnemyState.Telegraph: TickTelegraph(); break;
                case EnemyState.Attack: TickAttack(); break;
                case EnemyState.Recover: TickRecover(); break;
                case EnemyState.Stagger: TickStagger(); break;
            }

            if (_status != null) desiredVx *= _status.SpeedMultiplier;
            Move(desiredVx, dt);
        }

        // ---- patrol -----------------------------------------------------------------

        float TickPatrol(float dt)
        {
            if (CanSeeTarget(false)) { SetState(EnemyState.Chase); return 0f; }

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
            return _patrolDir * definition.patrolSpeed;
        }

        // ---- chase ------------------------------------------------------------------

        float TickChase(float dt)
        {
            Health target = Health.Player;
            if (target == null || target.IsDead) { SetState(EnemyState.Patrol); return 0f; }

            Vector2 tp = target.Center;
            Vector2 pos = transform.position;
            float dx = tp.x - pos.x;
            float dist = Mathf.Abs(dx);
            int dir = dx >= 0f ? 1 : -1;
            SetFacing(dir);

            if (!CanSeeTarget(true))
            {
                _lostSightTimer += dt;
                if (_lostSightTimer > 2f) { _lostSightTimer = 0f; SetState(EnemyState.Patrol); return 0f; }
            }
            else _lostSightTimer = 0f;

            bool sameLevel = Mathf.Abs(tp.y - (pos.y + definition.size.y * 0.5f)) < 1.6f;

            if (definition.archetype == EnemyArchetype.MeleeGrunt)
            {
                if (dist <= definition.attackRange && sameLevel && _cooldown <= 0f) { BeginTelegraph(); return 0f; }
                if (dist <= definition.attackRange * 0.7f) return 0f;
                return BlockedAhead(dir) ? 0f : dir * definition.chaseSpeed;
            }

            // ranged: hold a distance band
            if (dist < definition.preferredMinDistance)
            {
                return BlockedAhead(-dir) ? 0f : -dir * definition.chaseSpeed * 0.9f;
            }
            if (dist > definition.preferredMaxDistance)
            {
                return BlockedAhead(dir) ? 0f : dir * definition.chaseSpeed;
            }
            if (_cooldown <= 0f && sameLevel && HasLineOfSight(tp)) BeginTelegraph();
            return 0f;
        }

        // ---- telegraph / attack / recover -----------------------------------------------

        void BeginTelegraph()
        {
            SetState(EnemyState.Telegraph);
            _attackFired = false;
            if (_telegraph != null)
            {
                bool ranged = definition.archetype == EnemyArchetype.RangedShooter;
                Vector2 centre = ranged ? MuzzlePosition() + new Vector2(Facing * definition.telegraphSize.x * 0.5f, 0f) : MeleeCentre();
                Vector2 size = ranged ? definition.telegraphSize : definition.hitSize;
                _telegraph.Begin(centre, size, Facing, definition.telegraphTime);
            }
        }

        void TickTelegraph()
        {
            float t = Mathf.Clamp01(_stateTimer / definition.telegraphTime);
            if (_react != null) _react.SetWarning(t);
            if (_stateTimer >= definition.telegraphTime) SetState(EnemyState.Attack);
        }

        void TickAttack()
        {
            if (definition.archetype == EnemyArchetype.MeleeGrunt)
            {
                Vector2 centre = MeleeCentre();
                var info = new DamageInfo
                {
                    Amount = definition.damage,
                    SourceTeam = Team.Enemy,
                    Attacker = this,
                    SourcePosition = transform.position,
                    Knockback = new Vector2(definition.knockback.x * Facing, definition.knockback.y),
                    Stagger = definition.stagger,
                    HitStop = 0.07f,
                    Shake = 0.3f,
                    Tags = DamageTags.Melee
                };
                MeleeStrike.Strike(centre, definition.hitSize, LayerMask.GetMask(GameLayers.PlayerName), info, _hitSet);
            }
            else if (!_attackFired)
            {
                _attackFired = true;
                Projectile.Spawn(MuzzlePosition(), new Vector2(Facing * definition.projectileSpeed, 0f), definition.damage, Team.Enemy,
                    definition.knockback.x, definition.stagger);
            }

            if (_stateTimer >= definition.activeTime) SetState(EnemyState.Recover);
        }

        void TickRecover()
        {
            if (_stateTimer >= definition.recoverTime)
            {
                _cooldown = definition.attackCooldown;
                SetState(EnemyState.Chase);
            }
        }

        void TickStagger()
        {
            if (_stateTimer >= _staggerDuration) SetState(EnemyState.Chase);
        }

        float _staggerDuration;

        // =====================================================================
        // Movement
        // =====================================================================

        void Move(float desiredVx, float dt)
        {
            float vx = desiredVx + _knock.x;
            _knock.x = Mathf.MoveTowards(_knock.x, 0f, 28f * dt);

            float vyOld = _vel.y;
            _vel.y = Mathf.Max(vyOld - Gravity * dt, -MaxFall);

            _motor.Move(new Vector2(vx * dt, (vyOld + _vel.y) * 0.5f * dt));
            var c = _motor.Collisions;
            if (c.below && _vel.y < 0f) _vel.y = 0f;
            if (c.above && _vel.y > 0f) _vel.y = 0f;
        }

        bool BlockedAhead(int dir)
        {
            var c = _motor.Collisions;
            if ((dir > 0 && c.right) || (dir < 0 && c.left)) return true;

            // ledge check: is there floor just ahead of the front foot?
            Vector2 pos = transform.position;
            Vector2 origin = pos + new Vector2(dir * (definition.size.x * 0.5f + 0.25f), 0.3f);
            return Physics2D.Raycast(origin, Vector2.down, 1.1f, GameLayers.WalkableMask).collider == null;
        }

        // =====================================================================
        // Perception
        // =====================================================================

        bool CanSeeTarget(bool alreadyChasing)
        {
            Health target = Health.Player;
            if (target == null || target.IsDead) return false;

            Vector2 tp = target.Center;
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
            if (State == EnemyState.Telegraph && next != EnemyState.Attack)
            {
                if (_telegraph != null) _telegraph.Hide();
                if (_react != null) _react.SetWarning(0f);
            }
            if (State == EnemyState.Telegraph && next == EnemyState.Attack)
            {
                if (_telegraph != null) _telegraph.Hide();
                if (_react != null) _react.SetWarning(0f);
            }

            State = next;
            _stateTimer = 0f;
            if (next == EnemyState.Attack) _hitSet.Clear();
        }

        void SetFacing(int dir)
        {
            if (dir == 0) return;
            Facing = dir;
            if (_visualRoot != null) _visualRoot.localScale = new Vector3(dir, 1f, 1f);
        }

        Vector2 MeleeCentre()
            => (Vector2)transform.position + new Vector2(definition.hitOffset.x * Facing, definition.hitOffset.y);

        Vector2 MuzzlePosition()
            => (Vector2)transform.position + new Vector2(definition.muzzleOffset.x * Facing, definition.muzzleOffset.y);

        void EnterStagger(float duration)
        {
            if (State == EnemyState.Dead) return;
            _staggerDuration = duration;
            SetState(EnemyState.Stagger);
        }

        // =====================================================================
        // Combat interfaces
        // =====================================================================

        void IAttacker.OnAttackParried(Vector2 point, float stunDuration)
        {
            EnterStagger(stunDuration);
            _poise = 0f;
        }

        /// <summary>Stunned enemies take bonus damage: the reward for parrying.</summary>
        public HitResult Filter(ref DamageInfo info)
        {
            if (State == EnemyState.Stagger && _staggerDuration > definition.staggerTime + 0.05f)
                info.Amount *= definition.stunnedDamageMultiplier;
            return HitResult.Hit;
        }

        void OnHitTaken(HitReport r)
        {
            if (r.Result != HitResult.Hit || r.Amount <= 0f || State == EnemyState.Dead) return;

            Vector2 kb = r.Info.Knockback * (1f - definition.knockbackResistance);
            _knock.x = kb.x;
            if (kb.y > 0.1f) _vel.y = kb.y;

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

