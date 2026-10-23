using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using WeChatWASM;

namespace PortCargo.Editor
{
    [InitializeOnLoad]
    public static class WeChatBuildTools
    {
        public const string AppId="wx19e83e7294013864";
        public const string Output="Builds/WeChat/20261018-v0.6.1";
        public const string Evidence="Docs/Evidence/v0.6.1/wechat";
        private const string Request="Temp/portcargo-wechat-request.txt";
        static WeChatBuildTools(){EditorApplication.update+=Process;}
        [MenuItem("PortCargo/Configure WeChat Preview")]
        public static void Configure()
        {
            Directory.CreateDirectory(Evidence);
            var c=WXConvertCore.config;
            c.ProjectConf.Appid=AppId;c.ProjectConf.projectName="港口装箱";
            c.ProjectConf.relativeDST=Output;c.ProjectConf.DST=Path.GetFullPath(Output);
            c.ProjectConf.assetLoadType=1;c.ProjectConf.compressDataPackage=true;
            c.ProjectConf.CDN="";c.ProjectConf.MemorySize=256;
            c.ProjectConf.Orientation=WXScreenOritation.Portrait;
            c.SDKOptions.disableMultiTouch=true;
            c.CompileOptions.DevelopBuild=false;c.CompileOptions.AutoProfile=false;
            c.CompileOptions.ScriptOnly=false;c.CompileOptions.Il2CppOptimizeSize=true;
            c.CompileOptions.profilingFuncs=false;c.CompileOptions.ProfilingMemory=false;
            c.CompileOptions.Webgl2=true;c.CompileOptions.fbslim=false;
            c.CompileOptions.brotliMT=false;
            c.CompileOptions.showMonitorSuggestModal=false; // Keep monitoring/logs without interrupting play.
            c.CompileOptions.enableRenderThread=false;c.CompileOptions.enableEmscriptenGLX=false;
            c.CompileOptions.enableiOSMetal=false;c.CompileOptions.enableIOSPerformancePlus=false;
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();
            WXConvertCore.Init();
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL,ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            var symbols=PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL);
            if(!Array.Exists(symbols.Split(';'),s=>s=="PORT_WECHAT"))PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL,symbols+";PORT_WECHAT");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/PortCargo/Scenes/Bootstrap.unity",true)};
            File.WriteAllText(Evidence+"/sdk-config.json",JsonUtility.ToJson(c,true));
        }
        [MenuItem("PortCargo/Build WeChat Preview")]
        public static void Queue()=>File.WriteAllText(Request,"export");
        public static void ValidateConfiguration()
        {
            if(WXConvertCore.config.ProjectConf.Appid!=AppId)throw new BuildFailedException("WeChat AppID mismatch");
            if(!PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL).Contains("PORT_WECHAT"))throw new BuildFailedException("Missing PORT_WECHAT");
            if(!File.Exists("Assets/PortCargo/Plugins/WebGL/PortCargoStorage.jslib"))throw new BuildFailedException("Missing native storage adapter");
            BuildTools.ValidationEvidence=Evidence;
        }
        private static void Process()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)return;
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL,BuildTarget.WebGL);return;
            }
            File.Delete(Request);
            try
            {
                ValidateConfiguration();BuildTools.ValidateContent();
                var result=WXConvertCore.DoExport();
                File.WriteAllText(Evidence+"/export-result.txt",DateTime.UtcNow.ToString("o")+" "+result);
                if(result!=WXConvertCore.WXExportError.SUCCEED)throw new BuildFailedException(result.ToString());
                Debug.Log("PORTCARGO_WECHAT_EXPORT_PASS");
            }
            catch(Exception ex){File.WriteAllText(Evidence+"/export-failure.txt",ex.ToString());Debug.LogError("PORTCARGO_WECHAT_EXPORT_FAILED "+ex);}
        }
    }
}
