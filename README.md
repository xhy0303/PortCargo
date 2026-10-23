# PortCargo

用来赚点外快的微信小游戏

《港口装箱》Unity 工程：200 个固定关卡、随机无尽模式、双槽存档及微信小游戏适配。仓库保存可继续开发的工程、资源、工具与测试记录。

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

本次 Unity 6.5 验证包括 SDK 标识转换、200 关求解与规则回归、实际 Play 模式首页和帧推进；运行期间无错误。编译仍有弃用警告，微信导出及此引擎版本的真机测试尚未重做。旧版文档与历史证据保留其原有版本语境，以当前 ProjectVersion 与兼容修复说明为准。

URP 迁移后已通过画面对比、实际点击/撤销、200 关及规则回归、macOS 构建和独立程序重启读档测试。首页和 L200 的迁移前后截图完全一致；微信端尚待单独复测。

## 备份范围

提交 `Assets`、`Packages`、`ProjectSettings`、`Tools`、`Docs` 及项目说明。`Library`、`Temp`、`Logs`、`Builds`、`Backups`、`UserSettings`、`TextToolDatas` 和工具编译缓存仅保留在本地；远程仓库不包含可直接运行的发布包或个人存档。

第三方资源与 SDK 保留原有许可；本仓库不另行改变其授权条款。
