using UnityEngine;
using Roughlike.Combat;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>Drinks a flask charge on the Flask button.</summary>
    [DefaultExecutionOrder(14)]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class FlaskUser : MonoBehaviour
    {
        PlayerInventory _inventory;
        PlayerController _controller;
        Health _health;

        [Tooltip("Run from Update. Tests turn this off and call Tick() themselves.")]
        public bool autoTick = true;

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
        }

        void Update()
        {
            if (autoTick) Tick();
        }

        public void Tick()
        {
            if (_controller.LastIntent.FlaskPressed && !_controller.IsControlLocked) _inventory.UseFlask(_health);
        }
    }
}

