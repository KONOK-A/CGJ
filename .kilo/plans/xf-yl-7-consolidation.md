# XF × YL：阶段 7 最终架构收口与独立交付总体方案

**状态：COMPLETE（2026-10-14）：7.1、7.2 技术验收通过；7.R/7.O1 补充与 §16 两主体收口完成，随 `milestone/7-structure` Git 收口。** 阶段 7 具体路线已由用户最新确认，调整为“框架结构整理”，仅保留两个结构批次：

- **7.1 COMPLETE** `Runtime/Shared → Runtime/Foundation/Kernel`（实施、用户技术验收与 Git 收口已完成，`0f9876f`）。
- **7.2 COMPLETE**（技术门禁通过，随阶段 7 Git 收口）`Runtime/XFMain → Runtime/Core`、`Editor/XFMain → Editor/Core`，Core 内部职责目录、默认资源路径与 Analyzer 接线已同步，见第 8 节。

阶段 7 保持公开类型、程序集身份与功能契约；**暂时保留 Core 对 XFAB 的现有依赖，不改变 `ProjectName` 语义，不新增资源替代后端**。阶段 7 完成标准改为两个结构批次及必要迁移验证通过，不再要求在本阶段完成无 XFAB 的独立资源运行。

原 7.3/7.4 的“先补 YL 原生替代后端、再去 XFAB”路线，以及与之绑定的 D2 具体方向，已被新路线替代（见第 4、6、7 节）；资源系统深度融合调整为**阶段 8**（以 XFAB 成熟能力体系为主体），最终收口与独立交付调整为**阶段 9**。阶段 8、9 不在本轮生成专项方案，本轮不自动实施 7.2，也不启动资源系统研究。

**目标：** 保留阶段 1～6 的正式能力与身份，收口历史目录与依赖，使 Foundation 逻辑归口与 Core 结构清晰。阶段 7 只做两个结构批次及必要迁移验证；资源系统在阶段 8 以 XFAB 为主体深度融合，形成 YLFramework 自己完整、统一、可维护的资源系统；最终在阶段 9 于真实独立载体中收口验收。遵循“融合优先、重构其次、重写最后”，同时允许有依据的合理创新与必要重新设计，不追求零第三方依赖。

## 1. 实际基线与模块归属

### 1.1 Git、环境与保护范围

- 2026-10-13 已实际读取项目 `AGENTS.md`、检查 Git，并在任务中断后重新核对：HEAD、本地 `integration/xf-yl`、本地 upstream 和实时远端均为 `277d4f90eb5e32480f62dbe0661369d7e302c72e`。
- `milestone/6-editor` 为 Annotated Tag，本地与远端 tag object 均为 `d4e5e84be502ba5fbe91df3a19a9a95e3f3a5431`，解引用指向上述阶段 6 最终 Merge Commit。两个父提交为 `7db5806…` / `ddb7c8d…`；第二父提交与本地、实时远端 `fusion/6-editor` 最终 6.5 交付一致。基线门禁通过，没有补做 Git 操作。
- 主工作区已有 `.gitignore`、`.kilo/kilo.jsonc` 未暂存修改，暂存区为空；原样保护。已有 `test-worktree` 位于 `.kilo/worktrees/test-worktree`，基线 `09e3fe0`，有用户未跟踪文件 `Assets/worktree-test.txt`；不修改、清理或将其作为本阶段基线。工作树与会话配置只证明存在，不证明另一个参与者是否正在操作。
- Unity 为 `2022.3.62f3c1`。已核对 manifest、锁文件及必要安装源码；UGUI 声明/锁定为 2.0.0，而实际安装为 1.0.0，原工程还存在 URP 等历史声明/解析差异。独立载体不能照抄该工程的 manifest。
- 原 XF/XFAB/XFFSM 与其他原生态 Package 严格只读；原游戏业务、Scene、Prefab、资源、Bundle、Packages、ProjectSettings 不迁移、不修改。不创建实施分支，不自动 Commit、Push、Merge、Tag、Reset 或 Clean。

定向阅读了 [长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)、[Architecture](../../Assets/YLFramework/Documentation/Architecture.md)，阶段 1.1～1.3、阶段 2 五个专题及阶段 3～6 的最终设计/验收章节，包括 [6.4 Analyzer 专题](xf-yl-6.4-analyzers.md)。部分旧方案仍保留当时“待验收”文字，当前阶段 1～6 COMPLETE 以较新的蓝图、最终验收及 Git 里程碑为准。本轮只有静态研究与 Git 核验，没有执行 Unity 编译、测试、Play 或构建。

下文源码简称：`R` = `Assets/YLFramework/Runtime/`，`C` = `R/XFMain/`，`E` = `Assets/YLFramework/Editor/`，`T` = `Assets/YLFramework/Tests/`。行号对应本研究基线。

### 1.2 当前真实归属（7.1 后）

| 位置 / 程序集 | 当前职责与依赖 | 判断 |
|---|---|---|
| `R/Foundation/Kernel` / `YLFramework.Foundation.Runtime` | Pool/集合内核、EventBus/channel、Update、Timing、协程/会话、序列化字典、类型查找、展示元数据；显式 references=[] | 7.1 从 Shared 整闭包迁入，保持中立程序集及唯一实现 |
| `R/Foundation`（除 Kernel） / **`YLFramework.Runtime`** | Binding、EasyEvent/全局便捷入口、IOC、Safe/GO 池、普通/Mono/资产单例 | 不是第二套底层内核；是较高层入口和 Unity 适配，保持原程序集归属 |
| `C` / `YLFramework.Core.Runtime` | Module/MVC、View/Panel/UI、领域请求、组件/监听及分析器标记；依赖 Foundation、UGUI、过渡 XFAB | 正式 Core 已形成，`XFMain` 不应继续作为永久分类 |
| `R/{Action,Audio,Resource,Save}`、`Services/Http` | 完整系统与轻量 HTTP，共用 Runtime；Runtime→Foundation、ZipTool、UGUI | 职责合理，保留领域所有权与目录策略 |
| `R/Tools` | Extensions、StringTools、FSM、Grid、Json、Table；ZipTool 独立无引擎程序集 | 保留成熟工具，不再按来源拆分 |
| `E`、`E/XFMain`、`E/Help` | 普通 Editor、Core Editor、无模块反向依赖的 Help 核心 | 三程序集保留；Core 对 XFAB 的过渡依赖在阶段 7 保留，由阶段 8 资源融合处理 |
| 根 `Integrations` | 可选 DOTween Action、XFAB Audio 适配 | 与正式 Runtime 分离，保持不默认导出、不自动注册 |

边界依据：四份 Runtime asmdef；`R/Foundation/Kernel/Internal/AssemblyInfo.cs`；`T/Foundation/EditMode/FoundationBoundaryTests.cs`；`T/Core/EditMode/CoreIdentityTests.cs:96-117`。原游戏 `Project.Game.MainModule.asmdef:4-12` 引用 Runtime、原 XF/XFAB/XFFSM 等，但不引用新 Core；`MainModule.cs:4,27-28` 实际消费正式 YL Audio。游戏不是可忽略的 Runtime 消费者。

## 2. Runtime 最终组织

### 2.1 推荐目录

```text
Assets/YLFramework/Runtime/
  YLFramework.Runtime.asmdef / csc.rsp / YLFrameworkRoot.cs
  Foundation/
    Kernel/                           # 由 Shared 移入；中立共享闭包
      YLFramework.Foundation.Runtime.asmdef
      Pool/ Events/ Timing/ Serialization/ Attributes/ Internal/ ...
      # 原计划在此新增的窄资源契约（原 D2 方向）已被阶段 8 新路线替代，阶段 7 不新增
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

Foundation 不反向引用 Core、Runtime、Resource、Tools、Odin 或 Newtonsoft；Core/Runtime 继续互不引用。未来外部适配仍保持叶子程序集，不进入默认依赖闭包。（原“资源契约在 Foundation、真实后端在 Runtime 的显式装配协作”属原 D2 方向，已由阶段 8 新路线替代。）

公开 `YLFramework.Core`、`YLFramework.Actions`、`YLFramework.Services.*`、工具 namespace 和已经形成的 API 保留。`YLFramework.Core.Pool/Singleton/IOC` 的前缀确有归属误导，但它们已经是正式 YL 身份，不是待删除的 XF API。推荐本阶段以目录与程序集导航纠正认知，**不默认公开改名**；若用户选择清理这些 FullName，须单独进行消费者/反射/序列化及 Analyzer 迁移，不与纯目录移动捆绑，也不预建兼容壳。

绝不能改动游戏持久入口：游戏 Prefab 的 UnityEvent 已保存 `YLFramework.Services.Audio.AudioEmitter, YLFramework.Runtime`，并引用 AudioEmitter/MusicPlayer 的 GUID 和字段；详见 `Assets/Games/MainModule/UI/StartPanel/StartPanel.prefab:204`、`UI/PausePanel/PausePanel.prefab:189`、`UI/GameOverPanel/GameOverPanel.prefab:311`，以及 `Assets/Games/MainModule/Tests/EditMode/YLAudioWiringTests.cs:27-60`。保留 GUID 不能弥补程序集限定类型名或字段契约变化。

### 3.2 Analyzer 是双轴门禁

唯一 DLL 仍位于 **Core.Runtime asmdef 子树内**，迁后为 `Runtime/Core/Analyzers`；不移入 Kernel、Runtime 根、Editor 或独立 Analyzer asmdef。保留 GUID `471f81c4273ee66429677df44cbd6960`、RoslynAnalyzer 标签、显式引用及普通平台禁用，不加入 Runtime precompiledReferences。

- **注入轴：** Unity 按 Analyzer 所处 asmdef 范围与消费者引用关系注入，不由 GUID 保证。迁后必须读取本轮实际编译参数，验证 Core/直接消费者有 `-analyzer`，普通 Runtime/Foundation/Help/游戏等无关程序集没有新增作用域；传递引用与预定义程序集按真实图对照，不能推定全局覆盖。依据：[Unity 2022.3 官方说明](https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html)。
- **符号轴：** `Tools/YLFramework.Analyzers/Analyzer/AnalyzerSymbols.cs:28-55` 写死 Core 程序集名、View/标记/参数请求和 Foundation 的 `YLFramework.Core.Pool.ObjectPool<T>`。类型或程序集身份变化可能令分析器静默不注册，零错误不等于正常。
- **必要联动：** `build.ps1:17` 部署路径及 `Tests/AnalyzerFixture.cs:55` 标记源码路径随移动更新；仓库 SDK 重建入口须可用，UnityPackage 仍只带 DLL/meta/说明，不带 SDK、Roslyn/NuGet 和中间产物。
- **验收：** 合法消费者无误报，七项负例必须实际产生 YL0001～YL0007 的指定 Warning/位置，无 AD0001/load error；保留两个内部 owner 的边界。只改路径不要求重写规则；若身份改动导致源码/DLL变化，再按影响重建并重跑 SDK 契约。Player 可有编译器 `-analyzer` 输入，但不得有 Analyzer/Roslyn/自有 Editor 运行引用。

## 4. Resource 与原生态独立性（背景证据；具体路线已由阶段 8 替代）

> **路线状态：** 本节原推荐的“Foundation 中立窄契约 + Runtime 真实原生 Resource owner，先补 YL 替代后端再去 XFAB”整体方向（4.2、4.3 及 D2）**已被新路线替代**。阶段 7 保留 Core 对 XFAB 的现有依赖；资源系统改为阶段 8 以 XFAB 成熟能力体系为主体深度融合。以下 4.1 的真实依赖清单继续作为阶段 8 研究输入保留；4.2/4.3 的推荐与门禁不再作为阶段 7 目标，仅在阶段 8 重新研究后再定。

### 4.1 当前不能宣称独立（事实基线，仍有效）

默认产品闭包中没有原 XFGameFramework/XFFSM 的直接引用，但 Core Runtime/Core Editor 仍硬引用 XFABManager。这足以阻止无 XFAB 的完整包编译；可选 Audio.XFAB 不属于默认包，不能与该硬依赖混为一谈。阶段 7 不解除该依赖。

| 真实依赖 | 证据 | 必须收口的责任 |
|---|---|---|
| 命名 Prefab、实例取得/回池 | `C/Manager/ViewManager.cs:220,334-351,440`；`LoadViewRequest.cs:229-254` | 同步/异步资产租约、直接 Prefab 与命名 Prefab 的实例 owner |
| 启动 JSON/表、默认场景 | `C/AsyncOperation/StartUpModuleRequest.cs:118-167,198-220` | 复制文本后释放、失败传播、新 Scene 关联及启动回滚 |
| 资源组公开含义 | `C/Module/Module.cs:81-97`；`ModuleManager.cs:313-322` | ProjectName 及 owner→BaseModule 查找顺序不能被忽略 |
| Core Editor 资产定位 | `E/XFMain/Other/EditorAssetTool/EditorAssetTool.cs:5,56-83` | 退出原项目/Bundle 类型引用，保持有价值的自有资产工具用途 |
| 测试与验收承载 | `T/Core/Acceptance/YLFramework.UI.Acceptance.asmdef:4`、`RuntimeAcceptanceEntry.cs:124-134` | 原验收程序集硬引用游戏/XF/XFFSM，不能整体移入独立载体 |

原游戏继续保留原包及原资源链，**不从原工程 manifest 卸载任何 XF 系统**。去依赖只作用于 YL 正式 asmdef/代码/资产/工具闭包；独立载体不安装原系统，二者不是同一操作。

### 4.2 候选路线与推荐（已被阶段 8 替代，保留为历史分析）

| 路线 | 代价与判断 |
|---|---|
| Core→整个 Runtime，直接使用 ResLoader | 修改少，但把整体服务/Root/插件闭包带给 Core，推翻已验收的隔离，且仍缺实例 owner；不推荐 |
| **中立窄契约 + Runtime 原生桥接** | 复用现有 ResLoader/ResManager、GO 池及场景能力，保住程序集方向；必须真实实现资源租约与实例复用责任，推荐 |
| 抽取完整 Resource 独立程序集 | 牵连 Safe 池、Singleton、Table、Extensions、Root 等跨模块身份，属于更大资源架构专题；不推荐本阶段开展 |
| 保留硬 XFAB、复制 Package 或空后端 | 前者只能交付依赖 XFAB 的过渡版本，后两者违反目标与授权；均不算独立完成 |

推荐契约仅包含 Unity/BCL 的地址、资产/实例借用与释放、加载结果/进度和 Scene 结果，不含 Module/View、ResLoader、GameObjectPool 等上层类型，也不建设通用异步框架。Core 仍管理生命周期、参数、逻辑取消与提交；Runtime 真实桥接管理资源执行。显式装配在资源首次使用前完成，Core 不根据“安装了 XFAB”自动回退，也不为无资源操作的 MVC 惰性创建后端。

**必须解决的真实缺口：** `R/Resource/GameObjectLoader.cs:8-16,26-48` 只持有 Prefab 并 Instantiate，不负责实例池；而 `CoreDirectViewPlayModeTests.cs:880-922` 已要求 Close 后回池和同实例复用。现有 `R/Foundation/Pool/GameObject/GameObjectPool.cs:8-15` 实际属于 Runtime，明确不拥有 Prefab/Bundle 资产。原生桥接应组合现有池与 ResLoader，保住闲置/借出实例所需资产，隔离自有桶并有明确清理；不能仅把 XFAB 调用换成 Instantiate/Destroy，不能复制第二套通用池。

### 4.3 后端与公开语义门禁（已被阶段 8 替代，保留为历史分析）

- **资源寻址：** 推荐保留 ProjectName 的 public virtual get/set 和启动前捕获形状，将其含义改为后端解析的资源组；原生映射必须显式、可检查。不得默默忽略 scope、假设全工程资产短名唯一，或把 XFAB 项目格式等同于 YL Bundle/SubProject。`AssetDataTable` 当前无 ownerBundle 时可能取首个同名项；具体映射规则在资源批次提交审核。
- **租约/实例：** 请求接受时捕获 producer、Loader/池 owner 与会话；取消/退出后的迟到完成只能归还原取得结果，不能交给新会话。Direct Prefab 是借用资产，不错误卸载调用方资产。健康实例回池、污染实例退出缓存；不清理其他消费者的全局池或资源。
- **启动表：** 沿用文本复制后释放及原异常/附加清理错误契约。原 XFAB 的专用表前提源于无调用级租约；只有真实原生租约及共享测试成立后，才在获批契约中放宽该前提，不凭接口名称承诺安全共享。
- **场景：** 复用现有 YL Bundle 场景链，明确新 Scene 的实际 handle、Single/Additive、释放 Bundle 与卸载 Scene 的区别。`R/Resource/Framework/ResLoader/ResLoaderExtensions.cs:284-287,320-334` 在非法地址/Bundle失败时仅记录日志、不会调用 onStartLoading；不能直接包装成永不结束的启动请求。普通 Build Settings 场景与已加载 Bundle 场景的定位前提分别说明，验证本批选用路线，不据此新增场景后端模式。交给 SceneManager 后不承诺物理取消，启动回滚不恢复已被 Single 卸载的场景。
- **停止条件：** 若必要适配要求重做资源计数/Loader 会话模型、完整热更新、原产物格式兼容或所有场景/AB 的物理取消释放策略，停止该批并转资源专题；不删能力、吞失败或添加永久补丁层。

现有 Audio.XFAB 保持独立、可选且不默认导出。阶段 7 保留 Core 对 XFAB 的现有依赖，**不预建永久 Core-XFAB 兼容适配层**；旧测试不构成产品兼容需求。Core 与 XFAB 的解除与统一在阶段 8 研究后处理。原 XFAB Package 严格只读，原游戏继续使用原资源链。

阶段 8 的目标是以 XFAB 成熟能力体系为主体深度融合资源系统，吸收现有 YL Resource、GO 池与 Resource Editor 的有价值成果，形成 YLFramework 自己完整、统一、可维护的资源系统；其最终 API、租约模型、资源组含义与装配方式在阶段 8 研究双方源码后提出并审核，本轮不预定。阶段 8 不等于完成 XFABManager/XFFSM 等全部 14 个后续系统融合，也不承诺完整资源热更新、任意共享 AB 的物理卸载、IL2CPP/AOT 或跨平台。

## 5. Tools、Editor 与正式交付

### 5.1 保留的能力

FSM、Grid、JSONObject、Table、ZipTool 继续正式交付，保持当前入口、namespace、程序集和来源许可。轻量代码 FSM 由调用方驱动/回收，不与未来可视化 XFFSM 合并；Table 只管理索引/集合，不获得资源释放权；JSONObject 与 Json.NET 强类型序列化不统一；ZipTool 保持 noEngineReferences。当前缺少部分文档曾声称的工具全集测试，不据此重建无关套件或删除工具。

三套 Editor/Help 继续单向协作，保留 Help id、菜单、窗口 MovedFrom、只读行为和 Resource 标记/构建/生成器。Core Editor 的去 XFAB 不再作为阶段 7 目标，随阶段 8 资源系统融合统一处理；阶段 7 不合并普通 Editor。Core XML 缺失已有真实声明回落机制，不是独立交付阻塞；不为填满目录/帮助条目而另建文档平台或强制新增 Core csc.rsp。

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

## 6. 实施路线与最小充分门禁

阶段 7 只包含两个结构批次（7.1、7.2），按变更轴安排，不按目录机械分割。每批实施前重核基线和并行变化；先完成本批详细设计与必要审批，再执行。小批失败即停止，不在未通过的结构假设上叠加后续迁移。阶段 8、9 的路线在本节给出，具体专项方案在对应阶段启动前另行设计与审核，本轮不生成。

### 7.1 Foundation 逻辑归口，保持类型身份

- **目标/前置：** 经总体结构审核，将 Shared 中立闭包归入 Foundation/Kernel；现有 Foundation 高层入口原地保留。先核实实际编译归属与路径消费者。
- **退出/保护：** 退出根 Shared 与查实空壳目录，保持唯一 Pool/Events/Update/Timer/字典内核、friend、所有 namespace/asmdef/GUID、Root 和成熟领域实现。
- **必要门禁：** Unity 实际编译；FoundationBoundary、CoreIdentity；直接路径敏感的 AudioSourceIndependence 两 profile、字典/Attributes 身份及真实导出哨兵。只选 Binding/集合池等直接消费者，不重跑支撑五专题全集。
- **独立停止：** 出现程序集重归属、Odin/UGUI/Runtime 反向边、既有数据/反射身份变化，停止纯路径批并单独审核，不借迁移修改算法或公开 API。

#### 7.1 实施与验证记录（2026-10-13）

**7.1 COMPLETE：用户技术验收及 Git 收口已完成，提交 `0f9876f`；阶段 7 保持 IN PROGRESS。** 下文保留 7.1 实施时的实际证据，不重复提交或重跑未受影响验证。

- 启动前实际核对项目 `AGENTS.md`、Unity `2022.3.62f3c1`、manifest、Git 状态、源码及 asmdef。工作分支为 `fusion/7-consolidation`，起始 HEAD 为 `19fd485f0c5ed93e40916f17d8a11472cc4f5e95`；仅有用户已有 `.gitignore`、`.kilo/kilo.jsonc` 修改，两者保留且不计为本批产物。既有 test-worktree 未读取、修改或清理；本轮没有 Commit、Push、Merge、Tag、Reset、Clean 或切换分支。
- 通过 Unity `AssetDatabase.MoveAsset` 将 `R/Shared` 整体迁为 `R/Foundation/Kernel`，asmdef 与全部源码同批移动，不新增 asmref、不生成替代 meta。根 Shared 及其旧路径 meta 已退出。原 Foundation 的 Binding、Events、IOC、Pool、Singleton 保持位置和 `YLFramework.Runtime` 归属；Core / Editor/XFMain / Analyzer、资源 owner 与成熟领域实现均未迁移或接线。
- Unity 实查 `R/Tools/AssemblyTool`、`R/Tools/Attributes` 为零条目的空目录，资产 GUID 分别为 `79e6be29ee1d4af89530a3dfe585c0c4`、`ed3279bece18477089b01f45c2d67464`；定向搜索 Assets / ProjectSettings 未发现自身 meta 外的引用。使用 `AssetDatabase.DeleteAssets` 仅删除这两个空目录及对应 meta，没有批量清理其他历史目录。

实际结构：

```text
Runtime/Foundation/
  Kernel/                              # 原 Shared，YLFramework.Foundation.Runtime
    YLFramework.Foundation.Runtime.asmdef
    Attributes/ Events/ Internal/ Pool/ Serialization/ Timing/
    ApplicationTool.cs / AssemblyTool.cs / LayerMaskUtility.cs
    CoroutineStarter.cs / CoroutineObservation.cs / CustomAsyncOperation.cs
    UpdateManager.cs / LICENSE.md
  Binding/ Events/ IOC/ Pool/ Singleton/
                                       # 保持 YLFramework.Runtime
```

迁移前后保护与验证：

| 门禁 | 本轮实际结果 |
|---|---|
| 文件 / meta | 迁移前快照与迁后逐项 SHA-256 比对：113 个文件完全一致，包含 49 个 C#、asmdef、LICENSE 及 62 份文件/目录 meta；无新增或遗漏 Kernel 文件 |
| 正式资产身份 | Kernel 根目录 GUID 保持 `cbd6de2a25410264eb6ff85bef17fe73`，Foundation asmdef GUID 保持 `6b2be150d22ce3f4c9d858f1dafc81a4`；所有迁移资产 GUID 保持，35 个可解析 MonoScript 的程序集限定类型名保持 |
| 公共 / 反射身份 | Foundation / Runtime / Core 三程序集共 390 个公开类型的程序集限定名、基类、公开声明成员签名及序列化字段快照一致；程序集完整名称、运行时引用与 friend 集合一致，`datas/Key/Value` 直接身份测试通过 |
| Unity 实际编译及归属 | 编译完成且 `scriptCompilationFailed=false`；迁前后实际 Editor 源码集合除路径替换外一致：Foundation 49、Runtime 230、Core 46。同步刷新后磁盘与 AssetDatabase 仅一份 Foundation asmdef；Editor / Player 图均只有 49 个 Kernel 脚本，无高层适配误编入 Kernel |
| FoundationBoundary | 7/7；新增 Kernel 根 GUID / asmdef GUID、精确闭包及高层适配归属门禁，迁前因 Kernel 不存在而失败，迁后通过；中立依赖与仅 Runtime / Core 两个 friend 保持 |
| CoreIdentity | 9/9；Core / Foundation / Runtime 原类型及依赖边界保持，未提前退出 7.4 的 XFAB 过渡引用 |
| AudioSourceIndependence | 3/3，实际执行 Editor 与 Windows64 Development 非 Editor 两种源码编译 profile 及引用白名单；仅更新 Kernel 路径，不借用项目已编译实现 |
| Attributes / AssemblyTool | 各 3/3；展示元数据及派生构造在 Editor / 无 Editor 引用 profile 编译通过，FullName 查找与缓存行为保持 |
| 直接消费者 | Binding 15/15、CollectionPool 12/12、SerializedDictionaryJson 6/6；另选字典 GUID/字段身份、Unity JSON 往返、资产保存/卸载/重载/实例化 3/3。实际 Unity 编译的 Core、Runtime、Editor 与测试消费者正常，不重跑底层算法全集 |
| Help / Prefab | 受影响 Audio Help 注册 1/1，实际 Timing 页已显示 Kernel 路径；只读检查正式 Runtime / Prefabs / Samples 下 7 个 Prefab、112 个组件，无 Missing Script，没有修改或保存游戏场景/Prefab |
| 正式导出 | ExportUnityPackageTests 1/1；实际按原六根 + Recurse 导出并解包，比对全部递归文件内容、meta/GUID、完整 Plugins、许可、Analyzer 及排除项。新增八个 Kernel 固定源码哨兵，旧 Shared 与两个空壳工具路径不进入包；未使用 IncludeDependencies |

上述定向 EditMode 共 **63/63**，无失败或跳过；MCP 的 `TotalTests` 为发现目录数量，执行数量以实际 Passed/Failed/Skipped 为准，不代表运行了历史全集。路径消费者、现有模块导航、Architecture、Timing Help 和来源许可路径已同步更新；没有新建研究报告。

最终独立只读审查未发现可行动的重要问题；复核了 HEAD 旧路径与 Kernel 新路径的 113 个文件字节、meta/asmdef、测试断言、有效路径消费者、导出哨兵及阶段边界。该审查为静态复核，不替代上述主执行者实际完成的 Unity 编译、反射、测试和导出验证。

移动导入瞬间 Unity 曾报告旧 Shared / 新 Kernel 两路径的同名 asmdef 诊断。后续同步刷新、磁盘/AssetDatabase 唯一资产核对、Editor/Player 编译图及实际编译均确认无持久重复；刷新后没有新增 Error 日志。不以清 Console、删除 Library、弱化测试或添加兼容层消除诊断。辅助 MCP 只读脚本首轮有字符串转义编译错误，修正后完成检查，未写入产品源码。

本批未改变算法、运行时 owner、公共 API 或程序集归属，因此没有额外 PlayMode、完整 Player、原游戏综合验收或独立工程导入；这些历史功能证据仅用于未受影响契约，不充当阶段 7 最终独立交付证明。7.1 用户技术验收及 Git 收口现已完成，已建立 7.2 所需的稳定 Foundation 前置；7.2 已于本轮获得单独授权。

### 7.2 Core 职责目录与 Analyzer 接线收口

- **目标/前置：** 7.1 通过；Runtime/XFMain→Core、Editor/XFMain→Core，按 MVC/View/UI/组件收敛杂物分类。迁移前同步默认资产生成/监听的路径方案，在受控导入中避免旧路径副本。
- **退出/保护：** 退出来源目录与跨领域组织，不改 Module/MVC/View/Panel 事务、UI 策略/字段、标记类型、程序集名称、Resources key、持久设置的 FullName 路径和既有生成资产身份。
- **必要门禁：** CoreIdentity/CoreEditorIdentity、CoreDefaultResource 与 Editor 版、配置/路径直接测试；正式 Prefab/Sample 无 Missing Script；Help 对应 provider、真实导出、SDK 路径重建入口与第 3.2 节实际 scope/七项负例。无符号或 DLL 变化不机械重跑全部 SDK 内部场景。
- **独立停止：** 新旧 Core/Analyzer 副本、默认器重复生成、负例零诊断、作用域污染或资源身份断裂，均阻止进入资源接入批。不通过改 ruleset/关闭规则制造成功。

### 阶段 8：以 XFAB 为主体的资源系统深度融合

> **本路线替代原 7.3/7.4 及 D2 的具体方向。** 原 7.3“窄资源契约与原生执行 owner”、原 7.4“Core 接入原生资源并退出默认 XF 依赖”不再作为阶段 7 目标；其保留的依赖事实、候选路线与语义门禁见第 4 节，仅作阶段 8 输入。

- **目标：** 以 XFAB 的成熟能力体系为主体，吸收现有 YL Resource、GO 池及 Resource Editor 的有价值成果，形成 YLFramework 自己完整、统一、可维护的资源系统。目标不止于源码承接、去重或解除程序集引用，还包括有依据的架构、性能、可靠性和资源生命周期优化，API、Editor 与开发工作流改进，以及具有明确实用价值的新能力扩展。最终不长期保留两套相互竞争的资源所有权。
- **前置：** 阶段 7 通过；阶段 8 启动前先研究 XFAB 与 YL Resource 双方真实源码、依赖、资源所有权与使用场景，再提出方案。
- **不预定：** 最终 API、租约模型、资源组含义、装配方式与批次数量均不在本轮预定，由阶段 8 研究双方源码后提出并审核。
- **退出/保护：** 原 XFAB Package 严格只读，所有融合、修复与升级只在 YL 自有源码中进行；原游戏继续使用原资源链，不迁移、不改变其行为。复用阶段 7 保留的公开类型、程序集身份与既有数据契约，重大公共接口或数据格式变化先确认。
- **必要门禁：** 按阶段 8 研究后确定的实际变更轴设计，覆盖资源取得/提交/失败/归还闭环、真实资源或 Bundle 消费链、Editor 资源工具回归及原游戏非干扰；具体门禁在阶段 8 专项方案中给出。
- **独立停止：** 出现无法在 YL 自有源码内解决的资源所有权冲突、必须迁移原游戏资产或原产物、必须破坏公开身份且未获批准时停止，提交专题边界调整。

### 阶段 9：最终收口与独立交付

承接原 7.5/7.6 的最终文档、Help、分发、依赖与真实独立载体验收目标，依据阶段 8 最终成果重新设计详细门禁；本轮不生成阶段 9 专项方案。

- **目标/前置：** 结构与资源路线已稳定（阶段 7 结构批次与阶段 8 资源融合完成）；增量维护 Architecture、README、相关模块说明、Help、来源许可导航与导出哨兵，说明 Foundation 层级、资源装配/地址、外部依赖及残留限制。
- **独立交付：** 使用最终真实 UnityPackage，在工程外新建同版本 Unity 载体，明确提供必要依赖；不复制原工程 Library、ProjectSettings、manifest、游戏或原 XF DLL/Package。首次证明真实导入/编译、资产身份、Module/MVC 生命周期、资源/View/Panel/UI/Audio/HTTP 协作、Editor/Analyzer 正反例与作用域，以及 Windows64/Mono Player 构建与运行；不新增原生态、自有 Editor 或 Analyzer/Roslyn 运行依赖。
- **会话/原游戏：** 独立载体做一次真实关闭 Domain Reload、正常 Scene Reload 的连续双 Play；原工程按最终风险补原 Main/XF/XFAB/XFFSM/YL Audio 的代表性 Editor 与 Windows64/Mono 非干扰路径。历史 Logger 临时隔离授权不可沿用。
- **退出/保护：** 退出失效 XFMain/Shared 产品路径、默认 Core 的旧 XFAB 说明与虚构测试导航；保留 Tools 入口/说明、三套 Editor、Help 注册与缺 XML 回落、资源工具、六导出根和全部 Plugins。
- **独立停止：** 缺外部依赖恢复说明、默认包遗漏 owner/契约/正式资源、Plugin 范围意外变化、许可通知丢失、任一隐性 XF 引用、必要插件缺失、真实资源启动失败、Analyzer 静默失效、Missing Script、Player 泄漏 Editor/编译器依赖或原游戏新增回归，均不宣布阶段 COMPLETE；公开再分发仍需独立许可确认。

### 验收证据使用原则

阶段 1～6 已验收的功能契约和未受影响测试可以沿用，但不是新结构或独立工程的通过证据。路径/身份变化重测直接消费者；资源 owner 变化重测取得/提交/失败/归还闭环；独立导入是首次证明；最终 Player/游戏非干扰针对实际跨模块风险执行一次，不机械重跑历史全部测试。静态检查、SDK 测试、Unity 编译、EditMode/PlayMode、导出解包、独立导入、Player 构建/运行及人工结果分层报告。

## 7. 已批准决策与完成条件

以下 D1、D3、D4 仍为批准的总体决策；**D2 的原生资源路线及原 7.3～7.6 批次已被阶段 7～9 新路线替代**（见第 6 节）。阶段 7 仅保留 7.1、7.2 两个结构批次，不重新研究或重新拆分阶段。

| 决策 | 已批准内容 |
|---|---|
| D1 最终结构 | `Runtime/XFMain → Runtime/Core`；`Runtime/Shared → Runtime/Foundation/Kernel`；`Editor/XFMain → Editor/Core`。保留合理的 Foundation 高层入口、成熟大型系统、Tools 及现有程序集身份 |
| D2 独立资源路线（**已被替代**） | 原批准“Foundation 中立窄契约 + Runtime 真实原生 Resource owner、先补 YL 替代后端再去 XFAB”的方向及 `ProjectName` 转向显式资源组语义的具体设计，已由新路线替代：阶段 7 保留 Core 对 XFAB 的现有依赖且不改 `ProjectName` 语义；资源系统在阶段 8 以 XFAB 成熟能力体系为主体深度融合。原 D2 细节不再作为当前实施约定，具体 API/租约/资源组/装配在阶段 8 研究双方源码后提出并审核 |
| D3 公开身份 | 保留四套 Runtime 与三套 Editor 程序集的正式名称/GUID；保留现有 YL 公共 namespace、类型 FullName、序列化契约和成熟工具，不为目录一致性机械改名 |
| D4 独立交付 | 最终目标（阶段 9）为默认主框架在不安装原 XF 生态系统的独立 Unity 工程中真正可用；必要 Unity 与第三方依赖允许显式提供，不追求零依赖。默认 Plugins 继续完整递归导出；公开再分发许可仍属后续独立限制 |

总体批准只确认路线与停止门禁；每批仍须单独获授权。**7.1 COMPLETE（`0f9876f`）；7.2 及 7.R/7.O1 补充完成技术验收并随阶段 7 Git 收口；阶段 8、9 未启动。** 阶段 7 保持公开类型、程序集身份与功能契约，暂时保留 Core 对 XFAB 的现有依赖；资源系统到阶段 8 再研究双方源码后提出方案，本轮不预定其细节，也不能视为可直接开始改代码。

**阶段 7 完成条件：** 两个结构批次（7.1、7.2）及必要迁移验证通过；Foundation 逻辑归口与 Core 职责目录清晰，公开类型、程序集身份与功能契约保持；不再要求本阶段完成无 XFAB 的独立资源运行。**阶段 8/9 完成条件：** 依据阶段 8 资源融合与阶段 9 独立载体成果分别重新设计；GUID、序列化/反射及必要公共身份须保留，分发范围、必要依赖和公开许可限制须准确。其他未验证平台/资源能力明确保留，不扩大 COMPLETE 含义。

**阶段 7 COMPLETE（2026-10-14 Git 收口，`milestone/7-structure`）；不启动阶段 8 资源研究。**

## 8. 7.2 实施与必要验证（2026-10-13）

本批正式授权基线为 `fusion/7-consolidation` / `0a39928`；本地与实际远端分支起始 HEAD 一致。7.1 已由 `0f9876f` 完成 Git 收口，本轮只增量修正过期状态，不补做提交。阶段 7 仍为 IN PROGRESS，7.2 的技术结果不替代用户验收或 Git 里程碑。

### 8.1 最终职责与安全迁移

| 正式目录 | 实际归属 |
|---|---|
| `Runtime/Core/MVC` | Module/SingletonModule、Model/Controller、ModuleManager；Internal 为注册索引，Events 为 Core 事件入口，Requests 为 StartUpModuleRequest |
| `Runtime/Core/Views` | View、ViewManager、LoadViewRequest；Parameters 为 LoadViewArgs |
| `Runtime/Core/UI` | Panel/PanelManager/LoadPanelRequest 与原窗口、输入、根、Tips；Adaptation 为原 UI 适配，Tools 原取色结构保持 |
| `Runtime/Core/Components` | TimerObject、CloseView、Listeners UnityEvent 桥接 |
| `Runtime/Core/Attributes/RoslynAnalyzers`、`Analyzers` | 六个正式 marker 与唯一编译器 DLL，仍在 Core Runtime asmdef 子树 |
| `Runtime/Core/Resources/YLFramework/Core` | 原 Canvas/EventSystem，逻辑加载键不变 |
| `Editor/Core` | DefaultResources、ModuleGeneration、Settings、Extensions、Utilities/Assets/Preview/TreeViews、Windows、Inspectors 与 CoreHelpProvider |

未拆分成熟类或 UI 子系统，未改 Module/View/Panel/资源请求的算法、owner、租约、实例池、场景或取消生命周期。`ProjectName` 语义及 Core 对 XFAB 的现有依赖保持。

首次优先使用 AssetDatabase.MoveAsset，遇到 Windows 目录重命名“拒绝访问”及反复模态弹窗；用户恢复编辑器后，非动态编译入口和 Directory.Move 也复现拒绝访问。只读 ACL、独占文件打开及目录 DELETE 句柄检查均未证明 Analyzer 文件占用，不能将动态 Roslyn 缓存推定为已确认根因。最终在禁止自动刷新/重载及批量编辑保护下，逐文件迁移整个目录内容、原 meta 并删除空源目录；禁止覆盖目标，先迁出原 mvc 再归 Module→MVC，规避 Windows 大小写冲突。先完成生成器/监听器/配置路径同步，再平衡释放全部保护计数并由 Unity 重新导入。

### 8.2 身份与默认资源

- 迁前/迁后均为 165 个文件，其中 61 个 C#（Runtime 46、Editor 15）及 97 份 meta；全部 meta 原始内容/GUID 保持，所有目标存在，无重复目标。仅四份 Core Editor 的路径/Help 文案改变，其余 Core C#、两个 asmdef、资源、Analyzer DLL 和 LICENSE 保持原始内容。
- Runtime/Core 根 GUID `cc6645c11ab57e145b6b167078de0fac`、Editor/Core 根 GUID `d9b09bbe8b78666468f046d49ff546f3` 保持。目录 meta 亦全部归口保留，没有以新 GUID 替换原目录身份。
- 五个相关正式程序集共 420 个公开类型的 AssemblyQualifiedName、基类、公开成员签名、可序列化字段及程序集引用完全一致；61 个脚本的 GUID→类型绑定一致。实际编译 sourceFiles 与 Analyzer 图按迁移映射比较无变化。
- Resources.Load 实际解析 `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem` 到新 Core 资源目录；Canvas GUID `0df4ee5a6aa484a40b5d537a1c23a2c0`、EventSystem GUID `e5f419ca4a6e564439be23e0257ffb3c` 保持，没有旧目录重生或双份默认资源。
- 实际检查 Prefabs、Samples 及默认资源共 6 个 Prefab、40 个脚本组件，均无 Missing Script，Core 组件解析到新 Core 路径及原类型。配置生成根更新为 `Editor/Core/GeneratedAssets`；ProjectSettings 的持久键及 Examples 的生成输出保持。

### 8.3 Analyzer 实际接线

- 唯一正式 DLL 位于 `Runtime/Core/Analyzers`；GUID `471f81c4273ee66429677df44cbd6960`、RoslynAnalyzer 标签、禁用平台、显式引用和 Core asmdef 作用域保持。正式 DLL 原字节保持（SHA256 `6861D339663AAA0235C1A83047F78FB81C11588282068D2DE437CD4B5E09EBF3`）。
- 修复 SDK Fixture 的 marker 源码路径及 build.ps1 的 -Deploy 目标，说明同步至 Attributes 文档。默认构建入口实际成功：PowerShell 7.6.6 / SDK 9.0.312，0 build error/warning，SDK 121 项测试通过；本批没有使用 -Deploy，不以 SDK 新构建产物顶替既有 DLL。
- Unity 实际编译事件中，正式 Core 与独立 Core 消费者各有且仅有 YL0001～YL0007 七项负例诊断；合法探针文件无 YL 误报。Core Editor 注入正确，无关程序集及观察器不注入，无 AD0001 / CS8032 / CS8784 / CS8785 或加载失败。
- 真实 Bee/csc rsp 的 -analyzer 指向新 Core 路径；Core、Core Editor、原有 Core 测试/夹具、UI Acceptance 和 ColorSelector Sample 消费者范围保持。普通 Runtime、Foundation、Editor、Help 及无关程序集未被扩大作用域。清理负例后最新 Core/Core Editor 编译事件均为 0 YL 诊断、0 编译/加载失败。

### 8.4 必要验证与交接

| 本轮实际验证 | 结果 |
|---|---|
| Unity 2022.3.62f3c1 实际导入/编译与身份/编译图比对 | 通过；scriptCompilationFailed=false；无身份、GUID 或图差异 |
| Core Editor 门禁程序集 | 实际通过 101 项，含 CoreEditorIdentity、默认资源工厂/保护、配置路径、模块生成、序列化与 Drawer |
| Core EditMode 门禁程序集 | 实际通过 60 项，含 CoreIdentity、Resources.Load、UI/Tips/取色直接消费者 |
| FrameworkHelpProviderTests | 实际通过 6 项 |
| AudioSourceIndependenceTests（受排除路径变化影响） | 实际通过 3 项，包含 Editor/Windows Development 源码 profile；不是 Player 构建 |
| ExportUnityPackageTests | 真实导出/解包通过；新增 Core 职责目录、默认资源、Editor 与 marker 哨兵，核验递归内容/GUID/meta、唯一 Analyzer、平台及排除项 |
| SDK 构建入口与诊断测试 | 121 项通过；Unity 七项诊断另行实际验证，不以 SDK 文本或测试替代接线 |
| 独立只读复核及差异检查 | 165 文件/97 meta 独立复核保持；无其他可行动问题；git diff --check 通过 |

工具返回的全工程总发现数不计入实际通过数量。验证期间正式 ColorSelector Prefab 出现单一遮挡 mask 序列化副作用；用户确认非用户修改并授权仅恢复该字段后已恢复基线，Prefabs/Samples/原业务目录最终 diff 为空，最终内容已重新真实导出/解包通过。诊断探针、观察器及临时脚本/资产已清理；SDK 标准生成输出在取得合作锁后清理，保留既有 entry.lock。移除全部临时程序集后，CoreIdentity 10 项、CoreEditorIdentity 9 项再次通过，不重复计入上表。未修改 MainModule 资源或真实游戏场景。

Architecture、README、Attributes/UI/Pool/Help 导航、THIRD-PARTY-NOTICES 和 IntegrationPlan 增量同步。保留 `.gitignore`、`.kilo/kilo.jsonc` 及执行期间出现的非本批 MCP/NuGet/Packages/ProjectSettings 环境变化，不覆盖或纳入本批；未访问既有 test-worktree。未执行任何 Commit/Push/Merge/Tag/Reset/Clean 或切换分支，HEAD 保持 `0a39928`。

Core Runtime 行为及依赖未改变，未机械重跑完整 Module/UI/Audio/Resource 或 PlayMode 集，未执行 Player/独立载体验收。复用阶段 1～6 与 7.1 未受影响的历史证据，只作未变契约依据；阶段 8 资源融合、阶段 9 最终独立交付及公开再分发/未验证平台限制仍保留。7.2 满足本批技术完成条件，阶段 7 具备最终技术验收/Git 收口基础，但保持 IN PROGRESS；本轮停止，不启动阶段 8。

## 9. 7.2 后职责结构补充（2026-10-13）

用户另行授权 [结构重整方案 A](xf-yl-7-structure-redesign.md)，7.R1 → 7.R2 → 7.R3 → 7.O1 → 7.R4 已串行实施并通过各批技术门禁，完整结果见该方案 §15。Runtime 归 Foundation/Core/Systems/Utilities，普通 Editor 对应归口，Help/Core/Kernel 编译边界保持；原生 EventListener Inspector 验收后 Sirenix 整树退出。原公开类型、字段、GUID、Resources 键、Analyzer scope 与 Core XFAB/ProjectName 契约未变，未重新执行 7.1/7.2。

本轮实际六根递归导出/解包和 Windows64/Mono 开发 Player 启动/持久 UnityEvent smoke 通过；这些结果不替代阶段8资源融合、阶段9独立载体或公开商业分发许可。Core 7.2 与补充批成果已随阶段 7 最终 Git 收口提交（收口前 HEAD `0a39928e76e36564a4b354e370b0d939727b7aad`）；不自动启动阶段8。
 收口授权；不自动启动阶段8。
