using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Combat
{
    /// <summary>Shared hitbox query used by the player's weapons and enemy melee attacks.</summary>
    public static class MeleeStrike
    {
        static readonly Collider2D[] Buffer = new Collider2D[32];

        /// <summary>
        /// Overlaps a box and applies <paramref name="info"/> to every new <see cref="Health"/> of another team inside it.
        /// <paramref name="alreadyHit"/> prevents multi-hits from one swing. Returns the number of targets struck.
        /// </summary>
        public static int Strike(Vector2 centre, Vector2 size, LayerMask targetMask, DamageInfo info,
            HashSet<Health> alreadyHit, System.Action<Health, HitResult> onResult = null)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = targetMask, useTriggers = true };
            int count = Physics2D.OverlapBox(centre, size, 0f, filter, Buffer);
            int struck = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = Buffer[i];
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                if (h.Team == info.SourceTeam && h.Team != Team.Neutral) continue;
                if (!alreadyHit.Add(h)) continue;

                DamageInfo hit = info;
                hit.HitPoint = col.ClosestPoint(centre);
                HitResult result = h.TakeHit(hit);
                onResult?.Invoke(h, result);
                struck++;
            }
            return struck;
        }

        public static void DrawDebug(Vector2 centre, Vector2 size, Color colour, float duration = 0f)
        {
            Vector2 h = size * 0.5f;
            Vector3 a = centre + new Vector2(-h.x, -h.y), b = centre + new Vector2(h.x, -h.y);
            Vector3 c = centre + new Vector2(h.x, h.y), d = centre + new Vector2(-h.x, h.y);
            Debug.DrawLine(a, b, colour, duration); Debug.DrawLine(b, c, colour, duration);
            Debug.DrawLine(c, d, colour, duration); Debug.DrawLine(d, a, colour, duration);
        }
    }
}

