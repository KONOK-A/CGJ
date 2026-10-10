# Timing（公共计时服务）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 定位

Foundation 中唯一的独立到期任务内核，提供 Scaled / Unscaled 秒、rendered frame、有限 / 无限周期、暂停、精确句柄、Scope、显式复用与 GameObject 生命周期绑定。默认服务和手动实例共享算法，不保留 Core 或 Tools 的第二套调度。

逐帧进度、插值、条件和动作树仍交给 ActionKit。Timer 不改写 Action 的首步、Delay、Sequence、Repeat 或执行所有权，也不接管 Audio 播放链。

## 位置与接入

- 运行时：[`Runtime/Shared/Timing/`](../../Runtime/Shared/Timing/)，归属 `YLFramework.Foundation.Runtime`，namespace 为 `YLFramework.Timing`。
- 业务 asmdef 显式引用 Foundation；Core / Runtime 向下引用 Foundation，计时不依赖 Core、Root、UI、原 XF 或 XFAB。
- `Internal/` 的 run、堆、生命周期监听和默认驱动均为实现细节，不公开可写堆字段。
- Core `TimerObject` 仍位于原脚本路径，无需迁移 Inspector 绑定；默认 TimerDriver 自动建立，不手动挂载。

## 投递入口

```csharp
using UnityEngine;
using YLFramework.Timing;

var once = Timer.Delay(1, () => Debug.Log("completed")); // 默认 Unscaled。
var scaled = Timer.Delay(1, clock: TimerClock.Scaled); // null callback 可用于纯等待。
var repeat = Timer.Repeat(0, 1, tick => Debug.Log(tick), count: 5);
var frames = Timer.DelayFrames(2);
once.Cancel(); // 仅取消捕获的 run，不关闭宿主。
```

- `Timer.Delay` / `Repeat` 默认 Unscaled，显式 `TimerClock.Scaled` 受 timeScale 影响；`DelayFrames` / `RepeatFrames` 按渲染 Update 帧，不按 FixedUpdate 或 Action.Execute 次数。
- `Repeat` 的 firstDelay 与 interval 独立；count 为含首轮的正数总次数，null 为无限，回调 long 轮数从 1 开始。拒绝负 delay、非有限秒、非正 interval/count；零首次 delay 合法。
- `Timer.At(DateTimeOffset, callback)` 只在接受请求时换算一次非负 Unscaled delay；过去目标按零等待，不持续跟踪墙钟、不提供后台闹钟。
- 显式 `new TimerScheduler(0, 0, 0)` 用 `Advance(scaled, unscaled, renderedFrame)` 输入有限、非负、非回拨样本；不读 Unity Time、不创建宿主。自动实例没有手动推进入口。

### 时钟与时序

- 到期包含等号（now >= deadline）；同一域按 deadline、登记序号稳定排序，各域依次 Unscaled、Scaled、Frame，不承诺跨域物理时间全序。
- 登记不 inline，也不进入登记帧/当前 callback 派发；fixed-delay 为实际派发 now + interval，每任务每帧最多一次、不补发。递归 Advance 为空操作。
- 已知登记帧时 n>0 表示 n 个未来渲染帧，0 等同下一合法帧。冷启动尚无合法 Update 样本时，首个 Update 建立登记帧后再等 max(1,n) 帧。
- 首个合法 Update 前秒任务未锚定；Awake/Fixed 中登记的秒等待在下个合法 Update 锚定。已就绪的非 Fixed 登记/控制/查询使用同一渲染样本协议，不把 fixed time 当渲染秒时间。
- 三域使用索引堆，取消/暂停为 O(log N)；显式 GO owner 的存活兜底每次推进为 O(B)，失效 owner 再清理其绑定。暂停任务仍受生命周期约束。

## 任务状态与控制

`TimerHandle` 只控制捕获的 owner/run 代次，普通句柄保留稳定终态；默认值与复用槽旧代次为 Invalidated。

| 状态 | 含义 | RemainingSeconds |
|---|---|---|
| `Scheduled` | 等待当前一轮到期 | 所属时钟的 deadline - now，截断为非负 |
| `Executing` | 当前用户回调正在执行 | 0 |
| `Paused` | 等待已暂停 | 冻结的当前一轮剩余时间 |
| `Completed` / `Cancelled` / `Faulted` / `Invalidated` | 完成 / 取消 / 故障 / owner 或代次失效 | 0，终态清理归属、监听和委托 |

- 单次/末轮在 callback 前消费责任，释放名称与绑定；此时 IsActive=false、IsFinished=false、Status=Executing。正常退出 Completed，抛错 Faulted，不能以 !IsActive 当成功。
- `Pause()` 冻结当前剩余秒/帧；非末轮 callback 内冻结下一完整 interval，退出前仍为 Executing。同 callback Resume 可撤销暂停。Resume 排除当前派发，终态不能复活。
- `Cancel()` 先提交 Cancelled、撤销调度及引用，再通知 onCancelled；执行中的非末轮可以取消后续轮次，末轮取消无效。owner 结束为 Invalidated；故障不是取消，不触发传统 Abort。
- 失败任务停止重复，其余就绪任务继续；默认运行边界逐项日志报告，手动操作在必要收尾后传播原始/聚合异常。重复取消不重复通知。
- `RemainingSeconds` / `RemainingFrames` 是所属时钟最近合法样本的快照，只表示当前一轮；非对应时钟、执行中和终态为零。

## 命名与 TimerScope

```csharp
using UnityEngine;
using YLFramework.Timing;

var scope = new TimerScope();
var run = scope.PostNamed("refresh", 1, 1, tick => Debug.Log(tick), count: null);

scope.Pause("refresh");
Debug.Log(run.RemainingSeconds);
scope.Resume("refresh");
scope.ReplaceNamed("refresh", 2, () => Debug.Log("replacement"));
// 所有者结束时关闭范围，取消命名与匿名任务；不关闭 Timer。
scope.Dispose();
```

- 名称须非空白，使用 Ordinal；活跃同名投递拒绝，不同 Scope 和 Action 可同名。提供 Contains、TryGet(out TimerHandle)、按名 Cancel/Pause/Resume，以及秒/帧匿名和命名入口。
- ReplaceNamed 先校验，再取消旧责任；取消通知内新同名投递优先，外层返回默认句柄，不覆盖新任务。失败不回滚已取消的旧任务。
- CancelAll 捕获并尝试全部旧责任，重入新增留下一轮；Scope 仍可复用。Dispose 先永久关闭再清理，关闭后不接受投递。
- Scope 不创建独立调度器，不自动绑定任意场景或对象。同会话 host 重建后可显式重投，旧 Scope/复用槽不跨新 session 收养。手动 Scope 永久绑定显式实例。

## 显式复用与收集

```csharp
var slot = new ReusableTimer(); // 或 new ReusableTimer(manualScheduler)。
var oldRun = slot.Start(1);
oldRun.Cancel();
var newRun = slot.StartFrames(1); // oldRun 现在为 Invalidated，不能控制 newRun。
slot.Dispose(); // 永久关闭。

var group = new TimerHandleGroup();
group.Add(newRun);
group.Clear(); // 取消并释放已交付的旧句柄，不改变 Scope 归属。
```

活跃 run 须先显式 Cancel 才能重启；末轮 callback 内可直接 Start，新代次不受旧 finally 影响。普通句柄保留终态，复用槽不永久保存全部历史结果，需要时在下次 Start 前复制。NonAlloc 只指预热槽与缓存委托可避免任务状态分配，不保证绑定适配对象、闭包、扩容、异常或等待器全链路零 GC；GO 绑定每次接受新 run 会创建适配记录。

## 宿主与生命周期

首次有效 Play 投递惰性建立独立 DDOL `YLFramework.Timing.TimerDriver`，不创建 Root/UI、不借用场景组件。查询、控制、清理不创建宿主；disabled/released host 拒绝新投递，Shutdown 或销毁后可显式新建 owner，旧任务不恢复。

```csharp
TimerHandle run = Timer.Delay(1, callback);
bool bound = run.BindTo(gameObject); // GO 停用即取消。
// 或 run.BindTo(gameObject, TimerGameObjectLifetime.CancelOnDestroy)：停用继续，销毁取消。
```

- BindTo 只接受活跃的精确 run，可重绑，先清旧对象。已销毁或停用策略下已 inactive 的 owner 立即取消并返回 false，不执行成功 callback；终态/旧代次返回 false。
- paused run 仍受生命周期约束。从未 active 的对象销毁可能没有 OnDestroy，由默认 Update/手动 Advance 存活兜底结束。绑定不把停用解释为暂停，也不把单个组件禁用当成 GO 停用。
- 生命周期取消通知中不要嵌套对正在 SetActive 的同一个 GO 再调用 SetActive，Unity 会拒绝重入激活。可在后续帧重新启用、重绑其他对象，或按组件自己的启停边界重启。
- Core TimerObject 保留 Core 类型、脚本 GUID 与 delay/ignoreTimeScale/Action/Abort；每次启用启动复用槽，自身组件禁用也精确取消。成功 callback 自停用不 Abort，Faulted 不 Abort，取消只通知一次；Action/Abort 内合法重启不会被旧收尾取消。
- 无 owner 的默认任务跨场景；模块/View/Panel 不自动接管外部任务。普通所有者用自有 handle、Scope 或 Group 清理，不调用全局 Shutdown 代替局部结束。
- Timer.Shutdown、driver 失效、session reset 只结束 Timer owner 的旧责任，不清公共 Update/Events/Action/CoroutineStarter，不自动续跑旧任务。

## 终态等待与协程观察

```csharp
TimerWait wait = run.WaitForTermination();
yield return wait; // 仅观察，不拥有 Timer。
if (wait.IsCompleted) { /* 正常计时完成 */ }
else { /* 用 wait.Status 区分 Cancelled / Faulted / Invalidated */ }

var observation = YLFramework.Foundation.CoroutineObservation.Observe(producer, callback);
if (!observation.IsStarted) { /* Starter 未接受观察 */ }
observation.Dispose(); // 只停止观察 wrapper，绝不停止 producer。
```

停止等待协程不取消 Timer。TimerWait 捕获精确 run，首次看到终态后保存快照；复用槽在观察前重启时旧结果只能为 Invalidated，不跟随新任务。

CoroutineObservation 不属于 Timer，不提供业务成功或统一异步终态协议。仅用 CoroutineStarter 原 owner 路由观察原生 `yield return producer`，callback 只表示原生等待结束。Unity 2022.3 同一 Coroutine 只允许一个 waiter，已有 waiter 时启动可能被拒绝，必须检查 IsStarted；不会为此建立多播/轮询生产者状态系统。

本工程 `2022.3.62f3c1` 定向 PlayMode 实测：自然结束触发 callback 一次；Stop producer 或销毁 producer owner 后，六个后续渲染帧内原生等待未恢复、观察 callback 为零。不可依赖它们触发取消/成功通知；需要结束观察时由观察所有者 Dispose。Starter owner 失效也不保证 callback，不恢复旧观察。

## TimeExtend 迁移

旧 TimeExtend 的 Timer / TimerDriver / TimerExtend 退出，不保留兼容门面、独立 Driver 或旧 clock：

| 原需求 | 当前选择 |
|---|---|
| 延迟 / 周期到期回调、暂停恢复 | Timer.Delay / Repeat、DelayFrames / RepeatFrames，配合 TimerHandle |
| flag 查询、取消与所有者批量清理 | TimerScope；Action 根动作使用自己的命名执行入口 |
| OnUpdated 逐帧进度、插值、组合流程 | ActionKit 的 Delay / Lerp / Repeat、Sequence / Parallel 等组合 |

旧 Core TimerManager/Core.Timer/TimerType 和 Tools/Timing 的 Timer/TimeItem/TimerHelper/public Heap 已退出新 YL，不留兼容门面；原 XF Package 与原游戏继续使用自己的原类型，不在本次迁移范围。

旧 Post2Scale 用显式 Scaled，Post2Really 用默认 Unscaled，Helper 用 TimerHandleGroup，NonAlloc 用 ReusableTimer。重要变化：默认 Unscaled、到期含等号、firstDelay 独立、有限 count 为正数、无限为 null、帧 n 为未来 n 帧、失败任务不阻断其余任务；可变 Set/Post(existing) 改为新 run，不公开永久 DefaultScope。

Action 根动作入口为 `StartNamed(owner, flag)` / `ReplaceNamed(owner, flag)`，以及 `StartNamedCurrentScene` / `StartNamedGlobal` 和对应 ReplaceNamedCurrentScene / ReplaceNamedGlobal。查询与控制为 `ActionKit.TryGetNamed` / `ContainsNamed` / `CancelNamed` / `PauseNamed` / `ResumeNamed`，场景与全局版本分别追加 CurrentScene / Global 后缀，均不创建执行器。对象作用域是 owner 的 GameObject，不是单个组件；返回的 `ActionHandle` 仅属于 Action，结束或池复用后失效，不与 TimerScope 共用状态。详细 Action 接入见 [ActionKit 文档](Action.md)。

编辑器 Timing 指南仍由 Audio 提供方的 `timer` 页承载，不注册独立 Timing Provider；见 [Audio 文档](Audio.md#测试与帮助)。

## 公开类型

| namespace / 目录 | 类型 |
|---|---|
| `YLFramework.Timing` / `Shared/Timing` | Timer、TimerScheduler、TimerHandle、TimerState、TimerClock、TimerCancellationReason、TimerScope、ReusableTimer、TimerHandleGroup、TimerWait、TimerGameObjectLifetime、TimerGameObjectExtensions |
| `YLFramework.Foundation` / `Shared` | CoroutineObservation（不调度计时任务） |

## 边界与来源

不提供线程安全、跨线程调度、持久化任务、后台运行或高精度时钟保证。全部 Unity 对象访问与任务操作应在 Unity 主线程进行。

源码保留 QFramework、snowcold / liangxie、UnityTimer 来源与完整 MIT；承接 XF 的部分继续受 Shared/LICENSE.md 覆盖，集中声明见 Runtime/THIRD-PARTY-NOTICES.md。删除旧实现不删除被吸收代码的许可，分发时保留对应声明。

## 当前状态

2.4-C 实施及专项验证完成，待用户验收与 Git 收口；2.4 仍为 IN PROGRESS。实际 Unity 编译、定向内核/Scope/复用/绑定/等待 EditMode、默认驱动/TimerObject/观察 PlayMode 已验证；完整专题 API/命名/许可复查、真实关闭 Domain Reload 连续两轮 Play 及最终专题验收仍归 D。详细证据与历史记录见现有 [Timer 专项方案](../../../../.kilo/plans/xf-yl-2.4-timer.md)，不以专项通过替代 Player/IL2CPP、完整游戏或性能基准。
