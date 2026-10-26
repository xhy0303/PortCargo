using System;
using System.Runtime.InteropServices;
using PortCargo.Core;
using UnityEngine;

namespace PortCargo.Platform
{
    /// <summary>
    /// Page cards and the separate gameplay grid. A failed or user-closed ad stays dismissed
    /// for that visit. Navigation retires the native object before accepting a new slot.
    /// </summary>
    public sealed class WeChatNativeAds : MonoBehaviour
    {
        private string homeId,completedId,gridId;
        private bool configured,disposed,hidden,paused,ownerDisabled,blocked,attempted,resumeDeferred;
        private NativeAdPlacement placement;
        private NativeAdBounds bounds;
        private NativeAdSizing sizing;
        private NativeAdTemplateSize verifiedGridSize;
        private int visit;
        private INativeAdBackend backend;

        public bool HomeAvailable => Available(homeId);
        public bool CompletedAvailable => Available(completedId);
        public bool PauseAvailable => Available(completedId);
        public bool SettingsAvailable => Available(completedId);
        public bool GameplayGridAvailable => Available(gridId);

        public void Configure(string homeId,string completedId,string gridId=null,NativeAdTemplateSize? verifiedGridSize=null)
        {
            configured=false;
            Release(false);
            this.homeId=NormalizeId(homeId);this.completedId=NormalizeId(completedId);
            this.gridId=NormalizeId(gridId);
            this.verifiedGridSize=verifiedGridSize.HasValue&&verifiedGridSize.Value.IsValidFor(this.gridId)
                ?verifiedGridSize.Value:default;
            placement=NativeAdPlacement.None;bounds=default;
            visit++;blocked=false;attempted=false;
            configured=!disposed;
        }

        public void SetPlacement(NativeAdPlacement next,Rect cssRect,bool requireMeasuredSize=false)
        {
            if(disposed)return;
            if((int)next<(int)NativeAdPlacement.Home||(int)next>(int)NativeAdPlacement.GameplayGrid)next=NativeAdPlacement.None;
            var nextBounds=ToBounds(cssRect);
            // A grid lacking native size callbacks may use an explicitly measured envelope
            // bound to this exact ad unit. It never inherits the horizontal card's ratio.
            var nextSizing=requireMeasuredSize||next==NativeAdPlacement.Home||next==NativeAdPlacement.GameplayGrid
                ?NativeAdSizing.RequireMeasurement:NativeAdSizing.Horizontal20x7;
            if(!requireMeasuredSize&&next==NativeAdPlacement.GameplayGrid&&verifiedGridSize.IsValidFor(gridId))
                nextSizing=NativeAdSizing.VerifiedFixed;
            if(next!=placement)
            {
                Release(false);
                visit++;placement=next;blocked=false;attempted=false;
            }
            else if(!bounds.SameAs(nextBounds)||sizing!=nextSizing)
            {
                Release(true);attempted=false;
            }
            bounds=nextBounds;
            sizing=nextSizing;
            if(next==NativeAdPlacement.None||!bounds.IsValid){Release(true);return;}
            Tick();
        }

        public void SetHidden(bool value)
        {
            if(hidden==value)return;
            hidden=value;
            if(value){Release(true);attempted=false;}
            // WX onShow listeners are unordered across the platform and native bridge.
            // Wait for the next Unity frame so the bridge's background gate is cleared.
            else resumeDeferred=true;
        }

        private static string NormalizeId(string value)
        {
            value=value?.Trim();
            return !string.IsNullOrEmpty(value)&&value.StartsWith("adunit-",StringComparison.Ordinal)&&value.Length>7?value:null;
        }

        private string PlacementId => placement==NativeAdPlacement.Home?homeId:
            placement==NativeAdPlacement.GameplayGrid?gridId:completedId;

        private static NativeAdBounds ToBounds(Rect rect)
        {
            if(!NativeAdBounds.Finite(rect.x)||!NativeAdBounds.Finite(rect.y)
                ||!NativeAdBounds.Finite(rect.width)||!NativeAdBounds.Finite(rect.height)
                ||rect.x<0||rect.y<0||rect.width<=0||rect.height<=0)return default;
            var left=(float)Math.Ceiling(rect.x);var top=(float)Math.Ceiling(rect.y);
            return new NativeAdBounds(left,top,(float)Math.Floor(rect.x+rect.width)-left,
                (float)Math.Floor(rect.y+rect.height)-top);
        }

        private bool Available(string id)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(disposed||!configured||ownerDisabled||!isActiveAndEnabled||!WeChatPlatform.Ready||string.IsNullOrEmpty(id))return false;
            try{return Backend.IsSupported;}catch{return false;}
#else
            return false;
#endif
        }

#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        private const double LoadTimeoutSeconds=10;
        private const double ShowTimeoutSeconds=10;
        private const double ResizeGraceSeconds=.25;
        private enum Stage { Loading, Opening, Visible }
        private sealed class Lease
        {
            public INativeAdBackend backend;
            public int handle,visit;
            public bool retired;
            public Stage stage;
            public double deadline,loadedAt=-1;
            public float measuredWidth,measuredHeight;
        }
        private Lease current;
        private INativeAdBackend Backend => backend??(backend=new NativeBackend());
        private bool IsCurrent(Lease lease)
        {return lease!=null&&!lease.retired&&ReferenceEquals(current,lease)&&lease.visit==visit;}
#endif

        private void Update(){resumeDeferred=false;Tick();}
        private void Tick()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(disposed||hidden||paused||ownerDisabled||resumeDeferred||!isActiveAndEnabled||placement==NativeAdPlacement.None||!bounds.IsValid)return;
            if(sizing==NativeAdSizing.VerifiedFixed&&
                (bounds.Width<verifiedGridSize.Width||bounds.Height<verifiedGridSize.Height))return;
            if(!Available(PlacementId))return;
            if(blocked)return;
            if(current==null)
            {
                if(attempted)return;
                attempted=true;
                var lease=new Lease{backend=Backend,visit=visit,deadline=Time.realtimeSinceStartupAsDouble+LoadTimeoutSeconds};
                current=lease;
                try
                {
                    lease.handle=lease.backend.Create(PlacementId,bounds,sizing,verifiedGridSize);
                    if(!IsCurrent(lease)){Cleanup(lease);return;}
                    if(lease.handle<=0){Fail(lease,"create");return;}
                }
                catch{Fail(lease,"create");return;}
            }
            var active=current;
            if(!IsCurrent(active))return;
            try
            {
                var state=active.backend.Read(active.handle);
                if(!IsCurrent(active))return;
                if(state.Closed||state.Failed){Fail(active,state.Closed?"closed":"native-error");return;}
                if(state.HasSize&&!bounds.Fits(state.Width,state.Height)){Fail(active,"size");return;}
                if(state.HasSize&&(active.measuredWidth!=state.Width||active.measuredHeight!=state.Height))
                {
                    active.measuredWidth=state.Width;active.measuredHeight=state.Height;
                    Trace("measured "+placement+" "+state.Width+"x"+state.Height+" slot="+bounds.Width+"x"+bounds.Height);
                }
                var now=Time.realtimeSinceStartupAsDouble;
                if(active.stage==Stage.Loading)
                {
                    if(!state.Loaded){if(now>=active.deadline)Fail(active,"load-timeout");return;}
                    if(active.loadedAt<0)active.loadedAt=now;
                    if(!state.HasSize)
                    {
                        if(sizing==NativeAdSizing.RequireMeasurement)
                        {if(now>=active.deadline)Fail(active,"size-timeout");return;}
                        if(now-active.loadedAt<ResizeGraceSeconds)return;
                        var width=sizing==NativeAdSizing.VerifiedFixed?verifiedGridSize.Width:bounds.Width;
                        var height=sizing==NativeAdSizing.VerifiedFixed?verifiedGridSize.Height:(float)Math.Ceiling(bounds.Width*7/20);
                        if(!bounds.Fits(width,height)){Fail(active,"template-fit");return;}
                        Trace(sizing==NativeAdSizing.VerifiedFixed?"verified-template-envelope":"20:7-layout-fallback");
                    }
                    active.stage=Stage.Opening;active.deadline=now+ShowTimeoutSeconds;
                    active.backend.Show(active.handle);
                }
                else if(active.stage==Stage.Opening)
                {
                    if(state.Visible)active.stage=Stage.Visible;
                    else if(now>=active.deadline)Fail(active,"show-timeout");
                }
            }
            catch{Fail(active,"native-exception");}
#endif
        }

#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        private void Fail(Lease lease,string reason)
        {
            if(!IsCurrent(lease))return;
            blocked=true;
            Release(false);
            Trace(reason);
        }

        private static void Cleanup(Lease lease)
        {
            lease.retired=true;
            if(lease.handle<=0)return;
            try{lease.backend.Hide(lease.handle);}catch{}
            try{lease.backend.Destroy(lease.handle);}catch{}
            lease.handle=0;
        }

        private sealed class NativeBackend : INativeAdBackend
        {
            [DllImport("__Internal")] private static extern int PortCargoNativeAdSupported();
            [DllImport("__Internal")] private static extern int PortCargoNativeAdCreate(string id,double left,double top,double width,double height,int sizing,double fixedWidth,double fixedHeight);
            [DllImport("__Internal")] private static extern double PortCargoNativeAdRead(int handle,int key);
            [DllImport("__Internal")] private static extern void PortCargoNativeAdShow(int handle);
            [DllImport("__Internal")] private static extern void PortCargoNativeAdHide(int handle);
            [DllImport("__Internal")] private static extern void PortCargoNativeAdDestroy(int handle);
            public bool IsSupported => PortCargoNativeAdSupported()!=0;
            public int Create(string id,NativeAdBounds slot,NativeAdSizing sizing,NativeAdTemplateSize verifiedSize)
            {
                if(sizing==NativeAdSizing.VerifiedFixed&&!verifiedSize.IsValidFor(id))return 0;
                return PortCargoNativeAdCreate(id,slot.Left,slot.Top,slot.Width,slot.Height,(int)sizing,
                    sizing==NativeAdSizing.VerifiedFixed?verifiedSize.Width:0,sizing==NativeAdSizing.VerifiedFixed?verifiedSize.Height:0);
            }
            public NativeAdSnapshot Read(int handle)
            {
                var flags=(int)PortCargoNativeAdRead(handle,0);
                return new NativeAdSnapshot{Loaded=(flags&1)!=0,Closed=(flags&2)!=0,Failed=(flags&4)!=0,
                    Visible=(flags&8)!=0,HasSize=(flags&16)!=0,Width=(float)PortCargoNativeAdRead(handle,1),Height=(float)PortCargoNativeAdRead(handle,2)};
            }
            public void Show(int handle){PortCargoNativeAdShow(handle);}
            public void Hide(int handle){PortCargoNativeAdHide(handle);}
            public void Destroy(int handle){PortCargoNativeAdDestroy(handle);}
        }
#endif

        private void Release(bool preserveVisit)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var lease=current;current=null;
            if(lease==null)return;
            if(preserveVisit&&lease.handle>0)
            {
                try{var state=lease.backend.Read(lease.handle);if(lease.visit==visit&&(state.Closed||state.Failed))blocked=true;}
                catch{if(lease.visit==visit)blocked=true;}
            }
            Cleanup(lease);
#endif
        }

        private void OnApplicationPause(bool value)
        {
            if(paused==value)return;
            paused=value;
            if(value){Release(true);attempted=false;}
            else resumeDeferred=true;
        }
        private void OnEnable(){ownerDisabled=false;}
        private void OnDisable(){ownerDisabled=true;Release(true);attempted=false;}
        private void OnDestroy(){disposed=true;configured=false;Release(false);}
        private static void Trace(string reason)
        {
#if DEVELOPMENT_BUILD
            Debug.Log("PORTCARGO_NATIVE_AD "+reason);
#endif
        }
    }
}
