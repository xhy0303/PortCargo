"""Drive actual macOS mouse input into the WeChat simulator; observe wx-backed saves after every move.
Coordinates are calibrated from the official 591x1280 simulator screenshot and native AX runtime frame.
Never calls game APIs to execute a move. High-level fixtures must be installed separately and backed up.
"""
import importlib.util,json,re,subprocess,sys,time
from pathlib import Path
spec=importlib.util.spec_from_file_location('bridge',Path(__file__).with_name('devtools-v061.py'));b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
mode=sys.argv[1]
fn=Path(__file__).with_name('read-state-v06.js').read_text()
def state():
 r=b.call('automation_evaluate',fn_source=fn)
 assert r.get('ok') and r['result'].get('success'),r
 return r['result']['result']
def run(st):return st['payload']['endless']['run'] if mode=='endless' else st['payload']['activeRun']
def click(x,y,label):
 p=subprocess.run(['/tmp/portcargo-wechat-native','gameclick',str(x),str(y),'591','1280'],capture_output=True,text=True,timeout=15)
 assert p.returncode==0,p.stdout+p.stderr
 e=json.loads(p.stdout);e['name']=label
 with (b.EVIDENCE/'native-input.jsonl').open('a') as f:f.write(json.dumps(e)+'\n')
 return e
st=state();cached=st;current=run(st);assert st['payload']['mode']==('endless' if mode=='endless' else 'campaign')
if mode=='campaign':
 level=next(l for l in json.loads((b.ROOT/'Assets/PortCargo/Data/Levels/catalog.json').read_text())['levels'] if l['id']==current['levelId'])
 solution=next(l for l in json.loads((b.EVIDENCE.parent/'validated-solutions.json').read_text()) if l['levelId']==level['id'])['solutionBoxIds']
else:
 end=st['payload']['endless'];level=end['level']
 payload=b.EVIDENCE/'current-solve-payload.json';payload.write_text(json.dumps(st['payload'],ensure_ascii=False))
 solved=subprocess.run(['dotnet',str(b.ROOT/'Tools/CoreChecks/bin/Debug/net7.0/CoreChecks.dll'),str(b.ROOT/'Assets/PortCargo/Data/Levels/catalog.json'),'--solve-endless',str(payload)],capture_output=True,text=True,timeout=15)
 assert solved.returncode==0,solved.stderr
 solution=json.loads(solved.stdout)
 (b.EVIDENCE/'random61-solution.json').write_text(json.dumps(solution)+'\n')
assert current['board']['heights']==[len(s['boxes']) for s in level['stacks']],current
log=[]
def move(box,label):
 global cached
 before=cached;r=run(before)
 s=next(s for s,stack in enumerate(level['stacks']) if any(x['id']==box for x in stack['boxes']))
 h=r['board']['heights'][s];assert h>0 and level['stacks'][s]['boxes'][h-1]['id']==box,(box,r['board'])
 event=click(59+s*95,814-(h-1)*44,label)
 after=state();cached=after;new=run(after)
 assert new['stateVersion']==r['stateVersion']+1,(label,'click did not reach production input',new)
 assert new['board']['heights'][s]==h-1
 log.append({'step':label,'box':box,'input':event,'beforeVersion':r['stateVersion'],'afterVersion':new['stateVersion'],'board':new['board'],'freeHintUsed':new['freeHintUsed']})
 return after
# Undo one real move and confirm both board and hint entitlement survive.
move(solution[0],mode+'-undo-probe');before=run(state());click(175,1140,mode+'-undo')
cached=state();after=run(cached);assert after['stateVersion']==before['stateVersion']+1 and after['board']==current['board'] and after['freeHintUsed']==current['freeHintUsed']
log.append({'step':'undo-restores-exact-board-and-hint','PASS':True})
for i,box in enumerate(solution):
 final=move(box,mode+'-solution-'+str(i+1))
 print(mode,i+1,'/',len(solution),'PASS',flush=True)
 (b.EVIDENCE/(mode+'-pointer-replay.json')).write_text(json.dumps(log,ensure_ascii=False,indent=2)+'\n')
assert run(final)['board']['shipIndex']==len(level['ships']) and all(h==0 for h in run(final)['board']['heights'])
(b.EVIDENCE/(mode+'-completed-state.json')).write_text(json.dumps(final,ensure_ascii=False,indent=2)+'\n')
print('PASS',mode,'real pointer completion, wx storage after every move, undo and hint rights',flush=True)
