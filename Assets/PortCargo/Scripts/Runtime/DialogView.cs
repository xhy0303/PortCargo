using System;
using TMPro;
using UnityEngine;

namespace PortCargo.Runtime
{
    public sealed class DialogView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title, body;
        [SerializeField] private UnityEngine.UI.Button[] buttons;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private HarborTheme theme;
        [SerializeField] private UnityEngine.UI.Image illustration;
        private int optionCount;
        public int CurrentLevel {get;set;}
        private bool levelGrid;
        private int levelCount;
        private float illustrationHeight;
        private bool nativeAdAvailable,relayoutActive;
        private RectTransform nativeAdSlot;
        public bool Visible => panel.activeSelf;
        public RectTransform NativeAdSlot => nativeAdSlot;
        public bool NativeAdReserved {get;private set;}
        public void SetNativeAdAvailable(bool available)
        {if(nativeAdAvailable==available)return;nativeAdAvailable=available;Relayout();}
        public void Show(string heading, string message, string[] options, Action<int> selected,bool[] enabled=null)
        {
            if(options.Length>buttons.Length)throw new ArgumentException("Too many dialog options; paginate the list.",nameof(options));
            panel.SetActive(true);title.text=heading;body.text=message;
            optionCount=options.Length;
            for(var i=0;i<buttons.Length;i++)
            {
                var index=i;buttons[i].onClick.RemoveAllListeners();buttons[i].gameObject.SetActive(i<options.Length);
                if(i>=options.Length)continue;
                buttons[i].interactable=enabled==null||enabled[i];
                labels[i].text=heading=="选择关卡"&&options[i].StartsWith("第 ")?options[i].Replace("  ","\n"):options[i];buttons[i].onClick.AddListener(()=>selected(index));
                // Dialog reuse clears runtime listeners; restore feedback after the action.
                var feedbackSkin=buttons[i].GetComponent<HarborButton>();
                if(feedbackSkin)feedbackSkin.BindFeedback();
            }
            levelGrid=heading=="选择关卡";levelCount=0;
            if(levelGrid)foreach(var option in options){if(option.StartsWith("第 "))levelCount++;else break;}
            var art=heading.StartsWith("先取顶部")?"tutorial_only_top":heading.StartsWith("同类装船")?"tutorial_match_to_ship":heading.StartsWith("其余暂存")?"tutorial_other_to_buffer":heading=="装船完成"||heading=="本程完成"||heading=="全部通关"?"completion_badge":null;
            illustrationHeight=art==null?0:art=="completion_badge"?100:180;
            if(illustration){illustration.sprite=theme?theme.Find(art):null;illustration.gameObject.SetActive(art!=null);}
            var cardImage=panel.transform.Find("Card").GetComponent<UnityEngine.UI.Image>();
            if(theme)cardImage.sprite=theme.Find(heading.Contains("失败")||heading.Contains("未成功")||heading.Contains("无法")?"panel_warning":"panel_common");
            Relayout();
        }
        public void Relayout()
        {
            if(!panel||!Visible||relayoutActive)return;
            relayoutActive=true;
            try
            {
            Canvas.ForceUpdateCanvases();
            var card=(RectTransform)panel.transform.Find("Card");
            var available=((RectTransform)panel.transform).rect;
            var width=Mathf.Min(620,available.width*.86f);
            var bodyHeight=string.IsNullOrEmpty(body.text)?0:Mathf.Min(360,Mathf.Max(60,body.GetPreferredValues(body.text,width-60,0).y+8));
            title.rectTransform.sizeDelta=new Vector2(width-60,title.rectTransform.sizeDelta.y);
            body.rectTransform.sizeDelta=new Vector2(width-60,bodyHeight);
            var columns=levelGrid?4:title.text=="设置"?1:optionCount>=5?2:1;
            var tileSide=(width-60-36)/4;
            // Reserve the full eight-level page so sparse pages keep the card and footer in place.
            var rows=levelGrid?2:(optionCount+columns-1)/columns;
            var actionsHeight=levelGrid?rows*(tileSide+12)+108:rows*108;
            var height=Mathf.Min(available.height*.92f,150+bodyHeight+illustrationHeight+actionsHeight);
            var shownIllustrationHeight=illustrationHeight;
            var cardFits=NativeAdPlacementLayout.TryGetCardSize((RectTransform)panel.transform,out var adWidth,out var adHeight);
            var completion=title.text=="装船完成"||title.text=="本程完成"||title.text=="全部通关";
            var nativePage=completion||title.text=="休息一下"||title.text=="设置";
            var nativeCardHeight=150+bodyHeight+actionsHeight;
            var nativeHeight=nativeCardHeight+adHeight+NativeAdPlacementLayout.Gap;
            NativeAdReserved=nativeAdAvailable&&nativePage&&cardFits&&nativeHeight<=available.height*.92f;
            if(NativeAdReserved)
            {
                // Replace only the decorative completion badge, preserving full-sized action buttons.
                shownIllustrationHeight=0;height=nativeCardHeight;
                nativeAdSlot=NativeAdPlacementLayout.EnsureSlot((RectTransform)panel.transform,nativeAdSlot);
                nativeAdSlot.gameObject.SetActive(true);
                nativeAdSlot.anchorMin=nativeAdSlot.anchorMax=nativeAdSlot.pivot=new Vector2(.5f,.5f);
                nativeAdSlot.anchoredPosition=new Vector2(0,-(nativeCardHeight+NativeAdPlacementLayout.Gap)*.5f);
                nativeAdSlot.sizeDelta=new Vector2(adWidth,adHeight);
            }
            if(nativeAdSlot)nativeAdSlot.gameObject.SetActive(NativeAdReserved);
            if(illustration)illustration.gameObject.SetActive(shownIllustrationHeight>0);
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);
            card.anchoredPosition=NativeAdReserved?new Vector2(0,(adHeight+NativeAdPlacementLayout.Gap)*.5f):Vector2.zero;
            card.sizeDelta=new Vector2(width,height);
            if(shownIllustrationHeight>0&&illustration)
            {
                var r=illustration.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);
                r.anchoredPosition=new Vector2(0,-115-bodyHeight);r.sizeDelta=new Vector2(width-80,shownIllustrationHeight-12);
            }
            var top=-130-bodyHeight-shownIllustrationHeight;
            var buttonHeight=Mathf.Min(100,(height-150-bodyHeight-shownIllustrationHeight)/Mathf.Max(1,rows)-8);
            var buttonWidth=columns==1?width-60:(width-80)/2;
            for(var i=0;i<optionCount;i++)
            {
                var rect=(RectTransform)buttons[i].transform;
                if(levelGrid&&i<levelCount)
                {rect.anchoredPosition=new Vector2((i%4-1.5f)*(tileSide+12),top-(i/4)*(tileSide+12));rect.sizeDelta=Vector2.one*tileSide;}
                else if(levelGrid)
                {
                    var count=optionCount-levelCount;var w=(width-60-(count-1)*12)/count;
                    rect.anchoredPosition=new Vector2((i-levelCount-(count-1)/2f)*(w+12),top-rows*(tileSide+12));rect.sizeDelta=new Vector2(w,100);
                }
                else
                {rect.anchoredPosition=new Vector2(columns==1?0:(i%2==0?-1:1)*(buttonWidth+20)/2,top-(i/columns)*(buttonHeight+8));rect.sizeDelta=new Vector2(buttonWidth,buttonHeight);}
                labels[i].fontSize=levelGrid?23:columns==1?29:25;
                var skin=buttons[i].GetComponent<HarborButton>();
                if(skin){skin.levelTile=levelGrid&&i<levelCount;skin.currentLevel=skin.levelTile&&labels[i].text.StartsWith("第 "+CurrentLevel+" 关");skin.primary=!levelGrid&&i==0&&title.text!="设置";skin.Refresh();}
            }
            }
            finally{relayoutActive=false;}
        }
        private void OnRectTransformDimensionsChange(){if(title&&panel)Relayout();}
        public void Hide(){NativeAdReserved=false;panel.SetActive(false);foreach(var button in buttons)button.onClick.RemoveAllListeners();}
    }
}
