# PortCargo

用来赚点外快的微信小游戏

《港口装箱》完整项目：200 个固定关卡、随机无尽模式、双槽存档及微信小游戏适配。仓库统一保存工程、策划、美术源文件、设计稿、截图、推广素材、工具与测试记录。

## 当前版本与项目入口

当前工程版本为 **0.7.5**。本地留存的平台回执确认开发版于 **2026-10-25 14:55（北京时间）** 上传成功；该次交付未提交审核或正式发布。真实手机广告、安全区和跳转返回验收仍待完成。详见 [v0.7.5 首页整理](Docs/Home-Layout-v0.7.5.md)及[交付记录](Docs/Evidence/v0.7.5/wechat/delivery-summary.json)。

| 目录 | 内容 |
| --- | --- |
| `Assets/`、`Packages/`、`ProjectSettings/` | Unity 游戏与平台实现、依赖和配置 |
| [ProjectContent](ProjectContent/README.md) | 原始策划、设计稿、美术资产、实机截图和推广素材 |
| [Docs](Docs/README.md) | 开发说明、版本记录、验收边界及证据 |
| `Tools/` | 关卡生成、规则与广告检查、微信构建验证等工具 |
| `Builds/`、`Backups/` | 本地构建产物与备份，不进入 Git |

2026-10-26 将原“微信小游戏”总目录下的本游戏专属资料迁入 `ProjectContent/`；原始文件逐项 SHA-256 校验一致，见[迁移清单](Docs/Evidence/reorganization-20261026/migration-manifest.json)。该目录位于 `Assets/` 外，不参与 Unity 资源导入。后续本游戏资料直接保存在本仓库；多游戏规划与整体运营数据保存在独立的“微信小游戏”目录。

## 打开工程

1. 安装 **Unity 6000.5.10f1**，通过 Unity Hub 添加仓库根目录。
2. 等待 Unity 恢复依赖并生成 Library。
3. 打开 `Assets/PortCargo/Scenes/Bootstrap.unity`，点击 Play。

当前渲染管线为 **URP 17.5.0 / Universal Renderer（Forward）**。Graphics 与全部 6 个 Quality 档位已配置；迁移结果及复现方法见 [URP 迁移说明](Docs/URP-Migration.md)。

微信 SDK 是 `Packages/com.qq.weixin.minigame` 内的完整内嵌包，基于提交 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2` 加 Unity 6.5 兼容补丁。请保留该目录及全部 `.meta` 文件；详情见 [兼容修复说明](Docs/Unity-6.5-Compatibility.md)。

## 验证与文档

- [本机构建与复现](Docs/Build.md)
- [200 关与高难无尽](Docs/Expansion-v0.6.md)
- [随机无尽生成与验证](Docs/Random-Endless-v0.6.1.md)
- [微信接入记录](Docs/WeChat-Integration.md)
- [资源来源与许可](Docs/AssetSources.md)
- [2026-10-23 Unity 6.5 修复验证](Docs/Evidence/unity-6000.5.10-20261023/)
- [2026-10-23 URP 迁移验证](Docs/Evidence/urp-20261023/)

Unity 菜单 `PortCargo > Validate Content` 会校验关卡并执行规则回归。也可使用 .NET 7 SDK 运行无 Unity 的规则检查：

```sh
dotnet run --project Tools/CoreChecks/CoreChecks.csproj -- Assets/PortCargo/Data/Levels/catalog.json
```

2026-10-23 的 Unity 6.5 验证包括 SDK 标识转换、200 关求解与规则回归、实际 Play 模式首页和帧推进；运行期间无错误。编译仍有弃用警告。后续 v0.7.5 已完成微信导出与模拟器首页检查；真机测试边界见最新交付记录。旧版文档与历史证据保留其原有版本语境，以当前 ProjectVersion 和对应版本记录为准。

URP 迁移后已通过画面对比、实际点击/撤销、200 关及规则回归、macOS 构建和独立程序重启读档测试。首页和 L200 的迁移前后截图完全一致。这是迁移当时的验证记录，后续广告与首页修改的证据分别位于 `Docs/Evidence/v0.7.0` 至 `v0.7.5`。

## 备份范围

提交 `Assets`、`Packages`、`ProjectSettings`、`Tools`、`Docs`、`ProjectContent` 及项目说明。`Library`、`Temp`、`Logs`、`Builds`、`Backups`、`UserSettings`、`TextToolDatas` 和工具编译缓存仅保留在本地；远程仓库不包含可直接运行的发布包或个人存档。`ProjectContent` 中的美术和推广 ZIP 属于正式资料交付，随源文件一起保存。

第三方资源与 SDK 保留原有许可；本仓库不另行改变其授权条款。
