using System;
using PortCargo.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PortCargo.Runtime
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private RectTransform stackArea;
        [SerializeField] private BoxView[] boxes;
        [SerializeField] private RectTransform[] bases;
        [SerializeField] private CargoGraphic[] bufferSlots;
        [SerializeField] private GameObject[] bufferFrames;
        private GameSession session;
        private BoxView pressed;
        private string pressedId, pressedRun;
        private long pressedVersion;
        private int pointer;
        private Vector2 pressPosition;
        private bool cancelled;
        private bool reducedMotion;
        private readonly float[] baseFlashes=new float[6];
        private UnityEngine.UI.Image[] baseImages;
        private void Awake(){baseImages=new UnityEngine.UI.Image[bases.Length];for(var i=0;i<bases.Length;i++)baseImages[i]=bases[i].GetComponent<UnityEngine.UI.Image>();}
        private bool tutorialPhase;
        private static readonly Color BaseColor=Color.white;
        public Action<string,string,long,Vector3> MoveRequested;
        public Action<int> CoveredRequested;
        public BoxView[] Boxes => boxes;

        public void Render(GameSession value, bool reduce)
        {
            session=value; reducedMotion=reduce;
            var board = value.Board; var level = value.Level;
            var n = level.Stacks.Count;
            var gap=stackArea.rect.width * .012f;
            var width=(stackArea.rect.width-gap*(n-1))/n;
            width=Mathf.Min(width,144);
            var stride=width+gap; var total=stride*n-gap;
            var depthStep=Mathf.Min(width*.9f, (stackArea.rect.height-width-14)/5f);
            var index=0;
            for(var s=0;s<6;s++)
            {
                bases[s].gameObject.SetActive(s<n);
                if(s>=n)continue;
                var x=-total/2+width/2+s*stride;
                bases[s].anchoredPosition=new Vector2(x,0);bases[s].sizeDelta=new Vector2(width+2,(width+2)/4);
                for(var b=0;b<board.heights[s];b++)
                {
                    var box=boxes[index++];var definition=level.Stacks[s].Boxes[b];
                    box.gameObject.SetActive(true);box.Bind(this,definition.Id,definition.Type,s,b==board.heights[s]-1);
                    box.Rect.sizeDelta=new Vector2(width,width);
                    box.Rect.anchoredPosition=new Vector2(x,11+b*depthStep);
                    box.transform.SetAsLastSibling();
                }
            }
            for(;index<boxes.Length;index++) boxes[index].gameObject.SetActive(false);
            for(var i=0;i<bufferSlots.Length;i++)
            {
                bufferFrames[i].SetActive(i<value.Capacity);
                bufferSlots[i].gameObject.SetActive(i<board.buffer.Count);
                if(i<board.buffer.Count){level.TryGetBox(board.buffer[i],out var box);bufferSlots[i].Set(box.Type);}
            }
        }

        public void Press(BoxView box, PointerEventData e)
        {
            if(pressed || session==null || session.Busy || session.Status==BoardStatus.Completed) return;
            pressed=box;pressedId=box.BoxId;pressedRun=session.RunId;pressedVersion=session.Version;
            pointer=e.pointerId;pressPosition=e.position;cancelled=false;
            if(!reducedMotion)box.transform.localScale=Vector3.one*.96f;
        }

        public void Drag(PointerEventData e)
        {
            if(pressed && e.pointerId==pointer && Vector2.Distance(e.position,pressPosition)>DragThreshold()) cancelled=true;
        }

        public void Release(BoxView box, PointerEventData e)
        {
            if(!pressed || e.pointerId!=pointer)return;
            var selected=pressed;
            var valid=selected==box && selected.BoxId==pressedId && !cancelled
                && Vector2.Distance(e.position,pressPosition)<=DragThreshold()
                && RectTransformUtility.RectangleContainsScreenPoint(selected.Rect,e.position,e.pressEventCamera);
            var id=pressedId;var run=pressedRun;var version=pressedVersion;var position=selected.transform.position;
            ResetPointer();
            if(!valid)return;
            if(!selected.IsTop){CoveredRequested?.Invoke(selected.StackIndex);return;}
            MoveRequested?.Invoke(id,run,version,position);
        }

        // Unity screen pixels already follow the render target. Platform adapter may supply CSS scale later.
        private float DragThreshold() => 12 * Mathf.Max(1,Screen.width/720f);
        public bool IsStackEmpty(int stack)=>session!=null&&stack>=0&&stack<session.Level.Stacks.Count&&session.Board.heights[stack]==0;
        public void PulseCleared(int stack){if(!reducedMotion&&stack>=0&&stack<bases.Length)baseFlashes[stack]=Time.unscaledTime+.22f;}
        private void Update()
        {
            for(var i=0;i<bases.Length;i++)baseImages[i].color=!reducedMotion&&baseFlashes[i]>Time.unscaledTime?new Color32(246,210,113,255):BaseColor;
            if(session==null||session.Level.Id!="L02"||session.FreeHintUsed||session.Busy)return;
            var phase=reducedMotion||((int)(Time.unscaledTime/.6f)%2==0);
            if(phase==tutorialPhase)return;tutorialPhase=phase;
            var b=session.Board;if(b.shipIndex>=session.Level.Ships.Count)return;
            var type=session.Level.Ships[b.shipIndex].Type;
            foreach(var box in boxes)if(box.gameObject.activeSelf&&box.IsTop&&box.CargoType==type)box.Highlight(phase);
        }
        public void ResetPointer(){if(pressed)pressed.transform.localScale=Vector3.one;pressed=null;}
        public void ClearHighlights(){foreach(var box in boxes)if(box.gameObject.activeSelf)box.Highlight(false);}
        public void Highlight(string id){ClearHighlights();foreach(var box in boxes)if(box.gameObject.activeSelf && box.BoxId==id)box.Highlight(true);}
        public void HighlightTop(int stack){foreach(var box in boxes)if(box.gameObject.activeSelf&&box.StackIndex==stack&&box.IsTop)box.Highlight(true);}
        public Vector3 BufferPosition(int index) => bufferSlots[Mathf.Clamp(index,0,3)].transform.position;
        private void OnDisable() => ResetPointer();
    }
}
