# C组领域融合检查点

唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。
本轮用户明确授权五项连续源码融合、必要修复、离线验证及中文本地候选提交；覆盖Plan历史未授权/分批等待状态，不覆盖共享区域、许可和验证边界。

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
| Mission | 源码候选完成，引擎待验 | 11项Mission+前24项托管通过，四项直接Bug红→绿；不改Save |
| Excel接线 | 待核对B真实API | 开工时B交接文件不存在，不空等、不造接口 |
| 整组Review | 待五项候选完成 | 仅一次独立Review及直接修复回归 |

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
