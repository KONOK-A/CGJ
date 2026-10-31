# YLFramework 产品维护约定

本文件适用于 YLFramework 产品目录，并随默认框架包分发。源码仓库还受根 `AGENTS.md` 约束；导入到其他工程后服从该工程的上级约定，不把原开发宿主的工具和依赖当作消费方前置。

## 先找职责

修改代码前阅读 [代码规范](Documentation/CodingStyle.md)，按 [架构](Documentation/Architecture.md) 找到所有者，再查 [模块文档](Documentation/README.md#能力导航)、实现、调用方和测试。

- `Runtime` / `Editor` 是两个主体程序集；Foundation、Core、Systems、Utilities 是职责区，不是独立安装包。
- 基础机制归 Foundation，业务会话与 UI 归 Core，领域服务归 Systems，按需组合的工具与组件归 Utilities。配套 Editor 按所有者归位，中立支撑才进入 `Editor/Common`。
- 可选集成留在 `Integrations` 的明确编译边界，不让主体反向依赖其 DLL、宏或 SDK。开发宿主的 XF、MCP 与商业插件不成为产品必需依赖。

## 设计与合同

1. 优先复用已经正确的能力；目标要求时完整重构或淘汰旧实现，不为保留旧名增加无用兼容层，也不为统一形式新增系统、调度器或全局 Manager。
2. Core 通过自身窄合同消费资源；Resource 是资产、实例与场景租约的释放权威。Module、Audio、Timer、Action 和领域服务各自持有生命周期，不能由一个模块的关闭推导全局清理。
3. `YLFrameworkRoot` 是常驻宿主与 UI 装配入口，不是所有系统的自动启动器。轻量工具不强制创建 Root 或 Module；资源、音频 Provider 和业务启动由应用明确装配。
4. 公共 API、namespace、程序集、序列化字段、资源地址、JSON/Excel 键是不同合同。按实际消费者同步调用、反射、UnityEvent、生成模板与真实旧数据；`FormerlySerializedAs` 或保留 GUID 不能代替完整迁移验证。
5. 保留 Unity fake-null、主线程约束、重入与稳定终态；明确共享资源的借用、独占实例、取消和释放责任。不要以判空、宽泛异常捕获或静默回退掩盖错误。
6. Editor 工具只修改明确选择或拥有的资产，支持适当的 Undo / Prefab 流程；不为预览创建运行宿主、不自动保存用户场景、不接管未知生成目录或冻结基座。

## 验证与维护

- 源码仓库使用 `Tools/Testing` 的声明式入口选择相关模块；格式与 Analyzer 使用独立工具。消费方导入包没有 `Tools` 或 `Tests` 时，使用其实际可用的编译、测试与接入冒烟，不虚构仓库命令或测试结果。
- 源仓库格式工具与适用范围统一见 [代码规范](Documentation/CodingStyle.md#格式工具)；Apply 限定任务路径，排版通过不代表命名、结构或行为正确。
- 运行时改动验证实际 Unity 编译、相关生命周期和失败路径；资源/生成器验证消费者；程序集与发行变更验证编译图、导出边界和干净接收工程。IL2CPP / HybridCLR 另验裁剪、反射、metadata 与基座，不能由 EditorSimulation 替代。
- 变更落在对应模块文档，跨模块规则才更新架构，消费者入口才更新首页。一个事实维护一个权威位置，不往本文件加入阶段状态、测试数量、机器路径或历史回执。
- 保留资产 `.meta` / GUID、来源声明和适用许可证；新增文件的元数据由 Unity 生成并核对。完成前复查差异与使用入口，明确已验证和未验证范围，不夹带消费方或并行工作区的改动。
