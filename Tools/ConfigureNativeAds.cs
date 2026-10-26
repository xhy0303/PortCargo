// Run through Unity Pipeline eval_file in Edit Mode. IDs supplied by the game owner.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<PortCargo.Runtime.GameTuning>("Assets/PortCargo/Data/GameTuning.asset");
var so = new UnityEditor.SerializedObject(tuning);
so.FindProperty("nativeAdsEnabled").boolValue = true;
so.FindProperty("homeNativeAdUnitId").stringValue = "adunit-01b778c6152d4eb5";
so.FindProperty("completedNativeAdUnitId").stringValue = "adunit-019b3134e49d8311";
so.FindProperty("gameplayGridAdUnitId").stringValue = "adunit-2060c1fccf2ceecf";
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(tuning);
UnityEditor.AssetDatabase.SaveAssets();
return "Home card, shared completed/pause/settings card and independent gameplay grid configured; rewarded IDs retained.";
