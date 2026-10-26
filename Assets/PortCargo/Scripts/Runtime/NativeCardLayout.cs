using UnityEngine;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace PortCargo.Runtime
{
    /// <summary>Geometry for the configured 20:7 native cards; all output rects use Unity's bottom-left screen origin.</summary>
    public static class NativeAdPlacementLayout
    {
        public const float Aspect=20f/7f;
        public const float Gap=32;
        // The native CSS dimensions are integer-rounded independently of Canvas scaling.
        public const float RoundingSpace=12;
        // Measured with the configured card in WeChat: a 260 CSS request is clamped to 300.
        // These are this project's card choices, not universal CustomAd platform limits.
        public const float CardWidthCss=300;
        private static readonly Vector3[] corners=new Vector3[4];

        public static bool TryGetUnitsPerCssPixel(RectTransform parent,out float units)
        {
            units=0;
            if(!parent||parent.rect.width<=0||!TryGetScreenRect(parent,out var bounds))return false;
            var windowWidth=(float)Screen.width;
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            try {windowWidth=(float)WX.GetWindowInfo().windowWidth;}catch(System.Exception){return false;}
#endif
            if(windowWidth<=0||Screen.width<=0)return false;
            units=parent.rect.width/bounds.width*Screen.width/windowWidth;
            return units>0&&!float.IsNaN(units)&&!float.IsInfinity(units);
        }

        public static bool TryGetCardSize(RectTransform parent,out float width,out float height)
        {
            width=height=0;
            if(!TryGetUnitsPerCssPixel(parent,out var units))return false;
            // Two CSS pixels absorb inward integer rounding of independently mapped edges.
            width=(CardWidthCss+2)*units;
            height=((CardWidthCss+2)/Aspect+6)*units;
            return width<=parent.rect.width&&CanReserve(parent,width,height);
        }

        public static bool CanReserve(RectTransform parent,float width,float height)
        {
            // These are project layout limits, not platform ad eligibility rules.
            if(Screen.width<320||Screen.height<560||Screen.width>=Screen.height
                ||parent==null||parent.rect.width<=0||width<=0||height<=0)return false;
            if(!TryGetScreenRect(parent,out var bounds))return false;
            if(!TryGetUnitsPerCssPixel(parent,out var units))return false;
            return width/units>=240&&height/units>=84;
        }

        public static RectTransform EnsureSlot(RectTransform parent,RectTransform existing)
        {
            if(existing)return existing;
            var child=parent.Find("NativeAdSlot") as RectTransform;
            if(child)return child;
            var created=new GameObject("NativeAdSlot",typeof(RectTransform));
            var rect=(RectTransform)created.transform;rect.SetParent(parent,false);
            // No Graphic/RaycastTarget: an empty or failed native ad never blocks Unity input.
            return rect;
        }

        public static bool TryGetScreenRect(RectTransform rect,out Rect result)
        {
            result=default;
            if(!rect||!rect.gameObject.activeInHierarchy)return false;
            var canvas=rect.GetComponentInParent<Canvas>();
            var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            rect.GetWorldCorners(corners);
            var bottomLeft=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
            var topRight=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            result=Rect.MinMaxRect(bottomLeft.x,bottomLeft.y,topRight.x,topRight.y);
            return result.width>0&&result.height>0&&result.xMin>=-.5f&&result.yMin>=-.5f
                &&result.xMax<=Screen.width+.5f&&result.yMax<=Screen.height+.5f;
        }
    }
}
