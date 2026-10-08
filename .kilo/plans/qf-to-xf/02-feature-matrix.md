# 完整功能与 API 对照矩阵

## 1. 基线、编号与状态

当前：B1.1..B1.13用户确认全部测试通过（2026-10-04 03:35 UTC），含DOTween真实Play迁移。后续Help术语修正及本轮轻量门禁已完成；最小修复关停组合回调与Condition复租回写，10程序集编译/21项纯托管检查通过，用户确认Unity编译正常及新增7项EditMode回归通过。核心391/87、Observable58/25、DOTween1/79（总450/191）仅源码预期，未重跑全量PlayMode。ACTIONKIT_READY_FOR_AUDIOKIT = YES；完整发布级验收不扩大，恢复入口见03顶部。

2026-10-04 AudioKit 本轮状态：AU-001..035 正式能力已迁入 `Assets/ProjectModules/AudioKit`，Runtime / ActionKitIntegration / Editor / 三组 Tests 六程序集同版本辅助 C# 编译通过，34 个旧公开类型及 278 个直接可见成员映射无遗漏，27 项限定纯托管回归通过。EditMode 生命周期用例的 SendMessage 原生断言已修正；11:25 UTC 用户确认测试全部通过，记为 I+V(user)，不是代理自行运行 Unity。AUDIOKIT_READY_FOR_INTEGRATION = YES，本轮停止，不自动切换业务。Core 的程序集实际引用仅 Unity/.NET，动作联动使用 Project.ActionKit，Help 使用 Project.HelpKit；未修改业务或删除旧模块。AU-810..815 全部 Example/Sample/Demo 资产按本轮用户要求 OUT(user)；AU-800 的 XFAB Adapter 本轮不创建，Resources 与外部 Loader 契约保留。后文 Audio 的历史 P/示例/XFAB 要求以本段及当前用户范围为准，不作为本轮额外任务。

状态 `P` = 已盘点、已设计、未实现、未验证；`P+B` = 同上，另有原问题/兼容/提供者阻塞待确认。`H` = 历史说明或空实现仍纳入清单，不假称原功能可用。没有“未使用所以删除”或“暂不支持已结项”。

`OUT(user)` = 用户明确移出交付范围，不是已实现或验证通过；当前仅适用于Action完整主样例/扩展示例及其专用场景、图片。API Help简短代码、扩展实现、测试、ScreenTransition运行资源和Audio范围不变。后文历史批次中的“全部样例必须交付”是调整前要求，以本状态为准。

实施状态 `I+U` = 新源码已写入，Unity 导入/编译/测试待验证；`I+B+U` = 同上，原问题仍未处理结项；`PARTIAL+U` = 只完成该功能族的一部分，不能算整个族已实现。`I+V(user)` = 用户确认相应批次测试通过，非代理自行执行；带`B`仍表示原问题未解决。B1.1确认不替代B1.2重新编译/回归或最终独立工程验收。

AK-001..058 与 AU-001..035 是稳定功能族ID；800段是配套工具/资源/示例/扩展；900段是原问题/待确认项。签名附录中每个成员用 `功能ID.类型.成员` 作为子ID；同名重载增加本地冻结的 `.1/.2/...`，继承/接口实现用已明确列出的契约集合展开。**子ID一经冻结不随新行号重排**。表中的原始文件/行号是证据，不是新实现位置。

测试索引：`E-AK-nnn`/`P-AK-nnn` 是对应功能族的 EditMode/PlayMode 用例；AU同理。`M-xxx` 为人工/Editor检查；`I-A`/`I-B`/`I-C` 分别是独立Action、独立Audio和游戏回归；`BUILD-*`为Player/AOT/导出构建。每个族的全部子签名必须进入编译/行为测试，不只测一条代表调用。

依赖ID D-01..14的**实际XF候选文件、程序集、等价判断与最终方式**见计划第4节。依赖列不是声称新实现继续引用旧QF；均须解除。正常行为与原问题分列；B1.1的characterization有用户通过确认，但没有原问题修复结论；B1.2及其它风险仍以静态证据/待运行验证区分。

### 1.1 原始路径索引

`AK/` = `Assets/QFramework/Toolkits/_CoreKit/ActionKit/Scripts/`；`AU/` = `Assets/QFramework/Toolkits/AudioKit/Scripts/`；`CORE/` = `Assets/QFramework/Toolkits/_CoreKit/`；`F` = `Assets/QFramework/Framework/Scripts/QFramework.cs`。

| 文件编号 | 相对 AK/ 路径 | 已完整读取 | 主要族 |
|---|---|---:|---|
| F01 | ActionKit.cs | 1-502 | 001/008/042 |
| F02 | Framework/IAction.cs | 1-286 | 002-005 |
| F03 | Framework/IActionExecutor.cs | 1-34 | 006 |
| F04 | Internal/Executor/MonoUpdateActionExecutor.cs | 1-115 | 006 |
| F05 | Internal/Queue/MonoActionQueue.cs | 1-76 | 007 |
| F06 | Internal/ActionKitMonoBehaviourEvents.cs | 1-78 | 008/020 |
| F07 | Internal/ActionKitCurrentScene.cs | 1-34 | 009 |
| F08 | Internal/Action/Delay.cs | 1-115 | 010 |
| F09 | Internal/Action/DelayFrame.cs | 1-89 | 011 |
| F10 | Internal/Action/Lerp.cs | 1-106 | 012 |
| F11 | Internal/Action/Callback.cs | 1-77 | 013 |
| F12 | Internal/Action/Condition.cs | 1-80 | 014 |
| F13 | Internal/Action/ConditionSequence.cs | 1-98 | 015 |
| F14 | Internal/Action/Sequence.cs | 1-157 | 016 |
| F15 | Internal/Action/Parallel.cs | 1-119 | 017 |
| F16 | Internal/Action/Repeat.cs | 1-126 | 018 |
| F17 | Internal/Action/Custom.cs | 1-159 | 019 |
| F18 | Internal/Action/Coroutine.cs | 1-83 | 020 |
| F19 | Internal/Action/Task.cs | 1-98 | 021 |
| F20 | ShortCut/AudioSourceShortCutExtensions.cs | 1-33 | 022 |
| F21 | ScreenTransition/ScreenTransition.cs | 1-30 | 023/024 |
| F22 | ScreenTransition/ScreenTransitionFade.cs | 1-64 | 023 |
| F23 | ScreenTransition/ScreenTransitionInOut.cs | 1-56 | 024 |
| F24 | ScreenTransition/ScreenTransitionCanvas.cs | 1-20 | 025 |
| F25 | DeprecateActionKit.cs | 1-1783 | 026-044 |
| F26 | Utility/EaseUtility.cs | 1-284 | 045-055 |

Action共26文件/4702行；Audio共54文件/3996行，54文件完整覆盖表在第7节。归档另读取Action20脚本+20场景索引、Audio28脚本+16场景索引、Action两个嵌套扩展各2脚本+1场景。没有把压缩档导入工程，也没有将归档脚本计入当前程序集源文件数。

## 2. ActionKit 完整功能去向

每行包含实际能力、对应实现、必要支撑、兼容差异和测试。迁移后默认namespace为Project.ActionKit，核心算法来自该本地文件，不从零改成XF Timer。

| ID | 原定义与实际行为 | 对应实现 / 所需底层 | 兼容差异或原问题 | 验证用例 | 状态 |
|---|---|---|---|---|---|
| AK-001 | F01:21-389，全部工厂、公开ID_GENERATOR、ComplexAPI空实例方法 | 工厂/七消息/模块生命周期已实施获用户通过；B1.11 ComplexAPI历史组合说明进入Help | 原空文档载体不加Runtime空壳；IArchitecture通用角色差异仍见AK042 | 当前运行时前批用户确认，B1.11帮助/片段待Unity | I+B+U |
| AK-002 | F02:14-60,237-275；两枚举、IAction<TStatus>/IAction、Execute/Finish状态机 | B1.2 Runtime/API/IAction.cs完整原状态机 | 原风险保留 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-003 | F02:62-123；AbstractAction<T>泛型、自定义钩子、Allocate/Reset/Deinit | B1.2 Runtime/API/IAction.cs完整 | Reset不等同Allocate | B1.2用户确认通过；B1.3须完整回归 | I+V(user) |
| AK-004 | F02:27-39,125-172,221-235,278-284；Controller/时间模式/暂停恢复回收 | B1.3 API/IActionController.cs与ActionStartExtensions完整 | 原raw/空/stale问题仍开放 | B1.3用户确认通过；B1.4须完整回归 | I+B+V(user) |
| AK-005 | F02:178-218；六个Start、局部/当前scene/global，启动尝试Execute(0) | B1.3完整六重载/同步启动 | 原scope/生命周期边界保留 | B1.3用户确认通过；B1.4须完整回归 | I+B+V(user) |
| AK-006 | F03/F04；prepare→dictionary、scaled/unscaled、完成回调→Deinit→回收 | B1.3完整原自动调度及显式dt测试入口 | AK903-905不因characterization通过关闭 | B1.3用户确认通过；B1.4须完整回归 | I+B+V(user) |
| AK-007 | F05；旧队首每Update一项、现代延后回池回调 | B1.6 ActionQueue/driver接入完整旧FIFO队首，现代回收机制保持 | 旧节点完成只移出不Dispose；session/销毁清队列不执行遗留节点，原生命周期差异单列 | 新旧队列用例已写，源码完整待Unity回归 | I+B+U |
| AK-008 | F01:410-494/F06；7全局消息事件和协程服务 | B1.4七属性/七消息/协程wrapper完整 | 真实GUI/focus/pause/quit/平台和完整session仍需最后验收 | B1.4用户确认通过；B1.5需完整回归 | I+V(user) |
| AK-009 | F07；public场景组件/static字段、懒建，无DDOL | B1.3 Internal/ActionKitCurrentScene.cs完整 | 原单static/多实例风险仍开放 | B1.3用户确认通过；B1.4须完整回归 | I+B+V(user) |
| AK-010 | F08；固定/工厂Delay，先判断累计秒后加dt | B1.2 Runtime/Actions/Delay.cs完整 | 原factory/ID问题仍开放 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-011 | F09；frameCount帧差DelayFrame/NextFrame | B1.2 Runtime/Actions/DelayFrame.cs完整 | 原帧差和暂停语义保留 | B1.2用户确认通过；B1.3须完整回归 | I+V(user) |
| AK-012 | F10；Lerp/Lerp01、字段、自定义回调、首A尾B | B1.2 Runtime/Actions/Lerp.cs完整 | 强制Finish与原边界保留 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-013 | F11；OnStart同步Callback再Finish | B1.2 Runtime/Actions/Callback.cs完整 | 原同步完成语义 | B1.2用户确认通过；B1.3须完整回归 | I+V(user) |
| AK-014 | F12；OnStart空，OnExecute谓词成立后callback再Finish | B1.2 Runtime/Actions/Condition.cs完整 | 原轮询时机/Deinit语义 | B1.2用户确认通过；B1.3须完整回归 | I+V(user) |
| AK-015 | F13；一次条件决定子Sequence或直接完成 | B1.2 Runtime/Actions/ConditionSequence.cs完整 | AK-909不因characterization关闭 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-016 | F14；Sequence同步跨完成项，下一项Execute(0) | B1.2 Runtime/Actions/Sequence.cs完整 | 原组合/dt/child回收保留 | B1.2用户确认通过；B1.3须完整回归 | I+V(user) |
| AK-017 | F15；Parallel完成项交换前缀、Reset全部 | B1.2 Runtime/Actions/Parallel.cs完整 | 原空并行/交换风险仍开放 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-018 | F16；Repeat内Sequence，-1/0无限、正N轮 | B1.2 Runtime/Actions/Repeat.cs完整 | 原负值/新旧0差异保留 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-019 | F17；typed/untyped Custom、Data、回调设置、Finish | B1.2 Runtime/Actions/Custom.cs完整 | 原untyped/复用风险仍开放 | B1.2用户确认通过；B1.3须完整回归 | I+B+V(user) |
| AK-020 | F18/F06；Func<IEnumerator>、ToAction、嵌套协程 | B1.4完整现代协程实现 | AK907仍开放，不伪称安全取消 | B1.4用户确认通过；B1.5需完整回归 | I+B+V(user) |
| AK-021 | F19；Func<Task>、Task.ToAction、async void完成 | B1.4完整原Task/门面/扩展 | AK908 fault/cancelled专项及回写/Dispose问题仍开放 | B1.4用户确认通过；B1.5需完整回归 | I+B+V(user) |
| AK-022 | F20；Unity AudioSource.PlayAction，等!isPlaying | B1.4完整Shortcut，不依赖AudioKit | AK914原语义仍开放 | B1.4用户确认通过；B1.5需完整回归 | I+B+V(user) |
| AK-023 | F21/F22；FadeIn0→1/Out1→0，黑色1秒，Color/Alpha/Duration | B1.5完整Fade与工厂 | AK913仍开放；视觉人工缺口不等于alpha测试 | B1.5用户确认通过，后批完整回归 | I+B+V(user) |
| AK-024 | F21/F23；泛型InOut/OnInFinish/In/Out，另启global sequence | B1.5完整原InOut/子序列 | AK912不因保真测试通过关闭 | B1.5用户确认通过，后批完整回归 | I+B+V(user) |
| AK-025 | F24；internal ScreenTransitionCanvas/序列化ColorImage、Resources实例 | B1.5新资源已生成且GUID/字段核对，不依赖旧Prefab/SingletonKit | 当前scene实例/持久/资源错误诊断差异已记录 | B1.5用户确认通过；独立环境/实际画面待最终验证 | I+B+V(user) |
| AK-026 | F25:20-31,546-549,1396-1406,1541-1553；预热/元数据/序列化 | B1.6声明/启动已实现 | 原拼写与无Obsolete保留 | B1.6用户确认通过，后批回归 | I+V(user) |
| AK-027 | F25:1383-1539,1638-1656；旧基类与驱动 | B1.6完整旧引擎/Mono与IEnumerator驱动 | 真实EOF/生命周期风险仍保留 | B1.6用户确认通过，后批回归 | I+B+V(user) |
| AK-028 | F25:39-99；DelayAction与Mono/Chain | B1.6原子/Mono通过，B1.7补Chain.Delay完整 | 原回收边界未修 | 新Chain用例待本批Unity | I+B+U |
| AK-029 | F25:108-186；FrameAction与Mono/Chain | B1.7补Chain.DelayFrame/NextFrame两个重载，原void入口保持 | 真实frameCount及EOF验证缺口保留 | 新链式/帧跨tick测试待本批Unity | I+B+U |
| AK-030 | F25:194-234,1692-1695；EventAction/链式 | B1.7补Chain.Event(params) | 原params/null边界保留 | B1.6节点通过，本批链式待Unity | I+B+U |
| AK-031 | F25:238-269,1675-1678；OnlyBegin/回收/链式 | B1.7补Chain.OnlyBegin | 原无Reset/不自动回池不修 | 本批组合/链式待Unity | I+B+U |
| AK-032 | F25:277-305,1698-1701；Until/显式协议/链式 | B1.7补Chain.Until | 原null/生命周期边界保留 | 本批Chain条件终止待Unity | I+B+U |
| AK-033 | F25:383-507；Timeline/Pair/Append/KeyEvent | B1.7 LegacyTimeline.cs全部原声明/行为；ActionNodePool替Key池 | 原strict比较、空不Finish、key无Reset保留 | 新时间点/顺序/回调/Reset/回收测试待Unity | I+B+U |
| AK-034 | F25:509-526；AsyncNode/HashSet | B1.7 LegacyDynamicNodes.cs完整 | 集合修改/无parentFinish和cleanup原风险 | 新集合/异常/ownership测试待Unity | I+B+U |
| AK-035 | F25:528-544；QueueNode动态FIFO | B1.7 LegacyDynamicNodes.cs完整 | 原持久未完成/不清pending保留 | 新逐entry/Dispose缺口测试待Unity | I+B+U |
| AK-036 | F25:552-619；RepeatNode/INode | B1.7 LegacyCompositionNodes.cs完整 | -1无限、0依赖child是否即时完成，不统一现代Repeat | 新次数/Reset/ownership/recursiveCurrent测试待Unity | I+B+U |
| AK-037 | F25:625-720；SequenceNode/两列表/钩子 | B1.7完整，ListPool换节点自有List，Dispose仍Clear/null | 原同dt/剩余数/空索引和单次Dispose边界保留 | 新同tick/剩余数/派生钩子/Reset测试待Unity | I+B+U |
| AK-038 | F25:726-794；SpawnNode/params/Add | B1.7完整，原结束订阅/强制Finish/清理保持 | 原空不完成/预Finished不计数保留 | 新逆序/Reset/强制/空测试待Unity | I+B+U |
| AK-039 | F25:307-378,1558-1782；Chain/trigger | B1.7 LegacyActionChain及2trigger/全部接口与扩展完整 | 原拼写、显式OnFinished、autoDispose、停用一次性问题保留 | 新Mono工厂/Begin/Destroy/条件/多回调/trigger测试待Unity | I+B+U |
| AK-040 | F25:796-981,1071-1100；类状态机/target/transition/index | B1.8 Compatibility/ActionKitFSM.cs、ActionKitFSMTransitionTable.cs；领域TransitionTypeIndex替原TableIndex | 原PreviousState/泛型getter风险保留，Table基类及索引类型身份变更 | 类状态/顺序/桶/枚举/派生测试已写，待Unity | I+B+U |
| AK-041 | F25:983-1069；EnumStateMachine/typed事件/Restore | B1.8 EnumStateMachine.cs、Subscriptions/ActionStateEvents.cs | 原约束/Restore风险保留；事件改模块scope，不共享旧global bus | 字段/同值/Restore/非enum/注销及Destroy测试已写，待Unity | I+B+U |
| AK-042 | 原Architecture生命周期/公开面 | B1.9 API/ActionKitLifecycle.cs：Initialize/Shutdown、InitArchitecture/Deinit/patch/局部Interface | Interface不再是IArchitecture，旧服务注册/Command/Query角色明确不伪造 | B1.9用户确认通过，独立/平台验收仍待后续 | I+B+V(user) |
| AK-043 | F25:1105-1263；双泛型FSM/公开nested/delegate | B1.8 LegacyFSM.cs完整原核心，无enum约束 | 原callback顺序/未知/Clear风险保留 | 参数/顺序/覆盖/nested/错误/跨frame测试已写，待Unity | I+B+U |
| AK-044 | F25:1269-1381；QFSMLite字符串FSM | B1.8 QFSMLite.cs完整原核心 | 原缺状态/nullcallback/覆盖风险保留 | 转移/嵌套类型/错误/跨frame测试已写，待Unity | I+B+U |
| AK-045 | F26:14-56；EaseType31+Count/Linear | B1.1 Runtime/Utility/EaseUtility.cs完整 | InoutQuart拼写保留 | B1.1用户确认全部测试通过；B1.2仍需重跑 | I+V(user) |
| AK-046 | F26:58-77；三Back，bounciness1.5 | B1.1 EaseUtility原3方法 | 保留默认overshoot | B1.1用户确认通过；新批需回归 | I+V(user) |
| AK-047 | F26:79-112；三Bounce，7.5625 | B1.1完整3方法/4分段/参数 | AK-924仍开放，characterization通过不是修正通过 | B1.1用户确认通过；新批需回归 | I+B+V(user) |
| AK-048 | F26:115-133；三Circle | B1.1原3方法 | AK-925越界语义未修 | B1.1用户确认通过；新批需回归 | I+B+V(user) |
| AK-049 | F26:135-152；三Cubic | B1.1原3方法 | 浮点/拼接保留 | B1.1用户确认通过，包含Transform跨帧测试 | I+V(user) |
| AK-050 | F26:154-188；三Elastic，elasticity.3/duration5 | B1.1原3方法/全部参数 | AK-925原边界仍开放 | B1.1用户确认通过；新批需回归 | I+B+V(user) |
| AK-051 | F26:191-208；三Expo | B1.1原3方法 | AK-925非精确端点未修 | B1.1用户确认通过；新批需回归 | I+B+V(user) |
| AK-052 | F26:210-227；三Quad | B1.1原3方法 | 不新增clamp | B1.1用户确认通过，包含timeScale0测试 | I+V(user) |
| AK-053 | F26:229-246；三Quart | B1.1原3方法 | enum与方法拼写保持 | B1.1用户确认通过；新批需回归 | I+V(user) |
| AK-054 | F26:248-265；三Quint | B1.1原3方法 | 浮点算法保持 | B1.1用户确认通过；新批需回归 | I+V(user) |
| AK-055 | F26:267-282；三Sine | B1.1原3方法 | 常量1.570796保持 | B1.1用户确认通过；新批需回归 | I+V(user) |
| AK-056 | F02/04/05/08-19/F25；factory、安全池、列表、延后回收 | B1.2现代池已用户确认；B1.6 ActionNodePool实现旧原子节点安全/容量/预热及必要协议 | 私有必要支撑，不提供旧PoolKit provider身份/通用factory体系；旧组合列表支撑后续 | B1.6局部池13例及startup/混合回收测试已写待Unity，族未整体结项 | PARTIAL+U |
| AK-057 | F01/F06/F25/F:468-641,795-957；订阅/token/typedState/Or | B1.4订阅闭包已通过，B1.8 ActionStateEvents补全FSM实际所需typed发布/注册/注销 | 新模块scope/类型身份不同，旧监听不自动接收；原AK927不修 | 新状态事件测试待Unity，完整生命周期验收后续 | I+B+U |
| AK-058 | F04/F22/F24；GetOrAdd/ColorAlpha/Instantiate/DDOL/Editor属性 | 原生Unity小辅助+模块Help；D-13/14 | UnityObject存活语义、alpha只改单通道、Editor隔离 | E/P原参数、缺组件/资源；BUILD-A | P |

## 3. AudioKit 完整功能去向

默认namespace Project.AudioKit；原声音/音乐/人声算法不转发成XF AudioPlayer。所有基础能力不需要ActionKit。

| ID | 原定义与实际行为 | 对应实现 / 所需底层 | 兼容差异或原问题 | 验证用例 | 状态 |
|---|---|---|---|---|---|
| AU-001 | AudioKitArchitecture.cs:16-37；静态模型/系统/自动init | 幂等Initialize/Ensure/Shutdown/session reset；D-01 | 访问Settings时点、先换Config与派生生命周期显式设计 | E/P冷启动/重复/换Config/DomainReload关闭 | P |
| AU-002 | AudioManager.cs:21-95,118；懒Host/pool/Music/Voice/Listener | 模块owned持久host/已有Listener检查；D-05/08 | internal manager不是public API；销毁恢复原缺口 | P无/已有Listener、对象数、切scene、destroy | P+B |
| AU-003 | AudioKit.cs:43-45,189-191；Music按名async/Clip两重载、独占槽 | 全重载/loop/开始结束/scale；D-07/10 | MusicOn未gate、同名不重播；不默默修正 | E/P两载/默认/替曲/同名/音量 | P+B |
| AU-004 | AudioKit.cs:55/66/77；Music stop/pause/resume和返回对象 | 三控制+MusicPlayer全部继续操作；D-09 | Resume调用Play而非UnPause；手停非自然回调 | Psource.time/加载中控制/Stop后开关 | P+B |
| AU-005 | AudioKit.cs:89-91,194-196；Voice两载、public PlayVoiceWithClip.Execute | 完整槽/开关/重载/公开Execute；D-07/10 | string无scale参数，沿用之前scale可能；旧command base无隐式保留 | E/P开关/两载/直接Execute/默认 | P+B |
| AU-006 | AudioKit.cs:101/111/121；Voice控制 | 全pause/resume/stop+VoicePlayer；D-09 | 与Music同控制边界 | P加载中/自然/手停/重开 | P+B |
| AU-007 | AudioKit.cs:169-174；Sound两载返回AudioPlayer | 保留返回player、所有参数；D-05/07/10 | 不替返回void；raw引用有效期需写明 | E/Pname/Clip、返回后全操作 | P+B |
| AU-008 | PlayingSoundPoolModel.cs:18-64；分组追踪/快照StopAll | 完整group/count/快照/开关停止；D-05 | 原pending/拒播不在活动列表，不能谎称StopAll已取消全部request | P同名多名/修改中停止/pending/paused | P+B |
| AU-009 | AbstractPlayer:16-26/Extensions:30-39/Proxy:109-161 | 保留共享Bindable音量、scale、pitch、IsLoop、AudioName；D-07 | Volume不是每player独立设置；不加范围归一化 | E/P三音量/多scale/播中pitchloop | P |
| AU-010 | LifeCycle:19-63/AbstractPlayer:84-95,117-124；多播Once | 正常回调顺序全保留；C#委托 | 先Invoke后清，重入/异常原问题 | P多注册顺序/首播/自然/手停/重入异常 | P+B |
| AU-011 | Proxy:71-106,172-191；isPlaying完成、pause/Stop清监听 | 本地最小Update监听/generation；D-09 | 首次检查在Update；不是clip.length计时；不拷动作引擎 | Ppitch/loop/paused/Stop/destroy/replay/scale0 | P+B |
| AU-012 | AudioPlayer:31-61；安全池/上限/回收销毁source | 模块pool/接口完整；D-05 | rawplayer复用、外部Pool类型变化 | E/P预热/超限/双回收/清订阅/源销毁 | P |
| AU-013 | AbstractPlayer保护钩子/Music构造/Deinit/Loader工厂 | 所有实际派生能力和钩子；D-05/07 | internal abstract限制直接外部派生，不能误报开放 | E外程序集派生Music/Audio、LoaderPool；原限制 | P+B |
| AU-014 | 3 Channel/默认EveryOne/阈值10/<= | 三原策略、全局默认与per-call；Time.frameCount | Global共享窗口；SoundFrames自然结束删除、手停不删 | E/P N/N+1/A/B/结束/非法enum | P |
| AU-015 | Settings:17-61/Manager:31-70；六键与各开关 | 原PlayerPrefs键/default/订阅；D-07 | 不改XF键；重开Clip合成名送loader，不是无损恢复 | E/P6键/无事件/播放中音量/重开 | P+B |
| AU-016 | QFramework.BindableProperty.cs:25-85；CustomProperty/UnityEvent/IsOn | 原Func getter/setter/Bind/总开关AND；UnityEvent | 先通知后setter，子开关不自动通知总开关；不是普通Bindable | E/P混合开关/回调内旧值/UnityEvent继承 | P+B |
| AU-017 | IAudioLoader.cs:15-21；Clip/sync/callbackAsync/Unload | 完整模块契约+可自定义；D-10 | async不得堵塞/空实现；取消/失败时序明确 | E/Psync成功失败/即刻晚到callback/Unload pending | P |
| AU-018 | Config/AbstractPool/DefaultPool全部 | 可替接口/栈pool/工厂继承；D-05/10 | 保存分配pool，避免用当前Config错误回池 | E/P换池/不同类型/工厂计数/原池回收 | P+B |
| AU-019 | SearchKeys:14-30；两字段/Allocate/回收/ToString | 完整公开keys+请求快照；D-05/10 | async当前提前回收，不能删bundle字段 | E/P延迟读key/清字段/双回收 | P+B |
| AU-020 | DefaultLoader:19-37；Resources sync/async/unload | 基础真实Resources路由+cache/lease；D-10/11 | async没写Clip/共享卸载原问题 | P真async/Clip一致/失败/取消/最后lease | P+B |
| AU-021 | 三Prepare/mode切换；先新加载后释放旧 | 三路径完整，borrowed/owned/generation/原pool；D-10/11 | sync路径未被内部调用但不能删除协议；旧race | E/P全切换/stop复用/晚到/managerdestroy | P+B |
| AU-022 | FluentMusicAPI全82行；Play void/loop true | 所有链式/池/返回类型；D-05 | mode不重置、loop/手停builder回收缺口 | E/P默认/name/Clip/复用/loop/回收 | P+B |
| AU-023 | FluentSoundAPI全95行；Play返回player | 全name/Clip/loop/scale/pitch/mode/池 | clip/scale/pitch/mode未清的原问题 | E/P每字段复用/控制/拒播/loop | P+B |
| AU-024 | F:690-760/外Prefs子类；真实公开属性支撑 | 模块完整小属性协议/Comparer/token/自动注销；D-06/07 | 不搬Framework；类型身份变；静态Comparer/无事件不保存保持 | E/Pnull/相等/多注册/初值/无事件/Comparer | P+B |
| AU-025 | 3 Disposable文件；CustomDisposable/销毁绑定/HashSet | 全公开能力与Unitytrigger | 非幂等/异常中断/编辑态不执行 | E/P一次两次/null/重复绑/异常/编辑态 | P+B |
| AU-026 | BinaryHeap/IBinaryHeapElement；minmax、N/NLog/全部操作/钩子 | 原heap算法/泛型/2枚举；标准C# | 叶index、尾引用、0capacity等原问题 | E随机序/两构造/build/sort/rebuild/remove/边界 | P+B |
| AU-027 | TimeItem+Timer.cs；双heap、DateTime、repeat/tick/cancel、Update/Dump | 完整原Timer/TimeItem/单例入口；D-04/05/08 | Dump当前空H；DateTime截断/<到期/负repeat原问题 | E/P全部重载/时钟/DateTime/0负/Cancel/池 | P+B/H |
| AU-028 | TimerHelper:13-40；分组Add/Clear/保护字段 | 分组取消保留+内部租约归属；TimeItem | 持有回池raw引用可能误取消新任务 | E/P完成→复用→Clear/重复Add/派生 | P+B |
| AU-029 | TimerManager.cs:8-487；TimeExtend.Timer flag/duration/loop/Pause/Resum/Del/Set | 全第二套系统独立保留；D-04 | 不合并heap或XF；原拼写Resum/Setloop；遍历/时钟问题 | E/P每重载/事件/flag/修改/RemoveAll | P+B |
| AU-030 | TimerDriver:489-513/TimerExtend:515-545 | driver静态字段/Get、OnCompleted/Updated链式保留 | driver原非DDOL，不静默换成另一生命周期 | P场景切换/已有driver/多driver/无新timer | P+B |
| AU-031 | ConsoleModuleSystem:17-56；名字/paused/mode/count/分组/volumepos | 完整诊断GUI/Editor，D-14；AU-801入口 | 原仅注册页不自动打开；不搬ConsoleKit | M/P所有字段即时刷新/显示/关闭/销毁 | P |
| AU-032 | AudioKit所有UNITY_EDITOR元数据/例子 | 独立Help与全部示例内容；D-14 | 不保留旧Attribute程序集；正文必须对齐签名 | M逐API帮助；BUILD-B无Editor/QF | P |
| AU-033 | Action/PlaySound.cs:15-93；有效现代action/两个显式Allocate、继承Allocate/两扩展 | 可选双模块Integration，基础Audio无Action | 暂停原不暂停声音；拒播失败手停可能不Finish | PIntegration等待/取消/paused/加载中/失败 | P+B |
| AU-034 | SettingsModel/LoaderPoolModel继承AbstractModel | 保留Initialized/Init/Deinit钩子，不建IOC；D-01 | IModel/IArchitecture cast、GetUtility/SendEvent互操作破口 | E派生/生命周期/旧类型迁移；I-B | P+B |
| AU-035 | AudioKit.asmdef两旧refs、外露同名support | 新namespace/基础Runtime/backend/Integration/Editor单向依赖 | 不能各程序集导出同全名QFramework类型 | I-B单装、两基础同装、BUILD-B | P |

## 4. 资源、工具、示例与可选扩展的完整去向

除明确标记OUT(user)的Action样例外，以下仍属必须交付；“可选Integration/提供者”指安装组合，不是允许不实现。

| ID | 原定义、资源和流程 | 后续对应实现与支撑 | 差异/验证/状态 |
|---|---|---|---|
| AK-800 | CORE/PackageKit/PackageMaker/SingleFileCreator.cs:18-30/42-145；public配置LicenseCode/FileName/Scripts/DependencyScripts/OutputFilePath；Inspector OnInspectorGUI、Create、选目录、打开目录 | B1.10专属ScriptableObject/Inspector/Exporter，全部字段、完整预设、目录/打开/确认覆盖及预检 | B1.10用户确认测试通过；菜单操作未单独确认，不扩大跨项目/构建验收；I+V(user) |
| AK-801 | 原ScreenTransitionCanvas.prefab及脚本/Image关系 | B1.5菜单经Preview Scene修正后已生成新Prefab，脚本/Image/UI引用已核对；测试用户确认通过 | 新GUID非旧资源复制；实际画面/Root scale0归一化仍待人工，不将资源计数当视觉通过；I+B+V(user) |
| AK-802 | ActionKitSingleFileCreator.asset:20-35=15模块脚本；37-64=28依赖；GUID4a4456594a6e6480bb3c61ed87d9030c；旧作者输出路径 | B1.10菜单由Unity生成配置；58源完整预设，ActionKit.cs+11组件脚本/原meta+asmdef+ScreenTransition资源 | 替代安装保留模块GUID，不能同装；用户确认测试通过，配置菜单/跨项目绑定仍单独待验；I+B+V(user) |
| AK-803 | F01的ClassAPI/MethodAPI/PropertyAPI/中英描述/示例；CORE/Internal/APIDoc/APIDoc.cs:178-201反射浏览 | B1.11中文Help保留23指南/106类型548成员、用途/参数返回、具体类型名完整入门、分组重载/搜索/主题/阅读导航 | 用户14:51 UTC确认完整测试和界面验收，不扩大独立/Player/AOT；I+V(user) |
| AK-810 | Example.unitypackage：0Delay、1Sequence/Callback、2Frame、3Condition、4Repeat、5Parallel、6Complex、7Custom、8Coroutine、9GlobalMessages | 用户2026-10-03 09:34 UTC取消完整样例迁移，前10组脚本/场景不移植；旧归档保留 | 对应动作能力和回归不删，不假称样例已交付；OUT(user) |
| AK-811 | 同归档12RandomDelay、13StartGlobal、14PauseResume、15Lerp、16StopManagement、17Ease/Triangle.png、18Task、19CurrentScene、20IgnoreTimeScale、21ScreenTransitions | 同次范围调整：其余10组脚本/场景/专用图片不移植；必要运行资源保留 | 功能/测试及原问题不随样例豁免；OUT(user) |
| AK-812 | CORE/Internal/Guidline/.../04.ActionKit时序动作执行系统：01-15章及两份11生命周期；5.v1.0之后新增/20241016.ScreenTransition | B1.11中文Help用户验收；B1.12/13补齐Observable/DOTween可选性、契约、短用法与许可，25指南，原两份11合并 | 四独立模式实际内容检查/片段编译通过；新增DOTween帮助Unity待验；I+U |
| AK-830 | 嵌套10.DOTween包：DOTweenAction:IAction；Allocate(Func<Tween>)、S状态契约；ActionKitDOTweenExtension.ToAction/DOTween | B1.13 Project.ActionKit.DOTween独立适配，真实DLL1.2.825，保留private ctor及公开签名 | 预绑定/租约marker/原context延期Kill及明确限制保留；真实Play迁移后的1 Edit/79 Play已获用户全部通过确认，不扩大平台验收；I+V(user) |
| AK-831 | 嵌套11.UniRx包：UniRxAction<T>:IAction；Allocate(Func<IObservable<T>>)、S；ActionKitUniRxExtension.ToAction<T>/UniRx<T>；旧asmdef引用UniRx/CoreKit/QF | B1.12 Project.ActionKit.Observable独立程序集，保留原公开声明/private ctor；BCL IObserver支撑，不需UniRx SDK | 用户确认完整测试通过，首OnNext成功/空流取消/错误取消及订阅和租约修正已验收；不扩大独立/平台验收；I+V(user) |
| AK-832 | 两嵌套包各1扩展+1示例+1场景；DOTween/UniRx说明章12/13 | 演示脚本/场景按用户要求不移植；扩展实现和自动验证仍归AK830/831，说明归AK812 | 仅演示资产豁免，旧归档不删；OUT(user) |
| AU-800 | SupportOldQF/Scripts/AudioKitWithResKitInit.cs:6-64，自动替pool、ResKitAudioLoaderPool/nestedLoader、全部sync/async/Unload | 完整交付Resources路由+XFAB pool/backend/catalog/init，不移ResKit；D-10/11 | 旧AssetBundleName并未被桥读取；旧ResLoader类型迁移；I-B-XFAB全行为；P+B |
| AU-801 | CORE/ConsoleKit/ConsoleKit.cs:19/29/41；ConsoleModule/Window；Windows/OSX/Editor F1、Android Escape、iOS四指 | AudioDiagnostics.Show/显示开关/独立Editor窗口；音频状态完整 | 原只注册页面；不假称过去自动有窗口；按平台M/字段/销毁注销；P |
| AU-802 | APIDoc提供者与Audio UNITY_EDITOR元数据；Audio4章QuickStart/CustomLoader/ActionSupport/Fluent | Audio专用Help/说明和匹配全部API的可编译示例 | 不搬通用Editor框架；Action章标可选联动；M/编译所有片段；P |
| AU-810 | Audio归档0Basic/Designer、1CustomLoader、3Fluent、4v1_1Sound/Designer、5v1_1Clip/Designer、7ResourcesMusic/Designer、8FluentMusic/Designer | 7组/场景/字段全部适配；DefaultResources和customloader实作示例 | ViewController改MonoBehaviour；不保留失效生成器菜单说明；M/I-B/P |
| AU-811 | 归档9ResKitMusic/Designer、13ResKitSound/Designer、14MusicClip、15SoundClip/Designer | 两Clip组、两后端组完整适配XFAB/Resources与目录/字段 | 不保留ResKit.Init或伪名加载；地址catalog差异显式；M/I-B-backend；P |
| AU-812 | 归档10StopMusic/Designer、11PauseResume/Designer、12FluentSound/Designer | 3组控制/Fluent示例完整，注释BugTest单列材料 | Resume/复用/手停与源行为核验；M/Play；P+B |
| AU-813 | 归档2AudioKitAction、6StopPlaySoundAction/Designer | 可选ActionAudio Integration下2组及场景，与AU033全部公开API对应 | 基础Audio不靠联动示例启动；取消/失败/暂停非假通过；P+B |
| AU-814 | 6归档音频：Resources game_bg/home_bg/button_clicked/hero_hurt.wav；ResKitResources Movement_Effect.wav/VGMA Challenge27.ogg | 全6音频新GUID/资源路由/场景字段，后端两资源改XFAB测试project | 不复用原GUID共存冲突；许可来源核查；M资源绑定/实际听感；P |
| AU-815 | 全28脚本/16场景含partial Designer和旧ViewController/Bind元数据 | 枚举新旧脚本/资源/type-map，普通序列化字段保留，UnityEvent/组件用Editor资产API转换 | 样例全部有去向，不复制旧CodeGen/Toolkit；I-B全部Samples；P+B |
| AK-840 | B1.1..B1.13用户全部通过确认；本轮关停/Condition轻量门禁 | 核心391/87、Observable58/25、DOTween1/79，合计450/191=641源码预期 | B1.13原失败已获用户重验通过；本轮21项纯托管检查及新增7项Unity EditMode回归通过，不声称全量重跑/发布级验收；I+V(user) |
| AU-840 | 当前无专属fixture；归档BugTest为注释，非自动回归 | 新Audio完整API/后端/Timer/Heap/Integration测试 | 不只验证发声，所有AU族有案例；未执行；P |
| AK-850 | 实际MIT版权头（liangxiegame等）、示例/扩展来源、导出LicenseCode | B1.1 Ease源头版权/许可声明/链接已保留；03记录原文件来源。其余源码、归档资源、导出版权待对应批次处理 | 来源文字非运行依赖；不是所有模块版权核验已完成；PARTIAL+U |
| AU-850 | liangxiegame及snowcold/liangxie Timer/Disposable等版权/MIT文本 | 全实际作者/许可证随源保留、原始路径对应、资源来源核查 | 不将改namespace当自己原创；M/P |

## 5. 签名与类型迁移索引

下面不是实现代码。所有类型名称保留概念，QFramework命名空间改所属Project模块。`S`表示已完整列出的IAction状态契约，仅对明确实现该接口的类型展开；`L`表示旧ActionKitAction继承面；`R`表示AbstractAudioPlayer继承面。这样不重复粘贴每种动作的相同成员，但每个展开成员均有稳定子ID，不能跳过测试。

本地不存在专属public CLR event声明；Action回调为Action字段/属性、EasyEvent对象，Audio为委托/UnityEvent/Bindable通知。显式接口成员、有效internal类型中的public关键词、保护钩子、隐式构造和原框架继承面均单列，不混为public模块API。

### 5.1 Action 门面、契约与启动

签名用text记录以免误认为本轮已创建源码。行首为稳定子ID和原声明行号；同族实现的对应位置均是新Action Runtime，原Architecture公共面例外见5.5。void没有返回能力不可换成伪结果。

```text
AK-001.Type @F01:21 public partial class ActionKit : Architecture<ActionKit>
AK-001.ID_GENERATOR @23 public static ulong ID_GENERATOR = 0
AK-001.Delay @44 static IAction Delay(float seconds, Action callback)
AK-001.Sequence @71 static ISequence Sequence()
AK-001.DelayFrame @104 static IAction DelayFrame(int frameCount, Action onDelayFinish)
AK-001.NextFrame @109 static IAction NextFrame(Action onNextFrame)
AK-001.Lerp @115 static IAction Lerp(float a,float b,float duration,Action<float> onLerp,Action onLerpFinish=null)
AK-001.Callback @120 static IAction Callback(Action callback)
AK-001.Condition @149 static IAction Condition(Func<bool> condition,Action onCondition=null)
AK-001.Repeat @182 static IRepeat Repeat(int repeatCount=-1)
AK-001.Parallel @210 static IParallel Parallel()
AK-001.ComplexAPI @253 void ComplexAPI() [原空方法/示例载体，H]
AK-001.Custom.1 @323 static IAction Custom(Action<ICustomAPI<object>> customSetting)
AK-001.Custom.2 @330 static IAction Custom<TData>(Action<ICustomAPI<TData>> customSetting) [无约束]
AK-001.Coroutine @359 static IAction Coroutine(Func<IEnumerator> coroutineGetter)
AK-001.Task @386 static IAction Task(Func<Task> taskGetter)
AK-008.OnUpdate @410 static EasyEvent OnUpdate {get;}
AK-008.OnFixedUpdate @424 static EasyEvent OnFixedUpdate {get;}
AK-008.OnLateUpdate @438 static EasyEvent OnLateUpdate {get;}
AK-008.OnGUI @452 static EasyEvent OnGUI {get;}
AK-008.OnApplicationQuit @465 static EasyEvent OnApplicationQuit {get;}
AK-008.OnApplicationPause @478 static EasyEvent<bool> OnApplicationPause {get;}
AK-008.OnApplicationFocus @493 static EasyEvent<bool> OnApplicationFocus {get;}
AK-042.Init @496 protected override void Init() [当前空]

AK-002.ActionStatus @F02:14 enum {NotStart=0,Started=1,Finished=2}
AK-004.ActionUpdateModes @21 enum {ScaledDeltaTime=0,UnscaledDeltaTime=1}
AK-002.IActionGeneric @41 interface IAction<TStatus> [无约束]
  ActionID:ulong {get;set;}; Status:TStatus {get;set;}; Paused:bool {get;set;}; Deinited:bool {get;set;}
  OnStart():void; OnExecute(float dt):void; OnFinish():void; Reset():void; Deinit():void
AK-002.IAction @58 interface IAction : IAction<ActionStatus>
S = 上述9成员以ActionStatus代入；S成员子ID = 所属AK族.具体类型.成员名
AK-002.Finish @237 static void Finish(this IAction self)
AK-002.Execute @242 static bool Execute(this IAction self,float dt)

AK-003.AbstractAction @62 abstract class AbstractAction<T>:IAction where T:AbstractAction<T>,new()
  .ctor @64 protected AbstractAction()
  Allocate @71 static T Allocate(); S @80-122（OnStart/Execute/Finish virtual）
  OnReset @95 protected virtual void OnReset(); OnDeinit @99 protected virtual void OnDeinit()
AK-004.IActionController @27 interface IActionController
  ActionID:ulong {get;set;} @29; Action:IAction {get;set;} @31
  UpdateMode:ActionUpdateModes {get;set;} @33; Paused:bool {get;set;} @35
  Reset():void @36; Deinit():void @37; Recycle():void @38
AK-004.ActionController @125 class ActionController:IActionController
  上述7成员 @135-168；Allocate @154 static IActionController Allocate()
AK-004.Pause @221 static void Pause(this IActionController self)
AK-004.Resume @229 static void Resume(this IActionController self)
AK-004.IgnoreTimeScale @280 static IActionController IgnoreTimeScale(this IActionController self)

AK-005.Start.1 @178 static IActionController Start(this IAction self,MonoBehaviour monoBehaviour,Action<IActionController> onFinish=null)
AK-005.Start.2 @189 static IActionController Start(this IAction self,MonoBehaviour monoBehaviour,Action onFinish)
AK-005.StartCurrentScene.1 @200 static IActionController StartCurrentScene(this IAction self,Action<IActionController> onFinish=null)
AK-005.StartCurrentScene.2 @205 static IActionController StartCurrentScene(this IAction self,Action onFinish)
AK-005.StartGlobal.1 @210 static IActionController StartGlobal(this IAction self,Action<IActionController> onFinish=null)
AK-005.StartGlobal.2 @215 static IActionController StartGlobal(this IAction self,Action onFinish)
AK-005.ExtensionsType @175 public static class IActionExtensions
AK-004.ExtensionsType @278 public static class IActionControllerExtensions
AK-006.IActionExecutor @F03:14 interface IActionExecutor
  Execute @16 void Execute(IActionController controller,Action<IActionController> onFinish=null)
AK-006.ExtensionsType @20 public static class IActionExecutorExtensions
  UpdateAction @22 static bool UpdateAction(this IActionExecutor self,IActionController controller,float dt,Action<IActionController> onFinish=null)
AK-006.MonoExtensionType @F04:104 public static class MonoUpdateActionExecutorExtension
  ExecuteByUpdate @106 static IAction ExecuteByUpdate<T>(this T self,IAction action,IActionController controller,Action<IActionController> onFinish=null) where T:MonoBehaviour
AK-009.CurrentSceneType @F07:13 public class ActionKitCurrentScene:MonoBehaviour
  mSceneComponent @15 public static ActionKitCurrentScene mSceneComponent=null
  SceneComponent @17 public static ActionKitCurrentScene SceneComponent {get;}
```

### 5.2 Action 原子/组合/扩展的全部声明

每种IAction实现的S成员都按具体类型展开，而不是仅检查interface存在。private构造保持私有，通过factory分配；internal类型的public成员属于实现面，不误报可从别的程序集访问。

| 子ID前缀 / 类型声明 | 原位置 | 全部自身声明、重载与扩展点 |
|---|---|---|
| AK-010.Delay，internal class Delay:IAction | F08:14 | S:55-100；fields DelayTime:float:16、DelayTimeFactory:Func<float>=null:18；OnDelayFinish:Action{get;set;}:20、CurrentSeconds:float{get;set;}:22；private ctor:24；Allocate.1(float delayTime,Action onDelayFinish=null):Delay:31；Allocate.2(Func<float> delayTimeFactory,Action onDelayFinish=null):Delay:43 |
| AK-010.DelayExtension，public static class | F08:103 | Delay.1(this ISequence self,float seconds,Action onDelayFinish=null):ISequence:105；Delay.2(this ISequence self,Func<float> delayTimeFactory,Action onDelayFinish=null):ISequence:110 |
| AK-011.DelayFrame，internal class :IAction | F09:14 | S:16-69；Allocate(int frameCount,Action onDelayFinish=null):DelayFrame:26 |
| AK-011.DelayFrameExtension，public static class | F09:77 | DelayFrame(this ISequence self,int frameCount):ISequence:79；NextFrame(this ISequence self):ISequence:84 |
| AK-012.Lerp，public class :IAction | F10:14 | S:42-90；fields A:float:20、B:float:21、Duration:float:22、OnLerp:Action<float>:23、OnLerpFinish:Action:24；Allocate(float a,float b,float duration,Action<float> onLerp=null,Action onLerpFinish=null):Lerp:28 |
| AK-012.LerpExtension，public static class | F10:93 | Lerp(this ISequence self,float a,float b,float duration,Action<float> onLerp=null,Action onLerpFinish=null):ISequence:95；Lerp01(this ISequence self,float duration,Action<float> onLerp=null,Action onLerpFinish=null):ISequence:100 |
| AK-013.CallbackAction，internal class :IAction | F11:13 | S:34-63；private ctor:15；Allocate(Action callback):CallbackAction:24 |
| AK-013.CallbackExtension，public static class | F11:70 | Callback(this ISequence self,Action callback):ISequence:72 |
| AK-014.ConditionAction，public class :IAction | F12:13 | S:34-66；private ctor:21；Allocate(Func<bool> condition,Action onCondition=null):ConditionAction:23 |
| AK-014.ConditionExtension，public static class | F12:73 | Condition(this ISequence self,Func<bool> condition):ISequence:75 |
| AK-015.ConditionSequence，public class :IAction | F13:13 | S:35-84；private ctor:22；Allocate(Func<bool> condition,Action<ISequence> sequenceSetting):ConditionSequence:24 |
| AK-015.ConditionSequenceExtension，public static class | F13:91 | ConditionSequence(this ISequence self,Func<bool> condition,Action<ISequence> sequenceSetting):ISequence:93 |
| AK-016.ISequence，public interface :IAction | F14:14 | S继承；Append(IAction action):ISequence:16 |
| AK-016.Sequence，internal class :ISequence | F14:19 | S:41-136、Append:113；private ctor:25；Allocate():Sequence:32 |
| AK-016.SequenceExtension，public static class | F14:148 | Sequence(this ISequence self,Action<ISequence> sequenceSetting):ISequence:150 |
| AK-017.IParallel，public interface :ISequence | F15:15 | 完整继承S/Append，无新成员 |
| AK-017.Parallel，internal class :IParallel | F15:19 | S:38-97、Append:71；private ctor:21；Allocate():Parallel:30 |
| AK-017.ParallelExtension，public static class | F15:110 | Parallel(this ISequence self,Action<ISequence> parallelSetting):ISequence:112 |
| AK-018.IRepeat，public interface :ISequence | F16:13 | 完整继承S/Append，无新成员 |
| AK-018.Repeat，public class :IRepeat | F16:17 | S:42-101、Append:83；private ctor:27；Allocate(int repeatCount=-1):Repeat:31 |
| AK-018.RepeatExtension，public static class | F16:110 | Repeat.1(this ISequence self,Action<IRepeat> repeatSetting):ISequence:112；Repeat.2(this ISequence self,int repeatCount,Action<IRepeat> repeatSetting):ISequence:119 |
| AK-019.ICustomAPI<TData>，public interface无约束 | F17:13 | Data:TData{get;set;}:15；OnStart(Action onStart):ICustomAPI<TData>:17；OnExecute(Action<float> onExecute):ICustomAPI<TData>:18；OnFinish(Action onFinish):ICustomAPI<TData>:19；Finish():void:21 |
| AK-019.Custom<TData>，internal class :IAction,ICustomAPI<TData> | F17:24 | S:48-83；Data:26、全部ICustomAPI成员:88-106；protected fields mOnStart:Action:28、mOnExecute:Action<float>:29、mOnFinish:Action:30；protected ctor:35；Allocate():Custom<TData>:39；Deinit virtual:50 |
| AK-019.Custom，internal class :Custom<object> | F17:112 | 继承上行全部；protected ctor:117；new static Allocate():Custom:121；override Deinit():void:129 |
| AK-019.CustomExtension，public static class | F17:144 | Custom.1(this ISequence self,Action<ICustomAPI<object>> onCustomSetting):ISequence:146；Custom.2<TData>(this ISequence self,Action<ICustomAPI<TData>> onCustomSetting):ISequence:153，无约束 |
| AK-020.CoroutineAction，internal class :IAction | F18:14 | S:33-66；private ctor:21；Allocate(Func<IEnumerator> coroutineGetter):CoroutineAction:23 |
| AK-020.CoroutineExtension，public static class | F18:71 | Coroutine(this ISequence self,Func<IEnumerator> coroutineGetter):ISequence:73；ToAction(this IEnumerator self):IAction:78 |
| AK-021.TaskAction，public class :IAction | F19:14 | S:36-81；private ctor:22；Allocate(Func<Task> taskGetter):TaskAction:26 |
| AK-021.TaskExtension，public static class | F19:86 | Task(this ISequence self,Func<Task> taskGetter):ISequence:88；ToAction(this Task self):IAction:93 |
| AK-022.AudioSourceShortCutExtensions，public static class | F20:13 | PlayAction(this UnityEngine.AudioSource self):IAction:15 |
| AK-023.ScreenTransition，ActionKit中public nested static class | F21:7 | FadeIn():ScreenTransitionFade:9；FadeOut():ScreenTransitionFade:16；FadeInOut():ScreenTransitionInOut<ScreenTransitionFade,ScreenTransitionFade>:23 |
| AK-023.ScreenTransitionFade，public class :AbstractAction<ScreenTransitionFade> | F22:5 | 基类全部/继承Allocate():ScreenTransitionFade；Color(Color color):self:13；FromAlpha(float alpha):self:19；ToAlpha(float alpha):self:25；Duration(float duration):self:31；override OnStart/OnExecute/OnFinish:37/45/59 |
| AK-024.ScreenTransitionInOut<TIn,TOut>，public class :AbstractAction<self> where TIn:IAction where TOut:IAction | F23:6-8 | 基类全部/继承Allocate.1():self；Allocate.2(TIn transitionIn,TOut transitionOut):self:17；OnInFinish(Action onInFinish):self:25；In(Action<TIn> onSetIn):self:31；Out(Action<TOut> onSetOut):self:37；override OnStart():void:43 |
| AK-025.ScreenTransitionCanvas，internal class :MonoBehaviour,ISingleton | F24:6-7 | internal static Instance{get;}:9；public Image ColorImage:13；public OnSingletonInit():void:15；原MonoSingletonPath元数据:6，不是外部public类型 |

### 5.3 内部驱动与旧引擎的完整声明

```text
AK-006.MonoUpdateActionExecutor @F04:15 internal class :MonoBehaviour,IActionExecutor
  Execute(IActionController controller,Action<IActionController> onFinish=null):void @38
  nested public class ActionTask @17（有效internal）：Action:IAction @19；Controller:IActionController @20；OnFinish:Action<IActionController> @21
AK-007.IActionQueueCallback @F05:6 internal interface：Call():void @8
AK-007.ActionQueueRecycleCallback<T> @11 internal struct无约束
  public Pool:SimpleObjectPool<T> @13；Action:T @14；ctor(SimpleObjectPool<T> pool,T action) @16；Call():void @22
AK-007.ActionQueue @30 internal class :MonoBehaviour,ISingleton，MonoSingletonPath @30
  public static AddCallback(IActionQueueCallback):void @38；Append(IDeprecateAction):void @43；显式ISingleton.OnSingletonInit():void @67
AK-008.ActionKitMonoBehaviourEvents @F06:15 internal class :MonoSingleton<self>
  internal readonly EasyEvent OnUpdate/OnFixedUpdate/OnLateUpdate/OnGUIEvent @18-21
  internal readonly EasyEvent<bool> OnApplicationFocusEvent/OnApplicationPauseEvent @22-23；EasyEvent OnApplicationQuitEvent @24
  protected override OnApplicationQuit():void @61；public ExecuteCoroutine(IEnumerator coroutine,Action onFinish):void @67
  Awake/Update/OnGUI/Fixed/Late/Focus/Pause/Quit及wrapper @26-76 都保留实现职责

AK-026.DeprecateActionKit @F25:20 public class（无自行public成员）
  private RuntimeInitializeOnLoadMethod InitNodeSystem @22-30，Delay/Event预热50
AK-026.IResetable @546 public interface：Reset():void @548
AK-027.IDeprecateAction @1383 public interface :IDisposable
  Disposed:bool{get;} @1385；Execute(float delta):bool @1387；Reset():void @1389；Finish():void @1391；Finished:bool{get;} @1393；继承Dispose():void
AK-026.ActionData @1397 public Serializable class
  public SerializeField ActionName:string @1399；AcitonData:string @1400
AK-026.INode @1403 public interface：CurrentExecutingNode:IDeprecateAction{get;} @1405
AK-027.ActionKitAction @1409 public Serializable abstract class :IDeprecateAction
  OnBeganCallback:Action=null @1411；OnEndedCallback:Action=null @1412；OnDisposedCallback:Action=null @1413
  protected mOnBeginCalled:bool=false @1415；mDisposed:bool=false @1424
  显式IDeprecateAction.Disposed{get;} @1419；Finished:bool{get;protected set;} @1426
  public virtual Finish():void @1428；Break():void @1433；Reset():void @1442；Execute(float dt):bool @1455；Dispose():void @1521
  protected virtual OnReset/OnBegin/OnExecute(float dt)/OnEnd/OnDispose():void @1496/1500/1507/1511/1515
L = 上述旧基类全部公开/保护/显式接口成员，子ID按具体派生类型展开
AK-026.OnlyUsedByCodeAttribute @1541 public class :Attribute，无自行成员
AK-026.ActionGroupAttribute @1545 public class :Attribute
  public readonly GroupName:string @1547；ctor(string groupName) @1549
```

| 子ID前缀 / public类型 | F25声明行 | 全部自身成员、构造、钩子和接口 |
|---|---:|---|
| AK-028.DelayAction :ActionKitAction,IPoolable,IResetable | 39 | L；SerializeField DelayTime:float:41；OnDelayFinish:Action{get;set;}:43；CurrentSeconds:float{get;set;}:45；Allocate(float delayTime,Action onDelayFinish=null):DelayAction:47；protected override OnReset/OnExecute(float)/OnDispose:56/61/71；OnRecycled():void:76；IsRecycled:bool{get;set;}:83 |
| AK-028.DelayActionExtensions static | 86 | Delay.1<T>(this T selfBehaviour,float seconds,Action delayEvent):IDeprecateAction where T:MonoBehaviour:88；Delay.2(this IActionChain selfChain,float seconds):IActionChain:96 |
| AK-029.DelayFrameAction :ActionKitAction,IPoolable,IResetable | 108 | L；SerializeField FrameCount:int:110；OnDelayFrameFinish:Action{get;set;}:112；CurrentSeconds:float{get;set;}:114；Allocate(int frameCount,Action onDelayFrameFinish=null):self:116；protected overrides OnReset/OnBegin/OnExecute(float)/OnDispose:125/132/139/149；OnRecycled:154；IsRecycled:161 |
| AK-029.DelayFrameActionExtensions static | 164 | DelayFrame.1<T>(this T selfBehaviour,int frameCount,Action delayFrameEvent):void:166；NextFrame.1<T>(this T selfBehaviour,Action nextFrameEvent):void:172，均where T:MonoBehaviour；DelayFrame.2(this IActionChain selfChain,int frameCount):IActionChain:178；NextFrame.2(this IActionChain selfChain):IActionChain:183 |
| AK-030.EventAction :ActionKitAction,IPoolable | 194 | L；Allocate(params Action[] onExecuteEvents):self:203；protected overrides OnExecute(float)/OnDispose:213/223；OnRecycled():void:228；IsRecycled{get;set;}:234 |
| AK-031.OnlyBeginAction :ActionKitAction,IPoolable,IPoolType | 238 | L；Allocate(Action<OnlyBeginAction> beginAction):self:242；OnRecycled():void:251；protected override OnBegin:256；IsRecycled{get;set;}:264；Recycle2Cache():void:266 |
| AK-032.UntilAction :ActionKitAction,IPoolable | 277 | L；Allocate(Func<bool> condition):self:281；protected overrides OnExecute(float)/OnDispose:288/293；显式IPoolable.OnRecycled:298、IsRecycled{get;set;}:304 |
| AK-033.Timeline :ActionKitAction | 383 | L；OnTimelineBeganCallback:Action{get;set;}:387；OnTimelineEndedCallback:Action{get;set;}:393；OnKeyEventsReceivedCallback:Action<string>=null:399；TimelineQueue:Queue<TimelinePair>:416；protected overrides OnReset/OnExecute(float)/OnDispose:418/428/459；ctor(params TimelinePair[] pairs):441；Append.1(TimelinePair pair):void:449；Append.2(float time,IDeprecateAction node):void:454 |
| AK-033.Timeline.TimelinePair nested | 401 | Time:float:403；Node:IDeprecateAction:404；ctor(float time,IDeprecateAction node):406 |
| AK-033.KeyEventAction :ActionKitAction,IPoolable | 471 | L；Allocate(string eventName,Timeline timeline):self:476；protected overrides OnBegin/OnDispose:486/495；OnRecycled():void:500；IsRecycled{get;set;}:506 |
| AK-034.AsyncNode :ActionKitAction | 509 | L；mActions:HashSet<IDeprecateAction>:511；Add(IDeprecateAction action):void:513；protected override OnExecute(float dt):518 |
| AK-035.QueueNode :ActionKitAction | 528 | L；Enqueue(IDeprecateAction action):void:532；protected override OnExecute(float dt):537 |
| AK-036.RepeatNode :ActionKitAction,INode | 552 | L；ctor(IDeprecateAction node,int repeatCount=-1):554；CurrentExecutingNode{get;}:560；RepeatCount:int=1:572；protected overrides OnReset/OnExecute(float)/OnDispose:576/587/611 |
| AK-037.SequenceNode :ActionKitAction,INode,IResetable | 625 | L；protected mNodes/mExcutingNodes:List<IDeprecateAction>:627/628；TotalCount:int{get;}:630；CurrentExecutingNode{get;}:635；protected overrides OnReset/OnExecute(float)/OnDispose:645/655/702；protected virtual OnCurrentActionFinished():void:682；ctor(params IDeprecateAction[] nodes):686；Append(IDeprecateAction appendedNode):SequenceNode:695 |
| AK-038.SpawnNode :ActionKitAction | 726 | L；protected mNodes:List<ActionKitAction>:728；protected overrides OnReset/OnExecute(float)/OnDispose:730/746/783；public override Finish():void:736；ctor(params ActionKitAction[] nodes):763；Add(params ActionKitAction[] nodes):void:773 |

### 5.4 全部旧Chain、FSM与Ease

```text
AK-039.IDisposeWhen @F25:1626 public interface :IDisposeEventRegister
  DisposeWhen(Func<bool> condition):IDisposeEventRegister @1628
AK-039.IDisposeEventRegister @1631 public interface
  OnDisposed(Action onDisposedEvent):void @1633；OnFinished(Action onFinishedEvent):IDisposeEventRegister @1635
AK-039.IActionChain @1704 public interface :IDeprecateAction
  Executer:MonoBehaviour{get;set;} @1706；Append(IDeprecateAction node):IActionChain @1708；Begin():IDisposeWhen @1710
AK-039.ActionChain @1713 public abstract class :ActionKitAction,IActionChain,IDisposeWhen
  L；Executer @1715；protected abstract mNode:ActionKitAction{get;} @1717；public abstract Append(...) @1719
  protected override OnExecute(float)/OnEnd/OnDispose @1721/1734/1740
  Begin():IDisposeWhen @1755；DisposeWhen(Func<bool>):IDisposeEventRegister @1765
  显式IDisposeEventRegister.OnFinished(Action):IDisposeEventRegister @1772；OnDisposed(Action):void @1778
AK-039.SequenceNodeChain @1558 public class :ActionChain
  protected override mNode{get;} @1560；public ctor() @1567；override Append(...):IActionChain @1572；protected override OnDispose @1578
AK-039.RepeatNodeChain @1587 public class :ActionChain
  protected override mNode{get;} @1589；public ctor(int repeatCount) @1598；override Append(...):IActionChain @1604；protected override OnDispose @1610
AK-027.IActionExtension @1638 public static class
  ExecuteNode<T>(this T selBehaviour,IDeprecateAction commandNode):T where T:MonoBehaviour @1640
  Execute(this IDeprecateAction selfNode):IEnumerator @1648
AK-039.IActionChainExtention @1659 public static partial class（原拼写）
  Repeat<T>(this T selfbehaviour,int count=-1):IActionChain where T:MonoBehaviour @1661
  Sequence<T>(this T selfbehaviour):IActionChain where T:MonoBehaviour @1668
  OnlyBegin(this IActionChain selfChain,Action<OnlyBeginAction> onBegin):IActionChain @1675
  Wait(this IActionChain senfChain,float seconds):IActionChain @1687
  Event(this IActionChain selfChain,params Action[] onEvents):IActionChain @1692
  Until(this IActionChain selfChain,Func<bool> condition):IActionChain @1698
AK-039.OnDestroyDisposeTrigger @307 internal MonoBehaviour：public AddDispose(IDisposable):void @311
AK-039.OnDisableDisposeTrigger @334 internal MonoBehaviour：public AddDispose(IDisposable):void @338
AK-039.IDisposableExtensions @361 internal static：DisposeWhenGameObjectDestroyed(this IDisposable self,Component component):void @368

AK-040.ActionKitFSM @796 public class
  CurrentState:ActionKitFSMState{get;private set;} @798；PreviousStateType:Type{get;private set;} @799
  Update():void @801；FixedUpdate():void @809；AddState(ActionKitFSMState):void @821；AddTransition(ActionKitFSMTransition):void @826
  HandleEvent<TTransition>():bool where TTransition:ActionKitFSMTransition @831
  ChangeState.1<TState>():void where TState:ActionKitFSMState @850；ChangeState.2(Type stateType):void @855
  BackToPreviousState():void @866；StartState<T>():void @871（无约束）
AK-040.ActionKitFSMState @878 public class
  Enter/Update/FixedUpdate/Exit():void @880/885/890/895
  protected virtual OnEnter/OnUpdate/OnFixedUpdate/OnExit():void @900/904/908/912
AK-040.ActionKitFSMState<T> @917 public class :ActionKitFSMState（无约束）
  ctor(T target) @919；protected mTarget:T @924；Target:T{get;} @926；继承上行全部
AK-040.ActionKitFSMTransition @932 public class
  virtual SrcStateTypes:HashSet<Type>{get;set;} @934；virtual DstStateType:Type{get;set;} @936
  virtual OnTransition(ActionKitFSMState srcState,ActionKitFSMState dstState):void @938
  AddSrcState<T>():ActionKitFSMTransition where T:ActionKitFSMState @942
  WithDstState<T>():ActionKitFSMTransition where T:ActionKitFSMState @959
AK-040.ActionKitFSMTransition<TSrcState,TDstState> @967 public class :ActionKitFSMTransition（无约束）
  override SrcStateTypes{get;} @972、DstStateType{get;} @977；其余继承
AK-040.ActionKitFSMTransitionTable @1071 public class :Table<ActionKitFSMTransition>
  public TypeIndex:TableIndex<Type,ActionKitFSMTransition> @1073
  protected overrides OnAdd(item)/OnRemove(item)/OnClear()/OnDispose() @1076/1081/1086/1096
  public override GetEnumerator():IEnumerator<ActionKitFSMTransition> @1091
  Table继承Add/Remove/Clear/Update/GetEnumerator/Dispose与索引成员映射见5.5

AK-041.EnumStateChangeEvent<T> @983 public struct where T:struct,IComparable,IConvertible,IFormattable
  Target:GameObject @985；TargetStateMachine:EnumStateMachine<T> @986；NewState:T @987；PreviousState:T @988
  ctor(EnumStateMachine<T> stateMachine) @990
AK-041.IEnumStateMachine @999 public interface：TriggerEvents:bool{get;set;} @1001
AK-041.EnumStateMachine<T> @1004 public class :IEnumStateMachine，同上约束
  TriggerEvents:bool{get;set;} @1013；Target:GameObject @1016；CurrentState:T{get;protected set;} @1019；PreviousState同型 @1022
  ctor(GameObject target,bool triggerEvents) @1029；virtual ChangeState(T newState):void @1039；virtual RestorePreviousState():void @1060

AK-043.FSM<TStateEnum,TEventEnum> @1105 public class :IDisposable（无约束）
  ctor(Action<TStateEnum,TStateEnum> onStateChanged=null) @1109
  public delegate FSMOnStateChanged(params object[] param) @1117
  State:TStateEnum{get;} @1163
  AddTransition(TStateEnum fromState,TEventEnum name,TStateEnum toState,Action<object[]> onStateChagned=null):void @1190
  Start(TStateEnum name):void @1211；HandleEvent(TEventEnum name,params object[] param):void @1221；Clear():void @1244；Dispose():void @1259
AK-043.FSM.FSMState<TName> @1122 public nested class
  Name:TName @1124；ctor(TName name) @1126；readonly TranslationDict:Dictionary<TEventEnum,FSMTranslation<TName,TEventEnum>> @1134
AK-043.FSM.FSMTranslation<TStateName,KEventName> @1141 public nested class
  FromState:TStateName @1143；Name:KEventName @1144；ToState:TStateName @1145；OnTranslationCallback:Action<object[]> @1146
  ctor(TStateName fromState,KEventName name,TStateName toState,Action<object[]> onStateChagned) @1148
AK-044.QFSMLite @1269 public class
  public delegate FSMCallfunc(params object[] param) @1274；State:string{get;} @1319
  AddState(string name):void @1333；AddTranslation(string fromState,string name,string toState,FSMCallfunc callfunc):void @1345
  Start(string name):void @1354；HandleEvent(string name,params object[] param):void @1364；Clear():void @1377
AK-044.QFSMLite.QFSMTranslation @1298 public nested class
  FromState:string @1300；Name:string @1301；ToState:string @1302；OnTranslationCallback:FSMCallfunc @1303
  ctor(string fromState,string name,string toState,FSMCallfunc onTranslationCallback) @1305
AK-044.QFSMLite.QFSMState @1279 private nested class（不误报外部API）
  Name:string @1281；ctor(string name) @1283；readonly TranslationDict:Dictionary<string,QFSMTranslation> @1291
```

Ease参数共同定义为`float start,float end,float pos`，三个参数名/类型不缩写成不同公开契约。下面每个方法只有一个重载，返回float；public EaseUtility是普通class（不是静态class）。

| 稳定族/子方法 | F26声明行 | 额外参数/完整枚举约束 |
|---|---|---|
| AK-045.Linear | 53 | 无额外参数 |
| AK-046.InOutBack/InBack/OutBack | 58/66/72 | 每个追加float bounciness=1.5f |
| AK-047.InOutBounce/OutBounce/InBounce | 79/86/107 | 每个追加float bounciness=7.5625f |
| AK-048.InOutCircle/OutCircle/InCircle | 115/122/129 | 无 |
| AK-049.InOutCubic/OutCubic/InCubic | 135/142/148 | 无 |
| AK-050.InOutElastic/OutElastic/InElastic | 154/161/175 | 每个追加float elasticity=.3f,float duration=5.0f |
| AK-051.InOutExpo/OutExpo/InExpo | 191/198/204 | 无 |
| AK-052.InOutQuad/OutQuad/InQuad | 210/217/223 | 无 |
| AK-053.InOutQuart/OutQuart/InQuart | 229/236/242 | 无 |
| AK-054.InOutQuint/OutQuint/InQuint | 248/255/261 | 无 |
| AK-055.InOutSine/OutSine/InSine | 267/273/278 | 无 |

AK-045.EaseType（F26:14）值0-31顺序冻结：Linear、InOutBack、InBack、OutBack、InOutBounce、OutBounce、InBounce、InOutCircle、OutCircle、InCircle、InOutCubic、OutCubic、InCubic、InOutElastic、OutElastic、InElastic、InOutExpo、OutExpo、InExpo、InOutQuad、OutQuad、InQuad、**InoutQuart**、OutQuart、InQuart、InOutQuint、OutQuint、InQuint、InOutSine、OutSine、InSine、Count。全部31方法/数值需黄金测试；没有本地EaseType→曲线dispatch实现。

### 5.5 外露支撑的明确调用映射

| 稳定子ID / 原定义 | 全部相关表面 | 对应实现 / 必要差异 |
|---|---|---|
| AK-042.Architecture，F:70-205 | OnRegisterPatch:Action<T>:75；Interface:IArchitecture{get;}:79；InitArchitecture():89；Deinit():117；RegisterSystem<TSystem>(TSystem) where TSystem:ISystem:132；RegisterModel<TModel>(TModel) where TModel:IModel:146；RegisterUtility<TUtility>(TUtility) where TUtility:IUtility:160；GetSystem<T>() where T:class,ISystem:167；GetModel<T>() where T:class,IModel:169；GetUtility<T>() where T:class,IUtility:171 | 初始化/关闭→模块Initialize/Shutdown；Interface/补丁/通用注册角色不能假等价，迁其消费者，不重建IOC。ActionKit保留public class/隐式构造概念，不能随手改static类断掉所有实例用法 |
| AK-042.ArchitectureDispatch，F:173-205 | SendCommand<TResult>(ICommand<TResult>):173；SendCommand<TCommand>(TCommand) where TCommand:ICommand:175；SendQuery<TResult>(IQuery<TResult>):189；SendEvent<TEvent>() where new():199；SendEvent<TEvent>(TEvent):201；RegisterEvent<TEvent>(Action<TEvent>):IUnRegister:203；UnRegisterEvent<TEvent>(Action<TEvent>):205 | 原Command/Query/容器公共角色是底层依赖，不重建；事件消费者迁明确模块scope/原所属框架，不能保留旧类型签名当空壳 |
| AK-042.ArchitectureProtected，F:77/115/126/177/183/191 | static mArchitecture:T；abstract Init；virtual OnDeinit；ExecuteCommand<TResult>(ICommand<TResult>)、ExecuteCommand(ICommand)、DoQuery<TResult>(IQuery<TResult>) | 保留模块自身初始化/关闭派生钩子；通用分派保护面类型变化单列待确认 |
| AK-056.PoolProtocol | CORE/PoolKit/Scripts/Pool/IPoolable.cs:14：OnRecycled():void、IsRecycled:bool{get;set;}；IPoolType.cs:14：Recycle2Cache():void | 新模块所属同概念接口；原SafeObjectPool.Instance整工具级配置不隐式继续可用，必要warm/max/factory由模块pool配置映射 |
| AK-040.TransitionTable，CORE/TableKit/Script/TableKit.cs:80-120 | Add(TDataItem)、Remove(TDataItem)、Clear、Update(原空)、GetEnumerator、Dispose；protected OnAdd/OnRemove/OnClear/OnDispose | FSM领域表保持Add/Remove/Clear/枚举/释放/派生钩子；不提取完整TableKit；Update原空载体标H |
| AK-040.TransitionTypeIndex，同文件:123-200 | ctor(Func<TDataItem,TKeyType> keyGetter):130；Dictionary:IDictionary<TKeyType,List<TDataItem>>{get;}:135；Add(item):140；Remove(item):158；Get(key):IEnumerable<item>:165；Clear:178；Dispose:189 | 新TransitionTypeIndex替TableIndex，保留所有可继续操作能力与类型桶；泛型返回类型身份改变，不假称旧显式赋值兼容 |
| AK-057.Subscription，F:468-611 | IUnRegister.UnRegister；IUnRegisterList.UnregisterList；AddToUnregisterList、UnRegisterAll；CustomUnRegister(Action)及UnRegister；Destroy.1(GameObject)/.2<T:Component>、Disable.1(GameObject)/.2<T:Component>、CurrentSceneUnloaded | 新Project.ActionKit所属小token/trigger；接口/struct身份不同，生命周期能力完整。二次注销原问题AK927 |
| AK-057.EasyEvent，F:795-841 | IEasyEvent.Register(Action):IUnRegister；EasyEvent.Register/UnRegister/Trigger/RegisterWithACall；EasyEvent<T>.Register(Action<T>)/UnRegister/Trigger(T)及显式IEasyEvent.Register(Action) | 小多播/初值/无参接口，保留返回token；无需整个事件框架 |
| AK-057.Or，F:921-957 | OrEvent.Or(IEasyEvent):self、Register(Action)、RegisterWithACall、UnRegister、UnregisterList；OrEventExtensions.Or(this IEasyEvent,IEasyEvent):OrEvent | 保留组合能力、scope/注销契约；原卸一个listener就卸全部source单列AK927 |
| AK-041.StateEvents，F:624-641 | TypeEventSystem.Global；Send<T>() where new()/Send<T>(T)；Register<T>(Action<T>):IUnRegister；UnRegister<T>(Action<T>) | 模块ActionStateEvents提供旧Enum状态事件的实际监听/发布/注销；新scope不自动到旧全局bus，外部监听显式迁移 |

外部EasyEvent的2/3参数族、完整IOC/Query/EventKit/PoolKit并非模块原声明或返回闭包的必需全集，不额外整体提取。这里保留的是可达模块能力及其必要公共操作，而不是保留原框架全部角色。

### 5.6 构造与条件分支覆盖

隐式public无参构造的真实类型（以类型声明定位，不能伪造构造源码行）：ActionKit、ActionController、ActionKitCurrentScene、Lerp、ScreenTransitionFade、ScreenTransitionInOut<TIn,TOut>、EaseUtility、DeprecateActionKit、DelayAction、DelayFrameAction、EventAction、OnlyBeginAction、UntilAction、KeyEventAction、AsyncNode、QueueNode、ActionKitFSM、ActionKitFSMState、ActionKitFSMTransition、ActionKitFSMTransition<TSrcState,TDstState>、ActionKitFSMTransitionTable、QFSMLite、ActionData、OnlyUsedByCodeAttribute。ActionKitAction/ActionChain隐式protected构造；前面有显式构造的类型按声明保留。Unity组件必须AddComponent/Prefab管理，语言能new不代表有效Unity用法。

Action本体仅F01的UNITY_EDITOR文档块（15-20、25-43、50-70、76-103、126-147、155-181、188-209、215-252、258-322、338-358、364-385、395-408、412-423、426-437、440-451、454-464、467-477、482-492），不屏蔽算法。旧文件无Obsolete/if屏蔽；Deprecate.Init默认RuntimeInitialize入口保留职责。底层Unity注销provider有UNITY_5_6_OR_NEWER/GODOT分支，不把它虚构为当前动作引擎提供Godot后端。Player必须不依赖这些Editor文档provider。

### 5.7 Audio 门面全部签名与设置

下列均为原AU路径的有效public声明，迁移目标是新Audio Runtime；AU033的声明只放可选Integration。原两个公开模型基类的底层角色另列，不用删属性或假实现IModel掩盖。

```text
AU-035.AudioKit.Type @AU/AudioKit.cs:20 public static class AudioKit
AU-003.MusicPlayer @22 static MusicPlayer MusicPlayer {get;}
AU-005.VoicePlayer @23 static MusicPlayer VoicePlayer {get;}
AU-018.Config @25 static AudioLoaderPoolModel Config {get;}
AU-003.PlayMusic.1 @43 static void PlayMusic(string musicName,bool loop=true,Action onBeganCallback=null,Action onEndCallback=null,float volume=1f)
AU-003.PlayMusic.2 @189 static void PlayMusic(AudioClip clip,bool loop=true,Action onBeganCallback=null,Action onEndCallback=null,float volume=1f)
AU-004.StopMusic @55 static void StopMusic()
AU-004.PauseMusic @66 static void PauseMusic()
AU-004.ResumeMusic @77 static void ResumeMusic()
AU-005.PlayVoice.1 @89 static void PlayVoice(string voiceName,bool loop=false,Action onBeganCallback=null,Action onEndedCallback=null)
AU-005.PlayVoice.2 @194 static void PlayVoice(AudioClip clip,bool loop=false,Action onBeganCallback=null,Action onEndedCallback=null,float volumeScale=1.0f)
AU-006.PauseVoice @101 static void PauseVoice()
AU-006.ResumeVoice @111 static void ResumeVoice()
AU-006.StopVoice @121 static void StopVoice()
AU-014.PlaySoundModes @126 public nested enum：EveryOne=0、IgnoreSameSoundInGlobalFrames=1、IgnoreSameSoundInSoundFrames=2
AU-014.PlaySoundMode @134 static PlaySoundModes PlaySoundMode {get;set;}
AU-014.SoundFrameCountForIgnoreSameSound @140 static int SoundFrameCountForIgnoreSameSound {get;set;}
AU-014.GlobalFrameCountForIgnoreSameSound @148 static int GlobalFrameCountForIgnoreSameSound {get;set;}
AU-007.PlaySound.1 @169 static AudioPlayer PlaySound(string soundName,bool loop=false,Action<AudioPlayer> callBack=null,float volume=1.0f,float pitch=1,PlaySoundModes? playSoundMode=null)
AU-007.PlaySound.2 @172 static AudioPlayer PlaySound(AudioClip clip,bool loop=false,Action<AudioPlayer> callBack=null,float volume=1.0f,float pitch=1,PlaySoundModes? playSoundMode=null)
AU-008.StopAllSound @184 static void StopAllSound()
AU-015.Settings @233 static AudioKitSettingsModel Settings {get;}
AU-022.Music @237 static FluentMusicAPI Music()
AU-023.Sound @239 static FluentSoundAPI Sound()

AU-005.PlayVoiceWithClip.Type @AU/Architecture/Command/Voice/PlayVoiceWithClip.cs:15 public class
AU-005.PlayVoiceWithClip.Execute @17 static void Execute(AudioClip clip,bool loop=false,Action onBeganCallback=null,Action onEndedCallback=null,float volumeScale=1.0f)

AU-015.AudioKitSettingsModel.Type @AU/AudioKitSettings.cs:15 public class :AbstractModel
  IsSoundOn:PlayerPrefsBooleanProperty{get;private set;} @25
  IsMusicOn:PlayerPrefsBooleanProperty{get;private set;} @27
  IsVoiceOn:PlayerPrefsBooleanProperty{get;private set;} @29
  SoundVolume:PlayerPrefsFloatProperty{get;private set;} @31
  MusicVolume:PlayerPrefsFloatProperty{get;private set;} @33
  VoiceVolume:PlayerPrefsFloatProperty{get;private set;} @35
  IsOn:CustomProperty<bool>{get;private set;} @37
  protected override OnInit():void @38
AU-016.OnPropertyChangedEvent<T>.Type @AU/QFramework.BindableProperty.cs:16 public class :UnityEvent<T>
  无新增成员，保留继承UnityEvent<T>监听/移除/Invoke能力
AU-016.CustomProperty<T>.Type @21 public class（无约束）
  Value:T{get;set;} @25
  ctor(Func<T> valueGetter,Action<T> valueSetter=null) @56
  Bind(UnityAction<T> onValueChanged):void @62
```

六个持久化键是源码真实常量而不是推测：AudioKitSettings.cs:17-22的KEY_AUDIO_MANAGER_SOUND_ON、KEY_AUDIO_MANAGER_MUSIC_ON、KEY_AUDIO_MANAGER_VOICE_ON（均默认true），KEY_AUDIO_MANAGER_VOICE_VOLUME、KEY_AUDIO_MANAGER_SOUND_VOLUME、KEY_AUDIO_MANAGER_MUSIC_VOLUME（均默认1.0f，:57-61）。测试必须保留旧数据并清楚区分Value、SetValueWithoutEvent和aggregate通知，不使用XF的AudioConfigVolume/AudioConfigMute键替换。

Sound的`callBack`真实注册为**自然结束OnFinish**，不是开始/加载完成回调：PlaySoundCommand.cs:27、PlaySoundWithClipCommand.cs:30。name版本先开始异步准备再注册，Clip版本先注册再准备；这也是立即完成/重入需要验证的时序差异。loop声音与手停不自然触发该callback，不把它改成XF的任意clip加载通知。

### 5.8 Audio 播放器、加载器、链式、Disposable

每行子ID为族.类型.成员名；重载和扩展分别编号。除明确列出的internal支撑外，类型都是public。

| 类型 / 子ID前缀 | 原文件/声明行，相对AU/ | 全部自身成员、继承能力与钩子 |
|---|---|---|
| AU-013.AbstractAudioPlayer abstract | Player/AbstractAudioPlayer.cs:14 | Volume:BindableProperty<float>{get;protected set;}:16；IsLoop:bool{get;set;}:20；AudioName:string{get;internal set;}:26；Stop():void:104；protected OnInit(BindableProperty<float> volume):void:32、OnDeinit:40、abstract OnPlayStarted:98、mPlayedAudio:bool=false:100、abstract OnBeforeStop:113、abstract OnStop:115；internal abstract CanPlayAudio():bool:102。R=以上全部面 |
| AU-012.AudioPlayer :AbstractAudioPlayer,IPoolable,IPoolType | Player/AudioPlayer.cs:17 | R；IsRecycled:bool{get;set;}=false:39；OnRecycled():void:41；Recycle2Cache():void:46；protected overrides OnPlayStarted/OnBeforeStop/OnStop:65/79/87；Allocate:31为内部入口，不能误列为外部public；没有公开instance Play |
| AU-013.MusicPlayer :AbstractAudioPlayer | Player/MusicPlayer.cs:12 | R；ctor(BindableProperty<float> volume,bool isLoop=true):14；Deinit():void:37；protected overrides OnPlayStarted/OnBeforeStop/OnStop:20/27/32 |
| AU-009/010/011.AudioPlayerExtensions static | Architecture/Utility/AudioPlayerExtensions.cs:15 | OnStart<T>(this T self,Action onStart):T:17；OnFinish<T>(this T self,Action onFinish):T:23；Pitch<T>(this T self,float pitch):T:30；VolumeScale<T>(this T self,float volumeScale):T:36，均where T:AbstractAudioPlayer；Pause(this AbstractAudioPlayer self):void:83；Resume(...):void:93 |
| AU-017.IAudioLoader interface | Architecture/Model/AudioLoaderPoolModel/IAudioLoader.cs:15 | Clip:AudioClip{get;}:17；LoadClip(AudioSearchKeys audioSearchKeys):AudioClip:18；LoadClipAsync(AudioSearchKeys audioSearchKeys,Action<bool,AudioClip> onLoad):void:20；Unload():void:21 |
| AU-018.IAudioLoaderPool interface | 同目录/IAudioLoaderPool.cs:12 | AllocateLoader():IAudioLoader:14；RecycleLoader(IAudioLoader loader):void:15 |
| AU-018.AbstractAudioLoaderPool abstract :IAudioLoaderPool | 同目录/AbstractAudioLoaderPool.cs:14 | AllocateLoader():IAudioLoader:18；protected abstract CreateLoader():IAudioLoader:20；RecycleLoader(IAudioLoader loader):void:22 |
| AU-020.DefaultAudioLoader :IAudioLoader | 同目录/DefaultAudioLoader.cs:15 | Clip{get;private set;}:17；LoadClip(AudioSearchKeys panelSearchKeys):AudioClip:19；LoadClipAsync(AudioSearchKeys audioSearchKeys,Action<bool,AudioClip> onLoad):void:25；Unload():void:35 |
| AU-020.DefaultAudioLoaderPool :AbstractAudioLoaderPool | 同目录/DefaultAudioLoaderPool.cs:12 | 继承全部池协议；protected override CreateLoader():IAudioLoader:14 |
| AU-018.AudioLoaderPoolModel :AbstractModel | 同目录/AudioLoaderPoolModel.cs:12 | AudioLoaderPool:IAudioLoaderPool{get;set;}:16；protected override OnInit():void:26；模型生命周期见5.10 |
| AU-019.AudioSearchKeys :IPoolType,IPoolable | 同目录/AudioSearchKeys.cs:12 | fields AssetBundleName:string:14、AssetName:string:16；OnRecycled:18；IsRecycled:bool{get;set;}:24；static Allocate():self:26；Recycle2Cache:28；override ToString():string:30 |
| AU-022.FluentMusicAPI :IPoolable,IPoolType | FluentAPI/FluentMusicAPI.cs:14 | IsRecycled:bool{get;set;}:21；WithName(string name):self:30；WithAudioClip(AudioClip clip):self:37；Loop(bool loop):self:44；VolumeScale(float volumeScale):self:50；Play():void:56；static Allocate():self:68；OnRecycled:70；Recycle2Cache:79 |
| AU-023.FluentSoundAPI :IPoolable,IPoolType | FluentAPI/FluentSoundAPI.cs:14 | IsRecycled:bool{get;set;}:30；static Allocate():self:32；OnRecycled:34；Recycle2Cache:40；WithName(string name):self:42；WithAudioClip(AudioClip clip):self:49；Loop(bool loop):self:56；VolumeScale(float volumeScale):self:62；Pitch(float pitch):self:68；PlaySoundMode(AudioKit.PlaySoundModes playSoundModes):self:74；Play():AudioPlayer:81 |
| AU-025.CustomDisposable :IDisposable | Disposable/CustomDisposable.cs:34 | ctor(Action onDispose):45；Dispose():void:53 |
| AU-025.IDisposableExtensions static | Disposable/IDisposableExtension.cs:31 | DisposeWhenGameObjectDestroyed(this IDisposable self,Component component):void:38 |
| AU-025.OnDestroyDisposeTrigger :MonoBehaviour | Disposable/Trigger/OnDestroyDisposeTrigger.cs:34 | AddDispose(IDisposable disposable):void:38；HashSet/destroy行为完整保留 |

### 5.9 Audio 两套Timer、Heap与可选音频动作

```text
AU-026.BinaryHeapBuildMode @AU/Timer/BinaryHeap.cs:31 enum {kNLog=1,kN=2}
AU-026.BinaryHeapSortMode @37 enum {kMin=0,kMax=1}
AU-026.BinaryHeap<T> @44 public class where T:IBinaryHeapElement
  ctor.1(int minSize,BinaryHeapSortMode sortMode) @52
  ctor.2(T[] dataArray,BinaryHeapSortMode sortMode) @59
  Clear():void @77
  Insert.1(T[] dataArray,BinaryHeapBuildMode buildMode):void @87
  Insert.2(T element):void @121
  Pop():T @144；Top():T @157；Sort(BinaryHeapSortMode sortMode):void @171
  RebuildAtIndex(int index):void @181；RemoveAt(int index):void @236
  GetElement(int index):T @260；HasValue():bool @273
  protected mArray:T[] @46；mGrowthFactor:float=1.6f @48；mLastChildIndex:int @49；mSortMode:BinaryHeapSortMode @50
  protected ResizeArray(int newSize=-1):void @282；ProcolateUp(int index):void @306；ProcolateDown(int index):void @342
AU-026.IBinaryHeapElement @AU/Timer/IBinaryHeapElement.cs:29 interface
  SortScore:float{get;} @31；HeapIndex:int{set;} @33
  RebuildHeap<T>(BinaryHeap<T> heap):void where T:IBinaryHeapElement @35
AU-027.TimeItem @AU/Timer/TimeItem.cs:10 class :IBinaryHeapElement,IPoolable,IPoolType
  static Allocate(Action<int> callback,float delayTime,int repeatCount=1):TimeItem @25
  Set(Action<int> callback,float delayTime,int repeatCount):void @32；OnTimeTick():void @40
  callback:Action<int>{get;} @53；SortScore:float{get;set;} @58；HeapIndex:int{get;set;} @64
  isEnable:bool{get;} @70；IsRecycled:bool{get;set;} @75
  Cancel():void @88；NeedRepeat():bool @97；DelayTime():float @106
  RebuildHeap<T>(BinaryHeap<T> heap):void where T:IBinaryHeapElement @111
  OnRecycled():void @116；Recycle2Cache():void @124
AU-027.Timer @AU/Timer/Timer.cs:13 class :MonoBehaviour,ISingleton [旧QFramework.Timer]
  static Instance:Timer{get;} @15
  currentScaleTime:float{get;} @22；currentUnScaleTime:float{get;} @27
  OnSingletonInit():void @32；ResetMgr():void @41；StartMgr():void @47
  Post2Scale.1(Action<int> callback,float delay,int repeat):TimeItem @55
  Post2Scale.2(Action<int> callback,float delay):TimeItem @62
  Post2Scale.3(TimeItem item):void @69
  Post2Really.1(Action<int> callback,DateTime toTime):TimeItem @80
  Post2Really.2(Action<int> callback,float delay,int repeat):TimeItem @91
  Post2Really.3(Action<int> callback,float delay):TimeItem @98
  Post2Really.4(TimeItem item):void @105
  Update():void @113；UpdateMgr():void @118；Dump():void @193 [原空方法，H]
AU-028.TimerHelper @AU/Timer/TimerHelper.cs:11 class
  Add(TimeItem item):void @16；Clear():void @31
  protected m_TimeItemList:List<TimeItem> @13；m_IsUseAble:bool=true @14

AU-029.TimeExtend.Timer @AU/Timer/TimerManager.cs:8 class [旧QFramework.TimeExtend.Timer]
  public static driver:TimerDriver=null @30
  static ShowLog:bool{set;} @56；Duration:float{get;} @60；IsPause:bool{get;set;} @64
  private ctor(float time,string flag,bool loop=false,bool ignorTimescale=true) @87 [不得改成public]
  static AddTimer(float time,string flag="",bool loop=false,bool ignorTimescale=true):Timer @189
  static UpdateAllTimer():void @198
  static Exist.1(string flag):bool @215；Exist.2(Timer timer):bool @223；GetTimer(string flag):Timer @233
  static Pause.1(string flag):void @245；Pause.2(Timer timer):void @261
  static Resum.1(string flag):void @276；Resum.2(Timer timer):void @292 [原拼写]
  static DelTimer.1(string flag):void @309；DelTimer.2(Timer timer):void @325
  static DelTimer.3(Action completedEvent):void @340；DelTimer.4(Action<float> updateEvent):void @356
  static RemoveAll():void @372
  AddEvent.1(Action completedEvent):void @383；AddEvent.2(Action<float> updateEvent):void @398
  SetTime(float endTime):Timer @421；Setloop(bool loop):Timer @453；SetIgnoreTimeScale(bool ignoreTimescale):Timer @470
AU-030.TimeExtend.TimerDriver @489 class :MonoBehaviour
  public static Get:TimerDriver{get;private set;} @493
AU-030.TimeExtend.TimerExtend @515 static class
  OnCompleted(this Timer timer,Action completedEvent):Timer @523
  OnUpdated(this Timer timer,Action<float> updateEvent):Timer @538

AU-033.PlaySoundAction @AU/Action/PlaySound.cs:15 class :AbstractAction<PlaySoundAction> [有效现代源码]
  Allocate.1(string soundName,Action onFinish=null):PlaySoundAction @29
  Allocate.2(AudioClip sound,Action onFinish=null):PlaySoundAction @38
  Allocate.3():PlaySoundAction 继承AK/F02:71 [不能遗漏无参]
  override OnStart():void @48；override OnFinish():void @68；protected override OnDeinit():void @73
  继承S、AbstractAction.OnExecute(float)/Reset/OnReset/Deinit等完整契约
AU-033.PlaySoundExtension @87 static class
  PlaySound.1(this ISequence self,string soundName):ISequence @89
  PlaySound.2(this ISequence self,AudioClip clip):ISequence @91
```

补充计时行为基线：TimeItem.cs:40-50的callback参数从1开始递增，正repeat逐次减、repeat0仍可先tick一次，负repeat不减。Heap Timer.cs:124-190先unscaled堆再scaled堆，每次重复重新从当前时间+delay投递，不做按旧期限的长帧catch-up；<到期保留作为原条件。TimeExtend.Timer的clock为realtimeSinceStartup或Time.time（TimerManager.cs:34），OnUpdated传`Clamp01(timePassed/duration)`进度而非原始elapsed秒（:145），EndEvent在更新进度之后（:148），loop从当前时间重新计时（:151），Pause/Resum保持timePassed（:128）。同flag只warn仍添加（:94-99/189-193），空flag用GetHashCode字符串；测试不能给它套上XF Timer默认参数或唯一flag字典语义。

### 5.10 Audio 外露属性/生命周期与内部支撑闭包

| 子ID前缀 | 原定义与全部相关成员 | 迁移对应/差异 |
|---|---|---|
| AU-024.IBindableProperty<T> | F:690-694；继承IReadonlyBindableProperty<T>；new Value{get;set;}、SetValueWithoutEvent(T) | Project.AudioKit同能力协议 |
| AU-024.IReadonlyBindableProperty<T> | F:696-703；继承IEasyEvent；Value{get;}、RegisterWithInitValue(Action<T>):IUnRegister、UnRegister(Action<T>)、Register(Action<T>):IUnRegister | 不裁成float；token身份迁所属模块 |
| AU-024.BindableProperty<T> | F:705-760；ctor(T defaultValue=default):707；protected mValue:T:709；static Comparer:Func<T,T,bool>{get;set;}:711；WithComparer(Func<T,T,bool>):self:713；Value:719；SetValueWithoutEvent:736；Register:740；RegisterWithInitValue:745；UnRegister:751；override ToString:759；protected virtual GetValue/SetValue:734/732；显式IEasyEvent.Register(Action):753 | 全部保留；每T静态comparer；float/bool初始化比较职责；相等/null/ToString问题另列AU920 |
| AU-024.PlayerPrefsBooleanProperty | CORE/BindablePropertyKit/PlayerPrefsBoolProperty.cs:13-20；继承BindableProperty<bool>；ctor(string saveKey,bool defaultValue=false):15 | 原GetInt/变化SetInt键和值语义；业务设置默认true不改变该公开构造默认false |
| AU-024.PlayerPrefsFloatProperty | 同目录/PlayerPrefsFloatProperty.cs:13-20；继承BindableProperty<float>；ctor(string saveKey,float defaultValue=0.0f):15 | 原GetFloat/变化SetFloat；设置默认1不改变构造默认0 |
| AU-024.Subscription | F的IEasyEvent/IUnRegister/CustomUnRegister/销毁、停用、scene注销能力，完整签名与AK057相同但**新类型归Audio** | 不复制同全名QFramework类型；不要求Action安装；两个模块token不隐式互换 |
| AU-034.ModelLifecycle | F:264-285；AbstractModel.Initialized{get;set;}:276、Deinit:278；protected OnInit:284/OnDeinit:280；显式GetArchitecture:272/SetArchitecture:274/Init:277；IModel继承IBelongToArchitecture/ICanSetArchitecture/ICanGetUtility/ICanSendEvent/ICanInit | 新Audio模型生命周期/派生钩子保留；IArchitecture/IModel/GetUtility/SendEvent/旧登记是必要类型/框架互操作变化，明确迁消费者而非空接口 |
| AU-012/019/022/023/027.PoolProtocol | 必要IPoolable/IPoolType与AK056一样的成员，Timer还公开实现ISingleton.OnSingletonInit | 每模块自己的小协议，明确新全名；pool功能不借整个CoreKit |
| AU-001.InternalArchitecture | AudioKitArchitecture.cs:14-38 internal；public AutoInit:24但类型internal；静态settings/pool/channel/loader与注册 | 字段直接组织+幂等初始化，不迁成公共Architecture |
| AU-002.InternalManager | AudioManager.cs internal；public OnSingletonInit:21、IArchitecture GetArchitecture:122是internal类型上的旧接口实现 | 保留Listener、三类管理、root创建；取消底层IController角色 |
| AU-008.InternalSoundPool | PlayingSoundPoolModel.cs internal；public ForEachAllSound(Action<AudioPlayer>):47、按名字group/快照 | 保留内部快照/计数，不改全局容器 |
| AU-021.InternalPrepare | 三internal Prepare类均公开PrepareClip(AbstractAudioPlayer,GameObject root,string name,bool loop)/UnPrepareClip；SetUp:23/36、Sync:18/46、Async:18/45；internal IClipPrepareMode/ModeController | 完整3路径与模式选择、async/sync/unload/旧资源释放；不误报这些为public跨程序集接口 |
| AU-011.InternalProxy/LifeCycle | AudioSourceController.cs实际internal AudioSourceProxy；参数、暂停、监听、root/source寿命；Features/AudioPlayerLifeCycle.cs回调累积Once | 所有实际行为保留；ActionEngine替薄监听，非删完成能力 |
| AU-014.InternalChannels | EveryOne、GlobalFrames、SoundFrames及系统/抽象策略；frame阈值/字典/结束通知 | 原三算法、枚举错误默认EveryOne；不是仅保留当前业务GlobalFrames |
| AU-003..008.InternalCommands | Music/Voice/Sound所有内部command及public PlayVoiceWithClip | 内部操作保留，可拆分普通函数/对象而不需要Command架构；public Execute保留 |

隐式public无参构造：AudioKitSettingsModel、AudioLoaderPoolModel、AudioSearchKeys、DefaultAudioLoader、DefaultAudioLoaderPool、AudioPlayer、FluentMusicAPI、FluentSoundAPI、OnPropertyChangedEvent<T>、TimeItem、TimerHelper、PlaySoundAction；位置为各类型声明。AbstractAudioPlayer/AbstractAudioLoaderPool隐式protected构造。MonoBehaviour Timer/TimerDriver/OnDestroyDisposeTrigger无自定义构造，以Unity创建而非new；其它显式/private构造以上逐项列出。

Audio本体条件块是AudioKit.cs中的UNITY_EDITOR文档元数据；模块实现/旧Timer/PlaySoundAction没有因此被屏蔽，PlaySoundAction没有Obsolete。属性provider的UNITY_5_6_OR_NEWER注销分支保留Unity能力；GODOT分支不是Audio当前后端。诊断平台输入分支另见AU801。Player检查必须排除Editor而不排除完整运行API。

## 6. 原问题、历史材料和兼容阻塞

以下保留最初问题基线，**不是当前全部未修复清单**；已修复和实际验证状态以03恢复入口及对应批次为准。未修正的算法/兼容风险仍不因characterization通过而结项，不能据轻量门禁把所有原问题关闭。

### 6.1 Action

| 风险ID / 关联功能 | 原证据与问题 | 拟处理方式/具体验证，不作为已修复 |
|---|---|---|
| AK-900 / 042 | F01:21/F:70-205，去Architecture失去真实继承公共面 | lifecycle明确映射、通用容器消费者单列迁移；不做IArchitecture空壳；E契约/独立程序集 |
| AK-901 / 010 | F08:31-51,58-63,90-97，factory重载不更新ID，fixed/Deinit不清factory | 工厂→回收→fixed/ID复用characterization；拟显式分配重置，契约确认后修正 |
| AK-902 / 019 | F17:121-139,26,50-67，untyped漏ID/立即回收、Data不清 | typed/untyped重入及数据残留；拟统一安全租约但保留已确认时序差异 |
| AK-903 / 006 | F04:38-48,54-68,91-99，同步controller不回收、ActionTask没回收、重复Action覆盖 | 每种路径分配/回收计数；明确重复Start策略，不能静默覆盖 |
| AK-904 / 005/006 | executor无OnDestroy/OnDisable，组件与GO生命周期不同 | 分别destroy/disable caller/host；拟host拥有动作收尾，不声称旧本来自动取消 |
| AK-905 / 004/006 | F02:140-165,221-235/F03:24-31/F04:106-112，空/stalecontroller、ID未一致校验 | 参数不一致/旧controller复用/完成回调重入；内部generation不能保证旧raw引用安全 |
| AK-906 / 002 | F02:242-271，Finished反复OnFinish、Paused不挡OnStart | 直接Execute全部状态基线；不能为只测正常路径静默改为只完成一次 |
| AK-907 / 020 | F18:34-59/F06:67-76，Deinit/Pause不停协程，旧完成回写 | 长协程→取消→复用；拟取消/回写保护，暂停底层与动作暂停分别定义 |
| AK-908 / 021 | F19:38-50,54-58,69-75，未完成Task.Dispose/async void异常/迟到回写 | 完成/fault/cancel/未完成Dispose/复用；不将Task.Dispose当真正取消 |
| AK-909 / 015 | F13:63-80,84-88，取消/Reset丢child Sequence未Deinit | 活动child取消/reset收尾计数；拟明确child拥有关系 |
| AK-910 / 017 | F15:46-64，空Parallel不进入Finished | 空并行复现，拟空集合完成，改变契约需记录 |
| AK-911 / 018/036 | F16:54-76/F25:589-607，现代<-1没分支；新旧0不同且旧依赖子项 | 全数值/即时和延迟子项；不合并两套repeat算法 |
| AK-912 / 024 | F23:43-51，InOut父不Finish/无child handle/暂停取消不传播 | 拟保存childcontroller、完成桥、取消/回收清配置；全父/子时序/嵌套回归，不能只看画面淡入出 |
| AK-913 / 023/025 | F22:7-10,37-62/F24:9-13，Fade配置不Reset、共享Image、0duration/资源空 | 直接Allocate复用/并发/0时长/缺资源；拟明确默认重置与失败处理，不静默做全局特效系统 |
| AK-914 / 022 | F20:19-28，source销毁不结束、Pause可能算结束、action Deinit不Stop source | 各场景基线；改动独立确认，不借AudioKit作为基础前提 |
| AK-915 / 033 | F25:476-506，KeyEvent回收无Reset，可能保留Finished/Disposed | key事件回收再执行；拟清生命周期而非删key功能 |
| AK-916 / 031 | F25:238-269，OnlyBegin不Reset/Dispose不回池 | Dispose与Recycle路径分别测，明确回收职责 |
| AK-917 / 034/035 | F25:518-524,537-543，HashSet枚举修改/容器不自动Finish、无子收尾 | mutation/动态队列持久语义/Dispose测试；区分设计持久容器与确定缺陷 |
| AK-918 / 033/038 | F25:428-438,746-753,758-780，空Timeline/Spawn不完成、Spawn依赖OnEnded | 空/已Finishchild/强制结束复现；拟明确终态而非删除组合 |
| AK-919 / 039 | F25:346-357，OnDisable trigger将集合置null后再次操作 | disable-enable-disable/AddDispose；拟重建/清集合的生命周期修正 |
| AK-920 / 040 | F25:831-875，HandleEvent不写Previous、未Start/缺配置问题 | Change/Handle后Back、非法src/dst；不假称Back总可用 |
| AK-921 / 040 | F25:942-962,972-980，泛型Src getter临时HashSet、Dst固定getter与setter冲突 | AddSrc/WithDst后读与转移；拟类型域索引/transition契约确认 |
| AK-922 / 041 | F25:1006-1012,1060-1067，MMEventManager/Processor注释不是实现；Restore不更新Previous | 标注失效说明H，不伪造多阶段事件；测真实typed事件字段/Restore |
| AK-923 / 043/044 | F25:1211-1262,1333-1379，Start无校验/Clear留State/nullcallback/同名覆盖 | 全异常/clear/重复注册基线；必要输入/空回调修正明确记录 |
| AK-924 / 047 | F26:79-83,107-111，InBounce end-pos、InOut漏bounciness | 精确黄金值/参数；拟修公式单独差异，不换网上新版全部Ease |
| AK-925 / 048/050/051 | F26:122-132,165-186,198-207，sqrt越界、Elastic参数/容差、Expo端点 | NaN/Inf/首尾/死分支等值分析；不夸大静态死分支为已知数值错误 |
| AK-926 / 037/039 | F25:630-642,1567-1623,1713-1781，剩余数/空current/chain自动Dispose后Reset | 空current与链重复Begin、子状态；不声称所有链可无限复用 |
| AK-927 / 057 | F:499-503,943-946，token二次注销/Or移一listener卸全部sources | 幂等token/仅最后listener清source为拟修正；原作用域差异需明确；多listener回归 |
| AK-928 / 009 | F07:15-32，单static不按scene key、任一实例destroy清static | additive/多实例；保持实际lazy-scene含义，不扩充未要求scene服务 |
| AK-929 / 025 | F24/PrefabSingletonProperty.cs:21-44，path attribute不参与资源路径且不调用SingletonInit | 测真实Resources键/初始化；不得把attribute文字当资源证据 |
| AK-930 / 001 | F01:253 ComplexAPI本体空，仅元数据含示例 | H：历史示例载体，运行调用无能力；名字/文档去向保留，B0确认兼容处理，不标“组合引擎已实现”或靠日志假实现 |
| AK-931 / 830 | DOTweenAction归档:37-44两次getter/Restart；Paused不暂停Tween | B1.13按用户安全修复选择一次factory、实际完成判据、Pause/Resume/延期取消及复租隔离；真实1.2.825离线编译/元数据验证，真实生命周期仍待Unity，不提前结项 |
| AK-932 / 831 | UniRxAction归档:64-76，OnNext Finish后OnFinish丢subscription，再Deinit无法Dispose | B1.12按用户确认修完成/取消/Reset订阅释放与迟到句柄；首OnNext成功、空完成取消、错误日志取消；83专属用例已编译待Unity运行，不提前标验收通过 |
| AK-933 / 800/802 | 单文件配置遗漏新API/内联整个底层；provider文本using/namespace/public处理脆弱 | 完整清单/条件结构/版权/纯Unity资源绑定方案；导出在独立项目全API构建，不能只检查文件存在 |
| AK-934 / 801 | prefab旧UnityEngine.UI DLL式引用 vs 当前所读ugui源GUID | Unity导入确认，不静态宣称损坏/正常；新GUID/绑定/sourceMap验证 |
| AK-935 / 812/832/850 | 归档扩展第三方未安装、图片许可/部分来源待核 | 交付不删除，测试提供者版本与可分发许可作为明确缺口；不本轮安装 |
| AK-936 / 058/801 | manifest/lock ugui2.0.0，所读cache package1.0.0 | B0查实际Editor解析；不改Packages/清Library来证明兼容 |
| AK-937 / 024 | InOut继承无参Allocate没有transition参数，OnStart依赖两个child | 原继承公开面必须列出；以无参构造/Allocate启动的child初始化能力待确认，不能把它当等价于双参数分配已可用；P编译/运行及清理 |

### 6.2 Audio

| 风险ID / 关联功能 | 原证据与问题 | 拟处理方式/具体验证，不作为已修复 |
|---|---|---|
| AU-900 / 019/021 | AsyncPrepare:35-36提前回keys、无generation/cancel，loader/player复用后旧回调 | keys快照/分配pool/request/播放generation；AStop→B复用→A完成不得覆盖B，lease释放一次 |
| AU-901 / 018/021 | Sync/AsyncPrepare:41-51归当前Config pool | 保存原池；P1分配→P2替换→Stop只回P1 |
| AU-902 / 004/006/011 | Extensions:100-101 Resume调用Play非UnPause | 先测source.time/回调；是否标准续播修正须明确，不偷偷宣传旧resume正确 |
| AU-903 / 010/011 | LifeCycle:56-63/Proxy:81-83先Invoke后清、回调后Stop | 拟摘旧回调/commit state/generation保护，明确异常清理；回调Stop+Play/新OnFinish/抛错只消费一次 |
| AU-904 / 007/008 | AbstractPlayer:84-95先OnStart后CanPlay，拒播不Stop/回收、不进playingPool | 拟明确Rejected终态/资源释放，不将自然Finish静默作为拒播回调；Mute/duplicate/pending/StopAll资源计数 |
| AU-905 / 003/005 | Music未检查IsMusicOn，Voice name沿前Clip scale | Gate/默认行为是否修正单列；false开关显式PlayMusic、Voice clip .2后name |
| AU-906 / 003/005/007 | Extensions:45-73同AudioName直接return但Command累积回调/setscale | 同名与loop/注册次数/未来播放串回调基线；拟start/reuse契约明确 |
| AU-907 / 003/004/010 | MusicPlayer Stop不清LifeCycle/ClearDataAndStop无清回调 | A注册→Stop/换B→B完成；拟每代回调归属，不能A回调串入B |
| AU-908 / 022/023 | FluentMusic:70-76不清mode；FluentSound:34-38不清clip/scale/pitch/mode；loop/手停无自动builder回收 | 每字段复用/默认/所有终态回收策略；不减链式参数规避 |
| AU-909 / 003/005/007/014 | ClipCommand先clip.name/hash无null guard；Sound有效name反而hash标识 | null、空/空白/name、同名不同clip；标识规则保持业务要求，分支疑点不擅自反转 |
| AU-910 / 001/002/011 | manager无OnDestroy完整清理，Music/Voice订阅及Condition source存活 | 完整Shutdown/新session/late cleanup；加载或播放中destroy/两次Play无旧订阅 |
| AU-911 / 027 | HeapTimer:82 ticks整数除法，小数秒截断；到期用< | DateTime .5/1.5/过去值/恰到期；精度修正与原时间语义分开 |
| AU-912 / 027 | repeat0也先tick、负repeat无限、负delay+无限可能同while反复投过去 | 有界harness/输入契约；不让测试主Editor卡死；完整repeat能力保留 |
| AU-913 / 027 | ResetMgr/SingletonInit:34-45只Clear堆不取消回池，重复投同item | 关闭drain/双投/手工回收在堆内；明确Reset语义修正 |
| AU-914 / 027/028 | Helper保留回池rawitem，Set不恢复isEnable | helper记录租约；完成复用后Clear不能误取消B；旧外部raw引用不虚假保证安全 |
| AU-915 / 029 | TimeExtend.RemoveAll:374 List.ForEach内Stop→Remove改列表 | 安全快照/延后删除为拟修正；多timer全删/异常/计数 |
| AU-916 / 029 | TimeExtend.UpdateAll:200-205正序删除可能跳下一个、callback增删改遍历 | 拟稳定扫描/延后变更；同帧到期/OnUpdated/Completed内Add/Delete/RemoveAll |
| AU-917 / 029/030 | SetIgnoreTimeScale:474只换源不换cachedTime；0负Duration；driver非DDOL/静态列表还在 | 时钟切换/边界/场景中的原行为，拟转换/生命周期补齐单列 |
| AU-918 / 016 | CustomProperty:80-84先通知后setter；Bind没Unbind/token；子项不通知IsOn | 不当普通Bindable重写；原顺序/总开关/重复Bind完整测试；新增注销能力需明示 |
| AU-919 / 025 | CustomDisposable:55非幂等，trigger:50-56异常阻断后续 | 拟幂等/各dispose清理后汇报异常，不吞错误；两次/首抛/重复绑定 |
| AU-920 / 024 | F:711-716静态Comparer、without-event不存prefs、ToString(null)、token双注销 | 完整协议并保留静态作用域；安全边界修正单列；值/键/初值顺序测试 |
| AU-921 / 013/034 | AbstractPlayer:102 internal abstract，Model继承外框架 | 实际可派生能力保留、底层type-map；不伪称任意基类可派生/IModel兼容 |
| AU-922 / 033 | PlaySoundAction只有自然callback Finish；拒播/失败/手停卡等待；Paused不Pause sound | 可选联动终态/取消桥，paused行为确认；Sequence下一步、mute/duplicate/loadfail/手停 |
| AU-923 / 002/021 | syncPrepare只有定义；Manager.PlayVoiceOnce:111-113跳AudioPlayer.OnInit | 保留支撑路径，不按未调用删；内部harness检查Volume/loop/life/mode/release |
| AU-924 / 026 | heap build叶index、pop/remove尾引用、0/负index/0capacity边界 | 全方法黄金/随机/索引/引用释放用例，修正不等于换成XF池 |
| AU-925 / 020 | DefaultLoader async:25-32没写Clip，Unload:37读Clip；Resources无lease | 写Clip/共享cache与最后lease释放为明确修正；cancel pending后完成释放一次 |
| AU-926 / 800 | AB LoadAssetRequest无有效Abort、cache不是load次数计数、Unload(true)破坏共享/pending | 完整typed in-flight/projectPending/owned-exclusive或retained共享设计；不能仅改Unload方法名 |
| AU-927 / 800 | XFAB Assets preview同步、mapping/manifest/encrypted同步前奏 | Resources真async及AB准备后真request；预热与严格async模式校验，不syncLoad/Wait伪装 |
| AU-928 / 800 | XFAB空参FormatException、Assets空clip无error、协程可能不完成 | 输入/profile/catalog校验、error+typed clip双判、watchdog；timeout不宣称I/O取消 |
| AU-929 / 017/019/800 | AudioSearchKeys两个字段≠XF(project,asset)，原ResKit桥没用bundle字段 | 全地址catalog/前缀兼容/冲突诊断；不静默把bundle当project |
| AU-930 / 027 | Timer.cs:193 Dump本体空 | H：实现缺失，不计已可用；拟真实排期/heap/clock状态输出而非空日志，单独确认补齐/历史兼容去向 |
| AU-931 / 014/815/850 | 音频资源可分发许可、Designer/type/GUID/UnityEvent导入结果未确认 | 所有资源仍在范围；新GUID/来源/实际导入与听感验证，不声明整个归档MIT |
| AU-932 / 033 | PlaySoundAction继承无参Allocate没有配置name/clip；旧两显式overload才设置输入 | 公开继承面保留/配置可用性单列，不假称无参可以正常播放；测试不得为了通过固定返回null；与AU909一起明确原缺陷或必要契约 |

## 7. Audio 源文件全集覆盖

相对AU/；每行都已完整读取（不只读取public附近）。namespace以实际定义区分，其中TimeExtend并非简单别名。代码路径54个，34个public类型/接口/枚举；表中的内部实现也必须保留行为。

| # | 文件 | 行数 | 对照ID |
|---:|---|---:|---|
| 1 | AudioKit.cs | 243 | AU003-009/014-015/022-023 |
| 2 | AudioKitArchitecture.cs | 40 | AU001/031/034 |
| 3 | AudioManager.cs | 124 | AU002/015/021 |
| 4 | AudioKitSettings.cs | 64 | AU015-016/024/034 |
| 5 | QFramework.BindableProperty.cs | 88 | AU016 |
| 6 | Action/PlaySound.cs | 93 | AU033 |
| 7 | FluentAPI/FluentMusicAPI.cs | 82 | AU022 |
| 8 | FluentAPI/FluentSoundAPI.cs | 95 | AU023 |
| 9 | Player/AbstractAudioPlayer.cs | 127 | AU009-013 |
| 10 | Player/AudioPlayer.cs | 102 | AU007-014 |
| 11 | Player/MusicPlayer.cs | 42 | AU003-006/013 |
| 12 | Player/AudioSourceController.cs | 193 | AU009/011 |
| 13 | Player/Features/AudioPlayerLifeCycle.cs | 67 | AU010 |
| 14 | Player/Features/ClipPrepareMode/ClipPrepareModeController.cs | 31 | AU021 |
| 15 | Player/Features/ClipPrepareMode/IClipPrepareMode.cs | 20 | AU021 |
| 16 | Player/Features/ClipPrepareMode/PrepareClipBySetUp.cs | 40 | AU021 |
| 17 | Player/Features/ClipPrepareMode/PrepareClipByLoaderSync.cs | 57 | AU021 |
| 18 | Player/Features/ClipPrepareMode/PrepareClipByLoaderAsync.cs | 56 | AU021 |
| 19 | Architecture/Model/AudioLoaderPoolModel/IAudioLoader.cs | 23 | AU017 |
| 20 | Architecture/Model/AudioLoaderPoolModel/IAudioLoaderPool.cs | 17 | AU018 |
| 21 | Architecture/Model/AudioLoaderPoolModel/AbstractAudioLoaderPool.cs | 24 | AU018 |
| 22 | Architecture/Model/AudioLoaderPoolModel/DefaultAudioLoader.cs | 41 | AU020 |
| 23 | Architecture/Model/AudioLoaderPoolModel/DefaultAudioLoaderPool.cs | 16 | AU018/020 |
| 24 | Architecture/Model/AudioLoaderPoolModel/AudioLoaderPoolModel.cs | 31 | AU018/034 |
| 25 | Architecture/Model/AudioLoaderPoolModel/AudioSearchKeys.cs | 32 | AU019 |
| 26 | Architecture/Model/PlayingSoundPoolModel.cs | 67 | AU008 |
| 27 | Architecture/Utility/AudioPlayerExtensions.cs | 104 | AU009-011/021 |
| 28 | Architecture/System/PlaySoundChannelSystem/PlaySoundChannel.cs | 17 | AU014 |
| 29 | Architecture/System/PlaySoundChannelSystem/PlaySoundChannelSystem.cs | 50 | AU014 |
| 30 | Architecture/System/PlaySoundChannelSystem/EveryOneChannel.cs | 20 | AU014 |
| 31 | Architecture/System/PlaySoundChannelSystem/IgnoreSameSoundInGlobalFramesChannel.cs | 46 | AU014 |
| 32 | Architecture/System/PlaySoundChannelSystem/IgnoreSameSoundInSoundFramesChannel.cs | 45 | AU014 |
| 33 | Architecture/System/ConsoleModuleSystem.cs | 59 | AU031 |
| 34 | Architecture/Command/Music/PlayMusicCommand.cs | 30 | AU003 |
| 35 | Architecture/Command/Music/PlayMusicWithClip.cs | 30 | AU003 |
| 36 | Architecture/Command/Music/StopMusicCommand.cs | 16 | AU004 |
| 37 | Architecture/Command/Music/ResumeMusicCommand.cs | 16 | AU004 |
| 38 | Architecture/Command/Voice/PlayVoiceCommand.cs | 34 | AU005 |
| 39 | Architecture/Command/Voice/PlayVoiceWithClip.cs | 36 | AU005 |
| 40 | Architecture/Command/Voice/StopVoiceCommand.cs | 16 | AU006 |
| 41 | Architecture/Command/Voice/PauseVoiceCommand.cs | 16 | AU006 |
| 42 | Architecture/Command/Voice/ResumeVoiceCommand.cs | 16 | AU006 |
| 43 | Architecture/Command/Sound/PlaySoundCommand.cs | 32 | AU007 |
| 44 | Architecture/Command/Sound/PlaySoundWithClipCommand.cs | 39 | AU007 |
| 45 | Architecture/Command/Sound/StopAllSoundCommand.cs | 20 | AU008 |
| 46 | Disposable/CustomDisposable.cs | 59 | AU025 |
| 47 | Disposable/IDisposableExtension.cs | 49 | AU025 |
| 48 | Disposable/Trigger/OnDestroyDisposeTrigger.cs | 60 | AU025 |
| 49 | Timer/BinaryHeap.cs | 411 | AU026 |
| 50 | Timer/IBinaryHeapElement.cs | 37 | AU026 |
| 51 | Timer/TimeItem.cs | 130 | AU027 |
| 52 | Timer/Timer.cs | 198 | AU027 |
| 53 | Timer/TimerHelper.cs | 44 | AU028 |
| 54 | Timer/TimerManager.cs | 551 | AU029-030 |

## 8. 函数返回闭包与行为验收

| 验收族 | 不能只验证签名的闭包 | 独立/回归测试要求 |
|---|---|---|
| AK启动返回 | IActionController全部操作、状态/时间模式、raw复用有效期；IAction S、组合Append、ICustomAPI.Data/Finish | I-A：六Start×同步/跨帧，暂停/恢复/Deinit/回收；所有链式编译与行为；不同启动宿主 |
| AK组合/继承 | AbstractAction<T>钩子/constraint；ISequence/IRepeat/IParallel；旧L与节点Current；FSM所有公开nested/delegate/index | I-A：派生程序集、现代与旧算法差异、Empty/Repeat/Timeline/FSM全部族；不是Timer替代测试 |
| AK辅助 | 31Ease、7消息/token/Or、Screen全部配置与资源、导出及optional扩展 | E/P/M/BUILD-A；多文件和导出形态各独立；第三方实际版本验证 |
| AU播放返回 | AudioPlayer的R、OnStart/Finish/Pitch/Scale/Pause/Resume/Stop/Loop/Volume；Music/Voice对象同闭包 | I-B：自然/手停/替换/暂停、pitch/loop、音量动态、旧raw引用；独立无Action |
| AU加载扩展 | IAudioLoader/Pool/Factory/Config/keys所有重载与字段；同步/异步、cache/lease/实际Unload | I-B Resources + XFAB AB + 自定义backend，换pool/取消/失败/共享/owned/projectPending/late清理 |
| AU属性/辅助 | 6键、完整Bindable/CustomProperty/UnityEvent、两Timer/Heap/Dispose、派生能力/constructor | E/P每一重载/钩子/枚举/时钟/通知/回收/边界；不以能播放替代 |
| 联动 | AU033所有Allocate（包括继承无参）/ISequence重载，与modern S/Completion/Deinit | 两基础独立先通过，另装Integration：等待/取消/failed/paused/同帧重入，不相互引用 |
| 游戏回归 | 当前AudioController clip借用链、6clip/10调用、SoundVolume旧键/默认、Global10/hash、XF其它音频 | I-C：不重复整审计；实际音效/滑块/去重/切scene/重启；4随机选敌使用方改最小项目实现 |
| 独立依赖 | Runtime/Editor/Backend/Integration/Samples的所有实际程序集引用、序列化类型/GUID、Resources路径、反射加载 | I-A/B无旧QF/CoreKit/Game；互相不强制；BUILD/Player/AOT；原框架在场时编译不算独立通过 |

完整验收尚未执行；B1.1仅已创建Ease与首批程序集检查的EditMode/PlayMode测试，其余测试未创建。已有Test Framework复用；实际Editor/package解析、平台/后端、原缺陷契约、provider与资源许可、版本控制回退等缺口见计划第10节。用例必要人工检查包括可听音效、Fade视觉/输入遮罩、完整工具/Help/Samples，不将自动编译等同实际效果。

## 9. 规划轮产物与未修改检查（历史记录）

只新增/完善本矩阵和02-extraction-plan.md。没有本轮对原审计/工程源码/资源/.meta/Packages/asmdef/asmref/生成项目文件/缓存的写操作。终端覆盖检查确认AK001..058和AU001..035共93个主功能族没有缺号，连配套工具/资源/说明共116个主表条目；子签名/风险另外列出，不把116当API调用数或测试通过数。

交付时实际3571文件集合SHA256为`D31310B06F3959E57B311BD6404348083CACFF0AE0810F85EACA285B0637A71B`，原01 SHA256为`F150FBBE06179C8CC9E9106A9ECEA5A00F4746BE896ECC6461A3B24DC48CB4CB`（mtime早于本轮）；两模块全部199文件SHA256为`11302FCE7F665419F8BBD550383AB48C81D339D3FBF88753B82BC3A6CB393148`。先前长摘要录入/对照不可靠，错误起始值已撤销，不宣称整体与开始一致。

mtime核对发现EnemyShield.asset在本轮期间保存过；本轮工具未写它，未覆盖/回退，现状及摘要见计划第11节。该文件不在两模块基线，当前XFFSM来源已核对；后续游戏回归使用最新资源。没有Git/Plastic基线diff，因此内容摘要/mtime/工具写入范围分别陈述，不能伪称工作区干净。

最后连续复算MatchesFinalSnapshot=True、AuditMatchesRecordedCurrent=True，核对窗口快照稳定；没有据此升级为全程未变或任何运行/构建/测试已通过。当前交付状态仍是已盘点/已设计，所有功能实现及验证留在后续实施阶段。

## 10. ActionKit 实施批次 B1.1（写入时记录）

2026-10-02，本轮仅开始ActionKit，不实施AudioKit、不修改业务、不删除原QF。本节与主表I+U状态是最新状态，前述规划轮快照不作为本轮编译/测试证据。

新根目录为`Assets/ProjectModules/ActionKit/`，namespace为`Project.ActionKit`。当前新增3个asmdef（Runtime、Tests.EditMode、Tests.PlayMode）和3个C#文件（完整Ease与两个测试fixture）。没有手工写.meta、GUID或生成项目文件。Runtime当前references为空、overrideReferences=true且precompiledReferences为空，不自动依赖旧QF目录内DLL；autoReferenced=false避免在业务默认程序集自动引入新模块。

| 功能范围 | 本批去向/验证状态 | 后续必须工作，不得视为裁剪 |
|---|---|---|
| AK-045..055 | 新Runtime/Utility/EaseUtility.cs完整31方法与EaseType；测试源码已建立，I+U/I+B+U | Unity导入生成新.meta、编译、运行本批测试；更广黄金值/目标构建按整体验收补齐 |
| AK-001..044、AK-056..058 | P/P+B，未伪造占位接口或假动作门面 | 下一稳定批次继续现代动作/全部旧兼容/FSM/生命周期与必要支撑 |
| AK-800..803、AK-810..812、AK-830..832 | P/P+B | 完整ScreenTransition资源、Editor、导出、帮助、全部样例与可选扩展仍为必须交付 |
| AK-840、AK-850 | PARTIAL+U，仅首批Ease测试与来源声明 | 完整测试体系/全部源码与资源来源仍待后续 |
| AK-924、AK-925 | 原问题未修改，已写部分characterization但未跑 | 不把保留旧公式等同认可其数学正确，修正须单列契约与结果 |
| 其他AK-9xx | 保持原待确认状态 | 各功能实施前核对对应风险，不一次性标记解决 |
| 全部AU条目 | 未实施 | Action阶段未验收前不进入AudioKit |

本批Unity编译/测试是停止门槛，不继续堆积未经验证的动作引擎。所需最小Unity操作及本轮真实核对结果见03实施记录。当前**不具备进入AudioKit阶段的条件**。

本批已完成静态核对：31方法/32枚举项及默认值保留；新活跃源码/asmdef禁止依赖扫描无命中；测试源码预期51例EditMode+2例PlayMode，未发现/执行；3387个既有文件摘要与本批开始一致。独立只读审查无必改发现，仍不能替代Unity结果。尚无新.meta，本批停在Unity普通导入、编译和两组测试门槛。

### B1.1 后续验收

用户在2026-10-02 16:57 UTC明确回复“所有测试全部通过”。据此记录首批用户确认通过；代理未自行运行Test Runner、未收到逐例XML/截图，51/2为源码预期数量而非单独核验的运行计数。当前已看到Unity生成的原首批.meta。AK-924/925的原问题不因保真测试通过而关闭；完整独立工程/Player/AOT仍未验收。

## 11. ActionKit 实施批次 B1.2

本批补入`Runtime/API/ActionKit.cs`、`API/IAction.cs`、`Runtime/Actions/`10原动作文件，以及`Internal/ActionPool.cs`、`ActionQueue.cs`、`ActionRecycleDriver.cs`、`AssemblyInfo.cs`。新增两个EditMode fixture和一个PlayMode fixture。不修改前批Ease、asmdef、业务、原框架或Packages。

现代状态接口、AbstractAction派生钩子、手动Execute/Finish，以及Delay、DelayFrame、Lerp、Callback、Condition、ConditionSequence、Sequence、Parallel、Repeat、typed/untyped Custom已写入新实现。原私有构造/所有重载/链式、计时与组合算法保留，底层pool/长期List/现代延后回收改成动作私有支撑。新增driver**只回收，不自动执行动作**；还不能调用Start/StartGlobal/StartCurrentScene，本批没有为它们放占位方法。

| 范围 | 本批状态 | 后续依然必须交付 |
|---|---|---|
| AK-002/003、AK-010..019 | I+U或I+B+U，完整源码和本批用例已写，待Unity | 自动调度接入后的整体时序/组合回归，以及已列原问题的独立处理 |
| AK-001/007/056 | PARTIAL+U | 全门面、旧队首队列、旧安全池和其公共支撑协议等，不因本批可手动运行而结项 |
| AK-004..006、008..009、020..044、057..058 | 仍待对应后批 | 控制器/全部Start、executor/global events、Coroutine/Task/Shortcut、Screens/全部旧API/FSM/生命周期 |
| AK-800系列/840/850 | 配套工具/资源仍待完整实现，测试/版权仅新增对应本批内容 | ScreenTransition资源、SingleFileCreator/Editor/全部samples/可选集成/完整来源记录 |
| AU全部 | 未实施 | Action阶段完成后才开始 |

原AK-901/902/906/909/910/911未静默修正；本批用例明确标注其characterization。新内部队列按批次排空、递归Drain防护、异常可见、场景存活与会话重置见03，属于解除旧单例/池支撑的明确实现差异。没有新Common/Core或跨模块服务。

本批验证目标是两个程序集完整重跑，覆盖旧Ease和新增现代动作；具体预期用例数、静态检查及停止门槛以03最新记录为准。B1.1通过不能自动覆盖B1.2，当前仍不具备进入AudioKit阶段的条件。

B1.2静态核对收尾：预期88/9测试用例；禁用依赖、旧池类型、空占位与跳过测试扫描无命中；原工程3387文件与本批开始摘要一致、首批4个核对文件一致；只读审查发现的两处新增测试失败路径清理缺口已修正。未运行Unity编译/测试，立即停在验证门槛，不继续叠加下一批。

### B1.2 Unity编译反馈

用户于2026-10-02 17:41 UTC提供真实编译结果：三个新增测试类的`NonParallelizable`产生6条CS0246。本地NUnit定制DLL（com.unity.ext.nunit 2.0.3，程序集版本3.5.0.0）只读确认不含此特性；Test Runner当前子测试顺序执行。已删除三个类的此特性，全部测试方法/断言/清理、Runtime、asmdef及Packages保持不变，源码预期仍88/9，不跳过测试或升级依赖。

AK-840及所有B1.2的I+U/PARTIAL+U状态不变：修正源码不等于Unity编译或测试通过，须再次取得实际结果。详细证据和兼容修正见03记录；不扩展实施下一批。

### B1.2 后续用户验收

2026-10-02 17:52 UTC用户明确回复“测试都通过了”，据此记录修正后的B1.2与首批回归用户确认通过，非代理执行或XML逐例核验。88 EditMode/9 PlayMode仍作为该批源码预期数量。AK-002/003、010..019对应已实施能力的最新状态更新为I+V(user)或I+B+V(user)；AK-001/007/056仍只部分实现，已完成部分用户确认通过，不能给整个族结项。主表此前B1.2的“未运行”是该批写入时状态，本节覆盖其验证状态。原AK901/902/906/909/910/911未因characterization通过关闭。

## 12. ActionKit 实施批次 B1.3

新增Runtime的6个文件：API/IActionController.cs、IActionExecutor.cs、ActionStartExtensions.cs，Internal/MonoUpdateActionExecutor.cs、ActionKitCurrentScene.cs、ActionGlobalHost.cs。无新asmdef，无Common/Core，不改B1.1/2已验收源码，不修改原QF/XF/业务。

本批完整迁移IActionController/ActionController/IgnoreTimeScale、Pause/Resume、六个Start（local/currentScene/global×Action/Action<controller>）、IActionExecutor/UpdateAction/ExecuteByUpdate、原prepare/dictionary Update推进和当前场景组件。global host只承载StartGlobal；七消息事件、global协程/Task与Singleton扩展仍在对应后批，不放空占位。

新测试ActionExecutionTests为14个EditMode用例；ActionStartPlayModeTests为14个UnityTest，ActionStartTestCaller只为测试提供Mono组件，不是业务依赖。连同原用例源码预期 **102 EditMode、23 PlayMode**，实际发现/通过须Unity验证。

| 功能族 | 本批状态 | 尚需完整交付 |
|---|---|---|
| AK-004/005/006/009 | I+B+U，完整相关源及测试已写 | Unity验证、原问题决策与最终独立环境/平台验收 |
| AK-008 | 尚未实现七消息/全局协程；global宿主仅完成启动能力 | 全部事件/注销/Or及协程，不因StartGlobal可用而结项 |
| AK-001/007/056/058 | 相应工厂/池/现代回收/Unity组件替换仅局部完成 | 其余门面、旧队列、安全池、工具/其它小辅助 |
| AK-020..044及配套工具/资源/示例 | 仍在后批 | 全部Coroutine/Task/Shortcut/Screens/旧API/FSM/编辑器/导出/样例 |
| Audio全部 | 未实施 | Action阶段完成后才进入 |

保留原开始Execute(0)、回调先于Deinit、controller按原ID字段转发、scaled/unscaled选择。AK903/904/905相关同步/重复启动/宿主销毁缺口有characterization，不把“原算法已复制”写成资源生命周期已正确。新global host仅替旧反射单例路径，原Resources/脚本/Toolkit没有隐藏引用；完整模块Lifecycle仍待后批。当前停在B1.3 Unity验证门槛。

B1.3收尾静态证据：源码102 EditMode/23 PlayMode、23 Runtime/8 Test C#、仍3asmdef；禁止依赖/空占位/Ignore/不兼容特性扫描无命中；3387个原范围文件摘要与本批开始一致。只读审查发现的新增PlayMode借用host失败清理风险已改为明确的自有host隔离前置条件，不影响Runtime原行为或跳过测试。详见03。当前无本批Unity编译/运行结果，全部B1.3状态仍I+U/I+B+U；阶段未完成，不进入AudioKit。

### B1.3 后续用户验收

2026-10-02 18:34 UTC，用户确认“Test都通过了”，记录为B1.3及此前回归用户确认通过；代理未自行执行/逐例计数。102/23为当批源码预期数。AK004/005/006/009更新为I+B+V(user)，原scope/同步回收/重复启动/host销毁等风险仍开放，后续新host消息接入需再次回归。

## 13. ActionKit 实施批次 B1.4

新增3个原动作/Shortcut文件和8个Subscriptions支撑文件，扩展已验收ActionKit门面/ActionGlobalHost，不改现有动作/控制器算法或asmdef。仅提取本模块返回值闭包所需契约：IUnRegister/IUnRegisterList/CustomUnRegister、IEasyEvent与EasyEvent/EasyEvent<T>、Or、Unity的destroy/disable/scene触发器及5个绑定重载；不是整个QFramework.cs、事件Bus或CoreKit。原框架的Godot provider分支不是本地Unity模块能力，未引入；当前Unity2022可用的生命周期能力完整保留。

新测试ActionSubscriptionTests的11例、CompletedTaskActionTests的4例，GlobalMessagePlayModeTests的8例、AsyncActionPlayModeTests的11例；源码预期总 **117 EditMode、42 PlayMode**。所有新运行API有实际实现，不把故障/未实现能力隐藏为固定返回值。Task fault/cancelled-task的async void异常路径没有在本批主Runner中人为触发未捕获异常，仍记录专项验证缺口，不因此删原Task能力。

| 范围 | 本批实现/状态 | 后续仍需完成 |
|---|---|---|
| AK008/020/021/022 | I+U或I+B+U，七消息/协程服务、全部现代异步工厂/链式/ToAction/UnityAudioSource已写 | Unity验证、真实GUI/focus/pause/quit/平台检查及原风险决策 |
| AK057 | PARTIAL+U，已实施订阅/组合/lifetime闭包 | 旧FSM的模块typed state events及相关接口消费者验证 |
| AK001 | 工厂/7属性已完成，族仍PARTIAL | ComplexAPI历史载体/完整生命周期兼容面和说明等不能直接删除结项 |
| Screens、Deprecated/FSM/Timeline、Editor/导出/全部samples/可选扩展 | 未实施或部分支撑，仍保留完整范围 | 按后批逐项实现与验证，不以现代Runtime够用作阶段完成 |
| Audio全部 | 未实施 | Action阶段验收后进入 |

保持原消息参数/委托顺序、RegisterWithACall先调用再注册、正常协程/Task完成方式。AK907/908/914/927原问题未静默修正，有明确characterization及验证缺口。没有跨模块公共Core/容器或借原QF运行；状态仍待本批Unity结果。

独立静态审查指出协程→Task测试原等待点不足以保证它覆盖未完成Task，本批已改为等待Task getter启动标记并断言Task未完成，再设置结果并验证后续回调/完成顺序；没有用放宽断言或修Runtime代替正确测试。测试拥有的Custom<AudioSource>.Data在回收前清理，避免原池残留把临时资源引用带入其它用例。真实Unity结果仍未取得。

B1.4原范围3387文件收尾摘要与本批开始一致；新34 Runtime/12 Test C#、3asmdef和源码117/42计数一致；禁止依赖、空占位、跳过测试和不兼容特性扫描无命中。当前仍待本批Unity编译/完整回归，不能从静态证据升级为阶段已完成。

最终独立静态审查复核了上述测试修正，未留下确定新增API/编译/正常路径断言/清理问题；结论仅静态，不替代真实Unity结果。所有B1.4状态保持I+U/I+B+U/PARTIAL+U，原风险与任务完整范围不缩减。

### B1.4 后续用户验收

2026-10-02 19:03 UTC，用户确认“本次也全部测试通过”。记录为该批与此前回归用户确认，代理未自行执行或核对逐例结果；117/42是该批源码预期数。AK008/020/021/022相应能力更新为I+V(user)/I+B+V(user)，AK057仅已完成部分通过，其余typedFSM与全部模块生命周期仍需后批。原风险与人工/平台缺口不因保真测试通过关闭。

## 14. ActionKit 实施批次 B1.5

新增Runtime/ScreenTransition四文件、Editor资源创建工具和Editor asmdef，以及EditMode/PlayMode两个fixture。本批给Runtime和两个Tests asmdef显式增加UnityEngine.UI，新增Runtime→Editor friend仅为资源组件绑定；Editor仅Editor平台且单向引用Runtime/UI，不带QF/UnityEditor到Player。当前本地UI程序集名已核实，manifest/lock与缓存版本差异仍以Unity实际编译/导入为准，不改Packages。

| 范围 | 本批状态 | 下一门槛与尚需交付 |
|---|---|---|
| AK023/024 | 四个原ScreenTransition文件的运行能力已写入，I+B+U | 编译、资源创建、数值/顺序测试、实际画面检查；AK912/913等仍需明确处理 |
| AK025/801 | 新资源getter及明确生成步骤，资源创建工具已实现；实际Prefab待Unity | 菜单创建新Prefab与GUID/字段后才算资源接入，不靠旧Resources回退 |
| AK800/802/803等完整Editor/导出/帮助 | 仍待对应批次 | 此创建菜单不等于SingleFileCreator/完整Editor能力已交付 |
| Deprecated/FSM/Timeline/全部samples/可选扩展 | 仍保留完整范围 | 按后批实现，不因现代动作已通过而省略 |
| Audio全部 | 未实施 | Action阶段完成后进入 |

源码测试预期新增11 EditMode/7 PlayMode，总 **128 EditMode、49 PlayMode**，未运行。本批须先在Unity编译后执行`Tools > Project ActionKit > Create ScreenTransition Resource`（非Play Mode），再运行两个完整程序集。工具只创建`Assets/ProjectModules/ActionKit/Resources/ProjectActionKit/ScreenTransitionCanvas.prefab`及需要的新目录；已有目标prefab/meta不覆盖，不保存用户场景，不手写GUID。

原UI配置按169行Prefab核对：Canvas Overlay/排序10000、ConstantPixelSize/scaleFactor1、无GraphicRaycaster、stretch黑色Image/raycastTarget=true等保留。原root的序列化scale为0也不悄悄修正；Unity归一化/实际可见性单列人工缺口，测试不能仅看alpha便声称淡入淡出画面正确。真实资源还没有落盘；当前属于明确的Unity资源创建停止点。

B1.5静态审查发现的两个InOut用例Probe配置清理问题已修正，仅将测试参数清理从OnReset改到OnDeinit，Runtime原Sequence/InOut不改；最终复核无未解决的确定新增问题。原范围3387文件摘要保持一致，源码128/49和4asmdef计数一致，禁止活跃依赖扫描无命中。实际新Prefab尚未生成，无本批Unity通过结果；AK025/801仍待资源接入，所有原风险和未处理功能继续保留，Stage不结项。

2026-10-02 19:45 UTC用户提供一项真实EditMode失败：资源配置测试的Resources.Load结果为null。当前新模块Resources目录和目标Prefab/.meta未检出，生成/加载路径已核对一致。AK025/801仍阻于Unity菜单创建资源；不删除断言、不用旧QF资源fallback、不认定工具本身失败。先完成明确的资源创建步骤再整批回归，详见03记录；其它测试结果未推断。

2026-10-02 19:47 UTC用户随后提供创建工具真实失败：临时Additive场景激活返回false，保存前在原:42抛异常。已将工具改为NewPreviewScene/ClosePreviewScene和不保存的临时GO，直接移动Preview后构建，不再依赖活动场景切换；保留全部资源参数/防覆盖及128/49测试断言。Resources目录已建立，目标Prefab仍不存在，修正后需再次执行Unity菜单验证；AK025/801状态仍未完成资源接入，Stage不结项。完整证据见03。

### B1.5 后续用户验收与资源核对

2026-10-02 19:59 UTC用户明确确认B1.5全部测试通过，记录为用户确认，128/49为当批源码预期数而非代理逐例核验。新Prefab已存在，资源GUID为2a0bf8e4b9b44ec46a47f771105de1e4，脚本GUID256f0a9016a7098469c8afed875a5d50与新Canvas脚本.meta:2匹配；Prefab:97/100绑定新组件/ColorImage，:74/155为Unity UI组件GUID。根scale0、sorting10000等源设置保留，无旧脚本/资源依赖。实际画面与原缺陷不因该批测试通过结项。

## 15. ActionKit 实施批次 B1.6

已写Compatibility中的ActionKitAction/IDeprecateAction/IResetable/INode/ActionData与元数据、DeprecateActionKit启动预热、5个原子节点及旧ExecuteNode/IEnumerator/Mono延时入口；Internal仅新增专供旧节点的ActionNodePool。ActionQueue和现有driver补旧FIFO，每Update最多推进一个头再做现代回收，旧节点完成不会自动Dispose。modern动作不转换成旧引擎，旧引擎也不伪装成IAction。

旧安全池保留默认12、正上限/预热clamp、负/0上限无界、Allocate仅清flag、null/重复/满池分支、flag→hook→push次序。Delay/Event保留默认RuntimeInitialize各Init(50,50)。不复制SafeObjectPool/Singleton/完整Factory体系；原provider级公开配置、factory、Dispose/Clear身份兼容破口明确记录，不声称私有池替代整个PoolKit对外API。

Chain及所有组合/Timeline/FSM仍待后批，未放空类型/方法占位；AK028..032是部分功能族，不能以原子类已可运行结项。原OnlyBegin与双Dispose缓存污染等问题保持characterization，不静默修复。

新增LegacyAtomicActionTests（29例）、ActionNodePoolTests（13例）、LegacyAtomicPlayModeTests（9例），源码预期总 **170 EditMode、58 PlayMode**。44 Runtime/1 Editor/17 Test C#，仍4asmdef。新增internal Tick仅供测试并保持原旧head→modern Drain顺序；等待头/抛错头混合回收与启动上限均有明确测试，不声称异常已隔离或任何时刻warm cache数量必为50。没有升级依赖、不写meta、不改原业务/框架，也未开始AudioKit。Quota恢复后从既有源码与待补测试接续，不重做01/02审计或框架全量索引。

当前静态审查无必改新增问题，原双Dispose/OnlyBegin/旧异常阻断现代回收及provider配置破口仍未关闭。等待当前Unity导入编译与170/58完整回归，AK028..032的Chain扩展、组合/Timeline/FSM/Editor导出等仍待后批，不能提前结项。

B1.6收尾的原文件保护摘要与开始不一致；定向mtime检查发现项目Kilo配置、3个业务FSM资产与ProjectSettings.asset在期间保存过，未由本批写入、未覆盖/回退，详细路径和时间见03。该核对不是重做审计；不假称整个原工程未变。新增/变更代码限定依赖检查无命中，最终源码170/58计数已核对，静态增量审查无新增确定问题。当前仍无B1.6 Unity通过结果，所有未完成/部分功能族保持原范围与状态，不进入下一批或AudioKit。

### B1.6 后续用户验收

2026-10-03 02:54 UTC用户明确确认B1.6测试通过、没有报错，记录为该批用户确认，170/58是源码预期数，未由代理逐例运行。原旧节点/池问题及EOF/平台缺口未因characterization关闭；配置/FSM外部保存继续保留当前现状。

## 16. ActionKit 实施批次 B1.7

新增Compatibility的LegacyCompositionNodes/LegacyActionChain/LegacyTimeline/LegacyDynamicNodes和两个Dispose trigger，扩展LegacyExecutionExtensions的3个Chain重载。所有原公开/保护类型、接口、枚举器、委托/属性/字段/构造及默认值有真实实现；不放空IActionChain或假方法，不用modernIAction包装旧算法。

必要底层仅限原列表替节点自有List、KeyEvent池替现有ActionNodePool、原生Mono/Unity生命周期。没有新独立支撑模块或Shared/Core，不加旧Toolkit依赖。原关键差异：Sequence同dt推进、Repeat旧0语义、Spawn逆序与OnEnded计数、Timeline strict/insertion order、dynamic persistent node、Chain自然Dispose、显式OnFinished/替换OnDisposed、旧一次性OnDisable集合均保留并单列。

新增LegacyCompositionTests 19例、LegacyTimelineTests 10例、LegacyChainPlayModeTests 9例，源码预期 **199 EditMode、67 PlayMode**；50 Runtime/1 Editor/20 Test C#，仍4asmdef。旧原子Chain扩展现在补齐，但FSM/状态事件、完整初始化/兼容差异、SingleFileCreator/Help/Samples/可选扩展及独立验证继续后批，不以旧引擎能跑结项。

本批尚待静态审查收尾及Unity普通导入/编译后两个完整程序集回归；无新资源生成步骤，不重建已生成的Screens Prefab。原AK915/917/918/919/926等问题不静默修复、不通过日志抑制或Ignore制造通过。不改Audio/业务/原QF，当前停在本批验证门槛。

已独立静态审查，无必改新增问题；追加pending Begin的真实EOF用例补同步/手动驱动覆盖缺口，该用例不手动Execute，仅等待gate条件和原Coroutine完成，保持Editor Game View可见及2秒实时失败边界。不是yieldnull重写原调度或跳过失败，尚待Unity实际执行；详见03记录。

B1.7源码收尾计数199 EditMode/67 PlayMode、50 Runtime/1 Editor/20 Test C#、4asmdef已复核；本批依赖/不兼容特性/跳过/占位检查无命中，增量静态审查无新增确定问题。原文件保护摘要发生变化，定向定位EnemyBullet.cs于本批期间保存，本批未写/覆盖/回退；详情见03，不能假称原工程完全未变。当前等待Unity普通导入、编译及两个完整程序集结果，不开始FSM或AudioKit。

## B1.8 状态

- B1.7已用户确认通过（2026-10-03 03:52 UTC），对应已实施的旧组合/Chain/Timeline状态以此确认为准。
- 本批AK040/041/043/044与FSM事件支撑已写入，静态审查无必改新增问题；源码预期222 EditMode/70 PlayMode，待Unity完整回归。
- `TypeIndex`改领域类型；`ActionStateEvents.Global`不共享旧global事件和token身份。原缺陷仍保留，生命周期/Editor导出/示例/独立验收继续后批。

B1.8后续：用户确认全部测试通过。B1.9发现安全Shutdown需要明确处理原取消/回收缺陷；行为取舍未确认，AK042与相关AK907/908/909等状态不结项，未修改已通过代码。

## B1.9 修复状态

用户已选择修复方案，覆盖前述待确认状态。AK901/902/903/904/905/907/908/909对应factory/ID/回收/取消/host问题已改为安全实现并更新测试，状态为“已修待Unity”，不是旧风险已测试关闭。AK042完成必要生命周期API，无新容器或共享框架；兼容破口与第三方Task所有权保留。异常child收尾/宿主丢失/同帧重启等集成边界继续静态复核，其他原数学/旧节点等风险不因此批量关闭。

本批源码预期288 EditMode/87 PlayMode。EditMode销毁及两项PlayMode子取消失败已记录，当前不能通过；分别修正Editor生命周期与Step执行后取消/租约核验，保留原失败断言并新增回归，待Unity重验。普通完成顺序/公开签名保留；duplicate同租约启动明确拒绝。raw外部引用的有效期未承诺无限延长。

最新验收：用户2026-10-03 07:43 UTC确认修正后完整测试通过，覆盖B1.9此前待验证/失败记录。相关已修生命周期能力记录为用户确认通过；其他原风险、未实现工具/示例和独立/构建验收不扩展结项。恢复任务优先读03顶部，避免重读历史流水。

## B1.10 导出状态

AK800/802工具及完整当前Runtime预设已实现。导出保留源namespace/API/版权，程序集属性前置；Unity组件保留11份同名伴随脚本及原GUID，资源/asmdef配套输出，不假称一个.cs能承担所有序列化绑定。目录安全/清单完整/确认覆盖/程序集边界有回归；EditMode新增Editor引用，无Runtime依赖变化。

同Unity C#9离线编译（全部模块/测试和隔离导出，含/不含Editor defines）通过，6项renderer/24项引用filter纯托管检查及13对asset/meta字节验证通过。静态预期319 EditMode/87 PlayMode；真正Unity测试、菜单配置生成/导出及跨项目资源导入仍待验。AK803、Samples、可选扩展、最终许可证/独立Player/AOT不据此结项；停在本批验证门槛。

最新：用户2026-10-03 09:29 UTC确认B1.10测试通过，覆盖上述待测试状态；非代理执行/逐例XML确认，不扩大菜单操作、跨项目或Player/AOT验收。09:34 UTC用户取消Action完整Samples迁移，AK810/811及AK832演示资产记OUT(user)；下一批API Help，随后可选扩展实现/测试和最终验收。功能、必要资源及Audio范围不变。

## B1.11 帮助状态

AK803独立Editor窗口/当前API索引与23个主题已实现，ComplexAPI空历史载体改为组合指南；AK812基本说明进入Help，可选提供者说明仍随扩展后批。106类型/548声明成员全覆盖，无getter调用或GameObject创建；重载、可选参数、泛型/委托/嵌套/保护钩子有回归。22条短用法同版本离线编译、23项纯托管检查通过，原索引断言保留。源码预期345 EditMode/87 PlayMode；Unity实际测试/窗口交互未执行，停在本批验收，不扩大独立/Player/AOT或原缺陷结项。

最新Help优化：用户确认原功能正常，本轮保留全部条目并中文化；23指南/5组、完整入门+21短方法，106类型/548成员及381参数/147返回说明均保留。8 Editor/18 EditMode离线编译、37纯托管检查、入门及各短例实际编译通过；593保护文件哈希未变。新版菜单Tools > Project ActionKit > ActionKit 帮助；源码预期359/87，新版深浅主题/窄窗口/导航滚动复制及Unity完整测试仍待验，不以静态审查或截图模拟替代。此次止于Help，不继续其他模块。

最新命名调整：用户要求不使用通用Kit别名，帮助示例统一直接调用ActionKit；索引/Runtime API不变。无别名入门和21短例全部离线编译通过，38纯托管检查通过，新增拒别名回归后源码预期360/87；未来其他Kit同样保留具体名称。验证证据见03末尾，Unity验收仍待用户。

最新实际失败修正：两项Unity AssemblyBuilder编译因filter漏可信单体/Player引擎路径而缺Core引用；两测试显式UseEngineModules，保留原完整编译断言，并补路径白名单/CoreModule及实际引用集检查。离线8 Editor/18 EditMode、38纯托管、入门/21短例及实际Player模块下的帮助/导出编译通过，593保护文件未变；静态预期367/87，原两失败与新Native引用集测试待Unity重跑。仅改测试配置和现有记录，不扩大Runtime/API/独立Player构建验收。

## B1.12 Observable 状态

B1.11已用户确认全部测试及Help验收。B1.12 AK831独立BCL适配已实现，原提供者身份保留；用户明确首OnNext成功，空OnCompleted取消、OnError日志取消。取消/重置/完成订阅释放，slot隔离迟到通知，核心内部执行/组合子项租约修正使Reset或复租不被旧栈操作，不变更公开契约。DOTween AK830仍P+B，不安装/空实现冒充；演示资产仍OUT(user)。

核心独立及加载扩展的Help分别106/548与108/560，24指南有明确可用性；84 Help纯托管断言、6 Observable probe及47示例DLL编译通过。四测试程序集离线编译，核心371/87与扩展58/25仅为源码预期（合计429/112）；真实Unity/83扩展用例运行仍待用户。589个非授权保护文件一致、4个必要Runtime文件改动均单列，原meta无改删；详细证据见03末尾。停在B1.12四程序集与Observable帮助验收门槛，不提前独立工程/Player/AOT结项。

最新真实失败修正：用户UntypedCustom原True断言未改，Execute将同租约取消后Finished的终止结果与成功通知分离；ID/重置代次变化仍拒旧栈，执行器/组合取消保护不放宽。新增9行为+1帮助回归，新旧DLL对照旧6过/3败、新9全过；6 Observable probe及本批编译通过，原Unity用例仍未重跑。当前核心381/87、扩展58/25（合计439/112），不能据离线结果提前标实际验收通过。

最新结果：用户18:40 UTC确认测试全部通过，覆盖本批待重验状态；新增Observable帮助交互未单独报告，独立工程/导出资源绑定/Player/AOT不据此结项。下一步先处理AK830 DOTween真实提供者/版本或用户明确范围豁免，再完成兼容/许可/独立验收；本轮仅更新状态，不启动实现。

## B1.13 DOTween 状态

用户提供真实1.2.825且批准安全修复；AK830独立程序集/原公开签名、包装MIT与供应商Artistic分别保留。工厂一次、Pause不算完成、用户callbacks追加非覆盖，ToAction先绑定后Pause；首启前Reset保留binding，执行后Reset结束binding。逻辑取消立即生效，Kill(false)通过真实Unitycontext延期并重检marker，解决onKill用户回调重入重复Despawn及预启动Tween复租误操作；不能保证callback覆盖/异常、退出前Post或任意人工pump下的物理清理。无供应商/业务/其他模块改动。

当前冻结源码10程序集101文件同版本离线编译成功，wrapper Player无Editor引用；四Help模式各45检查、96示例DLL编译通过，基线核心公开/保护契约无变化、45vendor文件hash一致。25指南/110类型572成员（两扩展加载）无漏项。核心383/87、Observable58/25、DOTween56/21（合计497/133）仅声明核对，B1.13生命周期/六程序集与帮助Unity验收仍待用户；详见03末尾与既有Temp统一验证证据。稳定批次停在此，不提前最终独立/Player/AOT或Audio阶段。

最新真实失败：全DOTween fixture在SetUp全局0要求处失败（现存1Tween），不是56行为断言均失败。两fixture改自有target-token快照/单Tween.ManualUpdate固定测试dt，不要求全局无Tween，不改global timeScale、不全局推进或清理他人，保留行为断言/ActionKit关闭环境保护。新增3非自有对象/新增条目隔离回归，当前两fixture同版本编译通过，预期DOTween58/22、总499/134；真实Unity仍待重跑，Runtime/供应商未修改。

最新SDK初始化根因：真实1.2.825 Init/InitCheck在非PlayMode未初始化时不创建服务，但能创建active Tween；Kill则直接return，解释大量no-kill/no-callback/no-pool。将57生命周期例移到PlayMode，保留所有强断言与source GUID，仅1纯metadata接口例留Edit；两fixtureSetup要求真实Play/Init非null，不反射强开initialized、不改vendor、不Skip。当前DOTween1/79、整体442/191=633与前次总数一致，两测试程序集同版本编译通过；尚未真实Unity生命周期重跑，不提前结项。

最新验收与术语：用户03:35 UTC确认本批全部测试通过。随后仅修Help泛用token文案，明确IUnRegister为注销句柄、控制器为池化控制器引用，示例用unregister；公开API不改。新增1文案回归，Editor/CoreEdit编译、四模式46纯托管检查及两改后示例编译通过；当前总443/191，Unity此文案增量未运行。最终独立/Player/AOT和B1.14仍待后续，不从功能测试通过推断结项。
