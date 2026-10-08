# Events（事件系统）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供基于“事件句柄”的轻量事件系统：使用 `EasyEvent` 注册 / 触发 / 注销，支持按枚举、字符串、类型或整数 ID 的全局事件分发，并提供挂载在 GameObject 上监听鼠标 / 物理事件的事件组件。

## 位置

- 运行时：[`Runtime/Foundation/Events/`](../../Runtime/Foundation/Events/)
  - `Abstractions/`：`IUnRegister` / `IUnRegisterList` / `CustomUnRegister`
  - `Core/`：`IEasyEvent` / `EasyEvent`（0~3 参数）/ `EasyEvents` / `OrEvent`
  - `Global/`：四个全局事件系统
  - `Lifecycle/`：生命周期触发器与批量注销扩展
  - `Unity/`：`EventListener` 组件与便捷扩展
  - `Internal/`：内部池与回收助手

## 主要功能

- `EasyEvent`（0~3 个参数）：可池化（`IOfPool`）的基础事件，`Register` 返回 `IUnRegister`。
  - 支持 `RegisterOnce`（只触发一次）、`HasListener` / `ListenerCount`；
  - 触发使用多播委托快照：回调中的注册 / 注销只影响后续 `Trigger`；listener 抛异常会中断后续调用并向上传播。
- `EasyEvents`：按 `Type` 管理一组事件的集合；静态入口维护全局实例，并提供 `Clear` / `Remove<T>`。
- `OrEvent`：多个事件“任一触发”的组合事件；多个 listener 可共存，最后一个 listener 注销或 `Dispose()` 时释放所有 source 订阅。
- `EventListener` 组件：按 `EventType`（鼠标、拖拽、碰撞、触发器）注册回调，并支持 Inspector 中的 UnityEvent。
- 全局事件单例：`EnumEventSystem`、`StringEventSystem`、`TypeEventSystem`、`IntIDEventSystem`，均支持 `Dispose()` 后重建。
- 注销机制：`IUnRegister` / `IUnRegisterList` / `CustomUnRegister`，以及随生命周期自动注销的触发器 `OnDestroyUnRegisterTrigger`、`OnDisableUnRegisterTrigger`、`OnCurSceneUnLoadUnRegisterTrigger`。
- 扩展方法：`EventListenerExtension`、`UnRegisterExtension`、`RegisterListExtension`。

## 注册 / 注销 API

- 基础注册：`easyEvent.Register(action)` → `IUnRegister`；注销 `handle.UnRegister()`（幂等）。
- 一次性注册：`RegisterOnce(action)`；立即回调注册：`RegisterWithACall(action)`。
- 批量注销：`UnRegisterList.AddToUnRegisterList(handle)` + `UnRegisterList.UnRegisterAll()`（快照语义，可重入）。
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

- `Tests/EditMode/`：`EasyEventTests`、`EasyEventReliabilityTests`、`OrEventTests`、`EventSystemTests`、`EventSystemSessionResetTests`、`StringEventSystemTypeSafetyTests`、`LifecycleUnRegisterTests`、`EventListenerTests`、`EventListenerUnityEventTests`。
- `Tests/PlayMode/`：`EventListenerLifecyclePlayModeTests`、`SceneUnloadTriggerPlayModeTests`。
