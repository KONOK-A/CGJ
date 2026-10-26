# YLFramework Editor Organization Implementation Plan

> **For agentic workers:** 使用 `executing-plans` 在当前工程顺序执行；共享的 Unity Editor 和资产数据库不得并发写入。用复选框记录实际完成项。当前 Plan 代理只制定计划，工程修改由可实施代理执行。

**Goal:** 将 Editor/Utilities 收敛为 Runtime/Utilities 的配套编辑器，同时完成公共支撑、独立工具和模块专属工具的目录及 namespace 归位。

**Architecture:** 保留现有 Foundation/Core/Systems/Utilities 模块对应关系；新增 Common 和 Tools 两个职责明确的目录，不新增程序集。Common 提供 Editor 机制和预览桥接契约，Systems 持有领域策略及 Resource 预览实现，Tools 只承载无领域归属的独立命令。

**Tech Stack:** Unity 2022.3.62f3c1、C# 9、现有 YLFramework.Editor、Unity Test Framework 1.3.9；仓库格式工具使用 PowerShell 7.6。

**Spec:** 本会话用户要求“制定整理计划，然后实现”，并明确选择“同步整理命名空间”。执行时遵守 `Assets/YLFramework/AGENTS.md` 和 `Assets/YLFramework/Documentation/CodingStyle.md`；现有 Architecture 的旧目录描述属于本次同步对象。

## Global Constraints

- 这是经用户选择的 Editor 公共 API namespace 变更；更新仓内调用方，文档说明外部源码需要更新 using/全限定名，不保留旧 namespace 包装类或 CLR 别名。
- 保留所有既有文件和可整体移动目录的 `.meta`/GUID、类型名、成员名、访问级别及行为。新增目录和夹具由 Unity 生成新 GUID。
- 全部普通 Editor 代码继续归属 Editor-only 的 `YLFramework.Editor`；不改 asmdef/asmref、Runtime、依赖、脚本后端或可选集成边界。
- 不改菜单字符串、剪贴板协议、资源地址、设置存储根、派生设置类型身份、序列化字段或现有 FormerlySerializedAs。
- 不顺手修复工具命名、参数拼写、格式、默认值、异常或存储策略；没有 Editor 实现的 Runtime 模块不建空目录。
- 已有文档去重、导出清单修改和其他用户修改均保留，在现有差异上增量修改。尤其不动 `ProjectSettings/ProjectSettings.asset`、未跟踪夹具、YLFrameworkData 和 HybridCLRSettings。
- 使用当前工作区和已有 Unity 实例；不创建/切换分支，不自动提交、推送、保存脏场景、重导入全工程或修改 Unity/MCP 配置。

## Review Focus

- namespace 变化后，旧 SplitWindow 资产及 TreeView 状态仍能反序列化，不能只验证“新类型保存后再加载”。
- Settings 基类 namespace 可变，但 `typeof(T).FullName` 中的具体派生类型不变；现有设置不得成为孤儿或被默认值覆盖。
- Resource 仍通过委托安装 Common 的预览解析器，不能将公共桥接改为直接初始化 Runtime 或依赖具体 Resource 实现。
- 领域资产删除/重命名的所有权和 Undo 语义不变，借用资产不被误删；独立菜单路径不变。
- 每个原 GUID 只对应一个新位置；默认导出通过 Editor 根包含新目录，不遗漏新工具或夹带测试。

## 1. 固定迁移表

以下源路径相对 `Assets/YLFramework/Editor/Utilities/`，目标路径相对 `Assets/YLFramework/Editor/`。原 namespace 均为 `YLFramework.Editor.Utilities`。

| 源文件或目录 | 目标文件或目录 | 目标 namespace |
|---|---|---|
| `Assets/AssetNameTools.cs` | `Common/Assets/AssetNameTools.cs` | `YLFramework.Editor.Common` |
| `Assets/EditorAssetTool.cs` | `Common/Assets/EditorAssetTool.cs` | `YLFramework.Editor.Common` |
| `Serialization/` 两个扩展文件 | `Common/Inspector/` | `YLFramework.Editor.Common` |
| `Scene/HandlesExtensions.cs` | `Common/Scene/HandlesExtensions.cs` | `YLFramework.Editor.Common` |
| `Settings/` 两个文件及其中 Attribute | `Common/Settings/` | `YLFramework.Editor.Common` |
| `TreeViews/` 两个文件及 TreeViewStateInfo | `Common/TreeViews/` | `YLFramework.Editor.Common` |
| `Windows/SplitWindow.cs` | `Common/Windows/SplitWindow.cs` | `YLFramework.Editor.Common` |
| `Menus/` 两个菜单文件 | `Tools/` | `YLFramework.Editor.Tools` |
| `Assets/DomainAssetOwnership.cs` | `Systems/Shared/DomainAssetOwnership.cs` | `YLFramework.Editor.Domain` |
| `Assets/ScriptableObjectExtensions.cs` | `Core/ScriptableObjectExtensions.cs` | `YLFramework.Editor.Core` |

- Common 的子目录只划分文件职责，不继续增加 Assets/Settings 等 namespace 层级。
- `Utilities/Overlap`、`Utilities/CharacterController2D`、`Utilities/MoveablePlatform` 保持路径、GUID 和现有模块 namespace，只更新其 Common 引用。
- HandlesExtensions 虽然当前只有 Overlap 消费，但只封装 Unity Handles 绘图，不含 Overlap 类型或策略，保留为 Common/Scene 的现有绘图能力，不新增抽象。
- EditorAssetTool 保留类名及 PreviewResolver 契约。ResourceEditorSimulation 安装解析器，其他 Editor 通过 Common 桥接消费；不改装配方向。
- 先移出 Assets 中的 Domain/Core 两个专属文件，再整体移动余下 Assets 目录；其余目录整体移动。`Serialization -> Common/Inspector`、`Menus -> Editor/Tools` 均保留原目录 GUID。只为 Common 新建目录，不为旧目录留下兼容空壳。

## 2. 执行与验收任务

### Task 1: 建立迁移前证据与失败断言

**Files:** `Tests/EditMode/Editor/Core/CoreEditorIdentityTests.cs`、`Tests/EditMode/Editor/Distribution/FrameworkStructureTests.cs`、`Tests/EditMode/Editor/Help/HelpWindowTests.cs`；以下路径均在 `Assets/YLFramework/` 下。

- [x] 检查工作区基线、目标路径冲突、Unity 编译/运行状态和脏场景。脏场景不自动保存；若阻碍测试，报告并暂停相关验证。记录上述 14 个脚本及整体移动目录的真实 GUID，固化到测试期望，不能从迁移后的目标动态取期望值。
- [x] 在现有 `YLFramework.Tests.EditMode` 中新增 `Tests/EditMode/Editor/Common/EditorCommonStateFixture.cs`，主类型为 `YLFramework.Tests.EditorCommonStateFixture : ScriptableObject`，仅作为序列化测试宿主。用 SerializeReference 字段分别持有 TreeViewStateGroup 和 TreeViewStateInfo，不新增 asmdef，不进入分发包。
- [x] **在原 namespace 尚未修改时**，通过 Unity 生成 `Tests/Fixtures/NamespaceMigration/EditorCommonStateBefore.asset` 和 `SplitWindowBefore.asset` 及 `.meta`。前者的分组键为 42，选择为 73，展开项为 99，独立 info.id 为 17；后者的 HorizontalSplitPercent 为 0.57。夹具只保存本次创建的对象，不保存用户窗口或场景。
- [x] 确认树夹具确实包含旧 namespace 的托管引用类型记录，记录预期字段值；不得在改名后生成新数据再手写成“旧数据”。后续测试只操作临时副本，不重写种子夹具。
- [x] 扩展现有结构/身份断言，以字符串反射检查迁移表中的新 FullName、路径和原 GUID。新增 namespace/路径断言应因目标尚不存在而失败，而不是因测试直接引用未定义类型而编译失败；记录实际 Red 结果。

### Task 2: 迁移文件、namespace 与所有消费者

**Produces:** 表中所有目标路径和 namespace；方法签名、程序集、GUID、菜单和运行行为保持不变。

- [x] 使用 Unity AssetDatabase.MoveAsset 按表迁移，核对每次返回值；Common 新目录由 Unity 创建。发现目标已存在或 GUID 非预期时停止，不覆盖、强制合并或删除未知文件。
- [x] 在同一小批次修改 14 个脚本的 namespace、必要的 MovedFrom 和消费者引用，覆盖同文件内的 PreviousProjectSettingsNameAttribute、TreeViewStateInfo 等附属类型。SplitWindow、TreeViewStateGroup、TreeViewStateInfo 的 `[MovedFrom(true, sourceNamespace: "YLFramework.Editor.Utilities")]` 必须与其 namespace 修改一起落地，不能先让缺少迁移标记的新身份成功编译/Domain Reload。保留三个 Runtime Utilities 配套目录的 namespace。
- [x] 逐处更新源码、测试、夹具的 using、全限定名和反射字符串。范围包括 Editor/Core、Editor/Systems、三个配套模块，以及 Tests/EditMode 和 Tests/Fixtures/Core/Editor/CoreSmokeAssetFixture.cs。DomainAssetOwnership 的调用改到 Editor.Domain；ScriptableObjectExtensions 的调用改到 Editor.Core，不能把旧 Utilities 一律替换为 Common。
- [x] 同步 `Tools/Behavior/Import-BehaviorSources.ps1`、`Tools/Domain/Import-DomainSources.ps1` 中生成 Editor using 的模板，使其生成 Common 引用。只更新相关模板，不运行整套导入器，不覆盖已融合源码。
- [x] 本批 namespace、迁移标记和消费者更新完整后再主动刷新、等待实际 Unity 编译完成，中途不主动刷新或运行测试。若出现引用或类型歧义，沿真实消费者修正，不增加兼容包装类、public 暴露或新程序集。

### Task 3: 保住序列化和设置合同

**Files:** 新 `Editor/Common/Windows/SplitWindow.cs`、`Editor/Common/TreeViews/TreeViewStateGroup.cs`；既有 HelpWindowTests、CoreEditorConfigurationTests、BuffSettingsMigrationTests。

- [x] 核对上一任务已将 MovedFrom 与 namespace 修改一并应用到 SplitWindow、TreeViewStateGroup、TreeViewStateInfo，沿用程序集及类名。无需给普通静态工具、Attribute 或未改变身份的具体派生设置类型批量添加迁移特性。
- [x] 在 HelpWindowTests 中增补从两份真实旧夹具加载的回归：临时副本在导入后类型正确、无丢失托管引用、树选择/展开/id 与分栏比例不变；保存副本并重导入仍一致。保留原有旧字段名比例测试的 0 与 0.57 两个用例。
- [x] 验证原 namespace 不再存在 CLR 类型定义或包装类，MovedFrom 不当作 Type.GetType 旧字符串别名；旧 namespace 可保留于迁移 Attribute、真实旧数据、负向断言和明确的迁移说明。
- [x] 保持 `ProjectSettings/YLFramework/Core/{typeof(T).FullName}.asset` 公式、BuffSystemSettings 及两个设置测试派生类型的 FullName、旧名称 Attribute 字符串和 SettingsProvider 页面路径不变。沿用既有加载/保存、旧路径拒绝与显式迁移测试，不移动/重命名用户设置文件。

### Task 4: 同步路径合同与现有文档

**Files:** 以下测试、清单和文档，除 Tools 外均相对 `Assets/YLFramework/`。

- [x] 更新 CoreEditorIdentityTests 的类型/路径/GUID 映射，将 namespace 期望改为每项显式归属，不再使用“非 Foundation 即 Utilities”的推导；保留原 GUID、唯一类型、根 asmdef、Player 隔离和旧 Core 别名不存在的断言，覆盖 Common、Tools、Core、Domain 的本次迁移项。
- [x] 更新 `Tests/Fixtures/NamespaceMigration/ProductMoves.json` 中 ScriptableObjectExtensions、EditorAssetTool、Settings、DomainAssetOwnership 四项的最终 To 路径。保留 From 和 Guid，其余 24 项不变；这是净迁移映射，不追加指向过渡 Utilities 路径的重复 GUID 记录，既有 28 项数量断言无需放宽。
- [x] 更新 `Tests/EditMode/Editor/Distribution/ExportUnityPackageTests.cs` 的实际导出哨兵路径，覆盖 Common/Tools 与归位后的 Core/Domain 文件，继续核对包内路径、GUID、脚本和 meta 字节。默认导出仍为当前六个目录及 LICENSE.md、AGENTS.md 两个根文件；不要恢复上一轮已移除的根 CodingStyle.md。
- [x] 从 `Tests/EditMode/Editor/Domain/DomainBatchAcceptance.cs` 和 `Tests/Tools/Extensions/Domain/Editor/DomainMainAcceptance.cs` 移除旧 DomainAssetOwnership 单文件路径特判，新位置已由 Systems/Shared 范围覆盖；不缩小领域验收范围。
- [x] 同步 ReorderableTreeViewTests、HelpWindowTests、ResourceEditorSimulationTests、EquipmentAuthoringTests 等中的反射类型名和泛型引用；已有测试类身份、测试目录不为本次整理额外重排。
- [x] 修改现有 `Documentation/Architecture.md`、`Documentation/Modules/Attributes.md`，说明 Common/Tools 与模块对应区的边界、目录与 API namespace 对照、四类 namespace 迁移以及外部源码的 using 更新要求。按实际引用更新现有说明，不新建重复迁移文档，不修改 AGENTS.md 或 CodingStyle.md。

### Task 5: 分层验证与交付

- [x] 静态检查活动源码、模板和文档中的旧 using、全限定名与过时路径。保留受解释的旧数据/历史 From/负向断言，不以旧字符串零命中代替验证；确认 Utilities 下只余三个配套模块，所有新目录和文件 meta 齐全，移动对象 GUID 唯一。
- [x] 通过当前 Unity 实例执行实际编译及相关 EditMode 测试：CoreEditorIdentityTests、CoreEditorConfigurationTests、CoreSerializedPropertyTests、FrameworkStructureTests、ExportUnityPackageTests、ReorderableTreeViewTests、HelpWindowTests、ResourceEditorSimulationTests、BuffSettingsMigrationTests、DomainAuthoringTests、DomainMainAcceptanceTests、DomainReviewAssetTests、EquipmentAuthoringTests、DisableEditDrawerTests。选择这些 fixture 组成单次过滤运行，避免为每个文件重复全量编译；若依赖要求扩大范围，记录原因。执行结果及未通过项见下方记录，不声明全绿。
- [x] 真实旧夹具必须跨至少一次脚本重编译/Domain Reload 后再次读取。检查已有 Skill/Buff/Mission 等窗口的树状态消费、预览装配与订阅生命周期；只打开和关闭本次拥有的测试窗口，不关闭用户窗口或覆写当前布局。
- [x] 用 PowerShell 7.6 解析两个导入脚本；验证其模板不再生成旧 using，不执行会写入源码的导入主流程。复用真实导出测试核对包内新路径和 GUID，Editor/Tools 保持原菜单字符串，Distribution 的 ExecuteMenuItem 调用仍可找到原菜单注册。菜单通过编译后实际 MenuItem 元数据匹配，不为验证而启动系统 Explorer。
- [x] 对本次改动的 C# 显式限定路径运行格式 Check：`pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Mode Check -Paths <changed-path> [additional-paths...]`。不全库 Apply；仅在确属本次触及内容时定向修正格式。运行 `git diff --check` 并审阅完整差异及新增 meta。
- [x] 做一次独立 Review，重点检查五项 Review Focus、真实旧数据证据、所有权、命名、无关差异与导入模板遗漏。修复本次引入的问题，既有不相关失败单独报告，不弱化断言。
- [x] 在本 Plan 记录实际完成项、测试结果与阻塞，简要交付新目录/API 对照和验证范围。未经另行请求不 commit/push；不把“计划验证”报告为“已验证”。

## 3. 失败处理与范围边界

- 迁移失败时记录已经成功移动的路径和当前 GUID，先处理明确的本次冲突；不以删目录、重建 meta、硬回退或覆盖用户修改来恢复。需要回退本批时也只能反向移动本批自有修改并保留同一 GUID，不进行破坏性 Git 操作。
- 旧数据若出现 Missing Script/managed reference、窗口值丢失、设置路径变化，视为阻塞，不自动清空状态、重建设置或批量重序列化掩盖问题。
- Unity 不可用、现有编译错误或脏场景阻碍验证时，明确暂停对应验收，不另起同工程 Unity 实例；仅继续不依赖该结论的静态检查。
- 不调整 Runtime 模块、Help/Distribution 所有权、第三方代码、程序集依赖、Core 生成存储根、菜单名称、类名或成员名；不执行全套源导入、HybridCLR Prepare、Player 发布或平台升级。

## 4. 实施依据

- 本计划已通过静态源码核对：CoreEditorIdentityTests 固定旧路径和 GUID；FrameworkStructureTests 通过 ProductMoves.json 校验最终位置；Domain 两处验收已覆盖 Systems/Shared；两个导入模板仍生成旧 namespace。
- 当前 Plan 阶段未运行 Unity 编译、测试或资产迁移。上一轮导出测试结果不能作为本次 namespace 迁移后的验证。
- Unity 2022.3 官方序列化说明：<https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html>。
- Unity 2022.3 MovedFrom 源码：<https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Runtime/Export/Scripting/APIUpdating/UpdatedFromAttribute.cs>。只填写发生变化的 sourceNamespace，程序集和类名保持不变；仍须以上述真实旧数据验证实际结果。
- Context7 当前未提供可读取资源或资源模板，本轮版本敏感依据使用对应版本的 Unity 官方文档及一手源码。

**Open Questions:** 设计与实现已完成。验收剩余为既有分析器制品不一致和专用 Protection 入口前置条件，不声明完整发行验收通过。

## 5. 执行记录

- Ruling: 按批准计划在当前 integration/xf-yl 工作区顺序执行，以本 Plan 作为持久进度记录，不额外创建工作树、分支或重复 ledger；未授权的提交/推送不执行。
- Pre-flight: Task 1 的原 namespace 夹具和 GUID 快照必须先于 Task 2；Task 2 的新 namespace、MovedFrom 和消费者必须同批落地；Task 3 消费旧夹具，Task 4 更新净路径合同，Task 5 验证完整结果。方法签名、程序集及数据路径无跨任务冲突。
- 初始 Unity 证据：2022.3 工程处于 Edit Mode、未编译、未导入、无编译错误；YLFrameworkDev 场景非 dirty，四个迁移目标均不存在。已有工作区差异保持不动。
- Task 1: complete。Unity 运行 NeutralEditorHelpersKeepTheirGuidsAndSingleEditorOwnerAtTheDeclaredPaths，1/1 因 Common/Inspector/SerializedObjectExtensions.cs 尚不存在而失败，符合 Red 预期。两份真实旧夹具由 Unity 创建，树种子含 Utilities namespace 的 Group/Info 托管引用；窗口比例 0.57。资产 GUID 分别为 8b22906969d1b5148831d1f8485b23da、15a3cfad3e8049f40b4239b8b2404e17。
- Task 1 Ruling: Unity 2022.3 的 MonoScript.GetClass 对 ReorderableTreeView 泛型返回 null，对设置基类文件返回同文件的 Attribute。泛型身份通过显式 FullName、程序集、脚本路径/GUID 和 asmdef 校验；不以 GetClass 返回泛型作为不可能成立的合同。非泛型继续严格校验脚本与类型一致。
- Task 2: complete。AssetDatabase 完成 9 项文件/目录移动，所有 GUID 原样保留；namespace、MovedFrom、消费者及两处导入模板已同步。实际 Unity 编译后，结构/身份回归 1/1 Green，涵盖 14 个迁移脚本、7 个原目录、旧身份缺席及 Editor/Player 边界。
- Task 3: complete。当前 Unity 对 HelpWindowTests、CoreEditorConfigurationTests、BuffSettingsMigrationTests 合并运行 58/58 Passed（8.25s）。真实旧树/窗口夹具在 namespace 变更及多次 Domain Reload 后导入、保存副本、再次导入均保留值；原种子字节不变。既有比例 0/0.57、项目设置及 Buff 设置迁移回归保留并通过。
- Task 4: complete。最终路径合同、14 项导出哨兵、两处领域验收范围及现有文档已同步。FrameworkStructureTests 与 ExportUnityPackageTests 合并运行 8 Passed / 2 Failed；真实包递归、文件/meta 字节及结构检查通过，失败均因原有分析器 build-info.json 缺失，发行收据门禁在 Task 5 单独核对，不修改断言。
- Task 5 Ruling: 原 build.ps1 不带 -Deploy 运行，构建与 181 个分析器测试通过并生成真实收据；新构建 SHA256 为 5E5811DB535431E406DC768EF4633CF27786A824CC3BDD8F013ABB605CF6BDE4，仓内既有 DLL 为 4C621B4EEA949B5DB5C192BAD6373723E4C944A421170EAC88DEDC48C8E37DB3。遵守本次不改 Runtime/不覆盖冻结制品的边界，不部署新 DLL、不伪造收据；最终报告该既有发行制品不一致，而非扩展本次目录整理。若判断错误，代价是发行门禁仍不能通过，后续需独立核对并授权制品更新。
- Task 5 Ruling: MCP 长回归发生请求超时，缓存仍为上一轮，不能把超时或缓存当作新证据。读取当前 UTF API 确认前一轮已结束后，保持原 14 个 fixture 选择重新运行，并用独立 UTF 完成回调将真实报告保存到 Temp；不修改测试，不伪造 XML。代价是为取得可靠报告重复了一次无法取证的运行。
- 完整报告：`Temp/YLFramework.EditorOrganization-3ddc7c395a8e42ad94c724c24d0fe60d.xml`，14 fixture，174 用例，169 Passed / 5 Failed / 0 Skipped。Core 配置15、Core身份21、SerializedProperty16、DisableEdit3、Buff设置12、Domain作者19、Domain资源11、Equipment9、TreeView11、框架结构2、Help31均通过。
- 完整报告失败1：`ExportUnityPackageTests.DeployedAnalyzerMatchesTheLastVerifiedSourceBuild`，原因是上文两份 DLL SHA256 不一致。真实包递归、GUID、文件/meta 字节和 SourceBuildGate 输入集合防护均通过，不放宽发行断言。
- 完整报告失败2：`ResourceEditorSimulationTests.SimulationFactorySharesTheExplicitImagePolicyWithItsTextureSources` 捕获 IDE 生成 `Assembly-CSharp.csproj` 时的 sharing violation，并非 Common 预览桥断言失败。该 fixture 单独通过同一 UTF API 重跑，报告 `Temp/YLFramework.EditorOrganization-Resource-e2a69f2db4124da0aa1376aad069e6c8.xml`：8/8 Passed（79.07s），未忽略异常日志。仅清理本次失败清理留下的 `Release82b-eb3389d715024466b469c2b3dcd25dc1`（GUID 346573f1876f0184da4ad1b789579a77），未收编原有夹具。
- Task 5 Ruling: 原计划把 `[Explicit]` 的 DomainMainAcceptanceTests 放入普通 MCP 选择不合适。其保护开关、专用空场景和恢复 owner 只能由原 Protection 入口建立，而该入口写入工程外固定 Evidence 目录并接管设置保护，不能在本次目录整理中擅自启动。该类8项不作为普通迁移通过证据；代价是专用保护集成验收仍需在获授权的正确前提下独立运行。
- 专用 Protection 报告中3项前置失败：`SaveAssetsKeepsAnUnownedMaterialDirtyAndItsDiskBytesUnchanged`、`ScheduledFailureRecoveryRetainsItsOwnerUntilUtfIsInactive`、`TestRunnerEntryUsesAnOwnedEmptySceneInsteadOfOriginalAutoRunHosts`。没有设置假的 SessionState、关闭断言或调用外部 Evidence 写入流程。
- 汇总口径：完整报告中去除上述8项 Explicit 专用用例，余166项普通迁移回归；以独立资源8项复跑结果覆盖其原记录，166项中165项有通过证据，唯一未通过为分析器发行一致性。此为两份真实报告按完整用例名去重汇总，不是声称某一次运行166/166全绿。
- 静态与格式：PowerShell 7.6 在内存中执行两个导入模板的实际 AST assignment，输出 Common using，未执行导入主流程；74个本次相关C#显式路径 Format-YL Check 通过，未执行 Apply。两个修改文档的45个本地链接、19个新位置资产 meta 和三个 Tools 菜单注册检查通过；`git diff --check` 无错误，`git diff --exit-code -- Assets/YLFramework/Runtime` 无差异。
- Final Review: 独立只读审查完成（ses_e86331db7ffetWgUN3fQpGtMQf），Critical / Important / Minor 均无 findings。审查逐项比较14个迁移脚本与旧路径源码，14份脚本及7份目录 meta 的 Git blob hash 相同，21个原GUID均唯一；未进行第二次审查或新增修复。
- Final Ruling: 审查暂不判断的冻结分析器问题沿用前述不部署决定；对初始已有文档去重、ProjectSettings、既有夹具等仅保护，不替它们作正确性结论。代价是本次验收不覆盖这些既有修改本身的业务正确性。
- Deferred minors: 无。实现验收结束时尚未提交；后续 Git 操作按用户最新明确授权执行，交付仍区分实现完成与剩余验收边界，不宣称全部门禁通过。
- Task 5: complete（验证执行与边界记录完成，不表示全部发行门禁通过）。最终 UTF 不活跃，Unity 未编译/导入且无编译错误；原 YLFrameworkDev 场景保持打开且非 dirty。Runtime、所有 asmdef/asmref 无差异；已有 ProjectSettings/数据/夹具改动未被接管。

## 6. Git 收口

- 用户在实现验收后明确授权本地提交并远端推送；目标为当前 `integration/xf-yl` 到 `origin/integration/xf-yl`，不切换分支、不合并、不强制推送。
- 本批统一收录本会话的规范去重与 AI 规范入口、Editor 目录及 namespace 整理，以及直接关联的源码消费者、导入/导出工具、测试、真实旧夹具、全部配套 meta、现有文档与本 Plan。两项工作共享导出清单和测试，采用一个完整提交。
- 排除其他专项 Plan、ProjectSettings、YLFrameworkData、HybridCLRSettings、历史 Data/Release82b/Workbench 夹具及无关未跟踪内容；不使用全工作区 add，不更新冻结分析器 DLL，不纳入 Temp 构建收据或测试报告。
- 本次仅复用有效技术证据并核对 Git 范围，不重复技术验收；提交与推送不代表上文未通过的分析器发行门禁或未建立前提的专用 Protection 验收已经解决。
- 暂存检查发现 Unity 原生生成的新 meta 与两份真实旧种子存在空值字段的 `key: ` 尾空格。保留原始数据字节，逐项核对诊断只涉及这些生成字段；源码、文档及其余差异单独严格检查。不为 Git 格式提示手工改写旧 YAML，不修改 Git 配置或弱化技术测试。
- 提交 Hash 和远端同步结果以实际 Git 日志及交付回复为准，不为回填 Hash 另建文档提交。
