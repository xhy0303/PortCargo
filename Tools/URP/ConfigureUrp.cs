using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Explicit, reproducible setup for PortCargo's unlit Canvas rendering.
// Execute via Pipeline run_script after installing URP; no runtime dependency.
public static class ConfigureUrp
{
    const string Folder = "Assets/PortCargo/Settings/Rendering";
    const string PipelinePath = Folder + "/PortCargoURP.asset";
    const string RendererPath = Folder + "/PortCargoRenderer.asset";

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static object Apply()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Editor must be idle");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene changes must be preserved first");
        EnsureFolder(Folder);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (!renderer)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }
        ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
        renderer.renderingMode = RenderingMode.Forward;
        renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
        EditorUtility.SetDirty(renderer);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (!pipeline)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        if (pipeline.rendererDataList.Length != 1 || pipeline.rendererDataList[0] != renderer)
            throw new Exception("Unexpected renderer configuration; refusing to overwrite");
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipeline.renderScale = 1;
        pipeline.supportsCameraDepthTexture = false;
        pipeline.supportsCameraOpaqueTexture = false;
        // URP 17.5 exposes these as read-only; use the Editor's serialized API.
        var lighting = new SerializedObject(pipeline);
        lighting.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.Disabled;
        lighting.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
        lighting.FindProperty("m_MainLightShadowsSupported").boolValue = false;
        lighting.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        lighting.FindProperty("m_SoftShadowsSupported").boolValue = false;
        lighting.ApplyModifiedPropertiesWithoutUndo();
        pipeline.useSRPBatcher = true;
        EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        int originalQuality = QualitySettings.GetQualityLevel();
        try
        {
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
        }
        finally { QualitySettings.SetQualityLevel(originalQuality, false); }
        AssetDatabase.SaveAssets();

        foreach (var path in new[] { "Assets/PortCargo/Scenes/Bootstrap.unity", "Assets/Scenes/SampleScene.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var camera in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.allowHDR = false;
                camera.allowMSAA = false;
                data.SetRenderer(0);
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresColorTexture = false;
                data.requiresDepthTexture = false;
                data.antialiasing = AntialiasingMode.None;
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(data);
            }
            // Preflight proved there are no lights, lightmaps or probes. Remove
            // Bootstrap's built-in placeholder lighting-data reference, not a bake.
            if (LightmapSettings.lightmaps.Length != 0) throw new Exception("Unexpected baked lightmaps");
            Lightmapping.ClearLightingDataAsset();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene("Assets/PortCargo/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        return Validate();
    }

    public static object Validate()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (!pipeline || !renderer || GraphicsSettings.defaultRenderPipeline != pipeline)
            throw new Exception("Saved URP assignment missing");
        var serialized = new SerializedObject(pipeline);
        var index = serialized.FindProperty("m_DefaultRendererIndex");
        if (index == null || index.intValue != 0 || pipeline.rendererDataList.Length != 1 || pipeline.rendererDataList[0] != renderer)
            throw new Exception("Saved default renderer missing");
        var quality = Enumerable.Range(0, QualitySettings.names.Length).Select(i => new {
            name = QualitySettings.names[i], path = AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i)) }).ToArray();
        if (quality.Any(q => q.path != PipelinePath)) throw new Exception("A quality level was not migrated");
        if (pipeline.supportsHDR || pipeline.msaaSampleCount != 1 || pipeline.supportsCameraDepthTexture || pipeline.supportsCameraOpaqueTexture)
            throw new Exception("Mobile rendering settings did not persist");
        if (renderer.renderingMode != RenderingMode.Forward || renderer.intermediateTextureMode != IntermediateTextureMode.Auto
            || renderer.depthPrimingMode != DepthPrimingMode.Disabled || renderer.rendererFeatures.Count != 0)
            throw new Exception("The lightweight Forward renderer configuration did not persist");
        if (!Mathf.Approximately(pipeline.renderScale, 1f) || !pipeline.useSRPBatcher
            || pipeline.mainLightRenderingMode != LightRenderingMode.Disabled
            || pipeline.additionalLightsRenderingMode != LightRenderingMode.Disabled
            || pipeline.supportsMainLightShadows || pipeline.supportsAdditionalLightShadows || pipeline.supportsSoftShadows
            || PlayerSettings.colorSpace != ColorSpace.Gamma)
            throw new Exception("The unlit UI pipeline configuration did not persist");

        // Query the explicitly registered URP settings, independent of whether the
        // Editor has already constructed a live pipeline in this frame.
        var global = EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>();
        if (!global || GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>() != global)
            throw new Exception("URP Global Settings registration missing or inconsistent");
        var globalPath = AssetDatabase.GetAssetPath(global);
        if (string.IsNullOrEmpty(globalPath) || !File.Exists(globalPath) || !File.Exists(globalPath + ".meta")
            || AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath) != global)
            throw new Exception("Registered URP Global Settings is not a persistent project asset");
        if (!EditorGraphicsSettings.TryGetRenderPipelineSettingsForPipeline<URPDefaultVolumeProfileSettings, UniversalRenderPipeline>(out var volumeSettings)
            || volumeSettings == null || !volumeSettings.volumeProfile)
            throw new Exception("URP default Volume Profile reference missing");
        var defaultVolume = volumeSettings.volumeProfile;
        var volumePath = AssetDatabase.GetAssetPath(defaultVolume);
        if (string.IsNullOrEmpty(volumePath) || !File.Exists(volumePath) || !File.Exists(volumePath + ".meta")
            || AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath) != defaultVolume
            || defaultVolume.components.Any(component => !component))
            throw new Exception("URP default Volume Profile is not persistent or contains a missing component");
        var result = new {
            passed = true, unity = Application.unityVersion, graphics = PipelinePath, renderer = RendererPath,
            activePipeline = RenderPipelineManager.currentPipeline?.GetType().FullName,
            globalSettings = globalPath, defaultVolumeProfile = volumePath, defaultVolumeComponentCount = defaultVolume.components.Count,
            rendererMode = renderer.renderingMode.ToString(), defaultRendererIndex = index.intValue, quality,
            intermediateTexture = renderer.intermediateTextureMode.ToString(), rendererFeatures = renderer.rendererFeatures.Count,
            depthPriming = renderer.depthPrimingMode.ToString(),
            hdr = pipeline.supportsHDR, msaa = pipeline.msaaSampleCount, renderScale = pipeline.renderScale,
            depthTexture = pipeline.supportsCameraDepthTexture, opaqueTexture = pipeline.supportsCameraOpaqueTexture,
            mainLights = pipeline.mainLightRenderingMode.ToString(), additionalLights = pipeline.additionalLightsRenderingMode.ToString(),
            srpBatcher = pipeline.useSRPBatcher, colorSpace = PlayerSettings.colorSpace.ToString(),
            macOSBuildSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX),
            webGLBuildSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)
        };
        Directory.CreateDirectory("Docs/Evidence/urp-20261023");
        File.WriteAllText("Docs/Evidence/urp-20261023/saved-pipeline.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
}
