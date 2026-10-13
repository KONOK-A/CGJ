# XF × YL：阶段 7 最终架构收口与独立交付总体方案

**状态：APPROVED — 六批路线确认，7.1 待单独实施授权。** 阶段 7 总体方案已经用户审核批准，D1～D4 已确认（见第 7 节），本研究轮次本身没有实施任何批次。方案批准不自动授权各批实施、公共契约变更或 Git 操作；具体文件迁移、类型与 API 设计在对应批次启动时完成，7.3 的资源契约细节仍须实施前专项设计审核。

**目标：** 保留阶段 1～6 的正式能力与身份，收敛历史目录及依赖，以现有 YL Resource 支撑主框架，在不提供原 15 个 XF 系统的独立 Unity 工程中证明内部技术交付能力。遵循“融合优先、重构其次、重写最后”，不追求零第三方依赖。

## 1. 实际基线与模块归属

### 1.1 Git、环境与保护范围

- 2026-10-13 已实际读取项目 `AGENTS.md`、检查 Git，并在任务中断后重新核对：HEAD、本地 `integration/xf-yl`、本地 upstream 和实时远端均为 `277d4f90eb5e32480f62dbe0661369d7e302c72e`。
- `milestone/6-editor` 为 Annotated Tag，本地与远端 tag object 均为 `d4e5e84be502ba5fbe91df3a19a9a95e3f3a5431`，解引用指向上述阶段 6 最终 Merge Commit。两个父提交为 `7db5806…` / `ddb7c8d…`；第二父提交与本地、实时远端 `fusion/6-editor` 最终 6.5 交付一致。基线门禁通过，没有补做 Git 操作。
- 主工作区已有 `.gitignore`、`.kilo/kilo.jsonc` 未暂存修改，暂存区为空；原样保护。已有 `test-worktree` 位于 `.kilo/worktrees/test-worktree`，基线 `09e3fe0`，有用户未跟踪文件 `Assets/worktree-test.txt`；不修改、清理或将其作为本阶段基线。工作树与会话配置只证明存在，不证明另一个参与者是否正在操作。
- Unity 为 `2022.3.62f3c1`。已核对 manifest、锁文件及必要安装源码；UGUI 声明/锁定为 2.0.0，而实际安装为 1.0.0，原工程还存在 URP 等历史声明/解析差异。独立载体不能照抄该工程的 manifest。
- 原 XF/XFAB/XFFSM 与其他原生态 Package 严格只读；原游戏业务、Scene、Prefab、资源、Bundle、Packages、ProjectSettings 不迁移、不修改。不创建实施分支，不自动 Commit、Push、Merge、Tag、Reset 或 Clean。

定向阅读了 [长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)、[Architecture](../../Assets/YLFramework/Documentation/Architecture.md)，阶段 1.1～1.3、阶段 2 五个专题及阶段 3～6 的最终设计/验收章节，包括 [6.4 Analyzer 专题](xf-yl-6.4-analyzers.md)。部分旧方案仍保留当时“待验收”文字，当前阶段 1～6 COMPLETE 以较新的蓝图、最终验收及 Git 里程碑为准。本轮只有静态研究与 Git 核验，没有执行 Unity 编译、测试、Play 或构建。

下文源码简称：`R` = `Assets/YLFramework/Runtime/`，`C` = `R/XFMain/`，`E` = `Assets/YLFramework/Editor/`，`T` = `Assets/YLFramework/Tests/`。行号对应本研究基线。

### 1.2 当前真实归属

| 位置 / 程序集 | 当前职责与依赖 | 判断 |
|---|---|---|
| `R/Shared` / `YLFramework.Foundation.Runtime` | Pool/集合内核、EventBus/channel、Update、Timing、协程/会话、序列化字典、类型查找、展示元数据；显式 references=[] | 合理中立闭包，目录仍是承接期名字 |
| `R/Foundation` / **`YLFramework.Runtime`** | Binding、EasyEvent/全局便捷入口、IOC、Safe/GO 池、普通/Mono/资产单例 | 不是第二套底层内核；是较高层入口和 Unity 适配，不能整包下沉 |
| `C` / `YLFramework.Core.Runtime` | Module/MVC、View/Panel/UI、领域请求、组件/监听及分析器标记；依赖 Foundation、UGUI、过渡 XFAB | 正式 Core 已形成，`XFMain` 不应继续作为永久分类 |
| `R/{Action,Audio,Resource,Save}`、`Services/Http` | 完整系统与轻量 HTTP，共用 Runtime；Runtime→Foundation、ZipTool、UGUI | 职责合理，保留领域所有权与目录策略 |
| `R/Tools` | Extensions、StringTools、FSM、Grid、Json、Table；ZipTool 独立无引擎程序集 | 保留成熟工具，不再按来源拆分 |
| `E`、`E/XFMain`、`E/Help` | 普通 Editor、Core Editor、无模块反向依赖的 Help 核心 | 三程序集保留；Core Editor 的 XFAB 边需要退出 |
| 根 `Integrations` | 可选 DOTween Action、XFAB Audio 适配 | 与正式 Runtime 分离，保持不默认导出、不自动注册 |

边界依据：四份 Runtime asmdef；`R/Shared/Internal/AssemblyInfo.cs:3-4`；`T/Foundation/EditMode/FoundationBoundaryTests.cs:15-25,58-75`；`T/Core/EditMode/CoreIdentityTests.cs:96-117`。原游戏 `Project.Game.MainModule.asmdef:4-12` 引用 Runtime、原 XF/XFAB/XFFSM 等，但不引用新 Core；`MainModule.cs:4,27-28` 实际消费正式 YL Audio。游戏不是可忽略的 Runtime 消费者。

## 2. Runtime 最终组织

### 2.1 推荐目录

```text
Assets/YLFramework/Runtime/
  YLFramework.Runtime.asmdef / csc.rsp / YLFrameworkRoot.cs
  Foundation/
    Kernel/                           # 由 Shared 移入；中立共享闭包
      YLFramework.Foundation.Runtime.asmdef
      Pool/ Events/ Timing/ Serialization/ Attributes/ Internal/ ...
      Resources/                      # 获批后新增的窄资源契约，不是加载系统
    Binding/ Events/ IOC/ Pool/ Singleton/
                                      # 现有较高层入口，仍编入 Runtime
  Core/                               # 由 XFMain 收敛，程序集身份不变
    YLFramework.Core.Runtime.asmdef
    MVC/ Views/ UI/ Components/ Attributes/
    Analyzers/ Resources/ LICENSE.md
  Action/ Audio/ Resource/ Save/       # 成熟大型系统保留
  Services/Http/                      # 轻量服务域保留
  Tools/Extensions/ FSM/ Grid/ Json/ Table/ ZipTool/ ...
```

`Kernel` 明确表示 Core 与 Runtime 都可使用的底层闭包。Foundation 是逻辑能力分类，不要求其每个子目录属于同一程序集；现有便捷入口留在 Runtime，不新增 asmref 或拆分程序集来追求目录对称。相比简单交换 `Shared` / `Foundation` 两个名字，这一组织消除根部历史承接区，同时避免把另一组基础能力重新放回含义模糊的 `Shared`。

Core 的内部组织按实际 owner 收敛：Module/Model/Controller 及其索引辅助归 MVC；View 取得、参数、请求和退出归 Views；Panel 策略、窗口目录、根/层/输入、Tips/取色归 UI；组件与监听归 Components；标记归 Attributes。现有 `Manager`、`mvc`、`Other`、`AsyncOperation` 不再作为跨领域杂物分类，但不拆大类、不改变事务，也不为小文件制造深层目录。具体文件归属在 Core 批次确定，既有合理 UI 子结构不重排。

### 2.2 必须处理与无需移动的界线

- **必须处理：** `Runtime/XFMain`、`Editor/XFMain` 的来源目录；`Shared` 与实际 Foundation 归属错位；Core 中跨职责混放的 manager/请求/other；已经搬空的 `Tools/AssemblyTool`、`Tools/Attributes` 残留。空目录退出前仍须复核自身 GUID 引用。
- **不机械合并：** `R/Foundation/Events/Unity/EventListener.cs:3-7` 引用 Odin/UGUI；`Singleton/Mono/MonoSingleton.cs:2,50` 引用 Runtime Extensions。它们整体迁入 Kernel 会破坏中立程序集约束。共享算法已经唯一，便捷入口、Unity 适配与 IOC 不等于重复 owner。
- **合理保留：** Action/Audio/Resource/Save 是完整系统，HTTP 是轻量服务域，Tools 是直接使用能力。Audio namespace 属 Services 不构成必须搬到 Services 目录的理由；不全量搬动成熟系统。
- **仅澄清职责：** `YLFrameworkRoot` 是 Action/Audio 等使用的公共层级根，不是 ModuleManager，也不接管所有服务退出。Timer、Action、Audio、Resource 各自保留调度/取消/租约，不能据宿主数量推定重复机制。

移动优先使用 Unity AssetDatabase/API，保留文件和目录自身 `.meta`/GUID、类型/字段/程序集身份；asmdef 与其闭包一起移动，防止原 Runtime 文件误落入 Kernel 的编译范围。Core Resources 的逻辑键 `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem` 不变。默认资源生成器、目录监听、SDK fixture/build 路径、源码闭包测试和导出哨兵要同批更新，避免旧路径监听器重新生成副本。

## 3. Namespace、程序集与 Analyzer 收口

### 3.1 推荐保持的身份和依赖

保留 `YLFramework.Runtime`、`YLFramework.Foundation.Runtime`、`YLFramework.Core.Runtime`、`YLFramework.Utilities.ZipTool` 四个正式 Runtime 程序集及三套 Editor 程序集名称/GUID。默认依赖目标为：

```text
Core.Runtime  -> Foundation.Runtime + UGUI
Runtime       -> Foundation.Runtime + ZipTool + UGUI
Core.Editor   -> Core.Runtime + Foundation.Runtime + Editor.Help + UGUI
Editor        -> Runtime + Foundation.Runtime + Editor.Help + UGUI
Editor.Help   -> 无模块显式引用
```

Foundation 不反向引用 Core、Runtime、Resource、Tools、Odin 或 Newtonsoft；Core/Runtime 继续互不引用。资源契约在 Foundation，真实后端在 Runtime，通过显式装配协作，不把加载系统下沉。未来外部适配仍保持叶子程序集，不进入默认依赖闭包。

公开 `YLFramework.Core`、`YLFramework.Actions`、`YLFramework.Services.*`、工具 namespace 和已经形成的 API 保留。`YLFramework.Core.Pool/Singleton/IOC` 的前缀确有归属误导，但它们已经是正式 YL 身份，不是待删除的 XF API。推荐本阶段以目录与程序集导航纠正认知，**不默认公开改名**；若用户选择清理这些 FullName，须单独进行消费者/反射/序列化及 Analyzer 迁移，不与纯目录移动捆绑，也不预建兼容壳。

绝不能改动游戏持久入口：游戏 Prefab 的 UnityEvent 已保存 `YLFramework.Services.Audio.AudioEmitter, YLFramework.Runtime`，并引用 AudioEmitter/MusicPlayer 的 GUID 和字段；详见 `Assets/Games/MainModule/UI/StartPanel/StartPanel.prefab:204`、`UI/PausePanel/PausePanel.prefab:189`、`UI/GameOverPanel/GameOverPanel.prefab:311`，以及 `Assets/Games/MainModule/Tests/EditMode/YLAudioWiringTests.cs:27-60`。保留 GUID 不能弥补程序集限定类型名或字段契约变化。

### 3.2 Analyzer 是双轴门禁

唯一 DLL 仍位于 **Core.Runtime asmdef 子树内**，迁后为 `Runtime/Core/Analyzers`；不移入 Kernel、Runtime 根、Editor 或独立 Analyzer asmdef。保留 GUID `471f81c4273ee66429677df44cbd6960`、RoslynAnalyzer 标签、显式引用及普通平台禁用，不加入 Runtime precompiledReferences。

- **注入轴：** Unity 按 Analyzer 所处 asmdef 范围与消费者引用关系注入，不由 GUID 保证。迁后必须读取本轮实际编译参数，验证 Core/直接消费者有 `-analyzer`，普通 Runtime/Foundation/Help/游戏等无关程序集没有新增作用域；传递引用与预定义程序集按真实图对照，不能推定全局覆盖。依据：[Unity 2022.3 官方说明](https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html)。
- **符号轴：** `Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs:28-55` 写死 Core 程序集名、View/标记/参数请求和 Foundation 的 `YLFramework.Core.Pool.ObjectPool<T>`。类型或程序集身份变化可能令分析器静默不注册，零错误不等于正常。
- **必要联动：** `build.ps1:17` 部署路径及 `Tests/AnalyzerFixture.cs:55` 标记源码路径随移动更新；仓库 SDK 重建入口须可用，UnityPackage 仍只带 DLL/meta/说明，不带 SDK、Roslyn/NuGet 和中间产物。
- **验收：** 合法消费者无误报，七项负例必须实际产生 YL0001～YL0007 的指定 Warning/位置，无 AD0001/load error；保留两个内部 owner 的边界。只改路径不要求重写规则；若身份改动导致源码/DLL变化，再按影响重建并重跑 SDK 契约。Player 可有编译器 `-analyzer` 输入，但不得有 Analyzer/Roslyn/自有 Editor 运行引用。

## 4. Resource 与原生态独立性

### 4.1 当前不能宣称独立

默认产品闭包中没有原 XFGameFramework/XFFSM 的直接引用，但 Core Runtime/Core Editor 仍硬引用 XFABManager。这足以阻止无 XFAB 的完整包编译；可选 Audio.XFAB 不属于默认包，不能与该硬依赖混为一谈。

| 真实依赖 | 证据 | 必须收口的责任 |
|---|---|---|
| 命名 Prefab、实例取得/回池 | `C/Manager/ViewManager.cs:220,334-351,440`；`LoadViewRequest.cs:229-254` | 同步/异步资产租约、直接 Prefab 与命名 Prefab 的实例 owner |
| 启动 JSON/表、默认场景 | `C/AsyncOperation/StartUpModuleRequest.cs:118-167,198-220` | 复制文本后释放、失败传播、新 Scene 关联及启动回滚 |
| 资源组公开含义 | `C/Module/Module.cs:81-97`；`ModuleManager.cs:313-322` | ProjectName 及 owner→BaseModule 查找顺序不能被忽略 |
| Core Editor 资产定位 | `E/XFMain/Other/EditorAssetTool/EditorAssetTool.cs:5,56-83` | 退出原项目/Bundle 类型引用，保持有价值的自有资产工具用途 |
| 测试与验收承载 | `T/Core/Acceptance/YLFramework.UI.Acceptance.asmdef:4`、`RuntimeAcceptanceEntry.cs:124-134` | 原验收程序集硬引用游戏/XF/XFFSM，不能整体移入独立载体 |

原游戏继续保留原包及原资源链，**不从原工程 manifest 卸载任何 XF 系统**。去依赖只作用于 YL 正式 asmdef/代码/资产/工具闭包；独立载体不安装原系统，二者不是同一操作。

### 4.2 候选路线与推荐

| 路线 | 代价与判断 |
|---|---|
| Core→整个 Runtime，直接使用 ResLoader | 修改少，但把整体服务/Root/插件闭包带给 Core，推翻已验收的隔离，且仍缺实例 owner；不推荐 |
| **中立窄契约 + Runtime 原生桥接** | 复用现有 ResLoader/ResManager、GO 池及场景能力，保住程序集方向；必须真实实现资源租约与实例复用责任，推荐 |
| 抽取完整 Resource 独立程序集 | 牵连 Safe 池、Singleton、Table、Extensions、Root 等跨模块身份，属于更大资源架构专题；不推荐本阶段开展 |
| 保留硬 XFAB、复制 Package 或空后端 | 前者只能交付依赖 XFAB 的过渡版本，后两者违反目标与授权；均不算独立完成 |

推荐契约仅包含 Unity/BCL 的地址、资产/实例借用与释放、加载结果/进度和 Scene 结果，不含 Module/View、ResLoader、GameObjectPool 等上层类型，也不建设通用异步框架。Core 仍管理生命周期、参数、逻辑取消与提交；Runtime 真实桥接管理资源执行。显式装配在资源首次使用前完成，Core 不根据“安装了 XFAB”自动回退，也不为无资源操作的 MVC 惰性创建后端。

**必须解决的真实缺口：** `R/Resource/GameObjectLoader.cs:8-16,26-48` 只持有 Prefab 并 Instantiate，不负责实例池；而 `CoreDirectViewPlayModeTests.cs:880-922` 已要求 Close 后回池和同实例复用。现有 `R/Foundation/Pool/GameObject/GameObjectPool.cs:8-15` 实际属于 Runtime，明确不拥有 Prefab/Bundle 资产。原生桥接应组合现有池与 ResLoader，保住闲置/借出实例所需资产，隔离自有桶并有明确清理；不能仅把 XFAB 调用换成 Instantiate/Destroy，不能复制第二套通用池。

### 4.3 后端与公开语义门禁

- **资源寻址：** 推荐保留 ProjectName 的 public virtual get/set 和启动前捕获形状，将其含义改为后端解析的资源组；原生映射必须显式、可检查。不得默默忽略 scope、假设全工程资产短名唯一，或把 XFAB 项目格式等同于 YL Bundle/SubProject。`AssetDataTable` 当前无 ownerBundle 时可能取首个同名项；具体映射规则在资源批次提交审核。
- **租约/实例：** 请求接受时捕获 producer、Loader/池 owner 与会话；取消/退出后的迟到完成只能归还原取得结果，不能交给新会话。Direct Prefab 是借用资产，不错误卸载调用方资产。健康实例回池、污染实例退出缓存；不清理其他消费者的全局池或资源。
- **启动表：** 沿用文本复制后释放及原异常/附加清理错误契约。原 XFAB 的专用表前提源于无调用级租约；只有真实原生租约及共享测试成立后，才在获批契约中放宽该前提，不凭接口名称承诺安全共享。
- **场景：** 复用现有 YL Bundle 场景链，明确新 Scene 的实际 handle、Single/Additive、释放 Bundle 与卸载 Scene 的区别。`R/Resource/Framework/ResLoader/ResLoaderExtensions.cs:284-287,320-334` 在非法地址/Bundle失败时仅记录日志、不会调用 onStartLoading；不能直接包装成永不结束的启动请求。普通 Build Settings 场景与已加载 Bundle 场景的定位前提分别说明，验证本批选用路线，不据此新增场景后端模式。交给 SceneManager 后不承诺物理取消，启动回滚不恢复已被 Single 卸载的场景。
- **停止条件：** 若必要适配要求重做资源计数/Loader 会话模型、完整热更新、原产物格式兼容或所有场景/AB 的物理取消释放策略，停止该批并转资源专题；不删能力、吞失败或添加永久补丁层。

现有 Audio.XFAB 保持独立、可选且不默认导出。没有发现原游戏使用新 Core 的证据，**不预建永久 Core-XFAB 兼容适配**；旧测试不构成产品兼容需求。若批次研究发现真实外部 Core 消费者，再提出有限适配及退出条件供审核。

阶段 7 的可达目标是主框架默认闭包不依赖原 15 系统，并在原生资源路线上运行；不等于完成 XFABManager/XFFSM 等 14 个后续系统融合，也不承诺完整资源热更新、任意共享 AB 的物理卸载、IL2CPP/AOT 或跨平台。

## 5. Tools、Editor 与正式交付

### 5.1 保留的能力

FSM、Grid、JSONObject、Table、ZipTool 继续正式交付，保持当前入口、namespace、程序集和来源许可。轻量代码 FSM 由调用方驱动/回收，不与未来可视化 XFFSM 合并；Table 只管理索引/集合，不获得资源释放权；JSONObject 与 Json.NET 强类型序列化不统一；ZipTool 保持 noEngineReferences。当前缺少部分文档曾声称的工具全集测试，不据此重建无关套件或删除工具。

三套 Editor/Help 继续单向协作，保留 Help id、菜单、窗口 MovedFrom、只读行为和 Resource 标记/构建/生成器。Core Editor 去 XFAB 与对应 Runtime 接入同批完成，不合并普通 Editor。Core XML 缺失已有真实声明回落机制，不是独立交付阻塞；不为填满目录/帮助条目而另建文档平台或强制新增 Core csc.rsp。

### 5.2 默认包与必要依赖

沿用 `E/ExportUnityPackage.cs:16-24,66` 的六根：Editor、Plugins、Prefabs、Runtime、Samples、Documentation，以及 **Recurse、完整 Plugins 递归、不用 IncludeDependencies**。根 Integrations、Tests、游戏/Bundle、Examples/YLFrameworkData、SDK/NuGet/缓存/中间产物继续排除；不建设 UPM、安装管理器或 Editor 超级平台。

| 独立载体输入 | 实际证据与恢复口径 |
|---|---|
| Unity 2022.3.62f3c1 + Windows64/Mono | 首个真实独立验收目标，非所有 Unity/平台兼容声明 |
| UGUI | asmdef 与正式 Prefab 的包外脚本 GUID 依赖；本机实际 1.0.0，载体按该 Editor 的兼容版本明确解析 |
| `com.unity.nuget.newtonsoft-json` 3.2.1 | Runtime/Core/Editor 使用；原工程仅由 XFAB 传递带入（lock:361-369）。**独立工程显式声明**，原工程 Packages 不改 |
| 随包 Odin / Editor Enhancers | 按既定 Plugins 全递归保留；Odin Attributes 被 Root/EventListener 使用，不是假可选依赖 |
| `com.unity.mathematics` 1.2.6 基线 | 随包 Odin UnityMathematics asmdef:3-10 硬引用且无 defineConstraints；载体显式满足，不擅自裁剪插件 |
| Input System / Test Framework | Input System 产品侧为反射接入，基本载体可用旧输入；验证已保留的新输入能力时再提供兼容包。Test Framework 1.3.9 仅为验证输入，不是发行 Runtime 必需项 |

当前正式 Prefabs/Samples/Core 默认资源未发现 URP 特有资产引用；URP、Cinemachine、Timeline、MCP 不是据原工程 manifest 强加给主框架的必要依赖。最终以完整导出包的实际资产/插件闭包重新核验，不能把源码白名单当作干净导入结果。

XF/QFramework/JSONObject/SharpZipLib 的完整许可和通知随迁后路径继续发行。Odin、Editor Enhancers 的公开再分发授权没有充分证据；**技术独立与内部交付可验收，公开发布许可仍为独立停止门禁**。不以减少 Plugins 或改变导出范围来规避已确认决策。

## 6. 实施批次与最小充分门禁

以下六批按变更轴安排，不按目录机械分割。每批实施前重核基线和并行变化；先完成本批详细设计与必要审批，再执行。小批失败即停止，不在未通过的结构/owner 假设上叠加后续迁移。

### 7.1 Foundation 逻辑归口，保持类型身份

- **目标/前置：** 经总体结构审核，将 Shared 中立闭包归入 Foundation/Kernel；现有 Foundation 高层入口原地保留。先核实实际编译归属与路径消费者。
- **退出/保护：** 退出根 Shared 与查实空壳目录，保持唯一 Pool/Events/Update/Timer/字典内核、friend、所有 namespace/asmdef/GUID、Root 和成熟领域实现。
- **必要门禁：** Unity 实际编译；FoundationBoundary、CoreIdentity；直接路径敏感的 AudioSourceIndependence 两 profile、字典/Attributes 身份及真实导出哨兵。只选 Binding/集合池等直接消费者，不重跑支撑五专题全集。
- **独立停止：** 出现程序集重归属、Odin/UGUI/Runtime 反向边、既有数据/反射身份变化，停止纯路径批并单独审核，不借迁移修改算法或公开 API。

### 7.2 Core 职责目录与 Analyzer 接线收口

- **目标/前置：** 7.1 通过；Runtime/XFMain→Core、Editor/XFMain→Core，按 MVC/View/UI/组件收敛杂物分类。迁移前同步默认资产生成/监听的路径方案，在受控导入中避免旧路径副本。
- **退出/保护：** 退出来源目录与跨领域组织，不改 Module/MVC/View/Panel 事务、UI 策略/字段、标记类型、程序集名称、Resources key、持久设置的 FullName 路径和既有生成资产身份。
- **必要门禁：** CoreIdentity/CoreEditorIdentity、CoreDefaultResource 与 Editor 版、配置/路径直接测试；正式 Prefab/Sample 无 Missing Script；Help 对应 provider、真实导出、SDK 路径重建入口与第 3.2 节实际 scope/七项负例。无符号或 DLL 变化不机械重跑全部 SDK 内部场景。
- **独立停止：** 新旧 Core/Analyzer 副本、默认器重复生成、负例零诊断、作用域污染或资源身份断裂，均阻止进入资源接入批。不通过改 ruleset/关闭规则制造成功。

### 7.3 窄资源契约与原生执行 owner

- **目标/前置：** 7.1/7.2 通过，资源所有权与 ProjectName 方向获得审核。详细设计中明确地址映射、显式装配、未配置/加载失败终态、Loader/实例/Scene 生命周期，然后再新增契约与真实桥接；Core 旧执行链暂不切换。
- **退出/保护：** 为退出硬 XFAB 建立真实替代前提；复用 ResLoader/ResManager、现有 GO 池与场景支撑，不重写 Resource/池，不动 Audio 默认 provider、全局游戏资源模式或原产物。
- **必要门禁：** 用自有 fixture 验证同步/异步、缺失/错误类型、同名资源组隔离、共享表租约、直接/命名 Prefab、原实例复用、污染丢弃、asset 保活、清理/晚还与失败重入；选定场景路线的成功和失败必须终结。小型真实 YL Bundle/ResData 闭环复用 ResourceBuildIntegration 与 AudioNamedBundle 的现有构建方法，输出仅限自有临时位置，不写原 MainModule/全局产物。
- **独立停止：** 租约未闭环、跨会话向新池归还、Scene 失败挂起，或必须大规模改成熟 Resource 才能满足契约，停止并提交专题边界调整；不能先让 Core 接入再补 owner。

### 7.4 Core 接入原生资源，退出默认 XF 依赖

- **目标/前置：** 7.3 原生 owner 门禁通过且具体公开契约获批。按启动表/场景、命名 View、直接实例取得/归还的事务路径分小步接线；保留 owner→BaseModule 查找及既有失败/取消顺序。
- **退出/保护：** 删除 Core Runtime/Editor 的 XFAB 类型调用与 asmdef 边；处理 EditorAssetTool 的原项目绑定用途及模板说明，不保留无依据兼容层。原包/游戏仍走原线，Audio.XFAB 可选边界不变。
- **必要门禁：** 复用 CoreModule、CoreFailurePath、CoreDirectView、原 CoreXfab 中有效生命周期/加载/回滚断言，改原生输入和 owner 断言而非削弱测试；针对 Panel/窗口/ColorSelector、场景 attach、参数归还、取消迟到及同实例重启选择直接回归。CoreEditorIdentity/生成模板与无 XF 的 Framework 编译闭包必须成立。
- **独立停止：** 原生定位不能保留资源组/回退约束、关闭变成销毁而非已批准复用、启动/取消契约改变未获批，或需要迁移原游戏资产时停止。旧 XFAB fixture 可归可选测试，不再作为默认能力证明；不把其测试历史删除当迁移成功。

### 7.5 正式入口、Help 与交付契约收口

- **目标/前置：** 结构与资源路线已稳定；增量维护现有 Architecture、README、相关模块说明、Help、来源许可导航和导出哨兵，说明 Foundation 层级、资源装配/地址、外部依赖及残留限制。
- **退出/保护：** 退出失效 XFMain/Shared 产品路径、默认 Core 的 XFAB 说明和虚构测试导航；保留 Tools 入口/说明、三套 Editor、Help 注册与缺 XML 回落、资源工具、六导出根和全部 Plugins。
- **必要门禁：** 对受影响 Help/provider/生成器作直接回归；实际导出并解包，核对全部递归文件内容、meta/GUID、迁后 Analyzer 唯一身份/禁用平台、许可与排除项。记录本轮真实包指纹，不沿用 6.5 的记录数量或旧 SHA。
- **独立停止：** 缺外部依赖恢复说明、默认包遗漏 owner/契约/正式资源、Plugin 范围意外变化或许可通知丢失，停止交付；公开再分发仍需独立许可确认。

### 7.6 真实独立载体与最终兼容验收

- **目标/前置：** 使用 7.5 的最终真实 UnityPackage，在工程外新建同版本 Unity 载体，明确提供第 5.2 节必要依赖；不复制原工程 Library、ProjectSettings、manifest、游戏或原 XF DLL/Package。不对已打开的同一工程再启动 Unity 实例。
- **首次证明：** 真实导入/编译和重开后资产身份；最小业务 asmdef/场景仅引用所需 YL/Unity 类型；Module/MVC 启动关闭重启、原生 JSON、直接/命名 View/Panel、复用/取消迟到、Single/Additive、新场景 attach，及一条真实 YL Bundle 消费链。不能只展示空 Module 或同工程 AssemblyBuilder 通过。
- **Editor/Analyzer/Tools：** 创建模块与默认资源、原生资源标记/构建/常量生成、Help GUI、Prefab/Sample、七项分析器正反例/作用域与一个必要配置覆盖。FSM/Grid/Json/Table/Zip 只做接入和代表性协作冒烟，不补造其历史全集。
- **Player：** 在独立载体实际构建并运行 Windows64/Mono Development Player，核验 Core/资源/代表性 UI/Audio/HTTP 协作及退出；无原生态、自有 Editor、Analyzer/Roslyn 运行依赖。编译参数、PE/运行引用与运行结果分别报告。
- **会话/原游戏：** 独立载体做一次真实关闭 Domain Reload、正常 Scene Reload 的连续双 Play，检查新桥接/旧租约/宿主退出；无需原 Logger 隔离。原工程按最终 Runtime/资源风险补原 Main/XF/XFAB/XFFSM/YL Audio 的代表性 Editor 与 Windows64/Mono 非干扰路径，不重建原 Bundle。历史 Logger 临时隔离授权不可沿用，产物/环境不足或外部错误阻断则报告并停止相应门禁。
- **独立停止：** 任一隐性 XF 引用、必要插件缺失、真实资源启动失败、Analyzer 静默失效、Missing Script、Player 泄漏 Editor/编译器依赖或原游戏新增回归，均不宣布阶段 COMPLETE。验证设置仅修改独立载体；原工程需要配置变更时先取得单独授权，保留用户未保存场景与受保护文件。

### 验收证据使用原则

阶段 1～6 已验收的功能契约和未受影响测试可以沿用，但不是新结构或独立工程的通过证据。路径/身份变化重测直接消费者；资源 owner 变化重测取得/提交/失败/归还闭环；独立导入是首次证明；最终 Player/游戏非干扰针对实际跨模块风险执行一次，不机械重跑历史全部测试。静态检查、SDK 测试、Unity 编译、EditMode/PlayMode、导出解包、独立导入、Player 构建/运行及人工结果分层报告。

## 7. 已批准决策与完成条件

以下 D1～D4 为批准的总体决策；7.1～7.6 六批路线及既有风险门禁同时确认，不重新研究或重新拆分阶段。

| 决策 | 已批准内容 |
|---|---|
| D1 最终结构 | `Runtime/XFMain → Runtime/Core`；`Runtime/Shared → Runtime/Foundation/Kernel`；`Editor/XFMain → Editor/Core`。保留合理的 Foundation 高层入口、成熟大型系统、Tools 及现有程序集身份 |
| D2 独立资源路线 | 批准 Foundation 中立窄契约 + Runtime 真实原生 Resource owner 的总体方向；允许 `ProjectName` 保持既有公开属性形状并转向显式映射的资源组语义。具体 API、资源组映射、装配时机、资产租约、实例池 owner、取消/迟到归还及 Scene 生命周期必须在 7.3 实施前完成专项设计审核。不批准直接复制 XFAB、添加空资源后端、隐式回退 XFAB 或重写整个 Resource |
| D3 公开身份 | 保留四套 Runtime 与三套 Editor 程序集的正式名称/GUID；保留现有 YL 公共 namespace、类型 FullName、序列化契约和成熟工具，不为目录一致性机械改名 |
| D4 独立交付 | 阶段 7 目标为默认主框架在不安装原 XF 生态系统的独立 Unity 工程中真正可用；必要 Unity 与第三方依赖允许显式提供，不追求零依赖。默认 Plugins 继续完整递归导出；公开再分发许可仍属后续独立限制 |

总体批准只确认路线与停止门禁；每批仍须单独获授权，**7.1 待单独实施授权**。D2 的总体方向已批准，但资源所有权与公共接入的具体契约仍以 7.3 实施前的专项设计审核为准，不能视为已批准全部细节或可直接开始改代码。

**阶段完成条件：** 推荐结构与正式职责一致；默认 Runtime/Editor/资产/Analyzer/保留工具链无原生态直接或间接可执行依赖；最终包在真实独立载体中导入、启动、Editor/Analyzer 与 Windows64/Mono Player 通过；原游戏代表性非干扰通过；GUID、序列化/反射及必要公共身份保留；分发范围、必要依赖和公开许可限制准确。其他未验证平台/资源能力明确保留，不扩大 COMPLETE 含义。

**阶段 7 总体方案已批准；7.1 待单独实施授权，不自动实施任何批次。**
