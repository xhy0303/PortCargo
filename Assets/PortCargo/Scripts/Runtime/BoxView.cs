using UnityEngine;
using UnityEngine.EventSystems;

namespace PortCargo.Runtime
{
    /// <summary>The same RectTransform is used to draw and hit-test a box. Pointer-up never picks a new box.</summary>
    public sealed class BoxView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private CargoGraphic graphic;
        private BoardView owner;
        public string BoxId { get; private set; }
        public string CargoType { get; private set; }
        public int StackIndex { get; private set; }
        public bool IsTop { get; private set; }
        public RectTransform Rect { get; private set; }
        private void Awake() { Rect = (RectTransform)transform; if (!graphic) graphic = GetComponent<CargoGraphic>(); }
        public void Bind(BoardView board, string id, string type, int stack, bool top)
        {
            owner = board; BoxId = id; CargoType = type; StackIndex = stack; IsTop = top;
            if (!Rect) Rect = (RectTransform)transform;
            graphic.Set(type, !top);
            transform.localScale = Vector3.one;
        }
        public void Highlight(bool value) => graphic.Set(CargoType, !IsTop, value);
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) owner?.Press(this,e); }
        public void OnPointerUp(PointerEventData e) => owner?.Release(this,e);
        public void OnDrag(PointerEventData e) => owner?.Drag(e);
    }
}
