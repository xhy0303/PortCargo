from pathlib import Path
import subprocess,json,time,sys
p=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo');e=p/'Docs/Evidence/v0.4.1';app=p/'Builds/macOS/20261016-music-validation/PortCargo.app/Contents/MacOS/PortCargo'
run=sys.argv[1];save='/tmp/portcargo-v041-flow' if run in ['flow','read'] else '/tmp/portcargo-v041-'+run
flag={'music':'--portcargo-check-music','flow':'--portcargo-check-flow','read':'--portcargo-check-flow-read','edges':'--portcargo-check-flow-edges','dense':'--portcargo-check-art','technical':'--portcargo-check-technical','sweep':'--portcargo-check-sweep'}[run]
prefix=e/('flow' if run in ['flow','read'] else run)
with (e/(run+'-stdout.log')).open('w') as out:
 proc=subprocess.Popen([str(app),'--portcargo-save-dir',save,flag,str(prefix),'-screen-width','360','-screen-height','800','-logFile',str(e/(run+'.log'))],stdout=out,stderr=subprocess.STDOUT)
 print('RUN',run,'PID',proc.pid,flush=True);start=time.monotonic()
 try:code=proc.wait(timeout=800)
 except subprocess.TimeoutExpired:proc.terminate();code=proc.wait(timeout=20)
 result={'run':run,'pid':proc.pid,'exitCode':code,'seconds':round(time.monotonic()-start,2)};(e/(run+'-process.json')).write_text(json.dumps(result,indent=2));print(result,flush=True)
 sys.exit(code)
