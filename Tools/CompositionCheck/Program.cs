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
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "selftest") return SelfTest.Run();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            SceneData data = LandscapeBuilder.Build();
            long ms = watch.ElapsedMilliseconds;
            bool deterministic = Checksum(data) == Checksum(LandscapeBuilder.Build());
            Console.WriteLine("Same input, same landscape (built twice, identical geometry): " + (deterministic ? "yes" : "NO"));

            Console.WriteLine("Landscape built in " + ms + " ms: " + data.TriangleCount + " triangles in " + data.Layers.Count + " layers.");
            int shadow = 0;
            foreach (var layer in data.Layers) if (layer.CastShadows) shadow += layer.Mesh.TriangleCount;
            Console.WriteLine("  shadow-casting triangles: " + shadow);
            foreach (var layer in data.Layers)
                Console.WriteLine("  " + layer.Name.PadRight(34) + layer.Mesh.TriangleCount.ToString().PadLeft(7) + " triangles" + (layer.CastShadows ? "  shadows" : ""));

            var byTier = new Dictionary<TreeTier, int>();
            foreach (var t in data.Trees) { int n; byTier.TryGetValue(t.Tier, out n); byTier[t.Tier] = n + (t.Kind == TreeKind.Shrub ? 0 : 1); }
            Console.WriteLine("Trees: hero " + Get(byTier, TreeTier.Hero) + ", edge " + Get(byTier, TreeTier.Edge) + ", mid " + Get(byTier, TreeTier.Mid) +
                              "; grass tufts " + data.GrassClumps.Count + ", flowers " + data.Flowers.Count + " on " + data.IslandCenters.Count + " islands, stones " + data.Rocks.Count);
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

            if (args.Length >= 2 && args[0] == "export") { Export(data, args[1]); Console.WriteLine("Exported " + args[1]); }

            var violations = LandscapeChecks.Run(data);
            if (!deterministic) violations.Add("Building the landscape twice gave different geometry.");
            if (violations.Count == 0) { Console.WriteLine("Composition checks: all passed."); return 0; }
            Console.WriteLine("Composition checks: " + violations.Count + " violation(s):");
            foreach (string v in violations) Console.WriteLine("  - " + v);
            return 1;
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

        static void Export(SceneData data, string file)
        {
            var sb = new StringBuilder();
            sb.Append("{\"layers\":[");
            for (int i = 0; i < data.Layers.Count; i++)
            {
                var l = data.Layers[i];
                Color32 c = Palette.Color(l.Material);
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":\"" + l.Name + "\",\"material\":\"" + l.Material + "\",\"color\":[" + c.r + "," + c.g + "," + c.b + "],\"shadows\":" + (l.CastShadows ? "true" : "false") +
                          ",\"double\":" + (Palette.DoubleSided(l.Material) ? "true" : "false") + ",\"unlit\":" + (Palette.Unlit(l.Material) ? "true" : "false") +
                          ",\"positions\":\"" + Base64(l.Mesh.Vertices) + "\",\"normals\":\"" + Base64(l.Mesh.Normals) + "\",\"indices\":\"" + Base64(l.Mesh.Triangles) + "\"}");
            }
            sb.Append("],\"sun\":{\"pitch\":" + Number(Palette.SunPitch) + ",\"yaw\":" + Number(Palette.SunYaw) + ",\"intensity\":" + Number(Palette.SunIntensity) +
                      ",\"color\":[" + Number(Palette.SunColor.r) + "," + Number(Palette.SunColor.g) + "," + Number(Palette.SunColor.b) + "]},");
            sb.Append("\"ambient\":{\"sky\":[" + Number(Palette.AmbientSky.r) + "," + Number(Palette.AmbientSky.g) + "," + Number(Palette.AmbientSky.b) + "],\"equator\":[" +
                      Number(Palette.AmbientEquator.r) + "," + Number(Palette.AmbientEquator.g) + "," + Number(Palette.AmbientEquator.b) + "],\"ground\":[" +
                      Number(Palette.AmbientGround.r) + "," + Number(Palette.AmbientGround.g) + "," + Number(Palette.AmbientGround.b) + "]},");
            Color32 h = Palette.HorizonColor;
            sb.Append("\"horizon\":[" + h.r + "," + h.g + "," + h.b + "],\"skyTint\":[" + Number(Palette.SkyTint.r) + "," + Number(Palette.SkyTint.g) + "," + Number(Palette.SkyTint.b) + "],");
            sb.Append("\"path\":[");
            for (float s = 0f; s <= data.Path.Length; s += 4f)
            {
                Vector3 p = data.Path.PointAt(s);
                if (s > 0f) sb.Append(',');
                sb.Append("[" + Number(p.x) + "," + Number(p.z) + "]");
            }
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
