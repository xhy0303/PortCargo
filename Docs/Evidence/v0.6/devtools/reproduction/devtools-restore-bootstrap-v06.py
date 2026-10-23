"""Restore through the production C# repository inside Unity's runtime context, then remove
all temporary code and verify original files and payload in a clean IDE process.
"""
import hashlib,json,runpy,subprocess,time
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE'];project=b['PROJECT'];cli=b['CLI']
def quit_ide():
 p=subprocess.run([cli,'-c','Codex','quit'],capture_output=True,text=True,timeout=40);r=json.loads(p.stdout[p.stdout.find('{'):]);assert r.get('ok'),r
 for _ in range(50):
  if subprocess.run(['pgrep','-f','^/Applications/wechatwebdevtools.app/Contents/MacOS/Electron$'],capture_output=True).returncode:return
  time.sleep(.2)
 raise Exception('IDE did not quit')
def open_ready():
 r=call('open_project_window',window_mode='fullMode');assert r.get('ok'),r
 for _ in range(20):
  r=call('get_simulator_console',command='grep -n .')
  if r.get('ok') and '游戏启动耗时' in str(r.get('result')):return r
  time.sleep(.5)
 raise Exception('Unity did not start')
backup=json.loads((e/'runtime-inspect.json').read_text())['result']['result']['storage'];original=max((json.loads(x['value']) for x in backup),key=lambda x:x['revision']);mapping={x['key']:x['value'] for x in backup}
path=project/'webgl.wasm.framework.unityweb.js';raw=path.read_bytes();sha=hashlib.sha256(raw).hexdigest();(e/'recovery/framework.js.original').write_bytes(raw)
s=raw.decode();needle='function _PortCargoStorageRead(keyPointer){var result;try{var value=wx.getStorageSync(UTF8ToString(keyPointer));';assert s.count(needle)==1
replacement='function _PortCargoStorageRead(keyPointer){var result;try{var restoreKey=UTF8ToString(keyPointer);var restoreMap='+json.dumps(mapping)+';Module.__portcargoRestoreReads=Module.__portcargoRestoreReads||{};var restoreCount=Module.__portcargoRestoreReads[restoreKey]||0;Module.__portcargoRestoreReads[restoreKey]=restoreCount+1;var value=(Object.prototype.hasOwnProperty.call(restoreMap,restoreKey)&&restoreCount<2)?restoreMap[restoreKey]:wx.getStorageSync(restoreKey);if(restoreCount<2)console.log("PORTCARGO_RESTORE_BOOTSTRAP",restoreKey,restoreCount);'
quit_ide()
try:
 path.write_text(s.replace(needle,replacement));console=open_ready()
 (e/'restore-bootstrap-console.json').write_text(json.dumps(console,ensure_ascii=False,indent=2)+'\n')
 r=call('automation_evaluate',fn_source=Path(__file__).with_name('read-state-v06.js').read_text());assert r.get('ok') and r['result'].get('success'),r
 (e/'restore-bootstrap-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 assert r['result']['result']['payload']==json.loads(original['payload']),('Bootstrap did not load original snapshot',r['result']['result']['revision'])
 quit_ide()
finally:path.write_bytes(raw)
assert hashlib.sha256(path.read_bytes()).hexdigest()==sha
open_ready();r=call('automation_evaluate',fn_source=Path(__file__).with_name('read-state-v06.js').read_text());assert r.get('ok') and r['result'].get('success'),r
after=r['result']['result'];(e/'baseline-restored-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
assert after['payload']==json.loads(original['payload']) and after['checksum']==original['checksum'],('Final original snapshot mismatch',after['revision'],after['checksum'])
r=call('simulator_screenshot',path=str(e/'baseline-restored-home.jpg'));assert r.get('ok'),r
(e/'baseline-restored-home.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
entry=project/'game.js';entryOriginal=(e/'recovery/game.js.original').read_bytes();assert entry.read_bytes()==entryOriginal
(e/'baseline-restore-result.json').write_text(json.dumps({'PASS':True,'exactOriginalPayload':True,'originalPayloadChecksum':original['checksum'],'restoredPayloadChecksum':after['checksum'],'restoredRunId':after['payload']['activeRun']['runId'],'restoredLevel':after['payload']['activeRun']['levelId'],'frameworkSha256':sha,'frameworkBytesRestored':True,'entryBytesRestored':True,'originalSimulatorDeviceRestored':True,'method':'two original bootstrap reads per slot inside Unity context, normal C# repository persists; temporary framework removed; clean IDE process verifies original snapshot'},ensure_ascii=False,indent=2)+'\n')
print('PASS original snapshot restored through Unity repository; clean IDE restart preserves original checksum; all temporary entry/framework changes removed')
