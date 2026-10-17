using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>Raised when an area attack (grenade, trap) goes off so VFX/audio can react.</summary>
    public struct ExplosionEvent
    {
        public Vector2 Position;
        public float Radius;
        public Color Colour;
    }

    /// <summary>Circular area damage: grenades, traps, explosions. Knockback pushes away from the centre.</summary>
    public static class AreaStrike
    {
        static readonly Collider2D[] Buffer = new Collider2D[48];

        public static int Circle(Vector2 centre, float radius, LayerMask targetMask, DamageInfo template, HashSet<Health> alreadyHit = null)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = targetMask, useTriggers = true };
            int count = Physics2D.OverlapCircle(centre, radius, filter, Buffer);
            var seen = alreadyHit ?? new HashSet<Health>();
            int struck = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = Buffer[i];
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                if (h.Team == template.SourceTeam && h.Team != Team.Neutral) continue;
                if (!seen.Add(h)) continue;

                DamageInfo info = template;
                Vector2 closest = col.ClosestPoint(centre);
                float dir = closest.x >= centre.x ? 1f : -1f;
                info.HitPoint = closest;
                info.SourcePosition = centre;
                info.Knockback = new Vector2(template.Knockback.x * dir, template.Knockback.y);
                h.TakeHit(info);
                struck++;
            }
            return struck;
        }
    }
}
