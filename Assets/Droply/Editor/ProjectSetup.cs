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
            root.AddComponent<LandscapeGenerator>();

            var cameraRoot = new GameObject("Tracked viewpoint");
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.transform.SetParent(cameraRoot.transform, false);
            camera.tag = "MainCamera";
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 190;
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
            if (LandscapeGenerator.TreeGroupCount < 4 || LandscapeGenerator.TreeGroupCount > 7 ||
                LandscapeGenerator.FlowerIslandCount < 4 || LandscapeGenerator.FlowerIslandCount > 8 ||
                LandscapeGenerator.ForegroundRockCount < 3 || LandscapeGenerator.ForegroundRockCount > 6 ||
                LandscapeGenerator.PathDirectionChanges() < 3)
                throw new BuildFailedException("Composition counts are outside the landscape constraints.");
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
            Debug.Log("Landscape validation passed: tree group=" + LandscapeGenerator.TreeGroupCount +
                ", flower islands=" + LandscapeGenerator.FlowerIslandCount +
                ", foreground stones=" + LandscapeGenerator.ForegroundRockCount +
                ", path direction changes=" + LandscapeGenerator.PathDirectionChanges() +
                ", tracked main camera with no forced height.");
        }

        static void ValidateGeneratedLandscape(LandscapeGenerator generator, string[] forbidden)
        {
            var previousSkybox = RenderSettings.skybox;
            var previousSun = RenderSettings.sun;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousAmbientLight = RenderSettings.ambientLight;
            var previousFog = RenderSettings.fog;
            Transform generated = null;
            try
            {
                generator.SendMessage("Awake");
                generated = generator.transform.Find("Generated Landscape");
                if (generated == null) throw new BuildFailedException("Landscape generation did not create its content root.");
                if (generated.Find("Rolling meadow") == null || generated.Find("Curving sandy path") == null)
                    throw new BuildFailedException("The generated terrain or leading path mesh is missing.");
                var instances = generated.GetComponent<InstancedLandscape>();
                if (instances == null || instances.BatchCount < 5 || instances.InstanceCount < 500)
                    throw new BuildFailedException("The generated vegetation/flower instance batches are unexpectedly sparse.");
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
                RenderSettings.fog = previousFog;
            }
        }
    }
}
