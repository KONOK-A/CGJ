# ActionKit 实施记录

## 恢复入口（最新）

- 工程：G:/Unity/Projects/CGJ2024，Unity 2022.3.62f3c1。目标是完整提取本地ActionKit与AudioKit；当前只实施ActionKit，不按游戏调用量裁剪。
- 范围调整：用户2026-10-03 09:34 UTC明确新ActionKit不需要完整样例；不迁移20组主样例/场景/专用图片及可选扩展演示资产。保留全部功能、API Help简短代码、扩展实现/回归和ScreenTransition运行资源；不删旧样例，Audio范围不变。
- 新模块：Assets/ProjectModules/ActionKit，namespace Project.ActionKit；核心Runtime/Editor及基础测试，Observable/DOTween可选程序集与各自测试独立放Integrations。原QF/业务/Audio/XF包保留，不修改Library/PackageCache、Packages或生成项目文件。
- 已完成：B1.1..B1.9的已实施代码均获用户测试通过确认。最新B1.9于2026-10-03 07:43 UTC确认；源码预期288 EditMode/87 PlayMode，代理未执行Unity或逐例核验XML。
- 当前B1.10：专属单文件导出配置/Inspector/完整Runtime预设及回归已用户确认测试通过（2026-10-03 09:29 UTC）。源码预期319 EditMode（含1项UnityTest）/87 PlayMode，不是代理逐例执行计数；菜单操作/跨项目绑定未单独确认。
- B1.11已验收：用户2026-10-03 14:51 UTC确认“测试都通过了，Help界面也验收完毕”；含两项AssemblyBuilder失败修正/具体类型名调用及中文窗口优化。367 EditMode（含2项UnityTest）/87 PlayMode仍为源码预期数，非代理逐例执行计数；不扩大最终独立/Player/AOT验收。
- 命名约定：用户要求避免using Kit = Project.ActionKit.ActionKit等通用别名；帮助及后续示例直接使用ActionKit等具体Kit类型名。真实命名冲突时用完整类型名，不把所有Kit统称为Kit；不改Runtime命名空间/API。
- 已有代码：完整现代动作、控制器/启动/宿主/订阅，Ease、ScreenTransition，旧原子/组合/Chain/Timeline/FSM及必要内部支撑，模块Initialize/Shutdown。
- 新资源已生成：Resources/ProjectActionKit/ScreenTransitionCanvas.prefab。资源创建菜单由Editor Preview Scene构建，不覆盖旧资产/meta；无需重建。
- 用户批准修复取消/关闭/回收缺陷，不要求保留这些bug。已改安全Task/Coroutine取消、组合子项清理、executor/控制器租约、关闭重入与回收。其他数学/旧节点风险不能未经核对批量标解决。
- 最新修正：executor ExecuteAlways仅用于Editor生命周期，自动Update用Application.IsPlaying(gameObject)保护；Step执行后复核取消，控制器当帧释放。三个task-*异常是故障测试的LogAssert.Expect日志，不等于测试失败。
- 完整发布级待办：许可证/兼容收尾、导出操作与跨项目资源绑定、无旧QF受控环境/必要Player/AOT及人工视觉验收；这些不是本轮轻量门禁。ComplexAPI空历史入口已移入组合说明，不加Runtime空壳；完整Samples仍在范围外。
- B1.12已验收：用户2026-10-03 18:40 UTC确认“测试都通过了”，包含Observable和UntypedCustom返回值修正；核心381/87、Observable58/25（合计439/112）仍为源码预期数，非代理逐例执行/XML确认。新增Observable帮助界面未单独确认，不扩大独立环境/Player/AOT验收。
- B1.13已验收：用户2026-10-04 03:35 UTC确认“全部测试通过”，含DOTween真实Play初始化迁移与全部强回归；核心383/87、Observable58/25、DOTween1/79（合计442/191）仍为源码预期数，不是代理运行/XML证据。
- B1.13后Help术语修正：笼统token改注销句柄/IUnRegister及控制器引用，两个短例用IUnRegister unregister；当时Editor/核心Edit编译、四模式各46纯托管检查和两个改后短例编译通过。新增1文案回归后核心384/87，总443/191；文案增量未单独实际Unity运行。
- 2026-10-04轻量门禁通过：最小修复组合子回调Shutdown后继续执行、Condition谓词/回调后访问复租状态；10程序集同版本C#编译及21项纯托管检查通过，用户确认Unity编译正常和新增7项EditMode回归全部通过。当前核心391/87、总450/191为源码预期，本轮未重跑全量PlayMode。
- 活跃源码/程序集和62处资源GUID引用未发现旧QF依赖；ScreenTransition新脚本/UGUI引用有效，复用已有资源加载/执行测试通过确认，仅做smoke、不扩展视觉或AK-912等原问题。ACTIONKIT_READY_FOR_AUDIOKIT = YES；本轮停止，未开始AudioKit。
- DOTween安全边界：ToAction在Pause前预绑定同一Tween生命周期，首次执行前的组合Reset保留绑定，执行后Reset结束绑定；重复用fresh factory。取消逻辑立即生效，物理Kill(false)由真实UnitySynchronizationContext.Post延期并复核marker，不能承诺Shutdown/退出前同步释放。只支持主线程生命周期/原始context与独立根Tween，不借私有反射支持被DG.Sequence接管的子Tween。供应商45文件未改，DOTween适用独立Artistic许可，不是wrapper的MIT。
- 工作偏好：代码和测试优先；文档只记简要增量；每稳定批次等待已打开Unity编译/完整测试。无Unity MCP，不启动同工程第二Editor，不清缓存、不手写GUID。
- 版本管理：目录不是Git仓库，cm不可用；不初始化仓库、不提交/回退。并发用户/Unity保存要保留，不能根据历史摘要覆盖。

下方为历史批次记录；其中“待验证”、早期asmdef配置与错误反馈只描述当时，不覆盖本恢复入口。详细功能证据仅在必要时查02-feature-matrix对应ID。

## 新文件与程序集

所有源文件位于`Assets/ProjectModules/ActionKit/`，本批新增如下：

| 路径 | 内容与隔离 |
|---|---|
| Runtime/Project.ActionKit.Runtime.asmdef | 程序集Project.ActionKit.Runtime，namespace Project.ActionKit；references=[]；overrideReferences=true、precompiledReferences=[]；autoReferenced=false；保留Unity引擎引用 |
| Runtime/Utility/EaseUtility.cs | 31个完整Ease方法、EaseType全部32个值（含Count）、原public非static class与隐式构造 |
| Tests/EditMode/Project.ActionKit.Tests.EditMode.asmdef | 仅Editor；显式引用新Runtime、UnityEngine.TestRunner、UnityEditor.TestRunner、nunit.framework.dll；UNITY_INCLUDE_TESTS；不引用原QF |
| Tests/EditMode/EaseUtilityTests.cs | API/默认值/枚举、31个曲线黄金点、方向和边界、显式参数、原问题characterization、实际编译程序集引用白名单检查 |
| Tests/PlayMode/Project.ActionKit.Tests.PlayMode.asmdef | 无Editor平台限制；只引用新Runtime、UnityEngine.TestRunner、nunit.framework.dll；UNITY_INCLUDE_TESTS；不引用UnityEditor |
| Tests/PlayMode/EaseUtilityPlayModeTests.cs | 跨帧timeScale=0结果确定性、用曲线驱动Unity Transform与临时对象清理；不需游戏场景/动作host |

测试asmdef依据当前安装的Unity Test Framework 1.3.9文档与Samples中的定义，而不是升级测试框架。Runtime关闭自动预编译DLL引用，避免旧QF目录内Markdig等默认DLL意外进入新模块。当前只依赖Unity和标准库；后续ScreenTransition批次才按需要增加UnityEngine.UI显式引用，不在首批制造ugui分支依赖。

**未手工创建.meta/GUID。** 这些新脚本/程序集需要当前Unity Editor导入。没有复制原脚本GUID，没有移动或删除旧资源，没有手工修改.sln/.csproj。

## 已提取功能与来源

原始定义：`Assets/QFramework/Toolkits/_CoreKit/ActionKit/Scripts/Utility/EaseUtility.cs`，完整284行，原文件SHA256：

`663EAFFEA8DFA174AAAB88C10A0BBA0D9373F3E928DFEBC4257CE55CB74E528B`

对应矩阵AK-045..055：Linear，以及Back/Bounce/Circle/Cubic/Elastic/Expo/Quad/Quart/Quint/Sine各In/Out/InOut方法。保留全部参数/默认值、枚举顺序与`InoutQuart`原拼写。只改所属namespace为Project.ActionKit及空白排版，不改公式/默认值/常量，不新增不存在的EaseType分派API。

保留2016-2023 liangxiegame的原MIT声明和来源链接。版权/来源中的QFramework文字不是using、类型引用或程序集依赖，不删除来源声明来制造零文本命中。

## 底层替换与模块边界

| 处理项 | 本批实际结果 |
|---|---|
| QFramework底层 | 该原文件只使用System.Math与UnityEngine.Mathf，原QFramework依赖来自namespace和所在CoreKit程序集。本批移入独立namespace/asmdef，不复制任何CoreKit支撑 |
| XF能力 | 本批没有调用XF。定向查当前XFGameFramework包未检出相应EaseUtility/EaseType/曲线族；已有计时器/播放器不承担数值曲线算法，不能为“使用XF”改掉数学能力 |
| ActionKit Internal | 本批无需pool、事件、host或Internal适配，不预建以后可能使用的框架 |
| 新项目级独立支撑模块 | 无。仅创建用户已授权的ActionKit目录/程序集；没有Common/Core/IOC/通用服务容器 |
| AudioKit与业务 | 没有引用或修改AudioKit、AudioController、MainModule、武器、GetRandomItem；原QF保持现状 |

后续若出现具有复杂独立生命周期/实际多消费者、需要新的大型支撑模块，先按本次用户要求说明职责、XF缺口、消费者/依赖/API，再暂停确认。现有计划中“小内部适配”不是无限授权把所有底层装进ActionKit。

## 行为差异与原问题

- 本批有明确类型身份变化：`QFramework.EaseUtility/EaseType`迁到`Project.ActionKit.EaseUtility/EaseType`。原文件/类型仍在旧目录，没有改任何使用方；游戏不会自动切换到新版本。
- Runtime设置autoReferenced=false，因此新默认程序集使用方将来需要明确接入；本轮测试通过自身asmdef显式引用。这是隔离安排，不是删除功能。
- 保留AK-924：InBounce使用`end-pos`、InOutBounce没有传递bounciness。为这些行为写了明确命名的characterization测试，**未认定它们是正确曲线，也未修复/关闭问题**。
- 保留AK-925：Circle越界NaN、Elastic参数/终点容差、Expo非精确端点等边界。没有静默clamp/标准化端点、替换常量或换网上新版算法。
- 任何后续修正要单独更新矩阵、说明行为变化并运行用例，不能通过删参数/弱化断言制造迁移成功。

## 验证记录

### 环境事实

- Unity版本：ProjectVersion.txt为2022.3.62f3c1。
- 已存在打开本项目的Unity Editor及其AssetImportWorker；本轮没有启动第二个Editor，也没有向现有Editor发重新导入/编译/测试指令。
- 本会话没有可调用的Unity MCP操作，MCP资源/模板列表为空。这不证明Editor自身不能编译，只表明不能从当前工具取得对应批次结果。
- git status返回非Git仓库；cm status不可用。不假定工作区干净、不自动初始化Git、不安装CLI、不提交/回退。

### 本批测试设计与执行状态

| 检查 | 实际状态 |
|---|---|
| 全部31方法与32枚举成员、参数/defaults/非静态class检查 | 测试源码已写，未运行 |
| 全部曲线pos=.25黄金值 | 31个TestCase已写，使用冻结数值，不在测试中调用旧QF作为oracle；未运行 |
| Linear钳制、Quad反区间/越界、Back/Bounce/Elastic显式参数、Bounce四段 | TestCase/Test已写，未运行 |
| AK-924/925原行为characterization | 已写，未运行，问题仍开放 |
| 实际Runtime程序集只依赖Unity/标准库 | EditMode反射检查已写；源码/asmdef静态检查另记，不能替代真正编译后结果 |
| PlayMode跨帧/timeScale0/Transform驱动 | 2个UnityTest已写，未运行；try/finally恢复时间缩放并清理临时对象 |
| 完整ActionKit测试/ScreenTransition/工具/导出/独立工程/AOT | 未实现对应全部批次，未验证；不以首批Ease代替这些门槛 |

当前不能把本批称为“稳定验证通过”，只是已经形成可以交给Unity导入验证的完整切片。实际已完成的静态检查如下，不使用此前规划轮错误摘要当本轮基线：

- 原文件与新Ease文件的只读差异核查：除namespace、BOM与空白外，没有方法/公式/参数/defaults/枚举变化；31个public static float方法与32个枚举项均保留。
- 新目录全部活跃`.cs`和`.asmdef`检索`using QFramework`、`QFramework.`、`QFramework.CoreKit`、旧Assets/QFramework路径、Resources.Load/LoadAsync、AudioKit、MainModule、XFGameFramework，均无命中。本批没有资源加载/反射旧类型入口；版权来源链接仍保留。
- asmdef已定向复读：Runtime不引用预编译第三方DLL；EditMode与PlayMode的Editor隔离正确。JSON文本静态核对不是Unity导入成功证明。
- 测试源码枚举为7个`[Test]`、44个`[TestCase]`，预期51个EditMode用例，以及2个`[UnityTest]` PlayMode用例。**这是源码预期数量，不是Test Runner已发现/通过数量。**
- 一次独立只读代码审查未发现迁移差异、错误黄金值或必改配置问题；显式Elastic的左右半段都已覆盖。这不是运行测试结果。
- 最后枚举新目录只有6个手工新增源文件/asmdef，没有`.meta`，尚待Unity导入生成。没有以此去猜测Editor Console当前是否存在其他错误。

## 最小Unity操作与停止门槛

1. 回到**已经打开的当前工程**，保持非Play Mode，让Unity正常导入`Assets/ProjectModules/ActionKit/`；若没有自动导入，执行一次Assets > Refresh。只需普通导入，不要Reimport All或清Library。
2. 等待脚本编译完成，查看本次Console结果。若有编译错误，保留完整错误文本（文件、行号和错误码），先修本批，不继续增加功能。
3. 无编译错误后，打开Window > General > Test Runner。EditMode只运行`Project.ActionKit.Tests.EditMode`（源码预期51例）；PlayMode只运行`Project.ActionKit.Tests.PlayMode`（预期2例）。保留两组结果，失败时保留完整堆栈。若数量不符或找不到程序集，先记录发现/配置问题，不跳过测试。无需运行旧框架样例或游戏场景。
4. Unity负责为新文件生成.meta。不要手工复用旧QF GUID，也不需要重新生成sln/csproj、切换平台或安装依赖。

收到当前批次实际编译与测试结果后，才继续B1中的现代动作/生命周期等下一批；其余ActionKit功能不排除、不结项。**当前暂停在这里，不开始AudioKit。**

## 写入范围、回退与内容核对

本批是纯新增Runtime/Tests目录，不替换任何已存在源码/资源，因此不需要先覆盖或搬走原框架。原源码原封不动就是本批的运行回退路径；新程序集未被业务引用。新增路径已逐项列在本记录，后续若要求回退，只撤本批新增目录及Unity给它生成的.meta，并恢复本批矩阵的状态变更；操作前仍要核对没有用户后续编辑，不执行整工程删除/恢复。本轮没有自动执行任何回退或创建外部备份。

规划B0要求的正式版本控制/快照条件**没有假称满足**：没有Plastic/Git变更集或可恢复外部快照。本次用户明确开始实施后，首批限定为不覆盖原文件的独立新增切片；涉及覆盖/移动/资源重绑或批量迁移之前仍需落实实际回退手段。03和矩阵是本轮授权维护的记录，不将它们的修改说成源码未变。

实施前对Assets/Packages/ProjectSettings/.kilo中的既有文件去重摘要（排除新ProjectModules目录及其根.meta、02-feature-matrix.md、03-actionkit-implementation.md）：3387文件，SHA256：

`A878D9831BAA848DEAB652CD2DB66B130FEF346BAFC41889AE7FDDBC0ABB8758`

原01-audit.md SHA256：`F150FBBE06179C8CC9E9106A9ECEA5A00F4746BE896ECC6461A3B24DC48CB4CB`。

原02-extraction-plan.md SHA256：`D713A8EEFF04A78712447E1E1B6B04D2F8817F51442BE360E5CC3F41B5ABA7A2`。

这些是本轮实际读取的起始值；结束将用相同枚举口径核对，若外部发生修改，只记录不覆盖。Library/Temp/构建输出不在该摘要范围，不声称整个机器全程无变化。

结束按相同口径复算：3387个既有文件仍为`A878D9831BAA848DEAB652CD2DB66B130FEF346BAFC41889AE7FDDBC0ABB8758`，工具返回`MatchesStart=True`；原Ease、01审计、02提取计划的三个单文件SHA256也与上列值一致。由此确认监测范围的原源码、业务资源、Packages、ProjectSettings及只读规划未变。矩阵/03和新增ProjectModules按设计排除，不伪称它们也未变。没有Git diff、Unity编译/测试或独立工程验收结果。

## B1.2 范围与实现

本批新增19个C#文件，沿用首批3个asmdef，不改它们。原Ease与其测试不改，测试程序集通过新增fixture扩展；所有新文件位于`Assets/ProjectModules/ActionKit/`。

| 路径 | 已实现功能 / 矩阵 |
|---|---|
| Runtime/API/ActionKit.cs | public partial门面、ID_GENERATOR与11个工厂重载：Delay/Sequence/DelayFrame/NextFrame/Lerp/Callback/Condition/Repeat/Parallel/Custom及Custom<TData>；AK-001部分 |
| Runtime/API/IAction.cs | 两枚举、IAction<TStatus>/IAction完整成员；AbstractAction<T>全部派生钩子、Allocate/Reset/Deinit；手动Execute/Finish；AK-002/003 |
| Runtime/Actions/Delay.cs、DelayFrame.cs、Lerp.cs、Callback.cs、Condition.cs | 全部原基础动作算法/字段/重载/链式，保留构造与访问级别；AK-010..014 |
| Runtime/Actions/ConditionSequence.cs、Sequence.cs、Parallel.cs、Repeat.cs、Custom.cs | 完整原组合和typed/untyped自定义动作；AK-015..019 |
| Runtime/Internal/ActionPool.cs | 仅动作使用的factory/reset/预热/栈式分配回收，不搬PoolKit；AK-056部分 |
| Runtime/Internal/ActionQueue.cs | 现代动作延后回池及小回调协议；旧IDeprecateAction队首能力仍待旧API批次；AK-007部分 |
| Runtime/Internal/ActionRecycleDriver.cs | 为延后回收提供Unity Update/DDOL/退出/销毁/session-reset，**不执行动作**；不依赖旧单例 |
| Runtime/Internal/AssemblyInfo.cs | 仅向两个已命名测试程序集开放Internal，未把内部支撑变成公开框架 |
| Tests/EditMode/ModernActionTests.cs | 手动状态推进/派生钩子、10类动作、组合、重置/回收与原问题characterization |
| Tests/EditMode/ActionInternalSupportTests.cs | 私有factory、预热/reset、批次排空、重入和异常传播 |
| Tests/PlayMode/ModernActionPlayModeTests.cs | 真实帧延时、NextFrame/链式、暂停跨帧、timeScale0手动dt、延后回收、driver静态重置/销毁重建及场景存活 |

**本批还没有Start/StartGlobal/StartCurrentScene、IActionController、自动executor、7个全局消息事件、Coroutine/Task、ScreenTransition、旧API和工具。** 它们不是“不支持”或被裁掉，而是尚未开始的后续稳定批次。新门面当前只列实际实现的基础工厂，不放抛异常、固定默认值或日志占位方法。调用者在本批只能显式`Execute(dt)`驱动动作，并显式`Deinit()`结束所有权。

## B1.2 底层与差异

### XF 与 Internal 的实际选择

- 核对了当前XFGameFramework/Runtime/Manager/ClassObjectPoolManager.cs:134-173。其公开`Load<T>()`要求class,new()；动态Type路径使用Activator默认构造，没有Func工厂/逐类型预热/reset约定。原动作多个类型为private构造，故不能直接替换或为使用XF擅自开放构造。
- 核对了当前XF的UpdateManager.cs:27-39,84-149。它能提供Update订阅，但本回收支撑需要明确的拥有者、待处理队列、场景独立、host销毁/session重置及不在退出时重建。给XF再加订阅恢复/探测将不比一个仅回收的薄组件更小；当前沿用计划选择Unity私有driver，不改变原动作的dt/帧算法。
- ActionPool仅约40行，保留原SimpleObjectPool使用到的factory/reset/initCount/LIFO行为，不提供通用服务注册、引用全局池、Clear/Max/SafePool假实现。旧安全池需求留旧API批次处理。
- Sequence/Parallel的原ListPool.Get仅在各动作实例创建时取列表，之后随实例Clear而没有Release；改为每实例`new List<IAction>()`仍长期复用，不在每帧重新分配列表。
- 本批没有调用XF API，没有新增项目级独立模块/Shared/Common/Core。Internal当前消费者仅为这10类动作和AbstractAction，不为假想Audio复用扩成服务框架。

### 显式行为边界

- 10个原动作只改namespace、池类型与两处列表来源；保留原版权、公开/保护/接口成员、工厂默认值、private构造和算法。基础门面脱离Architecture的完整生命周期/公共底层契约仍属AK-042后续，不伪称整个公开继承面已兼容。
- 保留原现代Execute：首次只OnStart，Started才使用dt；已Paused但NotStart仍OnStart；直接多次Execute Finished可能多次OnFinish；手动完成不自动Deinit。这与未来自动executor的所有权不同。
- 多数Deinit仍延后回池；非泛型Custom原来立即回池/缺新ID/Data残留，当前未修。Delay工厂残留/ID、ConditionSequence提前Deinit的child缺口、空Parallel不完成、Repeat负值等原问题也未静默修正。
- 新内部回收队列使用固定批次数量，排空中新增回调推迟下一轮；防止递归Drain；取出回调后再调用，异常向外传播，已消费项不重播、未消费项保留。这是旧List/foreach支撑替换的明确差异，不改变动作自身完成算法。
- 回收driver仅在Play Mode需要排队时创建，命名为`[Project.ActionKit] RecycleQueue`，无旧Resources或脚本路径。停用driver会暂停自动Drain；销毁时排空，后续需要时重新创建。SubsystemRegistration重置静态状态，必要时查找已有自身组件，避免关闭Scene Reload时重复创建。
- EditMode手动驱动不创建场景对象，测试通过Internal Drain收尾；不能声称已提供通用Editor自动动作运行器。
- 内部退出/销毁/静态重置的收尾不等于完整模块Shutdown已实现。完整session/自动executor生命周期仍须在相应批次集成验证。

## B1.2 验证与当前门槛

源码预期用例：新增37个EditMode（ModernActionTests的19个Test+15个TestCase，InternalSupport的3个Test），新增7个PlayMode。连同首批应为 **88个EditMode、9个PlayMode**。这些是源码枚举，不是已发现或已通过的运行数量。

已完成独立静态审查：未发现确定的编译或正常路径断言错误；逐文件原对照确认10动作的必要替换没有新增算法差异。审查发现两处新增测试异常路径清理问题，已修正：动态Delay的factory清理移入finally；临时scene/UnloadSceneAsync句柄交给UnityTearDown等待。没有修改原Runtime缺陷来让测试通过，没有添加Ignore或放宽断言。

本批未由代理执行Unity导入/编译/测试，没有独立工程、Player或AOT结果。网络中断后已复核落盘源码，未重放覆盖业务文件；中断不是本批测试通过或失败证据。

当前最小操作：

1. 在已打开的Unity工程保持非Play Mode，等待正常导入/编译；需要时只执行Assets > Refresh，不Reimport All或清缓存。
2. 若Console有编译错误，先保留完整错误文本，停止增加下一批源码。
3. 无编译错误后，在Test Runner分别选中`Project.ActionKit.Tests.EditMode`和`Project.ActionKit.Tests.PlayMode`运行整个程序集，**包括首批Ease回归**。源码预期88/9；失败保留完整堆栈，数量不符也单列记录。
4. 当前测试自行创建/销毁临时对象/场景并恢复timeScale，不需要打开游戏场景或配置业务组件。新.meta由Unity生成，不手工复制原GUID。

通过本批后再继续自动控制器/启动和其它模块功能。**当前暂停于B1.2验证，不开始AudioKit。**

## B1.2 原文件核对

B1.2开始时（首批用户验收之后），同口径3387个既有文件SHA256为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`。这是新的批次基线，不拿B1.1之前的摘要覆盖用户/Unity后续保存。

已验收的4个首批文件分别记录：Ease源码`CD29DAD7B5A0354E68674F20DCB33EFFDE1AA845E4F38B336F2E1481C0892007`；Runtime asmdef `7D039A78BACD061B3DDB5CE416181F895E9FE1D1BCFAD430BB547A02ABDACE5D`；Ease EditMode测试`76CA852A34BD66FB9C6FC452ACDB5A81958B0208F0A3C0FF456CF7706AD1E0D1`；Ease PlayMode测试`603217C2E49A942BABFE7F29668765B5D9FC638280F80072380FCACAF717E44F`。结束再核对，不回退不属于本批的变化。

交付前复算相同3387文件集合仍为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`，工具返回`MatchesB12Start=True`；上述4个首批文件也逐个一致。由此确认本批未改变所监测的原框架、业务、Packages、ProjectSettings、01审计和02计划；B1.2新增文件及授权文档更新不在该“原文件未变”断言中。

最终活跃`.cs/.asmdef`依赖扫描未命中`using QFramework`、`QFramework.`、`QFramework.CoreKit`、旧Assets/QFramework路径、旧池类型符号、AudioKit、MainModule、旧Resources加载或NotImplementedException/Assert.Ignore。保留的`mSimpleObjectPool`仅是字段名，其类型已为内部ActionPool，不是旧池依赖。Runtime现有17个C#、Tests有5个C#，仍仅原先3个asmdef；源码预期88 EditMode/9 PlayMode再次计数一致。以上均为静态证据，B1.2实际Unity结果尚待用户操作。

### B1.2 首次Unity编译反馈与兼容修正

2026-10-02 17:41 UTC，用户提供本批真实Unity编译错误：ModernActionTests、ActionInternalSupportTests、ModernActionPlayModeTests三个类上的`NonParallelizable`无法解析，各报告Attribute和短名两条CS0246，共6条。这是本批新增测试源码的兼容性错误，不是动作Runtime算法错误；前述静态审查未检出此特性版本差异，不能再将其描述为已获Unity验证。

本地依据：Test Framework 1.3.9/package.json:18-21与packages-lock.json:136-141/255-263实际依赖`com.unity.ext.nunit 2.0.3`；其README/Documentation说明为NUnit 3.5定制版。只读检查实际`net40/unity-custom/nunit.framework.dll`得到Version=3.5.0.0、TestAttributeAvailable=True、NonParallelizableAttributeAvailable=False。当前Test Runner的`NUnitExtensions/Runner/CompositeWorkItem.cs:175-207`逐个完成child.Execute后才进入下一个子测试，本流程不依赖该特性实现串行执行。

修正仅删除上述三个测试类的`[NonParallelizable]`，不改Runtime、asmdef、Packages、测试方法/断言、SetUp/TearDown或UnityTearDown；不加伪特性、不升级NUnit、不Ignore/跳过用例。用例预期仍为88 EditMode/9 PlayMode。修正后尚需Unity重新编译并运行两个完整程序集，**当前没有“错误已消失/测试已通过”的新Unity结果**，停止门槛仍为B1.2验证。

### B1.2 后续用户验收

用户2026-10-02 17:52 UTC明确确认“测试都通过了”，覆盖前述兼容修正后的验证门槛。本节是最新结果，前段“尚需重新编译/测试”保留首次修正时的历史。没有更改原动作算法、关闭原缺陷或宣称完整ActionKit已通过。

## B1.3 控制器与自动执行

本批新增6个Runtime C#和3个测试C#，沿用原3个asmdef；不修改首两批已验收源码、业务、原QF/XF、Packages或生成项目文件。

| 路径（相对Assets/ProjectModules/ActionKit/） | 实现及原来源 |
|---|---|
| Runtime/API/IActionController.cs | IActionController全部7成员、ActionController/Allocate/Reset/Deinit/Recycle及IgnoreTimeScale；原Framework/IAction.cs:27-39,125-172,278-284 |
| Runtime/API/IActionExecutor.cs | IActionExecutor.Execute、UpdateAction完整原调度契约；原Framework/IActionExecutor.cs |
| Runtime/API/ActionStartExtensions.cs | 原六Start/Pause/Resume；原Framework/IAction.cs:178-235，仍立即尝试Execute(0) |
| Runtime/Internal/MonoUpdateActionExecutor.cs | 原prepare列表、IAction-key字典、scaled/unscaled执行、完成回调/Deinit/移除controller算法；原Internal/Executor/MonoUpdateActionExecutor.cs；GetOrAdd改原生GetComponent/AddComponent |
| Runtime/Internal/ActionKitCurrentScene.cs | 原public场景Mono组件、lazy SceneComponent和公开static字段；原Internal/ActionKitCurrentScene.cs，保持不DDOL |
| Runtime/Internal/ActionGlobalHost.cs | 仅StartGlobal所需私有Unity持久宿主，代替旧MonoSingleton/路径；非通用服务、非7消息/协程完整交付 |
| Tests/EditMode/ActionExecutionTests.cs | 14例：controller成员/ID guards/回收/空操作、两个local同步重载、dt选择、取消/重启/组件复用、重入/重复Start、宿主销毁characterization |
| Tests/PlayMode/ActionStartPlayModeTests.cs | 14例：三scope×两个重载、自动序列、暂停恢复/Deinit、IgnoreTimeScale、caller与GO停用区别、销毁/global存活/currentScene卸载 |
| Tests/PlayMode/ActionStartTestCaller.cs | 测试专用MonoBehaviour，只作为本批caller组件；Runtime不依赖它 |

### 本批底层边界

- 原控制器/task的SimpleObjectPool改为现有ActionPool，保留factory/预热/reset使用方式，不搬CoreKit。IActionController仍是可复用raw对象而非不可变token。
- 原GetOrAddComponent<T>改为Unity的GetComponent/AddComponent；返回值和构造时机不变，不额外引入FluentAPI。
- 本地XF UpdateManager只能提供全局三相委托，不能保持local GO disable、当前scene销毁和全局scope的差别；若每GO另做适配反而重复原executor，因此保留动作系统自己的MonoUpdateActionExecutor。没有为了使用XF改成Timer或全局bus。本批没有调用XF能力。
- ActionGlobalHost是ActionKit内部最小Mono宿主，不是新的独立模块：持久Root名称`[Project.ActionKit] Global`、SubsystemRegistration重置static、查找已有自己组件、退出时不重建。后续7消息/Coroutine可在明确Action生命周期中接入，不能靠本批宿主存在声称它们已实现。
- CurrentScene仍为首次创建所在scene的单个static组件，不升级为每additive scene独立服务；保留原多实例风险AK-928，没静默改public/static契约。
- 自动executor的Update主体抽为internal Tick(scaledDeltaTime,unscaledDeltaTime)，Unity Update只传原Time.deltaTime/unscaledDeltaTime；这是测试入口，不是新的公开调度模式。

### 保真与未解决问题

保留原同步启动/重启Finished动作、完成callback先于Deinit、controller ID比较、Paused属性直接转发、完成后异步controller回池，以及Update期间的新Start放prepare队列下一轮接入。

AK-903/904/905相关问题没有静默修正：同步完成controller未自动回池；同Action重复Start会覆盖旧task/callback；ActionTask原池并未在正常移除中Recycle；executor销毁没有自动取消已启动动作；空/stalecontroller和回调重入存在原限制。这些均有明确测试/记录，**characterization通过不表示生命周期问题已解决**。不能将本批保真复制称为所有资源回收或module Shutdown已正确。最终阶段完成前仍需针对原问题明确处理/接受；不通过删API避免它们。

已有动作算法、回收driver及池不改；没有Common/Core/IOC、资源加载路径、旧脚本GUID或业务启动类依赖。原版权/来源链接继续保留。

### 本批测试与验证门槛

新增源码用例14 EditMode、14 PlayMode，连首两批完整回归预期为 **102 EditMode、23 PlayMode**。没有NonParallelizable、Ignore、占位方法或新依赖安装；测试数量是源码枚举，不是实际Test Runner发现/执行记录。

本批待当前Unity普通导入/编译后，分别在Test Runner选中`Project.ActionKit.Tests.EditMode`与`Project.ActionKit.Tests.PlayMode`运行整个程序集。无需打开业务场景；用例自己管理临时GO/scene、等待卸载和恢复timeScale，失败时也保留清理。Console错误/测试失败堆栈/发现数量差异保留完整，不能提前标通过。新.meta仍由Unity生成，禁止复制旧GUID。

当前仅完成源码和测试写入，独立静态审查/源码依赖与旧文件内容核对在交付前记录；没有代理Unity编译、独立工程、Player或AOT通过结论。**停在B1.3验证，不继续叠加下一批，不开始AudioKit。**

### B1.3 原范围状态

开始时原工程3387文件按同口径（排除ProjectModules及两份实施文档）SHA256为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`。末次复算采用同口径，不把首两批用户/Unity的导入行为归为本批手工修改；若外部发生变化只记录、不覆盖。B1.3新增9个C#仅在授权新模块目录，01审计/02提取计划不修改。

交付复算为同一3387文件、同一SHA256，`MatchesB13Start=True`；受监测原源码/业务/Packages/ProjectSettings/01/02计划未改变。活跃新`.cs/.asmdef`检索旧namespace/程序集/路径、旧pool/singleton、AudioKit/业务、Resources、空占位/跳过测试/NonParallelizable均无命中。现有23个Runtime C#、8个Tests C#，仍3个asmdef；源码计数102/23一致。版权中的来源名称不是运行依赖。

独立只读审查未发现确定编译错误/正常路径必然错误断言，发现了新增PlayMode fixture借用既有global host时失败清理可能提前回收其仍调度的controller。已将该fixture明确设为受控隔离前置条件：开始时不得存在新模块的global/currentScene host，否则清楚失败、不跳过，也不销毁/推进别人的宿主；正常用例只创建自己host，清理先销毁并等待一帧，再取消/回收自有对象，等待临时scene卸载。这个修正只处理新增测试隔离，不改变Runtime AK903-905缺口。

该前置条件是测试环境约定，不是Runtime只能单独运行的限制。在Unity Test Runner测试场景运行，不需要手工设置或修改业务场景；若报前置条件错误，保留完整信息而不删除场景里的他人对象或修改测试断言。仍没有本批实际Unity通过结果。

### B1.3 后续用户验收

用户于2026-10-02 18:34 UTC明确确认全部Test通过，覆盖本批门槛及此前回归。前述“尚未验证”保留当时记录，不作为最新状态。原AK903-905等问题不因characterization通过而认定已修复，Stage整体与独立Player/AOT仍未完成。

## B1.4 消息、订阅与异步动作

新目录内新增11个Runtime源码（8个订阅文件及3个动作/Shortcut），扩展两个既有文件ActionKit.cs/ActionGlobalHost.cs；新增4个测试fixture。原动作、controller/executor、池/回收及3个asmdef不改。未复制任何原.meta、未修改业务、旧QF/XF、Packages或生成项目文件。

| 路径（相对Assets/ProjectModules/ActionKit/） | 功能及原证据 |
|---|---|
| Runtime/Actions/Coroutine.cs | 原Internal/Action/Coroutine.cs全部83行，CoroutineAction私有构造/工厂/状态成员/链式/Enumerator.ToAction；pool替为ActionPool，host转到新ActionGlobalHost |
| Runtime/Actions/Task.cs | 原Internal/Action/Task.cs全部98行，TaskAction全部公开操作/私有构造、Task/链式/ToAction；保留原await/Dispose语义，pool局部化 |
| Runtime/ShortCut/AudioSourceShortCutExtensions.cs | 原ShortCut文件全部33行；Unity AudioSource.PlayAction，不引用AudioKit |
| Runtime/Subscriptions/IUnRegister.cs | Framework/Scripts/QFramework.cs:468-504的IUnRegister/IUnRegisterList、List扩展与CustomUnRegister，迁为Project.ActionKit类型 |
| Runtime/Subscriptions/EasyEvent.cs | 同源:795-841的IEasyEvent、EasyEvent、EasyEvent<T>，包含无参interface实现、初始立即调用/手动Trigger/委托注销 |
| Runtime/Subscriptions/OrEvent.cs | 同源:921-957的组合/追加/初始调用/list/token注销全部相关成员 |
| Runtime/Subscriptions/UnRegisterTrigger.cs | 同源:507-528的公开抽象组件、HashSet去重、Add/Remove/UnRegister |
| Runtime/Subscriptions/UnRegisterOnDestroyTrigger.cs、UnRegisterOnDisableTrigger.cs | 同源:530-544的Unity消息绑定 |
| Runtime/Subscriptions/UnRegisterCurrentSceneUnloadedTrigger.cs | 同源:546-572的持久trigger/Get/场景卸载注册注销；原作用域实际上是任意sceneUnloaded，不擅自改成只绑定创建scene |
| Runtime/Subscriptions/UnRegisterExtension.cs | 同源:580-611的GameObject/Component Destroy与Disable各重载、当前scene卸载绑定，共5个公开入口，原生GetComponent/AddComponent |
| Runtime/API/ActionKit.cs（扩展） | 原ActionKit门面Coroutine/Task及7个EasyEvent属性，原名称、参数/返回操作保留 |
| Runtime/Internal/ActionGlobalHost.cs（扩展） | 原Internal/ActionKitMonoBehaviourEvents.cs:18-76七消息、退出先发布事件、yield IEnumerator→onFinish wrapper，私有Unity宿主代替SingletonKit |
| Tests/EditMode/ActionSubscriptionTests.cs | 11例：事件顺序、重复注册、初值前后时机、generic/无参interface、delegate解绑、token二次行为、列表/Or全部闭包及异常传播 |
| Tests/EditMode/CompletedTaskActionTests.cs | 4例：已完成Task同步完成、显式工厂/Reset、ToAction/链式、启动前Paused |
| Tests/PlayMode/GlobalMessagePlayModeTests.cs | 8例：7属性/单host、原生Update/Fixed/Late、GUI/focus/pause/quit handler分派、全部注销重载、trigger Add/Remove/去重、scene作用域 |
| Tests/PlayMode/AsyncActionPlayModeTests.cs | 11例：getter/原IEnumerator复用、协程→Task顺序、paused/Deinit底层继续、未完成Task/Dispose风险、Unity AudioSource Stop/Destroy/Pause/Deinit |

### XF、Internal与公共支撑边界

XF EventManager.cs:200-238的Add/Remove/Trigger以key和XFEvent委托工作，Add返回void；无法直接满足ActionKit返回token、无参/typed interface、Or/list及Unity生命周期绑定。改造成该闭包需另造key/身份/生命周期层，不更轻。因此本批仅复制必要订阅操作并归属新模块，不搬完整事件Bus、ICanSendEvent、IOC、Architecture或整个Framework.cs。

这不是新增项目级模块：已有实际消费者是7个ActionKit消息、返回的订阅/Or对象、自动销毁/停用/场景解绑；它们放Runtime/Subscriptions作为明确的模块公共契约，而不是塞成泛化Internal服务。旧EnumFSM的typed state-event支撑在后批解决。AudioKit未来自己的公开token不因此强制依赖ActionKit。

Coroutine原本依赖Action全局Mono宿主，保留Unity StartCoroutine/IEnumerator本体；XF CoroutineStarter只有Start/Stop，不提供本模块七消息与同host顺序，用现有ActionGlobalHost承载更直接。Task保留标准Task算法；Shortcut只用Unity AudioSource/Custom，没有新的播放管理框架。本批仍没有调用XF API，没有增加大型独立模块/共享Core。

Unity2022适用的原UNITY_5_6_OR_NEWER注销分支全部保留为Unity实现。Framework的GODOT分支不是本地ActionKit运行时Godot后端，不带入新Unity程序集；不据此宣称跨平台引擎兼容。原来源的MIT/作者/链接保留，不把来源文字算旧运行依赖。

### 行为与仍未解决的风险

- 新全局事件是Project.ActionKit命名空间的公开必要类型；原Register/UnRegister/Trigger/初值顺序、参数名、返回token/Or继续操作可用。迁移后不与旧QF bus共享订阅，不保持旧类型身份。
- 保持CustomUnRegister同一struct实例第二次注销抛异常、Or解绑一个listener时断开全部source等AK-927原问题；事件异常仍向外传播，不吞掉/日志伪成功。
- Coroutine底层由全局host运行；原Pause/Deinit不停止IEnumerator、迟到完成可回写池内对象。Task仍async void且Dispose不是取消，未完成Dispose可能在设置Deinited之后抛异常并阻断回池。AK907/908没有静默修正。
- Task fault/cancelled-task在async void中的未捕获错误路径尚未进入本批常规Runner复现，避免把不可控异常造成Runner故障后冒充兼容。仍为明确专项验证缺口，不能据正常Task测试关闭风险或删除相应能力。
- Shortcut保留`api.Data && !api.Data.isPlaying`；原Pause可当结束、destroy无法完成、Deinit不Stop Unity音源的AK914均单列。新测试用loop的临时静音Clip验证状态，不修改游戏音效或AudioKit。
- 退出消息先触发再设置“不再创建host”标记；本批人工向handler分派Quit而不调用Application.Quit。真实Editor/Player退出、平台焦点/暂停、GUI显示仍需最后人工/构建验证，不能把SendMessage当真实退出证据。
- 全局host被禁用/销毁的行为仍遵循Unity；session Reset只重置host静态查找/退出标记，不宣称关闭Domain/Scene Reload时所有旧订阅/异步工作已自动清空。完整初始化/Shutdown收尾与原缺陷处置在后续Lifecycle批次统一，不等于当前已完成Stage。

### 测试与停止门槛

本批新增15个EditMode、19个PlayMode，连此前回归源码预期 **117 EditMode、42 PlayMode**。现有34个Runtime C#、12个Tests C#，仍3个asmdef。测试使用现有Unity Test Framework 1.3.9/NUnit定制3.5，不用NonParallelizable/Ignore，不升级依赖。

Fixture要求拥有自己global host/scene-unregister trigger，失败时不销毁外部host。临时scene异步卸载/订阅清理/播放对象与Clip销毁明确；Task/coroutineGate测试在结束前释放自有底层工作，不能为清理刻意复用待回写动作到别人任务。原未完成Task Dispose异常后的回收恢复仅在fixture中，通过公开Deinited字段收尾，不改变Runtime行为。

本批静态审查发现一处新增测试覆盖问题：Coroutine完成标记出现时，父Sequence可能还没有推进到Task，因此过早SetResult会只测试已完成Task。已改为在Task getter标记启动并等待此标记、断言Task.IsCompleted=false和尚无下游callback，再完成Task验证顺序。清理还主动清除测试拥有的Custom<AudioSource>.Data后回收，以隔离原自定义动作数据残留；没有因此修改Runtime算法或将已登记风险关闭。

当前待Unity普通导入、编译后，分别运行整个`Project.ActionKit.Tests.EditMode`和`Project.ActionKit.Tests.PlayMode`。需有效的Unity音频环境供Shortcut状态用例使用，不能用Ignore伪通过。若环境无音频或有编译/测试错误，保留完整文本；不开始下批、不自行删除用例或改算法使其通过。新.meta由Unity生成，原项目不清缓存、不重导入全部、不切平台。

B1.3用户通过不能自动覆盖本批两个修改文件。独立静态审查已完成，复核覆盖问题修正后没有尚未解决的确定新增编译/API/断言或清理问题；当前仍无本批Unity执行/Player/AOT结果。handler模拟和真实平台消息、有效音频环境等证据边界仍按前述区分。**停在B1.4验证，不开始AudioKit。**

### B1.4 原范围基线

开始时按同口径3387个原工程文件（排除ProjectModules和02-feature-matrix/03实施记录）SHA256为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`。01原审计/02提取计划不改；旧模块、Packages、业务、Library/PackageCache无手工写入。末次按同口径核对，有外部改动只记录，不覆盖。

收尾复算仍是3387文件与同一SHA256，工具返回`MatchesB14Start=True`。当前新目录34个Runtime C#、12个Test C#、3个asmdef；源码用例117/42一致。活跃`.cs/.asmdef`检查旧using/完整类型、QFramework.CoreKit、旧Assets路径、旧pool/singleton/Architecture、AudioKit/MainModule/旧Resources、NotImplementedException、Assert.Ignore或NonParallelizable均无命中。没有将版权链接计为运行依赖，也不把静态扫描当Unity编译成功。

### B1.4 后续用户验收

2026-10-02 19:03 UTC用户明确确认本批全部测试通过，覆盖上述门槛及此前回归。此确认不意味着Task/协程取消、回调复用和其他原缺陷已修复，不覆盖真实退出/平台GUI/独立Player等最终验证。

## B1.5 ScreenTransition 与模块资源

新增4个Runtime、1个Editor工具、2个测试源码和1个Editor asmdef。修改Runtime及两个Tests asmdef增加显式UnityEngine.UI，修改AssemblyInfo允许Project.ActionKit.Editor绑定内部组件。没有改首四批动作算法、业务、旧QF/XF、Packages或生成项目文件；不手工创建任何.meta/GUID/Prefab。

| 路径（相对Assets/ProjectModules/ActionKit/） | 实现及来源 |
|---|---|
| Runtime/ScreenTransition/ScreenTransition.cs | 本地原同名文件3个工厂，public嵌套ActionKit.ScreenTransition与FadeIn/Out默认黑色/1秒等完全保留 |
| Runtime/ScreenTransition/ScreenTransitionFade.cs | 原Color/FromAlpha/ToAlpha/Duration返回self、OnStart/OnExecute/OnFinish算法；ColorAlpha Fluent替为直接读写Image.color.a |
| Runtime/ScreenTransition/ScreenTransitionInOut.cs | 原两个Allocate（含继承无参）、泛型constraints、In/Out/OnInFinish、启动child global Sequence；不擅自修父完成/取消问题 |
| Runtime/ScreenTransition/ScreenTransitionCanvas.cs | 原组件/ColorImage/OnSingletonInit（原空钩子）保留；只用Unity查场景实例、独立Resources键与持久实例；不搬SingletonKit |
| Editor/Project.ActionKit.Editor.asmdef | Editor-only，只引用本Runtime/UnityEngine.UI，关闭预编译DLL自动引用；Runtime不引用Editor |
| Editor/ScreenTransitionResourceCreator.cs | 明确Unity菜单生成新Prefab/新GUID并绑定模块脚本与Image；不加载旧资产、原脚本GUID或旧目录 |
| Tests/EditMode/ScreenTransitionTests.cs | 11例：默认值、所有fluent/参数、时间/首尾RGB-alpha、Reset/forceFinish、原池残留/重载、场景实例优先及生成资源组件/程序集配置 |
| Tests/PlayMode/ScreenTransitionPlayModeTests.cs | 7例：新资源持久单例、自动fade序列/unscaled、InOut中间回调/child顺序、原父暂停/取消缺口、实例销毁重建 |

### 底层选择与结构

UI实际程序集名已从本地UnityEngine.UI.asmdef及生成索引核实为UnityEngine.UI。项目manifest/lock声明的ugui版本与所读缓存版本不一致，保留该限制；本批不升级包、不强制重解UPM，以当前Unity实际编译/导入确认API。

新Screens只依赖Unity UI和本模块动作核心，不使用XF Panel/MVC、Toolkit UI或游戏Canvas。复用XF panel根将改变独立性/层级/默认sorting/scene生命周期；原算法本就是原生Image，原生接口直接替Fluent最小。没有调用XF能力，没有新增项目级独立模块或通用PrefabSingleton服务。

资源键明确变为`ProjectActionKit/ScreenTransitionCanvas`，新资产位置为`Assets/ProjectModules/ActionKit/Resources/ProjectActionKit/ScreenTransitionCanvas.prefab`。场景中已有本模块组件时保持原优先使用规则；仅资源实例DDOL，不无故把已配置场景对象改持久。旧资源不作为fallback，不复制其meta/GUID，不在新源硬编码旧资源路径/脚本。

缺资源或Prefab缺组件/ColorImage时，新getter报明确InvalidOperationException及创建步骤，代替旧的后续null引用；这是配置失败诊断差异，不是空实现或继续用旧资源。ColorImage有效时的默认/插值/回调操作不变。原内部ISingleton角色不保留框架接口身份，OnSingletonInit原空钩子无新职责。

### 资源创建工具与未保存场景

工具仅有`Tools > Project ActionKit > Create ScreenTransition Resource`菜单，不自动导入后执行。非Play Mode才能调用；目标Prefab或.meta已经存在时保留现状、提示不覆盖。若保留的是残留meta而没有有效Prefab，需核查实际文件，不擅自删除或生成同GUID覆盖。

创建使用Unity AssetDatabase.CreateFolder及PrefabUtility.SaveAsPrefabAsset，不手写YAML/meta。最初Additive临时场景方案在用户Editor环境中失败，现已修正为独立Preview Scene：临时对象立即以HideAndDontSave创建，移入Preview后清标记并添加UI组件，结束销毁所有临时对象、ClosePreviewScene。不调用SetActiveScene、不保存/关闭用户已有场景，不调用全局SaveAssets/Reimport或清缓存。没有用旧Prefab作为nested/variant来源。实际修正后的Unity创建结果仍待确认。

依据原169行Prefab保留Overlay、排序10000、UI layer5、ConstantPixelSize与像素/参考分辨率参数、stretch ColorImage黑色/raycastTarget=true/无sprite/CanvasRenderer设置。原没有GraphicRaycaster，不擅自增加输入阻挡组件。原Root的序列化localScale为0，本批也保留，不将未知的Unity归一化或疑似旧资源缺陷静默修正；画面可见性必须后续人工检查，不能只看alpha测试称视觉完整。

当前只写了资源创建工具，**尚未执行菜单，尚无新Prefab/GUID/资源导入结果**。旧UI脚本使用的DLL式GUID/fileID也没有简单文本替换；新工具让当前Unity生成实际UI组件引用，待生成后测试检查。

### 保留行为与阻塞项

- Fade保留Color/alpha/Duration/Mathf.Lerp、强制Finish只写目标alpha、直接Allocate复用的原设置残留、并发共享Image、0/负Duration的原行为。原AK913仍开放。
- InOut保留其泛型/配置/中间回调与子序列完成画面；原父动作不会自动Finished，Pause/Deinit不传播，子回调/引用也有池残留风险（AK912/937）。本批只明确记录和characterization，不伪称组合的父生命周期正确；最终阶段结项前需明确处理这些原问题。
- 编辑器创建菜单是本模块资源接入工具，**不等于SingleFileCreator、API Help或全部Editor能力已完成**。那些工具、旧API/FSM/Timeline及样例仍完整在范围内。

### 本批验证与最小Unity操作

新增源码11 EditMode/7 PlayMode，连此前应为 **128 EditMode、49 PlayMode**，未运行。现有38 Runtime/1 Editor/14 Test C#，4个asmdef。所有先前测试要完整回归，尤其Runtime增加Unity UI依赖后的程序集引用检查。

1. 当前已打开工程保持非Play Mode，等待新代码/程序集普通导入和编译。若有编译错误，保留完整错误文本，不继续生成资源或增加下一批。
2. 编译无误后点击`Tools > Project ActionKit > Create ScreenTransition Resource`一次。应在Console看到Created提示，并选中新资源Prefab。Unity生成新.meta/GUID；不是复制旧Prefab，也不是Reimport All。
3. 两个完整程序集分别Run Selected，源码预期128/49；若资源未创建，相关测试会明确失败，不跳过/删断言。失败/数量差异保留完整结果与堆栈。
4. 新Prefab与Unity导入引用检查通过后，屏幕实际可见性、不同Game View尺寸、0规模Root归一化、输入是否拦截等仍记录待人工，不能假称自动测试覆盖全部视觉。无需修改业务场景或挂载旧框架组件。

本批实际资源创建必须经Unity操作，当前停在该门槛。独立静态审查及最终内容摘要在交付前补录；当前没有Unity编译/测试/可视化/Player/AOT通过结论。**不开始AudioKit或下一稳定批次。**

独立静态审查已完成，发现的新增Probe测试配置在Sequence.Reset时被清空问题已修正：仅测试Probe改为OnDeinit清理，保留参数直至子动作启动，Runtime不变。复核后没有尚未解决的确定新增编译/API/正常路径断言或清理问题；资源真实性与视觉效果仍未验证，不将静态结论升级为Unity通过。

### B1.5 原文件基线

开始时同口径3387个旧工程文件SHA256为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`，排除新模块及两份实施文档。新模块内本批明确改动asmdef/AssemblyInfo，不能把这些排除项说成未修改。01审计/02提取计划、旧源码/资源及Packages不改；末次同口径复核并保留外部变化。

收尾复算仍是3387文件、同一SHA256，工具返回`MatchesB15Start=True`。源码预期128/49、38 Runtime/1 Editor/14 Test C#及4asmdef再次计数一致。活跃源码/asmdef未检出旧using/完整类型/QFramework.CoreKit、旧Assets路径、AudioKit/业务、不兼容特性、Ignore或空占位。来源注释提及原PrefabSingletonProperty只是出处，不是类型/程序集依赖，不能剥掉出处来制造文本零命中。新资源查找只引用已定义的模块ResourcePath；实际新Prefab枚举仍无文件，菜单未执行，资源/GUID未假称已生成。

### B1.5 首次资源测试反馈

2026-10-02 19:45 UTC，用户报告`CreatedResourceHasTheModuleScriptAndOriginalPrefabConfiguration`在ScreenTransitionTests.cs:180失败：Resources.Load返回null。随即只读核查新模块目录：Resources目录及ScreenTransitionCanvas.prefab/.meta均未检出；创建工具AssetPath与Runtime资源键对应一致。这是明确的资源前置步骤未完成，不是Fade数值断言失败，不能据此宣称生成工具已经运行失败。

未改源码或断言、未回退旧资源。当前仍需用户在非Play Mode执行`Tools > Project ActionKit > Create ScreenTransition Resource`，确认Created提示与新Prefab，再重跑两个完整程序集；若创建菜单报错，保留其真实错误另行处理。此次只记录用户提供的单项失败，不推断其它用例结果或整批通过。

### B1.5 资源创建失败与修正

2026-10-02 19:47 UTC，用户执行菜单后报告`InvalidOperationException: Could not activate the temporary resource-building scene`，定位ScreenTransitionResourceCreator.cs原:42。这次是本工具的实际运行失败：Additive新场景调用SceneManager.SetActiveScene返回false，工具硬性要求激活导致保存前终止。没有证据进一步判定当前Editor为何拒绝激活，不编造Unity版本/API普遍不可用或用户操作错误。

已只修改创建工具的场景构建/清理路径：用Unity2022.3官方支持的NewPreviewScene/ClosePreviewScene；通过CreateGameObjectWithHideFlags立即创建不保存临时对象，先MoveGameObjectToScene到独立Preview，再添加Canvas/Image等组件并正常序列化。root与未挂父的Image分别纳入finally，发生异常也销毁；不再需要切换/恢复活动场景，不触碰原场景内容。资源参数、路径、已有Prefab/meta不覆盖约束和Runtime算法/测试断言均保持不变。

静态核对已确认创建工具没有SetActiveScene、NewScene(Additive)、CloseScene或SaveScene调用；用例数量仍128 EditMode/49 PlayMode。新目录Resources已由用户前次菜单建立，但目标Prefab/.meta仍未检出，所以不能假称资源创建已成功。等待Unity编译更新后再次点击同一菜单，看到Created及新Prefab，再运行完整测试。当前尚无修正后的Unity执行结果，不扩展下一批。

### B1.5 后续验收

用户于2026-10-02 19:59 UTC明确确认全部测试通过。资源现已实际生成：新Prefab GUID `2a0bf8e4b9b44ec46a47f771105de1e4`；其:97脚本GUID `256f0a9016a7098469c8afed875a5d50`与新ScreenTransitionCanvas.cs.meta:2相符，:100的ColorImage指向本Prefab子Image，:74/155分别为当前Unity CanvasScaler/Image脚本GUID。Root scale仍0、sorting10000与无GraphicRaycaster配置保留。只读核对没有手改Prefab/meta或复用旧GUID；原缺陷及人工画面验证没有假称完成。

## B1.6 旧原子动作与驱动

从本地DeprecateActionKit.cs必要区间提取，不从头重做已完成的框架扫描。中断前已经写入本批Runtime与LegacyAtomicActionTests；额度恢复后仅核对这几处落盘与补齐剩余测试/记录，没有重复覆盖旧业务。新增6个Runtime C#、3个Tests C#，修改ActionQueue和driver接入旧FIFO；原4个asmdef/Editor资源生成工具/已通过Screens资源不改。

| 路径（相对Assets/ProjectModules/ActionKit/） | 实现及原证据 |
|---|---|
| Runtime/Compatibility/ActionKitAction.cs | IDeprecateAction、IResetable、INode、ActionData/AcitonData、ActionGroup/OnlyUsedByCode元数据与ActionKitAction全部回调/Execute/Reset/Finish/Break/Dispose/保护钩子；原:546-549,1383-1553 |
| Runtime/Compatibility/LegacyAtomicActions.cs | DelayAction、DelayFrameAction、EventAction、OnlyBeginAction、UntilAction全部原子类/工厂/字段/显式池协议；原:39-305 |
| Runtime/Compatibility/LegacyExecutionExtensions.cs | ExecuteNode<T>/IEnumerator.Execute完整旧驱动，Mono.Delay的IDeprecateAction返回和两个Frame void入口；原:86-94,164-175,1638-1656 |
| Runtime/Compatibility/IPoolable.cs | 原节点暴露的IPoolable/IPoolType两必要接口，迁为本模块全名，不引旧PoolKit |
| Runtime/Compatibility/DeprecateActionKit.cs | 默认RuntimeInitialize启动时Init(50,50)预热Delay/Event；其他旧节点默认池不擅自预热 |
| Runtime/Internal/ActionNodePool.cs | 仅旧Action共享每T的new/LIFO池、默认12/正上限/无界/预热/flag/hook/拒收和内部Clear，不构造整体Factory/Singleton服务 |
| Runtime/Internal/ActionQueue.cs、ActionRecycleDriver.cs（扩展） | 原队列一head每Update，legacy先推进再modern回收；完成只移出，不自动Dispose |
| Tests/EditMode/LegacyAtomicActionTests.cs | 29例：旧基类/五原子节点/原问题、序列化、EOF marker、FIFO及现代/旧队列混合顺序和异常阻断 |
| Tests/EditMode/ActionNodePoolTests.cs | 13例：默认/预热clamp/补差/无界/活跃数与cache上限区别/双回收/null/满池/降限/LIFO/Clear/hook异常 |
| Tests/PlayMode/LegacyAtomicPlayModeTests.cs | 9例：实际帧差/Reset、Mono即时驱动/返回、FIFO/阻塞头/独立host/手动dt、启动设置的50/12缓存上限 |

### 支撑选择与兼容边界

现有XF ClassObjectPoolManager的class,new()、IReuseable钩子、时间清理和不可配置的池与这些旧节点的标志/容量/预热语义不等价；现代ActionPool也无IsRecycled/安全容量分支。本批仅补一个59行动作专属内部池，当前消费者只有5类旧节点及启动预热，不增加项目级独立模块或Shared/Core，不把所有PoolKit功能搬入。

保留原SafeObjectPool实际所需行为：默认缓存数0、上限12；Init(maxCount,initCount)只正上限clamp/trim，重复调用补差；0/负上限无界；Allocate只清IsRecycled而不统一Reset；null/已回收项false；满池仍OnRecycled但不设true；可缓存时flag=true→hook→Push。Until的显式接口通过generic约束访问。new()约束与每类型缓存保留，无反射构造/容器注册。

**不是旧SafeObjectPool provider的对外兼容实现**：原Instance/SetFactoryMethod/SetObjectFactory/MaxCacheCount/CurCount/Clear/Dispose是通用PoolKit公共面；本模块不继续提供该Toolkit身份/Singleton生命周期。不在未发现实际消费者时搬整体provider或制造空兼容API；模块类型所需的IPoolable/IPoolType与节点回收能力全部保留。原provider级消费者必须按真实职责迁移，属于明确的底层API破口，不宣称零差异。

### 行为与仍未解决的原问题

- 旧Execute初次同tick OnBegin→OnBeganCallback→OnExecute，完成后OnEndedCallback→OnEnd；预先Finish/Break直接返回，不触发End。与现代IAction算法分开，不通过现代adapter统一丢掉语义。
- 原Reset清Finished/mOnBeginCalled/mDisposed但保留callback，Dispose先设置disposed并清开始/结束callback，再执行disposed callback/hook。成功回池的4类hook调用Reset会把disposed重新清false；连续第二次Dispose可能污染缓存，分配也不修复它。本批明确characterization，不静默修正。
- OnlyBegin.Dispose不自动回池，Recycle2Cache也不Reset begin/finished/disposed；这些是原行为/缺陷，不把它当“安全复用已实现”。Until保持显式IPoolable及原null predicate异常。
- 旧IEnumerator驱动仍yield WaitForEndOfFrame，常规完成不Dispose；EOF marker已EditMode验证设计，真实EOF在Game View焦点/Editor/Player或batchmode的差异仍待必要人工/构建。新Mono Frame void入口仍没有返回释放handle，测试不假称其有自动Dispose功能。
- 全局旧FIFO每Update最多处理一个头；完成只Dequeue不Dispose；在session reset/driver退出销毁时清掉静态遗留队列、不执行遗留节点或伪称自动释放，原完整模块生命周期在后批处理。modern回收仍用原本批次排空方式。
- 旧头执行在现代Drain之前，原旧谓词/回调抛异常会阻断当次现代回收，不能伪称代际执行隔离。本批增加internal Tick测试入口及混合characterization：等待头不阻断Drain、异常头会阻断且错误向外传播；没有吞异常或改成try/finally执行顺序修复。
- Chain相关Delay/Frame/NextFrame/Event/OnlyBegin/Until方法尚未写入，因为真实Chain/组合类型留下一批。没有建立空IActionChain或抛异常占位；相应功能族只能PARTIAL，范围不能删除。

### 验证状态与停止门槛

本批新增42个EditMode和9个PlayMode，连此前完整回归源码预期 **170 EditMode、58 PlayMode**；当前44 Runtime/1 Editor/17 Test C#、4asmdef。源码数量不是实际发现/通过数。本批没有新资源/GUID创建步骤，不需要重复生成已经生成的ScreenTransition Prefab。

新测试清理遵守队列所有权，环境前置条件失败不会清空外部legacy queue；缓存节点的原双Dispose状态由测试Reset恢复，OnlyBegin测试状态在回收前/后清理，避免characterization污染下一用例。未改Runtime原缺陷来制造通过，无Ignore/不兼容特性/弱化断言。

当前待Unity普通导入/编译后运行两个完整测试程序集，预期170/58，包含全部现代动作与Screens回归；失败/编译错误/发现数差异保留完整文本。无新Package/旧框架/业务改动，不启动第二个同工程Editor，不用辅助构建冒充Unity结果。本批静态审查结果和限定范围核对收尾后停止，不开始下批或AudioKit。

独立只读审查未发现必须修正的新增静态编译/API/正常断言/清理问题；原head异常耦合已作为既有风险记录，不误判为新增算法错误。新增混合Tick/启动上限用例只验证指定顺序和Max设置，不宣称任意时刻池内数量必须50；原factory/provider公共面也没有假称等价。所有测试仍未运行，本结论不能替代Unity结果。

### B1.6 原文件基线

开始时原工程同口径3387文件SHA256为`36368F4782C59615D503C834E5FA3FA6503A5D0C1644DBD9C6D4FF7D08FB0D6C`。仅新增/修改新模块及授权矩阵/03记录，01原审计/02提取计划不改。中断恢复不重做既有扫描，依赖检查只针对本批新改C#；最终内容摘要核对用于保护原文件，不回退用户的外部保存。

收尾同口径仍3387文件，当前摘要为`28C8074A7FA016504F9326A836C28D760425DEE2AA430C91C0A8BF67573E4C48`，与开始不一致。仅定位本批期间mtime变化，未重做功能扫描或读取配置中的敏感内容；以下原文件均未被本批写工具修改，保留当前现状：

| 文件 | 本批期间的LastWriteTimeUtc |
|---|---|
| .kilo/kilo.jsonc | 2026-10-03 02:36:59.1227697 |
| Assets/Games/MainModule/FSM/Enemy/EnemyShield.asset | 2026-10-03 02:39:44.1674249 |
| Assets/Games/MainModule/FSM/Enemy/EnemyState.asset | 2026-10-03 02:39:44.1653834 |
| Assets/Games/MainModule/FSM/GameState/GameStateManager.asset | 2026-10-03 02:39:44.1634024 |
| ProjectSettings/ProjectSettings.asset | 2026-10-02 20:26:51.6973074 |

mtime只定位保存线索，不等于逐字diff或来源证明；没有Git/Plastic CLI基线，不宣称原工程全程未变，也不把这些文件恢复到旧摘要。它们不与本批新旧原子动作源文件冲突，后续使用当前Unity配置和资产进行验证。所有本批手工工程写入限于新ActionKit模块和02矩阵/03记录。

最终源码计数170 EditMode/58 PlayMode、44 Runtime/1 Editor/17 Test C#、4asmdef一致；限定本批新改文件的禁止依赖/不兼容特性/Ignore/空占位检索无命中。增量静态审查复核Tick与3个追加测试，无新增确定错误，但不证明真实UnityUpdate/实际warm缓存数，未假称编译/测试已通过。当前完成本批源码收尾，暂停在Unity验证门槛。

### B1.6 后续用户验收

用户于2026-10-03 02:54 UTC明确确认测试通过、没有报错。前述待验证描述保留为当时记录，此确认是最新结果，不将原缺陷、provider兼容破口或平台边界视为已修复。后批以当前配置/外部资产现状回归，不恢复旧摘要。

## B1.7 旧组合、链式与Timeline

仅定向读取本地旧DeprecateActionKit.cs目标区间，不重扫整个框架。新增6个Runtime和3个测试C#，修改LegacyExecutionExtensions补回3个Chain重载；没有新asmdef或Package，没有新资源生成步骤。已生成Screens资源及全部现代动作继续回归。

| 路径（相对Assets/ProjectModules/ActionKit/） | 实现及原证据 |
|---|---|
| Runtime/Compatibility/LegacyCompositionNodes.cs | RepeatNode、SequenceNode、SpawnNode原字段/构造/保护钩子、递归INode、次数/同tick执行/逆序/Reset/Finish/Dispose；原:552-794 |
| Runtime/Compatibility/LegacyActionChain.cs | IActionChain、IDisposeWhen、IDisposeEventRegister、ActionChain、SequenceNodeChain、RepeatNodeChain，全部Mono工厂/Wait/Event/OnlyBegin/Until及OnFinished显式接口；原:1558-1782 |
| Runtime/Compatibility/LegacyTimeline.cs | Timeline/TimelinePair/Queue/回调/两个Append及KeyEventAction工厂/池hook；原:383-507 |
| Runtime/Compatibility/LegacyDynamicNodes.cs | AsyncNode公开HashSet/Add、QueueNode/Enqueue及原持久执行逻辑；原:509-544 |
| Runtime/Compatibility/OnDestroyDisposeTrigger.cs | 原internal trigger去重/销毁Dispose，内部IDisposableExtensions.Component绑定；原:307-331,361-378 |
| Runtime/Compatibility/OnDisableDisposeTrigger.cs | 原internal停用Dispose trigger，保持一次性集合语义；原:334-359 |
| Runtime/Compatibility/LegacyExecutionExtensions.cs（扩展） | Chain.Delay/DelayFrame/NextFrame，原Mono返回类型与原名不改；原:96-99,178-186 |
| Tests/EditMode/LegacyCompositionTests.cs | 19例：同dt/剩余数/递归/空/childDisposed、旧Repeat各次数、Spawn顺序/计数/强制、Chain所有非frame扩展与回调/条件终止 |
| Tests/EditMode/LegacyTimelineTests.cs | 10例：strict时间/入队序/两Append/Reset/回调/Dispose/key复用、dynamic Queue/Async原异常与缺清理 |
| Tests/PlayMode/LegacyChainPlayModeTests.cs | 9例：Mono工厂/Begin同步与真实pending EOF路径、重复链/Destroy/条件与Frame、trigger去重/原停用失败、跨frame key事件 |

### 底层与依赖

原节点只是BCL队列/集合和Unity协程，不改为XF Timer/FSM或modern Sequence adapter；它们的同dt、Count0、回调/Dispose契约与现代不同。XF已有能力不等价时直接保留有价值的算法。没有新增独立模块/Shared/Core。

SequenceNode/SpawnNode的ListPool仅提供存储复用及释放；本批改为节点自有List，Dispose时Clear并置null，保持保护字段/列表内容可观察行为，不移植整个ListPool provider。链节点本身不是池化对象，列表不是每帧创建，旧Reset仍重置子项与计数。底层列表重新借出/原provider配置身份不声称保持。

KeyEventAction使用本模块现有ActionNodePool，保留原IPoolable和回收仅清引用的hook，不由池统一Reset。其余类型只用标准集合/LINQ/委托和Unity MonoBehaviour。旧Dispose trigger与现代UnRegister trigger职责不同，不把Dispose混成事件注销接口；接口全名属于Project.ActionKit，没有AudioKit或旧框架引用。

### 原行为、缺陷及测试隔离

- Sequence所有连续完成子项在同次执行收到同一个dt，不是modern下项Execute(0)；TotalCount仍为剩余数，空Current仍索引失败；对子Disposed的检查及OnCurrentActionFinished扩展点保留。
- RepeatNode只有-1特殊无限分支，其余比较完成轮次；旧0取决于child是否当次完成，不统一现代0无限。Current递归INode；Dispose拥有child。
- Spawn逆序运行，依赖child OnEnded计数，强制Finish遍历child、Dispose解除计数委托；原空/预Finished缺口保留。
- Timeline严格pair.Time<累计时刻、Queue保留插入顺序而非自动排序；KeyEvent在OnBegin发布，回收不Reset（AK915）。空Timeline/persistent Queue/Async parent不会自动Finished；Async边枚举边移除的原错误及dynamic未清pending/child问题仍单列，不静默修。
- Chain.Begin实际用caller的旧Coroutine驱动；自然完成会Dispose，OnFinished是显式接口的追加，OnDisposed是替换，DisposeWhen可以结束pending。原类型和Executer/IActionChainExtention拼写保留，不擅自加“可无限重启”承诺。
- OnDisableDisposeTrigger第一次将集合置null，再停用/Add可失败（AK919）；用例在inactive GO上通过反射捕获原错误，避免Unity销毁触发额外同类错误，不Ignore或抑制日志。原EOF窗口焦点/Player/batchmode仍需必要人工/构建验证。
- 测试按父/子拥有关系释放，避免直接重复Dispose已在池内的节点；key的回收状态在清理末尾Reset，以防characterization污染共享cache。runtime的原问题并未因此修复。

### 验证门槛

本批新增29个EditMode、9个PlayMode，源码预期 **199 EditMode、67 PlayMode**；50 Runtime/1 Editor/20 Test C#、4asmdef。无NonParallelizable、Ignore、固定null/NotImplementedException或日志空实现。新旧所有已实施能力完整回归，不重复创建新Prefab。

待Unity正常导入/编译后分别选整个`Project.ActionKit.Tests.EditMode`/`Project.ActionKit.Tests.PlayMode`运行；有编译/测试/发现数量问题保留完整文本。静态审查及原范围核对收尾后停止，不叠加下一批，不开始FSM/AudioKit。当前无本批Unity/独立工程/Player/AOT通过结果。

独立静态审查未发现必须修正的新增API/编译/正常断言或当前失败清理问题。审查明确原成功同步Begin与手动跨帧不能证明pending Chain的真实EOF驱动，因此本批追加一个不手动Execute的真实Begin用例：先等待Until gate，再由原Coroutine结束链，以2秒实时边界防止挂住Runner。该用例在Editor依赖Game View可呈现的原EOF行为，运行时保持Game View可见；未改为yieldnull调度器、不Ignore或放宽结果。跨平台/真实引擎结论仍待Unity。

### B1.7 原范围基线

本批开始原工程同口径3387文件SHA256为`F9DD9319DF80A367D576A08CC735B44DDDAF01531F24218C9E626A651E8529BF`，这是当前版本/外部保存后的基线，不能使用更早摘要覆盖。只改新模块与02矩阵/03记录，01审计/02提取计划、原业务/QF/XF及Packages不改。依赖检查只针对新改文件，原文件内容摘要仅用于保护现状，不重做功能扫描。

收尾仍3387文件，当前摘要为`A3EFAE7B0C2EF1E63BBB4466AC3FB109033CD1A715D5E5E9D5252AFDCAE6986F`，与开始不一致。仅按本批期间mtime定向定位到`Assets/Games/MainModule/Views/Enemy/Bullet/EnemyBullet.cs`，LastWriteTimeUtc=2026-10-03 03:16:19.4156927；本批没有写此文件，不读取/覆盖其外部修改，不反推逐字diff或修改来源。没有Git基线，不能宣称整个原工程全程未变。

限定本批新改文件的活跃旧QF/PoolKit/ListPool/业务/Audio依赖、不兼容特性、Ignore或空占位检索均无命中；源码199/67、50 Runtime/1 Editor/20 Test C#与4asmdef计数一致。当前独立静态审查及pending Begin增量复核无必改新增问题，真实EOF/平台窗口与Unity结果依然待验证。源码收尾至此，不增加下一批。

## B1.8

- B1.7已用户确认通过，199/67为当批源码预期数。
- 新Runtime：Compatibility/ActionKitFSM.cs、ActionKitFSMTransitionTable.cs、EnumStateMachine.cs、LegacyFSM.cs、QFSMLite.cs，Subscriptions/ActionStateEvents.cs。所有旧FSM核心算法/API保留。
- 必要替换：Table/封闭TableIndex改FSM领域表与TransitionTypeIndex；旧TypeEventSystem.Global改模块ActionStateEvents.Global，不共享旧global监听或类型身份。不搬TableKit/IOC，不新增独立模块。
- 原AK920-923等风险保留；未修复或假称原缺陷可用。新增23 EditMode/3 PlayMode，源码总222/70，尚待Unity编译和完整回归。
- 独立静态审查无必改新增问题；不改业务、Audio、旧框架、Packages或asmdef。无需生成资源，继续复用已验证Screens Prefab。

## B1.9

- 用户2026-10-03 05:15 UTC批准修复取消/关闭/回收缺陷，相关旧characterization测试改为安全回归；其他算法和曲线不动。
- 新ActionKitLifecycle提供Initialize/Shutdown、InitArchitecture、Interface、OnRegisterPatch与实例Deinit，不实现旧IOC/IArchitecture服务角色；关闭不重置ID或patch。
- 修正Task/Coroutine取消与迟到结果、组合child清理、Custom数据/ID/延后复用、Delay factory残留、同步controller/task回收、duplicateStart拒绝和宿主销毁取消。
- Shutdown关闭新模块的local/global/scene执行器、事件、队列和自有资源；保留用户GO/场景Canvas，仅停止旧驱动在用户Mono上启动的自有Coroutine。回调重入延后至执行栈退出，清理异常可见。
- 新增生命周期测试与安全取消/复用用例，待Unity完整回归。外部Task不被Dispose/Cancel，nested IEnumerator异常仍由Unity报告；后者不是已修好的故障契约。
- 新增关闭gate、回调/Reset重入、controller与旧节点租约核验；取消/清理异常释放兄弟项并保留原错误诊断。不保证外部持有的raw池对象成为永久安全句柄。
- 当前源码预期288 EditMode/87 PlayMode，待Unity验证；无需新资源步骤，不修改业务/Audio/旧框架/Packages或asmdef。
- 07:12 UTC用户报告EditMode销毁执行器后Deinited断言失败，B1.9不能算通过。修正：executor加ExecuteAlways用于Editor生命周期清理，Update只在Application.IsPlaying(gameObject)时推进；保留原销毁断言，新增Editor不自动推进回归。尚未取得修正后Unity结果。
- 07:26 UTC用户报告两项Task子取消的PlayMode失败：父已Deinit但controller仍引用Sequence。修正Step执行后复查取消/租约，标记当帧摘除，不多等一帧或放宽断言；新增直接/条件Sequence同tick回归，原两PlayMode用例保留，待重验。
- 07:43 UTC用户确认此次完整测试全部成功，通过B1.9验证门槛；原预期288/87仅源码计数，独立环境/Player/AOT不据此标通过。本次只维护恢复状态，不开始下一批代码。

## B1.10 单文件导出

- 新Editor：ActionKitSingleFileCreator（保留LicenseCode/FileName/Scripts/DependencyScripts/OutputFilePath）、Inspector及Exporter；菜单Tools > Project ActionKit > Create Single File Export Config由Unity创建新配置/meta，不覆盖既有资产。EditMode仅新增Editor程序集引用，不改Runtime/PlayMode或业务/Audio/旧框架。
- 全58份Runtime源进入预设；46份非组件namespace源码合入ActionKit.cs，AssemblyInfo原文前置；11个组件整文件及meta、Runtime asmdef及meta、ScreenTransition Prefab及meta随目录输出。using留在各自namespace块内，保留源版权/访问级别/partial/条件；不改依赖namespace或伪装为一个.cs即可保留全部组件绑定。
- 导出是其他工程的替代安装，保留现有模块GUID/程序集名/Resources路径。必须导入整个文件夹，不能与多文件模块同装；使用方显式引用Project.ActionKit.Runtime并具备UGUI。不允许输出到源工程、经链接目录写入、缺漏/重复源、组件同名主文件、外来源码/asmdef/asmref；覆盖须确认，冲突meta不覆盖，不删除用户文件。
- 新SingleFileExportTests覆盖完整预设、许可证/设置、namespace与BOM/换行、组件/资源字节保留、预检/覆盖/程序集边界和严格可信路径编译引用。源码预期319/87（EditMode含1项AssemblyBuilder UnityTest），不是实际Unity执行计数。
- 同Unity 2022.3 C#9编译器：58 Runtime、4 Editor、17 EditMode/11 PlayMode源通过；真实CombineSource生成的12份导出C#在仅BCL+引擎+UGUI引用下、含/不含Editor defines均通过。6项纯托管renderer断言、24项严格引用filter检查通过；13对资产/meta字节比对及Canvas GUID对应通过。参数/日志在C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-b110-offline-20261003/final-source及filter-final-source。
- 尚未运行Unity Test Runner、AssetDatabase导出、Inspector或跨项目导入；离线player-defines编译不是Unity Player/AOT构建。原导出写入/MonoScript.GetClass/资源绑定需实际Editor验证；测试中的AssemblyBuilder会正常使用Unity生成目录。本批停在Unity完整编译/319 EditMode+87 PlayMode及一次菜单导出验证门槛，API Help/样例/可选扩展继续后批。
- 最新：用户2026-10-03 09:29 UTC确认B1.10测试通过；非代理执行或逐例XML核验，菜单/跨项目及Player/AOT仍单列。09:34 UTC用户移除Action完整Samples交付，保留功能/扩展实现/测试/必要资源，下一批API Help；本次仅改现有计划/矩阵/恢复记录，不修改源码/资产或Audio范围。

## B1.11 API Help

- 新Editor/Help/ActionKitApiHelpWindow.cs、ActionKitApiHelpContent.cs，归已有Editor-only程序集；菜单Tools > Project ActionKit > API Help，无新依赖/资源步骤。23个中英标题/摘要主题含22条可复制方法；当前Runtime反射索引106公开类型/548声明成员，展示重载、默认参数、泛型约束、嵌套类型、public/protected钩子，不执行getter/示例或写资产，也不进Runtime导出。
- 原01-15章（两个11合并）和新增ScreenTransition说明按当前契约改写；ComplexAPI作为可搜索历史文档载体，不增加空Runtime方法。明确旧/现代动作、模块而非IArchitecture、Task/Coroutine所有权、帧阈值/暂停及未修ScreenTransition/Ease/旧节点/provider边界；可选外部API仍标未实现。完整Samples不恢复。
- 新ApiHelpTests共26用例；仅将现有SingleFileExportTests引用白名单helper改为internal供同程序集测试复用，不改其逻辑/断言。Runtime/asmdef/PlayMode/旧框架/Audio/业务未修改；新meta由Unity导入产生，不手写GUID。
- 实际离线验证：同Unity C#9编译器编译58 Runtime、6 Editor、18 EditMode源通过；23项纯托管断言通过，106类型指南缺项为0，22条真实用法仅依赖Runtime+BCL+Unity/UGUI编译通过。修复指南缺项/内部接口泄漏/属性修饰符后原断言保留，最终GUI段落换行小修另行重编通过。证据在C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-b111-review-20261003/final-verified与final-ui。
- 未启动Unity或执行Test Runner/窗口交互；3项需要native的新增用例未离线调用。当前源码预期345 EditMode（343 NUnit+2 UnityTest）/87 PlayMode；本批门槛为用户Unity编译/两个完整测试程序集通过，并打开窗口验证分类/搜索/指南与API跳转/复制/缩放滚动无错误。离线通过不等于Player/AOT或真实画面验收。

### B1.11 帮助优化

- 用户确认原功能正常，仅要求Help的布局/导航/文档组织与中文化；沿用IMGUI及真实声明formatter，不改Runtime、公开API/功能范围、asmdef、Audio/业务/旧QF。修改Content/Window/ApiHelpTests，新增Help/Guides和Reference；原声明、版权与MIT许可集中到“关于与许可”。
- 新中文入口Tools > Project ActionKit > ActionKit 帮助：使用指南/API参考/关于与许可三页签，分组折叠/左对齐选中导航、可拖动侧栏、独立滚动阅读、只读可选正文、等宽代码块内滚动和复制反馈。修复返回时折叠/滚动恢复、链接选中隐藏、类型+成员搜索命中标记；极小停靠区有保护。全部23指南/5组保留，全中文正文/前提/例注，入门提供完整可挂载ActionKitQuickStart脚本；相关链接仅突出本页必要API。
- 保留106类型/548声明成员及继承接口入口，按成员类别/重载组织；中文用途、381参数说明和147非void返回值说明纳入搜索，泛型名称正常显示。没有调用Runtime getter或执行示例；未确认的未来API有明确提示，不添加行为承诺。
- 额度恢复后从原验证断点继续，先修复Unity2022.3 BeginScrollView缺背景样式的实际CS1503，再复验：8 Editor/18 EditMode C#9编译通过；37纯托管检查通过，3native项未离线调用；完整入门、21短例各自及整体编译通过，不引用QF/XF/业务/Editor。593个Runtime/游戏/asmdef保护文件与原哈希基线一致，无原meta修改。日志C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-help-verify-20261003/final-current，保留初轮失败证据。
- 未运行Unity/Test Runner/实际GUI，无法提供真实截图；静态审查不替代深浅主题、常规/窄窗口、拖动/滚动/搜索/返回/复制的人工验收。源码预期359 EditMode（357 NUnit+2 UnityTest）/87 PlayMode。完成Help优化后停止，不扩展其他模块。
- 用户13:54 UTC要求移除通用Kit别名：Guides所有调用改为ActionKit，示例生成容器改用独立ActionKitHelpExamples命名空间避免Project父命名空间的同名遮蔽；完整脚本与21短例均不含别名。新增拒通用别名回归，38纯托管检查/8 Editor/18 EditMode/所有示例实际离线编译通过，593保护文件未变；静态预期360/87。日志在既有验证目录的no-kit-alias子目录；Unity实际验收状态不扩大。
- 14:11 UTC用户实际EditMode失败：EveryDisplayedUsageMethodCompilesAgainstTheCurrentRuntime与ExportCompilesWithOnlyEngineAndUiReferencesAndNoOriginalRuntimeAssembly缺MonoBehaviour/SerializeField/SceneManagement。读取Unity保留的失败rsp确认无引擎引用；官方2022.3源码确认默认ReferencesOptions.None使用单体模式，旧filter漏Data/Managed/UnityEngine.dll且不接受安装Player目录。两测试在获取defaultReferences前显式UseEngineModules；filter接受可信安装模块/PlaybackEngines与精确单体引擎路径，继续拒项目/测试程序集/伪路径，构建前断言CoreModule保留。
- 保留原完整编译断言，新增6路径case+1实际AssemblyBuilder引用集回归；不改EditorAssembly flags、Runtime、业务、asmdef、Packages或Library。离线8 Editor/18 EditMode编译、11正反路径检查、38纯托管检查、所有用法编译及593保护hash通过；另用安装中67个Player模块编译入门/帮助和原导出源码均通过。日志在既有验证目录engine-module-references及其player-module-check子目录。当前预期367/87，Unity原两测试和新原生引用集测试尚未由代理执行，停在用户重验门槛。

## B1.12 Observable 可选扩展

- B1.11于用户14:51 UTC确认完整测试/Help界面通过。B1.12只实现Integrations/Observable，不创建完整样例/场景，不安装第三方或推进Audio/业务/旧QF；DOTween仍无提供者且未实现。原嵌套UniRx/DOTween实现仅定向提取并逐字节核对，来源与版权记录在C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-b112-original-20261003-efdbcfa7/provenance.json。
- Project.ActionKit.Observable只引用核心Runtime/BCL/Unity，不依赖UniRx SDK；保留原public UniRxAction<T>私有无参构造/Allocate及ActionKitUniRxExtension.ToAction/UniRx提供者身份。用户明确批准首个OnNext成功、空OnCompleted取消、OnError报错取消；订阅句柄在完成/Reset/Deinit正确释放，不Dispose source本体。旧通知slot隔离，后台通知由捕获context或拥有线程OnExecute递交；动作生命周期仍要求Unity主线程。
- 修复新集成暴露的三项内部问题：IAction.Execute回调前后复核ID/Deinited及可选重置代次，阻止旧启动栈覆盖新动作；Sequence/Parallel保存子项分配ID，Reset使用快照并逐项/前后复核，取消/清理不操作复租子项。核心public/protected声明对照B1.11保持一致。原有文件仅IAction.cs、Sequence.cs、Parallel.cs和AssemblyInfo.cs作运行时必要修改，复用现有池/队列，无新大型支撑模块。
- 两个专属测试程序集共58 EditMode/25 PlayMode；包括同步订阅返回前取消/Reset、Dispose异常、旧slot/旧ID、组合/Reset重入追加及复租、worker/context、host/Shutdown。核心测试源码预期371 EditMode/87 PlayMode，四程序集合计429/112；计数不是实际Unity通过数。
- Help无硬性Observable引用，仅读精确已加载程序集。核心独立106类型/548成员，加载扩展108类型/560成员；24篇指南保留原23篇，缺提供者时明确标不可用并过滤编译片段。核心导出仍只导出基础模块，Observable需另带其Integrations目录/消费者显式引用，不宣称单文件包含插件；不用通用Kit别名。
- 实际验证：58核心+1扩展同Unity C#9已编译并核对源码/DLL hash；8 Editor、18/11核心测试源码、两个扩展测试源码本轮编译通过。两独立进程Help各42纯托管检查通过，另6 Observable probe通过；原连续Reset/枚举强复现修正后通过。入门/每个可用短例/集合共47示例DLL实际编译通过，无QF/XF/业务/Editor依赖。证据C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-unified-observable-20261004/final-digest.json；目录日期只是验证产物标识。
- 593保护基线仅上述4个核心文件有授权差异，其余589一致；原meta无修改/删除，新增GUID由Unity导入产生。不沿用先前“5处差异”推断：该旧计数为Compare-Object条目，最新逐路径结果没有第5个原有文件差异。
- 尚未启动Unity或执行Test Runner/PlayMode、真实SynchronizationContext/回池时序、独立工程/Player/AOT；83个扩展测试仅编译不能算执行通过。稳定批次在此停止，等待已打开Unity重新编译并运行四个完整测试程序集及查看Observable帮助，不继续下一批。
- 用户17:59 UTC贴出的Observable故障日志已逐项核对：均对应测试主动构造的OnError，并在触发前有LogAssert.Expect；两个组合场景各3次分别是Sequence/Parallel/Repeat参数化。清理异常用例同时Assert.Throws并保留原故障日志。Console红日志本身不证明测试失败，未收到Test Runner断言失败文本，不据此关闭Runtime诊断或宣称B1.12验收通过。
- 本次仅清除两个新扩展测试中误沿用的通用Kit别名，按用户命名要求直接使用ActionKit；同版本EditMode/PlayMode测试程序集重编通过，断言/日志/Runtime均未修改，58/25及总429/112计数不变。增量编译参数在既有统一验证目录expected-log-check中。
- 用户18:06 UTC实际失败UntypedCustomDefersReuseAndRefreshesIDWhileItsCallbackIsExecuting原661行：回调先Deinit再Finish，同分配ID仍应返回true、成功钩子计数保持0。B1.12把取消和失效租约混为Current导致false。修IAction.Execute分开SameLease/Current/FinishedLease，回调内取消后Finished返回同租约终止结果，不调用OnFinish；入口已取消仍false，ID/重置代次变化仍false，调度器/组合继续检查取消，不推进成功路径。原断言保留，未放宽期望。
- 新增9个纯IAction返回值/取消/新ID/重置/finish-hook回归和1个帮助语义断言。真实新旧DLL对照9用例旧6过/3败、新9全过；另9边界probe与6 Observable probe通过，公开/保护签名一致。58 Runtime、8 Editor、18 Core EditMode、1 Observable及两个扩展测试文件本轮同版本重编通过，末轮文案/测试增量另编通过。日志C:/Users/Kano/AppData/Local/Temp/kilo/execute-review-efd05756-20261004及doc-final。未运行Unity或原池回收用例，当前核心381/87、扩展58/25（合计439/112）仅源码计数，停在用户重验门槛。
- 最新验收：用户18:40 UTC确认测试全部通过，覆盖上述B1.12/返回值回归待重验状态；证据来源为用户，不是代理执行。DOTween范围/真实提供者及最终独立验收仍未完成，本轮未开始下一批实现。

## B1.13 DOTween 可选扩展

- 用户提供Assets/Plugins/Demigiant/DOTween，DLL真实IL确认Version=1.2.825；使用原供应商不安装/升级/修改。保留原DOTweenAction私有无参构造、Allocate及ActionKitDOTweenExtension.ToAction/DOTween公开签名。用户选择修复旧双factory调用、把Pause当成功及漏Kill暂停Tween的缺陷；程序集Project.ActionKit.DOTween只显式引用Core及真实DOTween.dll，测试独立，核心无强提供者依赖。
- 正常完成由追加onComplete/onKill观察记录并辅以active/IsComplete判据，不靠“不播放”判成功。只追加/移除本租约hooks，不覆盖用户回调或改变SetUpdate/autoKill/recyclable/全局配置。ActionPause转发，Resume只恢复由Action暂停的Tween；IgnoreTimeScale不替代DOTween时钟配置。正常完成autoKill=false仅解除hooks，Tween后续生命周期由持有者管理。
- 修两项真实IL确认P1：用户onKill内Deinit/Reset不再立即重复Kill造成Despawn数组越界；ToAction在Pause前绑定marker消除预执行回收复租空窗。取消逻辑即时关闭，物理Kill通过真实Unity主线程context延期、执行前复核同marker/无其他owner，pending marker阻止再次绑定。首次执行前Reset保留原预绑定及更新内部代次，启动后的Reset结束绑定，Repeat使用factory。缺原context或非主线程在绑定/启动前拒操作；其余生命周期仍要求调用方主线程，不宣称所有worker调用自动拒绝。
- 明确限制：只包装可独立控制的根Tween/根DG.Sequence；回调覆盖/前置用户回调异常可能丢观察，不能绝对区分复租或保证物理释放。onKill失标时保守取消且不误Kill新Tween。Post在停止/退出/domain reload时未必执行，不能承诺Shutdown同步销毁Tween，不支持人为嵌套pump。DOTween独立Artistic许可/标准版readme要求与wrapper MIT在Help关于许可区分别保留。
- 中文Help保留全部原条目并增DOTween指南，共25个；仅读精确已加载提供者。CoreOnly106/548、单扩展108/560、Both110/572类型/成员；四模式各45纯托管断言通过，每个可用短例/完整入门/集合共96示例DLL实际编译成功，无QF/XF/业务/Editor或不需要的provider依赖，不使用通用Kit别名。导出仍仅核心，集成与其真实提供者需单独安装引用。
- 实际离线：本轮58 Runtime、8 Editor、18/11核心测试、Observable及其两测试、DOTween及其两测试，共10程序集101源码全部同Unity C#9编译通过；DOTween另Player条件编译/元数据无UnityEditor引用。公开/保护契约对B1.12一致，当前wrapper源码hash3a0ce5cd10b38664822773cf7e6edbf68f6b67755440eb5aff91543044700ac2与验证前后一致。证据C:/Users/Kano/AppData/Local/Temp/kilo/actionkit-b113-unified-20261004/final-digest.json，目录日期不是工程验收日期。
- 供应商45文件前后hash一致；Runtime对B1.12只AssemblyInfo新增3友元，无行为再改。593旧保护基线仅历史授权4文件差异，未知变化/删除0，既有Core/Observable asmdef/meta不变；DOTween新增meta由Unity正常导入，不手写GUID。
- 待Unity：核心383 Edit/87 Play、Observable58/25、DOTween56/21（合计497/133）仅源码和DLL声明核对，不代表生命周期用例运行通过。DOTween Edit56中24项UnityTest，其余32项NUnit；Play21项真实UnityTest。未启动Unity/调用native/执行Test Runner或Player构建。此稳定批次停止，等待已打开Unity编译、六个完整测试程序集和DOTween帮助验收，不继续Audio/业务/旧QF处理或最终平台结项。
- 用户21:55 UTC实际全fixture SetUp失败：全局TotalActiveTweens=1不符合原0前提，case尚未执行。改Edit/Play驱动为自有Dictionary条目快照，逐项在调用前验证active+allocation独立target，再调用Tween.ManualUpdate；移除全局0要求及强制global timeScale=1，不简单删保护后继续全局推进。其余ActionKit全局关闭/队列环境检查保留，非自有Tween不推进/暂停/Kill。
- 安装1.2.825 IL/XML核对单实例ManualUpdate只推进传入根Tween、paused无变化，使用传入dt和Tween自身timeScale/independent选择，不读global时钟；fixture传固定dt以保持原“测试时钟=1”的预期而不改他人配置。单实例autoKill会即刻Despawn而非全局批末销毁，保留相关行为强断言，不承诺两驱动整批排序相同。
- 新增Edit外来manualTween与global倍率不受影响、callback新增Tween下一批才推进，以及Play外来manualTween隔离三回归；消除两测试文件残留Kit通用别名。原DOTween行为断言未弱化，现Edit58/Play22及整体499/134。当前同版本两测试程序集编译通过，参数位于既有统一验证目录fixture-isolation；真实Unity/3新增用例未运行，待用户重验，不操作来源未知的现存1 Tween或供应商/Runtime。
- 2026-10-04续修：用户多项实际Kill/no-callback/no-pool失败，安装DLL真实IL确认public DOTween.Init在非PlayMode新domain返回null，ApplyTo仍可创建active Tween，而Kill入口initialized=false直接return；单TweenManualUpdate没污染isUpdateLoop，测试call token也确实绑定DG.Tweening方法。不能靠active/可推进认定SDK已初始化，不改vendor/反射initialized或弱断言绕过。
- 原DOTweenActionTests.cs及meta移入PlayMode，GUID fd5fb71a6ed9b754dbb18373d3469c97保持；SetUp要求Application.isPlaying=true，创建Tween前Assert真实DOTween.Init非null。原纯public/private构造签名检查原样移入EditMode/DOTweenApiContractTests.cs，无SDKInit或场景Setup。两生命周期fixture保留自有快照/target-token驱动、真实ctx等待及强Kill/复租断言；移动fixture只释放Setup已确认不存在而本fixture创建的RecycleDriver，yield等待销毁，避免污染下一fixture。
- 同Unity C#9编译迁移后DOTween Edit1源码、Play2源码通过（日志在既有统一验证目录playmode-migration）。生命周期57例+原Play22例合并79，原Edit58仅拆出1纯接口检查，无测试删除；当前总442 Edit/191 Play=633。未运行Unity Test Runner/真实Play生命周期，原失败仍待重验，Runtime/供应商/业务/asmdef未改。
- 最新用户03:35 UTC确认全部测试通过，覆盖上述B1.13待重跑状态；最终独立/导出/Player/AOT不据此标完成。用户03:45 UTC要求消除Help泛用token措辞，已改Reference所有用户说明为注销句柄（IUnRegister）/控制器引用，Guides相应说明和示例局部变量改unregister，保留真实C#签名/参数名。只动Help两文件/ApiHelpTests新增1回归/现有记录，Runtime/供应商/业务/asmdef不改。
- 此术语增量同版本Editor和核心Edit程序集编译通过；四加载模式各46纯托管断言通过，真实生成的订阅/状态事件两短例均独立编译通过。原API覆盖/25指南保持，Help源无泛用token/凭据残留；记录在既有统一验证目录terminology-check。核心源码384/87、Observable58/25、DOTween1/79（合计443/191），未实际运行Unity新文案用例或窗口；本轮结束，不推进下一模块。
