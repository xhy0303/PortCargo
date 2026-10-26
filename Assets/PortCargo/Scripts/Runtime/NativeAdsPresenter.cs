using PortCargo.Core;
using PortCargo.Platform;
using UnityEngine;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace PortCargo.Runtime
{
    /// <summary>Maps the flow's reserved UI rectangle to the native overlay, without gating navigation.</summary>
    public sealed class NativeAdsPresenter : MonoBehaviour
    {
        private GameFlowController flow;
        private WeChatNativeAds ads;
        private bool paused,hidden;

        public void Bind(GameFlowController nextFlow,WeChatNativeAds nextAds)
        {
            if(flow)flow.NativeAdPlacementChanged-=Synchronize;
            if(ads)ads.SetPlacement(NativeAdPlacement.None,default);
            flow=nextFlow;ads=nextAds;
            if(!flow)return;
            if(isActiveAndEnabled)flow.NativeAdPlacementChanged+=Synchronize;
            hidden=!isActiveAndEnabled||paused||WeChatPlatform.Hidden;
            if(ads)ads.SetHidden(hidden);
            flow.SetNativeAdsHidden(hidden);
            RefreshAvailability();
            Synchronize();
        }

        private void OnEnable()
        {
            hidden=true;
            WeChatPlatform.VisibilityChanged+=OnPlatformVisibility;
            if(flow)
            {
                flow.NativeAdPlacementChanged+=Synchronize;
                RefreshAvailability();
            }
            ApplyVisibility();
        }

        private void OnDisable()
        {
            WeChatPlatform.VisibilityChanged-=OnPlatformVisibility;
            if(flow)flow.NativeAdPlacementChanged-=Synchronize;
            hidden=true;
            if(ads)ads.SetHidden(true);
            if(flow)flow.SetNativeAdAvailability(false,false);
        }

        private void OnApplicationPause(bool value){paused=value;ApplyVisibility();}
        private void RefreshAvailability()
        {
            if(!flow)return;
            var enabled=isActiveAndEnabled&&ads;
            flow.SetNativeAdAvailability(enabled&&ads.HomeAvailable,enabled&&ads.CompletedAvailable,
                enabled&&ads.PauseAvailable,enabled&&ads.SettingsAvailable,enabled&&ads.GameplayGridAvailable);
        }
        private void OnPlatformVisibility(bool _) => ApplyVisibility();
        private void ApplyVisibility()
        {
            var nextHidden=!isActiveAndEnabled||paused||WeChatPlatform.Hidden;
            // Retain the visit across a temporary app hide, including a user's dismissed card.
            // Resume validates the current screen while the adapter is still hidden.
            // Flow emits a transient None while relayout runs; do not interpret that as navigation.
            hidden=true;
            if(ads)ads.SetHidden(true);
            if(flow)flow.SetNativeAdsHidden(nextHidden);
            hidden=nextHidden;
            if(!hidden){Synchronize();if(ads)ads.SetHidden(false);}
        }

        private void Synchronize()
        {
            if(!ads||hidden)return;
            if(!isActiveAndEnabled){ads.SetHidden(true);return;}
            if(!flow||!flow.TryGetNativeAdPlacement(out var placement,out var rect))
            {ads.SetPlacement(NativeAdPlacement.None,default);return;}
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            try
            {
                var window=WX.GetWindowInfo();
                if(TryConvertRect(rect,Screen.width,Screen.height,(float)window.windowWidth,(float)window.windowHeight,out var css))
                {ads.SetPlacement(placement,css);return;}
            }
            catch(System.Exception){ /* Optional overlay failure must never prevent play. */ }
#endif
            ads.SetPlacement(NativeAdPlacement.None,default);
        }

        public static bool TryConvertRect(Rect pixels,float screenWidth,float screenHeight,float windowWidth,float windowHeight,out Rect css)
        {
            css=default;
            if(!Positive(screenWidth)||!Positive(screenHeight)||!Positive(windowWidth)||!Positive(windowHeight)
                ||!Positive(pixels.width)||!Positive(pixels.height)||!Finite(pixels.x)||!Finite(pixels.y)
                ||pixels.xMin<-.5f||pixels.yMin<-.5f||pixels.xMax>screenWidth+.5f||pixels.yMax>screenHeight+.5f)return false;
            css=new Rect(pixels.xMin/screenWidth*windowWidth,(screenHeight-pixels.yMax)/screenHeight*windowHeight,
                pixels.width/screenWidth*windowWidth,pixels.height/screenHeight*windowHeight);
            return true;
        }
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        private static bool Positive(float n)=>Finite(n)&&n>0;
    }
}
