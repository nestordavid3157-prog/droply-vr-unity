using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Droply.Landscape
{
    /// <summary>
    /// Unity side of the landscape. The composition (terrain, path, trees, meadow details, distance layers, sky) is built as plain data by
    /// <see cref="LandscapeBuilder"/> from <see cref="Plan"/>, with the light already baked into the vertex colours (<see cref="Lighting"/>).
    /// This component turns that data into static meshes (one per layer, a few dozen draw calls) and keeps the walk area for <see cref="ViewerLocomotion"/>.
    /// Its only per-frame work: the sky dome and the sun follow the camera (they are "at infinity", so walking never brings them closer).
    /// <para>
    /// Baked pipeline (normal case): every layer is drawn with the tiny unlit shader "Droply/Vertex Colour Unlit": no lights, no shadow maps, no skybox, no textures.
    /// Lit fallback (only if that shader is missing or not supported on the device): URP Lit with one flat colour per material, a real warm sun with soft shadows,
    /// a trilight ambient and the scene's skybox. It looks plainer and is not checked on a headset either.
    /// </para>
    /// </summary>
    public sealed class LandscapeGenerator : MonoBehaviour
    {
        public const string BakedShaderName = "Droply/Vertex Colour Unlit";

        // Assigned by "Droply > Generate and configure landscape" (and stored in the scene). A serialized reference keeps a shader in player builds;
        // Shader.Find alone can fail there because nothing in a scene references the shader.
        public Shader vertexColorShader;
        public Shader litShader;
        public Shader unlitShader;

        /// <summary>Where the viewer may walk (built with the landscape).</summary>
        public WalkArea WalkArea { get; private set; }

        Transform skyDome, sunDisc;
        Camera viewer;

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

        /// <summary>Builds the landscape under this object (once; does nothing if it is already there). Also called by the editor menu and the EditMode tests.</summary>
        public void GenerateLandscape()
        {
            if (transform.Find("Generated Landscape") != null) return;
            var watch = Stopwatch.StartNew();
            SceneData data = LandscapeBuilder.Build();
            WalkArea = data.Walk;
            long built = watch.ElapsedMilliseconds;
            var content = new GameObject("Generated Landscape").transform;
            content.SetParent(transform, false);
            Shader baked = BakedShader();
            if (baked != null) CreateBaked(data, content, baked);
            else CreateLit(data, content);
            ConfigureCamera(baked != null);
            // Visible in logcat on the headset (adb logcat -s Unity): the real build time on the device.
            Debug.Log("Droply landscape: " + data.TriangleCount + " triangles in " + data.Layers.Count + " layers; composition + light bake " + built + " ms (bake " +
                      data.BakeMilliseconds + " ms), objects " + (watch.ElapsedMilliseconds - built) + " ms; " + (baked != null ? "baked vertex lighting" : "lit fallback (vertex colour shader missing or unsupported)"));
        }

        Shader BakedShader()
        {
            Shader shader = vertexColorShader != null ? vertexColorShader : Shader.Find(BakedShaderName);
            return shader != null && shader.isSupported ? shader : null;
        }

        // ---- baked pipeline ---------------------------------------------------------------------------------------------------------

        void CreateBaked(SceneData data, Transform parent, Shader shader)
        {
            // The colour of everything is in the vertices, so four materials are enough: solid, two-sided (blades, petals), sky and sun disc.
            // Sky and sun are drawn first and write no depth, so everything else simply covers them.
            Material solid = BakedMaterial(shader, "Baked solid", CullMode.Back, true, -1);
            Material twoSided = BakedMaterial(shader, "Baked two-sided", CullMode.Off, true, -1);
            Material sky = BakedMaterial(shader, "Sky", CullMode.Off, false, (int)RenderQueue.Background);
            Material sun = BakedMaterial(shader, "Sun", CullMode.Off, false, (int)RenderQueue.Background + 1);
            foreach (var layer in data.Layers)
            {
                Material material = layer.Material == Mat.Sky ? sky : layer.Material == Mat.SunDisc ? sun : Palette.DoubleSided(layer.Material) ? twoSided : solid;
                // Colours instead of normals: the shader reads the first and never the second (position + colour = 16 bytes per vertex, position + normal would be 24).
                Transform t = AddRenderer(layer, parent, material, CreateMesh(layer.Mesh, layer.Name, true), false, false);
                if (layer.Material == Mat.Sky) skyDome = t;
                else if (layer.Material == Mat.SunDisc) sunDisc = t;
            }
        }

        /// <summary>The sky is at infinity: the dome and the sun move with the eye, so walking never brings them closer or lets the far plane cut into them.</summary>
        void LateUpdate()
        {
            if (skyDome == null) return;
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            Vector3 eye = viewer.transform.position;
            skyDome.position = eye;
            if (sunDisc != null) sunDisc.position = eye;
        }

        static Material BakedMaterial(Shader shader, string name, CullMode cull, bool depthWrite, int queue)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)cull);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", depthWrite ? 1f : 0f);
            if (queue >= 0) material.renderQueue = queue;
            return material;
        }

        // ---- lit fallback -------------------------------------------------------------------------------------------------------------

        void CreateLit(SceneData data, Transform parent)
        {
            var materials = new Dictionary<Mat, Material>();
            foreach (var layer in data.Layers)
            {
                if (Palette.BakedOnly(layer.Material)) continue; // the sky dome and the sun disc: here a skybox and a real light do their job
                Material material;
                if (!materials.TryGetValue(layer.Material, out material)) { material = CreateLitMaterial(layer.Material); materials[layer.Material] = material; }
                AddRenderer(layer, parent, material, CreateMesh(layer.Mesh, layer.Name, false), layer.CastShadows, !Palette.Unlit(layer.Material));
            }
            ConfigureLight(parent);
            ConfigureSky();
        }

        Material CreateLitMaterial(Mat id)
        {
            bool unlit = Palette.Unlit(id);
            Shader shader = unlit ? unlitShader : litShader;
            if (shader == null) shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = id.ToString() };
            Color color = Palette.Color(id);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            // Matte, and only properties of the default shader variant: keywords switched on at runtime can be stripped from a player build.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (Palette.DoubleSided(id) && material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f); // thin blades and petals are seen from both sides
            return material;
        }

        /// <summary>Warm light from the left at low elevation (long soft shadows to the right), blue-green shade from the sky/ground ambient. No fog, no post-processing.</summary>
        static void ConfigureLight(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.fog = false;
            var sunObject = new GameObject("Warm side sun");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.rotation = Quaternion.Euler(Palette.SunPitch, Palette.SunYaw, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.SunColor;
            sun.intensity = Palette.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = Palette.SunShadowStrength;
            sun.shadowBias = .06f;
            sun.shadowNormalBias = .3f;
            sun.shadowNearPlane = .2f;
            RenderSettings.sun = sun;
        }

        static void ConfigureSky()
        {
            Material sky = null;
            if (RenderSettings.skybox != null) sky = new Material(RenderSettings.skybox); // the scene's default skybox material: its shader is part of every build
            else
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader != null) sky = new Material(shader);
            }
            if (sky == null) return;
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Palette.SkyTint);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", .9f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.05f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", .03f);
            RenderSettings.skybox = sky;
        }

        // ---- shared ---------------------------------------------------------------------------------------------------------------------

        static Transform AddRenderer(MeshLayer layer, Transform parent, Material material, Mesh mesh, bool castShadows, bool receiveShadows)
        {
            var go = new GameObject(layer.Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        /// <summary>One static mesh. <paramref name="baked"/>: vertex colours (final colour of each vertex), otherwise normals (the lit fallback computes its own light).</summary>
        static Mesh CreateMesh(MeshData data, string name, bool baked)
        {
            var mesh = new Mesh { name = name, indexFormat = data.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = data.Vertices;
            if (baked) mesh.colors32 = data.Colors;
            else mesh.normals = data.Normals;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true); // the CPU copy is not needed again: saves memory on the headset
            return mesh;
        }

        /// <summary>
        /// Camera for both pipelines. Baked: the dome is the sky, so the camera only clears to the horizon colour (nothing but the dome shows it).
        /// Far plane: the dome follows the eye at 325 m; everything else stays within <see cref="WalkArea.FarClip"/> from every walkable point (checked).
        /// </summary>
        static void ConfigureCamera(bool baked)
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = WalkArea.FarClip;
            if (baked)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.HorizonColor;
                RenderSettings.fog = false;
            }
            else camera.clearFlags = CameraClearFlags.Skybox;
        }
    }
}
