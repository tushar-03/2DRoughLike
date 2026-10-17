using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>
    /// On the player: finds the nearest interactable in reach, publishes its prompt for the HUD and uses it
    /// when Interact is pressed. Runs after the controller (reads <see cref="PlayerController.LastIntent"/>).
    /// </summary>
    [DefaultExecutionOrder(13)]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class InteractionSensor : MonoBehaviour
    {
        PlayerInventory _inventory;
        PlayerController _controller;
        Health _health;
        Interactable _current;
        string _lastPrompt;

        [Tooltip("Run from Update. Tests turn this off and call Tick() themselves.")]
        public bool autoTick = true;

        public Interactable Current => _current;

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
        }

        void OnDisable() => Publish(null);

        void Update()
        {
            if (autoTick) Tick();
        }

        public void Tick()
        {
            if (_health != null && _health.IsDead) { Publish(null); return; }

            Vector2 centre = (Vector2)transform.position + Vector2.up * 0.8f;
            Interactable best = null;
            float bestDist = float.MaxValue;
            foreach (var i in Interactable.All)
            {
                if (i == null || !i.CanInteract) continue;
                float d = Vector2.Distance(centre, i.transform.position);
                if (d <= i.Radius && d < bestDist) { best = i; bestDist = d; }
            }

            _current = best;
            Publish(best);

            if (best != null && _controller.LastIntent.InteractPressed && !_controller.IsControlLocked)
                best.Interact(_inventory, _controller);
        }

        void Publish(Interactable i)
        {
            string text = i != null ? i.Prompt : null;
            if (text == _lastPrompt) return;
            _lastPrompt = text;
            EventBus<InteractPromptEvent>.Raise(new InteractPromptEvent
            {
                Visible = i != null,
                Text = text,
                Colour = i != null ? i.PromptColour : Color.white,
                Item = i != null ? i.PreviewItem : null
            });
        }
    }
}


