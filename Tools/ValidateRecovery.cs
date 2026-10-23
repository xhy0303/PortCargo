using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using PortCargo.Core;
using PortCargo.Platform;
using PortCargo.Runtime;
public static class ValidateRecovery
{
 public static string Run()
 {
  var p=UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
  var codec=new UnityJsonCodec();
  var levels=LevelValidator.Load(codec.Decode<CatalogData>(File.ReadAllText("Assets/PortCargo/Data/Levels/catalog.json")));
  var tuning=AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/PortCargo/Data/GameTuning.asset");
  var originalRepo=(SaveRepository)typeof(GamePresenter).GetField("saves",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p);
  var original=codec.Decode<PlayerSave>(codec.Encode(new PlayerSave{profile=p.Profile,activeRun=p.Session.Export()}));
  var lines=new System.Collections.Generic.List<string>();
  try
  {
   var storage=new MemoryStoragePort();Func<SaveRepository> repo=()=>new SaveRepository(storage,codec,s=>CompositionRoot.ValidateSave(s,levels));
   var writer=repo();writer.Load();var sample=codec.Decode<PlayerSave>(codec.Encode(original));
   sample.activeRun.levelRevision++;
   if(!writer.Save(sample))throw new Exception("old revision fixture write failed");
   var changed=repo();p.Initialize(levels,tuning,changed,changed.Load());
   if(p.Session.Version!=0||p.Session.RunId==sample.activeRun.runId||p.Profile.unlockedCount!=original.profile.unlockedCount)throw new Exception("changed revision did not restart only active run");
   lines.Add("U08 PASS changed level revision restarts active run; unlock retained");
   // The previous initialize persisted a new valid run to B. Corrupt newest slot and recover older A.
   storage.WriteString(SaveRepository.Key(1),"broken newest");
   var fallback=repo();var loaded=fallback.Load();
   if(!loaded.RecoveredOlderSlot||loaded.Reset)throw new Exception("older valid slot not chosen");
   p.Initialize(levels,tuning,fallback,loaded);lines.Add("U08 PASS damaged newest slot recovers older valid snapshot in live presenter");
   storage.WriteString(SaveRepository.Key(0),"broken A");storage.WriteString(SaveRepository.Key(1),"broken B");
   var reset=repo();loaded=reset.Load();if(!loaded.Reset)throw new Exception("both corrupt reset flag");
   p.Initialize(levels,tuning,reset,loaded);
   if(p.Session.Level.Id!="L01"||p.Session.Version!=0||p.Profile.unlockedCount!=1)throw new Exception("both corrupt did not reset");
   lines.Add("U08 PASS both corrupt slots reset safely to L01");
   lines.Add("RESULT PASS");
  }
  finally{p.Initialize(levels,tuning,originalRepo,new LoadResult{Value=original});}
  File.WriteAllLines("Docs/Evidence/live-save-recovery.txt",lines);return string.Join("\n",lines);
 }
}
