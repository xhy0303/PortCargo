using System;
using System.Reflection;
using UnityEditor;
public static class GameViewSizeTool {
 public static object Set(int width,int height) {
  var assembly=typeof(Editor).Assembly;
  var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
  var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
  var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
  var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.ToObject(groupType,0)});
  var sizeType=assembly.GetType("UnityEditor.GameViewSize");
  var fixedType=assembly.GetType("UnityEditor.GameViewSizeType");
  var size=Activator.CreateInstance(sizeType,new[]{Enum.ToObject(fixedType,1),(object)width,height,"PortCargo "+width+"x"+height});
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
  var total=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
  var viewType=assembly.GetType("UnityEditor.GameView");
  var view=EditorWindow.GetWindow(viewType);
  viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,total-1);
  view.Focus();view.Repaint();
  return "Game View set to "+width+"x"+height;
 }
}
