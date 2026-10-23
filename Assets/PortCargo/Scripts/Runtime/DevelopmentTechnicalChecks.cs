#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PortCargo.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PortCargo.Runtime
{
    public sealed partial class DevelopmentChecks
    {
        private string technicalPrefix;
        private bool artChecksOnly;
        public void BeginArtChecks(string prefix){artChecksOnly=true;BeginTechnicalChecks(prefix);}
        public void BeginNativeObservation(string prefix)
        {
            presenter=FindFirstObjectByType<GamePresenter>();
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            StartCoroutine(ObserveNative(prefix));
        }
        private IEnumerator ObserveNative(string prefix)
        {
            var previous="";var index=0;
            while(true)
            {
                var pointer=UnityEngine.InputSystem.Mouse.current;
                var state=presenter.Flow.Screen.ToString();
                if(state!=previous || (pointer!=null && pointer.leftButton.wasPressedThisFrame))
                {
                    File.AppendAllText(prefix+".txt",Time.realtimeSinceStartup+" screen="+state+" mouse="+(pointer==null?"none":pointer.position.ReadValue().ToString())+" pressed="+(pointer!=null&&pointer.leftButton.wasPressedThisFrame)+" size="+Screen.width+"x"+Screen.height+"\n");
                    previous=state;
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(prefix+"."+(index++)+".png");
                }
                yield return null;
            }
        }
        public void BeginNativeMap(string prefix)
        {
            Initialize(prefix+".txt");Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            StartCoroutine(Guarded(NativeMap(prefix)));StartCoroutine(QuitAfterReport());
        }
        private IEnumerator NativeMap(string prefix)
        {
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return null;yield return PressButton("退出游戏");
            yield return CaptureFlow(prefix+".png");
            foreach(var label in new[]{"退出游戏","继续游戏"})
            {
                var button=FindButton(label);var center=Center((RectTransform)button.transform);
                report.Add("NATIVE_MAP "+label+" x="+(center.x/Screen.width).ToString("F5")+" yFromTop="+(1-center.y/Screen.height).ToString("F5")+" screen="+Screen.width+"x"+Screen.height);
            }
            yield return PressButton("继续游戏");Require(presenter.Flow.Screen==FlowScreen.Home,"native-map cancel returns home");
        }
        public void BeginTechnicalChecks(string prefix)
        {
            technicalPrefix=prefix;
            Initialize(prefix+".txt");Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            StartCoroutine(Guarded(TechnicalChecks()));StartCoroutine(QuitAfterReport());
        }
        private IEnumerator TechnicalChecks()
        {
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return null;yield return null;
            var args=Environment.GetCommandLineArgs();var isolated=false;
            for(var i=0;i<args.Length-1;i++)if(args[i]=="--portcargo-save-dir")
                isolated=Path.GetFullPath(args[i+1])!=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"Saves"));
            Require(isolated,"technical fixture requires explicit isolated save directory");
            presenter.Profile.unlockedCount=presenter.LevelCount;presenter.Profile.tutorialCompleted=true;
            presenter.Profile.music=false;presenter.Profile.sound=true;presenter.Profile.reducedMotion=false;
            presenter.SaveNow();presenter.Flow.ShowHome();
            report.Add("FIXTURE isolated save only: all campaign levels unlocked, guide completed, music off, sound and motion on");
            var sizes=new[]{new Vector2Int(320,568),new Vector2Int(360,800),new Vector2Int(720,1280)};
            foreach(var size in sizes)
            {
                yield return TechnicalResolution(size.x,size.y);
                foreach(var level in new[]{10,51,100,150,200})
                {
                    yield return TechnicalSelectLevel(level);
                    yield return null;Canvas.ForceUpdateCanvases();presenter.Render();
                    Require(presenter.Session.Export().moveCount==0,"dense check must start with initial board");
                    var initial=presenter.Session.Board.ExactKey();var tops=new List<BoxView>();
                    var minimum=new Vector2(float.MaxValue,float.MaxValue);
                    foreach(var box in presenter.BoardView.Boxes)
                    {
                        if(!box.gameObject.activeInHierarchy)continue;
                        var rect=TechnicalRect(box.Rect);
                        Require(rect.xMin>=-.5f&&rect.yMin>=-.5f&&rect.xMax<=Screen.width+.5f&&rect.yMax<=Screen.height+.5f,"cargo outside viewport "+box.BoxId);
                        if(!box.IsTop)continue;
                        Require(rect.width>=43.9f&&rect.height>=43.9f,"dense top target below 44px");
                        minimum=Vector2.Min(minimum,rect.size);tops.Add(box);
                        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Center(box.Rect)},hits);
                        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<BoxView>()==box,"top center is covered by another raycast target");
                    }
                    Require(tops.Count==presenter.Session.Level.Stacks.Count,"every stack has a top");
                    for(var a=0;a<tops.Count;a++)for(var b=a+1;b<tops.Count;b++)
                        Require(!TechnicalRect(tops[a].Rect).Overlaps(TechnicalRect(tops[b].Rect)),"top hitboxes overlap");
                    yield return Layout();
                    yield return CaptureFlow(technicalPrefix+".L"+level+"-"+size.x+"x"+size.y+".png");
                    // Re-query by stack after every render: pooled BoxViews can be rebound.
                    for(var stack=0;stack<presenter.Session.Level.Stacks.Count;stack++)
                    {
                        var id=GameRules.Top(presenter.Session.Level,presenter.Session.Board,stack);var version=presenter.Session.Version;
                        yield return Click(Center(FindBox(id).Rect));Require(presenter.Session.Version==version+1,"dense real pointer missed stack "+stack);
                        yield return PressButton("撤销");Require(presenter.Session.Version==version+2&&presenter.Session.Board.ExactKey()==initial,"dense undo differs");
                    }
                    var top=GameRules.Top(presenter.Session.Level,presenter.Session.Board,0);var pausedVersion=presenter.Session.Version;
                    var topRect=TechnicalRect(FindBox(top).Rect);
                    yield return PressButton("暂停");yield return Click(new Vector2(topRect.xMin+1,topRect.center.y));
                    Require(presenter.Session.Version==pausedVersion&&presenter.Flow.Screen==FlowScreen.Paused,"modal click-through");
                    yield return PressButton("继续游戏");
                    report.Add("DENSE PASS L"+level+" "+Screen.width+"x"+Screen.height+" topMin="+minimum.x.ToString("F1")+"x"+minimum.y.ToString("F1")+"; every top raycast/click/undo; no overlaps; modal blocked");
                    yield return TechnicalHome();
                }
            }
            File.WriteAllLines(output,report);
            yield return TechnicalResolution(320,568);
            if(artChecksOnly)
            {
                yield return TechnicalSelectLevel(presenter.LevelCount);
                yield return PressButton("提示");
                for(var wait=0;wait<300&&!presenter.Session.FreeHintUsed;wait++)yield return null;
                Require(presenter.Session.FreeHintUsed,"L200 production hint succeeds");
                yield return CompleteCurrentBoard("ART last campaign level");
                yield return CaptureFlow(technicalPrefix+".L200-completed.png");
                Require(FindButton("开启无尽")&&FindButton("选择关卡"),"last level retains endless and level selection");
                yield return PressButton("返回首页");yield return PressButton("选择关卡");
                yield return CaptureFlow(technicalPrefix+".last-page.png");
                var levelDialog=UnityEngine.Object.FindFirstObjectByType<DialogView>();
                var levelCard=(RectTransform)levelDialog.transform.Find("Dialog/Card");
                var sparseCard=TechnicalRect(levelCard);
                var sparseFooter=TechnicalRect((RectTransform)FindButton("返回").transform).y;
                yield return PressButton("上一页");yield return Layout();
                Require(TechnicalRect(levelCard)==sparseCard,"level card changes size or position between sparse and full pages");
                Require(Mathf.Abs(TechnicalRect((RectTransform)FindButton("返回").transform).y-sparseFooter)<.1f,"level footer shifts between pages");
                yield return CaptureFlow(technicalPrefix+".full-page.png");
                report.Add("ART PASS sparse/full level page bounds="+sparseCard+"; footer remains fixed");
                yield return PressButton("返回");
                report.Add("ART PASS dense viewports, L200 hint/result and last page");
                // The fixture contains a late endless round; exercise it through real UI input.
                yield return TechnicalSelectLevel(presenter.LevelCount);yield return FlowOneMove();
                var campaign=presenter.Session.Export();yield return TechnicalHome();
                yield return PressButton("继续无尽");
                Require(presenter.EndlessRound==60&&presenter.Session.Level.BoxCount==36,"late endless fixture");
                yield return PressButton("提示");
                for(var wait=0;wait<300&&!presenter.Session.FreeHintUsed;wait++)yield return null;
                Require(presenter.Session.FreeHintUsed,"late endless production hint succeeds");
                yield return CaptureFlow(technicalPrefix+".endless60-hint.png");
                yield return CompleteCurrentBoard("HARD endless round 60");
                yield return PressButton("下一程");
                Require(presenter.EndlessRound==61&&presenter.Session.Level.BoxCount==36,"late endless never resets easy");
                yield return FlowOneMove();var endless=presenter.Session.Export();
                presenter.Profile.sound=false;presenter.Profile.music=false;presenter.Profile.reducedMotion=true;
                yield return TechnicalHome();presenter.SaveNow();Require(!presenter.SavePending,"hard save committed");
                File.WriteAllText(technicalPrefix+".expected.json",JsonUtility.ToJson(new FlowExpected{campaignRun=campaign.runId,campaignBoard=campaign.board.ExactKey(),campaignVersion=campaign.stateVersion,
                    endlessRun=endless.runId,endlessBoard=endless.board.ExactKey(),endlessVersion=endless.stateVersion,round=61,completed=presenter.Profile.completed.Count},true));
                report.Add("HARD WRITE PASS L200 and endless60 production hints, real pointer completion, round61 stays 36 boxes, both-mode snapshots saved for separate-process read");
                yield break;
            }
            yield return TechnicalSoak();
        }
        private IEnumerator TechnicalResolution(int width,int height)
        {
            Screen.SetResolution(width,height,FullScreenMode.Windowed);
            for(var i=0;i<150&&(Screen.width!=width||Screen.height!=height);i++)yield return null;
            Require(Screen.width==width&&Screen.height==height,"requested runtime viewport unavailable");
            yield return null;yield return null;Canvas.ForceUpdateCanvases();presenter.Render();
        }
        private static Rect TechnicalRect(RectTransform target)
        {var corners=new Vector3[4];target.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
        private IEnumerator TechnicalHome()
        {
            if(presenter.Flow.Screen==FlowScreen.Home)yield break;
            if(presenter.Flow.Screen==FlowScreen.Playing)yield return PressButton("暂停");
            yield return PressButton("返回首页");Require(presenter.Flow.Screen==FlowScreen.Home,"home navigation");
        }
        private IEnumerator TechnicalSelectLevel(int number)
        {
            yield return TechnicalHome();yield return PressButton("选择关卡");
            var page=presenter.CampaignIndex/8;var target=(number-1)/8;
            while(page<target){yield return PressButton("下一页");page++;}
            while(page>target){yield return PressButton("上一页");page--;}
            var label="第 "+number+" 关";if(presenter.IsLevelCompleted(number-1))label+="  已完成";
            yield return PressButton(label);
            if(presenter.Flow.Screen==FlowScreen.Confirm)yield return PressButton("切换关卡");
            if(presenter.Flow.Screen==FlowScreen.Completed)yield return PressButton("重玩本关");
            Require(!presenter.IsEndless&&presenter.Session.Level.Id=="L"+number.ToString("00")&&presenter.Flow.Screen==FlowScreen.Playing,"technical select level");
            if(presenter.Session.Export().moveCount>0)
            {yield return PressButton("暂停");yield return PressButton("重新开始");yield return PressButton("重新开始");}
        }
        private IEnumerator TechnicalSoak()
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();var nextSample=0d;var loops=0;
            var transforms=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            var audio=FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            var samples=new List<long>();var managedSamples=new List<long>();var warningErrors=0;
            Application.LogCallback logCheck=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)warningErrors++;};
            Application.logMessageReceived+=logCheck;
            try
            {
                report.Add("SOAK START duration>=600s, real pointers, L10/L50/endless, undo, pause/settings, home switches; UTC="+DateTime.UtcNow.ToString("o"));
                while(clock.Elapsed.TotalSeconds<600)
                {
                    yield return TechnicalHome();
                    if(loops%3==2)
                    {yield return PressButton(presenter.Endless==null?"开启无尽":"继续无尽");Require(presenter.IsEndless,"soak endless entry");}
                    else yield return TechnicalSelectLevel(loops%3==0?10:50);
                    var initial=presenter.Session.Board.ExactKey();var version=presenter.Session.Version;
                    yield return FlowOneMove();yield return PressButton("撤销");
                    Require(presenter.Session.Board.ExactKey()==initial&&presenter.Session.Version==version+2,"soak undo preserves exact board");
                    yield return PressButton("暂停");yield return PressButton("设置");
                    var reduced=presenter.Profile.reducedMotion;yield return PressButton(reduced?"减少动效：开":"减少动效：关");
                    Require(presenter.Profile.reducedMotion!=reduced,"soak settings toggle");
                    yield return PressButton("返回");yield return PressButton("继续游戏");
                    yield return CompleteCurrentBoard("SOAK cycle "+(loops+1));loops++;
                    yield return TechnicalHome();
                    Require(!presenter.SavePending,"soak save pending");
                    Require(FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==transforms,"soak transform growth");
                    Require(FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==audio,"soak audio-source growth");
                    Require(warningErrors==0,"runtime error during soak");
                    if(clock.Elapsed.TotalSeconds>=nextSample||clock.Elapsed.TotalSeconds>=600)
                    {
                        GC.Collect();yield return null;
                        var allocated=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();var managed=GC.GetTotalMemory(false);
                        samples.Add(allocated);managedSamples.Add(managed);
                        report.Add("SOAK seconds="+clock.Elapsed.TotalSeconds.ToString("F1")+" loops="+loops+" transforms="+transforms+" audioSources="+audio+" unityAllocated="+allocated+" managed="+managed+" errors="+warningErrors);
                        File.WriteAllLines(output,report);nextSample=clock.Elapsed.TotalSeconds+60;
                    }
                }
                // Use warmed early/late windows: pools, fonts and both modes have been exercised.
                Require(samples.Count>=8,"insufficient soak samples");
                var early=(samples[2]+samples[3])/2;var late=(samples[samples.Count-2]+samples[samples.Count-1])/2;
                Require(late-early<16*1024*1024,"soak native allocation grows over 16 MiB between warmed windows");
                report.Add("SOAK PASS elapsed="+clock.Elapsed.TotalSeconds.ToString("F1")+"s loops="+loops+" transforms="+transforms+" audioSources="+audio+" warmedUnityDelta="+(late-early)+" errors="+warningErrors+"; current Mac only, memory samples retained");
            }
            finally{Application.logMessageReceived-=logCheck;}
        }
    }
}
#endif
