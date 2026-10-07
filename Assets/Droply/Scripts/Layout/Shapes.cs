using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// Geometry builders. Every shape is generated directly in world space with its own random stream, so no two crowns, stones or tufts are copies of each other,
    /// and everything is flat shaded (faceted). Nothing here needs a Unity Mesh.
    /// </summary>
    public static class Shapes
    {
        static readonly Dictionary<int, Vector3[]> icoVertices = new Dictionary<int, Vector3[]>();
        static readonly Dictionary<int, int[]> icoFaces = new Dictionary<int, int[]>();

        /// <summary>Unit icosphere: subdivision 0 has 20 faces, 1 has 80.</summary>
        public static void Ico(int subdivisions, out Vector3[] vertices, out int[] faces)
        {
            if (icoVertices.TryGetValue(subdivisions, out vertices)) { faces = icoFaces[subdivisions]; return; }
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Midpoint(v, midpoints, a, b), bc = Midpoint(v, midpoints, b, c), ca = Midpoint(v, midpoints, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = next;
            }
            vertices = v.ToArray();
            faces = f.ToArray();
            icoVertices[subdivisions] = vertices;
            icoFaces[subdivisions] = faces;
        }

        static int Midpoint(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int index;
            if (!cache.TryGetValue(key, out index))
            {
                index = vertices.Count;
                vertices.Add(((vertices[a] + vertices[b]) * .5f).normalized);
                cache[key] = index;
            }
            return index;
        }

        /// <summary>
        /// One crown lobe or stone: an icosphere with a random radius per vertex, squashed to an ellipsoid, rotated and moved.
        /// <paramref name="bottomFlatten"/> below 1 flattens the underside (crowns are rounder on top, stones sit flat).
        /// </summary>
        public static void Lobe(MeshBuilder mb, Vector3 center, Vector3 radii, Basis basis, Rng rng, float roughness, int subdivisions, float bottomFlatten)
        {
            Vector3[] unit; int[] faces;
            Ico(subdivisions, out unit, out faces);
            var p = new Vector3[unit.Length];
            for (int i = 0; i < unit.Length; i++)
            {
                Vector3 v = unit[i] * (1f + roughness * rng.Signed());
                if (v.y < 0f) v.y *= bottomFlatten;
                p[i] = center + basis.Apply(new Vector3(v.x * radii.x, v.y * radii.y, v.z * radii.z));
            }
            for (int i = 0; i < faces.Length; i += 3) mb.TriAway(p[faces[i]], p[faces[i + 1]], p[faces[i + 2]], center);
        }

        /// <summary>
        /// Like <see cref="Lobe"/>, but faces that point down (normal.y below <paramref name="splitY"/>) go to <paramref name="lower"/>: faceted two-tone volume
        /// for flat-coloured (unlit) shapes such as clouds.
        /// </summary>
        public static void LobeSplit(MeshBuilder upper, MeshBuilder lower, float splitY, Vector3 center, Vector3 radii, Basis basis, Rng rng, float roughness, int subdivisions, float bottomFlatten)
        {
            Vector3[] unit; int[] faces;
            Ico(subdivisions, out unit, out faces);
            var p = new Vector3[unit.Length];
            for (int i = 0; i < unit.Length; i++)
            {
                Vector3 v = unit[i] * (1f + roughness * rng.Signed());
                if (v.y < 0f) v.y *= bottomFlatten;
                p[i] = center + basis.Apply(new Vector3(v.x * radii.x, v.y * radii.y, v.z * radii.z));
            }
            for (int i = 0; i < faces.Length; i += 3)
            {
                Vector3 a = p[faces[i]], b = p[faces[i + 1]], c = p[faces[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) n = -n;
                (n.normalized.y < splitY ? lower : upper).TriAway(a, b, c, center);
            }
        }

        /// <summary>A bent, tapering tube along <paramref name="pts"/> (trunks and limbs): flat shaded sides, slightly uneven radius, optional pointed end.</summary>
        public static void Tube(MeshBuilder mb, Vector3[] pts, float[] radii, int sides, Rng rng, float roughness, bool tip)
        {
            int n = pts.Length;
            var rings = new Vector3[n][];
            Vector3 side = Vector3.zero, lastTangent = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                lastTangent = t;
                side = i == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) < .9f ? Vector3.up : Vector3.right).normalized : (side - t * Vector3.Dot(side, t)).normalized;
                Vector3 other = Vector3.Cross(t, side);
                rings[i] = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    rings[i][j] = pts[i] + (side * Mathf.Cos(a) + other * Mathf.Sin(a)) * (radii[i] * (1f + roughness * rng.Signed()));
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 axis = (pts[i] + pts[i + 1]) * .5f;
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    mb.Quad(rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j], axis);
                }
            }
            if (tip)
            {
                Vector3 apex = pts[n - 1] + lastTangent * (radii[n - 1] * 1.4f);
                for (int j = 0; j < sides; j++) mb.TriAway(rings[n - 1][j], rings[n - 1][(j + 1) % sides], apex, pts[n - 1]);
            }
        }

        /// <summary>
        /// One tier of a spruce: a ragged skirt (rim points with uneven radius and angle, some hanging lower), a raised inner ring and a tip, plus an underside,
        /// because from the ground the lower tiers are seen from below. Not a smooth cone: 4 triangles per side.
        /// </summary>
        public static void Tier(MeshBuilder mb, Vector3 center, float radius, float height, int sides, Rng rng, float droop)
        {
            var rim = new Vector3[sides];
            var inner = new Vector3[sides];
            float offset = rng.Value() * Mathf.PI * 2f;
            for (int i = 0; i < sides; i++)
            {
                float a = offset + (i + .5f * rng.Signed() * .55f) * Mathf.PI * 2f / sides;
                float r = radius * (.78f + .4f * rng.Value());
                rim[i] = center + new Vector3(Mathf.Cos(a) * r, -droop * rng.Value(), Mathf.Sin(a) * r);
                float b = offset + (i + .5f) * Mathf.PI * 2f / sides;
                inner[i] = center + new Vector3(Mathf.Cos(b) * radius * .46f, height * .42f, Mathf.Sin(b) * radius * .46f);
            }
            Vector3 apex = center + new Vector3(rng.Signed() * radius * .06f, height, rng.Signed() * radius * .06f);
            Vector3 under = center + new Vector3(0f, height * .08f, 0f);
            Vector3 above = center + Vector3.up * 200f, below = center - Vector3.up * 200f;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.TriToward(rim[i], inner[i], rim[j], above);
                mb.TriToward(rim[j], inner[i], inner[j], above);
                mb.TriToward(inner[i], apex, inner[j], above);
                mb.TriToward(rim[i], rim[j], under, below);
            }
        }

        /// <summary>A grass blade: a bent ribbon of three triangles, leaning along <paramref name="yaw"/>. Material is double sided.</summary>
        public static void Blade(MeshBuilder mb, Vector3 root, float yaw, float height, float width, float lean)
        {
            Vector3 dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (width * .5f);
            Vector3 mid = root + dir * (lean * .35f) + Vector3.up * (height * .55f);
            Vector3 tip = root + dir * lean + Vector3.up * height;
            mb.Tri(root - side, root + side, mid - side * .72f);
            mb.Tri(root + side, mid + side * .72f, mid - side * .72f);
            mb.Tri(mid - side * .72f, mid + side * .72f, tip);
        }

        /// <summary>
        /// A wildflower: two crossed stem triangles, five kite-shaped petals in a shallow cup, a small centre. About 20 triangles instead of the roughly 260 of the old flower.
        /// </summary>
        public static void Flower(MeshBuilder stems, MeshBuilder petals, MeshBuilder centers, Vector3 root, float scale, Rng rng)
        {
            float h = .42f * scale * rng.Range(.85f, 1.2f);
            Vector3 headCenter = root + new Vector3(rng.Signed() * .04f, h, rng.Signed() * .04f);
            for (int k = 0; k < 2; k++)
            {
                float a = k * Mathf.PI * .5f + rng.Value() * .6f;
                Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.03f * scale);
                stems.Tri(root - side, root + side, headCenter);
            }
            float spin = rng.Value() * Mathf.PI * 2f;
            for (int k = 0; k < 5; k++)
            {
                float a = spin + k * Mathf.PI * 2f / 5f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), around = new Vector3(-outward.z, 0f, outward.x);
                Vector3 inner = headCenter + outward * (.05f * scale);
                Vector3 left = headCenter + outward * (.14f * scale) - around * (.1f * scale) + Vector3.up * (.02f * scale);
                Vector3 right = headCenter + outward * (.14f * scale) + around * (.1f * scale) + Vector3.up * (.02f * scale);
                Vector3 tip = headCenter + outward * (.25f * scale) + Vector3.up * (.05f * scale);
                petals.Tri(inner, left, tip);
                petals.Tri(inner, tip, right);
            }
            float c = .065f * scale;
            Vector3 top = headCenter + Vector3.up * c;
            for (int k = 0; k < 4; k++)
            {
                float a0 = k * Mathf.PI * .5f, a1 = (k + 1) * Mathf.PI * .5f;
                centers.TriAway(headCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * c, headCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * c, top, headCenter);
            }
            Vector3 leafDir = new Vector3(Mathf.Cos(spin + 1f), 0f, Mathf.Sin(spin + 1f));
            stems.Tri(root + leafDir * (.02f * scale), root + new Vector3(-leafDir.z, 0f, leafDir.x) * (.03f * scale) + Vector3.up * (.05f * scale), root + leafDir * (.2f * scale) + Vector3.up * (.1f * scale));
        }
    }
}
