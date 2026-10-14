# XF × YL：7.2 成果后的 YLFramework 架构与目录重整候选 Plan

> 状态：用户已授权推荐方案 A，7.R1 → 7.R2 → 7.R3 → 7.O1 → 7.R4 已按顺序实施并通过本批技术门禁；尚未提交，等待用户验收与 Git 收口。真实结果见 §15，不自动授权阶段8或任何 Git 写操作。
> 下文研究基线与候选比较保留历史语境；原研究仅写入本文件，后续实施修改范围及验证另记于 §15。本方案补充已批准的 `xf-yl-7-consolidation.md`，不重跑 7.1/7.2。
> 当前后续修订见 §16：以两个主体程序集承载职责目录。§0-15 继续作为原方案及已完成成果的历史证据，不把其“全部程序集身份/输入作用域保持”约束静默套用于新的合并方案。用户已明确授权 §16（含 §16.9），本轮实际状态见 §17；Git 写操作与阶段8仍未授权。

**Goal：** 在保留已经成立的技术边界与功能契约的前提下，以一致的职责分类承载完整 YLFramework 与后续融合系统。

**Architecture：** 推荐“稳定编译边界 + 按职责分区”：Runtime 分为 Foundation、Core、Systems、Utilities；Editor 对应功能归属并保留独立 Help；测试优先保留现有编译所有者，再改善所有者内部导航。目录用于定位职责，不伪装成已经实现的程序集隔离。

**Tech Stack：** ProjectVersion 为 Unity `2022.3.62f3c1`；当前磁盘manifest直接记录UGUI `2.0.0`、URP `16.0.6`，lock另解析Input System `1.14.0`、Newtonsoft.Json `3.2.1`；后两者不是manifest直接条目。定向读到的PackageCache源码版本则为UGUI `1.0.0`、URP `14.0.12`。不把配置声明/缓存存在等同于活跃Editor已解析版本，也不在本任务修复或升级用户既有Packages/ProjectSettings。现有XF/XFAB/XFFSM Git Package；Analyzer为`netstandard2.0` / Roslyn `3.8.0`，SDK测试锁定`.NET SDK 9.0.312` / `net9.0`。

**Spec：** 用户本轮“基于 7.2 完成成果的全面架构与目录重整研究”；基线导航为 `Assets/YLFramework/Documentation/{IntegrationPlan,Architecture}.md` 和 `.kilo/plans/xf-yl-7-consolidation.md`，事实以当前磁盘源码、asmdef、消费者及资源身份为准。

## 0. 审核摘要

**推荐：** 采用A职责分区，不推倒7.1/7.2已成立成果。Foundation/Kernel、Core、三套Editor编译边界、Analyzer/默认资源原位；完整领域进入Systems，运行工具进入Utilities；未来系统按职责落位，不按规模反复迁目录。

| 决策 | 这次候选明确的内容 | 不在这次候选中实施/预设 |
|---|---|---|
| 架构与路径 | 四个结构批7.R1～7.R4；完整树/映射在 §5～§6 | 不因为目录变了就新增asmdef或改namespace |
| Odin退出 | 独立7.O1原生替换EventListener的42个实际展示标注，再整树移除插件；最终导出在其后 | 不重造Odin、不留假属性，不改UnityEvent字段或游戏/原Package |
| Resource未来 | 固定Systems/Resource外边界，记录Runtime支撑/Audio桥接的闭包约束 | 阶段8的API、租约、资源组、内部树及程序集装配仍另审 |
| 身份与风险 | 保持既有类型/asmdef/GUID/meta/资源键，按批同步白名单、创建器、Help/测试/导出 | 不机械改名、清库、替换已验收生命周期或重跑全历史套件 |
| 设置与交付 | 六导出根、剩余Plugins递归不变；五个未授权清理的惰性Odin宏明确保留 | 不扩大ProjectSettings/Packages写范围，不把技术导出当商业发行许可 |

原研究轮唯一交付为本 Plan。已授权实施顺序：7.R1 → 7.R2 → 7.R3 → 7.O1 → 7.R4。审核方案、授权按批实施与授权 Git 写操作是不同决定；实施结果见 §15。

## 1. 真实基线与证据边界

### 1.1 Git 与工作树

| 项目 | 本轮实查结果 | 解释 |
|---|---|---|
| 分支 | `fusion/7-consolidation`，跟踪 `origin/fusion/7-consolidation` | `git status --short --branch` 实查，不由任务描述推定 |
| HEAD | `0a39928e76e36564a4b354e370b0d939727b7aad` | 提交“docs(plan): 调整阶段7至9路线与长期融合目标” |
| 7.1 | 产品迁移提交 `0f9876ff4e90f3e95dc0646f3035da83c955a850` | Foundation/Kernel 是已提交成果；研究导航还包含未提交的实施记录更新 |
| 远端 | 实时 `git ls-remote --heads`：阶段7远端同为 `0a39928…`；`integration/xf-yl` 与 `fusion/6-editor` 为 `8d6c0f0…` | 未 Fetch、Push 或更改跟踪引用；只代表本轮查询时刻 |
| 7.2 | `Runtime/Core`、`Editor/Core` 及接线已存在；Git 显示旧 XFMain 删除、新 Core 未跟踪，以及对应文档/测试/SDK 路径变更 | 属于已完成但未提交的真实产品工作树，不是待执行的旧方案 |
| 暂存 | 初始 `git diff --cached --stat` 无输出；工作区状态项均为未暂存 | 本轮没有 stage 或 commit |
| 既有其他改动 | `.gitignore`、`.kilo/kilo.jsonc`、NuGet/MCP DLL、Packages、ProjectSettings 等均有既有变化 | 未归因、未覆盖、未清理，不纳入本次结构实施范围 |
| 并行状态 | 初始会话 board 未发现本轮子任务之外的参与者；随后分成五个只读研究域 | 不能据 board 断言整个机器没有其他任务。worktree 元数据命令被权限拒绝；未访问既有 test-worktree |

研究中观察到一次外部工作树漂移：初始存在未跟踪 `Assets/YL72MigrationProbe_0a39928/`；后续 `git status` 已不再列出，精确读取目录返回不存在。本轮及五个研究子代理没有删除它。不把早先残留继续列为“当前待清理”，也不推定外部操作者或清理流程已被验证。

用户确认 7.1、7.2 已完成，按此进入重新评估。实施记录 §8.4 仍区分“技术门禁通过”与“待用户验收/Git 收口”；这是记录与提交状态，不将其解释为 7.2 产品迁移未落地。

### 1.2 已有验证与本轮验证

- 7.2 实施记录载有：路径/GUID/源码闭包/程序集名保持，Core 默认资源、Help、导出、Unity 编译、Analyzer 双轴与七项红绿门禁、SDK 测试等通过。它们是**已有实施证据**，不是本轮重新执行的结果。
- 本轮进行了源码、消费者、程序集、目录、资源引用、测试契约和部署脚本的只读研究；没有 Unity 刷新、编译、Play、测试、SDK 构建、导出或 Player 构建。
- 早先迁移探针中的负向名单曾写 `MainModule` 而实际程序集名为 `Project.Game.MainModule`，且只枚举 Editor 图；不能单凭该名单证明 Player 无注入。源码图支持游戏在 Core Analyzer 范围外，已有完整 Editor 作用域前后比较比该名单更强。后续门禁必须点名真实程序集，不重复制造旧探针。
- 不把 IDE、源码检索或本轮计划审查说成 Unity 编译通过；不把历史测试数量说成当前覆盖率。仓库外消费者、公开再分发授权、未来 IL2CPP/AOT 的完整兼容性仍未知。
- Odin 补充研究为静态核实：已读全部实际功能标注、原生UnityEvent字段、插件/平台变体、外部源码及资产引用。没有运行新Inspector或实际删除。实施前必须从活跃Editor确认实际Unity/UPM版本与编译状态；配置/缓存若不匹配导致验证受阻，报告阻塞，不用本结构任务擅自修manifest/lock或升级引擎。

### 1.3 实施前的只读环境门禁

本Plan的路径/API方案按项目标识的Unity2022.3及源码研究建立。获批实施前，应从**已打开的同一工程**只读读取运行中Editor版本、PackageInfo的resolvedPath/version、当前编译状态与错误、目标平台，区分“manifest声明/lock/缓存/实际加载”四层证据。

UGUI/URP声明与定向缓存不同本身不是本轮已经确认的Unity编译错误；也不能据缓存目录存在推定运行中版本。若实际加载结果与7.2验证环境不一致，仅沿用未受影响证据，并在受影响API/编译部分重新核对；若Package解析或编译已被用户既有变更阻塞，保留现场并单独报告，不在目录/Odin批内修版本、删lock/Library或启动第二个Unity。

## 2. 7.2 后的当前全框架结构

### 2.1 当前目录树

以下为真实目录，不是 7.2 的迁前目标图；省略 `.meta` 展示，但它们属于资产身份。

```text
Assets/YLFramework/
  Runtime/
    YLFramework.Runtime.asmdef
    YLFrameworkRoot.cs
    THIRD-PARTY-NOTICES.md
    Foundation/                         # 逻辑公共能力，整个目录不是一个程序集
      Kernel/                           # YLFramework.Foundation.Runtime，49 个 C#
        YLFramework.Foundation.Runtime.asmdef, LICENSE.md
        ApplicationTool.cs, AssemblyTool.cs, LayerMaskUtility.cs
        CustomAsyncOperation.cs, CoroutineStarter.cs, CoroutineObservation.cs, UpdateManager.cs
        Attributes/
        Events/
        Internal/                       # AssemblyInfo、SingletonSessionState
        Pool/{Abstractions,Collections,Factories}/
        Serialization/
        Timing/Internal/
      Binding/                          # 18 个 C#，包括条件编译 EditorPrefs
      Events/{Core,Global,Internal,Lifecycle,Unity}/
      IOC/{Abstractions,Attributes,Collections,Core,Exceptions,Internal}/
      Pool/{Abstractions,Factories,GameObject,Internal,Object}/
      Singleton/{Asset,Internal,Mono,Plain}/
    Core/                               # YLFramework.Core.Runtime，46 个 C#
      YLFramework.Core.Runtime.asmdef, LICENSE.md
      MVC/{Events,Internal,Requests}/
      Views/Parameters/
      UI/{Adaptation,Tools}/
      Components/Listeners/
      Attributes/RoslynAnalyzers/
      Analyzers/YLFramework.Analyzers.dll
      Resources/YLFramework/Core/{Canvas,EventSystem}.prefab
    Action/                             # 34 个 C#，无自有 asmdef
      Core/, Actions/, Execution/, Internal/, Extensions/, Observable/, ScreenTransition/
      Resources/ActionKit/ScreenTransitionCanvas.prefab
    Audio/                              # 33 个 C#，无自有 asmdef
      AudioKit.cs, AudioKit.Named.cs, AudioChannel.cs, AudioHandle.cs, AudioPlayRequest.cs
      AudioClipAddress.cs, AudioSoundStats.cs, ResLoaderAudioClipProvider.cs
      Actions/, Components/, Config/, Diagnostics/, Internal/, Mixer/
    Resource/                           # 41 个 C#，无自有 asmdef
      ResManager.cs, GameObjectLoader.cs, SubProject.cs
      Framework/{Res,ResLoader}/         # Factory、Key、Table、ServiceManager 等在此
      Implementations/
        AssetBundleSupport/{ConfigFile,Res,ResCreator}/
        ResourcesSupport/
        LocalImageRes.cs, NetImageRes.cs
      HotUpdate/                        # 版本描述、未完成更新流程、FakeResServer
      Utilities/                        # Resource 自有 BinarySerializer/SimpleRc/ZIP/APK 辅助
    Save/                               # SaveManager、SaveFileIO、Serialization/{Color,Vector}
    Services/Http/                      # HttpManager、SendWebRequest
    Tools/
      StringTools.cs
      Extensions/{System,Unity,Math,Utilities}/
      FSM/{Abstractions,Core,States}/
      Grid/, Json/, Table/
      ZipTool/                          # 独立 YLFramework.Utilities.ZipTool
        Utilities/
        ICSharpCode.SharpZipLib/         # 内置第三方源码和完整来源/许可证
  Editor/
    YLFramework.Editor.asmdef
    ClearLog.cs, OpenFolder.cs, ExportUnityPackage.cs, FoundationToolsHelpProvider.cs
    Action/Help/                        # 另有 ScreenTransitionResourceCreator、AssemblyInfo
    Audio/Help/                         # Inspector、素材组、Mixer、调试窗口
    Resource/                           # EditorAPI/Window、Build、Exporter、CodeGenerator、输出校验
      Data/SubProjectData.cs
    Serialization/SerializedDictionaryDrawer.cs
    Core/                               # 独立 YLFramework.Core.Editor，15 个 C#
      YLFramework.Core.Editor.asmdef, LICENSE.md, CoreHelpProvider.cs
      DefaultResources/, ModuleGeneration/, Inspectors/, Settings/
      Extensions/, Utilities/{Assets,Preview,TreeViews}/, Windows/
    Help/                               # 独立 YLFramework.Editor.Help
      Api/, HelpRegistry, HelpModel, HelpWindow, HelpReaderRendering, HelpStyle, AssemblyInfo
  Tests/
    EditMode/{Action,Audio,Extensions,Help,Http}/   # 另有散落的 Foundation/Resource/导出测试
    PlayMode/{Action,Audio,Extensions,Http}/      # 另有宿主、计时、事件生命周期测试
    Foundation/{EditMode,PlayMode}/
    Core/{EditMode,PlayMode,Editor,Acceptance,Fixtures}/
    Fixtures/Audio/
    Integrations/{DOTween,XFAB}/
  Integrations/
    DOTween/Runtime/                    # YLFramework.Actions.DOTween
    XFAB/Audio/                         # YLFramework.Audio.XFAB
  Samples/ColorSelector/                # 独立样例 asmdef、脚本、Prefab、Readme
  Prefabs/{YLFramework,UITips,ColorSelector}.prefab
  Plugins/{Sirenix,Editor Enhancers Bundle}/
  Documentation/
    README.md, Architecture.md, IntegrationPlan.md
    Modules/                            # 20 份现有专题说明

Tools/YLFramework.Analyzers/              # 仓库级 SDK，不是 Runtime/Tools
  Analyzer/{YLFramework.Analyzers.csproj,packages.lock.json,规则及符号实现}
  Tests/{YLFramework.Analyzers.Tests.csproj,packages.lock.json,fixtures/规则测试}
  build.ps1, Directory.Build.props, global.json
```

当前 `Runtime/Tools` 没有 CodeGen。实际代码生成器是 `Editor/Resource/ResDataCodeGenerator.cs:2-9,41-95`，使用 UnityEditor/CodeDom；不能因为用户研究清单提到 CodeGen 就虚构 Runtime 模块。

### 2.2 编译依赖图

箭头为“使用方依赖被使用方”；这是 asmdef 图，不代表所有目录已经可独立交付。

```text
YLFramework.Foundation.Runtime -> BCL + Unity 引擎（references=[]，overrideReferences=true）
YLFramework.Utilities.ZipTool  -> BCL（references=[]，noEngineReferences=true）
YLFramework.Runtime            -> Foundation.Runtime + ZipTool + UGUI
YLFramework.Core.Runtime       -> Foundation.Runtime + XFABManager + UGUI
YLFramework.Editor.Help        -> BCL + Unity Editor（references=[]）
YLFramework.Editor             -> Runtime + Foundation.Runtime + Editor.Help + UGUI
YLFramework.Core.Editor        -> Core.Runtime + Foundation.Runtime + Editor.Help + XFABManager + UGUI

Actions.DOTween                -> Runtime + DOTween.dll（DOTWEEN，显式 opt-in）
Audio.XFAB                     -> Runtime + XFABManager（YL_AUDIO_XFAB，版本 define）
Samples.ColorSelector          -> Core.Runtime + Foundation.Runtime + UGUI
Project.Game.MainModule        -> Runtime + 原 XF/XFAB/XFFSM + UGUI（不引用 YL Core）
```

当前 Runtime/Core 还自动可见既有 precompiled 插件；例如 Runtime 的 EventListener 使用 Odin、Save/HTTP/Core 启动配表使用 Newtonsoft。Odin是补充需求的明确退出对象，目标状态将去掉该自动引用并使用 §14 原生Inspector；这里仍描述当前基线。`references=[]` 不能一概解释成“不依赖 Unity”，也不能把未列入 asmdef 的自动 DLL 依赖忽略。Foundation/Zip 的隔离由其 `overrideReferences`/`noEngineReferences` 实际配置限定。

Foundation 的 friend 仅为 `YLFramework.Runtime`、`YLFramework.Core.Runtime`（`Foundation/Kernel/Internal/AssemblyInfo.cs:3-4`）。Runtime 的 Action internal 被可选 DOTween 实际消费；目录迁移不改变 friend 名单。

### 2.3 逻辑依赖图：目录名称掩盖不了的边

```text
Core.MVC <-> Core.Views <-> Core.UI       # 同一模块/View/Panel 事务机，不是三个独立系统
    -> Kernel.Events/Pool/Timing/Async/Update/Coroutine/Reflection
    -> 原 XFAB 加载、实例池、配表与场景

Action -> Kernel.Update/Events/Pool/Session + Runtime.SingletonHost + YLFrameworkRoot
Audio  -> Runtime.SingletonHost/Binding + YLFrameworkRoot
Audio.Actions -> Action
Audio 默认资源 Provider -> Resource 的公开加载与 internal ServiceManager
Resource -> Runtime.SafeObjectPool/Singleton/Table/Extensions + ZipTool
Save -> Runtime.Singleton + Newtonsoft + 文件系统
HTTP -> Kernel.CustomAsyncOperation/Coroutine + Newtonsoft + UnityWebRequest

Runtime 高层 Foundation.Singleton/Events -> Utilities.Extensions
Utilities.Extensions -> Kernel.LayerMaskUtility
Utilities.FSM -> Runtime.SafeObjectPool
Utilities.Table -> Kernel 集合池
```

后两组边在同一个 Runtime 程序集内没有 asmdef 环，但会阻止“直接各加一个 asmdef”。不能用目标目录图抹掉这些实际边。

### 2.4 配套编译边界

现有 12 个测试程序集保持：主 EditMode/PlayMode 两个、Foundation 两个、Core EditMode/PlayMode/Editor/Acceptance/Fixtures.Editor 五个、DOTween 两个、XFAB Audio 一个。

- 主 EditMode 引用 Foundation 的 EditMode、PlayMode 测试程序集，存在跨测试夹具依赖；不能直接删引用。`SerializedDictionaryEditorTests.cs:13,35`、`SerializedDictionaryJsonTests.cs:7,94` 使用 Foundation 夹具，须按真实类型归属核对。
- Core Fixture.Editor 引用 Core.Tests.PlayMode，原因包括使用 `YLCoreSmokeModule` 的工程/路径常量（`CoreSmokeAssetFixture.cs:9,22-23`），不是产品 Editor 反向依赖 Player。
- `YLFramework.UI.Acceptance` 没有 `UNITY_INCLUDE_TESTS`，引用 `Project.Game.MainModule` 和原 XF/XFFSM（asmdef:4,11）；其场景属于项目耦合验收，不是可复制到独立框架载体的普通测试。
- Core PlayMode/夹具包含 XFAB 构建数据及原 XF 资源共存检查；“不导出 Tests”与“所有测试都是独立的”是两回事。

## 3. 全框架职责判断

### 3.1 应保留的有效设计

| 范围 | 源码结论与依据 | 保留理由 |
|---|---|---|
| Foundation/Kernel | Pool/EventChannel/Timing 内核、请求基类、会话门禁，以及必要 Unity 驱动；无对高层 Foundation/Core/Runtime 的反向类型引用 | Core 与 Runtime 的可验证中立闭包，绝非临时公共杂物目录 |
| Kernel 中的 Unity 代码 | CustomYieldInstruction、Update/Coroutine/TimerDriver、SerializedDictionary、Attribute、Editor 条件分支均真实存在 | “中立”指框架依赖，不等于纯 .NET；为目录纯洁而拆引擎层不值得 |
| Foundation 高层入口 | EasyEvent 复用 EventChannel；SafeObjectPool 复用 ObjectPool；Mono/Asset 单例有各自所有权；Binding/IOC 是共享机制 | 未发现第二套 Pool/EventBus/Timer 算法；不是因为名称多就应合并 |
| Core MVC/Views/UI | Module 排序调用 PanelManager（Module.cs:111-125）；Panel 继承 View（Panel.cs:16）；View 回调 PanelManager（View.cs:406,444）；事务禁止模块启停（ModuleManager.cs:202-216） | 共址反映真实内聚，不将 MVC、Views、UI 强拆 asmdef |
| Action | 工厂/组合动作、执行租约、命名控制、异步观察、场景/全局宿主、屏幕过渡组成完整行为系统 | 不因 Kit 名称或泛用性放进杂项工具 |
| Audio | 唯一播放内核、Clip/Provider/播放请求/租约/Source 所有权、组件与Mixer边界完整 | 现有 Actions/Resource 适配是消费侧协作，不应拆成第二播放内核 |
| Resource | ResLoader 持有依赖图、ServiceManager 调度、IRes 负责资产生命周期；AssetDataTable/ResTable 使用通用 Table但自己承担域所有权 | 是资源领域系统，不是 Foundation 或工具集；不能将其生命周期拆散到通用目录 |
| Save/HTTP | Save 管文件与序列化；HTTP 管 transport、取消、终态与 handler 释放 | 都是领域能力；没有“轻量必须放 Services，大了搬 Runtime 根”的合理依据 |
| Utilities 候选 | Extensions/StringTools、代码 FSM、Grid、JSONObject、Table；Zip 自含第三方源码 | JSONObject 与 Save/HTTP 的 Json.NET 用途不冲突；Table 不接管数据项所有权；不因无当前消费者删除 |
| Editor 与 Help | 三个 Editor asmdef、原生字典 Drawer、显式 provider 注册、安全生成/构建入口 | Help 是通用读者，provider 是消费者；避免 Help 反向引用各领域 |
| Analyzer 双轴 | SDK 在仓库 Tools，唯一 DLL/六 marker 在 Core 作用域 | 已成立，文件应按编译作用域而不是“看起来像Editor工具”安放 |
| Integration/Sample/Plugin | 两个可选适配是叶子，样例是正式组件的消费者，第三方插件独立根 | 不把可选适配混入默认产品，不把样例实现复制回正式组件 |

调用少的 IOC、Grid/JSONObject、资产单例等仍有通用场景价值。缺少消费者或测试只表示迁移证据有限，不自动表示废弃、实验性或可删除。

同名/近似辅助功能也不直接判定可合并：Core.Editor的`AssetNameTools.GetName`按所有资源的精确文件名判断占用（Assets/AssetNameTools.cs:36-45），`ScriptableObjectExtensions.GetName`按ScriptableObject搜索过滤结果递增（Extensions/ScriptableObjectExtensions.cs:85-100）。两者共享命名模式但搜索范围/匹配契约不同；归属债务不构成在路径批合并它们的依据。

### 3.2 具体结构问题与处理层级

| 问题 | 真实证据 | 推荐处理 |
|---|---|---|
| Runtime 同级混用功能、成熟规模、用途分类 | 当前 Action/Audio/Resource/Save 在根，HTTP 在 Services，代码 FSM 在 Tools；Architecture.md:49-53 明写“大型模块/轻量服务” | 一级按职责分区；领域系统不按大小分两处 |
| Tools 名称混淆开发工具与运行期能力 | Runtime/Tools 包含 Grid/Table/FSM/ZIP；仓库 Tools 却是 SDK 与部署 | Runtime 改为 Utilities；仓库 Tools 专指构建/开发工具 |
| Core 被误读为所有公共机制 | `Core.Pool` 同时在 Foundation 与 Runtime，`Core.Singleton/IOC` 实在 Runtime；Core asmdef 不引用 Runtime | 明确定义 Core 为应用架构与表现框架；保留技术身份对照，不做全局 namespace 替换 |
| 通用历史组件/Editor 支撑仍在 Core | TimerObject 仅用 Timing；三个 Listeners 的 Obsolete 文案指向未找到的 CommonSystem；Core.Editor 通用窗口/扩展/设置类并不依赖 MVC/UI | 记录真实边界债务，禁止未来通用能力继续堆入 Core；跨程序集提取另审，不以移动冒充解耦 |
| 通用设置机制有 Core 存储耦合 | `Editor/Core/Settings/ProjectSettingsScriptableObject.cs:89` 固定 `ProjectSettings/YLFramework/Core/{FullName}.asset` | 不让新领域直接继承后误存 Core。通用化涉及设置路径与持久身份，应另专题批准 |
| Resource 不能直接独立编译 | ResLoader.cs:72-84 用 SafeObjectPool；ServiceManager.cs:14-17 用 MonoSingletonProperty；ResTable.cs:20-22 用 Table；Audio provider:68,78 使用 internal ServiceManager | 阶段8先解决支撑闭包和 Audio 桥接，再评审 Resource asmdef；本轮仅确定领域位置 |
| Core.Editor 存 XFAB 耦合 | `Utilities/Preview/EditorAssetTool.cs:5,56-60` 是当前 Core.Editor 内唯一直接使用 XFAB 的工具 | 阶段8定向处理预览消费边，不错误地认为整个 Core.Editor 都依赖资源构建体系 |
| Editor 根散落跨领域入口，测试散落不同功能 | Export/ClearLog/OpenFolder/总览 provider 与 Resource、Timing、序列化测试夹杂 | 同一编译所有者内按功能导航归口，不动 Core/Help 已成立根 |
| 不必要Odin依赖及分发负担 | EventListener.cs:74,242-310使用42个展示标注，Root仅空using；Audio源码闭包测试硬放行插件；插件11个DLL含Player变体 | 本轮追加独立7.O1功能/依赖批，以原生Editor保留真实展示后整树删除，不只取消using或隐藏错误 |
| Resource 热更不能视为成熟更新链 | HotUpdateManager.cs:46、83-94 仍有缺版本/缺完成路径；FakeResServer.cs:60,82 固定当前源码目录路径 | 保留 Resource 自有实现并注明现状；不升级到通用 HTTP/Foundation、不宣称阶段8应沿用该更新主干 |
| 部分导航不准确/导出后断链 | Extensions.md:82 的 UI 消费者与 Core 不引用 Runtime 的源码不符；Resource.md:125 引不存在的 WindowLoaderPoolBaseOnRes；导出 Documentation 链向未导出的 `.kilo/plans` | 后续批准批次修正确切导航；阶段9区分仓库研究入口与包内可用说明，不改导出根偷渡解决 |

“Foundation 高层依赖 Utilities.Extensions”是现有逻辑边，不是 Kernel 反向依赖，也不是当前 asmdef 环。目录分类应诚实呈现它；只有要独立交付高层 Foundation 时才需要单向化/闭包设计。

## 4. 至少两种实质不同的目标方案

### 4.1 A：职责分区 + 功能纵向归属（推荐）

保留 `Assets/YLFramework/{Runtime,Editor,Tests,...}` 交付骨架。Runtime 设 Foundation/Core/Systems/Utilities 四个职责区，系统自有代码与资源在系统内；Editor 以相同职责对应，不与 Runtime 物理混装。现有 asmdef 稳定，新增独立系统只在真实需要时设边界。

收益不是多一个文件夹，而是消除“大了属于根、小了属于服务”“通用就属于Core/Tools”的归属规则。进入 Resource、Localization、Skill 等专题之前，可以固定外边界，而不预定其内部算法/API。

### 4.2 B：完整功能胶囊，模块根下共置 Runtime/Editor/Tests/Documentation

例如 `Assets/YLFramework/Modules/Resource/{Runtime,Editor,Tests,Samples,Documentation}`，Foundation/Core 也成为各自模块。跨域平台能力在独立 Platform/Support 模块；默认交付由显式模块清单组合。

它对独立系统包、团队各管一个模块、成套增删能力更有优势，并非错误方案。但在当前事实下需要二选一：新增多个 asmdef并先解决实际闭包，或建立多处 asmref继续共编译。后者只改变定位，不能带来真隔离。`Modules` 根又不在已批准六导出根中；递归整模块会意外携带 Tests/Integrations，需要新的选择式导出和发行约定。Core 的 Analyzer 必须跟随真实 Core asmdef，而非仅按路径推断。

当前没有被授权的逐模块 UPM/安装器交付需求，也没有已经独立的 Action/Audio/Resource 支撑闭包。故不把 B 的最大代价提前投入到本次目录整理。

### 4.3 C：保守导航修正，不新增 Systems

保留 `Runtime/{Action,Audio,Core,Foundation,Resource,Save}`，HTTP 与领域同级，Tools 改 Utilities；只在文档统一“机制/系统/工具”定义。此方案迁移量最少，平铺目录不是天然错误；仍可支持十几个功能模块。

不足是所有未来系统与平台分区长期混在同一层，Editor/Tests 难建立稳定的 Systems 对应入口，需要维护额外分类表。用户本次目标是重新建立可持续组织规则，A 的几个统一前缀有明确导航收益，成本又远小于 B。

### 4.4 比较

| 维度 | A 职责分区 | B 功能胶囊 | C 保守修正 |
|---|---|---|---|
| 内聚/职责 | 系统所有权明确，公共机制与工具分清 | 最强物理内聚，支撑提取前仍会跨胶囊 | 现有内聚保持，靠规则表表达类别 |
| 依赖/asmdef | 保留验证过的编译图，逐系统评审 | 真模块化需高风险闭包改造；asmref方案无真隔离 | 完全保持，新增模块隔离同样需另审 |
| Unity 便利性 | 熟悉 Runtime/Editor 与资源组织 | 跨模块找 Runtime/Editor，包内装卸更方便 | 路径最短，根模块逐渐增多 |
| 日常维护 | Runtime/Systems/X 与 Editor/Systems/X 成对定位 | 改一个系统最方便，找全框架 Editor 较分散 | 熟悉用户成本最低，分类导航较弱 |
| 层级/命名 | 多一层 Systems，取消 Services/大型小型二分 | 层级更多，模块目录统一 | 最浅，但职责类别不是物理分区 |
| Editor/Tests | 逻辑对应，身份稳定，不机械一一配套 | 全配套共址；须防 Editor/Tests 编译和导出泄漏 | 保留历史混合导航 |
| 未来系统 | 新领域有稳定槽位，内部大小不影响位置 | 最适合独立产品化模块 | 可以安放，但入口越来越平铺 |
| GUID/反射/API | 移目录保 meta，程序集/FullName不变 | 资源可保 GUID，程序集/导出范围往往也变 | 成本最低 |
| 迁移风险 | 主要是路径消费、白名单、Help、导出哨兵 | 引用环、asmref、Analyzer、发行与资产综合风险 | 主要为少量路径修正 |
| 过度拆分 | 不新增每工具/每子域程序集 | 容易把模块胶囊误等同程序集数量 | 几乎无拆分风险 |

C并不是零路径成本：它同样执行Tools→Utilities，白名单、Export、来源许可和文档同步成本与A的7.R1相当。A相对C新增的是已有领域根的Systems统一前缀及对应Editor/测试导航；本Plan不以文件数量或目录深度单独论证收益。

**自主推荐 A。** Foundation/Kernel 与 Core 的保留来自源码与闭包论证，不是“7.1/7.2做过就不能改”。不推荐再搬 Core、拆 MVC/UI、抽纯 .NET Kernel、增加泛型工具总框架或安装器。

## 5. 推荐目标目录树

这是结构完善后的明确目标。标注“现有内部保持”的部分不是待本轮设计新 API；尤其 Resource 的融合内部设计属于阶段8。没有实际文件的未来槽位只由 §9 规则表达，不提前创建空目录。

```text
Assets/YLFramework/
  Runtime/
    YLFramework.Runtime.asmdef            # 名称/GUID/引用不变
    YLFrameworkRoot.cs                    # 唯一公开框架层级入口，保留根位置
    THIRD-PARTY-NOTICES.md
    Foundation/
      Kernel/                            # 保持当前完整闭包，不拆纯BCL/Unity两套
        YLFramework.Foundation.Runtime.asmdef, LICENSE.md
        ApplicationTool, AssemblyTool, LayerMaskUtility
        CustomAsyncOperation, CoroutineStarter, CoroutineObservation, UpdateManager
        Attributes/, Events/, Internal/
        Pool/{Abstractions,Collections,Factories}/
        Serialization/
        Timing/Internal/
      Binding/
      Events/{Core,Global,Internal,Lifecycle,Unity}/
      IOC/{Abstractions,Attributes,Collections,Core,Exceptions,Internal}/
      Pool/{Abstractions,Factories,GameObject,Internal,Object}/
      Singleton/{Asset,Internal,Mono,Plain}/
    Core/                                # 应用架构 + View/Panel/UI，现有内部保持
      YLFramework.Core.Runtime.asmdef, LICENSE.md
      MVC/{Events,Internal,Requests}/
      Views/Parameters/
      UI/{Adaptation,Tools}/              # ColorSelector 属UI域，不迁为普通Utilities
      Components/Listeners/              # 历史通用组件的技术身份保留；不是未来通用组件桶
      Attributes/RoslynAnalyzers/
      Analyzers/YLFramework.Analyzers.dll
      Resources/YLFramework/Core/{Canvas,EventSystem}.prefab
    Systems/                             # 完整领域能力，不按规模分“大型/服务”
      Action/
        Core/, Actions/, Execution/, Internal/, Extensions/, Observable/, ScreenTransition/
        Resources/ActionKit/ScreenTransitionCanvas.prefab
      Audio/
        AudioKit.cs, AudioKit.Named.cs, AudioChannel.cs, AudioHandle.cs, AudioPlayRequest.cs
        AudioClipAddress.cs, AudioSoundStats.cs, ResLoaderAudioClipProvider.cs
        Actions/, Components/, Config/, Diagnostics/, Internal/, Mixer/
      Resource/                          # 完整 Resource 外边界；当前内部暂不重排
        ResManager.cs, GameObjectLoader.cs, SubProject.cs
        Framework/{Res,ResLoader}/
        Implementations/{AssetBundleSupport/{ConfigFile,Res,ResCreator},ResourcesSupport}/
        Implementations/{LocalImageRes,NetImageRes}.cs
        HotUpdate/, Utilities/
      Save/
        SaveManager.cs, SaveFileIO.cs
        Serialization/{SerializedColor,SerializedVector}.cs
      Http/{HttpManager,SendWebRequest}.cs
    Utilities/                           # 运行期按需值/算法/数据工具，非SDK开发工具
      StringTools.cs
      Extensions/{System,Unity,Math,Utilities}/
      FSM/{Abstractions,Core,States}/      # 代码优先轻量FSM，未来视觉系统不是这里的杂项
      Grid/{NormalGrid,DynamicGrid}.cs
      Json/{JSONObject.cs,JSONObjectEnumerator.cs,JSONTemplates.cs,readme.md,LICENSE.md}
      Table/{Table,TableIndex}.cs
      ZipTool/
        YLFramework.Utilities.ZipTool.asmdef
        Utilities/{ZipUtility,ZipArchiveReader,UnzipOptions,GZipUtility,CodePageScope}.cs
        ICSharpCode.SharpZipLib/           # 保留完整源码树、namespace及许可证
  Editor/
    YLFramework.Editor.asmdef
    Foundation/
      Serialization/SerializedDictionaryDrawer.cs
      Events/EventListenerEditor.cs       # 7.O1新增原生等价展示，仅Editor；不新增Runtime标注框架
    Systems/
      Action/{ScreenTransitionResourceCreator.cs,AssemblyInfo.cs,Help/}
      Audio/                             # 当前全部Inspector/Debug窗口/Help整体保留
        AudioClipLibraryEditor, AudioEmitterEditor, AudioGroupPlayerEditor
        AudioInspectorUtility, AudioKitDebugEditorWindow
        AudioMixerConfigEditor, AudioMixerControllerEditor, AudioMusicPlayerEditor, Help/
      Resource/
        Data/
        AssetBundleExporter, BuildScript, ResourceBuildOperation
        ResDataCodeGenerator, ResReferenceOutput
        ResServiceAssetsMenu, ResServiceEditorAPI, ResServiceEditorWindow
    Utilities/
      Menus/{ClearLog,OpenFolder}.cs
      Help/FoundationToolsHelpProvider.cs # 跨Foundation/Utilities总览；不塞入Help框架程序集
    Distribution/ExportUnityPackage.cs
    Core/                                # 保持7.2的独立编译所有者
      YLFramework.Core.Editor.asmdef, LICENSE.md, CoreHelpProvider.cs
      DefaultResources/, ModuleGeneration/, Inspectors/, Settings/
      Extensions/, Utilities/{Assets,Preview,TreeViews}/, Windows/
    Help/                                # 保持零领域反向引用的通用帮助基础设施
      YLFramework.Editor.Help.asmdef
      Api/, HelpRegistry, HelpModel, HelpWindow, HelpReaderRendering, HelpStyle, AssemblyInfo
  Tests/
    Foundation/{EditMode,PlayMode}/       # 独立Kernel验证闭包，保持现有根/asmdef
    Core/{EditMode,PlayMode,Editor,Acceptance,Fixtures}/
    EditMode/                            # 保持现有YLFramework.Tests.EditMode.asmdef
      Foundation/{Binding,Events,Pool,Singleton,Timing}/
      Systems/{Action,Audio,Http}/
      Systems/Resource/{Runtime,Editor}/
      Systems/Save/                      # SerializedDictionary + Save/Json集成测试
      Utilities/Extensions/
      Editor/{Help,Serialization,Events}/ # Events仅新增直接Inspector回归，不造通用测试设施
      Distribution/ExportUnityPackageTests.cs
    PlayMode/                            # 保持现有YLFramework.Tests.PlayMode.asmdef
      Foundation/{Events,Pool,Singleton,Timing}/
      Systems/{Action,Audio,Http}/
      Utilities/Extensions/
    Fixtures/Audio/                      # 自有测试资源不移动
    Integrations/{DOTween,XFAB}/          # define/asmdef/fixtures保持
  Integrations/{DOTween/Runtime,XFAB/Audio}/
  Samples/ColorSelector/                  # 与实现分开；本轮不为路径对称再搬样例
  Prefabs/{YLFramework,UITips,ColorSelector}.prefab
  Plugins/Editor Enhancers Bundle/        # Sirenix及配套meta在7.O1明确退出，Plugins根保留
  Documentation/
    README.md, Architecture.md, IntegrationPlan.md
    Modules/{Action,AssemblyTool,Attributes,Audio,Binding,Events,Extensions,FSM,Grid,Help}
    Modules/{IOC,Json,Pool,Resource,Save,Singleton,Table,Timing,UI,ZipTool}

Tools/YLFramework.Analyzers/
  Analyzer/, Tests/, build.ps1, Directory.Build.props, global.json
```

树中的概括文件名采用当前实际文件，唯一新的正式功能文件为明确标注的EventListenerEditor；迁移清单以 §6/§14 和实施前 GUID/脚本清单为准。`AudioClipLease`、`IAudioClipProvider` 是 AudioClipAddress.cs 中的类型，不是单独文件。没有把 DLL 迁出 Core，没有新增“Foundation.Unity.Runtime”，没有创建空 Resource.Contracts 或缓存/租约层，也没有将 Tests、Integrations 纳入默认分发。

### 5.1 分类规则

- **Foundation**：跨领域机制与生命周期协议；Kernel 是被编译强制的最小共用闭包，高层入口仍属于 Runtime。不是所有“纯 C#”都放 Kernel，也不吸收完整领域系统。
- **Core**：应用组织、Module/Model/Controller、View 会话、Panel/UI 表现。不能因为某类型在很多系统中使用就加入 Core；UI 域自有 ColorSelector/适配不机械迁成通用工具。
- **Systems**：具有明确业务/领域语义、状态/所有权或整套使用流程的能力。HTTP 即使只有两个文件也在此；Action 即使通用也不是 Utilities。
- **Utilities**：按需计算、转换、轻量容器和代码算法。不能只凭“没有Manager”决定归属，现有轻量FSM已明确不承担资产图、可视化编辑与框架调度。
- **Root**：保留唯一公开层级入口的根位置有导航价值，不为单文件新造 Hosting/Composition 层。Action/Audio 各自宿主、Kernel 三种驱动不合并为 Root 大管理器。
- **各域内部**：Abstractions、Internal、Components、Config 等只在有真实内容时使用；扩展方法随所属领域/适配归属，不新增全框架 Extensions 杂物根。

一级分区是职责类别，不是承诺所有逻辑边严格逐层向下。严格不反向依赖的是 Kernel 和经审批的独立程序集；当前 Runtime 内的公共机制与 Utilities 协作边必须记录。

## 6. 当前到目标的映射与理由

| 当前 | 目标 | 理由 / 技术影响 |
|---|---|---|
| Runtime/Foundation 全部 | 原位 | 中立闭包与高层门面分离有实际价值，避免重做7.1 |
| Runtime/Core 全部 | 原位 | MVC/View/UI事务内聚；默认Resources与Analyzer作用域已正确 |
| Runtime/Action | Runtime/Systems/Action | 动作领域所有者；内部/资源整目录保 meta，仍编入 Runtime |
| Runtime/Audio | Runtime/Systems/Audio | 播放领域系统；原Provider/Action消费关系不变 |
| Runtime/Resource | Runtime/Systems/Resource | 阶段8的最终外边界；41脚本与现有内部归属一起迁，暂不再次内部重排 |
| Runtime/Save | Runtime/Systems/Save | 持久化领域，不散拆Serialize DTO到Foundation |
| Runtime/Services/Http | Runtime/Systems/Http | 取消按规模二分；Http目录及meta移动，原Services空容器确认无引用后退出 |
| Runtime/Tools | Runtime/Utilities | 与仓库开发Tools分清；原Tools目录meta随重命名，Zip asmdef/DLL引用方向不变 |
| Runtime/YLFrameworkRoot.cs、THIRD-PARTY-NOTICES | 原位 | Root身份/Prefab不动；通知只更新变动后的来源路径 |
| Editor/Action、Audio、Resource | Editor/Systems/{Action,Audio,Resource} | 与Runtime领域成对导航，仍属于普通Editor程序集 |
| Editor/Serialization | Editor/Foundation/Serialization | Drawer明确消费Kernel容器；不移到Core.Editor或Help |
| Editor/ClearLog、OpenFolder | Editor/Utilities/Menus | 通用Editor便捷入口；MenuItem、类型身份不变 |
| Editor/ExportUnityPackage.cs | Editor/Distribution/ExportUnityPackage.cs | 框架交付工具不是Resource Bundle构建器；六ExportRoots不变 |
| Editor/FoundationToolsHelpProvider.cs | Editor/Utilities/Help/同名文件 | 跨公共能力目录；`foundation-tools` id、菜单、注册契约不改 |
| Editor/Core、Help | 原位 | 保持7.2身份与Help单向消费者图；不偷渡跨asmdef通用支撑提取 |
| Tests/EditMode/{Action,Audio,Http} | 同根的 Systems/{Action,Audio,Http} | 只改导航，保留测试程序集/FullName/筛选入口 |
| Tests/EditMode/Extensions | 同根的 Utilities/Extensions | 与运行工具对应 |
| Tests/EditMode/ResourcePoolTests | 同根的 Systems/Resource/Runtime | 池/Resource消费契约，不是Editor构建测试 |
| Tests/EditMode/Resource{BuildIntegration,CodeCompilation,CodeGeneration,Editor,OutputSafety,Window}Tests | 同根的 Systems/Resource/Editor | 保持互相调用的测试组一起迁移 |
| Tests/EditMode/SerializedDictionary{Json,Save}Tests | 同根的 Systems/Save | JSON/Save集成；不移入只引Kernel的独立测试程序集 |
| Tests/EditMode/SerializedDictionaryEditorTests、Help/ | 同根的 Editor/{Serialization,Help} | Editor展示与读者验收 |
| Tests/EditMode/ExportUnityPackageTests | 同根的 Distribution | 打包实证；与Zip实现区别 |
| 主EditMode其余Binding/Pool/Events/Singleton/Timing测试 | 同根 Foundation 对应子目录 | 明确高层/集成回归归属；不改现有Kernel独立套件，不删除测试间夹具引用 |
| 主PlayMode领域/Extensions/散落宿主回归 | 同根 Systems、Utilities、Foundation 对应子目录 | 和执行模式、编译边界兼容；不改资源夹具路径 |
| Tests/Foundation、Core、Integrations、Fixtures | 原位 | 已按独立编译/项目验收划分，无必要全量再搬 |
| Samples、Prefabs、Integrations、SDK、Plugins/Editor Enhancers Bundle | 原位 | 物理对称收益不足；默认资源、其余插件、叶子适配和SDK双轴保护 |
| Plugins/Sirenix及Sirenix.meta | 整树删除（7.O1，不是路径批） | 用户明确新增的依赖退出；先等价替换42个展示标注及去白名单，保留Plugins根和其余插件 |
| EventListener的Odin标注/Root空using | 移除；新Editor/Foundation/Events/EventListenerEditor.cs替代真实展示 | Runtime类型/字段/序列化GUID不变；新Editor资源由Unity导入生成独立身份 |
| Documentation | 保持文件布局，获批后增量更新导航 | API专题页无需机械复制Runtime树；包内链接在阶段9单独处理 |

删除空 Services 容器前检查其 GUID `8386c8ee393559f4db1e20ac0790a851` 是否被实际资产/字符串引用。不要将旧目录 GUID 转赠给新 Systems；新分区目录由 Unity 创建新身份。所有既有模块、脚本、许可证、Prefab、asmdef 的 meta 随资源移动。

## 7. 程序集、namespace、API 与资源身份

### 7.0 身份导航，不按目标目录推断技术名称

| 目标职责位置 | 保留的namespace / 代表类型 | 实际编译所有者 |
|---|---|---|
| Foundation/Kernel的一般入口/元数据 | YLFramework.Foundation、YLFramework.Foundation.Attributes | YLFramework.Foundation.Runtime |
| Foundation/Kernel/Events与Timing | YLFramework.Events的EventBus/EventChannel；YLFramework.Timing | YLFramework.Foundation.Runtime |
| Kernel Pool与会话内部状态 | YLFramework.Core.Pool的ObjectPool/ListPool/DictionaryPool；Core.Singleton内的internal SingletonSessionState | YLFramework.Foundation.Runtime；Core前缀不表示Core程序集 |
| Foundation高层能力 | YLFramework.Binding；YLFramework.Events的EasyEvent/全局系统/Unity适配；Core.IOC、Core.Singleton、Core.Pool的SafeObjectPool/GameObjectPool | YLFramework.Runtime |
| Core应用架构与UI | YLFramework.Core；YLFramework.Core.RoslynAnalyzers | YLFramework.Core.Runtime |
| Systems/Action | YLFramework.Actions | YLFramework.Runtime |
| Systems/Audio、Resource、Save、Http | YLFramework.Services.Audio、.Resource、.Save、.Http | YLFramework.Runtime，目录Systems不改变Services技术前缀 |
| Utilities一般工具 | YLFramework.Utilities及.Extensions/.Grid/.Json/.Table；代码FSM为YLFramework.FSM | YLFramework.Runtime |
| Utilities/ZipTool | YLFramework.Utilities.ZipTool及内置低层子namespace | YLFramework.Utilities.ZipTool |
| Editor非Core/Help部分 | 保留既有YLFramework.Editor相关namespace；新EventListenerEditor为YLFramework.Editor.EventListenerEditor | YLFramework.Editor |
| Editor/Core、Help | YLFramework.Core.Editor；YLFramework.Editor.Help | 分别Core.Editor与Editor.Help |
| Runtime根入口 | YLFramework.YLFrameworkRoot | YLFramework.Runtime，不是Core/Kernel服务定位器 |

此表是消费者查找入口，不是类型全集或改名映射。未来专题新增正式类型可采用批准后的明确命名，但既有类型不随文件夹名称批量替换；真正跨程序集提取仍按 §7.3 处理。

### 7.1 路径批次的硬约束

- 保留七个产品程序集名称、asmdef GUID、引用、平台/define、overrideReferences、friend 及正式 API。不新增 asmref，不靠自动引用修复断边。
- 保留 `YLFramework.Core.*`、`YLFramework.Actions`、`YLFramework.Services.*`、`YLFramework.Utilities.*` 等公开 FullName；目录 Systems 不要求 namespace 增加 Systems。`Core.Pool` 的 ObjectPool 在 Foundation、SafeObjectPool 在 Runtime是明确历史身份。
- 保留所有序列化字段、UnityEvent 持久目标、MonoScript GUID/fileID、正式Prefab内容与默认Resources逻辑键。Action资源外部路径变化，但键仍为 `ActionKit/ScreenTransitionCanvas`；Core两键仍为 `YLFramework/Core/Canvas|EventSystem`。
- 保留 YLFrameworkRoot.cs 身份与层级字符串、Audio PlayerPrefs 键、Save文件格式/目录配置、Core设置路径与ProjectName语义。目录移动不重写这些业务身份。
- Analyzer DLL唯一身份 GUID `471f81c4273ee66429677df44cbd6960`，仍在 Core asmdef 子树；六marker FullName/脚本GUID/程序集不变。SDK输出仍部署 `Runtime/Core/Analyzers`，不将“编译工具”移进普通Editor根。
- 正式导出仍为 Editor、Plugins、Prefabs、Runtime、Samples、Documentation 六根，Recurse，不加 IncludeDependencies。根 Integrations、Tests、SDK、NuGet、游戏、YLFrameworkData仍排除。

以上是7.R1～7.R4路径批的身份不变约束。7.O1按用户补充允许一个明确例外：移除Odin展示元数据/预编译引用及Sirenix插件树、增加原生EventListenerEditor和直接回归；不改变Runtime公开类型、程序集名/asmdef配置、事件字段或其他功能依赖。不能把这个例外扩大到泛用Editor/API重构。

### 7.2 Analyzer 的实际契约

七项仍为 YL0001基类调用、YL0002限制调用、YL0003指定类警告、YL0004工厂构造、YL0005方法局部所有权、YL0006受保护实例修改、YL0007 View消息遮蔽，默认 Warning。

`Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs:28-55` 精确绑定 Core程序集/View/marker、Foundation程序集的ObjectPool以及两个窄豁免字段形状。缺符号或不唯一Core时会静默不注册；“零诊断”可能是失败，不能当通过。

Unity 2022.3 的注入由 asmdef子树及其引用消费者决定，GUID不证明作用域。Core/Core.Editor/Core测试夹具验收/ColorSelector是当前正向范围；Runtime/Foundation/普通Editor/Help/游戏为负向范围。路径批不改变这些所有者。无需每批重建SDK或重跑七项历史套件；最终只对受影响编译输入核对作用域，身份/边界一旦变了则升级为独立技术批。

官方依据：<https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html>；程序集规则：<https://docs.unity3d.com/2022.3/Documentation/Manual/ScriptCompilationAssemblyDefinitionFiles.html>。本轮已读取对应版本文档，不依据新版本猜测。

### 7.3 必须另审的技术重构，不在路径批执行

| 候选 | 有据收益 | 消费者 / 成本 / 决策 |
|---|---|---|
| Resource独立编译与交付 | 让Core及其他系统引用资源而不引入整个Runtime，支撑成熟XFAB融合 | SafeObjectPool/Singleton/Table/Extensions在Runtime，Audio又反向消费Resource internal，直接加asmdef会环；阶段8先给完整闭包/身份方案，不能自动提取到Kernel或复制支撑 |
| 高层Foundation/Utilities单向化与必要支撑提取 | 为上述独立Resource等模块提供小依赖面 | 单例/事件使用GetOrAddComponent/Destroy扩展，FSM/Table/Resource依赖池；可能改变公共类型程序集与friend。由真实目标驱动评审，不为分层图漂亮预拆 |
| Core.Editor通用支撑与设置公共化 | 防止资源/未来领域为了通用窗口、树、设置而引用Core/XFAB | public Editor类型身份、设置存储路径、15文件边界测试都会受影响；现有生产消费很少，不急拆。发生真实复用需求时不得复制或直接把新通用框架塞Core |
| TimerObject/历史Listeners的跨程序集归位 | 概念归属更准确，修复不存在替代项的指引 | TimerObject FullName/assembly/字段被测试固定；外部UnityEvent未知。同程序集重标目录可低风险，但本轮不为此重复搬Core；删除/提取另批 |
| 统一Core.Pool/Singleton/IOC namespace | 降低身份导航歧义 | Analyzer符号、代码消费者、反射/序列化与仓库外使用者迁移成本高；目前用对照表足够，推荐不启动 |
| MVC/View/UI拆程序集、纯.NET Kernel | 只有独立发行/引擎无关需求明确时才可能获益 | 事务/双向类型关系与Unity生命周期是现有能力，当前不成立，明确不推荐 |

这些是待专题决策，不是许可任何实施者在目录批“顺手修复”的清单。阶段8的依赖重构批准也不得自动扩大为整个Foundation/Core改名。

## 8. Editor、Tests、Integrations 与交付策略

### 8.1 Editor

保留普通Editor、Core.Editor、Help三程序集。目标中的 Foundation/Systems/Utilities/Distribution 都仍在普通Editor根下；Core.Editor、Help自有asmdef子树不穿越。目录名 Editor 本身不提供隔离，平台条件以真实asmdef为准。

HelpRegistry/ApiReader只接收显式provider的数据，不扫描并构造业务实例。Action/Audio/Core/总览provider在自己的消费侧，分别保持 `actionkit`、`audiokit`、`core`、`foundation-tools` 等现有id与菜单。普通总览provider不因移动到带Help名字的目录而加入 `YLFramework.Editor.Help`。

SerializedDictionaryDrawer是原生UIElements+IMGUI展示，不是Odin适配；DisableEditDrawer仍在Core.Editor是历史编译归属。新Foundation展示工具不得自动引用Core才获得基础能力，若需复用既有Core.Editor公共设施，触发 §7.3 独立评审。

Resource CodeGen/Bundle Build属于领域Editor；ExportUnityPackage属于框架Distribution。两种“导出”不合并。

### 8.2 Tests 与 Samples

不为路径对称搬所有测试asmdef、验收场景或夹具。现有Kernel/Core独立套件保持严格边界；主测试程序集的文件在其根内分组，所以测试程序集名、test FullName、方法名和filter仍稳定。对直接依赖Foundation测试类型的序列化回归，保留原引用并记录，不假装解耦已完成。

未来独立领域系统按 `Tests/Systems/<Domain>/{EditMode,PlayMode,Editor,Fixtures}` 创建真实测试所有者；仍共编译Runtime的能力在现有主套件Systems子目录补用例。只有其产品已经独立或需要真实测试依赖隔离时才新增测试asmdef。

Acceptance明确是项目耦合验证；独立载体测试必须新建最小正反例而不是复制整份UIAcceptance。ColorSelector Sample保持干净消费者；正式ColorSelector/UITips/根Prefab不降格为样例。将来样例按域加目录，但不为一份已有样例追求树形对称。

### 8.3 可选适配与第三方

根 Integrations继续作为可选叶子适配：有真实外部依赖，显式define/versionDefine，autoReferenced=false，显式构造/安装；主Runtime/核心系统不反向引用。当前DOTween使用internal friend属真实边界，不擅自取消。

未来大型Resource的正式融合不能被误放成 `Integrations/XFAB` 音频适配旁边的小包装：作为统一YL资源主系统在 Systems/Resource；真正可换的外部桥接才进入 Integrations。XFAB内部API、缓存/租约/资源组/更新不在本轮预定。

内置SharpZipLib/JSONObject随功能owner放置并保留完整MIT与修改声明；其余Editor插件在Plugins原位。用户已明确要求Odin退出，这是批准方向的依赖/payload变更，不是为了缩包偷偷漏导插件。Sirenix整树及其专属许可证/演示/配置一起退出，不复制其实现到YL；六根与**剩余Plugins完整递归**的机制仍不变。Odin不再分发后其再分发授权问题随该依赖退出消除，Editor Enhancers公开再分发授权证据仍不足，公共发行继续有独立门槛。

### 8.4 Documentation

专题说明继续按用户能力导航，不机械镜像每一层代码路径。本轮不修改IntegrationPlan/Architecture；实施获批后，7.R1～7.R3同批修正直接路径消费者，7.R4明确包含Architecture的当前目录/职责表与README当前入口的最终路径修正。本轮候选不是长期蓝图的既成事实；历史阶段/提交记录与原路径来源说明保持，不用全局替换改写历史，长期路线或阶段状态变化另按正式授权流程处理。

阶段9须保证导出包内的API/使用导航可解析。`.kilo/plans`属于仓库研究记录：可在包内标明“仓库专用外部参考”，不能把唯一使用说明依赖该链接；不复制候选Plan进Runtime，也不借断链问题擅自改六根导出契约。Resource文档所列当前找不到的历史测试/消费者不再作为验证证明。

## 9. 阶段8与后续系统的承载规则

### 9.1 Resource 的确定位置与依赖方向

**完整资源系统是 `Runtime/Systems/Resource`，与Action/Audio/Save/Http平级。** Editor构建、标记、开发预览/调试、代码生成放 `Editor/Systems/Resource`；测试/样例/说明按资源域对应。

当前和未来必须分开画：

```text
结构完善后仍成立的现状：
Core -> Foundation + 原XFAB（不改ProjectName）
Audio/Resource/Action/高层Foundation/Utilities -> 同一YLFramework.Runtime闭包
Core.Editor -> Core + 原XFAB + Foundation + Help

阶段8希望达到、需其技术方案批准后才能成立的方向：
Resource -> 最小公共支撑闭包 + 必需Unity/第三方低层库
Core     -> Resource的正式资源能力 + Foundation
Audio消费侧适配 -> Audio + Resource
Resource.Editor -> Resource + 公共Editor支撑（不反向依赖Core应用组织）
可选外部后端/桥接 -> 其正式系统 + 外部库（系统不反向引用可选叶子）
```

这里不命名 Resource.Contracts、缓存/租约类或后端API，不提前设计XFAB内部树。阶段8是否用单一Resource.Runtime、必要的契约/实现边界，取决于实际API与闭包研究。关键原则是**不能让大型Resource长期依赖整个Runtime或Core，再要求Runtime/Core依赖它**。

现有Resource/Framework、HotUpdate、Utilities子树在外边界迁移时原样保留，是为了避免紧接阶段8前做一次易被推翻的内部重构。它们不是成熟融合后内部结构的强制模板，也不是本轮允许延后处理的未知外归属。

### 9.2 后续专题如何安放

| 能力 | 目标归属规则 | 不做的预设 |
|---|---|---|
| XFFSM完整资产/运行/编辑能力 | Systems/StateMachine与对应Editor域；代码FSM仍是轻量算法入口 | 不先规定状态/图/API，不默认两个完整管理体系永远并存 |
| Logger | Systems/Logging的日志服务/管线；纯格式函数才属Utilities | 不把有配置/输出/生命周期的Logger丢Foundation/Tools |
| Archive | 与Systems/Save持久化领域统一研究；是否保留独立Archive职责由能力差异决定 | 不因来源不同固定两个竞争存档Manager |
| Skill、Buff | 各自Systems/Skill、Systems/Buff；共享算法经实际复用才提取 | 不预造Gameplay大框架、不把规则引擎归Core |
| Localization | Systems/Localization，资源消费在域内或消费侧桥接 | 不能因“查表”放Utilities/Table，Table不拥有本地化生命周期 |
| Mission | Systems/Mission，流程/数据/状态所有权在域内 | 不因为事件很多就塞Foundation/Events |
| 配置/资源表/角色/平台/物理查询/通知 | 按完整领域职责放Systems；纯算法/容器才放Utilities | 不为每项创建空目录或一份asmdef |

允许系统之间有显式、单向、经论证的领域依赖，不机械强求每个系统只依赖Foundation；禁止让平台公共支撑依赖具体业务系统。双系统可选协作优先是消费侧叶子桥接，避免形成核心循环。目录名使用正式能力命名，来源前缀只用于必要的外部适配/许可。

## 10. 获批后追加的结构完善批次

四个结构批仍命名为 `7.R1`～`7.R4`，另加 `7.O1` Odin功能/依赖批，推荐顺序为 **7.R1 → 7.R2 → 7.R3 → 7.O1 → 7.R4最终收口**。不重新执行7.1/7.2，也不继承旧“只剩两个批次”限制。每批独立停止、审差异后才能进入下一批；不在本轮执行，不自动commit/切分支。开始实施时重新核对真实HEAD、已有变化与用户验收，不要求必须先提交7.2才能只读或设计，但迁移不得与其他写任务争同一资源。

### 10.1 共同门槛

- [ ] 建立批准范围内的路径/GUID/meta/文件内容/类型FullName/程序集源码归属基线；Core/Kernel/Help/默认资源/Analyzer作为不变哨兵。
- [ ] 定向收集当前路径消费者：Editor常量、SDK/测试白名单、Help、文档/许可路径、Prefab/UnityEvent/Resources。源码负例里的历史XFMain与本轮需退出路径区分，不全局替换。
- [ ] 先确认 §1.3 实际环境与需要复用的基线证据；已存在的编译/测试失败单独记录原因及归属，不记成目录/Odin引入的回归，也不通过跳过或放宽断言掩盖。
- [ ] 由Unity资产移动API保meta；只新建实际使用的分区目录；不保存用户dirty场景、不生成/覆写业务资源。
- [ ] 每批同批同步直接受影响的测试路径断言；更新路径但不弱化GUID、程序集、闭包、唯一性、所有权断言。
- [ ] 7.R路径批出现任何程序集/API/字段/资源内容/依赖变化，或任一批出现GUID不一致、第二实现、超出该批批准的功能变化、未授权资产写入，立即停止。7.O1仅允许 §14 明列的替换/依赖删除。失败保留现场，不Reset/Clean/Stash或覆盖用户工作。

### 10.2 7.R1：Utilities 与非领域 Editor 导航

**实际问题：** Runtime/Tools混淆SDK工具；通用Drawer/菜单/交付入口散在Editor根。

**迁移范围：** Runtime/Tools整体改名Utilities；Editor/Serialization→Foundation/Serialization；ClearLog/OpenFolder→Utilities/Menus；FoundationToolsHelpProvider→Utilities/Help；ExportUnityPackage→Distribution。不动Core、Help、Plugins、SDK。

**技术影响：** Tools目录meta保留，Zip的asmdef GUID/name/noEngineReferences保持；其他脚本仍Runtime或普通Editor；namespace/API无变化。Root/默认资源/Analyzer字节与作用域不变。THIRD-PARTY-NOTICES与JSONObject/SharpZipLib来源路径同步，完整许可证随目录。

**实施清单：**
- [ ] 按上述映射移动既有资源及meta，新增实际分区meta。
- [ ] 更新 ExportUnityPackageTests、读取Export源码的AudioXfabBoundaryTests等准确路径；同批将AudioSourceIndependenceTests.cs:35-36,52-53的Tools/Table、Tools/ZipTool/ICSharpCode.SharpZipLib及四个Tools/Extensions文件路径仅映射到Utilities，其Audio/Action/Resource段留各自后批。不改六根ExportRoots、menus、Help id或闭包白名单范围。
- [ ] 核对移动前后源码文件所属程序集与GUID集合完全一致；Unity编译受影响Runtime/Editor及测试依赖。
- [ ] 执行 AudioSourceIndependenceTests的Editor与Windows Development两个源码闭包profile、ToolsExtensionsContractTests、SerializedDictionaryEditorTests、HelpWindowTests 和变动的路径/许可/导出清单断言。ZIP算法不变不重复全部安全/性能历史套件。

**独立停止：** Zip被编入Runtime、许可遗漏、Help provider误入Help asmdef、任何类型身份/菜单变化，或普通Editor开始引用Core。真实包解包门禁留7.R4统一执行，本批不声称已验证正式导出。

### 10.3 7.R2：非Resource领域系统归口

**实际问题：** Action/Audio/Save根级与HTTP Services级不一致。

**迁移范围：** Runtime/{Action,Audio,Save}→Systems对应目录；Services/Http→Systems/Http；Editor/{Action,Audio}→Systems对应目录。保持域内部子树，Action/Audio因播放动作直接协作同批核对。

**技术影响：** 不新增领域asmdef；原脚本/目录/Action资源meta随迁；Audio组件持久UnityEvent、PlayerPrefs、Action Resources键、Root/Kernel/Core默认资源不变。Core Analyzer所有者不动。

**实施清单：**
- [ ] 迁移对应目录；核实Services容器GUID无引用后才退出空容器与其meta。
- [ ] 同批更新 ScreenTransitionResourceCreator.cs:16,35 的新物理路径；保持拒绝覆盖既有资源的行为，不重新生成Prefab。
- [ ] 精确映射 AudioSourceIndependenceTests.cs 的Audio/Action源码白名单与全量收集根、Export哨兵；同批更新ScreenTransitionTests.cs:183-184的物理Prefab路径，保留:186-191的Prefab/脚本GUID及配置断言。唯一迁移后的Action资源以原GUID和逻辑键可加载。ApiHelpTests临时生成的探针路径不因产品目录移动而改，定向核对其真实依赖即可。
- [ ] 同步相关Help、当前README/Architecture入口与THIRD-PARTY-NOTICES的Audio/Action/HTTP当前来源路径；保留明确标为历史的原路径说明。
- [ ] Unity编译与SourceIndependence、Action API/Help/资源身份门禁；最小PlayMode覆盖ScreenTransition加载、Action宿主和Audio组件序列化/原句柄生命周期。HTTP用现有HttpRequestTests及退出/取消回归；Save用SerializedDictionaryJsonTests、SerializedDictionarySaveTests，不伪造不存在的SaveManagerTests。
- [ ] 核对游戏Prefab中的YL Audio脚本GUID、UnityEvent目标FullName/assembly保持；只读检查，不修改游戏资产。不因跨系统路径变动重跑全部Module/Panel/XFAB启动套件。

**独立停止：** 旧路径仍用于创建默认资源、白名单误扩大到Core/全Runtime、Action逻辑键或Audio持久身份改变、移除friend造成DOTween断编译、Services目录实际有非HTTP内容/引用。域行为差异转缺陷专题，不能用新回退吞掉。

### 10.4 7.R3：Resource外边界迁移

**实际问题：** 大型资源能力缺少最终统一系统归属；源码路径消费仍指向旧根。

**迁移范围：** Runtime/Resource→Systems/Resource；Editor/Resource→Systems/Resource。当前所有加载/AB/HotUpdate/Utilities内部原样，不改Core→XFAB或ProjectName，不做阶段8融合。

**技术影响：** 41 Runtime脚本仍YLFramework.Runtime；Editor仍普通Editor；所有public FullName、资源身份、模拟开关、生成输出目录与Bundle名不变。默认Core/Action资源与Analyzer不再移动。

**实施清单：**
- [ ] 移动前检查HotUpdate/Remote/目录（包含其ResVersion.json、Bundle/.manifest）及两棵目录中的其他非代码资产。不存在不新造；若存在生成资源、Bundle、额外业务配置/字符串绑定或用户文件，先确认本批授权与所有权，未获准时停止，不能先整体搬目录再补查。
- [ ] 前项通过后迁移两棵目录与meta；仅把FakeResServer.cs:60,82两条字符串中的`/YLFramework/Runtime/Resource/`替换为`/YLFramework/Runtime/Systems/Resource/`，保留`HotUpdate/Remote/`及`ResVersion.json`尾段。:28的TempAssetBundlesPath是`Application.persistentDataPath + "/TempAssetBundles/"`，不在迁移范围、不得改为源码树。只改模块源码位置，不运行FakeResServer生成/下载，也不改其功能。
- [ ] 同批更新 SourceIndependence的Resource白名单、Resource编辑/生成测试的源文件常量、Export哨兵与模块文档（包括Resource.md:144的HotUpdate/Remote路径）；资源输出及persistentDataPath暂存位置仍在原自有位置，不迁到产品源目录。
- [ ] 静态核对上述两条完整远端目录/版本文件路径与迁后owner一致、TempAssetBundlesPath及所有消费表达式逐字不变；本轮没有现成FakeResServer/HotUpdate测试覆盖，不能由Resource编辑器测试代替这个定向检查，也不为目录迁移制造热更下载运行。
- [ ] Unity编译；执行 ResourcePoolTests、ResourceCodeGenerationTests、ResourceCodeCompilationTests及ResourceWindow/Editor中受路径影响的用例；检查输出安全规则与新源码文件所属程序集。
- [ ] 用现有最小资源加载入口验证Audio默认ResLoader Provider仍消费同一加载系统。资源构建算法/设置未变，不默认重打项目所有Bundle；需要构建实证时仅运行现有隔离ResourceBuildIntegrationTests，不触碰游戏XFAB工程。

**独立停止：** 需要加Resource asmdef、扩大Foundation friend、修改internal API、重做HotUpdate功能、任何生成资源归属不明、输出边界/Bundle/持久设置或persistentDataPath暂存路径变化。此时停止当前批转审批；不能以“阶段8迟早要改”夹带。

### 10.5 7.O1：Odin等价替换与整项目插件退出

**实际问题：** Runtime EventListener的展示仍绑定商业插件，Root空using与测试白名单维持不必要硬依赖，默认包包含全部11个Odin DLL及变体。用户明确将退出加入本轮实施规划。

**范围与接口：** Runtime/Foundation/Events/Unity/EventListener.cs移除42个Odin标注及using，Runtime/YLFrameworkRoot.cs移除空using；新增`Editor/Foundation/Events/EventListenerEditor.cs`（建议类`YLFramework.Editor.EventListenerEditor`）与`Tests/EditMode/Editor/Events/EventListenerEditorTests.cs`直接回归，两者新meta由Unity生成；更新AudioSourceIndependenceTests/EaseUtilityTests及当前依赖文案；整树删除`Assets/YLFramework/Plugins/Sirenix`和`Sirenix.meta`。使用方为`YLFramework.Events.EventListener`、20个现有UnityEvent字段；产出是相同折叠/页签/中文标签/事件配置能力的原生Editor，不产出mini-Odin公共API。

**技术影响：** Runtime公开FullName、程序集/asmdef/friend、EventListener/Root GUID、20字段名/类型/顺序/SerializeField及持久UnityEvent身份保持。新Editor脚本由Unity生成新meta；去掉Sirenix编译/Player引用是本批明确依赖变化；Core/Kernel/Analyzer和其他插件不动。无需Odin序列化数据迁移。五个惰性ODIN_INSPECTOR宏受原ProjectSettings禁改约束，本Plan当前不删，见 §14.4。

**实施清单：**
- [ ] 实施前重新核对 §14 的生产符号/资产/二进制消费者及已安装版本。先实跑EaseUtilityTests的现有Runtime引用门禁并读取实际引用集：源码白名单目前漏`YLFramework.Foundation.Runtime`，而HTTP基类/SafeObjectPool确实消费Kernel，存在高置信的既有基线漂移推断。若实测因这一个缺项失败，同批仅明确加入已合法依赖的Foundation.Runtime并记录为7.1后的测试契约同步，与Odin替换分开说明；不删除其余断言、不扩大任意项目引用、不冒称本轮已跑红/绿。记录EventListener字段与其原20标签/3页签，以及现有Prefab/Scene/默认Resources GUID/内容；若出现未知Odin消费者、持久数据或解析阻塞，停止，不能改原Package/游戏资产或偷偷留第二份Odin。
- [ ] 实现 §14.2 单一原生CustomEditor：隐藏m_Script、折叠组、三页签、20原标签/顺序；使用SerializedObject/SerializedProperty和UnityEvent原生Drawer，保留多对象编辑、Undo/Redo与Prefab override。只服务EventListener，不接管全项目Inspector；派生类型额外序列化字段仍按原生方式展示。
- [ ] 移除明确列出的42个Odin标注和两个using，保留SerializeField/字段/API/生命周期实现。不得用InspectorName或平铺Header冒充等价页签，不创建假Sirenix namespace/空属性，不复制闭源Odin源码。
- [ ] 同批收口AudioSourceIndependenceTests的odin路径、File.Exists、additionalReferences、IsAllowedReference参数/允许分支；仅允许原BCL/引擎/UGUI，不放行任意第三方。EaseUtilityTests删除Sirenix允许项并用负向断言拒绝Sirenix程序集；保留其余引用契约。
- [ ] 在单一EventListenerEditorTests文件中做直接自动契约/序列化回归：Editor.CreateEditor确实选择普通YLFramework.Editor中的新Editor、支持多对象；20字段名/UnityEvent类型/SerializeField与原模式保持，无Sirenix特性；SerializedObject可找到全部字段，Editor映射对实际字段20/20覆盖、无重复。增加一个仅测试使用的EventListener派生探针及额外SerializeField，验证该字段可见/可编辑，不另造测试程序集或通用UI测试框架。
- [ ] 人工GUI验收单独记录：隐藏Script、折叠/三页签的实际展示及20中文标签/顺序，UnityEvent持久监听增删改、多选、Undo/Redo、自有临时Prefab override/apply/revert。自动反射/映射测试不冒充这些视觉/交互通过；也不只是把映射表硬编码两次得到绿色。仅操作自有临时对象/资产，绝不保存用户场景或正式Prefab；原生多对象UnityEvent列表行为以Unity实现为准。
- [ ] 先在Odin仍存在时编译确认原生替换可用，确认生产代码零Odin类型引用且持久身份不变；然后**同批**用Unity资产API删除Sirenix整树/根meta（含NoEditor/NoEmitAndNoEditor、模块asmdef、config、link.xml、Demos/许可证），保留Plugins根与Editor Enhancers全部内容。前置未完成不得先删DLL；该批未删除也不能声称Odin退出完成。
- [ ] 更新当前README/Architecture/Help/模块使用文案及已有测试注释，移除“仍需Odin/Root Attributes”的现状陈述；历史阶段说明、负向测试词和明确的Odin退出记录不做无意义文本清零。长期路线/阶段状态不擅自改写。
- [ ] 删除后Unity实际重编译成功，核对所有活跃项目程序集及编译器输入无Sirenix引用；运行AudioSourceIndependenceTests两个profile、EaseUtilityTests引用门禁、UnityEventListenerContractTests、EventsLifecyclePlayModeTests及新Inspector直接回归。既有有效Core/Audio等行为证据只在未受影响时复用，不重跑全历史套件。
- [ ] 做一次当前主目标Standalone Windows64开发Player构建/启动smoke，BuildReport/Managed内容不得含Sirenix，验证代表性UnityEvent功能；全局Inspector回落原生另在Editor中验证，不能用Player替代Inspector验收。该依赖删除不同于纯路径批，不能只凭Editor无错误断言Player通过；Android/IL2CPP仅在当前交付实际要求时增加，不默认全平台。
- [ ] 保留实际验证结果供7.R4最终导出复用，避免再建第二包。全项目终扫：插件资产/DLL/外部GUID/生产代码引用/实际程序集引用均无Sirenix；测试不放行、文案不宣称依赖；仅历史/负向说明及 §14.4 明列惰性宏可出现旧名称。

**独立停止：** 原生功能缺页签/标签/事件编辑，任何字段/持久资源身份变化，发现Odin序列化/未知第三方硬引用，删除后编译/Analyzer加载/Player失败或MissingScript，白名单放宽，Runtime引入Editor/假兼容层，Plugins根/其他插件被删除，需要改原Packages/游戏资产/ProjectSettings却无明确例外。失败分析共同根因，不能关闭模块、吞异常或改历史有效测试制造通过。

### 10.6 7.R4：测试导航与一次整体验证/交付收口

**实际问题：** 主套件几十个散落回归难找owner；路径/使用说明和正式导出需要最后一起对齐。

**迁移范围：** 仅主Tests/EditMode、PlayMode根下文件/目录按 §6 分组；两份根asmdef不动。Tests/Core/Foundation/Integrations/Fixtures、所有场景/Prefab/Bundle不搬。批准后的Documentation/Help仅增量同步最终导航与已确认失实描述；长期蓝图状态更新由用户授权后的正式流程处理。

**技术影响：** 测试FullName/方法/程序集/filter、测试资源路径与序列化身份不变；产品源码/默认资源/Analyzer不改。测试分组不会纳入导出。

**实施清单：**
- [ ] 逐文件按 §6 移动测试及meta；互相访问Resource测试整组移动；不为了删跨测试引用搬夹具类型或改asmdef。
- [ ] 对比测试发现集合的test FullName/assembly/method，确认零丢失/零重复；执行直接路径/导出/Help/SourceIndependence/CoreIdentity/FoundationBoundary/默认资源身份门禁，不重复各功能完整历史套件。
- [ ] 核对受影响的真实Editor编译器Analyzer输入：Core与其消费者继续正向；点名 Project.Game.MainModule、Runtime、Foundation、普通Editor、Help继续负向。Core/DLL/marker/SDK未变不重建SDK；若证据显示变化则停止，升级双轴+七项红绿专题。
- [ ] 在7.O1通过后一次真实ExportUnityPackage并解包：六根、剩余Plugins完整递归、**无Sirenix/Odin插件payload**、路径/GUID/许可证、唯一Analyzer/meta、正式Prefab无MissingScript、排除Tests/Integrations/SDK/NuGet/游戏均成立。旧本轮迁出路径不得在包内出现，不要求清除历史说明与负例里的所有旧词。复用O1受影响域/Player证据，不机械再构建。
- [ ] 检查README/专题入口与Help导航；源工程路径正确，仓库专用`.kilo/plans`链接明确而不冒称包内链接可用。公共再分发授权仍单独阻断，不将内部导出通过写成商业授权通过。
- [ ] 最后只读复核Git变化、GUID/源码归属/公开身份、不变Core/Kernel/Help资源及SDK输入；交付每批实际验证与未验证项，不默认commit/push。

**独立停止：** 测试发现缺失/重复、fixture/Acceptance场景被迁改、平台/define或测试asmdef发生未经审批变化、Analyzer实际范围变化、正式包缺资产/许可证或包含禁入内容。仅路径批没有运行时/API/编译条件变化时不默认全Player构建或清Library；新增/拆程序集、依赖或平台条件才安排针对Player/IL2CPP的新验证。

## 11. 重点回归风险

| 条件 | 必须保持的结果 | 门禁所有批次 |
|---|---|---|
| 物理路径变而资源键不变 | Action原Prefab可加载、Core两个默认键唯一，创建器不重建新GUID | 7.R2；7.R4资源/导出 |
| 同namespace跨程序集 | ObjectPool仍Foundation，SafeObjectPool仍Runtime，Core不引Runtime；外部类型身份不漂移 | 每批源码归属；7.R4边界 |
| SourceIndependence/显式脚本白名单 | 只映射原白名单，独立Audio/Action探针不吞Core、Editor或整Runtime | 7.R1/7.R2/7.R3 |
| Analyzer缺符号静默失效或范围扩大 | Core正向与真实游戏/Runtime负向均成立，不能以零Warning假通过 | 7.R4；若技术身份变则另专题红绿 |
| 同名资产/用户dirty场景/已有生成文件 | 不接管、不覆盖、不保存用户场景；未知生成资源先停止 | 每批迁移前；7.R3生成边界 |
| Odin退出导致展示或数据退化 | 42个标注的真实效果被原生Editor保留，20字段/UnityEvent持久数据不变，全部平台变体退出 | 7.O1；7.R4剩余Plugins与无Sirenix门禁 |

## 12. 待用户审核的明确决定

1. **批准推荐A与四个路径/导航批次，以及用户已指定加入的独立7.O1功能/依赖批**，还是采用更少物理变化的C。主推荐A；B仅在明确逐模块发行需求后重开，不作为默认实施。
2. **批准保留现有技术身份与已成立Core/Kernel/Help边界**，包括历史Core.* namespace与Core中的通用组件/Editor支撑；它们是公开身份债务，不是下一代理可以自行清理的项目。
3. **确认阶段8研究是否及如何独立Resource程序集/公共支撑闭包**，但本Plan不预设其装配方式、API、租约或资源组语义，也不授权实现，不改当前XFAB/ProjectName。其他高风险候选 §7.3 默认不实施。
4. **确认Odin等价替换设计**：只实现EventListener实际使用的42个标注效果，不造mini-Odin；插件整树删除方向已由用户补充确定，六根与剩余Plugins递归机制不变。Editor Enhancers许可与包内Documentation导航仍分别保留发行/阶段9门禁。
5. **ProjectSettings边界明确保留**：用户未确认清理五个惰性Odin宏的例外，本Plan不安排修改；若日后要求连该元数据也清零，需单独明确授权，仅删五token并保留所有其他用户设置，不推定本轮插件退出扩大为配置重写。

批准本候选只确定设计与允许后续按批推进的范围，具体Git写操作仍需用户授权。不将审核候选与同意立即迁移、commit或清理既有工作混为一事。

## 13. 证据导航与独立审查

重要结论以以下源证据复核，路径均相对 `Assets/YLFramework/`（SDK路径另注明）：

- 编译闭包：Runtime/YLFramework.Runtime.asmdef:4-8；Runtime/Foundation/Kernel/YLFramework.Foundation.Runtime.asmdef:4,8-13；Kernel/Internal/AssemblyInfo.cs:3-4；Runtime/Core/YLFramework.Core.Runtime.asmdef:4-8；Editor/Core/YLFramework.Core.Editor.asmdef:4-19。
- Core内聚：Runtime/Core/MVC/Module.cs:111-125；MVC/ModuleManager.cs:202-216；Views/View.cs:406,444；Views/ViewManager.cs:205；UI/Panel.cs:16；UI/PanelManager.cs:329,390；UI/UIRootContext.cs:70-89。
- 公共支撑与身份：Runtime/Foundation/Kernel/Pool/ObjectPool.cs:5；Foundation/Pool/Object/SafeObjectPool.cs:10-36；Foundation/Singleton/Mono/MonoSingleton.cs:2,50,61；Kernel/Internal/SingletonSessionState.cs:20-45,73-95。
- Resource与Audio交叉：Runtime/Resource/Framework/ResLoader/ResLoader.cs:72-84；Framework/ServiceManager.cs:14-17,58-71；Framework/ResTable.cs:20-22；Runtime/Audio/ResLoaderAudioClipProvider.cs:68,78；Audio/Config/AudioClipLibrary.cs:58,100-108。
- Resource成熟度/路径：Runtime/Resource/HotUpdate/HotUpdateManager.cs:46,83-94；HotUpdate/FakeResServer.cs:60,82；Implementations/AssetBundleSupport/ConfigFile/ResData.cs:141-174；Utilities/ZipFileHelper.cs:5,271-299。
- Action与工具：Runtime/Action/Internal/ActionGlobalHost.cs:13-15,66-68；Action/Internal/AssemblyInfo.cs:3-8；Audio/Actions/PlaySoundAction.cs:3,8,97-107；Runtime/Tools/Table/Table.cs:112；Tools/FSM/Core/FSM.cs:465,549；Editor/Resource/ResDataCodeGenerator.cs:2-9,41-95。
- Editor错位/导航：Editor/Core/Settings/ProjectSettingsScriptableObject.cs:89；Core/Utilities/Preview/EditorAssetTool.cs:5,56-60；Editor/Action/ScreenTransitionResourceCreator.cs:16,35；Editor/FoundationToolsHelpProvider.cs:14-19,25-45；Editor/ExportUnityPackage.cs:16-24,66。
- 身份与路径测试：Tests/Foundation/EditMode/FoundationBoundaryTests.cs:17-57,91-107；Tests/Core/EditMode/CoreIdentityTests.cs:15-25,81,124-161；CoreDefaultResourceTests.cs:14-35,70-88；Tests/Core/PlayMode/TimerObjectPlayModeTests.cs:104-121；Tests/EditMode/Audio/AudioSourceIndependenceTests.cs:33-54,123-126；Tests/EditMode/ExportUnityPackageTests.cs:50-165,255-268,300-326。
- AnalyzerSDK：`Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs:28-55`；CoreUsageAnalyzer.cs:11-51；Tests/AnalyzerFixture.cs:30-55；build.ps1:17,27,49-68；Directory.Build.props:3-14；global.json:1-6。
- 可选叶子/测试约束：Integrations/DOTween/Runtime/对应asmdef:4-11；Integrations/XFAB/Audio/YLFramework.Audio.XFAB.asmdef:4-12；Tests/Core/Acceptance/YLFramework.UI.Acceptance.asmdef:4,11；Tests/Core/Fixtures/Editor/CoreSmokeAssetFixture.cs:9,22-29。
- 交付/文档：Runtime/THIRD-PARTY-NOTICES.md:45-65；Runtime/Tools/Json/LICENSE.md；Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md；Documentation/Modules/Extensions.md:77-101；Resource.md:125及历史测试清单；Architecture/README/IntegrationPlan中仓库Plan链接与导出边界。

本轮五个互不重叠只读研究域为Foundation/宿主、Core、全部Editor、Analyzer/配套交付、Action/Tools，主研究Resource/Audio/Save/HTTP并交叉核实关键边。已由未参与以上研究的独立审查者完成一次架构Review，方向有条件通过；成立的修正已写入候选：FakeResServer路径与persistentDataPath区分、R1同批源码闭包门禁、R2资源物理路径断言、Audio真实文件/计数、XFAB适配实际路径、文档收口与阶段8装配措辞。它不是Unity运行验证或实施授权。

用户补充后另分三域研究Odin生产用途/等价展示、外部Package/游戏依赖、插件/交付边界，主通读EventListener交叉核实。外部与交付报告曾将用途简化为“空using/仅HideMonoScript”，以真实源码的**42个标注和20字段**纠正后整合本Plan。随后按用户“继续完善Plan、不实施”的要求，在同一独立审查会话中仅追加Odin/版本边界复审，不重复全架构；成立修正已纳入：现有Foundation引用白名单漂移的实测前置、派生字段原生兜底及测试探针、自动/人工验收区分、manifest/lock/缓存证据归属。两次审查均为静态评审，不是实际Unity编译/GUI/Player验证。

## 14. Odin补充需求：功能等价、删除边界与验证

### 14.1 实际使用矩阵与外部边界

| 范围 | 本轮真实证据 | 处理 |
|---|---|---|
| Runtime/YLFrameworkRoot.cs:1 | 空`using Sirenix.OdinInspector`；MonoSingletonPath/DefaultExecutionOrder是YL/Unity自己的标注 | 仅删空using，无功能替换或Root行为改动 |
| Runtime/Foundation/Events/Unity/EventListener.cs:3,74,242-310 | HideMonoScript×1、FoldoutGroup×1、TabGroup×20、LabelText×20，共42个Odin标注 | 一个原生CustomEditor等价实现，Runtime保留SerializeField/事件字段 |
| 上述20字段 | 私有[SerializeField] UnityEvent<PointerEventData/Collision/Collision2D/Collider/Collider2D>，MonoBehaviour基类 | 原生序列化；无OdinSerialize/SerializedMonoBehaviour等，不需要数据格式转换 |
| AudioSourceIndependenceTests.cs:83-90,122-143 | 显式File.Exists、添加/允许Sirenix Attributes DLL；闭包含Root但不含EventListener.Unity目录 | 同批收口白名单，去掉根的空using；闭包范围不无故扩大 |
| EaseUtilityTests.cs:85-98 | Runtime依赖允许表含Sirenix.OdinInspector.Attributes，却未列实际Kernel依赖YLFramework.Foundation.Runtime | 先实跑基线并读取引用集；缺Foundation导致的失败按既有测试契约同步处理，再移除Odin允许项与加负向守卫，不弱化其他断言 |
| 产品/游戏资产 | Sirenix DLL/config/folder GUID命中均在插件自身树；EventListener脚本GUID`b5a1a5397384497b91f13b8a4a2a91ff`未发现现有Prefab/Scene消费者；框架9份自有Prefab的脚本均为YL/UGUI | 现有范围没有Odin数据/资产迁移需求；仍保持EventListener公开身份供仓库外消费者 |
| 游戏/原XF/XFAB/XFFSM及其他实装Package | 定向研究未找到直接Odin源码消费者；游戏MainModule.asmdef:16-17 overrideReferences=true且无precompiledReferences | 无需改游戏代码/资产、原Package、UPM manifest/lock，不创建Embedded/fork |
| NuGet及包内二进制DLL | 未做PE硬引用分析，文本/来源面不能证明全部二进制无依赖 | 删除前可用实际程序集引用检查，删除后全项目编译、加载日志与主目标Player构建为门禁；发现未知依赖停止，不越界修包 |
| Plugins/Sirenix | 唯一导入树，Git ls-files已确认受版本控制；11 DLL含Serialization/Utilities同名平台变体，另有模块/配置/4个demo包/link.xml | 删除整树及根meta，不只删除Attributes或Editor DLL；其余插件保留 |
| ProjectSettings.asset:587 | Standalone仍含5个ODIN_INSPECTOR*；当前源码没有对应条件消费 | 原禁改约束优先，未取得例外授权，作为惰性元数据豁免而非活跃插件依赖 |

当前插件InspectorConfig启用了全局Odin Inspector。移除后其他组件回落Unity原生/已有CustomEditor，这是退出的预期全局效果；不承诺复刻Odin对所有未知第三方类型的整套自动绘制。没有明确使用的Odin按钮/条件/只读/列表/序列化系统，不为它们建立替代框架。不得把“删除插件”描述为已经验证全平台或仓库外消费者兼容。

### 14.2 推荐等价设计与字段表

唯一正式功能新增文件：`Editor/Foundation/Events/EventListenerEditor.cs`，普通`YLFramework.Editor`程序集，建议类型`YLFramework.Editor.EventListenerEditor`；直接回归在`Tests/EditMode/Editor/Events/EventListenerEditorTests.cs`，仍属现有主EditMode程序集。明确采用`[CustomEditor(typeof(EventListener), true)]`与`[CanEditMultipleObjects]`支持派生类、多对象；Unity2022.3的editorForChildClasses默认false，不能遗漏第二参数后声称已支持派生类。不引Core.Editor、不新增Runtime展示属性、不新增asmdef/asmref/friend。

- `OnInspectorGUI`通过SerializedObject.Update/ApplyModifiedProperties；PropertyField绘制原UnityEvent并包含子项，使用GUIContent中文标签。不能用反射直接改字段或新建事件替换持久列表。
- 不绘制m_Script；折叠标题“Unity事件中心”，本方案默认展开并在Editor会话内保持状态；三个页签顺序为鼠标事件、碰撞事件、Trigger事件，不把未选页字段删除/重置。
- 以下固定映射只服务该组件；在已知20字段组之外，以serializedObject.GetIterator遍历其余可见的顶层序列化属性，排除m_Script与已知20字段的propertyPath，按原顺序PropertyField(includeChildren=true)绘制。遍历不重复进入已由PropertyField绘制的子属性；派生类型额外SerializeField不能被吞掉，不接管全项目Inspector。

| 页签 | 字段（保留原顺序） | 标签（保留原文） |
|---|---|---|
| 鼠标事件 | pointerEnter | 指针进入 |
| 鼠标事件 | pointerExit | 指针离开 |
| 鼠标事件 | pointerClick | 指针点击 |
| 鼠标事件 | pointerDown | 指针按下 |
| 鼠标事件 | pointerUp | 指针抬起 |
| 鼠标事件 | beginDrag | 开始拖拽 |
| 鼠标事件 | drag | 拖拽中 |
| 鼠标事件 | endDrag | 结束拖拽 |
| 碰撞事件 | collisionEnter | 碰撞进入 |
| 碰撞事件 | collisionStay | 碰撞保持 |
| 碰撞事件 | collisionExit | 碰撞离开 |
| 碰撞事件 | collisionEnter2D | 碰撞进入2D |
| 碰撞事件 | collisionStay2D | 碰撞保持2D |
| 碰撞事件 | collisionExit2D | 碰撞离开2D |
| Trigger事件 | triggerEnter | Trigger进入 |
| Trigger事件 | triggerStay | Trigger保持 |
| Trigger事件 | triggerExit | Trigger离开 |
| Trigger事件 | triggerEnter2D | Trigger进入2D |
| Trigger事件 | triggerStay2D | Trigger保持2D |
| Trigger事件 | triggerExit2D | Trigger离开2D |

`[CanEditMultipleObjects]`和SerializedProperty保留原生多选/Undo/Prefab语义，实际UnityEvent多对象列表行为以Unity实现为准；不是许诺额外Odin高级多选能力。页签/折叠状态不写进组件，不要求保存Odin全局偏好或像素级主题。展示特性的移除是明确功能替换，事件回调、payload校验、注销/销毁所有权完全不改。

仅PropertyDrawer无法表达跨字段页签；仅Header/平铺字段会丢功能。Unity2022.3的InspectorName用于枚举值，不能当普通字段LabelText替代。本轮已读取一手资料：

- <https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorGUILayout.PropertyField.html>
- <https://docs.unity3d.com/2022.3/Documentation/ScriptReference/CanEditMultipleObjects.html>
- <https://docs.unity3d.com/2022.3/Documentation/ScriptReference/InspectorNameAttribute.html>
- <https://docs.unity3d.com/2022.3/Documentation/ScriptReference/CustomEditor-ctor.html>

### 14.3 同批清单、导出与许可

按 §10.5 顺序，生产替换/测试先完成再删插件，前后均核对功能；不把Odin留到阶段9或只从导出根排除，而是从项目资产中真实移除。

直接文案消费包括：README.md:18,83；Architecture.md:41,48,147；IntegrationPlan.md:87的当前恢复条件；Modules/Attributes.md:9与Save.md:70；R1已移动的Editor/Utilities/Help/FoundationToolsHelpProvider.cs旧:45；SerializedDictionaryEditorTests.cs旧:248注释。只更新现状与必要能力说明，保留合理的“已退出Odin”历史/负向说明，不要求整个仓库Odin单词清零。

`ExportUnityPackageTests.cs:254-257`是Plugins实际递归集合与包内容的动态等价校验，删除Sirenix后仍适用；六根原值不改。最终可加/复用一个明确无Sirenix payload的负向门禁，不能删Plugins整体或顺手清其他商业插件。Odin专属许可证/Readme/Demos随依赖整树退出，不转抄Odin商业实现；JSONObject/SharpZipLib/XF/QFramework等保留许可证不受影响。Editor Enhancers授权门槛不因Odin退出自动解除。

AudioSourceIndependenceTests、总览Help、README/Architecture、Export测试与R1～R4有共享写范围，因此所有批次串行，一个文件同一时间只有一个写者。最终导出集中在R4一次执行，复用O1Player/功能验证；旧已有Odin正向证据不得当新无Odin证据。

### 14.4 配置边界与豁免

原任务禁止改ProjectSettings。五个实际token为`ODIN_INSPECTOR`、`ODIN_INSPECTOR_3`、`ODIN_INSPECTOR_3_1`、`ODIN_INSPECTOR_3_2`、`ODIN_INSPECTOR_3_3`，仅在Standalone定义；全文源码未找到条件使用。本轮对“未来是否允许仅删这五token”的澄清未得到确认，因此**当前方案保留它们并明示残留**，不认为插件移除自动授权改设置。

“整项目插件退出”的门禁是无插件实体/活动源码与二进制引用/导出与Player payload；未使用的五个符号不加载插件、不恢复已删资产，但不声称ProjectSettings文本完全无Odin。若用户日后明确要求严格符号清零，只作为最小例外修改该五token，保留同行MOREMOUNTAINS、DOTWEEN、UNITY_MCP及全部其他用户设置；不更改任何其他ProjectSettings、不清理所谓陈旧宏，不手改packages-lock。当前实施批没有等待该选择才能完成真实插件退出的依赖。

## 15. 方案 A 实施结果（2026-10-13）

实际分支为 `fusion/7-consolidation`，HEAD 在实施前后均为 `0a39928e76e36564a4b354e370b0d939727b7aad`。Core 7.2 等既有未提交成果作为真实启动基线保护，没有重新执行旧迁移。以下为工作区技术结果，不表示用户最终验收、提交、里程碑或阶段8授权。

| 批次 | 实际修改与验证 |
|---|---|
| 7.R1 | Tools → Utilities 与非领域 Editor 入口归口；269 个移动文件内容/meta 保持。定向 EditMode 82 项通过。 |
| 7.R2 | Action/Audio/Save/Http 及对应 Editor 进入 Systems，退出空 Services 容器；同步创建器与直接路径断言。定向 EditMode 109 项、最小 PlayMode 5 项通过。 |
| 7.R3 | Resource 两树仅含源码/meta，无 Remote、生成代码、Bundle 或业务配置；114 个文件仅归口外边界，FakeResServer 两处开发路径同步。定向 EditMode 37 项、真实 Resources 资源链 PlayMode 1 项通过；真实生成源码与临时程序集探针按方法明确核验，夹具清理。 |
| 7.O1 | 原生 EventListenerEditor 进入现有普通 Editor；移除 42 个展示标注及两个空 using；整树删除 Sirenix 的 115 个文件和根 meta，其他 Plugins 保持。删除后原生 Editor 5 项、运行契约 4 项、源码闭包与引用门禁 4 项，共 13 项 EditMode，以及生命周期 PlayMode 14 项通过。 |
| 7.R4 | Architecture/README/模块导航/许可链接及导出哨兵收口；Export/Help/CoreIdentity/CoreEditorIdentity 共 26 项通过，192 条本地链接可定位；实际六根递归导出/解包逐项核对内容、GUID/meta、唯一 Analyzer 和排除项。 |

R2 曾因 ApiHelp 两个 AssemblyBuilder 探针缺少既有合法 Foundation 引用停止；用户随后精确授权同步。仅允许真实 Foundation Assembly.Location，保留同名伪 DLL、测试/optional 隔离断言；71 项 ApiHelp 门禁通过。O1 按既定允许项同步 EaseUtility 的 Foundation 依赖并明确拒绝 Sirenix，未放宽产品 asmdef 或公开 API。

O1 的 GUI 为工具辅助的实际 Windows 原生 Inspector 操作，不以映射反射代替：三页签与全部中文标签、折叠、隐藏 Script 属性、派生字段、多选修改、监听新增/目标拖入/方法修改/删除、键盘 Undo/Redo，及自有临时 Prefab 原生 Apply/Revert 均核验。窗口、PreviewScene、对象、临时 Prefab 与选择/会话状态已清理或恢复；未保存用户场景。

一次实际 Windows64/Mono Development 构建使用未修改的 Main 场景及自有临时测试程序集/Resources 持久事件夹具，BuildReport 为 Succeeded、0 errors、203 warnings。MCP 因构建期间重载重发执行导致出现“目标目录非新”工具错误，已直接读取首次生成的真实 BuildReport/产物确认，没有覆盖或另建第二次 Player。先以 Null Graphics 启动暴露无图形 Shader 噪声，同一产物改用 D3D11 再启动；日志明确为 Main 启动、持久目标/方法、UnityEvent 回调与取消、无 Sirenix 引用 PASS，进程自行退出。工程内临时 Player 夹具已删除。产物位于 `C:/Users/Kano/AppData/Local/Temp/kilo/YL7-O1-Player-20261013/`，不是普通发布包或干净载体导入证明。

迁前 SHA-256 与加载身份快照核验：累计 585 个移动文件；444 个原脚本 GUID/类型绑定、548 个既有公开类型/序列化字段、Editor/Player 源码归属与 asmdef 图保持。O1 只新增普通 Editor 源码及直接测试，precompiled references 唯一变化为移除 Sirenix。Analyzer 的 DLL/meta/GUID/标签、平台禁用及 Runtime 非引用保持；实际 Editor 与 Player `.rsp` 仍只给 Core 与其引用消费者输入分析器，普通 Runtime/Foundation/Help/ZipTool/游戏不获该输入。无额外 Root、Manager、Pool、Timer 或资源权威。

独立只读审查无可行动问题，未重跑测试或操作有状态 Unity。`git diff --check` 通过；原 Package、游戏/Scene/正式 Prefab 内容、Samples、Core 原有成果、SDK、Packages、ProjectSettings、`.gitignore` 与 `.kilo/kilo.jsonc` 保持启动基线，所有临时工程夹具清理。没有访问 test-worktree，也没有 Commit/Push/Merge/Tag/Reset/Clean/Stash/Rebase 或分支切换。

剩余边界：五个惰性 Odin 宏按授权保留；已有 XML/过时 API 等构建 warnings 未通过关闭诊断或改无关源码处理。Core 当前 XFAB 依赖与 ProjectName 语义不变；未执行阶段8资源融合、最终干净载体 import、其他平台/IL2CPP/AOT、完整人工游戏流程或公开再分发许可验收。技术门禁不替代这些后续证明。

## 16. 后续优化修订：两个主体程序集（2026-10-13）

### 16.1 定位、现场与授权边界

**Goal：** 保留 Foundation/Core/Systems/Utilities 的职责导航，以一个主体 Runtime、一个主体 Editor 降低整包使用成本；修正 MVC 归属、功能平铺与大类导航，不把目录数量等同程序集数量。

**状态：** 本节两主体修订及§16.9已获用户明确授权并实施，技术验收结果见§17.12；替代先前Q0-Q8方案，不重跑历史7.1/7.2/7.R1-7.R4/7.O1。授权仅限本节明确的产品程序集退役、Analyzer更新、职责迁移与样例删除，不把允许类型归属迁移扩大为任意程序集/资源删除；阶段8仍未授权；Git 写操作已随阶段 7 最终收口单独授权执行（`milestone/7-structure`）。

实际现场仍是 `fusion/7-consolidation`，读取的 HEAD 为 `0a39928e76e36564a4b354e370b0d939727b7aad`，暂存区为空，既有大量未提交工作保留。暂停前 Q0 已生成 2,829 文件/549 公开类型/445 原 MonoScript 绑定基线，Unity `2022.3.62f3c1`、Windows64/Mono、Main 未脏；CoreIdentity 10 项通过；随后取消的 CoreModule PlayMode 不计通过。基线文件为 `C:/Users/Kano/AppData/Local/Temp/kilo/YL7Q-baseline-20261013-1254.json`。这些只用于重启时差异核对，实施必须重新读取实际状态，不复用过期假设。

当前活跃包核对为 UGUI `1.0.0`、URP `14.0.12`、Test Framework `1.3.9`，不按 manifest 的另版本声明擅自升级。操作限定自有 YL 源码、相关测试/样例引用、Analyzer SDK 的直接受影响部分和导航/许可文档。原 XF/XFAB/XFFSM Package、游戏、正式 Scene/Prefab/Bundle 内容、Packages/ProjectSettings、`.gitignore`、`.kilo/kilo.jsonc` 继续保护；不访问 test-worktree，不执行 Git 写操作。阶段8资源功能融合、XFAB 去依赖、ProjectName 重设计、五个惰性 Odin 宏清理均不在本节。

用户于 2026-10-13 补充明确退役 `Assets/YLFramework/Samples/ColorSelector`。该样例整树及根 `.meta` 是允许删除的唯一新增样例资产例外，详见 §16.9；正式控件、窗口、`Prefabs/ColorSelector.prefab` 和其他资产不因此获得删除授权。

### 16.2 程序集决策：主体精确为两个

| 当前产品程序集 | 修订目标 | 处理理由 |
|---|---|---|
| `YLFramework.Runtime` | 保留，全部自有运行主体归属此程序集 | 消费者只需一个主要运行引用；保留 asmdef GUID `2d319060abc80e3449e84285dd7fb703` |
| `YLFramework.Editor` | 保留，全部自有编辑器主体归属此程序集 | `includePlatforms=[Editor]`，只引用主体 Runtime 及实际外部依赖；保留 GUID `62b3fd14a5c704743835cacd6a6870a1` |
| `YLFramework.Foundation.Runtime` | 并入 Runtime，退出 asmdef 与配套 meta | Foundation 是逻辑机制分区，不再作为单独交付/编译内核 |
| `YLFramework.Core.Runtime` | 并入 Runtime，退出 asmdef 与配套 meta | 保留 MVC/Module/UI 组织，不靠额外 DLL 表达“重要代码” |
| `YLFramework.Utilities.ZipTool` | 并入 Runtime，退出 asmdef 与配套 meta | 默认整包包含的内置工具，没有已确认的独立交付要求；保留 SharpZipLib 实现、公开类型、许可证 |
| `YLFramework.Core.Editor`、`YLFramework.Editor.Help` | 并入 Editor，退出两份 asmdef 与配套 meta | Core 工具与共享 Help 仍有功能目录，不需要拆成独立 DLL |
| 聊天候选 `YLFramework.UI.Controls` | 不创建 | 控件独立性先体现为源码职责和最小合同，不为假设 DLL 交付增加一个边界 |

产品主体由原 7 个归为 2 个。退役 asmdef 的 GUID 不转赠或伪造为其他资产；现有脚本/资源 GUID 保持。不保留空程序集、兼容类、转发壳、asmref 分区或第二实现来维持旧数字。

**有实际原因的例外：** 已有 EditMode/PlayMode/Editor/Acceptance/fixture 测试程序集保留，避免 NUnit/TestRunner 混入普通载荷；DOTween、XFAB Audio 等可选集成保留其独立开关和第三方引用，不纳入主体；其他 Samples 若存在则先保留编译所有者，仅更新失效引用，`YLFramework.Samples.ColorSelector` 随明确退役的样例退出；第三方插件及 Packages 的 asmdef 不在自有合并范围。Analyzer SDK 仍是仓库根 Tools 下的独立构建工程，Analyzer DLL 不作为 Runtime 运行引用。因此“两个主体”不等于全工程只有两个 asmdef。

主体 Runtime 引用为实际必要依赖的并集，移除已退役的自有引用，当前必须明确包含 `XFABManager` 与 `UnityEngine.UI`；Editor 的自有主体引用归为 Runtime，保留其实际 UGUI/XFAB 等外部需求，不采用任意程序集前缀白名单。当前 Core 仍依赖 XFAB，所以仅消费事件/池也会承担整个 Runtime 的编译依赖，这是本次简化的已知代价，不宣传为零依赖或模块可裁剪交付。

### 16.3 职责结构保留，编译边界不随文件夹繁殖

```text
Runtime/                              YLFramework.Runtime
  Foundation/
    Async/                            协程/等待机制
    Attributes/
    Binding/{Collections,Prefs,Unity}/
    Events/{Global,Internal,Subscriptions,Unity}/
    IOC/
    Pool/{Abstractions,Collections,Factories,GameObject,Object}/
    Serialization/
    Singleton/
    Timing/{Components,Internal}/
    Update/
    Utilities/
    Internal/
  Core/
    Modules/Requests/
    MVC/Models/
    MVC/Controllers/
    MVC/Views/{Internal,Parameters,Components}/
    Events/
    UI/{Windows,Input,Roots,Tips,Adaptation}/
    Attributes/                       marker 类型保持公开身份
    Analyzers/                        唯一 Analyzer 产物，不是运行 DLL
    Resources/                        默认资源路径/键/内容保持
  Systems/{Action,Audio,Http,Resource,Save}/
  Utilities/UI/ColorSelector/          控件与输入合同，无新 asmdef
  Utilities/{Extensions,FSM,Grid,Json,Table,ZipTool}/
Editor/                               YLFramework.Editor，Editor-only
  Foundation/
  Core/
  Systems/
  Utilities/
  Distribution/
  Help/
```

这不是要求将单文件各放一层：按有独立变更原因的功能分组，不为对称增加空目录，不把 public API 全部塞进 Models。根级 asmdef/csc.rsp/YLFrameworkRoot/许可汇总保持根入口。多个原 Events/Pool 目录合并为唯一功能族，Kernel 清空退出；删除目录前检查其 GUID 引用，不将 Kernel 的目录身份转给另一个目录。

Core 的 Module 家族与启动请求进入 Modules；Model/Controller/View 各自按 MVC 归属，ViewManager 与加载/参数配套保持同一事务；CoreEvents/EventManager 是 Core 的事件入口，不移作第二总线。Panel 仍继承 View；UI 的窗口、根、输入、Tips 与适配按职责归口，默认 Canvas/EventSystem 的 Resources 路径、GUID 和内容不动。

`TimerObject` 属 Timing 的 Unity 组件适配，三物理/动画 Listener 属事件 Unity 适配，`CloseView` 属 View 组件；合并后其路径迁移不再改变类型的程序集归属。它们保持行为、字段与名字，Listeners 的 LayerMask/动画用途不合并进 EventListener，Obsolete 不作删除依据，也不顺手改弃用文案。

文件聚集仅优先整理 Binding Prefs/集合/Unity 桥接、Core UI 的功能组、Resource Editor 的 Build/Service/CodeGeneration/Data、订阅注销族等明确变更原因。Resource Editor 仅路径批，不改 owner/Bundle/生成输出/加载与清理算法，不把它解释成阶段8。

### 16.4 大类导航与控件耦合仍做，但取消无用抽象

`Module`、`ViewManager`、`PanelManager` 保持原唯一类型、字段与事务权威，按明确职责拆少量 partial：

- Module：主身份/生命周期、Models、Controllers、Views、Panels 入口；保留 `Module.cs` 及其 meta，不另建 manager/服务；
- ViewManager：注册与退出、加载、查询；
- PanelManager：会话/根、加载、排序遮挡、输入策略；
- View（当前约 456 行）、LoadViewRequest 等不为风格统一继续拆分；partial 只改善可导航性，不能宣称状态机已重设计。

每个方法、字段、属性、虚拟槽与 marker 只声明一次；状态字段仍集中，公开 API、初始化顺序、异常及清理顺序保持。MonoBehaviour 原主文件保留正确类名和原 GUID，新 part 由 Unity 生成独立 meta；不得复制主脚本 GUID。同批验证跨 part 生命周期、源码/metadata Analyzer 与原 MonoScript 绑定。

ColorSelector/ColorWheelGraphic/ColorSelectorCursor 进入 Utilities/UI/ColorSelector，ColorSelectorWindow 留 Core/UI/Windows。全部仍在主体 Runtime，所以不承诺可以只取一个 UI DLL。控件不应具体依赖 Panel 的源码策略；允许一个最小 `YLFramework.UI.IUIInputGate`（`bool IsInputAllowed { get; }`），由 Panel 显式实现 `IsLoaded && !IsInputBlocked`，不增加全局 provider、管理器、注册表或控件基类。

原两参 `BeginSession(Color, Action<Color>)`、CurrentColor/EndSession、初始不通知、RGB 校验不破坏健康会话、pointer/区域锁定、alpha、尺寸变化、回调重入、Close 清回调均保持。祖先门禁只在原 BeginSession 时点以原默认 active 搜索语义解析；保存 Component+接口以保 Unity 假空销毁语义，每个输入事件现算，不缓存 bool。迁移不顺手修强 inactive 祖先行为；若接口查找不符合 Unity 实际行为，就停止本控件批而不是静默改变合同。新增 N 个门禁实现者不是错误，最近正式宿主选择与真实 Panel 既有优先级必须实测，不制造任意命名匹配。

### 16.5 Analyzer 是必需迁移，不是可忽略配置

真实 `AnalyzerSymbols.cs:34-35,49-55` 硬绑定 Core/基础程序集，且 ObjectPool 被要求为 metadata。主体合并后这三个条件全部变化；只删 asmdef 会导致解析器无候选或精确 owner 豁免失效，不能以“没有诊断”当成功。

新候选只绑定 `YLFramework.Runtime` 中的真实 marker/View/LoadViewArgs/LoadViewRequest/ObjectPool 符号，namespace/marker 类型名字保持；同时覆盖分析当前 Runtime 源码和消费者的 metadata 两种情况。ObjectPool 必须从已确认的主体 symbol 获取，不按外部同名类或 `Core.*` 前缀猜测，不再一概排除 source location。poolOwner/requestOwner 仍要求精确字段形状及 `SymbolEqualityComparer` 身份，不放宽成名字相等。

直接受影响的 SDK 文件为 `Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs` 及 Tests/{AnalyzerFixture,OwnershipRulesTests,CallRulesTests,AnalysisBoundaryTests,PerformanceTests 等实际引用旧身份处}。SDK name/target/Roslyn 版本、YL0001-YL0007 的规则、消息/严重性、许可保持；使用既有 build.ps1 生成产物，不手补二进制、不升级工具链。输出 DLL仍位于 Core/Analyzers，GUID `471f81c4273ee66429677df44cbd6960`、RoslynAnalyzer 标签、平台禁用、非运行引用保持；DLL 内容校验自然随源码构建变化，不虚报旧 hash 保持。

Unity 输入作用域明确变成主体 Runtime 及其引用者（含 Editor、剩余 Samples、游戏或测试消费者的实际引用集合，不包括已退役 ColorSelector 样例）；不能再要求普通 Foundation/Runtime/引用 Runtime 的游戏都无输入。不把规则作用于原 XF/其他无主体引用程序集；全程序集获得输入不等于所有类型都需被规则诊断，未使用真实 marker 的普通代码不产生虚假诊断。

必须增加/更新主体源码、metadata 消费者、伪同名 marker、伪 owner、泛型精确 owner、partial、无主体引用、取消/并发、AD0001 等门禁，并以实际 Editor/Player `.rsp` 和正负消费者源码探针证明有效；不删除负向断言，不加入大范围 pragma/关闭规则来消除扩大输入后的告警。

### 16.6 迁移清单与不可弱化的契约

保留未退役脚本/资产 GUID、namespace、类型名、成员/字段形状、菜单、Resources 键、Help provider id/配置键、运行契约。准许变化精确为五份退役产品 asmdef/meta、归属这些程序集的类型 AQN、相应引用/IVT、Analyzer 的主体身份/输入作用域，以及 §16.9 明确退役的样例源码/资源/程序集身份；不声称“所有程序集身份不变”。建立旧→新归属及退役清单，未列入的变化立即停止。旧二进制/AQN 消费者须升级/重编译；不提供兼容空壳，不承诺仓库外已发布二进制无迁移成本。

保留 Action 的实际 DOTween/Editor/测试 friend；退出 Foundation 的自指及已退役 Core friend；检查合并后重复 AssemblyInfo/特性，不为测试方便新增全友元白名单。全部自有 Tests/Samples/可选集成的引用只替换实际退役身份，测试程序集名和约束保持；不碰原 Package/Game asmdef。测试内显式 AssemblyBuilder 引用由旧多 DLL变成精确 Runtime DLL，仍拒绝同名伪 DLL/测试/optional；语义中立源码闭包仍直接编译被选择的源文件，不偷渡统一 Runtime DLL，仍验证 Editor 和 Windows Development profile。

ZipTool 失去原 `noEngineReferences=true` 的独立编译边界，应补或复用仅 BCL 的 Zip 源码编译探针证明其源级无引擎依赖；不要为了保住旧断言恢复第 3 个产品程序集。Foundation 不反向消费 Core/Systems/Utilities 的逻辑规则可用窄的依赖源码/符号探针、评审与源码闭包证明，不把内部实现私有化或复制来伪装编译隔离。

正式 Prefab/Scene 只加载检查不保存；AssetDatabase 脚本身份、Missing Script、字段绑定、UnityEvent 目标/方法、managed reference、JSON `$type`/AQN、反射 AssemblyTool 与 old Assembly.GetType 消费要定向查证。需要旧格式回归时使用自有临时副本，不能让自动迁移保存正式资源。MovedFrom 不等同普通 Type.GetType(oldAQN) 可用。若新方案必须修改受保护资产/配置或提供旧 DLL壳才能成功，停止报告，不覆盖工作。

### 16.7 新批次：S0-S7，取代未实施的 Q1-Q8

| 批次 | 唯一可验收交付 | 红绿与门禁 |
|---|---|---|
| S0 | 重读现场与身份清单；与暂停 Q0 比较，核对 5 份退役 asmdef 的所有引用/资源、549 类型等实际新基线 | 有效 Unity 编译/相关基线、保护 hash、API/字段/GUID/AQN 与实际编译图；取消运行不计绿 |
| S0a | 明确退役 Samples/ColorSelector 整树与根 meta，清理专属测试、样例导航和导出正哨兵 | 删除前无受保护外部引用，新增整树导出负断言；正式控件/窗口/Prefab hash与GUID不变，正式颜色交互与owner生命周期测试保持，实际编译/导出门禁通过 |
| S1 | Runtime 主体合并 Foundation/Core/ZipTool；SDK符号绑定及产物、相关引用/IVT 同批同步，不搬职责目录 | 先红主体所有权/新 SDK源码与metadata owner，保留 lookalike/无引用负例；SDK 全套与真实 Unity编译、Runtime无 Editor依赖、代表 MVC/事件/池/计时源码回归 |
| S2 | Core.Editor/Editor.Help 并入 Editor，准确 Editor-only 引用 | 先红三个旧所有者的准确 Editor归属；Help/原生Inspector/Core工具门禁、Player源码无 Editor主体、可选依赖仍隔离 |
| S3 | Core 的 Modules/MVC/Views/Events/UI 目录归口与三个大类 partial 拆分 | 路径/唯一声明/MonoScript/成员快照，Module启动关闭与异常、View会话/子树/重入、UI根/模态/焦点/input路径回归；新 partial 不改状态权威 |
| S4 | Kernel功能并回 Foundation；Binding/订阅族整理；TimerObject/Listeners/CloseView 按职责落位 | 精确新路径/旧空容器退出/许可、唯一类型与字段、Timing真实取消故障重入、LayerMask/动画/UnityEvent/作用域、双 profile 源码闭包；不强制提前提炼反向helper，因为此时没有编译环 |
| S5 | 通用 ColorSelector 控件与 Core 窗口适配分离，最小门禁合同，无新 asmdef | 先红祖先门禁与逐输入求值、active/inactive/销毁语义；现有控件几何/材质/alpha/重入+真实输入/遮挡/模态/重开/按钮、正式Prefab只读解析 |
| S6 | 其余明确混杂的 Editor Resource/Binding/UI 功能组、导航/Help/许可/导出哨兵收口 | 仅职责路径；菜单/生成路径/源码探针仍有效，禁做资源功能融合；链接与内容/meta/GUID条目、独立无Engine Zip源码探针 |
| S7 | 实际导出解包、编译输入、开发 Player 与独立审查，记录新身份/迁移限制 | UnityPackage六根规则保留、2主体与唯一码表、无退役asmdef/壳/旧运行DLL/测试意外载荷、Analyzer正负输入、一次 Windows64/Mono开发Player启动/持久事件/MVC/UI门禁、保护hash；报告未验证平台 |

执行顺序为 S0 → S0a → S1 → S2 → S3 → S4 → S5 → S6 → S7。S0a 在主体合并前完成，避免先迁移后删除样例引用。每批先证明必要新断言红态（纯搬移用直接路径/归属门禁），再实现、读新结果、检查 diff/meta/保护范围，绿后才能进入下一批。不为按最初聊天顺序而重复拆装程序集，不把目录/代码/规则完全无关的故障同时修。S1 的源码迁移与 SDK绑定是一个完整批，暂时不能用旧解析器“无诊断”作为中间验收；其他批只做所属责任。

### 16.8 自审、停止与交接

本修订保留六点职责改善，取消 Foundation依赖释放→独立基础程序集及 UI.Controls DLL这些在整包模式下没有必要的交付。主要真实代价是更大的 Runtime 重编译范围、基础逻辑失去编译期独立、当前 XFAB变成主体编译依赖、五个程序集身份退役、Analyzer输入扩展；都已列门禁，不把它们写成零风险优化。未来只有真实独立分发、可选/平台依赖或测量到的编译瓶颈才重新拆产品 asmdef。

停止条件：未知归属/依赖/反射/序列化、GUID/字段/资源键漂移、需要原 Package/游戏/正式资产/配置写入、SDK或Unity编译/门禁失败无法在本批范围解决、第二权威/兼容壳出现、实际 Analyzer输入/Player载荷与目标不符。停后保留现场，不自动 Reset/Revert/Stash、不吞异常、不关闭诊断；发布许可、干净载体、IL2CPP/AOT/其他平台及完整人工游戏流程仍不能从本批推定。

本节按授权已执行并通过§17.12记录的最终门禁；后续接续仍读取有效AGENTS与真实工作树，不沿用旧Q批程序集假设或重跑迁移。原§15及§17各次失败/暂停记录作为历史保留，不能替代当前源码的实际验证或授权范围。

### 16.9 补充：退役 ColorSelector 样例，不删除正式能力

用户指定删除 `Assets/YLFramework/Samples/ColorSelector`，包含 `Scripts/TestColorSelector.cs`、`ColorSelectorSample.prefab`、`Readme.txt`、`YLFramework.Samples.ColorSelector.asmdef`、子目录及全部配套 `.meta`，连同根目录 `ColorSelector.meta`。根目录 GUID 为 `d33818ff6e2077b489a7329d47d3e566`，样例 Prefab 为 `151b6b54d6304454880ee84ff559511b`，样例脚本为 `ff117a6c8f0101e408f313e4f6c48634`，样例 asmdef 为 `f9840bdb773148b4d9eddbd2b90046f1`。删除前重扫全部样例 meta 身份及类型/路径/程序集引用；本轮对已知四 GUID 的定向 Assets 文本扫描只找到样例 Prefab 自身的脚本绑定，但这不替代实施时完整检查。

此删除不包含 `Runtime/Core/UI/Tools` 当前四个正式类型、正式 `Prefabs/ColorSelector.prefab`、UIAcceptance 或其他正式场景/资源；§16.4 的通用控件归位和输入解耦继续实施。不删除 `Samples` 根容器及其 meta，不改变六根默认导出机制；不将退役样例 GUID转给新资产。

同步项精确为：

- `ExportUnityPackageTests` 删除该样例 Prefab 的 requiredPath 正哨兵；将 `Samples/ColorSelector` 整树加入 excludedRoots 负断言，保留正式 ColorSelector 与默认资源的内容/GUID/meta 断言，不仅排除旧 Shaders/Textures 等子目录。
- `CoreDirectViewPlayModeTests.BoundSampleStartsTheFormalWindowAndDestroyClosesItsOwnModule` 是演示启动/销毁专属合同，随样例退役移除或替换为非样例的正式 owner 生命周期用例。移除不是修失败手段；正式颜色控件、窗口复用、输入、关闭与 owner 退出的其他用例完整保留，必要时补其未覆盖的正式合同，不再加载已退役样例。
- 清理 `Documentation/README.md`、`Architecture.md` 的样例导航/程序集表以及 `Modules/UI.md` 的演示绑定说明；源码能力使用说明改指向保留的正式入口，不造替代 Sample。
- `Runtime/THIRD-PARTY-NOTICES.md` 保留 CodeArtist/作者及独立重建的历史说明，修改“署名仍在 Sample 说明”这一失效表述，将必要来源集中保留在 notices；不能因删除 Readme 就撤销署名或声称新增再分发许可。
- 清除自有 asmdef/asmref、反射和测试发现中的样例引用，确认无样例运行 DLL/类型/资源进入导出或 Player；原历史 §0-15 的结果不机械重写成当时没有样例。

若发现正式游戏/Scene/其他资产依赖该样例，仍须停止报告，不擅自改受保护引用或删正式能力规避。该补充已在S0a按明确授权执行：样例整树/根meta退出，Samples根及正式控件/窗口/Prefab保持；删除前引用与最终导出证据见§17，当前实施完成状态见§17.12。

## 17. §16 实施状态（2026-10-13）

本轮按用户明确授权执行 S0 → S0a → S1 → S2 → S3 → S4 → S5 → S6 → S7，不重跑历史迁移。实际分支 `fusion/7-consolidation`，HEAD `0a39928e76e36564a4b354e370b0d939727b7aad`，暂存区为空；既有 45 项修改、867 项删除和 765 项未跟踪工作保留。

S0 已完成：Unity `2022.3.62f3c1`、Windows64/Mono，Main 未脏；UGUI `1.0.0`、URP `14.0.12`、Test Framework `1.3.9`、Newtonsoft.Json `3.2.1` 为活跃解析结果。与暂停 Q0 比较，2,829 个文件 hash、549 个公开类型的 AQN/API/序列化字段、445 个 MonoScript 绑定及原记录范围内的 Editor/Player 图保持；本轮另捕获全工程编译图用于后续输入范围核查，不把扩大的采样范围误记为新增程序集。本轮 CoreIdentityTests 实际 10/10 通过；取消的历史 PlayMode 不计通过。新机器基线为 `C:/Users/Kano/AppData/Local/Temp/kilo/YL7S-baseline-117a1123bd4141eabc7c8eb271a280f8.json`。

允许的公开类型归属映射：Foundation.Runtime 58、Core.Runtime 55、ZipTool 109 个类型进入 Runtime（原 277 个类型保持）；Core.Editor 15、Editor.Help 15 个类型进入 Editor（原 20 个类型保持）。这些计数是本轮公开类型基线，不替代内部类型及脚本绑定检查；namespace/API/字段保持，旧二进制与 AQN 消费者须重编译，不提供兼容壳。

S0a 已完成：删除前核对样例全部六个 meta/GUID，Assets/Packages/ProjectSettings 定向扫描未发现受保护外部引用。AssetDatabase 删除整树及根 meta 共 10 个文件，Samples 根/meta 保持；专属样例启动/销毁测试随明确退役移除，其他正式合同测试完整保留，导航及来源署名同步。整树导出负断言实际先红（仍导出样例 Prefab）后绿，真实六根导出/解包 1/1、Core EditMode 60/60、正式取色窗口坐标/回调/复用/关闭 PlayMode 1/1 通过。六个正式 Prefab 无 Missing Script，正式取色源码/Prefab/GUID 保持；没有样例编译所有者残留。

### 17.1 S1 写入内容与有效证据（未通过整批门禁）

- Runtime 主体纳入原 Foundation/Core Runtime/ZipTool 源码，三份 asmdef/meta 通过 AssetDatabase 退役；未移动职责目录或拆 partial。主 asmdef GUID 保持，相关正式 Editor/Test/Acceptance 引用去重到 Runtime，Module 生成模板亦改引用 Runtime；原游戏 asmdef 未修改。Kernel AssemblyInfo 删除 self/退役 friend 声明，文件 GUID 保留，正式 friend 继续来自原 Action AssemblyInfo。
- AnalyzerSymbols 以 `YLFramework.Runtime` 为唯一正式符号身份，ObjectPool 从真实主体符号取得，支持 source 与 metadata；不再依赖旧 Foundation 或 IsInSource 排除。Fixture 同步合并且增加真实源 partial 与异程序集同名 Pool 负例，不放宽 marker/owner/storage 规则。SDK 实际红态 122 Failed / 1 Passed；修正后 `build.ps1 -Deploy` 123 Passed / 0 Failed，唯一 DLL 重建部署，原 DLL meta/GUID/平台关闭与 RoslynAnalyzer 身份保持，build-info 随正常脚本生成。
- Core 身份新断言实际先红 8 Failed / 2 Passed，合并后 10/10；FoundationBoundaryTests 7/7（含真实中立源码 AssemblyBuilder 闭包、不接受框架/插件参考）；Core.Editor 原整组 101/101 复跑通过。真实 Unity csc 新见证 1/1：正例零 YL 诊断并产出 DLL；负例精确七项 YL0001-YL0007 Warning、无 AD0001。负例同时触发原 XF 全局分析器五项预期 Error，逐项保留断言且不产出 DLL，不关闭原分析器、降严重级别或把负例说成编译成功。
- Foundation EditMode 组合运行为 224 Passed / 1 Failed：TimerSessionTests 被运行器场景脏前置条件阻断，不能计该项绿；随后单独运行真实关闭 Domain Reload 的连续两次 Play 为 1/1，结束后 Main 干净、ProjectSettings 恢复原字节。Clipboard 多对象拒绝测试在一次整组运行失败，单项及完整 101 项原样复跑绿；未改 Clipboard 源码或断言，仍列为环境/时序风险，不掩盖先前失败。
- 合并后 RegisterListExtension / SubscriptionCleanup 两个 MonoScript 一度 GetClass 为空；定向 ForceUpdate + ForceSynchronousImport 后正常恢复到同 namespace 的 Runtime 类型，无手修 Library。停止前重新核对 549 个公开类型的 AQN/基类/API/序列化字段、445 个脚本 GUID/绑定，除三类准许程序集迁移外差异为零，Unity scriptCompilationFailed=false。
- 实际 Editor/Player 图中 Runtime 为 392 源文件，直接程序集引用为 XFABManager / UnityEngine.UI；自有 Analyzer 一份，进入 Runtime、引用 Runtime 的普通/Core Editor、Tests、DOTween 与 Project.Game.MainModule。Editor.Help、原 XFGameFramework/XFABManager/XFFSM、其 Editor 和本工程预定义程序集未注入；Runtime 实际 Bee rsp 有唯一自有 analyzer 输入，未含三份退役 DLL 引用。Player 图无 Editor 产品依赖，但本轮尚未构建/运行 Player。

### 17.2 停止条件与保护现场

S1 **未通过**，S2-S7 **尚未开始**。广范围 EditMode 工具请求超时且发生 domain reload，不能用旧计数或发现总数 1,532 冒充执行通过。随后读取运行器 collector 得到局部 94 Passed / 2 Failed，且计时已因 reload 失效；只将失败名作为定位证据，不将该局部结果作为完整套件验收。

独立原样运行 `SerializedDictionaryEditorTests` 明确为 **13 Passed / 2 Failed**，复现：

| 用例 | 真实失败点 | 状态 |
|---|---|---|
| `DefaultToolkitInspectorEditsBoundFieldsAndUndoRedoRefreshesProjection` | Tests/EditMode/SerializedDictionaryEditorTests.cs:288，新增行的绑定 IntegerField 未找到，`Sequence contains no matching element` | 未修复 |
| `ToolkitMultiObjectMixedFieldEditKeepsIndependentKeysAndDisablesChangedStructure` | 同文件:403，多选结构变化后 Foldout 仍 enabled | 未修复 |

相关 Drawer、SerializedDictionary 及测试文件与 S0 机器基线字节一致；追踪入口仍为原 Drawer 的 TrackSerializedObjectValue。尚不能区分程序集迁移影响、既有绑定问题或 UI 刷新时序，没有证据支持扩大修改范围。按用户停止条件保留现场，不改 Drawer、不增加延时放宽断言、不恢复旧程序集或吞掉错误，不进入后续批次。

停止前保护检查：Packages、ProjectSettings、原 XF/XFAB/XFFSM Package、游戏/正式 Scene/Prefab/Bundle、.gitignore/.kilo/kilo.jsonc 及其余既有文件 hash 保持；正式 ColorSelector 四源码及 Prefab 不改。本轮广范围测试产生一个独立 Audio43 Fixture（`Bundle-4fcaab67a06a4623be599bddaa7c9325`）及 Temp/kilo 独立输出，核对初始工作树不存在、创建日志/GUID、SessionState 所有权后，仅清理本轮测试数据和对应四个临时键，未调用或清理正式游戏 Bundle。无 Git 写操作，未访问 test-worktree；HEAD/暂存区不变，Main 未脏、Unity 非 Play/编译/刷新状态。

迁移限制：旧 Foundation/Core Runtime/ZipTool AQN 与已编译客户端必须更新引用并重编译；使用迁移脚本类型的既有 Player/AssetBundle 须重新构建验证，不提供兼容壳，不承诺仓库外二进制或旧 Bundle 无迁移成本。Core XFAB 与 ProjectName、五个惰性 Odin 宏、全部既有生命周期源码保持。当前 Core.Editor/Editor.Help 尚未退役，两主体目标、目录/partial/取色输入拆解、BCL Zip 探针、S7 导出/Player/独立审查均不能宣称完成。

停止交付前另完成本轮状态的六根真实导出/解包 1/1，含三份退役 Runtime asmdef 与 ColorSelector 样例整树负断言。最后保护检查为 2,829 基线文件中 2,772 保持，其余 57 项全部落于本轮允许修改/退役清单；六个正式 Prefab 无 Missing Script，正式取色四源码/Prefab/meta 及失败相关三文件保持原字节。实际产品图仍为 Runtime、Editor、Core.Editor、Editor.Help 四个程序集，不能描述为两主体已收口。`git diff --check` 无差异错误（既有换行提示保留），暂存区为空、HEAD 不变；当前工作树计数 69 修改 / 879 删除 / 761 未跟踪包含全部历史成果，不等于本轮改动数量。

### 17.3 S1 阻断定向处理（2026-10-13，用户授权后续诊断）

本轮只诊断/修复两项 UI Toolkit 失败和手动报告的 XFAB 边界测试，不继续 S2，不回退合并，不修改 Runtime / Drawer / 原适配器或脏场景安全前置条件。初始 Main 已干净，Unity 非 Play/编译/刷新状态，HEAD/分支/暂存区不变。

UI Toolkit 证据与修复：同一未修改测试本轮复跑 15/15，与 §17.2 和用户手动报告的 13/15 形成时序波动证据。Unity 2022.3 [绑定实现说明](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-binding-implementation-details.html) 明确 Bind 异步建立、变化按时间间隔而非每帧轮询，且可能跨多帧。将固定两三帧假设替换为测试私有 WaitForBinding：三秒硬上限，等待真实 Key/Value 控件及初始值、Undo/Redo 控件回显、新增行绑定值、多选 mixed value 和结构变更后的禁用/诊断状态；超时仍失败。保留唯一控件、真实写入、原数据/投影、Undo/Redo、多选独立 Key 与结构禁用的全部断言，不主动调用 Drawer 私有 Refresh、不清结构状态制造通过。最终版本连续两次整类 15/15，Drawer/字典实现与原基线 byte 保持。

XFAB 证据与修复：旧 OptionalXfabAdapterHasNoRuntimeReverseReferenceOrDefaultExport 在本轮实际红态 0 Passed / 1 Failed，禁止整个 Runtime 任何含 XFAB 的引用与已授权 Core→XFABManager 迁移矛盾。改为精确禁止 Runtime→YLFramework.Audio.XFAB 的反向产品依赖，确认 AudioKit 属于 Runtime；可选适配器独立程序集/接口、autoReferenced=false、默认不导出断言保留。新断言 1/1；另实际运行正式 Audio 无 XF/XFAB 的 Editor 与 Windows Development 源码闭包及严格参考白名单，3/3 通过，不用主体宽引用替代音频职责边界，也不提前解除 Core 临时后端依赖。

用户报告的三项未保存场景 SetUp 失败属于安全前置条件，不能计产品运行失败或跳过后计绿：TimerSessionTests、AudioContinuousPlayTests、AudioExitTests 在当前干净现场各自独立实际 1/1，通过真实跨 Play / Exit；前两者连续两次 Play 保持原有 Domain Reload/日志隔离与恢复，第三者正常域重载，工具 duration 因 reload 失效不作为时长证据。未保存/覆盖 Main，未更改这三份测试或安全断言。组合运行若用户场景被先前测试或手动编辑标脏，保护仍会拒绝进入；需由用户处理真实未保存编辑，或从干净现场分组运行，不能自动 Save/清脏/Ignore。

本轮追加 Core EditMode 60/60。保护核验：2,829 个原基线文件中 2,770 byte 保持，较上轮新增差异仅这两份测试；测试 meta/GUID、Drawer/字典、三个真实 Play 测试、Packages/ProjectSettings、游戏/原 Package/正式 Scene/Prefab/Bundle 等保持。Main 未脏、Unity 无编译错误，原 EnterPlayModeOptions 恢复，未生成或触碰正式 Bundle，未做 Git 写操作。当前报告的 UI/XFAB 阻断已解除，但没有重跑全量 EditMode，不宣称所有组合执行均绿；S1 其余迁移门禁及 S2-S7 尚未在本次诊断中完成，Player/游戏验证不得沿用 §15。

### 17.4 独立音频 AB 准备保护报告（2026-10-13）

用户追加报告 `BuildIndependentAudioBundlesForRuntimeOwnershipVerification` 的 “Do not overwrite an unconsumed fixture”。实时核对四个 SessionState 键及严格测试/Temp 路径，当前 `79f006b4d2254f549bfc0035119880d8` 为有效待消费 Fixture：资产根 GUID `8153849965dae264a8f819b57927da0a`、first/second WAV、dependency.txt、独立 Windows 主 Bundle/依赖/manifest 全部存在，不能当作空键残留删除。根因是该 EditMode 准备必须由配对 PlayMode 用例消费，单独反复准备会触发原有拒绝覆盖保护；不属于产品编译或资源迁移失败。

保持准备断言和两份测试源码不变，未直接清键或覆盖 Fixture。实际运行 `YLFramework.Tests.Audio.AudioNamedResourcePlaybackTests.RealAssetBundleSyncAsyncDependenciesAndFinalPhysicalUnload`，**1 Passed / 0 Failed**、无 Error 日志；验证同步/异步、依赖、暂停/取消/重播及最终物理卸载，由原有 finally 清理本 Fixture 的测试资产/根 meta、Temp 输出及四个状态键。结束后上述路径均不存在、四键为空，可再次准备新 Fixture，但每次仍须完成配对消费。没有为了“复跑准备绿”再次生成待消费数据。

保护核验：该准备/消费源码与原基线 byte 保持；原游戏/正式 Bundle、Packages/ProjectSettings、原 Package、.gitignore/.kilo/kilo.jsonc 等无 hash 差异，Main 未脏、Unity 编译无失败，HEAD/暂存区不变。增量更新现有 Audio 测试导航注明准备→消费顺序，不新增过程报告、幂等回退或自动清理接口，不推进 S2。该定向 1/1 不能代替 S1/S7 全量资源迁移或 Player 验收。

### 17.5 恢复实施：S1 门禁收口

用户再次明确继续中断的 §16 全计划。恢复时实际分支/HEAD/暂存区不变，工作树 69 修改 / 879 删除 / 768 未跟踪；额外七个文件为用户再次准备的有效 Audio43 `4a4e68f4ac3d4370b53f7a223e2b543d`，没有覆盖或直接清理，原配对消费测试实际 1/1，通过并完成其 finally。

本次追加 `CoreModulePlayModeTests` **28/28**，覆盖 Module 启动关闭/重启、Model/Controller 所有权/借用/ID/查询、构造/初始化/退出异常与重入、池及清理顺序；ViewCloseUnbindsBorrowedAndLocalModelsButOnlyExplicitRemovalExitsThem **1/1**。取消的历史运行仍不计通过。与 §17.1/17.3 已有效的 SDK123、真实 Unity csc、Core Edit60、Foundation/Timing/事件/池源码证据合并组成最小充分 S1 门禁，不再次运行跨模式全套造成 Fixture 污染，不声称全量 EditMode 绿。

本次主线程重新核对原 549 个公开类型 AQN/基类/API/序列化字段与 445 个脚本 GUID/绑定，除 Foundation/Core Runtime/ZipTool→Runtime 映射外差异零。Runtime Player 图为392源文件、直接依赖 XFABManager/UnityEngine.UI，无 Editor 或退役产品引用；Unity 编译无失败，Main 未脏。**S1 完成，可以进入 S2**；原停止记录保持历史事实，后续各批及 S7 Player 验证仍独立执行。

### 17.6 S2 Editor 主体合并

S2 的实际新归属断言先红 **5 Failed / 5 Passed**（此前测试编译因新增静态 Help 引用不可见而拒绝，改为加载类型的精确身份核对后才取得有效红态；编译错误不记契约红态）。Editor 主体 references 精确归 Runtime / XFABManager / UGUI，保留 Editor-only、主 GUID/autoReferenced；AssetDatabase 退役 Core.Editor 与 Editor.Help 两份 asmdef/meta，正式测试引用同步去重，Help/Action 相同 friend 只保留原 Action 声明，所有 AssemblyInfo/脚本 GUID 保留。Core 与 Help namespace、菜单/Provider id/配置键和原 MovedFrom 声明不改，无 Editor 兼容壳。

通过门禁：CoreEditorIdentityTests **10/10**，Core.Editor 原整组 **102/102**，HelpWindowTests **25/25**，FrameworkHelpProviderTests **6/6**，ApiHelpTests **71/71**（含真实 Runtime DLL 编译示例及同名伪 DLL/测试/可选参考拒绝），EventListenerEditorTests **5/5**，SerializedDictionaryEditorTests **15/15**。断言只把旧 DLL边界映射为当前精确主体，不放宽原 Package/Game/DOTween 依赖或 UI/Help 行为。

主线程核验549公开类型 AQN/基类/API/序列化字段、445脚本GUID/绑定，准许五类程序集映射外零差异；保护 hash 无变化。Editor 53源文件完整归属主体，显式引用为三项，实际图另保留原引擎/测试框架自动模块参考，不等同 Player 载荷。Player无 Editor主体/五份退役产品或Runtime→Editor边；实际 Editor friend 唯一为 YLFramework.Tests.EditMode。Assets asmdef/asmref/文本序列化扫描无两份退役身份/GUID消费，diff检查通过。**S2 完成，S3-S7 待实施**；S7仍执行实际导出/Player及独立审查，不据此宣称最终载荷验收。

### 17.7 S3 Core 归位与 partial（2026-10-14）

Core 的 Modules/MVC/Events/UI/Attributes 已按 §16.3 归位：33个既有脚本保持原meta/GUID与内容（三个拆分主文件除外），Views目录整体保留原目录GUID进入MVC。Module主文件与Models/Controllers/Views/Panels，ViewManager主文件与Registration/Loading/Queries，PanelManager主文件与Loading/Sorting/Input共同组成原唯一类型；十个新增part由Unity正常生成新meta。全部状态字段/属性初始化/cctor/ctor仍在原主文件，完整方法、标注、默认参数与异常清理代码只搬一次。

直接路径门禁先红（Modules/Module.cs尚不存在），后CoreIdentity11/11、Core EditMode61/61、Module PlayMode28/28、CoreDirectView PlayMode96/96（包含已声明的故障观察异常见证）、SDK123/123及真实Unity正负Analyzer见证1/1通过。Roslyn词法成员核对在Player与Editor+Legacy两profile分别比较Module96/ViewManager60/PanelManager78或79个直属成员，跨part集合逐token相等，主状态/cctor/ctor顺序相等，无第二声明/初始化；原namespace/公开API/字段/虚拟槽保持。SDK Fixture和导出哨兵同步新marker/Module/View/Panel路径，真实六根导出/解包1/1。

移动工具一度回报MoveAsset“invalid path”，实际所有目标/源码meta已移动；先检查磁盘和AssetDatabase、未重复执行移动或回退。按GUID重查33个脚本路径/所有445脚本绑定/meta及549公开类型仅有S1/S2准许映射，无漂移。核对无外部GUID引用后删除两个本批清空的MVC/Internal与Attributes/RoslynAnalyzers容器/meta。中断后Unity重启，三个未改动脚本GetClass缓存为空，定向正常ForceUpdate导入恢复，不手修Library；最终445绑定零差异。保护hash无变化，Main未脏，Unity无编译失败，HEAD/暂存区保持。S3完成，S4-S7待执行。

### 17.8 S4 Foundation 归位（2026-10-14）

Kernel原49源码/完整LICENSE并回Foundation功能族；Binding的Prefs/Collections/Unity、Events的Global/Internal/Subscriptions/Unity、Pool的Abstractions/Collections/Factories/GameObject/Object统一。TimerObject→Timing/Components、Animation/Collision/TriggerEventListener→Events/Unity、CloseView→MVC/Views/Components，namespace/API/字段不改。错误测试路径AnimatorListener按真实AnimationEventListener修正，没有产品改名。

批前快照 `C:/Users/Kano/AppData/Local/Temp/kilo/YL7S4-before-0b8fc18fd9804256bee775f1e082ef17.json` 完整165项：125文件（79移动/46原位）及40目录。125文件content/meta逐字节保持；核对空/无GUID外部引用后退出17旧容器及meta，23既有目录身份保持；15新目录均生成新GUID，Kernel目录GUID不转赠。Singleton/IOC等旁观文件保持。所有移动由AssetDatabase完成，代码算法不改。

退役路径门禁先红后绿；FoundationBoundary8/8含原49中立源集合Editor/Windows Development真实编译，拒绝Core/Runtime/XFAB/Json.NET/测试DLL，不扩大为整个Foundation。Audio迁后严格保持原源集合（不加入Lifecycle、全Factories或Binding集合适配），双profile/白名单3/3。TimerScheduler28、UnityEventListener4、LayerMask/动画桥3、Attributes3、字典GUID/字段1项EditMode通过；TimerObject6、EventsLifecycle14、TimerBinding3项PlayMode通过。一次不支持的排除式class筛选返回Unknown/0执行，不计验证通过。实际六根导出/解包1/1，路径导航/许可引用同步。

最终549类型API/序列化字段和445脚本绑定仅有S1/S2准许身份映射，保护hash、Main干净、Unity编译/HEAD/暂存区保持；diff检查通过。S4完成，S5-S7待实施。不同职责共用DLL不撤销中立源码边界、所有权或迁移限制。

### 17.9 S5 取色控件输入解耦（2026-10-14）

ColorSelector/ColorSelectorCursor/ColorWheelGraphic→Utilities/UI/ColorSelector，ColorSelectorWindow→Core/UI/Windows/Tools，四文件及meta/GUID保持；唯一内容修改为ColorSelector的非序列化Panel缓存改为IUIInputGate+Unity Component存活身份。最终新增 `YLFramework.UI.IUIInputGate` 只有只读 `IsInputAllowed`；Panel显式实现 `this && IsLoaded && !IsInputBlocked`，不新增setter、manager、owner、会话或状态总线。原四类型namespace/API/字段、正式Prefab与HSV/Alpha/Camera/重入/关闭行为不改。最初实现误用Utilities.UI/CanReceiveUIInput，在S7自审中按§16.4纠正并重新验证，不留未发布旧名壳；初次Player证据因此仅保留历史，不作最终源码验收。

结构门禁先红（接口不存在），独立宿主阻挡拖拽用例先红（旧控件忽略独立gate），修改后ColorSelectorTests16/16（含Editor/Windows Development四源码闭包，不参考Runtime/Core/XF DLL）、渲染1/1、CoreDirectViewPlayMode96/96、六根真实导出1/1。最近活跃祖先解析与同对象多候选规则沿Unity 2022.3 GetComponentInParent，不增加歧义异常；每个输入事件现读资格，阻挡丢旧捕获，无宿主或已销毁缓存宿主沿原默认，BeginSession才重查宿主。独立控件、多gate、inactive祖先/重新BeginSession、destroyed缓存与实时predicate均由新增最小用例覆盖；既有真实Panel/模态/回调复用合同完整保留。

四源码GUID/meta保持，六正式Prefab无Missing Script，549原公开类型API/序列化字段、445绑定除准许程序集迁移外差异零；仅新增一个门禁public类型及Panel显式interface关系。空旧Tools容器核验无GUID外部引用后退出；不复制第二份控件/窗口。保护hash、Main/Unity编译/HEAD/暂存区保持，diff检查通过。S5完成，S6-S7待实施。

### 17.10 S6 Editor功能组/导航/Zip闭包（2026-10-14）

Resource Editor八个既有脚本归Build3/Service3/CodeGeneration2，Data和其余已合理分组保持；内容/meta/GUID逐字节相等，namespace/owner/Bundle/生成输出/加载与清理算法不改，不实施阶段8。路径门禁先红后绿；新增ZipToolSourceTests全量编译Zip/SharpZipLib，仅接受Unity安装目录Mono/NetStandard下BCL参考，无Engine/Editor/主体/测试/XF DLL，保留全部109公开类型及完整MIT，取消独立产品DLL不宣传整个主体无依赖。

门禁：ResourceEditor12、ResourceWindow真实GUI1、ResourceOutputSafety17、ResourceCodeGeneration22、真实GeneratedSource asmdef/预定义消费者1及InterruptedCleanup1、Zip BCL-only源码1、Help Provider6、ApiHelp/真实声明示例71、六根真实导出1项通过。CodeGeneration组首次21 Passed/1 Failed，AtomicUpdate在已释放文件锁后File.Replace出现IOException；单项及完整22项原样复跑绿，根因未确认，保留Windows文件替换波动风险，不改产品或延时/弱化断言。GeneratedSource跨domain reload的duration失效，不作为时长证据；其自有输出/Consumer/SessionState已清理，不存在Assets/YLFrameworkData载荷残留。

导航/Help内容同步两主体、Foundation展开、控件分离、Resource功能组和实际Analyzer作用域；菜单/Provider id/配置键不改。文档相对链接实际核对无断链，旧退役目录只在历史记录/负断言保留；五个惰性Odin宏不动。445原绑定/meta、八文件原文与保护hash保持，Main未脏、Unity编译无失败，diff检查通过。S6完成；S7仍需新Player载荷/MVC-UI/持久UnityEvent/退出与独立审查，不沿用旧Player结果。

### 17.11 S7 实际证据与硬停止（2026-10-14）

**当前全计划未通过，S7阻断。** §17.9的S5验收需重新打开正式Panel混合门禁优先级回归；此前各组通过记录保留历史事实，不能据它们宣称最新正式资产保护门禁、独立审查或§16整体完成。发现保护漂移后冻结产品修改，不自动回退资产或变更基线/断言。

#### 已执行证据

- 新建的S7Smoke仅位于Tests/Core/Acceptance的自有临时子目录，使用既有非NUnit Acceptance程序集进入Player。临时Resources Prefab绑定正式根/取色Prefab及EventListener持久调用；用PreviewScene制作，不保存Main、不改EditorBuildSettings或ProjectSettings。代码最初Owner未实现OnStart及请求泛型类型写错，Unity实际拒绝编译，纠正后编译通过；这两次仅夹具编译错误，不计红绿合同证据。
- 自审发现新增接口命名未按§16.4，在S7纠正为唯一 `YLFramework.UI.IUIInputGate.IsInputAllowed`，不留先前未发布名字壳。纠正后控件16/16重新通过，重新构建最终源码；初次Player只能作历史，不能沿用其结果证明命名纠正后的代码。
- 最终产物 `C:/Users/Kano/AppData/Local/Temp/kilo/YL7S7-FinalPlayer-20261014-524b6410/`：BuildReport Succeeded、0 errors、534 warnings、StandaloneWindows64/Mono2x。requested/reported options数值1为Development；Unity枚举ToString同时显示几个别名（含Il2CPP），不据此误报后端，实际Mono路径/运行及backend读取一致。唯一目录保护导致CLI自动重入返回HTTP500“target not new”，首个真实BuildReport与产物已成功，未覆盖原目录或将重入失败当构建通过。
- D3D11真实GUI Player日志 `Player-NativeExit.log` 出现READY与PLAYER_PASS，原生同步进程退出码0。验证Main加载、Runtime身份、持久EventListener目标/方法/事件参数及取消、Module启动、正式窗口owner/初值、正常Panel输入/模态阻挡/捕获释放/重开无旧回调、pending取消及真实quitting失效/拒绝重入/owner关闭。这是代表性smoke，不是完整人工游戏流程，也未覆盖混合门禁优先级。
- 最终Managed191 DLL，无五份退役产品/Editor主体/自有Analyzer/Sirenix/NUnit/TestRunner或自有测试DLL；既有可选集成、UI.Acceptance、MCP/NuGet载荷仍在。Microsoft.CodeAnalysis.dll的SHA与旧O1 Player完全相同，机器原Player图亦有既有全局NuGet参考；YLFramework.Runtime IL不引用它，不假称整个工程无Roslyn，不擅自改保护范围外插件导入配置。完整DLL hash、夹具源/序列化证据记录在同目录Verification.json。

#### 独立审查与停止证据

独立只读审查提交两项重要发现后因usage limit中断，没有最终审查结论；不重述为“审查通过”。根代理实际复核：

1. 正式 `Runtime/Core/Resources/YLFramework/Core/Canvas.prefab` 当前SHA256 `30770BD2B50272D544743A4D600F79E330A4647C05D3EE169F20003085A26BB2`，实施前基线 `C6024E7F940DDC6D5E62FB512B10B6D4CCC396D0DA1D1C8D245F465EE29815FC`；meta/GUID保持。HEAD历史XFMain同一资源正文与机器基线恰匹配（仅用于该资产对照，不按HEAD总diff归责）。当前:104 `m_BlockingMask.m_Bits` 从2047变为4294967295，:117-119新增referenceResolution/automaticMatch/manualMatch；不是路径/换行差异。最后写UTC为2026-10-14T00:32:04.9179345Z，Editor.log仅能证实随后导入，不能确认写入者。ListenerInitOnLoad明确CanCreate仅允许不存在file/meta/asset的路径，不能据该工具存在就归责。原XF Canvas和其余五个正式Prefab保持基线。
2. 清理临时夹具并恢复Editor编译后，原 `CanvasPreservesOriginalSerializedDataExceptYLAdaptationBinding` 真实运行 **0 Passed / 1 Failed**，在CoreDefaultResourceTests.cs:76报告2047/4294967295正文差异。第一次因Player构建后测试未发现返回“No tests found”，不计通过或产品合同失败；正常Editor重编译后上述1项才是有效结果。未改该测试、未恢复或重序列化正式Canvas。当前受保护载荷断言失败，前序窄保护检查不能替代此次全正式Prefab复核。
3. 输入优先级：ColorSelector.cs:35只取最近IUIInputGate，:53只读该predicate；正式Prefab中selector位于HSV子对象，Panel在根。临时PreviewScene实例化正式Prefab，仅在副本selector上增加允许型gate，未启动Module或改正式资源：Panel.IsLoaded=false/IsInputAllowed=false；增加前CanInput=false；增加后childGate被选择且CanInput=true。证明真实Panel原加载拒绝可被较近门禁覆盖。现有同对象两个都允许的probe/普通模态96项/Player正常布局不足以满足§16.4真实Panel优先级，S5必须补真实混合候选红例并最小修复，不能删多候选能力、抛新注册错误或把此回归改成“预期行为”。未实施修复，避免在硬停止后继续产品修改。

#### 保护现场与恢复前置

核对S7Smoke根GUID `5e51ee90a66ad31488a8f8cb0449eb12`、临时Prefab `8f3717e54a9ec6f4f8ce3e001b7878f1`、源/Prefab正文与Verification一致及精确五文件清单后，AssetDatabase只删除该自有整树/根meta并清除三个自有YL7S7状态键；完整Player/日志/证据保留Temp。预览probe对象即时销毁、PreviewScene关闭；Main未脏。当前Canvas原样保留，停止快照 `ProtectedCanvasAtStop.json` 已存最终Player目录，不能从该快照擅自恢复。

恢复必须先明确保护资产漂移的归属及仅该资源恢复/保留处理权限，不能把“继续”当覆盖未确认用户编辑授权；然后修正式Panel混合门禁优先级、补红绿与回归、重建并重新核验Player/导出/最终Editor-Player rsp输入/完整独立审查与保护hash。当前最终rsp与审查未收口，旧Editor/Player正负证据只保留其有效范围。IL2CPP/AOT/移动/其他平台、旧已发布AQN二进制/Bundle、干净载体导入及公开许可证分发仍未验证。HEAD/分支/暂存区不变，无Git写操作、test-worktree访问或阶段8改动。

停止交付前重新核对1151个保护文件（原包/游戏/配置与正式YL Scene/Prefab/ScriptableObject及其meta），唯一差异仍为上述Canvas正文；其meta与其他保护文件保持。S7临时夹具/根meta、生成源码probe与Audio43待消费状态均不存在，Main干净、Unity非Play/编译且scriptCompilationFailed=false。现工作树76修改/1044删除/955未跟踪包含全部历史与本轮工作，不等于本轮变更数量；git diff --check与暂存区核对保持，未尝试自动修正Canvas。

### 17.12 授权修复与最终验收（2026-10-14）

**当前§16与§16.9技术实施完成，S0→S0a→S1→S2→S3→S4→S5→S6→S7收口。** 用户明确授权定向修复后才继续；§17.11保存历史停止与未确认写者事实，不静默覆盖先前失败记录、不把任意“继续”当正式资产恢复许可。本次不重做历史迁移，不执行Git写操作、test-worktree访问、阶段8或Odin恢复。

#### 复原与直接回归修复

- Canvas仅按原S0正文改回2047并移除三新增字段，恢复SHA `C6024E7F940DDC6D5E62FB512B10B6D4CCC396D0DA1D1C8D245F465EE29815FC`；原meta/GUID/fileID保持。正常定向导入、资源测试、导出、两次新构建及清理前后均未再漂移。写入来源仍未确认，不能宣称已修复未知写入者；未回退工作树或接受新基线。原CoreDefaultResourceTests 4/4通过。
- 正式混合门禁Play用例实际先红（较近允许gate绕过模态），最小修复后绿；用例覆盖实际正式Prefab、两种同对象组件顺序、较近gate、未加载拒绝/模态拒绝、阻挡丢捕获/解除不复活。Core窗口从selector按原GetComponentInParent<Panel>()规则解析并经internal三参BeginSession显式注入；public两参独立控件入口保留，统一唯一实现，不引入第二接口/排序注册表/状态权威。控件仍只识别只读IUIInputGate，Unity假空、inactive祖先、当前predicate与原HSV/Camera/Alpha/重入契约保持。最终控件16/16（含双profile四源码无Core闭包）、CoreDirectViewPlayMode97/97通过；该组两项预设观察异常按原断言验证，不把测试故障注入日志误报为产品编译失败。
- 独立审查完成，无新P0-P2，S3四profile完整成员/状态顺序等价、S4 125文件content/meta等价（79移动/46原位）、445原脚本meta/GUID和保护hash独立核验保持；中立49/Audio212源集合与11个SDK构建输入均匹配。唯一P3为Help将全部同Runtime类型误分Foundation；按既有tools指南精选类型而非DLL分类，实际先红后Provider7/7。菜单/Provider id/持久配置不变，根代理复查最小差异，不增加新框架或恢复旧DLL。

#### 最终编译输入/导出/身份

- SDK完整入口本轮123/123，0warning/0error；构建DLL与已部署唯一DLL SHA相同 `9F972B54A2E98CA56D805AFEE86C405715027D00E62371351D494CAC82CE2E12`，未无意义重复部署，meta/标签/普通平台关闭保持。当前11个构建输入与正常收据匹配，不伪造产物。
- CoreEditorIdentityTests 11/11含Editor与非Editor Development Unity csc正/负见证：精确七项YL0001-YL0007 Warning、无AD0001，原XF负例五项Error完整保留，未关闭诊断或把负例编译失败说成成功。主Runtime403源/Editor53源，Editor/Player实际图及13份代表Bee rsp逐项核对。Runtime/Editor/MainModule以及自动引用主体的Assembly-CSharp-firstpass各一份自有analyzer，XF/XFAB/XFFSM为零；无analyzer Runtime参考。此前§17.1“预定义程序集未注入”采样结论以此最终实际引用与rsp证据替代，不套用旧Core作用域。
- 五退休asmdef/meta真实退出、无旧AQN/asmdef/asmref/序列化文本消费或空壳；optional/test身份与约束保持。549原公开类型AQN/基类/API/序列化字段、445原脚本GUID/meta/绑定仅有准许五类程序集映射；六正式Prefab无Missing Script，原正式资源保持。新增public仅计划规定的YLFramework.UI.IUIInputGate/Panel显式关系，内部接入不扩大公开API。
- 最终六根真实导出与完整tar解包1/1，通过整树样例/退休身份/Tests/Integrations/SDK负断言及新路径/完整LICENSE/Plugins哨兵；Samples根/meta保持。既有导航/Help/许可引用与双主体/功能目录一致，文档相对链接收口，未建重复过程报告。

#### 新Player与夹具所有权

修复后新建S7RepairedSmoke、继而用S7MvcCompletion补齐同一产品源码的Model/Controller实际Player覆盖；均为独立Tests/Acceptance临时目录与新GUID，PreviewScene制作，显式引用正式Prefab，不保存Main、不改BuildSettings。只传Development（数值1）和Main场景，Mono2x；Unity枚举ToString的别名含Il2CPP不用于推断后端。CLI因构建域变化自动重入被“输出目录必须新”保护拒绝，首个真实BuildReport与保护双检有效，没有覆盖目录或把重入HTTP500计通过。

| 最终证据 | 实际结果 |
|---|---|
| `C:/Users/Kano/AppData/Local/Temp/kilo/YL7S7-MvcPlayer-20261014-ec3bfc41/BuildReport.json` | Succeeded，0 errors，4 warnings，Windows64/Mono2x Development |
| 同目录 `Player-NativeExit.log` | 持久EventListener目标/方法/调用参数/取消、Model/Controller初始化/查询/恰一次退出解绑、Module/View/UI、混合门禁优先级/捕获释放/复用、pending取消/真实quitting失效/拒绝重入全部CHECK；PLAYER_PASS、原生exit0 |
| 同目录 `Verification.json` | 191 Managed DLL hash、Runtime IL参考、13实际rsp、夹具源/序列化/新GUID和运行见证；产品Runtime SHA `B8819F496CA40D3D8482DC464101F9C8A8643C9309104778C248B1121D4C1E40` 与前次已修复Player完全相等 |
| 最终载荷 | 无五退休/Editor主体/自有Analyzer/Sirenix/NUnit/TestRunner/自有测试DLL；Runtime IL无Editor/Analyzer/Roslyn参考。既有UI.Acceptance/可选集成/MCP/NuGet载荷保留，Microsoft.CodeAnalysis原有，不宣传整个工程零Roslyn或已独立交付 |

所有本轮工程内S7夹具（两cs/两meta/Resources与Prefab/meta/根meta）均核对独立根GUID、精确文件清单、Source/YAML与Verification所有权后用AssetDatabase删除，清理自有SessionState键；构建/日志/机器证据保留Temp。不存在旧S7Smoke/S7RepairedSmoke/S7MvcCompletion或生成源码probe、未消费Audio43数据。只清自己创建的测试内容，不涉及正式Bundle/其他已有夹具。

#### 交付限制

五类旧程序集AQN和已编译客户端须更新引用并重编译；已发布Player/AssetBundle/仓库外持久类型数据须按迁移重新构建验证，不保旧名壳、不承诺旧二进制透明可用。Core仍用XFABManager，ProjectName保持XFAB项目语义；不实施阶段8、XFFSM替换、无XF独立框架或公开分发许可扩张。五惰性Odin宏保留。最终使用最小充分定向门禁，不宣称全量组合1544 EditMode/653 PlayMode发现项全部执行，取消/Unknown/场景安全阻断不计绿。

未验证IL2CPP/AOT、移动及其他平台、物理OS输入人工/完整游戏流程、干净载体导入及外部旧Bundle/AQN数据；第三方再分发许可仍以现有原文及用户授权范围为准。此前Clipboard/Windows文件替换波动保留为环境/时序风险记录，未吞异常或放宽合同。Canvas未知写者未重现仍是残余风险，后续正常保存/重序列化不得无核对宣称资源保持。本轮技术收口不等于用户最终验收、commit/push、阶段8或产品独立发布。

最终清理后资源保留测试1/1、最终导出/完整解包1/1再次通过；一次构建后立即发现不到测试的请求不计通过，确认Editor已加载正式测试并用全限定方法执行取得有效结果。最终独立机器收据 `FinalProtection.json` 核对1154保护文件（较1151追加两主体asmdef.meta和Samples.meta）、549原公开类型/字段、445原脚本GUID/meta/绑定及251个文档相对文件/目录链接，差异0；Runtime403/Editor53源，三套S7夹具/根meta、自有状态键、生成probe与待消费音频数据均清理。Main未脏、Unity编译无失败且非Play/编译/刷新，分支fusion/7-consolidation、HEAD0a39928e76e36564a4b354e370b0d939727b7aad、暂存区保持空；完整工作树76修改/1044删除/955未跟踪包含全部历史，不是本轮数量。git diff --check通过，已有CRLF提示保留，不作全库格式化。

## 18. 阶段 7.5 便利工具与 XF 许可集中（2026-10-14）

**状态：技术实施与定向验证完成，未提交/推送。** 本节是用户在阶段 7 Git 收口后新增的授权，不重做 S0-S7，也不改写 §17 的历史门禁。起点为 `integration/xf-yl` / `bcfeb3d`；已有 `.gitignore`、`.kilo/kilo.jsonc` 修改不动，不自动创建分支、提交、推送或启动阶段 8。

- ColorSelector 与 Json、FSM 同属按需便利工具；三控件和 ColorSelectorWindow 统一位于 `Runtime/Utilities/UI/ColorSelector/`。窗口通过 AssetDatabase 移动，保留 GUID `4878c19ecf0794f45baa1869cc9513cb`、namespace/API/序列化字段及正式 Prefab。
- 用户明确选择直接依赖 Panel：删除公开 `YLFramework.UI.IUIInputGate` 及其自定义宿主能力，Panel 不再实现该接口，也不向 Foundation 增加 UI 合同。控件每轮 BeginSession 缓存最近活跃 Panel，按原 `IsLoaded && !IsInputBlocked` 读取即时资格；无 Panel/已销毁缓存宿主沿默认行为，阻挡释放拖拽，恢复不复活旧捕获。HSV/Alpha/Camera/通知与窗口生命周期不变。
- 原 Editor/Core、Runtime/Core、Runtime/Foundation 三份相同 XF MIT 许可收敛为 `Assets/YLFramework/LICENSE.md`：移动原 Runtime/Core 文件，保留 GUID `52c8029f1cf2acf41bba065d713f352b`、版权与完整文本，删除另外两份及配套 meta。六个默认导出目录之外显式加入根级许可；QFramework、JSONObject、SharpZipLib 等其他来源许可不动。
- 旧无 Core 源码闭包要求由用户明确取消，改为实际消费源码的 Editor/Development 编译探针。公开接口退出是本节的兼容成本：外部自定义门禁或类型引用须移除并重编译；其余取色公共 API、类型归属与资源身份不变。

验证取得预期红绿：两项新输入场景先失败（旧自定义门禁能绕过未加载 Panel、无 Panel 时仍限制控件）；默认导出缺根级许可、根级完整许可缺失、Tools 导航缺取色入口亦先红。最终取色整类 18/18（含实际消费源码 Editor/Development 编译）、与原 XF Package 完整许可比对 1/1、FrameworkHelpProvider 7/7、正式窗口 Panel 策略/阻挡捕获和 Camera/回调/复用 Play 各 1/1、默认实际导出与完整 tar 解包 1/1 均通过。导出仍排除 Tests/旧样例/退役身份，包含新窗口路径与根级许可，不包含三份旧许可位置及 IUIInputGate。

原 Runtime/Core 许可全文、其 meta 和窗口脚本 meta 的 Git blob hash 在移动前后逐项相等；窗口 GUID/类型绑定、根级许可 GUID 由实际导出和取色测试再次断言。正式 Prefab/Core Resources、Packages、ProjectSettings.asset、原插件与 Analyzer SDK 无本批差异。工作期间出现的 ProBuilder `Settings.json` 日志选项变化不回退、不纳入本批。未运行新的 SDK123、Player 构建、全量测试、跨平台/AOT 或外部旧二进制验证；No tests found 请求不计绿，待正式类型与加载状态确认后取得上述有效结果。
