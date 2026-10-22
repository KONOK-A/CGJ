# HybridCLR Integration

本目录是可选叶子，不随默认 YLFramework 包导出。先导入默认框架及其 UGUI、Newtonsoft.Json 依赖，再安装官方 `com.code-philosophy.hybridclr`：

`https://gitee.com/focus-creative-games/hybridclr_unity.git#v9.0.3`

解析 commit 必须为 `6cadd24d101f4cbd00cd80190106f6621132d24d`。显式运行官方 Installer，确认 package 与本地 libil2cpp 均为 `9.0.3`。不包含官方包、运行库、dnlib、XF 或 XFAB 副本。支持目标边界为 Unity `2022.3.62f3c1`、Windows64、IL2CPP、.NET Framework；只有实际完成的原生验收才证明相应环境可用。

## Build

1. 在官方 HybridCLRSettings 配置明确的热更 asmdef/外部 DLL 名单。AOT 不可静态引用热更类型，不使用 Assembly-CSharp 或手写加载顺序。
2. 创建并保存 ResourceProject、BuildAndPublish ResourceBuildRecipe 和 HybridCLRBuildProfile。Profile 只引用 Recipe，并配置 Development、自有 Assets 生成目录及持久基线目录；身份、平台和兼容号从 Recipe 读取。基线必须在 Assets/Packages/Library/Temp 之外。
3. 用独立接收工程显式配置 Windows64、IL2CPP、.NET Framework、C++ 工具链和 Windows SDK；生成可运行基座时 `Build Scripts Only` 与 Windows `Create Solution` 都须为 false，导出 Visual Studio 工程不是 Player 成功。Validate 不自动安装或切换设置。当前目标和 Development 必须完全匹配，所有输入资产须显式保存。CI 的资源构建沿用正式 Resource 指纹检查；未验证的 `-nographics` 环境不能据成功日志跳过该检查。
4. 应用显式准备标准 `Assets/StreamingAssets` 父目录（不手建或认领其下的 YL 受管根），再在 `YLFramework/Integrations/HybridCLR` 窗口或 `HybridCLRBuildPipeline.BuildBasePlayer` 提交新 Player 路径和与 enabled scenes 相同的场景列表。流程依次检查编译、生成、剥离、完整桥、metadata/manifest、正式 Resource 发布、显式内置部署、真实 Player 和最终 AOT 基线。任一步失败都不是基座成功，读取报告的首因及已完成阶段；部署失败按正式 Resource Recover 验证 journal，不手工补拷或删除旧发布。
5. 更改热更代码后，只调用 `PublishPatch`。它读取持久冻结 AOT 和完成标记；不调用 Strip/GenerateAll、不构建 Player、不改变基座。新 AOT/原生需求、程序集集合变化、损坏基线或身份不符需新兼容号和新基座。静态 analyzer 不能承诺任意反射/动态泛型均安全。

Recipe.Project 必须明确选择 `Generated/hybridclr` 下的 `.bytes`/`.json`，以生成根为 SourceDirectory，得到规范地址 `hybridclr/manifest.json`、`hybridclr/assemblies/*.dll.bytes`、`hybridclr/metadata/*.dll.bytes`。不要选择 owner 记录、原生生成目录或原始 `.dll`。不要修改受管生成文件、遗留未知文件、链接或认领已有用户目录；写入保留 GUID、manifest 最后提交，中断时保留诊断而不冒充可恢复成功。

`BuildBasePlayer`、`PublishPatch` 接受应用注入的 `IResourceContentKeyProvider`；保密密钥不存 profile。签名更新继续使用既有 Resource 签名、Check/Prepare/Activate、发布版本与下载验证，不建立第二套下载器或密钥系统。

## Runtime

先启动并挂载 ResourceSystem，再构造 `new HybridCLRLoader(resources, appContentCompatibilityId)`。调用 `LoadAsync` 或 `StartModuleAsync(manifestAddress, entryAssemblyName, moduleTypeFullName)`；请求可 yield，提供 `isDone/progress/error`、原始 Exception、阶段/程序集/native 错误码和 RequiresRestart。类型只在精确入口程序集内解析。`Abort` 总在改变状态前拒绝。

加载器借用 ResourceSystem。先验证全部 bytes，再在主线程加载 SuperSet metadata 和依赖优先程序集；只保留一个小 manifest lease 作为活动代码版本屏障，不保留大 DLL 资源租约。可以 Check/Prepare，但活动闭包 Activate 返回 Resource Busy。先关闭本入口启动的 Module，再 Dispose loader，最后由应用关闭自己拥有的 ResourceStartup。Dispose 不关闭外部服务、不卸载 DLL，也不允许同进程换版；同名不同内容、未知来源已加载或部分 native 失败需要重启进程。

MVID 从已校验 bytes 的 PE/CLR Module metadata 在不可逆调用前读取，使用范围检查的最小读取器，不依赖运行时 dnlib；当前 IL2CPP 未实现 CLR ManifestModule/GUID 反射，不能用它的空值表示验证通过。对 IL2CPP 显式 neutral culture 解析差异只做 invariant culture 规范化，其余完整身份、版本、公钥、hash、MVID 与 CodePackageId 比较保持严格。

Editor 始终标为 **Simulation**，仅复用 AppDomain 中已经加载的精确程序集，不重复 Assembly.Load。外部独立 DLL 在 Editor 不能模拟；Editor metadata 返回 OK 不等于 IL2CPP 验收。关闭 Domain Reload 也不会卸载程序集。

## Sample And Export

独立样例位于本可选根的 `Samples`，而非默认主体 `Samples` 根。显式调用 `HybridCLRExampleAssets.Create(newAssetsDirectory, persistentBaselineDirectory)` 生成自有项目、分组、Recipe 和 Profile，不更改官方设置。把 `YLFramework.HybridCLR.Sample.Entry` 和 `YLFramework.HybridCLR.Sample.Support` 配成热更程序集。Host 只保存入口字符串，不静态引用热更类型；Entry 依赖 Support，并通过既有 Core 配表路径创建 Model。

作者工具同时创建附加真实热更 `ExampleBehaviour` 的 Prefab。Module 通过 Core 读取资源配置并执行支持程序集中的泛型/async 调用；Host 在加载 DLL 后才通过 Resource 实例化该 Prefab，并检查热更脚本与异步结果，不在初始化 Catalog 时推断尚不存在的热更 ScriptableObject 类型。

应用显式持有 `HybridCLRExample`，yield `Run(configuration, projectId, compatibilityId)` 后读取 Result；关闭时 yield `CloseAsync()`。更新实验可先显式 `CloseCode()` 关闭 Module、实例和 manifest 屏障，待原生销毁排空后 Activate，但仍须新进程运行另一版代码。配置可由 ResourceLocalSnapshot.OpenBuiltIn/ArtifactStore 构造。样例不自动启动、找全场景或接管现有游戏。只改 Support 的版本文本可做 v2 补丁实验，但正式证明仍须相同 Player hash 下的新进程原生执行。

`HybridCLRIntegrationExporter.Export(absolutePackagePath, includeSample: true)` 显式导出本叶子的源码、asmdef 和依赖说明。默认不带样例，不递归依赖，不带 Tests、官方包、生成 DLL、基线、StreamingAssets 制品、私钥或仓库验收工具；默认框架导出的七个根保持不变。

当前已实际验证 Windows64 IL2CPP Development 与 Release Low 基座/运行，以及不改同一 Player 的签名 HTTPS v1→v2、重启屏障、坏 hash/签名/缺 DLL、真实部分 metadata 失败与新原生需求拒绝。该结论不覆盖 High stripping、其他平台或任意动态泛型。一次性执行证据、环境故障与精确验收范围保存在专项 Plan，不以本说明替代实际结果。
