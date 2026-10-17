using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>How one shape is coloured: three cel tones lit from the upper left, a rim light from behind and an edge line.</summary>
    public struct Mat
    {
        public Color Light, Base, Shade, Rim, Edge;
        public float RimWidth;
        public bool Flat;      // one flat colour (glowing eyes, metal highlights)
        public bool NoEdge;

        /// <summary>Derives the full cel ramp from one base colour; shadows shift toward cool purple so they stay rich.</summary>
        public static Mat Of(Color baseCol, Color? rim = null, float rimWidth = 4.5f)
        {
            Color shade = Color.Lerp(baseCol * 0.58f, new Color(0.16f, 0.1f, 0.3f), 0.28f);
            shade.a = 1f;
            Color light = Color.Lerp(baseCol, new Color(1f, 0.95f, 0.8f), 0.24f);
            light.a = 1f;
            Color edge = shade * 0.55f;
            edge.a = 1f;
            return new Mat
            {
                Light = light, Base = baseCol, Shade = shade, Edge = edge,
                Rim = rim ?? Color.Lerp(baseCol, new Color(1f, 0.85f, 0.6f), 0.6f), RimWidth = rim.HasValue ? rimWidth : 0f
            };
        }

        public static Mat Glow(Color c) => new Mat { Light = c, Base = c, Shade = c, Edge = c, Rim = c, Flat = true, NoEdge = true };
    }

    abstract class Shape
    {
        public Mat M;
        public Vector2 Min, Max;
        /// <summary>Signed distance (negative inside) in final-resolution pixels, plus the outward normal.</summary>
        public abstract float Dist(Vector2 p, out Vector2 n);

        protected static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }

    sealed class CapsuleShape : Shape
    {
        readonly Vector2 _a, _b;
        readonly float _ra, _rb;

        public CapsuleShape(Vector2 a, Vector2 b, float ra, float rb)
        {
            _a = a; _b = b; _ra = ra; _rb = rb;
            float r = Mathf.Max(ra, rb) + 1f;
            Min = Vector2.Min(a, b) - Vector2.one * r;
            Max = Vector2.Max(a, b) + Vector2.one * r;
        }

        public override float Dist(Vector2 p, out Vector2 n)
        {
            Vector2 ab = _b - _a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - _a, ab) / len2);
            Vector2 q = _a + ab * t;
            Vector2 d = p - q;
            float dist = d.magnitude;
            n = dist < 1e-3f ? Vector2.up : d / dist;
            return dist - Mathf.Lerp(_ra, _rb, t);
        }
    }

    sealed class EllipseShape : Shape
    {
        readonly Vector2 _c;
        readonly float _rx, _ry, _cos, _sin;

        public EllipseShape(Vector2 c, float rx, float ry, float rotDeg)
        {
            _c = c; _rx = rx; _ry = ry;
            float r = rotDeg * Mathf.Deg2Rad;
            _cos = Mathf.Cos(r); _sin = Mathf.Sin(r);
            float m = Mathf.Max(rx, ry) + 1f;
            Min = c - Vector2.one * m; Max = c + Vector2.one * m;
        }

        public override float Dist(Vector2 p, out Vector2 n)
        {
            Vector2 d = p - _c;
            float lx = d.x * _cos + d.y * _sin;      // into the ellipse's frame
            float ly = -d.x * _sin + d.y * _cos;
            float qx = lx / _rx, qy = ly / _ry;
            float len = Mathf.Sqrt(qx * qx + qy * qy);
            // gradient of the implicit function, back to world space
            float gx = lx / (_rx * _rx), gy = ly / (_ry * _ry);
            float wx = gx * _cos - gy * _sin, wy = gx * _sin + gy * _cos;
            float gl = Mathf.Sqrt(wx * wx + wy * wy);
            n = gl < 1e-6f ? Vector2.up : new Vector2(wx / gl, wy / gl);
            return (len - 1f) * Mathf.Min(_rx, _ry);
        }
    }

    sealed class PolyShape : Shape
    {
        readonly Vector2[] _p;
        readonly Vector2 _centroid;

        public PolyShape(Vector2[] pts)
        {
            _p = pts;
            Min = pts[0]; Max = pts[0];
            Vector2 c = Vector2.zero;
            foreach (var v in pts) { Min = Vector2.Min(Min, v); Max = Vector2.Max(Max, v); c += v; }
            _centroid = c / pts.Length;
            Min -= Vector2.one; Max += Vector2.one;
        }

        public override float Dist(Vector2 p, out Vector2 n)
        {
            float best = float.MaxValue;
            Vector2 bestPoint = _p[0];
            bool inside = false;
            for (int i = 0, j = _p.Length - 1; i < _p.Length; j = i++)
            {
                Vector2 a = _p[j], b = _p[i];
                if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;

                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                Vector2 q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d < best) { best = d; bestPoint = q; }
            }
            float dist = Mathf.Sqrt(best);
            Vector2 toBoundary = bestPoint - p;
            Vector2 outward = inside ? toBoundary : -toBoundary;
            n = outward.sqrMagnitude < 1e-6f ? (p - _centroid).normalized : outward.normalized;
            // blend with the direction from the centre so big flat faces still shade smoothly
            n = Vector2.Lerp(n, (p - _centroid).normalized, 0.35f).normalized;
            return inside ? -dist : dist;
        }
    }

    /// <summary>
    /// Software rasteriser for one sprite frame: shapes are drawn back to front with cel shading, rim light and edge lines,
    /// then a dark outline is grown around the union. Everything is rendered 2x oversampled and averaged down for smooth edges.
    /// Coordinates are final-resolution pixels with y pointing up.
    /// </summary>
    public sealed class Painter
    {
        public const int Supersample = 2;

        public readonly int Width, Height;
        readonly List<Shape> _shapes = new List<Shape>();

        static readonly Vector2 LightDir = new Vector2(-0.42f, 0.8f).normalized;
        static readonly Vector2 RimDir = new Vector2(0.92f, 0.25f).normalized;

        public Painter(int width, int height) { Width = width; Height = height; }

        public void Capsule(Vector2 a, Vector2 b, float ra, float rb, Mat m) => _shapes.Add(new CapsuleShape(a, b, ra, rb) { M = m });
        public void Capsule(Vector2 a, Vector2 b, float r, Mat m) => Capsule(a, b, r, r, m);
        public void Circle(Vector2 c, float r, Mat m) => _shapes.Add(new EllipseShape(c, r, r, 0f) { M = m });
        public void Ellipse(Vector2 c, float rx, float ry, float rotDeg, Mat m) => _shapes.Add(new EllipseShape(c, rx, ry, rotDeg) { M = m });
        public void Poly(Vector2[] pts, Mat m) => _shapes.Add(new PolyShape(pts) { M = m });

        public Color32[] Render(float outlinePx, Color outline)
        {
            int S = Supersample;
            int sw = Width * S, sh = Height * S;
            var col = new Color[sw * sh];
            var covered = new bool[sw * sh];

            foreach (var s in _shapes)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(s.Min.x * S)), x1 = Mathf.Min(sw - 1, Mathf.CeilToInt(s.Max.x * S));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(s.Min.y * S)), y1 = Mathf.Min(sh - 1, Mathf.CeilToInt(s.Max.y * S));
                Mat m = s.M;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                    float d = s.Dist(p, out Vector2 n);
                    if (d > 0f) continue;

                    Color c;
                    if (m.Flat) c = m.Base;
                    else
                    {
                        float lit = Vector2.Dot(n, LightDir);
                        c = lit > 0.3f ? m.Light : (lit > -0.35f ? m.Base : m.Shade);
                        if (m.RimWidth > 0f && -d < m.RimWidth && Vector2.Dot(n, RimDir) > 0.55f) c = m.Rim;
                        if (!m.NoEdge && -d < 1.7f) c = Color.Lerp(c, m.Edge, 0.85f);
                    }
                    c.a = 1f;
                    int i = y * sw + x;
                    col[i] = c;
                    covered[i] = true;
                }
            }

            GrowOutline(col, covered, sw, sh, outlinePx * S, outline);
            return Downsample(col, covered, sw, sh, S);
        }

        /// <summary>Chamfer distance transform from the covered pixels; uncovered pixels within the radius become outline.</summary>
        static void GrowOutline(Color[] col, bool[] covered, int w, int h, float radius, Color outline)
        {
            if (radius <= 0f) return;
            var dist = new float[w * h];
            const float Inf = 1e9f;
            for (int i = 0; i < dist.Length; i++) dist[i] = covered[i] ? 0f : Inf;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float d = dist[i];
                if (x > 0) d = Mathf.Min(d, dist[i - 1] + 1f);
                if (y > 0)
                {
                    d = Mathf.Min(d, dist[i - w] + 1f);
                    if (x > 0) d = Mathf.Min(d, dist[i - w - 1] + 1.414f);
                    if (x < w - 1) d = Mathf.Min(d, dist[i - w + 1] + 1.414f);
                }
                dist[i] = d;
            }
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                float d = dist[i];
                if (x < w - 1) d = Mathf.Min(d, dist[i + 1] + 1f);
                if (y < h - 1)
                {
                    d = Mathf.Min(d, dist[i + w] + 1f);
                    if (x < w - 1) d = Mathf.Min(d, dist[i + w + 1] + 1.414f);
                    if (x > 0) d = Mathf.Min(d, dist[i + w - 1] + 1.414f);
                }
                dist[i] = d;
            }

            outline.a = 1f;
            for (int i = 0; i < dist.Length; i++)
            {
                if (covered[i] || dist[i] > radius) continue;
                col[i] = outline;
                covered[i] = true;
            }
        }

        static Color32[] Downsample(Color[] col, bool[] covered, int sw, int sh, int S)
        {
            int w = sw / S, h = sh / S;
            var result = new Color32[w * h];
            float inv = 1f / (S * S);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < S; sy++)
                for (int sx = 0; sx < S; sx++)
                {
                    int i = (y * S + sy) * sw + x * S + sx;
                    if (!covered[i]) continue;
                    r += col[i].r; g += col[i].g; b += col[i].b; a += 1f;
                }
                if (a <= 0f) { result[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                result[y * w + x] = new Color32(
                    (byte)Mathf.Clamp(r / a * 255f, 0, 255),
                    (byte)Mathf.Clamp(g / a * 255f, 0, 255),
                    (byte)Mathf.Clamp(b / a * 255f, 0, 255),
                    (byte)Mathf.Clamp(a * inv * 255f, 0, 255));
            }
            return result;
        }
    }
}
