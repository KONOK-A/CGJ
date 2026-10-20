# XF 生态拓展系统融合专项 Implementation Plan

> **执行说明：** 本文件为唯一拓展融合专项方案，2026-10-19 编制并按审核意见增量修订。用户后续仅授权B9 Git收口及E0最小公共准备，当前执行记录见§10；13项系统迁移、三个Worktree和D1-D6仍未获实施/裁定授权。已有盘点、分组及主要顺序沿用，不重新全库研究。未勾选项不是已实现成果，不以批准局部改进原则代替重大API、依赖、Git或后续Worktree授权。

**Goal:** 对长期蓝图选定的13项剩余 XF 系统完成真实盘点，确定唯一能力归属、依赖次序、三个低耦合 Worktree 边界及独立交付门禁。

**Architecture:** 保持 `YLFramework.Runtime` / `YLFramework.Editor` 两个产品主体及 Foundation / Core / Systems / Utilities 的现有方向。融合优先、局部调整其次：保留成熟领域算法、功能与编辑器工作流，必要处替换依赖、分离运行态或修复生命周期，不默认推倒每个Manager。领域系统消费既有 Pool、Update、Events、Resource、Save，不复制基础设施或建立统一大平台；原 XF 包、原游戏和既有资源链继续独立保留。

**Tech Stack:** Unity `2022.3.62f3c1`，C# / ScriptableObject / UGUI，现有 Json.NET、批准的 BouncyCastle；新增依赖不在本计划默认授权内。

**Spec:** [长期融合蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md) §1-5、§9；[现有架构](../../Assets/YLFramework/Documentation/Architecture.md)。蓝图 §2 的系统集合保持不变；XFABManager 已由阶段8 Resource 承接，本专项不再次迁移 XFABManager。

## Global Constraints

- 前两轮仅规划/修订，不修改产品或操作Unity/Git的限制保留为历史范围。后续明确授权仅允许本次B9与E0源码/必要测试/meta/说明、唯一主Unity定向门禁和两批精确提交/普通Push；不允许13项迁移、Worktree创建、未批准依赖、Core/Resource公共契约变化或重建既有交付。§1-2的首轮快照不冒充当前Git状态，实际B9及本批验证以§10为准。
- 后续实施只改 YL 自有内容。旧 XF PackageCache、embedded Logger、原游戏、MainModule Bundle / StreamingAssets 和现有用户 Worktree 均受保护，不以让游戏改用新 YL 为验收标准。
- 用户融合原则为：**“保留成熟实现，做好YL适配；发现Bug可以修复，有价值的优化和推荐的小型功能增强可以一起做，但不要大规模重写模块。”** 正确的领域算法、公开用途和作者工作流优先保留；依赖替换、配置/运行态分离、会话退出/订阅归还只改有必要的边界，不因Manager命名、静态入口或代码风格自动另起一套执行模型。
- 当前模块内有复现或可靠源码依据的Bug随融合修复，并补直接回归；尚待复现的风险先用用例核实。报告区分静态证据、实际复现、修复后验证，不把本轮盘点或未来测试要求写成已运行通过。
- 对应批次另获实施授权后，可自主采用有实际收益的局部性能、API易用性、文案、诊断和编辑器操作改进；小型增强须贴近原职责，形成可操作、可验证的完整流程，不新增未批准依赖、不改共享核心契约。普通细节不重复请示，无收益则不加；性能收益用相关成本分析/测量支持，不能仅冠以“优化”。
- §3区分拟纳入与候选：拟纳入项在本系统批次内核对收益、已有实现和影响后选择，候选不自动升级为必做。取舍按行为、兼容性、所有权、共享影响和验证成本判断，不以代码行数机械判断是否允许调整。
- 大规模替换执行模型、跨模块公共API/存档格式变化、新第三方依赖或明显删减功能仍单独裁定；D1-D6未决事项保持未决。保留Manager算法不等于保留已证实的Bug，修Bug也不授权重写整个模块或顺手裁掉Editor功能。
- 新承接类型使用 `YLFramework` 及适用子命名空间、新 GUID、新资源/菜单/偏好键。移动已有 YL 文件保留其 `.meta` / GUID；不复制原包 `.meta`，不改变旧包身份。
- 不引入安装器、UPM 分发、通用配置平台、第二个资源/事件/池/更新/存档权威；不按13个来源机械建立13个程序集。
- 新功能一处唯一实现；保留不同职责不等于保留重复实现。既有代码优先轻量 `YLFramework.FSM` 与资产驱动的层级状态机可分层，不能未经裁定强行互相替代。
- 当前 Unity 依赖声明与缓存有历史差异。manifest/lock 的 UGUI `2.0.0`、URP `16.0.6` 与当前缓存 `1.0.0`、`14.0.12` 不同；本批未操作 Package Manager，不能把目录版本冒充实时注册状态或干净恢复证明。不得顺手升级、改锁文件或修 Library。
- 技术验证、用户验收、Git 收口分别记录；源码编译、Unity 编译、测试、Player 构建、Player 运行和独立交付不能互相替代。

## Review Focus

- 原包的自动初始化、全资产扫描和静态缓存：新 YL 不能因同工程共存而改写旧资产、抢原 Logger handler 或重复创建全局宿主。
- 序列化与类型身份：复制源码的新 GUID、FSM 类名字符串、SO 根/子资产、UnityEvent、持久偏好键及真实 IL2CPP 类型创建必须分别核对。
- 异步资源换代：语言/图标/配置的旧请求晚到、owner 关闭和资源失效不能回写新 UI；所有请求与 Lease 按真实所有权归还。
- 重入和多 owner：技能池归还、Buff 移除回调、Mission Reset、红点多来源和物理查询共享缓冲不能相互污染或修改配置资产。
- Editor / 第三方隔离：条件编译的 GUI 不能掩盖 Runtime asmdef 的 Editor 引用；EPPlus、旧 Analyzer 和来源未闭环的图形 Editor 文件不能自动再分发。

## 1. 实核基线与阶段9 Git 收口

### 1.1 首轮规划快照

以下为开始规划时的历史事实，保留以解释未提交交付来源；当前B9已按§10收口，不能继续把这张表的旧HEAD或TLS失败当作当前状态。

| 项 | 2026-10-19 实际结果 |
|---|---|
| 主工作区 | `G:/Unity/Projects/CGJ2024`，当前分支 `integration/xf-yl` |
| 本地 HEAD / 本地分支引用 | 均为 `586eebedaf900d926d2e30301b20ed3129decb27`，提交说明为 UI 全链路验收修复 |
| 本地 `origin/integration/xf-yl` 跟踪引用 | 同为 `586eebedaf900d926d2e30301b20ed3129decb27`；这是本地记录，不是实时远端证明 |
| 真实远端 HEAD | `git ls-remote --heads origin integration/xf-yl` 因 TLS `unexpected eof while reading` 失败，**未能核实**；未 fetch、改 TLS 配置或执行远端写入 |
| 暂存区 | `git diff --cached --name-status` 为空 |
| 初始 dirty | 13条 tracked 变更：11条修改、2条删除；15条 untracked；本 Plan 尚不计入这份开工快照 |
| 原游戏 / 工程配置受管差异 | `Assets/Games`、`Assets/Scenes`、`Assets/StreamingAssets`、manifest、lock、`ProjectSettings` 的定向 diff 为空；不等于忽略产物已归档或可自动重建 |
| 阶段9 | 蓝图 §9 记录技术验收完成、待用户验收；本次进入后续规划不自动代签 Git 收口，也不改写该既有记录 |

**不得将 `586eebed...` 作为已经包含阶段9交付修复的最终创建基线。** 阶段9使用的是 HEAD 加未提交源文件状态，蓝图 §9.5 的 `delivery-binding.json`、`final-source.patch` 与文件 SHA 记录了这种区别。

### 1.2 尚未提交的交付内容

以下均为开工前已有改动，本批保留，不接管、暂存或重新生成：

| 类别 | 真实路径与收口要求 |
|---|---|
| 正式交付修复 | `Assets/YLFramework/Editor/Distribution/ExportUnityPackage.cs`：默认 Plugins 根收窄到批准的 BouncyCastle，不再分发商业 Editor Enhancers；`Editor/Systems/Resource/Build/ResourceBuildWorkspace.cs`：native 构建前拒绝 Unity Mono Windows 超长输出路径 |
| 成对移动 | 删除 `Assets/YLFramework/Editor/Core/UIPanelAcceptanceBuild.cs` 及 `.meta`；未跟踪目标位于 `Tests/Core/Editor/UIPanelAcceptanceBuild.cs` 及 `.meta`。已核对两端 GUID 均为 `358ebd50e0ddc2d4b8c4f0266f11ff1e`，后续提交须同时包含删除与目标，不能只提交一端 |
| 定向回归 | `Tests/EditMode/ExportUnityPackageTests.cs`、`Tests/EditMode/Resource/ResourceBundleBuilderTests.cs` |
| 6组稳定工具及 meta | `Tests/Tools/Invoke-Stage9Delivery.ps1`、`Run-Stage9Representative.ps1`、`Test-Stage9OptionalLogger.ps1`、`Stage9DeliveryEditor.cs.txt`、`Stage9DeliveryPanel.cs.txt`、`Stage9DeliveryProbe.cs.txt`，每项均有配套 `.meta`，合计12条未跟踪文件 |
| 说明与证据入口 | `.kilo/plans/xf-yl-8-resource.md`、`Documentation/IntegrationPlan.md`、`Documentation/Modules/Resource.md`、`Documentation/README.md`、`Runtime/THIRD-PARTY-NOTICES.md` |
| 原 Logger 的阶段9补丁 | `Packages/com.xfkj.xfgameframework.loggersystem/Runtime/LoggerManager.cs` 与 `CHANGELOG.md`：新增程序集级 `AlwaysLinkAssembly`，解决独立可选包无业务静态引用时的 IL2CPP 初始化根；仍属原 embedded 包，不是 YL Logger 融合 |
| 用户原有未跟踪文件 | `Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta`；不是阶段9新增交付，不擅自提交、删除、移动或补造对应资产 |

阶段9制品身份沿用已有记录，本批不重新验收：`G:/Unity/Y9/YLFramework-Stage9-Final.unitypackage` 的 SHA256 为 `d1710ed15ac86048f1a479d04779a776a27520060c35aeb2e39c8b4fc5d2a91c`；独立可选 Logger tarball 为 `a156bfd07c687bebd28d0abec42e639c8e56f6217a75db4f77bf624db7542057`。其 Mono / IL2CPP Low、首导入及失败历史按蓝图 §9 的原范围沿用，不外推新拓展系统已通过。

### 1.3 建议的 Worktree 创建基线

1. 另获明确授权后，审核并提交上表真正属于阶段9的源、测试、移动目标、`.meta`、工具和说明，保持原用户 `AlternateContent.meta` 不动；对照现有制品文件清单确认提交没有遗漏已测修复。纯 Git 收口不机械重跑全部技术验收。
2. 将实际包含阶段9修复的集成提交命名为本文占位符 **B9**，记录真实 SHA；不能提前填写成旧 HEAD。真实远端重查成功后分别记录本地/远端 SHA；是否 Push 必须另获授权，不以创建本地 Worktree 自动推送。
3. 先完成 §4 的最小公共准备批次及门禁，其集成提交记为 **B0**。三个实施 Worktree 推荐统一从 **B0** 创建；若公共准备最终无需产品改动，则 B0 = B9。
4. 创建前再核对 tracked/index 状态、白名单未跟踪文件、分支/路径占用与现有 Worktree；不 stash、清理或复制整个 dirty 主目录来拼基线。新 Worktree 不自动携带未提交修复、忽略 Bundle 或 Library，恢复依赖与原游戏制品须单独核验。

## 2. 13项真实来源清单

本节编号仅为本专项索引，不改长期蓝图的主框架18项编号。`PC` 表示 `Library/PackageCache/`；除 Logger 外，来源为锁文件 revision 与实际只读缓存目录共同确认。各包标准 `dependencies` 均为空，部分另有旧安装器 `xfgameframework_dependencies`，不能据此推导无代码依赖。

| 项 | 来源包 / 实际版本 | 锁文件 revision / 有效来源 | 主要程序集与规模 |
|---|---|---|---|
| E01 XFFSM | `com.xfkj.xffsm` 1.5.4 | `b06333c2cdc801fcc401f14c252853bf099aa1b7` | `XFFSM` / `XFFSMEditor`；Runtime 20、Editor 34个 C# |
| E02 CharacterController2D | `com.xfkj.xfgameframework.charactercontroller2d` 1.1.6 | `f0241054744ca0673ec370d5fad39c32b789d81c` | `XFGameFramework.CharacherController2D` / `XFGameFramework.CharacherController2DEditor`（上游拼写如此）；7 + 1个 C# |
| E03 MoveablePlatform | `com.xfkj.xfgameframework.moveableplatform` 1.0.6 | `1e1265d8e6adb4ebab6febe8bc51288972e2b234` | `XFGameFramework.Platform2D` / `XFGameFramework.Platform2DEditor`；5 + 2个 C# |
| E04 SkillSystem | `com.xfkj.xfgameframework.skillsystem` 1.5.8 | `4c9d8f5d3120cdf7333a9a040a85ee9583af072f` | `XFGameFramework.SkillSystem` / `XFGameFramework.SkillSystemEditor`；11 + 7个 C# |
| E05 BuffSystem | `com.xfkj.xfgameframework.buffsystem` 1.7.3 | `01d05626b01bcb01e0c27a1040119f41b534f902` | `XFGameFramework.BuffSystem` / `XFGameFramework.BuffSystemEditor`；16 + 11个 C# |
| E06 Overlap | `com.xfkj.xfgameframework.overlap` 1.0.4 | `4961d8358119485c94d6f0f74b6ca5d8b4163460` | `XFGameFramework.Overlap` / `XFGameFramework.OverlapEditor`；10 + 5个 C# |
| E07 Localization | `com.xfkj.xfgameframework.localization` 1.0.9 | `743876d52c5fe636effc0b63c34bd261d6cad243` | `Localization` / `LocalizationEditor`；语言配置、文本/图片组件与作者工具 |
| E08 MissionSystem | `com.xfkj.xfgameframework.missionsystem` 1.2.4 | `a3da5d37ae61d24becd915281f2b676cb1210749` | `XFGameFramework.MissionSystem` / `MissionSystemEditor`；13 + 10个 C# |
| E09 ScriptableObjectSystem | 实际包 `com.xfkj.xfgameframework.scriptableobjectconfigs` 1.1.4 | `d7b6c171999e05a1840f413e1f3e2080e3853163` | `ScriptableObjectSystem` / `ScriptableObjectSystemEditor`；配置根、子数据、选择/编辑工具 |
| E10 ExcelSystem | `com.xfkj.xfgameframework.excelsystem` 1.0.7 | `725f94f23327de190111573870146af66e9484b6` | `XFGameFramework.ExcelSystem` / `XFGameFramework.ExcelSystemEditor`；Runtime 4个 C#，Editor 含 `ExcelManager.cs` 及 EPPlus DLL |
| E11 ArchiveSystem | `com.xfkj.xfgameframework.archivesystem` 1.0.7 | `8046fe56f82983e6081a5c110559915d94df9e3e` | `XFGameFramework.ArchiveSystem`；模型缓存、槽位文件与同步/异步读写 |
| E12 ReddotSystem | `com.xfkj.xfgameframework.reddotsystem` 1.0.4 | `8e2c23e435a4ff1eb4dbc441a6d4fb389ee58c89` | `XFGameFramework.ReddotSystem`；Runtime 2个 C#，无 Editor |
| E13 LoggerSystem | `com.xfkj.xfgameframework.loggersystem` 1.0.1 | **有效来源 `Packages/com.xfkj.xfgameframework.loggersystem/` embedded**；上游 `0ebbebeb4b961d6fb67670d22cad819856969c3a` + 本地已提交生命周期补丁 + 阶段9未提交修复 | `XFGameFramework.LoggerSystem`；1个 C# 内含 Manager / Handler，另带旧 Analyzer DLL |

缓存路径规则为 `PC/<包名>@<revision前10位>/`。Logger 的同名缓存不是有效补丁源，不能反向覆盖 embedded 文件。缓存不随 checkout 恢复，后续来源归档以真正承接文件、revision 和适用许可为准。

### 2.1 许可边界

- E01 为 MIT，`Copyright (c) 2021 弦小风`；E02-E09 为 MIT，`Copyright (c) 2024 弦风课堂`；E10-E13 为 MIT，`Copyright (c) 2025 弦风课堂`。实际融合时在现有根许可与 Notices 增量记录适用版权、来源和全文，不能仅沿用2021版权替代。
- E10 的 `Editor/EPPlus.dll` 仅启用 Editor，包内未给该 DLL 的独立版本/许可文本。**其实际二进制版本与再分发授权尚未实核**；外层 XF MIT 不足以证明 DLL 可分发。无 NPOI / System.Data / CSV 第三方库证据，不据想象引入这些依赖。
- E01 的 Editor 使用 `UnityEditor.Graphs`、Experimental GraphView、UI Toolkit 内部反射；`FSMSelectionDragger.cs` 存在需核查的上游来源/通知风险。这是来源待核，不是已作法律结论；不直接复制风险文件进入分发。
- 不承接旧 Logger Analyzer、各包 logo、安装器、文档截图或无需求的 Demo。现有 YL Analyzer 保持唯一部署；原包 Analyzer 不卸载、不关闭诊断。

### 2.2 原游戏及资产实际使用

| 系统 | 已确认使用 / 搜索范围与保护重点 |
|---|---|
| XFFSM | 原游戏实际使用。`Assets/Games/MainModule/Scripts/Controller/GameController.cs:68-79` 经原 XFAB 加载 `GameStateManager` 并启动 `GameState`；3个 FSM 配置资产和4个敌人 Prefab 引用旧组件。状态脚本全名/GUID、`MainModule.asset` 的 `fsm` 映射及原验收中的 XFFSM 清理链保持 |
| Overlap | 原游戏8个敌人/武器 Prefab 使用 `OverlapCircle2D`，脚本 GUID `63157fd8eb2696a4eae4b5c7f31066ea`。WeaponBase / EnemyBase / ShotGun 读取 `OverlapBase2D.Results`；现有 enter/exit 持久调用列表为空，但不能据此删旧字段或重绑原 Prefab |
| Logger | Assets C# / 文本资产未找到业务类型/GUID绑定，但原包 `BeforeSceneLoad` 自动创建并接管全局 `Debug.unityLogger.logHandler`，因此**不是未使用包**。与新 YL 默认共存必须不新增第二次自动接管 |
| 其余10项 | 在 Assets 可读 C#、asmdef/asmref、Scene/Prefab/asset 的类型及 Runtime 脚本 GUID 检索中未找到静态消费；MainModule 程序集/Bundle 配置亦无相应入口。不能由此证明忽略 Bundle、动态字符串、外部存档或仓库外消费者不存在 |

`Assets/StreamingAssets/MainModule/` **实际存在且受保护**；默认 Glob 忽略无返回不能解释为目录不存在。本次未解包其二进制，也未通过 Unity AssetDatabase 做全依赖审计。Skill / Buff / Mission / Reddot 包本身无配置 Prefab/Scene 或测试资产；Localization / SO 的游戏配置在文本检索范围未找到。新测试必须使用自有新 GUID fixture，不迁移原游戏示例来凑独立证据。

## 3. 实际依赖与逐项融合决定

### 3.1 来源依赖图

箭头 `A -> B` 表示 A 消费/引用 B；`[Editor]` 是 Editor-only。下图是当前来源事实，不是要求新 YL 保留这些边。

```text
XFFSM.Runtime -> Unity                         （无 XF / XFAB 依赖）
XFFSM.Editor -> XFFSM + Unity Editor / GraphView / 内部反射
CC2D.Runtime -> XFGameFramework -> XFABManager
Moveable.Runtime -> XFGameFramework + CC2D     （CC2D 为 asmdef 声明边，未见类型使用）
Overlap.Runtime -> XFGameFramework + XFGameFrameworkEditor [Editor]

Skill / Buff / Mission.Runtime -> XFGameFramework + XFABManager
                               + Excel.Runtime + XFGameFrameworkEditor [Editor]
Skill / Buff / Mission.Editor -> 各自Runtime + XF/XFAB Editor工具 + Excel.Editor
Reddot.Runtime -> XFGameFramework.EventManager

Localization.Runtime -> XFGameFramework + XFABManager.ImageLoader + TMP GUID
Localization.Editor -> XF/XFAB Editor工具 + Excel.Editor
SO.Runtime -> XFGameFramework + XFABManager + Excel.Runtime + XF Editor [Editor]
SO.Editor -> XF/XFAB Editor工具 + Excel.Editor
Excel.Runtime -> Unity / BCL                  （字段DTO/枚举 + 3个Attribute，无解析器）
Excel.Editor -> XF Editor工具 + Json.NET + EPPlus
Archive.Runtime -> XFABManager.XFABTools / EncryptTools + Json.NET
Logger.Runtime -> Unity / BCL                 （全局Handler、文件writer）
```

补充：Mission 实际使用 Json.NET；多个包依赖自动预编译引用而非显式声明。Localization 的 TMP GUID 当前无对应已安装包，versionDefine 分支不能作为 TMP 已验证证据。Archive asmdef 只声明 XFABManager；`IArchiveModel.cs` 的 XF using 未实际使用，不应误画成必需的 XF 主框架边。

**明确不存在的必需边：** 未发现 Skill -> Buff、Buff -> Mission、Mission -> Archive、Skill -> XFFSM、其他拓展 -> Overlap 的源码依赖；不能按领域名称联想依赖。Moveable 不因旧 asmdef 多写 CC2D 就被新设计强制绑定；CC2D 与 Moveable 的联合物理回归是推荐验证顺序，不是新编译前置。SO 配置系统也不是 Skill / Buff / Mission 的基础类，禁止为统一形式强制它们改继承树。

### 3.2 逐项推荐与源码依据

保留首轮源码结论，以原实现为适配起点，不把各Manager统一改名/换成新Service作为交付条件。下表“拟纳入”是后续对应批次获授权后的推荐小改进，实施者确认实际收益、已有覆盖和局部影响后自主选择；标为候选或受D1-D6约束的事项不自动转为必做。各项原行为对照、直接回归及完整流程见 §6，不以换目录或只留元数据算功能完成。

| 项 / 主要源码依据 | 保留能力 | 必要适配 | Bug修复 | 拟纳入优化/增强：收益与验证 | 暂缓项 |
|---|---|---|---|---|---|
| E01 XFFSM；`Runtime/Scriptable/RuntimeFSMController.cs`、`RuntimeFSMControllerInstance.cs`、`FSMStateNode` | 原资产/层级/子状态机、参数/Trigger、状态回调及图形创作/检查工作流；与 `Runtime/Utilities/FSM/Core/FSM.cs` 的轻量FSM分层，不替换彼此算法 | YL身份、Runtime/Editor隔离、退出归还、扫描限于自有资产；按D1局部处理内部API/来源风险，不默认换运行内核 | 运行态/注册残留等可靠风险以直接用例锁定；隔离旧偏好键和全工程刷新副作用，未复现不写成已复现 | 拟补缺状态类型/转换目标的配置、层、状态定位诊断；用无效类型fixture验证可定位且正常状态顺序不变 | 强制工厂/新执行模型、整套Editor重写或删掉必要作者流程均属D1；来源未闭环文件不直接分发，优先级仍最后 |
| E02 CharacterController2D；`Runtime/CharacterController2D.cs` | Rigidbody2D/Collider2D、ground/wall/top、多段跳、重力/旋转与事件，以及现有Inspector | 可选上层组件；复用YL `LayerMaskExtensions.Physics2DSetting`，移出Editor代码，保持物理步长/禁用恢复；不放Foundation | 实例隔离静态hit缓冲、核对多Collider和禁用恢复；边界用例先固定可观察行为 | 拟补齐缺Collider/非法层的配置诊断，直接指向对象和字段；Editor负例及正常移动回归；无明确收益则不新增能力 | 新输入包、全局角色系统/整体motor替换；可选交付范围仍受D6 |
| E03 MoveablePlatform；`Moveable.cs`、`MoveablePlatform2D.cs`、`PlatformCatcher2D.cs` | 点/Transform路径、Loop/PingPong/Once、delay/pause/stop、载人与UnityEvent/路径编辑 | 去无用CC2D程序集依赖，复用YL LayerMask/Unity存活语义；保留现有驱动，不建第二Update引擎 | 空/单点/零距离路径、接触快照、多Collider/刚体位移直接回归；Vector3判空为静态缺陷候选，不冒称编译失败已复现 | 拟提供非法路径的节点索引/原因诊断，降低编辑排错成本；以坏路径不越界、不污染其他刚体验证 | 物理/路径执行模型整体替换、未有需求的路径功能；可选范围受D6 |
| E04 SkillSystem；`SkillManager.cs:173-315,339-460`、`SkillBase.cs:64-144,482-512` | 现有Manager领域流程、Release/Cancel/Interrupt、冷却、宿主技能及配置/脚本绑定Editor | 局部替换类池/Update依赖并持有归还句柄；按D3补会话/宿主关闭边界、配置只读；不强制新Service/工厂体系 | 冷却完成进度归零、非Unity宿主残留及池归还/重入边界；分别补冷却终点、移除后复用和Dispose回归 | 拟在类型/配置失败诊断中带技能ID、配置和类名，保留既有正常入口；错误fixture验证定位、正常释放序列不变 | 全局单例到多实例的公共API重设、整体执行模型替换属D3；过时SkillController/废弃UnityEvent核对后退役，不以此删现行用途；xlsx接线等B |
| E05 BuffSystem；`BuffManager.cs:193-325,639-669` | Single/Additive、持续时间、效果聚合及配置/EffectData/作者流程的正确算法 | 接YL池/Update，在原Manager内限定会话和宿主责任；隔离定义/运行态，按D3替换旧View特判，不接管YL View | 最后Buff效果缓存残留、重入移除、缺类型静默降级、关闭未注销；以效果清空/宿主释放/缺脚本回归 | 拟补Buff ID/配置/效果定位诊断；重复效果查询优化仅为候选，先有成本证据再做，验证聚合结果与事件次数不变 | 重写效果执行器、强制Skill/Timer依赖、跨模块宿主API变化属D3；xlsx等B |
| E06 Overlap；`OverlapBase.cs:34,185-188`、`OverlapCapsule2D.cs:82` | 现有2D/3D形状查询、Results、enter/exit、检测频率/时间模式和编辑操作 | 去XF/Editor反向依赖，Handles/Gizmos归Editor；复用YL `Physics2DSetting` / `PhysicsSetting`；不复制原Prefab/GUID | 静态缓冲重入、3D Reset误用2D层矩阵、fake-null及disable/enable状态；以双实例/回调重入/3D层/停用重启直接回归 | 拟在Editor/Development对结果达到容量提示“可能截断”，限频且不增加正常帧分配；容量边界/提示次数及原Results语义验证 | 无成本证据的扩容/池化/查询算法重写；不借优化改变检测频率或公开结果语义 |
| E07 Localization；`LocalizationManager.cs:67,109-187,236-341`、`LocalizeBase.cs:47-56`、`LocalizeImage.cs:9,27-58` | 语言/条目模型、查询、格式参数、文本/图片与现有多语言配置/编辑流程；YL无完整等价 | 优先保留MultiLanguageConfigs/LanguageConfig布局，接YL Events、根Lease和ResourceImage；回退按§3.3；不默认合并为新Catalog | 多配置覆盖回退、缓存失效/重复ID校验、空图片项、先Enable后配置到达/晚到隔离；移除SetActive强刷副作用并测组件/业务启停次数 | 拟补缺键、格式参数错误的语言/ID/配置定位诊断与直接刷新，提升排错且不触发业务物体重启；对应负例和切换完整流程验证 | 新单Catalog/格式变化仍D2；Other反射/TMP/联网翻译/Steam窗口的承接或明显删减仍D6，不自动安装包、不扩成语言平台 |
| E08 MissionSystem；`MissionManager.cs:137-160,451-488`、`MissionCondition.cs:199-219`、Editor `ListenerFileEvent.cs:21` | 参数/条件/奖励、任务状态/进度与现有创建编辑流程的正确算法；不虚构Archive前置 | 保留配置定义/任务流程，只分离或克隆参数值、isMeet和订阅等运行态；按D3补会话边界。若有真实持久化需求由应用消费Save，不在Mission写文件 | 错删EquipmentSystem路径、Float Equals/NotEquals缺分支、Reset顺序/条件残留/退订、初始化失败回滚和类型静默降级；逐项直接回归 | 拟将缺参数/比较类型不匹配诊断定位到任务和条件；错误配置用例加开始至完成/重开闭环，正常奖励/通知次数不变 | 新任务执行框架、强制快照/存档格式及跨模块API变化；xlsx等B，裸hash替换若影响公开键须D3 |
| E09 ScriptableObjectSystem；`ScriptableObjectManager.cs:66-104`、`SingletonScriptableObjectConfigs.cs:21-52` | 类型化目录、ID/名称/图标、根/子资产引用与创建/复制/改名/删除/排序、类型选择流程 | 局部接Resource Lease/预览和YL Editor工具；保留可用布局/类型绑定，在原查询逻辑补碰撞/重复校验与退出归还，不强制其他领域继承 | 空目录Min/Max越界、重复ID诊断、静态缓存归还及Editor泄漏；空/重复/关闭与资产保存重开回归，删除仅作用自有目标 | 拟在现有Inspector集中列出重复ID/缺失引用并定位资产，避免另造工具；验证编辑修复后诊断消失、排序/Undo可回退 | 去type_script_guid、全局入口/数据格式替换仍D2；无真实用途的统一DataCatalog/表/ORM、破坏式Excel同步不预建 |
| E10 ExcelSystem；Runtime字段DTO/3类Attribute，`Editor/ExcelManager.cs:415-459` | 拟选交付范围内的真实xlsx导入/导出、3行表头/第4行数据、enum/JSON/GUID/属性转换及SO/List/JSON作者入口；不以元数据冒充功能 | 共享薄元数据按消费者最小抽取，解析/资产操作保留成熟实现并局部隔离；Editor后端先核版本/许可，不直接复制未获准EPPlus、不造手写xlsx库 | catch后仍删资产/报成功、吞异常、enum类型、无效GUID与取消后的资产保护；两阶段校验/提交仅调整必要边界，坏文件与故障注入直接回归 | 拟在提交前显示新增/更新/拟删除及错误位置，确认/取消形成完整导入闭环；失败/取消原资产不变。大表扫描收敛仅在有成本证据时纳入 | 后端/依赖与明显裁掉原作者入口属D4；范围确定后必须真实文件往返，只有Attribute不得标Excel完成 |
| E11 ArchiveSystem；`ArchiveManager.cs:193-199,265-352,427-474` | 推荐由YL Save承接槽位/对象/设置、备份恢复/版本/文件名校验，保留原包和原数据。**真实差异**：IArchiveModel/模型ID及CurrentUseId的调用映射、原`.sav`/DES兼容、异步接口不能冒称被Save同名覆盖；Save已有缓存，不再另建模型缓存平台 | 不迁移运行时的建议仍待D6；先列Save调用映射/实际消费者缺口，不改Save格式或共享API，不新增第二ArchiveManager | 来源的直接覆盖写、Task.Run触Unity路径、单次ReadAsync、吞异常和DES/零IV不复制；若D6改为承接，必须通过现有Save或定向适配消除并补回归，不修旧包 | 当前无必做增强；模型调用便利层/离线旧档导入仅为候选，有真实消费者/样本再定。用Save对象/槽位磁盘往返验证映射，不借“增强”补无需求异步 | D6未决不视为Archive已完成/正式删范围；旧格式导入、加密/异步或存档格式变化另授权，不因本轮原则默认新增 |
| E12 ReddotSystem；`ReddotManager.cs:99-233`、`Reddot.cs:27-85` | 原路径集合、显示判断和UI刷新流程，复用YL Events/Binding；作为最小试点 | 在原逻辑补会话/退订责任、显式指示器目标，不改Panel/View；多来源登记的新API仍待D3 | null/按段前缀规范化、Reset通知不一致、停用残留/误切业务子物体；两owner、嵌套UI和Reset回归 | 拟复用一个局部路径规范化入口、补字段示例/错误路径提示，减少配置歧义；以等价路径/边界前缀/指示器启停验证；不是红点计数平台 | 多来源语义D3、整套优先级/图/计数系统；不为小功能强制重写Manager |
| E13 LoggerSystem；有效embedded `LoggerManager.cs:100-123,239-259,268-289` | 正确的类型过滤、格式/事件、文件输出及本地已修handler/writer归还实现；YL旧LogUtil退役不是已有正式文件Logger | 以现有实现接YL身份/会话与日志目录，默认不新增自动接管的推荐仍D5；处理线程/递归/handler引用所有权，不批量替换Debug调用 | 针对writer释放、会话文件冲突、回调异常/递归与退出归还补直接回归；阶段9AlwaysLinkAssembly只属原可选包，不能算正式YL结果 | 拟用会话可区分文件名避免同秒覆盖并明确输出路径/失败原因；连续两会话独占读回、失败诊断及正常退出验证，沿用成熟写盘逻辑 | 启用API/全局副作用D5；新后台日志管线、旧Analyzer、恢复LogUtil或新依赖不默认授权，也不机械给整个Runtime贴AlwaysLinkAssembly |

共性隔离问题：XFFSM、CC2D、Moveable、Overlap、Localization、SO、Skill/Buff/Mission 的若干 Runtime 文件存在无条件 `using UnityEditor`；后五类中的多个 asmdef 直接引用 Editor-only。新承接源码必须拆出Editor职责。**本批仅确认静态风险，不宣称发生新的Unity/Player编译失败**，历史生成DLL也不构成当前源验证。

### 3.3 Localization的有限、确定性回退

- 默认回退语言明确为 `SystemLanguage.English`；候选顺序为**请求语言精确匹配 -> 仅中文请求的适用中文变体 -> English -> 显式配置列表中的首个可用语言**，去重后依序查找。不引入通用语言平台或可编排回退图。
- 仅请求 `Chinese` / `ChineseSimplified` / `ChineseTraditional` 时进入中文变体分支：简体缺失时依次试通用中文、繁体；繁体缺失时依次试通用中文、简体；通用中文缺失时依次试简体、繁体。日语、法语等非中文请求不先尝试中文，直接进入English默认回退。
- 最终兜底顺序来自已有配置显式登记顺序及语言列表顺序，不能取Dictionary/扫描顺序；选择语言和按ID取条目沿用同一候选顺序，跳过没有该条目的语言。无可用条目返回明确缺失结果/诊断，不回写原资产、不用旧缓存或强制SetActive掩盖。
- 回归至少覆盖：日语缺项且中文/英语均存在时取英语；中文变体按上述顺序命中；English缺失时按显式列表稳定兜底；所有候选缺项、同值数量但内容已改的缓存失效，以及文本/图片晚到切换。新单Catalog格式仍属D2，回退修复不以改资产布局为前置。

## 4. 先统一的最小支撑与冻结合同

### 4.1 公共准备 E0，单 owner 串行

这不是新基础设施大改。只处理已有源码消费者确实需要、且即将获准实施的组共同依赖的最小缺口；逐项注明消费者后才进入E0。仅一组需要的内容留在该组，无消费者的字段/容器/注册器/Editor基类不预建；其余直接消费现有能力。B9前置不变，不为“准备齐全”扩张B0。

| 准备项 | 推荐落点 / 边界 | 验收 |
|---|---|---|
| Excel字段契约 | 实际落点为 `Runtime/Systems/Excel/Attributes/` 与 `ExcelFieldsType.cs`；Skill/Buff/Mission/SO标记及共享Excel Editor是消费者依据，已证明需要3类Attribute和转换签名枚举，故最小承接这4类；不迁解析器内部`ExcelFields` DTO。namespace为YLFramework.Excel，无OfficeOpenXml/文件IO/旧XF转发，不放Core、不依赖原Excel程序集 | 无第三方DLL的源码/Player profile可编译；消费者可用新YL属性/转换签名；枚举数值和字段赋值边界通过回归。只有元数据，不算xlsx交付 |
| 可重排Editor列表 | 建议落 `Editor/Utilities/TreeViews/ReorderableTreeView.cs`；Skill/Buff/Mission/Localization/SO的现有树为共同消费者。优先承接 `PC/com.xfkj.xfabmanager@c35dc4058e/Editor/Other/TreeView/DragableTreeView.cs` 的可用行构建/选择/重排实现，局部补Undo和实例隔离，复用TreeViewStateGroup；不新写通用树平台、不顺手改造全部窗口或重新迁移XFAB | 多选重排、重复值/空项不被隐式删除、Undo/Redo、同类型两窗口DragData隔离、目标限定资产；与原选中/排序流程对照，无隐式全局SaveAssets |
| 编辑器资源预览 | **已有可用实现** `Editor/Core/Utilities/Preview/EditorAssetTool.cs:8-36`，消费Resource装配的PreviewResolver；与现有AssetNameTools/SplitWindow/SerializedObjectExtensions/CustomPropertyGUI直接复用 | 无需再建EditorAssetTool；未配置资源预览有现有显式诊断；新Editor不扫描旧XF项目列表 |
| 共享目录身份 | B0复用已受管的EditMode/PlayMode `Extensions/`父目录，只为新`EditMode/Extensions/Shared/`、`Fixtures/Extensions/Shared/`及Runtime Excel所需目录生成Unity meta；各组后续只建自有子目录。公共测试归Shared，空fixture父目录以隐藏Git占位保留 | 同一共享父目录只有一个GUID，不在三个Worktree分别生成后覆盖；新来源GUID无冲突，Git检出后物理目录仍存在 |
| 合同/共享边界冻结 | 只冻结实际共用的命名空间、字段标记、重排入口和资源地址接线；保留各Manager可用算法/领域入口，局部注明owner/注册归还。强制多实例、统一服务接口、新Catalog/工厂不作为E0前置，仍受对应D门约束 | 新领域能力无Core反向引用；Foundation/Core源码闭包不变；两个产品asmdef默认不需新增引用，不以“统一”建立IService/表平台 |

### 4.2 可直接复用的真实合同

- Resource：`YLFramework.Services.Resource.ResourceSystem.Acquire<T>(ResourceAddress)` / `AcquireAsync<T>(ResourceAddress)`、`CreateScope()`；`ResourceRequest<AssetLease<T>>.TakeResult()` 只领取一次，领取后仅Dispose请求不释放Lease。使用 `ResourceScope.TakeResult/Own` 或由实际owner持Lease，退出明确释放。
- 图片：`ResourceImage.SetImage(ResourceAddress)` / `SetImage(string)` / `Clear()` 已处理代次、请求、Lease和临时Sprite。Localization仅换输入并拥有自己的组件绑定，不改Resource内核或复制ImageLoader。
- Foundation：直接消费 `UpdateManager` 三相位注册返回的 `IUnRegister`、现有 ObjectPool / Events / Binding / Timer。冷却/持续进度仍逐帧推进，不机械改为Timer；Mono物理组件保持其必要FixedUpdate次序，不强行接统一驱动。
- Session：`Foundation/Singleton/Internal/SingletonSessionState.cs` 已有会话复位与停止门。默认在同Runtime内消费现有能力，不为每系统扩充全局清理钩子；如确需改其语义，先回集成owner，不让分支并改。
- Core：Module/Controller/View可作为应用owner，关闭时归还自身Manager会话/服务/句柄；优先在现有实现补必要清理，不强制各模块改成同一Service模型。**Module.Close不自动管理任意外部系统**。不扩展 `ICoreResourceProvider` 为泛型配置平台，不复活已过时的Module预加载表API，不让Foundation/Core引用新Systems。
- 类型：Editor复用现有类型查找/TypeCache并限制到本系统基类；Runtime优先保留可用的类名绑定和创建入口，接现有AssemblyTool及按需的定向Preserve/link.xml后用IL2CPP验证。改为强制工厂注册或更换执行模型不是普通适配，仍属D1/D3；不先造通用工厂平台。SO根类型通过 `ResourceProjectMount` 显式登记；普通CLR状态/技能类须另验，不能将SO根登记当成全部AOT保留。

### 4.3 承接后真正的前置图

```text
阶段9 Git收口 B9 -> E0公共字段/Editor重排与合同冻结 -> B0
B0 -> A行为运动、B配置数据、C领域玩法/Logger                 （可并行）
现有Foundation -> 各运行态，现有Resource -> 配置根/图片/图标  （基线已有）
E0实际所需字段/重排 -> B.Localization/SO.Editor、C.Skill/Buff/Mission.Editor
B.Excel可选后端通过许可与实现门 -> B及C各自的xlsx作者命令接线
现有Save -> 应用可选的Mission快照持久化                     （非Mission必需边）
```

Skill/Buff/Mission普通编辑器及Runtime不必等待xlsx后端；xlsx按钮/命令的完整接线必须等B组的真实可用API，不能用空实现、静默回退或复制EPPlus让并行看似完成。

## 5. 并行 Worktree 与责任边界

推荐**三个实施Worktree**，理由是行为运动、配置数据、领域玩法的真实源码互相不调用；公共Editor/Excel薄契约先串行，才能获得实际低冲突。原主工作区负责串行公共准备与集成协调，不作为第四个并行产品分支。

下面仅为获授权后的建议名称/路径，本批没有创建、切换或预占它们；创建前验证父目录、分支/路径占用，不能接管已有目录。

| 组 / 分支 / 推荐短路径 | 系统与局部顺序 | 独占产品路径及测试责任 |
|---|---|---|
| A 行为运动；`fusion/xf-ext-behavior`；`G:/Unity/WX/behavior` | E06 Overlap -> E02 CC2D -> E03 Moveable -> E01资产FSM。前三者之间可局部并行，但同组顺序便于物理回归；XFFSM最后且受D1门禁 | 新 `Runtime/Systems/Overlap/`、`CharacterController2D/`、`MoveablePlatform/`、`StateMachine/` 及匹配的 `Editor/Systems/` 子树；`Tests/EditMode/Extensions/Behavior/`、`Tests/PlayMode/Extensions/Behavior/`、自有 `Tests/Fixtures/Extensions/Behavior/`。现有 `Runtime/Utilities/FSM/` 默认只读，不替换其API |
| B 配置数据；`fusion/xf-ext-data`；`G:/Unity/WX/data` | E11 Save承接/缺口裁定 -> E07 Localization -> E09 SO目录适配；E10许可核查可先行，后端通过后再接各自作者工具。实际所需E10字段由E0先完成，B不再复制 | 新 `Runtime/Systems/Localization/`、`DataAssets/`、E0之后的 `Systems/Excel/` 必要维护；匹配 `Editor/Systems/Localization/`、`DataAssets/`。xlsx后端若获准作为可选适配器，归 `Integrations/Excel/Editor/`，不进默认导出；`Tests/.../Extensions/Data/` 与独占fixture。目录名不意味着默认建立新DataCatalog格式；现有 `Runtime/Systems/Save/` 只读，无第二ArchiveManager |
| C 领域玩法；`fusion/xf-ext-domain`；`G:/Unity/WX/domain` | E12 Reddot最小试点 -> E13 Logger（默认不接管）-> E04 Skill -> E05 Buff -> E08 Mission。此为风险顺序，不是假依赖链；xlsx接线最后等B | 新 `Runtime/Systems/Reddot/`、`Logging/`、`Skill/`、`Buff/`、`Mission/` 及对应Editor子树；`Tests/.../Extensions/Domain/` 与独占fixture。领域配置不用B组DataCatalog做基类；不修改Panel、View、原Logger或游戏 |

所有新路径为**建议创建位置，不是已存在实现**。测试放现有 `YLFramework.Tests.EditMode` / `YLFramework.Tests.PlayMode` 覆盖的对应目录，精确子目录分别为 `Tests/EditMode/Extensions/<组>/`、`Tests/PlayMode/Extensions/<组>/`；不要为每系统新增test asmdef。独占fixture使用新目录与Unity新生成meta，不能共享一个临时Scene或覆盖既有Acceptance。

### 5.1 共享区域只由集成 owner 修改

| 共享区域 | 发生交叉的原因 / 操作规则 |
|---|---|
| `Runtime/Foundation/`、`Runtime/Core/`、`YLFrameworkRoot.cs` | A/C消费Update/Pool/Session，B/C消费Events/Module。默认无需修改；确有缺口必须集成主线小批次先实施，分支不能各加公开接口/退出钩子 |
| `Runtime/Systems/Resource/`、`Editor/Systems/Resource/` | B/C需要配置/图标，A的FSM需要资产加载。仅消费现有公开API/注册根类型，不各改ResourceAddress、Lease、mount、provider或预览权威 |
| `Runtime/Core/UI/`、`Prefabs/` | Localization/Reddot/Buff与UI协作，但新组件属于各自Systems；不改Panel/View缓存、模态输入和正式UI Prefab |
| 主体/测试asmdef、asmref、csc.rsp、Analyzer、通用源码边界测试 | 同程序集不等于类型相互依赖；独立性/编译白名单由集成owner维护，不能通过删负例、排除新源码或放宽XF/EPPlus限制完成迁移 |
| E0字段、共享TreeViews/Windows/HelpRegistry | 先冻结后复用；不多份实现拖拽树。系统自有Help提供方可放本系统Editor子树，中央注册机制不改成全局反射扫描 |
| `LICENSE.md`、`Runtime/THIRD-PARTY-NOTICES.md`、`Editor/Distribution/ExportUnityPackage.cs`、Documentation及本Plan | 分支提交事实清单，集成owner串行增量更新。默认Runtime/Editor根已递归导出，无需每组添加新总导出器；可选Excel不得漏进主体导出 |
| `Packages/`、`ProjectSettings/`、`Assets/Games/`、`Assets/Scenes/`、StreamingAssets和原Worktree | 保护区，不属于任何组。需要新依赖、输入/TMP包、后端/裁剪设置或原产物重建时先确认，不以分支权限代替用户授权 |

**离线开发与Unity/Player验收分开调度：** 三个Worktree并行做源码适配、局部回归准备、离线编译/静态自检，产出明确提交/文件状态的候选；它们的离线结果不计为Unity/PlayMode/Player通过。获Git接入授权后，集成owner将候选按批接入主工作区，串行执行主工作区的Unity编译、EditMode/PlayMode、代表Player和原游戏非干扰门；候选接入不等于已验收。无原XF的首导入/IL2CPP/独立Player仍在独立接收工程验证，不冒用主工作区通过证明独立交付。

每次Unity操作先核对真实 `projectPath` / Editor实例 / 输出根，禁止Worktree代理同时操作默认主工程Unity/MCP，禁止对同一工程路径启动第二个Unity实例。主工作区和独立接收工程的有状态操作分别由单一owner调度；未获得正确连接时只继续离线工作，不擅改工具配置。未执行、失败或阻塞的门保持对应状态，不因“已交分支/已编译”计通过。

### 5.2 集成顺序与失败边界

1. B9收口后，仅将已有消费者需要的E0内容作为共享批次，主工作区验收后形成B0；三组从同一B0开始，受保护对象/忽略制品恢复条件分别记录。
2. 先接C组Reddot试点及默认关闭的Logger、A组Overlap；各组离线自检形成候选，按上节调度串行接入主工作区，完成相应Unity/Player门后才标为已验收。失败只阻断相应批次，不能修改原包掩盖。
3. 接B组Localization / SO与A组CC2D / Moveable；它们可同时开发，集成owner按到达的完整门禁逐项接入，不要求等整组大合并。
4. 接C组Skill / Buff / Mission运行时及保留/适配的普通作者工具；不因该顺序强制建立新Service模型。E10通过版本/许可/后端门后，先集成B的可选适配器，再由B/C各自接线作者命令。接入前同步已验收基线须另有Git授权，不反复将未提交文件拷来拷去。
5. XFFSM的资产/运行时与Editor工作流最后按D1结论收口；所有选择交付的系统再做组合独立验收。Excel或XFFSM来源门未闭环时，可完成不依赖它们的批次，**不能把全13项标为已经全部融合**。
6. 每次允许集成的批次包含代码、必要测试/meta及本Plan验收记录；Commit/合并/Push仍按当时明确授权执行。失败保留证据，修复回到同一owner，禁止自动reset/删分支/覆盖别人的工作。

## 6. 执行任务与验收矩阵

### 6.1 通用门禁，实施后执行

| 门禁 | 具体要求与可复用设施 |
|---|---|
| G0 离线/静态 | 比对来源/许可、所有新文件及meta、序列化字段/AQN/字符串/GUID/程序集边；逐批简记保留能力、适配、Bug、采纳的小改进及候选未做原因，普通细节不另行审批/新建审计文档。Runtime无原XF类型/Editor依赖，可选Excel无Runtime库依赖；检查正/负例，不用“grep没找到”替代编译。可复用 `Tools/Verify-UIPanel.ps1 -Mode Runtime`、`Editor`、`EditorTools`；先核对本机输入，Roslyn/离线结果不能算Unity通过 |
| G1 Unity编译/EditMode | 在主工作区集成候选上使用实际Unity编译图、源文件、rsp和AssemblyRef。复用 `FoundationBoundaryTests`、`CoreArchitectureBoundaryTests`、`FrameworkAssemblyIndependenceTests` 的源码闭包/白名单门；新增系统不得被漏掉。保留配置创建/保存重开、类型绑定、Inspector/图/列表作者流程；验证实际采纳的诊断/Undo/预检操作，不以新UI替代原可用功能或未经裁定删入口 |
| G2 PlayMode/生命周期 | 真实资源/owner初始化、关闭、场景切换、inactive/Destroy/缓存View、订阅/三相位句柄/池归还、迟到隔离。涉及静态服务者执行真实关闭Domain Reload的连续两轮Play；不用手工清static伪造会话通过。拒绝覆盖未保存场景，测试只清理自己对象/资产/状态 |
| G3 Player | 每组结束及确有新增Player风险的中间批次，用专用消费者强制引用新能力，Windows64 Mono实际构建+图形进程运行+正常exit0+退出后日志检查；不机械每次小改重复构建。组结束以非Development结果收口；开发构建可调试但不替代。涉及反射/多态/泛型事件及Logger的IL2CPP Low须在独立接收工程实际构建/运行；裁剪级别不外推Medium/High/其他平台 |
| G4 独立交付 | 真实正式Exporter输出，核对逐文件/meta/GUID/hash/许可/依赖，排除游戏、Tests、秘密、原XF、旧Analyzer/未批准DLL。全新接收工程无原15系统、无MCP/主工程DLL，先首次导入编译再安装测试设施；可选适配器先不装，产品自身通过后才测可选能力。检查Editor/Player实际程序集、Missing Script、类型构造、资源释放、正常退出 |
| G5 原游戏保护 | 受保护文件/meta/hash/配置/Bundle输入无越界差异；原游戏仍走原XF/XFAB/XFFSM/Overlap与既有YL音频。按集成风险做菜单/设置/进入/暂停返回/再入/结束重开代表回归，最终组合检查一次；不机械每个纯独立小批次重跑全量游戏，不重建MainModule或覆写忽略产物 |

源闭包探针里用于编译的fixture、Test Runner执行、Player消费者必须分别报告。现有阶段9脚本与工程的owner/制品绑定不能冒用：复用源码和安全模式另建本专项接收根/Run，不重写 `G:/Unity/Y9` 的历史记录，也不把阶段9consumer没有覆盖的新能力算通过。

### 6.2 13项必测契约

表内测试类为建议新增的归属/筛选名，当前不存在；名称不要求改成新的Service/Catalog执行模型，按组落入 §5 独占目录。每批先选原成熟算法/Editor工作流的代表正常路径作对照，再为已知Bug补直接回归；能执行时先确认失败再修，只有静态证据/环境阻塞时如实记录，不能预填复现或通过。实际采纳的局部增强须补操作成功、错误/取消、生命周期等完整流程用例；候选未采纳不强加功能/空测试。性能改进比较相关成本或测量，不能只用“测试绿了”证明更快。最后由主工作区和独立接收工程分别执行其门禁。

| 项 / 建议测试类 | 离线与EditMode重点 | PlayMode / Player / 独立交付重点 |
|---|---|---|
| E01 `AssetStateMachineTests` | 原默认状态、层/子状态机/转换/条件/类绑定及必要图形编辑、保存重开流程对照；新GUID、无旧键/全工程扫描；缺类型/目标可定位；Editor来源门完成，工厂不是默认改造条件 | 原OnCreate/Enter/Exit和三相位顺序、Trigger消费、禁用重置/隔离/关闭后不再回调；数据驱动IL2CPP实际创建状态，不靠静态new掩盖裁剪；完整作者/运行闭环，不以删Editor换取独立编译 |
| E02 `CharacterMotor2DTests` | Collider配置、层矩阵、事件保存/重开，Runtime无UnityEditor | ground/wall/top、跳跃次数/重力/旋转、禁用恢复、多实例/多Collider及Fixed步长；Mono与IL2CPP泛型事件/序列化组件强制包含；无旧CC2D |
| E03 `MovingPlatformTests` | 路径编辑、零/单点/零距离、Transform删除、UnityEvent保存 | Positions/Transforms、Loop/PingPong/Once、delay/pause/stop、载人/多刚体多Collider、Fixed时序；Mono强制走到载人，IL2CPP验证组件/事件；不因删无用CC引用丢行为 |
| E04 `SkillLifecycleTests` | ID/类缺失/重复配置、类型绑定、普通Editor创建/重开，配置不变 | Release/Cancel/Interrupt、冷却进度边界、inactive/非Unity宿主/移除/池复用/服务Dispose；IL2CPP仅由注册/配置驱动子类；配置Lease和Update句柄归零，无类池Manager |
| E05 `BuffLifecycleTests` | 配置/EffectData/图标，缺类型明确失败，Editor退出不改配置 | Single/Additive/永久/到期、重入移除、host拒绝/inactive、最后Buff效果缓存清理、View缓存/关闭但不接管View；Mono/IL2CPP效果与Buff子类真实构造 |
| E06 `OverlapComponentTests` | 原形状设置/Results语义、2D/3D Reset正确层矩阵及事件保存对照；采纳容量提示则测“可能截断”的边界/限频和正常帧分配，不误报必然丢命中 | enter/exit、频率/时间缩放、disable/enable、双实例/回调重入、多Collider/fake-null；Mono/IL2CPP真实物理/泛型事件；旧8个Prefab保护，优化不偷偷改检测时序 |
| E07 `LocalizationTests` | 保留语言资产编辑/格式用途；重复ID/空项/同数量内容变更的缓存、缺键诊断和偏好键；按§3.3测试非中文不先回退中文、中文适用变体/English/显式顺序及全部缺失 | 先Enable后配置到达、切语言/图片晚到、关闭/重开/停用/失效；直接刷新不增加业务OnEnable/OnDisable次数；ResourceImage清理自身资源；Mono语言持久化/IL2CPP文本图片；未装TMP首导入与可选项状态分别报告 |
| E08 `MissionLifecycleTests` | 配置/参数/奖励、Float全部比较、重复键拒绝、删除操作只作用自有路径 | 两owner共享定义但状态隔离、开始/进度/完成/Reset/重开/中途失败回滚、退订/配置移除；源资产前后不变；快照若纳入仅测现有Save边界；IL2CPP任务子类无默认降级 |
| E09 `DataCatalogTests` | 对照原根/子资产、类型选择和CRUD/排序；空Min/Max、重复ID/缺引用诊断、重开/Undo/删除确认，不要求新Catalog布局 | 按D2确定的可用入口查询/释放，根Lease保活子资产，晚到/关闭/会话隔离；Mono/IL2CPP具体子类；无XF依赖和未归还静态引用，不以彻底删Manager/类型绑定作为通过条件 |
| E10 `ExcelAuthoringTests` | 字段元数据独立；后端许可/hash门后测真实xlsx三行头及转换、已选SO/List/JSON入口对照；新增/更新/拟删除预览、确认/取消、enum/无效GUID/坏值/故障注入，失败取消后原资产不变 | 后端不进入Mono/IL2CPP Runtime AssemblyRef/Player；未装后端基础包可首导入。真实文件导出再导入并核字段/对象引用，单独报告各作者接线；只有metadata/预览截图不算xlsx完成 |
| E11 Archive承接裁定 | D6确认Save覆盖项及模型ID/当前项/异步/旧格式真实差异；无代码改动沿用现有Save证据，不新建空测试或重跑55项。如采纳窄映射，新增其直接槽位/对象磁盘往返回归 | 推荐不迁移仍待D6，不把Save通过冒充原Archive API/旧档兼容已通过；组合包无原Archive/XFAB依赖。真实样本触发另授权导入器时才增加旧档/备份/版本/模型AOT门 |
| E12 `ReddotTests` | 路径规范化/按段前缀、Reset通知、null/重复及字段提示；按D3裁定测试同路径多来源，不先强制新计数/登记API | owner/来源隔离、缓存View启停、只控制明确指示器、销毁退订、关闭后无回调；Mono/IL2CPP实际UGUI，原集合不被清空；已采纳的小增强有等价路径到显示/清理闭环 |
| E13 `LoggerSessionTests` | 默认不创建宿主/不改handler/不写盘；独立YL目录、过滤与格式/事件异常/线程和递归策略 | 显式启用后记录五类日志/异常，重复Play两代writer独占读回、同秒新会话、disable/destroy/退出释放；后续handler接管时不覆盖。先无XF独立Mono/IL2CPP Low正常退出，再测原包共存；不能以文件存在或旧Logger tarball通过替代YL正式能力 |

### 6.3 每组交付完成标准

| 组 | 独立可拒收的交付与门禁 |
|---|---|
| A | Overlap、CC2D、Moveable、资产FSM分别是可测试批次。每批G0-G2与受影响的组件/序列化门；组内专用Mono消费者覆盖全部实际交付项，IL2CPP覆盖事件/工厂/资产引用，最后G4无旧运动/FSM包。D1未解决时XFFSM保持待处理，不能以轻量FSM现存替代该项完成 |
| B | Localization/SO保留成熟数据/作者流程后做适配、首导入/Lease/Player闭环，不以新Catalog为默认验收条件；Excel元数据、后端/真实xlsx和各入口分别报告；Archive覆盖/真实差异及不迁移建议等待D6裁定，不虚报已批准。Runtime不增EPPlus/TMP必需依赖、不改Save格式；G4未装可选后端必须通过 |
| C | Reddot试点先过owner/UI退出门；Logger单独过handler/writer及无XF IL2CPP门；Skill/Buff/Mission逐项过配置不变、运行态隔离、Update/Pool清理与多态AOT。组结束G4无原Controller/类池/Events/Excel程序集。xlsx未接入必须如实列缺口，不通过关闭原Logger/分析器让结果变绿 |
| 最终集成 | 三组选择交付的功能组合，执行G0-G5及受影响的阶段9代表链；新导出与新receiver绑定真实提交/文件SHA，旧阶段9证据保持不动。只验证Windows64 Mono/IL2CPP Low，不宣称可闻音频、High、移动/WebGL或零native泄漏 |

## 7. 有序实施清单

- [x] **T0 本轮授权与B9基线。** 用户已明确授权仅B9/E0及各自普通Push；B9完整SHA和真实远端一致性见§10.1。原用户meta保留，D1-D6及Worktree/13项实施仍未授权，不以此勾选代替后续裁定。
- [x] **T1 E0公共准备及必要验收。** 按真实消费者仅承接4个字段C#和1个成熟重排C#，复用既有预览/资源合同；Unity编译、限定43项、实际输入/meta和一次Review修复已完成，见§10。E0实现及必要指纹纠正后的最后集成基线定义B0，完整SHA/远端记录在回执与最终报告，不自引用预填；无xlsx后端或领域迁移。
- [ ] **T2 建立三个隔离工作区。** 经授权按§5名称从B0创建；核对每个工程依赖恢复、旧包共存/独立验证的不同目的；只恢复必需且获准的依赖/制品，不复制主Library或现有dirty文件。
- [ ] **T3 并行各系统批次。** 各owner按§3五类内容保留成熟实现、局部适配/修Bug，在授权边界内自主采纳有收益的小改进；以正常路径对照+缺陷回归/必要测量验证，候选未做不强加。离线自检只交候选，主工作区/独立接收工程按§5-6验收；不自行改Core/Resource，不攒整组大提交。
- [ ] **T4 可选与高风险收口。** 先解决EPPlus二进制/许可和接线，再做真正的xlsx作者闭环；按D1完成资产FSM工具。许可门失败时停止相应交付，不能用包外MIT、空函数或隐式禁功能绕过。
- [ ] **T5 集成与独立交付。** 按§5.2串行接批次并按影响回归；独立新接收根执行G3/G4，产品通过后再测试可选适配器/原包共存；最终原游戏代表非干扰G5，增量维护唯一蓝图入口与现有说明。
- [ ] **T6 用户验收/Git收口。** 本Plan逐项记录已实现/不迁移裁定/可选未交付、实际证据与未验证项；只按额外授权提交/合并/推送，报告真实Hash和同步状态，不自动删除Worktree/分支或启动其他专题。

## 8. 待用户裁定与明确未授权事项

以下是重大选择的推荐方向，**D1-D6均仍待裁定，本轮没有将任何一项转为已批准**。本轮仅确立成熟融合/局部修复与小改进的执行原则；只有对应批次后续另获实施授权，才可在未触及大取舍、未批准依赖和共享契约的范围内自主处理普通细节，不反复请示。大规模替换执行模型、跨模块API/存档格式变化、新依赖或明显功能删减仍先过相应门。不受影响的盘点与顺序沿用，不重新全库研究或循环全面Review。

| 门 | 推荐决定 | 影响 / 未获裁定时的停止位置 |
|---|---|---|
| D1 资产FSM及作者工具 | 保留轻量FSM和XFFSM成熟资产/层级执行、必要作者工作流；只对内部API/来源风险做定向查证及必要局部替换，不默认统一内核、强制工厂或重写整个Editor | 原API/执行模型大改、来源处理及作者功能范围仍未决；先明确保留能力/风险文件处理，不以删Editor或只有Runtime编译替代完成 |
| D2 配置数据布局 | 优先保留Localization多语言资产、SO根/子资产和可用类型绑定，局部接YL并修缓存/所有权；领域配置保持本领域。原单Catalog/去type_script_guid等提议保留为待裁定项，不是默认前置 | 新资产格式/公开数据入口变化尚未批准；无本项目静态资产不等于外部不存在。实际有样本再定迁移，不因本轮原则自动删旧流程或建设通用表平台 |
| D3 领域生命周期与红点语义 | 尽量保留各Manager核心流程，在必要字段/注册处补owner、退出和配置/运行态隔离；多实例公共入口、红点同路径独立登记责任及明确指示器语义仍待裁定 | 不自动把所有Manager换Service/统一工厂。Skill/Buff inactive暂停、关闭清理和重入按契约验证；局部Bug修复可做，跨模块API/大执行模型变化仍须裁定 |
| D4 Excel边界与依赖 | 主体仅放消费者需要的字段元数据；真实xlsx保留成熟转换/作者用途，后端为可选Editor适配器，先核实际版本/来源/完整许可 | 元数据不替代xlsx；依赖/后端及原SO/List/JSON等作者入口的取舍未决，明显删减须单裁。当前不批准新DLL/UPM/白名单或手写解析器；可选adapter asmdef若需要另审，不能凭用户同意代替第三方授权 |
| D5 Logger启用方式 | 正式YL Logger显式启用、默认不接管，独立YL目录；不保持原XF静态API兼容、不恢复LogUtil、不批量改Debug调用 | 全局日志副作用与新公共API取舍；裁定前不实施。原embedded包与阶段9可选tarball始终保留原归属，不由本决定获得修改权限 |
| D6 不迁移/可选范围认定 | 推荐Archive由Save承接，不建第二存档系统，但须确认模型ID/当前项映射、异步及旧`.sav`/DES兼容差异；CC2D/Moveable保持可选组件。TMP/Other反射/翻译/Steam作者窗口等选配范围仍明确裁定 | 不迁移Archive及明显删减原有功能尚未批准，不以Save已有或“静态未使用”抹平真实缺口。真实需求/样本出现后再定窄适配或导入；新增依赖、异步/加密/存档格式另授权，旧Analyzer不重复迁入YL |

首次规划轮的远端TLS失败属于历史核查限制，不是架构选择；后续B9已恢复真实远端查询并核实SHA，见§10，未关闭TLS验证或把旧跟踪值冒充实时同步。所有Worktree/分支路径仍只是推荐，实际可用性在获授权创建时再确认。

## 9. 规划修订轮交付与验证边界（历史）

- 沿用首轮：13项来源/依赖/资产/Editor/许可研究、阶段9dirty及B9前置、三组Worktree和主要集成顺序。首次远端TLS失败仍未解决，本轮未重查，不能据旧跟踪记录声称远端当前同步。
- 本轮修订：只修改本Plan，补成熟实现优先与自主局部改进边界、13项五类处置/收益/验证、有限确定的Localization回退、最小公共准备、离线与主工作区/独立接收工程验收调度；D1-D6仍未决。
- 本轮实际核查：读取原Plan、只读Git工作区状态/差异和修订内容一致性检查；没有重新全库盘点或循环全面Review。文档校核不是运行时验证。
- 本轮未执行：Unity操作/刷新/编译、离线辅助编译、EditMode/PlayMode、Player构建/运行、源码迁移、Package或工程配置修改、提交/推送/合并、分支切换或Worktree创建。§6全部为未来门禁，未执行不得计通过。
- 现有长期蓝图及阶段9验收证据均未改写。本轮到交付修订摘要为止停止；打开Plan不自动授权实施。

## 10. B9与E0本次执行记录

本节记录2026-10-19后续明确授权的实际执行，覆盖上文规划轮的“本轮仅文档”历史状态，不扩大到13项系统。当前用户授权只含B9与E0及各自普通Push；D1-D6继续待裁定，三个Worktree未创建。

### 10.1 B9已完成

- **B9完整SHA：`41c6c21bdb0413fd31fd7f15640af7397e8961e1`**，标题 `fix(delivery): 完成阶段9独立工程交付验收与分发修复`。本地HEAD、`origin/integration/xf-yl`与普通Push后实时`git ls-remote`三者一致，真实远端查询已恢复；未fetch、force、reset、stash、clean、rebase或改写历史。
- 收口前本地/实时远端均为`586eebedaf900d926d2e30301b20ed3129decb27`，暂存区空，无索引锁；Agent Manager未见活跃Git收口会话。先只读核对冻结`delivery-binding.json`的26份当前文件全部SHA匹配，再更新阶段9用户验收文字。原失败和限制保留。
- B9为25个变更记录，含正式Exporter许可根收窄、Windows输出路径拒绝及回归、验收构建器与meta成对移动、六组稳定工具及meta、原embedded Logger初始化裁剪根修复、必要说明。新拓展Plan及用户`AlternateContent.meta`没有混入B9；受测产品代码未另改。
- B9完整暂存diff/敏感信息与`diff --check`已审查。首次检查报六份工具meta的18处空值行尾空白，随后只规范这些空白，保持GUID/Importer/值不变；原冻结SHA不回写成规范后字节。没有重跑阶段9完整矩阵、重新导出或覆盖`G:/Unity/Y9`。
- 最终框架包SHA仍为`d1710ed15ac86048f1a479d04779a776a27520060c35aeb2e39c8b4fc5d2a91c`；可选Logger为`a156bfd07c687bebd28d0abec42e639c8e56f6217a75db4f77bf624db7542057`；绑定JSON的权威SHA为`b85aad87a7dbd43e01d79e0fa36781ebf66c0d7f01d8fd3a1b513c0333e23d0b`，与原`delivery-binding.sha256`一致。这是只读身份核对和沿用验收，不是重新运行的Player结果。

### 10.2 E0真实范围与共用接口

| 能力 | 实际交付与消费者 / 有意未做 |
|---|---|
| Runtime字段元数据 | `YLFramework.Excel.ExcelIgnore`、`ExcelScriptableObjectID`、`CustomExcelFieldImportAndExport`及`ExcelFieldsType`，共4个C#，归现有Runtime。`ExcelIgnore`被Skill/Buff/Mission及SO使用；Buff数据使用ID/转换标记，B组的共享`ExcelManager`读取这三类标记，构成真实跨组接线；枚举是转换签名所必需。没有消费者需要在E0交付解析器内部`ExcelFields` DTO，故未迁入 |
| 字段合同 | 保留属性类型/无参构造、枚举原数值0-9及自定义`object Import(object, object, ExcelFieldsType)`、`object Export(...)`、`void SetValue(object, FieldInfo, object)`。只有BCL元数据，不含文件IO、EPPlus/xlsx后端、领域转换器或兼容XF转发；未来业务明确使用YL namespace |
| Editor重排 | `YLFramework.Core.Editor.ReorderableTreeView<T>`，`T : class`，归现有Editor；保留TreeView行构建、原列表移动流程和`OnBuildRoot`/`RefreshDatas`/`OnDragEnd`扩展方式。局部按索引移除/插入，保留重复引用/空槽及源顺序；修即时放置和未选行拖拽、悬停/原位放回不改脏数据，校验快照/范围/每实例责任。Review另补行构建引用序列，绘制/起拖时识别等长重排，拒绝陈旧行首次拖拽，避免同资产双窗错移对象 |
| owner与Undo合同 | 构造`(ScriptableObject owner, Func<IList<T>> getData, TreeViewState state[, MultiColumnHeader header])`；getter取得owner当前序列化列表，Undo替换列表后不继续写旧引用。实际重排登记owner Undo、只SetDirty不自动SaveAssets，Undo/Redo后刷新；窗口停用必须Dispose归还事件与本实例拖拽。默认一槽一行，过滤/自定义ID覆盖`GetItemIndex(int)`返回真实索引，不引入领域基类或统一服务 |
| 元数据/目录 | 9个新C#（5产品、4测试）与5个确需新目录的14个meta均由主Unity生成，实际路径/GUID及raw Assets唯一性见`input-guid-audit.json`。EditMode/PlayMode的`Extensions`父目录原已受管，未另造；一次导入前创建产生的自有空`Shared 1`已用AssetDatabase删除。共享空fixture目录以隐藏`.gitkeep`保留，避免B0检出后只剩孤立folder meta；它不是Unity资产，无伪造meta。测试只创建/清理自有Run临时资产/子资产和窗口 |
| 来源与说明 | ExcelSystem 1.0.7 `725f94f23327de190111573870146af66e9484b6`，MIT/2025弦风课堂；XFABManager 1.24.0 `c35dc4058e` 的平面拖拽树，MIT/2021弦小风。原版权与完整MIT归入根LICENSE，Notices和Architecture增量记录；不重新迁移XFAB资源系统，不携旧Analyzer/DLL |

资源预览、Layer/资源加载/生命周期仍消费原能力；没有修改Core、Foundation、Resource产品源码/公开合同、asmdef、Package/锁文件、工程配置、场景、原游戏或旧包。新E0代码没有被接入Skill/Buff等模块。不是13项模块完成，也不是Excel完整功能完成。

### 10.3 定向验证、失败与修复

证据根为`Logs/YLFramework.Extensions/E0-20261019-152633/`，不覆盖Y9或旧失败。原始测试返回按运行分别保存，未删除红例或用后续成功覆盖。

| 记录 | 实际结果与边界 |
|---|---|
| `red.json` | 缺E0能力时20/20预期失败，无missing-symbol导致的工程编译中断；使用反射入口让缺合同/组件形成明确失败 |
| `green-1-timeout.Editor.log` / `green-1-log-prefix.txt` | 首次GUI测试调用Native StartDrag，进入原生桌面拖拽，MCP等待超时，**不计通过**。整份日志含大段NUL，另保留4,391,630字节可读前缀；没有把日志读取OOM称为产品OOM。仅对已确认PID/自有窗口尝试关闭和定向取消，没有全局键鼠注入/关闭Editor；最终用户确认Esc解除并取消旧测试 |
| GUI测试调整 | 保留生产`SetupDragAndDrop -> PrepareDrag -> Native StartDrag`流程；将真实准备与OS启动边界分开，测试在真实EditorWindow中验证CanStartDrag、真实PrepareDrag、HandleDragAndDrop及Undo/Save/Import，不再启动阻塞OS拖拽。不声称物理鼠标/原生OS Drag已验收，不以空函数或跳过数据断言制造成功 |
| `green-2.json` | 最初20/20通过，含重复/空槽、多选源顺序、快照拒绝、原位/悬停、Undo/Redo后二次操作、磁盘保存重导入、双窗口及Dispose |
| `red-selection.json` | 未选行拖拽1/1失败（实际True、预期False），补回原CanStartDrag的当前选择约束后通过；另补同一资产双窗口责任和过滤行真实索引用例 |
| `gates-1.json` | 限定42项，39通过/3失败。3项来自既有Foundation守卫旧输入/友元预期，不忽略或降低断言；没有以过滤失败测试改成成功 |
| Foundation守卫纠正 | `YLFrameworkRoot`及`YLFramework.Core.Tests.EditMode`友元均来自既有`09cc1df0116df1ed69ba43539e9f0cca01b9e9c5`，与B9相比未改。Root已使用Core.UIRootContext，由未修改的完整Foundation+Core+Root守卫覆盖；纯Foundation改为只收该目录全部源码，仍保留同参考/宏/正例/Core负例与产品DLL拒绝。只将预期友元补齐基线已有的一个精确名字，未改运行时友元或扩大白名单 |
| `gates-2.json` | **42/42通过、0跳过**：23个E0用例加19个Foundation/Core/整Runtime+Editor及实际程序集闭包/白名单门。范围不包含完整Resource/UI/阶段9矩阵 |
| `review-red-stale-rows.json` / `review-green-stale-rows.json` | Review指出的同资产第二窗口旧行问题直接先红1/1再绿1/1，覆盖外来重排后陈旧节点拒绝、刷新后新起拖实际对象一致，不只测拒收另窗payload |
| `gates-final.json` | **最终43/43通过、0失败/跳过**：24个E0用例加原19个限定边界门。Review后重跑同一门禁及新增直接回归，没有重跑完整Resource/UI/阶段9矩阵 |
| 离线源码探针 | `Tools/Verify-UIPanel.ps1 -Mode Runtime`退出0（41个warning）；`-Mode EditorTools`包含Editor profile退出0（40个warning）和EditorTools退出0（4个warning）。有既有告警，不写成零告警；这是Roslyn辅助编译，不代替Unity或Player |
| `input-guid-audit.json` | 实际主Unity PID32168、项目路径匹配；Runtime436/Editor93份实际编译输入覆盖全部5个新产品C#，无固定清单漏收；14个新GUID在raw Assets逐一唯一；自有fixture/窗口均为0，用户meta SHA与冻结记录一致；原UIAcceptance场景已保存、未dirty，源码编译无失败 |
| `unity-final-state.json` / `unity-restored-state.json` | 最后测试回调时短暂包含一个无dirty的空Runner场景；后续核验已由Runner自行回收，实际关闭数0，不保存/关闭用户场景。最终仅原UIAcceptance（1个根、无dirty），主PID32168仍为EditMode，无编译/更新/编译失败，自有窗口和临时fixture均为0 |

本批没有构建/运行新Player或新无XF接收工程，没有重新验阶段9或IL2CPP矩阵；只对本批BCL字段和Editor工具执行必要源码/Editor门禁。不由Development源码profile编译外推真实Player/其他Unity版本/13项运行时功能。

### 10.4 一次实现Review与E0 Git收口

**一次限定实现Review已完成。** 只读审查本批5个产品源码、4个测试文件、旧Foundation守卫纠正及来源/接口边界，不操作主Unity、不写Git、不扩大到全库。唯一实质发现P2为“同资产另一窗口等长重排后，旧行按新数据索引错移对象”；已在同一修复批次补引用序列/节点身份校验和陈旧起拖拒绝，直接红1 -> 绿1，最终43/43通过。普通GUI状态比较只逐项比引用、不在未变时分配新序列；没有引入全局窗口平台，未宣称经Profiler验证性能提升。

Reviewer确认Foundation修正仍保留完整目录输入、原参考限制和指定Core负例，Root由未改的完整Core守卫覆盖；字段合同/许可和真实PrepareDrag/HandleDragAndDrop测试边界成立。其余范围无有证据的实质发现，无待补的重要项；未再发起第二次Review。物理鼠标/原生OS拖拽、新完整Player/干净首导入不在此结论内。

E0精确暂存范围为29个文件记录（含本Plan及共享目录`.gitkeep`），用户原meta不纳入。首次完整`diff --check`发现14份Unity新生成meta的42处空值行尾空白，仅规范这些空白，GUID/Importer/值不变；不为纯文档、Git或该空白整理重复技术矩阵。完整暂存差异、敏感信息和最终空白检查为提交前门禁，后续回执记录实际结果；不调整Git配置、放宽检查或造空提交。

E0实现提交为 **`a35783d8916f719a548c78b5d05ded3793da5049`**，标题`feat(extensions): 完成拓展融合公共准备与接口约定`，29项范围已精确提交/普通Push且实时远端核验一致。

后置收据曾因本Plan与收据常量将绑定SHA误抄为`61db7f...`而拒绝。只读核查确认权威JSON和sidecar的最后写入分别为`2026-10-19T14:13:02.6753128Z`、`14:13:02.6773179Z`，均早于本轮`15:26:33Z`授权开始，且两者一致为上述`b85aad...`；没有证据支持“本轮外部改写”。这是本次记录抄录错误，不能归咎于历史制品或通过写Y9修平。保留错误核验事实，仅追加一次必要的本Plan指纹纠正提交；不amend/force、不改受测代码、不重跑未失效矩阵，也不为Git状态另造空提交。

B0不等于B9，定义为同时包含E0实现与该必要指纹纠正的最终集成基线。其自身完整SHA不能预填到同一提交形成自引用；普通Push后，真实本地/远端SHA、原E0实现SHA与纠正范围写入自有`git-receipt.json`及最终回复。两次主批次和额外必要纠正如实分列，不将后者藏成原来的29项实现提交或改写原提交身份。

### 10.5 A组主工作区接收与BM26验收（2026-10-20）

本节依据本次明确实施授权接收A，不批准整个XF阶段或B/C集成。主工作区为`G:/Unity/Projects/CGJ2024`、分支`integration/xf-yl`；集成前HEAD及共同基线均为`c88500aef6651f5b13ac933dd1f209ecb314e92d`。固定接收`ce1898e466d81cda187440293e89e2b596a84a18`，最后产品/测试/工具/meta变更为`209bceb87a76d8004db16c60d0899fcc6fc698b4`。已核对完整6提交、288个路径、151份正式meta，以固定SHA执行`--no-ff --no-commit`，无冲突、不复制A目录、不接分支后续提交。

主工程独占证据根：`C:/Users/Kano/AppData/Local/Temp/kilo/BM26-20261020T042500Z/`。`preflight.json`绑定实际主路径、集成前HEAD、候选集合、预存差异、3215个既有文件SHA及Unity进程；最终提交与工作区状态由同根`git-final.json`记录，避免提交SHA自引用。A的BA26及阶段9/E0证据未覆盖。

| 门 | 本轮主工程实际结果 |
|---|---|
| Unity导入/编译 | 实装`2022.3.62f3c1`，现有主Editor PID4708，未启动第二实例。`audit-final-01/import-audit.json`：compileFailed=false，116份本组实际源码逐项进入正确六程序集，18份真实rsp归档；Player/Editor图分别检查，不以Editor profile的合法条件引用冒充Player泄漏 |
| 程序集/边界 | 原Foundation/Core/框架独立性门`boundary-01`19/19，未放宽白名单。`player-assemblyref.json`被动读取实际Mono Player两个自有DLL，无UnityEditor/旧XF/EPPlus依赖，Player目录无Editor DLL |
| GUID/meta/场景 | 151份A正式meta与冻结提交逐字节一致，在全Assets中相关GUID逐一唯一，AssetDatabase映射一致。新增主验收入口和原游戏回归的两份meta由主Unity正式生成。3份自有场景原生加载，Missing Script=0，Player序列化事件引用完整，原场景集合未变 |
| EditMode | `edit-baseline-01`60/60、事件补例61/61；`save-fence-green-01`最终62/62=原60+持久事件保存/重导入1+未保存资产保护1，0失败/跳过/不确定。实际SaveAssets被过滤后自有脏Material字节不变、仍保留未保存值/dirty；逐例XML另存 |
| PlayMode/原游戏 | `play-01`50/50；原游戏补例后51/51，`play-final-01`在保存保护和逐轮断言下复验。原菜单/设置Slider/进入/真实暂停返回/再次进入及退出清理全部执行；每轮须各自捕获原Overlap自然查询，不能只看两轮累计。仍走原XF/XFAB/XFFSM/Overlap，未重建MainModule/Bundle |
| 同进程Reload/绑定 | `reload-01`三组合各连续两轮6/6，PID始终4708，双FSM各轮独立起步/转换，源资产SHA不变、退出无克隆；未手清static。`binding-01`真实源码创建、编译/程序集重载、产品pending-binding、保存重导入通过；原Reload选项false/3及UIAcceptance场景集合恢复 |
| Windows64图形Mono | `mono-01/build-report.json`：Mono2x、Disabled stripping、BuildOptions.None、非Development、Succeeded，0 errors/1225 warnings。有窗口Direct3D11 Player PID28036退出0且唯一PASS，不传batchmode/nographics；BuildReport/窗口句柄/exe与log SHA均已绑定 |
| Player事件 | 原生Prefab保存/重导入后消费十条持久绑定；实际计数hit2D=2、hit3D=2、CC2D Bool=3、ground=2、wall=1、top=1、Int=2、Vector2=26、平台Vector3=25；状态creates/enters/exits=2/2/2，不能只由类型引用或Preserve代替执行 |
| GUI未验项 | 自动Graph/Inspector作用域、条件/参数Undo/Redo、保存重导入和真实脚本绑定已有测试证据；物理鼠标节点拖拽/框选/缩放/面包屑/子图连线、条件重排、脚本选择、Overlap/平台Inspector实际编辑及Scene手柄、亮暗主题完整作者流程未计通过，无伪造截图或handler代替鼠标 |
| IL2CPP/G4 | `il2cpp-01/preflight.json`为BlockedBeforeBuild：当前Mono2x/Disabled，虽有IL2CPP Windows支持及VS2022 C++工具链，但未授权切换后端/Low，未构建/运行IL2CPP。无旧包接收工程、正式Exporter/G4独立交付未执行 |

修复仅限本轮自有验收：Player真实事件消费者及保存重载回归、原游戏观察时机、主工程绑定入口/保护检查和集中许可说明。没有修改A产品算法、原轻量FSM、Core/Foundation/Resource产品、共享E0工具或原游戏资源。A历史离线53项、源码阶段独立Review及BA26真实证据仅沿用，不混入上述新主工程统计。

失败原件保留：首次事件用例遇Runner自有untitled场景，改用自有Prefab生成；`events-red-02`明确消费者缺口后61/61；`play-game-01`观察窗口过早未见旧Overlap，改为等待自然查询后51/51，原断言未削弱。主入口一次错误引用不存在常量的编译失败已修正并经最终Unity编译复验；最初审计误把Editor profile引用当Player引用的失败也保留。Script Updating Consent仅对三个A旧Handles文件选No，未让API Updater改源码。

首个Mono构建曾临时改公司/产品名；finally只恢复内存，定向Unity保存尝试仍未落盘。已仅精确逆转本轮造成的两行，并核对全部ProjectSettings与构建前SHA一致，没有用全局保存恢复配置；主构建入口已取消这些setter。实际已运行exe仍是该次隔离公司/产品名产物，BuildReport如实保留，不据后续工具修正伪造重建。

一次只读Review仅审查本轮主入口/消费者增量，不重复A冻结产品Review，确认两个收口问题：UTF1.3.9在PlayMode bootstrap内部无条件SaveAssets、原游戏观察只有累计断言。之前既有文件SHA均未变化，但不宣称此前没有内部全局保存调用。现按Unity2022.3 OnWillSaveAssets契约，仅在本次测试保护期间放行UTF新InitTestScene，其他脏资产延后，正常状态不拦保存；`save-fence-red-01`先因缺保护失败，再以实际脏Material落盘保护62/62复验。原游戏补逐轮查询断言后用`play-final-01`复验，未弱化累计或原流程断言。

`cleanup-01`逐文件SHA核验归档后，只删除本轮有receipt的4个GeneratedAcceptance目录；临时绑定源码随其删除并完成真实重新编译。保护快照核对原游戏、Packages/ProjectSettings、Foundation/Core/Resource、原轻量FSM及预存用户meta不变。主工作区原有§11/T2未提交回执保留为未暂存差异，本批只提交本节；用户`AlternateContent.meta`不纳入。A未提交的`.vscode/settings.json`和两份MainModule SpriteAtlas未携带、未清理；无Push、B/C合并、分支切换、新worktree或远端写入。
