using System.Collections.Generic;
using UnityEngine;
using Roughlike.Combat;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Items
{
    /// <summary>Anything the player can use with the Interact button: items on the floor, chests, shops, altars.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        /// <summary>How close (centre to centre) the player must be.</summary>
        public float Radius = 1.7f;

        public abstract string Prompt { get; }
        public virtual Color PromptColour => Color.white;
        public virtual bool CanInteract => true;
        /// <summary>An item to show a detail card for while the prompt is up (floor items).</summary>
        public virtual ItemInstance PreviewItem => null;

        public abstract void Interact(PlayerInventory inventory, PlayerController player);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }
}

