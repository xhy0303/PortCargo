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
        private bool nativeAdAvailable,savePending,relayoutActive;
        private RectTransform nativeAdSlot,backdrop;
        private readonly Vector3[] backdropCorners=new Vector3[4];
        public RectTransform NativeAdSlot => nativeAdSlot;
        public bool NativeAdReserved {get;private set;}
        public void SetNativeAdAvailable(bool available)
        {if(nativeAdAvailable==available)return;nativeAdAvailable=available;Relayout();}
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
            this.savePending=savePending;
            gameObject.SetActive(true);
            title.text="港口装箱";tagline.text="先取顶部 · 同类装船 · 其余暂存";
            progress.text="闯关进度  "+completed+" / "+total;
            campaignButton.GetComponentInChildren<TMP_Text>().text=campaignLabel;
            endlessButton.GetComponentInChildren<TMP_Text>().text=endlessLabel;
            // The generic mode description is already explained in Help. Keep actual saved progress.
            endlessStatus.text=endlessLabel=="开启无尽"?"":endlessProgress;noticeLabel.text=notice;
            retryButton.gameObject.SetActive(savePending);
            if(harborPreview)harborPreview.gameObject.SetActive(!savePending);
            Relayout();
        }
        public void Hide()=>gameObject.SetActive(false);
        private void OnRectTransformDimensionsChange(){if(title)Relayout();}
        private void Relayout()
        {
            if(relayoutActive||!title)return;
            relayoutActive=true;
            try
            {
            var root=(RectTransform)transform;
            if(root.rect.width<=0||root.rect.height<=0)return;
            var units=1f;
            if(NativeAdPlacementLayout.TryGetUnitsPerCssPixel(root,out var cssUnits))units=cssUnits;
            var uiScale=Mathf.Clamp(root.rect.width/units/390f,1,1.25f);
            var u=units*uiScale;
            var width=Mathf.Min(320*u,root.rect.width-40*u);
            var height=root.rect.height;
            var compact=height/u<680;
            var tight=height/u<580;
            var navHeight=(tight?44:48)*u;
            var statusHeight=string.IsNullOrEmpty(endlessStatus.text)?0:20*u;
            var artTop=height-(tight?56:88)*u;
            if(!savePending&&!string.IsNullOrEmpty(noticeLabel.text))artTop-=40*u;
            EnsureBackdrop(root);

            title.fontSize=(tight?28:32)*u;tagline.fontSize=12*u;
            tagline.gameObject.SetActive(!tight);
            progress.fontSize=13*u;progress.color=HarborTheme.Muted;
            endlessStatus.fontSize=12*u;noticeLabel.fontSize=13*u;
            PlaceFromBottom(title.rectTransform,height-(tight?48:54)*u,width,(tight?36:42)*u,height);
            PlaceFromBottom(tagline.rectTransform,height-74*u,width,18*u,height);

            // One column and one bottom-up control group, with or without native ads.
            // Native availability is configuration, not fill status: failures do not reflow the page.
            NativeAdReserved=false;
            var bottom=(tight?8:10)*u;
            if(nativeAdAvailable&&!savePending&&NativeAdPlacementLayout.TryGetCardSize(root,out var adWidth,out var adHeight))
            {
                nativeAdSlot=NativeAdPlacementLayout.EnsureSlot(root,nativeAdSlot);
                nativeAdSlot.gameObject.SetActive(true);
                PlaceFromBottom(nativeAdSlot,bottom,adWidth,adHeight,height);
                bottom+=adHeight+(tight?12:18)*u;
                NativeAdReserved=true;
            }
            if(nativeAdSlot)nativeAdSlot.gameObject.SetActive(NativeAdReserved);

            var navGap=8*u;var navWidth=(width-3*navGap)/4;
            LayoutNavigation(levelsButton,-1.5f*(navWidth+navGap),bottom,navWidth,navHeight,height,u);
            LayoutNavigation(settingsButton,-.5f*(navWidth+navGap),bottom,navWidth,navHeight,height,u);
            LayoutNavigation(helpButton,.5f*(navWidth+navGap),bottom,navWidth,navHeight,height,u);
            LayoutNavigation(quitButton,1.5f*(navWidth+navGap),bottom,navWidth,navHeight,height,u);
            bottom+=navHeight+(tight?8:compact?12:18)*u;

            if(tight)
            {
                // On short safe areas use one row of actions, keeping the native card
                // and full-width harbor rather than suppressing the Home ad.
                var actionGap=10*u;var actionWidth=(width-actionGap)*.5f;
                Place(endlessStatus.rectTransform,(bottom+10*u)/height,actionWidth,20*u,(actionWidth+actionGap)*.5f);
                if(statusHeight>0)bottom+=statusHeight+4*u;
                Place((RectTransform)campaignButton.transform,(bottom+24*u)/height,actionWidth,48*u,-(actionWidth+actionGap)*.5f);
                Place((RectTransform)endlessButton.transform,(bottom+24*u)/height,actionWidth,48*u,(actionWidth+actionGap)*.5f);
                bottom+=56*u;
            }
            else
            {
                PlaceFromBottom(endlessStatus.rectTransform,bottom,width,20*u,height);
                bottom+=statusHeight;
                PlaceFromBottom((RectTransform)endlessButton.transform,bottom,width,46*u,height);
                bottom+=54*u;
                PlaceFromBottom((RectTransform)campaignButton.transform,bottom,width,52*u,height);
                bottom+=58*u;
            }
            StyleMainAction(endlessButton,u,true);StyleMainAction(campaignButton,u,false);
            PlaceFromBottom(progress.rectTransform,bottom,width,20*u,height);
            bottom+=30*u;

            if(harborPreview)
            {
                harborPreview.gameObject.SetActive(!savePending);
                FitHarbor(root,bottom,artTop);
            }
            // Storage recovery uses the same quiet art region, away from every action.
            var recoveryCenter=(bottom+artTop)*.5f;
            PlaceFromBottom(noticeLabel.rectTransform,savePending?recoveryCenter+28*u:artTop+2*u,width,38*u,height);
            PlaceFromBottom((RectTransform)retryButton.transform,recoveryCenter-36*u,width,48*u,height);
            }
            finally{relayoutActive=false;}
        }
        private static void Place(RectTransform rect,float y,float width,float height,float x=0)
        {rect.anchorMin=rect.anchorMax=new Vector2(.5f,y);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(width,height);}
        private static void PlaceFromBottom(RectTransform rect,float bottom,float width,float height,float rootHeight)
        {Place(rect,(bottom+height*.5f)/rootHeight,width,height);}
        private void FitHarbor(RectTransform root,float bottom,float top)
        {
            if(!harborPreview)return;
            // The harbor is a full-width stage, not a fixed-width card. Its height follows
            // the remaining safe space; only decorative edges of the background are cropped.
            const float originalWidth=690;
            var scale=root.rect.width/originalWidth;
            var stageHeight=Mathf.Max(1,top-bottom)/scale;
            Place(harborPreview,(bottom+top)*.5f/root.rect.height,originalWidth,stageHeight);
            harborPreview.localScale=Vector3.one*scale;
            var background=harborPreview.Find("ArtBackground") as RectTransform;
            if(background)
            {
                // Cover with a sky/harbor focal point. Disable the fitter before owning its
                // geometry, so a later layout pass cannot restore a center crop.
                var fitter=background.GetComponent<UnityEngine.UI.AspectRatioFitter>();if(fitter)fitter.enabled=false;
                var image=background.GetComponent<UnityEngine.UI.Image>();
                var aspect=image&&image.sprite?image.sprite.rect.width/image.sprite.rect.height:1.5f;
                var backgroundHeight=Mathf.Max(stageHeight,originalWidth/aspect);
                background.anchorMin=background.anchorMax=background.pivot=new Vector2(.5f,1);
                background.sizeDelta=new Vector2(backgroundHeight*aspect,backgroundHeight);
                background.anchoredPosition=new Vector2(0,(backgroundHeight-stageHeight)*.2f);
            }
            var ship=harborPreview.Find("ArtShip") as RectTransform;
            if(ship)
            {
                var shipHeight=Mathf.Min(180,stageHeight*.48f);
                PlaceStageLayer(ship,.64f,.38f,shipHeight*2,shipHeight);
            }
            var crane=harborPreview.Find("ArtCrane") as RectTransform;
            if(crane)
            {
                var craneHeight=Mathf.Min(200,stageHeight*.72f);
                PlaceStageLayer(crane,.15f,.62f,craneHeight,craneHeight);
            }
            var dock=harborPreview.Find("ArtDock") as RectTransform;
            if(dock)
            {
                var dockHeight=Mathf.Min(140,stageHeight*.24f);
                PlaceStageLayer(dock,.5f,dockHeight*.27f/stageHeight,dockHeight*640f/140f,dockHeight);
            }
        }
        private static void PlaceStageLayer(RectTransform rect,float x,float y,float width,float height)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(x,y);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(width,height);
        }
        private void LayoutNavigation(Button button,float x,float bottom,float width,float height,float rootHeight,float u)
        {
            Place((RectTransform)button.transform,(bottom+height*.5f)/rootHeight,width,height,x);
            var skin=button.GetComponent<HarborButton>();if(!skin)return;
            skin.quiet=true;skin.Refresh();skin.label.fontSize=11*u;skin.label.color=HarborTheme.Muted;
            Place(skin.label.rectTransform,.23f,width,18*u);
            if(skin.icon){Place(skin.icon.rectTransform,.67f,18*u,18*u);skin.icon.color=HarborTheme.Muted;}
        }
        private void StyleMainAction(Button button,float u,bool quiet)
        {
            var skin=button.GetComponent<HarborButton>();if(!skin)return;
            skin.quiet=quiet;skin.Refresh();skin.label.fontSize=16*u;
            var textWidth=skin.label.GetPreferredValues(skin.label.text).x;
            var iconSize=18*u;var gap=8*u;
            Place(skin.label.rectTransform,.5f,textWidth+2*u,32*u,(iconSize+gap)*.5f);
            if(skin.icon)Place(skin.icon.rectTransform,.5f,iconSize,iconSize,-(textWidth+gap)*.5f);
        }
        private void EnsureBackdrop(RectTransform root)
        {
            if(!backdrop)backdrop=root.Find("HomeBackdrop") as RectTransform;
            if(!backdrop)
            {
                var child=new GameObject("HomeBackdrop",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                backdrop=(RectTransform)child.transform;backdrop.SetParent(root,false);backdrop.SetAsFirstSibling();
                var image=child.GetComponent<UnityEngine.UI.Image>();image.color=new Color32(24,37,60,255);image.raycastTarget=false;
            }
            var canvas=root.GetComponentInParent<Canvas>().rootCanvas;
            ((RectTransform)canvas.transform).GetWorldCorners(backdropCorners);
            var min=root.InverseTransformPoint(backdropCorners[0]);var max=root.InverseTransformPoint(backdropCorners[2]);
            backdrop.anchorMin=backdrop.anchorMax=backdrop.pivot=new Vector2(.5f,.5f);
            backdrop.anchoredPosition=(min+max)*.5f;backdrop.sizeDelta=max-min;
        }
    }
}
