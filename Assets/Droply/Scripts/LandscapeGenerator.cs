using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Droply.Landscape
{
    /// <summary>
    /// Unity side of the landscape. The composition (terrain, path, trees, meadow details, distance layers) is built as plain data by
    /// <see cref="LandscapeBuilder"/> from <see cref="Plan"/>; this component turns that data into merged static meshes (one per material, a few dozen draw calls,
    /// no per-frame script work), materials, the warm side light, the trilight ambient and the sky.
    /// </summary>
    public sealed class LandscapeGenerator : MonoBehaviour
    {
        // Assigned by "Droply > Generate and configure landscape". A serialized reference keeps the shader in player builds;
        // Shader.Find alone can fail there because nothing in a scene references the shader.
        public Shader litShader;
        public Shader unlitShader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BuildIfEmpty()
        {
            if (FindAnyObjectByType<LandscapeGenerator>() != null) return;
            var root = new GameObject("Meadow Landscape");
            root.AddComponent<LandscapeGenerator>();
        }

        void Awake()
        {
            if (transform.Find("Generated Landscape") != null) return;
            SceneData data = LandscapeBuilder.Build();
            var content = new GameObject("Generated Landscape").transform;
            content.SetParent(transform, false);
            var materials = new Dictionary<Mat, Material>();
            foreach (var layer in data.Layers) CreateLayer(layer, content, materials);
            ConfigureLight(content);
            ConfigureSky();
            ConfigureCamera();
        }

        void CreateLayer(MeshLayer layer, Transform parent, Dictionary<Mat, Material> materials)
        {
            Material material;
            if (!materials.TryGetValue(layer.Material, out material)) { material = CreateMaterial(layer.Material); materials[layer.Material] = material; }
            var go = new GameObject(layer.Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = CreateMesh(layer.Mesh, layer.Name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = layer.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = !Palette.Unlit(layer.Material);
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Mesh CreateMesh(MeshData data, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = data.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = data.Vertices;
            mesh.normals = data.Normals;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true); // the CPU copy is not needed again: saves memory on the headset
            return mesh;
        }

        Material CreateMaterial(Mat id)
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

        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 340f; // the farthest hill layer is about 290 m away
        }
    }
}
