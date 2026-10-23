# ActionKit

> [文档首页](../README.md) · [架构总览](../Architecture.md) · [Audio](Audio.md) · [Timing](Timing.md)

## 模块定位

ActionKit 是 Unity 动作编排模块：把回调、等待、条件、插值等步骤组合成可启动的动作链（Sequence / Parallel / Repeat / Delay / Lerp / Coroutine / Task 等），并绑定到对象、当前场景或跨场景全局宿主执行。

- 主要能力：动作创建与组合、逐帧推进、暂停/取消、池化复用、屏幕过渡、全局 Unity 消息转发、内建 BCL Observable，以及可选 DOTween 扩展。
- 运行时位于 `Runtime/Systems/Action/`，namespace 为 `YLFramework.Actions`，公共入口 `ActionKit` 不改名；归属共享 `YLFramework.Runtime`，不是 Foundation 或 Timing 的子模块。
- 不负责业务资源管线、依赖注入或统一对象生命周期；状态机使用独立的 [FSM 模块](FSM.md)。动作统一使用 `IAction` 契约，不提供旧版 ActionKit 兼容 API。
- API 浏览：`YLFramework/Action/帮助`，由 [Help 核心](Help.md) 承载。

## 目录与依赖

路径相对 `Assets/YLFramework/`：

| 目录 | 职责 | 程序集 |
|---|---|---|
| [`Runtime/Systems/Action/`](../../Runtime/Systems/Action/) | Core、Actions、Execution、Internal、ScreenTransition、Extensions | `YLFramework.Runtime` |
| [`Runtime/Systems/Action/Observable/`](../../Runtime/Systems/Action/Observable/) | 内建 BCL `IObservable<T>` 适配，无 UniRx 包依赖 | 同上 |
| [`Runtime/Systems/Action/Resources/`](../../Runtime/Systems/Action/Resources/) | 屏幕过渡 Prefab | 同上 |
| [`Editor/Systems/Action/`](../../Editor/Systems/Action/) | 资源创建工具与 Help 提供方 | `YLFramework.Editor` |
| [`Integrations/DOTween/Runtime/`](../../Integrations/DOTween/Runtime/) | 可选 `Tween` 适配 | `YLFramework.Actions.DOTween` |

Runtime 引用既有 ZipTool 与 `UnityEngine.UI`；普通 Editor 引用 Runtime、Help、UI。二者不静态引用 DOTween wrapper 或供应商 DLL。Runtime / 普通 Editor / ZipTool 的 `autoReferenced` 为 true，Help / optional / tests 为 false；完整程序集契约见 [架构总览](../Architecture.md)。

DOTween wrapper 显式引用 Runtime + `DOTween.dll`，要求 `DOTWEEN`；供应商 DLL 位于包外 `Assets/Plugins/Demigiant/DOTween/DOTween.dll`。可选源码另行引入其依赖，不复制 Core / Vendor；默认导出不包含该扩展或 DLL。

## Core Concepts

- `ActionKit`（partial class，全体成员静态）：工厂入口 + 7 个全局消息事件（`OnUpdate` / `OnFixedUpdate` / `OnLateUpdate` / `OnGUI` / `OnApplicationQuit` / `OnApplicationPause` / `OnApplicationFocus`）。
- `IAction` / `AbstractAction<T>`：`ActionStatus` 状态机与 `OnStart` / `OnExecute` / `OnFinish` / `OnDeinit` 契约；自定义动作继承 `AbstractAction<T>`。
- `IActionController` / `ActionController`：匿名 `Start` 返回的池化控制器，含 `UpdateMode` 与 `IgnoreTimeScale` 扩展；取消即 `Deinit()`，回收后不可继续操作原始引用。
- `ActionHandle`：命名根执行返回的只读值类型安全句柄，每次操作验证动作标识、控制器代次和名字登记；失效后不影响后来同名或池复用的任务。
- 执行器：`IActionExecutor` 契约 + internal `MonoUpdateActionExecutor`（每个宿主 GameObject 一个实例，由 Unity Update 驱动）。
- 组合节点：`Sequence`、`Parallel`、`Repeat`、`Condition`、`ConditionSequence`、`Lerp`、`Delay`、`DelayFrame`、`Callback`、`Coroutine`、`Task`、`Custom<TData>`。
- 启动范围：`Start(owner)` / `StartCurrentScene()` / `StartGlobal()`，以及 `Pause` / `Resume`；扩展所在实际类型为 `IActionExtensions`。
- 事件与注销：7 个全局消息返回 `YLFramework.Events` 的 `EasyEvent` / `EasyEvent<bool>`；订阅、注销句柄与生命周期触发器统一使用 [Events 模块](Events.md)，Action 不再自带订阅层。
- ScreenTransition：`ActionKit.ScreenTransition.FadeIn` / `FadeOut` / `FadeInOut` + internal 共享画布 `ScreenTransitionCanvas`。

## Main Entry Points

按阅读顺序：

1. [`Core/IAction.cs`](../../Runtime/Systems/Action/Core/IAction.cs)：动作契约与池化基类。
2. [`Core/ActionKit.cs`](../../Runtime/Systems/Action/Core/ActionKit.cs)：工厂与全局消息；生命周期见同目录 `ActionKitLifecycle.cs`。
3. [`Extensions/ActionStartExtensions.cs`](../../Runtime/Systems/Action/Extensions/ActionStartExtensions.cs)、[`ActionNamedExtensions.cs`](../../Runtime/Systems/Action/Extensions/ActionNamedExtensions.cs)：匿名与命名执行。
4. [`Execution/MonoUpdateActionExecutor.cs`](../../Runtime/Systems/Action/Execution/MonoUpdateActionExecutor.cs)、[`ActionHandle.cs`](../../Runtime/Systems/Action/Execution/ActionHandle.cs)：推进、回收与租约。
5. [`Actions/Sequence.cs`](../../Runtime/Systems/Action/Actions/Sequence.cs)：组合实现范式；Delay / Task 同目录。
6. [`Editor/Systems/Action/Help/ActionKitHelpProvider.cs`](../../Editor/Systems/Action/Help/ActionKitHelpProvider.cs)：注册与内容组装。

## Runtime Flow

```text
创建：ActionKit.Sequence()/Delay() → Xxx.Allocate() → ActionPool<T>.Allocate()（取得 ActionId，未启动）
启动：action.Start(owner) → ActionKit.Initialize() → ActionController.Allocate() → owner.ExecuteByUpdate(...) → Step → OnStart
推进：Unity Update → MonoUpdateActionExecutor.Tick(Time.deltaTime, Time.unscaledDeltaTime) → IAction.Execute(dt) → OnExecute(dt)
完成：Finish() → 下一次 Execute 触发 OnFinish 并返回 true → 执行器调用外部 onFinish 回调 → Deinit → ActionQueue
      → ActionRecycleDriver.Update → Drain → ActionPool<T>.Recycle
取消：controller.Deinit() 或宿主销毁 → 动作 Deinit 入回收队列，不触发完成回调；控制器立即失效
```

行为要点：

- 禁用 GameObject 会暂停该执行器的更新；销毁 GameObject 会取消它持有的动作；禁用传入的组件不影响执行器。
- 即时动作（`Callback`、立即满足的 `Condition` 等）可能在 `Start` 返回前完成并回收，返回的控制器此时已无效。

## Named Execution

```csharp
using YLFramework.Actions;

var handle = ActionKit.Sequence()
    .Delay(1f)
    .Callback(() => Debug.Log("流程结束"))
    .StartNamed(this, "opening");
handle.SetUpdateMode(ActionUpdateModes.UnscaledDeltaTime);
handle.Pause();
handle.Resume();

if (ActionKit.TryGetNamed(this, "opening", out var current))
    current.Cancel(); // 不触发成功完成通知。

ActionKit.Delay(2f, null).ReplaceNamed(this, "opening");
```

- 启动：`StartNamed(owner, flag)`、`StartNamedCurrentScene(flag)`、`StartNamedGlobal(flag)`；每种都有 `Action<IActionController>` 或无参数 `Action` 正常完成通知重载。
- 替换：`ReplaceNamed(owner, flag)`、`ReplaceNamedCurrentScene(flag)`、`ReplaceNamedGlobal(flag)`。先验证新动作和参数，再取消旧同名任务；新启动或旧清理失败不会复活旧任务。清理重入新建同名任务时，外层替换会拒绝覆盖新登记。不能用仍被执行器持有的同一动作实例替换自己。
- 查询与控制：`ActionKit.TryGetNamed(owner, flag, out handle)`、`ContainsNamed`、`CancelNamed`、`PauseNamed`、`ResumeNamed`；场景和全局版本分别加 `CurrentScene`、`Global` 后缀且不接收 owner。只读现有宿主缓存和执行器，不创建对象或初始化模块。合法名字不存在或已失效时返回 `false`。
- 名字采用 `StringComparer.Ordinal`，区分大小写，所有命名入口拒绝 null/空白名字。默认有效重名抛出 `InvalidOperationException`，不消费新动作，也不扰动旧任务。匿名 `Start` 不创建名字字典或命名包装对象。
- 作用域为组件所属 GameObject，而非单个组件；其他对象、场景/全局执行器及 Timer 可以使用相同名字，索引彼此独立。沿用现有场景宿主行为，不因 activeScene 切换自动取消或新建作用域。
- 首次同步推进前登记；同步完成不留名。根完成通知中可以用新动作启动同名执行，旧收尾不删新登记；子节点回调和 Repeat 每轮回调仍属于活跃根执行，不提前释放名字。
- `ActionHandle.IsActive`、`Pause()`、`Resume()`、`Cancel()`、`SetUpdateMode(mode)` 每次核验原租约。默认值、正常完成、取消、异常、宿主销毁、Shutdown 后返回 `false`，控制器/动作池复用不使旧句柄重新有效；ActionId 为 0 也可有效。活跃动作 Reset 仅重置进度，不改变句柄租约。
- 对象停用仍只暂停自动 Update，组件停用仍不停止执行器；对象销毁取消所属任务。不要把命名接口当作 Timer 的时钟/重复算法，Delay、Lerp、Repeat 及 Shutdown 原语义未改。

全局任务不依附调用方对象；需要调用方退出时取消，应显式保存安全句柄并清理：

```csharp
private ActionHandle mBackground;
private void Start() => mBackground = ActionKit.Delay(10f, null).StartNamedGlobal("background");
private void OnDisable() => mBackground.Cancel();
```

## Extension Points

- 自定义动作：继承 `AbstractAction<T>`；或用 `ActionKit.Custom` / `Custom<TData>`、`ActionKit.Task`、`ActionKit.Coroutine` 包装逻辑。
- 组合扩展：`ISequence.Append` 与各 `XxxExtension`（`SequenceExtension`、`ParallelExtension`、`RepeatExtension`、`DelayExtension`、`LerpExtension`、`CustomExtension`、`TaskExtension` 等）。
- 执行器：`IActionExecutor` 是 public 接口并提供 `IActionExecutorExtensions.UpdateAction`，但 `Start` 路径固定使用 internal 的 `MonoUpdateActionExecutor`，当前没有公开注入点。
- 订阅生命周期：使用 `YLFramework.Events.UnregisterExtension` 与 `RegisterListExtension` 绑定注销句柄；`ActionKit.Shutdown` 只清空模块自有事件，不处置用户触发器或句柄。
- 池：`ActionPool<T>` 为 internal，无缓存数量上限，模块外不可配置。

## 生命周期与资源

- `Initialize()` 幂等懒初始化；`Start` 或全局宿主访问会自动调用。`Shutdown()` 取消模块管理的动作、释放全局宿主与事件；Session reset 处理关闭 Domain Reload 的残留。
- 全局 `OnUpdate` / `OnFixedUpdate` / `OnLateUpdate` 的自动通知复用 `YLFramework.Foundation.UpdateManager`。每个初始化成功且启用的全局宿主持有三个精确订阅，组件 disabled 或对象 inactive 时断开，恢复后重新连接；借用 inactive 宿主不会被自动激活。连接轮次与执行会话检查拒绝退订后仍留在公共快照中的旧回调。
- 七个属性继续返回模块自有 `EasyEvent` / `EasyEvent<bool>`；手动 Trigger、UnregisterAll、Dispose 及内部 fail-fast 语义保持。Action 监听抛异常会停止该消息的后续监听，公共源仍继续其他公共消费者并报告异常；清理 Action 消息或 Shutdown 不清理公共源的其他订阅。
- 初始化补丁中取得宿主不会提前连接，成功提交后刷新已有宿主；初始化失败期间不自动广播。本次不扩展为完整的补丁事务回滚。Shutdown/重建终结旧 owned 消息代次，旧 token 不进入新宿主。
- GUI/Focus/Pause/Quit 与 Action 协程仍由全局宿主处理；Quit 通知前设置共享会话退出屏障，监听中 Shutdown 后无法重建宿主或启动 Foundation 协程。需要注销的事件或句柄应提前保存，不在退出后重新访问创建式属性。
- 对象执行器与延迟回收驱动仍独立推进；全局消息、执行器和回收之间不保证执行全序。公共宿主被外部销毁或停用时不自动恢复既有工作；共享更新源不构成性能提升结论。
- 对象执行器、当前场景宿主、跨场景全局宿主保留独立生命周期；不能用 TimerScope 清理 Action，也不将 Action Delay 改成 Timer 包装。
- `ActionPool<T>` 无缓存数量上限，组合 YL 池并以 LIFO 复用；领域回收队列、回收标记与租约由 Action 管理。同一动作不能同时交给两个执行器。
- [`ScreenTransitionCanvas.prefab`](../../Runtime/Systems/Action/Resources/ActionKit/ScreenTransitionCanvas.prefab) 随模块归位，Resources key 为 `ActionKit/ScreenTransitionCanvas`；菜单 `YLFramework/Action/创建屏幕过渡资源` 创建该资源。不新增 YL Resource / XFAB Loader。
- 源码及资源移动保留原 meta / GUID；资源键已从 `ProjectActionKit` 更名为 `ActionKit`，与模块命名一致。

## 测试与帮助

- [`Tests/EditMode/Action/`](../../Tests/EditMode/Action/)：执行与回收、命名/重入/租约、生命周期、组合动作、屏幕资源、Help / API；Observable 测试同属普通 EditMode。事件与注销契约由 [`EventsContractTests.cs`](../../Tests/EditMode/EventsContractTests.cs) 覆盖。
- [`Tests/PlayMode/Action/`](../../Tests/PlayMode/Action/)：三种执行范围、真实帧延迟、宿主/场景销毁、全局消息、屏幕过渡与内建 Observable；事件生命周期触发器由 [`EventsLifecyclePlayModeTests.cs`](../../Tests/PlayMode/EventsLifecyclePlayModeTests.cs) 覆盖。
- [`Tests/Integrations/DOTween/`](../../Tests/Integrations/DOTween/)：两个独立测试程序集，要求 `DOTWEEN` + `UNITY_INCLUDE_TESTS`。
- [`ApiHelpTests.cs`](../../Tests/EditMode/Action/ApiHelpTests.cs) 保留可信引擎 / BCL 引用筛选回归；全量示例与 core-only 真实 AssemblyBuilder fixture 分别验证，后者不链接 optional wrapper 或供应商 DLL，不代表干净工程导入。
- 提供方 namespace `YLFramework.Editor.Actions`，稳定注册 id `actionkit`；factory 惰性构建只读内容，API 文档不会执行 demo。公共菜单 `YLFramework/帮助`。
- API 筛选所属 namespace 的精确或点分边界，包含内建 Observable 及可用 DOTween 类型，不展示整个共享 Runtime。只有 DOTween 指南保留可选程序集门控。
- XML 来自唯一 [`Runtime/csc.rsp`](../../Runtime/csc.rsp) 的 `Library/YLFramework.Runtime.xml`；optional 使用 `Library/YLFramework.Actions.DOTween.xml`，按真实 Assembly 去重。XML 是构建产物，不默认导出。
- 单文件导出功能及专用菜单 / 测试已退出；分发使用 `YLFramework/Toolkit/导出框架包`，边界见 [文档首页](../README.md#帮助与导出)，不承诺另一工程的独立替代安装。
��承诺另一工程的独立替代安装。
