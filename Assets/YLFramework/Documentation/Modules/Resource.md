# Resource（资源加载与 AssetBundle 管理）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

框架的资源服务：通过 `ResLoader` 统一同步 / 异步加载资源，维护资源引用计数与卸载；提供 AssetBundle 的编辑器模拟与运行时加载、Resources 回退实现，以及热更新流程的骨架。

## 位置

- 运行时：[`Runtime/Resource/`](../../Runtime/Resource/)
  - 核心：[`Framework/`](../../Runtime/Resource/Framework/)（加载器、资源表、工厂）
  - 实现：[`Implementations/`](../../Runtime/Resource/Implementations/)（AssetBundle / Resources / 本地与网络图片）
  - 热更新：[`HotUpdate/`](../../Runtime/Resource/HotUpdate/)
  - 工具：[`Utilities/`](../../Runtime/Resource/Utilities/)
- 编辑器工具：[`Editor/Resource/`](../../Editor/Resource/)

## 主要功能

- 资源服务入口：`ResManager.Init()` / `InitAsync()`（编辑器下可按模拟模式自动初始化）。
- 加载器 `ResLoader`：`Allocate()` / `RecycleToCache()` 池化复用；同步 / 异步加载、预加载回调、按资源名或 Bundle 释放、卸载实例化对象。
- 资源定位：`ResKey`（资源名 / 所属 Bundle / 类型，资产名统一小写）、`ResTable`（按名与类型查询）、`ResFactory`（按创建器匹配并创建资源）。
- 资源对象：`IRes` / `Res`（引用计数 `SimpleRc`、加载状态、卸载）。
- AssetBundle 支持：模拟模式（编辑器，`AssetBundleHelper.SimulationMode`）、资源标记菜单、打包与资源数据生成；运行时按平台从 StreamingAssets / 持久化目录读取并加载。
- 其他实现：Resources 加载（`ResourcesRes`）、本地 / 网络图片资源（`LocalImageRes`、`NetImageRes`）、场景加载（同步 / 异步，经 `ResLoaderExtensions`）。
- 工具：`GameObjectLoader`（对象实例加载）、`SubProject`（子工程配置 ScriptableObject）、`ZipFileHelper`（在 Android / iOS 上读取包内或 StreamingAssets 中的文件）、`BinarySerializer`、`PlatformCheck`、`ResUnloadHelper`。
- 热更新流程骨架：`HotUpdateManager`（检查本地版本 / 远端新版本 / 更新资源的状态流程）、`HotUpdateConfig`、开发用 `FakeResServer`（本地模拟远端）。

## 主要类型

- `ResManager`：资源服务入口（`Singleton<ResManager>`）。
- `ResLoader` / `IResLoader` / `ResLoaderExtensions`：加载器与扩展方法。
- `ResKey` / `ResTable` / `ResFactory` / `IResCreator`：资源定位与创建。
- `IRes` / `Res` / `ResState` / `ResLoadType`：资源对象与状态。
- `IRefCounter` / `SimpleRc`：引用计数。
- `AssetBundleHelper` / `AssetBundleSetting` / `ConfigFileUtility` / `ResData` / `AssetData` / `AssetDataGroup` / `AssetDataTable`：AssetBundle 支持。
- `GameObjectLoader` / `SubProject` / `ZipFileHelper`：辅助。
- `HotUpdateManager` / `HotUpdateConfig` / `FakeResServer` / `ResVersion`：热更新。
- 编辑器：`ResServiceEditorWindow`（菜单 `YLFramework/Service/ResService`）、`ResServiceEditorAPI`（模拟模式 / 打包 API）、`ResServiceAssetsMenu`（资源标记）、`AssetBundleExporter`、`BuildScript`、`ResDataCodeGenerator`、`SubProjectData`。

## 核心运行时契约（第一阶段整理后）

- **ResKey**：查询规则对象。`AssetName` / `OwnerBundleName` 创建时统一转小写（`ToLowerInvariant`），`OriginalAssetName` 保留原始大小写；`assetName` 为 null 抛 `ArgumentNullException`、空白抛 `ArgumentException`。`Match(IRes)` 是唯一的值匹配：资源名必须相等（资源侧也须是已归一化的小写名）、`AssetType` / `OwnerBundleName` 为 null 表示不过滤。未重写 `Equals` / `GetHashCode`，保持引用语义，不应作为哈希容器 key；使用后必须 `RecycleToCache()` 归还对象池，重复回收由对象池拒绝。
- **引用计数（SimpleRc / IRefCounter）**：初始值为 0；Release 到 0 只触发 `OnZeroRef`（是否卸载由子类决定）；计数为 0 时再次 Release 属非法状态，直接抛 `InvalidOperationException`（fail-fast，不静默变负）。
- **IRes / Res 生命周期**：Waiting（等待 / 上次失败）→ Loading → Ready；失败回到 Waiting 并以 false 通知回调；`RegisterOnResLoadDoneEvent` 在 Ready 时立即回调，否则加载结束时回调一次（事件随后清空）。`ReleaseRes()` 卸载缓存资产并回 Waiting（Loading 时返回 false，幂等）；`OnZeroRef` 在 Ready 时立即 `ReleaseRes`，Loading 时交给 ServiceManager 在加载完成后回收。`Asset` 是缓存资产本体，不代表调用方 Instantiate 的实例。
- **ResLoader ownership**：
  - 每个 Loader 对 `_recordedRes` 中每个资源只 Retain 一次；同一资源重复 `Load` / `AddToPreLoad` 只追加监听，不重复计数。
  - `ReleaseRes` / `ReleaseAllRes` 只释放自己记录过的资源，重复释放安全（不会二次扣减）。
  - `RecycleToCache` 先销毁 `AddObjectToDestroyWhenRecycle` 登记的对象，再回池；回池触发 `OnRecycle → ReleaseAllRes`，释放全部引用并清空内部状态，复用时无上一轮残留。
  - `UnloadAllInstantiateRes` 卸载 `UnloadInstantiate` 返回 true 的资源并释放持有；未 Ready 资源同步修正预加载队列与计数。
  - 传入的 `ResKey` 由调用方回收（Loader / 扩展不消费 key）。
- **ResFactory 选择规则**：按注册顺序，第一个 Match 命中且 Create 返回非 null 的创建器胜出；匹配但返回 null 时继续尝试后续匹配者；全部失败记录错误并返回 null；创建器异常原样传播。`AddResCreator` 追加到末尾，先注册者优先。
- **ResManager / ServiceManager 初始化**：`Init` / `InitAsync` 幂等，完成标志只在初始化成功后提交，失败可重试；不承诺并发初始化安全；仅运行时可用（编辑器非播放模式无法创建 Mono 单例，因此核心加载流程测试位于 PlayMode）。
- **ResourcesRes**：`resources://` / `resources/` 前缀；同步 / 异步失败（不存在或取回 null 资产）都会回 Waiting 并通知 false，不会以 null 资产进入 Ready；卸载走 `ResUnloadHelper`：GameObject 不调用 `Resources.UnloadAsset`（Unity 不允许），仅清空引用，Texture / AudioClip / ScriptableObject / TextAsset 等走 `Resources.UnloadAsset`。

## AssetBundle Runtime 契约（第二阶段）

- **三层 ownership**：
  1. `ResLoader` 对整条依赖链上的每个 IRes 各 Retain 一次（详见 `AddToPreLoad` 递归）；
  2. `AssetRes` 对所属 Bundle 只在自身加载期间通过 `HoldDependRes` / `UnHoldDependRes` 暂时持有（`GetDependBundleList` 返回所属 Bundle）；
  3. `AssetBundleRes` 持有 `UnityEngine.AssetBundle`，引用计数归零时按历史语义 `AssetBundle.Unload(_unloadFlag)`（默认 `Unload(true)`；`UnloadInstantiate` 可改写 flag，实例 ownership 属于后续阶段）。
  Bundle 之间的依赖不单独持有：依赖节点的加载与持有统一由 Loader 完成（同步按依赖优先顺序，异步以 `DependResLoadIsFinish` 门控）。
- **依赖失败**：任一依赖加载失败（`Waiting` 且已离开等待队列）时，Loader 会把阻塞中的宿主资源级联置为失败（回 Waiting、通知 false），不会永久挂在等待队列；只影响本次加载，重试需 `ReleaseRes` 后重新加载。
- **配置路径**：Bundle 文件 persistentDataPath 优先、StreamingAssets 回退；配置表路径由 `AssetBundleSetting.GetAssetBundleConfigFilePath` 生成（含平台目录与 `PathPrefix`）。
- **配置查询**：`AssetDataTable` 按资源名（`ToLowerInvariant`）索引，按类型码与 OwnerBundleName 过滤；类型码无精确匹配时保留同名条目（旧数据兼容），因此 `AssetResCreator` 显式拒绝 `typeof(AssetBundle)` 的 key，Bundle 永远由 `AssetBundleResCreator` 处理。
- **失败回滚**：Bundle/资产加载失败（文件缺失、`request.assetBundle == null`、`abRequest.asset == null`）都会回 Waiting 并通知 false，不会以 null Asset 进入 Ready；加载异常会记录并按失败处理（依赖持有通过 try/finally 严格成对释放）。
- **sync/async 混合**：同一 key 只会有一个 IRes；异步加载中调用同步加载不会重复加载，同步调用立即返回当前状态（可能是 `Loading` + `Asset == null`）；同步已 Ready 后再异步请求会立即完成回调。
- **Simulation / Runtime 语义**：模拟模式不读取真实 Bundle 文件（Bundle 节点空转后 Ready），但依赖加载顺序、引用计数与失败语义与运行时一致；差异仅在资产的读取方式（AssetDatabase vs `AssetBundle.LoadAsset`）。

## Unity Object Layer 契约（第三阶段）

三类 Unity 对象 ownership 严格区分，不能混用同一套 Unload：

- **A. Asset（缓存资产）**：由 IRes 持有，零引用时卸载。AssetBundle 走 `AssetBundle.Unload(_unloadFlag)`；Resources 资产走 `Resources.UnloadAsset`（GameObject 除外）；运行时创建的 Texture（Local/NetImage）走 `Object.Destroy`。
- **B. Instance（Instantiate 实例）**：属于调用方。`GameObjectLoader` / UI 窗口加载器 / `ResLoader` 都不会自动销毁实例；只有显式登记到 `ResLoader.AddObjectToDestroyWhenRecycle` 的对象才会在 `ResLoader.RecycleToCache()` 时被销毁（null 忽略、重复登记去重、外部已销毁的对象安全跳过；`ReleaseAllRes` 不销毁该列表）。
- **C. Scene**：由 SceneManager 拥有。`AssetBundleSceneRes` 只是"场景 Bundle 已就绪"的门控，不保存 Scene、不调用 SceneManager；释放 SceneRes / 回收 Loader **不会卸载已加载的 Scene**，卸载必须显式调用 `SceneManager.UnloadSceneAsync`。

- **Scene 加载**：`LoadSceneSync` / `LoadSceneAsync` 先等待场景 Bundle（模拟模式经 AssetDatabase 定位 Scene 资产），成功后才交给 SceneManager；Bundle 失败时记录错误并直接返回，不会调用 SceneManager，也不会让 Loader 永久挂起（依赖失败会级联到 SceneRes 并回调 false）。
- **UnloadInstantiate / _unloadFlag**：`AssetBundleRes.UnloadInstantiate(flag)` 仅在 Bundle 已加载时把 `flag` 记为**当前一轮加载**的 `AssetBundle.Unload(bool)` 参数（默认 true）。新一轮同步/异步加载与对象池回收都会把它重置为 true，多 Loader 不共享该参数。
- **Loader 回收顺序**：`RecycleToCache` 先销毁 destroy-on-recycle 列表（列表先脱钩，Destroy 触发的 OnDestroy 回调重入安全），再回池 → `OnRecycle` → `ReleaseAllRes` 释放全部资源引用。`GameObjectLoader` 回池时释放内部 ResLoader 并断开引用，复用无残留。
- **LocalImageRes / NetImageRes**：AssetName 为 ResKey 归一后的小写名，真实文件路径 / URL 取自 `OriginalAssetName`（保留大小写）；失败（文件/URL 无效、解码失败、零引用）统一回 Waiting + false 通知，不会 Ready + null；WWW 在所有路径 Dispose；回池时调用 base 清理并重置路径字段；运行时 Texture 在零引用时 Destroy。
- **zero-ref while Loading**：Scene / GameObject / Image 与核心契约一致——Loading 中归零不打断请求，完成后走统一失败/清理路径（回 Waiting、Asset null、由 ServiceManager 回收）。

## 真实 AssetBundle 端到端验证（第四阶段）

- **环境与资产**：PlayMode E2E 使用 `IPrebuildSetup`（`AssetBundleE2EPrebuild`）在进入 PlayMode 前，用项目现有 `BuildPipeline` + `ConfigFileUtility` + 路径规则构建最少测试 Bundle，产物写入 persistentDataPath（验证 persistent 优先规则）；复用工程内 `ExampleWindow.prefab`（主 Bundle）与其一个真实资源依赖（dep Bundle）、`SampleScene`（场景 Bundle）和专用文本资产。`IPostBuildCleanup` 撤销标记并删除/还原产物与空目录；不写入 StreamingAssets、不修改 BuildSettings。
- **已验证**：普通 TextAsset 同步/异步加载、卸载后重载；真实 dependency 的 RefCount 成对、不提前卸载；sync/async 同 key 共享同一 IRes；Prefab 加载 / Instantiate / destroy-on-recycle 与 `Unload(false)` 下实例存活；Scene Sync/Async Additive 真实加载 + 显式卸载（Release 不卸载 Scene）；失败链路（缺 Bundle / asset 名错 / Scene 名错）不挂起且可恢复；Simulation 与 Runtime 业务契约一致。
- **本阶段修复**：`Res.OnReleaseRes` 在 Asset 已被 Bundle `Unload(true)` 销毁（fake-null）时会跳过清空字段，导致对象池复用的 IRes 残留已销毁引用；现无条件清空，并同步加固 `AssetBundleRes` / `LocalImageRes` / `NetImageRes` 的卸载字段清理。
- **默认 Unload(true) 语义**：会同时销毁 Bundle 内已加载对象（含未登记的实例）；需要实例跨释放存活必须先用 `UnloadAllInstantiateRes(false)`，真实产物测试已锁定该行为。

## 依赖

- `ZipTool`（ZipFileHelper）、`Table`（ResTable）、`Extensions`、`Pool`、`Singleton`。
- 被 `UI` 依赖：`WindowLoaderPoolBaseOnRes` 默认通过 `ResLoader` 加载窗口预制体。

## 当前状态

核心运行时与 AssetBundle runtime 已完成前两阶段整理与测试锁定；热更新相关仍待完善：

- 第一阶段测试覆盖：`ResKey`、`SimpleRc`、`Res` 状态机、`ResTable`、`ResFactory`（EditMode），`ResLoader` ownership / 多 Loader 共享 / 异步回调 / 零引用回收、`ResourcesRes`（PlayMode）。
- 第二阶段测试覆盖：AssetBundle 配置查询与 Creator 路由（EditMode），真实 `AssetBundleRes` 依赖图（多层 / 共享 / 菱形）、依赖失败级联、sync/async 混合、Loader 回程清理、回调重入、缺失 Bundle 失败（PlayMode）。
- 第三阶段测试覆盖：SceneRes 门控与依赖失败不挂起、Scene 扩展失败路径（PlayMode）；GameObjectLoader 失败/复用/destroy-on-recycle/UI 窗口加载适配器（PlayMode）；Local/NetImage 真实文件成功/缺失/解码失败/零引用/池复用（PlayMode）；图片路径大小写与池重置、SceneRes 缺配置失败（EditMode）。
- 第四阶段测试覆盖：真实 AssetBundle 产物的端到端链路（构建/配置读取、普通资产、真实 dependency、Prefab 实例 ownership、Scene Additive、unload/reload、失败恢复、Simulation/Runtime 对照）；全量 EditMode **987/987**、PlayMode **125/125** 通过。
- `ServiceManager` 中"对象池初始化"TODO 已核实为过期注释（对象池初始化本已在同一方法内实现），第一阶段清理并合并了同步 / 异步初始化路径；初始化完成标志改为成功后提交。异步初始化配置路径的拼接缺陷（`List<string>` 与字符串相加）已在第二阶段修复。
- `HotUpdateConfig` 为空类，热更新流程未完整接入（`HotUpdateManager` 仅有流程骨架；`FakeResServer` 为开发用假服务器）——**未完成 / 后续专项**。
- 前四阶段未做：AssetBundle 打包与依赖生成流程改造、目录迁移。Scene / GameObject / Image 的 Unity Object 生命周期已纳入并测试锁定；真实产物链路已验证。
- 已知限制：运行时资源名 / Bundle 名需为小写；`ObjectAssetTypeCode` 未覆盖的自定义资产类型在配置查询时会走同名回退（建议显式传 Type）；子工程数据表（`SubProjectAssetBundleDataFiles`）只参与 Creator 匹配，运行时不加载其依赖信息；`AssetBundleSceneRes` 的运行时加载依赖场景已加入 BuildSettings（历史设计）。

## 备注

- 资源名在 `ResKey` 中统一转小写，`OriginalAssetName` 保留原名。
- 编辑器可通过模拟模式直接按 AssetBundle 名加载工程内资源，无需真实打包。
- `FakeResServer` 的远端目录指向 `Runtime/Resource/HotUpdate/Remote/`（工程内当前没有该目录）。
