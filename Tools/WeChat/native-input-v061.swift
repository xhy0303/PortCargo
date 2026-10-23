import Cocoa
import ApplicationServices
import CoreGraphics
let args=CommandLine.arguments
func out(_ obj:[String:Any]){print(String(data:try! JSONSerialization.data(withJSONObject:obj,options:[.sortedKeys]),encoding:.utf8)!)}
func attr(_ node:AXUIElement,_ key:String)->CFTypeRef?{var v:CFTypeRef?;return AXUIElementCopyAttributeValue(node,key as CFString,&v) == .success ? v:nil}
guard let app=NSWorkspace.shared.runningApplications.first(where:{($0.bundleURL?.path ?? "")=="/Applications/wechatwebdevtools.app" && ($0.localizedName ?? "")=="微信开发者工具"}) else{out(["error":"target absent"]);exit(1)}
guard AXIsProcessTrusted(),CGPreflightPostEventAccess() else{out(["error":"input permission absent"]);exit(2)}
app.activate(options:[]);Thread.sleep(forTimeInterval:0.2)
let pid=app.processIdentifier;let ax=AXUIElementCreateApplication(pid)
AXUIElementSetAttributeValue(ax,"AXManualAccessibility" as CFString,kCFBooleanTrue)
let windows=attr(ax,kAXWindowsAttribute) as? [AXUIElement] ?? []
if args.count>1 && args[1]=="closepopup" {
 guard let w=windows.first(where:{(attr($0,kAXTitleAttribute) as? String ?? "").contains("港口装箱的模拟器")}),let close=attr(w,kAXCloseButtonAttribute) else{out(["error":"popup absent"]);exit(3)}
 let r=AXUIElementPerformAction(close as! AXUIElement,kAXPressAction as CFString);out(["action":"close-simulator-popup","result":r.rawValue]);exit(r == .success ? 0:4)
}
func find(_ n:AXUIElement,_ depth:Int)->AXUIElement? {
 if depth>22{return nil}
 if (attr(n,kAXRoleAttribute) as? String)=="AXWebArea" && (attr(n,kAXTitleAttribute) as? String)=="小游戏运行时" {return n}
 for c in (attr(n,kAXChildrenAttribute) as? [AXUIElement] ?? []) {if let match=find(c,depth+1){return match}}
 return nil
}
var canvas:AXUIElement?
for w in windows {if let match=find(w,0){canvas=match;break}}
guard let canvas=canvas else{out(["error":"runtime web area absent"]);exit(5)}
var origin=CGPoint.zero;var size=CGSize.zero
if let v=attr(canvas,kAXPositionAttribute){AXValueGetValue(v as! AXValue,.cgPoint,&origin)}
if let v=attr(canvas,kAXSizeAttribute){AXValueGetValue(v as! AXValue,.cgSize,&size)}
guard size.width>100 && size.height>100 else {out(["error":"runtime frame invalid"]);exit(6)}
if args.count==6 && args[1]=="gameclick",let x=Double(args[2]),let y=Double(args[3]),let iw=Double(args[4]),let ih=Double(args[5]),x>=0,y>=0,x<iw,y<ih {
 guard NSWorkspace.shared.frontmostApplication?.processIdentifier==pid else{out(["error":"target not frontmost"]);exit(7)}
 let p=CGPoint(x:origin.x+size.width*x/iw,y:origin.y+size.height*y/ih);let source=CGEventSource(stateID:.hidSystemState)
 for kind in [CGEventType.mouseMoved,.leftMouseDown,.leftMouseUp]{CGEvent(mouseEventSource:source,mouseType:kind,mouseCursorPosition:p,mouseButton:.left)?.postToPid(pid);Thread.sleep(forTimeInterval:0.10)}
 out(["action":"native-targeted-game-click","x":p.x,"y":p.y,"sourceX":x,"sourceY":y,"frameX":origin.x,"frameY":origin.y,"frameWidth":size.width,"frameHeight":size.height,"pid":pid])
} else {out(["pid":pid,"frameX":origin.x,"frameY":origin.y,"frameWidth":size.width,"frameHeight":size.height])}
