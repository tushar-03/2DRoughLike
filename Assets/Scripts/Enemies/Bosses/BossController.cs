using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Utils;

namespace Roughlike.Enemies
{
    /// <summary>
    /// A boss fight. Owns health, phases, stun (a parried melee attack stuns it and it takes extra damage), telegraph pooling,
    /// the attack loop and death. What it actually does comes from a <see cref="BossBrain"/>. Attacks are coroutines that always
    /// show a dotted telegraph first, in the same visual language as ordinary enemies.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class BossController : MonoBehaviour, IAttacker, IHitFilter
    {
        const float Gravity = 45f;

        public BossDefinition Definition { get; private set; }
        public EnemyScaling Scaling { get; private set; } = EnemyScaling.One;
        public Health Health { get; private set; }
        public BossBrain Brain { get; private set; }
        public Rect Arena { get; set; } = new Rect(0f, 3f, 30f, 14f);
        public int Facing { get; private set; } = 1;
        public int Phase { get; private set; }
        public bool Engaged { get; private set; }
        public bool Dead { get; private set; }
        public bool Stunned => _stun > 0f;
        /// <summary>True while she has dropped to the ground and is open to attack.</summary>
        public bool Exposed => _exposed > 0f;
        public Vector2 Position => transform.position;
        public Transform Visual { get; private set; }
        public float FloorY => Arena.yMin;

        [Tooltip("Run from Update. Tests turn this off and drive Tick(dt) themselves.")]
        float _exposed;
        public bool autoTick = true;

        public event Action<int> PhaseChanged;

        KinematicMotor2D _motor;
        SpriteRenderer _sr;
        SpriteAnimator _anim;
        HitReactionVisual _react;
        Coroutine _loop, _attack;
        bool _attackDone, _busy, _parryable, _phaseCue, _invulnerable;
        float _stun, _vyGround;
        Vector2 _vel;
        readonly List<TelegraphIndicator> _telegraphs = new List<TelegraphIndicator>();
        readonly HashSet<Health> _hitSet = new HashSet<Health>();

        Team IAttacker.Team => Team.Enemy;
        Transform IAttacker.Transform => transform;
        public int Priority => 0;

        // ------------------------------------------------------------------ setup

        public void Initialize(BossDefinition def, EnemyScaling scaling, BossBrain brain, SpriteRenderer sr, SpriteAnimator anim, HitReactionVisual react, Transform visual)
        {
            Definition = def;
            Scaling = scaling;
            Brain = brain;
            _sr = sr; _anim = anim; _react = react; Visual = visual;
            Health = GetComponent<Health>();
            _motor = GetComponent<KinematicMotor2D>();
            brain.Bind(this);
        }

        void OnEnable()
        {
            if (Health == null) Health = GetComponent<Health>();
            Health.Died += OnDied;
            Health.HitTaken += OnHit;
            Health.AddFilter(this);
        }

        void OnDisable()
        {
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.HitTaken -= OnHit;
            Health.RemoveFilter(this);
        }

        /// <summary>Starts the fight.</summary>
        public void Engage()
        {
            if (Engaged || Dead) return;
            Engaged = true;
            EventBus<BossSpawnedEvent>.Raise(new BossSpawnedEvent { Boss = this });
            if (_loop == null) _loop = StartCoroutine(BrainLoop());
        }

        // ------------------------------------------------------------------ numbers the brains use

        public float Dmg(float multiplier) => Definition.damage * Scaling.Damage * multiplier;
        /// <summary>Phase 0 is 1, later phases shave time off warnings and rests so the fight speeds up.</summary>
        public float PhaseSpeed => Mathf.Pow(0.88f, Phase);
        public float Tel(float seconds) => seconds * Scaling.Telegraph * PhaseSpeed;
        public float Rest(float seconds) => seconds * Scaling.Cooldown * PhaseSpeed;
        public float Spd(float speed) => speed * Scaling.Speed;
        public bool HasTarget => Health.Player != null && !Health.Player.IsDead;
        public Vector2 TargetCentre => HasTarget ? Health.Player.Center : Position + new Vector2(Facing * 4f, 1f);
        public Vector2 TargetFeet => HasTarget ? (Vector2)Health.Player.transform.position : Position + new Vector2(Facing * 4f, 0f);

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (Dead || dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 20f);
            if (_stun > 0f) _stun -= dt;
            if (_exposed > 0f) _exposed -= dt;

            Vector2 v = _vel;
            if (!_busy && !Stunned && Engaged) v = Brain.IdleVelocity(dt);
            else if (Stunned) v = Vector2.zero;
            Step(v, dt);

            if (_sr != null && !_busy && !Stunned && _anim != null && _anim.Art != null)
                _anim.Loop(Mathf.Abs(v.x) > 0.5f && _anim.Art.Has("walk") ? "walk" : "idle");
        }

        void Step(Vector2 v, float dt)
        {
            if (Brain.Flying)
            {
                Vector2 p = Position + v * dt;
                p.x = Mathf.Clamp(p.x, Arena.xMin + 1.5f, Arena.xMax - 1.5f);
                p.y = Mathf.Clamp(p.y, Arena.yMin, Arena.yMax);
                transform.position = p;
                return;
            }
            if (_motor == null) return;
            _vyGround = Mathf.Max(_vyGround - Gravity * dt, -25f);
            _motor.Move(new Vector2(v.x * dt, _vyGround * dt));
            var c = _motor.Collisions;
            if (c.below && _vyGround < 0f) _vyGround = 0f;
            Vector2 q = Position;
            q.x = Mathf.Clamp(q.x, Arena.xMin + 1.5f, Arena.xMax - 1.5f);
            transform.position = q;
        }

        IEnumerator BrainLoop()
        {
            while (!Dead)
            {
                if (_phaseCue)
                {
                    _phaseCue = false;
                    _busy = true;
                    _invulnerable = true;
                    yield return StartCoroutine(Brain.PhaseShout(Phase));
                    _invulnerable = false;
                    _busy = false;
                }

                yield return WaitSeconds(Brain.Rest(Phase));
                if (Dead) yield break;
                while (Stunned) yield return null;
                if (!HasTarget) { yield return null; continue; }

                FaceTarget();
                _busy = true;
                _attackDone = false;
                _attack = StartCoroutine(Wrapped(Brain.NextAttack(Phase)));
                while (!_attackDone && !Dead && !Stunned) yield return null;

                if (Stunned)
                {
                    if (_attack != null) StopCoroutine(_attack);
                    CancelAttackEffects();
                    if (_anim != null && _anim.Art != null && _anim.Art.Has("hurt")) _anim.PlayOnce("hurt", Mathf.Max(0.3f, _stun));
                    while (Stunned) yield return null;
                }
                _attack = null;
                _busy = false;
            }
        }

        IEnumerator Wrapped(IEnumerator attack)
        {
            yield return StartCoroutine(attack);
            _attackDone = true;
        }

        void CancelAttackEffects()
        {
            HideTelegraphs();
            _parryable = false;
            _vel = Vector2.zero;
            if (_sr != null) { var c = _sr.color; c.a = 1f; _sr.color = c; }
        }

        // ------------------------------------------------------------------ helpers for attacks

        public void FaceTarget()
        {
            if (!HasTarget) return;
            SetFacing(TargetCentre.x >= Position.x ? 1 : -1);
        }

        public void SetFacing(int dir)
        {
            Facing = dir == 0 ? Facing : dir;
            if (Visual != null) Visual.localScale = new Vector3(Facing, 1f, 1f);
        }

        /// <summary>Waits in game time (so pausing and hit-stop affect it).</summary>
        public IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds && !Dead) { t += Time.deltaTime; yield return null; }
        }

        public void SetVelocity(Vector2 v) => _vel = v;

        /// <summary>Puts the boss somewhere (blinks, leaps) and drops any fall speed.</summary>
        public void TeleportTo(Vector2 p)
        {
            transform.position = p;
            _vyGround = 0f;
        }

        /// <summary>While true, a successful parry of this boss's melee stuns it.</summary>
        public void SetParryable(bool on) => _parryable = on;

        /// <summary>Takes extra damage for a while (a flying boss that has landed), without being stunned.</summary>
        public void Expose(float seconds) => _exposed = seconds;

        public void Loop(string clip) { if (_anim != null && _anim.Art != null && _anim.Art.Has(clip)) _anim.Loop(clip); }
        public void PlayOnce(string clip, float duration) { if (_anim != null && _anim.Art != null && _anim.Art.Has(clip)) _anim.PlayOnce(clip, duration); }
        public void Scrub(string clip, float progress) { if (_anim != null && _anim.Art != null && _anim.Art.Has(clip)) _anim.Scrub(clip, progress); }
        public void SetAlpha(float a) { if (_sr != null) { var c = _sr.color; c.a = a; _sr.color = c; } }
        public void SetWarning(float intensity) { if (_react != null) _react.SetWarning(intensity); }

        // telegraphs ---------------------------------------------------------------

        TelegraphIndicator GetIndicator()
        {
            foreach (var t in _telegraphs) if (t != null && !t.gameObject.activeSelf) return t;
            var go = new GameObject("Telegraph");
            go.transform.SetParent(transform, false);
            var ind = go.AddComponent<TelegraphIndicator>();
            _telegraphs.Add(ind);
            return ind;
        }

        /// <summary>A dotted box (or line, when thin) that lights up over <paramref name="seconds"/>.</summary>
        public TelegraphIndicator ShowBox(Vector2 centre, Vector2 size, float seconds, float angle = 0f, bool unparryable = false)
        {
            var ind = GetIndicator();
            ind.transform.SetParent(null, true);   // world space: it must not follow or flip with the boss
            // a rotated line already points the way it is aimed; mirroring it by the boss's facing would put the arrowhead on the boss
            ind.Begin(centre, size, Mathf.Abs(angle) > 0.01f ? 1 : Facing, seconds, unparryable, angle, false);
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.EnemyTelegraph, centre, 0.6f);
            return ind;
        }

        /// <summary>A dotted ring that fills outward over <paramref name="seconds"/>.</summary>
        public TelegraphIndicator ShowCircle(Vector2 centre, float radius, float seconds, bool unparryable = true)
        {
            var ind = GetIndicator();
            ind.transform.SetParent(null, true);
            ind.Begin(centre, new Vector2(radius * 2f, radius * 2f), Facing, seconds, unparryable, 0f, true);
            return ind;
        }

        public void HideTelegraphs()
        {
            foreach (var t in _telegraphs) if (t != null) t.Hide();
        }

        // strikes ------------------------------------------------------------------

        DamageInfo Template(float mult, DamageTags tags, Vector2 knockback, float stagger = 22f)
        {
            return new DamageInfo
            {
                Amount = Dmg(mult),
                SourceTeam = Team.Enemy,
                Attacker = this,
                SourcePosition = Position,
                Knockback = knockback,
                Stagger = stagger,
                HitStop = 0.09f,
                Shake = 0.55f,
                Tags = tags
            };
        }

        /// <summary>A melee box hit (parryable). Knockback pushes away along <see cref="Facing"/>.</summary>
        public int StrikeBox(Vector2 centre, Vector2 size, float mult, Vector2 knockback)
        {
            _hitSet.Clear();
            Roughlike.Utils.Sfx.PlayAt(Roughlike.Utils.SfxId.BossSlam, centre, 0.7f);
            var info = Template(mult, DamageTags.Melee, new Vector2(knockback.x * Facing, knockback.y));
            return MeleeStrike.Strike(centre, size, LayerMask.GetMask(GameLayers.PlayerName), info, _hitSet);
        }

        /// <summary>A circular blast (cannot be parried): the player is pushed away from the centre.</summary>
        public int StrikeCircle(Vector2 centre, float radius, float mult, Vector2 knockback, Color flash)
        {
            _hitSet.Clear();
            var info = Template(mult, DamageTags.Hazard | DamageTags.Unparryable, knockback);
            EventBus<ExplosionEvent>.Raise(new ExplosionEvent { Position = centre, Radius = radius * 0.8f, Colour = flash });
            return AreaStrike.Circle(centre, radius, LayerMask.GetMask(GameLayers.PlayerName), info, _hitSet);
        }

        public Projectile Shoot(Vector2 from, Vector2 velocity, float mult, Color tint, float radius = 0.24f, float life = 4.5f)
            => Projectile.Spawn(from, velocity, Dmg(mult), Team.Enemy, 5f, 16f, life, radius, tint);

        // ------------------------------------------------------------------ combat interfaces

        void IAttacker.OnAttackParried(Vector2 point, float stunDuration)
        {
            if (!_parryable || Dead) return;
            _stun = Mathf.Max(_stun, Mathf.Min(stunDuration, 1.8f));
            _parryable = false;
        }

        /// <summary>Stunned: takes extra damage. Mid phase-change: untouchable.</summary>
        public HitResult Filter(ref DamageInfo info)
        {
            if (_invulnerable) return HitResult.Ignored;
            if (Stunned || Exposed) info.Amount *= 1.5f;
            return HitResult.Hit;
        }

        void OnHit(HitReport r)
        {
            if (Dead || r.Result != HitResult.Hit || Definition == null) return;
            // phases
            float f = Health.Fraction;
            var thresholds = Definition.phaseThresholds;
            int target = 0;
            for (int i = 0; i < thresholds.Length; i++) if (f <= thresholds[i]) target = i + 1;
            if (target > Phase)
            {
                Phase = target;
                _phaseCue = true;
                PhaseChanged?.Invoke(Phase);
            }
        }

        void OnDestroy()
        {
            foreach (var t in _telegraphs) if (t != null) Destroy(t.gameObject);
        }

        void OnDied()
        {
            if (Dead) return;
            Dead = true;
            Engaged = false;
            if (_loop != null) StopCoroutine(_loop);
            if (_attack != null) StopCoroutine(_attack);
            CancelAttackEffects();
            Brain.OnDied();
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            EventBus<BossDefeatedEvent>.Raise(new BossDefeatedEvent { Position = Position, Definition = Definition });
            StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            if (_react != null) _react.enabled = false;
            PlayOnce("hurt", 0.4f);
            float t = 0f;
            Vector2 start = Position;
            while (t < 2.2f)
            {
                t += Time.deltaTime;
                if (Time.frameCount % 7 == 0)
                    EventBus<ExplosionEvent>.Raise(new ExplosionEvent
                    {
                        Position = start + new Vector2(UnityEngine.Random.Range(-1.4f, 1.4f), UnityEngine.Random.Range(0.4f, 3.2f)),
                        Radius = UnityEngine.Random.Range(0.8f, 1.6f),
                        Colour = Color.Lerp(new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.4f, 1f), UnityEngine.Random.value)
                    });
                SetAlpha(Mathf.Clamp01(1.4f - t / 1.6f));
                transform.position = start + new Vector2(Mathf.Sin(t * 40f) * 0.08f, 0f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
