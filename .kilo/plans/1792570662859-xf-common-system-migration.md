# XF CommonSystem 迁入 YL Implementation Plan

> **状态：CommonSystem 五项功能主工程技术验收完成，用户已确认通过，Git 收口完成，2026-10-21。** 使用 `executing-plans` 原地逐批执行，T1-T4/G0-G3及独立Review修复已完成，最终定向EditMode 28/28、PlayMode 59/59通过。证据与裁定见§5。未执行独立交付扩展门；本次执行普通本地提交与 Push，不将旧Player/独立包结果算作本次证据。
>
> **Git 收口范围：** 本次提交仅含 CommonSystem 五项能力、直接测试、使用说明和本 Plan/IntegrationPlan 状态；随后单独的根版权与总声明整理（`LICENSE.md`、`Runtime/THIRD-PARTY-NOTICES.md`、UI/Timing/Audio 帮助）保留为未提交的后续文档变更，不混入本提交。


**Goal:** 将 CommonSystem 的五项用途融入 YL，复用已有三种监听器，补齐局部生命周期组件与轻量场景 Controller，并完成有依据的默认值修复、物理层过滤增强及针对性验收。

**Architecture:** 沿用 `YLFramework.Runtime` / `YLFramework.Editor` 两主体。监听适配归 `Foundation/Events/Unity`，场景便利 Controller 只依赖 Core 既有窄资源合同；不复制事件、资源或场景所有权内核，不在 Root 中增加初始化。

**Tech Stack:** Unity `2022.3.62f3c1`、C#、UnityEvent、现有 LayerMaskUtility、Core MVC、ICoreResourceProvider/ResourceSystem、Unity Test Framework `1.3.9`；无新增第三方依赖。

**Spec:** 用户本轮迁移、合理优化/扩展升级及 Bug 修复要求；已确认的两项场景决定见 §2。共同约束参照 `Assets/YLFramework/Documentation/{IntegrationPlan,Architecture}.md`。CommonSystem 是既有 13 项之外的补充专项，不改写其他批次的历史范围或验收。

## Global Constraints

- 只实施本方案必要的 YL 源码、测试、Unity 生成的 meta 和现有说明；原 XF 包/PackageCache、原游戏、MainModule/StreamingAssets、Packages/ProjectSettings、工具配置及现有用户改动受保护，不卸载原包或迁移原游戏。
- 开工重新核对 branch/HEAD、index、dirty、未跟踪项与真实 Editor projectPath。研究时位于 `integration/xf-yl`；已有专项 Plan、`.mcp.json`、三份游戏 FSM、manifest/lock、`Assets/Test/` 和 `AlternateContent.meta` 的改动不接管。
- 既有 YL 类型、namespace、字段名、GUID 和 UnityEvent 持久调用身份保持；新增脚本使用 YL namespace 和新 GUID，不复制原包 meta。没有实际旧资产消费者，不增加 XF API 转发、资产转换器或猜测性的 `FormerlySerializedAs`。
- 不改 `ICoreResourceProvider`、Controller/Module 基础生命周期、Resource 请求/场景内核、全局事件内核或 Root；不新增产品 asmdef、System/Manager、计时器、Update 驱动、场景缓存或安装器。
- 所有 Unity 与请求操作在主线程；不通过后台任务访问 Unity 对象。保留 Unity fake-null 语义和既有异常契约，不用吞错、弱化断言或关闭诊断通过门禁。
- 不自动创建/切换分支、commit、push、merge 或清理工作区。若后续另获提交授权，按批次将源码、测试、meta、说明与验收记录一起中文提交，不为过程记录额外建提交。
- Unity 验证只使用该工程现有唯一实例；不替用户保存脏场景、不全局 SaveAssets、不覆盖现场。需要临时设置或独立接收工程写入时必须有相应授权并捕获/按所有权恢复；权限或环境不足记为阻塞。

## Review Focus

- 运行时 AddComponent、序列化 None 掩码与缺新字段的旧 Prefab：新组件易用，旧的显式 0 仍表示不接收。归 T1/T2。
- 物理过滤同时约束代码与 Inspector 通道，但不得改变八种指针消息、20 种原生 payload 或原有分发顺序。归 T2。
- provider 同步完成、同步异常及调用中退出/重入：不能留下重复键占位或把旧请求登记进新 Controller 会话。归 T3。
- 完成回调内再次加载、取消回调内新投递、同实例重新 AddController：旧清理不能摘除或取消新一轮责任。归 T3。
- Module 关闭、Single 原生操作已开始、其他 owner 的请求与成功场景：只结束本 Controller 的在途责任，不承诺物理回滚或隐式卸载。归 T3/T4。

## 1. 已核实依据

- 来源 `com.xfkj.xfgameframework.commonsystem` **1.0.3**，revision `fee858a64b73de70cea54d773970d9691c5f376c`，只读目录 `Library/PackageCache/com.xfkj.xfgameframework.commonsystem@fee858a64b/`；URL `https://gitee.com/xianfengkeji/xfgame-framework.common-system.git`。
- Runtime 只有 `Controllers/SceneController.cs` 和 `Listeners/{Animation,Collision,Trigger,Mono}EventListener.cs`。SceneController 依赖 XF Controller/XFAB；四个监听器是 GameObject 局部事件桥，无全局初始化、Update 或计时。
- 来源 `LICENSE:3` 为 MIT / `Copyright (c) 2025 弦风课堂`。YL 根 `LICENSE.md` 已含该版权及完整 MIT；实施只需补精确来源/用途说明，不重复创建许可文件。
- Assets 可读源码、asmdef 和序列化脚本 GUID 范围未发现直接使用该包；未解包外部 Bundle，不外推不存在外部消费者。游戏实际用 `Assets/Games/MainModule/Scripts/Controller/SceneController.cs:8-30`，带 LoadingPanel，与包内同名类不同，不修改它。
- YL 现有三种监听器在 `Runtime/Foundation/Events/Unity/`，namespace 为 `YLFramework.Core`；字段残留指向 CommonSystem 的 Obsolete/Header，UnityEvent 未默认初始化，物理掩码只在 Reset 赋全层。来源/自有 Animation 字段分别为 `onAnimationEvent` / `animationEvent`，不互相覆盖。
- `EventListener.cs:373-447` 有 12 个物理入口，但没有层过滤；`EventListenerEditor.cs:69-79` 会自动展示额外序列化属性，20 个事件/三个页签无需重建。
- `ICoreResourceProvider.cs:23-34,55-70` 已有带状态/进度/取消的场景请求，结果不授予卸载权；`CoreResourceProvider.cs:142-169` 明确终态后订阅可同步通知。`ControllerManager.cs:8-40,59-70` 允许外部实例注册，退出后可再次注册，不能假设 Controller 永不复用。
- 原 SceneController 按短名判已加载，异步跳过却返回 Abort 请求（来源 `SceneController.cs:69-82`）；只改方法名会留下错误语义及 XF 依赖。`SceneRequest.cs:16-17` 明确 Single 取消不能保证回滚；`SceneLease.cs:6-7` 明确 Dispose 不卸载。

## 2. 确定的功能边界

### 2.1 用户已确认

1. **SceneController 仅管理加载请求。** 不持有成功场景的卸载责任，不因 Controller/Module 关闭卸载成功场景；预加载、激活与显式卸载继续由现有 ResourceSystem/应用负责。
2. **采用明确的新请求语义。** 去掉来源同步 API、`force_loading` 和隐式已加载复用；同 Controller 的相同项目/地址在途时明确拒绝，不跨 Controller 合并；终态后再次调用就是新加载意图，允许有意的重复 Additive。

### 2.2 本方案纳入的修复与增强

| 能力 | 保留与修复 | 有明确收益的增强 |
|---|---|---|
| Animation | 保留 `YLFramework.Core.AnimationEventListener`、`animationEvent` 及 `OnAnimationEvent(string)`；去误导标记，新组件默认初始化事件 | 准确的中文用途/绑定说明，不扩大为 Animator 控制器 |
| Collision / Trigger | 保留各六个事件、对方 layer 过滤、原 `layerMask` 字段及 Reset；事件默认初始化，字段初始值为 AllLayers | 增加 `public LayerMask PhysicsLayerMask { get; set; }`，运行时无需反射即可配置 |
| Mono 生命周期 | 新增 `YLFramework.Events.MonoEventListener`，五个默认初始化的 UnityEvent：`onAwake/onStart/onEnable/onDisable/onDestroy` | 原生生命周期可在 Inspector 编排，不增加事件内核/宿主 |
| 通用 EventListener | 保留现有 20 个事件字段、注册 token、代码先于 Inspector 和异常传播契约 | 新增序列化 `physicsLayerMask` 及同名公共属性 `PhysicsLayerMask`；12 个物理消息可同时过滤代码/Inspector，八种指针消息不受影响 |
| 场景便利入口 | 新增下述 Core Controller，不保留 XFAB 返回类型或伪成功/伪取消兼容出口 | 实例级在途去重、批量取消、退出清理、同步完成/重入保护，直接利用原请求状态与进度 |

所有新/重置物理监听默认 `Physics.AllLayers`；显式 `0` 始终表示 None。新初始化不批量改写旧资源的 0、null 或持久调用。通用 EventListener 必须通过旧 schema Prefab 重导入证明缺字段仍为 AllLayers；失败就先解决可区分“缺字段”和“显式 None”的序列化初始化，不得用 OnValidate 将所有 0 改成全层。

过滤在一次物理回调入口执行一次，先于代码和 UnityEvent；对方 layer 或配置后续变化只影响下一回调，不建立 Enter/Exit 配对缓存、不合并 Stay。使用现有 `LayerMaskUtility.Contains`，不引入标签系统、逐帧扫描或分配；AllLayers 可短路，保持默认路径与现有 null-payload 桥测试语义。

不增加 RequireComponent 或禁止重复组件属性：2D/3D、父子刚体关系及不同层过滤实例有合法用途。旧专用监听器与通用 EventListener 保留不同工作流，不用自动挂两套组件实现兼容。

### 2.3 场景 Controller 合同

新增 `Runtime/Core/MVC/Controllers/SceneController.cs`，类型 `public sealed class YLFramework.Core.SceneController : Controller`，只使用 BCL、Unity Scene 类型和既有 Core 合同：

```csharp
public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(
    string address, LoadSceneMode mode = LoadSceneMode.Single);
public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(
    string projectId, string address, LoadSceneMode mode = LoadSceneMode.Single);
public void CancelPendingLoads();
public override void OnInit();
public override void OnExit();
```

- 默认重载使用当前 owner 的 `Module.ProjectName`；地址是 YL Catalog address/alias，不是自动转换的 XF 短名。不 trim/改大小写或猜已加载场景。
- Controller 必须经 Module 注册；新请求在未初始化、已退出、`IsExiting` 或 Module Closing/Closed/CloseFailed 时抛 `InvalidOperationException`。输入空白项目/地址和非法 mode 在调用后端前拒绝；分别为 `ArgumentException` / `ArgumentOutOfRangeException`。
- 每次取当前 `CoreResourceAccess.Provider`，不隐藏初始化或缓存后端；未绑定抛 `CoreResourceException(Unavailable)`。同实例 `(projectId,address)` 采用 Ordinal 在途键，mode 不拆键；重复抛 `CoreResourceException(InProgress)`，不调用 provider、不返回 null。不同 Controller 或不同项目/地址互不干扰；别名按提交字符串去重，不宣称解析为同一资产的别名也会合并。
- 直接返回 provider 的同一个 `CoreResourceRequest<CoreSceneResult>`，不创建第二种请求、不调用 TakeResult、不改写 Status/Progress/Error。调用方用 `using`、`yield`/Completed 和单次 TakeResult 消费；成功结果/终态请求由调用方收尾。
- Controller 对仍在途请求保留取消/释放权；调用方也可主动 Cancel/Dispose。完成 tracking 只解除登记/订阅，不 Dispose 已完成请求，防止抢走调用方领取机会。已终态/已 Dispose 项不得继续阻塞新投递。
- 在 provider 调用前登记一个**本实例私有占位**，防同步重入重复启动；返回后再填请求、订阅 tracking。订阅可以同步通知，因此先登记后订阅；完成先精确摘本条记录再允许用户观察，旧回调按记录身份不能删新键。
- OnInit 创建新注册代次并记录接受线程；provider 调用前后都验证代次/owner。调用中退出或同实例重新 AddController 时，迟到取得的旧请求只归还自身并以 InvalidOperationException 拒绝本次调用，不能登记进新代次。无静态表，无新会话 reset 钩子。
- `CancelPendingLoads()` 先分离调用时在途快照，再逐项 Cancel/Dispose 并解除 tracking；正常取消回调内新增的请求属于下一轮，不被旧清理取消。`OnExit()` 先关闭/失效本轮，再做相同收尾，拒绝退出回调中新投递；重复清理幂等。每项失败仍继续其余项，最后沿现有 Core 退出异常聚合路径报告，不仅日志后假装成功。
- 所有返回前异常/订阅失败都撤销自身占位并尝试释放已取得请求；保留原始异常和附加清理失败。provider 返回 null 抛 InvalidOperationException，作为后端合同错误，不静默回退。错误诊断带项目、地址、mode、owner。
- 取消/Dispose 只结束观察责任，不承诺撤销 native Single；成功场景不卸载，其他 owner 请求不取消。Completed 仍可能通知调用方取消终态，调用方必须检查 Status，不能把所有完成通知当成功。

### 2.4 不纳入

不引入场景栈、切换队列权威、自动加载屏、预加载/激活/卸载门面、按短名已加载复用、旧格式导入、动画状态机/参数控制、全局生命周期广播、Once/优先级/标签过滤。LoadingPanel 仍是业务 UI，轮询请求 Progress 即可，不拿 Panel 资源进度冒充场景进度。需要改变共享公共合同或出现真实旧资产消费者时，停止相应扩张并另行裁定。

## 3. 实施批次

以下路径省略 `Assets/YLFramework/` 前缀。新增文件/目录由 Unity 生成 meta。T1、T2、T3 为可独立验收的小批次，T4 统一收口；普通算法/内部私有记录由实施者依据合同选择，不机械照搬来源。

### T1: 专用监听器修复与生命周期组件

**Files:** 修改 `Runtime/Foundation/Events/Unity/{Animation,Collision,Trigger}EventListener.cs`；新增同目录 `MonoEventListener.cs`；扩展 `Tests/Core/EditMode/NeutralLayerMaskConsumerTests.cs`；新增 `Tests/PlayMode/CommonSystem/{AnimationEventListener,MonoEventListener}PlayModeTests.cs`，namespace `YLFramework.Tests.CommonSystem`，自有数据归 `Tests/Fixtures/CommonSystem/`。

- [x] 先补可复现断言：运行时 AddComponent 的物理掩码为 AllLayers、13 个原事件对象可直接 AddListener、新公共掩码可写；旧显式 0、字段/类型/GUID 和 Animation 字符串方法不变。记录默认值的真实失败，不把新增类型造成的首次编译缺失写成 Bug 复现。
- [x] 就地去掉指向 CommonSystem 的 Obsolete/Header，初始化事件/掩码，增加两个 PhysicsLayerMask 属性及简洁用途/2D-3D共享说明；仅清理由本次导致冗余的相关 pragma。不改原事件字段名或事件负载类。
- [x] 新 Mono 只用五个原生私有回调转发五个 UnityEvent；放入 YLFramework.Events，菜单/Tooltip 说明局部对象所有权。不得为补 Awake 观察而强制激活用户对象；运行时 AddComponent 后再绑定本来就可能错过 Awake/OnEnable，应说明使用预配置/未激活 Prefab。
- [x] 用自有 inactive 对象预配置回调后激活，验证 Awake -> OnEnable -> Start、再次 Enable 不重复 Awake/Start、Disable/Destroy、事件内合法停用/销毁；保持从未激活对象的 Unity 原生 OnDestroy 限制，不伪造生命周期。
- [x] 为 Animation 生成自有 Clip/AnimatorController 夹具并实际播放，断言事件经 `OnAnimationEvent(string)` 到达持久 UnityEvent 的目标且字符串准确；测试夹具创建的 UnityEditor API 保持 Editor/条件编译隔离。直接调用桥方法的 EditMode 测试与此引擎证据分开记录，不把状态变化或手动 SendMessage 冒充 Clip 事件。
- [x] 运行 `NeutralLayerMaskConsumerTests` 与两个新增 PlayMode 类，记录实际结果；确认三个旧脚本/meta身份和持久事件数据未变。

### T2: 通用事件组件物理过滤

**Files:** 修改 `Runtime/Foundation/Events/Unity/EventListener.cs`；扩展 `Tests/EditMode/UnityEventListenerContractTests.cs`、`Tests/EditMode/Editor/Events/EventListenerEditorTests.cs`；新增 `Tests/PlayMode/CommonSystem/UnityPhysicsListenerPlayModeTests.cs`；自有数据归 `Tests/Fixtures/CommonSystem/`。现有 Editor 自动展示额外字段，优先不改实现。

- [x] 在增字段之前用现有脚本生成/保存自有旧 schema Prefab（含持久事件、原 GUID），留作真正的重导入输入；补默认 AllLayers、None、含/排除层 0/31、指针不受影响和旧数据保持断言，先读实际失败。
- [x] 增加 `physicsLayerMask = Physics.AllLayers` 和 PhysicsLayerMask 属性；对全部 12 个物理入口先过滤，再沿原代码/Inspector顺序分发。复用 LayerMaskUtility，八个指针入口、事件枚举、清理及异常策略不改。
- [x] 调整 Inspector 测试时仍精确锁定原 20 个事件字段、8/6/6 分组、标签、payload 和旧顺序；额外断言新掩码只显示一次，派生 extraValue 仍可编辑，多选、Undo/Redo、Prefab override/apply/revert均保持。不要简单把20改21后放宽事件覆盖断言，也不要按额外属性数组下标误改掩码。
- [x] 在自有局部 3D/2D PhysicsScene 中覆盖真实 Collision/Trigger Enter/Stay/Exit，断言过滤同时约束代码和持久 UnityEvent；同次回调改mask只影响后续消息/严格代码优先由独立EditMode真实Trigger负载用例验证，不声称在Native Collision中另测了全部组合。复用局部物理测试模式，不改工程全局碰撞矩阵/模拟设置，不用全为 null 的反射调用替代物理证据。
- [x] 运行两组既有契约/Inspector类及新物理 PlayMode 类；重导入旧 Prefab 验证缺字段为 AllLayers、显式 None 保存重开仍为0、原 UnityEvent目标/方法/参数不变。实际打开 Inspector 检查字段和页签，视觉截图与自动化编辑操作分别记录，不称全量人工 GUI 验收。

### T3: Core 场景便利 Controller

**Files:** 新增 `Runtime/Core/MVC/Controllers/SceneController.cs`；新增 `Tests/PlayMode/CommonSystem/SceneControllerPlayModeTests.cs`（含最小私有 fake provider/request），共享自有 `Tests/Fixtures/CommonSystem/`。不修改 Module、CoreResourceAccess 或 Resource 实现。

- [x] 先按 §2.3 固定真实 API 与断言，用 fake provider 控制 Pending/同步终态/异常/重入，并通过 Module 正式注册。断言请求引用相同、默认/显式 project与mode传递、参数/未绑定错误、重复键后端调用次数不增加、失败不留占位、不同owner互不影响。
- [x] 实现两个 LoadSceneAsync、CancelPendingLoads 及 OnInit/OnExit；主线程检查先于字典/Unity访问。使用本地私有记录和注册代次，provider 调用前占位，tracking不领取结果，不创建新请求、后台任务、轮询驱动或成功场景表。
- [x] 专门验证：返回前已完成；provider 内重入相同地址；provider 中关闭并重注册同实例；Completed 中重载同键/关闭；Cancel/Dispose/Invalidated后重载；批量取消回调新增；退出回调拒绝新投递；单项清理抛错仍清余项并报告原因。旧记录不得删除/取消新代次责任。
- [x] 用真实 ResourceSystem + Core provider 跑 Single/Additive、失败地址、Progress/单次TakeResult、完成前关闭、已成功后关闭；Single复用既有Core安全harness，其余使用自有两个空场景。断言后端原生语义不被包装改写，成功Scene仍加载且其他owner仍可用。真实Scene卸载由测试实际owner通过Resource完成，不要求最后场景强制卸载。
- [x] 运行新 Controller PlayMode 类及现有 CoreResourcePlayModeTests 的场景/终态/Module关闭代表方法；重跑 FoundationBoundaryTests 和 CoreArchitectureBoundaryTests，确认新Core源码不解析到Systems.Resource或XF。错误只在本薄层修复，不顺手改Resource历史行为。

### T4: 集成、说明与交付状态

**Files:** 增量更新现有 `Documentation/{Architecture,IntegrationPlan}.md`、`Documentation/Modules/{Events,Resource}.md`、`Runtime/THIRD-PARTY-NOTICES.md` 和本 Plan；根 `LICENSE.md` 只补精确CommonSystem来源说明，保留现有MIT全文/版权。不新建重复功能说明、审计日志或通用验收平台。

- [x] 文档说明新namespace、五事件、Inspector绑定时机、PhysicsLayerMask/None、旧组件与EventListener选择、场景provider装配、地址与状态/进度使用、请求所有权、退出/取消与不卸载边界；使用完整限定 `YLFramework.Core.SceneController` 避免与游戏全局同名类混淆。
- [x] 来源登记精确到 CommonSystem 1.0.3/revision/URL/MIT-2025，区分三种旧YL能力融合和新Mono/场景用途承接；不宣称复制/修改原包。蓝图补充本专项入口，不把原13项计数或旧交付记录改成包含本包。
- [x] 执行 §4 必做门禁，按本批实际源码记录编译、测试、GUI与保护范围结果；检查所有新增/meta、diff、受保护文件和默认Exporter递归归属，Tests不进入产品。完成一次独立代码Review，关注本方案Review Focus及可读性，不为美学扩张重构。
- [x] 本批选择保留独立交付扩展门未执行状态，不借用历史Player或编译结果代替。最终交接标记“主工程完成、独立交付未验证”，不自动Git收口。

## 4. 验证与完成标准

**必做主工程门禁：**

| 门禁 | 可操作的通过条件 |
|---|---|
| G0 范围/静态 | 六个相关Runtime文件与测试/meta差异可解释；既有GUID/字段保持；Foundation/Core闭包在Editor和Windows64 Development两profile通过；无新增XF/XFAB/Editor反向依赖，无新产品asmdef或Root改动 |
| G1 Unity编译/定向测试 | 当前唯一Editor真实编译无失败；T1-T3新用例和上述既有直接消费者通过；完成通知/异常收尾/取消及退出重入均有真实结果，不以辅助编译代替 |
| G2 真实引擎/作者流程 | 新Mono原生顺序、真实2D/3D物理过滤、AnimationClip的`OnAnimationEvent(string)`接收、Single/Additive请求与Scene责任、旧Prefab重导入及Inspector Undo/持久调用通过；自有夹具和临时设置按所有权恢复 |
| G3 共存/保护 | 原游戏仍解析自己的SceneController；原XF包、MainModule资源链、配置与开工用户改动没有本次新增修改；按改动影响执行现有原游戏启动/场景切换代表检查，不重建游戏Bundle |

**独立交付扩展门（推荐后续收口，验收范围未获用户选择，不自动执行）：** 正式Exporter导出并核清单/许可，在不提供原XF生态的自有接收工程先product-first编译，再安装独占消费者；Windows64 Mono与IL2CPP Low代表性运行持久UnityEvent、物理过滤、场景请求/关闭，核BuildReport/原生退出/日志/源码和产物身份。复用既有Stage9接收、设置保护和证据算法，但使用CommonSystem自有owner/输出，不改写Equipment等历史工具或证据。未确认这一级范围/权限时明确留待后续，不据主工程通过宣称完成无XF或Player交付；不外推High stripping、移动/WebGL或全量人工验收。

**执行入口：** 使用项目现有 `tests-run`，按实际程序集/类筛选。例如工具输入 `{"testMode":"PlayMode","testAssembly":"YLFramework.Tests.PlayMode","testNamespace":"YLFramework.Tests.CommonSystem","includePassingTests":true}`；既有Core/Foundation类按各自测试程序集运行。可用CLI为 `unity-mcp-cli run-tool tests-run --input-file <参数文件>`，参数文件仅在获准的自有临时目录；运行前确认无脏场景，读取最终逐项结果，不把Processing/发现数量当通过，不因运行受阻安装工具或修改权限配置。

**完成口径：** 五项用途各有唯一归属；T1-T4与G0-G3真实通过才标“CommonSystem迁移主工程技术完成”。独立交付另按扩展门实证标记；用户验收、Git提交/推送分别记录，不自动代签。若发现需改共享Core/Resource契约、未知旧资产或保护现场冲突，暂停对应部分并明确最小未决项，不恢复原force/同步接口来掩盖问题。

## 5. 实施记录

- Pre-flight: T1/T2 共享物理掩码默认值和局部事件约定，T3 只消费既有 Core 资源合同，未发现接口冲突；按 T1 -> T2 -> T3 -> T4 执行。
- Ruling: 以本 Plan 的 §5 作为执行台账，不另建 SDD 工作区、分支或 commit；当前非 main 的既有集成工作区与唯一 Unity 实例已明确，避免未经授权的 Git/忽略规则变更。
- 开工 Unity：project=`G:/Unity/Projects/CGJ2024`，PID=16464，2022.3.62f3c1，非 Play/编译/更新，无编译失败；`UIAcceptance.unity` 已保存且 clean；EnterPlayModeOptionsEnabled=false，options=3。未修改这些设置。
- 受保护 tracked diff 初始指纹：`88aaa9b9e29303340cfcc847e30b23ba8b029195`（`git diff --binary` 覆盖旧生态 Plan、.mcp.json、Packages、Assets/Games、ProjectSettings 后经 git hash-object）。未跟踪用户项另按开工 status 保持。
- T1 基线：`NeutralLayerMaskConsumerTests` 3/3 Passed（Unity EditMode）；新增回归先于产品修改。
- T1 complete: PlayMode RED 0/3（两项缺新组件，一项真实运行时 mask=0）；最小修复后 PlayMode 4/4、Core EditMode 4/4 Passed，含原生 Animator Clip -> 持久 Text.set_text 及 Mono 生命周期。Animator 首次求值需先 Update(0)，错误的数字式 LayerMask JSON 夹具已改用真实 Unity 序列化往返；两者均为测试前提修正，未据此改产品行为。
- T2 旧 schema 输入已在加字段前由 Unity 创建：`Tests/Fixtures/CommonSystem/EventListener.Legacy.prefab`，GUID `fc7cb21d53b0a5140a3a90e90798cb76`，原20事件字段、持久 SetActive(false) 目标；只在自有临时附加场景创建并关闭，未保存用户场景。
- T2 技术验收：EditMode RED 7/12、Native Physics RED 0/4（缺新字段/API）；增加单一掩码后 EditMode 12/12、局部2D/3D Native Physics 4/4 Passed。覆盖代码/Inspector共同过滤、同次回调改变掩码只影响下一次、20事件原payload/顺序、Undo多选、Prefab override/apply/revert、旧schema全层默认和显式None往返。测试夹具修正包括分离2D/3D Collider、泛型使用原生基类payload、按Unity要求Update后读Prefab override，以及每种掩码独立接触对；未据夹具错误修改产品合同。
- T2 GUI 视觉门尚待T4：实际打开并重绘了自有Prefab的原生Inspector，但ReadScreenPixel捕获到VS Code前台而非Unity，不能算视觉通过。临时Selection、折叠/Tab SessionState及窗口焦点已在finally恢复；不把SerializedObject/反射断言替代GUI验收。
- T3 complete: 新类型缺失的编译RED已观察（不是既有产品Bug复现）；实现后新SceneController PlayMode 23/23 Passed，含真实Resource的重复Additive、取消后独立owner和失败地址。Core场景/终态/关闭代表回归11/11 Passed（含新Controller Single真实场景用例）；Foundation/Core闭包与两profile/负控制12/12 Passed。
- Ruling: Single引擎用例追加到既有`CoreResourcePlayModeTests`并复用其安全Single harness/恢复逻辑，而非重复建设接管TestRunner场景的设施；新CommonSystem的空Target/Neutral与ResourceProject只供自有Additive/失败夹具。代价是新增一处直接消费者测试文件，边界不变，现有原测试与用户场景不改。
- T4 GUI完成：原生Inspector的Collision页/三页签/Drawer以及折叠后的Physics Layer Mask=Everything已实际截图并目视核对。脚本用Win32临时置顶**同一Unity窗口**并确认可见像素归PID16464，未输入鼠标/键盘或改资产，finally恢复置顶状态、Selection、Editor焦点及折叠/Tab。可读截图：`%TEMP%/kilo/CommonSystem-Inspector-Verified-128ad30c136b4f10a1543b20b76eb5c2.png` 和 `CommonSystem-Inspector-Mask-bca9e409ebab4b7b98925852fabfbcf5.png`；最初VSCode/黑图不计证据。Undo/多选/Prefab apply/revert由真实SerializedObject自动化验证，不宣称全量手工操作。
- T4独立首次Review：最初审查上下文连续连接重置，已保留其board实质发现；由新上下文接替完成同一次未结束的只读Review，非修复后反复复审。最终无Critical或已确认产品功能Important；1项Important为新场景测试的超时隔离，另有文档示例编译和清理诊断上下文遗漏。旧`Runtime/Core`及资源内核不改。
- Ruling: 将“用户接入示例无法编译”和“批量清理错误缺合同要求的地址上下文”纳入同一次实质修复pass，而非仅作为文档/诊断Minor延期；前者真实编译RED `CS0019`后通过，后者新断言确认Data为null后通过。修改保留原异常引用/类型，只补Data；代价是三处catch补上下文，避免多地址清理失败不可定位。
- Review修复：`TimeoutRequestsIsolationAbortBeforeReportingFailure`、`TimedOutTeardownDoesNotTouchTheAuthorityBeingAborted`、`CleanupFailuresDoNotSkipLaterRequestsOrHideTheirCauses` 实际RED 0/3 -> GREEN 3/3；SetUp重置超时/退出动作，超时先请求结束隔离Play再断言，TearDown不继续触碰被终止会话。超时自测截获测试专属退出动作，不故意遗留真实native任务；退出策略沿用既有Core测试。无未解决Review findings或延期Minor。
- 最终组合回归（2026-10-21）：Unity EditMode **28/28 Passed**（Boundary12+CoreMask4+Listener/Inspector12）；PlayMode **59/59 Passed**（CommonSystem33+EventsLifecycle14+Core场景/终态11+原游戏非干扰1），Failed/Skipped均0。原游戏代表覆盖菜单/设置/暂停/返回和两次进入Game，使用原包/现有Bundle，不重建、不改原游戏。
- 默认Exporter仅做只读根归属校验：原7个根包含新Mono/SceneController及根LICENSE、Runtime通知；6个CommonSystem夹具路径均排除，无实际Export、独立工程导入或Player构建。所有13个新资产/脚本/目录身份有meta且GUID非空/互异；既有脚本meta未改。`git diff --check`通过。
- 用户验收（2026-10-21）：用户正式确认 CommonSystem 五项能力主工程技术验收通过，授权仅围绕本专项完成文档状态、中文 Commit 与普通 Push；未扩大开发或测试范围。
- 保护终态：tracked保护差异指纹仍为`88aaa9b9e29303340cfcc847e30b23ba8b029195`，与开工一致；原未跟踪用户项保留。Unity仍PID16464、2022.3.62f3c1，非Play/编译且无编译失败，原`UIAcceptance.unity` clean/active、Selection为空，EnterPlayModeOptionsEnabled=false/options=3，dirtyNativeAssets为空。未新开Unity或分支，未修改Packages/ProjectSettings/原包；本轮随后执行已授权的 CommonSystem 精确暂存与普通 Push。
- 完成边界：T1-T4及G0-G3主工程技术完成，用户已确认通过，Git 收口完成；无XF独立首导入、Mono/IL2CPP Player、High/移动平台与全量人工图形验收仍未执行。Review没有另行证明所有取消/重新注册组合的笛卡尔积；已测核心重入与所有权边界，不外推形式化/穷举保证。
