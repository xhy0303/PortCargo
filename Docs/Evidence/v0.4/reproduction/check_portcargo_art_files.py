from pathlib import Path
import json,hashlib
p=Path(__file__).resolve().parents[4];e=p/'Docs/Evidence/v0.4';a=p/'ProjectContent/美术资产/2026-10-16_像素夜港轻扁平'
before=json.loads((e/'before-source-hashes.json').read_text());assets=json.loads((e/'art-source-hashes.json').read_text())
protected=[rel for rel in before if rel.startswith(('Assets/PortCargo/Scripts/Core/','Assets/PortCargo/Scripts/Platform/','Assets/PortCargo/Data/','Assets/PortCargo/Art/Fonts/','Packages/')) or rel=='Assets/PortCargo/Scripts/Runtime/GamePresenter.cs']
assert all(hashlib.sha256((p/rel).read_bytes()).hexdigest()==before[rel] for rel in protected)
assert all(hashlib.sha256((a/rel).read_bytes()).hexdigest()==h for rel,h in assets.items())
result={'result':'PASS','protectedGameplayStorageDataFontAndPackageFiles':len(protected),'originalArtFilesUnchanged':len(assets),'backup':'Backups/pre-flat-pixel-20261016.zip','sourceHashes':{str(f.relative_to(p)):hashlib.sha256(f.read_bytes()).hexdigest() for f in (p/'Assets/PortCargo/Scripts').rglob('*.cs')}}
(e/'source-integrity.json').write_text(json.dumps(result,indent=2));print('PASS protected',len(protected),'source art',len(assets))
