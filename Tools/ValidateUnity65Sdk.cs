// Run via Unity Pipeline eval_file in Edit Mode; no imported test assembly needed.
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var bridge = typeof(WeChatWASM.WXRuntimeExtDef);
bridge.GetMethod("RegisterController", flags).Invoke(null, null);
var runtimeRef = System.Linq.Enumerable.Single(bridge.Assembly.GetReferencedAssemblies(), r => r.Name == "wx-runtime-editor" || r.Name == "wx-runtime");
var env = System.Reflection.Assembly.Load(runtimeRef).GetType("WeChatWASM.WXRuntimeExtEnvDef");
var goTo = env.GetMethod("GOTO", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
System.Func<object, object> sdkId = obj => goTo.Invoke(null, new object[] { "Unity.GetObjectInstanceID", obj });
var first = new UnityEngine.GameObject("SdkCompatibilityProbeA");
var second = new UnityEngine.GameObject("SdkCompatibilityProbeB");
first.hideFlags = second.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
var map = (System.Collections.Generic.Dictionary<UnityEngine.EntityId, int>)bridge.GetField("SdkObjectIds", flags).GetValue(null);
var highA = UnityEngine.EntityId.FromULong(0x1234567800000001UL);
var highB = UnityEngine.EntityId.FromULong(0x2345678900000001UL);
if (map.ContainsKey(highA) || map.ContainsKey(highB)) throw new System.Exception("Synthetic probe IDs already in use");
try
{
    var a = sdkId(first);
    var repeat = sdkId(first);
    var b = sdkId(second);
    if (!(a is int) || !(b is int) || (int)a == 0 || (int)a == (int)b || !a.Equals(repeat))
        throw new System.Exception("SDK Int32 ABI / identity check failed");
    if (!sdkId(null).Equals(0))
        throw new System.Exception("Null sentinel changed");
    bridge.GetMethod("RegisterController", flags).Invoke(null, null);
    if (!a.Equals(sdkId(first)))
        throw new System.Exception("Reinitialization changed a live SDK token");
    var get = bridge.GetMethod("GetSdkObjectId", flags);
    var upperA = (int)get.Invoke(null, new object[] { highA });
    var upperB = (int)get.Invoke(null, new object[] { highB });
    if (upperA == upperB || upperA == 0 || upperB == 0)
        throw new System.Exception("64-bit identifiers collided");
    var result = new { passed = true, editor = UnityEngine.Application.unityVersion,
        boxedInt32 = true, stable = true, distinctObjects = true, nullIsZero = true,
        reinitializationStable = true, sameLow32BitsRemainDistinct = true };
    System.IO.File.WriteAllText("Docs/Evidence/unity-6000.5.10-20261023/sdk-contract.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
    return result;
}
finally
{
    map.Remove(highA); map.Remove(highB);
    map.Remove(first.GetEntityId()); map.Remove(second.GetEntityId());
    UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
}
