# 架构总览

> [文档首页](README.md)

## 目录与程序集

阶段 7 §16 的历史修订已完成技术门禁：主体为YLFramework.Runtime/YLFramework.Editor两个，五个额外产品asmdef/meta及ColorSelector样例已退出。用户授权后Canvas恢复原基线，正式Panel混合门禁优先级和Help分组回归先红后绿；最终实际输入、六根导出、新Windows64/Mono MVC/UI/持久UnityEvent/退出Player及独立审查已收口。已随阶段 7 Git 收口提交（`milestone/7-structure`），不宣称阶段8、独立无依赖或所有平台验收完成；实际证据与迁移限制见 [结构方案§17.12](../../../.kilo/plans/xf-yl-7-structure-redesign.md#1712-授权修复与最终验收2026-10-14)。这些历史结果不代表下述当前目录与 namespace 升级已完成新一轮 Unity、测试或 Player 验收。

| 目录 | 程序集（asmdef） | 职责 |
|---|---|---|
| `Runtime/` | `YLFramework.Runtime` | 运行时主体，含 Foundation / Core / Systems / Utilities；依赖 UGUI / Json.NET，不依赖 XFAB |
| `Runtime/Foundation/` | `YLFramework.Runtime` | 中立机制与Unity适配按功能统一分组；Kernel空容器/meta已退出，无第二套内核或产品DLL |
| `Runtime/Core/` | `YLFramework.Runtime` | Module/MVC/View/Panel/UI 生命周期与窄消费合同；partial 仅导航，不改变唯一生命周期与事务 |
| `Runtime/Systems/` | `YLFramework.Runtime` | 领域服务、玩法规则及完整配置/可视化 StateMachine |
| `Runtime/Utilities/` | `YLFramework.Runtime` | 按需组合的工具、数据结构、检测/移动组件及 Excel 字段合同；Zip 源码保持 BCL-only 边界 |
| `Editor/` | `YLFramework.Editor` | 全部自有编辑器主体，显式引用 Runtime / UGUI / Json.NET，仅 Editor 平台 |
| `Editor/Help/` | `YLFramework.Editor` | 共享 Help 功能分区，保留稳定窗口、菜单/Provider id与持久配置身份，无独立产品 DLL |
| `Editor/Core/` | `YLFramework.Editor` | Core 默认资源、模块生成、UI 迁移与 Core Help；通用资产/设置/预览支撑归 Utilities |
| `Editor/Systems/Shared/` | `YLFramework.Editor` | 跨玩法生命周期及 Resource 图标作者支持，不塞入 Core 或中立 Utilities |
| `Integrations/DOTween/Runtime/` | `YLFramework.Actions.DOTween` | 可选 Action 扩展，受 `DOTWEEN` 约束 |
| `Integrations/XFAB/Audio/` | `YLFramework.Audio.XFAB` | 可选 Audio 借源适配，主体不反向依赖 |
| `Integrations/Excel/Editor/`、`Integrations/DomainExcel/Editor/` | `YLFramework.Excel.Editor`、`YLFramework.Domain.Excel.Editor` | 可选 xlsx 后端与领域作者接入，仅 Editor |
| `Integrations/HybridCLR/Runtime/`、`Editor/` | `YLFramework.HybridCLR`、`YLFramework.HybridCLR.Editor` | 可选热更叶子，精确约束官方 HybridCLR 9.0.3；dnlib 只在 Editor，主体无反向依赖 |
| `Tests/EditMode/` 根 asmdef | `YLFramework.Tests.EditMode` | 编辑器内单元测试；嵌套程序集与 asmref 按测试节保留实际归属 |
| `Tests/PlayMode/` 根 asmdef | `YLFramework.Tests.PlayMode` | 运行时测试；子目录不机械改成统一程序集 |
| `Tests/Integrations/DOTween/EditMode/`、`PlayMode/` | `YLFramework.Actions.DOTween.Tests.EditMode`、`.PlayMode` | 独立可选集成测试 |

Runtime / Editor 主体的 `autoReferenced` 为 true；可选 DOTween/XFAB 集成与测试程序集保留各自关闭自动引用和约束。使用 asmdef 的业务仍须显式声明所需引用；五份已退役产品的二进制/AQN消费者须更新并重编译，不提供旧身份兼容壳。

当前目录概览如下；同一行并列项是分类摘要，不表示额外程序集或字面目录：

```text
Assets/YLFramework/
  Runtime/
    Foundation/  Async, Attributes, Binding, Events, IOC, Pool,
                 Serialization, Singleton, Timing, Update, Utilities
    Core/        Modules, MVC, UI, Events, ResourceAccess, Attributes, Analyzers, Resources
    Systems/     Action, Audio, Buff, DataAssets, Equipment, Http, Localization,
                 Logging, Mission, Reddot, Resource, Save, Skill, StateMachine
    Utilities/   CharacterController2D, Excel, Extensions, FSM, Grid, Json,
                 MoveablePlatform, Overlap, Table, UI/ColorSelector, ZipTool, StringTools.cs
    YLFrameworkRoot.cs, YLFramework.Runtime.asmdef, csc.rsp
  Editor/
    Foundation/, Core/, Systems/ (含 Shared/), Utilities/, Help/, Distribution/
    YLFramework.Editor.asmdef
  Integrations/  DOTween, XFAB, Excel, DomainExcel, HybridCLR
  Tests/
    EditMode/   Foundation, Core, Systems/<模块>, Utilities, Editor/<功能>
    PlayMode/   Foundation, Core, Systems/<模块>, Utilities
    Integrations/<接入名>/, Acceptance/Core/, Fixtures/, Tools/
  Plugins/BouncyCastle/, Prefabs/, Samples/, Documentation/
  LICENSE.md, CodingStyle.md, AGENTS.md
```

Framework 是整体交付、按需使用的统一框架；Foundation/Core/Systems/Utilities 是源码职责分区，不是四个可独立安装的 DLL。Tests、Samples 及可选集成仍保留自身必要的编译边界，本轮不改任何现有程序集名称。

### Namespace 与迁移

namespace 表达稳定功能域，不机械拼接 Runtime/Foundation/Systems/Utilities 或内部 Config/Requests/Components 目录。下表省略共同前缀 `YLFramework.`：

| 源码归属 | 当前 namespace |
|---|---|
| Foundation/Async、Attributes、Events、Binding | `Async`、`Attributes`、`Events`、`Binding`；物理/动画 Listener 同归 `Events` |
| Foundation/IOC、Pool、Singleton | `DependencyInjection`、`Pooling`、`Singletons` |
| Foundation/Serialization、Timing、Update | `Serialization`、`Timing`、`Update`；TimerObject 同归 `Timing` |
| Foundation/Utilities | `Foundation`，如 ApplicationTool、AssemblyTool、LayerMaskUtility |
| Core | `Core`；分析器 marker 保持 `Core.RoslynAnalyzers` |
| Systems/Audio、Resource、Save、Http | `Audio`、`Resource`、`Save`、`Http`，不再包含 `Services` |
| Systems/Skill、Buff、Mission、Equipment、Reddot | `Skill`、`Buff`、`Mission`、`Equipment`、`RedDots`，不再使用旧 `*System` namespace |
| Systems/Action、Localization、DataAssets、Logging、StateMachine | `Actions`、`Localization`、`DataAssets`、`Logging`、`StateMachine` |
| Utilities/Overlap、CharacterController2D、MoveablePlatform、Excel | `Overlap`、`Character2D`、`Platform2D`、`Excel` |
| Utilities/Extensions、Grid、Table、FSM、UI/ColorSelector | `Extensions`、`Grids`、`Tables`、`FSM`、`UI` |
| Utilities/ZipTool 自有包装、StringTools.cs | `Compression`、`Utilities` |
| Utilities/Json 与 ZipTool 内 SharpZipLib 子树 | 保留现有 `Utilities.Json` / `Utilities.ZipTool.*` 身份及第三方源码，不按自有包装规则重命名 |
| 根装配宿主 YLFrameworkRoot | `YLFramework`，不属于 Foundation |

Editor 同样按所有者划分为 `YLFramework.Editor.Core`、`.Utilities`、`.Foundation`、`.<功能域>`、`.Domain`、`.Help`、`.Distribution`。通用资产/设置/序列化/Scene/TreeViews/Windows/Menus 属于 `.Utilities`；独立组件即使目录下放 Utilities，其 Editor 仍使用 `.Overlap`、`.Character2D`、`.Platform2D`，不是通用 `.Utilities`。共享领域支持使用 `.Domain`，Core 仅保留自身生命周期与 UI 作者工具；Help 的既有 API 子域不因目录整理强制合并。

| 可选集成 | 当前 namespace | 保留的程序集名 |
|---|---|---|
| DOTween | `YLFramework.Integrations.DOTween` | `YLFramework.Actions.DOTween` |
| XFAB Audio | `YLFramework.Integrations.XFAB.Audio` | `YLFramework.Audio.XFAB` |
| Excel Editor | `YLFramework.Integrations.Excel.Editor` | `YLFramework.Excel.Editor` |
| DomainExcel Editor | `YLFramework.Integrations.DomainExcel.Editor` | `YLFramework.Domain.Excel.Editor` |
| HybridCLR Runtime / Editor | `YLFramework.Integrations.HybridCLR` / `.Editor` | `YLFramework.HybridCLR` / `YLFramework.HybridCLR.Editor` |

本轮允许源码级身份升级，不承诺旧 namespace 源码兼容、转发壳或旧 DLL 兼容。调用方按真实类型所有者更新 using、完整名、反射/生成绑定并重编译；`rootNamespace` 只是新脚本默认值，不决定已声明类型的 CLR 身份。公开入口、数据合同和真实扩展点以源码可见性为准，内部缓存/调度等实现不因同一 Runtime 可见就成为业务扩展合同。

Module/View/Panel 业务扩展、Audio Provider、Action 扩展、Resource 显式接入及 Excel/领域配置合同继续作为正式外部入口；角色检测 helper、StateMachine Editor 图层/工厂/样式等实现，以及 LocalizationAuthoring、AudioClipLibraryMigration 已收为 internal，不应由业务直接依赖。通用 Editor 工具、必要作者接口与已有序列化 TreeView 状态仍按实际 public 合同使用；不为本轮封装新增友元、接口或程序集，也不将 internal 当作 Runtime 内的目录隔离。

脚本与存续资产移动须携带 `.meta`、保留 GUID；资源逻辑键、序列化字段、存档键、Excel 列名及持久协议不随目录或 namespace 改写。UnityEvent、领域 Script 绑定、Editor 设置路径和 full name/AQN 字符串是各自独立的迁移输入，须显式映射并验证。`MovedFrom` 仅服务适用的 Unity 序列化迁移，不是 `Type.GetType` 的旧 AQN 别名，也不能证明任意反射、JSON 或预编译客户端兼容。历史 Plan、冻结验收制品和旧 HybridCLR 基座不重写，受影响的新身份需另做真实验收。

## 依赖方向

- `YLFramework.Runtime` → UnityEngine.UI、Newtonsoft.Json.dll；主体不 self-reference 已合入源码，预编译插件使用必要白名单
- `YLFramework.Editor` → `YLFramework.Runtime`、UnityEngine.UI、Newtonsoft.Json.dll；Help与提供方保持源码职责分离，不再是不同产品DLL
- `YLFramework.Audio.XFAB` → `YLFramework.Runtime`、XFABManager；独立可选叶子，受包版本和 `YL_AUDIO_XFAB` 约束、`autoReferenced=false`，不反向成为主体必需依赖
- `YLFramework.Actions.DOTween` → `YLFramework.Runtime` + `DOTween.dll`
- `YLFramework.HybridCLR` → `YLFramework.Runtime` + 官方 `HybridCLR.Runtime`；对应 Editor 另引用 YL Editor、官方 Editor 和 dnlib，不引入 XF/XFAB 或第二套下载/资源权威
- `YLFramework.Tests.EditMode` → Runtime、Editor、Foundation 测试辅助、UI、TestRunner / NUnit
- `YLFramework.Tests.PlayMode` → Runtime、UI、TestRunner / NUnit

模块内协作：`Runtime/Systems/Audio/Actions/PlaySoundAction` → Action 契约 + Audio 播放入口，全部位于同一 Runtime，不是可拆卸的 optional Integration。BCL Observable 同样内建；DOTween 与可选 `Integrations/XFAB/Audio/` 各保留独立第三方程序集边界。正式 Audio Runtime 不反向引用 XFAB adapter；后者不自动注册或纳入默认导出。Runtime / 普通 Editor 不静态引用 DOTween wrapper 或供应商 DLL。

Resource/Audio 的内建装配集中在 `Runtime/Systems/Resource/Integration/Audio/`：`ResourceSystem.Audio.cs` 属于 `YLFramework.Resource`，`ResourceAudioClipProvider.cs` 属于 `YLFramework.Audio`。这是同一 Runtime 内明确登记的 Resource→Audio 协作，不是可选程序集；`GetAudioClipProvider`、同 System/Project 的 Provider 引用身份、会话结束失效及启动失败/回滚合同不因拆文件改变。

无循环依赖；Runtime 不引用 Editor 程序集。Runtime 中少量编辑器模拟代码（资源加载、AssetBundle 模拟、存档编辑器保护等）使用 `#if UNITY_EDITOR` 条件编译，玩家程序集不依赖 UnityEditor。

### HybridCLR 可选接入

`Integrations/HybridCLR` 不属于默认框架导出闭包，使用明确的 `HybridCLRIntegrationExporter` 另行交付，默认也不带其 Samples。样例留在可选根内，避免默认递归主体 Samples 时混入；Host 不静态引用热更类型。默认两主体及现有游戏不自动接入，旧 XF HybridCLR 扩展已退出，新接入不保留旧 API 或 AssetBundleName/下载链。

程序集登记以官方 HybridCLRSettings 为准；YL BuildProfile 引用 Resource Recipe，保存 Development、两个显式输出目录、项目入口及可选的一跳 BaseProfile 绑定。原生编译/生成/冻结覆盖全局并集，项目 manifest 只投影所选入口的热更依赖闭包；已有配置的空入口保留全量语义。不同 ResourceProject 通过明确的拥有者 GUID、目录和兼容身份复用同一基座，全局热更、metadata、native 登记仍是不可越过的能力上限。schema1 的 InitialManifest 保留全局登记，不冒充某个项目实际发布内容。

基座流程受检编译、官方生成/剥离、完整 metadata、正式 Resource 发布/部署和真实 Player 分阶段报告；最终全局 AOT 与登记输入一致且成功 Player 返回后才写持久完成基线。补丁只读此基线，不能借再次剥离或改基座让检查通过。运行时借用唯一 ResourceSystem，全部输入检查后加载 metadata/程序集；manifest lease 阻止活动代码闭包 Activate。进程级不同内容或部分不可逆失败需要重启，Dispose 不卸载程序集，也不关闭外部资源系统。

Module 请求在精确 entry assembly 中解析类型并组合既有 Core 生命周期，分段透传代码读取与 Core 启动的真实单调进度，不增加 Module/Core/Resource/Root 公共 API。先关闭本入口 Module，再 Dispose loader；即使借用服务先关闭，也不丢弃活动 Module 所有权。Native、验证发布产物的 EditorSimulation、显式跳过代码资源的 EditorFast 分开标识；快路径不写 native 身份、没有 CodePackageId，不是失败回退或原生证明。窗口提供显式创建/保存、项目选择、幂等资源关联、配置与原生环境分开预检及阶段诊断。边界、安装与用法见可选包内 `Integrations/HybridCLR/README.md`；当前原生与接收门禁状态以 [HybridCLR 专项 Plan](../../../.kilo/plans/1792591409867-xf-hybridclr-migration.md) 为准，不沿用旧 Mono 或旧 HybridCLR 结果。

外部依赖：UGUI、Newtonsoft.Json（存档、HTTP、Catalog 与 Core 配表等）等既有依赖；排除 DOTween 不意味着框架零外部依赖。EventListener 的折叠、页签与 UnityEvent 编辑由普通 Editor 中的原生 CustomEditor 承担，Sirenix Odin 插件整树及其编译引用已退出，不提供 mini-Odin 或兼容空壳。ProjectSettings 的五个未使用 Odin 宏按原配置权限边界保留。

E04内容编码在同一Resource主体内接入官方 `BouncyCastle.Cryptography 2.7.0` netstandard2.0，仅用于HKDF-SHA256；Runtime asmdef精确引用该DLL并允许已批准的CNG桥接unsafe代码。AES-256-GCM来自Windows CNG `Microsoft Primitive Provider`，不复制系统DLL、不提供软件GCM或临时明文fallback。64KiB/两明文槽与32KiB native读取缓冲、显式数据预算及owner沿原Bundle/事务图；应用拥有key provider，不新增key服务/加载权威。BC 精确 SHA 与来源随 `Plugins/BouncyCastle/` 交付，平台及风险见 [Resource](Modules/Resource.md#可选-bundle-内容加密)。

## Runtime 目录规则（Foundation / Core / Systems / Utilities）

Runtime 按机制、领域所有权和使用方式组织为四个责任分区，不按模块大小或来源命名分类：

- `Foundation/`：基础逻辑分类。Async/Attributes/Binding/Events/IOC/Pool/Serialization/Singleton/Timing/Update/Utilities按功能分组；Binding分Collections/Prefs/Unity，Events分Global/Internal/Subscriptions/Unity，Pool分Abstractions/Collections/Factories/GameObject/Object；
- 原Shared/Kernel闭包并回这些唯一功能族。完整Foundation源码守卫只递归收集该目录全部源码，不锁定历史49文件数、不借正式Runtime DLL或附加Core/Systems/Utilities。根级 `YLFrameworkRoot.cs` 自既有UI集成起已使用Core.UIRootContext，由完整Foundation+Core+Root守卫覆盖，不再误纳入纯Foundation输入；这只是校正过时测试归属，未改Root或核心契约。原AssemblyTool/LayerMaskUtility位于Foundation/Utilities，字典和展示元数据各在Serialization/Attributes；
- `Core/`：Module/MVC/View/Panel/UI 生命周期、消费合同、marker/Analyzer 与正式默认资源；只依赖 Foundation，通过自身窄合同消费系统能力，不引用具体 Systems 实现；
- `Utilities/`：按需组合的工具、数据结构、扩展、查询与通用组件。Overlap/CharacterController2D/MoveablePlatform/Excel 及前三者对应的 Editor 已下放此区；ColorSelector→Core 是明确登记的控件依赖，不要求 Core 反向依赖控件。仓库根 `Tools/` 仍是 SDK 与开发工具；
- `Systems/`：领域服务与规则，可消费 Foundation、Core 合同及适合的 Utilities；跨系统协作逐项明确。StateMachine 的完整配置/可视化工作流明确保留 Systems，轻量纯代码 FSM 留 Utilities；有 Editor、静态入口或 MonoBehaviour 不是自动归入 Systems 的依据；

`Systems/Http/` 当前使用公开 namespace `YLFramework.Http`，旧 `YLFramework.Services.Http` 调用方须更新身份；本轮目录整理不额外改变它的行为合同。`YLFrameworkRoot.cs`、主 asmdef、`csc.rsp` 属于根级身份或装配入口，可留在 Runtime 根部。通用工具只按用途直接消费，不新建系统管理器或中立内核副本。

Tidy守卫分别验证完整Foundation和Foundation+Core闭包：Core递归包含自身全部C#文件，允许Foundation与真实UGUI、Json.NET，不再放行XFABManager。两闭包在Editor和Windows64 Development profile先验证合法消费者，再以相同参考/define验证Foundation→Core、Core→Audio的限定名/别名等违规符号不可解析。参考按真实程序集身份及编译图选择，Json.NET分别使用Editor/AOT插件，固定产品当前两类输入宏；伪路径、预编译YLFramework/测试/optional DLL及错profile插件被拒绝。

Foundation的Unity挂载/销毁适配直接使用原生API，不为复用上层GetOrAdd/Destroy便利扩展新增底层helper。Pool公开域为 `YLFramework.Pooling`，目录所有权仍是Foundation；Systems/Utilities按具体能力合法协作，ColorSelector→Core仍被允许，Table 不参与正式资源所有权。该守卫不替代动态字符串/序列化/生命周期评审，也不等于全平台、SDK、Player构建或最终独立载体验收。测试入口为 `Tests/EditMode/Foundation/FoundationBoundaryTests.cs` 与 `Tests/EditMode/Core/CoreArchitectureBoundaryTests.cs`；Tidy历史记录见 [Tidy方案](../../../.kilo/plans/xf-yl-tidy-architecture.md)，随后 Core/Audio 正式接入及旧栈退役以[阶段 8 Plan](../../../.kilo/plans/xf-yl-8-resource.md)为准。

目录分类只是组织方式，不等于新的程序集边界；namespace不强制与目录一一对应。阶段 7.5 将取色控件、渲染及窗口统一归 Utilities/UI/ColorSelector，定位为与 Json、FSM 同类的按需便利工具。工具层直接依赖 Core Panel 的加载/输入策略；IUIInputGate 及自定义门禁入口退出，不向 Foundation 增加 UI 合同。当前取色类型使用 `YLFramework.UI`，正式 Prefab 与序列化字段/GUID 保留；旧 Core namespace 调用及已退出接口的客户端须更新并重编译。旧样例及其程序集已退役，Samples导出根保留。

### Core 职责归口

| Core Runtime 子目录 | 职责 |
|---|---|
| `Modules/` | Module / SingletonModule / ModuleManager；原主文件集中状态和生命周期，Models/Controllers/Views/Panels partial 仅组织入口，`Requests/` 为启动请求 |
| `MVC/Models/`、`Controllers/`、`Views/` | 各自的正式类型及唯一索引 Manager；ViewManager 的 Registration/Loading/Queries partial 保持同一会话，`Views/Parameters/` 为 LoadViewArgs |
| `MVC/Controllers/SceneController.cs` | 只通过ICoreResourceProvider提交场景请求；本Controller在途责任按注册代次管理，完成后不缓存/卸载Scene，不依赖Systems.Resource |
| `Events/` | CoreEvents / EventManager 领域事件入口，不增加另一份总线 |
| `UI/Windows/`、`Roots/`、`Input/`、`Tips/` | 窗口/加载请求、根配置、输入策略与Tips；PanelManager Loading/Sorting/Input partial共用原状态。取色便利工具归 Utilities/UI/ColorSelector，namespace 为 `YLFramework.UI` |
| `MVC/Views/Components/` | CloseView；TimerObject 已归 Foundation/Timing/Components、namespace 为 `YLFramework.Timing`，物理/动画 Listener 归 Foundation/Events/Unity、namespace 为 `YLFramework.Events`；脚本 GUID 与字段合同保留 |
| `Attributes/`、`Analyzers/` | 正式 marker 与唯一编译器 DLL，属于 Runtime 主体输入作用域，marker namespace 保持 |
| `Resources/YLFramework/Core/` | 既有 Canvas/EventSystem；逻辑加载键 `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem` 不变 |

Core Editor 的 `DefaultResources/` 成对保留生成器与删除保护，`ModuleGeneration/` 为 Module 模板，`UIPanelAssetMigration` 与 Core Help 保留在 Core，namespace 为 `YLFramework.Editor.Core`。通用 `ScriptableObjectExtensions`、`EditorAssetTool` 和 `DomainAssetOwnership` 归 `Editor/Utilities/Assets/`；项目设置支撑归 `Editor/Utilities/Settings/`，与 Serialization/Scene/TreeViews/Windows 一同使用 `YLFramework.Editor.Utilities`。必要 Drawer/Listener Inspector 使用 `YLFramework.Editor.Foundation`。移动保留 GUID，不随源码路径改写 GeneratedAssetRoot 或项目设置存储根；派生设置类型 FullName 变化可能改变文件名，旧值须显式迁移、冲突拒绝，不能据目录移动宣称设置已兼容。

已有 Buff 设置的升级输入仅为 `ProjectSettings/YLFramework/Core/YLFramework.BuffSystem.BuffSystemSettings.asset`，新身份对应 `ProjectSettings/YLFramework/Core/YLFramework.Editor.Buff.BuffSystemSettings.asset`。默认加载器不自动认领或迁移旧文件；基类在 `Instance`（含缓存命中）、`LoadOrCreate` 和 `Save` 写盘前检查 Buff 声明的精确旧全名，存在旧文件便拒绝，不创建默认值。设置页显示错误和迁移菜单，不在错误后继续保存；`ProjectSettings/XFGameFramework.BuffSystem.BuffSystemSettings.asset` 等外来文件不参与迁移。

升级已有工程时显式执行一次 `YLFramework/Buff/Migrate Legacy Project Settings`。迁移器先通过 Unity 反序列化确认旧文件只有一个精确的 `BuffSystemSettings` 对象，再独占创建相邻唯一 `.bak` 并验证原始字节，随后用不覆盖的 `File.Move` 改到新路径。它不改写 YAML namespace，实际重载后比较只读序列化快照及文件字节，保留效果类型、效果数据、分组和持久资产引用；原 Provider 对象清除序列化绑定，新设置实例重建非序列化字典缓存。没有旧文件时不创建目录、不重载、不生成备份，重复执行是 no-op；没有自动启动迁移钩子。

新旧文件同时存在、路径为目录或链接、旧类型不兼容或含多个对象时拒绝迁移，不覆盖用户文件；类型校验失败不会创建备份。备份或移动后的验证失败会明确报错并保留已有现场，不自动覆盖、恢复或删除文件，须检查错误中列出的路径后人工处理。旧脚本 GUID 和三类字段不变只是兼容前提，不能代替迁移验收；当前仍没有仓库外真实历史 Buff 设置输入的验证证据，合成夹具或干净工程的默认配置验证不代表这些历史数据已获验证。

CommonSystem的局部Unity适配融入`Foundation/Events/Unity`：动画/物理组件现统一使用 `YLFramework.Events`，类型短名、字段和 GUID 保留；`MonoEventListener` 只转发五个原生生命周期事件。通用EventListener新增物理LayerMask，默认全层、None明确禁用，只过滤12个物理入口且同时约束代码/Inspector，不改变指针消息或另建总线。SceneController归Core消费窄资源合同；资源初始化、SceneRecord/Lease与显式卸载仍归Resource/应用，不在YLFrameworkRoot增加bootstrap或挂载管理器。用途、组件选择见[Events](Modules/Events.md#局部-unity-监听组件)及[Resource](Modules/Resource.md#core-scenecontroller)。

上述目录承接既有结构整理成果，不重复历史迁移。五类产品程序集归属迁移时保留原脚本/存续资源GUID、namespace、公开API和序列化字段；该结构整理批未改Core资源后端。随后8.6a将ProjectName解释为正式Resource ProjectId并保留Prefab/Resources fallback与生命周期，8.6c解除主体XFAB依赖，详见阶段8 Plan。UI Toolkit测试用有上限的真实绑定状态等待，Drawer/容器实现不变；不把结构整理证据扩大为全量套件/全平台或阶段8、9交付。

主要模块间协作：Binding 依赖 Events（变更回调）与 Pool；FSM 依赖 Pool（池化复用）；Resource 复用 Runtime 基础工具。正式 UI 逻辑归 Core，复用唯一 Module/View 会话、Foundation Events/Update/Pool，并通过窄消费合同接入正式Resource；同程序集不代表恢复旧 UI Manager，Save 与 UI 职责相互独立。

### 成熟 Utilities 边界

| 工具 / namespace | 正式职责与协作 | 接入与所有权 |
|---|---|---|
| FSM / `YLFramework.FSM` | Runtime 轻量纯代码状态机，复用 Foundation 池；与 `Systems/StateMachine` 的完整配置/可视化工作流分层共存，也不替代外部 XFFSM | `Fsm<TStateId>.Allocate`，调用方驱动及回收；不依赖 Module / StateMachine / 统一 FSM Manager，Module 关闭不自动停止 |
| Grid / `YLFramework.Grids` | Runtime 矩形 / 稀疏二维数据结构，无框架内依赖；不强制替代为其他 Foundation 容器 | 直接 `new NormalGrid<T>` / `DynamicGrid<T>`，调用方管理数据 |
| Json / `YLFramework.Utilities.Json` | Runtime JSONObject 树处理，无框架内依赖；与 Json.NET 的强类型序列化、Save 文件策略、HTTP 传输职责分离 | 直接创建 / 解析节点，不建设全局 JSON 服务 |
| Table / `YLFramework.Tables` | Runtime 通用表与多值索引；TableIndex 复用 Foundation 的 List / Dictionary 池，不接管正式Resource索引 | 调用方维护索引及 Clear / Dispose；不取得数据项 / 资源的释放权 |
| ZipTool / `YLFramework.Compression` | 自有包装使用 Compression；SharpZipLib 子树保留现有 `YLFramework.Utilities.ZipTool.*`。整体源码保持 BCL-only，不新增 DLL | 常规使用ZipUtility/GZipUtility，多次读取以using管理ZipArchiveReader；不建设压缩Manager |
| ColorSelector / `YLFramework.UI` | 四个取色类型统一归 Utilities/UI/ColorSelector，依赖 Core Panel 策略的便利工具，不是中立基础机制 | 窗口由 Module.LoadPanel 按需打开；独立控件无 Panel 时不强制创建 owner；无自定义门禁接口或新 Manager |

FSM/Grid/Json/Table/ZipTool 无需组件挂载或公共Root；ColorSelector 是 UGUI 工具，窗口按需复用 Core 的 Module/Panel。Pool使用YLFramework.Pooling公开域且不依赖Core领域，机制闭包见[Pool](Modules/Pool.md)。五个产品程序集身份迁移是阶段 7 的明确成本；JSONObject/SharpZipLib来源与完整MIT随Runtime导出，分发限制见下文。


Core 中非领域扩展已退出，通用能力统一归 `YLFramework.Extensions` / Runtime；`StringTools` 归 `YLFramework.Utilities`。只有两份相同的 Unity fake-null 算法收敛为 Object 入口，递归 `SetLayer` 并入现有 GameObject 工具；单对象设层、不同概率离散化、逆插值与分量 / 方向算法仍按实际契约保留，不机械改名或合并。详见 [Extensions](Modules/Extensions.md)。展示元数据与必要 Drawer 保持 5.1 的 Foundation / Editor 边界。

Timing 位于 `Runtime/Foundation/Timing/`，namespace 为 `YLFramework.Timing`。默认服务与显式手动 TimerScheduler 共用唯一秒/帧内核；内部 TimerDriver 为独立 DDOL 薄驱动，不借用场景组件或创建 Root/UI。Scope、ReusableTimer、GO binding 与 TimerWait 不增加调度器；`YLFramework.Async.CoroutineObservation` 仅观察借用 producer。`YLFramework.Timing.TimerObject` 保留脚本 GUID 与事件字段，作为 Timing/Components 适配消费公共服务。旧TimerManager与Tools/Timing已退出新YL；原XF包不修改。详见 [Timing](Modules/Timing.md)。

### HTTP / AsyncOperation 边界

HTTP 正式位于 `Runtime/Systems/Http/`，namespace 为 `YLFramework.Http`，`HttpManager` / `SendWebRequest` / `HttpRequestStatus` 归 `YLFramework.Runtime`。使用 asmdef 的消费者显式引用 Runtime，不再为 HTTP 带入 Core / XFAB；旧 Core 类型退出且不留转发壳，调用方更新 using / 完整类型名。所有入口与 Abort 仅在 Unity 主线程使用，无需挂载组件：GET、POST Form/JSON、PUT/DELETE 返回可 yield 的请求。默认 timeout 为 5 秒，可在前置配置中覆盖；显式 headers 可覆盖默认 Content-Type。GET 合并原 query、保留 fragment，新增键/值分别编码，null 值为空，数值按 InvariantCulture；Form 为 UTF-8 URL 编码，JSON 字典使用 Json.NET，字符串原样发送。

`PreWebRequest` / `PostWebRequest` 现在为可 `+=` / `-=` 的静态事件，订阅者在自身结束时解除订阅，HTTP 会话重置会清空订阅。前置事件同步配置原生请求，异常提交 Failed 并保留原始 Exception；后置事件只观察原生完成（含协议/网络错误），不在构造失败、取消或退出时调用。后置事件的原生对象仅在回调内借读，返回后释放，不能持有、重发或主动 Dispose；若前置配置替换 Handler，原先的 Handler 和回调返回时附着的新 Handler 均交由 HTTP 清理，配置过程中自行创建但从未交付/又自行替换的中间对象仍由配置者负责。

`SendWebRequest.Status` 为独立稳定终态（Pending / Succeeded / Failed / Canceled / Invalidated），`Error`、`NativeResult`、`ResponseCode`、`ResponseHeaders`、`Text` / `Data` 在完成通知时脱离已释放的原生对象读取；协议失败也保留响应正文。`Abort` 幂等，只停止本请求独占的传输，不撤销服务器已执行的副作用。启动拒绝、退出及会话失效都释放 HTTP 自有请求/Handler；Module.Close 不清全局 HTTP。后置观察和完成通知异常报告但不反写终态，资源清理不依赖通知成功。

Foundation `CustomAsyncOperation` 提供 `IsDone/Progress/Error` 和历史通知：legacy `Completed` 事件先于 `AddCompleteEvent`，多播抛错会中断后续通知；完成后新增 `AddCompleteEvent` 立即同步调用（异常仍传播给订阅者）。派生类调用 `Complete` 或 `CompleteWithoutNotify`，Unity 等待入口仍为 `keepWaiting`。基类重复完成和完成后 Abort 仍可更新 Error，重复 Abort 仍运行 hook，不作为领域稳定终态或物理取消权威。HTTP 自管终态；View/Panel、Audio、Resource 保留各自 owner、逻辑取消、租约与共享 producer 所有权，不新增公共任务系统。

自有旧 `YLFramework.Core.LogUtil` 已退役，不保留转发壳或第二套文件 Logger；其全局异常订阅、后台写盘、三天文件清理及退出回调随实现退出。历史日志文件、原 XF LoggerSystem 和原游戏日志路径不修改。正式文件日志仍是长期需求，由后续 LoggerSystem 专题融合，本次退出不表示已有正式替代品。

上述外围闭包已通过阶段 5.5 的真实同域双 Play、Windows64/Mono Development Player 回环与退出、代表性 Core UI / Audio 消费者、原游戏 Editor / Player 非干扰及实际导出门禁。HTTP 独占原生清理不扩展为领域统一取消；`Application.quitting` 与后续场景销毁是不同验证相位。测试隔离原 Logger 不表示原 XF 生态已支持无 Domain Reload；临时 XFAB 依赖、文件 Logger、跨平台 / AOT 与最终独立承载仍按后续专题处理。范围、原生取消诊断与恢复证据见 [运行时外围方案的 5.5 记录](../../../.kilo/plans/xf-yl-5-runtime.md)。

### Timer / Action / Audio 边界

| 模块 | 职责 | 名字与清理范围 |
|---|---|---|
| Timer | 独立延迟、有限 / 无限周期到期回调 | `TimerScope` 管理命名与匿名任务；普通所有者用 CancelAll / Dispose 清理 |
| ActionKit | 逐帧进度、插值、条件与 Sequence / Parallel / Repeat 组合 | 命名根动作复用对象 GameObject、当前场景或全局执行器；`ActionHandle` 仅控制原动作执行租约 |
| AudioKit | Music / Voice / Sound、显式命名同步/异步取得与逻辑取消、代次安全 AudioHandle、随机/空间音效、去重与可选 Mixer | 唯一音频播放生命周期；内建 Action 桥接。直接 Clip / Mixer 借用，命名 Clip 由 ResourceAudioClipProvider 包装独立 AssetLease 保活/受控闲置缓存；Resource 是唯一资产/Bundle 释放权威，XFAB 适配仅借用 |

Timer 与 Action 使用独立名称索引，不增加共享 scope、全局任务表或第三套执行器。Timing 不提供逐帧 progress；Action 的 Delay 不改写为 Timer 包装，Audio 播放链也不强制绑定 Timer。两者的首帧、循环、异常和时钟行为并不完全等价。

Action 的 namespace 为 `YLFramework.Actions`（复数，避免遮蔽 `System.Action`），Audio 为 `YLFramework.Audio`。二者共用 Runtime，但保留自身领域生命周期与行为适配；Audio 直接复用 Foundation 的 Binding、Events、Pool 和 Host，不再复制通用包装或加载层。Help 为 `YLFramework.Editor.Help`，提供方为 `YLFramework.Editor.Actions` / `.Audio`。

Audio 的可选 Mixer 不新增程序集或宿主：`AudioOutputRoute` 表达继承分类默认、指定组与 Direct，`Mixer/AudioMixerConfig` 只保存外部资产引用，Apply/Clear 仅更新运行时默认组，不写 PlayerPrefs。`AudioMixerControl` 独立显式控制参数与 Snapshot；Settings 音量仍只控制 Source，不自动绑定 Mixer 音量或 Ducking。薄 `AudioMixerController` 的 Awake 仅路由，禁用/销毁不清全局配置；清路由不停止单次覆盖，Shutdown 仍停播并清理自有请求，两者均不撤销原生参数控制或 Snapshot 状态。详见 [Audio 的可选 Mixer](Modules/Audio.md#可选-mixer)。

正式 `AudioClipLibrary` 只负责具名素材的均匀/加权选择；`AudioGroupPlayer` 是显式命名通道的组件 owner，直接 Clip、命名异步和借源均提交同一 AudioManager/Voice。其自动播放默认 Manual，FirstStart/EveryEnable 显式配置，不改原游戏组件身份或六个设置键。通道 `VolumeChanged/MutedChanged` 先应用原请求再通知，同值不通知，重入/退出不会向新身份投递旧值；监听异常继续通知仍属原代次的其他监听后聚合报告。正式命名后端为 ResourceAudioClipProvider；主体 XFAB 过渡依赖与旧 YL 资源栈已退出，原 XF Package 不修改。详见 [素材组与组件](Modules/Audio.md#素材组与组件)。

## 主要入口

| 入口 | 说明 |
|---|---|
| [`Runtime/Systems/Action/Core/ActionKit.cs`](../Runtime/Systems/Action/Core/ActionKit.cs) | 动作工厂与全局消息入口；详见 [Action](Modules/Action.md) |
| [`Runtime/Systems/Audio/AudioKit.cs`](../Runtime/Systems/Audio/AudioKit.cs) | 直接 Clip / 显式命名音频播放、安全句柄、请求取消与资源租约、设置、查询及可选 Mixer；显式参数/Snapshot 控制见 `Mixer/AudioMixerControl`，详见 [Audio](Modules/Audio.md) |
| [`Runtime/YLFrameworkRoot.cs`](../Runtime/YLFrameworkRoot.cs) | 框架根单例（`MonoSingleton<YLFrameworkRoot>`，`DefaultExecutionOrder(-20)`），负责框架根对象的 DontDestroyOnLoad |
| [`Runtime/Core/Modules/Module.cs`](../Runtime/Core/Modules/Module.cs) | 唯一业务 owner，保留模块启动/关闭、MVC 与 View 接入协议 |
| [`Runtime/Core/MVC/Controllers/SceneController.cs`](../Runtime/Core/MVC/Controllers/SceneController.cs) | Module场景加载便利入口，直接返回Core原请求，只清自己的在途责任，不持成功Scene卸载权 |
| [`Runtime/Core/MVC/Views/View.cs`](../Runtime/Core/MVC/Views/View.cs) | 正式 View 会话与表现生命周期 |
| [`Runtime/Core/UI/Windows/PanelManager.Requests.cs`](../Runtime/Core/UI/Windows/PanelManager.Requests.cs) | 每消费者一份 Module pending 记录，按实际根和来源协调 Single/Multiple；复用 View 生命周期与 Core 资源责任 |
| [`Runtime/Core/UI/Roots/UIRootContext.cs`](../Runtime/Core/UI/Roots/UIRootContext.cs) | 根/层/遮罩配置，无独立窗口 owner 或加载缓存 |
| [`Runtime/Systems/Resource/ResourceSystem.cs`](../Runtime/Systems/Resource/ResourceSystem.cs) | 唯一显式初始化/关闭的资源权威，统一资产/实例/Scene pin，供 Core/Audio 消费 |
| [`Runtime/Systems/Equipment/EquipmentManager.cs`](../Runtime/Systems/Equipment/EquipmentManager.cs) | 装备双创建、挂载与精确归还；不管理背包、存档或Skill/Buff联动 |
| [`Runtime/Foundation/Timing/Timer.cs`](../Runtime/Foundation/Timing/Timer.cs) | 静态默认计时入口，秒/帧/日期投递；控制与清理不创建宿主 |
| [`Runtime/Foundation/Timing/TimerScope.cs`](../Runtime/Foundation/Timing/TimerScope.cs) | 独立命名与任务所有权范围；CancelAll 可复用，Dispose 永久关闭范围 |
| [`Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs`](../Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs) | 为 Mono 单例指定运行时层级路径（如 `GameObjectPool`）；正式 UI 根不是 Mono 单例 |

框架预制体：`Prefabs/YLFramework.prefab` 保留公共框架根与原 UI 布局，UI 组件已迁为 `UIRootContext` 和 `UITipsPresenter`；`Prefabs/UITips.prefab` 为正式提示项，`Prefabs/ColorSelector.prefab` 为独立重建的取色工具，均使用 UI layer。Foundation 不反向引用 Core；阶段 3 的适配、统一输入、真实双 Play 和 Windows64/Mono Player 属于历史验证，不替代新身份或其他平台的验收，详见 [UI](Modules/UI.md)。

## Editor 工具

Resource Editor 按 Data/Build/Publishing/Workbench/Inspectors/CodeGeneration 归组；原 `Service/` 混合目录已解散。工作台页面与 `ResServiceEditorWindow` 在 Workbench，三个 Inspector 在 Inspectors，ResourceBuildProgress/ResourceBuildSourceLocator 在 Build，ResourceEditorOperations/ResourceEditorSimulation 在 Resource 根部；全部使用 `YLFramework.Editor.Resource`，类名、菜单和数据合同不随路径改写。Help 的 FoundationToolsHelpProvider 已归 `Editor/Help/`；Zip 源码仍以 BCL-only 源码探针验证，不恢复旧产品 DLL。

`YLFramework.Excel` 位于 `Runtime/Utilities/Excel/`，提供三类字段 Attribute 及来源数值不变的 `ExcelFieldsType`；自定义转换器仍覆写 `Import/Export/SetValue`，Runtime 无 xlsx 后端或文件 IO。可选后端只在 `Integrations/Excel/Editor/`，namespace 为 `YLFramework.Integrations.Excel.Editor`，不成为普通 Runtime/Editor 依赖。原包仍在，消费者须明确使用 YL namespace，不扫描或转发原 XF 类型。

### 配置数据接入

Localization保持`MultiLanguageConfigs -> LanguageConfig -> LanguageInfo`布局。显式`AddConfig`或`LoadConfigAsync`返回owner句柄；关闭归还句柄，`GetConfig(string)`及按根/名称`RemoveConfig`仍可用。`LocalizeString/LocalizeImage`直接刷新并消费既有ResourceImage，不重启业务物体。请求语言、中文适用变体、English、显式登记顺序构成确定回退；Other公开text反射由应用为IL2CPP保留实际成员。

DataAssets保留`ScriptableObjectConfigs/ScriptableObjectData`与`SingletonScriptableObjectConfigs<T>`、类型脚本GUID、根/子资产、名称身份和hash查询。多owner共享登记，每个owner归还自己的Registration/异步Asset；hash碰撞明确拒绝，不以“无调用方”删除`StringToHash/GetConfig(int)/RemoveConfig(int|string)`。Editor入口`Window/YLFramework/Data Assets`，只编辑显式选择的YL根，不扫描或删除旧XF资产。

**RootAssetTypes**是应用传给`ResourceProjectMount.assetTypes`或`ResourceLocalSnapshot.CreateCatalog`的显式编译类型清单，不是新增全局注册器。B根类型为`typeof(YLFramework.Localization.MultiLanguageConfigs)`和`typeof(YLFramework.DataAssets.ScriptableObjectConfigs)`；闭合的自定义根/直接加载的具体子资产类型由应用同时登记，不能把开放泛型当运行时根。不改Resource默认类型表或Core合同。

**AssetMetadata**的当前C#字段为：SO根`displayName/configs/typeScriptGuid`、条目`parent/id/configName/icon/description`；语言根`displayName/languages`（公开`Languages`列表）、语言`language/displayName/languagesInfo`、条目`id/type/str/image/url/notes`。既有Unity字段旧名通过精确`FormerlySerializedAs`保留。资源地址中的`projectName/imageName/subAsset`仍是YL ProjectId/Path/SubAsset，不自动转换XF地址。两处`excel_path`已删除，且不以隐藏字段、迁移器或别名恢复。

可选Excel提供 `YLFramework.Integrations.Excel.Editor.ExcelAuthoring` 的SO/List/JSON导出及预览，`ExcelImportPlan`先核`Errors/Changes/CanCommit`再`Commit(true)`，取消不写；过期输入、新引用失效和部分应用失败均拒交或回滚。保留`boolean -> Bool`、`int64 -> Long`。已选SO根菜单位于`Assets/YLFramework/Excel/`；导出成功后EditorPrefs按资产GUID记忆工作簿，Inspector的“在文件夹中打开Excel”仅在文件存在时启用，改名/移动不丢记录，不把绝对路径写回.asset。Localization尚无Excel作者流程。选装时完整携带`Integrations/Excel/Editor`；默认包不包含它。

Skill/Buff/Mission/Equipment/DataAssets的既有工作表列名不随本轮C#字段改名变化。Excel Editor对这些自有领域字段使用唯一的`FormerlySerializedAs`旧名冻结表头，ID定位、普通字段导入和导出共用映射；例如`buff_id/skill_name/equipment_name`保留。这些别名同时属于工作表协议，后续不能随意删除或增加多重歧义别名。外部模型仍使用原本的字段名规则，不把用户的Unity别名自动扩展为Excel协议；未知列、重复列和ID校验保持严格。

统一Save由`SaveStore`承担唯一文件/索引/槽位/模型/异步/缓存权威，`SaveManager`只提供Unity路径和门面。只支持当前v2，损坏/IO/版本失败不变成默认值；备份读取注明来源，显式恢复才修盘。没有SaveMigration、v1复制或XF Archive导入，普通删除重载保留。用法与并发/取消边界见[Save](Modules/Save.md)。

### 领域玩法接入

Skill/Buff/Mission分别位于`Runtime/Systems/`及对应`Editor/Systems/`，Runtime namespace 为 `YLFramework.Skill` / `.Buff` / `.Mission`，Editor 为 `YLFramework.Editor.Skill` / `.Buff` / `.Mission`。沿用各自Manager与配置类型，不依赖DataAssets基类，也不新增共享服务权威。`AddConfig`借用定义，`LoadConfig(ResourceSystem, ResourceAddress)`由Manager持有根Lease；应用显式登记`SkillsConfig`、`BuffsConfig`、`MissionsConfig`根类型。运行实例使用既有YL对象池并克隆配置，宿主关闭调用`CloseHost`，会话退出调用`Shutdown`归还Update句柄、池、回调和Lease；不能以关闭某个Module自动替代任意外部owner的清理。当前回调、`ResetData/ClearRuntime`和按ID/实例/类型移除保留，不提供XF旧事件/旧池方法/宿主旧名兼容。

三套作者窗口位于`Window/YLFramework/SkillSystem`、`BuffSystem`、`MissionSystem`，复用共享平面重排、Undo与资源预览。条目只删除自己拥有的子资产或根GUID目录，外部引用仅解除关联。脚本绑定使用当前类型全名/GUID，实际派生CLR类型须由应用按IL2CPP路径定向保留；根类型登记不等于反射子类已自动保留。

`YLFramework.RedDots.ReddotManager.RegisterPath`返回独立来源token，owner退出时Dispose；普通`AddPath/RemovePath`保留集合语义，不能夺取独立来源。`Reddot`只切换显式`Indicator`，停用/销毁精确退订，不切换全部业务子节点。`YLFramework.Logging.LoggerManager`默认被动，仅在Play主线程显式`Enable(new LoggerOptions { WriteToFile = true })`才写盘/接管，`Disable`只归还仍由自己拥有的handler；与原embedded XF Logger共存，不修改或替换原包。

可选`Integrations/DomainExcel`需完整 Excel Editor后端及显式`YL_DOMAIN_EXCEL`。入口 namespace 为 `YLFramework.Integrations.DomainExcel.Editor`；`DomainExcelCommands.PreviewImport`使用显式工作簿路径，预览后明确取消或确认；根资产不保存本机绝对路径。Skill/Buff/Mission当前没有最近工作簿/打开文件夹入口，不提供Mission条目+参数跨sheet原子事务或全局Buff Effect库xlsx导入。普通Effect/条件/奖励/参数作者工具继续存在；默认核心导出不含这两个可选Integrations目录。

### Equipment 接入

Equipment已融合至YLFramework并通过技术及用户验收；主工程与无XF独立工程Mono、IL2CPP Low均有真实代表性图形运行证据。长期交付入口见[总体蓝图Equipment专项](IntegrationPlan.md#10-equipment-独立专项)。这不代表完整人工图形操作、IL2CPP High、移动平台或原生零泄漏已经验证；功能和原始引用使用权边界不变。

`YLFramework.Equipment`保留`EquipmentManager`静态主入口、`IEquipment/IEquippable`及Asset/Script双模式。应用先初始化Resource；通过`ResourceProjectMount.assetTypes`或`ResourceLocalSnapshot.CreateCatalog`显式提供`typeof(EquipmentsConfig)`及直接加载的具体派生类型，再调用`LoadConfig(resources,address)`或借用`AddConfig(root)`。`projectName/assetName`和图标字段采用YL ProjectId/Path，不读取旧XF配置。

`CreateEquipment(id)`先设置共享只读`EquipmentData`与空`Equipper`再触发领域`OnLoaded`，不承诺早于Prefab原生Awake/OnEnable。Prefab根须有且仅有一个IEquipment；Script须为具体普通类、public无参构造，字符串类型由应用按IL2CPP路径定向保留。运行态字段放实例，不能修改登记中的定义。同一实例跨宿主返回AlreadyEquipped，同ID的不同实例可同时装备；回调抛错撤销本域挂载并清理，不承诺回滚外部业务副作用。

Manager创建的实例由其持有既有InstanceLease或CLR池责任，直接`Equip`的外部对象只借用。外部owner自行保活配置/资源；卸下或关闭不替其调用OnLoaded/OnUnloaded、销毁或回池。宿主结束显式`CloseHost`；`UnEquip(equipment,false)`留下可重装的自有实例，最终由DestroyEquipment、RemoveConfig或Shutdown归还。应用退出与Editor会话退出清理全部自有结果，包含未装备者；只归还自身根Lease，不关闭ResourceSystem或清共享缓存。归还后的原始IEquipment引用不得继续使用，不提供跨checkout的公开Handle保护。

作者入口为`Window/YLFramework/EquipmentSystem`，新建根位于`YLFramework/EquipmentSystem(装备配置)`。独立条目放`Assets/YLFrameworkData/Equipment/<根GUID>/`，显式刷新补入本根条目，getter不扫描/写盘。派生配置、Script绑定、资源选择/预览、复制/删除、重排及独立窗口选择保留；对自有独立文件的Undo/Redo保留原始资产/meta身份，冲突不覆盖外来文件。外部引用只解除，所有者结束与配置删除有完整回调关闭屏障。

装备选配xlsx使用`DomainExcelCommands.PreviewImport(EquipmentsConfig,path)`；导出、预览/确认/取消及打开最近工作簿目录位于`Assets/YLFramework/Domain Excel/`。路径按根GUID存EditorPrefs，不进.asset；暂存阶段同步最终Script类/GUID，跨根ID在预览及提交前复核，失败不部分写入。Excel后端保留原7参PreviewScriptableObjects，新增8参afterImport重载仅调整暂存派生元数据；ExcelImportPlan.AddValidation执行只读的预览/提交前约束，不用于提交后修补。

`YLFramework.Editor.Utilities.ReorderableTreeView<T>`位于`Editor/Utilities/TreeViews/`，构造为`(ScriptableObject owner, Func<IList<T>> getData, TreeViewState state[, MultiColumnHeader header])`。getter须返回owner当前的序列化列表，不能固定捕获Undo前的List；窗口初始化后`Reload`，停用时`Dispose`归还Undo订阅/本实例拖拽。保留`OnBuildRoot`、`OnDragEnd`、`RefreshDatas`派生方式；默认一槽位一行，含空项与重复引用；过滤行/业务ID须覆盖`GetItemIndex(int)`返回真实数据索引。行引用快照识别另窗等长重排/替换，陈旧节点首次起拖被拒绝并刷新；相同对象内展示字段的编辑由所属Inspector显式Reload。工具只重排并标脏当前owner，不自动去重/删空项/保存资产，不修改资源预览或Core/Resource合同。

- `Editor/Foundation/`：Attributes 的 DisableEdit Drawer、Serialization 的原生字典 Drawer 与 Events 的 EventListener Inspector；`Editor/Core/` 只保留 Core 默认资源、生成、UI 迁移与帮助。
- `Editor/Systems/`：与 Runtime Systems 对应的领域 Editor；`Shared/DomainSessionLifecycle` 协调 Skill/Buff/Mission/Logging，`Shared/DomainResourceIconGui` 消费 Resource 作者数据，均使用 `YLFramework.Editor.Domain`，不充作 Core/中立工具。
- `Editor/Utilities/`：Serialization/Scene/Assets/Settings/TreeViews/Windows 与菜单通用支撑，以及下放的 Overlap/CharacterController2D/MoveablePlatform Editor；Excel 后端仍在可选 Integrations，不创建普通 Editor/Excel 空模块。`Editor/Distribution/` 承载框架导出入口。
- `Editor/Help/`：Editor主体中的共享帮助分区，额外Help asmdef已退役；保持提供方id、菜单与配置键，不机械拆入每个领域。
- 通用工具：`Editor/Utilities/Menus/ClearLog` / `OpenFolder`、`Editor/Distribution/ExportUnityPackage`，菜单 `YLFramework/Toolkit/...` 保持。
- 资源工具：`Editor/Systems/Resource/`，唯一菜单 `YLFramework/Resource/Workbench`，包含正式 ResourceProject/Group 选择、目录模拟、Builder、Publisher 与代码生成；不再提供旧 Importer 标记/子工程构建。详见 [Modules/Resource.md](Modules/Resource.md)。
- 帮助：YLFramework/帮助复用共享[Help](Modules/Help.md)。core入口仍为YLFramework/Core/帮助，foundation-tools仍为YLFramework/Toolkit/帮助，actionkit/audiokit及菜单保持；源码职责分离但共用Editor主体，不以独立DLL宣称单向编译隔离，只读索引不实例化宿主或运行示例。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。运行时 `AudioKitDebugWindow` 位于 Audio / Diagnostics，仅 Editor/Development Build 提供运行 GUI；诊断不为读取空状态创建 AudioManager、Source 或 Listener。

## XML 与分发

唯一核心配置 `Runtime/csc.rsp` 生成 `Library/YLFramework.Runtime.xml`，包含 Action、Observable、Audio 与桥接文档；DOTween 独立配置生成 `Library/YLFramework.Actions.DOTween.xml`。提供方按真实 Assembly 作为 `XmlPaths` 字典 key 去重，不把同一 Runtime 重复添加。`Library/` XML 是构建产物，不默认导出。

`AudioSourceIndependenceTests` 使用 AssemblyBuilder 直接编译正式 Audio 全目录及其必要自有源码依赖，排除预编译 YL/XF/XFAB 实现 DLL，分别验证 Editor 和 Windows64 Development 非 Editor 条件分支。该源码闭包只允许Unity/BCL、精确UGUI/Json.NET及批准路径/官方SHA同时匹配的BC DLL，不允许同名替代、任意插件或Sirenix。`FrameworkAssemblyIndependenceTests`进一步核对两主体实际编译图、Editor已编译AssemblyRef闭包，并以合法profile引用重新编译完整Runtime/Editor；Editor使用本次独立构建的Runtime，不借旧产品DLL。这些不代替干净工程导入或真正Player构建/运行。

Audio 的最终门禁分别保留真实同域双 Play、原游戏资源链非干扰、Source/DSP 输出与实际 Windows64/Mono Player 的证据，不能以源码闭包或构建成功互相替代。验收场景和 PCM 属于 Tests 的显式夹具，不改变唯一 Runtime Audio 权威、游戏持久资产或默认分发；系统端点出声与人工听感仍需单独确认，阶段状态以 [Audio 专项](../../../.kilo/plans/xf-yl-4-audio.md)为准。

自有YLFramework.Analyzers.dll仅一份，位于Runtime主体作用域的Runtime/Core/Analyzers；RoslynAnalyzer标签作用于主体及真实引用消费者，普通平台禁用，不作为Runtime precompiledReference或Player运行引用。最终Editor/Development Player的实际rsp已核对：Runtime/Editor、MainModule与自动引用主体的Assembly-CSharp-firstpass各一份输入；原XF/XFAB/XFFSM无自有分析器输入。七项Warning和真实正负见证见[Attributes](Modules/Attributes.md#core-使用诊断)。新Player仍含原MCP/NuGet的Microsoft.CodeAnalysis载荷，主体IL无其运行引用，不把它归因于本分析器或宣称整个工程无Roslyn。

默认导出根为 `Editor`、`Plugins/BouncyCastle`、`Prefabs`、`Runtime`、`Samples`、`Documentation` 六个目录，加根 `LICENSE.md`、`CodingStyle.md`、`AGENTS.md` 三个文件。`ExportPackage` 使用 `Recurse` 而非 `IncludeDependencies`；Plugins 当前仅含 BouncyCastle，不递归整个 Plugins 或宿主商业插件。正式 Editor/Help、唯一 Analyzer、Runtime 源码/meta、资源、第三方来源与许可证进入包；Tests、根 Integrations、游戏、SDK、NuGet 缓存和中间产物排除。现有 UGUI/Json.NET 外部依赖不自动携带，主体不要求 XFAB。历史干净首导入和 Player 证据只覆盖各自冻结包，不代表当前 namespace 的新包已验收；入口见 [文档首页](README.md#帮助与导出)。

## 测试

普通测试按 `Tests/<模式>/{Foundation,Core,Systems/<模块>,Utilities}` 组织，Editor 行为在 `Tests/EditMode/Editor/<功能>`；StateMachine 单域测试仍归 Systems，四个下放模块归 Utilities，混合验收不机械拆散。Foundation/Core 嵌套 asmdef、根测试及必要 asmref 保留实际程序集归属与 `UNITY_INCLUDE_TESTS` 约束，移动目录不能充当测试发现成功的证明。`Tests/Acceptance/Core`、`Tests/Fixtures/Core` 和可选 `Tests/Integrations/<接入名>/<模式>` 保留独立边界，测试类型 namespace 与程序集名不因目录改名。DOTween 测试仍同时要求 `DOTWEEN` + `UNITY_INCLUDE_TESTS`。API 示例编译、源码闭包、Unity 编译、Test Runner 与真实 Player 是不同证据；导航见 [Action 测试](Modules/Action.md#测试与帮助)。
