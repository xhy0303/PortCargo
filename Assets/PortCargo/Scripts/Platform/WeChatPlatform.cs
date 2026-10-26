using System;
using System.Runtime.InteropServices;
using PortCargo.Core;
using PortCargo.Runtime;
using UnityEngine;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace PortCargo.Platform
{
    /// <summary>Native synchronous storage, bypassing the SDK's asynchronous write-behind cache.</summary>
    public sealed class WeChatStoragePort : IStoragePort
    {
        [Serializable] private sealed class ReadResult { public bool ok, present; public string value, error; }
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string PortCargoStorageRead(string key);
        [DllImport("__Internal")] private static extern string PortCargoStorageWrite(string key,string value);
#endif
        public string ReadString(string key)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var result=JsonUtility.FromJson<ReadResult>(PortCargoStorageRead(key));
            if(result==null||!result.ok)throw new InvalidOperationException("WECHAT_STORAGE_READ: "+result?.error);
            return result.present?result.value:null;
#else
            throw new PlatformNotSupportedException("WeChat storage requires a WeChat player.");
#endif
        }
        public void WriteString(string key,string value)
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var error=PortCargoStorageWrite(key,value);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException("WECHAT_STORAGE_WRITE: "+error);
#else
            throw new PlatformNotSupportedException("WeChat storage requires a WeChat player.");
#endif
        }
    }

    public sealed class WeChatPlatform : MonoBehaviour
    {
        public static bool Ready {get; private set;}
        public static bool Hidden {get; private set;}
        public static event Action<bool> VisibilityChanged;
        private GamePresenter presenter;
        private Action initialized;
        private Action<Exception> failed;
        private bool starting,subscribed;
        private float deadline;
        private static Rect normalizedSafe=new Rect(0,0,1,1);
        public void Initialize(GamePresenter view,Action done,Action<Exception> error)
        {
            presenter=view;initialized=done;failed=error;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(starting)return;
            if(Ready){done();return;}
            starting=true;deadline=Time.realtimeSinceStartup+20;
            WX.InitSDK(code=>{
                if(!starting)return;
                starting=false;
                try
                {
                    if(code!=200)throw new InvalidOperationException("WECHAT_INIT_"+code);
                    WX.OnHide(OnHide);WX.OnShow(OnShow);subscribed=true;
                    WX.SetPreferredFramesPerSecond(30);
                    RefreshSafeArea();Ready=true;
                    Debug.Log("PORTCARGO_WECHAT_READY");initialized();
                }
                catch(Exception ex){failed(ex);}
            });
#else
            done();
#endif
        }
        private void Update()
        {
            if(starting&&Time.realtimeSinceStartup>deadline){starting=false;failed(new TimeoutException("WECHAT_INIT_TIMEOUT"));}
        }
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
        private void OnHide(GeneralCallbackResult _) { Hidden=true;NotifyVisibility(true);if(presenter)presenter.HandleHidden(); }
        private void OnShow(OnShowListenerResult _) { RefreshSafeArea();Hidden=false;if(presenter)presenter.HandleShown();NotifyVisibility(false); }
        private static void NotifyVisibility(bool hidden)
        {
            var listeners=VisibilityChanged;if(listeners==null)return;
            foreach(Action<bool> listener in listeners.GetInvocationList())
                try{listener(hidden);}catch(Exception ex){Debug.LogWarning("PORTCARGO_VISIBILITY "+ex.GetType().Name);}
        }
        private static void RefreshSafeArea()
        {
            var info=WX.GetWindowInfo();var capsule=WX.GetMenuButtonBoundingClientRect();
            if(info.windowWidth<=0||info.windowHeight<=0)return;
            var area=info.safeArea;
            var left=area==null?0:area.left;
            var right=area==null?info.windowWidth:area.right;
            var top=area==null?0:area.top;
            var bottom=area==null?info.windowHeight:area.bottom;
            if(capsule!=null&&capsule.height>0)top=Math.Max(top,capsule.bottom+8);
            normalizedSafe=Rect.MinMaxRect(Mathf.Clamp01((float)(left/info.windowWidth)),Mathf.Clamp01((float)(1-bottom/info.windowHeight)),Mathf.Clamp01((float)(right/info.windowWidth)),Mathf.Clamp01((float)(1-top/info.windowHeight)));
        }
#endif
        public static Rect SafeAreaPixels()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(Ready)return new Rect(normalizedSafe.x*Screen.width,normalizedSafe.y*Screen.height,normalizedSafe.width*Screen.width,normalizedSafe.height*Screen.height);
#endif
            return Screen.safeArea;
        }
        public static void Exit()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            WX.ExitMiniProgram(new ExitMiniProgramOption());
#else
            Application.Quit();
#endif
        }
        private void OnDestroy()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            if(subscribed){WX.OffHide(OnHide);WX.OffShow(OnShow);}
#endif
            Ready=false;
            Hidden=false;
        }
    }
}
