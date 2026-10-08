# 阶段 1.2：Module / Model / Controller 正式契约

本文是 1.2 唯一正式方案，保存已确认目标、已实施契约及 A～F 边界，不保存研究过程或逐次测试日志。长期目标与状态入口见 [IntegrationPlan.md](../../Assets/YLFramework/Documentation/IntegrationPlan.md)；1.1 归属基线见 [承接方案](1791455404620-xf-yl-core-ownership.md)。本文件不授权自动实施、提交或进入下一批次。

## 1. 依据与实施状态

- 原 1.2 设计来自“YL Core Module/Model/Controller 融合设计研究”会话（`ses_ee39a09bdffeGweJ61c0aba4Mw`）；后续 A/B 实施任务明确说明架构已确认，批次细节以更晚的授权及验收反馈为准，不将研究中的条件性建议自动升级为决定。
- A 已实施、验收、提交（`8ff1ea6`）；B 已实施、审阅通过、提交（`e39999e`），依据为本轮用户反馈、B 完成报告及 Git 核对。
- C 已实施、验收并提交（`60d0465`）；D 已实施、通过专项验证并提交（`8c3fff6`，补充回归 `750b32e`）；E 已实施、通过专项验证并实际提交（`10f7a79`）。F 已完成定向契约收口及本轮技术验收；1.2 实施及专项验证完成，待用户最终确认及 Git 收口，未自动进入后续阶段。具体验证摘要只在长期蓝图维护，不在本文件重复测试日志。
- 初始化重入口径在原设计与 B 实现之间有实质细化，见第 4 节；C 的历史失败状态已由 E 的自动逻辑回滚替代，见第 6 节。

## 2. 职责与所有权

| 对象 | 正式职责 | 不承担的职责 |
|---|---|---|
| Module | MVC 业务对象的唯一所有者、索引容器及生命周期边界，声明启动配置和模块级钩子 | 不代替通用 DI 容器或 Unity Root，不自动结束外部 FSM、全局计时、音频及借用服务 |
| Model | 本模块业务状态与数据规则，支持按实际类型及 ID 区分多实例 | 不默认全局单例，不隐式池化 |
| Controller | 协调本模块业务流程；每模块每实际类型至多一个注册实例 | 不等同 IOC 或所有服务 Manager，不隐式跨模块唯一 |
| ModuleManager | 模块注册、状态、启动调度及唯一关闭/Shutdown 管线的权威 | 不建立第二套 M/C 所有权索引，不通过 IOC 查询回退 |
| ModelManager / ControllerManager | 操作 Module 本地索引的实现辅助 | 不是独立业务所有者或全局实例容器 |

同一 Model/Controller 实例同时只能属于一个 Module。跨模块获取是显式借用，不改变 owner，不向借用者重复注册，不因借用者关闭而退出；拥有者结束后，借用者不能继续假定对象有效。不引入通用 Scope、依赖图、借用代理或引用计数体系。

IOC 只保留可选装配职责，不自动 Resolve 缺失 MVC；普通 Singleton 与 Root 不接管模块生命周期。不为复用这些能力新增 Core 对现有 `YLFramework.Runtime` 的整体依赖，不修改原游戏或现有 YL Audio 路径。

## 3. 查询、注册与 ID（A/B 已实现）

- `GetModel`、`GetController` 只查本模块已注册对象，不创建，缺失返回 `null`，不隐式搜索其他 Module 或 BaseModule。
- 单 Model 查询按精确类型匹配；无 ID 查询只允许本地唯一对象，多实例明确抛错。`GetModels` 保留该类型及派生类型集合查询，返回结果集合而非可修改的权威索引。
- `LoadModel`、`LoadController` 是本地 Get-or-create；`CreateController` 是新建本地注册对象，已有同类型时在构造前拒绝。泛型、Type 及适用的完整类型名入口保持一致。
- 注册统一遵守“校验 → 临时索引及 owner → OnInit → 完成”；临时可见不等于初始化完成。已完成注册的同实例 Add 幂等；跨 owner、冲突键、生命周期期间重复 Add 明确拒绝。
- 初始化失败尝试对象级 OnExit，撤销本次索引及 owner；清理错误附在原异常的 `Data["YLFramework.Core.CleanupException"]`，不覆盖原始失败。E 补充保留该位置已有异常，多个清理异常以 `AggregateException` 关联。只回滚该对象，不承诺撤销用户钩子的任意外部副作用。
- 移除核对 owner 与实际索引实例；错误实例、跨模块借用及重复移除不误退出其他对象。OnExit 期间保留必要的 owner 与查询可见性；递归移除不重复退出，即使抛错也最终解绑。每次有效注册最多对应一次退出尝试。
- Model ID 及查询参数统一为 `long`。保留 public setter 供注册前业务/JSON 赋值，注册期间锁定身份；`-1` 表示由框架分配。自动 ID 由 Module 生成并避开该实际类型的现有注册键，不依赖 GetHashCode，不保证跨运行持久化。初始化失败恢复注册前 ID。
- Model 默认创建和移除不再使用 ClassPool；Controller 普通创建。不改变 View/GameObject/Audio 池、通用 Pool 或 XFAB 缓存。

## 4. 初始化重入：原目标与 B 的实际细化

原设计同时允许初始化钩子 Get 自身，并表述“初始化中的同类型递归 Load/Create 应明确拒绝”。B 没有将这句话原样落实为一律拒绝 Load，而是区分构造预留和已建立临时注册的 OnInit：

| 时点与操作 | B 实际契约 |
|---|---|
| 构造函数中再次 Load/Create 同类型，尚未建立临时索引 | 拒绝递归构造，释放构造预留后可再次正常创建 |
| OnInit 中 Get 自身 | 返回临时注册实例，不承诺它已初始化完成 |
| OnInit 中 Load 同一已索引对象 | 返回临时注册的自身，不重新构造或再次 OnInit；Model 的无 ID 多实例歧义规则仍适用 |
| OnInit 中 CreateController 同类型 | 构造前拒绝；不能返回第二个未注册对象 |
| OnInit 中重复 Add 自身、占用同键或移除自身 | 拒绝 |
| OnExit 中 Get / Load / 递归移除自身 | Get 保持必要可见性；Load 拒绝；递归移除不再次退出 |

依据：B 实施任务允许“受控临时索引、初始化期间必要可见性”，要求避免无限递归和重复构造；B 实施报告明确说明 Get/Load 可访问自身，用户反馈 B 已审阅通过。源码 `ModelManager.LoadModel`、`ControllerManager.LoadController` 返回已有非退出对象；现有 `InitializationCanQueryAndLoadTemporarySelfButCannotReregisterOrRemoveIt` 与 `ConstructorReentryRejectsBeforeAnotherConstructionAndReleasesReservedKeys` 测试固定该区别，F 本轮已实际重新执行并通过。

未找到针对“OnInit 递归 Load 从一律拒绝放宽为返回自身”的单独逐条批准记录；当前依据是后续 B 指令、公开报告及批次审阅通过，不把两种原文解释为完全一致，F 保持已确认的实际契约，不回改代码或重新选择设计。

## 5. Module 启动与身份（C 已实现并验收）

- 每种具体 Module 类型同时至多一个有效注册实例。相同实例注册幂等；同类型不同实例明确拒绝，拒绝者不能 OnStart、成为 CurrentModule 或关闭实际注册的另一实例。
- Starting 同实例重复启动取得同一有效进行中请求；Running 重复启动得到成功结果，不重复 OnStart。按类型启动先查询已有实例，不先构造第二个对象。
- 启动记录先于可能同步执行的协程及回调建立，Core 自行去重，不依赖 XFAB 执行请求缓存。当前请求不池化，成功和失败后移除进行中记录；Running 返回新建的已完成成功请求，而非缓存旧请求。
- 先提交生命周期结果，再调用完成回调，订阅者异常不反向改写启动结果。未改造 Foundation 的整条通知链，不承诺逐订阅者异常隔离。
- 正常状态为 `Created → Starting → Running → Closing → Closed`。E 使 `StartFailed` 仅为失败回滚的内部过渡状态，已注册失败实例经 `Closing` 到 `Closed` 后才通知；`CloseFailed` 仍保留但不进入。Starting 注册可见但不是 Running，普通 Close 明确拒绝；StartUpModuleRequest 在任何完成状态修改前拒绝 Abort，Abort 不构成取消，也不提供取消系统。
- 注册表暴露缓存的只读实时视图；ModuleManager 保持注册、请求及实例身份一致。SingletonModule.Instance 只查询实际注册实例，无第二份可写缓存，不依赖 base.OnStart；解除注册后不返回旧实例。
- CurrentModule 仍是 View/资源的过渡上下文；拒绝或重复启动不污染它，失败仅在 current 仍指向自身时恢复此前有效上下文，不覆盖其他已成功模块；嵌套启动的执行上下文有作用域。未全面设计并发场景归属或替换 View/资源系统。
- Module 默认创建、关闭不再隐式使用 ClassPool。仅正确清理且核心索引无对象残留的实例可显式重启；不承诺重置全部业务字段。
- C 验收时曾有 StartFailed 保留注册和子对象、CloseFailed 重试关闭及旧 OnExit/Exit 双路径。D 已替换关闭行为，E 已补上自动失败逻辑回滚，当前契约见第 6 节；历史状态不是当前启动失败契约。

## 6. 关闭与启动失败（D/E 已实施）

- D 已建立唯一关闭清理管线，移除 public `Module.OnExit()` 旧入口；`OnClose` 是业务对象清理前钩子，无需调用 base。进入 Closing 后禁止 Add、缺失对象的 Load/Create 和 View/Panel 加载，已有未退出对象仍可查询/Load；Closed 拒绝新增，须先显式重启。顺序为 OnClose → Controller → 现有 View → 剩余 Model，View 对本地 Model 的既有移除联动保留，再清除核心索引/owner、注销并进入 Closed。
- 每个 owned 对象独立尝试清理，单一异常不阻断兄弟对象；在 Closed/注销后以 `AggregateException` 汇总清理错误，保留原异常实例。重复/递归 Close 不重复通知。`CloseFailed` 枚举保留，但本管线不再进入该状态。Closed 表示逻辑所有权结束，不等于所有外部资源都释放成功，也不承诺重置业务字段。
- 新公开入口为 `public static void ModuleManager.Shutdown()`：普通模块先于基础模块，各组逆注册顺序处理，继续清理并汇总错误，不引入自动拓扑；顺序索引按引用身份维护，包含当前部分注册失败的实例。基础模块普通 Close 仍拒绝。Starting 不提供取消，Shutdown 在任何模块 Starting 或 M/C 构造、初始化、退出事务中预先拒绝，不做部分 Shutdown；普通 Close 也保护这些对象事务。普通关闭期间不能嵌套开始 Shutdown，Shutdown 内递归 Shutdown 幂等，期间禁止模块启动/注册及跨模块普通 Close，避免钩子破坏顺序。
- 关闭事务保留初始 View 集合以覆盖钩子提前移除索引的情况，最终仅解绑仍归当前模块的 View，不清掉其他模块重新加载后的新 owner。View 清理异常后仅保证逻辑 owner/index 结束，View 自身缓存、资源、事件等异常收尾留给 1.3。
- E 将配表请求为空、已报告错误、资产为空或类型不符、空白文本、JSON/结构错误、Model 构造/初始化、默认场景已报告错误或无有效已加载 Scene、OnStart 异常作为启动失败。配表要求一维 Model 数组类型，JSON `[]` 合法，空文本或 JSON `null` 不作为空配置；额外 JSON 内容拒绝。未声明配表（null/空字典）仍合法，不增加可选表体系。保持原有先配表、默认场景、再 OnStart 的顺序。
- 已取得注册的失败实例，包括 BaseModule 和注册阶段部分失败，复用 D 的唯一关闭管线回滚；回滚不要求 Running，不再次调用已经退出的 M/C。先清理 MVC/owner/索引、注销、进入 Closed、移除本次启动记录及恢复有效 CurrentModule，再发布最终失败。嵌套成功模块和借用对象不回滚；正确清理后可显式重启同实例或同类型新实例，Singleton 查询不再返回失败旧实例。不保证重置任意业务字段。
- 身份冲突、非法类型和构造失败在取得本次注册前直接拒绝，不清理实际拥有者，也不假造 OnClose；请求建立后若未取得注册则恢复启动前状态。新增启动也不能与本模块尚未结束的 M/C 构造、初始化或退出事务交叉，此前置拒绝不影响 Starting/Running 的去重或幂等入口。
- 新公开只读结果 `StartUpModuleRequest.Exception` 保留首次启动异常实例，额外资源/对象/模块清理错误关联到其 `Data["YLFramework.Core.CleanupException"]`，已有异常不被覆盖，多个错误聚合；`error` 字符串同时包含根因和额外清理异常。内部状态先提交，完成订阅者异常仅按既有机制记录，不把成功结果改成失败。Foundation 完成、late callback 和进度规则未改，不承诺逐订阅者隔离。
- `Module.ProjectName` 保留 `public virtual string { get; set; }`，默认类型短名，可在构造/对象初始化时赋值或通过重写指定不同 XFAB 项目名；启动请求在首次加载前捕获非空白名称，不再由 OnStart 强制覆盖。ModuleName/具体类型仍为模块注册身份。基础模块加载不读取其他 CurrentModule 的项目名，异步 View 基础模块回退使用被遍历 owner 的 `ProjectName`；后端缺失 Prefab 挂起时仍不能到达回退。
- 临时启动配表与长期持有的 View/场景资源分开：只将已取得的配表资产在复制文本后以局部 finally 调用 `UnloadAsset` 归还，再进行 JSON 构造和 Model 初始化；null 资产不卸载，成功、空数组、空文本及异常均不漏掉取得的资产，卸载异常使启动失败且不覆盖已有根因。默认成功场景不是临时配表，不在此自动卸载。
- E 批次用户明确确认、F 沿用 **专用启动配表前置条件**：配表及其 Bundle 在取得/复制文本期间不存在其他调用者的持有或并发加载。实际 XFAB 没有按调用计数的租约，共享请求和对象缓存不能证明独占；本实现不提供任意共享配表的安全自动释放，也不建立新资源管理层。不得把缓存快照视为引用计数。Assets 模式通常不建立 AB 运行缓存，归还 API 不等于物理卸载；真实 AB 释放与并发共享仍未验证。
- 回滚不是 Unity 世界事务：不自动恢复 Single 已卸载场景，不撤销任意用户外部副作用，也不修补原 XFAB 的挂起路径。View 自身异常收尾、缓存及场景归属的全面契约留给 1.3。

## 7. A～F 边界与门禁

| 批次 | 目标与前置 | 实施边界与当前状态 |
|---|---|---|
| A 查询与共享 | 本地 Get、显式 owner、Load/Create 分流 | 已验收提交；不重建注册事务、Module 生命周期或 View/资源查找 |
| B 注册完整性 | A 稳定；单 owner、临时注册、精确移除、long ID、对象级异常安全、Model 非池化 | 已实施审阅通过并提交；不保证多对象 Module 关闭继续清理或模块启动整体回滚 |
| C 唯一注册与启动 | B 稳定；Module 状态、请求去重与重入、只读注册表、Module 非池化、Singleton 身份及最小 Abort 防护 | 已验收并提交（`60d0465`）；C 当时仅提供 D/E 的状态和最低限度清理支撑，后续 D/E 已完成完整专项实现 |
| D 关闭与 Shutdown | C 身份及状态稳定 | 已实施、通过专项验证并提交（`8c3fff6`），用户验收状态未据本轮推定；唯一管线、OnClose 顺序、逐对象异常继续清理、基础模块 Shutdown |
| E 启动失败与资源前置 | B/C/D 稳定 | 已实施、通过专项验证并提交（`10f7a79`）；逻辑回滚、错误传播、ProjectName 提前确定、专用启动表复制后成对归还；后端挂起与真实 AB 物理释放验证边界保留 |
| F 契约收口 | A～E 实施及专项验证、实际提交已核实 | 已完成定向 API/直接调用方、Editor/fixture 核查及必要修正，技术门禁通过，待用户最终确认及 Git 收口；不扩展后续系统 |

F 最终收口结论：第 3～6 节的 A～E 实际契约一致。泛型/Type/适用字符串入口共用本地索引与生命周期管线；YL 直接调用方及生成模板无错误的 Module.OnExit、旧 int Model ID 或隐式跨模块 MVC 参数引用。`StartFailed` 仅为回滚过渡，`CloseFailed` 与已过时的 `ControllerType` 不参与当前关闭/所有权判定，本轮不为形式清理删除保留 API。Module/Model 非池化不扩展为取消 View、参数容器或通用 Pool 的缓存。

唯一实质修正位于测试清理：`CoreClassPoolScope.Restore` 不再反射删除 Module 注册表或 current；调用方须先通过正常关闭/回滚结束自有模块，Restore 只读检查自有注册、启动记录及注册顺序残留，残留明确失败，不掩盖 owner/状态未结束的问题。新增回归验证保留仍注册对象及其他模块，关闭后才能恢复；既有池/参数恢复继续保持局部所有权。Editor 仅校正基础模块须由 Shutdown 结束的提示，并补生成模板不回退到旧入口的断言；View.Model XML 明确显式借用不会随 View 移除。未修改运行时生命周期逻辑、程序集、资源身份、原 Package 或游戏。

F 已实际执行 Unity 编译状态及新增回归加载检查、核心查询/注册/启动/关闭/失败回滚、最小直接 View/Panel、LOCAL + Assets 的 JSON/场景闭环及直接生成器测试，执行数量和结果见长期蓝图第 6 节，历史门禁不冒充本轮运行。1.2 技术验收条件满足，正式 COMPLETE 待用户最终确认及 Git 收口；1.3 核心技术前置条件满足，但未开始。临时 XFAB、专用启动表前置条件、后端挂起、AB 物理卸载/并发共享及 View 自身异常收尾边界仍按第 6 节保留；原 LoggerManager 退出残留不据此认定已解决，完整游戏非干扰与 Player 留给 1.4。

每批优先复用 Core 测试并补关键契约，执行 Unity 编译和直接受影响的专项回归；涉及 Foundation 时做最小相关验证。完整游戏、Audio、全量 Editor、Foundation 或 Windows Player 不作为逐批机械门槛；1.4 的完整核心与原游戏非干扰门禁仍保留。原 XF、XFAB PackageCache、游戏业务、既有 YL Audio、普通 IOC/Singleton/Root 均不在 1.2 默认修改范围。同工程专项通过不等于最终独立验收。
