using System;
using System.Collections.Generic;
using Droply.Landscape;
using UnityEngine;

namespace Droply.CompositionCheck
{
    /// <summary>
    /// Do the checks catch what they are meant to catch? Builds deliberately bad scenes (the old tree rows, a tree gate, grass at the viewer's feet, a tree on the path)
    /// and expects the matching violation. dotnet run --project Tools/CompositionCheck -- selftest
    /// </summary>
    static class SelfTest
    {
        static SceneData Blank()
        {
            return new SceneData { Terrain = new TerrainModel(), Path = new PathModel() };
        }

        static TreeRecord Tree(float x, float z, TreeTier tier, float height = 8f, float spread = 2.8f, TreeKind kind = TreeKind.Oak)
        {
            return new TreeRecord { Position = new Vector3(x, 0f, z), Height = height, Spread = spread, Kind = kind, Tier = tier, Triangles = 100 };
        }

        public static int Run()
        {
            int failed = 0;

            // 1. The old forest: six evenly spaced rows across the view plus scattered trees (what the first landscape generator did).
            var rows = Blank();
            var rng = new Rng(1);
            for (int row = 0; row < 6; row++)
            {
                int count = 13 + row * 2;
                for (int i = 0; i < count; i++)
                    rows.Trees.Add(Tree(-64f + 128f * i / (count - 1) + rng.Range(-4.2f, 4.2f), 49f + row * 15f + rng.Range(-5.5f, 5.5f) + Mathf.Sin(i * 1.7f + row) * 2.1f, TreeTier.Edge, 5f, 2.4f));
            }
            Expect(ref failed, "old forest rows are rejected as a carpet", rows, "carpet of trees");
            Expect(ref failed, "old forest rows are rejected as a wall", rows, "free of trunks");

            // 2. Perfectly regular rows.
            var rowsExact = Blank();
            for (int i = 0; i < 6; i++) rowsExact.Trees.Add(Tree(-25f + i * 10f, 70f, TreeTier.Edge));
            Expect(ref failed, "a straight row of equal spacing is found", rowsExact, "Tree row");

            // 3. The old "character group": two diagonal lines either side of the path.
            var gate = Blank();
            foreach (var p in new[] { new Vector2(-16f, 31f), new Vector2(-12.2f, 35f), new Vector2(-8.5f, 39f), new Vector2(9.8f, 32f), new Vector2(14f, 37f), new Vector2(18f, 42f) })
                gate.Trees.Add(Tree(p.x, p.y, TreeTier.Hero, 6f, 2.6f));
            Expect(ref failed, "a gate of two tree lines is not a group", gate, "stands in a line");

            // 4. Grass and flowers at the viewer's feet, uniformly scattered.
            var meadow = Blank();
            for (int i = 0; i < 175; i++) meadow.GrassClumps.Add(new Vector3(rng.Range(-17f, 17f), 0f, rng.Range(2f, 58f)));
            meadow.Flowers.Add(new Vector3(3f, 0f, 5f));
            Expect(ref failed, "grass in the foreground is rejected", meadow, "Grass tuft within 9 m");
            Expect(ref failed, "too much grass is rejected", meadow, "Too many grass tufts");
            Expect(ref failed, "flowers at the feet are rejected", meadow, "Flower within 12 m");

            // 5. A tree and a stone on the path.
            var onPath = Blank();
            Vector3 p60 = onPath.Path.PointAt(60f);
            onPath.Trees.Add(Tree(p60.x, p60.z, TreeTier.Edge));
            onPath.Rocks.Add(new RockRecord { Position = onPath.Path.PointAt(30f), Radius = 1f });
            Expect(ref failed, "a tree on the path is rejected", onPath, "covers the path");
            Expect(ref failed, "a stone on the path is rejected", onPath, "reaches the path");

            // 6. The finished landscape must pass all of it.
            var real = LandscapeBuilder.Build();
            var violations = LandscapeChecks.Run(real);
            if (violations.Count == 0) Console.WriteLine("  ok    the built landscape passes every check");
            else { failed++; Console.WriteLine("  FAIL  the built landscape has violations: " + string.Join(" | ", violations)); }

            Console.WriteLine(failed == 0 ? "Self test passed." : "Self test: " + failed + " failure(s).");
            return failed == 0 ? 0 : 1;
        }

        static void Expect(ref int failed, string what, SceneData data, string violationContains)
        {
            List<string> violations = LandscapeChecks.Run(data);
            bool found = violations.Exists(v => v.IndexOf(violationContains, StringComparison.Ordinal) >= 0);
            if (!found) failed++;
            Console.WriteLine((found ? "  ok    " : "  FAIL  ") + what);
        }
    }
}
