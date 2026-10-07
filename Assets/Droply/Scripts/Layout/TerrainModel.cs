using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The ground: gently rolling meadow with a few designed shapes (a knoll for the tree group, a shallow swale, a ridge the path climbs).
    /// The standing spot at the origin is level, so the floor-level tracking origin sits on the ground.
    /// The mesh grid is fine (2 m) where the composition is and grows coarser towards the horizon, so one mesh covers a 600 m disc cheaply.
    /// Everything that sits on the ground uses <see cref="SurfaceY"/>, the height of the rendered triangles, not the analytic function.
    /// </summary>
    public sealed class TerrainModel
    {
        const float Extent = 300f;

        public static float Gauss(float x, float z, float cx, float cz, float sx, float sz, float amp)
        {
            float dx = (x - cx) / sx, dz = (z - cz) / sz;
            return amp * Mathf.Exp(-.5f * (dx * dx + dz * dz));
        }

        /// <summary>Analytic ground height in metres (level at the standing spot).</summary>
        public static float Height(float x, float z) { return Raw(x, z) - originOffset; }

        static readonly float originOffset = Raw(0f, 0f);

        static float Raw(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float rolling = 1.2f * Noise.Signed(x * .028f + 11.3f, z * .028f + 4.7f, 7)
                          + .38f * Noise.Signed(x * .071f, z * .071f, 19)
                          + .06f * Noise.Signed(x * .19f, z * .19f, 31);
            float calm = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 1.5f) / 11f));
            float h = rolling * calm;
            h += Gauss(x, z, Plan.GroupCenter.x, Plan.GroupCenter.y, 14f, 12f, 1.45f);              // knoll under the tree group: it stands out against the sky
            h += Gauss(x, z, -17f, 15f, 7f, 6f, 1.15f);                                              // soft mound in the left foreground (the big stone sits on its flank)
            h += Gauss(x, z, 15f, 25f, 9f, 8f, 1.3f);                                              // second mound on the right: the path bends around it
            h += Gauss(x, z, 4f, 62f, 30f, 7f, -.95f);                                              // shallow swale before the rise
            h += Gauss(x, z, 0f, 92f, 70f, 16f, 3.1f * (.75f + .25f * Noise.Signed(x * .02f, 3f, 5))); // the ridge the path climbs
            h += Gauss(x, z, 0f, -75f, 45f, 28f, 2.2f);                                            // soft rise behind the viewer
            return h;
        }

        public readonly float[] Xs, Zs;
        readonly float[] heights;
        readonly Vector3[] normals;
        readonly int nx, nz;

        /// <summary>Dense (1.25 m) between the two limits, then growing steps out to the edge of the 600 m disc.</summary>
        static float[] Axis(float denseMin, float denseMax)
        {
            const float dense = 1.25f;
            var list = new List<float>();
            for (float c = denseMin; c <= denseMax + .001f; c += dense) list.Add(c);
            float step;
            for (float c = list[list.Count - 1]; c < Extent;) { step = 2f + .11f * (c - denseMax); c = Mathf.Min(c + step, Extent); list.Add(c); }
            for (float c = denseMin; c > -Extent;) { step = 2f + .11f * (denseMin - c); c = Mathf.Max(c - step, -Extent); list.Insert(0, c); }
            return list.ToArray();
        }

        public TerrainModel()
        {
            Xs = Axis(-30f, 30f);
            Zs = Axis(-14f, 74f);
            nx = Xs.Length; nz = Zs.Length;
            heights = new float[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    heights[j * nx + i] = Height(Xs[i], Zs[j]);
            normals = new Vector3[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int i0 = Mathf.Max(i - 1, 0), i1 = Mathf.Min(i + 1, nx - 1), j0 = Mathf.Max(j - 1, 0), j1 = Mathf.Min(j + 1, nz - 1);
                    float dhx = (heights[j * nx + i1] - heights[j * nx + i0]) / (Xs[i1] - Xs[i0]);
                    float dhz = (heights[j1 * nx + i] - heights[j0 * nx + i]) / (Zs[j1] - Zs[j0]);
                    normals[j * nx + i] = new Vector3(-dhx, 1f, -dhz).normalized;
                }
        }

        static int Cell(float[] axis, float v)
        {
            int lo = 0, hi = axis.Length - 1;
            if (v <= axis[0]) return 0;
            if (v >= axis[hi]) return hi - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (axis[mid] <= v) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Height of the rendered surface (the triangle under the point).</summary>
        public float SurfaceY(float x, float z)
        {
            int i = Cell(Xs, x), j = Cell(Zs, z);
            float u = Mathf.Clamp01((x - Xs[i]) / (Xs[i + 1] - Xs[i])), v = Mathf.Clamp01((z - Zs[j]) / (Zs[j + 1] - Zs[j]));
            float a = heights[j * nx + i], b = heights[j * nx + i + 1], c = heights[(j + 1) * nx + i], d = heights[(j + 1) * nx + i + 1];
            // The quad is split along the diagonal a-d: triangles (a, d, b) for u >= v and (a, c, d) for v > u.
            return u >= v ? a * (1f - u) + b * (u - v) + d * v : a * (1f - v) + c * (v - u) + d * u;
        }

        public Vector3 SurfaceNormal(float x, float z)
        {
            int i = Cell(Xs, x), j = Cell(Zs, z);
            return normals[j * nx + i];
        }

        /// <summary>The ground as one smooth mesh. Its colour and light are baked into the vertex colours (soft patches, no hard edges), see <see cref="Look.Ground"/>.</summary>
        public MeshData BuildMesh()
        {
            var vertices = new Vector3[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    vertices[j * nx + i] = new Vector3(Xs[i], heights[j * nx + i], Zs[j]);
            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                }
            return new MeshData
            {
                Vertices = vertices, Normals = (Vector3[])normals.Clone(), Triangles = triangles,
                Tags = new float[vertices.Length], Jitters = new float[vertices.Length], Owners = new int[vertices.Length]
            };
        }
    }
}
