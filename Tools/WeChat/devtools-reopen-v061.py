import json,runpy,subprocess,time
from pathlib import Path
b=runpy.run_path(str(Path(__file__).with_name('devtools-v061.py')),run_name='bridge');call=b['call'];e=b['EVIDENCE']
r=call('close_project_window');assert r.get('ok'),r
log=[r]
# Observe actual window destruction; close returns before destruction finishes.
for i in range(20):
 p=subprocess.run(['/tmp/portcargo-wechat-native'],capture_output=True,text=True,timeout=10)
 if p.returncode!=0:break
 time.sleep(.25)
else:raise Exception('Project window did not close')
r=call('open_project_window',window_mode='fullMode');assert r.get('ok') and r['result'].get('type')=='newopen',r
log.append(r)
for i in range(12):
 c=call('get_simulator_console',command='grep -n .')
 if c.get('ok') and '游戏启动耗时' in str(c.get('result')):break
 time.sleep(.5)
else:raise Exception('Restart did not reach Unity callMain completion')
fn=Path(__file__).with_name('read-state-v06.js').read_text();r=call('automation_evaluate',fn_source=fn)
assert r.get('ok') and r['result'].get('success'),r
(e/'after-reopen.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
before=json.loads((e/'before-reopen.json').read_text())['result']['result'];after=r['result']['result']
assert after['revision']>before['revision']
for key in ['activeRun','endless','profile']:
 assert before['payload'][key]==after['payload'][key],('restart changed saved field',key)
(e/'reopen-result.json').write_text(json.dumps({'PASS':True,'newWindow':log[1],'beforeRevision':before['revision'],'afterRevision':after['revision'],'exactCampaignRunAndUndo':True,'exactEndlessRunAndUndo':True,'exactProfile':True},ensure_ascii=False,indent=2)+'\n')
print('PASS WeChat project close/new-window reopen; exact both-mode run, full undo, rights and profile retained')
