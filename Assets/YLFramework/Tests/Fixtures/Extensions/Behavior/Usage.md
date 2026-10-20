# Behavior 组件与主 Unity 验收入口

本组为独立 YL 能力，原游戏继续使用 XF/XFFSM/Overlap，不重绑原 Scene/Prefab。
T3-A已在A独立Worktree执行真实Unity与Mono代表验收，结果见末节；人工GUI、IL2CPP Low及无旧包独立接收门仍未完成。夹具辅助编译、离线NUnit与真实引擎结果分别报告。

## Overlap

- Namespace：`YLFramework.Overlap`。保留 Point/Circle/Box/Capsule/Area 2D、Box/Sphere 3D、`Results`、`OverlapHit<T>`、enter/exit、`Running`、检测频率和 scaled/unscaled 模式。
- 新建自有对象，添加对应形状，设置 `LayerMask`、形状参数和事件。查询继续排除 Trigger；`Results` 是本组件维护的可读快照，不应由调用方在回调内改写。`OverlapHit<T>.point` 保留原 `Vector2` 契约，包括 3D 查询。
- 手动 `CheckOverlap()` 可独立使用；同组件事件内递归查询被拒绝，其他组件可查询。禁用不补发 exit，立即清空结果，重新启用后的首次查询重新 enter，`Running` 的显式设置保留。
- 容量保持 100，不偷偷扩容或改变时序。Editor/Development 达到容量时每次启用周期最多提示一次“可能截断”，不声称一定丢失命中；Release 无此日志路径。
- Inspector 保留 Results、区域列表/Scene 节点、Box/Capsule 手柄，绘制移到 Editor，不把 Handles 留在 Runtime。
- 主 Unity：执行 `YLFramework.Tests.Extensions.Behavior.OverlapComponentTests`；对每个形状检查触发/实体、多 Collider、负缩放/旋转、enter/exit、容量100/101、回调中双实例/同实例查询及禁用/销毁。
- 另验 scaled/unscaled 在 `timeScale=0/0.5/1` 与检测频率 0.01/10/100 的时序；3D/2D 层矩阵刻意设为不同后 Reset，确认各用各矩阵并恢复设置。检查正常帧 GC，不以无分配源码推断 Profiler 结果。
- 创建自有 Prefab，绑定 enter/exit 持久回调，保存/重新导入/关闭重开，确认字段、事件与新 GUID 正确；旧8个 Prefab/hash保持不变。

## CharacterController2D

- Namespace：`YLFramework.Character.Controller2D`，组件 `CharacterController2D`，保留原三种检测器、跳跃/长按中断、多段跳、自动旋转、五个事件与三种泛型包装及Inspector。
- 在同一 Rigidbody2D 下添加非 Trigger 的 Box/Circle/CapsuleCollider2D，允许子物体的多个 attached Collider。先配置再启用；运行时改变 Collider 结构后调用 `RefreshColliders()`，失败给出对象与所需字段诊断，不伪造有效碰撞体。
- 在调用方 `FixedUpdate` 使用 `Move(Vector2 delta)` 或 `MoveHorizontal(speed)`，控制 `Jump`/`JumpStart`/`JumpEnd`，不引入输入包或第二 Update 驱动。原接触检查仍在 Update，运动仍在 FixedUpdate；不冒称改变了物理步长。
- 实例分别拥有 10个 hit/contact 缓冲；原查询容量与算法保留。停用归还接管前的重力并清输入/跳跃/接触缓存，重新启用重新取得 Collider。值先提交再通知，事件中读取当前值一致，不人为发送 false/true 抖动。
- 主 Unity 执行 `CharacterMotor2DTests`；另测左右墙/顶部、斜坡阈值、不同 fixedDeltaTime、重力正/零/负、多段跳/长按中断、autoRotation、层变更和停用回调重入。两对象不同 Collider 类型同时运行，确认不串数据。
- Inspector 缺Collider/未命名Layer负例、Scene法线标签等人工作者流程仍须实测。事件持久绑定保存重导入、代表接触和泛型UnityEvent已在本次自动门/Mono消费者执行；完整接触矩阵和IL2CPP不能据此外推。
- Collider被删除/禁用/改成Trigger时停止驱动并归还重力、清理旧输入及接触；显式刷新失败也会释放所有权，恢复合法Collider后可重新刷新。检测和移动回调用版本阻断旧调用栈，包括墙面事件中禁用/重新启用。Box/Capsule从本地size与绝对缩放取尺寸，角度单独传入，不重复旋转世界AABB；物理body.rotation先于Transform更新时补相对角差，保留子Collider局部角度。0/30/90度近远障碍及子Collider回归已真实通过。

## MoveablePlatform

- Namespace：`YLFramework.Platform2D`。保留 `Moveable`、`MoveablePlatform2D`、`PlatformCatcher2D`，不引用CC2D。路径仍为世界坐标 Positions 或 Transform 快照，按总距离线性插值；PingPong/Loop/Once、delay、scaled/unscaled FixedUpdate、Run/Pause/Stop及UnityEvent保留。
- 先在自有对象配置路径，再启用组件或调用 `Run()`。可用 `PositionType`/`UpdateMode` 属性明确模式；运行时添加/移除节点使用原方法重新初始化。空路径或失效Transform/NaN按节点索引诊断，不运行；单点/全重合路径在delay后定位并停止，不除零或永久空转。
- Transform位置在初始化时冻结，运行中删除源Transform不访问已销毁对象；下次Run/节点编辑会验证引用。移动时间使用同一次快照总长，避免原位置快照与实时Distance混用。初始化由重复总长扫描O(n²)收敛为O(n)，不宣称实机Profiler收益。
- Pause只暂停/恢复已启动路径，不把Stop后的对象隐式启动；回调中Stop/Pause/改路径/禁用会阻止旧目标继续写入。Catcher保留原接触变化/计时查询驱动，实例缓冲，停用清空；`RefreshContacts()`供显式刷新。
- 载人按本次Contacts快照，每个Rigidbody2D仅搬运一次，排除平台自身和Trigger，不因多个Area/Collider重复位移。仍使用原刚体position位移方式，不重写为新motor。
- 主Unity执行`MovingPlatformTests`；补验Loop端点、PingPong折返、Once一次Stop、delay/pause/resume、timeScale、不同fixedDeltaTime、多个刚体、CC2D联合载人和接触变化30Hz/10Hz流程。
- Inspector保留路径列表和Scene手柄，增加非法路径定位；Catcher手柄通过SerializedProperty写入以保留Undo/Prefab。真实FixedUpdate三种路径/delay/pause/stop/载人与Mono消费者已执行；手工列表/Scene手柄全流程、所有时间组合及IL2CPP仍待验收。

## 双 FSM

- 原轻量 `YLFramework.FSM.FSM<TStateId>` 的6个C#及原meta均未修改。继续原 `Allocate → Init/State → SetUp → Update/FixedUpdate/LateUpdate/OnGUI → Pause/Continue → RecycleToCache`；不要在其生命周期回调内回收。
- 资产FSM为 `YLFramework.StateMachine`，独立使用原XFFSM算法与API。`FSMController`按Start初始化、Update/Late/Fixed驱动；`RuntimeFSMController`保存层级/子状态机、状态脚本、参数和条件组；组内AND/组间OR、AnyState、Trigger及完整回调保留。Entry沿用作者工具的默认入口展示，不新增原包没有的条件边执行语义；空条件链只有显式`AutoSwtich=true`才自动切换。
- 状态脚本所在asmdef引用`YLFramework.Runtime`。类继承此namespace的`FSMState`，保持public无参构造，使用原`controller`、`currentStateInfo`、`userData`字段；不要把旧包`XFFSM.FSMState`子类直接绑定给YL资产。原游戏、Scene、Prefab、旧包className/GUID完全不重绑。
- 在自有GameObject添加`FSMController`，序列化配置层，或在Start前调用`AddRuntimeFSMController(source, layerName)`；新`UserData`属性支持直接挂载时的启动前数据注入。沿用`GetCurrentStateInfo(index/layerName, subStateName)`、Set/Get Bool/Float/Int/Trigger、ResetTrigger。相同参数名跨层保留原广播/任意非零读取语义，但读取按参数类型匹配。
- 每个运行层克隆自己的配置，状态/过渡均引用克隆数据。`Close`退出并清活动/排队Trigger、参数与切换记录但保留克隆以便重启；移除层/销毁时幂等`Dispose`销毁自己拥有的克隆，不销毁源资产或他人实例。退出回调异常不吞掉，仍先尝试其余脚本/子状态/层清理，再重抛原异常。
- 首次创建按脚本分别记录，回调重入不会消费尚未访问脚本的OnCreate；未创建脚本不发送退出。首次回调异常摘除失败运行层、释放克隆，恢复初始化标志并完成其余层后传播原异常；参数不永久排队。Start前或初始化期间可取消尚未启动的源层。
- 同帧同状态访问上限32、整链256；超过后每帧最多一次资产/层/状态诊断，拒绝本帧后续切换，不依赖scaled Time、不抛循环检测异常。回调修改层列表用快照，常态驱动/预热后广播不按帧新建快照。未作Unity Profiler结论。
- 作者入口：`Assets/Create/YLFramework/Asset FSM/FSMController`、`Assets/Create/YLFramework/Asset FSM/FSMState`、`Window/YLFramework/Asset FSM/StateMachineWindow`。图保留节点/层级/连线/参数/脚本选择/预览；拖拽、框选和网格用Unity公共GraphView组件。旧内部`UnityEditor.Graphs`已移除，重点样式查找有公共Editor样式fallback，模板附`[Preserve]`。
- 保存/绑定只操作当前资产，不在Editor加载时扫描和重写所有配置；缺类型/参数/父节点或循环配置给资产/状态/字段诊断，暂时失效的脚本绑定不自动删除。严重损坏的重复/null节点可在Unity Debug Inspector按字段修复。脚本只按此FSMState基类筛选，不吸入旧包状态类型。
- New Scripts用独立文件存在检查及CreateNew拒绝覆盖已存在源码；Create And Add在刷新前保存项目作用域SessionState待绑定记录，等待编译/导入及程序集重载完成再绑定，不假设下一次delayCall已有GetClass。该流程已在A通过真实产品入口、编译重载及保存重导入验证，不由测试手工附加。条件阈值/比较/参数/AutoSwitch/组增删登记Undo，主组和附加组重排使用SerializedProperty；Undo后重新绑定Inspector。缺参数等错误保留合法端点连线，允许在图内修复，不隐藏整张图。
- 许可：XFFSM 1.5.4/MIT/2021弦小风全文随模块附带；`FSMSelectionDragger.cs`与Unity Reference-Only源码近似，未复制；网格/框选也未复制，分别调用公共`SelectionDragger`/`GridBackground`/`RectangleSelector`。来源与许可链接在模块`ThirdPartyNotices.txt`，未复制Logo或原GUID。
- `AssetFSMRuntimeTests`已执行双实例/资产隔离、嵌套默认状态、组条件、Entry/Any、Trigger消费/重启清队列、回调增删层、退出异常清理、零timeScale循环和双FSM退出。`BehaviorAuthoringTests`已执行原生资产/条件组、Prefab持久事件保存重导入及Undo/Redo；默认RuntimeOnly不在EditMode Invoke，测试显式验证并序列化EditorAndRuntime，不改产品默认。
- Play退出/再进入已在同一Editor进程按默认双Reload、禁Domain、同时禁Domain/Scene三种组合各连续两轮，验证双FSM独立起步/转换及退出无克隆。静态StartupFSM/RemoveFSM所有组合、源资产卸载和完整场景链仍不据此外推。轻量FSM的真实Unity时间/三相位由引擎用例及探针驱动，离线不做Time stub。

## 主 Unity / Player

1. 集成本组源码、两份专属验收asmdef及A正式生成的meta，保留新GUID，不再从原包复制或重新分配。重新核对实际编译输入、无原包GUID、重导入和保存重开；共享asmdef、Packages、ProjectSettings本轮diff仍应为空。独立接收工程的首次导入不能由A共存环境替代。
2. Test Runner按`YLFramework.Tests.Extensions.Behavior`过滤运行EditMode/PlayMode。GUI另验节点创建/拖拽/框选/缩放/面包屑/子图/连线/参数/条件组重排/脚本选择/Undo/Redo/保存重开、亮暗主题。A自动结果见末节；主工作区/接收工程结果另记，不把辅助编译或自动GUI作用域当成人工图形验收。
3. `Tools/YLFramework/Behavior Acceptance/Generate Owned Fixture`在自有Fixtures下生成全新资产/场景及真实GUID，不覆盖原场景、不保存其他脏场景。打开生成场景运行`BehaviorPlayerProbe`，它实际消费2D/3D Overlap、CC2D、载人、多Collider、泛型事件、反射状态和双FSM。
4. 在另获配置授权的工作区或独立接收工程选择Windows64及Mono或IL2CPP，再分别点`Build Release Mono Windows64`和`Build Release IL2CPP Windows64`，IL2CPP门明确选择Low，最终门用`BuildOptions.None`。另有Development菜单仅供诊断，不能替代Release门。工具核查当前设置但不自动切后端/裁剪；菜单输出新`Temp/Behavior.Acceptance/<backend>/<Release|Development>/<unique>`目录。
5. 分别运行生成exe，参数`-batchmode -nographics -logFile <自有日志路径>`，要求退出0及`YL_BEHAVIOR_ACCEPTANCE result=PASS`，最终门须`development=False`；记录Unity版本、后端、sourceGuid、状态生命周期计数、构建报告和日志SHA。消费者会核对实际Debug.isDebugBuild与生成场景期望一致。IL2CPP需实测状态类裁剪/无参构造/泛型UnityEvent；自有真实状态另按模板`[Preserve]`或自己的link.xml保留。

## 离线命令

从本 worktree 根运行 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence <唯一名称>`。
`Pure`只执行真实托管逻辑/静态契约的NUnit，不模拟Unity；`Legacy`用实际Runtime DLL执行轻量FSM只读回归，检测到缺Unity native icall即失败；`Compile`使用已安装Unity SDK的真实引用辅助编译Runtime、Editor profile、EditorTools、本组测试及独立Player消费者/生成器，不启动Unity。`All`连续执行全部离线门。

同一Evidence目录分别保留`pure-results.xml`和`legacy-results.xml`，不得把共享Runner最后写出的`results.xml`视作全部结果。源码契约、托管执行、辅助编译和主Unity/Player结果分别计数。

运行 `Tools/Behavior/Test-BehaviorBoundary.ps1 -Evidence <唯一名称>` 产生 `Temp/Behavior.Offline/<名称>/pending-meta.txt` 与逐文件 SHA/来源/保护区报告。来源和 SDK 引用留在临时证据，不提交二进制/缓存。所有新增脚本、目录和文本资产的 meta 由主 Unity 生成。

## T3-A 独立验收

环境固定为A工作区`fusion-xf-ext-behavior-901d14f1ae336a53`、分支`fusion/xf-ext-behavior`、Unity`2022.3.62f3c1_1623fc0bbb97`，源码候选基线`4b2803288f1c175cb4cb69f7291dc0f115d43649`。全部原始证据在`C:/Users/Kano/AppData/Local/Temp/kilo/BA26/`；唯一执行交接为`.kilo/handovers/xf-ext-behavior.md`。

| 门 | 实际结果与证据目录 |
|---|---|
| 导入/编译 | `import-02`退出0；`audit-final-01`六程序集归属正确、compileFailed=false；实际安装UTF1.3.9。Runtime484/Editor140/EditMode161/PlayMode45/Acceptance4/Acceptance.Editor5为真实编译输入数，不是本组测试通过数 |
| meta/GUID | `audit-final-01`原冻结146项全部存在，实际151=146+5新增入口；缺失/孤立/本组GUID冲突均0，全raw Assets1876 GUID；与89个上游GUID复用数0，AssetDatabase映射一致、轻量FSM15项SHA不变、自有Prefab Missing Script=0 |
| EditMode | `edit-01`58/60，修正夹具持久事件调用状态及Undo事务隔离；`edit-02`60/60，0失败/跳过/不确定 |
| PlayMode | `play-01`30/33暴露物理body.rotation先于Transform的Box/Capsule查询缺陷，保留诊断并修复；`play-02`35/35；新增七形状/容量/真实FixedUpdate覆盖后`play-coverage-02`50/50，0失败/跳过/不确定 |
| Reload | `reload-01`暴露空untitled场景Additive限制，定点修正生成器；`reload-02`同一进程三组合各连续两轮，6/6通过，全部退出无克隆，原选项及ProjectSettings字节恢复 |
| Create And Add | `binding-01`真实产品入口创建源码、编译/重载后自动绑定，保存重导入、GUID/类型和配置验证通过；未手工附加 |
| Mono构建/运行 | `build-mono-02`Windows64/Mono2x/非Development/stripping Disabled，BuildReport Succeeded，0 errors/1226 warnings；同次Player PID42996退出0且唯一PASS，creates/enters/exits均2。回收器company路径规范化误判已通过同次`-VerifyExisting`纠正，不重跑或覆盖证据 |
| IL2CPP Low | `build-il2cpp-preflight-01`仅得到BlockedBeforeBuild，当前为Mono2x/Disabled；未切配置、未构建、未运行，不计通过。setter恢复默认值可能新增Standalone键，不能保证只读ProjectSettings字节不变 |
| 临时资产 | `fixture-cleanup-01`仅归档并删除本组4个GeneratedAcceptance目录、20文件，逐文件SHA校验通过；原绑定/场景/GUID留在外部归档，不提交临时资产 |

本轮工具只用于此固定A工作区，不是通用主工程启动器。`Invoke-BehaviorUnity.ps1`每次启动前核对版本、绝对projectPath、分支、其他重型BatchMode及本工程PID/锁，独占run目录且不覆盖证据；`-Gate EditMode/PlayMode`执行UTF，`-Gate Execute -Method <精确入口>`执行审计，Reload/Binding入口需`-AsyncExecute`，Player构建需`-Win64BuildTarget`。`Test-BehaviorUnityMetas.ps1 -RunDirectory <同次审计目录>`补Unity Mono超长路径限制之外的raw Assets核对。`Invoke-BehaviorPlayer.ps1 -RunDirectory <成功构建目录>`实际运行Player，`-VerifyExisting`仅核验已保留的同次exe/log/PID结果。

提交前只移除151份正式meta的453处行尾空白，未改GUID/Importer/值；原始审计SHA保持原样，提交版本SHA另见`audit-final-01/meta-submission-audit.json`，不能将两种字节身份混用。

所有成功运行保护区SHA变化0。Mono实际Player的Runtime与独立消费者无UnityEditor/旧XF AssemblyRef，但整个A工程仍有旧包；不等同G4无旧包接收。Player以`-batchmode -nographics`运行，人工GUI/图形呈现、IL2CPP/AOT/Low、独立Exporter/接收工程、主工作区与原游戏组合验收均未完成，不宣布整个XF阶段完成。
