"""Bounded calls to the installed official WeChat IDE skill CLI; no secrets stored."""
import json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
PROJECT=ROOT/'Builds/WeChat/20261025-v0.7.4-ad-layouts-font/minigame'
EVIDENCE=ROOT/'Docs/Evidence/v0.7.4/wechat'
CLI='/Applications/wechatwebdevtools.app/Contents/MacOS/wechatide'
def call(tool,**options):
    args=[CLI,'-c','Codex',tool,'--project',str(PROJECT)]
    for key,value in options.items():
        if value is not None:args+=['--'+key.replace('_','-'),str(value) if isinstance(value,(str,int,float)) else json.dumps(value,ensure_ascii=False)]
    p=subprocess.run(args,text=True,capture_output=True,timeout=55)
    start=p.stdout.find('{')
    if start<0:raise RuntimeError(p.stderr+p.stdout)
    return json.loads(p.stdout[start:])
if __name__=='__main__':
    mode,name=sys.argv[1:3];EVIDENCE.mkdir(parents=True,exist_ok=True)
    if mode=='shot':r=call('simulator_screenshot',path=str(EVIDENCE/(name+'.jpg')))
    elif mode=='console':r=call('get_simulator_console',command='grep -n .')
    elif mode=='eval':r=call('automation_evaluate',fn_source=Path(sys.argv[3]).read_text())
    elif mode=='tap':r=call('automation_game_action',action='tap',x=float(sys.argv[3]),y=float(sys.argv[4]),coordinate_space='image',image_width=int(sys.argv[5]),image_height=int(sys.argv[6]))
    elif mode=='open':r=call('open_project_window',window_mode='fullMode')
    elif mode=='refresh':r=call('simulator_refresh')
    elif mode=='upload':r=call('upload',upload_version='0.7.4',desc='保留0.7.3广告布局与接入，修正隐私说明精简字库缺字')
    else:raise ValueError(mode)
    (EVIDENCE/(name+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(r,ensure_ascii=False)[:7000])
    if r.get('ok') is False:sys.exit(2)
