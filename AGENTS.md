# YLFramework 仓库协作约定

本仓库用于开发、验证与发行 YLFramework，不是游戏业务工程。本文件约束整个仓库；修改框架产品时还须遵守 [框架约定](Assets/YLFramework/AGENTS.md)。

## 工程边界

- 产品源码与随包资产位于 `Assets/YLFramework/`。工程目录和开发宿主统一使用 `YLFrameworkDev`，场景为 `Assets/Scenes/YLFrameworkDev.unity`；旧名 `CGJ2024` 仅作为历史标识，不用于当前工程路径。
- 主游戏已独立迁出，不为框架开发恢复原游戏、旧接收工程或业务插件。宿主中的 XF 生态、商业 Editor 插件和 MCP 是开发环境，不是框架默认依赖。
- `Assets/YLFrameworkData/` 是作者工具生成的项目数据，不是框架产品源码。测试夹具、生成数据与发布快照按实际所有权处理，不能仅凭日期、名称或 Git 忽略状态判断可删。
- Unity 版本、包解析和编译边界分别以 `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json` / `packages-lock.json`、实际 `.asmdef` / `.asmref` 为准；文档必须与其一致。

## 文档分工

| 入口 | 唯一职责 |
| --- | --- |
| [README.md](README.md) | 源仓库开发、工具入口与发行流程 |
| [框架文档首页](Assets/YLFramework/Documentation/README.md) | 消费方安装、能力导航与使用起点 |
| [Architecture.md](Assets/YLFramework/Documentation/Architecture.md) | 产品分层、依赖方向与所有权 |
| [CodingStyle.md](Assets/YLFramework/Documentation/CodingStyle.md) | C# 命名、排版与安全改名规范 |
| [测试工具说明](Tools/Testing/README.md) | 测试前置、CLI、隔离与结果合同 |

模块 API、作者操作和迁移步骤放对应模块页；历史来源放历史索引或 Git 记录。不要在 AGENTS、首页和架构页重复粘贴规则、测试数量、阶段回执或本机路径。

## 执行原则

1. 修改前检查分支、暂存区和工作区，识别用户与并行成果；不覆盖、不回退、不顺带提交这些改动。
2. 先确认目标与验收，再沿相关定义、调用方、测试和资产查证。已知标识符精确检索，未知位置优先语义检索；不可用时正常回退，不把建索引作为前置。
3. 在授权范围内完成必要的融合、重构和淘汰，不为沿袭旧设计保留重复实现，也不为假设需求增加抽象、包装层或全局单例。普通设计细节自主推进；影响公共契约或扩大范围时说明取舍。
4. 公共 API、程序集、序列化和资源协议变化须检查真实消费者与存量数据，提供必要迁移。没有具体兼容需求时不预建旧名壳；已有数据与外部消费者不能靠“仓库无引用”判定不存在。
5. 使用现有工具和测试设施，修正根因；不以空实现、吞异常、关闭诊断、削弱断言或隐藏失败维持表面通过。

## Unity 与资产

- 只改受维护源码、模板与配置；不手补生成的 `.sln` / `.csproj`、`Library/`、`Temp/` 或 `obj/`。`Library/PackageCache/` 可定向只读，包修复使用受维护源码或明确的 Local / Embedded Package。
- 依赖变更须核对来源、版本、平台与许可证，不顺手升级 Unity 或包。锁文件由 UPM 解析生成，不伪造解析结果；不以清空 Library、删除锁文件或全量 Reimport 代替诊断。
- 移动资产保留 `.meta` / GUID；独立复制生成新身份。删除前核对序列化、字符串、Resources、生成器与导出引用，并处理配套元数据。
- Editor 写操作使用适当的 `SerializedObject`、Undo、Prefab 和保存机制；不自动保存脏场景、不改共享资源实例、不覆盖未知输出。生成代码修改其源定义或生成器。
- 主线程访问 Unity 对象，保留 fake-null 语义。按创建者和登记者管理订阅、协程、句柄、租约与释放，覆盖停用、销毁、场景切换及禁用 Domain Reload。
- 同一项目只使用一个 Unity Editor；自动化可使用工具拥有的独立测试宿主，不能接管用户进程或场景。忽略规则不是访问许可，不读取无关密钥或绕过工具权限。

## 验证与交付

- 纯文档检查事实、链接、命令、路径与 diff；未影响代码和配置的内容不重复做技术验收。
- 代码按影响验证实际 Unity 编译及相关测试；程序集、发行或 AOT 变化再扩大到编译图、裸导入、Player 和相应专项。数据迁移用真实旧数据，生成器验证正式输出与消费者。
- 日常自动化使用 `Tools/Testing/Invoke-YLTests.ps1` 与 `TestSuites.json`，格式和 Analyzer 使用各自工具；具体前置、参数与支持组合只维护在工具说明中。
- Unity 编译、离线逻辑、EditMode、PlayMode、Player 构建与运行是不同证据。读取真实结果再报告；未运行、前置不足和历史结果均不得写成当前通过。
- 完成前检查实际差异、新增文件、`.meta`、文档与使用入口；简要报告变更、实际验证和剩余限制。性能结论需要测量或明确成本依据。

## Git 与范围

只在用户明确授权当前任务时创建或切换分支、提交、推送或合并；上个批次的授权不自动延续。获准提交后按完整逻辑批次收口代码、测试、资产和必要文档，使用中文说明，可保留英文类型前缀；推送后核对远端并报告 Hash。不得擅自改写历史、强制推送、清理他人改动或强制纳入忽略文件。
