using System;
using PortCargo.Core;
using UnityEngine;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace PortCargo.Platform
{
    /// <summary>
    /// Voluntary, single-use rewarded requests. Only WeChat's explicit isEnded close
    /// signal can complete a reward; desktop/editor never simulate an ad.
    /// GamePresenter owns game/audio suspension and rejects obsolete board rewards.
    /// </summary>
    public sealed class WeChatRewardedAds : MonoBehaviour
    {
        private string hintId, skipId;
        private bool configured, disposed;
        private IRewardedVideoProvider hints, endlessSkip;

        public IRewardedVideoProvider Hints => hints ?? (hints=new Placement(this,true));
        public IRewardedVideoProvider EndlessSkip => endlessSkip ?? (endlessSkip=new Placement(this,false));

        /// <summary>Call after WeChatPlatform initialization. IDs are public ad unit IDs.</summary>
        public void Configure(string hintId,string skipId)
        {
            configured=false;
            AbortPending(RewardedVideoResult.Unavailable);
            this.hintId=NormalizeId(hintId);this.skipId=NormalizeId(skipId);
            configured=!disposed;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            ScheduleWarm(hintSlot,InitialWarmDelaySeconds,true);
            ScheduleWarm(skipSlot,InitialWarmDelaySeconds,true);
#endif
        }

        private static string NormalizeId(string value)
        {
            value=value?.Trim();
            return !string.IsNullOrEmpty(value)&&value.StartsWith("adunit-",StringComparison.Ordinal)
                &&value.Length>7?value:null;
        }

        private bool IsAvailable(bool hint)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            return !disposed&&isActiveAndEnabled&&configured&&WeChatPlatform.Ready
                &&!string.IsNullOrEmpty(hint?hintId:skipId)&&globalRequest==null;
#else
            return false;
#endif
        }

        private sealed class Placement : ICancellableRewardedVideoProvider
        {
            private readonly WeChatRewardedAds owner;
            private readonly bool hint;
            public Placement(WeChatRewardedAds owner,bool hint){this.owner=owner;this.hint=hint;}
            public bool IsAvailable => owner.IsAvailable(hint);
            public bool CanCancel => owner.CanCancel(hint);
            public void Show(Action<RewardedVideoResult> completed){owner.Show(hint,completed);}
            public void Cancel(){owner.Cancel(hint);}
        }

        private void Show(bool hint,Action<RewardedVideoResult> completed)
        {
            if(completed==null)return;
            if(!IsAvailable(hint)){Deliver(completed,RewardedVideoResult.Unavailable);return;}
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var slot=hint?hintSlot:skipSlot;
            var now=Time.realtimeSinceStartupAsDouble;
            var request=slot.cached;
            slot.cached=null;slot.warmScheduled=false;slot.warmFailures=0;
            if(request!=null&&((request.stage==Stage.Ready&&now-request.readyAt>=CacheLifetimeSeconds)
                ||(request.stage==Stage.Loading&&now>=request.deadline)))
            {
                Trace(slot.name+" cache-expired");
                Release(request);request=null;
            }
            var reused=request!=null;
            if(request==null)request=NewRequest(slot,now);
            request.completed=completed;request.clickedAt=now;
            currentRequest=request;globalRequest=request;
            if(reused)
            {
                Trace(slot.name+(request.stage==Stage.Ready?" cache-hit":" cache-loading"));
                if(request.stage==Stage.Ready)Open(request);
                // A pending warm Load is already subscribed and retains its original deadline.
                return;
            }
            Trace(slot.name+" cache-miss");
            CreateAndLoad(request);
#endif
        }

#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        private void CreateAndLoad(Request request)
        {
            try
            {
                // Never recycle a consumed instance. The SDK's default singleton must not
                // be destroyed, so both warm caches and cold requests use fresh multitons.
                request.ad=WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam{
                    adUnitId=request.slot.hint?hintId:skipId,multiton=true
                });
                if(!IsOwned(request)){Release(request);return;}
                if(request.ad==null){Fail(request,RewardedVideoResult.Unavailable,"create-null");return;}
                request.error=_=>Fail(request,RewardedVideoResult.Failed,"native-error");
                request.close=res=>{
                    if(!IsCurrent(request)||(request.stage!=Stage.Opening&&request.stage!=Stage.Watching))return;
                    Finish(request,!request.cancelled&&res!=null&&res.isEnded?RewardedVideoResult.Completed:RewardedVideoResult.Cancelled,"close");
                };
                request.ad.OnError(request.error);
                if(!IsOwned(request))return;
                request.ad.OnClose(request.close);
                if(!IsOwned(request))return;
                request.ad.Load(_=>OnLoaded(request),_=>Fail(request,RewardedVideoResult.Failed,"load-failed"));
            }
            catch(Exception ex)
            {
                if(IsCurrent(request))Debug.LogWarning("PORTCARGO_AD "+request.slot.name+" create/load exception: "+ex.Message);
                Fail(request,RewardedVideoResult.Failed,"create-load-exception");
            }
        }
#endif

        private bool CanCancel(bool hint)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            return !IsCurrent(currentRequest)||currentRequest.slot.hint!=hint
                ||currentRequest.stage==Stage.Loading;
#else
            return true;
#endif
        }

        private void Cancel(bool hint)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var request=currentRequest;
            if(!IsCurrent(request)||request.slot.hint!=hint)return;
            if(request.stage==Stage.Loading)
            {
                // A later load callback must never open an ad after the player went back.
                Finish(request,RewardedVideoResult.Cancelled,"cancel-loading");
                return;
            }
            // Once Show crossed the native boundary there is no supported hide/cancel API.
            // Complete the game callback now, but retain exclusivity until close/timeout.
            request.cancelled=true;
            var completed=request.completed;request.completed=null;
            Deliver(completed,RewardedVideoResult.Cancelled);
#endif
        }

#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        private const double LoadTimeoutSeconds=20;
        private const double ShowTimeoutSeconds=20;
        private const double WatchTimeoutSeconds=180;
        private const double InitialWarmDelaySeconds=1;
        private const double WarmRetryDelaySeconds=30;
        private const double CacheLifetimeSeconds=300;
        private enum Stage { Loading, Ready, Opening, Watching }
        private sealed class Slot
        {
            public readonly bool hint;
            public readonly string name;
            public Request cached;
            public bool warmScheduled;
            public int warmFailures;
            public double warmAfter;
            public long warmOnUpdate;
            public Slot(bool hint){this.hint=hint;name=hint?"hint":"endless-skip";}
        }
        private sealed class Request
        {
            public Slot slot;
            public Action<RewardedVideoResult> completed;
            public WXRewardedVideoAd ad;
            public Action<WXADErrorResponse> error;
            public Action<WXRewardedVideoAdOnCloseResponse> close;
            public bool cancelled,released;
            public Stage stage;
            public double deadline,readyAt,clickedAt;
        }
        // Shared by both placements and all adapter components in the process.
        private static Request globalRequest;
        private Request currentRequest;
        private readonly Slot hintSlot=new Slot(true),skipSlot=new Slot(false);
        private long updateNumber;

        private static Request NewRequest(Slot slot,double now)
        {return new Request{slot=slot,stage=Stage.Loading,deadline=now+LoadTimeoutSeconds};}

        private bool IsCurrent(Request request)
        {return request!=null&&!request.released&&ReferenceEquals(currentRequest,request)&&ReferenceEquals(globalRequest,request);}

        private static bool IsCached(Request request)
        {return request!=null&&!request.released&&ReferenceEquals(request.slot.cached,request);}

        private bool IsOwned(Request request){return IsCurrent(request)||IsCached(request);}

        private void OnLoaded(Request request)
        {
            if(!IsOwned(request)||request.stage!=Stage.Loading)return;
            if(IsCurrent(request)){Open(request);return;}
            request.stage=Stage.Ready;request.readyAt=Time.realtimeSinceStartupAsDouble;
            Trace(request.slot.name+" warm-ready");
        }

        private void Open(Request request)
        {
            if(!IsCurrent(request)||(request.stage!=Stage.Loading&&request.stage!=Stage.Ready))return;
            request.stage=Stage.Opening;
            request.deadline=Time.realtimeSinceStartupAsDouble+ShowTimeoutSeconds;
            Trace(request.slot.name+" show-start click-seconds="+(Time.realtimeSinceStartupAsDouble-request.clickedAt).ToString("F2"));
            try
            {
                request.ad.Show(_=>{
                    if(!IsCurrent(request)||request.stage!=Stage.Opening)return;
                    request.stage=Stage.Watching;
                    request.deadline=Time.realtimeSinceStartupAsDouble+WatchTimeoutSeconds;
                    Trace(request.slot.name+" shown click-seconds="+(Time.realtimeSinceStartupAsDouble-request.clickedAt).ToString("F2"));
                },_=>Finish(request,RewardedVideoResult.Failed,"show-failed"));
            }
            catch(Exception ex)
            {
                Debug.LogWarning("PORTCARGO_AD "+request.slot.name+" show exception: "+ex.Message);
                Finish(request,RewardedVideoResult.Failed,"show-exception");
            }
        }

        private void Update()
        {
            updateNumber++;
            var request=currentRequest;
            if(IsCurrent(request)&&Time.realtimeSinceStartupAsDouble>=request.deadline)
                Finish(request,request.cancelled?RewardedVideoResult.Cancelled:RewardedVideoResult.Failed,"timeout-"+request.stage);
            TickWarm(hintSlot);TickWarm(skipSlot);
        }

        private void TickWarm(Slot slot)
        {
            if(disposed||!configured||!isActiveAndEnabled||!WeChatPlatform.Ready
                ||string.IsNullOrEmpty(slot.hint?hintId:skipId))return;
            var now=Time.realtimeSinceStartupAsDouble;
            var cached=slot.cached;
            if(cached!=null&&cached.stage==Stage.Loading&&now>=cached.deadline)
                Fail(cached,RewardedVideoResult.Failed,"warm-timeout");
            if(slot.cached!=null||!slot.warmScheduled||now<slot.warmAfter||updateNumber<slot.warmOnUpdate
                ||(IsCurrent(currentRequest)&&ReferenceEquals(currentRequest.slot,slot)))return;
            slot.warmScheduled=false;
            var request=NewRequest(slot,now);
            slot.cached=request;
            Trace(slot.name+" warm-start");
            CreateAndLoad(request);
        }

        private void ScheduleWarm(Slot slot,double delay,bool resetFailures=false)
        {
            if(resetFailures)slot.warmFailures=0;
            slot.warmScheduled=configured&&!disposed;
            slot.warmAfter=Time.realtimeSinceStartupAsDouble+delay;
            // Completion inside Update must not refill until the following frame.
            slot.warmOnUpdate=updateNumber+1;
        }

        private void Fail(Request request,RewardedVideoResult result,string reason)
        {
            if(IsCurrent(request)){Finish(request,result,reason);return;}
            if(!IsCached(request))return;
            var slot=request.slot;
            slot.cached=null;
            slot.warmFailures++;
            slot.warmScheduled=false;
            if(slot.warmFailures<2)ScheduleWarm(slot,WarmRetryDelaySeconds);
            Release(request);
            // Warm failures have no gameplay callback or player-facing error.
            Trace(slot.name+" warm-failed "+reason+" retry="+(slot.warmScheduled?"scheduled":"stopped"));
        }

        private void Finish(Request request,RewardedVideoResult result,string reason,bool replenish=true)
        {
            if(!IsCurrent(request))return;
            // Invalidate before native cleanup: Destroy/Off* or game callbacks may reenter.
            currentRequest=null;globalRequest=null;
            var completed=request.completed;request.completed=null;
            Release(request);
            if(replenish)ScheduleWarm(request.slot,0);
            if(result==RewardedVideoResult.Failed)
                Debug.LogWarning("PORTCARGO_AD "+request.slot.name+" "+reason);
            Trace(request.slot.name+" finished "+reason);
            Deliver(completed,result);
        }

        private static void Release(Request request)
        {
            request.released=true;
            var ad=request.ad;request.ad=null;
            if(ad!=null)
            {
                try{if(request.error!=null)ad.OffError(request.error);}
                catch(Exception ex){Debug.LogWarning("PORTCARGO_AD unsubscribe error: "+ex.Message);}
                try{if(request.close!=null)ad.OffClose(request.close);}
                catch(Exception ex){Debug.LogWarning("PORTCARGO_AD unsubscribe close: "+ex.Message);}
                try{ad.Destroy();}
                catch(Exception ex){Debug.LogWarning("PORTCARGO_AD destroy: "+ex.Message);}
            }
        }

        private void ClearPreloads()
        {
            var hint=hintSlot.cached;var skip=skipSlot.cached;
            hintSlot.cached=null;skipSlot.cached=null;
            hintSlot.warmScheduled=false;skipSlot.warmScheduled=false;
            if(hint!=null)Release(hint);
            if(skip!=null)Release(skip);
        }

        private static void Trace(string message)
        {
#if DEVELOPMENT_BUILD
            Debug.Log("PORTCARGO_AD "+message);
#endif
        }
#endif

        private void AbortPending(RewardedVideoResult result)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            ClearPreloads();
            if(currentRequest!=null)Finish(currentRequest,result,"owner-stopped",false);
#endif
        }
        private void OnEnable()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(!configured)return;
            ScheduleWarm(hintSlot,InitialWarmDelaySeconds,true);
            ScheduleWarm(skipSlot,InitialWarmDelaySeconds,true);
#endif
        }
        private void OnDisable(){AbortPending(RewardedVideoResult.Cancelled);}
        private void OnDestroy()
        {disposed=true;configured=false;AbortPending(RewardedVideoResult.Cancelled);}
        private static void Deliver(Action<RewardedVideoResult> callback,RewardedVideoResult result)
        {
            if(callback==null)return;
            try{callback(result);}catch(Exception ex){Debug.LogException(ex);}
        }
    }
}
