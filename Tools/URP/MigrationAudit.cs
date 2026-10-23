using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MigrationAudit
{
    public static object Run(string label)
    {
        if (EditorApplication.isPlaying) throw new Exception("Audit requires Edit Mode");
        var active = SceneManager.GetActiveScene();
        if (active.isDirty) throw new Exception("Save user scene changes before migration audit");
        var sceneRows = new List<object>();
        foreach (var path in new[] { "Assets/PortCargo/Scenes/Bootstrap.unity", "Assets/Scenes/SampleScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c).ToArray();
            Lightmapping.TryGetLightingSettings(out var lighting);
            sceneRows.Add(new {
                path, missingScripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                cameras = components.OfType<Camera>().Select(c => new { c.name, c.orthographic, c.allowHDR, c.allowMSAA, c.clearFlags, components = c.GetComponents<Component>().Select(x => x.GetType().FullName).ToArray() }).ToArray(),
                canvases = components.OfType<Canvas>().Select(c => new { c.name, mode = c.renderMode.ToString() }).ToArray(),
                renderers = components.OfType<Renderer>().Select(r => new { r.name, type = r.GetType().Name, materials = r.sharedMaterials.Select(m => m ? m.shader.name : "MISSING").ToArray() }).ToArray(),
                lights = components.OfType<Light>().Select(l => new { l.name, type = l.type.ToString(), bake = l.lightmapBakeType.ToString() }).ToArray(),
                particles = components.OfType<ParticleSystem>().Count(), probes = components.OfType<ReflectionProbe>().Count(), lightProbeGroups = components.OfType<LightProbeGroup>().Count(),
                postProcessing = components.Where(c => c.GetType().FullName.Contains("PostProcess") || c.GetType().FullName == "UnityEngine.Rendering.Volume").Select(c => c.GetType().FullName).ToArray(),
                lightmaps = LightmapSettings.lightmaps.Length,
                lightingSettings = lighting ? AssetDatabase.GetAssetPath(lighting) : null,
                lightingData = Lightmapping.lightingDataAsset ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) : null
            });
            SceneManager.SetActiveScene(active);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        var materialRows = new List<object>();
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
        foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
        {
            var props = new Dictionary<string, object>();
            foreach (var key in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_SpecGlossMap", "_EmissionMap" })
                if (mat.HasProperty(key)) props[key] = new { texture = AssetDatabase.GetAssetPath(mat.GetTexture(key)), scale = mat.GetTextureScale(key).ToString(), offset = mat.GetTextureOffset(key).ToString() };
            foreach (var key in new[] { "_Color", "_SpecColor", "_EmissionColor" }) if (mat.HasProperty(key)) props[key] = mat.GetColor(key).ToString();
            foreach (var key in new[] { "_Metallic", "_Glossiness", "_Cutoff", "_Mode" }) if (mat.HasProperty(key)) props[key] = mat.GetFloat(key);
            materialRows.Add(new { path, mat.name, guid = AssetDatabase.AssetPathToGUID(path), shader = mat.shader ? mat.shader.name : "MISSING", supported = mat.shader && mat.shader.isSupported, properties = props });
        }
        var quality = Enumerable.Range(0, QualitySettings.names.Length).Select(i => new { name = QualitySettings.names[i], asset = AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i)) }).ToArray();
        var result = new { label, version = Application.unityVersion, graphics = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), quality, scenes = sceneRows, materials = materialRows };
        Directory.CreateDirectory("Docs/Evidence/urp-20261023");
        File.WriteAllText("Docs/Evidence/urp-20261023/" + label + "-audit.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
}
