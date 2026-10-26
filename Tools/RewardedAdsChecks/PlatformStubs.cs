// Deterministic external-boundary doubles; production source is compiled unchanged.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class MonoBehaviour { public bool isActiveAndEnabled=true; }
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogException(Exception exception) { }
    }
}
namespace PortCargo.Platform
{
    public static class WeChatPlatform { public static bool Ready; }
}
namespace WeChatWASM
{
    public class WXTextResponse { }
    public class WXADErrorResponse { }
    public class WXRewardedVideoAdOnCloseResponse { public bool isEnded; }
    public class WXCreateRewardedVideoAdParam { public string adUnitId; public bool multiton; }
    public sealed class WXRewardedVideoAd
    {
        public string id;
        public bool multiton,destroyed,throwLoad,throwShow,throwCleanup;
        public int loadCount,showCount,offCloseCount,offErrorCount,destroyCount;
        public Action<WXTextResponse> loadOk,showOk,showFailed;
        public Action<WXADErrorResponse> loadFailed,error;
        public Action<WXRewardedVideoAdOnCloseResponse> close;
        public void OnError(Action<WXADErrorResponse> action){error+=action;}
        public void OffError(Action<WXADErrorResponse> action)
        {offErrorCount++;if(throwCleanup)throw new Exception("off-error");error-=action;}
        public void OnClose(Action<WXRewardedVideoAdOnCloseResponse> action){close+=action;}
        public void OffClose(Action<WXRewardedVideoAdOnCloseResponse> action){offCloseCount++;close-=action;}
        public void Load(Action<WXTextResponse> success,Action<WXADErrorResponse> failed)
        {loadCount++;loadOk=success;loadFailed=failed;if(throwLoad)throw new Exception("load");}
        public void Show(Action<WXTextResponse> success,Action<WXTextResponse> failed)
        {showCount++;showOk=success;showFailed=failed;if(throwShow)throw new Exception("show");}
        public void Destroy()
        {destroyCount++;destroyed=true;if(throwCleanup)throw new Exception("destroy");}
    }
    public static class WX
    {
        public static readonly List<WXRewardedVideoAd> Created=new List<WXRewardedVideoAd>();
        public static int CreateAttemptCount;
        public static bool ThrowCreate,NullCreate,ThrowLoad,ThrowShow,ThrowCleanup;
        public static WXRewardedVideoAd CreateRewardedVideoAd(WXCreateRewardedVideoAdParam options)
        {
            CreateAttemptCount++;
            if(ThrowCreate)throw new Exception("create");
            if(NullCreate)return null;
            var ad=new WXRewardedVideoAd{id=options.adUnitId,multiton=options.multiton,
                throwLoad=ThrowLoad,throwShow=ThrowShow,throwCleanup=ThrowCleanup};
            Created.Add(ad);return ad;
        }
    }
}
