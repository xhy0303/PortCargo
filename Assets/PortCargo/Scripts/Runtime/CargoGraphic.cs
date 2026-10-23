using UnityEngine;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    /// <summary>Shared sprite-backed cargo with geometry fallback; the same rect owns rendering and input.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CargoGraphic : MaskableGraphic
    {
        [SerializeField] private HarborTheme theme;
        [SerializeField] private bool smallIcon;
        [SerializeField] private Image highlightFrame;
        public override Texture mainTexture => ArtSprite?ArtSprite.texture:base.mainTexture;
        private Sprite ArtSprite=>theme?theme.Cargo(cargoType,smallIcon):null;
        protected override void OnEnable(){base.OnEnable();UpdateFrame();}
        private void UpdateFrame(){if(highlightFrame)highlightFrame.gameObject.SetActive(highlighted);}
        [SerializeField] private string cargoType = "R";
        [SerializeField] private bool highlighted;
        [SerializeField] private bool dimmed;
        public void Set(string type, bool dim = false, bool highlight = false)
        {
            if (cargoType == type && dimmed == dim && highlighted == highlight) return;
            cargoType = type; dimmed = dim; highlighted = highlight; SetVerticesDirty();SetMaterialDirty();UpdateFrame();
        }
        public static Color CargoColor(string type)
        {
            switch (type) {
                case "B": return new Color32(61, 138, 193, 255);
                case "G": return new Color32(43, 148, 119, 255);
                case "Y": return new Color32(221, 161, 47, 255);
                default: return new Color32(220, 105, 86, 255);
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            var sprite=ArtSprite;
            if(sprite)
            {
                // Square assets retain their aspect inside any legacy slot.
                var side=Mathf.Min(r.width,r.height);var x=r.center.x-side/2;var y=r.center.y-side/2;
                var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                var spriteTint=dimmed?new Color(.57f,.62f,.72f,1):Color.white;
                vh.AddVert(new Vector3(x,y),spriteTint,new Vector2(uv.x,uv.y));
                vh.AddVert(new Vector3(x,y+side),spriteTint,new Vector2(uv.x,uv.w));
                vh.AddVert(new Vector3(x+side,y+side),spriteTint,new Vector2(uv.z,uv.w));
                vh.AddVert(new Vector3(x+side,y),spriteTint,new Vector2(uv.z,uv.y));
                vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);return;
            }
            var edge = highlighted ? new Color32(249, 199, 73, 255) : new Color32(26, 60, 76, 255);
            Quad(vh, r.xMin, r.yMin, r.width, r.height, edge);
            var tint = CargoColor(cargoType);
            if (dimmed) tint = Color.Lerp(tint, new Color(.64f, .70f, .70f), .42f);
            var border = highlighted ? 5f : 2f;
            Quad(vh, r.xMin + border, r.yMin + border, r.width - border * 2, r.height - border * 2, tint);
            var ink = new Color(1, 1, 1, dimmed ? .46f : .82f);
            for (var i = 0; i < 3; i++)
            {
                var x = r.xMin + r.width * (.25f + i * .25f);
                var y = r.yMin + r.height * .43f;
                if (cargoType == "B") Circle(vh, x, y, Mathf.Min(r.width, r.height) * .065f, ink);
                else if (cargoType == "G") Line(vh, new Vector2(x - 6, y - 9), new Vector2(x + 6, y + 9), 4, ink);
                else if (cargoType == "Y") { Quad(vh, x - 2, y - 9, 4, 18, ink); Quad(vh, x - 9, y - 2, 18, 4, ink); }
                else Quad(vh, x - 2, y - 12, 4, 24, ink);
            }
            Quad(vh, r.xMin + 8, r.yMax - 10, r.width - 16, 3, new Color(1, 1, 1, .28f));
        }
        private static void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            var n = vh.currentVertCount;
            vh.AddVert(new Vector3(x,y),c,Vector2.zero); vh.AddVert(new Vector3(x,y+h),c,Vector2.zero);
            vh.AddVert(new Vector3(x+w,y+h),c,Vector2.zero); vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
        private static void Circle(VertexHelper vh, float x, float y, float radius, Color c)
        {
            var n = vh.currentVertCount; vh.AddVert(new Vector3(x,y),c,Vector2.zero);
            for(var i=0;i<=12;i++){var a=i*Mathf.PI*2/12;vh.AddVert(new Vector3(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),c,Vector2.zero);}
            for(var i=0;i<12;i++) vh.AddTriangle(n,n+i+1,n+i+2);
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var offset=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            var n=vh.currentVertCount;
            vh.AddVert(a-offset,c,Vector2.zero);vh.AddVert(a+offset,c,Vector2.zero);
            vh.AddVert(b+offset,c,Vector2.zero);vh.AddVert(b-offset,c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
