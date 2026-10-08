# 阶段 1.2：Module / Model / Controller 正式契约

本文是 1.2 唯一正式方案，保存已确认目标、已实施契约及 A～F 边界，不保存研究过程或逐次测试日志。长期目标与状态入口见 [IntegrationPlan.md](../../Assets/YLFramework/Documentation/IntegrationPlan.md)；1.1 归属基线见 [承接方案](1791455404620-xf-yl-core-ownership.md)。本文件不授权自动实施、提交或进入下一批次。

## 1. 依据与实施状态

- 原 1.2 设计来自“YL Core Module/Model/Controller 融合设计研究”会话（`ses_ee39a09bdffeGweJ61c0aba4Mw`）；后续 A/B 实施任务明确说明架构已确认，批次细节以更晚的授权及验收反馈为准，不将研究中的条件性建议自动升级为决定。
- A 已实施、验收、提交（`8ff1ea6`）；B 已实施、审阅通过、提交（`e39999e`），依据为本轮用户反馈、B 完成报告及 Git 核对。
- C 已在本会话交付实施完成报告及专项验证结果，待用户验收、未提交。D/E/F 未实施。具体状态及验证摘要只在长期蓝图维护，不在本文件重复测试日志。
- 初始化重入口径在原设计与 B 实现之间有实质细化，见第 4 节；C 的失败状态是未完整回滚的实际状态，不替代 E 的目标契约。

## 2. 职责与所有权

| 对象 | 正式职责 | 不承担的职责 |
|---|---|---|
| Module | MVC 业务对象的唯一所有者、索引容器及生命周期边界，声明启动配置和模块级钩子 | 不代替通用 DI 容器或 Unity Root，不自动结束外部 FSM、全局计时、音频及借用服务 |
| Model | 本模块业务状态与数据规则，支持按实际类型及 ID 区分多实例 | 不默认全局单例，不隐式池化 |
| Controller | 协调本模块业务流程；每模块每实际类型至多一个注册实例 | 不等同 IOC 或所有服务 Manager，不隐式跨模块唯一 |
| ModuleManager | 模块注册、状态与启动调度的唯一权威；关闭及 Shutdown 在 D 完善 | 不建立第二套 M/C 所有权索引，不通过 IOC 查询回退 |
| ModelManager / ControllerManager | 操作 Module 本地索引的实现辅助 | 不是独立业务所有者或全局实例容器 |

同一 Model/Controller 实例同时只能属于一个 Module。跨模块获取是显式借用，不改变 owner，不向借用者重复注册，不因借用者关闭而退出；拥有者结束后，借用者不能继续假定对象有效。不引入通用 Scope、依赖图、借用代理或引用计数体系。

IOC 只保留可选装配职责，不自动 Resolve 缺失 MVC；普通 Singleton 与 Root 不接管模块生命周期。不为复用这些能力新增 Core 对现有 `YLFramework.Runtime` 的整体依赖，不修改原游戏或现有 YL Audio 路径。

## 3. 查询、注册与 ID（A/B 已实现）

- `GetModel`、`GetController` 只查本模块已注册对象，不创建，缺失返回 `null`，不隐式搜索其他 Module 或 BaseModule。
- 单 Model 查询按精确类型匹配；无 ID 查询只允许本地唯一对象，多实例明确抛错。`GetModels` 保留该类型及派生类型集合查询，返回结果集合而非可修改的权威索引。
- `LoadModel`、`LoadController` 是本地 Get-or-create；`CreateController` 是新建本地注册对象，已有同类型时在构造前拒绝。泛型、Type 及适用的完整类型名入口保持一致。
- 注册统一遵守“校验 → 临时索引及 owner → OnInit → 完成”；临时可见不等于初始化完成。已完成注册的同实例 Add 幂等；跨 owner、冲突键、生命周期期间重复 Add 明确拒绝。
- 初始化失败尝试对象级 OnExit，撤销本次索引及 owner；清理错误附在原异常的 `Data["YLFramework.Core.CleanupException"]`，不覆盖原始失败。只回滚该对象，不承诺撤销用户钩子的任意外部副作用。
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

依据：B 实施任务允许“受控临时索引、初始化期间必要可见性”，要求避免无限递归和重复构造；B 实施报告明确说明 Get/Load 可访问自身，本轮用户反馈 B 已审阅通过。源码 `ModelManager.LoadModel`、`ControllerManager.LoadController` 返回已有非退出对象；现有 `InitializationCanQueryAndLoadTemporarySelfButCannotReregisterOrRemoveIt` 与 `ConstructorReentryRejectsBeforeAnotherConstructionAndReleasesReservedKeys` 测试固定该区别。本次仅定向读取，未重新执行测试。

未找到针对“OnInit 递归 Load 从一律拒绝放宽为返回自身”的单独逐条批准记录；当前依据是后续 B 指令、公开报告及批次审阅通过，不把两种原文解释为完全一致，也不在本次文档维护中回改代码或重新选择设计。

## 5. Module 启动与身份（C 已实现，待验收）

- 每种具体 Module 类型同时至多一个有效注册实例。相同实例注册幂等；同类型不同实例明确拒绝，拒绝者不能 OnStart、成为 CurrentModule 或关闭实际注册的另一实例。
- Starting 同实例重复启动取得同一有效进行中请求；Running 重复启动得到成功结果，不重复 OnStart。按类型启动先查询已有实例，不先构造第二个对象。
- 启动记录先于可能同步执行的协程及回调建立，Core 自行去重，不依赖 XFAB 执行请求缓存。当前请求不池化，成功和失败后移除进行中记录；Running 返回新建的已完成成功请求，而非缓存旧请求。
- 先提交生命周期结果，再调用完成回调，订阅者异常不反向改写启动结果。未改造 Foundation 的整条通知链，不承诺逐订阅者异常隔离。
- 当前状态为 `Created → Starting → Running → Closing → Closed`，另有 `StartFailed`、`CloseFailed`。Starting 注册可见但不是 Running，普通 Close 明确拒绝；StartUpModuleRequest 的 Abort 在提前完成请求之前拒绝，不提供取消系统。
- 注册表暴露缓存的只读实时视图；ModuleManager 保持注册、请求及实例身份一致。SingletonModule.Instance 只查询实际注册实例，无第二份可写缓存，不依赖 base.OnStart；解除注册后不返回旧实例。
- CurrentModule 仍是 View/资源的过渡上下文；拒绝或重复启动不污染它，失败仅在 current 仍指向自身时恢复此前有效上下文，不覆盖其他已成功模块；嵌套启动的执行上下文有作用域。未全面设计并发场景归属或替换 View/资源系统。
- Module 默认创建、关闭不再隐式使用 ClassPool。仅正确清理且核心索引无对象残留的实例可显式重启；不承诺重置全部业务字段。
- 实际限制：StartFailed 仍保留注册及可能的子对象，普通模块可清理后重启；基础模块普通 Close 仍拒绝，失败恢复依赖 D/E。CloseFailed 保留注册、拒绝启动并允许重试关闭。当前仍有旧 OnExit/Exit 双路径，不是 D 的最终管线；不得将这些状态称为完整回滚。

## 6. 关闭与启动失败（批准目标，D/E 未实施）

- D 建立唯一关闭清理管线，淘汰 Module 旧 OnExit 双入口；进入 Closing 后禁止新增业务对象，OnClose 作为清理前钩子，随后 Controller 退出、现有 View 清理、剩余 Model 退出，再清除核心索引/owner、注销并进入 Closed，最后报告清理异常。
- D 对每个 owned 对象独立尝试清理，单一异常不阻断兄弟对象；重复 Close 不重复通知。Closed 表示逻辑所有权结束，不等于所有外部资源都释放成功。
- 基础模块普通 Close 拒绝，但 D 的明确 Shutdown 可以结束基础模块；普通模块先于基础模块，各组逆注册顺序处理，继续清理并汇总错误，不引入自动拓扑。具体公开 API 随 D 授权任务落地。
- E 将配表/有效资产/JSON/Model 初始化/默认场景/OnStart 错误统一为启动失败；区分合法空数组与无效空文本，临时资源在成功与失败路径成对归还。
- E 复用 D 的清理管线，逻辑回滚、注销及移除启动记录后发布失败完成，保留原始错误及附带清理错误；基础模块失败不能被普通 Close 禁令阻断回滚。正确清理后允许重新启动，SingletonModule 的失败后失效以注销完成为前提。
- ProjectName 与 Module 身份分离，在任何资源加载前确定，不再临启动钩子强制覆盖。具体 setter/只读签名尚未由专项落地确认，不在此固化研究中的 API 建议。
- 回滚不是 Unity 世界事务：不自动恢复 Single 已卸载场景，不撤销任意用户外部副作用，也不修补原 XFAB 的挂起路径。View 自身异常收尾、缓存及场景归属的全面契约留给 1.3。

## 7. A～F 边界与门禁

| 批次 | 目标与前置 | 实施边界与当前状态 |
|---|---|---|
| A 查询与共享 | 本地 Get、显式 owner、Load/Create 分流 | 已验收提交；不重建注册事务、Module 生命周期或 View/资源查找 |
| B 注册完整性 | A 稳定；单 owner、临时注册、精确移除、long ID、对象级异常安全、Model 非池化 | 已实施审阅通过并提交；不保证多对象 Module 关闭继续清理或模块启动整体回滚 |
| C 唯一注册与启动 | B 稳定；Module 状态、请求去重与重入、只读注册表、Module 非池化、Singleton 身份及最小 Abort 防护 | 已报告完成、待验收未提交；D/E 仅有状态和最低限度清理支撑，不声称完整实现 |
| D 关闭与 Shutdown | C 身份及状态稳定 | 未实施；唯一管线、OnClose 顺序、逐对象异常继续清理、基础模块 Shutdown |
| E 启动失败与资源前置 | B/C/D 稳定 | 未实施；完整逻辑回滚、错误传播、ProjectName 提前确定、临时资源成对归还 |
| F 契约收口 | A～E 专项验证及验收成立 | 未实施；收敛本次替换直接产生的入口、模板和测试清理，完成 1.2 门禁，不扩展后续系统 |

每批优先复用 Core 测试并补关键契约，执行 Unity 编译和直接受影响的专项回归；涉及 Foundation 时做最小相关验证。完整游戏、Audio、全量 Editor、Foundation 或 Windows Player 不作为逐批机械门槛；1.4 的完整核心与原游戏非干扰门禁仍保留。原 XF、XFAB PackageCache、游戏业务、既有 YL Audio、普通 IOC/Singleton/Root 均不在 1.2 默认修改范围。同工程专项通过不等于最终独立验收。
