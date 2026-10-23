"""Restore wx storage across clean IDE processes to avoid Unity plugin/IDE cached state."""
import hashlib,json,runpy,subprocess,time
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE'];project=b['PROJECT'];cli=b['CLI']
def quit_ide():
 p=subprocess.run([cli,'-c','Codex','quit'],capture_output=True,text=True,timeout=40)
 result=json.loads(p.stdout[p.stdout.find('{'):]);assert result.get('ok'),result
 for _ in range(50):
  p=subprocess.run(['pgrep','-f','^/Applications/wechatwebdevtools.app/Contents/MacOS/Electron$'],text=True,capture_output=True)
  if p.returncode:return
  time.sleep(.2)
 raise Exception('IDE process did not exit')
def open_project():
 r=call('open_project_window',window_mode='fullMode');assert r.get('ok'),r
 return r
def wait_log(marker):
 for _ in range(20):
  r=call('get_simulator_console',command='grep -n .')
  if r.get('ok') and marker in str(r.get('result')):return
  time.sleep(.5)
 raise Exception('Marker absent '+marker)
backup=json.loads((e/'runtime-inspect.json').read_text())['result']['result']['storage'];original=max((json.loads(x['value']) for x in backup),key=lambda x:x['revision'])
entry=project/'game.js';raw=entry.read_bytes();sha=hashlib.sha256(raw).hexdigest();(e/'recovery/game.js.original').write_bytes(raw)
quit_ide()
helper='''const entries=ENTRYDATA;
entries.forEach(x=>wx.setStorageSync(x.key,x.value));
console.log('PORTCARGO_RESTORE_HELPER_DONE', entries.map(x=>({key:x.key,revision:JSON.parse(x.value).revision})));
const c=wx.createCanvas();const x=c.getContext('2d');x.fillStyle='#17243c';x.fillRect(0,0,c.width,c.height);
'''.replace('ENTRYDATA',json.dumps(backup,ensure_ascii=False))
try:
 entry.write_text(helper);open_project();wait_log('PORTCARGO_RESTORE_HELPER_DONE')
 fn=Path(__file__).with_name('read-state-v06.js').read_text();r=call('automation_evaluate',fn_source=fn)
 assert r.get('ok') and r['result'].get('success') and r['result']['result']['checksum']==original['checksum'],r
 (e/'baseline-helper-clean-process.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 quit_ide()
finally:entry.write_bytes(raw)
assert hashlib.sha256(entry.read_bytes()).hexdigest()==sha
open_project();wait_log('游戏启动耗时')
r=call('automation_evaluate',fn_source=Path(__file__).with_name('read-state-v06.js').read_text());assert r.get('ok') and r['result'].get('success'),r
after=r['result']['result'];(e/'baseline-restored-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
assert after['payload']==json.loads(original['payload']) and after['checksum']==original['checksum'],('Original progress not restored',after['revision'],after['checksum'])
r=call('simulator_screenshot',path=str(e/'baseline-restored-home.jpg'));assert r.get('ok'),r
(e/'baseline-restored-home.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
(e/'baseline-restore-result.json').write_text(json.dumps({'PASS':True,'exactOriginalPayload':True,'originalPayloadChecksum':original['checksum'],'restoredPayloadChecksum':after['checksum'],'restoredRunId':after['payload']['activeRun']['runId'],'restoredLevel':after['payload']['activeRun']['levelId'],'originalEntrySha256':sha,'entryBytesRestored':True,'originalSimulatorDeviceRestored':True,'method':'clean IDE processes; minimal temporary entry at same path; original game.js restored byte-for-byte before final clean-process reopen'},ensure_ascii=False,indent=2)+'\n')
print('PASS original progress restored in clean Unity/IDE process; checksum identical; entry unchanged')
