# Resource：配置、加载与资源生命周期

> [文档首页](../README.md) · [架构总览](../Architecture.md) · [阶段 8 Plan](../../../../.kilo/plans/xf-yl-8-resource.md)
>
> C02、E05、B06 和 E04 G1～G3 已通过用户技术验收，E04 G1～G4正式交付及上轮技术审核结论沿用；C05 技术完成、待用户验收，整体8.9未COMPLETE。**B02与F03技术完成，待用户验收**；实际范围、样本、限制与保护检查见原Plan §28.11，不据此改变其他项目验收状态。**远端安全更新仅支持WindowsEditor，Windows Player当前明确拒绝，不是仅缺测试。** 平台边界见文末。

## 正式入口

`YLFramework.Services.Resource.ResourceSystem` 是每个 Play/Player 会话唯一的资源权威，管理 Catalog、共享生产、租约、实例、Scene 和版本事务。读取 `Current`、构造配置或打开工作台都不初始化；资源操作与 Unity 对象访问在主线程执行，只读配置不代表入口线程无关。

- 应用接入：[`ResourceStartup`](../../Runtime/Systems/Resource/Integration/ResourceStartup.cs) 显式启动/关闭并管理自身责任；高级调用方可直接使用 `ResourceSystem.InitializeAsync`，不是第二套内核。
- [Runtime](../../Runtime/Systems/Resource/) / [Editor](../../Editor/Systems/Resource/) 分属 `YLFramework.Runtime` / `YLFramework.Editor`，namespace 为 `YLFramework.Services.Resource` / `YLFramework.Editor.Resource`；主体依赖 UGUI/Json.NET，不依赖 XF/XFAB。
- 旧 Manager/Loader、ResData/BinaryFormatter 与模拟 HotUpdate 栈已退役，不提供旧表、XFAB 或另一资源权威的自动 fallback。

## 初始化与持有

### 选择配置

| 来源 | 正式配置入口 | 前置条件 |
|---|---|---|
| 仅本地/URL 图片 | `new ResourceConfiguration()` | 无 Catalog，也须显式启动 |
| Resources 内存目录 | `new ResourceConfiguration(catalogs, images)` | 调用方明确提供目录，不扫描旧设置 |
| 正式 Bundle | `ResourceConfiguration.Create(ResourceLoadMode.Bundles, mounts, cacheRoot, images)` | 明确项目闭包、版本/兼容信息和编译类型 |
| Editor 模拟/Bundle 选择 | `ResourceEditorSimulation.CreateConfiguration(mode, projects, mounts, cacheRoot, images)` | 仅 Editor 使用；不因 Bundle 错误回退模拟 |

Bundle mount 复用 `ResourceProjectMount`。不传 `cacheRoot` 时每项目须有完整本地快照且不接受 remote；传入时装配原 ArtifactStore，创建配置本身不 Mount、建缓存目录或下载。Simulation 不接受 cacheRoot；可使用当前定义，或传完整版本闭包冻结身份。没有隐式 Auto 模式，Player 工厂拒绝 EditorSimulation。

下面是完整的本地发布配置方法。参数来自应用明确选择的交付，不扫描“最新”目录；自定义类型通过已编译的 `typeof(...)` 传入，不按远端字符串反射创建。

```csharp
using System;
using System.Collections.Generic;
using YLFramework.Services.Resource;

public static class ApplicationResources
{
    public static ResourceStartup Configure(string absoluteVersionDirectory,
        string projectId, string digest, string platform, string appId,
        IEnumerable<Type> compiledTypes = null,
        bool bindCore = false, bool bindAudio = false)
    {
        var snapshot = ResourceLocalSnapshot.Open(absoluteVersionDirectory,
            projectId, digest, platform, appId);
        var project = new ResourceProjectMount(projectId, platform, appId,
            snapshot, assetTypes: compiledTypes);
        var configuration = ResourceConfiguration.Create(
            ResourceLoadMode.Bundles, new[] { project });
        return new ResourceStartup(configuration, bindCore,
            bindAudio ? new[] { projectId } : null);
    }
}
```

### 启动与关闭

1. 保存配置与启动对象，显式调用 `StartAsync()`，等待并 `TakeResult()` 后使用 `Resources`；按需读取 `CoreProvider`、`AudioProviders[projectId]`。
2. 业务结束先归还自身请求/Scope、实例、播放和 Scene 责任，再等待 `StopAsync()` 成功。完整协程见正式样例，不以 Dispose 启动请求代替关闭。

- 配置身份按**同一对象引用**判断：同配置 join，不同配置冲突，不要反复重建配置来假装 join。每个启动对象只管理自己的接入责任。
- `OwnsResourceSystem` 区分自建与借用；借用会话不会被 Stop 关闭。自建会话遇其他 startup/live owner 返回 Busy，保持可恢复，不强制归还 Lease 或卸载外部 Scene。
- 装配失败只清自身责任；若自建会话被独立 owner 阻塞，`Resources` 仍保留，错误说明原因，归还后继续 `StopAsync()`，不能丢掉关闭责任。
- Core/Audio 默认不接入。Core 外部绑定冲突明确失败，归还只解绑自身引用，不清后来替换者；Audio 装配不启动或关闭全局 AudioKit，不改用户开关/路由/缓存。
- 取消 Start 等待者不伤同入口其他等待者；取消 Stop 只退出等待，已开始的 native 排空继续。旧会话失效后先 Stop 旧对象，再显式重新启动。

### 取得与归还

| 需求 | API / 请求结果 |
|---|---|
| 主资产/子资产 | `Acquire<T>` / `AcquireAsync<T>(ResourceAddress)`，独立 `AssetLease<T>` |
| Bundle 内 typed 批量 | `AcquireAllAsync<T>(projectId, bundleId)`，`AssetBatchLease<T>`；仅 Catalog 登记的可赋值成员，bundleId 不是磁盘路径 |
| 普通 Group | `AcquireGroupAsync(projectId, groupId)`，`AssetBatchLease<UnityEngine.Object>`，不加载 Scene |
| 混合组预加载 | `PreloadGroupAsync(projectId, groupId)`，`ResourceGroupLease`；普通资产、SceneBundle/ScenePreview 各自明确 |

`ResourceAddress` 使用稳定 ProjectId、区分大小写的逻辑地址及可选子资产 key，不是 Bundle 文件名。完整身份共享 producer，每个成功消费者有独立责任；同步遇在途返回 InProgress，不忙等或重复 IO。

`yield return request` 只等待；拥有型结果由 `TakeResult()` 恰一次交割。未领取 Dispose 释放结果，领取后 Dispose 请求不释放 lease。Scope 聚合自己的请求/租约，不维护第二缓存；取消不误杀他人，迟到 native 仍有清理 owner。Resources/AssetDatabase 是 borrowed，归零只 drop 记录；只销毁系统自建对象。

## 可运行最小接入

[`Samples/ResourceStartup/`](../../Samples/ResourceStartup/) 的 Runtime/Editor 分 asmdef，不引用 MainModule、XFAB、测试或机器临时路径；默认框架导出包含样例源码，生成的应用数据不在导出根。

1. 无脏场景的 Edit Mode 执行 `YLFramework/Samples/Create Resource Startup Example`，新建唯一 `Assets/YLResourceStartupExample[后缀]/` 的 Text/WAV/Data、Project 与独立 Scene，使用新 GUID；不会替用户保存原脏场景。
2. Play 中选择组件，保持 `Mode=EditorSimulation`，点 `Run Selected Mode`。同一 `Run` 读取 `message`/`data` 并按选项接入 Core/Audio；Scene/Inspector 加载不自动启动。
3. 退出并保存示例 Scene，点 `Build And Publish Example Bundle`，由正式 Builder/Publisher 写入真实 `SnapshotDirectory`/`CatalogDigest`；保存后明确切为 Bundles，再使用同一运行按钮。
4. 成功显示 `Passed: Hello from ResourceStartup / Explicit compiled sample type`。自定义 Data 在 `CreateMount` 有明确 typeof；本次 ProjectId/Core/Audio 选择启动时冻结，Inspector 中途修改仅影响下次。
5. Audio 被应用 Sound 设置或接纳策略拒绝时，按应用设置处理或关闭 `UseAudio`；样例不强开偏好。外部责任导致 Busy 时用 `Stop / Retry Shutdown`，不调用全局 AudioKit.Shutdown/Clear。

加密仍使用同一份样例和 `Run`：在生成的 `Project.asset` 选择 `ContentEncryption=None` 或 `Aes256GcmHkdfSegmentedV1`，加密时填写非秘密 `ContentKeyId` 并显式保存。应用在运行前设置 `example.ContentKeyProvider = applicationOwnedKeys`；该字段不序列化，选择配置不会取key或初始化。Editor构建按钮将同一provider传给原Builder/Publisher，不添加第二初始化入口。

非生产开发可在Inspector填**进程环境变量名**，点 `Inject Development Environment Key Provider`，复用正式的32字节Base64校验；不在Inspector输入key。未显式注入时，原Editor构建合同仍读取 `YL_RESOURCE_CONTENT_KEY`，但这不等于Runtime样例已获得key。进入Play/域重载后要重新注入，再按原按钮运行。此便利入口仅供开发，Player由应用注入provider，不自行下载key，不依赖EditorPrefs或开发机环境。缺/错key按下文错误处理，不转为Simulation、重下载或别的版本。

要交付内置资源，退出 Play 后保持已选的确切 `CatalogDigest`，先点 `Preflight Selected Built-In Release` 查看来源、目标和内容，再点 `Deploy Selected Release To Built-In`。成功仅将样例 `UseBuiltIn=true`，不启动服务、不代保存 Scene、不自动改为 Bundles。明确选择 `Mode=Bundles` 并保存自己的应用 Scene 后再运行；后续 Build 会选中新发布并重置 `UseBuiltIn=false`，须再次明确预检/部署。中断使用 `Recover Built-In Deployment`，再按结果重新预检。

Player 使用合法 Bundles 配置调用 `BeginBundles()` 或 `Begin/Run`。`UseBuiltIn=true` 时只使用 ProjectId/Platform/App/CatalogDigest 经过 `OpenBuiltIn` 定位，不使用开发机的 `SnapshotDirectory`。这是正式内置路径接入，不开放远端 gate，也不代表 Player 首次启动与持久重启已验收。

## 实例与场景

- `Instantiate/InstantiateAsync` 交付 `InstanceLease`；`Return/Dispose` 回缓存，`Discard` 销毁。复用 Foundation 桶，默认 scene-scoped；退休后的晚归还实例销毁，active/idle/destroy-pending 真实结束前均持 Prefab 图。
- `ClearInstanceCaches()` 只清本系统缓存，不全局清池；直接 prefab、Attach/场景实例不自动接管资产。业务仍须归还自己借出的实例。
- `LoadSceneAsync` 返回 SceneRequest，使用真实 handle/序号；显式门控通过 `Activate()` 放行，默认期限 30 秒，范围 `(0,300]`，不能无限占住 Unity async queue。`SceneLease.Dispose()` **不卸载 Scene**，须显式卸载并等真实 sceneUnloaded。
- Single 开始后取消不能恢复旧 Scene；最后 Scene 卸载可 Busy；外部同 path 并发歧义保守保活。特定同短名跨 path Scene Bundle 有 Unity 2022.3 限制，不承诺该组合或 native 多版本热共存。

## Core 与 Audio

- 使用 ResourceStartup 的可选 Provider 装配后再启动命名 Module；直接 InitializeAsync 的高级调用方自行显式绑定。Core `ProjectName` 是 Resource ProjectId；直接 prefab、Attach、默认 Canvas/EventSystem 不要求初始化资源，Module Close 不自动卸外部 Scene。
- Audio 使用 `AudioClipAddress(resources.GetAudioClipProvider(projectId), logicalAddress)`；每次成功交付独立 clip lease，播放/尾轨/缓存负责消费期持有。直 Clip 是 borrowed，不由 Resource 或 Audio 擅自卸载。
- AudioClipLibrary 的旧枚举及隐藏 BundleName 仅供持久化迁移，不是旧后端。显式迁移要求地址映射，只创建新 GUID 副本、不改原资产；详见 [Audio 素材组](Audio.md#素材组与组件)。

## 图片与图集

- `AcquireImage/AcquireImageAsync` 接受完整 HTTP(S) URL 或绝对本地 PNG/JPEG。冷 URL 同步 Unsupported，在途 InProgress，Ready 可同步取得；同步本地读取不占异步队列。
- ResourceImageOptions 默认编码上限 32MiB、16M 像素；idle LRU 为 128 项/32MiB RGBA 估计/30s TTL；并发默认 4、可 1～32，按 FIFO 调度，不是 priority。活跃 owner 不淘汰，普通 Catalog Texture 不进入此 idle 缓存，归零按其 Source 归还。
- ResourceImage 绑定 Image/RawImage，`SetImage(location/address)` 持自己的 Request/Lease，临时 Sprite 自毁、placeholder 借用；刷新/替换/停用/销毁按代次收尾。Refresh 不保证绕过同 URL 缓存，换代使用版本 URL；ClearImageCache 只清 idle。
- 显式 Atlas 必须在专用根、非 Variant、Include in Build 关闭、Sprite 名唯一，并作为主资产入 Group。首次/修改 packables 后，保存配置并显式 `PrepareAtlases(project)`，再构建；准备会更新选中 Atlas 缓存，不在预检中偷偷写全工程。
- Image 使用 `SetAtlasSprite(atlasAddress, name)`；非 UI 用 `AcquireAsync<SpriteAtlas>`。消费者销毁 GetSprite 克隆，再归还 Atlas Lease，不能销毁共享 Texture。默认编译类型表已含 SpriteAtlas，内部打包 Texture 不公开成子资产。
- 自动生成见下节；Variant、全局 atlasRequested/Prefab Sprite 透明晚绑定仍未实现。显式 V1/V2 构建与自动 V2 图集的证据不外推其他引擎/平台；未知 native 布局拒绝。

### 自动 SpriteAtlas

自动图集是 **Editor 规则 -> 自有 Atlas 主资产 -> 原 Group/BuildMap/Bundle/Catalog** 的正式生产流程，不是另一个 AtlasManager、Runtime 映射表或透明 Sprite 替换服务。输出为 `.spriteatlasv2`，只准备本配置拥有的图集，Include in Build 始终关闭；源 Texture/Sprite Importer、手工 Atlas、全局图集设置和 Scene/Prefab 不被修改。

1. 在 `ResourceProject` 配置独立 `AssetRootGuid` 和目标 Group，例如资源根 `Assets/MyResources/`，来源放在 `Sprites/`。Group 可只由自动图集组成，不必添加虚假的手工条目。
2. 选中 Project 资产，在 Inspector 显式保存定义，再用“登记自动图集范围”新建 `GeneratedAtlases` 直接子目录。已有目录（即使为空）、文件或孤立 `.meta` 均拒绝接管；该目录连同 `AutomaticAtlases.asset`、Atlas 和对应 meta 由该 Project 登记并维护，须一并保留。不要手工断开 `AutomaticAtlases` 引用，丢失/换绑原身份会阻止构建而非悄悄漏掉图集。
3. 在配置 Inspector 的 `Rules` 添加规则：`Id` 为稳定技术键，`GroupId` 对应已有 Group，`Sources` 选目录或已按 Sprite 导入的 Texture2D。目录递归收集 Sprite 纹理（支持 Multiple Sprite），普通非 Sprite 纹理不自动改 Importer；空目录不会产生空 Atlas。同一纹理不能进入多个自动规则，每个图集的 Sprite 名必须非空且唯一。
4. 选择 `Together` 将整条规则聚为一图，`Address` 即完整逻辑地址，例如 `atlases/ui`；`PerFolder` 按每张纹理所在文件夹分图，地址追加资源根相对目录，例如 `atlases/ui/Sprites/Icons`。PerFolder 身份以源文件夹 GUID 而非路径/成员摘要决定，移动或重命名该文件夹仍保持图集 GUID，但派生地址随目录变化。需要跨移动稳定的单一业务地址时用 Together。
5. 配置 Padding（2/4/8）、MaxTextureSize（32..8192 二次幂）、旋转/紧密打包、Readable、MipMaps、sRGB、FilterMode 和压缩；默认不旋转、不紧密、无 mip、不可读、Bilinear、2048、Uncompressed。过大内容由 Unity 多页承载，不按不稳定成员 hash 重命名。Sources 不能与生成范围重叠，包括选择其祖先资源根；不要把输出或配置再当来源。
6. “保存规则”后用“只读预览”查看 Create/Update/Current/Obsolete，再点“生成 / 更新并准备图集”。生成会仅对明确来源候选进行必要的正常同步导入，避免 PNG 已变但缓存仍旧；不改其 Importer 设置，不替用户保存未提交的 Importer 改动，遇 dirty 来源明确拒绝。也可直接执行正式 Workbench/API Build，Builder 会先生成/更新并准备自有图集，再冻结输入、分析依赖和构建；不需要再手动将输出加入 Group。
7. 存续 Atlas 保留原 `.meta` 和 GUID，相同有效输入不改资产字节。改地址/设置不会改 GUID；改规则 Id 则是新身份，旧输出转为 Obsolete。失效输出退出正式选择但不自动删除；Inspector 的“检查引用并删除失效输出”或 `DeleteObsolete(project, key)` 会重新检查归属和当前资产/资源定义引用，仍被使用则拒绝。

原始文件夹 Group 选择会排除已登记生成范围，避免重复收集配置、失效输出或自动 Atlas；自动输出只按规则明确进入目标 Group。手工直接选择该范围、生成文件或重复选择同一输出不是另一种合法登记方式。规则和生成配置均纳入原构建输入后验；只读 BuildMap/模拟索引遇待生成/更新会明确拒绝，不读取旧发布冒充新结果。

自动化复用同一入口，下面方法的前置条件是 Project 已保存、`ui-atlases` Group 已存在、参数为该专用根内已导入的 Sprite Texture2D；登记不是对已有任意目录的认领授权：

```csharp
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;

public static class ApplicationAtlases
{
    public static ResourceAtlasConfiguration Configure(ResourceProject project, Texture2D sprites)
    {
        var configuration = ResourceAtlasGenerator.CreateConfiguration(project, "GeneratedAtlases");
        configuration.Rules.Add(new ResourceAtlasRule
        {
            Id = "ui", GroupId = "ui-atlases", Address = "atlases/ui",
            Grouping = ResourceAtlasGrouping.Together,
            Sources = new Object[] { sprites }
        });
        EditorUtility.SetDirty(configuration);
        AssetDatabase.SaveAssetIfDirty(configuration);
        return configuration;
    }

    public static ResourceEditorBuildReport Build(ResourceProject project)
    {
        Debug.Log(ResourceEditorOperations.Preflight(project, "app-v1"));
        return ResourceEditorOperations.BuildAndPublish(project, "app-v1");
    }
}
```

也可显式 `ResourceAtlasGenerator.Preview(project)` / `Generate(project, cancellation)` 后调用原 Builder。`Preview` 不导入、保存或准备；冲突、非法配置、未知文件、缺失输出/meta、外部修改明确抛错，不能按普通 Update 强盖。待生成时 `ResourceEditorOperations.Preflight` 报告计划，并说明尚未完成最终依赖/Workspace 检查；正式 Build 仍执行生成后的全部验证。

普通生成异常/取消会回滚本次仍能确认归属的输出和登记写入，并停止本次构建；回滚只处理实际开始写入的资产对，写入前与恢复前均复验，不会删除被回调占用但自己尚未写入的路径。若写入后该路径又被外部修改，保留冲突内容并报告原异常与恢复异常，不强行覆盖；磁盘 meta GUID 也会与身份核对，不能只靠 AssetDatabase 缓存。旧有效图集不会被拿来冒充新构建成功。

应检查 `BuildError`、`PublicationError`、`GenerationError` 各阶段结果，后者是既有**资源常量**生成错误，不代表自动 Atlas 成功与否。成功的 `ResourceBuildResult.AtlasGeneration` 提供本次生成前计划；不承诺进程崩溃/断电的绝对原子恢复。外部修改请恢复已登记 Atlas/meta 的原字节，或在人工确认引用和归属后另设新范围，不自动接管或删除未知资产。

发布后用普通 `new ResourceAddress(projectId, "atlases/ui")` 取得 `SpriteAtlas` Lease，或 `ResourceImage.SetAtlasSprite(address, "SpriteName")`。GetSprite 的克隆由消费者销毁，Atlas Lease 持有期内共享 Texture 仍由原 Bundle 所有权保活；不要 Destroy 共享 Texture，不必注册 `atlasRequested`，也不会自动给 Prefab 中的 Sprite 做晚绑定。

当前验收为 Unity 2022.3.62f3c1 / Windows Editor / StandaloneWindows64 的自动 V2 图集；生成身份、冲突/失败保护、正式构建/Catalog、packed preview 与 Runtime 实际像素和 Lease 生命周期已验证，并完成一次独立 Review 及定向修复。B05 的显式 V1/V2 图集与 Runtime 合同继续保留；Player、其他平台、Variant 与透明晚绑定不由这些证据外推。具体叶结果、过程异常与保护摘要统一记录在阶段8 Plan，不把整体8.9或8.10标为完成。

## 预加载与诊断

- PreloadGroupAsync 领取的 ResourceGroupLease 在持有期保活：普通成员取得资产，SceneBundle 只准备 Bundle，Simulation 的 ScenePreview 只验证路径，**均不加载 Scene**。required 失败整批回滚，optional 保留 cause；普通 AcquireGroupAsync 不替代混合组预加载。
- GetDiagnosticsReport 按需复制 owner、producer/waiter、IsQueued、Bundle 根 pin/入边、实例、Scene 与版本责任，不持有 Unity 对象或初始化。历史默认关闭，由 ConfigureDiagnosticsHistory 显式开启。
- 构建 Placement/SourceBytes/Dependents 与实际 FileBytes/SHA 分开；源文件字节、压缩制品、native memory、累计 IO 不是同一指标，不能以共享引用次数判重复打包。

## Resource Workbench

1. 打开 `YLFramework/Resource/Workbench`，创建自有 ResourceProject，明确 ProjectId、独立根 GUID、Group 和 GUID 选择。预检检查地址/类型/子资产/根/依赖边界，不接管原 Importer 标记。
2. 选择可 Undo，保存仅针对目标配置；构建前处理脏场景/配置。Together/PerAsset 是意图，最终包服从共享依赖、Scene 分离和环归并，报告提供原因。
3. ResourceProject.Compression 仅 Lz4/Lzma/Uncompressed，恒 StrictMode；压缩进入 Workspace 身份，有显式 Force Rebuild，不接受任意 flags。只构建已激活、已安装、主机可 native 读回的桌面目标，不切平台。
4. Publisher 输出正式 resource-catalog.json 与 release index；BuildId 标识 payload，CatalogDigest 标识最终 bytes，不自写 CatalogDigest 字段，摘要不等于签名。历史选择、构建、发布、生成分别报告，不以一个成功替代另一个。

Workspace 按 Project GUID/设置隔离，不可变发布在 `AssetBundles/YLFramework/<ProjectId>/<Platform>/<CatalogDigest>/`。ReadHistory/ReadVersion 仅选 owned 完整版本；ExportRelease 只向工程外不存在的 digest 目录导出并复验。未知文件/归属、链接和篡改拒绝，不覆盖旧目录。旧 Service/ResService、BuildScript/AssetBundleExporter 与标记菜单已退役。

### 可共享构建配方

`ResourceBuildRecipe` 是保存在 `Assets/` 的普通配置资产，可随工程共享。通过 `Create/YLFramework/Resource/Build Recipe` 创建，在 Inspector 选择明确 `Project`、`Target`、`AppContentCompatibilityId`、`Action`、`ForceRebuild` 及必要的常量生成选项，再点“保存所选配方”。Workbench 的“可共享构建配方”选择入口可打开同一 Inspector；不依赖窗口存活、Selection、EditorPrefs 或开发机临时路径。原不使用配方的工作台、`BuildOnly`、`BuildAndPublish`、Builder/Publisher 调用及默认行为保留。

- 资源选择、Group、Compression、ContentEncryption/ContentKeyId 始终来自引用的 `ResourceProject`，配方不覆盖、不复制、不临时改写 Project，也没有多级继承。
- `Target` 必须明确、已安装且已激活并匹配本机可读回的桌面目标；配置不会切平台。`Action=BuildOnly` 不发布/生成；`BuildAndPublish` 才可选择 `GenerateReferences` 和 `ReferenceDirectory`，常量仍由原生成器及原完整发布集合决定。
- “查看有效执行选项”/`ResolveRecipe` 只解析非秘密值，不取 key、不构建/生成/发布/初始化。`ExecuteRecipe` 要求配方与 Project 已显式保存，开始时将执行选项复制为只读 `ResourceBuildInvocation`；配方后续编辑影响下一次。资源输入继续由原 Builder 冻结并做完整后验，无法冻结的回调继续阻止整体 skip。
- Inspector 与无窗口入口都调用 `ResourceEditorOperations.ExecuteRecipe`。结果 `Invocation`/`Summary` 说明配方路径、实际 Project/GUID、目标、App、操作、force、压缩/交付和生成选项；配方名称/GUID不加入构建内容身份。不把发布成功等同于常量生成成功，应分别检查 `BuildError`、`PublicationError` 和 `GenerationError`。
- 密钥与签名私钥不在配方中。加密继续传应用持有的 `IResourceContentKeyProvider`，或沿用原明确进程来源 `YL_RESOURCE_CONTENT_KEY`；查看/保存配方不读取环境值。配方不签发、不部署 StreamingAssets、不上传或激活 Runtime。缺 key、无效目标、取消与失败沿用原 owned 锁/路径/保旧合同。

已实际编译并在自有 None 夹具执行的最小无窗口例为 [`ResourceBuildRecipeExample.Build`](../../Samples/ResourceStartup/Editor/ResourceBuildRecipeExample.cs)；同一 `ExecuteRecipe` 的外部provider加密发布另有直接门禁：

```csharp
using YLFramework.Editor.Resource;
using YLFramework.Samples.Resource.Editor;
using YLFramework.Services.Resource;

public static class ApplicationResourceBuild
{
    public static ResourceEditorBuildReport Build(string recipeAssetPath,
        IResourceContentKeyProvider applicationKeys = null)
    {
        return ResourceBuildRecipeExample.Build(recipeAssetPath, applicationKeys);
    }
}
```

在已稳定的 Editor 主线程传入工程内所选配方资产路径；无需打开窗口。配方路径只是加载配置的入口，不是输出路径；输出仍由原 Workspace/Publisher 决定。此例只证明无窗口 Editor 调用，**未交付通用CLI/batchmode入口，也不声称独立CI验收**。

### 代表性成本与复跑

B02/F03本批正式记录在[原Plan §28.11](../../../../.kilo/plans/xf-yl-8-resource.md)，不代表8.9整体完成或C05验收。F03沿用G3原Windows64 Mono Player的正式本地证据，在其原源码/载体/窗口下解释；本批新增的构建、回环下载及共享消费数据均为Windows Editor，不将G0/B1原型或Editor数据冒充新Player结果。

本批固定约23.5MiB资源集：2048²+1024²无mip RGBA32纹理、3个共享Material/大纹理的Prefab、20s stereo PCM及独立Text，Uncompressed+StrictMode；每个主对照3样本、排除明确warmup。数据分列native/指纹后验/SHA/编码或密文复验/复制/验证；无源变化仍因原6个callback门禁强制native重建，只对经过认证的未变密文及不可变发布实际复用，不能把它称为整体no-op。

- 全量HTTPS实际payload为None24,654,402B、Encrypted24,664,643B；真实取消partial为4,194,304B offset，固定同一发布/强ETag，续传剩余实际为12,694,914/12,700,876B。64KiB/请求2ms分块的本机受控下载流水线中位约8.635/8.627MiB/s，全量Prepare约3.835/4.122s；包含SHA/IO/调度，不是socket带宽或公网/CDN承诺。
- 共享消费以正式Startup/System及原Asset/Instance/Audio provider取得7 Bundle闭包，12个Prefab消费者保持到峰值后才归还；512次held-hot获取/释放中位约1.522/1.398ms是整段总量，不是每帧成本。首次异步Acquire包含SHA/调度/native/物化交接，不能当纯CPU或主线程卡顿。
- 128MiB是显式内容数据预算，不是总内存限额。Encrypted 7个held输入共1,376,032B，另有声明Unity read buffer229,376B、CNG key-object逻辑22,834B；资源对象、Profiler/managed、进程内存分别看待，采样峰非绝对瞬时峰，allocated与reserved各自峰不相加。真实归还与Stop后按Lease/实例/输入/在途/预算/CNG责任验证，不用全局UnloadAll制造归零。
- 原始Mono进程计数器“成功返回0”已被Review判无效；working-set/private-memory统一按`null/Unavailable`，不从原始零字段推断占用。8192B分配能力探针亦失败，分配字节为null。原始JSON不覆盖，资格化统计明确排除失效值；没有可靠Audio欠载计数，不以isPlaying或静音播放证明无毛刺。

小型复跑入口及资源生成方式保留在 [`ResourceCostBaselineTests`](../../Tests/EditMode/Resource/ResourceCostBaselineTests.cs) 与其 `.Network.cs`/`.Consumers.cs`，不是只能运行Temp可执行文件。先用实际验证过的准备脚本创建**工程外不存在的新目录**与非生产回环凭据：

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File "Assets/YLFramework/Tests/Tools/Prepare-ResourceCostEvidence.ps1" -OwnedRoot "$env:TEMP\kilo\resource-cost-run"
```

父目录须已存在；脚本拒绝工程内路径、链接或既有目录，不覆盖旧证据，不启动Unity/构建/发布，不导入系统信任。凭据仅供自有loopback测试，不能用于应用内容密钥、生产签名或生产服务。

完成Unity导入/编译、确认原场景已保存后，在当前唯一Editor主线程的**测试域**调用。调用方需能引用`YLFramework.Tests.EditMode`，或使用可见该程序集的Editor动态执行；不把测试依赖带入Runtime/Player：

```csharp
var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kilo", "resource-cost-run");
YLFramework.Tests.ResourceBuild.ResourceCostEvidence.UseLoopbackEnvironment(root);
try
{
    YLFramework.Tests.ResourceBuild.ResourceCostEvidence.Run(root, "cost-01", new[]
    {
        "YLFramework.Tests.ResourceBuild.ResourceCostBaselineTests"
    });
}
catch
{
    YLFramework.Tests.ResourceBuild.ResourceCostEvidence.RestoreLoopbackEnvironment();
    throw;
}
```

整fixture A/B/C必须同一suite执行，B/C复用A的确切固定导出，不逐请求重新加密；每sample新Store、不清OS缓存或用户Library。测量会拒绝dirty/无持久路径场景，在自有空Scene运行后恢复原setup，不在MainModule中benchmark。`Run`立即返回TestRunner运行ID而非通过结论，实际耗时可达数十分钟；超时继续回收同一次运行，不重复启动。以`<root>/cost-01/finished.json`的`PassedGate`、suite终态及`EnvironmentRestored`为准，不能仅看leaf绿。当前正常收尾按原值校验恢复临时环境，失败缓存保留并记录owner，成功仅清本fixture登记资产/Workspace/Store/Scene，原始样本和导出保留。

原始证据及完整分项/范围索引：`%TEMP%/kilo/b02f03-20261017-0902/f03-baseline-02/`、其父目录的`f03-baseline-02-summary-qualified.json`与`f03-evidence-qualification.json`。总样本量只支持中位数、范围和样本最坏，不称总体P95；原始采样器身份保留，Review后的资格修正不伪装成整套重新测量。未新增独立Player、batchmode/CI、公网、OS冷盘、精确socket/SHA CPU或Audio欠载结论；Scene既有限制及g36未知事件继续见原Plan，不借本批追认修复。

## 交付与挂载

- 内置身份约定 `YLFrameworkResources/<ProjectId>/<Platform>/<CatalogDigest>/`，由应用明确 digest。`OpenBuiltIn(projectId, digest, platform, appId, resourcesRoot)` 只读定位；默认根为 `Application.streamingAssetsPath/YLFrameworkResources`，不扫描最新、不创建或复制目录。自有部署外壳会被完整验证后适配到内部 `payload~`，既有普通导出布局仍走原严格 Open，不能把 `payload~` 直接传给普通 Open 绕过外壳。
- 将已验证快照及编译类型组成 mounts，显式传 cacheRoot 才装配原 ArtifactStore；有效 active/LKG 优先，无有效持久版本再回退明确内置，整组选择不拼代。仅消费内置不建立持久 cache。
- 正式 payload 的闭合文件集合连 `.meta` 也不忽略。安全部署与 Unity 元数据边界见下节，不以工程外导出或导入前复制成功冒充真实 StreamingAssets 验证。
- 更新要求已配置 ArtifactStore。顺序为 CheckUpdatesAsync（本地完整项目闭包）或 CheckRemoteUpdatesAsync -> PrepareUpdateAsync -> 应用归还责任 -> ActivatePreparedUpdateAsync；Prepared 可查询/续用，但 **Prepare 不等于激活**。
- Check 纯读，Prepare/取消不写 active；激活 Busy 后旧版仍可用，from 变化返回 StaleBase。RollbackAsync 显式选 LKG 并走同一闸门，不是随意激活旧 prepared。CleanupVersionsAsync 安全清理 owned 内容；清理前查询见下节。
- 远端须应用注入公钥/端点/channel/最低序号，由原签名、HTTPS、ETag/Range 和事务链处理。签名不证明最新，不防本地状态恶意回滚；历史导出不等于 Runtime 回滚。当前非 WindowsEditor 后端明确 Unsupported。

## 安全内置部署

正式 Editor 入口为 [`ResourceBuiltInDeployer`](../../Editor/Systems/Resource/Publishing/ResourceBuiltInDeployer.cs)，只在已稳定、非 Play/编译/导入中的 Unity 主线程使用。Builder/Publisher 仍不隐式写 StreamingAssets；部署不会初始化 ResourceSystem、启动 Core/Audio、修改 active/LKG 或强制卸载现有资源。

```csharp
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;

public static class ApplicationBuiltInDelivery
{
    public static ResourceBuiltInPlan Inspect(ResourceProject project,
        string catalogDigest, string appId)
    {
        var release = ResourcePublisher.ReadVersion(project,
            EditorUserBuildSettings.activeBuildTarget, appId, catalogDigest);
        var plan = ResourceBuiltInDeployer.Preflight(release);
        Debug.Log(plan.ToString());
        return plan;
    }

    public static ResourceBuiltInDeployment Deploy(ResourceBuiltInPlan plan)
        => ResourceBuiltInDeployer.Deploy(plan);
}
```

1. 在 Publisher 历史中明确选择完整版本，将确切 digest 传给 `ReadVersion`，再调用 `Preflight(release)`。也可对已用严格 `ResourceLocalSnapshot.Open` 打开的完整发布快照调用 `Preflight(snapshot)`；两者都不会猜最新、建目录、加锁或恢复写入。异常说明来源、目标或归属冲突，不能按“目标为空”自动认领。
2. 检查 `plan.Source.Document`、`ProjectGuid`、`DirectoryPath`、`Files`、`PayloadBytes`、`WorkspacePath`、`IsRepeat`，然后显式 `Deploy(plan, cancellation)`。部署会重新预检，预览不是跳过复验的授权。结果提供实际 `Snapshot`、相对 StreamingAssets 的 `RelativeDirectory`、`CopiedBytes` 与 `IsRepeat`，身份仍以 `Snapshot.Document` 为准。
3. 将结果的 ProjectId/Platform/AppContentCompatibilityId/CatalogDigest 记录在应用配置；运行时通过下面的身份工厂定位，不序列化 Editor 发布绝对路径。多个必需项目逐项明确部署，再显式组成完整 mounts；Catalog v1 不引入跨项目依赖或另一套全局闭包权威。

```csharp
using System;
using System.Collections.Generic;
using YLFramework.Services.Resource;

public static class ApplicationBuiltInStartup
{
    public static ResourceStartup Configure(string projectId, string digest,
        string platform, string appId, IEnumerable<Type> compiledTypes = null,
        string privateCacheRoot = null)
    {
        var snapshot = ResourceLocalSnapshot.OpenBuiltIn(projectId, digest, platform, appId);
        var mount = new ResourceProjectMount(projectId, platform, appId,
            snapshot, assetTypes: compiledTypes);
        var configuration = ResourceConfiguration.Create(ResourceLoadMode.Bundles,
            new[] { mount }, cacheRoot: privateCacheRoot);
        return new ResourceStartup(configuration);
    }
}
```

随后沿用前文 `StartAsync/TakeResult`、Scope 取得资源与 `StopAsync` 的责任顺序。无 cacheRoot 时直接消费严格内置快照；有明确应用私有 cacheRoot 时仍复用原 ArtifactStore 的 active/LKG/内置优先级。Store 重开快照会连同原外壳一起复验，不仅验证内部文件；首次仅回退内置不会因此建立持久目录。

**布局与元数据：**正式文件位于 `Assets/StreamingAssets/YLFrameworkResources/<ProjectId>/<Platform>/<CatalogDigest>/payload~/`。root/project/platform 各有 `ylr-owner.json~`，版本外壳有 `ylr-deployment.json~`。Unity 2022.3 不导入 `~` 后缀目录/控制文件；正常目录 `.meta` 仅允许紧邻明确 owned 的 root/project/platform/digest 目录，且须为合法 folder meta。`payload~` 不生成目录或内部文件 meta，内部 Catalog v1/Complete/资源字节保持原样；外壳 receipt 与 Complete 的 ProjectGuid/身份交叉核对。额外 payload `.meta`、孤立 meta、未知文件/目录、损坏归属及 symlink/junction 均拒绝，不靠全局 ignore、删除 Unity 生成文件或关闭刷新实现。

**提交与恢复：**外部 `AssetBundles/YLBI_<token>/` 仅为本操作 workspace，完整 GUID/平台/工程根仍由 owner 校验，短 token 碰撞只能拒绝。journal 登记本次 staging 的闭合范围，复制、校验与目标复查完成后，将包含完整版本的最外层缺失节点搬入目标；相同完整版本幂等，不覆盖或自动删除旧部署。提交前取消/失败保留旧版和可恢复的私有 stage；提交后异常不代表未安装，必须调用 `ResourceBuiltInDeployer.Recover(project, target)`（或明确 projectId/platform/ProjectGuid 的重载）。记录保留至真实导入、严格复验及结果构造全部成功；已安装完整版本返回确认结果，未安装则仅清理 journal 登记的 stage 并返回 null，重新预检部署。未知或损坏控制记录拒绝自动删除/修补；Recover 不是任意目录清理授权。多项目分别部署不是绝对断电原子事务。

部署只允许新建或已验证由新 YL 管理的 `YLFrameworkResources` 边界，不认领已有空/未知根，不改父目录已有 meta，不触碰原 MainModule、其他 StreamingAssets 或用户文件。stage/锁/journal 均在 StreamingAssets 之外，不进入 Player 或正式 payload；本入口不提供全局旧版本垃圾回收。

真实 AssetDatabase 刷新后的严格读取、两个明确版本、重复部署及同一 ResourceStartup 业务的 native Bundle 读取已在 Windows Editor 验证，测试内容及对应 meta 已按归属清理；证据、Review 与完整边界见阶段 Plan 28.8。`~` 目录在真实 Player 交付包中的保留、Windows Player 首次启动及更新后持久重启仍须 8.10 验证，未进行本批 Player 构建；Android/JAR/WebGL 提取、其他主机和远端 Player gate 不在本批范围。

## Store 只读查询

正式入口为 `ResourceConfiguration.QueryArtifactsAsync(completed, maxEntries, cancellation)`，也可直接调用已配置的 `ArtifactStore.QueryAsync(...)`。两者均返回由调用方驱动的 `IEnumerator`，不创建协程宿主；每次 `MoveNext` 和 `Dispose` 都应在 Unity 主线程进行。无需先初始化 ResourceSystem，不 Mount、不下载、不 Prepare、不 Recover、不清理，不创建 root、缓存、staging 或锁文件。**不要用原 `GetPreparedUpdates()` 替代只读查询**：该入口会恢复元数据，并重建可操作的 PreparedUpdate。

下面的方法可由现有 MonoBehaviour 的 `StartCoroutine` 驱动。`cacheRoot` 必须是应用明确管理的绝对私有目录；项目/平台闭包必须与该 Store 的 owner 完全一致。此例只查询，不要求 built-in 快照，不初始化资源服务，也不会认领一个已有但无 owner 的目录。

```csharp
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using YLFramework.Services.Resource;

public static class ArtifactQueryExample
{
    public static IEnumerator Inspect(string cacheRoot, CancellationToken cancellation = default)
    {
        var store = new ArtifactStore(cacheRoot, new[]
        {
            new ResourceProjectMount("my-project", "StandaloneWindows64", "app-v1")
        });
        var configuration = new ResourceConfiguration(store);
        ArtifactStoreReport report = null;
        var query = configuration.QueryArtifactsAsync(value => report = value,
            maxEntries: 10000, cancellation: cancellation);
        try
        {
            while (query.MoveNext()) yield return query.Current;
        }
        finally { (query as IDisposable)?.Dispose(); }

        Debug.Log($"Store={report.Status}; observed={report.ObservedBytes}; " +
            $"sizeComplete={report.IsSizeComplete}; candidates={report.CleanupCandidateBytes}");
        foreach (var issue in report.Issues) Debug.LogWarning(issue);
        foreach (var entry in report.Entries)
            Debug.Log($"{entry.ProjectId}/{entry.Platform} {entry.Kind} {entry.State} " +
                $"{entry.Path}; bytes={entry.ObservedBytes}; sizeComplete={entry.IsSizeComplete}; " +
                $"protection={entry.Protection}; candidate={entry.IsCleanupCandidate}; " +
                string.Join("; ", entry.Issues));
        // 不根据 Path 或旧 candidate 自行删除。实际清理另行显式调用 CleanupVersionsAsync。
    }
}
```

- `Projects` 是明确配置的项目/平台/App 闭包，不扫描整个 persistentDataPath 或工程寻找其他 Store。`Entries` 包含版本、staging、partial 文件组、控制元数据及受管层级中遇到的未知项。`PreparedUpdates` 只复制记录身份和每项目 from/to，不生成可激活对象。
- `Protection` 是可组合的 `Active`、`LastKnownGood`、`PreparedFrom`、`PreparedTo`、`Mounted`、`Held`、`InFlight`。active/LKG 的完整镜像及 Prepared 整组闭包复用原校验；Mounted/Held/InFlight 来自当前 authority 和仍排空的旧会话，与正式清理共用持有快照来源。`ProtectedVersions` 也保留磁盘条目之外的明确内置/持有路径，不遍历它们。版本操作在途时通过 `InFlightOperations` 和 Busy 受限结果报告。
- `CompleteRecord` 仅代表有效 owned Complete 身份及登记文件集合，**不是 Bundle 内容 SHA-256、可挂载性或签名新鲜度认证**。查询校验必要控制记录和 Complete 身份，不默认重读全部 Bundle；原清理仍保留其完整复验。已知残余目录可为 `Incomplete` 且仍是原规则允许回收的 owned remainder。损坏记录、未知内容、外来归属、链接和读失败不成为候选。
- `ObservedBytes` 是普通文件的逻辑长度，包含实际观测到的数据和控制文件；partial 的 `.json`、`.part`、`.next` 作为一组只算一次，版本/staging 父项和其文件不重复计数。`IsSizeComplete=false` 表示缺少可读统计或未知目录内部未遍历，此时字节数仅是已观测下界，不是完整的 0。未知目录、symlink/junction 目标不扫描。`CleanupCandidateBytes` 仅在 Ready 时统计已确认候选的逻辑字节，不等于实际磁盘释放空间或 native memory。
- `NotConfigured`、`Missing`、`RecoveryRequired`、`Invalid`、`Unreadable` 分别表示无 Store、root 不存在、待显式恢复、无效归属/控制闭包、读取失败；不是同一种空目录。Ready 只表示本次保护上下文/扫描完成，各条目仍须查看 `State`、`Issues`、`IsSizeComplete`。局部版本读取失败会保留为 Unreadable 条目；控制闭包无法确认时整体不提供候选。
- 每段最多 32 个步骤，默认总上限 10000，可显式选择 `1..100000`。上限含必要复查，达到上限返回 Incomplete；取消返回 Canceled。中止枚举器须 Dispose，直接中止不会产生报告。文件系统单次调用仍受 OS IO 延迟影响，不承诺硬实时帧预算。
- 查询不占用整个扫描期的 Store 写锁。任一进程内 Store 事务、相关会话或持有集合变化返回 Changed；当前操作/已有外部写锁返回 Busy。观测路径的属性、长度和 mtime 变化也使结论失效。所有非 Ready 结果的候选标记均撤销、`CleanupCandidateBytes=null`，须在正常操作结束后重新查询。该边界针对遵守既有主线程/Store 协调的调用，不是跨进程文件系统原子快照，也不防绕过合同并伪造相同属性的外部篡改。

报告和集合均为脱离内部记录的只读副本，不增加资源 pin，不持有 Unity 对象。报告只描述 `StartedUtc..CompletedUtc` 的一次观测；取得报告之后任何保护变化都可使预览过时。实际删除必须显式通过已初始化服务的 `CleanupVersionsAsync()`，重新核对当前归属、active/LKG、Prepared、在途和持有责任；API 不接受旧报告作为删除授权。

## 代码生成

- 输入为正式发布 Catalog；默认文件 `Assets/YLFrameworkData/YLResourceReferences.cs`、namespace `YLFramework.Resources`，仅生成 ProjectId/Addresses/Aliases/Groups/SubAssets 键，不是可变目录或加载器。
- 程序集 `YLFramework.Generated.ResourceNames` 不依赖框架/Unity/Editor/插件；预定义程序集自动引用，业务 asmdef 显式引用它及 YLFramework.Runtime。
- 碰撞/类型校验先于写入，未知/篡改文件与孤立 meta 不接管；幂等更新保留 GUID，失败保留旧输出。发布、生成、Unity 消费者编译是三个结果；生成物不属默认框架导出。

## 常见错误

| 结果 | 处理方式 |
|---|---|
| ConfigurationConflict | 重用同一配置对象；核对已有 Resource/Core 绑定，不强制重置外部会话 |
| Busy | 查看 Diagnostics，归还自身 Lease/实例/Scene/Audio 或其他 startup，再重试；不全局清缓存/强停业务 |
| AddressNotFound / TypeMismatch | 核对 ProjectId、逻辑地址、子资产 key 和 compiledTypes，不把磁盘名作地址 |
| 快照/兼容验证失败 | 核对确切 digest、目录、平台/完整 Unity 版本/App 标识和文件完整性，不改成模拟来源 |
| StaleBase | 当前版本已变化，重新 Check/Prepare；要回退则明确 Rollback |
| SessionUnavailable | 旧请求/Provider 不能绑定新会话，结算旧启动对象后显式启动 |
| Unsupported | 检查模式/平台/来源及是否配置 Store；不是自动忽略或降级指令 |
| ContentKeyUnavailable | 向应用 provider 配齐当前项目/KeyId 后显式重试；不是文件损坏，不自动下载或切换版本 |
| ContentAuthenticationFailed / ContentFormatUnsupported | 拒绝当前输入及其依赖闭包的新取得；核对确切交付、模式与 key，不回退明文/Simulation |
| ContentBufferUnavailable | 释放本应用拥有的输入责任后显式重试，或在下次启动配置足够预算；不会自动扩容或驱逐活跃输入 |

## 可选 Bundle 内容加密

E04 G1～G3已完成正式接入及Windows Editor/Windows64 Mono Player代表性技术门禁，并获用户技术验收。以下是正式配置与使用合同；G1～G3证据见原Plan 28.10.19，G4交付与定向回归见28.10.20，不以旧G0/B1原型替代，不代表8.9整体完成。

- Project 的 `ContentEncryption` 默认 `None`，旧明文发布不需重写。`None` 继续 `LoadFromFile`/`LoadFromFileAsync`，Editor Simulation、Resources 和直接图片来源不改变。新增默认字段不进入 None 的 build settings 身份；正常保存 SO/代码输入变化仍可能使旧 input fingerprint 自然失效一次。
- 启用 `Aes256GcmHkdfSegmentedV1` 时，填写仅为标识的 `ContentKeyId`，覆盖该 Project 的内容包、共享依赖包和二进制 root manifest。`.manifest` 文本旁车不加密。正式后端固定为 Windows CNG `Microsoft Primitive Provider` AES-256-GCM，官方 BC 2.7.0 netstandard2.0 仅提供 HKDF-SHA256；不复制系统 DLL，不静默软件 GCM fallback。
- 首批支持范围仅 Unity 2022.3.62f3c1、Windows x64 Editor/Mono Player。其他环境的加密明确拒绝，不能据 None 可编译推断加密受支持；IL2CPP、Unity 2023/Unity 6 等不在本轮支持范围。

Editor 使用显式进程 secret 名或调用方 provider，不在 SO、Inspector、EditorPrefs、指纹或日志中保存 key。工作台默认进程变量为 `YL_RESOURCE_CONTENT_KEY`，严格接受 Base64 编码的 32 字节；非秘密 KeyId 与环境变量名不是密钥材料。

```csharp
project.ContentEncryption = ResourceContentEncryption.Aes256GcmHkdfSegmentedV1;
project.ContentKeyId = "content-v1";
var keys = ResourceEditorOperations.ContentKeyFromEnvironment(
    project.ProjectId, project.ContentKeyId, "YL_RESOURCE_CONTENT_KEY");
var build = ResourceBundleBuilder.BuildWithContentKey(
    project, BuildTarget.StandaloneWindows64, keys);
var release = ResourcePublisher.PublishWithContentKey(build, "app-v1", keys);
```

选择None时令 `project.ContentEncryption = ResourceContentEncryption.None`，使用原 `Build` / `Publish` 即可，不需要key。构建与发布成功后只记录返回的确切ProjectId/Platform/App/CatalogDigest；应用明确 `ResourceLocalSnapshot.Open` 的发布目录或 `OpenBuiltIn` 身份，不猜最新目录。后者不读取 `SnapshotDirectory`、raw workspace或开发机发布绝对路径。

raw workspace 仍供原 BuildPipeline 与图集前置使用，独立 delivery 在 Catalog 封口前生成。Catalog v1 的 length/SHA 始终描述实际交付字节；Report 分列 raw/delivery SHA、字节及编码成本。同身份/key 的候选仅在本次完整认证成立时复用确切密文，新加密使用新随机材料；同一 KeyId 换材料不会偷偷重新加密为成功。轮换使用新的 KeyId/配置分区。重复发布也需当前 key 及内容验证；失败/取消不切 index、不破坏旧版本。

Runtime 的应用 provider 实现 `IResourceContentKeyProvider.TryCopyKey(projectId, keyId, destination)`，向框架给出的确切 32 字节目标复制 key。provider 生命周期归应用，框架仅持自己的副本/派生状态；应用回调只发生在主线程，不发生在 native Stream 回调。不要在 provider 异常文本中携带 key；框架保留诊断因果但不串出其敏感原文。

```csharp
var config = ResourceConfiguration.Create(
    ResourceLoadMode.Bundles,
    mounts,
    cacheRoot,
    decryption: new ResourceDecryptionOptions(appOwnedKeys, 128L * 1024 * 1024));
var startup = new ResourceStartup(config, bindCore: true);
using (var start = startup.StartAsync())
{
    yield return start;
    start.TakeResult();
}
using (var request = startup.Resources.AcquireAsync<GameObject>(address))
{
    yield return request;
    using var prefab = request.TakeResult();
    // Consume prefab.Asset while its lease is held.
}
using (var stop = startup.StopAsync())
{
    yield return stop;
    stop.TakeResult();
}
```

以上是协程主体，`mounts`、`cacheRoot`、`appOwnedKeys`、`address`由应用显式提供；只消费本地/内置时可不配置cacheRoot。请求失败须读取 `Error` 并结算已取得责任，再按合同重试；不要吞错后继续取资源。完整错误收尾和Busy重试见可运行样例。实例、Audio尾轨或Scene的业务责任也须归还后才能Stop；归还SceneLease不等于卸载Scene。

- 同步取得如实承担完整 SHA/输入准备成本；异步入口的纯 IO/SHA/认证可取消并在 worker 执行，应用须等待请求终态后 `TakeResult`，不能假设方法名为 Async 却总是即时完成。native 开始后不靠提前关闭输入抢占取消。
- 每个共享 BundleRecord 只有一个输入。输入与 CNG owner/预算保持到真实 native unload；SceneLease 归还不等于 Scene 卸载，实例 destroy-pending、Audio 尾轨及缓存仍沿原责任图结算。实例化、Ready 复用、Group、Scene、Audio 缓存的新借出均执行依赖闭包故障准入；旧已交付对象不因首 fault 被强销毁。
- 64KiB 分段、两个明文缓存槽和 32KiB native read buffer；单输入固定数据缓冲为 196576B，默认 128MiB 预算不预分配整笔。新增闭包先原子预留，全部 live 输入与在途预留计量，共享不双计；不足明确失败后显式重试。`GetContentDiagnosticsSnapshot()` 与 `GetDiagnosticsReport().Content` 分列数据预算、native bridge 声明值及进程内该适配器的 CNG provider/key/key-object 成本，绝不是整个密码模块或进程总内存上限。
- 初始化成功前只对当前所选项目/KeyId 集合做一次轻量认证，同配置 join 不重复；不要求未选历史 key，不以创建 native Bundle 代替探针，全 None 不执行额外密码工作。Prepare 全 To 闭包、激活/回滚在原提交边界前完整认证，持有稳定输入到提交结束。恢复、Mount、Check 和 E05 查询不取 key、不解密；缺 key 不隐式重下载或切 LKG。
- 冻结 Catalog v1 不代表旧客户端支持密文。签发加密 release 必须显式确认渠道只投放支持 E04 的客户端；原签名 payload 不加字段，不能将同一渠道继续分发给不支持加密的旧客户端。Runtime 远端 Windows Player 硬门禁仍然关闭。

### 轮换与交付

- 轮换创建新的KeyId和密文发布，先Check/Prepare再在业务neutral边界激活；保留必要旧key才可显式回滚到旧密文LKG。缺旧key明确失败，原active/LKG及发布序号合同不改变。回滚目标仍由原LKG选择，不把“历史目录存在”当作回滚授权；回滚兼容明文LKG不需要内容key。
- 远端length、SHA、Range、ETag描述**同一份固定密文**。续传须有实际partial、强ETag与If-Range，并按确切offset追加；服务器200忽略Range要重置，实体变化要重新获取，错误/损坏不能提交半成品。最终还须通过内容认证和原Prepare/激活，不能把HTTPS或SHA成功当作解密成功。缺/错key不是网络重试理由。
- C02 `Preflight/Deploy/Recover`复制/验证交付字节，不需要内容key，不改Catalog/Complete/密文；真实导入后通过 `OpenBuiltIn` 严格定位到 `payload~`。应用运行时仍须提供key。E05原查询入口无需key、初始化、恢复或解密；统计实际密文与控制文件的逻辑字节，预览不授权删除，清理仍复查最新保护状态。
- **Resource发布快照**使用 `ResourceEditorOperations.ExportRelease(release, destination)`，目标为工程外已有本地父目录下**不存在且等于CatalogDigest的子目录**；精确交付Catalog、Complete和登记文件，不包含raw缓存或内容key。
- **YLFramework框架包**使用原菜单，或 `YLFramework.Editor.ExportUnityPackage.Export(absolutePackagePath)`向现有本地父目录导出。默认拒绝覆盖，不保存其他资产；菜单保留原显式保存/覆盖流程。导出根与Recurse不变，包含源码、必要meta、样例、批准的BC DLL及完整BC/Tink许可；不含Tests、原游戏、XFAB、临时runner/缓存/私钥/证书或系统CNG DLL。
- BC确切来源、官方2.7.0 netstandard2.0二进制SHA及Tink参照声明见 `Plugins/BouncyCastle/SOURCE.txt`，完整许可在同目录。现有UGUI/Json.NET等依赖不因导出而消失；商业插件既有公开再分发限制保留。实际导出和源码/二进制闭包不代替阶段9干净工程完整验收。

**保留限制**：Unity Scene native 接手后发生延迟 Stream 读取失败可能引发 native 崩溃，既有 G0/C1 证据仍保留，未被本轮修复或记为受控失败通过。不重新演示该崩溃，也不将合法输入、定位/竞争/提前释放或取消/退出/预算的新缺陷归入该豁免。native 前完整密文 length/SHA、首末认证、读取时每段认证后交付，以及发布/Prepare/激活/回滚的完整认证均不降低。不输出完整明文文件或整包内存 fallback；运行期写入观察的实际范围另按证据说明。

首个 Player 曾出现一次未保留完整因果的回滚 `UpdateFailed`；改进测试载体诊断后，正式代码未变的三个全新进程/缓存运行均完整通过，仍不能据此声称已查明该次具体根因。原失败、复核次数及有限观察缺口保留在 Plan。Mono 的分配计数能力探针不可用，相关分配字段为 `null`，不宣称零分配或固定帧率；声明数据缓冲/CNG 成本也不替代完整进程内存或 Profiler 测量。

## 共存与范围

| 环境 | 当前结论 |
|---|---|
| Windows Editor / Win64 资源 | 有正式构建、Simulation/native、Core/Audio、事务和签名下载证据 |
| Windows64 Mono Player | E04正式本地资源链已在自有载体代表性运行验证，远端硬拒绝；Player完整首启、同一持久Store重启等仍属8.10。Editor内置部署、实际导出也不替代阶段9独立工程完整验收 |
| OSX / Linux 桌面 | 有普通目录/格式分支，无本批跨主机真实验收；远端拒绝 |
| IL2CPP / Android / iOS / WebGL | 未有最终闭包/运行；mount 受桌面范围限制，jar/WebGL 提取未实现，不能概括为只缺设备测试 |

`Integrations/XFAB/Audio` 仅是可选 borrowed 叶子：YLFramework.Audio.XFAB、非 autoReferenced、受包版本与 YL_AUDIO_XFAB 条件约束，不自动注册/默认导出，主体不依赖它。原游戏、XF/XFAB/XFFSM、MainModule 与历史资产保持原链。

Variant/透明晚绑定、archive、整体Workbench UI/UX和其他平台扩展均不由本说明自动纳入。E04、8.9整体、8.10 Player和阶段9独立交付是不同验收范围。详细验收/授权及实际产物位置见原阶段8 Plan，该研发文件不随默认框架包导出。
