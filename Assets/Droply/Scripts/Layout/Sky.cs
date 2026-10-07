using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The sky as a vertex-coloured dome around the viewer, with a warm glow and a small disc where the sun is: blue at the zenith, the same horizon colour that the
    /// distant layers and the ground fade into (so there is no seam at the horizon), brighter and warmer towards the sun. Drawn first, without writing depth.
    /// It replaces the procedural skybox in the baked pipeline, which costs a pixel shader on the whole sky and cannot be matched to the haze.
    /// </summary>
    public static class Sky
    {
        public const float Radius = 325f;

        static readonly float[] Elevations = { -12f, 0f, 3f, 7f, 12f, 19f, 28f, 40f, 55f, 72f, 90f };
        const int Slices = 56;

        /// <summary>Linear colour of the sky in direction <paramref name="dir"/> (unit vector).</summary>
        public static Vector3 ColorAt(Vector3 dir, Vector3 towardSun)
        {
            float e = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * 180f / Mathf.PI;
            Vector3 horizon = Look.Linear(Palette.HorizonColor), zenith = Look.Linear(Palette.ZenithColor);
            Vector3 mid = Look.Mix(horizon, zenith, .55f);
            Vector3 c = e <= 0f ? horizon : e < 24f ? Look.Mix(horizon, mid, Look.Smooth(0f, 24f, e)) : Look.Mix(mid, zenith, Look.Smooth(24f, 88f, e));
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, towardSun), -1f, 1f)) * 180f / Mathf.PI;
            float glow = Mathf.Exp(-(angle / 9f) * (angle / 9f)) * .5f + Mathf.Exp(-(angle / 36f) * (angle / 36f)) * .17f;
            return Look.Mix(c, Look.Linear(255f, 228f, 176f), Mathf.Clamp(glow, 0f, .85f));
        }

        /// <summary>Adds the dome and the sun disc as layers with ready colours (inside faces, so they are seen from the viewer).</summary>
        public static void Build(List<MeshLayer> layers, Vector3 towardSun)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            int rings = Elevations.Length;
            for (int r = 0; r < rings; r++)
            {
                float e = Elevations[r] * Mathf.PI / 180f;
                for (int s = 0; s < Slices; s++)
                {
                    float a = s * Mathf.PI * 2f / Slices;
                    Vector3 dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
                    vertices.Add(dir * Radius);
                    colors.Add(Look.Encode(ColorAt(dir, towardSun)));
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < Slices; s++)
                {
                    int a = r * Slices + s, b = r * Slices + (s + 1) % Slices, c = (r + 1) * Slices + s, d = (r + 1) * Slices + (s + 1) % Slices;
                    AddFacingIn(vertices, triangles, a, b, d);
                    AddFacingIn(vertices, triangles, a, d, c);
                }
            layers.Add(new MeshLayer { Name = "Sky", Material = Mat.Sky, Mesh = Make(vertices, colors, triangles) });

            // the sun: a small disc on the dome, brighter than the glow around it
            var sv = new List<Vector3>();
            var sc = new List<Color32>();
            var st = new List<int>();
            Vector3 right = Vector3.Cross(Vector3.up, towardSun).normalized, up = Vector3.Cross(towardSun, right);
            float radius = Radius * Mathf.Tan(2.1f * Mathf.PI / 180f);
            Color32 core = Look.Encode(Look.Linear(Palette.SunDiscColor));
            sv.Add(towardSun * (Radius - 3f)); sc.Add(core);
            const int Sides = 18;
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                sv.Add(towardSun * (Radius - 3f) + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                sc.Add(core);
            }
            for (int i = 0; i < Sides; i++) AddFacingIn(sv, st, 0, 1 + i, 1 + (i + 1) % Sides);
            layers.Add(new MeshLayer { Name = "Sun", Material = Mat.SunDisc, Mesh = Make(sv, sc, st) });
        }

        static void AddFacingIn(List<Vector3> v, List<int> t, int a, int b, int c)
        {
            Vector3 centroid = (v[a] + v[b] + v[c]) / 3f;
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), -centroid) >= 0f) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
        }

        static MeshData Make(List<Vector3> vertices, List<Color32> colors, List<int> triangles)
        {
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = (-vertices[i]).normalized;
            return new MeshData
            {
                Vertices = vertices.ToArray(), Normals = normals, Triangles = triangles.ToArray(), Colors = colors.ToArray(),
                Tags = new float[vertices.Count], Jitters = new float[vertices.Count], Owners = new int[vertices.Count]
            };
        }
    }
}
