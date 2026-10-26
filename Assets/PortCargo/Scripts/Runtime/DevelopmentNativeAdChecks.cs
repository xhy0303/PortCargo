#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PortCargo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    /// <summary>Explicit development-only UI geometry/input verification; never simulates a native ad impression.</summary>
    public sealed class DevelopmentNativeAdChecks : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string result,failure,scope="Real Unity UI pointer/navigation and reserved-slot geometry with explicit availability and in-memory failure fixtures; no WeChat ad/network/impression verification";
            public int screenWidth,screenHeight,pointerClicks;
            public bool originalStateRestored;
            public List<string> checks=new List<string>(),screenshots=new List<string>();
        }
        private sealed class MemoryStorage : IStoragePort
        {
            readonly Dictionary<string,string> values=new Dictionary<string,string>();
            public bool FailWrites;
            public string ReadString(string key)=>values.TryGetValue(key,out var text)?text:null;
            public void WriteString(string key,string text){if(FailWrites)throw new IOException("Injected in-memory save failure");values[key]=text;}
        }
        private sealed class PendingProvider : ICancellableRewardedVideoProvider
        {
            Action<RewardedVideoResult> completion;
            public bool IsAvailable => true;
            public bool CanCancel => true;
            public int Requests;
            public void Show(Action<RewardedVideoResult> callback){Requests++;completion=callback;}
            public void Complete(RewardedVideoResult result){var callback=completion;completion=null;callback?.Invoke(result);}
            public void Cancel()=>Complete(RewardedVideoResult.Cancelled);
        }
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly Evidence evidence=new Evidence();readonly List<InputDevice> disabled=new List<InputDevice>();
        GamePresenter presenter;GameFlowController flow;LevelDefinition[] levels;GameTuning tuning;
        SaveRepository originalRepository;PlayerSave originalSave;MemoryStorage fixtureStorage;
        IRewardedVideoProvider originalHints,originalSkip;
        bool originalHome,originalCompleted,originalPause,originalSettings,originalGrid,originalHidden,hasSnapshot,inputSaved;
        Mouse mouse;InputSystemUIInputModule module;UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices;
        InputSettings.BackgroundBehavior oldBackground;InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;bool oldRunBackground;
        string output;bool started;Action placementGuard;bool unexpectedPlacement;
        public bool Finished{get;private set;}public bool Passed{get;private set;}public string Failure{get;private set;}
        public void Begin(string evidencePath)
        {
            if(started)throw new InvalidOperationException("One run per native layout check component");
            if(!Path.IsPathRooted(evidencePath))throw new ArgumentException("Absolute output prefix required");
            started=true;output=evidencePath;Directory.CreateDirectory(Path.GetDirectoryName(output));StartCoroutine(Guarded(Run()));
        }
        IEnumerator Guarded(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                object next=null;bool moved;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception ex){Failure=ex.ToString();break;}
                if(!moved){stack.Pop();continue;}if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
            }
            try{RestoreState();}catch(Exception ex){Failure=(Failure??"")+"\nRestore: "+ex;}
            try{RestoreInput();}catch(Exception ex){Failure=(Failure??"")+"\nInput restore: "+ex;}
            Passed=string.IsNullOrEmpty(Failure);evidence.result=Passed?"PASS":"FAIL";evidence.failure=Failure;
            File.WriteAllText(output+".json",JsonUtility.ToJson(evidence,true));
            var lines=new List<string>{evidence.scope};lines.AddRange(evidence.checks);lines.Add("RESULT "+evidence.result);
            if(!Passed)lines.Add(Failure);File.WriteAllLines(output+".txt",lines);Finished=true;
            Debug.Log("PORTCARGO_NATIVE_LAYOUT_CHECKS "+evidence.result+" "+output+".json");
        }
        IEnumerator Run()
        {
            yield return null;presenter=FindFirstObjectByType<GamePresenter>();Require(presenter&&presenter.Session!=null,"Initialized presenter required");
            Require(!presenter.RewardedRequestActive,"No active rewarded request during native layout checks");flow=presenter.Flow;CaptureState();ConfigureInput();
            evidence.screenWidth=Screen.width;evidence.screenHeight=Screen.height;
            fixtureStorage=new MemoryStorage();
            presenter.Initialize(levels,tuning,Repository(fixtureStorage),new LoadResult{Value=Fresh(0)});
            presenter.BindRewardedAds(null,null);flow.SetNativeAdsHidden(false);SetAvailability(true);
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var home=FindFirstObjectByType<HomeView>();Require(home,"Visible home view required");
            var harbor=Read<RectTransform>(home,"harborPreview");Require(harbor,"Original harbor art required");
            var harborBaseline=ScreenRect(harbor);RequireHarborVisible(harbor);var homeButtons=ButtonGeometry();
            yield return Capture("home",NativeAdPlacement.Home);Check("N01","Home offers a separate small card on ordinary portrait phones, full harbor artwork and usable actions");
            SetAvailability(false);yield return null;RequireHarborVisible(harbor);
            SetAvailability(true);yield return null;yield return null;
            RequireSameHarbor(harbor,harborBaseline,true,"restoring availability");RequireSameButtons(homeButtons,"Restoring availability is deterministic");
            flow.SetNativeAdsHidden(true);RequireNoPlacement("Hidden app must invalidate native placement synchronously");
            flow.SetNativeAdsHidden(false);yield return null;yield return null;
            RequireSameHarbor(harbor,harborBaseline,true,"foreground recovery");
            Check("N02","Availability loss retains full artwork; restoring availability and foreground returns the same ad-enabled layout");

            yield return Press("设置");Require(flow.Screen==FlowScreen.Settings,"Home settings navigation");
            yield return Capture("settings-home",NativeAdPlacement.Settings);
            var sound=presenter.Profile.sound;BeginVisitGuard();yield return Press(sound?"音效：开":"音效：关");EndVisitGuard("Settings toggle must not emit None or change the native visit");
            Require(presenter.Profile.sound!=sound&&flow.Screen==FlowScreen.Settings,"Settings toggle remains functional with native reservation");
            yield return Press("隐私与反馈");RequireNoPlacement("Privacy excludes all native ads");yield return Press("返回");
            yield return Press("返回");Require(flow.Screen==FlowScreen.Home,"Settings returns home");
            foreach(var navigation in new[]{"选择关卡","玩法说明"})
            {
                yield return Press(navigation);RequireNoPlacement(navigation+" must not host native ad");
                yield return Press("返回");Require(flow.Screen==FlowScreen.Home,"Menu returns home");
            }
            yield return Press("退出游戏");RequireNoPlacement("Quit confirmation excludes ads");yield return Press("继续游戏");
            Check("N03","Real pointer settings toggle retains its native visit; privacy, levels, help and quit confirmation clear native placement");

            yield return Press("开始闯关");Require(flow.Screen==FlowScreen.Playing,"Campaign starts freely");
            yield return Capture("playing-campaign",NativeAdPlacement.GameplayGrid);
            var boardGeometry=BoardGeometry();var gameButtons=ButtonGeometry();var originalBoard=presenter.Session.Board.ExactKey();
            SetAvailability(false);yield return null;RequireSameBoard(boardGeometry);RequireSameButtons(gameButtons,"Grid loss must not move game controls");
            SetAvailability(true);yield return null;RequireSameBoard(boardGeometry);RequireSameButtons(gameButtons,"Grid availability must not move game controls");
            BeginVisitGuard();yield return OneMove();EndVisitGuard("Normal box move must not emit None or restart the grid visit");
            BeginVisitGuard();yield return Press("撤销");EndVisitGuard("Normal undo must not emit None or restart the grid visit");
            Require(presenter.Session.Board.ExactKey()==originalBoard,"Grid-enabled move and undo restore board");
            yield return Press("暂停");yield return Capture("pause-campaign",NativeAdPlacement.Pause);
            yield return Press("设置");yield return Capture("settings-paused",NativeAdPlacement.Settings);
            yield return Press("返回");Require(flow.Screen==FlowScreen.Paused,"Settings returns to pause");
            yield return Press("玩法说明");RequireNoPlacement("Pause help excludes native ads");yield return Press("返回");
            yield return Press("继续游戏");yield return Press("船队");RequireNoPlacement("Queue excludes gameplay grid");yield return Press("返回");
            Require(presenter.Session.Board.ExactKey()==originalBoard,"Menu navigation preserves exact campaign board");
            Check("N04","Pause/settings reserve banners; normal grid-enabled move and undo never emit None; availability and menus preserve controls/board");

            BoxView pressedBox=null;foreach(var box in presenter.BoardView.Boxes)if(box.gameObject.activeInHierarchy&&box.IsTop){pressedBox=box;break;}
            Require(pressedBox,"Top box for interrupted pointer check");var pressAt=Center(pressedBox.Rect);var beforeHide=JsonUtility.ToJson(presenter.Session.Export());
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pressAt}.WithButton(MouseButton.Left,true));yield return null;yield return null;
            flow.SetNativeAdsHidden(true);presenter.HandleHidden();RequireNoPlacement("Hidden game must not retain grid or pause ad");
            presenter.HandleShown();flow.SetNativeAdsHidden(false);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pressAt});yield return null;yield return null;
            Require(flow.Screen==FlowScreen.Paused&&JsonUtility.ToJson(presenter.Session.Export())==beforeHide,"Background return cancels held box press without changing run");
            yield return Press("继续游戏");Require(JsonUtility.ToJson(presenter.Session.Export())==beforeHide,"Continue does not replay stale held input");
            Check("N05","Injected background lifecycle hides ads and cancels a real held box pointer; returning preserves exact run");

            SetAvailability(false);presenter.BindRewardedAds(null,null);
            yield return Press("提示");yield return Until(()=>presenter.Session.FreeHintUsed,"Free hint available without any ad provider");
            RequireNoPlacement("No available native providers");yield return OneMove();yield return Press("撤销");
            Require(presenter.Session.Board.ExactKey()==originalBoard,"Free undo restores board with ads unavailable");
            yield return Press("暂停");yield return Press("重新开始");RequireNoPlacement("Restart confirmation excludes native ads");yield return Press("重新开始");
            Require(!presenter.Session.FreeHintUsed&&presenter.Session.Board.ExactKey()==originalBoard,"Free restart retains original rules without ads");
            SetAvailability(true);yield return SolveCurrent();Require(flow.Screen==FlowScreen.Completed,"Real box input completes first level");
            Require(FindButton("下一关")&&FindButton("重玩本关")&&FindButton("返回首页"),"Result retains all three actions");
            yield return Capture("completed-three-actions",NativeAdPlacement.Completed);
            yield return Press("下一关");Require(flow.Screen==FlowScreen.Playing,"Next level remains free");
            Check("N06","Unavailable-ad fixture allows free hint, box input, undo and restart; real first-level completion and next level work");

            flow.ShowHome();var skip=new PendingProvider();presenter.BindRewardedAds(null,skip);
            yield return Press("开启无尽");Require(presenter.IsEndless,"Endless starts freely");
            yield return Capture("playing-endless",NativeAdPlacement.GameplayGrid);
            yield return Press("暂停");Require(FindButton("看广告跳过本程"),"Seven-action endless pause includes voluntary skip");
            yield return Capture("pause-endless-seven-actions",NativeAdPlacement.Pause);
            var beforeAd=JsonUtility.ToJson(presenter.Session.Export());var round=presenter.EndlessRound;
            yield return Press("看广告跳过本程");RequireNoPlacement("Rewarded confirmation excludes banner and grid");
            yield return Press("看广告跳过本程");Require(presenter.RewardedRequestActive&&skip.Requests==1,"Explicit skip click opens one pending request");
            RequireNoPlacement("Pending rewarded request excludes all native ads");
            skip.Complete(RewardedVideoResult.Failed);yield return null;yield return null;
            Require(!presenter.RewardedRequestActive&&flow.Screen==FlowScreen.Playing&&presenter.EndlessRound==round
                &&JsonUtility.ToJson(presenter.Session.Export())==beforeAd,"Failed rewarded request leaves exact endless run unchanged");
            Check("N07","Seven-action pause remains usable; voluntary rewarded confirmation and pending request hide native ads; injected failure preserves run");

            fixtureStorage.FailWrites=true;Require(!presenter.SaveNow()&&presenter.SavePending,"Injected save failure is pending");
            RequireNoPlacement("Save failure suppresses native overlay before retry controls");
            fixtureStorage.FailWrites=false;presenter.RetrySave();yield return null;
            Require(!presenter.SavePending,"Save retry recovers in-memory repository");
            Check("N08","Save failure suppresses overlays and retry recovers; all fixture writes remain isolated from real player storage");

            var observedStacks=new HashSet<int>();
            for(var i=0;i<levels.Length;i++)
            {
                var count=levels[i].Stacks.Count;if(count<4||!observedStacks.Add(count))continue;
                presenter.Initialize(levels,tuning,Repository(),new LoadResult{Value=Fresh(i)});SetAvailability(true);
                presenter.ContinueCampaign();flow.ShowBoardState();yield return null;
                yield return Capture("playing-"+count+"-stacks",NativeAdPlacement.GameplayGrid);
            }
            Check("N09","Distinct campaign stack counts checked for grid bounds and protected game areas: "+string.Join(",",observedStacks));

            var last=levels[levels.Length-1];var lastRun=new RunData{runId="native-final-layout-"+Guid.NewGuid().ToString("N"),
                levelId=last.Id,levelRevision=last.Revision,stateVersion=last.BoxCount,moveCount=last.BoxCount,
                board=new BoardSnapshot{heights=new int[last.Stacks.Count],shipIndex=last.Ships.Count,shipLoad=0}};
            GameSession.Restore(last,lastRun);
            var profile=new PlayerProfile{tutorialCompleted=true,sound=false,music=false,reducedMotion=true,unlockedCount=levels.Length};
            foreach(var level in levels)profile.completed.Add(level.Id);
            presenter.Initialize(levels,tuning,Repository(),new LoadResult{Value=new PlayerSave{profile=profile,activeRun=lastRun,mode="campaign"}});
            SetAvailability(true);yield return Press("继续闯关");
            Require(flow.Screen==FlowScreen.Completed&&FindButton("选择关卡")&&FindButton("重玩本关")&&FindButton("开启无尽")&&FindButton("返回首页"),
                "Final result preserves four original actions");
            yield return Capture("completed-four-actions",NativeAdPlacement.Completed);
            yield return Press("重玩本关");Require(flow.Screen==FlowScreen.Playing,"Final-level replay remains free");
            Check("N10","Validated final-level completion fixture keeps all four result actions and free replay");
            Require(NativeAdsPresenter.TryConvertRect(new Rect(60,900,960,336),1080,2400,360,800,out var css)
                &&Mathf.Abs(css.x-20)<.01f&&Mathf.Abs(css.y-388)<.01f&&Mathf.Abs(css.width-320)<.01f&&Mathf.Abs(css.height-112)<.01f,"DPR3 native CSS conversion and Y flip");
            Check("N11","Real Unity helper maps DPR3 pixels to CSS coordinates and flips Y correctly");
        }
        PlayerSave Fresh(int index)=>new PlayerSave{profile=new PlayerProfile{tutorialCompleted=true,sound=false,music=false,reducedMotion=true,unlockedCount=index+1},
            activeRun=new GameSession(levels[index],"native-layout-"+Guid.NewGuid().ToString("N")).Export(),mode="campaign"};
        void SetAvailability(bool value)=>flow.SetNativeAdAvailability(value,value,value,value,value);
        void BeginVisitGuard()
        {
            if(placementGuard!=null)flow.NativeAdPlacementChanged-=placementGuard;
            placementGuard=null;unexpectedPlacement=false;
            if(!flow.TryGetNativeAdPlacement(out var expected,out _))return;
            placementGuard=()=>{if(!flow.TryGetNativeAdPlacement(out var current,out _)||current!=expected)unexpectedPlacement=true;};
            flow.NativeAdPlacementChanged+=placementGuard;
        }
        void EndVisitGuard(string message)
        {
            if(placementGuard!=null)flow.NativeAdPlacementChanged-=placementGuard;placementGuard=null;
            Require(!unexpectedPlacement,message);
        }
        void RequireNoPlacement(string message)=>Require(!flow.TryGetNativeAdPlacement(out _,out _),message);
        IEnumerator Until(Func<bool> condition,string message)
        {var deadline=Time.realtimeSinceStartup+10;while(!condition()){Require(Time.realtimeSinceStartup<deadline,"Timed out: "+message);yield return null;}}
        IEnumerator OneMove()
        {
            var session=presenter.Session;
            using(var solver=new Solver(session.Level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                Require(solver.Status==SearchStatus.Found&&solver.Solution.Length>0,"Fixture has a solvable move");
                BoxView box=null;foreach(var candidate in presenter.BoardView.Boxes)if(candidate.gameObject.activeInHierarchy&&candidate.BoxId==solver.Solution[0]){box=candidate;break;}
                Require(box,"Rendered move box exists");var before=session.Version;yield return Click(Center(box.Rect));Require(session.Version==before+1,"Pointer accepts one move");
            }
        }
        IEnumerator SolveCurrent()
        {
            var session=presenter.Session;
            using(var solver=new Solver(session.Level,session.Board,session.Capacity,session.Version,new SearchBudget(200000,3000)))
            {
                while(solver.Step(100)==SearchStatus.Searching)yield return null;
                Require(solver.Status==SearchStatus.Found,"First level solution");
                foreach(var id in solver.Solution)
                {
                    BoxView box=null;foreach(var candidate in presenter.BoardView.Boxes)if(candidate.gameObject.activeInHierarchy&&candidate.BoxId==id){box=candidate;break;}
                    Require(box,"Rendered solution box exists");var before=session.Version;yield return Click(Center(box.Rect));Require(session.Version==before+1,"Pointer accepted each box exactly once");
                }
            }
        }
        IEnumerator Capture(string name,NativeAdPlacement expected)
        {
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var offered=flow.TryGetNativeAdPlacement(out var actual,out var bounds);
            if(expected!=NativeAdPlacement.GameplayGrid&&Screen.width>=320&&Screen.width<=500&&Screen.height>=640&&Screen.height>Screen.width)
                Require(offered,"Normal phone portrait must reserve the configured native slot: "+name);
            if(offered)
            {
                Require(actual==expected,"Placement belongs to current screen");
                Require(bounds.xMin>=-.5f&&bounds.yMin>=-.5f&&bounds.xMax<=Screen.width+.5f&&bounds.yMax<=Screen.height+.5f,"Native slot fits screen");
                Require(bounds.width>0&&bounds.height>0,"Native slot dimensions are positive");
                if(expected!=NativeAdPlacement.GameplayGrid)Require(bounds.height>=bounds.width*7f/20f,"Card slot reserves full 20:7 template height");
                var safe=PortCargo.Platform.WeChatPlatform.SafeAreaPixels();Require(bounds.xMin>=safe.xMin-.5f&&bounds.xMax<=safe.xMax+.5f
                    &&bounds.yMin>=safe.yMin-.5f&&bounds.yMax<=safe.yMax+.5f,"Native slot fits entire safe area");
                foreach(var button in VisibleButtons())
                {
                    var r=ScreenRect((RectTransform)button.transform);
                    Require(!bounds.Overlaps(r),"Native slot must not overlap button "+button.name);
                }
                if(expected==NativeAdPlacement.Home)
                {
                    Require(bounds.yMin>=safe.yMin-.5f&&bounds.yMax<=safe.yMax+.5f,"Home footer native slot fits vertical safe area");
                    var home=FindFirstObjectByType<HomeView>();var harbor=Read<RectTransform>(home,"harborPreview");
                    RequireHarborVisible(harbor);Require(!bounds.Overlaps(ScreenRect(harbor)),"Home native slot must not cover original harbor art");
                }
                if(expected==NativeAdPlacement.GameplayGrid)RequireGridAvoidsGame(bounds);
                evidence.checks.Add("GEOMETRY "+name+" slot="+bounds+" at "+Screen.width+"x"+Screen.height);
            }
            else evidence.checks.Add("GEOMETRY "+name+" no slot: original game UI retained at "+Screen.width+"x"+Screen.height);
            foreach(var button in VisibleButtons())
            {
                var r=ScreenRect((RectTransform)button.transform);
                Require(r.width>=43.9f&&r.height>=43.9f&&r.xMin>=-.5f&&r.yMin>=-.5f&&r.xMax<=Screen.width+.5f&&r.yMax<=Screen.height+.5f,
                    "Visible action remains in bounds with 44px touch target: "+button.name);
            }
            var path=output+"."+name+".png";ScreenCapture.CaptureScreenshot(path);
            var deadline=Time.realtimeSinceStartup+10;while(!File.Exists(path)){Require(Time.realtimeSinceStartup<deadline,"Screenshot save timeout");yield return null;}
            evidence.screenshots.Add(path);
        }
        static void RequireHarborVisible(RectTransform harbor)
        {
            Require(harbor&&harbor.gameObject.activeInHierarchy,"Full harbor artwork is active");
            var r=ScreenRect(harbor);var safe=PortCargo.Platform.WeChatPlatform.SafeAreaPixels();
            Require(r.width>0&&r.height>0&&r.xMin>=safe.xMin-.5f&&r.xMax<=safe.xMax+.5f&&r.yMin>=safe.yMin-.5f&&r.yMax<=safe.yMax+.5f,"Full harbor rect stays within safe area");
            Require(Mathf.Abs(harbor.localScale.x-harbor.localScale.y)<.001f,"Harbor scales uniformly");
            foreach(var name in new[]{"ArtBackground","ArtShip","ArtCrane","ArtDock"})
            {
                var child=harbor.Find(name);var image=child?child.GetComponent<UnityEngine.UI.Image>():null;
                Require(image&&image.gameObject.activeInHierarchy&&image.sprite&&image.type!=UnityEngine.UI.Image.Type.Filled,"Harbor illustration retains "+name);
            }
        }
        Dictionary<string,Rect> BoardGeometry()
        {
            var result=new Dictionary<string,Rect>();foreach(var box in presenter.BoardView.Boxes)if(box.gameObject.activeInHierarchy)result[box.BoxId]=ScreenRect(box.Rect);return result;
        }
        void RequireSameBoard(Dictionary<string,Rect> expected)
        {var actual=BoardGeometry();Require(actual.Count==expected.Count,"Grid availability retains every box");foreach(var pair in expected)Require(actual.TryGetValue(pair.Key,out var rect)&&(rect.position-pair.Value.position).sqrMagnitude<.1f&&(rect.size-pair.Value.size).sqrMagnitude<.1f,"Grid availability must not move or resize box "+pair.Key);}
        void RequireGridAvoidsGame(Rect bounds)
        {
            foreach(var box in presenter.BoardView.Boxes)if(box.gameObject.activeInHierarchy)Require(!bounds.Overlaps(ScreenRect(box.Rect)),"Grid does not overlap box "+box.BoxId);
            var stack=Read<RectTransform>(presenter.BoardView,"stackArea");Require(!bounds.Overlaps(ScreenRect(stack)),"Grid avoids complete stack/animation area");
            foreach(var frame in Read<GameObject[]>(presenter.BoardView,"bufferFrames"))if(frame.activeInHierarchy)Require(!bounds.Overlaps(ScreenRect((RectTransform)frame.transform)),"Grid avoids temporary cargo buffer");
            var hud=FindFirstObjectByType<HudView>();Require(hud,"Active HUD for grid checks");
            var level=Read<TMP_Text>(hud,"levelLabel");var backing=level?level.GetComponent<HarborTextPlate>():null;
            if(backing&&backing.plate&&backing.plate.gameObject.activeInHierarchy)
                Require(!bounds.Overlaps(ScreenRect(backing.plate.rectTransform)),"Grid avoids title readability plate");
            foreach(var field in new[]{"levelLabel","shipCount","nextLabel","instruction","toast","chain"})
            {var label=Read<TMP_Text>(hud,field);if(label&&label.gameObject.activeInHierarchy)Require(!bounds.Overlaps(ScreenRect(label.rectTransform)),"Grid avoids HUD text region "+field);}
            var ship=Read<RectTransform>(hud,"shipTarget");if(ship&&ship.gameObject.activeInHierarchy)Require(!bounds.Overlaps(ScreenRect(ship)),"Grid avoids ship target and loading animation");
            var cargo=Read<CargoGraphic>(hud,"currentCargo");if(cargo&&cargo.gameObject.activeInHierarchy)Require(!bounds.Overlaps(ScreenRect((RectTransform)cargo.transform)),"Grid avoids current cargo");
            foreach(var preview in Read<CargoGraphic[]>(hud,"nextCargo"))if(preview&&preview.gameObject.activeInHierarchy)
                Require(!bounds.Overlaps(ScreenRect((RectTransform)preview.transform.parent)),"Grid avoids upcoming ship preview");
        }
        static IEnumerable<Button> VisibleButtons()
        {
            var dialog=FindFirstObjectByType<DialogView>();var panel=dialog?dialog.transform.Find("Dialog"):null;
            foreach(var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {if(dialog&&dialog.Visible&&panel&&!b.transform.IsChildOf(panel))continue;yield return b;}
        }
        static Dictionary<string,Rect> ButtonGeometry()
        {var result=new Dictionary<string,Rect>();foreach(var b in VisibleButtons())result[b.name]=ScreenRect((RectTransform)b.transform);return result;}
        static void RequireSameButtons(Dictionary<string,Rect> baseline,string label)
        {var current=ButtonGeometry();foreach(var pair in baseline){Require(current.TryGetValue(pair.Key,out var rect),label+" missing "+pair.Key);Require((rect.position-pair.Value.position).sqrMagnitude<.1f&&(rect.size-pair.Value.size).sqrMagnitude<.1f,label+" changed "+pair.Key);}}
        static void RequireSameHarbor(RectTransform harbor,Rect baseline,bool visible,string transition)
        {
            Canvas.ForceUpdateCanvases();var current=ScreenRect(harbor);
            Require(harbor.gameObject.activeInHierarchy==visible,"Harbor visibility changed after "+transition);
            Require((current.position-baseline.position).sqrMagnitude<.1f&&(current.size-baseline.size).sqrMagnitude<.1f,
                "Original harbor position or dimensions changed after "+transition+": baseline="+baseline+" current="+current);
        }
        static Rect ScreenRect(RectTransform transform)
        {var corners=new Vector3[4];transform.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
        static Button FindButton(string label)
        {foreach(var b in VisibleButtons()){var text=b.GetComponentInChildren<TMP_Text>();if(text&&text.text.Replace("\n","  ")==label)return b;}return null;}
        IEnumerator Press(string label)
        {var b=FindButton(label);Require(b&&b.interactable,"Visible enabled button "+label);yield return Click(Center((RectTransform)b.transform));}
        static Vector2 Center(RectTransform r)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
        IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position}.WithButton(MouseButton.Left,true));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;evidence.pointerClicks++;
        }
        T Read<T>(object obj,string field)=>(T)obj.GetType().GetField(field,Fields).GetValue(obj);
        SaveRepository Repository(MemoryStorage storage=null)=>new SaveRepository(storage??new MemoryStorage(),new PortCargo.Platform.UnityJsonCodec(),save=>ProgressValidator.Validate(save,levels));
        void CaptureState()
        {
            levels=Read<LevelDefinition[]>(presenter,"levels");tuning=Read<GameTuning>(presenter,"tuning");originalRepository=Read<SaveRepository>(presenter,"saves");
            originalHints=Read<IRewardedVideoProvider>(presenter,"rewardedHints");originalSkip=Read<IRewardedVideoProvider>(presenter,"rewardedSkip");
            originalSave=JsonUtility.FromJson<PlayerSave>(JsonUtility.ToJson(new PlayerSave{profile=presenter.Profile,activeRun=presenter.CampaignRun,
                mode=presenter.IsEndless?"endless":"campaign",endless=presenter.Endless}));
            if(presenter.IsEndless)originalSave.endless.run=presenter.Session.Export();
            originalHome=Read<bool>(flow,"nativeHomeAvailable");originalCompleted=Read<bool>(flow,"nativeCompletedAvailable");originalHidden=Read<bool>(flow,"nativeHidden");
            originalPause=Read<bool>(flow,"nativePauseAvailable");originalSettings=Read<bool>(flow,"nativeSettingsAvailable");originalGrid=Read<bool>(flow,"nativeGridAvailable");hasSnapshot=true;
        }
        void RestoreState()
        {
            if(flow&&placementGuard!=null)flow.NativeAdPlacementChanged-=placementGuard;placementGuard=null;
            if(!hasSnapshot||!presenter)return;
            SetAvailability(false);presenter.Initialize(levels,tuning,Repository(),new LoadResult{Value=originalSave});
            presenter.BindRewardedAds(originalHints,originalSkip);presenter.HandleShown();
            typeof(GamePresenter).GetField("saves",Fields).SetValue(presenter,originalRepository);
            flow.SetNativeAdsHidden(originalHidden);flow.SetNativeAdAvailability(originalHome,originalCompleted,originalPause,originalSettings,originalGrid);
            evidence.originalStateRestored=true;hasSnapshot=false;
        }
        void ConfigureInput()
        {
            module=FindFirstObjectByType<InputSystemUIInputModule>();Require(module&&module.actionsAsset,"InputSystem UI module required");
            oldDevices=module.actionsAsset.devices;oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            oldRunBackground=Application.runInBackground;inputSaved=true;Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var device in InputSystem.devices)if(device.enabled&&device is UnityEngine.InputSystem.Pointer){disabled.Add(device);InputSystem.DisableDevice(device);}
            mouse=InputSystem.AddDevice<Mouse>("NativeAdLayoutMouse");mouse.MakeCurrent();module.actionsAsset.devices=new InputDevice[]{mouse};
        }
        void RestoreInput()
        {
            if(!inputSaved)return;if(module&&module.actionsAsset)module.actionsAsset.devices=oldDevices;
            if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}foreach(var device in disabled)if(device.added)InputSystem.EnableDevice(device);disabled.Clear();
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;Application.runInBackground=oldRunBackground;inputSaved=false;
        }
        void Check(string id,string detail)=>evidence.checks.Add(id+" PASS "+detail);
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        void OnDestroy(){if(!started||Finished)return;try{RestoreState();}finally{RestoreInput();}}
    }
}
#endif
