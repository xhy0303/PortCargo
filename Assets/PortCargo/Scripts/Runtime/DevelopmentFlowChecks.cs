#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using PortCargo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    public sealed partial class DevelopmentChecks
    {
        [Serializable] private sealed class FlowExpected
        {public string campaignRun,campaignBoard,endlessRun,endlessBoard;public long campaignVersion,endlessVersion;public int completed,round;}
        public void BeginFlowSmoke(string reportPath,bool read=false)
        {
            Initialize(reportPath+(read?".read.txt":".write.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            StartCoroutine(Guarded(FlowSmoke(reportPath,read)));StartCoroutine(QuitAfterReport());
        }
        private IEnumerator FlowSmoke(string path,bool read)
        {
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return null;yield return null;var flow=presenter.Flow;
            Require(flow.Screen==FlowScreen.Home&&flow.HomeVisible&&presenter.Session.Busy,"boot must enter safe home");
            if(read)
            {
                var expected=JsonUtility.FromJson<FlowExpected>(File.ReadAllText(path+".expected.json"));
                Require(presenter.Profile.completed.Count==expected.completed&&presenter.Profile.tutorialCompleted,"fresh process profile/guide restore");
                Require(!presenter.Profile.sound&&!presenter.Profile.music&&presenter.Profile.reducedMotion,"fresh process settings restore");
                yield return PressButton("继续闯关");
                Require(flow.Screen==FlowScreen.Playing&&presenter.Session.RunId==expected.campaignRun&&presenter.Session.Version==expected.campaignVersion&&presenter.Session.Board.ExactKey()==expected.campaignBoard,"campaign continue exact snapshot");
                yield return PressButton("撤销");Require(presenter.Session.Version==expected.campaignVersion+1,"campaign restored undo");
                yield return PressButton("暂停");yield return PressButton("返回首页");yield return PressButton("继续无尽");
                Require(flow.Screen==FlowScreen.Playing&&presenter.IsEndless&&presenter.EndlessRound==expected.round&&presenter.Session.RunId==expected.endlessRun&&presenter.Session.Version==expected.endlessVersion&&presenter.Session.Board.ExactKey()==expected.endlessBoard,"endless continue exact snapshot");
                yield return PressButton("撤销");Require(presenter.Session.Version==expected.endlessVersion+1,"endless restored undo");
                yield return PressButton("暂停");yield return PressButton("返回首页");
                yield return CaptureFlow(path+".restored-home.png");
                report.Add("F09 READ PASS separate process starts at home; campaign/endless exact runs restored independently, real undo and settings persist");
                yield break;
            }
            Require(presenter.Profile.completed.Count==0&&presenter.Session.Level.Id=="L01"&&!presenter.Profile.tutorialCompleted,"WRITE requires fresh isolated save");
            yield return CaptureFlow(path+".home.png");
            yield return PressButton("选择关卡");
            var locked=FindButton("第 2 关  未解锁");Require(locked&&!locked.interactable,"locked level must be visibly disabled");
            yield return CaptureFlow(path+".levels.png");
            yield return PressButton("下一页");Require(FindButton("第 9 关  未解锁")&&!FindButton("第 1 关"),"page navigation");
            yield return PressButton("上一页");yield return PressButton("返回");Require(flow.Screen==FlowScreen.Home,"level select back to home");
            yield return PressButton("开始闯关");Require(flow.Screen==FlowScreen.Help,"first play guide");
            yield return CaptureFlow(path+".help.png");
            yield return PressButton("下一页");yield return PressButton("下一页");yield return PressButton("开始游戏");
            Require(flow.Screen==FlowScreen.Playing&&presenter.Profile.tutorialCompleted,"guide starts actual game");
            yield return FlowOneMove();var before=presenter.Session.Export();
            yield return PressButton("船队");Require(flow.Screen==FlowScreen.Queue&&presenter.Session.Busy,"queue pauses board");yield return PressButton("返回");
            yield return PressButton("暂停");Require(flow.Screen==FlowScreen.Paused&&presenter.Session.Busy,"pause freezes board");
            yield return CaptureFlow(path+".pause.png");
            yield return PressButton("玩法说明");yield return PressButton("返回");Require(flow.Screen==FlowScreen.Paused,"help returns to pause");
            yield return PressButton("设置");yield return PressButton("音效：开");yield return PressButton("音乐：开");yield return PressButton("减少动效：关");
            yield return CaptureFlow(path+".settings.png");
            yield return PressButton("隐私与反馈");Require(flow.Screen==FlowScreen.Privacy,"privacy opens");yield return PressButton("返回");yield return PressButton("返回");
            Require(flow.Screen==FlowScreen.Paused,"settings returns to pause");yield return PressButton("返回首页");
            yield return PressButton("继续闯关");Require(presenter.Session.RunId==before.runId&&presenter.Session.Version==before.stateVersion&&presenter.Session.Board.ExactKey()==before.board.ExactKey(),"home return preserves partial campaign");
            yield return CompleteCurrentBoard("F03 L01");Require(flow.Screen==FlowScreen.Completed,"campaign result");
            yield return CaptureFlow(path+".completed.png");
            yield return PressButton("下一关");Require(presenter.Session.Level.Id=="L02"&&flow.Screen==FlowScreen.Playing,"next level");
            yield return FlowOneMove();var campaign=presenter.Session.Export();
            yield return PressButton("暂停");yield return PressButton("选择关卡");yield return PressButton("第 1 关  已完成");
            Require(flow.Screen==FlowScreen.Confirm,"switching away from partial campaign requires confirmation");yield return PressButton("返回选关");yield return PressButton("返回");yield return PressButton("返回首页");
            report.Add("F01-F04 PASS home, locked/paged levels, first-play guide, queue, pause/help/settings/privacy, campaign result and next level; cancelled switch preserves partial L02");
            yield return PressButton("开启无尽");Require(presenter.IsEndless&&flow.Screen==FlowScreen.Playing,"endless starts");
            yield return FlowOneMove();var firstRun=presenter.Session.RunId;
            yield return PressButton("暂停");yield return PressButton("重新开始");yield return PressButton("继续这一局");Require(presenter.Session.RunId==firstRun&&flow.Screen==FlowScreen.Paused,"cancel restart");
            yield return PressButton("重新开始");yield return PressButton("重新开始");Require(presenter.Session.RunId!=firstRun&&presenter.Session.Version==0&&presenter.EndlessRound==1,"confirmed restart creates fresh run same endless round");
            yield return CompleteCurrentBoard("F05 endless round 1");yield return CaptureFlow(path+".endless-completed.png");
            yield return PressButton("下一程");Require(presenter.EndlessRound==2,"endless next round");yield return FlowOneMove();var endless=presenter.Session.Export();
            yield return PressButton("暂停");yield return PressButton("返回首页");
            Require(presenter.Profile.completed.Count==1,"endless never adds campaign completion");
            yield return PressButton("退出游戏");Require(flow.Screen==FlowScreen.Confirm,"quit confirms");yield return PressButton("继续游戏");Require(flow.Screen==FlowScreen.Home,"cancel quit returns home");
            presenter.FlushSave();Require(!presenter.SavePending,"flow save committed");
            File.WriteAllText(path+".expected.json",JsonUtility.ToJson(new FlowExpected{campaignRun=campaign.runId,campaignBoard=campaign.board.ExactKey(),campaignVersion=campaign.stateVersion,
                endlessRun=endless.runId,endlessBoard=endless.board.ExactKey(),endlessVersion=endless.stateVersion,round=2,completed=1},true));
            report.Add("F05-F08 WRITE PASS endless restart cancel/confirm/result/next, independent campaign progress, home save, quit confirmation cancellation, final two-mode snapshot");
        }
        private sealed class FlowFaultStorage : IStoragePort
        {
            public bool Fail;
            private readonly System.Collections.Generic.Dictionary<string,string> values=new System.Collections.Generic.Dictionary<string,string>();
            public string ReadString(string key)=>values.TryGetValue(key,out var value)?value:null;
            public void WriteString(string key,string value){if(Fail)throw new IOException("Simulated write failure");values[key]=value;}
        }
        public void BeginFlowEdges(string reportPath)
        {
            Initialize(reportPath+".edges.txt");Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            StartCoroutine(Guarded(FlowEdges(reportPath)));StartCoroutine(QuitAfterReport());
        }
        private IEnumerator FlowEdges(string path)
        {
            yield return null;yield return null;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var levels=(LevelDefinition[])typeof(GamePresenter).GetField("levels",flags).GetValue(presenter);
            var tuning=(GameTuning)typeof(GamePresenter).GetField("tuning",flags).GetValue(presenter);
            var storage=new FlowFaultStorage();var codec=new PortCargo.Platform.UnityJsonCodec();
            var repository=new SaveRepository(storage,codec,value=>ProgressValidator.Validate(value,levels));
            var picks=new System.Collections.Generic.List<string>();
            GameSession initial=null;var fixtureIndex=-1;
            for(var i=3;i<levels.Length;i++)
            {
                initial=new GameSession(levels[i],"flow-edge-fixture");picks.Clear();
                if(FindDeadlock(levels[i],initial.Board,picks,new System.Collections.Generic.HashSet<string>())){fixtureIndex=i;break;}
                yield return null;
            }
            Require(fixtureIndex>=0,"catalog contains a reachable deadlock fixture");
            var profile=new PlayerProfile{unlockedCount=fixtureIndex+1,tutorialCompleted=true,sound=false,music=false,reducedMotion=true};
            for(var i=0;i<fixtureIndex;i++)profile.completed.Add(levels[i].Id);
            presenter.Initialize(levels,tuning,repository,new LoadResult{Value=new PlayerSave{profile=profile,activeRun=initial.Export(),mode="campaign"}});
            yield return PressButton("继续闯关");
            foreach(var pick in picks)yield return Click(Center(FindBox(pick).Rect));
            Require(presenter.Flow.Screen==FlowScreen.Deadlocked&&presenter.Session.Status==BoardStatus.Deadlocked,"deadlock result shown");
            yield return CaptureFlow(path+".deadlocked.png");yield return PressButton("撤销");
            Require(presenter.Flow.Screen==FlowScreen.Playing&&presenter.Session.Status==BoardStatus.Playing,"deadlock undo resumes play");
            yield return PressButton("暂停");yield return PressButton("返回首页");
            storage.Fail=true;Require(!presenter.SaveNow()&&presenter.SavePending,"simulated save pending");
            yield return PressButton("退出游戏");yield return PressButton("退出游戏");
            Require(FindButton("重试保存")&&FindButton("留在游戏")&&FindButton("仍然退出"),"failed-save quit choices");
            yield return CaptureFlow(path+".save-failed.png");yield return PressButton("留在游戏");
            Require(presenter.Flow.Screen==FlowScreen.Home&&presenter.SavePending,"failed quit stays home with pending save");
            storage.Fail=false;yield return PressButton("保存失败，点此重试");Require(!presenter.SavePending,"home retry recovers save");
            presenter.Flow.ShowBootError(()=>presenter.Flow.Initialize(""));
            Require(presenter.Flow.Screen==FlowScreen.BootError&&FindButton("重试"),"boot error recovery view");
            yield return PressButton("重试");Require(presenter.Flow.Screen==FlowScreen.Home,"boot error retry returns home");
            report.Add("F10-F12 PASS isolated in-memory "+levels[fixtureIndex].Id+" fixture: real pointer deadlock/undo; simulated write failure prevents quit and can retry; boot-error view retry route");
        }
        private static bool FindDeadlock(LevelDefinition level,BoardSnapshot board,System.Collections.Generic.List<string> path,System.Collections.Generic.HashSet<string> visited)
        {
            if(GameRules.Status(level,board,3)==BoardStatus.Deadlocked)return true;
            if(visited.Count>=20000||!visited.Add(board.ExactKey()))return false;
            for(var stack=0;stack<level.Stacks.Count;stack++)
            {
                var top=GameRules.Top(level,board,stack);if(top==null)continue;
                var move=GameRules.Apply(level,board,3,top);if(!move.Accepted)continue;
                path.Add(top);if(FindDeadlock(level,move.Board,path,visited))return true;path.RemoveAt(path.Count-1);
            }
            return false;
        }
        private IEnumerator FlowOneMove()
        {
            var session=presenter.Session;
            using(var solver=new Solver(session.Level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                Require(solver.Status==SearchStatus.Found&&solver.Solution.Length>0,"one-move solution");
                var version=session.Version;yield return Click(Center(FindBox(solver.Solution[0]).Rect));Require(session.Version==version+1,"real one-move pointer");
            }
        }
        private IEnumerator CaptureFlow(string path)
        {
            yield return null;Canvas.ForceUpdateCanvases();AssertVisibleGlyphs();
            var dialog=FindFirstObjectByType<DialogView>();var panel=dialog.transform.Find("Dialog");
            foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if(dialog.Visible&&!button.transform.IsChildOf(panel))continue;
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners[2].x-corners[0].x>=43.9f&&corners[1].y-corners[0].y>=43.9f,"touch target too small "+button.name);
                Require(corners[0].x>=-.5f&&corners[0].y>=-.5f&&corners[2].x<=Screen.width+.5f&&corners[2].y<=Screen.height+.5f,"control out of viewport "+button.name);
            }
            ScreenCapture.CaptureScreenshot(path);
            for(var i=0;i<90&&!File.Exists(path);i++)yield return null;
            foreach(var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if(string.IsNullOrEmpty(text.text))continue;
                if(dialog.Visible&&!text.transform.IsChildOf(panel))continue;
                text.ForceMeshUpdate();Require(!text.isTextOverflowing,"visible text overflows: "+text.name+" "+text.text);
            }
            Require(File.Exists(path),"screenshot not saved");
            report.Add("VIEW PASS "+presenter.Flow.Screen+" "+Screen.width+"x"+Screen.height+" glyphs/44px controls; "+Path.GetFileName(path));
        }
    }
}
#endif
