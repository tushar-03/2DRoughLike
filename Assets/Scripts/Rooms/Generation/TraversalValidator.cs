using System.Collections.Generic;
using UnityEngine;
using Roughlike.Core;
using Roughlike.Player;

namespace Roughlike.Rooms
{
    /// <summary>
    /// Checks that a built level can be finished with the base movement kit. It finds every standable surface in the real
    /// colliders, links surfaces the player can walk, jump or drop between (using the actual jump height, gravity and run speed
    /// with a safety margin, and sweeping the player's body along the arc so ceilings and walls count), then searches from the
    /// spawn. Rolls, wall jumps and double jumps are deliberately not counted: nothing mandatory may need them.
    /// </summary>
    public static class TraversalValidator
    {
        public sealed class Report
        {
            public int Nodes;
            public readonly List<Vector2> Reached = new List<Vector2>();
            public readonly List<Vector2> Missed = new List<Vector2>();
            public bool ExitReachable;
            public readonly List<string> Unreachable = new List<string>();
            public bool Ok => ExitReachable && Unreachable.Count == 0;
            public override string ToString() => Ok ? $"ok ({Nodes} surfaces)" : "unreachable: " + string.Join(", ", Unreachable) + (ExitReachable ? "" : ", EXIT");
        }

        struct Node { public Vector2 Pos; public int Collider; }

        // Safety margins: a real player is never perfectly efficient.
        const float ReachMargin = 0.8f;
        const float HeightMargin = 0.9f;
        static readonly Vector2 Body = new Vector2(0.6f, 1.5f);

        public static Report Validate(BuiltLevel level, MovementConfig cfg)
        {
            Physics2D.SyncTransforms();
            var report = new Report();
            LayerMask ground = GameLayers.GroundMask;
            int groundLayer = LayerMask.NameToLayer(GameLayers.GroundName);
            int oneWayLayer = LayerMask.NameToLayer(GameLayers.OneWayName);

            // ---- standable surfaces
            var nodes = new List<Node>();
            var colliders = level.Root.GetComponentsInChildren<BoxCollider2D>();
            for (int ci = 0; ci < colliders.Length; ci++)
            {
                var col = colliders[ci];
                int layer = col.gameObject.layer;
                if (layer != groundLayer && layer != oneWayLayer) continue;
                Bounds b = col.bounds;
                float x0 = b.min.x + 0.35f, x1 = b.max.x - 0.35f;
                if (x1 < x0) continue;
                var xs = new List<float> { x0 };
                for (float x = x0 + 1.5f; x < x1 - 0.2f; x += 1.5f) xs.Add(x);
                if (x1 > x0 + 0.01f) xs.Add(x1);
                foreach (float x in xs)
                {
                    Vector2 feet = new Vector2(x, b.max.y);
                    var head = Physics2D.OverlapBox(feet + new Vector2(0f, 0.05f + Body.y * 0.5f + 0.02f), new Vector2(Body.x, Body.y - 0.1f), 0f, ground);
                    if (head != null && head != col) continue;       // no room to stand
                    nodes.Add(new Node { Pos = feet, Collider = ci });
                }
            }
            report.Nodes = nodes.Count;
            if (nodes.Count == 0) { report.Unreachable.Add("no surfaces"); return report; }

            // ---- links
            float g = cfg.Gravity, gDown = g * cfg.fallGravityMultiplier;
            float maxUp = cfg.jumpHeight * HeightMargin;
            var adj = new List<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) adj[i] = new List<int>();

            for (int i = 0; i < nodes.Count; i++)
            for (int j = 0; j < nodes.Count; j++)
            {
                if (i == j) continue;
                Vector2 a = nodes[i].Pos, b = nodes[j].Pos;
                float dx = Mathf.Abs(b.x - a.x), dy = b.y - a.y;
                if (dx > 10f || dy > maxUp || dy < -40f) continue;

                // walking along one surface, or across a seam between flush surfaces
                if (Mathf.Abs(dy) < 0.06f && dx <= 1.6f && nodes[i].Collider == nodes[j].Collider) { adj[i].Add(j); continue; }
                if (Mathf.Abs(dy) < 0.06f && dx <= 0.8f && WalkClear(a, b, ground)) { adj[i].Add(j); continue; }

                // a jump or a drop: how far can the player get while at height dy?
                float flight = cfg.timeToApex + Mathf.Sqrt(2f * Mathf.Max(0.01f, cfg.jumpHeight - dy) / gDown);
                float reach = cfg.runSpeed * flight * ReachMargin;
                if (dx > reach) continue;
                if (ArcClear(a, b, cfg.jumpHeight, ground)) adj[i].Add(j);
            }

            // ---- search from the spawn
            int start = Nearest(nodes, level.PlayerSpawn, 2.5f);
            if (start < 0) { report.Unreachable.Add("spawn"); return report; }
            var seen = new bool[nodes.Count];
            var queue = new Queue<int>();
            queue.Enqueue(start); seen[start] = true;
            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                foreach (int m in adj[n]) if (!seen[m]) { seen[m] = true; queue.Enqueue(m); }
            }

            for (int i = 0; i < nodes.Count; i++) (seen[i] ? report.Reached : report.Missed).Add(nodes[i].Pos);

            bool Reached(Vector2 p, float radius)
            {
                for (int i = 0; i < nodes.Count; i++)
                    if (seen[i] && Mathf.Abs(nodes[i].Pos.x - p.x) <= radius && Mathf.Abs(nodes[i].Pos.y - p.y) <= 1.2f) return true;
                return false;
            }

            report.ExitReachable = level.Exit == null || Reached(level.Exit.transform.position, 1.6f);
            foreach (var c in level.Chests) if (!Reached(c.transform.position, 1.6f)) report.Unreachable.Add("chest " + c.CellIndex);
            foreach (var s in level.Shops) if (!Reached(s.transform.position, 1.6f)) report.Unreachable.Add("shop " + s.CellIndex);
            foreach (var a in level.Altars) if (!Reached(a.transform.position, 1.6f)) report.Unreachable.Add("altar " + a.CellIndex);
            return report;
        }

        static int Nearest(List<Node> nodes, Vector2 p, float maxDist)
        {
            int best = -1; float bd = maxDist;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = Vector2.Distance(nodes[i].Pos, p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static bool WalkClear(Vector2 a, Vector2 b, LayerMask ground)
        {
            Vector2 mid = (a + b) * 0.5f + new Vector2(0f, 0.05f + Body.y * 0.5f + 0.02f);
            return Physics2D.OverlapBox(mid, new Vector2(Mathf.Abs(b.x - a.x) + Body.x, Body.y - 0.1f), 0f, ground) == null;
        }

        /// <summary>True when the body can travel from a to b along a jump arc (either a low hop or a full jump) without hitting solid ground.</summary>
        static bool ArcClear(Vector2 a, Vector2 b, float jumpHeight, LayerMask ground)
        {
            float top = Mathf.Max(a.y, b.y);
            foreach (float bulge in new[] { 0.5f, jumpHeight })
            {
                // parabola from a to b whose apex is `bulge` above the higher end
                float h = bulge + Mathf.Abs(b.y - a.y) * 0.25f;
                bool clear = true;
                const int N = 14;
                for (int k = 1; k < N && clear; k++)
                {
                    float s = k / (float)N;
                    if (s < 0.08f || s > 0.92f) continue;
                    float x = Mathf.Lerp(a.x, b.x, s);
                    float y = Mathf.Lerp(a.y, b.y, s) + 4f * h * s * (1f - s);
                    if (Physics2D.OverlapBox(new Vector2(x, y + 0.05f + Body.y * 0.5f), Body, 0f, ground) != null) clear = false;
                }
                if (clear) return true;
            }
            return false;
        }
    }
}
