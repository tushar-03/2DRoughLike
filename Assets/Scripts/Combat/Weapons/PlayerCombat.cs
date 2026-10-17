using System;
using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Combat
{
    public enum AttackPhase { None, Windup, Active, Recovery }
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
        [SerializeField] float attackBufferTime = 0.25f;
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
        public WeaponData Slot1 => slot1;
        public WeaponData Slot2 => slot2;
        public WeaponData GetWeapon(int slot) => slot == 0 ? slot1 : slot2;

        /// <summary>The melee weapon to show in the hand: the one used last, else whichever slot holds one.</summary>
        public WeaponData HeldMelee
        {
            get
            {
                WeaponData first = _lastSlot == 1 ? slot2 : slot1, second = _lastSlot == 1 ? slot1 : slot2;
                if (first != null && first.kind == WeaponKind.Melee) return first;
                if (second != null && second.kind == WeaponKind.Melee) return second;
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
                    if (_phaseTimer >= step.windup) EnterActive(step);
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
                    if (_buffered >= 0 && _phaseTimer >= step.recovery * step.chainFraction)
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
            if (w == null || w.kind != WeaponKind.Melee || w.steps.Length == 0) { _buffered = -1; return; }

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

        void StartAttack(int slot, int index)
        {
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

        void Strike(AttackStep step)
        {
            Vector2 centre = HitCentre(step);
            if (showHitboxes) MeleeStrike.DrawDebug(centre, step.hitSize, Color.red);

            WeaponMods mods = Modifiers != null ? Modifiers.GetWeaponMods(_slot) : WeaponMods.Neutral;
            float amount = step.damage * DamageMultiplier * mods.DamageMultiplier * _swingBonus;
            bool crit = mods.CritChance > 0f && UnityEngine.Random.value < mods.CritChance;
            if (crit) amount *= mods.CritMultiplier;

            var info = new DamageInfo
            {
                Amount = amount,
                SourceTeam = Team.Player,
                Attacker = this,
                SourcePosition = transform.position,
                Knockback = new Vector2(step.knockback.x * _pc.Facing, step.knockback.y),
                Stagger = step.stagger,
                HitStop = step.hitStop * (crit ? 1.5f : 1f),
                Shake = step.shake * (crit ? 1.4f : 1f),
                Tags = DamageTags.Melee,
                IsCrit = crit,
                OnHitStatus = mods.Status,
                StatusChance = mods.StatusChance,
                StatusDuration = mods.StatusDuration,
                StatusPotency = mods.StatusPotency
            };

            MeleeStrike.Strike(centre, step.hitSize, _targetMask, info, _hitSet, (h, r) => StrikeLanded?.Invoke(h, r));
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
                _pc.MoveSpeedScale = step.moveScale;
                _pc.FacingLocked = _phase == AttackPhase.Windup || _phase == AttackPhase.Active;
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

