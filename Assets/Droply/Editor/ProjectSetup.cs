using System.IO;
using Droply.Landscape;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace Droply.Editor
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Droply/Scenes/Meadow.unity";

        [MenuItem("Droply/Generate and configure landscape")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Droply/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Meadow Landscape");
            var generator = root.AddComponent<LandscapeGenerator>();
            // Serialized shader references keep the shaders in Quest builds (the generator creates its materials at runtime).
            generator.litShader = Shader.Find("Universal Render Pipeline/Lit");
            generator.unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (generator.litShader == null || generator.unlitShader == null)
                Debug.LogWarning("The URP Lit/Unlit shaders were not found; check that the Universal RP package is installed.");

            var cameraRoot = new GameObject("Tracked viewpoint");
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.transform.SetParent(cameraRoot.transform, false);
            camera.tag = "MainCamera";
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 340; // the farthest hill layer is about 290 m away
            cameraRoot.AddComponent<HeadsetPose>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigureUniversalRenderPipeline();
            ConfigureOpenXR();
            ConfigureQuestPlayer();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Generated deterministic meadow scene at " + ScenePath);
        }

        static void ConfigureOpenXR()
        {
            const string settingsPath = "Assets/Droply/XRGeneralSettingsPerBuildTarget.asset";
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (settings == null)
            {
                string[] matches = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (matches.Length > 0)
                    settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(matches[0]));
            }
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
            }
            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var androidSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var assigned = UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
                androidSettings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            if (!assigned) Debug.LogWarning("OpenXR loader setup did not complete. Check XR Plug-in Management for Android.");
            androidSettings.Manager.automaticLoading = true;
            androidSettings.Manager.automaticRunning = true;
            EditorUtility.SetDirty(androidSettings.Manager);
        }

        static void ConfigureQuestPlayer()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.droply.vrlandscape");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        static void ConfigureUniversalRenderPipeline()
        {
            const string pipelinePath = "Assets/Droply/Materials/DroplyUniversalRenderPipelineAsset.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Droply/Materials/DroplyUniversalRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1f;
            pipeline.mainLightShadowmapResolution = 1024;
            pipeline.shadowDistance = 60f;
            pipeline.shadowCascadeCount = 1;
            var serializedPipeline = new SerializedObject(pipeline);
            serializedPipeline.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serializedPipeline.FindProperty("m_AnyShadowsSupported").boolValue = true;
            serializedPipeline.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serializedPipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.shadowDistance = 60f;
            QualitySettings.antiAliasing = 2;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Droply/Validate landscape")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var generators = Object.FindObjectsByType<LandscapeGenerator>();
            if (generators.Length != 1) throw new BuildFailedException("Expected exactly one landscape generator, found " + generators.Length);
            // The composition rules (open foreground, clear path, one grouped tree group, no tree rows, open sight corridors, relief, calm palette, budget)
            // are measured on the same data the player builds. The same checks run outside Unity: dotnet run --project Tools/CompositionCheck
            SceneData composition = LandscapeBuilder.Build();
            var violations = LandscapeChecks.Run(composition);
            if (violations.Count > 0) throw new BuildFailedException("Composition checks failed:\n - " + string.Join("\n - ", violations));
            if (!scene.IsValid() || scene.GetRootGameObjects().Length != 2) throw new BuildFailedException("Scene must contain only its landscape and tracked-viewpoint roots.");
            if (Camera.main == null) throw new BuildFailedException("Starting viewpoint camera is missing.");
            var poses = Object.FindObjectsByType<HeadsetPose>();
            if (poses.Length != 1) throw new BuildFailedException("The main viewpoint must use headset tracking.");
            if (poses[0].transform.localPosition != Vector3.zero) throw new BuildFailedException("The tracked camera rig must not impose a headset height.");
            string[] forbidden = { "House", "Building", "Architecture", "Furniture", "Device", "Sign", "Text", "Logo", "Canvas" };
            foreach (var root in scene.GetRootGameObjects())
                foreach (string term in forbidden)
                    if (root.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new BuildFailedException("Prohibited world content detected: " + root.name);
            ValidateGeneratedLandscape(generators[0], forbidden);
            int heroTrees = 0;
            foreach (var tree in composition.Trees) if (tree.Tier == TreeTier.Hero && tree.Kind != TreeKind.Shrub) heroTrees++;
            Debug.Log("Landscape validation passed: " + composition.TriangleCount + " triangles in " + composition.Layers.Count + " layers, tree group=" + heroTrees +
                ", flower islands=" + composition.IslandCenters.Count + ", stones=" + composition.Rocks.Count +
                ", tracked main camera with no forced height.");
        }

        static void ValidateGeneratedLandscape(LandscapeGenerator generator, string[] forbidden)
        {
            var previousSkybox = RenderSettings.skybox;
            var previousSun = RenderSettings.sun;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousAmbientLight = RenderSettings.ambientLight;
            var previousAmbientSky = RenderSettings.ambientSkyColor;
            var previousAmbientEquator = RenderSettings.ambientEquatorColor;
            var previousAmbientGround = RenderSettings.ambientGroundColor;
            var previousFog = RenderSettings.fog;
            Transform generated = null;
            try
            {
                generator.SendMessage("Awake");
                generated = generator.transform.Find("Generated Landscape");
                if (generated == null) throw new BuildFailedException("Landscape generation did not create its content root.");
                if (generated.Find("Ground GroundBase") == null || generated.Find("Sand path") == null)
                    throw new BuildFailedException("The generated terrain or leading path mesh is missing.");
                if (generated.childCount < 20)
                    throw new BuildFailedException("The generated landscape has only " + generated.childCount + " mesh layers; expected terrain, path, trees, meadow details and distance layers.");
                foreach (var child in generated.GetComponentsInChildren<Transform>(true))
                    foreach (string term in forbidden)
                        if (child.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            throw new BuildFailedException("Prohibited generated world content detected: " + child.name);
            }
            finally
            {
                if (generated != null) Object.DestroyImmediate(generated.gameObject);
                RenderSettings.skybox = previousSkybox;
                RenderSettings.sun = previousSun;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                RenderSettings.ambientSkyColor = previousAmbientSky;
                RenderSettings.ambientEquatorColor = previousAmbientEquator;
                RenderSettings.ambientGroundColor = previousAmbientGround;
                RenderSettings.fog = previousFog;
            }
        }
    }
}
