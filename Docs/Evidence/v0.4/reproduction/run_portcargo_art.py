from pathlib import Path
import subprocess,json,time,sys
p=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo');e=p/'Docs/Evidence/v0.4';app=p/'Builds/macOS/20261016-flat-pixel-validation/PortCargo.app/Contents/MacOS/PortCargo'
run=sys.argv[1];save='/tmp/portcargo-art-flow-final' if run in ['flow','read'] else '/tmp/portcargo-art-'+run+'-final'
flag={'flow':'--portcargo-check-flow','read':'--portcargo-check-flow-read','edges':'--portcargo-check-flow-edges','dense':'--portcargo-check-art'}[run]
prefix=e/('art-flow' if run in ['flow','read'] else 'art-'+run)
size={'flow':(320,568),'read':(360,800),'edges':(720,1280),'dense':(320,568)}[run]
with (e/(run+'-stdout.log')).open('w') as out:
 proc=subprocess.Popen([str(app),'--portcargo-save-dir',save,flag,str(prefix),'-screen-width',str(size[0]),'-screen-height',str(size[1]),'-logFile',str(e/(run+'.log'))],stdout=out,stderr=subprocess.STDOUT)
 print('RUN',run,'PID',proc.pid,flush=True);start=time.monotonic()
 try:code=proc.wait(timeout=180)
 except subprocess.TimeoutExpired:proc.terminate();code=proc.wait(timeout=20)
 result={'run':run,'pid':proc.pid,'exitCode':code,'seconds':round(time.monotonic()-start,2)};(e/(run+'-process.json')).write_text(json.dumps(result,indent=2));print(result,flush=True)
 sys.exit(code)
