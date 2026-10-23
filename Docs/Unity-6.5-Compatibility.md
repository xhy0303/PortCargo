# Unity 6000.5.10f1 安全模式修复（2026-10-23）

用户已切换到 Unity `6000.5.10f1 (3bd4f66ad299)`。本轮保留此版本，修复微信 SDK 编译阻塞；工程已自动退出安全模式，可打开 Bootstrap 场景并进入 Play 模式。

## 原因与补丁

原锁定微信 SDK 提交为 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`。`Runtime/WXRuntimeExtDef.cs:135` 调用 `Object.GetInstanceID()`，在 Unity 6.5 中触发 CS0619，阻止整个工程编译。

SDK 已复制为项目内嵌包 `Packages/com.qq.weixin.minigame`。manifest 保留原 Git 提交，Unity 自动更新 packages-lock 将来源标记为 embedded；内嵌包优先于 manifest，清理 Library 不会丢失补丁。升级 SDK 时必须核对并迁移补丁，不能直接删除内嵌包，否则会恢复原编译错误。

源码补丁只涉及 `Runtime/WXRuntimeExtDef.cs`：Unity 6.5+ 使用完整 EntityId 作为字典键，为 SDK 分配稳定的非零 int token；旧 Unity 分支仍使用原接口。原因是 `wx-perf.dll` 消费该回调后直接拆箱为 Int32，直接返回 EntityId 或 ulong 虽可编译，却会在运行时类型转换失败。token 只用于该 SDK 的性能统计、去重和报告，不能用于 Unity 对象反查或跨会话存档。字典不随重复初始化清空，不保留 Unity 对象引用，也不截断64位ID或用可能碰撞的哈希代替。

## 验证

- 编辑器 `6000.5.10f1` 正常连接，脚本编译成功；Bootstrap 场景正常打开。
- `Tools/ValidateUnity65Sdk.cs` 经 Unity Pipeline eval_file 执行：Int32 拆箱、同对象稳定、不同对象唯一、null 返回0、重复注册稳定、低32位相同而高32位不同的 EntityId 不碰撞，全部通过。
- 生产 `BuildTools.ValidateContent()` 完成200关求解/解回放及212行规则检查报告。调用方曾因30秒超时未收到响应，但 Unity 内部任务随后完成并写出全部结果；没有将超时响应本身当作通过证据。
- Play 模式首页正常；帧计数由643推进到645，随后为666；GamePresenter 存在；运行期 Console 0 errors。已退出测试播放，保留编辑器打开。
- 编译阶段仍有旧 API 弃用警告，保存于 `compiler-warnings.txt`；本轮没有把警告屏蔽或称为零警告编译。测试前清理过 Console，运行期无错误与编译期警告分开记录。
- 尚未重新导出 WebGL/微信包，未验证此引擎版本的微信真机运行或外部性能分析服务；历史微信构建证据不代表6.5验证。

证据目录：[本轮验证文件](Evidence/unity-6000.5.10-20261023/)。修复前配置及编译错误保存在 `Backups/unity-6000.5.10-repair-20261023`。

参考：[Unity 内嵌依赖说明](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-embed.html)、[Unity 对象标识迁移说明](https://docs.unity.com/en-us/engine/7000.0/manual/scripting/get-started/instanceid-to-entityid-migration)。接口行为同时核对了本机6.5程序集。
