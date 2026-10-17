using UnityEngine;

namespace Roughlike.Player
{
    /// <summary>
    /// Climbable zone. Put on a trigger BoxCollider2D on the Ladder layer.
    /// The collider spans from the bottom rung to the surface the player steps out onto (TopY).
    /// To climb through a floor, leave a gap in the floor and fill it with a thin OneWay platform at TopY.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Ladder : MonoBehaviour
    {
        BoxCollider2D _box;

        BoxCollider2D Box => _box != null ? _box : (_box = GetComponent<BoxCollider2D>());

        public float CenterX => Box.bounds.center.x;
        public float TopY => Box.bounds.max.y;
        public float BottomY => Box.bounds.min.y;

        void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

        void OnDrawGizmos()
        {
            if (Box == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Box.bounds.center, Box.bounds.size);
        }
    }
}
