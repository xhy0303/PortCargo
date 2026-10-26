using System;
using System.Collections;
using PortCargo.Core;
using UnityEngine;

namespace PortCargo.Runtime
{
    /// <summary>Owns gameplay transactions and two saved runs. Navigation belongs to GameFlowController.</summary>
    public sealed class GamePresenter : MonoBehaviour
    {
        [SerializeField] private BoardView board;
        [SerializeField] private HudView hud;
        [SerializeField] private FeedbackPlayer feedback;
        [SerializeField] private SafeAreaLayout layout;
        [SerializeField] private GameFlowController flow;
        private LevelDefinition[] levels;
        private SaveRepository saves;
        private GameTuning tuning;
        private PlayerProfile profile;
        private GameSession session;
        private int index;
        private RunData campaignRun;
        private EndlessProgress endless;
        private bool isEndless;
        private Coroutine hintRoutine;
        private Solver search;
        private float highlightUntil;
        private IRewardedVideoProvider rewardedHints, rewardedSkip;
        private IRewardedVideoProvider activeAdProvider;
        private long adRequest;
        private bool adActive, appHidden;
        public void BindRewardedAds(IRewardedVideoProvider hints,IRewardedVideoProvider skip)
        { CancelRewardedRequest();rewardedHints=hints;rewardedSkip=skip; }
        public bool RewardedRequestActive => adActive;
        public bool CanOfferEndlessSkip => isEndless&&session!=null&&session.Status!=BoardStatus.Completed
            &&endless.round<int.MaxValue&&!adActive&&rewardedSkip!=null&&rewardedSkip.IsAvailable;
        public bool HasRewardedAds => rewardedHints!=null||rewardedSkip!=null;
        public bool IsEndless => isEndless;
        public int EndlessRound => endless?.round ?? 0;
        public GameSession Session => session;
        public BoardView BoardView => board;
        public PlayerProfile Profile => profile;
        public GameFlowController Flow => flow;
        public bool SavePending => saves!=null&&saves.HasPending;
        public int LevelCount => levels?.Length ?? 0;
        public int CampaignIndex => index;
        public RunData CampaignRun => !isEndless&&session!=null?session.Export():campaignRun;
        public EndlessProgress Endless => endless;
        public double LastMoveMilliseconds { get; private set; }

        public void Initialize(LevelDefinition[] catalog,GameTuning settings,SaveRepository repository,LoadResult loaded)
        {
            CancelRewardedRequest();CancelTransient();isEndless=false;
            levels=catalog;tuning=settings;saves=repository;profile=loaded.Value?.profile??new PlayerProfile();
            profile.completed.RemoveAll(id=>Array.FindIndex(levels,l=>l.Id==id)<0);
            profile.unlockedCount=Math.Min(levels.Length,Math.Max(1,profile.unlockedCount));
            foreach(var id in profile.completed)
                profile.unlockedCount=Math.Max(profile.unlockedCount,Math.Min(levels.Length,FindLevel(id)+2));
            var saved=loaded.Value?.activeRun;
            index=saved==null?0:FindLevel(saved.levelId);if(index<0)index=0;
            session=saved!=null&&saved.levelRevision==levels[index].Revision&&saved.levelId==levels[index].Id
                ?GameSession.Restore(levels[index],saved):NewSession(levels[index]);
            campaignRun=session.Export();endless=loaded.Value?.endless;
            if(loaded.Value?.mode=="endless"&&endless!=null)
            {isEndless=true;session=GameSession.Restore(new LevelDefinition(endless.level),endless.run);}
            // Existing players do not have to repeat the first-play guide after upgrading.
            if(profile.completed.Count>0||campaignRun.moveCount>0||endless!=null)profile.tutorialCompleted=true;
            session.Busy=true;Canvas.ForceUpdateCanvases();Render();Persist();
            if(!flow)throw new InvalidOperationException("BOOT_FLOW_REFERENCE");
            flow.Initialize(loaded.Reset?"未能读取进度，已从第一关开始":loaded.RecoveredOlderSlot?"已恢复上次有效进度":"");
        }
        private static GameSession NewSession(LevelDefinition level)=>new GameSession(level,Guid.NewGuid().ToString("N"));
        private void OnEnable()
        {
            board.MoveRequested+=Move;board.CoveredRequested+=Covered;
            hud.Undo+=Undo;hud.Hint+=Hint;hud.Settings+=OpenPause;hud.Queue+=OpenQueue;hud.RetrySave+=RetrySave;
            layout.Changed+=Render;
        }
        private void OnDisable()
        {
            board.MoveRequested-=Move;board.CoveredRequested-=Covered;
            hud.Undo-=Undo;hud.Hint-=Hint;hud.Settings-=OpenPause;hud.Queue-=OpenQueue;hud.RetrySave-=RetrySave;
            layout.Changed-=Render;CancelRewardedRequest();CancelTransient();
        }
        private void OpenPause()=>flow.OpenPause();
        private void OpenQueue()=>flow.OpenQueue();
        private void Move(string id,string run,long version,Vector3 origin)
        {
            if(session==null||flow.Screen!=FlowScreen.Playing)return;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var result=session.TryMove(id,run,version);LastMoveMilliseconds=timer.Elapsed.TotalMilliseconds;
            if(!result.Accepted){feedback.Reject();if(result.Reason==RejectReason.BufferFull)hud.Toast("暂存已满");return;}
            CancelTransient();UnlockCompleted();Render();Persist();
            feedback.Play(session.Level,result.Events,origin,hud.ShipPosition,board,tuning.BoxMoveSeconds,tuning.ShipDepartSeconds);
            var count=0;foreach(var e in result.Events)if(e.Kind==EventKind.ShipDeparted)count++;
            if(count>0)hud.Chain(count);
            flow.ShowBoardState();
        }
        private void Covered(int stack)
        {if(flow.Screen==FlowScreen.Playing){board.HighlightTop(stack);highlightUntil=Time.unscaledTime+.6f;}}
        private void UnlockCompleted()
        {
            if(isEndless||session.Status!=BoardStatus.Completed)return;
            if(!profile.completed.Contains(session.Level.Id))profile.completed.Add(session.Level.Id);
            profile.unlockedCount=Math.Max(profile.unlockedCount,Math.Min(levels.Length,index+2));
        }
        public void Undo()
        {
            if(session==null)return;
            if(session.TryUndo(session.RunId,session.Version)){CancelTransient();Render();Persist();flow.ShowBoardState();}
        }
        private void Hint()
        {
            if(session==null||session.Busy||adActive||flow.Screen!=FlowScreen.Playing||hintRoutine!=null)return;
            if(session.FreeHintUsed&&(rewardedHints==null||!rewardedHints.IsAvailable))
            {hud.Toast("免费提示已用，广告暂不可用；可撤销或重开");return;}
            hintRoutine=StartCoroutine(FindHint(session.FreeHintUsed));
        }
        private IEnumerator FindHint(bool rewarded)
        {
            var run=session.RunId;var version=session.Version;
            search=new Solver(session.Level,session.Board,session.Capacity,version,new SearchBudget(tuning.SolverNodes,tuning.SolverWallMilliseconds));
            yield return null;
            while(search!=null&&search.Step(2)==SearchStatus.Searching)yield return null;
            if(search==null)yield break;
            var status=search.Status;var solution=search.Solution;
            search.Dispose();search=null;hintRoutine=null;
            if(session.RunId!=run||session.Version!=version)yield break;
            if(status==SearchStatus.Found&&solution.Length>0)
            {
                if(rewarded){OfferRewardedHint(solution[0]);yield break;}
                if(session.UseHint(run,version,solution[0],false))ShowHint(solution[0]);
            }
            else hud.Toast(status==SearchStatus.Unsolvable?"试试撤销":"暂未找到");
        }
        private void ShowHint(string boxId)
        {board.Highlight(boxId);highlightUntil=Time.unscaledTime+1.2f;Persist();}
        private void OfferRewardedHint(string boxId)
        {
            var ticket=new RewardedHintTicket(session,boxId);
            flow.ConfirmRewardedHint(()=>{
                ShowRewarded(rewardedHints,result=>{
                    flow.ShowBoardState();
                    if(ticket.Resolve(session,result))ShowHint(boxId);
                    else hud.Toast(result==RewardedVideoResult.Cancelled?"未完成播放，未消耗提示":"暂未获得提示，可稍后重试");
                });
            });
        }
        public void RequestEndlessSkip()
        {
            if(!CanOfferEndlessSkip)return;
            var ticket=new RewardedSkipTicket(session,endless.round);
            flow.ConfirmRewardedSkip(()=>ShowRewarded(rewardedSkip,result=>{
                if(ticket.TryResolve(session,isEndless,endless.round,result))
                {
                    try { CancelTransient();CreateEndless(endless.round+1);Render();Persist();flow.ShowBoardState();hud.Toast("已跳过，进入下一程"); }
                    catch(Exception ex){Debug.LogError("PORTCARGO_AD_SKIP "+ex);flow.ShowBoardState();hud.Toast("暂时无法进入下一程，请重试");}
                }
                else {flow.ShowBoardState();hud.Toast(result==RewardedVideoResult.Cancelled?"未完成播放，仍在当前程":"未跳过程数，可稍后重试");}
            }));
        }
        private void ShowRewarded(IRewardedVideoProvider provider,Action<RewardedVideoResult> apply)
        {
            if(adActive)return;
            if(provider==null||!provider.IsAvailable){flow.ShowBoardState();hud.Toast("广告暂不可用，可继续游戏");return;}
            var request=++adRequest;adActive=true;activeAdProvider=provider;
            flow.ShowRewardedPending(()=>{
                // Once native Show has begun, the SDK owns closing the advertisement.
                if(activeAdProvider is ICancellableRewardedVideoProvider cancellable&&!cancellable.CanCancel)return;
                CancelRewardedRequest();flow.ShowBoardState();
            });
            feedback.PauseAudio();
            Action<RewardedVideoResult> done=result=>{
                if(!this||!adActive||request!=adRequest)return;
                adActive=false;activeAdProvider=null;
                if(!appHidden)feedback.ResumeAudio();
                if(flow.Screen!=FlowScreen.Advertisement)return;
                apply(result);
                if(appHidden)feedback.PauseAudio();
            };
            try {provider.Show(done);}catch(Exception){done(RewardedVideoResult.Failed);}
        }
        public void CancelRewardedRequest()
        {
            ++adRequest;var wasActive=adActive;adActive=false;
            var provider=activeAdProvider;activeAdProvider=null;
            if(provider is ICancellableRewardedVideoProvider cancellable)cancellable.Cancel();
            if(wasActive&&!appHidden&&feedback)feedback.ResumeAudio();
        }
        public bool IsLevelCompleted(int number)=>number>=0&&number<levels.Length&&profile.completed.Contains(levels[number].Id);
        public void ContinueCampaign(bool advanceCompleted=true)
        {
            CancelRewardedRequest();RememberRun();CancelTransient();isEndless=false;
            session=GameSession.Restore(levels[index],campaignRun);
            if(advanceCompleted&&session.Status==BoardStatus.Completed&&index+1<levels.Length&&index+1<profile.unlockedCount)
            {index++;session=NewSession(levels[index]);}
            Render();Persist();
        }
        public bool SelectCampaign(int next)
        {
            if(next<0||next>=levels.Length||next>=profile.unlockedCount)return false;
            if(next==index){ContinueCampaign(false);return true;}
            CancelRewardedRequest();RememberRun();CancelTransient();isEndless=false;index=next;session=NewSession(levels[index]);Render();Persist();return true;
        }
        public void RestartCurrent()
        {
            CancelRewardedRequest();CancelTransient();session=NewSession(isEndless?new LevelDefinition(endless.level):levels[index]);Render();Persist();
        }
        public void ContinueEndless(bool advanceCompleted=true)
        {
            CancelRewardedRequest();RememberRun();CancelTransient();
            if(endless==null)CreateEndless(1);
            else session=GameSession.Restore(new LevelDefinition(endless.level),endless.run);
            isEndless=true;
            if(advanceCompleted&&session.Status==BoardStatus.Completed)CreateEndless(endless.round==int.MaxValue?int.MaxValue:endless.round+1);
            Render();Persist();
        }
        private void CreateEndless(int round)
        {
            var recent=endless?.recentLayouts==null?new System.Collections.Generic.List<string>():new System.Collections.Generic.List<string>(endless.recentLayouts);
            if(endless!=null)
            {
                var previous=EndlessGenerator.LayoutSignature(endless.level);
                if(!recent.Contains(previous))recent.Add(previous);
            }
            var generated=EndlessGenerator.Generate(Guid.NewGuid().GetHashCode(),round,recent);
            recent.Add(EndlessGenerator.LayoutSignature(generated.Data));
            while(recent.Count>EndlessGenerator.RecentLimit)recent.RemoveAt(0);
            session=NewSession(new LevelDefinition(generated.Data));
            endless=new EndlessProgress{generatorVersion=generated.GeneratorVersion,seed=generated.Seed,round=round,level=generated.Data,run=session.Export(),recentLayouts=recent};
        }
        public void Next()
        {
            if(session.Status!=BoardStatus.Completed)return;
            if(isEndless)ContinueEndless();else ContinueCampaign();
        }
        public void SetPaused(bool value)
        {if(session==null)return;if(value)CancelTransient();session.Busy=value;Render();}
        public void Render()
        {
            if(session==null)return;
            board.Render(session,profile.reducedMotion);hud.Render(session,index,saves.HasPending,profile.reducedMotion,isEndless?endless.round:0);
            feedback.Configure(profile.sound,profile.reducedMotion,profile.music);
            if(flow)flow.RefreshHome();
        }
        private void RememberRun()
        {if(session==null)return;if(isEndless)endless.run=session.Export();else campaignRun=session.Export();}
        private bool Persist()
        {
            if(session==null)return false;RememberRun();
            var success=saves.Save(new PlayerSave{profile=profile,activeRun=campaignRun,mode=isEndless?"endless":"campaign",endless=endless});
            if(!success)hud.Toast("保存失败，点此重试");
            hud.Render(session,index,saves.HasPending,profile.reducedMotion,isEndless?endless.round:0);
            if(flow)flow.RefreshHome();return success;
        }
        public void RetrySave(){if(saves.Retry())hud.Toast("进度已保存");else hud.Toast("保存失败，点此重试");Render();}
        public bool SaveNow()=>Persist();
        public void FlushSave()=>Persist();
        private int FindLevel(string id){for(var i=0;i<levels.Length;i++)if(levels[i].Id==id)return i;return -1;}
        private void CancelTransient()
        {
            if(hintRoutine!=null){StopCoroutine(hintRoutine);hintRoutine=null;}search?.Dispose();search=null;
            if(board){board.ResetPointer();board.ClearHighlights();}if(feedback)feedback.Cancel();if(hud)hud.ClearTransient();highlightUntil=0;
        }
        private void Update(){if(highlightUntil>0&&Time.unscaledTime>=highlightUntil){board.ClearHighlights();highlightUntil=0;}}
        public void HandleHidden()
        {appHidden=true;if(session!=null){CancelTransient();Persist();flow.HandleHidden();feedback.PauseAudio();}}
        public void HandleShown(){appHidden=false;if(session!=null){if(!adActive)feedback.ResumeAudio();Render();}}
        private void OnApplicationPause(bool hidden){if(hidden)HandleHidden();else HandleShown();}
        private void OnApplicationQuit(){CancelTransient();Persist();}
    }
}
