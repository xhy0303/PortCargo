# 港口装箱 · 本机 v0.3.0

2026-10-16。执行依据为原始《港口装箱_MVP完整开发参考规格_v1.1.docx》和附带首批 10 关 JSON。原始文档与关卡资料不改写。

## 本轮决定

- 按用户最新要求固定 **Unity 6000.3.25f1**，不安装 6000.3.26f1。
- **先完成本机版本，再统一接微信小游戏。** 微信 SDK 已固定依赖，但本轮不启用微信运行、广告或发布，不以桌面验收替代手机验收。
- 按最新要求提供 **50 个固定关卡 + 无尽模式**，默认三格、不用广告可通。原 10 关保留；UI 视觉设计另开对话处理。美术和 UI 先沿用占位组件，对齐已有五版设计稿的共同结构，暂不选定画风。
- 50 关与无尽规则保持；本轮补齐首页、首次说明、模式续玩、暂停/设置、结算、返回与保存后退出。
- 完整流程见 [Game-Flow-v0.3.md](Game-Flow-v0.3.md)；后续美术/UI 的资产和非资产要求见 [Art-UI-Handoff.md](Art-UI-Handoff.md)。
- 50 关与无尽数据说明见 [Expansion-v0.2.md](Expansion-v0.2.md)。

## 打开试玩

在 Unity 中打开 `Assets/PortCargo/Scenes/Bootstrap.unity`，点击 Play。原始 SampleScene 保留；构建场景只包含 Bootstrap。

独立版和构建记录见 [Build.md](Build.md)。规则：只取顶部；同类直接装入当前船，其余进入暂存。满船后自动接下一船，并从暂存左侧找首个同类继续装载。可撤销整次动作；每局有一次成功免费提示；重开需确认。首页提供闯关、无尽、分页选关、玩法说明和设置；游戏内通过“暂停”返回首页、设置或选关。

启动先进入首页。第一次开始游戏展示三页规则说明；通过后解锁下一关；再次打开从首页继续可恢复当前棋盘和撤销历史。闯关与无尽分别续玩；已通关 L10 的旧进度自动解锁 L11。没有倒计时、货币或付费系统。

## 工程地图

| 位置 | 用途 |
| --- | --- |
| `Assets/PortCargo/Scripts/Core` | 不引用 UnityEngine 的配置、规则、会话、求解与双槽存档合同 |
| `Assets/PortCargo/Scripts/Runtime` | HomeView 展示、GameFlowController 页面导航、GamePresenter 会话/保存，以及输入/反馈/开发验证工具 |
| `Assets/PortCargo/Scripts/Platform` | 本机文件存储、JSON 编解码和默认不可用的广告接口 |
| `Assets/PortCargo/Scripts/Editor` | 场景初始生成、内容校验、构建入口 |
| `Assets/PortCargo/Data/Levels/catalog.json` | 运行时 50 关目录；原 L01–L10 数据保留，新增 L11–L50 |
| `Assets/PortCargo/Data/GameTuning.asset` | 30 FPS、动效时间、提示预算、关闭的广告开关 |
| `Assets/PortCargo/Prefabs` | 箱子、船、底座、HUD、弹窗、首页制作资源 |
| `Assets/PortCargo/Tests/EditMode` | 生产规则回归，包括双槽损坏和重试 |
| `Tools` | 无 Unity 规则验证、Game View 分辨率及场景加工脚本 |
| `Docs/Evidence` | 实际测试记录、构建清单与画面 |

当前场景已完整序列化。`Create Bootstrap` 只用于空工程初次制作，会拒绝覆盖现有场景。日常修改请直接编辑现有场景/预制件；`Tools/PolishScene.cs` 是本轮追加固定反馈池的制作脚本，不属于运行时。

## 验收与交接

[Acceptance.md](Acceptance.md) 区分已测、部分通过和待测项目；[KnownIssues.md](KnownIssues.md) 记录边界；[AssetSources.md](AssetSources.md) 记录资源来源和许可。UI 设计留给单独对话；统一接微信时先接存储、生命周期、触摸、安全区，再用真实 AppID 在手机验收；保留本机版本作为规则对照。

下一版更新：[v0.6.0 · 200 关与高难无尽](Expansion-v0.6.md)，独立构建，不提交审核。

2026-10-18 下一版微信模拟器验收与船队分页修复：[v0.6.0 开发者工具验收](WeChat-Devtools-v0.6.md)。本轮未上传或提交审核，真机测试仍待完成。

- [v0.6.1 随机无尽生成与验证](Random-Endless-v0.6.1.md)
