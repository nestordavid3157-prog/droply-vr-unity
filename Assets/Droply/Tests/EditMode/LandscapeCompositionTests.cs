using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Droply.Landscape.Tests
{
    public sealed class LandscapeCompositionTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void CompositionHasExactlyOneBoundedCharacterGroup()
        {
            var trees = LandscapeGenerator.GetCharacterTreePositions();
            Assert.That(LandscapeGenerator.TreeGroupCount, Is.InRange(4, 7));
            Assert.That(trees.Length, Is.EqualTo(LandscapeGenerator.TreeGroupCount));
            Assert.That(LandscapeGenerator.FlowerIslandCount, Is.InRange(4, 8));
            Assert.That(LandscapeGenerator.ForegroundRockCount, Is.InRange(3, 6));
            Assert.That(trees.Select(p => p.x).Distinct().Count(), Is.EqualTo(trees.Length));
        }

        [Test]
        public void PathIsWalkableWidthAndHasAtLeastThreeTurns()
        {
            Assert.That(LandscapeGenerator.WalkablePathWidth, Is.InRange(2f, 3f));
            Assert.That(LandscapeGenerator.PathDirectionChanges(), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void PathUsesFeatheredVertexColorsAndTaperedUnseamedEnds()
        {
            Generate();
            Mesh mesh = root.transform.Find("Generated Landscape/Curving sandy path").GetComponent<MeshFilter>().sharedMesh;
            const int crossSections = 8;
            const int steps = 240;
            Assert.That(mesh.vertexCount, Is.EqualTo((steps + 1) * crossSections));
            Assert.That(mesh.colors.Distinct().Count(), Is.GreaterThanOrEqualTo(4));
            Assert.That(Vector3.Distance(mesh.vertices[3], mesh.vertices[4]), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(mesh.vertices[120 * crossSections + 3], mesh.vertices[120 * crossSections + 4]),
                Is.InRange(2f, 3f));
            Assert.That(Vector3.Distance(mesh.vertices[steps * crossSections + 3], mesh.vertices[steps * crossSections + 4]), Is.LessThan(.001f));
            Assert.That(mesh.normals[120 * crossSections + 3].y, Is.GreaterThan(.8f));
        }

        [Test]
        public void GeneratedWorldHasNoProhibitedNamedContent()
        {
            Generate();
            string[] prohibited = { "house", "building", "architecture", "furniture", "device", "sign", "text", "logo", "canvas", "ui" };
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                Assert.That(prohibited.Any(term => child.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False, "Prohibited world object: " + child.name);
        }

        [Test]
        public void SeededLandscapeLayoutIsRepeatable()
        {
            var first = Generate();
            string firstSignature = first.DeterministicSignature();
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            var second = Generate();
            string secondSignature = second.DeterministicSignature();
            Assert.That(LandscapeGenerator.RandomSeed, Is.EqualTo(3157));
            Assert.That(secondSignature, Is.EqualTo(firstSignature));
        }

        [Test]
        public void LandscapeFitsEstimatedQuestGeometryAndDrawBudget()
        {
            var batches = Generate();
            Debug.Log("Landscape budget estimate (not measured on Quest): " +
                batches.EstimatedTriangleCount + " triangles, " + batches.EstimatedDrawCalls +
                " draw calls, " + batches.EstimatedMaterialCount + " materials, " +
                batches.BatchCount + " instanced batches.");
            Assert.That(batches.AllMaterialsSupportInstancing, Is.True, "Every instanced material must opt in to instancing.");
            Assert.That(batches.BatchCount, Is.LessThanOrEqualTo(LandscapeGenerator.EstimatedDrawCallBudget));
            Assert.That(batches.EstimatedDrawCalls, Is.LessThanOrEqualTo(LandscapeGenerator.EstimatedDrawCallBudget));
            Assert.That(batches.EstimatedTriangleCount, Is.LessThanOrEqualTo(LandscapeGenerator.EstimatedTriangleBudget));
            Assert.That(batches.EstimatedMaterialCount, Is.LessThanOrEqualTo(20));
            Assert.That(batches.NearLodInstanceCount, Is.GreaterThan(0));
            Assert.That(batches.FarLodInstanceCount, Is.GreaterThan(0));
        }

        [Test]
        public void SmoothGlideAndTeleportPreserveRigAndHeadsetHeight()
        {
            root = new GameObject("Tracked rig");
            root.transform.position = new Vector3(3, 1.7f, -2);
            var locomotion = root.AddComponent<ComfortableLocomotion>();
            var camera = new GameObject("Tracked test camera");
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.65f, 0);
            float cameraHeight = camera.transform.localPosition.y;

            locomotion.MoveLocal(Vector2.up, Quaternion.identity, 2f);
            Assert.That(root.transform.position.y, Is.EqualTo(1.7f));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(cameraHeight));
            Assert.That(root.transform.position.z, Is.EqualTo(-2 + ComfortableLocomotion.GlideSpeed * 2f).Within(.0001f));

            locomotion.TeleportTo(new Vector3(-12, 50, 21));
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(-12, 1.7f, 21)));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(cameraHeight));
        }

        InstancedLandscape Generate()
        {
            root = new GameObject("Test landscape");
            var generator = root.AddComponent<LandscapeGenerator>();
            generator.GenerateLandscape();
            var generated = root.transform.Find("Generated Landscape");
            Assert.That(generated, Is.Not.Null);
            return generated.GetComponent<InstancedLandscape>();
        }
    }
}
