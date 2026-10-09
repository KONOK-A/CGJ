# XF × YL 阶段 2：公共运行能力融合总体规划

**状态：阶段 2 总体规划已批准；阶段 2：IN PROGRESS；2.1 Pool：COMPLETE（A～E 全部完成并通过用户验收；A～D 已 Git 收口，E 的 Git 收口提交由用户后续执行）。2.2 Events：待专项研究与设计，不自动启动。当前依据见 [Pool 专项](xf-yl-2.1-pool.md) 第 16～17 节。**

本文保留批准时的全局源码研究、专题顺序和边界，不是六套系统的详细重构契约，也不自动授权实施、切换分支或 Git 写入。下文研究基线及当时“下一步”属于历史规划，后续专项已另获授权；当前完成状态以专项实际结果及长期蓝图为准，不自动启动 2.2。

## 1. 基线与研究范围

- 2026-10-09 实际核对：分支 `integration/xf-yl`，HEAD `5ab6437`，研究开始时工作区干净；阶段 1 集成合并为 `6cea527`，1.4 提交为 `fd7b1e8`。阶段 1 已 COMPLETE，依据为 Git 历史及 [长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md) 当前状态，不重新验收 Module/MVC、View。
- 已读取根 `AGENTS.md`、长期蓝图、[1.1 承接方案](1791455404620-xf-yl-core-ownership.md)、[1.2 正式契约](xf-yl-1.2-module-mvc.md)、[1.3 当前实施记录](xf-yl-1.3-view.md)。旧专项文档中的历史“尚未开始”不覆盖长期蓝图较新的 COMPLETE 状态。
- Unity 为 `2022.3.62f3c1`。已核对 manifest、锁文件与实际包目录：XFGameFramework `1.18.8` / `2182708aecc596f30739fa8634fe2b9b6946ddd7`，XFABManager `1.24.0` / `c35dc4058ecd831409199b43cb328024202d4557`。阶段 1 记录的声明/实际 Unity Package 版本恢复差异仍未解决，不升级或修改依赖。
- 研究覆盖原 XF 安装源码、YL 自有 XFMain 副本、旧 YL Foundation、Tools/Timing、Save/Serialization、Action，以及必要的核心、UI、Audio、Resource、Editor 和测试调用边界。原包只作只读参考；没有开展独立 XF 扩展、完整资源系统或 UI/Audio 的融合设计。
- 本轮只做源码、文档、程序集和测试文件的静态研究；没有执行 Unity 刷新、编译、测试、运行或构建。阶段 1 历史验证仅证明原基线，不证明未来阶段 2 实现。

源码路径简称：`C` = `Assets/YLFramework/Runtime/XFMain/`，`Y` = `Assets/YLFramework/Runtime/`，`E` = `Assets/YLFramework/Editor/`，`T` = `Assets/YLFramework/Tests/`，`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，`AB` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`。行号对应本次基线，仅作为定位依据。

### 实际程序集边界

| 所在位置 | 实际归属与直接依赖 | 规划含义 |
|---|---|---|
| `Y/Shared/` | `YLFramework.Foundation.Runtime`，显式 references 为 `[]`，当前仅公共请求、协程、运行状态支撑 | 不是旧 Foundation 全集；不能假定已有 Events/Pool/Timer |
| `C/` | `YLFramework.Core.Runtime` → Foundation、XFABManager、UnityEngine.UI | 已承接公共能力仍主要在 Core；资源依赖保持过渡性质 |
| `Y/Foundation/`、Action、Tools/Timing、Save 等 | `YLFramework.Runtime` → ZipTool、UnityEngine.UI；当前没有 Core/Foundation 引用 | namespace 含 `Core` 也不代表编入 Core；不能以 Core → 整体 Runtime 偷渡公共能力 |
| 普通 `E/` / `E/XFMain/` | 分别为旧 Editor / Core Editor，均 Editor-only，分别引用对应 Runtime | 公共类型迁移后按真实用途调整引用；不能只搬 Runtime 忽略 Drawer/帮助/测试 |
| DOTween 集成 | `YLFramework.Actions.DOTween` → Runtime + DOTween.dll，受 `DOTWEEN` 约束 | 保持可选边界，不因 Action 协同引入底层第三方依赖 |
| 原游戏 `Project.Game.MainModule` | 引用原 XF、XFAB、XFFSM、旧 YL Runtime 等，不引用新 Core | 原 TimerManager 等调用仍走原包；现有 YL Audio 的底层变更仍可能影响游戏 |

依据：各实际 `.asmdef`；`Assets/Games/MainModule/Project.Game.MainModule.asmdef:4–11`。`Documentation/Architecture.md` 的历史目录总表未完整列出 Shared/XFMain；模块文档可作导航，不代替程序集事实或实际测试覆盖。

## 2. 功能与职责对比

| 能力 | XF / 已承接 Core | 现有 YL | 重叠与取舍方向 |
|---|---|---|---|
| Events | `EventManager` 全局 Delegate 总线，int/字符串 hash 键，0～7 参数；View 声明订阅与框架通知 | `EasyEvent` 0～3 参数、typed/string/int/enum 分发、注销句柄、Once/Or、生命周期触发器、Unity EventListener；Binding/Action/Audio 已复用 | 广播与订阅管理高度重叠；实例事件、typed bus、Unity 输入/物理适配职责不同，可共享机制但不强行合成一个全局总线。不能把 0～3 参当作 0～7 参直接替代 |
| Pool | 按 Type 的 `ClassObjectPoolManager`、`IReuseable`、超时空闲清理、集合/参数复用；View 对象池链实际包装 XFAB | `Pool<T>`、Safe/Simple/Independent/反射类池、集合池、工厂、预热/容量/顺序；自有 GameObjectPool；Action 已包装 `Pool<T>` | 普通对象/集合缓存高度重叠，领域 reset 不同。共享低层缓存，保留领域所有权；GO 复用不等于 Prefab/Bundle 资源租约 |
| Timer | 秒/帧计时、延迟/循环、首次延迟与后续间隔、GameObject 停用中断、NonAlloc 复用、协程等待；独立 Mono Update | Tools/Timing 双堆、scaled/unscaled 时钟、有限/无限重复、暂停/恢复、状态、剩余时间、TimerScope/命名、安全重投递；独立 Mono Update | 延迟与周期回调高度重叠；帧计时、GO 生命周期与作用域可互补，但首帧、到期比较、补发和取消语义不同；先形成能力/行为表再选实现，不预定用堆或列表 |
| Update | `UpdateManager` 的 Update/FixedUpdate/LateUpdate 委托；核心 View 存活扫描、Panel 排序、Core Audio 使用 | Action 全局宿主转发七种 Unity 消息；对象/当前场景/全局动作执行器；Timing/Audio 也有领域 Update | 公共消息转发有重叠，Action/Timer/Audio 的领域推进不是同一种工作。先稳定消息与订阅生命周期，再按实际收益共享入口；多宿主不自动等于性能缺陷 |
| SerializedDictionary | `DictionaryItem` 列表 + 两份延迟缓存，序列化列表可由默认 Inspector 展示；未找到专用字典 Drawer | Save 目录内的字典以隐藏的 `keyList/valueList` + `ISerializationCallbackReceiver` 同步真实 Dictionary；未确认实际存档消费者 | 通用序列化容器重叠，但字段格式、访问接口和 Editor 工作流不同；普通运行时 Dictionary/Table 不属于应替换对象 |
| Action | XF 没有与现有 YL Action 等价的组合执行层 | 动作树、逐帧进度/插值/条件、Sequence/Parallel/Repeat、Coroutine/Task/Observable、命名与租约、对象/场景/全局范围 | 已有能力，不再承接、不另立融合阶段；在 Pool/Events/Update/Timer 各专题处理必要协同，组合语义保持独立 |

### 主要源码依据

| 主题 | 关键定义及调用证据 |
|---|---|
| Events | `C/Manager/EventManager.cs`，对应 `XF/Runtime/Manager/EventManager.cs`；`Y/Foundation/Events/Core/`、`Global/`、`Lifecycle/`、`Unity/`；`EasyEventPool.cs:17–39`；`C/Manager/ViewManager.cs:208`；Action 的 `ActionGlobalHost` / `ActionKit` 消息入口 |
| Pool | `C/Manager/ClassObjectPoolManager.cs:134–209`；`Y/Foundation/Pool/Object/Pool.cs`、`SafeObjectPool.cs`、`ClassObjectPool.cs:63–145`、Collections/GameObject；`Y/Action/Internal/ActionPool.cs:13–40`；`C/Other/LoadViewArgs.cs:162–190,238–277` 的专有参数生命周期 |
| Timer | `C/Manager/TimerManager/TimerManager.cs:94–152,266–329,1149–1157`，对应 XF 原文件；`Y/Tools/Timing/Timer.cs:363–418`、TimeItem/TimerScope/BinaryHeap；Core `TimerObject`；原游戏 EnemyBase/TipPanel/EffectController 使用的是原 XF |
| Update | `C/Manager/UpdateManager.cs:26–38,64–76,119–148`；`C/Manager/ViewManager.cs:19–25,249–253`；`C/Manager/PanelManager.cs` 的 LateUpdate 接入；`Y/Action/Execution/MonoUpdateActionExecutor.cs:269–293`；`Y/Shared/CoroutineStarter.cs:43–59` |
| 字典 | `C/Extensions/SerializedDictionary.cs:12–85,106–148`，对应 XF 同机制；`Y/Save/Serialization/SerializedDictionary.cs:25–94`。当前 Assets C# 检索只命中两份定义，YL 自有文本资产定向检索未找到保留字段；未确认实际消费者/持久化数据，不等于证明外部存档或动态使用不存在。`C/Manager/AudioPlayer.cs:175–189` 使用 List 与普通 Dictionary，不是迁移对象 |
| 执行与复用边界 | `C/Module/Module.cs:39–61` 仍使用普通运行时集合；Module/Model 默认非池化；ViewManager 的真实取得/归还为 XFAB GameObjectLoader，不是 YL GameObjectPool；`Y/Action/Internal/ActionQueue.cs` 与 `Execution/ActionHandle.cs`；`Documentation/Modules/Action.md`、`Timing.md` 的现有领域契约 |

Unity 监听适配也有互补而非仅重复：Core `Other/Listener/CollisionEventListener.cs`、`TriggerEventListener.cs` 带 LayerMask 过滤，`AnimationEventListener.cs` 承接动画字符串事件；YL 通用 EventListener 带 Inspector UnityEvent，但不覆盖这些全部能力。其 UGUI/Odin/扩展依赖不应随纯分发机制整体下沉，相关字段、动画事件与脚本身份仅在实际迁移时定向验证。

## 3. 关键问题与阶段归属

下表是可靠源码机制或明确的现有契约限制，不冒充本轮运行复现。未来修复须在 YL 自有源码内验证；原包及游戏不跟随修改。

| 证据 | 影响与处理边界 |
|---|---|
| Core 类池 `Load<T>` 用旧 `obj` 而非新 `t` 调 `IReuseable`（143–149）；Unload 先记时间再调用户回收（200–209） | 冷创建漏 OnLoaded；回调抛错可留下“已归还但不可分配”状态。Pool 专项优先处理加载/回收异常和重入一致性 |
| Core 普通类池以对象默认相等比较维护回收表/移除；YL 反射类池先 OnRecycle 再设标记（109–110），自动写所有可写属性（139–145） | 前者可能混淆值相等实例；后者重入可递归，索引器/有副作用 setter 不适合无差别 reset。不能因任何一方“已可用”原样统一 |
| YL GameObjectPool 的去重限定单个 key 的池数据 | 跨 key 归还同一实例需要专项评估；不能将各 key 的局部保护当作全局所有权保证。只处理自有 GO 池正确性，不接管 XFAB 资源链 |
| Core 字符串事件使用 hash 当身份；签名冲突记录错误后忽略；YL StringEventSystem 保留完整字符串且冲突抛错 | hash 冲突和错误被忽略的机制风险；Events 专项确定键、签名及报告策略，不静默改变全部调用方异常契约 |
| YL 池化 EasyEvent 注销闭包绑定实例/委托，未绑定事件复用代次；生命周期触发器先摘集合，逐项异常会中断（UnRegisterTriggerBase:49–57） | 旧注销句柄可能影响复用后的相同委托；批量清理失败后剩余订阅丢失托管。Events 专项研究身份、异常继续清理；通知分发异常策略另行确认，不能混为一谈 |
| YL `EasyEvent.cs:65–73,106–108` 的 Once 包装没有已触发守卫，多播快照仍可持有包装器 | 前序监听嵌套 Trigger 时，内层退订后外层旧快照仍可能再次调用 Once。Events 专项验证重入的“一次”语义，不只覆盖普通连续两次 Trigger |
| `OnCurSceneUnLoadUnRegisterTrigger` 响应任意 sceneUnloaded | 是现有明确契约，不是 Module owner / 指定 Scene handle。不得直接用于替代阶段 1 的精确 View 场景处理；改公共含义或新增精确能力需专项设计 |
| Core RemoveUpdate/Fixed/Late 都经创建式 Instance；Shared CoroutineStarter.Stop(non-null) 同样经 Instance；ApplicationTool Editor 过渡值/Player 恒 true | “退订/停止”可能创建宿主，且没有完整退出屏障。Update 专项处理相关自有执行支撑边界；不展开整个 AsyncOperation 或修原 Logger |
| Core Timer 回调先于失效标记；OnRemove 不解除 GO 监听及清 callback；重新绑定 GO 未先退旧监听 | 异常可令一次任务再次触发，NonAlloc 回调重启可被旧收尾失效，监听可保留对象/闭包。Timer 专项必须验证终态、重入代次和绑定释放 |
| Core 字典 setter 只写非序列化 `datas_dic`（148），缓存只按 Count 判断（52/74） | 值更新可能不保存；同长度条目修改无法使缓存失效。SerializedDictionary 专项处理运行时/持久化一致性，不能继续将其作为推荐新容器 |
| YL Save 字典反序列化假定两个列表有效且匹配（85–94），重复键直接 Add 抛错；序列化每次创建两份 List（74–78） | 比 Core 有完整同步机制，但不是无条件替代品。专项确认无效输入、失败后的状态、Editor 可编辑性和迁移；不顺带更换整个存档格式 |

Save 系统实际使用 Json.NET（`Y/Save/SaveManager.cs:45–54,1140–1149`、`SaveFileIO.cs:90–93,190–194`）。Unity 的 `ISerializationCallbackReceiver` 与 .NET 回调存在，不等于该包装器在默认或用户定制 resolver/converter 下保存正确；若专项确认用于 Save，应验证对应真实 JSON round-trip，本轮不判定该路径已成功或失败。

性能结论只限定源码明确的成本：Core 池按 Type/List 管理和周期扫描，YL 集合池栈枚举判重，Timer 双堆与列表的不同成本，Action 回收队列装箱及部分组合节点反复扫描。没有 Profiler 测量，不据此宣称整体瓶颈或规定优化算法。Action 组合行为、空 Parallel、屏幕过渡、Task/Coroutine 外部推进等既有问题不因共享 Pool/Update 自动解决；仅直接涉及本专题支撑或已确认关键缺陷时纳入，否则留后续定向改进。

## 4. 真实依赖与推进顺序

### 依赖结论

1. **当前硬依赖**：YL 池化 EasyEvent → SafeObjectPool + Singleton/session 支撑；Core EventManager.RemoveAllEvent → Core 临时 List 池；ActionPool → YL `Pool<T>`；Binding/Audio/Resource/FSM 等 → 部分旧池/事件/宿主。这些消费者必须在公共归属变化时保持可达，不能搬完目录再补依赖。
2. **核心既有消费者**：ViewManager → UpdateManager 存活扫描与 EventManager 移除通知；View → 事件启停；PanelManager → LateUpdate 排序及临时列表。Panel 内部一致性已靠直接 Activate/Deactivate/Release 路径，不恢复原 XF 的“监听移除广播才维护状态”。
3. **不是当前硬依赖**：Core Timer、YL Timing、Action 执行器均独立接收 Mono Update，不是 UpdateManager 的现有消费者；Timer 只有协程等待路径使用 Shared CoroutineStarter。UpdateManager 本体只有 Action 与 ApplicationTool，没有 Pool/Events 依赖。
4. **数据独立**：SerializedDictionary 不以 Pool/Events/Timer/Update 为前置；它主要受保存格式、必要 Editor 支持与查实数据约束。当前未确认实际消费者或保留数据，这也支持先推进更广泛使用的公共能力。Module/MVC 索引和 AudioPlayer 缓存是普通字典，不需要被替换。
5. **推荐的新依赖方向，待批准**：Core 与整体 Runtime 按需引用中立公共基础，公共基础不反向依赖 View/Panel、Audio、Resource、Root 或原 XF。可复用已有 Shared Foundation，但具体迁入闭包及是否需要其他边界由专项研究决定，不预建五个程序集。

下沉不等于整文件移动：`C/Manager/EventManager.cs:200–537` 的多处 `WarnInClasses(typeof(View), ...)` 已形成 Core 类型依赖；View 声明适配、Core 专用约束及资源池包装应保留领域归属或在专项内拆清。旧 Safe/集合池的 singleton/session 支撑也须核对最小闭包，不把整个 Root/Singleton/UI 连带搬进公共基础。

### 推荐顺序

**2.1 Pool → 2.2 Events → 2.3 Update → 2.4 Timer → 2.5 SerializedDictionary。Action 协同随相关专题完成。**

- Pool 已被多个 YL 领域复用，同时 Core 存在重复类/集合缓存及直接影响参数生命周期的缺陷，先稳定最低层共享能力收益最大。
- Events 的部分存储依赖池，先明确池归属可避免重新承接第二套依赖；注销句柄/所有权研究本身不要求整个 Pool 全部完成。
- Update 可独立先行研究，但在公共 Pool/Events 边界明确后决定是否复用订阅机制，可减少 API 与生命周期返工；这不是要求 Update 必须用事件总线。
- Timer 放在公共执行边界之后，有利于判断哪些驱动值得共享；不要求先替换全部 Action/Timing 的驱动，也不要求 Timer 改成 UpdateManager 包装。
- 字典默认独立收口，不阻断前四项；在该专题完成前避免新增对有问题 Core 容器的依赖。若专项发现保留资产存在正在发生的数据丢失风险，应将整个字典专题提前并说明理由，而不是夹带进 Pool 批次。该调整不改变其他技术前置。

## 5. 各专题目标与批次

每个专题在启动时重新核对源码变化，按“深入研究 → 技术设计 → 用户审阅 → 分批实施 → 必要验证 → 经授权 Git 收口”推进。下列批次是单一主要变更轴的建议，不是固定类/API 或必须单独提交的微任务；详细设计就在对应专题实施前完成。

| 专题 | 目标、前置及边界 | 难度/主要风险 | 有独立验收价值的批次与验收方向 |
|---|---|---|---|
| **2.1 Pool** | 普通对象/集合缓存收敛、清理/异常/身份契约明确；先确认真实消费者和中立闭包。领域 reset、Action 延迟回收保留；Module/Model 不恢复池化。GO 自有池可整理，XFAB 加载/资源租约保持原链 | 中高：两种回收接口、反射 reset、singleton/session 依赖、别名复用、参数专用契约、Audio 实际游戏消费者 | 第一批稳定必要普通对象/集合及归属，配套直接消费者；第二批完成 Core 参数/事件等直接接入与冗余缓存收口；自有 GO 池由必做批次 E 完成并验收（见 Pool 专项第 17 节）。验证冷/热、重复/跨池、回调重入/异常、容量/清引用，复用 Action/Binding/Audio 与 LoadViewArgs 定向回归 |
| **2.2 Events** | 公共发布/注册/注销机制收敛；保留实例/类型/键/Unity 适配的职责区别。明确全部实际签名、事件范围、池化身份；不把 Module.Close 扩成全局清理 | 中高：0～7 vs 0～3 参、hash 键、全局隔离、token 旧代次、异常分发 vs 必要退订、inactive/场景生命周期 | 先落地中立事件/订阅机制及实际 Foundation 消费者，再接 Core View/Panel 通知并移除确认冗余存储；必要 Unity 生命周期/Inspector 适配随受影响批次完成。验证重入、重复/Once/Or、旧句柄、异常退订、场景精确边界及 View 缓存启停 |
| **2.3 Update** | 稳定 Update/Fixed/Late 与必要全局消息、订阅释放、非创建式移除、启动/退出边界。已有池/事件只是候选基础；精确管理各领域注册，不自动取消其任务 | 中：执行相位/顺序、多播异常、场景与对象停用、会话重置及退出重建；全局集中驱动不必然更快 | 先完成公共消息驱动及 Core 直接消费者，再按证据接入可共享的 Action 全局消息/回收或 Timing 驱动；无收益的领域宿主不强迁。验证帧相位、修改订阅、异常报告、末个订阅释放、无宿主退订、View 销毁扫描和 Panel 排序 |
| **2.4 Timer** | 独立延迟/周期回调收敛；吸收双方有价值的秒/帧、作用域/句柄和 GO 控制能力，是否保留某能力由真实需求决定。前置是时钟/执行边界清楚，不是固定堆算法；Action 编排不被替代 | 高：到期比较、首 tick、首次/循环间隔、暂停/追赶、NonAlloc 旧别名、完成内重启、GO 绑定、回调异常和跨场景 | 先完成时钟/调度/终态与安全控制，再完成实际 Core TimerObject 及必要生命周期接入并收口重复实现。验收 timeScale=0、零/负延迟、帧边界、循环、取消/暂停、回调重投递、宿主失效与清引用；不默认提供后台/跨线程/高精度/物理异步取消 |
| **2.5 SerializedDictionary** | 通用容器、必要 Editor 支持及持久化/运行时状态一致；前置为真实消费者/字段/数据/格式盘点，不预设已有字典 Drawer。Save 后端和普通运行时字典不重构 | 容器中等，若查实存量迁移则中高：`datas` vs 双列表、类型/程序集身份、重复/null/损坏数据、序列化回调环境、编辑失效 | 先确定统一容器行为与旧数据处理，验证 round-trip；再完成确有需要的 Editor 支持、查实数据/调用方迁移与冗余类型退出。验证运行时 set/add/remove 后保存重载、相同 Count 的编辑、坏输入；涉及实际编辑/资产时再验证 Undo/Redo、Prefab、保留数据。不能仅用 FormerlySerializedAs 假装完成跨类型/格式迁移 |

“冗余退出”针对新 YL 内同职责实现，须在实际调用/资源/反射核对及迁移验收后执行。原 XF 生态不删除、不转发到新 YL；职责不同的领域封装可以继续保留。空闲池的时间清理、GameObject 场景保活、事件额外参数、帧 Timer 等不是仅因另一方缺少就无条件删掉或新增。

## 6. Action 协同结论

- **已共享，不重复建设**：ActionPool 已使用 YL `Pool<T>`；全局 Unity 消息已使用 YL EasyEvent，注销机制也已复用 Events。研究和实施从这个实际基线继续，不重复“迁入 Action”或重建订阅层。
- **Pool 专题**：共享工厂/缓存/必要容量机制，但动作 reset、租约、回收标记与延迟回收队列属于 Action。不能用通用立即归池取代延迟回收，不能把仍在用户回调栈中的对象交给新执行。
- **Events 专题**：共享事件/注销的可靠基础，不混同 Action 的全局消息与其他领域 bus，也不让池复用后的旧 token 操作新订阅。
- **Update 专题**：评估公共 Unity 消息、时间输入和安全宿主边界的共享。对象执行器的停用即暂停、销毁即取消，当前场景与全局执行范围应继续区分；它们不是三套重复组合算法。替换驱动必须保持这些行为及回收顺序，不按宿主数量下结论。
- **Timer 专题**：Delay 与独立计时有功能交集，但 Action 的首帧同步 Execute(0)、Delay 先比较再累加、Sequence 子步推进与 Repeat 语义不能直接换成 Timer。TimerScope 和 Action 名称/句柄继续表达各自所有权，不建立第三套全局任务索引或跨系统通用 Scope。
- **明确不共享**：动作子树、插值/进度、组合状态、根任务名称、领域取消与成功完成。Module/View 关闭不因此自动结束外部 Action 或 Timer；调用方按既有所有权显式清理。
- **改进机会**：回收项的接口装箱、组合节点重复扫描、查找执行器时的数组分配值得按实际使用测量；Coroutine/Task 等外部操作不会因动作 Pause/IgnoreTimeScale 自动暂停或切时钟。只有专题直接改变相应执行支撑或取得明确改进授权时处理，不以总体规划授权整套 Action 优化。

## 7. 验证与非干扰边界

- 实际现存优先复用：`T/EditMode/PoolContractTests.cs`、`EventsContractTests.cs`、`TimingContractTests.cs`、`TimingSchedulerTests.cs`、`TimerScopeTests.cs`；`T/PlayMode/EventsLifecyclePlayModeTests.cs`、`TimingPlayModeTests.cs`、`TimerScopePlayModeTests.cs`；Action 的执行/组合/命名/生命周期测试；CoreLoadViewArgs、CoreDirectView、CoreXfab、CoreIdentity 相关用例及 Shared Foundation 原有最小回归。Pool 底层变更可按影响选 AudioSoundBudgetTests 的 Source 缓存验证；CoreClassPoolScope 的反射依赖须同步适配，不削弱关键断言。部分 Pool/Events 模块文档中的历史套件名称当前未找到，不将其当作现存覆盖或通过证据。
- 根据行为变化只补关键缺陷/契约验证。Update 专用边界及字典持久化缺乏已找到的直接专项覆盖，未来需要最小必要验证；不为总体规划建测试设施，不将历史通过当作新实现通过。
- 每批检查真实 diff、GUID/meta、实际 asmdef 和直接消费者；运行相应 Unity 编译及定向 EditMode/PlayMode，分别报告。公共代码移动会改变程序集类型身份，必须核查序列化类型、反射和 Inspector 引用；C# 编译通过不等于资产兼容。
- 修改公共池/事件可能经现有 YL Audio 影响原游戏，按风险复用现有 Audio 与游戏非干扰测试；保持偏好、全局音量及游戏原 XF 加载路径。没有改变这些路径时不逐批机械重跑全游戏或 Windows Player；阶段收口按实际变更风险安排必要代表性验证。
- 不修改原 Package、游戏业务、既有游戏场景/Prefab/Bundle、构建配置或依赖。迁移仅限经专项批准的 YL 自有内容；必要的自有 Editor/测试/资源适配随专题实施，不能在本轮提前执行。
- 不碰完整 UI 导航/遮罩/输入/适配、Audio 播放与 Mixer 领域、HTTP、AsyncOperation 总体协议、XFAB 资源计数/共享取消/Bundle 物理卸载或独立 XF 扩展。GO 池不是消除 Core → XFAB 的快捷方案，阶段 2 不承诺最终独立运行。
- 原 Logger/MCP PlayMode 过渡问题、Package 恢复差异、真实 AB 释放及 IL2CPP/跨平台既有风险仍保留在长期蓝图，不借公共支撑研究扩张处理。

## 8. 待确认的重大选择

以下均为推荐而非已经批准的实施契约；本轮以草案交付，不要求用户提前决定类名、API、算法。

| 决策 | 本轮推荐 | 后续需要确认的内容 |
|---|---|---|
| 专题路线与第一专题 | Pool → Events → Update → Timer → SerializedDictionary；优先启动 Pool 研究/设计 | 是否接受总体顺序与按实际数据风险提前字典的原则；不等于同时授权五专题实施 |
| 公共能力归属 | 选择中立的最小共享闭包，Core/Runtime 向下依赖，不建立 Core → 整体 Runtime | 首专题给出必要 Pool/工厂/session 依赖、程序集与直接消费者迁移方案；不先定完整五系统程序集清单 |
| Pool / GO 边界 | 普通对象/集合收敛；领域 reset 保留；只处理自有 GO 复用，XFAB 加载/资源链留后续 | 反射 reset 是否保留及限制；超时清理/容量/重复归还语义；自有 GO 池已由必做批次 E 完成并验收（见 Pool 专项第 17 节） |
| Events 异常与身份 | 完整键与签名校验、订阅所有权可靠；清理不因一个失败遗失剩余责任 | 通知是否逐观察者继续、如何报告；0～7 参保留/收敛、池化事件/token 身份及生命周期绑定方案 |
| 执行支撑共享幅度 | 共享必要消息/宿主门禁，保留 Action/Timer/Audio 领域调度 | 在 Update 专项决定哪些驱动值得接入，如何保持相位/排序/停用/退出；不预设单一宿主或统一调度器 |
| Timer 能力与字典兼容 | Timer 按能力与行为选优；字典优先持久化正确性，不机械照搬任一实现 | Timer 秒/帧/循环与控制取舍；字典格式/类型、必要 Editor 支持及仅对查实数据的迁移，分别在对应专题批准 |

## 9. 第一优先专题的下一步

**推荐：2.1 Pool 深入研究与技术设计。**主要收益是消除广泛重复的类/集合缓存，先修双方真实回收缺陷，为 Events 与 Core 参数等消费者提供清晰的低层归属；风险主要在公共身份和已有领域回收行为，而不在编写缓存算法本身。

下一专项只需完成以下设计闭环，不一次详设其他系统：

1. 以本次源码依据为起点，补齐将保留的普通/集合池消费者与接口差异，精确区分 `IReuseable`、`IOfPool`、Action 延迟回收及 LoadViewArgs 专用接管；避免恢复 Module/Model 池化。
2. 提出一份正式技术设计：必要共享闭包与依赖方向、生命周期/身份/异常/reset/容量选择、实际 API 影响及迁移方式。比较双方优点，必要时提出更合理新实现；用户确认重大取舍后才实施。
3. 确定能独立验收的完整批次与最小回归集合，评估现有 YL Audio 对原游戏的间接影响。自有 GO 池若纳入，单独说明与 XFAB 边界，不提前设计资源租约。
4. 每批实施后基于新证据验收；专题完成并取得 Git 授权后收口，再进入 Events 专项。若研究改变总体顺序或跨阶段边界，先回报并确认。

本轮到总体草案交付为止。没有功能实现、资产迁移、测试新增或 Git 收口操作。
