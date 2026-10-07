using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// Geometry builders. Every shape is generated directly in world space with its own random stream, so no two crowns, stones or tufts are copies of each other,
    /// and everything is flat shaded (faceted). Each vertex gets a tag (0 at the bottom, root or centre, 1 at the top, tip or rim) that the look turns into gradients,
    /// and each closed shape a small brightness jitter. Nothing here needs a Unity Mesh.
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
        /// <paramref name="bottomFlatten"/> below 1 flattens the underside (crowns are rounder on top, stones sit flat). Tag: 0 at the bottom of the lobe, 1 at the top.
        /// </summary>
        public static void Lobe(MeshBuilder mb, Vector3 center, Vector3 radii, Basis basis, Rng rng, float roughness, int subdivisions, float bottomFlatten)
        {
            Vector3[] unit; int[] faces;
            Ico(subdivisions, out unit, out faces);
            var p = new Vector3[unit.Length];
            var tag = new float[unit.Length];
            for (int i = 0; i < unit.Length; i++)
            {
                Vector3 v = unit[i] * (1f + roughness * rng.Signed());
                tag[i] = Mathf.Clamp01(unit[i].y * .5f + .5f);
                if (v.y < 0f) v.y *= bottomFlatten;
                p[i] = center + basis.Apply(new Vector3(v.x * radii.x, v.y * radii.y, v.z * radii.z));
            }
            mb.Jitter = rng.Signed() * .06f;
            for (int i = 0; i < faces.Length; i += 3)
                mb.TriAway(p[faces[i]], p[faces[i + 1]], p[faces[i + 2]], center, new Vector3(tag[faces[i]], tag[faces[i + 1]], tag[faces[i + 2]]));
        }

        /// <summary>
        /// Like <see cref="Lobe"/>, but faces that point down (normal.y below <paramref name="splitY"/>) go to <paramref name="lower"/>: faceted two-tone volume
        /// for flat-coloured shapes such as clouds.
        /// </summary>
        public static void LobeSplit(MeshBuilder upper, MeshBuilder lower, float splitY, Vector3 center, Vector3 radii, Basis basis, Rng rng, float roughness, int subdivisions, float bottomFlatten)
        {
            Vector3[] unit; int[] faces;
            Ico(subdivisions, out unit, out faces);
            var p = new Vector3[unit.Length];
            var tag = new float[unit.Length];
            for (int i = 0; i < unit.Length; i++)
            {
                Vector3 v = unit[i] * (1f + roughness * rng.Signed());
                tag[i] = Mathf.Clamp01(unit[i].y * .5f + .5f);
                if (v.y < 0f) v.y *= bottomFlatten;
                p[i] = center + basis.Apply(new Vector3(v.x * radii.x, v.y * radii.y, v.z * radii.z));
            }
            float jitter = rng.Signed() * .03f;
            upper.Jitter = jitter; lower.Jitter = jitter;
            for (int i = 0; i < faces.Length; i += 3)
            {
                int ia = faces[i], ib = faces[i + 1], ic = faces[i + 2];
                Vector3 a = p[ia], b = p[ib], c = p[ic];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) n = -n;
                (n.normalized.y < splitY ? lower : upper).TriAway(a, b, c, center, new Vector3(tag[ia], tag[ib], tag[ic]));
            }
        }

        /// <summary>A bent, tapering tube along <paramref name="pts"/> (trunks and limbs): flat shaded sides, slightly uneven radius, optional pointed end. Tag: 0 at the start, 1 at the end.</summary>
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
            mb.Jitter = rng.Signed() * .04f;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 axis = (pts[i] + pts[i + 1]) * .5f;
                float t0 = (float)i / (n - 1), t1 = (float)(i + 1) / (n - 1);
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    mb.Quad(rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j], axis, t0, t0, t1, t1);
                }
            }
            if (tip)
            {
                Vector3 apex = pts[n - 1] + lastTangent * (radii[n - 1] * 1.4f);
                for (int j = 0; j < sides; j++) mb.TriAway(rings[n - 1][j], rings[n - 1][(j + 1) % sides], apex, pts[n - 1], new Vector3(1f, 1f, 1f));
            }
        }

        /// <summary>
        /// One tier of a spruce: a ragged skirt (rim points with uneven radius and angle, some hanging lower), a raised inner ring and a tip, plus an underside,
        /// because from the ground the lower tiers are seen from below. Not a smooth cone: 4 triangles per side. Tag: 0 at the rim, 1 at the tip.
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
            mb.Jitter = rng.Signed() * .05f;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.TriToward(rim[i], inner[i], rim[j], above, new Vector3(0f, .55f, 0f));
                mb.TriToward(rim[j], inner[i], inner[j], above, new Vector3(0f, .55f, .55f));
                mb.TriToward(inner[i], apex, inner[j], above, new Vector3(.55f, 1f, .55f));
                mb.TriToward(rim[i], rim[j], under, below, new Vector3(0f, 0f, 0f));
            }
        }

        /// <summary>A grass blade: a bent ribbon of three triangles, leaning along <paramref name="yaw"/>. Tag: 0 at the root, 1 at the tip. Material is double sided.</summary>
        public static void Blade(MeshBuilder mb, Vector3 root, float yaw, float height, float width, float lean)
        {
            Vector3 dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (width * .5f);
            Vector3 mid = root + dir * (lean * .35f) + Vector3.up * (height * .55f);
            Vector3 tip = root + dir * lean + Vector3.up * height;
            mb.Tri(root - side, root + side, mid - side * .72f, new Vector3(0f, 0f, .55f));
            mb.Tri(root + side, mid + side * .72f, mid - side * .72f, new Vector3(0f, .55f, .55f));
            mb.Tri(mid - side * .72f, mid + side * .72f, tip, new Vector3(.55f, .55f, 1f));
        }

        /// <summary>A grass blade as a single sharp triangle, the spiky look of the concept image. Tag: 0 at the root, 1 at the tip. Material is double sided.</summary>
        public static void Spike(MeshBuilder mb, Vector3 root, float yaw, float height, float width, float lean)
        {
            Vector3 dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (width * .5f);
            mb.Tri(root - side, root + side, root + dir * lean + Vector3.up * height, new Vector3(0f, 0f, 1f));
        }

        /// <summary>
        /// A tuft of spikes fanned out from <paramref name="center"/>: tall in the heart, shorter and leaning outwards at the rim, dark at the root, light at the tip
        /// (the gradient comes from the tags), the tallest blades a shade lighter. Every blade sits on the rendered ground.
        /// </summary>
        public static void Tuft(MeshBuilder dark, MeshBuilder mid, MeshBuilder light, TerrainModel terrain, Vector3 center, float radius, int blades, float minHeight, float maxHeight, Rng rng)
        {
            for (int i = 0; i < blades; i++)
            {
                float around = i * 2.39996f + rng.Value() * 1.2f;
                float reach = radius * Mathf.Sqrt((i + .5f) / blades) * rng.Range(.7f, 1.05f);
                float x = center.x + Mathf.Cos(around) * reach, z = center.z + Mathf.Sin(around) * reach;
                Vector3 root = new Vector3(x, terrain.SurfaceY(x, z) - .02f, z);
                float heart = 1f - reach / Mathf.Max(radius, .01f);                       // 1 in the heart, 0 at the rim
                float height = Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(.2f + .6f * heart + .3f * rng.Value()));
                float pick = rng.Value() - .2f * heart;
                MeshBuilder mb = pick < .22f ? dark : pick < .72f ? mid : light;
                mb.Jitter = rng.Signed() * .08f;
                float yaw = Mathf.PI * .5f - around + rng.Signed() * .5f;                  // leans outwards, not all the same way
                Spike(mb, root, yaw, height, .05f + .07f * height, height * rng.Range(.1f, .45f) * (1.1f - heart * .6f));
            }
        }

        public enum FlowerKind { Daisy, Buttercup, Lavender }

        /// <summary>
        /// A wildflower with a head that reads from 20 m: a daisy (white petals around a golden eye), a buttercup (five round petals in a cup) or a lavender spike
        /// (a column of small buds). Stem and leaves are separate triangles. Tags: stems 0 at the root to 1 at the head, petals and buds 0 at the heart to 1 at the tip.
        /// </summary>
        public static void Flower(MeshBuilder stems, MeshBuilder petals, MeshBuilder centers, FlowerKind kind, Vector3 root, float scale, Rng rng)
        {
            float h = (kind == FlowerKind.Lavender ? .5f : .42f) * scale * rng.Range(.85f, 1.2f);
            Vector3 head = root + new Vector3(rng.Signed() * .05f, h, rng.Signed() * .05f);
            stems.Jitter = rng.Signed() * .06f;
            petals.Jitter = rng.Signed() * .08f;
            centers.Jitter = 0f;
            for (int k = 0; k < 2; k++)
            {
                float a = k * Mathf.PI * .5f + rng.Value() * .6f;
                Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.025f * scale);
                stems.Tri(root - side, root + side, head, new Vector3(0f, 0f, 1f));
            }
            float spin = rng.Value() * Mathf.PI * 2f;
            for (int l = 0; l < 2; l++)   // two leaves at the foot
            {
                Vector3 leafDir = new Vector3(Mathf.Cos(spin + l * 2.6f), 0f, Mathf.Sin(spin + l * 2.6f));
                Vector3 across = new Vector3(-leafDir.z, 0f, leafDir.x);
                stems.Tri(root + across * (.025f * scale), root - across * (.025f * scale) + Vector3.up * (.04f * scale), root + leafDir * (.24f * scale) + Vector3.up * (.12f * scale), new Vector3(0f, .3f, .9f));
            }
            switch (kind)
            {
                case FlowerKind.Daisy:
                {
                    const int Rays = 9;
                    float radius = .15f * scale;
                    for (int k = 0; k < Rays; k++)
                    {
                        float a = spin + k * Mathf.PI * 2f / Rays + rng.Signed() * .1f;
                        Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), around = new Vector3(-outward.z, 0f, outward.x);
                        Vector3 inner = head + outward * (.035f * scale);
                        Vector3 tip = head + outward * (radius * rng.Range(.9f, 1.1f)) + Vector3.up * (.03f * scale);
                        petals.Tri(inner - around * (.032f * scale), inner + around * (.032f * scale), tip, new Vector3(0f, 0f, 1f));
                    }
                    EyeOf(centers, head, .05f * scale, 6);
                    break;
                }
                case FlowerKind.Buttercup:
                {
                    for (int k = 0; k < 5; k++)
                    {
                        float a = spin + k * Mathf.PI * 2f / 5f;
                        Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), around = new Vector3(-outward.z, 0f, outward.x);
                        Vector3 inner = head + outward * (.04f * scale);
                        Vector3 left = head + outward * (.15f * scale) - around * (.11f * scale) + Vector3.up * (.03f * scale);
                        Vector3 right = head + outward * (.15f * scale) + around * (.11f * scale) + Vector3.up * (.03f * scale);
                        Vector3 tip = head + outward * (.26f * scale) + Vector3.up * (.07f * scale);
                        petals.Tri(inner, left, tip, new Vector3(0f, .6f, 1f));
                        petals.Tri(inner, tip, right, new Vector3(0f, 1f, .6f));
                    }
                    EyeOf(centers, head, .06f * scale, 5);
                    break;
                }
                default:
                {
                    // lavender: five small diamond buds stacked on the upper part of the stem, growing smaller towards the tip
                    const int Buds = 5;
                    for (int k = 0; k < Buds; k++)
                    {
                        float t = (float)k / (Buds - 1);
                        Vector3 c = head + Vector3.up * (-.2f * scale + t * .36f * scale) + new Vector3(Mathf.Cos(spin + k * 2f), 0f, Mathf.Sin(spin + k * 2f)) * (.012f * scale);
                        float r = (.06f - .028f * t) * scale, ry = (.085f - .03f * t) * scale;
                        Vector3 top = c + Vector3.up * ry, bottom = c - Vector3.up * ry * .8f;
                        for (int m = 0; m < 3; m++)
                        {
                            float a0 = spin + k * .7f + m * Mathf.PI * 2f / 3f, a1 = a0 + Mathf.PI * 2f / 3f;
                            Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                            petals.TriAway(p0, p1, top, c, new Vector3(.5f, .5f, 0f));      // bright at the top of a bud, darker below
                            petals.TriAway(p1, p0, bottom, c, new Vector3(.5f, .5f, 1f));
                        }
                    }
                    break;
                }
            }
        }

        static void EyeOf(MeshBuilder centers, Vector3 head, float radius, int sides)
        {
            Vector3 top = head + Vector3.up * (radius * .8f);
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                centers.TriAway(head + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius, head + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius, top, head, new Vector3(0f, 0f, 1f));
            }
        }
    }
}
