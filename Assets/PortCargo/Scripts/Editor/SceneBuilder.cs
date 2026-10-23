using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using PortCargo.Runtime;

namespace PortCargo.Editor
{
    /// <summary>Explicit authoring tool. All scene/prefab assets are created by Unity APIs, never YAML patches.</summary>
    public sealed class SceneBuilder
    {
        private const string Root="Assets/PortCargo/";
        private TMP_FontAsset font;
        private readonly Color ink=new Color32(27,57,66,255), accent=new Color32(30,111,119,255);

        [MenuItem("PortCargo/Create Bootstrap")]
        public static void Build() => new SceneBuilder().Create();

        private void Create()
        {
            if(File.Exists(Root+"Scenes/Bootstrap.unity"))throw new InvalidOperationException("Bootstrap already exists; edit existing objects instead of rebuilding.");
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Root+"Prefabs");Directory.CreateDirectory(Root+"Scenes");
            font=CreateFont();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("MainCamera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(237,242,238,255);
            var canvasGo=new GameObject("CanvasRoot",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(720,1280);scaler.matchWidthOrHeight=.5f;
            var background=Panel("HarborBackground",canvasGo.transform,new Color32(237,242,238,255));Stretch(background);
            var safe=Rect("SafeArea",canvasGo.transform);Stretch(safe);
            var layout=canvasGo.AddComponent<SafeAreaLayout>();Set(layout,"safeArea",safe);
            var hudRoot=Rect("StaticHud",safe);Stretch(hudRoot);
            var hud=hudRoot.gameObject.AddComponent<HudView>();
            var boardRoot=Rect("BoardLayer",safe);Stretch(boardRoot);
            boardRoot.gameObject.AddComponent<Canvas>();boardRoot.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var board=boardRoot.gameObject.AddComponent<BoardView>();
            var overlay=Rect("OverlayLayer",safe);Stretch(overlay);overlay.gameObject.AddComponent<Canvas>();overlay.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            CreateHud(hudRoot,hud);
            CreateBoard(boardRoot,board);
            var dialog=CreateDialog(overlay);
            var audioRoot=new GameObject("AudioRoot");
            var feedback=CreateFeedback(boardRoot,audioRoot.transform);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var go=new GameObject("CompositionRoot");var presenter=go.AddComponent<GamePresenter>();var composition=go.AddComponent<CompositionRoot>();
            Set(presenter,"board",board);Set(presenter,"hud",hud);Set(presenter,"dialog",dialog);Set(presenter,"feedback",feedback);Set(presenter,"layout",layout);
            var tuning=ScriptableObject.CreateInstance<GameTuning>();AssetDatabase.CreateAsset(tuning,Root+"Data/GameTuning.asset");
            Set(composition,"catalog",AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"Data/Levels/catalog.json"));Set(composition,"tuning",tuning);
            Set(composition,"presenter",presenter);Set(composition,"dialog",dialog);
            EditorSettings.serializationMode=SerializationMode.ForceText;
            PlayerSettings.companyName="PortCargo";PlayerSettings.productName="PortCargo";PlayerSettings.bundleVersion="0.1.0";PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.portcargo.prototype");
            PlayerSettings.defaultScreenWidth=720;PlayerSettings.defaultScreenHeight=1280;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;PlayerSettings.colorSpace=ColorSpace.Gamma;
            QualitySettings.antiAliasing=0;QualitySettings.vSyncCount=0;QualitySettings.shadows=ShadowQuality.Disable;
            PlayerSettings.stripEngineCode=true;
            EditorSceneManager.SaveScene(scene,Root+"Scenes/Bootstrap.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/Bootstrap.unity",true)};
            AssetDatabase.SaveAssets();
            Debug.Log("PORTCARGO_BOOTSTRAP_CREATED Unity="+Application.unityVersion);
        }

        private TMP_FontAsset CreateFont()
        {
            var path=Root+"Art/Fonts/PortCargoUI.asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing)return existing;
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/PortCargoUI.ttf");
            if(!source)throw new InvalidOperationException("Font source missing");
            var value=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
            value.name="PortCargoUI";
            var text=File.ReadAllText(Root+"Art/Fonts/ui-characters.txt").Replace("\n","").Replace("\r","").Replace("\t","");
            value.TryAddCharacters(text,out var missing);
            if(!string.IsNullOrEmpty(missing))Debug.LogWarning("PORTCARGO_FONT_MISSING "+missing);
            value.atlasPopulationMode=AtlasPopulationMode.Static;value.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>();
            AssetDatabase.CreateAsset(value,path);AssetDatabase.AddObjectToAsset(value.material,value);
            foreach(var texture in value.atlasTextures){texture.name="PortCargoUI Atlas";AssetDatabase.AddObjectToAsset(texture,value);}
            AssetDatabase.SaveAssets();return value;
        }

        private void CreateHud(RectTransform root,HudView hud)
        {
            var title=Text("LevelLabel",root,"港口装箱  /  01",32,ink);At(title.rectTransform,new Vector2(.03f,1),new Vector2(0,-66),new Vector2(390,56),new Vector2(0,1));title.alignment=TextAlignmentOptions.Left;
            var settings=Button("SettingsButton",root,"设置",new Vector2(112,64));At((RectTransform)settings.transform,new Vector2(.58f,1),new Vector2(0,-64),new Vector2(112,64),new Vector2(.5f,1));
            var water=Panel("HarborWater",root,new Color32(209,231,228,255));water.anchorMin=new Vector2(0,.69f);water.anchorMax=new Vector2(1,.88f);water.offsetMin=Vector2.zero;water.offsetMax=Vector2.zero;
            var ship=Rect("ShipView",root);At(ship,new Vector2(.5f,.79f),Vector2.zero,new Vector2(370,140),new Vector2(.5f,.5f));
            var hull=Panel("Hull",ship,accent);At(hull,new Vector2(.5f,.5f),new Vector2(0,-28),new Vector2(350,38),new Vector2(.5f,.5f));
            var cabin=Panel("Cabin",ship,new Color32(248,250,239,255));At(cabin,new Vector2(.5f,.5f),new Vector2(-125,14),new Vector2(52,48),new Vector2(.5f,.5f));
            var window=Panel("Window",cabin,ink);At(window,new Vector2(.5f,.5f),new Vector2(0,5),new Vector2(23,13),new Vector2(.5f,.5f));
            var cargo=Graphic("CurrentCargo",ship);At(cargo.rectTransform,new Vector2(.5f,.5f),new Vector2(-45,18),new Vector2(70,65),new Vector2(.5f,.5f));
            var counter=Text("ShipCount",ship,"0 / 3",38,ink);At(counter.rectTransform,new Vector2(.5f,.5f),new Vector2(80,20),new Vector2(170,60),new Vector2(.5f,.5f));
            var next=Text("NextShips",root,"最后一船",24,ink);At(next.rectTransform,new Vector2(.5f,.70f),Vector2.zero,new Vector2(640,50),new Vector2(.5f,.5f));
            var queue=Button("QueueButton",root,"船队",new Vector2(108,60));At((RectTransform)queue.transform,new Vector2(.84f,.86f),Vector2.zero,new Vector2(108,60),new Vector2(.5f,.5f));
            var tutorial=Text("Instruction",root,"",27,ink);At(tutorial.rectTransform,new Vector2(.5f,.635f),Vector2.zero,new Vector2(650,54),new Vector2(.5f,.5f));
            var bufferLabel=Text("BufferTitle",root,"暂存",24,ink);At(bufferLabel.rectTransform,new Vector2(.5f,.245f),Vector2.zero,new Vector2(180,40),new Vector2(.5f,.5f));
            var undo=Button("UndoButton",root,"撤销",new Vector2(260,88));At((RectTransform)undo.transform,new Vector2(.27f,0),new Vector2(0,48),new Vector2(260,88),new Vector2(.5f,0));
            var hint=Button("HintButton",root,"提示",new Vector2(260,88),true);At((RectTransform)hint.transform,new Vector2(.73f,0),new Vector2(0,48),new Vector2(260,88),new Vector2(.5f,0));
            var toast=Text("Toast",root,"",25,ink);At(toast.rectTransform,new Vector2(.5f,.29f),Vector2.zero,new Vector2(660,60),new Vector2(.5f,.5f));
            var chain=Text("Chain",root,"",38,accent);At(chain.rectTransform,new Vector2(.5f,.90f),Vector2.zero,new Vector2(420,60),new Vector2(.5f,.5f));
            var retry=Button("RetrySave",root,"保存失败，点此重试",new Vector2(600,52));At((RectTransform)retry.transform,new Vector2(.5f,.115f),Vector2.zero,new Vector2(600,52),new Vector2(.5f,.5f));retry.gameObject.SetActive(false);
            Set(hud,"levelLabel",title);Set(hud,"shipCount",counter);Set(hud,"nextLabel",next);Set(hud,"instruction",tutorial);Set(hud,"toast",toast);Set(hud,"chain",chain);
            Set(hud,"currentCargo",cargo);Set(hud,"shipTarget",cargo.rectTransform);Set(hud,"undoButton",undo);Set(hud,"hintButton",hint);Set(hud,"settingsButton",settings);Set(hud,"queueButton",queue);Set(hud,"retrySaveButton",retry);
            PrefabUtility.SaveAsPrefabAsset(ship.gameObject,Root+"Prefabs/ShipView.prefab");
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"Prefabs/GameHud.prefab");
        }

        private void CreateBoard(RectTransform root,BoardView board)
        {
            var area=Rect("Stacks",root);area.anchorMin=new Vector2(.025f,.34f);area.anchorMax=new Vector2(.975f,.62f);area.offsetMin=Vector2.zero;area.offsetMax=Vector2.zero;
            var basis=new RectTransform[6];
            for(var i=0;i<6;i++){basis[i]=Panel("StackBase"+i,area,new Color32(124,148,145,255));At(basis[i],new Vector2(.5f,0),Vector2.zero,new Vector2(100,7),new Vector2(.5f,0));}
            PrefabUtility.SaveAsPrefabAsset(basis[0].gameObject,Root+"Prefabs/StackView.prefab");
            var prototype=Graphic("BoxView",area);prototype.raycastTarget=true;var view=prototype.gameObject.AddComponent<BoxView>();Set(view,"graphic",prototype);
            At(prototype.rectTransform,new Vector2(.5f,0),Vector2.zero,new Vector2(100,108),new Vector2(.5f,0));
            var prefab=PrefabUtility.SaveAsPrefabAsset(prototype.gameObject,Root+"Prefabs/BoxView.prefab");UnityEngine.Object.DestroyImmediate(prototype.gameObject);
            var pool=new BoxView[36];
            for(var i=0;i<pool.Length;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,area);pool[i]=go.GetComponent<BoxView>();go.name="BoxSlot"+i;go.SetActive(false);}
            var buffer=Rect("Buffer",root);At(buffer,new Vector2(.5f,.185f),Vector2.zero,new Vector2(490,94),new Vector2(.5f,.5f));
            var slots=new CargoGraphic[4];var frames=new GameObject[4];
            for(var i=0;i<4;i++)
            {
                var frame=Panel("BufferFrame"+i,buffer,new Color32(174,193,188,255));At(frame,new Vector2(.5f,.5f),new Vector2((i-1)*114,0),new Vector2(96,92),new Vector2(.5f,.5f));frames[i]=frame.gameObject;
                var fill=Panel("Empty",frame,new Color32(224,233,225,255));Stretch(fill,3);
                slots[i]=Graphic("Cargo",frame);Stretch(slots[i].rectTransform,7);slots[i].gameObject.SetActive(false);
            }
            Set(board,"stackArea",area);SetArray(board,"boxes",pool);SetArray(board,"bases",basis);SetArray(board,"bufferSlots",slots);SetArray(board,"bufferFrames",frames);
        }

        private DialogView CreateDialog(RectTransform root)
        {
            var dialog=root.gameObject.AddComponent<DialogView>();
            var panel=Panel("Dialog",root,new Color(0.06f,.16f,.19f,.40f));Stretch(panel);panel.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var card=Panel("Card",panel,new Color32(247,249,239,255));card.anchorMin=new Vector2(.07f,.1f);card.anchorMax=new Vector2(.93f,.9f);card.offsetMin=Vector2.zero;card.offsetMax=Vector2.zero;
            var title=Text("Title",card,"",40,ink);At(title.rectTransform,new Vector2(.5f,1),new Vector2(0,-30),new Vector2(550,65),new Vector2(.5f,1));
            var body=Text("Body",card,"",25,ink);At(body.rectTransform,new Vector2(.5f,1),new Vector2(0,-105),new Vector2(540,90),new Vector2(.5f,1));
            var buttons=new UnityEngine.UI.Button[12];var labels=new TMP_Text[12];
            for(var i=0;i<buttons.Length;i++)
            {buttons[i]=Button("Option"+i,card,"",new Vector2(500,64),i==0);At((RectTransform)buttons[i].transform,new Vector2(.5f,1),new Vector2(0,-200-i*72),new Vector2(500,64),new Vector2(.5f,1));labels[i]=buttons[i].GetComponentInChildren<TMP_Text>();}
            Set(dialog,"panel",panel.gameObject);Set(dialog,"title",title);Set(dialog,"body",body);SetArray(dialog,"buttons",buttons);SetArray(dialog,"labels",labels);
            panel.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(panel.gameObject,Root+"Prefabs/Dialog.prefab");return dialog;
        }

        private FeedbackPlayer CreateFeedback(RectTransform root,Transform audio)
        {
            var player=root.gameObject.AddComponent<FeedbackPlayer>();var ghosts=new CargoGraphic[6];var voices=new AudioSource[6];
            for(var i=0;i<6;i++)
            {
                ghosts[i]=Graphic("Flight"+i,root);ghosts[i].rectTransform.sizeDelta=new Vector2(65,60);ghosts[i].gameObject.SetActive(false);
                var go=new GameObject("Voice"+i);go.transform.SetParent(audio,false);voices[i]=go.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].volume=.4f;
            }
            audio.gameObject.AddComponent<AudioListener>();
            var music=new GameObject("Music").AddComponent<AudioSource>();music.transform.SetParent(audio,false);music.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/music.wav");music.loop=true;music.volume=.13f;music.playOnAwake=false;Set(player,"musicSource",music);
            SetArray(player,"ghosts",ghosts);SetArray(player,"voices",voices);
            Set(player,"pickClip",AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/pick.wav"));Set(player,"dockClip",AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/dock.wav"));
            Set(player,"completeClip",AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/complete.wav"));Set(player,"rejectClip",AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/reject.wav"));return player;
        }

        private RectTransform Rect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        private RectTransform Panel(string name,Transform parent,Color color){var r=Rect(name,parent);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return r;}
        private CargoGraphic Graphic(string name,Transform parent){var r=Rect(name,parent);var g=r.gameObject.AddComponent<CargoGraphic>();g.raycastTarget=false;return g;}
        private TMP_Text Text(string name,Transform parent,string text,float size,Color color)
        {
            var r=Rect(name,parent);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.color=color;
            label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=false;label.raycastTarget=false;label.text=text;return label;
        }
        private UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 size,bool primary=false)
        {
            var r=Panel(name,parent,primary?accent:new Color32(216,228,217,255));r.sizeDelta=size;var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var label=Text("Label",r,text,29,primary?Color.white:ink);Stretch(label.rectTransform,4);return button;
        }
        private static void Stretch(RectTransform r,float inset=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,inset);r.offsetMax=new Vector2(-inset,-inset);}
        private static void At(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
        private static void Set(UnityEngine.Object target,string name,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static void SetArray<T>(UnityEngine.Object target,string name,T[] values)where T:UnityEngine.Object
        {var so=new SerializedObject(target);var p=so.FindProperty(name);p.arraySize=values.Length;for(var i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
