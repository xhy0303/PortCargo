import json,runpy,subprocess
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v06.py')),run_name='bridge');e=b['EVIDENCE'];call=b['call'];fn=Path(__file__).with_name('read-state-v06.js').read_text()
before=json.loads((e/'before-reopen.json').read_text())['result']['result']['payload'];checks=[]
def state():
 r=call('automation_evaluate',fn_source=fn);assert r.get('ok') and r['result'].get('success'),r
 return r['result']['result']
def click(x,y,label):
 p=subprocess.run(['/tmp/portcargo-wechat-native','gameclick',str(x),str(y),'591','1280'],text=True,capture_output=True,timeout=15);assert p.returncode==0,p.stdout+p.stderr
 with (e/'native-input.jsonl').open('a') as f:f.write(json.dumps(dict(name=label,**json.loads(p.stdout)))+'\n')
click(295,839,'restore-continue-campaign');s=state()['payload'];assert s['mode']=='campaign' and s['activeRun']==before['activeRun']
click(175,1140,'restore-undo-campaign');s=state()['payload'];r=s['activeRun'];assert r['stateVersion']==before['activeRun']['stateVersion']+1 and r['board']['heights']==[6]*6 and not r['undo']
checks.append('PASS L200 exact continue then real pointer undo after restart')
click(510,195,'restore-pause-campaign');click(420,850,'restore-home-campaign');click(295,940,'restore-continue-endless');s=state()['payload'];assert s['mode']=='endless' and s['endless']['round']==61 and s['endless']['run']==before['endless']['run']
click(175,1140,'restore-undo-endless');s=state()['payload'];r=s['endless']['run'];assert r['stateVersion']==before['endless']['run']['stateVersion']+1 and r['board']['heights']==[6]*6 and not r['undo']
checks.append('PASS endless61 exact continue then real pointer undo after restart')
assert s['profile']==before['profile']
(e/'resume-and-undo-result.json').write_text(json.dumps({'PASS':True,'checks':checks,'finalState':s},ensure_ascii=False,indent=2)+'\n')
print('\n'.join(checks))
