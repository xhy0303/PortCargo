from pathlib import Path
import json,hashlib,sys,re
root=Path(__file__).resolve().parents[2]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def read_u32(data, offset, end):
    """Read a bounded unsigned LEB128 value without trusting section lengths."""
    value=0
    for index in range(5):
        if offset>=end:
            raise ValueError('Truncated WASM unsigned LEB128 value')
        byte=data[offset];offset+=1
        if index==4 and byte>0x0f:
            raise ValueError('WASM unsigned LEB128 exceeds 32 bits')
        value|=(byte&0x7f)<<(index*7)
        if not byte&0x80:
            return value,offset
    raise ValueError('Invalid WASM unsigned LEB128 value')

def wasm_exports(data):
    """Inspect the binary export section; this is not a full WASM runtime validator."""
    if data[:8]!=b'\x00asm\x01\x00\x00\x00':
        raise ValueError('Expected an uncompressed WASM version 1 module')
    exports=[];offset=8;seen=False
    kinds={0:'function',1:'table',2:'memory',3:'global',4:'tag'}
    while offset<len(data):
        section=data[offset];offset+=1
        size,offset=read_u32(data,offset,len(data))
        end=offset+size
        if end>len(data):
            raise ValueError('Truncated WASM section')
        if section==7:
            if seen:
                raise ValueError('Duplicate WASM export section')
            seen=True
            count,offset=read_u32(data,offset,end)
            names=set()
            for _ in range(count):
                length,offset=read_u32(data,offset,end)
                if length>end-offset:
                    raise ValueError('Truncated WASM export name')
                name=data[offset:offset+length].decode('utf-8');offset+=length
                if name in names:
                    raise ValueError('Duplicate WASM export name: '+name)
                names.add(name)
                if offset>=end or data[offset] not in kinds:
                    raise ValueError('Missing or invalid WASM export kind')
                kind=kinds[data[offset]];offset+=1
                index,offset=read_u32(data,offset,end)
                exports.append({'name':name,'kind':kind,'index':index})
            if offset!=end:
                raise ValueError('Trailing bytes in WASM export section')
        offset=end
    return exports

def framework_entry_point(source):
    """Require the generated framework's Module._main binding to a known WASM entry."""
    module_main=r'''Module\s*(?:\[\s*["']_main["']\s*\]|\._main)'''
    assign=r'''(?:\b_main\s*=\s*)?'''+module_main+r'''\s*=\s*'''
    direct=assign+r'''(?:wasmExports|asm)\s*\[\s*["'](main|__main_argc_argv)["']\s*\]'''
    wrapper=assign+r'''createExportWrapper\(\s*["'](main|__main_argc_argv)["']\s*[,)]'''
    names=set(re.findall(direct,source)+re.findall(wrapper,source))
    if len(names)!=1:
        raise ValueError('Framework must bind Module._main to one main or __main_argc_argv WASM export')
    return names.pop()

def inspect_wasm_build(build_directory):
    """Reject SDK success outputs without a function entry and its framework binding."""
    paths=sorted(build_directory.glob('*.wasm'))
    if not paths:
        raise ValueError('No uncompressed Unity WASM output found in '+str(build_directory))
    checked=[]
    for path in paths:
        data=path.read_bytes()
        try:
            exports=wasm_exports(data)
        except ValueError as error:
            raise ValueError(str(path)+': '+str(error)) from error
        entries={item['name']:item for item in exports if item['name'] in {'main','__main_argc_argv'} and item['kind']=='function'}
        if not entries:
            raise ValueError(str(path)+': missing function export "main" or "__main_argc_argv"; SDK SUCCEED is not runtime acceptance')
        framework=path.with_name(path.stem+'.framework.js')
        if not framework.is_file():
            raise ValueError(str(path)+': matching framework is missing: '+framework.name)
        framework_bytes=framework.read_bytes()
        try:
            entry=framework_entry_point(framework_bytes.decode('utf-8'))
        except ValueError as error:
            raise ValueError(str(framework)+': '+str(error)) from error
        if entry not in entries:
            raise ValueError(str(framework)+': Module._main binds to '+entry+' but that function is absent from '+path.name)
        checked.append({'file':path.name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),
                        'exportCount':len(exports),'functionExportCount':sum(item['kind']=='function' for item in exports),
                        'entryPointExported':True,'entryPointName':entry,'entryPointFunctionIndex':entries[entry]['index'],
                        'framework':framework.name,'frameworkSha256':hashlib.sha256(framework_bytes).hexdigest(),
                        'frameworkMainBindingVerified':True})
    return checked

def main():
    out=root/(sys.argv[1] if len(sys.argv)>1 else 'Builds/WeChat/20261017-integration')
    game=out/'minigame'
    # Check raw Unity output before writing a successful manifest. Compressed package
    # size, BuildPlayer Success and SDK SUCCEED do not prove the entry survived.
    wasmChecks=inspect_wasm_build(out/'webgl'/'Build')
    config=json.loads((game/'project.config.json').read_text())
    g=json.loads((game/'game.json').read_text())
    files=[f for f in game.rglob('*') if f.is_file() and '.plugincache' not in f.parts]
    # Fingerprint shipped code/assets; ignore IDE private config, cached plugins and debug symbols.
    artifactFiles=sorted(f for f in game.rglob('*') if f.is_file() and '.plugincache' not in f.parts and f.name!='project.private.config.json' and 'symbols' not in f.name)
    artifactHash=hashlib.sha256('\n'.join(str(f.relative_to(game))+':'+digest(f) for f in artifactFiles).encode()).hexdigest()
    previousPath=out/'BuildManifest.json'
    previous=json.loads(previousPath.read_text()) if previousPath.exists() else {}
    adConfigPath=out/'RewardedAdsConfig.json'
    adConfig=json.loads(adConfigPath.read_text()) if adConfigPath.exists() else None
    summary={'artifactFingerprint':artifactHash,'appId':config['appid'],'compileType':config['compileType'],'orientation':g['deviceOrientation'],'plugins':g.get('plugins'),'totalBytes':sum(f.stat().st_size for f in files),'subpackages':{},'sourceSha256':{},'runtimeTests':'pending','realDeviceTests':'not run','adsEnabled':adConfig['adsEnabled'] if adConfig else None,'adConfiguration':adConfig,'wasmExportChecks':wasmChecks}
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
    summary['wasmExportCheckMeaning']='raw WASM exports a main or __main_argc_argv function and the matching framework binds Module._main to it; not proof of minigame startup or device acceptance'
    if previous.get('artifactFingerprint')==artifactHash:
        for key in ['runtimeTests','realDeviceTests','acceptanceEvidence']:
            if key in previous: summary[key]=previous[key]
    (out/'BuildManifest.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2))
    print(json.dumps({k:v for k,v in summary.items() if k!='sourceSha256'},ensure_ascii=False,indent=2))

if __name__=='__main__':
    main()
