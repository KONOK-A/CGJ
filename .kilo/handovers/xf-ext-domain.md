# C组领域融合检查点

唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。
本轮用户明确授权五项连续源码融合、必要修复、离线验证及中文本地候选提交；覆盖Plan历史未授权/分批等待状态，不覆盖共享区域、许可和验证边界。

## T3-C BatchMode追加授权

- 2026-10-19后续用户已正式授权本C worktree使用Unity `2022.3.62f3c1` BatchMode进行真实导入、EditMode/PlayMode、定向修复复验和条件具备时的独立Windows64 Mono/IL2CPP Low验证，覆盖下文上一轮禁止启动Editor/batchmode的历史限制。
- 本轮开工实核HEAD为`3dd320dc721398988111daa0f2b16ae0b1f6d7fc`，七个候选完整存在、分支`fusion/xf-ext-domain`、工作区/索引干净。主Unity仍在主工程；开工未发现A/B工作区的重型BatchMode，未操作或关闭它们。
- 使用显式绝对projectPath，不连接主工程Unity MCP；C同路径仅一实例。共享区域、原包、Packages/ProjectSettings继续只读，不合并、不Push、不删除worktree。
- 本轮证据独占`Logs/YLFramework.Domain/<Run>/`；`Tools/Domain/Invoke-DomainUnity.ps1`拒绝复用Run、已有同项目Editor或A/B重型任务占用。所有前轮通过数仍只是其原层级，真实Unity门以下续记。
- `G1-Import-01`在编译前端处无正常退出记录，不计通过；改为显式同步WaitForExit后`G1-Import-02`真实完整编译/退出0，未删Library或复制任何主Library/PackageCache。
- `G1-Audit-02`真实Unity CompilationPipeline/TestRunnerApi确认五模块源进入编译输入、C默认未定义YL_DOMAIN_EXCEL、Player编译图无XF/UnityEditor/可选Excel反向边；发现EditMode57与PlayMode12（含原准备28项原生）。140个本组资产/目录GUID实读、原始Assets meta唯一性通过。首次审计因既有超长路径meta读取失败，改用Win32扩展路径后通过，不跳过文件。
- C `G1-EditMode-01`实际46/57通过、11失败、0跳过：7项NUnit WorkDirectory空导致夹具Setup失败，2项未先Reload树，1项只读双Play配置前置条件，1项第三方MCP自动连接错误日志干扰。修正夹具目录与按E0合同Reload，子进程仅使用插件已有、不落盘环境覆盖关闭自动连接/自动服务，不改插件/工具持久配置、不吞日志；`G1-EditMode-02`实际56/57通过，唯一失败保持双Play前置条件。
- C ProjectSettings `m_EnterPlayModeOptionsEnabled: 0`保持不变。独立无XF `Logs/YLFramework.Domain/Carriers/R1`由实际当前Runtime/Editor/批准BouncyCastle及meta逐文件哈希快照建立，最小UPM依赖自然恢复，未复制原游戏、XF、MCP或Library/PackageCache。只配置载体自身EditorSettings；`R1-EditMode-01`真实57/57通过、0跳过，包含关闭Domain Reload连续两次真实Play。
- C `G2-PlayMode-01`原12/12通过；增加五类真实Debug/handler证据及Mission Int/Float/Bool约束后`G2-PlayMode-02`14/14通过、0跳过。日志明确记录前任`XFGameFramework.LoggerSystem.LoggerHandler, XFGameFramework.LoggerSystem`，由CLI门强制确认真实旧Logger共存，不用假handler冒称共存。
- 期间两次启动被A/B重型BatchMode占用保护拒绝，未生成测试结果、不计失败执行，也未停止或操作其进程；利用错峰准备本组载体后再运行。
- Unity会自然删除项目Temp，前轮Temp证据不可当持久交付；本轮独立工程、快照与构建均改存C自有Logs/Carriers，C自然退出不会清理它们。所有C受保护文件的受管差异迄今为空。

### T3-C最终实际结果

本节为本轮最新技术证据，覆盖下文上一轮全部“未执行引擎门”的历史状态；没有改正式Plan或自动合并。结束结论：**C组独立Worktree BatchMode技术验收完成，待主线集成**。该结论限定以下已实际验证范围，不等于主integration、所有平台或所有人工GUI已验收。

| 门 | 实际环境与证据目录（均在Logs/YLFramework.Domain） | 结果 |
|---|---|---|
| C真实导入/编译 | G1-Import-02；G1-Audit-Final | Unity2022.3.62f3c1退出0，实际Runtime/Editor/测试编译图已读 |
| C主体EditMode | G1-EditMode-02 | 57发现、56通过、1双Play配置前置条件失败、0跳过；C ProjectSettings保持原样 |
| 无XF独立载体EditMode | R1-EditMode-01 | 同源57/57通过，0失败/跳过；包含真正关闭Domain Reload连续两Play |
| C主体PlayMode/旧Logger共存 | G2-PlayMode-02 | 14/14通过、0失败/跳过；旧XF handler身份强制校验 |
| 无XF载体PlayMode | R1-PlayMode-01 | 原同次XML14/14通过，日志有Test run completed/Exiting code0；包装命令600秒超时，未取得OS退出码，保留差别、不覆写重跑 |
| Review作者/资产/窗口/复制 | G1-AuthorCleanup-03 | 最后定向15/15通过、0失败/跳过；覆盖subasset/兄弟/借用资产、GUID、Drawer、Clipboard、custom_data、Undo与自己清理 |
| 既有E0重排/Undo/窗口边界 | G1-E0-ReorderBoundary-02 | 实际图形设备BatchMode11/11通过；未修改E0 |
| B+C真实可选Excel | B1-ExcelTests-02 | 固定B09bec5a + C当前代码，真实TestRunner API 5/5通过、0失败/跳过、Editor退出0 |
| 独立Windows64 Mono | R1-MonoBuild-01；R1-MonoPlayer-01 | BuildSucceeded/0error/Options.None/Mono2x/Disabled；图形Player PASS且OS Exit0 |
| 独立Windows64 IL2CPP Low | R1-IL2CPPBuild-01；R1-IL2CPPPlayer-01 | BuildSucceeded/0error/Options.None/IL2CPP/Low/Release；原生产物存在，图形Player PASS且OS Exit0 |

- 原准备28项普通原生用例已全部被真实发现并运行；增加五类真实Debug与三参数类型后为30项原生行为用例，分布于57项EditMode中的16项与14项PlayMode。纯托管41项也已在真实Unity EditMode执行，不用离线数字替代发现数。
- 最终`G1-Audit-Final`实际发现57 EditMode/14 PlayMode；140个C新资产/目录meta由本C Unity自然生成，Assets原始GUID唯一。对原五个XF包140个身份集合检查无碰撞；没有复制旧XF GUID。完整路径/GUID/真实编译源在`unity-input-guid-audit.json`。
- Runtime Player图无UnityEditor/XF/可选Excel；Editor profile中受UNITY_EDITOR保护的UnityEditor API不是Player反向边。C默认不定义YL_DOMAIN_EXCEL，B缺席时主体独立编译成功，主体asmdef与共享E0/Core/Foundation/Resource/Save未改。
- Mono Build GUID `e9bce70ca8984c05b6b6aca0854b7af9`；IL2CPP Build GUID `db6ad778085943c6b3e29b36d670d059`。两份BuildReport.json、真实exe SHA256与执行PID/Exit0/marker在R1各Builds与执行目录。两构建各1168条warning，主要是既有CS1591/XML注释及已声明警告，0error；未关闭或掩盖诊断。
- 两个Player均未传batchmode/nographics，日志确认实际图形设备；故意发出的domain-error/domain-assert/domain-exception是Logger覆盖，不作为未处理程序失败隐藏。实际调用真实Resources后端、显式根类型catalog、三个仅靠字符串配置创建且有专用link.xml保留的领域子类、泛型Acquire、Pool/Update、冷却/到期/完成与资源归还。成功依赖PASS与OS Exit0，不仅BuildSucceeded。
- 两个G3工程是真正无XF/无MCP、无原游戏、无商业增强DLL的独立接收载体；并非拿含旧XF的C Player冒充独立交付。产品源和meta1482个文件逐文件前后哈希核对，记录sourceHead与输入清单；测试增量同步也校验哈希。没有复制主Library/PackageCache或C Packages/ProjectSettings。
- 真实旧Logger共存路径记录为`XFGameFramework.LoggerSystem.LoggerHandler, XFGameFramework.LoggerSystem`；无XF载体使用Unity原handler。五类真实Debug均经过C会话/原handler、事件与文件，退出后独占打开文件及归还handler通过。
- B1仅为隔离验证载体，固定`09bec5a675d4f920f5fc0e4fa65aaafb3fd8a300`源码以git archive读取；MiniExcel SHA256匹配批准值，Unity自然生成插件meta的Any=0、Editor=1。没有把B源码/DLL永久复制进C产品、没有等待B迁移或Merge；Mission跨sheet组合原子事务/全局Effect库xlsx仍是前轮已声明的未实现功能，不伪装通过。
- B1首次5项为2通过/3 WorkDirectory空Setup失败；修正C可选测试夹具路径后5/5。R1 PlayMode与B1首次CLI在测试结果已保存后关闭阶段未回收到包装退出码；先读取同次XML/log/进程状态，确认进程已无，再用实际TestRunner API保存原始结果并延迟稳定退出，B1复验退出0；没有对同次结果盲目重复启动。
- E0首次-nographics为0/11，全部因ShowUtility没有图形设备，原失败保留；只对该运行启用真实图形设备仍用BatchMode，11/11通过，不改E0源码、不忽略日志/断言。
- 发现Unity删除测试父文件夹不会逐个调用根配置OnWillDeleteAsset，导致Mission Clipboard测试生成的102/103留在自己的GUID目录。定向核对其mission名称、奖励和已删除父GUID后用Unity API清理；TearDown改为先逐个删除自己配表根再删自己文件夹，15项复验通过，随后只清理本轮空生成父目录。未删除用户资产或改原场景。

### T3-C Git与仍需补验

- 本轮C改动为真实验收工具、测试夹具修正/增量、140个自然meta与本检查点；没有改五项Runtime算法、原Logger、主体asmdef、Packages、ProjectSettings、Core/Foundation/Resource/UI/Save或正式Plan。中文本地提交完整SHA见本轮最终回复及git log，不为Hash自引用另建提交。
- **非C自动生成差异保留未暂存**：`.vscode/settings.json`被Unity IDE集成改成worktree解决方案；原游戏`Assets/Atlas(AutoGenerate)/MainModule/31dd0f6057ff73d89c56da5a46c4e39e_depend_on_res(auto).unity3d_Atlas.spriteatlas`与`9aa7ab4473730ba61a78819153f32058_depend_on_res(auto).unity3d_Atlas.spriteatlas`被自然填充packedSprite缓存。只读确认后未提交、未擅自回退。故最终工作区不能报全局clean；只保证C候选已提交、索引无额外内容。
- 保留图形Editor/主线清单：人工观察全部窗口/图标预览与真实桌面拖拽、缓存View集成与原场景/图集非干扰、正式分发包导出首导入及中央许可汇总、主integration组合后复验、其他平台/更高裁剪级别。E0本次使用真实图形事件/设备的自动化11项，不等于已完成这些人工视觉验收。
- C本体双Play配置前置条件未改成通过；已由同源独立载体实际57/57包括双Play建立技术证据。集成owner若要求本体原设置下该个案也绿，需要另授权或预配置C/主线EditorSettings，而不是修改测试条件或忽略它。

## 基线与边界

- 实际工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-domain-1c975de3e2f7676b`。
- 分支：`fusion/xf-ext-domain`；开工HEAD及共同B0：`c88500aef6651f5b13ac933dd1f209ecb314e92d`，祖先检查退出0，工作区/索引干净。
- 只改C组Systems、对应Editor、Domain测试/夹具、专属工具和本说明。主Plan、Foundation/Core/Resource/Save、主体asmdef、Packages、ProjectSettings只读。
- 不调用Unity MCP、不启动Editor/batchmode、不Push/合并/Rebase/Reset/Clean/Stash，不写其他worktree。
- Shell实际为Windows PowerShell 5.1，脚本按5.1语法编写；项目Unity为2022.3.62f3c1。不存在本工作区`.editorconfig`或YL子树补充AGENTS。
- 不手造Unity行为stub或meta；新资产/目录meta统一待集成owner用Unity生成，未执行引擎门不计Passed。

## 进度

| 模块 | 状态 | 候选提交/证据 |
|---|---|---|
| Reddot | 源码候选完成，引擎待验 | `5c04f2b`；9项托管红→绿，Runtime辅助编译退出0/41个既有warning |
| Logger | 源码候选完成，引擎待验 | `c2e1a0f`；6项Logger+9项Reddot托管通过；默认关闭，原embedded只读 |
| Skill | 源码候选完成，引擎待验 | `10699e6`；4项Skill+前15项托管通过；Runtime/Editor/EditorTools辅助编译通过 |
| Buff | 源码候选完成，引擎待验 | `2aa7c22`；5项Buff+前19项托管通过；普通Buff/Effect作者工具辅助编译通过 |
| Mission | 源码候选完成，引擎待验 | `9847078`；11项Mission+前24项托管通过，四项直接Bug红→绿；不改Save |
| Excel接线 | 可选候选完成，有明确未实现范围 | 精确B `09bec5a675d4f920f5fc0e4fa65aaafb3fd8a300`；3项真实xlsx通过 |
| 引擎验收入口 | 已准备、辅助编译通过，未执行 | 初批13项+Review15项=28项普通原生用例、2项可选SO用例及公开Player消费者 |
| 整组Review | 已完成一次独立审查与直接修复回归，不复审 | 审查范围B0..`a7cba4c`；41项领域托管与3项xlsx最终通过 |

## 来源与许可交接

只读来源为主工程`Library/PackageCache`中Plan所列精确revision；Logger用本工作区embedded源码。Reddot/Logger为MIT，Copyright (c) 2025 弦风课堂；Skill/Buff/Mission为MIT，Copyright (c) 2024 弦风课堂。新文件保留适用声明，各模块附完整许可；中央LICENSE/Notices由集成owner汇总。不承接logo、安装器、Demo、旧Analyzer或未核实DLL。

## 实施裁定

- 沿用原Manager领域职责与入口，不引入统一Service/领域工厂平台。红点独立token只管本次来源，Reset先清状态再通知；旧token不能撤销新登记。
- 离线执行只覆盖无需Unity原生调用的真实生产代码，真实Unity引用用于辅助编译；UI/资源/Play/Player不能用离线结果替代。

## 引擎待验

全部模块的Unity编译、EditMode/PlayMode实际运行、作者工具保存重开/Undo、资源Lease和宿主生命周期、关闭Domain Reload连续两Play、无XF独立Mono/IL2CPP Low构建运行、正式导出/首导入及原游戏非干扰均未执行。后续按模块补齐具体用例和入口。

## Reddot使用与验证

- namespace为`YLFramework.ReddotSystem`。保留`AddPath`/`RemovePath`/`Reset`和路径通知，`ReddotPath`改为只读快照，防止绕过通知；新YL身份不提供XF命名空间兼容。
- 推荐`var registration = ReddotManager.RegisterPath("Mail/Gift");`，owner退出`registration.Dispose()`；普通Remove不夺取独立来源。Reset清空后通知且使所有旧token失效，按Ordinal整段匹配，规范空白/斜杠，null安全。
- `Reddot`挂在持续存活的UI节点，显式设置`Path`和`Indicator`，只切换该目标，拒绝本物体/祖先；停用与销毁精确退订，不清业务路径。不要再依赖“所有子节点即指示器”的旧隐式行为。
- `Tools/Domain/Verify-Domain.ps1 -Mode Runtime`使用真实Unity引用辅助编译，退出0、41个既有warning；`-Run reddot-red`实跑9失败（YL能力缺失）；`-Run reddot-green`实跑9通过、0失败/跳过，证据为各自`Temp/Domain.Offline/<Run>/results.xml`。
- `ReddotLifecycleTests.OnlyExplicitIndicatorChangesAndDisabledHostUnsubscribes`已准备，未运行。覆盖嵌套UI业务子物体不变、停用退订、重新启用刷新、自指示器拒绝；后续补实际UGUI/缓存View/场景与双Play消费者。
- 原缺陷由Plan及只读原源码确认，未运行原XF测试，不把YL缺能力红例冒称原XF引擎Bug复现。

## Logger使用与验证

- `YLFramework.Logging.LoggerManager.Enable(new LoggerOptions { WriteToFile = true })`为唯一Unity接管入口，只在有效Play主线程显式调用；默认只读`Current/IsEnabled`不创建宿主、不访问目录、不写盘、不抢handler。Enable默认也不写盘，必须显式`WriteToFile=true`。关闭调用`Disable()`，宿主disable/destroy/quit亦释放。
- 默认目录为`Application.persistentDataPath/YLFramework/Logs`，可以配置独立目录。过滤字典在创建会话时复制，null全开、空表全关。事件始终触发，回调线程为日志原线程，不得访问非线程安全Unity API或等待会记录日志的其他线程。
- 沿用原同步writer/立即Flush、五类过滤、格式化和事件职责；加UTC高精度+GUID与CreateNew防会话碰撞、串行锁、回调逐项异常隔离。嵌套日志明确丢弃并计数，格式化ToString也在递归门内；失败通过`LastError`及实际`LogFilePath`诊断，不递归Debug报错或静默冒称成功。
- 只归还仍由自己拥有的handler；后续handler接管后不覆盖。已关闭会话若仍被后续handler串联，只透明转发原handler，不重开文件/监听。没有新后台线程、Analyzer、整Runtime AlwaysLinkAssembly或Debug批量替换。
- `logger-red`：9通过/5缺能力失败；`logger-green`：14通过；新增格式化重入直接回归`logger-format-red`为14通过/1失败（多发nested-format），修复后`logger-format-green`为15通过/0失败/跳过。并发100条完整记录、两会话文件独占读回、文件错误、过滤/事件/递归均实际执行。Runtime辅助编译退出0/41个既有warning。
- Context7没有提供精确2022.3 ILogHandler资料，补读Unity 2022.3官方Scripting API，接口/前任handler用法亦由实际程序集编译确认；不外推线程Unity行为。
- `LoggerLifecycleTests.DefaultIsPassiveAndLaterHandlerKeepsOwnershipOnClose`已准备未运行。无XF独立Mono/IL2CPP Low消费者、五类真实Debug、同秒连续双Play、disable/destroy/quit释放、原Logger共存仍待引擎门，旧Logger阶段9证据不能替代。
- 连续启用时发现合法handler链`新YL会话→后继handler→已关闭旧YL会话→原handler`被全局递归深度误吞。`logger-chain-red`真实35通过/1失败；改为线程内按会话身份判重后`logger-chain-green`36/36，既保留同会话递归保护，也允许关闭会话透明转发。此为附加直接修复，不归入未运行的Unity handler验收。

## Skill使用与验证

- namespace为`YLFramework.SkillSystem`。保留`IEquippableSkill`、`SkillBase`虚方法、Release/Cancel/Interrupt/冷却及`SkillManager.AddSkills/RemoveSkills/InterruptAllSkill`流程；配置保留原字段/子类/脚本GUID与FullName用途，新YL身份无旧资产自动迁移。
- `AddConfig(SkillsConfig)`借用调用方配置；`LoadConfig(ResourceSystem, ResourceAddress)`由Manager持根Lease。配置重复ID、空条目和无效/抽象/不可创建脚本明确失败，诊断带ID/配表/类名。不强制注册工厂；自定义子类需按真实IL2CPP保留策略验收。
- 每次装备克隆配置为运行态`SkillData`，释放回收销毁克隆并清Releaser/回调；YL实例ObjectPool按实际类型复用。先从宿主集合摘除再调用移除逻辑，回收延迟到当前嵌套操作结束，代次检查阻止旧回调继续写复用对象。冷却完成进度保持1，不再归零。
- CLR宿主关闭必须`CloseHost(host)`；Component inactive暂停，fake-null销毁后清理。`Shutdown()`归还全部宿主、三相位句柄、池及配置Lease；Player退出和Editor退出Play接本组局部清理，新Play复用Foundation会话复位，不改共享契约。
- 普通作者工具从精确原revision机械承接：菜单`Window/YLFramework/SkillSystem`、双击配表、创建/复制/删除/搜索/选中/拖拽、数据类型限制、字段自定义GUI、脚本选择/编辑/选中、ID改名。复用E0 ReorderableTreeView，补实际索引、窗口Dispose和切换归还；生成目录改`Assets/YLFrameworkData/Skill`，删除只处理自己的GUID目录，不删任意外部引用。图标按YL Resource ProjectId/address输入并复用预览，不调用XFAB。
- Runtime纯数据不依赖领域Editor Attribute；原Drawer迁至Editor，由本领域Inspector按字段分发，保留自定义Attribute扩展。脚本批量校正改为显式`Tools/YLFramework/Skill/Refresh Script Bindings`，不在导入时全工程改资产/SaveAssets。xlsx命令等待真实B API。
- `skill-contract-red`实跑15通过/1缺能力失败；`skill-managed`实跑19通过/0失败/跳过，含释放→完成→冷却终点、取消/中断次数、释放回调内结束代次不继续旧回调。不是原XF引擎Bug复现。辅助Runtime通过/41 warning；Editor通过/40、EditorTools通过/4。迁移中出现Editor命名空间和Drawer原类名不一致的辅助编译错误，已直接修复后重跑。
- `SkillOwnerTests.ConfigIsCopiedAndClrHostCloseReturnsSkillAndCallbacks`已准备未运行，覆盖原配置不变、回调内Close、重入移除和跨owner池复用。实际资源Lease、Inactive/Destroy、双Play、作者资产保存重开及无静态new的多态AOT仍待引擎门。
- `Tools/Domain/Import-DomainSources.ps1`仅用于首次精确来源机械承接，拒绝覆盖任何已存在目标，不得重跑覆盖修复；不复制meta/asmdef/DLL。PowerShell5.1首次解析因含中文的无BOM脚本失败，改为ASCII脚本后成功；未写来源文件。

## Buff使用与验证

- namespace为`YLFramework.BuffSystem`，保留`IAddableBuff`、`BuffBase`、Single替换/Additive叠加、永久/定时、按效果类型/ID/分类查询、owner添加/移除回调；没有重写数值战斗系统，原包本身只维护效果列表。
- `AddConfig`借用根、`LoadConfig(ResourceSystem, ResourceAddress)`持根Lease；空效果/重复Buff ID/类型缺失明确诊断，绝不回落BuffBase。`BuffData`及每个Effect均为本次实例克隆，原配表和跨owner效果不共享可变运行态。
- `AddBuff`/`RemoveBuff`保留静态入口；CLR退出`CloseHost`，末个Buff移除即删除效果索引和owner引用；清userData/Owner/BuffData。回调前摘除、延迟YL池归还、快照+代次检查防自删/互删/复用继续执行；同owner同ID递归添加明确失败而非无限递归。`Shutdown`收三相位、池、缓存与根Lease。
- 普通Buff与Effect窗口、数据子类创建/复制/删除/搜索/拖排、效果分组、多列表格、效果选择/编辑、脚本绑定、项目效果分类、ID/资产改名均承接。设置和Drawer全部在Editor程序集，复用YL项目设置基类；设置路径`YLFramework/Buff`，资产根`Assets/YLFrameworkData/Buff`。Buff列表用E0，Effect分组保留领域树并补owner payload、拒绝陈旧快照/跨窗口、Undo与窗口释放；原反射打开Effect/Settings改直接API。
- 修复配置空条目查询NRE、重复扫描Effect追加缓存、末个Buff后效果owner残留。共用C组图标Drawer补YL ResourceProject选择与真实预览；未复用XFAB。删除只清自己GUID目录，不删外部Effect/配置引用。
- `buff-contract-red`为19通过/1缺能力失败；`buff-managed`为24通过/0失败/跳过，覆盖真实BuffBase永久/定时进度、池reset清owner/userData/时间。Runtime辅助编译0/41 warning，Editor0/40，EditorTools0/8。额外warning为迁移保留的未用字段，不关闭诊断、不把warning当阻塞。
- `BuffOwnerTests.AdditiveSingleAndLastEffectCleanupKeepDefinitionsUnchanged`准备未运行：叠加/替换、深克隆、自身再次移除不重入、末效果索引清理、无效类型诊断。实际到期Update、inactive/destroy、跨宿主/场景/双Play、ResourceLease及Editor分组拖拽保存重开/Undo、AOT仍待引擎。
- B交接在本模块期间出现，已读真实List/SO预览事务签名，但记录尚无候选Commit。继续Mission，不依赖未提交B代码，不修改B文件。

## Mission使用与验证

- namespace为`YLFramework.MissionSystem`，保留多配表、MissionBase状态/回调、参数Int/Float/Bool、仅进行中/仅更大值约束、条件组合进度、RewardInfo数据及完成后共享参数保护。奖励由业务在完成回调读取运行态`MissionConfig.rewards`发放，原包也没有第二个奖励发放后端，本轮不新增存档/经济权威。
- `AddConfig`借用源根但建立参数/条件/奖励/任务配置克隆；`LoadConfig(ResourceSystem, ResourceAddress)`持根Lease。任务实例按实际类型接YL ObjectPool；移除/Reset先摘监听、静默归零，清回调/userData/运行克隆并归还参数EventChannel句柄；配置失败回滚部分登记。
- 保留原string/int查询和Set/Get/Add入口。新YL身份使用Ordinal确定性FNV-1a和注册键碰撞诊断，不复用原Animator hash或持久化它；string入口额外验证原名，不因hash相同取到别的权威。已用`costarring/liquid`真实碰撞验证拒绝。这个身份变化只针对尚无旧YL消费者的新系统，历史XF资产/hash迁移不自动兼容。
- 严格验证缺失类、抽象类、构造器、空条目、重复ID/参数和未知条件参数；错误带配表、ID、名称/类名，不静默降为MissionBase。`Reset`清业务监听；需重挂业务回调后再把任务设为InProgress。`RemoveConfig/Shutdown`使外部旧参数不再触发任务。
- Float补Equals/NotEquals，先更新IsMeet再发进度；二值比较用0/1，递增阈值保留有界比例进度，Bool维持原作者工具的相等语义。状态/进度回调后检查代次，回调内移除不能继续完成处理或重置新会话参数。
- 普通任务/参数双列表、创建/复制/剪贴板/删除/拖排、条件选择弹窗、参数改名同步条件、Reward列表、脚本选择/编辑/绑定、类型约束和ID改名均承接。两个平面树复用E0并补Dispose；参数Inspector归YL命名空间并释放SerializedObject。删除路径从原EquipmentSystem纠正为统一`MissionAuthoring.GeneratedDirectory`，只删除`Assets/YLFrameworkData/Mission/<rootGUID>`，不递归删外部引用。
- `mission-contract-red`24通过/6缺能力失败；承接原条件/Reset后`mission-bug-red`27通过/3实际失败，进度通知加直接回归后`mission-progress-red`27通过/4实际失败；修复后`mission-managed`35通过/0失败/跳过。新增覆盖解绑/重复Bind、回调内Dispose、参数配置克隆及真实hash碰撞。没有使用Unity行为stub或跳过失败。
- 当前五模块Runtime辅助编译0/41 warning、Editor0/40、EditorTools0/10；`mission-prepared-tests`把已准备EditMode/PlayMode源码与真实程序集辅助编译成功，未运行Unity用例。`MissionOwnerTests`准备了配置/条件/奖励不变、Reset和完成回调内移除及旧参数失效用例。
- 实际ResourceLease、Editor资产保存重开/剪贴板/Undo、关Domain Reload双Play、场景、无XF Mono/IL2CPP Low、多态类型裁剪、导出与原游戏非干扰仍待引擎，不能用35项托管结果替代。

## 可选Excel与组合条件

- B已提交Excel `09bec5a675d4f920f5fc0e4fa65aaafb3fd8a300`，含前置DataAssets候选`53edd50`。使用`git archive`从本组共享对象库读取这一个精确提交的DataAssets Runtime/Editor及Excel，解入本组`Temp/Domain.Excel/<Run>/B`；不读取B在途Save、不复制后端到C产品、不改B文件。
- C可选目录`Assets/YLFramework/Integrations/DomainExcel/`，新可选Editor/Test asmdef，不改主体asmdef。先集成B DataAssets+Excel，再集成C并显式定义`YL_DOMAIN_EXCEL`才启用；默认普通Editor不引用它，导出owner须另带B完整后端/许可并生成Editor-only插件meta。缺B的单分支只验证普通闭包，不宣称Unity已解析可选asmdef。
- 菜单`Assets/YLFramework/Domain Excel/Preview Import Entries`、`Export Entries`支持Skill/Buff/Mission根或条目的选择、三行表头、实际派生数据类型、脚本类验证、显式差异确认、取消、缓存失效；分别使用`skills`/`buffs`/`missions`工作表。精确拥有目录由本组Controller决定；新空根可用B真实subasset模式，预览/取消不创建目录。
- Buff effects恢复成熟的ID数组格式，引用真实EffectData资产，缺失/歧义拒绝；不把Unity对象写为脱离身份的JSON。GUID绑定字段不作为xlsx输入，parent由暂存初始化绑定。B的initialize先于ImportRow，因此脚本/条件检查在导入后差异执行，不在初始化时误拒绝新行。
- 领域差异校验固定依赖该B候选的`ExcelChange.After = FullTypeName:JSON`格式；声明在`Dependencies.json`，未来B变更须重验，不猜通用接口。只借用真实API，不复制B解析器或建立空后端。
- Mission参数用独立`params`工作表/`.params.xlsx`与独立确认事务；参数名标记为真实Excel ID，重复名称拒绝。参数预览提交前再检当前条件引用和资产清洁度，普通作者改名也补Undo。**未实现**任务条目+参数的跨sheet原子组合事务、全局共享Buff效果库xlsx导入；后者继续使用完整普通Effect作者窗口，不冒称全功能xlsx通过。
- `Tools/Domain/Verify-DomainExcel.ps1 -Run pre-review-final -BCommit 09bec5a`：C普通Editor/EditorTools 0/40、0/10；精确组合Runtime468源/38warning、Editor147源/10warning、B完整可选Excel7源/0warning、C adapter1源/0warning、可选测试+runner3源/0warning全部辅助编译退出0；实际选定3项纯托管xlsx用例全部通过、0失败/跳过。
- 三项xlsx覆盖实际参数往返/差异/取消后确认、重复名称错误、不允许工作簿过期后提交。两项SO资产用例一并编译但明确过滤未运行，覆盖新行在ImportRow后校验、取消不建资产、效果ID真实资产/缺失拒绝。后端DLL只在Temp输入，实测SHA256匹配B固定`0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e`。
- 早期`combined-1`因本组新Attribute缺限定名失败；`combined-2`定位为辅助工具混用了NetStandard Runtime与mscorlib Editor参考，统一真实Mono BCL后`combined-3/4/5/pre-review`完整通过。没有删源文件、排除Unity作者代码或关闭警告绕过编译。

## 原生与Player验收入口

- `Tools/Domain/Verify-Domain.ps1 -Mode Tests -Run pre-review-final-tests`已编译全部13项普通原生用例和公开消费者源，未运行；仅有真实TestRunner3.5参考对离线NUnit3.14的CS1701 warning，未抑制。辅助测试程序集使用真实friend名称`YLFramework.Tests.PlayMode`；另把Player模板单独按非friend `Assembly-CSharp`编译，防止误用internal接口。
- 13项包括实际UGUI Image/Button与显式指示器、退订/重启、Logger默认静默/后继handler、Skill池跨owner与配置不变、Buff叠加/效果缓存/配置不变、Mission奖励/条件/Reset/回调内移除、真实ResourceSystem/AssetLease配可控输入来源、Inactive暂停与实际场景卸载、三个领域根正确删除范围/外部资产保留、参数同步改名Undo及关闭Domain Reload连续两Play。输入来源仅为可控IResourceSource，不模拟任何Unity原生行为；真正Resources后端另在Player消费者准备。
- 提供`ConfigCount/HostCount/EffectOwnerCount`只读轻量诊断。原生用例拒绝接管已登记领域/Resource权威，生成资产/场景/日志均为自己命名的内容。双Play用例要求验收owner预先配置关闭Domain Reload，本测试不写ProjectSettings。
- Player源与创建器在`Assets/YLFramework/Tests/Fixtures/Extensions/Domain/*.cs.txt`，专用`link.xml.txt`只保留接收夹具三个反射子类，不保整Runtime。`Tools/Domain/Prepare-DomainPlayerProbe.ps1 -Run <新名称>`只在本组Temp生成接收资产脚本，不打开工程/Editor。后续独立无XF接收工程启用`YL_DOMAIN_PLAYER_PROBE`并运行夹具菜单创建自己的Resources根/子资产，再分别做Mono和IL2CPP Low；类创建只来自配置FullName，无静态new/typeof子类。
- 公开Player消费者覆盖默认Logger无副作用/五类日志/文件独占释放/handler归还、红点分段、真实Resources catalog显式根类型+域Lease、Skill释放冷却、定时Buff、Mission完成和归还；输出`YL_DOMAIN_PLAYER_PASS`才可算运行通过。本轮没有该输出，不计Player通过。
- `DomainClosureRunner`读取当前Player profile及单独消费者程序集真实引用，确认均无XF、UnityEditor或可选Excel backend；这是**程序集引用检查**而不是Unity/Mono Player/IL2CPP构建结果。
- 此前原生准备过程编译发现测试API命名空间、UI/CoreModule参考、friend override级别和领域树构造次序问题，已按实际Test Framework1.3.9源码/真实SDK修正后重跑，不通过stub补类型。

## 完整引擎待验矩阵

| 门 | 输入/操作 | 必须观察 | 本轮 |
|---|---|---|---|
| Unity导入/编译 | 先B后C，可选symbol按需开启，Unity生成全部新meta | 无丢失引用、循环依赖、Player的Editor泄漏；插件Editor-only | 未运行 |
| Reddot真实UI | UGUI、嵌套业务节点、同路径两来源、禁用/缓存显示/销毁 | 只动指示器、精准释放、刷新与退订；原游戏UI不干扰 | 已备自动用例，缓存View人工补验 |
| Logger独立/共存 | 五类Debug、连续启停、后台线程/递归、旧Logger或后继handler | 默认不创建/写盘/抢handler；每会话独立，最后writer可独占打开 | 托管部分已验，其余未运行 |
| 作者资产/GUI | 创建/复制/删除/拖排、保存重开、脚本/GUID与图标、条件/奖励/参数、Undo | 类型/父引用/ID保持；只删拥有资产；窗口无陈旧缓存 | 自动与人工场景已备，未运行 |
| xlsx原生 | 三根新旧/派生条目、效果ID、无效引用、差异/取消、脏/陈旧资产 | AssetDatabase事务与真实GUI正确；已知两项未实现不可计验收 | SO用例仅编译 |
| Resource与宿主 | 三域根租约、外部独立租约、CLR关闭、Component inactive/destroy、场景卸载 | 不关Resource权威，计数归零，晚回调不写旧对象 | 已备未运行 |
| 双Play | 外部配置Disable Domain Reload，连续两次Play | 旧path token不撤新来源、handler不串会话、writer释放、三域无旧句柄 | 已备未运行 |
| 无XF Mono/IL2CPP Low | 独立接收工程、显式类型catalog、夹具专用反射保留 | 真实构建运行PASS，资源/池/回调关闭，含Logger无XF与旧Logger共存变体 | 消费者与引用闭包已编译；构建/运行未做 |
| 分发/回归 | owner汇总MIT版权、保留GUID、正式导出/首导入、原场景回归 | 新能力不改变原XF与既有YL契约/存档/日志/游戏数据 | 未运行 |

## 唯一Review与直接修复

独立审查覆盖B0到`a7cba4cccb32229fd8b7640cd75cc61daa7b3e7d`全部六提交，返回1项Critical、9项Important、1项Minor。只做本次直接修复与验证，没有再次全库审查、再次派发Reviewer或修改共享区域。

| 事项 | 实际修改 | 验证层级 |
|---|---|---|
| 单项删除误删根/外部文件（Critical） | 局部共用`DomainAssetOwnership`先解除根列表引用；自己根内subasset对象级Undo删除；只有自己GUID目录且文件内仅一主对象才删文件；借用资产只解引用 | 真实SDK编译；三域subasset/兄弟/外部文件用例已备未跑 |
| Stateful Drawer串条目 | 三域Inspector按选择重建默认Drawer与SerializedObject/属性缓存，释放清空；Mission脚本GUID属性每次重新取得 | 真实SDK编译；三域选择切换/释放用例已备未跑 |
| RemoveConfig回调抛错/再入 | 先从查询注册中摘除并标记正在移除，逐宿主/实例收集错误，批次末归还池；finally释放根Lease并禁止同配置再登记 | 真实SDK编译；两域双owner异常/Lease/回调内再装备用例已备未跑 |
| 批量操作复用旧快照 | 外层操作深度、原宿主/成员身份/代次检查覆盖批量中断与移除，异常继续收尾后汇总 | 3项真实CLR技能竞态红→绿；Buff buddy移除用例已备未跑 |
| Single Buff复活已关宿主 | 只为在途添加维护嵌套深度及取消标记；CloseHost使整个在途/批量添加失效，结束后释放owner引用 | 真实SDK编译；Single替换回调Close用例已备未跑 |
| 红点注册回滚UI旧值 | 失败回滚也通知最终状态，原错误与回滚通知错误均保留 | 真实托管红→绿 |
| 非空失效数据脚本GUID降级 | 仅空GUID允许基础数据类型，非空失效明确带根名/GUID报错 | 真实SDK编译；三域坏GUID用例已备未跑 |
| subasset ID改名误改根 | 根内subasset只改对象名，文件级改名仅限独占目录单对象主资产；清根缓存 | 真实SDK编译；三域改名/根名/兄弟保留用例已备未跑 |
| Mission Clipboard只识别文件 | 用GlobalObjectId保留GUID+local identity，协议版本独立；先解析全部有效对象再复制 | 真实SDK编译；同文件两任务不同奖励的复制用例已备未跑 |
| 参数复制遗漏custom_data | 配置Clone公开给作者工具复用，私有自定义数据显式JSON配置；复制清运行值/回调并产生唯一名称 | 真实JSON往返+配置复制红→绿；Editor Duplicate用例已备未跑 |
| Buff选择偏好键共享（Minor） | 改为独立`YLFramework.Buff.SelectedConfig`，不读取/迁移旧XF值 | 源码常量与辅助编译核对，实际Editor未运行 |

- 裁定：偏好键虽被评为Minor，但与当前新YL身份/不干扰旧XF的范围直接相关，因此同批一行修正；无额外框架或迁移。代价是此前未发货C候选的选择记忆不迁移，不影响配置资产。
- 对Reviewer明确不要求的四项逐一沿用授权：不新增旧XF身份兼容（旧资产/hash需独立迁移）；不增加Save或自动发奖权威（业务负责消费奖励数据）；不伪装Mission跨sheet/全局Effect库xlsx；`Changes.After`仅保证固定B提交，其他revision需重验。没有把这些已声明边界当作未修缺陷或引擎通过。
- `review-observer-copy-red-2`真实36通过/2失败（亮灯回滚、custom_data丢失）；`review-skill-batch-red-2`真实0通过/3失败（跨owner中断、同ID替代误删、异常中止批次）；`review-fixes-green`及最终`final-domain`真实41通过/0失败/跳过。技能竞态夹具只安排真实CLR技能、真实ObjectPool和宿主状态，不创建伪Unity对象或原生行为stub。
- 新增原生回归15项：资产/选择/GUID/Clipboard/Duplicate 11项，退出/Lease/Single/buddy移除4项；最终`final-native-prepared`整体辅助编译通过，未执行。不要把这15项或最初13项计入Passed。
- 已核实原生测试SDK的friend override访问级别；准备阶段的`IUnRegister`非IDisposable用法、过期辅助程序集与Buff回调public访问级别错误已修正后重编，不通过改共享API或补空类型绕过。

## 最终证据与交付

- 模块候选：Reddot `5c04f2b`；Logger `c2e1a0f`；Skill `10699e6`；Buff `2aa7c22`；Mission `9847078`；可选Excel/验收入口 `a7cba4c`。本检查点随最后Review代码修复同批提交；最后Hash以本分支`git log`和最终回复为准，不为自引用Hash另建docs提交。
- 领域实际执行：`Tools/Domain/Verify-Domain.ps1 -Run final-domain`，41 passed / 0 failed / 0 skipped。
- 真xlsx实际执行：`Tools/Domain/Verify-DomainExcel.ps1 -Run final-review-fixed -BCommit 09bec5a`，3 passed / 0 failed / 0 skipped；28+2原生用例、Player构建/运行均不在此计数。
- 额外复用既有只读工具：`Tools/Verify-UIPanel.ps1 -Mode Pure`实跑Panel配置6/6；`-Mode PureCore`实跑Core参数/集合池28/28，均0失败/跳过。不是全工程Unity套件；未因本组仅增量docs/Git收口而无效重跑。
- 最终辅助编译：C Runtime 0error/41warning、Editor 0/40、EditorTools 0/10；精确B组合Runtime468源/38warning、Editor148源/10warning、B Excel7源/0warning、C adapter1源/0warning、可选测试3源/0warning全部退出0。测试准备保留CS1701版本提示，不关闭诊断。
- 最终原生/公开消费者编译与引用证据：`Temp/Domain.Offline/final-native-prepared/`；领域XML：`Temp/Domain.Offline/final-domain/results.xml`；xlsx XML、编译日志和精确B快照：`Temp/Domain.Excel/final-review-fixed/`。这些全部为Temp证据，不进Git。
- 最终接收夹具：`Temp/Domain.PlayerProbe/final/Assets/DomainFixture`，由专属工具从已编译模板机械生成。需要独立无XF验收工程及owner配置symbol/资产/Build；本轮没有打开接收工程、运行Editor或构建Player。
- 已核对五模块完整MIT许可与作者年份：Reddot/Logger 2025，Skill/Buff/Mission 2024；中央许可汇总与meta仍由集成owner处理。无B后端、未核实DLL、缓存或Temp证据纳入产品提交。
- 所有原生门依上表保持未运行：Unity编译/导入、真实UI与缓存View、作者保存重开/Undo、真实Resource后端、关闭Domain Reload双Play、Logger无XF与旧包共存、Mono/IL2CPP Low、正式导出/首导入及原游戏非干扰。源码候选及离线证据不等于这些门已通过。
