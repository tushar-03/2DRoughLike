using UnityEngine;
using Roughlike.Player;
using Roughlike.Utils;

namespace Roughlike.Combat
{
    /// <summary>
    /// Plays the player's combat clips: the swing for each attack step, a flinch when hurt, and the shield pose while blocking.
    /// Locomotion clips (idle, run, jump...) stay with <see cref="PlayerVisual"/>; this only adds the overrides on top.
    /// Lives on the same object as the sprite.
    /// </summary>
    public class PlayerCombatAnimator : MonoBehaviour
    {
        SpriteAnimator _anim;
        PlayerCombat _combat;
        Health _health;
        PlayerVisual _visual;
        PlayerController _controller;
        string _clip;
        AttackStep _step;
        bool _tinted;
        bool _chargeReadyPlayed;
        SpriteRenderer _sr;
        const float WindFraction = 0.36f;

        void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _visual = GetComponent<PlayerVisual>();
            _combat = GetComponentInParent<PlayerCombat>();
            _health = GetComponentInParent<Health>();
            _controller = GetComponentInParent<PlayerController>();
            _sr = GetComponent<SpriteRenderer>();
            if (GetComponent<HeldGear>() == null && GetComponentInParent<PlayerCombat>() != null) gameObject.AddComponent<HeldGear>();   // older scenes get it too
        }

        void OnEnable()
        {
            if (_combat != null) { _combat.AttackStarted += OnAttack; _combat.ChargeStarted += OnChargeStarted; _combat.ChargeReleased += OnChargeReleased; }
            if (_health != null) _health.HitTaken += OnHit;
            if (_controller != null)
            {
                _controller.RollStarted += OnRoll;
                _controller.Jumped += OnJump;
                _controller.DoubleJumped += OnJump;
                _controller.WallJumped += OnJump;
            }
            if (_visual != null) _visual.ClipOverride = BlockOverride;
        }

        void OnDisable()
        {
            if (_combat != null) { _combat.AttackStarted -= OnAttack; _combat.ChargeStarted -= OnChargeStarted; _combat.ChargeReleased -= OnChargeReleased; }
            if (_visual != null) _visual.HoldClip = false;
            if (_health != null) _health.HitTaken -= OnHit;
            if (_controller != null)
            {
                _controller.RollStarted -= OnRoll;
                _controller.Jumped -= OnJump;
                _controller.DoubleJumped -= OnJump;
                _controller.WallJumped -= OnJump;
            }
            if (_visual != null && _visual.ClipOverride == (System.Func<string>)BlockOverride) _visual.ClipOverride = null;
        }

        string BlockOverride()
        {
            if (_combat == null) return null;
            ShieldPhase s = _combat.Shield;
            if (s == ShieldPhase.Parry) return Pick("parry", "block_tower");
            if (s != ShieldPhase.Block) return null;
            WeaponData shield = _combat.HeldShield;
            return Pick(shield != null && !string.IsNullOrEmpty(shield.blockClip) ? shield.blockClip : "block_tower", "block_tower");
        }

        void OnChargeStarted() => Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.Charge, 0.22f, 1f, 0f);

        void OnChargeReleased(float level)
        {
            if (_visual != null) _visual.HoldClip = false;
            if (_anim == null || string.IsNullOrEmpty(_clip) || !_anim.Art.Has(_clip)) return;
            // the strike carries on from the coiled pose rather than starting the swing again
            float remaining = _step != null ? _step.active + _step.recovery * 0.6f : 0.3f;
            _anim.PlayOnce(_clip, Mathf.Max(0.2f, remaining / (1f - WindFraction)), WindFraction);
            if (level > 0.5f) Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.SwingHeavy, 0.9f);
        }

        void Update()
        {
            bool charging = _combat != null && _combat.Charging;
            // standing still the coiled pose is held; walking, the run cycle plays (the glow still shows the charge), so the legs never freeze
            bool moving = _controller != null && Mathf.Abs(_controller.Velocity.x) > 0.5f;
            if (_visual != null) _visual.HoldClip = charging && !moving;
            if (charging && !moving && _anim != null && !string.IsNullOrEmpty(_clip) && _anim.Art.Has(_clip))
                _anim.Scrub(_clip, WindFraction);
            else if (charging && moving && _anim != null && _anim.IsPlayingOnce) _anim.Loop(_anim.Art.Has("run") ? "run" : "idle");
            if (charging && _combat.ChargeLevel >= 1f && !_chargeReadyPlayed)
            {
                _chargeReadyPlayed = true;
                Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.ChargeReady, 0.5f);
            }
            if (!charging) _chargeReadyPlayed = false;
        }

        void LateUpdate()
        {
            // a charging fighter glows warmer and shudders as the blow builds
            if (_sr == null || _combat == null) return;
            if (_combat.Charging)
            {
                float level = _combat.ChargeLevel;
                float pulse = level >= 1f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
                _sr.color = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.4f), Mathf.Max(level * 0.8f, pulse));
                _tinted = true;
            }
            else if (_tinted)
            {
                _sr.color = Color.white;
                _tinted = false;
            }
        }

        string Pick(string wanted, string fallback) => _anim != null && _anim.Art != null && _anim.Art.Has(wanted) ? wanted : fallback;

        void OnRoll() => Haptics.Play(HapticKind.Roll);
        void OnJump() => Haptics.Play(HapticKind.Jump);

        void OnAttack(WeaponData weapon, AttackStep step, int index)
        {
            Haptics.Play(HapticKind.Attack);
            if (weapon == null || weapon.kind != WeaponKind.Bow) Roughlike.Utils.Sfx.Play(step.damage >= 22f ? Roughlike.Utils.SfxId.SwingHeavy : Roughlike.Utils.SfxId.SwingLight, 0.8f);
            else Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.SwingBow, 0.8f);
            if (_anim == null) return;
            string clip = string.IsNullOrEmpty(step.clip) ? (weapon != null && weapon.kind == WeaponKind.Bow ? "shoot" : "attack") : step.clip;
            if (!_anim.Art.Has(clip)) clip = weapon != null && weapon.kind == WeaponKind.Bow && _anim.Art.Has("shoot") ? "shoot" : "attack";
            _clip = clip;
            _step = step;
            if (!_anim.Art.Has(clip)) return;
            // the clip's impact sits about a third of the way in, so it is stretched to land exactly when the hitbox goes live
            float duration = Mathf.Max(0.2f, step.windup / 0.32f, step.windup + step.active + step.recovery * 0.6f);
            _anim.PlayOnce(clip, duration);
        }

        void OnHit(HitReport r)
        {
            if (_anim == null || r.Result != HitResult.Hit || r.Amount <= 0f || !_anim.Art.Has("hurt")) return;
            _anim.PlayOnce("hurt", 0.28f);
        }
    }
}
