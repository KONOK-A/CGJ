# XF × YL 2.3 Update：融合研究与技术设计

**状态：2.3 Update 技术 COMPLETE。A 已验收并提交 `d9f1b6c`，B 已验收并提交 `88cd76f`；C 已实施并通过最终技术门禁，待用户验收和 Git 收口。2.4 Timer 未启动。**

本方案的总体设计已获批准，批准不等同于批次实施、Git 写入或下一专题授权。统一公共更新订阅与执行安全，保留领域调度；不是把所有 Unity 消息、任务或 MonoBehaviour 合成一个系统。

## 1. 基线与边界

- 2026-10-10 实际核对：`integration/xf-yl`，HEAD `414cf82`，研究开始时工作区干净；该提交为 2.2 Events 集成里程碑，E 提交为 `5674cf3`。
- 已读取根 `AGENTS.md`、长期蓝图、阶段 2 总体规划，复用 Pool/Events 与阶段 1 的已验收结论。既有蓝图和阶段 2 文档仍写 Events 待 Git 收口，本轮以实际 Git 历史和用户确认覆盖该历史状态，不顺带修改旧文档。
- Unity `2022.3.62f3c1`；manifest 的 Test Framework 为 `1.3.9`。原 XF 来源为 `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，lock revision `2182708aecc596f30739fa8634fe2b9b6946ddd7`。原 Package 严格只读。
- 当前 `EditorSettings.asset:27–28` 为 `m_EnterPlayModeOptionsEnabled=0`、保存的 options=3；不能据 options 值声称当前关闭了 Domain Reload。
- 本轮仅静态读取源码、直接消费者、程序集、相关测试及必要历史结论；未运行 Unity 测试、刷新、编译、Play 或构建。源码可推导的问题不冒充运行复现，历史测试不代表未来实现通过。
- 不修改原游戏，不展开完整 Timer、Audio、Resource、UI 或 Action 重构。Module/MVC 关闭顺序、View/Panel 会话与直接退出管线、Action 租约/组合/暂停/取消、Events 身份/快照/异常语义均为保护边界。

源码简称：`C` = `Assets/YLFramework/Runtime/XFMain/`；`Y` = `Assets/YLFramework/Runtime/`；`F` = `Y/Shared/`；`T` = `Assets/YLFramework/Tests/`；`XF` = 上述原 XF 包。行号对应本次研究基线。

## 2. 双方实际能力与职责

| 机制 | 源码确认的能力与用途 | 融合取舍 |
|---|---|---|
| XF / Core `UpdateManager` | `XF/Runtime/Manager/UpdateManager.cs:27–149` 与 `C/Manager/UpdateManager.cs:26–148` 同一结构：惰性 DDOL 宿主、无参 Update/Fixed/Late、多播委托注册与移除。Core 只替换 namespace 和 ApplicationTool 来源 | 保留简单三相位、无参 Action、按注册顺序的使用方式；改善订阅身份、异常隔离、停止非创建和会话安全，公共能力下沉 Foundation |
| YL `ActionGlobalHost` | `Y/Action/Internal/ActionGlobalHost.cs:11–25,42–53,96–117`：七种全局消息、Action 自有协程和 StartGlobal 支撑；owned EasyEvent 已复用 Events 的唯一 channel | 三种帧消息可共享底层相位；owned 消息上下文、GUI/Focus/Pause/Quit 与协程责任保留，不把 Action.Shutdown 扩成公共 Update 清理 |
| Action 执行器 | `Y/Action/Execution/MonoUpdateActionExecutor.cs:269–309,311–427`：GameObject 的 Update 推进任务，逐任务选择 scaled/unscaled delta，租约校验、首步同步执行、取消与延后清理 | 是领域执行器，不是另一套公共消息总线；对象/场景/全局任务范围继续由 Action 管理 |
| Action 延迟回收 | `Y/Action/Internal/ActionRecycleDriver.cs:25–58`：首次入队才建驱动，Update Drain，Shutdown/退出额外 Drain | 回收时机与用户回调栈有关，不能换成公共 Pool 立即归还，2.3 不强迁 |
| Core Timer | `C/Manager/TimerManager/TimerManager.cs:288–330,1149–1157`：自身 Mono Update 倒序推进秒/帧计时；CoroutineStarter 仅用于等待协程结束 | 保留独立领域驱动，调度与终态缺陷归 2.4 Timer |
| YL Timing | `Y/Tools/Timing/Timer.cs:79–130,363–423`：独立宿主、双时钟/双堆、手动 UpdateMgr、更新重入守卫、作用域与会话清理 | 已有完整调度职责，2.3 不更换驱动，不把 Action.Delay 改成 Timer |
| Core AudioController | `C/Manager/AudioController.cs:168–208,307–365`：实际订阅 UpdateManager，按 unscaledDeltaTime 更新逻辑播放时间；Remove/Source 失效退订，Stop 不退订 | 作为公共 Update 的既有消费者做最小适配；不改变计时、暂停、循环、完成或 Source 所有权 |
| YL Audio | `Y/Audio/Internal/AudioManager.cs:305–373`：独立 Update，完成检查、fade/crossfade、请求异常隔离与领域清理；AudioVoice/AudioMusicPlayback 还使用 PCM 位置和各自时钟 | 不迁公共驱动；这不是多播更新器的重复实现，原游戏现有 Audio 路径保持 |
| Shared CoroutineStarter | `F/CoroutineStarter.cs:20–59`：非 Mono 调用者 Start/Stop，独立 DDOL 宿主，切场景不停协程 | 修执行宿主边界，保持与帧消息桥独立，不建通用异步调度器 |

### Core 直接消费者的真实作用

- `C/Manager/ViewManager.cs:17–39,247–254`：首个受管 owner 订阅存活扫描，最后一个 owner 退订；处理从未激活、没有 OnDestroy 的对象。必须保留主动扫描，不改成仅靠销毁事件或查询收尾。
- `C/Manager/PanelManager.cs:157–174,417–448,450–499,647–679`：LateUpdate 实际是 Panel 队列的失活兜底，不是每帧排序；AlwaysInFront 排序由 AddOrder 等显式调用触发。Activate/Deactivate/Release 的直接生命周期维护仍是主路径，不能恢复广播依赖。
- `C/Other/CloseView.cs:29–42` 的对象启用后计时和 `C/Other/Adaptation/AutomaticAdaptationCanvasScaler.cs:6,38–40` 的 EditMode 适配不迁全局运行宿主；后者还需要编辑状态更新。
- `Y/Resource/Framework/ServiceManager.cs:255–266` 是 dirty 资源清理；`Y/Tools/FSM/Core/FSM.cs:235–305` 是调用者显式驱动的领域方法，不是 MonoBehaviour 自动消息。仅凭方法名 Update 不能判定重复宿主。

### 重叠与互补

公共重叠仅为无所有者对象也可订阅的三种帧相位及其生命周期安全。XF 的轻量三相位 API，与 YL 的精确注销、owned channel、作用域取消和宿主所有权思想互补。动作树、计时器队列、音频播放状态、资源脏表、局部对象生命周期并不重叠，不为了形式统一迁移。

## 3. 源码问题分类

| 类别 | 依据 | 影响与本专题处理 |
|---|---|---|
| 确认缺陷 | UpdateManager 的三个 Remove 均访问创建式 Instance（119–148）；CoroutineStarter.Stop(non-null) 同样访问 Instance（53–59） | 无宿主退订/停止可创建新对象；2.3 必修，只读取既有 owner，不查找/创建替代宿主 |
| 确认缺陷 | UpdateManager.Add(null) 也先创建宿主（83–112） | 无效操作产生副作用；null 注册返回共享空 token，不创建 |
| 确认机制限制 | 三相位直接 multicast.Invoke（64–76） | 首个异常阻断后续订阅，同相位其他领域可能长期饥饿；推荐公共驱动显式继续并汇总，属于有意行为变化 |
| 确认机制限制 | `+=/-=` 无注册身份、重复注册允许、只移除最后相等委托序列 | 调用者容易少退订或误退他者；保留委托便利方式，新增精确 token 为首选，不自动去重 |
| 确认缺陷 | Panel MultiPanelListener 默认 true（106），构造不注册；setter 同值立即返回（165），唯一 Late 注册在 false→true | 默认开启不建立兜底更新订阅。直接 View/Panel 生命周期已覆盖大量正常路径，不能把缺口夸成所有 Panel 排序失效；本专题补齐订阅状态，不重写队列算法 |
| 确认安全缺口 | UpdateManager/CoroutineStarter 无退出屏障和会话清理；ApplicationTool 在 Editor 使用 will-change 状态，在 Player 恒 true（19–27） | 现有 gate 不足以拒绝进入过渡期/退出创建；2.3 给这两种执行宿主加窄门禁，不全局改变 ApplicationTool 含义 |
| 源码可确认的恢复限制 | Core Audio 只在构造订阅；View 只在 sceneOwners 从零变化时注册 | 强制销毁公共宿主后，存活消费者不会自动重订阅；正常路径不得销毁共享宿主，外部破坏后的领域自动恢复不作隐含承诺 |
| 未验证风险 | CoroutineStarter 的裸 Coroutine 无 owner/session 跟踪；Stop 路由到当前 Instance | 旧 handle 被送往新宿主是源码确认的错误路由；是否会误停新协程取决于 Unity 原生行为，本轮不声称已发生。设计上拒绝错 owner，而非依赖原生容错 |
| 未验证风险 | 没有会话清理的 static 引用/列表，注释把 Unity fake-null 等同会话重置 | Domain Reload disabled 下旧订阅、旧静态扫描责任或旧宿主回调可能残留，须实际两轮 Play 验证；fake-null 不能证明委托和纯 C# 数据已清 |

### 跨专题发现，仅登记边界

- Core Timer 回调先于终态标记（TimerManager:135–152），异常可重复触发一次任务；宿主销毁不清 IsRunning（260–269,314–330），旧任务重投递可能被拒绝。归 2.4，不用换 Update 宿主掩盖。
- Core AudioPlayerExtension 缓存卸载 Coroutine，仅用 null 判断且无 Stop（158–205,276–299）；Starter 被外部销毁后不能自动恢复检查。归 Audio/资源协作，不把 2.3 描述成已解决卸载生命周期。
- Action 初始化补丁重入创建宿主/任务后抛错，catch 只清模块引用（ActionKitLifecycle:43–60），不是完整回滚；执行器 ReleaseFinished 在深度归零后移除项再调用 Deinit（355–362,386–424），兄弟任务取消可重入修改同一列表。这些是可靠源码路径但未运行复现；不随三相位桥接修改组合/租约算法，后续 Action 定向修复另行授权。
- Action 退出时回收驱动 Drain 与执行器 OnDestroy.StopAll 没有跨对象统一先后契约。迟到入队是否残留须定向运行确认；保留现有显式 Shutdown 管线，不凭公共桥接宣称所有退出资源均已归还。

## 4. 推荐架构与技术取舍

### 4.1 公共层：一个现有消息桥，三个独立 channel

把现有 `UpdateManager` 的公共实现迁入 Foundation，推荐正式类型 `YLFramework.Foundation.UpdateManager`。沿用具体 MonoBehaviour 消息桥，不新增通用宿主基类、调度管理器、任务索引或 PlayerLoop 注入；这只是对三种同职责消息的合并选择，不要求其他领域共用此 MonoBehaviour。

- 每个相位持一个非池化 `EventChannel`；复用已验收的 COW、精确 token、multicast 展开和 owner 代次。它们是私有实例 channel，不进入 `EventBus.Default`，没有 string/int 键、协议冲突或全局 Clear 的连带影响。
- 仅有效注册在实际 Play 且非 stopping/quitting 时惰性创建，自有宿主命名为 `YLFramework.Foundation.UpdateManager`，DDOL，不搜索或收养同名游戏对象，不依赖 YLFrameworkRoot。
- 末个订阅移除必须释放委托/token 对 owner 的责任，正常帧不派发空 channel；保留空闲宿主到当前 Play 会话结束，不 Destroy/recreate 抖动。不为了避免三个空消息入口增加一套监听变更控制框架，是否值得禁用空闲组件留性能证据决定。
- 不提供公开 Trigger、手动 Tick、公共 ClearAll 或领域 Shutdown；更新相位由 Unity 驱动。业务不得销毁或停用这个自有基础宿主，领域停止只释放自己的 token。
- 异常/销毁清理以当前宿主身份和会话代次为界。外部强制销毁终结该 owner 的 channel/token；随后有效注册可建新 owner，但不自动把旧 callback 复制过去或重启领域任务。该限制需文档化；若需要故障恢复，再单独设计，不能在 OnDestroy 中自我复活。

### 4.2 Events 与 Pool 的关系

`F/Events/EventChannel.cs:17–32`、`EventRegistrations.cs:20–32,94–151`、`EventChannelBase.cs:20–45` 已提供所需算法：不重复造订阅表或 Once，也不修改 Events 默认广播行为。公共三相位使用 `TriggerAll`，在 Unity 消息边界将单个原异常或聚合异常集中 `Debug.LogException`；失败订阅保持，下一次仍可执行，不自动取消或静默吞错。

正常更新不借 Pool、不创建 payload 数组、不 GetInvocationList；注册/退订的 COW 分配是现有 channel 的已知成本。channel/token 不池化，避免旧别名操作新身份。Pool 继续服务 Panel 临时集合和 Action 领域缓存，ActionQueue 继续控制延迟归还，公共 Update 不代管这些责任。

### 4.3 Action：共享相位，不合并上下文或调度

推荐把 `ActionGlobalHost` 的 Update/Fixed/Late 自动通知改成公共三相位的桥接，移除其重复自动消息转发。保留当前七个 owned EasyEvent 与 ActionKit 返回类型；不让 `ActionKit.OnUpdate.UnRegisterAll/Dispose` 直接操作公共 channel。

- 每个有效启用宿主持三份公共相位 token，按宿主启停建立/释放；owned event 没有 listener 时不再触发内部发布。不为按 listener 数量连接而派生第二套事件注册适配，避免 listener-change 失败影响已提交注册；这是生命周期共享，不宣称减少所有空消息调用。
- 仅在 ActionKit 初始化成功后建立连接；OnRegisterPatch 重入取得的 host 在初始化未成功时不接公共源，成功提交后非创建式刷新已有 host。连接失败只回滚本轮桥接 token，保留原错误；不借此扩成全部 Action 初始化事务修复。
- disabled/inactive 的 Action 宿主不自动通知；重启时只建一份连接。桥接 callback 必须检查捕获的 host 存活、HostReleased、当前连接轮次、enabled/activeInHierarchy 及 ActionKit 关停状态，不能在回调中访问创建式 Instance。
- 先终结连接轮次再退订，必要清理全部尝试；普通公共 token 退订仍保留在途快照，因此轮次 guard 必须拒绝旧桥接。owned event 继续 ResetOwner 截断内部旧快照，保持 Shutdown→同帧新宿主隔离。
- owned Action event 仍用 Trigger/fail-fast：其首监听异常阻断该 Action 消息的后续监听，但公共驱动继续其他公共订阅并报告异常。直接手动调用 owned Trigger 的传播契约也保持。
- OnGUI、Focus、Pause、Quit 仍由 Action 宿主原生接收；OnGUI 不是每帧一次，不应伪装第四种更新相位。退出通知先设置共享会话退出标志和 Action 宿主退出屏障再通知用户，防止监听 Shutdown 后局部标志重置而重建宿主；不改变焦点/暂停 payload，也不承诺 Unity 在任意平台或无焦点状态都发送相同次数。
- Action 自有协程、MonoUpdateActionExecutor、CurrentScene/Global 范围、ActionRecycleDriver 与执行/回收顺序均不迁。源码没有既定的“全局消息→执行器→回收驱动”顺序，三份脚本 executionOrder 都为 0；不可凭桥接新增此保证。

### 4.4 Core 消费者的最小接入

- ViewManager 持有存活扫描的精确 token；零→非零建立、非零→零释放，保留每 module 的事务与异常隔离。执行会话 reset 只摘旧 token 和 sceneUnloaded 监听、清旧 sceneOwners/pruneOwners bookkeeping；不主动调用业务 Close、清 Module 的领域索引或其他模块事件，新会话由正常纳管重新登记。
- PanelManager 持自身 Late token/轮次，在 `MultiPanelListener && 有队列工作` 时确保连接，队列归零或设置关闭释放。创建默认 true 状态也可连接，不依赖 false→true setter。只修接入，不把排序改为每帧执行，不替代直接 Activate/Deactivate/Release。
- Core AudioController 持本次注册的 token，Source 失效/Remove 精确释放，移除构造中的“先 Remove 再 Add”习惯。必要的退出 guard 拒绝已移除 controller 的在途调用，不重写播放时间、完成回调或 Stop 的含义。
- Core 管理器/消费者重建时只结束自己的订阅轮次；旧 callback 不访问会创建新 PanelManager/宿主的属性。关闭 Module 不结束调用者独立持有的 Action、Timer、音频或 Shared 协程。

### 4.5 保留独立领域驱动

Core Timer、YL Timing 和 YL Audio 继续各自 Mono Update；职责边界、时钟、启停所有权和异常策略已有具体差异，共用 Foundation 可以是未来选择，但本专题没有收益证据要求迁移。CloseView、AudioPlayerAutoPause、EditMode 适配、资源 dirty 清理及外部显式 FSM 驱动也保持。

## 5. 行为与生命周期契约

### 5.1 注册、相位、顺序和重入

| 项目 | 推荐契约 |
|---|---|
| API | 保留 AddUpdateEvent/AddFixedUpdateEvent/AddLateUpdateEvent(Action)，返回 `IUnRegister`；保留对应 Remove(Action) 便利方法，精确 token 为首选。无 callback 不建宿主，清理幂等非创建 |
| 重复注册 | 每次有效注册都是独立身份，重复允许；token 只撤自身，委托 Remove 沿用最后相等普通子序列，不隐式去重 |
| 普通订阅变化 | 按共享 Events 快照：本相位发布开始捕获列表；新增不进入该快照，普通取消仍可能在该快照执行一次。相位开始前完成注册可进入该相位，因此 Update 内新订 Late 可在同一渲染帧收到 Late |
| owner 结束 | 会话 reset、宿主销毁/释放终结 owner 代次并截断在途快照。对象/领域结束必须有自身 guard，不能把普通退订等同立即停止旧快照 |
| 顺序 | 同一 channel 按注册 target 顺序；不加 priority。只承诺各回调处于对应 Unity 相位，不承诺与独立 MonoBehaviour/物理/其他框架之间的全序 |
| 时间 | 无参 Action 继续由消费者取适用的 Time 值；Fixed 不伪装 rendered-frame Update，timeScale=0 时不能据 Update 仍到来推断 Fixed 也到来。不共享一个 delta 取代 Timer/Audio 时钟 |
| 重入 | 不公开公共发布入口，订阅/退订、关闭领域及回调中重建须安全。Action owned Trigger 的合法嵌套仍遵循 Events，不通过全局防重入开关吞掉它 |
| 异常 | 公共相位逐 target 继续、末尾统一报告；不自动删除失败项。Action/Events 普通广播 fail-fast 不变，领域必要清理继续尝试并保留错误责任 |
| 线程/所有权 | 注册、退订与 Unity 宿主操作仅主线程；不承诺线程安全或弱引用订阅。可使用已有 Events 生命周期取消辅助，但从未激活对象的 OnDestroy 限制仍存在 |

### 5.2 进入、场景、退出与旧宿主

- 复用 `F/Internal/SingletonSessionState.cs:25–62` 的 session reset/退出标志，给公共 Update 和 CoroutineStarter 增加最小内部执行门禁及代次；不下沉 Runtime 的 MonoSingletonHost/Root，不建第三套全局会话管理器。
- 创建条件是实际运行且本执行会话未关停，不用 ApplicationTool 的 will-change/Player 恒 true 作为唯一证据。Editor ExitingPlayMode 提前关闭执行门禁，Player Application.quitting/OnApplicationQuit 在用户通知前关闭；进入新会话才重开。
- gate 关闭后只做非创建式注销/停止；新 Update 注册返回空 token，CoroutineStarter.Start 返回 null，保持既有“当前不可运行时不启动”的形状。创建过程失败须回滚本次自建对象及订阅并传播原始错误，不能返回貌似已注册的 token。
- SubsystemRegistration 终结旧 channel/token、协程 owner 表和旧宿主身份；旧宿主失去驱动权限，及时禁用/清理自有对象，不能仅把 static 置 null 留旧组件继续跑。reset 和迟到 OnDestroy 都只操作所捕获旧 owner，不能清新宿主或重关新会话 gate。
- 正常场景切换不结束公共订阅或 Starter 协程；领域对象按自己的 Scene/对象所有权退订。公共 DDOL 不代表所有 callback 的目标对象都仍有效。
- 基本门禁覆盖关闭 Domain Reload 的连续 Play，正常 Scene Reload；若同时关闭 Scene Reload，基础宿主仍必须隔离旧代次、不得双驱动，但不承诺自动恢复旧场景中已存在的 Module/View/Action 会话。发现存活领域对象需要恢复时另行提出，不把 2.3 扩成全框架 reload 引擎。
- 公共宿主不在末订阅、Remove、Stop 或 OnDestroy 中重建。外部删除/停用基础宿主是破坏驱动，不承诺热自愈；尤其已有 Core Audio controller 不会自动恢复，不能用“下次创建成功”宣称旧工作已继续。

### 5.3 CoroutineStarter 的窄安全改进

保持 `Start(IEnumerator) -> Coroutine` / `Stop(Coroutine)` 与独立 DDOL 行为，不统一 Action 协程宿主，不把停止协程解释为请求 Abort 或生产者物理取消。

- Start 捕获实际 host+会话代次；Stop(null)、无记录、旧会话或已销毁 owner 都为空操作，绝不经当前 Instance 查找/创建。
- 对 Starter 返回的 handle 保存原 host/代次归属，推荐弱键 `ConditionalWeakTable<Coroutine, owner记录>`，避免为兼容裸 Coroutine 而强保留所有已完成 handle。Stop 只路由到该有效原 owner，反复停止安全；新会话旋转表，旧 handle 不碰新协程。不重写迭代器以维护一个通用协程状态机。
- StartCoroutine 可能在返回 handle 前同步推进用户迭代器；登记和返回须复核捕获 owner 是否仍有效，不能把同步重入销毁/会话变化后的结果登记到新 owner。需要停止失效返回句柄时仍用捕获原宿主，不重取 Instance。
- 宿主销毁/会话终结停止它自己的协程，旧 OnDestroy 不操作新对象。不添加公共 StopAll，不在 Update 宿主结束时停所有 Starter 协程。
- 不承诺 Coroutine 自然结束时发送取消通知、iterator finally 在所有原生停止场景执行，或中断 XFAB 等其他宿主；HTTP、Module/View pending、Audio 卸载等待的领域终态仍由原调用方负责。

## 6. API、程序集与迁移影响

- Foundation 现有 references 为 `[]`，Core 和 Runtime 已直接引用 Foundation；公共 Update 下沉不需要新 asmdef 或 Core→Runtime / Foundation→Core 的反向引用。Events 与 Update 在同一基础程序集内复用，但 namespace/职责仍分别表达。
- 推荐单一正式 `YLFramework.Foundation.UpdateManager`，退出 Core 的同职责类型和已编译禁止的 onUpdate/onFixedUpdate/onLateUpdate 字段，不保留永久 facade。显式旧 namespace、返回 void 的方法组赋值以及把 UpdateManager 当组件查找的调用须适配；普通忽略返回值的 Add 调用保持可用。
- 实施前定向复核脚本 GUID `9f463ae1c4e1b464691b55eff7c2ce36`、类型字符串、UnityEvent/SerializeReference、资产和反射。当前 Assets 定向检索未发现该 GUID/完整旧类型名资产绑定；不能据此证明仓库外用户没有绑定。移动现有脚本连同自身 meta/GUID，保留 XF MIT 来源与适用 LICENSE，不手改原包。
- Core ViewManager、PanelManager、AudioController 与 `T/Core/PlayMode/CoreCoexistencePlayModeTests.cs:27–55` 的类型查找须同步调整 Foundation 引用/alias。测试清理只销毁本 fixture 新建、且无外部责任的宿主，不让迁移后的 fixture 清公共用户。
- ActionKit 的七个 EasyEvent 属性、ActionController/Handle/UpdateModes、Timer/Timing/Audio API 与 Optional DOTween 边界不变；桥接只消费既有 Runtime→Foundation。必要 XML 契约及现有模块说明随获授权实施更新，本轮不编辑非计划文档。

## 7. 备选与推荐理由

| 选择 | 推荐 | 合理备选与代价 |
|---|---|---|
| 公共算法 | 私有 EventChannel 三相位，非 EventBus | 自建可变列表/空洞/延迟增删可降低高频注册成本，但重建身份/快照/异常规则；只有测量证明必要才考虑 |
| 驱动方式 | 沿用现有具体 MonoBehaviour 桥 | PlayerLoop 可更精确定位相位，但要管理 loop 安装/卸载与其他库共存，当前无收益证据；所有领域单一 Mono 会扩大启停、时钟与异常责任 |
| 公共异常 | TriggerAll 后消息边界 LogException | 保持 multicast fail-fast 更少行为变化，但一个 callback 可阻断 View 扫描等无关领域。全框架 fail-fast→continue 则违反已验收 Events/Action，不推荐 |
| Action 共享幅度 | 仅三个全局帧消息桥接，保留 owned events | 完全保持独立消息桥风险更低，但新 YL 仍有同职责自动三相位转发；全体执行器/回收也迁中央会改变领域契约，无当前收益证据 |
| 其他 Unity 消息 | GUI/Focus/Pause/Quit 保留 Action 宿主 | 暴露新公共七消息 API 需要处理编辑状态/通知次数/退出订阅顺序，目前没有 Core/其他公共消费者证明必要；保留能力不等于全都搬底层 |
| 空闲/损坏宿主 | 会话内保留空闲，外部破坏明确结束 owner | 末监听销毁会增加重建和迟到清理竞态；损坏后透明恢复必须定义领域重连与任务恢复，不能只重建对象。均不因“少一个对象”默认采用 |

## 8. 实施批次与最小验收

以下 A→B→C 三批路线已获总体审阅批准，各批仍待独立实施授权；每批是完整可验收的变更轴，不按私有类或小补丁机械拆分。测试先复用现存结构，仅补关键新行为；不重跑所有 Pool/Events/Core 历史套件。

| 批次 | 范围与交付 | 最小验收方向 |
|---|---|---|
| A 公共相位与 Core 接入 | Foundation 唯一三相位、精确 token、非创建式操作、窄 gate/session、命名/身份迁移；View 扫描、Panel Late 接入与 AudioController 必要适配 | 三相位真实 Unity 调用；顺序/重复 token、普通快照新增取消、owner 截断、异常继续；null/无宿主 Remove、不在 Play/退出不创建；复用 View inactive 销毁扫描、Panel 激活/停用/队列和同组件旧会话断言；补 Core Audio 持续驱动/Remove/Source 失效，不测完整播放系统 |
| B Shared 协程宿主安全 | Starter 非创建 Stop、原 owner/代次路由、同步 Start 重入和退出安全，复用 A 的窄执行门禁 | 复用 CoroutineStarterTests 的独立宿主/场景保活/定向 Stop/销毁再 Start；补宿主死后 Stop(old) 不创建、建新后 Stop(old) 不影响新协程、退出 Start 拒绝及旧 OnDestroy 不清新宿主。既有 CustomAsyncOperation.Abort 不取消 producer 断言保持 |
| C Action 消息桥接与专题收口 | 三种 owned 消息按宿主生命周期连接公共相位、inactive/disabled/轮次 guard、退出通知屏障；其余消息/协程/执行器/回收独立；收口现有文档、身份和两轮 Play | 复用 GlobalMessagePlayModeTests、ActionKitLifecyclePlayModeTests 的真实三相位、Shutdown重建/旧快照/借用inactive；补 disabled/inactive桥接不通知、清Action消息不清Core、公共异常隔离仍保留Action内部fail-fast、同帧重建一份桥接、初始化失败不新增桥接残留。真实关闭Domain Reload两轮Play验证公共/Starter/Action旧身份隔离与退出不重建；不把SendMessage(Quit)当真实退出 |

### 现存验证入口与缺口

- `T/Foundation/PlayMode/CoroutineStarterTests.cs:44–129` 已覆盖基础宿主、指定 Stop、跨场景、销毁后新建；`T/Foundation/EditMode/FoundationBoundaryTests.cs:15–56,59–75` 覆盖程序集与旧 ApplicationTool 契约，后者保持，不用削弱测试来改全局语义。
- `T/Core/PlayMode/CoreDirectViewPlayModeTests.cs:603–639` 覆盖从未激活的销毁扫描；1046 起的 inactive Panel、排序/Canvas 与既有 Loaded/关闭重入用例按实际方法筛选。只选择桥接直接影响的代表项。
- `T/Core/PlayMode/CoreCoexistencePlayModeTests.cs:17–58` 现只覆盖 Audio prefs/借用 Source 所有权，不证明逐帧播放计时；A 需一项最小直接消费验证。
- Action `GlobalMessagePlayModeTests` 的 Update/Fixed/Late 是真实帧验证，GUI/Focus/Pause/Quit 是 SendMessage；Lifecycle 的 shutdown 内重建、borrowed inactive 保活与旧 token 可复用，另补实际 inactive 自动驱动断言。
- TimingPlayModeTests、Audio 的 fade/crossfade/Shutdown 用例仅在驱动或门禁差异实际影响它们时定向选择，不因共处 Runtime 机械全跑；独立驱动未改时，源码差异与依赖检查足以证明本批未迁它们，不冒充新的运行通过。
- 各批必要 Unity 实际编译与定向 EditMode/PlayMode分别报告；检查 diff、meta、程序集、源许可、原 Package/游戏/配置无意外差异。收口验证实际无 Domain Reload 连续 Play，测试后恢复用户设置与场景，不保存用户未保存内容。
- Player/IL2CPP/完整游戏不作为每批固定门禁；依据宿主退出/类型迁移真实风险在阶段收口安排代表性验证。原 Logger/MCP 过渡问题仍属外部已知问题，不改原包或吞日志，也不把静态推导当 Player 退出通过。

## 9. 性能与主要风险

- 选择 channel 是为了复用正确性，不宣称比多播委托更快；稳定帧路径无新增注册、无键查找、无 payload 打包，COW 的成本集中在注册/注销。频繁启停桥接可能分配，是否改存储或做按 listener 连接只能依据目标 Player 的 Profiler/分配和耗时测量。
- 不因多个 MonoBehaviour 就判为瓶颈。若后续需要比较，测空闲、稳定订阅、频繁启停三种代表负载，并计入桥接、领域 guard 与回收；Editor 单次计时不作为通用性能结论。本轮不开展基准设施或性能测试。
- 最大行为变化是公共更新异常继续、API 类型归属和停止非创建；最大集成风险是退订仍在快照中、旧宿主/旧 token 进入新会话、Action 借用 inactive 被中央桥意外激活、Panel 默认注册缺口修正后的调用时机。
- 不建立公共全序、损坏自愈或全领域退出事务。Core Audio/卸载协程和 Action/Timer 跨专题问题保留明确后续责任，不能把共享底层当作这些问题的解决证据。

## 10. 审批状态与重大决策

**2026-10-10：用户通过总体审阅，正式批准以下五项推荐方向及 A→B→C 三批实施路线。**确认范围为总体架构、功能边界、行为契约和验收方向；具体 API 内部设计、数据结构及实现步骤仍由实施时根据实际源码自主决定，不要求机械照搬候选细节。

1. 接受 Foundation 唯一公共三相位与正式类型迁移，不保留 Core 永久 facade；独立领域驱动继续存在。
2. 接受公共 Update 逐 target 继续并统一日志报告，Events/Action 内部默认 fail-fast 不变；接受普通取消保留快照、owner 结束截断且领域自身 guard 必须保留。
3. 接受 Action 仅桥接全局 Update/Fixed/Late，保留 owned 消息、其他 Unity 消息、自有协程、执行器与延迟回收，不新增执行全序。
4. 接受空闲宿主会话内保留、外部破坏结束 owner 不透明恢复，以及基础无 Domain Reload 隔离不等于全框架无 Scene Reload 恢复。
5. 接受 A→B→C 的范围与最小验收，Timer/Audio/Action 的跨专题缺陷另行授权，不借本方案提前重构。

### 已批准的补充实施要求

- UpdateManager 从 Core 迁入 Foundation 时，根据实际源码、资源身份和通用框架 API 价值处理兼容问题，不以当前游戏没有使用作为唯一判断依据。第 6 节的统一正式入口与不保留永久 facade 方向已批准，但必要的身份迁移和兼容措施仍须以实际证据决定。
- Action 三相位桥接必须保护已有执行时机、停用、退出及会话身份契约，不擅自建立跨领域执行顺序。
- 继续坚持融合优先、质量优先、面向通用 Unity 框架与最小充分测试；复用已完成的 Pool、Events 和 Core 结论，不重新开展这些专题研究。

### 实施与验证状态

- 2026-10-10，用户独立授权实施 A。实际基线为 `fusion/2.3-update`、HEAD `463feb5`，实施开始时工作区干净。本批变更尚未提交，未执行 Commit、Push、Merge、Reset、Clean 或切换分支。
- A：正式公共类型为 `YLFramework.Foundation.UpdateManager`，实现从 Core 移入 `Runtime/Shared/`，三个私有 `EventChannel` 复用既有 Events 注册、精确 token、COW 快照、owner 终结及 `TriggerAll`；退出 Core 同职责类型及禁止编译的三个旧静态字段。仍提供无参 Add/Remove 三相位便利 API，Add 现返回 `IUnRegister`，允许重复注册；显式旧 namespace/程序集、返回 void 的方法组绑定需要调用方迁移。
- 公共宿主仅有效 Play 注册惰性创建，DDOL、相位内注册顺序、主线程使用；null/非 Play/退出注册返回共享空句柄，Remove/token 注销非创建。空闲宿主保留到会话结束；异常逐 target 继续并汇总报告，失败项不自动删除，Events 默认 fail-fast 未改。普通取消保留当前快照，销毁/reset 终结 owner 并截断旧快照；外部销毁结束旧订阅，后续新注册不恢复旧领域工作。
- 基于既有 `SingletonSessionState` 增加窄执行 gate 与 session 身份，Editor ExitingPlayMode 提前关闭，Application.quitting/当前宿主 Quit 拒绝重建；进入新会话终结、停用并销毁旧公共宿主。旧宿主迟到的 Destroy/Quit 不清新实例、不关闭新会话。没有公共 Tick、ClearAll、Shutdown 或新通用宿主层。
- ViewManager 持有扫描 token/轮次，继续扫描从未激活的失效 View；reset 只清扫描/sceneUnloaded/bookkeeping，不主动 Close 业务或清 Module 索引。PanelManager 在默认 true 且队列有工作时连接 Late，队列空或关闭开关退订；旧轮次回调非创建且不能维护新实例，失活兜底复用直接 ReleaseQueue，排序和直接 Activate/Deactivate/Release 保持。AudioController 精确持有自身 token，Stop 保持持续驱动，Source 失效/Remove 先终结更新责任，释放钩子失败仍撤销订阅并释放自有 Source；保留原 unscaled 时间、暂停、循环及借用 Source 所有权。
- 迁移保留 Update 脚本 GUID `9f463ae1c4e1b464691b55eff7c2ce36` 及原 `.meta`；定向检索没有既有场景、Prefab、asset 或旧完整类型字符串绑定。Foundation/Core/Runtime asmdef 和依赖方向未变，Shared 已有 XF MIT 完整许可证覆盖迁入实现。游戏业务、持久场景/资源、Packages、ProjectSettings 和原 XF Package 未修改。
- Unity `2022.3.62f3c1` 实际导入/编译无错误；EditMode **15/15**（Update 边界 2、既有 Foundation 边界 5、Core 身份/依赖 8）通过。精确筛选的同一 Play 会话 **19/19** 通过：公共 Update 契约 6、Core View/Panel/Module/Session/回滚代表用例 10、Core Audio 更新/释放 2、既有 Audio prefs/Source 所有权 1；没有运行完整历史套件。测试工具 TotalTests 字段包含未选中的根树用例，以上数量依据真实执行结果与通过数。
- 已先运行回归确认原实现的非创建操作、token、Panel 默认 Late 缺口及 Audio 释放异常问题，再实施修复。独立审查发现 Core→Foundation 同一 Play 会话的测试宿主残留问题，已真实复现并修复：测试仅清本 fixture 新建且三个相位均无监听的宿主，失败的 Foundation SetUp 不接管已有宿主；组合运行及只读复查通过。新增脚本的 `.meta` 由 Unity 导入生成。
- 本批验证的会话 reset/旧消息/退出门禁为定向模拟，加上真实 Unity 三相位和场景卸载；未将 SendMessage(Quit) 描述为真实退出验收。未修改 Domain Reload 设置，真实关闭 Domain Reload 的两轮 Play 综合验收仍归 C；没有 Player/IL2CPP 构建、完整游戏或性能验证。本轮不声明宿主损坏自愈、全框架无 Scene Reload 恢复或跨领域全序。
- A 已达到本批技术完成标准，等待用户验收与 Git 收口。B 的窄 gate/session 前置能力已就绪，但 CoroutineStarter 尚未接入，旧 Coroutine handle 所有权/Stop 和重入安全仍归 B；Action 三相位仍保留原自动消息，桥接及专题最终收口归 C。Timer、YL Timing/Audio 驱动与既已登记的外围缺陷保持原范围。

### 2.3-B 实施与验证

- 2026-10-10，用户独立授权实施 B。实际基线为 `fusion/2.3-update`、HEAD `d9f1b6c`，开始时工作区干净，A 已由用户验收并提交。本批仅修改 Shared `CoroutineStarter.cs`、既有 `CoroutineStarterTests.cs` 与本计划；未新增文件或改动 `.meta`、asmdef、公共 Update/Events/Pool、Core/Action 消费者、原 XF Package、游戏业务、场景/资源、Packages 或 ProjectSettings。
- 保留 `Start(IEnumerator) -> Coroutine` / `Stop(Coroutine)`、Unity 同步首步与 yield 行为、独立命名 DDOL 宿主和正常跨场景运行。复用 A 的 `SingletonSessionState.CanRunExecution`、`ExecutionSession` 及 reset 注册，不引入新的全局管理器、迭代器状态机、公开 StopAll 或异步取消系统。
- 句柄归属使用 `ConditionalWeakTable<Coroutine, CoroutineStarter>`：弱键不强保留全部已完成句柄，原宿主自身保存不可变的会话身份和释放标记，无需额外 owner 记录类型。Stop 先移除登记再调用有效原宿主，重复 Stop、null、无记录/外部句柄、旧宿主和旧会话句柄均非创建式；不把裸句柄交给当前替代宿主。自然完成后保留的句柄仍可安全 Stop，不影响同宿主新工作。
- Start 捕获实际宿主，Unity 返回前允许用户同步重入；返回后复核宿主存活、当前 owner/会话、GameObject 活跃状态与门禁，只登记有效原宿主的句柄。同步 reset、Quit、销毁后新建或失活时返回 null，必要停止只访问捕获的原宿主。null 表示没有可用句柄，不保证迭代器从未推进或没有业务副作用。参数错误保持 Unity 原异常；启动失败只回滚本次新建的原宿主，不终结已有宿主的其他工作。
- 宿主销毁、当前宿主 Quit 和会话 reset 先撤销 owner/登记，再停止自身全部协程；reset/Quit 额外禁用并销毁自有旧对象。当前宿主 Quit 先关闭共享门禁，旧宿主迟到的 Destroy/Quit 不清新实例/新归属表、不关闭新会话。无宿主 Stop 不创建，既有失活宿主不被替换；损坏后的新 Start 不恢复旧任务。Starter 清理与公共 Update 的订阅所有权仍独立。
- 先对原实现执行定向回归，确认 Stop(old) 创建替代宿主、未登记句柄被静态 Stop 停止、reset 不停旧协程及同步 reset 后返回旧句柄等缺口，再修复。Unity `2022.3.62f3c1` 实际导入/编译成功；最终 Starter PlayMode **15/15** 通过，包含既有 6 项及 9 项关键生命周期用例。Foundation EditMode 边界 **5/5**、既有公共 Update reset/Quit 代表用例 **1/1**、直接消费者 Module 同步重入/启动失败与 View 异步加载/迟到 producer 取消代表用例 **4/4** 通过。通过的定向用例合计 EditMode 5 项、PlayMode 20 项；工具 TotalTests 包含未选择树节点，不作为运行数。独立只读审查无阻塞发现，`git diff --check` 无错误。
- Core Module/View 对 `null && !request.isDone` 的既有处理继续兼容，不修改其业务所有权；Timer/HTTP 忽略启动返回值、Audio maintenance 缓存旧句柄及已登记的外围终态/恢复问题仍由对应专题负责。保留 `CustomAsyncOperation.Abort` 不取消 producer 的既有测试，协程 Stop 不代表业务取消、UnityWebRequest Abort/Dispose、资源释放或 iterator finally 必然执行。
- 本批 reset/退出门禁采用定向反射 reset 和 SendMessage(Quit)，协程推进、场景卸载和生命周期清理为实际 Unity PlayMode 验证；不将模拟 Quit 描述为真实退出验收。未改变 Domain Reload 设置，真实关闭 Domain Reload 的两轮 Play 综合收口仍归 C；未进行 Player/IL2CPP、完整游戏、性能或专门 GC 回收验证。
- B 已达到本批技术完成标准，A 的会话前置与 B 的协程安全支撑具备进入 C 的技术条件；C 实施仍须用户独立授权。当前变更未提交，未执行 Commit、Push、Merge、Reset、Clean 或切换分支。

上述 A/B 记录保留为各批当时的实施证据；其验收与 Git 当前状态以上方状态及下节为准。

### 2.3-C 实施与专题最终技术验收

- 2026-10-10，用户正式授权 C 及整个 Update 专题技术收口。实际基线 `fusion/2.3-update`、HEAD `88cd76f`，工作区干净；A/B 已验收及 Git 收口。本轮未执行 Commit、Push、Merge、Reset、Clean 或分支切换，原 XF Package 严格只读。
- ActionGlobalHost 退出自身 Update/FixedUpdate/LateUpdate 自动消息方法，使用 Foundation UpdateManager 三份精确 token。OnEnable/OnDisable、初始化成功提交及 Release 负责连接/断开；捕获连接轮次，检查 Unity 存活、当前宿主、有效启用、模块状态、执行门禁及会话身份。先失效轮次再退订，普通公共快照中的旧桥接不能进入恢复/重建后的生命周期。
- 保留七个 owned EasyEvent 的上下文、手动 Trigger/清理/Dispose 与内部 fail-fast；公共 TriggerAll 继续其他消费者并报告 Action 异常。Shutdown 只注销 Action 自有三个身份，ResetOwner 截断旧内部快照；不调用公共清理或停止其他领域。初始化补丁重入取得宿主时不提前连接，成功后非创建式刷新，失败时不自动通知；不扩成完整 Action 初始化事务重写。
- GUI/Focus/Pause/Quit、Action 自有协程、组合执行、任务租约/暂停/取消、对象/场景/全局范围及延迟回收均保留。当前宿主 Quit 先设置自己的退出屏障与共享会话标志再通知监听；旧宿主 Quit/Destroy 不关闭新会话。没有新增跨领域全序、监听数量适配层、统一任务调度或损坏自愈。
- 最终真实 Play 门禁发现 A 的窄阻断：SubsystemRegistration 重新安装 Editor 监听后，较早注册的 ExitingPlayMode 监听可能先执行；当时 Application.isPlaying 仍为 true，Starter 仍能同步启动。连续两轮真实退出均复现。仅在 SingletonSessionState.CanRunExecution 的 UNITY_EDITOR 分支补充 isPlayingOrWillChangePlaymode 检查，退出请求后即拒绝，与 Editor 回调注册顺序无关；Player 表达式未改变，未重构 A/B 实现。
- Unity `2022.3.62f3c1` 实际导入/编译无错误，StandaloneWindows64 / Mono。定向 EditMode **13/13**（既有 Action 生命周期/重入/组合 Shutdown/回收）；PlayMode **45/45**：全局消息 **7**、Action 生命周期 **16**、公共 Update **6**、CoroutineStarter **15**、原游戏流程 **1**。真实根树发现数不是执行数；工具的空筛选或传输失败未记为通过，最终读取了以上实际结果。没有机械重跑完整 Pool、Events 或 Core；A/B 未受影响的边界及直接 Core 消费者历史结果继续有效。
- 新回归先确认原实现缺少共享源、初始化失败仍自动广播、Quit 通知内可重建等缺口，再实施并通过。只读审查发现的 fixture 所有权问题已修复：执行 Shutdown/reset 前拒绝已有 Action/Public Update/Starter owner；初始化补丁测试允许既有公共宿主；只清 fixture 自建且三个 channel 都无监听的公共宿主。最终独立静态复核无阻塞发现，diff 检查无错误。
- **关闭 Domain Reload 的连续两轮真实 Play 最终通过**，保留正常 Scene Reload。在临时空场景使用内存诊断，第一轮公共 token、owned event、Starter Coroutine 和 ActionHandle 持续保存至第二轮，未在退出前主动注销以代替生命周期验收。两轮 Update/Action 数分别 **34379/34379**、**10727/10727**；Fixed **1050/339**、Late **34379/10727**、Starter 推进 **34381/10729**。三种宿主各一份，执行会话身份轮换；旧 token/事件/协程/命名任务句柄不影响新工作，旧回调零次进入第二轮。两个真实 ExitingPlayMode 和 OnApplicationQuit 各收到一次，均拒绝新启动/重建；不是 SendMessage(Quit) 验收。两轮总计失败项零。
- 原始 Editor 设置为 enterPlayModeOptionsEnabled=false、保存 options=3；临时改为 enabled=true/options=DisableDomainReload（Scene Reload 保留），结束后恢复原 enabled=false/options=3。原 `Assets/Scenes/Main.unity`、活动场景与未脏状态恢复，无临时场景资产或新测试设施。随后现有 `YLAudioGameFlowPlayModeTests.MainStartSettingsGameExitAndReentryUseFrameworkAudio` **1/1**：真实 Main/XFAB→原 XF MainModule/XFFSM、菜单/设置/游戏/返回/再入/结束与现有 YL 音频通过，fixture 恢复音频偏好、timeScale 和 targetFrameRate。
- A～C 形成单一 `YLFramework.Foundation.UpdateManager` 公共三相位实现，复用 Events channel；Core View/Panel/Audio 消费精确 token，Action 保留 owned 领域消息。Core 重复 UpdateManager、旧静态字段、消费者先 Remove 再 Add 模式以及 Action 重复三相位转发已退出。Starter 保留独立 DDOL 与弱键原 owner 路由；Core Timer、YL Timing/Audio、Action 执行器/回收等独立驱动保留，因为它们拥有时钟、任务或资源语义。
- 本轮无新公共 API、程序集、资源迁移或 GUID 变化，Runtime/Core→Foundation 方向保持。A 的 UpdateManager 类型归属/返回 token 兼容变化仍适用；C 的自动消息连接需要初始化成功且宿主有效启用，Quit 回调不能关闭后重建。已同步 Action API XML 和既有模块说明；Foundation 不向 Core/Runtime 反向依赖，原 XF 的同名驱动继续供原游戏使用。
- 未进行新 Player/IL2CPP 构建、完整原游戏人工流程、同时关闭 Scene Reload 的全面恢复或性能测量。当前 Windows64/Mono 未新增反射/泛型/AOT 路径或 Player 依赖；结合既有阶段 1 Player 证据与本轮 Editor 代表性非干扰，不要求无差别重建。原 Logger/MCP 过渡日志、依赖声明/实际解析差异、资源/producer 物理取消与释放、宿主损坏恢复保持既有归属；不修原包或把历史限制当已解决。Core Timer 调度/终态与 YL Timing 融合归 2.4，资源和完整 Audio 归对应专题。

**结论：2.3 Update 已满足技术 COMPLETE 条件；具备进入 2.4 Timer 专项研究的技术前置条件。C 尚待用户验收与 Git 收口，完成后停止，不自动提交或启动 2.4。**
