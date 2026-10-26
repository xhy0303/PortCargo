using System;
using System.Collections.Generic;
using System.Linq;
using PortCargo.Core;

namespace PortCargo.Tests
{
    public static class RegressionSuite
    {
        public static List<string> Run(CatalogData catalog, IJsonCodec codec)
        {
            var result = new List<string>();
            var levels = LevelValidator.Load(catalog);
            foreach (var level in levels)
            {
                using (var solver = new Solver(level, GameRules.Initial(level), 3, 0, new SearchBudget(200000, 3000)))
                {
                    while (solver.Step(100) == SearchStatus.Searching) { }
                    Check(solver.Status == SearchStatus.Found, "R01 solve " + level.Id);
                    var first = new GameSession(level, "replay-one");
                    var second = new GameSession(level, "replay-two");
                    foreach (var id in solver.Solution)
                    {
                        var a = first.TryMove(id, first.RunId, first.Version);
                        var b = second.TryMove(id, second.RunId, second.Version);
                        Check(a.Accepted && b.Accepted && a.Board.ExactKey() == b.Board.ExactKey(), "R10 deterministic");
                        GameRules.Validate(level, a.Board, 3);
                    }
                    Check(first.Status == BoardStatus.Completed && solver.Solution.Length == level.BoxCount, "R01 completion");
                    result.Add("R01/R10 " + level.Id + " PASS nodes=" + solver.Nodes + " solutionBoxIds=" + string.Join(",", solver.Solution));
                }
            }
            var l4 = levels[3];
            var session = new GameSession(l4, "run");
            var initial = session.Board.ExactKey();
            Check(session.TryMove(l4.Stacks[0].Boxes[0].Id, "run", 0).Reason == RejectReason.NotTop, "R02 covered");
            Check(session.TryMove("absent", "run", 0).Reason == RejectReason.UnknownBox, "R02 absent");
            Check(session.Board.ExactKey() == initial && session.Export().undo.Count == 0 && session.Version == 0, "R02 unchanged");
            result.Add("R02 PASS covered, absent and rejected state unchanged");
            MoveStack(session, 0);
            Check(session.TryMove(GameRules.Top(l4, session.Board, 0), "run", 0).Reason == RejectReason.StaleVersion, "R12 stale");
            Check(session.TryMove(GameRules.Top(l4, session.Board, 0), "old-run", session.Version).Reason == RejectReason.WrongRun, "R08 old run");
            MoveStack(session, 0); MoveStack(session, 1);
            var beforeChain = session.Board;
            var last = session.TryMove(GameRules.Top(l4, beforeChain, 1), session.RunId, session.Version);
            Check(last.Events.Count(e => e.Kind == EventKind.ShipDeparted) == 2 && session.Status == BoardStatus.Completed, "R06 chain");
            Check(session.Export().undo.Last().ExactKey() == beforeChain.ExactKey(), "R06 pre-chain snapshot");
            Check(!session.TryUndo(session.RunId, session.Version), "completed undo disabled");
            result.Add("R06/R08/R12 PASS atomic two-ship chain, saved snapshot, stale version/run");

            var synthetic = MakeLevel(new[] { "RB", "RB", "RG", "B" }, "R3,B3,G1");
            session = new GameSession(synthetic, "capacity");
            MoveStack(session, 0); MoveStack(session, 1); MoveStack(session, 2);
            Check(session.Board.buffer.Count == 3 && session.Status == BoardStatus.Playing, "R03 full not deadlocked");
            Check(session.TryMove(GameRules.Top(synthetic, session.Board, 3), session.RunId, session.Version).Reason == RejectReason.BufferFull, "full reject");
            MoveStack(session, 0); MoveStack(session, 1); MoveStack(session, 2);
            Check(session.Board.shipIndex == 1 && session.Board.shipLoad == 2 && session.Board.buffer.Count == 1, "R05 skip mismatched buffer");
            result.Add("R03/R05 PASS full-buffer direct load and earliest matching scan");

            var dead = MakeLevel(new[] { "RBBBB" }, "R1,B4");
            session = new GameSession(dead, "dead");
            MoveStack(session, 0); MoveStack(session, 0); MoveStack(session, 0);
            Check(session.Status == BoardStatus.Deadlocked && session.TryUndo(session.RunId, session.Version), "R04 deadlock undo");
            using (var solver = new Solver(dead, GameRules.Initial(dead), 3, 0, new SearchBudget(20000, 2000)))
            {
                while (solver.Step(100) == SearchStatus.Searching) { }
                Check(solver.Status == SearchStatus.Unsolvable, "R09 proven unsolvable");
            }
            using (var solver = new Solver(l4, GameRules.Initial(l4), 3, 0, new SearchBudget(0, 2000)))
                Check(solver.Step(100) == SearchStatus.Unknown, "R09 budget unknown");
            result.Add("R04/R09 PASS deadlock recovery; Unknown differs from Unsolvable");

            session = new GameSession(l4, "rights");
            MoveStack(session, 0);
            var top = GameRules.Top(l4, session.Board, 0);
            Check(session.UseHint(session.RunId, session.Version, top, false), "free hint");
            Check(session.Expand(session.RunId, session.Version), "expansion");
            Check(session.TryUndo(session.RunId, session.Version) && session.FreeHintUsed && session.Capacity == 4, "R07 rights survive undo");
            var restore = GameSession.Restore(l4, session.Export());
            Check(restore.Capacity == 4 && restore.FreeHintUsed, "rights restore");
            var oldRun = session.RunId;
            session.Restart("new-rights");
            Check(session.Capacity == 3 && !session.FreeHintUsed && !session.Expand(oldRun, session.Version), "R08 restart rights");
            result.Add("R07/R08 PASS hint and expansion survive undo, reset on restart, old rewards rejected");

            var invalid = codec.Decode<CatalogData>(codec.Encode(catalog));
            invalid.levels[0].stacks[1].boxes[0].id = invalid.levels[0].stacks[0].boxes[0].id;
            Throws(() => LevelValidator.Load(invalid), "R11 duplicate ID");
            invalid = codec.Decode<CatalogData>(codec.Encode(catalog)); invalid.levels[0].ships[0].capacity++;
            Throws(() => LevelValidator.Load(invalid), "R11 wrong capacity total");
            invalid = codec.Decode<CatalogData>(codec.Encode(catalog)); invalid.levels[0].stacks[0].boxes[0].type = "X";
            Throws(() => LevelValidator.Load(invalid), "R11 enum");
            invalid = codec.Decode<CatalogData>(codec.Encode(catalog)); invalid.levels[0].stacks[0].boxes = Array.Empty<BoxData>();
            Throws(() => LevelValidator.Load(invalid), "R11 empty stack");
            result.Add("R11 PASS duplicate IDs, cargo mismatch, unknown type, empty stack");
            TestStorage(l4, codec, result);
            TestProgress(catalog, levels, codec, result);
            TestEndlessGeneration(codec, result);
            TestRandomEndless(catalog, codec, result);
            TestHardExpansion(catalog, levels, codec, result);
            TestRewardedHint(l4, codec, result);
            TestRewardedSkip(l4, codec, result);
            return result;
        }

        private static void TestEndlessGeneration(IJsonCodec codec, List<string> result)
        {
            var layouts = new HashSet<string>();
            var rounds = new[] { 1, 2, 3, 4, 5, 6, 8, 10, 50, 101, int.MaxValue };
            var fallbacks = 0;
            for (var i = 0; i < 32; i++)
            {
                var seed = i == 0 ? 0 : i == 1 ? int.MinValue : i == 2 ? int.MaxValue : unchecked(i * 7919 - 100000);
                var round = rounds[i % rounds.Length];
                var generated = EndlessGenerator.Generate(seed, round);
                var repeated = EndlessGenerator.Generate(seed, round);
                Check(generated.Seed == seed && generated.GeneratorVersion == EndlessGenerator.Version
                    && generated.Data.id == "E01" && codec.Encode(generated.Data) == codec.Encode(repeated.Data)
                    && generated.Solution.SequenceEqual(repeated.Solution), "deterministic endless seed and round");
                var level = new LevelDefinition(generated.Data);
                Check(level.BoxCount >= 12 && level.BoxCount <= 36 && generated.Solution.Length == level.BoxCount,
                    "bounded endless board");
                var session = new GameSession(level, "generated-" + i);
                for (var moveIndex = 0; moveIndex < generated.Solution.Length; moveIndex++)
                {
                    Check(session.TryMove(generated.Solution[moveIndex], session.RunId, session.Version).Accepted,
                        "generated witness accepted by production rules");
                    if (moveIndex == generated.Solution.Length / 2)
                    {
                        var saved = codec.Decode<RunData>(codec.Encode(session.Export()));
                        session = GameSession.Restore(level, saved);
                        Check(session.CanUndo, "generated mid-round restore retains undo");
                    }
                }
                Check(session.Status == BoardStatus.Completed, "generated witness completes round");
                layouts.Add(string.Join("/", generated.Data.stacks.Select(stack => string.Concat(stack.boxes.Select(box => box.type))))
                    + "|" + string.Join(",", generated.Data.ships.Select(ship => ship.type + ship.capacity)));
                if (generated.UsedFallback) fallbacks++;
            }
            Check(layouts.Count >= 24, "endless boards vary in structure or cargo order");
            result.Add("ENDLESS PASS 32 deterministic seeds/rounds, production-rule completion, mid-round restore; layouts="
                + layouts.Count + " fallback=" + fallbacks);
        }

        private static void TestRandomEndless(CatalogData catalog, IJsonCodec codec, List<string> result)
        {
            var templates = new HashSet<string>(catalog.levels.Select(EndlessGenerator.LayoutSignature));
            var all = new HashSet<string>(); var recent = new List<string>();
            var fresh = 0; var fallback = 0;
            for (var i = 0; i < 140; i++)
            {
                var seed = unchecked(190019 + i * 104729);
                var round = i % 3 == 0 ? 7 : i % 3 == 1 ? 30 : 60;
                var generated = EndlessGenerator.Generate(seed, round, recent);
                var signature = EndlessGenerator.LayoutSignature(generated.Data);
                Check(!recent.Contains(signature) && all.Add(signature), "canonical random layout uniqueness");
                if (!templates.Contains(signature)) fresh++;
                if (generated.UsedFallback) fallback++;
                recent.Add(signature);
                while (recent.Count > EndlessGenerator.RecentLimit) recent.RemoveAt(0);
                var level = new LevelDefinition(generated.Data);
                var analysis = new EndlessLayoutAnalysis(generated.Data);
                foreach (var capacity in new[] { 2, 3 })
                {
                    using (var solver = new Solver(level, GameRules.Initial(level), capacity, 0, new SearchBudget(200000, 10000)))
                    {
                        while (solver.Step(100) == SearchStatus.Searching) { }
                        Check(solver.Status != SearchStatus.Unknown && analysis.CanSolve(capacity) == (solver.Status == SearchStatus.Found),
                            "compact generator analysis agrees with independent production solver");
                    }
                }
                var session = new GameSession(level, "random-replay-" + i);
                foreach (var id in generated.Solution)
                    Check(session.TryMove(id, session.RunId, session.Version).Accepted, "fresh random witness replay");
                Check(session.Status == BoardStatus.Completed, "fresh random completion");
                if (i == 0)
                {
                    var blocked = EndlessGenerator.Generate(seed, round, recent);
                    Check(EndlessGenerator.LayoutSignature(blocked.Data) != signature, "blocked seed rerolls structurally");
                    var copy = codec.Decode<LevelData>(codec.Encode(generated.Data));
                    Array.Reverse(copy.stacks);
                    Func<string, string> rename = color => "BGYR"["RBGY".IndexOf(color)].ToString();
                    foreach (var stack in copy.stacks) foreach (var box in stack.boxes) box.type = rename(box.type);
                    foreach (var ship in copy.ships) ship.type = rename(ship.type);
                    Check(EndlessGenerator.LayoutSignature(copy) == signature, "color and stack permutations share canonical hash");
                }
            }
            Check(fresh == 140 && recent.Count == EndlessGenerator.RecentLimit, "new layouts exceed template pool; recent history bounded");
            // Compare exact probability against the actual rule engine on tractable small boards.
            for (var i = 0; i < 12; i++)
            {
                var generated = EndlessGenerator.GenerateCandidate(i * 65537, 1);
                var level = new LevelDefinition(generated.Data);
                var probability = RuleProbability(level, GameRules.Initial(level), new Dictionary<string, double>());
                Check(Math.Abs(probability - new EndlessLayoutAnalysis(generated.Data).WinProbability()) < 1e-12,
                    "compact exact probability agrees with production-rule enumeration");
            }
            var saved = new EndlessProgress { recentLayouts = recent };
            Check(codec.Decode<EndlessProgress>(codec.Encode(saved)).recentLayouts.SequenceEqual(recent), "canonical history serialization");
            var campaign = LevelValidator.Load(catalog);
            var old = codec.Decode<LevelData>(codec.Encode(catalog.levels[199])); old.id = "E01"; old.revision = 2;
            var oldRun = new GameSession(new LevelDefinition(old), "v2-active"); MoveStack(oldRun, 0);
            var save = new PlayerSave { activeRun = new GameSession(campaign[0], "history-test").Export(), mode = "endless",
                endless = new EndlessProgress { generatorVersion = 2, seed = 20261018, round = 60,
                    level = old, run = oldRun.Export(), recentLayouts = null } };
            Action<PlayerSave> validate = value => ProgressValidator.Validate(value, campaign);
            var storage = new FaultStorage(); var repository = new SaveRepository(storage, codec, validate); repository.Load();
            Check(repository.Save(save), "old v2 layout with absent history accepted");
            var restored = new SaveRepository(storage, codec, validate).Load().Value;
            Check(restored.endless.generatorVersion == 2 && restored.endless.level.revision == 2
                && restored.endless.run.board.ExactKey() == oldRun.Board.ExactKey()
                && GameSession.Restore(new LevelDefinition(restored.endless.level), restored.endless.run).CanUndo,
                "old v2 board and undo preserved without regeneration");
            save.endless.recentLayouts = recent;
            Check(repository.Save(save), "full 128 history fits validated save envelope");
            restored = new SaveRepository(storage, codec, validate).Load().Value;
            Check(restored.endless.recentLayouts.SequenceEqual(recent), "128 history survives repository reload");
            var bad = codec.Decode<PlayerSave>(codec.Encode(save)); bad.endless.recentLayouts.Add(recent[0]);
            Throws(() => validate(bad), "oversized history rejected");
            bad = codec.Decode<PlayerSave>(codec.Encode(save)); bad.endless.recentLayouts[0] = "invalid";
            Throws(() => validate(bad), "invalid history hash rejected");
            result.Add("RANDOM ENDLESS PASS 140 novel canonical layouts; rolling 128 history; blocked seed reroll; permutation-invariant hashes; 280 independent feasibility comparisons; 12 exact probability comparisons; production witness replay; fallback=" + fallback);
        }

        private static double RuleProbability(LevelDefinition level, BoardSnapshot board, Dictionary<string, double> memo)
        {
            var key = string.Join(",", board.heights);
            if (memo.TryGetValue(key, out var cached)) return cached;
            if (GameRules.Status(level, board, 3) == BoardStatus.Completed) return 1;
            var legal = 0; double sum = 0;
            for (var s = 0; s < level.Stacks.Count; s++)
            {
                var top = GameRules.Top(level, board, s);
                if (top == null || GameRules.CanMove(level, board, 3, top) != RejectReason.None) continue;
                legal++; sum += RuleProbability(level, GameRules.Apply(level, board, 3, top).Board, memo);
            }
            return memo[key] = legal == 0 ? 0 : sum / legal;
        }

        private static void TestProgress(CatalogData catalog, LevelDefinition[] levels, IJsonCodec codec, List<string> result)
        {
            var campaign = new GameSession(levels[3], "campaign-preserved");
            MoveStack(campaign, 0);
            var profile = new PlayerProfile { unlockedCount = 10, completed = new List<string> { "L01", "L02" } };
            // This is the v0.1 JSON shape: the new mode/endless fields did not exist at all.
            var legacyJson = "{\"profile\":" + codec.Encode(profile) + ",\"activeRun\":" + codec.Encode(campaign.Export()) + "}";
            var legacy = codec.Decode<PlayerSave>(legacyJson);
            ProgressValidator.Validate(legacy, levels);
            Check(string.IsNullOrEmpty(legacy.mode) && legacy.endless == null
                && legacy.activeRun.board.ExactKey() == campaign.Board.ExactKey(), "legacy progress fields");

            var data = codec.Decode<LevelData>(codec.Encode(catalog.levels[3]));
            data.id = "E01";
            var endlessSession = new GameSession(new LevelDefinition(data), "endless-preserved");
            MoveStack(endlessSession, 0);
            var save = new PlayerSave { profile = profile, activeRun = campaign.Export(), mode = "endless",
                endless = new EndlessProgress { generatorVersion = 1, seed = int.MinValue, round = 4,
                    level = data, run = endlessSession.Export() } };
            ProgressValidator.Validate(save, levels);
            var storage = new FaultStorage();
            Action<PlayerSave> validate = value => ProgressValidator.Validate(value, levels);
            var repository = new SaveRepository(storage, codec, validate);
            repository.Load();
            Check(repository.Save(legacy) && repository.Save(save), "both-mode save");
            var loaded = new SaveRepository(storage, codec, validate).Load().Value;
            Check(loaded.mode == "endless" && loaded.activeRun.board.ExactKey() == campaign.Board.ExactKey()
                && loaded.endless.run.board.ExactKey() == endlessSession.Board.ExactKey()
                && loaded.profile.unlockedCount == 10 && loaded.profile.completed.SequenceEqual(profile.completed),
                "endless round preserves campaign state");
            loaded.mode = "campaign";
            Check(repository.Save(loaded), "switch back save");
            loaded = new SaveRepository(storage, codec, validate).Load().Value;
            Check(loaded.mode == "campaign" && loaded.endless.run.runId == "endless-preserved"
                && GameSession.Restore(levels[3], loaded.activeRun).CanUndo, "mode switch preserves both undo histories");

            var invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.endless.run.board.heights[0] = 999;
            Throws(() => validate(invalid), "tampered endless board");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.endless.level.ships[0].capacity++;
            Throws(() => validate(invalid), "tampered endless definition");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.endless.round = 0;
            Throws(() => validate(invalid), "invalid endless round");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.endless = null;
            Throws(() => validate(invalid), "missing active endless round");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.profile.completed.Add("E01");
            Throws(() => validate(invalid), "endless must not unlock campaign levels");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.mode = "unsupported";
            Throws(() => validate(invalid), "unknown mode");
            invalid = codec.Decode<PlayerSave>(codec.Encode(save));
            invalid.profile.unlockedCount = levels.Length+1;
            Throws(() => validate(invalid), "unlocked campaign bound");

            var future = codec.Decode<PlayerSave>(codec.Encode(save));
            future.endless.generatorVersion = 999;
            future.endless.round = int.MaxValue;
            validate(future);
            Check(GameSession.Restore(new LevelDefinition(future.endless.level), future.endless.run).Board.ExactKey()
                == endlessSession.Board.ExactKey(), "stored endless definition survives generator version changes");
            var revised = codec.Decode<PlayerSave>(codec.Encode(legacy));
            revised.activeRun.levelRevision++;
            revised.activeRun.board = null;
            validate(revised);
            revised.activeRun.levelId = "L99";
            ProgressValidator.Validate(revised, levels.Take(10).ToArray());
            result.Add("PROGRESS PASS legacy fields, both-mode A/B restore, undo preservation, corruption rejection and future generator definitions");
        }

        private static void TestHardExpansion(CatalogData catalog, LevelDefinition[] levels, IJsonCodec codec, List<string> result)
        {
            Check(levels.Length==200 && levels[99].Id=="L100" && levels[199].Id=="L200","200 campaign IDs");
            foreach(var level in levels.Skip(50))
            {
                using(var solver=new Solver(level,GameRules.Initial(level),2,0,new SearchBudget(200000,10000)))
                {while(solver.Step(100)==SearchStatus.Searching){} Check(solver.Status==SearchStatus.Unsolvable,"hard level requires buffer 3 "+level.Id);}
            }
            var worstNodes=0;double slowest=0;
            // Fresh random samples in each tier, including late non-reset rounds.
            foreach(var start in new[]{7,20,50})for(int i=0;i<50;i++)
            {
                var round=start==7?7:start==20?20:50+i*6;
                var generated=EndlessGenerator.Generate(i*7919,round);var level=new LevelDefinition(generated.Data);
                Check(level.BoxCount==(start==7?24:start==20?30:36),"late rounds retain tier size");
                Check(level.Ships.Select(ship=>ship.Type).Distinct().Count()==4,"four cargo types");
                using(var low=new Solver(level,GameRules.Initial(level),2,0,new SearchBudget(200000,10000)))
                {while(low.Step(100)==SearchStatus.Searching){} Check(low.Status==SearchStatus.Unsolvable,"endless requires buffer 3");}
                using(var hint=new Solver(level,GameRules.Initial(level),3,0,new SearchBudget(200000,3000)))
                {
                    while(hint.Step(2)==SearchStatus.Searching){}
                    Check(hint.Status==SearchStatus.Found,"hard endless hint budget");
                    worstNodes=Math.Max(worstNodes,hint.Nodes);slowest=Math.Max(slowest,hint.ElapsedMilliseconds);
                    var board=GameRules.Initial(level);
                    foreach(var id in hint.Solution) {var move=GameRules.Apply(level,board,3,id);Check(move.Accepted,"hard hint replay");board=move.Board;}
                    Check(GameRules.Status(level,board,3)==BoardStatus.Completed,"hard hint completes");
                }
            }
            foreach(var index in new[]{49,99,199})
            {
                var run=new GameSession(levels[index],"expanded-save-"+index);
                MoveStack(run,0);var save=new PlayerSave{profile=new PlayerProfile{unlockedCount=index+1,completed=levels.Take(index).Select(l=>l.Id).ToList()},activeRun=run.Export()};
                var storage=new FaultStorage();Action<PlayerSave> validate=s=>ProgressValidator.Validate(s,levels);
                var repository=new SaveRepository(storage,codec,validate);repository.Load();Check(repository.Save(save),"200-level save bound/payload");
                var loaded=new SaveRepository(storage,codec,validate).Load().Value;
                Check(loaded!=null && loaded.activeRun.board.ExactKey()==run.Board.ExactKey(),"expanded save roundtrip");
            }
            var invalid=codec.Decode<CatalogData>(codec.Encode(catalog)); invalid.levels[199].id="L201";
            Throws(()=>LevelValidator.Load(invalid),"reject L201");
            result.Add("HARD PASS 150 campaign puzzles require buffer 3; 150 random endless layouts solved/replayed, worstNodes="+worstNodes+" maxMs="+slowest.ToString("F2")+"; L50/L100/L200 saves and ID bounds");
        }
        private static void TestRewardedHint(LevelDefinition level,IJsonCodec codec,List<string> result)
        {
            var session=new GameSession(level,"reward");var box=GameRules.Top(level,session.Board,0);
            foreach(var outcome in new[]{RewardedVideoResult.Cancelled,RewardedVideoResult.Failed,RewardedVideoResult.Unavailable})
            {
                var ticket=new RewardedHintTicket(session,box);
                Check(!ticket.Resolve(session,outcome)&&!ticket.Resolve(session,RewardedVideoResult.Completed)&&!session.AdHintUsed,"failed/cancelled reward never grants");
            }
            var stale=new RewardedHintTicket(session,box);MoveStack(session,0);
            Check(!stale.Resolve(session,RewardedVideoResult.Completed),"stale board reward rejected");
            box=GameRules.Top(level,session.Board,0);
            var replaced=new RewardedHintTicket(session,box);
            Check(!replaced.Resolve(new GameSession(level,"other"),RewardedVideoResult.Completed),"replaced run reward rejected");
            var complete=new RewardedHintTicket(session,box);
            Check(complete.Resolve(session,RewardedVideoResult.Completed)&&!complete.Resolve(session,RewardedVideoResult.Completed)&&session.AdHintUsed,"exactly once completed reward");
            Check(GameSession.Restore(level,session.Export()).AdHintUsed,"reward saved");
            Check(session.TryUndo(session.RunId,session.Version)&&session.AdHintUsed,"reward survives undo");
            box=GameRules.Top(level,session.Board,0);
            var boardBefore=session.Board.ExactKey();var versionBefore=session.Version;
            var firstAdditional=new RewardedHintTicket(session,box);
            var secondAdditional=new RewardedHintTicket(session,box);
            Check(firstAdditional.Resolve(session,RewardedVideoResult.Completed)
                &&secondAdditional.Resolve(session,RewardedVideoResult.Completed)&&session.Export().hintCount==3,
                "independent completed videos grant unlimited hints on same board");
            Check(!session.FreeHintUsed&&session.Board.ExactKey()==boardBefore&&session.Version==versionBefore,
                "rewarded hints preserve free hint and board");
            Check(session.UseHint(session.RunId,session.Version,box,false)
                &&!session.UseHint(session.RunId,session.Version,box,false),"free hint remains once per run");

            // Existing saves already contain this flag. It must not retain the former one-video limit.
            var legacy=codec.Decode<RunData>(codec.Encode(session.Export()));
            Check(legacy.adHintUsed&&legacy.freeHintUsed,"existing reward flags serialize");
            var restored=GameSession.Restore(level,legacy);
            Check(new RewardedHintTicket(restored,box).Resolve(restored,RewardedVideoResult.Completed)
                &&restored.Export().hintCount==5&&!restored.UseHint(restored.RunId,restored.Version,box,false),
                "old adHintUsed save permits more ads without restoring free quota");

            var saturated=restored.Export();saturated.hintCount=int.MaxValue-1;
            restored=GameSession.Restore(level,saturated);
            Check(new RewardedHintTicket(restored,box).Resolve(restored,RewardedVideoResult.Completed)
                &&new RewardedHintTicket(restored,box).Resolve(restored,RewardedVideoResult.Completed)
                &&restored.Export().hintCount==int.MaxValue,"unlimited hint counter saturates");
            Check(GameSession.Restore(level,codec.Decode<RunData>(codec.Encode(restored.Export()))).Export().hintCount==int.MaxValue,
                "saturated hint count remains a valid persisted run");

            var restarted=new RewardedHintTicket(restored,box);restored.Restart("reward-restarted");
            Check(!restarted.Resolve(restored,RewardedVideoResult.Completed)&&!restored.FreeHintUsed&&!restored.AdHintUsed,
                "same session restart invalidates reward and resets free hint");
            result.Add("REWARD PASS unlimited completed hints, once-only free hint, old saves, saturated counts, duplicate/stale/restarted callbacks");
        }

        private static void TestRewardedSkip(LevelDefinition level,IJsonCodec codec,List<string> result)
        {
            var session=new GameSession(level,"skip");const int round=20;
            foreach(var outcome in new[]{RewardedVideoResult.Cancelled,RewardedVideoResult.Failed,RewardedVideoResult.Unavailable})
            {
                var rejected=new RewardedSkipTicket(session,round);
                Check(!rejected.TryResolve(session,true,round,outcome)
                    &&!rejected.TryResolve(session,true,round,RewardedVideoResult.Completed),"failed skip ticket consumes late callback");
            }
            var wrongMode=new RewardedSkipTicket(session,round);
            Check(!wrongMode.TryResolve(session,false,round,RewardedVideoResult.Completed)
                &&!wrongMode.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip rejects campaign and consumes ticket");
            var wrongRound=new RewardedSkipTicket(session,round);
            Check(!wrongRound.TryResolve(session,true,round+1,RewardedVideoResult.Completed)
                &&!wrongRound.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip rejects changed round");
            var replaced=new RewardedSkipTicket(session,round);
            Check(!replaced.TryResolve(new GameSession(level,session.RunId),true,round,RewardedVideoResult.Completed),
                "skip rejects replacement session even with same run id");
            var stale=new RewardedSkipTicket(session,round);MoveStack(session,0);
            Check(!stale.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip rejects changed board version");
            var undone=new RewardedSkipTicket(session,round);Check(session.TryUndo(session.RunId,session.Version),"skip test undo");
            Check(!undone.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip rejects undo version");
            var restarted=new RewardedSkipTicket(session,round);session.Restart("skip-restarted");
            Check(!restarted.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip rejects same session restarted run");
            Check(!new RewardedSkipTicket(session,int.MaxValue).TryResolve(session,true,int.MaxValue,RewardedVideoResult.Completed),
                "maximum endless round cannot skip");
            Check(!new RewardedSkipTicket(session,0).TryResolve(session,true,0,RewardedVideoResult.Completed),"invalid round cannot skip");

            session.Busy=true; // The reward confirmation pauses gameplay while the platform video is shown.
            var before=codec.Encode(session.Export());var accepted=new RewardedSkipTicket(session,round);
            Check(accepted.TryResolve(session,true,round,RewardedVideoResult.Completed)
                &&!accepted.TryResolve(session,true,round,RewardedVideoResult.Completed),"skip accepts completed video exactly once");
            Check(codec.Encode(session.Export())==before&&session.Busy,"skip proof itself never changes board, counters or pause state");
            session.Busy=false;
            using(var solver=new Solver(level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching){}
                Check(solver.Status==SearchStatus.Found,"skip completion fixture solvable");
                foreach(var id in solver.Solution)Check(session.TryMove(id,session.RunId,session.Version).Accepted,"skip completion fixture move");
            }
            Check(session.Status==BoardStatus.Completed
                &&!new RewardedSkipTicket(session,round).TryResolve(session,true,round,RewardedVideoResult.Completed),
                "completed round does not offer rewarded skip");
            result.Add("SKIP PASS completed-only one-use proof, mode/run/version/round binding, restart/undo rejection, maximum round and no state mutation");
        }

        private static void TestStorage(LevelDefinition level, IJsonCodec codec, List<string> result)
        {
            var storage = new FaultStorage();
            Action<PlayerSave> validate = save => {
                Check(save != null && save.profile != null, "save profile");
                if (save.activeRun != null) GameSession.Restore(level, save.activeRun);
            };
            var repository = new SaveRepository(storage, codec, validate);
            repository.Load();
            var session = new GameSession(level, "save");
            Check(repository.Save(new PlayerSave { activeRun = session.Export() }), "initial save");
            MoveStack(session, 0);
            Check(repository.Save(new PlayerSave { activeRun = session.Export() }), "second save");
            var recovered = new SaveRepository(storage, codec, validate).Load();
            Check(recovered.Value.activeRun.board.ExactKey() == session.Board.ExactKey(), "newest load");
            storage.Values[SaveRepository.Key(1)] = "corrupt";
            recovered = new SaveRepository(storage, codec, validate).Load();
            Check(recovered.RecoveredOlderSlot && recovered.Value.activeRun.stateVersion == 0, "corrupt latest fallback");
            storage.Values[SaveRepository.Key(0)] = "corrupt";
            Check(new SaveRepository(storage, codec, validate).Load().Reset, "both corrupt reset");
            storage.Fail = true;
            Check(!repository.Save(new PlayerSave { activeRun = session.Export() }) && repository.HasPending, "failed pending");
            session.Restart("new-save");
            Check(!repository.Save(new PlayerSave { activeRun = session.Export() }), "new pending replaces old");
            storage.Fail = false;
            Check(repository.Retry() && !repository.HasPending, "retry exact pending");
            recovered = new SaveRepository(storage, codec, validate).Load();
            Check(recovered.Value.activeRun.runId == "new-save" && recovered.Revision == 4, "no stale overwrite");
            result.Add("U08/SAVE PASS A/B fallback, both corrupt, failed-write retry and newer-run ordering");
        }

        private sealed class FaultStorage : IStoragePort
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public bool Fail;
            public string ReadString(string key) => Values.TryGetValue(key, out var value) ? value : null;
            public void WriteString(string key, string value) { if (Fail) throw new Exception("simulated disk failure"); Values[key] = value; }
        }
        private static void MoveStack(GameSession session, int stack)
        { Check(session.TryMove(GameRules.Top(session.Level, session.Board, stack), session.RunId, session.Version).Accepted, "accepted move"); }
        private static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL " + message); }
        private static void Throws(Action action, string message)
        { try { action(); } catch (DataValidationException) { return; } throw new Exception("FAIL " + message); }
        private static LevelDefinition MakeLevel(string[] stacks, string ships)
        {
            var data = new LevelData { id = "L99", schemaVersion = 1, revision = 1, bufferCapacity = 3,
                stacks = new StackData[stacks.Length] };
            for (var s = 0; s < stacks.Length; s++)
            {
                data.stacks[s] = new StackData { id = "S" + s, boxes = new BoxData[stacks[s].Length] };
                for (var b = 0; b < stacks[s].Length; b++) data.stacks[s].boxes[b] = new BoxData { id = s + ":" + b, type = stacks[s][b].ToString() };
            }
            var entries = ships.Split(','); data.ships = new ShipData[entries.Length];
            for (var i = 0; i < entries.Length; i++) data.ships[i] = new ShipData { id = "V" + i, type = entries[i][0].ToString(), capacity = int.Parse(entries[i].Substring(1)) };
            return new LevelDefinition(data);
        }
    }
}
