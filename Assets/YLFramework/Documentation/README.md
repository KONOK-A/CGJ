# YLFramework 文档

YLFramework 是当前工程内自研的 Unity 游戏框架，面向游戏业务层提供对象池、单例、事件、数据绑定、状态机、IOC、动作编排、音频、资源管理、存档、UI 等能力。

> 当前框架仍在持续开发和完善中，部分模块缺少测试或存在明确的待完善项，详见各模块文档的“当前状态”。
> 本文档是导航性质的内部文档，不是完整 API Reference。

## 目录结构（当前）

框架根目录为 `Assets/YLFramework/`：

| 目录 | 说明 |
|---|---|
| [`Runtime/`](../Runtime/) | 运行时框架代码（Foundation / Core / Systems / Utilities） |
| [`Editor/`](../Editor/) | Editor-only主体；功能工具、模块帮助提供方与共享Help分区 |
| [`Integrations/DOTween/`](../Integrations/DOTween/) | 可选 Action DOTween 扩展，不在默认导出范围 |
| [`Tests/`](../Tests/) | EditMode / PlayMode 测试 |
| [`Plugins/`](../Plugins/) | 批准的官方BC 2.7.0 HKDF依赖及SOURCE来源；宿主商业Editor Enhancers不随框架导出，Sirenix Odin已整树退出 |
| [`Prefabs/`](../Prefabs/) | 自有 UI 模板与正式工具（`YLFramework.prefab`、`UITips.prefab`、`ColorSelector.prefab`） |
| [`Samples/`](../Samples/) | ResourceStartup显式None/加密、Simulation/Bundle及内置用法；ColorSelector样例整树已退役 |
| `Documentation/` | 本文档目录 |

Runtime按Foundation/Core/Systems/Utilities组织，Editor按同类功能及Distribution/Help组织；正式主体仅YLFramework.Runtime与YLFramework.Editor。五个额外产品asmdef退役，测试/可选集成/Analyzer SDK继续隔离，旧二进制消费者须重编译。目录分类不新增系统管理器或namespace迁移，Tools仍为仓库SDK/开发工具。

## 模块导航

### Foundation（框架基础机制）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Singleton | 单例：普通/持久/可替换 Mono 单例、Prefab/ScriptableObject 单例属性 | 已完成 | [Modules/Singleton.md](Modules/Singleton.md) |
| Pool | 对象池：类对象池、安全池、GameObject 池、List/Dictionary 池 | 已完成 | [Modules/Pool.md](Modules/Pool.md) |
| Events | 事件系统：EasyEvent、事件组件、按枚举/字符串/类型分发 | 基本完成（EventManager 为空占位） | [Modules/Events.md](Modules/Events.md) |
| Binding | 可监听数据：属性/列表/字典、PlayerPrefs 持久化属性 | 已完成 | [Modules/Binding.md](Modules/Binding.md) |
| IOC | 依赖注入容器：注册、解析、成员注入 | 已完成 | [Modules/IOC.md](Modules/IOC.md) |
| AssemblyTool | 按类型全名查找 `Type` 的唯一中立工具 | 已归 Foundation，供 Core / Editor 使用 | [Modules/AssemblyTool.md](Modules/AssemblyTool.md) |
| Attributes | 展示元数据、刷新配置与 DisableEdit | 元数据已归 Foundation，必要 Drawer 保持 Editor-only | [Modules/Attributes.md](Modules/Attributes.md) |

### Utilities（通用工具）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Extensions / StringTools | 常用 Unity / System / 几何扩展及字节 / 进度显示 | 正式归 Runtime，共用掩码判断归 Foundation | [Modules/Extensions.md](Modules/Extensions.md) |
| FSM | 程序员直接使用的轻量纯代码状态机，不替代可视化 XFFSM | 成熟实现保留 | [Modules/FSM.md](Modules/FSM.md) |
| Grid | 通用二维数据结构（矩形 / 稀疏坐标） | 成熟实现保留 | [Modules/Grid.md](Modules/Grid.md) |
| Json | JSON 树解析与构建（JSONObject），不统一 Save / HTTP 序列化内核 | 成熟实现保留 | [Modules/Json.md](Modules/Json.md) |
| Table | 通用表与多值索引，Resource 的直接支撑 | 成熟实现保留 | [Modules/Table.md](Modules/Table.md) |
| Timing | Foundation 唯一秒/帧内核、精确句柄、Scope/复用、GO 绑定与终态等待 | 2.4-C 实施及专项验证完成，待验收与 Git 收口；2.4 IN PROGRESS | [Modules/Timing.md](Modules/Timing.md) |
| ZipTool | Zip/GZip与SharpZipLib，BCL-only源码闭包，归Runtime主体 | 实现保留，旧独立DLL退役 | [Modules/ZipTool.md](Modules/ZipTool.md) |
| ColorSelector | 按需使用的取色控件与窗口，直接复用 Core Panel 输入与窗口生命周期 | 完整归 Utilities/UI/ColorSelector；自定义 IUIInputGate 退出 | [Modules/UI.md#colorselector](Modules/UI.md#colorselector) |

### Runtime 大型模块

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Action | 动作组合、命名执行、安全句柄、屏幕过渡与内建 BCL Observable | 已归入 Runtime | [Modules/Action.md](Modules/Action.md) |
| Audio | Clip-only Music / Voice / Sound、代次安全句柄、Sound 并发上限与开始回调、双轨音乐 Crossfade、手动/自动首尾淡变、均匀/加权随机、空间播放、可选 Unity Mixer 与 Action 桥接 | 已归入 Runtime（验证以执行记录为准） | [Modules/Audio.md](Modules/Audio.md) · [Sound 并发上限](Modules/Audio.md#sound-并发上限) · [音乐 Crossfade](Modules/Audio.md#音乐-crossfade) · [自动首尾淡变](Modules/Audio.md#自动首尾淡变) |
| Resource | 唯一 ResourceSystem、显式启动接入、Catalog、资产/实例/Scene 租约、Core/Audio Provider、构建发布与版本事务 | 正式加载/更新及双模式最小示例已接入；远端当前限Windows Editor，阶段8 Player最终验收另行实施 | [Modules/Resource.md](Modules/Resource.md) |
| Save | 唯一SaveStore的当前模型/槽位、同步异步、取消并发、缓存来源和显式备份恢复 | B已接入；仅当前v2，不迁移旧档 | [Modules/Save.md](Modules/Save.md) |
| Localization / DataAssets | 多语言文本图片与SO根/子资产作者流程、显式owner与Resource Lease | B已接入；验证范围见正式Plan | [配置数据接入](Architecture.md#配置数据接入) |
| Excel（可选） | MiniExcel Editor-only，真实xlsx SO/List/JSON预览确认、失败回滚与工作簿定位 | 不进入核心默认包或Player | [配置数据接入](Architecture.md#配置数据接入) |
| Skill / Buff / Mission / Reddot / Logging | 配置运行态隔离、领域池/宿主关闭、普通作者工具、独立红点来源与默认被动Logger | C完整候选已接入；真实门禁以专项Plan记录为准，DomainExcel为选装 | [领域玩法接入](Architecture.md#领域玩法接入) |
| Equipment | Asset/Script双创建、单一实例归属、自有/借用责任、装备配置窗口和可选xlsx | 技术及用户验收通过；主/独立工程Mono与IL2CPP Low代表性运行已验证，Git交接见专项Plan | [Equipment 接入](Architecture.md#equipment-接入) |
| HTTP | 轻量 GET / Form / JSON / PUT / DELETE、稳定终态与独占传输取消 | 正式归 Runtime，复用 Foundation 等待 / 通知 | [HTTP / AsyncOperation 边界](Architecture.md#http--asyncoperation-边界) |
| UI | Core Panel/View、窗口目录、根/层/模态、适配/输入、Tips；取色便利工具见 Utilities | 阶段 3 技术门禁完成，待用户最终验收与 Git 收口 | [Modules/UI.md](Modules/UI.md) |

### 其他内容

- [Help](Modules/Help.md)：Editor-only主体中的共享Help与各领域提供方，原菜单/窗口/Provider与配置身份保持。
- [ColorSelector](Modules/UI.md#colorselector)：四个取色类型完整归 `Runtime/Utilities/UI/ColorSelector/`，不是框架底层；正式 `YLFramework.Core.ColorSelectorWindow` 与 `Prefabs/ColorSelector.prefab` 保留，由业务 Module 显式打开，旧样例已退役。

## 架构与依赖

程序集划分、依赖方向与目录分层说明见 [Architecture.md](Architecture.md)。

FSM/Grid/Json/Table/ZipTool均归Runtime主体，按需直接调用；工具不强制创建Module/统一Manager/公共Root，Zip源码继续独立验证BCL-only。模块文档列明正式namespace与实际测试范围；成熟实现保留不等于历史完整套件均在仓库内。

Timing 的 Timer 负责到期回调；逐帧进度、插值与组合流程由 ActionKit 的 Delay / Lerp / Repeat 等动作承担。两边各自管理名字与生命周期，`TimerScope` 不与 `ActionHandle` 共用任务表。Action / Audio 位于 `Runtime/Systems/Action/`、`Runtime/Systems/Audio/`，共用 `YLFramework.Runtime`；公共入口仍为 `ActionKit` / `AudioKit`。

Audio 不配置 Mixer 时保留原功能；分类默认与单次覆盖/Direct 路由均可选，配置资产与薄 UnityEvent 组件不新增播放宿主或资源加载。Settings 音量不自动写 Mixer；路由、原生参数控制及资源所有权各自显式管理，见 [Audio](Modules/Audio.md#可选-mixer)。

## 帮助与导出

- 菜单：`YLFramework/帮助`、`YLFramework/Core/帮助`、`YLFramework/Toolkit/帮助`、`YLFramework/Action/帮助`、`YLFramework/Audio/帮助`。Core 与 Foundation/Tools 只提供必要接入指南、精选 API 和详细文档导航，不执行示例或创建宿主。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。
- `YLFramework/Toolkit/导出框架包` 的默认 ExportRoots 为 `Editor`、`Plugins/BouncyCastle`、`Prefabs`、`Runtime`、`Samples`、`Documentation` 六个目录及框架根级文件；使用 `Recurse`，不使用 `IncludeDependencies`。普通模块源码、资源与文档随这些根目录导出。
- 自动化可调用 `YLFramework.Editor.ExportUnityPackage.Export(absolutePackagePath)`向已有本地父目录导出同一闭包，默认不覆盖、不保存其他资产。BC 精确 DLL 与 SOURCE 随 Plugins 递归；系统 CNG DLL 不复制；Resource 发布快照是另一类交付，见 [Resource](Modules/Resource.md#轮换与交付)。
- `Plugins/BouncyCastle/` 完整递归且保留原导入平台；没有再分发授权的 `Plugins/Editor Enhancers Bundle/` 不导出、不删除，也不改变主工程插件配置。
- 包含Runtime/Editor正式功能分区及唯一 `Runtime/Core/Analyzers/YLFramework.Analyzers.dll` 与meta。七项分析器默认Warning，现为Runtime主体及真实引用消费者的编译器输入，不是Player运行引用；配置与源码重建见 [Attributes](Modules/Attributes.md#core-使用诊断)。
- 默认不导出 `Tests/`、根 `Integrations/`、`Examples/`、游戏资产/Bundle、`Assets/YLFrameworkData`、SDK 工具项目、NuGet 缓存、临时探针、中间产物及包外 DOTween。可选扩展源码须另行满足依赖；Analyzer 重建源码保留在仓库 `Tools/YLFramework.Analyzers`，不随 UnityPackage 提供 SDK 项目。
- 框架仍沿用 UGUI、Newtonsoft 等既有依赖，不是零依赖安装包。Runtime/Editor 主体不依赖 XFAB；可选 XFAB Audio 适配独立、显式装配且不默认导出，原游戏保持原链。EventListener 使用原生 Inspector，Sirenix Odin 插件及其编译引用已退出；ProjectSettings 的五个未使用 Odin 宏按配置权限边界保留。XML 位于 `Library/`，不默认导出；缺 XML 的 Help 仍保留真实声明。
- 导出回归真实解包并检查递归资产内容、GUID / meta、完整 Plugins、Analyzer 唯一身份与禁用平台及排除项，不等于干净工程导入。实际 Player 门禁与阶段状态单独记录于 [阶段 6 方案](../../../.kilo/plans/xf-yl-6-editor.md#65-帮助导出与阶段收口)。

## 测试

- EditMode：`Tests/EditMode/`，覆盖 Foundation / Tools / Resource，以及 `Action/`、`Audio/`、`Help/` 的行为、API、XML 与帮助边界。
- PlayMode：`Tests/PlayMode/`，覆盖 MonoBehaviour 单例、Timing 时钟与宿主，以及 `Action/`、`Audio/` 的真实帧执行和播放生命周期。
- DOTween：`Tests/Integrations/DOTween/` 下两个独立测试程序集，受 `DOTWEEN` + `UNITY_INCLUDE_TESTS` 约束。

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

### 独立交付验收入口

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
