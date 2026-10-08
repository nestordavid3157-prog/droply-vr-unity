using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Droply.Landscape
{
    public sealed class LandscapeGenerator : MonoBehaviour
    {
        const int Seed = 3157;
        public const int RandomSeed = Seed;
        public const int EstimatedTriangleBudget = 180000;
        public const int EstimatedDrawCallBudget = 80;
        public const float TreeLodDistance = 72f;
        public const float WalkablePathWidth = 2.36f;
        public const int TreeGroupCount = 6;
        public const int FlowerIslandCount = 6;
        public const int ForegroundRockCount = 5;
        static readonly Vector3[] PathPoints =
        {
            new Vector3(1.0f, 0, -20), new Vector3(0.4f, 0, -8), new Vector3(-1.8f, 0, 13),
            new Vector3(-5.2f, 0, 27), new Vector3(-2.1f, 0, 41),
            new Vector3(4.7f, 0, 55), new Vector3(5.1f, 0, 69),
            new Vector3(-2.6f, 0, 83), new Vector3(0, 0, 96), new Vector3(-8, 0, 110),
            new Vector3(0, 0, 123), new Vector3(-7, 0, 138)
        };
        static readonly Vector3[] CharacterTreePositions =
        {
            new Vector3(-16,0,31), new Vector3(-12.2f,0,35), new Vector3(-8.5f,0,39),
            new Vector3(9.8f,0,32), new Vector3(14,0,37), new Vector3(18,0,42)
        };

        public static Vector3[] GetCharacterTreePositions() { return (Vector3[])CharacterTreePositions.Clone(); }

        public static int PathDirectionChanges()
        {
            int changes = 0, previousDirection = 0;
            for (int i = 1; i < PathPoints.Length; i++)
            {
                float delta = PathPoints[i].x - PathPoints[i - 1].x;
                int direction = Mathf.Abs(delta) < .5f ? 0 : delta > 0 ? 1 : -1;
                if (direction == 0) continue;
                if (previousDirection != 0 && direction != previousDirection) changes++;
                previousDirection = direction;
            }
            return changes;
        }

        Transform content;
        InstancedLandscape instances;
        System.Random random;
        Material grass, grassLight, trunk, leaves, leavesLight, leavesDark, sand, pathEdge, stone, flowerA, flowerB, flowerC, cloud;
        Material leavesFar, leavesFarLight;
        Material pathVertexMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BuildIfEmpty()
        {
            if (FindAnyObjectByType<LandscapeGenerator>() != null) return;
            var root = new GameObject("Meadow Landscape");
            root.AddComponent<LandscapeGenerator>();
        }

        void Awake()
        {
            GenerateLandscape();
        }

        public void GenerateLandscape()
        {
            if (transform.Find("Generated Landscape") != null) return;
            random = new System.Random(Seed);
            CreateMaterials();
            content = new GameObject("Generated Landscape").transform;
            content.SetParent(transform, false);
            instances = content.gameObject.AddComponent<InstancedLandscape>();
            BuildTerrain();
            BuildPath();
            BuildMiddleGround();
            BuildForest();
            BuildHills();
            BuildClouds();
        }

        void CreateMaterials()
        {
            grass = Mat("Meadow green", new Color32(91, 142, 79, 255));
            grassLight = Mat("Sunlit meadow", new Color32(133, 170, 91, 255));
            trunk = Mat("Warm bark", new Color32(112, 77, 51, 255));
            leaves = Mat("Leaf green", new Color32(67, 119, 78, 255));
            leavesLight = Mat("Canopy light", new Color32(108, 153, 91, 255));
            leavesDark = Mat("Canopy shade", new Color32(48, 96, 82, 255));
            leavesFar = Mat("Distant teal canopy", new Color32(71, 130, 132, 255));
            leavesFarLight = Mat("Far blue green canopy", new Color32(102, 153, 153, 255));
            sand = Mat("Warm path", new Color32(205, 171, 118, 255));
            pathEdge = Mat("Path transition", new Color32(177, 157, 105, 255));
            stone = Mat("Soft grey stone", new Color32(132, 143, 131, 255));
            flowerA = Mat("Butter yellow", new Color32(239, 198, 92, 255));
            flowerB = Mat("Soft coral", new Color32(221, 133, 115, 255));
            flowerC = Mat("Meadow cream", new Color32(237, 222, 173, 255));
            cloud = Mat("Soft cloud", new Color32(231, 239, 239, 255));
            var pathShader = Shader.Find("Droply/Path Vertex Color");
            if (pathShader == null) throw new System.InvalidOperationException("Missing Droply/Path Vertex Color shader.");
            pathVertexMaterial = new Material(pathShader) { name = "Feathered sandy path", enableInstancing = true };
        }

        Material Mat(string label, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = label, color = color, enableInstancing = true };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            return material;
        }

        void BuildTerrain()
        {
            const int cells = 120;
            const float sizeX = 240, sizeZ = 220, originZ = -12;
            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var indices = new int[cells * cells * 6];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                float px = -sizeX * .5f + sizeX * x / cells;
                float pz = originZ + sizeZ * z / cells;
                vertices[z * (cells + 1) + x] = new Vector3(px, Height(px, pz), pz);
            }
            int t = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int a = z * (cells + 1) + x, b = a + cells + 1;
                indices[t++] = a; indices[t++] = b; indices[t++] = b + 1;
                indices[t++] = a; indices[t++] = b + 1; indices[t++] = a + 1;
            }
            CreateMeshObject("Rolling meadow", vertices, indices, grass, content, true);
            var terrain = content.Find("Rolling meadow").gameObject;
            var collider = terrain.AddComponent<MeshCollider>();
            collider.sharedMesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.58f, .65f, .61f);
            RenderSettings.fog = false;
            var sunObject = new GameObject("Warm afternoon sun");
            sunObject.transform.SetParent(content, false);
            sunObject.transform.rotation = Quaternion.Euler(48, -32, 0);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .88f, .68f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .62f;
            sun.shadowBias = .06f;
            sun.shadowNormalBias = .3f;
            sun.shadowNearPlane = .2f;
            RenderSettings.sun = sun;
        }

        static float Height(float x, float z)
        {
            return .48f * Mathf.Sin(x * .046f + z * .018f) +
                   .32f * Mathf.Sin(z * .039f - x * .028f) +
                   .22f * Mathf.Sin(x * .023f - z * .033f);
        }

        static Vector3 PathAt(float t)
        {
            float u = Mathf.Clamp01(t) * (PathPoints.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(u), PathPoints.Length - 2);
            float f = u - i;
            Vector3 p0 = PathPoints[Mathf.Max(i - 1, 0)], p1 = PathPoints[i];
            Vector3 p2 = PathPoints[i + 1], p3 = PathPoints[Mathf.Min(i + 2, PathPoints.Length - 1)];
            return .5f * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f);
        }

        void BuildPath()
        {
            const int steps = 240;
            const int crossSections = 8;
            float[] offsets = { -1.95f, -1.72f, -1.48f, -1.18f, 1.18f, 1.48f, 1.72f, 1.95f };
            var vertices = new Vector3[(steps + 1) * crossSections];
            var colors = new Color[vertices.Length];
            var indices = new int[steps * (crossSections - 1) * 6];
            Color[] bandColors =
            {
                grass.color, Color.Lerp(grass.color, pathEdge.color, .35f), pathEdge.color, sand.color,
                sand.color, pathEdge.color, Color.Lerp(grass.color, pathEdge.color, .35f), grass.color
            };
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = PathAt(t);
                Vector3 tangent = (PathAt(Mathf.Min(t + .002f, 1)) - PathAt(Mathf.Max(t - .002f, 0))).normalized;
                Vector3 side = new Vector3(-tangent.z, 0, tangent.x);
                float edgeNoise = Mathf.Sin(t * 73) * .045f + Mathf.Sin(t * 131 + 1) * .022f;
                float envelope = Mathf.SmoothStep(0, 1, t / .06f) *
                    (1 - Mathf.SmoothStep(0, 1, (t - .91f) / .09f));
                for (int edge = 0; edge < crossSections; edge++)
                {
                    float lateral = offsets[edge] * envelope + Mathf.Sign(offsets[edge]) * edgeNoise * envelope;
                    Vector3 q = p + side * lateral;
                    vertices[i * crossSections + edge] = new Vector3(q.x, Height(q.x, q.z) + .032f, q.z);
                    colors[i * crossSections + edge] = bandColors[edge];
                }
            }
            int k = 0;
            for (int i = 0; i < steps; i++)
            for (int edge = 0; edge < crossSections - 1; edge++)
            {
                int a = i * crossSections + edge;
                indices[k++] = a; indices[k++] = a + 1; indices[k++] = a + crossSections + 1;
                indices[k++] = a; indices[k++] = a + crossSections + 1; indices[k++] = a + crossSections;
            }
            var mesh = new Mesh { name = "Feathered curved footpath", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Curving sandy path");
            go.transform.SetParent(content, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = pathVertexMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildMiddleGround()
        {
            var flowers = new[] { flowerA, flowerB, flowerC, flowerA, flowerB, flowerC };
            Vector2[] islands = { new Vector2(-9, 23), new Vector2(8, 30), new Vector2(-11, 39), new Vector2(11, 45), new Vector2(-10, 53), new Vector2(10, 59) };
            if (islands.Length != FlowerIslandCount) throw new System.InvalidOperationException("Flower island layout count does not match its composition rule.");
            for (int i = 0; i < islands.Length; i++)
            {
                int count = 17 + i * 2;
                for (int j = 0; j < count; j++)
                {
                    float angle = j * 2.39996f;
                    float radius = Mathf.Sqrt((j + .5f) / count) * (1.0f + (i % 3) * .42f);
                    float x = islands[i].x + Mathf.Cos(angle) * radius;
                    float z = islands[i].y + Mathf.Sin(angle) * radius;
                    Flower(new Vector3(x, Height(x, z), z), flowers[i]);
                }
            }

            for (int i = 0; i < CharacterTreePositions.Length; i++)
            {
                float scale = .88f + (i % 3) * .14f;
                Tree(CharacterTreePositions[i].x, CharacterTreePositions[i].z, scale, i, true);
            }

            Vector3[] rocks = { new Vector3(-4, 0, 5), new Vector3(7, 0, 9), new Vector3(-10, 0, 14), new Vector3(9, 0, 19), new Vector3(-7, 0, 25) };
            if (rocks.Length != ForegroundRockCount) throw new System.InvalidOperationException("Foreground rock layout count does not match its composition rule.");
            for (int i = 0; i < rocks.Length; i++) Rock(rocks[i].x, rocks[i].z, .55f + .12f * (i % 3), i);

            for (int i = 0; i < 175; i++)
            {
                float x = Range(-17, 17), z = Range(2, 58);
                if (Mathf.Abs(x - PathAt(Mathf.InverseLerp(0, 110, z)).x) < 2.4f) continue;
                if (z > 27 && z < 43 && x > -12 && x < 11) continue;
                GrassClump(x, z, Range(.72f, 1.16f));
            }
        }

        void BuildForest()
        {
            var placed = new List<Vector2>();
            int variant = 0;
            for (int attempt = 0; attempt < 4200 && placed.Count < 205; attempt++)
            {
                float x = Range(-72, 72), z = Range(47, 146);
                float pathX = PathAt(Mathf.Clamp01((z + 20) / 158f)).x;
                if (Mathf.Abs(x - pathX) < 3.8f) continue;
                var position = new Vector2(x, z);
                bool crowded = false;
                foreach (var previous in placed)
                {
                    if ((position - previous).sqrMagnitude < 25f) { crowded = true; break; }
                }
                if (crowded) continue;

                placed.Add(position);
                float depth = Mathf.InverseLerp(47, 146, z);
                float scale = Mathf.Lerp(1.18f, .42f, depth) * Range(.76f, 1.24f);
                Tree(x, z, scale, variant++, false);
            }
        }

        void BuildHills()
        {
            for (int layer = 0; layer < 3; layer++)
            {
                float z = 100 + layer * 34;
                Material hillMat = Mat("Distant hill " + layer,
                    Color.Lerp(new Color32(91, 139, 130, 255), new Color32(125, 166, 167, 255), layer / 2f));
                int count = 5 + layer;
                for (int i = 0; i < count; i++)
                {
                    float x = -82 + i * (164f / (count - 1)) + Range(-7, 7);
                    float width = Range(20, 39) * (1 + layer * .2f);
                    float height = Range(7, 13) * (1 - layer * .12f);
                    Ellipsoid("Soft distant hill", new Vector3(x, Height(x, z) + height * .36f, z), new Vector3(width, height, 25 + layer * 5), hillMat, false, (i + layer) % 3 == 0 ? 8 : 9);
                }
            }
        }

        void BuildClouds()
        {
            Vector3[] centers = { new Vector3(-26, 29, 46), new Vector3(31, 34, 77), new Vector3(-50, 37, 112), new Vector3(57, 31, 137) };
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 c = centers[i];
                for (int lobe = 0; lobe < 4; lobe++)
                {
                    Vector3 offset = new Vector3((lobe - 1.5f) * 1.5f, lobe % 2 * .38f, (lobe % 2) * .24f);
                    Ellipsoid("Cloud lobe", c + offset, new Vector3(2.2f + (lobe % 2) * .6f, .65f + (lobe % 3) * .13f, 1.05f), cloud, false, 9);
                }
            }
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                var sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetColor("_SkyTint", new Color(.48f, .68f, .81f));
                sky.SetFloat("_AtmosphereThickness", .85f);
                sky.SetFloat("_Exposure", .92f);
                RenderSettings.skybox = sky;
            }
        }

        void Tree(float x, float z, float scale, int variant, bool character)
        {
            float baseY = Height(x, z);
            float shapeVariation = character ? 1f : Range(.84f, 1.18f);
            float height = (character ? 6.1f : 5f) * scale * shapeVariation;
            float crown = height * .55f * (character ? 1f : Range(.86f, 1.14f));
            var center = new Vector3(x, baseY, z);
            float maxNear = TreeLodDistance;
            TaperedBranch("Tree trunk LOD0", center, center + Vector3.up * height * .76f, .28f * scale, .095f * scale, trunk, character ? 7 : 6, character, 0, maxNear);
            int branches = character ? 3 : 2;
            for (int b = 0; b < branches; b++)
            {
                float angle = variant * 1.23f + b * Mathf.PI * 2f / branches;
                Vector3 start = center + Vector3.up * height * (.38f + .12f * b);
                Vector3 end = start + new Vector3(Mathf.Cos(angle) * height * .3f, height * .26f, Mathf.Sin(angle) * height * .3f);
                TaperedBranch("Tree branch LOD0", start, end, .12f * scale, .045f * scale, trunk, 6, character, 0, maxNear);
            }
            int lobes = character ? 4 + variant % 4 : 3 + variant % 4;
            for (int i = 0; i < lobes; i++)
            {
                float a = (i + variant * .41f) * Mathf.PI * 2f / lobes;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * crown * .34f, height * (.66f + .055f * (i % 3)), Mathf.Sin(a) * crown * .34f);
                float s = (i == 0 ? .75f : .57f + .08f * ((i + variant) % 3)) * scale;
                Material m = (i + variant) % 4 == 0 ? leavesLight : (i + variant) % 5 == 0 ? leavesDark : leaves;
                Ellipsoid("Canopy LOD0", p, new Vector3(2.65f, 2.15f, 2.5f) * s, m, character, 9, 0, maxNear);
            }

            float farStart = TreeLodDistance;
            Material farLeaves = z > 125 ? leavesFarLight : z > 85 ? leavesFar : leavesDark;
            TaperedBranch("Tree trunk LOD1", center, center + Vector3.up * height * .62f, .28f * scale, .1f * scale, trunk, 5, false, farStart, 0);
            Vector3 farCrown = center + Vector3.up * height * .72f;
            float farScale = scale * (.88f + .035f * (variant % 4));
            Ellipsoid("Canopy LOD1", farCrown, new Vector3(2.2f, 1.8f, 2.1f) * farScale,
                farLeaves, false, 6, farStart, 0);
            if (variant % 2 == 0)
                Ellipsoid("Canopy LOD1 overlap", farCrown + new Vector3((variant % 3 - 1) * .32f, .22f, .14f),
                    new Vector3(1.6f, 1.5f, 1.7f) * farScale, farLeaves, false, 6, farStart, 0);
        }

        void Rock(float x, float z, float scale, int variant)
        {
            var mesh = RockMesh(variant);
            var go = new GameObject("Rounded meadow stone");
            go.transform.SetParent(content, false);
            go.transform.position = new Vector3(x, Height(x, z) + .17f * scale, z);
            go.transform.rotation = Quaternion.Euler(0, variant * 31, 0);
            go.transform.localScale = new Vector3(scale * 1.25f, scale * .72f, scale);
            var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = stone;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        Mesh RockMesh(int seed)
        {
            const int lon = 10, lat = 5;
            var vertices = new List<Vector3> { new Vector3(0, -.39f, 0) };
            var triangles = new List<int>();
            for (int y = 1; y < lat; y++)
            {
                float phi = Mathf.PI * y / lat;
                for (int x = 0; x < lon; x++)
                {
                    float theta = 2 * Mathf.PI * x / lon;
                    float variation = 1 + .11f * Mathf.Sin(x * 12.31f + y * 7.2f + seed * 4);
                    float py = Mathf.Cos(phi);
                    if (y == lat - 1) py = Mathf.Max(-.38f, py);
                    vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * variation, py * variation, Mathf.Sin(phi) * Mathf.Sin(theta) * variation));
                }
            }
            vertices.Add(new Vector3(0, .97f, 0));
            int bottom = 0, top = vertices.Count - 1;
            for (int x = 0; x < lon; x++)
            {
                triangles.Add(bottom); triangles.Add(1 + (x + 1) % lon); triangles.Add(1 + x);
            }
            for (int r = 0; r < lat - 2; r++)
            for (int x = 0; x < lon; x++)
            {
                int a = 1 + r * lon + x, b = 1 + r * lon + (x + 1) % lon;
                int c = b + lon, d = a + lon;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
            int ring = 1 + (lat - 2) * lon;
            for (int x = 0; x < lon; x++) { triangles.Add(ring + x); triangles.Add(ring + (x + 1) % lon); triangles.Add(top); }
            var mesh = new Mesh { name = "Hand-shaped stone mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void GrassClump(float x, float z, float scale)
        {
            Vector3 position = new Vector3(x, Height(x, z) + .025f, z);
            var mesh = GrassMesh(random.Next(5, 16));
            instances.Add(mesh, random.Next(4) == 0 ? grassLight : grass,
                Matrix4x4.TRS(position, Quaternion.Euler(0, Range(0, 360), 0), new Vector3(scale, scale * Range(.82f, 1.18f), scale)));
        }

        void Flower(Vector3 p, Material petal)
        {
            Ellipsoid("Flower island leaf", p + Vector3.up * .09f, new Vector3(.24f, .07f, .17f), grassLight, false, 6);
            TaperedBranch("Flower stem", p, p + Vector3.up * .3f, .018f, .012f, grass, 5, false);
            for (int pet = 0; pet < 5; pet++)
            {
                float a = pet * Mathf.PI * 2 / 5;
                Vector3 offset = new Vector3(Mathf.Cos(a) * .105f, .32f, Mathf.Sin(a) * .105f);
                Ellipsoid("Wildflower petal", p + offset, new Vector3(.105f, .055f, .08f), petal, false, 6);
            }
            Ellipsoid("Flower heart", p + Vector3.up * .34f, new Vector3(.052f, .05f, .052f), flowerA, false, 6);
        }

        void TaperedBranch(string label, Vector3 start, Vector3 end, float r0, float r1, Material material, int sides,
            bool shadows = true, float minDistance = 0, float maxDistance = 0)
        {
            Vector3 axis = end - start;
            var matrix = Matrix4x4.TRS(start, Quaternion.FromToRotation(Vector3.up, axis.normalized),
                new Vector3(r0, axis.magnitude, r0));
            instances.Add(BranchMesh(sides, r1 / r0), material, matrix, shadows, minDistance, maxDistance);
        }

        void Ellipsoid(string label, Vector3 position, Vector3 scale, Material material, bool shadows, int segments,
            float minDistance = 0, float maxDistance = 0)
        {
            instances.Add(Icosphere(segments, label + " mesh"), material,
                Matrix4x4.TRS(position, Quaternion.identity, scale), shadows, minDistance, maxDistance);
        }

        static readonly Dictionary<int, Mesh> branchMeshes = new Dictionary<int, Mesh>();

        static Mesh BranchMesh(int sides, float taper)
        {
            int key = sides * 1000 + Mathf.RoundToInt(taper * 100);
            if (branchMeshes.TryGetValue(key, out Mesh cached)) return cached;
            var vertices = new Vector3[sides * 2];
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                Vector3 ring = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i] = ring;
                vertices[sides + i] = new Vector3(ring.x * taper, 1, ring.z * taper);
                int next = (i + 1) % sides, t = i * 6;
                triangles[t] = i; triangles[t + 1] = sides + next; triangles[t + 2] = next;
                triangles[t + 3] = i; triangles[t + 4] = sides + i; triangles[t + 5] = sides + next;
            }
            var mesh = new Mesh { name = "Tapered branch segment" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
            branchMeshes[key] = mesh;
            return mesh;
        }

        static Mesh Icosphere(int resolution, string name)
        {
            if (sphereMeshes.TryGetValue(resolution, out Mesh cached)) return cached;
            int longitude = resolution, latitude = Mathf.Max(4, resolution / 2);
            var vertices = new List<Vector3> { Vector3.down };
            var triangles = new List<int>();
            for (int y = 1; y < latitude; y++)
            {
                float phi = Mathf.PI * y / latitude;
                for (int x = 0; x < longitude; x++)
                {
                    float theta = 2 * Mathf.PI * x / longitude;
                    float wobble = 1 + .055f * Mathf.Sin(x * 13.7f + y * 9.1f);
                    vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * wobble);
                }
            }
            int top = vertices.Count; vertices.Add(Vector3.up);
            for (int x = 0; x < longitude; x++)
            {
                triangles.Add(0); triangles.Add(1 + x); triangles.Add(1 + (x + 1) % longitude);
            }
            for (int y = 0; y < latitude - 2; y++)
            for (int x = 0; x < longitude; x++)
            {
                int a = 1 + y * longitude + x, b = 1 + y * longitude + (x + 1) % longitude, c = b + longitude, d = a + longitude;
                triangles.Add(a); triangles.Add(d); triangles.Add(c); triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
            int ring = 1 + (latitude - 2) * longitude;
            for (int x = 0; x < longitude; x++) { triangles.Add(top); triangles.Add(ring + (x + 1) % longitude); triangles.Add(ring + x); }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            sphereMeshes[resolution] = mesh;
            return mesh;
        }

        static readonly Dictionary<int, Mesh> sphereMeshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> grassMeshes = new Dictionary<int, Mesh>();

        static Mesh GrassMesh(int count)
        {
            if (grassMeshes.TryGetValue(count, out Mesh cached)) return cached;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                float reach = .14f + .26f * ((i * 7 % count) / (float)count);
                float height = .3f + .38f * ((i * 5 % count) / (float)count);
                Vector3 origin = new Vector3(Mathf.Cos(angle) * reach * .22f, 0, Mathf.Sin(angle) * reach * .22f);
                Vector3 tip = origin + new Vector3(Mathf.Cos(angle) * reach, height, Mathf.Sin(angle) * reach);
                Vector3 side = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (.09f + .075f * ((i * 3 % count) / (float)count));
                int n = vertices.Count;
                vertices.Add(origin - side); vertices.Add(origin + side);
                vertices.Add(Vector3.Lerp(origin, tip, .56f) + side * .52f);
                vertices.Add(Vector3.Lerp(origin, tip, .56f) - side * .52f); vertices.Add(tip);
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
                triangles.Add(n); triangles.Add(n + 4); triangles.Add(n + 2);
                triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n + 3);
                triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 4);
            }
            var mesh = new Mesh { name = "Broad stylized grass leaves " + count };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            grassMeshes[count] = mesh;
            return mesh;
        }

        GameObject CreateMeshObject(string label, Vector3[] vertices, int[] indices, Material material, Transform parent, bool shadows)
        {
            var mesh = new Mesh { name = label + " mesh", indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return go;
        }

        float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
    }

    public sealed class InstancedLandscape : MonoBehaviour
    {
        struct BatchKey : System.IEquatable<BatchKey>
        {
            public Mesh mesh;
            public Material material;
            public bool shadows;
            public float minDistance;
            public float maxDistance;

            public bool Equals(BatchKey other)
            {
                return mesh == other.mesh && material == other.material && shadows == other.shadows &&
                    minDistance == other.minDistance && maxDistance == other.maxDistance;
            }

            public override bool Equals(object obj) { return obj is BatchKey other && Equals(other); }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (mesh != null ? mesh.GetHashCode() : 0) * 397 ^ (material != null ? material.GetHashCode() : 0);
                    hash = hash * 31 ^ shadows.GetHashCode();
                    hash = hash * 31 ^ minDistance.GetHashCode();
                    return hash * 31 ^ maxDistance.GetHashCode();
                }
            }
        }

        readonly Dictionary<BatchKey, List<Matrix4x4>> groups = new Dictionary<BatchKey, List<Matrix4x4>>();
        readonly Matrix4x4[] drawMatrices = new Matrix4x4[1023];
        public int BatchCount { get { return groups.Count; } }
        public int NearLodInstanceCount { get { return CountInstancesByLod(false); } }
        public int FarLodInstanceCount { get { return CountInstancesByLod(true); } }
        public int InstanceCount
        {
            get
            {
                int total = 0;
                foreach (var group in groups) total += group.Value.Count;
                return total;
            }
        }

        int CountInstancesByLod(bool far)
        {
            int count = 0;
            foreach (var group in groups)
            {
                bool isFar = group.Key.minDistance >= LandscapeGenerator.TreeLodDistance;
                bool isNear = group.Key.minDistance == 0 && group.Key.maxDistance == LandscapeGenerator.TreeLodDistance;
                if (far ? isFar : isNear) count += group.Value.Count;
            }
            return count;
        }

        public void Add(Mesh mesh, Material material, Matrix4x4 matrix, bool shadows = false,
            float minDistance = 0, float maxDistance = 0)
        {
            var key = new BatchKey
            {
                mesh = mesh, material = material, shadows = shadows,
                minDistance = minDistance, maxDistance = maxDistance
            };
            if (!groups.TryGetValue(key, out List<Matrix4x4> transforms))
            {
                transforms = new List<Matrix4x4>();
                groups.Add(key, transforms);
            }
            transforms.Add(matrix);
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 cameraPosition = camera.transform.position;
            foreach (var group in groups)
            {
                List<Matrix4x4> matrices = group.Value;
                int count = 0;
                for (int i = 0; i < matrices.Count; i++)
                {
                    float distance = Vector3.Distance(cameraPosition, matrices[i].MultiplyPoint3x4(Vector3.zero));
                    if (distance < group.Key.minDistance || (group.Key.maxDistance > 0 && distance >= group.Key.maxDistance)) continue;
                    drawMatrices[count++] = matrices[i];
                    if (count != drawMatrices.Length) continue;
                    DrawBatch(group.Key, count);
                    count = 0;
                }
                if (count > 0) DrawBatch(group.Key, count);
            }
        }

        void DrawBatch(BatchKey key, int count)
        {
            Graphics.DrawMeshInstanced(key.mesh, 0, key.material, drawMatrices, count,
                null, key.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off, key.shadows, gameObject.layer);
        }

        public int EstimatedTriangleCount
        {
            get
            {
                int triangles = 0;
                foreach (var group in groups)
                    triangles += group.Key.mesh.triangles.Length / 3 * group.Value.Count;
                foreach (var filter in GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.transform == transform || filter.sharedMesh == null) continue;
                    triangles += filter.sharedMesh.triangles.Length / 3;
                }
                return triangles;
            }
        }

        public int EstimatedDrawCalls
        {
            get
            {
                int draws = 0;
                foreach (var group in groups) draws += (group.Value.Count + 1022) / 1023;
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>()) draws += 1;
                return draws;
            }
        }

        public int EstimatedMaterialCount
        {
            get
            {
                var unique = new HashSet<Material>();
                foreach (var group in groups) unique.Add(group.Key.material);
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials) unique.Add(material);
                return unique.Count;
            }
        }

        public bool AllMaterialsSupportInstancing
        {
            get
            {
                foreach (var group in groups) if (!group.Key.material.enableInstancing) return false;
                return true;
            }
        }

        public string DeterministicSignature()
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var group in groups)
                {
                    hash = (hash ^ (uint)group.Key.mesh.vertexCount) * 16777619;
                    hash = (hash ^ (uint)group.Value.Count) * 16777619;
                    foreach (var matrix in group.Value)
                        for (int i = 0; i < 16; i++) hash = (hash ^ (uint)matrix[i].GetHashCode()) * 16777619;
                }
                foreach (var filter in GetComponentsInChildren<MeshFilter>())
                {
                    hash = (hash ^ (uint)filter.sharedMesh.vertexCount) * 16777619;
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        hash = (hash ^ (uint)vertex.x.GetHashCode()) * 16777619;
                        hash = (hash ^ (uint)vertex.y.GetHashCode()) * 16777619;
                        hash = (hash ^ (uint)vertex.z.GetHashCode()) * 16777619;
                    }
                }
                return hash.ToString("X8");
            }
        }
    }

}
