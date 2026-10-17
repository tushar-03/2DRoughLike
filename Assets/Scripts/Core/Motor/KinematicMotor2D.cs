using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>
    /// Kinematic character motor for 2D platformers. Moves a transform by casting rays from the
    /// collider edges, so movement is deterministic and fully ours (no physics solver, no tunnelling,
    /// no sticking on seams or slopes). It knows nothing about input or game rules: give it a desired
    /// displacement, it returns the displacement it could actually make and reports contacts.
    ///
    /// Handles: solid ground, slopes (climb and descend), one-way platforms, ceilings, walls.
    /// Pivot convention: the transform sits at the character's feet.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KinematicMotor2D : MonoBehaviour
    {
        public const float SkinWidth = 0.03f;
        const float MaxRaySpacing = 0.28f;
        const float OneWayTolerance = 0.06f;
        const float MaxSnapDown = 0.3f;

        [SerializeField, Range(0f, 80f)] float maxSlopeAngle = 55f;
        [SerializeField] bool drawDebugRays;

        public struct CollisionInfo
        {
            public bool above, below, left, right;
            public bool climbingSlope, descendingSlope;
            public float slopeAngle, slopeAngleOld;
            public Vector2 requestedDelta;
            public int faceDir;
            public Collider2D groundCollider;

            public void Reset()
            {
                above = below = left = right = false;
                climbingSlope = descendingSlope = false;
                slopeAngleOld = slopeAngle;
                slopeAngle = 0f;
                groundCollider = null;
            }
        }

        [System.NonSerialized] public CollisionInfo Collisions;

        /// <summary>When true one-way platforms are ignored (ladders, dropping through).</summary>
        public bool IgnoreOneWay { get; set; }

        BoxCollider2D _box;
        public BoxCollider2D Box => _box != null ? _box : (_box = GetComponent<BoxCollider2D>());
        public bool Grounded => Collisions.below;
        public bool OnOneWay => Collisions.below && Collisions.groundCollider != null
                                && Collisions.groundCollider.gameObject.layer == GameLayers.OneWay;

        /// <summary>Collider bottom relative to the pivot, and the full (standing) size; captured at Awake.</summary>
        float _bottomLocalY;
        Vector2 _fullSize;
        LayerMask _groundMask;
        LayerMask _walkableMask;

        struct RayOrigins { public Vector2 topLeft, topRight, bottomLeft, bottomRight; }
        RayOrigins _origins;
        int _hCount, _vCount;
        float _snapDown;
        float _hSpacing, _vSpacing;

        void Awake()
        {
            _fullSize = Box.size;
            _bottomLocalY = Box.offset.y - Box.size.y * 0.5f;
            _groundMask = GameLayers.GroundMask;
            _walkableMask = GameLayers.WalkableMask;
            Collisions.faceDir = 1;
        }

        public void ConfigureBody(Vector2 size)
        {
            Box.size = size;
            Box.offset = new Vector2(0f, size.y * 0.5f);
            _fullSize = size;
            _bottomLocalY = 0f;
        }

        /// <summary>Resize the collider height while keeping the feet where they are.</summary>
        public void SetHeight(float height)
        {
            Box.size = new Vector2(_fullSize.x, height);
            Box.offset = new Vector2(Box.offset.x, _bottomLocalY + height * 0.5f);
            Physics2D.SyncTransforms();
        }

        public void RestoreFullHeight() => SetHeight(_fullSize.y);

        /// <summary>True when the standing-height body would not overlap solid ground at the current position.</summary>
        public bool HasRoomForFullHeight()
        {
            Vector2 centre = (Vector2)transform.position + new Vector2(Box.offset.x, _bottomLocalY + _fullSize.y * 0.5f + SkinWidth * 0.5f);
            Vector2 size = new Vector2(_fullSize.x - SkinWidth * 2f, _fullSize.y - SkinWidth * 2f);
            return Physics2D.OverlapBox(centre, size, 0f, _groundMask) == null;
        }

        /// <summary>
        /// Moves by <paramref name="delta"/> (world units) resolving collisions. Returns the displacement actually applied.
        /// </summary>
        public Vector2 Move(Vector2 delta)
        {
            Physics2D.SyncTransforms();
            UpdateRayOrigins();

            bool wasBelow = Collisions.below;
            int face = Collisions.faceDir == 0 ? 1 : Collisions.faceDir;
            Collisions.Reset();
            Collisions.faceDir = face;
            Collisions.requestedDelta = delta;
            if (Mathf.Abs(delta.x) > 1e-5f) Collisions.faceDir = (int)Mathf.Sign(delta.x);

            // Ground snapping: while walking on the ground, follow surfaces that fall away slightly
            // (slope crests, tiny steps) instead of hopping off them.
            _snapDown = wasBelow && delta.y <= 0f ? Mathf.Min(MaxSnapDown, Mathf.Abs(delta.x) * 1.5f + SkinWidth) : 0f;

            if (delta.y < 0f && wasBelow) DescendSlope(ref delta);
            HorizontalCollisions(ref delta);
            if (Mathf.Abs(delta.y) > 1e-6f) VerticalCollisions(ref delta);

            transform.position += (Vector3)delta;
            Physics2D.SyncTransforms();
            return delta;
        }

        // ------------------------------------------------------------------ rays

        void UpdateRayOrigins()
        {
            Bounds b = Box.bounds;
            b.Expand(SkinWidth * -2f);
            _origins.bottomLeft = new Vector2(b.min.x, b.min.y);
            _origins.bottomRight = new Vector2(b.max.x, b.min.y);
            _origins.topLeft = new Vector2(b.min.x, b.max.y);
            _origins.topRight = new Vector2(b.max.x, b.max.y);

            _hCount = Mathf.Clamp(Mathf.CeilToInt(b.size.y / MaxRaySpacing) + 1, 2, 12);
            _vCount = Mathf.Clamp(Mathf.CeilToInt(b.size.x / MaxRaySpacing) + 1, 2, 8);
            _hSpacing = b.size.y / (_hCount - 1);
            _vSpacing = b.size.x / (_vCount - 1);
        }

        void HorizontalCollisions(ref Vector2 v)
        {
            bool moving = Mathf.Abs(v.x) > 1e-5f;
            int dirX = moving ? (int)Mathf.Sign(v.x) : Collisions.faceDir;
            float rayLength = moving ? Mathf.Abs(v.x) + SkinWidth : SkinWidth * 2f;
            if (moving && rayLength < SkinWidth * 2f) rayLength = SkinWidth * 2f;

            for (int i = 0; i < _hCount; i++)
            {
                Vector2 origin = dirX == -1 ? _origins.bottomLeft : _origins.bottomRight;
                origin += Vector2.up * (_hSpacing * i);
                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dirX, rayLength, _groundMask);
                if (drawDebugRays) Debug.DrawRay(origin, Vector2.right * dirX * rayLength, Color.red);
                if (!hit || hit.distance == 0f) continue;

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

                if (moving && i == 0 && slopeAngle <= maxSlopeAngle)
                {
                    if (Collisions.descendingSlope)
                    {
                        Collisions.descendingSlope = false;
                        v = Collisions.requestedDelta;
                    }
                    float distanceToSlopeStart = 0f;
                    if (!Mathf.Approximately(slopeAngle, Collisions.slopeAngleOld))
                    {
                        distanceToSlopeStart = hit.distance - SkinWidth;
                        v.x -= distanceToSlopeStart * dirX;
                    }
                    ClimbSlope(ref v, slopeAngle, hit.collider);
                    v.x += distanceToSlopeStart * dirX;
                }

                if (!Collisions.climbingSlope || slopeAngle > maxSlopeAngle)
                {
                    if (moving) v.x = (hit.distance - SkinWidth) * dirX;
                    rayLength = hit.distance;

                    if (Collisions.climbingSlope)
                        v.y = Mathf.Tan(Collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(v.x);

                    if (moving || slopeAngle > maxSlopeAngle)
                    {
                        Collisions.left = dirX == -1;
                        Collisions.right = dirX == 1;
                    }
                }
            }
        }

        void VerticalCollisions(ref Vector2 v)
        {
            float dirY = Mathf.Sign(v.y);
            float rayLength = Mathf.Abs(v.y) + SkinWidth + (dirY < 0f ? _snapDown : 0f);
            LayerMask mask = (IgnoreOneWay || dirY > 0f) ? _groundMask : _walkableMask;
            int oneWayLayer = GameLayers.OneWay;

            for (int i = 0; i < _vCount; i++)
            {
                Vector2 origin = dirY == -1 ? _origins.bottomLeft : _origins.topLeft;
                origin += Vector2.right * (_vSpacing * i + v.x);
                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up * dirY, rayLength, mask);
                if (drawDebugRays) Debug.DrawRay(origin, Vector2.up * dirY * rayLength, Color.green);
                if (!hit) continue;

                // One-way: only solid when landing on it from above with the feet at or above its top.
                if (hit.collider.gameObject.layer == oneWayLayer)
                {
                    if (hit.collider.bounds.max.y > _origins.bottomLeft.y - SkinWidth + OneWayTolerance) continue;
                }

                v.y = (hit.distance - SkinWidth) * dirY;
                rayLength = hit.distance;

                if (Collisions.climbingSlope)
                    v.x = v.y / Mathf.Tan(Collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(v.x);

                Collisions.below = dirY == -1;
                Collisions.above = dirY == 1;
                if (dirY == -1) Collisions.groundCollider = hit.collider;
            }

            // Slope changes mid-move: re-check ahead at the new height so we do not clip into a steeper slope.
            if (Collisions.climbingSlope)
            {
                float dirX = Mathf.Sign(v.x);
                rayLength = Mathf.Abs(v.x) + SkinWidth;
                Vector2 origin = (dirX == -1 ? _origins.bottomLeft : _origins.bottomRight) + Vector2.up * v.y;
                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dirX, rayLength, _groundMask);
                if (hit)
                {
                    float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                    if (!Mathf.Approximately(slopeAngle, Collisions.slopeAngle))
                    {
                        v.x = (hit.distance - SkinWidth) * dirX;
                        Collisions.slopeAngle = slopeAngle;
                        Collisions.groundCollider = hit.collider;
                    }
                }
            }
        }

        void ClimbSlope(ref Vector2 v, float slopeAngle, Collider2D slope)
        {
            float moveDistance = Mathf.Abs(v.x);
            float climbVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
            if (v.y <= climbVelocityY)
            {
                v.y = climbVelocityY;
                v.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(v.x);
                Collisions.below = true;
                Collisions.climbingSlope = true;
                Collisions.slopeAngle = slopeAngle;
                Collisions.groundCollider = slope;
            }
        }

        void DescendSlope(ref Vector2 v)
        {
            if (Mathf.Abs(v.x) <= 1e-5f) return;
            float dirX = Mathf.Sign(v.x);
            Vector2 origin = dirX == -1 ? _origins.bottomRight : _origins.bottomLeft;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, Mathf.Infinity, _groundMask);
            if (!hit) return;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle == 0f || slopeAngle > maxSlopeAngle) return;
            if (Mathf.Sign(hit.normal.x) != dirX) return;
            if (hit.distance - SkinWidth > Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(v.x)) return;

            float moveDistance = Mathf.Abs(v.x);
            float descendY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;
            v.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(v.x);
            v.y -= descendY;
            Collisions.slopeAngle = slopeAngle;
            Collisions.descendingSlope = true;
            Collisions.below = true;
            Collisions.groundCollider = hit.collider;
        }

        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            Gizmos.color = Collisions.below ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
        }
    }
}



