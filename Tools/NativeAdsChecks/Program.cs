using System;
using System.Collections.Generic;
using System.Reflection;
using PortCargo.Core;
using PortCargo.Platform;
using PortCargo.Runtime;
using UnityEngine;

static class Program
{
    static int checks;
    static readonly Rect HomeRect=new Rect(20,300,320,112),ResultRect=new Rect(20,420,320,112);
    static void Check(bool value,string label){checks++;if(!value)throw new Exception("CHECK FAILED: "+label);}
    static void Run(string label,Action test){var before=checks;test();Console.WriteLine("PASS "+label+" checks="+(checks-before));}
    static void Invoke(object instance,string method)=>instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(instance,null);
    static void Tick(WeChatNativeAds ads,double time){Time.realtimeSinceStartupAsDouble=time;Invoke(ads,"Update");}
    static WeChatNativeAds Owner(out FakeBackend backend)
    {
        Time.realtimeSinceStartupAsDouble=0;WeChatPlatform.Ready=true;WeChatPlatform.Hidden=false;
        var owner=new WeChatNativeAds();backend=new FakeBackend();
        typeof(WeChatNativeAds).GetField("backend",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,backend);
        owner.Configure("adunit-home","adunit-completed");return owner;
    }
    static Entry Place(WeChatNativeAds ads,FakeBackend backend,NativeAdPlacement placement=NativeAdPlacement.Home)
    {ads.SetPlacement(placement,placement==NativeAdPlacement.Home?HomeRect:ResultRect);Tick(ads,Time.realtimeSinceStartupAsDouble+.01);return backend.Last;}
    static Entry Display(WeChatNativeAds ads,FakeBackend backend,NativeAdPlacement placement=NativeAdPlacement.Home)
    {
        var entry=Place(ads,backend,placement);entry.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};
        Tick(ads,Time.realtimeSinceStartupAsDouble+.01);
        Check(entry.Shows==1,"known fitting loaded native card requests Show once");
        entry.Snapshot.Visible=true;Tick(ads,Time.realtimeSinceStartupAsDouble+.01);return entry;
    }
    static void Main()
    {
        Run("CSS mapping and invalid geometry",Coordinates);
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        Run("placement ownership and cleanup",Placements);
        Run("failed loads and no request storm",Failures);
        Run("dismissed visit and resizing",Dismissal);
        Run("hidden/background/rewarded gates",HiddenAndNavigation);
        Run("load and show deadlines",Deadlines);
        Run("native size containment",SizeContainment);
        Run("configuration and lifecycle",Lifetime);
        Run("lease-release reentrancy",ReleaseReentrancy);
        Run("presenter visibility visit preservation",PresenterVisibility);
        Run("expanded page visits and shared card ID",ExpandedPlacements);
        Run("grid and compact home require real size",MeasuredTemplates);
        Run("verified single-grid template envelope",VerifiedTemplateEnvelope);
        Console.WriteLine("NATIVE_ADAPTER_PLAYER_CONTRACT_PASS checks="+checks);
#else
        var ads=new WeChatNativeAds();ads.Configure("adunit-home","adunit-completed");
        Check(!ads.HomeAvailable&&!ads.CompletedAvailable&&!ads.PauseAvailable&&!ads.SettingsAvailable&&!ads.GameplayGridAvailable,"non-player native placements remain unavailable");
        ads.SetPlacement(NativeAdPlacement.Home,HomeRect);Tick(ads,100);ads.SetHidden(true);ads.SetHidden(false);
        ads.SetPlacement(NativeAdPlacement.Completed,ResultRect);Tick(ads,200);
        ads.Configure("adunit-home","adunit-completed","adunit-grid");
        foreach(var page in new[]{NativeAdPlacement.Pause,NativeAdPlacement.Settings,NativeAdPlacement.GameplayGrid})
        {ads.SetPlacement(page,ResultRect);Tick(ads,210);}
        Check(!ads.HomeAvailable&&!ads.CompletedAvailable,"unsupported navigation never enables native advertising");
        Invoke(ads,"OnDestroy");Console.WriteLine("NATIVE_ADAPTER_NONPLAYER_PASS checks="+checks);
#endif
    }
    static void Coordinates()
    {
        Check(NativeAdsPresenter.TryConvertRect(new Rect(60,900,960,336),1080,2400,360,800,out var css),"DPR3 screen rectangle converts");
        Check(Math.Abs(css.x-20)<.001&&Math.Abs(css.y-388)<.001&&Math.Abs(css.width-320)<.001&&Math.Abs(css.height-112)<.001,"CSS map divides by three and flips bottom-left Y to native top-left");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(-5,0,100,35),1080,2400,360,800,out _),"negative off-screen origin rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(1000,0,100,35),1080,2400,360,800,out _),"right screen overflow rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(0,2380,100,35),1080,2400,360,800,out _),"top screen overflow rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(float.NaN,0,100,35),1080,2400,360,800,out _),"NaN rectangle rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(0,0,float.PositiveInfinity,35),1080,2400,360,800,out _),"infinite rectangle rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(0,0,100,0),1080,2400,360,800,out _),"empty rectangle rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(0,0,100,35),0,2400,360,800,out _),"invalid framebuffer rejected");
        Check(!NativeAdsPresenter.TryConvertRect(new Rect(0,0,100,35),1080,2400,float.NaN,800,out _),"invalid native window rejected");
    }
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
    static void Placements()
    {
        var owner=Owner(out var backend);Check(owner.HomeAvailable&&owner.CompletedAvailable,"configured supported native placements available");
        Check(backend.Creates==0,"configuration does not create unsolicited native cards");
        var home=Display(owner,backend);Check(home.Id=="adunit-home","home has its own ad ID");
        Check(home.Bounds.Left==20&&home.Bounds.Top==300&&home.Bounds.Width==320&&home.Bounds.Height==112,"native reserved rectangle retained exactly");
        for(var i=0;i<20;i++){owner.SetPlacement(NativeAdPlacement.Home,HomeRect);Tick(owner,.1+i*.01);}
        Check(backend.Creates==1&&home.Shows==1,"same layout notifications never reload or reshow current card");
        owner.SetPlacement(NativeAdPlacement.None,default);
        Check(home.Hides>=1&&home.Destroys==1,"navigation invalidation synchronously hides and destroys native overlay");
        var completed=Display(owner,backend,NativeAdPlacement.Completed);
        Check(completed!=home&&completed.Id=="adunit-completed","completed screen uses independent placement/lease");
        var oldReads=home.Reads;home.Snapshot.Visible=true;home.Snapshot.Closed=true;Tick(owner,1);
        Check(home.Reads==oldReads&&completed.Destroys==0,"obsolete lease state cannot dismiss or revive new placement");
        Invoke(owner,"OnDestroy");Check(completed.Destroys==1,"owner shutdown cleans active card");
    }
    static void Failures()
    {
        foreach(var fault in new[]{"create-null","create-throw","load-error","read-throw","show-throw"})
        {
            var owner=Owner(out var backend);backend.NullCreate=fault=="create-null";backend.ThrowCreate=fault=="create-throw";
            owner.SetPlacement(NativeAdPlacement.Home,HomeRect);Tick(owner,.01);
            var entry=backend.Last;
            if(fault=="load-error")entry.Snapshot.Failed=true;
            if(fault=="read-throw")backend.ThrowRead=true;
            if(fault=="show-throw"){entry.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};backend.ThrowShow=true;}
            Tick(owner,.02);backend.ThrowRead=false;backend.ThrowShow=false;var attempts=backend.Creates;
            for(var i=0;i<30;i++){owner.SetPlacement(NativeAdPlacement.Home,HomeRect);Tick(owner,30+i*30);}
            Check(attempts==1&&backend.Creates==1,fault+" fails open for gameplay without retry storm in same visit");
            if(entry!=null)Check(entry.Destroys==1,fault+" retires failed native object");
            backend.NullCreate=backend.ThrowCreate=false;owner.SetPlacement(NativeAdPlacement.None,default);Place(owner,backend);
            Check(backend.Creates==2,fault+" allows a fresh attempt only on a later visit");
            Invoke(owner,"OnDestroy");
        }
        var offline=Owner(out var native);WeChatPlatform.Ready=false;offline.SetPlacement(NativeAdPlacement.Home,HomeRect);
        for(var i=0;i<20;i++)Tick(offline,i);
        Check(native.Creates==0&&!offline.HomeAvailable,"SDK-unavailable state cannot produce native requests");
        WeChatPlatform.Ready=true;Tick(offline,21);Check(native.Creates<=1,"SDK recovery cannot cause multiple creation attempts");Invoke(offline,"OnDestroy");
    }
    static void Dismissal()
    {
        var owner=Owner(out var backend);var card=Display(owner,backend);card.Snapshot.Closed=true;Tick(owner,1);
        Check(card.Hides>=1&&card.Destroys==1,"user close retires native object");
        var count=backend.Creates;
        for(var i=0;i<10;i++){owner.SetPlacement(NativeAdPlacement.Home,new Rect(20+i,300,320,112));Tick(owner,2+i);}
        Check(backend.Creates==count,"user close remains dismissed through same-visit resize and layout refresh");
        owner.SetHidden(true);owner.SetHidden(false);Tick(owner,20);
        Check(backend.Creates==count,"temporary hidden gate cannot reset user dismissal");
        owner.SetPlacement(NativeAdPlacement.None,default);Display(owner,backend);
        Check(backend.Creates==count+1,"new home visit may show again after previous visit was dismissed");Invoke(owner,"OnDestroy");

        owner=Owner(out backend);card=Display(owner,backend);card.Snapshot.Closed=true;
        owner.SetHidden(true);owner.SetHidden(false);Tick(owner,1);
        Check(backend.Creates==1,"hide snapshots a pending close before retiring its lease");Invoke(owner,"OnDestroy");
    }
    static void HiddenAndNavigation()
    {
        var owner=Owner(out var backend);var card=Display(owner,backend);owner.SetHidden(true);
        Check(card.Hides>=1&&card.Destroys==1,"background or rewarded gate synchronously hides native overlay");
        for(var i=0;i<10;i++)Tick(owner,1+i);
        Check(backend.Creates==1,"hidden native placement cannot request or show another card");
        owner.SetHidden(false);owner.SetPlacement(NativeAdPlacement.Home,HomeRect);
        Check(backend.Creates==1,"foreground and synchronous layout notification defer creation until the next Unity Update");
        Tick(owner,11);
        Check(backend.Creates==2&&backend.Last!=card,"returning to un-dismissed same visit can rebuild one lease");
        owner.SetHidden(true);owner.SetPlacement(NativeAdPlacement.Completed,ResultRect);Tick(owner,12);
        Check(backend.Creates==2,"navigation while hidden cannot expose native card");
        owner.SetHidden(false);Tick(owner,13);
        Check(backend.Creates==3&&backend.Last.Id=="adunit-completed","resume uses current eligible placement rather than stale prior screen");
        Invoke(owner,"OnDestroy");
        owner=Owner(out backend);card=Display(owner,backend);
        typeof(WeChatNativeAds).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,new object[]{true});
        typeof(WeChatNativeAds).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,new object[]{false});
        owner.SetPlacement(NativeAdPlacement.Home,HomeRect);
        Check(backend.Creates==1,"Unity pause recovery cannot race native onShow cleanup before next frame");
        Tick(owner,12);Check(backend.Creates==2,"deferred Unity pause recovery rebuilds on the next frame");Invoke(owner,"OnDestroy");
    }
    static void Deadlines()
    {
        foreach(var phase in new[]{"load","show"})
        {
            var owner=Owner(out var backend);var card=Place(owner,backend);var began=card.CreatedAt;
            if(phase=="show")
            {card.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};Tick(owner,1);began=card.ShownAt;}
            Tick(owner,began+10-.001);Check(card.Destroys==0,phase+" deadline is not early");
            Tick(owner,began+10);Check(card.Destroys==1,phase+" timeout retires stalled native ad");
            card.Snapshot=new NativeAdSnapshot{Loaded=true,Visible=true,HasSize=true,Width=320,Height=112};
            Tick(owner,began+100);Check(backend.Creates==1&&card.Destroys==1,phase+" late completion does not resurrect timed-out visit");Invoke(owner,"OnDestroy");
        }
    }
    static void SizeContainment()
    {
        foreach(var pair in new[]{(321f,112f),(320f,114f),(float.NaN,112f),(320f,float.PositiveInfinity),(0f,112f)})
        {
            var owner=Owner(out var backend);var card=Place(owner,backend);
            card.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=pair.Item1,Height=pair.Item2};Tick(owner,1);
            Check(card.Shows==0&&card.Destroys==1,"invalid/oversized measured native card is never shown: "+pair);Invoke(owner,"OnDestroy");
        }
        var fitting=Owner(out var native);var visible=Display(fitting,native);
        visible.Snapshot.Height=200;Tick(fitting,1);Check(visible.Hides>=1&&visible.Destroys==1,"late native growth exceeding reservation hides immediately");Invoke(fitting,"OnDestroy");
        fitting=Owner(out native);var unknown=Place(fitting,native,NativeAdPlacement.Completed);unknown.Snapshot.Loaded=true;
        Tick(fitting,1);Tick(fitting,1.249);Check(unknown.Shows==0,"missing native resize signal waits the measurement grace");
        Tick(fitting,1.25);Check(unknown.Shows==1,"20:7 reserved card permits bounded fallback after missing size grace");Invoke(fitting,"OnDestroy");
        foreach(var rect in new[]{new Rect(0,0,0,0),new Rect(-1,0,320,112),new Rect(0,float.NaN,320,112)})
        {
            fitting=Owner(out native);fitting.SetPlacement(NativeAdPlacement.Home,rect);Tick(fitting,1);
            Check(native.Creates==0,"invalid or too-short reservation never creates ad");Invoke(fitting,"OnDestroy");
        }
        fitting=Owner(out native);fitting.SetPlacement(NativeAdPlacement.Completed,new Rect(0,0,320,50));
        var shortSlot=native.Last;shortSlot.Snapshot.Loaded=true;Tick(fitting,1);Tick(fitting,1.25);
        Check(shortSlot.Shows==0&&shortSlot.Destroys==1,"unknown size cannot use 20:7 fallback when reservation is too short");Invoke(fitting,"OnDestroy");
    }
    static void Lifetime()
    {
        foreach(var action in new[]{"configure","disable","destroy"})
        {
            var owner=Owner(out var backend);var card=Display(owner,backend);
            if(action=="configure")owner.Configure("adunit-newhome","adunit-newcompleted");
            else if(action=="disable"){owner.isActiveAndEnabled=false;Invoke(owner,"OnDisable");}
            else Invoke(owner,"OnDestroy");
            Check(card.Hides>=1&&card.Destroys==1,action+" synchronously retires native lease");
            var count=backend.Creates;card.Snapshot.Visible=true;Tick(owner,10);
            if(action=="configure")
            {owner.SetPlacement(NativeAdPlacement.Home,HomeRect);Tick(owner,11);Check(backend.Last.Id=="adunit-newhome","configuration change uses replacement native ID");}
            else Check(backend.Creates==count&&!owner.HomeAvailable,action+" does not revive stale native card");
            Invoke(owner,"OnDestroy");
        }
        var cleanup=Owner(out var native);var ad=Display(cleanup,native);native.ThrowHide=native.ThrowDestroy=true;
        cleanup.SetPlacement(NativeAdPlacement.None,default);
        Check(ad.Hides>=1&&ad.Destroys==1,"throwing hide still reaches destroy cleanup");native.ThrowHide=native.ThrowDestroy=false;Invoke(cleanup,"OnDestroy");
    }
    static void PresenterVisibility()
    {
        var ads=Owner(out var backend);var flow=new GameFlowController();var bridge=new NativeAdsPresenter();
        Invoke(bridge,"OnEnable");bridge.Bind(flow,ads);Tick(ads,.01);var card=backend.Last;
        card.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};Tick(ads,.02);card.Snapshot.Visible=true;Tick(ads,.03);
        card.Snapshot.Closed=true;Tick(ads,.04);var count=backend.Creates;
        WeChatPlatform.SetVisibility(true);WeChatPlatform.SetVisibility(false);Tick(ads,1);
        Check(backend.Creates==count,"presenter foreground invalid-then-valid flow events preserve dismissed same visit");
        bridge.isActiveAndEnabled=false;flow.isActiveAndEnabled=false;flow.PublishLifecycleState();Invoke(bridge,"OnDisable");
        bridge.isActiveAndEnabled=true;flow.isActiveAndEnabled=true;Invoke(bridge,"OnEnable");Tick(ads,1.5);
        Check(backend.Creates==count,"flow-first GameObject disable then bridge re-enable preserves dismissed same visit");
        flow.Navigate(NativeAdPlacement.None);flow.Navigate(NativeAdPlacement.Home);Tick(ads,2);
        Check(backend.Creates==count+1,"real navigation through None permits a fresh home visit");
        WeChatPlatform.SetVisibility(true);flow.Navigate(NativeAdPlacement.Completed);WeChatPlatform.SetVisibility(false);Tick(ads,3);
        Check(backend.Last.Id=="adunit-completed","foreground resumes final screen after hidden navigation");
        Invoke(bridge,"OnDisable");Invoke(ads,"OnDestroy");WeChatPlatform.Hidden=false;
    }
    static void ReleaseReentrancy()
    {
        var owner=Owner(out var backend);var old=Display(owner,backend);old.Snapshot.Closed=true;
        backend.BeforeRead=_=>owner.SetPlacement(NativeAdPlacement.Completed,ResultRect);
        owner.SetHidden(true);owner.SetHidden(false);Tick(owner,1);
        Check(backend.Creates==2&&backend.Last.Id=="adunit-completed","old lease Read reentrancy may navigate without transferring old closed state");
        var next=backend.Last;next.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};Tick(owner,2);
        Check(next.Shows==1&&old.Destroys==1,"reentrant replacement remains displayable and old cleanup happens once");Invoke(owner,"OnDestroy");
    }
    static void ExpandedPlacements()
    {
        var owner=Owner(out var backend);
        Check(owner.PauseAvailable&&owner.SettingsAvailable&&!owner.GameplayGridAvailable,"shared page ID enables pause/settings while missing grid ID stays unavailable");
        owner.SetPlacement(NativeAdPlacement.GameplayGrid,new Rect(240,50,90,90));Tick(owner,1);
        Check(backend.Creates==0,"missing grid ID never falls back to the completed card ID");
        owner.Configure("adunit-home","adunit-completed","adunit-grid");
        Check(owner.GameplayGridAvailable,"valid dedicated grid ID enables only the configured grid backend");
        Entry previous=null;
        foreach(var page in new[]{NativeAdPlacement.Completed,NativeAdPlacement.Pause,NativeAdPlacement.Settings,NativeAdPlacement.Completed})
        {
            var entry=Display(owner,backend,page);
            Check(entry.Id=="adunit-completed","page shares configured result unit: "+page);
            Check(entry.Sizing==NativeAdSizing.Horizontal20x7,"existing card template is explicit: "+page);
            if(previous!=null)
            {
                Check(previous.Destroys==1&&previous!=entry,"transition retires previous object even when ID is identical");
                previous.Snapshot=new NativeAdSnapshot{Closed=true,Failed=true,Visible=true};Tick(owner,Time.realtimeSinceStartupAsDouble+.01);
                Check(entry.Destroys==0,"old same-ID callbacks cannot poison replacement visit");
            }
            for(var i=0;i<15;i++)owner.SetPlacement(page,ResultRect);
            Check(entry.Shows==1,"same-page settings/layout refresh cannot reload or show repeatedly");
            previous=entry;
        }
        previous.Snapshot.Closed=true;Tick(owner,5);var count=backend.Creates;
        owner.SetHidden(true);owner.SetHidden(false);Tick(owner,6);owner.SetPlacement(NativeAdPlacement.Completed,ResultRect);
        Check(backend.Creates==count,"shared result visit dismissal survives foreground resume");
        owner.SetPlacement(NativeAdPlacement.Pause,ResultRect);Tick(owner,7);
        Check(backend.Creates==count+1,"different page starts a new visit despite same ad unit ID");
        Invoke(owner,"OnDestroy");
    }
    static void MeasuredTemplates()
    {
        foreach(var page in new[]{NativeAdPlacement.Home,NativeAdPlacement.GameplayGrid,NativeAdPlacement.Settings})
        {
            var owner=Owner(out var backend);owner.Configure("adunit-home","adunit-completed","adunit-grid");
            var rect=page==NativeAdPlacement.GameplayGrid?new Rect(240,50,90,90):new Rect(40,300,240,84);
            owner.SetPlacement(page,rect,true);var entry=backend.Last;entry.Snapshot.Loaded=true;
            Check(entry.Sizing==NativeAdSizing.RequireMeasurement,"compact template uses explicit measured-size policy: "+page);
            Tick(owner,.3);Tick(owner,9.999);
            Check(entry.Shows==0&&entry.Destroys==0,"unmeasured template never adopts a20:7 size: "+page);
            Tick(owner,10);Check(entry.Shows==0&&entry.Destroys==1,"missing actual dimensions time out without a show: "+page);
            entry.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=rect.width,Height=rect.height};Tick(owner,20);
            Check(entry.Shows==0&&backend.Creates==1,"late size cannot revive rejected template: "+page);
            Invoke(owner,"OnDestroy");
        }
        var grid=Owner(out var native);grid.Configure("adunit-home","adunit-completed","adunit-grid");
        var slot=new Rect(240,50,90,90);grid.SetPlacement(NativeAdPlacement.GameplayGrid,slot);var ad=native.Last;
        Check(ad.Id=="adunit-grid"&&ad.Sizing==NativeAdSizing.RequireMeasurement,"grid selects dedicated ID and measured policy without optional strict flag");
        ad.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=90,Height=90};Tick(grid,.1);
        Check(ad.Shows==1,"measured square grid displays without horizontal card aspect assumptions");
        ad.Snapshot.Visible=true;Tick(grid,.2);
        for(var i=0;i<30;i++)grid.SetPlacement(NativeAdPlacement.GameplayGrid,slot);
        Check(native.Creates==1&&ad.Shows==1,"normal box-move layout updates do not refresh the grid");
        ad.Snapshot.Closed=true;Tick(grid,.3);grid.SetHidden(true);grid.SetHidden(false);Tick(grid,.4);
        grid.SetPlacement(NativeAdPlacement.GameplayGrid,new Rect(239,50,90,90));Tick(grid,.5);
        Check(native.Creates==1,"closed grid stays dismissed through same-visit resize/background");
        grid.SetPlacement(NativeAdPlacement.Pause,ResultRect);var pause=native.Last;
        Check(ad.Destroys==1&&pause.Id=="adunit-completed","pause replaces grid only after synchronous cleanup");
        grid.SetPlacement(NativeAdPlacement.GameplayGrid,slot);var next=native.Last;
        Check(pause.Destroys==1&&next.Id=="adunit-grid"&&next!=ad,"resume creates independent grid visit");
        next.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=90,Height=91};Tick(grid,.6);
        Check(next.Shows==0&&next.Destroys==1,"actual grid height beyond reserved slot is refused");
        Invoke(grid,"OnDestroy");
        var home=Owner(out native);home.SetPlacement(NativeAdPlacement.Home,new Rect(40,300,240,84));
        var compact=native.Last;Check(compact.Sizing==NativeAdSizing.RequireMeasurement,"Home always requires actual dimension proof");
        compact.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=320,Height=112};Tick(home,.1);
        Check(compact.Shows==0&&compact.Destroys==1,"native template ignoring smaller requested Home width cannot cover neighboring controls");
        Invoke(home,"OnDestroy");
    }
    static void VerifiedTemplateEnvelope()
    {
        const string id="adunit-2060c1fccf2ceecf";
        var profile=new NativeAdTemplateSize(id,80,120);
        var slot=new Rect(240,50,80,120);
        var owner=Owner(out var backend);owner.Configure("adunit-home","adunit-completed",id,profile);
        owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);var card=backend.Last;card.Snapshot.Loaded=true;
        Check(card.Sizing==NativeAdSizing.VerifiedFixed&&card.VerifiedSize.IsValidFor(id),"fixed template envelope stays bound to explicitly verified unit");
        Tick(owner,1);Tick(owner,1.249);Check(card.Shows==0,"fixed grid permits a short grace for native size before fallback");
        Tick(owner,1.25);Check(card.Shows==1,"verified80x120 grid may show when template intentionally has no resize API");
        Check(!card.Snapshot.HasSize,"fixed envelope does not fabricate native measurement evidence");
        card.Snapshot.Visible=true;Tick(owner,1.3);card.Snapshot.Closed=true;Tick(owner,1.4);
        owner.SetHidden(true);owner.SetHidden(false);Tick(owner,2);owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);
        Check(backend.Creates==1,"verified fallback cannot bypass user close or same-visit lifecycle gate");
        owner.SetPlacement(NativeAdPlacement.Pause,ResultRect);owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);var fresh=backend.Last;
        Check(fresh!=card&&fresh.VerifiedSize.IsValidFor(id),"a later grid visit gets an independent lease with the same ID-bound profile");
        fresh.Snapshot=new NativeAdSnapshot{Loaded=true,HasSize=true,Width=68,Height=110};Tick(owner,2.1);
        Check(fresh.Shows==1,"actual smaller native size takes precedence over conservative fixed envelope");
        fresh.Snapshot.Height=121;Tick(owner,2.2);
        Check(fresh.Destroys==1,"native dimensions exceeding slot still fail after fixed-profile show");
        Invoke(owner,"OnDestroy");
        foreach(var rect in new[]{new Rect(240,50,79,120),new Rect(240,50,80,119),new Rect(240.2f,50,80,120)})
        {
            owner=Owner(out backend);owner.Configure("adunit-home","adunit-completed",id,profile);
            owner.SetPlacement(NativeAdPlacement.GameplayGrid,rect);Tick(owner,30);
            Check(backend.Creates==0,"undersized fixed-template envelope generates no ad request: "+rect.width+"x"+rect.height);
            owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);Check(backend.Creates==1,"later fitting layout can start the one visit attempt");Invoke(owner,"OnDestroy");
        }
        foreach(var invalid in new[]{new NativeAdTemplateSize("adunit-other",80,120),new NativeAdTemplateSize(id,0,120),new NativeAdTemplateSize(id,80,float.NaN)})
        {
            owner=Owner(out backend);owner.Configure("adunit-home","adunit-completed",id,invalid);
            owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);card=backend.Last;card.Snapshot.Loaded=true;Tick(owner,.1);Tick(owner,10);
            Check(card.Sizing==NativeAdSizing.RequireMeasurement&&card.Shows==0,"mismatched or invalid profile never grants a fallback to a different template");Invoke(owner,"OnDestroy");
        }
        owner=Owner(out backend);owner.Configure("adunit-home","adunit-completed",id,profile);
        owner.Configure("adunit-home","adunit-completed",id);owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);
        Check(backend.Last.Sizing==NativeAdSizing.RequireMeasurement,"reconfigure without explicit evidence clears previously trusted template size");Invoke(owner,"OnDestroy");
        owner=Owner(out backend);owner.Configure("adunit-home","adunit-completed",id,profile);
        owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot,true);card=backend.Last;card.Snapshot.Loaded=true;Tick(owner,10);
        Check(card.Sizing==NativeAdSizing.RequireMeasurement&&card.Shows==0,"caller can still require runtime measurement despite a configured envelope");Invoke(owner,"OnDestroy");
        foreach(var fault in new[]{"load","show"})
        {
            owner=Owner(out backend);owner.Configure("adunit-home","adunit-completed",id,profile);owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);card=backend.Last;
            if(fault=="load")card.Snapshot.Failed=true;
            else{backend.ThrowShow=true;card.Snapshot.Loaded=true;Tick(owner,.1);}
            Tick(owner,1);backend.ThrowShow=false;for(var i=0;i<20;i++){owner.SetPlacement(NativeAdPlacement.GameplayGrid,slot);Tick(owner,10+i);}
            Check(card.Destroys==1&&backend.Creates==1,"fixed-template "+fault+" failure remains silent and bounded");Invoke(owner,"OnDestroy");
        }
    }
#endif
    sealed class Entry
    {
        public int Handle,Hides,Destroys,Shows,Reads;public string Id;
        public double CreatedAt,ShownAt;public NativeAdBounds Bounds;public NativeAdSnapshot Snapshot;public NativeAdSizing Sizing;public NativeAdTemplateSize VerifiedSize;
    }
    sealed class FakeBackend : INativeAdBackend
    {
        public bool Supported=true,NullCreate,ThrowCreate,ThrowRead,ThrowShow,ThrowHide,ThrowDestroy;
        public bool IsSupported=>Supported;
        public int Creates;public Entry Last;
        public Action<int> BeforeRead;
        readonly Dictionary<int,Entry> entries=new Dictionary<int,Entry>();
        public int Create(string id,NativeAdBounds bounds,NativeAdSizing sizing,NativeAdTemplateSize verifiedSize)
        {
            Creates++;if(ThrowCreate)throw new Exception("create");if(NullCreate)return 0;
            Last=new Entry{Handle=Creates,Id=id,Bounds=bounds,Sizing=sizing,VerifiedSize=verifiedSize,CreatedAt=Time.realtimeSinceStartupAsDouble};entries.Add(Creates,Last);return Creates;
        }
        public NativeAdSnapshot Read(int handle){if(ThrowRead)throw new Exception("read");var before=BeforeRead;BeforeRead=null;before?.Invoke(handle);var e=entries[handle];e.Reads++;return e.Snapshot;}
        public void Show(int handle){var e=entries[handle];e.Shows++;e.ShownAt=Time.realtimeSinceStartupAsDouble;if(ThrowShow)throw new Exception("show");}
        public void Hide(int handle){var e=entries[handle];e.Hides++;e.Snapshot.Visible=false;if(ThrowHide)throw new Exception("hide");}
        public void Destroy(int handle){entries[handle].Destroys++;if(ThrowDestroy)throw new Exception("destroy");}
    }
}
