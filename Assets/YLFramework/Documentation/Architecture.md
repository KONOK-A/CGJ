# 架构总览

> [文档首页](README.md)

本文说明当前源码的职责、依赖方向与所有权。具体 API 和操作步骤优先查模块文档；历史设计、版本和验收结果见 [IntegrationPlan](IntegrationPlan.md)。

本页导航：[目录与程序集](#目录与程序集) · [Namespace 与迁移](#namespace-与迁移) · [依赖方向](#依赖方向) · [Core](#core-职责归口) · [HTTP](#http--asyncoperation-边界) · [配置数据](#配置数据接入) · [领域玩法](#领域玩法接入) · [Equipment](#equipment-接入) · [Editor 工具](#editor-工具)

## 目录与程序集

默认产品只有 Runtime / Editor 两个主体程序集。Foundation、Core、Systems、Utilities 是源码职责区，不是四个可独立安装的 DLL；可选集成、样例和测试按需要保留各自编译边界。

| 目录 | 程序集（asmdef） | 职责 |
|---|---|---|
| `Runtime/` | `YLFramework.Runtime` | 运行时主体，含 Foundation / Core / Systems / Utilities；依赖 UGUI / Json.NET，不依赖 XFAB |
| `Runtime/Foundation/` | `YLFramework.Runtime` | 中立机制及其 Unity 适配，按事件、池、计时等功能分组 |
| `Runtime/Core/` | `YLFramework.Runtime` | Module / MVC / View / Panel / UI 生命周期与窄资源消费合同 |
| `Runtime/Systems/` | `YLFramework.Runtime` | 领域服务、玩法规则及完整配置/可视化 StateMachine |
| `Runtime/Utilities/` | `YLFramework.Runtime` | 按需组合的工具、数据结构、检测/移动组件及 Excel 字段合同；Zip 源码保持 BCL-only 边界 |
| `Editor/` | `YLFramework.Editor` | 全部自有编辑器主体，显式引用 Runtime / UGUI / Json.NET，仅 Editor 平台 |
| `Editor/Help/` | `YLFramework.Editor` | 共享 Help 功能分区，保留稳定窗口、菜单/Provider id与持久配置身份，无独立产品 DLL |
| `Editor/Core/` | `YLFramework.Editor` | Core 默认资源、模块生成、UI 迁移与 Core Help；通用资产/设置/预览支撑归 Utilities |
| `Editor/Systems/Shared/` | `YLFramework.Editor` | 跨玩法生命周期及 Resource 图标作者支持，不塞入 Core 或中立 Utilities |
| `Integrations/DOTween/Runtime/` | `YLFramework.Actions.DOTween` | 可选 Action 扩展，受 `DOTWEEN` 约束 |
| `Integrations/Excel/Editor/`、`Integrations/DomainExcel/Editor/` | `YLFramework.Excel.Editor`、`YLFramework.Domain.Excel.Editor` | 可选 xlsx 后端与领域作者接入，仅 Editor |
| `Integrations/HybridCLR/Runtime/`、`Editor/` | `YLFramework.HybridCLR`、`YLFramework.HybridCLR.Editor` | 可选热更叶子，精确约束官方 HybridCLR 9.0.3；dnlib 只在 Editor，主体无反向依赖 |
| `Tests/EditMode/` 根 asmdef | `YLFramework.Tests.EditMode` | 编辑器内单元测试；嵌套程序集与 asmref 按测试节保留实际归属 |
| `Tests/PlayMode/` 根 asmdef | `YLFramework.Tests.PlayMode` | 运行时测试；子目录不机械改成统一程序集 |
| `Tests/Integrations/DOTween/EditMode/`、`PlayMode/` | `YLFramework.Actions.DOTween.Tests.EditMode`、`.PlayMode` | 独立可选集成测试 |

Runtime / Editor 主体的 `autoReferenced=true`。使用 asmdef 的业务仍须显式声明所需引用；可选集成的自动引用、平台与 define 约束以各自 asmdef 为准，不能从目录名推断。

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
  Integrations/  DOTween, Excel, DomainExcel, HybridCLR
  Tests/
    EditMode/   Foundation, Core, Systems/<模块>, Utilities, Editor/<功能>
    PlayMode/   Foundation, Core, Systems/<模块>, Utilities
    Integrations/<接入名>/, Acceptance/Core/, Fixtures/, Tools/
  Plugins/BouncyCastle/, Prefabs/, Samples/, Documentation/
  LICENSE.md, CodingStyle.md, AGENTS.md
```

只使用 Grid、Json 等工具不需要创建 Module 或公共 Root。`YLFrameworkRoot` 是使用相关宿主能力时的装配入口，不负责自动初始化资源、配置或全部领域系统。

### Namespace 与迁移

namespace 表达稳定功能域，不机械拼接 Runtime/Foundation/Systems/Utilities 或内部 Config/Requests/Components 目录。下表省略共同前缀 `YLFramework.`：

| 源码归属 | 当前 namespace |
|---|---|
| Foundation/Async、Attributes、Events、Binding | `Async`、`Attributes`、`Events`、`Binding`；物理/动画 Listener 同归 `Events` |
| Foundation/IOC、Pool、Singleton | `DependencyInjection`、`Pooling`、`Singletons` |
| Foundation/Serialization、Timing、Update | `Serialization`、`Timing`、`Update`；TimerObject 同归 `Timing` |
| Foundation/Utilities | `Foundation`，如 ApplicationTool、AssemblyTool、LayerMaskUtility |
| Core | `Core`；分析器 marker 保持 `Core.RoslynAnalyzers` |
| Systems/Audio、Resource、Save、Http | `Audio`、`Resource`、`Save`、`Http` |
| Systems/Skill、Buff、Mission、Equipment、Reddot | `Skill`、`Buff`、`Mission`、`Equipment`、`RedDots` |
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
| Excel Editor | `YLFramework.Integrations.Excel.Editor` | `YLFramework.Excel.Editor` |
| DomainExcel Editor | `YLFramework.Integrations.DomainExcel.Editor` | `YLFramework.Domain.Excel.Editor` |
| HybridCLR Runtime / Editor | `YLFramework.Integrations.HybridCLR` / `.Editor` | `YLFramework.HybridCLR` / `YLFramework.HybridCLR.Editor` |

升级旧版本时，需要按真实类型归属更新 using、完整类型名、反射/生成绑定并重编译；不提供旧 namespace 转发壳或旧 DLL 兼容。`rootNamespace` 只是新脚本默认值，不决定已声明类型的 CLR 身份。

业务使用 Module / View / Panel、Audio Provider、Action、Resource 及配置合同等公开扩展点，不依赖内部缓存、调度或 Editor 图层/工厂实现。源码目录不是访问控制边界，实际可见性以声明为准。

脚本和资产移动需保留 `.meta` / GUID；序列化字段、资源地址、存档键和 Excel 表头是独立的数据合同。UnityEvent、Script 绑定、Editor 设置路径与 full name / AQN 字符串须逐项检查。`FormerlySerializedAs` 只处理字段旧名，`MovedFrom` 不是 `Type.GetType` 的旧 AQN 别名；两者都不能代替反射、旧 Bundle 或 HybridCLR 基座的兼容验证。

## 依赖方向

- `YLFramework.Runtime` → UnityEngine.UI、Newtonsoft.Json.dll、BouncyCastle.Cryptography.dll；预编译引用使用显式白名单
- `YLFramework.Editor` → `YLFramework.Runtime`、UnityEngine.UI、Newtonsoft.Json.dll；Help与提供方保持源码职责分离，不再是不同产品DLL
- `YLFramework.Actions.DOTween` → `YLFramework.Runtime` + `DOTween.dll`
- `YLFramework.Excel.Editor` → Runtime、Editor、MiniExcel.dll、Newtonsoft.Json.dll；DomainExcel 再引用 Excel Editor
- `YLFramework.HybridCLR` → `YLFramework.Runtime` + 官方 `HybridCLR.Runtime`；对应 Editor 另引用 YL Editor、官方 Editor 和 dnlib，不引入 XF/XFAB 或第二套下载/资源权威
- `YLFramework.Tests.EditMode` → Runtime、Editor、Foundation 测试辅助、UI、TestRunner / NUnit
- `YLFramework.Tests.PlayMode` → Runtime、UI、TestRunner / NUnit

模块内协作：`Runtime/Systems/Audio/Actions/PlaySoundAction` → Action 契约 + Audio 播放入口，全部位于同一 Runtime，不是可拆卸的 optional Integration。BCL Observable 同样内建；DOTween 保留独立第三方程序集边界。Runtime / 普通 Editor 不静态引用 DOTween wrapper 或供应商 DLL。

Resource/Audio 的内建装配集中在 `Runtime/Systems/Resource/Integration/Audio/`：`ResourceSystem.Audio.cs` 属于 `YLFramework.Resource`，`ResourceAudioClipProvider.cs` 属于 `YLFramework.Audio`。这是同一 Runtime 内明确登记的 Resource→Audio 协作，不是可选程序集；`GetAudioClipProvider`、同 System/Project 的 Provider 引用身份、会话结束失效及启动失败/回滚合同不因拆文件改变。

无循环依赖；Runtime 不引用 Editor 程序集。Runtime 中少量编辑器模拟代码（资源加载、AssetBundle 模拟、存档编辑器保护等）使用 `#if UNITY_EDITOR` 条件编译，玩家程序集不依赖 UnityEditor。

### HybridCLR 可选接入

`Integrations/HybridCLR` 通过 `HybridCLRIntegrationExporter` 单独导出，默认不带其 Samples；默认框架和原游戏不自动接入。它使用官方 HybridCLR `9.0.3`，不提供旧 XF 热更 API 或第二套资源/下载链。

程序集登记以官方 HybridCLRSettings 为准。BuildProfile 引用 Resource Recipe，可选择项目入口并一跳绑定共享基座；发布的是入口依赖闭包，原生编译与冻结登记仍覆盖官方全局集合，项目选择不能突破基座能力上限。

基座构建涵盖官方生成/剥离、metadata、Resource 发布/部署与 Player，成功后才写持久完成基线；补丁只读此基线，不重新剥离或改变基座。运行时借用唯一 ResourceSystem，先校验全部输入，再加载 metadata / 程序集；manifest lease 阻止活动代码版本被替换。

先关闭 loader 启动的 Module，再 Dispose loader，最后由应用关闭资源系统。Dispose 不卸载程序集；同进程冲突内容或部分不可逆失败要求重启。Native、校验产物的 EditorSimulation、主动跳过代码资源的 EditorFast 分别标识，Editor 模式不是原生运行证明。安装、构建、窗口与样例的完整步骤见选装目录的 [README](../Integrations/HybridCLR/README.md)。

### 第三方依赖

UGUI 提供 UI，Json.NET 用于存档、HTTP、Catalog 和 Core 配表。随包的官方 `BouncyCastle.Cryptography 2.7.0` netstandard2.0 用于 HKDF-SHA256；AES-256-GCM 使用 Windows CNG，不携带系统 DLL，也没有软件 GCM 或临时明文回退。内容加密的密钥与资源生命周期仍由应用 / Resource 持有，详见 [Resource](Modules/Resource.md#可选-bundle-内容加密)。

EventListener 和字典等 Inspector 使用原生 Editor，不要求 Sirenix Odin；默认产品也不要求原 XF 生态、MCP 或 DOTween。

## Runtime 目录规则（Foundation / Core / Systems / Utilities）

Runtime 按机制、领域所有权和使用方式组织为四个责任分区，不按模块大小或来源命名分类：

- `Foundation/`：基础机制及其 Unity 适配，不能反向消费 Core / Systems / Utilities；AssemblyTool、LayerMaskUtility 归此层的 Utilities 功能组。
- `Core/`：Module / MVC / View / Panel / UI 生命周期、资源消费合同、marker / Analyzer 与默认资源；通过自身窄合同消费系统能力，不引用具体 Systems 实现。
- `Systems/`：领域服务与规则，可消费 Foundation、Core 合同及适合的 Utilities；跨系统协作必须明确所有权。
- `Utilities/`：按需组合的工具、数据结构、扩展和组件。ColorSelector → Core 是明确的控件依赖，不要求 Core 反向依赖取色工具；完整 StateMachine 留在 Systems，轻量 FSM 留在 Utilities。

`YLFrameworkRoot.cs`、主 asmdef 与 `csc.rsp` 位于 Runtime 根部；Root 使用 Core 的 UIRootContext，不属于中立 Foundation。有 Editor、静态入口或 MonoBehaviour，不是自动归入 Systems 的依据。

Foundation 的 Unity 挂载 / 销毁适配直接使用原生 API，不依赖上层 GetOrAdd / Destroy 便利扩展。源码边界守卫分别检查 Foundation 及 Foundation + Core + Root，详见 [测试](#测试)；同一 Runtime 并不取消内部依赖方向。

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

Core Editor 的 `DefaultResources/` 负责默认资源生成和删除保护，`ModuleGeneration/` 提供 Module 模板；UI 资产迁移和 Core Help 同属 `YLFramework.Editor.Core`。通用资产、设置、场景与 TreeView 支撑归 Editor Utilities，不放入 Core。

局部 Unity 动画、物理和生命周期监听属于 Foundation Events，不增加事件总线；场景便利入口属于 Core SceneController，加载与卸载权仍归 Resource / 应用。用法见 [Events](Modules/Events.md#局部-unity-监听组件) 与 [Resource](Modules/Resource.md#core-scenecontroller)。

Module 关闭只清理自己的业务责任，不隐式停止外部 FSM、全局计时器、HTTP 或音频。应用须按创建/登记的所有权显式结束这些能力，不能用“同一程序集”推导共享生命周期。

### 成熟 Utilities 边界

| 工具 / namespace | 正式职责与协作 | 接入与所有权 |
|---|---|---|
| FSM / `YLFramework.FSM` | 轻量纯代码状态机，复用 Foundation 池；与 `Systems/StateMachine` 的配置/可视化工作流分离 | `Fsm<TStateId>.Allocate`，调用方驱动及回收；Module 关闭不自动停止 |
| Grid / `YLFramework.Grids` | Runtime 矩形 / 稀疏二维数据结构，无框架内依赖；不强制替代为其他 Foundation 容器 | 直接 `new NormalGrid<T>` / `DynamicGrid<T>`，调用方管理数据 |
| Json / `YLFramework.Utilities.Json` | Runtime JSONObject 树处理，无框架内依赖；与 Json.NET 的强类型序列化、Save 文件策略、HTTP 传输职责分离 | 直接创建 / 解析节点，不建设全局 JSON 服务 |
| Table / `YLFramework.Tables` | Runtime 通用表与多值索引；TableIndex 复用 Foundation 的 List / Dictionary 池，不接管正式Resource索引 | 调用方维护索引及 Clear / Dispose；不取得数据项 / 资源的释放权 |
| ZipTool / `YLFramework.Compression` | 自有包装使用 Compression；SharpZipLib 子树保留现有 `YLFramework.Utilities.ZipTool.*`。整体源码保持 BCL-only，不新增 DLL | 常规使用ZipUtility/GZipUtility，多次读取以using管理ZipArchiveReader；不建设压缩Manager |
| ColorSelector / `YLFramework.UI` | 四个取色类型统一归 Utilities/UI/ColorSelector，依赖 Core Panel 策略的便利工具，不是中立基础机制 | 窗口由 Module.LoadPanel 按需打开；独立控件无 Panel 时不强制创建 owner；无自定义门禁接口或新 Manager |

FSM / Grid / Json / Table / ZipTool 无需组件挂载或公共 Root；ColorSelector 是 UGUI 工具，窗口复用 Module / Panel。JSONObject / SharpZipLib 的来源和完整许可证随 Runtime 交付，见框架根 [LICENSE](../LICENSE.md)。

通用扩展归 `YLFramework.Extensions`，`StringTools` 归 `YLFramework.Utilities`。Unity fake-null、IO、数学和集合等不同语义不互相替代，见 [Extensions](Modules/Extensions.md)。

### HTTP / AsyncOperation 边界

`Runtime/Systems/Http/` 使用 namespace `YLFramework.Http`，提供 `HttpManager` / `SendWebRequest` / `HttpRequestStatus`。所有入口与 Abort 仅在 Unity 主线程使用，无需挂载组件；GET、POST Form / JSON、PUT / DELETE 返回可 yield 的请求。

- 默认 timeout 为 5 秒，可在前置配置中覆盖；显式 headers 可覆盖默认 Content-Type。
- GET 合并原 query、保留 fragment，新增键值分别编码；null 值为空，数值使用 InvariantCulture。
- Form 为 UTF-8 URL 编码；JSON 字典使用 Json.NET，字符串原样发送。

`PreWebRequest` / `PostWebRequest` 现在为可 `+=` / `-=` 的静态事件，订阅者在自身结束时解除订阅，HTTP 会话重置会清空订阅。前置事件同步配置原生请求，异常提交 Failed 并保留原始 Exception；后置事件只观察原生完成（含协议/网络错误），不在构造失败、取消或退出时调用。后置事件的原生对象仅在回调内借读，返回后释放，不能持有、重发或主动 Dispose；若前置配置替换 Handler，原先的 Handler 和回调返回时附着的新 Handler 均交由 HTTP 清理，配置过程中自行创建但从未交付/又自行替换的中间对象仍由配置者负责。

`SendWebRequest.Status` 为独立稳定终态（Pending / Succeeded / Failed / Canceled / Invalidated），`Error`、`NativeResult`、`ResponseCode`、`ResponseHeaders`、`Text` / `Data` 在完成通知时脱离已释放的原生对象读取；协议失败也保留响应正文。`Abort` 幂等，只停止本请求独占的传输，不撤销服务器已执行的副作用。启动拒绝、退出及会话失效都释放 HTTP 自有请求/Handler；Module.Close 不清全局 HTTP。后置观察和完成通知异常报告但不反写终态，资源清理不依赖通知成功。

Foundation `CustomAsyncOperation` 提供 `IsDone/Progress/Error` 和历史通知：legacy `Completed` 事件先于 `AddCompleteEvent`，多播抛错会中断后续通知；完成后新增 `AddCompleteEvent` 立即同步调用（异常仍传播给订阅者）。派生类调用 `Complete` 或 `CompleteWithoutNotify`，Unity 等待入口仍为 `keepWaiting`。基类重复完成和完成后 Abort 仍可更新 Error，重复 Abort 仍运行 hook，不作为领域稳定终态或物理取消权威。HTTP 自管终态；View/Panel、Audio、Resource 保留各自 owner、逻辑取消、租约与共享 producer 所有权，不新增公共任务系统。

HTTP 不负责全局异常或文件日志；文件日志由 `YLFramework.Logging` 显式启用，见 [领域玩法接入](#领域玩法接入)。

### Timer / Action / Audio 边界

| 模块 | 职责 | 名字与清理范围 |
|---|---|---|
| Timer | 独立延迟、有限 / 无限周期到期回调 | `TimerScope` 管理命名与匿名任务；普通所有者用 CancelAll / Dispose 清理 |
| ActionKit | 逐帧进度、插值、条件与 Sequence / Parallel / Repeat 组合 | 命名根动作复用对象 GameObject、当前场景或全局执行器；`ActionHandle` 仅控制原动作执行租约 |
| AudioKit | Music / Voice / Sound、显式命名同步/异步取得与逻辑取消、代次安全 AudioHandle、随机/空间音效、去重与可选 Mixer | 唯一音频播放生命周期；内建 Action 桥接。直接 Clip / Mixer 借用，命名 Clip 由 ResourceAudioClipProvider 包装独立 AssetLease 保活/受控闲置缓存；Resource 是唯一资产/Bundle 释放权威 |

Timer 与 Action 使用独立名称索引，不增加共享 scope、全局任务表或第三套执行器。Timing 不提供逐帧 progress；Action 的 Delay 不改写为 Timer 包装，Audio 播放链也不强制绑定 Timer。两者的首帧、循环、异常和时钟行为并不完全等价。

Action 使用 `YLFramework.Actions`（复数），Audio 使用 `YLFramework.Audio`，各自拥有独立生命周期。Timing 的独立驱动不创建 Root / UI，TimerScope、GO binding 与 TimerWait 不另建调度器。

详细用法见 [Timing](Modules/Timing.md)、[Action](Modules/Action.md)、[Audio](Modules/Audio.md)。Audio 的 [素材组](Modules/Audio.md#素材组与组件) 只做选曲，[Mixer](Modules/Audio.md#可选-mixer) 只提供可选路由与参数控制；二者都不新增播放器或资源服务。

## 主要入口

| 入口 | 说明 |
|---|---|
| [`Runtime/Systems/Action/Core/ActionKit.cs`](../Runtime/Systems/Action/Core/ActionKit.cs) | 动作工厂与全局消息入口；详见 [Action](Modules/Action.md) |
| [`Runtime/Systems/Audio/AudioKit.cs`](../Runtime/Systems/Audio/AudioKit.cs) | 直接 Clip / 显式命名音频播放、安全句柄、请求取消与资源租约、设置、查询及可选 Mixer；显式参数/Snapshot 控制见 `Mixer/AudioMixerControl`，详见 [Audio](Modules/Audio.md) |
| [`Runtime/YLFrameworkRoot.cs`](../Runtime/YLFrameworkRoot.cs) | 框架根单例（`MonoSingleton<YLFrameworkRoot>`，`DefaultExecutionOrder(-20)`），负责框架根对象的 DontDestroyOnLoad |
| [`Runtime/Core/Modules/Module.cs`](../Runtime/Core/Modules/Module.cs) | 唯一业务 owner，保留模块启动/关闭、MVC 与 View 接入协议 |
| [`Runtime/Core/MVC/Controllers/SceneController.cs`](../Runtime/Core/MVC/Controllers/SceneController.cs) | Module场景加载便利入口，直接返回Core原请求，只清自己的在途责任，不持成功Scene卸载权 |
| [`Runtime/Core/MVC/Views/View.cs`](../Runtime/Core/MVC/Views/View.cs) | 正式 View 会话与表现生命周期 |
| [`Runtime/Core/UI/Windows/PanelManager.Requests.cs`](../Runtime/Core/UI/Windows/PanelManager.Requests.cs) | 按消费者、根和资源来源协调窗口请求，复用 View 生命周期 |
| [`Runtime/Core/UI/Roots/UIRootContext.cs`](../Runtime/Core/UI/Roots/UIRootContext.cs) | 根/层/遮罩配置，无独立窗口 owner 或加载缓存 |
| [`Runtime/Systems/Resource/ResourceSystem.cs`](../Runtime/Systems/Resource/ResourceSystem.cs) | 唯一显式初始化/关闭的资源权威，统一资产/实例/Scene pin，供 Core/Audio 消费 |
| [`Runtime/Systems/Equipment/EquipmentManager.cs`](../Runtime/Systems/Equipment/EquipmentManager.cs) | 装备双创建、挂载与精确归还；不管理背包、存档或Skill/Buff联动 |
| [`Runtime/Foundation/Timing/Timer.cs`](../Runtime/Foundation/Timing/Timer.cs) | 静态默认计时入口，秒/帧/日期投递；控制与清理不创建宿主 |
| [`Runtime/Foundation/Timing/TimerScope.cs`](../Runtime/Foundation/Timing/TimerScope.cs) | 独立命名与任务所有权范围；CancelAll 可复用，Dispose 永久关闭范围 |
| [`Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs`](../Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs) | 为 Mono 单例指定运行时层级路径（如 `GameObjectPool`）；正式 UI 根不是 Mono 单例 |

`Prefabs/YLFramework.prefab` 提供框架根与完整 UI 模板，`UITips.prefab` 是提示项，`ColorSelector.prefab` 是取色窗口。也可以不预放完整模板而按需使用默认 UI 根，配置与输入边界见 [UI](Modules/UI.md)。

## Editor 工具

Editor 代码按功能归入 Foundation / Core / Systems / Utilities，全部普通工具共用 `YLFramework.Editor`。Help 是共享帮助分区，Distribution 只负责导出；不根据 `Editor/` 文件夹名推断独立程序集。

`YLFramework.Excel` 位于 `Runtime/Utilities/Excel/`，只提供字段 Attribute、转换器和 `ExcelFieldsType` 合同，Runtime 不提供 xlsx 文件 IO。实际后端位于可选 `Integrations/Excel/Editor/`，不反向成为主体依赖。

### 配置数据接入

| 能力 | 正式入口 | 所有权与限制 |
|---|---|---|
| Localization | `LocalizationManager.AddConfig` / `LoadConfigAsync` | 返回配置句柄；调用方 Dispose 自己的句柄。布局为 MultiLanguageConfigs → LanguageConfig → LanguageInfo |
| DataAssets | `ScriptableObjectManager`、`ScriptableObjectConfigs` / `ScriptableObjectData` | 多 owner 可共享根，每个 owner 只归还自己的 Registration / 加载句柄；名称与 hash 身份保留，碰撞拒绝 |
| Excel Editor | `ExcelAuthoring` / `ExcelImportPlan` | 预览 `Errors/Changes/CanCommit` 后显式 `Commit(true)`；取消不写，过期输入或引用失效拒交 / 回滚 |
| Save | `SaveStore`，Unity 门面为 `SaveManager` | 唯一文件、索引、槽位、模型与缓存权威；只支持当前 v2，见 [Save](Modules/Save.md) |

`LocalizeString` / `LocalizeImage` 直接刷新内容，不重启业务物体。语言依次按请求语言、适用的中文变体、English、登记顺序回退；Other 的公开 text 反射成员需由应用为 IL2CPP 保留。DataAssets 作者入口为 `Window/YLFramework/Data Assets`，只编辑显式选择的 YL 根。

应用通过 `ResourceProjectMount.assetTypes` 或 `ResourceLocalSnapshot.CreateCatalog` 提供显式编译类型清单。例如 `typeof(YLFramework.Localization.MultiLanguageConfigs)`、`typeof(YLFramework.DataAssets.ScriptableObjectConfigs)` 及自定义闭合根/直接加载的具体条目类型；开放泛型不能作为运行时根。关闭时先停止使用配置的 UI / 业务，再归还配置句柄和根资源。

当前资产字段为：SO 根 `displayName/configs/typeScriptGuid`，条目 `parent/id/configName/icon/description`；语言根 `displayName/languages`（公开 `Languages` 列表）、语言 `language/displayName/languagesInfo`、条目 `id/type/str/image/url/notes`。资源地址中的 `projectName/imageName/subAsset` 表示 YL ProjectId / Path / SubAsset，不转换 XF 地址。工作簿绝对路径不写入资产。

可选 Excel 提供 SO / List / JSON 导出与导入预览，保留 `boolean → Bool`、`int64 → Long`。SO 根菜单位于 `Assets/YLFramework/Excel/`；工作簿路径按资产 GUID 存在 EditorPrefs，只有文件存在时才启用“在文件夹中打开Excel”。Localization 没有 Excel 作者流程；选装时须完整携带后端与许可证。

Skill / Buff / Mission / Equipment / DataAssets 的既有工作表列名是独立协议，不能随 C# 字段改名漂移。后端对这些自有模型使用唯一的 `FormerlySerializedAs` 旧名映射表头，例如 `buff_id/skill_name/equipment_name`；外部模型使用自身字段名规则。未知列、重复列和 ID 校验保持严格。

Save 的损坏、IO 和版本失败不伪装成默认值；备份读取注明来源，只有显式恢复才修盘，不提供 v1 或 XF Archive 导入。

### 领域玩法接入

Skill / Buff / Mission 使用各自 Manager 与配置类型，不依赖 DataAssets 基类。`AddConfig` 借用定义，`LoadConfig(ResourceSystem, ResourceAddress)` 由 Manager 持有根 lease；应用显式登记 `SkillsConfig`、`BuffsConfig`、`MissionsConfig` 根类型。定义与运行态隔离，不修改共享配置来保存实例状态。

Skill / Buff 的宿主结束时调用各自 `CloseHost`；Mission 按配置维护任务/参数运行态，使用 `Reset`、`RemoveConfig` 或 `Shutdown` 管理它，不套用宿主 API。各系统的 `Shutdown` 只归还自己的更新、池、回调与 lease，不能用关闭任意 Module 替代。

作者窗口为 `Window/YLFramework/SkillSystem`、`Window/YLFramework/BuffSystem/Buff` / `Effect`、`Window/YLFramework/MissionSystem`，支持重排、Undo 与资源预览。条目只删除自己拥有的子资产或根 GUID 目录，外部引用仅解除关联。Script 绑定的具体派生 CLR 类型须由应用为 IL2CPP 定向保留；登记根类型不等于自动保留反射子类。

`YLFramework.RedDots.ReddotManager.RegisterPath` 返回独立来源 token，owner 退出时 Dispose；普通 `AddPath/RemovePath` 保留集合语义，不夺取其他来源。`Reddot` 只切换显式 `Indicator`，停用 / 销毁时精确退订，不切换全部业务子节点。

`YLFramework.Logging.LoggerManager` 默认被动。Play 主线程显式 `Enable()` 才安装 handler；文件写入另外由 `new LoggerOptions { WriteToFile = true }` 启用。重复启用返回当前会话，更换配置先 `Disable()`；关闭只恢复仍由自己拥有的 handler，不覆盖后来安装的 handler，也不修改原游戏的 Logger。

可选 `Integrations/DomainExcel` 需要完整 Excel 后端和 `YL_DOMAIN_EXCEL`。`DomainExcelCommands.PreviewImport` 接受显式工作簿路径，预览后确认或取消；不提供 Mission 条目与参数跨 sheet 原子事务，也不导入全局 Buff Effect 库。Skill / Buff / Mission 没有最近工作簿入口，Equipment 的对应能力见下节。

#### Buff 设置迁移

升级旧工程时，默认设置加载器遇到精确旧路径会拒绝写入，须显式执行 `YLFramework/Buff/Migrate Legacy Project Settings`：

```text
旧：ProjectSettings/YLFramework/Core/YLFramework.BuffSystem.BuffSystemSettings.asset
新：ProjectSettings/YLFramework/Core/YLFramework.Editor.Buff.BuffSystemSettings.asset
```

迁移器先验证单一正确类型、备份原始字节，再不覆盖地移动并重载核对；不改写 YAML，不认领外来 XF 设置。新旧并存、类型错误或路径冲突时拒绝；没有旧文件则不做任何写入。中途验证失败保留现场并报错，不自动覆盖、删除或回滚，需按报错路径处理。真实外部历史资产仍需单独验证，GUID 不变或合成测试通过不能代替该验证。

### Equipment 接入

Equipment 提供 Asset / Script 双创建与单一挂载归属，不管理背包、存档或自动的 Skill / Buff 联动。公开入口为 `YLFramework.Equipment.EquipmentManager`、`IEquipment` / `IEquippable`。

应用先初始化 Resource，并显式登记 `typeof(EquipmentsConfig)` 及直接加载的具体派生类型，再调用 `LoadConfig(resources, address)`；直接 `AddConfig(root)` 则借用根配置。`projectName/assetName` 与图标字段使用 YL ProjectId / Path，不读取旧 XF 配置。

`CreateEquipment(id)`先设置共享只读`EquipmentData`与空`Equipper`再触发领域`OnLoaded`，不承诺早于Prefab原生Awake/OnEnable。Prefab根须有且仅有一个IEquipment；Script须为具体普通类、public无参构造，字符串类型由应用按IL2CPP路径定向保留。运行态字段放实例，不能修改登记中的定义。同一实例跨宿主返回AlreadyEquipped，同ID的不同实例可同时装备；回调抛错撤销本域挂载并清理，不承诺回滚外部业务副作用。

Manager 创建的实例由其持有 InstanceLease 或 CLR 池责任；直接 `Equip` 的外部对象只借用，外部 owner 自行保活配置/资源，Manager 不替它调用创建/销毁钩子或回池。宿主结束显式 `CloseHost`；`EquipmentManager.Unequip(equipment, false)` 留下可重装的自有实例，最终用 `DestroyEquipment`、`RemoveConfig` 或 `Shutdown` 归还。只归还自身根 lease，不关闭 ResourceSystem 或清共享缓存。归还后的原始 IEquipment 引用不得继续使用，不提供跨 checkout 的公开 Handle 保护。

作者入口为 `Window/YLFramework/EquipmentSystem`，Create 菜单为 `YLFramework/EquipmentSystem(装备配置)`。独立条目放在 `Assets/YLFrameworkData/Equipment/<根GUID>/`，显式刷新才补入本根条目，getter 不扫描或写盘。自有文件支持 Undo / Redo 并保留身份，冲突不覆盖；外部引用只解除关联。

可选 xlsx 接入使用 `DomainExcelCommands.PreviewImport(EquipmentsConfig, path)`；导出、预览 / 确认 / 取消及最近工作簿目录菜单位于 `Assets/YLFramework/Domain Excel/`。路径按根 GUID 存 EditorPrefs，不进资产；暂存阶段同步 Script 类 / GUID，跨根 ID 在预览与提交前复核，失败不部分写入。`ExcelImportPlan.AddValidation` 是预览 / 提交前只读约束，不用于提交后修补。

#### 共享作者工具

`YLFramework.Editor.Utilities.ReorderableTreeView<T>` 的构造为 `(ScriptableObject owner, Func<IList<T>> getData, TreeViewState state[, MultiColumnHeader header])`。getter 须返回 owner 当前的序列化列表，不能捕获 Undo 前的 List；初始化后 `Reload`，停用时 `Dispose` 归还订阅和本实例拖拽。

默认一槽位一行，包含空项与重复引用；过滤行 / 业务 ID 须覆盖 `GetItemIndex(int)` 返回真实数据索引。陈旧节点起拖时拒绝并刷新；显示字段变化由所属 Inspector 显式 Reload。工具只重排并标脏当前 owner，不自动去重、删空项或保存资产。

- `Editor/Foundation/`：Attributes 的 DisableEdit Drawer、Serialization 的原生字典 Drawer 与 Events 的 EventListener Inspector；`Editor/Core/` 只保留 Core 默认资源、生成、UI 迁移与帮助。
- `Editor/Systems/`：与 Runtime Systems 对应的领域 Editor；`Shared/DomainSessionLifecycle` 协调 Skill/Buff/Mission/Logging，`Shared/DomainResourceIconGui` 消费 Resource 作者数据，均使用 `YLFramework.Editor.Domain`，不充作 Core/中立工具。
- `Editor/Utilities/`：Serialization/Scene/Assets/Settings/TreeViews/Windows 与菜单通用支撑，以及下放的 Overlap/CharacterController2D/MoveablePlatform Editor；Excel 后端仍在可选 Integrations，不创建普通 Editor/Excel 空模块。`Editor/Distribution/` 承载框架导出入口。
- `Editor/Help/`：共享帮助分区，提供方按领域组织但不形成额外主体 DLL。
- 通用工具：`Editor/Utilities/Menus/ClearLog` / `OpenFolder`、`Editor/Distribution/ExportUnityPackage`，菜单 `YLFramework/Toolkit/...` 保持。
- 资源工具：`YLFramework/Resource/Workbench`，包含 ResourceProject / Group、目录模拟、Builder、Publisher 和代码生成，见 [Resource](Modules/Resource.md)。
- 帮助：`YLFramework/帮助` 及 Core / Toolkit / Action / Audio 子入口，共用只读索引，不实例化宿主或运行示例，见 [Help](Modules/Help.md)。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。运行时 `AudioKitDebugWindow` 位于 Audio / Diagnostics，仅 Editor/Development Build 提供运行 GUI；诊断不为读取空状态创建 AudioManager、Source 或 Listener。

## XML 与分发

唯一核心配置 `Runtime/csc.rsp` 生成 `Library/YLFramework.Runtime.xml`，包含 Action、Observable、Audio 与桥接文档；DOTween 独立配置生成 `Library/YLFramework.Actions.DOTween.xml`。提供方按真实 Assembly 作为 `XmlPaths` 字典 key 去重，不把同一 Runtime 重复添加。`Library/` XML 是构建产物，不默认导出。

唯一 `YLFramework.Analyzers.dll` 位于 `Runtime/Core/Analyzers`，通过 RoslynAnalyzer 标签作为编译器输入，普通平台禁用；不作为 Runtime precompiledReference 或 Player 运行引用。七项诊断、配置与重建见 [Attributes](Modules/Attributes.md#core-使用诊断)。

默认框架通过原 Exporter 递归导出产品根，不使用 IncludeDependencies，不包含 Tests / 根 Integrations / 游戏 / SDK / NuGet 缓存。UGUI / Json.NET 由目标工程安装，BouncyCastle 及完整来源许可随框架交付；精确根与安装说明统一见 [首页](README.md#帮助与导出)。

## 测试

测试只在源码仓库提供，不随默认包分发。普通测试按 `Tests/<模式>/{Foundation,Core,Systems/<模块>,Utilities}` 组织；Editor 行为在 `Tests/EditMode/Editor/<功能>`，可选集成在 `Tests/Integrations/<接入名>/<模式>`。

| 守卫 | 检查内容 |
|---|---|
| FoundationBoundaryTests | 中立 Foundation 源码依赖与禁止向上引用 |
| CoreArchitectureBoundaryTests | Foundation + Core + Root 的允许依赖和领域消费边界 |
| AudioSourceIndependenceTests | Audio 及必要自有依赖在 Editor / Windows64 Development 条件下的源码编译与引用白名单 |
| FrameworkAssemblyIndependenceTests | Runtime / Editor 编译图、二进制引用及独立源码编译 |

这些守卫不替代动态身份、序列化、资源生命周期、干净导入或真实 Player 验证。历史结果只适用于对应的源码、包、环境与制品，见 [历史档案](IntegrationPlan.md)；本文不维护持续变化的测试数量或“全平台已通过”状态。
