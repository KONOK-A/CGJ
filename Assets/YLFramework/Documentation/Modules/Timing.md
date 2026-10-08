# Timing（计时工具）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 定位

按需使用的 Unity 主线程计时工具。Timer 保留缩放 / 非缩放双堆，负责独立延迟与有限 / 无限周期到期回调；提供状态、剩余时间、暂停恢复，以及独立的命名与清理范围。逐帧进度、插值、条件和组合流程交给 ActionKit，不要求 AudioKit 播放链或 ActionKit 调度改用 Timer。

模块归入 Tools：职责具体，供调用方按需使用；虽有宿主和长期任务状态，但不是其他模块共同建立其上的基础机制，也不承担大型框架服务职责。

## 位置与接入

- 运行时：[`Runtime/Tools/Timing/`](../../Runtime/Tools/Timing/)，归属现有 `YLFramework.Runtime` 程序集，无独立 asmdef。
- namespace：`YLFramework.Utilities.Timing`。
- `Internal/` 放置堆实现与契约，目录名称不改变其 public 可见性。
- 业务 asmdef 按需引用 `YLFramework.Runtime`；Action / Audio 也已归入该程序集，但与 Timing 保持独立的调度、名称与生命周期职责。

## 投递入口

```csharp
using UnityEngine;
using YLFramework.Utilities.Timing;

// 运行时投递一次缩放时间任务；回调参数为从 1 开始的触发次数。
var item = Timer.Instance.Post2Scale(tick => Debug.Log(tick), 1f);

// 取消会立即移除待执行任务；不会关闭宿主。
item.Cancel();
```

- `Timer.Instance`：按需获取默认宿主，由 Unity `Update` 驱动。
- `Post2Scale`：基于 `Time.time`，受 `Time.timeScale` 影响。
- `Post2Really`：基于 `Time.unscaledTime`，不是 `Time.realtimeSinceStartup`；`DateTime` 重载将本地目标时间换算为延迟，过去时间按零延迟处理。
- 默认执行一次；带 `repeat` 的重载支持指定正数次数，负数表示无限重复。回调参数从 1 开始计数。
- 原两参数 `Post2Scale(callback, delay)` / `Post2Really(callback, delay)` 保持；三参数 repeat 重载增加可选 `TimerScope scope = null`。`Post2Scale(TimeItem item, TimerScope scope = null)` / `Post2Really(TimeItem item, TimerScope scope = null)` 也可使用显式范围。两参数或 DateTime 重载不直接接收 scope，需要范围时使用 repeat 重载。
- 命名入口为 `PostNamed(string flag, Action<int> callback, float delay, bool ignoreTimeScale = false, int repeat = 1, TimerScope scope = null)`；`ReplaceNamed` 同签名。它们是 `Timer.Instance` 上的实例方法，默认使用缩放时间。
- 所有未传 scope 的投递归入本宿主的 `DefaultScope`，可通过 `Timer.Instance.DefaultScope` 取得。显式 `new TimerScope()` 不创建宿主，可同时持有命名与匿名任务。

### 双堆时序

- `UpdateMgr()` 先处理非缩放堆，再处理缩放堆；只有时钟**严格超过** deadline 才执行，等于 deadline 时继续等待。
- 重复间隔从本次更新的 now 重新计算，不追赶补发；同 deadline 不保证 FIFO。零或负间隔按零等待处理，自动重复需等待下一次时钟推进，不会同步调用或同帧无限重复。
- 到期回调在帧更新中执行，不是后台线程或精确实时调度。暂停任务移出堆，不阻塞堆顶，也不参与逐帧扫描；所有权集合仅用于管理与清理，Update 仍处理两个堆。

## 任务状态与控制

`TimeItem.State` 为只读 `TimerState`，与回调释放 / `IsRecycled` 分开保存：

| 状态 | 含义 | RemainingSeconds |
|---|---|---|
| `Created` | 已配置，尚未投递 | 0 |
| `Scheduled` | 等待当前一轮到期 | 所属时钟的 deadline - now，截断为非负 |
| `Executing` | 当前用户回调正在执行 | 0 |
| `Paused` | 等待已暂停 | 冻结的当前一轮剩余时间 |
| `Completed` / `Cancelled` / `Faulted` | 正常完成 / 取消 / 回调异常 | 0，清理后保留终态 |

- `IsActive` 在宿主仍存活且状态为 Scheduled、Executing、Paused 时为 true；重复任务的 RemainingSeconds 只代表当前一轮，不是全部重复的总时长。
- `Pause()` / `Resume()` 返回 bool。暂停将等待任务立即移出所属堆并冻结剩余时间；恢复按**同一时钟**的当前时间 + 冻结值重新入堆，不切换 clock。终态操作失败，不会隐式复活任务。
- 循环回调内 Pause 表示暂停**下一轮完整周期**；同回调内 Resume 可撤销此暂停请求。单次或最后一轮已开始执行时 Pause 返回 false，不撤销当前回调。
- `Cancel()` 立即移除待执行或暂停任务并释放回调引用；执行中的取消不强行打断当前 callback，但阻止后续重复。取消优先于暂停 / 重复，重复取消不改写已有终态。
- 回调异常继续向上传播；未被取消或显式重投递的任务按 Faulted 清理，不会当作正常完成。回调内显式重投递属于新调度租约，旧 finally 不清理新租约。

公开 TimeItem 不隐式进入共享对象池。仍被宿主持有的任务（包括暂停、执行中）不能直接 `Set` 或 `Recycle2Cache`；不要自行改写调度中的 SortScore / HeapIndex。现有 Set 是未被持有时的重新配置入口，不是运行中改周期 / 重复次数的替代 API。

## 命名与 TimerScope

```csharp
using UnityEngine;
using YLFramework.Utilities.Timing;

var scope = new TimerScope();
var item = Timer.Instance.PostNamed("refresh", tick => Debug.Log(tick), 1f,
    ignoreTimeScale: true, repeat: -1, scope: scope);

scope.Pause("refresh");
Debug.Log(item.RemainingSeconds);
scope.Resume("refresh");
Timer.Instance.ReplaceNamed("refresh", tick => Debug.Log(tick), 2f, scope: scope);
// 所有者结束时关闭范围，取消命名与匿名任务；不关闭 Timer。
scope.Dispose();
```

- flag 必须是非 null、非空白字符串，按 `StringComparer.Ordinal` 区分大小写；同 scope 拒绝有效重名，不影响旧任务。不同 scope，以及 Timer 与 Action 之间可以使用相同名字。
- ReplaceNamed 先验证参数，再取消同范围旧任务并投递新任务；新投递失败不恢复旧任务，不是事务性替换。
- `TimerScope` 提供 `Contains(flag)`、`TryGet(flag, out TimeItem item)`、`Cancel(flag)`、`Pause(flag)`、`Resume(flag)`，查询与控制不创建宿主；未找到 / 已结束时返回 false，非法 flag 仍抛出参数异常。
- 静态 `Timer.TryGet(string flag, out TimeItem item, TimerScope scope = null)` 与 `Timer.Contains` / `Cancel` / `Pause` / `Resume` 同样是非创建式入口。未传 scope 时只查现有宿主的默认范围；显式访问 Timer.Instance 本身仍可能创建宿主。
- 单次 / 最后一轮在进入用户 callback **之前释放名字**；循环的非最后一轮回调仍占名。完成、取消、异常及宿主释放清理登记；旧收尾按本次租约核验，不会删除回调中新投递的同名任务。
- `CancelAll()` 取消范围内全部命名与匿名任务，范围仍可再次使用。`Dispose()` 先关闭范围再取消全部任务，阻止清理重入投递；重复 Dispose 无副作用，关闭后投递抛出 ObjectDisposedException。
- scope 只管任务与名字，不提供 Update、MonoBehaviour 或独立执行器，也不自动绑定调用方 Unity 对象。

## 宿主与生命周期

Timer 宿主复用 Foundation 的 `MonoSingletonHost<T>`，自建运行时宿主按需归入 `YLFramework/Timing`，借用对象保持原层级，无需预先挂载组件。调用方对象停用、销毁或场景切换不会自动取消其任务，应明确选择生命周期边界清理：

```csharp
using UnityEngine;
using YLFramework.Utilities.Timing;

public class TimerOwnerExample : MonoBehaviour
{
    private TimerScope mScope;

    private void OnEnable()
    {
        mScope = new TimerScope();
        Timer.Instance.PostNamed("ui-delay", _ => Debug.Log("completed"), 1f, scope: mScope);
        Timer.Instance.Post2Really(_ => Debug.Log("anonymous"), 2f, 1, mScope);
    }

    private void OnDisable() => mScope?.Dispose();
}
```

此示例停用即关闭范围，再次启用时新建范围；若任务允许跨停用存活，可改在 OnDestroy 或明确的场景边界 Dispose。

- 普通所有者仅用自有 TimeItem.Cancel、scope.CancelAll / Dispose，或 `TimerHelper.Add(item)` / `Clear()` 收集与取消任务；不要把模块级 Shutdown 当作普通 owner cleanup。
- `ResetMgr()` 取消该宿主全部任务，包括暂停与执行中任务；`StartMgr()` 同步时钟，`UpdateMgr()` 为手动更新入口，默认宿主无需额外逐帧调用。
- `Timer.Shutdown()` 面向整个 Timing 宿主与任务清理，并关闭旧宿主的 DefaultScope；之后新宿主拥有新的默认范围。显式 scope 不随宿主关闭而自动 Dispose，但其在该宿主上的任务会被取消。
- 宿主销毁、Shutdown 与 Session reset 清理任务和名称；Timer 清理不代表关闭 ActionKit 或 AudioKit。
- 从未激活的借用宿主销毁时 Unity 不调用 OnDestroy；命名查询会补清该失效宿主的任务，不创建替代宿主，句柄已立即失效。

## TimeExtend 迁移

旧 TimeExtend 的 Timer / TimerDriver / TimerExtend 退出，不保留兼容门面、独立 Driver 或旧 clock：

| 原需求 | 当前选择 |
|---|---|
| 延迟 / 周期到期回调、暂停恢复 | Timer 的 Post2Scale / Post2Really 或 PostNamed / ReplaceNamed，配合 TimeItem |
| flag 查询、取消与所有者批量清理 | TimerScope；Action 根动作使用自己的命名执行入口 |
| OnUpdated 逐帧进度、插值、组合流程 | ActionKit 的 Delay / Lerp / Repeat、Sequence / Parallel 等组合 |

Timer 默认缩放时间，ignoreTimeScale 使用 unscaledTime，不保留旧默认 realtimeSinceStartup；不提供动态 SetTime / SetLoop / 时钟切换。Action 保持自己的首帧、Delay 先比较后累计、Lerp 起点通知与 Repeat 下一轮启动行为；不能把迁移理解成首帧、循环、异常或 clock 完全等价。

Action 根动作入口为 `StartNamed(owner, flag)` / `ReplaceNamed(owner, flag)`，以及 `StartNamedCurrentScene` / `StartNamedGlobal` 和对应 ReplaceNamedCurrentScene / ReplaceNamedGlobal。查询与控制为 `ActionKit.TryGetNamed` / `ContainsNamed` / `CancelNamed` / `PauseNamed` / `ResumeNamed`，场景与全局版本分别追加 CurrentScene / Global 后缀，均不创建执行器。对象作用域是 owner 的 GameObject，不是单个组件；返回的 `ActionHandle` 仅属于 Action，结束或池复用后失效，不与 TimerScope 共用状态。详细 Action 接入见 [ActionKit 文档](Action.md)。

编辑器 Timing 指南仍由 Audio 提供方的 `timer` 页承载，不注册独立 Timing Provider；见 [Audio 文档](Audio.md#测试与帮助)。

## 公开类型

| namespace / 目录 | 类型 |
|---|---|
| `YLFramework.Utilities.Timing` / 根目录 | `Timer`、`TimeItem`、`TimerState`、`TimerScope`、`TimerHelper` |
| `YLFramework.Utilities.Timing` / `Internal/` | `BinaryHeap<T>`、`IBinaryHeapElement`、`BinaryHeapSortMode`、`BinaryHeapBuildMode` |

`BinaryHeap<T>` 支持最小 / 最大分数优先与批量建堆，堆位置从 1 开始，0 表示未入堆。它只按 `SortScore` 排序，**不保证相同分数的 FIFO 顺序**；业务需要稳定顺序时不能依赖其插入次序。

## 边界与来源

不提供线程安全、跨线程调度、持久化任务、后台运行或高精度时钟保证。全部 Unity 对象访问与任务操作应在 Unity 主线程进行。

源码保留原作者与许可声明，包含 QFramework、snowcold / liangxie 相关来源；具体作者、年份及 MIT 许可以各源文件为准，分发时保留对应声明。

## 当前状态

已实现。专项 EditMode / PlayMode 测试覆盖双时钟、任务状态、暂停恢复、作用域与替换、回调重入及宿主释放重建。
