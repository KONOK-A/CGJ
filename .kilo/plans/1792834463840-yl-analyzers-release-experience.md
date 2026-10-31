# YLFramework.Analyzers 发布体验增强计划

> 状态：用户已在 Q25 确认共同理解，并于 2026-10-24 10:38 UTC 明确要求实施。本轮除用户21:05 UTC明确延期的Unity6.3外已收口：实现、2022.3接收、VS/Rider实机验收、清理、说明与新包审计均完成。2026-10-25 03:15 UTC用户授权本批Git提交及一次推送；沿用当前分支，Unity6.3明确转入后续独立验收批次，完整双Unity版本矩阵仍未宣称通过。
> **For agentic workers:** REQUIRED SUB-SKILL: 使用 `executing-plans` 内联执行，逐任务先红后绿并记录证据；本文件同时作为实施进度台账，保护既有无关修改。

**Goal:** 让使用 YL 开发业务的开发者可靠发现 YL 专属误用，并方便地理解、配置和核查分析器接入。

**Architecture:** 保留现有编译期 DLL 与七条规则语义，增加三类受限诊断。使用一个无 Unity/Roslyn 依赖的只读规则目录连接描述符、Core 帮助和原生 Project Settings 页面；设置持久化仅使用 Unity `.ruleset`。

**Tech Stack:** Analyzer 保持 `netstandard2.0` / `Microsoft.CodeAnalysis.CSharp 3.8.0` / C# 9；独立测试沿用 SDK `9.0.312` / `net9.0` / NUnit。首发必须验证 Windows 上的 Unity 2022.3 LTS、Unity 6.3 LTS及 Visual Studio / Rider，记录实际补丁、IDE 和集成包版本。

**Spec:** 本文件的决策树、规则契约、配置行为和验收标准是本次完整规格。历史 Plan 只提供背景，不扩大本轮授权。

## Global Constraints

- 面向 YL 使用者，低误报优先；不建设通用 C#/Unity 规范大全，不以规则数量为目标。
- 现有 YL0001-YL0007 不重编号、不静默加严；所有十条契约规则默认启用、默认 Warning，项目可按 ID 调整。
- 不增加 Runtime public API、marker、依赖、所有权或生命周期；不升级 Unity、Roslyn、SDK 或既有包。
- 仅按真实 `YLFramework.Runtime` 符号及受支持声明/API 匹配；保留生成代码跳过、取消、并发与 compilation 隔离。
- 保留唯一 `Runtime/Core/Analyzers/YLFramework.Analyzers.dll`、原 `.meta`/GUID 和 RoslynAnalyzer 导入角色；Roslyn、Workspaces、测试宿主不进入 Player。
- 本批不交付 CodeFix、全路径/跨过程所有权分析、CI 平台、UPM 迁移、通用工作台、Source Generator 或消费者资产扫描。
- 继续 `.unitypackage` 交付；macOS/Linux、Unity 6.0及其他版本不属于本批强验收，不据 Windows 结果宣称全平台验证。
- 实施前重查工作区，保护已有修改和未跟踪夹具；未经另行授权不创建/切换分支，不 commit/push，不自动保存脏场景。
- 同一工程不启动第二个 Unity；6.3 验收使用独立接收工程，不原地升级当前工程。不具备环境时报告阻塞，不伪造通过。

## Review Focus

- 别名、派生 Inject 特性、伪同名类型和泛型约束必须保持真实符号边界；由任务 2-4 的正反例验证。
- 不完整代码、缺失可选类型和 source/metadata consumer 不得引起 AD0001/CS8032；由任务 2-4、7验证。
- private setter、普通 View、查询/纳管、正确音频轮询及泛型数据迭代器不得误报；由任务 2-4验证。
- 新局部 ruleset 不得丢失 Default 策略、Include、其他 analyzer、未知 ID或并行修改；由任务 5验证。
- DLL、显示目录、实际编译、IDE 配置及 Player 隔离是不同证据；由任务 1、6-8验证，不互相替代。

## 1. 已确认的设计树

```text
业务开发者正确、便捷地使用 YL [Q1]
|-- 可信护栏 [Q2/Q6]
|   |-- 默认低误报、默认 Warning，支持单 ID 配置与局部抑制
|   |-- 保留旧规则语义，优化说明与回归 [Q16]
|   |-- 三类新诊断 [Q9]
|       |-- 注入：只报结构性不支持，不猜配置/注册 [Q18]
|       |-- Panel：现用/旧泛型重载及 Panel 约束 [Q19]
|       |-- 音频：受限 IEnumerator 协程样式，不猜等待意图 [Q20]
|-- 可落地的规则配置 [Q8]
|   |-- asmdef 与预定义业务程序集都支持 [Q11]
|   |-- 原生 ruleset，保守合并，预览后显式应用 [Q12]
|   |-- 同页只读接入自检，不把零诊断当运行成功 [Q13]
|   |-- Project Settings > YLFramework > Analyzers + Core 快捷菜单 [Q21]
|-- 发布承诺 [Q4/Q5/Q17/Q22]
|   |-- Windows：2022.3 LTS + 6.3 LTS，VS/Rider 真实验证
|   |-- 保留 unitypackage，本地可重复门禁，不新建 CI/UPM
|-- 明确后置
    |-- CodeFix 独立批次；安全前提与 IDE 加载均先验证 [Q3/Q15]
    |-- MustCallBase 全路径完整性独立提案 [Q16]
    |-- 其他候选记录价值/边界，不混入首批
```

Q7、Q10、Q14 的问答被 Kilo 自动中断，不代表用户跳过、拒绝或同意，不作为决策证据。路径范围依据用户随后对 Q16 的明确答复，发布范围依据 Q17，用户在 Q24 明确保留这些选择。Q4/Q5 的最终选择是 2022.3 + 6.3，不采用其他推荐矩阵；用户已在 Q25 确认整体共同理解。

## 2. 研究结论与取舍

| 已确认事实 | 规划含义 | 源码依据 |
|---|---|---|
| 七条规则已有符号身份、存储模型、并发/取消、不完整代码与性能语料回归 | 不重写分析器或重复搭测试平台；复用并扩充真实缺口 | `Tools/YLFramework.Analyzers/Tests/{AnalysisBoundaryTests,PerformanceTests,CallRulesTests}.cs` |
| YL0001 只检查正确 base 槽调用存在性，测试明确允许部分路径调用 | 全路径检查是新契约，不是假装修复旧测试 | `Analyzer/CallRules.cs:13-49`；`Tests/CallRulesTests.cs:17` |
| 描述符没有 description/helpLinkUri，帮助有手工重复说明 | 统一只读展示元数据；补原因、正确写法、限制和本地导航 | `Analyzer/CoreUsageAnalyzer.cs:11-18`；`Editor/Core/CoreHelpProvider.cs:59-81` |
| Core 帮助仍写两个 owner 例外，而实际解析已有第三个 PanelOperation.Args | 定向修正文档漂移，不扩大 owner 豁免 | `CoreHelpProvider.cs:73`；`Analyzer/AnalyzerSymbols.cs:36-62`；`Documentation/Modules/Attributes.md:113` |
| 注入器仅扫实例，拒绝 readonly/无 setter/indexer，允许 private setter | 适合声明级精确诊断；不分析容器模式或注册完备性 | `Runtime/Foundation/IOC/Internal/MetadataCache.cs:60-118` |
| 泛型 View 加载用于 Panel 没有成功路径；不存在公开 LoadView(Type) | 只检查真实 Module 的泛型入口，不虚构 Type 重载或扫描 Prefab | `Runtime/Core/Modules/Module.Views.cs:81-269`；`MVC/Views/ViewManager.Loading.cs:96-100,335-338` |
| AudioPlayRequest 是 IDisposable 普通类，没有 Wait() API | 诊断解释 Unity 等待协议，不自动改成可能永不结束的等待 | `Runtime/Systems/Audio/AudioPlayRequest.cs:31,57,84`；`Documentation/Modules/Audio.md:161-169` |
| Unity 2022.3 实际 csc 不自动沿用 IDE editorconfig；现有 ruleset 与 UI 辅助缺失 | 用原生 ruleset，不声称 editorconfig 等同 Unity 配置 | `Documentation/Modules/Attributes.md:118-146`；IDE 包 ProjectGeneration 源码 |
| 构建收据在 Temp，当前 DLL 与当前源码一致性尚无本轮运行证据 | 发布时重新构建、比对资产和包，不断言当前 DLL 已过期 | `Tools/YLFramework.Analyzers/build.ps1:25-68` |

本节未带 `Assets/YLFramework/` 前缀的 Runtime/Editor/Documentation 路径均以该目录为根；Analyzer/Tests 路径以 `Tools/YLFramework.Analyzers/` 为根。

## 3. 诊断契约

### 3.1 保持既有规则

| ID | 保持的语义 |
|---|---|
| YL0001 | override 中存在同一 base 槽的直接调用；不证明全路径、顺序或次数 |
| YL0002 | RestrictedCall 的直接调用者；lambda/local 不借用外层身份 |
| YL0003 | WarnInClasses 的真实词法类型/基类/接口约束 |
| YL0004 | DisableNew 受管类型通过正式工厂创建；保留现有精确豁免 |
| YL0005 | MethodLocalOnly 的 field/property 与已支持持有形态；不变成泄漏分析 |
| YL0006 | ProtectedHierarchy 写入/可写 ref 暴露；合法读取不报 |
| YL0007 | 真实 View 派生遮蔽三个框架 Unity 消息；不禁止普通 MonoBehaviour 消息 |

每条描述符补充非空中文 description；消息保留 ID、具体符号、原因和可操作的改正方向。设置页与 Core 帮助复用目录，并链接随包已有的本地文档。不创建文档站，不生成带构建机器绝对路径的 helpLinkUri，不承诺 IDE 能直接打开本地 Markdown。

### 3.2 YL0008：不支持的 Inject 声明

- 匹配真实 `YLFramework.DependencyInjection.InjectAttribute` 及其派生特性；继承链必须最终到达真实 Runtime 符号，伪同名不触发。
- 报 static 成员、const/readonly 字段、没有 setter 的属性、indexer；每个成员至多一条，定位成员名或明确标注位置，说明具体不支持原因。
- private setter、非公开实例成员、命名注入均可合法；不推测 MemberSearchModes、注册、名称解析、运行时是否实际注入或值类型 owner。
- 指导显式 Resolve、构造注入或可写实例成员等替代，不删除 readonly、改 static、添加 setter 或抹掉标记以消警。

### 3.3 YL0009：View 入口加载 Panel

- 只匹配真实 Module 声明的 `LoadView<T>` / `LoadViewAsync<T>` 全部十个泛型重载，包括 Obsolete 重载；不因 CS0618 已出现而隐藏 YL 诊断。
- T 为 Panel/派生类型，或可沿有效类/类型参数约束证明其属于 Panel，报告 Warning；只有 View 约束、未知或错误类型不报。
- 非泛型 LoadView、同名业务方法、LoadPanel、GetView/GetViews、AttachView 不报；不读取 Prefab、Catalog 或 Type 变量。
- 分别推荐 LoadPanel / LoadPanelAsync，说明 parent 与 UIRootContext root 不等价，不提供机械重命名修复。

### 3.4 YL0010：直接 yield 音频请求

- 仅检查直接所属方法/局部函数返回非泛型 `System.Collections.IEnumerator` 的 yield return，操作数原始静态类型为真实 AudioPlayRequest；不能误用隐式转换后的 object 类型判断。
- 泛型 IEnumerable/IEnumerator 数据迭代器、object 擦除、伪同名类型、正确 WaitUntil/轮询、yield break 不报；不跨方法追值。
- 标准 Unity 协程不会把该对象当完成等待；自定义调度器可按 ID 局部抑制，文档明确该协议前提。
- 等开始：`new WaitUntil(() => request.HasStarted || request.IsDone)`；等结束：`new WaitUntil(() => request.IsDone)`。解释终态不等于自然完成，循环请求可能不结束，不替开发者决定等待目标。

三条新规则均沿用真实程序集自门控、默认生成代码跳过；不按 Framework/Editor/Tests 目录整体豁免。故意构造违规的契约测试使用窄范围 pragma，不能因此关闭整组分析器。

## 4. 配置与自检行为

1. 页面为 `Project Settings > YLFramework > Analyzers`，菜单为 `YLFramework/Core/分析器规则`；不改共享 Help 渲染器或 Resource 工作台。
2. 从 CompilationPipeline 的真实编译图选择引用 Runtime 的消费者。支持 Assets 内业务 asmdef，以及实际存在的四类 Assembly-CSharp 预定义程序集；Packages、框架自有程序集和不可写目标只展示，不由本工具改策略。
3. asmdef 对应其同目录唯一局部 ruleset；没有时建议 `<AssemblyName>.ruleset`。预定义程序集只写 `Assets/<预定义程序集名>.ruleset`。永不改 `Assets/Default.ruleset`，不新增 ProjectSettings/ScriptableObject 策略资产。
4. 每个 ID 提供“继承 / Warning / Error / 关闭”。继承表示删除该 ID 的本地覆盖，不等于强制 Warning；显示本地值、继承来源和能否核实最终值，无法计算时明确未知。
5. 已有单一局部文件只改已知 YL ID；保留其他 analyzer、未知 ID、Include/IncludeAll、注释和无关 XML。新建局部文件且存在 Default 时显式 Include 该文件，保留原策略而非逐项复制，防止后续 Default 变更断联。
6. Preview 先验证目标/继承并保存观察指纹。损坏/DTD/XML、不支持结构、重复冲突 ID、多份局部规则集、循环/无法核实的 Include、只读/越界/链接目标只读拒绝；不选择任意一个文件覆盖。
7. 用户确认预览后 Apply；重新核对文件、已读取继承文件及同目录候选集合，观察到冲突时文件不变，要求刷新，不将指纹检查声称为对任意外部写者的 CAS。写入保持原 meta/GUID；在工程 Temp 内同卷暂存后替换，不暴露临时 Assets 文件，失败清理自身临时文件，不自动保存其他资产。
8. Error 有明确编译阻断提示。应用后只导入受影响文件、请求必要的脚本重编译；界面区分已写入/等待编译/编译完成或失败，提供文件定位和将本地 ID 恢复继承/Warning 的入口；文档说明 UI 不可用时如何手改该 ruleset 恢复。
9. 自检只读检查唯一 DLL、版本/指纹、RoslynAnalyzer 标签、禁用平台、当前编译图中的 analyzer 路径、程序集与规则集位置。不可取得的信息显示未核实；配置检查通过、编译完成、零诊断都不等于“规则已实际执行成功”。
10. 自检手动刷新并在必要生命周期更新，不每帧全工程扫描，不自动生成违规脚本或复制 DLL。真实正反诊断见证仅由测试在自有临时范围完成。

## 5. 实施任务

步骤中的 SDK 命令统一为：`pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/YLFramework.Analyzers/build.ps1"`。每个任务先建立并观察相关失败测试，再最小实现，再运行相关与既有回归、审查 diff；不因本计划默认创建提交。

### Task 1：统一规则说明与展示目录

**Files:** 新增 `Assets/YLFramework/Editor/Core/Analyzers/AnalyzerRuleCatalog.cs`；修改 `Tools/YLFramework.Analyzers/Analyzer/{CoreUsageAnalyzer.cs,YLFramework.Analyzers.csproj}`、`build.ps1`、`Editor/Core/CoreHelpProvider.cs`；新增 `Tools/YLFramework.Analyzers/Tests/RuleCatalogTests.cs`。

**Interfaces:** 目录 namespace 为 `YLFramework.Editor.Core.Analyzers`，只含 internal 不可变规则元数据（ID、标题、类别、中文说明、本地文档锚点），不含 Unity/Roslyn 类型。Analyzer csproj Link 编译该源文件，两个程序集不互相引用。保留 `CoreUsageAnalyzer` 类型及既有 Rules 顺序；本任务先仅七条，后续任务各追加自己的 ID。

- [x] 写 `ExistingRuleCatalogPreservesIdsCategoriesAndDefaults`，断言 YL0001-YL0007 顺序/类别/Warning 不变、description 非空；保留所有旧诊断位置与语义测试。观察新增断言失败。
- [x] 实现共享元数据、描述符说明和 Core 帮助复用，修正三个真实 owner 例外的说明；不重新实现 StorageModel。
- [x] 把共享源纳入既有构建 Inputs 指纹；运行 SDK 全回归与帮助检索测试，核对 Analyzer 不新增 Unity/Editor/Workspaces 程序集依赖。

### Task 2：YL0008 注入声明

**Files:** 新增 `Analyzer/InjectionRules.cs`、`Tests/InjectionRulesTests.cs`；修改 `Analyzer/{AnalyzerSymbols,CoreUsageAnalyzer}.cs`、`Tests/AnalyzerFixture.cs` 和共享目录。上述 Analyzer/Tests 路径均在 Tools 项目内。

**Interfaces:** `InjectionRules.Member(SymbolAnalysisContext, AnalyzerSymbols)`；AnalyzerSymbols 独立解析真实 Inject 符号，仅符号存在时注册。Fixture 复用正式 `Runtime/Foundation/IOC/Attributes/InjectAttribute.cs`，不手写替代标记。

- [x] 写 static 字段/属性、const、readonly、getter-only、indexer 的 YL0008 正例；写 public 实例、private setter、非公开成员、派生 Inject、别名、伪同名、错误代码和生成代码反例，断言单条 ID/位置/原因。先观察缺少诊断失败。
- [x] 按 §3.2 实现声明检查，处理显式 field-target 标记与重复原因，不引入容器数据流或改 Runtime。
- [x] 运行 SDK；补 source/metadata、并发与缺少 Inject 类型的回归，既有七条测试保持通过。

### Task 3：YL0009 Panel 加载

**Files:** 新增 `Analyzer/PanelLoadingRules.cs`、`Tests/PanelLoadingRulesTests.cs`；修改符号解析、注册、Fixture 与共享目录。

**Interfaces:** `PanelLoadingRules.Invocation(OperationAnalysisContext, AnalyzerSymbols)`；只使用真实 Module、Panel、目标方法和类型参数语义，不按调用文本判断。

- [x] 参数化覆盖十个公开泛型重载，断言具体 Panel、派生 Panel、直接/间接 Panel 约束均产生 YL0009。普通 View、View-only 约束、查询/AttachView/LoadPanel、非泛型和伪 API 均不报；先观察失败。
- [x] 按 §3.3 实现同步/异步入口匹配；Fixture 是最小签名载体，任务 7 必须用真实 Runtime 再证明，不能只信 stub。
- [x] 运行 SDK，检查 Obsolete 与 YL0009 能共存、不额外产生编译错误或重复诊断。

### Task 4：YL0010 音频协程

**Files:** 新增 `Analyzer/AudioRules.cs`、`Tests/AudioRulesTests.cs`；修改符号解析、注册、Fixture、共享目录和 `Tests/PerformanceTests.cs` 的受控语料。

**Interfaces:** `AudioRules.Yield(SyntaxNodeAnalysisContext, AnalyzerSymbols)`，注册 YieldReturnStatement；查最近直接所属方法/局部函数的返回符号和表达式原始静态类型。

- [x] 写非泛型 IEnumerator 内直接 yield 参数/局部/调用结果的 YL0010 正例；泛型数据枚举、object 擦除、WaitUntil、无关请求、局部函数隔离和生成代码不报。验证隐式 object 转换不会导致漏报；先观察失败。
- [x] 按 §3.4 实现受限检查及不猜等待意图的中文消息；不添加 Wait API 或 CodeFix。
- [x] 运行 SDK 全回归；性能语料加入新规则的命中/非命中，保存冷/暖耗时与分配对照及正确诊断数。沿用现有测量设施，不加不稳定的绝对耗时断言。

### Task 5：安全的 ruleset 预览与写入

**Files:** 新增 `Editor/Core/Analyzers/AnalyzerRuleSetFile.cs`、`Editor/Core/Analyzers/AssemblyInfo.cs`、`Tests/EditMode/Editor/Core/AnalyzerRuleSetFileTests.cs`（均位于 Assets/YLFramework）。复用现有 Core Editor 测试 asmdef，不增程序集或依赖。

**Interfaces:** `AnalyzerRuleSetFile.Preview(string projectRoot, string assemblyName, string asmdefPath, IReadOnlyDictionary<string,string> changes)` 返回内部 `RuleSetPreview`；asmdefPath 为 null 表示预定义程序集；action 为 null 表示继承，否则仅 Warning/Error/None。Preview 含目标、本地配置、原/新文本、阻塞原因与内部观察指纹。`Apply(RuleSetPreview)` 重新校验后写入，不依赖 Roslyn。新增 Editor AssemblyInfo 仅向既有 `YLFramework.Core.Editor.Tests` 开放 internal 测试访问；现有 Action 下的 friend 只面向另一测试程序集，不改那个文件，不为测试扩大 public API。

- [x] 在测试自有临时工程写路径映射、无变化不建文件、单 ID 修改、继承/显式 Warning 不同、Default Include 保留及未知内容保留的失败测试。
- [x] 补多文件/重复 ID/DTD/坏 XML/循环或无法核实继承/只读/越界、预览后文件或目录集合变化的拒绝测试；拒绝时所有相关原文件逐字节不变，不修改真实业务规则集。
- [x] 实现 §4 的最小 XML 层；新建文件显式 Include Default，既有文件不擅自增加或改写继承。处理真实文件 I/O 失败，不宽泛吞异常或重建未知文件。
- [x] 当前 2022.3.62f3c1 运行完整 Core Editor 测试，并用真实 csc 证明 Include 与局部覆盖语义；6.3 对应验证移至 Task 8，仍未完成，不能仅凭 XML 测试宣称双版本配置已生效。

### Task 6：Project Settings 与只读自检

**Files:** 新增 `Editor/Core/Analyzers/AnalyzerRuleSettingsProvider.cs`、`Tests/EditMode/Editor/Core/AnalyzerRuleSettingsTests.cs`；定向更新 `Editor/Core/CoreHelpProvider.cs`、`Tests/EditMode/Editor/Help/FrameworkHelpProviderTests.cs`。

**Interfaces:** SettingsProvider 路径和菜单按 §4；仅消费共享规则目录、CompilationPipeline、原生 Importer 与 RuleSetPreview/Apply。状态检查保持同一功能文件中的私有逻辑，不引入配置单例、第二套状态数据库或可执行帮助扩展。

- [x] 写页面路径/菜单唯一性、规则清单十条、业务/框架/Packages 目标区分、predefined/asmdef 映射和失败状态的测试；先观察失败。
- [x] 实现“选择 → 编辑 → 预览 → 应用”，保留继承状态，Error 明示风险，冲突不写；应用后只重导入必要文件并请求必要重编译，不保存场景。
- [x] 补缺 DLL、错标签/平台、重复 DLL、不在作用域、编译中/失败、零诊断状态测试，断言没有任何状态将“已保存/零诊断”标为实际分析成功。
- [x] 运行 Core Editor 与 Help 回归；在隔离 EditorWindow 中实际绘制 Provider、验证真实接入事实且不写 ruleset/不脏场景；空/只读目标与失败恢复由契约测试覆盖。新脚本/目录由 Unity 生成 meta，保持 Editor-only，无 asmdef 依赖变化。

### Task 7：真实 Unity 与导出门禁

**Files:** 修改 `Tests/EditMode/Editor/Core/CoreEditorIdentityTests.cs`、`Tests/EditMode/Editor/Distribution/ExportUnityPackageTests.cs`、`Tools/YLFramework.Analyzers/build.ps1`（仅部署前置/收据所需改动）；部署唯一 DLL，保留原 meta。

**Interfaces:** 沿用现有 `AssemblyBuilder` 临时见证、导出包 tar 读取与 SHA 校验，不新增测试框架或修改 Export 的公共行为。SDK 构建、Unity 导入、包验证分别有结果。

- [x] 保留旧七条真实见证及原 XF/Logger 诊断；增加三条新规则的真实 Runtime 正反见证，不关闭第三方规则。断言精确 ID、Warning、无 AD0001/CS8032。
- [x] `build.ps1 -Deploy` 前拒绝缺失/错误的预期 meta，不静默生成新身份；收据包含共享源指纹。SDK 失败不替换旧 DLL，部署前后保留现有合作锁与目标指纹保护。
- [x] 复用既有导出测试已提供的包内 payload/hash、唯一 DLL、标签/平台/GUID 验证，不重复编写 tar/hash 框架；仅新增共享目录与设置源码随 Editor 根导出的断言，并将 SDK 收据 DLL hash 与部署目标对照，无 Tools/SDK/测试宿主混入包。
- [x] 重建并部署，复用当前 Unity Editor 运行新增与受影响 EditMode 组。记录 SDK TRX、收据与实际 Unity 结果，不把历史记录或源码比对记为本轮通过。

### Task 8：双版本接收验收与交接

**Files:** 增量更新 `Documentation/Modules/{Attributes,IOC,Audio}.md`、`Documentation/README.md` 和本 Plan 验收记录；仅规则例子确有需要时同步 `Modules/UI.md`。不新建重复报告或文档站。

- [x] 同一最终包在 Windows / 2022.3.62f3c1 干净接收工程完成首次无harness导入编译与独立审计，记录包hash与最小UGUI/Json.NET依赖；未复制源manifest或原XF/MCP/商业插件。
- [ ] 后续独立批次：Unity6.3对应接收与全链路验收（含该版本生成项目后的VS/Rider验证）。用户于2026-10-25 03:15 UTC确认不阻塞本批Git收口；当前机器没有6000.3安装，未执行、不用6000.0代替。
- [x] 当前2022.3接收工程重启后执行8个真实编译检查，覆盖asmdef/预定义自动发现、正反诊断、None/Error与Default继承；源工程另有冲突拒绝/原文件不变回归。IDE重新生成及6.3项不在本勾选的通过范围内。
- [x] 当前2022.3正式IDE包完成VS/Rider生成与重生成、实机诊断、ruleset及pragma对照；VS额外editorconfig的Warning未覆盖Error，如实记录为未验证，不替用户改IDE全局策略。Rider MCP忽略pragma的工具缺陷已由原生编辑器显示对照区分，不冒充抑制失败。没有CodeFix按钮属于明确非目标；6.3对应项仍在上一未勾选项中。
- [x] 当前2022.3 Editor与Development源码见证通过，实际Windows64/Mono Player构建Succeeded、Errors0；输出文件与Runtime PE均无Analyzer/Roslyn/Workspaces/Editor/Test依赖。Player未执行，存在1697条既有XML/API等警告；不外推IL2CPP或其他平台。
- [x] 文档覆盖十条规则示例/例外/抑制、配置/Error恢复、安装升级移除与证据边界；hash和实际结果见台账。独立review已完成并一次TDD修复，限定22文件格式检查及本批diff检查通过。

## 6. 后续候选，不属于本批授权

| 优先研究方向 | 价值与条件 | 后置原因 |
|---|---|---|
| 安全 CodeFix | 可先研究真实 LoadViewArgs 的无参、无 initializer 构造转正式工厂；独立 IDE-only 产物需 VS/Rider + 双 Unity spike | Inject 的不可变性、Panel 的 parent/root、音频等开始/结束都不能机械修复；Workspaces 不进 Unity/Player |
| MustCallBase 路径完整性 | 可能捕获提前 return/条件绕过；先定义正常/异常、async/iterator、调用次数与新 ID | 改变现有存在性契约，不借旧 ID 静默加严 |
| Panel.OnOpen/UpdateDisplay 确定性禁入 | 有 Runtime 明确拒绝点，可研究直接 this/确定 receiver | receiver/owner 与实际调用窗口需要边界，不追任意 helper |
| LoadViewArgs 读写窗口 | 与 Claim、回收、YL0004/5 合作 | 跨方法借用/转交易误报，不能简单禁止字段以外一切使用 |
| Resource Request/Lease 所有权 | 价值高，可先研究局部确定 TakeResult/Dispose 误用 | 字段持有、跨方法转交、Scope 接管并非泄漏，不做通用 CA2000 |
| 注册 token 丢弃 | 可证明返回值被忽略，可考虑 opt-in Info | 永久订阅、owner 统一清理合法，不默认 Warning |
| YL IsNull/IsNotNull 用于 Unity 对象 | 文档已有 fake-null 风险，可研究 YL 专属提示 | 需区分 CLR 引用检查意图与 Unity 存活判断，不做通用空值检查器 |
| SaveItem 内存元数据修改 | 可引导 RenameSaveItem/SetSaveItemNote 持久化 | 新建/独立对象可合法编辑，不能全局禁止 setter |
| Equipments/Skills/Buffs 集合直改 | 可防绕过 Manager 记账 | 先明确 Manager-owned 合同，不能禁止自定义宿主合法集合操作 |
| 消费者 Panel/资源地址校验 | 只读 Editor 报告可检查 Prefab 根、Layer、Catalog 与类型 | 依赖资产与快照，不塞进 Roslyn，不自动修复资源 |
| CI、UPM、额外平台 | 可在本地门禁稳定后复用同一检查 | 独立涉及基础设施、分发作用域、支持矩阵，未获本批实现授权 |

后续方向是研究结论，不是默认启用的规则、不预占 ID，也不是本批完成后自动继续的任务。

## 7. 外部依据与当前证据

- Unity 2022.3：<https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html>，确认标签、asmdef 作用域与原生 ruleset。
- Unity 6.3：<https://docs.unity3d.com/6000.3/Documentation/Manual/create-roslyn-analyzer.html>、<https://docs.unity3d.com/6000.3/Documentation/Manual/analyzer-scope-and-diagnostics.html>。文档不是本项目已兼容的运行证据，也不据其示例升级依赖。
- Ruleset Include：<https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2019/code-quality/how-to-create-a-custom-rule-set>，作为格式依据；两代 Unity 实际生效仍由任务 5/8证明。
- 规划阶段实际使用了官方网页及已安装源码，未调用 Context7；资源列表为空不作为工具不存在的证明。实施阶段已发现 Context7 并查询 Unity CLI 通用文档；未提供精确2022.3版本示例的部分仍以已安装源码和对应官方手册/实际运行复核。

**本轮实际完成：** 已实施计划内Analyzer/Editor/测试/文档改动，部署唯一正式DLL并保留原meta；SDK、当前Unity、独立接收、Mono构建隔离及VS/Rider实机证据见台账。未修改Runtime公共契约、来源工程依赖、分支或既有无关业务资产。技术验收完成时尚未提交或推送，后续Git收口授权见台账。仅Unity6.3按用户要求延期，不宣称完整双版本发布矩阵已通过；Player只验证构建与输出隔离，未运行。

**定稿检查：** 重要设计分支已决定或明确后置，用户在Q25确认共同理解，随后“Implement the plan above”才是实施授权。剩余未验证项如实保留，不以现有局部通过替代。

## 8. 实施台账

- 2026-10-24：收到明确实施授权；计划约束与工作区已复核，当前为 `integration/xf-yl`，既有业务资产和其他 Plan 修改保留。
- Ruling: 在用户指定的现有工作区执行，不运行创建 worktree/分支或额外过程文档的技能脚本；本 Plan 承载持久进度，原因是已批准的工作区与文档约束优先。
- Pre-flight: Task 1 的共享目录由 Task 2-4 追加规则、Task 6 消费；Task 5 的 Preview/Apply 由 Task 6 消费；Task 7/8 在上述实现后验证真实产物。接口没有已知冲突。
- 预检完成：PowerShell 7.6.8 / SDK 9.0.312；SDK 基线构建零警告，129/129 测试通过（`Temp/YLFramework.Analyzers/TestResults/analyzers.trx`）。MCP 连接当前 2022.3.62f3c1，场景已保存，Editor 空闲。
- 环境限制：Hub 当前仅有 2022.3、2023.2、6000.0 安装，无 6000.3。Unity 6.3 验收不能由现有 6000.0 替代；继续不依赖该环境的任务。
- Task 1 complete：描述符中文说明测试先因 YL0001 description 为空失败，随后 SDK 131/131 通过、零构建警告；真实 Unity `FrameworkHelpProviderTests` 7/7 通过。构建收据已纳入共享目录源文件，引用隔离测试通过；未部署新 DLL。
- Task 2 complete：新增注入规则先出现“期望诊断但为0”的红测，随后完整 SDK 145/145 通过，零构建警告；覆盖 private setter、派生/别名、backing field、source/metadata、生成/不完整代码和并发。未改变 Runtime 源码。
- Task 3 complete：12 个预期诊断测试先因无 YL0009 失败；实现后 SDK 161/161 通过、零警告。十个重载、约束链、source/metadata、伪同名 Panel、合法查询/纳管与错误/生成代码均已覆盖。
- Task 4 complete：6 个音频正例及扩充语料先失败，随后 SDK 170/170 通过，零构建警告。性能记录位于测试输出 TestResults/performance.json；正常/压力语料精确产生 144/2100 条诊断，当前暖运行中位数约 52.8/355.2 ms（本机观测，不是性能承诺）。
- Task 5 complete（当前宿主）：最初 26 项因缺少功能断言失败；结构性审查后另加4项未知嵌套/文本红测并修正，最终含真实编译、批量修改的 Core Editor 154/154 通过。真实 csc 证明局部 YL0001=None 同时保留全局 YL0002=Error；所有夹具仅在自有 Temp 内。
- Ruling: Task 5 的 6.3 实测归 Task 8 的强发布门禁，当前没有该安装，不阻塞已在 2022.3 证明的实现向 Task 6 接入；代价是目前不得宣称双版本支持已验收。
- Task 6 complete（当前宿主）：设置页与帮助测试先有20项失败；实现后含真实 Provider 绘制的 Core/Help 181/181 通过，排版后再次181/181通过。Unity 原生菜单可打开设置窗口；读取当前 DLL 标签、平台、明确引用和编译图均为真，仍只显示结构事实、不声称执行已确认。
- Ruling: 原生 SettingsProvider 使用 `Project/YLFramework/Analyzers` API 路径，对应已批准的 Project Settings > YLFramework > Analyzers；菜单仍为 `YLFramework/Core/分析器规则`，不改变用户入口设计。
- 排版：仅本批19个 C#文件使用既有 Format-YL Apply，随后相同范围 Check 通过；未全库格式化。
- Task 7 RED：新增5个 SDK 部署 meta 保护测试先失败；真实 Unity 新规则 Editor/Player 见证与已部署产物指纹比对3例也先失败，证实旧资产未包含新规则且与构建收据不同。
- Task 7 部署：实现部署前/写入前的 meta 身份、精确标签、Runtime 引用与禁用平台预检；暂存改在工程 Temp，避免临时 Assets/meta。SDK 175/175 通过、零构建警告，`build.ps1 -Deploy` 成功；正式 DLL SHA256 为 `839ED13165E769AAF782FEC6340DE5C94C1E2767696403439AD47A63B64B9517`，原 meta 未修改。收据额外包含 build.ps1 本身。
- Task 7 complete（当前宿主）：MCP请求超时后未重复启动批次，等待后恢复读取 `testResults`：总数190、Passed190、非Passed0；明确确认新增Editor/Player正反见证、构建比对与完整导出测试均Passed，未见AD0001/CS8032。当前Windows64 Mono模块可用，外部编辑器设置为VS Code；VS/Rider真实运行证据尚缺。
- Task 8 文档：已更新 Attributes/IOC/Audio/文档首页，补十条规则锚点、准确用法/例外、原生配置/继承保护、Error恢复、升级移除与支持证据边界。锚点契约先红后绿，SDK最新176/176通过、零构建警告，DLL指纹未改变。
- Task 8 复用：验收设施只读研究确认现有 Stage9 Create/Import/Audit/Mono 链可用于新根，不复用旧交付状态、不执行其历史Finalize/Protect/Logger步骤。已在预批准临时目录创建独占归属 `C:/Users/Kano/AppData/Local/Temp/kilo/YLAnalyzer-20261024T1238` 并Initialize成功；正式Exporter已排队导出 `YLFramework-Analyzers.unitypackage`，等待完成标记后再审计与创建ReceiverA。
- Task 8 review：独立审查已返回，无Critical；确认两项Important（原生ruleset非法结构放行、构建后新增源文件漏检），另有字段target与变量Include两项窄边界。已直接核对Roslyn v3.8.0官方RuleSetProcessor/RuleSetInclude源码，不凭建议盲改。
- Final Ruling: 变量Include由Minor升为Important；错误验证字面文件可能漏掉编译器实际读取的策略与路径边界，属于已有安全预览合同。选择明确拒绝 `%VAR%` 语法，不扩展环境解析系统；代价是这类合法但未支持的复杂策略保持只读。
- Final Ruling: 显式field-target的static event/C#9 record字段由Minor升为Important；它们属于已承诺的static/readonly注入字段，可能静默不注入或运行时失败。修复只覆盖实际字段，不新增init-only属性禁令；代价是需要为合成符号补充精确定位与合法形态反例。
- Final fixed（输入集合）：先观察“构建后新增源码”回归未抛异常，修正后Export8/8通过；门禁现在比较完整当前路径集合，拒绝新增/缺失/重复，再比hash。
- Final fixed（ruleset及变量路径）：23个新增本地/继承拒绝回归先失败，修正后56/56通过；按元素区分Default、校验必要非空属性、跨analyzer识别所有ID冲突，并拒绝变量Include；同Action的未知重复仍保留。
- Final fixed（field-target）：static event与record readonly字段先红；改为实际隐式字段补扫及绑定真实构造符号的事件声明检查，保留生成代码排除和合法实例事件/property-target。SDK181/181通过；真实UnityEditor/Player扩展见证先在旧DLL显示缺少两条YL0008，部署后全部通过。
- Final review fix pass：四项均经证据判定后在一次修复轮完成，没有另派二次审查。最新SDK181/181、零警告；Unity Core200/200 + Help/Export15/15通过（选择器对自定义TestCaseSource名称有工具限制，Core按程序集执行，不改断言）。新正式DLL SHA256=`B61FD9599F11279BF5C8735F99A34325FAE6880276DF658EEEB11EABDDA50A38`，原meta不变。
- Final Declined ruling：6.3/IDE与外部Player须实际证据，保留发布门禁；review不独立运行二进制不影响根代理真实测试证据。初始业务资产/无关Plan/旧夹具不纳入本批，后置CodeFix/全路径/任意写者CAS仍不扩范围。代价是这些明确排除或未验项目不能随本批结果一起签字。
- 最终制品：正式Exporter直接导出 `YLFramework-Analyzers-final.unitypackage`，4,245,051字节；包SHA256=`6DAB9EDFC339249AE65228F28D710B368B75F6E52CEE50A795BAD51BB6DCBD52`，当前包审计1038 records通过；DLL SHA仍为`B61FD9599F11279BF5C8735F99A34325FAE6880276DF658EEEB11EABDDA50A38`。同字节副本位于仓库 `Temp/YLFramework.Analyzers/YLFramework-Analyzers-final.unitypackage`。
- 独立2022.3：外部新归属根 `C:/Users/Kano/AppData/Local/Temp/kilo/YLAnalyzer-20261024T1238`，ReceiverA首次无harness导入与Stage9审计通过。`analyzer-consumer-verdict.json`的8项均Passed，实际自动选择两个业务规则集，负例精确YL0008×3/YL0009/YL0010、正例为空、局部YL0001=None同时继承YL0002=Error；含Development画像及重启后检查。
- Ruling: Stage9综合Generate在此长临时路径触发既有Resource工作区路径上限，未修资源系统、未宣称资源运行验收通过。保留失败日志/旧场景后，仅在接收端加最小消费者和自有场景，复用原BuildMono；代价是本轮Player只证明构建/依赖隔离，不证明综合Resource场景运行。
- Player：`ReceiverA/P/m-analyzer-final`实际Mono2x/StrippingDisabled构建Succeeded，Errors0，Warnings1697（CS1591=1674、CS1573=12、CS1570=5、CS1572=4、CS0114=1），均不借本任务清理。`analyzer-player-isolation.json`通过：无禁止产物，Runtime PE只引用netstandard、UnityEngine模块、UGUI、Json.NET、BouncyCastle；未运行Player。
- 最终检查：本批22个C#文件Format-YL Check通过；受控路径git diff --check无输出；原业务asset/其他Plan/旧夹具未改动，原DLLmeta未变化。SDK181/181，Unity Core200/200 + Help/Export15/15；所有待交付实现已有回归或明确验证边界。
- 环境事实：vswhere发现VS Community2022 17.14.28，Rider注册安装2026.2.3.1（用户目录），但尚未进行IDE内实时诊断与规则读取验证，安装/集成包存在不当成运行通过。源Editor当前配置为VS Code，未替用户切换设置。Unity6000.3未安装。
- 续验前的剩余门禁记录：当时尚缺Unity6.3真实接收、编译/配置/Player检查及VS/Rider交互。以下21:05 UTC续验授权后已完成非6.3项，不将此历史记录误读为当前仍缺IDE验证。
- 2026-10-24 21:05 UTC续验授权：Unity6.3明确延期，不作为本轮停止条件；继续完成其余未完项。工作区复核仍为原分支和已知修改，源Editor的默认VS Code配置保持不变。VS/Rider只在独立ReceiverA及本轮自有验收输入上运行，不接管源项目会话。
- 续验准备：在独立接收工程加入与来源工程相同的Unity IDE集成包VS 2.0.28 / Rider 3.0.40，用正式项目生成机制验证Analyzer/ruleset输入与重新生成；这是验收工程依赖，不改变框架或源工程manifest。进程记录只保留PID、程序与已脱敏的项目归属，不采集完整Hub启动参数。
- IDE生成：VS/Rider各完成首次与再次正式生成，均包含唯一YLF Analyzer、正确CodeAnalysisRuleSet及两类实际探针Compile项；三份既有策略hash保持一致，默认外部编辑器未改。证据为`ide-generation-{vs,rider}-ide-*-{initial,regenerated}.json`。
- VS实机：自建隐藏COM实例PID33028，VS2022 17.14.28，非用户控制、未调用Build；真实Error List八组对照已由`Test-VsLiveEvidence.ps1`断言通过并写`vs-live-verdict.json`。未保存缓冲区的新增三诊断为Warning；仅修正注入则只剩两条控制，再引入则恢复；ruleset Error/None/Error热变化、pragma局部抑制以及预定义程序集同样通过。DTE不提供原始ID/来源/完成屏障，ID由自有探针/描述/位置映射，不伪称直接读取ErrorCode。
- VS边界：新增.editorconfig的Warning在本机旧式项目未覆盖Error，记录为未验证而非成功；不改用户全局选项。本功能已证明的配置入口是原生ruleset与pragma。Unity启动期间可能自动迁移生成的sln为slnx并保存自有VS缓冲，故缓冲-only对照期间不启动Unity；该迁移未改源码工程首选编辑器。
- VS清理：仅自建实例已通过DTE无保存关闭，采集服务已退出；自有探针恢复正对照，两类业务规则的YL0008覆盖已用正式后端移除回继承。准备Rider实机MCP对照，原包/DLL指纹不变。
- Rider启动诊断：初始自有PID9196未形成项目窗口或MCP监听；JBR jstack明确报告1个Java死锁，涉及CodeGlance ByteBuddy附加、URLClassPath与StatisticsFileEventLogger，并非YL分析器失败。完整线程输出见本会话工具产物`tool_16fea6f1f0015qjLy5ztD8Mxoh`。已仅停止该自有进程树。
- Ruling: 根据JetBrains官方CLI文档，使用`disableNonBundledPlugins`仅对本次Rider启动禁用第三方插件，不永久禁用、不编辑用户disabled_plugins.txt；原文件hash记录在`rider-owned-bundled-only.json`。当时新自有PID28256尚在启动，先安排等待，未把端口未就绪报通过；实际结果见以下记录。
- Rider实机完成：自有PID28256成功打开ReceiverA，真实MCP握手确认`JetBrains Rider MCP Server 2026.2.3.1`，现场schema使用`rootFolder`而不是研究候选的`projectPath`；已加载Runtime、Assembly-CSharp与Analyzer.Acceptance.Consumer。未调用Build、MSBuild或InspectCode；`Test-RiderLiveEvidence.ps1`生成`rider-live-verdict.json`，14组实际分析断言通过，覆盖两程序集三条Warning、Inject修正/全部修正、Error/None/恢复继承及清理后正例。
- Rider抑制证据：`get_file_problems`确实仍返回pragma已抑制的YL0008，复现`RIDER-142276`，不将工具输出冒充编辑器显示。仅对已校验PID/启动时间的ReceiverA HWND使用PrintWindow，无桌面截图、焦点切换、SendKeys或剪贴板；三张`rider-{pragma,unsuppressed,pragma-restored}-window.png`显示同一字段行红色波浪线像素为0/226/0，两条控制的黄色波浪线保持498/177，策略hash相同。图像已直接检查，限定区域计数断言也通过。
- Rider边界：MCP未提供独立诊断ID/文档链接字段，ID由自有探针、完整消息和位置映射，不伪称直接读取Code；初次文件分析可能先返回ReSharper结果而Roslyn尚未完成，未把该空结果计成功。Problems View另有Unity MonoScriptInfoGenerator的AdditionalFile警告，不归因于YL、不以本轮扩范围修生成器；三条YL实际工作且无YL加载异常证据。
- IDE清理完成：仅自有Rider正常关闭（exit0），自有VS此前已关闭；`ide-cleanup-final.json`为Passed。三份ruleset均恢复至`ide-generation-rider-ide-rider-live-input.json`记录的原始hash（包括一份被手工patch改变的末尾换行），两份探针恢复原正例，无临时editorconfig。用户`disabled_plugins.txt`前后SHA256均为`77E5833C84ADA5E50FF11CE7C4C5DE925AE5A6F0FFDF69B0CE51A3ECBCD8C493`，未永久改变插件选择或源工程默认VS Code。
- 交接说明：Attributes文档明确editorconfig只在实际支持的IDE/项目系统中生效，不能假定覆盖ruleset；本轮已验证的公共配置入口是ruleset与pragma。IDE续验没有修改产品代码、DLL、Runtime或来源manifest，沿用有效SDK181/181、Unity215/215和Mono隔离证据，不因纯说明修改重复技术验收。文档更新后的新命名发行包另行审计，旧冻结包保留。
- 最终新包：`Temp/YLFramework.Analyzers/YLFramework-Analyzers-ide-verified.unitypackage`，4,245,536字节，SHA256=`5A091FD7353AA8BA544B9C6117B5FC6865B2888019E3A603B443C9FE047109E4`；同字节包、1038条精确审计及`analyzer-final-delivery.json`保存在外部归属根。与旧已验包比较，唯一payload差异是`Documentation/Modules/Attributes.md`，全部代码/资源/DLL/GUID/meta不变，未重新绑定或冒称新包经过新的首次导入/Player运行。
- 临时证据边界：收口时来源工程`Temp/YLFramework.Analyzers`已不存在，旧`build-info.json`和原Temp包不在当前文件系统；未猜测清理原因、未伪造构建收据或重复跑SDK。外部旧包与接收证据仍完整，已只重建自有产物目录并复制新包。以后重跑依赖构建收据的导出门禁须先执行正式`build.ps1 -Deploy`；本次结论沿用实际历史测试，并由最终包可执行载荷逐字节相等支撑。
- 最终状态：`source-ide-final-state.json`确认源Editor仍为2022.3.62f3c1、默认VS Code、Main场景未脏、未播放/编译且无编译失败；`analyzer-final-delivery.json`为Passed，含VS8组/Rider14组/原生显示3组证据及清理。仅Unity6.3及其生成项目矩阵明确延期；其他平台/IL2CPP、Player执行和VS额外editorconfig覆盖不在本次已验证声明中。
- 2026-10-25 03:15 UTC Git收口授权：用户明确要求现在提交并推送一次，Unity6.3以后单独批次处理。提交仅含本任务37个文件：Analyzer/Editor实现、测试、DLL、必要meta、四份使用文档与本Plan；排除既有业务asset、其他Plan、旧夹具及无关未跟踪内容。不创建或切换分支，不合并、不改写历史、不强推。
- Git收口预检：当前分支`integration/xf-yl`，本地及远程上游均为`0296b1160aecc367860f14bc890b768cd6c9839d`，暂存区为空；已复核status/diff/log，正式DLL和最终包hash与技术验收一致，原DLLmeta和来源manifest/lock无差异。只做Git与文档收口，沿用有效技术证据，不重复测试或重建；提交Hash及实际推送同步结果在交付回复中报告，不为写入本提交自身Hash追加文档提交。
- 暂存复核：明确清单37项一致，未纳入任何无关改动；LFS指针仍为已验DLL的SHA256。完整`git diff --cached --check`仅提示7份Unity生成meta的21处空值字段尾空格（`userData`、`assetBundleName`、`assetBundleVariant`）；保留已审计元数据字节，不为格式修改GUID/meta或重导出。源码、文档与构建脚本另按非meta范围检查，未关闭诊断或跳过Git hooks。
