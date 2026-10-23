using System;
using System.IO;
using PortCargo.Core;
using UnityEngine;

namespace PortCargo.Platform
{
    public sealed class UnityJsonCodec : IJsonCodec
    {
        // Unity's inline serializer creates empty nested classes for missing/null fields.
        // Omit the optional endless object until it exists, and restore absence on decode.
        [Serializable] private sealed class CampaignOnlySave
        { public PlayerProfile profile; public RunData activeRun; public string mode; }
        public string Encode<T>(T value)
        {
            if(value is PlayerSave save && save.endless==null)
                return JsonUtility.ToJson(new CampaignOnlySave{profile=save.profile,activeRun=save.activeRun,mode=save.mode});
            return JsonUtility.ToJson(value);
        }
        public T Decode<T>(string text)
        {
            var value=JsonUtility.FromJson<T>(text);
            if(value is PlayerSave save)
            {
                // Field names in encoded string values have escaped quotes and cannot match.
                var optional=System.Text.RegularExpressions.Regex.Match(text,@"(?<!\\)""endless""\s*:\s*(?<value>[^\s])");
                if(!optional.Success||optional.Groups["value"].Value=="n")save.endless=null;
            }
            return value;
        }
    }

    /// <summary>Editor/macOS only. WeChat must use its verified SDK storage adapter.</summary>
    public sealed class FileStoragePort : IStoragePort
    {
        private readonly string directory;
        public FileStoragePort(string directory) { this.directory = directory; Directory.CreateDirectory(directory); }
        public string ReadString(string key) { var path = Path.Combine(directory, key); return File.Exists(path) ? File.ReadAllText(path) : null; }
        public void WriteString(string key, string value) { File.WriteAllText(Path.Combine(directory, key), value); }
    }

    /// <summary>Explicit non-persistent fallback, used only when a platform adapter is unavailable.</summary>
    public sealed class MemoryStoragePort : IStoragePort
    {
        private readonly System.Collections.Generic.Dictionary<string, string> values = new System.Collections.Generic.Dictionary<string, string>();
        public string ReadString(string key) => values.TryGetValue(key, out var value) ? value : null;
        public void WriteString(string key, string value) => values[key] = value;
    }

    public enum AdResult { Completed, Cancelled, Unavailable, Failed }
    public interface IAdService { void ShowRewarded(string requestId, Action<AdResult> done); }
    public sealed class DisabledAdService : IAdService
    {
        public void ShowRewarded(string requestId, Action<AdResult> done) => done(AdResult.Unavailable);
    }
}
