from pathlib import Path
import json,re
p=Path('/Users/xhy/UnityEditorProj/Projects/PortCargo');e=p/'Docs/Evidence/v0.4.1'
for report in ['music.txt','flow.write.txt','flow.read.txt','edges.edges.txt','dense.txt','technical.txt']:
 assert (e/report).read_text().rstrip().endswith('RESULT PASS'),report
for run in ['music','flow','read','edges','dense','technical']:
 assert json.loads((e/(run+'-process.json')).read_text())['exitCode']==0,run
assert json.loads((e/'release-native-result.json').read_text())['result']=='PASS'
text=(e/'technical.txt').read_text();soak=re.search(r'SOAK PASS elapsed=([\d.]+)s loops=(\d+) transforms=(\d+) audioSources=(\d+) warmedUnityDelta=(-?\d+) errors=(\d+)',text)
assert soak
seconds,loops,transforms,audio,delta,errors=soak.groups()
summary={'result':'PASS','elapsedSeconds':float(seconds),'loops':int(loops),'transforms':int(transforms),'audioSources':int(audio),'warmedUnityDeltaBytes':int(delta),'runtimeErrors':int(errors),'musicNaturalTransitions':6,'musicOutputCycles':2,'musicOutputElapsedSeconds':282.5762,'limitations':['Current Apple Silicon Mac only','Lifecycle focus tests use simulated callbacks','No subjective device listening verdict','WeChat and phone verification deferred','Known Unity exit ComputeBuffer cleanup warning persists']}
(e/'validation-summary.json').write_text(json.dumps(summary,indent=2))
section=f'''## v0.4.1：音乐更新与详细回归（2026-10-16）

三首音乐轮播、响度提高、音乐与音效独立开关、菜单连续播放及后台暂停续播已接入；详见 [Music-v0.4.1.md](Music-v0.4.1.md)。本轮证据均在 `Evidence/v0.4.1/`，没有将历史版本结果充当新版验收。

| 项目 | 本轮结果 | 证据 |
| --- | --- | --- |
| 音乐母版与响度 | 三首约 48 / 50.53 / 43.64 秒，响度差 0.14 LU，母版无削波，原素材不变 | `audio-masters.json`、`audio-loudness.json`、`source-integrity.json` |
| 实际音乐输出 | 两个完整自然轮播周期、六次换曲，约 282.58 秒观察；三首输出均非静音、峰值 <1、未观察到播放停滞，7 个音源稳定 | `music.txt`，进程耗时 291.52 秒、退出码 0 |
| 音乐操作与生命周期 | 首次点击启动、独立开关、设置/首页不重置、暂停续播；模拟 hidden/shown/focus 回调及交错顺序通过 | `music.txt`；不宣称手机后台真机已通过 |
| 规则与内容 | 50 关求解/生产规则回放、59 组聚合检查、32 个无尽种子/轮次通过 | `prebuild-rules.txt`、`validated-solutions.json` |
| 全流程与续玩 | 教学、首页、设置、闯关/无尽通关/下一局、取消重开/换关；第二进程恢复精确局面、撤销及开关设置 | `flow.write.txt`、`flow.read.txt` |
| 异常恢复 | 实际卡住/撤销，模拟保存失败阻止退出/重试，启动错误页重试通过 | `edges.edges.txt` |
| 布局与输入 | L10/L50 × 三种尺寸，共六组顶箱点击/撤销/遮挡检查；末页与满页面板尺寸及底部位置一致 | `dense.txt` |
| 十分钟连续运行 | {seconds} 秒、{loops} 轮；{transforms} 个 Transform、{audio} 个音源稳定；运行错误 {errors}，预热窗口内存差 {delta} 字节 | `technical.txt`、`technical-process.json` |
| 普通包退出与重启 | 首页退出/取消、原生窗口关闭/取消，两独立进程均正常退出，真实进度、撤销、设置保持 | `release-native-result.json` |
| 构建与交付隔离 | 普通/开发包 Succeeded、0 errors；普通包剔除开发驱动、Pipeline/Roslyn | 构建清单、`release-code-inspection.txt` |

长测 fixture 关闭音乐；音乐自然轮播由独立检查覆盖。自动检查不代替个人听感判断，也不证明任意硬件或任意运行时长没有问题。当前仅 Apple Silicon Mac 实际验证，微信真机、个人主体发布/广告资格待后续阶段。退出时的历史 ComputeBuffer 回收提示仍存在。

'''
f=p/'Docs/Acceptance.md';s=f.read_text();f.write_text(s.replace('# 本机验收记录\n\n','# 本机验收记录\n\n'+section,1))
f=p/'Docs/Music-v0.4.1.md';s=f.read_text().replace('最终结果待本轮完整验证结束后补充。',f'本轮检查全部通过：音乐两轮自然播放、六次换曲；完整流程/独立进程恢复/异常路径/密集布局；{seconds} 秒、{loops} 轮长测，运行错误 {errors}、音源和对象数量不变；普通包两次真实退出与重启保留存档。两份构建均 0 errors，保留 1 条预期 Pipeline 关闭提示。原始结果见 Evidence/v0.4.1/validation-summary.json 与各检查报告。\n\n试玩包：Builds/Delivery/PortCargo-macOS-v0.4.1.zip。当前未接微信或广告，发布阶段仍需 AppID 和真机验证。');f.write_text(s)
f=p/'Docs/KnownIssues.md';s=f.read_text().replace('本轮交付已接入像素夜港轻扁平美术的 macOS 本机 v0.4.0','本轮交付为像素夜港轻扁平美术、三首音乐轮播的 macOS 本机 v0.4.1');s=s.replace('音频文件和事件绑定已检查；实际设备听感、连续三次循环试听仍待人工确认。','v0.4.1 已完成三首音乐两轮自然轮播、实际输出及开关/续播自动检查，详见 Music-v0.4.1.md；个人听感与手机扬声器效果仍需试听，不能把数值检查当成人工试听。');s=s.replace('## 验证限制\n','## 验证限制\n\n- 当前 v0.4.1 完成 '+seconds+' 秒 / '+loops+' 轮长测，运行错误 0、对象和音源稳定；见 Music-v0.4.1.md。下列 v0.3/v0.4 记录为历史范围。\n',1);f.write_text(s)
print(json.dumps(summary,indent=2))
