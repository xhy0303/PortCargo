import json,runpy,subprocess,time
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE']
def reopen():
 r=call('close_project_window');assert r.get('ok'),r
 for _ in range(30):
  p=subprocess.run(['/tmp/portcargo-wechat-native'],capture_output=True,text=True,timeout=10)
  if p.returncode:break
  time.sleep(.25)
 else:raise Exception('Window did not close')
 r=call('open_project_window',window_mode='fullMode');assert r.get('ok') and r['result'].get('type')=='newopen',r
 for _ in range(15):
  c=call('get_simulator_console',command='grep -n .')
  if c.get('ok') and '游戏启动耗时' in str(c.get('result')):return
  time.sleep(.5)
 raise Exception('Unity did not finish startup')
r=call('simulator_set_device',device='iPhone 12/13 (Pro)');assert r.get('ok') and r['result'].get('success'),r
(e/'device-restored.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
reopen()
backup=json.loads((e/'runtime-inspect.json').read_text())['result']['result']['storage'];original=max((json.loads(x['value']) for x in backup),key=lambda x:x['revision'])
slots=[]
for i,x in enumerate(backup):
 entry=json.loads(x['value']);entry['revision']=100000+i;slots.append({'key':x['key'],'value':json.dumps(entry,separators=(',',':'),ensure_ascii=False)})
fn='function(){const slots='+json.dumps(slots)+';slots.forEach(x=>wx.setStorageSync(x.key,x.value));return {restored:slots.length};}'
r=call('automation_evaluate',fn_source=fn);assert r.get('ok') and r['result'].get('success'),r
(e/'baseline-restore-write.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
reopen()
fn=Path(__file__).with_name('read-state-v06.js').read_text();r=call('automation_evaluate',fn_source=fn);assert r.get('ok') and r['result'].get('success'),r
(e/'baseline-restored-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
after=r['result']['result'];assert after['payload']==json.loads(original['payload']),('Original progress not restored',after)
assert after['checksum']==original['checksum']
r=call('simulator_screenshot',path=str(e/'baseline-restored-home.jpg'));assert r.get('ok'),r
(e/'baseline-restored-home.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
(e/'baseline-restore-result.json').write_text(json.dumps({'PASS':True,'exactOriginalPayload':True,'originalPayloadChecksum':original['checksum'],'restoredPayloadChecksum':after['checksum'],'restoredRunId':after['payload']['activeRun']['runId'],'restoredLevel':after['payload']['activeRun']['levelId'],'slotRevisionsAdvancedToPreventStaleWrites':True,'originalSimulatorDeviceRestored':True},ensure_ascii=False,indent=2)+'\n')
print('PASS original WeChat progress restored, checksum identical, read after fresh project reopen; original simulator model restored')
