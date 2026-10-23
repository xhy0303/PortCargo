from pathlib import Path
import subprocess,time,json,sys
p=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo');e=p/'Docs/Evidence/v0.3/technical-20261016';prefix=e/'technical-01'
args=[str(p/'Builds/macOS/20261016-full-flow-validation/PortCargo.app/Contents/MacOS/PortCargo'),'--portcargo-save-dir','/tmp/portcargo-technical-20261016-01','--portcargo-check-technical',str(prefix),'-screen-width','320','-screen-height','568','-logFile',str(e/'technical-01.log')]
started=time.monotonic();samples=[];nextsample=0
with (e/'technical-01-stdout.log').open('w') as out:
 proc=subprocess.Popen(args,stdout=out,stderr=subprocess.STDOUT,cwd=p)
 print('Started isolated technical validation PID',proc.pid,flush=True)
 while proc.poll() is None:
  elapsed=time.monotonic()-started
  if elapsed>=nextsample:
   status=subprocess.run(['ps','-o','rss=','-p',str(proc.pid)],capture_output=True,text=True)
   samples.append({'elapsedSeconds':round(elapsed,2),'rssKiB':int(status.stdout.strip() or '0')})
   (e/'process-observations.json').write_text(json.dumps({'pid':proc.pid,'samples':samples,'complete':False},indent=2))
   print('Runtime sample',samples[-1],flush=True);nextsample=elapsed+60
  if elapsed>1200:
   proc.terminate();print('Watchdog exceeded 1200 seconds; failed, not accepted',flush=True);break
  time.sleep(1)
 code=proc.wait(timeout=30)
 result={'pid':proc.pid,'durationSeconds':round(time.monotonic()-started,2),'samples':samples,'complete':True,'exitCode':code}
 (e/'process-observations.json').write_text(json.dumps(result,indent=2))
 print('Finished exitCode=',code,'durationSeconds=',result['durationSeconds'],flush=True)
 sys.exit(code)
