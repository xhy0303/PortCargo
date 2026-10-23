using System;
using System.IO;
using UnityEngine;
using PortCargo.Core;
using PortCargo.Platform;
// Deliberately encodes the v0.1 payload without the new fields.
public static class CreateExpansionFixture
{
    [Serializable] public sealed class LegacySave { public PlayerProfile profile; public RunData activeRun; }
    public static string Run(string directory)
    {
        if(!Path.GetFullPath(directory).StartsWith("/tmp/portcargo-v02-")&&!Path.GetFullPath(directory).StartsWith("/private/tmp/portcargo-v02-"))throw new Exception("Isolated temporary fixture directory required");
        var codec=new UnityJsonCodec();
        var levels=LevelValidator.Load(codec.Decode<CatalogData>(File.ReadAllText("Assets/PortCargo/Data/Levels/catalog.json")));
        var session=new GameSession(levels[9],"legacy-expansion-fixture");
        using(var solver=new Solver(session.Level,session.Board,3,0,new SearchBudget(200000,3000)))
        {
            while(solver.Step(100)==SearchStatus.Searching){}
            if(solver.Status!=SearchStatus.Found)throw new Exception("L10 fixture solution missing");
            foreach(var id in solver.Solution)if(!session.TryMove(id,session.RunId,session.Version).Accepted)throw new Exception("Fixture move rejected");
        }
        var profile=new PlayerProfile{unlockedCount=10};
        for(var i=0;i<10;i++)profile.completed.Add(levels[i].Id);
        var payload=codec.Encode(new LegacySave{profile=profile,activeRun=session.Export()});
        var envelope=new SaveEnvelope{saveSchema=1,revision=1,payload=payload,checksum=SaveRepository.Hash(payload)};
        Directory.CreateDirectory(directory);
        if(File.Exists(Path.Combine(directory,SaveRepository.Key(0)))||File.Exists(Path.Combine(directory,SaveRepository.Key(1))))throw new Exception("Do not overwrite existing fixture");
        new FileStoragePort(directory).WriteString(SaveRepository.Key(0),codec.Encode(envelope));
        return "v0.1 legacy L10 completed fixture: "+directory;
    }
}
