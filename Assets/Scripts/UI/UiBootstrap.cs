using UnityEngine;
using Roughlike.Combat;
using Roughlike.Items;
using Roughlike.Player;

namespace Roughlike.UI
{
    /// <summary>Drop one in a scene: builds the canvas, HUD, toasts and menus for the given player when the scene starts.</summary>
    public sealed class UiBootstrap : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        public UiRoot Root { get; private set; }
        public HudView Hud { get; private set; }
        public ModalManager Modals { get; private set; }

        public void Bind(PlayerController p) => player = p;

        void Start() => Build();

        public void Build()
        {
            if (Root != null || player == null) return;

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogError("UiBootstrap: the player has no PlayerInventory (run PlayerItemsSetup.Attach).");
                return;
            }

            Root = UiRoot.Create();
            Root.transform.SetParent(transform, false);
            Hud = HudView.Create(Root, inventory, player.GetComponent<PlayerStats>(), player.GetComponent<Health>(),
                player.GetComponent<SkillRunner>(), player.GetComponent<PlayerCombat>());
            ToastView.Create(Root);
            Modals = ModalManager.Create(Root, inventory);
        }
    }
}
