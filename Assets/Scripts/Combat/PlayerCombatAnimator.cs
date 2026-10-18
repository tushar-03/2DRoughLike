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

        void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _visual = GetComponent<PlayerVisual>();
            _combat = GetComponentInParent<PlayerCombat>();
            _health = GetComponentInParent<Health>();
            _controller = GetComponentInParent<PlayerController>();
            if (GetComponent<HeldGear>() == null && GetComponentInParent<PlayerCombat>() != null) gameObject.AddComponent<HeldGear>();   // older scenes get it too
        }

        void OnEnable()
        {
            if (_combat != null) _combat.AttackStarted += OnAttack;
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
            if (_combat != null) _combat.AttackStarted -= OnAttack;
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
            return s == ShieldPhase.Parry || s == ShieldPhase.Block ? "block" : null;
        }

        void OnRoll() => Haptics.Play(HapticKind.Roll);
        void OnJump() => Haptics.Play(HapticKind.Jump);

        void OnAttack(WeaponData weapon, AttackStep step, int index)
        {
            Haptics.Play(HapticKind.Attack);
            if (weapon == null || weapon.kind != WeaponKind.Bow) Roughlike.Utils.Sfx.Play(step.damage >= 22f ? Roughlike.Utils.SfxId.SwingHeavy : Roughlike.Utils.SfxId.SwingLight, 0.8f);
            else Roughlike.Utils.Sfx.Play(Roughlike.Utils.SfxId.SwingBow, 0.8f);
            if (_anim == null) return;
            string clip = weapon != null && weapon.kind == WeaponKind.Bow && _anim.Art.Has("shoot") ? "shoot" : "attack";
            if (!_anim.Art.Has(clip)) return;
            _anim.PlayOnce(clip, Mathf.Max(0.12f, step.windup + step.active + step.recovery * 0.7f));
        }

        void OnHit(HitReport r)
        {
            if (_anim == null || r.Result != HitResult.Hit || r.Amount <= 0f || !_anim.Art.Has("hurt")) return;
            _anim.PlayOnce("hurt", 0.28f);
        }
    }
}
