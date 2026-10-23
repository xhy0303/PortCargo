# 微信接入记录（2026-10-17）

## 当前状态

微信 SDK、原生双槽存档、安全区、前后台生命周期及音频适配已完成。用户已开通 Unity/团结快适配，模拟器进入首页，用户实机反馈操作正常，并于 2026-10-17 确认音频应已无问题。此为用户反馈，不等于所有机型、完整流程均已验收。未上传版本、未提交审核、未发布。

用户最新决定：流量主暂未开通，首发无广告，后续版本再加入。保留 IAdService / DisabledAdService 接口及 GameTuning.AdsEnabled 配置；当前不创建广告组件、不展示广告奖励入口，不需要现在提供广告位 ID。

## 锁定环境与输出

- Unity 6000.3.25f1，按用户要求保留，不升级。
- 微信 SDK commit `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`。
- AppID `wx19e83e7294013864`；开发者工具 `2.02.2610142`，本次加载基础库 `3.17.4`。
- 导出 UnityPlugin `1.3.14`，provider `wxe5a48f1ed5f544b7`。
- `Builds/WeChat/20261017-integration/minigame` 是开发者工具导入目录。
- `webgl` 是 Unity 中间产物，不能直接作为小游戏导入。
- 包内加载：WASM Brotli 5,961,027 bytes，首资源 Brotli 11,633,202 bytes，合计约 16.8 MiB。当前无需配置 CDN。输出目录包含诊断符号，目录总字节数不等于上传包大小；symbols 已由 SDK packOptions 排除。
- 纵屏、WebGL2、单线程，发布模式编译，关闭 Development、Profiler、GLX、渲染线程等额外选项。
- 原 Mac v0.4.1 交付包与桌面存档未覆盖。修改前快照 `Backups/pre-wechat-20261017.zip`。

## 适配内容

1. SDK 初始化回调 200 后才启动游戏，20 秒未完成显示可重试错误页。
2. 微信使用原生 `wx.setStorageSync/getStorageSync`。已检查当前 SDK 的 `unity-sdk/storage.js`：其 `StorageSetStringSync` 实际更新 JS 缓存后排队异步写入，因此未使用该接口作为持久化确认。
3. 自有 JS 桥接捕获存储异常；写入立即原生回读，Core 继续执行双槽、完整快照、SHA-256、语义校验和失败重试。启动前先读取两槽，平台读权限异常时停止启动，避免将读失败误认为空档。无清空存档逻辑；不会自动将 Mac 档案迁入微信。
4. 接入 wx.onHide/onShow，切后台取消瞬时效果、保存、暂停游戏与声音；返回时重绘并保留暂停页面。
5. 使用微信窗口安全区与胶囊底部留白，换算为 Unity 屏幕坐标。现有 Canvas 和命中逻辑继续共用布局。
6. 游戏退出改为微信 ExitMiniProgram；桌面仍使用 Application.Quit。
7. 微信设置 30 FPS、禁多点触控；用户已反馈实机操作与修复后音频正常。
8. 放开受配置校验保护的微信导出，保留关卡求解与规则回归。原历史证据不覆盖，新证据写入本轮目录。

广告按用户决定保持关闭。备案材料仅描述现有无广告玩法，不声称已具备广告奖励功能。

## 复现

Unity 菜单先执行 `PortCargo → Configure WeChat Preview`，等待编译完成，再执行 `PortCargo → Build WeChat Preview`。构建请求保存在 `Temp/portcargo-wechat-request.txt`，编辑器主线程空闲后切换 WebGL 并执行，不能并发构建。

该配置使用固定输出目录；需要保留上次样包时先归档，再导出。导出后执行：

```sh
python3 Tools/WeChat/inspect-export.py
node Tools/WeChat/check-storage.cjs
```

开发者工具的服务端口已由用户开启，Codex 调试连接已授权。使用自带 `wechatide` CLI（clientName=Codex）。打开 `minigame` 目录后，先看模拟器，再读 console；首次打开曾无日志，一次显式编译后获得了上述权限错误。不要无差别反复刷新或改成测试 AppID。

## 已验证与待验证

已验证：59 组规则回归、50 关解回放、WebGL 编译/SDK 转换成功、AppID/纵屏/包内资源输出检查；7 组 JS 存储桥接契约检查（微信宿主模拟，包括配额失败、读错误、损坏类型、读回不一致）。

已完成模拟器启动，用户反馈手机操作及修复后声音正常。待补充：完整教程/闯关/无尽专项验收、手机退出重进与后台恢复、不同机型安全区和性能。真实广告移至后续版本。旧模拟器空白图仅为历史错误证据，不是备案图片。

证据目录：`Docs/Evidence/wechat-20261017`；构建清单 `Builds/WeChat/20261017-integration/BuildManifest.json`。

## 2026-10-17 声音修复补充

用户已开通快适配。首次微信模拟器首页启动成功，手机反馈操作正常，但背景音乐与胜利音效缺失；普通操作音效可听见。尚不能据此认定所有流程无缺陷。

源码修复：弹窗 Show 会 RemoveAllListeners，现明确恢复 HarborButton 的单次点击声音监听，覆盖设置、选关、暂停、结算等复用按钮；绑定前先移除同一监听避免叠加。24 个场景按钮都具有反馈音源绑定。效果 Cancel 只结束视觉效果，避免结算页切换截断通关声音；切后台和音效关闭仍停止短音效。音乐播放新增等待确认，避免微信 SDK onPlay 异步回调前 isPlaying=false 被误判为曲目结束并不断切歌。用户随后实机测试并确认音频应已无问题；未提供手机型号。

自动化边界：wechatide 的画布触控返回 canvas not found，桌面 CUA 坐标点击返回 noWindowsAvailable；不能声称完成自动触控验收。旧手机预览已生成，实际包 19,018,276 bytes，主包 1,420,537 bytes；修复版包体以重新预览结果为准。

## 无广告首发与后续广告接口

当前接口位于 `Assets/PortCargo/Scripts/Platform/LocalPlatform.cs`：`IAdService.ShowRewarded(requestId, done)`；默认 `DisabledAdService` 返回 `Unavailable`。`GameTuning.AdsEnabled` 保持 false。规则层已有带 runId/stateVersion 校验的奖励提示与扩容方法，但没有连接 SDK 或向玩家开放，这些方法不代表广告已经实现。

后续开通流量主后，在 Platform 层实现微信原生激励视频适配器，由 CompositionRoot 注入；不使用面向原生移动端的 LevelPlay。另加奖励协调器，绑定 requestId、runId、stateVersion、广告位置与奖励载荷，完整观看且回调有效才发奖，重复或过期回调不发奖。加载超时 10 秒、失败重试间隔 30 秒、成功间隔 90 秒、北京时间每日最多 6 次、前 5 关禁广告；每局奖励提示和扩容分别限一次，配额与权益需持久化。提示必须先求得有效解再让玩家自主选择观看。完成取消/失败/超时/重复/过期回调测试及真实设备验收后，才开启开关。仅打开现有开关不会自动接入广告。

## 备案截图下一步

使用当前微信预览版本在手机内截取真实画面：场景栏至少 3 张（首页、关卡选择、关卡内）；玩法栏至少 3 张（初始局面、操作过程中、通关）；每个实际申报系统至少 1 张，例如设置、关卡进度、无尽模式。按后台当前页面，每张 JPG/JPEG/PNG 为 60 KB–5 MB，画面内游戏名称与后台名称一致。优先选清晰原图，不使用 Unity 编辑器、桌面试玩包或设计稿冒充微信截图。系统分类按下拉选项匹配实际功能；没有货币系统，不要保留默认的货币系统条目。

当前阻点是备案所需实际截图与账号侧审核状态，不是广告资格。由账号负责人提交备案；通过后再安排开发版上传、体验验证及版本审核。此记录不代表已完成上述后台提交。

## 2026-10-17 无广告版本续检

开发者工具关闭项目窗口后重新打开，原第一关初始存档 runId 和 payload 校验值一致，双槽 revision 从 9/10 增为 11/12，首页正常显示。未清空或注入存档。此项只覆盖模拟器初始状态重启，不代表手机中途进度已验证。证据见 `Docs/Evidence/wechat-20261017/release-readiness.json` 与 `reopen-home.jpg`。7 项存储桥接契约检查再次通过。导出检查工具新增产物指纹，同一产物再次检查保留验收记录，产物变更后重新标为待验证。

2026-10-18 下一版微信模拟器验收与船队分页修复：[v0.6.0 开发者工具验收](WeChat-Devtools-v0.6.md)。本轮未上传或提交审核，真机测试仍待完成。
