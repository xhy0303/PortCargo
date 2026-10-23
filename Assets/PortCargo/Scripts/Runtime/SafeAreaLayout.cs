using System;
using UnityEngine;

namespace PortCargo.Runtime
{
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        private Rect lastSafe;
        private int lastWidth,lastHeight;
        public event Action Changed;
        private void Update()
        {
            if(Screen.width<=0||Screen.height<=0)return;
            var value=PortCargo.Platform.WeChatPlatform.SafeAreaPixels();
            if(lastSafe==value&&lastWidth==Screen.width&&lastHeight==Screen.height)return;
            lastSafe=value;lastWidth=Screen.width;lastHeight=Screen.height;
            safeArea.anchorMin=new Vector2(value.xMin/Screen.width,value.yMin/Screen.height);
            safeArea.anchorMax=new Vector2(value.xMax/Screen.width,value.yMax/Screen.height);
            safeArea.offsetMin=Vector2.zero;safeArea.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();Changed?.Invoke();
        }
    }
}
