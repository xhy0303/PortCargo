using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortCargo.Runtime
{
    /// <summary>Styles an authored button while retaining its original text and event binding.</summary>
    public sealed class HarborButton : MonoBehaviour
    {
        public HarborTheme theme;
        public Button button;
        public TMP_Text label;
        public Image icon;
        public Image toggle;
        public bool primary,compact,levelTile,currentLevel;
        public bool quiet;
        public FeedbackPlayer menuFeedback;
        private string lastText;
        private bool lastEnabled,lastPrimary,lastTile,lastCurrent,lastQuiet;
        private void OnEnable(){lastText=null;Refresh();BindFeedback();}
        private void OnDisable(){if(button)button.onClick.RemoveListener(MenuSound);}
        public void BindFeedback(){if(button){button.onClick.RemoveListener(MenuSound);button.onClick.AddListener(MenuSound);}}
        private void MenuSound(){if(menuFeedback)menuFeedback.Menu();}
        private void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!theme||!button||!label)return;
            var text=label.text;
            if(text==lastText&&lastEnabled==button.interactable&&lastPrimary==primary&&lastTile==levelTile&&lastCurrent==currentLevel&&lastQuiet==quiet)return;
            lastText=text;lastEnabled=button.interactable;lastPrimary=primary;lastTile=levelTile;lastCurrent=currentLevel;lastQuiet=quiet;
            var image=(Image)button.targetGraphic;
            var family=primary?"button_primary_":"button_secondary_";
            var normal=theme.Find(family+"normal");
            if(levelTile)normal=theme.Find(!button.interactable?"level_tile_locked":text.Contains("已完成")?"level_tile_completed":currentLevel?"level_tile_current":"level_tile_normal");
            image.sprite=normal;image.type=levelTile?Image.Type.Simple:Image.Type.Sliced;image.color=Color.white;
            button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=normal,selectedSprite=normal,pressedSprite=levelTile?normal:theme.Find(family+"pressed"),disabledSprite=levelTile?normal:theme.Find(family+"disabled")};
            if(quiet)
            {
                image.sprite=null;image.type=Image.Type.Simple;
                image.color=compact?new Color32(29,44,67,255):new Color32(39,59,87,255);
                button.transition=Selectable.Transition.ColorTint;
                var colors=ColorBlock.defaultColorBlock;colors.pressedColor=new Color(.65f,.75f,.9f,1);button.colors=colors;
            }
            label.color=primary&&!levelTile&&button.interactable?HarborTheme.Dark:HarborTheme.Ink;
            var toggleLabel=text.StartsWith("音效：")||text.StartsWith("音乐：")||text.StartsWith("减少动效：");
            if(toggle)
            {
                toggle.gameObject.SetActive(toggleLabel);var on=text.EndsWith("开");toggle.sprite=theme.Find(on?"toggle_track_on":"toggle_track_off");
                var knob=toggle.transform.Find("Knob") as RectTransform;
                if(knob){knob.anchorMin=knob.anchorMax=new Vector2(on?.75f:.25f,.5f);knob.anchoredPosition=Vector2.zero;}
            }
            if(icon)
            {
                icon.sprite=theme.Find("icon_"+IconFor(text));icon.color=label.color;
                icon.gameObject.SetActive(!levelTile&&icon.sprite);
            }
            var rect=label.rectTransform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            if(compact){rect.offsetMin=new Vector2(4,4);rect.offsetMax=new Vector2(-4,-51);label.fontSize=23;}
            else if(levelTile){rect.offsetMin=new Vector2(10,8);rect.offsetMax=new Vector2(-10,-14);label.fontSize=22;}
            else
            {
                var narrow=((RectTransform)button.transform).rect.width<160;
                rect.offsetMin=new Vector2(icon&&icon.gameObject.activeSelf?(narrow?40:54):12,8);rect.offsetMax=new Vector2(toggleLabel?-108:-8,-8);
                if(narrow){label.fontSize=24;if(icon){icon.rectTransform.sizeDelta=Vector2.one*26;icon.rectTransform.anchoredPosition=new Vector2(21,0);}}
            }
        }
        private static string IconFor(string text)
        {
            if(text.Contains("音效"))return "sound";if(text.Contains("音乐"))return "music";if(text.Contains("减少动效"))return "reduced_motion";
            if(text.Contains("保存")||text=="重试")return "retry_save";if(text.Contains("隐私"))return "privacy";
            if(text.Contains("无尽"))return "mode_endless";if(text.Contains("闯关"))return "mode_campaign";
            if(text.Contains("选关")||text.Contains("选择关卡"))return "level_select";
            if(text.Contains("设置"))return "settings";if(text.Contains("玩法")||text.Contains("说明"))return "help";
            if(text.Contains("退出"))return "exit";if(text.Contains("首页"))return "home";if(text.Contains("撤销"))return "undo";
            if(text.Contains("提示"))return "hint";if(text.Contains("暂停"))return "pause";if(text.Contains("船队"))return "queue";
            if(text.Contains("重新")||text.Contains("重玩"))return "restart";
            if(text.Contains("下一"))return "next";if(text.Contains("上一"))return "previous";
            if(text.Contains("返回")||text.Contains("留在"))return "back";
            if(text.Contains("继续")||text.Contains("开始"))return "play";
            return "info";
        }
    }
}
