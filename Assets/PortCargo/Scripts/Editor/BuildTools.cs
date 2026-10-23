using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PortCargo.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PortCargo.Editor
{
    public sealed class ContentBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-100;
        public void OnPreprocessBuild(BuildReport report)
        {if(report.summary.platform==BuildTarget.WebGL)WeChatBuildTools.ValidateConfiguration();BuildTools.ValidateContent();}
    }

    [InitializeOnLoad]
    public static class BuildTools
    {
        static BuildTools(){EditorApplication.update+=ProcessRequest;}
        private static void ProcessRequest()
        {
            const string request="Temp/portcargo-build-request.txt";
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer||!File.Exists(request))return;
            var kind=File.ReadAllText(request);File.Delete(request);BuildMac(kind=="validation");
        }
        public static string ValidationEvidence="Docs/Evidence/v0.6";
        public const string CatalogPath="Assets/PortCargo/Data/Levels/catalog.json";
        [MenuItem("PortCargo/Validate Content")]
        public static void ValidateContent()
        {
            var text=File.ReadAllText(CatalogPath);
            var root=JObject.Parse(text,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            Keys(root,"catalogVersion","levels");
            foreach(var l in root["levels"])
            {
                Keys(l,"schemaVersion","id","revision","bufferCapacity","stacks","ships");
                foreach(var stack in l["stacks"]){Keys(stack,"id","boxes");foreach(var box in stack["boxes"])Keys(box,"id","type");}
                foreach(var ship in l["ships"])Keys(ship,"id","type","capacity");
            }
            var levels=LevelValidator.Load(JsonUtility.FromJson<CatalogData>(text));
            var evidence=new JArray();
            foreach(var level in levels)
            {
                using(var solver=new Solver(level,GameRules.Initial(level),3,0,new SearchBudget(200000,3000)))
                {
                    while(solver.Step(100)==SearchStatus.Searching){}
                    if(solver.Status!=SearchStatus.Found)throw new BuildFailedException(level.Id+": "+solver.Status);
                    var session=new GameSession(level,"build-validation");
                    foreach(var id in solver.Solution)
                    {
                        var move=session.TryMove(id,session.RunId,session.Version);
                        if(!move.Accepted)throw new BuildFailedException(level.Id+" replay rejected");
                    }
                    if(session.Status!=BoardStatus.Completed)throw new BuildFailedException(level.Id+" replay incomplete");
                    evidence.Add(new JObject{{"levelId",level.Id},{"status","Found"},{"nodes",solver.Nodes},{"solutionBoxIds",new JArray(solver.Solution)}});
                }
            }
            Directory.CreateDirectory(ValidationEvidence);File.WriteAllText(ValidationEvidence+"/validated-solutions.json",evidence.ToString());
            var regression=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PortCargo.Tests.RegressionSuite")).FirstOrDefault(t=>t!=null);
            if(regression==null)throw new BuildFailedException("Rule regression assembly missing; import EditMode tests before building.");
            var checks=(IEnumerable<string>)regression.GetMethod("Run").Invoke(null,new object[]{JsonUtility.FromJson<CatalogData>(text),new PortCargo.Platform.UnityJsonCodec()});
            File.WriteAllLines(ValidationEvidence+"/prebuild-rules.txt",checks);
        }

        private static void Keys(JToken token,params string[] allowed)
        {
            if(!(token is JObject obj))throw new BuildFailedException("JSON object expected");
            var expected=new HashSet<string>(allowed);
            foreach(var property in obj.Properties())if(!expected.Remove(property.Name))throw new BuildFailedException("Unknown field: "+property.Path);
            if(expected.Count!=0)throw new BuildFailedException("Missing fields: "+string.Join(",",expected));
        }

        [MenuItem("PortCargo/Build macOS Validation Player")]
        public static void QueueMacValidation()=>File.WriteAllText("Temp/portcargo-build-request.txt","validation");
        [MenuItem("PortCargo/Build macOS Preview")]
        public static void QueueMacPreview()=>File.WriteAllText("Temp/portcargo-build-request.txt","preview");

        public static void BuildMac(bool development)
        {
            var buildId="20261018-v0.6-"+(development?"validation":"preview");
            var directory="Builds/macOS/"+buildId;
            Directory.CreateDirectory(directory);
            try
            {
                ValidateContent();
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneOSX))throw new BuildFailedException("macOS module missing");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/PortCargo/Scenes/Bootstrap.unity"},
                    locationPathName=directory+"/PortCargo.app",target=BuildTarget.StandaloneOSX,
                    options=development?BuildOptions.Development:BuildOptions.None});
                var diagnostics=new JArray(report.steps.SelectMany(step=>step.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Warning||m.type==LogType.Exception).Select(m=>new JObject{{"type",m.type.ToString()},{"message",m.content}}));
                var manifest=new JObject{{"buildId",buildId},{"appVersion",PlayerSettings.bundleVersion},{"applicationId",PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone)},{"unity",Application.unityVersion},
                    {"changeset","e1dba0a9aba4"},{"cli","1.0.0-beta.12"},{"pipeline","0.8.0-exp.1"},
                    {"wechatSdkGitSha","a09d4b29daa1dd8358b09b5b5639554ab08cfdc2"},{"wechatIntegration","deferred by user"},{"development",development},{"adsEnabled",false},{"catalogSha256",SaveRepository.Hash(File.ReadAllText(CatalogPath))},
                    {"tuningSha256",SaveRepository.Hash(File.ReadAllText("Assets/PortCargo/Data/GameTuning.asset"))},
                    {"buildUtc",DateTime.UtcNow.ToString("o")},{"result",report.summary.result.ToString()},
                    {"diagnostics",diagnostics},{"errors",report.summary.totalErrors},{"warnings",report.summary.totalWarnings},{"bytes",(long)report.summary.totalSize},
                    {"wechatDeviceAccepted",false},{"gitCommit",null}};
                File.WriteAllText(directory+"/BuildManifest.json",manifest.ToString());
                File.WriteAllText("Docs/Evidence/v0.6/"+buildId+"-build.json",manifest.ToString());
                if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException(report.summary.result.ToString());
                Debug.Log("PORTCARGO_BUILD_PASS "+directory);
            }
            catch(Exception ex){File.WriteAllText("Docs/Evidence/v0.6/"+buildId+"-failure.txt",ex.ToString());Debug.LogError("PORTCARGO_BUILD_FAILED "+ex);}
        }
    }
}
