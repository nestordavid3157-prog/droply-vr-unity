using System.Collections.Generic;
using UnityEngine;

namespace Droply.Landscape
{
    /// <summary>
    /// The composition rules of the repair as measurable checks (open foreground, clear path, one grouped character tree group, no tree rows,
    /// open sight corridors, detail falling with distance, relief, calm palette, budget). Run by the editor's "Validate landscape" and by
    /// Tools/CompositionCheck; what cannot be measured (does it feel calm and natural in the headset?) is not claimed here.
    /// </summary>
    public static class LandscapeChecks
    {
        public const int TriangleBudget = 80000;
        public const int ShadowTriangleBudget = 26000;

        public static List<string> Run(SceneData d)
        {
            var violations = new List<string>();
            PathStaysClear(d, violations);
            ForegroundIsOpen(d, violations);
            HeroGroupIsAGroup(d, violations);
            NoTreeRows(d, violations);
            SightCorridors(d, violations);
            DetailFallsWithDistance(d, violations);
            CountsStayLow(d, violations);
            GroundHasRelief(d, violations);
            PaletteIsCalm(d, violations);
            WithinBudget(d, violations);
            NothingProhibited(d, violations);
            WalkAreaHoldsUp(d, violations);
            return violations;
        }

        /// <summary>The landscape contains no buildings, props, signs, text or devices (also checked by the editor's "Validate landscape" on the scene hierarchy).</summary>
        public static readonly string[] Prohibited = { "House", "Building", "Architecture", "Furniture", "Device", "Sign", "Text", "Logo", "Canvas", "Palm" };

        static void NothingProhibited(SceneData d, List<string> v)
        {
            foreach (var layer in d.Layers)
                foreach (string term in Prohibited)
                    if (layer.Name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0) v.Add("Layer \"" + layer.Name + "\" contains prohibited content (" + term + ").");
        }

        static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        static void PathStaysClear(SceneData d, List<string> v)
        {
            foreach (var p in d.GrassClumps)
                if (d.Path.EdgeDistance(p.x, p.z) < 1.0f) v.Add("Grass tuft at " + At(p) + " is on or at the path edge.");
            foreach (var p in d.Flowers)
                if (d.Path.EdgeDistance(p.x, p.z) < 1.8f) v.Add("Flower at " + At(p) + " is too close to the path.");
            foreach (var p in d.EdgeTufts)
                if (d.Path.EdgeDistance(p.x, p.z) < .45f) v.Add("Path-edge tuft at " + At(p) + " is on the path.");
            foreach (var p in d.Pebbles)
                if (d.Path.EdgeDistance(p.x, p.z) < .2f) v.Add("Pebble at " + At(p) + " lies on the path.");
            foreach (var r in d.Rocks)
                if (d.Path.EdgeDistance(r.Position.x, r.Position.z) - r.Radius < 1.0f) v.Add("Stone at " + At(r.Position) + " reaches the path.");
            foreach (var t in d.Trees)
            {
                float edge = d.Path.EdgeDistance(t.Position.x, t.Position.z);
                if (t.Kind == TreeKind.Shrub ? edge < 1.5f : edge - .8f * t.Spread < .8f)
                    v.Add(t.Kind + " (" + t.Tier + ") at " + At(t.Position) + " covers the path.");
            }
        }

        static void ForegroundIsOpen(SceneData d, List<string> v)
        {
            Vector3 origin = Vector3.zero;
            foreach (var p in d.GrassClumps) if (Flat(p, origin) < 6f) v.Add("Grass tuft within 6 m of the viewer at " + At(p));
            foreach (var p in d.Flowers) if (Flat(p, origin) < 8f) v.Add("Flower within 8 m of the viewer at " + At(p));
            foreach (var p in d.EdgeTufts) if (Flat(p, origin) < 7f) v.Add("Path-edge tuft within 7 m of the viewer at " + At(p));
            foreach (var t in d.Trees) if (Flat(t.Position, origin) < 24f) v.Add(t.Kind + " within 24 m of the viewer at " + At(t.Position));
            int near = 0;
            foreach (var p in d.GrassClumps) if (Flat(p, origin) < 20f) near++;
            if (near > 18) v.Add("Too many grass tufts in the first 20 m: " + near);
        }

        static void HeroGroupIsAGroup(SceneData d, List<string> v)
        {
            var hero = new List<TreeRecord>();
            foreach (var t in d.Trees) if (t.Tier == TreeTier.Hero && t.Kind != TreeKind.Shrub) hero.Add(t);
            if (hero.Count < 3 || hero.Count > 7) { v.Add("The character group has " + hero.Count + " trees, expected 3 to 7."); return; }
            float mx = 0f, mz = 0f, hMin = float.MaxValue, hMax = 0f;
            foreach (var t in hero) { mx += t.Position.x; mz += t.Position.z; hMin = Mathf.Min(hMin, t.Height); hMax = Mathf.Max(hMax, t.Height); }
            mx /= hero.Count; mz /= hero.Count;
            float sxx = 0f, szz = 0f, sxz = 0f;
            foreach (var t in hero) { float dx = t.Position.x - mx, dz = t.Position.z - mz; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
            float tr = sxx + szz, det = sxx * szz - sxz * sxz, disc = Mathf.Sqrt(Mathf.Max(0f, tr * tr / 4f - det));
            float big = tr / 2f + disc, small = tr / 2f - disc;
            if (small / Mathf.Max(big, 1e-6f) < .12f) v.Add("The character group stands in a line (eigenvalue ratio " + (small / big).ToString("0.00") + ").");
            if (hMax / hMin < 1.5f) v.Add("The trees of the character group are too similar in height.");
            float distance = Mathf.Sqrt(mx * mx + mz * mz);
            if (distance < 28f || distance > 62f) v.Add("The character group is " + distance.ToString("0") + " m away; it belongs in the middle ground (28 to 62 m).");
            foreach (var a in hero)
            {
                float nearest = float.MaxValue;
                foreach (var b in hero) if (!b.Position.Equals(a.Position)) nearest = Mathf.Min(nearest, Flat(a.Position, b.Position) / (a.Spread + b.Spread));
                if (nearest < .55f) v.Add("A tree of the character group stacks into its neighbour (crown distance ratio " + nearest.ToString("0.00") + ").");
                if (nearest > 1.1f) v.Add("A tree of the character group stands apart (crown distance ratio " + nearest.ToString("0.00") + ").");
            }
        }

        /// <summary>
        /// The longest "row" through the point <paramref name="c"/>: trees at near-equal spacing (3 to 22 m) on a near-straight line (each within 15 % of the step).
        /// Four or more is a row. The forest clumps use this as a placement rule, so they cannot form rows; the check below verifies the finished scene.
        /// </summary>
        public static int RowThrough(List<Vector2> others, Vector2 c)
        {
            int best = 1;
            foreach (var j in others)
            {
                Vector2 dir = j - c;
                float step = Mathf.Sqrt(dir.x * dir.x + dir.y * dir.y);
                if (step < 3f || step > 22f) continue;
                int chain = 2;
                Vector2 forward = j;
                while (Near(others, forward + dir, step * .15f, out forward)) chain++;
                Vector2 backward = c;
                while (Near(others, backward - dir, step * .15f, out backward)) chain++;
                best = Mathf.Max(best, chain);
            }
            return best;
        }

        static bool Near(List<Vector2> points, Vector2 target, float tolerance, out Vector2 found)
        {
            foreach (var p in points)
                if (Mathf.Abs(p.x - target.x) < tolerance && Mathf.Abs(p.y - target.y) < tolerance) { found = p; return true; }
            found = target;
            return false;
        }

        static void NoTreeRows(SceneData d, List<string> v)
        {
            var points = new List<Vector2>();
            foreach (var t in d.Trees) if (t.Kind != TreeKind.Shrub) points.Add(new Vector2(t.Position.x, t.Position.z));
            foreach (var p in points)
            {
                var others = new List<Vector2>(points);
                others.Remove(p);
                int chain = RowThrough(others, p);
                if (chain >= 4) { v.Add("Tree row: " + chain + " trees on a line through (" + p.x.ToString("0.0") + ", " + p.y.ToString("0.0") + ")."); return; }
            }
        }

        /// <summary>
        /// Which view directions are free of trunks within 150 m? Rays from the eye in 1 degree steps over +-60 degrees ("clear lines of sight, no wall").
        /// With <paramref name="includeSimplified"/> the distant simplified groups count too (a carpet of trees fails even when the near ones are sparse).
        /// </summary>
        public static void Corridors(SceneData d, bool includeSimplified, out float openShare, out float widestDegrees, out int corridors)
        {
            int open = 0, total = 0, run = 0, widest = 0, count = 0;
            bool previousOpen = false;
            for (int deg = -60; deg <= 60; deg++)
            {
                float az = deg * Mathf.PI / 180f, rx = Mathf.Sin(az), rz = Mathf.Cos(az);
                bool blocked = false;
                foreach (var t in d.Trees)
                {
                    if (t.Kind == TreeKind.Shrub || t.Tier == TreeTier.Far || (t.Tier == TreeTier.Mid && !includeSimplified)) continue;
                    float along = t.Position.x * rx + t.Position.z * rz;
                    if (along < 15f || along > 150f) continue;
                    float across = Mathf.Abs(t.Position.x * rz - t.Position.z * rx);
                    if (across < .5f * t.Spread) { blocked = true; break; }
                }
                total++;
                if (!blocked) open++;
                if (!blocked) { run++; if (!previousOpen) count++; widest = Mathf.Max(widest, run); } else run = 0;
                previousOpen = !blocked;
            }
            openShare = (float)open / total; widestDegrees = widest; corridors = count;
        }

        static void SightCorridors(SceneData d, List<string> v)
        {
            float share, widest; int corridors;
            Corridors(d, false, out share, out widest, out corridors);
            if (share < .6f) v.Add("Only " + (share * 100f).ToString("0") + " % of the view directions are free of trunks (at least 60 %).");
            if (widest < 18f) v.Add("The widest open sight corridor is only " + widest + " degrees (at least 18).");
            if (corridors < 3) v.Add("Only " + corridors + " separate open corridors (at least 3).");
            float all, allWidest; int allCorridors;
            Corridors(d, true, out all, out allWidest, out allCorridors);
            if (all < .4f) v.Add("Counting the distant groups too, only " + (all * 100f).ToString("0") + " % of the view directions are free (at least 40 %): a carpet of trees.");
        }

        public static float AverageTriangles(SceneData d, TreeTier tier)
        {
            float sum = 0f; int n = 0;
            foreach (var t in d.Trees) if (t.Tier == tier && t.Kind != TreeKind.Shrub) { sum += t.Triangles; n++; }
            return n > 0 ? sum / n : 0f;
        }

        static void DetailFallsWithDistance(SceneData d, List<string> v)
        {
            float hero = AverageTriangles(d, TreeTier.Hero), edge = AverageTriangles(d, TreeTier.Edge), mid = AverageTriangles(d, TreeTier.Mid);
            if (!(hero > edge && edge > mid)) v.Add("Detail does not fall with distance: hero " + hero.ToString("0") + ", edge " + edge.ToString("0") + ", mid " + mid.ToString("0") + " triangles per tree.");
        }

        static void CountsStayLow(SceneData d, List<string> v)
        {
            if (d.IslandCenters.Count < 5 || d.IslandCenters.Count > 10) v.Add("Flower islands: " + d.IslandCenters.Count + ", expected 5 to 10.");
            if (d.Flowers.Count > 260) v.Add("Too many flowers: " + d.Flowers.Count);
            if (d.GrassClumps.Count > 110) v.Add("Too many grass tufts: " + d.GrassClumps.Count);
            if (d.EdgeTufts.Count > 30) v.Add("Too many path-edge tufts: " + d.EdgeTufts.Count);
            if (d.FleckCount > 1500) v.Add("Too many grass flecks: " + d.FleckCount);
            if (d.Pebbles.Count > 30) v.Add("Too many pebbles: " + d.Pebbles.Count);
            if (d.Rocks.Count < 3 || d.Rocks.Count > 8) v.Add("Stones: " + d.Rocks.Count + ", expected 3 to 8.");
            float smallest = float.MaxValue, largest = 0f;
            foreach (var r in d.Rocks) { smallest = Mathf.Min(smallest, r.Radius); largest = Mathf.Max(largest, r.Radius); }
            if (d.Rocks.Count > 0 && largest / smallest < 2f) v.Add("The stones are too alike in size.");
        }

        public static void Relief(SceneData d, out float maxSlopeDegrees, out float reliefMetres, out float worstMeshError)
        {
            float maxSlope = 0f, lo = float.MaxValue, hi = float.MinValue, worst = 0f;
            for (float x = -100f; x <= 100f; x += 3f)
                for (float z = -100f; z <= 100f; z += 3f)
                {
                    if (x * x + z * z > 100f * 100f) continue;
                    float h = TerrainModel.Height(x, z);
                    float sx = (TerrainModel.Height(x + 3f, z) - h) / 3f, sz = (TerrainModel.Height(x, z + 3f) - h) / 3f;
                    maxSlope = Mathf.Max(maxSlope, Mathf.Atan(Mathf.Sqrt(sx * sx + sz * sz)) * 180f / Mathf.PI);
                    if (x * x + z * z <= 90f * 90f) { lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
                    if (x * x + z * z <= 80f * 80f) worst = Mathf.Max(worst, Mathf.Abs(d.Terrain.SurfaceY(x, z) - h));
                }
            maxSlopeDegrees = maxSlope; reliefMetres = hi - lo; worstMeshError = worst;
        }

        static void GroundHasRelief(SceneData d, List<string> v)
        {
            float slope, relief, error;
            Relief(d, out slope, out relief, out error);
            if (relief < 2.5f) v.Add("The ground is too flat: " + relief.ToString("0.0") + " m relief within 90 m (at least 2.5 m).");
            if (slope > 12f) v.Add("The ground is too steep: " + slope.ToString("0.0") + " degrees (at most 12).");
            if (error > .12f) v.Add("The terrain mesh deviates up to " + error.ToString("0.00") + " m from the height function.");
            if (Mathf.Abs(TerrainModel.Height(0f, 0f)) > .05f) v.Add("The standing spot is not level with the tracking origin.");
            GroundIsNotOneGreen(v);
        }

        static float Luminance(Vector3 linear) { return .2126f * linear.x + .7152f * linear.y + .0722f * linear.z; }

        /// <summary>The meadow colour varies softly (cool shade, fresh green, sunlit and dry patches): not one flat green, not a patchwork either.</summary>
        static void GroundIsNotOneGreen(List<string> v)
        {
            var values = new List<float>();
            float hueLow = 1f, hueHigh = 0f;
            for (float x = -70f; x <= 70f; x += 5f)
                for (float z = -70f; z <= 70f; z += 5f)
                {
                    Vector3 c = Look.Ground(x, z);
                    values.Add(Luminance(c));
                    float warmth = c.x / Mathf.Max(c.y, 1e-4f);
                    hueLow = Mathf.Min(hueLow, warmth); hueHigh = Mathf.Max(hueHigh, warmth);
                }
            values.Sort();
            float p10 = values[values.Count / 10], p90 = values[values.Count * 9 / 10], median = values[values.Count / 2];
            float spread = (p90 - p10) / median;
            if (spread < .06f) v.Add("The meadow is one flat colour (brightness spread " + spread.ToString("0.00") + ", at least 0.06).");
            if (spread > .45f) v.Add("The meadow is a patchwork (brightness spread " + spread.ToString("0.00") + ", at most 0.45).");
            if (hueHigh - hueLow < .12f) v.Add("The meadow has no warm and cool patches (red-to-green ratio range " + (hueHigh - hueLow).ToString("0.00") + ", at least 0.12).");
        }

        public static float Saturation(Color32 c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 0f ? 0f : (max - min) / max;
        }

        static void PaletteIsCalm(SceneData d, List<string> v)
        {
            foreach (var layer in d.Layers)
            {
                // leaves, grass, ground, bark and stone stay muted; the small flower heads and their golden eyes may be brighter (the concept image has them clear)
                float s = Saturation(Palette.Color(layer.Material)), limit = Palette.Kind(layer.Material) == SurfaceKind.Petal ? .8f : .62f;
                if (s > limit) v.Add("Material " + layer.Material + " is too saturated (" + s.ToString("0.00") + ", at most " + limit.ToString("0.00") + ").");
            }
        }

        static void WithinBudget(SceneData d, List<string> v)
        {
            if (d.TriangleCount > TriangleBudget) v.Add("Triangles " + d.TriangleCount + " exceed the budget " + TriangleBudget + ".");
            int shadow = 0;
            foreach (var layer in d.Layers) if (layer.CastShadows) shadow += layer.Mesh.TriangleCount;
            if (shadow > ShadowTriangleBudget) v.Add("Shadow-casting triangles " + shadow + " exceed " + ShadowTriangleBudget + ".");
            if (d.Layers.Count > 60) v.Add("Too many draw layers: " + d.Layers.Count);
        }

        // ---- walking ---------------------------------------------------------------------------------------------------------------------

        /// <summary>How close walking may bring the viewer to the simplified layers: they are built to be seen from far away (rule 14: simplified groups 40-80 m, silhouettes 80-150 m).</summary>
        public const float MidTreeMinDistance = 35f, ForestLineMinDistance = 80f;
        /// <summary>Trees and bushes closer than this to walkable ground are built at full detail (rule 23: near = full low-poly geometry).</summary>
        public const float FullDetailDistance = 15f;
        /// <summary>The path must stay walkable for at least this far from the start.</summary>
        public const float MinWalkablePath = 55f;

        public struct WalkStats
        {
            public float AreaSquareMetres, PathMetres, Reach, FarthestView, NearestMid;
            public int UnreachedCells, CoarseCells;
            public Vector2 NearestMidAt, CoarseAt;
        }

        /// <summary>Samples the walk area on a 1 m grid: its size, how far the path stays walkable, how far it reaches, what can be seen from it, and whether it is all connected.</summary>
        public static WalkStats MeasureWalk(SceneData d)
        {
            var w = d.Walk;
            var st = new WalkStats { NearestMid = float.MaxValue };
            float s = 0f;
            for (; s <= d.Path.Length; s += .5f)
            {
                Vector3 p = d.Path.PointAt(s);
                if (w.Distance(new Vector2(p.x, p.z)) > -.6f) break;
            }
            st.PathMetres = s;

            // farthest vertex from the start of anything that can be seen: not the sky (it follows the viewer), not the ground behind the last hills (hidden)
            float farthest = 0f;
            foreach (var layer in d.Layers)
            {
                if (Palette.BakedOnly(layer.Material)) continue;
                bool ground = layer.Material == Mat.GroundBase;
                foreach (var v in layer.Mesh.Vertices)
                    if (!ground || v.x * v.x + v.z * v.z <= LandscapeBuilder.OutermostHills * LandscapeBuilder.OutermostHills) farthest = Mathf.Max(farthest, v.magnitude);
            }

            int x0 = Mathf.FloorToInt(w.Min.x), z0 = Mathf.FloorToInt(w.Min.y), nx = Mathf.CeilToInt(w.Max.x) - x0 + 1, nz = Mathf.CeilToInt(w.Max.y) - z0 + 1;
            var walkable = new bool[nx * nz];
            int cells = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(x0 + i, z0 + j);
                    if (!w.Contains(p)) continue;
                    walkable[j * nx + i] = true;
                    cells++;
                    if (p.x < TerrainModel.DenseMinX || p.x > TerrainModel.DenseMaxX || p.y < TerrainModel.DenseMinZ || p.y > TerrainModel.DenseMaxZ) { st.CoarseCells++; st.CoarseAt = p; }
                    float eye = w.GroundY(p) + 2f;
                    st.Reach = Mathf.Max(st.Reach, Mathf.Sqrt(p.x * p.x + p.y * p.y + eye * eye));
                    foreach (var t in d.Trees)
                    {
                        if (t.Tier != TreeTier.Mid && t.Tier != TreeTier.Far) continue;
                        float dx = t.Position.x - p.x, dz = t.Position.z - p.y, dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist < st.NearestMid) { st.NearestMid = dist; st.NearestMidAt = new Vector2(t.Position.x, t.Position.z); }
                    }
                }
            st.AreaSquareMetres = cells;
            st.FarthestView = st.Reach + farthest;

            // flood fill from the start: every walkable cell must be reachable
            var reached = new bool[walkable.Length];
            var queue = new System.Collections.Generic.Queue<int>();
            int start = (0 - z0) * nx + (0 - x0);
            if (start >= 0 && start < walkable.Length && walkable[start]) { reached[start] = true; queue.Enqueue(start); }
            int count = 0;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                count++;
                int ci = c % nx, cj = c / nx;
                for (int k = 0; k < 4; k++)
                {
                    int ni = ci + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = cj + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz) continue;
                    int n = nj * nx + ni;
                    if (walkable[n] && !reached[n]) { reached[n] = true; queue.Enqueue(n); }
                }
            }
            st.UnreachedCells = cells - count;
            return st;
        }

        /// <summary>
        /// Walking must not break the composition: the start and the path are walkable, the area is connected, stays on the fine terrain grid,
        /// keeps the simplified groups and the forest line at the distance they are built for, and nothing visible ends up behind the far plane.
        /// </summary>
        static void WalkAreaHoldsUp(SceneData d, List<string> v)
        {
            if (d.Walk == null) return;
            if (d.Walk.Distance(new Vector2(0f, 0f)) > -1f) v.Add("The standing spot is not inside the walk area (at least 1 m from its edge).");
            WalkStats st = MeasureWalk(d);
            if (st.PathMetres < MinWalkablePath) v.Add("The walk area follows the path for only " + st.PathMetres.ToString("0") + " m (at least " + MinWalkablePath + " m).");
            if (st.UnreachedCells > 0) v.Add(st.UnreachedCells + " m² of the walk area cannot be reached from the start.");
            if (st.CoarseCells > 0) v.Add("Walkable ground at (" + st.CoarseAt.x + ", " + st.CoarseAt.y + ") lies outside the fine terrain grid.");
            var reach = new WalkArea(d.Terrain, Plan.WalkOutline, new List<WalkArea.Disc>());
            foreach (var t in d.Trees)
            {
                if (t.FullDetail || t.Tier == TreeTier.Mid || t.Tier == TreeTier.Far) continue;
                if (t.Kind == TreeKind.Shrub && t.Spread > 1.5f) continue;   // large bushes always have the fine facets
                float gap = reach.Distance(new Vector2(t.Position.x, t.Position.z));
                if (gap < FullDetailDistance) { v.Add(t.Kind + " at " + At(t.Position) + " is " + gap.ToString("0") + " m from walkable ground but not built at full detail."); break; }
            }
            if (st.NearestMid < MidTreeMinDistance)
                v.Add("A simplified tree at (" + st.NearestMidAt.x.ToString("0") + ", " + st.NearestMidAt.y.ToString("0") + ") is only " + st.NearestMid.ToString("0") + " m from walkable ground (at least " + MidTreeMinDistance + " m).");
            if (LandscapeBuilder.ForestLineRadius - st.Reach < ForestLineMinDistance)
                v.Add("Walkable ground reaches " + st.Reach.ToString("0") + " m from the start: the forest line would come closer than " + ForestLineMinDistance + " m.");
            if (st.FarthestView > WalkArea.FarClip - 2f)
                v.Add("From walkable ground the farthest geometry is up to " + st.FarthestView.ToString("0") + " m away, beyond the far plane (" + WalkArea.FarClip + " m).");
        }

        static string At(Vector3 p) { return "(" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ")"; }
    }
}
