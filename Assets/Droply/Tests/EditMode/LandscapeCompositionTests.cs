using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Droply.Landscape.Tests
{
    /// <summary>
    /// EditMode tests (Window > General > Test Runner > EditMode > Run All). They check inside Unity's own runtime what Tools/CompositionCheck checks
    /// outside it: the composition rules, that the same seed gives the same landscape, that the light bake on all cores gives the colours of a bake on one,
    /// and the walking rules. They also check the Unity side, which only Unity can run: that the generator creates the landscape objects.
    /// </summary>
    public sealed class LandscapeCompositionTests
    {
        /// <summary>The Quest's lowest refresh rate: the longest regular step.</summary>
        const float Frame = 1f / 72f;

        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void BuiltLandscapePassesEveryCompositionCheck()
        {
            List<string> violations = LandscapeChecks.Run(LandscapeBuilder.Build());
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void SameSeedGivesTheSameLandscape()
        {
            AssertSame(LandscapeBuilder.Build(), LandscapeBuilder.Build());
        }

        [Test]
        public void BakeOnAllCoresGivesTheColoursOfABakeOnOne()
        {
            AssertSame(LandscapeBuilder.Build(false), LandscapeBuilder.Build(true));
        }

        [Test]
        public void GeneratorCreatesTheLandscapeAndNothingForbidden()
        {
            root = new GameObject("Test landscape");
            var generator = root.AddComponent<LandscapeGenerator>();
            generator.GenerateLandscape();
            Transform generated = root.transform.Find("Generated Landscape");
            Assert.That(generated, Is.Not.Null, "no content root");
            Assert.That(generated.Find("Ground"), Is.Not.Null, "no ground");
            Assert.That(generated.Find("Sand path"), Is.Not.Null, "no path");
            Assert.That(generator.WalkArea, Is.Not.Null, "no walk area for walking");
            string[] forbidden = { "house", "building", "architecture", "furniture", "device", "sign", "text", "logo", "canvas" };
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                foreach (string term in forbidden)
                    Assert.That(child.name.IndexOf(term, StringComparison.OrdinalIgnoreCase), Is.LessThan(0), "forbidden world object: " + child.name);
        }

        [Test]
        public void WanderingNeverLeavesTheWalkAreaAndNeverJolts()
        {
            SceneData data = LandscapeBuilder.Build();
            var rng = new Rng(77);
            var walker = new Walker();
            Vector2 head = new Vector2(0f, 0f), last = new Vector2(0f, 0f), stick = new Vector2(0f, 1f);
            float yaw = 0f, until = 0f, worst = float.MinValue, worstJolt = 0f;
            for (int i = 0; i < 72 * 120; i++)
            {
                float t = i * Frame;
                if (t >= until) { yaw = rng.Range(-180f, 180f); stick = new Vector2(rng.Signed() * .3f, 1f) * rng.Range(.3f, 1f); until = t + rng.Range(1f, 4f); }
                head += walker.Step(data.Walk, head, yaw, stick, Frame);
                worst = Mathf.Max(worst, data.Walk.Distance(head));
                worstJolt = Mathf.Max(worstJolt, (walker.Velocity - last).magnitude / Frame);
                last = walker.Velocity;
            }
            Assert.That(worst, Is.LessThanOrEqualTo(0f), "left the walk area");
            Assert.That(worstJolt, Is.LessThanOrEqualTo(Walker.MaxAcceleration * 1.01f), "acceleration in m/s²");
        }

        [Test]
        public void SnapTurnTurnsOncePerPush()
        {
            var walker = new Walker();
            float[] stick = { 0f, .8f, .9f, .6f, .5f, .2f, .75f, 0f, -1f, -1f };
            float[] want = { 0f, 30f, 0f, 0f, 0f, 0f, 30f, 0f, -30f, 0f };
            for (int i = 0; i < stick.Length; i++) Assert.That(walker.Turn(stick[i]), Is.EqualTo(want[i]), "step " + i);
        }

        static void AssertSame(SceneData a, SceneData b)
        {
            Assert.That(b.Layers.Count, Is.EqualTo(a.Layers.Count), "layers");
            for (int l = 0; l < a.Layers.Count; l++)
            {
                MeshData x = a.Layers[l].Mesh, y = b.Layers[l].Mesh;
                string name = a.Layers[l].Name;
                Assert.That(y.Vertices, Is.EqualTo(x.Vertices), name + ": vertices");
                Assert.That(y.Triangles, Is.EqualTo(x.Triangles), name + ": triangles");
                Assert.That(y.Colors, Is.EqualTo(x.Colors), name + ": baked colours");
            }
        }
    }
}
