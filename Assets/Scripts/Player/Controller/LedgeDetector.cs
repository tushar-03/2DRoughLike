using UnityEngine;
using Roughlike.Core;

namespace Roughlike.Player
{
    public struct LedgeInfo
    {
        public int Dir;
        /// <summary>Feet position while hanging.</summary>
        public Vector2 HangFeet;
        /// <summary>Feet position once climbed on top.</summary>
        public Vector2 ClimbFeet;
    }

    /// <summary>Stateless ledge probing for the player body. Only solid Ground layer geometry can be grabbed.</summary>
    public static class LedgeDetector
    {
        public static bool TryFind(KinematicMotor2D motor, MovementConfig cfg, int dir, out LedgeInfo info)
        {
            info = default;
            if (dir == 0) return false;

            LayerMask mask = GameLayers.GroundMask;
            Bounds b = motor.Box.bounds;
            float top = b.max.y;
            float halfW = b.extents.x;
            float reach = halfW + cfg.ledgeReach;
            Vector2 horizontal = Vector2.right * dir;

            // 1. A vertical wall just in front of the upper body.
            RaycastHit2D wall = Physics2D.Raycast(new Vector2(b.center.x, top - 0.15f), horizontal, reach, mask);
            if (!wall || Mathf.Abs(wall.normal.x) < 0.9f) return false;

            // 2. Free space beside the head above the corner.
            Vector2 clearOrigin = new Vector2(b.center.x, top + cfg.ledgeClearance);
            if (Physics2D.Raycast(clearOrigin, horizontal, reach + 0.1f, mask)) return false;

            // 3. Find the corner's top surface.
            Vector2 downOrigin = new Vector2(wall.point.x + dir * 0.05f, top + cfg.ledgeClearance);
            RaycastHit2D topHit = Physics2D.Raycast(downOrigin, Vector2.down, cfg.ledgeClearance + cfg.ledgeGrabWindow, mask);
            if (!topHit || topHit.normal.y < 0.9f) return false;

            float ledgeTop = topHit.point.y;
            float fullHeight = cfg.colliderSize.y;

            Vector2 climbFeet = new Vector2(wall.point.x + dir * (halfW + 0.1f), ledgeTop + KinematicMotor2D.SkinWidth);

            // 4. Room to stand up on top.
            Vector2 standCentre = climbFeet + new Vector2(0f, fullHeight * 0.5f + KinematicMotor2D.SkinWidth);
            Vector2 standSize = new Vector2(cfg.colliderSize.x - 0.06f, fullHeight - 0.06f);
            if (Physics2D.OverlapBox(standCentre, standSize, 0f, mask) != null) return false;

            info = new LedgeInfo
            {
                Dir = dir,
                HangFeet = new Vector2(wall.point.x - dir * (halfW + KinematicMotor2D.SkinWidth + 0.02f), ledgeTop - 0.02f - fullHeight),
                ClimbFeet = climbFeet
            };
            return true;
        }
    }
}

