using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    /// <summary>A replaceable home view: labels, layout and intent events only.</summary>
    public sealed class HomeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title,tagline,progress,endlessStatus,noticeLabel;
        [SerializeField] private RectTransform harborPreview;
        [SerializeField] private Button campaignButton,endlessButton,levelsButton,helpButton,settingsButton,quitButton,retryButton;
        public event Action Campaign,Endless,Levels,Help,Settings,Quit,RetrySave;
        public bool Visible => gameObject.activeSelf;
        private void OnEnable()
        {
            campaignButton.onClick.AddListener(OnCampaign);endlessButton.onClick.AddListener(OnEndless);
            levelsButton.onClick.AddListener(OnLevels);helpButton.onClick.AddListener(OnHelp);settingsButton.onClick.AddListener(OnSettings);
            quitButton.onClick.AddListener(OnQuit);retryButton.onClick.AddListener(OnRetry);
            Relayout();
        }
        private void OnDisable()
        {
            campaignButton.onClick.RemoveListener(OnCampaign);endlessButton.onClick.RemoveListener(OnEndless);
            levelsButton.onClick.RemoveListener(OnLevels);helpButton.onClick.RemoveListener(OnHelp);settingsButton.onClick.RemoveListener(OnSettings);
            quitButton.onClick.RemoveListener(OnQuit);retryButton.onClick.RemoveListener(OnRetry);
        }
        private void OnCampaign()=>Campaign?.Invoke();private void OnEndless()=>Endless?.Invoke();
        private void OnLevels()=>Levels?.Invoke();private void OnHelp()=>Help?.Invoke();private void OnSettings()=>Settings?.Invoke();
        private void OnQuit()=>Quit?.Invoke();private void OnRetry()=>RetrySave?.Invoke();
        public void Show(int completed,int total,string campaignLabel,string endlessLabel,string endlessProgress,bool savePending,string notice="")
        {
            gameObject.SetActive(true);
            title.text="港口装箱";tagline.text="先取顶部 · 同类装船 · 其余暂存";
            progress.text="闯关进度  "+completed+" / "+total;
            campaignButton.GetComponentInChildren<TMP_Text>().text=campaignLabel;
            endlessButton.GetComponentInChildren<TMP_Text>().text=endlessLabel;
            endlessStatus.text=endlessProgress;noticeLabel.text=notice;
            retryButton.gameObject.SetActive(savePending);
            if(harborPreview)harborPreview.gameObject.SetActive(!savePending);
            Relayout();
        }
        public void Hide()=>gameObject.SetActive(false);
        private void OnRectTransformDimensionsChange(){if(title)Relayout();}
        private void Relayout()
        {
            var root=(RectTransform)transform;var width=Mathf.Min(600,root.rect.width-60);var column=(width-20)/2;
            Place(title.rectTransform,.91f,width,104);Place(tagline.rectTransform,.845f,width,48);
            if(harborPreview)Place(harborPreview,.625f,root.rect.width,root.rect.height*.34f);
            Place(progress.rectTransform,.43f,width,50);
            Place((RectTransform)campaignButton.transform,.355f,width,108);
            Place((RectTransform)endlessButton.transform,.26f,width,100);
            Place(endlessStatus.rectTransform,.20f,width,38);
            var navWidth=(width-36)/4;
            Place((RectTransform)levelsButton.transform,.095f,navWidth,110,-1.5f*(navWidth+12));
            Place((RectTransform)settingsButton.transform,.095f,navWidth,110,-.5f*(navWidth+12));
            Place((RectTransform)helpButton.transform,.095f,navWidth,110,.5f*(navWidth+12));
            Place((RectTransform)quitButton.transform,.095f,navWidth,110,1.5f*(navWidth+12));
            Place(noticeLabel.rectTransform,.785f,width,48);
            Place((RectTransform)retryButton.transform,.645f,width,100);
        }
        private static void Place(RectTransform rect,float y,float width,float height,float x=0)
        {rect.anchorMin=rect.anchorMax=new Vector2(.5f,y);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(width,height);}
    }
}
