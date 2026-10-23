# 资源与授权

| 资源 | 来源与处理 | 本机版本用途 |
| --- | --- | --- |
| 货箱、纹样、港口、船、按钮 | 本工程 C# / uGUI 几何绘制，无外部图片 | 平面灰盒美术；四类货物颜色和纹样同时可辨 |
| 字体 | Google Fonts 的 Noto Sans SC，SIL Open Font License 1.1；源文件下载后固定字重 500、按界面用字子集化，并改名 PortCargoUI | 静态 1024×1024 TMP SDF 图集，不在线下载字体 |
| 音效及音乐 | 本轮用正弦波合成的自有 WAV：取箱、满船、通关、拒绝及 4 秒低音量音乐循环 | 单声道 22050 Hz；最多 6 个效果声和 1 个音乐源 |
| 关卡 | 用户工作区首批 10 关数据保留；新增 40 关与确定性无尽生成器由本工程制作 | 不改原始源文件；全部新关默认三格验证可解 |
| 微信 SDK | 官方 Git 仓库固定提交，依赖保留 | 本轮本机运行不初始化微信 SDK |

字体上游：[Google Fonts / Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)。完整字体许可证保存在 `Assets/PortCargo/Art/Fonts/OFL.txt`。子集的名称已经变更；源字体和 v0.1 子集哈希见 `Evidence/assets-and-sdk.json`；v0.2 为分页/无尽文字扩充到 272 字符；v0.3 为完整流程扩充到 330 字符，继续使用同一静态字体资产，当前哈希见 `Evidence/v0.3/content-summary.json`。

SDK 上游：[minigame-tuanjie-transform-sdk](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk)。实际依赖为 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`；package.json 显示 0.1.1，CHANGELOG 顶部 v0.1.34，插件字符串 202609161205，不能只用包面板版本推断代码版本。哈希以锁文件和证据为准。

没有加入外部广告素材、商用音轨、人物图片或远端素材下载。当前形态是可以试玩的本机 MVP，后续最终美术替换需继续记录来源和许可。
