using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using PortCargo.Core;
using PortCargo.Tests;
class Codec : IJsonCodec {
 readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
 public string Encode<T>(T value) => JsonSerializer.Serialize(value, options);
 public T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, options);
}
class Program {
 static void Main(string[] args) {
  var codec=new Codec();
  var catalog=codec.Decode<CatalogData>(File.ReadAllText(args[0]));
  if(args.Length==3 && args[1]=="--solve-endless") {
   var save=codec.Decode<PlayerSave>(File.ReadAllText(args[2]));
   ProgressValidator.Validate(save,LevelValidator.Load(catalog));
   var level=new LevelDefinition(save.endless.level);
   var run=GameSession.Restore(level,save.endless.run);
   using var solver=new Solver(level,run.Board,run.Capacity,run.Version,new SearchBudget(200000,10000));
   while(solver.Step(100)==SearchStatus.Searching){}
   if(solver.Status!=SearchStatus.Found)throw new Exception("Saved endless solve: "+solver.Status);
   Console.WriteLine(codec.Encode(solver.Solution));return;
  }
  if(args.Length==3 && args[1]=="--hard-fixture") {
   var levels=LevelValidator.Load(catalog);var generated=EndlessGenerator.Generate(20261018,60);
   var save=new PlayerSave{profile=new PlayerProfile{unlockedCount=200,tutorialCompleted=true,completed=levels.Take(199).Select(l=>l.Id).ToList()},
    activeRun=new GameSession(levels[199],"fixture-L200").Export(),mode="campaign",
    endless=new EndlessProgress{round=60,seed=generated.Seed,generatorVersion=generated.GeneratorVersion,level=generated.Data,run=new GameSession(new LevelDefinition(generated.Data),"fixture-endless60").Export()}};
   ProgressValidator.Validate(save,levels);var payload=codec.Encode(save);
   var envelope=new SaveEnvelope{saveSchema=1,revision=1,payload=payload,checksum=SaveRepository.Hash(payload)};
   Directory.CreateDirectory(args[2]);var target=Path.Combine(args[2],SaveRepository.Key(0));
   if(File.Exists(target))throw new Exception("Fixture already exists");
   File.WriteAllText(target,codec.Encode(envelope));Console.WriteLine("HARD fixture written "+target);return;
  }
  var report=RegressionSuite.Run(catalog,codec);
  foreach(var line in report) Console.WriteLine(line);
  Console.WriteLine("PASS "+report.Count+" groups; compiled shared production C# without Unity references");
 }
}
