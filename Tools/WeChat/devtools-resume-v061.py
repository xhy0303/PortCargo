import importlib.util,json,subprocess,sys,time
from pathlib import Path
spec=importlib.util.spec_from_file_location('bridge',Path(__file__).with_name('devtools-v061.py'));b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
fn=Path(__file__).with_name('read-state-v06.js').read_text()
def state():
 r=b.call('automation_evaluate',fn_source=fn);assert r.get('ok') and r['result'].get('success'),r
 return r['result']['result']['payload']
def click(x,y):
 p=subprocess.run(['/tmp/portcargo-wechat-native','gameclick',str(x),str(y),'591','1280'],capture_output=True,text=True,timeout=15);assert p.returncode==0,p.stdout+p.stderr
 return json.loads(p.stdout)
def wait_version(version):
 deadline=time.monotonic()+8
 while time.monotonic()<deadline:
  v=state()
  if v['endless']['run']['stateVersion']==version:return v
  time.sleep(.2)
 raise AssertionError('expected version '+str(version))
p=b.EVIDENCE/'random62-resume-expected.json'
if sys.argv[1]=='prepare':
 before=state();e=before['endless'];assert e['round']==62 and e['generatorVersion']==3 and e['run']['freeHintUsed'] and len(e['recentLayouts'])==3
 payload=b.EVIDENCE/'random62-payload.json';payload.write_text(json.dumps(before,ensure_ascii=False))
 r=subprocess.run(['dotnet',str(b.ROOT/'Tools/CoreChecks/bin/Debug/net7.0/CoreChecks.dll'),str(b.ROOT/'Assets/PortCargo/Data/Levels/catalog.json'),'--solve-endless',str(payload)],capture_output=True,text=True,timeout=15);assert r.returncode==0,r.stderr
 box=json.loads(r.stdout)[0];s=next(i for i,stack in enumerate(e['level']['stacks']) if stack['boxes'][-1]['id']==box)
 event=click(59+s*95,594);after=wait_version(e['run']['stateVersion']+1)
 assert after['endless']['run']['board']['heights'][s]==5
 p.write_text(json.dumps({'before':before,'after':after,'input':event},ensure_ascii=False,indent=2));print('PASS round62 partial move saved, hint used, 3 recent layouts')
else:
 expected=json.loads(p.read_text());actual=state();assert actual['endless']==expected['after']['endless'];assert actual['profile']==expected['after']['profile'];assert actual['activeRun']==expected['after']['activeRun']
 click(295,940);current=state();assert current['endless']==actual['endless']
 event=click(175,1140);undone=wait_version(actual['endless']['run']['stateVersion']+1)
 assert undone['endless']['run']['board']==expected['before']['endless']['run']['board'];assert undone['endless']['run']['freeHintUsed'];assert undone['endless']['recentLayouts']==actual['endless']['recentLayouts']
 (b.EVIDENCE/'random62-reopen-result.json').write_text(json.dumps({'PASS':True,'exactDefinitionAndRunRestored':True,'historyRestored':True,'campaignUnchanged':True,'realUndoRestoredBoard':True,'hintEntitlementPreserved':True,'input':event,'after':undone},ensure_ascii=False,indent=2));print('PASS new window restores exact level/run/board/history; real resume and undo preserve hint entitlement')
