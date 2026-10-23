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
        public bool Visible => panel.activeSelf;
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
            if(!Visible)return;
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
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);
            card.anchoredPosition=Vector2.zero;card.sizeDelta=new Vector2(width,height);
            if(illustrationHeight>0&&illustration)
            {
                var r=illustration.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);
                r.anchoredPosition=new Vector2(0,-115-bodyHeight);r.sizeDelta=new Vector2(width-80,illustrationHeight-12);
            }
            var top=-130-bodyHeight-illustrationHeight;
            var buttonHeight=Mathf.Min(100,(height-150-bodyHeight-illustrationHeight)/Mathf.Max(1,rows)-8);
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
        public void Hide(){panel.SetActive(false);foreach(var button in buttons)button.onClick.RemoveAllListeners();}
    }
}
