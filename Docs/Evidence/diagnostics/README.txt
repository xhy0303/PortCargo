这些是诊断或早期记录，不能作为最终通过证据。
- L01-pointer-result：直接在工具上下文发送指针状态，没有完成界面点击。
- layout-320：工具上下文 Screen 尺寸是编辑器窗口；最终尺寸在游戏 Update/协程读取。
- 早期 validation-build：同步构建遇到 Pipeline 5 秒请求超时，混入构建错误计数；最终改为 Editor update 请求队列。
- player-soak 与 second-player-soak：早期长测输入序列未完成，后改为将 UI action asset 的设备明确绑定到专用虚拟指针；不使用失败记录作长测通过依据。
- 旧截图保留用于定位画面尺寸/相机问题，最终画面见上一层 L10-final.png。
