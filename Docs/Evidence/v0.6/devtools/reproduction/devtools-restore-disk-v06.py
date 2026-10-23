"""One-off rollback of this run's simulator storage, with IDE stopped.
Requires explicit flag because the captured baseline belongs to this dated acceptance run.
"""
import hashlib,json,runpy,subprocess,sys,time
from pathlib import Path
assert '--restore-captured-baseline' in sys.argv,'Explicit --restore-captured-baseline required'
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE'];cli=b['CLI']
base=Path('/Users/xhy/Library/Application Support/微信开发者工具/d1e8765721a6c23d43b14c95b1843e6b/WeappSimulator/WeappStorage')
backup=json.loads((e/'runtime-inspect.json').read_text())['result']['result']['storage'];original=max((json.loads(x['value']) for x in backup),key=lambda x:x['revision'])
matched=[]
for p in base.glob('storage_*.json'):
 obj=json.loads(p.read_text());slots=obj.get('0',{})
 try:
  parsed=[json.loads(slots[k]['data']) for k in ['portcargo.save.a','portcargo.save.b']]
  if any(json.loads(x['payload']).get('profile',{}).get('unlockedCount')==200 for x in parsed):matched.append(p)
 except (KeyError,ValueError,TypeError):pass
assert len(matched)==1,matched
path=matched[0]
p=subprocess.run([cli,'-c','Codex','quit'],capture_output=True,text=True,timeout=40);r=json.loads(p.stdout[p.stdout.find('{'):]);assert r.get('ok'),r
for _ in range(50):
 if subprocess.run(['pgrep','-f','^/Applications/wechatwebdevtools.app/Contents/MacOS/Electron$'],capture_output=True).returncode:break
 time.sleep(.2)
else:raise Exception('IDE did not quit')
raw=path.read_bytes();obj=json.loads(raw);(e/'recovery/simulator-storage-before-rollback.json').write_bytes(raw)
untouched={k:v for k,v in obj['0'].items() if not k.startswith('portcargo.save.')};other={k:v for k,v in obj.items() if k!='0'}
for item in backup:
 assert obj['0'][item['key']]['dataType']=='String'
 obj['0'][item['key']]['data']=item['value']
assert untouched=={k:v for k,v in obj['0'].items() if not k.startswith('portcargo.save.')}
assert other=={k:v for k,v in obj.items() if k!='0'}
path.write_text(json.dumps(obj,ensure_ascii=False,separators=(',',':')))
for item in backup:assert json.loads(path.read_text())['0'][item['key']]['data']==item['value']
r=call('open_project_window',window_mode='fullMode');assert r.get('ok'),r
for _ in range(20):
 r=call('get_simulator_console',command='grep -n .')
 if r.get('ok') and '游戏启动耗时' in str(r.get('result')):break
 time.sleep(.5)
else:raise Exception('Unity did not start')
r=call('automation_evaluate',fn_source=Path(__file__).with_name('read-state-v06.js').read_text());assert r.get('ok') and r['result'].get('success'),r
after=r['result']['result'];(e/'baseline-restored-state.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
assert after['payload']==json.loads(original['payload']) and after['checksum']==original['checksum'],('Original snapshot mismatch',after['revision'],after['checksum'])
r=call('simulator_screenshot',path=str(e/'baseline-restored-home.jpg'));assert r.get('ok'),r
(e/'baseline-restored-home.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
assert (b['PROJECT']/'game.js').read_bytes()==(e/'recovery/game.js.original').read_bytes()
assert (b['PROJECT']/'webgl.wasm.framework.unityweb.js').read_bytes()==(e/'recovery/framework.js.original').read_bytes()
(e/'baseline-restore-result.json').write_text(json.dumps({'PASS':True,'exactOriginalPayload':True,'originalPayloadChecksum':original['checksum'],'restoredPayloadChecksum':after['checksum'],'restoredRunId':after['payload']['activeRun']['runId'],'restoredLevel':after['payload']['activeRun']['levelId'],'frameworkBytesRestored':True,'entryBytesRestored':True,'originalSimulatorDeviceRestored':True,'method':'IDE stopped; only two test-project simulator storage records restored from captured raw values; clean IDE process confirms original payload'},ensure_ascii=False,indent=2)+'\n')
print('PASS original progress restored in simulator host file and verified in clean Unity/IDE process; checksum identical; formal package bytes unchanged')
