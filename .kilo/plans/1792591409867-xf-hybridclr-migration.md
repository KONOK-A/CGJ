# XF HybridCLR 迁入 YL Implementation Plan

> **2026-10-22 增量：项目子集、共享基座、启动进度、EditorFast 与窗口优化技术实施完成，结果见 §9；最终 review 与 Git 收口按用户新授权纳入本提交，范围与更新证据见 §10。** 下方 T1-T6/G0-G4 和 §7/§8 为上一版迁移及 Git 收口事实，不代替本轮结果。

> **状态：T1-T6 实施及 G0-G4 实际门禁完成，Git 收口按 2026-10-22 用户明确授权纳入本批，范围见 §8。** 最终技术结果及交付边界见 §7；§6 的早期“当前工作点/待验证”与未提交记录是当时事实，不代表最终状态。Windows64 IL2CPP Development/Release Low 已运行，同一 Player 的签名 v2 与拒绝/重启门禁已验证；不外推 High stripping、其他平台或任意动态泛型。
>
> **For agentic workers:** 按任务使用 `executing-plans` 或 `subagent-driven-development`；先读本文件及相关源码，逐项勾选验收。不得把规划批准自动扩大为 Git 提交、Push、平台扩展或业务迁移授权。

**Goal:** 将 XF HybridCLR 1.1.3 的有效能力融合为 YL 原生、无 XF/XFAB 硬依赖的正式可选热更集成，以官方 HybridCLR 9.0.3 完成 Windows64 IL2CPP 的构建、发布、加载和 YL Module 启动闭环。

**Architecture:** 新增 `Integrations/HybridCLR` 的独立 Runtime/Editor 叶子程序集。复用 YL Resource 的 TextAsset 租约、发布/签名/更新事务和 Core 的 ModuleManager；不新建资源内核或 ModuleManager。官方 HybridCLRSettings 是唯一全局程序集配置源，YL 配方只编排，不通过另一份设置覆盖它。

**Tech Stack:** Unity `2022.3.62f3c1`、Windows64 IL2CPP、HybridCLR `9.0.3`、Unity Test Framework `1.3.9`、现有 Json.NET 与 HybridCLR 自带 Editor-only dnlib。

**Spec:** 用户本轮要求及下列已确认决定；遵循 `Assets/YLFramework/Documentation/{Architecture,IntegrationPlan}.md` 与项目 AGENTS.md。本文件是本专项唯一实施/验收计划。

## Global Constraints

- **已确认：** YL 原生集成，资源加载/发布走 YL Resource，不依赖 XF/XFAB。
- **已确认：** 只开发框架和独立样例；现有游戏与 YL 开发无关，不研究、迁移、修改或作为验收入口。
- **已确认：** 首批正式验收仅 Windows64 IL2CPP，不扩大 Resource 的桌面/远端/加密平台边界，不宣称 Android、iOS、macOS、Linux 已受测。
- **已确认：** 启动时可检查、下载、激活新版本后加载代码；已加载代码的进程只准备更新，重启换版，不做同进程替换、卸载、DHE 或状态迁移。
- **已确认：** 仅支持并精确锁定官方 `v9.0.3`；移除上轮临时嵌入的 XF HybridCLR 扩展和 manifest 项，其他 XF 包不动；不增加 XF API、7.3.0、旧配置或旧 bundle 兼容层。
- **已确认：** 保留 asmdef 与外部预编译 DLL 两种来源；不另造源码编译器，不默认支持 Assembly-CSharp 整体热更。
- **已确认：** 正式可选集成，独立程序集、工具、样例和导出入口；默认 YL 导出及无 HybridCLR 的独立工程保持原合同。
- **已确认：** 显式一键编排和分阶段报告；新基座与代码补丁分流。安装器单独显式执行，导入/OnGUI/普通构建不自动联网安装或同步覆盖全局设置。
- 修改前复核 branch、HEAD、index、dirty、未跟踪文件及唯一 Editor 的 projectPath。保护原有 Plan、工具配置、包增量、上一轮空目录清理等不相关改动；不主动切分支、建 worktree、提交或推送。
- `Library/PackageCache` 只读，不直接修补；只改真实源码、manifest，lock 由 Unity 解析更新。新增资产由 Unity 生成新 GUID，不复制 XF 包的旧 meta；删除旧包前复核是否出现并行修改或真实新消费者。
- 不改 `YLFrameworkRoot`、Core 资源合同、Core Module 生命周期、Resource 平台/类型/更新内核、默认 ExportRoots 或 Stage9 Mono 验收；不新增全局下载器、密钥系统或供应商安装器。
- 所有 Unity 对象、Resource 请求/租约和 Module 操作都在主线程；不把 `TextAsset.bytes`、Dispose/Unload 或 Core 启动放进 Task.Run。

## Review Focus

- 新版本代码被错误用于另一基座、另一 Target/Development 或旧 AOT 输出：由 T2/T3 的身份校验和 T6 的负向补丁证明阻断。
- 某个 metadata/Assembly.Load 已执行后失败：不能声称回滚或继续切到另一包；由 T4 的进程状态及 RestartRequired 测试固定。
- 并发调用、同步完成、完成通知重入、Module 启动失败：不能重复加载、挂起或清除后来请求；由 T4 定向测试覆盖。
- 代码已加载但资源可以在线激活：由 T4 的小型 manifest 租约屏障和 T6 的 Prepare/Activate 场景证明不会运行旧代码配新资源。
- 可选包缺失、错误版本、无 Domain Reload、多次 Play 与独立导出：由 T1/T5/T6 检查隔离，不放宽默认框架独立性门禁。

## 1. 已核实依据

- 来源：`Packages/com.xfkj.xfgameframework.hybridclr`，版本 `1.1.3`，上游 revision `7af0ba2299cfb2f17bb7442aa50b72c29980e0dd`。当前仅去掉未使用的 Codice using；MIT / Copyright (c) 2024 弦风课堂。当前可读 Assets/ProjectSettings 中无该热更 API 调用、设置资产或产物，不为未知外部工程预建兼容层。
- 当前实际包是 `#v7.3.0`；`Library/PackageCache/com.code-philosophy.hybridclr@e48a7beb95` 只是 9.0.3 的非活动 main 缓存。官方 `v9.0.3` tag 已核实为 `6cadd24d101f4cbd00cd80190106f6621132d24d`，不能用 main 代替发布标签。
- 9.0.3 程序集名仍是 `HybridCLR.Runtime` / `HybridCLR.Editor`，源码 namespace 改为 `HybridCLR.Settings`、`HybridCLR.Commands`、`HybridCLR.Installer`、`HybridCLR.Meta/Mod/Utils/CodeGen.*`；设置使用 `HybridCLRSettings.instance` 和无参 `Save()`。
- 9.0.3 输出改到 `Library/HybridCLR`，生成物为 `UnityVersion.inc`、`AssemblyManifest.inc`、`MethodBridge.inc`。旧 `HybridCLRData` 安装不能复用。2022.3.x 运行库清单对应 hybridclr `v9.0.1` / il2cpp_plus `v2022.3.x-9.0.1`，安装版本凭据仍须等于 package `9.0.3`。
- 9.0.3 无可直接调用的 AssemblySorter；`AssemblyReferenceDeepCollector.GetLoadedModules()` 不是依赖优先顺序。必须从实际 DLL 引用确定稳定拓扑序。
- YL `ResourceSystem.AcquireAsync<TextAsset>` / `AssetLease<TextAsset>.Asset.bytes` 可读取 DLL 字节；`.dll` 被 ResourceBuildMap 拒绝，必须输出 `.dll.bytes`。不需要扩展 `ICoreResourceProvider.ReadTextAsync`。
- YL `ModuleManager.StartUpModule(string/Type)` 已管理启动去重和失败清理；其 `StartUpModuleRequest` sealed 且不支持 Abort。新集成组合调用，不继承它、不复制生命周期。
- `ResourceEditorOperations.ExecuteRecipe`、`ResourceBuiltInDeployer.Preflight/Deploy`、更新的 Check/Prepare/Activate 均为可复用正式入口；`Integrations` 不进入默认 ExportRoots，现有独立性和 Stage9 证据不因新增可选模块自动失效。

## 2. 设计合同

### 2.1 目录与依赖

新增 `Assets/YLFramework/Integrations/HybridCLR/Runtime/YLFramework.HybridCLR.asmdef` 和 `Editor/YLFramework.HybridCLR.Editor.asmdef`，namespace 分别为 `YLFramework.Integrations.HybridCLR` / `.Editor`。Runtime 只引用 `YLFramework.Runtime`、`HybridCLR.Runtime` 和现有必要依赖；Editor 引用两者、`YLFramework.Editor`、`HybridCLR.Editor`，dnlib 只在 Editor 使用。

两程序集 `autoReferenced=false`，用包 versionDefines 和 defineConstraints 隔离，例如 `[9.0.3,9.0.4)` 产生 `YL_HYBRIDCLR_9_0_3`；预检再严格核对 version=`9.0.3`。样例/测试用相同约束，测试另加 `UNITY_INCLUDE_TESTS`。不扩大主体 asmdef 的依赖，也不添加 Foundation 的 InternalsVisibleTo。

### 2.2 配置、构建与 AOT 基线

- `HybridCLRBuildProfile` 是保存在 Assets 的非秘密 ScriptableObject，引用已保存的 `ResourceBuildRecipe`，并配置 Development、固定生成目录与明确的持久基线目录。ProjectId、Target、AppContentCompatibilityId 从 ResourceRecipe 读取，不建立第二套项目身份。
- 热更程序集清单只取官方 HybridCLRSettings：asmdef、程序集名和 `externalHotUpdateAssembliyDirs`。YL 窗口提供只读摘要及“打开官方设置”，不复制 XF 的项目同步/设置覆盖模型。
- 首版要求显式 asmdef 或由真实外部 DLL 解析出的程序集。验证空项、重复名称、大小写碰撞、非法路径、Editor-only 依赖、循环依赖、缺失依赖，以及 AOT 对热更程序集的静态反向引用。外部 DLL 不自动导入为 AOT Plugin，不按文件名猜 assembly identity。
- 预检固定 active Target=StandaloneWindows64、Development、IL2CPP、官方所需 API compatibility 与安装版本，检查 Windows IL2CPP/原生工具链。不能依赖官方 CheckSettings 在构建中自动切后端；不悄悄改变主工程设置。
- **新基座：** 用与 9.0.3 相同的 Unity PlayerBuildInterface 编译参数并检查 ScriptCompilationResult；按官方顺序执行 Il2CppDef、LinkXml、Empty MethodBridge、Strip AOT、完整 MethodBridge，再用官方 AOTReferenceAnalyzer 计算完整必需元数据集合。不能以残留 DLL、时间戳或 `compile finish` 日志判成功。
- 最后的 AOT 清单直接写入本集成 manifest，不为这一清单生成全局 `AOTGenericReferences.cs`；复用官方分析器而非解析生成的 C#。必要的 link.xml 仍沿官方设置路径生成，既有生成文件不盲删，不擅改用户输出策略。这避免为一个列表触发重编译/域重载及重复类。
- 将成功生成的 payload 纳入 Resource Build/Publish，并显式部署所选内置快照，再构建明确场景列表的基座 Player。最终 BuildReport 成功后，保存**该次真实 Player 的**裁剪 AOT DLL、元数据、工具/设置指纹、官方生成物、程序集集合及 hash，写完成标记，形成不可覆盖的持久基线；失败候选不能作为基线使用。
- 基线不放在可清理的 Library/Temp 中，不把后续 GenerateAll 留下的“最新 AOT DLL”当成已发布基座。AppContentCompatibilityId 是 AOT 启动端编译/绑定的基座身份；新基座使用新身份和新输出目录。初始包的 SuperSet 元数据与最终 Player 的一致性须验证，差异不能只用文件 hash 猜兼容。
- **代码补丁：** 读取明确、完整的旧基线，只重新编译热更 DLL、分析依赖/元数据需要并验证它们对冻结 AOT 的引用。复用冻结的 AOT 产物，不运行 GenerateAll/重新裁剪 AOT/重建基座；本批补丁不新增、删除或改名基座预登记的程序集。新 AOT 依赖、原生桥接/泛型需求或基座设置变化要求发新基座。检查器可检查的范围与仍需 Player 验证的范围分别报告，不承诺静态分析证明全部动态泛型安全。
- 生成目录仅写自己拥有的 `.dll.bytes`、metadata `.bytes` 和 JSON；保留稳定文件/目录 meta。先在自有临时目录验证，再更新受管文件并最后提交 manifest；未知文件/链接/所有权冲突阻断，不递归清空目录。源生成失败停止 Resource 构建，旧发布制品不动；发布和部署事务沿用 Resource，不另造发布事务系统。
- 初版 ResourceRecipe 不生成热更地址常量源码；地址由固定规则和 manifest 提供。默认样例 ContentEncryption=None、不发布 PDB；用户启用现有 Resource 加密时仍走其 key provider，不在 profile/日志/manifest 存密钥，不造 DLL 加密协议。

### 2.3 Manifest 与运行时

- manifest 是同一 ResourceProject 中的 TextAsset，schema v1 包含 ProjectId、平台、Unity/HybridCLR 版本、Development、AppContentCompatibilityId、代码包身份、有序热更程序集和必需 AOT 元数据；每项包含真实 assembly identity、资源地址、长度、SHA256、依赖。代码包身份基于规范化描述与 payload 计算，不把包含自身的 Resource CatalogDigest 写进自身产生循环依赖。
- 使用已签名/校验的 Resource 发布链运输 manifest 与字节，不增加第二套签名、HTTP 或“找最新目录”机制。严格校验字段、重复名称、地址归属、依赖顺序、数量/大小预算及 hash；不靠缺 order 时 LoadAllAssets 的不确定回退。
- Runtime 接收已成功启动的 ResourceSystem，不初始化/关闭它，不代替应用检查下载策略。启动流程为：ResourceStartup + Core 绑定 → 必要的 Check/Prepare/Activate → 读取/验证 manifest 与全部 payload → metadata → 依赖优先 Assembly.Load → 显式启动指定 YL Module。
- 先主线程取得 bytes，再释放各 DLL 的 TextAsset 租约；进入不可逆 native 加载前完成全部可完成的输入检查。LoadMetadataForAOTAssembly 使用 SuperSet 并检查所有返回码。只有本进程登记过且 hash 相同的 metadata 才能视作已加载；未知来源的“已加载”错误不冒充成功。
- 保存一个小型 manifest Lease 作为该活动代码会话的版本屏障，不保留所有大 DLL 租约。它利用现有 Resource Busy 屏障阻止该闭包在线激活；运行中仍可 Check/Prepare。样例的代码和资源在同一发布项目，避免另造跨项目事务；多 mount 应显式走 Resource 已有完整闭包。
- 进程级记录程序集 identity/hash 及不可逆加载结果；同版本重复请求幂等，同名不同字节拒绝并要求重启。实例内相同在途加载合并；跨实例竞争也不得重复 native 加载，可确定返回 Busy。已部分 native 加载后失败保留记录并标记 RestartRequired，不清表假装可回滚。DLL 已成功加载而 Module 启动失败时，复用 Core 清理合同，可显式重试同版本 Module，不重新加载 DLL。
- 新请求通过组合复用 Core，覆盖 Abort 并在任何状态改变前抛 NotSupportedException，不能取消后协程继续却报告成功取消。非法参数/线程/已关闭入口在建请求前拒绝；请求建立后的资源、native、反射及 Module 异常均终态完成一次，带项目、阶段、程序集、原始异常/native 错误码及是否需重启；通知异常不反转既有终态，不顺手重构 Foundation。
- Editor 模式显式标为 Simulation，只复用 AppDomain 中已加载的同名程序集，不重复 Assembly.Load。未加载的外部 DLL 给出明确不可模拟诊断，外部 DLL 正式能力在 Player 验收。Editor metadata 返回 OK 不是原生验收；关闭 Domain Reload 时重建会话/请求引用，不假装卸载程序集。
- 应用先关闭本入口启动的 Module，再 Dispose loader 释放自己的 manifest 屏障；在途/仍活动的受管 Module 阻止 Dispose，拒绝时不修改现有责任。LoadAsync 后由调用方自行启动的代码/Module 由调用方先停止，集成不扫描全场景猜生命周期。Dispose 不关闭 ResourceSystem、不卸载 DLL，也不准以 Dispose + 新 loader 实现代码换版；进程级冲突记录仍有效。

### 2.4 对外接缝

具体内部记录可由实施者保持最小实现；跨任务固定以下入口，禁止为单一 YL 后端预造多供应商抽象：

```csharp
// YLFramework.Integrations.HybridCLR
public sealed class HybridCLRLoader : IDisposable
{
    public HybridCLRLoader(ResourceSystem resources, string appContentCompatibilityId);
    public HybridCLRLoadRequest LoadAsync(ResourceAddress manifestAddress);
    public HybridCLRModuleRequest StartModuleAsync(ResourceAddress manifestAddress,
        string entryAssemblyName, string moduleTypeFullName);
    public void Dispose();
}
// 两请求沿用 CustomAsyncOperation<T> 的 yield/isDone/progress/error；
// 另公开只读 Exception、RequiresRestart；LoadRequest 提供已加载程序集结果，
// ModuleRequest 提供 Module。类型在指定 entryAssembly 内精确解析，不全域猜同名类型。

// YLFramework.Integrations.HybridCLR.Editor
public static HybridCLRBuildReport Validate(HybridCLRBuildProfile profile);
public static HybridCLRBuildReport BuildBasePlayer(HybridCLRBuildProfile profile,
    BuildPlayerOptions options, CancellationToken cancellation = default,
    IResourceContentKeyProvider provider = null);
public static HybridCLRBuildReport PublishPatch(HybridCLRBuildProfile profile,
    CancellationToken cancellation = default, IResourceContentKeyProvider provider = null);
// 三者位于 HybridCLRBuildPipeline；报告分阶段记录预检/编译/生成/资源发布/
// 部署/Player/基线结果，不把其中一个成功当作全部成功。
```

## 3. 实施任务

新增路径下省略 `Assets/YLFramework/` 前缀。新增文件/目录 meta 由 Unity 生成；普通内部算法和私有辅助由实施者自主保持简洁，不机械建框架。

### T1: 官方依赖与可选程序集基线

**Files:** 修改 `Packages/manifest.json`，由 Unity 更新 lock；移除 `Packages/com.xfkj.xfgameframework.hybridclr/**`；新增 `Integrations/HybridCLR/{Runtime,Editor}/*.asmdef`、对应 `Tests/Integrations/HybridCLR/{EditMode,PlayMode}/*.asmdef` 和 `Dependencies.json`。

- [x] 先记录原包清单、dirty、旧包文件指纹及真实消费者；若旧包自上轮临时嵌入后出现并行修改，停止其删除并确认归属，不丢弃用户工作。
- [x] 精确替换为官方 `https://gitee.com/focus-creative-games/hybridclr_unity.git#v9.0.3`，同批移除旧 XF 扩展及 manifest 项，避免 7.3 源码先被 9 编译；保留其他 manifest 项，复查 Unity 实际解析版本/tag/lock，不手写 lock。
- [x] 建立上述独立程序集和约束，添加 `HybridCLRBoundaryTests`，证明主体无 HybridCLR/XF 依赖、集成无 XF/XFAB 依赖、dnlib 不进 Runtime；主体排除全部可选依赖的源码编译与默认导出通过，完整无包接收工程另在 T6/G4 验证，错误版本的构建预检另在 T3 验证。
- [x] 在 Editor 实测官方 Installer 的读取/打开及安装预检。源码存在通过 `Packages/...` 读 package/Data~ 的路径风险，Git 虚拟路径是否可用不能靠离线 Directory.Exists 判定。若实际失败，保留诊断，停止该安装路径；不得修 PackageCache、造链接/伪目录或悄悄引入 fork，改用官方 tag 的原样 Local/Embedded 安装须明确说明并确认依赖交付变化。
- [x] 官方 Installer 显式安装后确认 `PackageVersion == InstalledLibil2cppVersion == 9.0.3`；未安装、工具链缺失或权限不足则记为阻塞，不声称支持已验收。此时尚未运行热更功能测试。

### T2: 格式、程序集分析与纯校验

**Files:** 新增 Runtime `HybridCLRManifest.cs`；Editor `HybridCLRBuildProfile.cs`、`HybridCLRAssemblyAnalysis.cs`；EditMode `HybridCLRManifestTests.cs`、`HybridCLRAssemblyAnalysisTests.cs`。

- [x] 先写失败断言：空/重复/大小写碰撞、未知 schema、错误基座/版本/平台、地址逃逸、循环/缺依赖、AOT 反向引用；同一 DLL 集合不同输入顺序产生相同规范化 manifest/拓扑序。
- [x] 从真实 DLL 元数据读取名字/引用，用稳定拓扑序构造 manifest，AOT/Unity/框架依赖与热更依赖分开；外部 DLL 和 asmdef 产物采用同一套校验。不加入手工 loading_order 或不确定回退。
- [x] 封装官方 AOTReferenceAnalyzer 的结果集合，完整收集所需 metadata；不从旧 patchAOTAssemblies、全局 AOTGenericReferences 类型或字典遍历顺序取名单。
- [x] 运行上述 EditMode 类并确认通过；损坏 DLL 用真实坏输入证明被拒绝，错误不得只因缺类型导致编译失败。dnlib 自有读取器正常 Dispose，不擅自 Dispose 官方 cache 拥有的 ModuleDef。

### T3: 基座/补丁构建与 Resource 交付

**Files:** 新增 Editor `HybridCLRBuildPipeline.cs`、`HybridCLRBuildBaseline.cs`、`HybridCLRPlayerBuildGuard.cs`；EditMode `HybridCLRBuildPipelineTests.cs`、`HybridCLRBaselineTests.cs`；夹具仅用本专项 owned 目录。

- [x] 先补失败测试：无配置/无安装/版本错、Target 或 Development 不符、编译失败但旧 DLL 仍存在、基线未完成/被改写、新 AOT 依赖、未知生成文件、发布/部署失败和取消检查点。
- [x] 实现 Validate 的纯只读预检；实现 T2 所需的受检编译和官方 9.0.3 生成顺序，特别保留 Strip AOT 之前的 Empty MethodBridge。不能盲调返回 void 且不检查 ScriptCompilationResult 的 CompileDll 后继续发布。
- [x] 完成 BuildBasePlayer：准备 → 正式 Resource Build/Publish → 显式内置部署 → 指定样例/调用方场景的 Player Build → 冻结真实最终 AOT 基线。取消仅在受支持检查点生效；native BuildPipeline 运行中不承诺中断。最终基线和发布结果分别报告，失败不伪装成功或删除旧交付。
- [x] 完成 PublishPatch：对完整旧基线编译/验证新代码，只使用冻结 AOT；不偷偷 GenerateAll 或更新基座。输出基线/程序集/代码包摘要，确认仅代码与声明资源变化，不改旧 Player。
- [x] Guard 只对本管线显式构建上下文校验，区别内部 scripts-only strip 和最终 Player；不在无 profile 的工程安装、生成或写配置，避免 Strip AOT 自身触发递归构建。全局 HybridCLRSettings 仍由官方/用户管理。
- [x] 用真实 `.dll.bytes` 经正式 Resource Builder/Publisher 验证产物，检查 Group 地址、无 assetBundleName 污染、稳定 GUID、manifest-last 失败封闭及旧发布完好；运行定向测试并记录结果。发布、部署、Player 三种成功不能互相替代。

### T4: Runtime 加载、会话屏障与 Module 组合

**Files:** 新增 Runtime `HybridCLRLoader.cs`、`HybridCLRRequests.cs`；PlayMode `HybridCLRLoaderTests.cs`、`HybridCLRModuleTests.cs`；必要的 native 调用替身仅为本集成内部测试接缝，不建立公共后端系统。

- [x] 先覆盖资源请求同步完成/失败、坏 hash、缺 DLL、metadata 非 OK、并发/重复/冲突、通知重入、Abort、会话失效和 Module 启动失败，保证任何路径都终态且只释放自己拥有的请求/租约。
- [x] 按 §2.3/2.4 实现主线程流程与阶段诊断，输入校验在不可逆加载前完成，metadata 先于热更 DLL；默认不使用后台 Unity API。成功与部分失败均维护正确的进程记录，不靠 loadedProjects 字符串 List 判断安全。
- [x] 保留小 manifest Lease 至 loader 结束；证明运行中 Prepare 可成功，Activate 对该闭包返回 Busy 且旧版本保持可用。所有 DLL 大租约读取完即释放；退出先关 Module，再释放屏障，再由应用 Stop 资源系统。
- [x] Module 启动只组合现有 Core 请求并精确传递异常；覆盖 DLL 成功但 Module 失败后同版本重试。Abort 在改状态前拒绝，Dispose 不模拟程序集卸载，也不擅自关闭 Core/Resource。
- [x] 运行上述 PlayMode 类和相关 Core Module / Resource 请求生命周期回归；Editor Simulation 与 native 替身仅证明合同，不能记录为 IL2CPP 热更成功。

### T5: 工具、可选导出与独立样例

**Files:** 新增 Editor `HybridCLRBuildWindow.cs`、`HybridCLRIntegrationExporter.cs`；`Integrations/HybridCLR/Samples/{Bootstrap,HotUpdate,Editor}/` 的独立 asmdef、启动样例和生成工具；`Tests/Integrations/HybridCLR/EditMode/HybridCLRExportTests.cs`。

- [x] 窗口只编辑明确选中的已保存 profile，显示官方设置/安装/代码包/基线摘要；提供预检、打开官方设置/Installer、新基座、代码补丁、部署与导出。OnGUI 不保存、不生成、不安装；窗口与 CI 复用 T3，不增加 Resource Workbench 页面。
- [x] 样例创建独立的新目录/新 GUID，不覆盖原场景或自动保存用户脏场景。AOT Bootstrap 与热更 Module 独立 asmdef，AOT 不静态引用热更类型；热更名在两版之间固定，autoReferenced=false。
- [x] 可选导出只含本集成、必要样例、来源/许可和使用说明，明确先导入默认 YL 包及安装官方 9.0.3；不递归打包整个 Integrations、测试、包缓存、官方包二进制或本机制品。不改变默认 ExportRoots/Stage9。
- [x] 样例演示 asmdef 与外部 DLL、依赖顺序、YL Module 启动、一个附加热更 MonoBehaviour 的 Prefab，以及 AOT 泛型/async 调用。首版不扩展 Resource 对“初始化前尚不存在的热更 ScriptableObject 类型”的目录映射合同。
- [x] 运行导出/边界和原默认导出/主体独立性定向回归；确认无包接收工程可继续使用默认框架，装入可选集成后编译依赖闭包正确。

### T6: IL2CPP 两版代码验收与文档收口

**Files:** 本专项 `Tests/Integrations/HybridCLR/` 的 Player 验收入口与自有夹具；`Tests/Tools/Integrations/HybridCLR/` 最小自动化入口，复用已有 Unity/Resource 验收设施；更新本 Plan、现有 `Documentation/{README,Architecture,IntegrationPlan}.md`、新增模块使用页及 `LICENSE.md` 中准确的来源/可选依赖说明。

- [x] 在独立接收工程安装官方 9.0.3、默认 YL 包和可选集成，显式 Installer + IL2CPP + Low stripping；不得对同一工程开第二个 Unity，也不为验收关闭用户打开的 Editor。SDK/授权/环境不足就报告阻塞。
- [x] Development/Release 各完成一次基座构建与真实运行；保留 native Player 日志、BuildReport、包/基线摘要和资源版本。验证 metadata、泛型/async、依赖 DLL 和热更 Prefab 脚本，不能仅看 DLL 生成或无编译错误。
- [x] 对相同基座生成 v2：只修改自有热更源/外部 DLL 及样例资源，发布正式签名的远程版本；复用既有 Resource 的隔离 HTTPS 验收能力，不向真实远程服务部署或保存密钥。运行中 Prepare 完成但不切代码，重启同一个 Player 后输出 v2 行为，证明 Player 可执行文件 hash 未变且加载的 DLL 内容确实变化。
- [x] 验证坏 hash/签名、缺 DLL、元数据错误、错误基座/Development、部分 native 失败及新 AOT 需求均明确失败；旧资源版本/旧基线不被破坏，不回退成 Editor Simulation、不默默重新编基座。
- [x] 验证正常停止、重复启动、无 Domain Reload 多次 Play，区分模拟/native 证据；必要的默认 YL 导出、Module/Core 和 Resource 局部回归保持通过，不机械重跑不受影响的全部历史验收。
- [x] 增量记录来源 XF 1.1.3/MIT、官方 tag/commit、调用方式、配置/构建/基线/重启/错误码/平台边界；保留适用版权，不复制模板文档或再造过程日志。新使用页属于用户本轮功能交付，规划代理本轮不编辑它。
- [x] 最后核对实际 diff、新增文件及 meta、所有旧 XF 热更引用/构建钩子已退役、其他包和不相关工作区未被接管；运行 `git diff --check`。只报告实际通过的门禁及明确阻塞，未授权不提交/Push。

## 4. 验收与交付标准

| 门禁 | 必须获得的证据 |
|---|---|
| G0 依赖/安装 | Unity 实际解析官方 9.0.3；旧 XF 扩展不再编译或执行钩子；Installer/本地运行库版本匹配；工具链可用 |
| G1 静态与单元 | 可选边界、格式、真实 DLL 分析、稳定排序、构建基线与负向测试通过；不放宽主体独立性 |
| G2 生命周期 | PlayMode 的请求/租约/屏障/去重/Module 合同通过；Simulation 明确标识，native 替身不冒充原生证据 |
| G3 真正热更 | Windows64 IL2CPP Development/Release 运行，metadata/泛型/async/Prefab 正常；同一基座 Player 重启加载 v2 asmdef 与外部 DLL，版本身份及 executable hash 可核对 |
| G4 独立交付 | 默认无 HybridCLR 的框架仍可导出/接收；可选包在干净工程独立编译/运行，不依赖 XF/XFAB/主工程测试程序集 |

**本轮不纳入：** 现有游戏、移动端/更多桌面平台支持、同进程热重载/DHE、程序集卸载、旧 XF 兼容、Assembly-CSharp 整体热更、新下载/加密/签名协议、PDB 分发、自动化安装器、全局资源类型注册重构及 Git 收口。

## 5. 证据与未验证项

- 关键 YL 源码：`Runtime/Systems/Resource/{ResourceConfiguration,ResourceSystem}.cs`、`Loading/ResourceRequest.cs`、`Lifetime/AssetLease.cs`、`Updates/ResourceSystem.Updates.cs:124-158`、`Catalog/ResourceLocalSnapshot.cs:111-135`；`Runtime/Core/Modules/ModuleManager.cs:346-497`；`Requests/StartUpModuleRequest.cs:24-57`。
- 关键 Editor 接缝：`Editor/Systems/Resource/Service/ResourceEditorOperations.cs:94-128`、`Publishing/ResourceBuiltInDeployer.cs:95-124`；`Editor/Distribution/ExportUnityPackage.cs:15-25`；可选 asmdef 惯例见 `Integrations/XFAB/Audio/YLFramework.Audio.XFAB.asmdef`。
- 上游版本/API 以官方 tag 的 `Editor/{Commands,Settings,Installer,Utils}` 源码为准。官网和缓存 Data~/skills 部分仍写旧 namespace/HybridCLRData，不据此硬编码路径。Context7 工具在本规划环境未暴露，已用官方 tag 源码与官网一手资料交叉核对。
- 官方版本源码：`https://github.com/focus-creative-games/hybridclr_unity/tree/v9.0.3`；安装：`https://www.hybridclr.cn/docs/basic/install`；运行限制：`https://www.hybridclr.cn/docs/basic/runhotupdatecodes`；构建：`https://www.hybridclr.cn/docs/basic/buildpipeline`。
- **规划时尚无运行证据，现以 §6 最终记录覆盖：** 不用源码支持声明冒充验收；SDK、联网、权限失败均保留真实诊断，未静默拓展依赖来源或降低标准。

## 6. 实施记录

### T1 启动基线（2026-10-21）

- 分支 `integration/xf-yl`，HEAD `b5950825eac886c78fde0a445459af769a97e96a`，index 为空；不创建分支、worktree 或提交。既有游戏、工具配置、其他包增量及四个空目录 meta 删除保持原样。
- 唯一 Unity 进程为 `2022.3.62f3c1`，实际 projectPath=`G:\Unity\Projects\CGJ2024`；启动时无脚本编译失败，当前 `StandaloneWindows64`、非 Development，已打开场景无脏状态。
- 旧 XF `1.1.3` 从上游 revision `7af0ba2299cfb2f17bb7442aa50b72c29980e0dd` 重新取得只读比较源；除 UPM 未携带上游 `.gitignore` 及已知的无用 Codice using 删除外，文件 diff 无新增改动。旧包全部内容的 Git blob 聚合指纹为 `c7b6ad3d661eaba8be2c4013eb3ae83aaa48d76c`。Assets/ProjectSettings 无旧热更 API 消费者或设置；其他 Packages 中仅旧包自身命中。
- 已有受管非包修改的保护指纹为 `c51c9143e2087d4b183e20975ba082e9facf3844`。未读出或保存任何密钥至本计划。
- 官方 Gitee `refs/tags/v9.0.3` 实测仍为 `6cadd24d101f4cbd00cd80190106f6621132d24d`。Context7 仅返回 native 主仓库 main 的资料，不覆盖 Unity 包 9.0.3 Installer；版本相关实现仍以精确 tag 的实际解析源码及 Editor 实测为准。
- 接口预检：T2 的规范化 manifest 同时供 T3 生成与 T4 校验；T3 基线身份直接复用 ResourceRecipe，T4 仅组合 Core 请求，T5/6 消费 §2.4 固定入口。当前未发现需要改动既有公共合同的冲突。
- 实施约定：依照本 Plan 在当前已授权工作区执行，进度只维护本 Plan，不采用技能默认的额外 ledger/worktree/提交步骤。
- T1 边界测试先红：实际执行 9 项，主体无依赖 3 项通过；旧包未退役、官方版本仍为 7.3.0、四个叶子定义/约束未建立的 6 项按预期失败。首次测试脚本的 PackageInfo 名称歧义已修正，编译错误不计作 RED 证据。
- 旧包 52 个文件原样移出 Packages，保留于 `Temp/YLHybridCLRMigration-20261021-XF-1.1.3`，避免丢失未跟踪的来源文件及二进制素材。按相对路径排序的逐文件 SHA256 聚合为 `36cd68fed867a0e656b4496f72ad194150186aae5bd10435f5b3e448fbfd79b0`；该目录不是依赖、导出或基线。
- Unity 实际 PackageInfo 与 Unity 自行更新的 lock 均为 Git / `v9.0.3` / `6cadd24d101f4cbd00cd80190106f6621132d24d`；其他包增量未改变。T1 边界测试 GREEN=9/9，无跳过；主体 Editor/Player 编译闭包未引入 HybridCLR/XF，四个可选 asmdef 的版本宏/平台/自动引用与 dnlib 边界通过。三个尚无源码的产品/PlayMode 叶子暂不产出 DLL，不以此宣称 Runtime 已实现或无包接收验证完成。
- 官方 InstallerController 在真实 Editor 中构造成功，PackageVersion=`9.0.3`、未安装，清单选择 hybridclr `v9.0.1` / il2cpp_plus `v2022.3.x-9.0.1`；官方 `HybridCLR/Installer...` 菜单成功打开 1 个窗口，验证后只关闭本次创建的窗口。Git 虚拟路径实测可用，不需要 Local/Embedded/fork。
- 切包导入曾因已不存在的旧 `Plugins/LZ4.dll.meta` 出现 Unity “Opening file failed” 模态，MCP 在此期间不可用；只读 UI 诊断确认原因，尝试 Cancel 时窗口已消失、未实际发送操作。随后域重载及 9 项测试成功，不修 PackageCache 或创建伪文件。
- 主工程预检实际为 `StandaloneWindows64` + `Mono2x` + `NET_Standard_2_0`，Windows 模块和 Editor il2cpp 目录存在；这些配置保持原样，不能作为基座构建预检通过。IL2CPP/.NET Framework 配置及原生构建验证归独立接收承载，非自动切换本工程设置。
- 官方 Installer 已单独显式执行成功，重建 Controller 复查 `PackageVersion == InstalledLibil2cppVersion == 9.0.3`，版本凭据位于 `Library/HybridCLR/LocalIl2CppData-WindowsEditor/il2cpp/libil2cpp/hybridclr/generated/libil2cpp-version.txt`；无全局设置保存。vswhere 证实 Visual Studio 2022 Community 的 x86/x64 C++ 工具组件已安装，但尚未执行本专项原生 Player 构建。
- T1 回归：`FrameworkAssemblyIndependenceTests` 7/7（含完整主体排除 XF/可选依赖的 Editor/Windows Development 双 profile 源码编译），`ExportUnityPackageTests` 6/6（实际默认导出/解包）；都无跳过。新建 meta 由 Unity 导入生成，原保护指纹复查保持。T1 基线完成不等于 G3 真热更或 G4 新版可选集成接收完成。

### T2 格式与程序集校验（2026-10-21）

- 已实现 schema v1、固定同项目资源地址、SHA256/长度/MVID/真实 assembly identity、基座/Unity/Development 校验、数量/大小预算、重复 JSON/未知字段/尾随数据拒绝、稳定依赖优先排序与不包含自身的 CodePackageId。
- 已实现自有 dnlib 读取器及热更/AOT 分图；实际 DLL 名称取元数据而非文件名，缺失/版本不匹配/Editor 依赖/循环/重复/大小写碰撞/AOT 反向引用拒绝。外部 DLL 与编译产物使用相同路径，默认 Assembly-CSharp 未开放。
- 官方 AOTReferenceAnalyzer 通过精确输入 resolver 运行；集合合并递归引用、直接引用（含 netstandard）和 AOT 泛型结果，排除热更后按名称排序；不生成 AOTGenericReferences.cs，不释放官方 cache 的 ModuleDefMD。
- T2 RED：30 项失败均为尚未实现的合同，后追加官方 analyzer 完整集合用例 RED=1/1。实际坏 DLL 字节和真实 dnlib 生成依赖图均参与测试，不使用缺类型编译失败替代行为断言。首次编译的 FileAttributes 名称歧义及摘要预计算时 Required.Always 拒绝尚不存在的 ID 已修复。
- 最终 `YLFramework.Tests.Integrations.HybridCLR` EditMode 40/40 通过（含 T1 9 项），无跳过。`HybridCLRBuildProfile` 仅保存 Recipe、Development 和两个明确目录，不另存项目身份或热更程序集清单。Runtime 加载、构建流程和 Player 均尚未实现/验证。

### T3 当前工作点（未完成）

- 已先建立 `HybridCLRBuildPipelineTests` 的 14 项失败断言，全部实际运行 RED（行为合同尚为显式 NotImplementedException；这不是完成实现）。覆盖分阶段错误且不改 PlayerSettings、无效请求在编译/发布前停止、Library/Temp/Assets 基线目录拒绝、生成目录未知文件保护、compiler 未报告的旧 DLL 拒绝、冻结基线缺 marker/字节被改/未知文件/receipt 被改拒绝、真实小 DLL 的冻结格式读取。
- 起始方法骨架已替换为实际实现（包括精确版本预检），不再留 NotImplementedException；完整基座/补丁构建仍须后续独立接收工程实测，不因脚本编译和格式测试通过就宣称可用。测试程序集友元仅授予本集成自己的 EditMode，不改变 Core 可见性。T3 测试格式 fixture 明确不作为原生 Player 成功凭据。
- 实现依据：官方 CompileDll 忽略 result，必须 fresh staging + `ScriptCompilationResult.assemblies` + 真实 DLL 身份；官方 Strip 使用全部 enabled scenes，所以首版 BuildBase 的显式 options.scenes 必须与此集合一致，不改 EditorBuildSettings。在成功实际 Player 返回后再冻结最终 AOT，若与发布 metadata 不一致则不写完成标记。
- Link 当前遵循 §2.2 的官方 outputLinkFile 路径策略，调用低层 LinkAnalyzer/LinkXmlWriter，不改官方设置或生成全局 AOT C#；此前考虑自有 Native/link.xml 的想法未采用。固定入口使用当前 §2.4 的 `BuildBasePlayer` 及 key provider 透传，不保留早期 BuildBase 占位名。临时/完整桥和 Il2CppDef 仅在显式新基座流程执行；补丁不运行 Strip/GenerateAll 或重建基座，只读冻结 AOT 做 MissingMetadataChecker、真实依赖和原生需求集合校验。
- 两个只读研究子任务已完成，未写代码或操作 Unity：Resource 的公开接缝为 `ResourceEditorOperations.ExecuteRecipe(recipe, token)`（Recipe 必须已保存且 BuildAndPublish）、`report.Release`、`ResourceBuiltInDeployer.Deploy(Preflight(release))`；生成资源必须预先由 Recipe.Project 的 SourceDirectory/Group 明确选择，Pipeline 不静默修改 Recipe。文件夹选择不能覆写 Address，故 SourceDirectory=GeneratedRoot、选择其 hybridclr 子文件夹，可产生固定 `hybridclr/...` 地址并排除 Native/owner。
- 官方 API 精确签名：`Il2CppDefGeneratorCommand.GenerateIl2CppDef()`；`MethodBridgeGeneratorCommand.GenerateEmptyMethodBridgeAndReversePInvokeWrapper(target)`；`StripAOTDllCommand.GenerateStripedAOTDlls(target)`；`MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper(target)`。为避免 external dir/旧 Library DLL 抢先，官方 resolver 的每个热更选择必须与 checked staging identity/hash 一致，或使用 T2 的精确 resolver。
- 仍须实现 T3 后推进 T4/T5/T6；主工程 Mono/.NET Standard 配置、游戏、其他 dirty 不变。独立接收工程须用于本专项 IL2CPP/.NET Framework 实际验收；不得把主工程配置不匹配当作已通过或改动其业务设置。
- 当前 T3 已实现分阶段报告、只读预检、fresh CompilePlayerScripts 产物核对、asmdef/外部 DLL 同一校验、生成/Strip/FullBridge、正式 Resource Recipe Build/Publish、显式 Deploy、受管 Player Guard、最终 AOT 对比与不可覆盖 baseline 完成标记；这组路径尚未在真实 IL2CPP 基座中执行。metadata 与最终 AOT bytes/MVID/identity 任一不符都不写完成标记；补丁拒绝程序集集合变化、新 AOT 依赖及新增可静态分析的 native/generic 需求，仍不承诺动态泛型全部安全。
- T3 追加测试曾实际 RED=5（4 个版本参数负例 + canonical 空目录 meta 初始化）；修复严格版本比较及只接纳三个已知空 payload 目录的 meta，不放宽未知文件/目录/链接和已修改受管文件的拒绝。
- 最新可跟踪 CLI 实际完成 `YLFramework.Tests.Integrations.HybridCLR` EditMode **62/62，通过，无跳过，65.7202129 秒**（T1/T2 40 + T3 22）。包含真实小 DLL `.dll.bytes` 经正式 Resource Builder/Publisher、取消后原 Catalog/旧制品不变、稳定 GUID、坏源字节在 manifest 提交前失败，以及基线坏字节/坏 receipt/未知文件；不等同真实热更或 native 成功。
- T3 还需补齐/自检 Player 参数与上下文变化防护、SDK/工具链预检、发布/部署失败分阶段及取消点，并在 T6 实测完整新基座/补丁。当前所有测试源码与实际实现均处于本集成目录，未改 Core、默认导出或主工程设置。额外 Unity PID 已实测为 AssetImportWorker，作者 Editor 仍只有 CGJ2024 一个。
- T3 进一步加入真实 vswhere + Windows SDK x64 UM/UCRT 预检，不安装/修复工具；无工具负例先 RED 后修复。新增无 profile Guard 不干预、错误 Player target 拒绝、未知空目录不认领测试。当前 EditMode 合集 **66/66**；按 T3 单元与正式发布范围完成，真实 Player/最终 AOT 基线/补丁端到端仍由 T6 门禁判定，尚未运行。

### T4 Runtime 与 Core 组合（2026-10-21）

- 已实现两种独立 Foundation 请求、显式 Resource 借用、逐个读取并释放 DLL lease、全量 hash/descriptor 检查后再 native、SuperSet 严格返回码、进程级程序集/项目冲突及部分失败重启记录、跨实例 Busy、manifest 小租约屏障、精确 entry assembly 内的 Module 类型解析与 Core 启动组合；不改 Core/Resource/Foundation API。
- Editor 只复用已加载程序集并明确 Simulation；不调用 Assembly.Load 或把官方 Editor metadata OK 当 native 证据。生产默认 calls 不可替换；只有本集成内部 PlayMode 友元使用原生调用替身与独立测试进程记录，替身用例不计为真热更验收。
- loader 基本行为 RED=8/8（修复同 Resources locator 的测试夹具错误后）；模块用例先运行 3 项均在尚未实现的构造入口失败，随后实际覆盖成功/失败/原异常/重试。当前 PlayMode **18/18** 通过，涵盖完整即刻完成、去重、坏 hash、缺资源、未知外部 DLL、线程、原生错误码/部分失败、跨实例重复和冲突、回调异常/重入、Module Dispose 拒绝与关闭后释放。Core 原有 `CoreModulePlayModeTests` **28/28** 回归通过。
- 更正测试假设：Resource Shutdown 在 manifest live lease 下返回 Busy 并保持 Ready，不会开始关闭；未为此修改主体。已按真实合同验证 Busy 后代码仍可用，释放 loader 后正式 Shutdown 成功。
- 仍待 T6 的真实 Bundle 更新 Activate Busy/关闭后重启应用 v2 以及 Domain Reload 关闭的双会话运行；18 项 PlayMode 不替代这些门禁。测试资源由显式 `HybridCLRTestFixtures.Prepare` 生成，只在 Tests 自有 Resources 内，不在默认/可选产品导出中。

### T5 实施裁决

- **Ruling:** 独立样例置于 `Integrations/HybridCLR/Samples`，不采用初始 Files 提示的主体 `Samples/HybridCLR`。依据：默认正式导出固定递归整个主体 Samples；保持七个默认根不变又让可选样例不进入默认包，必须把样例归入可选根。对外 API/功能不变；代价是文档路径与初始 Files 提示不同，统一在本文与使用说明记录。
- 工具窗口只在显式按钮中 Validate/Generate+BuildBase/PublishPatch/Export，打开/选中/绘制不保存、安装或生成。选定 profile/recipe、阶段错误、CodePackageId/CatalogDigest/基线引用由同一无 UI facade 报告。
- 可选 exporter 使用明确脚本/asmdef/依赖与许可说明选择，不递归依赖；不含 Tests、官方包/dnlib、原始 DLL、制品、基线、私钥。实际导出/解包、GUID与路径、默认不带样例以及网络路径拒绝 4 项先 RED 后 GREEN；窗口与样例资产作者 2 项通过。样例 Host 无热更静态引用，Entry→Support 依赖，并通过既有 Core 配表创建 Model。受管生成选择在实际生成前为空，Resource BuildMap 按合同拒绝，不放宽主体校验。
- 最新整个 HybridCLR EditMode **73/73** 通过，无跳过（77.8342757 秒）。其中新增实际 GenericClass 值类型区分回归先红后绿：官方 GenericClass 没有 ToString override，原需求指纹不能用对象默认字符串；现明确绑定程序集 identity 与 `ToTypeSig().FullName`，不把不同实例误判为相同原生需求。产品导出 r2 已重新正式生成，保留 r1 冻结制品及既有首导入收据，不覆盖历史。

### T6 接收与原生门禁当前工作点（2026-10-21）

- 自有根 `C:/Users/Kano/AppData/Local/Temp/kilo/H26-20261021`，owner.json 绑定来源工程、Unity 和唯一新项目 `R`；正式默认包 `YLFramework.unitypackage` 与可选包 `YLFramework-HybridCLR.unitypackage` 从实际 exporter 取得。首轮两次 -importPackage 均在安装 HybridCLR 或测试设施之前完成，进程 exit0、真实 Runtime/Editor DLL、无 XF/HCLR lock 由 `base-no-package-import.receipt.json`、`optional-no-package-import.receipt.json` 记录。默认/可选导出均无测试/官方 DLL；已解析官方精确 Git tag/hash 后才安装真实验收 consumer。
- 可选 r2 为 `YLFramework-HybridCLR-r2.unitypackage`；修正验收 consumer 的 AssemblyDefinitionAsset namespace 后，`sync-optional-r2-fixed.receipt.json` 记录成功导入。该错误只在测试消费端，不修改产品或放行编译错误。
- 可复现工具源位于 `Tools/HybridCLR/`：Unity wrapper、独立 harness 安装器、真实 Editor bootstrap/Player runner、loopback HTTPS fixture 和相同 Player 运行 wrapper。每次进程记录 `.log.process.json`/`.log.exit.json`，Run id 不覆盖；receiver-only consumer 仅在产品首导入后放入。Runtime 验收程序集使用主体现有 `YLFramework.Tests.PlayMode` friend 做**已存在且受 DEVELOPMENT_BUILD + YL_RESOURCE_ACCEPTANCE_TESTS 限定**的 loopback 精确证书 pin，不新增或放宽生产 API/信任，也不安装系统证书。
- 原生承载计划为 Development/Low Windows64 IL2CPP，AOT Host + 源 asmdef Entry + 真正独立编译的外部 Support DLL。持久基线在 `G:/Unity/Projects/CGJ2024/Build/HybridCLR/Reception-20261021/Baseline`，使用既有 Build 忽略规则、在 Temp/Library/Assets 之外，不改 ignore。接收工程和 HTTPS 测试凭据在自有 Temp；私钥不导出或写入日志/仓库。
- 前次安装进程 `install-official-runtime` 在域重载中中断，没有安装完成或退出收据，不能算失败验收或成功。核对已无该 PID 且无运行库版本文件后，只重启该未完成步骤；未重复启动同工程实例。改用 **persistent 跟踪进程** `bgp_15fb4e25f001wQ7R3UgzhNYewc`，Run=`install-official-runtime-2`，以进程退出和持久收据推进，不依赖定时任务或超时猜测。若工具会话丢失，先读该 Run 的 PID/exit/receipt 并核对存活状态，不能直接重跑。
- 下一步：安装成功后显式 CompileExternal（异步 AssemblyBuilder，Unity wrapper 使用 -KeepOpenForCallback）、Setup（仅接收工程改后端/API/宏、配置官方名单，Entry 通过预编译引用外部 Support，归档原 Support 源码）、Validate、Build；Build 若失败读分阶段首因，不改基座标准。之后 CompileExternalVTwo→Patch（核对整个 Player/基线不变）→隔离证书/签名 ServerV2→V1/Update/V2/BadPayload 原生门禁；新增 AOT/原生需求拒绝、Domain Reload 关闭双会话、最终保护/meta/独立 Review 仍须完成。
- 后续仍须补齐 T5 清单中尚未交付的官方 Settings/Installer/单独 Deploy 工具按钮、热更 MonoBehaviour Prefab 与 async 实际样例，并在重新正式导出后同步接收工程；目前不能将 T5 整体勾为完成。T6 需要 **Development 与 Release 各一套基座/运行**，当前规划的 Development fixture 不是替代 Release 门禁。以上约束不因已完成的 73 项单元测试或夜间授权而缩减。
- Resource 原有 `ResourceSystemTests` PlayMode **24/24** 回归通过；新四份 PowerShell 工具语法静态解析通过（初次嵌套 -Command 引号调用失败已改为当前 shell 直接解析，不把失败调用计作验证）。四份脚本的执行入口使用实际 pwsh7.6，长期进程继续用 persistent 生命周期与磁盘收据。

### T5/T6 继续实施记录（2026-10-21 夜间）

- T5 尚缺的官方 Settings/Installer/独立 Deploy 按钮已补齐，仍只有显式按钮写入；样例增加真实 `ExampleBehaviour` Prefab 和实际 Task.Yield async continuation，Host 加载代码后才实例化 Prefab 并验证版本属性，关闭顺序为 Module、实例、manifest 屏障、应用资源入口。新增两项用例实际 RED 后 GREEN，工具/样例四项通过，原场景 dirty 不变。
- **实际 SDK 问题修复：** 独立新启动 PackageInfo 的 git.revision/hash 为空（`inspect-official-identity.json`），但实际版本及 UPM manifest/lock 明确为官方 9.0.3/指定完整 commit。现在同时强制 exact version、Git source、官方完整 URL/tag、lock hash，并拒绝非空 API 矛盾值；不因空 API 字段误拒绝，也不允许 Local/Embedded 或宽泛版本回退。失败回归已先红后绿，官方 Installer 在实际新工程成功，两个版本都是 9.0.3。
- **实际 Framework identity 问题修复：** checked DLL 引用 System 4.0 b77，而官方 Unity unityaot-win32 的真实 System 为同版 7cec；不得机械忽略签名。现只接受由已安装 `UnityReferenceAssemblies/unity-4.8-api` 与 `MonoBleedingEdge/lib/mono/unityaot-win32` 精确读取的同名/同版/culture AOT 重定向，热更之间仍完全匹配。真实 DLL 引用回归先红后绿；后续 Strip、FullMethodBridge、Manifest 均已实际通过。
- 正式可选制品 r3 含完整样例；r4 及 r5 为 core-only 追加导出，避免覆盖接收工程显式改为 external Support 的样例作者接线。每次由实际 exporter 生成，原版本和导入收据保留，`product-selection.json` 仅作当前工作指针，基座收据绑定实际制品 SHA。r5 对应当前原子写修复。
- 两个旧路径在 Resource Workspace 严格 Windows 260 预检处拒绝，未发布或写基线，不修改主体路径保护、也不造链接。已按逐文件 SHA 验证物理复制 Assets/Packages/ProjectSettings 到 **`G:/Unity/H26`**，旧 `R` 和 `Temp/H26` 及其失败日志保留；不复制 Library/Temp/obj，最终路径重新正常导入并显式安装官方运行库。owner.project 已指向此路径，runtimeCacheRoot=`G:/Unity/HC26`，持久基线仍为原 Build 下显式位置。复制与 owner 历史分别见 relocation 收据。
- 外部 Support DLL 来自真正 AssemblyBuilder，Framework 编译配置明确为 NET_4_6，v1 编译产物/源 SHA 和 errors=[] 已保存。异步编译后 Unity Editor 退出停滞，不能冒充 clean exit；wrapper 现用 FileSystemWatcher 等原子完成收据，仅在 SHA 核实后对自有进程作有记录的强制清理，不触碰主 Editor。普通同步 -quit 调用均另存真实退出码。
- 原生流程在旧生成目录的 File.Replace 遇到 Windows 错误后中止；新 writer 跳过完全相同 bytes，只对 Win32 1175 在两边 bytes 仍未变化时限次重试，其他错误/部分写均保留失败。两项回归先 RED；主工程完整 `HybridCLRBuildPipelineTests` 缓存结果已实际取回 **30 项、0 non-Passed**。接收工程独立 NUnit 同类测试两次 **29/30**：唯一失败为 -nographics 下真实 Resource 指纹变化保护，不能计为通过；图形 batchmode 的单独实际 Resource Build 与前后输入快照已成功且 changed=[]。测试脚本已改用真实图形上下文复验，不削弱主体检查。
- 旧 batch 已有部分受管 bytes 更新，显式 RecoverPending 检查因此正确拒绝，未认领/删除未知或混合内容。作者显式新增 `GeneratedFresh` 并同步唯一 Resource Project 的目录/selection GUID，旧目录原样保留诊断；没有覆盖旧发布或冻结基座（截至目前尚无成功基座）。当前运行 **`build-clean-native-dev` / `bgp_160553128001EI0sZ0raWTrDJi`** 是新根完整基座门禁，以其 `.json`、真实 exit 收据和基线 marker 为准，不按时间宣称完成。
- 真实公开 loader 的两次 Play/Exit、关闭 Domain Reload 用例 **1/1** 通过；主场景 UIAcceptance/dirty=false、Mono2x/NET_Standard_2_0 和原 EditorSettings 选项已恢复核实。之后整套测试实际结束，但 MCP 在 Play 退出清理连接时未及时回传；已只 reconnect 现有插件实例（不保存/更改 host/token/权限、不重启 Editor），取回 **78 条、0 non-Passed** 的实际结果。新增 atomic 回归后当前主 pipeline 30/30；不把等待超时算成功，也不因 CLI 退出丢掉实际结果。
- 单次独立只读总审查已启动（`ses_e9fa89a37ffekiv9CaktyqqOAt`），不操作任何 Unity 或改文件。仍须完成当前 Dev 基座最终 AOT、同 Player v1→签名 v2、坏 hash/签名/缺 DLL、真实 metadata/部分加载重启、新 AOT 需求拒绝、Release 基座/运行、最终源码/导出/保护/meta 收口；当前没有任何 G3 完成声明。

### 总审查与最新原生事实（2026-10-21）

- 独立审查完成，3 项 Important、0 项已确认 Critical，未给出 Minor。均接受并以失败回归修复：共享 Load 请求的公开观察者抛异常/RemoveAll 不再截断独立内部 Module continuation；会话结束时为所有受理未终态请求恰一次报错、拆订阅并释放自身责任；泛型类型/方法/calli/PInvoke 指纹递归保留每一层完整程序集作用域，覆盖同名不同程序集的嵌套值类型。没有修改 Foundation/Core 公共 API。
- 观察者回归首次实际失败且 cleanup 拒绝，保留该 RED；只退出自有 InitTestScene，并在确认 Unity 实际 jobs=0 后清理本次精确 MCP request id 的 stale lease，不取消/清理其他工作、不重启用户 Editor。pending Module 跨退出 Play 回归实际 RED→GREEN；两项真实跨 Play/Domain Reload disabled 当前 **2/2**，所有 loader/Module PlayMode **20/20**。
- 最新完整 HybridCLR EditMode **86/86**，无跳过（79.7365251秒）；包含审查3项、新增原生 executable mode/output 负例。原 Core Module **28/28**、Resource **24/24**、主体排除全部可选依赖的源码编译 **7/7**、默认真实导出/解包 **6/6** 已重新验证。当前源码脚本编译无错误，两个新增树 missingMeta=[]。
- 最终正式产品包已重新导出到 `H26-20261021/Final` 并以全新真正 `-createProject` 工程验证：默认包先导入、未安装任何官方/测试包时可选完整包再导入均编译通过；只加入官方精确9.0.3后，主体2个与可选/样例6个实际DLL编译通过，未放任何测试/acceptance consumer。证据 `Final/final-supported-import.json`，绑定两个最终包SHA和8个DLL SHA，不靠 tests include 或占位类型兜依赖。G4 当前已取得最终源码制品证据。
- 新基座完整链第一次明确成功：`build-native-dev-short-player.json`、实际Editor exit0、`base-receipt.json`，14阶段均OK且持久完成标记已落盘。Dev CodePackageId=`bcb826d60d7e18753a540f94eddcde0b591c147db064c2d480d53368ff252890`；Catalog=`07e930e3ba0c4948f74f073022cc362c3f6a4218b6dd021c4482381e4bff6424`，Player=`G:/Unity/H26D`，基线SHA=`e33a8a2b83b4b4050e7afff229c6b384043425ecaad340874e89e7b8d3586875`。这证明构建/部署与最终AOT冻结，不自动证明启动消费成功。
- **真实消费端缺陷，不能掩盖：** `native-v1` Player真实exit21、整目录hash未变；acceptance IEnumerator 展平器错误地 Dispose `CustomYieldInstruction/ResourceRequest`，使 Sample 在首次 `TakeResult` 前失败，尚未执行生产加载器。已只修 Tools consumer：自定义等待只观察、不先Dispose；未改产品请求合同或吞错误。为不混用AOT消费端版本，保留原Player/原完成基线，显式新兼容号 `hybridclr-base-v1b`、新基线 `Baseline-v1b`、新Player `G:/Unity/H26D2`、新cache `G:/Unity/HC26B`、新收据stem=`base-v1b-receipt`。当前构建 `build-native-dev-correct-consumer`，persistent id=`bgp_16085cca7001mi2ea9hIm6x8Z2`，不得直接重跑或认为已完成；先查其退出/收据。
- 其他环境问题均保留：没有StreamingAssets标准父目录时部署正确失败，随后正式Recover删其登记stage并显式作者创建父目录；Windows Create Solution=true 生成了真实sln而非exe，不能计Player，现只在自有接收工程明确设置scripts-only=false/createSolution=false并新增生产只读拒绝与真实输出检查；旧长Player路径使Mono不能读取deep StreamingAssets，旧产物保留，改用短绝对输出而非链接/放宽路径检查。
- 下一阶段必须在上述修正消费者的基座成功后：同一真实Player运行V1；异步CompileExternalVTwo（FileSystemWatcher+原子收据，不靠时间猜测），RefreshExternal，PublishPatch，核实Player/冻结基线SHA不变；本地受限HTTPS签名更新的 Check/Prepare/Busy/关闭后Activate/新实例拒绝/重启V2；坏hash/签名/缺DLL、真实metadata部分加载、新native需求拒绝，以及Release独立基座/运行。任何失败先读首因，不以重建基座让补丁通过。Git仍未提交/推送。

## 7. 最终门禁与交付

### 平台事实与裁决

- **Ruling:** 采用精确 neutral culture 规范化而不删除身份字段。实际 SDK 和生成 C++ 证明 `AssemblyName(string)` 对显式 `Culture=neutral` 置 null，FullName 因而省略片段；本地构造真实 AssemblyName 对象、令 CultureInfo=null 的失败回归 RED→GREEN。仅将这条局部语义还原为 InvariantCulture，仍完整比较名称、版本、公钥、schema/hash/CodePackageId 和依赖身份；不改全局 culture 或 SDK。错误裁决的代价是过宽身份接受，因此保留精确原字符串检查而非去掉 Culture 文本。
- **Ruling:** 原生 MVID 在不可逆调用前从真实 PE/ECMA-335 Module metadata 读取。该版本 `RuntimeAssembly::GetManifestModuleInternal` 明确不支持，`RuntimeModule::GetGuidInternal` 明确没有实现；使用 Guid.Empty 或取消 MVID 都不可接受。最小范围检查读取器不引入运行时 dnlib；PE32/PE32+ 与损坏边界 6 项先 RED→GREEN，2048 个确定性变异/截断探测无 CLR 越界错误；本机 55 个真实冻结 AOT/热更 DLL 与 dnlib 的 MVID 全部一致。新增正确 hash/错误 MVID 在 native 前拒绝，重放冲突改为真正不同 bytes 的合法 PE，未削弱合同。读取器错误的代价为误读不可信字节，因此所有偏移/section/metadata/heap 范围均封闭失败并保留原有 hash/预算检查。
- 初期 native-v1/v1b/v1c 的失败、旧 Player/基线均保留，不作为通过。最终用于同 Player 两版证明的是下述 **v1d**，此后生成补丁和负例期间没有重新构建或改写它。普通原生构建与 Player 均读取实际 exit0，异步外部编译的 Editor 强制退出记录单独保留，不冒充 native clean exit。
- HTTPS 初次失败不是放宽 TLS 的理由：服务器诊断确认 Windows SChannel 不支持导入 ephemeral key；只将夹具证书加载改成受支持的 DefaultKeySet，仍是同一证书、精确 loopback pin、原有 Resource 协议与签名，没有安装证书或修改系统信任。服务通过 FileSystemWatcher stop 信号正常退出并 Dispose，4 个服务器均 errors=0；请求数分别为 7、7、7、1。

### 实际结果

| 门禁 | 结果与可复核证据 |
|---|---|
| G0 | 官方 Git 9.0.3/commit `6cadd24d101f4cbd00cd80190106f6621132d24d`、Installer 与本地运行库均9.0.3；旧XF包与自动构建钩子退出，工具链已实际完成IL2CPP构建 |
| G1 | 最终完整 HybridCLR EditMode **94/94**，无跳过（72.9224708秒）；包含严格格式/字节/PE/真实元数据、递归作用域指纹、旧DLL不得伪装成功、基线/输出/取消与真实资源发布 |
| G2 | 最终 loader/Module PlayMode **21/21**；2个真实双Play/无DomainReload用例在EditMode集合中通过；Core原有28/28、Resource原有24/24、主体独立编译7/7、默认导出/解包6/6 |
| G3 | Windows64 IL2CPP Low 的 Development 与 Release 实际基座/运行；v1d同Player签名HTTPS更新/Busy/释放后Activate/同进程新loader拒绝/重启v2全部真实通过；汇总 `native-final-audit.json` |
| G4 | `FinalVerified` 全新工程先导入正式默认包，再在无HybridCLR/无tests/无acceptance设施时导入完整可选包；随后只解析官方9.0.3，2个主体+6个可选/样例真实DLL编译通过；`FinalVerified/final-supported-import.json`绑定最终两个包SHA |

最终 Development 基座：

- 接收工程 `G:/Unity/H26`，Player `G:/Unity/H26D4/HybridCLR.exe`，兼容号 `hybridclr-base-v1d`。
- 基线 `Build/HybridCLR/Reception-20261021/Baseline-v1d`，基线 JSON SHA256=`b659f484f7f4c7f8967235967b161621f00f98b00e25467ae0a04eab591f74af`。
- 全 Player 指纹=`68956288edae769741326530704af6af89514c8082194e99d2ccff4ca6fdde4c`，补丁发布和全部当前Player测试 before/after 恒等。
- v1 CodePackageId=`7e3f297ea9967f7366e7db9e8358d2bd0b259e312b3e2d3a890f5b5da1e4fc0b`，Catalog=`e88b37ead9f0f32b13c07c1aaa5c66fc6cb639c1efc5f09f015c294a620f9dbb`。
- v2 CodePackageId=`62b92e834572af782f7e561b9ff845761f54cbde8ebe957b1df692c8e44ef9de`，Catalog=`d3b5d57d2190a21e629b20be8783eb5b9f0e4d98c885973a843cf1ee8a5dc08c`。只编外部Support DLL并经PublishPatch、正式Builder/Publisher和签名轮换，没有Strip/GenerateAll/Player Build。
- `native-v1d`输出真实 `Native: v1: YL Resource configuration; dependency-count=2; async=v1-async; prefab=v1-prefab`。`native-signed-update`明确记载 Check/Prepare=true、Activate Busy=true、关闭/激活后新实例换版拒绝=true。重启相同EXE的`native-v2-restart`输出相应v2且PackageId/Catalog与补丁一致。
- `native-corrupt-payload` 与 `native-missing-dll` 均 exit0，确认错误在 native 前并不要求重启；`native-bad-signature`按通用失败消费端 exit21 且保留 passed=false，实际错误必须为 `Release signature rejected`，汇总器校验这是预期拒绝而非网络错误，绝不把它直接篡成成功。
- `native-partial-metadata`在已处理前面真实 metadata 后于 `zzAbsentAot` 返回实际 native code **3**，保留 RequiresRestart，跨新loader仍拒绝；并未忽略非OK返回。新增 PInvoke 原生需求的真实外部DLL在 PatchCompatibility 拒绝，Player和基线指纹仍相同。

最终 Release 基座：Player `G:/Unity/H26R/HybridCLR.exe`，兼容号 `hybridclr-base-release`，Development=false、Low。基线 `Baseline-v1d-Release` SHA256=`6a1e749f7c20b3afcfc744207eeb70c448e55900f9280c6679e129b664583d27`，Catalog=`3038a4e1ca775eb2f03f07ebc813e951d9bbed91a018ee61dd3ed2272ccb897c`；`native-release-v2`真实exit0、Simulation=false，config/async/prefab/依赖均v2且Player未改。Release正常传输没有隔离开发证书开关，签名HTTPS轮换在明确Development夹具下验证。

### 最终保护与交接

- 主工程保持 Mono2x / NET_Standard_2_0、非Play、UIAcceptance原场景 dirty=false，EditorSettings原选项恢复；源码 `Runtime`主体、默认Exporter与ProjectSettings的实际tracked diff为空。不新增 Core/Foundation/Resource API，不自动接入现有游戏。
- 用户已有受管非包改动保护指纹最终仍为 `c51c9143e2087d4b183e20975ba082e9facf3844`；原 Common/Equipment 包增量保持。并行新增命名清理Plan、Assets/Test等不相关成果未被接管。index为空，无提交/Push。新树 missingMeta=0，Unity compilationFailed=false，`git diff --check`通过（已有.mcp换行提示不修改它）。
- 独立总审查3项Important全部回归修复；没有已知延后Minor。SDK身份/MVID问题也有真实失败及新原生证明，不拿SDK源码支持或替身当通过。
- 持久正式交付 `G:/Unity/H26Delivery/{YLFramework.unitypackage,YLFramework-HybridCLR.unitypackage}`；默认七根不变、可选完整包含自有样例但不含测试/官方包/dnlib/原始DLL/基线/私钥。`Freeze-HybridCLRDelivery.ps1`只复制显式当前成功/预期拒绝收据、原生日志、可复现Tools与本Plan快照，最终以 `delivery.json` 的逐文件SHA和门禁汇总核实。旧失败日志保留在原evidenceRoot，不删除或混入成功计数。
- 版权信息按既有集中化规则收口：`Assets/YLFramework/LICENSE.md` 为唯一集中声明，并补充官方 HybridCLR 作为独立许可外部包；删除 `Integrations/HybridCLR/THIRD-PARTY-NOTICES.md`、其 meta 及 `Dependencies.json` 的 license/copyright 字段，导出器、测试和 README 同步移除引用。定向 `FrameworkRootCarriesTheConsolidatedLicenseSet` 1/1 与 `HybridCLRIntegrationExportTests` 4/4 通过；另生成修正后的可选包 `Build/HybridCLR/LicenseFix-20261022/YLFramework-HybridCLR.unitypackage`（SHA256 `ec9c273cb4e4fe8fc18fc7fee0da0182cd406841b88e6566ad1e75b9e10bc9ef`），实际包内仅含根 `LICENSE.md`，无额外声明文件。`G:/Unity/H26Delivery` 中已冻结的旧包仍绑定旧源码，未覆盖；需重新导出并完成相应导入验收后才同步，不能把本次源码修正冒充已更新交付。
- 剩余范围限制：不验证High stripping、移动端、其他桌面平台、任意动态泛型、DHE或程序集卸载。额外接收CLI在 -nographics 下29/30的历史失败不隐去；图形上下文中相同生产资源构建输入快照 changed=[]，主工程最终94/94及实际原生发布通过，不为该旧环境失败改弱Resource保护。

## 8. Git 收口（2026-10-22）

- 用户明确要求完成 HybridCLR 模块的 Git 收口并推送远端；仅在现有 `integration/xf-yl` 分支创建本地提交并普通推送至 `origin/integration/xf-yl`，不创建分支、合并、改写历史或创建 PR。提交前本地与远端均为 `b5950825eac886c78fde0a445459af769a97e96a`。
- 本批范围为 `Integrations/HybridCLR` 产品/样例及配套 meta、`Tests/Integrations/HybridCLR` 回归/夹具生成器及 meta、`Tools/HybridCLR` 可复现验收工具、本 Plan、现有 Architecture/IntegrationPlan/README 与根 LICENSE 的直接增量，以及 Packages 中官方 HybridCLR 9.0.3 的 manifest/Unity lock 增量。不暂存 CommonSystem/Equipment 的包增量、其他 Plan、工具配置、游戏资产、空目录 meta 删除或 `Assets/Test` 等无关改动。
- 只读载荷检查确认 `PlayMode/Resources/YLHybridCLRTests` 的 10 个资产及文件/目录 meta 共 22 项由 `HybridCLRTestFixtures.Prepare()` 从本机当前程序集生成，不作为源夹具提交，也不删除本地输入。首次检出或程序集变化后先在非 Play 的 Editor 主线程显式 Prepare，再运行 loader/Module PlayMode；该必要步骤同步至现有 Documentation/README，不通过携带本机 DLL 快照掩盖准备步骤。
- 沿用 §7 已完成的功能、原生与接收门禁证据；版权收口后的集中声明检查 1/1、可选导出/解包 4/4 已实际通过。本轮只整理 Git/文档与检查实际暂存内容，不将其描述为重新执行 IL2CPP 构建或完整技术验收。
- 版权仍只集中于根 `Assets/YLFramework/LICENSE.md`，不重新加入集成内独立声明。修正后的可选包路径及旧 `G:/Unity/H26Delivery` 尚未更新的边界保持 §7 记录；构建包、Player、基线、私钥和本机验收日志不进入 Git。
- 提交前检查完整暂存 diff、meta、凭据与范围，并在提交和 Push 后核对实际 Hash、远端 ref、index 与无关改动保护指纹；最终 Hash 和同步结果随完成回复报告，不为写入本提交自身 Hash 追加文档提交。
- 源码、文档、脚本的暂存空白检查通过；完整 `git diff --cached --check` 仅报告 Unity 自动生成 `.meta` 的空 `userData`/`assetBundleName`/`assetBundleVariant` 值后尾空格。保留这些原生 meta 的字节与 GUID，不批量格式化或重写已验收的资产身份。

## 9. 功能补齐与编辑器体验（2026-10-22）

用户已授权补齐项目级程序集子集、启动进度、显式 Editor 快速模式及配置预检，并优化现有 HybridCLR 窗口。§7 是上一版 Windows64 闭环的历史验收，不代表旧扩展所有用户能力已等价覆盖，也不能替代本节新行为验证。

### 设计边界

- 继续复用官方 9.0.3 全局登记、现有 Resource Recipe/Project、Core Module 和 Foundation async；不引入新包，不修改 Core/Foundation 公共 API，不扩大平台或修改主工程 Mono/.NET Standard 设置。
- `HybridCLRBuildProfile.HotUpdateAssemblies` 选择本项目入口，依赖闭包自动包含；空数组保留现有已保存 Profile 的“全部登记程序集”语义。原生生成仍基于全局登记并集，项目发布只输出其闭包。A/B 项目共用同一冻结原生基座必须有明确绑定与登记证据，不能简单删除 ProfileGuid/ProjectId/兼容身份检查，也不能要求每个资源项目重建一份 Player 来冒充共享基座。
- `StartModuleAsync` 保持原调用方式，补齐真实读取/Core 启动进度与阶段；同步 native 阶段不伪造平滑进度。新增仅 Editor 的 `StartEditorModuleAsync(projectId, entryAssemblyName, moduleTypeFullName)`，不读取代码资源、不调用 native、不生成虚假 CodePackageId，结果模式与发布产物 Simulation 明确区分，不作为正式加载失败后的自动回退。
- 窗口继续使用 IMGUI，中文分区“配置/构建发布/诊断/导出”、显式新建/保存、可编辑 Profile、项目程序集选择、路径选择器、资源映射、配置与原生环境分开预检、阶段诊断；窄窗口退化为下拉导航和滚动内容。打开、绘制、选择配置不保存/安装/生成；既有官方 Settings/Installer、Validate、Base/Patch/Deploy/Export 入口均保留。
- 新建 Profile 显式创建自有 Recipe/Project 与代码目录映射，不创建样例玩法内容；编辑走 SerializedObject/Undo，仅显式保存所选配置。资源映射幂等、保留其他 Group，遇到归属冲突拒绝，不清理用户数据。
- 旧冻结基线、Player、交付目录不覆盖；新增真实验收使用新目录。版权声明只在根 LICENSE，不新增独立声明。当前任务未授权新的 Git 提交/Push。

### 实施与验收

- [x] 读取现有源码、程序集边界与 Resource 工作台模式；旧功能差异已逐项核实。
- [x] 项目子集与工具动作先写 9 个可编译回归，Unity EditMode 实际 RED 9/9，失败原因为缺少 Profile 子集字段和显式 Profile 工具，而非编译错误。
- [x] 实现项目闭包、A/B 子集输出、共享基座绑定与冻结元数据/native 约束；验证非法入口、跨项目共享 DLL 和旧 baseline 行为。
- [x] 实现显式新建配置、幂等资源映射、配置检查与 Undo；9 个 RED 变 GREEN，补共享基座关键回归。
- [x] Runtime 3 项实际 RED 后实现进度/EditorFast；observer、Dispose、失败重试及无 Domain Reload 会话终态回归通过。
- [x] 完成窗口布局与交互，实际检查 1040px 宽窗、540px 窄窗及 940px 构建页，保留四任务页和全部旧操作入口；打开无隐式副作用门禁通过。
- [x] 完成下述定向/原生验证并更新 README/架构；当前不重建旧冻结交付、不扩大平台，不自动 Git 提交/Push。

### 实际结果与边界

- 最终 HybridCLR EditMode **116/116**，无 failed/skipped；包括真实跨 Play/禁用 Domain Reload 会话测试。长调用超过 MCP 响应时限后读取 Editor 的实际完整结果缓存确认 total=passed=116、其他结果为 0、Editor 非 Play/编译/导入状态，没有把传输超时写成测试失败或自行重跑制造通过。
- HybridCLR loader/Module PlayMode **25/25**，连同既有 CoroutineStarter **15/15** 合计 **40/40**。代码资源 pending、Core scene pending 的实际中间进度、EditorFast 零代码读取/零 native/无虚假 CodePackageId、缺入口失败及活动 Module 生命周期均实际验证；没有修改 Core/Foundation API。
- 独立只读审查发现 **3 Important、0 已确认 Critical**；已补 4 项真实 RED 并修复：ResourceSystem 先关闭不能绕过 EditorFast 活动 Module 的 Dispose 门禁；生成目录切换遇旧规范代码选择先拒绝且预检报告，不追加冲突映射；Group ID 及数字后缀按 OrdinalIgnoreCase 避重。原有 observer、共享宿主和跨 Play 用例保持。
- 实际窗口截图仅针对 Unity 进程的准确 HybridCLR 窗口，宽/窄配置及构建页可读、任务导航窄窗退化、长路径与说明滚动；图片位于 `C:/Users/Kano/AppData/Local/Temp/kilo/H26UI-20261022/{empty-wide,profile-wide,profile-narrow,build-wide}.png`。自有可视夹具资产已删除，不遗留 Profile/Recipe/Project 到源码工程；本轮未声称浅色主题实测。
- 最新产品-only 可选包 `C:/Users/Kano/AppData/Local/Temp/kilo/H26-20261021/YLFramework-HybridCLR-projects.unitypackage` 导入自有 `G:/Unity/H26` 后正常 exit0。仅更新本叶子的 Runtime/Editor 与集中声明，不恢复接收端已替换为外部 DLL 的 Sample.Support 源码，原游戏/主工程 Mono 配置不动。
- 新原生 Player `G:/Unity/H26S/HybridCLR.exe`、新基线 `Build/HybridCLR/Reception-20261022/ProjectSubsets`，Windows64 IL2CPP Development，兼容号 `hybridclr-project-subsets-v1`。真实基座构建 exit0：A manifest 只有 Support，但冻结 InitialManifest 全局登记 Entry+Support；B 不同 ProjectId/ProfileGuid 显式绑定 A，正式 PublishPatch 首发 Entry+Support 依赖闭包，实际阶段没有 Player/Strip/原生生成，exit0。
- `project-subsets-base.json` / `project-subsets-patch.json` 的全部 Player 指纹均为 `36137ae637edcc949b3d6cc230e2ce4d93592d25a85539f6edba028fe6e1d72d`；基线 JSON SHA256 `d614e72b49d498eeb54d27621225a403e7c9cc21c1ec048c299bbb935c6ec82d`、全基线指纹 `88e439eb97ac1602983e2345d76879b24dabf26ea6cc0a651219c74f1fb5e26a` 前后不变。旧 H26D4/H26R、旧基线与交付未覆盖。
- `native-project-subsets.json` 真实 exit0、`passed=true`、两次 `Mode=Native/Simulation=false`：A 只加载 Support，B 加载 Entry+Support，两个 Loader 的 Support 为同一 Assembly、AppDomain 中仅 1 份，执行结果 `v2-async`，两个 barrier 均释放且 ResourceStartup 正常停止。证据同在 `C:/Users/Kano/AppData/Local/Temp/kilo/H26-20261021`。这次使用明确的可信本地 A/B 发布快照，不冒充重验 HTTPS 签名更新，也没有启动固定旧 ProjectName 的示例 Module；原生 Core 场景/Prefab及 Release/HTTPS 是 §7 的历史证据，不外推为本轮全部重验。
- 窗口新动作、模式与共享基座用法见本叶子 README；不同历史项目版本的共享 DLL 组合仍需应用协调，不能仅凭同一 native 基座宣称任意组合一致。所有补丁继续拒绝未登记程序集、新 metadata/native 需求，平台仍不扩展至 Windows64 之外。
- 导航反馈追加修正：旧 Toolbar 自动套用首尾 18px 样式；仅改名虽然统一尺寸，却丢失原生 Skin 的选中态绘制，先前尺寸门禁不足以覆盖交互。最终改用保留原生 Skin 的四个显式等宽 30px Toggle，选中项加下划线，窄窗仍使用 Popup。新增实际 MouseDown/MouseUp 的 `2→3→4→1` 往返点击回归，并保留尺寸/字体门禁；工具测试最终 12/12。本追加仅涉及窗口和测试，不冒充重跑原生 Player。

## 10. 本批 Review 与 Git 收口（2026-10-22）

- 用户明确要求 review 本批增量并完成提交/远端推送。本地与 `origin/integration/xf-yl` 在收口前均为 `508a32519ecbbedfffeaca8663d37d53d029f327`；只在当前分支创建一次本批提交并普通 Push，不改写历史、不新建分支/PR。
- Runtime/共享基座和 Editor/配置交互分别由独立只读审查覆盖。Runtime 侧无阻断发现；Editor 侧确认 2 项 P2：canonical 代码目录的文件夹别名/重复选择可误判、已删除目录可因历史 GUID 被接受。3 个 Unity 回归先实际 RED，再共用 `ReadCodeSelection` 验证唯一/无别名/标准选择，并先确认真实文件夹存在；工具测试 15/15，原审查者复核原发现已解决，无新增阻断。
- 工具审查另确认首次 `Install-HybridCLRReceptionHarness.ps1` 漏拷新增 Probe、只有 Sync 拷贝，会令首次接收缺少编译类型。补齐首次安装复制项。自有临时文件复制夹具先因缺 Probe 失败，再验证 Runner/Probe 两份实际源文件均精确复制；夹具只复用历史真实导入 receipt 作为输入门禁，不冒充一次新的 Unity 导入/编译/native 验收。
- 最终完整 HybridCLR EditMode **121/121**，0 failed/skipped；包括实际导航 MouseDown/MouseUp 与跨 Play/no-Domain-Reload 回归。MCP 长调用超时后以完整实际结果缓存核实 total=passed=121、其余 0，Editor 已退出 Play/编译/导入。Runtime 未再变动，沿用 §9 的 HybridCLR PlayMode 25/25、CoroutineStarter 15/15及真实 Native A/B 证据；本轮不重复原生基座构建或声称重验 Release/HTTPS。
- 本提交范围为本叶子产品/README、3 个受影响测试文件、新 ProfileTools 及原生 meta、4 个受影响验收工具脚本、新 Probe、现有 Architecture/IntegrationPlan 与本 Plan。`Packages`、`ProjectSettings`、主体 Runtime/Editor、其他 Plan、`.mcp.json`、原游戏、`Assets/Test`、孤立 meta 和本机生成的 PlayMode Resources 均不提交，也不删除本地文件。
- `8865e4731e16cf73e3bf957bf3a66a0d7f670204` 为其他受管改动保护指纹；无关未跟踪内容保护指纹为 `d02a336a8fdcca47e13306fc48ae91c3ac348003`。提交/推送后核对保护指纹、远端 ref 和暂存区；最终 Hash 在完成回复中报告，不为记录本提交自身 Hash 追加 docs 提交。旧冻结交付继续保持 §7/§9 边界，不随 Git 收口冒充已重新导出。
