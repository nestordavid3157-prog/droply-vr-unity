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
            Expect(ref failed, "grass in the foreground is rejected", meadow, "Grass tuft within 6 m");
            Expect(ref failed, "too much grass is rejected", meadow, "Too many grass tufts");
            Expect(ref failed, "flowers at the feet are rejected", meadow, "Flower within 8 m");

            // 5. A tree and a stone on the path.
            var onPath = Blank();
            Vector3 p60 = onPath.Path.PointAt(60f);
            onPath.Trees.Add(Tree(p60.x, p60.z, TreeTier.Edge));
            onPath.Rocks.Add(new RockRecord { Position = onPath.Path.PointAt(30f), Radius = 1f });
            Expect(ref failed, "a tree on the path is rejected", onPath, "covers the path");
            Expect(ref failed, "a stone on the path is rejected", onPath, "reaches the path");
            onPath.EdgeTufts.Add(onPath.Path.PointAt(40f));
            onPath.Pebbles.Add(onPath.Path.PointAt(20f));
            Expect(ref failed, "a path-edge tuft on the path is rejected", onPath, "Path-edge tuft at");
            Expect(ref failed, "a pebble on the path is rejected", onPath, "lies on the path");

            // 5b. Too much of the small stuff.
            var crowded = Blank();
            crowded.FleckCount = 5000;
            for (int i = 0; i < 80; i++) crowded.Pebbles.Add(new Vector3(30f + i, 0f, 30f));
            Expect(ref failed, "a carpet of grass flecks is rejected", crowded, "Too many grass flecks");
            Expect(ref failed, "a heap of pebbles is rejected", crowded, "Too many pebbles");

            // 5c. A walk area reaching a simplified group, and one that leaves the path at once.
            var far = Blank();
            far.Trees.Add(Tree(0f, 20f, TreeTier.Mid, 10f, 3f, TreeKind.Blob));
            far.Walk = new WalkArea(far.Terrain, new[] { new Vector2(-6f, -6f), new Vector2(6f, -6f), new Vector2(6f, 6f), new Vector2(-6f, 6f) }, new List<WalkArea.Disc>());
            Expect(ref failed, "a walk area next to a simplified group is rejected", far, "simplified tree");
            Expect(ref failed, "a walk area that does not follow the path is rejected", far, "follows the path for only");
            var island = Blank();
            island.Walk = new WalkArea(island.Terrain, new[] { new Vector2(-6f, -6f), new Vector2(6f, -6f), new Vector2(6f, 70f), new Vector2(-6f, 70f) },
                new List<WalkArea.Disc> { new WalkArea.Disc { Centre = new Vector2(0f, 30f), Radius = 7f } });
            Expect(ref failed, "walkable ground cut off from the start is found", island, "cannot be reached");

            // 6. Walking: never into a trunk, never out of the area, never a jolt, snap turns one at a time.
            failed += Walking();

            // 7. The light bake on all cores gives exactly the colours of a bake on one thread.
            var serial = LandscapeBuilder.Build(false);
            var real = LandscapeBuilder.Build(true);
            Report(ref failed, "the bake on " + Environment.ProcessorCount + " cores gives exactly the colours of a bake on one (" + serial.BakeMilliseconds + " ms -> " + real.BakeMilliseconds + " ms)",
                SameColours(serial, real));

            // 8. The finished landscape must pass all of it.
            var violations = LandscapeChecks.Run(real);
            if (violations.Count == 0) Console.WriteLine("  ok    the built landscape passes every check");
            else { failed++; Console.WriteLine("  FAIL  the built landscape has violations: " + string.Join(" | ", violations)); }

            Console.WriteLine(failed == 0 ? "Self test passed." : "Self test: " + failed + " failure(s).");
            return failed == 0 ? 0 : 1;
        }

        const float Frame = 1f / 72f;   // the Quest's lowest refresh rate: the longest regular step

        static int Walking()
        {
            int failed = 0;
            var open = new WalkArea(new TerrainModel(), new[] { new Vector2(-20f, -20f), new Vector2(20f, -20f), new Vector2(20f, 40f), new Vector2(-20f, 40f) },
                new List<WalkArea.Disc> { new WalkArea.Disc { Centre = new Vector2(0f, 6f), Radius = .8f } });

            // straight at a trunk: stops in front of it, never inside, and decelerates gently
            var walker = new Walker();
            Vector2 head = new Vector2(0f, 0f), last = new Vector2(0f, 0f);
            float worstInside = float.MinValue, worstJolt = 0f;
            for (int i = 0; i < 72 * 12; i++)
            {
                head += walker.Step(open, head, 0f, new Vector2(0f, 1f), Frame);
                worstInside = Mathf.Max(worstInside, open.Distance(head));
                worstJolt = Mathf.Max(worstJolt, (walker.Velocity - last).magnitude / Frame);
                last = walker.Velocity;
            }
            Report(ref failed, "walking straight at a trunk stops in front of it (closest " + (-worstInside).ToString("0.00") + " m from the body's edge)", worstInside <= 0f && head.y > 3f);
            Report(ref failed, "no jolt: acceleration at most " + Walker.MaxAcceleration.ToString("0.0") + " m/s² (measured " + worstJolt.ToString("0.0") + ")", worstJolt <= Walker.MaxAcceleration * 1.01f);

            // slightly off-centre: glides around the trunk and walks on
            walker = new Walker(); head = new Vector2(.3f, 0f);
            bool inside = false;
            for (int i = 0; i < 72 * 15; i++) { head += walker.Step(open, head, 0f, new Vector2(0f, 1f), Frame); inside |= open.Distance(head) > 0f; }
            Report(ref failed, "walking past a trunk glides around it (" + head.y.ToString("0") + " m further on)", !inside && head.y > 12f);

            // into the edge at an angle and along it for a long time: never outside, no jolt
            walker = new Walker(); head = new Vector2(0f, 10f); last = new Vector2(0f, 0f);
            float outside = float.MinValue; worstJolt = 0f;
            for (int i = 0; i < 72 * 40; i++)
            {
                head += walker.Step(open, head, 60f, new Vector2(0f, 1f), Frame);
                outside = Mathf.Max(outside, open.Distance(head));
                worstJolt = Mathf.Max(worstJolt, (walker.Velocity - last).magnitude / Frame);
                last = walker.Velocity;
            }
            Report(ref failed, "walking into the edge stays inside (" + (-outside).ToString("0.00") + " m from it) and glides along it without a jolt (" + worstJolt.ToString("0.0") + " m/s²)",
                outside <= 0f && worstJolt <= Walker.MaxAcceleration * 1.01f);

            // a long frame (loading) must not become a jump
            walker = new Walker();
            Vector2 jump = walker.Step(open, new Vector2(0f, -10f), 0f, new Vector2(0f, 1f), 2f);
            Report(ref failed, "a two-second frame moves at most a few centimetres", jump.magnitude < .05f);

            // speed: a slow walk at full stick, nothing inside the dead zone
            walker = new Walker();
            for (int i = 0; i < 72 * 2; i++) walker.Step(null, new Vector2(0f, 0f), 0f, new Vector2(.72f, .72f), Frame);
            float top = walker.Velocity.magnitude;
            walker = new Walker();
            for (int i = 0; i < 72; i++) walker.Step(null, new Vector2(0f, 0f), 0f, new Vector2(.1f, 0f), Frame);
            Report(ref failed, "full stick walks at " + top.ToString("0.00") + " m/s, the dead zone does not move", Mathf.Abs(top - Walker.MaxSpeed) < .01f && walker.Velocity.magnitude == 0f);

            // the head's direction: stick forward with the head turned right moves right
            walker = new Walker();
            for (int i = 0; i < 72; i++) walker.Step(null, new Vector2(0f, 0f), 90f, new Vector2(0f, 1f), Frame);
            Report(ref failed, "forward is where the head faces", walker.Velocity.x > 1.1f && Mathf.Abs(walker.Velocity.y) < .01f);

            // snap turns: one per push, the stick has to come back first
            walker = new Walker();
            float[] stick = { 0f, .8f, .9f, .6f, .5f, .2f, .75f, 0f, -1f, -1f };
            float[] want = { 0f, 30f, 0f, 0f, 0f, 0f, 30f, 0f, -30f, 0f };
            bool turnsOk = true;
            for (int i = 0; i < stick.Length; i++) turnsOk &= walker.Turn(stick[i]) == want[i];
            Report(ref failed, "snap turns: 30 degrees per push, holding the stick does not spin", turnsOk);

            // the real landscape: following the path from the start, it stays walkable to the end of the area
            var real = LandscapeBuilder.Build();
            walker = new Walker(); head = new Vector2(0f, 0f);
            float s = 0f, reached = 0f; bool left = false;
            for (int i = 0; i < 72 * 120; i++)
            {
                float d = real.Path.Distance(head.x, head.y, out s);
                Vector3 ahead = real.Path.PointAt(s + 4f);
                float yaw = Mathf.Atan2(ahead.x - head.x, ahead.z - head.y) * 180f / Mathf.PI;
                head += walker.Step(real.Walk, head, yaw, new Vector2(0f, 1f), Frame);
                left |= real.Walk.Distance(head) > 0f;
                reached = Mathf.Max(reached, s);
            }
            Report(ref failed, "walking the path in the real landscape: " + reached.ToString("0") + " m along it, never outside the walk area", !left && reached >= LandscapeChecks.MinWalkablePath);

            // ten minutes of wandering in the real landscape (a new direction every 1 to 4 s, often straight at the edge or a trunk): never outside, never a jolt
            var rng = new Rng(77);
            walker = new Walker(); head = new Vector2(0f, 0f); last = new Vector2(0f, 0f);
            float wanderYaw = 0f, until = 0f, worst = float.MinValue; worstJolt = 0f;
            Vector2 wanderStick = new Vector2(0f, 1f);
            for (int i = 0; i < 72 * 600; i++)
            {
                float t = i * Frame;
                if (t >= until) { wanderYaw = rng.Range(-180f, 180f); float m = rng.Range(.3f, 1f); wanderStick = new Vector2(rng.Signed() * .3f, 1f) * m; until = t + rng.Range(1f, 4f); }
                head += walker.Step(real.Walk, head, wanderYaw, wanderStick, Frame);
                worst = Mathf.Max(worst, real.Walk.Distance(head));
                worstJolt = Mathf.Max(worstJolt, (walker.Velocity - last).magnitude / Frame);
                last = walker.Velocity;
            }
            Report(ref failed, "ten minutes of wandering in the real landscape: never outside (closest " + (-worst).ToString("0.00") + " m), at most " + worstJolt.ToString("0.0") + " m/s²",
                worst <= 0f && worstJolt <= Walker.MaxAcceleration * 1.01f);
            return failed;
        }

        /// <summary>Same layers with the same vertices and, vertex by vertex, the same baked colour.</summary>
        static bool SameColours(SceneData a, SceneData b)
        {
            if (a.Layers.Count != b.Layers.Count) return false;
            for (int l = 0; l < a.Layers.Count; l++)
            {
                MeshData x = a.Layers[l].Mesh, y = b.Layers[l].Mesh;
                if (x.Vertices.Length != y.Vertices.Length || x.Colors == null || y.Colors == null || x.Colors.Length != y.Colors.Length) return false;
                for (int i = 0; i < x.Colors.Length; i++)
                {
                    Color32 p = x.Colors[i], q = y.Colors[i];
                    if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a || x.Vertices[i].x != y.Vertices[i].x || x.Vertices[i].y != y.Vertices[i].y || x.Vertices[i].z != y.Vertices[i].z) return false;
                }
            }
            return true;
        }

        static void Report(ref int failed, string what, bool ok)
        {
            if (!ok) failed++;
            Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + what);
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
