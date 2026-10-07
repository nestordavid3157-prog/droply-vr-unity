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
using UnityEngine.XR.OpenXR;

namespace Droply.Editor
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Droply/Scenes/Meadow.unity";
        const string BakedShaderPath = "Assets/Droply/Shaders/VertexColorUnlit.shader";

        [MenuItem("Droply/Generate and configure landscape")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Droply/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Meadow Landscape");
            var generator = root.AddComponent<LandscapeGenerator>();
            // Serialized shader references keep the shaders in Quest builds (the generator creates its materials at runtime).
            // The vertex colour shader draws the baked landscape; the URP shaders only serve the lit fallback.
            generator.vertexColorShader = AssetDatabase.LoadAssetAtPath<Shader>(BakedShaderPath);
            generator.litShader = Shader.Find("Universal Render Pipeline/Lit");
            generator.unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (generator.vertexColorShader == null)
                Debug.LogError("The vertex colour shader was not found at " + BakedShaderPath + "; the landscape would fall back to the plain lit look.");
            if (generator.litShader == null || generator.unlitShader == null)
                Debug.LogWarning("The URP Lit/Unlit shaders were not found; check that the Universal RP package is installed.");

            // The tracking space (moved by walking, its floor on the ground) with the tracked camera inside it.
            var trackingSpace = new GameObject("Tracking space");
            trackingSpace.AddComponent<ViewerLocomotion>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.transform.SetParent(trackingSpace.transform, false);
            camera.tag = "MainCamera";
            camera.nearClipPlane = .1f;
            camera.farClipPlane = WalkArea.FarClip; // the sky follows the eye; everything else stays within this from every walkable point
            camera.gameObject.AddComponent<HeadsetPose>();

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
            EnableQuestFeatures();
        }

        /// <summary>
        /// OpenXR features the app needs on the Quest: "Meta Quest Support" (the Android build runs as a Quest VR app) and the controller profiles
        /// (Oculus Touch and Meta Quest Touch Plus, the Quest 3S controllers), without which the thumbsticks report nothing and walking does not work.
        /// Matched by type name so the setup does not depend on the namespaces of individual features.
        /// </summary>
        static readonly string[] QuestFeatures = { "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };

        static void EnableQuestFeatures()
        {
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR == null) { Debug.LogWarning("OpenXR settings for Android were not found: enable Meta Quest Support and the Touch controller profiles by hand."); return; }
            foreach (string name in QuestFeatures)
            {
                bool found = false;
                foreach (var feature in openXR.GetFeatures())
                {
                    if (feature == null || feature.GetType().Name != name) continue;
                    found = true;
                    if (!feature.enabled) { feature.enabled = true; EditorUtility.SetDirty(feature); }
                }
                if (!found) Debug.LogWarning("OpenXR feature " + name + " was not found for Android; enable it in Project Settings > XR Plug-in Management > OpenXR.");
            }
            EditorUtility.SetDirty(openXR);
        }

        static bool QuestFeaturesEnabled(out string missing)
        {
            missing = "";
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR == null) { missing = "OpenXR settings for Android"; return false; }
            foreach (string name in QuestFeatures)
            {
                bool on = false;
                foreach (var feature in openXR.GetFeatures()) if (feature != null && feature.GetType().Name == name && feature.enabled) on = true;
                if (!on) missing += (missing.Length > 0 ? ", " : "") + name;
            }
            return missing.Length == 0;
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
            pipeline.msaaSampleCount = 4; // sharp low-poly edges on a tile-based GPU: 4x is the usual choice on Quest
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
            QualitySettings.antiAliasing = 4;
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
            if (generators[0].vertexColorShader == null || !generators[0].vertexColorShader.isSupported)
                throw new BuildFailedException("The vertex colour shader is missing or not supported: run \"Droply > Generate and configure landscape\" (or assign " + BakedShaderPath + " to the generator).");
            var poses = Object.FindObjectsByType<HeadsetPose>();
            if (poses.Length != 1) throw new BuildFailedException("The main viewpoint must use headset tracking.");
            if (poses[0].transform.localPosition != Vector3.zero) throw new BuildFailedException("The tracked camera rig must not impose a headset height.");
            if (poses[0].transform.parent == null || poses[0].transform.parent.GetComponent<ViewerLocomotion>() == null)
                throw new BuildFailedException("The tracked camera must sit inside a tracking space with ViewerLocomotion (walking and snap turns): run \"Droply > Generate and configure landscape\".");
            string missingFeatures;
            if (!QuestFeaturesEnabled(out missingFeatures))
                Debug.LogWarning("Not enabled for Android: " + missingFeatures + ". Without the controller profiles the thumbsticks do nothing; without Meta Quest Support the app does not run as a Quest app.");
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
                ", tracked main camera with no forced height, walk area " + LandscapeChecks.MeasureWalk(composition).AreaSquareMetres.ToString("0") + " m2.");
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
                if (generated.Find("Ground") == null || generated.Find("Sand path") == null)
                    throw new BuildFailedException("The generated terrain or leading path mesh is missing.");
                if (generated.Find("Sky") == null || generated.Find("Sun") == null)
                    throw new BuildFailedException("The sky dome or the sun disc is missing.");
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
