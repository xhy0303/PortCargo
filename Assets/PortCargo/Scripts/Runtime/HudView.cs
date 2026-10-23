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
        private void OnEnable()
        {
            undoButton.onClick.AddListener(OnUndo);hintButton.onClick.AddListener(OnHint);settingsButton.onClick.AddListener(OnSettings);
            queueButton.onClick.AddListener(OnQueue);retrySaveButton.onClick.AddListener(OnRetrySave);
        }
        private void OnDisable()
        {
            undoButton.onClick.RemoveListener(OnUndo);hintButton.onClick.RemoveListener(OnHint);settingsButton.onClick.RemoveListener(OnSettings);
            queueButton.onClick.RemoveListener(OnQueue);retrySaveButton.onClick.RemoveListener(OnRetrySave);
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
        }
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
