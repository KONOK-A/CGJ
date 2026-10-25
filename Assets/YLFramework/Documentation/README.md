# YLFramework 文档

YLFramework 是整体交付、按需使用的 Unity 游戏框架。运行时主体为 `YLFramework.Runtime`，编辑器主体为 `YLFramework.Editor`；功能目录不是独立安装包，使用某项工具也不要求启动全部系统。

本文档说明当前源码的接入方式与行为边界，不是完整 API Reference。方法签名以源码和 Editor 帮助为准，平台支持以各模块的限制为准。

## 阅读顺序

| 需要做什么 | 入口 |
|---|---|
| 导入到其他工程 | [独立工程安装](#独立工程安装) |
| 理解目录、namespace、依赖和所有权 | [架构总览](Architecture.md) |
| 选择需要的能力 | [模块导航](#模块导航) |
| 启动资源并连接 Module / Audio | [Resource 最小接入](Modules/Resource.md#可运行最小接入) |
| 打开界面、播放音频或组合动作 | [UI](Modules/UI.md) · [Audio](Modules/Audio.md) · [Action](Modules/Action.md) |
| 使用 Editor 帮助或导出框架 | [帮助与导出](#帮助与导出) |
| 修改框架源码 | [代码规范](../CodingStyle.md) · [AI 编程说明](../AGENTS.md) |
| 查询旧版本的设计和验收证据 | [历史记录](IntegrationPlan.md) |

## 目录结构

框架根目录为 `Assets/YLFramework/`：

| 目录 | 说明 |
|---|---|
| [`CodingStyle.md`](../CodingStyle.md) | 代码、命名、bool 分类与安全改名的唯一规范 |
| [`AGENTS.md`](../AGENTS.md) | 面向 AI 编程的项目导航、工作流程与验证要求 |
| [`Runtime/`](../Runtime/) | 运行时框架代码（Foundation / Core / Systems / Utilities） |
| [`Editor/`](../Editor/) | Editor-only 主体、作者工具、帮助与导出 |
| [`Integrations/DOTween/`](../Integrations/DOTween/) | 可选 Action DOTween 扩展，不在默认导出范围 |
| [`Integrations/Excel/`](../Integrations/Excel/) | 可选 Editor-only xlsx 后端；Runtime 字段合同在 Utilities/Excel |
| [`Integrations/DomainExcel/`](../Integrations/DomainExcel/) | 依赖可选 Excel 后端的领域作者接入 |
| [`Integrations/HybridCLR/`](../Integrations/HybridCLR/) | 可选官方 HybridCLR 9.0.3 接入，独立导出；不自动接入原游戏 |
| [`Tests/`](../Tests/) | EditMode / PlayMode 测试 |
| [`Plugins/`](../Plugins/) | 仅含批准的官方 BouncyCastle 2.7.0 HKDF 依赖及 SOURCE 来源；不包含宿主商业插件 |
| [`Prefabs/`](../Prefabs/) | 自有 UI 模板与正式工具（`YLFramework.prefab`、`UITips.prefab`、`ColorSelector.prefab`） |
| [`Samples/`](../Samples/) | ResourceStartup 的显式启动、模拟/Bundle、内容加密与内置部署示例 |
| `Documentation/` | 本文档目录 |

Runtime 分为 Foundation、Core、Systems、Utilities 四个职责区，Editor 按对应功能及 Help / Distribution 组织。namespace 表达功能域，不机械复制目录；精确归属和升级注意事项见 [Namespace 与迁移](Architecture.md#namespace-与迁移)。

## 模块导航

### Foundation（框架基础机制）

| 模块 | 作用 | 文档 |
|---|---|---|
| Singleton | 普通、Mono、Prefab 与 ScriptableObject 单例及会话生命周期 | [Singleton](Modules/Singleton.md) |
| Pool | 类对象、安全池、GameObject 与集合池；回收责任由调用方持有 | [Pool](Modules/Pool.md) |
| Events | 实例、命名、类型事件与 Unity 消息适配；Core EventManager 为薄入口 | [Events](Modules/Events.md) |
| Binding | 可监听属性、列表、字典与 PlayerPrefs 属性 | [Binding](Modules/Binding.md) |
| IOC | 注册、解析、构造与成员注入 | [IOC](Modules/IOC.md) |
| AssemblyTool | 按完整类型名查找 Type | [AssemblyTool](Modules/AssemblyTool.md) |
| Attributes | 展示元数据、DisableEdit 与 Core 编译器诊断 | [Attributes](Modules/Attributes.md) |
| Timing | 秒/帧计时、精确句柄、Scope、显式复用与终态等待 | [Timing](Modules/Timing.md) |
| Serialization | 通用 SerializedDictionary，与 Save 文件策略分离 | [字典用法](Modules/Save.md#serializeddictionary-用法与边界) |
| Async / Update | 协程启动与观察、基础请求、三相位更新订阅 | [Async 源码](../Runtime/Foundation/Async/) · [Update 源码](../Runtime/Foundation/Update/) |

### Core（业务生命周期与 UI）

| 模块 | 作用 | 文档 |
|---|---|---|
| Module / MVC / View | Module 统一持有业务会话、注册、启动与退出；通过窄合同消费资源 | [Core 职责](Architecture.md#core-职责归口) |
| UI | Panel/View、根与层、模态、输入、适配与 Tips | [UI](Modules/UI.md) |

### Utilities（通用工具）

| 模块 | 作用 | 文档 |
|---|---|---|
| Extensions / StringTools | Unity、System、几何扩展与显示格式工具 | [Extensions](Modules/Extensions.md) |
| FSM | 调用方驱动与回收的轻量纯代码状态机 | [FSM](Modules/FSM.md) |
| Grid | 矩形与稀疏二维数据结构 | [Grid](Modules/Grid.md) |
| Json | JSONObject 树解析与构建，不替代 Json.NET 或 Save | [Json](Modules/Json.md) |
| Table | 通用表与多值索引，不接管资源所有权 | [Table](Modules/Table.md) |
| ZipTool | Zip / GZip 与显式归还的归档读取器 | [ZipTool](Modules/ZipTool.md) |
| ColorSelector | 按需使用的取色控件与 Core Panel 窗口 | [ColorSelector](Modules/UI.md#colorselector) |
| Overlap / CharacterController2D / MoveablePlatform | 检测、2D 角色移动与平台组件及对应 Editor | [Utilities 源码](../Runtime/Utilities/) |
| Excel | Runtime 字段合同；xlsx 作者后端另行选装 | [配置数据接入](Architecture.md#配置数据接入) |

### Systems（领域服务与规则）

| 模块 | 作用 | 文档 |
|---|---|---|
| Action | 动作组合、命名执行、安全句柄、屏幕过渡与 BCL Observable | [Action](Modules/Action.md) |
| Audio | Clip / 命名播放、Music / Voice / Sound、Crossfade、空间音效与可选 Mixer | [Audio](Modules/Audio.md) |
| Resource | 显式启动、Catalog、资产/实例/场景租约、构建发布与更新事务 | [Resource](Modules/Resource.md) |
| Save | 唯一 SaveStore、模型与槽位、异步请求、缓存与显式备份恢复；仅当前 v2 | [Save](Modules/Save.md) |
| Localization / DataAssets | 多语言文本图片、SO 根/子资产作者流程与登记句柄 | [配置数据接入](Architecture.md#配置数据接入) |
| Skill / Buff / Mission / Reddot / Logging | 领域运行态、宿主清理、配置作者工具、红点来源与显式文件日志 | [领域玩法接入](Architecture.md#领域玩法接入) |
| Equipment | Asset / Script 双创建、单一挂载归属与装备配置窗口 | [Equipment 接入](Architecture.md#equipment-接入) |
| StateMachine | 配置驱动的完整状态机与可视化作者工具，与轻量 FSM 分离 | [Runtime 源码](../Runtime/Systems/StateMachine/) · [Editor 源码](../Editor/Systems/StateMachine/) |
| HTTP | GET / Form / JSON / PUT / DELETE、稳定终态与独占传输取消 | [HTTP / AsyncOperation 边界](Architecture.md#http--asyncoperation-边界) |

### 可选集成

下列目录只在源码仓库或显式选装后存在，不随默认框架包分发，也不成为主体的必需依赖：

| 集成 | 使用条件 | 说明 |
|---|---|---|
| DOTween | 安装供应商 DLL、启用 `DOTWEEN`，业务 asmdef 显式引用集成程序集 | Action 扩展，`autoReferenced=false` |
| Excel | 完整携带 `Integrations/Excel/Editor` 及其依赖与许可证 | Editor-only xlsx 作者后端 |
| DomainExcel | Excel 后端及 `YL_DOMAIN_EXCEL` | Skill / Buff / Mission / Equipment 作者接入 |
| HybridCLR | 官方包 `9.0.3` 及对应原生工具链 | 独立导出；详见选装目录的 [README](../Integrations/HybridCLR/README.md) |

不提供 XFAB 兼容入口或自动回退；YL 的正式资源与音频接入由自身 ResourceSystem 完成。

### 其他内容

- [Help](Modules/Help.md)：Editor-only主体中的共享Help与各领域提供方，原菜单/窗口/Provider与配置身份保持。
- [ColorSelector](Modules/UI.md#colorselector)：四个取色类型完整归 `Runtime/Utilities/UI/ColorSelector/`，不是框架底层；正式 `YLFramework.UI.ColorSelectorWindow` 与 `Prefabs/ColorSelector.prefab` 保留，由业务 Module 显式打开，旧样例已退役。

## 帮助与导出

- 菜单：`YLFramework/帮助`、`YLFramework/Core/帮助`、`YLFramework/Toolkit/帮助`、`YLFramework/Action/帮助`、`YLFramework/Audio/帮助`。浏览帮助不执行示例或创建运行宿主，扩展方式见 [Help](Modules/Help.md)。
- 常用工具：`YLFramework/Resource/Workbench`、`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`、`YLFramework/Core/分析器规则`（Project Settings > YLFramework > Analyzers）。
- `YLFramework/Toolkit/导出框架包` 的默认根为 `Editor`、`Plugins/BouncyCastle`、`Prefabs`、`Runtime`、`Samples`、`Documentation` 六个目录，以及 `LICENSE.md`、`CodingStyle.md`、`AGENTS.md` 三个根文件；使用 `Recurse`，不使用 `IncludeDependencies`。普通模块源码、资源与文档随这些根导出。
- 自动化可调用 `YLFramework.Editor.Distribution.ExportUnityPackage.Export(absolutePackagePath)` 向已有本地父目录导出同一内容，默认不覆盖、不保存其他资产；菜单入口会先保存资产。Resource 发布快照是另一类交付，见 [Resource](Modules/Resource.md#轮换与交付)。
- `Plugins/BouncyCastle/` 完整递归且保留原导入平台；不递归整个 Plugins，更不复制或改变宿主商业插件配置。JSONObject/SharpZipLib 的现有来源声明与许可证随 Runtime 保留。
- `Runtime/Core/Analyzers/YLFramework.Analyzers.dll` 是编译器输入，不是 Player 运行依赖；十项诊断默认 Warning，新增 Inject 声明、View 入口加载 Panel、音频协程等待检查。设置页可选 asmdef/预定义业务程序集、预览并修改原生 ruleset，保留其他规则与继承；只读接入自检不把零诊断当成功。配置、Error 恢复、升级与源码重建见 [Attributes](Modules/Attributes.md#core-使用诊断)。
- 默认不导出 `Tests/`、根 `Integrations/`、`Examples/`、游戏资产/Bundle、`Assets/YLFrameworkData`、SDK 工具项目、NuGet 缓存、临时探针、中间产物及包外 DOTween。可选扩展源码须另行满足依赖；Analyzer 重建源码保留在仓库 `Tools/YLFramework.Analyzers`，不随 UnityPackage 提供 SDK 项目。
- 框架需要 UGUI、Json.NET 与随包的 BouncyCastle，不是零依赖安装包；不需要原 XF 生态或商业 Editor 插件。`Library/` 下的 XML 是构建产物，不默认导出，缺 XML 时 Help 仍展示可反射的声明。

## 测试

- EditMode：`Tests/EditMode/{Foundation,Core,Systems/<模块>,Utilities}/`；Editor 行为位于 `Tests/EditMode/Editor/<功能>/`，如 Help、Resource 和 Core。
- PlayMode：`Tests/PlayMode/{Foundation,Core,Systems/<模块>,Utilities}/`，覆盖单例、Timing、Action、Audio 等运行行为。普通单模块测试随职责归位，混合验收保留跨模块范围。
- `Tests/Acceptance/Core/` 与 `Tests/Fixtures/Core/` 分别保留显式验收入口及夹具；Tests 整体不默认分发。目录重排不更名测试程序集，也不以源码存在冒充测试发现或通过。
- DOTween：`Tests/Integrations/DOTween/` 下两个独立测试程序集，受 `DOTWEEN` + `UNITY_INCLUDE_TESTS` 约束。
- HybridCLR：`Tests/Integrations/HybridCLR/` 的独立 EditMode/PlayMode 叶子，精确约束 9.0.3；Editor Simulation、替身与真实 IL2CPP 证据分别记录，不把单元测试冒充原生热更。

HybridCLR 源码仓库首次检出或相关程序集重新编译后，先在非 Play 的 Editor 主线程调用 `YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Prepare()`，再运行 loader/Module PlayMode 测试。该步骤从当前已编译程序集生成 `PlayMode/Resources/YLHybridCLRTests` 下的 `.bytes` 和 manifest JSON；这些本机输入及其 meta 不纳入 Git，不能复用其他机器或旧程序集的快照。

导出的框架包不包含 `Tests/`，测试仅在源码仓库内运行。

运行入口为 Unity Test Runner。API 示例编译、Unity 编译、EditMode、PlayMode、Player 构建与运行是不同证据；测试数量和具体制品结果只放在对应的历史记录中，不作为本文的当前通过承诺。

## 独立工程安装

以 Unity `2022.3.62f3c1` 为安装基线，先在目标工程准备下列依赖，再通过 `Assets > Import Package > Custom Package` 导入框架 `.unitypackage`。不要复制来源工程的 manifest、Library 或游戏插件来补依赖；来源工程含有与框架无关的包，其声明也不能代替目标工程实际解析的版本。

Analyzer 的发行验收目标为 Windows / Unity 2022.3 LTS、Unity 6.3 LTS / Visual Studio、Rider；以每份发行记录的实际版本与结果为准，不将目标矩阵当作已通过证明。未完成环境验证时不得扩写支持声明，其他系统、Unity版本及IL2CPP不由本目标自动保证，详见 [发布支持边界](Modules/Attributes.md#发布支持边界)。

| 依赖 | 安装来源与版本 | 用途 |
| --- | --- | --- |
| UGUI | Unity Package Manager，使用该 Editor 对应版本；独立安装历史基线为 `1.0.0` / builtin | `UnityEngine.UI` |
| Json.NET | Unity Registry，`com.unity.nuget.newtonsoft-json` `3.2.1` | Runtime/Editor直接依赖 |
| BouncyCastle | 框架包内`Plugins/BouncyCastle`，2.7.0官方netstandard2.0制品 | HKDF-SHA256 |
| Unity内建模块 | 同版本Editor正常提供/解析的UI、Audio、AssetBundle、UnityWebRequest等模块 | 不复制UnityEngine或Windows系统DLL |

Input System 为可选输入后端；最小工程可用 Legacy Input。使用新输入时需另装兼容的 Input System（现有基线为 `1.14.2`），配置 Active Input Handling 并重启 Editor，具体限制见 [UI 输入边界](Modules/UI.md#输入边界)。框架不要求 URP；使用现有渲染管线时，由应用维护对应材质。

不需要原 XF / XFAB / XFFSM、MCP、DOTween 或历史 XF Logger 补丁。YL 自有 `LoggerManager` 默认被动，只有应用显式启用才接管日志，见 [领域玩法接入](Architecture.md#领域玩法接入)。

导入并确认 Unity 编译成功后，再按 [Resource 最小接入](Modules/Resource.md#可运行最小接入) 生成目标工程自己的资源，或按 [UI 用法](Modules/UI.md) 使用轻量默认根与完整模板。随包不含源码仓库的 Acceptance 场景、测试程序集或历史接收工程。

Resource 远端更新、Bundle 加密与 HybridCLR 各有额外平台和环境限制；安装主体成功不等于这些能力已在任意目标平台可用。

## 历史与维护

[IntegrationPlan.md](IntegrationPlan.md) 保留融合蓝图、旧交付身份、失败与复验记录；其中的本机路径、提交、测试数量和“本轮”只属于记录当时的快照，不是安装步骤或当前验收结论。源码仓库的 `.kilo/plans/` 与验收工具不随默认包分发，读取产品指南不需要这些文件。

维护文档时优先更新对应模块页，首页只保留导航，架构页只保留跨模块规则。不要在多个页面复制阶段回执，或将旧包、旧 Player 与当前源码的验证混为一谈。
