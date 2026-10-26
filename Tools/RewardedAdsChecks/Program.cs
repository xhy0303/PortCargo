using System;
using System.Collections.Generic;
using System.Reflection;
using PortCargo.Core;
using PortCargo.Platform;
using UnityEngine;
using WeChatWASM;

static class Program
{
    static int checks;
    static void Check(bool value,string name)
    {checks++;if(!value)throw new Exception("CHECK FAILED: "+name);}
    static void Invoke(WeChatRewardedAds ads,string name)
    {typeof(WeChatRewardedAds).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(ads,null);}
    static WeChatRewardedAds Owner()
    {
        var ads=new WeChatRewardedAds();ads.Configure("adunit-hint","adunit-skip");return ads;
    }
    static void Main()
    {
        WeChatPlatform.Ready=true;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        Run("original availability/concurrency",AvailabilityAndConcurrency);
        Run("original completion proof",CompletionProof);
        Run("original failure paths",FailurePaths);
        Run("original timeouts",Timeouts);
        Run("original lifetime",Lifetime);
        Run("original cancellation",Cancellation);
        Run("original reentrancy",Reentrancy);
        Check(checks==77,"original 77 contract assertions retained");
        Run("delayed preload and warm click",DelayedPreloadAndWarmClick);
        Run("preload takeover and cancellation",LoadingTakeover);
        Run("preload availability and global lock",PreloadAvailability);
        Run("silent retry backoff",PreloadBackoff);
        Run("cached expiry and refill",ExpiryAndRefill);
        Run("preloaded error and takeover deadlines",CachedErrorsAndDeadlines);
        Run("cached lifetime and stale events",CachedLifetime);
        Run("warm request reentrancy",WarmReentrancy);
        Console.WriteLine("REWARDED_ADAPTER_PLAYER_CONTRACT_PASS checks="+checks);
#else
        var owner=Owner();var outcomes=new List<RewardedVideoResult>();
        Check(!owner.Hints.IsAvailable&&!owner.EndlessSkip.IsAvailable,"desktop/editor unavailable");
        owner.Hints.Show(outcomes.Add);owner.EndlessSkip.Show(outcomes.Add);
        Check(outcomes.Count==2&&outcomes.TrueForAll(x=>x==RewardedVideoResult.Unavailable),"never synthetic desktop success");
        Check(WX.Created.Count==0,"desktop never calls SDK");
        Invoke(owner,"OnDestroy");Console.WriteLine("REWARDED_ADAPTER_NONPLAYER_PASS checks="+checks);
#endif
    }

#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
    const double WarmDelay=1,RetryDelay=30,CacheLifetime=300;
    static void Run(string name,Action test)
    {
        var before=checks;test();Console.WriteLine("PASS "+name+" checks="+(checks-before));
    }
    static void Tick(WeChatRewardedAds owner,double time)
    {Time.realtimeSinceStartupAsDouble=time;Invoke(owner,"Update");}
    static WXRewardedVideoAd Latest(string id,int from=0)
    {
        for(var i=WX.Created.Count-1;i>=from;i--)if(WX.Created[i].id==id)return WX.Created[i];
        throw new Exception("Missing native ad instance "+id+" from index "+from);
    }
    static int Count(string id,int from=0)
    {var count=0;for(var i=from;i<WX.Created.Count;i++)if(WX.Created[i].id==id)count++;return count;}
    static void Ready(WXRewardedVideoAd ad)=>ad.loadOk(new WXTextResponse());
    static WXRewardedVideoAd Last => WX.Created[WX.Created.Count-1];
    static void LoadAndShow(WXRewardedVideoAd ad)
    {ad.loadOk(new WXTextResponse());ad.showOk(new WXTextResponse());}
    static void Close(WXRewardedVideoAd ad,bool ended)
    {ad.close(new WXRewardedVideoAdOnCloseResponse{isEnded=ended});}
    static void AvailabilityAndConcurrency()
    {
        var owner=new WeChatRewardedAds();var outcomes=new List<RewardedVideoResult>();
        Check(!owner.Hints.IsAvailable,"not configured");owner.Hints.Show(outcomes.Add);
        Check(outcomes[0]==RewardedVideoResult.Unavailable,"unconfigured returns unavailable");
        owner.Configure("bad", " adunit-skip ");
        Check(!owner.Hints.IsAvailable&&owner.EndlessSkip.IsAvailable,"placement-specific ID validation");
        WeChatPlatform.Ready=false;Check(!owner.EndlessSkip.IsAvailable,"SDK readiness required");
        WeChatPlatform.Ready=true;owner.Configure("adunit-hint","adunit-skip");
        var other=Owner();outcomes.Clear();owner.Hints.Show(outcomes.Add);var ad=Last;
        Check(ad.id=="adunit-hint"&&ad.multiton,"correct placement and multiton");
        Check(!owner.Hints.IsAvailable&&!owner.EndlessSkip.IsAvailable&&!other.Hints.IsAvailable,"global in-flight exclusion");
        var denied=new List<RewardedVideoResult>();other.EndlessSkip.Show(denied.Add);
        Check(denied.Count==1&&denied[0]==RewardedVideoResult.Unavailable,"concurrent callback once");
        LoadAndShow(ad);Close(ad,false);
        Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Cancelled,"early close");
        Check(owner.Hints.IsAvailable&&other.EndlessSkip.IsAvailable,"global slot released");
        owner.EndlessSkip.Show(outcomes.Add);ad=Last;Check(ad.id=="adunit-skip","skip uses its ID");
        ad.loadFailed(new WXADErrorResponse());Invoke(owner,"OnDestroy");Invoke(other,"OnDestroy");
    }
    static void CompletionProof()
    {
        var owner=Owner();var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);
        var ad=Last;var oldClose=ad.close;var oldError=ad.error;
        oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        Check(outcomes.Count==0,"close during loading cannot reward");
        LoadAndShow(ad);Check(outcomes.Count==0,"load/show success never reward");
        Close(ad,true);oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        oldError(new WXADErrorResponse());ad.loadFailed(new WXADErrorResponse());ad.showFailed(new WXTextResponse());
        Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Completed,"real complete once despite duplicate callbacks");
        Check(ad.destroyed&&ad.destroyCount==1&&ad.offCloseCount==1&&ad.offErrorCount==1,"all event subscriptions cleaned");
        owner.Hints.Show(outcomes.Add);var next=Last;
        oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        Check(outcomes.Count==1&&!owner.Hints.IsAvailable,"old callback cannot touch next request");
        next.loadOk(new WXTextResponse());next.close(null);
        Check(outcomes.Count==2&&outcomes[1]==RewardedVideoResult.Cancelled,"missing completion proof cancels");
        owner.Hints.Show(outcomes.Add);ad=Last;ad.loadOk(new WXTextResponse());Close(ad,true);
        ad.showOk(new WXTextResponse());
        Check(outcomes.Count==3&&outcomes[2]==RewardedVideoResult.Completed,"close can precede show promise resolution");
        Invoke(owner,"OnDestroy");
    }
    static void FailurePaths()
    {
        var owner=Owner();
        Action<Action<WXRewardedVideoAd>> run=signal=>{
            var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);var ad=Last;
            var close=ad.close;signal(ad);close(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
            Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Failed,"failure cannot later complete");
            Check(owner.Hints.IsAvailable,"failed request recovers");
        };
        run(ad=>ad.loadFailed(new WXADErrorResponse()));
        run(ad=>ad.error(new WXADErrorResponse()));
        run(ad=>{ad.loadOk(new WXTextResponse());ad.showFailed(new WXTextResponse());});
        foreach(var kind in new[]{"create","load","show","null"})
        {
            WX.ThrowCreate=kind=="create";WX.ThrowLoad=kind=="load";WX.ThrowShow=kind=="show";WX.NullCreate=kind=="null";
            var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);
            if(kind=="show")Last.loadOk(new WXTextResponse());
            Check(outcomes.Count==1&&outcomes[0]==(kind=="null"?RewardedVideoResult.Unavailable:RewardedVideoResult.Failed),"synchronous "+kind+" failure");
            Check(owner.Hints.IsAvailable,"exception releases global request");
            WX.ThrowCreate=WX.ThrowLoad=WX.ThrowShow=WX.NullCreate=false;
        }
        WX.ThrowCleanup=true;var cleanup=new List<RewardedVideoResult>();owner.Hints.Show(cleanup.Add);
        var last=Last;LoadAndShow(last);Close(last,true);
        Check(cleanup.Count==1&&cleanup[0]==RewardedVideoResult.Completed&&last.offCloseCount==1&&last.destroyCount==1,"cleanup exceptions do not swallow result or later cleanup");
        WX.ThrowCleanup=false;Invoke(owner,"OnDestroy");
    }
    static void Timeouts()
    {
        foreach(var phase in new[]{"loading","opening","watching"})
        {
            Time.realtimeSinceStartupAsDouble=100;var owner=Owner();var outcomes=new List<RewardedVideoResult>();
            owner.Hints.Show(outcomes.Add);var ad=Last;var close=ad.close;
            if(phase!="loading")ad.loadOk(new WXTextResponse());
            if(phase=="watching")ad.showOk(new WXTextResponse());
            var limit=phase=="watching"?180:20;
            Time.realtimeSinceStartupAsDouble=100+limit-.01;Invoke(owner,"Update");
            Check(outcomes.Count==0,phase+" not timed out early");
            Time.realtimeSinceStartupAsDouble=100+limit;Invoke(owner,"Update");Invoke(owner,"Update");
            close(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
            Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Failed,phase+" timeout fails exactly once");
            Check(ad.destroyed&&owner.Hints.IsAvailable,phase+" timeout releases native object and lock");
            Invoke(owner,"OnDestroy");
        }
    }
    static void Lifetime()
    {
        foreach(var action in new[]{"disable","destroy","configure"})
        {
            var owner=Owner();var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);
            var ad=Last;var close=ad.close;
            if(action=="configure")owner.Configure("adunit-newhint","adunit-newskip");
            else if(action=="disable"){owner.isActiveAndEnabled=false;Invoke(owner,"OnDisable");}
            else Invoke(owner,"OnDestroy");
            ad.loadOk(new WXTextResponse());close(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
            Check(outcomes.Count==1&&outcomes[0]==(action=="configure"?RewardedVideoResult.Unavailable:RewardedVideoResult.Cancelled),action+" rejects stale result");
            Check(ad.showCount==0&&ad.destroyed,action+" rejects late load and destroys ad");
            if(action=="configure")
            {
                owner.Hints.Show(outcomes.Add);Check(Last.id=="adunit-newhint","new configuration applied");
                Last.loadFailed(new WXADErrorResponse());
            }
            else Check(!owner.Hints.IsAvailable,action+" unavailable");
            Invoke(owner,"OnDestroy");
        }
    }
    static void Cancellation()
    {
        foreach(var phase in new[]{"loading","opening","watching"})
        {
            Time.realtimeSinceStartupAsDouble=100;var owner=Owner();var outcomes=new List<RewardedVideoResult>();
            owner.Hints.Show(outcomes.Add);var ad=Last;var close=ad.close;
            if(phase!="loading")ad.loadOk(new WXTextResponse());
            if(phase=="watching")ad.showOk(new WXTextResponse());
            Check(((ICancellableRewardedVideoProvider)owner.Hints).CanCancel==(phase=="loading"),phase+" player cancellation availability");
            Check(((ICancellableRewardedVideoProvider)owner.EndlessSkip).CanCancel,"idle placement cancellation availability");
            ((ICancellableRewardedVideoProvider)owner.EndlessSkip).Cancel();
            Check(outcomes.Count==0,"other placement cancellation cannot touch request");
            ((ICancellableRewardedVideoProvider)owner.Hints).Cancel();
            ((ICancellableRewardedVideoProvider)owner.Hints).Cancel();
            Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Cancelled,phase+" cancellation callback once");
            if(phase=="loading")
            {
                ad.loadOk(new WXTextResponse());close(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
                Check(ad.destroyed&&ad.showCount==0&&owner.Hints.IsAvailable,"loading cancelled then late load never shows ad");
            }
            else
            {
                Check(!owner.Hints.IsAvailable&&!ad.destroyed,phase+" native request retains exclusivity");
                ad.showOk(new WXTextResponse());close(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
                Check(ad.destroyed&&owner.Hints.IsAvailable,phase+" native close releases cancelled request");
            }
            Check(outcomes.Count==1,phase+" completed native close cannot reward cancelled request");
            Check(((ICancellableRewardedVideoProvider)owner.Hints).CanCancel,"resolved request permits cancellation");
            Invoke(owner,"OnDestroy");
        }
        Time.realtimeSinceStartupAsDouble=100;
        var timed=Owner();var timedResults=new List<RewardedVideoResult>();timed.Hints.Show(timedResults.Add);
        var pending=Last;pending.loadOk(new WXTextResponse());
        ((ICancellableRewardedVideoProvider)timed.Hints).Cancel();
        Time.realtimeSinceStartupAsDouble=120;Invoke(timed,"Update");
        Check(timedResults.Count==1&&pending.destroyed&&timed.Hints.IsAvailable,"cancelled native request also recovers by timeout");
        Invoke(timed,"OnDestroy");
    }
    static void Reentrancy()
    {
        var owner=Owner();var outcomes=new List<RewardedVideoResult>();
        owner.Hints.Show(result=>{outcomes.Add(result);owner.EndlessSkip.Show(outcomes.Add);});
        var first=Last;LoadAndShow(first);Close(first,true);var second=Last;
        Check(!ReferenceEquals(first,second)&&first.destroyed&&second.id=="adunit-skip","callback may start next request after cleanup");
        LoadAndShow(second);Close(second,true);
        Check(outcomes.Count==2&&outcomes.TrueForAll(x=>x==RewardedVideoResult.Completed),"reentrant request resolves independently");
        owner.Hints.Show(_=>throw new Exception("game callback"));var ad=Last;LoadAndShow(ad);Close(ad,true);
        Check(owner.Hints.IsAvailable,"throwing consumer does not retain lock");
        Invoke(owner,"OnDestroy");
    }

    static void DelayedPreloadAndWarmClick()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();
        Check(WX.Created.Count==start,"Configure leaves initial frame free of SDK creation");
        Tick(owner,WarmDelay-.001);Check(WX.Created.Count==start,"no warm before startup delay");
        Tick(owner,WarmDelay);var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);
        Check(WX.Created.Count==start+2&&hint!=skip&&hint.multiton&&skip.multiton,"delayed warm creates two independent multitons");
        Check(hint.loadCount==1&&skip.loadCount==1&&hint.showCount==0&&skip.showCount==0,"background warm loads each placement once without showing");
        Tick(owner,WarmDelay+.1);Check(WX.Created.Count==start+2&&hint.loadCount==1&&skip.loadCount==1,"Update does not duplicate in-flight warm loads");
        var earlyClose=hint.close;earlyClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        Check(hint.showCount==0&&owner.Hints.IsAvailable,"idle close never automatically shows or claims global lock");
        Ready(hint);Ready(skip);var created=WX.Created.Count;var outcomes=new List<RewardedVideoResult>();
        owner.Hints.Show(outcomes.Add);
        Check(hint.showCount==1&&hint.loadCount==1&&WX.Created.Count==created,"warm hint click immediately shows same native object with no create/load");
        Check(outcomes.Count==0,"warm load/show invocation is not proof of a reward");
        hint.showOk(new WXTextResponse());Close(hint,true);
        Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Completed&&hint.destroyed,"warm hint completion rewards once and destroys consumed instance");
        Check(!skip.destroyed&&skip.loadCount==1,"hint completion preserves preloaded skip instance");
        owner.EndlessSkip.Show(outcomes.Add);
        Check(skip.showCount==1&&skip.loadCount==1&&WX.Created.Count==created,"warm skip click uses its own ready object without create/load");
        Close(skip,false);Check(outcomes.Count==2&&outcomes[1]==RewardedVideoResult.Cancelled,"warm skip early close cancels");
        Invoke(owner,"OnDestroy");
    }

    static void LoadingTakeover()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
        var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);var created=WX.Created.Count;
        var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);
        Check(WX.Created.Count==created&&hint.loadCount==1&&hint.showCount==0,"cold click claims existing loading cache without another create/load");
        Check(!owner.Hints.IsAvailable&&!owner.EndlessSkip.IsAvailable,"claimed loading cache occupies request lock");
        Check(((ICancellableRewardedVideoProvider)owner.Hints).CanCancel,"claimed background load is cancellable");
        var oldLoad=hint.loadOk;var oldClose=hint.close;
        ((ICancellableRewardedVideoProvider)owner.Hints).Cancel();
        oldLoad(new WXTextResponse());oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        Check(outcomes.Count==1&&outcomes[0]==RewardedVideoResult.Cancelled&&hint.showCount==0&&hint.destroyed,"cancelled loading takeover never opens on late cache completion");
        Check(!skip.destroyed&&skip.loadCount==1,"cancelling one loading placement preserves the other preload");
        Tick(owner,WarmDelay+.1);var next=Latest("adunit-hint",start);
        Check(next!=hint&&next.loadCount==1,"cancelled loading slot refills on a later Update");
        owner.Hints.Show(outcomes.Add);Ready(next);
        Check(next.showCount==1&&outcomes.Count==1,"ready callback of claimed replacement triggers exactly one native show");
        Ready(next);Check(next.showCount==1,"duplicate load success cannot call Show twice");
        Close(next,true);Check(outcomes.Count==2&&outcomes[1]==RewardedVideoResult.Completed,"replacement takeover completes normally");
        Invoke(owner,"OnDestroy");
    }

    static void PreloadAvailability()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();var other=Owner();
        Tick(owner,WarmDelay);var firstHint=Latest("adunit-hint",start);var firstSkip=Latest("adunit-skip",start);Tick(other,WarmDelay);
        Check(WX.Created.Count==start+4&&owner.Hints.IsAvailable&&owner.EndlessSkip.IsAvailable&&other.Hints.IsAvailable,
            "two owners can preload both placements without consuming global request lock");
        Check(firstHint!=firstSkip&&firstHint.id=="adunit-hint"&&firstSkip.id=="adunit-skip","placement caches preserve separate native identity");
        Ready(firstHint);Ready(firstSkip);var outcomes=new List<RewardedVideoResult>();owner.Hints.Show(outcomes.Add);
        var denied=new List<RewardedVideoResult>();other.EndlessSkip.Show(denied.Add);
        Check(denied.Count==1&&denied[0]==RewardedVideoResult.Unavailable,"ready/loading caches cannot bypass cross-owner display exclusion");
        Tick(other,WarmDelay+.2);
        Check(!firstHint.destroyed&&firstHint.showCount==1&&WX.Created[start+3].showCount==0,"background cache maintenance never starts a concurrent native ad");
        Close(firstHint,true);Check(other.EndlessSkip.IsAvailable,"active completion releases lock for another owner cache");
        Invoke(owner,"OnDestroy");Invoke(other,"OnDestroy");

        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;WeChatPlatform.Ready=false;owner=Owner();
        Tick(owner,10);Check(WX.Created.Count==start&&!owner.Hints.IsAvailable,"SDK-not-ready never preloads");
        WeChatPlatform.Ready=true;Tick(owner,11);
        Check(WX.Created.Count==start+2&&owner.Hints.IsAvailable,"SDK readiness recovery permits delayed background preparation");
        Invoke(owner,"OnDestroy");
        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;owner=new WeChatRewardedAds();owner.Configure("invalid","adunit-skip");Tick(owner,WarmDelay);
        Check(WX.Created.Count==start+1&&Latest("adunit-skip",start).loadCount==1,"invalid placement ID does not prevent valid placement warm");
        Invoke(owner,"OnDestroy");
    }

    static void PreloadBackoff()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
        var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);Ready(skip);
        var staleLoad=hint.loadOk;hint.loadFailed(new WXADErrorResponse());
        Check(hint.destroyed&&!skip.destroyed&&owner.Hints.IsAvailable,"background failure retires only its cache and leaves manual request available");
        Tick(owner,WarmDelay+RetryDelay-.001);
        Check(Count("adunit-hint",start)==1,"background failure respects full retry backoff");
        Tick(owner,WarmDelay+RetryDelay);var retry=Latest("adunit-hint",start);
        Check(retry!=hint&&Count("adunit-hint",start)==2&&retry.loadCount==1,"background failure gets one delayed automatic retry");
        staleLoad(new WXTextResponse());Check(hint.showCount==0&&retry.showCount==0,"obsolete preload completion cannot show or replace retry");
        retry.loadFailed(new WXADErrorResponse());Tick(owner,1000);Tick(owner,2000);
        Check(Count("adunit-hint",start)==2,"second warm failure stops automatic retry rather than polling forever");
        var results=new List<RewardedVideoResult>();owner.Hints.Show(results.Add);var manual=Latest("adunit-hint",start);
        Check(manual!=retry&&manual.loadCount==1&&results.Count==0,"explicit player request can retry an exhausted warm slot");
        LoadAndShow(manual);Close(manual,true);
        Check(results.Count==1&&results[0]==RewardedVideoResult.Completed,"manual retry after offline backoff can still complete");
        Invoke(owner,"OnDestroy");

        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;owner=Owner();Tick(owner,WarmDelay);hint=Latest("adunit-hint",start);
        Tick(owner,WarmDelay+20-.001);Check(!hint.destroyed,"background load timeout not early");
        Tick(owner,WarmDelay+20);Check(hint.destroyed&&hint.showCount==0&&owner.Hints.IsAvailable,"background load timeout retires cache without showing or consuming request lock");
        Tick(owner,WarmDelay+20+RetryDelay-.001);Check(Count("adunit-hint",start)==1,"timed-out preload waits before retry");
        Tick(owner,WarmDelay+20+RetryDelay);Check(Count("adunit-hint",start)==2,"timed-out preload retries once at backoff boundary");
        Invoke(owner,"OnDestroy");

        foreach(var fault in new[]{"create","null","load"})
        {
            Time.realtimeSinceStartupAsDouble=0;owner=Owner();var attempts=WX.CreateAttemptCount;
            WX.ThrowCreate=fault=="create";WX.NullCreate=fault=="null";WX.ThrowLoad=fault=="load";
            Tick(owner,WarmDelay);var after=WX.CreateAttemptCount;
            Check(after==attempts+2&&owner.Hints.IsAvailable,fault+" warm failure is contained for each placement");
            Tick(owner,WarmDelay+.1);Tick(owner,WarmDelay+.2);
            Check(WX.CreateAttemptCount==after,fault+" synchronous failure does not spin on every Update");
            WX.ThrowCreate=WX.NullCreate=WX.ThrowLoad=false;Invoke(owner,"OnDestroy");
        }
    }

    static void ExpiryAndRefill()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
        var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);Ready(hint);Ready(skip);
        var results=new List<RewardedVideoResult>();var before=WX.Created.Count;
        Time.realtimeSinceStartupAsDouble=WarmDelay+CacheLifetime;owner.Hints.Show(results.Add);var replacement=Latest("adunit-hint",start);
        Check(hint.destroyed&&hint.showCount==0&&replacement!=hint&&replacement.loadCount==1,"cache expiry at five minutes replaces stale object before displaying");
        Check(WX.Created.Count==before+1&&replacement.showCount==0,"expired click waits for fresh load without duplicate create");
        LoadAndShow(replacement);before=WX.Created.Count;Close(replacement,true);
        Check(results.Count==1&&results[0]==RewardedVideoResult.Completed,"expired-cache reload completes normally");
        Check(WX.Created.Count==before,"consumed request does not synchronously create refill inside close callback");
        Tick(owner,WarmDelay+CacheLifetime+.001);var refill=Latest("adunit-hint",start);
        Check(refill!=replacement&&refill.loadCount==1&&refill.showCount==0,"next Update refills consumed placement in background");
        Ready(refill);before=WX.Created.Count;owner.Hints.Show(results.Add);
        Check(refill.showCount==1&&refill.loadCount==1&&WX.Created.Count==before,"subsequent repeated reward can use new warmed instance immediately");
        Close(refill,true);Invoke(owner,"OnDestroy");

        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;owner=Owner();Tick(owner,WarmDelay);hint=Latest("adunit-hint",start);Ready(hint);
        Time.realtimeSinceStartupAsDouble=WarmDelay+CacheLifetime-.001;owner.Hints.Show(results.Add);
        Check(!hint.destroyed&&hint.showCount==1,"cache just before five-minute boundary remains reusable");
        Close(hint,false);Invoke(owner,"OnDestroy");
    }

    static void CachedErrorsAndDeadlines()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
        var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);Ready(hint);Ready(skip);
        var oldClose=hint.close;hint.error(new WXADErrorResponse());
        Check(hint.destroyed&&!skip.destroyed&&owner.Hints.IsAvailable,"native error on unused ready cache retires only that placement");
        var results=new List<RewardedVideoResult>();owner.Hints.Show(results.Add);var next=Latest("adunit-hint",start);
        oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});
        Check(next!=hint&&next.loadCount==1&&results.Count==0,"failed ready cache cannot later reward a replacement request");
        LoadAndShow(next);Close(next,true);owner.EndlessSkip.Show(results.Add);skip.showFailed(new WXTextResponse());
        Check(results.Count==2&&results[1]==RewardedVideoResult.Failed&&skip.destroyed&&owner.Hints.IsAvailable,
            "show failure on cached object fails once and releases global lock");
        Invoke(owner,"OnDestroy");

        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;owner=Owner();Tick(owner,WarmDelay);hint=Latest("adunit-hint",start);
        Time.realtimeSinceStartupAsDouble=WarmDelay+10;results.Clear();owner.Hints.Show(results.Add);
        Tick(owner,WarmDelay+20-.001);Check(results.Count==0&&hint.loadCount==1,"loading takeover reuses load before original deadline");
        Tick(owner,WarmDelay+20);Check(results.Count==1&&results[0]==RewardedVideoResult.Failed&&hint.destroyed,
            "loading takeover inherits original timeout rather than extending it");
        var created=WX.Created.Count;Tick(owner,WarmDelay+20+.001);
        Check(WX.Created.Count>created&&Latest("adunit-hint",start)!=hint,"Update timeout defers refill until a subsequent Update");
        Invoke(owner,"OnDestroy");

        Time.realtimeSinceStartupAsDouble=0;start=WX.Created.Count;owner=Owner();Tick(owner,WarmDelay);hint=Latest("adunit-hint",start);
        Time.realtimeSinceStartupAsDouble=WarmDelay+20;results.Clear();owner.Hints.Show(results.Add);next=Latest("adunit-hint",start);
        Check(hint.destroyed&&next!=hint&&next.loadCount==1&&results.Count==0,"click after expired loading deadline replaces stale pending cache before attachment");
        LoadAndShow(next);Close(next,false);Invoke(owner,"OnDestroy");
    }

    static void CachedLifetime()
    {
        foreach(var phase in new[]{"loading","ready"})foreach(var action in new[]{"disable","destroy","configure"})
        {
            Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
            var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);
            var oldLoad=hint.loadOk;var oldClose=hint.close;var oldError=hint.error;
            if(phase=="ready"){Ready(hint);Ready(skip);}
            if(action=="configure")owner.Configure("adunit-newhint","adunit-newskip");
            else if(action=="disable"){owner.isActiveAndEnabled=false;Invoke(owner,"OnDisable");}
            else Invoke(owner,"OnDestroy");
            Check(hint.destroyed&&skip.destroyed&&hint.destroyCount==1&&skip.destroyCount==1,phase+" "+action+" cleans both unused caches exactly once");
            oldLoad(new WXTextResponse());oldClose(new WXRewardedVideoAdOnCloseResponse{isEnded=true});oldError(new WXADErrorResponse());
            Check(hint.showCount==0&&skip.showCount==0,phase+" "+action+" ignores stale cache events");
            if(action=="configure")
            {
                Tick(owner,2*WarmDelay);
                Check(Latest("adunit-newhint",start).loadCount==1&&Latest("adunit-newskip",start).loadCount==1,"reconfiguration warms only replacement IDs");
            }
            else
            {
                var count=WX.Created.Count;Tick(owner,1000);
                Check(!owner.Hints.IsAvailable&&WX.Created.Count==count,phase+" "+action+" cannot restart cache maintenance");
                if(action=="disable")
                {
                    owner.isActiveAndEnabled=true;Invoke(owner,"OnEnable");
                    Tick(owner,1000+WarmDelay-.001);
                    Check(WX.Created.Count==count,phase+" re-enable retains startup warm delay");
                    Tick(owner,1000+WarmDelay);
                    Check(WX.Created.Count==count+2&&Latest("adunit-hint",start)!=hint&&Latest("adunit-skip",start)!=skip,
                        phase+" re-enable warms fresh instances for both placements");
                }
            }
            Invoke(owner,"OnDestroy");
        }
    }

    static void WarmReentrancy()
    {
        Time.realtimeSinceStartupAsDouble=0;var start=WX.Created.Count;var owner=Owner();Tick(owner,WarmDelay);
        var hint=Latest("adunit-hint",start);var skip=Latest("adunit-skip",start);Ready(hint);Ready(skip);
        var results=new List<RewardedVideoResult>();var created=WX.Created.Count;
        owner.Hints.Show(result=>{results.Add(result);owner.EndlessSkip.Show(results.Add);});Close(hint,true);
        Check(hint.destroyed&&skip.showCount==1&&WX.Created.Count==created,"completion callback may immediately show the other preloaded placement");
        Check(!owner.Hints.IsAvailable&&!owner.EndlessSkip.IsAvailable,"reentrant warm show keeps global lock for new request");
        Tick(owner,WarmDelay+.1);Check(skip.showCount==1&&!skip.destroyed,"refilling prior placement cannot alter reentrant active request");
        Close(skip,true);Check(results.Count==2&&results.TrueForAll(x=>x==RewardedVideoResult.Completed),"reentrant warmed placements each reward exactly once");
        Tick(owner,WarmDelay+.2);hint=Latest("adunit-hint",start);Ready(hint);
        owner.Hints.Show(_=>throw new Exception("warm consumer"));Close(hint,true);
        Check(hint.destroyed&&owner.Hints.IsAvailable,"throwing warm consumer still releases native object and lock");
        Invoke(owner,"OnDestroy");
    }
#endif
}
