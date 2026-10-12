# 架构总览

> [文档首页](README.md)

## 目录与程序集

| 目录 | 程序集（asmdef） | 职责 |
|---|---|---|
| `Runtime/`（除下列独立程序集目录） | `YLFramework.Runtime` | 运行时功能，含 Action / Observable / Audio / Audio-Action 桥接 |
| `Runtime/Shared/` | `YLFramework.Foundation.Runtime` | 公共 Pool、Events、Update、CoroutineStarter 与唯一 Timer 内核 |
| `Runtime/XFMain/` | `YLFramework.Core.Runtime` | Core 领域组件，TimerObject 消费 Foundation Timer |
| `Samples/ColorSelector/` | `YLFramework.Samples.ColorSelector` | 消费正式 Core UI 的演示，显式引用 Core / Foundation / UGUI |
| `Runtime/Tools/ZipTool/` | `YLFramework.Utilities.ZipTool` | Zip/GZip 工具与内置 SharpZipLib，不引用 UnityEngine |
| `Editor/`（除 `Help`、`XFMain`） | `YLFramework.Editor` | 普通编辑器工具、Action / Audio 帮助提供方，仅 Editor 平台 |
| `Editor/Help/` | `YLFramework.Editor.Help` | 独立 Editor-only 帮助核心，显式 references 为 `[]` |
| `Editor/XFMain/` | `YLFramework.Core.Editor` | Core 模块生成工具，显式引用 Core / XFAB / UGUI，仅 Editor 平台 |
| `Integrations/DOTween/Runtime/` | `YLFramework.Actions.DOTween` | 可选 Action 扩展，受 `DOTWEEN` 约束 |
| `Tests/EditMode/` | `YLFramework.Tests.EditMode` | 编辑器内单元测试 |
| `Tests/PlayMode/` | `YLFramework.Tests.PlayMode` | 运行时测试 |
| `Tests/Integrations/DOTween/EditMode/`、`PlayMode/` | `YLFramework.Actions.DOTween.Tests.EditMode`、`.PlayMode` | 独立可选集成测试 |

`Runtime`、普通 `Editor`、`ZipTool` 的 `autoReferenced` 为 true；Help、可选 DOTween 与测试程序集为 false。使用 asmdef 的业务代码仍须显式声明所需引用。

## 依赖方向

- `YLFramework.Foundation.Runtime` → 无其他框架显式引用
- `YLFramework.Core.Runtime` → Foundation、XFABManager、UnityEngine.UI
- `YLFramework.Runtime` → Foundation、`YLFramework.Utilities.ZipTool`、`UnityEngine.UI`
- `YLFramework.Editor` → Foundation、`YLFramework.Runtime`、`YLFramework.Editor.Help`、`UnityEngine.UI`
- `YLFramework.Core.Editor` → `YLFramework.Core.Runtime`、XFABManager、UnityEngine.UI
- `YLFramework.Editor.Help` → 无模块显式引用；Help 不反向依赖 Runtime 或提供方
- `YLFramework.Actions.DOTween` → `YLFramework.Runtime` + `DOTween.dll`
- `YLFramework.Tests.EditMode` → Runtime、Editor、Help、ZipTool、UI、TestRunner / NUnit
- `YLFramework.Tests.PlayMode` → Runtime、UI、TestRunner / NUnit

模块内协作：`Runtime/Audio/Actions/PlaySoundAction` → Action 契约 + Audio 播放入口，全部位于同一 Runtime，不是可拆卸的 optional Integration。BCL Observable 同样内建；DOTween 与可选 `Integrations/XFAB/Audio/` 各保留独立第三方程序集边界。正式 Audio Runtime 不反向引用 XFAB adapter；后者不自动注册或纳入默认导出。Runtime / 普通 Editor 不静态引用 DOTween wrapper 或供应商 DLL。

无循环依赖；Runtime 不引用 Editor 程序集。Runtime 中少量编辑器模拟代码（资源加载、AssetBundle 模拟、存档编辑器保护等）使用 `#if UNITY_EDITOR` 条件编译，玩家程序集不依赖 UnityEditor。

外部依赖：UGUI，以及通过插件自动引用使用的 Sirenix Odin（Inspector 标注）与 Newtonsoft.Json（存档与热更新序列化）等既有依赖；排除 DOTween 不意味着框架零外部依赖。

## Runtime 目录规则（Foundation + Tools + 大型模块）

Runtime 采用“Foundation + Tools + 一级大型模块”结构：

- `Foundation/`：其他框架模块经常建立在其之上的基础机制（Binding、Events、IOC、Pool、Singleton）；
- `Tools/`：具有明确具体功能职责、按需直接使用的通用工具（AssemblyTool、Attributes、Extensions、FSM、Grid、Json、Table、ZipTool）；
- Runtime 根部：承担完整框架级服务/系统职责的大型模块（Action、Audio、Resource、Save）；正式 UI 位于 Core 的 `XFMain/UI/`，复用 Module / Panel / View。

目录分类只是组织方式，不等于新的程序集边界；namespace 不强制与目录一一对应。模块内部按实际复杂度组织：小模块保持扁平，大模块可继续内部细分。`Samples/ColorSelector` 仅演示正式 `Runtime/XFMain/UI/Tools/` 工具；取色能力本身位于 Core Runtime。

主要模块间协作：Binding 依赖 Events（变更回调）与 Pool；FSM 依赖 Pool（池化复用）；Resource 复用 Runtime 基础工具。正式 UI 归 Core，复用 Module/View 的唯一会话与请求、Foundation Events/Update/Pool 及暂存的 XFAB 后端，不依赖整体 Runtime 的旧 UI Manager；Save 与 UI 相互独立。

Timing 位于 `Runtime/Shared/Timing/`，namespace 为 `YLFramework.Timing`，归 Foundation。默认服务与显式手动 TimerScheduler 共用唯一秒/帧内核；内部 TimerDriver 为独立 DDOL 薄驱动，不借用场景组件或创建 Root/UI。Scope、ReusableTimer、GO binding 与 TimerWait 不增加调度器；Foundation 的 CoroutineObservation 仅观察借用 producer。Core TimerObject 保留组件身份与事件字段，消费公共服务。旧 Core TimerManager 与 Tools/Timing 已退出新 YL；原 XF 包不修改。详见 [Modules/Timing.md](Modules/Timing.md)。

### Timer / Action / Audio 边界

| 模块 | 职责 | 名字与清理范围 |
|---|---|---|
| Timer | 独立延迟、有限 / 无限周期到期回调 | `TimerScope` 管理命名与匿名任务；普通所有者用 CancelAll / Dispose 清理 |
| ActionKit | 逐帧进度、插值、条件与 Sequence / Parallel / Repeat 组合 | 命名根动作复用对象 GameObject、当前场景或全局执行器；`ActionHandle` 仅控制原动作执行租约 |
| AudioKit | Music / Voice / Sound、显式命名同步/异步取得与逻辑取消、代次安全 AudioHandle、随机/空间音效、去重与可选 Mixer | 唯一音频播放生命周期；内建 Action 桥接。直接 Clip / Mixer 借用，命名 Clip 通过窄 ResLoader Provider 租约保活/受控闲置缓存；物理释放仍由后端 owner 管理，XFAB 适配仅借用 |

Timer 与 Action 使用独立名称索引，不增加共享 scope、全局任务表或第三套执行器。Timing 不提供逐帧 progress；Action 的 Delay 不改写为 Timer 包装，Audio 播放链也不强制绑定 Timer。两者的首帧、循环、异常和时钟行为并不完全等价。

Action 的 namespace 为 `YLFramework.Actions`（复数，避免遮蔽 `System.Action`），Audio 为 `YLFramework.Services.Audio`。二者共用 Runtime，但保留自身领域生命周期与行为适配；Audio 直接复用 Foundation 的 Binding、Events、Pool 和 Host，不再复制通用包装或加载层。Help 为 `YLFramework.Editor.Help`，提供方为 `YLFramework.Editor.Actions` / `.Audio`。

Audio 的可选 Mixer 不新增程序集或宿主：`AudioOutputRoute` 表达继承分类默认、指定组与 Direct，`Mixer/AudioMixerConfig` 只保存外部资产引用，Apply/Clear 仅更新运行时默认组，不写 PlayerPrefs。`AudioMixerControl` 独立显式控制参数与 Snapshot；Settings 音量仍只控制 Source，不自动绑定 Mixer 音量或 Ducking。薄 `AudioMixerController` 的 Awake 仅路由，禁用/销毁不清全局配置；清路由不停止单次覆盖，Shutdown 仍停播并清理自有请求，两者均不撤销原生参数控制或 Snapshot 状态。详见 [Audio 的可选 Mixer](Modules/Audio.md#可选-mixer)。

正式 `AudioClipLibrary` 只负责具名素材的均匀/加权选择；`AudioGroupPlayer` 是显式命名通道的组件 owner，直接 Clip、命名异步和借源均提交同一 AudioManager/Voice。其自动播放默认 Manual，FirstStart/EveryEnable 显式配置，不改原游戏组件身份或六个设置键。通道 `VolumeChanged/MutedChanged` 先应用原请求再通知，同值不通知，重入/退出不会向新身份投递旧值；监听异常继续通知仍属原代次的其他监听后聚合报告。自有 Core Audio 四份 Runtime、旧 Inspector 和重复驱动/卸载机制已退出；Core 保留其他模块的 XFAB 过渡依赖，原 XF Package 不修改。详见 [素材组与组件](Modules/Audio.md#素材组与组件)。

## 主要入口

| 入口 | 说明 |
|---|---|
| [`Runtime/Action/Core/ActionKit.cs`](../Runtime/Action/Core/ActionKit.cs) | 动作工厂与全局消息入口；详见 [Action](Modules/Action.md) |
| [`Runtime/Audio/AudioKit.cs`](../Runtime/Audio/AudioKit.cs) | 直接 Clip / 显式命名音频播放、安全句柄、请求取消与资源租约、设置、查询及可选 Mixer；显式参数/Snapshot 控制见 `Mixer/AudioMixerControl`，详见 [Audio](Modules/Audio.md) |
| [`Runtime/YLFrameworkRoot.cs`](../Runtime/YLFrameworkRoot.cs) | 框架根单例（`MonoSingleton<YLFrameworkRoot>`，`DefaultExecutionOrder(-20)`），负责框架根对象的 DontDestroyOnLoad |
| [`Runtime/XFMain/UI/WindowCatalog.cs`](../Runtime/XFMain/UI/WindowCatalog.cs) | 仅保存窗口定义；同步/异步入口复用 Module 的活跃会话与 pending 请求 |
| [`Runtime/XFMain/UI/UIRootContext.cs`](../Runtime/XFMain/UI/UIRootContext.cs) | 根/层/遮罩配置，无独立窗口 owner 或加载缓存 |
| [`Runtime/Resource/ResManager.cs`](../Runtime/Resource/ResManager.cs) | 资源服务入口（`Singleton<ResManager>`，`Init` / `InitAsync`） |
| [`Runtime/Shared/Timing/Timer.cs`](../Runtime/Shared/Timing/Timer.cs) | 静态默认计时入口，秒/帧/日期投递；控制与清理不创建宿主 |
| [`Runtime/Shared/Timing/TimerScope.cs`](../Runtime/Shared/Timing/TimerScope.cs) | 独立命名与任务所有权范围；CancelAll 可复用，Dispose 永久关闭范围 |
| [`Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs`](../Runtime/Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs) | 为 Mono 单例指定运行时层级路径（如 `GameObjectPool`）；正式 UI 根不是 Mono 单例 |

框架预制体：`Prefabs/YLFramework.prefab` 保留公共框架根与原 UI 布局，UI 组件已迁为 `UIRootContext` 和 `UITipsPresenter`；`Prefabs/UITips.prefab` 为正式提示项，`Prefabs/ColorSelector.prefab` 为独立重建的取色工具，均使用 UI layer。Runtime 不反向引用 Core；适配与统一输入已完成，阶段 3 真实双 Play 和 Windows64/Mono Player 门禁已验证。IL2CPP、移动设备与最终独立承载不由这些结果替代，详见 [UI](Modules/UI.md)。

## Editor 工具

- 通用工具：`ClearLog`、`OpenFolder`、`ExportUnityPackage`，菜单 `YLFramework/Toolkit/...`。
- 资源工具：`Editor/Resource/`，菜单 `YLFramework/Service/ResService`，包含 AssetBundle 模拟模式、资源标记、打包（`AssetBundleExporter` / `BuildScript`）与资源数据代码生成（`ResDataCodeGenerator`）。详见 [Modules/Resource.md](Modules/Resource.md)。
- 帮助：`YLFramework/帮助`、`YLFramework/Action/帮助`、`YLFramework/Audio/帮助`；独立 [Help 核心](Modules/Help.md) 承载普通 Editor 中的提供方，稳定 id 为 `actionkit` / `audiokit`。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。运行时 `AudioKitDebugWindow` 位于 Audio / Diagnostics，仅 Editor/Development Build 提供运行 GUI；诊断不为读取空状态创建 AudioManager、Source 或 Listener。

## XML 与分发

唯一核心配置 `Runtime/csc.rsp` 生成 `Library/YLFramework.Runtime.xml`，包含 Action、Observable、Audio 与桥接文档；DOTween 独立配置生成 `Library/YLFramework.Actions.DOTween.xml`。提供方按真实 Assembly 作为 `XmlPaths` 字典 key 去重，不把同一 Runtime 重复添加。`Library/` XML 是构建产物，不默认导出。

`AudioSourceIndependenceTests` 使用 AssemblyBuilder 直接编译正式 Audio 全目录及其必要自有源码依赖，排除预编译 YL/XF/XFAB 实现 DLL，分别验证 Editor 和 Windows64 Development 非 Editor 条件分支。该源码闭包仍需要 Unity/BCL、UGUI 和 Root 既有 Odin Attributes；不是整个尚在融合中的工程独立验收，也不替代真正 Player 构建/运行。

默认 ExportRoots 仍为 `Editor`、`Plugins`、`Prefabs`、`Runtime`、`Samples`、`Documentation`，`ExportPackage` 使用 `Recurse` 而非 `IncludeDependencies`。普通模块、帮助源码与屏幕资源包含在默认包；Tests、根目录 DOTween 扩展及包外供应商 DLL 排除。可选源码另行引入依赖，不复制 Core / Vendor。导出回归仅验证当前工程实际包清单与 GUID / meta 字节，不宣称干净工程 import。入口见 [文档首页](README.md#帮助与导出)。

## 测试

`Tests/EditMode` 与 `Tests/PlayMode` 通过 asmdef 的 `UNITY_INCLUDE_TESTS` 约束，不进入正式构建；普通 Action / Audio / Help 测试共用 YL 测试程序集。DOTween 两个测试程序集独立，并同时要求 `DOTWEEN` + `UNITY_INCLUDE_TESTS`。API 示例全量编译与 core-only 的真实 AssemblyBuilder fixture 是独立验证，不替代全工程 Unity 编译或 Test Runner 门禁；导航见 [Action 测试](Modules/Action.md#测试与帮助)。
