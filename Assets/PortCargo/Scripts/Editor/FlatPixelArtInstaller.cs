using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using PortCargo.Runtime;

namespace PortCargo.Editor
{
    /// <summary>Explicit one-time authoring of existing scene; never runs in a player.</summary>
    [InitializeOnLoad]
    public static class FlatPixelArtInstaller
    {
        const string Art="Assets/PortCargo/Art/";
        static HarborTheme theme;
        static FlatPixelArtInstaller(){EditorApplication.update+=Consume;}
        static void Consume()
        {
            const string request="Temp/portcargo-art-request.txt";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try{Install();File.WriteAllText("Docs/Evidence/v0.4/art-install.txt","PASS: sprites, audio, theme, Bootstrap and prefab snapshots authored through Unity APIs\n");}
            catch(Exception e){File.WriteAllText("Docs/Evidence/v0.4/art-install.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("PortCargo/Apply Flat Pixel Art")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop playback first");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/PortCargo/Scenes/Bootstrap.unity")scene=EditorSceneManager.OpenScene("Assets/PortCargo/Scenes/Bootstrap.unity");
            var manifest=JObject.Parse(File.ReadAllText("Docs/Evidence/v0.4/asset-manifest.json"));
            foreach(var a in manifest["assets"])
            {
                if(!Used((string)a["id"]))continue;
                var path=Art+((string)a["file"]).Replace("01_可交付/","");
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
                settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2((float)a["pivot"][0],(float)a["pivot"][1]);
                settings.spriteBorder=new Vector4((float)a["border"][0],(float)a["border"][1],(float)a["border"][2],(float)a["border"][3]);
                importer.SetTextureSettings(settings);importer.SaveAndReimport();
            }
            foreach(var path in Directory.GetFiles("Assets/PortCargo/Audio/FlatPixel","*.wav",SearchOption.AllDirectories))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.forceToMono=!path.Contains("BGM");importer.SaveAndReimport();
            }
            theme=AssetDatabase.LoadAssetAtPath<HarborTheme>(Art+"FlatPixelTheme.asset");
            if(!theme){theme=ScriptableObject.CreateInstance<HarborTheme>();AssetDatabase.CreateAsset(theme,Art+"FlatPixelTheme.asset");}
            theme.sprites=manifest["assets"].Where(a=>Used((string)a["id"])&&(string)a["id"]!="app_icon_master").Select(a=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+((string)a["file"]).Replace("01_可交付/",""))).ToArray();EditorUtility.SetDirty(theme);
            var canvas=GameObject.Find("CanvasRoot").transform;var safe=canvas.Find("SafeArea");var hud=safe.Find("StaticHud");var board=safe.Find("BoardLayer");var overlay=safe.Find("OverlayLayer");var home=overlay.Find("HomePanel");
            foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))label.color=HarborTheme.Ink;
            var bg=canvas.Find("HarborBackground").GetComponent<Image>();Apply(bg,"bg_gameplay_harbor");
            // Cover while preserving the portrait illustration's proportions; side crop is decorative.
            var fit=bg.GetComponent<AspectRatioFitter>();if(!fit)fit=bg.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=1024f/1536;
            Camera.main.backgroundColor=HarborTheme.Dark;
            home.GetComponent<Image>().color=new Color32(24,37,60,255);
            var hero=home.Find("HarborPreview");foreach(Transform child in hero)child.gameObject.SetActive(false);
            var heroBg=EnsureImage(hero,"ArtBackground","bg_home_harbor");Stretch(heroBg.rectTransform);heroBg.preserveAspect=false;
            // Use a masked art slot and aspect-cover child, leaving all labels/buttons independent.
            if(!hero.GetComponent<RectMask2D>())hero.gameObject.AddComponent<RectMask2D>();
            var hf=heroBg.GetComponent<AspectRatioFitter>();if(!hf)hf=heroBg.gameObject.AddComponent<AspectRatioFitter>();hf.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;hf.aspectRatio=1.5f;
            var ship=EnsureImage(hero,"ArtShip","spr_ship_empty");At(ship.rectTransform,.64f,.31f,360,180);ship.preserveAspect=true;
            var crane=EnsureImage(hero,"ArtCrane","spr_crane");At(crane.rectTransform,.15f,.62f,200,200);crane.preserveAspect=true;
            var dock=EnsureImage(hero,"ArtDock","spr_dock_foreground");At(dock.rectTransform,.5f,.04f,640,140);dock.preserveAspect=true;
            home.Find("Title").GetComponent<TMP_Text>().fontSize=62;home.Find("Tagline").GetComponent<TMP_Text>().color=HarborTheme.Muted;
            home.Find("EndlessProgress").GetComponent<TMP_Text>().color=HarborTheme.Muted;
            home.Find("CampaignProgress").GetComponent<TMP_Text>().color=new Color32(255,212,86,255);
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))
            {
                var primary=button.name=="CampaignButton"||button.name=="HintButton"||button.name=="Option0";
                var compact=button.transform.parent==home&&(button.name=="LevelsButton"||button.name=="HomeSettingsButton"||button.name=="HelpButton"||button.name=="QuitButton");
                StyleButton(button,primary,compact);
            }
            Apply(hud.Find("HarborWater").GetComponent<Image>(),"toast");hud.Find("HarborWater").GetComponent<Image>().color=new Color(1,1,1,.22f);

            // Keep navigation at least 100 reference units, away from title and ship.
            At((RectTransform)hud.Find("SettingsButton"),.86f,.94f,120,100);
            At((RectTransform)hud.Find("LevelLabel"),.36f,.94f,400,80);
            At((RectTransform)hud.Find("QueueButton"),.86f,.69f,120,100);
            At((RectTransform)hud.Find("ShipView"),.5f,.79f,460,230);
            StyleShip(hud.Find("ShipView"));
            Plate(hud.Find("LevelLabel"),"toast");hud.Find("LevelLabel").GetComponent<TMP_Text>().margin=new Vector4(16,0,12,0);
            Plate(hud.Find("BufferTitle"),"toast");Plate(hud.Find("Toast"),"toast");Plate(hud.Find("Instruction"),"toast");
            var previews=new CargoGraphic[2];var counts=new TMP_Text[2];
            var font=hud.Find("NextShips").GetComponent<TMP_Text>().font;
            for(int i=0;i<2;i++)
            {
                var group=EnsureRect(hud,"NextShipPreview"+i);At(group,.24f+i*.29f,.69f,160,64);
                var plate=EnsureImage(group,"Plate","toast");Stretch(plate.rectTransform);plate.transform.SetAsFirstSibling();
                var cg=group.Find("Cargo");if(!cg){var go=new GameObject("Cargo",typeof(RectTransform),typeof(CanvasRenderer),typeof(CargoGraphic));go.transform.SetParent(group,false);cg=go.transform;}
                previews[i]=cg.GetComponent<CargoGraphic>();At((RectTransform)cg,.25f,.5f,56,56);
                var label=group.Find("Count");if(!label){var go=new GameObject("Count",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(group,false);label=go.transform;}
                counts[i]=label.GetComponent<TMP_Text>();counts[i].font=font;counts[i].fontSize=27;counts[i].color=HarborTheme.Ink;counts[i].alignment=TextAlignmentOptions.Center;counts[i].raycastTarget=false;At((RectTransform)label,.72f,.5f,70,60);
            }
            SetArray(hud.GetComponent<HudView>(),"nextCargo",previews);SetArray(hud.GetComponent<HudView>(),"nextCounts",counts);
            foreach(Transform basis in board.Find("Stacks"))if(basis.name.StartsWith("StackBase"))Apply(basis.GetComponent<Image>(),"stack_base");
            foreach(Transform frame in board.Find("Buffer"))
            {Apply(frame.GetComponent<Image>(),"buffer_slot");frame.Find("Empty").gameObject.SetActive(false);((RectTransform)frame).sizeDelta=new Vector2(100,100);}
            // Update the actual reusable BoxView prefab too, retaining its GUID and 36 instances.
            var box=PrefabUtility.LoadPrefabContents("Assets/PortCargo/Prefabs/BoxView.prefab");StyleCargo(box.GetComponent<CargoGraphic>());PrefabUtility.SaveAsPrefabAsset(box,"Assets/PortCargo/Prefabs/BoxView.prefab");PrefabUtility.UnloadPrefabContents(box);
            foreach(var cg in canvas.GetComponentsInChildren<CargoGraphic>(true))StyleCargo(cg);
            foreach(Transform departure in board.Find("Departures"))
            {((RectTransform)departure).sizeDelta=new Vector2(460,230);StyleShip(departure);}
            var card=overlay.Find("Dialog/Card");Apply(overlay.Find("Dialog").GetComponent<Image>(),"scrim");Apply(card.GetComponent<Image>(),"panel_common",true);
            var illustration=EnsureImage(card,"Illustration","completion_badge");illustration.preserveAspect=true;illustration.gameObject.SetActive(false);
            Set(overlay.GetComponent<DialogView>(),"theme",theme);Set(overlay.GetComponent<DialogView>(),"illustration",illustration);
            var bits=overlay.Find("Celebration").GetComponentsInChildren<Image>(true);
            for(int i=0;i<bits.Length;i++){Apply(bits[i],new[]{"confetti_square","confetti_star","confetti_diamond"}[i%3]);bits[i].rectTransform.sizeDelta=new Vector2(18,18);}
            var feedback=board.GetComponent<FeedbackPlayer>();
            foreach(var pair in new[]{new[]{"pickClip","pick"},new[]{"dockClip","depart"},new[]{"completeClip","complete"},new[]{"rejectClip","reject"},new[]{"menuClip","menu"}})
                Set(feedback,pair[0],AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PortCargo/Audio/FlatPixel/SFX/"+pair[1]+".wav"));
            var music=GameObject.Find("AudioRoot/Music").GetComponent<AudioSource>();music.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PortCargo/Audio/FlatPixel/BGM/night_harbor_loop.wav");music.loop=true;music.volume=.22f;
            foreach(var voice in GameObject.Find("AudioRoot").GetComponentsInChildren<AudioSource>())if(voice!=music)voice.volume=.5f;
            PlayerSettings.bundleVersion="0.4.0";
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Sprites/Harbor/app_icon_master.png")});
            home.GetComponent<HomeView>().Show(0,50,"开始闯关","开启无尽","每程一个新码头，随时回来继续",false);
            hud.gameObject.SetActive(false);board.gameObject.SetActive(false);overlay.GetComponent<DialogView>().Hide();
            PrefabUtility.SaveAsPrefabAsset(home.gameObject,"Assets/PortCargo/Prefabs/HomePanel.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject,"Assets/PortCargo/Prefabs/GameHud.prefab");
            PrefabUtility.SaveAsPrefabAsset(hud.Find("ShipView").gameObject,"Assets/PortCargo/Prefabs/ShipView.prefab");
            PrefabUtility.SaveAsPrefabAsset(overlay.Find("Dialog").gameObject,"Assets/PortCargo/Prefabs/Dialog.prefab");
            PrefabUtility.SaveAsPrefabAsset(board.Find("Stacks/StackBase0").gameObject,"Assets/PortCargo/Prefabs/StackView.prefab");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("PORTCARGO_FLAT_PIXEL_ART_INSTALLED");
        }
        static bool Used(string id)=>!new[]{"cargo_hint","icon_lock","icon_complete","icon_close","icon_toggle_on","icon_toggle_off","icon_feedback","icon_save","icon_warning"}.Contains(id);
        static void StyleShip(Transform ship)
        {
            ship.Find("Hull").gameObject.SetActive(false);ship.Find("Cabin").gameObject.SetActive(false);
            var art=EnsureImage(ship,"ShipArt","spr_ship_empty");Stretch(art.rectTransform);art.preserveAspect=true;art.transform.SetAsFirstSibling();
            At((RectTransform)ship.Find("CurrentCargo"),.49f,.52f,72,72);
            At((RectTransform)ship.Find("ShipCount"),.76f,.52f,144,64);ship.Find("ShipCount").GetComponent<TMP_Text>().fontSize=34;Plate(ship.Find("ShipCount"),"toast");
        }
        static void StyleCargo(CargoGraphic cg)
        {
            Set(cg,"theme",theme);var so=new SerializedObject(cg);so.FindProperty("smallIcon").boolValue=!cg.GetComponent<BoxView>();so.ApplyModifiedPropertiesWithoutUndo();
            var frame=EnsureImage(cg.transform,"ArtHighlight","cargo_highlight");Stretch(frame.rectTransform);frame.preserveAspect=true;frame.gameObject.SetActive(false);Set(cg,"highlightFrame",frame);
        }
        static void StyleButton(Button button,bool primary,bool compact)
        {
            var skin=button.GetComponent<HarborButton>();if(!skin)skin=button.gameObject.AddComponent<HarborButton>();
            skin.theme=theme;skin.button=button;skin.label=button.GetComponentInChildren<TMP_Text>(true);skin.primary=primary;skin.compact=compact;skin.menuFeedback=UnityEngine.Object.FindFirstObjectByType<FeedbackPlayer>(FindObjectsInactive.Include);
            skin.icon=EnsureImage(button.transform,"ArtIcon","icon_info");
            At(skin.icon.rectTransform,compact?.5f:0,compact?.68f:.5f,36,36,compact?0:28);
            skin.toggle=EnsureImage(button.transform,"ArtToggle","toggle_track_off");At(skin.toggle.rectTransform,1,.5f,88,44,-58);skin.toggle.type=Image.Type.Sliced;var knob=EnsureImage(skin.toggle.transform,"Knob","toggle_knob");At(knob.rectTransform,.25f,.5f,32,32);skin.toggle.gameObject.SetActive(false);skin.Refresh();
        }
        static void Plate(Transform text,string sprite){var p=EnsureImage(text.parent,text.name+"ReadabilityPlate",sprite);var source=(RectTransform)text;var r=p.rectTransform;r.anchorMin=source.anchorMin;r.anchorMax=source.anchorMax;r.pivot=source.pivot;r.anchoredPosition=source.anchoredPosition;r.sizeDelta=source.sizeDelta;p.transform.SetSiblingIndex(text.GetSiblingIndex());text.SetSiblingIndex(p.transform.GetSiblingIndex()+1);var tracking=text.GetComponent<HarborTextPlate>();if(!tracking)tracking=text.gameObject.AddComponent<HarborTextPlate>();tracking.label=text.GetComponent<TMP_Text>();tracking.plate=p;}
        static RectTransform EnsureRect(Transform parent,string name){var t=parent.Find(name);if(t)return (RectTransform)t;var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static Image EnsureImage(Transform parent,string name,string sprite){var r=EnsureRect(parent,name);r.gameObject.SetActive(true);var image=r.GetComponent<Image>();if(!image)image=r.gameObject.AddComponent<Image>();Apply(image,sprite);image.raycastTarget=false;return image;}
        static void Apply(Image image,string sprite,bool sliced=false){image.sprite=theme.Find(sprite);image.color=Color.white;image.type=sliced?Image.Type.Sliced:Image.Type.Simple;}
        static void At(RectTransform r,float x,float y,float w,float h,float ox=0){r.anchorMin=r.anchorMax=new Vector2(x,y);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(ox,0);r.sizeDelta=new Vector2(w,h);}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static void Set(UnityEngine.Object target,string name,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static void SetArray<T>(UnityEngine.Object target,string name,T[] values)where T:UnityEngine.Object{var so=new SerializedObject(target);var a=so.FindProperty(name);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
