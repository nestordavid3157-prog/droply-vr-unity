using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    public enum TreeKind { Oak, Beech, Spruce, Shrub, Blob, BlobSpruce }

    /// <summary>Where in the depth of the scene a tree stands. Detail falls with distance: Hero > Edge > Mid > Far.</summary>
    public enum TreeTier { Hero, Edge, Mid, Far }

    public struct TreeRecord
    {
        public Vector3 Position;
        public float Height, Spread;
        public TreeKind Kind;
        public TreeTier Tier;
        public int Triangles;
    }

    public sealed class MeshLayer
    {
        public string Name;
        public Mat Material;
        public bool CastShadows;
        public MeshData Mesh;
    }

    /// <summary>Collects geometry per material (and per shadow class), so the whole landscape becomes a handful of merged static meshes.</summary>
    public sealed class LayerSet
    {
        readonly SortedDictionary<int, MeshBuilder> builders = new SortedDictionary<int, MeshBuilder>();

        public MeshBuilder Get(Mat material, bool shadows)
        {
            int key = (int)material * 2 + (shadows ? 1 : 0);
            MeshBuilder builder;
            if (!builders.TryGetValue(key, out builder)) { builder = new MeshBuilder(); builders[key] = builder; }
            return builder;
        }

        public int TriangleCount
        {
            get { int total = 0; foreach (var b in builders) total += b.Value.TriangleCount; return total; }
        }

        public List<MeshLayer> Finish()
        {
            var layers = new List<MeshLayer>();
            foreach (var pair in builders)
            {
                if (pair.Value.TriangleCount == 0) continue;
                var material = (Mat)(pair.Key / 2);
                bool shadows = pair.Key % 2 == 1;
                layers.Add(new MeshLayer { Name = material + (shadows ? " (casts shadows)" : ""), Material = material, CastShadows = shadows, Mesh = pair.Value.ToMesh() });
            }
            return layers;
        }
    }

    /// <summary>
    /// Trees. A tree is a trunk, limbs and several unequal crown lobes (never a ball on a stick, never a cone). Near trees have more lobes and finer facets,
    /// mid trees are small groups of simple lobes, far trees are tinted blobs. All of it is merged into the layer set.
    /// </summary>
    public static class TreeFactory
    {
        public static TreeRecord Build(LayerSet layers, TreeKind kind, TreeTier tier, Vector3 position, float height, float spread, Rng rng, Mat haze)
        {
            int before = layers.TriangleCount;
            bool shadows = tier == TreeTier.Hero || tier == TreeTier.Edge;
            switch (kind)
            {
                case TreeKind.Oak: Oak(layers, position, height, spread, rng, shadows, tier == TreeTier.Hero ? 1 : 0, tier == TreeTier.Hero ? 11 : 7); break;
                case TreeKind.Beech: Beech(layers, position, height, spread, rng, shadows, tier == TreeTier.Hero ? 1 : 0); break;
                case TreeKind.Spruce: Spruce(layers, position, height, spread, rng, shadows, tier == TreeTier.Hero ? 7 : 6, tier == TreeTier.Hero ? 6 : 5); break;
                case TreeKind.Shrub: Shrub(layers, position, spread, rng, shadows); break;
                case TreeKind.Blob: Blob(layers, position, height, spread, rng, haze); break;
                default: BlobSpruce(layers, position, height, spread, rng, haze); break;
            }
            return new TreeRecord { Position = position, Height = height, Spread = spread, Kind = kind, Tier = tier, Triangles = layers.TriangleCount - before };
        }

        static Mat CrownMaterial(float f, Rng rng, Mat top, Mat mid, Mat dark)
        {
            // High lobes catch the sun (light), low ones sit in blue-green shade (dark); a few swap with a neighbour so it never looks banded.
            float g = f + rng.Signed() * .1f;
            return g > .62f ? top : g > .3f ? mid : dark;
        }

        /// <summary>Broad oak: short thick trunk, three limbs, a wide dome of unequal lobes with small ones at the limb tips.</summary>
        static void Oak(LayerSet layers, Vector3 pos, float h, float spread, Rng rng, bool shadows, int subdivisions, int lobes)
        {
            var bark = layers.Get(Mat.Trunk, shadows);
            float r0 = Mathf.Max(.13f, .04f * h);
            float lean = rng.Signed() * .3f * h / 9f, forkY = .33f * h;
            Vector3 fork = pos + new Vector3(lean, forkY, lean * .3f);
            Shapes.Tube(bark, new[] { pos + Vector3.down * .3f, pos + new Vector3(lean * .15f, forkY * .4f, 0f), pos + new Vector3(lean * .6f, forkY * .75f, lean * .2f), fork },
                new[] { r0 * 1.4f, r0 * 1.05f, r0 * .85f, r0 * .7f }, 7, rng, .06f, false);
            var tips = new List<Vector3>();
            float yaw0 = rng.Value() * Mathf.PI * 2f;
            for (int i = 0; i < 3; i++)
            {
                float yaw = yaw0 + i * Mathf.PI * 2f / 3f + rng.Signed() * .4f, elev = rng.Range(.7f, 1.05f), len = spread * rng.Range(.7f, .95f);
                Vector3 dir = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Sin(yaw) * Mathf.Cos(elev));
                Vector3 mid = fork + dir * (len * .5f), end = fork + dir * len + Vector3.up * (len * .08f);
                Shapes.Tube(bark, new[] { fork, mid, end }, new[] { r0 * .52f, r0 * .34f, r0 * .14f }, 5, rng, .08f, true);
                tips.Add(end);
            }
            float yc = .70f * h, hh = .30f * h;
            for (int k = 0; k < lobes; k++)
            {
                float az = rng.Value() * Mathf.PI * 2f, rho = k == 0 ? spread * .1f : spread * rng.Range(.34f, .8f);
                float f = k == 0 ? .86f : Mathf.Clamp01(rng.Range(.12f, .74f) + (1f - rho / spread) * .18f);
                Vector3 c = new Vector3(fork.x + Mathf.Cos(az) * rho, pos.y + yc - hh + f * 2f * hh, fork.z + Mathf.Sin(az) * rho);
                float rl = spread * (k == 0 ? .58f : rng.Range(.42f, .6f)), ry = rl * rng.Range(.8f, 1.02f);
                Mat m = CrownMaterial(f, rng, Mat.OakTop, Mat.OakMid, Mat.OakDark);
                Shapes.Lobe(layers.Get(m, shadows), c, new Vector3(rl, ry, rl), Basis.Euler(rng.Value() * 6.28f, rng.Signed() * .3f, rng.Signed() * .3f), rng, .2f, subdivisions, .7f);
            }
            foreach (Vector3 tip in tips)
            {
                float rl = spread * rng.Range(.2f, .3f);
                Shapes.Lobe(layers.Get(Mat.OakMid, shadows), tip + new Vector3(0f, rl * .5f, 0f), new Vector3(rl, rl * .75f, rl), Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .22f, 0, .6f);
            }
        }

        /// <summary>
        /// Slimmer, lighter tree: a clean trunk that forks into two rising limbs, and an upright oval crown whose lobes start where the limbs end
        /// (a small lobe sits on every limb tip), so the crown is carried by the limbs and never floats above them.
        /// </summary>
        static void Beech(LayerSet layers, Vector3 pos, float h, float spread, Rng rng, bool shadows, int subdivisions)
        {
            var bark = layers.Get(Mat.Trunk, shadows);
            float r0 = Mathf.Max(.1f, .031f * h);
            float lean = rng.Signed() * .25f * h / 9f, forkY = .4f * h;
            Vector3 fork = pos + new Vector3(lean, forkY, lean * .2f);
            Shapes.Tube(bark, new[] { pos + Vector3.down * .3f, pos + new Vector3(lean * .1f, forkY * .35f, 0f), pos + new Vector3(lean * .55f, forkY * .72f, lean * .1f), fork },
                new[] { r0 * 1.3f, r0, r0 * .82f, r0 * .64f }, 6, rng, .05f, false);
            var tips = new List<Vector3>();
            float yaw0 = rng.Value() * Mathf.PI * 2f;
            for (int i = 0; i < 2; i++)
            {
                float yaw = yaw0 + i * Mathf.PI + rng.Signed() * .5f, elev = rng.Range(.8f, 1.05f), len = spread * rng.Range(.9f, 1.15f);
                Vector3 dir = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Sin(yaw) * Mathf.Cos(elev));
                Vector3 end = fork + dir * len;
                Shapes.Tube(bark, new[] { fork, fork + dir * (len * .5f), end }, new[] { r0 * .5f, r0 * .32f, r0 * .14f }, 5, rng, .08f, true);
                tips.Add(end);
            }
            float yc = .72f * h, hh = .28f * h;
            for (int k = 0; k < 8; k++)
            {
                float az = rng.Value() * Mathf.PI * 2f, f = k == 0 ? .92f : rng.Range(.12f, .8f);
                float rho = k == 0 ? 0f : spread * rng.Range(.12f, .6f) * (1f - .3f * f);
                Vector3 c = new Vector3(fork.x + Mathf.Cos(az) * rho, pos.y + yc - hh + f * 2f * hh, fork.z + Mathf.Sin(az) * rho);
                float rl = spread * (k == 0 ? .56f : rng.Range(.5f, .7f)) * (1f - .2f * f), ry = rl * rng.Range(.8f, 1.05f);
                Shapes.Lobe(layers.Get(CrownMaterial(f, rng, Mat.BeechTop, Mat.BeechMid, Mat.BeechDark), shadows), c, new Vector3(rl, ry, rl),
                    Basis.Euler(rng.Value() * 6.28f, rng.Signed() * .3f, rng.Signed() * .3f), rng, .2f, subdivisions, .7f);
            }
            foreach (Vector3 tip in tips)
            {
                float rl = spread * rng.Range(.3f, .4f);
                Shapes.Lobe(layers.Get(Mat.BeechMid, shadows), tip + new Vector3(0f, rl * .3f, 0f), new Vector3(rl, rl * .85f, rl), Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .22f, 0, .65f);
            }
        }

        /// <summary>Spruce: a bare lower trunk and ragged, uneven tiers (see <see cref="Shapes.Tier"/>); never a smooth cone.</summary>
        static void Spruce(LayerSet layers, Vector3 pos, float h, float spread, Rng rng, bool shadows, int tiers, int sides)
        {
            Shapes.Tube(layers.Get(Mat.Trunk, shadows), new[] { pos + Vector3.down * .3f, pos + Vector3.up * (.5f * h), pos + Vector3.up * (.84f * h) },
                new[] { Mathf.Max(.1f, .028f * h) * 1.3f, Mathf.Max(.1f, .028f * h) * .8f, .05f }, 6, rng, .05f, false);
            for (int i = 0; i < tiers; i++)
            {
                float t = tiers > 1 ? (float)i / (tiers - 1) : 0f;
                float y = h * (.15f + .62f * t), radius = spread * Mathf.Lerp(1f, .24f, Mathf.Pow(t, .9f)), tierHeight = h * (.23f + .06f * (1f - t));
                Mat m = t < .34f ? Mat.SpruceDark : t < .7f ? Mat.SpruceMid : Mat.SpruceTop;
                if (rng.Value() < .15f) m = m == Mat.SpruceDark ? Mat.SpruceMid : Mat.SpruceDark;
                Vector3 c = pos + new Vector3(rng.Signed() * radius * .12f, y, rng.Signed() * radius * .12f);
                Shapes.Tier(layers.Get(m, shadows), c, radius, tierHeight, sides, rng, h * .03f);
            }
        }

        /// <summary>Bush at the foot of a tree group or along the forest edge: two or three flat, rough lobes.</summary>
        static void Shrub(LayerSet layers, Vector3 pos, float size, Rng rng, bool shadows)
        {
            int n = 2 + rng.Int(2);
            for (int i = 0; i < n; i++)
            {
                float a = rng.Value() * 6.28f, d = i == 0 ? 0f : size * rng.Range(.35f, .6f), rl = size * (i == 0 ? rng.Range(.55f, .7f) : rng.Range(.35f, .5f));
                Vector3 c = pos + new Vector3(Mathf.Cos(a) * d, rl * .5f, Mathf.Sin(a) * d);
                Mat m = i == 0 ? Mat.ShrubMid : (rng.Value() < .5f ? Mat.ShrubDark : Mat.ShrubMid);
                Shapes.Lobe(layers.Get(m, shadows), c, new Vector3(rl, rl * .72f, rl), Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .25f, 0, .45f);
            }
        }

        /// <summary>Distant tree: no trunk, two or three stacked lobes in the layer's haze colour.</summary>
        static void Blob(LayerSet layers, Vector3 pos, float h, float spread, Rng rng, Mat haze)
        {
            int n = 2 + rng.Int(2);
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0f, rl = spread * (1f - .3f * t) * rng.Range(.85f, 1.1f);
                Vector3 c = pos + new Vector3(rng.Signed() * spread * .15f, h * (.42f + .26f * t), rng.Signed() * spread * .15f);
                Shapes.Lobe(layers.Get(haze, false), c, new Vector3(rl, rl * rng.Range(.85f, 1.15f), rl), Basis.Euler(rng.Value() * 6.28f, 0f, 0f), rng, .22f, 0, .6f);
            }
        }

        /// <summary>Distant spruce: three small tiers in the layer's haze colour.</summary>
        static void BlobSpruce(LayerSet layers, Vector3 pos, float h, float spread, Rng rng, Mat haze)
        {
            for (int i = 0; i < 3; i++)
            {
                float t = i / 2f;
                Shapes.Tier(layers.Get(haze, false), pos + new Vector3(0f, h * (.12f + .5f * t), 0f), spread * Mathf.Lerp(1f, .3f, t), h * .38f, 5, rng, h * .02f);
            }
        }
    }
}
