using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using PortCargo.Runtime;
public static class PolishScene
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first");
        var root=GameObject.Find("CanvasRoot").transform;
        var feedback=UnityEngine.Object.FindFirstObjectByType<FeedbackPlayer>();
        var ship=root.Find("SafeArea/StaticHud/ShipView") as RectTransform;
        var board=root.Find("SafeArea/BoardLayer");var overlay=root.Find("SafeArea/OverlayLayer");
        var old=board.Find("Departures");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var fx=new GameObject("Departures",typeof(RectTransform)).GetComponent<RectTransform>();fx.SetParent(board,false);Stretch(fx);
        var ships=new RectTransform[3];var cargo=new CargoGraphic[3];var labels=new TMP_Text[3];var alpha=new CanvasGroup[3];
        for(var i=0;i<3;i++)
        {
            ships[i]=UnityEngine.Object.Instantiate(ship,fx);ships[i].name="DepartingShip"+i;
            cargo[i]=ships[i].GetComponentInChildren<CargoGraphic>();labels[i]=ships[i].GetComponentInChildren<TMP_Text>();alpha[i]=ships[i].gameObject.AddComponent<CanvasGroup>();
            alpha[i].blocksRaycasts=false;alpha[i].interactable=false;ships[i].gameObject.SetActive(false);
        }
        old=overlay.Find("Celebration");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var celebration=new GameObject("Celebration",typeof(RectTransform)).GetComponent<RectTransform>();celebration.SetParent(overlay,false);Stretch(celebration);
        var bits=new Image[12];var colors=new[]{new Color32(228,109,86,255),new Color32(65,146,191,255),new Color32(53,151,130,255),new Color32(232,186,75,255)};
        for(var i=0;i<bits.Length;i++)
        {
            var go=new GameObject("Bit"+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(celebration,false);
            bits[i]=go.GetComponent<Image>();bits[i].color=colors[i%4];bits[i].raycastTarget=false;bits[i].rectTransform.sizeDelta=new Vector2(9,13);go.SetActive(false);
        }
        Set(feedback,"dock",ship);SetArray(feedback,"departingShips",ships);SetArray(feedback,"departingCargo",cargo);SetArray(feedback,"departingCounts",labels);SetArray(feedback,"departingAlpha",alpha);SetArray(feedback,"confetti",bits);
        var hud=root.Find("SafeArea/StaticHud");
        foreach(var name in new[]{"SettingsButton","QueueButton","UndoButton","HintButton","RetrySave"})
        {var rect=hud.Find(name) as RectTransform;rect.sizeDelta=new Vector2(rect.sizeDelta.x,100);}
        // Keep the taller Settings hitbox clear of the ship queue control.
        ((RectTransform)hud.Find("SettingsButton")).anchoredPosition=new Vector2(0,-36);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        return "Ship departure pool=3; celebration pool=12; controls=100 reference px; scene saved";
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static void Set(UnityEngine.Object o,string field,UnityEngine.Object value){var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void SetArray<T>(UnityEngine.Object o,string field,T[] values)where T:UnityEngine.Object{var s=new SerializedObject(o);var p=s.FindProperty(field);p.arraySize=values.Length;for(var i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
}
