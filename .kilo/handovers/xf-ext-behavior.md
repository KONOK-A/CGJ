# A 组连续实施检查点

唯一正式 Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。本文件仅记录 A 组执行事实，不修改共享 Plan。

## 授权与基线

- 用户 2026-10-19 本轮授权连续完成 Overlap、CC2D、MoveablePlatform、XFFSM，含离线验证、本地中文候选提交及一次组内定向 Review。
- 实核工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-behavior-901d14f1ae336a53`；分支 `fusion/xf-ext-behavior`；开工 HEAD/B0 `c88500aef6651f5b13ac933dd1f209ecb314e92d`，祖先检查退出 0，开工工作区干净。
- Unity `2022.3.62f3c1`。不调用 Unity MCP，不启动 Editor/batchmode。不修改共享区域、原包、主工程或其他 worktree；不 Push/合并/Rebase/Reset/Clean/Stash。
- Ruling：本次授权覆盖 Plan 过时的未授权/首批停止状态；D1 采用双 FSM 独立共存、局部替换风险 Editor 实现，D6 将运动组件作为可选组件。唯一执行记录为本文件，不另建过程 Plan/ledger。
- 原轻量 `Runtime/Utilities/FSM` 完整只读；共享产品/测试 asmdef、E0 工具、Core/Foundation/Resource、Packages/ProjectSettings 只读。仅新增本组验收夹具的两份专属asmdef，不改共享引用。

## 当前进度

- 当前模块：四模块实现、唯一一次定向Review及修正后的离线门均已收口。最终候选以包含本检查点当前版本的`feat(fsm): 融合资产状态机并收口本组定向审查`提交为准；该HEAD且工作区干净即实施结束，不重复实现/提交。
- Overlap本地候选：`51e61da`，未Push。
- CC2D本地候选：`a072bdd`，未Push。
- Moveable本地候选：`0067246`，未Push。
- 下一动作：本轮在最终候选提交及Git范围复核后停止；后续仅由主Unity集成阶段执行meta/GUI/资产/Mono及IL2CPP门，不重复Review、不Push/合并。最终提交Hash由Git日志及交付回复给出，不另做文档提交。

## Overlap

- 来源：主工程只读 `Library/PackageCache/com.xfkj.xfgameframework.overlap@4961d83581`，1.0.4，revision `4961d8358119485c94d6f0f74b6ca5d8b4163460`，MIT/2024 弦风课堂。逐文件 SHA 已记录在 `Tools/Behavior/Overlap.source.json`。
- 静态根因：静态结果缓冲跨实例污染、回调内查询重入、3D Reset 使用 2D 矩阵、销毁 Collider 比较与禁用结果残留、Area 无效节点提前返回不发退出。
- 已保留：7种查询形状、Results/enter/exit、100容量、检测频率/时间模式、区域/Box/Capsule作者操作；Editor绘制与Runtime隔离。
- 已修复：实例缓冲+同实例重入守卫、禁用时中断并清快照、fake-null过滤、Area空路径退出、3D层矩阵；小增强为每次启用周期一次“可能截断”提示。
- 实际红例：`overlap-red` 0/4通过；承接后 `overlap-import-red` 2通过/5失败，覆盖静态缓冲及缺重入/生命周期守卫。
- 实际绿例：`pwsh -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence overlap-final`，7/7（3托管逻辑+4静态契约），0失败/跳过。Runtime/Editor profile/EditorTools辅助编译退出0，分别41/40/10警告；含既有告警和上游过时Handles API告警，不计Unity通过。本组EditMode/PlayMode夹具辅助编译成功，未执行。
- 来源/保护区：`Test-BehaviorBoundary.ps1 -Evidence overlap-final` 核对19个C#、1个上游身份、保护区diff空，26个meta待主Unity生成；完整路径见`Temp/Behavior.Offline/overlap-final/pending-meta.txt`。完整MIT在产品局部`ThirdPartyNotices.txt`，共享根LICENSE/Notices留给集成owner串行汇总。
- 收口记录：候选`51e61da`，完成后已连续推进CC2D。

## CharacterController2D

- 来源：1.1.6，revision `f0241054744ca0673ec370d5fad39c32b789d81c`，逐文件身份在`Tools/Behavior/CharacterController2D.source.json`；MIT/2024弦风课堂全文保留于模块内。
- 保留原Rigidbody2D/Collider2D、ground/wall/top检测、多段跳/长按中断、重力/旋转、五个事件及三种泛型事件包装与原Inspector。无新输入包/全局角色系统，无CC2D以外共享代码改动。
- 局部修复：hit/contact实例隔离；停用归还重力并清输入/接触/跳跃缓存，重新启用刷新attached Collider；状态先提交再通知，检测结果不再为了刷新强发false/true；按配置groundAngle判断下方；Circle负缩放半径；更新信息节流时间实际写回。
- 小增强：`RefreshColliders()`及缺Collider/未命名Layer定位诊断；允许运行时配置修正后显式恢复。Scene标签/法线和Inspector计数保留，Handles仅在Editor。
- 实际红例`cc2d-red`：新增9项全部因缺能力失败，Overlap7项仍绿。`cc2d-green1`辅助编译抓到本次补丁局部变量赋值错位，已定点修正，不计成功。
- 最终`cc2d-closure`：16/16（11托管逻辑+5静态契约），0失败/跳过；Runtime/Editor profile/EditorTools辅助编译退出0，41/40/9警告。本组EditMode/PlayMode夹具编译通过未执行，PlayMode测试引用另有1条NUnit3.5/3.14兼容警告，不计为引擎运行通过。
- `cc2d-final`静态身份/保护区检查：31个C#、2个上游身份、保护区diff空、42个meta待生成。后续最终审计将覆盖调试计数恢复后的最终SHA。
- 收口记录：候选`a072bdd`，完成后已连续推进Moveable。

## MoveablePlatform

- 来源：1.0.6，revision `1e1265d8e6adb4ebab6febe8bc51288972e2b234`，身份`Tools/Behavior/MoveablePlatform.source.json`，完整MIT/2024弦风课堂随模块保留。
- 保留：世界坐标/Transform路径快照、距离加权插值、PingPong/Loop/Once、delay/pause/stop、FixedUpdate、载人、UnityEvent和原路径/区域手柄。无CC2D编译依赖，不建更新引擎。
- 修复：空/单点/零距离路径、NaN/无效Transform节点、同刚体多Collider/Area重复搬运、静态缓冲、fake-null接触、停用残留；Catcher手柄改SerializedProperty以保留Undo/Prefab；回调Stop/Pause/改路径/禁用不再提交旧目标；Scene选择失效不越界。
- Ruling：单点/全重合路径按delay后定位并停止；Stop后Pause(false)不重新启动；运行时Transform沿用原初始化快照，删除源引用在下次初始化给索引诊断。避免原位置快照与实时距离混用导致时间漂移，改变只限原不完整边界，正常三种移动模式不变。
- 小改进：公开PositionType/UpdateMode、ValidatePath及RefreshContacts便于自有调用方；初始化总距离从每节点扫描收敛为线性两次遍历，O(n²)→O(n)，未作Profiler性能主张。
- 实际红例`moveable-red`：新增9项失败、旧16项通过。`moveable-green1`：累计25/25（19托管逻辑+6静态契约）通过，0失败/跳过；Runtime/Editor profile/EditorTools辅助编译退出0，41/40/11警告；准备的EditMode/PlayMode测试源码编译通过未执行，保留NUnit引用兼容警告。
- 静态审计：42个C#、3个上游身份逐SHA核对、保护区diff空、56个meta待主Unity生成，路径见本次证据目录。
- 收口记录：候选`0067246`，完成后已连续推进资产FSM。

## XFFSM / 双 FSM

- 来源：XFFSM1.5.4，revision `b06333c2cdc801fcc401f14c252853bf099aa1b7`，原包逐文件身份在`Tools/Behavior/StateMachine.source.json`。MIT/2021弦小风全文、来源及局部替换说明随模块分发。
- 保留资产/层/子状态机、参数、Trigger、AND/OR条件组、Entry/Any、状态全生命周期和必要图形编辑。新namespace `YLFramework.StateMachine`，不依赖、不合并、不改写`YLFramework.FSM`轻量内核。复用既有Foundation AssemblyTool/ApplicationTool，未引入新依赖或公共执行系统。
- Ruling：`FSMSelectionDragger.cs`与UnityCsReference2022.3代码近逐行匹配，Unity Reference-Only条款不允许按MIT承接其修改实现，故未复制；连同网格/框选局部改用公共SelectionDragger/GridBackground/RectangleSelector。删除UnityEditor.Graphs依赖、局部替换样式/AA绘制/模板/Logo；不删除整个Editor或重写整个FSM。代价是拖拽细节采用Unity公共组件，须主Unity GUI验收。
- Runtime局部修复：所有过渡使用本层克隆数据；Close保留克隆但清活动及排队Trigger/参数/记录，Remove/OnDestroy幂等Dispose归还克隆；回调增删层用快照，预热后广播复用快照；状态先摘除再退出防重入；退出异常仍尝试其他脚本/子状态/层清理后重抛。原State字段/API沿用，新增启动前UserData属性供直接挂载调用方使用。
- 配置守卫：资产/层/状态/参数/父链/默认状态/重复字段/脚本类型及无参构造在克隆前定位。循环保护按frameCount，同状态32/整链256，本帧达到上限诊断并拒绝后续切换，不靠scaled Time，不抛循环检测异常。静态Startup注册清理及退出创建门不改共享Core。
- Editor边界：UnityEditor实现在Editor程序集，Runtime仅保留无Editor类型的UNITY_EDITOR桥接；取消加载时全库重写，只修当前资产绑定；缺脚本保留数据并诊断。节点/参数/过渡/绑定增加Undo，保存限当前资产，保留图的层级/条件/脚本/预览流程；模板带Preserve，不复制原GUID。
- 实际红例：`asset-fsm-red`新增9失败/旧25绿；`asset-fsm-guards-red`新增6失败，覆盖层级配置、缺参数、默认/循环父链、Trigger清队列和按帧恢复守卫。首轮原生夹具辅助编译捕获调用方错误使用上游不存在的API，已改用真实controller/currentStateInfo/userData/GetCurrentStateInfo契约，未增兼容stub。
- Review前历史`asset-fsm-final`：40/40（32托管逻辑+8静态契约）通过；实际Runtime DLL上的轻量FSM只读回归5/5通过，共45通过、0失败/跳过。轻量FSM6份C#及9份meta共15项SHA与B0一致；其真实Update方法JIT会解析Time.deltaTime，相关用例完整保留到Unity PlayMode门，不造Time stub、不将含native缺失日志的一次早期运行计为纯逻辑通过。
- Runtime/Editor profile/EditorTools辅助编译退出0，41/40/14警告；两组测试源码、独立Player消费者及自有场景/资产生成器辅助编译通过。仍有NUnit3.5/3.14兼容警告及上游Handles过时/未用字段警告，不冒充Unity编译或引擎运行。
- `Test-BehaviorBoundary.ps1 -Evidence asset-fsm-final`：106个C#、4个上游身份逐SHA核对、保护区及范围外diff空、未复制原GUID、143个meta待主Unity生成。精确清单`Temp/Behavior.Offline/asset-fsm-final/pending-meta.txt`；产物/SDK引用及SHA见同目录`source-boundary.json`与`reference-inputs.json`。
- 所有新增资产当前只准备生成入口，未生成/保存/导入。主Unity、Mono、IL2CPP菜单、用例与回执标准见`Assets/YLFramework/Tests/Fixtures/Extensions/Behavior/Usage.md`，不修改ProjectSettings自动切后端。

## 唯一定向 Review 与最终门

- 完成一次独立只读Review（`ses_eaa3c64dffferxarfvPBakYWde`），范围B0到本组完整工作树；0个Critical、11个Important、2个原标Minor。未二次分派Review或全库扩大。两项验证问题涉及正式交付证据/Plan的非Development门，按验收有效性升级为必要修正，不作为装饰性优化延期。
- R1：脚本路径占用与类型扫描分离，CreateNew原子拒绝覆盖；真实BCL文件不变测试红转绿，另准备实际Creator负例。Create And Add在刷新前记录项目作用域SessionState，编译/导入/程序集重载后精确绑定当前资产/状态，不全库扫描；跨重载时序仍须主Unity验证。
- R2：逐脚本创建标志替代整节点首次标志，未创建脚本不退出；Tracker红转绿，保留并补齐A→B→A重入的真实Unity用例。
- R3/R4：初始化finally恢复标志，失败启动实例摘除/Dispose，完成其余层后保留原异常传播；取消层同时处理待启动源定义和运行实例。补首次OnCreate异常、正常层参数不永久排队、初始化中/Start前移除B层的用例，已辅助编译未运行。
- R5/R6/R7：Collider失效归还重力并清旧状态，可恢复刷新；生命周期版本阻断墙面/速度/跳跃回调后的旧栈；Box/Capsule用本地size×绝对缩放与独立角度。尺寸3项红转绿；缺Collider/双墙禁用及0/30/90度真实形状近远查询夹具已辅助编译未运行。
- R8/R9/R10：移除不配对Disabled Scope；条件值/比较/参数/AutoSwitch/组命令登记Undo，主/附组重排用SerializedProperty，Undo后重新绑定Inspector；无效参数不隐藏合法端点的线，空参数查找返回缺参数诊断。3项源码契约（含Release入口）红转绿；GUI窗口作用域、完整条件Undo/Redo、缺参数连线及保存重载用例已辅助编译未运行。
- R11：核实Entry只是默认入口展示，修正夹具而未扩展内核运行语义；真实循环用例显式AutoSwtich=true。修正用例仅编译，未声称已经命中Unity循环日志。
- R12/R13：All保留独立pure-results.xml与legacy-results.xml，不把最后的results.xml当总数；最终Mono/IL2CPP菜单使用BuildOptions.None，Development仅调试，Player报告/核对实际构建flavor。不修改ProjectSettings、不创建或运行Player。
- 最终命令：`pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence behavior-final`。结果：48/48（37托管逻辑+11源码契约）与实际Runtime DLL轻量回归5/5，共53通过、0失败/跳过/不确定；两个XML根节点已实际读取确认48与5，保留完整逐例结果。
- Runtime、Editor profile、EditorTools辅助编译全部退出0，警告分别41/40/14；本组EditMode/PlayMode夹具、独立Player消费者与生成器均辅助编译通过。EditMode与PlayMode各有NUnit3.5/3.14绑定警告。仅源码/编译验证，不计Unity编译或原生执行通过。
- 最终`Test-BehaviorBoundary.ps1 -Evidence behavior-final`：109个C#、4原包身份/SHA核实、轻量15项身份不变，保护区及范围外diff空、无原包GUID拷贝，146个meta待主Unity生成。精确路径`Temp/Behavior.Offline/behavior-final/pending-meta.txt`，产物/引用SHA见同目录JSON；所有Temp证据/二进制/缓存不提交。
- 被动ReflectionOnly元数据读取确认Runtime与独立Player消费者均无UnityEditor/XFFSM/XFGameFramework程序集引用；四原包package.json版本与主工程packages-lock.json完整revision已只读逐项核实。公共GraphView替换亦已查对应2022.3官方Scripting API。
- 当前无未处理的已确认Review发现或源码/离线范围实质阻塞。物理排序/容量、GUI、资产真实保存重开、新meta、Domain/Scene Reload退出重入、独立接收工程、Mono/IL2CPP非Development及裁剪仍未运行，按Usage入口交主Unity集成阶段，不计Passed。

## 验证边界

- 本轮Passed仅为上述真实离线逻辑/静态契约。Unity/EditMode/PlayMode/GUI/资产保存/Mono Player/IL2CPP 均未运行。
- 新脚本/目录/文本资产 `.meta` 由主 Unity 集成阶段生成，不复制原 GUID、不手工伪造。精确待生成清单将由专属静态工具输出。
