# XF × YL 2.2 Events 事件系统融合研究与技术设计

**状态：整体设计已批准，待分批实施（2026-10-09 用户完成总体审阅；2.2-A 及后续批次待单独授权）。**

本文件是唯一专项方案。总体设计已获用户确认，但**尚未实施、尚未运行验证**。确认的是架构方向、行为契约和阶段边界，不要求逐条照搬具体私有结构、算法或候选方法名；普通内部取舍由实施时依据源码自主决定，重大行为、所有权或范围变化先说明并等待确认。本状态更新仅记录审阅结论，不代表功能已实现、缺陷已运行复现或已获批次实施授权；不自动启动 2.2-A、其他专题或 Git 操作。

## 1. 基线与边界

- 本轮实际核对 `integration/xf-yl`，HEAD `62a6b20`，开始时工作区干净；包含 Pool 合并里程碑 `5754f3a` 及 E 提交 `71fc00d`。最新提交是空目录残留清理，不是 Events 功能变更。
- 已读取根 `AGENTS.md`、`Documentation/IntegrationPlan.md`、阶段 2 总体方案，复用 Pool 第 16～17 节和 View 当前完成契约。部分旧文档仍写 Pool E 待 Git 收口，与当前 Git 历史不一致；本文件采用已核实基线，不顺带修改历史规划。
- Unity `2022.3.62f3c1`；锁文件中的 XF revision 为 `2182708aecc596f30739fa8634fe2b9b6946ddd7`，XFAB revision 为 `c35dc4058ecd831409199b43cb328024202d4557`，与本轮定向读取的 PackageCache 目录一致。既有依赖声明/实际解析差异沿用长期蓝图，不在本专题升级依赖。
- 只做静态源码、测试断言、引用与程序集研究；没有执行 Unity 刷新、编译、EditMode、PlayMode、Player、性能测量或配置变更。下文“确认缺陷”指可由控制流证明的问题，不冒充本轮运行结果。
- 只产生本计划文件。原 XF Package、CGJ2024 游戏、Runtime、Editor、测试、资源、asmdef 和项目配置均不修改。
- 不重审已验收的 Pool/Module/MVC/View，不设计后续 Update/Timer/Audio/Resource 内部实现，不把生命周期作用域升级为跨系统任务管理器。

路径简称：`Y` = `Assets/YLFramework/Runtime/`；`C` = `Y/XFMain/`；`T` = `Assets/YLFramework/Tests/`；`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`。行号对应研究基线，实施时重新核对受影响位置。

## 2. 双方能力与源码依据

### 2.1 通用机制

| 能力 | XF / 已承接 Core | YL 现状 | 融合取舍 |
|---|---|---|---|
| 实例事件 | 通用 EventManager 以静态全局分发为主；Unity 监听器另持 UnityEvent | `EasyEvent` 0～3 参，实例所有权、监听数、普通/Once/立即回调注册、池化；`IEasyEvent` 提供类型擦除的无参观察 | 保留实例事件，不强制经全局 bus；沿用 YL 易用入口，共享可靠注册/分发机制 |
| 命名事件 | int 或 string，string 经 `Animator.StringToHash` 汇入同一 int 字典；`XFEvent` 0～7 参 | String 保留完整键，0～3 参，签名冲突抛异常 | 保留 XF 0～7 强类型参数能力与 YL 完整键/显式校验，改用 `Action` 家族而非另一套来源前缀委托 |
| 类型事件 | 无对应独立 payload-type bus | `TypeEventSystem.Send<T>(T)`，按泛型声明类型路由；`Send<T>()` 可创建参数 | 保留类型消息，不用反射分发；不把事件对象类型容器 `EasyEvents` 与 payload 类型路由混为一谈 |
| enum/int 动态事件 | int 强类型签名，同 int 混签名记录错误后拒绝 | Enum 为 `Action<object[]>`；IntID 为 `Action<int, object[]>`，通过 `IConvertible` 转 int | 保留动态协议适配层；新正式键使用明确 int/enum 身份，不让通用内核承担任意字符串/数值转换 |
| 取消所有权 | Add/Remove 按委托；无注册 token | `IUnRegister`、`CustomUnRegister`、列表及生命周期扩展 | 精确 token 成为所有权的正式表达；保留显式委托退订作为便利操作，不让它替代 token |
| 组合观察 | 无对应 Or 组合 | `OrEvent`，任一 source 触发即通知；末监听移除时释放 source | 保留轻量组合，不扩展为响应式框架；修正惰性连接、终结及身份 |
| 分发行为 | 注册顺序，多播快照，重复注册会重复执行；回调异常向上中断 | 普通 EasyEvent 同样采用快照和 fail-fast；Once 用包装委托 | 保留普通广播契约，补精确身份与 Once consumed；必要清理采用独立异常策略 |
| 重置/清理 | 清指定键/全局监听，内部空键仍保留；Editor Play 切换重建单例 | 实例清监听、回收、系统 Dispose/重建、Session reset | 区分清监听、终结实例代次、删除键、关闭整个 bus，不再用一个 Clear 暗示全部含义 |

核心证据：`C/Manager/EventManager.cs:15–30,87–104,175–195,201–251,413–546,550–643`；与原 `XF/Runtime/Manager/EventManager.cs` 比较，除 namespace/import 和 Pool 临时 List/finally 外，事件算法基本未变。旧 `params object[]` 方法已是 `[Obsolete(..., true)]`，不算仍可正常使用的 XF 公共能力，也不需要为其保留第二份存储。

YL 证据入口：`Y/Foundation/Events/Core/EasyEvent.cs`、`IEasyEvent.cs`、`EasyEvents.cs`、`OrEvent.cs`；`Global/{StringEventSystem,TypeEventSystem,EnumEventSystem,IntIDEventSystem}.cs`；`Abstractions/`、`Lifecycle/`、`Internal/EasyEventPool.cs`。现有 `Documentation/Modules/Events.md` 的多播/异常说明仍可参考，但末尾列出的部分历史测试类名不等于当前实际测试入口。

### 2.2 领域能力，不合并成全局总线

| 领域 | 有价值的能力 | 推荐边界 |
|---|---|---|
| XF 物理监听 | Collision/Trigger 的 2D/3D Enter/Stay/Exit、LayerMask 过滤、Inspector UnityEvent | 保留自有 Core 组件及字段身份；无需把 UnityEvent 转成全局消息 |
| XF 动画监听 | `OnAnimationEvent(string)` 接收 AnimationClip 的字符串函数调用 | 保留动画适配与方法名；不因原包标为过时就删除自有能力，亦不引入其 CommonSystem 依赖 |
| XF EventTrigger 扩展 | 按 Unity `EventTriggerType` 添加/移除 runtime UnityAction | 保留对 Unity EventTrigger 的便利操作，与 YL EventListener 是不同宿主/工作流 |
| YL EventListener | 指针、拖拽、鼠标、碰撞/触发器及 Inspector UnityEvent；代码注册与 Inspector 通道共存 | 共享代码监听底层；固定原生 payload 映射；不重写 UnityEvent 的持久化结构或执行算法 |
| Action | 全局 Unity 消息作为实例 EasyEvent，任务执行器有对象/当前场景/全局所有权 | 只替换事件机制，不集中驱动、不更改动作调度、暂停、取消和延迟回收 |
| Binding / Audio | 属性实例事件、立即通知、音量/配置观察及重入版本保护 | 保留值通知与配置生命周期；只修正直接相关订阅身份/Once/reset |
| Resource | 每个 Res 的完成通知、Ready 时同步回调及 Loader 自有取消登记 | 不改成 bus；其资源引用计数、回调失败收尾、ReleaseAllRes 问题留 Resource 专题 |

原 XF 领域证据：`XF/Runtime/Other/Listener/CollisionEventListener.cs:16–36,47–99`、`TriggerEventListener.cs:16–37,44–97`、`AnimationEventListener.cs:8–22`、`Extensions/EventTriggerExtensions.cs:12–70`。这些领域功能与全局 EventManager 没有必须统一存储的理由。

## 3. 可靠性问题分类

| 项目 | 分类与源码机制 | 2.2 处理方向 |
|---|---|---|
| string hash 身份 | 确认身份设计有损：string 只保留 int hash 路由，hash 冲突时同签名会合并监听、不同签名会拒绝；还与显式 int 共用域。没有寻找/运行一组实际碰撞字符串 | 完整 string + Ordinal equality；int 与 string 独立身份域；不保留自动 hash 互通 |
| 签名错误只写日志 | 确认可靠性不足，`EventManager.cs:598–611,631–643` 失败不返回有效身份；Trigger 同样只日志拒绝 | 注册/发布签名冲突在修改前抛 `InvalidOperationException`；错误包含键和两个签名；token 取消不依赖再次推断签名 |
| token 不代表注册实例 | 确认缺陷：`EasyEvent.cs:43–46,80–82` 的 token 捕获对象+委托而非注册身份；重复委托可注销另一注册，旧 token 在池复用或整键回收后可影响新监听 | 每次 Register 新身份，精确取消；Clear/回收/Dispose 撤销旧身份，不复用 token |
| Once 重入 | 确认缺陷：`EasyEvent.cs:65–73` 先注销但无 consumed 守卫；前序监听嵌套 Trigger，内层执行后外层旧快照再次执行 | 执行用户代码前 consumed + 精确摘除；即使抛异常也已消耗；Binding 自有 Once 包装同样处理 |
| 批量清理丢责任 | 确认缺陷：`UnRegisterTriggerBase.cs:49–57`、`RegisterListExtension.cs:26–35` 先 DetachAll，首个取消异常中断，剩余 token 已丢出容器 | 每项尝试，成功/失败分开，最终汇总；存活 owner 保留失败 token 供明确重试；销毁 owner 则尝试全部并报告失败，不虚称全部退订成功 |
| 场景“Current”命名 | 当前明确契约限制而非偶发错误：响应任意 `sceneUnloaded`，现有 PlayMode 测试锁定 | 保留 AnyScene 能力并明确命名；新增显式 Scene handle 绑定，不静默把旧场景语义改成指定场景 |
| Or 连接/复活 | 确认风险：构造即连接 source；没有 listener 或立即回调抛错时可能留下连接；Dispose 没有永久终态，旧 token 可能影响重新连接 | 首 listener 连接、末 listener 释放；保留 source 描述以支持正常重新订阅；Dispose 永久终结；失败回滚本次连接 |
| EventListener payload | 确认缺陷：只校验与当前注册类型相同，未校验 EventType 原生 payload；首次 `OnClick + int` 可注册成功但实际 PointerEventData 消息不分发 | 预定义 payload schema；注册前校验，不接受“首次错误类型锁住通道” |
| 类型擦除 null 注册 | 确认不一致：泛型 `IEasyEvent.Register(null)` 包装 null 后 Trigger 可 NRE，强类型 null 路径却忽略 | 一致返回空 token，不创建有害 wrapper；不把 null 问题伪装成异步异常 |
| 回收中旧快照继续 | 当前 XML 明确允许，不能说已违反旧契约；但在 owner 回收/复用时会继续执行旧业务，且 Binding 同样可能复用 | 推荐有意改变：owner reset/回收终结旧代次，停止旧在途快照；普通退订/ClearListeners 仍保持快照。须单独验收并更新契约 |
| 从未 active 的 GameObject | Unity OnDestroy 的平台契约限制，不是通用 token 修复即可解决 | 不强制激活对象、不新增全场景扫描；自动销毁适配说明前置限制，永不激活的 owner 用显式 scope/Core 已有精确退出 |
| scene trigger 自身销毁 | 仅解除 sceneUnloaded 监听但未取消 pending token，会遗留其托管责任 | 在 trigger Dispose/OnDestroy 尝试全部 pending 取消，并维持非创建式关闭 |
| View 已捕获的旧业务回调 | `View.cs:430–475` 的 Session/eventEpoch 守卫保护的是启停钩子及回滚，`RegisterEvents` 实际仍添加原始 Delegate；普通bus快照中前序回调关闭/复用View后，后序旧回调仍可进入同一组件的新状态 | 不重写View会话；实际激活的typed callback增加捕获Session+eventEpoch的有效性检查，并按该轮精确token取消；补一个前序关闭/复用后旧回调不得进入的回归 |
| 全局EasyEvents reset仅丢映射 | `EasyEvents.cs:43–45,102–113` 的全局reset调用Clear而非ClearListeners，外部持有的旧event仍可能保留监听；普通Clear仅丢映射由测试明确允许 | 普通实例Clear语义不变；Play Session结束时全局容器尝试清旧事件监听并旋转标准channel代次，再丢映射；不将容器Remove自动改为归池 |
| TypeEvent Dispose只丢映射 | `TypeEventSystem.cs:70–73`调用EasyEvents.Clear，旧token仍捕获旧事件，因而可能保留其他监听/闭包；与“清全部监听”的XML叙述不一致 | 关闭其私有bus时实际撤销注册、清callback/token引用；旧facade不允许再Register复活，Instance按既有入口创建新实例 |
| 旧Global实例影响新入口 | 四个Global.Dispose直接调用SingletonProperty.Dispose且不核对当前身份：`TypeEventSystem.cs:70–74`、`StringEventSystem.cs:268–272`、`EnumEventSystem.cs:105–109`、`IntIDEventSystem.cs:116–120`；old.Dispose→next=Instance→old.Dispose可摘掉next入口 | facade关闭永久幂等；只允许摘除仍指向自身的静态入口，跨Play Session旧facade也不能影响新Instance；局部适配，不重写Singleton全集 |

已有广播异常向上传播、重复监听执行多次、委托注销只删最后匹配项等属于公开契约，不一概归为缺陷。新框架不是线程安全总线；后台线程自动切主线程、弱订阅、事件队列、回放、优先级、跨进程或通用异步事件均不列入本专题。

## 4. 推荐最终架构

### 4.1 唯一机制，分层入口

```text
YLFramework.Foundation.Runtime (现有 Shared)
  纯事件 channel 0～7 参 / IEventSource / 精确订阅 token / 纯组合与清理机制
  EventBus 实例 + 明确键/签名 registry + 全局上下文的 Play Session reset
  现有 ObjectPool、集合池、SingletonSessionState
             ↑                              ↑
YLFramework.Core.Runtime            YLFramework.Runtime
  EventManager 薄入口                  EasyEvent 池化适配 / Global 薄入口
  View 声明与 Session owner           Binding / Action / Audio / Unity lifecycle
  CoreEvents / Core Unity 组件         EventListener / EasyEvents 类型容器
```

- 复用现有 Foundation，不新建 Events asmdef，不让 Foundation 引用 Core、整体 Runtime、SafeObjectPool、Root、UGUI、Odin、Audio、Resource、DOTween 或原 XF。
- `WarnInClasses(typeof(View), ...)`等现存Core专用注册提示留在Core facade/声明入口，不连同EventManager整文件搬进Foundation；共享纯机制不得为提示引用View或Core分析器。
- `EventChannel` 0～7 参是拟定的新正式实例 API，使用 `Action` 0～7 参；纯机制 namespace 沿用现有 `YLFramework.Events`，避免无必要的第二次名称迁移。文件落在 `Y/Shared/Events/`，目录与程序集身份以 asmdef 为准。
- 保留 `EasyEvent` 0～3 参作为有真实消费者的正式池化适配，不再维护另一套多播/Once/token 算法；通过继承或直接组合 channel 实现，以最少字段且不改变既有 owned event 身份为准。不为形式对称补 4～7 参的池化别名，4～7 参能力由正式 channel/bus 提供。
- 保留EasyEventPool现有容量50、无参预热10/泛型预热1及Safe→ObjectPool链（`EasyEventPool.cs:17–39`）。纯channel默认普通实例，bus内部channel先按bus/key生命周期持有，不恢复所有事件自动池化；显式池化入口继续有价值。Global适配内部由“池化EasyEvent字典”改成“普通channel的EventBus”会改变内部缓存库存，不是承诺完全保留每个内部实现细节；稳定发布不额外分配，键频繁建销的性能如有实际成本再测量，不本轮增设第二套池。
- `IEasyEvent : IOfPool, IPoolType` 目前跨越池领域，不能原样下沉；保持在 Runtime，并承接共享的纯 `IEventSource`。泛型事件的无参观察是包装成丢弃payload的typed callback，与typed监听在同一序列，按注册先后执行，并非独立的先行无参通道（`EasyEvent.cs:244–248,363–367`）；共享接口维持这一能力及顺序。`IUnRegister` 及必要的纯取消实现进入 Foundation，保留已有完整类型名；pool 接口/Safe 仍保持 Pool 收口归属。
- Or 的纯组合逻辑只保留一份；可将现有 OrEvent 解耦为只依赖 IEventSource 后下沉，现有 Runtime 扩展只作必要适配。不建立两套同名组合算法。
- `EventBus`可`new`创建独立命名空间，`Default`是一种显式选择，Core EventManager转接Default。推荐YL四个Global facade各持同一EventBus实现的独立上下文，维持现有Dispose范围、IntID动态协议和字符串系统隔离；不自动让历史入口跨系统互相收发。一个通用实现的多个实例不是两套算法，不再各维护Delegate/EasyEvent分发逻辑。新业务如需统一命名空间，直接使用同一EventBus实例/Default的string、int、enum、type入口，而不是经多个历史facade猜测互通。
- `EasyEvents` 是按事件定义类型存储实例的容器，不是 payload bus。保留自有容器所有权和自定义事件能力；普通 `Remove/Clear` 只丢映射，`ClearListeners` 清监听再丢映射，维持 `EventsContractTests.cs:168–184` 的区别。静态全局实例在会话reset时增加真正的旧监听清理；自定义IEasyEvent按其UnRegisterAll契约逐项尝试，失败不能阻断其他项。它不和TypeEventSystem共用一个含义不清的Type字典。

### 4.2 键、签名和参数

| 正式身份 | 规则 | 路由方式 |
|---|---|---|
| string | 完整字符串，Ordinal，拒绝 null；空串是否有业务含义不擅自改现状 | string 域；不得经 Animator.StringToHash |
| int | 显式数值 | int 域，与相同 string hash 不互通 |
| enum | 枚举类型 + 完整底层值 | enum 域，不同 enum 的相同数值互不冲突，不强转所有 enum 到 int |
| payload type | 泛型声明的 `typeof(T)` | type 域；不按对象运行时类型额外广播给父类/接口 |

推荐内部使用带域的key+唯一channel/signature描述；每个EventBus实例使用同一种discriminated-key registry，不再让四个Global系统各自维护重复分发算法。bus实例身份本身是最外层事件命名空间；多个实例中的相同key可以有不同签名，但同一实例/key不得按签名偷偷拆成两个事件。兼容领域facade是否共享实例必须显式选择，不能仅因都是“全局事件”默认合并。

1. 首次有效注册为该 bus/key 建立签名；后续 Register/Send 的参数数量与声明类型必须精确一致。不要因为 delegate variance 或实参派生类型推断另一个签名。
2. 缺失键的Send是no-op，不创建宿主、Singleton、空channel、hash记录或签名绑定；已存在键即使暂时无listener，也先校验既有签名，匹配后no-op，错误签名仍抛错。Cancel/Remove缺失键同样不初始化Default。空callback注册返回空token，不创建新键/绑定签名；有效键/已有签名的校验不因空callback而省略。
3. 最后一个token注销只清监听，已绑定签名保持；显式`RemoveKey/Clear(key)`才终结channel、使旧token失效并允许重新绑定签名。整个bus Dispose永久关闭；Default reset关闭旧实例，新访问建立新实例。持有已Dispose实例再Register/Send抛ObjectDisposedException，Dispose/Cancel保持幂等；静态非创建式Send在尚无实例时no-op。旧Global facade同样禁止Dispose后复活，并且只摘除仍指向自身的静态入口；新的Instance仍可创建新系统。这是明确的旧实例可靠性收口，不称为无API行为变化。方法最终名称在实施批次统一，但语义不得混合。
4. 强类型路径不使用 `DynamicInvoke`、`object[]` 装箱或反射构造分发。保留 0～7 多参数便捷性，新业务可以自选单个 message struct/class，不强制所有旧 API 改 DTO。
5. Enum `object[]` 和 IntID `int + object[]` 保留为明确的动态适配协议；IntID 旧 `IConvertible` 输入在 Runtime 适配层转换，错误/溢出不得静默吞掉。正式核心 API 只接 int，不泛化转换。
6. `StringToHash/HashToString` 不再承担事件身份；删除只用于事件的自动转换链。若实施时查出外部真实持久化 hash 依赖，暂停该迁移点，单独设计有期限的映射，不猜测性保留全局 hash 别名。

### 4.3 注册身份与重入

- 每次 Register，包括相等委托重复注册，都产生独立且不复用的注册身份。token 至少关联 source incarnation 与精确 registration；推荐非池化引用身份对象，避免计数溢出和 slot 重用问题。
- token 幂等；h1 只撤销 h1，不通过 `-= callback` 推断；旧 token 在 Clear、键删除、回收、重新租用、Default 重建或 Dispose 后不会触及新注册。取消成功/失效后清除其取消引用，避免长期持有已注销回调或 owner。
- 推荐注册表采用 copy-on-write 的类型化调用记录：注册/注销时替换，Trigger 捕获 immutable snapshot；沿用现有多播变更时分配的成本模型，不在每次发布中 GetInvocationList、借临时 List 或复用正在执行的 snapshot。正常发布不分配，修改和异常收集允许必要成本。没有 Profiler 数据，不宣称该结构已比原实现更快。
- 保留 multicast Action 的有效能力：普通注册按 target 展开，保留顺序与 ListenerCount；一次 Register 返回的 token 精确移除其整组剩余 target。直接 `UnRegister(delegate)` 保持最后相等子序列语义，即使撤销某个组内 target，组 token 也不能转而影响别的组。Once 的包装视为一个注册，内部 multicast callback 按正常委托规则执行。不为这一少见输入新增复杂公共 group API。
- 普通 Register/UnRegister/ClearListeners 只影响后续或嵌套 Trigger，当前 snapshot 中已捕获的普通监听继续。嵌套 Trigger 捕获当时的新状态，不递归修改外层数组。
- Once 在实际调用前标记 consumed 并精确撤销；其他 snapshot 持有同一 Once 状态，看到 consumed 即跳过。前序监听抛错而尚未到达 Once 时不消耗；Once 自身抛错仍已消耗。
- owner reset/回收/Dispose是不同操作：撤销全部身份、终结incarnation，每个旧snapshot在继续下一监听前核对incarnation，不再进入已结束owner的业务。该项有意改变旧EasyEvent回收仍继续快照的契约；Or.Dispose也同样由继续在途快照改为终结截断（`OrEvent.cs:10–11,71–79`），不以“内部实现不变”包装。普通最后listener注销不等同Dispose，仍保留正常快照。
- `ClearListeners` 保持普通快照清理含义；新增内部 owner-reset 操作用于 Binding 等持有同一实例却必须切换生命周期的场景。reset 后事件仍可用但旧在途通知停止。原始池化对象别名归还后禁止继续使用，token 安全不意味着能辨认旧原始别名。
- `RegisterWithACall` 保持先立即调用、成功后才 Register；立即调用抛错不遗留本次监听。不改成先订阅以改变重入赋值行为。
- 泛型 `IEventSource.Register(Action)` 仍通过typed序列的payload丢弃包装实现，空callback先返回共享空token；与typed注册交错时按同一时间顺序执行，不额外引入双阶段快照。
- Or首次listener成功加入后连接sources，连接失败撤销本次部分连接和listener；最后listener离开时断开，但保留source描述供下一轮重新连接；Dispose永久关闭且清source引用。只有创建Or而不监听不会向sources注册，立即回调失败不会留下source token。有listener时追加`.Or(source)`立即连接，失败仅回滚本次追加；同source重复登记按次数连接/通知，不自动去重（原`OrEvent.cs:12,26–29`）。每轮source连接捕获连接轮次，断连后旧source snapshot不可转发给重连的新listener；source.Register若同步通知或返回时已重入关闭，迟到取得的token只取消自己的旧连接，不写入新轮。该连接守卫不改变Or已开始的普通listener退订快照。

### 4.4 异常策略

| 路径 | 推荐正式契约 | 理由 |
|---|---|---|
| 普通 Trigger/Send | fail-fast，原异常向上传播，后续监听不执行；finally 保证内部状态可再次使用 | `EventsContractTests.cs:63–79` 已锁定，Binding/Action 的实际行为不宜全面改变 |
| 签名/非法类型 | 修改前立即抛明确异常 | 不再日志后假装注册成功；不得部分建立键或连接 |
| 必要批量取消 | 先摘当前责任集，逐项尝试，保留新加入项、失败项及错误，结束后报告单个原异常或AggregateException | 必要清理不应采用广播fail-fast；不能丢失尚未尝试的token；重试资格由句柄自身契约决定，不自动重试，已消费的SingleUse失败只报告，不宣称可恢复 |
| Unity OnDisable/OnDestroy adapter | 尝试本轮全部取消，然后集中 `Debug.LogException` 报告；显式 Dispose/UnRegisterAll 仍向调用方传播 | Unity 消息边界没有常规返回调用方，不靠静默 catch 隐藏失败 |
| Core 可选移除通知 | 保持生命周期先提交、异常加入既有关闭错误收集；推荐此通知显式逐观察者继续并汇总 | 不将观察者异常变成 Panel/索引/缓存清理失败；普通广播默认不随之改变 |

逐观察者继续只作为显式通知模式/方法使用，名称建议 `TriggerAll/SendAll`，不引入全局可变异常开关或自由可编程调度策略。内部纯 channel 负责遍历和收集，Core 负责把错误并入已有关闭报告，Foundation 不调用 Core logger/生命周期。不能承诺 UnityEvent 内部的每个 persistent listener 同样逐项隔离；其算法不在本轮重写。

既有取消辅助不机械改成同一种token：`SingleUseUnRegister`在调用前消费，失败不重试；`CustomUnRegister`成功后释放，失败可重试；`CallbackUnRegister`值类型副本具有独立状态，StrictAfterSuccess允许再次调用报错。这些有明确测试/用途，不替换成假装同一注册身份的token。共享事件返回的新精确token是引用型且幂等；Custom增加同实例执行中重入守卫但保留失败重试，Callback的值类型复制/Strict模式不扩大改造。`SubscriptionCleanup.ReleaseAfterSuccess`及普通DetachAll保持既有纯工具契约，新增逐项清理入口给必要生命周期使用，不弱化`EventsContractTests.cs:94–165`的旧断言。

## 5. 生命周期与所有权

### 5.1 不同边界协同

| owner | 责任与终点 | 禁止扩大到 |
|---|---|---|
| 普通对象 | 自有 token/list 或轻量 `SubscriptionScope.Dispose`；scope 只管理明确加入的订阅 | 全局清 bus、其他对象/任务 |
| GameObject Destroy/Disable | 两种独立边界；Disable 注销不自动在 Enable 重订，重订由业务 OnEnable 决定 | 模块关闭、资源归还、全场景监听 |
| 指定 Scene | 绑定注册时显式传入的有效 `Scene.handle`；仅匹配该 sceneUnloaded；host 的 scene 迁移不暗中改变已捕获 owner | 当前 active Scene、任意 Additive 卸载、所有跨场景订阅 |
| 任意 Scene 卸载 | 明确 AnyScene 适配，维持原能力；按一个卸载触发本轮责任集 | 指定 Scene 的精确 lifetime |
| Module | 只有该 Module 明确持有的 scope/list 随既有关闭管线取消；需要时在 Controller/Module 的既有入口显式清理 | `Default.ClearAll`、其他 Module、全局 Audio/Action/Timer |
| View Session | View 自己的注册轮次/Session 负责 Activate/Stop/退出，精确 tokens 按轮次持有 | GameObject 一生、资源缓存生命周期、同实例后续 Session |
| Play Session | 只重置静态 Default/Global 注册与领域明确的静态容器，关闭旧实例并使旧 token 失效 | Module Session 与 Scene 关系，不清任意应用对象的独立 event |

`SubscriptionScope` 是订阅集合，不是通用跨系统 Scope。优先沿用 IUnRegisterList；只在需要可永久 Dispose 和禁止关闭后添加时增加一个小型具体 scope，不建全局 owner 索引或 Module 自动发现系统。scope disposed 后加入的新 token 应立即取消，不创建新的存活责任；普通 `UnRegisterAll` 的回调中新增项留到下一轮。

场景 API 建议分成 `UnRegisterWhenSceneUnloaded(Scene)` 和 `UnRegisterWhenAnySceneUnloaded()`。旧 `UnRegisterWhenCurrentSceneUnloaded()` 的本地真实调用应迁到明确 AnyScene/指定 Scene；是否保留有期限 obsolete 入口只由实际外部依赖决定，不因为现有公开方法就永久维持误导命名。保留现有 trigger 脚本 GUID/字段身份，未查实序列化依赖前不顺手改 MonoBehaviour 类型名。

自动对象生命周期不是绝对兜底：Unity 2022.3 `OnDestroy` 只保证曾 active 的 GameObject；始终 inactive 的对象必须显式 Dispose/使用 Core 已有 owner 退出能力，不为了宣称“支持所有对象”强制激活或新增 Update 扫描。参见官方 [MonoBehaviour.OnDestroy](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.OnDestroy.html)。已销毁/fake-null host 的绑定应直接取消而非创建组件；绑定 inactive host 的 Disable 意义是下一次实际 OnDisable，不暗中等同立即取消。

静态 bus 用现存 `SingletonSessionState` 注册一次 reset，不新增宿主，不把其 session reset 机制重写成 Events 服务。Default 清理保持非创建式；关闭 Domain Reload 的验证要覆盖两轮 Play 与旧 token，不能只用静态 reset 方法调用冒充真实切换通过。

reset callback自身必须完成全部Events责任后集中报告错误，不把自定义IEasyEvent.UnRegisterAll的异常继续抛进共享SessionResets循环而跳过Pool/Singleton其他重置项；只在Events自己的reset边界处理，不扩张重写已验收Session状态机制。独立EventBus实例不自动按Play Session关闭，只有明确全局owner才注册reset。

### 5.2 必须保护的消费者

| 消费者 | 已核对事实/证据 | 未来适配要求 |
|---|---|---|
| Core View/Panel | `ViewManager.cs:160–217` 已先 StopEvents、逐个 onViewRemove、OnRemove/解绑、Panel/索引/实例处理，最后可选全局广播和受旧 LastSession 限定的 OnClosed；`PanelManager.cs:450–458` 直接维护队列/排序 | View 声明记录保留完整键而非提前hash；实际Activate创建捕获Session/epoch的typed callback和token，Stop先撤本轮身份再取消该轮token；保留既有钩子/回滚守卫，并补业务callback守卫，不能声称原始Delegate已经受该守卫保护。Panel不恢复广播驱动清理 |
| Action | 消息入口复用 EasyEvent，任务有独立 handle/租约/执行器 | 不把订阅 token 当 ActionHandle，不更改 frame phase、首帧 Execute、延迟回收、Pause 或 Scene executor 取消范围；事件 facade 改动时显式核对直接 asmdef 引用 |
| Binding | `BindableProperty.cs:186,232–248,296–301` 的 readonly owned event 不独立归还；`BindingContractTests.cs:17–51,62–79,95–112` 锁定事件实例、先立即调用、重复注册 | owned event reset 旋转 incarnation，但保持同实例、IsRecycled=false、不归事件池；补旧属性 token/新租期同委托隔离；Once 复用可靠机制，不改 comparer、silent/force 与持久化逻辑 |
| Audio | `AudioMixerTests.cs:372–455` 已锁定 settings 关闭断连、嵌套新 payload 优先、Shutdown+重建隔离 | 保留内部 revision/settings/session 防线；事件 reset 不触及 global volume、Mixer、音源预算、加载或原游戏偏好。按影响选设置/路由回归，必要时复用原游戏音频非干扰 |
| Resource | 实例 Res 自有完成 Action，Loader 自有登记，Ready 时同步通知 | 不强迁 bus、不更改同步时机/引用责任；仅在共享取消接口发生程序集身份变化时核查编译和直接消费者 |
| Binding集合/Inspector属性 | `BindableList.cs:58–94,296–360`、`BindableDictionary.cs:43–78,284–336`的数据先修改，具体操作→CountChanged→Changed，批量外层合并Reset；`BindPropertyWithUnityEvent.cs:39–51`代码通知先于UnityEvent | 不改集合顺序/空Clear/批量规则，不把实例通知全局化；双通道异常阻断及序列化结构保持。共享channel确实改动这些实例事件时补一个集合顺序/批量代表回归，不为整个集合API建新套件 |
| Core其他通知 | `PanelManager.cs:185,520–531`的静态onCreateCanvas是直接Action；Module OnStart/OnClose是虚方法；StartUpModuleRequest先提交结果再发完成通知；Timer的DisableListener有真实对象停用消费者 | 保留领域hooks/直接通知，不顺手改成bus或宣称其自动绑定Module；Timer监听解绑问题留Timer专题，保留DisableListener字段和OnDisable时点 |
| Unity listener | 代码事件+persistent UnityEvent并存；`EventListener.cs:161–195`区分默认清代码与显式清Inspector | 只替换代码通道/取消机制；native payload校验；code-first及默认异常阻断UnityEvent保持。默认清代码不动Inspector；显式`UnRegisterAll(true)`仍把20个UnityEvent字段置null、断开运行时persistent通道，不改资产、不清外部持有的旧UnityEvent对象，不能以RemoveAllListeners替代 |

共享token并不能替代View/Audio等领域代次。例如普通事件快照里旧回调仍可能被进入，领域wrapper必须验证当前owner/Session才执行业务。View按typed定义保存订阅工厂，激活时创建本轮guard+token，不通过DynamicInvoke处理0～7参；部分注册失败只取消本次成功取得的tokens，不能按委托扫描误删其他View或重入后新轮。Stop先摘本轮token集合再逐项取消，后续hook重入产生的新轮保持独立。只在owner-reset时，通用channel额外阻止旧通知继续。

需要留后的实证问题：`Resource/Framework/Res/Res.cs:95–97`在完成Invoke返回后才清回调，异常可跳过清空，回调中新增项又可能被之后的清空删除；ReleaseAllRes部分失败、Audio播放租约等均不由通用Events自动修复。它们不阻断本方案，只作为对应专题的可靠源码风险保留，不以Pool通过证明完成通知安全。

## 6. 程序集、公开 API 与命名迁移

### 6.1 依赖约束

`Y/Shared/YLFramework.Foundation.Runtime.asmdef` 当前 references 为 `[]`，允许 UnityEngine，无 Runtime/Core 依赖；`Shared/Internal/AssemblyInfo.cs:3–4` friend 仅 Runtime/Core。Pool 的 `ObjectPool.cs:139–144` 已明确不提供租用代次，Events 自己承担订阅 incarnation，不改 Pool 算法。

`CoreIdentityTests.cs:62–70` 明确断言 Core 不引用 `YLFramework.Runtime` 或原 `XFGameFramework`，仍引用 Foundation/XFAB/UI。因此把 Core 直接接到现有 Runtime EasyEvent 是不可接受的捷径。迁移 IUnRegister 等共享公开类型后，实际使用该类型的 Runtime/Core/Tests/Editor/DOTween 程序集按需加直接 Foundation 引用，不能把间接 references 当 C# 类型可达的保证；不批量给无使用者加依赖。

移动已有脚本保留各自 meta/GUID；新增 channel 文件由 Unity 生成新身份。对真实共享类型做定向 SerializeReference、完整类型名、反射、UnityEvent、场景/Prefab 检索；不靠保留 GUID 假装跨程序集类型兼容。未找到真实持久化依赖时，不加空兼容壳/MovedFrom 模板。实施时同步已有 Events 模块说明与必要 API XML，不新建过程报告。

### 6.2 正式入口与退出

| 现有入口 | 推荐结果 | 迁移注意 |
|---|---|---|
| Core EventManager | 保留 Core 薄入口和 0～7 参 Add/Remove/Trigger 易用性，转接唯一共享 bus；Add 返回精确 token | 忽略返回值的普通调用可继续；View 接收/持有 token；显式方法组、反射和测试需定向适配 |
| `XFEvent` 0～7 | 替换为 System.Action 0～7，退出自有来源前缀 delegate 定义 | 修改 View 声明及真实消费者，不在原 XF 命名空间创建兼容别名，不机械替换原包/游戏 |
| String/Type/Enum/IntIDEventSystem | 保留有价值facade；各自组合唯一EventBus实现的独立实例，不保留第二套广播算法 | 原Singleton实例Dispose只关闭自己的bus和监听；其他系统及Core不受影响。IntID的`int + object[]`不会与Core同数值的typed int键签名冲突 |
| EasyEvent / IEasyEvent | Runtime 的池化/生命周期适配，共享纯 channel | 不搬 Safe/池接口，不改变 Binding owned event 存活；直接注册/Once/观察/数目等能力必须保留 |
| EasyEvents | 独立事件定义类型容器 | Remove/Clear 是否终结旧事件应明确，不把容器移除映射自动等同对象归池；池化实例仍由真实 owner 归还 |
| OrEvent | 唯一纯组合实现 + 必要 Runtime 接口适配 | 末 listener 注销可再注册并重连；Dispose 不可复活；旧 token 不可影响新轮 |
| 旧 object[] Core API、hash 字典/反查 | 随新实现退出 | 已编译禁止的旧 API 不恢复；诊断用完整键，无需长期留两套字典 |

facade的系统Dispose不是owner-scope清理，业务不要用来替代token；本推荐通过独立实例保持这一边界。Core EventManager的显式全局清理若保留，名称/文档须明确影响整个Default，严禁Module/View自动调用。若采用共享registry备选，必须另外决定facade是否只管理其注册scope、整键清理会否跨入口生效及IntID协议的独立域；不能用“只清string域”假装没有取消Core的string订阅。该额外所有权复杂度是本轮不推荐默认合并registry的原因。

### 6.3 `XFGameFrameworkEvents` 的处理

- 本地类型是 `YLFramework.Core.XFGameFrameworkEvents`，普通非 Serializable、非 UnityEngine.Object 的 const 容器；唯一能力为 `ON_REMOVE_VIEW = "ON_REMOVE_VIEW"`。原包有独立的 `XFGameFramework.XFGameFrameworkEvents`，不可全局替换。
- 本地实用引用为 `ViewManager.cs:208` 和 `CoreDirectViewPlayModeTests` 六处增删订阅。原游戏未找到该类型/键文本引用；本地脚本 GUID `400a6633333b837428dfaf762f92a43c` 在 Assets 定向检索仅命中自身 meta，未找到资产/反射字符串引用。该结果限定当前本地检索范围，不宣称所有外部分发者不存在。
- 推荐改为 `YLFramework.Core.CoreEvents`，文件及自身meta一同改名；保留程序集、namespace、普通class形状、`ON_REMOVE_VIEW`成员与字符串值、`Action<View>`消息负载和发布时机。本专题不为常量容器顺手添加static限制；不新增无实际用途的`YLGameFrameworkEvents`前缀替代品。
- 不为未发布框架的这一常量容器长期留旧名 alias。实施前重新核对本地完整类型名、GUID、字符串绑定；若发现真实外部或保存数据，再单独制定最小迁移。
- 不顺便把该通知改成带新字段的 DTO，也不删除通知：它具有通用观察价值，只是不负责 Core 必要状态维护。其他专题 XF 命名留对应阶段。

## 7. 实施批次与最小验收

以下是在总体设计获确认且实施另行授权后的顺序，不表示已获实施授权。批次允许按实际差异小幅调整，不能把公共机制和所有领域重构压成一个不可回退的大批。

| 批次 | 单一主轴/范围 | 难度与风险 | 独立验收方向 |
|---|---|---|---|
| A 共享事件基础 | Foundation 纯 channel 0～7、IEventSource/精确 token、Once/COW/owner incarnation、完整键与签名 bus、必要纯取消/Or；保留旧消费者直到下一批迁入 | 高：快照、multicast、Once、旧 token、异常/嵌套与组取消；初期两实现只允许作为迁移过渡 | Foundation 直接契约测试，复用/迁移现有 Events 的相应断言；0/1/3/7 参代表性类型路径，signature/no-op/noncreating、独立 bus/Default 与 session，不生成参数笛卡尔积 |
| B YL 消费者收敛 | EasyEvent/Global/EasyEvents/Or转接唯一机制，Binding owned reset/Once必要适配；取消Runtime旧分发算法 | 高：池化旧token、readonly owned event、facade独立清理范围、Audio/Action重入保护 | EventsContract、BindingContract相关重要契约；Action全局消息直接测试，Audio settings/mixer关键回归；Observable动作未改变其订阅支撑则不机械重跑。增加一个facade Dispose不影响Core/其他bus的隔离断言 |
| C Core 接入 | EventManager薄化、Action 0～7签名、View完整key与本轮guard/token、移除广播显式异常模式；退出Core hash/Delegate字典 | 中高：View/Panel Session生命周期不能回退、签名错误进入现有回滚、全局隔离 | 复用CoreDirectView启停/缓存、Loaded/队列重入、Close观察者异常、同组件新Session；补旧bus快照不能进入复用后View及部分注册回滚不误取消他者；CoreIdentity依赖/唯一归属，命名资源集成仅选受影响路径 |
| D Unity 生命周期适配 | 批量取消异常继续/失败责任、精确Scene与明确AnyScene、trigger销毁收尾、EventListener原生payload校验；Core领域组件保持身份 | 中高：Additive精确范围、inactive限制、Unity序列化/Inspector | EventsLifecyclePlayMode复用及必要补例；两个Additive Scene只取消指定owner、scene迁移/trigger销毁、Disable再Enable重订、payload错误、默认保留Inspector与显式true断开；不建立全游戏生命周期模拟设施 |
| E 命名与专题收口 | CoreEvents改名、退出确认冗余类型/API/存储，更新现有文档和测试入口，定向资源/反射/asmdef/meta/许可复核 | 中：类型身份和公开API退出；不能借命名改事件payload或外围系统 | 有代表性的Events/Binding/Core/Action/Audio跨模块回归；关闭Domain Reload两轮Play及旧token；按真实风险选择原游戏音频非干扰。只有一套通用算法/registry实现，不要求所有上下文只有一个实例，领域能力仍可用 |

### 必须补齐的关键情景

1. 同委托注册A(h1)→B→A(h2)，取消h1后次轮必须B→A，而非A→B；现有只计次数的测试不能证明身份。最小旧token回归无需池：Register(A)→ClearListeners→Register(A)→旧token取消不能影响新A。正常snapshot仍执行已捕获项；multicast组和legacy delegate取消各补一个代表情景，不堆组合矩阵。
2. EasyEvent回收/再租同实例、整键移除/重建、Binding同实例复用、Or正常重连后旧token无效；事件在回调中owner-reset后旧快照停止且不会操作新轮；View普通bus快照前序关闭/复用后，后序旧Session callback被guard拒绝。原始旧对象别名仍属禁止使用，不建立无法实现的测试承诺。
3. 前序监听受控嵌套 Trigger，Once 内层先消耗，外层不再执行；Once 自身重入/抛错仍最多一次；Binding 注册一次通知同样覆盖。继续使用小型现有测试结构，不添加通用重入测试框架。
4. 发布中新增/取消/普通清空、嵌套发布、普通回调抛错后后续发布可用；默认 fail-fast 断言不削弱，Core 显式继续模式另有断言。
5. 一个 custom token 取消抛错，后续仍取消，新加入项/失败项保留且汇总错误；重入同一取消不重复执行。销毁清理不伪装全部成功。
6. string/int/enum/type 键域独立、完整string比较、签名冲突原子拒绝、明确清键后允许重绑；无需寻找hash碰撞或全量随机键压力来证明不再按hash身份。
7. Or无listener时不连接sources，立即回调/部分连接失败不留订阅，末listener释放、正常重新连接、Dispose终结截断；追加source/重复source能力保留，旧source快照不误转发新轮。source描述仍被Or持有到Dispose，不把“不连接”误称弱引用或不保活对象；EventListener错误原生类型首次即拒绝且正确注册仍可用。旧Global.Dispose不能摘新Instance入口。

测试依据与复用入口：`T/EditMode/EventsContractTests.cs`、`BindingContractTests.cs`；`T/PlayMode/EventsLifecyclePlayModeTests.cs`；CoreDirectView/必要 CoreXfab 的事件与队列相关用例、CoreIdentity；Action 消息/Observable/生命周期套件；AudioMixer/设置测试。实施时按真实方法筛选，不凭模块文档中的历史类名构造“已存在测试”，不机械重跑 Pool 数百项测试或所有 Core 历史用例。

每批运行对应版本 Unity 实际编译和必要定向 EditMode/PlayMode，分别报告；检查实际 diff、meta和程序集。Player/IL2CPP/AOT不由本轮研究证明：强类型无反射分发降低新增AOT风险，但未来目标平台验证仍按阶段门禁与泛型变化安排，不默认每批全平台构建。没有改游戏加载/资产/构建链时不逐批全游戏或Windows Player；Audio的间接变化达到风险条件时复用已有代表性非干扰检查。

## 8. 重大决策：已批准方向与备选记录

| 决策 | 推荐 | 可行备选与代价 |
|---|---|---|
| 公共归属 | 现有Foundation承接纯机制；Runtime池化/Unity与Core领域适配向下依赖 | 移整个Events/Safe/Singleton闭包会扩大公共层；新建Events程序集增加管理成本；Core直接引用Runtime违反现有边界 |
| 全局收敛幅度 | 统一算法和正式EventBus API；Core使用Default，YL历史facade保留独立上下文/Dispose范围；新业务显式选同一bus才能互通 | 全部共享Default可减少命名空间，但同数值IntID/Core协议冲突、facade.Dispose扩大清理范围，需要新的scope/协议域规则或主动退出这些旧API。不能只修改字典就称融合完成；只有明确接受API/所有权变化才采用 |
| 异常 | 普通fail-fast；必要清理逐项汇总；Core移除通知显式continue-and-aggregate | 所有广播逐观察者继续便于通知但改变现有测试与Binding/Action契约；仅原样fail-fast最少改动但不能修清理丢责任，也不利多观察者的Core通知 |
| 参数/键 | 保留0～7 Action便利API、完整键/精确签名与类型消息；不保留自动hash互通 | 只留DTO/type bus可更少重载，但无必要牺牲XF多参数/命名动态能力；按签名拆键会隐藏调用错误，不推荐 |
| owner结束与snapshot | 普通取消保持snapshot，reset/回收/Dispose截断旧代次；Binding owned reset单独表达 | 全部操作仍跑完旧snapshot改动少，但保留回收后进入旧owner的风险；全部取消都立即跳过当前snapshot则改变正常广播太多 |
| 历史API/命名 | XFEvent→Action；XFGameFrameworkEvents→CoreEvents；仅真实依赖保留限时适配 | 全套旧API永久兼容维护成本高且保留误导；一次清理所有XF前缀超出范围。共享类型程序集移动仍须定向核查 |

以上推荐方向已于 2026-10-09 获用户总体审阅确认：统一公共事件机制并保留有明确所有权价值的独立事件上下文（不强制所有历史入口共享 Default）；普通广播保持既有 fail-fast，必要清理逐项尝试并报告，Core 移除通知采用明确的继续通知与错误汇总；普通退订保留既有快照，owner 生命周期结束不得让旧代次继续进入已结束或复用的业务；保留双方有价值的参数、类型、命名、组合及生命周期能力，并按本方案处理 Events 范围内历史 API 与 XF 命名；按 A～E 顺序分批推进。确认不要求先决定每个私有字段或类层级，上表“可行备选与代价”保留为审阅历史；实施中若需偏离已批准方向，先说明并等待确认，不重启 Pool/Core 审计。

## 9. 审批记录、设计门禁与交付结论

- 已具备：双方真实能力对照、可靠源码问题分类、唯一机制/领域适配边界、依赖方向、owner与token契约、API迁移、分批与最小验证方向。
- 已确认（2026-10-09）：总体设计及第8节推荐方向；确认范围为架构方向、行为契约与阶段边界，不等同于批次实施授权，也不要求逐条照搬私有结构、算法或候选方法名。
- 待授权：2.2-A 及后续每批的实施、专项验证与 Git 收口均需单独授权；重大行为、所有权或范围变化先说明并等待确认。
- 实施时需再查：具体移动类型的资产/反射、Global facade真正键域归属及直接asmdef引用；泛型无参观察已确认在同一typed序列，按第4节保持。发现真实外部持久化依赖时按该点暂停迁移，不猜测兼容方案。
- 重要兼容变化保留：owner reset/回收/Dispose（含 Or.Dispose）截断旧在途快照、Global 旧实例不得摘除新 Instance 入口、键签名校验与 no-op 细节、批量清理异常汇总、历史 API 与 XF 命名退出等，均为有意的行为/API 变化，须按第7节在相应批次验收并在必要时同步 XML 文档。
- 未验证：Unity编译、运行复现、所有平台/AOT、性能、实际无Domain Reload切换；历史Pool/Core验收不构成新Events通过证明。本次状态更新同样未执行任何编译、测试或运行验证。
- 非本专题：原XF/游戏改造、Update宿主重写、Timer调度、Audio播放/Mixer重构、Resource租约/AB释放、全局弱订阅或后台事件平台。

结论：**总体设计已批准，待分批实施。尚未实施、尚未运行验证；等待单独授权 2.2-A，当前不宣布 2.2 已实现或已验收。**
