import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
PROJECT=ROOT/'Builds/WeChat/20261018-v0.6/minigame'
EVIDENCE=ROOT/'Docs/Evidence/v0.6/devtools'
CLI='/Applications/wechatwebdevtools.app/Contents/MacOS/wechatide'
def call(tool,**options):
 args=[CLI,'-c','Codex',tool,'--project',str(PROJECT)]
 for key,value in options.items():
  if value is None:continue
  args+=['--'+key.replace('_','-'),str(value) if isinstance(value,(str,int,float)) else json.dumps(value,ensure_ascii=False)]
 p=subprocess.run(args,text=True,capture_output=True,timeout=50)
 start=p.stdout.find('{')
 if start<0:raise Exception(p.stderr+p.stdout)
 result=json.loads(p.stdout[start:]);return result
if __name__=='__main__':
 mode=sys.argv[1];name=sys.argv[2]
 if mode=='native':
  proc=subprocess.run(['/tmp/portcargo-wechat-native','gameclick',*sys.argv[3:7]],capture_output=True,text=True,timeout=15)
  if proc.returncode:raise Exception(proc.stderr+proc.stdout)
  r=json.loads(proc.stdout)
  with (EVIDENCE/'native-input.jsonl').open('a') as f:f.write(json.dumps(dict(name=name,**r))+'\n')
 elif mode=='shot':r=call('simulator_screenshot',path=str(EVIDENCE/(name+'.jpg')))
 elif mode=='console':r=call('get_simulator_console',command='grep -n .')
 elif mode=='eval':r=call('automation_evaluate',fn_source=Path(sys.argv[3]).read_text())
 elif mode=='tap':r=call('automation_game_action',action='tap',x=float(sys.argv[3]),y=float(sys.argv[4]),coordinate_space='image',image_width=int(sys.argv[5]),image_height=int(sys.argv[6]))
 elif mode=='refresh':r=call('simulator_refresh')
 elif mode=='close':r=call('close_project_window')
 elif mode=='open':r=call('open_project_window',window_mode='fullMode')
 else:raise Exception(mode)
 (EVIDENCE/(name+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(r,ensure_ascii=False)[:1800])
 if r.get("ok") is False:sys.exit(2)
