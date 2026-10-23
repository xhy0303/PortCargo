# 本机验收记录

## v0.4.1：音乐更新与详细回归（2026-10-16）

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
| 十分钟连续运行 | 602.1 秒、106 轮；352 个 Transform、7 个音源稳定；运行错误 0，预热窗口内存差 5096 字节 | `technical.txt`、`technical-process.json` |
| 普通包退出与重启 | 首页退出/取消、原生窗口关闭/取消，两独立进程均正常退出，真实进度、撤销、设置保持 | `release-native-result.json` |
| 构建与交付隔离 | 普通/开发包 Succeeded、0 errors；普通包剔除开发驱动、Pipeline/Roslyn | 构建清单、`release-code-inspection.txt` |

长测 fixture 关闭音乐；音乐自然轮播由独立检查覆盖。自动检查不代替个人听感判断，也不证明任意硬件或任意运行时长没有问题。当前仅 Apple Silicon Mac 实际验证，微信真机、个人主体发布/广告资格待后续阶段。退出时的历史 ComputeBuffer 回收提示仍存在。

## v0.4.0：像素夜港轻扁平接入（2026-10-16）

首页、局内、四类货物、船与离港影像、八格选关、设置、教学、结果和声音已替换。规则、存档合同及关卡数据不改；详见 [Art-Integration-v0.4.md](Art-Integration-v0.4.md)。

| 项目 | 结果 | 证据（`Evidence/v0.4/`） |
| --- | --- | --- |
| 普通包原生退出与恢复 | 首页退出/取消、原生窗口关闭/取消、两个独立进程均正常退出码 0；现有闯关、无尽、撤销和设置指纹保持 | `release-native-result.json` |
| 构建与剔除 | 普通和开发包 Succeeded，0 errors、1 条预期 Pipeline 提示；普通包无 DevelopmentChecks、Pipeline/Roslyn | 两份构建清单、`release-code-inspection.txt` |
| 素材与场景 | 62 张运行 Sprite 加独立应用图标、6 WAV；3 Canvas、36 BoxView、7 AudioSource；无丢失 Sprite，文字底板顺序检查通过 | `scene-art-audit.json` |
| 原资料保护 | 原美术包 194 文件不变；38 个规则/存储/关卡/调参/字体/依赖文件不变；接入前快照可恢复 | `source-integrity.json` |
| 主流程 | 实际点击首页、八格选关和分页、首次教学、船队、暂停/设置/隐私、闯关和无尽通关/下一局、重开/切关取消；最终 PASS，退出码 0 | `art-flow.write.txt`、`flow-process.json` |
| 独立进程恢复 | 第二进程精确恢复两模式 run/version/棋盘，实际撤销有效，设置与教学状态保留；退出码 0 | `art-flow.read.txt`、`read-process.json` |
| 异常恢复 | 实际点击卡住/撤销；故障注入验证保存失败阻止退出、留在游戏、重试；启动错误视图重试；退出码 0 | `art-edges.edges.txt` |
| 密集关卡 | L10/L50 × 320×568、360×800、720×1280 共六组；所有顶箱实际点击及完整撤销、无遮挡重叠、模态阻挡；最窄热区 47.6×47.6 像素 | `art-dense.txt` |
| 最后一关与分页 | L50 实际通关，保留选关/重玩/无尽入口；末页 49/50 显示正常 | 同上及 `art-dense.L50-completed.png`、`art-dense.last-page.png` |
| 文字与截图 | 当前可见字形、文本溢出、按钮 ≥44 像素及界内检查通过；目视检查首页、教学、设置、选关、结算和密集局面 | `art-flow.*.png`、`art-dense.*.png` |

规则构建前置仍执行 50 关求解和生产规则回放、59 组聚合检查及 32 组无尽生成；日志在本版证据目录。开发流程均使用隔离存档。首次标题高度不足和导入/构建重叠工具记录保留在 diagnostics，已经修正，不能当作最终构建结果。

本轮为简单回归；没有重新执行十分钟长测、逐关人工试玩、微信或 Intel Mac 硬件测试。保留此前 Unity 退出时的 ComputeBuffer 回收提示；没有把音频数值/绑定检查表述为设备试听。

## v0.3.0：完整本机流程与设计稿共同结构（2026-10-16）

保留 50 关与无尽，补齐首页、首次说明、分页选关、暂停/设置/帮助、结算、确认和退出保存。用户暂不选定五种画风；简化首页采用中部港口展示、底部闯关主操作与无尽次入口，局内保留共同分区，弹窗按内容收紧。

| 项目 | 实际结果 | 证据（均位于 `Evidence/v0.3/`） |
| --- | --- | --- |
| 规则与内容 | .NET 与 Unity JSON 59 组聚合检查通过；50 关求解/生产规则回放，32 组无尽生成回归；关卡目录与 v0.2 哈希一致 | `standalone-core-checks.txt`、`prebuild-rules.txt`、`validated-solutions.json`、`content-summary.json` |
| 完整主流程 | 独立开发程序实际指针操作：首页、锁定/翻页选关、首次三页说明、船队、暂停/帮助/设置/隐私；L01 完成→L02 操作→取消切关；无尽重开取消/确认→完成→下一程；退出确认取消；退出码 0 | `player-flow-aligned.write.txt` |
| 新进程续玩 | 第二进程从首页继续：闯关/无尽 run、version、精确棋盘分别一致；实际撤销可用，音效/音乐/减少动效与教学记录保留；退出码 0 | `player-flow-aligned.read.txt` |
| 卡住与异常 | 独立内存 L06 fixture：实际指针进入卡住并撤销恢复；模拟写失败时退出提供重试/留在游戏，首页重试成功；启动错误视图重试路径可用；退出码 0 | `player-flow-aligned.edges.txt` |
| 可见布局 | 主流程 320×568、恢复首页 360×800、异常弹窗 720×1280：实际截图、可见中文无缺字、顶层按钮 ≥44 屏幕像素且在界内；人工检查首页/选关/暂停/保存失败截图 | 上述报告和 `player-flow-aligned.*.png` |
| 构建与剔除 | 普通/开发包均 Succeeded，0 errors、1 条预期 Pipeline 禁用提示；版本 0.3.0，Unity 6000.3.25f1；普通包开发检查/Pipeline/Roslyn 均剔除 | 两份 `20261016-full-flow-*-build.json`、`release-code-inspection.txt` |
| 密集关卡和小屏 | L10/L50 × 三种尺寸共 6 组；逐顶箱点击/撤销、遮罩拦截、中文和按钮通过 | `technical-20261016/technical-01.txt` |
| 十分钟连续运行 | 604.1 秒、106 轮；199 Transform、7 AudioSource 稳定，运行错误 0，退出码 0 | `technical-20261016/technical-summary.json` |
| 普通包原生退出/重启 | 首页退出及原生窗口关闭，均验证取消后返回和确认退出；两独立进程退出码 0，原存档完整保留 | `technical-20261016/release-native-result.json` |
| 普通包启动与旧进度 | 按用户授权启动普通包；本机存档重新写入成功，闯关和无尽完整 run 指纹、已完成关 ID、设置均保持，解锁数没有减少；启动日志无 PortCargo 异常 | `release-save-preservation.json`、`release-startup.log` |

自动流程与故障注入使用独立 `/tmp` 或内存存储，未清空真实进度。普通包启动正常读取并保存用户现有进度；本轮收尾已正常退出，可再次双击续玩。

早期 edges 驱动假定 L04 能进入僵局，未找到可达路径，属于测试夹具假设错误。改为搜索实际目录中已确认可达僵局的关卡，最终使用 L06；未因此改动关卡或规则。早期报告移至 `diagnostics/pre-alignment/`，不计为最终失败。

本轮是流程抽样与简单验证，未逐一人工试玩 50 关；技术收尾已补齐十分钟长测、密集关卡小屏检查和普通包原生退出/重启，详见 [Technical-Checks-v0.3.md](Technical-Checks-v0.3.md)。未做微信导出/真机或 Intel Mac 硬件验证。启动错误项验证的是视图重试路线，存储写失败使用故障注入；不能等同于真实设备故障。开发程序退出时仍有 Unity 内部 ComputeBuffer 回收提示，边界见 KnownIssues.md。

## v0.2.0：50 关与无尽（2026-10-16）

本轮按用户要求只做简单测试，UI 另开对话设计。继续使用 Unity 6000.3.25f1，普通包版本 0.2.0。

| 项目 | 实际结果 | 证据 |
| --- | --- | --- |
| 固定 50 关 | 全部生产 Solver 求解并回放通关；原 L01–L10 数据完全保留 | `Evidence/v0.2/validated-solutions.json`、`content-summary.json` |
| 新 40 关 | 最少暂存一格 13 关、两格 14 关、三格 13 关；均默认三格可解 | `Evidence/v0.2/new-level-metrics.csv` |
| 无尽生成/存档 | 32 组确定性种子/局数，生产规则见证回放与中途恢复通过；0 次回退 | `Evidence/v0.2/prebuild-rules.txt` |
| 规则回归 | .NET 与实际 Unity JSON 编解码均通过 59 组聚合检查 | `standalone-core-checks.txt`、`prebuild-rules.txt`（均在 v0.2） |
| 实际输入与升级 | 独立开发包读取旧格式 L10 完成档，升级解锁 L11；真实点击通关 L11、L25、L50、无尽前两程 | `Evidence/v0.2/player-expansion.write.txt` |
| 分页/切换 | 第七页仅 49/50；无尽第三程一步后从选关返回 L50 保留原局，再进无尽也保留；可见文案无缺字 | 同上 |
| 跨进程续玩 | 退出码 0；第二独立进程恢复第三程精确局面、run/version/撤销和设置，实际点击撤销并恢复已完成 L50；退出码 0 | `Evidence/v0.2/player-expansion.read.txt` |
| 构建 | validation 与 preview 均 Succeeded、0 errors、1 条预期 Pipeline 禁用提示；普通包开发驱动与运行 Pipeline/Roslyn 已剔除 | v0.2 的两个 `*-build.json` 与 `release-code-inspection.txt` |

测试使用 `/tmp` 独立目录，未删除、迁移或重置用户真实存档。为抽样快速进入后段关卡，在已证明旧档解锁 L11 后，仅对测试档设为全解锁，日志有明确 FIXTURE 标记。

Unity JsonUtility 会将缺失的内联类字段构造为空对象，首次构建回归因此揭示旧存档兼容错误；已修复为省略尚不存在的无尽字段、解码时恢复缺失字段语义，最终 Unity 回归和独立旧档启动均通过。首次失败保留于 `Evidence/v0.2/diagnostics/legacy-json-first-build-failure.txt`，不计为最终失败。

未做本轮长时间压测、逐一人工试玩 50 关、UI 视觉重设计或微信真机验证。自动可解与玩家难度评价分开。普通包构建和代码剔除检查通过，实际指针抽样在对应开发包执行。

## v0.1 历史验收（原 10 关）

日期：2026-10-15。引擎：6000.3.25f1。平台：当前 Apple Silicon Mac。规则内容为原始首批 10 关，默认三格，不用广告。

## 已有证据

| 项目 | 结果与证据 | 边界 |
| --- | --- | --- |
| 工具连接 | `tool-connection.json`：版本、模块、测试场景创建/保存/重开/删除 | 与微信兼容性分开 |
| R01–R06、R09–R12 | `rules-regression.txt`、`standalone-core-checks.txt`、`prebuild-rules.txt`：17 组回归通过；10 关完整解回放 | 1 个 NUnit 入口；生产 Core 独立于 Unity 编译 |
| R07/R08 | 撤销保留提示/扩位权益；旧 run、旧 stateVersion 被拒绝 | 尚未接广告协调器，不代表真实广告回调验收 |
| U01 | `pointer-sweep.txt`、`pointer-edges-final-320.txt`、`player-sweep.txt`：10 关经 Input System 指针完成；遮挡、拖动、双指归属、撤销、当前局提示通过 | 不直接调用规则方法替代 UI 点击 |
| U02 | `layout-320x568.txt`、`layout-720x1280.txt`、`layout-360x800.txt`：按钮在界内且无缺字；320 窄屏六堆顶箱 47.6×48.0 px | 微信胶囊与异形屏未测 |
| U03 | 原子规则提交与反馈池已实现；独立包最后一步规则耗时约 0.03–0.10 ms | 这是规则调用耗时，不是端到端触控延迟；低端手机 100 ms 待测 |
| U04/U05 | 有音、静音/减少动效通关；实际点击下一关 499 ms（含测试注入手势帧） | 当前 Mac/Editor 结果，不是手机成绩 |
| U06 | 50 次设置菜单重开，场景 Transform 数稳定为 164；10 分钟独立包长测见下方 | 原生内存/音源与设备验收分开 |
| U07 | `player-save-write.txt`、`player-save-read.txt`：两个独立进程，写入→退出→重读→界面撤销 | 测试目录隔离，未移动用户应用存档 |
| U08 | `live-save-recovery.txt`：单槽坏回退、双槽坏重置、内容 revision 变更只重开活跃局；纯规则覆盖写失败/旧待存替换 | 使用真实 Presenter 和内存故障存储；不是手机闪存故障实验 |
| U09 | 本地内容和核心存档不依赖服务器；本机独立冷启动完成 | 微信冷/热启动、弱网待下一阶段 |
| U10 | 普通预览包与开发验证包分别构建；`release-code-inspection.txt` 确认开发检查、运行 Pipeline、Roslyn 剔除 | 开发包实际全通；普通包仅独立启动检查，未逐关重跑 |

## 构建对应关系

`20261015-mvp-validation` 为开发测试程序，`20261015-mvp-preview` 为可交付普通程序。各自构建日志清单记录哈希和开关。两者均 0 编译错误；一条 Pipeline 服务关闭提示属于预期警告。

`player-save-write/read` 和 `player-sweep` 在最终生产玩法代码上执行；之后开发工具增加物理指针隔离以避免自动测试受系统鼠标干扰，普通包不含此代码。长测证据绑定更新后的 validation 构建。

## 长测

本次长测未完成完整 600 秒验收：最后采样为 572.1 秒、136 轮，随后进程退出，没有最终 RESULT 标记。已观测区间内 Transform 始终 163、AudioSource 始终 7；Unity 已分配内存约 58.64 MB，没有持续上涨趋势。只能作为阶段观察，不能标记十分钟测试通过。记录见 `Evidence/player-soak.txt` 与 `Evidence/soak-summary.json`。早期未完成序列保存在 `Evidence/diagnostics/player-soak.txt`，不计为通过。最终驱动会逐步确认版本恰好递增一次，并在测试程序内部暂时隔离其他物理指针；退出时恢复输入设备状态。

## 阶段结论

T01/T02/T04/T05 的本机部分已实现并验证。T03 的动作反馈已实现，手机触控延迟待测。T06 教学已实现，新玩家理解和真实通关时长等待试玩；T00/T07 微信部分、T08 广告、T09 扩关按用户决定暂不推进。

不能把“10 关自动可解”“macOS 构建成功”“Editor 点击通过”写成微信真机验收或商业验证。

### 2026-10-16 选关末页尺寸修复

选关固定预留四列两行，不足八关的页面保留空白位置；面板高度和底部操作区不再随关卡数量变化。
实际验证包翻页检查通过：320×568 下末页与满页边界均为 `(22.40,145.39,275.20,277.22)`，底部按钮纵向位置一致；L10/L50 三种分辨率点击回归通过。证据位于 `Evidence/v0.4/level-panel-fixed/`。
本次更新普通试玩包；此前完整流程、独立进程续玩和原生退出记录属于修复前版本，本次未重跑这些检查。`final-art-bindings.json` 保留为此前美术接入版本的历史绑定；本次构建绑定见 `level-panel-fixed/build-bindings.json`。
