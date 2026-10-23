using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using PortCargo.Runtime;
public static class AddFlowShell
{
    private static TMP_FontAsset font;
    private static readonly Color ink=new Color32(27,57,66,255),accent=new Color32(30,111,119,255);
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before authoring");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/PortCargo/Scenes/Bootstrap.unity")scene=EditorSceneManager.OpenScene("Assets/PortCargo/Scenes/Bootstrap.unity");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PortCargo/Art/Fonts/PortCargoUI.asset");
        var safe=GameObject.Find("CanvasRoot").transform.Find("SafeArea");var overlay=safe.Find("OverlayLayer");
        var existing=overlay.Find("HomePanel");HomeView home;
        if(existing)home=existing.GetComponent<HomeView>();
        else
        {
            var panel=Panel("HomePanel",overlay,new Color32(237,242,238,255));Stretch(panel);panel.SetAsFirstSibling();
            home=panel.gameObject.AddComponent<HomeView>();
            Set(home,"title",Label("Title",panel,56));Set(home,"tagline",Label("Tagline",panel,25));
            Set(home,"progress",Label("CampaignProgress",panel,30));Set(home,"endlessStatus",Label("EndlessProgress",panel,25));
            Set(home,"noticeLabel",Label("Notice",panel,24));
            Set(home,"campaignButton",Button("CampaignButton",panel,"开始闯关",true));
            Set(home,"endlessButton",Button("EndlessButton",panel,"开启无尽"));
            Set(home,"levelsButton",Button("LevelsButton",panel,"选择关卡"));Set(home,"helpButton",Button("HelpButton",panel,"玩法说明"));
            Set(home,"settingsButton",Button("HomeSettingsButton",panel,"设置"));Set(home,"quitButton",Button("QuitButton",panel,"退出游戏"));
            Set(home,"retryButton",Button("HomeRetrySave",panel,"保存失败，点此重试"));
        }
        var preview=home.transform.Find("HarborPreview") as RectTransform;
        if(!preview)
        {
            // Independent, non-interactive art slot shared by the five concepts.
            // Replace the whole group later; labels and navigation remain separate.
            preview=new GameObject("HarborPreview",typeof(RectTransform)).GetComponent<RectTransform>();
            preview.SetParent(home.transform,false);
            Block("Water",preview,.04f,.02f,.96f,.40f,new Color32(186,213,214,255));
            Block("Quay",preview,.04f,.40f,.36f,.50f,new Color32(156,173,164,255));
            Block("CraneTower",preview,.14f,.50f,.17f,.94f,ink);
            Block("CraneArm",preview,.14f,.89f,.56f,.94f,ink);
            Block("CraneCable",preview,.53f,.69f,.538f,.89f,ink);
            Block("ShipHull",preview,.34f,.23f,.94f,.37f,accent);
            Block("ShipDeck",preview,.34f,.37f,.91f,.41f,ink);
            Block("ShipCabin",preview,.81f,.41f,.91f,.64f,new Color32(221,230,218,255));
            Block("CabinWindow",preview,.835f,.51f,.90f,.58f,accent);
            Cargo("DockCargo",preview,.20f,.50f,.32f,.72f,"Y");
            Cargo("ShipCargoR",preview,.37f,.41f,.49f,.63f,"R");
            Cargo("ShipCargoB",preview,.51f,.41f,.63f,.63f,"B");
            Cargo("ShipCargoG",preview,.65f,.41f,.77f,.63f,"G");
            Block("WakeA",preview,.12f,.12f,.36f,.135f,new Color32(237,242,238,255));
            Block("WakeB",preview,.55f,.07f,.85f,.085f,new Color32(237,242,238,255));
        }
        Set(home,"harborPreview",preview);
        home.transform.Find("EndlessButton").GetComponent<Image>().color=new Color32(216,228,217,255);
        home.transform.Find("EndlessButton").GetComponentInChildren<TMP_Text>().color=ink;
        var presenter=UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
        var flow=presenter.GetComponent<GameFlowController>();if(!flow)flow=presenter.gameObject.AddComponent<GameFlowController>();
        var hud=safe.Find("StaticHud").GetComponent<HudView>();var board=safe.Find("BoardLayer").GetComponent<BoardView>();var dialog=overlay.GetComponent<DialogView>();
        Set(flow,"home",home);Set(flow,"dialog",dialog);Set(flow,"presenter",presenter);Set(flow,"hud",hud);Set(flow,"board",board);Set(presenter,"flow",flow);
        hud.transform.Find("SettingsButton").GetComponentInChildren<TMP_Text>().text="暂停";
        home.Show(0,50,"开始闯关","开启无尽","每程一个新码头，随时回来继续",false);
        PrefabUtility.SaveAsPrefabAsset(home.gameObject,"Assets/PortCargo/Prefabs/HomePanel.prefab");
        hud.gameObject.SetActive(false);board.gameObject.SetActive(false);dialog.Hide();
        PlayerSettings.bundleVersion="0.3.0";
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Flow shell authored: HomePanel + GameFlowController; one scene, existing three canvases; version 0.3.0";
    }
    private static RectTransform Panel(string name,Transform parent,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=true;return (RectTransform)go.transform;}
    private static TMP_Text Label(string name,Transform parent,int size)
    {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TMP_Text>();text.font=font;text.fontSize=size;text.color=ink;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;}
    private static Button Button(string name,Transform parent,string label,bool primary=false)
    {var rect=Panel(name,parent,primary?accent:new Color32(216,228,217,255));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.navigation=new Navigation{mode=Navigation.Mode.None};var text=Label("Label",rect,29);text.text=label;text.color=primary?Color.white:ink;Stretch(text.rectTransform,6);return button;}
    private static void Stretch(RectTransform rect,float inset=0){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=Vector2.one*-inset;}
    private static void Bounds(RectTransform rect,float x0,float y0,float x1,float y1)
    {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Block(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
    {var rect=Panel(name,parent,color);rect.GetComponent<Image>().raycastTarget=false;Bounds(rect,x0,y0,x1,y1);}
    private static void Cargo(string name,Transform parent,float x0,float y0,float x1,float y1,string type)
    {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(CargoGraphic));go.transform.SetParent(parent,false);Bounds((RectTransform)go.transform,x0,y0,x1,y1);var graphic=go.GetComponent<CargoGraphic>();graphic.Set(type);graphic.raycastTarget=false;}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
}
