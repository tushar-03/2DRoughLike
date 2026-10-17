using System;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>
    /// Runs the two skill slots: input, cooldowns and spawning grenades, turrets and traps with damage scaled by
    /// the player's stats. Runs after the controller (reads <see cref="PlayerController.LastIntent"/>).
    /// </summary>
    [DefaultExecutionOrder(12)]
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerStats))]
    public sealed class SkillRunner : MonoBehaviour
    {
        PlayerInventory _inventory;
        PlayerStats _stats;
        PlayerController _controller;
        Health _health;

        readonly float[] _remaining = new float[2];
        readonly float[] _total = new float[2];

        [Tooltip("Run from Update. Tests turn this off and call Tick(dt) themselves.")]
        public bool autoTick = true;

        public event Action<int, SkillData> SkillUsed;

        public float CooldownRemaining(int slot) => _remaining[slot];
        public float CooldownFraction(int slot) => _total[slot] <= 0f ? 0f : Mathf.Clamp01(_remaining[slot] / _total[slot]);

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _stats = GetComponent<PlayerStats>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
        }

        void OnEnable() => _inventory.RunReset += ResetCooldowns;
        void OnDisable() => _inventory.RunReset -= ResetCooldowns;

        void ResetCooldowns()
        {
            _remaining[0] = _remaining[1] = 0f;
            _total[0] = _total[1] = 0f;
        }

        void Update()
        {
            if (autoTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < 2; i++) _remaining[i] = Mathf.Max(0f, _remaining[i] - dt);

            if (_health != null && _health.IsDead) return;
            if (_controller.IsControlLocked) return;

            PlayerIntent intent = _controller.LastIntent;
            if (intent.Skill1Pressed) TryUse(0);
            if (intent.Skill2Pressed) TryUse(1);
        }

        public bool TryUse(int slot)
        {
            ItemInstance item = _inventory.Skills[slot];
            if (item == null || item.Skill == null || _remaining[slot] > 0f) return false;

            SkillData data = item.Skill;
            var payload = new SkillPayload { Data = data, Damage = _stats.SkillDamage(slot), Tint = data.tint };
            Vector2 feet = transform.position;
            int facing = _controller.Facing;

            switch (data.kind)
            {
                case SkillKind.Grenade:
                    var g = GrenadeProjectile.Throw(feet + new Vector2(facing * 0.5f, 1.2f), new Vector2(facing * 9f + _controller.Velocity.x * 0.3f, 8f), payload);
                    g.autoTick = autoTick;
                    break;
                case SkillKind.Turret:
                    var t = TurretEntity.Place(feet + new Vector2(facing * 0.9f, 0f), payload);
                    t.autoTick = autoTick;
                    break;
                case SkillKind.Trap:
                    var trap = TrapEntity.Place(feet + new Vector2(facing * 0.8f, 0f), payload);
                    trap.autoTick = autoTick;
                    break;
            }

            _total[slot] = _stats.SkillCooldown(slot);
            _remaining[slot] = _total[slot];
            SkillUsed?.Invoke(slot, data);
            return true;
        }
    }
}
