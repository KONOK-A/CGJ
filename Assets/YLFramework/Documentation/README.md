# YLFramework 文档

YLFramework 是当前工程内自研的 Unity 游戏框架，面向游戏业务层提供对象池、单例、事件、数据绑定、状态机、IOC、动作编排、音频、资源管理、存档、UI 等能力。

> 当前框架仍在持续开发和完善中，部分模块缺少测试或存在明确的待完善项，详见各模块文档的“当前状态”。
> 本文档是导航性质的内部文档，不是完整 API Reference。

## 目录结构（当前）

框架根目录为 `Assets/YLFramework/`：

| 目录 | 说明 |
|---|---|
| [`Runtime/`](../Runtime/) | 运行时框架代码（Foundation / Tools / 大型模块） |
| [`Editor/`](../Editor/) | 编辑器工具、模块帮助提供方与独立 Help 核心 |
| [`Integrations/DOTween/`](../Integrations/DOTween/) | 可选 Action DOTween 扩展，不在默认导出范围 |
| [`Tests/`](../Tests/) | EditMode / PlayMode 测试 |
| [`Plugins/`](../Plugins/) | 第三方插件（Sirenix Odin、Editor Enhancers） |
| [`Prefabs/`](../Prefabs/) | 自有 UI 模板与正式工具（`YLFramework.prefab`、`UITips.prefab`、`ColorSelector.prefab`） |
| [`Samples/`](../Samples/) | 示例（`ColorSelector`） |
| `Documentation/` | 本文档目录 |

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

### Tools（通用工具）

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Extensions / StringTools | 常用 Unity / System / 几何扩展及字节 / 进度显示 | 正式归 Runtime，共用掩码判断归 Foundation | [Modules/Extensions.md](Modules/Extensions.md) |
| FSM | 程序员直接使用的轻量纯代码状态机，不替代可视化 XFFSM | 成熟实现保留 | [Modules/FSM.md](Modules/FSM.md) |
| Grid | 通用二维数据结构（矩形 / 稀疏坐标） | 成熟实现保留 | [Modules/Grid.md](Modules/Grid.md) |
| Json | JSON 树解析与构建（JSONObject），不统一 Save / HTTP 序列化内核 | 成熟实现保留 | [Modules/Json.md](Modules/Json.md) |
| Table | 通用表与多值索引，Resource 的直接支撑 | 成熟实现保留 | [Modules/Table.md](Modules/Table.md) |
| Timing | Foundation 唯一秒/帧内核、精确句柄、Scope/复用、GO 绑定与终态等待 | 2.4-C 实施及专项验证完成，待验收与 Git 收口；2.4 IN PROGRESS | [Modules/Timing.md](Modules/Timing.md) |
| ZipTool | 独立 Zip / GZip 能力（内置 SharpZipLib，无 UnityEngine 依赖） | 成熟实现保留 | [Modules/ZipTool.md](Modules/ZipTool.md) |

### Runtime 大型模块

| 模块 | 作用 | 状态 | 文档 |
|---|---|---|---|
| Action | 动作组合、命名执行、安全句柄、屏幕过渡与内建 BCL Observable | 已归入 Runtime | [Modules/Action.md](Modules/Action.md) |
| Audio | Clip-only Music / Voice / Sound、代次安全句柄、Sound 并发上限与开始回调、双轨音乐 Crossfade、手动/自动首尾淡变、均匀/加权随机、空间播放、可选 Unity Mixer 与 Action 桥接 | 已归入 Runtime（验证以执行记录为准） | [Modules/Audio.md](Modules/Audio.md) · [Sound 并发上限](Modules/Audio.md#sound-并发上限) · [音乐 Crossfade](Modules/Audio.md#音乐-crossfade) · [自动首尾淡变](Modules/Audio.md#自动首尾淡变) |
| Resource | 资源加载与 AssetBundle 管理（ResManager / ResLoader） | 基本完成（热更新待完善） | [Modules/Resource.md](Modules/Resource.md) |
| Save | 存档槽与全局设置读写（原子写入 / 备份恢复 / 元数据） | 已完成（含专项测试） | [Modules/Save.md](Modules/Save.md) |
| HTTP | 轻量 GET / Form / JSON / PUT / DELETE、稳定终态与独占传输取消 | 正式归 Runtime，复用 Foundation 等待 / 通知 | [HTTP / AsyncOperation 边界](Architecture.md#http--asyncoperation-边界) |
| UI | Core Panel/View、窗口目录、根/层/模态、适配/输入、Tips 与 Runtime ColorSelector | 阶段 3 技术门禁完成，待用户最终验收与 Git 收口 | [Modules/UI.md](Modules/UI.md) |

### 其他内容

- [Help](Modules/Help.md)：独立 Editor-only 帮助核心；普通 Editor 承载 Action、Audio、Foundation/Tools，Core Editor 单向接入 Core Provider。
- [`Samples/ColorSelector/`](../Samples/ColorSelector/)：正式 `YLFramework.Core.ColorSelectorWindow` 的消费者演示，含已绑定的 Sample Prefab，不保存另一份工具实现。

## 架构与依赖

程序集划分、依赖方向与目录分层说明见 [Architecture.md](Architecture.md)。

FSM / Grid / Json / Table 位于 `YLFramework.Runtime`，按需直接使用；ZipTool 保持独立程序集。工具不强制依赖 Module、统一 Manager 或公共 Root；各模块文档列明正式 namespace、入口与实际存在的测试范围，成熟实现保留不等于当前仓库具备其历史完整测试套件。

Timing 的 Timer 负责到期回调；逐帧进度、插值与组合流程由 ActionKit 的 Delay / Lerp / Repeat 等动作承担。两边各自管理名字与生命周期，`TimerScope` 不与 `ActionHandle` 共用任务表。Action / Audio 位于 `Runtime/Action/`、`Runtime/Audio/`，共用 `YLFramework.Runtime`；公共入口仍为 `ActionKit` / `AudioKit`。

Audio 不配置 Mixer 时保留原功能；分类默认与单次覆盖/Direct 路由均可选，配置资产与薄 UnityEvent 组件不新增播放宿主或资源加载。Settings 音量不自动写 Mixer；路由、原生参数控制及资源所有权各自显式管理，见 [Audio](Modules/Audio.md#可选-mixer)。

## 帮助与导出

- 菜单：`YLFramework/帮助`、`YLFramework/Core/帮助`、`YLFramework/Toolkit/帮助`、`YLFramework/Action/帮助`、`YLFramework/Audio/帮助`。Core 与 Foundation/Tools 只提供必要接入指南、精选 API 和详细文档导航，不执行示例或创建宿主。
- 模块工具：`YLFramework/Action/创建屏幕过渡资源`、`YLFramework/Audio/Debug`。
- `YLFramework/Toolkit/导出框架包` 的默认 ExportRoots 为 `Editor`、`Plugins`、`Prefabs`、`Runtime`、`Samples`、`Documentation`，使用 `Recurse`，不使用 `IncludeDependencies`。普通模块源码、资源与文档随这些根目录导出。
- `Plugins/` 完整递归，保留商业插件、Demo 与辅助工具，不裁剪或重配导入平台。这是内部交付决策，公开再分发许可仍需后续核查。
- 包含正式 Editor/Help 与唯一 `Runtime/XFMain/Analyzers/YLFramework.Analyzers.dll` 及 meta。七项分析器默认 Warning，DLL 仅作为 Core 作用域的编译器输入，不是 Runtime 引用；配置与源码重建见 [Attributes](Modules/Attributes.md#core-使用诊断)。
- 默认不导出 `Tests/`、根 `Integrations/`、`Examples/`、游戏资产/Bundle、`Assets/YLFrameworkData`、SDK 工具项目、NuGet 缓存、临时探针、中间产物及包外 DOTween。可选扩展源码须另行满足依赖；Analyzer 重建源码保留在仓库 `Tools/YLFramework.Analyzers`，不随 UnityPackage 提供 SDK 项目。
- 框架仍沿用 UGUI、Odin、Newtonsoft 等既有依赖，Core 保留过渡 XFAB，不是零依赖安装包。XML 位于 `Library/`，不默认导出；缺 XML 的 Help 仍保留真实声明。
- 导出回归真实解包并检查递归资产内容、GUID / meta、完整 Plugins、Analyzer 唯一身份与禁用平台及排除项，不等于干净工程导入。实际 Player 门禁与阶段状态单独记录于 [阶段 6 方案](../../../.kilo/plans/xf-yl-6-editor.md#65-帮助导出与阶段收口)。

## 测试

- EditMode：`Tests/EditMode/`，覆盖 Foundation / Tools / Resource，以及 `Action/`、`Audio/`、`Help/` 的行为、API、XML 与帮助边界。
- PlayMode：`Tests/PlayMode/`，覆盖 MonoBehaviour 单例、Timing 时钟与宿主，以及 `Action/`、`Audio/` 的真实帧执行和播放生命周期。
- DOTween：`Tests/Integrations/DOTween/` 下两个独立测试程序集，受 `DOTWEEN` + `UNITY_INCLUDE_TESTS` 约束。

导出的框架包不包含 `Tests/`，测试仅在源码仓库内运行。

运行方式：Unity Test Runner，或使用 `-runTests` 批处理参数。
