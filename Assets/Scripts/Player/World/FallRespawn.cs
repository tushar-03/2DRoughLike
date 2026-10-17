using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>Graybox helper: puts the player back at the start when they fall below a height.</summary>
    public class FallRespawn : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float killY = -20f;
        Vector2 _start;

        public void SetPlayer(PlayerController p) { player = p; _start = p.transform.position; }

        void Start()
        {
            if (player != null) _start = player.transform.position;
        }

        void Update()
        {
            if (player != null && player.transform.position.y < killY) player.ResetTo(_start);
        }
    }
}
