using System;
using System.Collections.Generic;
using System.Text;
using PortCargo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortCargo.Runtime
{
    public enum FlowScreen { Home,Playing,Paused,LevelSelect,Help,Settings,Privacy,Queue,Completed,Deadlocked,Confirm,BootError,Advertisement }

    /// <summary>Screen navigation only. Views can be replaced without changing rules or saved runs.</summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private HomeView home;
        [SerializeField] private DialogView dialog;
        [SerializeField] private GamePresenter presenter;
        [SerializeField] private HudView hud;
        [SerializeField] private BoardView board;
        private bool ready,allowQuit;
        private bool nativeHomeAvailable,nativeCompletedAvailable,nativePauseAvailable,nativeSettingsAvailable,nativeGridAvailable,nativeHidden,nativeTransitioning;
        private NativeAdPlacement publishedNativePlacement;
        private Rect publishedNativeRect;
        private string notice="";
        private Action back,redisplay;
        public FlowScreen Screen { get; private set; }=FlowScreen.Home;
        public bool HomeVisible => home&&home.Visible;
        public event Action NativeAdPlacementChanged;

        /// <summary>Configuration/platform support, not transient Load status. Failure never gates navigation.</summary>
        public void SetNativeAdAvailability(bool homeAvailable,bool completedAvailable,bool pauseAvailable=false,bool settingsAvailable=false,bool gridAvailable=false)
        {
            if(nativeHomeAvailable==homeAvailable&&nativeCompletedAvailable==completedAvailable
                &&nativePauseAvailable==pauseAvailable&&nativeSettingsAvailable==settingsAvailable&&nativeGridAvailable==gridAvailable)return;
            BeginNativeTransition();nativeHomeAvailable=homeAvailable;nativeCompletedAvailable=completedAvailable;
            nativePauseAvailable=pauseAvailable;nativeSettingsAvailable=settingsAvailable;nativeGridAvailable=gridAvailable;EndNativeTransition();
        }
        public void SetNativeAdsHidden(bool hidden)
        {
            if(nativeHidden==hidden)return;
            BeginNativeTransition();nativeHidden=hidden;EndNativeTransition();
        }
        public bool TryGetNativeAdPlacement(out NativeAdPlacement placement,out Rect screenPixels)
        {
            placement=NativeAdPlacement.None;screenPixels=default;
            if(nativeTransitioning||!NativeAdContextAllowed())return false;
            RectTransform slot=null;
            if(Screen==FlowScreen.Home&&nativeHomeAvailable&&home&&home.Visible&&home.NativeAdReserved&&(!dialog||!dialog.Visible))
            {placement=NativeAdPlacement.Home;slot=home.NativeAdSlot;}
            else if(Screen==FlowScreen.Completed&&nativeCompletedAvailable&&dialog&&dialog.Visible&&dialog.NativeAdReserved)
            {placement=NativeAdPlacement.Completed;slot=dialog.NativeAdSlot;}
            else if(Screen==FlowScreen.Paused&&nativePauseAvailable&&dialog&&dialog.Visible&&dialog.NativeAdReserved)
            {placement=NativeAdPlacement.Pause;slot=dialog.NativeAdSlot;}
            else if(Screen==FlowScreen.Settings&&nativeSettingsAvailable&&dialog&&dialog.Visible&&dialog.NativeAdReserved)
            {placement=NativeAdPlacement.Settings;slot=dialog.NativeAdSlot;}
            else if(Screen==FlowScreen.Playing&&nativeGridAvailable&&hud&&hud.NativeAdReserved&&(!dialog||!dialog.Visible))
            {placement=NativeAdPlacement.GameplayGrid;slot=hud.NativeAdSlot;}
            if(slot&&(placement==NativeAdPlacement.GameplayGrid||NativeAdPlacementLayout.CanReserve(slot,slot.rect.width,slot.rect.height))
                &&NativeAdPlacementLayout.TryGetScreenRect(slot,out screenPixels))return true;
            placement=NativeAdPlacement.None;screenPixels=default;return false;
        }
        private bool NativeAdContextAllowed()
        {return ready&&isActiveAndEnabled&&!nativeHidden&&presenter&&!presenter.RewardedRequestActive&&!presenter.SavePending;}
        private void RefreshNativeAdLayout()
        {
            var allowed=NativeAdContextAllowed();
            if(home)home.SetNativeAdAvailable(allowed&&nativeHomeAvailable&&Screen==FlowScreen.Home);
            if(dialog)dialog.SetNativeAdAvailable(allowed&&((nativeCompletedAvailable&&Screen==FlowScreen.Completed)
                ||(nativePauseAvailable&&Screen==FlowScreen.Paused)||(nativeSettingsAvailable&&Screen==FlowScreen.Settings)));
            if(hud)hud.SetNativeAdAvailable(allowed&&nativeGridAvailable&&Screen==FlowScreen.Playing);
        }
        private void BeginNativeTransition()
        {
            if(nativeTransitioning)return;
            nativeTransitioning=true;
            // Publish an invalid placement before any card, board or menu can move underneath a native overlay.
            PublishNativeAdPlacement(true);
        }
        private void EndNativeTransition()
        {RefreshNativeAdLayout();nativeTransitioning=false;PublishNativeAdPlacement();}
        private void PublishNativeAdPlacement(bool force=false)
        {
            TryGetNativeAdPlacement(out var placement,out var rect);
            if(!force&&placement==publishedNativePlacement&&Mathf.Abs(rect.x-publishedNativeRect.x)<.5f
                &&Mathf.Abs(rect.y-publishedNativeRect.y)<.5f&&Mathf.Abs(rect.width-publishedNativeRect.width)<.5f
                &&Mathf.Abs(rect.height-publishedNativeRect.height)<.5f)return;
            publishedNativePlacement=placement;publishedNativeRect=rect;
            var listeners=NativeAdPlacementChanged;
            if(listeners==null)return;
            foreach(Action listener in listeners.GetInvocationList())
            {try{listener();}catch(Exception ex){Debug.LogException(ex);}}
        }
        private void LateUpdate(){RefreshNativeAdLayout();PublishNativeAdPlacement();}
        private void OnEnable()
        {
            if(home){home.Campaign+=OpenCampaign;home.Endless+=OpenEndless;home.Levels+=HomeLevels;
                home.Help+=HomeHelp;home.Settings+=HomeSettings;home.Quit+=ConfirmQuit;home.RetrySave+=RetrySave;}
            Application.wantsToQuit+=WantsToQuit;
            nativeTransitioning=false;RefreshNativeAdLayout();PublishNativeAdPlacement();
        }
        private void OnDisable()
        {
            BeginNativeTransition();RefreshNativeAdLayout();
            if(home){home.Campaign-=OpenCampaign;home.Endless-=OpenEndless;home.Levels-=HomeLevels;
                home.Help-=HomeHelp;home.Settings-=HomeSettings;home.Quit-=ConfirmQuit;home.RetrySave-=RetrySave;}
            Application.wantsToQuit-=WantsToQuit;
        }
        public void Initialize(string message){ready=true;notice=message;ShowHome();}
        public void ShowHome()
        {
            if(!ready)return;
            BeginNativeTransition();
            presenter.CancelRewardedRequest();
            presenter.SetPaused(true);presenter.SaveNow();SetGameVisible(false);dialog.Hide();
            Screen=FlowScreen.Home;back=null;redisplay=ShowHome;RefreshHome(true);
            EndNativeTransition();
        }
        private void SetGameVisible(bool visible)
        {hud.gameObject.SetActive(visible);board.gameObject.SetActive(visible);}
        public void RefreshHome(bool force=false)
        {
            if(!ready||(!force&&!home.Visible))return;
            // A save failure must hide an existing native overlay before exposing the retry UI.
            PublishNativeAdPlacement();
            var campaign=presenter.CampaignRun;
            var start=presenter.Profile.completed.Count==0&&campaign.moveCount==0;
            var endless=presenter.Endless;
            var progress=endless==null?"每程一个新码头，随时回来继续":"无尽进度：第 "+endless.round+" 程";
            home.Show(presenter.Profile.completed.Count,presenter.LevelCount,start?"开始闯关":"继续闯关",
                endless==null?"开启无尽":"继续无尽",progress,presenter.SavePending,notice);
            RefreshNativeAdLayout();PublishNativeAdPlacement();
        }
        private void OpenCampaign()=>AfterGuide(()=>{presenter.ContinueCampaign();ShowBoardState();});
        private void OpenEndless()=>AfterGuide(()=>{presenter.ContinueEndless();ShowBoardState();});
        private void AfterGuide(Action begin)
        {
            BeginNativeTransition();
            if(presenter.Profile.tutorialCompleted){begin();return;}
            ShowHelp(0,ShowHome,()=>{presenter.Profile.tutorialCompleted=true;presenter.SaveNow();begin();});
        }
        private void HomeLevels()=>OpenLevels(ShowHome);
        private void HomeHelp()=>ShowHelp(0,ShowHome);
        private void HomeSettings()=>OpenSettings(ShowHome);
        private void RetrySave(){presenter.RetrySave();notice=presenter.SavePending?"保存未成功，请稍后重试":"进度已保存";RefreshHome();}
        public void ShowBoardState()
        {
            // Normal moves and undo refresh the board without starting another ad visit.
            if(Screen==FlowScreen.Playing&&presenter.Session.Status==BoardStatus.Playing)
            {presenter.SetPaused(false);RefreshNativeAdLayout();PublishNativeAdPlacement();return;}
            BeginNativeTransition();
            home.Hide();SetGameVisible(true);
            var status=presenter.Session.Status;
            if(status==BoardStatus.Playing)
            {Screen=FlowScreen.Playing;back=OpenPause;redisplay=ShowBoardState;dialog.Hide();presenter.SetPaused(false);EndNativeTransition();return;}
            presenter.SetPaused(true);
            if(status==BoardStatus.Deadlocked)
            {
                var options=new List<string>{"撤销","重新开始","返回首页"};
                if(presenter.CanOfferEndlessSkip)options.Add("看广告跳过本程");
                Show(FlowScreen.Deadlocked,"暂时无法装船","试试撤销，换一个取箱顺序",options.ToArray(),choice=>{
                    if(choice==0){presenter.SetPaused(false);presenter.Undo();}
                    else if(choice==1)ConfirmRestart(ShowBoardState);else if(choice==2)ShowHome();else presenter.RequestEndlessSkip();
                },ShowHome);
                return;
            }
            var run=presenter.Session.Export();
            var detail="撤销 "+run.undoCount+" 次 · 提示 "+run.hintCount+" 次";
            if(presenter.IsEndless)
            {
                Show(FlowScreen.Completed,"本程完成","第 "+presenter.EndlessRound+" 程已完成\n"+detail,new[]{"下一程","重玩本程","返回首页"},choice=>{
                    if(choice==0)presenter.Next();else if(choice==1)presenter.RestartCurrent();else{ShowHome();return;}ShowBoardState();
                },ShowHome);return;
            }
            var all=presenter.Profile.completed.Count==presenter.LevelCount;
            var last=presenter.CampaignIndex==presenter.LevelCount-1;
            Show(FlowScreen.Completed,all?"全部通关":"装船完成",(all?"已完成全部 "+presenter.LevelCount+" 关": "第 "+(presenter.CampaignIndex+1)+" 关已完成")+"\n"+detail,
                last?new[]{"选择关卡","重玩本关","开启无尽","返回首页"}:new[]{"下一关","重玩本关","返回首页"},choice=>{
                    if(last){if(choice==0){OpenLevels(ShowBoardState);return;}if(choice==2){presenter.ContinueEndless();ShowBoardState();return;}if(choice==3){ShowHome();return;}}
                    else if(choice==2){ShowHome();return;}
                    if(choice==1)presenter.RestartCurrent();else presenter.Next();ShowBoardState();
                },ShowHome);
        }
        public void OpenPause()
        {
            if(!ready||presenter.RewardedRequestActive)return;
            var options=new List<string>{"继续游戏","重新开始","选择关卡","玩法说明","设置","返回首页"};
            if(presenter.CanOfferEndlessSkip)options.Add("看广告跳过本程");
            Show(FlowScreen.Paused,"休息一下","进度会自动保存",options.ToArray(),choice=>{
                if(choice==0)ShowBoardState();else if(choice==1)ConfirmRestart(OpenPause);
                else if(choice==2)OpenLevels(OpenPause);else if(choice==3)ShowHelp(0,OpenPause);
                else if(choice==4)OpenSettings(OpenPause);else if(choice==5)ShowHome();else presenter.RequestEndlessSkip();
            },ShowBoardState);
        }
        private void OpenSettings(Action returnTo)
        {
            var profile=presenter.Profile;
            Show(FlowScreen.Settings,"设置","声音和动效设置会保存在本机",new[]{profile.sound?"音效：开":"音效：关",profile.music?"音乐：开":"音乐：关",
                profile.reducedMotion?"减少动效：开":"减少动效：关","隐私与反馈","返回"},choice=>{
                if(choice==4){returnTo();return;}
                if(choice==3){OpenPrivacy(()=>OpenSettings(returnTo));return;}
                if(choice==0)profile.sound=!profile.sound;if(choice==1)profile.music=!profile.music;if(choice==2)profile.reducedMotion=!profile.reducedMotion;
                presenter.SaveNow();presenter.Render();OpenSettings(returnTo);
            },returnTo);
        }
        private void OpenPrivacy(Action returnTo)
        {
            var ads=presenter.HasRewardedAds?"可自愿观看微信广告获得提示或跳程。\n广告由微信提供，需要联网。":"此版本不提供广告。";
            if(nativeHomeAvailable||nativeCompletedAvailable||nativePauseAvailable||nativeSettingsAvailable||nativeGridAvailable)
                ads=(presenter.HasRewardedAds?"可自愿观看微信广告获得提示或跳程。\n":"")+"游戏页面有微信广告。\n广告由微信提供，需要联网。";
            Show(FlowScreen.Privacy,"隐私与反馈","进度和设置保存在本机。\n"+ads+"\n反馈请记录关卡、程数和操作步骤。\n版本 "+Application.version,new[]{"返回"},_=>returnTo(),returnTo);
        }
        private void ShowHelp(int page,Action returnTo,Action start=null)
        {
            var headings=new[]{"先取顶部","同类装船","其余暂存"};
            var messages=new[]{"点击每一堆最上面的箱子。\n下方的箱子要等上面移走后才能取。",
                "与当前船纹样相同的箱子会直接装船。\n满载后下一艘船自动进港。\n点“船队”可以查看后续顺序。",
                "其余箱子会放进三格暂存。\n遇到同类船时会自动装载。\n卡住可以撤销、提示或重新开始。"};
            var options=new List<string>();if(page>0)options.Add("上一页");
            var main=options.Count;options.Add(page<2?"下一页":start!=null?"开始游戏":"返回");
            var cancel=-1;if(page<2||start!=null){cancel=options.Count;options.Add("返回");}
            Show(FlowScreen.Help,headings[page]+"  "+(page+1)+"/3",messages[page],options.ToArray(),choice=>{
                if(choice==cancel){returnTo();return;}
                if(page>0&&choice==0){ShowHelp(page-1,returnTo,start);return;}
                if(choice==main){if(page<2)ShowHelp(page+1,returnTo,start);else{presenter.Profile.tutorialCompleted=true;presenter.SaveNow();if(start!=null)start();else returnTo();}}
            },returnTo);
        }
        public void OpenQueue()=>ShowQueue(0);
        private void ShowQueue(int page)
        {
            if(!ready)return;
            const int pageSize=6;
            var session=presenter.Session;var first=session.Board.shipIndex;
            var remaining=session.Level.Ships.Count-first;
            page=Math.Max(0,Math.Min(Math.Max(0,(remaining-1)/pageSize),page));
            var start=first+page*pageSize;var end=Math.Min(start+pageSize,session.Level.Ships.Count);
            var message=new StringBuilder();
            for(var i=start;i<end;i++)
            {var ship=session.Level.Ships[i];message.Append(i+1).Append(". ").Append(HudView.TypeName(ship.Type)).Append(" ×").Append(ship.Capacity).Append('\n');}
            var options=new List<string>();var previous=-1;var next=-1;
            if(page>0){previous=options.Count;options.Add("上一页");}
            if(end<session.Level.Ships.Count){next=options.Count;options.Add("下一页");}
            var exit=options.Count;options.Add("返回");
            Show(FlowScreen.Queue,"后续船队",message.ToString().TrimEnd(),options.ToArray(),choice=>{
                if(choice==exit)ShowBoardState();else if(choice==previous)ShowQueue(page-1);else if(choice==next)ShowQueue(page+1);
            },ShowBoardState);
        }
        private void OpenLevels(Action returnTo,int page=-1)
        {
            dialog.CurrentLevel=presenter.CampaignIndex+1;
            const int pageSize=8;
            if(page<0)page=presenter.CampaignIndex/pageSize;
            page=Math.Max(0,Math.Min((presenter.LevelCount-1)/pageSize,page));
            var current=page;var start=page*pageSize;var count=Math.Min(pageSize,presenter.LevelCount-start);
            var options=new List<string>();var enabled=new List<bool>();
            for(var i=0;i<count;i++)
            {
                var n=start+i;var unlocked=n<presenter.Profile.unlockedCount;
                options.Add("第 "+(n+1)+" 关"+(!unlocked?"  未解锁":presenter.IsLevelCompleted(n)?"  已完成":""));enabled.Add(unlocked);
            }
            var previous=-1;var next=-1;
            if(page>0){previous=options.Count;options.Add("上一页");enabled.Add(true);}
            if(start+count<presenter.LevelCount){next=options.Count;options.Add("下一页");enabled.Add(true);}
            var exit=options.Count;options.Add("返回");enabled.Add(true);
            Show(FlowScreen.LevelSelect,"选择关卡",(start+1)+"–"+(start+count)+" / "+presenter.LevelCount+"\n通过前一关后解锁",options.ToArray(),choice=>{
                if(choice==exit){returnTo();return;}if(choice==previous){OpenLevels(returnTo,current-1);return;}if(choice==next){OpenLevels(returnTo,current+1);return;}
                var selected=start+choice;if(selected>=presenter.Profile.unlockedCount)return;
                Action enter=()=>AfterGuide(()=>{presenter.SelectCampaign(selected);ShowBoardState();});
                var run=presenter.CampaignRun;
                if(selected!=presenter.CampaignIndex&&run.moveCount>0&&CampaignHasRemainingCargo())
                    Show(FlowScreen.Confirm,"切换关卡？","将开始第 "+(selected+1)+" 关，\n当前闯关局面将被替换。",new[]{"切换关卡","返回选关"},c=>{if(c==0)enter();else OpenLevels(returnTo,current);},()=>OpenLevels(returnTo,current));
                else enter();
            },returnTo,enabled.ToArray());
        }
        private bool CampaignHasRemainingCargo()
        {
            var board=presenter.CampaignRun.board;
            if(board.buffer.Count>0)return true;
            foreach(var height in board.heights)if(height>0)return true;
            return false;
        }
        public void ConfirmRewardedHint(Action watch)
        {
            Show(FlowScreen.Confirm,"额外提示","看完广告，显示当前局面的一次提示。\n需要时可以再次观看获取。",new[]{"看广告获取提示","继续游戏"},choice=>{
                if(choice==1){ShowBoardState();return;}
                watch();
            },ShowBoardState);
        }
        public void ConfirmRewardedSkip(Action watch)
        {
            Show(FlowScreen.Confirm,"跳过本程？","看完广告后，放弃本程局面，进入下一程。\n下一程仍按原有难度生成。\n也可以继续本程、免费撤销或重开。",new[]{"看广告跳过本程","继续本程"},choice=>{
                if(choice==1){ShowBoardState();return;}watch();
            },ShowBoardState);
        }
        public void ShowRewardedPending(Action cancel)
        {
            Show(FlowScreen.Advertisement,"正在打开广告","完成观看后发放奖励。\n加载时可返回游戏；广告打开后，\n请使用广告中的关闭按钮。",new[]{"返回游戏"},_=>cancel(),cancel);
        }
        private void ConfirmRestart(Action returnTo)
        {
            Show(FlowScreen.Confirm,"重新开始？","将从本局第一箱重新开始",new[]{"重新开始","继续这一局"},choice=>{
                if(choice==0){presenter.RestartCurrent();ShowBoardState();}else returnTo();
            },returnTo);
        }
        public void ConfirmQuit()
        {
            if(presenter&&presenter.RewardedRequestActive)return;
            if(!ready){ExitNow();return;}
            var returnTo=redisplay??ShowHome;
            Show(FlowScreen.Confirm,"退出游戏？","退出前会保存两种模式的进度",new[]{"退出游戏","继续游戏"},choice=>{
                if(choice==1){returnTo();return;}
                if(presenter.SaveNow()){ExitNow();return;}
                ShowQuitSaveFailure(returnTo);
            },returnTo);
        }
        private void ShowQuitSaveFailure(Action returnTo)
        {
            Show(FlowScreen.Confirm,"保存未成功","可以重试或留在游戏。\n仍然退出可能丢失最近的操作。",new[]{"重试保存","留在游戏","仍然退出"},choice=>{
                if(choice==1){returnTo();return;}if(choice==2){ExitNow();return;}
                if(presenter.SaveNow())ExitNow();else ShowQuitSaveFailure(returnTo);
            },returnTo);
        }
        private void Show(FlowScreen screen,string title,string body,string[] options,Action<int> chosen,Action returnTo,bool[] enabled=null)
        {
            var sameSettings=screen==FlowScreen.Settings&&Screen==FlowScreen.Settings;
            if(!sameSettings)BeginNativeTransition();
            if(ready)presenter.SetPaused(true);Screen=screen;
            back=()=>{BeginNativeTransition();returnTo?.Invoke();if(nativeTransitioning)EndNativeTransition();};
            RefreshNativeAdLayout();
            redisplay=()=>Show(screen,title,body,options,chosen,returnTo,enabled);
            dialog.Show(title,body,options,choice=>{
                // Toggling a preference updates labels only; retain a dismissed/failed ad for this visit.
                if(screen!=FlowScreen.Settings||choice>=3)BeginNativeTransition();
                chosen(choice);if(nativeTransitioning)EndNativeTransition();
            },enabled);
            EndNativeTransition();
        }
        public void HandleHidden(){if(ready&&Screen==FlowScreen.Playing)OpenPause();}
        public void ShowBootError(Action retry)
        {
            BeginNativeTransition();
            ready=false;if(home)home.Hide();SetGameVisible(false);
            Show(FlowScreen.BootError,"暂时无法进入","请重试；进度文件会保留。",new[]{"重试","退出游戏"},choice=>{if(choice==0)retry();else ExitNow();},ExitNow);
        }
        private bool WantsToQuit(){if(allowQuit||!ready)return true;ConfirmQuit();return false;}
        private void ExitNow()
        {
            BeginNativeTransition();nativeHidden=true;RefreshNativeAdLayout();
            allowQuit=true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            PortCargo.Platform.WeChatPlatform.Exit();
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void AllowExitForChecks(){allowQuit=true;}
#endif
        private void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)
            {if(Screen==FlowScreen.Home)ConfirmQuit();else back?.Invoke();}
        }
    }
}
