# 本机构建与复现

## 当前入口与历史包归档（2026-10-26）

当前工程为 Unity 6000.5.10f1 / URP 17.5.0 / v0.7.5，实际关卡目录为 200 关。最新微信导出位于 `Builds/WeChat/20261025-v0.7.5-home-layout`，构建和开发版上传记录见 [v0.7.5 交付](Home-Layout-v0.7.5.md)。本页下面的 50 关、59 组回归、6000.3.25f1 和 v0.4.1 包说明均属于对应历史阶段。

旧构建已按[清理清单](Cleanup-20261026.md)归档；下文旧路径不再保证实体存在。普通桌面历史交付保留 `Builds/Delivery/PortCargo-macOS-v0.4.1.zip`，最近 URP 验证包保留 `Builds/macOS/20261023-urp-validation`。当前 `BuildTools.cs` 的桌面输出目录仍使用 `20261018-v0.6-*` 固定名称，重新构建时会创建目录，不应仅凭目录名判断实际工程版本。

## 锁定环境

当前 Unity **6000.5.10f1**, changeset `3bd4f66ad299`，用户于2026-10-23切换。微信 SDK 已做内嵌兼容补丁并恢复正常打开，详见 [Unity 6.5 兼容修复](Unity-6.5-Compatibility.md)。下文历史发布包使用6000.3.25f1；2026-10-23 修复时未重新导出微信包，后续 v0.7.5 已完成微信导出与开发版上传，真实手机验证边界见最新交付说明。

当前验证使用 Unity CLI `1.0.0-beta.14`；Pipeline `0.8.0-exp.1`；Input System `1.20.0`。完整依赖见 `Packages/manifest.json` 与 `packages-lock.json`。微信 SDK 保持原锁定提交加本地兼容补丁，不追随 main。

2026-10-23 工程已迁移到 **URP 17.5.0 / Universal Renderer（Forward）**，全部画质档位统一使用 `Assets/PortCargo/Settings/Rendering/PortCargoURP.asset`。迁移测试、独立构建及平台验收边界见 [URP-Migration.md](URP-Migration.md)。

## 制作与启动

1. 打开工程，等待编译完成，打开 `Assets/PortCargo/Scenes/Bootstrap.unity`。
2. `PortCargo > Validate Content`：严格校验 JSON 字段和数量、求解 50 关、回放解并运行规则回归，失败中止构建。
3. `PortCargo > Build macOS Preview`：生成普通本机试玩包；`Build macOS Validation Player` 生成含自动检查工具的开发包。
4. 成品位于 `Builds/macOS/20261016-music-preview/PortCargo.app`，也可使用 `Builds/Delivery/PortCargo-macOS-v0.4.1.zip`（程序、中文说明、构建清单和美术/UI 交接文件）。双击打开。测试包单独位于 `20261016-music-validation`，不要把它当发布包分发。
5. 每次构建在对应目录及 `Docs/Evidence/v0.4.1` 写 BuildManifest，保存版本、内容哈希、开发开关、结果与诊断。正式预览不编译 `DevelopmentChecks` 及其流程检查部分，不启用 Pipeline 运行连接。

构建菜单通过 Editor update 消费 `Temp/portcargo-build-request.txt`，保证控制请求先返回，实际构建在 Editor 主线程执行。文件内容为 `preview` 或 `validation`。播放、编译或导入时暂不执行；退出 Play Mode 后执行。不要同时提交两个构建请求。

历史构建清单的 gitCommit 为 null。2026-10-23 已建立 GitHub 备份；仓库提交 Assets、Packages、ProjectSettings、Tools、Docs，忽略 Library、Temp、Builds 及测试工具 bin/obj，保留工具 csproj。

## 存档

普通本机运行使用 `Application.persistentDataPath/Saves/portcargo.save.a` 和 `.b`。它们保存 SHA-256 校验的完整快照，恢复时选最新合法槽。包含解锁、已完成 ID、首次说明完成标记、音乐/音效/减少动效，以及闯关和无尽各自的当前局、权益和撤销历史。旧版载荷缺少 mode/endless 字段时继续按闯关恢复。

开发验证包独有 `--portcargo-save-dir <绝对路径>`，用于隔离测试与真实试玩进度。下列参数只在开发构建中生效，完成检查后进程退出：

- `--portcargo-check-music <报告前缀>`：独立存档，音乐开关/暂停续播与两个完整自然轮播周期的实际音频输出检查。详见 Music-v0.4.1.md。
- `--portcargo-check-art <报告前缀>`：仅使用显式独立存档；执行 L10/L50 六组密集尺寸、顶箱射线/实际点击/撤销/遮罩、最后一关结算与最后一页截图检查，不运行十分钟长测。
- `--portcargo-check-technical <报告前缀>`：仅使用显式独立存档，临时全解锁；实际点击 L10/L50 在 320×568、360×800、720×1280 的全部顶箱并撤销，随后至少 600 秒交替闯关/无尽、设置切换、暂停/回首页及通关，记录对象/音源/内存样本和最终结果。
- `--portcargo-check-flow <报告前缀>`：使用空的临时存档目录，实际点击首页、首玩说明、选关、暂停/设置/帮助/船队、L01 通关、L02 续玩、无尽重开/通关/下一程、退出确认取消，并保存两模式快照与关键截图。
- `--portcargo-check-flow-read <相同报告前缀>`：另一个进程从首页继续两模式，比较精确局面/版本/撤销历史，再实际撤销。
- `--portcargo-check-flow-edges <报告前缀>`：在独立内存 fixture 中通过实际点击验证生产目录中可达僵局关卡的卡住/撤销、模拟写失败时退出被阻止及重试恢复、启动错误页重试路线。

测试目录必须与真实存档分开。旧 sweep/edges/persistence/expansion 参数仍作为历史工具保留，但其导航假设是 v0.1/v0.2，应先适配首页和暂停菜单后再用，不能把旧驱动直接作为 v0.3 验收。v0.3 技术收尾使用新的 technical 驱动完成十分钟长测，旧 soak 参数不用于当前验收。

退出状态和报告共同判断结果。所有驱动向 Input System 队列发送鼠标/触摸状态，经正常游戏帧到达 InputSystemUIInputModule；不会直接调用 TryMove 冒充点击。求解器仅负责选出下一次应该点击的可见箱。

## 原生窗口检查复现

技术结果见 [Technical-Checks-v0.3.md](Technical-Checks-v0.3.md)。`Evidence/v0.3/technical-20261016/reproduction/` 保存本次实际使用的 Swift 窗口工具和 Python 包装脚本，使用前将脚本中的证据目录换为新目录，避免覆盖历史证据。Swift 工具用 `swiftc` 编译，macOS 必须已授予启动工具的辅助功能权限；只控制 bundle id 为 `com.portcargo.prototype` 且处于前台的目标进程。

普通包原生脚本会正常读取并保存现有用户进度，执行退出/取消，不做关卡动作。隔离长测使用另一个脚本及 `/tmp` 存档。两个脚本不可混用。

开发包另有 `--portcargo-check-native-map <前缀>`：输出退出弹窗实际按钮中心并验证取消，完成后自动退出；`--portcargo-observe-native <前缀>`：只记录真实鼠标事件位置、流程状态及游戏内截图，不注入输入，需正常从游戏退出。诊断时也显式指定隔离存档。

## 不依赖 Unity 的规则回归

本机 .NET SDK 7 可执行：

```sh
/usr/local/share/dotnet/dotnet run --project Tools/CoreChecks/CoreChecks.csproj -- Assets/PortCargo/Data/Levels/catalog.json
```

工程引用同一份生产 Core C# 源码和回归代码。`NuGet.Config` 清空外部包源，此工具不依赖额外 NuGet 包。Unity Test Runner 的 EditMode 测试为一个入口，内部聚合规则、50 关解回放及无尽/存档回归，本轮实际为 59 组检查，不能误记成 59 个 NUnit 测试。

## 微信入口

2026-10-17 已按用户授权开始接入，使用 Unity 6000.3.25f1 与已锁定 SDK，不升级引擎。微信专用菜单为 `PortCargo > Configure WeChat Preview` 与 `Build WeChat Preview`；先配置、等待编译，再构建。输出 `Builds/WeChat/20261017-integration/minigame`。

首次 WebGL 编译和转换成功，运行暂被 AppID 的 Unity 快适配插件授权阻断。详情、版本和验收边界见 [WeChat-Integration.md](WeChat-Integration.md)。Mac 历史交付包保持不变。
