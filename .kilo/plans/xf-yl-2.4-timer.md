# XF × YL 2.4 Timer：融合研究与技术设计

**状态：2.4-A 已收口（`861404c`）；2.4-B 已实现并通过定向验证，待用户验收与 Git 收口；C/D 未实施。**

本文的技术决策与实施路线已获用户总体审阅确认。批准仅覆盖总体设计方向、功能边界与 A→B→C→D 路线，不构成批次实施授权；未获另行授权前不改功能代码、不运行 Unity、不切分支、不执行 Git 写入，也不自动启动 2.4-A。

## 1. 基线与结论

- 2026-10-10 实际核对：`integration/xf-yl`，HEAD `c9e6024`，研究开始时工作区干净。用户已确认阶段 1、2.1 Pool、2.2 Events、2.3 Update COMPLETE；Git 的 `c9e6024` 为 Update 集成里程碑。长期蓝图及旧专项仍有待收口的历史文字，本轮不修改它们，也不按历史文字重新验收公共基础。
- 已阅读 `AGENTS.md`、`Assets/YLFramework/Documentation/IntegrationPlan.md`、`.kilo/plans/xf-yl-2-runtime-support.md`，复用 2.3 专项中已确认的公共相位、会话门禁、协程原 owner 与领域驱动边界。
- Unity 为 `2022.3.62f3c1`。原 XF 安装源码为 `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，lock revision 为 `2182708aecc596f30739fa8634fe2b9b6946ddd7`。原包只读，原 CGJ2024 继续使用原 XF 生态。
- 本轮仅进行源码、测试文本、消费者、程序集、资源身份的定向静态研究和文档检查；没有运行 Unity 刷新、编译、测试、Play 或构建。下文控制流问题不等于已经运行复现，性能成本分析不等于实测结论。

**推荐：以 YL 的 deadline 调度、Scope 和调度代次保护为融合基础，吸收 XF 的帧任务、GameObject 绑定、独立首次/周期参数及调用方持有的复用能力；形成 Foundation 中唯一 Timer 内核。**修正两边的实际问题，不原样复制任一方，也不全面重写 Action。

最终只保留一套独立到期任务调度：一个 `TimerScheduler` 内核、一个默认运行实例、一个薄的自有 Timer 驱动。Scope、可复用任务槽、TimerObject 和协程观察是职责不同的封装，不再保留新 YL 内的 Core TimerManager 与 Tools/Timing 两套调度器。

源码简称：`C` = `Assets/YLFramework/Runtime/XFMain/`；`Y` = `Assets/YLFramework/Runtime/`；`F` = `Y/Shared/`；`T` = `Assets/YLFramework/Tests/`；`XF` = 上述原 XF 包。行号为本次研究基线定位，不是跨版本契约。

## 2. 双方真实能力

### 2.1 对比与取舍

| 能力 | XF / 自有 Core 副本 | YL Tools/Timing | 最终推荐 |
|---|---|---|---|
| 秒延迟 | `DelayInvoke`，scaled/unscaled 开关，默认 unscaled | `Post2Scale` / `Post2Really`，双时钟 deadline | 保留双时钟，统一秒延迟；默认便利入口为 Unscaled，显式参数优先于 bool |
| 日期时间便利投递 | 无 | DateTime.Now 换算一次 delay 后投 Unscaled，不持续追踪墙钟 | 保留为薄转换入口，推荐 DateTimeOffset 避免本地时区歧义；不新增 alarm/后台调度 |
| 帧延迟 | `DelayFrame`，按管理器 Update 次数推进 | 无帧任务 | 保留帧任务，明确为 rendered Update 帧，不是 FixedUpdate 次数或 Action.Execute 次数 |
| 有限/无限周期 | 秒/帧 Repeat，首次 delay 与 interval 参数，有限次数含特殊分支 | 有限/无限 repeat、回调 tick 计数 | 保留秒/帧、总执行次数和无限重复，消除含混的零/负次数 |
| 首次与周期 | 秒首次实际消费 `delay + interval`；帧首次也叠加两阶段 | 同一 delta 同时用于首次与后续周期 | 公开区分 initialDelay 和 interval，首次只等待 initialDelay；便利重载可显式令二者相等 |
| 到期与补发 | 秒单次先减 delta 再判断；帧单次递减到 -1；周期重置 interval、每 tick 最多一次，不补发 | 严格 `deadline < now`；下一次 `now + delta`，不补发 | `now >= deadline`；fixed-delay、不补发、每任务每次推进最多一次 |
| 暂停/恢复 | 无正式暂停 API，Abort 后重启不是暂停 | 移堆、保存 remaining；回调内暂停保存下一完整周期 | 保留秒/帧暂停，冻结剩余量，不用 timeScale 或停用驱动替代任务暂停 |
| 句柄与状态 | 可变 `Timer` 同时作为任务存储和公开控制引用；IsRunning / isValid 并非稳定终态 | `TimeItem` 有状态、剩余时间、取消；ScheduleVersion 隔离旧调度收尾 | 公开只给精确 run 句柄，存储与控制身份分开；正常句柄可查询稳定终态 |
| 非分配复用 | 调用方保留 Timer，NonAlloc 改写同一对象 | Allocate 总是 new；Set/Post 是显式重配/重投递，不是池化 NonAlloc | 保留调用方持有的可复用槽，每次启动产生不同代次的值型句柄；不池化公开控制引用 |
| Scope / 名称 | 无 Scope 或名称域 | 默认/独立 Scope，命名与匿名任务，Replace、CancelAll、Dispose | 保留独立 Scope 与 Ordinal 名称；不与 Action 或 Events Scope 合并 |
| 非拥有式取消收集 | 可由调用者保留多个 Timer，但无组封装 | TimerHelper 强引用收集已有任务并 Clear/Cancel，不拥有其调度或 Scope | 保留小型 TimerHandleGroup 的收集/批量取消能力，与真正 Scope 所有权区分；不是第三个调度器或全局任务表 |
| GO 生命周期 | 绑定 GameObject，停用通常经 DisableListener Abort，管理器另有失效扫描 | 无 GO 绑定，依靠显式取消/Scope | 增加精确 GO 生命周期绑定，覆盖 paused 和未曾 active 对象失效；清监听与终态统一 |
| 场景 | 全局驱动 DDOL；GO 绑定随对象停用/失效，未提供精确 Scene Scope | 全局驱动；Scope 不自动跟随任意 sceneUnloaded | 无 owner 的任务跨场景；显式 Scope 所有权。精确 Scene Scope 可作为薄适配，不监听任意场景卸载清全部任务 |
| Coroutine | `WaitForCoroutine` 是独立观察协程，不进 Timer 列表，也不返回控制句柄 | 未提供等价出口 | 保留 Timer 终态等待，以及独立 Coroutine 观察便利能力；不把两者伪装为同一种计时任务 |
| 驱动与复用成本 | 独立 Mono Update，逐项倒序扫描，活动帧为 O(N) | 独立 Mono Update，双索引堆；Pause/Cancel 已由 HeapIndex 做 O(log N) 移除 | 延续索引堆机制，补帧域、稳定比较与私有归属校验；不把已有索引当新优化，不先建 PlayerLoop 或全域更新器 |

### 2.2 主要源码依据

| 主题 | 定位与确认结果 |
|---|---|
| XF 原版与 Core 副本 | `C/Manager/TimerManager/TimerManager.cs` 对照 `XF/Runtime/Manager/TimerManager/TimerManager.cs`：核心算法、NonAlloc、GO 监听未融合；差异主要是 namespace/import、Foundation alias 与宿主 FullName。TimerObject 主要是 namespace 身份变化 |
| XF 到期/终态/帧 | `TimerManager.cs:94–152,157–248,266–269`：绑定、Abort、回调、不同秒/帧推进分支及移除；`643–689,757–782,953–978,1095–1126`：NonAlloc 初始化；`1137–1157`：入队及 Coroutine 观察 |
| XF 时间输入 | `C/Extensions/TimeExtensions.cs:15–23`：unscaled delta 大于 0.2s 时替换为 0.02s，原包也相同。这是截断时间，不是完整真实时间 |
| YL 调度 | `Y/Tools/Timing/Timer.cs:253–285,289–324,363–423`：登记、时钟锚点、取消/暂停/剩余时间、重入守卫、到期派发与 lease 收尾 |
| YL 任务状态 | `Y/Tools/Timing/TimeItem.cs:52–87`：Allocate、Set 和 repeat/tick；Timer 的 ScheduleVersion 在重配/重投递时递增，旧 finally 只对匹配 lease 收尾 |
| YL Scope 与堆 | `Y/Tools/Timing/TimerScope.cs:13–103`、`Internal/BinaryHeap.cs:31–35,128–147,173–205`：Ordinal、名字与任务责任、已有索引及局部修复；`Documentation/Modules/Timing.md:31–43,89–94`：固定延迟、不补发、无帧/GO 绑定等边界 |
| Core 真实消费者 | `C/Manager/TimerManager/TimerObject.cs:6–42`：OnEnable 复用私有 Timer，delay/ignoreTimeScale/Action/Abort；未找到其他新 YL Runtime 直接计时消费者 |
| Action 计时 | `Y/Action/Core/IAction.cs:214–263`；`Actions/Delay.cs:60–90`、`DelayFrame.cs:39–54`、`Sequence.cs:58–126`、`Repeat.cs:78–135`；`Execution/MonoUpdateActionExecutor.cs:206–293` |
| 已验收公共基础 | `F/UpdateManager.cs:9–15,46–60,93–131`；`F/Internal/SingletonSessionState.cs:20–30,73–95`；`F/CoroutineStarter.cs:28–72,97–129`；`F/Pool/ObjectPool.cs:8–9,139–143` |

现存测试是契约依据和未来复用入口，不是本轮通过证据。已检查 TimingContractTests、TimingSchedulerTests、TimerScopeTests、TimingPlayModeTests、TimerScopePlayModeTests，以及必要 Action 和公共 Foundation 测试。Core 测试定向检索未找到 TimerObject/TimerManager 专项覆盖。

## 3. 问题分类与改进

### 3.1 XF / Core Timer

| 源码事实 | 影响、分类与本专题处理 |
|---|---|
| 单次 `callback` 先于 `isValid=false`（139→151） | 回调抛异常跳过终态，后续 Update 可再次调用；回调中 NonAlloc 重启会被旧收尾标无效。可靠控制流缺陷，尚未运行复现；必须先消费调度权并捕获 run 代次 |
| 周期回调后才重置 interval/减少次数（186–195、229–237） | 回调重新配置同一 Timer 后旧步骤仍会修改新配置；应按原 run 提交计数与后续调度，不能只加一个 isRunning 判断 |
| OnRemove 只清 IsRunning（266–269） | 不清 callback/onAbort，不解除 GO 监听，isValid 未统一终结；可能保留闭包和对象，终态不可可靠等待。终态管线统一负责解除调度/归属/监听及清引用 |
| 重绑先覆盖 gameObject，不先解绑旧对象（94–109） | 旧 A 的监听触发时可 Abort 已绑 B 的新任务，并按当前 B 退监听；A 继续保留旧闭包。绑定必须持原对象、原 run 与精确释放身份 |
| 管理器移除失效 GO（320–323）未走 Abort/统一失效 | 初始 inactive、未曾 active 后销毁等路径不保证 Abort；这与普通 OnDisable 通知不是同一契约。新设计区分未接收的任务与已接收后取消 |
| AddTimer 只凭 IsRunning 去重，宿主没有统一清理（1137–1141） | 宿主消失后旧任务仍可能 IsRunning=true，重启到新宿主时不入队；没有 owner/session 隔离。不得以换驱动掩盖，必须终结旧 owner 下所有 run |
| 帧单次 n 要 n+1 次管理器 tick；周期 I 要 I+1 次 tick | 静态算法确定的语义，不冒充精度实测。推荐显式改为 n 个未来渲染帧，注册帧不计入；需要更新直接契约测试 |
| 秒首次 Repeat 消耗 delay 后再消耗 interval | 真实参数含义与通常的 initialDelay 不同；推荐改为独立首次延迟，旧 API 不永久保留含混行为 |
| Unscaled 时间截断长帧 | 推荐 Timer 不再使用该 helper；不全局修改 TimeExtensions 影响其他领域。是否造成实际用户可见漂移需要运行测量 |

### 3.2 YL Timing

| 已确认机制 | 影响、分类与本专题处理 |
|---|---|
| 末轮名字提前释放、finally 保留终态、ScheduleVersion、更新重入守卫、取消优先 | 是值得继承的正确性基础；YL 仅提前释放名字，末轮仍在 Scope 集合内且可被 CancelAll 取消，新推荐完成预留是显式行为变化，不误写为原实现 |
| Post 用最近 Update 缓存时间建 deadline，Pause/Remaining 读取实时 Time | 长时间未 Update 或借用宿主失活时存在注册锚点与控制时钟不一致路径；静态风险未运行复现。登记、推进、暂停和查询必须使用同一 Timer 时钟协议 |
| repeat=0 仍存在执行一次的路径（TimeItem:83–86） | 参数契约含混，不能把零解释为可靠的零次任务；新 API 的有限总次数必须大于零，无限用显式选项表示 |
| 重投递只换调度 lease，不一定重置剩余 repeat/tick | 最后一轮回调重投递可以再多调用一轮。新 API 区分新 run 与控制当前 run，不再公开 Set/Post 同一任务引用的组合陷阱 |
| 用户显式 Set 后仍是同一公开 TimeItem 引用 | 调度 lease 保护旧 finally，但不能阻止旧公开别名 Cancel 新配置；这是公开控制身份问题，不是 heap finally 可以解决的问题 |
| 回调异常向外传播，中断本次 UpdateHeap/另一时钟队列 | 现有测试说明后续 Update 能恢复，不证明同帧其他任务继续。推荐仅失败任务 Faulted，其余就绪任务继续；这是有意异常契约变化 |
| 既有 host 的 Post/Update 快路径没有 2.3 CanRunExecution 检查 | 新建单例检查不等于已有宿主受退出屏障约束；接入已验收窄 gate/session，不改变其他领域单例语义 |
| readonly DefaultScope 可 Dispose，reset 不重建该 Scope | 关闭后的默认 Scope 在关闭 Domain Reload 的会话重建中可残留；新默认 owner 每会话重建，不向外暴露可被永久关闭的全局 Scope 本体 |
| Timer 创建链经 Root，Root.Awake 有 UI/EventSystem 支撑 | 公共 Timer 下沉不能连带 Root/UI/InputSystemBridge；默认驱动独立自建，不搜索或收养任意场景 Timer |
| TimeItem 与堆元数据 public 可写、任务集合按默认相等性 | 正常实现已有索引堆，不应重造；内部化 score/index、封闭任务状态，按引用/run 身份维护责任，避免外部改分数或派生 Equals 混淆所有权。属于 API 收口，非当前默认调用已坏的运行结论 |

这些问题须通过未来的最小回归确认，不能将旧测试文件的存在、旧历史通过或静态推导写成本轮已复现/已修复。借用 Timer MonoBehaviour、Root 分组及公有 Heap 不是独立计时能力，推荐不作为新公共服务契约保留。

## 4. 推荐架构与行为契约

### 4.1 唯一内核与驱动

建议正式 namespace 为 `YLFramework.Timing`，代码归属 `YLFramework.Foundation.Runtime`。不因 namespace 与目录不同另建程序集。

| 部件 | 职责与边界 |
|---|---|
| `TimerScheduler` | 唯一秒/帧到期调度、任务身份/状态、暂停、取消和异常处理；同一算法用于默认实例和显式手动实例 |
| `Timer` 静态入口 | 默认 Play 运行实例的便利 API；查询、取消与清理不创建驱动；没有 Core 专用依赖 |
| 内部 `TimerDriver` | 一个薄 DDOL MonoBehaviour，Update 提供时钟样本，管理默认 owner 的启停/销毁/session；不转发公共 Update 消息，不执行 Action 子树 |
| `TimerHandle` | 只控制所捕获的 scheduler owner + run 身份；默认值无效，旧会话/旧代次控制为空操作 |
| `ReusableTimer` | 调用方持有、不可入公共对象池的复用槽；每次 Start 创建新代次，返回 TimerHandle，不公开内部可变任务字段 |
| `TimerScope` | 自己的匿名/命名任务及取消责任；不拥有独立 Update、第三套任务总表或宿主 |
| `TimerHandleGroup` | 收集显式给它的已有句柄供批量取消，继承 TimerHelper 的独立用途；不转移任务 Scope、不占名称、不参与推进 |
| GO / Scene 绑定 | 仅把显式 owner 生命周期映射为对应 run/Scope 的取消；不能取消其他系统或按任意场景卸载清全局 |
| Core `TimerObject` | 保留 Inspector 和 UnityEvent 领域组件，消费公共 Timer；不保留 Core 调度内核 |

**推荐保留 Timer 专用薄驱动，而非直接接公共 UpdateManager。**目标是把两份同职责 Timer 驱动收敛为一份；重复的是到期算法与任务管理，不是 Timer 与 Action/Coroutine 的所有宿主。本轮未实施驱动收敛。

直接共享 Update 的收益是少一个 Mono Update 入口和 token 化连接，但本轮没有性能数据支持它更快。更重要的是公共 token 无可查询存活状态或 owner 结束通知，公共宿主被外部销毁后 Timer 无法单凭 token 即刻终结其 pending/paused 责任。补公共 owner 通知或反射查宿主会扩张已验收 2.3 契约。独立薄驱动能直接管理自己的毁损、终态和关闭，且复用既有窄 gate/session，不需要新增公共宿主框架。

未来若实际测量或依赖需求支持共享入口，可只换 TimerDriver 的时间输入与连接，不替换 TimerScheduler、Scope 或 Action 的时序；本轮不预建可替换 driver 接口体系。

### 4.2 时钟、首次执行与周期

| 项目 | 推荐正式契约 |
|---|---|
| 时钟域 | Scaled 秒、Unscaled 秒、RenderedFrame 三域。仅主线程、逐渲染 Update 到期，非高精度实时钟/后台线程/Fixed 计时器 |
| 精度 | 秒 deadline 和内部时间为 double；帧计数为单调 long。Unity 2022.3 支持双精度时间，但仍受帧粒度、scaled 时间与平台运行方式影响 |
| 时间输入 | 自动时钟按 Unity 渲染帧缓存一份 double 双时钟样本，首次就绪由 TimerDriver.Update 建立。已就绪且不在 Fixed 步内的登记/控制/查询可以通过同一采样器取得当前渲染帧样本，不能只沿用上一轮 Update 的旧时间 |
| 初始化/调用相位 | 不在 Awake 用尚未定义的 timeAsDouble 建有效 deadline，也不把 FixedUpdate 中读到的 fixed time 当 rendered time。冷启动/无法取得有效渲染样本时先 pending，在首个合法 Update 锚定；手动实例使用明确的初始时间与推进输入 |
| 无同步任务回调 | 登记不执行到期 callback；零延迟也最早在后续合法推进执行。自动运行排除登记所在渲染帧，回调内新增零延迟任务不能进入当前派发 |
| 边界 | 非有限秒值、负 delay、负 frameDelay、非正重复 interval、非正有限次数拒绝并在创建宿主/取消替换前校验。零首次 delay 合法；每帧重复使用帧 interval=1，而不是秒 interval=0。保留 null callback 的纯等待能力：仍推进计时、次数和终态，不需制造空委托 |
| 到期 | `now >= due`，不人为加 epsilon；首个合法 tick 观察到到期就执行。时间跨度很大也不把 unscaled delta 改为固定小值 |
| 帧延迟 | 时钟已就绪时，n>0 表示登记帧之后的 n 个未来 rendered frame；0 表示下一个合法帧。尚无合法帧样本的冷启动请求以首次 Update 样本建立登记帧，再等待 max(1,n) 帧；这一初始化量化明确例外，不冒充从未知 Awake 帧精确计数 |
| 周期 | 首次只等待 initialDelay，随后每次实际派发时的 now + interval；fixed-delay、不补发、不保留错过周期的债务，每任务每次推进至多一次 |
| 次数 | 有限 count 为总到期轮数，有 callback 时对应总调用次数，第一轮计入；无限由显式可选值/入口表示，不兼容 0 或任意负数的隐式含义 |
| 暂停 | 从调度队列摘下并保存剩余秒/帧；恢复重新建 due，不消费暂停时间。回调内暂停非末轮时保存下一完整 interval；多余 Pause/Resume 幂等 |
| 观察时间 | Remaining 是同一 Timer 时钟协议下的快照，不承诺在任意 Mono 相位取得亚帧精度；长暂停不让缓存的旧 now 变成新任务起点 |

日期时间入口只在接受请求时用 DateTimeOffset 当前值换算一次非负 delay，已过去的目标按零延迟处理，再走同一 Unscaled 调度。之后改系统时间不重算，不承诺后台唤醒或墙钟闹钟；手动内核不隐式读 DateTime。这承接 YL `Timer.cs:172–180` 的实际能力，而不是再做一套绝对日期调度器。

自动时钟在初始化就绪后以 `Time.frameCount` 识别同一渲染帧，不把这个 int 直接当永久 long 身份；本会话的帧序号单调递增，登记与驱动观察同一帧不会重复递增。Unity 文档也明确初始 Awake 的 frameCount 未定义，所以尚无首个合法 Update 样本时不能读它来伪造可靠登记帧。秒样本与帧样本分开管理；已就绪后的 Fixed 步可取得帧身份，但不能把 fixed 时间写入渲染秒样本。

冷启动或 Fixed 步登记的秒任务先记录请求，在下一合法 Update 取 initialDelay 的锚点；这类启动量化可能多等待到下个 Update，不承诺从 Awake/Fixed 调用瞬间计算精确墙钟延迟。只有帧时钟也尚未就绪时才采用表中的冷启动帧例外；秒样本需延期不能顺带把已知登记帧推迟。首次在 LateUpdate 创建默认 host 的帧任务也须按这项冷启动规则验收，不能实现后再隐藏其额外等待。

Pause 立即撤销该 run 的派发权限。没有合法新秒样本时按最近合法样本冻结 remaining；未锚定任务则冻结完整 initialDelay。Resume/首次锚定仍排除当前派发。手动实例不读 Unity Time，创建时给出初始双时钟/帧样本，推进只接受有限、非回拨输入；同实例递归推进为空操作，不推进第二次，也不阻止 callback 正常登记/取消任务。自动实例不提供手动推进出口。

实施时要把这些采样器边界作为具体源码契约落地，而不是全局替换 `Time.time` 为 `Time.timeAsDouble`。自动 host 无有效运行权限时不得接收任务。这保留现有手动调度的价值，但不开放公共 UpdateManager.Tick，不创建另一个算法。

Unity 2022.3 一手依据：[Time.timeAsDouble](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-timeAsDouble.html)、[Time.unscaledTimeAsDouble](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-unscaledTimeAsDouble.html)、[Time.frameCount](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-frameCount.html)、[Time.inFixedTimeStep](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-inFixedTimeStep.html)。timeAsDouble 与 frameCount 都有初始 Awake 未定义的限制；Fixed 步读取双时钟会得到 fixed time。双精度只改善表示精度，不证明调度实时性或后台补偿。

### 4.3 调度数据与成本

- 延续 YL 按 deadline 取就绪项的堆思路，增加帧域；三域共享同一调度代码和 run 生命周期，不建设三套 Timer 实现。
- 复用 YL 现有 HeapIndex/RemoveAt/局部修复思想与可用实现，条目补所处队列及 run 校验，收窄为内部调度数据。取消/暂停继续 O(log N)，不是本轮才增加索引；不按 TimerScope 名字扫描整个队列。队内按 due、登记序号稳定排序；跨时钟域不承诺物理时间全序。
- 新增/重投递任务有 pending/最早合法 tick 约束；callback 修改队列后重新验证捕获 run。不用固定整队列快照维持 Event 的普通退订语义，因为 Timer.Cancel 要阻止尚未开始的到期回调。
- 没有就绪任务时只查各堆顶；每次派发再插入的主要成本约 O(R log N)。GO 存活兜底另为 O(B)，B 为显式绑定 owner 数，含 paused 任务，不偷偷承诺全部工作 O(1)。Scope.CancelAll 为所属 k 个任务的必要清理，不是全局扫描。
- 在需要缓存内部条目/临时集合时复用现有 ObjectPool/ListPool。Pool 不提供租用代次，Timer 自己负责 run 代次和回调栈安全，禁止改 Pool 契约以替 Timer 解决旧别名。
- 不预建 timing wheel、全局优先级、公平性配额、任意 CatchUp 策略或通用 Clock 插件。若以后有实测负载支持，再比较；单次长帧完成大量合法一次任务不是本轮凭空增加限额的理由。

### 4.4 身份、复用与终态

推荐公开状态为 `Scheduled`、`Paused`、`Executing`、`Completed`、`Cancelled`、`Faulted`、`Invalidated`。Executing 表示 callback 尚未退出，不是稳定终态；内部区分是否已预留单次/末轮完成责任，不能让执行中项再次取得已消费的到期任务。

1. 普通 Schedule 每次创建独立的 run 状态，TimerHandle 可为只读值类型，指向该 run 的身份。状态不进入对象池；任务结束后清 callback/owner/Scope/调度条目，句柄仍可查询该 run 的最终状态。保留句柄不会继续保留用户 callback 闭包。
2. ReusableTimer 显式持有可重复使用的 run 存储，每次 Start 增加代次并返回新值型句柄。旧句柄只能看到 Invalidated、取消/暂停为空操作，不能重新取得槽的控制权。需要保留某次历史结果的调用方在下次 Start 前复制结果；不承诺零分配同时永久保存所有历史终态。
3. ReusableTimer.Start 要求当前没有可调度 run；活跃时拒绝，先显式 Cancel 再新 Start。末轮/单次 callback 中允许启动新代次，但旧收尾只操作捕获的旧身份。Dispose 永久关闭该槽；同一 Play 会话默认 host 重建后可显式 Start 新 run，不自动恢复旧工作，旧会话的槽不得收养到新会话。
4. 内部池化条目到用户回调栈退出后才可归还，不让重入取得仍在旧 finally 使用的条目。多播 Action 仍视为一个任务 callback：内部 target 失败可阻断该 callback，不能因此阻断其他 Timer。
5. 到期项在 callback 前消费；单次/最后一轮在 callback 前解除调度、Scope、GO 绑定和名字占用，预留完成责任，因此可以同名/同槽创建新 run。此时 IsActive=false、IsFinished=false、Status=Executing，对旧末轮的 Cancel/Pause 不再产生取消/Abort；callback 正常返回后 Completed，抛错后 Faulted，IsFinished 才为 true。等待器不得把 IsActive=false 当成功或完成；旧完成收尾不得写新代次。
6. 非末轮在 callback 前提交本轮计数与下一周期的必要状态，callback 的 Cancel/Pause/Dispose/新 run 优先于旧续调度。一个 callback 取消另一尚未开始的到期任务，后者本次不得执行。
7. Cancel/Invalidated 先提交终态并撤销索引、归属和监听，再通知取消原因。重复/重入取消不再通知。清理一组任务全部尝试；一个取消通知失败不丢失其他责任。名字替换必须捕获操作代次，旧取消通知不能覆盖回调中新建的同名 run。
8. 失败任务停止重复，默认运行边界继续其他就绪任务并集中日志报告。显式手动推进在本轮必要收尾及其他就绪项完成后传播原始/聚合异常。已被 callback Cancel/替代的 run 不因后续抛错改写已提交终态，但错误仍报告，不静默吞掉。

默认运行入口的取消通知异常也在边界报告，不反向改变已完成取消；手动实例的显式操作可以在提交必要状态后传播异常。明确区分调度提交与通知失败，不能因通知抛错复活旧任务。Faulted 不等于成功完成，也不自动触发传统 Abort UnityEvent。

NonAlloc 仅表示预热后复用槽及缓存委托可以避免新的任务状态分配，不承诺冷创建、扩容、闭包、GO 首次绑定、异常报告或 Coroutine wait 全链路零 GC。真正的稳态分配须后续针对该路径测量。

## 5. 所有权与系统协作

### 5.1 TimerScope 与对象/场景

- Scope 保留匿名/命名责任、PostNamed、ReplaceNamed、TryGet/Contains、按名 Cancel/Pause/Resume、CancelAll、Dispose 的能力，所有方法只作用于所属 run。名称为 Ordinal，命名入口拒绝空白；活跃同名普通 Post 拒绝，Replace 显式结束旧 run，新参数先校验。不因融合顺手加新的全组暂停 API。
- CancelAll 可重用当前有效 Scope；Dispose 永久关闭，关闭后投递拒绝，不复活。显式 Scope 由调用者而非默认 driver 所有，同一 Play 会话默认 host 重建只取消其旧 run，允许调用者用原 Scope 显式投递新 run；这保留 YL 的独立 Scope 价值，不自动续跑旧任务。查询/控制不能触发重建。
- Default 关联的 Scope 捕获执行 session，新会话拒绝旧 Scope 投递；显式手动实例的 Scope 绑定该实例上下文，实例 Dispose 后不能自动切换其他实例。默认内部范围随默认 owner 创建/结束，不公开可永久 Dispose 的 DefaultScope 本体。
- TimerHandleGroup 只收集调用者明确交付的取消权限，不收养调度或更改原 Scope；Clear 尝试本轮全部取消并释放旧句柄，新加入责任留到下一轮。它可以收集不同 Scope 的任务，不做名称查找或自动宿主绑定，保留 Helper 独立价值但不复制 Scope/调度逻辑。
- 未指定 owner 的默认任务跨场景，调用者显式取消。Module/View 关闭不自动取消全局任务，也不通过 Timer.Clear 改变 Events、Update、Action 或 Audio。
- GO 绑定至少保留两种实际有用的策略：停用即取消；销毁才取消而停用继续。它们不等于暂停/恢复。组件禁用与 GameObject inactive 区分；TimerObject 明确处理自身组件禁用。
- GO adapter 持绑定对象与精确 run/Scope 代次；成功、取消、替换、故障、owner 结束均解除监听并清引用。 paused run 仍受 owner 生命周期约束。不能只在任务到期时检查 fake-null，长时间暂停也必须及时失效。
- 生命周期消息结合按绑定 owner 的最小存活兜底，覆盖从未 active 的对象可能不收到 OnDestroy。回调中 Disable→Enable→新投递不能被旧清理快照取消；初始已失效/inactive 且选择停用取消的对象不接收活跃任务，不调用成功 callback。
- 精确场景所有权使用显式 Scene handle Scope 薄适配或已有 owner 在卸载时 Dispose，不借 `OnCurSceneUnLoadUnRegisterTrigger` 的“任意 sceneUnloaded”契约替代。只有查实消费者需要时才实现便利 Scene adapter，核心能力与验证不以新增场景服务为前提。

### 5.2 Core / MVC / View / Panel

- `C/Manager/ModuleManager.cs:217–268` 的 OnClose → Controller → View → 剩余 Model、异常继续及 Closed/注销顺序保持；不插入新的全局 Timer 清理步骤。
- Module/Controller 可以显式拥有 Scope，在现有退出钩子 Dispose；重启创建新 Scope。框架不自动给每个 MVC 对象增加 Timer 字段或注入服务。
- View 暂时 Disable 不等于表现会话结束，见 `C/mvc/View.cs:175–193`；View/Panel 的 Timer 所有权由业务选择 GO 停用取消或会话 Scope，缓存重用不能把旧 run 带入新会话。不得为 Timer 修改阶段 1 的 View/Panel 事务、缓存或 Model 归属。
- TimerObject 保留 Core 领域身份、GUID 及 `delay`、`ignoreTimeScale`、`Action`、`Abort` 四个序列化字段。私有 Timer 改为 ReusableTimer + 当前 run 句柄；缓存 UnityEvent 包装委托，避免每次 OnEnable 建闭包。自身 OnDisable 精确结束本轮，OnDestroy 释放复用槽，完成事件先消费旧轮次，Abort 只通知一次取消而非故障或成功。
- 明确变化：仅禁用 TimerObject 组件而 GO 仍 active，也结束该组件启动的计时；初始 invalid owner、成功回调中自停用、重复启用、OnAbort 中重启分别有明确定义，不沿用旧监听漏清和双通知路径。

### 5.3 Action 的独立价值

Timer 只调度独立到期 callback；Action 执行动作树、进度/插值/条件/组合。以下真实契约本专题不改：

| Action 事实 | 为什么不能替换为 Timer |
|---|---|
| IAction 首步只 OnStart，执行器 Start 同步 Step(0) | 即时子步骤可在 Start 返回前完成，Timer 新投递不 inline |
| Delay 先比较再累计 dt；Delay(1) 的 Execute(1) 只累计，下一次 Execute(0) 才完成 | deadline 到期算法并不等价，零 Delay 也不能简单同步触发 |
| Sequence 切步 Execute(0)，不传剩余 dt | 用独立 Timer 替子节点会改变即时链、切步和组合所有权 |
| Repeat 每轮 Reset 后等下一次 Execute 开新轮 | 不是 Timer 的 interval 或补发；节点和根名称仍属于活跃动作 |
| DelayFrame 读 Time.frameCount，Pause 不冻结全局帧 | 与推荐 Timer 的冻结剩余帧不同，不为统一而改变 Action |
| 对象 GO 停用暂停执行器、销毁取消；组件禁用不结束整个 GO 执行域 | 与 XF 式停用取消不同，TimerObject 不应改成 Action.Delay.Start(this) |
| CoroutineAction 运行在 ActionGlobalHost；Task 只观察外部 Task | Pause/IgnoreTimeScale 不重新定义协程或 Task 的时钟/物理取消 |

依据除第 2 节外：`Y/Action/Actions/Coroutine.cs:87–146`、`Task.cs:54–135`、`Extensions/ActionStartExtensions.cs:16–89`；`T/EditMode/Action/ModernActionTests.cs:319–340,477–491` 与 `T/PlayMode/Action/AsyncActionPlayModeTests.cs:155–189,229–256` 表达相关契约。

不新建统一 Action–Timer Scope、全局名字表或通用任务索引；不重写 Action 组合、执行器/回收或 AsyncOperation。若未来确需动作等待 Timer，必须区分 Completed/Cancelled/Faulted/Invalidated，不能用 `!IsActive` 无条件认定成功；本轮不新增没有消费者的桥接。

### 5.4 Update、Events、Pool 与 Coroutine

- 复用 `SingletonSessionState.CanRunExecution`、ExecutionSession 与 reset 注册。Timer 清自己 owner 时不调用公共 StopExecution；只有真实退出路径关闭共享执行 gate，旧 host 的迟到 Quit/Destroy 不关闭新会话。
- 不改 Update 的普通退订保留快照、owner 结束截断、相位顺序、空闲宿主及外部破坏不透明恢复契约；Timer 的精确取消由自己的 run guard 实现。
- Events 的 IUnRegister 可用于可选取消/协程观察适配，但不把 TimerScope 做成 SubscriptionScope，也不让 Timer 调度经 EventBus 发布。Runtime 中的 Events GO Trigger 不能反向依赖到 Foundation；只迁最小中立监听或维护 Timer 领域 binding，不搬整组 UnityEvent/输入监听。
- Pool 只提供普通存储复用与集合借还，Timer 负责时钟、代次、回调栈及清引用；不把 ReusableTimer 或正常 run 状态交给公有池。
- TimerHandle 的 Coroutine 等待只等待所捕获 run 的终态；停止等待协程不自动 Cancel Timer，取消/故障/失效不是成功。等待器的分配不纳入 NonAlloc 承诺。
- XF `WaitForCoroutine` 本来就不在 Timer 列表。推荐改为 Foundation 的窄 Coroutine 观察便利方法，返回可释放观察句柄，复用 CoroutineStarter 的原 owner 路由。只停止自己的观察 wrapper，不停止借用 producer；Starter.Start 拒绝时明确返回未启动，不悬挂为运行中 Timer。
- Coroutine 观察 callback 仅表示 Unity 对该 Coroutine 的等待结束，不证明业务成功、未取消或资源已释放。自然结束/Stop/producer owner 销毁的原生等待细节需最小 PlayMode 验证，不借此建立统一异步结果协议。

### 5.5 默认运行生命周期

| 情形 | 默认 Timer 的责任 |
|---|---|
| 首次有效投递 | Play 且 gate 开放时惰性自建独立命名 DDOL 驱动，成功后才提交；参数无效不建宿主，创建失败回滚本次对象与责任 |
| 查询/取消/Scope 清理 | 仅访问既有 run/owner，不搜索、创建或复活宿主 |
| 正常场景切换 | 全局任务继续；显式 GO/Scene owner 单独结束自己的任务 |
| 末个任务结束 | 清委托、监听和索引；空闲自有 host 可会话内保留，空队列不派发任务，不反复 Destroy/recreate |
| 外部禁用/销毁驱动 | 先使 owner 失效，所有 pending/scheduled/paused run 终结并清理，不把它当任务 Pause；不承诺热自愈旧任务 |
| disabled host 仍存在 | 新投递拒绝，不收养第二份替代宿主；显式 Shutdown 或销毁后新的有效投递可以建立新 owner，但旧任务不恢复 |
| Timer.Shutdown | 只结束默认 Timer owner，不清公共 Update/Events/Action/CoroutineStarter 的工作；非退出且后续获有效投递时可新建默认 owner |
| Quit / ExitingPlayMode | 新投递拒绝、旧 run 失效、只清已有责任；取消通知不能重建 Timer 或其他受 gate 约束的执行宿主 |
| SubsystemRegistration | 先换会话身份并终结旧 driver/run/默认范围/绑定；旧句柄、Scope、复用槽不得控制新会话 |

至少覆盖关闭 Domain Reload、正常 Scene Reload 的连续两轮真实 Play。即使同时关闭 Scene Reload，Timer 自己也不得双驱动或复活旧代次；不承诺恢复所有旧 Module/View/Action 会话，不扩成全框架 reload 引擎。

## 6. 公开 API 与程序集影响

以下是建议 API 形状，不要求用户提前决定所有参数排列或内部类型；实施不得突破已确认行为契约。

```csharp
Timer.Delay(delaySeconds, callback, clock: TimerClock.Unscaled);
Timer.At(targetDateTimeOffset, callback); // 仅在投递时换算 Unscaled delay。
Timer.Repeat(initialDelay, interval, callback, count: null, clock: TimerClock.Unscaled);
Timer.DelayFrames(frameCount, callback);
Timer.RepeatFrames(initialFrames, intervalFrames, callback, count: 5);

TimerHandle handle; // 精确控制一次 run，不是可重新 Set 的任务对象。
handle.Cancel();
handle.Pause();
handle.Resume();

TimerScope scope; // 匿名/命名、PostNamed/ReplaceNamed、CancelAll/Dispose。
ReusableTimer reusable; // 调用方持有；Start 返回新代次句柄。
TimerScheduler manual; // 显式手动实例，同一个内核，不自动创建 Unity host。
```

还应保留有价值的 Status、IsActive/IsFinished、Remaining、执行次数查询与取消原因；不重复公开多个含义相同的 bool、可写计时字段、IsValid/IsRunning 双状态或 Heap。

| 现有身份/API | 迁移与影响 |
|---|---|
| `YLFramework.Core.TimerManager`、`Core.Timer`、`TimerType` | 退出新 YL 调度实现，秒/帧/重复/NonAlloc 统一映射到正式 API；不留永久 Core facade。不修改原 XF 的同名类型 |
| `YLFramework.Utilities.Timing.Timer : MonoBehaviour` / Instance / UpdateMgr | 静态默认服务 + 显式手动实例，退出任意场景组件借用和 Root 创建链；显式手动能力保留但不再驱动默认实例 |
| `TimeItem` / Set / Post(existing) | 普通 run 由安全句柄控制；重新投递是新 run，复用要显式 ReusableTimer，不再依赖可变公开别名 |
| TimerScope 与 TimerHelper | Scope 保留真正归属/名称控制，Helper 的非拥有式批量取消改为 TimerHandleGroup；归正式 namespace，移除旧重复转发、误导性命名及公有内部堆，不为旧未发布接口建立全量 shim |
| 默认时间与参数 | 默认 Unscaled；首次独立 initialDelay、到期含等号、帧 n/冷启动、负 delay/非正 interval/count 校验、末轮预留完成、异常继续是明确行为变化；null callback 的纯等待用途保留 |
| `TimerObject` | 保留组件身份、meta/GUID、Inspector 字段及 UnityEvent 方法入口；只换私有任务存储与生命周期实现 |
| 文档、帮助、测试 | 更新现有 Timing/Architecture/必要 Action 边界与 Audio Help 的计时说明；直接测试按真实契约适配，不削弱有效断言 |

实际程序集已核对：Foundation references `[]`，Core → Foundation/XFAB/Unity UI，Runtime → Foundation/ZipTool/Unity UI。推荐不新增 asmdef、不增加 Core → 整体 Runtime、不让 Foundation 引用 Core、Root、UI、Action、原 XF 或 XFAB。Core 的临时 XFAB 资源依赖本专题不解除。

当前实际消费者范围：新 YL TimerObject、五套 Timing/Scope 测试及 `Editor/Audio/Help/AudioKitHelpProvider.cs:362–367` 的帮助文本；未在新 YL Audio/UI/Resource/Save/Action/Samples 发现真实 Timing 调用。原游戏 EnemyBase、TipPanel、EffectController 等使用原 XF，不迁移。没有仓库调用不构成删除帧/GO/Scope 等通用能力的理由。

资源身份已定向核对：新 TimerObject GUID `8fd9c1c43a386034fbb60e5c2b0fa8b0`、DisableListener GUID `78704436403237740ab5d3d691561347`，Assets 文本引用检索未找到挂载资产；TimerManager/原新 TimerObject GUID 同样未找到相关挂载。未找到 TimerObject 专用 Editor。这个结果不证明外部资源或动态使用不存在。

未来类型迁移前补查受影响脚本 GUID、SerializeReference、UnityEvent、反射字符串和测试绑定；保留实际移动脚本的 meta/GUID，新资产由 Unity 生成新身份。TimerObject 的字段与事件名不重命名，不用 FormerlySerializedAs 假装解决 namespace/程序集迁移。

来源许可分开保留：承接 XF 的部分继续受 `F/LICENSE.md` 的 XF MIT 声明覆盖；YL Timing 的 Timer/TimeItem/TimerHelper 有 `Copyright (c) 2017 snowcold/liangxie`，BinaryHeap 文件头还有 QFramework 来源链接及完整 MIT 文本，不能用 XF LICENSE 替换或覆盖这些原声明。Timer 文件头另有 UnityTimer 链接，复用该来源部分时定向核对适用上游声明，不借重新命名去掉出处。只维护被实际吸收的代码和必要完整许可证，不开展第三方全库审计。

## 7. 实施批次与最小验收

实施仅在用户另行授权后开始。A→B→C→D，每批交付可独立验收的完整能力，不按类数量切碎；旧两套机制仅允许迁移中暂存，C 结束后不再承担新 YL 的独立计时调度。

| 批次 | 主要变更轴与交付 | 最小验收 |
|---|---|---|
| **A 内核与正式计时契约** | Foundation 唯一手动内核、三个时钟域、独立 initial/interval、次数、精确 run/终态、暂停和异常继续。承接可用 YL 调度/lease/索引堆，补帧与 double/稳定比较，不重造已正确的索引算法；不先改 Core/MVC 或 Action | 用可控时间代表项验证恰好到期、零/负/非有限输入、帧 n、有限/无限、不补发、暂停剩余；单次抛错不重触发、非末轮 Cancel/Pause、末轮建立新 run、取消另一个到期任务、递归推进不重复。优先改用不依赖 Scope 名称的现存内核断言；Scope 同名验收归 B |
| **B 默认驱动、Scope 与显式复用** | 单一正式默认 TimerDriver、gate/session/owner、非创建控制、Scope/名称、ReusableTimer、TimerHandleGroup 和 DateTimeOffset 薄入口。按本批明确授权，ReusableTimer 提前至 B；旧 Timing/Core 迁移及删除不在 B 执行 | 复用 Timing/TimerScope 的控制、名称、取消、Dispose、Remaining、末轮同名与同会话 Scope 跨 host 重建；补 registered clock anchor/冷启动/非 Update 输入、未锚定 Pause/Resume、LateUpdate 首次 host 帧等待、host disabled/destroy、句柄/Scope 查询取消不建 host、旧会话 Scope/复用槽拒绝投递、gate 关闭拒绝。验证复用旧句柄隔离、callback 新 run 与旧 finally，日期转换及组清理；PlayMode 选真实时间/timeScale=0/场景保活代表路径 |
| **C XF 互补能力与 Core 接入** | 接入 B 已完成 ReusableTimer，实施 GO 生命周期、TimerObject、Timer 终态等待与窄 Coroutine 观察；迁移旧计时消费者与调度实现，保留字段/GUID。旧实现移除须随 C/D 授权完成 | 重绑 A→B 不留 A、paused owner 失效、从未 active 后销毁、GO 停用/组件停用/重新启用、成功回调自停用不双 Abort、OnAbort 重启、监听/委托清引用。Coroutine 定向验证自然结束、producer 被 Stop/owner 销毁的实际等待结果、停止观察不停止 producer、Starter 拒绝不悬挂，不扩成异步矩阵 |
| **D 专题收口** | 消费者、命名/冗余入口、来源许可、程序集/资源身份、现有文档收口；确认唯一调度归属和默认驱动 | 真实关闭 Domain Reload 连续两轮 Play，旧 run/Scope/复用槽及迟到消息隔离、真实退出不重建；复用一个 Core/View 生命周期代表项和一个 Timer/Action 共存隔离项。按变化风险选择原游戏既有非干扰；不跑历史全集或默认建 Player |

### 7.1 优先复用与必要缺口

- 现存 `T/EditMode/TimingContractTests.cs`、`TimingSchedulerTests.cs`、`TimerScopeTests.cs`；`T/PlayMode/TimingPlayModeTests.cs`、`TimerScopePlayModeTests.cs`。重点复用取消、终态、Pause/Resume、名字提前释放、Replace/Dispose、重投递和异常后恢复的有效断言。
- 相当部分旧调度测试通过反射 UpdateHeap 或改 SortScore 绕过实际时钟。新内核用明确手动输入验证同类行为；只同步变更的 `<`→`<=`、次数、首 delay、异常等契约，不把“旧断言不适配”当作删除测试的理由。
- 真正缺口为帧 Timer、复用句柄旧别名、callback 后旧收尾、GO 监听清理、TimerObject、注册时钟锚点、已有 host 退出门禁及真实会话重建。围绕这些关键风险补用例，不生成时钟×次数×Scope×owner 的全组合矩阵。
- Action 未改时优先只静态核对；需要跨领域代表验证时从 `ModernActionTests` 的 Delay/Sequence 零 dt 与 Repeat、`ActionExecutionTests` 的同步 Start/重入、`ModernActionPlayModeTests` 的帧/暂停中选少量代表项。Coroutine/Task 全套不作为 Timer 默认回归。
- Foundation 身份/程序集复用 `FoundationBoundaryTests`；宿主门禁/fixture 所有权沿用 2.3 的测试方法。测试不得接管用户已存在的 Timer/Update/Action/Starter owner，不清别人全局责任制造通过。

### 7.2 分层验证与性能

每个实施批次检查实际 diff、meta/GUID、程序集和直接消费者，再执行相应 Unity 编译及定向 EditMode/PlayMode，分别报告。未改变的 Pool/Events/Update 历史结果可沿用，不重跑全部历史测试。本轮没有执行这些未来验证。

真实无 Domain Reload 验收与测试后设置/场景恢复归 D；不能用反射 reset 或 SendMessage(Quit) 代替真实两轮 Play，也不保存用户未保存的场景。Player/IL2CPP/全游戏验收仅在新增平台依赖、身份/裁剪或实际行为风险需要时安排；当前不以“Timer 是公共模块”为由强制全量构建。

性能只在需要证明方案价值时做代表测量：空闲、大量远期任务、到期突发、频繁取消/Scope 清理及预热 ReusableTimer。比较至少计入 heap 索引、GO 兜底、委托/闭包、扩容、Pool 身份跟踪和异常路径；不要只测派发方法或用 Editor 单帧计时声称通用提升。不为理论性能建立专用基准框架；未测量前只交付明确的算法成本与稳态分配目标。

## 8. 备选、风险与重大决策

### 8.1 重大备选

| 方案 | 优点 | 风险与推荐判断 |
|---|---|---|
| XF List/Timer 为主体，补 Scope 与暂停 | 帧/GO/复用已有结构，承接较直接 | 终态、控制身份、监听、时钟和线性扫描均需实质改造；YL 已有较完整的 Scope/lease，推荐以 YL 调度基础融合更合适 |
| YL 双堆原样扩帧 | 改动小，保留既有测试 | 原缓存时钟、公开可变 TimeItem、异常中断和默认 Scope 生命周期未解决；只加第三堆不足以融合 |
| 唯一内核 + 公共 Update token | 少一份 Mono Update 入口，复用精确订阅 | 无实测收益，公共 owner 结束通知不足；不推荐为此改变 2.3 已验收契约 |
| **唯一内核 + Timer 薄驱动** | 可直接管理 owner/时钟/终态，复用 gate/session/Pool；两旧驱动收一份 | 仍有一个领域宿主；不构成重复计时机制，推荐 |
| Timer/Action 完全共享时钟和调度器 | 表面统一全部延迟/重复 | 改变首步、Delay/Sequence/Repeat、暂停/协程和名称责任，跨专题高风险，明确不采用 |
| PlayerLoop / timing wheel / CatchUp 策略组 | 可服务特定高负载或精确相位 | 现阶段缺乏收益证据，安装/卸载/追赶/预算带来新契约；本专题不预建 |

主要风险是计时语义改变、Scope 和复用代次在 callback 内交错、所有权失效后的通知、时钟调用上下文及资源/程序集身份。通过统一终态管线、明确新 run、少量真实 Unity 边界验证控制，不通过保留两套算法或隐藏兼容开关规避。

### 8.2 重大技术决策（已批准）

**2026-10-10：用户通过总体审阅，确认以下六项决策与 A→B→C→D 实施路线。**确认范围为总体架构、功能边界、行为契约与实施路线；内部字段、类数量、堆实现细节仍由实施时根据实际源码自主决定。

1. 基础与补充：YL Timing 的 deadline 调度、Scope 与调度代次保护作为融合基础；XF 的 Frame 计时、GameObject 绑定及复用能力作为补充。
2. 架构：Foundation 唯一 `YLFramework.Timing` 公共计时内核与薄 Timer 专用驱动，复用已验收 gate/session/Pool，不为 Timer 修改公共 Update 的所有权协议。
3. 计时契约变化：到期含等号、首次只等 initialDelay、时钟就绪后的帧 n 为 n 个未来渲染帧及明确冷启动量化例外、零延迟不 inline、非正 interval/count 拒绝、真实 Unscaled 不截断；默认 fixed-delay、不补发，不预建 CatchUp。
4. 复用与终态：普通 run 稳定终态与显式 ReusableTimer 的不同存储责任；后者启动新代次后旧句柄 Invalidated，不承诺零分配同时保存无限历史结果；不保留公开 TimeItem.Set/NonAlloc 裸引用控制。异常只结束失败任务、默认边界继续并报告，取消/失效先提交状态再通知；不把 Faulted 当成功或 Abort，不改变 Events/Action 的已验收异常策略。
5. 所有权：GO 停用取消/销毁取消的明确策略、TimerObject 组件停用结束本轮、无 owner 全局任务跨场景；Module/View 不自动接管外部 Timer；Action 保持独立，不包装为 Timer 也不统一调度；旧 Scope/复用槽不跨会话复活。
6. 实施路线：A→B→C→D，旧 API/两套驱动的迁移期而非永久兼容层，按风险最小验证；Action/AsyncOperation/Audio/UI/Resource/HTTP 的完整重构和原游戏迁移均不在授权范围。

批准仅覆盖上述总体方向与路线，不自动授权批次实施、切换分支或 Git 写入。各批次仍需独立授权，按“实施 → 必要验证 → 用户验收 → Git 收口”推进；若实施发现重大新兼容证据或超出这些边界，先提出调整再继续。第 2～7 节的技术条款继续作为本批准范围内各批次的设计依据。

**总体设计批准批次仅更新本文档状态与已批准决策记录，未修改 Runtime、Editor、测试、资源、asmdef、项目配置或旧长期文档，未执行 Unity 或 Git 写入。各批次另行授权后的实施结果记于下文。**

## 9. 2.4-A 实施结果（2026-10-10）

本批获明确实施授权，开始时核对 `fusion/2.4-timer`、HEAD `91c062a`、工作区干净。仅新增 Foundation 内核与其契约测试，增量维护本文；未切换分支、提交、推送、合并或创建 Tag。

### 9.1 实际实现与融合

- `F/Timing/TimerScheduler.cs` 为唯一正式手动内核，namespace `YLFramework.Timing`，归 `YLFramework.Foundation.Runtime`；未新增 asmdef 或程序集依赖。显式实例由调用方提供初始/推进样本，不读 Unity Time、不创建或借用 MonoBehaviour 宿主。
- 保留 YL 的 deadline、执行租约和索引堆思路。`Internal/TimerHeap.cs` 从既有 BinaryHeap 收窄，沿用 1-based 索引、RemoveAt 与上浮/下沉局部修复；新增内部归属核验、double 秒分数、long 帧分数及 due/order 稳定比较，完整保留所吸收堆代码的 QFramework 来源、版权与 MIT 许可文本。
- 吸收 XF 的 Frame Timer、独立首次/周期参数及复用身份需求；三域使用同一派发/终态算法，不新增第二套帧生命周期逻辑。
- `TimerHandle` 为只读值类型，捕获私有 run 引用及 generation；任务存储不公开、普通 run 不池化。控制核验原 owner/run/代次，执行收尾只处理捕获代次。正常句柄可保留稳定终态和执行次数，终态释放 callback、取消通知、owner 和队列引用。
- 本批不需要公开基础 Scope；唯一内核的 `_owned` 集合只维护自身责任。Scope 名称与所有权收敛留 B，未为了内部代次提前实现 ReusableTimer 或复用槽管理。

### 9.2 已落地契约与 API

```csharp
var manual = new TimerScheduler(scaledTime, unscaledTime, renderedFrame);
TimerHandle once = manual.Delay(delay, callback, clock: TimerClock.Unscaled);
TimerHandle repeat = manual.Repeat(firstDelay, interval, onTick, count: null);
TimerHandle frames = manual.DelayFrames(frameDelay, callback);
TimerHandle frameRepeat = manual.RepeatFrames(firstDelay, interval, onTick, count: 5);
manual.Advance(nextScaledTime, nextUnscaledTime, nextRenderedFrame);
once.Pause(); once.Resume(); once.Cancel();
manual.Dispose();
```

- 秒入口接受 Scaled/Unscaled，默认 Unscaled；Frame 使用专用入口。秒为 double、帧为 long。调用方须提供 rendered frame 样本，不得用 FixedUpdate 或 Action 执行次数充当帧时钟。
- 有限、非负初始/推进样本；三个时钟均不得回拨。参数非法在登记前拒绝：负 delay/frameDelay、非有限秒、非正 interval 或有限 count、秒入口使用帧域/未知域。count 为总到期轮数，null 明确表示无限；重复回调参数与 `ExecutionCount` 使用 long，避免沿用旧 int 轮数上限。
- 到期 `now >= deadline`。首次只等 firstDelay；后续 deadline 为实际派发样本 + interval。fixed-delay、不补发、不积累周期债务；同一任务每个推进渲染帧最多执行一次。null callback 的纯等待仍消费次数并正常完成。
- 登记不 inline。Frame 的 n>0 等待登记帧之后 n 个未来帧，0 等待下一帧。相同帧的 Advance 仅刷新时间快照、不派发；递归 Advance 是空操作，不提交递归输入。pending 登记/恢复/下一周期只在后续帧入堆，回调新增任务不能串入当前任何时钟域，也不会挡住其他已就绪任务。
- 状态为 Scheduled、Paused、Executing、Completed、Cancelled、Faulted、Invalidated。默认句柄为 Invalidated。提供 Status、IsActive、IsFinished、ExecutionCount、RemainingSeconds、RemainingFrames；两种 remaining 分别只对对应域有意义，均遵循手动实例最近合法样本。
- Pause 保存剩余秒/帧，Resume 从当前样本重新建立 deadline；重复控制安全。非末轮回调内 Pause 保存完整下一 interval，当前状态保持 Executing，回调退出后才提交 Paused；回调内 Pause→Resume 不产生重复入队。
- Cancel 先提交 Cancelled、撤销归属/队列并清委托，再通知 `TimerCancellationReason.Cancelled`。Dispose 先永久关闭实例并提交全部未完成责任 Invalidated，再逐项通知 `SchedulerDisposed`；一个通知失败仍尝试其他通知，重复取消/关闭不再次通知。
- 单次/末轮在回调前预留完成责任：Status=Executing、IsActive=false、IsFinished=false，Cancel/Pause 不再生效；正常返回为 Completed、失败为 Faulted。非末轮取消/暂停优先于旧续调度；取消另一就绪任务立即撤销它的本轮派发。
- 回调失败只终结该 run，不阻断本轮其他独立任务或时钟域。手动 Advance 完成必要收尾后以 ExceptionDispatchInfo 传播单一原始异常，多个错误以 AggregateException 汇总；已提交 Cancelled/Invalidated 不被后续抛错覆盖。异常终态不调用取消通知。默认自动驱动的日志边界留 B，核心已提供继续执行与汇总传播能力。

### 9.3 实际验证

- **Unity 实际编译**：在当前已打开的 `2022.3.62f3c1` 工程导入并编译新增源码/测试；编译结束后 `EditorUtility.scriptCompilationFailed=false`、`EditorApplication.isCompiling=false`，实际载入的 Scheduler 程序集为 `YLFramework.Foundation.Runtime`。未使用辅助 dotnet 编译替代 Unity 结果，未启动第二个 Unity 实例。
- **定向 EditMode**：`TimerSchedulerContractTests` 28/28 通过、0 失败、0 跳过。覆盖双秒时钟、精确到期、零延迟、首次/周期、有限/无限、纯等待、帧 n/long 精度、不补发、秒/帧暂停、回调取消/暂停/恢复、新投递与跨域延期、递归推进、旧句柄隔离、末轮完成预留、单错/聚合异常、取消通知失败、关闭截断及终态回调对象释放。
- 测试先验证新内核缺失的失败；公开形状就绪后 22 个核心行为断言失败，再实现后通过。复查时补充“全部失效先于任何关闭通知”回归，实际观察到其他任务仍为 Scheduled 的失败，修正为两阶段提交/通知后通过；收紧回调内暂停状态断言，实际观察到过早呈现 Paused，修正为执行租约退出后提交暂停，最终 Timer 集全部通过。
- 复用 `FoundationBoundaryTests` 5/5 通过，确认公共程序集依赖及既有边界未扩张。MCP 的 Summary.TotalTests 是全项目发现数，实际执行数量以上述 PassedTests 为准，没有执行全项目 1188 个测试。
- 实际检查新增源码与测试、Unity 生成的目录/脚本 `.meta`、无新增 asmdef/配置改动及 `git diff --check`。未重跑 Pool/Events/Update 历史全集；未执行 PlayMode、Player 构建、性能基准或真实 Domain Reload 会话验证，本批手动内核没有自动 Unity 生命周期集成。

### 9.4 未完成边界与进入 B 条件

A 内核及正式手动契约具备进入 B 的基础，待用户验收与 Git 收口，不自动启动 B。B 仍需实现 TimerDriver、默认入口、Unity 合法采样/冷启动、gate/session/owner、Scope 名称与默认范围、TimerHandleGroup、日期转换及旧 YL Timing 迁移；其中真实自动驱动的继续运行/日志报告尚未交付。

ReusableTimer、GO 绑定、TimerObject/Core TimerManager 消费者迁移、Coroutine Wait/观察、旧 Core 内核移除归 C；完整命名/许可/文档与真实会话收口归 D。旧 XF/Core Timer 与 YL Tools/Timing 仅作为迁移基线保留，本批没有删除或迁移它们，因此尚未宣称整个项目已只运行一套 Timer。Action、UpdateManager、原游戏消费者和长期蓝图均未修改。

## 10. 2.4-B 实施结果（2026-10-10）

本批按用户新的明确授权实施默认驱动、Scope、ReusableTimer、HandleGroup 与 DateTimeOffset。开始时核对 `fusion/2.4-timer`、HEAD `861404c`，工作区干净。新授权将 ReusableTimer 从原 C 路线提前到 B，并明确禁止在 B 迁移 Core TimerManager、TimerObject、Coroutine 或删除旧 Timer 实现；本节及第 7 节更新后的路线优先于第 9 节 A 当时的后续安排。未切换分支、提交、推送、合并或创建 Tag。

### 10.1 默认运行服务与生命周期

- 新增 `F/Timing/Timer.cs` 静态入口和内部 `Internal/TimerDriver.cs`。仅有效 Play 投递惰性创建 `YLFramework.Timing.TimerDriver` 独立顶级 DDOL GameObject；不搜索/收养场景对象，不创建 Root、UI 或公共 UpdateManager。默认与手动实例使用同一个 TimerScheduler 派发算法，自动实例拒绝公开手动 Advance。
- 查询、Cancel/Pause/Resume、Scope/HandleGroup 清理和 Shutdown 都不创建驱动。无效参数先拒绝；Play/gate 无运行权限、仍存在的 disabled/released driver 拒绝投递并抛 InvalidOperationException，不静默返回假任务。
- 自动时钟首个合法 Update 前保持未锚定，避免读取 Awake 未定义时间/帧。首个 Update 建立合法样本；每个已知 Unity rendered frame 只采一份 double 双时钟，维护会话内单调 long 帧身份。已就绪的非 Fixed 登记/控制/查询可同步当前帧样本，独立派发帧游标防止登记采样漏掉该帧的旧任务派发。
- Fixed 步只更新已知帧身份，不把 fixed time 写入渲染秒样本。秒任务下个合法 Update 锚定 delay，帧任务使用已知登记帧。冷启动帧 n 在首个 Update 建立登记帧后再等 max(1,n) 帧；零秒/帧均不 inline、不进入登记帧或当前 callback 派发。
- OnDisable 立即结束 owner，pending/scheduled/paused/非末轮执行责任统一 Invalidated。再次启用不复活已释放 owner，也不建立第二份 driver；需 Shutdown 或实际销毁后显式重投。空闲自有 host 保留，正常场景卸载不影响默认任务。
- Shutdown 只清既有默认 Timer owner；先撤销静态 owner，再失效任务和销毁自有对象，通知期间禁止重建。后续同会话新投递可新建 owner，旧工作不自动恢复。没有调用公共 StopExecution 清其他领域；只有当前 driver 的 Quit 路径关闭共享 gate。
- 捕获 SingletonSessionState.ExecutionSession 并注册 Timer 专属 session reset。Reset 终结旧 owner；旧 driver 的迟到 Quit/Destroy 不关闭新 gate 或释放新 owner。旧 Scope/复用槽投递拒绝，旧 handle 控制为空操作。默认异常在已提交终态与必要清理后逐项 Debug.LogException，其他就绪任务继续；手动实例维持原始/聚合异常传播契约。

### 10.2 Scope、复用与句柄组 API

```csharp
TimerHandle once = Timer.Delay(1, callback); // 默认 Unscaled。
TimerHandle date = Timer.At(targetDateTimeOffset, callback);
TimerHandle frames = Timer.DelayFrames(2, callback);
TimerHandle repeat = Timer.Repeat(0, 1, onTick, count: 5);

using var scope = new TimerScope(); // 或 new TimerScope(manualScheduler)。
scope.Delay(1, callback); // 匿名归属。
scope.PostNamed("cooldown", 1, callback);
scope.ReplaceNamed("cooldown", 2, callback);
scope.PostNamedFrames("frameLoop", 1, 2, onTick, count: 5);
scope.TryGet("cooldown", out TimerHandle named);
scope.Pause("cooldown"); scope.Resume("cooldown");
scope.CancelAll(); // 可继续使用，Dispose 则永久关闭。

using var reusable = new ReusableTimer(); // 或绑定 manualScheduler。
TimerHandle run = reusable.Start(1, callback);
run.Cancel();
TimerHandle newRun = reusable.StartFrames(1, callback); // run 已 Invalidated，不能控制 newRun。

var group = new TimerHandleGroup();
group.Add(once); group.Add(named);
group.Remove(named); // 仅移出集合，不取消或转移 Scope。
group.CancelAll(); // 与 Clear 一样取消并释放当前旧集合。
Timer.Shutdown(); // 只结束默认 Timer owner。
```

- Scope 只持自己的匿名/命名 run 责任，名称使用 Ordinal；提供匿名秒/帧单次与周期、PostNamed/ReplaceNamed 及对应 Frames 重载、Contains/TryGet、按名控制、CancelAll、Dispose 和 IsDisposed。不是 scheduler、driver 或全局表。
- ReplaceNamed 在取消前校验形状与当前时钟可表示的 deadline/frame 范围；实际登记仍重验。按名称的替换操作版本保证取消通知重入同名 Post/Replace 优先，外层不覆盖并返回默认 Invalidated 句柄，即使内层又取消自己也不恢复外层旧意图。取消后因通知、owner 变化或登记失败不回滚旧任务。
- 单次/末轮在 callback 前释放 Scope/名称，旧 finally 不摘除 callback 中新建的同名 run。CancelAll 捕获入口旧责任并全部尝试；重入新任务保留。Dispose 先永久关闭再取消，通知不可复活。默认 Scope 在同会话 host 重建后可显式重投；手动 Scope 永久绑定原实例，不回退到默认服务。
- ReusableTimer 调用方持有一个私有 TimerRun，不放入 ObjectPool、不公开记录；Start/StartRepeat/StartFrames/StartRepeatFrames 每次增加 run generation 并返回新值型 handle。旧 handle 下次 Start 后只呈现 Invalidated，控制无效，不承诺保存全部历史终态。Current 仅给槽当前代次句柄。
- 活跃 run（含暂停及非末轮执行）拒绝 Start，需先显式 Cancel；单次/末轮 callback 中可直接新 Start。复用存储重置当前 run 数据，旧执行栈核验捕获 generation，不能完成、故障或重新调度新 run。取消通知中显式重启同样隔离。Dispose 先关闭槽再取消，永不复活；默认槽捕获 session，不跨会话收养。
- HandleGroup 只收集明确交付的已有句柄，不创建 Timer、不拥有 Scope、不改名称/归属、不参与推进。Clear/CancelAll 先摘下旧集合再全部尝试取消并释放；重入 Add 留下一轮，单个失败不丢其余责任。Scope/Group 临时集合使用已有 ListPool，保留所吸收 Helper 的版权头；未改变 Pool 契约。
- Timer.At 仅在请求时用 DateTimeOffset.UtcNow 换算一次非负 delay 后进入 Unscaled。相同绝对时间的不同 offset 正确归一，过去目标按零等待，后续不追踪系统时间、不后台唤醒。手动内核不隐式读取 DateTime。

### 10.3 实际验证

| 分层 | 本轮实际结果 | 范围 |
|---|---|---|
| Unity 编译 | `2022.3.62f3c1` 实际导入/编译成功；`EditorUtility.scriptCompilationFailed=false`、`EditorApplication.isCompiling=false`；正式类型载入 Foundation Runtime | 未新增 asmdef/依赖，未使用 dotnet 构建替代 Unity |
| EditMode 内核回归 | TimerSchedulerContractTests **28/28** | A 内核在本轮采样、Scope、存储代次接入后的直接回归 |
| EditMode Scope/Group | TimerOwnershipTests **29/29** | 名称/替换/匿名归属/控制、Dispose/重入、异常继续、终态清引用、组隔离、非创建及累计溢出拒绝 |
| EditMode 复用与日期 | ReusableTimerTests **9/9** | 私有存储复用、新身份、旧句柄、末轮与取消 callback 重启、旧 finally/异常隔离、永久关闭及日期薄转换 |
| EditMode 自动时钟 | TimerAutomaticClockTests **3/3** | 可控采样下冷启动/未锚定暂停、登记前采样排除当帧、Fixed 秒与已知帧域分离 |
| EditMode 程序集边界 | FoundationBoundaryTests **5/5** | 复用现存正式程序集依赖断言 |
| PlayMode 默认服务 | TimerDriverPlayModeTests **11/11** | 真实 Update/LateUpdate/FixedUpdate、timeScale=0、DateTime、Shutdown/disable/destroy、同会话重投、定向 session/quit、异常及真实临时场景 DDOL 保活 |

- 实际执行 EditMode 74 项、PlayMode 11 项，全部通过，0 失败、0 跳过。MCP Summary.TotalTests 为全项目发现数量，不是执行全项目；初次组合 class 字符串过滤实际执行 0 项，未作为通过证据，随后按单类分别运行取得上表结果。
- 首次测试编译暴露当前 NUnit 不支持 NonParallelizable，移除测试专属标记后重新 Unity 编译与运行，不新增依赖。替换累计溢出新增两例在修复前实际失败（旧责任已失活），前移校验后两例及相关回归通过。PlayMode 预期日志 `YLTimer.ExpectedFailure` 已由 LogAssert 断言，不是未处理失败。
- Session/迟到生命周期/quit 使用 Timer 专属反射身份模拟，不调用公共全域 ResetForNewSession，不修改 SessionResets 列表；不是关闭 Domain Reload 的真实连续两轮 Play。该真实验收仍归 D。
- Fixture 拒绝接管已有 TimerDriver，记录并仅清理自己创建的 host/probe/临时场景，恢复 timeScale/session/gate。测试后已退出 Play，TimerDriver 数为 0，原 `Assets/Scenes/Main.unity` 保持打开且 dirty=false，没有保存或覆盖场景。
- 已检查实际差异和新增源码格式；9 个新增脚本均有 Unity 生成的 `.meta`，AssetDatabase 查询确认 9 个有效且互异 GUID。Unity `.meta` 模板空字段自带尾随空格，保留生成格式，未为格式检查重写资产。未新增 asmdef、配置或资源绑定依赖。
- 独立只读复核确认累计溢出问题已关闭，未发现其余实质缺陷；该静态复核不替代上表实际运行证据。未重跑 Pool/Events/Update 历史全集，未运行 Player 构建、性能基准，也未声称实测 NonAlloc 全链路零 GC。

### 10.4 边界与进入 C 条件

B 正式控制体系已具备继续 C 的基础，待本批用户验收与 Git 收口；本轮在 B 停止，不自动启动 C。新服务无需手动挂载驱动；首次有效投递自建宿主，调用方显式持有并关闭 Scope/复用槽或取消 handle。

未实现 GameObject Binding、TimerObject 私有实现迁移、Core TimerManager 接入/删除、Timer 终态 Coroutine Wait 或窄 Coroutine 观察。旧 `Y/Tools/Timing` 与自有 `C/Manager/TimerManager` 源码和原测试完整保留，原 XF 包及 CGJ2024 消费者未修改；因此本批只确认**正式 YLFramework.Timing 服务使用一套内核与一个默认驱动**，不宣称全工程已消除所有旧 Timer。

C/D 仍需完成上述旧消费者/实现收敛、相关资源身份与许可/模块文档收口、真实关闭 Domain Reload 连续 Play 验收及必要跨领域代表验证。Action、UpdateManager、Events、Pool 的公共契约及 MVC/View 生命周期均未修改。
