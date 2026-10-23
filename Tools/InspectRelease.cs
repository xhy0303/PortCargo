using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
public static class InspectRelease
{
 public static string Run()
 {
  var path="Builds/macOS/20261016-music-preview/PortCargo.app/Contents/Resources/Data/Managed/";
  var t=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Unity.Cecil").GetType("Mono.Cecil.AssemblyDefinition");
  var assembly=t.GetMethod("ReadAssembly",new[]{typeof(string)}).Invoke(null,new object[]{path+"PortCargo.Unity.dll"});
  var module=t.GetProperty("MainModule").GetValue(assembly);var types=(IEnumerable)module.GetType().GetProperty("Types").GetValue(module);
  var names=types.Cast<object>().Select(x=>x.ToString()).ToArray();
  var clean=!names.Any(x=>x.Contains("DevelopmentChecks"))&&!File.Exists(path+"Unity.Pipeline.dll")&&!File.Exists(path+"UnityPipeline.Microsoft.CodeAnalysis.dll");
  ((IDisposable)assembly).Dispose();
  var message="DevelopmentChecks absent; runtime Pipeline absent; Roslyn absent: "+clean+"\nRESULT "+(clean?"PASS":"FAIL");
  File.WriteAllText("Docs/Evidence/v0.4.1/release-code-inspection.txt",message);return message;
 }
}
