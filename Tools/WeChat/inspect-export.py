from pathlib import Path
import json,hashlib,sys
root=Path(__file__).resolve().parents[2]
out=root/(sys.argv[1] if len(sys.argv)>1 else 'Builds/WeChat/20261017-integration')
game=out/'minigame'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
config=json.loads((game/'project.config.json').read_text())
g=json.loads((game/'game.json').read_text())
files=[f for f in game.rglob('*') if f.is_file() and '.plugincache' not in f.parts]
# Fingerprint shipped code/assets; ignore IDE private config, cached plugins and debug symbols.
artifactFiles=sorted(f for f in game.rglob('*') if f.is_file() and '.plugincache' not in f.parts and f.name!='project.private.config.json' and 'symbols' not in f.name)
artifactHash=hashlib.sha256('\n'.join(str(f.relative_to(game))+':'+digest(f) for f in artifactFiles).encode()).hexdigest()
previousPath=out/'BuildManifest.json'
previous=json.loads(previousPath.read_text()) if previousPath.exists() else {}
summary={'artifactFingerprint':artifactHash,'appId':config['appid'],'compileType':config['compileType'],'orientation':g['deviceOrientation'],'plugins':g.get('plugins'),'totalBytes':sum(f.stat().st_size for f in files),'subpackages':{},'sourceSha256':{},'runtimeTests':'pending','realDeviceTests':'not run','adsEnabled':False}
for sp in g.get('subpackages',[]):
 directory=game/sp['root'];summary['subpackages'][sp['name']]=sum(f.stat().st_size for f in directory.rglob('*') if f.is_file())
for sub in ['Assets/PortCargo/Scripts','Assets/PortCargo/Plugins','Packages']:
 for f in (root/sub).rglob('*'):
  if f.is_file():summary['sourceSha256'][str(f.relative_to(root))]=digest(f)
assert config['appid']=='wx19e83e7294013864'
assert config['compileType']=='game' and g['deviceOrientation']=='portrait'
assert summary['subpackages']['data-package']>0
assert summary['subpackages']['wasmcode']>0
assert 'loadDataPackageFromSubpackage: true' in (game/'game.js').read_text()
summary['sourceHashMeaning']='source files at inspection time; not proof these files were compiled'
if previous.get('artifactFingerprint')==artifactHash:
 for key in ['runtimeTests','realDeviceTests','acceptanceEvidence']:
  if key in previous: summary[key]=previous[key]
(out/'BuildManifest.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2))
print(json.dumps({k:v for k,v in summary.items() if k!='sourceSha256'},ensure_ascii=False,indent=2))
