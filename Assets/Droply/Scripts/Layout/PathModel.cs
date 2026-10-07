using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The sand path: a smooth centre line (Catmull-Rom through hand-placed points) with a width that narrows with distance.
    /// It starts at the viewer's feet as a narrow tongue that widens ahead, swings right around a mound, passes behind the tree group and leads between the forest clumps over the ridge. Everything else is placed relative to it.
    /// </summary>
    public sealed class PathModel
    {
        // x, z in metres. The viewer stands at the origin, looking along +z.
        static readonly Vector2[] Control =
        {
            new Vector2(0.0f, -1.5f), new Vector2(-0.2f, 5f), new Vector2(1.6f, 13f), new Vector2(4.8f, 22f), new Vector2(6.2f, 32f),
            new Vector2(3.8f, 43f), new Vector2(-0.4f, 54f), new Vector2(-4.6f, 66f), new Vector2(-3.6f, 80f), new Vector2(1.6f, 95f), new Vector2(5.4f, 111f)
        };

        const int SubSteps = 36;

        readonly Vector3[] points;
        readonly float[] arc;

        public float Length { get; private set; }

        public PathModel()
        {
            int segments = Control.Length - 1;
            points = new Vector3[segments * SubSteps + 1];
            arc = new float[points.Length];
            for (int s = 0; s < segments; s++)
            {
                Vector2 p0 = Control[Mathf.Max(s - 1, 0)], p1 = Control[s], p2 = Control[s + 1], p3 = Control[Mathf.Min(s + 2, Control.Length - 1)];
                for (int k = 0; k < SubSteps; k++)
                {
                    float f = (float)k / SubSteps;
                    Vector2 p = .5f * ((2f * p1) + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
                    points[s * SubSteps + k] = new Vector3(p.x, 0f, p.y);
                }
            }
            points[points.Length - 1] = new Vector3(Control[Control.Length - 1].x, 0f, Control[Control.Length - 1].y);
            for (int i = 1; i < points.Length; i++) arc[i] = arc[i - 1] + (points[i] - points[i - 1]).magnitude;
            Length = arc[arc.Length - 1];
        }

        /// <summary>Half the width of the sand at arc length s: about 2.4 m wide near the viewer, about 1.9 m far away.</summary>
        public float HalfWidth(float s)
        {
            float t = Mathf.Clamp01(s / Length);
            return Mathf.Lerp(1.2f, 0.95f, Mathf.SmoothStep(0f, 1f, t));
        }

        /// <summary>Centre line point at arc length s (clamped); y is 0, the caller puts it on the terrain.</summary>
        public Vector3 PointAt(float s)
        {
            s = Mathf.Clamp(s, 0f, Length);
            int lo = 0, hi = arc.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (arc[mid] <= s) lo = mid; else hi = mid;
            }
            float span = arc[hi] - arc[lo];
            return Vector3.Lerp(points[lo], points[hi], span > 1e-6f ? (s - arc[lo]) / span : 0f);
        }

        /// <summary>Unit direction of the centre line (x, z) at arc length s.</summary>
        public Vector3 DirectionAt(float s)
        {
            Vector3 d = PointAt(s + 0.5f) - PointAt(s - 0.5f);
            d.y = 0f;
            return d.sqrMagnitude < 1e-8f ? Vector3.forward : d.normalized;
        }

        /// <summary>Distance from (x, z) to the centre line; <paramref name="s"/> is the arc length of the nearest point.</summary>
        public float Distance(float x, float z, out float s)
        {
            float best = float.MaxValue, bestS = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float ax = points[i].x, az = points[i].z, bx = points[i + 1].x - ax, bz = points[i + 1].z - az;
                float len2 = bx * bx + bz * bz;
                float t = len2 > 1e-9f ? Mathf.Clamp01(((x - ax) * bx + (z - az) * bz) / len2) : 0f;
                float dx = x - (ax + bx * t), dz = z - (az + bz * t);
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; bestS = arc[i] + (arc[i + 1] - arc[i]) * t; }
            }
            s = bestS;
            return Mathf.Sqrt(best);
        }

        public float Distance(float x, float z) { float s; return Distance(x, z, out s); }

        /// <summary>Distance from (x, z) to the edge of the sand (negative: on the path).</summary>
        public float EdgeDistance(float x, float z)
        {
            float s;
            float d = Distance(x, z, out s);
            return d - HalfWidth(s);
        }
    }
}
