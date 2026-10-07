using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Droply.Landscape;
using UnityEngine;

namespace Droply.CompositionCheck
{
    /// <summary>
    /// dotnet run --project Tools/CompositionCheck                -> build the landscape, print the numbers, run the composition checks (exit code 1 on a violation)
    /// dotnet run --project Tools/CompositionCheck -- export f.json -> additionally write the scene for Tools/Preview
    /// dotnet run --project Tools/CompositionCheck -- probe 26     -> baked ground colour along the line z = 26 (is the cast shadow where the geometry says it is?)
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "selftest") return SelfTest.Run();
            if (args.Length >= 2 && args[0] == "probe") { Probe(LandscapeBuilder.Build(), float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)); return 0; }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SceneData data = LandscapeBuilder.Build();
            long ms = watch.ElapsedMilliseconds;
            bool deterministic = Checksum(data) == Checksum(LandscapeBuilder.Build());
            Console.WriteLine("Same input, same landscape (built twice, identical geometry): " + (deterministic ? "yes" : "NO"));

            int vertexTotal = 0;
            foreach (var layer in data.Layers) vertexTotal += layer.Mesh.Vertices.Length;
            Console.WriteLine("Landscape built in " + ms + " ms (light bake " + data.BakeMilliseconds + " ms, " + data.OccluderCount + " occluders): " + data.TriangleCount + " triangles, " + vertexTotal +
                              " vertices (" + (vertexTotal * 16 / 1024) + " KiB with position and colour) in " + data.Layers.Count + " layers.");
            int shadow = 0;
            foreach (var layer in data.Layers) if (layer.CastShadows) shadow += layer.Mesh.TriangleCount;
            Console.WriteLine("  shadow-casting triangles: " + shadow);
            foreach (var layer in data.Layers)
                Console.WriteLine("  " + layer.Name.PadRight(34) + layer.Mesh.TriangleCount.ToString().PadLeft(7) + " triangles" + (layer.CastShadows ? "  shadows" : ""));

            var byTier = new Dictionary<TreeTier, int>();
            foreach (var t in data.Trees) { int n; byTier.TryGetValue(t.Tier, out n); byTier[t.Tier] = n + (t.Kind == TreeKind.Shrub ? 0 : 1); }
            Console.WriteLine("Trees: hero " + Get(byTier, TreeTier.Hero) + ", edge " + Get(byTier, TreeTier.Edge) + ", mid " + Get(byTier, TreeTier.Mid) +
                              "; grass tufts " + data.GrassClumps.Count + " (+ " + data.EdgeTufts.Count + " at the path edge, " + data.FleckCount + " flecks), flowers " + data.Flowers.Count + " on " +
                              data.IslandCenters.Count + " islands, stones " + data.Rocks.Count + " (+ " + data.Pebbles.Count + " pebbles)");
            var reachable = new List<string>();
            foreach (var t in data.Trees) if (t.FullDetail && t.Tier != TreeTier.Hero) reachable.Add(t.Kind + " (" + t.Position.x.ToString("0") + ", " + t.Position.z.ToString("0") + ") " + t.Triangles);
            Console.WriteLine("Outside the character group at full detail (the viewer can walk up to them): " + reachable.Count + (reachable.Count > 0 ? ": " + string.Join(", ", reachable) : ""));
            Console.WriteLine("Triangles per tree: hero " + LandscapeChecks.AverageTriangles(data, TreeTier.Hero).ToString("0") + ", edge " +
                              LandscapeChecks.AverageTriangles(data, TreeTier.Edge).ToString("0") + ", mid " + LandscapeChecks.AverageTriangles(data, TreeTier.Mid).ToString("0"));
            float share, widest; int corridors;
            LandscapeChecks.Corridors(data, false, out share, out widest, out corridors);
            float allShare, allWidest; int allCorridors;
            LandscapeChecks.Corridors(data, true, out allShare, out allWidest, out allCorridors);
            float slope, relief, error;
            LandscapeChecks.Relief(data, out slope, out relief, out error);
            Console.WriteLine("Open view directions " + (share * 100f).ToString("0") + " % (" + (allShare * 100f).ToString("0") + " % counting the distant groups), widest corridor " + widest + " deg, " + corridors + " corridors; relief " +
                              relief.ToString("0.0") + " m, steepest " + slope.ToString("0.0") + " deg, mesh error " + error.ToString("0.00") + " m");

            var walk = LandscapeChecks.MeasureWalk(data);
            Console.WriteLine("Walk area " + walk.AreaSquareMetres.ToString("0") + " m² (" + data.Walk.Obstacles.Length + " trunks, bushes and stones kept clear), path walkable for " + walk.PathMetres.ToString("0") +
                              " m, reaches " + walk.Reach.ToString("0") + " m from the start; nearest simplified tree " + walk.NearestMid.ToString("0") + " m, farthest visible geometry " +
                              walk.FarthestView.ToString("0") + " m (far plane " + WalkArea.FarClip + " m)");

            if (args.Length >= 2 && args[0] == "export") { Export(data, args[1]); Console.WriteLine("Exported " + args[1]); }

            var violations = LandscapeChecks.Run(data);
            if (!deterministic) violations.Add("Building the landscape twice gave different geometry.");
            if (violations.Count == 0) { Console.WriteLine("Composition checks: all passed."); return 0; }
            Console.WriteLine("Composition checks: " + violations.Count + " violation(s):");
            foreach (string v in violations) Console.WriteLine("  - " + v);
            return 1;
        }

        /// <summary>Prints the baked ground colour (sRGB) of the ground vertices near the line z = <paramref name="z"/>, from x = -20 to 20.</summary>
        static void Probe(SceneData data, float z)
        {
            MeshLayer ground = data.Layers[0];
            Console.WriteLine("Ground colours near z = " + z + " (x: r g b):");
            var rows = new SortedDictionary<float, Color32>();
            for (int i = 0; i < ground.Mesh.Vertices.Length; i++)
            {
                Vector3 v = ground.Mesh.Vertices[i];
                if (Math.Abs(v.z - z) < .65f && v.x >= -20f && v.x <= 20f) rows[(float)Math.Round(v.x, 2)] = ground.Mesh.Colors[i];
            }
            foreach (var pair in rows) Console.WriteLine("  x " + pair.Key.ToString("0.00").PadLeft(6) + ": " + pair.Value.r.ToString().PadLeft(3) + " " + pair.Value.g.ToString().PadLeft(3) + " " + pair.Value.b.ToString().PadLeft(3));
        }

        /// <summary>Order-sensitive checksum over every vertex and triangle index of every layer.</summary>
        static double Checksum(SceneData data)
        {
            double sum = 0;
            foreach (var layer in data.Layers)
            {
                for (int i = 0; i < layer.Mesh.Vertices.Length; i++)
                {
                    Vector3 v = layer.Mesh.Vertices[i];
                    sum += (v.x * 3.1 + v.y * 5.7 + v.z * 7.3) * ((i % 97) + 1);
                }
                for (int i = 0; i < layer.Mesh.Triangles.Length; i++) sum += layer.Mesh.Triangles[i] * ((i % 89) + 1) * 1e-3;
            }
            return sum;
        }

        static int Get(Dictionary<TreeTier, int> map, TreeTier tier) { int n; return map.TryGetValue(tier, out n) ? n : 0; }

        static string Base64(Vector3[] values)
        {
            var bytes = new byte[values.Length * 12];
            for (int i = 0; i < values.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].x), 0, bytes, i * 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].y), 0, bytes, i * 12 + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(values[i].z), 0, bytes, i * 12 + 8, 4);
            }
            return Convert.ToBase64String(bytes);
        }

        static string Base64(int[] values)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        static string Number(float f) { return f.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture); }

        static string Base64(Color32[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++) { bytes[i * 4] = values[i].r; bytes[i * 4 + 1] = values[i].g; bytes[i * 4 + 2] = values[i].b; bytes[i * 4 + 3] = 255; }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Layers with their baked vertex colours (sRGB bytes); Tools/Preview draws them unlit, which is what the baked pipeline draws on the headset.</summary>
        static void Export(SceneData data, string file)
        {
            var sb = new StringBuilder();
            sb.Append("{\"layers\":[");
            for (int i = 0; i < data.Layers.Count; i++)
            {
                var l = data.Layers[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":\"" + l.Name + "\",\"material\":\"" + l.Material + "\",\"sky\":" + (Palette.BakedOnly(l.Material) ? "true" : "false") +
                          ",\"double\":" + (Palette.DoubleSided(l.Material) ? "true" : "false") +
                          ",\"positions\":\"" + Base64(l.Mesh.Vertices) + "\",\"colors\":\"" + Base64(l.Mesh.Colors) + "\",\"indices\":\"" + Base64(l.Mesh.Triangles) + "\"}");
            }
            Color32 h = Palette.HorizonColor;
            sb.Append("],\"horizon\":[" + h.r + "," + h.g + "," + h.b + "],");
            sb.Append("\"path\":[");
            for (float s = 0f; s <= data.Path.Length; s += 4f)
            {
                Vector3 p = data.Path.PointAt(s);
                if (s > 0f) sb.Append(',');
                sb.Append("[" + Number(p.x) + "," + Number(p.z) + "]");
            }
            sb.Append("],\"walk\":[");
            Vector2[] outline = data.Walk.Outline;
            for (int i = 0; i < outline.Length; i++) sb.Append((i > 0 ? "," : "") + "[" + Number(outline[i].x) + "," + Number(outline[i].y) + "]");
            sb.Append("],\"obstacles\":[");
            WalkArea.Disc[] obstacles = data.Walk.Obstacles;
            for (int i = 0; i < obstacles.Length; i++) sb.Append((i > 0 ? "," : "") + "[" + Number(obstacles[i].Centre.x) + "," + Number(obstacles[i].Centre.y) + "," + Number(obstacles[i].Radius) + "]");
            sb.Append("],\"trees\":[");
            for (int i = 0; i < data.Trees.Count; i++)
            {
                var t = data.Trees[i];
                if (i > 0) sb.Append(',');
                sb.Append("[" + Number(t.Position.x) + "," + Number(t.Position.z) + "," + Number(t.Spread) + ",\"" + t.Tier + "\",\"" + t.Kind + "\"]");
            }
            sb.Append("]}");
            File.WriteAllText(file, sb.ToString());
        }
    }
}
