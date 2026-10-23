using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using PortCargo.Core;

// Offline exhaustive screening. Heights determine removed cargo counts and normalized
// ship/buffer state; buffer order cannot affect feasibility. Memoization merges equivalent orders.
sealed class Difficulty
{
    readonly LevelDefinition level;
    readonly Dictionary<int,double> memo = new();
    public int WinningStates, TrapStates, LosingChoices, Nodes;
    public Difficulty(LevelDefinition level) { this.level = level; }
    public double Probability(BoardSnapshot board)
    {
        int key=0; foreach(var h in board.heights) key=key*7+h;
        if(memo.TryGetValue(key,out var cached)) return cached;
        if(board.shipIndex==level.Ships.Count) return 1;
        Nodes++;
        double sum=0; int legal=0, losing=0;
        for(int s=0;s<board.heights.Length;s++)
        {
            var top=GameRules.Top(level,board,s);
            if(top==null || GameRules.CanMove(level,board,3,top)!=RejectReason.None) continue;
            legal++; var p=Probability(GameRules.Apply(level,board,3,top).Board);
            sum+=p; if(p==0) losing++;
        }
        var result=legal==0?0:sum/legal;
        if(result>0) { WinningStates++; if(losing>0) { TrapStates++; LosingChoices+=losing; } }
        memo[key]=result; return result;
    }
}
class Program
{
    static readonly JsonSerializerOptions Json = new() { IncludeFields=true, WriteIndented=true };
    static string Signature(LevelData data)
    {
        var types=new Dictionary<string,int>();
        string Color(string c) { if(!types.ContainsKey(c))types[c]=types.Count; return types[c].ToString(); }
        var manifest=string.Join("/",data.ships.Select(s=>Color(s.type)+":"+s.capacity));
        return manifest+"|"+string.Join("/",data.stacks.Select(s=>string.Concat(s.boxes.Select(b=>Color(b.type)))).OrderBy(x=>x));
    }
    static void Main(string[] args)
    {
        var original=JsonSerializer.Deserialize<CatalogData>(File.ReadAllText(args[0]),Json);
        var all=original.levels.Take(50).ToList();
        if(all.Count!=50)throw new Exception("Expected original 50");
        var signatures=new HashSet<string>(all.Select(Signature));
        var metrics=new List<object>();
        var lines=new List<string>{"level,seed,round,boxes,colors,minBuffer,randomLegalWinProbability,winningStates,trapStates,losingChoices,states,solverNodes"};
        for(var number=51;number<=200;number++)
        {
            int round=number<=100?10:number<=150?30:60;
            double threshold=number<=100?.03:number<=150?.005:.001;
            GeneratedLevel selected=null; Difficulty measured=null; double probability=1; int selectedNodes=0;
            for(var attempt=0;attempt<3000;attempt++)
            {
                var candidate=EndlessGenerator.GenerateCandidate(unchecked(20261018+number*65537+attempt*104729),round);
                if(signatures.Contains(Signature(candidate.Data)))continue;
                var level=new LevelDefinition(candidate.Data);
                using(var low=new Solver(level,GameRules.Initial(level),2,0,new SearchBudget(200000,10000)))
                {
                    while(low.Step(100)==SearchStatus.Searching){}
                    if(low.Status!=SearchStatus.Unsolvable)continue;
                }
                using var solver=new Solver(level,GameRules.Initial(level),3,0,new SearchBudget(18000,10000));
                while(solver.Step(100)==SearchStatus.Searching){}
                if(solver.Status!=SearchStatus.Found)continue;
                var difficulty=new Difficulty(level); var p=difficulty.Probability(GameRules.Initial(level));
                if(p<=0 || p>threshold || difficulty.TrapStates<12)continue;
                selected=candidate; measured=difficulty; probability=p; selectedNodes=solver.Nodes; break;
            }
            if(selected==null)throw new Exception("No sufficiently hard candidate L"+number);
            var data=selected.Data;signatures.Add(Signature(data));
            var id="L"+number.ToString("00");data.id=id;
            foreach(var stack in data.stacks)foreach(var box in stack.boxes)box.id=box.id.Replace("E01",id);
            foreach(var ship in data.ships)ship.id=ship.id.Replace("E01",id);
            all.Add(data);
            var row=string.Join(",",id,selected.Seed,round,data.stacks.Sum(s=>s.boxes.Length),data.ships.Select(s=>s.type).Distinct().Count(),3,probability.ToString("G8",System.Globalization.CultureInfo.InvariantCulture),measured.WinningStates,measured.TrapStates,measured.LosingChoices,measured.Nodes,selectedNodes);
            lines.Add(row);Console.WriteLine(row);
        }
        var catalog=new CatalogData{catalogVersion=1,levels=all.ToArray()};LevelValidator.Load(catalog);
        File.WriteAllText(args[1],JsonSerializer.Serialize(catalog,Json)+"\n",new UTF8Encoding(false));
        File.WriteAllLines(args[2],lines,new UTF8Encoding(false));
        Console.WriteLine("PASS 200 levels; first 50 preserved; 150 unique hard layouts require all three buffer slots; exact random-policy metrics");
    }
}
