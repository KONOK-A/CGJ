# Events（事件系统）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供实例事件、独立事件总线、精确订阅句柄与轻量组合。共享纯机制位于 `YLFramework.Foundation.Runtime`；池化、历史全局入口和 Unity owner 适配仍由 Runtime/Core 持有，不强制所有通知进入全局上下文。

当前处于 2.2-A 共享基础批次：`OrEvent` 和纯取消类型已使用 Foundation；旧 `EasyEvent` / 四个 Global 系统和 Core `EventManager` 分别等待 B/C 接入，不能将新机制契约套用到尚未迁移的旧算法。

Or 首次连接尚未完成时的同步注册属于该轮临时责任：连接失败时整体回滚，明确断连后重建的轮次不受旧轮回滚影响；契约与验证见专项第 10.5 节。A 实施及专项验证已完成，待用户验收与 Git 收口。

## 位置

- 共享机制：[`Runtime/Shared/Events/`](../../Runtime/Shared/Events/)，归属 `YLFramework.Foundation.Runtime`：`EventChannel`（0~7 参数）、`IEventSource`、`EventKey` / `EventBus`、`OrEvent`、取消辅助及 `SubscriptionScope`。
- Runtime 适配：[`Runtime/Foundation/Events/`](../../Runtime/Foundation/Events/)
  - `Core/`：`IEasyEvent` / `EasyEvent`（0~3 参数）/ `EasyEvents`
  - `Global/`：四个全局事件系统
  - `Lifecycle/`：生命周期触发器与批量注销扩展
  - `Unity/`：`EventListener` 组件与便捷扩展
  - `Internal/`：内部池与回收助手

## 主要功能

### 共享 Channel 与 Bus

- `EventChannel` / `EventChannel<T1, ...>` 使用 `Action` 0~7 参数；`Register` / `RegisterOnce` 返回独立、不池化的精确注册身份。普通 multicast 注册按 target 展开，一次 token 取消自身剩余整组；`UnRegister(delegate)` 删除最后一个相等的普通 target 子序列，Once 使用 token 取消。
- `Trigger` / `Send` 按注册顺序 fail-fast；显式 `TriggerAll` / `SendAll` 才逐监听继续，结束后抛出单个原异常或 `AggregateException`。Once 的 multicast 回调整体作为一个监听，内部仍按委托异常规则中断。
- 发布捕获写时复制数组，普通退订和 `ClearListeners` 不改变已开始的快照，嵌套发布读取新状态。Once 在用户代码前消费，嵌套及旧快照不会重复执行。
- `ResetOwner` 保持 channel 实例但终结旧代次；`Dispose` 永久关闭。两者都截断旧在途快照并撤销旧 token。`IEventSource.Register(Action)` 将无参观察放在同一个强类型序列，不另建先行通道；null 回调不注册。
- `EventBus` 可 `new` 建立独立上下文。`EventKey` 保留完整 Ordinal string、int、枚举类型与完整值、声明 Type 四个独立键域，不使用字符串 hash 路由。枚举构造键需要装箱，可缓存 `EventKey` 用于重复发布；payload 分发不打包 `object[]`、不反射调用。
- 同一 bus/key 的签名由首次非空注册绑定，按泛型声明类型精确校验，不按实参运行时类型或委托 variance 改签名。空回调仍校验已存在签名；缺失键的发布、退订和空注册不建键。末监听离开及 `ClearListeners` 保留签名，`RemoveKey` / `ClearKeys` 终结旧 channel 后才允许重绑。
- 命名消息使用 `Register(key, callback)` / `Send(key, args)`；类型消息使用 `RegisterMessage<T>` / `RegisterMessageOnce<T>` / `SendMessage<T>` / `SendMessageAll<T>` / `UnRegisterMessage<T>`。分开命名避免单参数 string/int/enum 被重载解析为错误的键域；路由仍是同一个 registry。
- `Default` 是显式的全局选择，Core 在 C 接入。`TryGetDefault(out bus)` 和 `ResetDefault()` 不创建实例；Default 随既有 Play Session reset 关闭，独立 `new EventBus()` 不随全局 reset 清理。已关闭实例不得 Register/Send 复活；旧 token、委托退订和 `RemoveKey` 清理保持安全。

```csharp
using var bus = new EventBus();
var subscription = bus.Register<int>("score", value => UpdateScore(value));
bus.Send("score", 10);
subscription.UnRegister();
// 同一键仍绑定 int；显式删除后才允许换签名。
bus.RemoveKey("score");

var changed = new EventChannel<int>();
using var scope = new SubscriptionScope();
scope.Add(changed.Register(value => UpdateScore(value)));
changed.Trigger(20);
changed.ResetOwner(); // 截断旧 owner 的在途快照；实例仍可重新注册。
```

### 共享取消与组合

- `SubscriptionCleanup.ReleaseAll` / `SubscriptionSet.ReleaseAll` 先摘本轮责任，全部尝试后报告，保留失败项和重入新加入项；不在同一轮自动重试失败项。`UnRegisterAllRetainingFailures` 为 `IUnRegisterList` 提供相同必要清理入口。
- `SubscriptionScope` 只管理明确加入的订阅；Dispose 先永久关闭再清理，关闭后 Add 立即取消。失败责任保留供明确处理；句柄自身若是调用前消费的 `SingleUseUnRegister`，失败并不因此变得可恢复。
- `CustomUnRegister` 同实例执行中重入为空操作，成功后释放回调，失败可重试；`SingleUseUnRegister` 与值类型 `CallbackUnRegister` 的原有消费/复制/Strict 契约不变。
- `OrEvent` 首 listener 才连接 source，末 listener 断开并可再注册重连；重复/追加 source 保留。连接失败回滚本次责任，旧 source 快照和迟到 token 不影响重连后的新轮；Dispose 永久终结并截断自身在途通知。source 描述保留到 Dispose，不是弱订阅。

### 尚未迁移的 Runtime 入口

- `EasyEvent`（0~3 个参数）：可池化（`IOfPool`）的基础事件，`Register` 返回 `IUnRegister`。
  - 支持 `RegisterOnce`（只触发一次）、`HasListener` / `ListenerCount`；
  - 触发使用多播委托快照：回调中的注册 / 注销只影响后续 `Trigger`；listener 抛异常会中断后续调用并向上传播。
- `EasyEvents`：按 `Type` 管理一组事件的集合；静态入口维护全局实例，并提供 `Clear` / `Remove<T>`。
- `EventListener` 组件：按 `EventType`（鼠标、拖拽、碰撞、触发器）注册回调，并支持 Inspector 中的 UnityEvent。
- 全局事件单例：`EnumEventSystem`、`StringEventSystem`、`TypeEventSystem`、`IntIDEventSystem`，均支持 `Dispose()` 后重建。
- 注销机制：`IUnRegister` / `IUnRegisterList` / `CustomUnRegister`，以及随生命周期自动注销的触发器 `OnDestroyUnRegisterTrigger`、`OnDisableUnRegisterTrigger`、`OnCurSceneUnLoadUnRegisterTrigger`。
- 扩展方法：`EventListenerExtension`、`UnRegisterExtension`、`RegisterListExtension`。

## 注册 / 注销 API

- 基础注册：`easyEvent.Register(action)` → `IUnRegister`；注销 `handle.UnRegister()`（幂等）。
- 一次性注册：`RegisterOnce(action)`；立即回调注册：`RegisterWithACall(action)`。
- 旧批量注销：`handle.AddToUnRegisterList(owner)` + `owner.UnRegisterAll()`（原快照/fail-fast 工具契约保留）；必要取消使用共享 `owner.UnRegisterAllRetainingFailures()`，Unity 生命周期消费者在 D 切换。
- 生命周期绑定：`handle.UnRegisterWhenGameObjectDestroyed(go)` / `UnRegisterWhenDisabled(go)` / `UnRegisterWhenCurrentSceneUnloaded()`。
- 全局系统：`EnumEventSystem.Instance.Register(key, action)`、`StringEventSystem.Instance.Register(...)`、`TypeEventSystem.Instance.Register<T>(...)`、`IntIDEventSystem.Instance.Register(id, action)`。

## Global EventSystem

| 系统 | Key | 发送 | 备注 |
| --- | --- | --- | --- |
| `EnumEventSystem` | 枚举 | `Send(key, params object[])` | 回调收到 `object[]` |
| `StringEventSystem` | string | `Send(key, ...)` | 同一 key 必须保持相同参数签名；签名冲突时抛出 `InvalidOperationException`（可用 `UnRegister(key)` 更换签名） |
| `TypeEventSystem` | 事件类型 | `Send<T>(T e)` / `Send<T>()` | 每个泛型类型对应一个事件 |
| `IntIDEventSystem` | int（`IConvertible`） | `Send(key, params object[])` | 回调首参为归一化后的 int key |

四个系统均为 `SingletonProperty` 单例：`Dispose()` 会注销全部监听并释放实例，下次访问 `Instance` 创建新实例。

## 生命周期注销

- 三个触发器由 `UnRegisterTriggerBase` 统一托管句柄；触发采用“快照 + 先清空再执行”：
  - 重复触发幂等；回调中新增的句柄留到下一次触发；回调中增删不会破坏遍历。
- `OnDestroyUnRegisterTrigger`：随 GameObject 销毁。
- `OnDisableUnRegisterTrigger`：随 GameObject 禁用。
- `OnCurSceneUnLoadUnRegisterTrigger`：**任意**场景卸载时触发（监听 `SceneManager.sceneUnloaded`，含 additive 场景；不区分 active scene 与句柄注册时所在场景），常驻单例（`DontDestroyOnLoad`）。

## Domain Reload 行为

- Domain Reload disabled 时，`EasyEvents` 全局映射会在每次进入 Play 时通过 `SingletonSessionState` 的会话重置机制清空，避免旧 listener 残留到下一次 Play；会话运行期内注册的全局事件不受影响。
- 四个全局 EventSystem 与 `SafeObjectPool` 同样基于 `SingletonProperty` 的会话重置，Play → Stop → 再 Play 不残留。

## 重要限制

- `EasyEvent` 的 listener 顺序为注册顺序；重复注册同一委托会被调用多次，注销一次只移除一次。
- 事件触发不做异常隔离：一个 listener 抛异常会中断后续 listener。
- 池化 `EasyEvent` 回收及再次分配时都会清空 listener，旧引用不应在回收后继续使用。
- `StringEventSystem` 同一 key 混用不同参数签名时抛出 `InvalidOperationException`（包含 key、已绑定类型与请求类型），不再静默失败；更换签名前先 `UnRegister(key)`。
- `EventListener` 同一 `EventType` 只能使用一种参数类型，混用会输出警告并拒绝注册。
- 生命周期触发器与 `RegisterListExtension.UnRegisterAll` 采用“快照 + 先清空再执行”：若某个句柄抛异常，异常向上传播，本次剩余句柄不再注销（容器/集合已清空，不会重复执行旧句柄）。
- 生命周期触发器（`IUnRegister.cs` 旧文件名问题已修复）现为独立文件，场景 / Prefab 中不再依赖旧脚本文件。
- 原空静态类 `EventManager` 已删除（无任何调用方），统一由四个全局 EventSystem 承担分发职责。

## 测试

- `Tests/Foundation/EditMode/`：`EventChannelContractTests`、`EventBusContractTests`、`EventCancellationAndOrTests`，直接验证共享基础；session reset 的定向调用不等于真实关闭 Domain Reload 两轮 Play 的验证。
- `Tests/EditMode/EventsContractTests.cs`：既有 Runtime 契约与取消/Or 直接回归；`BindingContractTests.cs` 覆盖属性 owner。
- `Tests/PlayMode/EventsLifecyclePlayModeTests.cs`：现存 Unity 生命周期契约，D 按影响迁移和补验。
- 本批实际验证结果及 B～E 剩余边界见 [2.2 专项方案](../../../../.kilo/plans/xf-yl-2.2-events.md)。
