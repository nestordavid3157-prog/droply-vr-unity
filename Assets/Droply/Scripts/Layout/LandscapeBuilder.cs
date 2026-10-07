using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    public struct RockRecord
    {
        public Vector3 Position;
        public float Radius;
    }

    /// <summary>Everything the landscape consists of, as plain data: merged mesh layers plus the records the composition checks look at.</summary>
    public sealed class SceneData
    {
        public readonly List<MeshLayer> Layers = new List<MeshLayer>();
        public readonly List<TreeRecord> Trees = new List<TreeRecord>();
        public readonly List<Vector3> GrassClumps = new List<Vector3>();
        public readonly List<Vector3> Flowers = new List<Vector3>();
        public readonly List<Vector3> IslandCenters = new List<Vector3>();
        public readonly List<RockRecord> Rocks = new List<RockRecord>();
        public PathModel Path;
        public TerrainModel Terrain;

        public int TriangleCount
        {
            get { int total = 0; foreach (var layer in Layers) total += layer.Mesh.TriangleCount; return total; }
        }
    }

    /// <summary>
    /// Builds the landscape from <see cref="Plan"/>: terrain, path, tree group, forest clumps, simplified forest, forest line and hills, grass, flowers, stones, clouds.
    /// Pure geometry and deterministic: the player and Tools/CompositionCheck call exactly this.
    /// </summary>
    public static class LandscapeBuilder
    {
        const int Seed = 3157;
        const float Deg = Mathf.PI / 180f;

        public static SceneData Build()
        {
            var data = new SceneData { Terrain = new TerrainModel(), Path = new PathModel() };
            var root = new Rng(Seed);
            var layers = new LayerSet();

            AddTerrain(data);
            AddPath(data);
            AddHeroGroup(data, layers, root.Fork(1));
            foreach (var cluster in Plan.EdgeClusters) AddCluster(data, layers, cluster, TreeTier.Edge, root.Fork(10 + data.Trees.Count));
            foreach (var cluster in Plan.MidClusters) AddCluster(data, layers, cluster, TreeTier.Mid, root.Fork(100 + data.Trees.Count));
            // Each layer peeks over the one in front (angular height grows with distance) and is paler and bluer-green: forest line 1.2-2.3 deg, then 2-3 deg, hills 3-7 deg.
            AddForestLine(data, layers.Get(Mat.TreeLineNear, false), 150f, 1.8f, 4.5f, 8.5f, 3f, 6f, root.Fork(3));
            AddForestLine(data, layers.Get(Mat.TreeLineFar, false), 186f, 3.5f, 5.5f, 10f, 4f, 7.5f, root.Fork(4));
            AddHills(data, layers.Get(Mat.Hill1, false), 206f, 9f, 10f, 7, root.Fork(5));
            AddHills(data, layers.Get(Mat.Hill2, false), 246f, 13f, 14f, 8, root.Fork(6));
            AddHills(data, layers.Get(Mat.Hill3, false), 288f, 19f, 20f, 9, root.Fork(7));
            AddGrass(data, layers, root.Fork(8));
            AddFlowers(data, layers, root.Fork(9));
            AddRocks(data, layers, root.Fork(11));
            AddClouds(layers.Get(Mat.CloudTop, false), layers.Get(Mat.CloudBottom, false), root.Fork(12));

            data.Layers.AddRange(layers.Finish());
            return data;
        }

        // ---- ground and path ------------------------------------------------------------------------------------------------------

        static void AddTerrain(SceneData data)
        {
            foreach (var patch in data.Terrain.BuildPatches())
                if (patch.Value.TriangleCount > 0)
                    data.Layers.Add(new MeshLayer { Name = "Ground " + patch.Key, Material = patch.Key, CastShadows = false, Mesh = patch.Value });
        }

        /// <summary>
        /// The sand path as two ribbons draped on the rendered terrain: the sand itself and a darker, irregular worn edge on both sides.
        /// The edge of the sand wobbles a little (no ruler line), the path tapers in at the start and out at the far end.
        /// </summary>
        static void AddPath(SceneData data)
        {
            var path = data.Path;
            var terrain = data.Terrain;
            int n = Mathf.CeilToInt(path.Length / 1.1f);
            var coreL = new Vector3[n + 1]; var coreR = new Vector3[n + 1];
            var outL = new Vector3[n + 1]; var outR = new Vector3[n + 1];
            var centre = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = path.Length * i / n;
                Vector3 p = path.PointAt(s), d = path.DirectionAt(s);
                Vector3 side = new Vector3(d.z, 0f, -d.x);
                float taper = Mathf.SmoothStep(.3f, 1f, Mathf.Clamp01(s / 6f)) * Mathf.SmoothStep(.4f, 1f, Mathf.Clamp01((path.Length - s) / 16f));
                float hw = path.HalfWidth(s) * taper;
                float wl = hw * (1f + .09f * Noise.Signed(s * .11f, 0f, 61)), wr = hw * (1f + .09f * Noise.Signed(s * .11f, 9f, 62));
                float el = .34f + .16f * Noise.Signed(s * .17f, 5f, 63), er = .34f + .16f * Noise.Signed(s * .17f, 7f, 64);
                coreL[i] = Drape(terrain, p - side * wl, .05f); coreR[i] = Drape(terrain, p + side * wr, .05f);
                outL[i] = Drape(terrain, p - side * (wl + el * taper), .035f); outR[i] = Drape(terrain, p + side * (wr + er * taper), .035f);
                centre[i] = Drape(terrain, p, .05f);
            }
            data.Layers.Add(new MeshLayer { Name = "Sand path", Material = Mat.PathCore, Mesh = Ribbon(terrain, coreL, coreR) });
            var edge = Ribbon(terrain, outL, coreL);
            var edgeRight = Ribbon(terrain, coreR, outR);
            data.Layers.Add(new MeshLayer { Name = "Path edge", Material = Mat.PathEdge, Mesh = Merge(edge, edgeRight) });
        }

        static Vector3 Drape(TerrainModel terrain, Vector3 p, float lift)
        {
            return new Vector3(p.x, terrain.SurfaceY(p.x, p.z) + lift, p.z);
        }

        /// <summary>A strip between two polylines, facing up, with the terrain's smooth normals.</summary>
        static MeshData Ribbon(TerrainModel terrain, Vector3[] a, Vector3[] b)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < a.Length; i++)
            {
                vertices.Add(a[i]); normals.Add(terrain.SurfaceNormal(a[i].x, a[i].z));
                vertices.Add(b[i]); normals.Add(terrain.SurfaceNormal(b[i].x, b[i].z));
            }
            for (int i = 0; i < a.Length - 1; i++)
            {
                int k = i * 2;
                AddUp(vertices, triangles, k, k + 1, k + 2);
                AddUp(vertices, triangles, k + 1, k + 3, k + 2);
            }
            return new MeshData { Vertices = vertices.ToArray(), Normals = normals.ToArray(), Triangles = triangles.ToArray() };
        }

        static void AddUp(List<Vector3> v, List<int> t, int a, int b, int c)
        {
            if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y >= 0f) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
        }

        static MeshData Merge(MeshData a, MeshData b)
        {
            var vertices = new List<Vector3>(a.Vertices); vertices.AddRange(b.Vertices);
            var normals = new List<Vector3>(a.Normals); normals.AddRange(b.Normals);
            var triangles = new List<int>(a.Triangles);
            foreach (int t in b.Triangles) triangles.Add(t + a.Vertices.Length);
            return new MeshData { Vertices = vertices.ToArray(), Normals = normals.ToArray(), Triangles = triangles.ToArray() };
        }

        // ---- trees ----------------------------------------------------------------------------------------------------------------

        static Vector3 OnGround(SceneData data, float x, float z) { return new Vector3(x, data.Terrain.SurfaceY(x, z), z); }

        static void AddHeroGroup(SceneData data, LayerSet layers, Rng rng)
        {
            for (int i = 0; i < Plan.HeroGroup.Length; i++)
            {
                var hero = Plan.HeroGroup[i];
                data.Trees.Add(TreeFactory.Build(layers, hero.Kind, TreeTier.Hero, OnGround(data, hero.Position.x, hero.Position.y), hero.Height, hero.Spread, rng.Fork(i), Mat.HazeTree1));
            }
            for (int i = 0; i < Plan.GroupBushes.Length; i++)
            {
                var bush = Plan.GroupBushes[i];
                data.Trees.Add(TreeFactory.Build(layers, TreeKind.Shrub, TreeTier.Hero, OnGround(data, bush.Position.x, bush.Position.y), bush.Size, bush.Size, rng.Fork(50 + i), Mat.HazeTree1));
            }
        }

        static List<Vector2> Positions(SceneData data)
        {
            var list = new List<Vector2>();
            foreach (var t in data.Trees) if (t.Kind != TreeKind.Shrub) list.Add(new Vector2(t.Position.x, t.Position.z));
            return list;
        }

        static float SpreadFor(TreeKind kind, float height)
        {
            switch (kind)
            {
                case TreeKind.Oak: return height * .42f;
                case TreeKind.Beech: return height * .31f;
                case TreeKind.Spruce: return height * .23f;
                case TreeKind.Blob: return height * .34f;
                default: return height * .22f;
            }
        }

        /// <summary>
        /// A forest clump: trees dropped into an ellipse with rejection (crowns may overlap a little but never stack into a wall, nothing on the path),
        /// tallest in the middle and shorter towards the rim. No grid, no rows.
        /// </summary>
        static void AddCluster(SceneData data, LayerSet layers, Plan.Cluster c, TreeTier tier, Rng rng)
        {
            bool simple = tier != TreeTier.Edge;
            float gap = simple ? .52f : .66f;
            Mat haze = c.Center.y > 118f || c.Center.y < 0f ? Mat.HazeTree2 : Mat.HazeTree1;
            int placed = 0;
            for (int attempt = 0; placed < c.Count && attempt < c.Count * 120; attempt++)
            {
                float a = rng.Value() * Mathf.PI * 2f, r = Mathf.Pow(rng.Value(), .8f);
                float x = c.Center.x + Mathf.Cos(a) * r * c.RadiusX, z = c.Center.y + Mathf.Sin(a) * r * c.RadiusZ;
                float height = Mathf.Lerp(c.MaxHeight, c.MinHeight, r) * rng.Range(.88f, 1.08f);
                float k = rng.Value();
                TreeKind kind = simple ? (k < .7f ? TreeKind.Blob : TreeKind.BlobSpruce) : (k < .32f ? TreeKind.Oak : k < .6f ? TreeKind.Beech : TreeKind.Spruce);
                float spread = SpreadFor(kind, height);
                if (data.Path.EdgeDistance(x, z) < 2f + spread * .8f) continue;
                bool free = true;
                foreach (var other in data.Trees)
                {
                    if (other.Kind == TreeKind.Shrub) continue;
                    float dx = other.Position.x - x, dz = other.Position.z - z;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < gap * (other.Spread + spread)) { free = false; break; }
                }
                if (!free) continue;
                if (LandscapeChecks.RowThrough(Positions(data), new Vector2(x, z)) >= 4) continue;
                data.Trees.Add(TreeFactory.Build(layers, kind, tier, OnGround(data, x, z), height, spread, rng.Fork(attempt), haze));
                placed++;
            }
            if (tier != TreeTier.Edge) return;
            int bushes = Mathf.Max(1, c.Count / 2), made = 0;
            for (int attempt = 0; made < bushes && attempt < bushes * 40; attempt++)
            {
                float a = rng.Value() * Mathf.PI * 2f, r = rng.Range(.8f, 1.2f);
                float x = c.Center.x + Mathf.Cos(a) * r * c.RadiusX, z = c.Center.y + Mathf.Sin(a) * r * c.RadiusZ;
                if (data.Path.EdgeDistance(x, z) < 3f) continue;
                data.Trees.Add(TreeFactory.Build(layers, TreeKind.Shrub, TreeTier.Edge, OnGround(data, x, z), 1f, rng.Range(.9f, 1.4f), rng.Fork(900 + attempt), haze));
                made++;
            }
        }

        // ---- distance: forest line and hills --------------------------------------------------------------------------------------

        /// <summary>Lowers the far layers around the path's azimuth so the view opens over the ridge between the clumps ("this way").</summary>
        static float Opening(float azimuthDegrees, float depth, float sigma)
        {
            float d = Mathf.DeltaAngle(azimuthDegrees, Plan.OpeningAzimuth) / sigma;
            return 1f - depth * Mathf.Exp(-d * d);
        }

        /// <summary>
        /// A silhouette of tree crowns and spruce tips all around the viewer (a ribbon seen from inside): crowns of random width and height overlap along the circle,
        /// flat colour, no shadows. This is the "simplified distant forest" and the horizon behind the clumps.
        /// </summary>
        static void AddForestLine(SceneData data, MeshBuilder mb, float radius, float baseHeight, float minWidth, float maxWidth, float minCrown, float maxCrown, Rng rng)
        {
            float circumference = Mathf.PI * 2f * radius;
            int segments = Mathf.CeilToInt(circumference / 1.6f);
            float seg = circumference / segments;
            var profile = new float[segments];
            for (float pos = 0f; pos < circumference;)
            {
                float w = rng.Range(minWidth, maxWidth), h = rng.Range(minCrown, maxCrown);
                bool spruce = rng.Value() < .25f;
                if (spruce) { w *= .7f; h *= 1.1f; }
                for (int i = Mathf.FloorToInt(pos / seg); i <= Mathf.CeilToInt((pos + w) / seg); i++)
                {
                    float u = (i * seg - pos) / w * 2f - 1f;
                    if (Mathf.Abs(u) > 1f) continue;
                    int index = (i % segments + segments) % segments;
                    profile[index] = Mathf.Max(profile[index], h * (spruce ? 1f - Mathf.Abs(u) : Mathf.Sqrt(1f - u * u)));
                }
                pos += w * rng.Range(.5f, .9f);
            }
            // a few clearings where the line drops to meadow height and the next layer shows through
            var gain = new float[segments];
            for (int i = 0; i < segments; i++) gain[i] = 1f;
            for (int c = 0; c < 4; c++)
            {
                float centre = rng.Value() * segments, half = rng.Range(14f, 34f) / seg;
                for (int i = 0; i < segments; i++)
                {
                    float dd = Mathf.Abs(Mathf.DeltaAngle(i * 360f / segments, centre * 360f / segments)) / 360f * segments / half;
                    gain[i] = Mathf.Min(gain[i], Mathf.Lerp(.2f, 1f, Mathf.SmoothStep(0f, 1f, dd - .3f)));
                }
            }
            var top = new Vector3[segments];
            var bottom = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float az = i * Mathf.PI * 2f / segments, deg = az / Deg;
                float x = Mathf.Sin(az) * radius, z = Mathf.Cos(az) * radius;
                top[i] = new Vector3(x, TerrainModel.Height(x, z) + (baseHeight + profile[i]) * gain[i] * Opening(deg, .72f, 15f), z);
                bottom[i] = new Vector3(x, -8f, z);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                mb.TriToward(bottom[i], top[i], top[j], Vector3.zero);
                mb.TriToward(bottom[i], top[j], bottom[j], Vector3.zero);
            }
        }

        /// <summary>
        /// A ring of soft hills: rounded humps of different width and height along the horizon, seen from the front slope (it rises from below the ground to the crest).
        /// Each ring is paler and bluer-green than the one before, the only "atmosphere" (no fog).
        /// </summary>
        static void AddHills(SceneData data, MeshBuilder mb, float crestRadius, float baseHeight, float amplitude, int humps, Rng rng)
        {
            const int segments = 240;
            var centers = new float[humps]; var widths = new float[humps]; var heights = new float[humps];
            for (int k = 0; k < humps; k++)
            {
                centers[k] = (k + rng.Range(.15f, .85f)) * 360f / humps; widths[k] = rng.Range(9f, 20f); heights[k] = rng.Range(.45f, 1f) * amplitude;
            }
            var top = new Vector3[segments]; var bottom = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float deg = i * 360f / segments, az = deg * Deg;
                float h = baseHeight * .6f;
                for (int k = 0; k < humps; k++)
                {
                    float d = Mathf.DeltaAngle(deg, centers[k]) / widths[k];
                    h += heights[k] * Mathf.Exp(-d * d);
                }
                h *= Opening(deg, .5f, 18f);
                float x = Mathf.Sin(az) * crestRadius, z = Mathf.Cos(az) * crestRadius;
                top[i] = new Vector3(x, TerrainModel.Height(x, z) + h, z);
                float run = (h + 8f) * 2.6f, baseRadius = crestRadius - run;
                bottom[i] = new Vector3(Mathf.Sin(az) * baseRadius, -8f, Mathf.Cos(az) * baseRadius);
            }
            Vector3 above = new Vector3(0f, 80f, 0f);
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                mb.TriToward(bottom[i], top[i], top[j], above);
                mb.TriToward(bottom[i], top[j], bottom[j], above);
            }
        }

        // ---- meadow details ---------------------------------------------------------------------------------------------------------

        static void AddGrass(SceneData data, LayerSet layers, Rng rng)
        {
            foreach (var group in Plan.GrassGroups)
            {
                for (int i = 0; i < group.Clumps; i++)
                {
                    float a = i * 2.39996f + rng.Signed() * .4f, r = Mathf.Sqrt((i + .5f) / group.Clumps) * group.Radius;
                    Vector3 p = OnGround(data, group.Center.x + Mathf.Cos(a) * r, group.Center.y + Mathf.Sin(a) * r);
                    data.GrassClumps.Add(p);
                    int blades = 6 + rng.Int(4);
                    float size = rng.Range(.8f, 1.25f);
                    for (int b = 0; b < blades; b++)
                    {
                        float yaw = b * 2.39996f + rng.Value(), reach = rng.Range(.04f, .16f);
                        Vector3 root = p + new Vector3(Mathf.Cos(yaw) * reach, 0f, Mathf.Sin(yaw) * reach);
                        float pick = rng.Value();
                        Mat m = pick < .2f ? Mat.GrassDark : pick < .72f ? Mat.GrassMid : Mat.GrassLight;
                        Shapes.Blade(layers.Get(m, false), root, yaw, rng.Range(.38f, .72f) * size, rng.Range(.07f, .12f), rng.Range(.12f, .34f) * size);
                    }
                }
            }
        }

        static void AddFlowers(SceneData data, LayerSet layers, Rng rng)
        {
            foreach (var island in Plan.FlowerIslands)
            {
                data.IslandCenters.Add(OnGround(data, island.Center.x, island.Center.y));
                for (int i = 0; i < island.Flowers; i++)
                {
                    float a = i * 2.39996f + rng.Signed() * .3f, r = Mathf.Sqrt((i + .5f) / island.Flowers) * island.Radius * rng.Range(.85f, 1.1f);
                    Vector3 p = OnGround(data, island.Center.x + Mathf.Cos(a) * r, island.Center.y + Mathf.Sin(a) * r);
                    data.Flowers.Add(p);
                    Shapes.Flower(layers.Get(Mat.FlowerStem, false), layers.Get(island.Petal, false), layers.Get(Mat.FlowerCenter, false), p, rng.Range(1.0f, 1.4f), rng);
                }
            }
        }

        static void AddRocks(SceneData data, LayerSet layers, Rng rng)
        {
            foreach (var rock in Plan.Rocks)
            {
                Vector3 ground = OnGround(data, rock.Position.x, rock.Position.y);
                var radii = new Vector3(rock.Size * rng.Range(.95f, 1.1f), rock.Size * .78f, rock.Size * rng.Range(.72f, .88f));
                Shapes.Lobe(layers.Get(rock.Warm ? Mat.StoneWarm : Mat.Stone, true), ground + Vector3.up * (radii.y * .1f), radii,
                    Basis.Euler(rng.Value() * 6.28f, rng.Signed() * .08f, rng.Signed() * .08f), rng, .17f, 1, .5f);
                data.Rocks.Add(new RockRecord { Position = ground, Radius = Mathf.Max(radii.x, radii.z) });
            }
        }

        /// <summary>
        /// A few soft clouds high up: each is a cluster of rounded lobes (a row below, a few on top), flat coloured and two-tone by face direction
        /// (white on top, pale blue underneath), so they read as faceted volume without light, shadows or transparency.
        /// </summary>
        static void AddClouds(MeshBuilder top, MeshBuilder bottom, Rng rng)
        {
            foreach (var cloud in Plan.Clouds)
            {
                float az = cloud.Azimuth * Deg;
                Vector3 centre = new Vector3(Mathf.Sin(az) * cloud.Distance, cloud.Altitude, Mathf.Cos(az) * cloud.Distance);
                Vector3 along = new Vector3(Mathf.Cos(az), 0f, -Mathf.Sin(az)), towards = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
                int lobes = 8;
                for (int i = 0; i < lobes; i++)
                {
                    bool upperRow = i >= 5;
                    float t = upperRow ? (i - 5) / 2f - .5f : i / 4f - .5f;                 // -.5 .. .5 across the cloud
                    float rx = (upperRow ? 8.5f : 10.5f) * cloud.Scale * (1f - .5f * Mathf.Abs(t) * 2f * (upperRow ? .8f : 1f)) * rng.Range(.88f, 1.1f);
                    Vector3 p = centre + along * (t * 30f * cloud.Scale) + towards * (rng.Signed() * 4f * cloud.Scale) + Vector3.up * ((upperRow ? 5.2f : 0f) * cloud.Scale + rng.Signed() * .8f);
                    Shapes.LobeSplit(top, bottom, .12f, p, new Vector3(rx, rx * .62f, rx * .8f), Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .1f, 1, .5f);
                }
            }
        }
    }
}
