# Binding（可监听数据）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供可监听变化的数据容器：属性、列表、字典在值或结构变化时触发回调，用于 UI 刷新、逻辑联动等场景；并提供若干落盘到 PlayerPrefs / EditorPrefs 的持久化属性。

## 位置

- 运行时：[`Runtime/Foundation/Binding/`](../../Runtime/Foundation/Binding/)

## 主要功能

- `BindableProperty<T>`：值变化回调。`WithComparer` 现在是实例级比较器，不再污染同类型的其他实例；新增 `TrySetValue`（返回是否变化）、`ForceNotify`（值对象原地修改后强制通知）、`RegisterOnce`（一次性监听）。`Value` 默认 `TrySetValue`，相同值默认不触发；`SetValueWithoutCallback` 只改值不通知；`RegisterWithInitValue` 先回传初始值再注册。
- `BindableList<T>`：`OnAdd` / `OnRemove` / `OnReplace` / `OnMove` / `OnClear` / `OnCountChanged` 保持不变，并新增统一事件 `OnChanged`（`BindableListChange<T>`）；新增 `AddRange`、`RemoveAll`、`TryGet`；`Move` 会先校验索引，非法索引不再破坏列表。
- `BindableDictionary<TKey,TValue>`：在原有事件上新增统一事件 `OnChanged`（`BindableDictionaryChange<TKey,TValue>`）与 `TryAdd`、`AddOrUpdate`、`Remove(key, out value)`、`GetValueOrDefault`；实现 `IReadOnlyDictionary<TKey,TValue>`。
- 单源派生只读属性：`source.Map(selector)` 返回 `MappedProperty<TSource,TTarget>`，初始值立即计算，源变化时重新计算并按 `BindableProperty` 的相同值规则通知；释放责任归返回对象（`Dispose()` / `IUnRegister.UnRegister()` / `RecycleToCache()`，可接入 `UnRegisterWhenGameObjectDestroyed`）。多源组合仍不提供。
- 批量通知：`BindableList` 与 `BindableDictionary` 均提供 `BeginBatch()` / `EndBatch()`。批量期间所有事件被抑制，结束时最多触发一次 `OnCountChanged`（数量确实变化时）和一次 `OnChanged(Reset)`。
- `BindPropertyWithUnityEvent<T>`：属性变化同时驱动 `UnityEvent<T>`，支持初始值构造；`OnRecycle` 清空监听。
- 持久化属性：`PlayerPrefsBoolProperty` / `PlayerPrefsFloatProperty` / `PlayerPrefsIntProperty` / `PlayerPrefsStringProperty`；编辑器侧 `EditorPrefsBoolProperty` / `EditorPrefsIntProperty` / `EditorPrefsFloatProperty` / `EditorPrefsStringProperty`（仅在 `UNITY_EDITOR` 下编译）。统一基类提供 `Key`、`DefaultValue`、`HasKey`、`Delete()`、`Reload()`；`Value` 变化立即写回；`Delete()` / `Reload()` 只在实际值变化时通知（与 `BindableProperty` 一致），回调里再次改值会正常写回。

## 主要类型

- `IReadOnlyBindableProperty<T>` / `IBindableProperty<T>` / `BindableProperty<T>`：属性绑定接口与实现。
- `BindableList<T>` / `BindableDictionary<TKey,TValue>`：集合绑定。
- `BindableCollectionChangeType` / `BindableListChange<T>` / `BindableDictionaryChange<TKey,TValue>`：统一变化类型与数据。
- `MappedProperty<TSource,TTarget>`：单源派生只读属性。
- `PlayerPrefsProperty<T>` / `EditorPrefsProperty<T>`：持久化属性基类。
- `CustomProperty<T>` / `BindPropertyWithUnityEvent<T>`：扩展属性类型。

## 事件语义

- 单次操作按“具体事件 → `OnCountChanged` → `OnChanged`”顺序触发；`Replace` / `Move` 不触发 `OnCountChanged`。
- `Clear` 在空集合上也会触发 `OnClear` / `OnChanged(Clear)`，但不触发 `OnCountChanged`。
- 批量通知结束时 `OnChanged(Reset)` 只表示“结构已变化，需要整体刷新”，不携带具体差异；即使批量内净变化为零也会发一次 Reset（只抑制重复通知，不比对快照）。
- 回调基于 `EasyEvent`（多播委托），一次分发使用固定快照：回调中注册的 listener 收不到本次事件；注销自身 / `UnRegisterAll` 不会取消本次分发中尚未调用的 listener；某个监听抛异常会中断该事件剩余监听的调用（数据修改本身已完成）。
- 回调内再次修改同一属性或集合会同步触发嵌套分发，事件可能交错；这是 `EasyEvent` 的实时语义，Binding 不加重入锁、也不吞掉合法更新。`RegisterOnce` 先注销再回调，回调抛异常也已注销。
- `BindableList.RemoveAll` 对回调内增删做了游标边界钳制，不会因此索引越界。
- 批量建议使用 `using (list.BeginBatchScope())` / `using (dict.BeginBatchScope())`：代码抛异常时也会 `EndBatch` 并补发合并通知。`BeginBatch` / `EndBatch` 保留，少调用一次 `EndBatch` 会保持静默直到补齐，多调用一次抛 `InvalidOperationException`（不会破坏状态）。

## 依赖

- `Events`：变更回调基于 `EasyEvent` / `IUnRegister`；可用 `UnRegisterWhenGameObjectDestroyed` 等扩展绑定生命周期。
- `Pool`：`BindableProperty<T>.Allocate/Recycle` 与事件复用。

## 当前状态

已完成。测试：`Tests/EditMode/BindablePropertyTests.cs`、`BindableListTests.cs`、`BindableDictionaryTests.cs`、`MappedPropertyTests.cs`、`BindablePropertyReentrancyTests.cs`、`BindableCollectionReliabilityTests.cs`、`BindablePropertyPoolLifecycleTests.cs`、`EditorPrefsPropertyTests.cs`、`BindPropertyWithUnityEventTests.cs`。

## 已知限制

- `BindableList(List<T>)` / `BindableDictionary(Dictionary)` 是引用语义：直接包装传入集合，外部修改不会触发事件（`BindableList(IEnumerable<T>)` 构造会复制）。
- 持久化属性不监听外部对 PlayerPrefs / EditorPrefs 的直接修改，需要时调用 `Reload()`；`Delete()` 删除键并把内存值重置为默认值（不写回键）。
- 派生属性仅支持单源 `Map`，不支持 Where/Merge/CombineLatest/Throttle 等响应式操作符；多源组合需自行用 `Register` 组装并管理注销。
- 同一个属性在自身回调里再次赋值会导致嵌套分发，后置 listener 可能“先收到新值、再收到外层旧值”；需要严格顺序时应避免在回调里修改同一属性。
- `Map` 的 selector 在后续源变化时抛异常会向上传播，订阅保留，派生值保持上一次成功结果；恢复后继续更新。
- 测试 Key 统一使用 `YLFramework.Tests.*` 前缀。
