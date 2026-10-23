using UnityEngine;

namespace PortCargo.Runtime
{
    /// <summary>Serialized art references; no Resources loads or gameplay state.</summary>
    [CreateAssetMenu(menuName="PortCargo/Harbor Theme")]
    public sealed class HarborTheme : ScriptableObject
    {
        public Sprite[] sprites;
        public static readonly Color Ink=new Color32(239,245,255,255);
        public static readonly Color Muted=new Color32(177,196,220,255);
        public static readonly Color Dark=new Color32(20,35,65,255);
        public Sprite Find(string id)
        {
            if(sprites!=null)foreach(var sprite in sprites)if(sprite&&sprite.name==id)return sprite;
            return null;
        }
        public Sprite Cargo(string type,bool small=false)=>Find((small?"cargo_icon_":"cargo_")+type);
    }
}
