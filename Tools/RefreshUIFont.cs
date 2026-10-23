using System;
using System.IO;
using UnityEditor;
using TMPro;
public static class RefreshUIFont
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first");
        const string folder="Assets/PortCargo/Art/Fonts/";
        AssetDatabase.ImportAsset(folder+"PortCargoUI.ttf",ImportAssetOptions.ForceUpdate);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder+"PortCargoUI.asset");
        font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
        font.ClearFontAssetData();
        var chars=File.ReadAllText(folder+"ui-characters.txt");
        if(!font.TryAddCharacters(chars,out string missing))throw new Exception("Missing font characters: "+missing);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        foreach(var atlas in font.atlasTextures)EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        PlayerSettings.bundleVersion="0.3.0";
        AssetDatabase.SaveAssets();
        return "Static UI font updated: "+chars.Length+" characters; version 0.3.0";
    }
}
