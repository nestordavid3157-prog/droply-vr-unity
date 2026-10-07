using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// Where the viewer may walk: inside the organic outline <see cref="Plan.WalkOutline"/>, outside small discs around trunks, bushes and stones.
    /// <see cref="Distance"/> is a signed distance in metres (negative inside), so the locomotion (<see cref="Walker"/>) can slow down softly before the edge and glide
    /// along it instead of bumping into an invisible wall. Points are (x, z) on the ground; the head is the reference point.
    /// </summary>
    public sealed class WalkArea
    {
        public struct Disc { public Vector2 Centre; public float Radius; }

        /// <summary>Room the body needs beside a trunk (the head is the reference, shoulders and feet are a little wider).</summary>
        public const float BodyRadius = .35f;

        /// <summary>
        /// The camera's far plane. The sky dome follows the viewer (it is "at infinity"); everything else stays within this distance from every walkable point (checked),
        /// so walking never clips a hill or a cloud.
        /// </summary>
        public const float FarClip = 400f;

        const int SubSteps = 8;

        readonly Vector2[] outline;
        readonly Disc[] obstacles;
        readonly TerrainModel terrain;

        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }

        /// <summary>The smoothed outline (closed: the last point connects to the first).</summary>
        public Vector2[] Outline { get { return outline; } }

        public Disc[] Obstacles { get { return obstacles; } }

        public WalkArea(TerrainModel terrain, Vector2[] controlPoints, List<Disc> obstacles)
        {
            this.terrain = terrain;
            outline = Smooth(controlPoints);
            Vector2 min = outline[0], max = outline[0];
            foreach (var p in outline) { min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y)); max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y)); }
            Min = min; Max = max;
            // only what can be reached matters
            var near = new List<Disc>();
            foreach (var d in obstacles)
                if (d.Centre.x + d.Radius > min.x && d.Centre.x - d.Radius < max.x && d.Centre.y + d.Radius > min.y && d.Centre.y - d.Radius < max.y) near.Add(d);
            this.obstacles = near.ToArray();
        }

        /// <summary>The walk area of a built landscape: the planned outline, kept clear of every trunk, bush and stone.</summary>
        public static WalkArea For(SceneData data)
        {
            var obstacles = new List<Disc>();
            foreach (var t in data.Trees)
            {
                if (t.Tier == TreeTier.Mid || t.Tier == TreeTier.Far) continue;
                float radius;
                if (t.Kind == TreeKind.Shrub) radius = t.Spread * .9f;                                    // walk around a bush, not into it
                else if (t.Kind == TreeKind.Spruce) radius = Mathf.Max(TreeFactory.FootRadius(t.Kind, t.Height) + BodyRadius, t.Spread * .75f); // its lowest tier hangs at head height
                else radius = TreeFactory.FootRadius(t.Kind, t.Height) + BodyRadius;
                obstacles.Add(new Disc { Centre = new Vector2(t.Position.x, t.Position.z), Radius = radius });
            }
            foreach (var r in data.Rocks) obstacles.Add(new Disc { Centre = new Vector2(r.Position.x, r.Position.z), Radius = r.Radius + .25f });
            return new WalkArea(data.Terrain, Plan.WalkOutline, obstacles);
        }

        /// <summary>Closed Catmull-Rom curve through the control points: an organic outline without corners.</summary>
        static Vector2[] Smooth(Vector2[] c)
        {
            int n = c.Length;
            var result = new Vector2[n * SubSteps];
            for (int s = 0; s < n; s++)
            {
                Vector2 p0 = c[(s - 1 + n) % n], p1 = c[s], p2 = c[(s + 1) % n], p3 = c[(s + 2) % n];
                for (int k = 0; k < SubSteps; k++)
                {
                    float f = (float)k / SubSteps;
                    result[s * SubSteps + k] = .5f * ((2f * p1) + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
                }
            }
            return result;
        }

        /// <summary>Signed distance to the edge of the walkable ground in metres: negative inside (walkable), positive outside or inside an obstacle.</summary>
        public float Distance(Vector2 p)
        {
            float d = OutlineDistance(p);
            foreach (var o in obstacles)
            {
                float dx = p.x - o.Centre.x, dz = p.y - o.Centre.y;
                d = Mathf.Max(d, o.Radius - Mathf.Sqrt(dx * dx + dz * dz));
            }
            return d;
        }

        public bool Contains(Vector2 p) { return Distance(p) < 0f; }

        /// <summary>Unit direction in which <see cref="Distance"/> grows fastest (out of the area, or into the nearest obstacle).</summary>
        public Vector2 Normal(Vector2 p)
        {
            const float h = .05f;
            float gx = Distance(new Vector2(p.x + h, p.y)) - Distance(new Vector2(p.x - h, p.y));
            float gz = Distance(new Vector2(p.x, p.y + h)) - Distance(new Vector2(p.x, p.y - h));
            float m = Mathf.Sqrt(gx * gx + gz * gz);
            return m > 1e-6f ? new Vector2(gx / m, gz / m) : new Vector2(0f, 0f);
        }

        /// <summary>Height of the rendered ground under (x, z): where the floor of the tracking space goes.</summary>
        public float GroundY(Vector2 p) { return terrain.SurfaceY(p.x, p.y); }

        float OutlineDistance(Vector2 p)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                Vector2 a = outline[j], b = outline[i];
                if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                float ex = b.x - a.x, ez = b.y - a.y, len2 = ex * ex + ez * ez;
                float t = len2 > 1e-9f ? Mathf.Clamp01(((p.x - a.x) * ex + (p.y - a.y) * ez) / len2) : 0f;
                float dx = p.x - (a.x + ex * t), dz = p.y - (a.y + ez * t);
                best = Mathf.Min(best, dx * dx + dz * dz);
            }
            float d = Mathf.Sqrt(best);
            return inside ? -d : d;
        }
    }
}
