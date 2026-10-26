// Run through Unity Pipeline eval_file in Edit Mode. Account IDs supplied by the owner.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<PortCargo.Runtime.GameTuning>("Assets/PortCargo/Data/GameTuning.asset");
var so = new UnityEditor.SerializedObject(tuning);
so.FindProperty("adsEnabled").boolValue = true;
so.FindProperty("rewardedHintAdUnitId").stringValue = "adunit-d04702ad95d16c3c";
so.FindProperty("endlessSkipAdUnitId").stringValue = "adunit-eac16355a06f9b4f";
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(tuning);
UnityEditor.PlayerSettings.bundleVersion = PortCargo.Editor.WeChatBuildTools.Version;
var charPath = "Assets/PortCargo/Art/Fonts/ui-characters.txt";
var all = new System.Collections.Generic.SortedSet<char>(System.IO.File.ReadAllText(charPath));
foreach(var path in System.IO.Directory.GetFiles("Assets/PortCargo/Scripts/Runtime", "*.cs"))
    foreach(var c in System.IO.File.ReadAllText(path)) if(c >= 0x4e00 && c <= 0x9fff) all.Add(c);
var chars = string.Concat(all);
var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/PortCargo/Art/Fonts/PortCargoUI.asset");
var fontSerialized = new UnityEditor.SerializedObject(font);
fontSerialized.FindProperty("m_AtlasWidth").intValue = 2048;
fontSerialized.FindProperty("m_AtlasHeight").intValue = 2048;
fontSerialized.ApplyModifiedPropertiesWithoutUndo();
typeof(TMPro.TMP_FontAsset).GetProperty("SourceFont_EditorRef",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
    .SetValue(font,UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/PortCargo/Art/Fonts/PortCargoUI.ttf"));
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
// Glyph indices can change when the subset grows; rebake all characters together.
font.ClearFontAssetData();
var added = font.TryAddCharacters(chars, out string missing);
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
if(!added) throw new System.InvalidOperationException("Missing UI glyphs: " + missing);
System.IO.File.WriteAllText(charPath, chars);
UnityEditor.EditorUtility.SetDirty(font);
foreach(var atlas in font.atlasTextures) UnityEditor.EditorUtility.SetDirty(atlas);
UnityEditor.AssetDatabase.SaveAssets();
return "Ads configured; static font glyphs verified; version " + PortCargo.Editor.WeChatBuildTools.Version;
