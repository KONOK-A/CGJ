# XF × YL 阶段 8：Resource 深度融合与全面升级 Plan

> **总体方案 APPROVED；8.1～8.8 已完成技术验收及 Git 收口；8.9 仍在分批交付。C02、E05、B06、C05 与 E04 G1～G3 已通过用户技术验收；E04 G1～G4正式交付及上轮技术审核结论沿用，保留原验收层级和限制。本批B02与F03技术完成，待用户验收，见28.11。2026-10-17 用户调整 Git 策略为两次提交：第一次已交付功能基线，第二次后续 Workbench UI/UX；见28.12。整体8.9与阶段8均未COMPLETE，8.10尚未实施。**
>
> 当前分支 `fusion/8-resource`，基线 HEAD `ddf154cf2c72cd0b403cdd1547eee8ae06963547`。既有 8.9 与 C05 实现仍在工作区，第一次功能基线 Commit 前暂存区为空；C05 已通过用户技术验收，但技术验收不等于已提交或整体 8.9 已验收。
>
> **E04 G1～G4正式交付及上轮技术审核结论沿用；本批仅联合实施B02与F03，见28.11。** 正式采用 Windows CNG `Microsoft Primitive Provider` AES-256-GCM + 官方 BC2.7.0 netstandard2.0 HKDF，64KiB/2slot/32KiB native read buffer、默认128MiB显式数据预算；平台限 Windows x64 Editor/Mono Player。历史 G0/Scene 风险见28.10.15～17，B1原型证据见28.10.18，G1～G3正式证据见28.10.19，G4交付见28.10.20。**Git当前有效策略（2026-10-17）为两次独立提交：第一次提交已交付并具备证据的8.9功能基线，第二次提交后续Resource Workbench UI/UX全面优化。本批只执行第一次功能基线Commit，不Push、不Merge、不Tag、不切换分支；Push仍待用户明确授权。第一次Commit不表示8.9 COMPLETE。** 保留原成果及资产，不进入Workbench重设计、远端Player开放、8.10或阶段9。

本文保留稳定契约、阶段门禁、关键历史证据与当前待办；重复授权、已被后续状态覆盖的中间记录和一次性诊断过程已合并。保留原章节编号，尤其是 17.4、28.2.1～28.2.4 和 28.6，便于继续引用。

## 1. 当前基线、授权与保护边界

### 1.1 当前状态

| 项目 | 已确认状态 |
|---|---|
| 工程 | Unity `2022.3.62f3c1`；当前真实验证主环境为 Windows Editor / `StandaloneWindows64` |
| 实施分支与 HEAD | `fusion/8-resource` / `ddf154cf2c72cd0b403cdd1547eee8ae06963547` |
| 总体架构 | 自有唯一 ResourceSystem；Runtime/Editor 两主体，Foundation/Core 保持实际编译边界 |
| 8.1～8.8 | 已完成技术验收与 Git 收口；提交映射见第 10 节 |
| 8.9 | Workbench、图片/预加载/诊断、功能补齐已交付；C02/E05/B06/C05 与 E04 G1～G3 已通过用户技术验收；E04 整体验收待用户确认，G4 技术完成；B02/F03 技术完成待用户验收；第一次功能基线 Commit 按28.12执行，8.9 整体仍未 COMPLETE |
| 持久协议 | Catalog Schema v1 已冻结；签名授权与激活记录不塞入 Catalog 字段 |
| 当前 Git 权限 | 第一次8.9功能基线Commit与后续Workbench UI/UX独立Commit；禁止Push/Merge/Tag/切换分支，Push待明确授权 |

历史设计从 Tidy 集成 `fe1cd72`、方案批准 `0e71c8b` 开始。早期“当前为 integration 分支”“主体仍依赖 XFAB”“8.6b 待提交”等描述已被后续实施取代，不再作为现状。历史阶段 7/Tidy 细节分别见其专项 Plan，不在这里重复。

### 1.2 必须保护

- 原 XF/XFAB/XFFSM Package、游戏业务、MainModule、正式 Scene/Prefab、Canvas、旧设置和历史 Bundle 不因新资源系统自动迁移、保存、重建或删除。
- `Packages`、`ProjectSettings`、`AGENTS.md`、用户配置、ProBuilder Settings、并行 UI Plan 及其他工作区成果保持原样；Git 忽略不等于可删除。
- 不调用旧 `ProjectBuild.Build(MainModule)`，不清整个 AssetBundles、StreamingAssets、persistentDataPath、Library 或 Temp，不启动第二个同工程 Unity。
- 只写明确登记的新 YL 配置、夹具和产物。未知文件/目录、未知 `.meta`、symlink/junction 不自动接管；删除仅限本批真正拥有的内容。
- 新资源使用新 GUID；移动存续文件保留 `.meta`。类型/AQN、字段、地址、程序集和格式变化单独核对，不靠 `FormerlySerializedAs` 掩盖不适用的迁移。
- 用户最新决定（2026-10-17）为**两次独立提交**：第一次提交已交付并具备证据的8.9功能基线，第二次提交后续Resource Workbench UI/UX全面优化；本次只执行第一次，不Push、不Merge、不Tag、不切换分支，也不得无差别接管用户/并行改动。

### 1.3 证据使用规则

- 源码和实际配置优先。历史验收只支撑其对应代码、环境和范围；文档维护不冒充重新编译、运行或构建。
- 测试发现树总数、跨 Domain Reload 异常总耗时、父 suite、旧失败缓存和 NotRun 不计为通过。分组有重叠时不相加为唯一总数。
- 编译闭包、真实 Editor/native 行为、Player 构建、Player 运行、持久进程重启、独立工程交付分别成立，不能互相替代。
- 临时证据统一以 `%TEMP%/kilo/` 表示。文件可能随清理失效，复验入口是相应测试/正式操作，而不是依赖某台机器的临时路径。

## 2. 当前源码与交付导航

缩写：`R`=`Assets/YLFramework/Runtime/Systems/Resource/`；`E`=`Assets/YLFramework/Editor/Systems/Resource/`；`T`=`Assets/YLFramework/Tests/`；`X`=`Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`，仅作旧实现只读对照。

| 职责 | 正式位置 |
|---|---|
| 唯一会话与配置 | `R/ResourceSystem.cs`、`ResourceConfiguration.cs` |
| 显式应用接入、Core 与图片组件 | `R/Integration/ResourceStartup.cs`、`CoreResourceProvider.cs`、`ResourceImage.cs` |
| 身份、协议、快照 | `R/Catalog/ResourceAddress.cs`、`ResourceCatalogCodec.cs`、`ResourceCatalogDocument.cs`、`ResourceLocalSnapshot.cs` |
| 加载与持有 | `R/Loading/`、`Lifetime/`、`Sources/`、`Instances/`、`Scenes/` |
| 版本、安全与诊断 | `R/Updates/`、`Diagnostics/` |
| 定义、构建、发布、生成、工作台 | `E/Data/`、`Build/`、`Publishing/`、`CodeGeneration/`、`Service/` |
| Core 窄合同 | `Runtime/Core/ResourceAccess/`；Core 不导入 Systems/XFAB 类型 |
| Audio 领域后端 | `Runtime/Systems/Audio/ResourceAudioClipProvider.cs` |
| 可运行应用例 | `Assets/YLFramework/Samples/ResourceStartup/Runtime/` 与 `Editor/` |
| 使用说明 | [Resource 模块说明](../../Assets/YLFramework/Documentation/Modules/Resource.md) |

以上是职责组织，不要求每个目录建立抽象层。当前没有单独 Resource 产品 DLL；示例的 Runtime/Editor asmdef 是独立示例载体，不是第二套资源系统。

## 3. 融合目标与承接结论

采用 XFAB 的资源项目、显式构建、依赖分包、加载与更新领域体系，吸收旧 YL 的持有域、失败收尾、归属保护及可靠生成机制，形成自有实现，而非原 Package 的永久包装层。

| 来源能力 | 当前正式承接 |
|---|---|
| XFAB Project/Group/GUID 选择与自动依赖分包 | ResourceProject/Group/Selection、显式 BuildMap、依赖者集合聚合、Scene 分离与环归并 |
| Asset/SubAsset/批量/实例/Scene/图片 | 唯一 ResourceSystem、独立 Request/Lease/Scope 与 Bundle 图 |
| 检查、下载、恢复与回滚 | ArtifactStore、PreparedUpdate、唯一 journal、签名/Range/序号门槛 |
| YL 持有域及 Loading 零消费者收尾 | ResourceScope、producer/等待者分离、session 失效与迟到回收 |
| YL Editor 安全和生成 | owned workspace、不可变发布、路径/归属/后验、常量碰撞/幂等/编译保护 |
| 旧 Profile 的接入便利 | C05 显式配置工厂、ResourceStartup 与双模式样例，不读取旧 XFAB Settings |

固定 1 MB/10 项拆包、文件名 MD5、任意 BuildAssetBundleOptions、检查时删旧包、盲 Range 追加、DES、全局 Importer 接管不承接。详细能力状态见 28.2.2，不按旧 API 名、类型数或代码体积判断缺口。

## 4. 统一所有权与已知风险

| 对象 | 唯一责任 |
|---|---|
| Bundle/依赖 | BundleRecord 持 native 包；依赖图边保活；public lease、pending、实例、Scene、idle/cache 是根 pin |
| Asset/SubAsset | 每次成功取得独立 AssetLease；alias 归一到同 canonical 身份；Scope 只聚合，不建第二缓存 |
| 在途操作 | producer 观察 native 收尾，等待者独立取消；无 owner 的迟到结果由 producer 回收 |
| Prefab 实例 | Resource 保有 Prefab 图，复用 Foundation 桶；active/idle/destroy-pending 均覆盖至真实销毁 |
| Scene | SceneRecord 从 native 成功到真实 sceneUnloaded 持图；SceneLease 归还不等于卸载 |
| 动态图片 | 仅销毁系统创建的 Texture/Sprite；borrowed AssetDatabase/Resources/placeholder 不随租约归零销毁 |
| 文件/版本 | 私有 staging/parts，ArtifactStore 负责版本事务与安全清理；Publisher 负责 Editor 发布 |
| 应用接入 | ResourceStartup 只结算本入口责任；ownership 绑定具体 ResourceSystem 实例，不跨实例继承 |

旧 XF/YL 缺陷的回归目标保留：依赖首项即时失败不挂起；typed 批量真正等待；完整 identity 避免异类型/URL 混淆；先保活再通知；observer 异常不阻止其他请求或清理；单消费者取消不误杀共享生产；实例/Scene pin 不被 Loader 释放替代。无需为这些触发行为逐个保留旧实现。

## 5. 必需、按需与专项边界

| 类别 | 当前处置 |
|---|---|
| 必需且已交付 | 唯一身份/图/租约，可靠构建发布，Core/Audio 正式接入，版本事务，Editor 安全链，图片/预加载/诊断及 C05 |
| 已通过用户技术验收 | E05 Runtime store 只读枚举/预览；原证据见 28.7，不另建清理权威 |
| 已通过用户技术验收 | C02 StreamingAssets 安全部署；仅连带 C03 部署/定位及必要 C05 启动衔接，门禁见 28.8 |
| 本批明确授权 | B02 项目级/自动化构建配置与 F03 代表性性能基线联合实施，先B02必要门禁后F03，不等待中间确认或提交；见28.11 |
| 按实际需求 | E03 历史签名便利入口与用法；不为表面完整度造平台 |
| 已验收 / 待整体验收 | B06自动Atlas及E04 G1～G3已通过用户技术验收；G4技术完成、E04具备整体验收条件，历史G0/C1/B1与限制继续保留 |
| 不默认纳入 | archive、优先级/抢占调度、复杂跨项目去重、透明 late binding、native 多版本热共存、二进制差分、云发布后台 |

FIFO 是现有事实，不是优先级调度。可选能力须有真实需求或同条件成本证据，不建立空接口和占位 Manager。

## 6. 架构裁定

- 采用已批准的 B：XFAB 领域主体 + 自有快照/租约内核，吸收 YL 成熟安全机制；不保留巨大旧 Manager 再不断补 adapter，也不以旧 ResLoader 外挂第二更新器。
- Resource 与 Core/Audio 维持清晰依赖：Core 定义窄消费合同，Resource 实现；Audio 保留领域 Provider；Foundation 不懂 Catalog、Bundle 或资源更新。
- 主体继续为 `YLFramework.Runtime` / `YLFramework.Editor`。Foundation/Core/整个主体分别有真实编译闭包，不能仅按目录声称隔离。未来 DLL/AQN 边界变化需另行设计。

## 7. 稳定公共合同

### 7.1 身份与目录

- ProjectId 是稳定资源身份，定义 SO GUID 用于 Editor 归属，DisplayName 仅显示；不等于 Module AQN、磁盘路径或旧 XFAB 类型。
- ResourceAddress 不可变，project/address/subasset 按明确 ordinal 语义解析；构建拒绝大小写折叠冲突。需要跨移动稳定的地址应显式配置，旧发布不随 Editor 重命名静默漂移。
- GUID/localId 标识资产；Runtime 使用明确编译类型，不由 AQN/远端类型字符串反射创建对象。native 子资产按唯一 name+type 定位，歧义在构建时拒绝。
- Catalog 是唯一运行目录，Group 成员指向 canonical AssetId；常量生成器只输出键，不维护第二运行表。Schema v1 细则见 17.4。

### 7.2 Request、Lease 与共享生产

- 显式 InitializeAsync 建唯一 authority；同一 ResourceConfiguration 对象在途/Ready 可 join，不同配置冲突。Current 读取不初始化；在途/退出/旧 native 排空的冲突不能通过换 epoch 绕过。
- owning 结果由请求先持有，`TakeResult()` 恰一次交割；`yield return` 只等待。未领取 Dispose 释放结果，已领取后 Dispose 请求不再释放 lease；二次领取、Dispose 后领取明确报错。公开 request/lease 不池化。
- canonical 身份、snapshot、加载类型/投影及 subasset 决定 asset producer；snapshot+BundleId 决定 Bundle producer。consumer 独立 lease，共享真实生产；sync 遇在途返回 InProgress，不忙等或第二次 IO。
- 先校验对象、建立保活/终态/清理排队，再通知；逐个隔离 observer 异常。失败保留 source/dependency cause，不用 null 成功吞错。
- 取消只退出本等待者。可取消的私有 IO 可终止，不可取消的 native 操作继续观察；零 owner 的迟到结果及时回收。Scope 只停止自己的请求、归还自己的 pin。
- Resources/AssetDatabase 为 borrowed，归零只 drop 本系统记录；自建 Texture/Sprite 才 Destroy。禁止单 lease 隐式触发全局 UnusedAssets 或 UnloadAllAssetBundles。
- Shutdown 遇 live lease/实例/Scene 返回 Busy 并恢复可用；真正会话退出失效请求、解绑事件，旧 native cleanup owner 保留到完成，不在退出回调中阻塞或新建宿主。

### 7.3 实例与 Scene

- Instantiate 前先取得 creation pin，覆盖 Awake/OnEnable 重入；每个 checkout 独立身份，旧 handle 不能还新 checkout。Return 回缓存、Discard 销毁；直接 Prefab/Attach 不自动纳入命名资产所有权。
- 复用 Foundation `GameObjectPoolData`，不再造池算法。桶退休后，借出及 destroy-pending 未真实 fake-null 前不归还 Data/Prefab 图；从未激活实例不能只靠 OnDestroy 观察。
- 默认 scene-scoped；scene unload 清 idle，不毁已迁移/DDOL 活实例；旧桶晚归还销毁，不流入新场景桶。persistent 预热须明确选择。
- SceneRecord 使用 handle+加载序号+snapshot。SceneLease.Dispose 只归还 caller 责任；显式 UnloadSceneAsync、真实 sceneUnloaded 后才释放图。ModuleClose 不自动卸载 Scene。
- 同 path Additive 是不同意图，串行关联事件和 handle 差集；外部同 path 并发无法归属时明确 SceneIdentityAmbiguous，保守保活，不取第一项冒充结果。
- native 前取消可无副作用；Additive 迟到完成可合法激活再卸载；Single 替换开始后不能恢复旧 Scene。0.9 激活门控默认 30s，允许范围 `(0,300]`，不可无限堵住 Unity async queue；最后 Scene 卸载可 Busy。

### 7.4 Core、Audio 与应用装配

- Core 的 ICoreResourceProvider 仅提供复制文本、Prefab/实例责任、真实 Scene 结果；合同不泄漏 ResourceSystem/Address 类型。Resource 的一个 CoreResourceProvider 实现它，初始化本身不绑定。
- Module.ProjectName 保留名字，正式 YL 语义为 Resource ProjectId；原 XF Module 与原游戏语义不变。named 操作缺 Provider 明确失败，不回退 XFAB。
- Core 原 fallback、owner/parent/session 校验、Close/Attach/缓存行为保留；同步 override 在途必须提示 async，不将 InProgress 当缺失而 fallback；异步 join 同 producer。
- Audio 通过 `GetAudioClipProvider(projectId)` 取得领域 Provider，每次交付独立 AudioClipLease。Library 使用 Resource 来源与 ProjectId/Key；直 Clip 不卸载。旧枚举/BundleName 仅供序列化读取与显式副本迁移。
- C05 的 ResourceStartup 管理显式应用接入，不重写内核；Core/Audio 可选。borrowed 会话不关闭，绑定按引用归还，不清后来替换者；不擅自关闭全局 AudioKit。操作与示例见 28.6。
- Core Editor 预览通过窄解析接点读取正式索引，借读 AssetDatabase/AssetPreview，不启动 Runtime。Table/Json/HTTP/ZipTool 仍是容器或工具，不成为第二资源 owner。

## 8. 构建、版本事务与安全

### 8.1 构建与交付

- 从自有定义显式生成 BuildMap，不自动认领原 Importer 标记。Together/PerAsset 是意图，最终物理包服从共享依赖、Scene 分离和合法环归并；最终 Bundle 图必须是完整 DAG。
- 预检 -> 冻结完整输入/设置 -> owned 锁与持久 workspace -> native 构建/readback -> SHA/Catalog -> 私有 stage -> 完整验证 -> 不可变发布，最后切 index。构建、发布、常量生成分别报告。
- 压缩仅 LZ4/LZMA/Uncompressed，恒 StrictMode，进入 Settings/Workspace 身份；不暴露任意 flags。目标必须为已安装、已激活且可本机 native 读回的桌面目标，不隐式切换。
- Workspace 按 Project GUID/target/设置分区。无法可靠冻结的 build callback 阻止整体 skip；本机六项回调导致 no-change 也安全重建，这是正确性保护，不是缓存命中或优化成果。
- 普通 Bundle 使用自有 `ylr_` native 命名空间。Unity 2022.3 自动根 manifest 的 native name 实测为空，不用文件名伪造 native 身份；以项目化输出、摘要和真实 readback/并行加载证据验隔离。
- Publisher 根为 `AssetBundles/YLFramework/<ProjectId>/<Platform>/`，版本叶为 CatalogDigest。物理 Bundle 用短名，保留长路径拒绝；外部完整目录导出不等于上传或激活。

### 8.2 ArtifactStore 与恢复

- RootPath 由应用明确提供，不自动认领整个 persistentDataPath。项目子树为 `<root>/<ProjectId>/<Platform>/`，包含 versions、staging、parts、active.json、last-known-good.json。
- **稳定排序首项目的平台目录承担整个配置闭包的 coordinator**：commit-journal.json 与 prepared 记录在这里；不是每项目各自提交再拼接。owner 登记完整项目配置，不匹配即拒绝。
- Check 纯读，不建 root/lock/staging、不删除旧版或恢复写；待恢复 journal 返回 Busy。Prepare 幂等且可恢复，Complete 验证后才可成为 prepared；跨会话可重建。
- Activate 已是 target 则幂等；from 已变化且 target 不同则 StaleBase。任何项目 Busy/失败均不部分切换，闸门恢复后旧版仍可取得。
- 激活/回滚前排空对应项目责任、flush idle、卸载旧 native 图；不强还他人 lease 或卸载外部 Scene。应用在 neutral/bootstrap 边界结算业务，必要时保留 prepared 到下次启动。
- 启动只能选择整组已验证 active、LKG 或明确内置快照，不跨项目拼凑、不猜目录最新。journal/同目录替换提供恢复，不承诺跨目录断电绝对原子。
- owned 清理保护 active/LKG/prepared from-to/在途及持有快照，未知内容拒绝。E05 在原规则上补只读快照，预览不是删除权限，实际清理仍重新检查。

### 8.3 远端安全

- Release 授权使用固定 RSA-2048 或以上、PKCS#1 v1.5、SHA-256，签名覆盖确切 canonical bytes；公钥/keyId/endpoint/channel 来自应用可信配置，私钥不进资产或代码。
- 签名 envelope 独立于 Catalog v1，绑定 project/platform/channel/App/Unity、CatalogDigest、发布序号及有效期；当前有效期上限 31 天，时钟容差 5 分钟。
- HTTPS 默认正常证书校验、禁重定向；测试只允许明确 loopback pin，不进入生产。路径、大小、条目、磁盘预算和响应编码均有边界。
- 强 ETag 才续传并发 If-Range；206 严格验证 Content-Range，200 忽略 Range 时重置，416 复验/重置而非直接成功。无 HEAD 不妨碍 GET，重试与 Retry-After 有界，取消不删除 active。
- 每文件 length/SHA 与实际完整性校验不能由 sidecar/mtime 替代。最高成功激活序号不由只读 Check 落盘；低序号、同序不同 digest 拒绝，相同发布幂等，LKG 回滚不降低门槛。
- 签名不证明最新。首次安装、本地状态丢失/恶意回滚、时钟篡改或服务器冻结旧合法发布没有强保护承诺；远端回滚须更高序号重新签发。
- **正式远端后端当前只接受 WindowsEditor**；Windows Player 同样 Unsupported，不是仅缺测试。开放须按 8.10 验证后显式决定，不能直接删 gate。

### 8.4 缓存与诊断

- 图片 TTL/容量/并发有界；RGBA 预算是估计，不是总 native memory 硬上限，活跃 owner 不因预算淘汰。Catalog Texture 原零 lease 释放合同不变。
- 诊断只复制事实，不保活、不建立第二状态权威；可解释根 pin、依赖入边、pending、实例/Scene/cache、下载与恢复。未知业务 owner 明示 Unknown，不猜调用栈。
- 历史默认关闭，采样显式启用；源字节、实际制品字节、native memory 与测量时间分别报告。

## 9. 保留、退役与兼容边界

| 对象 | 当前决定 |
|---|---|
| 旧 ResManager/ServiceManager/ResLoader/IRes/ResKey/ResTable/ResFactory | 已由唯一内核和 Request/Lease/Scope 替代，不留旧名永久转发 |
| 旧 ResData/BinaryFormatter、SubProject、模拟 HotUpdate/FakeResServer、旧路径/标记辅助栈 | 已退役；不读取历史 unsafe 格式或旧 prefs 猜测来源 |
| YL 安全构建/生成机制 | 保护与消费者语义保留，正式输入改为 Project/BuildMap/Catalog，不保留双 Builder |
| Foundation 池、通用 Table/ZipTool、Audio 领域合同和直 Clip | 保留，不能借资源迁移清理无关通用能力 |
| 原 XF/XFAB/XFFSM 与原游戏 | 不迁移、不修原包、不删除其历史产物 |
| `Integrations/XFAB/Audio` | 可选外部 borrowed 叶子，非 autoReferenced、非默认导出，不成为内部 fallback |

新产品不兼容 XF 旧映射、YL BinaryFormatter 或缺冻结字段的 8.2a/b 原型；如有真实历史数据需求，专项离线转换，不建立常驻第二 authority。源码复用保留完整适用许可，不复制来源/许可不明的 SharpZipLib.dll。

## 10. 实施路线与当前状态

以下提交由本次只读 `git log` 核对，是实际实现提交；中间 docs 状态提交不再混作功能提交。

| 批次 | 当前状态 | 实现提交 / 后续门禁 |
|---|---|---|
| 8.1 身份、请求、Resources | 已验收并收口 | `1666213` |
| 8.2a 分包与构建 | 已验收并收口 | `8910345` |
| 8.2b Catalog 与安全发布 | 已验收并收口 | `1b547b2` |
| 8.2c 工作台、模拟、生成与 v1 冻结 | 已验收并收口 | `66400c8` |
| 8.3 Bundle/子资产/动态 source | 已验收并收口 | `732c828` |
| 8.4 实例保活与缓存 | 已验收并收口 | `d26c166`；`c515b4f` 为后续状态文档提交 |
| 8.5 Scene | 已验收并收口 | `43e43d0` |
| 8.6a Core | 已验收并收口 | `4075bd3` |
| 8.6b Audio | 已验收并收口 | `b111608` |
| 8.6c 旧栈退役/主体解耦 | 已验收并收口 | `be3dcb8` |
| 8.7 本地版本事务 | 已验收并收口 | `05add9c` |
| 8.8 远端安全链 | 已验收并收口，限已验后端 | `ddf154c` |
| 8.9 功能/体验完善 | 多批已交付，C05 已通过用户技术验收，整体未 COMPLETE | 第 27～28 节；尚未 Git 收口 |
| 8.10 总体验收 | 未实施、未授权自动开始 | Windows Player/持久重启、阶段整体流程与交接 |

每个新批次先确认范围和工作区，建立最小失败/基线验证，再实现、定向回归、独立 Review、核对保护与文档。已完成门禁不机械重跑；总体批准不自动授权下一批或 Git 操作。

## 11. 验证与测量纪律

- 静态/辅助编译验证层级、条件分支与引用；真实 Unity 编译和运行另记。dirty scene 不自动保存以启动测试，环境阻断先解决，不弱化断言。
- EditMode 覆盖协议/身份/图/归属/构建发布/生成；真实 Play/native 覆盖 lease、实例、Scene、Core/Audio、迟到与物理卸载；网络/磁盘只用自有测试源、缓存与故障点。
- 默认执行本批新增及直接受影响路径，只有改通用 Foundation/会话能力才追加对应回归。纯文档/Git 收口不使技术证据失效时，不重跑阶段 8 或 1～7/Tidy 全量。
- BuildPipeline、输入指纹/后验、产物 hash、readback、复制分别计时；段落可嵌套，不相加为互斥总耗时。缓存命中和性能改进须有同条件事实。
- 共享加载记录实际生产/GET、等待帧与取消；图片/诊断区分编码字节、RGBA 估计、Profiler 样本和峰值。微小压缩输入、Mono 分配计数器恒 0 不支持普遍性能结论。
- 公平旧实现对照缺失时只报告结构性与新系统实测，不重建原 MainModule 做 benchmark，不宣称加速百分比。

## 12. 已批准决策与阶段 9 前置条件

### 12.1 D1～D8

| 决策 | 已批准且继续有效 |
|---|---|
| D1 主体 | XFAB 领域体系 + 自有租约内核，价值机制融合，不以保存旧类为验收 |
| D2 格式/地址 | 新 ProjectId/Address/SubAsset、Catalog v1；不兼容旧 unsafe 格式，不静默改协议 |
| D3 Core | 消费方窄合同、单 Resource 实现；ProjectName 为正式 ProjectId，无 XFAB fallback |
| D4 实例/Scene | Resource 保活 + Foundation 桶；scene-scoped 默认；Scene 不随 ModuleClose 卸载 |
| D5 Resources | borrowed 默认 drop，不按自身引用归零 UnloadAsset |
| D6 更新 | 本地事务与生产真实性分别验收；签名/序号有明确限制，不承诺 native 热共存 |
| D7 编译 | 两产品主体及真实 Foundation/Core 边界，可选 XFAB 叶子隔离 |
| D8 扩展 | 有需求和证据再纳入可选项；平台、权限、重大身份/协议变化专项决定 |

### 12.2 阶段 9 前

- 通过 8.10 的阶段完整流程及必要 Windows64 Player/持久缓存重启，确认源码边界、真实资源释放和平台矩阵。
- 明确 Json.NET 等实际依赖与许可，不能把当前工程已安装包当作独立工程必然具备；遵守 UPM 包壳及其 Third Party Notices 的不同许可。
- 融合的 XFAB MIT 来源归属 `Copyright (c) 2021 弦小风` 与完整许可随附，来源 revision 集中维护，不批量复制不明 DLL。
- 新 GUID、类型/AQN、生成程序集及 Bundle 脚本身份不指向原 XF 不可运行资产；构建成功不等于独立交付可用。
- 阶段 9 才创建独立载体、安装依赖、导入与运行，不把现有编译闭包或默认导出冒充该验收。

## 13. 关键外部语义依据

采用项目已使用的 Unity 2022.3 API，不照搬其他版本重载；这些是历史依据，本轮文档维护未重新联网研究。

- [AssetBundle.Unload](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.Unload.html)：Unload(false) 不能替代正确保活。
- [AssetBundle 依赖](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundles-Dependencies.html)：加载依赖是调用方责任。
- [SceneManager.LoadSceneAsync](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) 与 [allowSceneActivation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html)：无 scene Abort，0.9 可阻塞 async queue。
- [BuildPipeline.BuildAssetBundles](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/BuildPipeline.BuildAssetBundles.html)：显式 map、平台与根 manifest 行为仍需真实 readback。
- [DownloadHandlerFile](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.DownloadHandlerFile-ctor.html)：append 不是 Range 验证。
- [Resources.UnloadAsset](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Resources.UnloadAsset.html) 与 [BinaryFormatter 安全说明](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide)。

## 14. 8.1 历史验收摘要

显式唯一初始化、最小内存 Catalog、Request/AssetLease/ResourceScope、Resources borrowed、失败/取消/重入/退出合同完成。历史 **57 项零失败**；持久 Schema 当批未冻结，不能把 8.1 模型说明当成最终格式。已提交 `1666213`，不再处于待提交状态。

## 15. 8.2a 历史验收摘要

自有定义/BuildMap、共享依赖分包、Scene 分离、owned workspace 和真实 manifest readback完成，历史 **23/23**。确认自动根 manifest native name 为空，用户批准按实际身份机制验收；不同项目真实隔离、同产物改名不能热共存。未冻结回调阻止 skip；禁止构建卸掉外部已加载 Bundle。已提交 `8910345`。

## 16. 8.2b 历史验收摘要

不可变发布、owner/Complete/index、故障恢复和两代真实制品验证 **16/16**。长物理名触发 Windows Mono 路径限制后改短名并保留边界检查；恢复只接受预期 From/To，不接管未知发布根。已提交 `1b547b2`；原包、MainModule 和旧产物保护保持。

## 17. 8.2c 历史验收与格式冻结

工作台、正式模拟索引、常量生成与实际消费编译闭环完成，历史 **50 项定向全过**，已提交 `66400c8`。菜单为 `YLFramework/Resource/Workbench`；生成值来自正式目录，消费者程序集 `YLFramework.Generated.ResourceNames`，生成失败与成功发布分开报告。

### 17.4 Catalog Schema v1 冻结合同

以 `R/Catalog/ResourceCatalogCodec.cs` / `ResourceCatalogDocument.cs` 为权威；本轮只校正文档计数和原型描述，不改变协议。

- 持久顶层为 **12 个字段**：SchemaVersion、ProjectId、Platform、UnityCompatibility、AppContentCompatibilityId、BuildSettingsFingerprint、ManifestFile、Bundles、Assets、Groups、SupportFiles、BuildId。**CatalogDigest 是最终 bytes 的计算结果，不作为自身序列化字段**；Document 的公开属性数不等于持久字段数。
- Asset 固定字段含 AssetId/Guid/LocalId/Address/Aliases/SubAssetKey/BundleId/LoadName/AssetType/AssetAssembly/Kind/SubAssetName。Bundle/SupportFile/Group 字段按 Codec 固定集合；v1 不含签名、激活、加密或额外项目依赖字段。
- canonical AssetId 为小写 32hex GUID + `_` + 非零 Int64 localId 十进制；SubAssetKey 是 localId 十进制，主资产为 null。alias 归同一 canonical，子资产继承 alias 路径及自身 key，不生成第二映射。
- Kind 为 Asset/Scene/SubAsset；普通 LoadName 为小写 `assets/...`，Scene 为精确 `Assets/...unity`。类型用 FullName+程序集简单名，不含 AQN/版本；Scene 使用特殊描述，不按字符串反射创建。
- 严格无 BOM UTF-8、无空白/尾换行、EscapeNonAscii、InvariantCulture、ordinal 排序；拒重复/未知/缺失字段、非 canonical bytes、伪字符串数值/布尔及歧义子资产。
- BuildId=去除 BuildId 后规范 payload 的 SHA-256；CatalogDigest=包含 BuildId 的最终 bytes 的 SHA-256。两者不自引用，也不等于认证。
- 集合上限 16384、依赖边 65536、JSON 16MiB、深度 64；canonical+alias 共用总地址预算。读取精确匹配 Schema/ProjectId/平台/完整 Unity 版本/App 兼容值。
- Release Index 维持既有 v1 单摘要快照。字段、身份、编码变更需新版本和明确兼容门禁；8.2a/b 缺字段原型及旧 XF/YL 产物应从正式定义重建，不建永久适配层。

## 18. 8.3 历史验收摘要

资产/Bundle 图、子资产、typed 批量、local/URL Texture、共享/迟到完成，历史 PlayMode **36/36** 加真实 native 综合叶通过。Bundle FIFO 4 槽；冷 URL 同步 Unsupported、在途 InProgress、Ready 可同步取得。BundleLease 的借读不可自行 Unload。已提交 `732c828`。

Canvas 曾有外部变更，后来经用户单独授权恢复并回归；以该最终恢复记录为准，不将研究初期 dirty 值继续当现状。本轮文档维护不再次修改或保存 Canvas。

## 19. 8.4 实例合同与证据

复用 Foundation 桶并保有 Prefab 图；默认 idle 容量 32，0/-1 等策略按显式配置，清理每轮有界。native 综合叶、Pool **13/13** 及受影响 **36/36** 通过；active/idle/never-active/destroy-pending、退休晚归还和旧 checkout 均有真实覆盖。未把 Data 回池当作 native 已销毁证明。

## 20. 8.4 Git 收口记录

实现提交 `d26c166`；后续 `c515b4f` 是验收状态文档提交，不是另一个实例实现。历史验收/收口已完成，不再保留“技术完成但待 Git”的中间结论；本轮不改写历史或补 docs-only 提交。

## 21. 8.5 Scene 合同与证据

真实 Scene/handle、激活门控、迟到/外部卸载、最后 Scene Busy 等 native 综合叶及专项通过，已提交 `43e43d0`。保留 SceneIdentityAmbiguous 的保守保活和 Single 不可逆约束。

Unity 2022.3 对特定“不同 path、相同短名”的 Scene Bundle 布局实测报 `another AssetBundle with the same files is already loaded`；产品不据字符串改名承诺该组合可用，也不宣称多版本 native 热共存。此限制与可以区分已加载 Scene 的真实 handle 不矛盾。

## 22. 8.6a Core 合同与证据

Core 文本/Prefab/实例/Scene、显式 Provider、fallback 与同步 InProgress 语义、Editor 预览及生成指引接入完成，已提交 `4075bd3`。历史 Core **189/189、72/72** 与 Editor **104/104** 为不同范围，不累加；原游戏只读检查不等于本阶段 Player 验收。Core 不导入 Systems/XFAB。

## 23. 8.6b Audio 合同与证据

正式 ResourceAudioClipProvider、Library Resource 来源和副本迁移完成，历史 **134 个不同叶 Passed**，已提交 `b111608`。旧来源枚举保留序列化读取，BundleName 不再作正式命名加载；缺映射/覆盖拒绝。真实 Bundle 依赖、crossfade/取消/尾轨/最终释放及直 Clip borrowed 合同保留。

## 24. 8.6c 退役与主体独立性

旧资源/模拟更新栈及旧 Provider 退出，已提交 `be3dcb8`；主体 Runtime/Editor 已解除 XFAB 依赖，不是当前仍待迁移。历史完整源码/二进制独立性 **6/6** 及必要消费者门禁通过，原游戏与可选外部 XFAB 适配继续共存。

主体预编译输入白名单为明确 Json.NET，Core/Foundation 另有负例闭包；两主体策略不等于默认带原 XF/XFAB。当前源码数会随 C05 等新增变化，不保留当时 391/58 等计数作为未来必须固定的接口。

## 25. 8.7 版本事务与证据

已提交 `05add9c`。纯 Check、幂等 Prepare、Complete 恢复、StaleBase、Busy 后旧版可用、多项目整组 commit/recovery/rollback 和 owned 清理完成；历史专项 **23/23**、合并组 **29/29**，该批 **74 个不同用例**为另一统计口径，不混加。

默认解析整组 active/LKG/明确内置，不拼接混代；coordinator 布局见 8.2。Catalog v1 的 native 命名空间已在外部加载时也可 Busy。故障注入/重新初始化不等于杀进程或真实断电验收，后者仍不宣称完成。

## 26. 8.8 安全链与证据

已提交 `ddf154c`。签名授权、强 ETag/Range、流式 parts、有限重试、发布序号门槛及与原版本事务协作完成，实际支持边界为 Windows Editor。

- 历史 **62 个不同用例** 与外部 MCP 环境恢复后的 **16/16** 分属前后环境，不能合计为 78 个独立通过；后者覆盖直接受影响的签名/TLS/断点等门禁。
- 本机 Mono 曾出现 `RSA.Create()+KeySize=2048` 实导出 1024-bit；测试签发改用明确 `RSACryptoServiceProvider(2048)` 并断言 Modulus。私钥/PFX 只在外部测试临时环境，不进产品或系统信任。
- HTTPS/Range 六类终态、恢复/回滚与序号门槛已有真实覆盖；不将 digest 正确、签名有效或状态可恢复扩大成“最新发布已证明”或 Player 已支持。

## 27. 8.9 既有 Workbench、图片与诊断成果

### 27.1 已交付工作流

- 正式菜单保持 `YLFramework/Resource/Workbench`，Project/Group/条目编辑、Undo、分包预览、预检、构建/发布/生成、owned 历史与明确目录导出复用原服务。
- 编辑沿用 SerializedObject/归属/路径保护；窗口关闭、切换、Play/编译/导入前的自有 queue 取消有效。native 构建不可抢占；后台 queue 已从饥饿的 idle delayCall 改为有身份保护的一次性 Editor update。
- 布局、路径可读性和真实 GUI 已有本批验证；不据此自动进入后续整体 UI/UX 重设计。诊断和窗口不成为资源/版本权威。

### 27.2 图片、预加载与只读诊断

- ResourceImage 支持 Image/RawImage、直接 HTTP(S)/绝对本地 PNG/JPEG、Catalog Texture/Sprite 及后续显式 Atlas。完整 URL 保留大小写/query 身份，显示脱敏；刷新不承诺绕过同 URL 缓存。
- 默认编码上限 32MiB、16M 像素；直接图片 idle 默认 128 项、32MiB RGBA 估计、30s TTL。仅 idle 可淘汰；setter/回调重入、快速切换、Disable/Destroy/外部销毁均按代次和自身 ownership 收尾。
- PreloadGroupAsync 返回拥有型 ResourceGroupLease：普通成员取得资产，SceneBundle 只准备图，ScenePreview 只验证 Editor 路径；都不等于 Scene 已加载。required 失败整批回滚，optional cause 保留，Scope 可统一归还。
- GetDiagnosticsReport 按需复制资产/Bundle/owner/依赖/实例/Scene/队列/更新与恢复事实，历史默认关闭，不猜业务栈，不因观察增加 pin。

### 27.3 保留的测量结论

| 样本 | 可支持的结论 |
|---|---|
| clean/no-change/单 Text 变更 | 内容 SHA 稳定或对应变化；六项回调均阻止整体 skip，实际仍 native 重建 |
| 正式窗口构建 | 样本总操作 44.22s、Builder 30.74s，输入指纹/后验约 25.76s，是主要成本；未证明可安全跳验 |
| 100 同 URL 消费者、10 取消 | 1 GET，独立取消不伤其他消费者；测试帧延迟不外推实机 FPS/网络延迟 |
| 图片/native memory、300 owner 诊断 | 编码/RGBA/Profiler 样本分别计量；诊断拼接由明确 O(N²) 改线性，不凭恒 0 分配计数器声称零分配 |

### 27.4 历史证据与边界

该批 Resource PlayMode **64/64**、GUI **7/7**、三次构建/历史/导出叶、native 综合叶及必要 Core/Audio/更新协作通过；完整主体/Windows Development 源码闭包另 **6/6**。后续新增受影响路径的更近结果见 28.4，不累加成测试总数。

关键证据：`%TEMP%/kilo/yl89-final-gui-build.json`、`yl89-final-cross-module-results.json`。GUI 截图和临时窗口尺寸、证书路径、PID、夹具 GUID 等一次性过程不再作为长期 Plan 正文；相关保护与测试契约仍保留。

## 28. 8.9 功能核查、补齐与当前待办

### 28.1 当前进度

既有功能补齐与 C05 均保留在工作区，整体 8.9 未 COMPLETE。31 项审计已完成，不因维护文档重新审计或补功能。C05 的后续进展已回写下表，旧“Samples 为空、C05 仍缺正式入口、下一项先实施 C05”不再作为现状。

### 28.2 31 项目标与处置

#### 28.2.1 范围与证据

- 需求来源为用户提供的《YL-Resource-目标功能清单与核查提示词》第 2～7、9、10 节，原入口 `E:/Downloads/YL-Resource-目标功能清单与核查提示词.md`。保留 A01～F05 编号，不把条件可选项强制记缺陷。
- 初审以工作区实际源码/调用链为准，未重跑全量。以下仅叠加 C05/E05/C02 的已知进展，不宣称重新验证其余能力；“完整”均受平台/来源边界限制。
- 完成度分开表达：底层功能、正式入口、可执行用法、必要验证、专项授权。无 GUI/CLI、不沿旧 API 名、保守重建，都不自动代表功能缺失。

#### 28.2.2 A01～F05 当前能力表

| 编号 | 正式入口/证据 | 当前结论 | 真正限制与处置 |
|---|---|---|---|
| **A01** 项目与范围 | ResourceProject、BuildMap.ValidateProjects、CreateProject；15～17/28.4 | 完整 | 身份/根重叠拒绝；ID 变化不自动迁移旧发布，复用 |
| **A02** Group/选择 | ResourceGroup/Selection、BuildMap.Create、EditorOperations；28.4 | 完整 | 文件夹/过滤/选择与物理包分离；无实际需求不增通用排除规则 |
| **A03** 地址/alias/子资产 | ResourceAddress/Catalog、DescribeSubAssets、Simulation；17～18 | 完整 | 显式地址可跨移动稳定，旧快照不漂移；歧义拒绝，不造第二身份表 |
| **B01** 分包/共享依赖 | BuildMap.CollectDependencies/BundleEdges/MergeCycles、ReadBack | 完整 | Scene/私有/共享及环归并已解释；不恢复旧固定阈值 |
| **B02** 构建配置 | ResourceBuildRecipe/Invocation、Inspector、Workbench接点及ExecuteRecipe；28.11 | 技术完成，待用户验收 | Project定义/压缩/加密唯一权威；同解析同正式服务、原调用/None保留，无batchmode/CI或部署/签发扩展 |
| **B03** 压缩 | ResourceCompression、Builder.Options/Settings；28.4 | 完整 | 三值白名单恒 StrictMode，其他组合拒绝；复用 |
| **B04** 增量/重建 | Workspace、完整指纹/后验、force 与 blocked 原因 | 完整且保守 | 六回调阻止 skip 是正确性保护；无可靠闭包不优化命中率 |
| **B05** 显式 Atlas | ResourceAtlasRules、PrepareAtlases、SetAtlasSprite；28.4 | 完整 | 非 Variant、Include in Build 关闭、名字唯一、修改后显式准备；非透明 late binding |
| **B06** 自动图集 | ResourceAtlasConfiguration / Generator / Plan、Project Inspector、正式 Builder；28.9 | 已通过用户技术验收 | 稳定规则/文件夹身份，输出正式入 Group；保留 B05 的显式加载边界，不是透明晚绑定 |
| **B07** 预检/自动化 | Preflight/BuildOnly/BuildAndPublish/PublishBuild 与公开 Builder/Publisher | 等价满足 | 同服务可无窗口调用；有 CI 交付需求才补薄调用方/取证，不另造构建器 |
| **C01** 发布/导出 | Publisher.ReadVersion/ReadHistory/Recover、ExportRelease、Snapshot.Open | 完整 | 导出到工程外新目录并复验；不等于上传、内置部署或 Runtime 激活 |
| **C02** 内置部署 | ResourceBuiltInDeployer.Preflight/Deploy/Recover、样例明确入口；28.8 | 已通过用户技术验收 | 仅受限自有根，外部stage/journal，真实导入与旧版保护已验；无自动GC或绝对断电原子承诺 |
| **C03** 内置/首次启动 | OpenBuiltIn、ProjectMount、ArtifactStore.Select、C05；28.8 | 本批部署/定位衔接完成，整体仍部分 | 真实StreamingAssets导入后strict读取及直接/空Store启动已验；Player打包/首启/持久重启留8.10 |
| **C04** 模式选择 | Runtime Configuration.Create、Editor Simulation 模式工厂；28.6 | 本批技术完成 | 明确 Simulation/Bundles，不自动 fallback；Player 拒绝模拟，不追加 Auto 模式 |
| **C05** 应用启动 | ResourceStartup、正式配置/双模式样例；28.6 | 已通过用户技术验收 | owned/borrowed/Provider/失败/Busy 已闭环；不新增全局 Manager |
| **D01** 取得/释放 | Acquire/AcquireAsync/AcquireAll、Request/Lease/Scope；18/28.4 | 完整 | 冷 URL Unsupported、在途 InProgress、Ready 可同步；borrowed 与自建分开 |
| **D02** Group 预加载 | PreloadGroupAsync、ResourceGroupLease、Scope；27～28 | 等价完整 | SceneBundle/ScenePreview 不加载 Scene；Lease 期限内保活，不建 PreloadManager |
| **D03** Prefab 缓存 | Instances、InstanceCache/InstanceLease；19/27～28 | 完整 | active/idle/destroy-pending 真实销毁前保活；外部 Destroy 只能观察收尾 |
| **D04** Scene | Scenes、SceneRequest/Record/Lease；21/27～28 | 完整 | Single 不可逆、最后 Scene Busy、歧义保活及同名布局引擎限制保留 |
| **D05** 图片组件 | ResourceImage、Images、Inspector；27/28.4 | 完整 | 快速替换/重入/独立取消/Atlas 克隆均有证据；不强刷新同 URL |
| **D06** 缓存/调度 | ResourceImageOptions、Images/Bundle FIFO、IsQueued；27～28 | 当前策略完整 | TTL/容量/并发有界，活跃 owner 不淘汰；FIFO 不宣称 priority |
| **E01** 激活/回滚 | Updates、ArtifactStore.Check/Prepare/Commit/Recover；25～26 | 完整 | 唯一事务与整组恢复；真实进程崩溃未验，不扩成热共存 |
| **E02** 可信更新 | RemoteSource、Signature/Transfer/Parts/ReleaseFloors；26 | Editor 链完整，Player 拒绝 | 非 WindowsEditor 是源码硬 gate，须在 8.10 验证后显式开放，不仅补测试 |
| **E03** 签名交付 | ReadVersion + Signer.Create/CreateFromEnvironment/Export；26 | 正式 API 完整 | 不必手拼 JSON；GUI 仅当前版，历史签名便利入口/更广用法按需，不纳入 C05 重做 |
| **E04** 内容加密 | 正式ResourceProject/Builder/Publisher/Content/ResourceStartup；28.10.19～20 | G1～G4正式交付，上轮技术审核结论沿用 | Windows x64 Editor/Mono、CNG AES-GCM+BC HKDF、64KiB/2slot/32KiB、128MiB显式预算；Scene延迟坏Stream及已记观察限制保留，不重开专题 |
| **E05** 磁盘查询 | QueryArtifactsAsync / ArtifactStore.QueryAsync、原 CleanupVersionsAsync/parts；28.7 | 已通过用户技术验收 | 只读枚举版本/临时量/保护原因；不另建版本索引，不接入整体 UI |
| **F01** 所有权诊断 | GetDiagnosticsReport、InstanceCache.Diagnostics、Workbench；27～28 | 完整 | 显示真实 pin/入边/owner/排队，未知明示，历史默认关闭 |
| **F02** 构建分析 | Placement/SourceBytes/Dependents、BuildResult/FileBytes；27～28 | 完整 | 预估/制品/变化原因分开；更多可视化非数据缺口，不启动 UI 重设计 |
| **F03** 成本基线 | G3正式Player历史数据+配方驱动A/回环HTTPS B/共享消费与峰值C；28.11 | 技术完成，待用户验收 | 约23.5MiB/18文件、3重复、真实4MiB partial，原始JSON/制品/源码身份完整；进程及分配计数Unavailable，不外推公网/Player |
| **F04** 用法/示例 | Resource 模块说明、首页、模板、Samples/ResourceStartup；28.6 | 本项最小例已交付 | 启动/双模式/Core/Audio/类型/退出不再占位；更多专项示例仅按实际需要扩充 |
| **F05** 平台/Player | 主体与 Sample 图、Windows Development 编译；28.6 | 编译有据，Player 未验 | 8.10 全流程/持久重启未做，远端硬拒绝；其他平台不凭格式白名单宣称支持 |

#### 28.2.3 剩余事项与优先级

- C05 已通过用户技术验收，不重复实施；E05、C02、B06 已通过用户技术验收。E04 G1～G4正式交付及上轮技术审核结论沿用，既有证据与限制见28.10.19～20。
- B02与F03已获2026-10-17联合实施授权，先B02必要门禁再固定负载补测F03，见28.11；原“按需/非本轮必做”不再适用于此两项。E03历史签名便利入口仍按需。
- 不重开E04后端、编码、安全或31项总体审计；G0/B1不冒充正式性能，G3正式Player可按原环境范围沿用。
- C03 的 C02 投放/定位与 Editor 启动衔接已闭合，完整首次启动/重启仍依赖 8.10；E02/F05 是生产交付前必须处理的后端/Player 门禁，不能写成只缺文档或一般测试。
- 不因缺 GUI/CLI、采用等价 API、安全回退为重建而重复开发已有功能；不自动安排 archive、priority、native 热共存或整体 UI。

#### 28.2.4 首项决定及停止点

原首项建议 C05 已获单独授权并技术交付，连带 C04/F04 必需范围，详见 28.6。2026-10-16 新授权仅推进 E05，并不表示当时已验收 C05 或整体 8.9；C05 后于 2026-10-17 通过用户技术验收，见 28.6.5，该历史授权边界不变。不自动授予其他专项、Git 写入、Workbench 重设计或 8.10 权限。

### 28.3 既有功能补齐的使用约束

- 压缩保存于 ResourceProject.Compression，正常 Build/Publish；三种均进入 Settings 身份、保持后验，不能切换成任意 flags。
- 显式 Atlas 位于专用根、非 Variant、Include in Build 关闭、Sprite 名唯一。修改 packables 后先保存并调用 PrepareAtlases，再冻结输入构建；首次 pack 写回缓存不是可忽略的源变化。
- GetSprite 克隆由消费者销毁，Atlas 由 Lease 保活；组件代次/placeholder 保护不变。不调用全工程 PackAllAtlases，不改变原图集或 spritePackerMode。
- 图片并发默认 4，可显式 1～32。ScenePreview 是 Editor 路径验证，SceneBundle 是真实包准备，均不伪造已加载 Scene。
- OpenBuiltIn 只按明确 project/digest/platform/App 与允许根定位，再调用严格 Open；不从待验证文件自证版本，不部署 StreamingAssets，不忽略额外 .meta。

### 28.4 功能补齐的最终证据

| 门禁 | 历史最终结果与定位 |
|---|---|
| Resource PlayMode | **68/68**；图片 23 例、等待/claim/Lease/Scope、FIFO/独立取消/退出、Diagnostics/预加载；`yl89-functional-play-results.json` |
| Editor/编译/最小 GUI | **40/40**；BuildMap/Atlas/压缩/Simulation/Snapshot、主体闭包与工作台；`yl89-functional-editor-results.json` |
| Native 联测 | **3/3 叶**；`yl89-functional-native-results.json`。综合叶内多段合同不另计叶数 |

三个 native 入口为 `ExplicitAtlasClonesUseNormalLeasesAndBuiltInMountDoesNotCreateCache`、`SimulationGroupScenePreviewsArePreparedWithoutLoadingAndRequiredFailureKeepsOutsideOwners`、`Native89OwnersPreloadImagesAndCoreRegressionShareOnePublication`。证据文件位于 `%TEMP%/kilo/`。

V1/V2 Atlas 的真实 BuildMap/manifest/render-data、Clone/Texture 生命周期与严格 Snapshot 通过；Simulation ScenePreview 的 required 回滚不伤外部 owner。该批一次 Review 的 ScenePreview Important 与同步 URL 文档 Minor 已闭合。单 Text 压缩样本 LZ4/LZMA/Uncompressed 为 1336/957/3136B，仅是小样本，不给普遍压缩率或耗时排序结论。

### 28.5 平台与后续交付矩阵

| 环境 | 已有证据 | 未完成/拒绝边界 |
|---|---|---|
| Windows Editor / Win64 资源 | 正式构建、模拟/native 消费、版本事务、签名 HTTPS 与 C05 双模式 | 不代替实际 Player |
| Windows64 Mono Player | Runtime/Sample Development 源码共同编译、实际 Player 图隔离 | 资源完整流程与持久重启未验；远端硬 gate 未开放 |
| OSX/Linux 桌面 | 普通目录/平台格式分支存在 | 无本批跨主机真实验收；远端拒绝 |
| IL2CPP/Android/iOS/WebGL | 仅部分平台映射或计划范围 | 没有相应最终闭包/运行；mount 仍受桌面范围限制，jar/WebGL 内置提取未实现，不只是缺设备 |

8.10 须单独授权后验阶段完整流程、必要 Player/重启、许可证/导出和交接；阶段 9 的干净工程导入另行实施。StreamingAssets 写入、加密、自动 Atlas、跨平台或协议变更均不随此表自动启动。

### 28.6 C05 统一启动与最小接入：已通过用户技术验收

#### 28.6.1 正式入口与使用

- Runtime：`ResourceConfiguration.Create(ResourceLoadMode.Bundles, mounts, cacheRoot, images)`。mount 复用 ProjectId/平台/App、明确快照与 compiled types；无 cacheRoot 只读快照，有 cacheRoot 复用 ArtifactStore，配置创建不 Mount/建目录/下载。
- Editor：`ResourceEditorSimulation.CreateConfiguration(mode, definitions, mounts?, cacheRoot?, images?)`。Simulation 可使用当前定义或明确版本闭包；Bundles 直接走 Runtime 工厂，任何校验失败都不切模拟。
- `new ResourceStartup(configuration, bindCore: ..., audioProjects: ...)` 后显式 StartAsync 并 TakeResult；属性读取不启动。保持同一配置引用；成功请求结果借读启动对象，Dispose 请求不等于 Stop。
- 先验证可选 Audio 项目，再绑定可选 Core。已有不同 Core Provider 明确冲突；共享绑定最后使用者按引用归还，不摘后来替换者。Audio 只取得正式 Provider，不改偏好或关闭全局 AudioKit。
- 应用先归还自身 Scope/播放/实例/Scene 等，再 StopAsync。借用外部会话不关闭；自建会话遇其他 startup/live owner 返回 Busy 并保留。失败清理被阻塞时同样保留关闭责任供重试；取消关闭等待不丢失内部排空。

#### 28.6.2 正式样例与操作

示例位于 `Samples/ResourceStartup/`，Runtime/Editor 分 asmdef，不引用 MainModule/XFAB/测试；自定义 Data 显式 typeof。默认框架导出包含样例源码，生成的应用目录不在默认导出根。

1. 无脏场景的 Edit Mode 执行 `YLFramework/Samples/Create Resource Startup Example`，只新建唯一 `Assets/YLResourceStartupExample[后缀]/` 的 Text/WAV/Data、Project、Scene 和 GUID，打开该独立 Scene。
2. Play 中点击 `Run Selected Mode` 跑 EditorSimulation；加载 Scene/Inspector 不自动启动。
3. 退出、保存示例 Scene，点 `Build And Publish Example Bundle` 取得真实绝对目录/digest，再保存；明确改为 Bundles 后使用同一按钮、同一 Run 业务。
4. 成功显示 `Passed: Hello from ResourceStartup / Explicit compiled sample type`。本次 Project/Core/Audio 选择开始时冻结，Inspector 中途编辑只影响下次；可关闭 BindCore/UseAudio。
5. Sound 偏好/接纳由应用负责，示例不强开。外部播放或 idle cache 责任可导致 Busy，由真实 owner 归还后点 `Stop / Retry Shutdown`，不全局 Shutdown/Clear。

此样例构建不自动部署。C02 本批新增确切版本 Preflight/Deploy/Recover 与 UseBuiltIn 明确选择，运行时按身份定位而非转换开发机绝对发布路径，见 28.8。Player 只选合法 Bundles/本地正式来源；完整 Player 门禁仍未通过。Module 模板接入注释与文档首页已同步，不再以未定义 configuration 作为正式最小例。

#### 28.6.3 最终验证与独立 Review

| 门禁 | 结果与实际证据 |
|---|---|
| 配置/生命周期/编译 | **5/5 Passed**；ResourceStartupConfigurationTests 两叶、ResourceStartupTests 综合叶、ResourceStartupCompilationTests 两叶；`%TEMP%/kilo/yl-c05-targeted-results.json` |
| 正式双模式 native 样例 | **1/1 Passed**；`ResourceStartupExampleTests.SameFormalBusinessRunsSimulationAndNativeBundlesAndRejectsBadConfiguration`；`yl-c05-native-final-results.txt` |
| 编译范围 | 完整 Runtime 与 Sample Runtime 共同 Windows Development 源码编译，Sample 使用刚编译的 Player Runtime；实际 Player 图排除 Sample.Editor。不是 Player BuildReport/运行 |

共 **6 个不同 C05 叶**，不把发现树 1698 项、异常 aggregate Duration、旧结果缓存或 native 综合叶内的多个断言另算通过。MCP native 等待超时后，待实际运行结束再从 Unity Test Runner 读取最终结果，没有并发重启测试或以 timeout 当成功。

一次独立 Review 的两项均真实 RED→GREEN，未安排第二轮：

- P1：失败保留旧自建会话后，外部换代重试可能继承 own 标记；现按具体 ResourceSystem 引用保留 ownership，既不丢同实例责任，也不关闭外部新实例。
- P2：样例跨 yield 重读可编辑字段，可能换地址/访问未装配 Provider；现冻结本次 ProjectId/Core/Audio，原装配和业务全程一致。

另覆盖初始化中重入结束会话、取消、未知 Audio 项目、绑定替换、独立 startup/lease Busy、错误 digest/重复项目/缺 compiled type、只读配置不建 cache、显式停止。Sound 原值关闭时先明确拒播，隔离验证临时启用后精确恢复，没有在产品中强开偏好。

#### 28.6.4 完成度、保护与停止

- C05 以及本项必要 C04/F04 范围已交付，**已通过用户技术验收**；不是整体 8.9 COMPLETE，也不是所有专项使用示例都必须本轮补齐。
- C05 最终现场无编译失败、非 Play/编译/导入，底层 authority 为空、native Bundle=0；恢复唯一干净 Main Scene，无生成夹具 Project 残留。新 meta 由 Unity 生成，GUID 保持。
- native 夹具对 Games/Scenes/StreamingAssets/主体 Prefabs/MainModule 历史 Bundle/Packages/ProjectSettings/AGENTS/用户配置/并行 UI Plan 前后摘要一致；Sound 原值恢复。仅清理自有新夹具 GUID、workspace/release。
- 上一轮文档维护只整理本文件，C05 技术证据沿用并核对原始结果，不冒充本轮重新验收 C05。既有 8.9、C05 与用户/并行成果不回退、不清理、不重新归属。
- C05 当时保持待用户验收，后续于 2026-10-17 通过用户技术验收，见 28.6.5。此后的 E05 单项授权见 28.7，不授权 B02/UI/8.10 或 Git 写入；阶段 8 剩余成果最后统一收口，不能无差别打包用户/并行成果。

#### 28.6.5 用户技术验收（2026-10-17）

- 用户已审核 C05 统一启动与最小接入的交付范围、配置与启动/停止、自建/借用所有权、可选 Core/Audio 装配、正式双模式样例，以及 5/5 配置/生命周期/编译叶、1/1 双模式 native 叶和独立 Review P1/P2 的真实 RED→GREEN 证据，确认 **C05 统一启动与最小接入：已通过用户技术验收**。
- 本项必需的 C04 模式选择与 F04 最小使用样例随 C05 一并认可；不扩大为 C03/F05 或其他专项的验收，不改变 E02 远端对 Windows Editor 以外的硬限制。
- 本次仅回写验收结论，不重新实施 C05、不重跑技术门禁、不改产品/测试/资源；C05 技术验收完成不代表整体 8.9 或阶段 8 验收完成，8.10 仍未实施。
- 8.9 整体 Git 收口仍按既定策略在整体完成并获用户验收后统一执行；本轮无任何 Git 写操作。

### 28.7 E05 ArtifactStore 只读枚举与清理预览

#### 28.7.1 授权与实现边界

- 用户于 2026-10-16 正式授权本项，基线现场为 `fusion/8-resource` / `ddf154cf2c72cd0b403cdd1547eee8ae06963547`。既有 8.9/C05/用户/并行工作区是正常前置状态，不清理或补提交。
- 最新 Git 策略为阶段 8 剩余成果最后统一提交收口，本项无任何 Git 写操作，不为拆提交改造产品；不延伸到 C02/C03/B06/E04/B02/F03、整体 Workbench UI、远端 Player 开放、跨平台或 8.10。
- 正式入口 `ResourceConfiguration.QueryArtifactsAsync` 与 `ArtifactStore.QueryAsync`，结果为只读 `ArtifactStoreReport`；使用方在 Unity 主线程驱动/释放 IEnumerator，不初始化服务或创建宿主。完整可用调用例见 Resource.md 的“Store 只读查询”。
- 复用原 owner/index/Prepared/Stage/Partial 校验；保护路径和 held 来源与 Cleanup 共用，删除仍走原入口。新 Query 不调用原 GetPreparedUpdates/Mount/Recover，不创建 root/lock/cache/staging，不修改磁盘记录或增加 pin。
- 只列明确 owner 闭包及其受管层级，未知目录和链接不递归；空间为不重复的文件逻辑长度，未读/未知标明不完整，非实际释放空间/native memory。CompleteRecord 仅是记录与文件集合事实，不默认 hash Bundle、不宣称完整性或可挂载已验证。
- 分段上限 32 步、总上限默认 10000（显式 1..100000，含复查）；不长期持 Store 写锁。进程内任一 Store 事务/相关会话/持有变化及观测文件属性变化使预览失效，Busy/Changed/Incomplete/Canceled 等非 Ready 结果撤销所有候选。现有 OS 调用不保证硬实时，结果不是跨进程磁盘原子快照。

#### 28.7.2 实际定向门禁

- 已实际观察缺少 QueryArtifactsAsync 的 RED，并验证不初始化/不建目录的首例 GREEN；随后 13 个真实文件系统用例通过。
- 自检的损坏 Complete 用例曾将坏记录报为 CompleteRecord，已提炼原 Snapshot 的 Complete 校验并 RED→GREEN；不增加 Bundle 全量 SHA 扫描。
- Review 修复后的文件系统查询组 **22/22**、Catalog codec 定向组 **15/15**，同轮实际 **37/37 Passed**。覆盖完整多项目闭包、active/LKG/Prepared from-to 叠加原因、取消 staging、partial 三件组/孤立 next、无归属/坏记录/读取失败、文件逻辑长度与不重复统计、未配置/缺目录/重复查询不写入、journal/next 不恢复、跨实例 Store 操作/文件变化、Busy、主线程和预算/取消。
- Windows junction 指向 Store 外、但仍由本夹具拥有的 temp 目录，定向重验 **1/1 Passed**；目标文件未被遍历，链接条目尺寸不完整/非候选。锁住普通 Bundle 文件的同时仍可查询，证明展示长度未默认重读其 SHA。
- 正式 native 叶 `ArtifactQueriesObserveRealHeldAndInFlightVersionsWithoutPinsAndCleanupRechecksOldPreviews` **1/1 Passed**（test body 约 8.916s）。真实 Asset/Bundle producer、lease 与在途版本被观测；前后 lease/root pin/Bundle record 不变，旧 candidate 后新 Prepare 的版本仍被 `CleanupVersionsAsync` 重新保护；Shutdown 跨段导致 Changed/撤销候选。
- 原 `CleanupProtectsActiveGoodPreparedAndUnknownContentAndReportsLockedFile` 与 `CleanupReclaimsCanceledStagesAndCanContinueAfterADeletionFailure` **2/2 Passed**。补充选中的原 native 生命周期、整闭包回滚、journal/截断替换三叶也实际读到 Passed；未重审 31 项或重跑阶段 8 全量验收。
- 首轮长测跨 Domain Reload 后 MCP 只保留部分叶缓存，没有把超时或总发现树计为通过。改用 CLI 长等待并拆开必需 native 叶取得实际结果；工具显示的 `TotalTests=1721` 是发现树，不是执行数。native 新用例曾因测试闭包跨 EnterPlayMode 重载失效而 NRE，按原夹具模式延后创建闭包后真实重验通过，不是删断言或放宽产品逻辑。
- 原始结果在本机 `%TEMP%/kilo/yl-e05-files-final-results.json`（37 叶）、`yl-e05-native-final-results.json`（native 叶）、`yl-e05-cleanup-final-results.json`（原清理两叶）、`yl-e05-link-final-results.json`（外部 junction 叶）与 `yl-e05-final-state.json`。只统计实际执行叶，补充回归、复验不重复计数。

#### 28.7.3 独立 Review 与修复

- 已完成**一次独立只读 Review**，同时核查只读性、规则一致性、并发边界、异常、可读性与命名；未重审既有 8.9/C05，未追加重复审查。
- 发现并修复两项 P2、一项 P3：partial next 在验证后首次统计前可被替换；过滤后的 Prepared 枚举和尾部 O(保护数×条目数) 核对漏计扫描预算；静止未知 `.json` 目录误报 Changed。四个定向用例均先实际 RED，再修复 GREEN，并通过上述完整相关组回归。
- 修复保持最小范围：验证前观测 metadata/part/next；Prepared 按目录项逐项计费后筛选；扫描内临时 HashSet 做保护查找并逐项 yield；目录/链接不按普通 JSON 文件处理。没有新增持久索引、监控或清理权威，也未改生命周期、Catalog v1 或签名格式。

#### 28.7.4 最终状态与停止点

- Unity 正式编译结束，`isCompiling=false / isUpdating=false / scriptCompilationFailed=false`。Player 编译图包含三个新 Runtime 源文件，`YLFramework.Runtime` 无 UnityEditor 引用；这里只核对编译图与本轮 Unity 编译，未声称执行完整 Player 构建/运行。
- 自有 native 夹具已按原所有权门禁清理；Main Scene 已恢复且不 dirty，ResourceSystem authority 为空，native Bundle 为 0。原夹具资产保护摘要通过；另对 AGENTS、Git 忽略/Kilo 配置、并行 UI Plan、Packages manifest/lock、ProjectSettings/ProBuilder 八项做前后摘要核对，均一致。新增五个脚本均由 Unity 生成 `.meta`，身份互不重复。
- 既有 8.9/C05 和用户/并行改动仍在工作区，未回退、清理、接管或为拆提交重写。全程未执行 Git 写操作；阶段 8 剩余成果最后统一收口的决定仍有效，不等于本项获得提交许可。
- 未做整体 Workbench UI、C02/C03、跨平台、Player 远端/网络或专项性能验收；不承诺跨进程原子磁盘快照、抗外部伪造属性篡改、实际物理释放空间或硬实时 IO。
- **E05 技术完成，已于 2026-10-16 通过用户技术验收。** 原技术证据沿用，不标记整个 8.9 或阶段 8 完成。本项已停止；后续 C02 另行受限授权见 28.8，不自动推进其他专项、8.10、提交、合并或里程碑。

### 28.8 C02 StreamingAssets 安全部署

#### 28.8.1 本轮授权与实施裁定

- 2026-10-16 E05 已通过用户技术验收。现场核对仍为 `fusion/8-resource` / `ddf154c`，既有 8.9/C05/E05 与用户/并行修改继续保留，不要求先提交，不重审 31 项。本轮无 Git 写操作。
- 写入仅限 `Assets/StreamingAssets/YLFrameworkResources/` 新建或已验证归属的部署内容与必要新目录/meta。原 MainModule、其他 StreamingAssets 内容、已有父目录/meta 不改写；未知内容、损坏归属和链接拒绝认领/覆盖/迁走。
- 元数据裁定：采用 `<ProjectId>/<Platform>/<CatalogDigest>/payload~/` 部署外壳。实现时定向核对 Windows 路径预算，去掉初选方案重复的内层 digest（否则本工程必定超出 Unity Mono 260 字符限制）；外层明确 digest + receipt 绑定身份，仅内部窄入口适配 payload 目录名，Catalog/Complete/文件闭包/SHA 校验不变。Unity 2022.3 SpecialFolders 文档规定 `~` 后缀不进入 AssetDatabase 导入，StreamingAssets 文档规定原样投放文件；本轮必须以实际 AssetDatabase Refresh 验证前一行为，Player 打包及首次启动/重启仍留 8.10，不以文档或 Editor 成功替代。
- 外壳归属控制文件同样使用 `~` 后缀，目录 `.meta` 仅存在于外壳相邻的受管目录项；不进入正式 payload、不改 Catalog v1、CatalogDigest、Complete 或 Bundle。`OpenBuiltIn` 只在明确外壳标识成立时适配到内部原严格 Open，payload 内任意额外 `.meta`/未知项继续拒绝。既有普通导出布局仍由原 Open 验证，不全局忽略 `.meta`。
- staging、锁和恢复 journal 使用工程 `AssetBundles` 下本操作明确 owned 的独立空间，不进入 StreamingAssets 或正式发布快照。以新增不可变完整版本为提交对象，不覆盖/删除旧部署、不切 Runtime active/LKG、不初始化 ResourceSystem。失败/取消留明确恢复入口；不承诺普通复制或多目录步骤的绝对断电原子性。
- Catalog v1 已封闭单项目的 Bundle/资产/依赖闭包，不新增跨项目依赖协议；每次显式选择 Publisher 的确切完整版本，应用多项目配置继续显式组成完整 mounts，由已有 Configuration/Store 检查，不扫描猜最新。

#### 28.8.2 正式入口与用法

- `ResourceBuiltInDeployer.Preflight(ResourceRelease)` 复用 Publisher owned 完整版本复验；`Preflight(ResourceLocalSnapshot)` 支持明确完整发布快照，重新校验 Catalog v1/兼容信息/Complete/必要资源闭包与文件字节。只读，不建目录/锁、不 Recover，冲突直接指出路径。
- Plan 明确 `Source.Document`、`ProjectGuid`、目标 `DirectoryPath`、`Files`、`PayloadBytes`、`WorkspacePath`、`IsRepeat`。`Deploy(plan, cancellation)` 复验后才写自有外部stage，完整校验控制文件和payload后，搬入最外层缺失的owned节点。结果含实际 Snapshot、`RelativeDirectory`、CopiedBytes、IsRepeat 与冻结身份，调用方不手拼 JSON。
- 布局为 `Assets/StreamingAssets/YLFrameworkResources/<ProjectId>/<Platform>/<CatalogDigest>/payload~/`；root/project/platform 的 `ylr-owner.json~` 及版本 receipt 同属忽略导入的控制面。只有owned目录的合法相邻folder meta可存在；payload额外meta/未知项不被忽略。普通 Open 仍要求digest末级目录，内部窄适配只在完整外壳验证后生效，未变更冻结协议。
- `ResourceLocalSnapshot.OpenBuiltIn` 使用明确身份与 `Application.streamingAssetsPath`，无需开发机发布绝对路径。Snapshot 保留私有外壳来源，原 ArtifactStore Reopen 同时复验外壳；不把payload目录名特例推广到所有来源。
- 运行时继续 `ResourceProjectMount` -> `ResourceConfiguration.Create(Bundles, mounts, optionalPrivateCacheRoot)` -> `ResourceStartup.StartAsync/TakeResult` -> Scope 业务 -> 归还自身责任 -> StopAsync。纯构造/部署不初始化、不建持久Store、不激活或更改active/LKG，不代启动Core/Audio。
- 正式样例 Inspector 增加 `Preflight Selected Built-In Release` / `Deploy Selected Release To Built-In` / `Recover Built-In Deployment`。Build 选中明确新发布且 UseBuiltIn=false；显式部署成功才设 UseBuiltIn=true，仅标记自有Scene dirty，由应用选择Bundles并保存。应用样例保留 Data 的显式 typeof，运行链不自动fallback。完整可编译配置方法、恢复入口与责任见 Resource.md。
- `Recover(project, target)` 或显式 projectId/platform/ProjectGuid 重载：若已装入完整版本，复验后确认；否则只删除journal登记的私有stage，返回null供重新预检。坏归属、坏/冲突journal/next、未知stage文件和链接拒绝恢复写入。不得按异常假定“未安装”；不删除旧部署，不新增全局GC。

#### 28.8.3 实际技术门禁

| 门禁 | 本轮真实结果 |
|---|---|
| 文件系统、取消与恢复 | `ResourceBuiltInDeploymentTests` **23/23 Passed**；在真实授权StreamingAssets根内仅用新独立项目，最终自有内容/meta清理 |
| Catalog/快照/启动/编译回归 | 原 Catalog codec **15/15**，原Snapshot **2/2**，原配置/生命周期/Runtime+Sample编译 **5/5**；与上述同轮 **45/45 Passed** |
| 新C02 native叶 | `ResourceStartupExampleTests.TwoExplicitPublishedVersionsDeployAndRunTheSameStartupBusinessAfterUnityImport` **1/1 Passed**；正式Publisher两明确版本、AssetDatabase真实刷新、同一Text/Data/Core业务、观测真实native Bundle、无模拟回退，关闭后归零 |
| 原C05 native叶 | `SameFormalBusinessRunsSimulationAndNativeBundlesAndRejectsBadConfiguration` 单独重验 **1/1 Passed**，保留原Simulation/Bundles/失败配置及责任链 |
| 既有正式产物复用 | 明确选择owned登记的两份冻结v1快照，真实部署/重复/导入/严格重开并清理，无重建或修改原发布 |

- 共 **47 个不同测试叶**，不把 native 叶内部多段断言、重复复验或发现树 `TotalTests=1745` 计入通过数量。跨 EnterPlayMode 的 MCP 聚合 Duration 仍异常且只缓存部分叶，因此两个 native 叶分别取得明确 Passed；不采用异常时长或缓存缺失推断成功。末轮原Snapshot两叶包含必要自有夹具构建，超出短MCP等待时未重启测试，改用CLI长等待读取同一运行最终完整45条Passed。
- 复用项目为 `yl-release-3d2075f754ed4d5d9b345d65ee1dd532`，ProjectGuid=`1e98cd1d2da979e488ae391c25a7822f`，Win64/App=`fixture-v1`。明确版本为 `e9912c107f6790e3feae5b2c3f68ca7dca9391b3116f23fdce66c1b0ed65e05c` 与 `575de52efba4cc17534829d0f14bf188478193f0eaf68b981263e71a298e883e`，来源来自Publisher owner/index登记而非猜“最新”；源目录与第一版部署逐文件摘要保持一致，第二版复制91760B，payload meta为0，重复部署不改文件/时间。
- 单元门禁覆盖首次/重复/两版本、只读预检不建workspace、源在预检后篡改、同digest不同ProjectGuid拒绝、外壳未知项/孤立meta/坏归属/坏Bundle、六个提交前取消点、三个提交后故障点（装入后、导入前、导入后）且源已删除时恢复、损坏/冲突journal、未知stage不删、真实Windows junction及悬空root junction拒绝且不改写外部目标。payload额外meta明确失败，不放宽原文件闭包。悬空junction追加探针证实Mono仍将它报为存在的directory且有ReparsePoint，既有拒绝逻辑直接通过，无需放宽或额外修改产品。
- native新叶的V1直接内置、V2配置空私有cacheRoot：均走正式 ResourceStartup，读取不同的真实Text与已编译自定义Data，通过显式Core桥消费；没有模拟fallback，也未因首次Store回退而创建cache。Scope/请求/Provider关闭后 authority为空、nativeBundle=0；测试不强开全局Audio偏好。
- 首轮native叶因第一次显式Deploy将夹具Scene按设计标dirty，第二次Build正确拒绝；夹具改为先明确保存自身新Scene后通过，没有让Builder自动保存用户Scene。一次既有产物动态探针遇待导入/编译状态被ValidateEditor拒绝，待Unity实际稳定后按明确身份继续并完成验证，不绕过主线程/导入门禁。
- Unity编译无失败；本轮还实际编译完整Runtime与Sample Runtime为Windows Development源码闭包，Player编译图排除Editor/可选XFAB输入。它不是Player BuildReport或运行。未重跑E05、全部Bundle、31项审计或阶段8全量。

#### 28.8.4 一次独立Review及收口

- 完成一次独立只读Review，检查归属、失败恢复、元数据边界、路径/链接、交付可用性及命名/结构；未新增另一轮审查。
- 两项P2均实际RED→GREEN：带cacheRoot的Store首次重开内置外壳失败，现由Snapshot保留来源并复验完整外壳，回归还包含坏receipt Mount拒绝及内置到内置目标Check/Prepare；最终复验后的取消窗口，现在Move前最后检查token，六个取消点最终均通过。
- 一项测试P3为native新叶污染共享Message/序列化字段/已保存Scene，现用叶级SessionState恢复原字段和原Message字节、只保存自身Scene并清理自有部署；原C05叶另行重验通过。未留下延后处理的Review问题。
- 收口自检补充修复：journal原先在AssetDatabase刷新及最终读取前就删除，若此后异常不能由Recover确认已安装版本。导入前/后两个故障注入均实际RED→GREEN；现在Deploy和Recover均在导入、严格复验及结果构造全部成功后最后清除记录，期间故障保留明确恢复身份，不扩大删除范围。
- 外部stage/锁/journal均未混入StreamingAssets或正式快照；中断未安装则可恢复中止，已安装则确认完整新版本。对损坏归属/控制记录或未知内容拒绝自动修补，不承诺绝对断电原子性或抗任意恶意跨进程文件竞态。
- 最终工程保护摘要与实施前逐字节一致：`4d9ac127a2e7fe00bb7c674d15c8bf3a0dcd502fdcd4848807f95b808dc8d1f7`。覆盖AGENTS、ignore/Kilo配置、并行UI Plan、Packages manifest/lock、ProjectSettings/ProBuilder、已有StreamingAssets父meta，以及Games/Scenes/StreamingAssets/主体Prefabs/MainModule历史Bundle；native夹具另核对完整Packages/ProjectSettings目录。本轮未改上述内容或接管既有dirty。
- 最终Unity为非Play/编译/导入，scriptCompilationFailed=false；唯一恢复的 `Assets/Scenes/Main.unity` 已加载且不dirty，ResourceSystem authority为空、Core未绑定、nativeBundle=0。StreamingAssets重新仅有原MainModule及原meta，本轮测试root及孤立meta均不存在，无YLBI待恢复journal。三个新脚本meta均由Unity生成，GUID互异；所有测试只清自有新项目/版本及对应meta，未清用户未知内容。
- 当前仍为 `fusion/8-resource` / `ddf154c`，阶段8既有修改与用户/并行未跟踪成果继续保留。`git diff --check` 无空白错误（仅原NuGet/manifest换行提示）；全程无Git写操作，不暂存/Commit/Push/切分支/合并/里程碑。
- **C02 技术完成，待用户验收。** C03仅覆盖本批真实部署/定位和Editor直接/空Store启动衔接；Windows Player交付包中payload~保留、首次启动及更新后持久重启仍留8.10，不标记整个C03、8.9或阶段8完成。至此停止，不自动实施B06、E04、Workbench优化或8.10。

### 28.9 B06 自动 SpriteAtlas：已通过用户技术验收

#### 28.9.1 授权、设计与门禁

- 2026-10-16 用户确认 C02 已通过技术验收，正式授权 B06。现场为 `fusion/8-resource` / `ddf154c`；既有 8.9/C05/E05/C02 及并行 UI 成果保留，本项无任何 Git 写操作。C03 的 Player 交付、首次启动和持久重启仍留 8.10。
- 自有生成范围由 `ResourceAtlasGenerator.CreateConfiguration(project, folderName)` 显式登记：仅在该 Project 的专用资源根下新建一个此前不存在的直接子目录，内含配置及本功能生成的 Atlas/meta。未知目录、文件或孤立 meta 不接管；不改手工 Atlas、源 Texture/Sprite Importer、全局 Packer、正式 Scene/Prefab 或 StreamingAssets。
- 配置使用可保存的 `ResourceAtlasConfiguration`，规则指定稳定 Id、目标既有 Group、目录/Texture2D 集合、Together/PerFolder 组织、逻辑地址前缀及必要打包/纹理设置。输出登记只承担 Editor 归属、输入指纹和文件完整性，不成为 Runtime 映射表；最终 Group/BuildMap/Catalog 仍是原正式链。
- Together 以规则稳定 Id 为身份，PerFolder 额外使用源文件夹 GUID；源增删/移动及设置变化更新存续输出而不重建 meta。来源与输出目录不得重叠；规则停用的输出明确报 Obsolete、退出正式选择但保留资产，仅显式删除且重新核对归属与引用。
- `Preview` 纯读；生成、更新及 owned Atlas 准备在 Builder 最终 BuildMap/设置/指纹冻结前完成。`ResourceBuildMap.Create` 不偷偷生成，未同步状态明确拒绝。生成失败停止本次构建，普通异常/取消回滚本次自有写入，不以旧 Atlas 冒充新成功；不承诺进程崩溃的绝对原子事务。
- 实施顺序：先建立缺失能力 RED；实现配置/只读计划/生成与保护；接入 Builder 和最小 Inspector；执行生成身份/冲突/失败与真实构建、Catalog、Runtime 图像和 Lease 回归；一次独立 Review 后定向修复；更新本节与 Resource.md。仅验证新增及直接受影响路径，不重跑阶段 8/C02 全集。
- 继续沿用 B05 的非 Variant、Include in Build 关闭、唯一 Sprite 名和显式 Atlas Lease/GetSprite 克隆合同；不实施全局 atlasRequested、Prefab Sprite 透明晚绑定、平台体系或 Catalog v1 变化。完成后仅标记“B06 技术完成，待用户验收”，不启动下一专项或 8.10。

#### 28.9.2 机制取舍与当前证据

- 定向读取实际安装的 XFABManager `1.24.0` / `c35dc4058ecd831409199b43cb328024202d4557`：`ProjectBuild.cs:724-807` 每次删除重建项目图集目录，`AtlasPacking.cs` 以物理包成员过滤小单 Sprite、Include in Build=true，图集并未显式加入构建清单；当前 MainModule 旧 manifest 中 SpriteAtlas ClassTypes 与仅 PNG Assets 印证其隐式打包路径。仅只读研究，未调用原构建或重建历史产物。
- 融合其配置化收集、独立生成入口与构建前执行便利，不照搬删目录、成员摘要命名、硬编码尺寸过滤或隐式晚绑定。YL 的规则直接指定目标 Group，生成先于最终依赖分析，支持 Multiple Sprite 与稳定文件夹 GUID；Runtime 仍显式取得 Atlas Lease/GetSprite。
- `ResourceAtlasConfiguration` 的可编辑 Rules 与隐藏 Editor 归属记录分离。`Preview` 只读展开 Create/Update/Current/Obsolete；输出目录闭合检查、文件/meta hash、Project/根/目录/配置 GUID 交叉校验阻止未知接管或外部改动覆盖。Project 另保留注册配置 GUID，断开/丢失引用不会被当成“关闭自动图集”静默漏包。
- `ResourceAutomaticAtlasTests` 初轮 **14/14 Passed**：首次/重复、源增删与目录移动、PerFolder GUID、设置/地址变化、失效退出构建及引用阻止删除、未知目录/meta/文件、缺失输出、重名 Sprite/非法设置、取消与来源在导入中变化的回滚、断开配置引用拒绝、正式 Preflight/BuildAndPublish/BuildMap/Catalog。初始缺失 API、未接入 Group、未接入构建预检及断开引用漏包均有实际 RED；这里不将测试发现树数计为通过。
- 新 native 叶 `AutomaticAtlasPixelsAndConsumerClonesUseTheExistingLeaseAndSharedTextureContract` **1/1 Passed**：真实 Unity 导入/准备/BuildMap/Bundle/Catalog 后进入 Play，经唯一 ResourceSystem 与 ResourceImage 消费自动 Atlas；GPU 读回 Left 绿色、Right 蓝色，替换/停用销毁自身克隆，不伤独立 Lease 的共享 Texture，最终真实 Atlas/Texture/Bundle 归零。工具跨 Domain Reload 的聚合 Duration 异常，不作为耗时或执行数证据；该叶明确 Passed、body 约 5.605s。
- 支持边界仍为当前 Unity `2022.3.62f3c1` / Windows Editor / `StandaloneWindows64`、SpriteAtlasV2 实测；无本批 Player/持久重启/其他平台验收。正式配置、Inspector/API 操作和加载责任已增量写入 Resource.md；上述为首轮证据，最终 Review/回归/保护结果见下节。

#### 28.9.3 独立 Review 与定向修复

完成一次独立只读 Review，发现 1 项 P1、5 项 P2，均已修复；未增加第二轮 Review，也未扩展审计其它既有成果。

| 发现 | 修复及验证 |
| --- | --- |
| P1：导入回调占用下一 Create 路径，原先预先快照/整计划回滚会接管或删除未知资产 | 逐项写入前复验 Create 空位或 Update 身份/字节；AtlasWrites 只记录实际开始写入的资产对，恢复前再核对最近受控写入点；两个“占用下一路径后取消/继续”的测试实际 RED→GREEN，未知资产/meta 原样保留 |
| P2：引用扫描漏掉 Packages | Assets 与 Packages 的可见资产纳入同一只读依赖扫描；真实序列化引用阻止删除，实测已安装 Package 候选确实进入扫描。为保护 Packages，未伪造/写入包内引用资产，此子场景不冒称真实跨包引用夹具通过 |
| P2：用 dirty Importer 筛选，却哈希已保存 meta | 目录候选筛选前及显式纹理均拒绝未保存的来源/Importer，不替用户保存；Sprite→Default、Default→Sprite 两个方向实际 RED→GREEN，输出与源 meta 不变 |
| P2：驻留配置对象掩盖配置文件/meta 缺失 | 磁盘文件存在性、配置 meta hash 与持久身份一起核对；未 Refresh、对象仍驻留时分别删除配置文件/meta 的两个测试实际 RED→GREEN，预览和生成均拒绝且不补写身份 |
| P2：Together 与 PerFolder 的文件名映射可能碰撞 | Preview 对全部输出路径按忽略大小写去重并明确报冲突，不改变存续命名；合法不同规则键的同路径测试实际 RED→GREEN、零输出写入 |
| P2：已被 BuildMap 排除的祖先目录选择误阻止失效删除 | 引用判断沿用自动目录排除和扩展名过滤，保留直接 GUID 与真实序列化引用保护；资源根选择不包含自动输出时删除成功，测试实际 RED→GREEN |

- 自检额外补齐三个直接相关边界，均有实际 RED→GREEN：原始 PNG 已改变但导入缓存未更新时，真实 packed preview 仍是旧红像素；现仅对明确候选正常同步导入后再冻结计划，GPU 读回已变为预期蓝色，不设置/保存源 Importer。导入回调在当前输出的磁盘 meta 中换 GUID 而 AssetDatabase 仍缓存旧 GUID 时，新增实际 meta 身份核对，拒绝接管并保留冲突内容，回滚也不删除。Builder 因 Options 前移曾把 null Project 的异常变成 NullReferenceException，已恢复原 ArgumentNullException 合同。
- 同步来源导入可能先触发旧 Atlas 的依赖导入回调；因此把“生成中改源/取消/占用下一路径”夹具的触发点收紧到本次 Padding=8 设置确实写入后，确保测试的是最终计划冻结后的窗口，而非合法的冻结前变化。合并回归中原先一个触发时机不准确的用例曾失败，未删断言、未跳过；修正触发点后全组通过。
- 失败回滚不承诺覆盖一切：若受控写入后同一路径再次被外部改变，则保留该冲突内容、报告原异常及恢复异常，阻止后续构建，要求先人工恢复已登记身份；不以“回滚成功”之名覆盖未知改动。

#### 28.9.4 最终验证、保护与停止点

| 门禁 | 实际结果 |
| --- | --- |
| 最终 B06 Editor 全组 | **26/26 Passed**，约 168.7s；含首次/幂等/源增删移动/设置、归属冲突、缺失与外部修改、回调窗口、取消/失败、正式生成前置及 BuildAndPublish/Catalog 读回。1772 是发现树总量，不是执行数 |
| B05、BuildMap、模拟索引与 Builder 受影响回归 | **28/28 Passed**：显式 V1/V2 构建 2 叶与 Atlas 其余 3 叶、BuildMap 11 叶、模拟索引 8 叶、Builder 参数/取消/重入 4 叶；不重跑阶段 8/C02 全集 |
| 最终生成 Atlas native 叶 | `AutomaticAtlasPixelsAndConsumerClonesUseTheExistingLeaseAndSharedTextureContract` **Passed**，最终 body 约 6.138s；真实绿/蓝像素、同一共享 Texture、旧克隆销毁、独立 Lease 保活、最终真实 Atlas/Texture/Bundle 卸载 |
| B05 原显式 Atlas native 叶 | `ExplicitAtlasClonesUseNormalLeasesAndBuiltInMountDoesNotCreateCache` **Passed**，body 约 5.934s；沿用本批通过且未受最后 meta 核对/空参数修复影响的 Runtime 合同证据 |
| Inspector 最小入口 | 实际创建 Project/configuration Editor，分别绑定 ResourceProjectInspector 与 ResourceAtlasConfigurationEditor；保存的 Rules=1、Sources=1 序列化字段正确，随后清理自身夹具；未做整体布局或点击自动化验收 |
| 元数据与程序集 | 6 个新脚本均有 Unity 生成的 `.meta`，GUID 互不相同；4 个产品脚本位于仅 Editor 的 YLFramework.Editor，2 个测试位于 Editor/UNITY_INCLUDE_TESTS 测试程序集；未新增 Runtime→Editor 依赖或修改 asmdef |
| 保护复验 | 每个复用资源夹具的完整 ProtectedSnapshot/ProtectedDigest 均通过，覆盖原 Games、Scenes、StreamingAssets、MainModule 历史 Bundle、Packages、ProjectSettings。另初始与收口 SHA256 **8/8 相同**：AGENTS.md、.gitignore、.kilo/kilo.jsonc、并行 UI Plan、manifest/lock、ProjectSettings.asset、ProBuilder Settings；原手工 Atlas 目录与上述原资源目录的 Git diff 为空 |
| 最终 Editor 状态 | 同一 PID 3936、当前工程正确；Compiling/Updating/Play/CompilationFailed/ResourceSystem Authority 均 false，已加载 Bundle=0，恢复 Assets/Scenes/Main.unity 且 Dirty=false |

- 计数按不同测试叶为 **26 + 28 + 2 = 56**，不是一次聚合 56，也不把重复/RED/发现树计入通过数。合并 52 叶曾有客户端超时，通过只读缓存取回发现上述一个夹具触发时机失败；其后最终 B06 全组 26/26 明确通过。native 工具跨 Domain Reload 的聚合 Duration 异常，仅采用叶 Passed 和 body 时间。
- 一次 native OneTimeSetUp 曾因新夹具发布 `.staging` 的 Windows `IOException: Access to the path ... is denied` 失败，body 未执行；自身夹具清理完成、Main 场景恢复、Authority/Bundle=0 后，同代码重验通过。未修改 Publisher/部署逻辑、放宽权限或删除未知目录；该一次性 IO 的根因未确认，不把它写成已修复的产品缺陷。较大的补充脚本探针未取得结果，未计入成功；以小型状态读取、独立 SHA256、meta/asmdef 只读检查完成相关收口，没有修改 MCP 配置或启动第二个 Unity。
- 本批真实资产写入仅在随机专用资源夹具根及其显式登记的 GeneratedAtlases 内；均按自身归属清理，未留下演示生成范围占用正式项目。用户实际接入时从 Project Inspector 在自己的专用资源根显式登记，完整步骤见 Resource.md。不再次操作 C02 正式部署根、原 StreamingAssets、Canvas、Scene/Prefab、源 Importer 设置、手工 Atlas、Package 或全局 Packer。并行 UI 成果和既有未提交成果保留。
- **B06 技术完成，已于后续 E04 授权中通过用户技术验收。** C02/E05 沿用用户已通过的技术验收，C05 当时待用户验收（后于 2026-10-17 通过，见 28.6.5），C03 Player 交付/首次启动/持久重启仍留 8.10；不标记整体 8.9 或阶段 8 完成。该实施批次已停止；后续仅 E04 设计授权见 28.10，不自动实施内容加密、配方、性能专项、整体 Editor 优化或 8.10；不暂存、Commit、Push、切换/创建分支、合并或创建里程碑。

### 28.10 E04 可选 AssetBundle 内容加密：G1～G4正式交付及技术审核结论沿用

> **当前状态优先级**：以下历史设计、研究、失败及当时授权原样保留；“只改Plan / 等待G1授权 / 不进入G4 / 批次结束停止”不再约束已完成的G1～G4交付。正式证据见28.10.19～20，上轮技术审核结论沿用；本批联合授权、Git策略及B02/F03结果见28.11，不重开E04或改写其他项目验收。

#### 28.10.1 授权、目标与结论层级

- 2026-10-16 现场只读核对：`fusion/8-resource`，HEAD `ddf154c`；暂存区为空，既有 8.9/C05/E05/C02/B06 和用户/并行修改均保留。本轮只增量维护本 Plan，不改产品、Resource.md、Package、配置、旧发布或资产，不执行任何 Git 写操作。
- **最新研究优先级，尚未实施/定案**：用户追加要求先研究“认证分块解密 + LoadFromStreamAsync”的难度与性能，只有证明具体性能或平台阻碍后才评估有限内存或受限临时明文。当前优先候选为 Tink 已定义的 AES-GCM-HKDF Streaming 格式 + 成熟库原语 + 有界可 Seek 明文视图；原“整文件 GCM + 临时文件”不再作为默认推荐。它仍接原 Bundle/Lease 图，不是另一个 ResourceSystem、下载器或版本事务。新增研究与门禁见 28.10.13。
- 最新专项授权进一步明确应用层边界：**运行期解密路径不主动创建完整明文 Bundle 文件**，不是只把正式缓存加密后把明文长期留在另一目录；不约束 Unity 构建端生成 raw Bundle，也不承诺 OS 分页/转储无明文。YooAsset/Tink 仅作边界/安全构造参照，不替换 YL 主体。续核依据与原型观察门禁见 28.10.14；前置 Plan 阶段未运行原型，首批 G0 隔离执行结果见 28.10.15，后续获准的有限 Scene 定位见 28.10.16。
- 保护目标是降低直接解包、批量复制与复用交付内容的便利度，使交付文件和下载缓存中的 Bundle 载荷保持密文，交给 native 的每个分段在返回前均已认证；不暗示按需打开时全部段已验完。HTTPS 保护传输，签名授权发布者，SHA 描述字节身份，均不替代内容加密。
- 不承诺防调试、内存转储、管理员/同账户攻击、已授权客户端提取、绝对防逆向或安全擦除。客户端必须取得可用密钥和明文；离线自动启动尤其不能同时承诺客户端无法恢复密钥。Catalog、地址、类型、依赖与文本 manifest 仍可读，加密不隐藏清单或资源长度。
- 以下区分**当前源码事实、推荐契约、实施前门禁**。2026-10-16 首批G0/SD1因Scene延迟读失败导致native崩溃的 **no-go历史不变**；后续C1仅按用户限定风险接受恢复其他G0，实际结果及审批建议见28.10.17。既有产品验收仍只支持原明文流程，不能用原型结果宣称正式加密已接入。
- 首发加密支持范围建议只考虑 Windows Editor / Windows64 Mono Player，且**将来各自通过 G0 后才可开放；当前两者均未获整体加密支持判定**。其他主机、IL2CPP、Android/iOS/WebGL 的加密明确 Unsupported，不能由 .NET Standard 声明外推。None 保持既有平台边界，远端 Player gate 不随加密开放。
- 执行阅读顺序：先看 28.10.14 的 F1～F3 已确认行为，再核对 28.10.3～28.10.9 的数据/所有权合同，最后按 28.10.13 的 G0a～G0e 取证。28.10.11 的产品 G1～G4 仅为后续影响范围，不是本轮执行许可。

#### 28.10.2 双方源码与平台依据

| 已确认事实 | 对设计的约束 |
| --- | --- |
| XFAB `1.24.0/c35dc4058e` 的 `Runtime/Tools/EncryptTools.cs:19-108` 使用 DES-CBC/PKCS7，UTF-8 前 8 字节密钥、固定空格 IV、无 MAC，异常返回 null | 不继承旧密码实现、密钥截断、吞错或旧格式；内容保护的功能价值不因此被否定 |
| XFAB `Editor/ProjectBuild/ProjectBuild.cs:169-206` 可选加密并重算交付 length/md5，且将密钥输出 Console；`Runtime/AssetBundleManager.cs:449-485,559-592` 整包读入/解密后 LoadFromMemory(Async) | 融合项目级开关、交付摘要按实际文件、同步异步共用入口的便利；不继承日志泄密、整包内存峰值或“async 名字代表解密异步”的假象 |
| `R/Loading/ResourceSystem.Bundles.cs:184-249` 是 Runtime Bundle native 创建接点；`CollectBundle:260-277` 先 Unload(true)，再释放已验证文件句柄和依赖边 | 解密输入只能接入同一 producer，新增输入所有权随 BundleRecord 收尾；Prefab/Atlas/Audio/Scene 不另开解密路径 |
| `R/Catalog/ResourceLocalSnapshot.cs:66-87,129-143` 严格验证完整文件闭包、交付 length/SHA，并保留只读句柄；`E/Publishing/ResourcePublisher.cs:470-513` 还对发布 stage 做真实 native 验证 | 不放宽闭包、不只信头/mtime，也不能为加密删除 Publisher native 校验 |
| `E/Build/ResourceBundleBuilder.cs:224-255` 每次由 BuildPipeline 写同一 native workspace，随后 ReadBack；`ResourceBuildWorkspace.cs:94-109` 拒绝未知子目录 | 禁止把 native workspace 原地改成密文再交给下次 BuildPipeline；交付派生区必须明确登记，不向既有 workspace 随意塞目录 |
| 当前 `2022.3.62f3c1`，项目 API compatibility 为 .NET Standard 档；未发现已接入的 Bouncy Castle、libsodium、Tink 或 AEAD 资源读取实现 | 新密码库是明确依赖变化；参考 API 存在不等于该 Mono Player 的运行实现可用 |
| 本机 XML 存在 `EnableProtection`、`SetAssetBundleEncryptKey/SetAssetBundleDecryptKey`，只说明 16-byte key；没有足够证据证明其认证算法/单位和多 key 行为 | 不将其作为已满足 AEAD 的候选；全局 key 状态还可能影响原游戏/其他资源调用，不调用这些 API 做推断性试验 |

前置研究阶段采用的一手资料如下；当时 Context7 查询工具未暴露，改查版本化 Unity 官方文档、标准和库原始源码，不冒称 Context7 已查询成功。后续隔离执行证据另见 28.10.15。

- [Unity 2022.3 .NET profile](https://docs.unity3d.com/2022.3/Documentation/Manual/dotnet-profile-support.html) 支持 Standard 2.1/Framework profile；[Unity Mono 的 AesGcm 源码](https://github.com/Unity-Technologies/mono/blob/unity-main/mcs/class/corlib/corefx/AesGcm.cs) 在该上游分支抛 PlatformNotSupportedException。后者不是本机安装 revision 或 Windows64 Player 的实测；现代 .NET 的 Windows 支持表也不能证明 Unity Mono 支持。因此不把 BCL AesGcm 选作无需验证的默认后端。
- [NIST SP 800-38D](https://csrc.nist.gov/pubs/sp/800/38/d/final) 定义 GCM/认证及 nonce 约束；[RFC 5869](https://www.rfc-editor.org/rfc/rfc5869.html) 定义 HKDF。密码原语采用库实现，不自写 AES、GCM、GHASH 或 KDF。
- [Bouncy Castle 官方下载](https://www.bouncycastle.org/download/bouncy-castle-c/) 与 `bcgit/bc-csharp` 的 `release-2.7.0`：[项目目标](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/BouncyCastle.Crypto.csproj) 包含 netstandard2.0；[GcmBlockCipher](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/modes/GCMBlockCipher.cs) 提供增量 ProcessBytes/最终 DoFinal 验证；[HKDF](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/generators/HkdfBytesGenerator.cs) 和 [GCM vectors](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/test/src/crypto/test/GCMTest.cs) 可作实现依据。前置阶段只核对源码；本次获准隔离运行的确切资产见 28.10.15，未安装进主工程。
- Unity 2022.3 [LoadFromFile](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.LoadFromFile.html) / [LoadFromStream](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.LoadFromStream.html) / [LoadFromStreamAsync](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.LoadFromStreamAsync.html)：LZ4/Uncompressed 可延迟读取，LZMA 解压到 native 内存；Stream 必须可 Seek、从 0 开始、允许 native 线程访问并存活至 Bundle.Unload 之后。这些文档只提供 API 合同；本次实际 Scene/Audio Player 证据另见 28.10.15。
- [libsodium secretstream](https://doc.libsodium.org/secret-key_cryptography/secretstream) 的认证消息链与 [Tink Streaming AEAD](https://developers.google.com/tink/streaming-aead) / [语言支持表](https://developers.google.com/tink/primitives-by-language) 仅用于排除“现成可直接接入 C# 随机读取”的假设，不把自行移植或未核对的第三方绑定当成熟现成方案。

#### 28.10.3 算法、认证单位与最小配置

- 推荐首版只交付 `None / Aes256GcmHkdfSegmentedV1`（后者名称待 G0 固定），默认 None。原 XFAB 的使用粒度为项目，YL 最终物理包又可能在 Group 间共享/归并，项目级策略能避免同一个依赖包出现矛盾保护要求；没有真实局部保护需求时，不增加 Group/资产级覆盖、任意算法下拉、KMS 后台或策略继承平台。`ResourceProject` 只持久化模式和稳定 `KeyId`；启用时覆盖该 Project 的全部内容 Bundle、共享依赖 Bundle 和 root manifest 二进制 Bundle；文本 `.manifest`、Catalog、Complete 保持原格式并参与原 SHA/签名链。
- 原语后端优先评估 `BouncyCastle.Cryptography 2.7.0` 的成熟 API，AES-256-GCM，**每个分块一个认证单位**，tag 固定 16B；不自写 GCM/GHASH。官方 netstandard2.0 仅作基线，低分配 netstandard2.1 受控构建为待验证候选；能被项目引用不等于满足吞吐/GC 门禁，具体风险见 28.10.13。构建密钥为 CSPRNG 生成的 32B 高熵材料，不接收密码字符串、补齐、截断或 GUID 子串充当密钥。
- 遵循 Tink 格式：每次真正加密生成随机 salt 32B 与 nonce prefix 7B；用库的 HKDF-SHA256 从主密钥派生 32B 文件 key，info 为固定 domain `YL.Resource.BundleEnvelope/v1` 加外层头的确切字节。分块 nonce 严格为 `prefix[7] || UInt32BE(index) || last[1]`，last 为 0/1；每块 GCM 的 AAD 为空，外层 context 由 HKDF info 绑定。salt/prefix 由安全 RNG 提供，不需全局 nonce 账本；随机失败即失败，不能回退 Guid/时间/固定 IV。HKDF 不把低熵密码变成高熵密钥。
- 新加密使用新 salt/prefix；不得从 plaintext SHA、BuildId 或文件名直接构造固定 nonce 来追求稳定输出，不在同一文件 key/nonce 下原地改块。复制已完成密文不属于重新加密。严格遵守 Tink 的最多 `2^32` 段及更小的既有文件/下载/平台预算，计数溢出/超限拒绝；不新增多级索引、大包拆卷或可变记录格式。
- 推荐将 G0 最终选定的 BC 构建（官方资产或明确标识的受控变体）、完整适用许可、版本/来源与实际校验值放入未来明确的 `Assets/YLFramework/Plugins/BouncyCastle/`，精确增加需要它的主体 asmdef 预编译引用；只在内部内容编解码实现中使用 BC 类型，不泄漏到公共 API、Foundation/Core。**新增产品依赖须用户裁定**；本次仅在获准隔离目录取得实验依赖，不安装进主工程。
- 现有白名单只明确允许 Json.NET，不代表禁止将来批准的密码库。实施时应为确切 BC 文件增加可证明的依赖允许项并保持外来同名 DLL、XFAB、Sirenix 等负例，不能宽放整个 Plugins 或 `System.*`。现有 Plugins 导出根可携带依赖和许可；源码/二进制闭包、Sample、Player 编译与导出门禁须定向更新。不为规避这些检查而把正式能力藏进不导出的 Integrations，也不新增永久密码后端插件框架。

#### 28.10.4 密钥提供、离线与轮换

推荐窄合同 `bool IResourceContentKeyProvider.TryCopyKey(string projectId, string keyId, byte[] destination)`，前置条件为 destination.Length==32：provider 只复制指定 key 到调用方拥有的缓冲；false 表示明确缺失，异常保留 cause，不能尝试其他 key 或把文件当明文。接口名称为设计建议，不是已存在 API。

| 环节 | 正常用法与责任 |
| --- | --- |
| Editor/CI 构建 | Project 只保存 KeyId；应用把 provider 显式传给正式 Editor 操作/Builder。最小内置适配从本次进程环境中的明确 secret 名称读取 Base64 32B 内容，或使用调用方 provider；环境名可配置/显示，值不进入 SO、EditorPrefs、workspace、命令行、report 或异常文本。CI secret 注入不由 Resource 管理 |
| 离线应用 | 安装/部署环节将必要 key 安全供给应用私有密钥来源，应用启动时构造 provider，再通过 C05 配置装配。没有网络也必须拥有 key；纯公开 Bundle 加一个公开 SO 密钥不能提供有意义隔离。首版不内置通用设备密钥库/安装器，不声称 DPAPI 或混淆已被实现 |
| 自包含离线发行 | 若应用必须零外部供给，则由应用的私有构建流程/代码提供 key，不写入框架、公开示例或仓库；生成中间物须置于其受控构建区。最终客户端包含可恢复秘密，仍可被提取。这是便利与保护上限，不是可靠密钥隔离 |
| 外部发放 | 应用自行完成鉴权、联网取 key、离线缓存及撤销策略，随后将内存 provider 交给 Resource；Resource 不新增密钥 HTTP 客户端，也不在持有 Store 锁或 native 回调时等待取 key。外部供给未完成时，明确缺 key，应用完成后显式重试 |
| C05 接入 | 建议 `ResourceDecryptionOptions`（provider、应用可配置的 `MaxBufferedBytes`）由 `ResourceConfiguration` 的构造/工厂统一接入，包括高级 ArtifactStore 构造路径；一次 ResourceSystem 初始化至关闭/失效的主体实例内，所有项目共用该缓冲上限，启动时冻结，不预分配整笔内存。首版不做热调整/自动扩张/多级配额；预算是 Runtime 限制，不进入 Builder 指纹、Catalog 或密文身份，也不强加给 Editor 发布校验。`ResourceStartup` 仅转交，不另设全局 SetSecret。流方案不要求 ScratchRoot；只构造配置不取 key、不创建目录、不下载 |
| 内存责任 | provider 属于应用，Resource 不 Dispose 外部 provider。主线程取得所需 key 副本后才做后台纯 IO/密码计算；native Read/Seek 不访问任意应用 provider、不调 Unity。派生后尽早清理主 key 副本；流自持其文件 key/密码上下文/已认证块，直到 native Unload 后释放。Prepare/发布全量校验的副本在操作结束清理；不承诺 GC、库内部副本或进程内存可被彻底擦除 |

KeyId 是不敏感、区分大小写的稳定标识，建议限制为 1..64 个 ASCII 字母/数字/`._-`。同一 `(ProjectId,KeyId)` 的材料不可重新赋义；轮换生成新 KeyId，发布新密文/新 digest，远端递增 Sequence。**keyring 和退役策略归应用所有**，应用保留 active、LKG、prepared、明确内置仍需的 key；Resource 只从 provider 取得操作副本并持有 live 输入的派生 key，不持久化 keyring 或建立密钥状态权威。磁盘清理不等于可撤销密钥；回滚只选择旧完整发布和其原 KeyId，不原地重加密，旧 key 缺失时在切换前报错，active/LKG 不变。跨机 CI/客户端同名 KeyId 的材料一致性不能凭名称证明：复用旧密文须用本次 key 认证，不符即配置冲突，不偷偷重加密为“成功”；新发布仍需应用 key 装配冒烟验证，首末/全量认证会明确暴露错配。首版不新增公开 KCV/秘密指纹注册表。没有生产 key 的公开示例只能使用明确标注的非生产测试材料。

#### 28.10.5 文件封装与冻结协议兼容

| 路线 | 真实差异与裁定建议 |
| --- | --- |
| **文件内自描述 envelope，推荐** | 保持 `manifest` / `b_<token>` 及 support 文件集合不变；新协议版本属于文件编码，不是 Catalog Schema。现有 length/SHA 本来就是物理交付字节，能直接描述密文；原闭包、Complete、签名和版本目录可继续复用 |
| 额外 delivery manifest/旁车 | 放版本目录会违反 v1 精确文件闭包；放目录外没有现有签名绑定、也不能保证缺失时不降级。必须另设计受签名绑定的新发布协议，不是“免费兼容”的小改动；首版不采用 |
| Catalog v2 | 可显式记录编码、keyId 和两层摘要，但须维护双 schema，改动所有生产/消费/部署/查询路径，仍解决不了 native 解密输入。当前只有内容编码需求，无须据此强制迁移旧发布；首版不采用 |

优先封装草案为**有界 YL context 头 + 标准 Tink RAW streaming payload**，没有自创分块索引或可变记录：8B magic `YLRENC01` + UInt32 little-endian HeaderLength + canonical UTF-8 JSON header（最多 4096B）+ Tink payload。外层固定字段为 `Version=1, Algorithm=AES256-GCM-HKDF-SEGMENTED, CiphertextSegmentSize, KeyId, ProjectId, Platform, UnityCompatibility, FileRole, LogicalId, PlainLength, PlainSha256`。首选验证 64KiB ciphertext 段，产品只固定一档，不建设调参 UI；4KiB/1MiB 仅作 G0 对照。Tink 内头为 `40 || salt[32] || noncePrefix[7]`，内段布局/末段标志完全按官方规范，firstSegmentOffset=0；外层通过子流隔离，不计入内段偏移。此 envelope 尚未落地，替换前稿不涉及历史文件迁移；冻结 Catalog v1 不变。

摘要为小写 64hex，长度为严格整数，拒未知/重复字段、非法枚举、非规范字节、溢出、短读、超预算段长和额外尾部。FileRole 为 Bundle/Manifest，Bundle 的 LogicalId 为 native BundleId，Manifest 为固定 `manifest`。复用 Catalog 的规范 JSON 编码原则但使用独立 codec，不修改其固定字段集。

- Tink 的 AssociatedData 是 domain 加 magic、HeaderLength、header 的确切字节，按规范进入 HKDF info，不误改成另一套逐段 AAD 算法。expected ProjectId/平台/Unity/角色/LogicalId 由已验证 Catalog/构建结果给出并交叉核对，不相信头来自证。PlainLength/PlainSha 描述原始压缩 Bundle 字节，不是解压后的 native 对象；打开时验证首/末段并交叉核对长度，返回任何字节前认证其整段。头在认证前不用于无界分配。
- PlainSha/PlainLength 在客户端是**已认证头与解密输出的自洽核对**，不是 Catalog v1 额外提供的独立明文摘要；Catalog 独立约束的是完整交付密文。正式构建/发布另有 raw workspace 的独立 length/SHA 可交叉核对两层关系。不能把两者混写为同一“SHA 已证明原资源”，也不因此偷偷向 v1 增加字段。
- 最终 BuildId/CatalogDigest **不放进 AAD/header**，否则密文摘要进入 Catalog 后形成循环；AppContentCompatibilityId 与 channel 由外层 Catalog/签名合同绑定，不由未认证的文件头授信。文件跨 App 复用仅在既有兼容策略允许时成立。
- 先验证 Catalog/Complete/必要签名与交付 length/SHA，再识别编码。已验证的原 UnityFS 文件走原明文加载；magic 表示加密时必须完整解析和认证。未知魔数/不支持版本/损坏头明确失败；不按“未匹配 magic 就当明文”处理。删头、改 magic、截断会破坏 Catalog SHA；认证失败、缺 key 或不支持编码均不得重试明文/native 或模拟模式。
- `UnityHash` 继续是 Unity manifest 对**原始 Bundle 内容**的 hash，不能改成 ciphertext SHA；NativeName、依赖图、资产地址与 compiled types 不变。Catalog `Length/Sha256` 是**交付文件**身份；BuildId 是包含这些交付记录的 canonical payload SHA，CatalogDigest 是最终 Catalog bytes SHA，均不是纯源资源 identity，也不是认证。
- 原签名 envelope 直接认证 CatalogDigest/CatalogLength/BuildId 和 CompleteLength/SHA，间接覆盖 Catalog 中每个密文/support 文件及封装头；Complete 不变成第二逐文件摘要清单。RSA 发布私钥与对称内容 key 必须分开，不在 envelope 塞内容 key。
- **不重写任何旧 v1 发布。** 新客户端继续读旧明文 v1；旧客户端不能读加密文件。首次加密应只投放给明确升级 E04 的客户端，例如使用其可信配置中的新 channel/endpoint，保留旧客户端原渠道；不能因 Catalog schema 相同就宣称旧客户端兼容。仅内容编码变化不必更改 AppContentCompatibilityId，旧明文 LKG 可沿原 App 合同回滚；若实际 App 合同也变更，则遵守原跨 App 拒绝规则，不绕过它。此发布隔离须进入签发/交付操作检查。

#### 28.10.6 构建、随机化复用与不可变发布

1. 正式 Preflight 冻结项目加密模式/KeyId/协议版本，并检验明确 provider 的 key 长度、目标支持及归属；缺 key 不开始加密交付。启用时模式/KeyId/协议进入构建设置身份，秘密及其可被用作凭据的输出不进入指纹、marker 或日志。None 保留原设置编码/身份和路径，不因增加默认字段而无故改变未加密产物的 fingerprint/digest。
2. Builder 保留原 BuildPipeline、Atlas 前置、源输入后验与明文 native ReadBack；**raw native 输出始终保持原字节**。加密开启时，在同一 owned workspace 下建立明确登记的派生交付区，不是第二构建器；内部 workspace 格式/闭包须显式版本化，新增设置形成新分区，旧 workspace 不迁移/删除。派生区使用短路径并复验 Mono Windows 路径预算。
3. 对最终确认的 raw Bundle SHA/length、context、策略/KeyId 匹配且完整认证可用的旧派生文件，逐文件复验后复制/复用确切密文；其余文件以新 salt/prefix 加密。新输出 CreateNew，完成前不可复用，记录只含输入/输出身份而不含 key。原有未冻结回调仍可阻止 native skip；“native 安全重建后 raw SHA 相同可复用密文”不等于绕过回调门禁。
4. `ResourceBuildResult` 保留 raw workspace/文件/ReadBack 事实，另提供不可混淆的 delivery 文件记录与 owned 路径。`ResourceCatalogBuilder.LockWorkspace/CreateLocked` 明确复验 raw 与交付快照，只用最终 delivery length/SHA 封 Catalog。不能事后修改已封 Catalog 或把现有 `ResourceBuiltBundle.FileBytes` 静默从 raw 改成密文；报告分列 RawBundleBytes/DeliveryBytes/复用与加密成本，非 native memory。
5. Publisher 复制确切交付快照到原私有 stage；完整 SHA/闭包不变，先逐段全量认证并与本次 raw 构建记录交叉核对 length/SHA，再通过同一解码内核为**每个 Bundle（含 root manifest）创建独立输入实例**，沿 LoadFromStream 保留原类型/依赖/UnityHash/内容 native 校验。多个同时存活 Bundle 不能共用一个 Stream/Position；全量校验也不复用 native 正在访问的游标。**不能只验 header/SHA、只引用 raw ReadBack 或跳过此门禁。** 各输入在各自 native Unload 后释放，不生成明文 stage 文件；失败不切发布 index。若 G0 迫使改用明确后端，另按其批准的明文边界收尾。
6. 随后沿原 Complete、owned journal、不可变 digest 目录、index 提交、恢复、签发或 C02 部署。相同完整交付快照重复发布保持同一 digest；同源重新随机加密则是新 BuildId/digest，不声称字节可复现。派生缓存丢失只会失去复用并生成新版本，不影响旧发布真实性；不得扫描“最新”或用 source SHA 覆盖另一份密文。远端同 Sequence 不同 digest 仍冲突，必须新序号签发。

因此加密发生在**Catalog 封口之前的正式构建交付阶段**，不在下载后覆盖旧文件，也不在 Publisher 已确定旧 digest 后追加变换。原 Builder/Publisher 两者职责和单一正式构建入口保留；新增派生缓存不是 Runtime active/LKG 的权威。

#### 28.10.7 加载路线、内存与生命周期

| 路线 | 当前取舍 |
| --- | --- |
| 整包解密 byte[] + LoadFromMemory(Async) | 不作为默认；密文/明文数组、native 复制/解压与并发叠加，异步 native API 也不自动异步解密。小夹具的成功不能证明生产大包可用 |
| 整文件 GCM 顺序流直接交 LoadFromStream | 不成立：普通解密流不可 Seek，且 DoFinal 前输出尚未完整认证；不能通过吞读异常或将坏数据返回 EOF 来掩盖认证失败 |
| **认证分块 + LoadFromStream(Async)，优先验证** | Tink 有成熟可随机访问格式和官方 Java Seekable 实现；缺官方 C# 不是不可行证明。自行维护范围限于该格式的 C# 适配与 Unity Stream 合同，原语用成熟库；难度、性能与待验证见 28.10.13。不默认移植整个 Tink keyset/KMS 平台 |
| 整文件 AEAD 完成认证后私有临时文件，保留原稿对照 | 最终 tag 前所有明文只在隔离临时状态，完整认证成功后才交 LoadFromFile(Async)；冷加载必须全量解密并写 F 字节，持有期占 F 明文磁盘、崩溃可残留。native 读路径最简单，但不符合优先目标；未再次批准不得默认使用或自动 fallback，不预先交付第二套文件协议 |
| 有限整包内存，次级备选 | 只有明确每包上限、所有 live 输入/在途的总预算与 native 峰值可容纳时才可选；可复用同一分块格式顺序验证到受限内存，完成后 LoadFromMemory 或只读 MemoryStream。不能拿“超限转 byte[]”作为流失败的兜底 |
| 受限私有临时明文，末级备选 | 只有具体流/native/性能障碍已复现，且内存预算也不适用，再由用户明确接受明文边界。可顺序认证同一格式再交 LoadFromFile，不必因此新增另一个整文件密码协议；代价与责任见 28.10.8 |

优先加载顺序：`OpenVerified` 保持密文只读句柄并按既有合同全文件 SHA 复验 -> 有界 header/context/长度检查 -> 主线程取得指定 key 副本并预留固定缓冲 -> 派生文件 key、认证首/末段 -> 主线程调用 LoadFromStream/LoadFromStreamAsync（crc=0，先用 32KiB managedReadBufferSize）-> Unity 的同步 Read 按逻辑位置取得已认证分块 -> 原 FinishBundle/资产路径校验并交割 lease。BC 在 DoFinal 前可能写入明文，必须先写内部隔离段缓冲；只有该段 DoFinal 成功后才可复制给 Unity，失败清零/锁存错误，绝不返回未认证字节。

与前稿整文件方案的保证差异须明示：发布与 Prepare/激活边界仍全量认证；随机读取本身只保证**每次交出的分块已认证**，不是凭首段就宣称所有中间块已验。已验证签名/Catalog SHA 仍约束完整交付文件；初始内置快照无 Prepare 时也不跳过原 SHA 复验。不得为了宣称“按需 IO”删除 OpenVerified 的全文件 SHA 门禁。

- 同步冷加载包含 SHA、首末认证与被 native 实际访问分块的成本，会阻塞调用线程，推荐大包用异步。异步在原 Bundle FIFO/4 槽内分可取消纯 IO/预验阶段与不可取消 native 阶段；后台阶段不调 Unity，回主线程后复验 session/owner。LoadFromStreamAsync 不代表 Unity 调 ReadAsync；Read/Seek/Length 可能来自任意 native 线程，同步完成，不能等主线程取 key/分配预算或持有等待主线程释放的锁。不能为每个 asset consumer 重复解密或另建加载器。
- 解密中的 producer 也必须计入 running/in-flight/draining。现 `CollectBundle` 只用 `Native!=null` 阻止收集，不能照抄成 worker 一开始就可释放；必须加入明确内容生产状态/cleanup owner，并覆盖同步在途返回 InProgress、共享等待、全取消、退出和迟到结果。零消费者时可取消尚未交 native 的私有 IO，native 已开始则继续观察，不关文件抢占 native。
- `BundleRecord` 持一个内部 `BundleInput` 所有权对象，聚合密文句柄、可 Seek 认证流、文件 key/段缓冲、sticky fault 与预算预留；不是新的 Bundle 缓存。每条流独占全局逻辑 Position，Read/Seek/Dispose 序列化，不能共用校验流的游标。Bundle/依赖按原 snapshot+BundleId 共享，确认 Unload(true) 完成后才清输入/key/缓冲/预算，最后释放依赖边；native 未知或 Unload 异常不得盲目关流。现 CollectBundle 先标记 Released 再 Unload，新输入不能照抄这个顺序而在异常时丢失责任，须留在同一图的 cleanup owner/Held/InFlight/draining 中直到可确认收尾。native 已卸载而输入 Dispose/删除报错也要保留可诊断的自有清理责任，不跳过后续归还或假装成功；不建立第二清理权威。失败、迟到与 EndSession 同样受保护，异常细节先经 G0 验证。
- Prefab 的 active/idle/destroy-pending、Atlas 的 lease/共享 Texture、Audio 的 clip/播放/尾轨/缓存、Scene 从 native 成功到真实 sceneUnloaded 的 pin，全部沿现有图延长输入寿命。不能因 AssetBundleCreateRequest 完成、AssetLease.Dispose 或 SceneLease.Dispose 就提前释放流/段缓存，或删除备选临时文件。Asset/子资产/批量和 SceneBundle 预加载均用这一内核；Resources、EditorSimulation、直接图片来源不经过加密。
- 延迟 fault 的阻断必须覆盖 **Ready 缓存命中**，不只检查 StartBundle/FinishBundle：现 `ResourceSystem.cs:214-230` 可直接发新 AssetLease，绕过 Source/Bundle producer。未来须在主线程结果交割前，对新增 Acquire/批量/Group、实例 idle 复用与 Scene 入口检查所依赖图的已观测故障；native 完成回调同样复核。worker 只锁存首个 fault 并发出有界通知，不调 Unity；主线程不能只等下一次冷加载才看见故障。已交割 Lease 不倒改终态、不强行销毁活跃对象或关在途流，继续保留 cleanup owner。用户已选 F1 依赖闭包隔离，native 是否能安全承受错误仍由 G0 决定。
- 主线程推进复用 `Instances/ResourceSystem.Instances.cs:159-187` 的 TickInstanceCleanup/InstallInstanceCleanup PlayerLoop hook；RemoteRequest 也已复用此 hook，但加密不借用要求 ArtifactStore/Ready 的网络 job 协议，不放开 Player 远端 gate。仅 Bundle/只读内置、无 Instance/Scene/remote 的配置也须按需安装 hook。内容预验、首个 fault 通知与初始化收尾仍归原 ResourceSystem/BundleRecord；CheckDrain 必须覆盖这些在途责任，通知固定回创建输入的主体实例，不经 Current 回填新会话。无新 MonoBehaviour、全局轮询服务或第二状态权威。
- LZ4 推荐作交付默认，保留 Uncompressed/LZMA 既有选择。加密在压缩后执行；LZMA 即使解密使用有界缓冲，Unity 仍可能整包解压到 native 内存。Streaming Audio 与 Scene 的延迟读要求真实 Player 门禁，不能靠 TextAsset 夹具或文档外推。

#### 28.10.8 仅在备选获批时的临时明文边界

优先分块流不主动生成运行期明文文件，也不需要临时目录清理器；仍不能承诺操作系统 swap/crash dump 或 Unity 内部行为绝无明文。以下仅约束将来证明有必要并另获批准的临时后端，不是当前默认路径或已授予的落盘许可。

- 只有应用显式提供的绝对私有 `ScratchRoot` 才能创建运行期明文，示意为应用自己的 temporaryCachePath 子根；不能放 Assets/StreamingAssets、发布版本、Store versions/parts、下载目录或系统共享可预测文件名中。只读内置模式不因此强制创建 ArtifactStore，只有**该备选启用并获批**才产生相应临时写入；默认分块流与 None 均不要求 scratch。
- 新建唯一 session 子目录，使用严格 owner/相对文件登记及排他 session 锁；CreateNew，拒绝未知已有目录、额外文件、孤立控制记录与 symlink/junction。目录使用应用私有访问边界；文件写入时独占，native 使用期保持只读句柄与 session 锁。不能把“private 路径”或 FileShare 说成对同账户/管理员的安全隔离，不修改系统信任/全局 ACL。
- 临时空间预算覆盖**所有已驻留图和在途预留**的明文长度，不只是并发 4 个解密任务；与下载所需密文/stage 空间分开估计，按预期 plaintext 上限和可用磁盘预检。不足明确失败，不驱逐 live owner、不回退整包内存、不偷偷删版本。
- 未认证临时文件在失败/取消时释放；已认证输入保留至真正 native 卸载。正常关闭只清自己的临时内容；崩溃后只能在下次显式初始化/清理边界核对本应用 owner、锁已释放、精确登记闭包和链接后回收。不是按 PID/mtime 或文件扩展名猜可删，未知/损坏归属拒绝自动清理。删除不保证 SSD/文件系统物理擦除，异常退出到下次清理前可能留明文；DeleteOnClose 即使未来使用也不能替代该责任。
- 复用现有 owned 清理的路径/归属/链接/复验原则与错误报告，不建立第二套版本索引、active 或恢复 journal。scratch 的 owner 仅证明可丢弃临时文件归属，不决定版本选择。E05 仍报告 Store 中实际密文字节与原保护原因；独立 scratch 不伪装成 Store 条目或可释放版本空间，当前 session 的 scratch 量/清理错误由原 Resource diagnostics 增量展示并明确“不包含在 Store 统计”。不为展示引入新的全盘扫描器或后台 GC。

#### 28.10.9 更新、恢复与内置部署

| 边界 | 推荐合同 |
| --- | --- |
| Check / CheckRemote | 保持只读和原签名/兼容验证；远端 Catalog 未下载 Bundle 头时，不宣称已经知道所有 KeyId 或解密可用。Check 可返回可传输计划，不等于可激活；不为了取 key 建目录、下载或改指针 |
| 下载 / Range | `.part` offset、期望 length/SHA、强 ETag、If-Range 和 Content-Range 全部针对**固定的密文交付文件**。CDN 不得按请求重新随机加密/转码；任意 Range 不等于 GCM 的独立认证块。完整文件 length/SHA 通过后才解析/解密；取消留下的 partial 仍是密文 |
| Prepare | 复用 Install/InstallRemote/复用文件路径，所有 To 目标在 `RecordPrepared` 前统一通过内容头/context/key、全部分块 tag 的有界顺序验证，并将解密输出与已认证头的 PlainLength/PlainSha256 自洽核对；后者不是 v1 另有的独立明文摘要。明文输出丢弃，不为 prepared 保留解密副本。包括已安装、复用、未变化项目与恢复旧 prepared，不能只验证新下载文件；失败不新增“可激活成功”结果、不切 active/LKG，不删除原有效版本 |
| Activate / Rollback | 在现有唯一 commit journal 写入**之前**，重新确认完整 To 闭包的内容认证和所需 key，Rollback 一样。现 `ResourceSystem.Updates.cs:90-96` 只有类型校验，须插入可取消/有界预验；耗时工作在可恢复前置阶段完成，最后主线程复验 from/epoch/Busy 与目标文件稳定性后进入原提交段。所有目标确认之前不卸掉仍有效的旧 native 图、不切指针；不持 Store 临界提交锁做外部 key 获取 |
| 重开 / 恢复 | Store 的字节/签名/journal 恢复不依赖内容 key，仍按原 phase/from/to 收敛；key 缺失不被 IsFileFailure 当作文件坏了从而隐式切到另一版本。Mount 选出既定完整闭包后，显式启动检查 key/编码；错误 key 在完整认证预验/首次 native 前明确失败。磁盘恢复成功不等于内容可解密，不新增“decryption-ready”持久标志 |
| C02 | 原 Preflight/Deploy/Recover、payload~ 外壳及 .meta 隔离按确切密文字节复制/复验；仍可在没有生产 key 的部署机上搬运已由 Publisher 验证的发布，不产生运行期明文。KeyId 能被读取不等于部署工具有 key。密钥供给是应用装配责任，Deploy 不注入 key、不初始化 ResourceSystem、不切 active/LKG |
| E05 / 清理 | Query 不请求 key、不解密、不写 scratch；active/LKG/prepared/held/in-flight 保护集合不变。内容生产未完成也计入 Held/InFlight；旧 query 不授予删除。原 CleanupVersionsAsync 继续是版本删除入口 |

Prepare 的“完整内容认证通过”只对当时 key/字节成立，不能写入一个永远有效的可解密缓存标记。实施时应在本地/远端共同的 `RecordPrepared` 前置校验点接入，不能只修改 DownloadPart.Verify；Commit 前再验覆盖跨进程/配置重开。认证失败保留已验证密文以便更正配置后显式重试，不能把错误密钥一概当网络错误自动重下。磁盘文件在事务保护下保持稳定，同一次激活的提交收尾阶段不再取 key 或开始新的可失败内容生产；后续业务加载仍须重新认证。已被外部任意恶意修改或应用主动撤销所有 key 的可用性不作绝对保证。

启动至少从已选闭包的有界头取得 key 需求并验证 provider/缓冲预算，缺 key 明确失败而不偷偷选择明文。Publisher 与 Prepare/激活边界逐段**全量 AEAD** 验证；每次 native 入口保留**完整密文 SHA**复验、首/末认证，后续每个访问段先验 tag 再交字节，不把“按需”写成全量 AEAD 已完成。初始内置快照没有 Prepare 时也适用这组入口保证；仅 Start 成功不表示全部资源已解密/加载。临时配置只在另获批准后才检查，历史 active/LKG/内置选择规则不因内容 key 错误新增自动 fallback。

#### 28.10.10 开发者正常流程与错误定位

1. 在自有 ResourceProject 的现有 Inspector 配置经 G0 固定的 `Aes256GcmHkdfSegmentedV1 + KeyId` 并保存；内容 key 由安全随机生成后放工程外 secret 来源。只增加必要模式/KeyId/来源状态提示，不做整体 Workbench 布局改造，不放密钥输入框或打印 key。
2. 本次 Editor/CI 进程明确提供 build key provider；运行原 Preflight -> Build/BuildAndPublish。BuildOnly 返回 raw 与加密交付阶段的分项事实，只有已验证 delivery 才能封 Catalog/正式发布；缺 key/加密失败不得报告“明文发布成功”。None 完整保留原流程。
3. 对成功 Release 沿原签发/导出或 C02 确切 digest 的部署操作。重复使用确切 Release 不重新加密；新密文使用新 digest，远端新发布签发更高 Sequence 并投放 E04 客户端专用渠道。原游戏/原 Package/旧发布不动。
4. 应用在自己的装配代码取得离线或外部 keyring，构造带明确缓冲预算的 decryption options，再创建同一 ResourceConfiguration/ResourceStartup；需要更新才传 cacheRoot，分块流不要求 scratchRoot。只有另获批准的临时后端才配置其私有目录。启动不读取旧 XFAB secret/settings，不根据加密失败切 Simulation。
5. 正常使用 Acquire/Prefab/Atlas/Audio/Scene/Scope 和原 Prepare/Activate/Rollback；退出先结算业务，再 StopAsync。遇 Busy 仍归还真正 owner，不强制清全局 Bundle、实例或声音。

建议为错误增加窄的可区分原因 `ContentKeyUnavailable`、`ContentAuthenticationFailed`、`ContentFormatUnsupported`、`ContentBufferUnavailable`（最终命名按现 ResourceError 风格）；仅临时后端获批后才增加其 scratch 错误。同时保留源/dependency/IO cause。日志仅记录阶段、ProjectId、CatalogDigest、逻辑 BundleId、KeyId 和错误类别，不含 key、Base64 secret、明文内容或敏感 provider 异常原文。格式损坏、Catalog SHA 不符与 tag 不符分别定位；tag 错只说明 key/数据/context 不匹配，不能断言一定是网络损坏或一定密码错误。备选 scratch 删除失败明确仍有残留；key 错误不无限重试、不用 null 成功或卡住共享依赖等待者。

#### 28.10.11 实际影响范围与实施顺序

以下仅是未来获批后的实施清单，不是本轮代码授权；新增类型名为推荐，不能把接口已存在当作实现前提。

| 小批次 | 受影响位置 / 可复用机制 | 验收门禁 |
| --- | --- | --- |
| **G0 阻塞可行性门禁** | 自有隔离载体，匹配 `2022.3.62f3c1` 和 Windows64/Mono；非生产 key/自有夹具。先做直通 FileStream 的 native 身份/延迟故障探针，再做官方 Tink 格式互操作、BC 实际 profile/RNG/GC/吞吐与认证流 | 最小对照与 go/no-go 见 28.10.13；实际 Player 的同步/异步、真实依赖/Atlas/Audio/Scene 与退出收尾，不以 Editor/编译代替。工具不可执行或数据不足属于待验证，不是流方案不可行证明 |
| **G1 内容编码与密钥** | 新 `R/Content/ResourceBundleEnvelope.cs`、`ResourceBundleDecryptStream.cs`、`IResourceContentKeyProvider.cs`、`ResourceDecryptionOptions.cs`、内部 BundleInput；批准的 BC DLL及必要移植许可/版本记录与精确 asmdef 引用 | 官方跨语言 vectors、严格头上限/首末段边界/溢出/EOF/额外字节/错 context、跨文件/换块、每块 DoFinal 前不返回其字节；buffer 有界、任意线程/共享 Position/Dispose/fault、provider copy与派生 key 生命周期；只有备选获批才补其 scratch/整包内存门禁 |
| **G2 正式构建发布** | `E/Data/ResourceProject.cs`、`Build/ResourceBundleBuilder.cs`、`ResourceBuildWorkspace.cs`、`ResourceCatalogBuilder.cs`、`Publishing/ResourcePublisher.cs`、原 EditorOperations/Project Inspector；复用 Atlas/后验/锁/journal/native ReadBack | 先建立缺能力失败测试再实现；None 旧路径不变；raw workspace 未被密文污染；加密缺 key 不发布；native 重建后密文可安全复用，丢失缓存重新随机化；重复发布、旋转、取消/故障保旧版，Publisher 每包独立输入做 stage native 校验；签发检查加密发布只投放已具 E04 能力的明确渠道 |
| **G3 统一加载与更新** | `R/ResourceConfiguration.cs`、`ResourceSystem.cs`、`Loading/ResourceSystem.Bundles.cs`、`Loading/ResourceSystem.Batches.cs`、`Sources/AssetBundleSource.cs`、`Scenes/ResourceSystem.Scenes.cs`、`Instances/ResourceSystem.Instances.cs`（复用 hook/缓存入口）、`Updates/ArtifactStore*.cs`、`Updates/ResourceSystem.Updates.cs`、`Updates/ResourceSystem.Remote.cs`、原 diagnostics；C05 透传配置，consumer 不增加公开解密 API | 共 producer、取消/退出/draining/迟到与真实卸载；Ready/idle 命中服从 F1，无关图可用、旧 owner 不强销毁；F2 共享不重复计费、取消归还自有增量；F3 探针成功前不 Ready，不创建 native、join一次/全None无额外密码处理，退出待清理；纯Bundle无其他子系统也能回传fault；Prepare/激活/回滚失败保持 active/LKG 与序号，恢复不依赖key、不偷偷fallback |
| **G4 交付与定向回归** | 复用 `ResourceReleaseFixture`、`ResourceBundleRuntimeTests`、`ResourceUpdateTests`、C02/E05、`FrameworkAssemblyIndependenceTests`、`ResourceStartupCompilationTests`；后续增量 Resource.md/Sample 非生产用法 | 远端密文 Range/ETag 续传与完整验证；旧 v1 发布不重建即可读，旧客户端不误入加密渠道；key 装配/轮换/明文 LKG 冒烟；C02 payload~ 字节和导入不变、部署机无 key；E05 无 key只读零写；新依赖准确源码/二进制闭包与导出许可。只跑新增及直接受影响用例，不重跑 31 项或历史全量 |

实施每个小批次使用针对新增行为的 RED -> 最小实现 -> 实际定向验证；已有历史用例只选择受影响路径。不把白名单合法增加依赖变成放松断言，不把认证失败改成跳过。8.10 总体 Windows Player/首次启动/持久重启与远端 gate 仍单独授权；G0/E04 的局部 Player 验证不能冒充它们已经完成，当前 Windows Player 远端仍 Unsupported。

性能按风险取证：相同 raw 内容/压缩模式、磁盘/硬件，比较文件直载、明文流与认证流的冷加载、共享热加载、Prepare 认证和轮换；记录分项耗时、Read/Seek轨迹、hit/miss/重复解密、GC分配/停顿、主线程峰值帧、托管/native/长期持有峰值，条件备选另计 scratch。包含多包并发和长 Streaming Audio/Scene，不以微小 Text 包或现代.NET硬件加速数字推断 Mono。分块模型与门禁见 28.10.13；首版不持久缓存明文，不为减少 IO 去掉认证。

#### 28.10.12 本轮证据、重大待裁定与停止点

| 证据层级 | 本轮实际结果 / 仍未知 |
| --- | --- |
| 现场与源码 | 已核对分支/HEAD/dirty，定向研究 XFAB 加密、YL 构建/目录/签名/Range/Store/部署/查询/加载与消费者责任；没有重审 31 项或重跑旧验收 |
| 官方/原始资料 | 已核对 Unity 2022.3 文件/流加载约束、本机保护 API XML、BC 2.7.0 netstandard2.0/GCM/HKDF 源码、NIST/RFC；追加核对 Tink 分块规范/官方 Seekable 实现与互操作/截断测试、C# 参照库及 Unity 2022.3.58 race 修复。原始源码支持候选方向，不等于本工程 Player 已可用 |
| 前置 Plan 阶段限制（历史） | 当时只读 `unity-mcp-cli ... editor-application-get-state` 被工具权限拒绝，未改权限/配置或绕过。随后新的明确 G0 授权允许自有隔离工程/Player，执行证据见 28.10.15；没有对原工程启动第二个 Unity |
| 当前阻塞与未知 | 原型的 Stream native 名称和普通载入已有实证；Scene 延迟 tag 失败已导致 native 崩溃，不能安全闭合，因此 G0 整体未通过。外部重复加载屏障、认证取消/退出/清理异常、实际重复 Player 性能与预算仍未完成；scratch/整包内存均未获实施许可 |

当前重大边界：优先完成分块认证流的可行性证明，不因实现更简单或当前工具不能执行就退回临时明文；BC 依赖及所需小范围格式移植的产品维护仍待最终裁定。项目级范围、保留 Catalog v1、显式 key provider 沿用推荐，不建设全能策略/KMS 平台。只有 G0 证实原语后端吞吐/GC、native stream 或预算存在具体阻碍后，才把有界内存/受限临时明文提交用户比较，未获得的明文落盘许可不能视为同意。

**已裁定的实验边界**：2026-10-16 用户明确选择“允许受控构建”，同意最小隔离实验同时验证 BC 2.7 官方 netstandard2.0 包，以及同一官方源码仅增加 netstandard2.1 目标的受控构建。此选择不等于批准产品依赖/格式移植、不授权修改密码核心或主工程，不是明文临时文件许可。

**本次执行授权仅限 G0a～G0e**，使用 `C:\Users\Kano\AppData\Local\Temp\kilo\e04-g0-<unique>` 自有隔离载体；先验证已批准父目录和目标归属，拒绝复用未知目录。允许非生产 vectors/探针/分块原型/依赖构建与日志，不更改主工程 Assets/Packages/ProjectSettings、原 Package/游戏/旧产物/配置，不做 Git clone 或任何 Git 写操作，不对同一工程再启动 Unity。匹配本机 Unity 2022.3.62f3c1、Windows64/Mono、.NET Standard 2.1；只用自己生成的夹具/测试 key，日志不输出 key。记录源码 revision、实际构建 checksum、Player 参数与原始结果路径，结果增量回写本节，不另建重复总方案。G0 完成或遇真实阻塞后必须报告并停止，不能顺势进入 G1～G4。

G0证据及关键边界获确认后，才可在新的明确实现授权中按G1～G4推进。**首批/SD1因Scene故障门禁停止的历史不变；C1按限定豁免完成其他探针后提交条件性实施审核建议，详见28.10.17；正式产品仍未实施**，不宣称加密已可用或阶段8完成。本任务主工程增量仅本Plan，实验源码/依赖/产物留在自有临时载体；不改Catalog/旧资产、不推进其他功能/Workbench/8.10，不暂存、Commit、Push、切分支、合并或创建里程碑。

#### 28.10.13 追加研究：认证分块 + LoadFromStreamAsync

**首批结论（历史保留）：RAW互操作及普通native载入已有实证，但Scene延迟故障造成Windows64/Mono native崩溃，当时G0整体no-go。** 这不证明所有分块路线不可行。后续仅用户明确豁免该项的本轮受控失败要求，C1才继续其余G0并取得28.10.17的实际成本；不是修复或无条件通过。集成难度中高，不判定生产可用，未批准内存/临时后端。此前“缺官方C# Tink，故优先临时文件”的依据不足，仍不成立。

**现成成果与最小移植边界**

| 候选与原始证据 | 可复用与不足 |
| --- | --- |
| [Tink AES-GCM-HKDF 格式](https://developers.google.com/tink/streaming-aead/aes_gcm_hkdf_streaming)、官方 Java [AesGcmHkdfStreaming](https://github.com/tink-crypto/tink-java/blob/main/src/main/java/com/google/crypto/tink/subtle/AesGcmHkdfStreaming.java) / [SeekableChannel](https://github.com/tink-crypto/tink-java/blob/main/src/main/java/com/google/crypto/tink/subtle/StreamingAeadSeekableDecryptingChannel.java) / [边界测试](https://github.com/tink-crypto/tink-java/blob/main/src/test/java/com/google/crypto/tink/subtle/AesGcmHkdfStreamingTest.java) | 格式明确允许随机访问，有位置映射、首/末段、换块/截断测试；优先按此做小范围 C# framing/Stream 适配，原语仍用成熟库。不是自创 nonce/索引协议，也不冒称移植代码继承了 Tink 的安全审计；需独立评审、跨官方实现互操作，实施前 pin 源 revision，复制/改写代码保留 Apache-2.0 等适用声明 |
| [TinkStreamingAead 1.0.1](https://github.com/salehalakhras/TinkStreamingAead)，`src/TinkStreamingAead/TinkStreamingAead.csproj` / `Aes256GcmHkdf1MB.cs` | netstandard2.0 分支用旧 Portable.BouncyCastle 1.9.0，解密流 CanSeek=false；有 Tink Python 互操作/完整末段修正，可参考测试，不能直接包成随机流。当前规模/维护不足以作为默认安全背书；依赖 BC 本身不是排除理由 |
| [AgeSharp](https://github.com/pscheid92/AgeSharp) 的 AgeRandomAccess 与其 README 链接的 AgeSharpNetStandard 移植 | 可 Seek、打开即认证末块的 C# 参照确实存在；主线 net10，不适配 Unity；社区 netstandard2.0 移植包含 BC/完整 age 收件人容器，上游明确不维护/不审计该移植。不是技术上绝对不可引用，但引入整个 age 与跟进安全修复的负担高于本需求，不作为首选 |

本轮未找到成熟、可维护、直接满足目标 TFM + Seek 合同的即插即用 C# 依赖；这是定向 GitHub/NuGet 检索结论，不是“全网不存在”。不选已归档 Miscreant 或只支持新 .NET 的库，不为了零依赖手写 GCM、GHASH 或临时拼装 CTR/HMAC。libsodium secretstream 仍是顺序链，不能靠增加 Seek 方法变成认证随机流。

适配器只负责有界外头、Tink framing、offset 映射、认证段缓存和 Stream/错误/所有权；不引入 Tink keyset、KMS 或远端随机下载。设 ciphertext 段大小 S，P=S-16，内头 H=40：首段 plaintext 容量 P-H，后续为 P；非空读取的段号为 `floor((position+H)/P)`，与官方实现交叉验证首段边界。总段数 `N=ceil((F+H)/P)`，内密文长度 `F+H+16N`（F 是原压缩 Bundle 长度）；完整末段、空输入、单字节跨段和 `2^32` 上限都必须有向量。打开时首/末认证确定 key/context/长度，只有真实且已认证的 EOF 可短读/返回 0，错误不能伪装 EOF。

| 实现单元 | 难度 / 风险判断（非工期） |
| --- | --- |
| 成熟格式的 C# framing/互操作 | 中：格式与位置算法已有规范/官方实现，无需自建索引，但首末段、整数界限和移植独立评审不可省 |
| Seekable Stream/缓冲/线程 | 中高：全局 Position、跨段短读、重复读取、native 线程锁、缓冲预算与 Dispose 顺序一起满足，不是覆写 CanSeek 一个属性 |
| native 延迟故障与外部身份屏障 | 高风险：本次 Scene 故障已出现 native 崩溃，门禁失败；普通名称保持与 TextAsset/Audio 故障受控退出不能代替全部责任验证 |
| YL 融合 | 中：原 BundleRecord.Input/producer/lease、发布校验和 Prepare/Commit 可复用，新增内部 BundleInput 聚合所有权；fault/内容生产状态须覆盖每个出口，不改各 consumer API、不建立第二加载系统 |

**性能：以下是静态成本模型，不是 benchmark**

| ciphertext 段 S | 长文件 tag 占 plaintext 比例 `16/(S-16)` | 一密文工作段 + 两已认证明文缓存约 `3S` / live Bundle |
| --- | --- | --- |
| 4KiB | 0.3922% | 12KiB |
| **64KiB，优先实验** | **0.02442%** | **192KiB** |
| 1MiB | 0.001526% | 3MiB |

- 占比仅是包体膨胀，**不是加载变慢百分比**；另有每文件 40B 内头与最多 4108B 外封装。64KiB 是 Tink 参数允许的一档，不是杜撰的算法变体，也不是未经实验已冻结的产品参数。
- 冷 miss 必须读/解密/验完整段：32KiB 小读在 64KiB 段下约 2～4 倍解密字节，在 1MiB 段下约 32～64 倍（分别按一段/跨两段全 miss 近似，首尾部分段更小）。连续读取且缓存命中时，每段通常只解密一次，不是每个 32KiB 回调都重新解密；随机跳转/多热点抖动会反复 miss，因此不能仅测顺序吞吐。
- 开段 miss 的成本约为“段 IO + S/实测 AEAD 吞吐 + 单段 Init/tag + 拷贝/锁/managed-native bridge”；同一流串行、不同 Bundle 可并行，但读取可能发生在 audio/native streaming 线程，不可依赖主线程执行密码任务。不能假设 Async 会消除 CPU、GC 或音频 deadline 风险。
- cache 内存随 **所有 live Bundle 图** 而非创建并发 4 增长。两缓存时 100 个 64KiB 流约 18.75MiB，加默认 Unity bridge 32KiB/流约 3.125MiB，尚不含 key/GHASH、对象、资产/native/OS page cache；1MiB 段仅上述三个工作段即约 300MiB。预算须在 native 交接前预留；一块明文缓存约 2S，是否需要第二块由读轨迹决定，不建设无界 LRU。
- 当前 `OpenVerified` 已逐文件 SHA 全扫，切流也保留。因此冷加载仍有 O(F) 密文读取，不能宣传“仅下载/读取访问到的少量块”；优势是按需解密、有界明文缓存、无主动明文落盘。发布、Prepare、激活的全闭包认证仍有 O(F) AEAD 成本，不能用未验证持久 ready 标志省掉。
- LZ4/Uncompressed 适合可寻址视图；LZMA 在 Unity 内全量解压，仍有 native 内存/CPU成本，不是分块算法不支持。CRC 保持既有 0，不为加密另外启用 native 全包 CRC；SHA、签名与分块认证各司其职。

**已确认的后端风险，不等于已测平台失败**

[BC 2.7 AesUtilities](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/AesUtilities.cs) 与 [GCMBlockCipher](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/modes/GCMBlockCipher.cs) 已定向核对：netstandard2.0 不包含 NETCOREAPP3_0 的 AES-NI/GHASH intrinsic 分支；其数组版 DecryptBlocks2 在 ProcessBytes 主循环每 32B 新建 byte[16]，约 **32,768 次小数组/MiB**，另有每段 Init/DoFinal/Reset 固定分配。该数据量正比项不能靠扩大段、外层复用缓冲或重复使用 cipher 消除；原整包/临时解密同样付一次该成本，随机反复 miss 会重复支付。具体 GC 字节/停顿和吞吐仍须 Mono 实测，不外推现代.NET数字。

因此**不将“直接引用官方 netstandard2.0 DLL”视作已经合格的后端**。G0 同时评估该基线与“同一 release-2.7.0 源码、仅增加 netstandard2.1 目标的受控构建”能否在项目 profile 工作：原源码 2.1 Span 分支使用 stackalloc，可避开按 16/32B 的堆分配；但仍是软件 AES/每段有固定分配。实际 metadata-only 构建与小范围 hybrid 尝试均受阻，未产出 2.1 DLL，见 28.10.15；不能把静态 Span 分支当已验证优化。此为新的依赖构建变体，须记录来源/实际 checksum/许可/构建方式，不能冒称官方 NuGet asset，也不能直接塞入不兼容的 net6 DLL、补丁 Library 或手改密码核心。进一步后端研究须技术审核后另行授权，不能以此断言所有分块认证方案不可行。

**native 集成难点与明确未知**

1. Unity 文档支持可读/可 Seek 流，native 同步调用 Read/Seek/Length，可能来自任意 native 线程；每流独占 Position、线程安全且不可重入应用/Unity API。流的 header 必须被隔离成原 Bundle 的 position=0 视图，活到 Unload 后；沿原 Input 所有权接入，不按创建请求完成就清缓存/key。既有 PlayerLoop hook 可复用，须确保纯 Bundle 配置也安装，并覆盖初始化/退出/旧主体迟到通知，而不是假定已有通用 MonoBehaviour 驱动器。
2. 创建后资产/Scene/Streaming Audio 的回调错误**没有官方传播合同**。本次适配器虽已锁存认证 fault，Scene 路径仍在主线程取回操作结果之前 native 崩溃，未安全收尾；详见 28.10.15，不用其他成功样例抵消。要求不复制坏块、原 source completion/Scene/native 完成前检查 fault，已 Ready 后的 fault 经现驱动器回主线程诊断并阻止新 acquire，已有交割不能假装从未成功；不得返回 0/零填充假成功，也不能 Dispose 去强行 abort native。故障路径是 go/no-go 证据，不是只写 catch 就算解决。
3. `bundle.name == NativeName` 与 GetAllLoadedAssetBundles 的外部占用屏障必须原样验证。本次普通 Stream/native 名称实测保持 `g0_*`，但外部重复 native 载入屏障尚未独立覆盖。社区错误日志出现 `IO.Stream` 只能说明诊断 archive 名，**不能据此断言 Object.name 已改变**；不能删除校验使测试通过。
4. 官方 [2022.3.58f1 发行说明](https://unity.com/releases/editor/whats-new/2022.3.58f1) 已修 UUM-92381 LoadFromStream race；本工程 62f3c1 晚于它，不能把旧 bug 当当前不可行理由，也不能据版本号代替实测。旧 IssueTracker 的额外 buffered reader 报告在本轮主控重取时遇到迁移/502，保留为线索不引用其中“100～200KB/包”作为实测；预算先用文档默认 32KiB 并测实际 native 增量。

**最小验证次序与降级门槛（门禁要求，实际进度见 28.10.15）**

依赖顺序为 G0a → G0b → G0c → G0d；G0e 是横切写入观察，在 G0c/对应成本场景启动前部署并持续至收尾，不是加载全部结束后才扫目录。语义/故障轮与性能轮分开控制探针开销，不重复历史技术验收。

| 顺序 | 自有隔离实验 / 可判定产物 |
| --- | --- |
| G0a 先验 native | 同一原始 LZ4 Bundle 对照 LoadFromFileAsync 与直通 FileStream+LoadFromStreamAsync，检查 Object.name、Scene 标记、两个不同包/重复 native 载入；创建成功后再取不同资产/重复取得，记录确实发生的延迟 Read，再等真实 Unload 才关流。同步入口补一个代表样本；随后同合同查 Uncompressed，LZMA 单列解压/native 内存，不删除既有能力。G0 只证明适配/native 条件，不冒充尚未实现的 YL 加密集成 |
| G0b 格式/后端 | 官方实现与 C# 在固定非生产 key 下双向互操作；明文模型核对随机/跨段/重复读及 Position/Length/EOF。坏 tag、错 key、跨文件替换、重排、截断、首段/满末段/空输入和计数/尺寸溢出分层测试：交付层先验 length/SHA，独立 codec 测试不依赖外层 SHA，证明 AEAD 拒绝；不复制坏段、不伪 EOF。比较 BC 两构建的 Mono/GC/吞吐。另测当前 key 集的轻量认证、缺/错 key拒绝及未选历史key不阻塞，记录探针成本，不把原型称为已完成 C05 集成 |
| G0c 故障与责任探针 | 在已通过 G0b 的认证原型底层密文视图，于 native 创建完成后武装故障并触碰未缓存的资产/Audio/Scene 段；用直通流 Read 异常作对照，不改真实文件或绕过 FileShare。必须确认该次延迟 Read/失败被触发，无触发不能报通过。覆盖取消/退出、迟到、Unload/Dispose 异常 owner 保留，及纯 Bundle 无其他子系统的通知推进；超时/进程退出、无坏段交出、无丢失 native/输入责任是门禁，不能安全闭合则停下产品接入 |
| G0d 实际成本 | 文件直载/明文流/认证流三基线，同输入/压缩/页缓存条件，SHA 前置单列并一致；先 64KiB，再按轨迹对照块大小，不跑完整笛卡尔积。测冷创建、首次资产、Scene、持续 Streaming Audio、并发4/多驻留包的 P50/P95、最慢 Read、帧/欠载、GC与总 live buffer/native。默认预算用自有常规 Scene+UI/Atlas+Audio+Prefab 共存及切场景新旧图重叠验证，记录 live/预留峰值并留合理余量；不能只靠小 TextAsset，或让正常样例必须手动调高才能通过。语义轨迹存有界内存后写日志，性能轮关闭/采样轨迹，避免回调写盘污染；逻辑读写字节与物理磁盘 IO 分列 |
| G0e 无完整明文写出观察 | 加密运行使用独立的**只含密文 payload**目录，raw 构建夹具与明文直载对照留在另一目录，不让原型通过 raw 路径加载冒充解密。审查解密路径仅以只读方式开密文、无明文文件写 API；对自有运行输出、实际 persistentDataPath/temporaryCachePath 的受控范围做事件与前后文件/尺寸/SHA观测，区分 Unity/日志基线写入。长持有资产/Audio/Scene、错误/取消/退出均检查，不能只退出后检查“已清理”；记录覆盖范围、写字节和不确定性，事件溢出/权限缺口不能报“无写入通过”。没有合适系统跟踪工具不安装驱动/改全局配置；只在已观测范围内下结论，不承诺 OS分页/转储或未监测路径无明文 |

G0结果必须附机器/Unity/profile/backend/Bundle压缩与大小、样本数、原始日志及差值，先报告可重复成本，再与应用认可的冷加载/帧/内存预算比较。**首批因G0c崩溃未进入重复G0d；C1在限定豁免后取得的分阶段Player成本见28.10.17，仍无统一生产“损耗X%”或跨平台结论。** 辅助Mono顺序吞吐不替代Player/图/音频/预算验收。

若原语实现慢/GC重，先验证成熟低分配后端；若分块粒度或 cache miss 是主要原因，先用真实轨迹作一次有界参数对照；若 native 错误/身份不能安全闭合，或上述合理调整仍超实测预算，才评估：**受限整包内存 -> 受限临时明文**。内存后端必须同时限制单包、所有 live 明文/在途和 native 复制/解压峰值，必要时先重划自有 Bundle；临时后端须单独批准落盘/残留边界。三种后端尽量复用同一成熟分块交付格式与既有事务，不能在缺 key/认证失败时自动换后端，更不能降到明文/弱认证。

#### 28.10.14 本轮续核：参考边界、Stream 合同与裁定

**未改回临时文件**：AES-256 分段认证 + 有界可定位解密 Stream 的格式/普通加载可行依据已补实，但目标 Mono 当前原型的 Scene 故障安全门禁未通过，不能交付。前稿把“每次入口全量认证”与按需分段混写的地方已改正；正式 Runtime 仍未接入加密。已批准的 BC 受控构建只是实验许可，不是正式依赖批准，也不是回退落盘许可。

| 参考锚点（本轮实际读取） | 可采用的依据 / 不能推出的结论 |
| --- | --- |
| YooAsset **3.0.6**，tag commit `3b4cfb36cc2e81b9558ab74d65fee2cf2eac2804`；[IBundleDecryptor](https://github.com/tuyoogame/YooAsset/blob/3.0.6/Assets/YooAsset/Runtime/Interfaces/IBundleDecryptor.cs)、[AssetBundleHandle](https://github.com/tuyoogame/YooAsset/blob/3.0.6/Assets/YooAsset/Runtime/BundleHandle/Services/AssetBundleHandle/AssetBundleHandle.cs)、LoadLocalAssetBundleOperation 与 BundleEncryptionSample | IBundleStreamDecryptor 返回流与缓冲大小，native 走 LoadFromStream(Async)，成功后把流交给 Bundle handle，Unload(true) 后 Dispose。示例为 XOR/偏移/整内存，不是 AES/AEAD 分段实现；框架提供 Stream 扩展点不证明认证或 native 失败传播正确。官网链接的 yoo3 分支目录与 tag 有差异，以 tag/类方法锚定，不混用旧 IDecryptionServices 兼容层作为现 API |
| Tink [supported-key-types](https://developers.google.com/tink/supported-key-types) 与 AES-GCM-HKDF 规范；Java 实现参考快照 [`18b32f1c63f0520d05bed1ba8bc6bd401b50169d`](https://github.com/tink-crypto/tink-java/tree/18b32f1c63f0520d05bed1ba8bc6bd401b50169d) | 支持表列 Java/C++/Go/Python 等，无官方 C#；采用公开分段构造与测试，不依赖完整 Tink SDK/KMS，也不把 C# 移植称作已审计现成库。复制其代码保留 Apache-2.0/适用 NOTICE；快照不是产品依赖版本，G0 记录实际采用范围与互操作产物 |
| BC **2.7.0** tag 解引用 commit [`4007498b13582d90ee1eda5d9920c324428b98b3`](https://github.com/bcgit/bc-csharp/tree/4007498b13582d90ee1eda5d9920c324428b98b3)，已核 [MIT 许可](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/LICENSE.md) | 可维护成熟密码原语；官方资产的 netstandard2.0 GC 热点与 2.1 源码分支差别见 28.10.13。标签/commit 标识不是下载包校验或签名验证结果，实际 archive/DLL 的 checksum、构建命令、程序集/包体/运行数据仍由 G0 记录；不添加到主工程来试错 |

借鉴 YooAsset 只取“返回输入由加载者持有到卸载”的责任边界，不引入其 FileSystem、ResourceManager、FallbackDecryptor 或新接口平台；它的 Unload 后 Dispose 代码也不能替代 YL 对异常/取消/退出责任的验证。保留 XF 的项目级可选保护与便捷装配价值，不继承 DES、XOR、静默空 key 或泄密日志。

**Stream 合同必须形成可测断言，而非只声明 CanSeek**

| 情况 | 必须行为 |
| --- | --- |
| 逻辑视图 | position 0 是原始 Bundle 起点，Length 是认证后交叉核对的 plaintext 长度；外头/内头/tag 不对 Unity 暴露。Position/Seek 只改变逻辑位置；物理定位和解密按段索引计算，不依赖从文件开头一路解密的旧状态 |
| 正常 Read / EOF | 在未 fault 且结束边界已认证的正常状态，返回实际读取量并推进对应 Position；count=0、从末尾或越过末尾读返回 0，跨末尾只短读有效部分，不因 Unity 的越界探测抛错。已锁存认证/IO fault 后 Read 继续明确失败，不能用 Seek 到尾部、清缓存或下一次短读消除；Dispose 后按已释放状态拒绝访问。Seek 至非负末尾之外可定位，负位置、非法 origin/参数、checked 算术溢出按明确错误拒绝 |
| 跨段/重读/随机定位 | 支持任意合法位置；cache 命中重用已认证段，miss 整段验证再复制。坏段不向 caller buffer 复制，不能把已写内部缓冲或一段成功当整次请求都成功；若先复制了已认证前缀再遇坏段，明确失败并锁存 fault，不用正常短读/EOF掩盖它 |
| 文件/位置/结束边界 | 独立随机 salt 派生文件 key，context 绑定项目/逻辑身份/长度等；nonce 的位置计数拒绝重排，末段 bit 与长度/计数/完整交付 SHA 拒绝截断和拼接。与另一合法旧版本整体替换的授权/新鲜度仍由 Catalog/签名/Sequence/显式回滚决定，AEAD 不自称证明“最新” |
| 线程/状态 | 每个 live Bundle 一条独立流，Read/Seek/Length/Position 与 Dispose 协调；底层校验与 native 不共享游标。锁只保护自身状态，不宣称能修复 native 跨调用错误配对的 Seek/Read；不调 Unity/provider、不等主线程、不在回调写日志。native 是否并发/如何取错误必须由 G0 观察 |
| 取消/退出/释放 | 取消消费者不释放仍被共享/在途 native 使用的流；预验阶段可取消，native 开始后原 owner 观察到完成再收尾。Bundle Ready 不等于结束读取；AssetBundleRequest、Scene/Audio/Instance 的图持有结束且 native 确认卸载后才清文件 key/缓冲/流。G0 原型用最小输入 owner 验证，不另仿制整套 ResourceSystem；正式图集成仍待 G3 |

nonce 唯一性来自同一文件内不重复计数、不同新加密重新生成 salt/prefix，不能把随机性说成绝对无碰撞；不复制 RNG 状态到子进程，不改用时间/明文 SHA/固定 IV。AES-256 固定派生 key32/tag16/nonce12，单段满足 GCM 消息限额，总段数与 .NET/文件预算更小者约束整文件；所有尺寸/位置计算用受检整数，不能仅依赖最后 native 加载失败。

**前置只读阶段完成与未完成（历史）**：核对了上述官方资料/源码、当前 YL 的 OpenVerified、Start/Complete/CollectBundle、AssetBundleSource 和 EndSession/CheckDrain；定向独立复核修正了自洽摘要、每包独立输入、应用 keyring 归属和发布能力隔离门禁。该阶段未运行原型；后续 G0 实际结果统一见 28.10.15，不能把部分证据写成“已证明无明文文件”或“Windows64/Mono 已整体通过”。

**已裁定 F1：依赖闭包隔离。** 2026-10-16 用户明确选择：仅阻断“故障 Bundle，以及传递依赖它的 Bundle/资产入口”的新增资源、实例和 Scene 责任，包含 Ready/idle 缓存命中；无关 Bundle/项目继续可用，既有 Lease/在途 native 保留至安全收尾，active/LKG 指针不变。例：A→S、B→S，A 坏不牵连 S/B，S 坏才影响 A/B/S。故障判断仍来自原 BundleRecord/input，不新增持久 blacklist、全局停机或自动下载/换版本。该裁定不代表 G0 已通过，也不授权产品实现。

故障锁存归该次输入/图责任，不增加重置或恢复服务：旧故障图尚未收尾时不通过缓存发新责任；相关 owner 归还且 native/输入安全收集后，应用的新显式请求才可重开输入、重新走字节与认证门禁，仍使用明确选定版本。没有后台自动重试、清 fault 后复用旧流或隐式修复不可变文件；再次验证失败继续明确报错。

**已裁定 F2：可配置预算，明确失败后显式重试。** 用户选择推荐行为，同时要求默认值合理可用、不能是过低固定限制。启动新 native producer 前，对依赖闭包需要新增的加密输入整体检查/预留；共享节点不重复计费，预留归 BundleRecord/input，不归第一个等待者。计量使用“已分配 + 尚未兑现的预留”，兑现时转换而非重复累计；失败/取消只归还仍由本次独占的增量。同步/异步不足都报 ContentBufferUnavailable，应用归还自己持有的责任后显式重试；不无限等待、不挤出 live 输入、不转临时明文/整包数组、不建预算等待队列。

默认预算候选为 Windows64/Mono **128MiB**，与最终段大小/缓存数量一起经过 G0 后再固定，不宣称已实测达标。按 64KiB、三段工作缓冲估计仅 buffer 约可容纳 680 余个大段输入，但这只是算术上界，不等于常规游戏实测；小包的单输入缓冲占用可能更少，其他开销另计。上限可由应用启动配置合理调高/调低，拒绝非正值和溢出，不静默钳位到低值、也不在不足时自动突破上限。它限制我方可计量的解密工作/明文缓存及其预留，不是整个进程/Unity/native/OS缓存的内存硬上限；诊断分列配置上限、已用、未兑现预留、峰值与驻留输入数，并在失败时给出所需增量与相关持有原因。默认门禁必须覆盖常规工作集和切场景过渡峰值；若普通合法流程频繁触发预算不足，应调整默认/缓冲设计再评审，不把责任一律推给用户调参，不据“并发4”限制驻留计量。

**已裁定 F3：首次启动做轻量密钥认证。** 用户明确选择：在核心 InitializeAsync 的首次装配中验证当前选定 active/内置闭包必需的 KeyId/材料，ResourceStartup 仅共享该结果，不另做第二初始化。每个必需 `(ProjectId,KeyId)` 用已验证的代表性密文做首/末段认证（同一段去重，优先小文件）；所需 key 在操作内取得稳定副本，探针结束清 key/缓冲/句柄，成功前不置 Ready、不创建 native Bundle。同配置在途 join 共享一次结果，已 Ready 的同配置借用不重跑；全 None/无 Bundle 不做额外密码处理。只校验当前闭包，不以未选历史/LKG/prepared 的 key 缺失阻塞本次启动；回滚/激活时仍独立检查其 To 闭包。缺 key/认证失败明确初始化失败，不切版本/模拟，不泄密；取消/退出/迟到需保持内容作业收尾与 CheckDrain。轻量探针不做整个项目的 AEAD 扫描、不证明所有文件完整认证或 provider 将来永不失效，真实加载仍保留每输入完整密文 SHA、首末和逐段认证；探针/发现 key 需求的具体 IO 成本在 G0b/G0d 单列。

**其余后续裁定**：不重复询问已明确的“无完整运行时明文默认路线”或已批准的隔离受控构建；正式BC资产/受控构建与framing移植仍须实施审核。段大小与缓冲预算据真实轨迹、应用性能门限据实测再定，不新增全能配置；任何临时明文产品路线仍需另行批准。首批遇阻后，用户仅按28.10.17限定豁免恢复其余G0，现已据新证据提出有条件实施审核建议；不执行产品G1～G4，不推进Workbench/8.10，不做Git写操作。

#### 28.10.15 首批 G0 执行记录：Scene 故障门禁 no-go，停止待审核

**执行边界与证据入口**

- 2026-10-16 UTC，本次仅执行获准的 G0 自有实验；根目录为 `C:\Users\Kano\AppData\Local\Temp\kilo\e04-g0-20261016-1513-c7a9`，有 `owner.json`。下列实验相对路径均从此根解析。总索引为 `evidence/g0-summary.json`，密码分项为 `crypto/results/report.json`；没有新增第二份总体方案。
- 环境：Windows 11 `10.0.26200`，Ryzen 7 9700X / 16 logical processors，约 32453MiB RAM，RTX 4070 Ti SUPER；Unity `2022.3.62f3c1` / Windows64 Mono / .NET Standard 档，非 Development Player。辅助测量另用 Unity 自带 **x86 Mono 6.13.0**，不能混为 Win64 Player。
- 自有夹具每种压缩 70 个 Bundle，生成 LZ4 / Uncompressed / LZMA 三组；包含真实共享材质/Texture、两个各 4MiB TextAsset、Prefab、SpriteAtlas V2、40 秒 Streaming Vorbis Audio、2400 个 GameObject 的 Scene，以及 64 个各 256KiB payload 的驻留包。LZ4 原 Bundle 字节：asset `8,392,573`、audio `1,554,374`、scene `462,160`；全部 length/SHA 在 `native/raw/fixture.json`。无用户游戏内容或生产 key。
- 主工程 `fusion/8-resource` / HEAD `ddf154cf2c72cd0b403cdd1547eee8ae06963547`。`evidence/workspace-before.json` 与 `workspace-preplan.json` 对比，**158 个既有变动文件 SHA256 零变化、新状态路径为 0、暂存为空**；回写后复核见 `workspace-final.json`，本任务主工程增量仅本 Plan。未操作原工程 Unity PID `3936` 及其既有 worker；没有 Git 写操作、产品依赖或正式配置变更。

**产物与来源冻结**

| 项目 | 实际产物 / 身份 |
| --- | --- |
| BC 官方 netstandard2.0 | `crypto/official20/BouncyCastle.Cryptography.dll`；包版本 2.7.0，程序集版本 2.0.0.0；SHA256 `d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495`，匹配官方下载 CHECKSUMS；来源/下载失败标记均保留，未把超时 nupkg 当已下载资产 |
| 原始 BC 源码 | release-2.7.0 解引用 `4007498b13582d90ee1eda5d9920c324428b98b3`，2272 个原版 `.cs` 最终复核零字节变化，MIT 许可保留；本轮未修改密码核心 |
| 最终 RAW 原型 | `shared/E04Crypto.cs`，SHA256 `5e4242c820a6a70c2f9a3a194fcb77660f51c5323587f3e1ea6dc6da47c83ec6`；仅可信 segmentSize / firstSegmentOffset=0 / 调用方 expected AAD，不是 YLRENC01 envelope、provider、Catalog 或 ResourceSystem 实现；BC/Tink 许可保留 |
| 官方互操作参考 | Maven `com.google.crypto.tink:tink:1.24.0`，实际直接调用官方 `newEncryptingStream` / `newSeekableDecryptingChannel`；其 AesGcmHkdfStreaming 源与指定 `18b32f1c63f0520d05bed1ba8bc6bd401b50169d` 快照逐字节一致。已核官方 SHA1 sidecar 并记录本地 SHA256，未做独立 PGP 供应链认证，不宣称官方 C# SDK/审计背书 |
| 最终 Player | `native/players/official20-final/`；构建 `evidence/unity-build-20261016-161823.log` exit0。exe SHA256 `059c313352892718258a120b87bdc6fc7c23ce67181084abe14f86f204f5dddb`，Assembly-CSharp SHA256 `c37f09f2a43ab20ecc745a43427ebf9a416aa0886980bd3c6e7059b1ec6ea6a7`；4个关键二进制身份见该目录 `provenance.json`，不是整个Player目录的完整文件清单 |

较早 15:56 构建的官方20正向/asset/audio故障结果使用先前发布的 `381940…` 原型；最终 `5e4242…` 收紧了非 IO 内部异常锁存。旧运行结果保留原归属，但其exit记录没有绑定Player/程序集哈希、旧Player未按最终版本同等归档，不能宣称具有完整的旧二进制重放来源链，也不追认成最终版本全部重验；最终版本本次只有下述 Scene 故障运行。首次夹具构建曾在 **任何解密 Stream 调用前**于 PackAtlases 崩溃，修正自有 Sprite authoring 后已成功，日志 `evidence/unity-build.log` 保留；它不是本次 Scene 认证故障，也不是密码兼容性失败。

**分项门禁实绩**

| 门禁 | 实际结果 / 证据 / 未完成 |
| --- | --- |
| G0a native 对照，部分通过 | `evidence/editor-baseline.txt`：Editor File/Stream 代表样本通过；Player `native/runs/file-lz4-normal-20261016-154004`、`stream-lz4-normal-20261016-154024`、`stream-none-normal-20261016-154120` 均 exit0、FinalNative=0。同步 shared、异步 asset、Prefab/Atlas/Audio/Scene 和重复资产实例身份通过；不同包 `bundle.name` 保持 `g0_*`。LZ4 创建后实际 Read：asset `4,196,640B`、8 秒 Audio `393,216B`、Scene `461,968B`，确有延迟读取。外部重复 native 载入屏障及 LZMA runtime 未完成，不能报 G0a 全通过 |
| G0b RAW/互操作通过，后端与启动探针部分阻塞 | 最终 Mono 功能套件 `25/25`，非 IO 异常锁存回归 RED `1 fail` → GREEN `1/1`；官方 Java 探针 `29/29`，Java→C# Mono `14/14`。14 个长度 `0..16MiB+1`，首段/满末段/计数器256进位、双向共2800次随机定位、跨段/EOF/坏tag/错key与AAD/换文件/重排/拼接/截断/溢出/短读/线程/所有权覆盖；codec 对抗测试不依赖外层 SHA，Java 独立拒绝19个样本。RAW 流先验首末、逐段认证后复制，坏段不复制、fault sticky。F3 当前选定 key 集/缺key/未选历史key与初始化 join 成本未做，不能用错key单测代替 |
| BC netstandard2.1，受阻 | 完整原始分支构建 `29` errors，涉及 HashCode.AddBytes、Unsafe、BitOperations、ValueTask 等 NS2.1 不具备的 API。仅选原有分支的逐文件 metadata hybrid 在12文件后扩散为 `135` errors，并移除 GCM 需要的 Arrays Span API；已停止绕行，未加 polyfill/删成员/改核心。`crypto/backend21/status.json` 明确 artifact=null；日志 `crypto/logs/backend21-12-utf8.log`、`hybrid-initial.log`。**没有可用2.1 DLL或2.0/2.1性能对照** |
| G0c 正向与部分拒绝证据 | 早期官方20 `auth-official20-lz4-normal-20261016-155759` 完成 TextAsset/Prefab/Atlas/8秒Audio/Scene，exit0、FinalNative=0。直通流 create/asset IOException 对照正常 exit1；认证 asset/audio 后创建翻位各实触发1次，`injected-faults:*` 与 `crypto-fault:*` 均存在，exit1、FinalNative=0、输入已Dispose。明文流64驻留包通过；名为cancel的场景只模拟不消费创建结果，验证在途native drain/输入保留，没有实际取消或分离等待者，**不证明真实取消传播或退出所有权**。这些只证明各自探针，不是正式 Lease/F1/F2/F3 集成 |
| **G0c Scene 延迟认证故障，失败并停止** | 最终 `native/runs/auth-final-scene-fault-20261016-161857`：Bundle 创建成功后在底层只读密文返回缓冲翻位，随后 LoadSceneAsync 实际进入 GCM DoFinal 并报 `mac check in GCM failed`。`player.log:19-50` 记录 IOException、ManagedStreamHelpers、不可读/Seek 错误及 `archive:/BuildPlayer-FixtureScene/... [Position out of bounds!]`，随后 **Crash**；exit `-2147483645 / 0x80000003`。`steps.jsonl` 止于 `scene-after-create`，无 `cleanup-begin` / `result.json`，FinalNative和输入收尾**未知，不能记0/通过**。没有把坏tag返回0、补零、关流抢占或落盘fallback来掩盖 |
| G0d，未进入 | G0c 已触发真实阻塞停止条件，不继续公平重复 File/Stream/Auth Player 性能、参数对照、认证64驻留、Uncompressed/LZMA、Scene切换重叠与预算验收。旧单次计时只留原始记录，不生成“损耗百分比”或公平 P50/P95 结论；128MiB、64KiB/两缓存仍是候选，不据此冻结产品默认 |
| G0e，部分观察，未整体通过 | 最终故障轮启动**之前**外部监测 cipher payload、该次 run、真实 persistent/temp 四个受控根，持续到进程退出；`external-write-events.json` 为24事件、0监测错误。`io-observation.json` 前后文件中没有完整 raw Bundle SHA匹配，变化最终文件共581,934B，包含日志/观察记录及 Unity 自动 crash.dmp，不是实际累计写字节或物理磁盘IO。未看到解密路径创建完整明文 Bundle，但取消/退出等全场景未完成，不能给“无明文落盘”整体通过 |

本次总计11次 Player 运行：6次正向 exit0、4次受控拒绝 exit1、1次 Scene native crash；`evidence/g0-summary.json` 逐次索引。负例的 exit1 是预期拒绝证据，不改写为普通成功；native crash 不是可接受的拒绝结果。

**实际成本与保证边界**

- 仅有官方20的 **辅助 x86 Mono 顺序解密**三次样本：1MiB 为 `68.850 / 69.122 / 68.808MiB/s`，每次分配 `1,284,680B`；32MiB 为 `67.518 / 68.414 / 70.870MiB/s`，每次分配 `34,572,232B`、Gen0=8。六个具体样本见 `crypto/results/benchmark-mono20.json` 和 `crypto/logs/performance-mono20-final3.log:2-7`；`performance-mono20-final3.json` 只有用例汇总。不是 Win64 Player、native 回调或2.1对照数据，不据此推导冷盘/音频欠载/加载损耗。
- 显式文件数据缓冲为 `196,576B/input`（64KiB cipher + 两个65520B plaintext段），在1MiB/32MiB输入下相同；不包括BC状态/临时分配、调用方、Unity bridge、native对象或工作集。旧 Player 的分配计数曾返回0，最终补充计数器/BCL AesGcm 探针因崩溃未落盘，**0不可解释为零分配，也没有本机Player AesGcm支持结论**。
- 认证缓存依赖独占不可变底层；不会每次重读已认证缓存以侦测同长度外部修改。跨坏段Read可先复制合法认证前缀后抛异常，但绝不复制坏段，不提供整次Read原子性。自有key/明文数组清理不等于BC内部key schedule可可靠擦除。
- 实际应用 persistent 为 `C:/Users/Kano/AppData/LocalLow/YL-E04-Isolated/E04G0-c7a9`，temp 为 `C:/Users/Kano/AppData/Local/Temp/YL-E04-Isolated/E04G0-c7a9`。Unity 自动生成 `Crashes/Crash_2026-10-16_161906225/crash.dmp`（460,354B）；已按SHA复制到隔离根 `evidence/crash-scene-20261016-161906225/` 并保留原件。dump 可能含非生产key/明文内存，本次未检视其内容；没有完整Bundle的SHA匹配**不证明dump无明文**。未安装驱动、未改全局配置，也不承诺OS分页/转储/未监测路径安全。
- 目录事件只记录路径/时点，不保存写入内容；完整文件SHA只比较最终仍存在且尺寸/mtime变化的文件。因此不能排除瞬时写后删除、包装文件内或部分明文，24事件/0错误也不代表观察完备。dump原件及副本均按敏感测试证据本机保留，不公开附送、不自动清理。

**结论、成本与停止点**

当前只判定**这份原型 + 本机2022.3.62f3c1 Windows64/Mono + 所测Scene故障路径未满足安全失败/收尾门禁**，足以阻止产品接入；不是“缺官方C#所以不可行”，也不是对所有分块认证实现的否定。此次只观察到1次Scene crash，未重复复现、未做同Scene直通IO故障对照，native内部精确根因尚未确定；已按首次真实阻塞停止，不通过猜测修复继续扩展实验。

后续若另获授权，应先以归档Player/相同密文核对正向与Scene直通IO对照，定位 ManagedStreamHelpers 对抛错/CanRead/Seek/部分合法前缀的处理，证明在不返回坏段、不伪EOF、不强关流的前提下能安全终止。需重新做取消/退出/迟到、Unload/Dispose异常owner、纯Bundle通知与完整G0d/G0e，成本为新的native故障研究和回归，不是替换API两行。仅改善BC吞吐/Span分配不能证明已修复native崩溃；完整2.1构建若需兼容层/源码调整，已超本次metadata-only边界，须另评维护与安全成本。

**该批次已停止；后续获准的有限定位见 28.10.16，不代表恢复全部 G0 或进入 G1～G4，不自动选择整包内存或临时明文，不修改正式依赖/协议/资产，不推进 Workbench、8.10 或阶段8收口，不做 Commit/Push/其他Git写入。**

#### 28.10.16 Scene 有限定位 SD1：普通 IO 同样崩溃，能力变体未修复

**授权与保护**

- 2026-10-16 17:12 UTC 用户授权仅按已说明的批次执行：冻结现场、最终版本正向基线、同 Scene 普通 IO 对照、按结果做单变量验证、必要回归、回写本 Plan，然后停止待技术审核。不包含 G1～G4、引擎/依赖升级、fallback、BC2.1 续建或 G0d 性能扩展。
- 本轮子目录为首批隔离根下 `native/scene-diagnostic-20261016-1712/`，下文称 SD1；归属在 `owner.json`，过程与裁定在 `progress.json`，运行索引为 `runs.json`，最终摘要/保护/产物 SHA 在 `summary.json`。旧 `evidence/g0-summary.json` 等首批结果不覆盖。
- 开始时验证原始 `5e4242…` 源码、归档 Player 的4个关键二进制、所有 raw/cipher 夹具 length/SHA；冻结 **574 项**旧证据/源码/夹具/关键资产。本轮工作区另建 **158 项** SHA 基线；回写前主工程零变化，旧574项零变化。回写后只允许本 Plan 增量，最终对比见 SD1 `summary.json` / `workspace-final.json`。分支/HEAD不变、暂存为空，无 Git 写操作。
- 本轮开始观察到原工程 Unity PID为`14272`（前批为3936），不推断重启原因，本轮未启动或操作它。只顺序启动既有自有隔离工程；正常/故障 Player 均已退出。未触碰正式 Assets、Packages、ProjectSettings、用户配置或旧密码核心。

**单变量与验证实绩**

| 场景 | 源码 / 产物身份 | 实际结果 |
| --- | --- | --- |
| 最终原型正向基线 | 既有归档 `official20-final`，源码 `5e4242…`，Assembly-CSharp SHA `c37f09f2a43ab20ecc745a43427ebf9a416aa0886980bd3c6e7059b1ec6ea6a7` | `native/runs/sd1-baseline-auth-normal-20261016-171857` exit0、passed；TextAsset/Prefab/Atlas/Streaming Audio/Scene 完整流程通过，FinalNative=0，shared/asset/atlas/audio/scene 五个输入的 Dispose 均完成。这是最终版本新增的正向证据，不再只依赖首批旧版本 |
| **同 Scene 普通 IO 对照** | 同一归档 Player、LZ4 raw 夹具，调用直通 FileStream 包装，**不调用认证流**；只在 Bundle 创建后下一次有效 Read 进入前抛 IOException | `native/runs/sd1-baseline-stream-scene-io-20261016-171921`：`player.log:19-39` 为 `E04_G0_INJECTED_READ_FAILURE` → ManagedStreamRead → Material/Shader反序列化尺寸错误 → Scene Position越界 → **Crash**，exit `-2147483645 / 0x80000003`。没有 CanRead=false 的验证异常；无result/cleanup证据，FinalNative/输入收尾未知 |
| 能力合同 RED | 新增隔离 Editor 探针 `native/UnityProject/Assets/Editor/SceneDiagnostic.cs`，使用真实只读夹具和既有 ProbeReadStream/AuthenticatedStream | SD1 `tests/caps-contract-red.json`：**1/2**；普通 IO 失败前未改 caller buffer、未推进Position、能力仍true的对照通过；原始认证流因 fault 后 CanRead/CanSeek=false 在指定断言失败，不是编译失败 |
| 能力合同 GREEN | 复制原型为 `variants/capabilities/E04Crypto.cs`，**仅两处 getter**从 `!disposed && fault == null` 改成 `!disposed`；Read/Seek/Length的sticky拒绝、认证、清理与缓存完全不改。源码 SHA `bfa6c449a609c0f7799fbc124dafdbc5374d42b56abcee265fb4cb17f631d044` | SD1 `tests/caps-contract-green.json`：**2/2**；真实中间段翻位触发GCM失败，caller sentinel未变化，首fault锁存、重复Read继续抛错、不提前Dispose，最终Dispose后能力关闭。只证明该managed合同，不是native安全修复；未重跑完整25项codec套件，不追认成此变体全量回归 |
| 能力变体构建及正向回归 | SD1 `players/capabilities/`；Unity Build succeeded，0 errors/0 warnings，Mono2x。Assembly-CSharp SHA `cc945634a3ebe43d45943707ce361af415264a9479a68596cd990720cd1d06ef`；UnityPlayer/BC与基线相同。两版exe启动器SHA相同，**不能只用exe区分版本**；关键身份见 `provenance.json` | `native/runs/sd1-caps-auth-normal-20261016-172552` exit0、passed，FinalNative=0，五输入Dispose完成；没有改/重新随机加密夹具，没有混用不同密文字节 |
| **能力变体 Scene 认证故障** | 同密文/同注入时机/同Runtime探针，只改变能力getter | `native/runs/sd1-caps-auth-scene-fault-20261016-172618`：`player.log:19-61` 仍有GCM失败/反序列化错误/Position越界及 **Crash**，exit同为 `0x80000003`。原来的CanRead校验异常消失，替换成faulted Seek的IOException，**不是安全结束**；`steps.jsonl`止于scene-after-create，无cleanup/result，不能写FinalNative=0 |

本轮共 **4 次 Player：2 次正向通过、2 次native崩溃**；两次Editor合同运行为 RED 1 fail → GREEN 0 fail，另有一次成功Player构建。没有有效安全修复候选，因此未进入“对有效候选重复验收”，不把一项managed合同变绿当作故障已修复，也未恢复G0c剩余取消/退出/清理异常或G0d/G0e整体验收。

**结论与未做项**

1. **已把问题缩小到所测 Scene 延迟 Stream 读失败与 native 反序列化之间，GCM失败、CanRead翻转、认证前缀部分写入都不是普通IO崩溃的必要条件。** 直通故障分支在调用底层Read或写入caller buffer之前抛错；针对同一实现的managed对照也证明缓冲/Position不变、能力保持，但native仍崩溃。因此不能把两处能力getter调整当根治。
2. 这仍**不是已定位的Unity内部根因或“所有C#方案不可修复”结论**。公开2022.3 ManagedStreamHelpers仅在Read/Seek正常返回时回写native返回值；本轮没有检查native初始化/异常处理内部。能力变体仍拒绝fault后的Seek/Length，未试放开元数据定位；也未做整次Read原子提交变体。普通IO对照已能在无caller写入时复现，继续堆提交策略不是本批优先项，且不能据此宣布这些策略永远无效。
3. 为保留可比性，本轮继续使用现有完整的 asset/atlas/audio→Scene 前置流程，**尚不是剥离依赖后的Scene-only引擎最小工程**。可重放入口为隔离 `native/SceneDiagnostic.ps1 -Action run -Variant baseline -Transport stream -Case scene-fault -Label <new-safe-label>`；每次真实参数、Player和程序集SHA另在对应exit/runs记录。重放会触发已知崩溃，不自动执行或远程提交issue。
4. **下一技术审核方向应为更小的Scene-only普通IO复现及引擎修复/版本验证边界**，而非继续BC性能优化或产品接入。引擎内部根因仍未知；如需另一个C#变体、引擎补丁/版本对照或上游报告，另开有明确范围的新批次，不自动升级、不改Unity/BC核心、不吞错、不返回假EOF/补零、不强关native正在使用的输入、不落完整明文文件。

**环境、证据与收尾**

- 本轮最终基线Player实际记录 `IntPtr.Size=8`、Mono `6.13.0 (Visual Studio built mono)`；BCL `AesGcm(byte[])` 构造探针得到 `PlatformNotSupportedException`，只适用于本机所测构造路径，不外推其他Unity/平台。Allocated/PeakWorkingSet计数仍返回0，不能解释为零分配/零工作集，没有公平性能或损耗百分比结论。
- 构建前检查等于 `ApiCompatibilityLevel.NET_Standard`，Unity 2022.3 [官方定义](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ApiCompatibilityLevel.NET_Standard.html)为.NET Standard 2.1；保留的enum ToString原始结果是`NET_Standard_2_0`，不据该名称或BC目标netstandard2.0误判工程profile。
- 两次dump及日志副本按SHA保存在 SD1 `crashes/baseline-stream-scene-io/` 与 `crashes/caps-auth-scene-fault/`，原件不删除，未读取dump内容、不公开附送。每次外部观察均先于Player启动，事件/最终文件SHA仍不能排除瞬时、包装、部分明文或dump内存；没有扩大“无明文写出”保证。
- 收集脚本曾因单条旧记录被PowerShell展开成scalar而报错，已修正并仅收集已完成的IO运行，**没有为了修复索引重复触发Player**。原始exit/log/事件记录未修改。
- 仅将本轮安装到隔离工程的E04Crypto.cs从`bfa6c4…`恢复为冻结`5e4242…`；SD1失败变体源码、Player、合同测试和证据全部保留，详见`source-restored.json`。未改Library/Temp缓存，恢复后不再启动Unity编译；不把残留生成程序集当作基线重建结果。

**SD1批已停止并保留no-go；后续续验授权见28.10.17。未获得产品接入或fallback许可，不进入G1～G4，不推进其他阶段，不做Git写入。**

#### 28.10.17 G0 续验 C1 已收口：Scene 限定风险接受，条件性提交实施审核

**本轮授权（2026-10-16 18:24 UTC，先记录后执行）**

- 恢复尚未完成的 G0 隔离实验，继续 Tink AES-GCM-HKDF RAW / 有界可Seek认证Stream / Unity native Bundle 路线；不重定总体方案、不重做31项审计、不进入产品G1～G4。
- **已知风险／本轮受控失败要求豁免**：仅限已复现的“Scene在native接手后Stream读取失败可能导致进程崩溃”。28.10.15～16的历史no-go、crash、清理未知仍保留，不改为修复或Passed；本轮不重复制造同一崩溃，不自动做Scene-only复现、引擎升级/补丁或上游报障。进程崩溃时不承诺F1无关图继续服务或正常收尾。
- 豁免**不覆盖**合法输入崩溃、我方定位错误/线程竞争/提前释放，或正常取消/退出/预算错误导致的故障；这些仍是需要修复或停报的新阻塞。新崩溃不得自动归入已接受风险或连续试大量变体。
- native前保留完整密文length/SHA与必要首末段认证，读取时对应段先认证后交付；不因Scene故障强加每次全量AEAD扫描，也不取消既定发布、Prepare/激活的全量认证设计。不关认证、不交未认证字节、不伪EOF/补零、不清fault冒充成功、不Dispose抢占native，不改整包明文内存或临时明文落盘。
- 复用既有可用codec/官方互操作与适用native证据，先冻结本轮源码/DLL/Player/程序集/夹具身份，原始5e4242、能力变体bfa6c4与后续构建不混算；只重验受影响部分。以官方BC2.7.0 netstandard2.0取得真实Windows64 Mono结果，不无依据重试受阻的完整2.1/hybrid，不修改密码核心/API/TFM或装入不兼容DLL。
- 本轮补齐外部重复native身份屏障、必要压缩边界、**真实取消触发与责任变化**、退出/迟到/清理异常责任、普通资源代表故障；同条件File/明文Stream/认证Stream重复成本、持续Audio、多包/切场景重叠；F1受控失败闭包、F2共享预算/明确拒绝/显式重试、F3选定key集轻量认证。仅做最小探针，不复制ResourceSystem/Store/Provider/更新系统。
- 128MiB、64KiB和两缓存仍是候选，需实测工作集/重叠预留使用归还后推荐；预算不足不等待/驱逐/扩张。计数器不可靠的0不作零分配/工作集结论；辅助x86Mono与单次小资产不替代Player性能。观察必须运行前部署并持续到正常/取消/退出收尾，认证运行只开独立cipher目录，不用raw路径载入；结合源码路径审查，不仅检查最终SHA，明确瞬时/部分/包装/未观测路径限制，OS分页与crash dump不在无明文保证内。
- 只修改owned隔离载体必要源码、测试和取证；旧证据保留，新运行独立标识。主工程本任务只增量维护本Plan，不改产品/依赖/Packages/ProjectSettings/游戏/配置；不操作原工程Unity、不对同工程开第二实例。非生产key/dump敏感本机保留，不上传/提交/公开。不做任何Git写入，不进入Workbench/8.10/其他功能。
- 完成或遇现有授权内无法解决的新实质阻塞即报告停止，**不因已豁免历史Scene故障重复停止**。结果分别列沿用/新增通过/失败/未测/豁免，给后端/段/预算推荐与局限；其余必需门禁满足时才可建议“明确保留Scene风险进入G1～G4实施审核”，不宣称全绿或正式加密已可用。

**C1 执行结果：剩余可行性探针已完成；不是产品全绿**

以下路径除另述均相对首批隔离根；本轮根为 `native/g0-continuation-20261016-1824/`（简称C1）。执行/源码入口为隔离 `native/ContinueG0.ps1`、C1 `RunObserved.ps1` / `RunBatch.ps1`；逐次结果在C1 `runs/`，完整数据与限制在 `evidence/analysis.json`、`evidence/benchmark-brief.json`，工作区/旧证据保护见 `summary.json`，最终源码/产物哈希见 `artifact-hashes.json`。敏感dump没有上传或提交。

**身份、复用与边界**

- 最终认证codec始终为 `5e4242c820a6a70c2f9a3a194fcb77660f51c5323587f3e1ea6dc6da47c83ec6`，**未采用SD1能力变体**、未改任何密码核心。官方BC2.7.0 netstandard2.0 DLL仍为 `d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495`，不重试2.1/hybrid。
- 本轮新Player编译成功，身份在C1 `players/c1/identity.json`；Assembly-CSharp为 `fafd06a29f2b90672d603a9ead315fc1c139941efe2c337244d2a52d5d582fb3`，UnityPlayer为 `b07814a510eea3f8b66a25117ab602237037d74668e6ce1f8de92f60c0ef28c4`。exe启动器与旧版相同，**以程序集而非exe单独辨别版本**。BC/Tink许可随Player保留。
- 沿用原自有夹具的全部raw/cipher字节，在C1分离目录复制后核对身份；仅新增 `c1_scene_b` 独立Scene Bundle，以实现两Scene同时加载。其每次真正加密使用codec新随机salt/nonce，不重加密旧夹具。C1清单SHA：raw `aa4263de5d3ce733ce1ad53049d6e3428bf915a980f8c0abea4fec2647f20ee0`、cipher `0ce3e0aeada3979f477ee6952815a9327e52f5e2ad84c535976429a3ab6b1099`。
- 沿用25项codec、官方Java双向互操作及SD1的适用证据，不把不同构建混算；本轮新Runtime探针 `ContinuationRunner.cs`（构建SHA `91eceb94973de1912c472e31f3e9087fa68ea1628f2fdf5ed29d3399b02ee2b7`）只在新 `--continuation c1` 入口使用，不接正式ResourceSystem。F1/F2小型纯元数据代码175行，不新增资源平台；27项测试先在.NET宿主RED27fail→GREEN27pass，再在本轮Win64 Mono Player实际27pass。
- 每次native前仍完整length/SHA，再构造首末认证输入，访问段先认证后返回。本轮未新增“每次全量AEAD”，没有任何fallback。BC内部schedule擦除、段缓存不可变假设、合法前缀跨坏段Read等既有限制不变。

**本轮新增通过：33次独立Windows64 Mono Player，零新崩溃**

| 分项 | 本轮实际证据与保证范围 |
| --- | --- |
| 真实取消、退出和迟到 | `c1-cancel-*`：native创建实际在途时触发CancellationTokenSource.Cancel，TaskCompletionSource真实变Canceled，消费者责任1→0，native drain责任仍1；不提前关闭输入，完成后TrySetResult被拒绝，才卸载与释放。`c1-quit-*`实际调用Application.Quit并由wantsToQuit延迟退出、停止新输入准入、同样取消消费者并收集迟到结果。**不是首批“只不领取结果”**；退出只验证该最小owner探针，不宣称产品EndSession已接入 |
| native身份/重复屏障 | `c1-identity-*`真实LoadFromStream结果name保持`g0_cancel`，GetAllLoadedAssetBundles预检查拒绝下一同身份输入且不增加预算；绕过探针前置屏障后真实调用native重复加载，返回null且原Bundle继续可用、仅1个同名native。最终两输入安全回收；不是移除身份校验换通过 |
| 受控普通资源故障/F1 | 最终codec下各1次auth与明文Stream的TextAsset异步读取故障、StreamingAudio播放阶段故障，均真实触发注入并锁存；进程继续，无关bulk资源成功消费，旧责任先收集再显式重新验证/打开同名输入成功。asset故障经同一gate拒绝模型中的Ready/idle入口、不污染共享依赖/独立B。共享S故障传播、反向闭包及collect后重开另由27项小图合同验证，**不是对产品所有获取入口的集成验收**；Prefab/Atlas正常消费通过但没有专门篡改测试，不默认所有非Scene类型都安全 |
| F2真实预算拒绝/归还 | `c1-budget-*`以 `3×196576=589728B` 固定小预算打开3个真实认证输入，第四个立即明确ContentBufferUnavailable，无新输入/预算副作用；真正native卸载/流关闭后返回额度，再显式打开第四个成功，无等待/驱逐/扩张。27项合同另证共享pending/allocated不双计、闭包整体预留原子失败、取消单一责任不释放共享节点、溢出等；正式跨项目ResourceSystem仍待G1～G4接入 |
| 清理异常责任 | `c1-cleanup-fault-*`在**实际调用前**分别注入Unload边界、Dispose边界异常，保留对应native/input/ticket与诊断；其他3输入继续回收。清除一次性探针注入后显式重试，最终全部归还。只证我方控制流不丢owner，**不证明Unity Unload内部已部分执行后抛错的状态可恢复** |
| F3选定key集 | `c1-contracts-*`为两个选定 `(P,active)` / `(Q,builtin)` 代表，重复P只探一次；构造配置零取key/零IO，初始化两个join返回同Task，成功前没有native。2代表首末认证实际读取 `184611B`，单次wall `20.7825ms`（含异步调度，不是纯密码CPU或P95）。缺选定key、错key均拒绝；未选history不取key、不阻塞；全None零key/cryptoIO。原型使用两个标签映射同一非生产fixture key，**不是最终YL envelope/provider/keyring实现** |
| 压缩边界 | 最终codec的LZ4重复正常流程、Uncompressed与LZMA各1次完整TextAsset/Prefab/Atlas/StreamingAudio/Scene通过；LZMA创建期整包解压带来不同读取/内存分布，不以创建后无延迟Read误判失败。None/LZMA仅边界验证，无公平重复压缩性能排名 |
| 工作集/重叠 | File/Stream/Auth分别各1次，旧Scene+UI/Atlas+Audio+Prefab保留时加载新Scene，确认两个Scene同时loaded，再以每批4包创建64bulk并全部保持：共70个live输入。另各1次持续Audio24秒，isPlaying与适用路径的延迟读取有效；**没有可靠音频underrun计数或听音验收，不承诺无可闻毛刺** |

33次均exit0、report passed、FinalNative=0、RetainedInputs=0、BudgetUsed/Reserved=0。4个普通资源fault用例的passed表示“预期故障被受控拒绝并正确收尾”，不是坏密文成功载入；Scene豁免项未运行、未计入Passed。原生创建/资产/Scene/Audio正常持有期间检测输入仍有效，真正卸载后才Dispose与释放ticket。清理异常的两条预期诊断原样保留，不吞掉以制造空日志。

**真实Player成本（不是辅助x86Mono，也不宣称生产预算已达标）**

同一C1非Development Player、Win64 Mono、本机配置、LZ4夹具、64KiB/2slot、32768 native读取buffer，File/明文Stream/Auth各**5次fresh process**交错运行；无其他隔离Player/Unity并发。未清OS缓存，每个包又先完整SHA，因此是**前置SHA后的缓存条件**，不是冷盘；各场景8秒音频hold，不用hold占主导的总时间计算损耗百分比。统计P50为样本中位数，n=5的nearest-rank P95就是样本最大，不是可靠总体尾延迟。

| 指标 | File | 明文Stream | 认证Stream |
| --- | ---: | ---: | ---: |
| 整图前置SHA总耗时 P50 ms | 86.803 | 86.832 | 87.873 |
| native创建await总wall P50 / 样本P95 ms | 33.324 / 33.402 | 33.518 / 34.897 | 25.307 / 33.506 |
| 资产消费阶段 P50 ms（两个4MiB TextAsset+Prefab） | 12.820 | 14.224 | 125.166 |
| Scene加载await P50 ms | 17.047 | 17.030 | 17.044 |
| managed Read累计 P50 ms | 不经managed回调 | 4.284 | 134.090 |
| 单次Read最大值的样本P95 ms | 不适用 | 0.181 | 3.083 |
| GC.CollectionCount(0)增量 P50 | 3 | 4 | 115 |
| 每轮最大帧时间的样本P95 ms | 62.127 | 68.019 | 127.712 |
| 外部Windows采样工作集峰值 P50 B | 164208640 | 164519936 | 166346752 |

- 认证资产阶段相对明文Stream的P50差约 **+110.942ms**，相对File约 **+112.346ms**，不是所有负载统一百分比。AssetMs含异步NoiseA、同步NoiseB及Prefab消费，另行统计相同明文摘要VerifyMs，不把校验时间都算解密。native创建await受帧调度粒度影响，认证样本P50较小**不是加密加速**结论；Scene正常wall相近不抵消资产/GC/帧尾成本。
- ReadMs包含认证/缓存/锁/缓冲复制，PhysicalReadMs是被包装FileStream读耗时，不是物理磁盘IO；File分支native IO不经过managed trace，0只表示未覆盖此层，**不是零IO**。OpenAuthMs另记录首末认证开销；没有伪称精确隔离出的AES-only CPU。
- `GC.CollectionCount(0/1)`原始增量、`GC.GetTotalMemory(false)`存活峰值、Unity heap/allocated/reserved与外部50ms目标采样的WorkingSet/PrivateBytes均保留。Mono两代计数可能相同，不能当作独立代事件相加；没有可靠累计分配bytes或GC pause归因。先前不可信的0没有复用成零分配/零工作集；帧P50/P95受8秒hold稀释接近8.33ms，另列最大帧暴露卡顿，未用“平均帧好看”掩盖峰值。
- 官方20按32B处理分配小数组的源码热点与本次GC/耗时方向一致，但本次不是Profiler逐函数归因。**调大认证段不会消除该BC内部数组分支**，也不能用buffers固定证明总堆零分配；没有为填2.1对照表改源码/伪造DLL。

**预算、候选参数与后端建议**

- 每个认证输入的显式文件数据数组固定 **196576B**（65536 cipher + 两个65520 plaintext）。正常5输入峰值 **982880B / 0.937MiB**；新Scene重叠6输入 **1179456B**；70输入峰值 **13760320B / 13.123MiB**，最终预留/使用均0。这里不含BC状态、native bridge、资产、元数据、调用者数组，不把此预算当整个进程上限。
- Auth重叠的外部工作集峰值 **216088576B**、PrivateBytes峰值 **333795328B**，managed存活峰值 **20541440B**，Unity allocated峰值 **88010482B**；bulk消费约411.556ms、GC0增量270。File/Stream重叠分别各1样本，不能做稳定百分比排名；完整值在analysis.json。预算承诺峰值与实际数组模型吻合，不把小夹具代表整个游戏资源规模。
- **段大小/缓存推荐**：保留64KiB/2slot作为已互操作、已实测的G1设计基线；不声称它是最优参数，不做全排列。**预算推荐**：128MiB可保留为Windows64实施审核候选，本轮常规+切换+70驻留有余量；只验证到13.123MiB，不证明128MiB必要、也不据此自动冻结产品默认。正式默认需应用工作集/支持平台和允许并发规模裁定；不默默调大以过测。
- **后端推荐**：官方20作为可复现正确性基线已足够，但不建议在未接受上述成本时直接冻结为“低分配、低卡顿”的正式后端。若应用要求帧内/交互式大资产加载，应先另行批准兼容本Unity profile的成熟低分配AEAD后端对照，或批准更大范围受维护构建适配，并核对许可/升级成本；仍使用同一Tink framing/认证合同，不手写或修改密码核心。仅增加2.1 metadata已证失败，不能假定零成本修好。若选择保留官方20，须明确接受实测负载成本及应用性能门限；主线程同步SHA/消费调度也需在G1设计中承接，不能把全部帧峰值归因密码库。

**无完整运行时明文Bundle主动写出：有限范围观察完成**

- 17个auth运行只使用C1 `payload/cipher/`；33次观察都在Player创建前安装FileSystemWatcher并持续到进程退出，覆盖该次payload/run/实际persistent/temp，事件Error总数0。每次read-only file-open/key-probe日志均核对为cipher根内`.enc`，认证进程不打开raw Bundle；构建端/父取证脚本读raw清单不混算运行期加载。
- runtime源码审查：`AuthenticatedStream`只读底层/写自有内存数组，不创建文件；ContinuationRunner写入仅指定output的steps/result，manifest读取为fixture元数据，Bundle File API仅在显式File对照分支。SegmentCodec.Encrypt只在构建端调用生成密文；完整源码定位在analysis.json RuntimeFileSites。配合事件与前后文件没有观察到解密路径主动创建完整明文Bundle，最终变化文件中完整rawSHA匹配数0；结论**不只依赖最终SHA**，也不是逐写入内容捕获。
- 路径事件不能排除瞬时删除、部分/包装明文、未观察目录或Unity内部未知写入；不证明全系统无明文，OS分页/crash dump明确除外。不安装驱动或改安全配置；此前dump及副本仍作敏感本机证据保留，本轮无新dump/崩溃。

**沿用、失败、未验证与审批停止点**

- **沿用**：RAW成熟格式/官方Java互操作/25项codec与适用正常native证据、原Scene no-go/清理未知、2.1/hybrid无产物。**新增通过**：本节33次Player代表探针、27项F1/F2最小合同、选定key集F3、真实取消退出、身份/普通故障/预算/清理控制流、压缩边界、重复Player成本及有限写入观察。
- **失败保留**：Scene故障未修复，标“已知风险／本轮受控失败要求豁免”，不计Passed；BC完整2.1/hybrid仍失败且无新尝试。**本轮没有新的功能崩溃或必需门禁失败**，官方20高GC/时延是明确成本与产品性能待裁，不能写成性能已满足未知业务门限。
- **未验证**：正式ResourceSystem/Lease/EndSession/跨项目真实集成、YL envelope/发布/Prepare/keyring产品链、引擎部分执行后异常状态、所有Unity资产类型、总分配bytes/GC pause/可靠Audio欠载、冷盘、完整参数最优、真实游戏全部工作集、IL2CPP/移动平台，均不由G0最小原型冒充通过。
- **可提交的建议**：分块认证Stream路线具备**在明确保留Scene风险、确认正式密码依赖与性能门限/后端策略、确认预算默认及平台范围后，进入G1～G4实施审核**的可行性依据；不是无条件全绿，不是已批准实施。若应用不能接受当前官方20资产/帧尾成本，应先批准低分配后端专项，不先把高成本实现混入正式框架。
- **停止**：续验到此收口，按新增代码/结论做一次定向只读独立复核与最终保护检查；不循环重审历史，不自动进入G1～G4、Scene-only/引擎变更、Workbench或8.10，不做任何Git写操作。
- **源码/保护收尾**：已将构建所用源码与相关meta固定在C1 `source/c1/`，归档Player及身份文件保留。仅恢复本轮向旧隔离ProbeRunner添加的一行分派，原入口SHA恢复 `3dfd9e502dc943411ded1d4e21831dec4f6eabbe29e6e0f8e9e2998ba275868f`，之后不再启动Unity导入/编译；已测C1须按归档Player/源码快照重放，不把恢复后的生成缓存当作C1重建。902项旧资产/源码/证据核对零变化；本轮158项工作区基线中仅本Plan变化，暂存为空，原工程Unity仍PID14272且未操作。
- **定向独立复核完成**：只读核对C1新增代码、33份结果和本节结论，未发现Critical/Important或必须先修的报告问题；确认真实消费者取消/原生owner分离、受测清理边界责任、最小F1/F2账本与F3线程范围、性能口径和有限写入观察表述。没有重跑实验或重审历史密码/全部产品代码；结论仅支持上述有条件实施审核，不追加授权。复核记录及最终证据完整性见C1 `evidence/progress.json` / `evidence/final-verification.json`。

#### 28.10.18 密码后端低分配对照 B1：实测完成，推荐 CNG + BC HKDF，产品待审核

**本轮授权与现场（2026-10-16 19:54 UTC）**

- 用户正式授权一次后端研究与隔离对照，以28.10.17归档C1为基线，不重开总体Plan、不重做G0、不接入产品G1～G4。现场仍为`fusion/8-resource` / `ddf154c`、暂存为空；158项既有工作区文件保留。主工程仅增量维护本Plan，无任何Git写操作，不操作原工程Unity PID14272。
- 自有隔离根下新增`native/backend-compare-20261016-2000/`（B1），`owner.json`登记本轮所有权，旧C1归档源码/Player/夹具/结果等966项SHA冻结。新实验使用独立UnityProject、源码、Player及结果，不修改旧入口，不把旧工程Library或恢复源码当C1重建证据。
- 固定AES-256-GCM-HKDF RAW格式、密文身份、认证与Stream合同；64KiB/2slot、32768 native读取buffer、相同raw/cipher字节及前置全文件SHA不变。只定向筛选并优先实测一个成熟候选，不重试无新依据的BC2.1/hybrid，不手写/修改密码原语，不改完整明文内存或临时文件路线。
- 新后端先取得自己的codec/官方互操作、边界/拒绝/并发/清理证据，再做必要正常Bundle/Scene/Audio/资产与生命周期回归。Scene延迟读失败风险仍按C1限定接受，不重现、不修复、不计Passed；合法输入失败或提前释放不在豁免内。
- 同条件交错重复基线与候选，语义检查与性能测量分开；程序若变化，两后端同用新程序，不直接拿旧C1数字排名。不把不可靠计数器0、GC次数、工作集或少量样本分别冒充零分配、GC暂停、累计分配或稳定总体P95；不自行宣布生产性能达标。
- 仅允许实验依赖/最小薄适配/隔离构建运行，非生产key和敏感证据本机保留，不上传提交。新增系统API/原生资源须明确部署、句柄、缓冲、线程、平台与维护成本；若需密码核心分叉或越界权限则停止。完成定向独立复核、结果回写和选型建议后停止待审核，不自动进入G1～G4、Workbench或8.10。

**结论与单候选依据**

- **候选正确兼容本次受测环境，并在完整认证Stream上取得5/5方向一致、量级明确的实质收益。** 推荐正式Windows64/Mono后端为**系统CNG `Microsoft Primitive Provider` AES-256-GCM + 官方BC2.7.0 HKDF-SHA256**；不是BCL `AesGcm`，不是密码核心改版，也不是已接入产品。没有追加库海选或改变加载路线。
- Context7的Win32索引已查询，细节不足部分进一步核对Microsoft Learn、本机Windows SDK `10.0.22621.0` 的`bcrypt.h`和固定`.NET runtime v8.0.0`的一手源码。Microsoft的[AesGcm Windows实现](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/AesGcm.Windows.cs)及[AeadCommon](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/AeadCommon.Windows.cs)使用相同BCrypt AEAD接口，构成成熟实现参照，不把现代.NET数据当Mono性能证明。完整链接和选择边界在B1 `evidence/candidate-basis.json`。
- CNG直接接受现有12B nonce、16B tag及独立段消息，能固定现有数组、使用88B blittable认证结构，避免BC netstandard2.0热路径的小数组。段AAD仍为空，调用方context继续进入原HKDF info，不能重复添加到逐段AAD。实际在Mono核验x64结构88B、`cbData@72`，并查询provider支持固定16B tag；空明文也执行认证，不把`pbOutput=NULL`尺寸查询当成功。
- 每输入建立1个配置为GCM的算法provider和1个文件key，在所有段间复用，不每段打开；同流沿既有gate串行Read/Seek/Dispose，不同输入各自独立。私有槽在native认证成功且长度吻合后才标记有效并复制；所有失败均视输出不可信、清零并sticky锁存，不伪EOF、不补零、不提前交付坏段。SafeKey持provider引用，确定性先销毁key后关闭provider；SafeHandle仅作兜底，不替代Unity owner卸载收尾。

**来源、版本与归档身份**

下列B1相对路径均从`native/backend-compare-20261016-2000/`解析。没有下载/分发系统DLL、安装组件、修改全局设置或主工程依赖。

| 项目 | 本次实际身份与交付限制 |
| --- | --- |
| CNG入口模块 | Windows系统`%SystemRoot%/System32/bcrypt.dll`，版本`10.0.26100.8875`，SHA256 `d0fc7863d7b034da33f34dc82b96a8604572598a217668a43fc9eca3a8e2a8e1` |
| 映射的系统密码模块 | `bcryptPrimitives.dll`，版本`10.0.26100.9711`，SHA256 `f0dba794068893c9616b0b54bef1be8f904be89eb2cb09b575d3d7fb1b5000f8`；两模块Authenticode均Valid，实际System32映射另见运行`crypto-modules.json` |
| 官方BC | 仍为官方2.7.0 netstandard2.0、程序集2.0.0.0，SHA256 `d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495`；复用28.10.15已核官方CHECKSUMS资产，候选只用其HKDF，不改库源码/TFM |
| B1候选codec | `source/E04Crypto.cs`，SHA256 `f3c9fddf9a3fea8f37776bc039fe7fd918f3c7f30faadbb98651ffce5d8eb619`；从归档`5e4242…`仅替换GCM调用及必要释放，不是YL envelope/provider实现 |
| B1薄适配 | `source/CngSegmentAead.cs`，SHA256 `8078f6d7c354ad959371c1b41c43f605ac7878d540a5a999325c240814daf7d8`；按公开API ABI独立编写，不复制.NET密码实现，不含AES/GHASH/HKDF原语 |
| 新BC/CNG Player | `players/bc/`、`players/cng/`；Assembly-CSharp SHA分别`a070d3d4b635ff0fe54eb0e351fa7eafd5d25559301930b918aa52ca2bd13187` / `1be442126f0845280d02d366eb8a855fac1d7fa6c5f083968222f2ded97a1c77`；完整新源码、两Player目录及运行文件SHA见`artifact-hashes.json` |
| 夹具 | raw/cipher清单SHA仍为C1的`aa4263de5d3ce733ce1ad53049d6e3428bf915a980f8c0abea4fec2647f20ee0` / `0ce3e0aeada3979f477ee6952815a9327e52f5e2ad84c535976429a3ab6b1099`；性能及native用例没有重新随机加密或替换资源字节 |

两份新源码快照`source/bc/`与`source/cng/`逐文件核对，**仅`Assets/E04Crypto.cs`、`Assets/csc.rsp`存在预期差异**；runner、trace、测试、Player/Audio/Quality等配置一致，BC DLL一致。新BC版保留原`5e4242…`认证流，双方共用本轮测量程序，不能称原C1完整重建。两构建均有Build report `Succeeded / 0 errors / 0 warnings`；首次BC包装脚本因GUI调用未等待而报`LASTEXITCODE`未设置，Unity实际继续构建成功，保留成功退出日志并明确**OS exit未捕获、记null**，没有捏造exit0或为补索引重复构建。CNG构建正常捕获exit0。

**新后端自己的正确性与生命周期证据**

- **真实Windows64 Mono Player** `b1-codec-player-*`：NIST/官方BC AES256-GCM空与16B向量、空明文坏tag拒绝；原codec **25/25**重新运行通过，随后官方Java夹具 **14/14**解密/随机定位通过。原Probe同进程两次调用有静态结果累积，第二份报告的`cases=39`是25+14，不是39项Java测试。`tests/results/codec-player-cng.json`、`java-player-cng.json`及Player log保留实际证据。
- 官方Tink Maven1.24.0/Java23.0.1重新读取**Mono候选新生成密文**，双向官方套件 **29/29**，包含19个对抗拒绝样本；不是沿用旧BC通过记录。两侧合计2800次随机定位，覆盖`0..16MiB+1`、首/满末段及计数器进位等既有格式边界。测试夹具新生成仅发生在独立`tests/fixtures/`，不改变性能payload。
- 新增native所有权探针同样在该Mono Player执行：30次末段认证失败open保留caller input且销毁新CNG owner；中间段坏tag不改变caller sentinel、锁存故障、清私有槽/原生key而保留底层流；8 worker ×12独立输入逐字节验证及100次重复open/dispose均通过，不靠强制GC收尾。原codec另重验Read/Dispose、Read/Seek串行和跨坏段仅复制合法前缀等合同。
- `b1-semantic-*` **6次**：正常shared/TextAsset×2/Prefab/Atlas/StreamingAudio/Scene消费、真实取消、真实Quit/迟到、Unload/Dispose边界注入及显式重试、普通asset/audio认证故障与无关资源继续服务。均exit0、正常输入持有到native卸载后才Dispose；失败用例的passed指预期受控拒绝/收尾，不是坏密文成功。未重演全G0或全部Unity类型，没有运行Scene延迟失败。
- 全批**35次新独立Player**：1 codec +6语义 +20常规对照 +8工作集/持续Audio，均有实际exit0/report passed，最终Native/Input/Budget/CNG活动owner及释放失败计数均0；没有新崩溃。`runs/`保存逐次参数、程序集SHA、结果、事件与外部内存采样，`evidence/analysis.json`、`benchmark-brief.json`、`run-records.json`作索引。

**同条件完整认证Stream成本**

Ryzen7 9700X / Windows11 `10.0.26200` / Unity2022.3.62f3c1，非Development、Windows64 Mono、.NET Standard档；LZ4相同资源，64KiB/2slot、32768 native read buffer，原逐包完整length/SHA前置不省略。四模式各**5次fresh process**按固定顺序交错，测量阶段不写详细step日志，结束后统一flush；语义轮另跑。未清OS缓存，且前置SHA已读过文件，所以**不是冷盘**；以下P50只表示5样本中位数，不报稳定总体P95，不用8秒Audio hold主导的总wall计算提速率。

| 指标 | File | 明文Stream | 官方BC认证Stream | CNG认证Stream |
| --- | ---: | ---: | ---: | ---: |
| 整图前置SHA总耗时 P50 ms | 88.343 | 88.225 | 87.350 | 87.279 |
| Open/首末认证 P50 ms | 0.001 | 0.001 | 10.890 | 6.490 |
| 资产消费 P50 ms：两个4MiB TextAsset+Prefab | 12.598 | 14.036 | 123.676 | 14.102 |
| 资产阶段managed Read P50 ms | 不经managed trace | 2.863 | 102.826 | 3.088 |
| 整轮managed Read P50 ms | 不经managed trace | 4.144 | 130.614 | 4.422 |
| 单次Read最大值的样本最坏 ms | 不适用 | 见原始数据 | 2.394 | 0.245 |
| GC.CollectionCount(0)整轮增量 P50 | 2 | 3 | 114 | 11 |
| 资产阶段GC0增量 P50 | 见原始数据 | 见原始数据 | 89 | 2 |
| 每轮最大帧 P50 / 样本最坏 ms | 62.262 / 62.635 | 62.527 / 65.879 | 112.669 / 116.539 | 61.911 / 64.581 |
| 外部采样工作集峰值 P50 B | 163692544 | 163987456 | 165761024 | 165023744 |
| 外部采样PrivateBytes峰值 P50 B | 290066432 | 290738176 | 290619392 | 291037184 |

- BC资产阶段范围`122.324..126.891ms`，CNG为`13.869..14.362ms`，五组配对各减少`108.455..112.950ms`；中位数差约`109.575ms`、在本负载该阶段约减少88.6%，不是所有场景/整轮统一损耗率。Read分别`127.288..134.566ms` / `4.338..4.852ms`，五组GC0均114→11，支持可重复实质收益，不只AES微基准。
- 两认证后端每轮均**336次Read、178次段认证、334 hits/178 misses、11193102B密文读取**，交付字节完全一致；没有靠少读、少认证、增加缓存或去SHA换收益。ReadMs仍包含锁/缓存/复制/计时，PhysicalReadMs只是FileStream包装耗时，不是物理磁盘时间。native await的帧粒度及Scene约17ms的接近结果不用于宣称加密加速。
- **Player累计分配不可可靠取得**：35次阳性分配探针均返回0，`ReadAllocated=-1`，绝不记零分配。GC0/GC1在Mono可为同一次collection，不能相加，次数不等于精确GC pause。额外辅助`.NET10.0.2`宿主使用相同codec/原密文/130段认证与可靠阳性探针，3次读取阶段BC均分配`10657240B`，CNG均观测`0B`；含打开的总分配中位数`10960224B` / `205720B`。各自程序/response及身份按backend独立归档在`tests/bin/AllocationProbe-*/`，共享探针源码为`tests/AllocationProbe.cs`、codec/适配源码见`source/`，identity保留源码SHA/位置；**它只证明该宿主下的托管分配，不证明Unity零分配或原生零分配**，其计时不参与Player排名。

**成本归因、工作集与长期成本**

- **库内部**：官方netstandard2.0的`GCMBlockCipher.ProcessBytes`循环进入`DecryptBlocks2`，原始源码1718～1720每32B处理创建16B数组，另有Init/DoFinal状态及小数组。BC源码热点、辅助宿主分配与Player资产Read/GC的同步大幅变化相互支持这是主要可消除成本；没有宣称Profiler已给出精确AES-only/GC暂停占比，没有修改该实现。
- **原型适配/Stream**：原C1适配每段向`gcm.Init`再次传入同一非null key，触发库的key schedule重建，**这一部分是适配调用策略带来的成本，不能全部记为BC不可避免的内部成本**；本轮未单独实测BC的key复用调整，不把C1适配称为BC最优实现。该调整也不会移除当前官方netstandard2.0的按块数组分支，因此不自动追加另一轮优化实验。私有认证槽、两级包装锁、seek/read、缓存复制与小型账本保留，明文Stream约4.144ms与CNG约4.422ms的Read量级说明原型/IO/bridge仍有成本。OpenAuth约6.490ms仍含HKDF、provider/key创建、首末认证和封送，不是纯AES CPU。残余GC11尚未逐函数归因，不能全推给Unity或声称CNG热路径在Mono零分配。
- **SHA/业务消费**：CNG前置SHA整图约87.279ms，其中单包SHA中位数约60.111ms，资产后明文摘要验证约44.964ms，两项均未省略。CNG与File/明文Stream仍共同观察到约62ms最大帧，和同步SHA时长接近；这是仍需G1承接的主线程工作/调度成本，不因换GCM消失。资产返回、TextAsset.bytes复制、Prefab/Unity反序列化与await帧调度也仍存在；没有把所有残余峰值归因密码库，亦未做逐帧Profiler确定性归因。
- **工作集/多包**：两轮反序交错BC/CNG，旧Scene+UI/Atlas/Audio/Prefab持有时加载第二Scene，再保持64bulk，共70输入。显式数据数组峰值双方仍`13760320B / 13.123MiB`；bulk消费BC`361.747/363.164ms`，CNG`287.196/294.159ms`；GC0为265/265与81/81。CNG工作集采样峰值`215003136..216133632B`、BC`216338432..216702976B`；这不是稳定总体内存优势或累计分配比较。
- **Audio**：每后端两轮24秒持续播放均isPlaying、延迟Read有效，各轮延迟消费`1015808B`；认证段数均189，输入活到停止/卸载后，CNG活动owner最终0。没有可靠underrun计数或听音验收，不写成无可闻毛刺。
- **没有隐藏原生成本**：每输入数据数组仍`196576B`，另有CNG算法provider/key、两个SafeHandle及原生key-object。实测provider公布的`ObjectLength=3262B/key`，5输入`16310B`、70输入`228340B`，**不含provider/allocator等内部开销，亦不在旧显式数据预算内**。CNG常规PrivateBytes中位数还略高于BC，不能仅凭GC减少宣布总内存下降。外部计数为目标50ms间隔采样，包含引擎/资源/托管/native，不是精确瞬时峰值或密码模块独占成本。
- **维护/部署**：只测试本机Windows x64 Mono，不外推IL2CPP/ARM64/其他OS。CNG自动key-object API要求Windows7+，但正式部署应以仍获安全维护的Windows为策略，不把历史API可用下限等同安全支持下限。调用系统DLL，不随包复制或锁死本机DLL版本，OS升级承担密码核心维护；我方仍维护P/Invoke、ABI、状态和owner回归。每输入开provider而非新建全局缓存，打开成本已包含测量，不提前建通用密码插件平台。
- **许可**：CNG是Windows系统组件，受适用Windows许可约束，不是MIT可再分发库；本方案无需再分发系统DLL。BC仍须随包保留MIT，既有Tink framing的Apache-2.0义务不变，两个Player均携带原适用许可。未来若复制.NET源码则另保留其MIT声明；本次未复制。使用CNG不使整个E04/BC HKDF方案自动获得FIPS认证。

**保护、审核与停止点**

- 本轮正常认证/native运行只打开B1独立cipher目录的`.enc`；运行前安装外部watcher持续至退出，35轮事件Error总数0、最终变化文件完整rawSHA匹配0，结合源码确认解密只用私有内存槽。codec写入限自有测试密文；事件/最终SHA仍不能排除瞬时、部分、包装明文、未观测路径或OS分页/dump，不扩大为全系统无明文保证。
- **建议进入G1实施审核，而不是自动实施**。需用户确认的少数取舍为：正式采用“Windows CNG AES-GCM + 官方BC2.7.0 HKDF”及Windows64 Mono平台范围；继续明确保留Scene native延迟读失败风险，并接受/制定同步SHA与业务消费的性能门限；确认128MiB是否作为显式数据缓冲预算初值、同时明确额外native owner成本及并发上限。64KiB/2slot仍是有实证的设计基线，不声称最优；None+File继续保留，其他平台不无声fallback或改临时明文。
- 若不接受Windows系统后端依赖而继续官方BC，本轮C1适配下实测代价就是约124ms资产消费、约131ms managed Read与114次GC0的代表负载成本，不能以“已可行”掩盖，也不能外推为所有BC调用策略的性能下限；本轮不强行继续寻找其他库。无生产业务门限，**不宣布生产性能达标**。
- Scene已知崩溃、原历史no-go/清理未知全部保留，未重现、未修复、不计Passed；正式ResourceSystem/Lease/keyring/发布/Prepare集成、其他压缩/所有资产类型、真实游戏工作集、可靠累计Player/native分配、GC pause、音频欠载、跨平台均未由B1冒充验收。CNG原生Destroy/Close失败本轮未注入，释放失败计数0只表示未观察到，不能当作该异常可重试恢复的证明。本轮不重跑阶段8测试、不推进8.10或G1～G4。
- 归档源码/完整Player/结果已独立保存；预回写核对966项旧证据零变化、158项主工作区基线仅本Plan变化、无新增状态路径、暂存为空，原工程Unity仍PID14272且未操作，B1 Unity/Player均已退出。最终保护记录见`summary.json`、`evidence/protection-final.json`；新产物清单在`artifact-hashes.json`。
- **定向独立只读复核已完成**：同一复核先检查新适配/实验程序，再以最终原始结果复算本节数值与结论，未发现未解决Critical/Important或必须修正的归因/推荐问题。已确认ABI、认证前不交付、owner、两版源码身份、35次运行终态、五组一致收益及平台/计数限制；唯一Minor为辅助程序源码归档位置文案，已据实际文件修正，不影响技术证据。未循环重审历史、未重跑实验；复核记录见B1 `evidence/independent-review.json`，最终保护检查以`evidence/protection-final.json`实际结果为准。**本轮结果到此交付待审核，无Git写操作，不自动进入下一阶段。**

#### 28.10.19 G1～G3 夜间连续正式实施（2026-10-16 21:03 UTC 授权）

**当前有效授权及冻结选择**

- 用户已审核G0/C1/B1，本轮允许YL Resource产品、直接必要协作代码、测试/自有夹具/meta、批准BC插件/许可/精确程序集门禁，以及本Plan/Resource.md增量维护。G1必要门禁通过后直接G2，G2通过直接G3，最后完成必要验证、一次独立技术Review及修复后统一报告；最终用户验收不作为批次间等待点。
- 正式后端固定为Windows CNG `Microsoft Primitive Provider` AES-256-GCM + 官方 `BouncyCastle.Cryptography 2.7.0` netstandard2.0 HKDF-SHA256。不复制系统DLL，不锁死B1机器系统版本，不改密码核心，不静默软件GCM fallback。平台专用内部隔离，其他环境加密明确Unsupported，None及其他模块不受阻。
- 保持Catalog v1；YL文件内有界canonical envelope + Tink RAW分段格式；Project默认None，启用覆盖全部Bundle及二进制root manifest。采用64KiB段、两个明文槽、32KiB native读取buffer，Runtime默认128MiB可启动配置的显式数据预算，不预分配整笔、不自动扩容/驱逐。CNG provider/key/句柄及可计量native成本分列，不称整个密码模块/进程硬上限。
- F1依赖闭包隔离、F2新增闭包原子预留/共享不双计/不足明确失败后显式重试、F3当前选定key集合初始化前轻量认证，按28.10.14落实到正式原图。异步纯IO/完整SHA/认证可取消并离开主线程；native开始后观察至安全收尾，原producer、输入稳定性及退出责任保留。
- Scene native接手后延迟Stream读失败可能崩溃这一限定风险继续接受，不重演、不算修复/Passed，不豁免合法输入崩溃、定位/竞争/提前释放、取消/退出/预算缺陷。native前完整密文length/SHA、首末认证及读取时每段先认证不削减；发布/Prepare/激活/回滚仍全量认证。
- 主工程唯一Unity会话允许编译、导入及自有测试；不启动同工程第二实例。用户dirty场景不保存、关闭或覆盖；需要Player时用独立owned载体且运行本轮正式源码。原XF/XFAB/XFFSM、游戏/MainModule、原Scene/Prefab/Canvas、历史Bundle/StreamingAssets、Packages/ProjectSettings/AGENTS、工具权限配置、.gitignore/.kilo/kilo.jsonc、ProBuilder及并行UI成果均保护。
- **Git纠正**：8.9整体完成并通过用户验收后，统一一个Commit并Push。此前“阶段8剩余成果最后统一Git收口”及默认每小批次Commit均被本次决定取代，历史记录保留。本轮不暂存、Commit、Push、切分支、Merge、Tag、Reset/Clean/Stash/Rebase/Force Push。G3完成不等于8.9、E04/G4或阶段8完成。
- 不重开总体Plan/海选后端/31项审计，不进入G4完整交付矩阵、整体Workbench、8.10或其他专题；只在授权内无法解决的真实阻塞时提前停止，普通细节自主裁定并简记依据，不交互询问。

**现场与可恢复检查点**

- 首次现场：`fusion/8-resource` / HEAD `ddf154cf2c72cd0b403cdd1547eee8ae06963547`，暂存为空；已有dirty/未跟踪成果按原状保留。实际进程核对主工程Unity `14272` / `2022.3.62f3c1`，不是凭历史PID判断。场景/编译状态另以实时Editor读取为准。
- 自有正式验证根：`C:/Users/Kano/AppData/Local/Temp/kilo/e04-product-20261016-2103/owner.json`。`evidence/baseline.json`记录160个当时变动路径及SHA，`baseline-protected.json`登记保护范围；捕获期间路径集合变化，`statusPathSetStable=false`，不冒称实施前原子快照。唯一主工程实时状态为Main场景已保存、非Play/编译/导入；不读取或传播进程命令行中的凭据。
- **G1必要门禁已通过，自动进入G2**：正式BC2.7.0 DLL/完整许可/来源/meta、YLRENC01严格封装/Tink RAW/Windows CNG、认证Stream/窄输入聚合、配置透传、4类错误、全主体预算/预留、可取消SHA与锁外key捕获/认证验证基础已实现。主Unity首轮内容19/19，新增NIST/通知/输入责任等后29 Passed + 1 Skipped（当前Windows平台不适用的unsupported编译配置断言，非失败也不计Passed）；配置预算及key边界13/13；Framework/Audio/C05直接Editor与Windows Development编译闭包/BC精确白名单负例12/12。发现树先1809后1852，均不计运行叶；重复运行不累加为独立测试数。Java新YL context官方夹具已由正式内容源码成功消费，不冒充旧G0 domain互通。
- RED与修复事实：主Unity最初配置/错误5叶RED；合同同断言动态NUnit RED；G2新32叶已实际0/32 RED（缺正式字段/API）。中间G2测试使用本Unity不支持`NonParallelizable`及尚未写完helper造成编译阻塞，前者移除，后者补齐；Audio必要源闭包加入Content/Updates/Diagnostics、精确BC与实际unsafe后编译门禁已通过。辅助新`.csproj`权限拒绝未绕过，G1改用正常Roslyn编译的自有辅助宿主，其结果与主Unity分列。
- G2正式产品接入已开始：raw/delivery分离、Catalog封口前编码、当前key全量认证后确切密文复用、原Publisher stage全量AEAD/native每包独立输入、原签发payload不改而显式渠道能力确认。**重复发布不能因已有digest早退而免除本次key/完整内容验证**，以既定当前key验证合同优先；None保持原指纹及路径。G3仅完成接点研究与缺能力测试准备，产品接入待G2必要门禁。独立Review、正式Windows64 Mono Player及有限写入观察待执行，G1完整技术结论仍受后续集成Review与Player验证约束。
- **21:33 UTC可恢复现场**：G2完整产品源码已落盘，32叶真实Unity门禁的**同一原运行仍在执行**。MCP客户端`Request timed out`不是产品失败/成功，已核对唯一Unity14272仍存活/Responding；回执根`e04-product-20261016-2103/evidence/test-results/g2-first/`包含armed/started及首叶Passed（`CancellationAfterTheNativeBuildCannotSealACompleteEncryptedWorkspace`，48.751s）。不要重启/重复提交测试；先取得该根finished.json或原运行终态。中间新Runtime消费者测试编写时错误使用旧API名字导致编译阻塞，已按真实`CheckUpdatesAsync/ActivatePreparedUpdateAsync/GetActiveCatalogDigest`、`ResourceLoadMode.Bundles`修正；未用新增别名/兼容壳掩盖。
- G3目前仅准备9叶`ResourceContentRuntimeTests(.Consumers)`及4叶`ResourceContentUpdateTests`，后者已有主Unity1 Passed/3预期RED（未知编码仍能Prepared）；尚未宣称G3功能接入。Runtime夹具计划走真实G2 Builder/Publisher两版本、启动/Scope/Core/Prefab/Atlas/Sprite/Audio尾轨/Scene/Group、共享取消、F1/F2/F3、事务/关闭，非实验loader。源产品在G2门禁运行时保持稳定，不并发操作Unity。
- 一次独立技术Review已分段启动（内部只读任务`ses_eb95ca05fffetDZUs0IJxR2QU0`），先审G1/G2，G3接入后由同一Reviewer补审新增部分，不重复全面审查。正式Player载体/runner由原验证子任务在owned temp/carrier-sources准备中，尚未复制/构建/运行；不把这些准备计为Player通过。
- **21:40 UTC独立Review第一段已返回，非无保留通过**：未发现Critical，确认2项Important待修复。其一`ResourcePublisher.VerifyNative`只查当前包及末尾root，较早shared依赖可能在后续A资产物化时才锁存Fault而漏检；需成功返回/释放前后检查全部独立输入并补非Scene的延迟通知回归。其二`ResourceContentValidation.Dispose`过早置disposed，首FileStream关闭异常会跳过后续输入/预算且显式重试no-op；Capture双失败还需保留首因及未释放owner。均为静态调用链发现，Reviewer未实际注入，不计RED或修复。另1项Minor为BuildView报告raw/delivery字节和SHA缺少区分，拟局部标注既有字段而不重做布局。原G2运行未终止，不在其native测试中途修改产品脚本；终态后先复现再修复/回归。历史Scene风险豁免不覆盖这两项。
- **22:00 UTC原运行回收**：`g2-first`已有27个逐叶回执，未见`Status=Failed`，仍无finished.json，唯一Unity14272存活/Responding且CPU累计增加。只报告已完成叶，不把未完成5叶或发现树当通过；合理定时等同一运行，不重复测试。验证子任务已完成owned载体生成/Build/runner/写入观察脚本及5源码辅助编译、10脚本语法/5拒绝保护自测，全部不等于正式Unity载体或Player已运行。设施根README类重复文档未新增；执行入口见该根`Invoke-ResourceE04Editor.ps1`等脚本。
- 保护中间证据`runner-ready-protection.json`严格门禁为false：12048个原有保护文件仍0修改/0删除，新增24路径来自正在运行的G2 fixture/workspace；随后部分路径已由该原测试自行收尾，不能据此声称全部24已消失。保留原始证据，终态后按测试归属及最终快照核对，不清理未知产物、不放宽保护断言。
- **2026-10-17 02:42 UTC恢复纠正**：用户指出未继续执行；实际`g2-first/finished.json`已在10-16 22:07:49记录32 Passed/0 Failed，但上一轮等待后没有推进到G3。定时工具曾安排等待，不等于后台实际工作；此段中断原因未确认，不能将中断期间记为实施/验证。02:42回收原终态后恢复连续实施，不重复32叶，不执行Git写操作。
- **G2审查修复已实际通过**：验证清理失败/重试及较早shared输入fault被Release漏检的2个新回归先主Unity7 Passed/2 Failed RED，再修复；整合内容套件44 Passed/1不适用Skipped，随后真实`EncryptedPublicationContainsOnlyDeliveryV1FilesAndSupportsIndependentNativeInputs`单叶通过（63.360s，MCP timeout后取同一运行testResults，不重启）。BuildView仅局部分列raw/delivery身份与现成成本，旧Minor已修。G2必要门禁通过，自动进入G3。
- **G3产品接入已落盘、技术验收中**：主主体初始化Store Mount/SHA worker→main配置/类型/平台与key捕获→worker F3轻量认证；同配置join/原producer/Request/Scope/Lease/Bundle/实例/Scene/直接Core与Audio/原PlayerLoop清理图接入，F1首fault有界通知、F2全live输入+新增闭包原子预留、native卸载后关闭输入及归还预算。新增只读成本快照与原Report.Content分列CNG/bridge。Snapshot纯IO重开显式传UnityCompatibility与取消，保留BuiltIn wrapper验证。Updates以原Store一次性收据绑定To/From/owner/epoch，Prepare/Activate/Rollback提交前全量认证并持柄；Check/E05/恢复不取key，远端Player硬门禁不变。初始local/remote安装旧同步IO热点正在补worker化，不将其说成已经满足异步要求。
- 主Unity当前内容+预算/key+更新17叶套件已62 Passed/1不适用Skipped。新增Audio缓存F1缺口先1叶RED（缓存仍交付）再修复窄internal admission callback，保留公开AudioClipLease构造及旧voice；额外已确认length mismatch仍先整包SHA的回归先1叶RED（ReadCalls=2）再修复为先length拒绝，Validation 11/11回归。直接编译闭包与兼容组57叶35 Passed/22 Failed：失败全部为2个旧synthetic fixture的未知编码（Query fixture21叶、BuiltIn shell准备1叶），并非22种缺陷；三编译闭包均0失败。已保留失败输出并安排只修synthetic二进制UnityFS前缀，未放宽严格解析。
- **G3独立Review进行中、尚未无保留通过**：新增Important待直接复现/修复：应用provider重入cancel+retry时旧Bundle admission可误删新代节点；Group/Preload父批次handoff漏复查此前成功子项的晚发fault；公开Audio provider terminal progress用户回调后漏末次准入；ContinueInitialization将原None配置InvalidConfiguration误分LoadFailed。均未归入Scene豁免，已准备正式owned native回归（Runtime fixture目前14叶），修复前不标G3技术完成。Review沿用同一Reviewer，仅补G3及关联修复，不重复旧G0/B1全面审查。
- **05:10 UTC当前有效检查点，Editor技术门禁已收齐、Player待验证**：同一独立Reviewer最后补审未发现新增Critical/Important，原G1/G2的2项及G3的5项Important均已按代码与定向回归闭环。G3五项分组包含：provider admission在途+代次身份移除；旧零owner IO取消后新owner不能继承取消（旧IO实际结束再用同ticket回到reserved重启）；Batch/Preload末次准入与optional释放回调后有限复查；Audio末次progress回调后复查；None配置/挂载错误保持InvalidConfiguration。未开启第二个全面Review。
- Native正式主链`g3-green` 15/15 Passed（04:26:25 UTC），覆盖两版真实Builder/Publisher发布、C05启动、共享/取消/重入/初始化退出、F1/F2/F3、Core/Prefab/实例/Atlas/Sprite/Audio尾轨、合法Additive Scene及SceneLease归还后输入保活、Group与本地Prepare/Activate/Rollback。新增真实回环签名HTTPS下载链`g3-remote-final` 1/1 Passed（05:10:45 UTC），证明Check不取内容key、下载后的缺key不Prepared/不改active、补key显式重试不重下载、激活消费及回滚；仅信任该回环证书对象，不改系统信任，测试进程凭据按原值恢复且断言不输出值。原直读临时PFX的首次`g3-remote`也通过，后者仅因测试凭据可复用合同调整而定向重验，不累加为第二个独立功能叶。
- 最终专属Updates30/30 Passed（`g3-updates`），包括本地/remote Check及安装/复用/parts/stage的可取消纯IO、只读guard无root/lock写、完整To/稳定性/receipt/取消责任。Simulation/C05配置/Request必要兼容15/15 Passed。最终直接影响组合`g3-finalunits`已135叶终态＝134 Passed、0 Failed、1 Skipped（本Windows host不适用的unsupported编译配置专项）；CompleteEvidence=true，通用收集器严格PassedGate=false仅因该明确不适用叶，不改成Passed或隐藏。原unsupported独立编译配置5个通过/25个加密专项不适用证据继续分列，不扩本轮平台。三直接编译闭包、BC精确白名单/负例、旧plain Store/BuiltIn壳与查询本次均无失败；此前22个synthetic不合前缀叶修fixture后已全部通过。
- Native夹具本身的失败另记而非产品缺陷：最初Mixed重复选择触发原Map拒绝；改为PerAsset且不重复选择。旧legacy Atlas直接打包已选择sheet导致readback失败，改复用成熟v2 authoring及独立Atlas source。若干UnityTest因跨EnterPlayMode的编译器闭包丢失NRE，改现有约定的EnterPlay wrapper→独立body；缓存根过长/把父目录当Store的两个夹具问题改短根与s/t分离，未放松产品保护。zero-owner IO barrier最初ThreadPool.SetMaxThreads(1)被Mono拒绝，不算产品RED；后用自有8MiB随机payload、实测InputWork未完成→Cancel→确认OCE退出→不先tick再fresh join取得真正RED，修后在15/15内通过，不再修改ThreadPool。
- 为避免MCP两分钟传输timeout再次悬挂，主会话采用已安装`unity-mcp-cli`的600000ms原接口和tracked monitor，配合原逐叶collector；未安装/升级CLI或改工具权限。`Wait-ResourceE04Run.ps1`仅事件驱动回收同一已登记运行，不启动/重复测试。测试发现树（例如1898）与实际叶分列；原leaf内NUnit开始时钟存在偏移，不作wallclock依据，使用Root回执UTC与runner Duration，不改写原记录。
- **正式Player载体仍进行中，不宣称通过**：g31 fresh空untitled场景的Additive限制已在owned模板修；g32长根manifest272字、g33预批准短根侧车265字均触Mono MAX_PATH，native文件实际存在，未当缺包或加密失败，未改产品/系统长路径设置、不用扩展前缀或映射盘。进一步明确owned第三候选为主repo `Temp/<3字符Name>` 的独立项目（不同Assets/Library/ProjectSettings及projectPath，非第二主工程实例），g34根34字+known raw侧车221＝255已越过此边界并完成首个None正式Build/Publish；随后模板局部SO引用被native清理，改每mode从持久Project.asset重取。g31～g34失败原件/identity/日志保留，验证子任务正在全新g35捕获并真实Fixture/Mono Build。它仅操独立载体，Main14272仍由母会话独占且不关闭，不触Temp其他项/原资产。
- **05:24 UTC Player实际失败检查点**：g35只因文本`.manifest`嵌入各自workspace路径导致“所有raw原字节一致”的夹具断言不适用，已在owned模板仅归一各自确切workspace前缀后比较旁车，其余内容完全一致；二进制raw 10包含root manifest仍逐字节/SHA严格相等。g36=`Temp/g36`已真实生成None/Encrypted×v1/v2四份发布、Mono2x Build成功/0 errors，1038条XML/成员隐藏等编译警告完整保留，未逐条认定全部为既有；1348正式文件及BC精确SHA身份保持。证据外存`evidence/carrier-g36`。主会话真实执行`runs/g36-01`，process exit1/non-timeout，176预期Check仅执行172，其中`update.rollback.terminal`失败detail=UpdateFailed，后4未执行；整体gate=false。None/Encrypted功能与两组性能阶段已运行不等于整个Player通过。watcher0错误、0 UnityFS事件、143内容探测miss，只是该有限范围观察，不作全进程/所有瞬时文件无明文证明。
- g36失败不自动归Scene风险，母会话已委派原验证子任务只诊断自有runner/cache、必要新capture并真实重跑。旧runner的`IEnumerator Update(ResourceSystem)`命名碰Unity消息（日志`Update() can not be a coroutine`）将更名；Wait只记Kind丢失因果，需在新诊断保留安全完整cause。不得修改g36冻结副本或跳过Rollback。若确为产品缺陷交母会话源修复/回归/重新冻结；当前真实原因未确认，不把问题归为已修。新载体g37或下一唯一短名由同一子任务独占，母会话不并发Player。
- g36已完成的16MiB代表性阶段仅作**失败运行的部分证据**：5样本median None total102.6413ms、输入准备+native97.8575、asset4.2989；Encrypted total112.0021、准备100.4775、asset11.322。测量含正式SHA/帧调度/IO/native，不等同B1纯输入约14ms。单个held encrypted input196576B、native读取声明32768B、CNG provider1/key1/key-object3262B，观察峰589728B。所有main-thread alloc读数0可疑，已要求known allocation+KeepAlive能力探针，不增加则改null并说明不可用，绝不宣称零分配；process managed-heap delta不是分配数。最终Player证据仍须完整重验通过。
- **现场收尾已实际预验**：`git diff --check` exit0（仅2个原有文件CRLF转换warning，未改它们），HEAD仍`ddf154cf2c72cd0b403cdd1547eee8ae06963547`、暂存为空。32个新增Content/Plugin/加载基础/诊断/测试target的meta齐全、GUID在Assets内唯一，BC SHA精确，`evidence/metas-final.json`通过。保护prefinal-1显示12048原文件0改0删/24新增；24全来自g3-setupfix失败未登记workspace，经marker的确切ProjectId/Guid/keyId、失败15叶消息、全文件集合和baseline不存在交叉证明，保留marker/旁车/24项SHA后仅删除该唯一owned root（`evidence/g3-failed-workspace/inventory.json`），未清未知root。实际main native Bundle/CNG provider/key均0，非Play非编译。最终保护复验与Player后现场状态仍待收齐。
- **当前最终技术结论（05:50 UTC）**：G1/G2/G3实现及本轮必要门禁已通过，待用户验收，不自动展开G4/8.10。最终g37独立载体运行本轮正式产品代码，Unity2022.3.62f3c1/Win64/Mono2x、0编译错误、BC官方SHA一致。`g37-diag`、`g37-ck01`、`g37-ck02`三个独立进程/全新cache均完整176/176唯一Check、六阶段完成、exit0/non-timeout、failure/cleanupFailure=null；这是176种断言重复3次，不说528个独立测试。主代码未因g36/g37载体诊断而更改。g36首个Rollback UpdateFailed具体原因仍未知，缺完整cause不能补造；新runner保留完整安全cause并去掉Unity消息命名冲突后三次未复现，不声称已证明某个模板变化修复该次错误，也不套Scene豁免。
- g37正式Player测量为每mode固定1 warmup+5测量，diagnostic run中位数None准备/native混合97.640ms、物化4.235ms、total101.874ms；Encrypted混合98.452ms、物化10.983ms、total109.482ms。整体计时包含正式SHA/调度/IO/native，不等于B1纯输入14ms，不把样本变硬帧门限。已知4096B分配能力探针before=after=0，故MainThreadAllocatedBytes均null，明确未能测分配计数；heapDelta仅进程managed差值。CNG/native成本与DataBuffer分列，完整后三次数据以`final-player-summary.json`及各run原件为准，不把旧g36的可疑0当证据。
- 三次Player有限观察均watcherErrors=0、观测UnityFS写事件=0，内容探测miss分别132/139/137；观察从进程启动前到退出、范围仅Player/发布/本run/cache/tmp，自有源码IO图仅静态互证，不覆盖所有用户目录/Unity内部/native/GPU/全进程，也不能精确PID归因或排除瞬时/锁定文件。不能由最终文件不存在推无明文。Scene native接手后坏Stream风险仍未修复、未复现、不计受控通过；合法Scene三次成功只证明正常链。
- 最终严格保护`evidence/final-protection.json`通过：12048原保护文件0新增/0删除/0内容改变/0不完整，所有自有PS脚本语法0错误；baseline原始非原子限制保留。`evidence/final-unity-state.json`实际main PID14272、Main场景`Assets/Scenes/Main.unity`已加载且dirty=false、非Play/编译/导入、compilationFailed=false、native Bundle/CNG key/provider/releaseFailures均0，draining/contentCleanup主体均0、Current不可用（测试会话已归还）。最终Git仅做只读核对，无暂存/Commit/Push或其他Git写。
- **06:10 UTC最终归档与收尾完成**：`evidence/final-player-summary.json`实际落盘并交叉核对三次176同组ID、进程终态、分项/计数器能力和成本。`g37-ck02`最终run的5次median为None准备97.708ms/物化3.975ms/总101.700ms，Encrypted准备97.547ms/物化10.760ms/总108.177ms；三次总median范围None101.700～103.352ms、Encrypted108.177～109.583ms，均为混合异步窗口，不宣称固定帧率或精确纯解密成本。CNG key-object3262B为本机provider查询值，不锁定OS DLL版本，也不当全密码模块成本。
- 三次完整Player证据外存`evidence/carrier-g37/g37-diag`、`g37-ck01`、`g37-ck02`；总汇总保留g36旧失败且明确未归因。7个本轮载体按owner/name/canonical/sourceidentity/无reparse严格核验，只删除18项明确可再生Library/Temp及成功run cache/tmp，逻辑文件长度合计593178603B（非物理磁盘释放量），0失败；预删/结果分别`evidence/owned-cache-cleanup-plan.json`、`owned-cache-cleanup-result.json`。源码/meta/日志/receipts/四份发布/Player仍保留，清后身份复验无变化，g36失败cache与全部G0/C1/B1原样保留，未知根/主Library/Temp父/TLS凭据未触。g37现可执行入口`G:/Unity/Projects/CGJ2024/Temp/g37/Player/E04Formal.exe`，位于主Temp，主Unity退出可能清掉；必要核心证据已外存，不把归档当永久存在的可执行文件。
- 文件范围：本轮增量集中于YL Runtime Resource的Content、Loading/原Bundle/批次/初始化、Snapshot/BuiltIn、Instances/Scenes/Sources、Updates/Parts/Transfer及Diagnostics；直接必要AudioClipLease/AudioClipCache/ResourceAudioClipProvider协作；Editor ResourceProject、原Builder/Workspace/CatalogBuilder、Publisher/Signer/签发入口/Operations与局部模式/KeyId/raw-delivery报告。另为Runtime asmdef必要unsafe/批准BC引用、精确BC/许可/source/meta、新ResourceContent测试及直接闭包/相关synthetic fixture、现有Resource.md/本Plan。已有8.9/C05/E05/C02/B06和并行UI改动不归本轮接管；`.gitignore`、工具配置、AGENTS、Packages/ProjectSettings等dirty保持baseline内容未改。
- 本轮新验收不重算原型：正式Unity G2 32/32、最终内容/Store/闭包组合134 Passed+1平台NA、直接Simulation/config/request15/15、正式Native16（15+TLS单叶）、Windows64 Mono同组176×3均分别报告；重复/发现树不累加独立数量。独立Review原2+5 Important修复闭环、无新增未解重要发现；未验证范围为真实BCrypt销毁/关闭系统失败注入、其他平台/IL2CPP/其他Unity版本、全系统IO/完整进程内存与完整交付矩阵，留后续授权，不伪造通过。
- **停止点**：本轮G1～G3技术完成，统一交付待用户验收；不自动进入G4、Workbench整体优化、8.10、阶段9或其他专题。最后改动仅Plan/Resource.md技术状态与限制说明，不使产品编译/运行证据失效。Git仍为8.9整体完成并验收后统一一次Commit/Push，本轮始终无Git写操作。

#### 28.10.20 G4 正式交付与定向回归（2026-10-17 06:58 UTC 授权）

- 用户确认G1～G3技术验收通过，保留已记平台、安全与证据限制；授权G4范围内必要产品修复、测试、样例、实际导出及文档增量维护。不重新实施G1～G3、不重开总体Plan、不自动验收C05或8.9整体。Git仍待8.9整体完成并用户验收后统一一次Commit/Push，本轮不做任何Git写操作。
- 现场已核对预期分支/完整HEAD一致；主工程唯一Unity PID14272，Main场景dirty=false，非Play/编译。起始232个dirty/未跟踪路径按内容登记，证据沿用自有根`e04-product-20261016-2103/evidence/g4-baseline*.json`，path集合稳定不等于原子内容快照；旧失败现场及保护范围不接管。

| G4要求 | 沿用的有效证据 | 真正缺口与本轮执行 |
|---|---|---|
| 密文远端与续传 | G3正式签名回环HTTPS、缺key不Prepared/补key不重下载；8.8通用HTTP边界 | 固定正式密文真实partial、强ETag/If-Range精确offset、重启/损坏拒绝及原Prepare/激活；Player远端gate保持关闭 |
| 旧发布、轮换/回滚、渠道 | G2 None身份/密文复用/轮换与签发能力门禁；G3正式事务及176同组Player×3 | 查有来源的E04之前自有v1明文发布直接消费；新旧key装配与明文LKG冒烟，不把新None冒充历史 |
| C02/E05 | 已验收部署/查询合同，G3修正synthetic前缀后兼容组通过 | 正式密文无key部署/真实导入/严格定位/应用key启动；无key查询实际密文字节及零写，保护原有部署/父meta |
| 实际导出与样例 | 正式源码/二进制闭包、BC精确身份与许可；原ResourceStartup双模式样例 | 工程外owned目录实际发布导出和框架导出/解包核对；补样例加密及key用法并实际运行，不冒充阶段9独立验收 |
| 上批遗留 | g36失败原件、g37安全cause及三次176；G3性能/有限写观察 | 最小补from/to/阶段/取消与持有/失败后身份；旧cause不可恢复则保留未知；按实际日志分类1038警告与内容探测miss |

- **最终结论（2026-10-17 08:25 UTC）**：**G4技术完成，E04具备整体验收条件，待用户验收。** 仅一次独立Review，问题及新增夹具收尾缺陷已修复并直接回归；没有未解决的重要G4产品问题。后端/分段/预算/协议/F1～F3、Scene限定风险及真实BCrypt系统释放失败未测范围保持。未改加载/调度/预算热路径，沿用G3正式性能，不重跑B1或以新缓存冒充冷盘。

**证据与正式产物**

- 本节 `B` 为 `C:/Users/Kano/AppData/Local/Temp/kilo/e04-product-20261016-2103`。原G0/C1/B1、G1～G3原始证据、g36失败/cache及旧r87制品不覆盖。新增结果在 `B/evidence/test-results/g4-*`；产品与交付字节终验为 `g4-delivery-final.json`，Unity终态为 `g4-final-unity.json`，本轮保护基线/终态及比较为 `g4-baseline*`、`g4-final*`、`g4-scope-final.json`。
- 正式 `ExportRelease` 的三份闭合集为 `C:/Users/Kano/AppData/Local/Temp/kilo/e4g4-b61ae750/` 的确切digest子目录：None=`483cf361780069498607a52a12da5d476fe388cc435d8d1f6649680171d68c66`；key1密文=`0c4629082a46f6ecad3e93baa59dbbf9386bdd2fdd02e42b05cf5eb4db63e3e8`；key2密文=`456986b9f9fb07993b35412460b1dc97dd5a60b67dc89b8e1e9b0c0f3a13ccd7`。三者均为本轮新制品；None不冒充历史证据。项目为 `yl-release-f6b6712b5e2841deb0aa450c6ab3a456` / StandaloneWindows64 / app-v1，每份严格6个登记文件，Catalog/Complete/交付长度与SHA复算一致；完整每文件SHA见终验JSON。旁边Store缓存目录是取证载体，不属于交付快照。
- 正式框架包：`C:/Users/Kano/AppData/Local/Temp/kilo/YLFramework.ExportTests-0db5cc2040da4767adc6906a771a73e2/framework.unitypackage`，7423155 bytes，SHA256=`14fd137328317a035013dda2fa188e79be5146be74e7e32e8d4a4459328d6120`。使用原导出器新增的显式目标API，不复制导出清单另建第二规则；六目录+根LICENSE及Recurse不变。实际包已解开逐档校验pathname/GUID/meta/asset SHA，固定BC DLL/BC许可/SOURCE/Tink许可sentinel及系统DLL/秘密文件排除通过。保留既有商业插件，不宽放闭包白名单；公开再分发许可限制及阶段9干净工程完整验收仍保留。

**A/B：密文远端、历史兼容及事务**

- 以本轮Builder/Publisher固定密文走原签名、ResourceRemoteSource、ArtifactStore和CNG认证。最新续传叶实际记录cipher length=351921、SHA=`a05c0140fa4bf3d2a1bf9db343ed9959ae3315641ad59feda857959519d06fb8`：正常206在32768字节partial后真实Shutdown，按同路径重建Store/ResourceSystem、重新Check，再发 `Range: bytes=32768-` + `If-Range: "g4-fixed-cipher"`；没有从零下载冒充续传。持久metadata的长度/SHA/ETag、partial字节前缀与同一正式cipher一致，最终进入内容认证、Prepare、激活及TextAsset读取。这是Editor会话重建，不外推Player进程重启。
- 同一密文定向覆盖忽略Range的200（清空后写完整实体）、强ETag变化（第一次offset=16384，第二次精确无Range/If-Range的`full:`）、损坏尾部（清空不可复用完整part）、404错误（无Prepared/active变化）。缺key/错key分别映射正式内容错误，补key后的Prepare不再请求payload；失败不改变原active/LKG/原序号合同。通用HTTP组合沿用8.8/G3，不穷举网络矩阵。原签发入口未确认E04渠道能力时拒绝密文，确认后同原签名payload成功。Windows Player远端gate未改。
- 历史直接消费输入为 `C:/Users/Kano/AppData/Local/Temp/kilo/r87-bdbc648a/yl-release-ef5f7112de994b238bc4ff8239b7e258/StandaloneWindows64/versions/453ebfdae1effb027574fa45473a3780669e81bad64af6f89f27d097fdcc9760`。身份来自r87 owner/Complete（ProjectGuid=`64b81f4b7f3ea07438164f8dd2f9d087`）及 `Resource87-20261015-evidence` 的原29项测试/fixture记录，早于E04正式实施；不是MainModule。原Publisher目录已被旧测试清理，保留下来的是原ArtifactStore version，而非伪造Publisher来源。当前正式 `OpenOwned`→CreateCatalog→ResourceStartup直接读到`first release content`，输入是FileStream/非加密，provider调用0，目录全部SHA前后一致；没有重建、删除版本记录或改写制品。
- key1→key2新发布Check/Prepare/激活、缺旧key回滚拒绝、保留旧key显式回滚成功、密文→兼容None明文LKG成功；当前新None仅验证事务对照，历史兼容另由上一条证明。None/File、Simulation身份与正式样例双模式沿用且直接回归，不新增兼容加载器、key服务或keyring权威。
- g36仍为真实未知原因事件。G4只补同一路径最小状态：明确From=`456986...`/To=`0c462...`，持有租约Busy、显式取消、受控`JournalVerified` IO失败（未到CommitDecided）、Recovered后内存/磁盘active仍是From、LKG仍是To、无残留journal/next、From内容仍可取得；去掉故障后正常回滚成功。新cause为受控IO，不冒充g36原因；现有消息命名调整和三次176同组Player也不被称为修复旧事件。没有发现状态损坏或正常回滚不可靠，未套用Scene豁免。

**C/D：内置、无key查询、样例与闭包**

- 正式key1密文经C02 Preflight/Execute，注入原 `BeforeImport` 中断后走Recover并重复Deploy，部署全程无内容key。payload逻辑复制356877 bytes，Catalog/Complete/密文逐SHA不变；真实AssetDatabase.Refresh后wrapper可导入、`payload~`及其后代无meta/无GUID，原 `OpenBuiltIn` 严格定位成功。应用key经原ResourceConfiguration/ResourceStartup后取得并归还TextAsset，不读raw或开发机发布路径代替内置。仅自有ProjectGuid子树被使用和清理，父目录/父meta与其他部署不接管。
- E05直接走原Store/Configuration查询：未初始化、无key时Missing且不建root/lock；启动并准备另一密文版本后，将provider设为缺key，原查询仍Ready、调用key计数不变、Store全部SHA不变，逻辑715901 bytes/1个prepared，逐version实盘文件大小一致，PreparedFrom/To保护与不可清理标记有效。active/LKG/held/in-flight和最新状态复查合同沿用已有门禁；未新增删除权限或第二清理入口。
- ResourceStartup样例仅增加非序列化的应用ContentKeyProvider和原配置/Build透传；Editor显式非生产环境变量provider便利按钮不保存秘密、进入Play需重新注入。None/Simulation不取key，配置不初始化、不创建Store。真实加密样例用原Editor Build、同一Run/Startup、Text/Data/Core接入及完整归还通过；原None/Simulation和双内置版本/真实导入样例也通过。用法、缺/错key、显式版本、归还/Stop及两类导出区别已补Resource.md，无新Manager或第二初始化入口。
- 本轮直接重新验证完整Runtime/Editor源码闭包、Windows Development Runtime及Sample源码编译/Player依赖图、精确BC路径+官方SHA白名单和实际AssemblyRef图；这些是辅助编译/静态闭包，不冒充Player运行或阶段9独立工程。G3正式Player本地加密/版本事务176同组×3继续沿用，与Editor HTTPS、Editor BuiltIn分别报告。

**遗留证据范围与警告**

- g36/g37正式构建日志每份2076条匹配回显，去重均为1038诊断、集合差0，实际均来自Runtime的Csc区块，非后半日志附近的Assembly-CSharp。Code分类：CS1591=1019、CS1570=5、CS1572=2、CS1573=9、CS1574=2、CS0114=1。来源：Core222、Foundation143、Action35、Audio39、Http12、Resource540、Save2、Utilities44、Runtime根1；Utilities含vendored SharpZipLib的6条。与HEAD未改657条/99文件，已有但修改130条/17文件，HEAD未有251条/8文件；HEAD不是E04开始时工作区，不能据此全部归为E04或历史。
- 至少24条CS1591明确是E04新公开API：编码/KeyProvider/预算8、内容诊断10、内容错误4、Configuration.Decryption与Report.Content各1。已补线程/复制/所有权、预算与native成本边界、诊断快照及错误语义；**当前Unity实际生成的Library/YLFramework.Runtime.xml中24个member均存在**，Unity编译无错误。未重新构建完整Player以制造新warning总数，不将1038减24冒充当前实测余量。既有ResourceException.Source隐藏基类属性（CS0114）保留，不为清警告破坏public API；不关闭Analyzer或压制诊断。
- 有限明文观察复用G3原件，不再机械重复Player。g36 miss143，g37三次132/139/137：IOException119/110/113/114（缺HResult，不能逐条确认为共享冲突）、FileNotFound19/20/21/20、ShortOrPartial5/2/5/3（均Created空日志/替换JSON临时文件）；不是文件数量或产品认证失败。20个staging encoded路径中成功读取YLRENC01头的路径只有8/9/11/10，其余12/11/9/10确有内容采样缺口，目录提升也不自动逐文件补样。watcherErrors=0不等于无miss。只能说声明窗口/根未观测UnityFS写事件，不证明全系统、native内部、其他格式或进程内无明文。
- 正式CNG内存缓冲/只读解密Stream/LoadFromStream路径未改，无runtime临时明文fallback；None正常LoadFromFile和构建端raw是不同IO范围。16项source-io是定向正则点索引，不是全调用图/OS拦截。无驱动、系统信任/权限改动、用户目录扫描或真实CDN访问；不可靠分配计数仍不写成零。

**Review、实际回归与工程保护**

- 一次独立G4 Review发现3个P2与1个P3，均修复：样例构建前只保存确认owned Scene；导出在规范化后且任何Exists前拒绝正/反斜杠UNC/device；206重建Editor会话/Store后续传；ETag变化第二请求精确断言`full:`。UNC缺陷真实RED为1P/1F，之后两个参数GREEN；原样例缺provider、原导出缺API各有一叶真实RED。没有二次全面Review。
- 原始结果全部保留：`g4-delivery-01`=3P/1F（C02夹具斜杠断言错误，非产品）；`g4-targeted-01`=17P/1F（样例owned Scene未保存，未进入构建）；`g4-review-regression-01`=14/14叶通过但**suite总门禁失败**，不称为完整GREEN。后者已直接复现为夹具用canonical BuiltIn reader读取pretty JSON的Builder workspace marker，按真实工作区格式读取且保留链接/GUID检查后，原owned尾部成功清理。现有结果收集器只增量记录SuiteFailures，防止leaf全绿掩盖收尾失败，不另建产品诊断系统。
- 最终只回归受影响样例完整组：`g4-sample-final-01`=5P/0F、RunnerResultState=Passed、SuiteFailures空、CompleteEvidence/PassedGate均true。其余续传/历史/事务/C02/E05/导出/闭包Passed叶按影响沿用上述运行，不把不同运行相加为一次完整大套件，也不重跑无关G1～G3。CLI超时均等待同一run的持久finished/leaf，不重复并发启动；工具根Duration/部分StartTime存在跨Domain时区/重置问题，不作性能结论。
- 本轮18个内容变化路径（含原Plan）：导出器1、样例2、E04公开API XML5、测试/收集器5、新测试meta1、现有文档3、原Plan1；只新增ResourceContentDeliveryTests.cs及Unity生成的配套meta，不新建框架/总体Plan/过程文档。准确名单见 `g4-scope-final.json`。未新增依赖/asmdef/渠道平台，不宽放插件白名单。
- **以G4开始与结束为准的12048项受保护内容SHA全部一致**，新增/删除/变化/不完整均0；Git dirty路径232→237，18个内容差异均在上述范围，path集合稳定不等于原子快照。旧保护脚本固定拿G1原baseline比较，产生3项早已在G4起点存在的差异；其失败JSON保留，不能拿它误认G4修改Packages，也不能覆盖旧基线。正确本轮比较见 `g4-scope-final.json`。原XF/XFAB/XFFSM、MainModule、场景/Prefab/Canvas、历史Bundle、用户StreamingAssets/父meta、Packages/ProjectSettings、AGENTS、工具配置/忽略规则及并行成果未改。`git diff --check`通过（仅既有CRLF提醒），始终没有Git写操作。
- 终态Unity仍PID14272，Main已恢复且dirty=false，非Play/编译且scriptCompilationFailed=false；ResourceSystem.Current空、native Bundle空、CNG provider/key/key-object bytes/releaseFailures均0，样例owned标记false，测试TLS覆盖已恢复。直接事务测试在Shutdown后以保留主体引用检查live input/work、allocated/reserved预算、retained cleanup均0；其他已自行Stop的叶检查Current/native/CNG终态，不伪称已关闭主体仍能全局查询预算。无在途更新或丢失清理责任。测试回环server/cert均按using/finally释放；保留确切发布、package、缓存及失败/测试证据，自有临时Scene/源资产/构建raw/C02部署按归属清理。真实BCrypt系统释放失败、Scene延迟坏Stream、其他平台/IL2CPP/全系统IO限制仍按批准范围保留。
- **停止点**：G4技术完成，E04具备整体验收条件，待用户验收。不自动推进Workbench整体优化、8.10、阶段9或其他待验主题，不标记8.9或阶段8 COMPLETE，不顺带验收C05；不暂存、Commit、Push、Merge、Tag、切分支或执行任何历史/工作区写操作。

### 28.11 B02 + F03 联合实施（2026-10-17 09:02 UTC 授权）

- 用户正式授权本批必要源码研究、设计、产品实施、测试、测量、定向修复、一次独立Review及原文档维护。先B02必要门禁，再基于稳定实现完成F03，中间不等待确认或Git提交。E04 G1～G4正式实现、有效证据、上轮技术审核及限制沿用；不变更C05或8.9整体验收状态。
- 初始核对：分支/完整HEAD与预期一致，暂存为空；237个已有dirty/未跟踪路径正常保留。唯一主Unity实际为2022.3.62f3c1、StandaloneWindows64，非Play/编译/导入、compilationFailed=false，Main场景dirty=false、native Bundle=0；MCP无pending测试，原g4-sample-final-01收集器Completed=true。不启动同工程第二实例，不保存/关闭用户脏场景。
- 最小安排：先为现有Project资源定义/压缩/加密之外的执行选项增加小型持久载体，同一解析接入原Operations/Builder/Publisher，保持原调用默认；以直接功能测试确认保存/重载/Undo、无窗口、拒绝/取消/失败保旧。随后复用27.3/G3/G4证据，固定自有负载、1次warmup与少量重复样本，分别取构建/发布、Windows Editor回环HTTPS完整/真实续传、共享/热缓存业务与峰值/释放窗口；不重跑176组Player或G4完整异常矩阵。
- 取值裁定：Project继续唯一拥有Group、选择、Compression、ContentEncryption/KeyId，不做临时改Project或多级覆盖；执行配置仅保存项目引用、明确目标、App兼容标识、操作、force和必要生成选项，密钥仍为应用provider/明确环境来源。解析/查看不启动工作；操作开始冻结执行输入，资源输入仍走原完整指纹/后验/锁/owned路径合同。仅提供Editor可调用入口，不将当前Editor内执行称为batchmode/独立CI验收。
- 证据归属预算：新增外部owned根拟定`%TEMP%/kilo/b02f03-20261017-0902/`，保存owner、起止工作区/保护SHA及原始样本；小型复跑入口与夹具生成方式留在YL测试源码，不以Temp可执行文件作为唯一入口。只在登记自有Asset根/Workspace/Publisher/Store/回环服务内操作，不使用MainModule，不改正式游戏资源、受保护配置或旧证据。主要测量不并发导入/构建/其他自有重负载；临时环境/TLS按finally恢复。
- 工程保护沿用1.2及28.10.19～20：原XF/XFAB/XFFSM、MainModule、正式Scene/Prefab/Canvas、历史Bundle、用户StreamingAssets、Packages、ProjectSettings、AGENTS、工具权限配置、忽略规则及并行成果严格保护；本轮不暂存/Commit/Push/Merge/Tag/切分支/Reset/Clean/Stash/Rebase/Force Push。最终仅在真实门禁满足后记录“B02与F03技术完成，待用户验收”，不写8.9 COMPLETE，不自动进入后续专题。
- **B02首轮必要门禁已通过，进入F03**：小型`ResourceBuildRecipe` SO仅持有执行选项/Project引用；`ResolveRecipe`纯解析，Inspector/无窗口`ExecuteRecipe`复用原Operations/Builder/Publisher。原接口/None默认保留，`ResourceBuildInvocation`只报告不参与内容身份。真实缺能力RED为新保存/重载行为1叶因配置类型不存在失败；随后`b02-green-01`为11/11、RunnerResultState=Passed、SuiteFailures空。覆盖保存重载、Undo/dirty拒绝、冻结执行选项、窗口关闭/同Inspector服务/实际样例、legacy None Catalog身份、加密provider及失败/取消保旧。`b02-closure-01`为4/4、suite Passed，包含Editor Runtime/Editor与Player依赖图、Sample Player图；这是实际Unity编译/图闭包，不冒充独立Player或batchmode运行。
- 新证据根`%TEMP%/kilo/b02f03-20261017-0902/`已登记owner；`baseline.json`在改产品前记录237个dirty路径与12048项保护SHA，statusPathSetStable=true仅代表路径集合稳定、不是原子内容快照。`ResourceCostEvidence`为显式测试回执，记录原始leaf与suite终态及实际源码SHA/硬件；不形成产品常驻监控。首次tests-run在刚导入测试时得到Unknown/0叶，不计RED/Passed；随后发现树确认新叶，再实际执行1叶RED，没有把0叶冒充通过。

| F03流程 | 已有有效数据 | 本轮补测安排 |
|---|---|---|
| A 构建/发布 | 27.3正式小样本、已有分项计时与强制native回调合同 | B02配方驱动同一自有多包资源集，new/no-change/local-change，分列指纹/后验、native、SHA、编码或密文复用、复制/验证 |
| B 完整/续传 | 8.8/G3正式HTTPS合同；G4固定351921B密文的真实partial/ETag/If-Range功能 | 优先补足较大、非高度可压缩制品及多包，固定每份发布字节；1warmup+少量重复，实际网络产生partial，按确切offset/新传输字节计算吞吐 |
| C 正式消费 | G3 Windows64 Mono三进程、各mode 1warmup+5samples，含正式加载与消费者；不重跑176组 | 仅补Editor共享依赖、多消费者、held热复用与多包业务窗口，不将Editor新增数据外推Player |
| D 峰值/释放 | G3显式输入/预算/CNG及归还终态；分配能力探针Unavailable | 新测下载/消费窗口的显式分项与可获得Profiler/进程采样峰、释放后责任，采样峰不称绝对瞬时峰 |

- TLS仅本批新生成的非生产`loopback.pfx`，只在自有IPv4 loopback服务信任其确切证书，不改系统信任。原test-only `ResourceTlsServer`增加可选`BodyBytesWritten`观察点，记录成功的TLS body WriteAsync字节，不等同TLS wire bytes或客户端收到；无订阅时行为不变。取消首段的服务端写出与实际partial落盘offset分列，不能把计划响应长度冒充实际传输。
- B02自检另发现新配方报告的策略冻结边界：provider可在原Preflight重入修改Project压缩，导致冻结报告Lz4但实际构建Uncompressed。`b02-policy-red`真实1叶失败（BuildError=null），不是推断；现已增加配方路径的Preflight后策略复查与Builder结果一致性门禁，不恢复/覆盖应用自行改动的Project，定向GREEN待运行。该门禁不加进旧调用，也不变更内容协议或密码后端。
- 策略修复及测量接点直接回归`b02-policy-and-cost-green-01`已完成15/15、suite Passed、SuiteFailures空；含策略重入RED→GREEN、原None/UI/无窗口样例/legacy身份、加密冻结/保旧及完整`ResourceContentValidationTests`。可选内容分项接口另在真实Unity动态NUnit断言见缺接口RED，再以同路径实际验证事件序列GREEN；它不是独立Player或全框架性能实证。
- F03测量前固定：2048²+1024² RGBA32无mip非高度可压缩纹理、共享Material/3Prefab、20s stereo PCM和独立16KiB Text；Uncompressed+StrictMode。一个自有Project在完成None后单向保存Encrypted定义，不克隆资源权威；两mode从相同entry字节/相同1～3局部变更序列开始。A各mode1warmup+new/nochange/local各3；B各mode1full warmup+3full+3真实partial续传；C各mode1warmup+3新Editor会话，12Prefab+2Texture+2Audio消费者与512次held热获取。B/C使用A最终固定导出，非逐请求重新加密。State/SceneSetup在真实Unity JsonUtility roundtrip已核对保留Main路径。
- 新短Store缓存归属：`%TEMP%/kilo/f3-<本夹具8hex>/nf0…er3`，version路径≤210、完整文件<255，marker登记Run/Root/Token及各Store owner；成功仅清本批核验owned缓存，失败保留并记录。自有Scene在`Temp/F03-<8hex>.unity`，拒绝用户dirty场景，原setup仅在收尾恢复；不会在原Main中Play或运行MainModule。所有新样本只称Windows Editor，不重复G3正式Player矩阵。
- **首次F03运行不通过，不计性能完成**：`f03-baseline-01/finished.json`为3叶失败、6个失败suite。A在None local-change-1的Index原子替换`File.Replace`报“无法删除要被替换的文件”，B/C因A未完成无样本。已有None/new/nochange样本与完整错误保留，不挑选成功叶掩盖整体失败；本次原夹具正常回收raw/Publisher/source并恢复Main，失败空Cache根`f3-eda210a4`按记录保留。实际G盘为Healthy NTFS Fixed；尚不能据此确认哪个外部句柄/根因，更不能追认g36为同根因。后续先用同卷原AtomicWrite做无阻塞/显式阻塞读者诊断；测量夹具已补失败HRESULT/metadata状态并保留真实失败根，未加盲目retry/降验证/改系统环境。
- I/O最小实证：`publication-io-01`在无本方长期reader的连续写第8次真实1175失败（其人工reader分支尚未执行，不能冒称对照已过）；独立`.NET 10.0.13`同卷无读回路径亦在第209次失败，带读回512次成功，见`publication-core-io-corrected.json`。初版PS把$null绑定为空串的ArgumentException87原始输出单独保留，不纳入I/O结论。`publication-lock-probe.json`在第53次1175后紧邻采集：Restart Manager候选为空（不排除短时句柄/内核过滤），From/To原名字节均与预期完全相同；只注册两个自有路径的短暂诊断session并finally End，没有shutdown/restart或系统配置/信任改动。现场`AssetBundles/YLFramework/f3-io-rm-7cdb8915`为本批自有，未触碰未知目录。
- 局部修复裁定：依据[ReplaceFileW官方1175状态](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)及上述双文件实际一致性，只对HRESULT `0x80070497`最多5次、10/25/50/100/200ms有限重试；每次重试前重新拒绝链接、逐字节核对From/To，并在等待前后响应取消。其他错误直接上抛，1176/1177等可能部分提交状态绝不重试；不先删旧、不换MoveFileEx、不跳锁/校验、不改元数据协议或所有权。来源进程/过滤器仍未知，本项仅针对已证实的原子替换暂时失败模式，不宣称识别root owner或追认g36；若超过385ms退避预算仍失败则保持原明确错误。新增直接I/O回归含持续阻塞/变更From/晚到取消，GREEN待实际取得；不以此宣称性能提速。

#### 28.11.1 最终技术门禁、源码与证据索引

- **有效完整运行**：`f03-baseline-02/finished.json`为A/B/C 3/3 Passed、SuiteFailures空、EnvironmentRestored=true。A叶1227.916s、B叶96.344s、C叶11.484s均含fixture/正确性开销，不拿叶时长当性能样本。600s工具等待窗口到期后只等待同一观察进程PID40436终结，没有重复启动测试。A实际为1个bootstrap+2mode各1warmup、3new/3nochange/3local；B每mode1full warmup+3full+3resume，续传无另加warmup；C每mode1warmup+3测量。
- **直接回归**：`publication-io-green-02` 6/6、suite Passed，覆盖128次普通同卷原子写、持续reader拒绝保旧、竞争From不覆写、取消不提交、原正式发布故障/取消/恢复与4个程序集闭包。人为持久reader实际HRESULT32，不等同未知1175 owner；竞争变更/晚取消回执没有证明“已进入1175退避后才发生”，该分支只完成静态核对，不扩大声称。
- **一次独立Review**：覆盖B02设计/实现、测量可信度及必要I/O修复，未发现P0/P1；发现1个P2：Mono Process.WorkingSet64/PrivateMemorySize64成功返回0却被当作有效计数。`review-counter-red`真实1叶因0不大于0失败；修复仅在测试采样器将非正值归为null/Unavailable，保持原API调用频次，`review-counter-green` 1/1、suite Passed。`f03-baseline-02`原JSON不改写，进程0值全部从统计中排除，不以全绿掩盖此资格问题，不做第二轮全面Review。
- **测量身份**：Unity2022.3.62f3c1，Windows11 10.0.26200 x64，Ryzen7 9700X/16 logical、报告内存32452MiB、RTX4070Ti SUPER/16061MiB VRAM，StandaloneWindows64 Editor/Mono；G盘Healthy NTFS Fixed，未测介质类型或OS冷盘。未清系统缓存/用户Library，不并发自有重负载。固定Uncompressed+StrictMode、原SHA/读回/认证、128MiB显式数据预算、64KiB/2slot/32KiB参数不变。
- `f03-baseline-02/identity.json`记录768项实际C#/asmdef/BC DLL/manifest/lock/版本源条目及4个loaded程序集SHA，另有fixture源文件/meta GUID与RawFiles/DeliveryFiles SHA。只有HEAD一致不被当作源码一致。`f03-evidence-qualification.json`确认Review后Runtime/Editor/Sample.Editor DLL与测量时逐字节SHA相同；Tests仅改采样器资格及新增回归，继续引用测量时collector身份，不冒充修正后重新跑过A/B/C。
- **owned证据根**：`%TEMP%/kilo/b02f03-20261017-0902/`。索引为`f03-baseline-02-summary-qualified.json`（n=3中位/范围/样本最坏）、`f03-evidence-qualification.json`（53个原始JSON SHA、无效计数排除及版本边界）、各测试目录`finished.json`与`identity.json`。原始集为`f03-baseline-02/f03-a-*`、`f03-b-*`、`f03-c-*`、`f03-fixture.json`、`f03-export-*`、`f03-releases.json`及`f03-owned-cleanup.json`；不存在另一个`f03-load.json`。
- 最终固定制品Project=`f3-460e2471`，ProjectGuid=`5d506ce2a4ca6bf48b0c9b4f189af48e`；None Catalog=`e0feb4314684e96eeabeaa38326d83b4a5223fdb43fc74065d54c2a0b90e0e80`、BuildId=`d5240990fbe230dcc3c1ba7a15b11e6d7188da4e48c9a8848b0da1537a34ee48`；Encrypted Catalog=`24c2a73164bee10e6c4f7c8742e3dac0795222ba4b19c8b062a186f6bd1de3cc`、BuildId=`a812d58a767117f0033fca4755c02df17e6eeb233ade7481b809523679f1a283`。快照在该run的`f03/exports/{none,encrypted}/<digest>`，两份均18个交付文件、含catalog/complete共20文件；raw均24,654,121B，delivery分别24,654,121/24,664,362B，最大文件16,782,160/16,786,740B；整个导出分别24,663,784/24,674,025B。

#### 28.11.2 A 构建与发布成本

以下主对照均n=3、排除warmup；格式为中位数`[min,max]`，范围上界即样本最坏值，不称总体P95。表内其他分项为中位数，完整分项范围与每个样本在资格化JSON中。各阶段存在嵌套，不把本表列数相加当总耗时。

| mode/场景 | 总操作s [范围] | Operations预检s | Builder指纹/后验s | native s | 制品SHA s | native读回s | 编码/密文复验s | 发布s |
|---|---|---|---|---|---|---|---|---|
| None/new raw | 45.951 [45.484,51.963] | 16.014 | 28.223 | 1.528 | 0.218 | 0.191 | 0 | 0.653 |
| None/no change | 46.031 [44.585,47.624] | 14.767 | 28.119 | 1.409 | 0.434 | 0.190 | 0 | 0.642 |
| None/local | 46.989 [43.924,47.989] | 14.118 | 26.155 | 1.417 | 0.415 | 0.188 | 0 | 2.799 |
| Encrypted/new raw | 67.093 [65.929,69.116] | 16.327 | 42.120 | 1.583 | 0.229 | 0.203 | 1.279 | 4.773 |
| Encrypted/no change | 65.888 [61.452,66.388] | 15.247 | 45.227 | 1.516 | 0.469 | 0.208 | 0.316 | 2.045 |
| Encrypted/local | 72.999 [69.860,76.550] | 15.733 | 48.386 | 1.589 | 0.470 | 0.207 | 1.474 | 5.976 |

| 场景 | native实际重建 | 密文新编码B / 复用B | 发布repeat / delivery复制B | 复制ms / staging验证ms（中位） |
|---|---|---|---|---|
| None/new raw及no change | 是，6个未冻结callback强制 | 不适用 | true / 0 | 0 / 0 |
| None/local | 是 | 不适用 | false / 24,654,120 [24,654,119,24,654,121] | 240.212 / 418.246 |
| Encrypted/new raw | 是 | 24,657,505 / 0 | false / 24,664,362 | 240.669 / 905.793 |
| Encrypted/no change | 是 | 0 / 24,657,505 | true / 0 | 0 / 0 |
| Encrypted/local | 是 | 25,581 / 24,631,924 | false / 24,664,361 [24,664,360,24,664,362] | 265.043 / 974.341 |

- new仅指新建本Project owned raw Workspace，发布根已经由warmup建立；不能把None/new的不可变发布命中称为“新Publisher零成本”。全18个正式样本均`UnityBuildInvoked=true / ForcedRebuild=true / IsCacheHit=false`。本批没有绕过6个原回调门禁；没有将源未变宣称native skip。
- raw制品总量在local序列为24,654,120B中位、24,654,119～24,654,121B范围（text manifest长度随真实CRC变化），其他场景24,654,121B；`Build.DeliveryBytes`是交付量，不能误叫raw。编码/复用字节只计加密二进制，未含明文sidecar；发布复制量是delivery，不含新写catalog/complete。
- 明确瓶颈是Settings/源指纹及后验的wall窗口，不等同纯SHA CPU或每帧卡顿。Operations预检与Builder指纹（两者不重叠）占总操作中位约85.7%～93.3%；Builder自身源SHA读量None334,772,844B、Encrypted502,159,293B。Encrypted在编码后按原合同多做一次完整源/配置复验，不能把总差异全算AES成本。未为速度绕过该复验、SHA、native读回或密文认证；未实施性能重构或宣称提速。
- 密文复用不等于不验证，无源变化仍花316.055ms在交付复验且发布2045.141ms；局部只重编码约25KiB并复用大密文，但不可变新发布仍复制整套delivery，未新增delta发布协议。

#### 28.11.3 B 正式HTTPS完整下载与真实续传

- 自有IPv4 loopback HTTPS，64KiB body WriteAsync、每块请求2ms延迟（不是保证精确调度）；同一证书/每mode一次固定签名envelope与固定导出数组。每pair新Store，续传半段只复用自己的真实取消partial，不是安装版本的本地命中，也不称进程重启恢复或OS冷盘。平台仅Windows Editor，Player远端硬门禁不变。
- 每个完整样本19 payload GET、含Check metadata共21 GET；续传剩余14 GET、含Check与取消前段共22 GET。两mode全部`PlanReusableBytes=0`、观察到重试0、最终接收/服务端写出/预期剩余字节一致、无server errors、无checkpoint不配对或采样截断。每个续传largest实际offset均4,194,304B、`Range: bytes=4194304-`、If-Range为确切该文件SHA的强quoted ETag；prefix逐字节匹配，最终所有正式文件完整SHA/内容认证通过，激活后正式Acquire取得正确entry。
- None取消前实际收到11,959,488B、剩余12,694,914B；Encrypted为11,963,767B、剩余12,700,876B。前段除大文件4MiB外还包括此前完成的文件，因此不能用完整文件总量减4MiB冒充剩余量。完整payload含额外complete281B，分别24,654,402/24,664,643B；Check的catalog/envelope字节不算payload吞吐。

| mode/对照 | 新payload B | 下载流水线s [范围] | 有效MiB/s [范围] | Prepare完整s [范围] | Activate s [范围] |
|---|---|---|---|---|---|
| None/full | 24,654,402 | 2.723 [2.559,2.736] | 8.635 [8.594,9.188] | 3.835 [3.716,4.066] | 0.743 [0.689,0.787] |
| None/resume剩余 | 12,694,914 | 1.798 [1.738,2.014] | 6.733 [6.012,6.966] | 3.080 [2.963,3.659] | 0.677 [0.648,0.834] |
| Encrypted/full | 24,664,643 | 2.727 [2.712,2.836] | 8.627 [8.295,8.673] | 4.122 [4.103,4.216] | 0.926 [0.871,0.997] |
| Encrypted/resume剩余 | 12,700,876 | 1.954 [1.857,2.012] | 6.199 [6.022,6.522] | 3.375 [3.347,3.427] | 0.928 [0.853,1.011] |

| mode/对照 | Check ms（中位） | part SHA ms | receipt SHA ms | content SHA ms | 全量AEAD认证ms | staging结构+SHA ms |
|---|---|---|---|---|---|---|
| None/full | 45.357 | 149.544 | 0.686 | 149.705 | 不适用 | 180.611 |
| None/resume | 45.228 | 157.486 | 0.716 | 152.823 | 不适用 | 184.468 |
| Encrypted/full | 47.231 | 153.509 | 0.679 | 161.174 | 163.131 | 191.622 |
| Encrypted/resume | 46.748 | 152.294 | 0.663 | 155.224 | 158.264 | 196.893 |

- 下载分母为服务端观察到首个payload GET至客户端首次确认下载已完成/transfer已释放的wall窗口，包含串行文件SHA/IO、TLS与帧分发间隙，不是隔离socket时间；分子只用真实新增、最终验证的part长度增量。服务端成功写body数量/窗口另外原样保留，不当成TLS wire或单独证明客户端收到。续传吞吐未用完整24.6MB虚增；回环本机/同进程服务负载不外推公网/CDN。
- 每sample先分别计时再相加的Check+Prepare+Activate有效API总时长中位：full None4.669s [4.451,4.854]、Encrypted5.156s [5.077,5.189]；resume剩余为3.802s [3.655,4.538]、4.394s [4.275,4.404]，含实际取消前段则整pair4.970s [4.821,5.600]、5.502s [5.394,5.534]。这是不含额外correctness核验间隙的API-active完成成本，不冒充从用户点击到声音输出的未采样总wall，也不相加各列中位数或嵌套checkpoint。
- 上表SHA/auth是原具名实际elapsed边界，非纯CPU、非所有重复Open/cache/commit SHA的精确总量。content SHA、staging等可与其他窗口嵌套，不能加到Prepare。额外prefix/最终SHA断言在计时窗口外；采样/诊断观察器开销两mode同规则并包含在窗口内。

#### 28.11.4 C 正式共享消费与D峰值/释放

- **沿用的正式Player数据**仍仅指28.10.19原Windows64 Mono载体：每mode每进程1warmup+5samples，三进程各自总耗时中位None101.700～103.352ms、Encrypted108.177～109.583ms；Bundle prepare/native约97ms，物化None3.975～5.106ms、Encrypted10.760～11.941ms。保留其原源码、warm-cache、本地、Scene/Atlas/Audio支持窗口与分配Unavailable限制；不是本批新Player，也不是G0/B1原型或公网数据，不重跑176组。
- 新补仅为Editor：正式ResourceStartup/ResourceSystem+ResourceAudioClipProvider，12个Prefab消费者共享3个producer、2纹理与2Audio消费者，7 Bundle闭包，3个实例和静音AudioSource均保留到驻留采样之后，再归还实例/cache/Lease/请求并Stop。512次held-hot获取/归还不增加native加载且资产身份不变，未为好看的峰提前卸载。

| mode | Startup ms [范围] | 首次共享Acquire ms [范围] | 实例/Audio启动ms | 512次held-hot总ms [范围] | 归还ms [范围] | Stop ms [范围] |
|---|---|---|---|---|---|---|
| None | 12.833 [11.280,13.034] | 134.093 [133.181,137.474] | 5.518 | 1.522 [1.328,1.847] | 2.030 [1.975,2.033] | 0.911 [0.776,0.925] |
| Encrypted | 14.854 [14.664,15.922] | 139.909 [139.496,157.016] | 3.011 | 1.398 [1.323,1.428] | 2.265 [2.010,2.296] | 0.814 [0.746,1.332] |

| 指标/窗口 | None | Encrypted | 含义/限制 |
|---|---|---|---|
| held显式内容buffer B | 0 | 1,376,032 | None不走加密输入预算，不等于Unity输入或资源零占用 |
| 加载期在途预留采样峰 B | 0 | 1,376,032 | 与held allocated不在同一时刻，不能两者相加 |
| 主体allocated+reserved实际高水位 B | 0 | 1,376,032 | 128MiB上限内；非GC/native/进程总内存 |
| held live input / 声明Unity read buffer B | 0 / 0 | 7 / 229,376 | 是此适配器声明的额外读取buffer，不是全部Unity内存 |
| held CNG provider/key / key-object逻辑B | 0/0 / 0 | 7/7 / 22,834 | 仅正式CNG适配器owner及系统查询对象长度，非整个密码模块分配 |
| Texture对象 / AudioClip对象估计MiB | 40.001 / 3.370 | 40.001 / 3.370 | Profiler.GetRuntimeMemorySizeLong，不能当buffer或进程工作集 |
| 消费窗口Mono used采样峰MiB（中位[范围]） | 1089.504 [1085.957,1094.324] | 1046.645 [1042.398,1106.730] | Editor全局managed heap快照，非累计分配，不据此说加密省内存 |
| 消费窗口Unity allocator采样峰MiB | 355.247 [355.047,355.447] | 326.926 [326.728,336.105] | Unity全局逻辑计数，含Editor/观察器，不与Mono简单相加 |
| B完整/续传Prepare窗口Unity allocator峰MiB | 325.728 / 324.654 | 325.754 / 324.425 | 此窗口不包含后续资产消费峰；完整范围在summary |
| B完整/续传Prepare窗口显式allocated峰 B | 0 / 0 | 196,576 / 196,576 | 同窗reserved采样0、work峰1；不是精确瞬时峰 |
| 进程working set / private bytes | null / Unavailable | null / Unavailable | 原API返回0被Review判无效，原JSON保留但不进入结论 |
| 主线程分配字节 | null / Unavailable | null / Unavailable | 8192B能力探针before=after=0；不称零分配 |

- 每个C样本在真实归还后以及Stop后均验证：allocated/reserved/live input/work/retained cleanup=0，Lease/producer/waiter/record/Bundle record=0，native加载卸载计数7/7，CNG provider/key/key-object/read buffer=0、CNG release failures=0。累计高水位仍可保留，不能要求Peak清零；没有全局UnloadAll或清他人资源。
- 所有峰均为主线程tick采样（无截断），不是绝对瞬时峰或精确CPU归因；B同进程TLS server数组/测试、Editor、GC状态都会影响全局内存。热操作表是512次总量，不是每帧费用；异步首次Acquire包含调度/SHA/native/物化/交接，未新增细粒度主线程stall或纯解密CPU测量。不可靠GC分配计数不补0，未获取Audio欠载计数，静音isPlaying观察不证明无毛刺或可听输出。

#### 28.11.5 复跑、收尾与停止

- 正式配置用法/最小已执行示例在Resource.md“可共享构建配方”；`ResourceBuildRecipeExample.Build`已实际None执行，外部provider加密经同一ExecuteRecipe直接验证。配方的API、字段规则、存储/Undo、None兼容、拒绝/取消/失败保旧证据按28.11前文计，不重复全面E04。
- 可复跑代码保留在`Tests/EditMode/Resource/ResourceCostBaselineTests{,.Network,.Consumers}.cs`，从owned资源生成、正式配方到固定导出/HTTPS/消费/清理全部可重新运行；`Tests/Tools/Prepare-ResourceCostEvidence.ps1`实际执行过，创建工程外新owner与非生产loopback证书，不启动Unity或改系统信任。`ResourceCostEvidence.Run`是当前唯一Editor的显式入口，先导入/编译稳定与场景已保存，再以同一suite选整个fixture；无通用CLI/batchmode/CI承诺，原始证据不依赖Temp可执行文件。
- `review-counter-green/terminal-unity.json`实际终态：Unity2022.3.62f3c1、StandaloneWindows64、非Play/编译/导入、CompilationFailed=false；Current=false、native Bundle=0、Editor retained readback=0、CNG provider/key/key-object/release failure均0、无MCP pending，自有run已完成，Main恢复为唯一loaded/active且dirty=false。逐sample预算/在途/Lease归还见原始Stopped，不用构造默认snapshot冒充资源归零。
- `owned-cleanup-final.json`：成功F03 cache/source/raw/Publisher/自有Scene已由fixture核owner回收；首次失败空cache仅按其真实旧owner（Kind=F03BaselineCache、Run/Root/Token完全匹配）清理。1175探针From/To原样归档至`publication-probe-preserved`并删自有探针根；两份新生成的非生产PFX均已删除，原临时环境按值校验恢复，旧E04证据/凭据、未知目录未动。原正式发布故障回归按原fixture设计保留2个小快照，明确归属`AssetBundles/YLFramework/yl-release-03a2f7977601493ba2421d17cca53ecc/StandaloneWindows64`（ProjectGuid82b225c44222046439a47efe3f15c16a；versions801c4b65…/c0d476bc…），不是未明runtime所有权。
- **保护末检按实际分层记录**：`preclose.json`观测262个dirty路径、12088项广义保护条目，raw比较`preclose-comparison.json`有5088个保护差异，不能写成全部一致。`preclose-protection-qualified.json`逐项分离：5049是Agent Manager UI/恢复状态及worktree路径的并行/自动观测变化，另有新增`.kilo/run-script.ps1`；本批未调用Agent Manager API、创建/停止/移动worktree或编辑这些配置，来源不猜测、不回退。39项为上文明确登记的本批新小型回归发布制品。剩余4640项原核心保护内容（游戏/MainModule/正式Scene/Prefab/Canvas、旧Bundle/StreamingAssets、Packages/ProjectSettings/AGENTS、忽略与工具配置等）SHA差异0，未稳定读取条目0；不以新制品掩盖旧文件变化。
- 原237个dirty中227项内容保持原样，另10项仅本批获准增量；连同新增源码/示例/测量/Tools及meta共34个本批路径，额外1个并行run-script不归入本批。所有新C#与准备工具/目录meta齐全，实际Unity编译及Runtime/Editor/Sample/Player依赖图闭包通过；不声称另做独立batchmode/Player构建。`git diff --check`实际exit0，仅原NuGet安装记录/manifest的CRLF→LF提示，未修改它们或为此格式化全库。主Unity PID14272唯一，另2个为其AssetImportWorker子进程；本批所有等待进程均已终结，没有遗留自有后台服务。
- 当前分支与完整HEAD仍为`fusion/8-resource` / `ddf154cf2c72cd0b403cdd1547eee8ae06963547`，暂存0；未暂存/Commit/Push/Merge/Tag/切分支/Reset/Clean/Stash/Rebase/Force Push。最后文档收口后再记录`final*.json`同口径复核，不重复技术测试。
- **B02与F03技术完成，待用户验收。** E04沿用上轮技术审核及原限制，C05已通过用户技术验收，8.9整体与阶段8不写COMPLETE；到此停止，不进入Workbench整体优化、8.10、阶段9、其他XF扩展迁移或Unity版本适配。8.9整体获用户验收后再统一一次Commit/Push。

#### 28.11.6 1175重试路径定向补验（2026-10-17 12:15 UTC 授权）

- B02配方与F03主体交付已获技术审核认可；本次只补正式HRESULT `0x80070497`分支的关键行为证据，不预设产品有缺陷。旧reader=32、竞争变化/取消未证实晚于1175退避的限制继续保留，不用旧通过结果替代本轮。
- 起点实际核对：`fusion/8-resource`、完整HEAD `ddf154cf2c72cd0b403cdd1547eee8ae06963547`与预期一致，既有262个dirty/未跟踪路径保留、暂存0。主Unity PID14272为2022.3.62f3c1/Windows Editor、StandaloneWindows64，另2个是其AssetImportWorker；非Play/编译/导入、编译无错，Main唯一loaded/active且dirty=false，Current=null/native Bundle=0、无pending测试，自有上轮run已终结、loopback环境不再占有。
- 正式调用链为Publisher Execute的journal/owner/index提交及Acquire首次owner写入→同一私有AtomicWrite。From是写入前现有目标字节，To是本次已flush/关闭的`.next`字节；初次尝试+最多5次重试，请求等待10/25/50/100/200ms，等待前后取消、随后拒绝链接与核对From/To。非1175错误不进入此分支，首次不存在目标则仍走原File.Move。
- 最小安排：采用每次调用显式注入的私有替换/退避边界接点，不设置全局静态替身；所有异常筛选、等待值、取消、链接/字节复查及重试仍在唯一正式实现中。用准确HResult的受控IOException、小型自有真实文件和ManualResetEventSlim同步，顺序证明“1175→退避进入→取消/分别改From或To→恢复→拒绝且不再替换”；另测有限失败后真实File.Replace成功、持续1175耗尽与32/1176/1177分类。
- 新结果独立owner根`%TEMP%/kilo/b02f03-1175-20261017-1215/`，不覆盖旧JSON/资格统计。自有小文件根登记在新run中，结束按owner和确切文件集合清理，不把竞争者内容恢复旧值。仅回归新增接点及原子写/必要发布消费者，做一次小范围独立补审；不跑F03 A/B/C、B02全组合、E04/Player矩阵或31项。
- 不修改Resource.md（无业务变化时）、正式游戏/Scene/Prefab、StreamingAssets、Packages/ProjectSettings、AGENTS、工具权限/忽略、Agent Manager/worktree或并行成果；不启动第二个Unity、不保存/覆盖用户dirty场景、不终止未知进程、不改系统权限。全程无Git写操作，完成后只记录联合验收条件并停止，不推进其他专题。

**实际实现与结果**

- 产品差异仅`ResourcePublisher.AtomicWrite`四参入口转发到私有`AtomicWriteCore`，Core持有原唯一控制流；增加逐次调用的replace/backoff两个私有边界，默认null仍直接File.Replace/Thread.Sleep。未改HRESULT集合、Windows平台筛选、首次+5重试、等待值、取消/链接/From-To复查、File.Move首次写入或发布提交协议；无静态替身/公开API/第二算法。未发现需要修复的产品重试缺陷，不以新增接点编造产品RED。
- 新测试`ResourcePublicationRetryTests`共9叶；仅操作新run内`files/<case>`自有小文件。三种退避内动作由ManualResetEventSlim形成确定顺序：序号1替换尝试、2准确HRESULT `-2147023721`受控失败、3正式catch内BackoffEntered(10)、4工作线程取消/改From/改To、5BackoffResumed、6正式检查拒绝。没有用进入函数前取消或Sleep猜测时序。
- `seam-contract-before`1叶在接点尚不存在时的Assert.NotNull失败，只证明原代码缺少确定可控边界，不是产品逻辑失败；当时小夹具按失败策略保留，证据不改写。一次新测试编译曾因当前NUnit不提供NonParallelizable而拒绝运行，删除该不适用属性后按原串行Editor TestRunner执行；未开始的调用不计作测试运行、没有并发重启。
- **实际当前Unity运行**`targeted-01/finished.json`：11/11 Passed、ResultState=Passed、失败suite=0、EnvironmentRestored=true。组成是9个新定向叶+原ResourcePublicationIoTests一叶+正式ResourcePublishingTests.RealReleaseRepeatFaultCancellationAndRecoveryPreserveCompleteVersions一叶。原四参默认入口还在每个新case的独立default-probe文件上实际替换成功，未回写竞争变化的From/To。本轮没有跑F03 A/B/C、B02全组合、E04/Player矩阵或阶段8全量。

| 场景 | 实际替换尝试（首次+后续） | 请求等待ms | 正式结果 / 提交 | 清理前真实From / To |
|---|---|---|---|---|
| 退避内取消 | 1+0 | 10 | OperationCanceledException；不再替换、0提交 | old / next |
| 退避内From变化 | 1+0 | 10 | InvalidOperationException（inner1175）；0提交 | competitor / next，未覆盖竞争者 |
| 退避内To变化 | 1+0 | 10 | InvalidOperationException（inner1175）；0提交 | old / competitor，未恢复To |
| 首次1175后成功 | 1+1 | 10 | 成功；真实File.Replace 1次、提交1次 | next / 不存在 |
| 前2次1175后成功 | 1+2 | 10,25 | 成功；真实File.Replace 1次、提交1次 | next / 不存在 |
| 持续1175 | 1+5 | 10,25,50,100,200 | 第6次准确1175退出；0提交 | old / next |
| 32、1176、1177各一例 | 各1+0 | 空 | 原受控IOException直接退出；0提交 | old / next（只检分类，不模拟OS部分提交） |

- 表中old=`{"value":"old"}`、next=`{"value":"next"}`、competitor=`{"value":"competitor"}`。每份`retry-*.json`保存最终实际bytes与SHA、线程ID和严格有序事件，不仅断言异常类型。old SHA=`041e5924ebd1eca8a610f9391249b2e097f20f7a2802232408200cf883fb859a`、next SHA=`abe62bc5156539f93b1c481cd9ac061912dbdd0be910b52447302c6176bdf7e0`；竞争字节在清理前原样留存于回执，无自动恢复旧值步骤。
- backoff边界用事件/虚拟等待控制时序，记录的是正式算法请求值，不以OS调度总耗时断言精确命中385ms。成功替身不会直接返回成功，解除受控失败后明确调用并完成真实File.Replace。所有9例WorkerJoined/DefaultEntryVerified/ExclusiveHandleProbesPassed/OwnedFilesDeleted=true，cleanup error=null；注入仅是本次参数，无全局状态需要复位，事件/线程由finally回收。
- **一次独立补审**沿用原Reviewer `ses_eb67fe4f1ffeRbVOhwm44rdZW3`，只读对照新根`source-before/ResourcePublisher.cs`精确diff及9份新回执、11叶suite，未发现实质finding。确认没有绕过正式重试、默认分支未改、From/To变化晚于1175、成功是真实替换、计数与状态收尾可信。不做第二次全面Review。新增叶未单独注入退避期间链接变化；原链接检查通过diff确认保持原样，原保护证据沿用。
- **证据与复跑**：新根`%TEMP%/kilo/b02f03-1175-20261017-1215/`，`targeted-01-summary.json`为9场景有序统计，原始在`targeted-01/retry-*.json`/`finished.json`/`identity.json`。复跑仍用已有ResourceCostEvidence.Run选择完整`YLFramework.Tests.ResourceBuild.ResourcePublicationRetryTests`及所需直接回归，在新的本轮owner目录/新run ID执行，不设loopback环境、不依赖网络或Temp可执行文件。
- `cleanup.json`记录缺接点失败夹具已仅按owner+真实旧字节SHA+确切文件集合删除，未恢复任何竞争内容；9例成功小文件已各自清理。直接发布回归按原fixture设计保留新小型2版本，归属`AssetBundles/YLFramework/yl-release-41be9a5901d9494fb018d92e4754619b/StandaloneWindows64`，digests=`b530d5c9a782dec14e41715ba79d66a6bc7eba766b219af6291f0151c5bb9807`/`461768e5c16b4217eaa71c9c62fae12d401dace18c9879ce83e6810c318a7a4a`。旧B02/F03/E04及未知目录不动。
- **F03适用性不扩大也不机械撤销**：只有Editor私有转发/边界和测试源码变化，正式默认算法、资源/压缩/校验/安全参数、Runtime和测量窗口未改；旧性能数字继续按其原源码/载体/采样器身份解释，不宣称本次重新测过。旧53份F03原始JSON起点SHA逐一匹配，末检继续复核；不因Editor程序集hash变化要求整套重测。不改Resource.md，不把受控1175当作再次复现OS占用或查明g36。
- `targeted-01/terminal-unity.json`实读：Unity2022.3.62f3c1、非Play/编译/导入、CompilationFailed=false，Current=null/native Bundle=0、retained Editor readback=0、CNG算法/key/key-object/release failure=0、无pending测试或临时loopback环境占有，自有run终结，Main仍唯一loaded/active且dirty=false。本轮未切换或保存场景、未启动第二Unity或任何网络服务。
- 保护、meta及Git末检结果在本次新根`final.json`/`final-verification.json`记录；范围仅原Plan、本Publisher、新测试及其Unity生成meta，旧原子写测试/Resource.md/工具配置保持原样。不执行任何Git写操作。**1175重试路径定向补验完成，B02/F03具备联合验收条件，待用户验收。** C05已通过用户技术验收，状态不变；8.9及阶段8不标COMPLETE，至此停止，不自动进入其他专题。

### 28.12 第一次Git收口：功能基线

- 2026-10-17 用户正式调整阶段8.9的Git策略：现有已完成并取得验收证据的8.9功能作为第一次独立Commit；后续Resource Workbench UI/UX全面优化完成后另做第二次独立Commit。原“必须等整个8.9完成才允许第一次Commit”的限制被本次明确授权替代。
- 本次只授权第一次功能基线Commit；不自动Push、Merge、Tag或切换分支，Push仍等待用户明确授权。本次不重新测试E04、B02/F03、C05等已完成功能，也不提前实施UI优化。
- 第一次提交范围以真实源码、Git差异和既有证据逐文件核实：既有Resource Workbench已交付功能；ResourceImage、预加载、诊断及功能补齐；C05 ResourceStartup、C04/F04必要配套和正式样例；E05 ArtifactStore只读查询；C02 StreamingAssets安全部署；B06自动SpriteAtlas；E04 G1～G4内容加密、构建、加载、更新、导出及必要第三方依赖和许可；B02 ResourceBuildRecipe；F03性能基线、正式测量入口及必要测试；1175原子替换重试修复及后续定向测试；直接相关文档、asmdef、插件与合法新增.meta。
- 明确排除：未实施/未验收的Workbench UI/UX改造、并行的Agent Manager/UI成果、原XF/XFAB/XFFSM Package、原游戏/MainModule/正式Scene/Prefab/Canvas、用户StreamingAssets/历史Bundle/未知构建产物、Packages/ProjectSettings/AGENTS.md、`.gitignore`/`.kilo/kilo.jsonc`/工具权限配置、Library/Temp/缓存/Player构建/证据JSON/密钥/证书等临时内容。
- 暂存只使用明确核对的文件路径，禁止`git add .`、`git add -A`、全仓库`git add -u`或无核对整目录暂存；提交前核对`--cached --name-status`、`--stat`、`--check`、具体差异与.meta/GUID关联、依赖闭包及敏感内容。
- 第一次Commit建议信息为`feat(resource): complete stage 8.9 functional baseline`；不写“8.9 complete”“stage 8 complete”等会误示后续UI和8.10已完成的描述。提交后只读核对SHA、分支/HEAD、文件清单、未提交UI/并行修改、暂存区与其他成果保护情况。
- **第一次Commit不代表8.9 COMPLETE**，也不表示8.10或阶段9已完成。提交后在最终报告给出实际SHA，不立即回写Plan制造新的待提交差异；后续UI收口再按实际状态维护Plan。
��
