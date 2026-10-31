# YLFramework 开发仓库

本仓库用于开发、验证和发行 **YLFramework**。工程目录与 Unity 开发宿主统一使用 `YLFrameworkDev`；旧名 `CGJ2024` 是历史标识，不再作为当前工程路径。原游戏已迁出，不是框架示例、构建目标或运行依赖。

只想把框架接入自己的项目，从 [框架使用文档](Assets/YLFramework/Documentation/README.md) 开始。本页面向维护框架源码、工具和交付包的开发者。

## 文档入口

| 需要做什么 | 入口 |
| --- | --- |
| 了解能力、安装和使用 | [框架文档](Assets/YLFramework/Documentation/README.md) |
| 理解分层、依赖和生命周期 | [架构](Assets/YLFramework/Documentation/Architecture.md) |
| 修改框架源码 | [仓库协作约定](AGENTS.md) · [产品维护约定](Assets/YLFramework/AGENTS.md) · [代码规范](Assets/YLFramework/Documentation/CodingStyle.md) |
| 发现、运行和诊断测试 | [测试工具](Tools/Testing/README.md) |
| 构建或维护编译器诊断 | [Analyzer 重建与验证](Assets/YLFramework/Documentation/Modules/Attributes.md#重建与验证) |
| 查历史设计与迁移来源 | [历史索引](Assets/YLFramework/Documentation/IntegrationPlan.md) |

模块页维护具体 API 和操作，架构页维护跨模块合同，工具页维护命令与环境。历史通过记录不等于当前检出的验证结果。

## 打开与开发

1. 按 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 使用对应 Unity Editor，等待 [manifest](Packages/manifest.json) 与 [锁文件](Packages/packages-lock.json) 完成真实解析和导入。不要用 IDE 生成项目文件或离线编译代替 Unity 导入。
2. 打开 `Assets/Scenes/YLFrameworkDev.unity`。该场景使用正式 `YLFramework.prefab` 与活动主 Camera，适合检查宿主、UI 和模块装配。
3. 按任务显式初始化 Resource、配置 Provider 并启动业务 Module。进入 Play 只代表开发场景开始运行，不会自动启动全部系统；具体接入见 [Resource](Assets/YLFramework/Documentation/Modules/Resource.md) 和 [UI](Assets/YLFramework/Documentation/Modules/UI.md)。
4. 修改前检查工作区与相关程序集，保留用户场景、测试夹具和生成数据。仓库有 XF 生态、可选 SDK、URP、MCP 与商业编辑器插件，是共存开发环境，不是框架安装清单。

产品实现兼顾 Unity 2022.3 LTS 与 Unity 6.3 LTS；源码宿主版本、自动化可识别的 Editor 身份、产品实际验证范围分别由项目设置、测试声明和具体交付证据管理，不互相替代。

| 环境身份 | 当前事实 | 不应据此推导 |
| --- | --- | --- |
| 源码开发宿主 | `2022.3.62f3`，UGUI `1.0.0`、Input System `1.14.2`；版本以项目设置和 UPM 锁文件为准 | 不是把 `f3` 与 `f3c1` 当作同一受测指纹，也不要求消费方安装宿主全部依赖 |
| 具体原生能力门禁 | Resource 远端 Player 与 IL2CPP 加密后端使用 `2022.3.62f3c1` / `6000.3.27f1` 定点；对应接收环境 UGUI 为 `1.0.0` / `2.0.0` | 不是任意 LTS 补丁、平台或裁剪级别均已支持 |
| 测试执行器身份 | [TestSuites.json](Tools/Testing/TestSuites.json) 按完整 ProductVersion 识别 Editor，模块与套件另有声明 | 被执行器识别不等于所有原生能力均可用或本轮测试已通过 |

具体 Bundle、远端更新与加密边界只维护在 [Resource 平台与范围](Assets/YLFramework/Documentation/Modules/Resource.md#共存与范围)。默认产品不因此依赖宿主 Input System 或商业插件。

## 目录与所有权

| 路径 | 用途 |
| --- | --- |
| `Assets/YLFramework/` | 正式 Runtime、Editor、可选集成、样例、框架测试和随包文档 |
| `Assets/Scenes/YLFrameworkDev.unity` | 源码开发场景，不随默认框架包导出 |
| `Assets/YLFrameworkData/` | 作者工具生成的项目数据，不是框架实现；按资产 owner 维护 |
| `Assets/Resources/`、`Assets/Plugins/`、`Packages/` | 宿主共享资产、外部插件与开发依赖，不整体打入产品包 |
| `Tools/` | 测试、格式、Analyzer 与专项验收工具，不是 Player 依赖 |
| `ProjectSettings/` | 本开发宿主配置，不复制到消费方工程 |
| `.kilo/plans/` | 源码维护任务和专项证据，不是产品安装流程或永久执行规则 |
| `Library/`、`Temp/`、`obj/`、`UIElementsSchema/`、根 `.csproj` / `.slnx` | Unity / IDE 生成内容，不手工修补、不纳入 Git |
| `Build/`、`AssetBundles/` | 构建或验收产物，按真实 owner 和发布合同管理 |

旧测试夹具、可选集成和作者工具数据不能按日期或“没有 C# 引用”批量清理。Core 模块生成器仍使用 `Assets/YLFramework/Examples/Core/Modules` 作为输出；它不同于正式 `Samples`，不随默认包导出。默认导出的权威实现是 `Assets/YLFramework/Editor/Distribution/ExportUnityPackage.cs`，不是仓库所有文件的集合。

源码主线为 `main`，变更通过短期分支和 PR 整合，阶段基线由 tag 保留。项目 `.kilo/` 策略和共享 VS Code 设置继续跟踪；Agent Manager 工作树、会话状态以及 `.mcp.json`、`.vscode/mcp.json`、`opencode.json` 的本机连接配置不跟踪，由开发者在本机工具中配置。

商业 Editor Enhancers Bundle 由开发者通过自己的授权账户安装，不由公开源码仓库提供；取消跟踪时保留本机插件及其 `.meta`。这不影响不使用它们的框架开发，第三方依赖仍按各自许可处理。忽略规则和取消跟踪不删除已有 Git 历史，历史治理需要单独决策。

源码开发宿主保留 DOTween Standard 的官方安装后布局，与商业 Editor Enhancers 分开处理。版本和来源以 [DOTween 依赖声明](Assets/YLFramework/Integrations/DOTween/Dependencies.json) 为准，原始供应商文件不做换行转换，版权及原始 readme 保留，另附 [Standard 许可](Assets/Plugins/Demigiant/DOTween-LICENSE.txt)；默认产品包仍不捆绑该 SDK，也不把它视为 MIT 或 DOTween Pro。

## 检查与测试

从仓库根使用 PowerShell 7.6。工具各自的版本锁、参数与平台前置以对应说明为准；不自动安装或升级 SDK。

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Mode Check
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Tests/Test-YLGitHygiene.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Invoke-YLTests.ps1 -Action List
```

`List` 只读取声明。需要执行时，把以下变量设置为本机实际 Editor 路径和明确的独立工作根，再选择已声明的模块与层级：

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Invoke-YLTests.ps1 `
  -Action Run -Suite Quick -Module Behavior `
  -Unity $env:YL_TEST_UNITY -WorkspaceRoot $env:YL_TEST_WORKSPACE
```

Quick 是离线逻辑验证，不代表真实 Unity 生命周期。Daily、Special、包导入与 Player 各有明确前置，详见 [测试说明](Tools/Testing/README.md)。同一来源项目不启动第二个 Editor；CLI 可以使用其拥有的独立宿主，不接管当前编辑器。

GitHub 的 `Repository Tooling (No Unity)` 检查 Git 索引卫生、C# 格式、Analyzer 的 .NET 构建测试及测试工具合同，不需要 Unity 安装或许可证，不代表框架 Unity 编译、EditMode、PlayMode 或 Player 验收通过。

需要定位单个用例时可用 Unity Test Runner 或连接当前 Editor 的工具；不要另维护一套日常叶子名单。修改代码、测试声明或输入资产后重新发现选集，交付时读取本轮摘要、XML 和进程结果。

### 可选集成测试

可选集成按自身依赖、宏和测试程序集显式准备，不因源码目录存在就视为已经接入。HybridCLR 首次检出或相关程序集重新编译后，先在非 Play 的 Editor 主线程调用 `YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Prepare()`，再运行 loader / Module PlayMode 测试。

该步骤从当前已编译程序集生成 `Tests/Integrations/HybridCLR/PlayMode/Resources/YLHybridCLRTests` 下的 bytes 与 manifest；这些是本机测试输入，不纳入 Git，不复用其他机器或旧程序集的快照。该目录只跟踪身份 `.meta` 与 `.gitkeep`，生成内容由精确规则排除。具体选装合同见 [HybridCLR](Assets/YLFramework/Integrations/HybridCLR/README.md)，原生 Player 验收另按测试工具声明执行。

## 发行与贡献

- 通过 `YLFramework/Toolkit/导出框架包` 或正式 Export API 生成 `.unitypackage`，在干净接收工程安装必要依赖并验证导入；[产品安装与导出边界](Assets/YLFramework/Documentation/README.md#安装与分发) 是消费方说明。
- 发布前按影响验证主体编译图、导出内容、接收工程和目标 Player。`Tools/Testing/Tests/Test-YLDistribution.ps1` 提供显式分发验证；可选 HybridCLR、原生输入和音频输出保留独立验收。
- 可选集成单独携带其源码、依赖和许可证，不把 SDK、商业插件、测试探针、缓存或宿主配置混入默认产品。
- 变更同步其权威文档和必要迁移；不要为每轮工作新建重复报告。只在当前任务获得授权后提交和推送，提交仅包含该逻辑批次。

仓库采用 [MIT License](LICENSE)，框架及随附来源见 [产品许可证](Assets/YLFramework/LICENSE.md)。外部包与商业插件遵循各自许可，仓库许可不赋予它们再分发权。
