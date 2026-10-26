// External Unity/host boundary only. The production adapter and contract compile unchanged.
using System;
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled=true;
        public static implicit operator bool(MonoBehaviour value)=>value!=null;
    }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height){this.x=x;this.y=y;this.width=width;this.height=height;}
        public float xMin=>x;public float yMin=>y;public float xMax=>x+width;public float yMax=>y+height;
    }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Screen { public static int width=1080,height=2400; }
    public static class Debug
    {
        public static void Log(object message){}
        public static void LogWarning(object message){}
        public static void LogException(Exception exception){}
    }
    public static class Mathf
    {
        public static float Abs(float value)=>Math.Abs(value);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
    }
}
namespace PortCargo.Platform
{
    public static class WeChatPlatform
    {
        public static bool Ready,Hidden;
        public static event Action<bool> VisibilityChanged;
        public static void SetVisibility(bool hidden){Hidden=hidden;VisibilityChanged?.Invoke(hidden);}
    }
}
namespace WeChatWASM
{
    public sealed class WindowInfo { public double windowWidth=360,windowHeight=800; }
    public static class WX { public static WindowInfo GetWindowInfo()=>new WindowInfo(); }
}
namespace PortCargo.Runtime
{
    // Navigation boundary reproduces the production flow's synchronous invalid-then-valid events.
    public sealed class GameFlowController : UnityEngine.MonoBehaviour
    {
        private bool hidden,transitioning,homeAvailable,completedAvailable,pauseAvailable,settingsAvailable,gridAvailable;
        public PortCargo.Core.NativeAdPlacement Placement=PortCargo.Core.NativeAdPlacement.Home;
        public UnityEngine.Rect Bounds=new UnityEngine.Rect(60,900,960,336);
        public event Action NativeAdPlacementChanged;
        public void SetNativeAdAvailability(bool home,bool completed,bool pause=false,bool settings=false,bool grid=false)
        {
            if(home==homeAvailable&&completed==completedAvailable&&pause==pauseAvailable&&settings==settingsAvailable&&grid==gridAvailable)return;
            transitioning=true;NativeAdPlacementChanged?.Invoke();homeAvailable=home;completedAvailable=completed;
            pauseAvailable=pause;settingsAvailable=settings;gridAvailable=grid;
            transitioning=false;NativeAdPlacementChanged?.Invoke();
        }
        public void SetNativeAdsHidden(bool value)
        {
            if(hidden==value)return;
            transitioning=true;NativeAdPlacementChanged?.Invoke();hidden=value;
            transitioning=false;NativeAdPlacementChanged?.Invoke();
        }
        public bool TryGetNativeAdPlacement(out PortCargo.Core.NativeAdPlacement placement,out UnityEngine.Rect rect)
        {
            placement=PortCargo.Core.NativeAdPlacement.None;rect=default;
            if(hidden||transitioning||!isActiveAndEnabled)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.Home&&!homeAvailable)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.Completed&&!completedAvailable)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.Pause&&!pauseAvailable)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.Settings&&!settingsAvailable)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.GameplayGrid&&!gridAvailable)return false;
            if(Placement==PortCargo.Core.NativeAdPlacement.None)return false;
            placement=Placement;rect=Bounds;return true;
        }
        public void Navigate(PortCargo.Core.NativeAdPlacement placement)
        {
            transitioning=true;NativeAdPlacementChanged?.Invoke();Placement=placement;
            transitioning=false;NativeAdPlacementChanged?.Invoke();
        }
        public void PublishLifecycleState()=>NativeAdPlacementChanged?.Invoke();
    }
}
