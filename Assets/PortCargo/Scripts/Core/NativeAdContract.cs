using System;

namespace PortCargo.Core
{
    public enum NativeAdPlacement { None, Home, Completed, Pause, Settings, GameplayGrid }

    /// <summary>Unknown native sizes never inherit another template's aspect ratio.</summary>
    public enum NativeAdSizing { RequireMeasurement, Horizontal20x7, VerifiedFixed }

    /// <summary>A measured conservative envelope for one exact configured template/ad unit.
    /// It is not a claim that unrelated CustomAd templates have the same dimensions.</summary>
    public readonly struct NativeAdTemplateSize
    {
        public readonly string AdUnitId;
        public readonly float Width,Height;
        public NativeAdTemplateSize(string adUnitId,float width,float height)
        {AdUnitId=adUnitId?.Trim();Width=width;Height=height;}
        public bool IsValidFor(string adUnitId)
        {return !string.IsNullOrEmpty(adUnitId)&&string.Equals(AdUnitId,adUnitId,StringComparison.Ordinal)
            &&NativeAdBounds.Finite(Width)&&NativeAdBounds.Finite(Height)&&Width>0&&Height>0;}
    }

    /// <summary>The whole reserved slot, in native CSS pixels with a top-left origin.</summary>
    public readonly struct NativeAdBounds
    {
        public readonly float Left,Top,Width,Height;
        public NativeAdBounds(float left,float top,float width,float height)
        {Left=left;Top=top;Width=width;Height=height;}
        public bool IsValid => Finite(Left)&&Finite(Top)&&Finite(Width)&&Finite(Height)
            &&Left>=0&&Top>=0&&Width>0&&Height>0;
        public bool Fits(float width,float height)
        {return IsValid&&Finite(width)&&Finite(height)&&width>0&&height>0&&width<=Width+.5f&&height<=Height+.5f;}
        public bool SameAs(NativeAdBounds other)
        {return Left==other.Left&&Top==other.Top&&Width==other.Width&&Height==other.Height;}
        public static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }

    public struct NativeAdSnapshot
    {
        public bool Loaded,Closed,Failed,Visible,HasSize;
        public float Width,Height;
    }

    /// <summary>One native object per handle; Read never loads, refreshes or shows ads.</summary>
    public interface INativeAdBackend
    {
        bool IsSupported {get;}
        int Create(string adUnitId,NativeAdBounds bounds,NativeAdSizing sizing,NativeAdTemplateSize verifiedSize);
        NativeAdSnapshot Read(int handle);
        void Show(int handle);
        void Hide(int handle);
        void Destroy(int handle);
    }
}
