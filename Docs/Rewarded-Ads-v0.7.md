# 激励广告 v0.7 接入记录

当前后续优化见 [v0.7.1 广告预加载](Rewarded-Ads-v0.7.1.md)：针对用户反馈的广告跳转等待，提前准备两个广告位缓存，奖励和免费流程约束保持。0.7.1 已完成构建、模拟器启动观察及后台开发版本上传；未提交审核或正式发布，真机广告等待时间仍待用户比较。本文保留 0.7.0 的实现、验收和上传历史证据，不将旧产物结果直接用于新版本。

日期：2026-10-24。本文记录本轮用户授权、配置与实现约束，并区分纯规则测试和微信实际播放验收。

## 本轮授权与范围

用户已确认微信小游戏流量主开通，并授权实际接入更多可选广告机会。首批接入两个激励视频场景，全部由玩家主动选择，不在进入游戏、取箱、暂停、通关或切换页面时自动播放广告。

1. **重复额外提示**：每局原有的一次免费提示保持不变。免费提示用完后，每次完整观看一次激励广告可获得一次额外提示；同一局可以重复使用，不设每局或每日次数上限。
2. **无尽跳程**：无尽模式的暂停菜单和卡住界面提供“看广告跳过本程”。完整观看后放弃当前程的局面，进入下一程。下一程仍有自己的一次免费提示。

撤销、重开、普通提示额度、继续游戏、正常完成后的下一关/下一程和首页返回路径保持可用。玩家可以始终不看广告继续玩。此轮不加入插屏广告、原生模板广告、扩容广告或广告货币系统。

旧文档中的“首发无广告”“每局广告提示一次”“每日最多 6 次”“前 5 关禁广告”和固定奖励间隔不再生效。SDK 加载失败处理、一次只允许一个广告请求在途等技术保护不等于玩法配额，也不能造成免费流程阻塞。

用户已明确授权：**本轮测试通过后，将开发版本 `0.7.0` 上传微信后台**。本地验证完成后已执行该授权，用户在微信开发者工具中确认，工具于北京时间 **2026-10-24 12:19:04.952** 返回上传成功。**开发版本已上传，尚未提交审核或正式发布；手机验收待用户进行。**

## 广告位与序列化配置

配置资产：`Assets/PortCargo/Data/GameTuning.asset`。字段定义：`Assets/PortCargo/Scripts/Runtime/GameTuning.cs`。广告位通过序列化字段配置，不写进 Core 规则层。

| 场景 | 序列化字段 | 本轮广告位 ID |
| --- | --- | --- |
| 额外提示 | `rewardedHintAdUnitId` | `adunit-d04702ad95d16c3c` |
| 无尽跳程 | `endlessSkipAdUnitId` | `adunit-eac16355a06f9b4f` |
| 广告功能开关 | `adsEnabled` | 微信接入包配置为 `1`；禁用时保留免费流程 |

对应只读属性为 `RewardedHintAdUnitId`、`EndlessSkipAdUnitId` 和 `AdsEnabled`。`CompositionRoot` 仅在微信运行环境中按开关绑定真实广告 provider；桌面和未配置的环境不伪造“观看完成”。广告位的可用状态、填充和真实内容由微信决定，配置 ID 并不保证每次请求都有广告。

文档中的配置表是本轮接入约定。最终打包前必须检查实际资产字段、构建所使用资产和导出包；不能仅凭本文或 Inspector 截图认定发布包已正确接入。

## 提示奖励规则

入口沿用现有提示按钮。未使用免费提示时直接按免费流程求解和显示提示，不要求观看广告。免费提示用完后，必须先为当前棋盘求得有效下一步，再显示自愿观看确认，避免玩家看完广告却没有可提供的提示。

每次完整观看只显示一次提示；每个新广告请求可以产生一个新奖励票据。`GameSession.UseHint` 仅对免费提示检查 `freeHintUsed`，不再以 `adHintUsed` 限制广告提示次数。旧存档的 `adHintUsed` 字段保留为“曾获得过广告提示”的历史标记，因此旧档中该值为 `true` 也可以继续获得广告提示。

`hintCount` 继续统计实际发出的提示，达到 `int.MaxValue` 后保持该值，避免无限使用下溢出成负数而被存档校验拒绝。广告提示不改变棋盘和状态版本，不消耗仍未使用的免费提示；撤销不回滚已经发出的提示统计。

未完整观看、取消、加载失败、无可用广告、调用异常或奖励票据失效时不发奖励，也不扣除免费提示。无解或求解预算不足时沿用原有反馈，不为这次无效提示启动广告。

## 无尽跳程规则

跳程入口只在无尽模式当前程未完成、广告可用、没有其他广告请求在途且当前程号小于 `int.MaxValue` 时开放。入口位于暂停菜单和卡住界面；已有撤销、重开、继续游戏和返回首页入口保留。

确认文案必须说明“跳过本程，进入下一程”。程号也参与无尽难度生成，因此**下一程不保证更简单**。不得把跳程称为通关，不得给当前程补发成功结算，也不修改闯关的完成列表、解锁数量或独立保存的闯关局面。

完整观看后，运行层验证 `RewardedSkipTicket`，再生成下一程的新布局、新 runId 和初始棋盘，更新最近布局历史并保存无尽进度。失败或取消仍停留在当前程；不要提前递增程号或重置局面。达到最大程号时不提供跳程，避免溢出或广告完成后无法兑现奖励。

## 回调、生命周期与存档

Core 使用 `IRewardedVideoProvider` 抽象广告结果。结果分为 `Completed`、`Cancelled`、`Failed`、`Unavailable`；仅 `Completed` 可以进入奖励发放逻辑。真实微信适配器必须根据平台的实际完成结果报告，不根据关闭广告、超时或返回游戏猜测完成。

| 保护项 | 要求 |
| --- | --- |
| 单次处理 | 每个 ticket 第一次 resolve 后即消耗；重复回调和失败之后迟到的完成回调不得发奖 |
| 当前局绑定 | 绑定 GameSession 实例、runId 和 stateVersion；移动、撤销、重开或切换局面后的旧奖励失效 |
| 跳程绑定 | 另校验无尽模式、当前程号、当前程未完成，以及程号未达到最大值 |
| 并发与取消 | 全局只允许一个广告请求在途，并绑定独立请求 token；加载阶段返回游戏会同步取消底层请求，迟到的加载回调不能再次弹广告；原生展示阶段须通过广告自身关闭控件退出 |
| 暂停恢复 | 确认弹窗会暂停棋盘；发放提示前需恢复可接收规则命令的状态。广告引起的正常前后台切换不能直接冒充“未完成”或“已完成” |
| 持久化 | 成功提示后保存计数；成功跳程后保存新程号、布局和 run；沿用双槽保存、失败提示及重试路径 |

`RewardedSkipTicket.TryResolve` 只验证奖励资格，不修改棋盘、计数或暂停状态；实际跳程由运行层执行。单请求保护仍然必要，因为提示不会递增棋盘版本，两个不同 ticket 不能靠版本号自动互斥。

取消按阶段区分：`Loading` 时玩家可使用游戏内“返回游戏”，该操作结束适配器请求并销毁等待中的广告实例；`Opening`/`Watching` 已跨入微信原生展示，此时没有可调用的原生 hide/cancel 接口，游戏内返回与退出确认不能覆盖正在播放的广告，须使用广告自身的关闭按钮。若组件或局面生命周期使请求失效，运行层拒绝其奖励，但适配器仍保留原生广告占用，直到真实关闭、错误或超时后才允许下一次请求；不将“游戏请求已取消”误写成“原生广告已关闭”。

保持现有存档 schema，不清空、重置或迁移用户进度。`ProgressValidator` 沿用原有规则；旧档的免费提示消耗状态、广告提示历史标记和闯关/无尽各自的进度继续有效。

## 断网和广告无响应时的行为

小游戏已加载并进入游戏后，免费流程照常：玩家可以取箱、使用原有免费提示、撤销、重开和继续游戏，广告失败不会成为继续玩的前置条件。这不代表微信首次启动或下载小游戏包可以离线完成。

| 阶段 | 当前超时 | 玩家操作与失败结果 |
| --- | --- | --- |
| `Loading` 加载广告 | 20 秒 | 可以点击游戏内“返回游戏”取消；加载出错或超时后结束请求，不发奖励，迟到加载回调不会再弹广告 |
| `Opening` 调用原生展示 | 20 秒 | 原生展示已请求，使用广告自身的关闭控件；展示失败或超时不发奖励 |
| `Watching` 等待观看结果 | 180 秒 | 使用原生广告关闭控件；未完整观看、错误或超时均不发奖励，不改变原局面或扣免费提示 |

超时由运行时更新检查；真机暂停、原生页面和网络切换的具体回调顺序仍需验收。断网本身不会被当作完成广告，仅在平台明确回报完整观看且当前奖励票据有效时才兑现提示或跳程。失败请求结束后，网络恢复且广告可用时可以再次主动尝试，不设置每日次数或失败后的永久锁定。

本轮模拟测试已覆盖加载/展示错误、三个阶段超时、加载取消、原生展示阶段返回限制、失败后的再次请求与免费玩法恢复。**真实手机断网、恢复网络、原生广告页面切换及缓存广告行为尚未验收**，不能把模拟 API 结果写成真机断网测试通过。

## 验证结果与当前状态

Unity **6000.5.10f1** 编译、核心规则、适配器契约、Unity 实际 UI 输入和字体烘焙已有通过结果。补齐异步 WASM 生命周期后，最终微信产物通过入口检查，模拟器明确完成资源依赖并进入首页，等待 35 秒后采集的控制台无 error，超过此前约 30 秒的失败窗口。开发版本 **0.7.0 已上传成功**。Unity UI 使用假广告 provider，适配器契约使用模拟微信 API；模拟器启动和后台上传也不代替真实广告或手机完整流程验收。

### 核心规则

执行命令（项目根目录）：

```sh
dotnet run --no-restore --project Tools/CoreChecks/CoreChecks.csproj -- Assets/PortCargo/Data/Levels/catalog.json
```

结果：退出码 `0`，`PASS 213 groups; compiled shared production C# without Unity references`。持久证据已归档为 [CoreChecks 日志](Evidence/v0.7.0/tests/core-checks.txt)。`git diff --check` 在核心修改范围内通过。

本轮新增回归覆盖：

- 同局多个独立完成广告都可得到提示，免费提示仍仅一次，广告提示不改变棋盘。
- 旧档 `adHintUsed=true` 可继续获得提示，原免费额度消耗保持；提示统计饱和后仍可保存和重载。
- 提示回调重复、失败后迟到完成、棋盘变化、同对象重开和会话替换时不错误发奖。
- 跳程票据只接受完整观看一次，验证模式、实例、runId、状态版本和程号，拒绝撤销、重开、最大程与已完成程。
- 跳程票据本身不修改游戏状态；原有 200 关、无尽生成、双模式存档和损坏恢复回归继续通过。

### Unity UI 与奖励事务

`DevelopmentAdChecks` 在 Unity GameView **360×800 手机竖屏尺寸**运行，经过实际 InputSystem 指针输入与 UI 命中，共 **88 次点击**，A01–A13 加 A07b 共 **14 项通过**。这是编辑器内的竖屏 UI 测试，不是真实手机点击或真实广告观看。共记录提示广告请求 12 次、跳程请求 6 次；这些包括取消和失败测试，不能当作实际广告展示数。

| 检查 | 已验证结果 |
| --- | --- |
| A01 | 首次真实 HUD 提示点击使用原免费额度，不请求广告 |
| A02 | 拒绝额外提示确认，棋盘和奖励计数不变 |
| A03 | 等待广告时重复点击被遮挡 HUD，不产生双请求或双奖励 |
| A04 | 同一局连续获得 5 次广告提示，每次重复回调均被忽略 |
| A05 | 取消、失败、不可用结果不发奖励，并恢复可玩 UI |
| A06 | 确认前后广告不可用及同步异常均保留免费游玩 |
| A07 | 模拟平台 hide/show 不丢失待完成事务，完成回调只恢复一次 |
| A07b | 假 provider 的原生展示阶段拒绝游戏返回取消和系统退出覆盖层，后续完成仍兑现奖励 |
| A08 | 真实按钮关闭并重新打开请求后，旧完成回调不发奖、不关闭新请求 |
| A09 | 拒绝、取消或失败的跳程保留当前程号与原 run |
| A10 | 两次成功跳程各前进一程，重复回调无效，闯关存档和解锁完全相同 |
| A11 | 两广告 provider 均不可用时，返回闯关、取箱、免费撤销、免费重开和首次免费提示可用 |
| A12 | 将序列化双槽 JSON 复制到新仓库，执行 Load、重新 Initialize Presenter，再点击继续无尽，精确恢复第 3 程棋盘、run、布局历史及闯关进度 |
| A13 | 无尽 seed=1701 经过 16 次实际箱子点击进入卡住状态；跳程拒绝/取消保留棋盘，完成前进一程，免费撤销/重开仍可用 |

A12 全程在**同一进程**中完成，验证的是序列化、全新仓库读取和运行层重初始化，不是退出 Unity/微信后的干净进程持久化验收。测试存档全部使用内存仓库，原仓库未被写入；测试结束已恢复原游戏状态，报告中的 `originalStateRestored=true`。

证据：[结构化检查报告](Evidence/v0.7.0/runtime/ad-checks.json)、[文字报告](Evidence/v0.7.0/runtime/ad-checks.txt)，以及同目录的提示确认、等待广告、跳程确认、重载和卡住界面截图。早期缺字检查单独保存在 `runtime/initial-missing-glyph/`，不能替代补字后的最终报告。

### 适配器契约与非微信分支

`Tools/RewardedAdsChecks` 检查真实生产适配器代码与模拟宿主的交互：微信编译分支 **77 项通过**，非微信分支 **3 项通过**，Editor 分支 **3 项通过**。微信分支的持久日志为 [adapter-wechat.txt](Evidence/v0.7.0/tests/adapter-wechat.txt)。这些结果验证分支编译、请求生命周期与回调契约，不代表已连接微信真实广告服务器或验证真实填充。

### JS 存档桥

本轮 `node Tools/WeChat/check-storage.cjs` 的 **7 项检查通过**：缺失键识别、Unicode 完整快照往返、两槽独立、配额失败保留旧槽、读回不一致上报、读取异常不当作空档，以及非字符串损坏上报。检查直接执行生产 `PortCargoStorage.jslib`，`wx` 宿主由测试模拟；它验证 JS 桥接契约，不代表手机退出重启后的存档已经验收。检查入口见 [check-storage.cjs](../Tools/WeChat/check-storage.cjs)。

### 字体与画面

源 TTF 字集已由 330 扩至 **358 字符**，补齐广告文案用字，保留 PortCargoUI 名称与固定字重 500。原有字符轮廓和字宽逐字比较保持一致，当前 Runtime CJK 全覆盖，源字体、新旧哈希及备份见 [字体重建清单](Evidence/v0.7.0/fonts/rebuild-manifest.json)。

Unity 已完成全字形烘焙。原 **1024×1024** 图集不能容纳扩充后的完整字集，现静态 TMP SDF 图集调整为 **2048×2048 单图**；不是动态补字或运行时下载字体。最终 360×800 UI 回归在补字及图集烘焙后执行通过。重建清单里的 `unity_atlas_status` 是 TTF 子集生成时的阶段记录，后续烘焙状态以本节和当前字体资产为准。

### Unity 6.5 与固定微信 SDK 的局部兼容

本轮保持 Unity **6000.5.10f1** 和已固定的微信 SDK 包版本，兼容修改集中于 [WXConvertCore.cs](../Packages/com.qq.weixin.minigame/Editor/WXConvertCore.cs) 及微信项目模板配置。下列组合已完成最终转换、实际 WASM 检查、模拟器启动观察和开发版本上传；验收范围与证据见后文。

| 兼容项 | 最终实现与原因 |
| --- | --- |
| 编译函数导出 | `UNITY_6000_5_OR_NEWER` 下显式保留 `_main`、`_malloc`、`_free`，避免 SDK 的显式导出列表替换新 Emscripten 默认值后剥除入口或运行所需分配函数 |
| 栈 cookie | 同条件追加 `-s STACK_OVERFLOW_CHECK=1`，生成并暴露 SDK 要求导出的 `writeStackCookie`、`checkStackCookie` |
| 已有 JS 符号 | 同条件追加 `-s EXPORT_ALL=1`，将已存在的 JS 符号导出到 `Module`，供 SDK 使用堆视图及 `dynCall_*` 回调；未使用手工枚举的 HEAP 导出列表，该选项也不代替编译函数的显式保留 |
| 浏览器音频悬空别名 | SDK 原规则移除 `jsAudio*` 浏览器辅助函数后，同条件清理 `Module["jsAudio..."]=jsAudio...` 别名；本次产物共清理 7 个，避免导出指向已移除函数 |
| 新版函数声明形式 | 同条件将 `replacePrefixes` 匹配的 `var Name=function(` 归一化为 `function Name(`，再执行 SDK 原有转换规则，解决 Unity 6.5 下唯一漏匹配的 `_JS_Sound_Init`；保留 `GetMetaData` 与键盘接口原有参数包装 |
| 微信异常构造器差异 | 同条件在转换 framework 时，将 `e instanceof WebAssembly.RuntimeError` 改为 `typeof WebAssembly.RuntimeError === 'function' && e instanceof WebAssembly.RuntimeError`，避免 `WXWebAssembly` 缺少该构造器时，诊断代码再抛 TypeError 并遮盖原始异常 |
| 异步 WASM 生命周期 | 同条件适配 Unity 6.5 的 `async createWasm`：增加 `wasm-instantiate` 与 `wasm-preloadAssets` 两个依赖，保留插件计时，实例化后填入 `Module.asm` 并回调 `Module.wasmInstantiated`、完成对应依赖移除，恢复与插件资源就绪通知的协作；若预期函数结构未匹配则让转换显式失败 |
| macOS Brotli 定位 | `CompressBrotliFile` 在 `UNITY_EDITOR_OSX && UNITY_6000_5_OR_NEWER && !PLATFORM_WEIXINMINIGAME` 条件下，通过 `BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.WebGL, BuildOptions.None)` 定位实际模块内的 `BuildTools/Brotli/macos/brotli` |
| 微信工具 Babel 转译 | 将 SDK `Runtime/wechat-default/project.config.json` 模板及导出的项目配置中 `babelSetting.ignore` 改为 `[]`，让 framework 中可选链等语法进入转译；修改后的项目已成功上传开发版本，未将该结果表述为预览二维码已生成 |

栈 cookie 的条件已核对 Unity 6000.5.10f1 本地 Emscripten 的 `src/modules.mjs` 和 `src/runtime_stack_check.js`：关闭 `STACK_OVERFLOW_CHECK` 时，相应方法不会进入可导出 runtime 元素或生成结果。官方设置说明区分 `0` 为不检查、`1` 为启用栈 cookie 检查，见 [Emscripten STACK_OVERFLOW_CHECK](https://emscripten.org/docs/tools_reference/settings_reference.html#stack-overflow-check)。

当前安装的模块目录实际为 `/Users/xhy/UnityEditorProj/Editors/6000.5.10f1/PlaybackEngines/WebGLSupport`，位于 `Unity.app` 同级；固定 SDK 原有定位则假定在 app 内。两处 Brotli 源码调用改用包装方法，保留普通压缩质量 **11** 与 assembly 压缩质量 **8**，校验可执行文件、退出码和输出文件；其他环境继续原 SDK 路径。没有改动 SDK DLL、Unity 安装文件，也没有创建目录兼容链接。以后更换 SDK 时需重新核对本地兼容补丁是否仍需要。

`RuntimeError` 保护只负责避免诊断路径遮盖原异常；本轮生命周期适配已有 `no dependencies, invoke run`、引擎启动和超过原超时窗口的观察证据。实际游戏输入和真实广告仍按后文边界单独验收。

定位证据继续保留：[stack-cookie 链接错误](Evidence/v0.7.0/wechat/initial-stack-cookie-failure/emscripten-error.txt)、[首次导出失败](Evidence/v0.7.0/wechat/initial-stack-cookie-failure/export-failure.txt)、[首次导出结果](Evidence/v0.7.0/wechat/initial-stack-cookie-failure/export-result.txt)、[Brotli 定位失败](Evidence/v0.7.0/wechat/initial-brotli-path-failure/export-failure.txt)，以及 [入口缺失诊断](Evidence/v0.7.0/wechat/initial-missing-entry-point/diagnosis.txt)、[当时导出检查](Evidence/v0.7.0/wechat/initial-missing-entry-point/export-inspection.json) 与 [SDK 返回结果](Evidence/v0.7.0/wechat/initial-missing-entry-point/export-result.txt)。

早期音频适配失败的日志也保留：[jsAudio 别名错误](Evidence/v0.7.0/wechat/initial-js-audio-export-failure/simulator-console.json)、[Sound Init 转换错误](Evidence/v0.7.0/wechat/initial-sound-init-conversion-failure/simulator-console.json)。新增问题证据为 [异步资源依赖超时](Evidence/v0.7.0/wechat/initial-run-dependency-failure/simulator-console.json)、[当时短暂显示的首页](Evidence/v0.7.0/wechat/initial-run-dependency-failure/simulator-home.jpg) 和 [预览语法打包失败](Evidence/v0.7.0/wechat/initial-preview-parser-failure/preview-result.json)。失败证据保留，不覆盖为后续成功结果。

### 实际 WASM 入口与 framework 检查

新版 Emscripten 的 WASM 入口可能导出为 `__main_argc_argv`，framework 再将其绑定到 `Module["_main"]`；入口验证不能仅查找字面名 `main`，也不能根据 SDK 的 `SUCCEED` 或包体大小判断可运行。

[inspect-export.py](../Tools/WeChat/inspect-export.py) 已增加无额外依赖的 WASM export section 解析：检查实际 `webgl/Build/*.wasm`，要求导出 `main` 或 `__main_argc_argv` **函数**，并核实对应 `.framework.js` 的 `Module._main` 确实绑定到该导出。缺失入口、同名非函数、缺失 framework 或映射指向未导出的函数时立即失败，不生成新的通过清单；通过后在 `wasmExportChecks` 中记录实际入口名、WASM/JS 文件哈希及绑定结果。

解析与绑定检查先用独立临时样本完成 **26 项检查**，其中 3 个样本与 Node 原生 `WebAssembly.Module.exports` 对照一致，覆盖两种入口名、错误映射、缺失入口、多文件中夹带无入口模块及损坏数据。最终产物已通过完整 inspector，见 [导出检查报告](Evidence/v0.7.0/wechat/export-inspection.json)。中间产物曾在入口检查通过后仍发生异步资源依赖超时，因此入口及绑定检查不能替代生命周期、稳定运行或真机广告验收。

### 微信启动、预览与上传的当前状态

最终导入目录为 `Builds/WeChat/20261024-v0.7.0-ads/minigame`，AppID 为 `wx19e83e7294013864`，纵屏及两个广告位按本文配置保持。[SDK 导出结果](Evidence/v0.7.0/wechat/export-result.txt) 为 `SUCCEED`，并有独立产物检查与模拟器证据。

| 最终产物检查项 | 结果 |
| --- | --- |
| 导出完成时间 | `2026-10-24T04:17:04.6099270Z`，北京时间 12:17:04.609927 |
| 产物指纹 | `be1f2671304024a14633efa694c8300560821b73aa70441e50aa51b33ac4b911` |
| 原始 WASM | 25,226,429 bytes；465 个导出，其中 463 个函数；入口 `__main_argc_argv`，函数索引 64054 |
| 原始 WASM SHA-256 | `f300f05497a5475c57449b78903eb4fb6eb1e9145b912c865c4b194edcddf903` |
| 原始 framework SHA-256 | `8287c2132b84711a06fbb857a0c77b4f1f68c14a67884d0c1563f84fcb1fa195`；`Module._main` 绑定已核实 |
| 转换后 framework SHA-256 | `2485587bd17b92362ecb686895b84701412f94f1f924140012a9004180f04aa2`，文件为 `minigame/webgl.wasm.framework.unityweb.js` |

最终模拟器于北京时间 **12:17:19** 启动，**12:17:20.594** 记录 `no dependencies, invoke run`，**12:17:20.820** 记录游戏启动耗时 **1618 ms**。等待 35 秒后采集的 [控制台](Evidence/v0.7.0/wechat/simulator-console.json) 无 error，[首页截图](Evidence/v0.7.0/wechat/simulator-home.jpg) 已视觉检查。该次观察超过中间产物原约 30 秒的错误窗口，未复现 `runDependencies ids=[]`；它仍不是长时间运行或完整玩法验收。

控制台保留摄像头能力、IndexedDB 不可用和帧率接口等 warning，不表述为“零警告”。游戏存档使用自有 `wx` 存储桥，IndexedDB warning 本身不直接证明游戏存档失败，实际持久化仍需手机退出重启验证。

模拟器画布自动点击仍有 `canvas not found` 的工具限制，不宣称小游戏内自动取箱、广告按钮或完整流程输入已通过。前文 88 次点击来自 Unity GameView 的 InputSystem/UI 检查，不能换算成微信小游戏内的点击证据。

手机预览此前失败于 framework 的可选链语法（`Unexpected token .`），原因包含该 framework 被 `babelSetting.ignore` 排除转译；模板和导出配置已修改为 `ignore: []`。随后执行用户授权的开发版上传，工具最初等待微信开发者工具内确认，用户完成确认后查询原任务，得到 `status=success`、`detail=execution_success` 和内部 `result.success=true`，见 [上传成功结果](Evidence/v0.7.0/wechat/upload-confirmed-result.json)。该任务于北京时间 **2026-10-24 12:19:04.952** 更新为成功；[首次等待确认记录](Evidence/v0.7.0/wechat/upload-result.json) 仅保留为历史，不再是当前阻点。

后台开发版本为 **0.7.0**，上传工具报告总包体 **18,828,291 bytes**：主包 **1,229,897**，`data-package` **11,650,022**，`wasmcode` **5,948,372**。此处使用上传工具实际包体，和包含诊断文件的本地目录总大小分开记录。**开发版本上传成功；未提交审核、未正式发布。手机验收待用户进行，尚无本轮真机广告通过或成功预览二维码的记录。**

完整交付状态汇总在 [delivery-summary.json](Evidence/v0.7.0/wechat/delivery-summary.json)，包含当前验收证据和上传结果。`export-inspection.json` 中的 `runtimeTests=pending` 保留为产物检查时的阶段快照，不回填覆盖历史；后续模拟器与上传结果以交付汇总及对应原始证据为准。验证结束后已恢复用户原先为空的 `preloadedAssets`，清理自动生成的空 `Resources` 目录；没有改变已上传产物。

## 本轮仍需完成的验证

1. **微信实际输入与更长流程**：最终模拟器启动及 35 秒观察已通过；画布自动点击工具仍报 `canvas not found`，小游戏内取箱、按钮操作和更长流程需要实际设备继续验收。
2. **微信真机广告**：尚未实测两个真实广告位的加载、填充、播放、提前关闭与完成发奖；需要连续提示、暂停/卡住跳程、失败重试和广告位对应关系验证。
3. **真机前后台与声音**：尚未验收真实广告期间切后台/返回、广告页面跳转及音频恢复。现有 hide/show 与原生阶段测试使用模拟事件，不能替代手机验证。
4. **干净进程持久化与断网**：尚未完成本轮广告奖励后退出微信、重新启动并恢复进度的检查；已有同进程新仓库重载结果不能替代此项。还需真机断网/恢复网络、无填充和 SDK 错误时的免费流程验证，参见上文断网处理规则。
5. **后台后续流程**：开发版本 `0.7.0` 已上传成功，用户可从后台开发版本继续手机验收；本轮未完成提交审核或正式发布，也没有成功预览二维码的证据。

历史无广告版本的证据见 [微信接入记录](WeChat-Integration.md) 和 [v0.6 开发者工具验收](WeChat-Devtools-v0.6.md)，不以旧版本的通过记录替代本轮广告验收。
