#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PortCargo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PortCargo.Runtime
{
    /// <summary>Development-only evidence harness. Queues hardware-level pointer states on real frames.</summary>
    public sealed partial class DevelopmentChecks : MonoBehaviour
    {
        private Mouse mouse;
        private Touchscreen touch;
        private UnityEngine.InputSystem.UI.InputSystemUIInputModule module;
        private UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices;
        private readonly List<InputDevice> suspendedDevices=new List<InputDevice>();
        private GamePresenter presenter;
        private readonly List<string> report=new List<string>();
        private string output;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }

        public void Begin(string reportPath)
        { Initialize(reportPath);StartCoroutine(Guarded(Sweep())); }
        public void BeginLayout(string reportPath){Initialize(reportPath);StartCoroutine(Guarded(Layout()));}
        public void BeginEdges(string reportPath)
        { Initialize(reportPath);StartCoroutine(Guarded(Edges())); }
        public void BeginPersistence(string reportPath,bool write)
        { Initialize(reportPath);StartCoroutine(Guarded(Persistence(write)));StartCoroutine(QuitAfterReport()); }
        public void BeginPlayerSweep(string reportPath){Initialize(reportPath);StartCoroutine(Guarded(Sweep()));StartCoroutine(QuitAfterReport());}
        public void BeginSoak(string reportPath){Initialize(reportPath);StartCoroutine(Guarded(Soak()));StartCoroutine(QuitAfterReport());}
        /// <summary>Two-process expansion smoke; uses only the caller's isolated save fixture.</summary>
        public void BeginExpansionSmoke(string reportPath,bool read=false)
        {
            Initialize(reportPath+(read?".read.txt":".write.txt"));
            StartCoroutine(Guarded(ExpansionSmoke(reportPath+".expected.json",read)));
#if !UNITY_EDITOR
            StartCoroutine(QuitAfterReport());
#endif
        }
        private void Initialize(string reportPath)
        {
            output=reportPath;presenter=FindFirstObjectByType<GamePresenter>();
            Application.runInBackground=true;
            oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var device in InputSystem.devices)if(device.enabled&&device is UnityEngine.InputSystem.Pointer){suspendedDevices.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("PortCargoValidationMouse");mouse.MakeCurrent();
            module=FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();oldDevices=module.actionsAsset.devices;module.actionsAsset.devices=new[]{(InputDevice)mouse};
        }

        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object value=null;bool moved;
                try{moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}
                catch(Exception ex){Failure=ex.ToString();report.Add("FAIL "+Failure);break;}
                if(!moved){stack.Pop();continue;}
                if(value is IEnumerator child){stack.Push(child);continue;}
                yield return value;
            }
            Passed=Failure==null;Finished=true;
            report.Add("RESULT "+(Passed?"PASS":"FAIL"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllLines(output,report);
            RestoreInput();
            Debug.Log("PORTCARGO_POINTER_CHECKS "+(Passed?"PASS":"FAIL")+" "+output);
        }

        private IEnumerator Sweep()
        {
            yield return null;
            var resume=FindButton("继续");if(resume)yield return Click(Center((RectTransform)resume.transform));
            for(var level=0;level<10;level++)
            {
                var session=presenter.Session;
                if(session.Status==BoardStatus.Completed)
                {
                    var next=FindButton("下一关");if(!next)break;
                    yield return Click(Center((RectTransform)next.transform));session=presenter.Session;
                }
                if(session.Busy)throw new Exception("Unexpected paused board");
                using(var solver=new Solver(session.Level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
                {
                    while(solver.Step(100)==SearchStatus.Searching)yield return null;
                    if(solver.Status!=SearchStatus.Found)throw new Exception("No replay solution");
                    foreach(var id in solver.Solution)
                    {
                        var view=FindBox(id);var version=session.Version;
                        yield return Click(Center(view.Rect));
                        if(session.Version!=version+1)throw new Exception("Pointer failed for "+id+" v="+session.Version+" expected="+(version+1));
                    }
                }
                if(session.Status!=BoardStatus.Completed)throw new Exception("Not complete "+session.Level.Id);
                report.Add("U01 "+session.Level.Id+" PASS screen pointer through InputSystemUIInputModule; moves="+session.Export().moveCount+" ruleMs="+presenter.LastMoveMilliseconds.ToString("F3"));
                if(session.Level.Id=="L10")break;
            }
        }

        private IEnumerator Edges()
        {
            yield return null;
            var resume=FindButton("继续");if(resume)yield return Click(Center((RectTransform)resume.transform));
            if(presenter.Session.Status==BoardStatus.Completed)
            {yield return PressButton("选择关卡");yield return PressButton("第 4 关  已完成");}
            else {yield return PressButton("设置");yield return PressButton("选择关卡");yield return PressButton("第 4 关  已完成");}
            yield return PressButton("设置");yield return PressButton("重新开始");yield return PressButton("重新开始");
            var session=presenter.Session;var start=session.Board.ExactKey();var version=session.Version;
            var covered=FindBox(session.Level.Stacks[0].Boxes[0].Id);
            // The center can be occluded by the next box; click the visibly exposed lower strip.
            yield return Click(RectTransformUtility.WorldToScreenPoint(null,covered.Rect.TransformPoint(new Vector3(0,covered.Rect.rect.yMin+8,0))));
            Require(session.Version==version,"covered box changed board");
            var top=FindBox(GameRules.Top(session.Level,session.Board,0));var pos=Center(top.Rect);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos}.WithButton(MouseButton.Left,true));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+Vector2.right*40}.WithButton(MouseButton.Left,true));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+Vector2.right*40});yield return null;yield return null;
            Require(session.Version==version,"drag submitted a move");
            yield return Click(pos);Require(session.Version==version+1,"one click did not move exactly once");
            yield return PressButton("撤销");Require(session.Board.ExactKey()==start,"undo did not restore board");
            touch=InputSystem.AddDevice<Touchscreen>("PortCargoValidationTouch");module.actionsAsset.devices=new InputDevice[]{mouse,touch};
            version=session.Version;
            var first=Center(FindBox(GameRules.Top(session.Level,session.Board,0)).Rect);
            var second=Center(FindBox(GameRules.Top(session.Level,session.Board,1)).Rect);
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=first});yield return null;yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=second});yield return null;yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=second});yield return null;yield return null;
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=first});yield return null;yield return null;
            Require(session.Version==version+1,"two simultaneous fingers moved more or less than once");
            InputSystem.RemoveDevice(touch);touch=null;mouse.MakeCurrent();module.actionsAsset.devices=new[]{(InputDevice)mouse};
            yield return PressButton("撤销");Require(session.Board.ExactKey()==start,"multitouch undo");
            report.Add("U01 PASS InputSystem two-finger overlap accepts only first gesture");
            yield return PressButton("提示");
            for(var i=0;i<12&&!session.FreeHintUsed;i++)yield return null;
            Require(session.FreeHintUsed,"hint not granted");
            yield return Click(Center(FindBox(GameRules.Top(session.Level,session.Board,0)).Rect));
            yield return PressButton("撤销");Require(session.FreeHintUsed&&session.Board.ExactKey()==start,"undo revoked hint");
            report.Add("U01/R07 PASS covered click, drag cancellation, one move per gesture, undo, current-state hint");
            yield return PressButton("设置");if(presenter.Profile.sound)yield return PressButton("音效：开");if(!presenter.Profile.reducedMotion)yield return PressButton("减少动效：关");yield return PressButton("继续");
            using(var solver=new Solver(session.Level,session.Board,3,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                foreach(var id in solver.Solution)yield return Click(Center(FindBox(id).Rect));
            }
            Require(session.Status==BoardStatus.Completed,"reduced-motion L04 completion");
            var watch=System.Diagnostics.Stopwatch.StartNew();yield return PressButton("下一关");
            Require(presenter.Session.Level.Id=="L05"&&!presenter.Session.Busy&&watch.ElapsedMilliseconds<800,"next level latency");
            report.Add("U04/U05 PASS muted reduced-motion L04; next level "+watch.ElapsedMilliseconds+"ms including test gesture frames");
            var objects=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            for(var i=0;i<50;i++)
            {yield return PressButton("设置");yield return PressButton("重新开始");yield return PressButton("重新开始");}
            Require(FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==objects,"restart object leak");
            Require(presenter.Session.Export().moveCount==0&&presenter.Session.Capacity==3,"restart state");
            report.Add("U06 partial PASS 50 UI restarts; scene transform count stable="+objects+"; ten-minute device memory test not run");
            yield return PressButton("设置");yield return PressButton("选择关卡");yield return PressButton("第 10 关  已完成");
            var minWidth=float.MaxValue;var minHeight=float.MaxValue;
            foreach(var b in presenter.BoardView.Boxes)if(b.gameObject.activeInHierarchy&&b.IsTop)
            {var corners=new Vector3[4];b.Rect.GetWorldCorners(corners);minWidth=Mathf.Min(minWidth,corners[2].x-corners[0].x);minHeight=Mathf.Min(minHeight,corners[1].y-corners[0].y);}
            Require(minWidth>=44&&minHeight>=44,"narrow screen touch target too small");
            report.Add("U02 PASS viewport="+Screen.width+"x"+Screen.height+" six-stack top targets min="+minWidth.ToString("F1")+"x"+minHeight.ToString("F1")+" screen px");
        }

        private IEnumerator Layout()
        {
            yield return null;yield return null;Canvas.ForceUpdateCanvases();presenter.Render();
            foreach(var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var c=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(c);var w=c[2].x-c[0].x;var h=c[1].y-c[0].y;
                Require(w>=43.9f&&h>=43.9f&&c[0].x>=-.5f&&c[0].y>=-.5f&&c[2].x<=Screen.width+.5f&&c[2].y<=Screen.height+.5f,"button layout "+b.name);
                report.Add(b.name+" "+w.ToString("F1")+"x"+h.ToString("F1")+" PASS");
            }
            foreach(var t in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                foreach(var c in t.text)Require(char.IsWhiteSpace(c)||t.font.HasCharacter(c),"missing glyph "+c);
            report.Add("U02 runtime viewport="+Screen.width+"x"+Screen.height+"; active controls in bounds; no missing visible glyphs");
        }
        private IEnumerator Persistence(bool write)
        {
            yield return null;yield return null;
            if(write)
            {
                Require(presenter.Session.Level.Id=="L01"&&presenter.Session.Version==0,"write probe must use a fresh save directory");
                presenter.Profile.sound=false;presenter.Profile.reducedMotion=true;
                yield return Click(Center(FindBox(GameRules.Top(presenter.Session.Level,presenter.Session.Board,0)).Rect));
                Require(presenter.Session.Version==1&&!presenter.SavePending,"write through UI did not persist");
                report.Add("U07 WRITE PASS actual UI move saved; version=1; undo=1; muted; reducedMotion");
            }
            else
            {
                var data=presenter.Session.Export();
                Require(data.levelId=="L01"&&data.stateVersion==1&&data.undo.Count==1&&data.board.heights[0]==0,"fresh process did not restore board and history");
                Require(!presenter.Profile.sound&&presenter.Profile.reducedMotion,"fresh process did not restore settings");
                yield return PressButton("撤销");Require(presenter.Session.Board.heights[0]==1&&!presenter.SavePending,"restored undo failed");
                report.Add("U07 READ PASS distinct process restored board/settings/history; UI undo persisted");
            }
            presenter.FlushSave();
            
        }
        [Serializable]
        private sealed class ExpansionExpected
        {
            public string runId,levelId,boardKey,initialBoardKey,campaignRunId,campaignBoardKey;
            public long version,campaignVersion;
            public int round,undoCount;
        }
        private IEnumerator ExpansionSmoke(string expectedPath,bool read)
        {
            yield return null;yield return null;
            var resume=FindButton("继续");if(resume)yield return PressButton("继续");
            if(read)
            {
                Require(File.Exists(expectedPath),"expansion READ requires the WRITE expected snapshot");
                var expected=JsonUtility.FromJson<ExpansionExpected>(File.ReadAllText(expectedPath));
                var run=presenter.Session.Export();
                Require(presenter.IsEndless&&presenter.EndlessRound==expected.round,"fresh process did not restore endless mode/round");
                Require(run.runId==expected.runId&&run.levelId==expected.levelId&&run.stateVersion==expected.version&&
                    run.board.ExactKey()==expected.boardKey&&run.undo.Count==expected.undoCount,"fresh process endless board/history mismatch");
                Require(!presenter.Profile.sound&&!presenter.Profile.music&&presenter.Profile.reducedMotion,"fresh process settings mismatch");
                AssertVisibleGlyphs();
                yield return PressButton("撤销");
                Require(presenter.Session.Board.ExactKey()==expected.initialBoardKey&&presenter.Session.Export().undo.Count==0,
                    "fresh process endless undo did not restore starting board");
                Require(!presenter.SavePending,"fresh process endless undo did not persist");
                report.Add("X07 READ PASS separate player process restored endless round 3, exact board/run/version/history/settings; real UI undo restored starting board");
                yield return PressButton("设置");yield return PressButton("返回闯关");
                Require(!presenter.IsEndless&&presenter.Session.Level.Id=="L50"&&presenter.Session.Status==BoardStatus.Completed&&
                    presenter.Session.RunId==expected.campaignRunId&&presenter.Session.Version==expected.campaignVersion&&
                    presenter.Session.Board.ExactKey()==expected.campaignBoardKey,"fresh process campaign progress was not retained separately");
                Require(presenter.Profile.completed.Contains("L50"),"L50 completion missing after endless restore");
                AssertVisibleGlyphs();
                report.Add("X08 READ PASS return to campaign restored the completed L50 run without resetting either mode");
                presenter.FlushSave();Require(!presenter.SavePending,"read smoke final save pending");
                yield break;
            }
            Require(!presenter.IsEndless&&presenter.Session.Level.Id=="L10"&&presenter.Session.Status==BoardStatus.Completed,
                "expansion WRITE requires isolated legacy L10-completed save fixture");
            Require(presenter.Profile.unlockedCount==11,"old ten-level save must migrate to unlock L11");
            yield return PressButton("下一关");
            Require(presenter.Session.Level.Id=="L11"&&!presenter.IsEndless,"legacy completion did not advance through UI to L11");
            report.Add("X01 PASS old ten-level save migrated to unlockedCount=11; actual next-level button opened L11");
            // This is an isolated test fixture, not a claim that normal players unlock 50 levels at once.
            presenter.Profile.unlockedCount=50;presenter.Profile.sound=false;presenter.Profile.music=false;presenter.Profile.reducedMotion=true;
            presenter.FlushSave();presenter.Render();
            report.Add("FIXTURE isolated save manually unlocked 50 for quick content sampling; sounds disabled and reduced motion enabled");
            yield return CompleteCurrentBoard("X02 L11");
            yield return SelectCampaignLevel(25);
            yield return CompleteCurrentBoard("X03 L25");
            yield return SelectCampaignLevel(50);
            yield return CompleteCurrentBoard("X04 L50");
            var campaign=presenter.Session.Export();
            yield return PressButton("无尽模式");
            Require(presenter.IsEndless&&presenter.EndlessRound==1,"endless entry did not open first round");
            AssertVisibleGlyphs();
            yield return CompleteCurrentBoard("X05 endless round 1");
            yield return PressButton("下一程");
            Require(presenter.IsEndless&&presenter.EndlessRound==2,"first endless completion did not advance");
            yield return CompleteCurrentBoard("X05 endless round 2");
            yield return PressButton("下一程");
            Require(presenter.IsEndless&&presenter.EndlessRound==3,"second endless completion did not advance");
            var initial=presenter.Session.Board.ExactKey();
            using(var solver=new Solver(presenter.Session.Level,presenter.Session.Board,presenter.Session.Capacity,presenter.Session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                Require(solver.Status==SearchStatus.Found&&solver.Solution.Length>1,"third endless round did not provide a playable solution");
                yield return Click(Center(FindBox(solver.Solution[0]).Rect));
            }
            var partial=presenter.Session.Export();
            Require(partial.stateVersion==1&&partial.undo.Count==1&&presenter.Session.Status==BoardStatus.Playing,"third endless round move/history");
            yield return PressButton("设置");AssertVisibleGlyphs();yield return PressButton("选择关卡");yield return PressButton("第 50 关  已完成");
            Require(!presenter.IsEndless&&presenter.Session.RunId==campaign.runId&&presenter.Session.Status==BoardStatus.Completed&&
                presenter.Session.Board.ExactKey()==campaign.board.ExactKey(),"switching to campaign discarded campaign progress");
            yield return PressButton("无尽模式");
            Require(presenter.IsEndless&&presenter.EndlessRound==3&&presenter.Session.RunId==partial.runId&&
                presenter.Session.Version==partial.stateVersion&&presenter.Session.Board.ExactKey()==partial.board.ExactKey(),
                "switching to endless discarded its partial round");
            AssertVisibleGlyphs();
            presenter.FlushSave();Require(!presenter.SavePending,"expansion write save pending");
            var expectedWrite=new ExpansionExpected{runId=partial.runId,levelId=partial.levelId,boardKey=partial.board.ExactKey(),initialBoardKey=initial,
                version=partial.stateVersion,undoCount=partial.undo.Count,round=3,campaignRunId=campaign.runId,
                campaignBoardKey=campaign.board.ExactKey(),campaignVersion=campaign.stateVersion};
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));File.WriteAllText(expectedPath,JsonUtility.ToJson(expectedWrite,true));
            report.Add("X06 WRITE PASS endless first two rounds completed through real pointer input; round 3 one move saved; campaign/endless round trip preserved both exact runs");
        }
        private IEnumerator CompleteCurrentBoard(string label)
        {
            var session=presenter.Session;
            Require(!session.Busy&&session.Status==BoardStatus.Playing,label+" must begin in playable state");
            AssertVisibleGlyphs();
            using(var solver=new Solver(session.Level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                Require(solver.Status==SearchStatus.Found,label+" no replay solution");
                foreach(var id in solver.Solution)
                {
                    var version=session.Version;
                    yield return Click(Center(FindBox(id).Rect));
                    Require(session.Version==version+1,label+" pointer failed for "+id);
                }
            }
            Require(session.Status==BoardStatus.Completed,label+" did not complete");
            AssertVisibleGlyphs();
            report.Add(label+" PASS real screen pointer replay; moves="+session.Export().moveCount+" ruleMs="+presenter.LastMoveMilliseconds.ToString("F3"));
        }
        private IEnumerator SelectCampaignLevel(int number)
        {
            var direct=FindButton("选择关卡");
            if(direct)yield return PressButton("选择关卡");
            else
            {
                if(presenter.Session.Status==BoardStatus.Completed)yield return PressButton("重玩本关");
                yield return PressButton("设置");yield return PressButton("选择关卡");
            }
            for(var attempt=0;attempt<8;attempt++)
            {
                AssertVisibleGlyphs();
                var target=FindButton("第 "+number+" 关")??FindButton("第 "+number+" 关  已完成");
                if(target)
                {
                    if(number==50)
                    {
                        Require((FindButton("第 49 关")??FindButton("第 49 关  已完成"))&&FindButton("上一页")&&!FindButton("下一页"),"last level page navigation is incorrect");
                        var levelButtons=0;
                        foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                        {var text=button.GetComponentInChildren<TMP_Text>();if(text&&text.text.StartsWith("第 ")&&text.text.Contains(" 关"))levelButtons++;}
                        Require(levelButtons==2,"last page should show exactly L49 and L50");
                        report.Add("X04 paging PASS last page contains L49/L50 and previous/back controls; no next page");
                    }
                    yield return Click(Center((RectTransform)target.transform));
                    Require(!presenter.IsEndless&&presenter.Session.Level.Id=="L"+number.ToString("00"),"level page selected wrong board");
                    yield break;
                }
                var next=FindButton("下一页");Require(next,"requested level missing from paged catalog");
                yield return Click(Center((RectTransform)next.transform));
            }
            throw new Exception("paged level selection exceeded seven pages");
        }
        private static void AssertVisibleGlyphs()
        {
            foreach(var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                foreach(var character in text.text)
                    Require(char.IsWhiteSpace(character)||text.font.HasCharacter(character),"missing visible glyph "+character);
        }
        private IEnumerator Soak()
        {
            yield return null;yield return null;
            var end=Time.realtimeSinceStartup+600f;var nextSample=0f;var loops=0;
            while(Time.realtimeSinceStartup<end)
            {
                var session=presenter.Session;
                if(session.Status==BoardStatus.Completed){yield return PressButton("重玩本关");session=presenter.Session;}
                if(session.Busy)yield return PressButton("继续");
                using(var solver=new Solver(session.Level,session.Board,3,session.Version,new SearchBudget(200000,3000)))
                {
                    while(solver.Step(100)==SearchStatus.Searching)yield return null;
                    Require(solver.Status==SearchStatus.Found,"soak solve");
                    foreach(var id in solver.Solution){var version=session.Version;yield return Click(Center(FindBox(id).Rect));Require(session.Version==version+1,"soak pointer missed "+id+" before="+version+" after="+session.Version+" busy="+session.Busy+" screen="+Screen.width+"x"+Screen.height);}
                }
                Require(session.Status==BoardStatus.Completed,"soak completion");loops++;
                if(Time.realtimeSinceStartup>=nextSample)
                {
                    GC.Collect();
                    report.Add("SOAK seconds="+Time.realtimeSinceStartup.ToString("F1")+" loops="+loops+" transforms="+FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" audioSources="+FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length+" unityAllocated="+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()+" managed="+GC.GetTotalMemory(false));
                    File.WriteAllLines(output,report);nextSample=Time.realtimeSinceStartup+60;
                }
            }
            report.Add("U06 macOS ten-minute pointer soak PASS loops="+loops+"; device-specific testing remains separate");
        }
        private IEnumerator QuitAfterReport(){while(!Finished)yield return null;if(presenter&&presenter.Flow)presenter.Flow.AllowExitForChecks();Application.Quit(Passed?0:1);}
        private IEnumerator PressButton(string text)
        {var b=FindButton(text);if(!b)throw new Exception("Missing visible button "+text);yield return Click(Center((RectTransform)b.transform));}
        private static void Require(bool condition,string failure){if(!condition)throw new Exception(failure);}

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position}.WithButton(MouseButton.Left,true));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
        }
        private BoxView FindBox(string id){foreach(var b in presenter.BoardView.Boxes)if(b.gameObject.activeInHierarchy&&b.BoxId==id)return b;throw new Exception("Missing visible box "+id);}
        private static Vector2 Center(RectTransform rect)=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        private static UnityEngine.UI.Button FindButton(string label)
        {
            var dialog=FindFirstObjectByType<DialogView>();var panel=dialog?dialog.transform.Find("Dialog"):null;
            foreach(var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {if(dialog&&dialog.Visible&&panel&&!b.transform.IsChildOf(panel))continue;var text=b.GetComponentInChildren<TMP_Text>();if(text&&text.text.Replace("\n","  ")==label)return b;}
            return null;
        }
        private void RestoreInput()
        {
            if(module&&module.actionsAsset)module.actionsAsset.devices=oldDevices;
            if(touch!=null){InputSystem.RemoveDevice(touch);touch=null;}
            if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
            foreach(var device in suspendedDevices)if(device.added)InputSystem.EnableDevice(device);suspendedDevices.Clear();
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;
        }
        private void OnDestroy(){if(mouse!=null)RestoreInput();}
    }
}
#endif
