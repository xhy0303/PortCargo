# v0.4.1 音乐更新与验证

## 已实现

- 原曲保留，新增《灯火流向》（lantern_current）与《晨潮》（morning_tide）两首原创合成音乐。原曲演奏两遍后换曲；三首依次循环，单轮约 142.16 秒。
- 三首 PCM 母版按 -24 dBFS RMS 统一，FFmpeg 实测综合响度分别为 -22.50、-22.58、-22.44 LUFS。音乐 AudioSource 默认增益从 0.22 改为 0.65；音效保持 0.5。
- 设置、暂停、回首页不会重新从曲首播放。首次点击后开始音乐；保留音乐与音效独立开关及原有存档字段。
- 音乐开关采用 120 ms 淡变并保留播放位置；切后台、失去窗口焦点立即暂停。回前台后必须同时满足用户开启音乐和窗口获得焦点才恢复；曲尾短淡出，下一曲短淡入。单音乐源顺序播放，不叠加多份背景音乐。
- 当前是固定默认音量和独立开关，没有新增音量滑杆。音乐播放位置不跨应用退出保存；新进程从第一首开始，用户音乐开关保持。
- 三首均 44.1 kHz 双声道，Unity 使用 Streaming / Vorbis 0.8 / Load In Background，音效导入不变。没有新增包或混音器，沿用 Unity 6000.3.25f1。

## 来源与替换

`Tools/Music/generate_playlist.py` 保存新增编曲与响度处理源码；`original_synthesis.py` 是原素材包音色算法的副本。新曲没有下载第三方音乐或使用采样库。旧音乐源与微信小游戏工作区原始美术包保持不动。

母版：`Assets/PortCargo/Audio/MusicV041/`；检查：`Docs/Evidence/v0.4.1/audio-masters.json` 和 `audio-loudness.json`；导入回读：`audio-imports.txt`。原曲 24 秒源文件保留在 `Audio/FlatPixel/BGM`。

重制：在工程根目录执行 `python3 Tools/Music/generate_playlist.py`（需要 numpy），只覆盖 MusicV041 的生成文件和音频检测 JSON，不覆盖原始资产。然后在 Unity 编辑模式执行 `PortCargo > Apply Music v0.4.1`，重新绑定并保存 Bootstrap 场景。新播放器不会自动修改旧存档。

## 验证入口与范围

- 开发包 `--portcargo-check-music <报告前缀>`：必须配合独立 `--portcargo-save-dir`。实际点击菜单/音乐/音效开关，检查暂停续播、模拟生命周期回调顺序；不快进地播放两个完整轮播周期，检查六次换曲、每首实际输出幅度和音源数。
- `--portcargo-check-flow` / `--portcargo-check-flow-read`：两个独立进程验证完整流程、闯关和无尽恢复、撤销及音乐设置持久化。
- `--portcargo-check-flow-edges`：卡住撤销、保存失败退出/重试与启动错误页。
- `--portcargo-check-art`：六组密集分辨率、最后一关结算，以及选关末页尺寸保持。
- `--portcargo-check-technical`：六组密集分辨率与至少十分钟实际界面循环，检测对象、音源和内存趋势。此工具的长测 fixture 关闭音乐，音乐连续输出由独立 music 驱动覆盖。

所有检查以运行报告和进程退出码为准。源码音频检测、压缩后真实输出与主观试听是不同证据；自动检查不能保证所有设备听感，也不能宣称游戏绝对没有问题。微信端音频恢复、手机扬声器响度和首包仍在平台接入时验证。

本轮检查全部通过：音乐两轮自然播放、六次换曲；完整流程/独立进程恢复/异常路径/密集布局；602.1 秒、106 轮长测，运行错误 0、音源和对象数量不变；普通包两次真实退出与重启保留存档。两份构建均 0 errors，保留 1 条预期 Pipeline 关闭提示。原始结果见 Evidence/v0.4.1/validation-summary.json 与各检查报告。

试玩包：Builds/Delivery/PortCargo-macOS-v0.4.1.zip。当前未接微信或广告，发布阶段仍需 AppID 和真机验证。
