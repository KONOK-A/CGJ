# YLFramework 文档

YLFramework 是统一交付、按需使用的 Unity 游戏框架，面向游戏业务层提供对象池、单例、事件、数据绑定、状态机、IOC、动作编排、音频、资源管理、存档、UI 等能力。

> 当前框架仍在持续开发和完善中，部分模块缺少测试或存在明确的待完善项，详见各模块文档的“当前状态”。
> 本文档是导航性质的内部文档，不是完整 API Reference。
> 各模块的既有阶段结果与 Player 证据只适用于其原版本和制品，不代表当前目录与 namespace 迁移已通过新一轮测试、干净导入或 Player 验收。

自有代码的格式、命名、bool 语义与安全改名规则统一见框架根目录的 [代码规范](../CodingStyle.md)；AI 编程工作说明见 [AGENTS.md](../AGENTS.md)。

## 目录结构（当前）

框架根目录为 `Assets/YLFramework/`：

| 目录 | 说明 |
|---|---|
| [`CodingStyle.md`](../CodingStyle.md) | 代码、命名、bool 分类与安全改名的唯一规范 |
| [`AGENTS.md`](../AGENTS.md) | 面向 AI 编程的项目导航、工作流程与验证要求 |
| [`Runtime/`](../Runtime/) | 运行时框架代码（Foundation / Core / Systems / Utilities） |
| [`Editor/`](../Editor/) | Editor-only主体；功能工具、模块帮助提供方与共享Help分区 |
| [`Integrations/DOTween/`](../Integrations/DOTween/) | 可选 Action DOTween 扩展，不在默认导出范围 |
| [`Integrations/Excel/`](../Integrations/Excel/) | 可选 Editor-only xlsx 后端；Runtime 字段合同在 Utilities/Excel |
| [`Integrations/DomainExcel/`](../Integrations/DomainExcel/) | 依赖可选 Excel 后端的领域作者接入 |
| [`Integrations/HybridCLR/`](../Integrations/HybridCLR/) | 可选官方 HybridCLR 9.0.3 接入，独立导出；不自动接入原游戏 |
| [`Tests/`](../Tests/) | EditMode / PlayMode 测试 |
| [`Plugins/`](../Plugins/) | 仅含批准的官方 BouncyCastle 2.7.0 HKDF 依赖及 SOURCE 来源；不包含宿主商业插件 |
| [`Prefabs/`](../Prefabs/) | 自有 UI 模板与正式工具（`YLFramework.prefab`、`UITips.prefab`、`ColorSelector.prefab`） |
| [`Samples/`](../Samples/) | ResourceStartup显式None/加密、Simulation/Bundle及内置用法；ColorSelector样例整树已退役 |
| `Documentation/` | 本文档目录 |

Runtime 按 Foundation/Core/Systems/Utilities 四个职责分区组织，Editor 按对应职责及 Help/Distribution 组织；正式主体仍为 `YLFramework.Runtime` 与 `YLFramework.Editor`，不是四个独立 DLL。namespace 表达功能域，不机械复制目录；本轮源码身份升级需要调用方更新 using、完整类型名及动态身份并重编译，不提供旧 namespace 转发壳。测试/可选集成/Analyzer SDK 继续隔离，现有程序集名称保留；目录树与精确功能域见 [架构总览](Architecture.md#namespace-与迁移)。

## 模块导航

### Foundation（框架基础机制）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Singleton | 单例：普通/持久/可替换 Mono 单例、Prefab/ScriptableObject 单例属性 | 已完成 | [Modules/Singleton.md](Modules/Singleton.md) |
| Pool | 对象池：类对象池、安全池、GameObject 池、List/Dictionary 池 | 已完成 | [Modules/Pool.md](Modules/Pool.md) |
| Events | 事件系统：EasyEvent、事件组件、按枚举/字符串/类型分发 | 共享机制归 Foundation，Core EventManager 为薄入口 | [Modules/Events.md](Modules/Events.md) |
| Binding | 可监听数据：属性/列表/字典、PlayerPrefs 持久化属性 | 已完成 | [Modules/Binding.md](Modules/Binding.md) |
| IOC | 依赖注入容器：注册、解析、成员注入 | 已完成 | [Modules/IOC.md](Modules/IOC.md) |
| AssemblyTool | 按类型全名查找 `Type` 的唯一中立工具 | 已归 Foundation，供 Core / Editor 使用 | [Modules/AssemblyTool.md](Modules/AssemblyTool.md) |
| Attributes | 展示元数据、刷新配置与 DisableEdit | 元数据已归 Foundation，必要 Drawer 保持 Editor-only | [Modules/Attributes.md](Modules/Attributes.md) |
| Timing | 唯一秒/帧内核、精确句柄、Scope/复用、GO 绑定与终态等待 | 正式归 Foundation；历史专题验证范围见模块文档 | [Modules/Timing.md](Modules/Timing.md) |
| Serialization | 通用 SerializedDictionary 与 Unity 序列化合同 | 与 Save 文件策略分离 | [字典用法](Modules/Save.md#serializeddictionary-用法与边界) |
| Async / Update | 协程启动与观察、基础异步请求、共享更新订阅 | 功能域为 `YLFramework.Async` / `YLFramework.Update` | [Async 源码](../Runtime/Foundation/Async/) · [Update 源码](../Runtime/Foundation/Update/) |

### Core（业务生命周期与 UI）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Module / MVC / View | 唯一业务 owner、Model/Controller/View 生命周期与窄资源消费合同 | namespace 保持 `YLFramework.Core`，不反向依赖具体 Systems | [Core 职责](Architecture.md#core-职责归口) |
| UI | Panel/View、根/层/模态、适配/输入与 Tips；取色控件见 Utilities | 复用 Core 生命周期，历史验收见模块文档 | [Modules/UI.md](Modules/UI.md) |

### Utilities（通用工具）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Extensions / StringTools | 常用 Unity / System / 几何扩展及字节 / 进度显示 | 正式归 Runtime，共用掩码判断归 Foundation | [Modules/Extensions.md](Modules/Extensions.md) |
| FSM | 程序员直接使用的轻量纯代码状态机，不替代可视化 XFFSM | 成熟实现保留 | [Modules/FSM.md](Modules/FSM.md) |
| Grid | 通用二维数据结构（矩形 / 稀疏坐标） | 成熟实现保留 | [Modules/Grid.md](Modules/Grid.md) |
| Json | JSON 树解析与构建（JSONObject），不统一 Save / HTTP 序列化内核 | 成熟实现保留 | [Modules/Json.md](Modules/Json.md) |
| Table | 通用表与多值索引，Resource 的直接支撑 | 成熟实现保留 | [Modules/Table.md](Modules/Table.md) |
| ZipTool | Zip/GZip与SharpZipLib，BCL-only源码闭包，归Runtime主体 | 实现保留，旧独立DLL退役 | [Modules/ZipTool.md](Modules/ZipTool.md) |
| ColorSelector | 按需使用的取色控件与窗口，直接复用 Core Panel 输入与窗口生命周期 | 完整归 Utilities/UI/ColorSelector；自定义 IUIInputGate 退出 | [Modules/UI.md#colorselector](Modules/UI.md#colorselector) |
| Overlap / CharacterController2D / MoveablePlatform | 独立检测、2D 角色移动与平台组件 | Runtime 及对应 Editor 已下放 Utilities；功能域为 Overlap / Character2D / Platform2D | [Utilities 源码](../Runtime/Utilities/) |
| Excel | 字段 Attribute、转换器与 ExcelFieldsType 合同 | Runtime 合同随默认包；xlsx 后端另行选装，不进入 Player | [配置数据接入](Architecture.md#配置数据接入) |

### Systems（领域服务与规则）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Action | 动作组合、命名执行、安全句柄、屏幕过渡与内建 BCL Observable | 已归入 Runtime | [Modules/Action.md](Modules/Action.md) |
| Audio | Clip-only Music / Voice / Sound、代次安全句柄、Sound 并发上限与开始回调、双轨音乐 Crossfade、手动/自动首尾淡变、均匀/加权随机、空间播放、可选 Unity Mixer 与 Action 桥接 | 已归入 Runtime（验证以执行记录为准） | [Modules/Audio.md](Modules/Audio.md) · [Sound 并发上限](Modules/Audio.md#sound-并发与优先级) · [音乐 Crossfade](Modules/Audio.md#音乐-crossfade) · [自动首尾淡变](Modules/Audio.md#自动首尾淡变) |
| Resource | 唯一 ResourceSystem、显式启动接入、Catalog、资产/实例/Scene 租约、Core/Audio Provider、构建发布与版本事务 | 当前合同与平台边界见模块文档；新结构的 Player 验收另行确认 | [Modules/Resource.md](Modules/Resource.md) |
| Save | 唯一SaveStore的当前模型/槽位、同步异步、取消并发、缓存来源和显式备份恢复 | B已接入；仅当前v2，不迁移旧档 | [Modules/Save.md](Modules/Save.md) |
| Localization / DataAssets | 多语言文本图片与SO根/子资产作者流程、显式owner与Resource Lease | B已接入；验证范围见正式Plan | [配置数据接入](Architecture.md#配置数据接入) |
| Skill / Buff / Mission / Reddot / Logging | 配置运行态隔离、领域池/宿主关闭、普通作者工具、独立红点来源与默认被动Logger | 历史 C 候选接入与门禁见专项 Plan，DomainExcel 为选装；不替代新结构验收 | [领域玩法接入](Architecture.md#领域玩法接入) |
| Equipment | Asset/Script双创建、单一实例归属、自有/借用责任、装备配置窗口和可选xlsx | 历史技术/用户验收及 Mono、IL2CPP Low 代表性证据见专项 Plan，不代表本轮已重验 | [Equipment 接入](Architecture.md#equipment-接入) |
| StateMachine | 完整配置与可视化作者工作流 | Runtime/Editor 明确保留 Systems，不与轻量 FSM 合并 | [Runtime 源码](../Runtime/Systems/StateMachine/) · [Editor 源码](../Editor/Systems/StateMachine/) |
| HTTP | 轻量 GET / Form / JSON / PUT / DELETE、稳定终态与独占传输取消 | 正式归 Runtime，复用 Foundation 等待 / 通知 | [HTTP / AsyncOperation 边界](Architecture.md#http--asyncoperation-边界) |

### 可选集成

DOTween、Excel Editor、DomainExcel 与 HybridCLR 均位于 `Integrations/`，不随默认框架包分发，也不成为 Runtime/普通 Editor 的必需依赖。其 namespace 使用 `YLFramework.Integrations.<接入名>`；Excel/DomainExcel 后端为 Editor-only。HybridCLR 的安装与用法见 [现有 README](../Integrations/HybridCLR/README.md)，各集成程序集与边界见 [架构总览](Architecture.md#namespace-与迁移)。

### 其他内容

- [Help](Modules/Help.md)：Editor-only主体中的共享Help与各领域提供方，原菜单/窗口/Provider与配置身份保持。
- [ColorSelector](Modules/UI.md#colorselector)：四个取色类型完整归 `Runtime/Utilities/UI/ColorSelector/`，不是框架底层；正式 `YLFramework.UI.ColorSelectorWindow` 与 `Prefabs/ColorSelector.prefab` 保留，由业务 Module 显式打开，旧样例已退役。

## 架构与依赖

程序集划分、依赖方向与目录分层说明见 [Architecture.md](Architecture.md)。

FSM/Grid/Json/Table/ZipTool均归Runtime主体，按需直接调用；工具不强制创建Module/统一Manager/公共Root，Zip源码继续独立验证BCL-only。模块文档列明正式namespace与实际测试范围；成熟实现保留不等于历史完整套件均在仓库内。

Timing 的 Timer 负责到期回调；逐帧进度、插值与组合流程由 ActionKit 的 Delay / Lerp / Repeat 等动作承担。两边各自管理名字与生命周期，`TimerScope` 不与 `ActionHandle` 共用任务表。Action / Audio 位于 `Runtime/Systems/Action/`、`Runtime/Systems/Audio/`，共用 `YLFramework.Runtime`；公共入口仍为 `ActionKit` / `AudioKit`。

Audio 不配置 Mixer 时保留原功能；分类默认与单次覆盖/Direct 路由均可选，配置资产与薄 UnityEvent 组件不新增播放宿主或资源加载。Settings 音量不自动写 Mixer；路由、原生参数控制及资源所有权各自显式管理，见 [Audio](Modules/Audio.md#可选-mixer)。

## 帮助与导出

- 菜单：`YLFramework/帮助`、`YLFramework/Core/帮助`、`YLFramework/Toolkit/帮助`、`YLFramework/Action/帮助`、`YLFramework/Audio/帮助`。Core 与 Foundation/Tools 只提供必要接入指南、精选 API 和详细文档导航，不执行示例或创建宿主。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。
- `YLFramework/Toolkit/导出框架包` 的默认根为 `Editor`、`Plugins/BouncyCastle`、`Prefabs`、`Runtime`、`Samples`、`Documentation` 六个目录，以及 `LICENSE.md`、`CodingStyle.md`、`AGENTS.md` 三个根文件；使用 `Recurse`，不使用 `IncludeDependencies`。普通模块源码、资源与文档随这些根导出。
- 自动化可调用 `YLFramework.Editor.Distribution.ExportUnityPackage.Export(absolutePackagePath)`向已有本地父目录导出同一闭包，默认不覆盖、不保存其他资产。BC 精确 DLL 与 SOURCE 随 `Plugins/BouncyCastle/` 递归；系统 CNG DLL 不复制；Resource 发布快照是另一类交付，见 [Resource](Modules/Resource.md#轮换与交付)。
- `Plugins/BouncyCastle/` 完整递归且保留原导入平台；不递归整个 Plugins，更不复制或改变宿主商业插件配置。JSONObject/SharpZipLib 的现有来源声明与许可证随 Runtime 保留。
- 包含Runtime/Editor正式功能分区及唯一 `Runtime/Core/Analyzers/YLFramework.Analyzers.dll` 与meta。七项分析器默认Warning，现为Runtime主体及真实引用消费者的编译器输入，不是Player运行引用；配置与源码重建见 [Attributes](Modules/Attributes.md#core-使用诊断)。
- 默认不导出 `Tests/`、根 `Integrations/`、`Examples/`、游戏资产/Bundle、`Assets/YLFrameworkData`、SDK 工具项目、NuGet 缓存、临时探针、中间产物及包外 DOTween。可选扩展源码须另行满足依赖；Analyzer 重建源码保留在仓库 `Tools/YLFramework.Analyzers`，不随 UnityPackage 提供 SDK 项目。
- 框架仍沿用 UGUI、Newtonsoft 等既有依赖，不是零依赖安装包。Runtime/Editor 主体不依赖 XFAB，原游戏保持原链。EventListener 使用原生 Inspector，Sirenix Odin 插件及其编译引用已退出；ProjectSettings 的五个未使用 Odin 宏按配置权限边界保留。XML 位于 `Library/`，不默认导出；缺 XML 的 Help 仍保留真实声明。
- 导出回归应真实解包并检查递归资产内容、GUID / meta、批准的 BouncyCastle 子树、Analyzer 唯一身份与禁用平台及排除项，不等于干净工程导入。历史 Player 门禁与阶段状态单独记录于 [阶段 6 方案](../../../.kilo/plans/xf-yl-6-editor.md#65-帮助导出与阶段收口)，不能替代当前结构的新交付验收。

## 测试

- EditMode：`Tests/EditMode/{Foundation,Core,Systems/<模块>,Utilities}/`；Editor 行为位于 `Tests/EditMode/Editor/<功能>/`，如 Help、Resource 和 Core。
- PlayMode：`Tests/PlayMode/{Foundation,Core,Systems/<模块>,Utilities}/`，覆盖单例、Timing、Action、Audio 等运行行为。普通单模块测试随职责归位，混合验收保留跨模块范围。
- `Tests/Acceptance/Core/` 与 `Tests/Fixtures/Core/` 分别保留显式验收入口及夹具；Tests 整体不默认分发。目录重排不更名测试程序集，也不以源码存在冒充测试发现或通过。
- DOTween：`Tests/Integrations/DOTween/` 下两个独立测试程序集，受 `DOTWEEN` + `UNITY_INCLUDE_TESTS` 约束。
- HybridCLR：`Tests/Integrations/HybridCLR/` 的独立 EditMode/PlayMode 叶子，精确约束 9.0.3；Editor Simulation、替身与真实 IL2CPP 证据分别记录，不把单元测试冒充原生热更。

HybridCLR 源码仓库首次检出或相关程序集重新编译后，先在非 Play 的 Editor 主线程调用 `YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Prepare()`，再运行 loader/Module PlayMode 测试。该步骤从当前已编译程序集生成 `PlayMode/Resources/YLHybridCLRTests` 下的 `.bytes` 和 manifest JSON；这些本机输入及其 meta 不纳入 Git，不能复用其他机器或旧程序集的快照。

导出的框架包不包含 `Tests/`，测试仅在源码仓库内运行。

运行方式：Unity Test Runner，或使用 `-runTests` 批处理参数。

## 独立工程安装

使用Unity `2022.3.62f3c1`新建工程，先通过Package Manager安装以下必需依赖，再通过Assets > Import Package > Custom Package导入最终`.unitypackage`。不要复制主工程manifest、Library或补入其DLL：

| 依赖 | 安装来源与版本 | 用途 |
| --- | --- | --- |
| UGUI | Unity Package Manager，`com.unity.ugui` `1.0.0`；本版本Editor实际解析为builtin | `UnityEngine.UI` |
| Json.NET | Unity Registry，`com.unity.nuget.newtonsoft-json` `3.2.1` | Runtime/Editor直接依赖 |
| BouncyCastle | 框架包内`Plugins/BouncyCastle`，2.7.0官方netstandard2.0制品 | HKDF-SHA256 |
| Unity内建模块 | 同版本Editor正常提供/解析的UI、Audio、AssetBundle、UnityWebRequest等模块 | 不复制UnityEngine或Windows系统DLL |

Input System为可选输入后端。最小工程可使用Legacy Input；使用新输入时另安装Unity Registry的`com.unity.inputsystem` `1.14.2`，设置Active Input Handling并真实重启Editor。不能以包版本声明代替实际加载版本。框架不要求URP；独立最小用法使用Built-in Render Pipeline，现有URP项目按自身管线维护材质。

不需要原XF/XFAB/XFFSM、MCP、DOTween或商业Editor插件。YL使用Unity原生日志入口，不把可选Logger当作产品依赖。主工程`com.xfkj.xfgameframework.loggersystem` 1.0.1的修复保留原包身份，另外以独立tarball交付于`G:/Unity/Y9/Optional/Final/com.xfkj.xfgameframework.loggersystem-1.0.1.tgz`，不夹在框架`.unitypackage`中，也不代表其他XF扩展已迁移。

需要该可选文件Logger时，在框架自身导入/编译成功后，通过Package Manager > Add package from tarball选择该制品；不安装原XF总包、XF依赖管理器或MCP。版本仍为1.0.1，补丁身份以`Optional/logger-final-receipt.json`的SHA256和27文件清单区分，不以AssemblyVersion或旧远端Git包冒充修复版。原meta/GUID和Analyzer随tarball保留。补程序集级`AlwaysLinkAssembly`让独立IL2CPP消费者处理既有自动初始化入口，不添加全量`link.xml`，受测范围为Windows64 Mono与IL2CPP Low，不外推High。

真实首次导入编译成功后，可按[Resource最小接入](Modules/Resource.md#可运行最小接入)生成本工程的新样例；UI轻量默认根与完整模板见[UI用法](Modules/UI.md)。随包不含源码仓库的Acceptance场景或测试程序集。

### 独立交付验收入口（阶段 9 历史）

以下目录、命令和结果描述阶段 9 的冻结交付，不是当前 namespace 迁移的新包或新 Player 证据。新结构须绑定本次源码/包 hash，在新的自有输出与干净接收工程重新验收；不覆盖这些旧制品，也不将旧 `Finalize` 或恢复旧运行作为本轮通过记录。

源码入口的 `AuditPackage` 已按当前六目录加三根文件解析并校验导出根；这一局部校验不代表新包已完成首导入、构建或 Player 运行。历史 `Finalize` 仍不能用于验收本轮新结构，不能将整套交付脚本描述为当前绿色。

稳定交付根为`G:/Unity/Y9`，不是Temp。正式源码入口为`Assets/YLFramework/Tests/Tools/Invoke-Stage9Delivery.ps1`；交付根`Tools/`提供同一入口及独立消费者模板的冻结副本。它们属于源码/交付验收设施，**不在产品包内**，只能在第一次产品编译证明之后安装，不提供产品缺失的程序集或资源。

以下命令用PowerShell 7.6执行；`ReceiverD`必须尚不存在，脚本拒绝覆盖工程、已有日志、Player输出或运行目录。`owner.json`明确界定该交付根与源码仓库归属，接收工程由真正的Unity `-createProject`建立，不复制来源工程：

```powershell
$entry = 'G:/Unity/Y9/Tools/Invoke-Stage9Delivery.ps1'
$delivery = @{ DeliveryRoot = 'G:/Unity/Y9'; Receiver = 'ReceiverD'; Package = 'YLFramework-Stage9-Final.unitypackage' }
& $entry @delivery -Operation Create
& $entry @delivery -Operation Import
# 上一步真实Unity首次编译成功后，才可安装公开API消费者并生成本工程的两版资源。
& $entry @delivery -Operation InstallHarness
& $entry @delivery -Operation Editor -Method Audit -Run independent
& $entry @delivery -Operation Editor -Method Generate -Run independent
& $entry @delivery -Operation Editor -Method BuildMono -Run independent
& $entry @delivery -Operation Player -Backend Mono -BuildRun independent -Run independent -Command smoke -Store independent-first
```

已有运行只用`RecoverPlayer`配原`Backend/BuildRun/Run/Command/Store/ExpectedVersion`重收，不换Run伪装同一次结果。`RecoverImport`同样锁定原包SHA/参数/PID与已有退出记录；不重新导入、补DLL或重复启动。若Unity批处理已生成证据却在native退出阶段停滞，先读取其`.process.json`核对专有PID/projectPath、保留日志与完成报告，只回收或停止该自有进程，不操作主Editor，也不把它记为Editor正常退出。Player仍须独立进程exit0及退出后完整日志核验才通过。

本阶段完整结果、失败与复验关系见`IntegrationPlan.md`§9和交付根`delivery-binding.json`。产品最终包由原Exporter导出并逐载荷/meta/GUID核对；ReceiverB提供两种Player和新资源证据，另一个空ReceiverC用于最终包首次导入复验。物理输入历史矩阵不重跑；本轮模态证据为实际Canvas raycast与公开UGUI指针处理器，不称OS物理输入验证。Audio已验证资源、播放与释放，未以输出采样为0声称可闻音频；原Scene风险、CA正向联网未测、非零原生泄漏遥测边界保持。
