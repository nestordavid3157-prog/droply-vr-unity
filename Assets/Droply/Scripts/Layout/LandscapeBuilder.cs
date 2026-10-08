using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    public struct RockRecord
    {
        public Vector3 Position;
        public float Radius;
    }

    /// <summary>Everything the landscape consists of, as plain data: merged mesh layers (with baked colours) plus the records the composition checks look at.</summary>
    public sealed class SceneData
    {
        public readonly List<MeshLayer> Layers = new List<MeshLayer>();
        public readonly List<TreeRecord> Trees = new List<TreeRecord>();
        public readonly List<Vector3> GrassClumps = new List<Vector3>();
        public readonly List<Vector3> Flowers = new List<Vector3>();
        /// <summary>Small tufts lining the path edge (they may stand closer to it than the deliberate groups, but never on it).</summary>
        public readonly List<Vector3> EdgeTufts = new List<Vector3>();
        /// <summary>Small pebbles along the path edge (a few centimetres to a hand's breadth): scale cues close to the viewer.</summary>
        public readonly List<Vector3> Pebbles = new List<Vector3>();
        /// <summary>Number of small grass flecks in patches (not a tuft group: a ground texture made of a few triangles each).</summary>
        public int FleckCount;
        public readonly List<Vector3> IslandCenters = new List<Vector3>();
        public readonly List<RockRecord> Rocks = new List<RockRecord>();
        public int OccluderCount;
        /// <summary>Time the light bake took on this machine (diagnostic; the headset is slower).</summary>
        public long BakeMilliseconds;
        public PathModel Path;
        public TerrainModel Terrain;
        /// <summary>Where the viewer may walk (null in hand-made test scenes).</summary>
        public WalkArea Walk;

        public int TriangleCount
        {
            get { int total = 0; foreach (var layer in Layers) total += layer.Mesh.TriangleCount; return total; }
        }
    }

    /// <summary>
    /// Builds the landscape from <see cref="Plan"/>: terrain, path, tree group, forest clumps, simplified forest, forest line and hills, grass, flowers, stones, clouds,
    /// then bakes the light into the vertex colours (<see cref="Lighting"/>) and adds the sky dome. Pure geometry and deterministic: the player and
    /// Tools/CompositionCheck call exactly this.
    /// </summary>
    public static class LandscapeBuilder
    {
        const int Seed = 3157;
        /// <summary>Radius of the nearest forest line (a silhouette ribbon around the start).</summary>
        public const float ForestLineRadius = 150f;
        /// <summary>Radius of the farthest hill layer: the ground behind it is hidden.</summary>
        public const float OutermostHills = 288f;
        const float Deg = Mathf.PI / 180f;

        public static SceneData Build() { return Build(true); }

        /// <param name="parallelBake">Bake the light on all processor cores (the normal case); false bakes on one thread, for the self test that compares both.</param>
        public static SceneData Build(bool parallelBake)
        {
            var data = new SceneData { Terrain = new TerrainModel(), Path = new PathModel() };
            var root = new Rng(Seed);
            var layers = new LayerSet();

            AddTerrain(data);
            AddPath(data);
            // what the viewer can walk up to gets full detail (the outline alone: trunks and stones do not matter for that)
            var reach = new WalkArea(data.Terrain, Plan.WalkOutline, new List<WalkArea.Disc>());
            AddHeroGroup(data, layers, root.Fork(1), reach);
            foreach (var cluster in Plan.EdgeClusters) AddCluster(data, layers, cluster, TreeTier.Edge, root.Fork(10 + data.Trees.Count), reach);
            foreach (var cluster in Plan.MidClusters) AddCluster(data, layers, cluster, TreeTier.Mid, root.Fork(100 + data.Trees.Count), reach);
            // Each layer peeks over the one in front (angular height grows with distance) and is paler and bluer-green: forest line 1.2-2.3 deg, then 2-3 deg, hills 3-7 deg.
            AddForestLine(data, layers.Get(Mat.TreeLineNear, false), ForestLineRadius, 1.8f, 4.5f, 8.5f, 3f, 6f, root.Fork(3));
            AddForestLine(data, layers.Get(Mat.TreeLineFar, false), 186f, 3.5f, 5.5f, 10f, 4f, 7.5f, root.Fork(4));
            AddHills(data, layers.Get(Mat.Hill1, false), 206f, 10f, 13f, 7, root.Fork(5));
            AddHills(data, layers.Get(Mat.Hill2, false), 246f, 15f, 19f, 8, root.Fork(6));
            AddHills(data, layers.Get(Mat.Hill3, false), OutermostHills, 22f, 27f, 9, root.Fork(7));
            AddGrass(data, layers, root.Fork(8));
            AddPathTufts(data, layers, root.Fork(13));
            AddPebbles(data, layers, root.Fork(15));
            AddFlowers(data, layers, root.Fork(9));
            AddFlecks(data, layers, root.Fork(14));
            AddRocks(data, layers, root.Fork(11));
            AddClouds(layers, layers.Get(Mat.CloudTop, false), layers.Get(Mat.CloudBottom, false), root.Fork(12));

            data.Walk = WalkArea.For(data);
            data.Layers.AddRange(layers.Finish());
            data.OccluderCount = layers.Occluders.Count;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var lighting = new Lighting(data.Terrain, layers.Occluders);
            lighting.BakeAll(data.Layers, parallelBake);
            data.BakeMilliseconds = watch.ElapsedMilliseconds;
            Sky.Build(data.Layers, lighting.TowardSun);
            return data;
        }

        // ---- ground and path ------------------------------------------------------------------------------------------------------

        static void AddTerrain(SceneData data)
        {
            data.Layers.Add(new MeshLayer { Name = "Ground", Material = Mat.GroundBase, CastShadows = false, Mesh = data.Terrain.BuildMesh() });
        }

        /// <summary>
        /// The sand path as one ribbon draped on the rendered terrain, seven vertices across: light sand in the middle, sand, a darker worn edge, and an outer row that has
        /// exactly the colour of the meadow at that spot, so the edge ends without a hard line but also without a blur (the fade is a hand's width: about 0.1 m). The width wobbles
        /// (no ruler line), the path tapers in at the start and out at the far end.
        /// </summary>
        static void AddPath(SceneData data)
        {
            const int Rows = 7;
            var path = data.Path;
            var terrain = data.Terrain;
            int n = Mathf.CeilToInt(path.Length / 1f);
            var vertices = new Vector3[(n + 1) * Rows];
            var normals = new Vector3[vertices.Length];
            var tags = new float[vertices.Length];
            float[] rowTag = { 1f, .6f, .3f, 0f, .3f, .6f, 1f };
            for (int i = 0; i <= n; i++)
            {
                float s = path.Length * i / n;
                Vector3 p = path.PointAt(s), d = path.DirectionAt(s);
                Vector3 side = new Vector3(d.z, 0f, -d.x);
                float taper = Mathf.SmoothStep(.3f, 1f, Mathf.Clamp01(s / 6f)) * Mathf.SmoothStep(.4f, 1f, Mathf.Clamp01((path.Length - s) / 16f));
                float hw = path.HalfWidth(s) * taper;
                // the edge wanders (slow swing plus a small, faster wobble) so it is never a ruler line, and fades out over a hand's width only: a soft low-poly edge, not a blur
                float wl = hw * (1f + .12f * Noise.Signed(s * .11f, 0f, 61)) + .07f * taper * Noise.Signed(s * .52f, 1f, 65);
                float wr = hw * (1f + .12f * Noise.Signed(s * .11f, 9f, 62)) + .07f * taper * Noise.Signed(s * .52f, 3f, 66);
                float el = (.11f + .05f * Noise.Signed(s * .17f, 5f, 63)) * taper, er = (.11f + .05f * Noise.Signed(s * .17f, 7f, 64)) * taper;
                float fade = .03f * taper;
                float[] offset =
                {
                    -(wl + el + fade), -(wl + el * .6f), -wl * .88f, 0f, wr * .88f, wr + er * .6f, wr + er + fade
                };
                for (int r = 0; r < Rows; r++)
                {
                    Vector3 q = p + side * offset[r];
                    int k = i * Rows + r;
                    vertices[k] = new Vector3(q.x, terrain.SurfaceY(q.x, q.z) + (r == 0 || r == Rows - 1 ? .035f : .045f), q.z);
                    normals[k] = terrain.SurfaceNormal(q.x, q.z);
                    tags[k] = rowTag[r];
                }
            }
            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
                for (int r = 0; r < Rows - 1; r++)
                {
                    int a = i * Rows + r, b = a + 1, c = a + Rows, d = c + 1;
                    AddUp(vertices, triangles, a, b, c);
                    AddUp(vertices, triangles, b, d, c);
                }
            data.Layers.Add(new MeshLayer
            {
                Name = "Sand path", Material = Mat.PathCore,
                Mesh = new MeshData { Vertices = vertices, Normals = normals, Triangles = triangles.ToArray(), Tags = tags, Jitters = new float[vertices.Length], Owners = new int[vertices.Length] }
            });
        }

        static void AddUp(Vector3[] v, List<int> t, int a, int b, int c)
        {
            if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y >= 0f) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
        }

        // ---- trees ----------------------------------------------------------------------------------------------------------------

        static Vector3 OnGround(SceneData data, float x, float z) { return new Vector3(x, data.Terrain.SurfaceY(x, z), z); }

        static void AddHeroGroup(SceneData data, LayerSet layers, Rng rng, WalkArea reach)
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
            for (int i = 0; i < Plan.SideBushes.Length; i++)
            {
                var bush = Plan.SideBushes[i];
                data.Trees.Add(TreeFactory.Build(layers, TreeKind.Shrub, TreeTier.Edge, OnGround(data, bush.Position.x, bush.Position.y), bush.Size, bush.Size, rng.Fork(70 + i), Mat.HazeTree1,
                    reach.Distance(bush.Position) < LandscapeChecks.FullDetailDistance));
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
                case TreeKind.Birch: return height * .2f;
                case TreeKind.Spruce: return height * .23f;
                case TreeKind.Blob: return height * .34f;
                default: return height * .22f;
            }
        }

        /// <summary>
        /// A forest clump: trees dropped into an ellipse with rejection (crowns may overlap a little but never stack into a wall, nothing on the path),
        /// tallest in the middle and shorter towards the rim. No grid, no rows.
        /// </summary>
        static void AddCluster(SceneData data, LayerSet layers, Plan.Cluster c, TreeTier tier, Rng rng, WalkArea reach)
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
                TreeKind kind = simple ? (k < .7f ? TreeKind.Blob : TreeKind.BlobSpruce)
                                       : (k < .28f ? TreeKind.Oak : k < .5f ? TreeKind.Beech : k < .62f ? TreeKind.Birch : TreeKind.Spruce);
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
                bool reachable = reach.Distance(new Vector2(x, z)) < LandscapeChecks.FullDetailDistance;
                data.Trees.Add(TreeFactory.Build(layers, kind, tier, OnGround(data, x, z), height, spread, rng.Fork(attempt), haze, reachable));
                placed++;
            }
            if (tier != TreeTier.Edge) return;
            int bushes = Mathf.Max(1, c.Count / 2), made = 0;
            for (int attempt = 0; made < bushes && attempt < bushes * 40; attempt++)
            {
                float a = rng.Value() * Mathf.PI * 2f, r = rng.Range(.8f, 1.2f);
                float x = c.Center.x + Mathf.Cos(a) * r * c.RadiusX, z = c.Center.y + Mathf.Sin(a) * r * c.RadiusZ;
                if (data.Path.EdgeDistance(x, z) < 3f) continue;
                data.Trees.Add(TreeFactory.Build(layers, TreeKind.Shrub, TreeTier.Edge, OnGround(data, x, z), 1f, rng.Range(.9f, 1.4f), rng.Fork(900 + attempt), haze,
                    reach.Distance(new Vector2(x, z)) < LandscapeChecks.FullDetailDistance));
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
        /// with a few clearings where the next layer shows through. Colour is hazier at the foot and deeper at the crest (tag 0 to 1).
        /// This is the "simplified distant forest" and the horizon behind the clumps.
        /// </summary>
        static void AddForestLine(SceneData data, MeshBuilder mb, float radius, float baseHeight, float minWidth, float maxWidth, float minCrown, float maxCrown, Rng rng)
        {
            float circumference = Mathf.PI * 2f * radius;
            int segments = Mathf.CeilToInt(circumference / 1f);
            float seg = circumference / segments;
            var profile = new float[segments];
            for (float pos = 0f; pos < circumference;)
            {
                float w = rng.Range(minWidth, maxWidth), h = rng.Range(minCrown, maxCrown);
                bool spruce = rng.Value() < .18f;
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
            var shade = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                float az = i * Mathf.PI * 2f / segments, deg = az / Deg;
                float x = Mathf.Sin(az) * radius, z = Mathf.Cos(az) * radius;
                // a small, quick unevenness on every crown (leaf clumps) so the line reads as trees, not as a mountain ridge; and a brightness that drifts along the line
                float bump = 1f + .16f * Noise.Signed(i * .55f, radius * .01f, 77) + .07f * Noise.Signed(i * 1.9f, radius * .01f, 78);
                top[i] = new Vector3(x, TerrainModel.Height(x, z) + (baseHeight + profile[i] * bump) * gain[i] * Opening(deg, .72f, 15f), z);
                bottom[i] = new Vector3(x, -8f, z);
                shade[i] = .14f * Noise.Signed(i * .13f, radius * .01f, 79);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                mb.Jitter = shade[i];
                mb.TriToward(bottom[i], top[i], top[j], Vector3.zero, new Vector3(0f, 1f, 1f));
                mb.TriToward(bottom[i], top[j], bottom[j], Vector3.zero, new Vector3(0f, 1f, 0f));
            }
        }

        /// <summary>
        /// A ring of soft hills: rounded humps of different width and height along the horizon, seen from the front slope (it rises from below the ground to the crest).
        /// Each ring is paler and bluer-green than the one before, the only "atmosphere" besides the baked haze (no fog).
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
                mb.TriToward(bottom[i], top[i], top[j], above, new Vector3(0f, 1f, 1f));
                mb.TriToward(bottom[i], top[j], bottom[j], above, new Vector3(0f, 1f, 0f));
            }
        }

        // ---- meadow details ---------------------------------------------------------------------------------------------------------

        /// <summary>Blade width factor by distance from the viewer: 1 up to 20 m, widening to 2.2 at 80 m, so far blades stay at least about a pixel wide (no shimmer in the headset).</summary>
        static float BladeWidth(Vector3 p)
        {
            float d = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            return Mathf.Lerp(1f, 2.2f, Mathf.Clamp01((d - 20f) / 60f));
        }

        static void AddGrass(SceneData data, LayerSet layers, Rng rng)
        {
            MeshBuilder dark = layers.Get(Mat.GrassDark, false), mid = layers.Get(Mat.GrassMid, false), light = layers.Get(Mat.GrassLight, false);
            foreach (var group in Plan.GrassGroups)
            {
                for (int i = 0; i < group.Clumps; i++)
                {
                    float a = i * 2.39996f + rng.Signed() * .4f, r = Mathf.Sqrt((i + .5f) / group.Clumps) * group.Radius;
                    Vector3 p = OnGround(data, group.Center.x + Mathf.Cos(a) * r, group.Center.y + Mathf.Sin(a) * r);
                    data.GrassClumps.Add(p);
                    float size = rng.Range(.85f, 1.3f);
                    Shapes.Tuft(dark, mid, light, data.Terrain, p, rng.Range(.36f, .55f) * size, 10 + rng.Int(6), .28f * size, .7f * size, rng, BladeWidth(p));   // at most about 0.9 m tall
                }
            }
        }

        /// <summary>Tufts lining the path edge, now on one side, now on the other, at uneven distances: they break up the edge line and make the path part of the meadow.</summary>
        static void AddPathTufts(SceneData data, LayerSet layers, Rng rng)
        {
            MeshBuilder dark = layers.Get(Mat.GrassDark, false), mid = layers.Get(Mat.GrassMid, false), light = layers.Get(Mat.GrassLight, false);
            var path = data.Path;
            float side = 1f;
            for (float s = 12f; s < path.Length - 14f; s += rng.Range(4.5f, 9.5f))
            {
                if (rng.Value() < .65f) side = -side;
                Vector3 p = path.PointAt(s), d = path.DirectionAt(s);
                float offset = path.HalfWidth(s) + rng.Range(.62f, 1.25f);
                float x = p.x + d.z * side * offset, z = p.z - d.x * side * offset;
                Vector3 ground = OnGround(data, x, z);
                data.EdgeTufts.Add(ground);
                Shapes.Tuft(dark, mid, light, data.Terrain, ground, rng.Range(.28f, .46f), 8 + rng.Int(4), .2f, rng.Range(.4f, .62f), rng, BladeWidth(ground));
            }
        }

        /// <summary>
        /// The meadow's own grass: small flecks of a few spikes each, in irregular patches (never a carpet): thinner with distance, none within 3.5 m of the viewer,
        /// none on the path or on a flower island, a little more along the path edge. They give the ground its grain and a bit of parallax when the head moves.
        /// </summary>
        static void AddFlecks(SceneData data, LayerSet layers, Rng rng)
        {
            MeshBuilder dark = layers.Get(Mat.GrassDark, false), mid = layers.Get(Mat.GrassMid, false), light = layers.Get(Mat.GrassLight, false);
            const int Wanted = 1250;
            int made = 0;
            for (int attempt = 0; attempt < 16000 && made < Wanted; attempt++)
            {
                float x = rng.Range(-48f, 48f), z = rng.Range(2f, 95f);
                float distance = Mathf.Sqrt(x * x + z * z);
                if (distance < 3.5f) continue;
                float edge = data.Path.EdgeDistance(x, z);
                if (edge < .55f) continue;
                float patch = Noise.Value(x * .1f + 31f, z * .1f + 7f, 73);
                float want = Mathf.Clamp01((patch - .5f) / .3f) * Mathf.Lerp(1f, .3f, Mathf.Clamp01((distance - 25f) / 60f));
                if (edge < 2.4f) want = Mathf.Max(want, .45f * (1f - (edge - .55f) / 1.85f));
                if (rng.Value() > want) continue;
                bool onIsland = false;
                foreach (var island in Plan.FlowerIslands)
                {
                    float dx = x - island.Center.x, dz = z - island.Center.y;
                    if (Mathf.Sqrt(dx * dx + dz * dz) < island.Radius + .5f) { onIsland = true; break; }
                }
                if (onIsland) continue;
                float height = rng.Range(.12f, .26f) * Mathf.Lerp(.8f, 1.6f, Mathf.Clamp01(distance / 70f));   // small close to the viewer, a little larger far away so they still read
                Shapes.Tuft(dark, mid, light, data.Terrain, OnGround(data, x, z), height * 1.1f, 5 + rng.Int(3), height * .6f, height, rng, BladeWidth(new Vector3(x, 0f, z)));
                made++;
            }
            data.FleckCount = made;
        }

        /// <summary>
        /// A few pebbles lying along the path edge, alone or in twos and threes, a few centimetres to a hand's breadth across. Close to the viewer they are the cue for
        /// scale and depth (the eye reads the ground plane from them); they do not touch the path itself.
        /// </summary>
        static void AddPebbles(SceneData data, LayerSet layers, Rng rng)
        {
            var path = data.Path;
            for (float s = 4f; s < 64f; s += rng.Range(5f, 11f))
            {
                int group = 1 + rng.Int(3);
                float side = rng.Value() < .5f ? -1f : 1f, baseOffset = path.HalfWidth(s) + rng.Range(.35f, 1.1f);
                for (int k = 0; k < group; k++)
                {
                    Vector3 p = path.PointAt(s + k * rng.Range(.2f, .6f)), d = path.DirectionAt(s + k * .4f);
                    float offset = baseOffset + rng.Range(0f, .4f);
                    float x = p.x + d.z * side * offset, z = p.z - d.x * side * offset;
                    float size = rng.Range(.05f, .15f);
                    Vector3 ground = OnGround(data, x, z);
                    data.Pebbles.Add(ground);
                    Mat mat = rng.Value() < .4f ? Mat.StoneWarm : Mat.Stone;
                    Shapes.Lobe(layers.Get(mat, false), ground + Vector3.up * (size * .12f), new Vector3(size * rng.Range(.9f, 1.3f), size * .6f, size * rng.Range(.7f, 1f)),
                        Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .2f, 0, .4f);
                }
            }
        }

        static Shapes.FlowerKind KindOf(Mat petal)
        {
            return petal == Mat.FlowerLavender ? Shapes.FlowerKind.Lavender : petal == Mat.FlowerWhite ? Shapes.FlowerKind.Daisy : Shapes.FlowerKind.Buttercup;
        }

        /// <summary>
        /// A flower island: a few flat mounds of leaves (the green the flowers grow out of) with the flowers rising above them, in one main colour and sometimes a second.
        /// Daisy, buttercup and lavender spike each have their own shape; the head is large enough to read from 20 m.
        /// </summary>
        static void AddFlowers(SceneData data, LayerSet layers, Rng rng)
        {
            foreach (var island in Plan.FlowerIslands)
            {
                data.IslandCenters.Add(OnGround(data, island.Center.x, island.Center.y));
                int mounds = 4 + rng.Int(3);
                for (int k = 0; k < mounds; k++)
                {
                    float a = k * 2.39996f + rng.Value(), r = island.Radius * .6f * Mathf.Sqrt((k + .3f) / mounds);
                    float rx = rng.Range(.5f, .85f) * island.Radius / 1.5f, ry = rng.Range(.2f, .34f);
                    Vector3 c = OnGround(data, island.Center.x + Mathf.Cos(a) * r, island.Center.y + Mathf.Sin(a) * r) + Vector3.up * (ry * .3f);
                    Shapes.Lobe(layers.Get(k % 3 == 0 ? Mat.GrassDark : Mat.GrassMid, false), c, new Vector3(rx, ry, rx * rng.Range(.8f, 1.1f)),
                        Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .3f, 0, .4f);
                }
                // a ring of leaf tufts around the island, so it grows out of the meadow instead of lying on it
                for (int k = 0; k < 4; k++)
                {
                    float a = k * 1.6f + rng.Value() * .8f, r = island.Radius * rng.Range(.85f, 1.15f);
                    Vector3 c = OnGround(data, island.Center.x + Mathf.Cos(a) * r, island.Center.y + Mathf.Sin(a) * r);
                    Shapes.Tuft(layers.Get(Mat.GrassDark, false), layers.Get(Mat.GrassMid, false), layers.Get(Mat.GrassLight, false), data.Terrain, c, rng.Range(.3f, .45f), 8, .22f, rng.Range(.45f, .65f), rng, BladeWidth(c));
                }
                for (int i = 0; i < island.Flowers; i++)
                {
                    float a = i * 2.39996f + rng.Signed() * .3f, r = Mathf.Sqrt((i + .5f) / island.Flowers) * island.Radius * rng.Range(.85f, 1.1f);
                    Vector3 p = OnGround(data, island.Center.x + Mathf.Cos(a) * r, island.Center.y + Mathf.Sin(a) * r);
                    data.Flowers.Add(p);
                    Mat petal = island.Accent != Mat.GroundBase && rng.Value() < .26f ? island.Accent : island.Petal;
                    Shapes.Flower(layers.Get(Mat.FlowerStem, false), layers.Get(petal, false), layers.Get(Mat.FlowerCenter, false), KindOf(petal), p, rng.Range(1.0f, 1.45f), rng);
                }
            }
        }

        static void AddRocks(SceneData data, LayerSet layers, Rng rng)
        {
            foreach (var rock in Plan.Rocks)
            {
                Vector3 ground = OnGround(data, rock.Position.x, rock.Position.y);
                var radii = new Vector3(rock.Size * rng.Range(.95f, 1.1f), rock.Size * .78f, rock.Size * rng.Range(.72f, .88f));
                Vector3 centre = ground + Vector3.up * (radii.y * .1f);
                layers.CurrentOwner = layers.NewOwner();
                Shapes.Lobe(layers.Get(rock.Warm ? Mat.StoneWarm : Mat.Stone, true), centre, radii,
                    Basis.Euler(rng.Value() * 6.28f, rng.Signed() * .08f, rng.Signed() * .08f), rng, .17f, 1, .5f);
                layers.AddOccluder(centre + Vector3.up * (radii.y * .15f), radii, 1f);
                layers.CurrentOwner = 0;
                data.Rocks.Add(new RockRecord { Position = ground, Radius = Mathf.Max(radii.x, radii.z) });
            }
        }

        /// <summary>
        /// A few soft clouds high up: each is a cluster of rounded lobes (a row below, a few on top), faceted, two-tone by face direction
        /// (white on top, pale blue underneath), so they read as volume without light, shadows or transparency.
        /// </summary>
        static void AddClouds(LayerSet layers, MeshBuilder top, MeshBuilder bottom, Rng rng)
        {
            foreach (var cloud in Plan.Clouds)
            {
                if (cloud.Shades) layers.CurrentOwner = layers.NewOwner();
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
                    Vector3 radii = new Vector3(rx, rx * .62f, rx * .8f);
                    Shapes.LobeSplit(top, bottom, .12f, p, radii, Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .1f, 1, .5f);
                    if (cloud.Shades) layers.AddOccluder(p, radii, .5f);
                }
                layers.CurrentOwner = 0;
            }
        }
    }
}
