# YL 代码规范与统一命名整理 Implementation Plan

> **状态：2026-10-23 T0–T8 技术收口，G0–G5 按本文分范围证据通过；用户已另行授权本批本地 Git 收口。** 唯一完整独立 Review 的问题已限定修复并复验；保留整组失败、分组边界和批处理退出限制，不宣称全库单次全绿。不 Push，用户与既有冻结制品保持。
>
> **For agentic workers:** 使用 `executing-plans` 或按不重叠模块使用 `subagent-driven-development`，按任务勾选验收。规划批准不等于立即实施，不包含创建分支、worktree、Commit、Push、发布或修改工具权限的授权。

**Goal:** 除已批准的名称迁移外，保持 YL 功能行为、有效数据与持久协议不变，统一自有 C# 的格式、名称和局部组织，使代码阅读顺畅、疏密适当、职责清晰，并让模板与后续新增代码保持一致。

**Architecture:** 沿用现有 Foundation / Core / Systems / Utilities、Runtime / Editor 及可选集成边界。采用一份短规范、路径受限的格式配置、语义改名和分模块验证；不重写系统、不新建格式化引擎、不把命名整理变成架构迁移。

**Tech Stack:** 当前 Unity `2022.3.62f3c1`、C# 9、Unity Test Framework `1.3.9`；HybridCLR `9.0.3`；既有 Analyzer 工具固定 .NET SDK `9.0.312` / Roslyn `3.8.0`。执行时读取迁移完成后的实际配置，不升级依赖。

**Spec:** 用户本轮要求、下列已确认决定及本计划 §2；遵循项目 `AGENTS.md` 与 `Assets/YLFramework/Documentation/Architecture.md`。本文件是该整理专项的唯一实施与验收计划；已完成的 HybridCLR Plan 是交接依据，只读引用其最终 §7–§10，不改写历史记录。

## Global Constraints

- 已确认：**全面统一**，允许类型、公开 API、参数和 Unity 序列化字段改名，同步 YL 自有调用及文档，保证既有资产数据不丢失；不为未发布接口增加无必要的旧名兼容壳。
- 已确认：允许**行为等价的局部整理**，包括展开压缩语句、少量私有步骤提取与必要的 partial 拆分；不得改变算法、执行顺序、线程、资源所有权、异常传播、取消或生命周期。
- 已确认：框架外调用方允许**最小引用及绑定迁移**。`Assets/Games/MainModule` 已有 AudioKit 消费者；只同步受改名影响的引用，不改业务逻辑，不顺手格式化游戏，不覆盖其已有修改。
- 不改依赖版本、程序集身份、asmdef/asmref 编译边界、namespace 分层、默认导出根、资源地址、存档/协议版本和签名算法；类型改名不意味着全树移动目录或重划 namespace。
- 不把字段改成属性、Action 改成 event、普通类改成 sealed 或接口改成另一种接口；这些属于额外 API 设计，而非本次名称或排版统一。
- 保留版权和许可证；已融合的 XF/QFramework 来源 YL 模块在范围内，不因来源被整体排除。真正随附的第三方库保持上游身份与风格。
- 保留 `.meta` / GUID、枚举数值、序列化有效值、初始化顺序、原生布局及持久化字符串；bool / bool? 的类型、真值含义、默认值及 null 语义不变。字段改名与类型迁移分别评估，不把 Attribute 当作自动完成所有迁移的保证。
- 不编辑生成的 `.sln` / `.csproj`、Library / PackageCache 或受管生成物；不修改 AGENTS、忽略规则、IDE/MCP/Kilo 权限配置，不关闭诊断来通过整理。
- 使用已打开的唯一 Unity；不得为本专项对同一工程启动第二个实例。无可用验证环境时记录阻塞，不把离线编译、Simulation 或历史日志冒充当前运行证据。

## Review Focus

- 名称冲突与外部约束：`completed` 事件与 `Completed(...)` 方法不能直接同名；最新 HybridCLR 的 `Progress(...)` 方法会遮蔽改名后的基类 Progress 属性，必须联动迁移。Unity 的 `keepWaiting`、消息、外部接口实现不得按普通 YL 名称改写。
- 非 C# 引用：`FindProperty`、反射、UnityEvent、Excel 列、JSON 键和生成模板可能仍使用旧名；必须检查真实数据，不以编译通过替代。
- 看似排版的行为变化：字段重排、using 生命周期、短路判断、bool 正反含义与 bool? 三态、Unity fake-null、闭包捕获及通知重入不能改变。
- 自动化静默失效：Analyzer 元数据名、精确源码清单和输出所有权校验必须同步，零诊断或跳过测试不等于门禁仍在工作。
- HybridCLR 基座失配：AOT 公开符号变更可能要求新基座；不得覆盖旧冻结基线或放宽兼容检查，以旧 Player 加新 DLL 宣称通过。

## 1. 启动条件与范围

### 1.1 已完成的迁移与当前交接基线

2026-10-22 用户已明确确认迁移完成。只读核对分支 `integration/xf-yl`，HEAD 为 `bfaeb94`：`23d0a56` 完成官方 9.0.3 集成与原生验收，`508a325` 补交来源包依赖及空目录 meta 清理，`bfaeb94` 补齐项目级热更与编辑器工作流。源码基线必须包含最后一项，不能退回最初迁移版本。

交接依据为 `.kilo/plans/1792591409867-xf-hybridclr-migration.md` §7–§10 和 `Assets/YLFramework/Integrations/HybridCLR/README.md`；本轮读取记录和源码，**没有重跑**其中的技术验证：

| 已有证据 | 整理计划中的使用方式 |
|---|---|
| §7/§8：T1–T6、G0–G4 已完成；Windows64 IL2CPP Development/Release Low、签名 HTTPS 同 Player 重启 v2、默认无包及可选接收 | 解除前置等待，作为既有合同与回归参照，不当作整理后通过证明 |
| §9/§10：最终 EditMode 121/121；Runtime 未再变化，沿用 HybridCLR PlayMode 25/25、CoroutineStarter 15/15 | 纳入项目子集/共享基座、进度、EditorFast、配置工具和窗口交互的回归；数量是交接事实，不是必须硬编码的目标 |
| §9：新 Development Player 实测 A/B 共享基座、依赖闭包、共享 DLL 单份及 async，使用可信本地快照 | 与早期 Release/HTTPS 证据分别记录，不声称新增 A/B 场景已在 Release/HTTPS 重验 |

当前仍有非本专项修改：生态集成 Plan、`.mcp.json`、MainModule 三个 FSM 资产；另有 `Assets/Test`、孤立 fixture meta、HybridCLR 本机生成 PlayMode Resources、`ProjectSettings/HybridCLRSettings.asset` 和本 Plan 未跟踪。不得归零、删除、暂存或接管这些内容；生成 Resources 仅在确认原入口所有权后按下述 Prepare 流程刷新。Git 工作区干净不是前提，修改归属和无重叠写入才是前提。

已知记录差异：`Integrations/HybridCLR/Dependencies.json` 仍写 `nativeValidated: false`，而最终 Plan/README 明确记录实际 native 成果。该单一字段不用于推翻已有分范围证据，也不在命名整理中盲改为 true；保留差异说明，以实际收据和版本范围判断可复用性。

旧冻结制品不是最新源码快照：`G:/Unity/H26Delivery` 不包含所有后续许可及项目级增量，不能直接作为本次新包；旧 `H26D4/H26R/H26S` Player 及 `Build/HybridCLR/Reception-20261021/{Baseline-v1d,Baseline-v1d-Release}`、`Reception-20261022/ProjectSubsets` 保留原样。正式验收重新导出当前源码并使用新的接收/Player/基线目录。

启动实施时仍需重新记录 HEAD、index、dirty、唯一 Editor、场景状态和实际编译结果，核对最终日志/收据可访问；不为仅更新本计划重复整个 native 验收。Formatter 本机可用性仍待 T0 验证，前置迁移完成不代表 T0/G0 全部通过。

### 1.2 纳入与排除

| 范围 | 处理方式 |
|---|---|
| `Assets/YLFramework/Runtime/{Foundation,Core,Systems,Utilities}`、`Runtime/YLFrameworkRoot.cs` | 自有/融合代码全面整理，排除下述原样第三方源码 |
| `Assets/YLFramework/Editor` | 自有工具、Inspector、Drawer、Help 和生成器同步整理 |
| `Assets/YLFramework/Integrations`、`Samples`、`Tests` | 自有 C#、手写样例及有效测试同步；可选依赖与条件编译不变 |
| `Tools/YLFramework.Analyzers` 及受改名影响的 YL 开发工具 | 整理自有 C#，维护符号查找和验证入口；不全库重排 PowerShell |
| `Tools/HybridCLR/*.cs.txt` 与安装/同步脚本 | 属于手写验收源码模板，不是 DLL 生成物；同步受改名影响的消费者和模板格式，并验证首次安装与后续同步均完整，不重写验收平台 |
| `Assets/Games/MainModule` 等框架外消费者 | 仅必要引用/绑定迁移；不作整体风格整理 |
| `Runtime/Utilities/ZipTool/ICSharpCode.SharpZipLib`、`Runtime/Utilities/Json` | 上游库例外，保留 SharpZipLib / JSONObject API、风格和来源；自有外围工具仍整理 |
| `Plugins`、第三方 DLL、Packages、Library、Temp、obj/bin | 不格式化、不改名，不把二进制或外部源码列入 YL 风格检查 |
| `Assets/YLFrameworkData`、Examples 中的生成模块、Resource/HybridCLR 生成物与冻结发布/基线 | 不手改、不通用格式化；修改源模板/生成器，仅通过原正式入口更新自有输出 |
| `Tests/Integrations/HybridCLR/PlayMode/Resources/YLHybridCLRTests` | 本机当前程序集生成的输入，不作为源夹具提交；测试前由 `HybridCLRTestFixtures.Prepare()` 更新，不沿用旧 MVID/hash 快照 |
| `.meta/.asset/.prefab/.unity/.asmdef`、JSON/XML/CSV/xlsx | 不作批量美化；只有已审定的引用或数据迁移可定向修改，asmdef 与协议身份不变 |

## 2. 目标规范

实施时将本节整理为 `Assets/YLFramework/Documentation/CodingStyle.md`，正文控制在一至两页，以规则和短示例为主。迁移记录留在本 Plan，不再建立重复命名文档、Glossary 或审计日志。

### 2.1 排版与阅读节奏

- UTF-8 无 BOM、LF、单个结尾换行、无行尾空白；沿用仓库 `.gitattributes`，不修改其规则。
- 四空格缩进、Allman 大括号、block namespace，适配 C# 9；不引入 file-scoped namespace、global using、primary constructor 或 collection expression。
- 控制流一律使用清楚的多行块，包括单条语句的 if/else、循环、using/lock，以及 try/catch/finally。一行一条语句、一条字段声明；不保留多项赋值或多成员声明挤在一行的写法。
- 空方法和真正简单的只读属性/单表达式转发可紧凑表达；多步 setter、带状态转换的 lambda 和异常处理必须展开。禁止为缩短行数制造嵌套三元或长链式副作用表达式。
- 120 列为软上限；长参数、条件和调用按逻辑分行、续行四空格，不为对齐制造大量填充空格。字符串、URL 和清楚的长标识符可例外，不改协议文本来凑行宽。
- 成员之间通常一个空行，同组字段连续排列；去除连续空行与无用途 region。按“入口 → 主流程 → 细节”组织相关方法，允许小型内部记录同文件，不强行一类型一文件或按访问级别机械排序。
- **不重排**字段初始化、静态初始化、序列化展示字段、枚举值或原生结构字段顺序。using 声明的资源释放点、短路求值及 callback 前后状态检查保持原位置。
- using 以 System 在前，再按命名空间字典序分组；保留有意义的别名、`global::` 和条件编译。`var`、target-typed new、表达式体仅在类型/含义清楚时使用，不作为全库替换指标。
- 注释解释约束、原因和所有权；新注释以中文为主，改名同步 XML/cref/param。删除已确认失效的注释代码，纠正触及范围内的误导描述，不添加复述代码的模板注释，不批量改上游版权或翻译现有注释。
- 不设强制方法行数或拆分数量。只有独立职责清晰、名称准确且不改变异常/重入边界时才提取私有方法；状态机与事务主流程宁可稍长，也不拆成难追踪的微方法。

### 2.2 命名规则与优先级

| 对象 | 规则与示例 |
|---|---|
| 自有类型、方法、属性、事件、枚举/枚举成员、const | PascalCase；接口 `IResourceSource`，类型参数 `TState/TKey/TValue` |
| 参数、局部变量 | camelCase；有意义的 `i/x/min/max/temp` 等短名允许，不建立缩写黑名单 |
| 实际 Unity 序列化字段 | camelCase，包含 SerializeField/SerializeReference 与可序列化公开字段；该规则优先于访问级别 |
| 私有非 Unity 序列化字段 | `_camelCase`，static/readonly 同规则，不用 m/s/k 前缀 |
| internal/protected/public 数据成员 | PascalCase；内部记录成员是跨类型数据接口，不机械变成 `record._value`；不为统一名称改字段形状或访问级别 |
| Json.NET 等持久化字段 | 不等同 Unity 字段；名称按访问级别，外部键通过明确映射保持，不改变存档或协议格式 |
| bool / bool? | 按 §2.2.1 表达判断或行为意图，再应用访问级别/序列化的大小写规则；不加 b/bool 类型前缀，不统一硬加 Is |
| 集合、数值 | `_entries`、`timeoutSeconds/maxBytes`；复数与单位清楚，不堆类型缩写 |
| 异步入口、钩子、通知 | 非阻塞公共入口用 Async；事件 `Completed/VolumeChanged`；OnXxx 可用于 Unity 消息与可覆写钩子，处理器可用 HandleXxx；不因改名改变原委托/事件语义 |
| 缩写 | `Id/Http/Json/Xml/Url/Api/Guid/Mvid`；保留 UI/IO、官方 HybridCLR/DOTween/XFAB 和外部契约名；自有 Fsm 类型族成组统一，不按字符串长度机械替换 |
| 文件 | 主类型名 `.cs`；partial 为 `Type.Concern.cs`；保留合理的契约集合/内部数据文件；测试允许 `Scenario_Expected` |

类型与方法名字要求准确、简洁、有领域含义。Manager/Helper/Utility/Data/Info 不是禁用词；Get/TryGet、Load/Close、Acquire/Dispose 等服从真实合同，不能为了动词对称改生命周期。Try 只表示可预期失败的返回方式，不承诺所有异常都被吞掉。

### 2.2.1 bool 命名、表达式与迁移

bool 名称应读成自然判断，区分“当前状态”和“希望采取的行为”。不使用 `bReady/boolReady/flag1` 等类型前缀或无语义名称，也不把所有 bool 机械变成 `IsXxx`。

| 语义 | 属性/数据成员或状态查询 | 私有运行时字段 | Unity 字段/参数/局部变量 |
|---|---|---|---|
| 当前状态 | `IsReady/IsDisposed` | `_isReady/_isDisposed` | `isReady/isDisposed` |
| 存在、持有 | `HasOwner/HasValue` | `_hasOwner/_hasValue` | `hasOwner/hasValue` |
| 能力、许可 | `CanEquip/CanClose` | `_canEquip/_canClose` | `canEquip/canClose` |
| 策略判断 | `ShouldRetry` | `_shouldRetry` | `shouldRetry` |
| 支持、需要 | `SupportsCaching/RequiresRestart` | `_supportsCaching/_requiresRestart` | `supportsCaching/requiresRestart` |
| 行为选项 | `AutoPlay/IncludeInactive/AllowOverwrite/ResetOnDisable` | `_autoPlay/_includeInactive/_allowOverwrite/_resetOnDisable` | `autoPlay/includeInactive/allowOverwrite/resetOnDisable` |

- 状态布尔按表使用 Is/Has/Can/Should 等准确谓词；行为选项用动词意图，不写 `IsCanEquip/IsShouldRetry/IsAutoPlay/IsUseCache`。是否序列化只决定字段形式，不决定该值是状态还是选项。
- `Contains/Matches/Exists/Supports` 等自然谓词和 `TryGetValue/TryAllocate` 等返回操作结果的方法不加 Is；不因为返回 bool 改写其原有合同。const 仍遵循 PascalCase。
- 优先正向、单层判断，避免人为制造 `isNotXxx` 和难读的双重否定；但 `IsDisabled/IsCancelled/IsInvalid` 可以是准确的领域状态，不能为“正向命名”将其改成相反含义。`disableCache → enableCache` 不是纯改名，本专项不作这种极性反转。
- 普通 bool 条件用 `if (isReady)` / `if (!isReady)`，不写无必要的 `== true/== false`；遵循 §2.1 的多行控制块。不要将 bool 条件包装为 `condition ? true : false`。
- bool? 保留现有“未设置/继承/未知”的第三态及具体合同；`== true`、`== false`、`!= true` 和 `??` 可能有意地区分 null，不做普通 bool 的简化，不擅自补默认值或改为 bool。
- 长条件可按职责分行或提取准确的判断方法；保留 `&&/||` 的短路和执行顺序，不为命名局部 bool 提前计算原本不会执行的分支，不重复调用有副作用的查询。Unity 对象的隐式存活判断不视作普通 bool 等价替换。
- 多个 bool 实参容易混淆时使用命名实参，如 `Play(clip, loop: true)`；参数改名同步命名实参和 XML，不改变参数顺序、默认值，不为风格新增 Options 类型或重设 API。
- 每个实际改名的 bool 在原批次映射中说明语义类别、原 true 含义、默认值及持久化引用；保持原值、缺省行为和绑定，旧 JSON 键/Unity 字段迁移沿用本计划的数据规则。仅符号改名不为每个字段新增测试。
- 对实际修改条件表达式的路径验证 true/false；涉及 bool? 时验证 null；序列化 bool 改名验证旧资产的显式 true 与 false，不能只测恰好等于新字段默认值的一种情况。复用现有测试，仅补真实缺口，不新增 bool 命名 Analyzer。

### 2.3 已核对的代表性改名

这些是明确的起点，不是全库文本替换规则；每批先检查同名冲突、接口实现和字符串消费者，再补充该批语义映射。

| 当前符号 | 目标 | 约束 |
|---|---|---|
| `EquipmentManager.Configs/Records/depth` 等私有字段 | `_configs/_records/_depth` | 内部 Record 数据成员仍 PascalCase；初始化顺序不变 |
| `YLFrameworkRoot.preparingPanelRoot`、`EquipmentManager.flushing` | `_isPreparingPanelRoot`、`_isFlushing` | 表示进行中的状态；不改 true 含义、初始值或重入保护顺序 |
| `AudioClipLease.mOwner/mValidateAcquisition` | `_owner/_validateAcquisition` | Dispose 的移交顺序、恰一次归还不变 |
| `IUnRegister` / `UnRegister` 词族 | `IUnregister` / `Unregister` 词族 | 作为拼写收口，保留注销幂等/异常/重入合同，不借机改成 IDisposable |
| `CustomAsyncOperation<T>.isDone/progress/error` | `IsDone/Progress/Error` | protected `isCompleted` → `IsCompleted`；Unity override `keepWaiting` 原样 |
| 同类 `completed` 事件、`Completed(...)` 方法 | `Completed` 事件、`Complete(...)` 方法 | 连同 `CompleteWithoutNotify` 一次解决名称冲突；保留原完成通知、过时状态与可见性 |
| `HybridCLRLoadRequest.Progress(...)`、`HybridCLRModuleRequest.Progress(...)` | `ReportProgress(...)` | 与基类 `progress → Progress` 同批改，避免属性被方法遮蔽；单调进度、观察者异常隔离和完成语义不变 |
| `HybridCLRLoadResult.Simulation` | `IsSimulation` | 仍由 Mode 派生，EditorSimulation/EditorFast 都为 true；RequiresRestart 已符合规范，不改为相反含义 |
| `HybridCLRBuildProfile.Recipe/Development/GeneratedAssetDirectory/BaselineDirectory/HotUpdateAssemblies/BaseProfile` | `recipe/development/generatedAssetDirectory/baselineDirectory/hotUpdateAssemblies/baseProfile` | 均为 Unity 序列化字段；保留旧值/引用、空数组全量及单层 BaseProfile 绑定合同，同步窗口 FindProperty、ProfileTools 和验收模板 |
| `View.onViewRemove`、`PanelManager.onCreateCanvas` | `ViewRemoved`、`CanvasCreated` | 仍为原 Action 字段，不转换为 event；同步所有触发/订阅/清理 |
| `IEquippable.IsCanEquip`、`IEquipment.Equiper/OnUnEquip` | `CanEquip`、`Equipper/OnUnequip` | 仅名称，装备所有权与流程不变 |
| `EquipmentConfigData.equipment_name/equipment_class_full_name/equipment_class_guid` | `equipmentName/equipmentClassFullName/equipmentClassGuid` | 迁移 Unity 字段、Inspector/Excel/反射引用；不改字段值 |
| `MissionConfigData.reset_param_on_completed` | `resetParamOnCompleted` | 行为选项不加 Is，保留默认 true 与旧资产显式 false；`FSMController.resetOnDisable` 已清楚，不强行加前缀 |
| `SaveItem.SaveID/_saveID`、`SaveIndex.CurrentSaveID` | `SaveId/_saveId`、`CurrentSaveId` | JSON 仍为 `SaveID/CurrentSaveID` 等原键；补齐显式映射，不升 schema |
| `CoreEvents.ON_REMOVE_VIEW` | `ViewRemoved` | const 值仍为 `"ON_REMOVE_VIEW"`，不重命名事件键 |
| `IPamaterCompare`、`FSMParamterFactory`、`CharacherController2DInspector` | `IParameterComparer`、`FsmParameterFactory`、`CharacterController2DInspector` | 拼写/命名修正，文件配对移动 meta；比较接口 `IsMeetCondition` → `Matches`，不改变判断 |

其他已序列化类型改名应有适用的类型迁移和资产验证；没有必要的 API 不为好听重造名称。历史旧名可以出现在迁移 Attribute、协议映射、上游代码和历史 Plan 中，不能把“旧字符串零命中”当验收标准。

## 3. 工具与执行方式

- 新建**仓库根** `.editorconfig`，仅以 `Assets/YLFramework` 自有 C# 和自有工具 C# 的路径节生效；不得用全局 `[*]` / `[*.cs]` 把游戏、Packages、vendor 纳入清理。不新增 formatter DLL 到 Assets，不改默认导出根。
- 复用 SDK 的 `dotnet format whitespace --folder`，以明确 `--include` 文件清单及 vendor/generated 排除运行，先 Check 再显式 Apply；不加载 Unity 生成工程，不运行 `dotnet format style/analyzers` 的全修复，不 restore、不新增格式化 NuGet 包。
- 实施一个薄入口 `Tools/CodeStyle/Format-YL.ps1`，参数固定为 `-Mode Check|Apply`（默认 Check）与可选 `-Paths <repo-relative paths>`。强制仓库内、自有源码白名单；Apply 必须有非空批次 Paths，拒绝游戏、vendor、生成物、链接越界和非 C# 输入。使用现有 SDK `9.0.312`；版本/权限不足就报告阻塞，不自行安装或修改权限。
- SDK 选择复用 `Tools/YLFramework.Analyzers/global.json`，从其目录启动 dotnet 子进程，格式目标仍显式指定仓库路径；不新增根级 global.json 改变其他工具环境，不以进程当前目录代替文件作用域校验。
- 薄入口只负责选择路径、调用官方工具、转发失败码与输出范围，不实现自己的 C# formatter。以 `Tools/CodeStyle/Test-Format-YL.ps1` 在临时隔离副本中验证默认只查、边界拒绝、合法样例可修复、二次 Check 零变化和范围外字节不变；副本保持相同目录布局，不给真实产品源码写入坏格式夹具，不引入 Pester 等新依赖。
- Check 只自动检查空白排版；`.editorconfig` 可额外给 IDE 提供基础大小写/接口前缀提示，但 whitespace 命令**不执行命名诊断**。Unity 字段分类、120 列软上限、职责命名和局部提取由语义改名与 Review 检查，本期不新增一套命名 Analyzer。
- whitespace formatter 也不会替代添控制流括号、拆多项声明等语法整理；这部分定向人工编辑并验证，不能宣称“格式检查通过即全部规范通过”。
- 目录模式不等于 Unity 完整编译上下文；定向检查 UNITY_EDITOR、ENABLE_IL2CPP 和可选宏的各分支排版，保留双 profile/真实可选程序集编译验证，不能以未启用分支未报错证明正确。
- 官方文档已确认目录模式和 `--verify-no-changes`：<https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format>。本规划环境 CLI 探测被权限拒绝，**尚未验证本机 formatter 实际运行**；执行 T0 时核验，不使用未获准的绕过路径。

## 4. 实施任务

任务顺序为 T0 → T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8。每任务可按领域拆小批；格式展开与改名/结构整理保持可区分的 diff，不在并发任务中同时修改共享基类、模板或符号表。每批改名涉及的跨模块调用、模板和反射映射必须同批闭环，不得拖到 T6 才修复；T6 是生成排版及完整生成链的统一收口。

### T0：迁移交接与整理基线

**Files:** 本 Plan；只读最终 HybridCLR Plan、工作区、相关资源与测试/工具配置。

- [x] 规划更新时已核对用户完成确认、§1.1 最终交接记录与 `23d0a56/bfaeb94` 源码提交，解除等待 HybridCLR 完成的前置条件；不代表本专项技术验收已执行。
- [x] 实施前复核 §1.1 的实际日志/收据及当前写入归属，记录源码与制品对应关系；不改写已完成的 HybridCLR 计划，不覆盖旧发布、Player 或冻结基线。
- [x] 建立本批自有文件清单、排除清单、现存调用方及候选改名表，bool 按 §2.2.1 区分状态/选项并记录原真值与默认值。记录资源 GUID/fileID、必要序列化值/绑定/JSON 字节基线；检查 FSM 的序列化 `_runtimeFSMControllerLayer` 与运行时同词字段等冲突。
- [x] 核对唯一 Editor、编译状态、测试基线与 SDK/formatter 能力；以至少包含 `bfaeb94` 的实际后续 HEAD 为准，不再使用迁移前 `b595082`，也不把历史测试数量当作当次结果。
- [x] 识别旧迁移守卫：`Tools/Behavior/Test-BehaviorBoundary.ps1` 固定历史提交，轻量 FSM 快照工具会拒绝 dirty。不得将它们的“历史文件未改”当成本专项门禁；改用仍有效的依赖/编译/行为断言，不重写历史验收结果或清零基线。

**Gate:** 前置迁移完成已确认；现场修改归属、完整范围、基线与工具仍须核验。本任务全部通过前不开始格式化。

### T1：短规范、路径配置与最小格式入口

**Files:** 新建 `Assets/YLFramework/Documentation/CodingStyle.md` 及 Unity 生成的 meta、仓库根 `.editorconfig`、`Tools/CodeStyle/{Format-YL,Test-Format-YL}.ps1`；文档 README 加入口。格式入口自身用隔离临时副本验证，不新增测试平台。

- [x] 将 §2 写成短规范，明确内部数据成员、Unity 字段和 JSON 键的区别，单列 bool 的状态/选项、自然谓词与三态规则；配置仅覆盖自有路径，vendor/生成文件不被“标成 generated”来隐藏诊断。
- [x] 按 §3 实现 Check/Apply 薄入口，先验证范围拒绝、Check 不改样例和 Apply 显式路径，再在可用 formatter 下验证幂等与范围外不变。
- [x] 只落地规则和入口，**不在此任务全树 Apply**。不修改已有 Analyzer 测试专用 `.editorconfig` 的 YL0002 夹具含义。

**Gate:** Check/Apply 边界和实际效果有证据，短规范与配置一致；未支持自动化的人工规则明确列出，工具受阻则不宣称自动门禁已交付。

### T2：可读性样板批次

**Files:** `Runtime/YLFrameworkRoot.cs`、`Runtime/Systems/Equipment/EquipmentManager.cs`；对应现有 Core/Equipment 测试；仅涉及的文档引用。

- [x] 先展开压缩控制流、try/catch/finally、多项字段/赋值；保留字段初始化顺序、Unity fake-null、原通知与异常路径，不同时提取大段逻辑。
- [x] 统一私有状态字段与必要局部名，bool 按 §2.2.1 命名；内部 Record 保持 PascalCase，状态成员采用准确的 Is/Has 等谓词。只有清楚独立的步骤才做私有方法提取，不新增共享 helper。
- [x] Check 零变化；Unity 实际编译与 CoreModule / Equipment 定向 EditMode、PlayMode 通过。复核重入、失败清理及宿主结束；纯格式部分没有非格式意图的修改。

**Gate:** 两种代表性代码达到“入口好找、阶段分明、无多语句挤行、名字自然”，且未改变初始化、callback 和回收合同；作为后续批次的可读性样板，不另加审批流程。

### T3：Foundation 与基础 Utilities

**Files:** `Runtime/Foundation/**`、自有 `Runtime/Utilities/**`，相关消费者/测试；涉及 marker 或类型元数据名时同步 `Tools/YLFramework.Analyzers/{Analyzer,Tests}`。

- [x] 分 Async、Events/Binding、Pool/Singleton/IOC、Timing/Update、FSM/Grid/Table/Extensions 等小批处理；按 §2.3 一次完成请求完成族、Unregister 族的继承/调用/模板消费者更新，保留外部 override 名称。
- [x] Async 改名同批更新 HybridCLR 请求的 `ReportProgress`、Loader/样例/测试及 `Tools/HybridCLR/*.cs.txt`，不能只更新 `.cs` 文件。Unity 编译完成后先显式 Prepare 本机夹具，再回归单调进度、完成重入、跨 Play 与 EditorFast，不能拖到 T6 才恢复可选集成编译。
- [x] 添上或沿用与改名风险对应的测试：请求恰一次终态与回调重入、注销重试、对象池借用代次、Timer 同帧取消/重启。只做格式的路径复用现有行为测试，不为排版制造虚假 RED。
- [x] `SerializedDictionary` 的 `datas/Key/Value` 已有明确布局断言，作为持久化布局例外保留，避免连带改变 JsonUtility 原始行输出；仅整理私有运行时状态和排版。保留原字段/GUID 断言、旧数据重导入、重复键/fake-null 与 Json.NET/Save 合同，不为统一大小写放松布局测试。
- [x] Foundation/Core Editor 与 Windows Development 源码闭包测试、该批行为测试通过；若符号表受影响，重建部署 Analyzer，并证明 Unity 的合法/非法调用见证仍分别成立，不能仅看零诊断。

**Gate:** 基础符号迁移完整、外部契约例外清楚，依赖方向和关键生命周期断言保持，vendor 原样。

### T4：Core 与通用 Editor

**Files:** `Runtime/Core/**`、`Editor/{Core,Foundation,Utilities,Help,Distribution}/**`；`Tests/Core/**`、相关 Help/导出测试。

- [x] 分 Module/MVC、Panel/UI、CoreEvents 与 Editor 小批整理；公开 Action 字段只改名，保持订阅、触发和归零语义；const 名称变更不改字符串值。
- [x] 同步私有反射名、XML/Help 名称、精确 sourceFiles/文件路径守卫及 AnalyzerSymbols/fixtures；文件随类型改名时成对保留 meta，不调整程序集或主资源加载键。
- [x] 运行 `CoreIdentityTests`、`CoreEditorIdentityTests`、`CoreArchitectureBoundaryTests`、Core Module/View/Panel 生命周期与既有 UnityEvent/输入定向回归。守卫只更新预期的新身份，不弱化唯一所有者、完整闭包或 GUID 断言。

**Gate:** Core 调用链、默认 UI、帮助定位和 Analyzer 真实工作，持久事件无 Missing Method/Script，Runtime 未引入 Editor/Systems 反向依赖。

### T5：Systems、行为编辑器与数据迁移

**Files:** `Runtime/Systems/**`、`Editor/Systems/**` 及自有可选 Excel 工具；相关测试与受影响资产；框架外仅最小消费者变更。

- [x] 分 Action/Audio、Resource、Save/Http、DataAssets/Localization、Skill/Buff/Mission/Equipment、Reddot/Logging、StateMachine/Overlap/CharacterController2D/MoveablePlatform 处理，保留每组来源声明。不一次性正则替换全树。
- [x] 优先按语义改名，随后重新排版；遇规范已清楚的名称不为“零旧词”另造同义词。标记程序集/namespace、外部 override、协议/native 字段等真实例外。
- [x] Unity 字段：旧资产先建立值/引用基线；适用时使用 FormerlySerializedAs，类型改名另评估 MovedFrom 与绑定；同步 `SerializedObject.FindProperty`、反射、Excel 字段映射和实际资源引用。用真实旧数据证明读取、保存、重导入及 Prefab override 保留，不能只测新建对象。
- [x] bool 定向检查：状态与行为选项不混名，不反转正负含义，不改变可选参数默认值；修改条件时覆盖 true/false 和实际存在的 bool? null，迁移 Unity 字段时覆盖旧资产显式 true/false，并验证新建实例默认值仍相同。测试仅覆盖触及的风险，不机械为所有 bool 增加用例。
- [x] JSON/Save/Resource：显式保留旧键和 enum 数值；对原存档 round-trip、Catalog canonical bytes/digest、签名输入和错误路径做回归，禁止为改名升 schema 或重签旧制品。Unity JsonUtility 直接使用字段名，不能靠 JsonProperty 保留其输出；如字段确属对外 JSON 协议则作为可核实的协议例外保留，而非让数据格式跟随代码改名。
- [x] 局部提取保留短路/延迟求值、闭包捕获、异常聚合及资源移交顺序；保留原同步完成、取消和通知重入测试。热路径不因美化引入 LINQ、额外分配、全场景搜索或重复计算。
- [x] 最小迁移 MainModule 等真实消费者的符号/绑定并验证全工程编译；不改其业务，不覆盖原场景/资产的未保存或既有修改。各领域执行现有定向 EditMode/PlayMode；仅测试缺口补迁移与边界用例。

**Gate:** 自有代码规范化，真实旧数据/绑定仍可用，存档/协议字节合同不变，框架外 diff 均能对应必要改名。

### T6：可选集成、模板与样例防回退

**Files:** `Integrations/{HybridCLR,DOTween,XFAB,Excel}/` 自有代码与测试；`Editor/Core/ModuleGeneration/CreateModuleCommand.cs`、`Editor/Systems/StateMachine/Template/FSMStateTemplate.txt`、`FSMStateCreator.cs`、`Editor/Systems/Resource/CodeGeneration/{ResourceCodeGenerator,ResourceReferenceOutput}.cs`；自有手写 Samples；`Tools/HybridCLR/*.cs.txt` 及必要安装/同步脚本、`Integrations/HybridCLR/README.md`。

- [x] 同步所有生成源模板，而非手改输出。Module 的 `global::`、StateMachine 模板占位符/Preserve、Resource 稳定排序/首行 hash/输出所有权保持；生成标识符遵循规范，但常量对应的真实资源地址值不变，生成 asmdef 字符串不为排版修改。
- [x] 在自有隔离目录生成 Module、Fsm State、Resource 常量并实际编译；确认新输出满足格式，原合法旧格式生成物可由原正式入口安全更新，自定义/损坏输出仍被拒绝，二次生成稳定。已有用户生成模块仅同步必要 API 引用，不能被模板整文件覆盖。
- [x] 整理 HybridCLR 已完成的源码与手写样例，保留 manifest canonical 格式及官方名字；同步样例 `EntryAssembly/ModuleType`、link/preserve/反射消费者，保留原 asmdef/版本宏/叶子边界。只整理 YL adapter，不改官方包或 DLL。
- [x] 以最新 `HybridCLRWindow.cs`、`HybridCLRProfileTools.cs`、BuildProfile/BuildPipeline/BuildBaseline、Loader/Requests/Metadata 为完整整理面。序列化字段改名同步 `FindProperty`、Undo 和旧 Profile；保留空入口数组的全量含义、显式单层共享基座绑定及父子各自 ProjectId/生成目录，不能因改名退回最初单项目设计。
- [x] 保留 Native/EditorSimulation/EditorFast 区分、代码读取 0–0.8 / Core 启动 0.8–1 的单调进度、EditorFast 零代码资源/零 native/空 CodePackageId、活动 Module 阻止 Dispose（即使 ResourceSystem 已关闭）。保留 PE/MVID 范围检查和 neutral culture 精确处理，不替换成 IL2CPP 未支持的 ManifestModule 反射。
- [x] 运行 `CoreModuleGenerationTests`、`BehaviorAuthoringTests`、`ResourceCodeGenerationTests`、`ResourceCodeCompilationTests`、`ResourceOutputSafetyTests`；HybridCLR 完整 EditMode/PlayMode 与 CoroutineStarter 回归按实际发现集执行，不锁定历史数量。重点包括 `PendingModuleReportsMonotonicCodeAndCoreSceneProgressBeforeCompletion`、`EditorFastKeepsModuleOwnershipAfterTheBorrowedResourceSystemShutsDown`、`SharedBaseBindingRetainsOwnerIdentityWhileChildKeepsItsOwnProject` 及非法绑定/metadata 未登记负例。
- [x] 保留窗口真实 MouseDown/MouseUp 四页往返与选中态、宽窄布局、打开无隐式写入，以及 Profile 新建/Undo/目录别名重复选择/删除目录历史 GUID 拒绝的工具回归。这里是保护已完成 UI，不另作视觉重设计；首次 Install 与 Sync 都须复制 Runner/Probe 等当前模板并实际编译，不能借旧导入 receipt 声称新接收已通过。

**Gate:** 新生成代码不回到旧格式或旧 API；默认无可选包闭包仍通过；依赖已安装的可选集成实际编译/测试，未装集成不伪称运行通过。

### T7：全局一致性与独立交付验收

**Files:** 各批结果、现有 `Tests/Tools` 及 `Tools/HybridCLR` 交付入口、实际受影响的模块文档/Help 示例、本 Plan；不新建验收平台。

- [x] 全部自有 C# 运行 Check，第二次无变化。定向搜索旧名称仅剩合法例外、数据映射、上游和历史记录；复查文档示例、模板、反射和框架外最小调用迁移。核对游戏/vendor/包/生成物/制品未被美化。
- [x] 运行完整 Runtime/Editor 的 Editor 与 Windows Development 源码闭包、FrameworkAssemblyIndependence、默认 ExportUnityPackage 和实际改动的可选导出测试；实际解包核对 GUID、唯一 Analyzer、许可与排除项。
- [x] 在独立接收工程导入本轮默认包，不借源码仓库 DLL/Tests/可选依赖补编译；实际运行最小 Module/UI/Audio/Save/资源流程。主工程实际 Unity 编译与已影响的 EditMode/PlayMode 集合通过，无新增未说明失败/跳过。
- [x] 若改动 AOT/热更可见类型或成员、反射绑定或裁剪输入，沿用已完成 HybridCLR 的交付入口，使用**新兼容身份、新目录、新基座与新 payload**完成 Windows64 IL2CPP Development Low 的签名 HTTPS v1 → 重启 v2，以及 Release Low 的真实加载/Module/async/Prefab 代表性运行；Development 同一 Player 的 executable hash 在两版间不变。不覆盖旧基座/基线，不把本次整体框架改名伪装成给旧基座打补丁；原坏 hash/不匹配基座/Activate Busy 等关键合同继续受测。
- [x] 同时覆盖最新共享基座链：A 冻结全局登记，B 显式绑定同基座只发布项目闭包，A/B 共享 DLL 同字节且只加载一份；B 发布不执行 Strip/Player 重建，基线/Player 指纹保持。复用 `HybridCLRProjectSubsetProbe.cs.txt` 和现有 Build/Patch/Player 工具，用新目录执行，不覆盖旧 ProjectSubsets 基线。Development 的签名 HTTPS 更新、Release Low 的真实启动、Development 的本地 A/B 分别报告，不为表面全矩阵重复既有不相关验证；v1→v2 不要求在 Release 引入开发证书开关。
- [x] 若确实只有纯排版且未影响相关符号/构建输入，可按证据说明复用哪些 native 结果；不能将源码/公开 API 已变化仍归类为纯排版。平台范围保持 Windows64，不扩大移动端验证。

**Gate:** 风格、编译、数据、行为、默认/可选交付和受影响的 native 路径分别有证据；缺一项就记录具体阻塞，不用“测试全绿”合并描述不同证据层级。

### T8：独立 Review 与交接

**Files:** `Documentation/{CodingStyle,README,Architecture}.md`、确实受改名影响的 `Documentation/Modules` / Help 页面、本 Plan。

- [x] 对完整 diff 做一次独立 Review，重点检查阅读体验、命名准确性、重复/过度提取、初始化顺序、数据与字符串迁移、Analyzer 真实见证及新基座边界；发现问题在原批次职责内修正并定向复验，不另设“美学审查”循环。
- [x] 规范文档只保留长期规则；旧新 API 映射、例外和验证记录集中在本 Plan，模块用法更新为真实可编译的新 API，不给每个小批新建报告。
- [x] 执行 `git diff --check`，核对实际新增/改名文件及 meta、未授权范围、已有修改的保护情况。未经单独授权不 Commit/Push；用户授权提交后才按已验证小批次提交，不把本计划当作授权。
- [x] 交付整理范围、关键新名/数据迁移、例外、实际通过的验证和阻塞，明确与旧基座的兼容界限；本目标完成后停止，不扩展下一轮架构优化。

## 5. 可复用验证入口

以下命令是**实施阶段**的入口，不代表本轮已运行。执行前确认实际路径、权限及工具版本，终端拒绝则报告，不通过编辑权限配置绕过。

```powershell
# 自有批次的格式只查；最终不传 Paths 只检查全部白名单。
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/CodeStyle/Format-YL.ps1" -Mode Check

# 格式薄入口自身的边界与幂等验证，只使用自有临时副本。
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/CodeStyle/Test-Format-YL.ps1"

# 只有符号表/Analyzer 源码变化才重建、验证并部署到原唯一路径，保留 meta。
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/YLFramework.Analyzers/build.ps1" -Deploy

# 离线编译只是补充；不是 Unity 编译/运行或 IL2CPP 的替代。
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/Verify-UIPanel.ps1" -Mode Runtime
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/Verify-UIPanel.ps1" -Mode EditorTools

git diff --check
```

Unity 用现有 Editor Test Runner / `tests-run` 在唯一 Editor 内按实际类筛选；有脏场景先等待用户处理，不为测试自动保存。只有确认工程没有正在运行的 Editor 时才可采用既有 batchmode 入口，不并开实例。

HybridCLR 首次检出或相关程序集重新编译后，先在**非 Play 的 Editor 主线程**显式调用 `YLFramework.Tests.Integrations.HybridCLR.HybridCLRTestFixtures.Prepare()`，等待导入稳定，再运行 loader/Module PlayMode；该入口从当前程序集生成 bytes、MVID、hash 与 manifest，不能复用迁移前或上一批的生成快照，也不能提交它们充当源夹具。此前须确认生成目录仍为入口自有范围，无用户手写冲突。

原生交付复用 `Tools/HybridCLR/{Invoke-HybridCLRUnity,Install-HybridCLRReceptionHarness,Sync-HybridCLRReceptionHarness,Run-HybridCLRPlayer,Audit-HybridCLRNative,Audit-HybridCLRFinalImport,Freeze-HybridCLRDelivery}.ps1`，参数与收据格式以已提交入口为准。源码模板包含 ReceptionBootstrap、ReceptionRunner、ProjectSubsetProbe、FixtureHost；改名时同步而不是拷贝旧冻结工具继续执行。收据和日志在本专项新目录中保存；历史 `G:/Unity/H26Delivery` 只作历史参照。

## 6. 验收汇总与记录

| 门禁 | 完成标准 | 当前结论与范围 |
|---|---|---|
| G0 启动 | HybridCLR 前置完成已确认；实施时复核最新 HEAD、修改归属、现场基线与工具后通过 | 通过；integration/xf-yl/bfaeb94、index空、六保护blob一致；原场景/options/backend与Audio原状态恢复 |
| G1 规范/格式 | 短规范包含独立 bool 规则；受限配置与 Check/Apply 可用，全白名单 Check 幂等，谓词/选项分类等人工规则经过 Review | 通过；1110 Check幂等、9组工具自测、编码/EOF零问题；独立Review和338块受守卫缩进补漏，复查0候选 |
| G2 编译/身份 | Unity 实际编译、Editor/Development 闭包、依赖方向、GUID、Analyzer 正负见证通过 | 通过；当前Unity编译/闭包及导出17项、89脚本meta配对；Analyzer原固定版本129项证据沿用其未变源码/产物范围 |
| G3 数据/行为 | 旧资产重导入不丢值/引用，旧存档/协议键不漂移；bool 极性/默认值及 bool? 三态不变，重入、取消、清理与通知合同不变 | 分组通过；Review旧Variant/xlsx RED→GREEN，完整相关102；HybridCLR 123+独立1+1/Edit与25 Play；广域失败/调度及VS冲突保留原始结果，不称单次全库全绿 |
| G4 生成/交付 | 模板新输出规范且可编译；默认/可选包实际导出与独立接收通过 | 通过；正式生成/编译证据、初始product-only接收及当前包重新导入；默认5业务实际通过，批处理强清理/observer退出限制分别记录 |
| G5 native | 受影响的 HybridCLR/IL2CPP 路径用新基座验证，覆盖重启换版与 A/B 共享基座；分开报告 Dev HTTPS、Release Low、本地 A/B，不损坏旧冻结基线 | 通过；YS28独有新Dev/Release/A-B基座与真实正负例；当前包正式无native阶段复用通过、Player/baseline不变，不提升未受测平台/动态用法声明 |

**例外仅限：** 外部契约/官方专名、真正上游源码、持久化字符串与必要迁移旧名；每项说明原因与范围，不用例外绕过自有代码整理。

**规划更新记录（2026-10-22）：** 用户确认 HybridCLR 完成后，只读核对当前 HEAD `bfaeb94`、工作区、迁移最终 §7–§10、README、最新 Profile/Requests/测试与夹具生成入口；已解除前置等待，补齐进度属性冲突、项目子集/共享基座/EditorFast/窗口回归、动态夹具刷新及制品版本差异。既有 bool 与命名规则保留。本轮只修改本 Plan，没有修改产品代码、依赖、测试或迁移文档，也没有运行 Unity、formatter、构建或 Player。

## 7. 实施记录

### T0（2026-10-22）

- HEAD=`bfaeb94`，分支 `integration/xf-yl`，index 为空。§1.1 的既有 dirty/未跟踪清单保持；未启动分支/worktree、未提交。唯一作者 Unity PID=2944，另外两个 Unity.exe 为 AssetImportWorker，不创建第二实例。
- 实际 Editor：2022.3.62f3c1，非 Play/编译/导入、`scriptCompilationFailed=false`；场景 `Tests/Core/Acceptance/UIAcceptance.unity` dirty=false；StandaloneWindows64、Mono2x、非 Development，EnterPlayModeOptions 未启用。尚未为整理运行测试。
- 实际命令：Analyzer 目录 `dotnet --version`=9.0.312，`dotnet format whitespace --help` 支持 folder/include/no-restore/verify-no-changes。终端宿主为 Windows PowerShell 5.1，后续脚本显式使用已安装 pwsh 7.6.7，不依赖宿主语法。
- 已读取历史 `H26-20261021/native-final-audit.json` 与 `native-project-subsets.json`，passed=true，后者 Native A/B/单份 Support/正常停止；只是迁移交接证据，未重跑。
- 初始受管自有 C# 分组（排除 Plugins/JSONObject/SharpZipLib）：Runtime 471、Editor 215、Integrations 35、Samples 4、Tests 366，Analyzer 工具 Analyzer 7 / Tests 10。实际各批由路径入口重新枚举，不锁定文件数。样板首先仅 Root/EquipmentManager，不涉及资产字段改名；Root GUID=`78a320b7b5e042d38d8b9c5e4c08db68`，EquipmentManager GUID=`2e5fea2cfa9cced4bac442a6b7eca258`。
- 非本专项保护 Git blob：生态 Plan `b1fde5587fe9e72c309f3f4b73e551a517a6a5a1`；.mcp.json `888901e951f2f431b17e06e2ead45d7166dd42ab`；EnemyShield `8f7260761f8c5c384a193d4aebcbbbfe8d7a163a`；EnemyState `e7b78abada308a11146efc229d00b0689e584932`；GameStateManager `4e41479738d229afb02556538cd6c1efff26314e`；HybridCLRSettings `dfa01389c9f47c9e8fd0c78d7c58bf851ec02bca`。只记录指纹，不记录配置凭据。
- Pre-flight：T3 Async 与 T6 HybridCLR 共享请求 API，必须同批处理 ReportProgress/Completed 冲突和模板；T5 Profile/序列化与 T6 Inspector/验收模板共享字段，调用与字符串同批闭环；T7 只能用新基座验证变更后的 AOT。FSM 同词序列化/运行态字段已确认，需要按职责区分，不能机械交换下划线。
- Ruling：沿用当前授权工作区与本 Plan 记录进度，不执行技能默认的额外 worktree/ledger/自动提交；纯排版复用现有行为测试，不造假 RED，新工具和缺陷回归按先红后绿。代价是所有未提交差异必须由范围清单与实际 diff 追踪。
- 已复核旧 Behavior/LightweightFSM 历史快照脚本；不将其固定旧提交的“零 diff”要求当本专项门禁，不改写历史基线。G0 通过；G1–G5 未验收。

### T1（2026-10-22）

- 已添加根 `.editorconfig`、`CodingStyle.md` 与 README 入口、`Tools/CodeStyle/{Format-YL,Test-Format-YL}.ps1`。文档由 Unity 显式导入生成 meta，GUID=`af0ff0e561630f34f8e69387e0233953`；实际 compilationFailed=false。
- 新工具先运行测试，RED 为所需 Format-YL 入口尚未实现。最终隔离测试 8 组通过：默认 Check 真正返回 WHITESPACE 且字节不变、Apply 必须显式路径、vendor/game/generated/meta 拒绝、越界/绝对/通配拒绝、混合列表先全部验证再写、空格/方括号路径与 LF/无 BOM/幂等、目录自动排除、junction 拒绝且目标不变。测试只创建自有临时副本，清理其本身，不格式化仓库产品源码。
- 实际多路径 RED 揭示 pwsh -File 命名数组只绑定首值；加入隐藏的剩余参数接收并统一验证，不改变公开 Mode/Paths 用法。实际 formatter RED 揭示 SDK 9.0.312 的 folder 模式拒绝 --no-restore；Ruling：只用 --folder（本身不加载/还原项目），保留“不得 restore”目标，不传无效组合。代价是后续 SDK 必须仍固定并由同一真实测试守卫。
- 配置只作用于自有源码并显式 unset 上游/生成目录规则，不关闭原 Analyzer；Check 只管 whitespace，bool/Unity 字段分类和控制流补括号仍由语义整理与 Review 负责。未新增依赖、未改权限/IDE配置/忽略规则。
- T1 验证最终 `Test-Format-YL.ps1` 8/8；尚未宣称 G1 的全库 Check 与人工 Review 完成。T2 改动前 Equipment EditMode 基线 39/39、无失败/跳过（包含作者工具/双 Play 门禁），源码尚未修改。

### T2（2026-10-22）

- 仅修改 `YLFrameworkRoot.cs` 与 `EquipmentManager.cs`：展开控制流/异常处理/回收 lambda，拆开多字段声明、分行复合判断，统一私有字段与局部谓词；未改公开 API、序列化字段、常量字符串或执行顺序，未新增 helper/封装。两个原 meta 无 diff，Root 原 BOM 按规范移除。
- 私有映射：`preparingPanelRoot/preparedPanelRootSession` → `_isPreparingPanelRoot/_preparedPanelRootSession`；Equipment 的 Configs/Records/Pools → `_configs/_records/_pools`，Closing/Returned/Pending/Retired → `_closingHosts/_returnedEquipment/_pendingReturns/_retiredConfigs`，ReturnedMarker/depth → `_returnedMarker/_depth`。
- bool 映射：私有 flushing/stopping/quitting → `_isFlushing/_isStopping/_isQuitting`，aggregateReturns → `_aggregateReturns`（行为选项）；Config.Closing → IsClosing；Record 的 Owned/Loaded/Attached/Notified/Equipping/Unequipping/CancelEquip/ReleaseRequested/Returning → 对应 Is 谓词，Discard → ShouldDiscard；局部 external/permitted/notified/discard → isExternal/canEquip/wasNotified/shouldDiscard。均非序列化运行态，默认 false；IsOwned 仍仅在本域创建 Record 时显式 true，其余 true 的设置阶段及 finally 清理顺序不变，未反转谓词。私有 Alive/Current/DefinitionClosing → IsAlive/IsCurrent/IsDefinitionClosing。
- 改前与改后 Unity 实际 Equipment EditMode 均 39/39（改后 9.017 秒），CoreModule/EquipmentResource/CoreDefaultResource PlayMode 均 44/44（改后 5.670 秒），无失败/跳过；保持真实配置、重入、失败清理、双 Play 和资源租约回归。最后仅补行分组/续行空白，Check 再次零变化，Editor 实际 compilationFailed=false、非 Play。
- 已逐项复查 word diff：短路顺序、Unity fake-null、获取/释放、通知前后标志和错误聚合不变。显式两文件 Apply/Check 成功，`git diff --check` 通过；公开 Equiper/OnUnEquip/IsCanEquip 等仍等待 T5 联动改名，没有提前打断消费者。
- EquipmentAuthoringTests 的既有 TearDown 留下本次首次生成的空 `Assets/YLFrameworkData/Equipment` 与父目录；确认启动前不存在、内容已空后，只用 AssetDatabase 删除这两个自有空目录及 meta，没有清理任何预先存在或非空内容。其他既有修改继续保护。

### T3 / Async 小批（2026-10-22，T3 仍在进行）

- 使用当前 Unity CompilationPipeline 的真实引用、宏和源文件建立临时 Roslyn AdhocWorkspace（32 程序集、1195 源文件），调用已安装 SDK 4.14 Renamer，而非字符串替换。预览前后辅助编译均零错误；变更 25 个 YL 文件，所有原文 SHA256 校验通过才批量应用。未改 Unity/XF/XFAB 同名成员，也未改 Games 的请求消费者；MainModule 的旧请求实际属于 XF，不属于本批。
- 语义变更：CustomAsyncOperation 的 isDone/progress/error/isCompleted/completed → IsDone/Progress/Error/IsCompleted/Completed，Completed/CompletedWithoutNotify → Complete/CompleteWithoutNotify，CHECKING_ERROR → CheckingError；HybridCLR 两个 Progress(float) → ReportProgress(float)。LoadPanelRequest 的三参数 Complete 保持，基类调用显式 base.Complete。保持 legacy 事件先行、多播中断、重复完成更新 Error、Abort hook 和静默完成合同，不加旧名兼容壳。
- 手工整理 Async 三个支撑文件与 HybridCLRRequests 的控制块、空白、无用 region/失效注释。CoroutineStarter._released → _isReleased、局部 created → wasCreated；CoroutineObservation._ended → _isEnded，均默认 false、真值阶段与停止/捕获顺序不变；HybridCLR _continuations 保留原多播/异常隔离顺序。保留 Unity keepWaiting、OnCompleted/OnAbort 及原过时事件标记。
- Architecture/UI/HybridCLR README 同步 API 名称；只读核对 Tools/HybridCLR/*.cs.txt，没有这些旧成员引用，未制造空改动；其 error JSON 键和 Task.IsCompleted 保留。
- Ruling：项目权限拒绝创建任何 .csproj，未创建或修改该类文件。临时调用器改用已允许的 SDK csc.dll 直接编译，不 restore、不加包/永久工具、不修改权限；临时产物位于预批准 temp 的 `yl-style-semantic-20261022`。SDK 自带 Composition 版本产生 CS1702 警告，保留诊断；实际符号隔离自测通过。Context7 配额不足，查验了 Microsoft Learn 的 RenameSymbolAsync 官方签名。
- 改前/改后 CustomAsyncOperation/HttpRequest/HybridCLRSession EditMode 均 23/23；随后显式 Prepare 生成当前 MVID/hash 输入。较大 PlayMode 合并请求 MCP 超时，但实际 Test Runner 已完成 309 项：274 通过、35 SetUp 失败（CoroutineStarterTests 15、HttpRequestPlayModeTests 20），不是测试仍在等待。
- 根因证据：CoreResourcePlayModeTests.TearDown（161–175 行）明确保留共享维护宿主并断言其不被销毁；后续两个 fixture SetUp 则要求无现存 Foundation host。Ruling：这两个所有权 fixture 使用独立 Play 会话，与保留共享服务的 Core/HybridCLR 组分开；不销毁不属于本夹具的宿主、不删除测试、不放宽断言，也不修改产品行为来迎合错误的组合方式。
- 独立复验 CoroutineStarter 15/15、HTTP 20/20 全通过。随后仅 CoroutineStarter/Observation bool 与排版变化后再验 CoroutineStarter 15/15、实际 fixture CoroutineObservationPlayModeTests 6/6 全通过；一次含错误候选 fixture 名 CoroutineObservationTests 的查询只执行了 Starter 15 项，已纠正并单独执行 Observation 6 项，未把未发现的测试算通过。
- Async 三文件 Apply 成功；T3 剩余 Events/Binding/Pool/Singleton/IOC/Timing/Utilities、闭包与后续完整验收尚未完成。后续长测试按实际 fixture 会话边界拆分，MCP 超时只读取结果不盲目重复启动。

### T3 / Events、Foundation 与 Utilities 收口（2026-10-22）

- Unregister 接口/实现/重载/扩展/Update 与消费方按语义整族迁移，86 个文件；改前/后 Events/Binding 定向 EditMode 112/112。IntIDEventSystem → IntIdEventSystem；原场景触发器命名与实际指定/任意场景职责不符，统一为 SceneUnregisterTrigger，其他触发器用 Unregister 词形。9 个脚本 AssetDatabase 移动后 GUID 逐项保持，未检出其 GUID 的既有 Scene/Prefab/asset 文本引用。
- Foundation 私有字段与 bool 状态按逐类型映射统一（包括池/单例/IOC metadata/Timer/SerializedDictionary 的私有索引）；保留 datas/Key/Value 布局、Unity 作者字段、外部 override 与命名空间。CustomPropertyGUI.ReloadTreeViewOnValueChange 同步领域 Inspector、特性编译字符串和文档；HostReleased → IsHostReleased 同步 Action/Audio 宿主。
- 非源码的帮助示例、文档和 Stage9DeliveryProbe 模板在确认旧 token 所属后定向批量更新，保持非 ASCII 原字节；Action.md 的原有乱码并非本批引入，未擅自重写全文。临时工具保存该批 before/after 与指纹，不构建永久代码修复引擎。
- Ruling：复用 SDK 官方 IDE0011 Analyzer/CodeFix 补括号，仍不调用广泛 style/analyzers 全修复。每文档先后辅助编译零错误，除大括号外 token、注释/预处理/禁用文本逐项相同才允许应用。Foundation 共 429 处/51文件、Utilities 73处/19文件；后续按原 whitespace 入口收口。代价是该检查不证明所有宏分支语义，双 profile/Unity 边界测试仍保留。
- Fsm<TStateId>、CurrentStateId/PreviousStateId、AbstractState.StateMachine 成组迁移，**不改 YLFramework.FSM namespace/目录/程序集**；HtmlColorExtensions 精确说明 HTML 能力，不合并两个类型或删功能；Ray MinimumDistance/GetClosestPoints 保留无限直线、有符号参数合同。ColorSelector 私有运行态加前缀和准确 bool，serialized 光标字段不变；TableIndex 泛型键改为 TKey，StringTools const 用单位名但格式输出字符串不变。
- Fsm/HtmlColorExtensions 两脚本保留 GUID=`92df2eb0cc899794a99dd4a26820c154` / `7479d2036c8040de9791e515d225ecfd`，导出 sentinel、样例/反射消费者/文档同步。Windows case-only 路径通过中间名移动；实际磁盘新名已正确，Git 默认 ignorecase 的状态可能仍显示旧拼写，T8 用只读 case-sensitive status 核对，不自动暂存。
- 验证：主要 Foundation/Binding/Pool/Serialization/Timer EditMode 292 项有291通过；TimerSession 的干净场景 SetUp 被其他夹具产生的临时场景 dirty 触发，Runner 恢复原干净 UIAcceptance 后独立重跑1/1通过，未清除/保存用户脏场景。Attributes 双profile3/3、FoundationBoundary9/9通过。其后 Fsm/扩展/池/身份/帮助相关161/161，Foundation/Core双profile闭包+CoreLoadViewArgs+实际默认导出29/29，AssetFSMRuntime13/13通过。
- PlayMode 独立会话：EventsLifecycle14/14、UpdateManager6/6、TimerDriver11/11、GameObjectPool13/13、SingletonHost6/6、TimerObject6/6、TimerBinding3/3、TimerWait2/2、TimerScope3/3、Timing1/1。TimerObject 初验5项失败的根因为漏同步测试的私有反射 current/timer；改成 _current/_timer 后6/6，不改变断言与产品行为。串行多次 tests-run 的连接可能因每次退出Play时重连而中断；已逐次读取结果补齐，今后每次只运行一个所有权会话。
- Foundation124 + 自有Utilities51，共175文件 Check幂等通过；实际 Unity compilationFailed=false，git diff --check通过；Plugins、JSONObject、SharpZipLib无diff。T3不涉及Analyzer selector，T4的 _pool/_args 再同步重建见证。TimerObject等历史大写Unity作者字段和其真实资产迁移统一留到T5数据批，不能当永久“风格例外”。

### T4（2026-10-22）

- 初始253个精确符号映射经只读复核修正行为选项/过去快照命名（fade/wasAttached/previousIgnore 等不硬加 Is），Renamer 预览32程序集前后零编译错误，64文件原文指纹确认后应用；没有修改 Games 的 XF Module。CoreEvents.ViewRemoved 保持 const 值 `ON_REMOVE_VIEW`，补真实值断言在旧/新名称下都通过。
- 主要 API：View.ViewRemoved、PanelManager.CanvasCreated 保持原 Action 字段形状；Module.IsBaseModule、LoadViewRequest.View、LoadPanelRequest.Panel 为原属性改名；View.loaded → IsSessionLoaded 避开已有 IsLoaded；Module 内部 Models/Views/TypeViews/OwnedViews/PendingViews 等统一 PascalCase。snake_case 参数按语义迁移并同步 XML/命名实参，事件 int 键参数使用 eventId，值与路由不变。
- Core 的 _pool/_args 是实际 Analyzer selector：先只更新合成fixture及精确owner形状测试得到3项预期 RED（126/129通过），再同步 AnalyzerSymbols 后129/129 GREEN；后续整理私有缓存字段、IsCandidate/IsComplete、官方IDE0011 122处/7文件，并用实际 Roslyn3.8 构建0警告/0错误、129/129再次通过。最终唯一DLL部署SHA256=`219832878E80F4C312DBF58CBEC44DBF9928861B2EB398A2251C7619F2751FC1`，原meta未改；没有添加命名Analyzer/关闭规则。
- 18个已逐字核对的反射/帮助/文档消费者同步，CoreTemporaryCollectionTests 的 ListPool._pool 也补齐。Module 模板生成 IsBaseModule 并更新冲突名守卫；用户传入 `{BaseModule}` 等模板样式字符串仍是原样数据，未做递归替换或更改生成asmdef身份。
- HelpWindow 的77个非序列化运行态/样式字段与3个Help反射测试同步；其11个Unity持久字段留T5显式迁移，不永久保留m前缀。其余通用Editor、字段逐条声明及有用的注释同步，未移Core/Editor文件、拆partial、重排初始化或更改asmdef/namespace。
- 官方IDE0011针对Core/中立Editor预览1214处/64文件，仅新增大括号、其他token与注释/宏逐项相同且辅助编译前后零错误后应用；正常格式入口收口86文件。不因代码行数增长拆散状态机或改变事务/短路/清理次序。
- bool收口进一步按真实职责验证：私有 Current/Valid/Bound 等变为 IsCurrent/IsValid/IsBound，PositiveFinite/Finite 用 Is 谓词；Reject/CommitDisplay/CopyRect 等有副作用的bool操作不改成判断。Ruling：MultiPanelListener 的实际作用是同根完整覆盖停绘，名称会误导，改为 CullCoveredPanels / _cullCoveredPanels，默认true、原排序/Modal行为不变，不凭旧名猜“多监听”。文档及8处真实测试调用同步，未发现模板/外部调用。
- 验证：实际Core/Help/Editor EditMode127/127（含Core身份/Editor-Development闭包/Module生成/真实Analyzer正负见证）；最终DLL重部署后CoreEditorIdentity+实际默认导出18/18。PlayMode Module28/28、DirectView135/135、Failure13/13、Resource48/48、DefaultResource7/7、SceneController25/25；最终predicate/culling改名后DirectView135/135再次通过。Resource单组超过MCP等待上限，实际Editor仍在Play，计划唤醒后读得48项均Passed，未重复启动。
- 最终Core/中立Editor+ColorSelector+Analyzer源码/测试104文件Check幂等通过，git diff --check通过；只移除一段失效的ManagedReference注释代码及其行尾空格，没有开启新支持。UIAcceptance场景仍dirty=false，六项非本专项指纹逐一未变；默认测试未留下Examples/生成资源目录，未Commit/Push。历史 native 证据未作为本次通过，新基座仍待T7。

### T5 / PanelConfig 旧数据迁移（2026-10-22，T5仍在进行）

- 先在既有CoreDefaultResourceEditorTests中添加真实旧YAML副本测试：ColorSelector预制体显式true/false/0/非零、局部fileID引用，及原UITips/Overlay关系与旧settings.Layer覆盖。夹具从当前源码资源构造明确旧键后导入，源码资源不作为可写夹具；初验修正了ColorSelector实际位于子对象且字段名outerCursor的测试假设后，3项均以“新字段缺失”正确RED。
- PanelConfig七个Unity字段统一camelCase并添加精确FormerlySerializedAs旧名，语义改名同步11个CSharp消费者；PanelOverrides的七个nullable非序列化公开字段仍PascalCase，不混淆三态/来源层级。所有历史声明顺序、enum值、Config.Default真值不变。
- 新字段后基础旧YAML2/2通过，但Variant虽然读到Layer4，新字段prefabOverride=false，真实暴露FSA不足。Ruling：在现有显式UIPanelAssetMigration中增加内部MigratePanelConfigOverridePaths，迁移7个已知Panel目标路径，保留全部其他modification，冲突旧新值先拒绝、同值旧项不重复。无InitializeOnLoad/运行时修补/新公开配置入口；MigrateTips在保存前调用。
- Variant迁移入口缺失的RED复现后实现GREEN；回归同时断言重复调用无变化、旧新值冲突时实际API状态不变及保存重导入后值/override/父Prefab关系。冲突测试比较SetPropertyModifications之后的真实快照，避免把Unity自身的排序误认为迁移写入；未放宽override断言。
- 显式迁移前保存5个自有Panel Prefab原始字节/GUID，在全部preimage匹配后逐个使用PrefabUtility.SavePrefabAsset和原内部路径迁移重导入；逐项比较七字段有效值、YAML所有document fileID及meta字节完全保持。实际diff仅4个base Prefab的7个键与Overlay一条propertyPath；没有重排/删除原Transform modification或改变Layer4/false/零值。
- 旧YAML三项GREEN；CoreDefaultResourceEditorTests/CoreDefaultResourceTests/PanelConfigurationTests整组45/45。用户3个已修改FSM资产指纹原样。迁移记录与原文保存在批准temp的panel-assets-before，不提交该临时夹具。
- 其余域字段、Profile、Save、TimerObject/HelpWindow/其他历史作者字段与Systems私有/公开命名仍待本T5后续小批，T5及G3未验收完成。新代码及测试最终格式收口与全局回归待后续，不把本段成绩替代完整验收。

### T5 / Profile、Save、Timer 与 Help 状态（2026-10-22）

- HybridCLRBuildProfile六字段统一camelCase并精确FSA，8个真实CSharp消费者、窗口FindProperty、2个测试反射名同步。四个旧Profile YAML用例覆盖Development真假、空/显式入口数组、Recipe/BaseProfile引用和重导入；先以新字段recipe缺失RED，完整BuildPipeline55/55、Tool窗口15/15通过。
- Profile指纹的匿名Development成员先改为显式`Development = profile.development`，保留旧指纹键；Tools/HybridCLRReceptionBootstrap的profile接收者六字段同步，并在改名前将两处隐式BaselineDirectory回执成员改为显式同名，避免JSON回执意外变更。独立接收模板实际编译仍待T6/7，不复用旧接收回执。
- Profile指纹测试首次因本机官方热更名单为空未达到目标断言；核对实际9.0.3源码的Settings字段后，测试仅同步替换内存名单、definitions/preserved数组并finally逐引用恢复，不调用Save。ProjectSettings/HybridCLRSettings.asset指纹仍`dfa01389c9f47c9e8fd0c78d7c58bf851ec02bca`，未更改官方包或设置文件。整组EditMode超时后预约取回55项Passed，而非重复运行。
- SaveIndex/Store/Manager.CurrentSaveId、SaveItem.SaveId/_saveId与构造参数统一Id，排序枚举用SaveId/Descending，数字顺序不变。明确JsonProperty保留CurrentSaveID/SaveID等磁盘键，不改schema。新增旧原始JSON冷重开/重命名再写/再次重开测试先RED缺新API；Save安全/Facade/SerializedDictionary存盘35/35 GREEN，旧键存在且新CSharp大小写键未写入。其余Save参数与运行态字段随后续Systems批收口。
- TimerObject.Action/Abort → onCompleted/onAborted（类型、顺序、默认delay=2/ignoreTimeScale=true及原取消语义不变），加FSA；真实旧Prefab YAML保留SetActive持久目标/方法、真假参数、显式ignoreTimeScale=false与3.25秒。新字段缺失RED后通过；原六项实际帧/取消/重启PlayMode6/6，反射布局断言只更新字段名而未弱化。
- HelpWindow十一持久字段去m前缀、mInitialGroupsSet→hasInitializedGroups，加FSA并同步三个反射测试文件。新字段缺失RED后发现EditorJsonUtility并不以预期方式应用该别名；Ruling：不为错误测试API给产品新增JSON兼容层，改用实际Unity旧YAML资产导入。夹具只把自建窗口的DontSave标志去掉以保存到自有临时asset，测试完删除/销毁，不触碰用户窗口。旧页面ID、查询、页签、宽度、折叠组与初始化真假两例均保留；Help/Timer相关EditMode42/42通过。
- Timer/Save/UI文档同步，UI表明确PanelConfig序列化camelCase与PanelOverrides非序列化PascalCase不同职责。上述原数据旧名仍保留在FSA/JsonProperty/旧YAML证明中，不以旧字符串零命中作为完成标准；剩余T5域数据及整个Systems整理尚未结束。

### T5 / Domain 与 Fsm 数据及类型族（2026-10-22）

- Domain 的 Skill/Buff/Mission/Equipment/DataAssets 共36个字段采用语义改名和精确FSA；Json.NET剪贴板的only_can_set_in_progress/only_can_set_larger/des/custom_data/param_name通过显式JsonProperty保留。旧YAML五模块乘真假共10例先因缺typeScriptGuid而RED，后10/10 GREEN，覆盖数值、数组、列表、父引用、脚本GUID、图标与Buff Effect子资产及重导入。Inspector/Drawer/Excel列/反射字符串定向同步26文件，不改有效协议旧键。
- 原语义闭包漏掉Assembly-CSharp-Editor中的Domain验收源码，真实Unity编译暴露7处引用。补齐后闭包为34程序集/1207源文件，额外包含Assembly-CSharp/Assembly-CSharp-Editor与游戏程序集；不把原32程序集辅助编译冒充全工程验证。定向六fixture 61/61；广域EditMode116项中114通过，两个DomainTwoPlay因测试环境未启用DisableDomainReload而失败。
- DomainTwoPlay临时启用DisableDomainReload时又触发UIAcceptance场景自身的UI34输入断言；未改产品或削弱断言。确认无脏场景后用自建空场景隔离，实际2/2通过；恢复原SceneSetup、enterPlayModeOptionsEnabled=false及原选项DisableDomainReload/DisableSceneReload，删除自有空场景目录与临时SessionState。EditorSettings.asset无Git diff，受保护六文件blob仍与T0一致。
- Fsm同族51类型统一Fsm，namespace及asmdef身份保持；IPamaterCompare/FSMParamterFactory/CharacherController2DInspector分别修正为IParameterComparer/FsmParameterFactory/CharacterController2DInspector。九个Unity字段精确FSA；序列化runtimeFsmControllerLayers与运行态_activeLayers明确分开。bool IsInitialized与Action Initialized解决真实同名冲突，Action形状不变；StateChanged、Matches、CreateFsmState及FsmConst三常量值保持。34程序集辅助编译前后零错误后应用68文件，47脚本经AssetDatabase保GUID移动，未改变文件内伴随类型布局。
- 三个旧YAML迁移用例先因缺新字段RED。迁移后复验发现夹具Save仅标脏未落盘，补SaveAssetIfDirty；另一个夹具启用绑定历史Editor PID4708的保存过滤导致真实Prefab保存报错。Ruling：无回执的独立测试仅借用原目录白名单过滤，历史流程的Gate/Guard以及任何回执输出仍调用原Identity；不放行额外路径或伪造历史证据。旧YAML真假、named-layer Prefab及无关脏资产保存护栏4/4 GREEN。
- 完整Behavior EditMode首轮78/79，实际Inspector仍有group_conditions字符串导致组列表NullReference；改为nameof(FsmTransitionData.groupConditions)，复跑79/79。Behavior实际PlayMode51/51，无失败/跳过，涵盖Fsm/碰撞/移动平台；Unity编译无错误。历史EditorPrefs/SessionState键、Asset FSM菜单品牌、上游来源路径/SHA及模板文件名保留。
- 已核对受保护三个游戏FSM资产属于XFFSM，不属于YL数据迁移；未改其字节。用户未跟踪Assets/Test/NewState.cs只做YL基类引用迁移，示例asset不接管。Systems其余私有字段/API/格式、SplitWindow持久字段及T6–T8仍待完成，不以本批通过宣告G3全局通过。

### T5 / Systems 私有运行态字段（2026-10-22，仍在验证）

- 从真实Unity图生成1408候选，排除Unity字段、const、外部契约、原生internal布局及非自有范围。审查后118项显式语义/词法修正，保留LoggerSession._writeToFile行为选项，实际1407项进入语义迁移；loop/mute/leaveOpen/bindCore/autoFade等选项不机械加Is。特别依据SkillBase实际控制流确认isCooldown=true表示冷却完成，改为_isCooldownComplete，不采纳相反含义的isOnCooldown候选。
- 临时工具第一预览发现前缀规范脚本的PowerShell运算优先级错误，停止且未写产品；第二预览在完整后编译抓到FsmTransitionInspector的GUIContent auto_switch与bool autoSwitch同归_autoSwitch冲突，仍未写产品。明确标签_autoSwitchLabel与值_autoSwitch后，第三预览34程序集前后均零错误，再核验全部SHA256 preimage后应用241源码文件。新增匿名成员名保护的临时自测通过；本批无需要额外保留的匿名协议键。Unity实际scriptCompilationFailed=false。
- 29个定向测试/验收反射文件批量同步；原Unity布局/原生GUI/private字段不属于本批的字面量不改。复查发现临时PowerShell字典大小写不敏感误触Stream/Settings等公开字段，依据逐文件preimage及afterhash修正为Ordinal映射后重建正确结果，没有覆盖并发修改或保留错误替换。另手工同步AudioVoice清理数组、Action Help负见证以及T4遗漏的LoadViewRequest._resourceRequest反射。
- Audio整namespace首次实际521项为50通过/471失败：464是前置Play退出后的AudioKit quitting污染（其中377还产生TearDown次级NullReference），5是同一大套件中场景被前面测试标脏而拒绝接管，2是已有固定源码闭包缺UIRootContext。确认实际非Play、无Audio宿主、_isQuitting=true；仅调用既有ResetSession恢复冷EditMode前提，不改产品生命周期。整namespace再次运行仍会先执行Play型fixture再污染后续测试，因此按既有会话边界拆分，不削弱断言。
- Ruling：Audio离线闭包纳入Root真实依赖的完整Core源码，而非缺UIRootContext的旧Core/ResourceAccess清单；新增完整目录等价断言，仍拒绝预制YL/XF/XFAB/可选程序集，不用stub补编译。冷EditMode的非Play型Audio452/452通过（包含两个真实AssemblyBuilder闭包），单独原生class筛选AudioSoundBudgetTests61/61，保留其TestCaseSource自定义名称与27个JointAdmission组合，不改名或跳过。
- Actions整namespace首次345/346，唯一失败为历史依赖白名单不识别已集成的BouncyCastle.Cryptography。Ruling：复用FrameworkAssemblyIndependenceTests.IsApprovedBouncyCastleReference校验实际编译图中的受批准文件，不按DLL名字无限放行或加生产依赖。改后346/346通过。
- 当前ResourceEditor完整EditMode已启动，MCP请求超时，实际结果待续读；不得盲重跑。Audio五个Play/资源会话fixture、Resource其余回归、Domain/Fsm私有字段后的复验、未纳入候选的现有下划线缩写、公开API/参数、SplitWindow及格式仍未完成。临时文件集中在yl-style-semantic-20261022的systems-reviewed-map-v3.json、systems-private-preview-v3、systems-bindings-v2；全部变更未Commit/Push。
- 续读ResourceEditor实际结果100项为65通过/35失败，未将旧Actions缓存346计入。期间出现Caller must complete domain reload断言，最终确认native TestRunnerApi.IsRunActive=false、pending请求为空、编译结束。失败包含工作台/构建遇到非settled Editor、GUI交互未响应，另有两项生成编译测试因现有Assets/YLFrameworkData而正确拒绝接管、旧反射调用没有为新增可选provider参数传值，以及SigningWindow.selected仍为Unity字段却被跨类型同名映射误改的反射。
- 修正SelectedSigning测试仅恢复该真实Unity字段字符串selected；BuildIntegration改用正式强类型Preflight/BuildAndPublish调用，保留原只读/双版本/生成失败断言。Ruling：ResourceCodeCompilationTests保留原指纹与拒绝接管机制，输出预约改为自有Fixtures/ResourceNamesCompile-<guid>，跨重载由SessionState保存，不占用或删除现有YLFrameworkData，不改生产默认导出根；该更改尚待实际编译回归。
- SelectedSigning与BuildIntegration定向复验已经启动，MCP再次超时；继续等待实际新结果，不重复启动或修改其运行依赖。未通过的GUI/构建以及生成编译用例仍是开放事项，不能将全部35项归类为已排除的环境噪声。
- 该定向复验实际1通过/1失败：SelectedSigning通过；BuildIntegration已完成正式预检与两份native构建/发布，随后PublishedCatalogs因既有yl-resource-multidirectory-modern-20261018的Unity/App兼容身份不同而拒绝。当前产品合同明确禁止静默忽略已发布的不兼容项目，因此不改产品、不删除或重签历史制品、不让测试自动接管它们；完整双项目收集/生成失败部分留待独立干净接收环境复验，不算通过。已继续启动ResourceEditor除CodeCompilation/BuildIntegration/ResourceEditorTests三个会触发生成或全局环境约束的fixture之外的集合，MCP超时，结果待续读。
- 该集合实际89项57通过/32失败，完成后已取消定时续查。冷态独立StaticReadiness1/1及ResourceWindowTests5/5通过，重现大套件中的编译污染而未改产品。Interaction独立7项6通过，剩余NativeTableKeepsHiddenSelectionAndResolvesModelIndicesAfterSortAndUndo连续两次在AssetDatabase.CopyAsset引发的Visual Studio项目同步中遇到Assembly-CSharp.csproj共享冲突；不是产品断言失败。只读Restart Manager两次成功查询均未检出持有者，不能据此认定锁来自某特定进程；未编辑生成文件或修改IDE配置。
- SplitWindow.horizontal_split_percent迁移为horizontalSplitPercent并精确FSA，唯一EffectConfigsWindow派生引用语义同步；34程序集预览前后零错误。真实旧窗口YAML的0与0.57、新建默认0.3、保存/重导入测试先因新字段缺失2项RED，后2/2 GREEN。未变水平拖动/钳制行为、窗口类GUID或ShowBorder形状。
- ResourceCodeCompilationTests已将全部GenerateReferences调用显式传入自有预约Output，不只修改断言目录；中断重入及预约清理保留原所有权拒绝。独立class2项已启动，MCP超时，等待实际跨域编译/结果，绝不并发修改其运行依赖。新增Systems surface只读候选432项，正在审查Unity真实序列化/消息/API/协议边界，尚未应用。

### T5 / API、真实作者字段及协议复核（2026-10-22，未收口）

- ResourceCodeCompilation真实生成/asmdef及predefined消费者、二次生成/拒绝自定义覆盖/回滚测试已通过；跨域后MCP只保留最后一个用例，另独立执行InterruptedCleanupRefusesRecreatedOrModifiedOutput也通过。测试自有输出目录与预约已清理，原UIAcceptance场景保持clean。AudioContinuousPlay1/1、AudioExit1/1、AudioResourceProvider3/3；AudioResourceBundle的native运行结束后缓存仅可证明RealAssetBundleSyncAsyncDependenciesAndFinalPhysicalUnload通过，另两项仍需独立复验，不能将缓存1项冒充整个fixture。
- Systems非序列化surface采用298项审定映射，成组更新ActionId、Equipment的CanEquip/Equipper/OnUnequip/Unequip、Buff的CanAdd、Skill的CanRelease、ContainsParameter/ContainsEffect、状态IsMet及通知字段等。修正公开/内部字段与属性冲突，保留Action字段形状、Unity消息与持久键。34程序集前后辅助编译零错误后应用117文件，必要同步EditorHelp/反射/文档及未启用的Equipment Player/DomainExcel引用；未启用分支仅同步源码，不宣称已编译。
- Actions实际346/346再通过；Behavior/Domain/Equipment的广域173项首轮138通过，35项暴露旧反射/source守卫等非语义引用，修复后170通过，剩余3项为作者树拖放拒绝。独立重现发现payload存在、object refs=0，但原生drag paths仍指向先前Resource测试已删除的Release82b-6fcee875846048fabcad568d74df6279/Content/disabled-drop.txt。常规paths赋空与PrepareStartDrag不能终止该原生拖放；核对Unity2022.3官方DragAndDrop.bindings.cs后，仅在完整路径匹配、目标已删除且无外部引用的断言保护下调用native Cleanup，未清用户拖拽。最终173/173 GREEN；产品拖放拒绝逻辑不变，测试保留payload诊断消息。
- Unity现场SerializedObject确认PlatformCatcher2D.Contacts不是序列化字段，保持PascalCase；OverlapBase.Results确为序列化字段，不能因只作运行结果而永久豁免，先把内部缓冲字段分开为ResultBuffer，再迁移公开结果为results。ResourceBuildWorkspace的JsonUtility所有权/工作区DTO字段及AudioSoundConcurrency原始JSON键保留为协议例外。
- 新增六项真实旧YAML测试（Audio库/嵌套配置/加权、Resource Project/Recipe/Atlas/工作台布局、Character/Overlap/Reddot/Action Prefab）均先正确RED缺新字段。105个真实字段添加精确FSA；预览抓到元组隐式名称随字段改名漂移，修复临时工具显式保留原元组名称并补自测（匿名JSON键也保留）。34程序集前后辅助编译零错误后应用140文件；六项真实旧YAML值/真假/对象链接/保存重导入/meta验证6/6 GREEN。Inspector/模板等非语义字符串由限定worker同步，尚待完整运行回归。
- Ruling（纠正初步分类）：进一步读取Audio.md:128–136发现已公开承诺完整旧AudioSoundOptions JsonUtility JSON原键，不因生产源码内无调用就否定该合同。按本Plan的JSON协议例外，AudioSoundOptions10项以及嵌套AudioAutoFade2项/AudioOutputRoute2项共14字段应恢复原PascalCase，保证FromJson/FromJsonOverwrite和输出旧键；不用FSA冒充JSON兼容，也不新增兼容壳。原105映射中这14项将精确收回，其他91项继续camelCase。正在补先红后绿的完整旧JSON回归；恢复尚未应用、不能报告JSON合同已通过。若该分类过保守，代价仅是这14个双用途字段继续作为明确协议例外，而非数据丢失。
- 旧Variant路径不能仅由FSA保证override身份。新增覆盖根字段、嵌套/数组、冲突原子拒绝与幂等的真实Variant回归，拟复用最小显式迁移能力解决；不挂自动hook、不批量保存用户场景/资产。该测试及helper尚未验收，当前T5、G1–G5仍未完成。
- 协议纠正已完成：新增旧JSON真假测试先RED（Volume落回1、Loop=true丢失）；14项精确语义恢复前后34程序集辅助编译零错误，36文件preimage核验后应用。移除本批给这14项添加的自别名FSA，不保留无必要兼容壳。旧JSON完整键集合、嵌套Route/Fade及部分FromJsonOverwrite读取已GREEN；Audio.md明确解释该双用途协议例外。最终作者迁移表为systems-serialized-final-map.json的91项。
- 新增Editor/Foundation/Serialization/SerializedFieldMigration.cs（Unity生成meta GUID=44199036ca6c2764e95db4c0bfdda625）：只在调用方加载的Prefab上按YL字段FSA解析旧路径，支持List/数组和嵌套字段，全部冲突预检后再写，保留目标/值/引用，不自动保存。原Panel入口以Panel目标过滤复用，不新增自动迁移hook；同时修正ConfigureTips的六个既遗漏camelCase字段字符串。新增Variant2项先RED缺helper；修正测试预图采样为Unity SetPropertyModifications之后的真实顺序后，原子拒绝比较包括target/path/value/objectReference，不削弱断言。
- 联合实际Unity51/51通过，包含6项旧YAML、2项旧JSON、2项Variant及原CoreDefaultResourceEditor/PanelConfiguration回归；旧值、父Prefab、幂等、覆盖标记、冲突不写与meta均验证。限定worker26个绑定/模板文件完成，主代理另同步WeightedClip反射/XML字段ID与ResourceWorkbenchLayout精确反射；保留Catalog/workspace/owner/EditorPrefs等旧协议。bootstrap数组profiles[]四处旧Profile字段也已补齐，模板仍须T6独立编译，不用主工程编译冒充。
- 冷Audio常规集合新增4项后首轮455/456，唯一失败为旧WeightedClip XML字段ID，已精确同步；复跑456/456，无失败/跳过。当前Resource GUI十一fixture集合独立执行中，MCP再次超时，需续读实际结果，不盲重启。EditorSettings.asset仍无diff；T5其余状态属性/类型拼写、参数/局部/测试与全Systems排版、实际Play回归及T6–T8仍未收口。
- Resource GUI集合完成76项74通过；一项断言仍期待旧YAML Id而实际id已正确保存，一项被Visual Studio项目同步写Assembly-CSharp.csproj时的共享冲突打断。修正前者断言为真实新键，不改保存逻辑；随后这两项定向2/2通过。等待期间只读确认原Unity处于native Build AssetBundles/TestRunner busy且Responding=true，未重启/杀进程或改生成文件，完成后已取消cron。
- 状态收口66项：Action IsPaused/IsDeinitialized、AudioVoice IsActive/IsPaused、Fsm instance IsDisposed/HasStarted、Resource瞬态及诊断状态等，保留行为选项和Required等政策字段。ICustomAPI/IconGUIContent及两个Overlap Inpector拼写同步；34程序集/1208源文件前后辅助编译零错误后应用135文件，匿名证据JSON的GenerationRequested/UnityBuildInvoked/ForcedRebuild键显式保留。13文件精确反射/Help/XML同步，Mission已失效的注释类副本删除。
- 初次新Unity图遗漏非选中源码程序集的assemblyReferences，辅助编译在任何产品写入前抓到TestRunner/UGUI/XF缺引用；捕获compiledAssemblyReferences与全部assemblyReferences输出并集后恢复34程序集前后zero errors。临时PowerShell类型数组展开也先被预览拒绝，改为明确记录后才应用。后续有效清单为unity-editor-post-status.json；不以缺引用预览替代Unity错误结论。
- AssetDatabase移动6文件（Action的CallbackAction/ConditionAction/CoroutineAction/TaskAction及OverlapBase2D/3DInspector），逐项核验GUID与完整meta hash不变；回执status-action-script-moves.json。首次MCP因已有域重载断开，确认回执未写且旧路径存在后才重试，没有盲目重复移动。新路径已纳入34程序集/1208文件清单。
- 实际Unity回归：Actions+冷Audio802/802；Behavior/Domain/Equipment/Http/Save合计217项首轮216，唯一Overlap反射Invalidated旧字符串，精确同步后217/217；AudioSoundBudget61/61；Catalog/Codec/Artifact查询47项首轮46，唯一Eligible反射旧名，同步后47/47。没有减少发现集、跳过或弱化行为断言。GUI遗留原生拖放路径再次仅在确认属于已删除Release82b自有夹具且无外部对象引用后清理。
- 最后名称盘点发现自有代码尚有987私有字段（主要测试/集成）、2参数与154局部名组；均未宣称已完成。2参数+154局部已审定，临时工具补大小写、局部捕获/局部函数、同名const与普通变量角色隔离的自测；常量PascalCase不会误改同文件camelCase普通变量。34程序集前后zero errors后应用113文件；nameof值仅变化构造参数的ArgumentException参数名，没有持久键漂移。私有字段候选仍由只读worker审查，尚未应用；括号/多字段拆分/全白名单格式及后续实际验证仍待完成。
- 名称收口后的Actions PlayMode178项171通过，7个GlobalMessage用例因前序fixture留下公共Update宿主而在SetUp拒绝接管；独立新Play会话7/7，通过所有原断言，未删其他所有者的宿主。Audio PlayMode174项首轮171，通过修正PlaySoundAction六处mHandle反射后173通过；最后mOnStarted同类旧反射精确同步，AudioStartedComponentTests独立12/12。完整174现版本尚待整体GREEN，不把173加单项复验写成一个不存在的完整通过收据。
- 私有盘点的只读审查确认无JsonMapped候选，真实反射缺口为HybridCLRWindow导航style/bounds；page、所有Unity选项、Prefs/SessionState值、旧YAML及协议键均不补入候选。追加bool研究未返回实质结果，主代理自行读取410个私有bool/bool?并依据读写语义分类，明确90个状态/选项/原值快照目标；例如ownsEnvironment、wereOptionsEnabled、shouldRestartLayout，fade/loop/skipNextSave等行为选项不加Is。与987候选合并成995项（含既有下划线状态和已知_character拼写），34程序集前后zero errors后应用192文件；没有nameof值或匿名协议键变化。导航三处反射同步，不永久豁免正常私有字段。
- 结构批次在1081个实际Unity图内自有路径上执行：368处多字段拆为单字段（69文件），用结构签名逐字段比较原顺序/类型/修饰符/属性/初始化表达式，非字段token全部不变；原生Cng布局也保持原字段顺序。带分隔注释/条件指令的含糊声明会拒绝，不猜迁移。临时自测覆盖副作用初始化、重复属性、注释拒绝和路径规范化。官方IDE0011补括号687文件，非括号token与注释/条件/禁用文本逐项不变；官方Roslyn OrganizeImports整理174文件，所有导入、非导入token、条件文本保留，不编写自定义using排序器。
- 首轮后台全库whitespace Apply后Check仍有未收敛项，因此没有宣称幂等；单文件真实Apply/Check通过，格式工具原8组及新增65文件多批目录用例共9组隔离自测通过，未证实需要修改格式器逻辑。随后前台完整显式Apply并完成全白名单Check：1110文件、SDK9.0.312，zero changes。仓库根配置、工具边界保持，Test-Format-YL新增大批次覆盖；未用放宽规则或关闭诊断制造通过。
- 当前Unity实际idle、scriptCompilationFailed=false，原UIAcceptance dirty=false；git diff --check通过（只有受保护.mcp.json原CRLF提示），六保护blob仍与T0相同。包含Actions/冷Audio、Behavior/Domain/Equipment/Http/Save、Core旧数据及HybridCLRToolTests的广域EditMode已启动，MCP超时；等待实际结果，禁止盲重跑。T5未完成；T6仍需Simulation→IsSimulation等集成public面及模板独立编译/生成链，T7新基座三场景、T8完整独立Review均未做。
- 该广域EditMode实际1075/1075、无失败/跳过，native TestRunner已结束。整理后Audio完整PlayMode174/174；Actions按既有宿主所有权边界分独立会话171/171及GlobalMessage7/7；Behavior/Domain/Equipment/Http混合94中非Http74通过，Http20均因前fixture公共CoroutineStarter宿主而在SetUp拒绝接管，随后独立20/20。Resource PlayMode68/68、Extensions.Data6/6、DataAssetResource9/9。
- 最终Domain/Equipment双Play使用自有空场景与临时DisableDomainReload，3/3通过；已恢复原SceneSetup、optionsEnabled=false及原DisableDomainReload|DisableSceneReload，删除自有CodeStyleFinalTwoPlay-f5d8bf40b1164034b18715b0e02075c8及所有预约SessionState。DataDomainReloadSessionTests另有历史MB26/PID/固定sourceTip专属前提，本会话不伪造该owner或改门禁来执行，不能算作本轮已通过。
- AudioAcceptanceResourceTests两项通过，工具Duration为明显异常值，未将其作为运行耗时证据。独立NativeCancelAudioShutdownRetryAndResourceExitDrainLateResults首轮OneTimeSetUp返回空AssetBundle Manifest；真实Unity Console根因为Player编译HybridCLRLoader.cs:89的!UNITY_EDITOR分支仍用旧moduleRequests。精确同步为_moduleRequests，没有改BuildPipeline行为、异常或测试；修复后同一方法已重启实际native回归，MCP超时，结果待续读。新增只读条件分支审计worker检查同类漏引用，当前尚未返回。该问题证明Editor/Roslyn当前宏编译不能替代Player，G2/G5没有提前验收。
- 续读确认NativeCancelAudioShutdownRetryAndResourceExitDrainLateResults实际Passed，native TestRunner inactive、Unity编译无错误。随后独立ProviderProgressReentrantExitNeverPublishesInvalidClip已启动，MCP超时，待续读；没有并发改依赖或把缓存当新结果。只读审计已确认另有HybridCLRLoader IL2CPP const simulation/Simulation局部名、Unity6及DomainExcel禁用分支漏引用，最终去重清单未回；待当前native结束后统一修正并按可用平台实际验证，不将Unity6静态检查描述为编译。
- ProviderProgressReentrantExitNeverPublishesInvalidClip续读实际Passed且native runner inactive，已取消其wakeup。现独立RealAssetBundleSyncAsyncDependenciesAndFinalPhysicalUnload正在运行，MCP超时，待续读。只读分支审计已完整返回24组真实漏项（Native局部常量、Unity6 Runtime/Editor、YL_DOMAIN_EXCEL），没有编辑文件或冒充编译；父代理等待当前native结束后执行限定引用同步，不修改namespace/官方消息/旧协议值或旧YAML。
- 第三个RealAssetBundleSyncAsyncDependenciesAndFinalPhysicalUnload也已实际Passed并确认native runner inactive；本轮三个Native AudioBundle方法均有独立新版证明，非跨域单项缓存拼接成虚假一次完整suite。
- 限定worker已通过apply_patch完成24组/22个C#文件条件分支名称同步：HybridCLR native局部常量Simulation；Unity6 Character/Platform/Overlap查询缓冲及Editor窗口/Tree/Context旧名；DomainExcel四源码文件旧域C#字段。原公开List results、合法局部/参数、Prefs/SessionState、FSA/JsonProperty/旧YAML、Excel工作表与字符串/模板输出均不变。该审计及修复未运行Unity6，当前仍只记录Unity6静态证据。
- T6已开始重叠准备但尚未验收：集成public/内部面12项通过34程序集前后zero errors后应用10文件，含LoadResult/RuntimeCalls.IsSimulation、HasSucceeded报告、IsEditorBusy/AreAssetsSaved、DOTween Lease.IsClosed等。Unity IOrderedCallback.callbackOrder和OnGUI消息作为外部约束保留，Manifest.Development仍为配置/协议选项。手写Bootstrap/Runner/SubsetProbe同步新消费者；Stages回执显式Succeeded=s.HasSucceeded，保留原JSON键、simulation键及BuildResult/ResourceRequestStatus.Succeeded官方枚举。
- 资源样例十个真实Unity字段以精确FSA迁移camelCase，非序列化ContentKeyProvider不纳入；新增旧Prefab/Data YAML真假测试先2项RED缺mode，改后2/2 GREEN，覆盖默认值、显式flag、数值/字符串、保存重导入和meta。34程序集前后zero errors后应用6文件，当前注入密钥/执行顺序和默认导出根不变。主工程新符号编译不等同于接收模板已编译，T6的独立模板与真实生成链仍待执行。
- 含Behavior/Domain及HybridCLR Manifest/Metadata/BuildPipeline/Tool/AssemblyAnalysis/Boundary和新样例旧数据的定向EditMode集合已启动，MCP超时，待读实际结果。T5的最后分支修正验证尚在收口，不提前将T5/T6标为完成。
- 上述定向集合已实际274/274通过，native TestRunner inactive、Unity编译无错误。T5完成并转入T6；此完成只代表已记录的Systems/数据/行为/必要引用范围，不替代G2全平台闭包、G4独立交付或G5新Native基座。DomainExcel的条件构建、Unity6静态范围及手写模板/新输出均在T6继续验证，未启用条件不以主工程无错宣称通过。

### T6 / 条件编译与生成命名（2026-10-22，完成）

- 未改工程defines或ProjectSettings，使用唯一Unity的AssemblyBuilder在批准temp目录以额外YL_DOMAIN_EXCEL定义实际编译7个DomainExcel生产/测试源码，0 errors/0 warnings；收据domain-excel-compile-91295538a2664f6783c2f8afb2cf2330/compile.json。它证明可选源码在该条件下的Unity编译，不等于默认asmdef已启用或Excel测试已执行。Unity6仍只静态核对；4个Equipment Player源码已由当前实际Player/AudioBundle编译覆盖。
- 资源生成器现有Identifier会输出Sprite_alias、Sprites_png_Left等，不满足已批准生成命名规范。先将契约测试改为PascalCase并新增大小写/Unicode/原值/合法旧owned输出升级/保留meta/拒绝手改用例，实际10项首轮7通过3正确RED；随后只改标识符转换及子资产类型组合，地址/ProjectId/alias/Group值、Ordinal排序、碰撞拒绝、首行v1 hash、默认目录、asmdef和输出所有权/原子写入机制不变，10/10 GREEN。数字起始仍保留必要前缀_，UI/IO及Id/Http等缩写按规范；新碰撞继续拒绝，不加静默后缀。
- ResourceCodeCompilationTests消费者改用当前PascalCase EntryTxt/SpritesPngLeft/StableAlias/NewAlias，根Project类型从正式生成文本读取，不复制第二份命名算法。真实隔离生成/双消费者编译/重生成/拒绝损坏的class已启动，MCP超时，等待新实际结果；旧cross-domain缓存不能冒充完整2项。Module/Fsm模板原有global::/Preserve和占位符已回读，实际新生成编译仍在后续验收中。
- 续读RealGeneratedSourceCompilesForAsmdefAndPredefinedConsumers实际Passed，独立InterruptedCleanupRefusesRecreatedOrModifiedOutput也Passed，所有输出预约为空。Module/Fsm经两个正式入口在新StyleModuleb0c9e7658cf34735879e9d49cc267ae9目录生成，真实Unity加载独立YLFramework.Core.Examples同名程序集，验证SingletonModule基类、FsmState/Preserve、全部源/meta GUID与重生成拒绝，LF/无BOM/单个EOF换行；收据module-fsm-generation-proof.json。只清理该owned输出及原不存在的空父目录，未覆盖用户模块。
- Analyzer按真实SDK9.0.312/Roslyn3.8重建部署，0warning/0error、129/129，DLL仍为219832878E80F4C312DBF58CBEC44DBF9928861B2EB398A2251C7619F2751FC1，既有meta保持。
- 新独立接收根C:/Users/Kano/AppData/Local/Temp/kilo/YS26、项目R，owner绑定来源工程与本专项。初次后台Create被中断且无exit收据，未称成功；确认recorded PID已不在、ProjectVersion正确、Assets空后，仅补现有Create流程预期的UGUI1.0.0/Newtonsoft3.2.1 manifest后置条件，保留create-recovery.json及原日志，不伪造退出证明。后续改为前台正式Invoke脚本：默认包无可选/测试first import exit0、可选包无官方包first import exit0、解析官方精确9.0.3后exit0，8个真实产品DLL、无XF/tests/acceptance消费者，独立style-supported-product-import.json绑定两个产品包SHA与DLL SHA。此为当前源码独立产品编译，不借主工程DLL/测试补依赖。
- 产品-only证明后才首次Install真实接收模板，style-harness-first-compile实际exit0；模板整理从该接收工程真实compiler rsp捕获2项目/3模板引用上下文，辅助编译前后zero errors（不冒充Unity）；官方IDE0011加91处括号，13个私有字段规范化，官方Formatter保持每个token/字面量/注释不变。正式Sync后existing meta SHA全部不变，style-harness-sync-compile实际Unity exit0，首次与后续同步均有证据。
- TLS宿主脚本CompileOnly先正确RED于旧精确Record源码守卫（它还引用旧sync/errors压缩方法）；改为当前多行_sync/_errors精确方法，仍不匹配就拒绝，日志注入仍在lock后，未放宽守卫。FixtureHost仅补控制流括号，现CompileOnly实际通过，没有启动服务/改系统信任/输出私钥。
- 用当前程序集显式HybridCLRTestFixtures.Prepare刷新本机MVID/hash夹具后，HybridCLR完整PlayMode25/25、CoroutineStarter15/15；完整HybridCLR EditMode已启动，MCP超时，待新实际结果。该回归运行时不并发改依赖。T6暂未完成；当前Bootstrap旧SubsetPlayer/SubsetCache/SubsetCompatibility等旧基座常量绝不能用于本轮执行，T7须先绑定新short工程/Player/cache/基线/兼容身份，且保留旧H26/HC26/H26S等产物。
- 续读完整HybridCLR EditMode实际125/125通过，含两个HybridCLRSessionTests；native TestRunner inactive、scriptCompilationFailed=false、原UIAcceptance clean。CoreModuleGeneration/BehaviorAuthoring/ResourceCodeGeneration/ResourceOutputSafety联合74/74。手写三模板最终official using/format预览均0变化；全1110文件Apply/Check和9组工具自测通过，但后续发现的紧凑语句缺口不能由这项Check推导为符合完整排版规则。
- Ruling: 官方Roslyn4.14/SDK9 whitespace在明确preserve=false时仍保留部分紧凑catch/finally及相邻语句，隔离样例与加强断言已真实RED；不是路径配置漏载（AnalyzerConfig精确读取已确认），不能把返回0当完整结构验收。沿用计划§3的显式结构整理责任，在既有一次性Roslyn工具内只为非空block/同排相邻语句与成员插入显式LF换行，缩进仍由官方Formatter承担；不新增永久formatter/依赖或重排token。临时工具先RED后GREEN及二次0边界，逐文件token/字面量/注释/directive保持校验；不使用会被官方Formatter重新折叠的elastic lambda边界。薄入口自测明确仅验证已展开控制块的幂等，短规范同步该真实限制。若判断错误，成本为仍需人工识别的版式遗漏，不会改算法或协议。
- 显式版式收口应用主图436文件/5718边界、三模板67边界及图外/工具9文件130边界；全部token/字面量/注释/directive保持，主图34程序集与模板2程序集辅助编译前后0 errors。四个实际Equipment Player夹具路径为Tests/Fixtures/Extensions/Equipment/Player（不是Tests/PlayMode），另手工补控制流括号、拆分Result多字段声明；原JsonUtility回执键保留。模板补LF全文件并验证二次expand/format均0变化；正式final Sync三meta字节不变，style-harness-final-sync-compile实际Unity exit0，TLS CompileOnly再次通过。全1110文件Check、工具9组、Analyzer真实129/129及0warning/0error/DLL原SHA均通过。
- 其余可选/闭包/导出EditMode首轮71/80，9个ExcelAssetAuthoringTests均因测试反射按名Single遇现有7/8参数重载而歧义；只将该夹具helper改为匹配泛型形状、实参数量和遗漏可选参数，不改产品API或资产断言，原集合80/80 GREEN。DOTween+XFAB真实PlayMode80/80，实际结果确认包含Xfab借用/取消/关闭负例；无失败/跳过。T6完成；T7仍须全局最终表面/禁用分支/文档核对、最新独立默认运行和新Native三场景，不能以此结束目标。

### T7 / 全局复核与新门禁（2026-10-22，进行中）

- 全自有表面重新盘点，不再只用Systems/Integrations目录筛选：首次329候选含26类型。逐项保留原生布局、Unity隐式layout接口、Unity消息/同名生命周期转发、公开rawJson/工作表和回执布局；实际SerializedObject探针区分了PlatformCatcher2D.Contacts、窗口Tree/Failure/Graph等不序列化误报，原场景保持clean。
- 按真实支持字段整理23种测试/验收作者类型共45字段，精确FSA；isAllowed/isComplete是真状态，gameMode/disableCache/expectLegacyLogger仍为选项。新AcceptanceAuthoringFieldsKeepExactFormerNames与LegacyAcceptancePrefabKeepsReferencesFlagsAndIdentity真假两例先3项RED，后3/3 GREEN；旧YAML覆盖对象自引用、URL、真假值、重导入、保存及meta/GUID/fileID。Resource GUI回执的RootHeight匿名JSON键显式保留。Audio验收反射及DomainAcceptanceAssets四个FindProperty字符串定向同步，不动AudioOutputRoute.Group等真实公开JSON布局。
- 非序列化/API/类型62项经34程序集前后辅助编译zero errors后应用103文件：包含BindableProperty.StoredValue、ApplicationTool.IsPlaying、CustomClassNameGui/CustomPropertyGui/OnPropertyGui、ExcelScriptableObjectId、ResourceWorkbenchGui及Domain Gui drawer类型族、SplitWindow.DrawGuiLeft/Right和测试方法/三个Fsm测试类型；不改namespace、程序集、字段/Action形状或常量值。Unity layout八属性、IOrderedCallback.callbackOrder和text反射外部测试替身保持原合同。去掉仅为已修正名称存在的两处旧诊断抑制，不添加新抑制。
- 16个当前绑定/帮助/文档/源码编译见证文件同步精确新标识符，保留Foundation旧名负守卫并增加新错误namespace与旧Foundation类型无别名守卫。历史Import-BehaviorSources.ps1的上游输入/初次转换拼写原样保留，未应用会误改其左侧源模式的第一次预览；当前离线runner路径已同步新测试文件名。冻结JSON/YAML、历史Plan、上游来源清单未批量改字。
- AssetDatabase保GUID移动25脚本（SkillAuthoring.cs内伴随类型仍原位）。首次同一StartAssetEditing内做双跳时第二步因尚未导入中间路径被Unity拒绝，仅第一脚本处于已知中间路径；先读取全部25个实际GUID路径与原字节/meta哈希，再分两阶段注册中间路径并移动，未重放原请求或覆盖任何外部文件。final-type-moves-before.json、final-type-move-stages.json及final-type-moves.json完整记录25项最终GUID、meta与源码字节不变。
- 当前唯一有效主图更新为unity-editor-final-surface.json：34程序集/1208文件，actual scriptCompilationFailed=false。所有旧post-status图及旧路径预览不得再用于apply。重盘点余222候选仍包含协议/原生/接口/Unity消息/不支持序列化误报，不以候选数归零代替真实契约审查。更正：上轮续查笔记误记了E0ReorderableListItem.Label/Number；实际源码与语义清单中无此类型，E0ReorderableListAsset.items为List<UnityEngine.Object>，用Texture2D子资产承载条目；撤回这两项推断，不创建虚构迁移或测试。
- 四个图外Equipment夹具实际受YL_EQUIPMENT_ACCEPTANCE defineConstraints约束，同时允许Editor/WindowsStandalone64，不是单纯Player-only；当前普通Editor/Player编译不能推导它们通过。已按真实源码将Probe的Prefix迁为prefix+FSA、三个非序列化private字段加下划线、Host允许状态改CanAcceptEquipment，并仅同步EquipmentBuild的probe.prefix/equipmentClassFullName。Result原12个JsonUtility回执字段保持。仍需额外define下五源码（含Editor builder）的真实AssemblyBuilder编译与JSON/别名证据，且不能冒充实际Player运行。
- 新一轮Foundation/Core/Excel/Localization/Behavior/GUI等所选EditMode集合MCP超时；最后只读native TestRunnerApi.IsRunActive=true、isPlaying=false、isCompiling=false、scriptCompilationFailed=false、UIAcceptance原场景clean。等待实际新结果，不拿先前3/3或80/80缓存算本轮通过，不再改其运行依赖。
- 续查六个保护blob全部与本计划开工记录相同；未Commit/Push、未改主工程依赖/设置/用户资产。T7新Native尚未开始：YS26保留T6产品first-import/模板证据；须在全部命名/条件分支收口后再导出最终包，以新独立owner/路径/基线/兼容身份完成当前版本证明，不能覆盖YS26已记录的包或旧H26/HC26/Delivery产物。
- 上述完整EditMode实际488/491，native已inactive。三失败中Excel注入回滚测试仍把typeof(Owner).GetField("Rows")传入私有ApplyAssets；实时反射已确认旧字段不存在、rows存在，故空FieldInfo使回滚再抛异常。只改为nameof(Owner.rows)，不动产品回滚实现/异常种类/字节还原断言，整ExcelAssetAuthoringTests 11/11 GREEN。TimerSessionTests在混跑的临时脏场景前置拒绝后独立1/1真实双Play通过；GUI一项来自VisualStudio生成Assembly-CSharp.csproj共享冲突，未改IDE/生成文件/日志断言，静止后独立1/1通过。保留首轮488/491事实，不伪称一次完整全绿；后续常规集合与真实会话fixture分开执行。
- Equipment额外define的实际编译已完成：前两次临时proof漏列原CSharp-Editor中的EquipmentSettings依赖，另确实暴露YL_EQUIPMENT_ACCEPTANCE里的旧stableFrames引用，已仅改为_stableFrames并展开该if。补真实公开EquipmentSettings程序集后，最小重建旧consumer严格只有equipment_class_full_name/Prefix两项CS1061 RED；当前5源码Unity AssemblyBuilder为0 errors/0 warnings GREEN。receipt equipment-conditional-compile-47f75577970748ccb645176bffa25239绑定DLL SHA 439AB76F6FCEB07D3EF4DDF76DC3DDB248142A9E96AF964A26750C981F98600D。Before后的独占delayCall尚未执行，先检查实际目录/编译状态与已完成RED，仅移除匹配本proof类型的1个待调delegate后显式启动未执行的current阶段，未清外部回调或重跑已完成阶段。PE元数据无加载检查再通过：prefix精确FormerlySerializedAs("Prefix")、Result Serializable标志及原12个public JSON字段名/顺序保持；不冒充Player或运行态JsonUtility验证。
- 新Native工具隔离：新增Get-HybridCLRReceptionScope.ps1供Invoke/Relocate/RunPlayer三入口复用。新nativePrefix必须是与Root叶名一致的YS两位数字、当前专项work及当前源仓库，Player/cache/baseline与三组compatibility均逐项绑定唯一新路径；拒绝回落H26/HC26/旧ProjectSubsets，拒绝链接/目录别名，保留缺少style标记的真实旧owner合同。Test-ReceptionOwnership先缺实现RED后GREEN，13个错owner/旧路径/旧身份负例及legacy兼容均通过，未启动Unity或Native。当前YS26专项缺少新prefix因此不能用新入口误跑旧Native路径。
- Tools三C#模板同步新scope：Bootstrap在Owned中独立复核新scope，Subset路径/身份和Release身份从经验证owner取得；Runtime Subset的原三硬编码const改为显式camelCase作者字段，Setup在自有新场景绑定，旧默认行为保留。Runner的ProjectId/CompatibilityId按真实作者字段迁为projectId/compatibilityId+精确FSA，Bootstrap强类型消费者及原匿名回执键保持；两项目辅助编译前后0errors，当前三模板expand/format预览0变化。仍待新接收真实编译/旧数据证明，不沿用YS26旧receipt冒充。
- 新root C:/Users/Kano/AppData/Local/Temp/kilo/YS27已在确认七个计划project/Player/cache路径及.meta全部不存在后真实创建owner，nativePrefix=YS27，short工程G:/Unity/YS27，Player为YS27D4/R/S，cache为YS27C/YS27CS，baseline均Root/Baselines下，三compatibility为本root独有ys27身份。正式Invoke Create实际exit0（有进程/exit/log），无上轮Create恢复流程；artifactPassed=False仅表示此步没有产品验收，不当产品通过。当前仅创建R，未导入最终包、未安装SDK/构建Native。
- 只读全范围条件分支审计完成（内部explore，不是T8独立Review），确认并已修三处ENABLE_IL2CPP错名：Behavior/Domain的const Backend与DataPlayerProbe._report；Domain两个.cs.txt内CanRelease/CanAdd/ShouldBeDisplayed及作者字段引用；Stage9/ResourcePlayer模板当前camelCase作者字段与LoadPanelRequest.Panel，保留JObject键和sample匿名ProjectId/UseBuiltIn回执键。iOS音频窗口mTouching改_isTouching，另发现同一禁用InputSystem区域9个private static readonly未规范，已仅改为下划线名并显式展开控制流，字符串反射API/顺序/查询次数保持。Unity6仍仅静态，未声称新平台实际构建。
- 模板实际编译首轮8源码4errors/1warning：三个真实named.panel错误已改为Panel；ResourcePlayerFileWatch.ToHexString为验证上下文错误，该文件实际由Invoke-ResourcePlayerAcceptance.ps1/Test-ResourcePlayerFileWatch.ps1用PowerShell Add-Type编译，不属于Unity。按真实入口分开验证，未为通过Unity删功能/换API；现已启动其余7模板的Unity AssemblyBuilder（YL_DOMAIN_PLAYER_PROBE），receipt目录acceptance-template-compile-e768967712684522aebdc8dd3433ce1c，等实际结果。FileWatch尚需PowerShell实际编译/自测；7模板及FileWatch其余命名/格式表面仍待收口。
- 独立默认最小流程只读检索已完成，不使用依赖XF/Game的UIAcceptanceEntry：优先复用ResourceStartup样例真实Build/Deploy/BeginBundles（Resource+Audio+Core）与CoreModulePlayModeTests.EmptyModuleRegistersClosesAndRestarts、ColorSelectorRenderingTests.FormalGraphicsRenderThroughStencilRectClipAndCanvasAlpha、SaveManagerFacadeTests的真实磁盘用例。Stage9可覆盖单Player Module/UI/Audio/Resource但无Save、模板表面待收口；不引入新测试平台或vendor shim。两只读子代理均结束，无活动子代理。
- 上述7模板实际Unity编译0errors/1既有CS0618（SpriteAtlasAsset.SetIncludeInBuild，后面已有SpriteAtlasImporter.includeInBuild=false），未抑制/伪报0warning；DLL未加载、未执行Authoring入口。manifest.json/files.json精确保存当前Sources/Defines/References，供后续一次性语义整理。Stage9三个named.panel编译错误真实RED后已改为Panel，回执字面量不变。ResourcePlayerFileWatch保留其.NET/PowerShell API，真实入口是Add-Type，尚待单独验证。
- 模板补充inventory-owned/inventory-complete返回0不是合格证据：临时IsOwnedSource原本仅接受Assets内.cs与明确Tools三模板，尚未纳入这7个Assets内.cs.txt。需要仅加入8个明确自有模板白名单后重盘点/整理，不能把未纳入白名单误算成零问题；不修改MCP权限或放开vendor/生成物。
- 已启动同一常规EditMode集合的独立新run，明确排除已独立1/1通过的TimerSessionTests（预计490项，实际数以本次新结果为准）；MCP超时，尚待读取native状态和实际结果。此期间不修改运行依赖，旧7模板编译、Gui 1/1或先前488/491缓存不替代本轮结果。
- 该run实际474/490，16失败中14项由Resource Atlas自有文件拖拽残留传播至Domain和ReorderableTreeView（空paths赋值未清Unity native状态），另2项为VS自动生成csproj共享冲突。新增DisposingAnOwnedDragLeavesNoNativePathsForTheNextTreeOwner真实RED，ResourceReleaseFixture.Dispose只在全部paths与object refs均属于本fixture时调用已确认Unity2022 private native Cleanup，保留所有拒绝外部拖拽的断言；补另fixture不接管当前owner负例。仅对已知本run/RED生成且已删除的两个具体GUID目录清过遗留native状态，未做泛化清场。修复后Atlas+DomainAuthoring+Reorderable组合35/35 GREEN。
- 8模板显式scope已纳入临时工具的read/apply白名单，不改MCP权限。七Unity模板27 private字段+5局部const+nullable HasPassed与7作者字段精确FSA已语义迁移，receipt匿名/JSON旧键保护。官方IDE0011加117组括号、官方using0变、显式418换行+官方格式前后辅助编译0errors。最后批写Stage9Panel遇用户映射文件IO错误：先核验7项实际before/after，仅1已完成、6未写；在已核验范围内用同卷Temp中间文件+File.Replace原子完成6项，没有重放已完成项/改meta。原子恢复收据曾存在于临时目录。
- 外部FileWatch在真实.NET9/C#9上下文迁移10private字段（disposed为_isDisposed），7组官方括号、15显式换行、官方format；ToHexString/JSON字段/线程及所有权顺序保持。通过原Test-ResourcePlayerFileWatch.ps1在全新自建独占self-test目录实际PowerShell Add-Type与FileSystemWatcher自测：10000次原子替换、2次正例明文探测、50002控制项不探测、Failure=null/Passed=true。不把该self-test描述成Unity接收或Player。
- 主1110文件最新完整Format Apply与Check实际exit0。此后发现C:/Users/Kano/AppData/Local/Temp/kilo下旧文件持续消失：语义工具bin空，随后Build.ps1/Program.cs及主图/映射缺失，七模板编译目录为空，YS27仅余R目录且owner/Create exit/log等控制文件缺失；原因未确认，不归因用户或具体进程。仓库源码完整，六保护blob再次逐项一致。停止使用这些目录作为当前可验证owner/新Native基座，不补写owner接管YS27。
- 真实阻塞：尝试给全新持久控制目录Build/HybridCLR/CodeStyleAcceptance-20261022/YS28建立owner时，工具被项目edit规则Build/** deny明确拒绝；整批未执行，已确认GetScope与Bootstrap没有新增durable放宽代码。没有通过shell/其他工具绕过该拒绝，未改权限/忽略/MCP配置。YS28七计划路径虽已查unused但尚未创建owner/工程。需要确定不会被清理且当前权限允许的新控制目录，才能安全继续新接收与Native长流程；历史结果不替代新门禁。T7/T8仍未完成。
- 上述目录阻塞已由用户实际选择“使用G盘新目录”解除：明确授权G:/Unity/YLStyleAcceptance/YS28，仅创建新验收数据。七计划路径/.meta复查unused后创建新owner；GetScope与Bootstrap只增加这个精确G路径，持久模式强制style owner，不改Build/**规则或权限。YS27不补owner、不接管。新YS28正式Create exit0、正式默认/可选Export/Import各exit0、官方9.0.3 Resolve exit0，独立Audit-ProductImport.ps1确认8个实际DLL及SHA/引用，无XF、source DLL、tests/consumer补依赖，证据supported-product-import.json先于所有consumer安装。所有后续控制数据持久保存在此G目录。
- 依正式InstallHarness安装当前3模板后实际编译exit0；Relocate物理复制413文件至全新G:/Unity/YS28，逐文件hash/meta一致，owner记录真实history与新G cache。官方Native runtime install、Configure、CompileExternal、Setup及新compat/cache/baseline绑定均完成；异步外部编译强制清理与正常exit分别记录，不冒充干净进程退出。
- 冷工程第一次基座产品阶段至ResourcePublish均成功，Deploy失败因遗漏文档明确的StreamingAssets父目录准备。只读诊断证实非命名回归；先保留17个失败workspace/stage文件与hash，再调用原PrepareDeployment（正式Recover后AssetDatabase创建标准父目录），未改deployer算法/原子所有权。中间一次Prepare因最小测试拷贝漏YLCoreSmokeView/Panel而编译失败，补两个真实fixture及.meta，不建stub。
- 独立默认流程现已复用真实现有tests：实际UI stencil/RectClip/CanvasAlpha渲染+SaveManager真实JSON磁盘往返/异步facade共3/3；真实Module注册/关闭/重启PlayMode1/1。输出default-ui-save-tests.xml与default-module-test.xml均原生Passed/无跳过。工具初次GUI调用未等待、另一次结果写入时共享冲突，XML和日志已完成后只清理精确owned PID50704/48804；进程真实exit未知/null、forcedCleanup=true如实记录，不描述成clean exit0。主工程依赖未改，新接收测试包1.3.9。
- 新Development产品报告build-native-development-after-preparation.json实际14阶段（包括Player/Baseline）全部成功；观察者在随后缺product-selection.json时失败。没有重建/改写已提交基座：从真实完整产品报告、已部署catalog bytes SHA、实际可选包首导入digest、实际IL2CPP.exe/GameAssembly以及baseline重建描述性选择/回执，注明OriginalObserverProcessFailedAfterProductCommit，恢复步骤未修改任何Player/baseline。v1 CodePackageId=e524aeb202454e24ed55283a020fd8f723abf0abe8554e62c6a63e4cccb4cdcf，Catalog=fec851ba63b0760b8f67a7bb02a4dc5c289856c5207815c5a0d804706955d960；整个Player指纹e19c667ff2204a921359d5eab0086f4e86054936be23b0569c2e34a7c852ea5a，baseline SHA e5b2044606d77c2767e9b382e650f2e8dce4740bf8a0667f5dc6596272a1c0cb。
- 新Native Development三个实际运行均exit0/Simulation=false/全Player before=after：native-v1d真实v1 message；CompileExternalVTwo与Patch在冻结基座上发布v2；独占ServerV2 loopback HTTPS精确pin/签名，不改系统信任，native-signed-update通过Check/Prepare、Activate Busy、释放后Activate、同进程换版拒绝；native-v2-restart重启同EXE加载v2。Server已发送其专有.stop信号，后续须读取server统计/确认无遗留。
- 独立Release Low：ConfigureRelease、真实CompileExternalRelease、Build均完成；build-native-release-low.log实际exit0。一次误用不存在CompileExternalReleaseVTwo被原入口拒绝，不计通过；一次误用-Release参数也未启动Player。正确-Variant Release -Action V2的native-release-v2实际exit0、Simulation=false、Player全文件不变。不要重跑已通过基座。
- 新A/B共享基座：首次PrepareProjectSubsets因旧Assets/HCLRExternal DLL与新真实外部DLL SHA不一致被严拒；用原RefreshExternal同步仅新receiver参考DLL后，PrepareProjectSubsets真实exit0。BuildProjectSubsetBase与PublishProjectSubset均实际exit0，均使用本root独有新Player/cache/compat/baseline；native-shared-project-subsets实际exit0、两个Native loads/同Support Assembly/两边barrier释放/停止/Player与baseline不变均由原runner验证。三组新Native主门禁已实际完成，仍需补齐Plan要求的其余负例/默认Resource+Audio样例完整流程、正式Sync/meta/最终范围核验与T8全量独立Review，未宣称整体完成。
- 2026-10-23续作只读复核：HEAD仍bfaeb949f8816b345be5d5ed269e4850a6ab740d/integration/xf-yl、index为空；唯一作者Editor PID2944及两个ImportWorker，实际非Play/编译/导入、scriptCompilationFailed=false、原UIAcceptance clean，options保持。ServerV2 tracked进程PID61408已退出，最终7次accepted requests/0fixture errors。真实Update三项判断均true，新v2 CodePackageId=bf7da9caf949d711320717254a5775db1e017b3d867ee8ef3171ee1d3d23d6ea、Catalog=940a5d89ca277c24db6e1892cd647e27ed176fbf48f04e8325e32923cb0e8976，与patch及重启receipt一致；A/B supportAssemblyCount=1、两边barrier/stopped=true。未重建通过的基座。
- 默认Resource/Audio新receiver验证首次实际业务Passed：Create/Build/DeployBuiltIn/BeginBundles，原生bundle被观察到、Core/Audio启用且播放已开始、资源与绑定全部释放，证据default-resource-audio-business.json。但新验收适配器误对TestRunner初始空SceneSetup调用Restore，OneTimeTearDown真实失败，XML根为Failed(Child)，不能以passed=1/failed=0或进程exit0计整套通过。仅修适配器的空setup处理，并修自有等待脚本不把未完成的空XML当完整结果、强制检查根result；保留首轮XML/log/receipt，用全新v2 run复验，不改产品。
- 上述限定适配器修复后default-resource-audio-tests-v2.xml根与fixture均Passed，实际1/1、无失败/跳过，进程exit0且ForcedCleanupAfterResults=false/NoGraphicsFlag=false。独立业务receipt default-resource-audio-business-v2.json确认Bundles非Simulation、真实Sample Project/Scene/部署catalog、Core和Audio启用且播放开始、原生Bundle出现后全部卸载、Resource/Core/AudioProviders归还；样例资产保留在新receiver作为验收数据，未改源工程/旧Player/冻结baseline。至此独立默认Module/UI/Save/Resource/Audio流程均有各自真实范围证明；前三项进程强清理限制仍沿用§514，不合并伪称全部clean exit。
- 当前Native完整audit先因缺native-corrupt-payload正确RED。新增receiver-only PrepareDevelopmentNegatives入口只将可写Profile/recipe与generated payload恢复Development，通过原PublishPatch完成6阶段，nativeStages为空；不重放Patch的签名/固定成功receipt，不触碰任何冻结Player/baseline。正式Sync后三个meta Git blob仍62dc86fbce651b92841b9b046d175f261c154ff9/6ef2c2783771d27a20d857cd6d92e5263451d7ca/81d98222e2bcc9a51b41370fe4443886f83109f3，真实Unity编译与该入口exit0，Player/baseline原SHA保持。
- PublishBadPayload/PublishMissingDll/PublishBadMetadata三独立正式入口均exit0且finally恢复工作输入。冻结Development Player的native-corrupt-payload/native-missing-dll均在Payloads阶段拒绝、无RequiresRestart、exit0且Player全文件不变；native-partial-metadata真实zzAbsentAot nativeErrorCode=3、RequiresRestart跨新loader保持，exit0/Player不变。ServerBad及ServerMissing各7次请求/0fixture errors，均已收到自己的stop并退出。
- 新native requirement负例通过真实外部DLL编译与RejectNewRequirement，后者exit0并证明Player/baseline不变；外部源码按精确v2备份恢复、重编译。两次异步AssemblyBuilder产物passed但Editor强清理/exit=-1，未称clean exit0。单bit错误签名在真实Player以Release signature rejected/exit21拒绝且Player未变；ServerBadSignature最终1次请求/0fixture errors，已stop退出。最终Audit-HybridCLRNative.ps1 -RequireRelease实际GREEN，native-final-audit.json绑定当前Dev HTTPS/重启/全部负例/Release Low及原Player和baseline指纹；A/B仍由单独subset receipt证明。G5技术门禁已完整通过，未宣称T7/T8其他门禁完成。
- 最终七Assets Unity模板真实编译证据final-acceptance-template-compile-v3.json：当前七源SHA逐项绑定，Unity2022.3.62f3c1、NET_Unity_4_8/Assembly-CSharp-Editor实际引用闭包，errors=0/warnings=1（既有SpriteAtlasAsset.SetIncludeInBuild的CS0618），DLL SHA087f3fbd0807efc219d606b5f5363e9139e80101db852bd9b3bc18dd70a4a402；compileOnly=true/assemblyLoaded=false/playerRunClaimed=false。前两轮scratch误混AssemblyBuilder默认NetStandard shim与实际4.8 facade而CS1703，保留失败证据；按工程既有FrameworkAssemblyIndependenceTests模式精确exclude非context默认引用后通过，未修改产品或弱化诊断。最后1110文件Check exit0、9组格式自测全通过、git diff --check exit0（仅既有.mcp CRLF提示）；六保护blob仍原值。
- 新广域常规EditMode完整native RunFinished已落盘final-regular-editmode.xml/.json：2026-10-23T05:53:27Z至06:02:57Z，1011/1045、34失败、0skip，不能称全绿。范围包含Foundation/Core/CoreEditor/generalYL/Help/Editor/Extensions/DataAssets/Http/ResourceEditor，单列原会话及动态生成前提组。34失败为一个EquipmentSettingsAdmission场景保护及33个ResourceGUI/Editor闲置前提/焦点交互；当前主线程与四个Resource guard均1、编译/导入/Play=false、场景clean、drag与hot/keyboardControl为空，尚不把当时失败机械归为环境噪声。EquipmentSettingsAdmissionTests.NewSetterCannotAdoptAnUnownedAndroidChange在当前clean前提独立真实1/1，未改代码或场景；其PlayerSettings所有权测试需和GUI构建组隔离，后续取整个ResourceGUI组当前结果。上轮定时等待后未接续导致中断，未宣告计划完成。
- ResourceEditor全组二次实际71/104、33失败，final-resource-gui-editmode.xml/.json完整保留；没有据定向绿色掩盖整组。随后同一Atlas失败方法独立1/1、ResourceWindow实际GUI完整流程独立1/1。只读native callback最小顺序复现：仅运行ResourceOutputSafetyTests.LiveDuplicateAssemblyIsRejectedEvenForByteIdenticalGeneration与Atlas WidePanes，前者Passed后isCompiling从false变true，后者开始/结束均true且被原ValidateEditor拒绝；线程始终1、isUpdating/Play均false（generated-source-gui-ordering-trace.json，实际1/2）。该输出安全测试在Assets真实生成并导入asmdef，编译在当前UTF运行结束前被延期；不是框架门禁或布局回归。
- T7 Ruling: 将ResourceCodeGenerationTests/ResourceOutputSafetyTests与需要settled Editor的ResourceGUI完整组分开运行，纳入原有动态生成隔离约定；不删除测试、不放宽编译/构建门禁、不吞异常、不改IDE或生成csproj。代价若判断错误：只能证明分组通过，不能证明任意测试调度顺序兼容；保留整组失败与最小复现，最终报告明确分组边界。所有动态生成测试仍需各自完整真实结果。
- 实际非生成器ResourceGUI完整91项于06:56:16Z至07:12:12Z完成（955秒、真实Bundle构建，不是hung）：88通过/3失败/0skip，final-resource-gui-isolated.xml/.json。两项分别为DisabledCustomDrop与CopiedAtlasConfiguration，在VisualStudio2.0.28 ProjectGeneration同步写Assembly-CSharp.csproj产生sharing violation，有完整外部包异常栈；未改IDE或弱化LogAssert。第三项OlderAttemptAlwaysMarksExactAnalysisConservativelyStale真实RED：测试反射ExactReportStale未同步实际IsExactReportStale。仅精确改该测试字符串、NotNull/True断言不变，随后三原失败方法同组真实3/3 GREEN。记录为88/91后3/3，不伪造一次91/91整套全绿。
- 从原完整native XML final-resource-gui-editmode.xml按真实test-case节点重读生成器两个fixture：ResourceCodeGeneration10项与ResourceOutputSafety3项共13/13 Passed；原含GUI整次104的根仍Failed。生成器源码此后未改，未为制造全绿重复跑或弱化其所有权/IO拒绝断言。
- T8唯一fresh-context全量独立Review已由内部general只读代理ses_e9863703bffejPAwWSCjqrGrNE启动（无AgentManager/新worktree/model override），与最终GUI验证只读重叠，不并行操作Unity/修改代码。初步提出SerializedFieldMigration数组size与variant-only元素两边界；尚待完整报告、分级及真实旧Variant RED/GREEN，一律不先写通过或忽略该发现。主代理只作上述明确测试字符串修正；产品代码尚未进行Review修复。
- 唯一完整Review已返回：0 Critical、3 Important（Excel旧列/ID，Array.size，Variant-only元素）、2 Minor（退格lambda、Resource加密示例旧成员）。Reviewer完成89脚本移动的完整meta指纹配对与跨域token/控制流筛查，未逐行通读全部1110源，也未操作Unity；六保护blob、程序集边界、Scope/Native receipt均按报告范围检查。Declined项为未安装平台实际运行、仓库外所有消费者、冻结二进制真实性、秘密配置、VS锁进程级处置、后续修复；不将它们伪装为已独立验证。
- Final Ruling: 两个Minor在本任务中升级为必须收口的规格问题，而非额外美学优化：多行lambda缩进属于本轮四空格/结构可读性产物，旧C#成员示例不能作为当前可编译用法。仅做对应空白/文档修正，不再派第二轮Review；代价是增加一次有token守卫的空白批次及必要受影响验证，不引入产品格式化器。
- Review修复RED：新增真实旧Variant空基座→新增元素/Array.size×冲突四例均在未迁移/未报冲突处失败；新增Buff旧ID工作簿资产更新与Buff/Skill/Equipment旧表头导出四例均因Missing ID column id/旧键缺失失败，真实xlsx/非模拟。限定修复后数组新旧8例8/8、Excel新增4例4/4；随后BehaviorAuthoring/CoreSerializedProperty/ExcelAuthoring/ExcelAssetAuthoring/DomainAuthoring完整相关集合102/102、0skip。
- 数组修复保持显式、原子冲突预检与target/value/objectReference不变：允许集合末端Array.size，字段需真实Unity序列化资格；集合子路径按类型及已序列化集合根验证，不以源Prefab数组长度否决Variant新增元素。旧Variant重导入/保存、引用、meta/GUID/fileID与幂等均由新增真实用例覆盖。
- Final Ruling: Excel稳定列名只对YL Runtime中Skill/Buff/Mission/Equipment/DataAssets的本轮迁移字段读取唯一精确FSA旧名，普通列/ID查找/导出共用ColumnName；基线这些领域不存在旧FSA多重链，且当前别名唯一。外部模型保持原FieldInfo.Name规则，未扩大用户FSA为Excel协议；不新增Runtime属性/依赖或修改JSON序列化策略。代价是这些自有别名成为冻结工作表契约，未来多重别名需显式协议决策而非静默任选。Architecture记录长期约束，真实旧buff_id更新保持同一子资产GUID/fileID。
- 退格lambda一次性整理不发布第二格式引擎：复用原Format-YL正式1110文件白名单，Roslyn C#9对7组Editor/Player/IL2CPP/输入及静态平台符号逐文件检查，只插入行首空白，守卫token/注释/字面量/directive不变。review-lambda-indent-audit-v3/apply-v3.json记录134文件/338块；audit-v4再审为0候选。仅语法/空白证明，不冒充Unity6/移动端编译。早期辅助collector的转义错误被1110数量门禁拒绝（无源写），改为直接消费正式白名单；另一次临时snippet因ExCSS的Tuple冲突未编译，未改依赖。134文件无meta/资产写入；正式Check再次1110通过、9组薄入口自测通过、git diff --check通过，UTF8无BOM/LF/单EOF逐字节1110检查0问题。
- 当前文档示例project.contentEncryption/contentKeyId与真实字段同步；AssetMetadata当前C#字段与旧Unity键区别明确，CodingStyle说明官方formatter不保证多行lambda相对缩进，未将Check当完整结构验收。
- 当前默认包由正式ExportUnityPackage.Export导出到全新YLFramework-StyleReview.unitypackage，SHA04690a7fd81abd61dddbb8ec3beb08aaeeecc76b27c46896687d297a0f3955b2（4233175字节），未SaveAssets/保存场景/覆盖原包。仅为保留原包，Invoke工具末尾追加严格叶名BasePackage参数（默认和原位置参数均不变），穿越负例在绑定时拒绝。import-review-fixed-base实际导入/编译/进程exit0且无强清理；包装器随后正确拒绝已装HybridCLR/test设施的“首次无包导入”断言，此次不记为新的product-only proof，原supported-product-import仍是首次证据。之后正式verify-review-fixed-base-reuse真实exit0，完整报告/冻结指纹将继续读取，不因“纯排版”自行豁免原规则。
- 已读verify-review-fixed-base-reuse-frozen.json：passed=true/nativeStages=[]，PlayerFingerprint仍e19c667ff2204a921359d5eab0086f4e86054936be23b0569c2e34a7c852ea5a，baseline SHA仍e5b2044606d77c2767e9b382e650f2e8dce4740bf8a0667f5dc6596272a1c0cb；实际当前包编译后由原PublishPatch/ValidateBaselineBinding/ValidatePatchSet验证，无Strip/Player/基线重建。新候选CodePackageId=4cbfa5da004df5889e923c0aa1352d099fd11c34074e963b4a194174c69703a6、Catalog=62d02592c2b6f12f8cda621355e81480f67b4dab045bd49577f6d7eae3241dde；未替换原ServerV2/签名更新回执或原三组实际native结果。
- Final Ruling: Review之后Runtime仅行首空白变化，7组token/注释/字面量/directive及行数不变，公开签名/初始化/IL逻辑输入未改变；功能修复在Editor/Excel Editor与测试/文档。根据实际原Native规则（设置指纹不含源码空白，Patch检查冻结登记/AOT依赖/原生签名要求）及上述正式复用成功，沿用本轮YS28已完成的Dev HTTPS/Release Low/A-B与负例运行范围，而不是归类为可复用更早bfaeb94前API基座。代价边界：静态签名检查不保证任意动态泛型；未追加移动/Unity6或额外全矩阵承诺。
- 当前包独立默认业务再次真实完成：review-default-ui-save.xml根Passed 3/3、review-default-module.xml根Passed1/1；各自.exit.json明确ActualExitCode=-1/ForcedCleanupAfterResults=true，保留限制，不称clean exit0。review-default-resource-audio.xml根/fixture Passed1/1、business.json绑定全新startup-example-f0394e6c/实际原生Bundle与Audio启动/Core资源归还；原observer错过XML最终Changed事件并超时，Native随后正常完成且PID17160已自然消失，未回收它，OS ExitCode未捕获，不从日志0伪造OS0。仅修自有等待脚本在超时或进程退出时再取最终完整XML快照，不改产品/断言，不重跑已完成业务。
- 338块空白整理后的当前主工程实际Unity编译成功；原Prepare已按最新MVID/hash更新仅自有HybridCLR测试夹具。常规HybridCLR EditMode完整123/123（MCP超时后读取native完整缓存确认，不重跑），两个Session方法各独立1/1，HybridCLR PlayMode25/25；随后五个相关fixture完整集合再次102/102、0skip，包含所有8个Review新增旧数据回归。无新增产品修复进入第二轮Review，唯一Review的五项均已按各自证据收口。
- 最终补齐当前FrameworkAssemblyIndependence/ExportUnityPackage/HybridCLRIntegrationExport完整组17/17、0skip，覆盖Editor与Windows Development源码闭包及实际导出检查。完成后只读确认原UIAcceptance单场景clean、非Play/非编译/非导入、无native test active、Mono2x/非Development/optionsEnabled=false/options3。
- 自有Excel RED夹具仅对精确Assets/YLExcel-14a7cadf519048c69c1dd3191d9ad267核验4个预期文件、Owner CLR类型及Buff id7/previous原值/无dirty后，归档为review-excel-red-fixture.unitypackage，再用AssetDatabase删除该唯一自建目录；未接管其他失败历史目录或用户Assets/Test。
- source-session-restored.json明确恢复Audio原_isQuitting=true并清唯一T5 SessionState键，无live Audio host、未保存场景/资产/ProjectSettings；17项最终组后再次实际读回仍true且键已移除，原场景/options/backend不变。六保护blob与T0一致，index为空，HEAD/分支未变，未创建分支/worktree、Commit、Push或远程部署；YS28验收数据与所有旧冻结制品保留。Dependencies.json的已知nativeValidated=false声明差异按§1.1保留，不因专项样例证据盲改能力声明。
- 最终交付入口为本Plan、CodingStyle.md及模块/Architecture更新；当前新默认包为YS28/YLFramework-StyleReview.unitypackage。T0–T8和G0–G5技术范围收口；未验证Unity6/移动平台、仓库外所有消费者、可闻音频或任意动态泛型安全，不把静态/编译/测试/Native/批处理OS退出证据互相替代。本任务到此停止，不开启下一轮重构或第二轮审查。
- 后续用户要求将规范上移框架根并提供AI编程说明：CodingStyle.md经AssetDatabase保原GUID af0ff0e561630f34f8e69387e0233953移到Assets/YLFramework，原文档地址保留单一跳转；新增框架根AGENTS.md，后按用户要求压缩为项目导航/工作方式/验证说明，并明确维护和新增代码均遵守CodingStyle。仅显式添加两份根文档到正式导出清单，不扩大到整棵框架根。导出回归先因缺两根RED，更新后完整6/6通过；文档后续精简只检查内容/链接/编码及差异，未冒充重跑运行时。
- 2026-10-23用户明确要求本批Git收口，授权在现分支创建本地提交；不扩展为Push/合并/切分支。提交范围为本专项YL源码、模板、测试、规范/AGENTS、必要Tools/.editorconfig及本Plan；排除T0用户保护文件、Assets/Test、历史孤立meta与本机生成资源。提交前复核status/diff/log、完整1110格式Check与实际暂存内容；既有技术证据按源码影响复用，不为Git动作重跑Native矩阵。提交Hash与最终工作区状态在Git日志及本次交付回复报告，不另建收口文档或自引用Hash提交。
