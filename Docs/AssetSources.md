# 资源与授权

| 资源 | 来源与处理 | 本机版本用途 |
| --- | --- | --- |
| 货箱、纹样、港口、船、按钮 | 本工程 C# / uGUI 几何绘制，无外部图片 | 平面灰盒美术；四类货物颜色和纹样同时可辨 |
| 字体 | Google Fonts 的 Noto Sans SC，SIL Open Font License 1.1；源文件下载后固定字重 500、按界面用字子集化，并改名 PortCargoUI | v0.7 为静态 2048×2048 TMP SDF 单图集，源 TTF 358 字符；不在线下载字体 |
| 音效及音乐 | 本轮用正弦波合成的自有 WAV：取箱、满船、通关、拒绝及 4 秒低音量音乐循环 | 单声道 22050 Hz；最多 6 个效果声和 1 个音乐源 |
| 关卡 | 用户工作区首批 10 关数据保留；新增 40 关与确定性无尽生成器由本工程制作 | 不改原始源文件；全部新关默认三格验证可解 |
| 微信 SDK | 官方 Git 仓库固定提交，保留包版本并添加 Unity 6.5 本地兼容补丁 | v0.7 已通过转换、WASM 入口检查和模拟器 35 秒启动观察，开发版本 0.7.0 上传成功；真机广告待验 |

字体上游：[Google Fonts / Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)。完整字体许可证保存在 `Assets/PortCargo/Art/Fonts/OFL.txt`。子集的名称已经变更；源字体和 v0.1 子集哈希见 `Evidence/assets-and-sdk.json`；v0.2 为分页/无尽文字扩充到 272 字符；v0.3 为完整流程扩充到 330 字符，继续使用同一静态字体资产，该历史版本哈希见 `Evidence/v0.3/content-summary.json`。

v0.7 于 2026-10-24 从相同 SHA-256 的 Google Fonts 官方源字体重新固定字重 500，将全部 Runtime CJK 用字并入旧字集，补充 28 字后共 358 字符，保留 PortCargoUI 名称且旧字符轮廓和字宽不变；旧 TTF/字集备份及新旧哈希见 [字体重建证据](Evidence/v0.7.0/fonts/rebuild-manifest.json)。Unity 6000.5.10f1 随后编译成功并完成全字形静态烘焙：原 1024×1024 容量不足，现使用 2048×2048 单图集，补字后的 360×800 竖屏 UI 检查通过；详见 [v0.7 验证记录](Rewarded-Ads-v0.7.md) 和 [运行时检查报告](Evidence/v0.7.0/runtime/ad-checks.json)。字体重建清单中的待烘焙字段仅记录 TTF 生成时点，不代表后续烘焙仍未完成。

SDK 上游：[minigame-tuanjie-transform-sdk](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk)。基础依赖为 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`；package.json 显示 0.1.1，CHANGELOG 顶部 v0.1.34，插件字符串 202609161205，不能只用包面板版本推断代码版本。本轮在 `WXConvertCore.cs` 添加显式入口及分配函数导出、栈检查、已有 JS 符号导出、jsAudio 悬空别名清理、函数声明形式归一化、RuntimeError 构造器保护、异步 WASM 生命周期适配和 Brotli 路径兼容，并调整微信项目模板的 Babel 排除配置；继续使用原 SDK 的 GetMetaData 与键盘参数包装，未变更 Unity/SDK 版本、SDK DLL 或 Unity 安装文件。补丁说明、最终产物和保留的失败证据见 [v0.7 接入记录](Rewarded-Ads-v0.7.md)，最终产物指纹和文件哈希已记录，源码以本轮本地补丁为准。

没有将外部广告素材打入本地资源包；激励广告内容由微信 SDK 提供。后台开发版本 `0.7.0` 于北京时间 2026-10-24 12:19:04.952 上传成功，见 [工具结果](Evidence/v0.7.0/wechat/upload-confirmed-result.json)。尚无本轮成功预览二维码或真机广告通过记录；手机验收待用户进行，未提交审核、未正式发布。后续资源替换继续记录来源和许可。
