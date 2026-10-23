import Cocoa
import ApplicationServices
import CoreGraphics
let args=CommandLine.arguments
func out(_ value:[String:Any]) {let data=try! JSONSerialization.data(withJSONObject:value,options:[.sortedKeys]);print(String(data:data,encoding:.utf8)!)}
func attr(_ e:AXUIElement,_ key:String)->CFTypeRef?{var v:CFTypeRef?;let result=AXUIElementCopyAttributeValue(e,key as CFString,&v);return result == .success ? v:nil}
guard args.count>=3,let pid=Int32(args[2]) else {out(["error":"usage: window|click PID [u v]"]);exit(1)}
let app=AXUIElementCreateApplication(pid)
guard let process=NSRunningApplication(processIdentifier:pid),process.bundleIdentifier=="com.portcargo.prototype" else {out(["error":"target is not PortCargo"]);exit(1)}
guard AXIsProcessTrusted() && CGPreflightPostEventAccess() else {out(["error":"accessibility permission unavailable"]);exit(2)}
guard process.activate() else {out(["error":"could not activate target game"]);exit(8)}
Thread.sleep(forTimeInterval:1)
guard NSWorkspace.shared.frontmostApplication?.processIdentifier==pid else {out(["error":"target game is not frontmost"]);exit(9)}
var resolvedWindow:AXUIElement?
for _ in 0..<20 {
 if let windows=attr(app,kAXWindowsAttribute) as? [AXUIElement],let first=windows.first {resolvedWindow=first;break}
 Thread.sleep(forTimeInterval:0.25)
}
guard let window=resolvedWindow else {
 let list=CGWindowListCopyWindowInfo([.optionAll,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]] ?? []
 let matching=list.filter{($0[kCGWindowOwnerPID as String] as? Int32)==pid}
 out(["error":"no AX app window","pid":pid,"matchingCGWindows":matching]);exit(3)
}
var point=CGPoint.zero;var size=CGSize.zero
if let p=attr(window,kAXPositionAttribute){AXValueGetValue(p as! AXValue,.cgPoint,&point)}
if let s=attr(window,kAXSizeAttribute){AXValueGetValue(s as! AXValue,.cgSize,&size)}
let title=attr(window,kAXTitleAttribute) as? String ?? ""
// Unity launch arguments are render pixels; macOS AX bounds are desktop points.
let renderWidth=Double(ProcessInfo.processInfo.environment["PORTCARGO_TEST_WIDTH"] ?? "360")!
let renderHeight=Double(ProcessInfo.processInfo.environment["PORTCARGO_TEST_HEIGHT"] ?? "800")!
let contentHeight=size.width*renderHeight/renderWidth
let titleHeight=size.height-contentHeight
guard titleHeight>=18 && titleHeight<=60 else {out(["error":"window does not match requested viewport aspect","width":size.width,"height":size.height]);exit(7)}
let content=CGRect(x:point.x,y:point.y+titleHeight,width:size.width,height:size.height-titleHeight)
if args[1]=="close" {
 guard let button=attr(window,kAXCloseButtonAttribute) else {out(["error":"no close button"]);exit(5)}
 let result=AXUIElementPerformAction(button as! AXUIElement,kAXPressAction as CFString)
 out(["action":"native-window-close","pid":pid,"result":result.rawValue])
 if result != .success {exit(6)}
} else if args[1]=="click" {
 guard args.count==5,let u=Double(args[3]),let v=Double(args[4]),u>=0,u<=1,v>=0,v<=1 else {exit(4)}
 let position=CGPoint(x:content.minX+content.width*u,y:content.minY+content.height*v)
 let source=CGEventSource(stateID:.hidSystemState)
 let move=CGEvent(mouseEventSource:source,mouseType:.mouseMoved,mouseCursorPosition:position,mouseButton:.left)!
 move.post(tap:.cghidEventTap);Thread.sleep(forTimeInterval:0.1)
 let down=CGEvent(mouseEventSource:source,mouseType:.leftMouseDown,mouseCursorPosition:position,mouseButton:.left)!
 down.post(tap:.cghidEventTap);Thread.sleep(forTimeInterval:0.12)
 let up=CGEvent(mouseEventSource:source,mouseType:.leftMouseUp,mouseCursorPosition:position,mouseButton:.left)!
 up.post(tap:.cghidEventTap)
 out(["action":"click","pid":pid,"title":title,"x":position.x,"y":position.y,"contentWidth":content.width,"contentHeight":content.height])
} else {
 out(["pid":pid,"title":title,"x":point.x,"y":point.y,"width":size.width,"height":size.height,"titlebar":titleHeight,"contentWidth":content.width,"contentHeight":content.height])
}
