from pathlib import Path
import zipfile,hashlib,os,stat,json
project=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo')
build=project/'Builds/macOS/20261016-music-preview';app=build/'PortCargo.app'
out=project/'Builds/Delivery/PortCargo-macOS-v0.4.1.zip';out.parent.mkdir(parents=True,exist_ok=True)
root='PortCargo-v0.4.1/'
guide='''港口装箱 v0.4.1 · 音乐更新版

双击 PortCargo.app，从首页开始或继续。固定50关和无尽模式保留。
原有进度直接续接，不必删除存档；闯关与无尽分别保存局面和撤销。
首页：闯关主入口、无尽次入口、选关、设置、玩法说明和退出。
首次开始有三页规则；游戏内“暂停”可继续、重开、选关、设置或回首页。
通关后点下一关，无尽完成后点下一程；全部50关完成后可重玩或继续无尽。
选关每页8关；未通过前一关时锁定。未完成局重开/换关会确认。
退出先保存；失败可重试或留在游戏，明确选择仍退出才放弃未落盘动作。

规则：只取顶部，同类装船，其余暂存。默认3格。
每局一次成功免费提示，可撤销；音效、音乐和减少动效在设置中调整。
没有倒计时、货币、广告、账号或联网统计。

已接入像素夜港轻扁平美术：港口、船、四类纹样货箱、按钮、弹窗、八格选关、教学图与声音。
标题、关卡、进度、装载量均保持动态，原有闯关和无尽分别续玩。
Docs/Art-Integration-v0.4.md：本轮接入与回退说明。
Docs/Game-Flow-v0.3.md：沿用的完整流程；Docs/Acceptance.md：当前验收记录。
原设计稿、母版与制作源保留在微信小游戏工作区，未覆盖。

简单测试已通过：50关规则回放、59组规则/存档回归、实际界面流程点击、
两个独立进程的闯关/无尽续玩、卡住撤销和模拟保存失败恢复。
普通包完成首页退出/取消、关闭窗口/取消及两个独立进程正常退出和恢复，现有进度未被重置。
美术回归：L10/L50三种尺寸共6组点击/撤销通过，最后一关结算与最后页检查通过。
本轮验证结果见 Docs/Music-v0.4.1.md 与 Docs/Acceptance.md。
背景音乐三首轮播，统一响度，默认音量提高；暂停/设置/回首页不重置曲目。
音乐实际连续轮播和音频输出有独立检查；个人音量偏好仍以试听为准。原始证据保留在Unity工程Docs/Evidence目录。
Unity 6000.3.25f1；Apple Silicon/Intel通用包，仅当前Apple Silicon Mac实际运行。
微信小游戏统一后接，此包不是微信发布版本。
'''
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in sorted(app.rglob('*')):
        name=root+f.relative_to(build).as_posix()
        if f.is_symlink():
            info=zipfile.ZipInfo(name);info.create_system=3;info.external_attr=(stat.S_IFLNK|0o777)<<16;z.writestr(info,os.readlink(f))
        else:z.write(f,name)
    z.write(build/'BuildManifest.json',root+'BuildManifest.json')
    for f in sorted((project/'Tools/Music').glob('*.py')):z.write(f,root+'MusicSource/'+f.name)
    z.writestr(root+'MusicSource/说明.txt','这里是音乐生成源码的备份。请放回 PortCargo Unity 工程的 Tools/Music 目录后再运行 generate_playlist.py；不要直接在试玩包目录运行。工程须包含原始 Audio/FlatPixel/BGM/night_harbor_loop.wav，并安装 numpy。详见 Docs/Music-v0.4.1.md。')
    z.writestr(root+'说明书.txt',guide)
    for f in sorted((project/'Docs').glob('*.md')):z.write(f,root+'Docs/'+f.name)
    z.write(project/'Assets/PortCargo/Art/Fonts/OFL.txt',root+'字体许可-OFL.txt')
    for name in ['来源总表.md','字体与音频来源.md']:
        z.write(project/'Docs/Evidence/v0.4'/name,root+'来源与许可/'+name)
with zipfile.ZipFile(out) as z:
    assert z.testzip() is None
    assert z.read(root+'说明书.txt').decode('utf-8')==guide
    main=root+'PortCargo.app/Contents/MacOS/PortCargo'
    assert (z.getinfo(main).external_attr>>16)&stat.S_IXUSR
    assert hashlib.sha256(z.read(main)).digest()==hashlib.sha256((app/'Contents/MacOS/PortCargo').read_bytes()).digest()
    assert z.read(root+'Docs/Art-Integration-v0.4.md').decode('utf-8')==(project/'Docs/Art-Integration-v0.4.md').read_text()
    report={'archive':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'entries':len(z.infolist()),'crcPass':True,'utf8GuideAndHandoffPass':True,'executableModePass':True,'executableBytesMatch':True}
(project/'Docs/Evidence/v0.4.1/delivery-integrity.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
