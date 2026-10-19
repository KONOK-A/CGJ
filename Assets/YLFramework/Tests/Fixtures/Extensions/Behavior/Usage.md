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

- Namespace：`YLFramework.Character.Controller2D`，组件 `CharacterController2D`，保留原三种检测器、跳跃/长按中断、多段跳、自动旋转、五种 UnityEvent 及 Inspector。
- 在同一 Rigidbody2D 下添加非 Trigger 的 Box/Circle/CapsuleCollider2D，允许子物体的多个 attached Collider。先配置再启用；运行时改变 Collider 结构后调用 `RefreshColliders()`，失败给出对象与所需字段诊断，不伪造有效碰撞体。
- 在调用方 `FixedUpdate` 使用 `Move(Vector2 delta)` 或 `MoveHorizontal(speed)`，控制 `Jump`/`JumpStart`/`JumpEnd`，不引入输入包或第二 Update 驱动。原接触检查仍在 Update，运动仍在 FixedUpdate；不冒称改变了物理步长。
- 实例分别拥有 10个 hit/contact 缓冲；原查询容量与算法保留。停用归还接管前的重力并清输入/跳跃/接触缓存，重新启用重新取得 Collider。值先提交再通知，事件中读取当前值一致，不人为发送 false/true 抖动。
- 主 Unity 执行 `CharacterMotor2DTests`；另测左右墙/顶部、斜坡阈值、不同 fixedDeltaTime、重力正/零/负、多段跳/长按中断、autoRotation、层变更和停用回调重入。两对象不同 Collider 类型同时运行，确认不串数据。
- Inspector 缺Collider/未命名Layer负例、正常移动、Scene法线标签，事件绑定保存重开须实测。Mono/IL2CPP消费者必须真实触发地面/墙/顶部及泛型 UnityEvent；尚未执行。

## MoveablePlatform

- Namespace：`YLFramework.Platform2D`。保留 `Moveable`、`MoveablePlatform2D`、`PlatformCatcher2D`，不引用CC2D。路径仍为世界坐标 Positions 或 Transform 快照，按总距离线性插值；PingPong/Loop/Once、delay、scaled/unscaled FixedUpdate、Run/Pause/Stop及UnityEvent保留。
- 先在自有对象配置路径，再启用组件或调用 `Run()`。可用 `PositionType`/`UpdateMode` 属性明确模式；运行时添加/移除节点使用原方法重新初始化。空路径或失效Transform/NaN按节点索引诊断，不运行；单点/全重合路径在delay后定位并停止，不除零或永久空转。
- Transform位置在初始化时冻结，运行中删除源Transform不访问已销毁对象；下次Run/节点编辑会验证引用。移动时间使用同一次快照总长，避免原位置快照与实时Distance混用。初始化由重复总长扫描O(n²)收敛为O(n)，不宣称实机Profiler收益。
- Pause只暂停/恢复已启动路径，不把Stop后的对象隐式启动；回调中Stop/Pause/改路径/禁用会阻止旧目标继续写入。Catcher保留原接触变化/计时查询驱动，实例缓冲，停用清空；`RefreshContacts()`供显式刷新。
- 载人按本次Contacts快照，每个Rigidbody2D仅搬运一次，排除平台自身和Trigger，不因多个Area/Collider重复位移。仍使用原刚体position位移方式，不重写为新motor。
- 主Unity执行`MovingPlatformTests`；补验Loop端点、PingPong折返、Once一次Stop、delay/pause/resume、timeScale、不同fixedDeltaTime、多个刚体、CC2D联合载人和接触变化30Hz/10Hz流程。
- Inspector保留路径列表和Scene手柄，增加非法路径定位；Catcher手柄通过SerializedProperty写入以保留Undo/Prefab。列表重排、删除节点后Scene选择不越界、Undo/Redo、事件及路径保存重开、Mono/IL2CPP真实载人均待主Unity验收。

## 离线命令

从本 worktree 根运行 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence <唯一名称>`。
`Pure` 只执行真实托管逻辑/静态契约的 NUnit，不模拟 Unity；`Compile` 使用已安装 Unity SDK 的真实引用辅助编译 Runtime、Editor profile、EditorTools 及本组测试源码，不启动 Unity。

运行 `Tools/Behavior/Test-BehaviorBoundary.ps1 -Evidence <唯一名称>` 产生 `Temp/Behavior.Offline/<名称>/pending-meta.txt` 与逐文件 SHA/来源/保护区报告。来源和 SDK 引用留在临时证据，不提交二进制/缓存。所有新增脚本、目录和文本资产的 meta 由主 Unity 生成。
