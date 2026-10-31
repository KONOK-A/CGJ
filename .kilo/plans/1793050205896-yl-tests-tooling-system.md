# YL 测试与工具体系整理实施计划

> 状态：**正式结项**。T1–T7及用户追加的会话、Player运行、IL2CPP/HybridCLR、两个NativeInput、风险导向警告收口和一次独立Review均已完成。最终两版Daily十模块各2355叶、Special七模块各54叶通过；Mono/普通IL2CPP/HybridCLR实际运行及同产物重复通过。公共31、两版安全8/8、分发拒绝保护各3/3、格式1136文件通过；Analyzer181沿用未受影响的有效证据，不冒称本批重跑。独立Review的3项Important已修复并完成负例与真实宿主验收，无未关闭必修。当前入口及终态证据见“正式结项总览”；下方分阶段记录保留其当时范围，不改写历史失败。
>
> 结项限于指定双版本Windows矩阵：Unity6 HybridCLR仅已核验ParameterOwner维护源；NativeInput为真实EditorWindow事件链，不等于物理OS设备自动化。DSP/真实音频输出、IDE/其他平台、未声明SDK、任意泛型/高裁剪及V2替换回滚不外推为通过。一次内部只读Review已按明确授权执行；后续Git提交推送已单独授权，范围见“Git收口”。仍不授权可见Agent Manager会话/worktree、历史归档清理或退出豁免。
>
> **实施者：** 按 `executing-plans` 逐项推进并记录检查框；不默认每项启动新代理/审查会话，不新建分支或 worktree。Git/独立 Review 等执行动作按届时明确授权办理。

**Goal:** 面向 YL 整个框架的长期开发与发布，基于现有资产建立职责清楚、入口稳定、可复现、维护成本可控的测试与工具体系。

**Architecture:** 保留 NUnit/Unity Test Runner 和既有模块边界，以精简测试集定义、一个日常 CLI、少量进程/工作区/结果公共能力连接现有测试。明确隔离 Editor 支持代码和显式安装的 Player 探针，先完成 Reddot 真实纵向试点，再分组治理迁移，不建设通用编排平台。

**Tech Stack:** Windows；当前验收Unity `2022.3.62f3 (96770f904ca7)` / `6000.3.27f1`，C#9；沿用已用 PowerShell 7、NUnit/UTF、Mono 离线执行方式及 Analyzer 的 SDK `9.0.312`/现有锁定依赖。原c1目标经后续明确选择官方2022及主工程迁移授权替换，不将官方结果冒充c1通过；不默认继续升级Unity/SDK/包或新建.NET测试平台。Save特殊运行方式单独评估，不硬并。

**Spec:** 本文件的已确认决策与设计树。上轮兼容计划仅提供背景，不把其完整执行矩阵与历史目录作为本批默认实施范围。

## 已确认决策

- Q1：用户选择“是，按此目标”。本批面向 YL 整体，不局限于 Unity 6.3 专项；允许有依据地删除、合并、拆分、重构及替换错误设计，但不预设全部重写。CI 托管平台建设不自动纳入，后续单独明确。
- Q2：用户选择“是，按此优先级”。主要使用者是 YL 源码维护者、协作开发者和编码 Agent；日常模块相关回归优先，发布检查分层，native/DSP/OS 输入/IDE 等专项按需要触发，不默认每次全跑。
- Q3：用户选择“是，按此范围”。延续 Unity 2022.3/6.3 兼容合同，首轮以 Windows、2022.3.62f3c1/6000.3.27f1 验收；消除不必要的个人路径、PID 和历史接收端绑定，不自动声明其他平台/补丁版本或所有后端已验证。
- Q4：用户选择“是，按此边界”。全面覆盖测试、夹具、开发/验收脚本与探针模板、Analyzer 测试和构建部署链；Runtime 产品行为、用户 Editor 窗口/工作台、Analyzer 规则实现不默认全面重构。确需产品接缝/缺陷调整须说明，不以改产品行为使测试变绿。
- Q5：用户选择“是，采用双通道”。维护者可在当前 Editor 手动运行适用测试；自动化默认使用少量可复用/可重建隔离宿主，不接管主工程、不自动保存关闭场景、不每次新建完整工程。纯逻辑/Analyzer/格式检查可在仓库直接运行；特殊宿主测试另作分类。
- Q6：用户选择“是，允许有据淘汰”。按长期回归、集成/分发、重型/交互、历史诊断分类；保留有效合同和覆盖，允许有据合并/删除历史专用实现。旧资产兼容使用受控夹具，不为旧提交/旧目录状态检查复活环境；不得按日期、无调用或失败简单删除，也不要求测试数只增不减。
- Q7：用户选择“是，允许明确迁移”。开发/验收工具可收敛入口、参数与结果协议，不兼容所有旧 CLI/回执；同步现实调用方和说明，确有外部消费者时才设计有期限过渡。旧记录不升级改写，产品公共 API/序列化合同不因此放宽。
- Q8：用户选择“是，CLI 与原生入口为主”。日常测试一个清晰主入口；格式/Analyzer 等成熟入口可独立保留，发行/native/交互专项按职责分开；统一必要契约而非万能大命令。不默认增加综合测试工作台、可视化编排器或 IDE 插件，人工继续使用原生 Test Runner。
- Q9：用户选择“是，只做到可接入”。本批提供稳定本地 CLI、前置条件、机器可判定的结果与退出码、未来 CI 接入说明；不新增实际 CI 流水线、Runner/远端服务、授权/密钥部署，也不以无界面模拟替代交互专项。
- Q10：经对比后用户选择“是，采用第一种”。默认通过要求功能/测试与真实正常退出均通过，选择非空且预期/XML闭合，不把超时/强杀/异常退出伪装成功。已知问题允许单独限域接受并显示独立状态；不自动继承旧批一次性授权，不为严格语义无止境重跑或研究引擎底层。
- Q11：用户选择“是，采用轻量轮换”。每测试集/配置默认保留最新1次成功、最近3次失败的摘要/选集/XML/日志/必要身份与真实退出信息；完整工程、Player、Cpp/AOT、转储不默认冻结，调试或发布需要的重型制品显式保留/固定。缓存与结果分开，清理仅作用于新工具归属明确且未使用的非固定输出，不重新清理暂停的历史存档。
- Q12：用户选择“是，治理与关键补缺”。本批优先提高现有覆盖的可信性、隔离和可维护性，新增验证对应重构风险/真实缺陷/关键缺口；不默认全面扩充所有业务测试或追求固定覆盖率/用例数，源码文本断言按实际静态合同价值判断。
- Q13：用户选择“是，限域最小修复”。对直接阻挡本批目标、已明确违反现有合同的局部产品缺陷或必要内部测试接缝，先复现说明，独立小项最小修复并补回归；不引入宽泛测试后门。公共 API、序列化/数据兼容、依赖升级或明显功能扩大另行确认，其他非阻断历史问题不自动纳入。
- Q14：用户选择“是，显式选择与真实发现”。按层次/模块、程序集及测试元数据生成精确实际选集，支持预览和前置/排除原因；只维护精简规则/说明，不再逐名复制全库清单或固定数量表，不建设 Git diff 自动依赖图/智能最少测试平台。
- Q15：用户选择“是，按影响分层收口”。建立实际发现/可跑基线，区分产品/工具/夹具/环境/历史问题；每项执行相应回归，公共执行层变更扩大其所覆盖集合。最终双版运行本批日常集并验证必要集成/包裸导入/生产隔离；不默认重演全部历史native/DSP/OS输入/IDE/远端矩阵，重型仅按真实影响追加，不以静态/模拟替代运行。
- Q16：用户选择“是，先试点再迁移”。用真实现有模块先跑通发现/选择、执行、结果、夹具和输出清理闭环；试点成为最终体系的一部分，公共能力从现实重复提取，再小批迁移并明确结束新旧过渡。若试点未降低维护负担或执行成本，先修方案，不批量复制复杂性。
- Q17：用户选择“是，显式配置工作根”。Unity 自动化前由参数或本机环境配置指定工作根，预览解析路径；宿主/缓存/结果按明确归属布局，不静默选择盘符、不散建 G 盘根目录、不领养旧存档。只管理归属明确、未使用且未固定的内容。
- Q18：用户重新选择“是，按此原则复用”。U63 与 FusionHistory 纳入有界只读选材，以仓库为准、对照去重，只吸收仍有现实合同价值的独有补缺；不整体回迁、不恢复旧工程、不要求所有历史代码继续可运行，不重新清理存档。
- 磁盘与历史归档清理暂停，等待框架 v1 正式发布时另行处理。

## 设计树

```text
Q1 根目标 [确认]
├─ Q2 维护者日常回归优先；Q3 双版本 Windows [确认]
├─ Q4 开发/验证代码边界；Q13 限域产品缺陷修复 [确认]
├─ 测试体系 [确认]
│  ├─ Q5 手动 Editor / 自动化隔离双通道
│  ├─ Q6 分类及历史淘汰；Q12 关键补缺；Q18 外部素材准入
│  └─ Q14 显式选择/真实发现；Q15 按影响验证
├─ 工具体系 [确认]
│  ├─ Q8 少量 CLI / 原生入口；Q9 实际CI排除
│  ├─ Q7 明确迁移；Q10 严格结果及限域例外
│  └─ Q11 轻量保留；Q17 显式工作根
└─ Q16 纵向试点后小批迁移 [确认]
   └─ 实施已交接；执行代理/Git授权 [不包含]
```

## 事实与保护

- 已读取项目及 YL 子目录 AGENTS.md、CodingStyle.md；当前分支为 integration/xf-yl，工作区存在用户既有修改和未跟踪资产，实施前须重新核实并保护。
- 只读现状调查已完成，未运行构建/测试。主协调者已复核当前 Git 状态及代表性 asmdef/Stage9 入口；调查结果不构成全库审计结论。
- 当前 Tests 不止 NUnit：EditMode/PlayMode 按嵌套 asmdef/asmref 分节；Acceptance/Core 是可进入 Player 的运行时探针；可选集成受独立宏/程序集约束；Tests/Tools 包含已安装 C# 与模板，部分代码的默认程序集隔离仍需实际编译图核实。
- 已核实 `Tests/EditMode/YLFramework.Tests.EditMode.asmdef` 引用部分 PlayMode 测试程序集；`Acceptance/Core/YLFramework.UI.Acceptance.asmdef` 引用 InputSystem、无测试宏约束；`Tests/Tools/Editor/Stage9ReceptionTests.cs` 依赖 stage9-owner.json 与专有 CLI 参数，并保存宿主/历史/平台测试名单。
- 分组工具实际散布在 Tests/Tools、Tools/Behavior、Tools/Extensions/Data、Tools/Domain、Tools/Equipment、Tools/HybridCLR。部分入口绑定旧融合工作树/分支，不能当作主仓库通用入口；另有多套离线 runner、Unity 启动和结果逻辑，复用价值须逐项判断，不能机械合并。
- Analyzer 独立测试/构建在 Tools/YLFramework.Analyzers，格式工具在 Tools/CodeStyle；面向使用者的窗口/工作台/导出等属于 Editor 产品能力，依 Q4 不默认全面重构。日期化夹具引用和当前 Unity 编译图仍需在具体整理任务中核实。
- 补充抽样：UIPanelOfflineRunner 是多个现有脚本共用的 NUnit host，Excel 为较窄的同模式 host；SaveSafetyRunner 采用反射执行测试方法，语义不同；DomainClosureRunner 是 reflection-only 引用闭包检查器，不是测试 runner，不能机械合并。当前没有跨组公共进程 helper，HybridCLR 域内 OwnedProcess 和严格 XML/退出断言是可评估复用的成熟候选，但 owner 业务绑定不能直接泛化。
- 只读调查未发现现有 CI 定义。Analyzer 是现有独立 .NET 测试工程，SDK 9.0.312；新建 csproj 会涉及当前忽略规则及 SDK/构建属性作用域，不能为整理无依据增项目或改忽略。尚无证据表明重构必须升级依赖或变更工具权限。
- 不根据文件名、历史失败或无直接 C# 调用便删除测试、资产或 CLI；不弱化断言、退出标准或资源生命周期合同来制造通过。
- 补充事实：`Invoke-HybridCLROwnedProcess` 的成熟观察能力可评估复用，但 owner/schema/purpose、封印与 Known2022 是专项业务，不直接移入公共进程契约；其测试也须移除历史临时根绑定。
- Reddot 试点已有 `EditMode/Systems/Reddot/ReddotTests.cs` 与 `PlayMode/Systems/Reddot/ReddotLifecycleTests.cs`，由现有根测试程序集承载。无外部 fixture/owner，PlayMode 自建 Canvas/Image/Button；namespace 属于 Extensions.Domain，不能用大 namespace 过滤代表该模块。

### 外部素材复用抽样

抽样约12项，只读对照现文件和位置索引；不是全部归档审计，也未运行任何历史工具。

| 材料 | 已确认情况 | 本批取舍 |
|---|---|---|
| U63 Timer终态/GC材料 | 仓库 `TimerSchedulerContractTests.cs` 已有同主线程 delayCall 的正式回归；外部 `b10-timer-metadata1.json`、`Retained/RootFiles/z2-timer-roots-roots1.json` 是当时IL/已知持有图证据 | 直接治理仓库回归；外部诊断只供复发定位，不新增常驻GC诊断系统 |
| U63 `Retained/Tooling/Tools` 与 FusionHistory `Stage9/Y9/Tools` | 抽样为较早Stage9副本，当前仓库启动/桥接已有双版本、输入绑定等后续改动 | 不反向覆盖或重复迁入；成熟公共能力从当前仓库提取 |
| `DomainMainAcceptanceTests`、场景保护和计划写入失败注入 | 仓库已有相应场景/恢复合同，外部RED记录可解释为何存在这些断言 | 保留有效回归并改造成自有夹具；不把历史失败重计为新覆盖 |
| FusionHistory实际Player程序集快照 | `IntegrationAcceptance/MC26-20261020T173208Z/main-actual-player-assemblies.json` 有实际DLL的MVID/引用；仓库有相关审计能力，独有程度尚需定向核实 | 候选为小型程序集隔离/闭包夹具；只有当前确缺该合同才采纳，不归档整个Player作为普通单元夹具 |
| 历史owner/relocation、ToolsHistory、原始XML/verdict、一次性Check脚本 | 有出处/版本漂移/失败边界参考价值，不等于新的测试实现或长期接口 | 只取必要约束与诊断依据，不整体恢复旧身份、路径或回执编排 |
| 两份 `ReceiverInputs.zip` 内的源码 | 本轮读取ZIP的命令受当前工具策略阻止；子代理亦未能读取，未解压或恢复，因此不能断言ZIP无独有代码 | T1在获准的只读能力下核少量候选条目；目前标未核实，不为读取绕过权限或扩大全量盘点 |

吸收门槛：能指明现实合同、仓库确有缺口、版本/来源/许可证可确认、可移除历史硬编码、能以当前宿主复现。满足后才进入正式测试或最小工具；原有和已回流的测试不得重复归功为本批新增成果。

## 全局约束

- 只处理测试、夹具、开发/验收工具与必要模板/说明；CI平台、自定义综合UI、智能影响分析、历史环境恢复和存档清理均不包含。
- 遵守根/YL AGENTS 与 CodingStyle；移动资产保留 meta/GUID，核实 asmdef/asmref、反射/菜单/字符串调用及旧数据；新身份由 Unity 生成。
- Runtime/Editor/可选依赖方向和产品分发边界不变；测试不得混入普通 Player 或给生产裁剪保活。源码宿主通过不等于正式包裸导入通过。
- 保护当前 integration/xf-yl 工作区中的用户旧 Plan、ProjectSettings、HybridCLRSettings、YLFrameworkData、未跟踪夹具等输入，不认领/覆盖/回退。不对同一工程启动第二 Editor，不保存关闭主场景、不停止未知进程。
- 不默认增语言栈、外部服务、包/TFM升级、权限或忽略规则变更。现有工具链不满足则明确前置不满足，必要变更另行说明；Analyzer 默认不 Deploy。
- 外部验收存档可只读选材，不等于恢复旧环境、运行历史脚本或继续清理磁盘。先与仓库比较，已合入的以仓库为准；仅提炼独有且仍有现实合同价值的代码/夹具，不整套回迁、不把外部快照当依赖安装源，也不因保留历史价值默认纳入 Daily。
- 普通内部实现由实施者根据真实源码决定；Q13 以外的产品 API/数据/依赖变更再确认。实施与 Git/代理动作不由规划同意自动授权。

## 结构与接口

以下是要实现的新落点/接口，不是已有可运行命令；优先融合现有能力，试点只创建当前必要部分。

| 落点 | 职责 |
|---|---|
| `Tools/Testing/Invoke-YLTests.ps1` | 日常入口；`-Action List\|Discover\|Run\|Clean`，默认 List；`-Suite`、`-Module` 选择，Unity 动作支持 `-Unity`、`-WorkspaceRoot`，重型保留支持 `-KeepArtifacts`；Clean 默认预览，显式 `-Apply` 才删除 |
| `Tools/Testing/TestSuites.json` | 小型层次/模块/前置/执行器定义；起步 Quick、Daily、Integration、Special，不列全库叶名或固定数量 |
| `Tools/Testing/Common/{Workspace,Process,Results}.ps1` | 归属/宿主刷新、真实进程观察、选集/XML/结果/轮换；无 HybridCLR 业务 schema。编译公共能力仅在实际重复迁移时提取 |
| `Tools/Testing/Tests/Test-YLTesting.ps1` | 参数化临时根的工具合同回归；不引入 Pester 等新依赖，子进程/文件均自有 |
| `Tools/Testing/NUnitHost.cs` | 从UIPanel旧落点搬迁的真实NUnit host；CLI与合同已同步，不保留旧名称转发壳，不将闭包检查当NUnit runner |
| `Assets/YLFramework/Tests/Infrastructure/Editor/` | 长期 Unity 发现/宿主桥接，明确 Editor-only asmdef；源于现有 Stage9 能力，不依赖旧接收端身份或默认程序集 |
| 现有 EditMode/PlayMode/Integrations/Fixtures/Acceptance | 按合同治理与隔离，不先全库搬目录；运行时 Player 探针只在专项宿主显式引入 |
| `Tools/Testing/README.md` | 一份源码维护者入口、分层/前置、迁移、产物清理和未来CI说明；同步现有有效链接，不新造过程报告 |

数据流：仓库/配置 → 前置检查 → 显式归属工作根及兼容宿主 → 当前真实发现/选集 → 执行 → 测试结果和进程事实 → 摘要/XML/日志 → 所属结果轮换。

- List 只列配置，不启动 Unity、不创建宿主；Discover 可准备隔离宿主并预览真实选集。Run 复核输入身份，不消费旧源码/宏/依赖/测试图的选集；Quick 只允许无需 Unity native 的逻辑。
- 工作根由参数或 `YL_TEST_WORKSPACE` 指定，Unity 路径在需要时显式提供并核版本。宿主键由仓库、Unity、输入模式/可选功能组成，兼容时复用，变更时明确刷新/重建，不改变旧 owner 身份。仅安装所选集合必要代码和固定依赖，锁文件由 Unity 生成。
- 选择依程序集及 NUnit 元数据，试点使用 `YL.Module.Reddot` 类别。支持测试集说明、实际发现、前置/排除原因；不复制大叶名表，不以调用者手填 ExpectedCount 作为唯一保障。
- 新 `Invoke-YLTestProcess` 消费 ProcessStartInfo、日志路径、正数有界超时/取消；返回 PID/exe/时间/真实退出码/超时/强制清理/观察器与子进程错误。接受策略在调用层，无专项兼容号/native清单/旧授权。
- `summary.json` 最少含 action/suite/module、Unity/工具链、有效输入身份、选集引用、XML/日志路径、测试结果、进程事实及最终状态。退出码约定：操作正常完成/Run普通通过为0，执行失败1，前置不满足2，显式限域接受3；List/Discover/Clean 的操作成功不能标成测试通过。
- 发现与执行绑定参与程序集/源码、相关 meta/asmdef/asmref、宏、包/profile；拒绝过期选集、零测试、漏/多/重复、未批准skip、不完整XML及观察丢失。NUnit生命周期必须真实执行，不以反射逐个调用方法替代。
- 按 suite/module/profile 的结果键轮换普通成功1/非普通成功3（含限域接受）；结果与缓存分开。显式保留/固定制品不轮换，清理仅处理归属明确、无占用、无链接越界的未固定输出。敏感参数脱敏，不记录私钥/令牌、不改全机信任/音量/前台策略。

## 审查重点

- 输入漂移、新增/改名测试：拒旧选集，新测试被其分类发现，不能静默少跑。
- 程序集/裁剪：默认程序集、Tests/Acceptance/Editor/Analyzer不得污染生产；不同输入和可选SDK前置不能混同。
- 清理：未知/链接/使用中/固定发布基座不删除，取消/异常也有确定收尾。
- 验证层次：引用闭包不等于功能测试，Save手工反射不等于NUnit，模板/静态通过不等于Unity/native/DSP/OS通过。
- 历史合同：真正旧资产/身份兼容有夹具；不以源码字符串替代行为，不以旧目录缺失无依据删断言。

## 实施任务

推进顺序为 T1 → T2/T3 最小闭环 → T4/T5 按模块配对迁移 → T6/T7 收口。T2 不是先建全套公共层，T4 也不是先一次性移动所有程序集；每步只实现当前试点/迁移需要的能力。

### T1：有界基线与治理清单
**文件：** 当前 `Assets/YLFramework/Tests/**`、`Tools/**`；本计划附一份按模块/入口汇总的留/并/改/退表，不生成逐次大报告。
**交付：** 实际程序集、可跑/不可跑集合、宿主前置和待退入口的替代/失效依据，不假定现状全绿。
- [x] 追踪真实 CLI/菜单/反射、资产和文档调用；区分历史/特殊宿主/平台前置。用户未跟踪夹具只标保护，不当废物处理。
- [x] 按Q18及上述抽样表有界对照位置索引和可读候选；两份输入ZIP仍受既有工具策略限制，明确保留未核实且不采纳，不声称已读取或没有独有代码。准入取舍只记录本计划，不恢复工程、不绕过权限。
- [x] 记录真实发现及代表性基线，区分产品/工具/夹具/环境/历史输入问题；旧入口仍硬绑已归档根时只读分析，不恢复旧工程。
- [x] 核实依赖/工具链和程序集图；没有必要不加项目、SDK/包或忽略例外。

### T2：最小执行与结果合同
**文件：** 新 Common/Tests；参考 `Tools/HybridCLR/Invoke-HybridCLROwnedProcess.ps1`、`Get-HybridCLRReceptionScope.ps1`，同步真实调用方与测试。
**接口：** 独立进程事实、受控工作根、当前发现/选集/XML校验、结果轮换；上层不依赖旧批 owner。
- [x] 先写失败用例：exit0/nonzero、超时/取消、观察丢失、子进程收尾；XML空/漏/多/重复/skip、输入/profile漂移；路径空格/Unicode/越界/链接、未知/使用中/固定目录拒删、成功1/失败3轮换。
- [x] 最小融合已有成熟逻辑，分离 HybridCLR 身份/封印/退出接受；不搬所有历史字段到新公共层。
- [x] 在明确临时根运行纯工具回归和实际自有子进程，核真实终态、日志/清理及无个人路径；失败测试不能全靠成功模拟。

### T3：Reddot 纵向试点
**文件：** 新 CLI、TestSuites、Editor基础设施；既有 `Tests/EditMode/Systems/Reddot/ReddotTests.cs`、`Tests/PlayMode/Systems/Reddot/ReddotLifecycleTests.cs` 及相关 asmdef/asmref。
**接口：** `-Module Reddot` 的离线/Unity发现、选择、执行、结果和清理闭环；不默认改产品 Reddot。
- [x] 两类测试添加模块元数据；审视反射样板/TearDown，保留路径段匹配、来源独立、reset失效、回滚通知与错误、真实UGUI/订阅生命周期合同。
- [x] 复用NUnit host；Unity使用官方UTF；List无写入、Discover/Run只操作自有宿主，人工Test Runner亦经迁移专项实跑。
- [x] 两版真实Edit10/Play2及安全回归7/7，覆盖源码新增拒旧选集、失败/取消后同宿主恢复；公共25合同覆盖轮换/固定/使用中保护，核真实Runtime图无测试依赖。
- [x] 按同类层级比较旧入口编译输入/运行过滤与新闭包缓存、选集/结果/进程追踪及实测重跑；未重演旧入口，未捏造旧版时间或百分比提速。试点已收敛，可继续其余模块。

### T4：程序集、夹具与生命周期治理
**文件：** EditMode/PlayMode asmdef/asmref、Fixtures、Acceptance、Tests/Tools 中 `.cs` 与模板。
**交付：** 长期测试归属明确，Editor support/运行时probe隔离，历史退出活代码，自有资源确定清理。
- [x] 根EditMode不再引用PlayMode测试程序集；默认验收支持与运行时探针具备明确程序集/显式宏边界，现实菜单/反射/模板消费者已核并同步。当前真实源图三组边界通过。
- [x] 合并有真实多消费者的依赖校验，移除测试类互借；受控移动保留原GUID与序列化类型，不为当前日常恢复旧G盘/提交夹具。原有效历史合同保留，缺原上下文的集成另列。
- [x] 通过双版普通/会话组与真实failure/cancel/recovery核生命周期和恢复；追加已修通ResourceSession3、AudioSession7及两个NativeInput桌面用例。Action夹具仅释放自身新建空闲宿主；真实DSP/音频输出不因会话成功而外推。具体历史失败、根因和最终复验见执行记录。
- [x] 已按声明模块分组真实回归并追加两版正式包/普通Mono Player隔离、源工程真实图与GUID核验。重型OS/DSP/历史矩阵按Q11明确保留边界，不凭静态图外推其功能。

### T5：迁移模块工具，结束历史协议
**文件：** `Tools/Verify-UIPanel.ps1`、`Tools/{Behavior,Extensions/Data,Domain,Equipment,HybridCLR}/`、Stage9启动/桥接与相应探针模板/文档。
**交付：** 层次/模块有唯一有效入口，各旧入口有迁移或退役依据，不长期双栈。
- [x] Foundation/Core、Behavior/Data/Domain、Action/Audio及有界Resource/Excel/Save/发行已按组闭合；公共能力只融合实际重复，原闭包规则并真实产物/图检查而非假装NUnit功能通过。
- [x] Excel以两版真实可选程序集/XLSX/AssetDatabase覆盖原加载/过滤差异；Save沿用声明的.NET10专项但改真实NUnit生命周期，未升级SDK/TFM，不把native叶混入BCL。前置与未验边界已明示。
- [x] 当前主线CLI、日常组与可移植会话不依赖旧worktree/branch/PID/owner；Data独占会话复用真实自有身份校验，原主Editor/桌面/冻结接收明确不自动执行。
- [x] 当前接入的native/Resource/发行共用必要进程/路径/结果约束并有真实追加验证；未接入DSP/OS/IDE/旧性能证据保留原前置与许可证，README集中列明，不重演历史矩阵或虚构统一执行能力。
- [x] 已迁移离线组的调用方/说明/测试闭合后删除旧实现与名单，不留无期限转发；历史记录与原始来源不改报新成功。重型专项仍按其独立边界处理，非借此整体删除。

### T6：独立快速检查与说明
**文件：** `Tools/YLFramework.Analyzers/{Tests,build.ps1}`、`Tools/CodeStyle/*`、新README与现有 Architecture/UI/Attributes/Behavior 等有效说明。
- [x] 保留Analyzer/formatter独立入口并核现有SDK/路径及临时资源保护合同；原命令实际通过，未改诊断规则或Deploy。详见本轮9项formatter合同和181项Analyzer记录。
- [x] 复用真实命令：本批35文件Check、formatter安全9/9、Analyzer181/181均已实际执行；新分发器加入后完整1132文件Check也退出0。不为旧命令臆造参数，不改诊断/Deploy。
- [x] 单一Tools/Testing/README说明层次/前置、调用、真实结果、异常、保留清理、迁移与未来CI；有效UI/Behavior入口已同步，历史记录不改写，默认分发不假称包含仓库工具。

### T7：双版本分层收口
**文件：** 全部实际差异、测试集与说明；本计划一处最终验收记录。
- [x] 最终双版十模块Daily各2355叶与七模块Special各54叶通过；发现/选集/XML/声明模式/身份/普通终态均检查，整个PlayMode程序集消失也会拒绝。新增/排除原因明确，旧失败与未验范围不改写。
- [x] 两版正式导出、裸导入和普通生产图/产物边界通过；Mono、普通IL2CPP、显式HybridCLR各新Player进程实际运行两次，原生backend/metadata/热更/反射/async/Prefab及清理闭环通过。普通分发不安装探针，Unity6维护源与平台/裁剪边界明确。
- [x] 公共31、两版真实安全8/8覆盖类型身份、整组模式漏发现、失败/取消/恢复和1/3轮换；分发拒绝保护各3/3证明无请求/错误owner/交互调用不会误退出调用者。未放宽Job与普通终态，未建统计平台或虚构成本。
- [x] 一次独立只读Review完成，3项Important经RED→GREEN及双版真实验收关闭，Minor说明同步；格式1136、实际差异/源图/GUID及分阶段输入证据均核。用户配置期间的字节变化单列，不声称原四配置逐字节未变；未提交推送或创建可见会话/worktree。

## 完成与交接

- 维护者能从一个主说明找到入口、层次/模块和前置；List不启动Unity，真实Discover/Run可复现且无历史个人路径。
- 测试归属/隔离/清理和历史去留可解释，有效覆盖不被删断言/模拟替代，公共层失败路径有实测。
- 冗余/不必要入口减少；不用大抽象、材料数量或覆盖率包装成果，常见准备/执行/产物成本有前后依据。
- 支持矩阵、迁移终点、结果语义与未验范围准确；用户工程、已有改动、历史冻结和许可边界保持。
- 18项产品/使用决策已确认；逐入口退役表、外部候选核实、实际基线和运行证据由 T1–T7 产生，不在规划中虚构完成。规划阶段只改本计划，实施阶段范围与进度见执行记录。
- 用户已交接实施；执行代理、提交/推送授权不由规划默认获得。CI部署/跨平台/新依赖升级/全面产品重构/历史存档清理/全旧重型矩阵重演明确排除。

## 正式结项总览（2026-10-31）

- 统一入口、真实发现/精确选集、程序集/夹具治理、退役旧入口、受控缓存/清理、异常终态与发布分层均按批准范围交付；当前使用入口为 `Tools/Testing/README.md`。
- 最终Daily：两版各2355叶（Reddot12、Foundation414、Core463、Domain99、Behavior135、Data93、Excel41、Action373、Audio698、Resource27）；Special：两版各54叶（Foundation1、Domain2、Behavior NativeInput2、Save38、Data1、AudioSession7、ResourceSession3）。零失败/skip；NativeInput修正Unity6原生菜单阻塞后有独立重复及完整Special顺序复验，不用首次偶然成功替代稳定性。
- 当前Daily证据，相对 `G:/Unity/Testing/N26/Results`：`Tooling-Matrix-Daily-2022.3.62f3_96770f904ca7/d4deb1fb96ff4a5f9bddd1c1b1bc3693`、`Tooling-Matrix-Daily-6000.3.27f1_672d562a42a7/5d6956b60f324921833a7b5fbabc53ed`。
- 当前Special证据，相对 `G:/Unity/Testing/Results`：`Tooling-Matrix-Special-2022.3.62f3_96770f904ca7/ac96d99a2e3a4ca28f9570613cd6aef1`、`Tooling-Matrix-Special-6000.3.27f1_672d562a42a7/6f06f0cb86d74d1b9ed07d0a12097053`。2022菜单清理兼容补验的双次执行另见 `N26/Results/Tooling-UnityCLI-Special-Behavior-2022.3.62f3_96770f904ca7/62a2e12b50114d6db459a98e889fee55`；Unity6为 `...-6000.3.27f1_672d562a42a7/1d726da9e54145fbbc7d15a289a1a911`。
- 当前发布证据（下表相对 `G:/Unity/Testing`）：每版普通分发3门；三个显式运行组分别同一产物启动两次，普通exit0与后代清零。生产警告代码修改后已重验，不沿用旧Player冒充当前运行。HybridCLR原生基座由正式流水线重新生成并冻结。

| 当前发布组 | 官方2022 | Unity6 |
| --- | --- | --- |
| 普通Mono，无探针 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7/ee3aef427d4742feaec4077bd73b95b1` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7/b2edbf7a12c348a1bf0637b5ca217911` |
| Mono运行与重复 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-Smoke/f69dd166097a4c599d6a1eff3a1905e9` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-Smoke/a562ee6db27f40ce9a4589c43ef2222b` |
| IL2CPP运行与重复 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-Smoke/f701dac66fd4430fbd0a9ae73d924363` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-Smoke/6798b119b3eb4b6f889a5d246909bc30` |
| HybridCLR原生运行与重复 | `Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-HybridCLR-Smoke/cfb332b9e4584fa78a22897db8c17ae8` | `Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-HybridCLR-Smoke/89483b1ee03b4e9ca31bef8de3684d72` |

- 独立Review仅一次，同一内部会话连接重置后续接，没有第二审查者。结果0 Critical、3 Important、1 Minor；三项Important经最小修复、RED→GREEN和两版实际宿主验收关闭，Minor为Behavior Special说明，随本次同步。不以审查者只读结论冒充其运行测试。
- 当前公共合同31/31；两版实际安全8/8，含真实排除整个PlayMode asmdef的CLI失败、还原、失败XML、取消和恢复。相对N26/Results：`Tooling-UnitySafety-2022.3.62f3_96770f904ca7/6543fbc25d214ee08a294a82f2465ab1`、`Tooling-UnitySafety-6000.3.27f1_672d562a42a7/b82a0b0b73db42a49e5dd7df6f03fd86`。
- 分发拒绝保护各3/3，在实际自有Editor验证缺请求、错误owner及交互调用均返回调用者而不被Entry误退出：`N26/Results/Tooling-DistributionGuards-2022.3.62f3_96770f904ca7/e1a8cfe36d4f4dfe8ed10ee552e8ae67`、`...-6000.3.27f1_672d562a42a7/5e96107d40ff4439adaae5043c56991f`。旧实现真实缺请求exit1失败证据 `...-6000.3.27f1_672d562a42a7/10471c45bae4491ca7c4d5bff13a2a36`；该测试只调用自身新宿主，不在主工程试错。
- 生产ResourceException.Source公开API保留并明确有意隐藏；基类诊断Source独立测试通过。六处Action过时查找改为Include/InstanceID保留旧语义。低风险测试夹具/冻结协议与第三方文档警告保留，不宣称全库零警告。
- 格式1136文件Check、差异检查、有效Analyzer181历史证据分层保留。原有四配置期间字节变化及新指纹在追加记录中明示，不回退用户/并行修改。未提交推送、安装外部工具链、建立可见会话/worktree、改权限或清理历史档案。
- 未验且非本次阻塞：物理OS输入设备、DSP/音频测量、其他平台/SDK、任意AOT泛型及高裁剪、V2替换回滚、所有历史冻结协议。Unity6仅ParameterOwner维护源；CI托管部署、Git合入与发布包制作须另行明确。**本计划正式结束，不再自动追加下一阶段。**

## 执行记录（历史阶段快照）

### 2026-10-30：准入与有界 T1

- 用户明确选择新工作根 `G:\Unity\Testing`；初查该目录尚不存在。后续仅管理新工具自行创建并标记归属的内容，不设置全机环境变量、不领养历史工程。
- 已重新读取根/YL 约定、CodingStyle、`.editorconfig`、ProjectVersion 和 manifest；分支仍为 `integration/xf-yl`，既有旧 Plan、ProjectSettings 和未跟踪夹具/数据与规划保护清单一致，不作本批输入清理。
- 工具链实际存在：PowerShell `7.6.9`、SDK `9.0.312`。Unity 2022 当前主工程正在运行；Unity 6 候选 `G:\Unity Editor\6000.3.27f1\Editor\Unity.exe` 已核存在，尚未以新宿主核版本。
- 主工程只读实际编译图：Unity `2022.3.62f3c1`，查询时未编译/更新、无脚本编译失败，`YLFrameworkDev.unity` 未脏。Runtime 本身无 Tests 源文件；根 EditMode 真实依赖 Foundation/Core PlayMode；7 个 Tools/Editor 支持脚本进入 `Assembly-CSharp-Editor`；Data 的 `DataPlayerProbe`、`DataPlayerTextSink`、`DataPlayerFixture` 进入 `Assembly-CSharp`，Player 图亦包含它们。`UI.Acceptance` 和 Behavior Acceptance 也在 Player 编译图中。此图不是正式 Player 构建/裁剪结果。
- MCP RQL 发现的是方法行：YL EditMode 1937、PlayMode 891；Reddot 为 6 个 EditMode 方法和 2 个 PlayMode 方法，参数化方法尚未展开为 NUnit 叶。随后官方 UTF 异步取树请求超时，未返回可用选集，不能据此推断实际叶数或通过；不运行主工程测试、不重启/停止该 Editor。
- Context7 请求被配额拒绝；已转查本机 UTF `1.3.9` 的 `ITestAdaptor`、`Filter`、`TestRunnerApi` 及现有 Stage9 发现实现。没有更新依赖或修改工具设置。
- 两份 LocationMap 已定向读取；专用 Read 均拒绝二进制 `ReceiverInputs.zip`。ZIP 候选仍未核实，不通过其他执行通道绕过此限制；不恢复工程、不运行归档脚本、不重新清理存档。
- T1 运行基线尚未完成。Ruling: 先以已核源码/实际程序集图推进 T2 的最小自有进程与工作区合同，再由 T3 隔离宿主一次生成正式发现/执行基线，避免为基线临时搭建第二套宿主；代价是运行时缺陷分类稍后完成，故 T1 对应检查框保留未完成，试点通过前不批量迁移。

### T1 入口治理表（有界基线完成，迁移按T4/T5执行）

| 模块/入口 | 已核合同、调用方及前置 | 决定与迁移门槛 |
|---|---|---|
| Reddot EditMode/PlayMode | 根测试程序集；纯路径/事件合同与真实 UGUI 生命周期分开；无 owner/外部 fixture | 作为首个纵向试点；按类别真实发现，不用 Domain namespace 代表模块 |
| UIPanel/Foundation/Core，`Verify-UIPanel.ps1` | 仍被Behavior/DataProfiles/Domain的编译分支调用；`Documentation/Modules/UI.md`为当前入口说明。NUnit host可复用，其他Mode是辅助编译，不能当运行；SDK/模板路径固定 | 保留覆盖，试点后收敛日常入口；同步现实调用方后才退旧入口，不改写历史handover或旧Plan |
| Behavior，`Invoke-BehaviorOffline.ps1` | Pure/Legacy真实NUnit；Compile/Static与Unity/Player不同；`Fixtures/Extensions/Behavior/Usage.md`明确调用；Unity入口固定旧分支/worktree，boundary回归另引用旧编译器路径 | 保留纯合同；旧Unity接收协议不恢复；同步Usage及guard后按实际fixture迁移 |
| Data/Localization/DataAssets，`Verify-DataProfiles.ps1` 等 | DataProfiles调用UIPanel辅助编译；默认DataPlayer三类型由`DataUnityAcceptance`建资产/场景，后者又由`DataMainAcceptance`的Audit/Build调用；未跟踪Data夹具只读保护 | 分类执行与编译；探针显式安装前追踪GUID/序列化及两层消费者，不能按无菜单/无普通测试引用便删除 |
| Excel，`Run-ExcelOffline.ps1` | 脚本明确编入`ExcelOfflineRunner.cs`；独立host先加载可选程序集且固定类过滤，使用实际MiniExcel/Runtime/Editor。旧handover只有历史证据，不作为活调用方改写 | host合并前覆盖加载/过滤差异；Unity AssetDatabase/GUI仍独立验证 |
| Save，`Run-SaveSafety.ps1` | 脚本明确编入`SaveSafetyRunner.cs`；当前.NET10/10.0.2特殊宿主，反射逐方法调用不执行NUnit fixture生命周期 | 不冒称NUnit、不默认升级或切TFM；特殊前置和替代语义单独核实 |
| Domain，`Verify-Domain.ps1` | 调用UIPanel编译分支并复用NUnit host；`Verify-DomainExcel.ps1`消费其Editor编译；Tests模式只编译，`DomainClosureRunner.cs`单独编译为闭包检查器；接收工具绑MC26/主仓库 | 闭包与功能测试保持分离；同步DomainExcel实际消费者；不恢复旧MC26，日常集与主宿主合同分开 |
| Equipment | 显式 `YL_EQUIPMENT_ACCEPTANCE` Player asmdef；接收器自带包审核/构建与绑定流程 | 试点后统一必要进程/结果语义；不把导入/构建当功能测试 |
| HybridCLR | OwnedProcess 真实终态逻辑成熟；owner/schema/封印/Known2022 是专项；若干旧工具明确 legacy-only | 仅提取中性能力；native/AOT/固定发布基座不由日常入口接管 |
| Stage9/Resource/Audio/native/OS/IDE | `Invoke-Stage9Delivery.ps1`明确复制Stage9ReceptionTests源码并生成executeMethod；Representative及Binding/Optional/NativeInput/Delivery guards消费交付入口或接收源码路径。默认Editor支持类及模板与专有owner/参数耦合 | 按组迁移，保留显式安装/反射消费者或同步退役；重型专项只统一必要契约，不重演历史矩阵 |
| Analyzer/formatter | Analyzer 已锁 SDK `9.0.312`，默认不 Deploy；formatter 自有合同测试已存在 | 保留成熟独立入口；T6 跑真实命令，不重建测试平台或改诊断规则 |

### 接口核对

- T2 → T3：公共层只提供路径/归属、真实进程事实、选集/XML/结果合同；不得消费旧 owner 或 Stage9 叶名表。
- T3 → T4/T5：先证明 Reddot 闭环及收敛，再按程序集/模块实际重复提取编译能力，不把当前所有旧入口包装成永久转发。
- T4/T5 → T6/T7：仅对已迁移内容宣称新入口可用；正式包裸导入与生产隔离必须另有实际宿主证据，不能沿用源码工程图代替。

### 2026-10-30：T2 已完成，T3 接续

- 新增 `Tools/Testing/Common/{Workspace,Process,Results}.ps1` 和参数化工具合同脚本/自有子进程夹具；未修改 Runtime、包、SDK、权限/忽略规则，未创建新测试工程或引入依赖。
- 先见工作区 7 项失败、进程 8 项失败、结果 8 项失败，再实现对应能力。追加真实“父进程已退出、取消发生在子进程排空阶段”的回归，见失败后修正；JSON 日期自动转型和错误类型进程事实也见失败后修正，保留线协议字符串与严格类型校验。
- 最终新跑命令：`pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Tests/Test-YLTesting.ps1 -TemporaryRoot 'C:\Users\Kano\AppData\Local\Temp\kilo' -Group All`，**24/24 通过**。其中进程执行 exit0、exit17、真实超时/取消、实际后代进程和父退出后留存后代；XML/schema/漂移使用有界数据夹具，观察丢失用真实运行后移除自有终态回执复现，未声称实际杀死观察器测试已完成。
- 工作区实测未知/源根/越界/歧义/保留名拒绝，空间/Unicode 路径往返、真实 junction、活动租约、固定结果保护、默认预览与显式删除。轮换实际创建并清理自有结果，保留普通成功1与非普通3（含 Accepted），未知/占用/固定不消耗轮换名额；参数不写入进程身份回执。
- Ruling: 进程启动使用 Windows Job 观察/收尾当前新建进程的普通 CreateProcess 后代，不仅依赖父 PID；接受策略仍在结果层。系统安装的可执行文件/引用允许只读重解析路径，输出和清理路径仍拒绝链接。边界：Start/Job 分配不原子，失败闭合为 ObserverError；不声称支持 WMI/breakaway 启动器或被阻塞 OS Start 的硬时限。代价是这类启动器需专项入口，不可伪装成普通成功。
- 工具回归均在显式批准临时根的唯一自有子目录运行并收尾；尚未创建正式 `G:\Unity\Testing` 宿主，未运行 Unity 测试，未把这些纯工具结果算作 T3/T7。
- VS Code 重启后重新核实 Git：仅本批 Plan 与 `Tools/Testing/` 新增，用户已有修改/未跟踪资产仍在。主工程 Unity PID 42280 仍存在，不另启同工程。用户通知 Context7 配额恢复，已重新查询；当前检索库未命中 UTF 1.3.9，继续以已安装 UTF 源码和对应官方一手资料为准。NUnit 已找到明确 `v3.14.0` 文档入口。

### 2026-10-30：T3 Quick/NUnit 闭环已跑通，Unity 接续

- 正式工作根 `G:\Unity\Testing` 已由工具创建并标记归属，宿主/缓存/结果分区；工具测试和本批结果均为新归属，不领养旧输出。主工程仍由原 PID 42280 打开；最新 MCP 只读状态为未 Play、未编译，不运行或保存主场景。
- Context7 已恢复：查询了 `/nunit/nunit/v3.14.0`，但结果不足以证明 runner API；已进一步核本机 NUnit XML 文档及上游 **v3.14.0** `NUnitTestAssemblyRunner` 源码。UTF 对应官方 1.3.9 页面和已安装源码均已读取；本地 1.3.9 仍公开 `RetrieveTestList`，不根据网页描述猜用本机不存在的新 API。Unity 6 内置 uGUI `package.json` 实查版本为 `2.0.0`，未更新主工程依赖。
- NUnit host 保留实际旧 class-filter 调用，同时增加 `--yl-discover <category> <output>` 与 `--yl-run <NUnit-filter-fragment> <output>`；新输出只新建，不覆盖已有证据。发现只用真实 NUnit tree，执行仍由 NUnit 完整运行，根级清理失败也返回非零。
- 真实 host 回归命令 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Tests/Test-YLNUnitHost.ps1 -WorkspaceRoot 'G:\Unity\Testing' -Unity 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'` 最新 **4/4 合同通过**：类别/参数化发现与真实 fixture 生命周期、旧 class-filter、增加分类用例且拒旧选集、OneTimeTearDown 实际失败 XML 和退出1。证据 `Results/Tooling-NUnitHost/0b6a8c3ef0014428a048a22c95137316`。
- 接入实际编译时暴露 T2 进程返回的 generic Dictionary 不是接受层预期 NoteProperty 对象；已把真实 process fact 接到接受层的回归先跑失败，再最小修复返回对象转换，重新 **24/24 工具合同通过**。另核 NUnit 3.14 的 filter 读取需要无 XML declaration 的原生 fragment，生产者已按此生成；没有放宽结果 XML 检查。
- 新 CLI 和小型 `TestSuites.json` 已支持 Reddot **Quick**：默认 List 无写入，显式工具链/工作根，按当前源码/meta/程序集定义/宏 profile/工具链生成输入身份，原子发布轻量编译缓存，真实类别发现，精确叶名执行，严格 XML/退出闭合，拒过期及手改缺项选集，默认清理预览及结果1/3轮换。不是全面模块迁移完成。
- Quick 真实跑通原反射版 **10/10 叶**后，才把测试改为直接 public API，保留原测试名称与全部路径/来源/reset/回滚合同；用 `using` 确保纯来源句柄在断言失败时收尾，补核两个回滚观察异常。Runtime 未修改；两类 Reddot 测试添加 `YL.Module.Reddot`，现有 `.meta`/GUID 未变。
- `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Testing/Tests/Test-YLQuick.ps1 -WorkspaceRoot 'G:\Unity\Testing' -Unity 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'` 重构后 **5/5 CLI 合同通过**，内部真实 Reddot 仍 **10/10**。证据 `Results/Tooling-QuickCLI/b34ad958670549dcae1de6e86b5286cc`。这是 Unity 分发 Mono 下的纯逻辑结果，不是 Unity EditMode/PlayMode/Player 结果。
- 三个实际修改 C# 文件（host、Reddot EditMode/PlayMode）的既有 `Format-YL.ps1 -Mode Check -Paths ...` 通过，相关 tracked diff 的 `git diff --check` 通过。尚未跑完整 formatter/Analyzer；未把无文件匹配的搜索当作新文件格式通过。
- 已先写并运行 `Test-YLUnity.ps1`：当前 Daily 明确前置不满足、退出2，原因为尚未安装 Unity adapter/bridge；这是预期 RED，不是 Unity 编译/测试失败或通过。**下步**：新增 Editor-only 官方 UTF bridge 和最小可复用宿主 adapter，先 2022 再 6000 真跑发现/双模式/重复执行，补齐实际 `.meta`、输入漂移与清理验证；成功前不迁移 T4/T5。CLI 当前只实现 Quick，Daily 不能宣称可用。
- 尚待处理：为新 Tooling 回归入口也收敛轻量轮换收尾（当前回归证据为自有、无重型工程）；普通 CLI 的轮换已实测。未实施历史清理、Git 操作或独立 Review。

### 2026-10-30：T3 Unity adapter 初次运行（尚未验收）

- 新增 Editor-only `Tests/Infrastructure/Editor` asmdef 与 `YLTestEntry`，只在归属校验通过的 batch 测试宿主工作；异步 UTF 保持 Editor update loop，Play Domain Reload 后从进程内 SessionState 重新注册回调，EditMode/PlayMode 顺序执行并分开保存 XML。非 batch/非自有项目在显式退出路径外拒绝，不关闭用户 Editor。
- 新增内部 `Tools/Testing/UnityHost.ps1`，由统一 CLI 的 Daily 分派；对具名自有宿主保留 Library，按实际来源刷新已归属文件，拒外部篡改/额外代码输入及已打开的宿主。所选源码、meta、原程序集定义、profile 与桥接输入绑定发现；实际参与编译的 DLL、UPM manifest/lock 在 UTF 导入后补入有效输入身份。该最小 profile 不是完整主工程程序集图，T4 的主工程依赖治理仍待做。
- 通过主工程现有 Editor 的一次 `AssetDatabase.Refresh()` 生成了新脚本/asmdef及目录 `.meta`，未手编 GUID、保存场景或运行主工程测试。格式工具实际允许 `Check,Apply`；先前误用 `Fix` 被参数校验拒绝，随后 `Apply` 成功。
- 第一轮发现失败分别定向定位并修正：错误加入不存在的 textrendering 包（已核本机 BuiltInPackages 和实际包 JSON，移除此猜测条目）；混用 optional TestAssemblies 与显式 runner 引用造成重复引用（按 1.3.9 官方工作流保留显式引用）；NUnit/UTF 同名枚举歧义（使用实际 UTF API 枚举）。没有跳过编译、关闭诊断、改包锁或接受失败退出。
- 当前自有宿主为 `Hosts/Unity-2022.3.62f3c1-Reddot-66CA98EE4A4D`，host id `6598244bbbc54854912413381bb5f390`。最新 Unity log 已显示真实 Bee 编译成功、UTF `response.json` 为 Completed、实际版本 `2022.3.62f3c1`，实际展开 **EditMode 10 / PlayMode 2**；尚未执行或判定双模式运行通过。
- **当时在途，现已终结**：`bgp_18ceb2dff001G4f564kdZsZNIA`，命令 `Test-YLUnity.ps1 -WorkspaceRoot G:\Unity\Testing -Unity <2022.3.62f3c1 exe>`；monitor 到 120 秒上限后等待其真实终态自动唤醒。子请求为 `Results/Discover-Daily-Reddot-2022.3.62f3c1/de3ee1705fe9439aaea8ec8ee92198f5/Discover`，response 完整但进程最终 600 秒超时，force cleanup、真实退出1、后代0，不能接受为正常发现成功。曾通过 CIM 确认 PID 7464 归属，仅作诊断，未按 PID 接管它。此项不再是待等待任务。
- 此次响应的 Editor 编译图给 Runtime 提供 `UnityEditor.UI` 编译引用，且真实 `SingletonSessionState` 本就有受 `#if UNITY_EDITOR` 保护的编辑器路径。待修正 Native 合同脚本的过宽 `Editor` 名称判定：测试/接收/Analyzer/YL Editor 反向依赖仍必须拒绝，但不能把合法 Editor 条件编译当作 Player 泄漏；Player 隔离仍由 T7 实际包/Player 证据验收，不弱化为仅查名称。

### 2026-10-30：T3 验证收口与阻塞

- **Unity 6000 实测通过**：`Test-YLUnity.ps1 -WorkspaceRoot G:\Unity\Testing -Unity 'G:\Unity Editor\6000.3.27f1\Editor\Unity.exe' -TimeoutSeconds 120`，发现、双模式、重复运行 **3/3**。每轮 EditMode **10/10**、PlayMode **2/2**，普通正常退出0、未超时/强制清理/观察丢失，兼容运行复用同一归属宿主。删除临时诊断分支后的最终证据：`Results/Tooling-UnityCLI-6000.3.27/d726c466d78a4d71ac468bed8ab6f68b`。
- 实际 UTF `ITestResultAdaptor.ToXml()` 返回 `test-suite type="TestSuite"` 根，而非只含 Assembly/test-run。先增真实格式回归见失败，再精确支持该根类型；任意 TestFixture 根仍拒绝，原有完整解析、DTD禁用、叶/程序集精确对应、重复/skip/计数和真实进程门槛全保留。公共工具合同重新 **24/24** 通过，未以减少检查制造通过。
- Editor 编译图中的 Unity 自身 Editor 编译引用不等同 Player 泄漏。已按约定修正 Native 合同的过宽匹配：Tests/TestRunner/Acceptance/Analyzer/YL Editor 反向引用继续拒绝；不拒绝已有 `#if UNITY_EDITOR` 的合法 Unity 编辑器依赖。尚未做 T7 正式包/Player 证明，不宣称生产隔离已通过。
- **2022 环境侧退出阻塞，未放宽策略**：纯启动 `-batchmode -nographics -quit` 由同一中性进程层正常退出0；同一自有宿主的 API 发现、有界独立原始句柄/Windows Job 无输出捕获对照、官方 `-runTests` EditMode，以及开启图形的官方 runner 均未正常退出。官方 runner 曾真实生成 **10/10 Passed XML** 并打印 `Exiting with code 0`，但原始进程仍存活，60秒后必须回收。证据保留在 `Results/Tooling-UnityExit-2022.3.62/ecd690e610b54a04bdea1fc80b875c46`（纯启动成功）、`0d71975336e1460a9c6a1c2b398427f5`（独立无捕获）、`f29d7d599c9744f39fcecd3af411f7a1`（官方runner）、`03b90c46d77540f3b5813944835bf10d`（图形对照）。所有强制收尾都记录为失败，后代收尾为0；不是产品用例失败，也未证明具体 Unity native 根因。
- 最后一次正式 2022 发现失败为 `Results/Discover-Daily-Reddot-2022.3.62f3c1/3fa681a2975c49a7b11e2323ddb21441`：真实响应12叶、`TimedOut=true`、`forcedCleanup=true`、真实退出1、activeProcesses=0。没有把输出/日志中的退出0字样当作 OS 退出0，没有引入 Known2022 宽泛开关或调用 Environment.Exit 伪造正常退出。
- 曾尝试对齐官方 runner 的后续 update 退出；实际1.3.9的 `IsRunActive` 为 internal，编译已拒绝并修正为仅使用公开回调完成后续 update，不反射访问内部 API。6000 的实测验证覆盖这条公开路径。该调整未解决 2022 退出，不能称其为已修复。
- 临时 startup/无捕获/图形/官方命令行诊断代码已从正式 Native 回归脚本移除，只保留最小双模式/复用合同和正数超时参数；诊断原始证据保留，不建立第二套常驻进程观察器。T2 中性接受条件未降级。
- 最后复核：Quick CLI **5/5**、实际纯逻辑 Reddot **10/10**，新证据 `Results/Tooling-QuickCLI/15b5893ae7b042678beee533bb411a2b`；共享 NUnit host **4/4**，`Results/Tooling-NUnitHost/2b6178d1b1464c2da3ba2c4fa6650ff9`。四个在现有格式作用域内的修改 C# 均 `Mode=Check Files=4 SDK=9.0.312` 通过，相关 tracked diff `git diff --check` 通过。未跑 Analyzer 完整独立套件或所有旧模块，不把此复核计为 T6 完成。
- 主工程只读复核：实际 Unity `2022.3.62f3c1`、`compilationFailed=false`、未编译、未 Play；Git 保护清单仍在。Runtime 源码、主工程依赖和用户 ProjectSettings 等没有被本批更改；新增基础设施 `.meta` 由 Unity 生成。未提交/推送、未建分支/worktree、未恢复历史环境、未清理旧资产。
- **门槛与恢复**：T3 明确要求两版真实闭环，当前仅 6000 与 Quick 达成，故 T3 未完成、T4/T5 不扩散、T6/T7 不虚标。重复等待不能修复已复现的 2022 退出条件；继续需使指定 2022 环境恢复普通正常退出，或由用户明确修订版本/限域接受边界。现有工具仍默认严格失败1，不自行扩大既有 HybridCLR 豁免。T1 的剩余调用方清单/外部未核项、Tooling 证据轻量轮换、完整使用说明与迁移均明确留待后续，不宣称全系统交付。
- 收口前另以文档明确支持的 `-disableManagedDebugger` 作一次60秒有界正式回归，仍未通过（`Results/Tooling-UnityCLI-2022.3.62/976f0d56c9fd448ca2f837ff242f42f9`）。该未证实有效的参数已撤销，保留已验证的 6000 命令/源码，不积累推测性配置。没有未完成的定时等待可解除本项阻塞。

### 2026-10-30：用户批准的 Job 对照与独立官方安装

- 用户明确要求执行后续诊断；没有恢复无关迁移、Git、工作树或历史清理授权。诊断代码仅放批准临时目录，主线公共 Process/接受策略未改。
- **Job 假设未获支持**：同一已预热宿主、相同 request/日志路径/命令行先 WithJob 后 WithoutJob（两次间移存自有产物），两次 Unity 启动的 `IsProcessInJob=False`，B组未额外分配Job；观察器自身在父级Job，不能混称观察器未在Job。两组真实发现12叶，均60秒超时、必须回收、后代全部结束、无观察/清理错误、输入未变。证据 `Results/Tooling-UnityJobAB/17c184e9fe4644fa82df608290bfd9ea`。不能据此删 Job 或降低接受门槛。
- **空宿主缩小复现**：新自有 `Hosts/ExitOnly-2022.3.62f3c1` 只含一个 Editor C#探针、空 UPM manifest，无 UTF/Reddot，Unity 子进程不在Job。命令行返回配 `-quit` 正常0（`Tooling-UnityExitOnly-ExitReturn/44e05d7b3b264ccd9b18c35e8f414fd0`）；同步 `EditorApplication.Exit(0)` 正常0（`ExitSync/83d9ca0823c349b199f380c2b982319a`）；相同退出在下一 update 超时（`ExitUpdate/e4c463d00be449e8b5397ec50ed74899`）；delayCall 同样超时（`ExitDelay/8000f620436649d3b8790dd961d86706`）。`File/Quit` 菜单不存在，实际 Windows `File/Exit` 也进入关闭日志后超时，不能当修复。结论是已最小复现的异步回调退出差异，不是已证明具体 native 根因。
- 发现 Hub 中另有 `2022.3.45f1c1` 目录，但实查只有 `modules.json`、无 Unity.exe；拒绝把目录存在当已安装，也不拿失败安装作版本对照。
- **新增明确授权**：用户在问题中选择“新增官方对照（推荐）”，允许下载并独立安装一份官方2022.3 Editor到 `G:\Unity Editor` 新目录；只装Editor，不覆盖现有版本、不切换/升级主工程、不增平台模块。之后在新自有宿主跑空探针，再验证Reddot；未授权把新版本结果自动冒充原 `f3c1` 的通过。
- 官方 `services.api.unity.com/unity/editor/release/v1/releases` 已核 `2022.3.62f3`，revision `96770f904ca7`，无安全警告；Windows x86_64 URL 为 `https://download.unity3d.com/download_unity/96770f904ca7/Windows64EditorInstaller/UnitySetup64-2022.3.62f3.exe`。同API的f1有安全警告，未选。下载约3.695GB、安装约6.474GB；实查G盘约125GB空余，批准目标 `G:\Unity Editor\2022.3.62f3-official` 不存在。
- 下载与校验用一次性脚本 `C:\Users\Kano\AppData\Local\Temp\kilo\yl-editor-compare-install.ps1`，归属缓存 `Cache/OfficialEditor-2022.3.62f3`。先核官方MD5和有效Unity Authenticode签名并记SHA256，再允许安装。安装依Unity/NSIS官方规则 `/S /D=<最后且不加引号的可信路径>`，不通过Hub改全机安装路径。若系统要求额外权限，不能绕过。
- **当前在途**：`bgp_18d4a627a0013DLf92ScG5YjH4` 正在执行上述脚本 `-Stage Download`；monitor120秒到上限仍运行，下载有3600秒界限，尚未安装。不要重复启动下载或轮询；使用原进程终态继续。下载完成后同脚本 `-Stage Install`，再以 `-EditorVersion 2022.3.62f3` 在临时探针执行器中创建独立 ExitOnly 宿主对照，不改现有工程 ProjectVersion。

### 下载续传更新（旧进程已被本节替代）

- 用户反馈下载缓慢：69.38%后近期85秒几乎停滞。仅停止自有下载 `bgp_18d4a627a0013DLf92ScG5YjH4`，保留已下载字节；旧等待cron `wku_18d53c2be0013qduR75vOtt36A` 已取消。增加30秒低于32KiB/s自动断点重连后，新单连接 `bgp_18d599bd3001oggHixUiqlaxg4` 虽恢复，但近期仅约0.15MB/s，也已停止，均未执行安装。
- 官方同一URL的HTTP HEAD确认 `Accept-Ranges: bytes`、Content-Length **3695061288**（API advertised size近似值为3695060992），ETag与官方MD5一致；单字节Range实测 **206 / 1 byte**。国内官方CDN同版本路径404，未用不同版本或不明镜像。
- **最新在途唯一下载**：`bgp_18d63c00d001QRKnkp52fZ0rxU`，脚本 `C:\Users\Kano\AppData\Local\Temp\kilo\yl-editor-range-resume.ps1`。保留原前缀 **2638139392 bytes** 并记SHA256，使用现有curl从同一官方URL四连接仅下载余下四段（无新依赖/全局代理改动）。归属缓存中的 `range-plan.json` 绑定前缀、总长、段范围；每段必须长度和Content-Range完全匹配后才拼接，拼接前重核原前缀SHA256，最终必须官方全文件MD5一致。异常保留已完成段，不重下原前缀。
- 最新实际采样 2026-10-30T15:19:40Z：新增分段约147MB、合计约2.785GB、**75.38%**，明显恢复增长；不据单点捏造瞬时速度或ETA。monitor60秒后仍运行，继续等同一进程终态，不重复启动或沿旧ID重试。成功后回到 `yl-editor-compare-install.ps1 -Stage Download` 做最终Authenticode/SHA256校验（已完成exe不再下载），再 `-Stage Install`。
- **再次续传，替代上一ID**：`bgp_18d63c00d001QRKnkp52fZ0rxU` 已按有界重试结束失败，分段1/2/3各264230474字节完整；分段0仍有21007586字节匹配原服务器范围的有效前缀。旧等待cron已删除。临时分段脚本已改为仅请求 `segment.start + retainedLength` 起的余量，在单独tail中核准确Content-Range后才追加；超时已收到的有效前缀也保留，不再丢弃失败段已收到的字节。当前唯一进程 **`bgp_18d82a21c001zdNx2W0geIcd73`** 正请求 `2659146978-2902369865`，其余完成段不重下。完整官方MD5/签名门槛不变，尚未安装。

### 官方对照下载完成，独立安装进行中

- `bgp_18d82a21c001zdNx2W0geIcd73` 已正常结束：最后范围206完成，拼接前原始前缀SHA256及完整官方MD5均匹配。完整installer文件3695061288字节；下载等待cron已删除，不再等待/重试旧下载。
- `yl-editor-compare-install.ps1 -Stage Download` 已正常结束，`Cache/OfficialEditor-2022.3.62f3/verification.json` 实证：Authenticode **Valid**，签署者 **Unity Technologies SF**；MD5 `2e9ec527a1329e0d72c3454047f37d24`，SHA256 `62B891A02C1FBE44E22621E2B7E485D2E324A500A63610E50C601AD6D9A49EE1`，revision仍96770f904ca7。
- 未提升的官方安装调用被Windows以“请求的操作需要提升”拒绝，无安装进程产生。随后只通过正常 `ShellExecute/runas` 请求系统UAC确认，不绕过/禁用UAC、不改权限策略；此流程启动了已验证安装进程 PID8056。
- **最新唯一在途安装** `bgp_18d976af5001GhtITP8uDytnzO`，临时脚本 `-Stage Install -RequestElevation`；monitor120秒仍运行，目标仍为全新 `G:\Unity Editor\2022.3.62f3-official`。等待该原始安装进程终态，不能重复安装或把文件已出现当完成；结束后读取installation.json、实际Unity.exe元数据，再做独立空宿主及Reddot验证。

### 官方 2022.3.62f3 对照完成（不改写原 c1 验收）

- 官方安装进程已终结，`installation.json` 为真实 **exitCode=0**，结束时间2026-10-30T16:18:11Z；实际 `G:\Unity Editor\2022.3.62f3-official\Editor\Unity.exe` ProductVersion **2022.3.62f3_96770f904ca7**，与原 **2022.3.62f3c1_1623fc0bbb97**、6000的 **6000.3.27f1_672d562a42a7** 分别核实。未覆盖、切换或升级主工程/原Editor，没有增加平台模块。
- 新自有 `Hosts/ExitOnly-2022.3.62f3` 的正常命令行返回/quit（`Tooling-UnityExitOnly-ExitReturn/899cbc82c45a4414a0e33694dfd02704`）、update异步退出（`ExitUpdate/8b034449e7fc4e469d9efcaacd6c65b1`）、delayCall退出（`ExitDelay/616964cd6584466387b7fa9303c897a7`）均为原始句柄观测的正常退出0，无Job分配/强制回收。与c1最小复现形成发行构建差异证据；不推断未经验证的具体native缺陷，也不反向认定Job是根因。
- 工具原先只按前三段文件版本匹配，无法区分同为2022.3.62的两个构建。先加入并见失败的精确选择回归，再最小增加 `Get-YLTestEditorProfile`，按 **fileVersion+完整ProductVersion/revision** 唯一匹配。配置保留原c1和6000，新增官方f3；未知revision、重复配置均明确拒绝，不靠目录名/猜测选择。公共工具合同现 **25/25** 通过，默认接受条件未降级。
- 官方f3首次真实Run父进程已正常退出0，但后代未在原2秒窗口自然退出，生产者按 `DescendantsAlive/forcedCleanup` 正确拒绝；保留 `Run-Daily-Reddot-2022.3.62f3/8cfabd9319ac4b46ab596c829f33ad41` 和 Tooling `7e7b458dfa7a400b9b292306b12d235e`。临时原始句柄/后代跟踪将自然等待窗口设15秒作**独立诊断**后，WithJob和WithoutJob均实际正常0、无强制、无观察错误（`Tooling-UnityJobAB/720c7c3dac8e49539896345b58180b80`）。生产Common仍保留原2秒及强制清理拒绝，未将此失败改报通过；冷启动后代收尾时序仍有剩余风险。
- 随后用**未放宽的正式CLI**再次跑官方f3：`Test-YLUnity.ps1 -WorkspaceRoot G:\Unity\Testing -Unity 'G:\Unity Editor\2022.3.62f3-official\Editor\Unity.exe' -TimeoutSeconds 120`，发现、EditMode/PlayMode、重复执行 **3/3通过**；每轮 **10+2** 真实叶、精确XML、正常退出0，无超时/强制/观察错误，复用新官方宿主。证据 `Results/Tooling-UnityCLI-2022.3.62/670dcabe8ec742158efc1f221ddc53cf`。
- 版本选择改动扩大到既有消费者，已实际回归6000同命令 **3/3**（每轮10+2，`Tooling-UnityCLI-6000.3.27/38e40ae8229c43e1967a3857faedfef0`），原c1的Quick仍 **5/5**（实际纯逻辑10/10，`Tooling-QuickCLI/b1678dee5ebd4dd08df743e3948dab70`），未知/混淆版本单测拒绝；未为确认c1已知退出失败再重演长超时。相关 tracked source `git diff --check` 通过，Git保护清单仍在，Runtime/主依赖/用户设置未改，无提交推送或worktree。
- **本次狭窄目标已交付**：已授权的官方安装可用，提供一个实测正常退出的2022.3运行路径。现可显式用上述官方Unity.exe执行Quick/Daily；不要将这当作原 `f3c1` 已修好，或自动把原批准的双版本矩阵替换为新版本。原Plan其余治理/正式包/Player/T4-T7没有随本次对照完成，任何矩阵验收范围调整需保持明确边界。没有待下载/待安装的定时任务，安装包与诊断证据保留在自有工作根。

### 主工程迁移与旧 Editor 退役授权（2026-10-30）

- 用户要求主工程换版本并删除旧安装，随后明确选择 **官方2022.3（推荐）**：迁到已安装并核验的 `G:\Unity Editor\2022.3.62f3-official\Editor\Unity.exe`。先保护当前未提交输入和必要设置，正常关闭旧主工程实例，导入/验证通过后才处理旧 `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1`；不得删除共享Hub、许可、其他Editor，不先破坏回退入口。此授权覆盖本次必要主工程版本切换，不扩展Git或全套T4-T7实施。
- 当前主工程 ProjectVersion 仍是c1；旧Editor PID42280仍在运行，旧Unity.exe仍存在，不把“之前编辑器删了”的口语视为已确认完成。场景 `Assets/Scenes/YLFrameworkDev.unity` 真实 `isDirty=false`、未Play、未编译；Prefab stage不脏，Assets下无未保存持久资源。Builtin和Packages着色器/资源的dirty不作用户数据保存，不调用SaveAssets或重序列化。
- 实查包声明不一致：manifest写URP16.0.6/uGUI2.0.0，packages-lock与PackageCache实际为URP14.0.12/uGUI1.0.0；Unity官方URP16兼容表最低2023.2。用户目标选择描述已明确 **保持当前实际URP14.0.12/uGUI1.0.0并对齐声明**，不顺手升级渲染管线；锁文件交Unity更新，不手改伪造。其他包/Runtime/已有用户修改保留。
- 迁移前应留自有备份与初始diff/输入清单；正常关闭仅限已确认的主工程旧Editor。新Editor只开主工程一次，验证实际版本、UPM/编译及场景状态；必要时处理已明确的包声明，不能用隔离Reddot结果代替主工程验收。旧安装目录目前根部没有Uninstall.exe，退役时需查真实卸载入口或精确删除授权目录，不猜路径、不扩大删除。
- 迁移快照已独立校验：`Cache/MainMigration-20261030T171210-3dec7b78/project-inputs.zip`，包含Assets/Packages/ProjectSettings当前 **3778文件、49862739源字节**，压缩24142628字节；逐ZIP条目回读SHA256/长度对应清单，并记整体SHA256、初始status/working/staged差异。备份包含未跟踪输入，不含Library/Temp；未创建提交。
- 关闭前再次MCP查证主工程实际c1、未Play/编译、dirtySceneCount=0、dirtyPrefab=false、dirtyProjectAssetCount=0；随后对确认的原PID42280及原始可执行身份调用正常 `CloseMainWindow`，返回True且30秒内真实退出，未强杀、未保存Packages/builtin dirty数据。
- 旧实例结束后仅手改ProjectVersion两行到官方f3/revision96770f904ca7，manifest仅对齐URP14.0.12与uGUI1.0.0，锁文件未手改。**当前新主工程在途**：`bgp_18dcf79d0001vbiTDr1x2f1e8L`，以新官方Editor交互打开本仓库一次，persistent以免会话结束关闭用户主工程；日志在上述备份目录 `official-main-editor.log`。等待首次导入再核实际版本、UPM/编译/资产；旧安装尚未删除。
- 首次主工程启动在UPM阶段失败：上述日志显示 `com.unity.burst: aborted`、退出码1，尚未进入脚本编译；随后新官方进程PID41104（父PID37648）使用默认Editor日志重启，显示 `Corrupted Library Detected`。现有日志没有ArtifactDB/SourceAssetDB损坏证据，不把该通用提示当成整个Library已坏的结论。恢复前日志保存在同一快照目录 `before-recovery-Editor.log`、`before-recovery-upm.log`。
- 经核对进程可执行路径、项目参数、弹窗及准确按钮，选择 **Don't rebuild Library**。此按钮实际继续UPM而非退出；临时恢复脚本的30秒退出预期不成立，不代表点击失败。**未执行Rebuild、未移动检测标记、未清空或重建任何Library内容**，旧Editor保留。UPM于2026-10-30T17:33:46Z开始从 `https://packages.unity.com` 下载Burst1.8.29；官方元数据端点可访问，确认该版本的官方tgz及SHA1，但包下载/主工程导入尚待完成。
- Unity已自动重写包锁：官方registry URL由 `.unity.cn` 变为 `.unity.com`，URP/uGUI落为14.0.12/1.0.0，并有依赖排序与splines depth变化；没有手改锁或顺带改包版本。待解析完成后再与迁移快照逐项核对，不据锁文件生成就宣称主工程验收通过。
- Burst官方tgz为490454035字节，官方元数据SHA1为 `6BB9ACA3EF382E98BD0C1E4AB5FC74380C16095F`。本机原 `.unity.cn` 缓存的同版tgz长度及SHA1完全匹配；`.unity.com`的新下载至17:42约49421943字节，仍有进展而非已证明损坏。拟在UPM完全退出后复用该已校验tarball，不改manifest版本、不写Library包源码或关闭签名校验。普通 `CloseMainWindow` 后进度窗口消失，但原PID41104及其UPM42180在30秒内尚未退出；保护检查已拒绝复制，**缓存未被修改、未强杀、未再开同工程**。继续等待现有下载/正常收尾，不把等待当作已完成或阻塞。
- 17:58:15Z上述下载最终 `aborted`，显示UPM Error；另有现有Burst目录重命名 `EPERM` 警告，定向进程检查未发现bcl/ILPP残留。18:05选择已核实弹窗的 **Quit**，原PID41104在30秒内正常退出，UPM42180也已退出。仅在确认无占用后，将现有且匹配官方SHA1的tgz复制到缺少的 `.unity.com` tarball位置，未覆盖已有包；复制后SHA256为 `5A7D6DF652BD5842D3964327C321CDE743D971FD0A0BBFD817E7E8FE0FEF48D5`，回执 `burst-cache-reuse.json`。未写项目Library或任何签名状态。
- 随后仅重开官方主工程一次：当前persistent进程 `bgp_18dfd87d7001Rbn09WIFdC7wuG`，实际Editor PID41836，日志 `official-main-cache-retry.log`。相同恢复提示再次出现，已核身份并再次选择Don't rebuild，未全量重建。仅有tgz的条目未被UPM视为完整缓存，仍启动正常下载；18:08新临时文件约40MB，未伪造 `signature` 的Valid状态或跳过UPM校验。继续等此下载的真实结果，不宣称缓存复用已解决主工程导入。
- 上述重试于18:15:00Z再次下载aborted，并重现Burst目录清理EPERM；18:24通过准确Quit按钮正常结束PID41836及其UPM31036。目录为普通目录且现有ACL允许当前用户修改，没有修改ACL或停止未知进程。
- **缓存判定更正**：定向读取本机UPM6.1.5的 `isPackageInNpmCache` / `cacheAdd` / `copyToGlobalCache` 真实源码，缓存命中要求是tarball与 `package/package.json` 同时存在，**不是signature状态文件也必须存在**。此前只复制tgz必然无法命中；signature由后续正常解压流程调用实际验证器生成。现已在无Editor/UPM时从 `https://packages.unity.com/com.unity.burst` 取得1.8.29真实元数据，复核name/version/dist URL/SHA1与现有tgz一致，仅补缺失的全局缓存manifest，回执 `burst-cache-metadata.json`。未复制旧签名结果、未写Valid状态、未改安装源代码/项目Library或全机设置；待下一次正常UPM导入验证假设。
- **真实主工程验证进展**：最新启动 `bgp_18e0fbc69001qyQenC1YMR6jEg` / PID49116，日志 `official-main-complete-cache.log`。18:27选择Don't rebuild后，UPM **9.50秒解析成功、89包注册成功**，没有再下载Burst或出现本轮EPERM；Burst全局缓存的signature文件由UPM实际生成，status=Valid。Unity自动按引擎hash更新Bee缓存，真实1739项脚本图构建成功（31.27秒），初次Asset Pipeline Refresh完成（49.190秒）。实际MCP确认 `Application.unityVersion=2022.3.62f3`、新Editor路径、未Play/未编译/未更新、`scriptCompilationFailed=false`，不是dotnet或隔离宿主替代证据。
- 主工程实际包版本：URP14.0.12、uGUI1.0.0、Burst1.8.29、Cinemachine3.1.6、Timeline1.8.12、HybridCLR9.0.3。当前 `Assets/Scenes/YLFrameworkDev.unity` 已加载、不脏、缺失脚本0、无PrefabStage。`main-editor-validation-before-restart.json` 保存本轮只读现场及全部89包版本。
- **主工程实际回归**：最初同时指定namespace/class时，MCP组合filter实际运行整个Domain namespace，结果85项中83通过、2失败；失败为 `DomainTwoPlayTests` 两方法分别在第47/91行的Disable Domain Reload前置断言，当前 `enterPlayModeOptionsEnabled=false`，未更改该用户设置或弱化断言，不据此宣称该专项验收通过。随后使用精确RQL fixture选择，Reddot **EditMode10/10、PlayMode2/2** 均通过，结束后场景原样恢复、不脏、无缺失脚本且未Play。上述12项是在主工程实际执行，不复用隔离结果。
- **输入保护验收**：对迁移前3778文件逐一SHA256核对，3774文件完全相同，无新增、无缺失；仅 `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectVersion.txt`、`ProjectSettings/PackageManagerSettings.asset` 四处为预期迁移变化。包锁相对真实快照仅两项批准版本及官方registry URL变化，没有其他包版本/hash/dependencies漂移；此前splines depth/排序描述来自与HEAD对比，不代表本次新增变化。`input-audit-after-tests.json` 保存明细；Assets、用户ProjectSettings.asset及未跟踪夹具未变化。
- HybridCLR依赖核实：设置为 `useGlobalIl2cpp=false`，本地完整副本位于 `Library/HybridCLR/LocalIl2CppData-WindowsEditor`，安装版本标记9.0.3，未见reparse point或旧c1配置路径引用，保留原数据不重新安装。EditorPrefs定向SDK/NDK/JDK/Gradle路径键未设置，相关进程环境/User/Machine环境未发现旧安装路径。旧安装包含Android/UWP/WebGL及Windows模块，新官方仅Editor基础支持；**未执行Player构建或HybridCLR原生构建**，不能将Editor验证写为这些目标通过。
- 残余诊断：原有代码/包有warning；本轮Console还记录在线包搜索 `ECONNRESET`，与UPM resolve200/已安装包完整/编译成功区分，不改代理、防火墙或信任策略掩盖。旧版本真正卸载器现已定位为 `C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Uninstall.exe`，产品名Unity2022.3.62f3c1且Authenticode Valid；HKLM卸载注册项精确匹配此版本和路径。尚未执行卸载。
- 主工程全部场景、Prefab及Assets持久资源脏数量为0后，已向PID49116正常CloseMainWindow，30秒内实际退出0，没有保存/强杀。下一步仅重开同一官方Editor，确认恢复弹窗不再出现，再满足退役顺序。
- **干净重启验收通过**：`bgp_18e1ac2a9001u237qBDGo29GUW` / 实际PID36372，日志 `official-main-clean-restart.log`。没有人工处理恢复弹窗即直接进入主工程；1739项图增量构建成功（0.55秒），初次Refresh11.665秒，89包正常。实际MCP再次核对官方ProductVersion `2022.3.62f3_96770f904ca7`、无编译错误、未Play/编译/更新、原场景不脏且缺失脚本0；本次Console Error/Exception/Assert查询0项。`main-editor-clean-restart.json` 为重启现场证据，不据此推断在线搜索服务长期稳定。
- **旧Editor已精确退役（2026-10-30T18:46:50Z）**：标准UAC后调用上述经签名/产品版本/注册项验证的真实卸载器 `/S`，工作目录在自有备份而非旧安装内。第一次启动包装器的PowerShell路径解析问题发生在启动前，未执行卸载；修正后正式卸载已实际移除旧Unity.exe、卸载器及HKLM旧版本项，但其提升包装进程以 `-1073741510` 结束，**未将该异常当作普通退出0，也没有伪造卸载器退出码**。
- 正式卸载后独立检查发现仅残留原版本目录树和4个文件、共20548字节。对照原签名卸载前记录/模块清单，核对规范化精确根目录、全部祖先与残余内容无reparse point、无使用旧路径的进程、无范围外内容；先WhatIf，随后标准UAC仅清理 `C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1`。收尾进程实际退出0，最终确认整个旧根目录、旧Unity.exe、旧版本注册项均不存在。没有手动清理任何Hub、许可、其他Editor或历史工作区；未修改权限策略。真实卸载器退出码未知，最终依据实际目标/注册项检查验收，见 `old-editor-retirement.json`、`old-editor-retirement-preflight.json`、`old-editor-remnant-preflight.json`。
- **退役后保护复验通过**：新官方与现有Unity6000.3.27f1的Unity.exe SHA256均与卸载前一致，新官方注册项仍存在，主工程PID36372及受保护Hub服务PID27960仍在运行。`main-editor-after-retirement.json` 现场确认官方版本、89包、原场景不脏/缺失脚本0、无编译失败、旧根不存在。再次核对全部3778输入仍为3774相同、仅原四项预期变化、无新增/缺失；相关四个配置文件的 `git diff --check` 通过，没有新增产品代码修改或Git提交。
- **本专项完成**：主工程已切到官方2022.3.62f3并保持打开，恢复弹窗经正常重启确认不再出现，旧c1安装已退役。原T1–T7全量治理状态不因本专项自动勾选；Domain Reload专用配置下的两项用例、Player/各平台模块及HybridCLR原生构建仍按前述边界未验收，不扩大到T4–T7，不创建Commit/Push/代理/独立Review。
- **旧父目录追加清理授权（2026-10-30）**：用户随后明确要求清理 `C:/Program Files/Unity`。只读检查确认其中仅剩2022.3.45f1c1与6000.0.27f1c1两份旧Hub模块清单，以及6000.0.25f1c1等空目录，没有Editor/SDK/构建工具、活动进程或卸载注册项使用该根路径；不把前一轮仅限62f3c1的授权静默扩大。
- 两份清单合计31654字节，已复制到现有自有备份内 `legacy-unity-root-20261030T1851/` 并逐项SHA256核验，`preflight.json`保存文件清单及受保护Editor哈希。再次核对精确根路径、全部祖先和子项无reparse point且没有新内容，先WhatIf，再以标准UAC仅删除该旧父目录；2026-10-30T18:53:13Z实际清理进程退出0，`result.json`记录RootExists=false。
- 清理后独立复验：整个 `C:/Program Files/Unity` 已不存在；G盘官方2022与Unity6000.3.27f1程序哈希保持不变，主工程与Hub服务继续运行。实际MCP确认主工程仍为官方2022.3.62f3、无编译失败、未Play/编译/更新，原场景不脏。未清理任何G盘安装、许可、项目数据或其他历史材料，未修改权限策略，未提交Git。

### 2026-10-30：恢复原T1–T7

- 用户明确要求接续原任务，仍沿用本计划18项决策，不新建执行体系/工作树/代理/Review或Git动作。Ruling: 当前前瞻性双版本基线采用用户已选择并完成迁移的官方2022.3.62f3与6000.3.27f1；原c1 profile身份和失败历史保留，但不重装已退役Editor，也不将官方通过冒称c1通过。代价是c1不属于本机后续可执行验收矩阵，其他补丁版本仍不自动受支持。
- T1有界基线收口：已补核当前脚本/模板/反射消费者及有效维护说明，治理表更新在原位置。Ruling: 两份已受策略限制的ReceiverInputs.zip继续明确未核实、不采纳，不为准入调查绕过工具策略；这表示未确认其中独有内容，而非断言没有独有内容。现有可读候选的仓库去重与合同取舍沿用已核证据，不复活历史工程或重新清理归档。
- T3实际剩余缺口：`Test-YLUnity.ps1`目前只有发现/双模式/重复三项；新增真实失败、取消与宿主恢复/源码漂移的隔离回归，而非重跑未变的主工程。普通CLI已有1/3轮换，但三个Tooling回归入口结束后仍未轮换；公共轮换能力仅在这处真实重复下最小提取，不降低进程或XML接受条件。T3闭环及成本比较收口前不扩散T4/T5。
- T3轮换RED→GREEN：先把现有真实目录/lease/固定证据轮换用例改为经统一执行入口，Results组因缺少该入口明确1/8失败；随后最小提取 `Invoke-YLTestRotation` 并接入普通CLI/Unity及三个Tooling收尾，完整公共合同重新实跑25/25。未改1个普通成功/3个非普通结果策略、Windows Job、2秒后代收尾或XML规则。
- T3精确profile RED：新增 `Test-YLUnitySafety.ps1`，仅在自有Cache内维护有输入哈希收据的Reddot源码夹具与独立工作区，不复制完整工程、不接触主场景或归档。先运行真实NUnit合同后，消费其结果的独立断言因Tooling使用通用Mono/文件版本前缀桶而失败，证据 `Results/Tooling-UnitySafety-2022.3.62f3_96770f904ca7/25ca6b33734e40d1a2c62afec1103f8b`。据此将NUnit/Quick/Unity三个Tooling组键及profile改用真实ProductVersion，下一步实跑完整安全回归。Ruling: 不领养/重命名旧通用桶，也不对其追加轮换，从而保留原c1及历史诊断；仅新产物使用精确Editor身份分组，避免两版互相覆盖。
- T3官方2022安全回归GREEN：`Test-YLUnitySafety.ps1 -WorkspaceRoot 'G:/Unity/Testing' -Unity 'G:/Unity Editor/2022.3.62f3-official/Editor/Unity.exe'` 实际7/7，证据 `Results/Tooling-UnitySafety-2022.3.62f3_96770f904ca7/df5af30c9ff448d98abd29c72f266ccf`。其中重新实跑NUnit合同4/4、Quick5/5、Unity3/3并验证精确profile桶；受控源码增加一个真实分类叶后发现数量+1、旧选集在执行前拒绝；注入的真实UTF失败保留Failed XML与普通Editor exit1；真实Editor取消保留Cancelled/forcedCleanup=true/activeProcesses=0；恢复源码后同一宿主再次Edit10/Play2正常通过。Tooling通过是这些反例合同通过，**不把注入失败或取消改报模块成功**。
- T3成本核对按可比层级记录：旧Domain Pure依赖另行生成的整Runtime DLL，并每次编译Skill/Buff/Mission/Reddot/Logging五模块测试，类过滤只影响运行；新Quick按Reddot声明闭包生成/复用哈希缓存，结果含真实discovery与XML/进程校验。官方2022本轮父进程观察NUnit合同约2.78秒、Quick合同约9.10秒、含首次宿主导入的Unity三合同约52.92秒，取消后同宿主恢复约17.74秒。旧入口本轮没有在未知旧输出上重演，**没有旧版耗时基线或百分比提速结论**；Native比纯逻辑承担额外Editor启动成本，以换取真实UI生命周期验证，不能拿两者时间混比。后续T5结束旧入口须保留其全量模块覆盖，不把试点闭包当完整Runtime验收。
- T3 Unity6000.3.27f1同组安全回归亦实际7/7，证据 `Results/Tooling-UnitySafety-6000.3.27f1_672d562a42a7/416a7707beb142a6bcd40d5ede946f04`，底层NUnit4/4、Quick5/5、UTF3/3及失败/漂移/取消后同宿主Edit10/Play2均重新闭合。两版使用原公共Job/超时/XML策略，无主工程测试或额外Editor接管；`git diff --check -- Tools/Testing`通过。T3按当前矩阵收口，接续T4分组程序集/夹具治理；旧c1及首次冷启动失败不因本结果改写。
- T4第一组真实图RED：只读MCP采集当前已编译Editor及PlayerWithoutTestAssemblies图至自有Cache `AssemblyLayout-2022.3.62f3/before.json`；新 `Test-YLAssemblyLayout.ps1 -Group EditMode` 准确报告根EditMode依赖Foundation/Core PlayMode两处失败，不以源码字符串或假程序集替代实际图。当前图另外确认8个Editor和4个Runtime验收源码仍在默认程序集，生产图另有UI/Behavior Acceptance；这些是后续组尚未完成的问题，不能因本组修复而宣称生产隔离通过。
- Ruling: 序列化组件只由Editor序列化/Prefab测试使用，移入根EditMode；InstanceLifecycleProbe虽主要供Resource EditMode测试使用，但其Prefab/Bundle需要运行时可序列化类型，不能一并塞入Editor程序集。它与四个Core烟测脚本进入单一 `YLFramework.Tests.Fixtures` 共享测试程序集，保留Runtime/UI能力并以UNITY_INCLUDE_TESTS/TestRunner引用/autoReferenced=false隔离。根EditMode、Core PlayMode、Core Fixture Editor改引该夹具程序集，UIPanel旧CorePlay辅助编译的真实源码消费者同步补Shared输入，未新建长期转发层。
- 本组6个脚本与各自.meta成对移动，类型全名不改，未改Runtime产品API；六处Core prefab/scene引用仅依原脚本GUID，定向资产搜索未找到旧PlayMode程序集AQN绑定。新Shared目录/asmdef身份由Unity导入生成，尚待核GUID与原夹具加载。已正常AssetDatabase.Refresh，返回isCompiling=true、compilationFailed=false，等待真实编译后重采图验GREEN；不运行/保存/关闭用户主工程场景。本组功能回归仍需后续隔离完整模块宿主，不能拿Reddot切片替代。
- T4共享夹具图GREEN：真实Unity重新编译完成、compilationFailed=false、原场景clean且未Play；`after-shared.json`对应的 `Test-YLAssemblyLayout.ps1 -Group EditMode` 实际通过。6个原GUID均经AssetDatabase查回新位置及原完整类型名，四个Core View/Panel prefab缺失脚本0，共享程序集不在PlayerWithoutTestAssemblies图。新Shared文件夹/asmdef.meta由Unity生成；这仍不是功能或Player构建通过。工作区同时出现ProBuilder Settings.json额外修改，未把它归成本组手工改动、未还原或覆盖。
- T4/T5 Foundation配对RED：将现有Unity三合同入口参数化目标模块，并按目标模块+精确Editor分桶，默认仍为Reddot；无手写叶子名单。Foundation首次调用明确返回 `No defined Daily selection for module Foundation`，证据 `Results/Tooling-UnityCLI-Foundation-2022.3.62f3_96770f904ca7/5b6e77328d1c422a9d2a86e8ba90281b`。接着最小扩展原宿主：源目录可纳入哈希与同步；Foundation采用整Runtime/Editor真实源及现有BouncyCastle、资源Sample/夹具，而不是切片重写asmdef；UTF按真实程序集元数据选择未分类的既有Foundation用例，category与程序集选择必须恰选一种。包前置仅沿用源锁中的Json.NET和源manifest已有内置模块，不改主工程依赖。功能GREEN待实际隔离宿主编译/执行，不提前勾T4/T5完成。
- Foundation整源码首次已真实编译及发现双模式；执行EditMode **357/366、失败9、跳过0**，保留普通exit1/无强杀/activeProcesses0及原Failed XML，未报成功。证据 `Run-Daily-Foundation-2022.3.62f3/7551f68df69b42f58cd7fcc0f73fdf79`。其中6项确需图形设备，2项直接显示Editor-only MonoBehaviour AddComponent为空，1项Timer双次Play拒绝已有脏场景。完整Runtime在Editor图的UnityEditor.UI自动编译引用不等于生产反向引用；生产边界仍须实际PlayerWithoutTest图/构建核实。
- Ruling修正: 仅按消费者位置把SerializedDictionaryEditorComponent移入Editor-only程序集是错误判断；真实失败证明其作为可创建/Prefab序列化MonoBehaviour仍需Runtime-capable测试程序集。已连同原meta改移Shared，保留原类型/字段/GUID，不改现有断言或Runtime产品实现。Foundation默认模块显式启用图形设备，不再强行-nographics。
- 双次Play独占会话按原分层进入 `YL.Special.Session`，Daily通过类别元数据明确列出排除项，Special使用相同源闭包独立可复用宿主执行原用例；不把它变成Skip或删除。缺失Special定义先实跑RED，证据 `Tooling-UnityCLI-Special-Foundation-2022.3.62f3_96770f904ca7/484d1ff86645428099c1fb1d3dd5bb6b`。UTF桥接同步保留被排除的真实叶元数据，并仅执行/验收本选集实际声明的模式，零总叶仍拒绝。Tooling组键进一步包含Suite/目标模块/精确Editor，避免Special与Daily互轮换；旧桶不接管。
- Foundation Special GREEN：原TimerSession用例不改安全前置/断言，在独立自有Special宿主实际完成发现、执行及同宿主重复三合同3/3；证据 `Tooling-UnityCLI-Special-Foundation-2022.3.62f3_96770f904ca7/2efff9ba9f7447e7b6e1db3872d4fa69`。这证明分类后的会话用例确实仍执行，而不是将首轮失败隐藏成Skip。Foundation Daily图形/序列化修正后的完整回归继续在另一自有可复用宿主验收。
- Foundation Daily修正后EditMode **365/365、失败0、跳过0**，证明图形前置和MonoBehaviour测试夹具归属已修复。但整次执行仍被严格拒绝：UTF从EditMode切换PlayMode时，对测试留下的Untitled脏临时场景调用SaveModifiedSceneTask，batch取消保存对话框后停滞；300秒门限触发TimedOut/forcedCleanup=true/activeProcesses0，未以已有Passed XML冒称整次通过。证据 `Run-Daily-Foundation-2022.3.62f3/b39d99ac9a7a4ad3821c30da645a2a4b`，末段Unity日志明确记录Scene(s) Have Been Modified，不是推测的退出或测试断言失败。
- Ruling: 原最小UTF桥接将Edit/Play串在同一Editor进程，对真实Editor夹具留下的临时场景不稳。改为同一自有宿主按实际选集模式分别启动Editor，桥接每次只接受一个非空合法模式；每个XML仍独立核原发现集合、输入身份及本进程普通退出，全部模式都通过才汇总Passed。代价是每次双模式运行增加一次Editor启动，换取真实进程/静态/场景隔离；不增加清场/保存/自动丢弃未知场景，不延长超时或降低后代门槛，也不伪装未执行模式。原双模式同进程7/7历史仍是当时证据，适配器改变后按影响重跑。
- Foundation官方2022 Daily GREEN：分模式进程后，实际发现/执行/同宿主重复三合同3/3；每轮 **EditMode365/365 + PlayMode49/49 = 414/414，失败0、跳过0**，所有相关Editor普通退出且无Runtime测试反向依赖。证据 `Tooling-UnityCLI-Daily-Foundation-2022.3.62f3_96770f904ca7/3fd8a6f1ead947779f37ab006c4837ae`；发现摘要明确列出1个Runnable TimerSession叶因Special类别排除，该叶此前另行实际执行/重复通过，不是未批准Skip。接续Unity6同组验收，T4其余模块与默认验收脚本/生产隔离仍未完成。
- Foundation Unity6000.3.27f1 Daily同组3/3亦通过：每轮365 Edit + 49 Play =414，零失败/跳过，重复使用同一宿主；证据 `Tooling-UnityCLI-Daily-Foundation-6000.3.27f1_672d562a42a7/8d91257086fc4a70b977cc952e99b38f`。两版真实整Runtime/Editor编译与Foundation日常闭环已成立，不代表Core/Domain/Data等其余功能组或正式Player构建通过。
- Foundation Unity6 Special同组3/3也完成，证据 `Tooling-UnityCLI-Special-Foundation-6000.3.27f1_672d562a42a7/687ed4a4a6634b07879766e72494369e`。第一组涉及的独占会话在两版都真实执行及重复，不把未配置主工程当此专项宿主。
- T4第二组RED实跑：真实 `after-shared.json` 编译图经Defaults检查12失败（默认Editor8/Runtime4），Player检查12失败（Behavior4、UI4、默认Runtime4）。据此将8个Editor支持源归入单一Editor-only `YLFramework.Tests.Tools.Editor`，Data三探针和Domain探针分别使用命名Runtime-capable探针程序集；保留全部脚本与meta/GUID，Domain作者工具用asmref共享Editor支持程序集，不改用户未跟踪数据。
- Ruling: 非NUnit Player探针不应仅因autoReferenced=false便默认进入生产。采用 `UNITY_EDITOR || YL_*_ACCEPTANCE` 约束，Editor仍能读取旧场景/资产类型，只有显式自有Player验收才能选择Data/Domain/Behavior/UI宏；不自动改主工程符号。同步真实Data反射AQN、Domain定向link.xml、新探针程序集的stripped输入取值及显式构建前置；不重新执行/恢复MB26、MC26历史owner协议，也不把受保护的Data目录当可迁移输出。
- 第二组新增身份交由Unity生成，已正常Refresh且返回isCompiling=true、compilationFailed=false、原场景不脏。待实际编译/图/反射与资产绑定验GREEN；不能将程序集图通过当成真实Player产物或其余模块功能通过。
- 第二组首轮实际编译定位两根错误：Data.Probes因SaveStore公共构造签名的JsonSerializerSettings需显式Json.NET引用；Behavior builder新前置Contains缺少LINQ扩展。已分别补现有Newtonsoft.Json.dll precompiled引用及改Array.IndexOf，未加包或改产品API。Burst随后报告无法解析未生成的Tools.Editor是附带症状，没有改Burst或清缓存。已重新Refresh，待新编译完成再读结果，旧compilationFailed标记不作新结果推断。
- 第二组图GREEN：实际新编译结束compilationFailed=false、未Play/编译/更新、原场景clean；`after-probe-boundaries.json`经 `Test-YLAssemblyLayout.ps1 -Group All` 实跑通过，原12处默认源和12处生产探针源均不再出现在相应图，Runtime生产引用边界亦通过。新Meta由Unity生成；DataPlayerFixture、DataPlayerProbe、DomainProbe原GUID分别dcba682e937a6c84ab48537be7337ce9/9e123d0046306b6448868627e96a3431/4dcbcddcec259054c93f2c0957bd7483仍绑定原类型，新DataMainAcceptance反射AQN实际可解析。未保存/转换用户资产，未将图验收冒称运行或Player验收；需继续其余功能组及真实显式安装链。
- 当前分模式适配器在官方2022安全回归重新7/7，证据 `Tooling-UnitySafety-2022.3.62f3_96770f904ca7/d44e6feb1ae24297ac784f9182f55b67`，不是直接复用旧单进程双模式结果。Unity6当前适配器同组仍待受影响回归。
- Core配对首次缺模块RED见 `Tooling-UnityCLI-Daily-Core-2022.3.62f3_96770f904ca7/2b075af65c68407d8ff8f516cc5371dd`。加入整Runtime/Editor、Core原始Prefab/场景与测试声明后实际编译/发现，EditMode251/262、失败11、跳过0，普通exit1/无强杀，证据 `Run-Daily-Core-2022.3.62f3/876512fb1cf2430c950b91b6ec92c878`。其中漏许可证属于宿主准备缺陷，补真实框架LICENSE；其余定位为旧XF程序集/原始资源/全局分析器对照及跨模块作者字段/验收Prefab合同，不当成本体功能缺陷或通过。
- Ruling: 保留全部旧XF和跨模块作者合同的原断言，分别标 `YL.Integration.LegacyXF` / `YL.Integration.CrossModuleAuthoring`，Core日常发现明确报告排除元数据，不导入旧SDK/资源、不改成Skip。两项旧分析器合同仍保持原XF错误集合；另外用同一实际AssemblyBuilder增加纯YL Editor/Player正负见证，严格核YL0001–7、编译结果与无全局XF Error，不能删除有效警告来绿。Integration能力与前置须在最终说明明确，当前未宣称这些排除项通过。Core日常修正后继续实跑。
- Core准备继续按真实失败修正：框架许可合同还需要现有MiniExcel许可证，连同框架LICENSE作为输入复制，不删许可证断言；之后EditMode正式合同254/254。PlayMode首次208/212，三项旧XF LogUtil/EventSystem/Canvas共存前置明确不存在，保留原断言归同一Integration类别；第四项依赖工程SortingLayer。没有引旧框架或改来源工程TagManager。
- Core自有宿主配置明确增加 `YL.Testing.Core` SortingLayer，只能在已验证owner与请求的batch宿主执行，名称/固定ID占用则拒绝，不覆盖既有层。TagManager实际文件加入有效输入身份，不能仅信配置。首次准备后仍真实208/209：原测试硬编码Map，证明有非默认层不等于有源工程Map。将测试改为读取实际高于Default的现有SortingLayer，保留相同排序/可见性/原生Canvas断言，不修改TagManager、不把0或不存在当通过。其余边界/用户设置继续保留，当前整组仍待GREEN。
- Core官方2022 Daily GREEN：实际三合同3/3，每轮Edit254/254、Play209/209，共463，失败0/跳过0，普通退出与同宿主重复闭合；证据 `Tooling-UnityCLI-Daily-Core-2022.3.62f3_96770f904ca7/ee0a875f88ab48379f4638824f1ac861`。发现摘要明确列出13个依赖旧XF/跨模块作者上下文的Integration叶，原断言与类型均保留，未声明其通过；纯YL分析器Editor/Player正负见证实际通过。本组尚需Unity6及后续正式包/Player隔离验收。
- Core Unity6同组3/3也已实际通过，证据 `Tooling-UnityCLI-Daily-Core-6000.3.27f1_672d562a42a7/9d4c6301c9134a95a1d85ef8937fedf7`；不是源仓库主工程测试，Integration13叶仍未声称通过。
- 用户睡前明确要求后续普通选择自主采用推荐方案；沿用原18项决策和保护范围，不把此要求扩大为Git/新代理/依赖升级/历史清理授权。新增细节决策继续在本计划留据，不等待状态性确认。
- Domain Quick由缺模块真实RED起步，复用原NUnit发现/运行并加入实际Category元数据；没有反射逐方法执行。整Runtime用Unity现有netstandard2.1引用编译（4.5引用不支持真实默认接口实现），NUnit host沿用Mono/NUnit3.14；UI/Json.NET/Bouncy等真实输入均指纹绑定，不写Library或改包。首次暴露工具链重复引用、JSONObject缺Unity分支符号、Unity6缺6000版本分支，分别按真实编译错误修正参数，不改产品源码、忽略诊断或虚构native支持；符号明确记录在summary。
- Domain纯逻辑官方2022合同5/5、实际41/41通过，证据 `Tooling-QuickCLI-Domain-2022.3.62f3_96770f904ca7/cc6c42a13edd4f718b4b6e7f844f8e8d`；Unity6同合同5/5，证据 `Tooling-QuickCLI-Domain-6000.3.27f1_672d562a42a7/3f1b8628afde4e8aa8ffbe2cc7083c38`。公共合同同步重验25/25。上述Mono结果不是Unity生命周期或Player构建验收。
- Domain native配对接入原逻辑/作者/场景测试；Timer两次Play进入同类Special，显式自有宿主预配DisableDomainReload并绑定实际EditorSettings输入，原测试前置/断言保留。原主Editor保护Explicit合同单列 `YL.Special.MainEditor`，自动化不接管主工程；输入拒绝测试仍Daily执行且反射AQN同步已移入Tools.Editor的新程序集。首次隔离编译暴露缺少原共享TreeView测试窗口依赖，已纳入原源码和图形前置，不做空替身；第一轮compile失败及DescendantsAlive仍原样拒绝，没有降低2秒收尾。待实际native运行GREEN。
- Domain官方2022 Daily三合同3/3，每轮Edit85+Play14=99/99，证据 `Tooling-UnityCLI-Daily-Domain-2022.3.62f3_96770f904ca7/3a53002bc16f42509b9b9c2a3403f45d`；Special两项原双次Play亦3/3并重复，证据 `Tooling-UnityCLI-Special-Domain-2022.3.62f3_96770f904ca7/c830bf8c3f794172bd1468a80e388597`。从未改主工程Reload选项，不再把原主工程前置不满足当此专项成功。
- Domain Unity6 Daily首轮83/85失败2，真实输出显示场景句柄相同但原测试先转int后与实际API句柄类型比较；改为保留API返回类型的var，仍断言同一原场景句柄与设置未变，不改产品入口或删断言。随后Daily3/3、每轮99/99，证据 `Tooling-UnityCLI-Daily-Domain-6000.3.27f1_672d562a42a7/b84691b00e304effad9bf05e55870024`；Special两项3/3，证据 `Tooling-UnityCLI-Special-Domain-6000.3.27f1_672d562a42a7/27b3635604bb46d9b2a095d496cbd998`。Explicit主Editor专项仅明示前置，不自动运行。
- T6现有快速门新证据：`Test-Format-YL.ps1`在自销毁sandbox9/9，未格式化仓库源；`Tools/YLFramework.Analyzers/build.ps1`实际SDK9.0.312 locked restore/build/test，181/181、零skip、build零警告/错误，未Deploy。既有Analyzer DLL和诊断裁决未改。批次/全量格式检查和统一维护说明尚未收口。
- Behavior Quick按现有5个fixture加Category，不列叶子表或模拟native。实际RED为纯作者写入器未编入测试支持源（缺Editor命名空间），随后仅把真实无UnityEditor依赖的FsmStateFileWriter作为testSupport编入离线host；测试通过真实类型定位该纯写入器，不再为了它引用FsmEditorWindow。旧有SourceRoot环境仅传子进程，不设系统变量；写入器占路径反例改在NUnit自有WorkDirectory中完成，不能向来源工程Temp新增运行产物。官方2022 Quick五合同5/5，证据 `Tooling-QuickCLI-Behavior-2022.3.62f3_96770f904ca7/8275daf64a244bf7b8bee5098b05dd12`，后续native及Unity6待验。
- Behavior Quick Unity6五合同5/5，实际55/55、零skip，证据 `Tooling-QuickCLI-Behavior-6000.3.27f1_672d562a42a7/0b94b66c736a4e1ea2f886dd9fbc7dce`。Native首次86/87明确暴露UTF的NUnit.WorkDirectory为空；修正为工具子进程专属 `YL_TEST_OUTPUT_ROOT`（人工则OS临时根+本例GUID），不再假设UTF提供WorkDirectory或写来源工程Temp。原占路径/不覆盖字节/真实创建断言不变，输出父路径受工具owner控制，子目录用例自己释放。
- Behavior官方2022 Daily三合同3/3，每轮Edit87+Play50=137/137、失败0/跳过0，证据 `Tooling-UnityCLI-Daily-Behavior-2022.3.62f3_96770f904ca7/4a711d85eed54fab8a1794255801f7ef`。Unity6首次137项已实际通过，当前正在同宿主重复，进程 `bgp_18ec31088001dIdhWuW2I1Sq75`；完整终态未到前不标该组最终通过，不为等待改超时或轮询。
- T6本批当前35个修改/新增C#文件经既有 `Format-YL.ps1 -Mode Check -Paths ...` 实际退出0（SDK9.0.312），未Apply；首次嵌套PowerShell命令引号解析失败仅是调用失败，改为临时-File驱动后完成真实Check，不当成源码诊断。当前 `git diff --check -- Tools/Testing Assets/YLFramework/Tests`通过。最终全Check/README及其余模块仍待收口。
- T5后续前置核实：本机确有SDK10.0.102与10.0.402，不需为了Save升级或改Analyzer锁；旧SaveRunner逐方法反射且手写纯JSON叶表不能继承为新NUnit结果。Excel旧Runner虽用NUnit但固定类/可选程序集装载，后续须按真实合同迁移，不把GUI/AssetDatabase编译当已运行。原有Save/Excel脚本此时仅只读调查，未重跑其硬编码旧Unity路径或旧输出。
- Behavior Unity6终态已回读：三合同3/3，重复Edit87+Play50=137/137、失败0/跳过0，全部普通退出0；证据 `Tooling-UnityCLI-Daily-Behavior-6000.3.27f1_672d562a42a7/2c6c07ddbf1e4e19adee885c1100f45e`，终态时间21:45:37Z。此前“正在重复”仅是当时状态，不再将它列为等待项。
- Save专项RED为原List无该模块；采用同一真实NUnit runner而非逐方法反射，Category实际发现纯BCL叶；Unity JSON方法不加BCL类别、仍由native Data集运行。沿用已安装SDK10.0.102/Runtime10.0.2及NUnit3.14 netstandard2.0，不升级依赖或更改Analyzer锁。Ruling: 明确禁用Runtime补丁滚动以绑定现有10.0.2真实CoreLib指纹，不将旧runtimeconfig声明误报实际运行版本；代价是缺少该已声明前置时直接拒绝，而非无声换版本。测试程序集用既有friend身份YLFramework.Tests.EditMode，不公开SaveFileIO或修改Runtime可见性。
- Save官方2022工具合同5/5，实际38/38、零失败/跳过；证据 `Tooling-QuickCLI-Special-Save-2022.3.62f3_96770f904ca7/93fe34c601af4d9da532de39fa514ba9`。首次support路径错误/内部类型编译错误均按实证修正，没有修改存储逻辑、异常断言或伪造结果。父进程仍严格核普通退出、XML集合与输入漂移；Quick/DotNet工具结果按Suite/Module/精确Editor分桶。
- Data与Excel已先取得未定义模块的真实RED，再声明原源码/程序集/图形前置；Excel明确只有EditMode，不虚构PlayMode。Data日常保留Save原生JSON、Localization/DataAssets逻辑/作者/资源生命周期，独占会话类仍保持原MB26断言等待安全迁移，暂不声称已通过。Excel纳入真实可选适配/随附MiniExcel，不伪造后端或只编译后报通过。
- 22:56恢复检查：上轮Data/Save6启动被中断，各新Tooling目录无summary且Data CLI日志为空，**没有可接受终态，不当失败或通过复用**；仅保留原始不完整记录，不接管/清理。当前无受跟踪后台进程、无自有宿主Editor，用户主工程已是PID23028、Hub服务PID13788，按可执行路径与项目身份保护，不依据旧PID操作进程。后续继续自有宿主新run验收，不开第二个主工程实例。
- 恢复时原Data宿主一份IOC元数据前32字节全零、243字节长度，源仓库同文件仍是正常原GUID文本；原Save6缓存亦内容指纹不符。来源未知，不归咎Unity或假装修复生产问题。原自有内容仍保留且拒绝复用，未重写收据、弱化校验或删除目录；在现有工具归属Cache内新建自有恢复空间。第一份深路径恢复空间导致旧Mono的Save原子临时路径达273字符，原Data Edit74/78；改用短根 `G:/Unity/Testing/Cache/RD26/W` 后相同路径243字符且全过，没有改存储实现/异常/原断言。长根及失败XML保留，不声称任意长路径得到产品支持。
- Save Unity6在新恢复空间真实38/38、五合同5/5，证据 `Cache/Recovery-20261030-DataSave/Workspace/Results/Tooling-QuickCLI-Special-Save-6000.3.27f1_672d562a42a7/e301937188f94ba29eeaf96145f76941`。Data两版Daily各三合同3/3，每轮Edit78+Play15=93/93、失败/Skip均0，证据在短根的 `Tooling-UnityCLI-Daily-Data-2022.3.62f3_96770f904ca7/31f7ff1baac64363bd745867ca456777` 和 `Tooling-UnityCLI-Daily-Data-6000.3.27f1_672d562a42a7/bc1cd1f6b578428fb73d40aa66df7d8e`。
- Data独占会话先真实RED，失败仅为历史MB26主工程固定身份。迁移为调用现有基础设施 `RequireOwnedRunOutput`，复用batch宿主/请求/结果owner/输入/路径校验，不另写一套身份协议；根EditMode显式引用Editor基础设施，不让Runtime反向依赖。保留真实两次Play、旧owner释放、缓存归零、原场景及Reload恢复断言，绝不接管主工程。两版Special均三合同3/3，分别在短根 `Tooling-UnityCLI-Special-Data-2022.3.62f3_96770f904ca7/4c52ba4b48dd4b4aaf11890b92893e0b` 和 `Tooling-UnityCLI-Special-Data-6000.3.27f1_672d562a42a7/4cf41780096c4194b6819f7e068e26c8`。
- Excel沿用真实可选源码/MiniExcel、XLSX/AssetDatabase/预览/绑定DataRoot，不使用旧Runner固定类名单；明确本模块只有EditMode，声明与实际模式集合严格一致而非虚构PlayMode。两版各41/41、三合同3/3，证据在短根 `Tooling-UnityCLI-Daily-Excel-2022.3.62f3_96770f904ca7/4dcf736e84014afaaebc19238337b2ff` 与 `Tooling-UnityCLI-Daily-Excel-6000.3.27f1_672d562a42a7/e6fb0be5c54143c9b14384f19e36154b`。
- T5原离线组收口：公共host搬至 `Tools/Testing/NUnitHost.cs` 并同步CLI、NUnit/Safety合同与formatter白名单；删除已被当前Quick/Daily/Special等价覆盖的UIPanel、Domain、Behavior Offline及Data/Localization/DataAssets/Excel/Save离线执行器和两个旧runner，不留转发壳。原DomainClosure禁止XF/UnityEditor/可选MiniExcel的引用规则并入真实生产图检查，仍不冒称功能测试。唯一余下旧路径文本为A固定Behavior Unity入口的历史保护集合，不是活编译调用；不改旧冻结授权使它能在当前主工程运行。
- Ruling: 旧DomainExcel脚本会git archive旧B提交，但当前仓库已无其Tests目录，且没有其他现实调用方；移除该历史复合编译入口，不为它恢复旧提交/工程或补占位测试。当前DomainExcel可选适配与宏、原始历史记录保留，**未宣称该可选跨模块专项通过**。UI当前维护表与Behavior Usage当前入口同步新CLI，后续章节历史事实不改写。
- 搬迁后真实NUnit host四合同4/4、Save五合同5/5以及公共25/25重新通过，短根证据分别为 `Tooling-NUnitHost-2022.3.62f3_96770f904ca7/c4f8d941e9354dcd95c9afc4f0b8aec6`、`Tooling-QuickCLI-Special-Save-2022.3.62f3_96770f904ca7/871ba74f9a0249fb96ea9c8d450790cc`；不是只凭路径替换判收口。统一维护说明已落 `Tools/Testing/README.md`，还需最终全格式检查、当前适配器双版安全门和分发/生产验收；T4/T5整体未冒称完成。
- T6全量Check先真实失败，1131文件中仅Stage9支持源码35处空白格式；本组显式限定该一个文件Apply，diff仅展开原if/foreach和JObject成员行，不改常量、检查或历史授权。随后1131文件全量Check实际退出0，未改formatter规则或批量格式化其他来源。当前Unity6全安全回归也重新7/7，短根 `Tooling-UnitySafety-6000.3.27f1_672d562a42a7/b130f9e9966f4e3395878a11f4729f78`；包含真实失败/取消及同宿主恢复，不以已有XML掩盖非普通进程终态。
- T7新增限域分发验证器 `Tests/Test-YLDistribution.ps1` / `DistributionEntry.cs`，只复用实际ExportUnityPackage.Export并解析其真实固定根；Bootstrap位于自有工程的产品根外，不进入包。包内容从本次真实unitypackage读取，不解压/恢复外部历史ZIP；新接收工程仅声明UGUI/Json.NET与来源内置模块，没有Tests/UTF/工具上下文。每阶段严格核owner、结果、版本、普通进程收尾；Player缺模块返回明确PrerequisiteNotMet而不是通过。
- 分发验证器首轮编译存在Assembly同名歧义，修正全限定名；第二轮包检查错误拒绝合法产品Editor/Tools，已将开发Tools排除定位到实际根，仍严格以产品根白名单拒绝Tests/Integrations/仓库Tools，不放开任意路径。两轮原始失败保留，未当产品缺陷或验收成功。
- 官方2022分发实际三门通过：正式API导出产物的真实根/GUID检查、无测试上下文裸导入编译/GUID/Prefab缺脚本0/生产图边界，以及普通Windows64 Mono BuildOptions.None真实Player产物与Runtime AssemblyRef检查。短根 `Tooling-Distribution-2022.3.62f3_96770f904ca7/97495d14f2f74fecbd206fbd8307ba2c`。这确认Editor基础安装具备本次Mono构建所需支持，不能把先前“未额外安装模块”当永久构建阻塞；未执行该Player或IL2CPP/AOT，不冒称运行/其他平台通过。下一步Unity6分发及当前源码冻结后的完整声明矩阵。
- Unity6同一分发三门也全部实跑通过，短根 `Tooling-Distribution-6000.3.27f1_672d562a42a7/0c0d57f46d6341a59fc02f6f10dc9c3d`，checks=3、player=Passed；含真实Mono Player产物，不是静态理想图或导出非空替代。当前全1132文件formatter Check退出0。
- 全局git diff --check只报告受保护ProjectSettings.asset的既有18处m_SubKind空值尾空白；其当前SHA256与迁移前原始快照完全一致，均为52836F9870EC4204D26E8421BC126D40682051F7CF5F2A5C5A11E18C4A446CA4，证明本批未改。没有为了全绿格式化它；本批Tools/Tests/文档/计划/批准配置范围的diff --check通过。
- 最终当前声明矩阵按配置枚举模块、通过原CLI逐一实跑，不维护手写叶名单或新的正式主入口；临时驱动只聚合真实子run摘要/进程事实。官方2022 Daily正在自有短根执行，进程 `bgp_18f4c4c8e001E2BYmEwfHkyLje`，已完成Reddot12和Foundation414，仍需剩余终态；随后Unity6同矩阵。驱动是本次验证材料，不新建永久执行框架。
- 官方2022当前声明Daily矩阵已实证完整通过：Reddot12、Foundation414、Core463、Domain99、Behavior137、Data93、Excel41，共七模块1259项，失败0/skip0，逐子run普通退出/选集/XML/身份闭合。短根 `Tooling-Matrix-Daily-2022.3.62f3_96770f904ca7/c85d25e754174c789da28bf3033d88f4`，不是复用历史计数。Unity6相同矩阵当前 `bgp_18f525e830013yYceEIYAVzXHy`，已过前两模块，余下终态待回读。T4/T5剩余Resource/重型/模板边界不能借此无条件勾完。
- 上述Unity6最终矩阵实际为六模块通过、Behavior失败，整体Failed，证据 `Tooling-Matrix-Daily-6000.3.27f1_672d562a42a7/0b37fa3c7a774007a0dbe9030171fa38`。第一次Behavior是发现前UPM IPC中断/包解析取消、普通exit1且无XML，未假报为用例失败或通过；串行重跑的第二矩阵则是另一问题：发现完成后EditMode执行300秒无终态，强制回收保持TimedOut，证据 `.../8cff89e8e2c74c8d81c6042f176c2fee`，没有提高超时或忽略结果。
- 增加每个自有run内的最小 `progress.jsonl`（实际UTF leaf Started/Finished+状态），写入失败走已有Fail终态，不构建统计平台。120秒定向复现明确停在原生窗口输入派发用例 `NativeWindowMouseSelectionRoutesDeleteToTheSelectedNode`，不是继续猜UPM或等待；保留原记录 `Run-Daily-Behavior-6000.3.27f1/a374fcae3cff47f19cd55186337b1f8c`。
- Ruling: 两个真正的窗口焦点/原生输入派发合同归 `YL.Special.NativeInput`，原用例、输入链和断言不改；当前unattended batch的交互前置不能稳定保证，不接管用户Editor或伪报专项通过。Daily显式列出这两项排除叶，不能当Skip/已过；既有137条通过仍仅为此前环境证据，后续新日常数量由实际发现决定。此分层符合Q11，不把偶发早期通过外推可无人值守稳定运行。
- T4 Action接入先缺模块RED，实际编译又暴露Ease测试借用其他测试类的白名单。把已有路径/正式BouncyCastle SHA256/Engine/BCL依赖判断提取为测试专用TestDependencyGuard，更新所有现实消费者（Framework、Audio、Action、导出与Resource启动测试），不留原测试类转发。Action移除旧XFAB放行并只纳入已有UGUI编辑编译依赖，不改Runtime；新meta由Unity生成。
- Action首次真实Play92/99，7项GlobalMessage前置拒绝接管残余公共UpdateManager。定位到前置ActionStart/Async夹具未释放自己新建的空闲公共源；SetUp捕获既有owner，TearDown沿用ReleaseIdleUpdateHost仅释放新建且公共事件无监听者的宿主，保护借用/有监听对象，不修改生产退出或弱化保护断言。随后Unity6发现/执行/重复三合同3/3，短根 `Tooling-UnityCLI-Daily-Action-6000.3.27f1_672d562a42a7/e1aa7d62067b41b69e5f61dd619ffad9`。官方2022仍待配对。
- Audio接入先缺模块RED，再按真实编译错误补共享依赖guard及既有ActionFixtureSession支持源；Resource必需辅助测试明确标本模块类别并在Audio排除，避免复制帮助逻辑/误执行另一模块。首轮Edit518/521、失败3均为独占场景/连续Play的原安全拒绝；将这些方法/类与原DSP/Bundle测量一起明确归YL.Special.AudioNative，保留断言，不在脏场景上自动保存/丢弃以凑绿。该专项未宣称无人值守实现，Module只声明Daily；当前日常重验进程bgp_18f7b2115001XTwu7HJYCq7gAJ仍待终态，其他模块/分发已过不能替代它。
- 分发自检还借鉴现有Equipment审计的独有价值，收紧本次生成包的实际成员/重复/路径、meta GUID与原meta逐字节指纹、每个文件payload指纹及非空源资产覆盖，不仅检查根目录存在。它不执行旧Equipment接收或读取历史ZIP。该检查新增后需重跑两版真实分发；早先三门记录保留为当时版本证据，不提前复用为新审计通过。
- Action官方2022同组三合同3/3也通过，Edit274+Play99=373，短根 `Tooling-UnityCLI-Daily-Action-2022.3.62f3_96770f904ca7/e4c72762484a45b4990350bdea5df845`；Unity6前述同373。原owner保护和所有失败记录保持，不为会话清理关闭他人的公共源。
- Audio当前日常两版三合同3/3，实际Edit518+Play180=698/698、失败0/skip0，短根 `Tooling-UnityCLI-Daily-Audio-6000.3.27f1_672d562a42a7/f44824933bef462a80015be59dd493ad` 与 `Tooling-UnityCLI-Daily-Audio-2022.3.62f3_96770f904ca7/9c5354feb6e54bbaab2bf2d10878bc93`。Special类别的原独占/设备合同未执行不冒称通过；README现有专项边界表按Q11集中说明而非新增平台或重演历史矩阵。
- Behavior经NativeInput显式分层后，Unity6日常实际发现/执行/同宿主重复3/3，短根 `Tooling-UnityCLI-Daily-Behavior-6000.3.27f1_672d562a42a7/ff4ffb17fc244ca0a79f3bc2b24d951e`。必须依据本轮真实选集确认新135项及2个原生输入排除，不能把此前137项历史证据改写。本轮增加的进度日志/测试共享helper与Source分类使最终矩阵、主工程编译图和格式门需要再次核对。
- 收紧后的包审计第一次正确识别预期集合错误：父目录Assets/YLFramework.meta为准备宿主而复制，不在真实_exportRoots声明内，Unity不必把这个父目录作为包记录。仅将覆盖断言限制到实际导出根内的源资产，未删除任何产品文件/meta/payload指纹或重复/越界校验。随后官方2022完整三门重新通过，短根 `Tooling-Distribution-2022.3.62f3_96770f904ca7/71f61d2258fe49c4a38d1abbebe5bf41`；当前1133文件全量formatter Check退出0。Unity6收紧版审计和最后包含Action/Audio的新矩阵仍需完成，不以旧包审计版本外推。
- Unity6收紧版分发审计三门已实际全部通过，短根 `Tooling-Distribution-6000.3.27f1_672d562a42a7/ceef9200eaa64ff085c0f4e315d23bc9`；实际包的payload/meta字节、GUID、完整源覆盖、裸导入和普通Mono Player产物均闭合，不包含运行/IL2CPP结论。
- 当前官方2022安全回归进程 `bgp_18f91eb0b001U2ajbKSW9ubQA1` 正在独立自有夹具执行，已通过新NUnit4/Quick5/UTF3合同与源码漂移拒旧选集、真实failure XML/exit1，余下实际取消和同宿主恢复待终态。已声明的新Action/Audio与NativeInput分层使旧1259矩阵不能继续当当前全量；下一轮按真实配置重新枚举，不把排除/新增叶手工凑到旧数量。
- 当前官方2022安全回归终态已完整7/7，真实取消保持非普通/forcedCleanup=true/active0并且恢复原同宿主Edit10/Play2；短根 `Tooling-UnitySafety-2022.3.62f3_96770f904ca7/118e468565594694ae898c2754a272d6`。该结果不掩盖前面Behavior两个实际失败。
- T4/T5 Resource最后按有界当前合同迁移，缺模块先RED；仅Catalog/Codec/真实Bundle构建进入普通入口，不复制Workbench/Cost/TLS/native下载的固定日期证据执行器。首轮25/26，保护快照发现Unity首次生成SceneTemplateSettings.json；在验证owner后，仅空、未脏、无内容的自有启动场景可生成本工具的新准备场景，实际设置/场景文件指纹参与输入，不排除保护或保存未知场景。旧Additive准备方式被Unity明确拒绝，改成只操作已确认空启动场景的Single，失败原样保留。
- Resource原生构建又准确触发已有260路径限制，RD26深根和18字符标签仍不够；不改产品限制。只对Resource采用紧凑目录标签并持久比对完整key+hostId，碰撞拒绝且不接管无收据旧host；实际工作根用原批准G:/Unity/Testing，其他恢复模块继续RD26/W。两版普通发现/执行/同宿主重复3/3，26/26零失败/skip；分别 `G:/Unity/Testing/Results/Tooling-UnityCLI-Daily-Resource-6000.3.27f1_672d562a42a7/0de12886135f434db9bbf616c71bc151` 与 `...-2022.3.62f3_96770f904ca7/937c861a454b42d189f523a7f1fcc51a`，有真实BuildPipeline Bundle/manifest及artifact身份，不只是静态编译。
- 原Resource会话三项首次执行0/3，明确依赖旧UTF提供的多场景/活动空未保存场景布局，当前runner没有此前置。原保护断言/源码保留在YL.Special.ResourceSession，Module不声明尚未实现的unattended Special，不改成Skip/通过，不在未知场景上清空/保存，也不为它恢复旧接收工程。与NativeInput、Audio设备/独占会话同按Q11列出未验边界，不影响已限定26项真实结果。
- 冻结后两版Daily完整矩阵均实际通过，当前十个模块合计2354叶/版、零failed/skip：12+414+463+99+135+93+41+373+698+26。分别短根 `Tooling-Matrix-Daily-2022.3.62f3_96770f904ca7/636dccca2ae847069b65853707983e78` 与 `Tooling-Matrix-Daily-6000.3.27f1_672d562a42a7/76d711671e3349748578a6f0a61a99f0`，Resource子run在原批准短根G:/Unity/Testing，聚合摘要记录实际workspace。不把早先失败矩阵改报通过。
- 当前已声明Special两版也整组通过：Foundation1、Domain2、Save38、Data1，共42叶/版，分别短根 `Tooling-Matrix-Special-2022.3.62f3_96770f904ca7/218eff7e3f624233b1e512a4e306e464` 与 `Tooling-Matrix-Special-6000.3.27f1_672d562a42a7/e0b3e61931a54609b9a96d0b370d169c`。未实现/缺前置的NativeInput、AudioNative、ResourceSession不在声明中，原代码和已发生失败保持记录，不能当这些专项通过。
- 最终主工程只读证据：正常Refresh后实际编译无失败、未Play/更新/编译，原YLFrameworkDev场景clean；九个保留GUID仍解析原完整类型。自有Cache `FinalGraph-2022-20261031/final.json` 由当前真实Editor与PlayerWithoutTestAssemblies采集，`Test-YLAssemblyLayout.ps1 -Group All`实际通过；新增TestDependencyGuard.meta为Unity导入生成。另 `protected-inputs.json`逐项确认36个原用户ProjectSettings/HybridCLR设置/YLFrameworkData/Data与Release82b夹具等输入仍与最初SHA256快照一致。
- 本批作者自检已读实际差异与关键执行/结果/宿主代码；当前任务范围diff --check退出0，源图/资产身份、模式/结果边界未混淆，没有追加独立Review或Git动作。最后只余Plan复选框/终态收口及进程检查，不能因此再重复完整技术矩阵或扩散下一阶段。

### 最终验收与交接（2026-10-31）

- 本轮实现验收范围是统一日常/声明专项入口、程序集与自有夹具治理、旧离线执行器退役、独立快速门和双版分发/生产隔离，不是恢复全历史工程或完成所有设备/可选SDK功能矩阵。按Q11保留真实平台/场景前置，禁止借本次完成把未验项改报通过。
- 入口：`Tools/Testing/Invoke-YLTests.ps1`，维护说明 `Tools/Testing/README.md`，唯一模块元数据 `Tools/Testing/TestSuites.json`。List默认只读；Discover不是测试通过；Native逐模式独立进程，普通成功必须选集/XML/身份/进程与收尾全部闭合。普通失败/取消/未知输出不放宽。
- 最终有效证据集中在 `G:/Unity/Testing/Cache/RD26/W/Results`；Resource因真实路径预算使用原批准根 `G:/Unity/Testing/Results`，各子轮摘要记录实际工作根。源图/GUID及36保护输入在RD26/W的 `Cache/FinalGraph-2022-20261031`。中断导致的旧内容不接管、不修指纹、不清理。
- 新跑：双版Daily各2354、Special各42，双版普通Mono生产构建与包payload/meta/GUID/bare import，公共25、NUnit4、Quick5、双版真实Safety7，完整formatter1133与formatter合同9；Analyzer181沿用本轮未受影响的实际锁定构建测试。没有用dotnet编译代替Unity/native或把Player构建称为运行。
- 仍未验/限制：2个NativeInput需要真实交互桌面；AudioNative设备/独占会话与ResourceSession旧runner布局条件未成立，保留原失败/断言并且不声明可无人值守执行；原XF及跨模块旧资产对照、DomainExcel可选适配、OS/IDE/DSP、IL2CPP/AOT/其他平台不外推。受策略阻止的外部输入ZIP仍未核实不采纳。上述均在原授权分层边界内明示，不制造Accepted/Skip成功。
- 用户主工程继续保持原场景clean，代码实际编译完成，无自动主工程测试/保存/关闭；用户既有设置/未跟踪数据经原快照指纹保护。旧c1与旧父目录清理仅沿用此前独立明确授权结果，本轮不再扩散磁盘清理。所有修改保留工作区，未commit/push/切分支/创建代理/worktree或独立Review。

### 下一轮：核心会话与 Player 运行补验（2026-10-31）

- 用户要求执行下一轮主线：ResourceSession、Audio会话/资源集成、有实际行为的Windows64 Mono Player冒烟及受影响回归；IL2CPP/AOT、NativeInput为随后补验，不由本轮自动扩大安装、Git、代理/worktree或独立Review授权。沿用现有CLI、隔离宿主及结果协议，不接管主工程场景，不恢复历史工程。
- 环境已核：官方2022实际路径为 `G:/Unity Editor/2022.3.62f3/Editor/Unity.exe`，完整身份仍为 `2022.3.62f3_96770f904ca7`；Unity6仍为 `6000.3.27f1_672d562a42a7`。主工程使用新2022路径，包声明中的Cinemachine/Timeline已有用户调整，本轮保护不回退。
- Ruling: Resource会话优先修正自有宿主的启动场景，而不是改原安全断言。UTF1.3.9的CreateNewSceneTask在单一无路径场景上建立DefaultGameObjects，已保存场景上则追加EmptyScene；后者才是原会话夹具所需布局。配置和实际准备场景继续纳入输入身份，未知/脏场景拒绝。
- [x] ResourceSession双版本发现、执行及同宿主重复。
- 初次新跑再次真实0/3，保留 `Run-Special-Resource-6000.3.27f1/c2e7a52255b2425eb68e51f2f7ef5394`。启动前打开已保存场景仍0/3，进一步查实际宿主锁文件发现Unity6使用内置UTF1.6.0（声明请求1.3.9不等于实际解析）；其CreateBootstrapSceneTask总是Single默认场景。Ruling: 保留启动前身份/空场景保护，并在RunStarted、测试尚未开始时仅替换严格匹配Camera/Light组件的原生默认bootstrap，准备同一已保存空场景+活动空场景；不删除任意根、不按版本盲目清场。实际包锁与编译DLL继续绑定选集。
- [x] Audio会话/资源集成与设备/DSP分层，双版本专项与日常回归。
- [x] 有实际资源加载/框架行为/释放和正常退出的Player冒烟，构建与运行证据分开。
- [x] 公共安全、影响范围回归、格式及维护说明收口。
- Resource原3叶两版真实通过、三合同各3/3，场景保护断言未修改；证据根 `G:/Unity/Testing/N26/Results`，Unity6 `Tooling-UnityCLI-Special-Resource-6000.3.27f1_672d562a42a7/a5d4fa0914eb48e7854af6ff2717a125`、2022 `...-2022.3.62f3_96770f904ca7/bed93a183c2e4bde9fa09499c815e55c`。曾在场景修正后2/3发现缺少原Resources文本夹具，现仅纳入真实Message.txt/meta；整Resources目录的Sprite导入元数据会被Unity6更新，旧宿主已按门槛拒绝且保留，不修收据。新N26短根使用同一owner/lease协议；两次成功确实复用同一宿主。
- Ruling: AudioSession只选择真实连续Play、authority/provider和两个无输出采样的Bundle生命周期合同；真实GetOutputData测量叶仍为AudioNative且不纳入。原AudioAcceptanceEntry只作为显式测试宿主支持源安装，不改变主工程或生产包程序集；所有旧断言与DSP代码保留。
- AudioSession实际7叶两版三合同3/3，日常仍698叶两版三合同3/3，零失败/skip。初次N26根5/7，两个Bundle用例被产品260路径预算拒绝；切回原批准最短根 `G:/Unity/Testing` 后原断言通过，未改产品限制。Special证据 `Tooling-UnityCLI-Special-Audio-6000.3.27f1_672d562a42a7/f235894679fb41d99a487470de584398`、`...-2022.3.62f3_96770f904ca7/7a2296a66c654c388ab9ada7413045fa`；Daily `...-2022.3.62f3_96770f904ca7/b5311f0ce3514e07acaa557aa851679f`、`...-6000.3.27f1_672d562a42a7/f2cca93558614993a6fa4fc367334204`，均位于原根Results。
- Player先新增身份/阶段/真实退出校验合同，明确缺少Read-YLPlayerSmoke而RED；随后8个原Results合同加新合同共9/9通过，拒绝旧run/host/版本/PID、Editor替代、未完成/重复阶段、残余租约、错误日志及非普通进程状态。真实空场景Player在新运行门超时失败，保留 `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-Smoke/4bf90a8b6f7d4fa3bc3626bfb39cf328`，没有将原构建通过当运行通过。
- Ruling: 不建设另一套Player平台，扩展原Test-YLDistribution.ps1的显式-RunPlayer，在正式导出/裸导入与图隔离通过后才安装Tools/Testing/PlayerSmoke.cs及专用asmdef/Resources到自有接收宿主的产品根外。普通默认构建仍不安装探针。实际执行加载Resources文本、红点通知/释放、live Lease的Busy保护、关闭及零计数，报告绑定构建run/host/实际PID/版本，构建产物前后SHA256一致；两次运行使用同一产物但独立输出。
- 双版新Player链各5门通过（导出、裸导入、Mono构建、真实运行、同产物重跑），各两次普通exit0。证据根 `G:/Unity/Testing/N26/Results`：2022 `Tooling-Distribution-2022.3.62f3_96770f904ca7-Smoke/c50b74e295984936b49a12b648924c7c`，Unity6 `...-6000.3.27f1_672d562a42a7-Smoke/b99687f8955740469932555686c4d6a4`。这是Windows64 Mono的无图形逻辑/Resources冒烟，不包含图形、DSP、IL2CPP/AOT或其他平台。
- 最终源码冻结后的实际UTF版本分别为2022的1.3.9与Unity6的1.6.0，已写入机器结果；未升级源工程依赖或把请求版本当实际版本。四组每版734叶（Resource Special3、Audio Special7、Resource Daily26、Audio Daily698），全部零失败/skip且每组发现/执行/同宿主重复3合同通过。以下是当前终态证据；较早成功可能按正常保留策略轮换，固定RED不轮换。

| 最终组 | 官方2022证据后缀 | Unity6证据后缀 |
| --- | --- | --- |
| Resource Special | `Results/Tooling-UnityCLI-Special-Resource-2022.3.62f3_96770f904ca7/f1e8824a8c684e75bc7147aa7f706979` | `N26/Results/Tooling-UnityCLI-Special-Resource-6000.3.27f1_672d562a42a7/7403903abfed4440b9b780368b7b985a` |
| Audio Special | `Results/Tooling-UnityCLI-Special-Audio-2022.3.62f3_96770f904ca7/b4409c2b96374cc0988c1b6803993c36` | `Results/Tooling-UnityCLI-Special-Audio-6000.3.27f1_672d562a42a7/87fdda46ec9c40b1adfde51440b9098c` |
| Resource Daily | `Results/Tooling-UnityCLI-Daily-Resource-2022.3.62f3_96770f904ca7/14c298e23ffa4eb6ac739ba2233b0ad2` | `Results/Tooling-UnityCLI-Daily-Resource-6000.3.27f1_672d562a42a7/dcceb4a9131247d3942f3ed44d630976` |
| Audio Daily | `Results/Tooling-UnityCLI-Daily-Audio-2022.3.62f3_96770f904ca7/6877231cadd14353918d8f27d13cda8e` | `Results/Tooling-UnityCLI-Daily-Audio-6000.3.27f1_672d562a42a7/7396fa55a4054d94b2828e8536afff08` |

- 上表相对 `G:/Unity/Testing`。临时串行驱动曾误选Unity6原根中已被拒绝的Sprite元数据宿主，发现前按门槛失败，保留 `Results/Tooling-UnityCLI-Special-Resource-6000.3.27f1_672d562a42a7/90934dbcfafd405c980eb8eea0835bc5`；改回已验证N26自有宿主完成终态，不修补收据或删除未知输入。
- 原普通分发三门（未安装探针）两版回归通过：相对N26/Results，2022 `Tooling-Distribution-2022.3.62f3_96770f904ca7/b3e5a480f019464e96285c40e3642110`、Unity6 `...-6000.3.27f1_672d562a42a7/70523eaf7c5243d6b467b49bda63aab6`，Build/report.json明确smoke=false、errors=0，warnings分别9/14；包含既有ResourceException.Source成员遮蔽、Unity6过时FindObjectsOfType和第三方XML文档警告，未外推零警告或顺手改生产源码。
- 公共合同26/26（含新增Player严格报告合同）、Formatter保护9/9、全量格式1134文件Check通过；Formatter仅增加精确PlayerSmoke模板项，不放宽目录保护。Safety两版7/7（真实新增叶拒旧选集、失败XML/exit1、取消、后代归零及同宿主恢复）：相对N26/Results为 `Tooling-UnitySafety-2022.3.62f3_96770f904ca7/aec37616cd2c48d893789148afa8b17c`、`...-6000.3.27f1_672d562a42a7/acb284307713486694b91ad1e4b21602`。生产Analyzer源码未改，不重复沿用其历史181项作为本轮新跑结论。
- 自检实际差异及模板、普通/冒烟分发程序集边界和报告路径完成；维护指南更新本机新Editor路径、SessionScenes与Audio分类、显式-RunPlayer及证据边界。主工程只读复核仍为 `Assets/Scenes/YLFrameworkDev.unity`、isDirty=false、rootCount=2，Editor未Play/编译/刷新；未在主工程测试、保存、关闭或运行Player场景，未commit/push、新建代理/worktree或独立Review。
- 本轮主线完成。NativeInput、DSP/设备输出、IL2CPP/AOT/HybridCLR热更新、其他平台和未声明可选SDK仍未验；这些是后续独立验收，不是本轮的隐藏通过项。所有改动留工作区，既有用户修改不回退。

### 下一轮：IL2CPP 与 HybridCLR 最小发布链（2026-10-31）

- 用户授权执行前两项主线：官方2022 Windows64 IL2CPP有效冒烟、真实HybridCLR metadata/热更程序集/反射泛型闭环，再按实际前置配对Unity6。不自动安装外部依赖，不接管主工程、不提交推送、不创建代理/worktree或独立Review；NativeInput/DSP/其他平台不扩入本轮。
- 环境：两版win64非Development IL2CPP模块已存在；vswhere -all确认VS2022 Community及VS18 Community/BuildTools均有x64 C++，Windows SDK存在10.0.22621.0/10.0.26100.0。主工程9.0.3的热更登记为空，不擅自配置主工程。
- 本轮保护起点SHA256：HybridCLRSettings.asset=`D5FE35AD30A9E44A8A82DF4929BE5B540E23EF564E18AC812888C1875BC4A82B`、ProjectSettings.asset=`52836F9870EC4204D26E8421BC126D40682051F7CF5F2A5C5A11E18C4A446CA4`、manifest.json=`4CD627D306FB634B5BBACB68A942AB6F9D5C81CE23FF25F337F5BF180C8DE8369`、packages-lock.json=`3DB2968E23F3562EA30F4EDBB6C4E5C9C7B33E19F73309C2AD5E495AC8078BF2A`。当前Cinemachine等版本调整为用户已有输入，不回退。
- 现有2022原生源固定il2cpp_plus=`ba3a76c9979d14cee404fb5d44301773e0768866`、hybridclr=`83929787581bce6c7219418f62348a10c33582ee`；对合入后的两个实际工作树定向git diff HEAD均无差异。Unity6已有独立ParameterOwner维护源；原Verify实际检查913文件、前后像/唯一GlobalMetadata.cpp差异和双MIT通过，manifest SHA256=`787b8659754b14b60cb2403570390007998596d708092fcd8ffa9cbc6d6563c6`。这些仅为源验证，不冒充新原生构建或运行。
- Ruling: 复用现有正式HybridCLRBuildPipeline.BuildBasePlayer、HybridCLRExampleAssets及完整样例，不恢复旧接收工程/固定日期协议，不重新写加载器。原生宿主准备只从已存在且核验的版本源通过官方local-source流程生成自有宿主本地基座，不下载、不改Editor/PackageCache/主工程Library，不混用2022与6.3，不把维护源称为未修改官方runtime。
- [x] 官方2022 IL2CPP有效Player构建、真实运行及同产物重复。
- [x] 正式HybridCLR基座、AOT metadata、热更Module/泛型/async/Prefab加载与释放闭环。
- [x] Unity6按已核实维护源及安装前置配对；明确失败与版本/变体边界。
- [x] Mono/公共结果安全回归、格式、源指纹保护和维护说明收口。
- TDD先证实旧接受器会把标成IL2CPP的报告当Mono接受，新增后端合同RED：`C:/Users/Kano/AppData/Local/Temp/kilo/yl-testing-1bfc06bd829d42538c68b1d9d429f9bf`。新增实际编译宏后端证据，不用请求参数替代真实执行后端。
- 后端合同9/9 GREEN；官方2022首次股票IL2CPP链5门通过、两次实际Player普通exit0，证据 `G:/Unity/Testing/N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-Smoke/3a4892fc42384c7d8983b7847271b9d0`。校验真实GameAssembly.dll/global-metadata.dat、实际ManagedStripped的Runtime依赖和编译宏后端；不将Mono请求或Editor模拟当原生结果。
- HybridCLR接受器单独RED（缺失专用阶段/原生证据）到GREEN，当前Results10/10。明确拒绝EditorSimulation、错误codePackage/native variant、空/多/重复metadata与热更集、未运行泛型/async/Prefab、缺失幂等/错基座保护和Null图形设备。
- 接入阶段按原门槛实际失败并修正自有工具，不弱化生产断言：overlay先一次性预检全部源码/meta，避免把本次已复制的配套meta误当外来文件；Single场景切换卸载新资产后从保存路径重载profile；显式填写Standalone targetGroup/Player subtarget。Together Bundle最长路径260，短宿主H+7位标签降为259，已存在标签碰撞一律拒绝，完整run/host身份另有绑定。
- Ruling: 新Player放入独立HP自有Cache/P，保留完整备份与所有文件的冻结指纹，不跳过过长文件或修改Mono/产品路径限制；原结果长路径下Cpp备份不能被Mono读回。内置快照完整projectId使旁车路径仍262，显式自有样例改用绑定run的hs-8位身份后实算243；baseline、runtime cache、Player三个独立Cache都有owner/lease，正式流水线仍要求全新不可重叠子目录。外部原生源/主工程/旧制品未修补。
- 官方2022 HybridCLR六门真实通过（含local source preparation），正式14阶段含最终Player/Baseline全部成功；两次普通exit0、Job activeProcesses=0，Native/非Simulation、Direct3D11、38份实际metadata清单、Entry/Support两程序集、解释器泛型反射返回3、Module/async/Prefab执行、重复load同实例、错误base提前拒绝、最终record/lease=0。证据根 `G:/Unity/Testing/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-HybridCLR-Smoke/35af4c2c138b4e15abe2bdd96ee5d02a`；代码包 `ca6a0388dee56e91f1cf516167ee8956ba549e976d4307640cdc1920ec784c37`。完整源许可随NativeInput保存，Player路径见HybridBuild/report.json，未将测试运行包当正式发布包。
- Unity6普通IL2CPP五门、两次真实运行已通过：`G:/Unity/Testing/N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-Smoke/d4545a20f4f847bbb4d718c0770ec3e8`。维护源HybridCLR仍待配对，不以普通IL2CPP成功替代。
- Unity6的ParameterOwner配对随后已真实完成六门：`Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-HybridCLR-Smoke/cb2c80aa0c964bf1a394fd2475400962`，41份metadata、两热更程序集、Native/非Simulation、泛型反射/async/Prefab、重复load与错基座保护、关闭归零，两次普通exit0。该结论仅适用于已核实维护源。
- 收口自检发现PowerShell `$true -ceq 'Mono'/'Started'/'host-1'`均为true，旧字符串比较可误收布尔身份。先新增实际RED（2/10失败，固定 `C:/Users/Kano/AppData/Local/Temp/kilo/yl-testing-1855581f49064b0cae110cf0df0f9fed`），再强制JSON身份/阶段/HybridCLR文本为string、schema为整数、计数/布尔为对应类型；全公共合同27/27 GREEN，不只是增加不匹配值测试。结果验证未通过宽松转换、旧报告或退出豁免。
- 新模板的PlayerSettings改用两版均已实际编译的NamedBuildTarget重载，避免引入Unity6已弃用API警告；未关闭诊断或修改第三方/生产代码来凑零警告。
- 最终源码冻结后每版四组共19门（普通Mono3、Mono运行5、普通IL2CPP运行5、HybridCLR6）通过；三个显式运行组每组同产物启动两次，共每版6次新Player进程，均要求普通exit0/后代清零。以下为当前终态，路径相对 `G:/Unity/Testing`，此前普通成功按保留策略轮换，不把历史路径当永久固定证据。

| 最终组 | 官方2022证据后缀 | Unity6证据后缀 |
| --- | --- | --- |
| 普通Mono，无探针 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7/228662601d4a41a8b29206015198e345` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7/dad7f041e1a64b179d74f53c62b6313f` |
| Mono，有效运行与重复 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-Smoke/47800cb90892422eae364680c77651d6` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-Smoke/e00b51fd03e44bd89c8e8d5d01a0fdb8` |
| 普通IL2CPP，有效运行与重复 | `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-Smoke/5fae02fa79924e4b8ad89940385816cc` | `N26/Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-Smoke/1a6b63261a5340538cb527f24531ba49` |
| HybridCLR，原生运行与重复 | `Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-HybridCLR-Smoke/10b05ef881be40f0a92d2b43a73afdaf` | `Results/Tooling-Distribution-6000.3.27f1_672d562a42a7-IL2CPP-HybridCLR-Smoke/bb202a32ebf74139b42749fc63d89770` |

- 最终代码包：2022=`94064eb4160e9c277f534163442b1d3dde6dcdcb86f3d0f788cc4f6253661947`、6=`c19f3b078a753d5d89162f51329776ff1b161e4ddb7a34609611b3889c5ca459`；实际运行报告为Native、Direct3D11、reflectionCount=3、message/async/prefab均为真实v1结果、repeatedLoadReused/wrongBaseRejected=true、record/lease=0。完整Player路径、baseline、源变体、38/41 metadata列表与实际PID均在回执中，不以本文常量替代机器比对。
- 最终并行回归中2022一次Export父进程exit0但后代在2秒收尾后仍活跃，真实记录为DescendantsAlive/forcedCleanup，保留 `N26/Results/Tooling-Distribution-2022.3.62f3_96770f904ca7-IL2CPP-Smoke/88589b1faa6c452794de346766b1256c`，没有放宽Job或改写成通过。Unity6完成后串行重验2022两组成功，终态如表。
- 主工程原始字节指纹在任务期间出现范围外变化，11:17后复读/最终复读一致：HybridCLRSettings=`734BFFE58FDB01C3D6A543CCAC12FC26CBDD50D4D9631794173A9AFA296A2771`、ProjectSettings=`8B832029BCCE44DDC920EE8E7DE387DEC8E29C895A481B68DBA43425AB27A122`、manifest=`05FAF8332D4D4F615BD87260DBAB5A6CAC716EBB67061FB54BEFC177FB199382`、lock=`6990025BCE8F09B74E634FF4029B399F9798B95C7B3743C57CB95B464205521D`。读取可见MCP更新到1.6.0/扩展2.1.0，HybridCLR仍9.0.3、热更名单仍空。未回退或认领这些变化，不能声称四文件自开工逐字节未变；最终输入指纹绑定当前manifest/lock，主工程设置不注入隔离宿主。本轮手工编辑只在Tools与原Plan。
- 本轮工具验证：公共27/27、NUnit host4/4（`N26/Results/Tooling-NUnitHost-2022.3.62f3_96770f904ca7/c9154d1c90434dab96f8cec4b89ea21a`）、Quick CLI5/5（`.../Tooling-QuickCLI-Quick-Reddot-2022.3.62f3_96770f904ca7/c92f75bfaa2a4227827575af74d75d88`）、Formatter保护9/9及全量1136文件Check通过；不将这些辅助合同当Unity/Player验收，也不冒称本轮重跑了既有Analyzer181或全部Daily矩阵。
- 维护指南已同步后端开关、实际编译宏/严格JSON类型、显式HybridCLR原生源前置、短缓存及Owner/Lease、阶段/回执路径、无需模拟/不卸载VM程序集等边界。主工程只读复核仍为YLFrameworkDev、isDirty=false、rootCount=2，未Play/编译/刷新；未停止用户Editor、安装外部工具链、提交推送、创建代理/worktree或独立Review。NativeInput/DSP/其他平台、任意AOT泛型与高裁剪、热更V2替换/回滚仍不属于本轮成功。本轮两项主线已完成。

### 最终结项批次（2026-10-31，按用户指定顺序）

- 用户明确追加并依次授权：两个NativeInput用例、自有代码风险导向警告收口、一次独立只读Review、当前文档终态同步及正式结项。Review仅一名内部只读审查者，不创建可见Agent Manager会话/worktree、不扩大Git/安装/场景操作权限。
- [x] 两个NativeInput原断言及双版本重复验收。
- [x] 自有生产代码遮蔽/过时API按风险收口及回归。
- [x] 一次独立Review及必要修复验收。
- [x] 当前状态/最终验收总览同步并正式结项。
- NativeInput先在batch新Special中短暂通过，但并行及串行重复均真实重现卡住；进度和逐事件日志定位到选择窗口已ShowUtility、等待布局帧阶段，尚未发送鼠标输入。未以首次成功抹掉重复失败，未改原断言或延长退出豁免。固定失败证据包括 `N26/Results/Run-Special-Behavior-6000.3.27f1/c31c0279da88497d817aac5651941e40`。
- Ruling: 两个原生图形窗口合同改为显式自有Desktop Special（-ylTestInteractive），与batch宿主分身份。请求flag、实际Application.isBatchMode、owner/request/input/result全部匹配才执行；主工程普通交互Editor无flag仍在退出/写入前拒绝。普通Daily/其他Special仍batch，结果记录真实isBatchMode。首次桌面参数曾因PowerShell单元素if输出变string而未运行目标；已用外层数组固定参数列表，缺response真实失败未报通过。
- Desktop Special两版2叶、发现/执行/同宿主重复3合同均通过，实际isBatchMode=false；相对G:/Unity/Testing：2022 `N26/Results/Tooling-UnityCLI-Special-Behavior-2022.3.62f3_96770f904ca7/8f1a728c27334307a254217230b2f057`，6 `...-6000.3.27f1_672d562a42a7/10f6402e207249cab8b1aa4affaa8d04`。这证明真实EditorWindow事件/焦点/选中/Delete及右键动作/坐标链，不等于物理OS设备自动化；桌面专项串行运行，不与其他窗口验收抢焦点。
- Behavior原Daily仍135叶，两版batch三合同通过：2022 `N26/Results/Tooling-UnityCLI-Daily-Behavior-2022.3.62f3_96770f904ca7/62ba979f02584d23be3b5beb76fd4fdb`，6 `...-6000.3.27f1_672d562a42a7/053d0b72aa1546c5bf7a79d33eeacc2d`。
- 风险收口不重命名ResourceException.Source公开API，仅明确new隐藏及XML语义。新增characterization测试先在原实现通过，验证资源SourceId与基类Exception.Source字符串、地址及InnerException独立；原Unity编译CS0114诊断是本次静态修复依据，不伪称该new关键字修复了运行行为。Action六处FindObjectsOfType(true)改为2022.3/6000.3均支持的FindObjectsByType(Include, InstanceID)，保留inactive和既有排序，不借机用None/Any改变清理顺序。
- Action两版373叶三合同通过：2022 `N26/Results/Tooling-UnityCLI-Daily-Action-2022.3.62f3_96770f904ca7/1f45c94ab05b46198a7070b95f4a1aac`，6 `...-6000.3.27f1_672d562a42a7/9a67882244a445899e1e6523ae83516d`；Resource增加1个语义合同后为27叶，两版三合同通过：2022 `Results/Tooling-UnityCLI-Daily-Resource-2022.3.62f3_96770f904ca7/bc0ef7b6aa754d09b8c8ea1449620220`，6 `...-6000.3.27f1_672d562a42a7/a5023b1a480c412bb65d49b09328e123`。实际新Unity6编译不再产生上述生产CS0114/CS0618；旧测试夹具/冻结builder及第三方文档警告按低风险保留，不关闭诊断或批量改历史协议。
- 唯一独立Review会话 `ses_e6e0c197affeY7FHS9CzO0tc6x` 只读完成（连接重置后续同一会话），无Critical、3 Important、1 Minor。审查未运行Unity/测试/编译/格式化、未写工作区或创建下级；不声称其独立复验运行证据。没有第二轮Review。
- Important 1确认并修复：DistributionEntry原catch在缺请求/错误owner/交互调用上也会Editor.Exit。只在全部归属/结果/版本/编译/链接保护通过后设置ownsInvocation，拒绝路径原样抛回调用者。新增真实Unity guard harness先在旧入口缺请求时exit1为RED，再在两版完成MissingRequest/WrongOwner/Interactive各3/3，见总览；未在用户主Editor验证危险路径。
- Important 2确认并修复：非Player的owner/workspace/host-key/input receipt/Unity response及轮换摘要原有PowerShell布尔转字符串漏洞。workspace/container/rotation新增2个合同在旧实现2/29 RED（固定 `C:/Users/Kano/AppData/Local/Temp/kilo/yl-testing-fa2e039ffae7405fb0a7587108c45fcc`），响应类型与整组模式合同缺失2/13 RED（固定 `.../yl-testing-97ee9cf450664adfaa9ae9ac98acc127`）；现边界先验string/整数，再比较值，无通用schema框架或宽松回退。公共合同最终31/31。
- Important 3确认并修复：声明模式集合检查移入正式CLI，在接受新发现、保存选集或执行前拒绝整个模式消失；未维护固定叶数。实际Safety把自有Reddot PlayMode asmdef限制Android，两版正式Discover均exit1/Failed并报告execution modes，随后还原同宿主恢复，最终安全8/8。
- Minor为当前README未列Behavior Special，本次已同步入口/桌面/串行/物理设备边界。明确搁置项裁决：历史阶段摘要不改写，仅顶部/T4/T7/当前总览同步；源码工程Domain link.xml与无验收宏普通IL2CPP组合缺实际linker证据，不升级为确认bug或宣称已验；Process WMI/breakaway限制、可选SDK/平台等按既有范围，不追加实现；用户Packages/ProjectSettings/旧数据不归责本批。不要求第二审查或以审美追加框架。
- 最终完整Special顺序曾再次真实卡在Unity6右键用例（`Results/Run-Special-Behavior-6000.3.27f1/a2787c869b0d47c6bd6523a2df0f59a0`，300秒TimedOut/forcedCleanup、后代0），不能以此前桌面单独成功结束。查官方6000.3 UnityCsReference MouseEvents.cs的ContextualMenuPopulateEvent.PostDispatch：只要menu.Count>0就显示原生菜单，不检查PreventDefault，注释要求clear+StopImmediatePropagation。测试现捕获原DropdownMenuAction列表后仅清空显示项并停止传播，后续原动作/类型/坐标/内建子状态断言逐条保留；不改生产菜单、造动作、直调工厂或发送OS快捷键。最终独立双次执行及完整Unity6 Special顺序均通过，总览给当前终态。
- 原生/分发/桌面拒绝回归当前全部完成，主计划顶部、T4/T7摘要、当前总览及维护说明已同步。后续不再将NativeInput/Session/IL2CPP列为本次未验；仍明确未验DSP/物理OS设备/IDE/其他平台/高裁剪/V2矩阵。历史阶段数字、失败、源码输入漂移与保留策略保持事实，**本批结束，本计划正式结项**。

### Git收口（2026-10-31）

- 用户在技术结项后明确授权本计划提交与正常推送。目标为当前 `integration/xf-yl`，远端 `origin`，不切分支、不合并、不强推或改写历史。
- 同一提交纳入本计划源码/测试/夹具及配套meta、统一工具与旧入口退役、维护说明与本Plan；包含官方2022 ProjectVersion、URP14.0.12/uGUI1.0.0及Unity生成的官方registry迁移区段。
- 包文件混有MCP升级，暂存采用真实git diff的精确迁移hunks，不修改工作区、不手工重写锁文件；MCP DLL/版本升级、其他计划、用户ProjectSettings/PackageManager/ProBuilder设置、YLFrameworkData与原Release82b/Workbench遗留资产均保留在工作区，不纳入本提交。
- Git收口仅改变版本控制与本节交接文字，不使现有技术验收失效，不为提交重跑原生构建或矩阵。最终Hash与远端同步结果由提交后Git事实和交付回复报告，不另建docs提交或改写历史。
- 实际暂存审计：138个变更路径，新增/修改/移动123项；Assets配套meta齐全，6个移动夹具meta与HEAD原文件逐字节一致。配置索引仅含27个Unity迁移区段，manifest/lock保持MCP原版1.5.0配对，工作区MCP升级继续保留。源码/脚本/文档diff check通过；完整check的33条尾空白仅来自11份Unity生成meta的userData/assetBundle空字段，保留原始身份与字节，不按“全检查无警告”报告，不改配置或跳过hooks。
