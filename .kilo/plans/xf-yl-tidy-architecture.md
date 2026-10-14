# XF × YL: Tidy 架构分层审计与治理总体方案

> 状态: 原规划轮为只读研究；Tidy-1 当前原生方案及交付见 §14、§15，初版历史见 §13。Tidy-2 已按用户授权完成，结果与限制见 §16；Tidy-3 和阶段 8 未启动。
> Tidy-2 实施仅迁移七个 Editor 源码/meta、更新直接测试与导航，不修改用户配置、原 Package、游戏或正式资源；用户现另行授权本批 Git 交付，范围见 §17。§15 的授权与记录对应已交付的 Tidy-1。
> 实施者仅在用户另行授权后按批推进，可使用 `executing-plans` 或 `subagent-driven-development`；每批先核对现场，完成其验收后停止，不自动进入阶段 8。

**Goal:** 使 Foundation 真正不依赖上层，保持 Core 的结构核心职责，以最小必要治理支撑 Systems / Utilities 有序协作和后续资源融合。

**Architecture:** 保留 `YLFramework.Runtime` / `YLFramework.Editor` 两主体。四层按实际声明、方法调用、领域职责和所有权判定，不按 namespace 前缀或程序集数量推定；Systems 与 Utilities 是平行的上层职责分类，允许合理直接协作。

**Tech Stack:** Unity `2022.3.62f3c1`、UGUI、BCL、既有 Json.NET；独立 SDK 使用现有 .NET / Roslyn 工具链，本方案不更新任何依赖。

**Spec:** 用户本轮正式四层架构与 Tidy 只读研究授权；导航为 `Assets/YLFramework/Documentation/{IntegrationPlan,Architecture}.md`、阶段 7 最终方案和 `.kilo/plans/xf-yl-7-structure-redesign.md` 的 §16、§17.12、§18。较早章节的七程序集、独立 Kernel、XFMain 均不是当前事实。

## 1. 审核摘要

**推荐三个实施批次，不重做阶段 7。**

| 优先级 / 分类 | 审计结论 | 推荐处置 |
|---|---|---|
| 高 / A1 | Foundation 在 5 个文件、9 处调用中使用 Utilities 的 Unity 扩展；扩展又调用 Foundation 的 LayerMaskUtility，形成同一 Runtime 内的双向依赖 | Tidy-1: Foundation 直接使用 Unity 原生 API；便利扩展留 Utilities，不为复用几行上层便利操作增加底层 helper |
| 高 / C1 | 现有 Foundation 边界探针仅覆盖原 Kernel 的 49 文件，不能代表完整 Foundation；两个主体 asmdef 也无法约束四层 | Tidy-1 扩为完整 Foundation 闭包；Tidy-3 补 Core 闭包和有效正反例，不建立架构测试平台 |
| 中 / B1 | Editor/Core 仍包含基础 Attribute Drawer 和无 Core 领域依赖的通用序列化、Handles、窗口、TreeView、资产命名支撑 | Tidy-2 仅将 7 个明确文件按职责迁路径，保留 FullName、程序集、字段、GUID 和行为 |
| 中 / C2 | Audio 的具体 ResLoader provider 直接调用 Resource 的 internal ServiceManager | 真实跨系统内部耦合，但不违反 Foundation/Core 方向；留阶段 8 随资源所有权及 provider 边界处理 |
| 中 / B2、C3 | 通用 Editor 设置基类固定 Core 持久路径；部分导航与 7.5 Git 状态文字过期 | 本轮不改；实施收口时修正准确导航，设置路径/API 通用化另审 |
| D | Core 的 Module/MVC/View/Panel 权威、领域系统、独立工具、取色对 Panel 的直接依赖、可选集成叶子边总体合理 | 保持，不为图形纯洁新增接口、适配层、全局 Manager 或兼容壳 |

推荐默认不改变公开身份和重要行为。高风险 namespace/AQN 统一、产品 asmdef 重拆、资源后端/租约/ProjectName 变化、热更新及旧组件退役均不属于 Tidy 默认范围。

## 2. 基线与证据边界

### 2.1 Git 与保护现场

| 项目 | 本轮实查 |
|---|---|
| 工作目录 / 分支 | `G:/Unity/Projects/CGJ2024` / `integration/xf-yl` |
| HEAD / 本地 upstream / 实时远端 | 均为 `63e152e0bbc54f8397707c3eb407cc2eb1e3ee17`；实时只读 `git ls-remote` 核对，没有 fetch |
| 当前提交 | `refactor(tools): 完成阶段7.5 取色工具归位、IUIInputGate 退出与 XF 许可集中` |
| 阶段 7 里程碑 | 实时远端 `milestone/7-structure` 解引用为 `134936b3adad03ce0d4d59c867f1d852925faa59`；阶段 7 分支为 `0982bacaafe9b76ad471bcb5686cc7fd0ff605c5` |
| 暂存区 | 初始 `git diff --cached --stat` 为空 |
| 用户已有修改 | `.gitignore`、`.kilo/kilo.jsonc`、`ProjectSettings/Packages/com.unity.probuilder/Settings.json`，全部保护 |
| 并行工作区 / 配置 | 不访问、修改或清理既有 test-worktree，不更改权限、忽略规则、AGENTS 或工具配置 |

`git branch -vv` 曾被现有权限拒绝，没有尝试绕过或改变权限。分支、HEAD/upstream、远端由其他已获准只读命令独立确认。文档首段和结构方案 §18 仍写 7.5 未提交，已被上述 Git 事实覆盖；本轮不修改这些历史/长期文档。

### 2.2 环境与验证口径

- 已读取项目 AGENTS、IntegrationPlan、Architecture、阶段 7 总体方案及最终技术验收记录。
- ProjectVersion 为 `2022.3.62f3c1`；manifest 声明 UGUI `2.0.0` / URP `16.0.6`，磁盘 PackageCache 存在 UGUI `1.0.0` / URP `14.0.12`。阶段 7 最终记录已确认其当时活跃版本；本轮未连接 Editor 重新查询，不把缓存存在冒充当前活跃解析结果。
- 锁文件包含 Json.NET `3.2.1`，由 XFAB 传递引入。Foundation 未发现 Json.NET 依赖，Core 启动表、Save、HTTP、Resource 热更则确有消费者；不能统一禁止合理的第三方依赖。
- 本轮执行的是源码审计、消费者/条件编译/asmdef/测试契约核对及 Git 基线核验。没有 Unity 刷新、编译、EditMode/PlayMode、SDK 构建、导出或 Player 验证。
- 阶段 7 / 7.5 已有 SDK、实际 Unity 编译、导出、Player、GUID/身份记录只作未变基线，不重复执行，也不作为未来 Tidy 修改已通过的证据。

### 2.3 审计覆盖与限制

五个只读研究域覆盖完整 Foundation 功能族、Core 全部 partial 与请求/UI、五个 Systems、全部自有 Utilities、Editor/Help/测试/SDK/集成边界。Foundation 的 123 个 C# 文件逐文件读取；Utilities 的 31 个 Extensions 类以及 FSM/Grid/Json/Table/取色实现逐文件核对。SharpZipLib 以全源码引用扫描、封装和代表实现为主，不逐行重审第三方压缩算法；历史测试仅读取对当前架构决策有意义的部分。

重要结论按真实声明位置、继承、字段、成员调用、扩展解析及编译条件复核。无可用语义检索/调用图工具时使用精确检索和源码追踪，没有新建索引或审计程序。没有生成整库符号依赖矩阵；“未发现”不等同于证明任意反射、字符串配置、仓库外消费者和未来平台都不存在。

以下简称: `R`=`Assets/YLFramework/Runtime/`，`E`=`Assets/YLFramework/Editor/`，`T`=`Assets/YLFramework/Tests/`。行号对应上述研究基线。

## 3. 当前真实架构

### 3.1 物理结构与编译图

```text
Assets/YLFramework/
  Runtime/                         YLFramework.Runtime
    Foundation/                    Async/Attributes/Binding/Events/IOC/Pool/
                                   Serialization/Singleton/Timing/Update/Utilities
    Core/                          Modules/MVC/Events/UI/Attributes/Analyzers/Resources
    Systems/                       Action/Audio/Http/Resource/Save
    Utilities/                     Extensions/FSM/Grid/Json/Table/ZipTool/UI/ColorSelector
    YLFrameworkRoot.cs             公共层级入口，非全部服务管理器
  Editor/                          YLFramework.Editor，Editor-only
    Foundation/Core/Systems/Utilities/Help/Distribution
  Integrations/                    可选 DOTween、XFAB Audio，各自独立 asmdef
  Tests/                           原测试/夹具/Acceptance 编译所有者保留
  Prefabs/Plugins/Samples/Documentation/
  LICENSE.md                       集中完整 XF MIT

Tools/YLFramework.Analyzers/        独立 SDK；不属于产品 Runtime/Utilities
```

箭头表示“使用方依赖被使用方”，不是初始化顺序:

```text
YLFramework.Runtime --> XFABManager + UnityEngine.UI + 实际 BCL/Unity 模块
                     + 自动可见的 Json.NET 等既有预编译依赖
YLFramework.Editor  --> YLFramework.Runtime + XFABManager + UnityEngine.UI
YLFramework.Actions.DOTween --> Runtime + DOTween.dll        [DOTWEEN，可选]
YLFramework.Audio.XFAB      --> Runtime + XFABManager        [YL_AUDIO_XFAB，可选]
Project.Game.MainModule     --> Runtime(公开 Audio API) + 原 XF/XFAB/XFFSM
```

证据为两个主体 asmdef 的实际 references/platform/overrideReferences。Runtime `autoReferenced=true`、`overrideReferences=false`，不能将显式 references 两项解释成“只有两个外部依赖”。Editor `includePlatforms=[Editor]`；Runtime 不反向引用 Editor。两个主体之外的测试、可选集成和第三方 asmdef 不在主体数量约束内。

### 3.2 实际逻辑依赖图

```text
Foundation:
  Singleton/session --> BCL/Unity
  Pool --> Singleton/session
  Events --> Pool/Singleton
  Binding --> Events/Pool/session
  Timing、Update、Async --> 各自必要的上述机制
  IOC、Serialization、Attributes、AssemblyTool/LayerMaskUtility --> BCL/Unity
  Singleton / Events Unity适配 --> Utilities Unity扩展         [A1，需修]

Core:
  Module/MVC <--> View <--> Panel/UI                           [同一事务内聚]
      --> Foundation Events/Pool/Async/Update/AssemblyTool/ApplicationTool/session
      --> XFAB 加载/实例池/启动表/场景                          [阶段8例外]
      --> UGUI + Json.NET(启动Model表)

上层:
  Action --> Foundation + YLFrameworkRoot
  Audio --> Foundation + YLFrameworkRoot
  Audio/Actions --> Action
  Audio/ResLoaderAudioClipProvider --> Resource公开API + internal ServiceManager [C2]
  Resource --> Foundation + Utilities(Extensions/Table/ZipTool)
  Http --> Foundation Async/session + Json.NET + UnityWebRequest
  Save --> Json.NET + BCL文件系统/必要Unity值类型
  FSM / Table --> Foundation Pool
  通用 Extensions --> Foundation LayerMaskUtility             [允许；与A1叠加成环]
  Grid / JSONObject / ZipTool --> 各自 BCL/必要 Unity 能力
  ColorSelector --> Core Panel/View 参数 + UGUI                 [已确认合理]
```

未发现 Core→Systems/Utilities 的具体实现依赖；已确认的 Utilities→Core 取色消费是允许边，不与该结论冲突。Audio→Action、Audio→Resource、Resource→Utilities 均无已确认的返回边。不能因此宣称任意运行时事件/反射依赖均已形式化证明无环。

### 3.3 Namespace / API / 目录不一一对应

| 实际职责 | 现有公开身份 | 治理规则 |
|---|---|---|
| Foundation Pool/Singleton/IOC | `YLFramework.Core.Pool` / `.Singleton` / `.IOC` | 声明位于 Foundation，不是 Core 领域依赖 |
| Foundation Timing/事件组件 | `YLFramework.Timing` / `YLFramework.Events`；TimerObject 和三旧 Listener 保留 `YLFramework.Core` | 保留组件 FullName、字段、GUID，不靠改名制造分层 |
| Core 结构 | `YLFramework.Core`、`.RoslynAnalyzers` | 继续作为唯一 MVC/View/UI 类型与 marker 身份 |
| Systems | `YLFramework.Actions`、`YLFramework.Services.{Audio,Resource,Save,Http}` | 目录 Systems 不要求 namespace 增加 Systems |
| Utilities | `YLFramework.Utilities.*`、`YLFramework.FSM`；取色为 `YLFramework.Core` | 取色的公开身份不等同于 Core 内部实现 |
| 公共 Root | `YLFramework.YLFrameworkRoot` | 逻辑属Foundation层级支撑，保留根级公开入口路径，不成立第五层 |
| 通用 Editor 旧身份 | `YLFramework.Core.Editor.*` | 纯目录归位不改变 FullName/AQN/程序集 |

## 4. 分层源码审计

### 4.1 Foundation

| 功能族 | 实际职责与主要关系 | 判断 |
|---|---|---|
| Async | CustomAsyncOperation 提供等待/通知，CoroutineStarter 自有协程宿主，CoroutineObservation 观察借用 producer | 底层机制；不统一领域终态、取消或资源 ownership |
| Events | 唯一 EventBus/channel 算法，精确 token、scope、注销绑定、EasyEvent 和独立全局上下文、Unity/UGUI/物理适配 | 合理；事件挂载对上层扩展的使用属 A1 |
| Pool | ObjectPool/集合内核；Safe 包装；GameObjectPool 的实例桶与归还/清理 | 合理；GO 池不拥有 Prefab/AB 租约，不能下沉资源加载系统 |
| Binding | 变更通知、集合/偏好键/Unity 绑定，消费 Events/Pool/session | 基础共享能力，不成为 Audio/UI 状态 owner |
| Singleton/session | 普通/Mono/Prefab/Host 创建、场景收养、退出屏障、会话重置 | 基础生命周期支撑；三个旧单例文件的扩展调用属 A1 |
| IOC | 容器、注册/解析、注入、生命周期策略，BCL 自包含 | 即使当前产品未接入仍保留；不为了统一入口强接 Core/Root |
| Timing | 唯一秒/帧 scheduler、默认 driver、scope、wait、GO binding、TimerObject | 底层计时；不承载 Action 组合或 Audio 播放 |
| Update | 唯一公共三相位 channel，Core/Action 消费精确订阅 | 合理，不恢复旧独立转发器 |
| Serialization | SerializedDictionary 的唯一容器、raw/运行投影 | Foundation；Drawer、业务容器及测试消费；Save本体没有该类型依赖 |
| Attributes | 展示元数据及 Editor 条件方法形状 | Editor 分支必须保持保护；不把 Drawer 混入 Player |
| Utilities | ApplicationTool、AssemblyTool、LayerMaskUtility | 中立底层；不是 Runtime/Utilities 的工具全集 |

未确认 Foundation 对 Module/View/Panel、领域 Resource/Audio/Action/Save/HTTP 或原 XFAB 的真实类型依赖。`EventListener.cs` / `EventListenerExtension.cs` 使用 EventSystems 是必要 Unity 适配，完整 Foundation 闭包必须精确允许 UGUI，而不是把它判成上层框架依赖。

### 4.2 Core

| 责任区 | 实际结构 / 协作 | 是否应治理 |
|---|---|---|
| Modules / MVC | Module 唯一 owner，Model/Controller/View 索引及公开入口；各 partial 共享原状态与关闭管线 | 保持唯一类型和事务，不增加通用 SystemManager |
| View 请求 / 注册 | 归属、绑定、参数接管、pending 代次、逻辑取消、迟到隔离、实例复用 | 保持；物理加载/归还的 XFAB 部分留阶段 8 |
| Panel / UI | Panel 继承 View，PanelManager 排序/输入/loading 共用状态，UIRoot/Tips/Input/适配属于表现结构 | 合理内聚，不强拆 MVC/View/UI 依赖；UIRoot与公共YLFrameworkRoot不是同一责任 |
| Core Events | CoreEvents 为领域事件身份；EventManager 是 EventBus.Default 的薄入口 | 不移成第二总线，不因底层总线通用而撤销 Core 事件入口 |
| Attributes / Analyzer | marker 为真实 Core 使用约束，DLL 为编译期输入而非 Runtime 服务 | 保持身份、作用域与构建来源，不新增架构规则 DLL |
| 资源消费 | 命名 View/Panel、启动 Model 表与默认 Scene 使用原 XFAB | 明确例外；不得提前替换后端或改变 ProjectName |

代表证据: `Core/Events/EventManager.cs:15-27` 不保存独立事件表；`Modules/Requests/StartUpModuleRequest.cs:31,118-179,198-220` 继承 Foundation 请求基类并调用 XFAB，使用 Json.NET 反序列化 Model；`ViewManager.Loading.cs` / `LoadViewRequest.cs` 沿真实加载链管理 Core 会话。`View.cs:149`、`PanelManager.Loading.cs:101`、`PanelManager.Input.cs:36` 消费 Foundation.ApplicationTool 的播放门禁。源扫描与关键调用核对均未发现 Core 通过上层工具获取底层能力的必要反向边。

### 4.3 Systems 与所有权

| 系统 | 领域完整性 / 依赖 | 所有权边界 |
|---|---|---|
| Action | 动作工厂、组合、执行租约、命名控制、scene/global/object 执行器、Observable/Coroutine/Task 适配与屏幕过渡 | 动作/执行器清理自有工作，观察外部 Task 不取得其取消权；Delay 不机械改成 Timer 包装 |
| Audio | 唯一播放内核、Source/handle/session、命名请求/租约、组件、Mixer、素材选择；消费 Foundation、Action、Resource | clip/mixer 直传为借用，命名取得由 provider 持有/归还；Audio 不关闭 Resource 服务 |
| Resource | ResKey/ResLoader/Res/IRes、服务表、加载队列、依赖计数、Resources/AB/图像实现及 Editor 构建/生成 | Loader/Res/后端分别管理依赖与物理资源；Table/池只支撑数据及实例，不取得领域释放权 |
| Http | 请求创建、独占传输、稳定终态/响应快照、handler 接管/释放、会话退出 | 每笔 transport 自有；Module.Close 不清全局 HTTP，不把共享资源 producer 的物理取消交给请求基类 |
| Save | 存档槽/索引/用户对象缓存、文件原子更新与备份、版本拒绝、JSON设置与元数据策略 | 自有文件/缓存，非基础容器；改变格式/根路径/静态缓存生命周期须独立行为专题 |

证据: `Action/Core/ActionKit.cs:20-23,33-35,123-136`；`Audio/ResLoaderAudioClipProvider.cs:8-10,19-27,43-54,57-99`；`Resource/Framework/ServiceManager.cs:14-17,29-73,129-168`；`Http/HttpManager.cs:18-68,128-161`；`Save/SaveManager.cs:26-78,138-179`。

Save 的四个实现文件没有 Foundation/Core/System 类型消费边。`SerializedDictionarySaveTests` 将 Foundation 容器作为用户数据交给通用 Save API，证明两能力可组合，不代表 Save内部依赖该容器。系统身份不要求必须使用 Foundation 或 Core。

Resource 热更源码不是已完成更新系统: `HotUpdate/HotUpdateManager.cs:46,83-105` 的空版本路径/无回调分支直接阻断组合检查，类 XML 已如实注明。属于阶段 8/后续热更专题，不在 Tidy 修成新 HTTP/任务平台，也不能因 Resource 被列为 Systems 就宣称每个子能力成熟可用。

### 4.4 Utilities

| 工具 | 真实框架出边 / 消费者 | 结论 |
|---|---|---|
| Extensions / StringTools | Unity/BCL 算法及便利调用；Component/GO→LayerMaskUtility；Resource 消费 InstantiateWithParent/GetOrAdd；Foundation 的 9 处逆向调用是例外 | 算法与链式 API 留 Utilities；简单底层操作直接使用原生 API，不为便利复用下沉封装或整体搬扩展库 |
| FSM | SafeObjectPool/IOfPool/IPoolType 全部声明在 Foundation | 合理向下；调用者驱动/回收，不建 FSMManager 或强接 Module |
| Grid | 独立二维结构，必要 System/Unity 值类型 | 保持，不强制依赖 Core |
| JSONObject | 独立 JSON 树及解析/枚举，与 Json.NET 强类型序列化职责不同 | 不合并成公共 JSON 服务 |
| Table / TableIndex | 消费 ListPool/DictionaryPool；Resource 的 ResTable/AssetDataTable 使用索引 | 平行协作，不取得资源释放权 |
| ZipTool | 既有纯 BCL 源码闭包；Resource ZipFileHelper 使用内置 ZipFile/ZipEntry 读取 APK | 合理；保留低层类型与许可，不为当前无消费者删除封装 API |
| ColorSelector 四类型 | 控件缓存最近活跃 Panel，读取 `IsLoaded && !IsInputBlocked`；窗口继承 Panel，消费 LoadViewArgs | 7.5 已确认的便利工具→Core 关系，保留，不恢复 IUIInputGate 或无 Core 闭包 |

`FSM.cs` / `TableIndex.cs` 的 `YLFramework.Core.Pool` using 不构成 Core 领域依赖。取色源码位于 Utilities、公开 namespace 为 Core 也不构成 Core 反向引用工具。Utilities 当前无需要为了“通用”引入整个资源系统的事实边。

## 5. 重要问题与推荐取舍

### A1. Foundation 对 Utilities 的真实反向依赖

| Foundation 调用点 | 实际被调用定义 | 真实语义 |
|---|---|---|
| `Singleton/Mono/MonoSingleton.cs:50,61` | `Utilities/Extensions/Unity/ComponentExtension.cs:535` | 非当前实例 Dispose / 应用退出销毁 GameObject |
| `Singleton/Mono/PersistentMonoSingleton.cs:83` | 同上 | 后登记重复实例销毁 |
| `Singleton/Asset/PrefabSingletonProperty.cs:110` | 同上 | 已释放实例销毁并保留同帧隔离身份 |
| `PrefabSingletonProperty.cs:142,147` | `ObjectExtension.cs:18,103` | 克隆 Prefab / 缺目标组件时清理克隆 |
| `Events/Unity/EventListenerExtension.cs:26` | `ComponentExtension.cs:601` → `GameObjectExtension.cs:461` | 自动获取/添加监听组件 |
| `Events/Subscriptions/UnRegisterExtension.cs:27,56` | `GameObjectExtension.cs:461`，直接 GameObject 重载 | 自动挂载销毁/停用注销触发器 |

这是 9 个 Foundation 直接调用点；不将 Utilities 内部链或测试调用重复计入。环的另一侧为 `ComponentExtension.cs:583` / `GameObjectExtension.cs:68` → `Foundation/Utilities/LayerMaskUtility.cs:46`。

**违反原则:** Foundation 不得依赖上层便利库。**影响:** 公共单例/订阅清理被上层扩展组织绑住，同 asmdef 掩盖环，未来局部编译/改动易双向牵连。当前源码没有证明运行故障，不把架构违规冒充现有游戏 Bug。

| 候选 | 优点 | 代价 / 推荐 |
|---|---|---|
| 整个 Object/Component/GameObjectExtension 上提 | 公开身份可保持，无新 helper | 约 1,400 行便利 API 随 9 处需要进入底层；还须处理 5 处对 VectorExtension.X/Y/Z 的传递上引。范围偏大，不推荐 |
| 同名 partial 跨 Foundation/Utilities | 能保持方法 DeclaringType | 类型归属跨层，新增永久先例，守卫/维护复杂，不推荐 |
| 方法迁至新公开基础类 | 边界清晰 | 扩展声明身份、显式静态调用/二进制消费者变化，须重大 API 审批，默认不选 |
| 允许 Utilities“基础工具箱”例外 | 产品零改动 | 违反用户本轮明确规则；把工具塞入闭包只证明整团可编译，不能证明 Foundation 无上引，否决 |
| 唯一 internal 原语 + 真实便利 API 消费 | 层向下、实现唯一、公开身份不变 | 初版选择，现已撤销。当前仅几行原生操作，没有独立状态或复杂策略，不值得为消除局部重复增加永久封装 |
| **Foundation 直接使用原生 API** | 无上引、无新 helper；便利 API 及其公开身份仍留 Utilities | **用户批准的当前方案**。简单 GetComponent/AddComponent/Destroy 判断可在消费处直接表达 |

Foundation 使用原生 `GetComponent<T>()`，以 Unity fake-null 判断决定是否 `AddComponent<T>()`；销毁使用原生 `Object.Destroy`，保留必要的存活判断、帧末生效和销毁整个 GO 的区别。Prefab 克隆继续直接 `Object.Instantiate(prefab)`。不新增 `UnityObjectUtility` 或替代包装类，也不改成另一套底层 extension。

Utilities 原 GetOrAdd、DestroySelfGracefully、DestroyGameObjGracefully 及 Instantiate 便利 API 保留原声明和实现。这些有真实客户端价值的便利 API 留在上层；Foundation 不为复用它们而增加底层代码。少量原生操作的局部重复不等于建立第二套事件、池、单例或资源管理权威。

最终范围为 A1 五个 Foundation 文件及直接测试/闭包，不新增产品类型。不改变公开 FullName/AQN/程序集、序列化字段、既有 GUID、Root 或资源所有权。不能改变 fake-null、原生失败、帧末销毁、扩展返回原对象或销毁组件/整个 GO 的区别。

### B1. Editor 通用支撑误留 Core

真实证据: `E/Core/Inspectors/DisableEditDrawer.cs:7` 只消费 Foundation.DisableEdit；`SerializedObjectExtensions.cs:18-32`、`SerializedPropertyExtensions.cs:20,107,339-351`、`HandlesExtensions.cs:18-45` 只实现通用 Unity Editor 操作；`AssetNameTools.cs:18-45`、`SplitWindow.cs:8,66-103`、`TreeViewStateGroup.cs:16-49` 不依赖 Module/MVC/Panel。

前者是明确 Foundation Editor 适配；其余为通用 Editor 便利能力。当前产品消费者少不构成删除依据。主要真实消费者包含 `CoreSerializedPropertyTests` 的反射取方法和 `DisableEditDrawerTests`；外部消费者未知，FullName/字段必须保持。

推荐 Tidy-2 纯路径归位，成本限于 7 文件/meta、目录身份及导航/路径断言，不修改算法或公开身份。保持它们在 `YLFramework.Editor`。保守候选是只登记不动，风险更低但长期错误归属继续诱导新公共工具进入 Core；本方案推荐趁同程序集低风险归位。不合并两个契约不同的资产命名实现。

### B2. 通用 Editor 设置与资源预览的不同边界

- `E/Core/Settings/ProjectSettingsScriptableObject.cs:13,82-95` 为通用公开基类，却固定 `ProjectSettings/YLFramework/Core/{FullName}.asset`，包含拒绝不兼容覆盖及 symlink 保护。移动目录不能解决存储语义。推荐保留路径/API，未来出现实际跨域使用时再审最小存储契约及迁移，不能复制第二套设置基类，也不能让新系统默默写入 Core。
- `E/Core/Extensions/ScriptableObjectExtensions.cs` 绑定既有生成目录与输出行为，不和纯序列化助手一起机械搬移/重命名。
- `E/Core/Utilities/Preview/EditorAssetTool.cs:20,56-83` 实际查询 XFABProject/XFABAssetBundle，不是中立基础工具。候选为仅迁 Resource Editor 路径或随 provider/资源资产定位接口一起治理；推荐留阶段 8，避免 Tidy 把资源依赖搬一下就宣称解除。

### C1. 架构门禁覆盖不足

`T/Foundation/EditMode/FoundationBoundaryTests.cs:59-69` 精确收录原 Kernel 49 文件，未包含 A1 的五个调用文件。`T/EditMode/Audio/AudioSourceIndependenceTests.cs:31-55` 则同时编译 Audio/Action/Resource 支撑和 Utilities 扩展，是 Audio 无 XF/XFAB 的能力证据，不是 Foundation 单层证据。

应保留身份/程序集/IVT 断言，但将“Foundation 无上引”的证明升级为完整源码集合和严格参考白名单。严禁通过补收 Utilities 源码、借预编译 Runtime.dll、忽略报错或排除问题文件把边界测绿。推荐完整 Foundation 和 Foundation+Core 两闭包，见 §8；无需新增产品 asmdef或架构 Analyzer。

### C2. Audio 的 Resource 内部耦合

`R/Systems/Audio/ResLoaderAudioClipProvider.cs:68,78` 使用 Resource internal `ServiceManager.Instance.GetRes(...)` 和 `StartCoroutine(...)`；后者声明在 `Systems/Resource/Framework/ServiceManager.cs:14-17`，持有资源表/任务队列。Provider 这样做是为了 Loader retain 之后再交付并避免在其通知栈中回收重用（provider:66-78,93-98）。

**不是层方向违规:** 两个 Systems 可以必要协作，provider 在消费方，播放与物理资源 owner 仍分开。**质量风险:** 对内部表与协程宿主的依赖使 Resource 重构会穿透 Audio。

候选是扩充 Resource 有意义的取得/进度/完成契约、把交付适配放在合理消费边界，或维持过渡实现；不推荐为隐藏 internal 使用复制加载器/调度器或只把代码搬到中立目录。推荐阶段 8 研究资源 producer/Loader/租约后统一处理，届时保留取消等待者、迟到归还、retain/回调顺序、原 owner 和最终物理释放契约。本轮不预定最终接口或 API。

根因接缝为 `ResLoader.cs:495-511,531` 的逐项完成注册可能先于本 Loader Retain；provider必须规避这一通知顺序，不能把调用改成表面更短的回调。ServiceManager 宿主为 DDOL，不把此耦合误诊为“换场景一定丢协程”。阶段 8 同步更新 AudioSourceIndependenceTests 的实际源表，并保留 AudioNamedResourcePlaybackTests 的真实 Resources引用归零/卸载链与 AudioHelpTests 的公开 provider / 隐藏 Resource内部面契约。

### C3. 其他有据质量债务，不扩大 Tidy

| 证据 | 风险 / 处置 |
|---|---|
| `R/Foundation/Singleton/Mono/MonoSingletonHost.cs:98-100` 自定义创建结果未经组件校验即访问 host | 基础契约诊断可改进，但当前 ScreenTransition 创建方已检查。登记为单例质量专题，不捆绑 A1 改错误/资源清理行为 |
| `E/Distribution/ExportUnityPackage.cs:51` 调用全局 SaveAssets | 可能落盘他人未保存的资产；不在 Plan 阶段执行。后续导出验证先核脏资产且不调用会自动全局保存的菜单；导出交互/保存策略修改单独批准 |
| `IntegrationPlan.md:3` / 结构方案 §18 的 7.5 未提交文字；`Architecture.md:14` 的 Zip 探针待 S6 | 被当前 Git/现有测试与最终记录覆盖；后续授权批次增量同步，不改写历史失败或过渡阶段记录 |
| `Documentation/Modules/Extensions.md` 的 UI/Pool 消费者描述与当前声明/调用不符 | 随 Tidy 收口改为实际消费者，不将无引用等同无通用价值 |
| `R/Systems/Action/Internal/AssemblyInfo.cs:3-8` 为整个Runtime提供六条IVT；`E/Systems/Action/ScreenTransitionResourceCreator.cs:48,92` 消费 internal ScreenTransitionCanvas；DOTween实现内部执行代次/池队列，测试也有真实消费者 | 保留exact friend。文件所在Action目录不使其变成Action专属属性；IVT不是Runtime反向依赖Editor，也不能用不收此文件的闭包推断真实可见性 |
| `SaveManager.cs:26,37-59` 静态索引/缓存/存储覆盖/格式设置没有会话复位；`Resource/Framework/ServiceManager.cs:22,29-41,144,263-265` 的静态初始化/退出标志未接公共会话重置 | 关闭Domain Reload的跨Play状态一致性风险有源码依据，未本轮复现；Save留独立持久状态专题，Resource归阶段8初始化/owner专题，不将缺少reset自动定为当前运行故障 |
| `Documentation/Modules/Save.md:79` 声称SaveManagerTests 55项，当前测试树未找到该类/文件，仅确认字典/Save组合测试 | 不能把文档测试数量当现有门禁或完整Save恢复验收；先定向确认历史/缺失测试，再在获授权说明维护中纠正，不因此删除能力或新建无关测试全集 |
| `R/Core/UI/Windows/PanelManager.cs:139-150,167` 公开返回实时可变UICanvas字典，并暴露可写通知委托 | 外部可绕过根注册不变量或替换观察者；是公共API质量风险，不是已证实第二owner。推荐保留Tidy契约，后续比较只读投影/受控根注册/event方案，须审外部消费者与行为，不能本轮静默改签名 |
| `R/Core/Modules/ModuleManager.cs:29-57` 仅靠Editor退出/进入钩子丢弃实例；View/Panel等另接公共session reset | 重置组织不对称，但当前退出路径有明确实现；先核真实退出/无Domain Reload复位，不因差异强合生命周期。留Core生命周期专题，收益是减少重复策略，风险是关闭顺序/旧owner引用变化 |
| `R/Core/Modules/Module.cs:100-101` 将PreloadConfigTables标过时，`StartUpModuleRequest.cs:125-126,186` 屏蔽该警告仍消费它 | 能力仍在正式启动路径，未定义替代契约；阶段8配表资源专题需明确保留/取消obsolete/替代及迁移，不能为消警告删除加载能力或提前换公共入口 |

其余未用using、内部历史转接、比较器/容器分配、逐帧适配等局部整洁/性能候选不进入默认批次。没有可复现缺陷或Profiler依据时不顺手清理，公共error-obsolete类型与旧组件也不因代码消费者少就删除。

## 6. 合理现状与保持项

1. **唯一机制不等于只有一个入口。** EventManager→EventBus.Default；TypeEventSystem 等 facade 拥有不同上下文；SafeObjectPool/集合池消费唯一 ObjectPool。它们不是因为名字多就产生重复权威。
2. **Core 内循环协作体现同一事务。** Panel 是 View，Module 是业务 owner；View/Panel manager 共用注册、加载、退出和输入状态。保持 OnClose→Controller→View→剩余 Model、启动失败收尾和请求代次，不能将 partial 当成可独立 owner。
3. **执行宿主按所有权保留。** Update/Coroutine/Timer 的 DDOL driver、Action scene/global/object 执行器、Audio Host、Resource ServiceManager 分别管自有工作。Root 不接管停止/释放，Timer/Action/Audio 不合成全局任务表。
4. **Root 是层级机制，不是服务定位器。** `R/YLFrameworkRoot.cs:9,14-26` 只有 MonoSingleton、DDOL 和 GetModuleRoot，逻辑归Foundation；根路径作为公开入口保留。实际类型消费者为 Action 两个 Host 与 AudioManager；Foundation目录当前没有 Root 类型引用。`GameObjectPool` 的 `YLFramework/Pool/GameObjectPool` 字符串与其共居是层级约定，根名/路径不能顺手改，Root也不得未来接管各领域服务。
5. **IOC 是可直接使用的底层能力。** 没有产品消费者不授权删除、搬上层、强接 Module 或建设总容器。
6. **成熟系统与工具按职责保持。** HTTP 两个文件仍是领域系统；轻量 FSM、Grid、JSONObject、Table、Zip 不因规模或无 Manager 被降级/删除。
7. **7.5 取色契约保持。** Utilities→Panel 的真实便利消费是用户明确选择，不能恢复 IUIInputGate、自定义门禁、独立控件 DLL 或无 Core 的四文件闭包要求。
8. **UGUI / BCL / UNITY_EDITOR 合理使用。** Runtime 的 Editor 使用按真实条件分支核对，Editor-only 主体和非 Editor 编译 profile 分别验证；不因目录名或 using 字面误判泄漏。
9. **不把阶段 8 过渡当重复资源权威已需立即重写。** Core 命名链走原 XFAB，Audio 默认 provider 走 YL Resource。当前两个地址/释放体系明确不同，未来须统一研究，但本轮不得改 ProjectName、实例池/Bundle/Scene 所有权。

## 7. Editor、测试、Help、Analyzer 与分发约束

- Editor 是主体内的功能分区，不是四套独立 DLL。Foundation Drawer、Systems Inspector、通用 Help、Distribution 各有实际职责；Editor 不反向成为 Runtime 的必要类型。
- HelpRegistry 显式接受 provider 数据，不扫描实例化系统。`core` / `foundation-tools` / `actionkit` / `audiokit`、菜单、MovedFrom、持久配置键与按真实 Assembly 去重的 XML 索引保持；同一 Runtime 不代表全部类型都属于 Foundation。
- 当前 AnalyzerSymbols 精确绑定 `YLFramework.Runtime` 的 View/marker/ObjectPool，覆盖 source 与 metadata；真实输入传播到 Runtime 消费者，包括 Editor、游戏及预定义消费者。保留唯一 `Core/Analyzers/YLFramework.Analyzers.dll`、GUID `471f81c4273ee66429677df44cbd6960`、RoslynAnalyzer 标签和普通平台禁用。
- DLL 所在 Core 目录只决定当前 Runtime 作用域，不表示 Foundation 运行时依赖 Core Analyzer。SDK 不随默认包，Runtime IL 不得引用 Analyzer/Editor/Roslyn；阶段 7 已有输入/Player 证据不因本轮 Plan 重跑。
- 不额外重建 SDK、不增加 YL0008、不把架构测试放入运行时 DLL。Tidy 无 marker/符号/程序集变更，默认沿用未受影响 SDK 证据；若实施超出此边界立即单独评审。
- 主测试与 Foundation/Core 专项保留原 asmdef 和直接夹具依赖。`UI.Acceptance` 是项目耦合 Player 验收承载，无普通测试 define，不能把它当干净独立框架测试或为了“无测试 DLL”擅自删除。
- DOTween/XFAB Audio 是可选叶子，保留开关、autoReferenced=false、公开消费接口和默认不导出；主体不反向引用 adapter 或供应商 DLL。内建 Audio/Actions 不变成可选 Integration。
- 默认导出六根加根级完整 XF LICENSE，Recurse、不加 IncludeDependencies；Samples 空根是明确保留项。Tests、根 Integrations、SDK、游戏/Bundle/缓存不自动进入包，商业插件公开再分发仍需后续许可确认。

## 8. 推荐目标与轻量约束

### 8.1 目标依赖规则

```text
Foundation --> BCL/必要 Unity 能力(含精确 UGUI 事件适配)
Core       --> Foundation + 必要 Unity/Json.NET
            + 已登记的原 XFAB 过渡例外(仅阶段8解除)
Systems    --> Foundation / 必要 Core / Utilities / 必要其他 Systems
Utilities  --> Foundation / 必要 Core或Systems，或完全独立
Editor     --> 对应Runtime能力 + UnityEditor + 合理Editor支撑
Integrations --> 被集成的公开能力 + 外部依赖，不形成主体返回边
```

跨 Systems/Utilities 的检查单位是“具体消费能力与 ownership”，不是零跨引用。下层需要公共机制时先确认已有 Foundation 实现；仅将确有基础职责和维护收益的机制收口到它，不能复制领域权威、把完整领域下沉或为一条调用建平台。不要为复用上层便利代码给底层增加封装；几行原生调用可以直接使用，不能把消除所有源码重复作为基础架构目标。

Foundation 直接使用原生 API，不新增便利 helper；Core Runtime 和 Systems 默认不迁路径、不改 API。Root不为目录整齐而搬动，作为唯一显式根路径基础入口参与下述源码守卫。Editor 目标映射如下:

| 原路径(均相对 E) | 目标路径 |
|---|---|
| `Core/Inspectors/DisableEditDrawer.cs` | `Foundation/Attributes/DisableEditDrawer.cs` |
| `Core/Extensions/SerializedObjectExtensions.cs` | `Utilities/Serialization/SerializedObjectExtensions.cs` |
| `Core/Extensions/SerializedPropertyExtensions.cs` | `Utilities/Serialization/SerializedPropertyExtensions.cs` |
| `Core/Extensions/HandlesExtensions.cs` | `Utilities/Scene/HandlesExtensions.cs` |
| `Core/Utilities/Assets/AssetNameTools.cs` | `Utilities/Assets/AssetNameTools.cs` |
| `Core/Utilities/TreeViews/TreeViewStateGroup.cs` | `Utilities/TreeViews/TreeViewStateGroup.cs` |
| `Core/Windows/SplitWindow.cs` | `Utilities/Windows/SplitWindow.cs` |

上述保持文件内容/meta、namespace `YLFramework.Core.Editor`、成员、程序集与持久字段。Settings、ScriptableObjectExtensions、XFAB EditorAssetTool、默认资源/模块生成/Help provider 原位。只创建有实际文件的新目录，不建立未来空槽位。

### 8.2 源码闭包守卫，而非巨大符号平台

优先复用现有 `FoundationBoundaryTests` / `AudioSourceIndependenceTests` 的 AssemblyBuilder 模式。两主体保持，不新增产品/测试 asmdef，不引入 Unity 内运行时 Roslyn。

| 闭包 | 源集合 | 精确可接受参考 |
|---|---|---|
| 完整 Foundation | 递归收集 `R/Foundation/**/*.cs` 加唯一显式入口 `R/YLFrameworkRoot.cs`；不附加 Utilities、Core、Systems | Unity 安装目录内可信 BCL/Engine；实际解析的 UGUI；Editor profile 可用真实 UnityEditor，Development profile 禁用 |
| Foundation+Core | 递归收集上述基础集合和 `R/Core/**/*.cs`，不附加 Systems/Utilities | 上述参考 + 实际 XFABManager + 实际 Json.NET；XFAB 为已登记阶段 8 例外，不放行 XFGameFramework/XFFSM/optional adapter |

两者均拒绝预编译 YLFramework.Runtime/Editor 或测试实现 DLL，拒绝按 DLL 名称前缀放行伪参考。UGUI/XFAB/Json.NET 用运行中编译图/已加载程序集的真实、唯一身份和 Location 确认，不猜缓存 DLL 路径，不把 `overrideReferences=false` 的自动参考照搬入探针。若外部必需传递类型未列出，先沿真实签名核验，报告理由；不扩大为整个原生态或预编译主体。

Foundation 必须单独先证明无上引；不能用 Foundation+Core 可编译覆盖这条禁令。Core 闭包同时允许 Foundation，这是结构关系，不强拆 Core 内 MVC/UI。Foundation 原 49 文件计数契约升级为“完整集合 + 严格参考”，不只是改 49 为 50，也不排除 Singleton/Events 适配。

守卫对源集合做归一化路径后的逐文件等价断言: Foundation目录集合与磁盘递归枚举完全一致，加且只加上已确认的Root入口；Core集合与磁盘Core枚举完全一致，合并后不得遗漏/重复；两者不含Systems/Utilities/Editor或其他任意根级源码。这个显式源身份例外仍要求Root只依赖基础能力，防止它将来长成上层服务集合。当前Core为48个C#文件仅作研究快照，测试不锁死该数目；`Core/Analyzers/` 的DLL不作为源码/Runtime参考。输出真实参考及 `UNITY_EDITOR`、`DEVELOPMENT_BUILD`、`ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER` define集合，输入宏按实际产品配置固定，不能默认空集合而默默检验另一分支；只有涉及其他编译分支的实际改动才追加对应profile。

闭包不包含 `Systems/Action/Internal/AssemblyInfo.cs` 的Runtime级IVT，且使用临时程序集身份；这是源码独立性的证明，不是正式程序集友元可见性、Analyzer注入、公开AQN或资源身份的替代验收。后几项沿用/定向运行各自现有门禁。

**必要正反例:**

1. Foundation 完整集合在 A1 未修时必须实际失败，定位到缺失上层扩展；Tidy-1 修复后 Editor / Windows64 Development 两 profile 通过。
2. 在独立临时 probe 向 Foundation 加 `global::YLFramework.Core.Module` 字段/继承或上层调用，必须失败；向 Core probe 加 `global::YLFramework.Services.Audio.AudioKit` 成员调用/`typeof`，必须失败。别名/限定名/泛型字段不应绕过真实编译解析。
3. 参考白名单拒绝预编译 Runtime/测试/optional DLL 和伪名/错误路径的 UGUI/XFAB/Json.NET；负例失败来自指定违规符号/参考，而不是丢必需 BCL 或基础基线本来就红。
4. 合法正例包括 `YLFramework.Core.Pool` 中的 Foundation 类型、Core 的真实 Json.NET/XFAB 过渡消费和 UGUI；证明不会按 namespace Core 误判。
5. 不新增“所有 Utilities 不得依赖 Core/Systems”规则。复用 7.5 ColorSelector 消费源码测试与已有 Audio/Resource/Table 编译消费者作为合法上层协作正例，至少在守卫批实际运行一项代表，不机械重跑整组行为测试。

探针仅写自己拥有的 Temp 目录，不把负例放产品树，不触发正式资产保存。每个 profile baseline/负例从同一参考配置派生，先确认正例绿再确认故意违规红；输出原始 CompilerMessage 文件/行/错误，零测试发现、取消、脏场景阻断不计通过。

同一源码集合、参考、define、Unity环境未变时复用该批已取得的正例证据。Tidy-2纯Editor路径不使Tidy-1的Foundation/Audio源码闭包自动失效；Tidy-3只跑新增/被修改的门禁和新Core闭包，不再次无差别执行上述全部probe。若守卫的收集/白名单/define算法本身改变，则对应正例必须重验，不能复用旧算法的绿态。

**边界:** 编译闭包可以约束实际静态符号/成员、泛型、属性与编译分支，但不能判断职责语义，不能完整识别 AssemblyTool 字符串、UnityEvent/Prefab、运行时事件循环；新动态调用和身份变化仍须定向评审。只覆盖项目实际两种代表 profile，不声称所有平台已证明。当前没有必要建立持久 dependency 数据库、每类规则文件、全域拓扑平台或全 Runtime 重编译测试。

## 9. 建议实施批次

以下为原规划的分批任务；当前完成情况见复选框及 §14、§16，后续批次仍须另行推进。默认顺序 Tidy-1 → Tidy-2 → Tidy-3。保护基线捕获、GUID核对和直接导航更新并入各自批次，不独立制造一个“准备阶段”。

### Tidy-1: Foundation 断环与完整底层证明

**目标:** A1 归零，Foundation 直接使用原生 API，不增加底层便利封装，所有公开便利契约保持。

**实际范围:** 修改 A1 五个 Foundation 文件，升级 `T/Foundation/EditMode/FoundationBoundaryTests.cs` 的完整闭包，保留直接行为测试。初版新增的 helper/meta 及其 Audio 源表、导出哨兵引用全部移除，三个 Utilities 扩展恢复原实现。现有源码哨兵路径不迁，不新增产品文件或类型。

- [x] 读取最新现场/同一已打开 Editor 状态；保留既有改动，捕获受影响公开成员/字段/GUID、正式资源与原包/游戏/配置保护事实。
- [x] 先补完整 Foundation 探针，取得未修改实现的有效红态；保持严格引用和 Runtime 唯一所有者断言。测试引用 UGUI可通过实际编译图/程序集路径获取，避免为了 `typeof(UI.Image)` 修改 Foundation 测试 asmdef。
- [x] 按 §5 A1 直接使用原生 API；五文件去掉上层扩展依赖，工具侧保留原便利实现。保留原 XML 公共契约和许可证来源说明，不新增 helper、public 类型或底层便利扩展。
- [x] 完整 Foundation 两 profile 实际绿；精确静态核对 Foundation 上层符号边消失，Utilities→LayerMask 仍是允许方向。初版证据见 §13，本次修订必须重新验证并记录在 §14。
- [x] 复用 Singleton/PrefabSingleton/事件生命周期和 `ExtensionLifecyclePlayModeTests` 的相关用例，覆盖 fake-null/帧末销毁、重复单例、Dispose后同帧隔离、缺组件克隆清理、GetOrAdd已有/缺失组件、OnDestroy/OnDisable注销。只有真实未覆盖关键行为才补测试，不建新 Mock/Fixture 体系。
- [x] Audio 源码闭包运行其两 profile与参考断言，确认直接原生调用随源编译而非借 Runtime DLL。只选一个受影响 Resource GetOrAdd/Root生命周期直接消费者，按实际影响核验，不重跑完整 Audio/Resource/MVC。初版证据见 §13，本次修订重新验证见 §14。
- [x] 检查 diff、新 meta、公开签名/既有GUID/资源 hash；本批只记录本文实施结果，未扩展到 Extensions/Architecture 等长期说明维护。

**保持:** 9 处清理/挂载行为、根名/DDOL、singleton/session/事件轮次、原生失败、不把 GameObject 销毁改成 Component 销毁、不改变资源释放和 Module.Close。

**成本 / 风险:** 小范围源码及一个闭包测试升级；低身份风险、中等生命周期回归风险，收益最高。失败后停止，不恢复旧 asmdef、放行 Utilities 或静默吞掉日志。

### Tidy-2: 明确 Editor 支撑纯目录归位

**目标:** 将 §8.1 七文件放到对应 Foundation / Utilities 功能目录，Core 不继续作为公共 Editor 工具集合。

**实际范围:** 七文件与原 meta；新目录由 Unity 正常生成身份；直接路径/职责测试、Help/Architecture 导航和导出哨兵。Runtime、SDK、ProjectSettings、正式资源内容不变。

- [x] 核对七文件内全部类型的继承/成员/字段/实际消费者和路径字符串；尤其 SplitWindow、TreeViewStateGroup/TreeViewStateInfo 的持久字段、SerializedPropertyExtensions 的反射方法。
- [x] 写路径/唯一类型归属红例，再用 AssetDatabase/API 移动并保 meta/GUID；全部文件内容逐字节保持。旧空目录/meta保留，不清无关目录。
- [x] 验证 Editor 唯一程序集、Editor-only、Runtime/Player无 Editor 产品依赖；保持所有 FullName/AQN、序列化字段、菜单/Help id及资源键，不新建公共“EditorCore”。
- [x] 受影响的 `CoreSerializedPropertyTests` / `DisableEditDrawerTests` 和直接身份/路径断言通过；Help无受改提供方。纯路径不要求完整 Core Editor 或重跑 SDK七规则；本次组合筛选实际扩大到104项，偏差与结果如§16，不继续重复运行。
- [x] 新旧源码集合除路径映射外等价；现有直接说明与导出哨兵已同步，不迁测试程序集、不迁持久设置。

**保持:** 同一 Editor assembly、算法、GUI布局/Undo/序列化/反射和既有数据。Settings、ScriptableObjectExtensions、EditorAssetTool 不参与本批。

**成本 / 风险:** 七文件可审查的纯路径批；低代码风险，有路径/MonoScript/Editor窗口身份风险，用字节/GUID/直接消费者门禁控制。

### Tidy-3: Core 闭包、正反例与总体收口

**目标:** 以少量可执行门禁约束两主体内部真实层级，证明合理协作不会误报。

**实际范围:** 增量扩展 Foundation 边界 fixture 的参考/源集合检查；可在 `T/Core/EditMode/` 新增一个 `CoreArchitectureBoundaryTests.cs` 与 meta承担完整 Foundation+Core 双profile闭包。优先已有私有测试方法，不建设跨测试程序集通用 harness，不改测试 asmdef/产品 Analyzer。

- [ ] 在 Tidy-1 基线上落实 §8.2 两类白名单与来源集合；Core 的 Json.NET 必需引用和 XFAB 过渡边明确保留，不能以缩水 Core 文件集代替完整 Core。
- [ ] 有效执行 Foundation→Core 和 Core→上层的负例、伪参考拒绝与合法历史 namespace/UGUI/XFAB/Json.NET正例；检查负例确由目标违规失败。
- [ ] 实际执行一项取色/Resource-Table 或已有跨系统编译消费者正例；不额外跑全部上层测试，只证明未错误施加平行层隔离规则。
- [ ] 完成源码依赖复核和公开API/字段/GUID保护核对。可选 Analyzer传播断言仅当输入相关代码受本批实际修改时增加，不为 Tidy再跑SDK123或13份rsp。
- [ ] 若 Tidy-1/Tidy-2 的新增源码/路径改变分发内容，只在最终源码状态执行一次现有六根真实导出/解包，验证七文件新路径、原完整许可/Plugins与排除项；不重复逐批导出。先检查脏资产，不调用全局SaveAssets菜单，测试仅清自己拥有的内容。
- [ ] 增量同步现有 Architecture/Extensions/相关Help与蓝图当前状态（须实施授权包含），说明完整Foundation规则、具体阶段8例外、namespace导航和未验证限制；本文记录批次真实结果，不再创建另一份审计报告。

**默认不做:** Player 重建、完整双Play、原游戏全流程、SDK重建、全量EditMode/PlayMode。只有出现真实生命周期/API/字段/编译输入变化，按 §10 提升必要门禁，不靠沿用历史结果遮盖新增风险。

**成本 / 风险:** 少量已有模式的测试及一次交付内容复核；实现规模受两闭包/正反例限制。主要风险是参考白名单配置错误、源码集合漏收与把基础闭包成功误当整工程独立，须按 §8.2 防止。

## 10. 风险、兼容与停止门禁

| 改动类型 | 必要验收 | 不默认重复 |
|---|---|---|
| Foundation 原生调用修正 | 实际 Unity 编译、完整 Foundation闭包、直接 Singleton/Events/Extensions生命周期及Audio源表 | SDK、Player、整框架/游戏 |
| 纯 Editor 目录归位 | 路径/GUID/meta/字节/FullName与直接反射/序列化消费者、Editor隔离 | Runtime行为全集、SDK、Player |
| 静态架构守卫 | baseline绿、指定违规红、限定名/字段等实际符号解析、合理上层协作正例 | 巨大调用图平台、全Runtime探针 |
| 意外公开契约/字段/资源键或生命周期变化 | 停止默认批；单独论证迁移，获批后定向 API/UnityEvent/反射/数据和真实运行 | 不得弱化原断言将意外变化当通过 |
| 源路径/新增源码影响交付 | 最终一次真实解包与内容/meta/排除项 | 每批重复导出或干净工程阶段9验收 |

主要兼容风险是目录敏感的外部工具与 Editor 持久对象。默认方案不改变 FullName、AQN、asmdef、成员/字段、序列化身份或正式资产内容；Foundation 原生调用调整不新增产品类型或运行时自动初始化/线程/反射。既有阶段 7 五类 AQN/旧二进制/Bundle 重编译限制仍保持，不能借 Tidy 宣称旧兼容问题消失。

如果发现用户修改与本批直接冲突、需要写原 Package/游戏/Scene/Prefab/Bundle/Packages/ProjectSettings、正式资源漂移、未知反射/字段身份、编译/门禁失败、第二实现/永久兼容壳或未授权 API变化，则停止并保留现场。禁止 reset/revert/stash/clean、自动保存用户场景、改保护基线、清 Library、弱化断言、跳过错误或扩大参考列表伪合规。

## 11. 阶段 8 与未来扩展约束

### 11.1 阶段 8 资源融合

1. 正式资源能力保持 `Systems/Resource` / `Editor/Systems/Resource` 的领域承载；Foundation 只提供池、事件、生命周期等基础，Core 保持业务/表现 owner。
2. 阶段 8 单独研究原 XFAB 与 YL Resource。Core 目前的 XFAB调用、启动表专用前提、ProjectName 捕获/查找语义、场景与命名实例取得/归还、Editor资产定位都纳入真实消费清单，不假设只有一个 using 需要替换。
3. 届时解除 Core 对原 XFAB及 Audio对ServiceManager internal的过渡耦合，但具体公开资源消费契约/装配位置由专题论证；不预建 Foundation.ResourceContracts、第二 Provider registry、租约层或最终资源组语义。
4. 以统一资源权威管理地址/加载/依赖/租约/实例与物理释放。Module/View逻辑退出、Audio播放租约、Foundation GO池桶与后端资产释放仍有不同责任；不能把缓存归还等同最终 AB卸载。
5. 完整 Foundation/Core闭包继续作为方向门禁。阶段8明确批准的窄消费合同可调整 Core参考政策，必须说明 owner/解除条件和正反例；不能把整个 Systems目录加入 Core闭包或将完整资源实现搬进 Foundation。
6. 原 XFAB Package和原游戏加载链保持只读。新增自有资源实现不等于改原 manifest/产物，也不等于阶段9无原15系统独立验收。

阶段8消费接缝的具体库存如下，不以using数量替代真实调用:

| Core/Editor调用点 | 现有原XFAB能力 | 必须保持的约束 |
|---|---|---|
| `ViewManager.Loading.cs:55`、`LoadViewRequest.cs:233-234` | 同步/异步Prefab取得及LoadAssetRequest | 原ProjectName fallback、迟到结果、原请求失败与Core逻辑取消 |
| `ViewManager.Loading.cs:70,159`、`ViewManager.Registration.cs:169` | GameObjectLoader.Load/UnLoad | 命名/直传实例的原owner、父级、缓存归还与物理销毁区别 |
| `StartUpModuleRequest.cs:139,167` | TextAsset加载/UnloadAsset | 启动表专用前提、取文本后归还、构造/清理异常关系 |
| `StartUpModuleRequest.cs:198-220` | LoadSceneRequest及按名场景返回 | 新实例识别、失败/歧义检查、归属View挂载 |
| `EditorAssetTool.cs:56-83`及Editor asmdef | XFABProject/Bundle资源定位 | 编辑器资产预览的实际定位契约，不误迁成Foundation工具 |

移除Runtime的XFAB asmdef引用本身不等于Json.NET立即消失，原Package若仍存在其传递依赖仍可能保留。但阶段8/独立载体解除原XFAB Package来源时必须显式确认Core启动表、Save、HTTP、Resource对Json.NET的依赖继续解析，或另行批准序列化替代；不得依赖“当前缓存碰巧存在”。本轮不修改manifest/锁文件，也不提前迁移数据格式。

资源库存还包括未完成HotUpdate（空版本路径、被注释的状态/回调管线、private ReplaceRes空目录）、FakeResServer本地模拟/WWW、ResData的WWW配置读取和BinarySerializer的BinaryFormatter。它们的补齐/退役/平台兼容由资源专题决定，不进入Tidy，也不在层级守卫通过后宣称可用。

### 11.2 后续 XF 扩展的实际承载依据

本轮只定向读取未来原系统代表，不启动融合。当前锁定 revision及源码证明它们需要的不是“大量 Core公共管理器”，而是领域系统消费共享基础:

| 代表源证据(PackageCache只读) | 后续约束 |
|---|---|
| SkillManager:32-38、218-227、295-300; BuffManager:32-38、201-223、322 | Skill/Buff的配置、角色owner、状态/效果在各自System；复用Foundation Update/Pool/AssemblyTool。必要View消费可以保留，不能复制原Update/ClassPool或把完整领域下沉 |
| LocalizationManager:185; LocalizeBase:49-55; LocalizeImage:9,14,50-57 | 语言权威/配置/偏好属于Localization；组件绑定语言事件与资源取得，不要求Core View参与所有文本。未来图片链须消费统一Resource，不继续绑定原XFAB.ImageLoader |
| MissionManager:70-76、183-196 | Mission管理任务/参数索引及领域实例，类型查找复用Foundation；持久格式与配置/资源关系另审，不建Core全局任务管理器 |
| LoggerManager:97-119、235-270 | Logger是完整领域系统；自动宿主、全局ILogHandler与文件writer的owner/恢复/退出需专项治理。Foundation可直接用Unity诊断，不反向调用未来具体LoggerSystem |

原Skill/Buff/Mission/Localization/Logger的 namespace及Package身份不是未来正式YL身份，不能原样接入两主体再依赖其原公共机制。新领域与便利适配按职责放Systems/Utilities；成熟代码FSM与未来可视化XFFSM也不能只因名字相同强行合并。无实际授权时不提前创建空目录或复制这些源码。

## 12. 待审核决策与完成条件

| 决策 | 本方案推荐 | 审核 / 授权边界 |
|---|---|---|
| D1 Foundation基础调用 | 直接使用原生 API，便利扩展留 Utilities，不新增底层 helper，拒绝工具箱例外 | 2026-10-14 用户明确批准，替代初版的共享 helper 方案 |
| D2 Editor治理范围 | §8.1七文件纯路径归位，Settings/生成输出/资源预览不动 | 可独立批准/延期Tidy-2，不影响Foundation断环必要性 |
| D3 约束方式 | 完整Foundation及Foundation+Core源码闭包 +少量正反例，保留两主体 | 不新增产品asmdef、SDK规则或运行时Roslyn |
| D4 公共FullName/AQN/重要API统一 | 明确不在默认Tidy内 | 如用户另选须单独消费者/序列化/反射/迁移论证及批准，不机械改名 |
| D5 资源所有权/ProjectName/后端/热更 | 全留阶段8或后续专题 | 本Plan不预定租约/地址/合同，不授权提前实现 |
| D6 保存策略、单例诊断、旧组件/IVT退役 | 登记有据债务，不借Tidy顺手改行为或删功能 | 需要相应行为专题与必要证据；缺C#使用不等于可删除 |

**原规划轮完成条件:** 唯一总体方案已给出现行事实/逻辑图、源码证据、A/B/C/D判断、推荐治理/替代方案、兼容风险、可审查批次、门禁及阶段8/未来承载约束；没有产品/长期文档/配置/Git修改，没有虚称新测试或编译通过。

**未来Tidy完成条件:** A1实际归零，完整Foundation边界及Core边界的有效正反例通过；批准的目录归位保持所有身份与直接消费者；未引入第二权威/下层反向依赖；保护现场/必要分发内容核对通过，未验证平台及资源专题保持明确。失败批不能由其他历史绿记录覆盖。

当前授权实施止于 Tidy-2。后续 Tidy-3、阶段 8 研究实施及任何 Git 写操作不自动启动。

## 13. Tidy-1 初版历史记录 (2026-10-14)

本节保留初版 helper 方案的实际实施及当时验证，不能作为后续原生 API 修订的验证证据。用户已批准移除该 helper，当前源码与本次重验以 §14 为准。

### 13.1 实际改动与保护

- 在原 `integration/xf-yl` 工作区实施，HEAD 仍为 `63e152e0bbc54f8397707c3eb407cc2eb1e3ee17`；未创建/切换分支、暂存或提交。原三项用户修改保持。
- 新增唯一 `Foundation/Utilities/UnityObjectUtility.cs`，为 `YLFramework.Foundation` 的 internal 非扩展静态类，实际运行中确认只有四个 internal 静态方法及唯一 Runtime 类型身份。Unity 正常导入生成 meta，GUID 为 `fe6991c99fd342f49a584e8a2c9b64b2`；Assets 的 meta 定向扫描仅发现一处该 GUID。
- 五个 Foundation 文件的九处调用归口；Prefab 克隆直接 `Object.Instantiate`。三个公开扩展文件保留声明/XML 契约；Component GetOrAdd 保留便利转接，GO 泛型/Type 重载和销毁 API 消费唯一底层实现。
- `FoundationBoundaryTests` 从原 49 文件改为当前全部 124 个 Foundation C# 文件 + 唯一 Root，共 125 个源码输入；测试不锁定该计数。UGUI 从唯一加载程序集与运行中编译图 outputPath 核对，不新增 asmdef 引用。可信 Unity/BCL 参考之外仅允许该真实 UGUI，不借正式 Runtime DLL。
- 两 profile 明确核对 `UNITY_EDITOR` / `DEVELOPMENT_BUILD`；`ENABLE_INPUT_SYSTEM`、`ENABLE_LEGACY_INPUT_MANAGER` 都与产品当前编译图一致，真实参考及 define 集合写入测试输出。
- Audio 显式源表加入新 helper，导出测试加入 helper 哨兵。未执行真实导出，仍按原方案留最终源码状态的 Tidy-3 执行一次。
- 现有测试没有 PrefabSingleton/PersistentMonoSingleton 的直接覆盖，也没有 GetOrAdd 双重载替换及组件销毁返回值覆盖；仅在现有两个 PlayMode fixture 补五项真实 Unity 对象用例，未新建 fixture、Mock 或测试程序集。
- 受影响十二个类型的 FullName/AQN、基类、成员、字段及特性快照共 341 条保持；框架既有 meta/asmdef/资源/插件与选定用户配置、manifest/锁文件等 994 个受保护文件 SHA-256 全部一致。基线工具与快照位于 `C:/Users/Kano/AppData/Local/Temp/kilo/xf-yl-tidy1-*`，未纳入工程资产。

### 13.2 本轮实际验证

在同一已打开的 Unity `2022.3.62f3c1` Editor 中使用 `unity-mcp-cli run-tool tests-run`，只运行下表选定 fixture/方法；测试前后场景为已保存的 `Assets/Scenes/Main.unity`，未自动保存用户场景。

| 模式 | 实际选定测试 | 最终执行结果 |
|---|---|---|
| EditMode | `FoundationBoundaryTests`，含完整 Foundation Editor / Windows64 Development 闭包及原身份/IVT 断言 | 8 passed |
| EditMode | `AudioSourceIndependenceTests`，含双 profile 与参考拒绝断言 | 3 passed |
| EditMode | `UnityEventListenerContractTests` | 4 passed |
| EditMode | `SingletonContractTests` | 5 passed |
| PlayMode | `SingletonHostPlayModeTests`，含新增持久重复、Prefab 同帧隔离、缺组件克隆清理；审查补强后重新运行 | 6 passed |
| PlayMode | `ExtensionLifecyclePlayModeTests`，含新增 GetOrAdd 双重载替换与组件销毁返回值 | 3 passed |
| PlayMode | `EventsLifecyclePlayModeTests`，含 OnDestroy/OnDisable 生命周期 | 14 passed |
| PlayMode | `AudioPlaybackTests.SameFrameRepeatedShutdownRecreatesHostWithoutOldCallbacksOrSources`，唯一选定 Root 直接消费者 | 1 passed |

合计 **44 个不同的定向用例通过 (20 EditMode + 24 PlayMode)**，不是全量测试。CLI 的 `TotalTests` 是发现的整棵测试树数量，不作为执行数量。

实现前两种完整 Foundation profile 分别取得有效红态，原始错误为五个指定文件的 `CS0234` 上层 Utilities namespace 缺失；随后同一参考/profile 政策下绿态。新增生命周期用例先在原行为上取得基线绿态。首次使用全限定 `testClass` 产生 `Unknown/0 executed`，以及把 fixture 当 `testMethod` 的无发现调用均不计验收；后续以短类名和真正全限定方法精确执行，结果如表。

Unity 已实际刷新、编译并加载新 Runtime helper 和修改后的测试，正常生成新 meta；这不是 SDK/dotnet 编译替代证据。最终 `git diff --check` 无错误，暂存区为空，Editor 未播放、未编译、未更新。

一次只读独立代码审查未发现产品实现 Critical/Important 问题。其指出的 Prefab 用例 GO 断言与失败清理缺口已修正：保留自有 clone GO、明确帧末整个 GO fake-null，并在 finally 清理存活克隆；修正后该 fixture 6 项重新通过。Runtime、Foundation/Audio 探针输入未受这次测试补强影响，不重复执行已有效的闭包基线。

### 13.3 限制与停止位置

- 退出 PlayMode 时 Console 出现 `Some objects were not cleaned up when closing the scene ... LoggerManager`。修复前的生命周期基线运行已在本地时间 `14:22:09` 出现，修复后运行仍出现；所选测试报告 Passed 不代表 Console 清洁。
- 只读定位到原 `com.xfkj.xfgameframework.loggersystem@0ebbebeb4b/Runtime/LoggerManager.cs` 的自动宿主/全局 logHandler 安装、Instance 重建及 OnDestroy 未恢复 handler 路径，与此现象一致；没有把未经完整运行时追踪的链路冒充完整根因，也未修补受保护的原 Package。该环境问题留原 Logger/退出行为专题。
- 没有执行 Tidy-2 七文件移动、Tidy-3 Core 闭包/故意违规负例/伪参考矩阵、长期导航维护或真实导出；没有重建 SDK、Player、运行全量 EditMode/PlayMode 或原游戏全流程。Windows64 Development 源码闭包通过不等于 Player 构建通过。
- Tidy-1 完成后停止，后续批次及阶段 8 不自动推进。

## 14. 原生 API 修订与重新验收 (2026-10-14)

用户明确批准 Foundation 直接使用 Unity 原生 API，并要求后续不要为复用上层便利代码向底层增加不必要的封装。本节是当前源码状态及本次验证；§13 仅保留初版历史，不覆盖本次证据。

- 五个 Foundation 调用方直接使用 `GetComponent` / `AddComponent` / `Object.Destroy` / `Object.Instantiate`。保持必要的 Unity fake-null 判断、销毁整个 GameObject、帧末销毁和 Prefab Dispose 的同帧隔离；没有新增替代 helper、private 包装或扩展。
- 删除初版的 `UnityObjectUtility.cs` 及 meta；此前资产引用/GUID 定向检索没有发现正式资产消费者。现有 Foundation 身份测试补充该源码/meta/Runtime 类型已退役断言，先在 helper 存在时取得有效红态，再随删除取得绿态。
- 三个 Utilities 扩展文件恢复原实现。Audio 源表与导出哨兵移除 helper；这些文件最终均与原 HEAD 无 diff，便利 API 及客户端行为保持。
- 完整 Foundation 闭包继续递归收集全部源码并只附加 Root，当前为 123 个 Foundation 文件 + Root；源码收集、参考白名单和 define 算法未放宽，没有收进上层工具或借 Runtime DLL。
- 已更新 §1、§5 A1、§8、Tidy-1 范围与 D1，撤销“几行原生操作也必须唯一实现”的要求。真正的基础机制仍按职责与维护收益复用，简单原生调用的局部重复不等于第二套领域权威。

本次在同一 Unity `2022.3.62f3c1` Editor 中重新执行:

| 模式 | 本次选定测试 | 实际结果 |
|---|---|---|
| EditMode | `FoundationBoundaryTests`，含完整 Foundation Editor / Windows64 Development 闭包及 helper 退役断言 | 8 passed |
| EditMode | `AudioSourceIndependenceTests`，含双 profile 与参考拒绝断言 | 3 passed |
| EditMode | `UnityEventListenerContractTests` | 4 passed |
| PlayMode | `SingletonHostPlayModeTests` | 6 passed |
| PlayMode | `ExtensionLifecyclePlayModeTests` | 3 passed |
| PlayMode | `EventsLifecyclePlayModeTests` | 14 passed |
| PlayMode | `AudioPlaybackTests.SameFrameRepeatedShutdownRecreatesHostWithoutOldCallbacksOrSources` | 1 passed |

合计 **39 项定向测试通过 (15 EditMode + 24 PlayMode)**，不是复用初版 44 项结果，也不是全量测试。Unity 已实际刷新、重新编译和加载原生实现；Foundation 原生源码没有上层 Utilities 引用，helper 名称在 Assets 中仅保留于退役断言。

截至原生 API 修订验收时，994 个受保护文件 SHA-256 与 341 条类型/成员/字段记录再次全部匹配初始基线；原三项用户修改及正式资源保持。`git diff --check` 无错误，暂存区为空，尚未创建/切换分支、暂存或提交。

原 XF `LoggerManager` 的 PlayMode 退出清理错误仍出现，§13.3 的环境限制继续有效；没有声称 Console 清洁，也未扩展处理原 Package。未执行 SDK/Player 重建、全量测试或真实导出，不进入 Tidy-2、Tidy-3 或阶段 8。

## 15. Tidy-1 Git 交付授权 (2026-10-14)

用户另行授权创建阶段 Tidy 专用分支，并首次提交、推送 Tidy-1。分支采用 `refactor/xf-yl-tidy`，基于 `integration/xf-yl` 的 `63e152e0bbc54f8397707c3eb407cc2eb1e3ee17`，目标远端为 `origin`。

首次提交只纳入五个 Foundation 调用方、三个直接测试文件及本方案，共九个文件；`.gitignore`、`.kilo/kilo.jsonc`、`ProjectSettings/Packages/com.unity.probuilder/Settings.json` 的用户改动保留在工作区，不暂存或提交。代码与 §14 验证状态一致，本次仅进行 Git 交付，不重复运行未受影响的测试，也不授权合并、强制推送或后续实施批次。

本节记录授权与交付范围，实际提交 ID 和推送结果以 Git 历史、远端核对和本次交付回复为准。

## 16. Tidy-2 实施与验收 (2026-10-14)

### 16.1 实际范围

- Tidy-2 实施验收时分支为 `refactor/xf-yl-tidy`，HEAD 为已推送的 Tidy-1 提交 `846a02f7d928e4c32b74756b101304b6bcaf1732`；实施期间未创建分支、暂存、提交或推送，原三项用户配置修改继续保护。随后 Git 交付按 §17 的另行授权执行。
- 经当前 Unity AssetDatabase 确认七个原脚本及 GUID，再由 `assets-create-folder` 创建六个实际目录，`assets-move` 完成 §8.1 七对源码/meta 迁移；没有覆写目标、改名类或合并实现。
- 新目录为 `Editor/Foundation/Attributes/` 及 `Editor/Utilities/{Serialization,Scene,Assets,TreeViews,Windows}/`，其六份目录 meta 正常导入且 GUID 各自唯一。七个原脚本/meta 的字节、BOM/换行、版权及 GUID 全部保持。
- 八个类型包含同文件的 `TreeViewStateInfo`，仍为 `YLFramework.Core.Editor.*`，属于唯一 `YLFramework.Editor`；公开/私有成员、序列化字段及特性未变。旧 Core 的四个空目录及 meta 保留，非空 Extensions、Preview、Settings 等不移动。
- 仅更新 `Documentation/Architecture.md`、`Documentation/Modules/Attributes.md` 和 `Runtime/THIRD-PARTY-NOTICES.md` 的直接位置说明，来源/完整许可证不改变。Help provider 未引用七个旧路径或类型，不改 SourceLinks、菜单、ID、配置键或共享 Help 功能，也未重写旧阶段方案。
- 现有 `CoreEditorIdentityTests` 增加一个七文件路径/GUID/MonoScript、唯一类型/Editor归属及Player源码缺席断言，没有新建 fixture/asmdef/测试平台。`ExportUnityPackageTests` 加入七个新路径哨兵；实际导出留 Tidy-3 最终源码状态执行一次。

### 16.2 实际验证与偏差

- 同一 Unity `2022.3.62f3c1` Editor 未进入 PlayMode；迁移后实际刷新、重新编译并加载类型。新路径断言先以目标缺失有效失败，再验证七对路径/GUID/类型/程序集与 Player 源码隔离通过。
- 初次短类名 `CoreSerializedPropertyTests` 调用返回无发现；增加 assembly/namespace/class 的组合筛选未按预期缩小到单一fixture，实际执行了 `YLFramework.Core.Editor.Tests` 下 **104 项**，全部 Passed、无失败/跳过。此为真实扩大的验证范围，不冒称只运行14项；随后不重复执行其中已经有效的用例。

| 本次实际执行中包含的直接门禁 | 实际结果 |
|---|---|
| `CoreSerializedPropertyTests`：真实Clipboard、拒绝不兼容数据、Undo/Redo、多对象及Prefab override/原资产保护 | 14 passed |
| `DisableEditDrawerTests`：原生高度、注册绑定、展开子属性禁用及异常后GUI恢复、Clipboard直接消费 | 3 passed |
| `CoreEditorIdentityTests`：新路径、完整Editor源码集合、唯一类型归属、仅Editor程序集、原资源/许可、菜单及原分析器正负见证 | 12 passed |
| 同次组合筛选额外执行的DefaultResources、Configuration、ModuleGeneration既有用例 | 75 passed |

合计104个不同用例，而非 `TotalTests:1547` 的整棵发现树。额外用例是测试工具筛选的实际偏差，不扩大产品实施范围；未重跑完整工程/Resource UI、SDK 或 Player 构建。Help 无受改提供方，本批未专门重跑HelpGUI；Window/TreeView状态以原字节、FullName/AQN、字段/特性和唯一类型身份保护，未宣称Editor布局重启测试。

本轮保护核对使用的证据明确区分:

- 迁移前不可变 `xf-yl-tidy2-baseline.json` 完整保留七份源码 SHA-256、七份 meta SHA-256、七组 GUID/MonoScript 类型映射及八类型共53条身份/成员/字段记录，迁移后全部逐项匹配。
- 临时工具的 `JsonUtility` 未写入动态复合 `FileRecord[]`，初次回读因该数组为空失败。没有把失败当通过、覆盖原快照，或迁移后重抓数据冒充迁移前基线。
- 缺失的资源集合证据由已有不可变 Tidy-1 保护记录补足：七份meta做唯一旧新路径映射，六份新目录meta作为实际新资产单独核对，其余 **994个受保护文件SHA-256全部匹配**。不把原捕获工具打印的996条数值当作完整往返验证通过。
- 源码集合使用开始时无Editor源码改动的不可变Git提交 `846a02f` 恢复，反向应用七路径映射后仍为同一 **53个Editor源文件**；既有root源码集合测试也实际通过。保存的Main场景/meta另由Git无差异及打开场景无dirty验证，不冒称新996条完整hash校验。
- 原快照未改写；修正后的只读验证工具和证据均在 `C:/Users/Kano/AppData/Local/Temp/kilo/xf-yl-tidy2-*`，没有添加工程工具或正式资产。终态脚本返回MATCHED全部上述原字节、GUID、53条身份字段、994条哈希、53文件源集合及6目录meta/GUID唯一性。

一次独立静态审查没有Critical/Important/Minor发现，未重复主执行者的运行验证。本批的测试发现失败和临时校验工具回读错误仍留日志，不声称整个Console清洁；未发现迁移造成的正式项目编译错误。

### 16.3 停止位置

Tidy-2 源码移动、直接导航和门禁完成后停止，验收时改动未提交；之后的 Git 交付另获用户授权，见 §17，而非扩展之前仅针对Tidy-1的授权。不进入Tidy-3/Core闭包负例、真实导出或阶段8，不修改asmdef、用户场景、原Package、游戏、SDK/Player配置及已有用户改动。

## 17. Tidy-2 Git 交付授权 (2026-10-14)

用户另行要求处理 Tidy-2 的 Git 工作，按已采用的交付方式，在现有 `refactor/xf-yl-tidy` 分支提交并推送到 `origin`，不创建另一分支、不合并或强制推送。

提交只包含七对源码/meta的纯重命名、六份实际新目录meta、三份直接导航/来源说明、两个直接测试文件及本方案，共26项Git文件记录。三项用户配置修改不暂存、不提交。源码/测试差异与§16已通过的104项实际验证一致，本次不重复运行未受影响的测试；暂存后检查重命名相似度、完整meta与排除项。

暂存核对确认十四项重命名均为100%内容相同。完整 `git diff --cached --check` 仅报告六份新目录meta中Unity生成的空字段尾空格，与既有目录meta格式一致，保留原生成内容；非meta暂存补丁检查通过。没有修改Git配置、关闭诊断或跳过hook，不把完整检查冒称为零报告。

本节记录授权及交付范围，实际提交ID、推送同步及原集成分支保护结果以Git历史、实时远端核对和本次交付回复为准。不自动推进Tidy-3或阶段8。
