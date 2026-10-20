# A 组连续实施检查点

唯一正式 Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。本文件仅记录 A 组执行事实，不修改共享 Plan。

## 授权与基线

- 用户 2026-10-19 本轮授权连续完成 Overlap、CC2D、MoveablePlatform、XFFSM，含离线验证、本地中文候选提交及一次组内定向 Review。
- 实核工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-behavior-901d14f1ae336a53`；分支 `fusion/xf-ext-behavior`；开工 HEAD/B0 `c88500aef6651f5b13ac933dd1f209ecb314e92d`，祖先检查退出 0，开工工作区干净。
- 源码阶段历史边界：Unity `2022.3.62f3c1`，当时不调用 Unity MCP、不启动 Editor/batchmode；后续 T3-A 已明确允许 A 独立 BatchMode，见下节。不修改共享区域、原包、主工程或其他 worktree；不 Push/合并/Rebase/Reset/Clean/Stash。
- Ruling：本次授权覆盖 Plan 过时的未授权/首批停止状态；D1 采用双 FSM 独立共存、局部替换风险 Editor 实现，D6 将运动组件作为可选组件。唯一执行记录为本文件，不另建过程 Plan/ledger。
- 原轻量 `Runtime/Utilities/FSM` 完整只读；共享产品/测试 asmdef、E0 工具、Core/Foundation/Resource、Packages/ProjectSettings 只读。仅新增本组验收夹具的两份专属asmdef，不改共享引用。

## 当前进度

- 当前模块：2026-10-19T20:59:52Z用户正式授权T3-A独立Worktree BatchMode真实验收及本组定向修复，覆盖此前本文件禁止启动Unity的旧阶段限制；仍不操作主Editor/MCP、其他Worktree，不改共享源码/asmdef/Packages/ProjectSettings，不Push/合并或改写历史。
- 源码候选HEAD实核`4b2803288f1c175cb4cb69f7291dc0f115d43649`，四候选均存在，本轮开工工作区干净。既有离线验收记录保持历史身份，不替代本轮Unity结果。
- 首启动前快照：安装实核`C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`，ProductVersion `2022.3.62f3c1_1623fc0bbb97`。当时A无Editor/工程锁、无Library/PackageCache，UTF1.3.9仅为manifest声明；实际安装已在后续UPM恢复及最终审计核实。
- 错峰：初见B组重型IL2CPP PID4688，未启动A；安排3分钟调度。21:03:10Z检查B父进程已退出，仅主GUI PID32536及主导入Worker仍在；取消错峰调度，不干预这些进程。进程证据仅保留脱敏PID/工程路径/类型。
- 本轮证据根：`C:/Users/Kano/AppData/Local/Temp/kilo/BA26/`，每运行独占子目录。专属`Tools/Behavior/Invoke-BehaviorUnity.ps1`启动前检查版本/分支/其他重型任务/本工程锁，记录自身Unity PID及受保护输入SHA，等待同一次运行退出，不自动杀进程或还原文件。
- `import-01`：21:06:47Z启动A PID18120（wrapper35096），86包解析/注册完成，日志停于GfxDevice创建；会话级monitor返回stopping后已丢失跟踪，两个已记录PID均消失，退出码未知，无正常退出标记，不计导入/编译通过。保留job/editor.log；`-RecoverOnly`生成同一次recovery.json，受保护SHA变化0，残留锁经独占FileShare.None打开证明无持有者，未删除锁。后续使用persistent tracked进程与独立run目录，不重写import-01。
- A自己的UPM恢复UTF1.3.9（`Library/PackageCache/com.unity.test-framework@1.3.9/package.json`，仓库revision `5c8a1448cee4d7607be0d60dceb66703b0cfe016`），不是主工程缓存复制。21:09见B短批次PID28996，当时继续错峰；向B现有会话发送非干预式空档协调请求，不操作其文件或进程。
- B于21:12:07Z结束最后审计并回传本轮不再启动Unity/Player；A重新预检后启动persistent `import-02`（Unity44256）。本次21:14:53Z正常退出0、正常batch shutdown标记存在，Asset Pipeline首导入11323项，真实编译完成，保护区SHA变化0。A的Library/ScriptAssemblies实际包含YL Runtime/Editor/两组测试/两份验收程序集，未复制其他工程Library。
- 首导入后实际Unity生成本组meta，数量/GUID由后续专属Audit确认。另观察`.vscode/settings.json`产生非本组变更，未接管/暂存/还原，最终单列工作区状态。编译日志含既有许可通道回退和原包无脚本程序集等警告，不把零退出码误写成零警告或GUI已通过。
- 首导入后新增本组`BehaviorUnityBatch.AuditImport`，记录CompilationPipeline实际输入、包注册、meta/GUID、轻量FSM指纹及自有Prefab Missing Script。各门逐运行记录，不沿用离线53项计数。
- Gate1收口：`audit-01`新增CI入口PackageInfo名称冲突编译失败，已限定类型；`audit-02`编译通过后因既有超长路径在Mono文件读取失败。拆分Unity实际归属/AssetDatabase审计与PowerShell7全Assets原始meta检查后，`audit-03` PID6984退出0，6程序集归属无误、compileFailed=false、MissingScript=0、轻量指纹不变。原146预期全部存在，实际147（额外为本轮BehaviorUnityBatch.cs.meta），全Assets原始GUID无与本组冲突、无孤立。原始预期已从import-01外部副本冻结，未用结果重造；Unity清理过工程Temp，不再依赖其历史临时证据。
- Gate2：`edit-01` PID2256实际60项，58通过/2失败/0跳过。定位为EditMode持久事件默认RuntimeOnly不Invoke、同帧创建阶段未flush的Undo记录混入重命名事务；明确测试用EditorAndRuntime并验证保存状态，flush/隔离Undo事务，未改生产默认事件状态或削弱断言。
- `edit-02` PID27668实际60/60通过，0失败/跳过/不确定，正常退出0，保护区变化0。原生资产/条件组保存重导入、Prefab持久事件、条件Undo/Redo、重命名Undo/Redo及自动GUI作用域检查实际执行；不代表人工鼠标拖拽/视觉呈现验收。下一门PlayMode。
- Gate3首轮`play-01` PID25348实际33项，30通过/3失败，均为旋转Box/Capsule查询。`play-rotation-diag`保留同断言得到真实证据：body.rotation30/90已更新、物理bounds已旋转而Transform.eulerAngles仍为0；并非通过放宽几何断言解决。
- 定点修复CC2D：原非分配Cast保留，本地size×绝对缩放保留；查询角度加attached Rigidbody物理角度相对其Transform的差量，支持子Collider局部旋转。新增2项子Collider回归，原6旋转参数断言保留。
- `play-02` PID39432实际35/35通过，0失败/跳过/不确定、退出0、保护区变化0；基础真实命中/回调、CC2D碰撞/重力、载人与去重、资产FSM层级/条件/Trigger/异常释放以及轻量并行退出已执行。容量/七形状/移动模式与连续Reload矩阵继续补代表覆盖，未提前声明全部门完成。
- 补覆盖委派仅修改本组Overlap/平台两个测试文件，子代理未运行Unity、改配置/meta或Git；主代理独占引擎。`play-coverage-01`被其他Worktree重型PID44612预检拦截，未启动A，不计执行。`play-coverage-02` PID31104实际**50/50通过**，0失败/跳过/不确定，退出0，保护区变化0。覆盖七种形状真实2D/3D命中、Trigger/Layer/范围过滤、负缩放/旋转/offset、重复Area、100/101容量与提示限频/停用清理；平台三种路径模式、单次Stop、fixed-clock delay、Pause/Stop/Run及真实FixedUpdate载人。原反射手动Tick改为真实FixedUpdate，不弱化原断言。
- Reload：`reload-01`因单个空untitled场景不能Additive创建失败；生成器仅对`sceneCount=1/path为空/rootCount=0`改用Single，其他仍Additive，finally恢复。`reload-02` PID41844在**同一Editor进程**执行默认双Reload、禁Domain Reload、同时禁Domain/Scene Reload三种组合，每种连续两轮，**6/6通过**。每轮双FSM独立起步/转换、Trigger Set/Reset、源参数不变、实际克隆及退出无残留均验证；全部`afterExitNoClone=true`。原enabled=false/options=3由API恢复，退出0、ProjectSettings及其他保护区字节变化0，不手工清static制造通过。
- Create And Add：`binding-01` PID25184通过产品`FSMStateCreator.CreateFSMState`及其pending-binding队列，真实创建源码、编译/导入/程序集重载后自动绑定，未由测试手工附加。验证GetClass、GUID/类型一致、保存重导入仍绑定及配置合法；`complete=true/passed=true/error为空`，退出0、保护区变化0。类型`T3ABoundStateac79d03a6e1c4f4c9d905e85f432836b`，资产GUID`836c88f48e2016947882341f66e313a5`，脚本GUID`ba8772970d871984c865da4025bb32f3`。
- Mono构建：`build-mono-01`因新CI结果字段与Unity2022.3 BuildSummary的int类型不符出现CS0266，已定点修正；`build-mono-02` PID24044实际Windows64/Mono2x/非Development/stripping Disabled构建Succeeded，**0 errors、1226 warnings**，37.5088717秒，103985068 bytes，Build GUID`97c3c51661934a0d87de49de82275752`，退出0、保护区变化0。company/product仅临时隔离Player数据目录，finally恢复并核对字节。
- Mono运行：上述同次exe实际PID42996退出0，唯一`YL_BEHAVIOR_ACCEPTANCE result=PASS`报告Unity2022.3.62f3c1/backend=Mono/development=False，creates/enters/exits均2，源GUID`ab27c9091baaec5429f47046ead9a152`，数据目录为`LocalLow/YLFramework_Behavior_Acceptance/A_build-mono-02`。首个回收器将company中的`.`未按Unity规则规范为`_`而误判；修正后`-VerifyExisting`核验**同一PID/log/exe**，未重跑/覆盖原证据，另存`player-validation.json`。exe SHA256`059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb`，log SHA256`93be1dcc365ec3096c1fd7d764261f6731563b1a93e61517200115582abecfd9`。使用`-batchmode -nographics`，不声称图形呈现已验。
- IL2CPP Low：`build-il2cpp-preflight-01` PID33520只读getter得到Mono2x/Disabled，与请求不符，`status=BlockedBeforeBuild`、无Build GUID、未构建/运行Player。Unity2022.3没有独立BuildPlayerOptions/CLI后端与裁剪覆盖；setter恢复逻辑默认值可能新增Standalone字典键，不能保证ProjectSettings字节不变。未调用后端/裁剪setter或手工覆盖配置；退出0、保护区变化0仅为预检成功，**不计IL2CPP/AOT/Low通过**。该项须在另获配置授权或独立接收环境中验证。
- 临时资产：`fixture-cleanup-01` PID41808将本组4个`GeneratedAcceptance_<32hex>`目录内及目录meta共20文件归档到同次`fixture-archive/`，记录原路径、GUID及SHA后才用AssetDatabase删除。退出0、保护区变化0；后置逐文件核对20个归档SHA一致、4个源目录及meta均无残留。真实脚本绑定与Mono场景资产仍可从归档追溯，不提交临时资产。
- 最终导入身份：`audit-final-01` PID14452退出0、保护区变化0，`compileFailed=false`。冻结146预期全部存在，**实际151份meta=146+5个新增入口**（Binding/Player/Reload/Unity Batch及ReloadProbe）；无缺失、孤立、本组重复GUID或AssetDatabase映射差异。PowerShell7扫描raw Assets共1876个GUID，本组GUID全局唯一；轻量FSM6 C#与9 meta共15项SHA仍等于B0；自有Prefab Missing Script=0。
- 最终实际编译图六程序集及输入数：Runtime484、Editor140、EditMode161、PlayMode45、Acceptance4、Acceptance.Editor5；本组每份源码均归属正确，rsp留于同次`assembly-inputs/`。实际注册UTF1.3.9/source Registry/resolvedPath为A自己的PackageCache，不以manifest声明替代。被动读取**本次Mono Player**的Runtime与Acceptance DLL，均无UnityEditor/XFFSM/XFGameFramework/XFAB AssemblyRef；SHA分别`8cb7d6709c8a8a326f59e780eddea613114bb62d71633e3d530f44458884c71b`与`2720875d71efd5c4c28cf50a94940e8cb34d962d5fc03b4ccc7ccc848ed50c4c`。本工程仍有旧包共存，不冒充无旧包独立接收工程。
- 最终只读来源核对：4份manifest的89个上游文件SHA一致，4份完整MIT仍在；受保护路径相对源码候选HEAD无diff。观察到`.vscode/settings.json`及`Assets/Atlas(AutoGenerate)/MainModule/`下`31dd0f6057ff73d89c56da5a46c4e39e_depend_on_res(auto).unity3d_Atlas.spriteatlas`、`9aa7ab4473730ba61a78819153f32058_depend_on_res(auto).unity3d_Atlas.spriteatlas`三项非本组差异，未接管/暂存/还原，不把最终工作区称为全干净，不把该结果扩展为完整原游戏G5验收。
- Git收口仅规范151份正式meta的453处空值行尾空白，忽略行尾空格后的差异为0，GUID/Importer/字段值不变。原始Unity/raw审计保留，不将其SHA回写；`audit-final-01/meta-submission-audit.json`另存各路径/GUID/原始SHA/提交版本SHA。独立比较89个上游GUID与151个本组GUID，复用数0；暂存排除二进制/缓存/临时资产/保护区及三项范围外差异，明显凭据字面量检查未检出。仅文档与空白收口不重跑已有效技术矩阵。
- 四个本地源码候选完整历史保留：Overlap `51e61da4c0bf9a472007c383247bef51fd148c20`；CC2D `a072bdde296ea6f2beb2f4539d9b200e6c9706b3`；Moveable `00672464d04795ea8d496126612c690f913471c9`；资产FSM `4b2803288f1c175cb4cb69f7291dc0f115d43649`。本轮另作中文本地修复/meta提交，完整SHA由最终回复及Git提供，避免同提交自引用；无Push/合并/改写历史。

以下模块及Review章节为源码候选阶段的历史记录；其中“未运行/待生成”只描述当时状态，T3-A实际结果以上节与末节为准。

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

- T3-A已实际完成A独立Unity2022.3.62f3c1导入/编译、EditMode60/60、PlayMode50/50、同进程Reload6/6、Create And Add跨编译重载、Windows64 Release Mono构建及实际Player退出0/PASS。离线53项只沿用源码阶段证据，不混入Unity计数；Reload及绑定也不混入UTF用例数。
- 新脚本/目录/文本资产meta均由A自己的Unity正式生成，共151份，原146冻结预期未重造；逐路径GUID/SHA见`BA26/audit-final-01/import-audit.json`和`meta-audit.json`。所有外部日志/XML/构建/归档留在`C:/Users/Kano/AppData/Local/Temp/kilo/BA26/`，不提交二进制、缓存或临时生成资产。
- 未完成/不外推：IL2CPP Low构建与AOT/裁剪运行；人工鼠标拖拽/完整图形Editor与Player视觉呈现；全面时序/Profiler测量、所有平台/裁剪级别；无旧包独立接收工程G4；主工作区集成及原游戏组合G5。自动GUI作用域、Undo/Redo、保存重导入通过不能替代人工GUI/完整作者流程。
- 本轮在授权边界内完成可执行门并保留上述阻塞，不宣布整个XF阶段、正式Exporter/独立交付或完整引擎矩阵完成。仅精确提交本组修复/验收入口/测试/说明及正式meta，三项范围外差异保持原状。
