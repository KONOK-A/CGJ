# 架构总览

> [文档首页](README.md)

## 目录与程序集

当前§16修订已完成技术门禁：主体为YLFramework.Runtime/YLFramework.Editor两个，五个额外产品asmdef/meta及ColorSelector样例已退出。用户授权后Canvas恢复原基线，正式Panel混合门禁优先级和Help分组回归先红后绿；最终实际输入、六根导出、新Windows64/Mono MVC/UI/持久UnityEvent/退出Player及独立审查已收口。已随阶段 7 Git 收口提交（`milestone/7-structure`），不宣称阶段8、独立无依赖或所有平台验收完成；实际证据与迁移限制见 [结构方案§17.12](../../../.kilo/plans/xf-yl-7-structure-redesign.md#1712-授权修复与最终验收2026-10-14)。

| 目录 | 程序集（asmdef） | 职责 |
|---|---|---|
| `Runtime/` | `YLFramework.Runtime` | 运行时主体，含 Foundation / Core / Systems / Utilities；依赖 UGUI / Json.NET，不依赖 XFAB |
| `Runtime/Foundation/` | `YLFramework.Runtime` | 中立机制与Unity适配按功能统一分组；Kernel空容器/meta已退出，无第二套内核或产品DLL |
| `Runtime/Core/` | `YLFramework.Runtime` | Modules/MVC/Events/UI 与领域组件；partial仅导航，不改变唯一生命周期与事务 |
| `Runtime/Utilities/ZipTool/` | `YLFramework.Runtime` | Zip/GZip 与 SharpZipLib 源码保持；额外 asmdef/meta 已退役，BCL-only 源码探针待 S6 |
| `Editor/` | `YLFramework.Editor` | 全部自有编辑器主体，显式引用 Runtime / UGUI / Json.NET，仅 Editor 平台 |
| `Editor/Help/` | `YLFramework.Editor` | 共享 Help 功能分区，保留稳定窗口、菜单/Provider id与持久配置身份，无独立产品 DLL |
| `Editor/Core/` | `YLFramework.Editor` | Core 默认资源、模块生成、持久设置、资产生成/预览与 Core Help；通用支撑按职责归 Foundation / Utilities，功能目录不增加编译边界 |
| `Integrations/DOTween/Runtime/` | `YLFramework.Actions.DOTween` | 可选 Action 扩展，受 `DOTWEEN` 约束 |
| `Tests/EditMode/` | `YLFramework.Tests.EditMode` | 编辑器内单元测试 |
| `Tests/PlayMode/` | `YLFramework.Tests.PlayMode` | 运行时测试 |
| `Tests/Integrations/DOTween/EditMode/`、`PlayMode/` | `YLFramework.Actions.DOTween.Tests.EditMode`、`.PlayMode` | 独立可选集成测试 |

Runtime / Editor 主体的 `autoReferenced` 为 true；可选 DOTween/XFAB 集成与测试程序集保留各自关闭自动引用和约束。使用 asmdef 的业务仍须显式声明所需引用；五份已退役产品的二进制/AQN消费者须更新并重编译，不提供旧身份兼容壳。

## 依赖方向

- `YLFramework.Runtime` → UnityEngine.UI、Newtonsoft.Json.dll；主体不 self-reference 已合入源码，预编译插件使用必要白名单
- `YLFramework.Editor` → `YLFramework.Runtime`、UnityEngine.UI、Newtonsoft.Json.dll；Help与提供方保持源码职责分离，不再是不同产品DLL
- `YLFramework.Audio.XFAB` → `YLFramework.Runtime`、XFABManager；独立可选叶子，受包版本和 `YL_AUDIO_XFAB` 约束、`autoReferenced=false`，不反向成为主体必需依赖
- `YLFramework.Actions.DOTween` → `YLFramework.Runtime` + `DOTween.dll`
- `YLFramework.Tests.EditMode` → Runtime、Editor、Foundation 测试辅助、UI、TestRunner / NUnit
- `YLFramework.Tests.PlayMode` → Runtime、UI、TestRunner / NUnit

模块内协作：`Runtime/Systems/Audio/Actions/PlaySoundAction` → Action 契约 + Audio 播放入口，全部位于同一 Runtime，不是可拆卸的 optional Integration。BCL Observable 同样内建；DOTween 与可选 `Integrations/XFAB/Audio/` 各保留独立第三方程序集边界。正式 Audio Runtime 不反向引用 XFAB adapter；后者不自动注册或纳入默认导出。Runtime / 普通 Editor 不静态引用 DOTween wrapper 或供应商 DLL。

无循环依赖；Runtime 不引用 Editor 程序集。Runtime 中少量编辑器模拟代码（资源加载、AssetBundle 模拟、存档编辑器保护等）使用 `#if UNITY_EDITOR` 条件编译，玩家程序集不依赖 UnityEditor。

外部依赖：UGUI、Newtonsoft.Json（存档、HTTP、Catalog 与 Core 配表等）等既有依赖；排除 DOTween 不意味着框架零外部依赖。EventListener 的折叠、页签与 UnityEvent 编辑由普通 Editor 中的原生 CustomEditor 承担，Sirenix Odin 插件整树及其编译引用已退出，不提供 mini-Odin 或兼容空壳。ProjectSettings 的五个未使用 Odin 宏按原配置权限边界保留。

## Runtime 目录规则（Foundation / Core / Systems / Utilities）

Runtime 按机制、领域所有权和使用方式组织为四个责任分区，不按模块大小或来源命名分类：

- `Foundation/`：基础逻辑分类。Async/Attributes/Binding/Events/IOC/Pool/Serialization/Singleton/Timing/Update/Utilities按功能分组；Binding分Collections/Prefs/Unity，Events分Global/Internal/Subscriptions/Unity，Pool分Abstractions/Collections/Factories/GameObject/Object；
- 原Shared/Kernel闭包并回这些唯一功能族。完整Foundation源码守卫递归收集该目录，且只额外包含根级 `YLFrameworkRoot.cs`，不锁定历史49文件数，不借正式Runtime DLL或附加Core/Systems/Utilities；原AssemblyTool/LayerMaskUtility位于Foundation/Utilities，字典和展示元数据各在Serialization/Attributes；
- `Core/`：Modules/MVC/Events、View/Panel/UI、领域组件、marker/Analyzer 与正式默认资源；归 Runtime 主体，职责归位与partial保持唯一状态权威，不按来源建立第二产品程序集；
- `Utilities/`：具有明确具体功能职责、按需直接使用的便利工具（多数 Extensions、FSM、Grid、Json、Table、ZipTool、ColorSelector）；仓库根 `Tools/` 则承载 SDK 与开发工具；
- `Systems/`：Action、Audio、Save、Http 与 Resource 按领域归口，共用整体 Runtime，不新增系统程序集；Resource 仅归口外边界，内部结构与生命周期契约不变；

`Systems/Http/` 仍使用公开 namespace `YLFramework.Services.Http`；目录名变化不改公开身份。`YLFrameworkRoot.cs`、主 asmdef、`csc.rsp` 和许可汇总属于根级身份或装配入口，可留在 Runtime 根部。通用工具只按用途直接消费，不新建系统管理器或中立内核副本。

Tidy守卫分别验证完整Foundation和Foundation+Core闭包：Core递归包含自身全部C#文件，允许Foundation与真实UGUI、Json.NET，不再放行XFABManager。两闭包在Editor和Windows64 Development profile先验证合法消费者，再以相同参考/define验证Foundation→Core、Core→Audio的限定名/别名等违规符号不可解析。参考按真实程序集身份及编译图选择，Json.NET分别使用Editor/AOT插件，固定产品当前两类输入宏；伪路径、预编译YLFramework/测试/optional DLL及错profile插件被拒绝。

Foundation的Unity挂载/销毁适配直接使用原生API，不为复用上层GetOrAdd/Destroy便利扩展新增底层helper。历史namespace `YLFramework.Core.Pool/Singleton/IOC` 不改变实际Foundation所有权；Systems/Utilities按具体能力合法协作，ColorSelector→Core仍被允许，Table 不参与正式资源所有权。该守卫不替代动态字符串/序列化/生命周期评审，也不等于全平台、SDK、Player构建或最终独立载体验收。测试入口为 `Tests/Foundation/EditMode/FoundationBoundaryTests.cs` 与 `Tests/Core/EditMode/CoreArchitectureBoundaryTests.cs`；Tidy历史记录见 [Tidy方案](../../../.kilo/plans/xf-yl-tidy-architecture.md)，随后 Core/Audio 正式接入及旧栈退役以[阶段 8 Plan](../../../.kilo/plans/xf-yl-8-resource.md)为准。

目录分类只是组织方式，不等于新的程序集边界；namespace不强制与目录一一对应。阶段 7.5 将取色控件、渲染及窗口统一归 Utilities/UI/ColorSelector，定位为与 Json、FSM 同类的按需便利工具。工具层直接依赖 Core Panel 的加载/输入策略；IUIInputGate 及自定义门禁入口退出，不向 Foundation 增加 UI 合同。正式Prefab、取色类型的 namespace/API/序列化字段/GUID 保持，引用已退出接口的客户端须重编译。旧样例及其程序集已退役，Samples导出根保留。

### Core 职责归口

| Core Runtime 子目录 | 职责 |
|---|---|
| `Modules/` | Module / SingletonModule / ModuleManager；原主文件集中状态和生命周期，Models/Controllers/Views/Panels partial 仅组织入口，`Requests/` 为启动请求 |
| `MVC/Models/`、`Controllers/`、`Views/` | 各自的正式类型及唯一索引 Manager；ViewManager 的 Registration/Loading/Queries partial 保持同一会话，`Views/Parameters/` 为 LoadViewArgs |
| `Events/` | CoreEvents / EventManager 领域事件入口，不增加另一份总线 |
| `UI/Windows/`、`Roots/`、`Input/`、`Tips/` | 窗口/加载请求、根配置、输入策略与Tips；PanelManager Loading/Sorting/Input partial共用原状态。取色便利工具完整归 Utilities/UI/ColorSelector，原namespace不改 |
| `MVC/Views/Components/` | CloseView；TimerObject转Foundation/Timing/Components，物理/动画Listener转Foundation/Events/Unity，原namespace/GUID/字段保持 |
| `Attributes/`、`Analyzers/` | 正式 marker 与唯一编译器 DLL，属于 Runtime 主体输入作用域，marker namespace 保持 |
| `Resources/YLFramework/Core/` | 既有 Canvas/EventSystem；逻辑加载键 `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem` 不变 |

Core Editor 的 `DefaultResources/` 成对保留生成器与删除保护；`ModuleGeneration/` 为 Module 模板；`Settings/` 为项目设置；`Extensions/ScriptableObjectExtensions` 和 `Utilities/Preview` 保留当前资产生成/定位契约。通用序列化、Handles、资产命名、TreeView 状态与 SplitWindow 已分别归 `Editor/Utilities/{Serialization,Scene,Assets,TreeViews,Windows}/`，DisableEdit Drawer 归 `Editor/Foundation/Attributes/`。七文件仍使用 `YLFramework.Core.Editor` namespace、原 GUID 和同一 Editor 程序集；生成配置资产、ProjectSettings 持久路径及 Examples 输出不变。

上述目录承接既有结构整理成果，不重复历史迁移。五类产品程序集归属迁移时保留原脚本/存续资源GUID、namespace、公开API和序列化字段；该结构整理批未改Core资源后端。随后8.6a将ProjectName解释为正式Resource ProjectId并保留Prefab/Resources fallback与生命周期，8.6c解除主体XFAB依赖，详见阶段8 Plan。UI Toolkit测试用有上限的真实绑定状态等待，Drawer/容器实现不变；不把结构整理证据扩大为全量套件/全平台或阶段8、9交付。

主要模块间协作：Binding 依赖 Events（变更回调）与 Pool；FSM 依赖 Pool（池化复用）；Resource 复用 Runtime 基础工具。正式 UI 逻辑归 Core，复用唯一 Module/View 会话、Foundation Events/Update/Pool，并通过窄消费合同接入正式Resource；同程序集不代表恢复旧 UI Manager，Save 与 UI 职责相互独立。

### 成熟 Utilities 边界

| 工具 / namespace | 正式职责与协作 | 接入与所有权 |
|---|---|---|
| FSM / `YLFramework.FSM` | Runtime 轻量纯代码状态机，复用 Runtime Safe 池与 Foundation 池内核；与带完整 Editor 的大型可视化 XFFSM 分层共存 | `FSM<TStateId>.Allocate`，调用方驱动及回收；不依赖 Module / XFFSM / 统一 FSM Manager，Module 关闭不自动停止 |
| Grid / `YLFramework.Utilities.Grid` | Runtime 矩形 / 稀疏二维数据结构，无框架内依赖；不强制替代为其他 Foundation 容器 | 直接 `new NormalGrid<T>` / `DynamicGrid<T>`，调用方管理数据 |
| Json / `YLFramework.Utilities.Json` | Runtime JSONObject 树处理，无框架内依赖；与 Json.NET 的强类型序列化、Save 文件策略、HTTP 传输职责分离 | 直接创建 / 解析节点，不建设全局 JSON 服务 |
| Table / `YLFramework.Utilities.Table` | Runtime 通用表与多值索引；TableIndex 复用 Foundation 的 List / Dictionary 池，不接管正式Resource索引 | 调用方维护索引及 Clear / Dispose；不取得数据项 / 资源的释放权 |
| ZipTool / `YLFramework.Utilities.ZipTool` | Runtime主体内的BCL-only源码闭包；原独立产品asmdef已退役，仍用全源码无Engine探针核验 | 常规使用ZipUtility/GZipUtility，多次读取以using管理ZipArchiveReader；不建设压缩Manager |
| ColorSelector / `YLFramework.Core` | 四个取色类型统一归 Utilities/UI/ColorSelector，依赖 Core Panel 策略的便利工具，不是中立基础机制 | 窗口由 Module/WindowCatalog 按需打开；独立控件无 Panel 时不强制创建 owner；无自定义门禁接口或新 Manager |

FSM/Grid/Json/Table/ZipTool 无需组件挂载或公共Root；ColorSelector 是 UGUI 工具，窗口按需复用 Core 的 Module/Panel。现有YLFramework.Core.Pool namespace不表示依赖Core领域，机制闭包见[Pool](Modules/Pool.md)。类型/namespace/GUID保持，五个产品程序集身份迁移是阶段 7 的明确成本；JSONObject/SharpZipLib来源与完整MIT随Runtime导出，分发限制见下文。

XF 承接源码的完整 MIT 许可只保留于 [`Assets/YLFramework/LICENSE.md`](../LICENSE.md)，保留上游版权与全文；默认导出在六个目录之外显式加入该文件。其他来源的完整许可和 Runtime/THIRD-PARTY-NOTICES.md 的来源说明保持。

Core 中非领域扩展已退出，通用能力统一归 `YLFramework.Utilities.Extensions` / Runtime；`StringTools` 归 `YLFramework.Utilities`。只有两份相同的 Unity fake-null 算法收敛为 Object 入口，递归 `SetLayer` 并入现有 GameObject 工具；单对象设层、不同概率离散化、逆插值与分量 / 方向算法仍按实际契约保留，不机械改名或合并。详见 [Extensions](Modules/Extensions.md)。展示元数据与必要 Drawer 保持 5.1 的 Foundation / Editor 边界。

Timing 位于 `Runtime/Foundation/Timing/`，namespace 为 `YLFramework.Timing`。默认服务与显式手动 TimerScheduler 共用唯一秒/帧内核；内部 TimerDriver 为独立 DDOL 薄驱动，不借用场景组件或创建 Root/UI。Scope、ReusableTimer、GO binding 与 TimerWait 不增加调度器；Async中的CoroutineObservation仅观察借用producer。Core.TimerObject保留组件身份与事件字段，作为Timing/Components适配消费公共服务。旧TimerManager与Tools/Timing已退出新YL；原XF包不修改。详见 [Timing](Modules/Timing.md)。

### HTTP / AsyncOperation 边界

HTTP 正式位于 `Runtime/Systems/Http/`，namespace 为 `YLFramework.Services.Http`，`HttpManager` / `SendWebRequest` / `HttpRequestStatus` 归 `YLFramework.Runtime`。使用 asmdef 的消费者显式引用 Runtime，不再为 HTTP 带入 Core / XFAB；旧 Core 类型退出且不留转发壳，调用方更新 using / 完整类型名。所有入口与 Abort 仅在 Unity 主线程使用，无需挂载组件：GET、POST Form/JSON、PUT/DELETE 返回可 yield 的请求。默认 timeout 为 5 秒，可在前置配置中覆盖；显式 headers 可覆盖默认 Content-Type。GET 合并原 query、保留 fragment，新增键/值分别编码，null 值为空，数值按 InvariantCulture；Form 为 UTF-8 URL 编码，JSON 字典使用 Json.NET，字符串原样发送。

`PreWebRequest` / `PostWebRequest` 现在为可 `+=` / `-=` 的静态事件，订阅者在自身结束时解除订阅，HTTP 会话重置会清空订阅。前置事件同步配置原生请求，异常提交 Failed 并保留原始 Exception；后置事件只观察原生完成（含协议/网络错误），不在构造失败、取消或退出时调用。后置事件的原生对象仅在回调内借读，返回后释放，不能持有、重发或主动 Dispose；若前置配置替换 Handler，原先的 Handler 和回调返回时附着的新 Handler 均交由 HTTP 清理，配置过程中自行创建但从未交付/又自行替换的中间对象仍由配置者负责。

`SendWebRequest.Status` 为独立稳定终态（Pending / Succeeded / Failed / Canceled / Invalidated），`error`、`NativeResult`、`ResponseCode`、`ResponseHeaders`、`Text` / `Data` 在完成通知时脱离已释放的原生对象读取；协议失败也保留响应正文。`Abort` 幂等，只停止本请求独占的传输，不撤销服务器已执行的副作用。启动拒绝、退出及会话失效都释放 HTTP 自有请求/Handler；Module.Close 不清全局 HTTP。后置观察和完成通知异常报告但不反写终态，资源清理不依赖通知成功。

Foundation `CustomAsyncOperation` 仅提供等待和历史通知：legacy `completed` 先于 `AddCompleteEvent`，多播抛错会中断后续通知；完成后新增 `AddCompleteEvent` 立即同步调用（异常仍传播给订阅者）。基类重复完成和完成后 Abort 仍可更新 error，重复 Abort 仍运行 hook，不作为领域稳定终态或物理取消权威。HTTP 自管终态；View/Panel、Audio、Resource 保留各自 owner、逻辑取消、租约与共享 producer 所有权，不新增公共任务系统。

自有旧 `YLFramework.Core.LogUtil` 已退役，不保留转发壳或第二套文件 Logger；其全局异常订阅、后台写盘、三天文件清理及退出回调随实现退出。历史日志文件、原 XF LoggerSystem 和原游戏日志路径不修改。正式文件日志仍是长期需求，由后续 LoggerSystem 专题融合，本次退出不表示已有正式替代品。

上述外围闭包已通过阶段 5.5 的真实同域双 Play、Windows64/Mono Development Player 回环与退出、代表性 Core UI / Audio 消费者、原游戏 Editor / Player 非干扰及实际导出门禁。HTTP 独占原生清理不扩展为领域统一取消；`Application.quitting` 与后续场景销毁是不同验证相位。测试隔离原 Logger 不表示原 XF 生态已支持无 Domain Reload；临时 XFAB 依赖、文件 Logger、跨平台 / AOT 与最终独立承载仍按后续专题处理。范围、原生取消诊断与恢复证据见 [运行时外围方案的 5.5 记录](../../../.kilo/plans/xf-yl-5-runtime.md)。

### Timer / Action / Audio 边界

| 模块 | 职责 | 名字与清理范围 |
|---|---|---|
| Timer | 独立延迟、有限 / 无限周期到期回调 | `TimerScope` 管理命名与匿名任务；普通所有者用 CancelAll / Dispose 清理 |
| ActionKit | 逐帧进度、插值、条件与 Sequence / Parallel / Repeat 组合 | 命名根动作复用对象 GameObject、当前场景或全局执行器；`ActionHandle` 仅控制原动作执行租约 |
| AudioKit | Music / Voice / Sound、显式命名同步/异步取得与逻辑取消、代次安全 AudioHandle、随机/空间音效、去重与可选 Mixer | 唯一音频播放生命周期；内建 Action 桥接。直接 Clip / Mixer 借用，命名 Clip 由 ResourceAudioClipProvider 包装独立 AssetLease 保活/受控闲置缓存；Resource 是唯一资产/Bundle 释放权威，XFAB 适配仅借用 |

Timer 与 Action 使用独立名称索引，不增加共享 scope、全局任务表或第三套执行器。Timing 不提供逐帧 progress；Action 的 Delay 不改写为 Timer 包装，Audio 播放链也不强制绑定 Timer。两者的首帧、循环、异常和时钟行为并不完全等价。

Action 的 namespace 为 `YLFramework.Actions`（复数，避免遮蔽 `System.Action`），Audio 为 `YLFramework.Services.Audio`。二者共用 Runtime，但保留自身领域生命周期与行为适配；Audio 直接复用 Foundation 的 Binding、Events、Pool 和 Host，不再复制通用包装或加载层。Help 为 `YLFramework.Editor.Help`，提供方为 `YLFramework.Editor.Actions` / `.Audio`。

Audio 的可选 Mixer 不新增程序集或宿主：`AudioOutputRoute` 表达继承分类默认、指定组与 Direct，`Mixer/AudioMixerConfig` 只保存外部资产引用，Apply/Clear 仅更新运行时默认组，不写 PlayerPrefs。`AudioMixerControl` 独立显式控制参数与 Snapshot；Settings 音量仍只控制 Source，不自动绑定 Mixer 音量或 Ducking。薄 `AudioMixerController` 的 Awake 仅路由，禁用/销毁不清全局配置；清路由不停止单次覆盖，Shutdown 仍停播并清理自有请求，两者均不撤销原生参数控制或 Snapshot 状态。详见 [Audio 的可选 Mixer](Modules/Audio.md#可选-mixer)。

正式 `AudioClipLibrary` 只负责具名素材的均匀/加权选择；`AudioGroupPlayer` 是显式命名通道的组件 owner，直接 Clip、命名异步和借源均提交同一 AudioManager/Voice。其自动播放默认 Manual，FirstStart/EveryEnable 显式配置，不改原游戏组件身份或六个设置键。通道 `VolumeChanged/MutedChanged` 先应用原请求再通知，同值不通知，重入/退出不会向新身份投递旧值；监听异常继续通知仍属原代次的其他监听后聚合报告。正式命名后端为 ResourceAudioClipProvider；主体 XFAB 过渡依赖与旧 YL 资源栈已退出，原 XF Package 不修改。详见 [素材组与组件](Modules/Audio.md#素材组与组件)。

## 主要入口

| 入口 | 说明 |
|---|---|
| [`Runtime/Systems/Action/Core/ActionKit.cs`](../Runtime/Systems/Action/Core/ActionKit.cs) | 动作工厂与全局消息入口；详见 [Action](Modules/Action.md) |
| [`Runtime/Systems/Audio/AudioKit.cs`](../Runtime/Systems/Audio/AudioKit.cs) | 直接 Clip / 显式命名音频播放、安全句柄、请求取消与资源租约、设置、查询及可选 Mixer；显式参数/Snapshot 控制见 `Mixer/AudioMixerControl`，详见 [Audio](Modules/Audio.md) |
| [`Runtime/YLFrameworkRoot.cs`](../Runtime/YLFrameworkRoot.cs) | 框架根单例（`MonoSingleton<YLFrameworkRoot>`，`DefaultExecutionOrder(-20)`），负责框架根对象的 DontDestroyOnLoad |
| [`Runtime/Core/Modules/Module.cs`](../Runtime/Core/Modules/Module.cs) | 唯一业务 owner，保留模块启动/关闭、MVC 与 View 接入协议 |
| [`Runtime/Core/MVC/Views/View.cs`](../Runtime/Core/MVC/Views/View.cs) | 正式 View 会话与表现生命周期 |
| [`Runtime/Core/UI/Windows/WindowCatalog.cs`](../Runtime/Core/UI/Windows/WindowCatalog.cs) | 仅保存窗口定义；同步/异步入口复用 Module 的活跃会话与 pending 请求 |
| [`Runtime/Core/UI/Roots/UIRootContext.cs`](../Runtime/Core/UI/Roots/UIRootContext.cs) | 根/层/遮罩配置，无独立窗口 owner 或加载缓存 |
| [`Runtime/Systems/Resource/ResourceSystem.cs`](../Runtime/Systems/Resource/ResourceSystem.cs) | 唯一显式初始化/关闭的资源权威，统一资产/实例/Scene pin，供 Core/Audio 消费 |
| [`Runtime/Foundation/Timing/Timer.cs`](../Runtime/Foundation/Timing/Timer.cs) | 静态默认计时入口，秒/帧/日期投递；控制与清理不创建宿主 |
| [`Runtime/Foundation/Timing/TimerScope.cs`](../Runtime/Foundation/Timing/TimerScope.cs) | 独立命名与任务所有权范围；CancelAll 可复用，Dispose 永久关闭范围 |
| [`Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs`](../Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs) | 为 Mono 单例指定运行时层级路径（如 `GameObjectPool`）；正式 UI 根不是 Mono 单例 |

框架预制体：`Prefabs/YLFramework.prefab` 保留公共框架根与原 UI 布局，UI 组件已迁为 `UIRootContext` 和 `UITipsPresenter`；`Prefabs/UITips.prefab` 为正式提示项，`Prefabs/ColorSelector.prefab` 为独立重建的取色工具，均使用 UI layer。Runtime 不反向引用 Core；适配与统一输入已完成，阶段 3 真实双 Play 和 Windows64/Mono Player 门禁已验证。IL2CPP、移动设备与最终独立承载不由这些结果替代，详见 [UI](Modules/UI.md)。

## Editor 工具

Resource Editor按Data/Build/Publishing/Service/CodeGeneration归组，使用正式Project/Catalog/BuildMap与安全发布、代码生成。Help归Editor主体的独立功能分区，Core/Action/Audio/Foundation-Tools提供方及菜单/窗口/持久配置身份保持。Zip源码以全量BCL-only编译探针验证，不再以旧Zip产品DLL维持独立性。

- `Editor/Foundation/`：Attributes 的 DisableEdit Drawer、Serialization 的原生字典 Drawer 与 Events 的 EventListener Inspector；`Editor/Core/` 保持原默认资源、生成、设置与预览契约。
- `Editor/Systems/`：与 Runtime Systems 对应的领域 Editor；`Editor/Utilities/` 承载 Serialization/Scene/Assets/TreeViews/Windows 通用支撑、菜单及 Foundation-Tools Help provider；`Editor/Distribution/` 承载框架导出入口。
- `Editor/Help/`：Editor主体中的共享帮助分区，额外Help asmdef已退役；保持提供方id、菜单与配置键，不机械拆入每个领域。
- 通用工具：`Editor/Utilities/Menus/ClearLog` / `OpenFolder`、`Editor/Distribution/ExportUnityPackage`，菜单 `YLFramework/Toolkit/...` 保持。
- 资源工具：`Editor/Systems/Resource/`，唯一菜单 `YLFramework/Resource/Workbench`，包含正式 ResourceProject/Group 选择、目录模拟、Builder、Publisher 与代码生成；不再提供旧 Importer 标记/子工程构建。详见 [Modules/Resource.md](Modules/Resource.md)。
- 帮助：YLFramework/帮助复用共享[Help](Modules/Help.md)。core入口仍为YLFramework/Core/帮助，foundation-tools仍为YLFramework/Toolkit/帮助，actionkit/audiokit及菜单保持；源码职责分离但共用Editor主体，不以独立DLL宣称单向编译隔离，只读索引不实例化宿主或运行示例。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。运行时 `AudioKitDebugWindow` 位于 Audio / Diagnostics，仅 Editor/Development Build 提供运行 GUI；诊断不为读取空状态创建 AudioManager、Source 或 Listener。

## XML 与分发

唯一核心配置 `Runtime/csc.rsp` 生成 `Library/YLFramework.Runtime.xml`，包含 Action、Observable、Audio 与桥接文档；DOTween 独立配置生成 `Library/YLFramework.Actions.DOTween.xml`。提供方按真实 Assembly 作为 `XmlPaths` 字典 key 去重，不把同一 Runtime 重复添加。`Library/` XML 是构建产物，不默认导出。

`AudioSourceIndependenceTests` 使用 AssemblyBuilder 直接编译正式 Audio 全目录及其必要自有源码依赖，排除预编译 YL/XF/XFAB 实现 DLL，分别验证 Editor 和 Windows64 Development 非 Editor 条件分支。该源码闭包只允许 Unity/BCL 与精确的 UGUI/Json.NET 引用，不再允许 Sirenix DLL。`FrameworkAssemblyIndependenceTests` 进一步核对两主体的实际编译图、Editor 已编译 AssemblyRef 闭包，并使用真实 profile 的合法引用重新编译完整 Runtime/Editor 源码；Editor 使用本次独立构建的 Runtime，不借旧产品 DLL。这些不是干净工程导入或真正 Player 构建/运行的替代证据。

Audio 的最终门禁分别保留真实同域双 Play、原游戏资源链非干扰、Source/DSP 输出与实际 Windows64/Mono Player 的证据，不能以源码闭包或构建成功互相替代。验收场景和 PCM 属于 Tests 的显式夹具，不改变唯一 Runtime Audio 权威、游戏持久资产或默认分发；系统端点出声与人工听感仍需单独确认，阶段状态以 [Audio 专项](../../../.kilo/plans/xf-yl-4-audio.md)为准。

自有YLFramework.Analyzers.dll仅一份，位于Runtime主体作用域的Runtime/Core/Analyzers；RoslynAnalyzer标签作用于主体及真实引用消费者，普通平台禁用，不作为Runtime precompiledReference或Player运行引用。最终Editor/Development Player的实际rsp已核对：Runtime/Editor、MainModule与自动引用主体的Assembly-CSharp-firstpass各一份输入；原XF/XFAB/XFFSM无自有分析器输入。七项Warning和真实正负见证见[Attributes](Modules/Attributes.md#core-使用诊断)。新Player仍含原MCP/NuGet的Microsoft.CodeAnalysis载荷，主体IL无其运行引用，不把它归因于本分析器或宣称整个工程无Roslyn。

默认 ExportRoots 仍为 `Editor`、`Plugins`、`Prefabs`、`Runtime`、`Samples`、`Documentation`，`ExportPackage` 使用 `Recurse` 而非 `IncludeDependencies`。Plugins 完整递归，包括商业插件、Demo 和辅助工具；内部交付不代表公开再分发许可已核查。正式 Editor/Help、Analyzer DLL/meta、Runtime 与资源/说明进入包；Tests、根 Integrations、游戏资源、Examples/YLFrameworkData、SDK 项目、NuGet 缓存和中间产物排除。现有 UGUI/Json.NET 外部依赖不自动携带，主体不要求 XFAB。回归真实解包核验全部递归资产内容及 GUID/meta，不宣称干净工程 import。入口见 [文档首页](README.md#帮助与导出)。

## 测试

`Tests/EditMode` 与 `Tests/PlayMode` 通过 asmdef 的 `UNITY_INCLUDE_TESTS` 约束，不进入正式构建；普通 Action / Audio / Help 测试共用 YL 测试程序集。DOTween 两个测试程序集独立，并同时要求 `DOTWEEN` + `UNITY_INCLUDE_TESTS`。API 示例全量编译与 core-only 的真实 AssemblyBuilder fixture 是独立验证，不替代全工程 Unity 编译或 Test Runner 门禁；导航见 [Action 测试](Modules/Action.md#测试与帮助)。
