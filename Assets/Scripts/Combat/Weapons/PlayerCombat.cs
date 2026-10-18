using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Combat
{
    public enum AttackPhase { None, Windup, Charging, Active, Recovery }
    public enum ShieldPhase { None, Parry, Block, Lowering }

    /// <summary>Raised when a swing goes live so VFX/audio can react without knowing about weapons.</summary>
    public struct SwingEvent
    {
        public Vector2 Position;
        public int Facing;
        public Color Tint;
        public Vector2 Size;
        public int ComboIndex;
    }

    /// <summary>Raised when the player parries (perfect block).</summary>
    public struct ParryEvent
    {
        public Vector2 Position;
    }

    /// <summary>
    /// Player weapon logic: two weapon slots (each with its own button, which is the instant swap), data-driven
    /// combos with input buffering, timed parry and block with a shield. Runs after the controller each frame
    /// (execution order 10) and reads <see cref="PlayerController.LastIntent"/>.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(PlayerController), typeof(Health))]
    public class PlayerCombat : MonoBehaviour, IAttacker, IHitFilter
    {
        [SerializeField] WeaponData slot1;
        [SerializeField] WeaponData slot2;
        [SerializeField] float attackBufferTime = 0.35f;
        [SerializeField] bool showHitboxes;
        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;
        /// <summary>Global damage scaling applied on top of the weapon's own modifiers.</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Supplies per-weapon damage, crit, status procs and speed from the item system. Neutral when null.</summary>
        public IPlayerModifiers Modifiers { get; set; }
        /// <summary>Multiplies the damage of the next swing, then resets (parry riposte mutation).</summary>
        public float PendingHitMultiplier { get; set; } = 1f;
        float _swingBonus = 1f;
        bool _shieldLatched;
        /// <summary>Accessibility option: the shield button toggles instead of holding.</summary>
        public bool ToggleBlock => SettingsService.Current.toggleBlock;

        PlayerController _pc;
        Health _health;
        PlayerVisual _visual;
        LayerMask _targetMask;

        // attack state
        AttackPhase _phase;
        float _phaseTimer;
        int _slot;
        int _combo;          // index of the current step, or of the next step while idle
        int _lastSlot = -1;
        int _buffered = -1;
        float _bufferTimer;
        float _comboTimer;
        readonly HashSet<Health> _hitSet = new HashSet<Health>();

        // shield state
        ShieldPhase _shieldPhase;
        float _shieldTimer;
        WeaponData _shieldWeapon;
        PlayerDefense _defense;

        public AttackPhase Phase => _phase;
        public ShieldPhase Shield => _shieldPhase;
        public int ComboIndex => _combo;

        /// <summary>Hold-to-charge can be switched off (the automated tests press buttons without ever releasing them).</summary>
        public static bool ChargeAttacks = true;
        /// <summary>Holding is worth nothing under this long; at <see cref="MaxCharge"/> the bonus is full.</summary>
        public const float MinCharge = 0.25f, MaxCharge = 1.2f;
        /// <summary>Extra damage at a full charge (1 = double).</summary>
        public const float ChargeDamageBonus = 1.0f;

        PlayerIntent _intent;
        float _chargeTimer, _chargeLevel;

        public bool Charging => _phase == AttackPhase.Charging;
        /// <summary>0..1: how charged the current (or just released) blow is.</summary>
        public float ChargeLevel => _phase == AttackPhase.Charging ? ChargeFor(_chargeTimer) : _chargeLevel;
        public int AttackSlot => _slot;

        static float ChargeFor(float seconds) => Mathf.Clamp01((seconds - MinCharge) / (MaxCharge - MinCharge));

        public event Action ChargeStarted;
        public event Action<float> ChargeReleased;
        public WeaponData Slot1 => slot1;
        public WeaponData Slot2 => slot2;
        public WeaponData GetWeapon(int slot) => slot == 0 ? slot1 : slot2;

        /// <summary>The melee weapon to show in the hand: the one used last, else whichever slot holds one.</summary>
        public WeaponData HeldMelee
        {
            get
            {
                WeaponData first = _lastSlot == 1 ? slot2 : slot1, second = _lastSlot == 1 ? slot1 : slot2;
                if (first != null && first.kind != WeaponKind.Shield) return first;
                if (second != null && second.kind != WeaponKind.Shield) return second;
                return null;
            }
        }

        /// <summary>The shield the player carries, if either slot holds one.</summary>
        public WeaponData HeldShield
        {
            get
            {
                if (slot1 != null && slot1.kind == WeaponKind.Shield) return slot1;
                if (slot2 != null && slot2.kind == WeaponKind.Shield) return slot2;
                return null;
            }
        }

        public event Action<WeaponData, AttackStep, int> AttackStarted;
        public event Action<Health, HitResult> StrikeLanded;
        public event Action Parried;
        public event Action ShieldRaised;

        // IAttacker
        Team IAttacker.Team => Team.Player;
        Transform IAttacker.Transform => transform;
        void IAttacker.OnAttackParried(Vector2 point, float stunDuration) { }

        // IHitFilter
        public int Priority => 10;

        void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            _defense = GetComponent<PlayerDefense>();
            _visual = GetComponentInChildren<PlayerVisual>();
            _targetMask = LayerMask.GetMask(GameLayers.EnemyName);
        }

        void OnEnable()
        {
            _health.AddFilter(this);
            _pc.RollStarted += Interrupt;
        }

        void OnDisable()
        {
            _health.RemoveFilter(this);
            _pc.RollStarted -= Interrupt;
            ReleaseMovementOverrides();
        }

        /// <summary>Assigns both slots without interrupting (used while building the player).</summary>
        public void SetWeaponsUnsafe(WeaponData a, WeaponData b)
        {
            slot1 = a;
            slot2 = b;
        }

        public void SetWeapon(int slot, WeaponData weapon)
        {
            Interrupt();
            if (slot == 0) slot1 = weapon; else slot2 = weapon;
            _combo = 0;
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
            if (_pc == null) return;
            dt = Mathf.Min(dt, 1f / 20f);
            PlayerIntent intent = _pc.LastIntent;
            _intent = intent;

            bool canAct = !_pc.IsControlLocked &&
                          (_pc.State == PlayerState.Grounded || _pc.State == PlayerState.Airborne);
            if (!canAct)
            {
                if (_phase != AttackPhase.None || _shieldPhase != ShieldPhase.None) Interrupt();
                _buffered = -1;
                return;
            }

            if (intent.Attack1Pressed) Buffer(0);
            if (intent.Attack2Pressed) Buffer(1);
            _bufferTimer -= dt;
            if (_bufferTimer <= 0f) _buffered = -1;

            TickShield(intent, dt);
            TickAttack(dt * (Modifiers != null ? Modifiers.AttackSpeed : 1f));
            ApplyMovementOverrides();
        }

        void Buffer(int slot)
        {
            _buffered = slot;
            _bufferTimer = attackBufferTime;
        }

        // =====================================================================
        // Attacks
        // =====================================================================

        void TickAttack(float dt)
        {
            switch (_phase)
            {
                case AttackPhase.None:
                    if (_buffered >= 0 && _shieldPhase == ShieldPhase.None) TryStartFromBuffer();
                    else if (_comboTimer > 0f)
                    {
                        _comboTimer -= dt;
                        if (_comboTimer <= 0f) _combo = 0;
                    }
                    break;

                case AttackPhase.Windup:
                {
                    _phaseTimer += dt;
                    AttackStep step = CurrentStep();
                    if (_phaseTimer >= step.windup) BeginChargeOrStrike(step);
                    break;
                }

                case AttackPhase.Charging:
                {
                    // the blow is drawn back and held; letting go (or holding too long) throws it
                    AttackStep step = CurrentStep();
                    _chargeTimer += dt;
                    bool held = _slot == 0 ? _intent.Attack1Held : _intent.Attack2Held;
                    if (!held || _chargeTimer >= MaxCharge)
                    {
                        _chargeLevel = ChargeFor(_chargeTimer);
                        ChargeReleased?.Invoke(_chargeLevel);
                        EnterActive(step);
                    }
                    break;
                }

                case AttackPhase.Active:
                {
                    _phaseTimer += dt;
                    AttackStep step = CurrentStep();
                    Strike(step);
                    if (_phaseTimer >= step.active)
                    {
                        _phase = AttackPhase.Recovery;
                        _phaseTimer = 0f;
                        _combo++; // next step index
                        _lastSlot = _slot;
                    }
                    break;
                }

                case AttackPhase.Recovery:
                {
                    _phaseTimer += dt;
                    AttackStep step = PreviousStep();
                    if (_buffered >= 0 && _phaseTimer >= step.recovery * step.chainFraction * 0.7f)
                    {
                        ChainFromBuffer();
                    }
                    else if (_phaseTimer >= step.recovery)
                    {
                        EndAttack(GetWeapon(_slot).comboResetTime);
                    }
                    break;
                }
            }
        }

        AttackStep CurrentStep() => GetWeapon(_slot).steps[Mathf.Clamp(_combo, 0, GetWeapon(_slot).steps.Length - 1)];
        AttackStep PreviousStep() => GetWeapon(_slot).steps[Mathf.Clamp(_combo - 1, 0, GetWeapon(_slot).steps.Length - 1)];

        void TryStartFromBuffer()
        {
            WeaponData w = GetWeapon(_buffered);
            if (w == null || w.kind == WeaponKind.Shield || w.steps.Length == 0) { _buffered = -1; return; }

            int index = (_buffered == _lastSlot && _comboTimer > 0f && _combo < w.steps.Length) ? _combo : 0;
            StartAttack(_buffered, index);
        }

        void ChainFromBuffer()
        {
            WeaponData w = GetWeapon(_buffered);
            if (w == null) { _buffered = -1; return; }

            if (w.kind == WeaponKind.Shield)
            {
                EndAttack(0f);   // the shield raises on the next tick via the held button
                _buffered = -1;
                return;
            }

            int index = (_buffered == _slot && _combo < w.steps.Length) ? _combo : 0;
            StartAttack(_buffered, index);
        }

        void BeginChargeOrStrike(AttackStep step)
        {
            bool held = _slot == 0 ? _intent.Attack1Held : _intent.Attack2Held;
            if (ChargeAttacks && held)
            {
                _phase = AttackPhase.Charging;
                _phaseTimer = 0f;
                _chargeTimer = 0f;
                ChargeStarted?.Invoke();
                return;
            }
            _chargeLevel = 0f;
            EnterActive(step);
        }

        /// <summary>The nearest living enemy within <paramref name="range"/>; enemies behind count as farther, so the one you face wins ties.</summary>
        public Health NearestEnemy(float range)
        {
            Vector2 from = (Vector2)transform.position + Vector2.up;
            var hits = Physics2D.OverlapCircleAll(from, range, _targetMask);
            Health best = null;
            float bestScore = float.MaxValue;
            foreach (var c in hits)
            {
                var h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead || h.Team != Team.Enemy) continue;
                Vector2 to = h.Center - from;
                float score = to.magnitude * (Mathf.Sign(to.x) == _pc.Facing || Mathf.Abs(to.x) < 0.5f ? 1f : 1.6f);
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        /// <summary>Turns toward the nearest enemy when an attack starts, so a swing never whiffs at thin air behind you.</summary>
        void AutoFace(float range)
        {
            var target = NearestEnemy(range);
            if (target == null) return;
            float dx = target.Center.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.4f) _pc.FaceToward(dx > 0f ? 1 : -1);
        }

        void StartAttack(int slot, int index)
        {
            _chargeLevel = 0f;
            WeaponData starting = GetWeapon(slot);
            if (starting != null) AutoFace(starting.kind == WeaponKind.Bow ? starting.range : 3.6f);
            _slot = slot;
            _combo = index;
            _buffered = -1;
            _phase = AttackPhase.Windup;
            _phaseTimer = 0f;
            _hitSet.Clear();
            _comboTimer = 0f;

            AttackStep step = CurrentStep();
            AttackStarted?.Invoke(GetWeapon(slot), step, index);
            if (_visual != null) _visual.Punch(new Vector2(1.12f, 0.9f));
        }

        void EnterActive(AttackStep step)
        {
            _phase = AttackPhase.Active;
            _phaseTimer = 0f;
            _hitSet.Clear();
            _swingBonus = PendingHitMultiplier;
            PendingHitMultiplier = 1f;

            _pc.AddImpulse(new Vector2(_pc.Facing * step.lunge, 0f));
            if (_visual != null) _visual.Punch(new Vector2(0.88f, 1.12f));

            if (GetWeapon(_slot).kind == WeaponKind.Bow) FireArrow(step);

            EventBus<SwingEvent>.Raise(new SwingEvent
            {
                Position = HitCentre(step),
                Facing = _pc.Facing,
                Tint = GetWeapon(_slot).tint,
                Size = step.hitSize,
                ComboIndex = _combo
            });
        }

        Vector2 HitCentre(AttackStep step)
            => (Vector2)transform.position + new Vector2(step.hitOffset.x * _pc.Facing, step.hitOffset.y);

        /// <summary>A bow shoots an arrow when its draw completes instead of swinging a hitbox.</summary>
        void FireArrow(AttackStep step)
        {
            WeaponData w = GetWeapon(_slot);
            WeaponMods mods = Modifiers != null ? Modifiers.GetWeaponMods(_slot) : WeaponMods.Neutral;
            float amount = step.damage * DamageMultiplier * mods.DamageMultiplier * _swingBonus * (1f + ChargeDamageBonus * _chargeLevel);
            bool crit = mods.CritChance > 0f && UnityEngine.Random.value < mods.CritChance;
            if (crit) amount *= mods.CritMultiplier;

            Vector2 from = (Vector2)transform.position + new Vector2(0.55f * _pc.Facing, 1.15f);
            // auto aim: the arrow flies at the nearest enemy (up, down or ahead); with no target it goes straight forward
            Vector2 dir = new Vector2(_pc.Facing, 0f);
            var target = NearestEnemy(w.range);
            if (target != null)
            {
                Vector2 to = target.Center - from;
                if (to.sqrMagnitude > 0.25f) dir = to.normalized;
                if (Mathf.Abs(to.x) > 0.4f) _pc.FaceToward(to.x > 0f ? 1 : -1);
            }
            float speed = w.projectileSpeed * (1f + 0.4f * _chargeLevel);
            float flight = (w.range + 4f) / Mathf.Max(1f, speed);
            Projectile arrow = Projectile.Spawn(from, dir * speed, amount, Team.Player,
                step.knockback.x * (1f + 0.8f * _chargeLevel), step.stagger * (1f + 0.8f * _chargeLevel), flight, 0.22f + 0.08f * _chargeLevel, w.tint);
            arrow.SetArrow(w.pierce + (step.damage >= 22f ? 1 : 0) + (_chargeLevel > 0.6f ? 1 : 0));
            Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.BowRelease, 0.85f);
        }

        void Strike(AttackStep step)
        {
            if (GetWeapon(_slot).kind == WeaponKind.Bow) return;
            Vector2 centre = HitCentre(step);
            if (showHitboxes) MeleeStrike.DrawDebug(centre, step.hitSize, Color.red);

            WeaponMods mods = Modifiers != null ? Modifiers.GetWeaponMods(_slot) : WeaponMods.Neutral;
            float amount = step.damage * DamageMultiplier * mods.DamageMultiplier * _swingBonus * (1f + ChargeDamageBonus * _chargeLevel);
            bool crit = mods.CritChance > 0f && UnityEngine.Random.value < mods.CritChance;
            if (crit) amount *= mods.CritMultiplier;
            float power = 1f + 0.8f * _chargeLevel;

            var info = new DamageInfo
            {
                Amount = amount,
                SourceTeam = Team.Player,
                Attacker = this,
                SourcePosition = transform.position,
                Knockback = new Vector2(step.knockback.x * _pc.Facing, step.knockback.y) * power,
                Stagger = step.stagger * power,
                HitStop = step.hitStop * 0.7f * (crit ? 1.5f : 1f) * power,
                Shake = step.shake * (crit ? 1.4f : 1f) * power,
                Tags = DamageTags.Melee,
                IsCrit = crit,
                OnHitStatus = mods.Status,
                StatusChance = mods.StatusChance,
                StatusDuration = mods.StatusDuration,
                StatusPotency = mods.StatusPotency
            };

            MeleeStrike.Strike(centre, step.hitSize * (1f + 0.3f * _chargeLevel), _targetMask, info, _hitSet, (h, r) => StrikeLanded?.Invoke(h, r));
        }

        void EndAttack(float comboResetTime)
        {
            _phase = AttackPhase.None;
            _phaseTimer = 0f;
            _comboTimer = comboResetTime;
            if (comboResetTime <= 0f) _combo = 0;
            _hitSet.Clear();
        }

        // =====================================================================
        // Shield
        // =====================================================================

        void TickShield(in PlayerIntent intent, float dt)
        {
            WeaponData shieldWeapon = null;
            bool held = false;
            if (!ToggleBlock)
            {
                _shieldLatched = false;
                if (slot1 != null && slot1.kind == WeaponKind.Shield && intent.Attack1Held) { shieldWeapon = slot1; held = true; }
                else if (slot2 != null && slot2.kind == WeaponKind.Shield && intent.Attack2Held) { shieldWeapon = slot2; held = true; }
            }
            else
            {
                // accessibility: press once to raise the shield, press again to lower it
                bool s1 = slot1 != null && slot1.kind == WeaponKind.Shield;
                bool s2 = slot2 != null && slot2.kind == WeaponKind.Shield;
                if ((s1 && intent.Attack1Pressed) || (s2 && intent.Attack2Pressed)) _shieldLatched = !_shieldLatched;
                shieldWeapon = s1 ? slot1 : (s2 ? slot2 : null);
                held = _shieldLatched && shieldWeapon != null;
            }

            switch (_shieldPhase)
            {
                case ShieldPhase.None:
                    if (held && (_phase == AttackPhase.None))
                    {
                        _shieldWeapon = shieldWeapon;
                        _shieldPhase = ShieldPhase.Parry;
                        _shieldTimer = 0f;
                        _buffered = -1;
                        ShieldRaised?.Invoke();
                        if (_visual != null) _visual.Punch(new Vector2(1.08f, 0.94f));
                    }
                    break;

                case ShieldPhase.Parry:
                    _shieldTimer += dt;
                    if (!held) BeginLowering();
                    else if (_shieldTimer >= _shieldWeapon.shield.parryWindow * (Modifiers != null ? Modifiers.ParryWindowMultiplier : 1f))
                        _shieldPhase = ShieldPhase.Block;
                    break;

                case ShieldPhase.Block:
                    if (!held) BeginLowering();
                    break;

                case ShieldPhase.Lowering:
                    _shieldTimer += dt;
                    if (_shieldTimer >= _shieldWeapon.shield.lowerTime) _shieldPhase = ShieldPhase.None;
                    break;
            }
        }

        void BeginLowering()
        {
            _shieldPhase = ShieldPhase.Lowering;
            _shieldTimer = 0f;
        }

        public HitResult Filter(ref DamageInfo info)
        {
            if (_shieldPhase != ShieldPhase.Parry && _shieldPhase != ShieldPhase.Block) return HitResult.Hit;
            if ((info.Tags & DamageTags.Unblockable) != 0) return HitResult.Hit;

            float dx = info.SourcePosition.x - transform.position.x;
            bool inFront = Mathf.Abs(dx) < 0.15f || (int)Mathf.Sign(dx) == _pc.Facing;
            if (!inFront) return HitResult.Hit;

            ShieldSettings s = _shieldWeapon.shield;
            Vector2 point = info.HitPoint != Vector2.zero ? info.HitPoint : (Vector2)transform.position + Vector2.up;

            if (_shieldPhase == ShieldPhase.Parry && (info.Tags & DamageTags.Unparryable) == 0)
            {
                info.Attacker?.OnAttackParried(point, s.parryStun);
                if (_defense != null) _defense.GrantInvulnerability(s.parryInvulnerability);
                info.HitStop = s.parryHitStop;
                info.Shake = s.parryShake;
                info.HitPoint = point;
                Parried?.Invoke();
                EventBus<ParryEvent>.Raise(new ParryEvent { Position = point });
                if (_visual != null) _visual.Punch(new Vector2(1.18f, 0.9f));
                return HitResult.Parried;
            }

            info.Amount *= 1f - s.blockDamageReduction;
            info.Knockback = Vector2.zero;
            info.Stagger = 0f;
            info.HitStop = 0.04f;
            info.Shake = 0.15f;
            info.HitPoint = point;
            _pc.AddImpulse(new Vector2(-_pc.Facing * s.blockPushback, 0f));
            return HitResult.Blocked;
        }

        // =====================================================================
        // Interrupts and movement overrides
        // =====================================================================

        /// <summary>Hit reaction, roll or ladder: drop whatever we were doing.</summary>
        public void Interrupt()
        {
            _chargeLevel = 0f;
            _phase = AttackPhase.None;
            _phaseTimer = 0f;
            _combo = 0;
            _comboTimer = 0f;
            _buffered = -1;
            _hitSet.Clear();
            if (_shieldPhase != ShieldPhase.None) _shieldPhase = ShieldPhase.None;
            _shieldLatched = false;
            ReleaseMovementOverrides();
        }

        void ApplyMovementOverrides()
        {
            if (_phase != AttackPhase.None)
            {
                AttackStep step = _phase == AttackPhase.Recovery ? PreviousStep() : CurrentStep();
                // swinging should not root you to the spot: keep most of the run speed so movement and attacks flow together
                _pc.MoveSpeedScale = Mathf.Min(0.9f, step.moveScale * 1.5f + 0.1f);
                _pc.FacingLocked = _phase == AttackPhase.Windup || _phase == AttackPhase.Charging || _phase == AttackPhase.Active;
            }
            else if (_shieldPhase == ShieldPhase.Parry || _shieldPhase == ShieldPhase.Block)
            {
                _pc.MoveSpeedScale = _shieldWeapon.shield.moveScale;
                _pc.FacingLocked = false;
            }
            else
            {
                ReleaseMovementOverrides();
            }
        }

        void ReleaseMovementOverrides()
        {
            if (_pc == null) return;
            _pc.MoveSpeedScale = 1f;
            _pc.FacingLocked = false;
        }

        public string DebugSummary()
        {
            string a = slot1 != null ? slot1.displayName : "-";
            string b = slot2 != null ? slot2.displayName : "-";
            return $"[J] {a}   [K] {b}\nphase={_phase} combo={_combo} shield={_shieldPhase}";
        }
    }
}

