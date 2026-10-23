"""Restore simulator storage with Unity completely stopped. Temporarily uses a minimal entry
in the same project path so its wx storage namespace is identical. Always restores game.js bytes.
"""
import hashlib,json,runpy,subprocess,time
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE'];project=b['PROJECT']
def close():
 r=call('close_project_window');assert r.get('ok'),r
 for _ in range(30):
  p=subprocess.run(['/tmp/portcargo-wechat-native'],text=True,capture_output=True,timeout=10)
  if p.returncode:return
  time.sleep(.25)
 raise Exception('Window did not close')
def open_project():
 r=call('open_project_window',window_mode='fullMode');assert r.get('ok') and r['result'].get('type')=='newopen',r
 return r
def wait_log(marker):
 for _ in range(20):
  r=call('get_simulator_console',command='grep -n .')
  if r.get('ok') and marker in str(r.get('result')):return r
  time.sleep(.5)
 raise Exception('Runtime marker absent: '+marker)
backup=json.loads((e/'runtime-inspect.json').read_text())['result']['result']['storage']
original=max((json.loads(x['value']) for x in backup),key=lambda x:x['revision'])
entry=project/'game.js';raw=entry.read_bytes();fingerprint=hashlib.sha256(raw).hexdigest();recovery=e/'recovery';recovery.mkdir(exist_ok=True)
(recovery/'game.js.original').write_bytes(raw)
close()
helper='''const entries=ENTRYDATA;
entries.forEach(x=>wx.setStorageSync(x.key,x.value));
console.log('PORTCARGO_RESTORE_HELPER_DONE', entries.map(x=>({key:x.key,revision:JSON.parse(x.value).revision})));
const canvas=wx.createCanvas();const ctx=canvas.getContext('2d');ctx.fillStyle='#17243c';ctx.fillRect(0,0,canvas.width,canvas.height);
ctx.fillStyle='#ffffff';ctx.font='18px sans-serif';ctx.fillText('Original progress restored',20,80);
'''.replace('ENTRYDATA',json.dumps(backup,ensure_ascii=False))
try:
 entry.write_text(helper)
 open_project();wait_log('PORTCARGO_RESTORE_HELPER_DONE')
 fn=Path(__file__).with_name('read-state-v06.js').read_text();r=call('automation_evaluate',fn_source=fn)
 assert r.get('ok') and r['result'].get('success'),r
 assert r['result']['result']['payload']==json.loads(original['payload']),r
 (e/'baseline-restored-by-helper.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 close()
finally:
 entry.write_bytes(raw)
assert hashlib.sha256(entry.read_bytes()).hexdigest()==fingerprint
open_project();wait_log('游戏启动耗时')
fn=Path(__file__).with_name('read-state-v06.js').read_text();r=call('automation_evaluate',fn_source=fn)
assert r.get('ok') and r['result'].get('success'),r
after=r['result']['result'];assert after['payload']==json.loads(original['payload']),('Original progress not restored',after)
assert after['checksum']==original['checksum']
(e/'baseline-restored-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
r=call('simulator_screenshot',path=str(e/'baseline-restored-home.jpg'));assert r.get('ok'),r
(e/'baseline-restored-home.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
(e/'baseline-restore-result.json').write_text(json.dumps({'PASS':True,'exactOriginalPayload':True,'originalPayloadChecksum':original['checksum'],'restoredPayloadChecksum':after['checksum'],'restoredRunId':after['payload']['activeRun']['runId'],'restoredLevel':after['payload']['activeRun']['levelId'],'originalEntrySha256':fingerprint,'entryBytesRestored':True,'originalSimulatorDeviceRestored':True,'method':'Unity stopped; minimal temporary entry at same project path; original game.js restored byte-for-byte before final reopen'},ensure_ascii=False,indent=2)+'\n')
print('PASS original progress restored and read in final Unity player; identical checksum; game.js restored byte-for-byte')
