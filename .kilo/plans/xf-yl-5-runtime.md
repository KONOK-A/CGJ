# XF × YL 阶段 5：运行时外围融合总体研究与方案

**状态：APPROVED / 阶段 5 技术 COMPLETE，待最终 Git 集成收口；5.1～5.4 已由用户确认技术验收及 Git 收口（5.4 `a4399a6`），5.5 综合门禁及技术文档验收已由用户确认。** 总体方案及 D1～D3 保持不变，路线为 5.1 → 5.2 → 5.3 → 5.3.5 → 5.4 → 5.5；阶段 6 尚未启动。本文仍是阶段 5 唯一总体方案，不另建逐类实施脚本，批次实施授权不包含 Git 写操作。

**目标：**收口 08 HTTP、13 AsyncOperation、15 Tools、16 Extensions、17 Attributes 的唯一职责、有效能力及依赖方向，保持阶段 1～4 的领域所有权、业务行为和存续资源身份。

**架构：**复用现有 Foundation、Core、Runtime 与 Editor 程序集。Foundation 仅承接实际共用的中立闭包；HTTP 和独立工具归 Runtime；Module/View/Panel 与 Audio 继续各自拥有请求及资源责任，不建设公共任务系统。

**技术基线：**Unity `2022.3.62f3c1`，既有 UnityWebRequest、UGUI、Json.NET 与自有 Foundation；不升级工具链或包。

## 1. 基线、证据与范围

第 1～3 节保留总体研究轮次的实施前证据及静态结论；其中“当前/本轮/未执行”均指该研究基线，不覆盖后续实施结果。5.1、5.2 的实际基线、实施与验证见对应批次记录。

- 2026-10-12 实际核验：分支 `integration/xf-yl`，HEAD `6e5b113`，研究开始时工作区干净。`milestone/4-audio` 指向 `0a0e74f`；两提交间只有 `IntegrationPlan.md` 的状态修正，产品源码基线一致，不回退 HEAD。
- 阶段 2 各里程碑、阶段 3 `7e44d9f` / `milestone/3-ui`、阶段 4 `0a0e74f` / `milestone/4-audio` 已由 Git 历史及用户确认核对。蓝图首页、第 6 节与 Audio 编号 `06` 已在 `6e5b113` 修正，本轮不重复编辑；专项方案中的旧停止文字保留为历史，不覆盖较新的完成状态。
- 已读 `AGENTS.md`、[长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)第 2、3 节、[Architecture](../../Assets/YLFramework/Documentation/Architecture.md)，以及 1.1、1.3、2、2.3、3、4 的必要历史约定和实施记录；关键判断以当前源码、消费者和 asmdef 为准。
- 已核对 manifest、锁文件及缓存包：XFGameFramework `1.18.8` / `2182708aecc596f30739fa8634fe2b9b6946ddd7`；XFABManager `1.24.0` / `c35dc4058ecd831409199b43cb328024202d4557`；XFFSM `1.5.4` / 锁定 revision `b06333c2cdc801fcc401f14c252853bf099aa1b7`。XF、XFAB 的 package fingerprint 与锁文件相符。
- 既有环境差异仍在：URP 声明/锁定 `16.0.6`、缓存源码 `14.0.12`；UGUI 声明/锁定 `2.0.0`、缓存源码 `1.0.0`；Cinemachine 声明 `3.0.1`、锁定/缓存 `3.1.6`，Timeline 声明 `1.8.7`、锁定/缓存 `1.8.12`。本轮不调用包解析、不修配置，也不把缓存核对描述为本轮实时 Package Manager 注册结果。
- 本轮只有只读源码研究、Git 查询、Unity 2022.3 官方资料查证及本文写入；没有 Unity 刷新、编译、EditMode/PlayMode、Player 构建或运行。下文缺陷均为静态证据或待复现风险，阶段 1～4 的历史通过不等于阶段 5 已验证。

路径简称：`Y` = `Assets/YLFramework/Runtime/`；`C` = `Y/XFMain/`；`E` = `Assets/YLFramework/Editor/`；`T` = `Assets/YLFramework/Tests/`；`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`；`AB` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`。行号对应研究基线，实施时以符号及最新源码为准。

**硬边界：**原 XF/XFAB/XFFSM Package 只读；原游戏、资源、场景、ProjectSettings、Packages 和 Bundle 不迁移。不得重构整个 Resource/XFAB、UI、Audio、Action 或公共 Root。18 Editor 整体整合归阶段 6；全框架命名/独立承载归阶段 7；其余 14 个生态系统另行规划。本轮唯一新增文件为本文。

## 2. 实际结构与能力比较

### 2.1 08 HTTP

| 项目 | XF 与自有承接 | 现有 YL 其他路径及判断 |
|---|---|---|
| 入口与请求 | `XF/Runtime/Manager/HttpManager.cs`、`AsyncOperation/SendWebRequest.cs`；自有同名文件在 `C`，属 Core，近乎原样承接 | 尚无第二套同职责 YL HTTP 门面；本阶段是融合收口，不是从零造网络层 |
| 能力 | GET 参数、POST Form/JSON 字符串或字典、PUT/DELETE JSON、headers；Text/Data、yield 等待、完成回调、继承 Abort；默认 timeout=5 秒 | 能力适合通用轻量 HTTP；无认证、重试、下载管理等，不新增这些系统 |
| 执行与依赖 | UnityWebRequest + Json.NET + Foundation CoroutineStarter/CustomAsyncOperation；HTTP 本身无 Module、View 或 XFAB 类型依赖 | 位于 Core 不等于属于 MVC；迁入 Runtime 可解除使用 HTTP 对 Core/XFAB 的非必要依赖 |
| 消费者与覆盖 | 当前检索的 Assets 与相关原包源码未找到外部功能调用，只有定义及 `T/Core/EditMode/CoreIdentityTests.cs:75-85` 的身份断言 | 无调用不等于无通用价值，保留并验证。原生态/Resource 自己使用原生 UnityWebRequest，不强改这些路径 |

`Y/Resource/Implementations/AssetBundleSupport/Res/AssetBundleRes.cs` 的 WebGL Bundle 请求是资源生产者，不是应迁入 HTTP 的重复服务；Audio 命名取得也不是 HTTP 消费者。

### 2.2 13 AsyncOperation

| 层次 | 已有能力与来源 | 阶段 5 判断 |
|---|---|---|
| Foundation 最小等待/通知 | `Y/Shared/CustomAsyncOperation.cs` 承接 XFAB：isDone/progress/error、yield、完成/迟到回调、静默完成及默认逻辑 Abort | 已有唯一自有基类，不新增 Task/Promise/scope/请求调度框架；不误称其已被阶段 1.3 重写 |
| 执行支撑 | `Y/Shared/CoroutineStarter.cs` 源自 XFAB，阶段 2.3 已加固原 owner、会话、安全 Stop 和退出 gate；ApplicationTool 为历史运行态查询 | 已完成成果，不再次迁入或替换宿主。`ApplicationTool.isPlaying` 不是可靠的新执行准入门禁 |
| Core 领域请求 | `StartUpModuleRequest`、`LoadViewRequest<T>`、`LoadPanelRequest<T>` 在 `C/AsyncOperation/`，阶段 1～3 已形成领域事务、稳定终态与退出协议 | Module 启动、View pending 与 Panel 转接保持 Core；不下沉其 owner/Status/事务到 Foundation |
| HTTP 请求 | `SendWebRequest` 仍是早期承接实现 | 真正未完成的是 HTTP 自己的取消、终态和原生资源责任 |
| 借观察与其他领域 | Foundation `CoroutineObservation` / TimerWait 是借用 producer 的观察；Audio `AudioPlayRequest` 是独立播放请求；Resource ResLoader 有自己的 Retain/Release | 不相互替换，也不将 Audio 请求改为继承公共异步基类 |

消费者证据：`C/Manager/ModuleManager.cs` 的启动；`ViewManager.cs:393-409`、`PanelManager.cs:187-210`、`UI/WindowCatalog.cs:35-51`；`Y/Audio/AudioPlayRequest.cs:11-13,138-153`；`Y/Shared/CoroutineStarter.cs:8-14,32-72`。

**历史基类契约不能猜测：**`T/Foundation/EditMode/CustomAsyncOperationTests.cs:79-90,139-195` 明确固定重复完成可更新 error、完成后 Abort 可改 error、重复 Abort 可重复调用 hook、通知异常在状态提交后传播并中断后续通知。它不是稳定领域终态的权威。推荐本阶段保留这些基础历史语义并明确文档，HTTP 用自己的终态守卫；View/Panel 已有守卫及异常报告，不将它们改成新的分发语义。若另要修订基类契约，必须提出明确收益、同步契约测试并单独确认，不能以 HTTP 修复静默改变。

### 2.3 15 Tools

| 家族 | 双方能力、消费者与证据 | 推荐取舍 |
|---|---|---|
| 类型名查找 | XF 的 `AssemblyTools` 已承接到 `C/Tools/AssemblyTools.cs`；另有 `Y/Tools/AssemblyTool/AssemblyTool.cs`。前者被 Module/ModuleManager 和 Core Editor 使用，后者当前无功能消费者 | 唯一按完整名查找机制进入 Foundation，采用 YL 的清晰契约/成功缓存，保留跨业务程序集发现能力；不建通用反射平台 |
| 字节/进度显示 | `C/Tools/StringTools.cs` 源自 XF，FormatByte / FormatProgress；当前未找到功能调用 | 有独立通用价值，保留有限格式化能力；不因无调用删除，也不扩展成格式库 |
| 旧文件日志 | `C/Tools/LogUtil.cs` 源自已过时 XF 工具，含全局 Exception 订阅、后台队列写盘、三天文件清理；真实消费者是 Core 共存测试 | 按 D2 在 5.4 退役这个有线程/退出风险的历史日志权威，不保留转发原 LoggerSystem 的壳；文件日志功能需求保留，后续由 LoggerSystem 专题正式融合。原 XF Logger、原游戏日志及现有日志文件不受影响 |
| JSON 树 | `Y/Tools/Json/JSONObject*`（Matt Schoen MIT），解析/构造 JSON 树，与 Json.NET 的 typed serialization 用途不同 | 保留 YL 独立价值，不为统一 HTTP JSON 序列化而替换整套 JSONObject/Save |
| Table/Index | `Y/Tools/Table/`；`Resource/Framework/ResTable.cs:20-22` 与 `AssetDataTable.cs:15-18` 实际使用 | 保留现有表/索引，不与 SerializedDictionary 或 MVC Dictionary 强行合并 |
| Grid/FSM | `Y/Tools/Grid/NormalGrid.cs`、`DynamicGrid.cs`：固定/稀疏二维容器；`Tools/FSM/`：轻量状态切换及池化支撑 | 保留通用独立能力；不在本阶段决定其替代 XFFSM，也不进行两个 FSM 的融合 |
| Zip/GZip | `Y/Tools/ZipTool/` 独立 `YLFramework.Utilities.ZipTool`，内置 SharpZipLib；Resource 的 ZipFileHelper/资源数据和导出测试使用 | 保留独立无 UnityEngine 依赖的边界及许可，不重复吸收 XFAB Zip/下载工具 |
| 已验收领域支撑 | Foundation ApplicationTool/CoroutineStarter、LoadViewArgs、Core 适配与 Listener/CloseView 组件 | 区分归属：参数/适配不重做；组件的过滤、动画事件及挂载用途仍有通用价值，不因暂未调用或 Obsolete 就删除 |

主要重复闭包：`C/Module/Module.cs:871,885,958`、`ModuleManager.cs:494`、`E/XFMain/Extensions/SerializedPropertyExtensions.cs:193,204,217` 都使用 Core AssemblyTools；两份工具均仅扫描已加载程序集和缓存 Type，不需要 Core 领域类型。

XF 主框架 Tools 与 XFAB Tools 不是同一范围。AB 的 File/Encrypt/AssetBundleTools/XFABTools/DefaultServerFilePath/XFTask 等服务资源更新与构建，不在本阶段整组承接。既有三个 Foundation 支撑的 XFAB 来源不意味着阶段 5 获得整个 XFAB Tools 的授权。

### 2.4 16 Extensions

现有两组：`C/Extensions/` 的 16 个承接类属 Core、namespace `YLFramework.Core.Extensions`；`Y/Tools/Extensions/{System,Unity,Math,Utilities}/` 属 Runtime、namespace `YLFramework.Utilities.Extensions`。原 XF 的 SerializedDictionary 已在阶段 2.5 统一到 Foundation，不再计为待融合扩展。

| 能力家族 | 实际对照 | 保留与融合方向 |
|---|---|---|
| 对象/组件/层级 | YL 链式 Show/Hide/Parent/InstantiateWithParent/GetOrAddComponent/安全销毁；XF 递归 SetLayer 与 Unity IsDestroy | 保留真实 Resource/Singleton/事件适配消费者的语义，吸收递归设置/存活判断用途；同职责只留一个算法，不制造两个扩展候选 |
| LayerMask | YL IsInLayerMask；XF Set/Contains、Physics/Physics2D 碰撞关系读取 | 统一掩码机制；Core Collision/Trigger Listener 仍需 Contains，所以最小共享闭包应在 Foundation，不以删除消费者规避依赖 |
| 数学/随机 | YL 标量/方向/距离/分量、RandomUtility；XF Vector/Color/Quaternion 反插值、旋转 | 互补为主，概率判断存在两套离散算法；统一明确契约，不机械逐方法保留来源别名 |
| Ray/Bounds/Rigidbody/Animator/Scene/Time/Type | XF 的距离/包含、抛物线、动画同步、根对象查找、时钟选择及委托显示名 | 有通用用途，保留有效能力，但几何/运动必须先补正确性门禁，不能将承接视为算法可靠 |
| System/IO/Reflection | YL 集合遍历/合并、字符串解析/文件工具、单类型反射和特性读取 | 保留独立用途；live enumeration、快照、Culture、私有方法查找各有契约，不强行全部快照或替换为新反射服务 |
| UGUI/相机 | YL Graphic/RectTransform/Camera capture，XF EventTrigger 注册/移除 | GUI/UGUI 扩展留 Runtime，不为一个掩码消费者把整个工具集及 UGUI 下沉 Foundation；不接管已验收 UI 输入策略 |

真实消费者：`Y/Resource/GameObjectLoader.cs`、`ResLoaderOnDestroyRecycler.cs`、`Foundation/Events/Unity/EventListenerExtension.cs`、`Foundation/Events/Lifecycle/UnRegisterExtension.cs`、`Foundation/Singleton/`；`E/Resource/BuildScript.cs` 等用 IO/字符串 helper。这些 `Runtime/Foundation/` 路径中的代码不少仍属整体 Runtime，不能按目录名推定其位于 Foundation 程序集。

`T/EditMode/Audio/AudioSourceIndependenceTests.cs:31-62` 固定 Audio 的真实自有源码闭包，包含 Component/GameObject/Object/Vector/LayerMask 文件；移动公共 helper 后必须同步闭包，不可改成引用预编译实现 DLL 来放宽门禁。

原游戏 SnakeBase.IsDestroy、Gaming.FindRootGameObject、火箭 Physics2DSetting 使用原 XF 包；相关 CharacterController2D/Overlap/MoveablePlatform 也使用原包。本阶段只据这些用途评估通用价值，不迁移消费者或修改物理设置。

### 2.5 17 Attributes

| 角色 | 实际实现与消费者 | 推荐边界 |
|---|---|---|
| 展示元数据 | `C/Attribute/CustomClassNameGUI.cs`、`CustomPropertyGUI.cs`；另有 `Y/Tools/Attributes/` 同名重写版，FullName 不同 | 收敛为一组 Foundation 中立元数据。融合 XF 的刷新意图与 YL AttributeUsage/只读元数据；不按来源目录归 Core |
| 条件编译差异 | Core CustomClassNameGUI 整个类型仅 Editor 存在；Core CustomPropertyGUI 的构造、刷新字段与绘制方法仅 Editor 存在；YL 版本有 Runtime 类型但缺刷新契约 | 类型/构造/元数据在 Player 可编译，只有 SerializedProperty 绘制方法留 `UNITY_EDITOR`；派生类无需为构造签名维护两套声明 |
| DisableEdit | Core `PropertyAttribute`，绑定 `E/XFMain/CustomPropertyDrawer/DisableEditDrawer.cs:9` | 保留唯一定义与必要原生 Drawer，迁元数据时同步绑定、Editor 引用及绘制验证；不新建万能 AttributeProcessor |
| 分析器标记 | `C/Attribute/RoslynAnalyzers/` 六个标记，被 View/Model/EventManager/LoadViewArgs 实际使用 | 保留 Core 的声明意图；YL 未承接适配过的自有 Analyzer。原包 Analyzer 仍在工程，本轮未证明其对 YL 的覆盖，不声称标记已受检或完全不会生效；适配归阶段 6 |
| 领域特性 | IOC Inject、MonoSingletonPath 等现有类型 | 职责不同、消费者真实，不与展示特性合并，不借阶段 5 重构 IOC/Singleton |

仓库当前未找到两组展示特性的功能标注，现有连接主要为 Drawer 和身份测试。`CustomPropertyGUI.reloadTreeViewOnValueChange` 在原 Skill/Buff/Mission/ScriptableObject Inspector 有实际用途（例如 MissionConfigDataInspector 的读取）；保留其通用 authoring 价值，但不为原生态建立 YL 兼容桥。

Core Editor 的 SerializedProperty/Object/ScriptableObject/Handles 扩展整体归阶段 6。本阶段只允许随类型查找/特性迁移更新直接调用或必要 Drawer，不提前融合整个 Editor 工具族。

## 3. 关键问题与技术取舍

以下是可靠源码问题与契约差异，不是本轮运行复现；实施批次先建立回归再修正。

| 证据 | 影响与推荐处理 |
|---|---|
| `C/Manager/HttpManager.cs:101-116` 直接拼 key/value，并总追加 `?` | 保留 URI 原 query/fragment，编码参数键和值，明确空值和格式化规则；不能把所有中文/空格输入一概写成必然故障，但 `&`/`#` 等会改变参数含义 |
| HttpManager `175-185,208-218,238-245` 先创建 form/put 请求再换 handler，JSON 默认类型未明确 | JSON 与 Form 分别构建适当 method/body/content-type，只创建由本请求负责的 handler；调用方显式 headers 可覆盖。PostWwwForm 默认 form content-type 有官方证据，最终发包仍须回环验证 |
| `SendWebRequest.cs:28-42` 用户前/后钩子及 Completed 在 Dispose 之前，无可靠清理保护 | 用户异常、发送失败、完成通知抛错不能跳过原生释放；响应快照与必要收尾不由通知负责。已创建请求若宿主拒绝启动，也必须终结和释放 |
| `SendWebRequest` 没有 Abort override；基类 Abort 先逻辑完成，原协程仍写结果/error | HTTP 自有稳定终态及独占 transport，取消可对自己的 UnityWebRequest Abort/Dispose；迟到回调不可改状态，不照搬为共享资源的取消策略 |
| HttpManager `70-87` 的静态钩子为只读 Delegate，Instance internal 且构造 private；外部不能正常订阅。耗时用 `Time.time` | 保留前置配置/后置观察用途，提供真正可订阅且可解除的窄接点；耗时用真实非 scaled 时钟，不新增中间件链 |
| Core/YL 两份 AssemblyTool 均缓存首个 FullName 匹配；Core 用 ContainsKey/Remove/Add | 合为唯一小工具，成功缓存、失败不永久缓存、晚加载可见，声明线程边界与同名歧义处理。按完整名跨程序集发现，不限定 Core；不改 Module 实例/注册机制 |
| `C/Tools/StringTools.cs:14-35` 定义 TB 阈值却在 >=1 TB 时返回空字符串 | 保留格式化用途并补足有限单位/边界；人类显示与机器 Culture 解析分开，不全局改所有字符串规则 |
| `C/Tools/LogUtil.cs:223-237,297-340` 主线程/Task 共享 Queue/Writer，后台访问创建式 Instance，异常吞掉；reset 仅置空实例 | 当前共存测试证明隔离写盘，不证明线程/退出可靠。推荐退役，避免为历史工具重新建设第三套日志生命周期；不清历史文件、不改原 Logger |
| LayerMask `Set/Contains` 未校验 -1/越界（`C/Extensions/LayerMaskExtensions.cs:16-64`） | 未定义层名可被移位映射到 bit31。统一校验，不写错位；PhysicsSetting 只读取当前碰撞规则，保留有效层语义，禁止改 ProjectSettings/全局 collision matrix |
| Rigidbody 抛物线 `14-36,64-83` 假设正的有效重力和可达目标，退化输入可除零 | 定义可达/不可达、零重力/速度及二维 gravityScale 边界，拒绝或 Try 返回失败，不给刚体写 NaN/Infinity；不升级为运动控制系统 |
| Ray `20-38,67-105` 有符号距离、仅识别同向平行；点到“射线”距离允许反向延长线。Quaternion `17-34` 不统一 current 的符号 | 距离应非负并覆盖反向平行；明确无限直线与射线半轴的差别，正式 API 不伪装另一种保证。Quaternion 的 q/-q 等价性要成立；几何能力以正确契约保留，不照搬公式 |
| Type display `19-25` 对 FullName=null 调 Contains；集合 Dictionary.ForEach 为 live enumerate，IEnumerable.ForEach 物化 | 修显示名的开放泛型/退化边界。live enumeration 本身不是缺陷，不为“统一”给所有 ForEach 加分配；保留清楚的修改限制和异常语义 |

Unity 2022.3 官方依据：[PostWwwForm](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequest.PostWwwForm.html)、[Abort](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequest.Abort.html)、[Dispose](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequest.Dispose.html)。当前工具未提供 Context7 查询接口，故定向读取对应版本官方文档。Abort 表示尽快停止通信，不保证回滚服务器已执行的副作用。

## 4. 推荐统一架构与兼容

### 4.1 归属与依赖

| 归属 | 本阶段正式职责 | 不应进入的内容 |
|---|---|---|
| Foundation（`Y/Shared/`） | 现有异步等待/通知、CoroutineStarter/gate；唯一 FullName 查找；Core/Runtime 共用的 LayerMask 等最小 helper；展示/只读编辑特性元数据 | HTTP/JSON 服务、UGUI 大集合、资源生产者、Core 请求 owner、Logger、全体 Tools |
| Runtime（`Y/`） | 独立 HTTP 域，推荐 namespace `YLFramework.Services.Http`；保留的独立 Tools 与多数 System/Math/Unity/UGUI 扩展 | Module/View 管理、原包适配的默认注册、第二套公共调度/取消内核 |
| Core（`C/`） | Module/View/Panel 请求及业务声明；现有 Listener/CloseView/适配组件 | HTTP 权威、重复类型查找、无 Core 语义的独立扩展全集 |
| Editor | 唯一元数据对应的必要 Drawer与直接反射调用适配 | Runtime→Editor asmdef 依赖；阶段 6 整体 Inspector/分析器/菜单重建 |

维持 `Core → Foundation + 临时 XFAB + UGUI`、`Runtime → Foundation + ZipTool + UGUI`，二者互不引用。Foundation 不新增 XFAB、Newtonsoft、Root 或 Editor 产品程序集依赖；UGUI 专用扩展留 Runtime。Core Editor 若直接使用迁后的工具/元数据，按实际调用显式引用 Foundation。不新建 HTTP/Tools/Attributes 三套产品程序集。

namespace 可以跨正确的程序集边界组织同一逻辑家族，不为了目录整齐搬全部辅助类。迁移本阶段重复类型时统一其正式名称/入口；旧 Pool/IOC 等不相关命名归阶段 7，不机械全局替换 `YLFramework.Core.*`。

### 4.2 HTTP 与异步所有权

- Foundation 只提供等待与通知，不负责业务取消。Core 启动仍拒绝 Abort；View/Panel 仍由 Module owner/会话、pending、参数借读与初始化回滚决定终态，Panel 仅转接 View。Module.Close 不自动清全局 HTTP、Timer、Action 或 Audio。
- HTTP 每笔请求独占其 UnityWebRequest/handler，主线程提交，生产入口内部化；在途登记先于 CoroutineStarter.Start 的同步首步，前置钩子内取消/退出及启动返回 null 都按原请求收尾。结果包含稳定状态、错误、响应码和独立 Text/Data 快照，必要时保留原异常；协议失败也可保留响应供诊断。保留 yield、完成/迟到订阅用途，不新增通用 await 桥接。
- 请求构造/发送/前置配置失败、正常结束、取消、宿主拒绝和退出都有一次收尾。前置配置异常可使本请求 Failed；后置观察/完成通知异常不反向改已提交结果，且不能跳过释放。后置钩子中的原生请求只可同步借读，不允许持有后继续操作；完成观察使用结果快照。
- 自有 HTTP Abort 幂等、终态后无副作用，先建立终态/撤销登记再停止和释放自己的 transport；结果不会被晚到协程覆盖。完成回调顺序与 late callback 用途保留，不把物理取消写成服务器事务撤销。
- HTTP 域只登记自己的在途 transport，复用 Foundation 会话/gate；真实 Application.quitting / Editor 退出同步收尾，旧会话通知不能创建新宿主。`CoroutineStarter.Stop` 和迭代器 finally 均不能独立证明业务资源已释放；不得新增跨领域请求表。
- Audio 的 pending/Voice/Channel、资源租约、自然完成及 Shutdown 行为不变。Audio/Timer 的借观察和 View 对 XFAB 的等待都不获得物理取消权限；共享 producer 仍归其后端。XFAB 永久挂起、实例半登记及物理 Bundle 释放留资源专题。

### 4.3 能力保留与迁移原则

- Extension 收敛按语义而不是方法名：层递归与单层设置、Unity fake-null 与普通 managed null、不同概率离散化、直线与射线都不能靠同名替换。实际消费者所用的现有 YL helper 保持行为；相同机制共用一个实现，确有不同契约的 API 明确区分。
- 保留 JSONObject/Grid/Table/FSM/Zip 及有效 XF 独有扩展。未变化工具不为了阶段完整感重写或堆测试；发现直接影响本阶段的确定缺陷再以最小回归修正。不承接 XFAB 文件/下载/加密大全，也不决定两个 FSM 的最终融合。
- Attributes 推荐唯一 namespace `YLFramework.Foundation.Attributes`：合入 AttributeUsage、类名元数据及刷新意图，构造/元数据不依赖 Editor，绘制方法条件编译。DisableEdit 保留正确高度、GUI 状态恢复及原生绘制；必要 Drawer 可归普通 Editor，不新增通用绘制发现框架。分析器标记保持 Core 的领域声明，不声称获得原分析器等效覆盖。
- 迁移既有受管文件保留自身 `.meta`/GUID；新独立资源由 Unity 生成身份。静态 helper/Attribute 通常不直接序列化，但要检查查实的派生、typeof、FullName 字符串与 Drawer/test 接线，不能仅凭“不是 MonoBehaviour”免检。Listener/适配等组件不改类型、程序集、字段或 GUID。
- 原游戏调用仍走原 XF 扩展及既有 YL Audio；不改 asmdef、UnityEvent、Prefab/Scene、偏好键或 Bundle。新 YL 无已确认外部消费者的旧外围 API 不建永久 shim；若发现真实外部持久化样本，停止对应退出并确认最小迁移。
- 旧机制退出必须在实际消费者迁移及验证后完成：两份反射查找、两组展示特性、Core 的非领域扩展与 HTTP 最终不留双权威/转发壳。LogUtil 的退役已由 D2 确认（5.4 按既定条件执行，文件日志需求留 LoggerSystem 专题），原日志不删除；其历史测试在退役后改为旧类型/自动副作用退出的共存检查，而不是继续伪造写盘通过。
- XF/XFAB MIT、QFramework、JSONObject、SharpZipLib 的署名和完整许可保持；迁出 XFMain/Shared 的来源范围增量维护现有 `THIRD-PARTY-NOTICES.md`。不因为源包仍在工程而漏随附许可，不在本阶段认证整个 Plugins 分发。

## 5. 实施批次与退出门禁

**批准路线：5.1 → 5.2 → 5.3 → 5.3.5 → 5.4 → 5.5。** 5.3.5 是用户在 5.3 验收及 Git 收口后正式授权新增的成熟 Tools 整合批次；其余批准边界不变。5.2 与 5.3 没有互相的硬依赖，默认顺序是先处理有原生资源责任的 HTTP，再修纯工具契约；不按五个功能各立一批，也不为每个类拆批。每批依赖前批验收和独立实施授权，批准总体方案不自动授权后续批次。

### 5.1 中立支撑归口

- **主要轴：**公共源码归属与唯一权威。合并类型名查找和最小 LayerMask 闭包，归 Foundation；展示特性唯一化并同步必要 Drawer。迁 Core/Runtime/Editor 直接消费者，保护领域组件身份；暂不改 HTTP、几何算法或日志生命周期。
- **前置/退出：**接受第 6 节 D1；核对类型/反射字符串及 Editor asmdef。完成后新 YL 无两套相同类型查找和展示元数据，Core/Runtime 只能向中立闭包依赖；迁移失败不退出原自有类型。
- **验收：**实际 Unity 编译；FoundationBoundary、CoreIdentity、CoreEditorIdentity；定向 Module 字符串启动/Controller 查找及生成器/SerializedProperty 直接回归。补查找失败后可见晚加载、命名歧义及特性跨 Editor/非 Editor 编译；真实 SerializedObject 验证 DisableEdit 不改值、高度/GUI 状态正确。若移动 LayerMaskUtility，同步并运行 AudioSourceIndependence 的两个源码闭包 profile。

#### 5.1 实施与验证记录（2026-10-12）

**实际基线：**`fusion/5-runtime`，HEAD `e222accdc29103d2e1e77bd70a702071fe9167fd`，开始时工作区干净。已核对全局/项目 AGENTS、Unity `2022.3.62f3c1`、manifest 与本方案第 4 节/5.1；本次没有切分支、Commit、Push、Merge、Tag、Reset 或 Clean。

**实际成果及取舍：**

- 唯一类型查找为 `Y/Shared/AssemblyTool.cs` / `YLFramework.Foundation.AssemblyTool`，以既有 YL 清晰契约为基底，成功结果使用 Ordinal 字典缓存，失败不缓存。保留已加载程序集顺序的首匹配及成功缓存稳定性，不把同名类型改为异常；不自行裁剪/补 namespace/做短名扫描，不提供程序集限定名解析或加载器，底层名称解析交由 `Assembly.GetType`。声明 Unity 主线程/单线程边界，没有新增并发反射平台。
- Core `AssemblyTools` 和旧 `Utilities.AssemblyTool.AssemblyTool` 不留双实现或转发壳。Module 的三个字符串 Controller 入口、ModuleManager 字符串启动，以及 Core Editor 的三个 Clipboard 查找已迁到 Foundation；注册、owner 与生命周期未改。
- `Y/Shared/LayerMaskUtility.cs` / `YLFramework.Foundation.LayerMaskUtility` 承接唯一成员判断算法。Core 的两个 Contains 重复实现移除，Listener 使用 Foundation 扩展；Runtime 的 GameObject/Component 包装签名与 Unity null 语义保持。仅为本批迁移兼容保留两种入口：Contains 使用旧 Core 的位移取模语义（未定义层名仍映射 bit31），IsInLayerMask 严格校验 0..31；两者共用同一位判断。Set / PhysicsSetting / Physics2DSetting 仍暂留 Core，完整边界修复归 5.3、其余归属收口归 5.4，不下沉整组工具。
- 唯一展示特性为 `Y/Shared/Attributes/` / `YLFramework.Foundation.Attributes`：CustomClassNameGUI 保留 YL 的 AttributeUsage/只读名称元数据，CustomPropertyGUI 合入 XF 的 public 刷新字段、无参/布尔构造，类型/构造/配置在 Player 可编译，只有 OnPropertyGUI 条件编译为 Editor。旧 Core 与 Tools 的同职责展示定义退出；DisableEdit 保留有效 PropertyAttribute/AttributeUsage。
- 必要 DisableEditDrawer 保留在 Core Editor 原文件，以 Foundation 特性重新绑定；Core Editor asmdef 显式增加 Foundation 引用。Drawer 使用 DisabledScope 确保正常/异常状态恢复，原生 PropertyField 与高度计算均包含展开子属性，不应用/保存 SerializedObject；未建设特性扫描器、Editor 产品平台或 Analyzer。
- 五个迁移脚本的 `.meta` 内容与 GUID 保持：AssemblyTool `0d642493baff4da3a1658e765259bdda`、LayerMaskUtility `2df0ea77b7acdc64ea603270e62697f7`、CustomClassNameGUI `c7fc0b53a07a4c4ba926bb6236296c73`、CustomPropertyGUI `d3749dc9c74244c488c5590c4589b1f0`、DisableEdit `3a18a3569256cf14aa3e03ac64826127`。Unity AssetDatabase 实际解析到上述新路径；新增测试及 Attributes 目录身份由 Unity 导入生成。Listener/Drawer 原 GUID、组件/程序集/序列化字段均未改。迁移/退出 GUID 在 Assets 定向搜索未发现资产绑定，旧 API 字符串仅剩退出守卫。
- Foundation asmdef 未增加产品程序集依赖；Core 与整体 Runtime 保持互不引用，Core Editor-only 边界保留。仅增量更新现有 Architecture、模块说明/导航和 THIRD-PARTY-NOTICES；完整许可仍随附。未改原 Package、游戏、Scene、Prefab、Bundle、Packages 或 ProjectSettings。

**分层证据（本轮实际执行）：**

| 验证层 | 结果与范围 |
|---|---|
| 静态引用/身份检查 | 旧查找与展示类型只剩测试退出守卫；迁移 GUID 无资产引用且在新路径唯一；新增源码/目录均有 Unity 生成或保留的 meta；`git diff --check` 通过。 |
| Unity 实际编译 | 已通过当前打开工程的 AssetDatabase 刷新/重编译；Unity `EditorUtility.scriptCompilationFailed == false`，未用 IDE 或 dotnet 构建替代。 |
| 定向 EditMode | **70 项通过，0 失败/跳过**：FoundationBoundary 6、CoreIdentity 9、CoreEditorIdentity 8、CoreModuleGeneration 33、AssemblyTool 3、Attributes 3、DisableEditDrawer 3、NeutralLayerMaskConsumer 2、AudioSourceIndependence 3。统计使用实际 PassedTests/单项 Results，不将工具的全工程发现总数作为执行数量。 |
| 类型查找回归 | 完整名/大小写/嵌套/开放泛型、成功缓存、失败不缓存及之后的动态程序集晚加载、同名首匹配/缓存稳定性均通过。归属门禁先实际失败，迁移后通过。 |
| Inspector/Editor 直接消费者 | 真实 SerializedObject 的展开高度由迁移前 18（期望 38）失败转为通过；已注册 DisableEdit Drawer 实际禁用子字段，正常及子 Drawer 抛异常时都恢复 enabled/disabled 状态、不改值；Clipboard 复制/可粘贴/粘贴真实调用往返通过并恢复系统剪贴板文本。 |
| 非 Editor 元数据源码 | AttributesTests 用 AssemblyBuilder 直接编译三份正式源码与派生/命名参数探针，EditorAssembly、Windows Development 无 UnityEditor 引用两个 profile 均通过；不借用项目预编译 Foundation 实现，不声称实际 Player 运行。 |
| Audio 源码闭包 | 固定清单已改为真实 `Shared/LayerMaskUtility.cs`；EditorAssembly 与 Windows Development 两个源码闭包及引用白名单门禁通过，不改为引用预编译 Foundation DLL，不替代 Player 构建。 |
| 定向 PlayMode | **4 项逐项取得 Passed，0 用例失败**：CoreModulePlayModeTests 的 TypeStartupConstructsOnlyWhenMissingAndNeverImplicitlyUsesModulePool、SynchronousStartupPublishesOneRequestBeforeReentryAndSingletonNeedsNoBaseCall、ControllersAreLocalAndExplicitBorrowingPreservesOwnershipAndExit、CreateControllerOverloadsCreateLocallyWithoutBorrowingOtherModules。覆盖字符串 Module 启动和 Controller 查询/加载/创建，不跑整个 Core/Audio/UI 全集。 |

**限制、失败分类及停止点：**

- 最初类型查找测试的反射助手误选继承的 GetType，改为直接正式 API 后通过；LayerMask 夹具曾混用同对象 2D/3D Collider 且在 EditMode 使用 SendMessage，修正为分离的真实 Collider 和直接调用实际过滤消费者后通过。未修改业务行为或削弱断言；该 EditMode 回调测试不证明物理引擎自动分发。
- Play 测试退出曾出现原 Logger 的 `LoggerManager` 对象清理错误，以及 MCP 批量连续调用时的域重载瞬断。连接恢复后四个目标用例已分别返回有效通过结果；丢失结果的批量调用不计为通过。没有修改原 Logger/Package、替换 logHandler 或沿用此前临时隔离授权；用例本体通过不证明原 Logger 的退出安全，综合退出门禁仍留 5.5。
- 独立只读代码审查未发现阻断性缺陷。非阻断覆盖限制：Contains 的非法层号/未定义层名取模保留已静态核对等价，本轮没有新增将该缺陷锁为永久契约的测试；按 5.3 的正式修订补完整边界回归。
- 没有实际 Player 构建/运行、阶段综合双 Play、原游戏综合验收或 IL2CPP/AOT 证明；均未提前实施。Core 其余 Tools/Extensions、HTTP 和 LogUtil 仍按后续批准批次收口。

**结论：5.1 唯一中立权威、消费者迁移、身份保护与定向技术门禁满足，具备进入 5.2 的前置基础；等待用户验收及 Git 收口。阶段 5 不标记 COMPLETE，5.2 不自动实施。**

以上为 5.1 实施轮次的历史停止点。5.2 开始前用户已确认 5.1 技术验收及 Git 收口；实际对应提交为 `40f0c5fb5f1d686aa30dd4afd7f173814eb3cb72`。

### 5.2 HTTP 请求生命周期与 AsyncOperation 边界

- **主要轴：**现有 HTTP 的行为正确性，暂留当前类型/程序集身份以便独立比对。修 query/body/headers、钩子可订阅性、耗时、结果快照、稳定终态、取消及全路径释放；补 HTTP 自有退出收尾。AsyncOperation 只收口真实契约与直接消费者证明，不改基类历史多播/Abort 语义。
- **前置/退出：**Foundation 原 owner/gate 可用且 D3 边界确认；不依赖几何工具。完成后 HTTP 不能在 Abort 后改写结果，不能把通知当清理入口，拒绝启动与退出均不留自有 transport；不调用共享后端 Abort。
- **验收：**实际 Unity 编译；新增最小本地回环 HTTP fixture，不依赖公网/第三方服务。覆盖 GET 原 query/fragment、特殊字符和空参数、Form/JSON/PUT/DELETE 的 method/body/content-type、headers、成功/协议错误 body、timeout、取消与重复取消、前/后/完成回调抛错及同步重入、未开始/在途退出、宿主拒绝及迟到结果；用原生请求/handler 的实际释放证据，不仅断言逻辑 isDone。测试服务使用系统分配端口、严格释放，只处理 HTTP 字节，后台不访问 Unity 对象。
- **直接回归：**CustomAsyncOperationTests、CoroutineStarterTests；CoreXfab 中 View/Panel 取消、owner 关闭/重启/late callback 和启动拒绝 Abort；Audio 命名取消/旧会话租约代表性用例。只复用最相关用例，不重跑每个领域全集。

#### 5.2 实施与验证记录（2026-10-12）

**实际基线：**`fusion/5-runtime`，HEAD `40f0c5fb5f1d686aa30dd4afd7f173814eb3cb72`，开始时工作区干净；`git ls-remote origin refs/heads/fusion/5-runtime` 实时返回同一提交，与本地远端跟踪一致。已读项目 AGENTS、批准计划 4.2/5.2/D3、Unity `2022.3.62f3c1` 和 manifest，并定向核对 HTTP、Foundation 与直接消费者；没有重新开展阶段 5 总体研究。

**实际成果及取舍：**

- `HttpManager` 的 GET、POST Form、POST JSON 字典/字符串、PUT、DELETE 签名与 Core namespace/程序集身份保留。GET 合并原 query/fragment、分别编码键值、null 转空值、数字使用 InvariantCulture；无 query 的路径即使以 `&` 结尾也先添加 `?`。Form 明确 UTF-8 URL 编码；JSON 直接创建合适的 method/body/Content-Type，不再先造 Form/PUT 的 Handler 然后覆盖；显式 headers 可覆盖默认类型。要求绝对 http/https URI，JSON 字符串 null 返回 Failed，字典 null 序列化为 JSON null，不增加内容验证、认证、重试或下载系统。
- `SendWebRequest` 仍继承原 Foundation 基类，但 sealed、构造与生产入口内部化；新增 HTTP 自有 Pending/Succeeded/Failed/Canceled/Invalidated、Exception、NativeResult、ResponseCode、ResponseHeaders 与未缩放 ElapsedSeconds。Text/Data/响应头为完成后可读快照，协议错误也保留正文；自定义 Handler 的虚方法先局部读取、每次读取后检查终态/会话，再集中发布，Data 复制以避免 Handler.Dispose 修改其原数组。原始失败异常保留；异常自身 ToString 再抛错时使用安全备用错误文本，不让请求登记逃逸。
- 每笔请求在 CoroutineStarter.Start 的同步首步前已登记在 HTTP 专用 pending 表；构造、配置、发送失败、null 启动句柄、正常完成、协议/网络失败、超时、取消与退出均提交一次终态并撤销登记。复用 Foundation 会话身份/gate/reset，不增加公共任务表或新执行宿主。原生 completed 回调独立于协程等待，实际销毁 CoroutineStarter 宿主后仍完成本请求并释放资源，不创建替代宿主。HTTP 真实订阅 Application.quitting 与 Editor 退出，收尾不依赖迭代器 finally。
- Abort 幂等，终态后不修改结果，只 Abort/Dispose 自己独占的 UnityWebRequest；旧协程及迟到原生通知不能反写。先提交状态、撤销登记和原生通知，再进行借读 post、可靠释放及基类完成通知。同步 pre 取消/退出、post 重入 Abort/创建另一请求、完成回调重入 Abort 都有定向覆盖；不停止 View/Audio/Resource 的共享 producer。
- Handler 所有权为入口接管的初始对象与各配置回调边界附着的对象，逐回调 finally/收尾捕获当前 Handler，包含替换后的旧/新 Download、Upload、Certificate Handler。显式释放接管对象，不受调用者更改 dispose*OnDispose 标志影响。同一回调自行创建又在交付前移除的中间 Handler 归配置者自行释放，HTTP 不承诺观察不可见的赋值；不为此添加 setter 中间件。post 原生对象只同步借读，完成回调读取快照。
- 前/后钩子改为可订阅/解除的静态事件，HTTP 会话 reset 清空订阅；pre 异常使本请求 Failed，post 异常仅报告且继续后续观察，完成通知仍保持 legacy -> registered 及历史多播中断顺序，异常报告不改终态或跳过清理。迟到 AddCompleteEvent 保留立即同步调用及异常向订阅者传播的基类语义。Foundation CustomAsyncOperation/CoroutineStarter、View/Panel、Audio/Resource 源码未改。
- 两个既有产品脚本 GUID 保留：HttpManager `fe1d02983613d0e49b3a622fa7377063`、SendWebRequest `cc8484abb98c20245860b999454a03e7`；CoreIdentity 实际通过。仅新增两个测试脚本，meta 由 Unity 生成；没有移动产品类型、修改 asmdef、原 Package、原游戏、持久资源、ProjectSettings 或 Packages。实际 API/使用变化增量写入既有 Architecture 的 HTTP/AsyncOperation 节，不另建方案或过程报告。

**分层证据（本轮实际执行，按实际 PassedTests/单项 Results 统计）：**

| 验证层 | 结果与范围 |
|---|---|
| Unity 实际编译 | 当前打开的 Unity `2022.3.62f3c1` 经 AssetDatabase 刷新实际编译；最终 `EditorApplication.isCompiling == false`、`EditorUtility.scriptCompilationFailed == false`。没有以 IDE/dotnet 替代。 |
| HTTP EditMode | **4 Passed，0 Failed/Skipped**：非 Play 启动拒绝、未启动 native 与三类 Handler 释放（关闭自动 Dispose 标志）、构造/JSON 参数失败稳定终态、异常格式化失败仍收尾。 |
| 真实 HTTP PlayMode | **20 Passed，0 Failed/Skipped**：系统分配端口 TCP 回环，真实 UnityWebRequest；GET query/fragment/特殊字符/空值/Culture/路径末尾 `&`，Form/JSON/PUT/DELETE method/body/type/headers，200/503 快照，timeScale=0 下真实 1 秒 timeout，独占在途取消/重复取消，pre/post/legacy/registered 异常与重入，未开始与在途退出、失活启动拒绝、实际销毁协程宿主后收尾、迟到通知、Handler getter 取消与 Dispose 数组变更隔离。 |
| 原生释放 | 正常、协议失败、超时、取消、前置失败/同步取消、拒绝启动及退出路径实际检查 UnityWebRequest/DownloadHandler/UploadHandler/CertificateHandler 的原生 `m_Ptr` 从非零到零；替换前后的对象均检查，完成通知时已归零，post 时仍可借读。未用 isDone 代替释放证据，未写原生指针。 |
| Foundation EditMode | **CustomAsyncOperationTests 16 Passed**：历史重复完成更新 error、完成后 Abort 改 error、重复 Abort hook、legacy/registered/hook 顺序、多播抛错中断、静默完成与迟到通知均保持。 |
| Foundation PlayMode | **CoroutineStarterTests 15 Passed**：原 owner、同步首步重入、失活/销毁、会话 reset/迟到消息、退出拒绝新宿主、等待和基类逻辑 Abort 不取消 producer 均保持。 |
| Core 代表性回归 | **CoreIdentityTests 9 EditMode Passed**；**5 PlayMode Passed**：Module 的 SynchronousStartupPublishesOneRequestBeforeReentryAndSingletonNeedsNoBaseCall（含启动拒绝 Abort）；CoreXfab 的 ParallelNamedViewsCancelLateProducerWithoutInstanceOrRecycledArgsLeak、CloseInvalidatesPendingBeforeNotificationAndAllowsSameInstanceRestart、CompletionThrowAndRepeatedAbortDoNotRewriteSuccessAndArgsSelectCurrentModel、PanelWaitingAbortDuplicateClaimAndBorrowedArgsAbortKeepStableOwnership。复用既有 Assets/LOCAL 夹具，没有创建/修改资产。 |
| Audio 代表性回归 | **3 EditMode Passed**：AudioNamedBorrowedTests.LoadingDoesNotReserveSourceAndOneCanceledWaiterCannotStopItsNewBorrowerOrSharedPeer；AudioRequestLifecycleTests.RejectionValidationAndOldSessionReleaseWithoutBuildingHost、OldHostLateQuitCannotCloseReplacementSession。证明借用/共享取消与旧会话责任不受 HTTP 物理取消影响，不声称本批实际音频输出验证。 |
| 静态/收尾 | `git diff --check` 通过；原 GUID 与程序集归属实际核对。最终 Editor 非 Play、HTTP pending=0、FoundationHosts=0、HTTP pre/post 订阅=0。回环每个夹具只关闭并等待自身 listener/socket/Task，后台无 Unity 对象访问。 |

定向通过总数为 **EditMode 32、PlayMode 40，共 72 项**，不是 MCP 输出的全工程发现总数；中间 red/重复 green 不重复计数。

**审查、失败分类与限制：**

- 首个拒绝启动用例实际先失败（isDone=false）再通过。独立只读审查发现 Handler getter 重入导致完成后反写、数组别名、路径末尾 `&` 及异常 ToString 逃逸；补充用例分别实际失败，再修复为通过。复查未发现阻断性实证缺陷；中间 Handler 所有权通过明确交付边界解决，不增加新网络中间件。
- 初次测试导入/完整类名筛选曾返回 Unknown/无测试；确认编译和实际类型后使用短类名或完整 testMethod 得到有效逐项结果，空结果不计通过，也不冒充产品故障。
- Play 测试本体、回环与资源断言均取得有效结果，但测试退出日志仍出现原 XF `LoggerManager` 的 `Some objects were not cleaned up when closing the scene` 历史错误（MCP Console sequence 45405、45409、45410、45420、45421）。未修改原 Logger/Package、替换 logHandler、吞错误或重用旧隔离授权；本批通过不证明原 Logger 的退出安全。
- HTTP gate/OnExecutionEnded/Reset 的未开始与在途退出为受控回调/会话测试；另有真实销毁本夹具 CoroutineStarter 宿主的原生完成与释放验证。没有执行阶段 5.5 的同域连续双 Play、实际 Application 进程退出或 Windows64/Mono Player 综合门禁，不把受控 reset 描述为真实双 Play。移动/WebGL/IL2CPP/AOT、原游戏综合路径与全生态独立性均未验证。

**结论：满足 5.2 本批技术完成条件，HTTP 独立终态、全路径自有资源收尾及 D3 定向兼容证据成立，具备进入 5.3 的基础。等待用户验收和 Git 收口，阶段 5 仍 IN PROGRESS；不自动实施 5.3 或执行 Git 写操作。**

以上为 5.2 实施轮次的历史停止点。5.3 开始前用户已确认 5.2 技术验收及 Git 收口，对应提交为 `5a14250c5766be6c89c7e0e4845d9ab324a6c638`。

### 5.3 工具与扩展的契约修复

- **主要轴：**保留能力的确定正确性。在现有身份下修无效 layer、字节格式化、Ray/Quaternion/抛物线/类型显示的关键缺陷；明确概率、枚举、Culture、Unity fake-null 与相机返回资源责任，不把已写明的限制一概当缺陷。
- **前置/退出：**已选定保留用途与正式语义；先以确定输入复现静态问题。完成后层设置不写错 bit、几何距离非负且退化不产生非法运动状态、q/-q 等价、>=TB 不返回空字节文本；没有测量不宣称整体性能提升。
- **验收：**相关 EditMode 纯契约与最小 PlayMode 原生对象用例；LayerMask 0/31/未定义名、同/反向平行与原点背后、Quaternion 等价/同端点、零重力及不可达、Unity 对象销毁、开放泛型 Type 与格式化阈值。碰撞规则验证只读取或用自己的受控 fixture 并恢复，不更改持久设置。集合 live/snapshot 契约分别验证，不以统一快照扩大高频分配。
- **复用：**实际存在的 CollectionPoolTests、PoolContractTests、ResourcePoolTests、SingletonContractTests/SingletonHostPlayModeTests 按修改闭包选取；未修改 JSONObject/Grid/FSM/Zip 的内部实现时不补机械全量测试，也不宣称它们已获全面验收。

#### 5.3 实施与验证记录（2026-10-12）

**实际基线：**`fusion/5-runtime`，HEAD `5a14250c5766be6c89c7e0e4845d9ab324a6c638`，工作区干净；`git ls-remote origin refs/heads/fusion/5-runtime` 实时返回同一提交，与远端跟踪一致。已读 AGENTS、Unity `2022.3.62f3c1`、manifest 和批准方案第 3/4.3/5.3 节，定向核对工具、两组扩展、Foundation 共享算法及真实消费者，没有重新研究阶段 5 总体路线。

**实际修复与兼容取舍：**

- Foundation `LayerMaskUtility.Contains` 结束 5.1 临时保留的位移取模，统一调用已有严格 0..31 判断；非法编号及未定义层名返回 false。Core `Set`、`PhysicsSetting`、`Physics2DSetting` 在操作前拒绝非法编号，抛 `ArgumentOutOfRangeException` 且不改掩码。bit0/bit31 及有效碰撞规则读取保持原语义，仍只选已命名且可碰撞的层；没有调用碰撞矩阵写 API 或修改持久物理设置。
- Runtime `ComponentExtension.IsInLayerMask` 在访问 gameObject 前使用 Unity fake-null 判断；null/已销毁组件返回 false，避免真实 `MissingReferenceException`。GameObject 包装与 Foundation 算法共用不变；没有把普通 managed null、强制销毁、Gracefully 销毁、单对象/递归层设置强行合并。
- `StringTools.FormatByte` 保留 1024 进制、b/kb/mb/gb 小写后缀及当前 Culture 的 N2 人类显示，补 tb 分支；大于 TB 至最大 long 继续以 tb 显示，不扩展成单位大全。除法使用 double，避免 float 在单位阈值附近过早舍入；小于 1024 的历史有符号 b 显示和 FormatProgress 不变。
- Ray 原 `MiniDistancePoint` 已明确允许负参数，因此旧 `MiniDistance`、两个 `MiniDistancePoint` 与 `Distance(Vector3)` 继续表达无限直线，不偷偷夹到半轴；XML 和现有使用说明明确该边界。距离非负，同向/反向平行返回有限的一组最近点，零方向当原点；双精度叉积分母避免近乎平行时点积消减，距离用独立双精度三重积/残差长度计算，不由 float 最近点重建，以免放大参数舍入误差。无法表示的距离/最近点参数明确拒绝。新增唯一窄入口 `DistanceToRay(Vector3)` 表达点到射线半轴距离，不新增通用几何框架或两个射线半轴的最近点平台。
- `QuaternionExtensions.InverseLerp` 仍为最短角距离比、不 clamp、不验证 current 在同一弧上；对有限非零输入归一化、对齐等价符号，使用双精度 atan2 差长/和长计算保留小角度精度，使所有 q/-q 表示等价，同端点返回 0。零/非有限旋转抛 `ArgumentException`，无法表示为有限 float 的比例抛 `InvalidOperationException`，不静默替换 identity 或引入 Quaternion.Angle 小角度死区。
- 抛物线使用有效恒定重力：3D 尊重 useGravity 和完整 Physics.gravity，2D 尊重 Physics2D.gravity * gravityScale 的方向并忽略目标 Z。按指定 Y 初速度解二次式，稳定求根并取最晚正时间（通常下落段），修正顶点时间和高于顶点仍返回错误速度的旧行为；零重力可直线求解，零初速度向下落仍可达，负 gravityScale 保留实际方向。不可达、无正时间/时间无法确定、所需速度无法表示时抛 `InvalidOperationException`，非有限输入抛 `ArgumentException`；ParabolicMotion 仅在计算成功后赋值，失败保留原生速度。无阻力、无约束的连续解不是原生离散积分精确落点承诺，不改 drag/constraints 或建设运动控制系统。
- `GetDelegateDisplayName` 对 FullName=null 回退 Name，开放和部分构造泛型也使用 GetGenericArguments；基于泛型定义移除元数、保留嵌套类型名，继续使用 Name(ArgName,...) 风格，null Type 明确抛 `ArgumentNullException`。不扩展为委托签名/反射平台。
- 定向检索 Assets 下未发现自有 StringTools、Ray、Quaternion、Rigidbody、Type 扩展的功能消费者；游戏火箭的 Physics2DSetting 仍来自原 XF，未改游戏或原包。Core Listener 仍消费 Foundation Contains 的有效 Collider layer，直接回归保留。保留现有类型、namespace、程序集、方法签名和产品脚本 GUID，仅新增 DistanceToRay；无 asmdef 变更，Foundation 中立闭包、Core/Runtime 互不引用及领域请求所有权不变。
- 集合 IEnumerable 快照（数组复用）与 Dictionary 实时枚举、当前 Culture 解析、两种概率离散化及 RNG 推进差异、相机临时/返回资源责任保持原契约。本批未修改这些算法，不为形式统一添加分配或隐式副作用。JSONObject/Grid/Table/FSM/Zip 及其他独立通用能力原样保留，不宣称获得全量验收。
- 增量更新现有 `Documentation/Modules/Extensions.md` 的公开行为、异常、Ray 半轴用法与实际测试入口，不新建重复方案/过程文档；四个新增测试脚本 meta 由 Unity 导入生成，八个产品脚本 meta 未变。原 Package、游戏、Scene/Prefab/Bundle、Packages 与 ProjectSettings 无改动。

**分层证据（本轮实际执行，按 PassedTests/逐项 Results 统计）：**

| 验证层 | 结果与范围 |
|---|---|
| 确定性 RED | Tools 首轮 7 Passed/18 Failed，复现 TB 空文本、非法层别名、距离 -3/反向平行错误、q/-q 得到 2.75 而非 0.25、FullName=null 异常及销毁 Component 的 MissingReferenceException；抛物线首轮 3 Failed，另两个定向用例分别复现非法写不拒绝与不可达赋值不拒绝。不可达 RED 实际产生原生 NaN velocity 错误（Console sequence 45429），未屏蔽。 |
| Unity 实际编译 | 当前打开的 Unity `2022.3.62f3c1` 经 AssetDatabase 刷新实际编译，新增/修改源码在 Test Runner 实际执行；最终 Editor IsCompiling=false、IsUpdating=false。没有以 IDE/dotnet 替代。 |
| Tools EditMode | ToolsExtensionsContractTests **30 Passed**：非法层极值、bit0/31、未定义名、碰撞矩阵只读、单位阈值至 long.MaxValue、CurrentCulture、直线有符号参数/半轴、同向/反向/近乎平行、零方向/大有限距离、q/-q 八种组合及缩放/同端点/非法旋转/小角度/无法表示的比例、开放 Type、失效组件。 |
| 抛物线 EditMode | ParabolicMotionContractTests **5 Passed**：正常下落/顶点、零速度自由落体、不可达及速度保持、useGravity=false 直线、零速度无确定时间、非有限输入、2D 零/负 gravityScale；大有限有效重力通过实际私有纯计算内核验证连续解，不更改全局重力，也不宣称原生引擎支持该极端运动参数。 |
| 直接闭包 EditMode | FoundationBoundaryTests **6 Passed**、CoreIdentityTests **9 Passed**、NeutralLayerMaskConsumerTests **2 Passed**、AudioSourceIndependenceTests **3 Passed**；Audio 为真实源码的 EditorAssembly 与 Windows Development 两 profile，不替代 Player 构建。 |
| 原生 PlayMode | ParabolicMotionNativeTests **2 Passed**：自有局部 3D/2D PhysicsScene 固定步长模拟、成功赋值/失败保留速度、3D 重力抛物线及零重力直线、2D 零 gravityScale 运动；原生积分与连续解的步长偏差分别断言，不操作默认物理 Scene 或全局自动模拟设置。 |
| 对象生命周期 PlayMode | ExtensionLifecyclePlayModeTests **1 Passed**：延迟 Destroy 生效前后、managed 引用仍存在但 Unity fake-null、GameObject/Component 掩码与重复 Gracefully 销毁；SingletonHostPlayModeTests **3 Passed**：同帧退休身份、跨场景存活/外部销毁重建、既有路径单例 Dispose。临时对象/局部场景由自身 finally/TearDown 清理。 |
| 静态与收尾 | `git diff --check` 通过；没有产品文件迁移/程序集变更、产品 meta 修改或禁止目录改动；最终 Editor 非 Play、不编译/更新，仅 Main 场景打开且 IsDirty=false，测试局部场景已卸载；新增测试均有导入生成的 meta。 |

最终定向通过数 **EditMode 55、PlayMode 6，共 61 项**，不使用 MCP 全工程发现总数计为执行数，不重复计 RED/重复 GREEN。审查修复后重跑受影响的 Tools/抛物线、Foundation/Core/掩码/Audio 闭包及原生抛物线；未受这些修复影响的生命周期和 Singleton 结果沿用本轮有效证据。

**失败分类与剩余限制：**

- 独立只读审查发现两个实际回归：Quaternion.Angle 的死区吞掉 0.1° 终点比例，以及近乎平行共面直线经 float 最近点重建误报距离 2。新增用例在本工程 Unity 实际分别返回 0（期望 1）和 2（期望 0），修复后通过；随后补不可表示比例的实际拒绝 RED→GREEN，防止小角度算法输出 Infinity。没有静默将小角度死区改成新契约。
- 审查的另两个大数溢出候选来自外部 CLR 纯计算探针/源码，不在当前 Unity 执行环境复现为产品失败；大有限 Ray 距离用例及直接纯计算内核用例在修复前后均 Passed，不将其写成 Unity RED→GREEN。距离修复同时显式使用双精度长度，抛物线判别式首乘改为 `2.0`，避免依赖不同后端对 float 中间精度的保留。最初尝试原生 gravityScale 极值被当前 Unity 夹到有效上限，属于夹具前提失败，改为直接纯计算验证而非修改全局物理设置；这些证据不替代 IL2CPP/Player 验证。
- 初次新增测试导入前的组合筛选返回 Unknown/空 Results，不计为通过；显式刷新后用单类过滤取得有效逐项 RED/GREEN。两次 Roslyn script-execute 环境探针超时，不作为验证证据；后续使用有效的 Editor 状态、刷新和实际 Test Runner 结果，没有启动第二个 Unity 实例或修改工具配置。
- Play 测试本体取得有效 Passed，但退出 Console 仍记录原 XF LoggerManager 的 `Some objects were not cleaned up when closing the scene`（sequence 45593～45597）。复查后的批量 Play 调用曾在域重载交界连接失败，丢失结果不计通过；连接恢复后原生抛物线类单独重跑取得 2 Passed。没有修改原 Logger/Package、替换 logHandler、清除/吞异常或沿用旧隔离授权；本批不证明原 Logger 退出安全。
- 未改变集合/概率/Culture 解析/相机算法，未机械补全量回归；Pool/Resource 请求、JSONObject/Grid/Table/FSM/Zip 不在本次修改闭包，不因保留就宣称全面验收。原生局部场景证明本批速度及对象行为，不替代碰撞引擎自动分发、原游戏综合路径或实际进程退出。
- HTTP 正式归属、完整 Tools/Extensions 迁移和旧机制/LogUtil 退出留 5.4；阶段 5.5 的同域连续双 Play、Windows64/Mono Player 构建运行与综合游戏门禁均未提前执行，移动/WebGL/IL2CPP/AOT 仍未验证。没有性能测量，不宣称性能提升。

**结论：满足 5.3 本批技术完成条件，保留能力的关键行为、直接闭包和最小原生门禁已取得有效证据，具备进入 5.4 的前置基础。等待用户验收及 Git 收口，阶段 5 仍 IN PROGRESS；不自动实施 5.4，不 Commit/Push/Merge/Tag/Reset/Clean 或切换分支。**

以上为 5.3 实施轮次的历史停止点。5.3.5 开始前用户已确认 5.1～5.3 技术验收及 Git 收口；5.3 对应提交为 `5586f40967f09be14f4f7e9d79f95d0eb3509acd`。

### 5.3.5 成熟 Tools 模块的框架整合

- **主要轴：**仅研究并按需要整合既有 `Tools/FSM`、`Grid`、`Json`、`Table`、`ZipTool` 的架构归属、现有能力协同、正式入口、分发与直接消费者。融合前成熟模块的功能 / 质量验证作为既有背景，不重新全面源码审查，不要求每个模块产生代码变更。
- **职责边界：**FSM 是程序员运行时直接编写的轻量纯代码状态机；与具备完整 Editor 的大型可视化 XFFSM 定位不同，可分层共存。本批不承接 XFFSM，不强制依赖 Module / Manager，不预建兼容抽象。Grid 保留二维结构；JSONObject 保留 JSON 树，不与 Json.NET / Save / HTTP 强制统一内核；Table 保留表与索引及 Resource 协作；ZipTool 保留独立无 UnityEngine 程序集和内置 SharpZipLib 许可。
- **前置 / 退出：**5.1～5.3 已验收，D1～D3 及阶段 1～5.3 核心契约有效。只对实质接入问题做最小必要调整；重大公共接口、程序集方向或资源身份变化先提供证据并等待确认。原包、游戏、资源、Scene / Prefab、Packages / ProjectSettings 均不修改。
- **验收：**Runtime / asmdef 修改才要求相应实际 Unity 编译与直接消费者回归；导出 / Editor / 身份调整定向验证实际入口。未修改成熟模块不重跑原有完整测试，不为批次新增抽象或机械用例。同步现有架构 / 使用说明，覆盖口径以当前可运行入口为准。
- **衔接：**5.4 继续 HTTP 正式迁移、其余外围机制退出和 D2 LogUtil 退役，不重复改造本批已确认合理的成熟 Tools；5.5 综合 Player 门禁仍单独授权实施。

#### 5.3.5 实际整合与验证记录（2026-10-12）

**实际基线：**`fusion/5-runtime`，HEAD `5586f40967f09be14f4f7e9d79f95d0eb3509acd`，开始时工作区干净；与用户指定基线相符。已读全局 / 项目 AGENTS、批准计划、Architecture 与阶段 1～5.3 的必要成果约束，核对 Unity `2022.3.62f3c1`、manifest、工具声明、asmdef 与实际消费者。没有重新进行成熟模块全面源码审查，也没有修改原包 / 原游戏或调整依赖包。

**最终定位与必要取舍：**

| 模块 | 正式定位、协作与接入 | 本批调整 |
|---|---|---|
| FSM | `YLFramework.FSM`，整体 Runtime；程序员纯代码使用，Allocate / 注册 / 显式驱动 / 回收，复用现有 Runtime Safe 池与 Foundation 池内核。与独立大型可视化 XFFSM 分层共存，无 Module / Manager / XFFSM 依赖 | 保留代码、名称及程序集；补 IFsmOwner 示例前提、调用方生命周期责任，以及既有池契约的退出失败隔离边界。隔离不等于状态资源已清理，不改阶段 2 池契约或 FSM 算法 |
| Grid | `YLFramework.Utilities.Grid`，整体 Runtime；NormalGrid 矩形 / DynamicGrid 稀疏二维容器，无框架内依赖 | 保留代码与直接构造入口；纠正导航的“动态扩容”措辞，不用其他容器强制替代 |
| Json | `YLFramework.Utilities.Json`，整体 Runtime；JSONObject JSON 树 / 字段处理，与 Json.NET typed serialization、Save 文件策略、HTTP 传输各司其职 | 保留独立内核和上游类型名；补接入 / 职责与许可分发说明，不改 Save / HTTP 消费链 |
| Table | `YLFramework.Utilities.Table`，整体 Runtime；Table / TableIndex 通用表与多值索引，复用 Foundation List / Dictionary 池，Resource ResTable / AssetDataTable 已直接消费 | 保留实现和消费者；补真实 Pool 程序集归属、数据项借用 / 索引释放责任，不替代 SerializedDictionary，不接管资源 Retain / Release |
| ZipTool | 独立 `YLFramework.Utilities.ZipTool`，`noEngineReferences: true`、框架 references=[]；通用 Zip / GZip 与会话读取。Resource Android APK 路径及导出测试已消费内置低层类型 | 保留程序集 / 门面 / 低层公开类型；明确显式程序集引用、Resource 平台边界及来源 / 完整 MIT 许可，不强制消费者迁到高层门面 |

**实际改动：**增量维护现有 Architecture、README、五个模块使用说明及长期蓝图的阶段 5 状态；修正 Runtime 总来源说明中错误的 SharpZipLib 许可导航，真实文件为 `Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md`。JSONObject `LICENSE.md`、SharpZipLib 原完整 MIT 文本及署名未改。原模块文档列出的 FSM / Grid / Json / Table / Zip 专项测试文件当前不在仓库，已明确现存入口和覆盖范围，去掉不可作为当前证据的旧全量数字；不因文档旧名字而重建测试套件。既有 ExportUnityPackageTests 仅增加 15 个固定 sentinel（工具源码、Zip asmdef、两份完整许可及五份使用说明），沿用原真实 ExportPackage / GZip / Tar 检查与临时目录清理，不增加新测试类型或设施。

五个模块的产品 C#、namespace、公开 API、asmdef、所有 `.meta` / GUID 均未修改；Resource / Editor 产品消费者不改，无资源身份迁移。正式 Runtime / Core 仍互不引用，Foundation 不反向依赖工具。没有新增 Manager、抽象层、公共 Root 或其他生命周期系统。

**分层验证（本轮实际执行）：**

| 验证层 | 有效结果与范围 |
|---|---|
| Unity 实际编译 | 当前打开的 `2022.3.62f3c1` 工程经 AssetDatabase 刷新；修改后的导出测试已在 Test Runner 执行。实时 `EditorApplication.isCompiling == false`、`EditorUtility.scriptCompilationFailed == false`，dataPath 与本项目相符；不是 IDE / dotnet 替代证据 |
| 实际导出 | ExportUnityPackageTests **1 Passed**；调用实际默认 ExportRoots + Recurse，读取真正 unitypackage。核对所有当前源文件 / 配置 / 文档在包内、GUID / meta 一致，新增 15 个 sentinel 的 payload 与 meta 字节一致，两份完整 MIT 许可实际随包输出；Tests / 可选 Integrations / 包外 DOTween 排除。自有临时包与目录由 finally 清理，不调用带 SaveAssets / UI 副作用的菜单 |
| FSM / Pool 直接接点 | 既有 `FsmRetainsCallbackReturnGateAndFailedExitIsIsolatedRatherThanRetried(False)` / `(True)` **2 Passed**；正常复用、回调内拒绝回收、退出失败不重试 / 不重新出借及残余状态引用边界保持 |
| Table / Foundation 池接点 | CollectionPoolTests 的 `TableConstructorValidatesSelectorBeforeBorrowingDictionary`、`TableClearAndDisposeReturnOwnedCollectionsOnlyOnce`、`TableRemoveReturnsEmptyBucketAndKeepsOtherKeys`、`TableInsertionFailureReturnsNewBucketAndPreservesOriginalError` **4 Passed**；仅复用既有直接协作用例，不扩大为 Resource / Table 全部契约验收 |
| 实际类型 / 程序集 | 当前 Unity 已加载类型确认 FSM / Grid / JSONObject / Table / TableIndex / SafeObjectPool 属 Runtime，ListPool / DictionaryPool 属 Foundation，ZipUtility 属独立 ZipTool；实际 ZipTool Assembly references 仅 `netstandard`，Runtime 不引用 Core 或原 XF / XFAB / XFFSM。这是当前 Editor 程序集证据，不替代 Player 条件分支验证 |
| 静态范围 | 实际 diff 仅现有文档、总许可导航及导出测试清单；没有新增文件 / meta、产品源码 / asmdef 变化或禁止目录改动；`git diff --check` 通过 |

合计 **7 项 EditMode Passed，0 Failed / Skipped**，逐项 Results 与 PassedTests 为准；工具发现总数 1442 不计为本轮执行数。最初省略参数的 FSM 方法过滤及尝试 class 正则过滤返回 No tests found，不算执行或通过；使用真实含 `(False)` / `(True)` 的完整用例名后取得有效结果，不修改产品或测试断言。

独立只读 diff 复查未发现阻断或重要问题，范围仅本批新增说明与直接接点，不扩展为成熟模块质量审查。类型 / 程序集动态探针初次因字符串换行转义错误被 Roslyn 拒绝，修正探针后取得上表有效结果；该失败属于临时探针，不是工程脚本编译或产品行为失败，没有文件写入。最终 Editor 非 Play / 不编译 / 不更新，仍只有 Main 场景打开且 IsDirty=false。

**限制与停止点：**没有重跑五个成熟模块的历史全量测试，没有 PlayMode、Player 构建 / 运行、干净工程 import、Android APK 实机或 IL2CPP / AOT 验证；本批未改变产品源码或身份，因此未扩大到上述门禁，也不以本批导出证明最终独立承载。FSM 退出失败后的领域资源收尾仍由调用方负责；本批只明确既有契约，不顺手修生命周期。XFFSM、Resource 平台 / 后端整合、全 Plugins 分发、阶段 6 Editor 整体收口与阶段 7 独立承载仍属后续专题。

**结论：满足 5.3.5 技术完成条件，五模块已有合理代码 / 程序集边界保持，正式定位、使用说明及分发接点已收口，具备进入 5.4 的前置基础。等待用户验收与 Git 收口，不自动进入 5.4；D1～D3 保持，阶段 5 仍 IN PROGRESS，不执行任何 Git 写操作。**

以上为 5.3.5 实施轮次的历史停止点。5.4 开始前用户已确认 5.1～5.3.5 技术验收，并报告 5.3.5 提交完成；实际本地与实时远端均指向 `98d08fe0c1f31b3bc91ea8443af4db18f1d87d11`。

### 5.4 正式外围接口与旧机制退出

- **主要轴：**行为验证后的类型/API/程序集归属收口。HTTP 移入 Runtime HTTP 域；其余非 Core 领域的 XF 扩展与格式化进入正式工具家族，UGUI 留 Runtime，实际共享 helper 留 Foundation。合并同职责入口，迁完退出旧副本；经 D2 确认退役 LogUtil，不新建文件 logger。
- **前置/退出：**5.2/5.3 行为证据有效，5.3.5 成熟 Tools 定位、接入与分发边界确认；重查真实调用、meta、反射与资产绑定。最终 Core 无 HTTP/非领域工具重复权威，只有一组展示元数据/一套同职责扩展算法；原 XF 原样保留，不被新 API 转发。既有组件及游戏身份不动。
- **验收：**实际 Unity 编译、迁后的 HTTP 行为用例、Core/Foundation/Editor 身份与依赖、AudioSourceIndependence 两 profile、直接 Resource/Singleton/事件适配回归；确认退出类型、旧路径和无用途壳不存在。导出测试检查迁后源码/meta/许可及旧副本排除，Runtime 无 Editor 产品程序集依赖。不把源码闭包通过当成实际 Player 通过。
- **文档：**只增量维护 Architecture、现有 HTTP/Tools/Extensions/Attributes 使用入口及必要来源说明，说明接入、取消和所有权。FSM/Grid/Json/Table/Zip 的定位及现有测试入口由 5.3.5 收口；其他模块继续按实际变更核对覆盖口径，不为凑齐文档名称批量创建测试。

#### 5.4 实际整合与验证记录（2026-10-12）

**实际基线：**`fusion/5-runtime`，HEAD `98d08fe0c1f31b3bc91ea8443af4db18f1d87d11`，开始时工作区干净；`git ls-remote origin refs/heads/fusion/5-runtime` 实时返回同一提交，本地远端跟踪一致，无未推送提交或未知改动。已核对全局 / 项目 AGENTS、LF 约定、Unity `2022.3.62f3c1`、manifest 及批准方案第 4 节 / 5.4 / D1～D3；仅定向研究外围模块与直接闭包，没有重新研究总体架构或成熟 Tools 内部实现。

**正式归属与兼容变化：**

| 能力 | 最终归属及实际取舍 |
|---|---|
| HTTP | `Runtime/Services/Http/`，`YLFramework.Services.Http.HttpManager` / `SendWebRequest` / `HttpRequestStatus`，整体 `YLFramework.Runtime`。两个产品文件仅变 namespace，不改 5.2 请求构造、会话 / 取消、稳定终态、快照、Handler 所有权或通知代码；消费者引用 Runtime，不再为 HTTP 带入 Core / XFAB。旧 Core 类型 / 路径不留副本或转发壳 |
| 独立扩展 | 14 个原 Core 类型按职责迁到 `Tools/Extensions/{Math,Unity,System,Utilities}/`，统一 `YLFramework.Utilities.Extensions`：Animator、Bounds、Color、EventTrigger、LayerMask、Quaternion、Random、Ray、Rigidbody、Scene、Time、Type、Vector2、Vector3。除 namespace 与文件尾部空行外算法不变，UGUI 留 Runtime；不为单 / 复数类名形式统一重命名 |
| 相同机制收敛 | 两份 `IsDestroy` 的 `!UnityEngine.Object` 合为既有 `ObjectExtension.IsDestroy(Object)`，GameObject / Component 均可直接扩展调用。原 GameObject 的递归 `SetLayer` 合入既有 `GameObjectExtension`，保留默认 GetComponentsInChildren 排除 inactive 子孙的范围；与仅改自身的 `Layer` 不互换。旧 GameObjectExtensions / ComponentExtensions 实现及 meta 退出，无资产绑定 |
| 语义不同的保留能力 | 百分比两种离散化 / RNG 推进、逆插值与方向 / 分量、Unity fake-null 与托管 null、无限直线与射线半轴等按原契约保留；不造来源别名或通用工具平台。5.3 的几何、有效重力 / 不可达拒绝、掩码和格式化行为保持 |
| 格式化 | `Runtime/Tools/StringTools.cs` / `YLFramework.Utilities.StringTools`，整体 Runtime；FormatByte / FormatProgress 算法不变，不扩展成格式库 |
| Foundation / Core | 本批不扩大 Foundation 闭包：唯一类型查找、严格 LayerMask 成员判断、展示 Attributes 与 Foundation 历史异步通知均保持。Core 仅保留领域请求 / 组件和分析器标记，不再包含 HTTP、通用 Extensions、StringTools 或 LogUtil；Core 与 Runtime 互不引用 |
| 成熟 Tools | FSM / Grid / Json / Table / ZipTool 的代码、API、程序集及定位未改；轻量纯代码 FSM 与可视化 XFFSM 的独立职责保持，不开展 XFFSM 融合 |
| LogUtil | 按 D2 删除自有实现及 `.meta`，不留壳或新 Logger。产品消费者为零，唯一历史测试改为退出 / 共存检查；惰性全局 Exception 订阅、后台文件写入、三天文件清理和旧退出回调随实现退出。未删除已有日志、调用原包惰性 LogUtil 或修改原 LoggerSystem / 游戏日志；正式文件日志留后续 LoggerSystem 专题，当前没有正式替代品 |

**消费者、身份与分发：**

- HTTP 两份测试移到 `Tests/{EditMode,PlayMode}/Http/`，三份工具 / 抛物线测试移到 `Tests/{EditMode,PlayMode}/Extensions/`，由既有 Runtime 测试程序集编译；不为迁后测试增加 Core PlayMode→Runtime 依赖。CoreIdentity 同步领域请求边界及外围退出门禁，历史 CoreCoexistence 测试文件 / GUID 保留。
- 17 份迁移产品 `.meta` 与 5 份迁移测试 `.meta` 均通过 `git rev-parse HEAD:<old>` / `git hash-object --no-filters <new>` 实际核对，原始字节相同。HTTP GUID 仍为 `fe1d02983613d0e49b3a622fa7377063`、`cc8484abb98c20245860b999454a03e7`；AssetDatabase 实际解析到新路径。6 个新目录由 Unity 导入生成新身份，GUID 各唯一。两个抽空旧目录经验证为空且无 GUID 引用后由 Unity AssetDatabase 删除。
- 定向搜索迁移 / 退出脚本与目录 GUID、类型字符串、Editor 和真实消费者，未发现 Scene / Prefab / asset / config 的绑定。旧类型名仅剩测试退出守卫；Listener / Drawer / 领域组件的类型、字段、程序集和 GUID 均未改变。不为未确认的仓库外消费者建立永久兼容层。
- Runtime / Core / Foundation / Editor 的产品 asmdef 全部未改。HTTP 的会话支撑实际属于 Foundation，Runtime 沿用既有窄 friend 许可；Foundation 无上层业务依赖，Core / Runtime 互不引用，Runtime 不依赖 Editor 产品程序集。Editor 直接 IO / 字符串消费者继续编译，阶段 6 Inspector / Editor 扩展不提前整合。
- 增量更新 Architecture、README、Extensions 使用说明和 Runtime 总来源记录；Attributes / 必要 Drawer 无本批行为或归属变化，不重复改写。XF / QFramework 既有署名及完整 MIT、JSONObject / SharpZipLib 许可继续随默认导出，未改全 Plugins 许可范围。修正文档中当前不存在的 GameObjectLoaderPlayModeTests 导航，不重建历史全集。
- 原 Package、原游戏、持久资产 / Prefab / Scene、Packages / ProjectSettings、Audio / Resource / Action / UI / Module / Root 与成熟 Tools 产品代码均无本批差异。

**分层验证（本轮实际取得有效 Results，不计发现总数或重复运行）：**

| 验证层 | 有效结果与范围 |
|---|---|
| Unity 实际编译 | 当前打开的 `2022.3.62f3c1` 工程经实际 AssetDatabase 刷新 / 重编译，迁后测试由 Test Runner 执行；最终 `isCompiling=false`、`scriptCompilationFailed=false`，dataPath 与本项目相符，不使用 IDE / dotnet 替代 |
| HTTP | **EditMode 5 Passed、PlayMode 20 Passed**。正式 Runtime 服务归属门禁先在旧 Core 实际失败，再迁移后通过；复用完整 5.2 真实 TCP 回环用例，覆盖 GET / Form / JSON / PUT / DELETE、200 / 503、timeout、在途 / 未发送取消和退出、前后 / 完成异常与同步重入、宿主销毁、快照隔离及原生 m_Ptr 释放。后台夹具不访问 Unity 对象 |
| 工具 / 几何 | **ToolsExtensionsContractTests 32 Passed、ParabolicMotionContractTests 5 Passed、ParabolicMotionNativeTests 2 PlayMode Passed**。Object 唯一入口及递归 / 单对象设层门禁先缺入口失败，再通过；LayerMask、字节阈值、Ray、Quaternion、Type、失效组件与有效重力 / 拒绝路径保持。原生测试只模拟自身局部 3D / 2D PhysicsScene |
| 身份 / 依赖 | **CoreIdentity 9、FoundationBoundary 6、CoreEditorIdentity 8、NeutralLayerMaskConsumer 2 EditMode Passed**。覆盖正式类型 / 旧副本退出、程序集依赖、5.1 唯一查找 / Attributes / 掩码、必要 Drawer 接线及真实 Core Collider 过滤消费者 |
| Foundation 历史通知 | **CustomAsyncOperationTests 16 EditMode Passed**。重复完成更新 error、完成后 Abort、重复 hook、通知中断 / 顺序及迟到通知的历史契约不变，不把 HTTP 稳定终态推广到基类 |
| Audio 真实源码闭包 | **AudioSourceIndependenceTests 3 Passed**：固定清单仍指向正式 Component / GameObject / Object / Vector / Shared LayerMask 文件，两个 profile 重编译包含本次合并后的源码；EditorAssembly、Windows Development 非 Editor 和引用白名单通过。未引用预编译 YL / XF / XFAB 实现来放宽门禁，不替代 Player 构建 |
| 直接消费者 | **ResourcePoolTests 7、UnityEventListenerContractTests 4 EditMode Passed**；**ExtensionLifecycle 1、SingletonHost 3、CoreModulePlayModeTests.CreateControllerOverloadsCreateLocallyWithoutBorrowingOtherModules 1 PlayMode Passed**。保留 Resource / Events / Singleton 的代表性闭包、真实帧末 fake-null、旧宿主 / 场景卸载责任和字符串 Controller 创建，不重跑领域全集或原游戏综合流程 |
| LogUtil 退出 / 共存 | **CoreCoexistence 1 PlayMode Passed**。先因旧类型仍存在实际失败，再退出后通过；仅检查原包类型身份、不初始化原日志入口，观察 Application / Editor 静态委托无旧回调，原 / YL 日志目录存在性与全部 SHA256 内容不变，无替代写盘或清理；短观察窗不是真实连续双 Play 证明 |
| 正式导出 | **ExportUnityPackageTests 1 EditMode Passed**。实际 ExportPackage(Recurse) 并读取真实 unitypackage；新增 23 个固定 sentinel 覆盖 HTTP、工具、合并入口、Foundation / Core / Runtime asmdef 与完整 XF MIT，payload / meta 字节与当前源码一致；旧 Core 路径、旧 LogUtil / 合并退出 GUID 和 Tests / 可选 Integrations / 包外 DOTween 排除。临时包 / 目录由 finally 清理，未触发菜单 SaveAssets，不证明干净工程 import |
| 静态 / 最终状态 | `git diff --check` 通过，受保护目录零差异；独立只读全差异审查未发现可确认的本批代码缺陷或越界改动。最终 Editor 非 Play / 不编译，HTTP pending=0、pre / post 订阅为空、Foundation CoroutineStarter host=0；仅 Main 场景打开，IsDirty=false |

合计 **98 项 EditMode、28 项 PlayMode，共 126 项 Passed，目标用例 0 Failed / Skipped**。中间 red、重复 green 和未取得结果的批量调用不重复计数。

**失败分类、限制与停止点：**

- 导出排除 Core 旧目录的门禁先实际失败，再迁移 / 删除空目录后通过。新增退休 GUID 查询起初误把 `GUIDToAssetPath` 的会话删除缓存当存续资产；动态探针确认文件、meta、可加载资产均已不存在。Context7 没有匹配该 API 的资料，改查 Unity 2022.3 官方 AssetPathToGUID 文档，按 `OnlyExistingAssets` 加磁盘 / 包内排除断言后通过，没有重启、清缓存或弱化退出条件。
- Play 批量连续调用曾触发 MCP “another test run is already in progress”（request `26774fb7-0a73-4440-b870-a87abb40958d`）；丢失的批量结果不计通过，待实际 Editor 恢复后各目标类 / 方法分别取得上表有效结果。临时最终探针一次 MCP timeout 不计验证，修正探针并恢复连接后取得明确最终状态；未修改产品来解决工具瞬断。
- 实际退出日志仍出现原 XF `LoggerManager` 的 `Some objects were not cleaned up when closing the scene`（Console sequence `46069`）。另有 MCP 自动 SkillFileGenerator 写 `.kilocode/skills/inputsystem-action-add/SKILL.md` 失败（`46066`），属于插件生成 / 工具日志，不是 YL 脚本编译失败。本批不改 Package、权限、工具配置或 logHandler，不屏蔽这些错误，目标测试通过不证明原 Logger 退出安全。
- namespace / 程序集归属是批准的公开迁移：旧 Core HTTP / Extensions / StringTools 和两个合并类的静态调用名退出；仓库内直接消费者及测试已迁。仓库外程序集限定类型名、动态反射拼接、裁剪 / AOT 风险未获得额外兼容证明，不虚构兼容 shim。
- 没有执行阶段 5.5 的真实同域连续双 Play、Application 进程退出、Windows64 / Mono Player 构建运行、原游戏综合路径或干净工程 import；IL2CPP / AOT、移动 / WebGL、全 Plugins 许可认证和无原 15 系统最终独立承载仍未验证。后续 LoggerSystem、资源后端、阶段 6 Editor 与阶段 7 命名 / 独立承载均未自动实施。

**结论：满足 5.4 正式归属、必要同职责整合、旧 LogUtil 退出及定向技术门禁，具备进入 5.5 的基础；原 Logger 退出问题必须在 5.5 如实处理，不能据本批判定综合退出通过。等待用户验收及 Git 收口，阶段 5 仍 IN PROGRESS，5.5 不自动实施；本批未 Commit / Push / Merge / Tag / Reset / Clean 或切换分支。**

### 5.5 阶段综合门禁

- **主要轴：**验证与交付，不再借验收展开功能重构。按最终闭包运行一次代表性跨模块回归；实际 Unity 编译、EditMode、PlayMode、Player 分层报告，不沿用迁移前的受影响结果。
- **真实会话：**同一脚本域、正常 Scene Reload 连续两轮 Play，验证 HTTP pending/transport/钩子退出、旧通知隔离、退出拒绝新请求；代表性 View/Panel 和 Audio 旧身份/资源责任不变。恢复 Editor 设置/场景，不用反射 Reset 代替退出证据。原 Logger 再阻断时先报告；先前临时日志隔离授权不能自动沿用。
- **Player：**一次 `StandaloneWindows64/Mono` Development 构建与实际运行，覆盖本地回环 HTTP 成功/协议失败/取消/退出、非 Editor 特性/工具编译和代表性 View/Panel/Audio 接点。服务端进程与夹具只拥有自己的状态；不更改正式 Build Settings 或后端。HTTP 的 Unity 原生取消/释放和条件编译变化值得这个门禁，即使游戏暂未使用。
- **原游戏非干扰：**复用现有 Main/FSM/XFAB/YL Audio 的菜单设置、点击、进入/暂停返回/再入代表性路径；保持既有 Bundle 与配置。HTTP 单批不机械重跑全游戏，阶段末因 Runtime/helper 迁移补一次必要 Editor/Player 代表性检查。产物不足则列环境阻塞，不擅自重建 XFAB。
- **完成条件：**五项能力的归属和有价值用途明确，旧自有重复机制已退出，直接消费者与必要 Player 门禁通过；迁移/许可/分发证据独立成立。IL2CPP/AOT、移动/WebGL 网络限制及无原 15 系统承载仍属后续，不以本阶段通过证明最终独立。

#### 5.5 实施与验证记录（2026-10-12）

- **基线：**用户独立授权。实际 `fusion/5-runtime`，HEAD、本地 upstream 与实时 `git ls-remote` 均为 `a4399a617654f2828b10f6e2bd5752cd0e4271f0`；开始时工作区干净。Unity `2022.3.62f3c1`，StandaloneWindows64 / Mono，Main 场景无未保存修改。
- **最终职责：**D1～D3 成立。HTTP / StringTools / 通用 Extensions 归 Runtime；类型查找、严格 LayerMask 成员判断、展示元数据及最小等待通知归 Foundation；View/Panel、Audio 保留各自 owner / 取消 / 租约 / 共享 producer。旧 Core HTTP / Tools / Extensions 与 LogUtil 无转发壳；正式文件日志留 LoggerSystem。FSM、Grid、JSONObject、Table、独立 ZipTool 的职责、依赖与完整许可保持 5.3.5 结论。
- **修改范围：**未发现需要修复的阶段 5 产品缺陷，未修改产品源码、未重新重构成熟模块。新增仅为不参与分发的 `Tests/Core/Acceptance/RuntimeAcceptanceEntry`、Editor-only Driver、配套 meta 与现有文档增量；复用 5.4 程序集 / 迁移 meta-GUID / 定向测试及 5.3.5 的未受影响证据。
- **本轮 Logger 授权：**首次真实 Enter Play 的 Console `46079` 证明 `LoggerHandler.LogFormat → LoggerManager.Instance → Init` 在过渡期创建 DDOL 对象，`46080/46081` 为日志处理器 `NullReferenceException`。先退出并恢复，再取得用户本轮“授权本轮临时隔离”答复；只在受控验收窗口保存、临时替换并恢复 `Debug.unityLogger.logHandler`，未改 Package、未吞异常。原游戏 / Player 使用原 Logger，未隔离。
- **真实双 Play：**Driver 准备自有临时场景，由真实 Editor Play 入口开始；关闭 Domain Reload、保留正常 Scene Reload。同一 AppDomain / 探针身份经历两轮真实 Enter / Exit，最终 `DOUBLE_PLAY_PASS enters=2 exits=2 verified=2`。未以反射 Reset / Quit 替代真实 Play。
- **HTTP 会话证据：**两轮成功 / 503 响应快照、在途 POST / UploadHandler、幂等物理 Abort、退出 Invalidated 通过；request / DownloadHandler / UploadHandler 原生指针归零，退出 pending 表及最终 Foundation 宿主为零。旧钩子 / 身份 / 完成通知不污染新轮，退出通知不能创建新请求或替换宿主。真实退出后的旧原生通知 / 旧 HTTP 操作 / 旧 Audio provider 补充调用，仅用于证明迟到隔离，不代替真实退出。
- **消费者证据：**代表性 Core owner、活跃 View、独立 pending Panel 和 Audio active voice / pending acquisition 通过。按真实相位验证 `Application.quitting` 的 HTTP / pending Panel / Audio 清理，以及场景销毁后的活跃 View 解绑，不要求 View 在场景销毁前提前解绑。测试 Provider 计数只证明探针自有租约归还，不冒认 XFAB Bundle 物理卸载。
- **验收代码加固：**独立审查后修正后置回调失败锁存、双轮就绪 / 证据身份、进入 / 运行 / 退出超时、日志失败与恢复、自有 Clip 清理及活跃 View 证据。修正探针的错误前置条件，不修改成熟产品契约。提前停止、进入超时与服务丢失的负向尝试未记为 PASS；最终加固源码重新执行完整双 Play 后通过。
- **Player 构建：**显式 `BuildPlayerOptions.scenes` 使用自有临时场景 + 原 Main，构建一次 Windows64 / Mono Development Player，不改正式 Build Settings / backend / 持久 Player 配置。Succeeded，0 errors、4 warnings、152934569 bytes；警告均为既有 MCP Tilemap / InputSystem / Cinemachine / Timeline 空 Runtime 程序集。
- **Player 运行：**独立 Node 服务仅绑定 `127.0.0.1`，系统选择可用端口（最终 7378）；`/ok` 为 200、`/error` 为 503、`/hold` 保持在途，无公网依赖。初始会话生命周期服务失去后记为失败，改用受控独立进程，完成后只停止本任务服务。真实 Direct3D11 窗口 Player 无 `-noaudio` / `-nographics`；成功、协议错误、取消、退出 Invalidated、原生释放、Foundation 查找 / Attributes / StringTools / Grid / JSONObject 与 Core / Audio 协作通过，`RUNTIME55 QUIT_PASS round=1`，实际进程退出码 **0**。
- **Player 日志分类：**保留 `Curl error 42: Callback aborted` 原生取消 / 退出诊断，不忽略或改写；未见未处理托管异常、探针 FAILED 或退出残留宿主错误。Direct3D refresh / vsync 与 Mono symbol 提示不作为 HTTP 失败；原 Logger 未阻断本次 Player。两份日志末尾还有 Unity `##utp MemoryLeaks / Immediate` 分配汇总（HTTP 585750 bytes / WebRequest 1427，原游戏 595684 bytes / WebRequest 4440），无具体对象 / 调用栈，不能据此定位为正式 HTTP 自有泄漏，也不能声称整个引擎零泄漏；本轮仅凭实际独占对象指针归零及连接关闭证明 HTTP 所有权范围内释放，未执行全引擎内存专题。
- **当前资源匹配：**两处 MainModule 1.0.0 / StandaloneWindows64 均为 `2026/10/9 19:03:49`；112 个 build-info 条目大小 / MD5 全通过，109 个普通 Bundle、143 条依赖与映射目标齐全，224 个非 meta 文件逐项大小 / SHA256 一致，122 个 manifest 源资产路径存在。build-info SHA256 为 `df1ffc32e3fd20a25543176de96f672fb6cba8bedb83751b8ac5489365918145`，bundle-list 为 `964628dfc049317628961f631e941616ebd4531d3088a61f73558d0be8f9d648`，映射为 `8759f89c3df32f80b883ee3db88751dd30d1975f024445c01a41033d6140a276`。这些 hash / 日期不同于 4.5 文档第 350 行历史值，不声称历史字节同一；以当前自洽产物、源码 / 组件 / 程序集身份及实际运行证明匹配。未重建、复制或替换 XFAB 产物。
- **原游戏：**复用既有 `AudioGameAcceptanceFlow`，在正常 Domain Reload 的 Editor 和同一最终 Player 中，真实执行 Main / 原 XF Module / XFFSM / XFAB 启动、菜单、设置 Slider 持久回调、实际 Button pointer-click / AudioEmitter 音效、进入游戏、暂停返回、再次进入。原 EventSystem、音效全局去重与 BGM 暂停策略保持，Source / DSP 输出和接线通过；两处 `RUNTIME55 GAME_PASS`，Player 退出码 **0**。Flow 恢复测试偏好键、timeScale 与自有 FSM / Module；不当作完整战斗回归或本轮人工听感验收。
- **实际分发：**`ExportUnityPackageTests.DefaultExportContainsTheIntegratedSourcesAndPreservesTheScreenPrefab` 本轮实际 **1/1 通过**，检查真实 unitypackage 源码、asmdef、meta、文档、XF / QFramework / JSONObject / SharpZipLib 完整许可、sentinel 字节及退役路径 / GUID 排除。不是干净工程 import 或无原生态独立承载证明；Architecture / Modules 的正式入口和依赖说明保持。
- **验证口径：**一次过早启动的 Export 测试被 Unity 拒绝（Play 中不能保存测试场景设置），随后确认 EditMode 空闲并重跑通过。额外组合身份筛选的 MCP 请求超时、未取得有效结果，**不计为通过项**；对应未变化产品的身份 / Foundation 边界复用 5.4 已验收证据，不把工具 timeout 当作产品成功或失败。明确范围 `98d08fe..a4399a6` 的完整 `git diff --check` 有四份 Unity 目录 meta 的 12 处尾随空格；限定 C# / asmdef / Markdown 检查通过。校正 5.4 的空白检查范围，不顺手格式化历史 meta。
- **恢复：**原 Main 场景恢复且无未保存修改；Play options 恢复 enabled=false / flags=3，持久 `EditorSettings.asset` 无 diff，正常游戏会话保留原 XF LoggerHandler。删除自有临时 `Runtime55.unity` / meta；自有 Clip、pending、宿主清理，独立服务停止。原游戏、Prefab、Scene、Bundle、Packages、ProjectSettings 及原日志未被修改或清理。
- **产物与复验：**工程外入口 `C:/Users/Kano/AppData/Local/Temp/kilo/CGJ2024-5.5-20261012/`，包含最终 exe / Data / Mono runtime、`build-summary.txt`、`http-player.log`、`game-player.log`、`double-play.log`、`game-editor.json`。验收源码留在 Tests、不随框架导出；复验须先准备本地服务，调用 Driver 的 `Begin(url, logPath)` 后使用真实 Editor Play 入口，不能把准备调用当作已进入 Play。
- **完成与限制：**阶段 5 必要技术门禁均满足。原 Logger 无 Domain Reload 兼容、正式文件 Logger、真实 AB 物理卸载、无原生态独立承载、IL2CPP / 移动 / WebGL、Runtime 历史目录整体收口仍归后续；不借本批全面整理 `XFMain` / `Shared`。待用户最终验收与 Git 收口，不自动进入阶段 6。

## 6. 总体审核决策与跨阶段事项

| 决策 | 简明比较 | 推荐结论与确认状态 |
|---|---|---|
| **D1 公共归属与 HTTP 位置（用户已确认）** | 留 Core 省身份改动，但 HTTP/通用工具消费者被迫带 Core/XFAB；全部下沉 Foundation 又会带入 UGUI/服务/工具大全；新增多个 Utilities 程序集增加组织成本 | **HTTP→Runtime，中立真实共用闭包→Foundation，领域请求→Core，其余工具/UGUI→Runtime**，复用现有 asmdef；接受本阶段必要类型归属迁移，不改领域组件/游戏身份 |
| **D2 历史 LogUtil（用户已确认）** | 保留须修后台队列/Writer/线程准入/退出/全局订阅，等于继续维护一个独立文件日志 owner；原工具已标记过时且未覆盖这些风险 | **自有旧 LogUtil 在 5.4 满足迁移及验收条件后退役，不留兼容空壳**（不删除原游戏日志、不修改原 XF Logger，也不删除已有日志文件）；正式文件日志仍是长期需求，留 LoggerSystem 专题融合。原 XF Logger、原游戏日志及现有日志文件不受本阶段退役影响 |
| **D3 异步共享幅度（用户已确认）** | 全部请求统一会碰触 View事务、Audio自然完成/租约和共享 producer；只硬化基类也会改变明确历史测试契约 | **保留最小基类及历史通知语义，HTTP 自己管稳定终态与独占 transport**；不统一 Status/取消/owner，不新增公共任务表。可独立取消 HTTP 不等于可物理取消 View/Audio 后端 |

D1、D2、D3 已由用户确认，原 5.1～5.5 路线正式批准，5.3.5 已另获正式新增及实施授权；5.1～5.4 已由用户确认验收及 Git 收口。具体类名、API 布局与算法由获授权批次据上述契约自主决定，不要求逐类确认；每批仍须单独实施授权。5.5 经本轮独立授权完成综合门禁，阶段 5 技术 COMPLETE，待最终 Git 集成收口。

**当前未发现必须先修改跨阶段系统才能完成本阶段的源码阻塞。** 以下是真实保留边界，不扩大为本阶段前置修复：

- Core→XFAB 资源/场景请求、实例池及 Editor 资产定位过渡依赖；物理取消、共享资源计数和 AB 释放由资源专题解决。13 不能以改请求基类解除这些边。
- 原 Logger 的无 Domain Reload/MCP 问题、依赖声明与缓存差异是既有环境风险，验证遇阻时分类报告并申请最小授权，不改原 Package或关闭诊断。
- 分析器标记的等效校验及 Editor 四类扩展整体整合归阶段 6；Attribute 元数据/必要 Drawer 的本阶段迁移不代表已获得全部 authoring/静态分析功能。
- 类型反射/字符串构造仍有裁剪与 AOT 风险，当前未证明 IL2CPP；本阶段明确依赖并用 Mono 门禁，最终独立/目标平台保留按真实消费者补充 Preserve/link.xml 的职责，不无依据全局保留程序集。
- 当前同工程测试与源闭包不能证明无原 15 系统的最终独立运行；阶段 7/长期系统仍需独立承载及完整分发验证。

本方案已经用户正式批准；D1～D3、既有批次边界与跨阶段限制保持原样。**5.5 实施与验证完成，阶段 5 运行时外围满足技术完成条件；待 `integration/xf-yl` 合并与 `milestone/5-runtime` 里程碑完成最后 Git 收口。** 不启动阶段 6。
