# Table（数据表）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

轻量通用数据表基础设施，为 Resource 等模块提供最简单的数据表能力：

- `Table<TDataItem>`：数据表抽象基类，统一 `Add` / `Remove` / `Clear` / `Dispose` / 枚举入口，具体存储与索引由子类钩子维护；
- `TableIndex<TKeyType, TDataItem>`：按键查询的索引，同一键支持多个数据项。

定位边界：不做 ORM / Database / DataFrame / SQL / LINQ Provider / 响应式表 / ECS 存储。集合过滤、投影、排序等交给 LINQ。

## 位置

- 运行时：[`Runtime/Tools/Table/`](../../Runtime/Tools/Table/)
- `Table.cs`：数据表基类；`TableIndex.cs`：键索引。
- namespace：`YLFramework.Utilities.Table`；程序集：`YLFramework.Runtime`。
- 按需 `new` 子类或 `TableIndex`，由调用方负责 `Clear` / `Dispose`；无需 Module、Manager、组件挂载或公共 Root。

## Table&lt;TDataItem&gt;

| 成员 | 语义 |
|---|---|
| `Add(item)` | 转发到 `OnAdd`；基类不校验 null、不去重，是否允许重复由子类存储决定 |
| `Remove(item)` | 转发到 `OnRemove`；不存在的项如何处理由子类决定 |
| `Clear()` | 转发到 `OnClear`；只清数据，实例可以继续使用 |
| `Dispose()` | 转发到 `OnDispose`；基类不跟踪释放状态，每次调用都会触发钩子（不做幂等保护），子类钩子应自行幂等 |
| `Update()` | 保留的 no-op 扩展占位（历史空实现），无调用方，仅为 public API 兼容保留，调用无副作用 |
| `Count` | 默认按枚举计数（O(n)），virtual，子类可用自有存储覆写 |
| `GetEnumerator()` | 枚举入口，枚举内容由子类存储决定 |

Ownership：

- `Table` 只维护子类自身的数据结构（存储 / 索引），**从不释放 `TDataItem`**；数据项生命周期归创建者。
- `Clear` = 清空、可复用；`Dispose` = 由子类决定销毁语义，且可重复触发。
- Dispose 后是否还能继续使用由子类决定（例如 `TableIndex` 释放后所有操作抛 `ObjectDisposedException`，`ResTable` 释放后查询同样抛 `ObjectDisposedException`）。
- 回调异常：`OnAdd`/`OnRemove`/`OnClear`/`OnDispose` 抛出的异常原样传播给调用方；基类不捕获、不回滚，异常前已产生的子类副作用保留，写入顺序由子类负责。

## TableIndex&lt;TKeyType, TDataItem&gt;

- 同 key 多值（multi-set）：同一键可关联多个数据项，**允许重复项**（同一个 item 重复 Add 会出现两次，不去重、不抛异常），保持添加顺序。
- 键由构造函数传入的选择器在 `Add`/`Remove` 时实时计算，索引不缓存键。
- **mutable key 契约**：索引期间用于生成键的值必须保持稳定；若 item 的键属性发生变化，必须先按原状态 `Remove`，再修改并重新 `Add`。否则 `Remove` 按新键查找而旧索引项残留。
- `Add(item)`：按键追加；键不存在时从 `ListPool` 租用列表。
- `Remove(item)`：返回 `bool`；键或项不存在返回 false、不抛异常；移除后列表为空会立即删除该键并把列表归还 `ListPool`。
- `Get(key)`：键不存在返回空集合（不返回 null）；命中时返回**内部实时列表（借用引用，无拷贝）**。
- `Clear()`：归还全部列表后清空字典；可重复调用、不重复回收；清空后可继续使用。
- `Dispose()`：幂等；归还全部列表与字典；释放后 `Add`/`Remove`/`Get`/`Clear`/`Dictionary` 一律抛 `ObjectDisposedException`。
- null 键（引用类型 null / `Nullable` 无值）不受支持：在字典操作前抛出带明确消息的 `ArgumentNullException`；空字符串、值类型 default 键合法。
- keySelector 重入：选择器是用户委托。选择器内 `Add`/`Remove`/`Clear` 会先按调用顺序完成（外层操作随后继续）；选择器内 `Dispose` 索引会在返回后 fail-fast 抛 `ObjectDisposedException`，不会出现 NRE。

### Get / Dictionary 借用契约（旧引用边界）

- `Get(key)` 命中时返回内部 `List<TDataItem>` 本身；`Dictionary` 属性返回内部字典本身。两者都是**借用引用**，不是快照。
- 不要修改返回的集合；修改会绕过 Pool 回收并破坏“key 必有非空 List”的不变量。
- 旧引用生命周期：列表在下一次结构修改 / `Clear` / `Dispose` 时失效（`Clear`/`Dispose` 会回收并清空列表）；字典在 `Clear` 后仍为同一对象但内容被清空，在 `Dispose` 后已归还 `DictionaryPool` 并失效。
- 不要长期缓存这些引用；需要快照时调用方自行拷贝（如 `.ToArray()`）。

## 依赖

- Foundation 的 `Pool`：`ListPool<T>` 与 `DictionaryPool<TKey, TValue>`（Clear / Dispose 时归还），沿用既有 `YLFramework.Core.Pool` namespace；这不构成对 `YLFramework.Core.Runtime` 的依赖。
- Table 提供数据项与多值索引，不替代 Foundation 的 `SerializedDictionary` 序列化容器，也不接管 Resource 的资源加载、Retain / Release 或缓存策略。

## Resource 消费者

- `ResTable : Table<IRes>`（`Runtime/Resource/Framework/ResTable.cs`）：`OnAdd`/`OnRemove`/`OnClear` 转发到 `TableIndex<string, IRes>`（按小写 AssetName 索引），枚举通过 `Dictionary.SelectMany`。
- `AssetDataTable : Table<AssetData>`（`.../AssetBundleSupport/ConfigFile/AssetDataTable.cs`）：公开 `NameIndex` 属性，`OnDispose` 调用 `NameIndex.Dispose()`；`ResData.Reset()` 是真实 Dispose 调用点。
- `ResTable.OnDispose()` 释放其独占的 `_nameIndex`；`ServiceManager` 负责资源登记、移除与资源生命周期，Table 不取得资源所有权。现有 Resource 消费者保持直接调用，无需额外适配层。

## Update() 最终处理

保留 no-op，不删除（避免 public API 兼容风险），不虚拟化（避免外部同名成员隐藏警告）。当前无任何调用方；若未来出现真实刷新需求，再以显式方法或 virtual 钩子单独设计。

## 测试

- 当前仓库的 `Tests/EditMode/CollectionPoolTests.cs` 中四个 `Table*` 用例覆盖选择器校验、Clear / Dispose 归还、移除空桶及插入失败归还；这不是 Table 全部契约或 Resource 消费者的完整覆盖。
- `Tests/EditMode/Audio/AudioSourceIndependenceTests.cs` 的真实源码闭包包含 Table / ResTable；它验证编译边界，不替代 Table 行为测试。
- 文档旧列出的 `TableTests.cs`、`TableIndexTests.cs`、`TableResourceConsumerTests.cs` 当前不在仓库，不作为可运行入口或本批验证证据。阶段 5.3.5 不重跑融合前成熟模块的全面验证。

## 已知限制

- `Table` 不自动维护索引，新增子类必须在 4 个钩子中同步维护，遗漏会导致索引不一致。
- 基类不提供全局重入保护/回滚：Hook 内再次操作、枚举期间修改按底层存储语义表现；异常前副作用保留，由子类负责写入顺序。
- `TableIndex` 不缓存键：数据项键属性变更后，`Remove` 无法定位旧索引项，会留下陈旧数据（契约见上）。
- `Get`/`Dictionary` 返回内部活集合的借用引用，不是快照；修改或长期缓存会破坏不变量，调用方需自行拷贝。
- 非线程安全，不支持多线程 / Job 访问。
