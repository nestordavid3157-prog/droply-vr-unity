using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>A soft sphere that blocks light: a crown lobe, a bush, a stone. Registered while the landscape is built.</summary>
    public struct Occluder
    {
        public Vector3 Center;
        public float Radius;
        /// <summary>How much light it blocks at its core (leaves let some through): 0.7 to 1.</summary>
        public float Density;
        public int Owner;
    }

    /// <summary>
    /// Finds occluders near a point: by position (ambient occlusion) and by where their shadow falls on the ground plane (sun shadows).
    /// Two flat grids of 8 m cells over 880 m, stored as sorted arrays (no dictionary lookups: the bake asks millions of times).
    /// </summary>
    public sealed class OccluderIndex
    {
        const float Cell = 8f, Origin = -440f;
        const int Size = 110;

        readonly List<Occluder> items;
        readonly Vector3 towardSun;
        readonly int[] positionStart, positionList, shadowStart, shadowList;

        public OccluderIndex(List<Occluder> occluders, Vector3 towardSun)
        {
            items = occluders;
            this.towardSun = towardSun;
            var positionCell = new int[items.Count];
            var shadowCell = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                positionCell[i] = CellIndex(items[i].Center.x, items[i].Center.z);
                Vector3 s = Project(items[i].Center);
                shadowCell[i] = CellIndex(s.x, s.z);
            }
            Sort(positionCell, out positionStart, out positionList);
            Sort(shadowCell, out shadowStart, out shadowList);
        }

        public Occluder this[int index] { get { return items[index]; } }

        public int Count { get { return items.Count; } }

        /// <summary>The point on the ground plane (y = 0) that the sun ray through <paramref name="p"/> hits.</summary>
        public Vector3 Project(Vector3 p) { return p - towardSun * (p.y / towardSun.y); }

        static int Axis(float v) { return Mathf.Clamp(Mathf.FloorToInt((v - Origin) / Cell), 0, Size - 1); }

        static int CellIndex(float x, float z) { return Axis(z) * Size + Axis(x); }

        static void Sort(int[] cellOfItem, out int[] start, out int[] list)
        {
            start = new int[Size * Size + 1];
            for (int i = 0; i < cellOfItem.Length; i++) start[cellOfItem[i] + 1]++;
            for (int c = 0; c < Size * Size; c++) start[c + 1] += start[c];
            list = new int[cellOfItem.Length];
            var fill = new int[Size * Size];
            for (int i = 0; i < cellOfItem.Length; i++) list[start[cellOfItem[i]] + fill[cellOfItem[i]]++] = i;
        }

        /// <summary>Writes the indices of all occluders in the 5 x 5 cells around (x, z) into <paramref name="result"/> and returns how many.</summary>
        static int Collect(int[] start, int[] list, float x, float z, int[] result)
        {
            int cx = Axis(x), cz = Axis(z), n = 0;
            for (int dz = -2; dz <= 2; dz++)
            {
                int zz = cz + dz;
                if (zz < 0 || zz >= Size) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int xx = cx + dx;
                    if (xx < 0 || xx >= Size) continue;
                    int cell = zz * Size + xx;
                    for (int k = start[cell]; k < start[cell + 1]; k++) result[n++] = list[k];
                }
            }
            return n;
        }

        public int Near(Vector3 p, int[] result) { return Collect(positionStart, positionList, p.x, p.z, result); }

        public int Shadowing(Vector3 p, int[] result)
        {
            Vector3 s = Project(p);
            return Collect(shadowStart, shadowList, s.x, s.z, result);
        }
    }

    /// <summary>
    /// Bakes the light into vertex colours, once, when the landscape is built: warm sun from the left with soft shadows from the occluders,
    /// a sky/ground trilight ambient with sphere ambient occlusion (dark under crowns and at the foot of trunks and stones), a little glow through leaves,
    /// atmospheric haze towards the horizon colour, a soft clip so bright faces do not burn out. A static look: no lights, no shadow maps on the headset.
    /// </summary>
    public sealed class Lighting
    {
        const float Pi = Mathf.PI;

        static readonly Vector3 Eye = new Vector3(0f, 1.65f, 0f);

        readonly Vector3 towardSun, sun, sky, equator, ground, horizon;
        readonly TerrainModel terrain;
        readonly OccluderIndex index;
        readonly int[] scratch;

        public Lighting(TerrainModel terrain, List<Occluder> occluders)
        {
            this.terrain = terrain;
            float pitch = Palette.SunPitch * Mathf.PI / 180f, yaw = Palette.SunYaw * Mathf.PI / 180f;
            Vector3 travel = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            towardSun = -travel;
            sun = Look.Linear(Palette.SunColor) * Palette.SunIntensity;
            sky = Look.Linear(Palette.AmbientSky);
            equator = Look.Linear(Palette.AmbientEquator);
            ground = Look.Linear(Palette.AmbientGround);
            horizon = Look.Linear(Palette.HorizonColor);
            index = new OccluderIndex(occluders, towardSun);
            scratch = new int[Mathf.Max(1, index.Count)];
        }

        public Vector3 TowardSun { get { return towardSun; } }

        Vector3 Ambient(Vector3 n)
        {
            return n.y >= 0f ? Look.Mix(equator, sky, n.y) : Look.Mix(equator, ground, -n.y);
        }

        /// <summary>Share of the sun that reaches the point (soft edges: the shadow of a crown is wider and softer the farther it is from the crown).</summary>
        float Sunlight(Vector3 p, int owner)
        {
            int count = index.Shadowing(p, scratch);
            float visible = 1f;
            for (int i = 0; i < count; i++)
            {
                Occluder o = index[scratch[i]];
                if (o.Owner == owner && owner != 0) continue;
                Vector3 v = o.Center - p;
                float along = Vector3.Dot(v, towardSun);
                if (along < .2f) continue;
                float distance = Vector3.Cross(v, towardSun).magnitude;
                float width = o.Radius * .22f + Mathf.Min(along, 40f) * .045f;   // the blur grows with the distance to the occluder, but a cloud far above still casts a clear shape
                float cover = 1f - Look.Smooth(o.Radius - width, o.Radius + width, distance);
                visible *= 1f - o.Density * cover;
            }
            return Mathf.Max(visible, .12f);
        }

        /// <summary>Sphere ambient occlusion from the occluders around the point; 1 = open sky, lower = enclosed.</summary>
        float Openness(Vector3 p, Vector3 n)
        {
            int count = index.Near(p, scratch);
            float occlusion = 0f;
            for (int i = 0; i < count; i++)
            {
                Occluder o = index[scratch[i]];
                Vector3 v = o.Center - p;
                float d = v.magnitude;
                if (d < 1e-3f) continue;
                float cosine = Vector3.Dot(n, v) / d;
                if (cosine <= 0f) continue;
                float reach = 1f - Look.Smooth(o.Radius * 3f, o.Radius * 6f, d);
                if (reach <= 0f) continue;
                float dd = Mathf.Max(d, o.Radius);
                occlusion += cosine * (o.Radius * o.Radius) / (dd * dd) * reach * o.Density;
            }
            return Mathf.Clamp(1f - occlusion * .85f, .22f, 1f);
        }

        static float SoftClip(float x)
        {
            const float knee = .78f;
            return x <= knee ? x : knee + (1f - knee) * (1f - Mathf.Exp(-(x - knee) / (1f - knee)));
        }

        /// <summary>
        /// Gentle ground reads as flat under a low sun. For the light only (the geometry stays as it is) the slope is exaggerated, so every swell and swale
        /// shows as a sunlit warm side and a shaded cool side, like the rolling meadow of the concept image.
        /// </summary>
        static Vector3 Stylize(Vector3 n, float slope)
        {
            return new Vector3(n.x * slope, n.y, n.z * slope).normalized;
        }

        const float SlopeExaggeration = 2.6f;

        /// <summary>Colour of one surface point with everything baked in (sRGB bytes).</summary>
        public Color32 Shade(Mat mat, Vector3 p, Vector3 n, float tag, float jitter, int owner, float sunlight, float openness)
        {
            float height = Mathf.Max(0f, p.y - terrain.SurfaceY(p.x, p.z));
            Vector3 albedo = Look.Albedo(mat, p, tag, jitter, height);
            Vector3 color;
            float diffuse = Mathf.Max(0f, Vector3.Dot(n, towardSun));
            if (Palette.Atmospheric(mat))
            {
                // distant layers and clouds: mostly flat haze colour, only a hint of direction
                Vector3 full = Ambient(n) * .75f + sun * diffuse * .55f;
                color = Look.Scale(albedo, Look.Mix(Vector3.one, full, .35f));
            }
            else
            {
                float contact = Palette.Upright(mat) ? Mathf.Lerp(Palette.Kind(mat) == SurfaceKind.Grass ? .8f : .62f, 1f, Look.Smooth(0f, .8f, height)) : 1f;   // dark where a trunk, stone or tuft meets the ground
                Vector3 ambient = Ambient(n) * (openness * contact);
                Vector3 direct = sun * (diffuse * sunlight);
                float glow = Palette.Translucency(mat);
                Vector3 through = glow > 0f ? sun * (glow * Mathf.Max(0f, -Vector3.Dot(n, towardSun))) * Mathf.Lerp(.55f, 1f, sunlight) : Vector3.zero;
                color = new Vector3(albedo.x * (ambient.x + direct.x + through.x), albedo.y * (ambient.y + direct.y + through.y), albedo.z * (ambient.z + direct.z + through.z));
            }
            float d = (p - Eye).magnitude;
            float haze = Palette.Kind(mat) == SurfaceKind.Cloud ? 0f : .6f * Mathf.Pow(Mathf.Clamp01((d - 40f) / 300f), 1.3f);   // clouds are in the sky, not in the ground haze
            color = Look.Mix(color, horizon, haze);
            return Look.Encode(new Vector3(SoftClip(color.x), SoftClip(color.y), SoftClip(color.z)));
        }

        /// <summary>
        /// Fills <see cref="MeshData.Colors"/> of a layer. Flat shaded triangles are lit once at their centre (all three vertices share the light, the albedo
        /// still varies with the vertex tag); smooth meshes (ground, path) are lit per vertex. Layers that already carry colours (the sky) are left alone.
        /// </summary>
        public void Bake(MeshLayer layer)
        {
            MeshData m = layer.Mesh;
            if (m.Colors != null) return;
            m.Colors = new Color32[m.Vertices.Length];
            bool atmospheric = Palette.Atmospheric(layer.Material);
            SurfaceKind kind = Palette.Kind(layer.Material);
            bool groundSurface = kind == SurfaceKind.Ground || kind == SurfaceKind.Path;
            bool twoSided = Palette.DoubleSided(layer.Material);
            for (int i = 0; i < m.Triangles.Length; i += 3)
            {
                int a = m.Triangles[i], b = m.Triangles[i + 1], c = m.Triangles[i + 2];
                bool flat = (m.Normals[a] - m.Normals[b]).sqrMagnitude < 1e-8f && (m.Normals[a] - m.Normals[c]).sqrMagnitude < 1e-8f;
                if (flat)
                {
                    Vector3 centre = (m.Vertices[a] + m.Vertices[b] + m.Vertices[c]) / 3f;
                    Vector3 n = m.Normals[a];
                    // A thin two-sided surface (blade, petal, stem) is lit on its upper side whichever way its winding points; the sun reaches the underside
                    // through the leaf (translucency in Shade). Without this, half of all petals and blades would be lit like the underside of a stone.
                    if (twoSided && n.y < 0f) n = -n;
                    float sunlight = 1f, openness = 1f;
                    if (!atmospheric)
                    {
                        if (Vector3.Dot(n, towardSun) > 0f) sunlight = Sunlight(centre + n * .05f, m.Owners[a]);
                        openness = Openness(centre + n * .05f, n);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        int v = k == 0 ? a : k == 1 ? b : c;
                        m.Colors[v] = Shade(layer.Material, m.Vertices[v], n, m.Tags[v], m.Jitters[v], m.Owners[v], sunlight, openness);
                    }
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int v = k == 0 ? a : k == 1 ? b : c;
                        if (m.Colors[v].a != 0) continue;
                        Vector3 n = groundSurface ? Stylize(m.Normals[v], SlopeExaggeration) : m.Normals[v];
                        float sunlight = Vector3.Dot(n, towardSun) > 0f ? Sunlight(m.Vertices[v] + n * .05f, m.Owners[v]) : 1f;
                        m.Colors[v] = Shade(layer.Material, m.Vertices[v], n, m.Tags[v], m.Jitters[v], m.Owners[v], sunlight, Openness(m.Vertices[v] + n * .05f, n));
                    }
                }
            }
        }
    }
}
