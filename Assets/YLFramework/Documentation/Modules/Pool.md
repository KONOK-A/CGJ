# Pool（对象池）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

普通引用对象和集合共用一套 `ObjectPool<T>` 缓存、身份、容量与释放算法。领域封装负责自己的 reset、借用代次和归还时机，不维护第二套普通缓存。

普通对象不必实现接口，也不要求公开无参构造。`SafeObjectPool<T>` 将 `IOfPool` 生命周期适配到同一内核；`IPoolType` 只表示对象提供释放入口，不承诺该对象一定会缓存。

## 位置

| 内容 | 路径 | 程序集 |
|---|---|---|
| ObjectPool、IPool、工厂接口、Default/Custom 工厂、List/Dictionary、预热/顺序选项 | [`Runtime/Shared/Pool/`](../../Runtime/Shared/Pool/) | `YLFramework.Foundation.Runtime` |
| Safe、IOfPool/IPoolType、非公开/动态创建工具、自有 GO 池 | [`Runtime/Foundation/Pool/`](../../Runtime/Foundation/Pool/) | `YLFramework.Runtime` |
| LoadViewArgs 私有参数池、Core 临时集合消费者 | [`Runtime/XFMain/`](../../Runtime/XFMain/) | `YLFramework.Core.Runtime` |

公开 namespace 保持 `YLFramework.Core.Pool`，不据目录名推断程序集。Foundation 无 Core/Runtime/XFAB 依赖；Runtime 和 Core 向下引用 Foundation，Core 不引用整体 Runtime。Core 的 XFAB 资源后端仍是独立的过渡依赖。会话状态不作为公共 API，仅向 Runtime/Core 提供窄 friend 访问。

## 主要类型

| 类型 | 职责与主要 API |
|---|---|
| `ObjectPool<T> : IPool<T>, IDisposable`，`T : class` | 显式实例池；`Allocate`、`TryAllocate`、`Recycle`、`Discard`、`Prewarm`、`Clear`、`Dispose`、`CurStoreCount`、`MaxPoolSize`、`SetObjectFactory`、`SetFactoryMethod` |
| `SafeObjectPool<T>`，`T : class, IOfPool, new()` | 类型单例入口，直接组合 ObjectPool；保留上述实用 API、`Instance`、`Init(maxSize, initialCount)` 及 protected 创建/生命周期钩子 |
| `IOfPool` | `OnAllocate`、`OnRecycle`、`IsRecycled`；可观察领域状态不等于池归属 |
| `ListPool<T>` / `DictionaryPool<TKey,TValue>` | 静态 LIFO 集合入口；`Allocate()` / `Allocate(capacity)`、void `Recycle`、`RecycleToPool`、`Clear`、`MaxPoolSize` |
| `IObjectFactory<T>` / Default / Custom | 纯创建策略，分别为接口、`new()` 和委托；与 reset 分离 |
| `NonPublicObjectFactory<T>` / `MultiObjectFactory` | 保留非公开无参构造信息缓存、动态 Type/泛型带参创建和创建后初始化；不维护缓存、不自动重置成员 |
| `GameObjectPool` / `GameObjectPoolData` | Unity 激活/层级/销毁领域缓存；不是普通池或资源租约，独立正确性归必做批次 E（尚未开始） |

## 使用

```csharp
using System.Text;
using YLFramework.Core.Pool;

using var pool = new ObjectPool<StringBuilder>(
    () => new StringBuilder(),
    reset: value => value.Clear(),
    maxPoolSize: 20,
    cacheOrder: PoolCacheOrder.Lifo);
pool.Prewarm(4);
var buffer = pool.Allocate();
try
{
    buffer.Append("current lease");
    // 使用当前借用，不把引用保留到归还之后。
}
finally
{
    pool.Recycle(buffer);
}
```

工厂可以返回接口实现，也可以显式使用 `new ObjectPool<object>(() => MultiObjectFactory.Create(type, constructorArgs))` 创建动态类型。非公开构造可用 `var factory = new NonPublicObjectFactory<MyClass>();`，再传 `factory.Create` 或 `SetObjectFactory(factory)`。这些反射创建工具位于 Runtime；只有公开工厂/普通对象需求的 Core 不需因此引用 Runtime，可直接配置自己的显式委托。IL2CPP/裁剪环境由调用方保留所需构造信息，本专题没有 AOT 自动注册机制。

构造内核时第二个参数是 `onAllocate`，reset 应使用命名参数 `reset:`。涉及借用初始化、资源取得及失败清理时，配置取得/reset/永久丢弃钩子，不把资源销毁寄托于丢引用。

## 生命周期与回收约束

- 工厂必须提供独占的新引用，不能反复返回同一对象或借用其他池对象。工厂返回 null 不缓存；`TryAllocate` 返回 false，但工厂/回调异常仍传播。
- 冷/热取得均先进入转换态再执行取得钩子，成功后才交付。归还只接收本池借出对象，先进入 Returning 再 reset；引用身份不调用用户 Equals/GetHashCode。
- `Recycle` 的 bool **只表示是否入空闲缓存**。满池/关闭池的 false 仍已消费引用，不得重试或继续使用；null、重复、同对象转换期归还拒绝且不重复清理。foreign 抛异常且不产生 reset 副作用。
- Safe 取得前标记 false，回收前标记 true；取得/reset 失败后永久隔离，不回滚成可重试对象。手工修改 `IsRecycled` 不能绕过内核归属或退休状态。
- 取得/reset 错误保留根因，永久清理仍尽力执行；附加清理错误聚合报告。失败隔离只保证不复用污染对象，不等于领域资源必然释放成功。
- `Clear` 和缩容先摘除/退休空闲快照，再逐项尝试永久清理，最后聚合异常，不再 reset。Clear 回调中新归还的缓存保留；`Clear(callback)` 的附加快照回调也逐项尝试。
- `Dispose` 先关闭，禁止取得/预热，不主动清理借出对象。晚归还原池仍清理但不缓存；Safe 新单例拒绝旧池借用，原池 Dispose 不会关闭新单例。
- 普通池不自动反射清零、不自动 Dispose 对象、不管理 Unity fake-null，不提供并发安全或每租用 token。归还后的原始旧别名无法与重新借出的同一引用区分，调用方不得保留。
- 弱键状态不会强制保活失联的借出对象；空闲缓存仍强持对象。热路径有状态查找成本，不承诺零分配或未经测量的性能提升。

## 容量与会话

- 默认空闲容量为 20，正数限制、非正值不限；不是活动配额。调小立即永久丢弃多余空闲对象。普通默认 FIFO，集合/Action/Audio 显式 LIFO；Audio 的 0=no-cache 是领域策略，不是普通池的 0 语义。
- `Prewarm(count, mode)` 按目标空闲数补齐，受容量裁剪，最多尝试缺口次创建；返回本次新增缓存数。`Recycle` 模式执行 reset，`Raw` 不执行取得/reset；Safe Raw 仍正确标记空闲。null 跳过、不循环重试；已经成功缓存的对象不因随后失败而自动回滚。
- 集合归还 Clear 元素，保留 backing capacity；List 保证最小容量，Dictionary 仅冷创建应用请求容量。非法负容量在借用前拒绝，不接纳外部 new 或自定义 comparer 字典。
- 集合与 Core 参数池在新 Play 会话清空闲缓存，不清借出对象；Safe 关闭原会话池并摘单例。实例池由 owner 清理；Action 自己管理 Shutdown 和延迟队列，不受公共池重置顺序替代。
- 无普通池 TTL、自动扫描协程或 Update 宿主。有限容量、显式 Clear/缩容/Dispose 替代固定 scaled-time 淘汰；需要 TTL 的领域按实际时钟/释放需求另行设计。

## 领域边界

- Action 保留 ActionID/Generation 和当前执行栈退出后的延迟归还；失败清理只能延迟丢弃，不重放失败清理。
- Events、Binding、FSM、Resource 经 Safe 复用；Binding readonly 内部事件仅清监听，不独立归池。Audio 私有池永久丢弃负责销毁 Source。
- LoadViewArgs 仅通过 Core 专用入口接管/归还，保留线程、Claim、读取窗口及终态即时清值。Module/Model 不恢复默认隐式池化；普通事件、MappedProperty、TimeItem 不因接口名而自动池化。
- 自有 GO 池保持 `MonoSingletonPath("YLFramework/Pool/GameObjectPool")`，`Allocate(key,parent)` 空池返回 null，需要 `InitOneGoData` 预热。其跨 key、激活重入、初始化数组、fake-null/容量等正确性归必做批次 E（尚未开始），不声称当前已修复或存在完整 GO 专项测试。
- 自有 GO、YL Resource GameObjectLoader、XFAB 的实例缓存/Prefab/Bundle 所有权不同。Core/原游戏继续走 XFAB 原链，本专题不接管资源租约或物理卸载。

## 历史 API 迁移

`ClassObjectPool`、`IndependentObjectPool<T>`、`Pool<T>`、`SimpleObjectPool<T>` 已退出；Core `ClassObjectPoolManager/IReuseable`、集合 `StaticStackPool` 也不再保留。没有隐藏第二套算法或永久兼容壳。

| 旧入口/行为 | 正式承接方式 |
|---|---|
| Simple 委托工厂/reset、构造预热、默认无限 FIFO | ObjectPool 显式 `reset:`、`maxPoolSize: 0`，再 `Prewarm(initialSize)`；默认值不机械等同 |
| Pool/Independent 派生缓存、静态自持缓存 | 组合 ObjectPool，领域生命周期配置为委托；不再提供旧普通池基类身份 |
| Class/Manager 的 Type 创建与可选生命周期 | `ObjectPool<object>` + 显式动态工厂及 onAllocate/reset；无全局 Type registry |
| 反射自动写字段/属性 | 退出，不等价复刻；由类型/领域显式 reset，保留构造默认值与必要资源责任 |
| IOfPool 类型入口 | Safe 保留接口回调、单例、工厂/容量/预热及扩展钩子，直接组合内核 |

本专题明确改变旧基类/类型身份、外部 new 接纳和异常后重试行为。仓库文本资源/反射引用已定向核查，保留工具和 Safe 的 GUID、namespace、程序集不变；这不证明仓库外源码、DLL 或二进制存档兼容，使用方需按上述方式迁移。反射创建只是显式能力，不恢复自动成员 reset。

## 当前状态

**2.1-A～D 已完成技术工作（D 技术验收通过、待 Git 收口）；2.1-E 自有 GameObject Pool 融合与正确性完善为必做批次、尚未开始；2.1 Pool 整体 IN PROGRESS，E 验收后 COMPLETE。**正式设计、A/B/C 历史验证与 D 实际定向结果见 [Pool 专项](../../../../.kilo/plans/xf-yl-2.1-pool.md)。

实际测试入口为 `Tests/Foundation/EditMode/ObjectPoolTests`、`FoundationBoundaryTests`，`Tests/EditMode/PoolContractTests`、`CollectionPoolTests`、`ResourcePoolTests` 及 Action/Events/Binding/Audio 定向契约，Core 的参数/临时集合/身份及代表性 PlayMode 用例。不存在的旧套件名称不作为覆盖依据。

保留限制：Events 旧 token/Once 嵌套归 2.2；Resource ReleaseAllRes 部分失败、嵌套加载器跨会话原池捕获、FSM Exit 后残余引用、Action 配置扩展失败及 UI Tips 未启用显示流程归各领域后续处理。它们不由普通缓存统一自动解决。许可证和来源声明继续保留；本专题不声明框架已最终独立于 XF/XFAB，也未验证 IL2CPP/全平台。
