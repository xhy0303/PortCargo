# URP 迁移记录（2026-10-23）

本工程在 Unity **6000.5.10f1** 上由 Built-in 迁移到 **URP 17.5.0**。迁移前的完整远程备份为 `a4d72061a84281bff34ae4f4de5868475e546ef2`；依赖版本通过 Unity Package Manager 安装并锁定。

## 配置

- Pipeline：`Assets/PortCargo/Settings/Rendering/PortCargoURP.asset`。
- Renderer：同目录 `PortCargoRenderer.asset`，Universal Renderer、Forward、默认 Renderer 索引 0。
- Graphics 默认管线及 Very Low / Low / Medium / High / Very High / Ultra 全部明确引用该 Pipeline。
- 维持 Gamma 与渲染比例 1；开启 SRP Batcher。关闭 HDR、MSAA、深度/不透明纹理、光照、阴影、后处理与 Depth Priming；Intermediate Texture 为 Auto，无 Renderer Feature。
- Unity 自动生成并注册 `Assets/UniversalRenderPipelineGlobalSettings.asset`，其默认 Volume 引用 `Assets/DefaultVolumeProfile.asset`。构建后 Unity 自动填充了 19 个默认 Volume 组件，两者及 `.meta` 都随仓库保存；相机后处理仍然关闭。
- Bootstrap 和 SampleScene 的相机均添加 UniversalAdditionalCameraData，使用 Renderer 0，关闭相机 HDR/MSAA、阴影、后处理和额外纹理。

这是当前纯 UI 游戏的轻量配置。以后如果增加受光 3D 物体或 2D 光照，需要相应调整 Renderer 与光照设置。

## 资产处理依据

两个场景均没有 Mesh/Sprite/Particle Renderer、Light、Reflection Probe、Light Probe Group 或 PPv2。游戏图像由 uGUI Canvas 绘制，字体使用 TextMeshPro/Mobile/Distance Field；UI/Default 与该字体 Shader 在 URP 下正常支持，因此无需替换成 URP Lit，也没有改变 Canvas 模式、字体、贴图或材质参数。

迁移前已有的 7 个材质审计条目完全一致。安装 Shader Graph 依赖后，已有 TMP 示例 Shader Graph 又能导入 4 个预览材质条目；这些示例不用于游戏画面，也不是本次新增生产材质。

没有烘焙光照或探针需要重烘焙。清除了 Bootstrap 旧的内置占位 LightingData 引用。场景与 ProjectSettings 通过 Unity API 保存，Unity 6.5 同时更新了部分序列化字段，包含引擎自动归一化的 Android 最低版本 26、Input System 预加载引用及 WebGL batching 字段；本次没有做 Android 导出验收。

## 验证

证据目录：[Evidence/urp-20261023](Evidence/urp-20261023/)。

- `before-audit.json` / `after-audit.json`：两场景重新加载后，缺失脚本为 0；URP 相机、Graphics、全部 Quality 引用正确。
- `saved-pipeline.json`：活动实例为 UniversalRenderPipeline，Renderer、GlobalSettings 和默认 Volume 引用有效且已保存。
- `builtin-visual.json` / `urp-visual.json`：在实际 Play 模式以 720 × 1280 运行，首页及 L200 渲染正常，无缺字或错误 Shader。
- `visual-comparison.json`：两组迁移前后 PNG 文件的 SHA-256 完全一致。
- 通过 Input System 向真实 UI 队列发送鼠标事件：顶箱射线命中、移动版本从 0 到 1、撤销后版本为 2 且精确恢复原局面；帧持续推进。测试使用内存存档，测试区间内真实存档哈希未变化。
- `play-console.json`：编译失败标记为 false，Console 错误为 0。保留了 SDK 弃用警告、URP 首次生成全局设置提示和 Metal memoryless depth load/store 提示，不声称零警告。
- `validated-solutions.json` / `prebuild-rules.txt`：200 关逐关求解与回放，规则、随机无尽、存档和权益回归全部通过，共 212 条检查记录。
- `macos-build-status.json` / `macos-build-summary.json`：独立开发包构建 Succeeded，0 错误、42 警告。警告主要为已有开发检查 API 弃用，另有 Pipeline 调试工具禁用提示（指 CLI 连接工具，不是 URP 渲染管线）。
- `player-flow.write.txt` / `player-flow.read.txt`：真实 macOS Player 在 320 × 568 完成首页、选关、说明、暂停、设置、闯关结算、无尽重开/结算/下一程与退出取消；另启进程精确恢复两模式局面并实际撤销，两次均 `RESULT PASS`、退出码 0。相应 PNG 和 Player 日志一并保存。

测试包位于本地 `Builds/macOS/20261023-urp-validation/PortCargo.app`，使用独立临时存档目录 `/private/tmp/portcargo-urp-20261023-flow-saves`。Builds 和存档不提交到 Git。此开发包用于验证，不是新的微信发布包。

微信小游戏导出、微信开发者工具及手机真机尚未在此 URP 配置下重新验收；本地 Editor/macOS 结果不能替代微信平台验收。

## 复现入口

使用 Unity CLI `1.0.0-beta.14` 与 Pipeline `0.8.0-exp.1`，项目打开且处于 Edit Mode 时：

```sh
unity command run_script --caller plugin --skill migrate-birp-to-urp --project-path "$PWD" --format json -- --file Tools/URP/ConfigureUrp.cs --entry ConfigureUrp.Validate
unity command run_script --caller plugin --skill migrate-birp-to-urp --project-path "$PWD" --format json -- --file Tools/URP/MigrationAudit.cs --entry MigrationAudit.Run --args '["after"]'
```

实际 Play 模式下运行 `Tools/URP/VisualProbe.cs` 的 `VisualProbe.Run("urp")` 可复现画面与输入测试。它会临时替换当前运行实例的存档服务并进入 L200，结束后应退出 Play Mode；不会保存测试场景。证据写入 Docs，不导入 Assets。

`ConfigureUrp.Apply()` 是可复现配置工具，会保存两个场景，应仅在没有未保存场景改动的 Edit Mode 执行。日常使用无需重复迁移。

本次 macOS 构建使用 `unity command build`，目标 `StandaloneOSX`，场景为 Bootstrap，选项为 `Development` 和 `DetailedBuildReport`。先把 `PortCargo.Editor.BuildTools.ValidationEvidence` 设为本轮证据目录，避免覆盖历史回归记录。构建状态以 `build_status` 返回的 `Succeeded` 为准。

官方参考：[URP Asset 与 Renderer](https://docs.unity3d.com/6000.5/Documentation/Manual/urp/urp-asset-and-renderer.html)、[Built-in 到 URP 迁移](https://docs.unity3d.com/6000.5/Documentation/Manual/urp/upgrading-from-birp.html)。
