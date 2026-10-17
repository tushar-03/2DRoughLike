using System.Collections;
using UnityEngine;
using Roughlike.Player;

namespace Roughlike.Combat
{
    /// <summary>
    /// Graybox stand-in for the run/death flow (M3+): when the player dies, wait, then restore health and
    /// put them back at the start. The real permadeath loop replaces this.
    /// </summary>
    public class DeathRespawn : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerController player;
        [SerializeField] float delay = 1.2f;
        Vector2 _start;

        public void Bind(Health h, PlayerController p)
        {
            playerHealth = h;
            player = p;
            _start = p.transform.position;
        }

        void Start()
        {
            if (player != null) _start = player.transform.position;
        }

        void OnEnable()
        {
            if (playerHealth != null) playerHealth.Died += OnDied;
        }

        void OnDisable()
        {
            if (playerHealth != null) playerHealth.Died -= OnDied;
        }

        void OnDied() => StartCoroutine(Respawn());

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(delay);
            playerHealth.ResetToFull();
            player.ResetTo(_start);
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.Interrupt();
        }
    }
}
