# Pool（对象池）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供类对象、GameObject 与常用集合的复用池，减少频繁 `new` / `Destroy` 带来的 GC 与实例化开销。

统一约定：实现 `IOfPool` 的类型可以被对象池安全管理；实现 `IPoolType` 的类型可指定自己所属的池。

## 位置

- 运行时：[`Runtime/Foundation/Pool/`](../../Runtime/Foundation/Pool/)
  - `Abstractions/`：`IPool<T>` / `IOfPool` / `IPoolType` / `IObjectFactory<T>`
  - `Object/`：`Pool<T>` 与各类对象池实现
  - `Factories/`：对象创建策略
  - `Collections/`：集合静态池（内部共用 `Internal/StaticStackPool<T>`）
  - `GameObject/`：GameObject 池

## 主要类型

- `IPool<T>`：池子接口（`Allocate` / `Recycle`）。
- `IOfPool`：可由对象池管理的对象契约（`OnAllocate` / `OnRecycle` / `IsRecycled`）。
- `Pool<T>`：池子抽象基类，持有 `IObjectFactory<T>`；未设置对象工厂时分配会抛出 `InvalidOperationException`。
- `SafeObjectPool<T>`：默认安全池单例（`T : class, IOfPool, new()`），带回收状态与容量保护。
- `ClassObjectPool`：按运行时类型分桶的普通类对象池（`ISingleton`）。
- `SimpleObjectPool<T>`：无回收状态、无容量上限的轻量池（UIManager 提示对象使用）。
- `GameObjectPool` / `GameObjectPoolData`：GameObject 池与池化数据。
- `ListPool<T>` / `DictionaryPool<TKey, TValue>`：集合静态池，提供 `RecycleToPool` 扩展。
- `IndependentObjectPool<T>`：类型自持静态池的抽象基类。
- `IObjectFactory<T>` / `DefaultObjectFactory<T>` / `CustomObjectFactory<T>` / `NonPublicObjectFactory<T>` / `MultiObjectFactory`：对象创建策略。

## 重要 API

- `SafeObjectPool<T>`：`Instance`、`Init(maxSize, initialCount)`、`Prewarm(count)`、`Allocate()`、`TryAllocate(out T)`、`Recycle(T)`、`MaxPoolSize`、`Dispose()`。
- 集合池：`Allocate()`、`Allocate(capacity)`（List 保证返回容量不小于请求）、`Recycle(obj)`、`Clear()`。
- `GameObjectPool`：`InitOneGoData(...)`、`Allocate(keyName, parent)`（池空返回 null，不实例化）、`Recycle(go)` / `Recycle(keyName, go)`、`Clear(keyName)`、`ClearAll()`。

## 生命周期与回收约束

- `SafeObjectPool<T>` 分配时先清除 `IsRecycled` 再调用 `OnAllocate`；回收开始时即标记 `IsRecycled` 再调用 `OnRecycle`，因此回调中的重入回收会被拒绝（不会无限递归），回调抛异常时回滚标记并传播。
- 空对象与重复回收会被拒绝；缓存已满时对象不入池，但仍执行一次 `OnRecycle` 并保持已回收标记（调用方应视为回收失败，无需再次清理）。
- `MaxPoolSize` 是空闲缓存上限，不限制已分配对象数量；小于等于 0 表示不限制。调小会立即丢弃多余缓存。
- `Dispose()` 后再次访问 `Instance` 会创建新池，旧缓存对象随旧实例丢弃；`EasyEvent` / `BindableProperty` 等消费者会在池实例缺失时自动重新应用初始化配置（不依赖静态标志跨会话残留）。
- `GameObjectPoolData` 拒绝同一 GameObject 重复入池；池数据销毁（Clear / ClearAll / 单例销毁）会销毁池节点与缓存对象，避免对象残留场景。
- `Pool<T>.Clear` 先清空缓存再执行回调，回调中操作池不会破坏遍历；回调异常会中断剩余回调但池已清空，状态保持一致。
- 集合池在进入新 Play 会话时自动清空缓存（`SingletonSessionState`），可用 `Clear()` 手动释放。

## GameObjectPool 路径

- `MonoSingletonPath("YLFramework/Pool/GameObjectPool")`（DontDestroyOnLoad，由兼容测试锁定，不要修改）。

## 已知限制

- `ClassObjectPool` 用反射重置实例字段与可写属性（不含基类私有字段），且与 `SafeObjectPool` 相互独立，同一类型不要同时交给两个池管理。
- 集合池的重复回收检测是栈枚举（O(n)）；`SimpleObjectPool` 不检测重复回收、无容量上限。
- `GameObjectPool` 不会自动实例化预制体：`Allocate` 池空返回 null，需要先用 `InitOneGoData` 预热或自行回收对象。
- Unity 的 `Destroy` 延迟到帧末执行：`Clear` 后同帧对旧对象再回收不可靠，分配缓存时会跳过已销毁对象（对象不会被重新分配，但该帧内无法复用）。

## 当前状态

已完成。测试：`Tests/EditMode/`（SafeObjectPoolTests、PoolExtraTests、ClassObjectPoolTests、IndependentObjectPoolTests、PoolFactoryTests）与 `Tests/PlayMode/`（GameObjectPoolTests、GameObjectPoolDataTests）。

## 备注

- `EasyEvent`、`FSM`、`ResKey`、`ResLoader` 等框架类型都通过 `IOfPool` / `IPoolType` 复用对象池。
