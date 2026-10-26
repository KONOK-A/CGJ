# YLFramework AI 编程说明

YLFramework 是 Unity 游戏框架，按 Runtime、Editor 和可选集成分层。本文件适用于本目录及子目录，与上级项目约定共同生效。

## 项目导航

- `Runtime/Foundation`：异步、事件、池、计时与更新等基础机制。
- `Runtime/Core`：Module、MVC、UI 和资源接入；`Runtime/Systems`：Audio、Resource、Save、玩法等系统；`Runtime/Utilities`：通用工具。
- `Editor`：作者工具、工作台与导出；`Integrations`：可选适配；`Plugins`：随附第三方库。
- `Samples`：接入示例；`Tests`：测试与验收夹具；`Documentation`：架构和模块说明。
- 代码规范只维护 [Documentation/CodingStyle.md](Documentation/CodingStyle.md)；架构与模块入口见 [Architecture](Documentation/Architecture.md) 和 [文档首页](Documentation/README.md)。

## 工作方式

AI Agent 处理本目录及子目录的框架代码前，必须先阅读并遵守 [代码规范](Documentation/CodingStyle.md)；维护、修复、重构和新增代码均适用。

1. 先读相关模块的实现、调用方、测试和资产，确认预期行为及验证方式，不凭名称猜契约。
2. 复用现有模块与模式，做最小必要修改；不顺手全库重构、格式化或增加抽象。框架外业务仅同步必要调用/绑定。
3. 保持 Runtime/Editor 依赖方向、程序集与可选集成边界；生成代码修改模板或生成器，不手改受管输出。
4. 涉及 API、序列化或协议时，同步检查反射、UnityEvent、旧资产、JSON/Excel 键及文档。保留 `.meta`/GUID；`FormerlySerializedAs` 不代表所有数据合同已兼容。
5. 保护用户和并行修改；不自动保存脏场景、接管未知输出或覆盖冻结制品。未经授权不改依赖、权限/忽略配置、分支或提交推送。

## 验证与交付

- 按影响选择最小充分验证：代码看实际 Unity 编译与相关测试；数据迁移用真实旧数据；生成器验证正式输出与消费者。纯文档只检查内容、链接和差异。
- 同一工程只用一个 Unity Editor。程序集变化影响 HybridCLR 夹具时，先走原 Prepare；AOT/热更合同变化按原门禁验证，不覆盖旧基座。
- 源仓库提供格式只查命令；Apply 须显式限定路径。分发包缺少工具时不虚构执行，formatter 通过也不能代替结构与命名 Review。

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Mode Check
```

- 完成前检查 diff、文档与资产身份；简要报告改动、实际验证和未验证范围。过程记录放任务 Plan，不写入长期规则。
