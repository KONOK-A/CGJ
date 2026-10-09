# XF × YL 阶段 2.1：Pool 深度融合研究与技术设计

**状态：2.1 Pool 专项设计目标已批准，审阅补充已确认；功能实施尚未开始。**

本文是唯一 Pool 专项方案，承接 [阶段 2 总体规划](xf-yl-2-runtime-support.md) 与 [长期融合蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)。用户已批准可靠身份、异常安全及统一公共内核等设计目标，并确认第 10 节两项审阅补充；具体实施细节保留合理调整空间。研究阶段未修改产品、测试、资源、程序集、原 Package 或游戏，也未执行 Unity 或构建。本次授权仅为设计文档 Git 收口与实施分支准备，不自动授权功能实施。

## 1. 基线、范围与结论

- 研究基线：2026-10-09，`integration/xf-yl`，最新提交 `090a6c9`，开始时工作区干净。Unity 为 `2022.3.62f3c1`。manifest、锁文件及实际安装源码已定向核对：XFGameFramework `1.18.8` / `2182708aecc596f30739fa8634fe2b9b6946ddd7`，XFABManager `1.24.0` / `c35dc4058ecd831409199b43cb328024202d4557`。
- 证据来自当前源码、实际 asmdef 和现存测试文件。以下“确认缺陷”指可靠源码控制流，不代表本轮进行了运行复现；性能只说明可核实成本，不报告未经测量的收益。
- 主范围为普通 C# 对象、List/Dictionary 缓存、直接消费者的必要接入及重复实现退出。Module/Model 不恢复默认隐式池化；Model 允许未来根据实际需求另行研究显式、可选的池化，不作为本专题自动新增任务。Action 的执行租约和延迟回收、LoadViewArgs 的接管协议不改变。
- 推荐最终只有一套普通对象缓存内核，以 YL 的显式工厂、容量、预热和领域 reset 为主要技术基础，吸收 XF 的简洁取得/归还体验，不沿用 XF 的时间表和扫描宿主，也不沿用 YL 的反射自动重置。
- 公共内核及集合池进入现有 `YLFramework.Foundation.Runtime`；Safe 池的类型入口和领域接口留在 `YLFramework.Runtime`，Core 使用中立内核，不引用整个 Runtime。只迁移最小真实依赖闭包，不重组 Foundation 全集。
- GO 池不是必做项。建议登记并延期其独立正确性修复；允许审阅后选择完整的可选批次，但不替换 XFAB 后端、不设计完整资源租约或 Bundle 卸载系统。

路径简称：`Y` = `Assets/YLFramework/Runtime/`，`C` = `Y/XFMain/`，`P` = `Y/Foundation/Pool/`，`T` = `Assets/YLFramework/Tests/`，`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，`AB` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`。行号对应研究基线；实施时重新核对受影响位置。

## 2. 双方实现究竟解决什么问题

### 2.1 XF 与已承接 Core

`C/Manager/ClassObjectPoolManager.cs` 与原 `XF/Runtime/Manager/ClassObjectPoolManager.cs` 的核心机制相同：

- `Dictionary<Type,List<object>>` 保存空闲对象；泛型 `Load<T>` 要求 `class,new()`，动态 `Load(Type)` 用 Activator。无须实现接口也可缓存对象。
- `IReuseable.OnLoaded/OnUnloaded` 是可选生命周期钩子，不自动清字段或集合。
- 取尾对象后通过 `List.Remove(obj)` 删除，另以 `Dictionary<object,float>` 维护回收时间和重复归还判定。
- 没有缓存数量上限、预热、永久丢弃回调或正式关闭入口。每 60 秒扫描，空闲 600 秒丢引用；判时及等待都使用 scaled time，`timeScale=0` 不提供正常的超时回收。
- 首次使用启动 Shared CoroutineStarter；Editor 进入/退出 Play 时置空实例。协程是静态 `Check()`，通过当前 Instance 访问数据，不绑定原实例，也未保存停止句柄。多次会话置空后的宿主重建/重复 checker 是生命周期风险，不是本轮已证明的运行故障。
- 缓存丢引用不是 Dispose，也不负责外部 Unity 对象或资源引用释放。

真实新 Core 的普通对象消费者目前只有 LoadViewArgs；其他直接调用都在复用临时 List。动态 `Load(Type)` 在新 Core 的生产调用中没有找到，不能以原 XF Module/Model 的历史调用证明新 Core 仍需要它。

### 2.2 YL 对象池、工厂和集合池

| 实现 | 真实职责与契约 | 评价 |
|---|---|---|
| `Pool<T>` | 无泛型引用类型约束；工厂 + FIFO/LIFO 缓存，默认容量 20，非正值不限；Allocate 不调用生命周期钩子；PrepareRecycle 由派生类决定 | 已有清晰缓存骨架，可吸收机制，不必保留开放继承层级 |
| `SimpleObjectPool<T>` | 引用类型、委托工厂/回收 reset；默认 FIFO、无限缓存；满池也 reset；不检测重复归还，可接收外部构造对象 | Audio/UI 的私有缓存有价值，但“不检查”是当前明文契约，不能称为已实现安全池 |
| `SafeObjectPool<T>` | `class,IOfPool,new()`；SingletonProperty 按封闭类型持有；冷/热均清标记再 OnAllocate；回收先置标记再 OnRecycle；满池仍清理 | 已防止同对象回收重入；IsRecycled 不是池身份或租用代次；失败时标记回滚不是对象状态回滚 |
| `ClassObjectPool` | 静态 Type→List<IOfPool>；反射创建；每次取得先重置字段及可写属性，再 OnAllocate | 与 Safe 并行且 reset 不可靠；静态 Allocate/Recycle 并不访问 Instance，不能假定单例初始化/会话 reset 一定触发 |
| `IndependentObjectPool<T>` | 自持静态 FIFO，实例标记、OnRecycle，实例方法 Allocate/Clear，无容量限制 | 同职责重复；当前没有查到实际派生消费者，不值得维持第二套算法 |
| `ListPool<T>` | 静态 LIFO；归还 Clear 保留 backing capacity；Allocate(capacity) 保证 List 容量；重复归还抛错 | 应保留清元素和容量语义，并复用公共内核 |
| `DictionaryPool<K,V>` | 静态 LIFO；归还 Clear；capacity 只用于冷创建；冷创建用默认 comparer，当前却可归还外部自定义 comparer 字典 | 不承诺热租容量；存在 comparer 污染桶的入口；当前静态栈无上限 |
| `StaticStackPool<T>` | Stack.Contains 线性判重；新 Play 会话注册到 SingletonSessionState 清缓存 | 会话重置有真实用途；不是线程安全池 |
| 工厂 | Default/Custom 分别 new/委托；NonPublic 缓存 ConstructorInfo；Multi 反射构造及可选初始化 | 创建与 reset 应分开；已有构造异常解包和缓存构造信息可保留，不建设 DI/工厂注册管理器 |
| GO 两文件 | 字符串桶、场景层级、SetActive、Destroy 与 fake-null 处理；Data 本身借 Safe 复用 | 不是普通 C# 对象生命周期，见第 7 节 |

重要依据：`Pool.cs:34–59,73–98,121–173,182–203`，`SafeObjectPool.cs:23–40,57–78,99–118`，`ClassObjectPool.cs:18–33,47–95,102–152`，`IndependentObjectPool.cs:23–49,62–75`，`Collections/ListPool.cs:25–59`、`DictionaryPool.cs:28–54`，`Internal/StaticStackPool.cs:16–19,40–59`，`Factories/DefaultObjectFactory.cs:16–26`、`NonPublicObjectFactory.cs:18–28`、`Internal/NonPublicConstructorFinder.cs:16–50`。

没有查到 YL 现有 HashSetPool、QueuePool、StackPool 或线程安全池消费者；本专题不为“集合齐全”新增这些产品 API。IPoolType 仅表示对象提供释放入口，不承诺进入任何缓存。

### 2.3 收敛、互补与应保留的差异

- 高度重叠：普通引用对象缓存、同类型复用、List/Dictionary 暂存。应统一工厂、缓存、容量、清理、异常和身份实现，而非只把同名 API 互相转发。
- 值得吸收：YL 的私有池、显式 reset、FIFO/LIFO、目标数量预热、满池仍清理、先摘缓存再 Clear 回调；XF 的简单取得/归还使用方式。
- 不值得照搬：XF 全局 Type 表 + 回收时间表 + 协程扫描；YL 反射写所有成员；用公开 IsRecycled 作为唯一所有权依据。
- 应保留：ActionPool、EasyEventPool、Audio 私有 Voice 池、LoadViewArgs 私有参数池等领域入口。它们决定何时真正可归还、reset 清什么、是否允许继续回调，不是第二套公共缓存算法。
- 不强制池化：普通实例事件、MappedProperty、TimeItem、Module、Model、FSM 的状态对象。减少“new”不是无条件目标，公开句柄不复用有真实身份收益。

## 3. 缺陷、安全边界与优化机会

### 3.1 确认问题与推荐处理

| 证据及根因 | 分类/影响 | 2.1 处理建议 |
|---|---|---|
| Core 泛型 Load 对旧 `obj` 而非新 `t` 调钩子，`ClassObjectPoolManager.cs:143–149`；Type 版本正确使用 t，172–173 | 确认缺陷：冷/热行为不同 | 新内核取得钩子在冷/热路径一致；原 manager 随迁移退出，不修一份再留两份 |
| Core Unload 先写时间表后回调，200–209 | 确认异常状态缺陷：抛错后不在缓存却被记为已回收，并保留强引用 | 回调前进入转换态，成功后才发布空闲；失败对象不入池 |
| Core `List.Remove(obj)`、默认相等时间表，110–120,194–200 | 确认身份混淆：值相等实例会误判；可变 hash 可破坏查找；尾部 Remove 还存在 O(n) | 使用引用身份，不调用用户 Equals/GetHashCode；移除已知尾位置或直接 Stack.Pop |
| YL ClassObjectPool 先 OnRecycle 后标记，109–110 | 确认重入保护缺口；同对象回收可递归/重复入缓存 | 淘汰该重复实现；不构造无终止递归实验 |
| 反射 reset 无差别写字段、setter/indexer，ClassObjectPool.cs:128–145 | 确认算法不适合普遍 reset：可破坏构造默认值、两次写 backing field/属性、索引器参数不足、有副作用 setter；也不能覆盖全部基类私有状态 | 不保留自动反射 reset；由类型或领域显式清理 |
| Safe OnRecycle 失败只恢复 IsRecycled=false，113–118 | 已有异常契约，但字段/订阅/资源可能已部分清理；不能证明可安全重试 | 推荐失败隔离并丢弃，不自动恢复为可回收对象；属于需确认的行为升级 |
| Safe OnAllocate 前已标记可用，68–78，没有转换态 | 潜在租约风险：钩子可归还当前对象，外层仍把它交给调用者；失败已从缓存取出但无永久丢弃钩子 | 内部 Allocating 状态拒绝同对象归还，成功后才交付 |
| Pool 先决定 willCache 后调用用户 reset，再 Cache，129–134 | 确认容量不变量缺口：原先有空位，回调归还另一对象占满后，外层仍入池；原先满但回调取走对象的保守丢弃是现有测试契约 | 保留“初判满则本次不缓存”；初判可缓存也必须在提交前复核实际上限 |
| Raw 预热可缓存 null，Pool.cs:162–170 | 当前行为且被测试锁定，不是未知问题；null 消耗容量却不能提供复用 | 推荐 null 从不缓存；有限创建尝试不循环重试，TryAllocate 可表达无对象 |
| Pool 缩容无永久丢弃回调；Clear 回调遇错中断，73–81,199–203 | 明确限制；持有 Unity 对象时不能靠丢引用释放 | 统一永久丢弃路径；批量必要清理逐项尝试，最后聚合报告 |
| Simple 重复归还、Safe 标记可写且无 owner、集合只检测当前空闲 | 当前限制；不是旧别名、跨池或代次保证 | 默认内核使用池拥有的引用身份状态，明确禁止归还 foreign 对象和租用后旧别名 |
| Action 在公共池之外先清理，再延迟归还；`Y/Action/Core/IAction.cs:163–176` 的 finally 即使 OnDeinit 失败也会安排普通归还 | 确认失败回收路径；清理是否完成取决于派生类，公共 reset 钩子看不到此前异常，不能自动隔离污染动作 | B 中让领域回收项表达“自身已清净可缓存”或“只能延迟丢弃”；不改变等待当前栈退出的规则 |
| EventManager.RemoveAllEvent、Panel.GetUIOrderMax、UpdateAlwaysInFront 归还不统一清元素 | 确认引用保持成本：Core 普通池不 Clear，Panel 列表可保留组件和 Session；异常路径也可能漏还 | 改用归还自动 Clear 的 ListPool，并把实际借用范围放入 finally |

新内核改变契约不等于自动修复消费者全部异常安全。Resource 的释放失败、FSM Exit 失败、Events 旧 token 仍由领域承担，见第 5 节。

### 3.2 正确性与性能分开判断

- **可核实收益**：移除 Core 时间表和 checker，消除普通池触发的宿主/等待对象；直接 O(1) 取尾；归还立即清引用；消除每次反射遍历和 setter 调用；容量边界可限制空闲实例数量。
- **可靠性有成本**：所有权跟踪需要状态元数据和查找。推荐弱键表，首次创建对象会额外分配状态，热路径也多一次查询；不能称为零分配或无成本优化。
- **有条件测量**：YL 集合栈判重 O(n) 与弱键状态查找成本、FIFO/LIFO 对实际峰值缓存和局部性的影响、保留大集合 backing buffer 的内存占用、Action 高周转路径的状态元数据成本。
- **不作无依据改造**：不增加全局池管理器、每帧自动 Trim、容量分桶、自适应扩容、并发池、通用租约 token、Action 回收队列去装箱或大规模集合替换。后几项不是共享 Pool 所必需，收益尚无测量证据。
- XF 空闲超时是可选功能而非当然缺陷，但在新 Core 已查实用途中没有必须保留 10 分钟自动淘汰的要求。有限空闲容量、显式 Clear/Trim 与已有会话 reset 足够作为首版；需要 TTL 的领域将来另提明确时钟及释放需求。

## 4. 推荐最终架构与契约

### 4.1 职责与合理备选

| 选择 | 优点 | 问题 | 推荐 |
|---|---|---|---|
| 保留 XF manager，YL 全部接到它 | 使用方式简单，Type 调用保留 | 缺工厂/容量/丢弃钩子，仍需大量重建；全局类型桶不适合 Audio 多实例所有权 | 不采用 |
| 原样搬整个 YL Pool + Singleton 到 Shared | 尽量保持旧类型 | 会带入 Mono/Root/Extensions；反射 reset 和旧身份契约仍存在；没有理由搬整个 Foundation | 不采用 |
| 在 Shared 稳定一套泛型内核，领域薄封装向下依赖 | 私有所有权明确；Core 可独立接入；只维护一套缓存算法 | 要显式迁移行为与测试；严谨身份检查有少量成本 | **采用** |
| 只用 Stack + 空闲 HashSet | 成本较低，可判重复归还 | 无法识别已借出对象的所属池，跨池归还仍可能通过 | 不作为默认可靠契约 |
| 用强引用表记录所有借出对象 | 直观，容易查询 | 调用者遗失引用后被池永久保活，Binding 内部事件等长期借用尤其不合适 | 不采用 |
| 池内弱键状态表；对象类型无侵入要求 | 引用身份，能拒绝非本池归还；不会因追踪把失联借出对象保活 | 状态分配/查找须定向测量，目标 Unity 环境需要实际验证 | **默认推荐** |

最终候选 API 名称为 `ObjectPool<T> : IPool<T>`，`T : class`，以委托配置创建、取得、回收 reset 和永久丢弃。它取代当前 `Pool<T>` 开放继承缓存及 Simple 的重复入口；Safe 是领域协议适配，不维护自己的 Queue/Stack。不要求所有对象实现 IOfPool，不要求 new()，支持接口类型和 private 构造的显式工厂。

**已批准的是设计目标，不是具体实现锁定。**`ConditionalWeakTable`、上述候选 API、状态元数据及内部结构均为当前推荐；实施时可依据真实依赖、正确性验证、性能测量和维护成本合理调整，选择更简单且能满足相同目标的方案，不必机械照搬本草案的类名、接口或数据结构。调整不得弱化可靠身份、异常安全、统一公共内核及领域所有权边界；重大行为/兼容性变化或跨专题扩张仍需说明并取得确认。

为控制迁移范围，2.1 中立类型继续使用当前公开子命名空间 `YLFramework.Core.Pool`；真实归属由 asmdef 决定。不给类型移动附加全域 namespace 重命名，最终命名收口另按长期蓝图执行。以下命名是设计推荐，实施不得再同时保留两套正式对象池产品。

### 4.2 普通对象取得、归还和释放

建议主要入口：`Allocate()`、`TryAllocate(out T)`、`Recycle(T)`、`Discard(T)`、`Prewarm(count,mode)`、`Clear()`、`Dispose()`，以及 `CurStoreCount`、`MaxPoolSize`、已查实需要的工厂替换入口。保留 IPool 的 Allocate/Recycle 形状，不引入第二套 Rent/Return 同义 API。

1. **创建**：空闲缓存为空才调用工厂。工厂必须提供独占的新对象，不能从别的池借对象，也不能把同一引用当作多次创建结果。当前池已登记的工厂结果应拒绝。工厂抛错直接传播；返回 null 可由 TryAllocate 返回 false，null 不缓存。
2. **取得**：冷/热路径统一进入 `Allocating`，执行可选取得钩子，成功后进入 `Leased` 才交付。取得钩子不能归还当前对象。普通对象没有默认反射清零；Reset 由配置负责。
3. **归还**：只接收本池借出的 `Leased` 对象，先进入 `Returning`，再 reset。成功后按容量提交为 `Idle`，或进入永久 `Retired` 并执行丢弃回调。调用方无论是否入缓存，都不得继续使用已成功消费的引用。
4. **Discard**：显式结束本池借用但不缓存，仍执行一次领域 reset 和一次丢弃回调。用于 Audio 禁缓存/坏 Source、消费者局部失败等真实场景，不能拿来销毁其他池对象。
5. **异常**：取得/reset 失败对象绝不发布到空闲缓存；内部进入 Retired。可配置的丢弃清理仍尽力执行，保留最初异常；追加清理失败聚合报告。不能因为池恢复了结构不变量就宣称外部资源已释放干净。
6. **Clear/缩容**：只影响空闲对象，先从缓存摘除目标并标记 Retired，再逐项执行丢弃清理；不再次 reset，不触碰借出对象。Clear 回调中新归还对象保留，与现存快照清理行为一致。缩容时以最新容量约束嵌套归还。
7. **Dispose**：先进入关闭状态、禁止新取得/预热，再清空空闲对象并清理。不会追踪并强制 reset 借出对象；若原持有者仍保有旧池，可归还到该已关闭池，执行清理后丢弃，不复活缓存。静态 Safe 入口不得把旧借用偷偷转接到新池。
8. **线程**：普通池不保证并发访问安全，Unity 消费者继续主线程使用。弱键表自身的并发特性不等于整个池线程安全；不为本轮增加锁/后台池。

`Recycle` 的 bool 保留“是否成功进入空闲缓存”，不是“是否完成清理/是否应重试”。null 和同对象重复/转换期归还返回 false；foreign 归还抛明确异常且不调用 reset；满池/关闭池/主动 Discard 完成清理后不缓存。重复归还不得再触发丢弃回调。XML 契约必须明确 false 不等于引用仍有效。

工厂替换只影响后续冷创建，不更换已借出对象的 reset/丢弃责任。Safe.Dispose 清理其内部实例池后，仅在 SingletonProperty 当前实例仍为自己时摘除静态入口；旧 Safe 引用不能释放已经建立的新 Safe 单例。取得钩子中 Dispose 后，外层必须丢弃本次对象并报告池关闭，不能继续交付。

### 4.3 身份、回调重入和旧引用

- 推荐每个实例池一个 `ConditionalWeakTable<T,Entry>`，Entry 仅维护内部状态，不强持有对象键；空闲容器持有空闲对象，借出追踪不强保活。不需要全局对象→池管理器、对象基类或遍历所有借出对象。
- 所有判断按实际实例身份，不调用用户相等性。跨池 Return 因目标池没有该对象的租用记录而拒绝；不支持 Recycle 自动接纳外部 new 对象，也不提供没有真实消费者的 Adopt API。
- **工厂独占是前置条件**：每池弱表不能发现恶意工厂把一个外部同引用同时交给两个池；本方案不宣称检测任意跨池工厂造假。若未来确需转移对象，必须另立明确接管协议，而非增加默认全局 registry。
- 同一对象在 Allocating/Returning 内不可再次归还；其他对象的取得/归还允许嵌套。用户钩子前后重新检查池关闭状态及容量，外层不得覆盖回调内已发生的 Dispose 或提交到已关闭缓存。
- 容量初判已满时保留现有保守语义：reset 中即使腾出空位也不缓存本次对象。初判有空位时，提交前再次复核；嵌套 reset 不能突破上限。
- Clear 按已摘快照清理；Dispose 期间新 Allocate 抛错，重复 Dispose 无操作。清理后出现的正常新缓存不被旧 Clear 随后误删。
- **原始对象引用不等于代次句柄**：A 归还后同一实例被 B 取得，A 的旧引用与 B 完全相同，弱表/标记不能分辨是谁在操作。禁止保存归还后的引用；普通池不创建每租用 token，不承诺阻止旧别名归还当前租用。
- 需要异步/延迟回调身份的 Action、Audio、Core View 继续使用自己的 generation/lease/session。LoadViewArgs 继续明确禁止保留参数引用，而非伪装成通用池解决了旧别名。

### 4.4 Reset、IOfPool 与领域对象

- IOfPool 和 IPoolType 留作现有 Runtime 领域协议，不成为中立内核约束。SafeObjectPool 的接口回调只适配到 ObjectPool 的钩子。
- Safe 的 OnAllocate 在冷/热路径都执行；归还前仍使 IsRecycled=true，供领域观察。真正所有权由内核状态决定，手动写 false 不能重新接纳 Retired/Idle 对象；失败隔离时不恢复为“可安全再次回收”。
- 不给每个对象规定一个新 Reset 接口；普通类用显式委托，Safe 类沿用已有接口，Action 等沿用自己的清理和执行标记。
- Reset 清理租用数据与订阅；永久丢弃负责长期附属资源。两者不是自动 Dispose，池不能猜哪些对象或资产可销毁。回收时持有 Unity 对象的类，必须通过领域设置丢弃回调或沿用明确的释放步骤。
- 通用内核只判断 managed null/引用身份，不替代 Unity fake-null。UITipsItem 等 Unity 对象若通过委托缓存，领域仍须核验存活及清理 GameObject；不能据普通池就声称具有通用 GO 激活/场景语义。

### 4.5 容量、预热、集合与会话

- `MaxPoolSize` 继续表示空闲实例上限；正数限制，非正值不限，不表示 active 配额。普通默认 20，具体已配置的 Safe 容量 50/40、Audio 的设置值继续按领域配置。Action 首次接入保留显式无限，不未经测量改变动作峰值复用策略。
- FIFO/LIFO 都保留，普通默认 FIFO；Action/Audio/集合显式 LIFO，不在性能未测量前宣称 FIFO 必须淘汰。
- 预热按目标空闲数补齐，受容量裁剪，最多进行缺口数量的创建尝试。`Recycle` 模式执行 reset；`Raw` 模式只登记新对象并缓存，不触发取得/reset，供 Action 已有预热使用。null 跳过但不无限重试。Safe 的 Raw 也必须正确设置空闲标记，不能绕过身份登记。
- 预热 reset/工厂异常直接传播；已成功进入缓存的对象保持有效，失败对象隔离。需要全有或全无资源预热的 Audio 保留自己的 finally，不要求公共预热提供事务容器。
- List/Dictionary 静态入口保留现有 Allocate/Recycle/扩展形状，底层用同一 ObjectPool。归还先 Clear 清元素，保留可复用 backing capacity；List(capacity) 保持容量保证；Dictionary(capacity) 暂不升级热租容量契约，不添加 comparer 工厂重载。
- 静态集合池推荐有限默认空闲数 20，可按具体类型配置或显式 Clear。这个默认是与已有普通池一致的可预测边界，不是测量得出的最佳值。大 backing buffer 的阈值淘汰/容量分桶暂不实施，先定向测量；必要时人工 Clear 释放缓存引用。
- 严格归还要求 DictionaryPool 不再接收任意带自定义 comparer 的外部字典，避免比较规则污染默认桶；普通外部构造集合不自动进入缓存。现有 Table 实际从本池取得，迁移不需要 Adopt。
- 无自动 TTL、无新协程/Update 宿主。静态集合及 Core 参数池按现有会话支撑清缓存；实例池由 owner 清理，不能把每个实例强登记到全局 session 列表。
- Action 保留已有 Shutdown/回收队列清理顺序；其静态纯 C# 空闲缓存是否保留跨会话由 Action 负责，不以公共池 reset 的不确定执行顺序提前丢弃仍在领域清理栈上的对象。
- 集合入口在借用前校验 capacity 非负，避免热/冷路径对非法输入不同；扩容失败不能丢失已经取得的借用。冷创建仍满足请求容量，内部实现不为每次热租构造新的捕获闭包；不为这点新增公开工厂注册/接管 API。

## 5. 真实消费者、迁移影响与边界

| 消费者及证据 | 必须保持的复用/释放契约 | 2.1 接入影响 |
|---|---|---|
| Action：`Y/Action/Internal/ActionPool.cs:13–40`、`ActionQueue.cs:38–72`、`Core/IAction.cs:75–115,163–176` | LIFO、Raw 预热、动作 ActionID/lease；Deinit 先失效和清资源，当前栈退出后才真正入池；Drain 只处理本轮队列，嵌套入队留下一轮 | ActionPool 换为公共 ObjectPool，保留队列/执行器/控制器 Generation。不得把 Deinit 变成立即 Recycle，也不删除队列的失败传播测试 |
| ActionController：`Execution/IActionController.cs:34–40,48–80,93–98` | 归还重置 Action/ActionID；自身 mRecycled 与执行器 Generation 防旧执行栈 | 内核检查是补充，不取代控制器身份；接口工厂必须继续可用 |
| EasyEvent：`Foundation/Events/Internal/EasyEventPool.cs:17–38`、`Core/EasyEvent.cs:106–131` | 容量 50、0 参预热 10/泛型 1；冷/热和归还清委托；static pool 缺失时重新配置 | Safe 适配内核即可，保留实例入口；旧注销 token/Once/广播异常不在本专题重写 |
| 普通实例事件：`Events/Core/EasyEvents.cs:54–113`、TypeEventSystem、BindableList/Dictionary | new 创建，Clear/Remove 并不归池；ClearListeners 有自己的监听语义 | 不自动改为池化，不把移除映射等同 Recycle |
| BindableProperty：`Binding/BindableProperty.cs:186,266–312` | 回收直接清值/comparer/监听，不走 Value setter；readonly 内部事件只清监听，不能归事件池 | 保留容量 50、预热 1；拒绝外部 new 或派生实例误归基类池是明确 API 升级；严禁归还内部 readonly 事件后继续持有它 |
| MappedProperty：`Binding/MappedProperty.cs:13–16,84–106,126–128` | new 对象与目标属性；接口入口只是 Dispose 源订阅，保留最后值 | 不池化，不统一成可再次租用对象；不按接口名判断重复 |
| Audio：`Audio/Internal/AudioManager.cs:45–47,189–224,344–364`、`AudioVoice.cs:230–264,288–305` | Sound 私有 LIFO；Stop 失效 generation、清回调/clip/target/Source；mSounds.Remove 保证只归一次；Music/Voice 不属于 Sound 池 | 换私有缓存，维持容量 0=no-cache、坏 Source/关闭丢弃、缩容和 Shutdown 的 DestroySource；统一丢弃回调后删除重复释放路径，不能同时依赖 bool=false 再销毁一遍 |
| Resource：`Resource/Framework/ServiceManager.cs:233–248`、`ResLoader/ResLoader.cs:82–107,385–415`、`ResKey.cs:43–60,102–118` | 零 RefCount、非 Loading、ReleaseRes 成功后才摘资源表/归池；Key 临时引用由创建方归还；Loader reset 与真实资产释放不同 | 保留类型 Safe 封装；只做契约所需的局部 finally/失败接入，不设计引用计数、异步身份或物理卸载系统 |
| YL Resource GameObjectLoader：`Resource/GameObjectLoader.cs:168–218` | 自身与 ResLoader 池化；释放加载器资产引用，不是缓存/销毁所有已创建实例 | 只适配 Safe，对象实例仍由调用者负责；不要与 XFAB Loader 混名合并 |
| FSM：`Tools/FSM/Core/FSM.cs:463–549` | 禁止业务回调内归还；Pause/Exit、摘状态、逐个 DeInit；不池化状态对象 | 保留领域门禁；Pause/Exit 失败可能仍持有状态，隔离失败对象不等于资源都已清净 |
| Timing：`Tools/Timing/TimeItem.cs:52–76,132–156`、`Timer.cs:380–420` | Allocate 实际 new；释放清引用、不复用公开句柄；ScheduleVersion 防回调重投递 | 不因 IOfPool/IPoolType 恢复句柄复用，不融合 Timer 调度 |
| Table：`Tools/Table/TableIndex.cs:28–43,118–182` | 持有集合池字典和列表；空桶即回收，Clear 留字典，Dispose 归还全部；Get 是借用引用 | 直接使用统一集合；构造参数失败应先验证再租字典或局部归还；不改索引算法/公开借用引用语义 |
| UI tips：`UI/UIManager.cs:515–552`、`UI/Tip/UITipsItem.cs:17–43` | 私有 factory Instantiate，reset/回收父节点；池重建需避免旧完成闭包操作当前 _tipsPool | 若换池应捕获原池/本轮身份、先校验 uiTipPos、保留清缓存销毁；显示协程目前被注释，不在本专题恢复功能或宣称闭环 |
| Core 临时集合：`C/Manager/EventManager.cs:552–567`、`ModelManager.cs:128–193`、`PanelManager.cs:309–361,413–440` | 快照避免业务修改枚举；Panel 仍按 Session 核验，Model 清理遵守既有关闭管线 | 改为 ListPool + 明确 finally 自动清元素；这里只改临时存储，不做 Events/Panel/Model 完整融合 |
| LoadViewArgs：`C/Other/LoadViewArgs.cs:176–209,238–287` | New 初始化 leased/thread；Claim 冻结；BeginRead/EndRead 只读窗口；操作终态 Release；立即清七类值；禁止旧别名 | 改私有 ObjectPool；保留这些入口和 thread/owned/reading 门禁，不向业务暴露通用参数归还接口 |

消费者范围中未找到正常生产路径依赖“外部 new 后进入 Safe 池”，但公共 API 当前可以这样调用，仓库外消费者未知。收紧为本池借出对象是实现可靠身份目标的当前推荐，具体 API 按第 10 节保留合理调整空间；涉及重要兼容性变化仍须明确说明，不把本地搜索当作外部兼容性证明。

**Action 失败回收的必要补充**：通用池只能看到自身钩子中的失败，看不到此前 OnDeinit/订阅释放的异常。AbstractAction 的派生清理失败、UniRxAction 的订阅释放失败等不得仍安排无条件入缓存；回收队列保留到栈退出后执行，但可以执行 Discard，且不重复调用已经失败的领域清理。组合节点若自身已摘空引用、只是子对象清理失败，可由该领域确认自身仍可复用，不把“任何异常”机械等同所有节点污染。领域 Allocate/Reset 交付前失败也必须结束本次借用而不把半初始化对象缓存；保留原始异常及已有附加清理错误报告，不重写组合算法。

以下既有领域问题不得夸大为已随池融合修复：旧事件 token 没有代次；Once 嵌套 Trigger；Resource 临时 Key 多处没有 finally、ReleaseAllRes 可能部分失败；FSM Pause/Exit 失败；UI tips 显示流程未启用。仅在接入新契约会直接触发或必须保护本次借用时做最小局部处理，其余留对应专题。

## 6. 源码与程序集归属

### 6.1 实际结构和推荐依赖

| 当前位置/程序集 | 当前 references | 本专题建议 |
|---|---|---|
| `Y/Shared/` / `YLFramework.Foundation.Runtime` | `[]`，overrideReferences=true，autoReferenced=false，允许 UnityEngine | 增加中立 ObjectPool、IPool、必要工厂、集合池；不依赖 Core/Runtime/Root/Audio/Resource/XFAB |
| `C/` / `YLFramework.Core.Runtime` | Foundation、XFABManager、UnityEngine.UI | 引用保持；Core 参数/临时列表向 Foundation 接入，不增加 Runtime 引用 |
| 普通 `Y/` / `YLFramework.Runtime` | ZipTool、UnityEngine.UI | 增加 Foundation；Safe、IOfPool/IPoolType、Action/Audio/Binding/Events/FSM/Resource/GO 留此处 |
| 普通 Editor / Core Editor | 各自 Runtime，Editor-only | 当前 Editor Pool 类型检索无直接调用；仅实际用到迁移类型时才加 Foundation，不批量改引用 |
| 旧 EditMode/PlayMode Tests | Runtime 等，当前未直接引用 Foundation | 使用迁入/新内核类型的套件加显式 Foundation 引用，Unity asmdef 不能靠间接引用假定可达 |
| Core/Foundation Tests | 已引用对应 Core/Foundation | Foundation 增必要内核回归；Core 继续验证无整体 Runtime/原 XF 引用 |
| DOTween 集成 | Runtime + DOTween.dll，DOTWEEN 条件 | 仅实际引用公共 Pool 类型时加 Foundation；不能让 Foundation 反向依赖 DOTween |
| 原游戏 `Project.Game.MainModule` | Runtime、原 XF/XFAB/XFFSM 等，无新 Core | 不修改；目前无本专题 Pool 直接调用；保持原加载链及现有 YL Audio 行为 |

证据为各同名 asmdef；`T/Core/EditMode/CoreIdentityTests.cs:35–44` 已明确断言 Core 不依赖 YLFramework.Runtime、XFGameFramework 或 Editor。这些关键断言继续保留，不为迁移弱化。

### 6.2 最小迁移闭包

1. 新内核位于 `Y/Shared/Pool/`。共享 IPool、IObjectFactory 及 Default/Custom 工厂若被内核保留使用则一并移动；ListPool/DictionaryPool 移入 Shared 并改为同一内核，退出 StaticStackPool 的独立缓存。
2. **会话依赖只需一个现存小文件**：`Y/Foundation/Singleton/Internal/SingletonSessionState.cs` 只依赖 System 和 UnityEngine，不依赖 SingletonCreator、Root、MonoSingleton、Events。推荐原样迁入 Shared/Internal，保留现有 reset/quitting 行为和类型名，不顺带改 Singleton 实现。
3. 该类型当前 internal；建议 Foundation 只对 `YLFramework.Runtime` 和 `YLFramework.Core.Runtime` 给予所需的窄 `InternalsVisibleTo`，不是向所有程序集公开会话管理 API。friend 不产生反向程序集引用。不要为这一个文件迁整个 Singleton，或让 Shared 引用 Runtime。
4. SafeObjectPool 留 Runtime，继续使用现有 SingletonProperty，内部组合 ObjectPool；EasyEventPool/BindableProperty 的 HasInstance 检查仍能重新配置池。IOfPool/IPoolType 留 Runtime，因为 Core 推荐方案不需要它们。
5. LoadViewArgs 的私有静态池及会话清理在 Core；New/Release 才是唯一生命周期入口。不迁 LoadViewArgs 到 Foundation，不给 Foundation 引入 View 类型。
6. NonPublicObjectFactory/MultiObjectFactory 等纯创建辅助可保留 Runtime；除非最终内核确有引用，不为归属整齐搬它们。移除反射 reset 不等于禁止显式反射构造；不引入反射泛型池/AOT 代码生成系统。

移动已有受管脚本保留 `.meta`/GUID；新文件由 Unity 生成独立 meta。纯类移动也会改变程序集类型身份，实施批次需定向核对 SerializeReference、类型/反射字符串、公开基类及实际脚本依赖；保留 GUID 不能代替程序集兼容分析。没有依据时不生成兼容壳或大范围 MovedFrom；发现真实保存数据再明确迁移。

## 7. GameObject Pool 与 XFAB 边界

### 7.1 三种能力不能混同

| 能力 | 实际职责/所有权 | 本阶段关系 |
|---|---|---|
| 自有 `P/GameObject/GameObjectPool{Data}` | string key + FIFO，回收失活并挂 DDOL 桶根；空池不创建；Prefab 初始化可 Instantiate；容量拒绝会 Destroy；不拥有资源加载登记 | 普通池只复用 Data；不能靠替换缓存就完成 GO 生命周期 |
| `YLFramework.Services.Resource.GameObjectLoader` | ResLoader 持 Prefab 资产，自身可复用；实例由调用方拥有 | 与自有 GO 池无调用关系，不合并资源领域 |
| `XFABManager.GameObjectLoader` | Prefab hash 桶、实例 hash 映射、活跃/空闲表、超时销毁和 Prefab 释放 | Core/原游戏真正使用的临时后端，保持只读原链 |

`C/Manager/ViewManager.cs:5` 显式别名指向 XFAB Loader，187 归还、319/408 取得；原游戏 `Assets/Games/MainModule/Scripts/Controller/EffectController.cs:16–61` 也使用 XFAB。当前原 XF 包未找到独立 ObjectPool GO 类；实际是 ViewManager 直接包装 Loader，不虚构待迁移的额外类型。

XFAB 依据：`AB/Runtime/Loaders/GameObjectLoader/GameObjectLoader.cs:93–96,313–330,563–622,710–714,734–747`。Prefab/实例以 GetHashCode 作为表键；回收只失活并记时、不 reparent；记录全部实例而非仅 FIFO 空闲队列。默认扫描 60 秒、闲置销毁/空池关闭各 600 秒，扫描等待用 unscaledDeltaTime、期限使用 Time.time；Preload 可停自动卸载。这里只确认来源边界，不在 YL Pool 修原包 hash 或时钟。

XFAB 空池关闭调用 UnloadAsset(prefab)；`AB/Runtime/AssetBundleManager.cs:1540–1560,1620–1645,649–677` 表明资产缓存 hash 去重不是每次调用的独立租约，最终有 Bundle.Unload(true) 路径。Editor Assets 模式不走相同 Bundle 登记。缓存实例、销毁实例、释放 Prefab 登记、卸载 Bundle 是四个不同动作；自有 GO 池没有后两者协议。

### 7.2 自有 GO 池确认问题

- 跨 key：`GameObjectPool.cs:218–231` 直接交对应 Data，`Data.cs:90–94` 仅桶内判重。同一实例可同时排在两桶，分配两次或被一桶 Clear 销毁而仍登记于另一桶；按可变 name 回收不是稳定身份。
- 转换期：`Data.cs:114–118` 先入队再 SetParent/SetActive(false)；139–149 先解除空闲判重再 SetParent/SetActive(true)。父节点/激活回调可重入归还当前对象，外层没有转换态及回调后的桶/实例校验。确认保护缺口，不宣称已复现无限递归或特定 Unity 激活错误。
- 初始化数组：`GameObjectPool.cs:152–166` 按索引而非成功接纳数量补齐，尾部无条件 Destroy。已有 x 后传 `[x]` 会销毁仍在缓存的 x；已有一项后传 `[y,y]` 会先入池 y 再销毁同一 y；null/重复前缀也会误丢有效后项。这是独立正确性缺陷。
- 容量/fake-null：`-1` 无限、`0` 禁缓存；修改容量不缩已有缓存，非法负值不一致校验；死引用只在 Allocate 清理，因此 Count 会误挡归还/预热。普通池 `<=0` 无限不能直接替代。
- 清理：Clear/ClearAll 销毁缓存和根，不追踪借出对象。活字典枚举是潜在加固点，但 Destroy helper 是延迟 `Object.Destroy`，不能据此称普通 OnDestroy 已同步破坏枚举。待销毁而尚未 fake-null 的对象可能被同帧重收，需要退休身份。

Assets 下定向 C#、meta/类型引用与测试检索未找到自有 GO 两类的直接业务调用或专项套件；不证明仓库外/动态消费者不存在。`Documentation/Modules/Pool.md:51,62` 宣称路径兼容测试与 GO 套件，但当前未找到这些专项，不能作为覆盖证据。

### 7.3 推荐范围

**默认不把 GO 纳入 2.1 必做批次。**共享 Safe 适配时保证 Data.OnRecycle 的销毁责任不退化即可。独立 GO 缺陷留在本文，不将自有 GO 现状称为已安全统一。

可选 GO 批次必须作为完整正确性任务：实例→桶身份、转换态/桶代次、先摘清理与待销毁隔离、fake-null 数量维护、容量校验/缩容、唯一有效初始化输入及输入所有权。仅用带一次性触发守卫的 Unity probe 验证重入，不进行危险递归。

不纳入：接管 XFAB 缓存、重新设计 Prefab 资源 lease、AB 计数/物理卸载、共享异步取消、原游戏特效迁移、全局资源 Clear 或新自动超时宿主。Module.Close 仍不清全局池，不释放其他消费者持有的 Prefab。

## 8. 分批实施计划

每批按已批准的设计目标与第 10 节调整边界，另行取得实施授权并验证；表中的独立提交价值不等于自动 Commit 授权。实施前重新核对工作区及源码变化，保护已有修改。默认四批，不把每个小 helper 切成一批。

| 批次 | 核心目标/修改范围 | 前置与直接消费者 | 风险/难度 | 验收与独立价值 |
|---|---|---|---|---|
| **A 中立内核与集合基础** | Shared 新 ObjectPool，引用身份状态、工厂/冷热、容量/丢弃/预热/重入；迁必要工厂/接口与会话小文件；Runtime 增 Foundation；集合改共享内核 | 遵守第 10 节 D1/D2/D4 的目标与调整边界；Table 与已有集合 API，SingletonSessionState 现存使用者、Pool 测试程序集 | 高：身份追踪不强保活、回调状态机、容量重入、程序集可达性；不得连带迁 Singleton 全集 | Unity 编译；Foundation/集合定向 EditMode；旧 Singleton 会话最小回归、Table 最小集成；无池自动宿主、Core依赖边界不变。可独立提交新的稳定公共底座；旧对象实现只作为后续待迁移临时路径 |
| **B YL 对象消费者接入** | Safe 组合内核；ActionPool 改共享对象池并区分延迟归还/失败丢弃；Audio/UI 私有缓存迁移，必要局部 finally/原池捕获；Binding/Events/FSM/Resource 保留领域 reset；退出现有 Pool/Simple 独立存储算法 | A 稳定，并遵守 D2/D3 的目标与调整边界；Action、EasyEvent、BindableProperty、Audio、Resource、FSM、GO Data；不做系统完整融合 | 高：Safe 标记契约、旧 token 未解决、Audio 丢弃重复销毁、UI Unity-null、Action 不能提前复用、失败部分清理 | 定向 Pool/Action/Events/Binding/Audio；必要 FSM/Resource reset 补测；Audio Source 缓存与游戏音频非干扰。Simple/Action null/重复与 Safe 失败重试测试按新契约改写，不删关键断言。可独立提交消费者共用一套机制 |
| **C Core 参数与集合收敛** | LoadViewArgs 私有池保留接管/线程/读取窗口；Core Event/Model/Panel 临时 List→ListPool + finally；退出 ClassObjectPoolManager/IReuseable；适配 CorePool 测试清理 | A/B 稳定，并遵守 D3/D4 的目标与调整边界；Core 参数、Panel、EventManager、Model 清理 | 中高：操作终态参数归还一次、旧 alias 限制、Panel Session 快照、fixture 不能清非法残留制造通过 | CoreLoadViewArgs、必要 DirectView/Xfab 参数成功/失败/Abort、Panel与Model清理、CoreIdentity；保持 Module/Model 非池化及原 XFAB GO链。可独立提交Core不再维护第二个普通池 |
| **D API 退出与专题收口** | 删除已无消费者的 ClassObjectPool/Independent/旧基类及临时 API壳；保留纯创建工具；同步实际 Pool 文档、测试入口及必要使用说明；定向核对类型/资产/反射、许可/meta与程序集唯一归属 | A–C 验收，并遵守 D3 的目标与调整边界；真实C#/文本绑定、Editor/测试、公开API | 中：来源记录/脚本身份、遗漏间接消费者、误保留兼容壳；不顺带重命名Foundation | 差异、引用/asmdef静态核对；挑选跨模块回归与Domain Reload关闭验证；Audio受影响时代表性原游戏音频流程。只有一套普通/集合算法，无Core→Runtime，无Package/游戏修改。可独立提交正式收口 |

**可选 E：自有 GO 正确性，难度中高。**只在 D5 明确选择后纳入，不阻塞 A–D。范围见 7.3；前置为公共 Safe/丢弃契约稳定。直接影响只有自有两类及新建最小 Unity probe；需要实际 PlayMode 验证跨 key、转换期、数组、死引用容量、Clear/宿主销毁及场景回迁。当前没有可复用 GO 专项，必须补必要测试；不迁游戏或Core后端。具备完整独立提交价值，但默认延期。

B/C 大小来自真实消费者闭包，不要求一次改全文件：允许在同批按风险先编译再小范围验证，但不得把半迁移状态宣布为本批验收完成。若发现外部绑定、资源释放或状态 reset 需求超出本文，先报告并确认，不靠兼容补丁隐式扩大任务。

## 9. 测试、测量与质量策略

### 9.1 已存在、可复用的证据

| 现存套件/位置 | 价值及影响 |
|---|---|
| `T/EditMode/PoolContractTests.cs` | 工厂异常、FIFO/LIFO、容量、满池清理、预热、Clear 快照重入、Safe 标记；目前未覆盖全局所有权。重复/null/失败回滚用例是真实旧契约，不是测试错误 |
| `T/EditMode/Action/ActionInternalSupportTests.cs` | private/interface factory、Raw 不 reset、LIFO、Drain 延后嵌套、异常不重放；68–85 明确锁定缓存 null/重复，需按批准新契约替换为拒绝行为 |
| `ActionExecutionTests`、`ModernActionTests`、`NamedActionTests`、`ActionKitLifecycleTests`；对应 PlayMode 定向用例 | 回调 Deinit、组合 lease、旧 controller、关停、延迟回收驱动；不机械重跑所有动作或 DOTween |
| `EventsContractTests`、`BindingContractTests`、必要 `EventsLifecyclePlayModeTests` | 保持多播快照、异常传播、普通事件清理、Binding 通知/comparer/Prefs；Binding 当前不直接覆盖属性池化 reset，需要最小补测 |
| `Audio/AudioCleanupTests`、`AudioSoundBudgetTests` | LIFO/重复Stop、旧handle、容量0、Source销毁、独立Source预热、失败预热finally、损坏Source。564–593 反射依赖 SimpleObjectPool字段及工厂注入，需按新类型适配但保留故障注入 |
| `T/Core/EditMode/CoreLoadViewArgsTests.cs` | 清七类值、冷热同实例、Claim/读取窗口/线程/重复归还；13–76,230–233 依赖旧pool私有结构；256–295 有 IReuseable门禁验证，退出接口后转为验证唯一专用入口，不删保护目标 |
| `T/Core/PlayMode/CoreClassPoolScope.cs`、`CoreModulePlayModeTests.cs:248–274,857–877` | fixture只清自身对象；Module/Model不池化。新fixture按实际私有/集合池适配；保留构造次数、不同实例、业务状态/ID、不隐式入池断言，不只是删掉反射检查 |
| `CoreDirectViewPlayModeTests`、`CoreXfabPlayModeTests`、`CoreIdentityTests` | 参数成功/失败/Abort和后端同实例复用、Panel会话、非干扰依赖；Xfab Assets用例不证明真实Bundle物理卸载 |
| `Assets/Games/MainModule/Tests/PlayMode/YLClickSoundPlayModeTests.cs`、`YLAudioGameFlowPlayModeTests.cs` | Audio迁移后代表性点击/重叠音效/设置/进入退出再入验证；不修改游戏测试或业务作为Pool实施要求 |

未找到文档宣称的 SafeObjectPoolTests/PoolExtraTests/ClassObjectPoolTests/IndependentObjectPoolTests/PoolFactoryTests/GO 专项，不将历史文档名称当现存覆盖。本轮所有测试只读，没有执行结果或通过数量。

### 9.2 只补关键契约

- 冷/热取得回调一致；OnAllocate/OnRecycle 抛错与部分清理后的隔离；丢弃清理异常仍保留根因。
- 自定义 Equals/可变 GetHashCode 的不同实例保持独立；null不缓存；重复归还只清一次；wrong pool不修改状态/不调用回调；失败对象不能再归还。
- 同对象取得/归还钩子重入拒绝；其他对象重入不会突破容量；回调内 Clear/Dispose 不把当前对象交付到关闭池或复活缓存。
- 缩容/Clear/Dispose 的永久丢弃回调逐项执行；借出对象不被主动清理；旧池晚归还只清理不缓存，Safe新实例不接纳旧借用。
- 弱键追踪不保活失联借出对象：只用普通测试对象和 WeakReference 做最小 GC 生命周期验证，不建立性能/内存测试框架；GC检测按实际环境设计可靠断言，不用任意Sleep。
- 集合归还 Count=0、List容量保证、Dictionary comparer不污染、静态新会话不保留引用。Binding内部readonly事件不被独立归池。
- Action业务Deinit后、Drain前不得取得同一实例；派生清理失败不能复用污染实例，延迟丢弃不重复失败的清理；租约/旧控制器继续有效隔离。复用 `ActionExecutionTests` 的根因/附加清理错误用例，补最小丢弃身份验证。公共pool检查不代替延迟队列。
- LoadViewArgs失效/Claim/读取窗口/线程/终态立即清值保持；Module/Model绝不隐式入公共池。
- Audio容量0、缩容/坏Source/Shutdown永久释放且不重复；LIFO、generation和预热故障注入保持。

### 9.3 未来验收分层

每批先检查 diff、新文件/meta、实际 asmdef及消费者，执行对应Unity编译，再执行最相关EditMode/PlayMode，分层报告结果。无明显行为受影响时不全框架测试、不全游戏/Player构建、不清Library。

性能若进入实施决策，比较相同工作负载下的现有栈/新内核：明确冷创建与热周转、缓存规模、计数与托管分配、保留内存；分别测集合和Action高周转，而非给整个框架虚构性能百分比。Mono/Editor测量不代替IL2CPP/AOT或Player结论，必要平台验证按实际发布风险安排。

原游戏没有直接Pool调用，不代表完全无影响：其YL Audio与Binding/Events随新Runtime变化。B/D按风险复用现存游戏音频代表性验证，保留偏好/音量及XFAB资产路径；不把历史阶段1通过当作新改动通过。原Package恢复差异、Logger/MCP过渡问题和真实AB卸载仍沿用长期蓝图限制，本专题不扩大处理。

## 10. 审阅结论与调整边界

### 10.1 已确认的两项设计补充

- **Model 的池化边界**：不恢复默认隐式池化，保持现有 Model 的构造、注册、移除及业务状态隔离契约；允许未来根据实际需求研究显式、可选的池化。未来方案须另行明确接管、reset、身份与失效引用、资源释放及验证，不因本次统一 Pool 自动实施 Model 池化。
- **目标批准与实现弹性**：用户批准可靠身份、异常安全及统一公共内核等设计目标；具体 `ConditionalWeakTable`、API、类层次和内部结构保留合理调整空间。以下表格及第 4 节具体方案是当前推荐，不是必须逐字实现的限制。基于源码和验证作出的等目标合理调整无需重新开展总体研究；若改变重要公共行为、兼容性、依赖方向、领域所有权或专题范围，仍需明确说明并取得确认。

### 10.2 当前推荐与范围约束

| 决策 | 推荐 | 合理备选及代价 |
|---|---|---|
| **D1 公共归属/最小闭包** | 复用Shared Foundation；ObjectPool/集合/必要工厂进入中立层；只移动现有session小文件，窄friend访问；Safe及领域接口留Runtime；不新建asmdef | 可新建Pool专用asmdef但收益不足；或重复一个CollectionSession机制，避免移动session但增加相同会话责任，不优先 |
| **D2 默认安全与异常契约** | 默认只归还本池借出对象；弱键身份状态；禁止外部new自动接纳；回调失败隔离而不是自动可重试；null不缓存；容量提交前复核；永久清理逐项尝试 | 保留unchecked默认更快但持续暴露重复/跨池风险；保留Safe失败重试需承认状态不回滚，不能称安全。若需unchecked必须有实际测量及明确独占领域证明再单独批准 |
| **D3 API收敛/兼容幅度** | 新ObjectPool内核；保留IPool形状、Safe/领域入口、List/Dictionary API；迁实际Simple/Action/Core消费者；退出旧Pool继承、Simple重复入口、Type类池/反射reset、Independent、Core manager/IReuseable | 保留Type分配或自动reset仅在查实动态调用/外部真实依赖时另立窄适配，不为未发布API保留全部旧产品。需接受外部new、旧基类继承、字段反射测试的明确变化 |
| **D4 内存/生命周期策略** | 不保留普通池TTL协程；默认空闲20，已配置领域容量/顺序保留；集合有限默认20、显式Clear；没有每租用token或通用资源lease | 保留TTL须增加时钟/宿主/关闭/异常释放机制；强租约改变所有公共用法；大buffer自动淘汰须先证实收益。不把容量默认当最佳调参结果 |
| **D5 GO专题范围** | 默认延期独立GO正确性批次；2.1只保证GO Data接入Safe不回归，不替换XFAB | 可选择可选E完整修复自有GO并补专项，但没有Core资源链收敛收益，增加Unity生命周期验证；依然不纳入完整资源系统 |

以上设计目标及两项补充已获用户确认，具体技术选择按 10.1 保留调整空间；GO 默认延期，可选 E 仍须单独确认。**本次仅授权文档提交到 `integration/xf-yl`、快进同步远端并准备 `fusion/2.1-pool` 分支，不据此启动 A、可选 E 或任何功能代码实施。**后续实施中若改变重要所有权、异常契约、API 兼容或 GO 范围，应先说明并同步本文件，避免下一会话自行猜测。

## 11. 专题完成标准

- 新YL普通对象/集合最终只有一套缓存、身份与释放算法；领域封装没有各自再维护通用缓存。
- Core只向中立Foundation接入，不增加Core→整体Runtime/原XF引用，不恢复Module/Model默认池化。
- Action延迟回收/租约、LoadViewArgs接管协议、AudioSource释放、Binding内部事件所有权、Resource领域释放顺序均有定向验证。
- 冷热、身份、转换期、容量、清引用和失败隔离契约明确；普通池对旧别名、线程、工厂独占、资源释放的限制不作虚假保证。
- GO和XFAB范围保持批准边界；原Package/游戏路径未迁移。必要Audio间接影响通过代表性非干扰验证。
- API/测试/文档/程序集与meta对应真实最终状态，已发现但延期问题清楚保留；实际验证分别报告。Git操作继续单独授权。

**设计目标与审阅补充已确认；本轮仅完成文档 Git 收口和实施分支准备，不进入功能代码实施。**
