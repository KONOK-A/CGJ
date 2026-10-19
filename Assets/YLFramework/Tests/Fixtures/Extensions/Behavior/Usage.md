# Behavior 组件与主 Unity 验收入口

本组为独立 YL 能力，原游戏继续使用 XF/XFFSM/Overlap，不重绑原 Scene/Prefab。
以下 Unity 门尚未执行，不能把夹具编译或离线 NUnit 结果当作引擎验收。

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
- Inspector 缺Collider/未命名Layer负例、正常移动、Scene法线标签，事件绑定保存重开须实测。Mono/IL2CPP消费者必须真实触发地面/墙/顶部及泛型 UnityEvent；尚未执行。
- Collider被删除/禁用/改成Trigger时停止驱动并归还重力、清理旧输入及接触；显式刷新失败也会释放所有权，恢复合法Collider后可重新刷新。检测和移动回调用版本阻断旧调用栈，包括墙面事件中禁用/重新启用。Box/Capsule从本地size与绝对缩放取尺寸，角度单独传入，不重复旋转世界AABB；0/30/90度近远障碍夹具已准备。

## MoveablePlatform

- Namespace：`YLFramework.Platform2D`。保留 `Moveable`、`MoveablePlatform2D`、`PlatformCatcher2D`，不引用CC2D。路径仍为世界坐标 Positions 或 Transform 快照，按总距离线性插值；PingPong/Loop/Once、delay、scaled/unscaled FixedUpdate、Run/Pause/Stop及UnityEvent保留。
- 先在自有对象配置路径，再启用组件或调用 `Run()`。可用 `PositionType`/`UpdateMode` 属性明确模式；运行时添加/移除节点使用原方法重新初始化。空路径或失效Transform/NaN按节点索引诊断，不运行；单点/全重合路径在delay后定位并停止，不除零或永久空转。
- Transform位置在初始化时冻结，运行中删除源Transform不访问已销毁对象；下次Run/节点编辑会验证引用。移动时间使用同一次快照总长，避免原位置快照与实时Distance混用。初始化由重复总长扫描O(n²)收敛为O(n)，不宣称实机Profiler收益。
- Pause只暂停/恢复已启动路径，不把Stop后的对象隐式启动；回调中Stop/Pause/改路径/禁用会阻止旧目标继续写入。Catcher保留原接触变化/计时查询驱动，实例缓冲，停用清空；`RefreshContacts()`供显式刷新。
- 载人按本次Contacts快照，每个Rigidbody2D仅搬运一次，排除平台自身和Trigger，不因多个Area/Collider重复位移。仍使用原刚体position位移方式，不重写为新motor。
- 主Unity执行`MovingPlatformTests`；补验Loop端点、PingPong折返、Once一次Stop、delay/pause/resume、timeScale、不同fixedDeltaTime、多个刚体、CC2D联合载人和接触变化30Hz/10Hz流程。
- Inspector保留路径列表和Scene手柄，增加非法路径定位；Catcher手柄通过SerializedProperty写入以保留Undo/Prefab。列表重排、删除节点后Scene选择不越界、Undo/Redo、事件及路径保存重开、Mono/IL2CPP真实载人均待主Unity验收。

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
- New Scripts用独立文件存在检查及CreateNew拒绝覆盖已存在源码；Create And Add在刷新前保存项目作用域SessionState待绑定记录，等待编译/导入及程序集重载完成再绑定，不假设下一次delayCall已有GetClass。该跨重载流程仍须主Unity验收。条件阈值/比较/参数/AutoSwitch/组增删登记Undo，主组和附加组重排使用SerializedProperty；Undo后重新绑定Inspector。缺参数等错误保留合法端点连线，允许在图内修复，不隐藏整张图。
- 许可：XFFSM 1.5.4/MIT/2021弦小风全文随模块附带；`FSMSelectionDragger.cs`与Unity Reference-Only源码近似，未复制；网格/框选也未复制，分别调用公共`SelectionDragger`/`GridBackground`/`RectangleSelector`。来源与许可链接在模块`ThirdPartyNotices.txt`，未复制Logo或原GUID。
- 主Unity执行`AssetFSMRuntimeTests`：双实例/资产隔离、嵌套默认状态、组条件、Entry/Any、Trigger消费/重启清队列、回调增删层、退出异常清理、零timeScale循环和双FSM退出。`BehaviorAuthoringTests`检查原生资产/条件组、Prefab持久事件保存重载及Undo/Redo。以上仅准备并辅助编译，尚未运行。
- Play退出/再进入在Domain Reload开/关两组分别实测，另补Scene Reload关闭组合、静态StartupFSM/RemoveFSM同名重启、退出期间禁止创建宿主、源资产卸载及克隆总数。轻量FSM的Time.deltaTime测试保留在Unity门，离线不做Time stub。

## 主 Unity / Player

1. 先集成本组源码及两份专属验收asmdef，由主Unity生成`pending-meta.txt`中的新身份，核对无原包GUID、重导入、保存并关闭重开。共享asmdef、Packages、ProjectSettings本轮diff应仍为空。
2. Test Runner按`YLFramework.Tests.Extensions.Behavior`过滤运行EditMode/PlayMode。GUI另验节点创建/拖拽/框选/缩放/面包屑/子图/连线/参数/条件组重排/脚本选择/Undo/Redo/保存重开、亮暗主题。运行结果及meta清单由集成owner回填，不把本轮辅助编译当结果。
3. `Tools/YLFramework/Behavior Acceptance/Generate Owned Fixture`在自有Fixtures下生成全新资产/场景及真实GUID，不覆盖原场景、不保存其他脏场景。打开生成场景运行`BehaviorPlayerProbe`，它实际消费2D/3D Overlap、CC2D、载人、多Collider、泛型事件、反射状态和双FSM。
4. 在主Unity工作区显式选择Windows64及Mono或IL2CPP，再分别点`Build Release Mono Windows64`和`Build Release IL2CPP Windows64`，最终门用`BuildOptions.None`。另有Development菜单仅供诊断，不能替代Release门。工具核查当前设置但不修改ProjectSettings，只输出新`Temp/Behavior.Acceptance/<backend>/<Release|Development>/<unique>`目录。
5. 分别运行生成exe，参数`-batchmode -nographics -logFile <自有日志路径>`，要求退出0及`YL_BEHAVIOR_ACCEPTANCE result=PASS`，最终门须`development=False`；记录Unity版本、后端、sourceGuid、状态生命周期计数、构建报告和日志SHA。消费者会核对实际Debug.isDebugBuild与生成场景期望一致。IL2CPP需实测状态类裁剪/无参构造/泛型UnityEvent；自有真实状态另按模板`[Preserve]`或自己的link.xml保留。

## 离线命令

从本 worktree 根运行 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence <唯一名称>`。
`Pure`只执行真实托管逻辑/静态契约的NUnit，不模拟Unity；`Legacy`用实际Runtime DLL执行轻量FSM只读回归，检测到缺Unity native icall即失败；`Compile`使用已安装Unity SDK的真实引用辅助编译Runtime、Editor profile、EditorTools、本组测试及独立Player消费者/生成器，不启动Unity。`All`连续执行全部离线门。

同一Evidence目录分别保留`pure-results.xml`和`legacy-results.xml`，不得把共享Runner最后写出的`results.xml`视作全部结果。源码契约、托管执行、辅助编译和主Unity/Player结果分别计数。

运行 `Tools/Behavior/Test-BehaviorBoundary.ps1 -Evidence <唯一名称>` 产生 `Temp/Behavior.Offline/<名称>/pending-meta.txt` 与逐文件 SHA/来源/保护区报告。来源和 SDK 引用留在临时证据，不提交二进制/缓存。所有新增脚本、目录和文本资产的 meta 由主 Unity 生成。
