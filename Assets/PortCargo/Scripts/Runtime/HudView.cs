using System;
using PortCargo.Core;
using TMPro;
using UnityEngine;

namespace PortCargo.Runtime
{
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel, shipCount, nextLabel, instruction, toast, chain;
        [SerializeField] private CargoGraphic currentCargo;
        [SerializeField] private UnityEngine.UI.Button undoButton, hintButton, settingsButton, queueButton, retrySaveButton;
        [SerializeField] private RectTransform shipTarget;
        [SerializeField] private CargoGraphic[] nextCargo;
        [SerializeField] private TMP_Text[] nextCounts;
        public event Action Undo, Hint, Settings, Queue, RetrySave;
        public Vector3 ShipPosition => shipTarget.position;
        private float toastUntil, chainUntil;
        private string tutorialType;private bool tutorialPhase,reduced;
        private bool nativeAdAvailable,nativeGridConfigured,nativeLayoutActive,nativeGeometryCaptured;
        private RectTransform nativeAdSlot,shipRoot;
        private LayoutSnapshot levelLayout,shipLayout,chainLayout;
        public RectTransform NativeAdSlot => nativeAdSlot;
        public bool NativeAdReserved {get;private set;}
        // This envelope was measured for the configured single-grid template, independently
        // of the 20:7 card. Loading an ad never changes the geometry of the game beneath it.
        private const float GridWidthCss=82,GridHeightCss=122,GridInsetCss=6,GridGapCss=6;
        private struct LayoutSnapshot
        {
            public Vector2 min,max,pivot,position,size;
            public LayoutSnapshot(RectTransform r){min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;position=r.anchoredPosition;size=r.sizeDelta;}
            public void Restore(RectTransform r){r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;}
        }
        public void SetNativeAdAvailable(bool available)
        {
            nativeAdAvailable=available;
            // Retain the chosen layout through ad errors, hidden states and page changes.
            // This only arranges header art; no board or control hitbox is ever moved.
            nativeGridConfigured|=available;
            RelayoutNativeAd();
        }
        private void OnEnable()
        {
            undoButton.onClick.AddListener(OnUndo);hintButton.onClick.AddListener(OnHint);settingsButton.onClick.AddListener(OnSettings);
            queueButton.onClick.AddListener(OnQueue);retrySaveButton.onClick.AddListener(OnRetrySave);
            RelayoutNativeAd();
        }
        private void OnDisable()
        {
            undoButton.onClick.RemoveListener(OnUndo);hintButton.onClick.RemoveListener(OnHint);settingsButton.onClick.RemoveListener(OnSettings);
            queueButton.onClick.RemoveListener(OnQueue);retrySaveButton.onClick.RemoveListener(OnRetrySave);
            NativeAdReserved=false;if(nativeAdSlot)nativeAdSlot.gameObject.SetActive(false);
        }
        private void OnUndo()=>Undo?.Invoke();private void OnHint()=>Hint?.Invoke();private void OnSettings()=>Settings?.Invoke();
        private void OnQueue()=>Queue?.Invoke();private void OnRetrySave()=>RetrySave?.Invoke();
        public void Render(GameSession session,int index,bool savePending,bool reduce=false,int endlessRound=0)
        {
            var b=session.Board;var l=session.Level;reduced=reduce;
            tutorialType=endlessRound==0&&index==1&&b.shipIndex<l.Ships.Count?l.Ships[b.shipIndex].Type:null;
            levelLabel.text=endlessRound>0?"无尽模式  /  "+endlessRound:"港口装箱  /  "+(index+1).ToString("00");
            if(b.shipIndex<l.Ships.Count)
            {
                var ship=l.Ships[b.shipIndex];currentCargo.gameObject.SetActive(true);currentCargo.Set(ship.Type);
                shipCount.text=b.shipLoad+" / "+ship.Capacity;
                var preview="";
                for(var i=b.shipIndex+1;i<Math.Min(l.Ships.Count,b.shipIndex+3);i++)
                    preview+=(preview.Length==0?"接下来  ":"    ")+TypeName(l.Ships[i].Type)+" ×"+l.Ships[i].Capacity;
                nextLabel.text=preview.Length==0?"最后一船":preview;
            }
            else { currentCargo.gameObject.SetActive(false);shipCount.text="全部装满";nextLabel.text="码头已清空"; }
            if(nextCargo!=null&&nextCargo.Length==2)
            {
                var count=0;
                for(var i=0;i<2;i++)
                {
                    var at=b.shipIndex+1+i;var exists=at<l.Ships.Count;
                    nextCargo[i].transform.parent.gameObject.SetActive(exists);
                    if(exists){nextCargo[i].Set(l.Ships[at].Type);nextCounts[i].text="× "+l.Ships[at].Capacity;count++;}
                }
                if(count>0)nextLabel.text="";
            }
            instruction.text=endlessRound==0&&index==0 && session.Export().moveCount==0?"点亮起的箱子":"";
            undoButton.interactable=session.CanUndo;hintButton.interactable=!session.Busy&&session.Status!=BoardStatus.Completed;
            retrySaveButton.gameObject.SetActive(savePending);
            RelayoutNativeAd();
        }
        public void RelayoutNativeAd()
        {
            if(nativeLayoutActive||!nativeGridConfigured||!levelLabel||!currentCargo||!chain||!settingsButton)return;
            nativeLayoutActive=true;
            try
            {
                var root=(RectTransform)transform;
                NativeAdReserved=false;
                if(!nativeGeometryCaptured)
                {
                    shipRoot=currentCargo.transform.parent as RectTransform;
                    if(!shipRoot||shipRoot.parent!=root)return;
                    levelLayout=new LayoutSnapshot(levelLabel.rectTransform);shipLayout=new LayoutSnapshot(shipRoot);chainLayout=new LayoutSnapshot(chain.rectTransform);
                    nativeGeometryCaptured=true;
                }
                levelLayout.Restore(levelLabel.rectTransform);shipLayout.Restore(shipRoot);chainLayout.Restore(chain.rectTransform);
                if(!NativeAdPlacementLayout.TryGetUnitsPerCssPixel(root,out var units)||Screen.width>=Screen.height)return;
                var inset=GridInsetCss*units;var gap=GridGapCss*units;
                var reserved=new Rect(root.rect.xMin+inset,root.rect.yMax-inset-GridHeightCss*units,GridWidthCss*units,GridHeightCss*units);
                if(reserved.xMax>root.rect.xMax-inset||reserved.yMin<root.rect.yMin+inset)return;
                var pauseBounds=LocalBounds(root,(RectTransform)settingsButton.transform);
                var titleLeft=reserved.xMax+gap;var titleRight=pauseBounds.xMin-gap;
                // Keep enough room for the existing readable level title and leave pause untouched.
                if(titleRight-titleLeft<130*units)return;
                var titleRect=levelLabel.rectTransform;
                titleRect.anchorMin=new Vector2(.5f,titleRect.anchorMin.y);titleRect.anchorMax=new Vector2(.5f,titleRect.anchorMax.y);
                titleRect.pivot=new Vector2(.5f,titleRect.pivot.y);
                titleRect.anchoredPosition=new Vector2((titleLeft+titleRight)*.5f,titleRect.anchoredPosition.y);
                titleRect.sizeDelta=new Vector2(titleRight-titleLeft,titleRect.sizeDelta.y);
                var shipBounds=LocalBounds(root,shipRoot);
                var shipShift=Mathf.Max(0,reserved.xMax+gap-shipBounds.xMin);
                if(shipBounds.xMax+shipShift>root.rect.xMax-inset){levelLayout.Restore(titleRect);return;}
                shipRoot.anchoredPosition+=new Vector2(shipShift,0);
                var chainBounds=LocalBounds(root,chain.rectTransform);
                var chainShift=Mathf.Max(shipShift,reserved.xMax+gap-chainBounds.xMin);
                chain.rectTransform.anchoredPosition+=new Vector2(chainShift,0);
                nativeAdSlot=NativeAdPlacementLayout.EnsureSlot(root,nativeAdSlot);
                nativeAdSlot.anchorMin=nativeAdSlot.anchorMax=nativeAdSlot.pivot=new Vector2(.5f,.5f);
                nativeAdSlot.anchoredPosition=reserved.center;nativeAdSlot.sizeDelta=reserved.size;
                // All transient labels and every buffer/stack region stay protected even
                // when empty; a toast, a later ship or a new board can never overlap the ad.
                NativeAdReserved=nativeAdAvailable&&HasClearGridEnvelope(root,reserved,gap);
            }
            finally
            {
                // Readability plates are authored as siblings, not label children.
                // Keep the decorative title backing aligned with its resized text area.
                var tracking=levelLabel.GetComponent<HarborTextPlate>();
                if(tracking&&tracking.plate)new LayoutSnapshot(levelLabel.rectTransform).Restore(tracking.plate.rectTransform);
                if(nativeAdSlot)nativeAdSlot.gameObject.SetActive(NativeAdReserved);nativeLayoutActive=false;
            }
        }
        private bool HasClearGridEnvelope(RectTransform root,Rect reserved,float gap)
        {
            var protectedRects=new RectTransform[]{levelLabel.rectTransform,shipRoot,nextLabel.rectTransform,instruction.rectTransform,toast.rectTransform,chain.rectTransform,
                (RectTransform)undoButton.transform,(RectTransform)hintButton.transform,(RectTransform)settingsButton.transform,
                (RectTransform)queueButton.transform,(RectTransform)retrySaveButton.transform,root.Find("BufferTitle") as RectTransform};
            foreach(var rect in protectedRects)if(rect&&Touches(root,rect,reserved,gap))return false;
            if(nextCargo!=null)foreach(var cargo in nextCargo)if(cargo&&Touches(root,cargo.transform.parent as RectTransform,reserved,gap))return false;
            var board=root.parent?root.parent.Find("BoardLayer"):null;
            if(!board)return false;
            var stacks=board.Find("Stacks") as RectTransform;var buffer=board.Find("Buffer") as RectTransform;
            if(!stacks||!buffer||Touches(root,stacks,reserved,gap)||Touches(root,buffer,reserved,gap))return false;
            foreach(RectTransform child in buffer)if(Touches(root,child,reserved,gap))return false;
            return true;
        }
        private static bool Touches(RectTransform root,RectTransform rect,Rect candidate,float gap)
        {
            if(!rect)return true;
            var bounds=LocalBounds(root,rect);bounds.xMin-=gap;bounds.yMin-=gap;bounds.xMax+=gap;bounds.yMax+=gap;
            return candidate.Overlaps(bounds);
        }
        private static Rect LocalBounds(RectTransform root,RectTransform rect)
        {
            var points=new Vector3[4];rect.GetWorldCorners(points);
            var a=root.InverseTransformPoint(points[0]);var b=root.InverseTransformPoint(points[2]);
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        private void OnRectTransformDimensionsChange(){RelayoutNativeAd();}
        public static string TypeName(string type)=>type=="R"?"条纹":type=="B"?"圆点":type=="G"?"斜线":"十字";
        public void Toast(string value){toast.text=value;toastUntil=Time.unscaledTime+2.2f;}
        public void Chain(int count){chain.text=count>=2?"×"+count:"满载出港";chainUntil=Time.unscaledTime+.45f;}
        public void ClearTransient(){toast.text="";chain.text="";}
        private void Update()
        {
            if(tutorialType!=null){var phase=reduced||((int)(Time.unscaledTime/.6f)%2==0);if(phase!=tutorialPhase){tutorialPhase=phase;currentCargo.Set(tutorialType,false,phase);}}
            if(toastUntil>0&&Time.unscaledTime>=toastUntil){toast.text="";toastUntil=0;}
            if(chainUntil>0&&Time.unscaledTime>=chainUntil){chain.text="";chainUntil=0;}
        }
    }
}
