# YLFramework 结构与命名空间实施计划

> 状态：T0–T5 实现及约定范围的验收交付完成。新 HybridCLR 原生闭环、Buff 旧设置迁移、默认 r3 双 Player/Save、GUI/TLS 定向门禁及两项 Review 所有权修复均已完成；默认七程序集原3240动态case保持，新增74case单列。此前受权限限制的两个真实file symlink场景已由用户管理员隔离补测，两轮均2/2通过、0跳过、外部目标与Source不变，原Skipped记录保留。历史原T0环境未知、旧完整混跑失败、Source TLS旧崩溃及重建采集退出挂起仍明确保留，不将其改称全绿。用户已另行授权按本任务范围在当前分支提交并推送，Git结果以实际提交/远端核验及交付回复为准；不接管旧H26/冻结基座。Q18 仅下放四项，StateMachine 保留。
> For agentic workers: 当前使用 `executing-plans` 内联执行；不并发修改 namespace 及其消费者。执行记录见 §9。

**Goal:** 将 YLFramework 整理为适合多项目复用、整体交付、按需使用的统一 Unity 框架，使目录、职责、命名空间、公开契约与实际依赖一致。

**Architecture:** 保留 Runtime、Editor 两个主体程序集及 Foundation/Core/Systems/Utilities 四个职责分区。目录承担组织职责，namespace 表达稳定功能域；可选集成保持独立边界，不为目录形式增加程序集、全局服务或第二套生命周期。

**Tech Stack:** Unity 2022.3.62f3c1、C# 9、现有 UGUI/Json.NET/BouncyCastle、Unity Test Framework、现有 Roslyn 分析器与 HybridCLR 9.0.3 可选集成；不升级工具链或依赖。

**Spec:** 本文件中的决策树、目标映射、迁移约束与验收范围，来源为本次 Q1-Q16 及 Q18 访谈。Q17 目录预览引出的调整以 Q18 最后选择为准；执行者无需从历史 Plan 推断本次范围。

## 1. 已确认的决策树

```text
统一框架，整体交付、按需使用 [Q1]
|-- 完整结构规范化，不改变功能行为 [Q2]
|   |-- Runtime/Editor 等用途优先的顶层 [Q3]
|   |-- Foundation/Core/Systems/Utilities 四区 [Q4]
|   |   |-- 下放 Overlap/CharacterController2D/MoveablePlatform/Excel [Q18]
|   |   |-- StateMachine 保留 Systems；FSM 保留 Utilities [Q18]
|   |-- 功能域 namespace，不机械复制目录 [Q5]
|   |-- 保留两个主体程序集及必要外围程序集 [Q6]
|   |-- public 契约与内部实现分离 [Q7]
|   |-- 具体功能域命名 [Q15]
|   |-- Editor 局部归位与命名 [Q16]
|-- 允许源码级身份升级，保护已有资产和数据 [Q8]
|   |-- 主体测试按模式、职责、模块组织 [Q9]
|   |-- 基础层严格单向，系统协作明确登记 [Q10]
|   |-- 保留资源键、交付方式及可选边界 [Q11]
|   |-- 工程内与干净接收工程的真实验收 [Q12]
|   |-- Resource/Audio 桥接集中并拆分文件 [Q13]
|   |-- 现用内容统一迁移，冻结历史不重写 [Q14]
|-- 完整计划交回审阅；实施须另行授权
```

Q10 首次提问被关闭，未作为批准；仅采用随后用户明确确认的“基础边界严格、系统协作明确”版本。

Q18 抽象标准的首次提问未获选择；随后具体名单中，用户明确选择“只移动四项，保留状态机”。不把五项移动推荐、子代理提出的 Logging/Reddot 下放或此前全留 Systems 的位置表当作最终决定。

## 2. 全局约束

- 范围包括自有 Runtime、Editor、Integrations、Tests、Samples，以及受影响的生成器、分析器、源码消费者和正式文档。
- 不重写系统，不改变初始化、所有权、取消、异常、回滚、缓存或退出语义，不升级依赖。
- 保留 `YLFramework.Runtime`、`YLFramework.Editor` 的程序集名称、现有身份和必要外围程序集；不为每个目录或系统新增 DLL。
- 允许 namespace 与必要 public/internal 调整造成源码级升级；同步已知消费者，不创建旧 namespace 转发壳、永久别名或旧 DLL 兼容层。
- 类型短名默认保留；确有误导的改名必须明确列入最终映射，不借机全库改名。
- 本计划没有批准批量改类型短名；`UniRxAction`、`ResService*` 等也不在本轮顺手改名。
- 移动已有脚本/资产必须携带 `.meta`、保留 GUID；新文件及目录由 Unity 生成新身份。
- 保留现有资源逻辑键、序列化字段、存档键、Excel 列名等数据合同；类型身份字符串按显式映射迁移并验证。
- 不覆盖已有导出包、冻结验收制品或 HybridCLR 基座；受影响制品使用新输出，并重新证明兼容性。
- 保留 UnityPackage 交付方式；不改成 UPM，不批量搬业务资产，不移动宿主商业插件，不改第三方 namespace、源码和许可证。
- 不未经授权创建/切换分支、提交或推送；实施前重新读取实际 Git 状态，保护用户与并行改动。
- 同一工程不启动第二个 Unity 实例；不自动保存用户脏场景。接收工程使用独立工程路径及明确的验收输出所有权。
- 仅本计划文件在规划阶段可写；产品源码与其他文档均未获本轮执行授权。

## 3. 目标组织

```text
Assets/YLFramework/
  Runtime/
    Foundation/
    Core/
    Systems/
      Action/
      Audio/
      Buff/
      DataAssets/
      Equipment/
      Http/
      Localization/
      Logging/
      Mission/
      Reddot/
      Resource/
      Save/
      Skill/
      StateMachine/
    Utilities/
      CharacterController2D/
      Excel/
      Extensions/
      FSM/
      Grid/
      Json/
      MoveablePlatform/
      Overlap/
      Table/
      UI/ColorSelector/
      ZipTool/
      StringTools.cs
    YLFrameworkRoot.cs
    YLFramework.Runtime.asmdef
    csc.rsp
  Editor/
    Foundation/
    Core/
    Systems/
    Utilities/
    Help/
    Distribution/
    YLFramework.Editor.asmdef
  Integrations/<接入名>/
  Prefabs/
  Plugins/
  Samples/
  Tests/
    EditMode/{Foundation,Core,Systems/<模块>,Utilities,Editor}/
    PlayMode/{Foundation,Core,Systems/<模块>,Utilities}/
    Integrations/<接入名>/{EditMode,PlayMode}/
    Acceptance/
    Fixtures/
    Tools/
  Documentation/
```

目录中的花括号仅描述分类，不创建字面目录或空目录。Runtime 与 Editor 的同一功能采用一致归属与名称；模块主入口靠近模块根，内部仅按实际职责细分，不统一套用 Interfaces/Managers/Models/Helpers 模板。

### 3.1 职责与依赖

| 分区 | 职责 | 依赖约束 |
| --- | --- | --- |
| Foundation | 事件、池、异步、计时、更新、绑定等共享机制 | 不引用 Core、Systems、Utilities、可选集成或框架根宿主 |
| Core | Module/MVC/View/Panel/UI 生命周期与消费合同 | 依赖 Foundation；通过自身窄合同消费系统能力，不引用具体 Systems |
| Systems | 领域服务、业务规则和统一协调能力；用户明确保留的配置/可视化 StateMachine | 可消费 Foundation、Core 合同、适合的 Utilities；跨系统关系逐项明确 |
| Utilities | 按需组合的工具、数据结构、扩展、查询、通用组件和描述性合同 | 默认中立；允许局部状态及组件生命周期；ColorSelector 的 Core 依赖明确登记，Core 不反向依赖控件 |

Runtime 不引用 Editor 程序集或可选适配；已有 `UNITY_EDITOR` 分支维持 Player 隔离。沿用并更新 Foundation/Core 源码闭包守卫，为有实际风险的其他边界添加定向检查，不另建通用架构治理框架。

有 Editor、MonoBehaviour、静态入口或较多代码，都不是自动归入 Systems 的充分条件。Action 的统一执行器、Http 的传输会话、DataAssets 的注册与资源租约、Reddot 的多来源共享状态、Logging 的全局 handler 接管/恢复仍是服务责任；本轮均留 Systems。StateMachine 留在 Systems 是用户对完整配置/作者工作流的明确产品归类，不将此扩展为“带 Editor 的模块都属于系统”。

### 3.2 Namespace 规则

- 正式 API 使用 `YLFramework.<功能域>`，例如 Events、Timing、Core、Audio、Resource；不机械包含 Runtime/Foundation/Systems/Utilities。
- Editor 使用 `YLFramework.Editor.<功能域>`；同名编辑器类型保留必要功能域隔离，不全部压进同一 namespace。
- 可选适配使用 `YLFramework.Integrations.<接入名>`，必要时有明确的领域/Editor 子域。
- 模块内部 Config/Requests/Components 等目录不自动产生 namespace；仅实际独立使用或消除歧义时细分。
- `Actions` 等避免实际类型遮蔽的名称保留合理复数；不批量把第三方身份改成 YL 规则。
- 下表按源码所有者映射，不对 `YLFramework.Core` 或 `YLFramework.Foundation` 做全库字符串替换。同一旧 namespace 下的不同职责会迁往不同目标。
- asmdef 的 `name`/GUID 保持；`rootNamespace` 只是新建脚本默认值，可按所属程序集的新默认域同步，不把它误作编译后的类型身份。

#### Runtime 精确目标

下表 namespace 均省略共同前缀 `YLFramework.`；模块内部已有必要 `.Internal` 子域保留相对结构。

| 当前源码目录（相对 Runtime；目录目标另见下表） | 旧 namespace | 目标 namespace |
| --- | --- | --- |
| Foundation/Async | Foundation | Async |
| Foundation/Attributes | Foundation.Attributes | Attributes |
| Foundation/Binding | Binding | Binding |
| Foundation/Events | Events；局部 Unity Listener 使用 Core | Events；Listener 同归 Events |
| Foundation/IOC | Core.IOC | DependencyInjection |
| Foundation/Pool | Core.Pool | Pooling |
| Foundation/Serialization | Foundation | Serialization |
| Foundation/Singleton | Core.Singleton | Singletons |
| Foundation/Timing | Timing；TimerObject 使用 Core | Timing；TimerObject 同归 Timing |
| Foundation/Update | Foundation | Update |
| Foundation/Utilities | Foundation | Foundation（少量共享机制工具，目录不移动） |
| Core 的 Modules/MVC/UI/ResourceAccess/Events | Core | Core |
| Core/Attributes 中的分析器 marker | Core.RoslynAnalyzers | Core.RoslynAnalyzers |
| Systems/Action | Actions | Actions |
| Systems/Audio | Services.Audio | Audio |
| Systems/Resource | Services.Resource | Resource |
| Systems/Save | Services.Save | Save |
| Systems/Http | Services.Http | Http |
| Systems/Skill、Buff、Mission、Equipment | SkillSystem、BuffSystem、MissionSystem、EquipmentSystem | Skill、Buff、Mission、Equipment |
| Systems/Reddot | ReddotSystem | RedDots |
| Systems/CharacterController2D | Character.Controller2D | Character2D |
| Systems/MoveablePlatform | Platform2D | Platform2D |
| Systems/Localization、DataAssets、Excel、Logging、StateMachine、Overlap | 各同名功能域 | 保持 |
| Utilities/Extensions | Utilities.Extensions | Extensions |
| Utilities/Grid | Utilities.Grid | Grids |
| Utilities/Table | Utilities.Table | Tables |
| Utilities/FSM | FSM | FSM（不与可视化 StateMachine 合并） |
| Utilities/UI/ColorSelector | Core | UI（允许依赖 Core Panel 的控件，不移动目录） |
| Utilities/ZipTool 的 YL 自有包装 | Utilities.ZipTool | Compression |
| Utilities/ZipTool/ICSharpCode.SharpZipLib | 现有 Utilities.ZipTool.* | 原样保留上游子树、身份和许可证 |
| Utilities/Json | Utilities.Json | 原样保留 JSONObject 来源代码和身份 |
| Utilities/StringTools.cs | Utilities | Utilities |
| YLFrameworkRoot.cs | YLFramework | YLFramework（共享装配宿主，不归 Foundation） |

`RedDots/Grids/Tables/Singletons` 避免 namespace 与 Reddot、Unity Grid、Table、Singleton 类型同名；`Character2D` 避免重复的 `CharacterController2D.CharacterController2D`。同时导入 Unity/YL 对象池仍可能有同名类型，调用处使用明确限定/using 别名，不复制类型解决歧义。

#### Editor、Integrations、Tests/Samples

| 范围 | 目标 namespace（省略 YLFramework.） |
| --- | --- |
| Core 专属 Editor | Editor.Core |
| Foundation 的 Drawer、序列化和监听器 Inspector | Editor.Foundation |
| 通用 Assets/Settings/Serialization/Scene/TreeViews/Windows/Menus 工具 | Editor.Utilities |
| 下放到 Utilities 的独立功能模块的 Editor | 仍用 Editor.<功能域>，如 Editor.Overlap、Editor.Character2D；不混入通用 Editor.Utilities |
| 共享 Help，含 FoundationToolsHelpProvider | Editor.Help；既有 Help.Api 保持 |
| Distribution | Editor.Distribution |
| 各 Systems 的 Editor | Editor.<Runtime 功能域>，如 Editor.Skill、Editor.Character2D、Editor.DataAssets |
| 跨玩法专用生命周期和作者支持 | Editor.Domain（仅 §3.4 列明的共享支持，不是通用底层） |
| Integrations/DOTween 的运行时扩展 | Integrations.DOTween |
| Integrations/XFAB/Audio | Integrations.XFAB.Audio（保持） |
| Integrations/Excel/Editor | Integrations.Excel.Editor |
| Integrations/DomainExcel/Editor | Integrations.DomainExcel.Editor |
| Integrations/HybridCLR Runtime/Editor | Integrations.HybridCLR / Integrations.HybridCLR.Editor（保持） |
| Tests 与 Samples 自身类型 | 保留原 namespace 与类型短名，只改消费者引用和确实变化的目标类型字符串 |

不同领域的 `ConfigsWindow/ListenerFileEvent/ScriptBindController` 不合并到同一 Editor namespace。迁移 `Core.*`、`Utilities.*` 等相对限定表达式时按真实类型解析，必要时用完整名称或 using 别名，防止解析到 `YLFramework.Editor.Core/Utilities` 自遮蔽。Help provider id、菜单、EditorPrefs/PlayerPrefs 键保持。

#### Systems/Utilities 目录调整（Q18 最终选择）

路径相对 `Assets/YLFramework`，现有目录连同全部内容和 `.meta` 整体移动：

| 当前 | 目标 |
| --- | --- |
| Runtime/Systems/Overlap | Runtime/Utilities/Overlap |
| Runtime/Systems/CharacterController2D | Runtime/Utilities/CharacterController2D |
| Runtime/Systems/MoveablePlatform | Runtime/Utilities/MoveablePlatform |
| Runtime/Systems/Excel | Runtime/Utilities/Excel |
| Editor/Systems/Overlap | Editor/Utilities/Overlap |
| Editor/Systems/CharacterController2D | Editor/Utilities/CharacterController2D |
| Editor/Systems/MoveablePlatform | Editor/Utilities/MoveablePlatform |

Excel 没有对应现用的 Editor 模块目录，不创建空目录；后端仍在 Integrations/Excel。对应纯模块测试归 `Tests/<模式>/Utilities/<模块>`，跨模块验收保留真实共同职责，不机械拆散。三个组件模块之间没有必须同时搬迁的运行时依赖；Excel 下放后 DataAssets->Utilities/Excel 仍符合依赖方向。

StateMachine 的 Runtime/Editor 仍在 Systems，相关单模块测试仍归 Systems/StateMachine；轻量 FSM 保留 Utilities/FSM。目录下放本身不增加新 namespace 变更，统一采用已确认命名表中的 Overlap、Character2D、Platform2D、Excel 及 Editor 对应域。

### 3.3 Resource/Audio 桥接

将 `Runtime/Systems/Audio/ResourceAudioClipProvider.cs` 的两个类型拆到：

```text
Runtime/Systems/Resource/Integration/Audio/
  ResourceSystem.Audio.cs        # namespace YLFramework.Resource
  ResourceAudioClipProvider.cs   # namespace YLFramework.Audio
```

现有文件与 `.meta` 随 provider 移动，ResourceSystem partial 为新增文件。`ResourceStartup.cs` 保留在 Resource/Integration。保持 `GetAudioClipProvider` 调用形式、同 System/Project 的 Provider 引用身份、关闭后失效、缺失项目导致启动失败及原回滚语义。这里是明确的内建装配关系，不为消除 Resource->Audio 引用引入工厂服务或改变生命周期。

### 3.4 Editor 目录迁移

路径均相对 `Assets/YLFramework/Editor`；移动现有文件均携带原 `.meta`。

| 当前 | 目标 |
| --- | --- |
| Core/Extensions/ScriptableObjectExtensions.cs | Utilities/Assets/ScriptableObjectExtensions.cs |
| Core/Utilities/Preview/EditorAssetTool.cs | Utilities/Assets/EditorAssetTool.cs |
| Core/Settings/ProjectSettingsScriptableObject.cs、ProjectSettingTools.cs | Utilities/Settings/ 下对应同名文件 |
| Utilities/Help/FoundationToolsHelpProvider.cs | Help/FoundationToolsHelpProvider.cs |
| Systems/Skill/DomainAssetOwnership.cs | Utilities/Assets/DomainAssetOwnership.cs |
| Systems/Skill/DomainSessionLifecycle.cs | Systems/Shared/DomainSessionLifecycle.cs |
| Systems/Skill/SkillAuthoring.cs 内的 DomainResourceIconGui | 拆到新文件 Systems/Shared/DomainResourceIconGui.cs；SkillAuthoring 本身留在 Skill |
| Systems/StateMachine/Listtener/ | Systems/StateMachine/Listeners/ |
| Systems/StateMachine/GUI/Custom Inspector/ | Systems/StateMachine/GUI/Inspectors/ |
| Systems/{Buff,Mission,Equipment,Skill}/Other/ | 各模块的 Listeners/ |

`DomainAssetOwnership` 是通用资产归属工具；`DomainSessionLifecycle` 直接协调 Skill/Buff/Mission/Logging，`DomainResourceIconGui` 依赖 Resource 作者数据，后两者不是 Core 或中立 Utilities。Core 中仅保留默认资源、模块生成、Core UI 迁移与 Core Help 等专属职责。

资产创建的既有 `GeneratedAssetRoot` 和项目设置存储根不随源码移动改写。`ProjectSettingsScriptableObject<T>` 使用 `typeof(T).FullName` 构造文件名，因此派生 Editor 类型的 namespace 变化必须纳入身份迁移，保护旧配置值，不能默默创建空设置覆盖用户配置。

Resource 的 `Service/` 按下表消除混合分类，namespace 全部保持 `YLFramework.Editor.Resource`，类名、菜单和数据合同保持：

| 目标（相对 Editor/Systems/Resource） | 从 Service 移入的文件名（均为 .cs，携带 .meta） |
| --- | --- |
| Workbench/ | ResourceAnalysisView、ResourceArtifactTable、ResourceAtlasView、ResourceBuildView、ResourceBuiltInScope、ResourceBundleTable、ResourceDefinitionView、ResourceDeliveryView、ResourceDiagnosticsView、ResourceEntryTable、ResourceGuideView、ResourceOperationConfirmation、ResourceProjectActionsPopup、ResourceProjectPicker、ResourceReleaseTable、ResourceStoreQueryView、ResourceWorkbenchGui、ResourceWorkbenchView、ResServiceAssetsMenu、ResServiceEditorWindow |
| Inspectors/ | ResourceAtlasConfigurationEditor、ResourceBuildRecipeEditor、ResourceImageEditor |
| Build/（已有） | ResourceBuildProgress、ResourceBuildSourceLocator |
| Resource 根部 | ResourceEditorOperations、ResourceEditorSimulation |

`ResourceBuiltInScope` 是交付页摘要，依赖 Workbench GUI，不能归 Publishing 造成反向依赖。同步 `ResourceGuideView` 的代码导航路径、`ResourceEditorTests` 和 `ExportUnityPackageTests` 的路径断言；正式 Resource 文档更新现行路径，历史 Plan 不重写。

### 3.5 测试、验收与夹具

以下路径相对 `Assets/YLFramework`。普通测试依职责进入新目录，保留测试类型 namespace；不得因为移动跨过 asmdef 而无声改变测试集合。

| 当前 | 目标与程序集策略 |
| --- | --- |
| Tests/EditMode 与 Tests/PlayMode 的普通功能测试 | 各模式内 Foundation、Core、Systems/<模块>、Utilities；Editor 行为进入 EditMode/Editor/<功能> |
| Tests/Foundation/EditMode、PlayMode | Tests/<模式>/Foundation；保留 YLFramework.Foundation.Tests.<模式> |
| Tests/Core/EditMode、PlayMode | Tests/<模式>/Core；保留 YLFramework.Core.Tests.<模式> |
| Tests/Core/Editor | Tests/EditMode/Editor/Core；保留 YLFramework.Core.Editor.Tests |
| Tests/Core/Fixtures/ | Tests/Fixtures/Core/；Editor 子程序集 YLFramework.Core.Tests.Fixtures.Editor 保持名称与 Editor-only |
| Tests/Core/Acceptance/ | Tests/Acceptance/Core/；保留 YLFramework.UI.Acceptance 的独立 all-platform 边界，不并入 TestRunner 程序集 |
| Integrations/DomainExcel/Tests/Editor | Tests/Integrations/DomainExcel/EditMode；保留 YLFramework.Domain.Excel.Tests 和宏约束 |
| Tests/Integrations/XFAB/Audio/PlayMode | Tests/Integrations/XFAB/PlayMode；保留 YLFramework.Audio.XFAB.Tests.PlayMode |
| Tests/Integrations/DOTween、HybridCLR | 当前模式层级已符合目标，保留必要子目录、Resources 和程序集 |
| Tests/<模式>/Extensions 中 Data、Domain、Equipment、Behavior | 按实际覆盖进入 Systems 或 Utilities 的模块；四个下放模块测试跟随 Utilities，StateMachine 测试仍归 Systems；混合验收不复制或机械拆分 |
| Tests/Fixtures/Extensions/**/Player、Editor | 已在 Fixtures 根下，保留其独立程序集、平台/宏、路径与历史制品边界；不归普通单元测试 |
| Tests/Tools 与仓库 Tools 的 SDK/测试项目 | 不移动主入口，只同步现用路径和新身份 |

根 `YLFramework.Tests.EditMode/PlayMode` 继续覆盖普通子目录。Foundation/Core 嵌套 asmdef 的名称保留；原根测试归入这些域时逐文件核对实际依赖，必要的测试引用可精确补齐，但不放宽产品源码闭包守卫。真正的跨层消费者测试放对应消费方或 Editor 分组；仅在保留原程序集归属确有必要时使用定向 asmref，不为每模块新增 DLL，不制造程序集循环。

关键路径一起更新：`CoreEditorIdentityTests`、`CoreDefaultResourceEditorTests`、`CoreSerializedPropertyTests`、`YLCoreSmokeModule.FixtureRoot`、`UIPanelAssetMigration` 的 Core fixture 路径、`ResourceWorkbenchUXEvidence`、`UIPanelAcceptanceBuild`、`RuntimeAcceptanceDriver`、Stage9/ResourcePlayer 脚本的保护清单。父目录可移动，`Resources/YLResource81` 与 `Resources/YLHybridCLRTests` 之下的逻辑键保持。

## 4. 公开范围

- public 保留业务入口、必要数据/合同、正式扩展点；内部调度、缓存记录、事务状态及辅助实现默认 internal/private。
- 不因仓库内零调用就收紧既有 public；先检查跨程序集使用、合法继承、反射、序列化、Editor 和分析器。
- 只对证据充分的候选收紧可见性；明确记录保留公开的理由。不能为了消除编译报错把实现一律改回 public。
- `internal` 在同一 Runtime 内不构成目录隔离；依赖方向仍由守卫验证。
- 不新增无必要接口或程序集；现有必要友元与测试访问范围显式登记，不向游戏程序集开放全部内部实现。
- 每项可见性调整附“旧签名/新签名/真实消费者/保留或收紧理由”；证据不足的既有 public 留公开并记录，不静默推断仓库外无人使用。不修改业务可继承钩子的行为。
- 保持必要的跨程序集扩展入口：Core 的 Module/View/Panel 业务扩展、Audio 的 IAudioClipProvider、Action 的第三方扩展合同、Resource 显式接入、Excel 转换器与领域派生配置。具体签名以当前实现和真实消费者为准。

## 5. 迁移风险与验证重点

| 输入/失效方式 | 预期保护 | 验证归属 |
| --- | --- | --- |
| Prefab UnityEvent 的旧类型全名/AQN | 目标、方法、参数与调用行为保留，不只检查脚本 GUID | 持久身份迁移与游戏 Audio 回归 |
| 领域配置/Excel 类型全名与白名单 | 精确迁移身份值，保持列名、字段别名与绑定语义 | 配置迁移及 Excel/DomainExcel 回归 |
| 分析器静默找不到 metadata 名或私有/internal 符号 | 新身份下真实正例无误报、反例仍触发诊断 | 分析器源码重建及真实消费者验证 |
| IL2CPP 裁剪与动态解析 | 更新 `link.xml` 等身份映射，动态消费者运行成功 | 新接收工程 IL2CPP Low Player |
| Provider 身份、启动失败与回滚 | 单实例 Provider 缓存及原错误时机不变 | AudioResourceProvider/ResourceStartup 回归 |

另外专项覆盖派生 Editor 设置类型全名导致的文件路径变化，以及移动测试跨过嵌套 asmdef 导致的漏编译、漏发现或循环依赖。

重要事实：当前游戏主要消费 YL Audio，Module/Panel 仍来自 XF；不得顺带迁移游戏框架。UnityEvent 类型字符串、配置 full name、`Resources.Load` 键、分析器符号与 `.meta` GUID 是不同合同，不能相互替代验证。

## 6. 实施顺序

所有步骤均在用户后续授权实施后执行；不是当前已完成事项。按“映射、代码、持久身份、消费者、验证”成组切换，每批结束恢复可编译状态，不留下新旧 namespace 双轨。生产代码与其必要调用方不能被委派给互相不知道映射的并行改写者。

### T0：确定输入与迁移保护

**涉及：** 本计划；`Runtime/Editor/Integrations/Tests/Samples`；`Assets/Games/MainModule`；`Tools/YLFramework.Analyzers`；现用验收脚本。

- [x] 检查当前分支、Git diff/untracked 和 Unity 状态；标明已有用户改动。不要把最初分析时的干净/脏状态当实施时状态。
- [x] 按 §3 固定所有者映射规则并保存全量旧类型、程序集、脚本/GUID 清单；精确到文件的目标映射在每批修改前展开并绑定哈希，见 §9 裁定。混合 namespace 按类型所有者处理，不作全库前缀替换。
- [x] 在旧身份仍可解析时保存源码、资产、UnityEvent/领域绑定的原始文件及 Editor 设置、测试发现基线。快照仅写本次自有输出；各批数据迁移前仍须读取并验证其真实拥有者。
- [x] 建立迁移安全规则：先预览范围、检查脏场景和目标冲突、再精确修改；未知类型、外来文件、无对应 GUID 或双重设置文件冲突必须拒绝并报告，不静默清空或回落默认值。
- [x] 记录实际 Unity 编译与相关既有测试的起点；已区分场景保护导致的既有批量运行失败与新身份红态，不预先削弱行为断言。

**产物：** 当前版本的精确映射与可恢复基线，增量附在本计划实施记录中；无产品行为变化。这里的盘点是执行数据，不重新开启已确认架构决策。

### T1：Runtime 功能域与已知消费者切换

**修改：** §3.2 Runtime 表对应源码、`Runtime/Systems/Save/link.xml`、所有受影响 using/完整名/反射入口；`Assets/Games/MainModule` 的 YL Audio 消费者、Samples、Integrations 与测试；`Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs`。

**合同：** 类型短名及成员签名默认不变；所有者仍是现有 Runtime；新 namespace 与旧身份移除按表执行。

- [x] 在现有身份/边界测试中补目标全名的正例和已迁旧名的负例，用反射/源码探针避免测试本身提前无法编译；确认旧代码在这些新身份断言上失败。
- [x] 按 Foundation、Systems、Utilities 的有界批次切换；每批同时更新所有已知消费者、Help/XML 索引、代码生成模板、可选集成的 namespace 和默认 `rootNamespace`。Core 主体 namespace 保持，不重复迁移未变身份。
- [x] 更新分析器的 Pool 等 metadata 名并从源码重建/部署 DLL；不手改二进制，不改私有字段或分析器规则语义。`CoreUsageAnalyzer` 找不到符号会静默停用，必须验证诊断仍触发。
- [x] 同步 Excel 的 namespace 白名单、full name/AQN 类型列解析目标、DomainExcel 的动态目标名、Save 的 link.xml。保持列名别名、ID/枚举、存档短类名键和数据协议。
- [x] 迁移真实持久类型值：游戏 Audio UnityEvent、领域 Script 绑定、受影响 Unity 资产及工具配置。只处理映射中的实际拥有者，保留方法/参数/fileID/GUID；必要 Unity 序列化迁移信息仅服务真实存量数据，不建立旧 C# API 壳。
- [x] Unity 重导入后，验证迁移幂等、无缺失脚本/断开事件、旧配置值可读；运行身份、Foundation/Core 边界、原游戏 Audio、配置/Excel、Save 相关回归。非法 namespace 自遮蔽用正确类型解析修复，不改语义绕过。

**关键现有入口：** `FoundationBoundaryTests`、`CoreIdentityTests`、`CoreArchitectureBoundaryTests`、`ApiHelpTests`、`YLAudioWiringTests`、`YLAudioGameFlowPlayModeTests`；Excel/DomainExcel 的既有导入导出测试。

### T2：文件归位与 Editor 身份切换

**修改/移动：** §3.2 的四个 Runtime 模块及三个 Editor 模块下放；§3.3 两个桥接文件；§3.4 全部路径；对应 `.meta`、引用、代码导航和路径断言。

**合同：** 不改变 Resource Provider、Startup 或 Editor 写操作/Undo/设置的行为；新建两个拆分文件仅组织既有类型。

- [x] 保留或补充 Provider 同实例、会话结束失效、缺失项目失败及回滚断言；按 §3.3 移动 provider 并拆出 ResourceSystem.Audio partial。
- [x] 按 Q18 下放四个 Runtime/三个 Editor 目录并保留 GUID；同步普通源码探针、生成器、导出与现用 Tools/Domain、Tools/Behavior 路径。验证 Utilities 新成员不引用 Systems/Core 实现，不能仅移动目录却破坏已确认依赖边界。
- [x] 按 §3.4 移动通用 Editor 工具、共享领域支持与 Resource 工作台文件，拆出 DomainResourceIconGui；清理仅因本次移动产生的空目录及其原 meta，不删除资源数据。
- [x] 同步 Editor namespace/消费者和 Help 目标全名；保持菜单、Provider id、显示配置与业务扩展合同。项目设置类型改名先对旧值做受控迁移，再启用新身份加载，不用空设置替换旧值。
- [x] 更新 `ResourceGuideView`、导出路径测试、Core Editor 工具身份与文件路径断言；验证 CustomEditor/Drawer 发现、实际预览、窗口与设置重载、Undo 和 Help 索引。
- [x] 运行 `AudioResourceProviderTests`、`ResourceStartupTests`、`ResourceStartupExampleTests`、`AudioNamedLoadingTests`、`AudioHelpTests`、`AudioSourceIndependenceTests` 和相关 Resource/Core Editor 测试。
- [x] 复用检测/角色/平台的现有 Behavior 合同与 Editor 回归，以及 Excel 字段转换和 DataAssets/DomainExcel 消费者验证；下放不能改变检测、移动、搭乘或导入导出结果。

Editor namespace/消费者已迁移；Buff 现有精确旧路径拒绝守卫、显式备份/移动/重载入口，并已用真实旧 namespace 类型生成的数据完成新 r3 的原字节、三字段、GUID 引用及幂等消费验证。T0 主工程没有旧文件，仓库外任意存量工程并未被本任务实际迁移；不宣称通用自动兼容。

### T3：收敛实现可见性

**涉及：** T0 映射中的自有实现候选、`AssemblyInfo.cs`/精确 IVT、AnalyzerSymbols、相关消费者探针。

- [x] 依 §4 对候选逐项分类；在本计划实施记录列出实际收紧集合及保留公开理由，不把命名为 Internal 的目录当自动授权。
- [x] 先建立代表性外部消费者：Module/View/Panel 派生类、Audio provider、Action/DOTween 扩展、Resource/Excel/领域配置接入应可编译；明确内部实现的非法外部访问应不可编译。
- [x] 只收紧证据充分的类型/成员；保持 Unity 组件身份、序列化资格、反射目标、回调和合法继承。必要 Editor/Tests 访问沿现有明确友元处理，不向游戏或任意插件扩大内部访问。
- [x] 同时运行新旧消费者、API Help 与分析器正反例；更新合理改变的公开面基线，但不能以删除消费者、削弱断言或停用诊断取得通过。

### T4：统一测试布局与现用工具

**移动/修改：** §3.5；`Tests/Tools/Invoke-Stage9Delivery.ps1`、`Prepare-ResourcePlayerAcceptance.ps1`；受影响现用 `Tools/Behavior`、`Tools/Equipment`、`Tools/Domain` 路径。

- [x] 移动前后对照测试类型/方法发现集合、程序集归属、define 与平台约束；先按职责安排普通测试，再检查嵌套 asmdef 接管范围。既有测试程序集名称保留，必要引用精确更新，产品边界守卫不放宽。
- [x] 迁移 Core Editor、Core Fixtures、Core Acceptance 及现用路径常量；资源父目录可变，Resources 下逻辑键不变。保留 Player 夹具独立编译边界及已有 Fixtures 子结构。
- [x] 原 `Extensions/Behavior` 的混合验证保持跨模块验收身份；单域 StateMachine 测试归 Systems，Overlap/CharacterController2D/MoveablePlatform 测试归 Utilities。历史 B0 白名单/SHA 不改写为新基线。
- [x] 修正当前 Stage9 自动化需要使用的入口：旧脚本按 `ExportRoots` 和 7 项解析，而当前 exporter 是 `_exportRoots`、6 目录加 3 文件。新增正负用例覆盖真实根列表、缺失/额外根及排除项，不能仅把计数改为任意值。
- [x] 不执行绑定历史 HEAD/冻结交付的 `Finalize` 来验收新结构；新验收绑定本次源码/包 hash 和独立输出，保留旧记录。保护路径随现用夹具更新，不删除保护机制。
- [x] 比较迁移前后测试发现结果，运行主体 EditMode/PlayMode 和本机已安装、受影响的可选集成测试。HybridCLR 测试先用当前程序集执行原 `HybridCLRTestFixtures.Prepare()`，不能复用旧 bytes/manifest。

布局静态身份和实际运行证据均已按§9保存，补充完成当前完整动态树及T0源受控重建多重集对照，不以静态总数冒充动态叶节点。上方组合条目表示所列发现/执行工作已实际进行，不把旧完整EditMode混跑、Source TLS原生中断或重建进程退出挂起改称全绿；当前定向隔离复验、发现等价和剩余风险分别记录。

### T5：文档、分发与独立验收

**修改：** `Documentation/Architecture.md`、`Documentation/README.md`、受影响 `Documentation/Modules/*.md`、可选集成 README 与既有生成模板/验收消费者；本计划实施记录。

- [x] 将正式文档更新为当前四区、namespace/公开范围/依赖关系与迁移用法；修正 Timing 的导航归属，明确 Core、桥接例外和第三方身份。历史 Plan/冻结制品原样保留。不主动改 AGENTS、权限或工具配置。
- [x] 用实际 Exporter 生成新的包；核对 6 个目录加 3 个根文件、原 GUID、唯一 Analyzer、第三方许可证及排除项。Tests/Integrations/游戏/项目数据/商业插件仍不进入默认包。
- [x] 在新的、自有接收目录进行干净首次导入；只安装框架必需依赖，不复制主工程 manifest、Library、DLL 或 XF 栈补洞。产品第一次编译成功后才安装验收 harness。
- [x] 使用代表性消费者验证 Module/UI、Resource、Audio、Save、生成代码及持久事件，完成 Windows Mono 与 IL2CPP Low 的构建、独立 Player 运行及退出日志核验。不把编译或启动画面当运行成功。
- [x] 可选集成分别验证新 API；HybridCLR 的 AOT API/泛型/裁剪输入若改变，必须使用新基座/metadata/补丁闭环，主体程序集名称相同不代表旧基座可复用。
- [x] 检查实际 diff、资产身份、发现测试集合、文档链接和未完成门禁，完成独立 Review；报告已实现/实际验证/未验证。没有另行授权不 commit/push。

## 7. 验证入口与完成条件

### 已有入口

执行环境使用 Windows PowerShell 7.6；下列命令是实施时入口，本次规划未执行。先确认工具已安装和目标父目录存在，不为满足命令自动升级环境。

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Mode Check
pwsh -NoLogo -NoProfile -NonInteractive -File Tools/YLFramework.Analyzers/build.ps1 -Deploy
```

分析器脚本锁定 .NET SDK `9.0.312`，执行 restore/build/test；测试项目是 `Tools/YLFramework.Analyzers/Tests/YLFramework.Analyzers.Tests.csproj`。产物由脚本部署到原 `Runtime/Core/Analyzers/YLFramework.Analyzers.dll`，检查脚本退出码、构建记录与 Unity 实际诊断，不手改生成产物。

Unity 测试优先复用本项目 `tests-run` 能力；连接实际已打开的该工程，检查无编译错误及无脏场景，不自动替用户保存。若 CLI 已可用，可通过 JSON stdin 调用，例如：

```powershell
$request = @{ testMode = 'EditMode'; testAssembly = 'YLFramework.Foundation.Tests.EditMode'; includeMessages = $true }
$request | ConvertTo-Json -Compress | unity-mcp-cli run-tool tests-run --input-file -
```

按各任务目标替换 testAssembly/testClass，再运行主体及受影响集成的完整测试集合；使用实际返回结果核对发现/执行/通过/失败/跳过，不用固定历史数量验收。测试入口不可用时使用已有 Unity Test Runner，不安装新工具或另开同工程实例绕过限制。

独立交付入口保持 `Assets/YLFramework/Tests/Tools/Invoke-Stage9Delivery.ps1`；先完成 T4 的入口修正和回归。其 `DeliveryRoot` 必须位于仓库外且有正确 owner 标识，`Receiver/Run/BuildRun/Store` 必须标识本次新验收，不覆盖旧输出。顺序为：

```text
保护来源输入与确认 owner
-> Initialize / PublishTools / AuditPackage（适用的当前入口）
-> Create -> Import
-> 产品首次编译通过 -> InstallHarness
-> Editor Audit -> Generate -> VerifyGeneration
-> Editor BuildMono -> Player(Backend Mono, Command smoke)
-> Editor BuildIl2Cpp -> Player(Backend Il2Cpp, Command smoke)
-> 与本次身份变化相关的代表性场景、退出日志和输入保护复核
```

脚本已支持 `Operation`、`Method`、`Backend` 等参数，但当前旧 `AuditPackage/Finalize` 不能直接作为绿色证据；不得盲用历史文档命令或把恢复旧运行包装成本次新运行。CLI/工具命令仅描述入口，编译、测试、Player 运行和迁移验证分别记录。

### 完成条件

- Unity 实际编译、受影响 EditMode/PlayMode 测试、架构守卫和格式检查。
- 资产重导入、持久 UnityEvent、领域类型绑定、GUID 与资源键检查；不修改用户未保存场景。
- 生成代码的真实消费者编译，分析器重建及正反例，不能只核验 DLL 存在。
- 默认包在新干净接收工程中首次导入，并进行代表性 Windows Mono 与 IL2CPP Low 构建/运行。
- 可选集成分别验证；HybridCLR 的受影响基座合同用新制品验证，不沿用旧通过记录或覆盖旧基座。
- 不外推移动平台、IL2CPP High、全部人工玩法、听感或原生泄漏结论；阻塞项明确报告，不制造通过。

本节记录批准时的计划与完成条件；后续授权实施及真实编译、测试、Player 构建/运行的证据见§9，不能把规划时未执行的说明当作当前状态。

## 8. 决策状态与交接

Q1-Q16 的主干决策与 Q18 的最终四项下放选择均已明确，当前无待决的模块归属。此前五项移动推荐已被用户选择替代；完整目录、namespace、公开范围、迁移与验证步骤以本文件整合版本为准。

第三方重整、UPM、系统行为重设计、游戏 XF 迁移、全平台验收和 Git 写入均明确不在本次默认范围。用户随后已明确授权实施本计划，未授权提交或推送。

## 9. 执行记录

### T0 基线与裁定

- 分支 `integration/xf-yl`；起始 HEAD `c7922424036ef89edd080af8b63c07ac23146250`。不创建 worktree、分支或提交；以本计划维护执行账本，不再创建 SDD 文档体系。
- 起点已有修改：生态融合旧 Plan、`.mcp.json`、三个游戏 FSM 资产；已有未跟踪 `Assets/Test`、WorkbenchUX1 的 AlternateContent.meta、HybridCLRSettings.asset。均保留，不回退或混作本任务产物。
- Unity 初始状态为 EditMode、无编译/更新，打开的 `Tests/Core/Acceptance/UIAcceptance.unity` 不脏。
- 旧身份快照：`C:/Users/Kano/AppData/Local/Temp/kilo/yl-structure-1792757470714`，owner.json 绑定本计划和上述 HEAD。记录 161 个 Editor 编译图节点、6362 个已加载 YL 类型、1249 个 MonoScript 身份；保存 3724 个源码/数据/元数据文件及 SHA-256。基线只复制到自有目录，不保存用户场景。
- 快照包含 `assemblies-before.json`、`types-before.json`、`scripts-before.json`、`scenes-before.json`、`file-hashes-before.json` 及 `before/`。ProjectSettings/YLFramework/Core 当时为空，没有旧设置资产需要凭空迁移。
- 起点 YL 测试发现：Core.Editor 52、Core.EditMode 69、Core.PlayMode 238、Foundation.EditMode 191、Foundation.PlayMode 43、根 EditMode 1471、根 PlayMode 509、DOTween PlayMode 73/EditMode 1、XFAB PlayMode 1、HybridCLR EditMode 72/PlayMode 25。
- 首轮实际基线测试：250 项，246 通过，4 项因其他测试执行期间 UIAcceptance 场景变脏而在保护性 SetUp 中失败：TimerSessionTests 的真实双 Play、AudioResourceProviderTests 的两个真实会话用例、ResourceStartupTests 的会话所有权用例。未修改或绕过保护断言；调用结束后场景恢复不脏。
- 独立重试上述会话 fixture 的 MCP 请求超时，结果未知，不能记录为通过。用户中断期间升级了 MCP 1.3.0->1.4.0、NuGet DLL 和 UNITY_MCP_DEPS_13->15；这些新改动保留。恢复时 Unity 空闲，旧 Test Runner 结果缓存已清空。后续验证使用当前环境，不将旧重试结果外推。
- Ruling: 使用模块级、可检查的身份批次，而不是一次性全库切换；完整旧 namespace 分区可机械迁移，混合分区按类型所有者处理。每批同步调用方和分析器并验证，减少错误半径；不得机械重写已冻结记录或协议键。

### T1 当前进度

- `01-pooling` 完成：`YLFramework.Core.Pool` -> `YLFramework.Pooling`，目录、GUID、类型短名和程序集未变。预览及逐文件 SHA 位于 `steps/01-pooling.json`，本批原文件副本位于 `steps/01-pooling-before/`。
- RED：新增 `FoundationBoundaryTests.PoolingUsesFunctionalNamespaceWithoutLegacyAliases` 后，10 项中 9 通过，唯一本断言因新全名不存在失败。旧名负例用分段常量保持，未被机械迁移抹除。
- 受控批量改动 65 个源码/当前文档文件，随后定向修正 Help 的相对 `Core.Pool` 引用、AnalyzerFixture/OwnershipRulesTests 的相对 `Pool` 引用以及文档措辞。第三方、历史 Plan、用户现有资产和冻结制品未参与此批。
- 首次分析器重建发现合成 Core 夹具仍引用相对 Pool，未部署失败产物；修正真实夹具引用后，SDK 9.0.312 构建 0 Warning/0 Error，129/129 测试通过并部署。分析器 SHA-256 `B9A4B4BA55FF2D68BCB1833057AADB662E5DDC44D005E1D47AFAD2B0B965969E`。
- GREEN：实际 Unity 编译后，FoundationBoundary/ObjectPool/PoolContract/CollectionPool/CoreArchitecture/CoreIdentity/CoreLoadViewArgs/CoreTemporaryCollection/ApiHelp 共 193/193 通过，无跳过。
- 批次写入时临时抑制 AutoRefresh；完成源与分析器更新后已归还并清除 `YL.Structure.1792757470714.AutoRefreshOwner`。当前不持有刷新锁。
- Ruling: T0 精确目标清单随已定义身份批次展开，而非先一次性改完全部类型；旧状态已完整保留，每批预览检查所有源哈希后才写入。代价是多次编译，收益是隔离混合 namespace/字符串消费者风险。
- `02-singletons` / `03-dependency-injection` 完成：分别迁移 79 / 17 个文件到 `YLFramework.Singletons` / `YLFramework.DependencyInjection`。修改前确认这些旧全名未出现在仓库现有 asset/prefab/unity/json 中；未改字段、宿主行为、目录或 GUID。
- 上述两个身份批次的 RED 为 14 项中 4 个新全名不存在；GREEN 为 Unity 实际编译后 87/87 定向 EditMode 测试，以及 6/6 `SingletonHostPlayModeTests`。批次刷新抑制已释放，没有遗留刷新锁。
- `04-attributes` 完成：63 个文件迁移到 `YLFramework.Attributes`，同步真实 Drawer/作者工具。RED 17 项中 3 个新全名不存在；GREEN 为 49/49 实际 Unity EditMode 测试，包含 Foundation/Core 守卫和 DisableEdit Drawer/Core Editor 身份。
- `05-foundation-families-v2` 完成：58 个文件，按类型所有者拆出 Async/Update/Serialization；ApplicationTool/AssemblyTool/LayerMaskUtility 保持 Foundation。RED 24 项中 8 项因新身份/新字典 namespace 尚不存在而失败；GREEN 99/99 EditMode（包含实际 Drawer、JSON/Save、旧字典资产）及 Foundation 全程序集 43/43 PlayMode。
- 字典旧数据证据：通过旧 `YLFramework.Foundation.SerializedDictionary` 创建并卸载/重载 `Tests/Fixtures/NamespaceMigration/SerializedDictionaryBefore.asset`，保存 Direct/Derived/Nested/ObjectKeys 和自身引用。资产 GUID `c920a96e550145e47b63934ef3cc76ea`，原夹具脚本 GUID `da82f70412bbaab439cd654edba33760`。在新身份加载后全部值和引用通过；与自有外部快照比较，asset 与 meta 字节完全相同。未新增迁移兼容壳。
- `05-foundation-families.json` 是被拒绝、从未应用的首版预览：仅按短类型名加 using 会误碰游戏现用的 XF 同名类型。修正为必须具有原 Foundation 导入/作用域，实际使用 v2 预览及输入/输出 SHA 双重检查；两个游戏源码文件保持无 diff。
- Ruling: DDOL 宿主名不是 CLR 身份。CoroutineStarter/UpdateManager 的两个既有宿主字符串及其行为断言保持原样，只迁移类型与引用；不以旧字符串零命中作为成功标准。
- 所有上述批次的 AutoRefresh 抑制均已归还；最后验证时 owner 为空、Unity 非 Play/非编译，`git diff --check` 无文本错误。
- `06-core-adapters`：Foundation 的 Unity Listener/TimerObject 原 Core 身份迁入 Events/Timing；ColorSelector 家族迁入 UI。新身份及公开域断言先红；完成后 111/111 定向 EditMode、10/10 组件 PlayMode 通过。源码负例在底层完全无 Core namespace 后产生 CS0234 的 Core 缺失而不总包含 Module，守卫改为只接受同一故意违规文件行上的 Core/Module 指定诊断，不能用任意编译失败造绿。
- `07-runtime-domains`：13 组完整功能域按预览和 SHA 迁移 325 个源码/现用文档文件；游戏 7 个 Prefab 的 15 处 AudioEmitter 持久 AQN 精确替换为 `YLFramework.Audio.AudioEmitter, YLFramework.Runtime`。保留方法/目标/fileID/GUID 与 Prefs 键。RED 为 13 个新类型和 7 个 Prefab 绑定用例失败；GREEN 为 22/22 新身份、游戏音频绑定及程序集闭包验证。
- `08-owned-namespaces`：使用 Roslyn 声明所有者表，207 个文件迁移 Editor、DOTween/Excel/DomainExcel 与自有 Compression 包装；上游子树不动。曾发现条件 using 插入 Unity 6000 分支、可选扩展同名方法误导入及父 namespace 自遮蔽，均用真实编译诊断定向修复，不扩大 Runtime->Editor 或必需主体->可选集成引用。110 项回归中 108 通过，2 项旧身份断言按新分区更新；随后 CoreEditorIdentityTests 19/19 通过，包含实际分析器正反例。完整组合尚待最终整合后重跑。
- 可选集成 3 个 asmdef 仅更新 rootNamespace，name/GUID/reference/define 未变；Core namespace 仍为 Core，分析器 marker 保留 Core.RoslynAnalyzers。

### T2 产品目录归位

- `FrameworkStructureTests` 两条真实红态已观察。`Tests/Fixtures/NamespaceMigration/ProductMoves.json` 保存移动前 28 个资产/目录 GUID；外部 `product-moves-with-guids.json` 与原始备份对应。
- 产品 28 项移动全部完成，逐项使用 AssetDatabase 重新核对目标路径与原 GUID。四个 Runtime 模块、三个 Editor 模块、通用工具、Shared 支持、Resource 工作台/Inspector/Build/根入口均按目标表归位；StateMachine 保留 Systems。
- ResourceSystem.Audio partial 与 provider 已分成两个文件，provider 保留原 GUID；DomainResourceIconGui 已从 SkillAuthoring 拆出。均只移动既有实现，无缓存/生命周期重设计。
- DomainExcel 的测试目录以原 GUID `f475cf3eb2edcdf4d9294da999eeac91` 从 Integrations/DomainExcel/Tests/Editor 移至 Tests/Integrations/DomainExcel/EditMode。没有改其程序集身份。
- AssetDatabase.StartAssetEditing 内即时查路径缓存曾延后更新，不能拿尚未更新的缓存当 GUID 丢失；核对磁盘 meta 后在导入完成再以 GUIDToAssetPath 验证。中断调用已实际完成剩余移动，恢复时只读确认，不盲重跑。
- `09-product-and-tests` 刷新抑制仍由主线程持有。产品新文件已显式导入；中间编译的 GetAudioClipProvider 缺失属于新 partial 尚未导入的暂态，不能作为最终结果。测试工作完成后统一释放刷新、编译并验证。

### T4/T5 非重叠协作范围

- 测试工作者仅写 Assets/YLFramework/Tests，保留原测试程序集归属、宏和夹具资源键，禁止 Unity 调用。主线程只保留新 FrameworkVisibilityTests 文件和最终编译/集成；其他测试不并发覆盖。
- SDK 工作者仅写仓库 Tools 下现用路径/模板/namespace，历史 B0/SHA/冻结清单原样保留；文档工作者仅维护现有正式 Documentation/可选集成 README。均不得改 AGENTS/权限、Git 写入或触及外部旧制品。
- Stage9 的 AuditPackage 解析和本次独立交付由主线程继续处理，历史 Finalize 不作为新门禁。

### T3 候选与公开范围裁定

- 初筛候选为 CharacterController2D 的 CheckGround/CheckWall/CheckTop；StateMachine Editor 的 Context、GraphLayer/ParamLayer、四个 Fsm*Factory、StateStyles/Style、StateMachineEditorTools/StateMachineMenu/ListenerFile；LocalizationAuthoring；AudioClipLibraryMigration。须在 T3 逐项复核及测试，不把零调用或候选报告当充分依据。
- 明确保留实际扩展与跨程序集入口，包括 ResourceBuildProgress/Stage、ResourceAtlasPlan/Change、ResourceBuildMap/Result/Publisher、ResourceEditorOperations/Simulation、DataAssetsWorkbookMemory、通用 Editor 工具以及 DomainExcel 消费的作者接口。
- TreeViewStateGroup/Info 有序列化和非友元消费者，暂不收紧，不新增 IVT 来迁就封装偏好。

### 整合后的验证进展（2026-10-23）

- T2 的 `FrameworkStructureTests` 已实际 2/2 通过，覆盖全部 28 项原 GUID 与目标路径，以及两个桥接/共享类型拆分文件。09 刷新锁已归还，Unity 已生成新目录和 asmref 的 meta，并成功编译整合后的产品与测试。
- T3 上述 17 个高置信候选已从 public 改为 internal，无新增友元、接口或程序集。`FrameworkVisibilityTests` 先红 18 项，后绿 18/18；包含真实非友元消费者编译正例及 CheckGround 的 CS0122 负例。正式组件、业务扩展、Resource/Audio/Action 入口未收紧；TreeView 缓存仍 public。
- 组合行为验证实际 67 项，66 通过，1 项 `ResourceStartupTests.StartupJoinAssemblyFailureCancellationAndBusyOnlySettleTheirOwnResponsibilities` 因原场景保护 SetUp 中止。随后在干净初始状态独立运行该精确用例，实际 1/1 通过；没有保存或丢弃用户脏场景，没有改保护断言。
- `Invoke-Stage9Delivery.ps1` 的当前根解析已修正为 `_exportRoots`，严格验证 6 个目录 + 3 个文件及唯一性；不是只把数字改为 9。外部 `Test-ExportRoots.ps1` 对真实正例与 missing/extra/duplicate/commercial-plugin 四个负例先红后绿，5/5 通过。历史 Finalize 未执行也不作为当前门禁。
- 当前 HybridCLR 测试输入目录原本不存在；在外部 owner 下保存不存在的证据后，使用当前编译图执行原 `HybridCLRTestFixtures.Prepare()` 生成新测试 payload/manifest。它是可变测试夹具，不是覆盖旧生产基座；不能据此声明新的 IL2CPP 基座验证完成。
- 完整 EditMode 采用原生 namespace 过滤 `YLFramework`；首次 RQL 全集选择因 NUnit 自定义 TestCase 名不能与源码方法名匹配而在启动前被工具拒绝，未漏选后声称全量通过。改用 namespace 原生过滤后已开始真正的完整执行。
- 完整执行约于 18:00 启动，包含真实资源/图集/Bundle 构建等慢速集成测试，批次过大。CLI 长请求进程记录丢失后，持续以 TestRunner.IsRunActive/Editor 日志验证同一次执行，未重复启动。19:13 实际窗口为 Build AssetBundles busy ~72 分钟，日志仍生成不同 Release82b 夹具并构建/清理 Bundle；完成数因主线程同步工作导致 MCP 超时而尚未取得。
- 本次完整 EditMode 结果尚未完成收集，绝不能记为通过。等待由已有 cron 驱动，不阻塞为失败；结束后只按具体失败及受影响程序集复验，不再把每个小改动都重复全套慢速回归。后续完整 PlayMode、格式/最终分析器、干净 Mono/IL2CPP Low、可选集成及最终独立 Review 均尚未完成。

### 完整 EditMode 结果与定向复验

- 20:30 确认 TestRunner 已结束，等待 cron 已删除，没有重启全量。最终从 Unity Test Runner 已刷新视图取得全部 2621 项：2037 Passed、583 Failed、1 Skipped，保存为外部 `editmode-all-20261023-window-final.json`。此前 MCP `testResults` 仅保留 Domain Reload 后末段 144 项（111/33），不能当完整结果；首次 GUI 快照也早于 UI 队列刷新，含 NotRun，不作为最终证据。完整结果不是全绿。
- 主要失败：446 项 Audio SetUp 与 20 项正文共享 `AudioKit is shutting down`；21 项缺显式 loopback PFX；其他包括 Editor 未稳定/GUI 输入状态、专用 Evidence 前置条件、少量迁移后 Help/边界引用。Audio 和非 GUI 组分别由只读诊断子代理定位，不通过重置私有标志或忽略失败造绿。
- 工作台状态污染隔离：在 settled Editor 中不改产品/测试代码重跑 `ResourceWorkbenchInteractionTests`，7/7 通过；三个原失败 `ResourceWindowTests` 精确复验 3/3 通过，表明这些失败不能直接当目录迁移行为回归。
- 为 TLS 21 项创建本计划自有临时 loopback PFX，未安装证书或修改系统信任。仅 Unity 进程的两个测试环境变量临时设置，原值保存在自有 SessionState scope；该批结束须恢复，不能遗漏。
- TLS 定向复验已发出，不重复请求。之后的较长测试使用现有 CLI `--timeout` 显式延长，并注册现有 UTF `ResultsSavingCallbacks` 写原生 XML，避免仅依赖 MCP 跨 Reload 的短暂缓存。TLS 原生结果预期路径 `tls-rerun-01.xml`；当前结果未完成。
- Tools 最终17个现用文件已完成，13个PS脚本语法、3个HybridCLR模板C#9双宏语法、路径集合及Equipment导出根正负6/6静态验证通过；历史B0/SHA/固定commit输入原样。正式文档22个现有文件已完成；24份文档279相对目标/43锚点有效，未伪造本轮Player通过。上述静态结果不能替代运行验收。
- Ruling: 原始 T0 `ExcelValues.ColumnName` 已含 DataAssets 白名单并使用旧 `config_name/des` 表头。本次不删除该分支来迎合错误的新字段名测试，因为用户明确要求保留既有 Excel 协议。两个绑定测试的源前置合同须核实并按既有协议验证；不能将它们误称新 namespace 回归或改变生产列名。

### TLS 验证中断与崩溃证据

- 2026-10-23 20:54:38，原 Unity PID 64812 在 TLS 定向复验中原生崩溃。最后可观察进度为完成12/21、剩余9；完成不等于通过，最终 XML 未生成，该批标记 InterruptedByEditorNativeCrash，不能记为全绿或21项断言失败。
- 崩溃目录 `C:/Users/Kano/AppData/Local/Temp/Unity/Editor/Crashes/Crash_2026-10-23_205438194` 含 crash.dmp 与 Editor.log。日志已只读复制到本任务外部 owner 的 `crash-20261023-205438-Editor.log`，中断回执 `tls-rerun-01-interrupted.json`。
- 栈顶为 Mono `do_mono_metadata_type_equal` / `mono_type_get_object_checked`，调用链来自 `CertificateHandlerScript::ValidateCertificate` / UnityTLS / Curl worker。它是原生崩溃，不是 C# 编译诊断；尚不能仅凭栈确定具体触发机制或把它归因于目录迁移。
- TLS 等待 cron 已取消，不自动重跑同组或升级引擎/关闭安全检查。用户已重新打开 Unity，现 PID 23740；主线程未另启或关闭 Editor。确认当前工程正确、非Play/编译、原测试环境scope不存在且当前PFX不是本任务路径；旧进程变量/临时回调未残留，无需在新进程按失效InstanceID清理对象。
- 当前为用户新打开的未命名场景（不脏、1个根对象），不保存、丢弃或恢复到旧场景。不会清空 Library/Temp 或批量清理崩溃残留夹具。
- Ruling: Audio466项为历史已存在的 EditMode真实Play退出后的quitting污染；本轮不引入新的Editor重置hook或改变退出拒绝语义。通过正常源码修复重编译后的冷态与真实会话分批验证；不直接改私有flag，也不把受污染批次说成通过。
- 已根据真实红态修复 ActionKit Help 精确可选类型索引、DOTween 示例条件using及Core-only文档/测试，Core边界负例继续绑定指定文件/行和新Audio诊断。Excel产品协议不变，只校正绑定fixture表头为原有config_name，并新增config_name/des合同断言。

### 崩溃后轻量回归与独立 Review

- namespace/Help/边界/Excel/结构/可见性组实际100/100通过。冷 Audio 在正常源码编译产生的新冷态执行，明确排除3个真实Play/Bundle fixture：456/456通过；自定义NUnit用例名导致RQL拒绝启动的预算fixture改用原生class筛选，61/61通过，合计517，无改私有flag/Audio生命周期。两组原生XML分别保存在 `audio-cold-01.xml`、`audio-budget-01.xml`，自有UTF结果回调已注销销毁。
- 最终 Analyzer 源构建0 Warning/0 Error、129/129通过，SHA仍为B9A4B4BA55FF2D68BCB1833057AADB662E5DDC44D005E1D47AFAD2B0B965969E，与已部署产物相同，无需重复部署。格式Check1114个自有C#通过；Review修复后再定向Check3个C#通过。无Formatter Apply或全库重排。
- 独立Review检查新旧文件/GUID、14asmref、持久绑定、程序集/Player边界与工具分发，报告3个确定问题。状态机Writer反射及Mission剪贴板协议两条先实际红态，修复后连同领域/结构/可见性61/61通过；Importer对同协议另有外部片段红绿验证。
- Ruling: Review将Mission前缀列P3，但这是已约定的数据协议，按重要行为问题同批修复，保留原 `YLFramework.MissionSystem.CopyParamter`（包括拼写），不增加旧namespace兼容壳。状态机测试改查真实Editor.Writer类型，原防覆盖字节断言完整保留。
- Domain carrier默认分支是现用源码投影，不能因其尾部含BCommit冻结分支就整体不维护。修正为原Domain20个C#的精确当前路径+配套meta/父meta；静态同一自测先红缺55项，后绿62项缺0/额外0，40个meta GUID有效唯一。固定BCommit及原records至EOF字节SHA保持。该carrier在原HEAD就缺3个测试依赖程序集，未重写asmdef或声称freshCarrierUnity通过。
- Review的Buff设置文件风险未被冒称已验证：T0主工程无旧文件，但其他旧工程可能有存量。正式Architecture已明确新旧路径、备份/显式迁移/重载与双文件冲突限制，不增加通用自动认领机制或虚构旧文件兼容。

### 独立交付当前证据与阻塞

- 新的自有交付根位于外部owner下 `delivery/`，目的身份匹配现用脚本；Initialize与PublishTools已完成。没有覆盖旧制品、采用链接/subst或在Source内创建另一个Unity项目。
- 初版 `YLFramework-Stage9.unitypackage` 保留不再使用；Review协议修复后新导出 `YLFramework-Structure-r2.unitypackage`，4239206字节，SHA256 `6dda280330c5c1f524319a22a4ea7e9fa2271aa91f3f05232b318358536709ea`。精确1032条package/meta/GUID/payload审计通过。
- ReceiverA Create与product-first Import实际Unity退出0，首次编译 Passed=true/HarnessPresent=false/Errors=[]，Runtime/Editor/ResourceStartup两个样例程序集均生成。仅新工程内建模块+UGUI1.0.0+Newtonsoft3.2.1，无XF/MCP/Tests或Source Library/manifest补洞。
- 原Editor Audit实际红态为旧Analyzer固定hash，与本次源构建B9不同。已修Source模板为从本次已审计 `.files.json` 精确选择Analyzer记录并检查非Folder/实际hash相等；没有换成另一个历史常量或删CompilerInputs检查。该片段正例及坏hash/Folder/重复记录负例通过，真实Receiver Audit补验正在进行。
- Generate实际在正式构建前抛PathTooLongException：接收工程根83字符，最长正式产物304字符，超出产品既有260保护；该布局要求根最长38字符。失败Content及日志原样保留，不反复Generate或改产品/路径映射绕过。尚无VerifyGeneration/Mono/IL2CPP Low/Player成功证据。
- Source保护Protect/receiver-check实际3482项前后零差异。Source用户新进程后来已打开干净 `Assets/Scenes/Main.unity`；在接收Audit/Protect收口前，暂不启动Source PlayMode，避免临时EditorSettings变化与保护快照交错。

### 独立 Player 与剩余门禁（最新进展）

- 新自有短交付根 `G:/Unity/YLN1023`，ReceiverA 根26字符；完整Together manifest路径254，保留此前304/263长路径失败现场，不改产品260保护、packing或使用映射。r2包SHA仍为 `6dda280330c5c1f524319a22a4ea7e9fa2271aa91f3f05232b318358536709ea`；Create/首次Import/Audit/Generate/VerifyGeneration均真实Unity exit0。
- Mono `P/m-m/BuildReport.json` Succeeded/Mono2x/Disabled/非Development/0error0warning，BuildGuid `56d117d7f3444df589b3f3cca4915031`；Player `compact-mono-smoke` PID61420 exit0。IL2CPP `P/i-i/BuildReport.json` Succeeded/IL2CPP/Low/非Development/0error1697warning，BuildGuid `3a3e5f9b5b0d4e2dabc0af6ae54f62af`；Player `compact-il2cpp-smoke` PID47256 exit0。两份verdict均Passed/BusinessPassed/IdentityMatched，晚期/退出错误为空；完整Shutdown日志和截图已读取。警告为1695 XML文档、1成员隐藏、1headless probe，不抑制诊断。
- Source分批Play验证：Foundation43/43、Core238/238、DOTween79/79、XFAB1/1、HybridCLR25/25；根程序集混跑497过27失败，GlobalMessage7/7与Http20/20在各自独立会话复验通过，保留原失败。游戏Flow1/1、Click8/8，真实Audio资源会话3+3+2全绿。HybridCLR当前托管夹具仍不等于新原生基座。
- 追加GUI27项的权威RQL LastResults为26过1失败；UI旧快照的27 Passed已标为陈旧，不使用。失败为 `RealWorkbenchBuildPublishAndIndependentGenerationFailurePreserveVersions` 被已发布 `yl-resource-multidirectory-modern-20261018` 不同Unity/App兼容前置拒绝。不删除外来发布物、不弱化兼容保护；资产与T0相同不证明其当时发布目录状态。
- `protection-compact-final2.json` 对Source3482项前后零差异；Main场景字节/不脏、非Play/编译及EditorSettings均恢复。`final-source-package-consistency.json` 逐项核对1032个包记录的源码/meta/GUID，errors=0。
- Save门禁先红：旧真实Mono结果缺少 `save-index-object-roundtrip`，外部 `Test-PlayerSaveEvidence.ps1` 实际拒绝。仅扩展测试harness，在运行输出自有目录写索引/DTO、Close后重载，保留原Resource smoke不创建Store断言；DTO专属Preserve不额外保留框架类型。新增双Player运行尚未完成，不沿用旧smoke冒充Save通过。
- HybridCLR只读核查确认现用接收脚本可生成新基座，必须导出当前源码可选包（含Samples）并使用新自有root；ReceiverA不符合其scope，旧10/22包/基座不可复用。接收工程官方9.0.3 installer需独立Gitee clone，Source的Library不能代替；后续仅在接收工程写依赖/安装运行库，不改主工程或Editor安装。
- Save已取得真实绿灯：新Mono `P/m-s`（Build57236 exit0，Guid `d2f519e3d5994bef92c467b945ae974c`）及IL2CPP Low `P/i-s`（Build42264 exit0，Guid `ffe6fb36223745558cc6e3d4253ef8fb`）均非Development、Succeeded/0error1697warning。Player分别PID49528/62960、exit0，`save-mono-smoke`/`save-il2cpp-smoke` verdict均Passed/IdentityMatched/无晚期错误。两个运行目录各保留真实 `SaveSystemData.json` 与 `Stage9DeliverySaveData.json`；Close后IsCached=false，索引槽名/CurrentSaveID及DTO的73/namespace-roundtrip均核实，进程退出后同一外部脚本再次独立读盘通过。仅对消费者DTO加Preserve，未修改产品link.xml/源码或Resource smoke不创建Store断言。
- 当前可选包已由Source正式 `HybridCLRIntegrationExporter.Export(includeSample:true)` 导出，28个明确源码输入、65201字节、SHA `a6511c6f19f6ca3df9c5347f84b640fe427308e7b75b035b59405e444debd08f`，receipt含输入GUID/meta/内容SHA；根 `C:/Users/Kano/AppData/Local/Temp/kilo/S27` 明确独立owner与新baseline/player/cache路径，默认包仅改接收文件名且SHA仍为r2。Create及无可选依赖的主体包Import均真实exit0，尚未宣称新原生基座完成。
- 可选包无官方依赖首次Import也真实exit0/testsPresent=false；随后仅接收工程Resolve官方9.0.3及独立installer成功，commit `6cadd24d101f4cbd00cd80190106f6621132d24d`、PackageVersion/InstalledLibil2cppVersion均9.0.3。Configure/Setup/身份/部署前置正常exit0。异步外部DLL实际产物passed，但其原runner15秒收尾后强停自有Editor，`compile-external.log.exit.json` 真实exit=-1/editorForcedCleanup=true，不能写作正常exit0。
- 新基座首次`build-base`真实exit1：产品Preflight拒绝系统Temp下baseline，尚未创建该目录。将owner/profile通过正式ConfigureBaseIdentity改绑到当时不存在的持久 `G:/Unity/YLN1023/HB27`，保留原路径与失败历史，不放宽产品保护。第二次`build-base-persistent`实际Preflight/PlayerPreflight/Compile/Il2CppDef/LinkXml/EmptyMethodBridge/StripAot/FullMethodBridge/Manifest/PayloadCommit十阶段通过，但ResourcePublish因PathTooLongException失败、Unity exit1；report无CatalogDigest/BaselineDirectory。故本轮有真实新AOT/桥接输入，但无可用新Player、冻结基座或补丁闭环。
- 路径阻塞已按真实保存的Together三组与产品命名公式量化：manifest后缀228字符，工程根43→完整271，产品要求完整<260（工程根<=31）。现有scope内最短系统Temp形状根41→269、Source Temp/H26根34→262，均不满足；唯一预设持久短路径 `G:/Unity/H26` 已存在，不能接管/覆盖历史。没有修改scope/权限、采用别名、删组或弱化260 guard来造绿。后续需要现用接收流程支持一个明确新owner的独立短工程，不能盲重跑当前失败目录；Source/旧H26/旧基座均未修改。
- 本轮终态：`protection-save-hybrid-final.json` 再次3482项零差异；`final-closure-save-hybrid.json` 核对默认包1032项及可选包28输入的源码/meta/GUID和两个包SHA全部一致，Source PID23740、Main原SHA/不脏、非Play/编译且无编译错误。无自有接收/Player残留，无wakeup/cron。HEAD仍 `c7922424036ef89edd080af8b63c07ac23146250`、暂存为空，未commit/push。整体验收仍被上述HybridCLR原生闭环、TLS中断、GUI发布兼容前置及已注明的未验证范围阻塞。

### 继续完成剩余门禁（2026-10-24）

- 用户指出任务尚未完成，继续执行，不重复用阻塞清单代替完成。恢复时 Source PID23740，Main不脏、非Play/编译/更新，无编译错误；不改变用户工程状态或已有改动。
- Ruling: 接收 harness 的旧路径所有者合同是现用测试工具，不是不可维护的工具权限。为本计划增加精确 `Sxx`/`yl-structure-and-namespaces-20261023` owner，绑定同名的新物理短工程、独立持久基座/Player/cache及compatibility id；保留旧style/legacy合同，不允许本轮回落认领H26。产品260 guard、AGENTS、权限和冻结输出均不变。
- 新 owner 正例先实际红态于旧style-only校验；同步 PowerShell scope 与接收 C# Owned 后，style/structure正例、28项历史/错路径/跨owner/缺prefix负例全部通过。未启动Unity或创建这些测试代表的原生输出。
- 本轮 descriptor仍为外部 `S27`，旧owner保存为 `owner-before-short-reception.json`，所有旧失败现场及旧 `S27/R` 保留；新目标 `G:/Unity/S27`、`S27B`、`S27D4`、`S27C` 使用前已确认不存在。下一步通过既有Relocate物理复制并重新生成Library，不使用链接或缩改产品资源组。
- 短工程已实做：既有Relocate仅复制Assets/Packages/ProjectSettings/UserSettings，不复制Library；官方9.0.3独立安装通过。新Library不带旧EditorUserBuildSettings状态，首次`short-build-base`被Development前置拒绝；通过正式Configure恢复后继续。`short-build-base2`在C++编译中途会话进程中断、无exit/report，不作为断言失败或成功；保留日志，再以持久跟踪执行新run。
- `short-build-base3`真实Unity exit0，Preflight到Baseline全部14阶段成功，冻结新基座 `G:/Unity/S27B`，Player在 `G:/Unity/S27D4`。基座SHA `e79334ccf3d408d8f0b1b9d38d73cfbb4964181935d9b8e28eb8f4383a23d0c8`，Player规范指纹 `94c7c2bffbabfd10ef694c66d81ba55ab593878c2c95a5e47754ddd5245b7bdc`。
- V1真实原生加载（simulation=false）通过，codePackage `9ca971ff65215bc5d9644226b1fb61405514f56cfa5002ee62fd1c59bc97ae53`。外部V2编译/刷新后，PublishPatch的6阶段通过；初次签名缺本轮local-only PFX而exit1，保留失败；只创建本轮loopback自签fixture、不装系统信任后，`short-patch-v2-signed`正常exit0。V2 codePackage `a3e923de79c9e148c2e7c1f003f6d6eb980d029433b0b240a132ff650fa0378e`，冻结基座/Player指纹不变。
- 同一原始Player经本轮独占HTTPS获取/验证签名补丁，运行中激活busy、同进程替换拒绝；重启后真实Native V2同步/异步/prefab入口返回v2，错误base均拒绝。V1/Update/V2进程exit0，全部文件前后指纹一致；服务7请求/0错误并正常停止。`Verify-NativeClosure.ps1`独立读取build/patch/run/exit、当前baseline SHA后通过，生成 `S27/native-validation-complete.json`。旧blocked.json留作历史，不再当当前阻塞。
- Native闭环使用Development/IL2CPP，不冒充Release/High或视觉验收；-nographics保留Sprites/Default、Sprites/Mask不支持Null GPU的启动日志及Development memory telemetry，未抑制、不宣称原始日志零error。功能/冻结/退出以实际receipt、原始日志及exit共同判定。
- T4离线Roslyn补查 `t4-discovery-verification-01.json`：372→374文件、276→278测试类型、2760→2773方法声明；原方法/类型移除0、原372文件程序集/meta变更0；18相关asmdef身份/平台/define保持，14定向asmref准确。2类/13方法为本任务新增，唯一TestCaseSource旧新body token一致。T0原数量实为方法声明数，不是完整动态树；旧动态集合缺失不伪造，后续捕获当前树并逐声明/参数化归属核验。
- Buff旧设置追踪证明确有默认值回退缺口，新增9方法/12用例待真实红绿；初次Unity编译指出该fork NUnit没有NonParallelizable，已删除仅此不支持装饰，保留全部行为断言且不启用并发。Source精确新旧设置路径在测试前确认均不存在，真实红态及生产修复尚未完成。
- Buff真实红绿已完成：`buff-migration-red-03` 12/12失败，3个入口Expected IOException而返回null，其余缺迁移/Reload API。最小Editor元数据guard覆盖Instance缓存、显式LoadOrCreate和Save，内部迁移器独占备份/不覆盖移动并真实重载；Provider原对象清理三处绑定。`green-01` 10/12，另2项是夹具先创建空文件后Unity serializer报坏档，未忽略日志；改为唯一临时真实序列化后独占写目标，`green-02` 12/12通过。`buff-core-regression-01` 54/54，并与当前完整树的Settings15/Identity19/Structure2/Visibility18逐项核对，执行遗漏0。原新旧路径、外来XF设置和用户场景均恢复/保持。
- GUI在干净ReceiverB按原源码/原现代fixture验证 `Gui-03` 1/1、exit0。只准备外部保存场景/稳定SpritePacker宿主，不删Source不兼容发布物、不改产品生成保护。TLS原Source崩溃历史保留；ReceiverB `Tls-03` 17/21、`Tls-04` 18/21，未合并结果。T0与当前8个Runtime类体逐字相同，定位为旧fixture把Initialize/激活/Busy判定当同步及consumer取消当producer已退休；仅测试正常yield公开操作/诊断退休信号，全部原断言保持，不加Shutdown/私有状态/猜测帧数。`Tls-05` 单次完整21/21、Unity PID12708正常exit0、无skip/native崩溃/进程或监听残留，XML SHA `919f4a23f5f10925a1675f0dd10ef37c6da68001b0a96cf4bd62857a7276d888`。证据 `ReceiverB-TlsRetirementFix-01/result.json` 与原XML。
- 当前完整动态树 `t4-current-dynamic-discovery-01.json`：Edit2641/Play925、162实时编译图节点及DLL SHA、完整typed参数/声明/RunState/SkipReason/重复叶节点。ReceiverC从3724个T0原源/原GUID受控重建，仅将缺失BouncyCastle依赖从已测r2包带许可证提取，明确不声称它是未知历史二进制；恢复原图ENABLE_INPUT_SYSTEM分支而不装InputSystem。七默认程序集重建3240/current3314，按mode+assembly+原GUID映射类型+method+有序typed参数+重复多重集逐项比：旧独有0、原方法额外case0、RunState/skip/category差0；当前独有22方法74case恰好为13方法62case加Buff9方法12case，554项类型映射有源/GUID依据，不改string参数。
- 重建原始历史环境/DLL版本仍Unknown，已标 `ReconstructedFromT0Sources=true/HistoricalEnvironmentIdentical=false`。三次采集在两树完整落盘后native shutdown挂起，仅清理精确自有PID，实际exit=-1全部保留，不把完整数据采集冒称正常进程退出；helper完整编译和Buff生成另有真实exit0。`T4ControlledReconstruction/final-report.json`、`comparison-02/report.json`为证据入口；原before3724文件再hash零变化，3007有效输入保持，自有进程全部终止。
- 真实旧Buff payload由旧 `YLFramework.BuffSystem.BuffSystemSettings` 和旧EffectData类型原生序列化生成，不是当前类重写旧YAML。在r3 ReceiverD中验证旧精确路径先拒绝默认加载，然后调用新Migrate：settings及backup SHA均 `09a1fb1faf5d2b95803f99f230576a233ae5bd2cf6e6cf7530197d28e2bd401f`，三列表/7391标记、Effect GUID `f01209172e1628d4f8245c91fc6e1b33`、id179275、名称/描述全部保持，再次迁移no-op、实际LoadOrCreate重载字段引用一致。`buff-real-old-r3-consumer.json` Passed=true，Unity PID51016 exit0；仅自有ReceiverD设置有迁移结果，Source ProjectSettings不写。
- 新正式默认包 `YLFramework-Structure-r3.unitypackage` 4241990字节，SHA `11fbc8ad4f96d2ca4ee8e0d304b55b490ec327afb2089c83650e4a667c32d01a`，原六目录三文件合同不变，审计1033记录通过（新增迁移器及其新meta身份）。新ReceiverD产品首次编译harness=false/Errors空，Audit/Generate/VerifyGeneration均真实exit0。Mono非Development/Disabled BuildGuid `2b4c1a08fea9490798c3e966747e996c`、0error1warning；IL2CPP非Development/Low BuildGuid `6061118b8571467d847972e40c86ef5d`、0error1697warning。两新图形Player PID42060/47004均exit0、IdentityMatched/BusinessPassed/Passed=true、无late/exit errors，Source资源逻辑键、关闭计数及Save独立读盘门禁均通过；不外推视觉听感/移动平台/High。
- r3首次harness安装发现现用脚本硬编码 `first-import-initial.json`，而该产品实际run为structure-r3；未伪造/复制回执。增加显式ImportRun参数默认initial保留已用SyncHarness修订run语义，选择经相同package/project/hash校验的实际首次proof，并写入revision；r3真实安装转绿。
- 全部受管C#最终格式Check实际1116文件通过；此前Analyzer129/129及部署SHA输入未变，沿用有效证据。`current-native-runtime-inputs-01.json`实证717个Runtime/Plugins/可选输入仍与新Native闭环已测输入相同，Editor-only Buff修复不冒充Native重建。r3 Source保护检查再次通过，最终Source包一致性与Review修复收口待完成。
- 增量独立Review确认2项Important：metadata叶link未拦截可使Relocate写Root外、C# Owned repository校验弱于PS可信Source。未发生实际外部覆盖；正先以自有旧工具/纯托管真实guard取得红态，再补leaf NoLinks/独占receipt/可信Source渲染绑定，不改产品或权限。无symlink创建权限的真实链接场景必须明示未执行，不能标过；正在处理，不以已完成Native闭环替代此安全修复。

### 最终收口（2026-10-24）

- 两项Important已修复。Scope/Install/Sync/Relocate覆盖metadata叶与父链NoLinks，Relocate拒绝已存receipt、CreateNew写入并同目录原子更新owner前重验原JSON；C#模板以未绑定即拒绝的SourceRepository占位，由真实Source侧脚本渲染，不再接受自洽foreign repo或dot段。冻结117份输入同组对照：旧C#35/39→新39/39，旧PS37/39→新39/39，输入零改；主20/20、扩展Parity82/82、实际Receipt方法旧2/3→新3/3，原28负例保留。两个真实file symlink因Windows令牌无权限创建而明确Skipped，未提权/改开发者模式；常规路径、同输入负例与静态leaf NoLinks已验证，不把Skipped计入通过。证据 `HybridCLROwnerReview-20261024/Final02/results.json`。
- 主线程独立读六文件并重跑当前28负例/正例后，在无S27 Editor时执行真实Sync+`owner-guard-source-bound-01 Inspect`，Unity exit0、非强停，绑定的Owned与官方9.0.3 package receipt实际落盘。Inspect没有passed字段，wrapper的artifactPassed=false不是构建/业务失败，不能据此虚构artifact通过；`hardened-consumer-unity-proof.json`单独验证真正来源绑定、正常退出和当前基座/Player规范指纹仍与既有冻结记录逐字相同，不重建原生基座。
- 最终当前树再次实际采集为 `t4-current-dynamic-discovery-final.json`，SHA `3d2a286d96dae96682abb831e752775cfa5d96ca961957217d117da7e7adb707`，Edit2641/Play925/编译图162，覆盖最后TLS fixture body修正。`comparison-final/report.json`重新比对仍是原3240 case全部保持、原有方法case差0、当前新增22方法74case、runState/skip/category差0，输入变化空。
- `final-r3-source-package-consistency.json`实证默认r3的1033记录及可选包28输入的源码/GUID/meta/两个包SHA全部匹配、errors空；Source PID23740，Main原SHA/不脏、非Play/编译/更新，无编译错误，新旧Buff设置路径均为空。`protection-structure-r3.json` 3482→3482、所有difference数组空。r3两份ui-modal截图已读，均实际渲染同一Panel；完整Player log及Shutdown、真实exit/晚期错误已核对。
- 最终全部受管C#格式Check1116通过，git diff --check通过（用户既有CRLF警告不改写），HEAD保持 `c7922424036ef89edd080af8b63c07ac23146250`、暂存为空。只有用户Unity23740及其2个AssetImportWorker存活；所有独立Receiver/Player/fixture服务均退出，持久后台记录为exited，无wakeup/cron。未创建分支/worktree、未commit/push、未覆盖用户场景/外来发布物/冻结历史制品。
- 交付默认包为 `G:/Unity/YLN1023/YLFramework-Structure-r3.unitypackage`（SHA见上）；可选HybridCLR包在 `C:/Users/Kano/AppData/Local/Temp/kilo/S27/YLFramework-HybridCLR.unitypackage`（SHA `a6511c6f19f6ca3df9c5347f84b640fe427308e7b75b035b59405e444debd08f`）。旧r2/失败run/崩溃dump/旧冻结包全部原样保留。最终结论是已交付上述有界实现/回归/分发合同，不是“历史所有混跑、任意外部旧项目、High/移动端/听感或任意链接攻击变体全部通过”。

### 真实文件 symlink 权限补测（2026-10-24 06:22 UTC）

- 用户在同一 `KANO\Kano` 账户的管理员PowerShell7.6.8中手动运行仅两场景的 `HybridCLROwnerReview-20261024/Test-RealFileSymlinks.ps1`。本助手没有提权、修改开发者模式/系统策略或启动Unity。
- 两个独立run `RealSymlinks-20261024-062232-853ac582`、`RealSymlinks-20261024-062251-8c755c62` 的原始results.json均已实际读取：Tests=2、Passed=true、Skipped=0、SourceToolsUnchanged=true。各自linked-owner与linked-receipt均RealFileSymlink=true、RejectedByNoLinks=true、SourceUnchanged/OutsideUnchanged=true、DestinationCreated=false；错误明确指向owner.json或relocation receipt的叶链接，不以共享锁等下游IO失败冒充NoLinks拒绝。
- 最新run的invocation.json记录Elevated=true、UnityStarted=false、SourceWritten=false；受测Scope/Relocate SHA分别 `fe9021491e6e1ef765386a9907aacd45d4fc3e1d0e1a340f954e11fe19d6276d`、`f055fce2e4ecf6a45434d6d7012a4576584eddc408fae6ccaca8c4f00698161b`，与最终审查输入一致。其owner外部目标前后SHA均 `5992d75a37c422a034be03b7a1d64d83f0be67e04025016416d9ecb417ef8473`，receipt外部目标前后均 `8a9aa5920fda7ac7a6017e00df749ad448e2fc12c0c547d902c111e5ed4b79a1`。
- 用户将输出文本 `PASS: ...` 再次输入终端导致的ParserError只是交互命令解析错误，随后脚本第二轮真实结果仍通过，不改变已保存测试结果。空表格来自OrderedDictionary经Select-Object的显示适配，已仅将显示转换为PSCustomObject；不改测试逻辑、判定或原两轮证据，不要求管理员重复运行。
- 这两项不再属于当前未验证范围；历史Green02等Skipped记录原样保留，以本节新增实测补足，不倒写历史。该补测及显示修复均未改产品包或Source运行代码，既有r3/Native证据不失效。

### Git 收口范围（2026-10-24）

- 用户明确授权提交并推送当前 `integration/xf-yl` 到 `origin/integration/xf-yl`，不切换/创建分支、不合并或强推；开始时本地及实际远端均为T0的 `c7922424036ef89edd080af8b63c07ac23146250`，暂存为空。
- 仅纳入本任务Framework/Tools、游戏namespace消费者与7个Prefab持久类型名、本计划及配套meta；迁移删除与对应新路径共同暂存。用户既有MCP/NuGet升级、配置/宏、旧计划、3个游戏FSM资产、Assets/Test、YLFrameworkData及HybridCLRSettings不纳入、不回退。两份Release82b测试现场、预存AlternateContent.meta和空Localization测试目录meta保留不纳入。
- 本批为代码/测试/工具/文档/计划的同一个实质提交；既有有效技术验收、最终r3源码/包一致性和新symlink补测证据复用。提交前检查精确清单、meta/GUID移动、staged diff、敏感内容和排除项原字节；不为Git状态另建文档提交。实际hash与推送同步结果通过Git及最终回复交付，不预写未发生的成功或自引用hash。
