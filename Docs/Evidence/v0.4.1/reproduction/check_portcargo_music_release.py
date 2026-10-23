from pathlib import Path
import subprocess,time,json,hashlib,sys,traceback
p=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo');e=p/'Docs/Evidence/v0.4.1'
app=p/'Builds/macOS/20261016-music-preview/PortCargo.app/Contents/MacOS/PortCargo'
base=Path('/Users/xhy/Library/Application Support/PortCargo/PortCargo/Saves')
events=[];procs=[]
def digest(x):return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def snapshot():
 valid=[]
 for f in base.glob('portcargo.save.*'):
  v=json.loads(f.read_text());assert hashlib.sha256(v['payload'].encode()).hexdigest()==v['checksum'];valid.append(v)
 v=max(valid,key=lambda a:a['revision']);s=json.loads(v['payload'])
 return {'revision':v['revision'],'campaign':digest(s.get('activeRun')),'endless':digest(s.get('endless')),'profile':s['profile'],'mode':s.get('mode') or 'campaign'}
before=snapshot()
def preserved():
 after=snapshot()
 for k in ['campaign','endless','profile','mode']:assert after[k]==before[k],'existing progress changed: '+k
 return after['revision']
def record(action,**data):
 events.append({'action':action,**data});print(json.dumps(events[-1]),flush=True)
 (e/'release-native-progress.json').write_text(json.dumps(events,indent=2))
def invoke(proc,action,*xy):
 assert proc.poll() is None,'ordinary player exited prematurely'
 result=subprocess.run(['/tmp/portcargo-native-window',action,str(proc.pid),*[str(v) for v in xy]],capture_output=True,text=True,timeout=15)
 assert result.returncode==0,result.stdout+result.stderr
 data=json.loads(result.stdout);record(action,**data) if 'action' not in data else record(action,details=data)
 time.sleep(2)
 return data
def launch(index):
 prior=snapshot()['revision'];f=(e/f'release-native-{index}-stdout.log').open('w')
 proc=subprocess.Popen([str(app),'-screen-width','360','-screen-height','800','-logFile',str(e/f'release-native-{index}.log')],cwd=p,stdout=f,stderr=subprocess.STDOUT);procs.append(proc)
 deadline=time.monotonic()+25
 while time.monotonic()<deadline and snapshot()['revision']<=prior:
  assert proc.poll() is None,'startup exited';time.sleep(.25)
 assert snapshot()['revision']>prior,'startup did not commit restored save'
 time.sleep(3)
 data=invoke(proc,'window');assert abs(data['contentHeight']/data['contentWidth']-800/360)<.01,data
 record('startup-preserved',pid=proc.pid,revision=preserved())
 return proc
def cancel_and_probe(proc):
 invoke(proc,'click',.5,.59433)
 # The old dialog's confirm location is blank on Home. It must not quit after cancellation.
 invoke(proc,'click',.5,.51887);assert proc.poll() is None,'cancel did not return to home'
 record('cancel-preserved',pid=proc.pid,revision=preserved())
def wait_exit(proc):
 code=proc.wait(timeout=15);assert code==0,code
 record('normal-exit',pid=proc.pid,exitCode=code,revision=preserved())
try:
 first=launch(1)
 invoke(first,'click',.85,.905);assert first.poll() is None,'home quit bypassed confirmation'
 cancel_and_probe(first)
 invoke(first,'click',.85,.905);invoke(first,'click',.5,.51887);wait_exit(first)
 second=launch(2);assert second.pid!=first.pid
 invoke(second,'close');assert second.poll() is None,'window close bypassed confirmation'
 cancel_and_probe(second)
 invoke(second,'close');invoke(second,'click',.5,.51887);wait_exit(second)
 result={'result':'PASS','ordinaryExecutableSha256':hashlib.sha256(app.read_bytes()).hexdigest(),'ordinaryManagedSha256':hashlib.sha256((app.parent.parent/'Resources/Data/Managed/PortCargo.Unity.dll').read_bytes()).hexdigest(),'distinctProcessIds':[a.pid for a in procs],'homeQuitConfirmed':True,'nativeWindowCloseConfirmed':True,'cancelReturnsHome':True,'twoNormalExitCodes':[a.returncode for a in procs],'separateProcessSaveRestore':True,'realProgressPreserved':True,'saveRevisionBefore':before['revision'],'saveRevisionAfter':preserved(),'events':events}
except Exception as ex:
 result={'result':'FAIL','error':str(ex),'events':events,'remainingOwnedPids':[a.pid for a in procs if a.poll() is None]};traceback.print_exc()
(e/'release-native-result.json').write_text(json.dumps(result,indent=2));print('RESULT',result['result'],flush=True)
if result['result']=='FAIL':
 print('Retaining owned player for diagnostics',flush=True);time.sleep(60)
sys.exit(0 if result['result']=='PASS' else 1)
