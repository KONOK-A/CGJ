# XF × YL 阶段 8：Resource 深度融合与全面升级 Plan

> **总体方案 APPROVED；8.1～8.10 已通过用户验收，阶段8为 COMPLETE。8.9 最终 Resource 基线为 `9a88e6a4ec04209f60453b1e166de28b3034430d`；2026-10-19 用户正式确认阶段8总体验收通过，Windows64 Mono及Unity `2022.3.62f3c1` Windows64 IL2CPP（Low裁剪）正式支持成立，完整证据及限制见§30。既有专项证据、旧单根兼容已移除的最终模型继续有效；未重开UI、密码后端或31项审计。阶段9不启动；本轮按授权执行精确Commit、普通Push及`integration/xf-yl`集成。**
>
> 当前分支 `fusion/8-resource`；本轮执行阶段8收口提交、普通Push至已核实`origin/fusion/8-resource`及`integration/xf-yl`集成。开工HEAD `5074595212b08bba45d95d48768a895258ea6f93`保留为历史检查点，其相对Resource基线的`Assets/YLFramework`差异为空。C05、E04、B02/F03的既有验收、技术审核与证据边界分别沿用原记录；实际Commit、远端SHA及合并方式以最终报告和远端历史为准。
>
> **Workbench UX1～UX5、视觉精修、多资源目录与中文文案已通过用户验收。** 最终证据与限制见§29.25～§29.31；§29.17～29.24保留反馈和历史检查点。旧单根兼容清理的最终模型见§29.30～§29.31，8.10不恢复旧模型。8.10及Windows64 IL2CPP增补已通过用户阶段8总体验收，阶段8 COMPLETE。

本文保留稳定契约、阶段门禁、关键历史证据与遗留限制；重复授权、已被后续状态覆盖的中间记录和一次性诊断过程已合并。保留原章节编号，尤其是 17.4、28.2.1～28.2.4 和 28.6，便于继续引用。

## 1. 当前基线、授权与保护边界

### 1.1 当前状态

| 项目 | 已确认状态 |
|---|---|
| 工程 | Unity `2022.3.62f3c1`；Windows Editor / `StandaloneWindows64` Mono / 同版本Windows64 IL2CPP（Low裁剪） |
| 实施分支与 HEAD | `fusion/8-resource`；阶段8收口提交实际SHA见最终报告及远端历史；Resource基线 `9a88e6a4ec04209f60453b1e166de28b3034430d` |
| 总体架构 | 自有唯一 ResourceSystem；Runtime/Editor 两主体，Foundation/Core 保持实际编译边界 |
| 8.1～8.8 | 已完成技术验收与 Git 收口；提交映射见第 10 节 |
| 8.9 | 全部已交付范围通过用户验收，第二次提交 `9a88e6a` 已在当前本地及远端历史中确认，Git已收口 |
| 8.10 | Windows64 Mono全流程及同版本Windows64 IL2CPP（Low裁剪）增补通过用户阶段8总体验收；阶段8 COMPLETE |
| 持久协议 | Catalog Schema v1 已冻结；签名授权与激活记录不塞入 Catalog 字段；Editor配置模型调整不改变Runtime读取 |
| 当前 Git 权限 | 2026-10-19授权精确暂存本批、中文Commit、普通Push及`integration/xf-yl`集成；禁止Amend、Force、Rebase、Reset、Clean、Stash，不接管用户/并行改动，阶段9不启动 |

历史设计从 Tidy 集成 `fe1cd72`、方案批准 `0e71c8b` 开始。早期“当前为 integration 分支”“主体仍依赖 XFAB”“8.6b 待提交”等描述已被后续实施取代，不再作为现状。历史阶段 7/Tidy 细节分别见其专项 Plan，不在这里重复。

### 1.2 必须保护

- 原 XF/XFAB/XFFSM Package、游戏业务、MainModule、正式 Scene/Prefab、Canvas、旧设置和历史 Bundle 不因新资源系统自动迁移、保存、重建或删除。
- `Packages`、`ProjectSettings`、`AGENTS.md`、用户配置、ProBuilder Settings、并行 UI Plan 及其他工作区成果保持原样；Git 忽略不等于可删除。
- 不调用旧 `ProjectBuild.Build(MainModule)`，不清整个 AssetBundles、StreamingAssets、persistentDataPath、Library 或 Temp，不启动第二个同工程 Unity。
- 只写明确登记的新 YL 配置、夹具和产物。未知文件/目录、未知 `.meta`、symlink/junction 不自动接管；删除仅限本批真正拥有的内容。
- 新资源使用新 GUID；移动存续文件保留 `.meta`。类型/AQN、字段、地址、程序集和格式变化单独核对，不靠 `FormerlySerializedAs` 掩盖不适用的迁移。
- 用户Git决定（2026-10-17）为**两次独立提交**：第一次8.9功能基线 `8abaf2d` 已交付；第二次提交已验收的Workbench UX1～UX5、视觉/交互修正、多资源目录、旧单根兼容清理、直接配置转换、测试、正式样例及文档。2026-10-18用户正式授权第二次Commit与普通Push；只精确暂存本批已验收内容，不暂存/Commit并行NuGet、Packages、ProjectSettings、AGENTS、工具配置、其他Plan或用户资产，不Force Push、Merge、Rebase、Reset、Clean、Stash或切分支。

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
| 已通过用户验收 | B02项目级/自动化构建配置与F03代表性性能基线，见28.11；B06自动Atlas及E04 G1～G4正式交付范围沿用原结论，历史G0/C1/B1与限制继续保留 |
| 按实际需求 | E03 历史签名便利入口与用法；不为表面完整度造平台 |
| 已验收 | B06自动Atlas、E04 G1～G4正式交付范围、B02/F03及Workbench UX1～UX5均已通过用户验收或沿用既定技术审核结论；历史G0/C1/B1与限制继续保留 |
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
- **正式远端后端接受WindowsEditor及§30实际受测的Unity `2022.3.62f3c1` Windows64 Mono/IL2CPP Player**；各自原硬拒绝均先记录，真实HTTPS、签名/序号、partial续传、拒绝与恢复门禁通过后才明确开放，IL2CPP增补见30.7。其他平台、32位和其他Player引擎版本仍由源码拒绝；普通Player不含测试pin，不以删除gate或测试绕过代替支持。

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
| 8.9 功能/体验完善 | 用户验收与Git收口完成 | 第27～29节；最终Resource提交 `9a88e6a` |
| 8.10 总体验收 | 用户验收通过，阶段8 COMPLETE；Git收口按本轮授权执行 | §30；Mono/IL2CPP真实Player、跨进程恢复、远端、Review修复与导出检查全部收齐；实际Commit/远端SHA以最终报告为准 |

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

既有功能补齐、Workbench UX1～UX5、多资源目录及旧单根兼容清理均已纳入8.9最终交付；全部已交付范围已通过用户验收。31项审计结论保留，不因最终收口重新审计或扩展功能。多资源目录的最终模型见29.29～29.30，Legacy名称的样例仅保留来源说明，不再是产品兼容入口。第二次Commit与Push见29.31。

### 28.2 31 项目标与处置

#### 28.2.1 范围与证据

- 需求来源为用户提供的《YL-Resource-目标功能清单与核查提示词》第 2～7、9、10 节，原入口 `E:/Downloads/YL-Resource-目标功能清单与核查提示词.md`。保留 A01～F05 编号，不把条件可选项强制记缺陷。
- 初审以工作区实际源码/调用链为准，未重跑全量。以下仅叠加 C05/E05/C02 的已知进展，不宣称重新验证其余能力；“完整”均受平台/来源边界限制。
- 完成度分开表达：底层功能、正式入口、可执行用法、必要验证、专项授权。无 GUI/CLI、不沿旧 API 名、保守重建，都不自动代表功能缺失。

#### 28.2.2 A01～F05 当前能力表

| 编号 | 正式入口/证据 | 当前结论 | 真正限制与处置 |
|---|---|---|---|
| **A01** 项目与范围 | ResourceProject.SourceDirectories、ResourceProjectPaths、BuildMap；15～17/28.4/29.29 | 完整（多目录） | 目录以GUID登记并保存地址前缀；空/无效/重复/嵌套拒绝；跨Project来源与生成范围保护；ID变化不自动迁移旧发布 |
| **A02** Group/选择 | ResourceGroup/Selection、BuildMap.Create、EditorOperations；28.4/29.29 | 完整（多目录） | Group可跨已登记目录选择；目录登记不等于选择；文件夹/过滤/子资产与物理包分离 |
| **A03** 地址/alias/子资产 | ResourceAddress/Catalog、DescribeSubAssets、Simulation；17～18 | 完整 | 显式地址可跨移动稳定，旧快照不漂移；歧义拒绝，不造第二身份表 |
| **B01** 分包/共享依赖 | BuildMap.CollectDependencies/BundleEdges/MergeCycles、ReadBack | 完整 | Scene/私有/共享及环归并已解释；不恢复旧固定阈值 |
| **B02** 构建配置 | ResourceBuildRecipe/Invocation、Inspector、Workbench接点及ExecuteRecipe；28.11 | 已通过用户验收 | Project定义/压缩/加密唯一权威；同解析同正式服务、原调用/None保留，无batchmode/CI或部署/签发扩展 |
| **B03** 压缩 | ResourceCompression、Builder.Options/Settings；28.4 | 完整 | 三值白名单恒 StrictMode，其他组合拒绝；复用 |
| **B04** 增量/重建 | Workspace、完整指纹/后验、force 与 blocked 原因 | 完整且保守 | 六回调阻止 skip 是正确性保护；无可靠闭包不优化命中率 |
| **B05** 显式 Atlas | ResourceAtlasRules、PrepareAtlases、SetAtlasSprite；28.4 | 完整 | 非 Variant、Include in Build 关闭、名字唯一、修改后显式准备；非透明 late binding |
| **B06** 自动图集 | ResourceAtlasConfiguration / Generator / Plan、Project Inspector、正式 Builder；28.9/29.29～30 | 已通过用户技术验收 | 生成父目录显式登记并使用真实GUID；只新建自有子目录；规则来源可跨多目录；旧布局/隐式入口已移除，不自动取首源 |
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
| **E03** 签名交付 | ReadVersion + Signer.Create/CreateFromEnvironment/Export；26/29.25～26 | 正式 API 与当前版/所选历史版GUI已接入 | 历史签发绑定确切ReadVersion，当前索引入口独立；本轮精修展示，不重签或改变私钥/信任策略 |
| **E04** 内容加密 | 正式ResourceProject/Builder/Publisher/Content/ResourceStartup；28.10.19～20 | G1～G4正式交付，上轮技术审核结论沿用 | Windows x64 Editor/Mono、CNG AES-GCM+BC HKDF、64KiB/2slot/32KiB、128MiB显式预算；Scene延迟坏Stream及已记观察限制保留，不重开专题 |
| **E05** 磁盘查询 | QueryArtifactsAsync / ArtifactStore.QueryAsync、原 CleanupVersionsAsync/parts；28.7/29.25～26 | 原服务已通过用户技术验收，工作台只读表单已接入 | root及完整闭包显式输入，Ready非认证，候选非删除授权；不另建索引，UI不清理/恢复/挂载 |
| **F01** 所有权诊断 | GetDiagnosticsReport、InstanceCache.Diagnostics、Workbench；27～28 | 完整 | 显示真实 pin/入边/owner/排队，未知明示，历史默认关闭 |
| **F02** 构建分析 | Placement/SourceBytes/Dependents、BuildResult/FileBytes；27～29 | 原数据完整，Workbench展示接入并精修 | 预估/确切制品/变化原因分开；友好用途和来源不替代完整Bundle身份，不推断逐文件因果 |
| **F03** 成本基线 | G3正式Player历史数据+配方驱动A/回环HTTPS B/共享消费与峰值C；28.11 | 已通过用户验收 | 约23.5MiB/18文件、3重复、真实4MiB partial，原始JSON/制品/源码身份完整；进程及分配计数Unavailable，不外推公网/Player |
| **F04** 用法/示例 | Resource 模块说明、首页、模板、Samples/ResourceStartup；28.6 | 本项最小例已交付 | 启动/双模式/Core/Audio/类型/退出不再占位；更多专项示例仅按实际需要扩充 |
| **F05** 平台/Player | 主体与 Sample 图、Windows Development 编译；28.6 | 编译有据，Player 未验 | 8.10 全流程/持久重启未做，远端硬拒绝；其他平台不凭格式白名单宣称支持 |

#### 28.2.3 剩余事项与优先级

- C05、E05、C02、B06、B02、F03及Workbench UX1～UX5已通过用户验收，不重复实施。E04 G1～G4正式交付及上轮技术审核结论沿用，既有证据与限制见28.10.19～20。
- B02与F03已通过用户验收，见28.11；E03历史签名便利入口仍按需。
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
| Unity 2022.3.62f3c1 Windows64 Mono Player | §30真实C02内置首启、None/加密与Core消费、持久整组事务及重启恢复、HTTPS签名/序号/续传/拒绝门禁 | 受测范围已明确开放；普通Player默认TLS，loopback pin仅隔离测试构建；未实测普通信任链CA正向联网，不外推其他平台或阶段9 |
| Unity 2022.3.62f3c1 Windows64 IL2CPP Player | §30.7真实AOT/Low裁剪、None/E04内置与Core消费、持久整组事务/强停恢复、HTTPS签名/序号/partial门禁及无候选非Development构建 | 已在全部真实候选门禁及Review修复通过后开放；普通无pin、默认TLS拒绝实测，CA链正向未实测；不外推High裁剪、其他版本或其他平台 |
| OSX/Linux 桌面 | 普通目录/平台格式分支存在 | 无本批跨主机真实验收；远端拒绝 |
| Android/iOS/WebGL及Windows64以外IL2CPP | 仅部分平台映射或计划范围 | 没有相应最终闭包/运行；mount仍受桌面范围限制，jar/WebGL内置提取未实现，不只是缺设备；其他Unity版本也未开放 |

8.10已按独立授权完成上述正式Player/重启、许可/导出与交接（§30）；阶段9干净工程导入另行授权，未实施。本表不自动授权其他平台或协议变更；§28.2的31项表及各历史子批中“Player未验/硬拒绝”保留为当时快照，当前结论以本节与§30为准，不重开或重计审计。

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

## 29. Workbench UI/UX 总体设计（2026-10-17）

### 29.1 授权、依据与交付状态

- **目标**：把已有正式资源能力组织成 Unity 原生、以数据列表和明确任务为中心的工作台，不是给四个旧页面换皮。此节是设计与后续实施依据，不是本轮 Code 授权。
- 起点实读：分支 `fusion/8-resource`，HEAD `8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，提交标题与用户基线一致；暂存区为空。已有 `.gitignore`、`.kilo/kilo.jsonc`、NuGet/McpPlugin、Packages 及并行 UI Plan 等变更不接管、不回退；不要求工作区干净。
- 工程版本来自 `ProjectSettings/ProjectVersion.txt`：Unity `2022.3.62f3c1`。读取了当前 AGENTS、`Documentation/IntegrationPlan.md` 的 XF × YL 协作/边界约定、本 Plan §27、能力表、§28.11 最终 F03/1175 记录、§28.12，以及 `Documentation/Modules/Resource.md`。不重开31项审计、不改其他批次验收。
- 本节路径前缀为 `Assets/YLFramework/`：`Service/`、`Data/`、`Build/`、`Publishing/` 简写相对 `Editor/Systems/Resource/`；`Diagnostics/`、`Updates/` 相对 `Runtime/Systems/Resource/`，ResourceSystem/ResourceConfiguration位于该Runtime根，未写目录的同模块文件按符号定位。行号为本轮工作区读取位置，执行时重定位。Editor UI/Operations/Build/Publishing均在 `YLFramework.Editor`，保持Editor-only asmdef，不拆新前端程序集。
- `.kilo/plans/1791960935353-ui-panel-redesign.md` 仅只读核对：它处理 Runtime UGUI `UISystem/UIPanel/Layer/Canvas`，不是本 Workbench；没有覆盖它，也不进入或扫描其他 worktree。
- 四张截图作为现状视觉证据，源码作为行为证据。本轮没有实时测量 Editor 字号、显示器 DPI、停靠布局或进行 GUI 操作；现有只读 CLI 探测受权限限制后未尝试绕过，也未启动另一个 Unity。以下尺寸是 GUI point 的设计起点，不是截图像素测量或已完成 Unity 验收。
- 本轮只修改原 Plan，不改源码、配置资产、样例、测试、Resource.md、AGENTS、依赖及工具配置，不运行构建、发布、部署、清理、Runtime 初始化、原游戏或历史技术测试。不 Commit/Push/Merge/Tag/切分支/Reset/Clean/Stash/Rebase。

### 29.2 可见问题与源码对应

| 可见问题 | 源码原因 / 已确认边界 | 设计处理 |
|---|---|---|
| 定义、Group、资源和详情挤在上半部；宽屏仍是窄列表 | `Service/ResourceDefinitionView.cs:68-108,117-143,180`：Project字段在前；Group宽145/160；侧详情模式下条目宽为38%并夹在290～440；列表高度为窗口28%并夹在125～280。窄窗已有详情下置，但列表未利用剩余高度 | 项目设置移入上下文面板；资源表占满剩余高度，Group与详情可调整/折叠，不重复发明已有窄窗能力 |
| 勾选、高亮、构建包含关系不直观 | 同文件 `:18,29,229-249,284-331`：单条 `selected` 与批量 `checkedEntries` 是两套UI状态；真正纳入构建的是 `Project.Groups[].Selections`，不存在“未勾选就不构建”合同 | 去掉无标题勾选列，改原生Ctrl/Shift多选；单行详情、批量摘要与已定义/只读解析结果明确分开，不新增Enable字段 |
| 分包页是横向长条与文本 | `Service/ResourceBuildView.cs:72-124`：包为横向Toggle，源文件为可定位链接，依赖/依赖者为文本；已有缓存筛选和可见行绘制，缺的是列、排序与更明确层级 | Bundle多列表格 + 选中详情/依赖树；逻辑Group是来源/过滤维度，不假定与物理Bundle一一对应 |
| 配方、策略、所有执行/交付动作权重接近 | 同文件 `:127-237`：Recipe跳Inspector，Project策略与全部动作连续纵排；`ResourceBuildRecipeEditor.cs:12-29` 自身也带执行按钮 | 构建与版本交付分开；配方字段内联，Project策略只读显示来源；一个明确主操作，其余按任务分层 |
| Diagnostics 未启动时只有说明 | `ResourceDiagnosticsView.cs:25,117-175` 已绘制 Projects/Assets/Bundles/Instances/Scenes/Updates/History 七区；`ResourceDiagnosticsReport.Content` 已有数据但尚未绘制。截图仅对应 `ResourceSystem.Current` 不可用，不是诊断功能不存在 | 正常空状态 + 三种数据来源选择；补齐已有内容字段的显示，不新增采样账本或自动 Start/Initialize |
| “已更新预估分包”出现在其他页顶部 | `ResourceWorkbenchView.cs:17-19,28-35,54-75,140-168`：status为窗口级；切页不清，SetProject清空。成功提示未附项目/来源/时效；截图不能证明跨项目污染 | 成功提示回到所属页，操作结果带项目/操作/时间；全局仅显示确实在执行的任务或未读错误摘要 |
| 放大/缩小文字似乎能解决拥挤 | `ResServiceEditorWindow.cs:28,42` 的实际 `minSize=620×440`，正文多用EditorStyles；截图经过缩放，不能推导真实字号 | 先调整区域分配、行密度和信息层级，再用真实主题/DPI验收字号 |

### 29.3 明确推荐：一个项目上下文、五个任务入口

推荐保留 `YLFramework/Resource/Workbench` 和现有 Project 资产入口，取消每页常驻的 PROJECTS 大侧栏，改为可搜索的 Project 选择器；不是新增概览/Dashboard。导航为 **资源 / 分包分析 / 构建 / 版本交付 / 诊断**。当前项目与保存状态始终可见。

| 入口 | 职责与主要动作 | 既有能力映射 |
|---|---|---|
| 项目选择器 / 项目设置 | 搜索、创建、定位 Project；低频编辑名称、专用根、稳定 ProjectId、压缩与加密策略 | `ResServiceEditorWindow`、`ResourceEditorOperations.CreateProject/SaveProject`、`ResourceProject` |
| 资源：条目 / 图集 | Group、选择定义、目录/子资产、地址/alias、拖放、批量操作；图集规则和计划 | `ResourceDefinitionView`、Operations 编辑接口、B05 `PrepareAtlases`、B06 Generator/Plan |
| 分包分析 | 当前定义预估 / 确切构建；包、源文件、分包原因和双向 Bundle 依赖 | `ResourceBuildMap`、`ResourceBuildResult`，不计算第二套依赖图 |
| 构建 | 可选 Recipe、有效输入、正式预检、构建/发布及本次分阶段结果 | B02 `ResolveRecipe/ExecuteRecipe`；无配方原 BuildOnly/BuildAndPublish/PublishBuild |
| 版本交付 | 显式读取 owned 历史与当前 Index；选定版本后导出、签发、C02 内置部署/恢复 | Publisher、ExportRelease、Signer、BuiltInDeployer；常量生成仍走原完整发布集合 |
| 诊断：运行会话 / Store 查询 / 报告与开始使用 | 会话只读快照；明确 root/闭包的 E05；已选构建报告、F03参考及样例文档定位 | GetDiagnosticsReport、QueryArtifactsAsync/QueryAsync；不创建会话、benchmark 或清理入口 |
| 底部“任务与输出” | 当前操作、阶段、错误、复制详情；有界构建尝试选择与最近结果，可折叠 | 保留现最多32次尝试，改造原一次性 `EditorApplication.update` queue 的显示，不另建任务系统 |

顶栏显示 `[Project 名称 v] [未保存] [保存 Project] [项目设置] [帮助]`。Project 选择器列显示名称 + ProjectId，展开详情可看 GUID/路径；同名项目不混淆。创建只请求新路径，后续逐步配置，不自动填入或接管既有根。

项目设置在宽屏为限定宽度的上下文面板，窄屏为可返回的整页。根、ProjectId 与策略不常驻资源表上方；稳定身份附持续可见的影响说明和失效提示，不靠改名承诺迁移历史。修改仍只是原 SerializedObject/Undo，不建立草稿配置副本。

### 29.4 资源与图集：浏览优先，编辑按上下文

以下所有线框中的项目、数量、版本和结果均为**演示**，不是本轮运行结果；比例表示区域责任，不表示逐像素实现。

```text
资源（宽屏演示，Group约16% / 资源表约56% / 详情约28%）
+--------------------------------------------------------------------------------------+
| Project [Demo Resources v]  未保存   [保存 Project] [项目设置]                  [帮助] |
| [资源]  分包分析  构建  版本交付  诊断                                                 |
| [条目] 图集     搜索[路径 / 地址 / alias................] [筛选 v] [添加 v]            |
+----------------+----------------------------------------------+----------------------+
| GROUPS     [+] | 选择定义 [当前Group v]    [解析结果：只读]     | 当前：demo.prefab    |
| all            | 名称             类型      地址      状态    | 来源 / Group         |
| > ui           | > UI/            目录      自动地址  已定义  | 源路径 [复制][定位]  |
|   audio        |   demo.prefab    Prefab    ui/demo   已定义  | 地址 [............]  |
|   scenes       |   demo.png       Texture   icons     已定义  | alias (2) [展开]     |
|                |                                              | 子资产 / 技术身份    |
| [Group 设置]   | 表格占满剩余高度，可排序、滚动、多选           | 多选时改为批量摘要   |
+----------------+----------------------------------------------+----------------------+
| 选中 3 项  [移动到 Group v] [从定义移除]        定义 24 项 / 匹配 8 项 / 清除筛选        |
| > 任务与输出：Project / 操作 / 状态 / 时间                                            |
+--------------------------------------------------------------------------------------+
```

- 默认“选择定义”是可编辑的数据层，目录是一条定义，不展开成数千条可删除的伪定义。“解析结果”读取明确计算所得 BuildMap/子资产描述，只读，标注来自内存未保存定义或某次保存状态；提供“定位来源定义”。未计算时不扫描目录补数量。
- 移除现有无标题批选 checkbox；单击选中，Ctrl/Shift批量，焦点项是键盘/详情上下文。单条显示字段，多条只显示计数、所属范围和移动/移除动作，不静默批改地址。切Group清多选；筛选保留未显示的选择时明确写“共选N，当前可见M”，可一键清除，批量摘要按真实完整集合。`全部Group`只读聚合检索/定位，进入具体Group才编辑/拖入/批量移动移除，复用原单sourceGroup原子操作，不新增跨Group批编辑能力。
- 列默认名称、类型/目录、地址、定义状态；宽屏可加路径/Group，技术 GUID/localId 放详情或可选列。行高亮只代表 UI 选择；所有有效 Selection 都参与正式解析，添加/移除才改变定义，不新增“勾选参与构建”的底层行为。
- 添加仅登记文件/目录GUID，目录可改Extensions，文件可改地址/alias；`ResourceSelection`没有Kind或独立子资产选择字段。子资产只在解析结果中按原 `DescribeSubAssets`/Catalog只读派生，显示name/localId/type并可定位；地址/alias继承主文件规则，源描述与真实构建验证结果分开。按单个Sprite挑选/改身份属于新数据能力，本专题不新增，也不做透明Atlas绑定。
- 拖放文件/目录到明确Group的资源区；悬停显示目标与待加入的文件定义，非法来源显示原因。只调用现有 `AddSelections`，拖入子资产仍按原API归到主文件GUID，预览清楚写“加入主文件及其派生子资产”，不声称仅选一个Sprite。所有事件和快捷键同样遵循Play/编译/导入/执行门禁，不只禁用可见按钮。
- Group 新增/改名/Packing/删除仍调用原 Operations；设置按上下文呈现。`从定义移除`可 Undo，不删除源文件，不逐次弹窗；`删除 Group`确认“删除该组及 N 条选择定义，不删源资产”；Workbench 不提供“删除真实源资产”。B06 失效输出的真实删除另列危险动作。
- Ctrl+S 只保存当前明确编辑的 Project 或规则资产，按钮始终写明对象；多处 dirty 时分别显示，不调用 SaveAssets/SaveOpenScenes。Ctrl+F 聚焦当前表搜索，Alt+G 新建 Group，Esc 取消当前输入/清搜索或清多选（按焦点顺序），Undo/Redo 保持原原子编辑与调用合同。

**图集子页**：不新增占满屏幕的独立向导。

```text
资源 > 图集（演示）
+--------------------------------------+-----------------------------------------------+
| [自动规则] [显式 Atlas]               | 规则 / Atlas 详情                             |
| 自动配置 [DemoAtlasRules] [定位]      | 自动：来源、分组、地址、打包设置（同一资产）   |
| 状态       地址          Group       | 显式：资源路径、合同检查、[在 Inspector 编辑]  |
| 待创建     atlases/menu   ui          | 输出路径 / 输入来源 / 完整身份                 |
| 待更新     atlases/icons  ui          | 计划时间；已过期时不宣称当前有效               |
| 当前有效   atlases/common ui         | [定位输出] [复制路径]                          |
| 已失效     atlases/old    ui          | 失效输出：[检查并删除...]（危险、需确认）       |
+--------------------------------------+-----------------------------------------------+
| [保存规则] [只读预览]   [生成 / 更新并准备]   显式：[准备本 Project 图集]                |
+--------------------------------------------------------------------------------------+
```

- 未登记时提供 `登记自动图集范围...`，调用原 CreateConfiguration，只新建不存在的自有子目录。配置缺失/归属损坏是错误，不降级成“未配置”。不允许 UI 直接改隐藏 owner/输出登记字段。
- 自动规则在工作台内选择与编辑公开字段；使用同一 SerializedObject 小型字段绘制供工作台/Inspector 复用，执行按钮归各宿主，不能直接嵌入带执行副作用的完整 `OnInspectorGUI`。复杂 Unity SpriteAtlas Importer/平台打包设置仍用原生 Inspector，不重写其整套编辑器。
- Create/Update/Current/Obsolete 对应“待创建/待更新/当前有效/已失效”，保留英文状态 tooltip。Preview 只读；Generate 实际生成并准备；显式 PrepareAtlases 也会先执行 B06 Generate，按钮旁清楚说明，不能误称只准备手工 Atlas。
- Obsolete 保留可见，不自动删除；DeleteObsolete 继续复验归属、字节/meta 与引用，拒绝冲突。生成回滚失败显示主错误和恢复错误，不用旧 Atlas 假装新构建成功。不接管外部 Atlas/Importer，不 PackAll、不晚绑定。

### 29.5 分包分析：先识别来源，再解释物理分包

```text
分包分析（演示，列表约60% / 详情约40%；需要时详情可切到底部）
+--------------------------------------------------------------------------------------+
| 来源 [当前定义预估 v]  尚未计算/已过期/完成于...   [计算预估] [查看确切构建 v]          |
| 搜索[包名 / Group / 源路径.......] [Placement v] [仅共享]                              |
+--------------------------------------------------+-----------------------------------+
| Bundle（友好摘要 + 唯一后缀）  来源  源字节 依赖数 | ui / Together 的物理包           |
| ui · Asset · ...ab12          Group  演示值  2    | Placement：Group                  |
| shared · ...cd34              Shared 演示值  0    | 完整 BundleName [复制]            |
| scenes · ...ef56              Scene  演示值  1    | [源文件] [依赖] [被依赖] [构建值] |
|                                                  | Main / Dependency / Shared        |
| 可排序；Group不是固定树父节点；共享包不重复列出   | > 依赖包...  [定位到包]           |
|                                                  | 选择定义/图集规则来源 [定位]      |
+--------------------------------------------------+-----------------------------------+
| 源文件大小 != 交付文件大小 != native memory；本次数据来源与身份始终可见                |
+--------------------------------------------------------------------------------------+
```

- 默认以物理 Bundle 为一行，按 Placement、Scene、Shared、Group 来源、源文件数与 SourceBytes 过滤/排序；友好摘要由结构化字段生成，完整技术名始终可复制，不依赖截断后的哈希猜身份。共享/环归并包可关联多个逻辑 Group，不伪造一个“唯一父 Group”。
- 源文件 tab 列角色、路径、主资源地址、来源定义；主资源来自 Map.Assets，其他 AssetPaths 是依赖源，Shared 是包的共享归属属性，不假设每个文件都有可直接 Acquire 的地址。点击定位源资产或来源定义，不因选中反复 FindAssets/GetDependencies。
- 依赖/被依赖用现有 Bundle DAG 按需展开树和列表；同节点多路径允许再次引用，标明重复节点并允许跳转。只解释当前正式图的层级，不上图编辑器、不新增依赖计算系统。没有正式逐文件因果边时写“此包依赖闭包的源文件”，不编造“由某一具体 Prefab 引入”的链路。
- Placement 显示原 Group/PerAsset/SharedDependencies/SceneDependencies/CycleMerge 及简明说明；真实来源定义/自动规则追溯需要在原 BuildMap 构造中保留只读 provenance（见 §29.10），不能拿后来已修改的 Project 反推某次旧构建。缺少历史 provenance 时显示不可追溯，而不是按现配置补写。
- 当前预估来自显式 `ResourceBuildMap.Create`；Map不拒绝内存dirty，故标签必须区分“含未保存定义”。自动图集待生成时显示计划与“最终分包尚不可用”，不能用旧输出表示新规则已完成。确切构建以 `ResourceEditorBuildReport` 为报告来源：时间/Invocation在report，Map/指纹/文件在report.Build；无配方Invocation可空，由操作上下文标注本次输入。定义改变只使报告“不代表当前定义”，不修改原事实。
- 预估只显示 SourceBytes、避免重复源字节估计等已有量；实际 raw FileBytes/DeliveryFiles.Length、SHA、ContentChanged、native调用/缓存原因/阶段耗时只在确切构建数据中出现。无基线的 ContentChanged 显示“未知”，未测/不可用显示 `未计算`/`不可用`，不补0。
- raw、delivery、manifest 旁车分别统计；`ResourceBuildResult.CopiedBytes` 恒0，不作为复制实测；本次发布复制量读 Release.CopiedBytes。历史 ReadHistory 重建的复制量/耗时不是历史实测，不能把其默认0显示成当时零成本。

### 29.6 构建：有效输入、一个主动作、独立阶段结果

```text
构建（演示，输入区约55% / 检查与最近结果约45%，不用全页三栏）
+--------------------------------------------------------------------------------------+
| 执行来源 [Demo Recipe v] [不使用配方] [新建配方] [定位]                               |
+-----------------------------------------------+--------------------------------------+
| 本次有效输入                         来源     | 就绪检查 / 正式预检                  |
| Project / 资源根 / Group             Project  | 未保存：Project（阻塞）[定位][保存]   |
| 压缩 / 加密模式 / KeyId              Project  | 活动目标与Recipe目标不符（阻塞）     |
| Target / App / Action / Force        Recipe   | key：尚未正式预检，不探测秘密         |
| 常量生成选项 / 目标目录              Recipe   | 图集需生成：最终预检尚未完成          |
| [内联编辑执行选项] [保存 Recipe]              | [正式预检]                           |
| Project 策略只读 [编辑 Project 策略]          | > 查看完整检查/帮助                  |
+-----------------------------------------------+--------------------------------------+
| [执行配方：构建并本地发布]（主） [更多 v]    不部署 / 不上传 / 不激活                  |
+--------------------------------------------------------------------------------------+
| 本次结果：构建 成功 | 发布 成功 | 常量生成 失败（演示，不统称全部成功）                |
| Project / Target / App / Recipe / 指纹 / 开始时间 / 总耗时                            |
| [分析本次构建] [查看确切发布] [定位失败阶段]                                          |
| > 任务与输出 [本窗口构建尝试 v]   排队时[取消排队]；开始后同步执行，不可交互取消        |
+--------------------------------------------------------------------------------------+
```

**配置与主操作规则**：

- 无 Recipe 时保留原调用方式：目标只读为已活动 Editor 目标，App/Force/生成选项为本次非共享执行输入；默认主操作“构建并本地发布”，相邻次操作“仅构建”。Project 仍唯一持有定义/Compression/ContentEncryption/KeyId，不新增工作台执行配置资产。
- 有 Recipe 时内联编辑其真实字段，主按钮明确随 `Action` 显示“执行配方：仅构建”或“执行配方：构建并本地发布”，只调用 ExecuteRecipe。不得用“仅构建”按钮临时覆盖 Recipe.Action；需要另一操作时显式改并保存 Recipe，或切换“不使用配方”。
- Recipe引用其他Project时显示两个真实身份与“切换到配方Project / 清除配方”；执行阻塞，不改Recipe.Project凑当前页面。手动切Project时不匹配配方解除选择并提示，不修改资产。现窗口监听Selection只是Repaint，不新增隐式跟随任意选择；显式菜单/资产入口与SetProject统一经过排队取消/执行锁定门禁。
- 有效输入表从 ResolveRecipe 或原 Project/本次参数投影；每项带来源，可定位而不复制政策。编辑 Project 策略会使 Project dirty；保存 Recipe 不保存 Project/图集/场景。普通查看、选配方、重绘不触发 ContentProvider。
- 静态就绪检查只读非秘密配置/dirty/目标/Editor转换状态，不冒称完整 Preflight。明确点击“正式预检”才调用正式服务，且注明“加密项目会通过既有来源临时取 key 进行检查”；不保存/回显秘密。正式执行仍重新冻结、预检、校验与后验，UI 显示的上次绿色不是执行许可。
- 图集待生成的预检提前返回必须显示“前置步骤待完成 / 尚未最终检查”，不是通过。缺 key、目标不支持、打开场景 dirty 分别给出下一步，不自动取环境值试错、不切平台、不保存场景。密钥仅说明来源名称与 KeyId，绝不提供 key 输入框。

**辅助动作与结果**：

- “发布已验证构建”仅针对当前明确选中的真实 BuildResult，显示 Project/平台/指纹/Workspace；输入已变时提示它是旧结果，服务仍复验并有权拒绝，不用预估替代构建。BuildResult 不持久化为可恢复任务，重载后不猜 raw 目录构造它。
- BuildError / PublicationError / GenerationError 与阶段计时保持独立。生成失败后发布仍成立，结果为部分成功；提供原常量生成操作入口，不自动重复发布。发布异常若提交未确认，显示“发布结果未确认，请显式恢复确认”，不武断断言 Index 未变或自动重试。
- 保留现有最多32次构建尝试的能力（`ResourceBuildView.cs:39,238-263`）：任务输出提供尝试选择器，含失败记录、操作时间、原Project与可用指纹/digest；选择某次report即可进分包分析/结果详情。新窗口内总量仍有界32，按Project身份过滤，不创建持久历史账本；关闭/重载后清空，不从raw目录伪恢复。
- 正常明确按钮不反复弹模态确认；首次执行可展开的范围摘要说明 owned Workspace/Publisher 与是否生成常量。部署、签发导出、恢复和删除则各自需要其确切目标/范围确认，不能随主动作捆绑。
- 阶段拟为“正式预检 → 图集前置 → 指纹/冻结 → native构建 → 读回/交付复验 → 本地发布 → 可选常量生成”；只在真实阶段接点记录。任务条仅在实际可重绘边界更新，同步段显示最后已确认的执行阶段和“不响应交互”，不承诺逐段实时动画/百分比/剩余时间。当前无完整阶段通知，需补小型观察接点（§29.10），不是重写native执行。
- 耗时严格标来源：report.PreflightMilliseconds是正式服务预检；build.PreflightMilliseconds包含取key/图集生成/Map/设置指纹/旧产物验证等组合准备；build.MapMilliseconds也含图集/准备及后验，不冒称单一算法纯耗时。原有native/Hash/readback/编码/发布/生成字段照实展示；不同层级可重叠，不能相加凑总耗时或用组合值伪造缺少的阶段测量。
- GUI仅允许开始前“取消排队”。排队时切Project先取消原callback并提示，再切换；操作开始后锁Project/Recipe与写入口，主线程同步执行期间没有可靠的窗口取消点击接点，按钮禁用并明示“本操作执行中不可交互取消”。脚本API原有token检查点保留，但不据此承诺GUI随时可取消；native不可抢占，Publisher/Deploy提交后须原Recover确认。E05分帧查询的取消是另一条已有可交互路径。
- 不把 Unity API/native 构建放进 Task.Run，不做新异步任务引擎。只改反馈与解释，不删除指纹/后验、SHA/认证、StrictMode 或六项 callback 门禁；F03 的成本结论不是 UI 重写底层构建的理由。

### 29.7 版本交付：先选确切版本，后选明确操作

```text
版本交付（演示，历史列表约45% / 选定版本详情约55%）
+--------------------------------------------------------------------------------------+
| Project / Target   [读取已登记历史] [读取当前发布]   筛选 App / Unity / digest          |
+------------------------------------+-------------------------------------------------+
| CatalogDigest     App   文件/字节   | 选定发布：完整digest [复制]                     |
| > ab12...98ef   app-demo  演示值    | BuildId / Platform / Unity / App / 加密与KeyId   |
|   cd34...76ab   app-demo  演示值    | 路径 / Catalog / Complete / 登记文件             |
| “当前发布”只在读到Index后标记      | 当前Publisher Index：相同/不同/未读取            |
| 无创建时间则不设“最近”或时间列    | Runtime active/LKG：此页不推断，[到诊断查看]     |
+------------------------------------+-------------------------------------------------+
| [导出发布副本...] [签发此版本...] [内置部署...] [定位发布]                             |
| > 高级：发布事务恢复 / 内置部署恢复 / 从完整已发布集合生成常量                          |
+--------------------------------------------------------------------------------------+
```

- ReadHistory 只取 owner.KnownVersions 的完整版本；调用逐版本验证/SHA，必须显式触发、显示成本/读取中，不在选页/绘制/排序时反复执行。现 API 遇一个坏版本会整次失败，保留明确错误，不把失败说成空历史；本设计不新增容错枚举/目录扫描。
- 没有正式发布时间和序号历史时，按 digest/App 等可用字段排序；不拿目录 mtime、digest 顺序或本窗口打开时间冒充“最新发布”。本次操作时间可显示，但明确不是历史发布元数据。不同 App/Unity 可见且标为兼容不匹配，不静默隐藏成不存在。
- `raw workspace` 是可变构建缓存；Publisher release 是 owned 不可变发布；导出是工程外新目录的副本；C02 是 StreamingAssets 的精确部署；Runtime active/LKG 属会话/Store。名称、路径和按钮始终使用这些概念，不用一个含混“发布/上线”串联。
- 导出调用原 ExportRelease，先选择已有本地父目录，再明示即将新建的完整 digest 子目录；不覆盖、不同步 Runtime，不把框架 unitypackage 导出放进资源版本操作。
- 签发为薄便利入口：让现 SigningWindow 接受一个明确 ResourceRelease/完整身份，执行前按原 ReadVersion 复验，不再悄悄 ReadCurrent 换版本。保留 channel/sequence/keyId/有效期与“渠道支持密文”确认；秘密仍只从原工程外来源获取。sequence 由操作者明确提供，不自增、不默认宣称1安全，不建设签名历史账本、渠道平台或私钥管理。
- C02推荐纳入薄UI接点：先 `Preflight(selectedRelease)` 展示Source/DirectoryPath/WorkspacePath、完整身份、PayloadBytes与IsRepeat，再确认 `Deploy(plan)`；服务继续重新Preflight，不把旧Plan当授权。确认必须列完整自有写入集：`AssetBundles/YLBI_<token>/` 的owner、deploy.lock、journal/next与私有stage；`Assets/StreamingAssets/YLFrameworkResources` 下新增的root/Project/Platform/digest外壳、owner/布局控制文件、`payload~/`；以及原AssetDatabase.Refresh带来的对应文件夹.meta/导入副作用。不是只写payload，也不自动覆盖/删除旧版本或Activate（`ResourceBuiltInDeployer.cs:16-36,129-194,243-247,348-354,397-402`）。
- 部署预览的PayloadBytes只是复制文件逻辑量，不是磁盘峰值或临时空间保证。新部署先有一份stage副本，源Publisher版本仍保留，提交是Directory.Move而非再复制一份；仅源+本次副本就约两份payload，外壳/meta/已有旧版本另计。分开显示“待复制payload / 临时workspace / 最终目标”，未测可用磁盘容量不画“空间足够”的绿勾；重复完整部署显示原IsRepeat复验语义。
- “发布事务恢复”和“内置部署恢复”是不同动作，分别显示 ProjectGuid/ProjectId/目标与写入范围；现无通用 pending 状态查询，未查询时写“恢复状态未知”，按正式失败回执或 C02 预检错误给出恢复建议。不得解析异常文案猜 journal 阶段或自行读写控制文件；明确 Recover 由原 API 验证，不把它称作历史回滚。
- 本设计不新增“把任意历史发布设为当前 Index”、列举全部部署或签名历史、磁盘清理、上传、Runtime 激活等不存在的 UI 能力。常量生成明确仍从匹配 App 的完整 owned 已发布集合读取，不声称只生成当前选中 digest。

### 29.8 诊断：会话、磁盘与历史报告互不冒充

```text
诊断（演示；运行时为空不是错误）
+--------------------------------------------------------------------------------------+
| [运行会话] [Store 只读查询] [报告与开始使用]                                          |
| 来源：当前应用会话 / 窗口会话标签 / 采样时间 [采样] 自动采样[关闭 v]                   |
+--------------------------------------------------------------------------------------+
| ResourceSystem 尚未初始化                                                           |
| 本面板不会创建会话、加载资源或运行游戏。应用明确启动后再采样。                         |
| [打开 Resource 文档] [定位 ResourceStartup 样例] [切换到 Store 只读查询]               |
+--------------------------------------------------------------------------------------+
| 已启动时：责任摘要 / 资源与Bundle / 实例与Scene / 项目与版本事务 / 内容输入 / 请求历史   |
| 可排序明细表：状态 / owner / producer / waiter / root pin / 入边 / 责任原因            |
| 选中详情或依赖路径；无 Destroy/Unload/清理/激活按钮                                   |
+--------------------------------------------------------------------------------------+
| Store页：来源 / 绝对root / Project+Platform闭包 / App(输入) / 扫描步骤上限 [查询]        |
| Status / 起止时间 / 完整性 / observed逻辑字节下界 / 候选是否有效 / issues              |
| Kind / State / Project / 路径 / 字节完整性 / Protection / 保护原因                     |
| [取消本次只读查询] [复制报告]  无删除按钮                                             |
+--------------------------------------------------------------------------------------+
```

- 运行会话报告保留当前七区的完整能力：Projects 的挂载/兼容/身份，资源owner/producer/waiter/queued，Bundle root pin/入边，实例/Scene责任，Updates的阶段、传输/重试、激活项目与RecoveryNotes，请求历史。全会话数据标记为全局，可按当前 Project 过滤，但不能把全局计数误称为该项目。
- 补显示已存在的 `report.Content` 预算/allocated/reserved/peak、live input/work、声明native read buffer及CNG分项；直接用同一次报告，不再另采一次内容快照。Bundle内容在途/故障/清理失败、Asset排队/图片缓存资格、Scene记录/native开始与完成等原字段在详情可见；这是显示缺口，不是新增Runtime账本（`Diagnostics/ResourceDiagnosticsReport.cs`、`ResourceContentDiagnosticsSnapshot.cs`）。
- 未初始化为中性空状态；会话退出/替换则标记旧快照并停止自动采样，提示重新绑定，不自动初始化或强停资源。报告没有正式SessionId/Mode；沿现sampledSystem引用相等判断会话，窗口分配短标签仅供本地辨识并明示不是Runtime身份，不反射私有配置。正式资源版本身份仍用报告内SnapshotId/CatalogDigest。文档/样例按钮只打开文件或Ping，不执行样例、创建Scene或注入provider。
- 自动采样默认关，用户显式选择后先提供原5s频率；页面不可见/窗口关闭/Play转换/编译导入时暂停，回来显示“已暂停，点击恢复”，不默默重开。请求历史默认关闭，但不覆盖应用已有设置；显示采样时HistoryCapacity与时效，提供明确“记录最近128条/关闭”操作。提示须说明ConfigureDiagnosticsHistory会清空旧缓冲、不回填，且切换前已在途请求的完成事件也不再记录；这是重新开始一段历史，不是无损开关。不是每次绘制按旧报告同步设置，关闭窗口不重置应用历史（`Diagnostics/ResourceSystem.Diagnostics.cs:47-74`）。
- **Store默认入口只有一张显式表单**：初始root和闭包为空，由操作者输入绝对root、完整ProjectId/Platform集合及各项目App输入；应用显式传入的现有ResourceConfiguration/ArtifactStore仅作为同表单可选预填来源，确认后才查询。Workbench Project/活动构建目标不能推导应用Store配置，不扫描persistentDataPath、不采用ArtifactStore.DefaultRoot，不把查询输入变成Runtime配置权威。
- ResourceSystem/ResourceStartup 都没有公开配置getter；不反射取私有字段、不为UI新增Runtime getter。可选预填需要小型Editor-only会话参数接点，由应用Editor代码显式转交其保留的配置，仅复制root/完整mount闭包；Runtime程序集不反向引用Editor。无接入也可用默认手工表单（`ResourceSystem.cs:45-59`、`Integration/ResourceStartup.cs:15-30`、`ResourceConfiguration.cs:15`）。
- 输入只在窗口会话存留，关闭/重载丢弃，不新增SO、不写Project/Recipe、不保存provider。只复制root与ProjectId/Platform/App字符串，不保留传入的Store/配置/mount/BuiltIn/Remote对象；用纯值重新构造仅查询所需对象。root须符合原私有目录限制；owner验证的是完整ProjectId/Platform闭包，**不验证App**。App是mount必需输入，UI标“操作者输入/应用提供，未经Store owner校验”，Ready也不是App匹配或内容兼容认证；不为UI再读Catalog补成未经授权的新验证（`Updates/ArtifactStore.cs:38-55,110-151`）。
- 查询不要求BuiltIn快照/digest，不能把Publisher目录当Store。临时ArtifactStore不是第二ResourceSystem authority，原Query仍按同root采集当前及排空旧会话的保护；不同root未见持有不等于全局无持有。界面始终显示保护观测的root与来源，不把App输入或空运行态保护当认证（`Updates/ResourceSystem.Artifacts.cs:20-44`）。
- 查询用原 IEnumerator 的分段机制，在Editor主线程update驱动；只允许一个在途查询，显示“只读查询中”而无伪百分比。`maxEntries`标为扫描步骤上限（默认10000），不是最多返回多少行。取消/离页/关闭/切换来源或Project必须Dispose枚举器；直接中止可能无新报告，旧结果明确保留原身份。不创建MonoBehaviour/协程宿主、root/lock/目录，不读取key、不加pin、不自动重试；不能调用有恢复副作用的GetPreparedUpdates。
- Ready/Missing/NotConfigured/RecoveryRequired/Busy/Changed/Incomplete/Canceled/Invalid/Unreadable 等保留不同空态/错误语义；RecoveryRequired 只提示应用通过既有恢复流程处理，本页不放恢复/清理按钮。CompleteRecord 不是 Bundle SHA/可挂载性/签名新鲜度认证。
- `ObservedBytes` 为逻辑观测值；`IsSizeComplete=false` 显示“已观测至少...，不完整”。`CleanupCandidateBytes=null` 或非Ready显示“不适用/结论失效”；Ready下明确有效的0仍显示0，不混淆未知与真实零。Protection组合和每项issues保留，Changed/Busy清楚说明结论不能用于操作。此页不提供CleanupVersionsAsync，不把旧候选预览变成清理授权。
- “报告与开始使用”可选本窗口某次确切构建；F03只提供已有Resource.md“代表性成本与复跑”和测试/准备脚本的精确路径式定位，不做外部证据浏览器，不读/扫描Temp或解析Plan。环境/负载/mode/n/资格限制由原文档呈现，性能数字不硬编码为实时指标，Unavailable不补0，不调用测量入口。Editor不引用Tests类型，缺少参考文件就显示不可用，不查找替代目录。

### 29.9 视觉规范、窄屏与关键状态

**视觉与密度**：使用 Unity 原生表格/表单语言，少量边框与一个选中强调色，无大卡片、渐变、装饰图表或商城式按钮。

| 项目 | 具体规范（GUI point，真实GUI成形时允许按主题校准） |
|---|---|
| 文字层级 | 项目/页面标题约14、加粗；正文与可编辑字段沿原生 EditorStyles 字体/字号；辅助信息用同字号较低对比度，不靠极小字降权；GUID/digest/路径是可选择技术文本，不强制引入外部字体 |
| 行与命中 | 数据行 `max(22, singleLineHeight+4)`，工具条26～28，主要按钮26，普通控件至少原生行高；图标命中至少20×20，拖动分隔命中6；不为窄屏压低字号/点击区 |
| 间距 | 4为基础：字段间4、区域内8、区块间12、页面外边距8～12；标题后8；边框1，使用低对比度原生分隔 |
| 表单 | 常规 label约110～140，字段随区宽；长表单可读宽约480～640，不无限伸到屏幕右端；详情默认320～400，可拖宽；窄区标签改上置 |
| 表格 | 固定行高、原生列头、可调列宽/隐藏次要列、细交替底纹；不自动把所有列撑满，名称/地址吃剩余空间；数据表不套第二层无必要滚动条 |
| 状态 | 选中用原生主题蓝，失焦选中用原生弱化态；hover只轻微提亮，不伪装选中；键盘焦点可见。状态必须有文字/图标，不只靠红绿 |
| 操作权重 | 每任务区一个主动作，以位置/文案/少量强调区分；只读/定位/复制为次操作；危险删除/恢复单独组并列明范围，禁用时旁边可见原因，不仅tooltip |
| 主题/DPI | 按 isProSkin 与原生样式构建窗口级副本，深浅主题都验；不修改全局 EditorStyles。按 pixelsPerPoint 使用 GUI point，不二次放大，换DPI后重建必要缓存 |
| 长身份 | 默认保留路径尾段/地址关键段；digest可用首12+末8，但近似值碰撞时扩展或看完整值。详情、复制按钮/上下文菜单提供原值，复制不复制省略号；签发/部署确认必须可查看完整身份 |
| 中文用词 | 统一“资源工作台、资源、选择定义、解析结果、分包预估、确切构建、本地发布、版本交付、只读查询、待恢复确认”；保留 Project/Group/Bundle/Recipe/KeyId 及正式API/错误名，不建设框架多语言系统 |
| 帮助与告警 | 常规合同收进字段提示、折叠帮助或文档；阻塞错误、未保存、部分成功、未确认提交必须在对应区域持续可见。成功瞬时提示约6s，之后仍可查所属结果，不将全页顶端HelpBox当日志 |

**尺寸与布局**：

- 宽屏参考 `1440×900`，资源页三块；Project选择不常驻占一栏。分析/版本两块；构建左右输入/检查，诊断依数据一表加详情，不强迫所有页面三栏。
- 普通停靠参考 `1080×720`：Group约160～190，详情约300～340，资源表优先；详情可折叠，次要列默认隐藏，工具条低频动作进入“更多”，主操作与错误仍可达。
- 合理最小目标 `720×480`：Project工具条可两行，任务导航切紧凑下拉；Group改下拉，资源表与“详情”用同区域来回切换并有返回/所选名称；构建区按输入→检查→结果垂直排列，版本详情同样切换。输出默认折叠为约26高，可拖展开。详情有独立纵向滚动，不让主操作落到无出口的横向溢出区。
- 现有minSize为620×440；720×480是本次密度设计目标，不把建议直接写成已验证下限。UX1也回归620×440的临时停靠/滚动可达性，若要提高minSize须在主要布局体验确认时明示影响；不靠缩字解决。最终下限以真实Unity结果为准，不以线框代替结论。

| 关键状态 | 用户看见什么 / 可执行什么 |
|---|---|
| 未选 Project | Project选择/创建/文档；项目任务为空态，不扫描全部资产来填列表；会话诊断可独立查看并标明不依赖Project |
| 空 Group | “尚无选择定义”，提供添加/拖放，不显示资源错误；正式预检仍可因原非空Group合同阻塞，UI不提前放宽 |
| 未计算 | 无伪0计数/包；显示来源和显式计算入口 |
| 结果过期 | 保留旧快照与原时间/身份，标明不代表当前定义；重算/重读需明确触发 |
| 未保存 | 顶栏和实际资产各有dirty，分别保存；执行按原门禁拒绝，不保存场景或无关资产 |
| 执行中 | 原项目/配方/操作身份与最近可显示阶段；同步段不承诺交互刷新或取消，禁止重入与改输入；只读查询分帧取消另列 |
| 部分成功 | 按构建/发布/生成分列；已发布版本仍可定位，不一键重做全部 |
| 失败 | 原阶段/错误码/非秘密因果与下一步；有旧结果也不冒称本次成功，不自动重试写入 |
| 待恢复确认 | 区分Publisher或C02；保持不确定语义并提供范围明确的原Recover，不自动判回滚或清目录 |
| Runtime未启动 | 中性空态，文档/样例定位和E05查询入口；无初始化/运行原游戏按钮 |

### 29.10 技术选型、数据流与必要接入

**推荐继续 IMGUI，但重组前端结构并采用原生多列表格/树，而不是保留当前 GUILayout 堆叠。**

- 当前Views、Inspector、键盘/拖放、一次性queue均为IMGUI；`UnityEditor.IMGUI.Controls.TreeView + MultiColumnHeader + SearchField` 可提供可见行绘制、原生多选、列头排序接点、搜索与依赖树，直接满足任务。排序/过滤投影仍由工作台按正式数据实现，不声称控件自动理解业务。宽窄布局以明确Rect/最小宽度组织，解除现条目宽度上限440和列表高度上限280，而非继续GUILayout堆叠。
- Unity 2022.3 官方推荐复杂 Editor 工具优先考虑 UI Toolkit，且该版已有 MultiColumnTreeView、固定高度虚拟化、SerializedProperty绑定及 IMGUIContainer；本建议不是认定它不可用。这里主要瓶颈是信息组织、来源/失效和执行入口，而非缺少 retained-mode能力；原生 TreeView 的可见行绘制已足够，切换框架不会让同步 BuildPipeline/历史SHA异步。
- 相比之下，完整 UITK 迁移需重建壳/列表/拖放/快捷键/焦点/绑定/重载处理，并验证2022.3与已有CustomEditor边界；直接包现四页 IMGUIContainer 只会保留原问题，整窗两套交互也增加维护。本专题先用 IMGUI 统一完成结构重组，成本集中于真正有收益的表格、上下文和少量服务接点，不引入第三方依赖。
- 唯一有实质取舍的备选是“UI Toolkit壳与虚拟列表 + 纯字段的局部IMGUI容器”，适合后续团队明确要用 UXML/USS 协同维护；业务/操作仍相同。它不是本次默认，不双轨实施、不新增MVVM/主题平台；若用户选它需在UX1前一次性裁定迁移范围，而不是页面逐个随意混用。
- 版本资料：Context7工具本轮未提供可调用入口，改用已读取的官方2022.3文档：[UI systems comparison](https://docs.unity3d.com/2022.3/Documentation/Manual/UI-system-compare.html)、[TreeView API](https://docs.unity3d.com/2022.3/Documentation/Manual/TreeViewAPI.html)、[MultiColumnTreeView](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-uxml-element-MultiColumnTreeView.html)、[IMGUI migration](https://docs.unity3d.com/2022.3/Documentation/Manual/UIE-IMGUI-migration.html)、[pixelsPerPoint](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorGUIUtility-pixelsPerPoint.html)。不拿Unity6资料当本工程能力证据。

**已核对的复用位置**：

| 位置 | 复用结论 |
|---|---|
| `Editor/Utilities/Windows/SplitWindow.cs` | 它是单水平分隔的EditorWindow基类，现Resource窗口直接继承EditorWindow，并未使用它。可参考现拖动/光标/比例逻辑，不为形式复用更换基类；Workbench嵌套/窄窗分隔在局部处理，不改所有Editor窗口 |
| `Editor/Utilities/TreeViews/TreeViewStateGroup.cs:31-49` | 现成可序列化的hash→TreeViewState容器，可复用本地树选择/展开状态；它不负责列宽、EditorPrefs或完整布局持久化。按窗口/Project/视图隔离，不把整数hash当资源身份；不为此扩展通用状态框架 |
| `Editor/Help/HelpStyle.cs`、`HelpWindow.cs` | 深浅色、可复制文本/消息及样式缓存可参考；HelpDraw/Palette多为internal、阅读页装饰不适合照搬数据台，不为此抽一个全框架主题系统。文档入口直接定位现Resource.md，不假设已有Resource帮助模块注册 |
| `Editor/Systems/Audio/AudioInspectorUtility.cs` | 可参考SerializedProperty字段与混合值条件显示；它是Audio内部绘制，不是通用保存/执行门禁组件，不建立Resource对Audio工具的依赖 |
| Resource现Views/Operations/Inspectors | 保留菜单、正式执行与序列化合同；按资源编辑/分析/构建/交付/诊断职责拆分当前BuildView和消息状态。小型共用字段绘制只服务真实重复点，不写通用MVVM或第二任务框架 |

**数据流**：用户编辑同一 Project/Recipe/Atlas配置 → 原 SerializedObject/Operations 与 Undo/dirty → 本地视图失效标记 → 用户显式计算/预检/执行 → 原服务返回只读快照/真实结果 → 前端按操作上下文接收。UI缓存不是资源、发布、配置或会话的权威。

| 接入类别 | 具体边界与必要性 |
|---|---|
| 现有能力，仅重排UI | Project/Group/Selection编辑、保存/Undo、BuildMap/BuildResult表、原构建发布、Publisher历史/导出/恢复、运行诊断；不新增底层语义 |
| 小型UI适配 | Recipe/Atlas纯字段绘制与明确保存；选择/多选稳定映射；report及32次尝试表格投影；C02完整范围Preflight→确认→Deploy/Recover；签名窗接收选定历史发布；E05显式表单、纯值查询对象与Editor IEnumerator驱动 |
| 可选Editor预填接点 | 应用Editor代码显式向当前窗口传入/清除root与ProjectId/Platform/App纯值，仅作同表单预填、不自动查询；不保留配置/provider/mount/BuiltIn/Remote引用，关闭/重载清除，Runtime不引用Editor。默认手工表单不依赖此接入 |
| 必要新增只读报告接点 | 正式预检增加可供UI消费的结构化结果，至少区分“静态阻塞/图集前置待完成/完整预检完成”，支持Recipe的真实Target/App来源；复用原唯一预检控制流，保留旧string入口和原执行复检，不解析成功文案猜状态 |
| 必要新增观察接点 | 在原Editor Operations/Builder的实际阶段边界增加只读阶段通知，复用原发布/部署阶段接点而非暴露测试故障注入。通知不改变执行、锁、校验、取消或提交结果；无接点的细分阶段不显示为已知，不为逐帧刷新重写native流程 |
| 必要新增追溯数据 | 原BuildMap解析时保留有限的Selection/自动图集规则来源副本，供当前预估及该BuildResult定位来源；不持有另一Project配置、不改变Catalog/Bundle身份、不新增资源依赖计算系统 |
| 明确不新增 | pending-journal查询平台、签名/部署历史账本、发布时间schema、发布索引历史回滚、清理、上传/渠道/密钥管理、benchmark、新配置权威或全框架UI系统 |

上述新增接点是后续 Code 范围建议，尚未实施/授权；若实现时发现必须改变公开执行合同、Catalog/序列化身份或新增依赖，应停止该部分并重新确认，不能以UI适配名义扩大。

**失效与异步归属**：

- Project切换：只显示该Project缓存/报告，原消息不贴到新Project。**排队时切换即取消未开始的旧callback；开始后才锁定Project/Recipe与写入口**，所有显式菜单/资产入口一致。同步执行期间窗口可能不响应，不能承诺还能交互浏览其他页；有可重绘机会时可看已有结果，但不触发交叉重操作。应用外部修改仍由正式冻结/后验拒绝，而非假定UI锁能管住全工程。
- Project/Group/选择/地址/alias/策略/图集规则编辑或Undo：当前Map/图集计划/预检变过期；本次Build报告保留原事实并标“与当前定义不同”。只改Recipe执行值使有效输入/预检过期，不无故把资源Map重新计算；Target/导入环境变化则标记相关分析环境改变。
- 外部Inspector/API编辑、AssetDatabase导入/删除及projectChanged：记录廉价修订/保守失效，不扫描；用户明确刷新时重新解析。无事件可证明最新性时写“最近一次显式计算”，不靠UI哈希冒称已冻结。选择、排序、搜索、列宽不引发Map/SHA/native工作。
- 每次排队/查询捕获Project GUID、Recipe身份及解析的非秘密来源、目标、定义修订、操作序号；Runtime以sampledSystem引用相等判会话，E05以root/完整ProjectId+Platform闭包/App输入及查询序号判上下文，不保留外部配置引用。回调只更新匹配视图，旧结果归原上下文，不用当前project重命名。开始仍调用正式冻结/复验，不把UI快照当Invocation。
- 保持原一次性 EditorApplication.update queue；待执行时遇Play/编译/导入/关闭/切换则取消并解除订阅。正在执行的同步服务沿自身finally负责，关闭窗口不是native取消或回滚，不能释放服务拥有的输入。前端观察者失效只停止更新，不改变正式操作结果。
- OnDisable/Domain Reload解除本窗口订阅、取消/Dispose查询、销毁自己创建的Editor/样式资源；不关闭业务startup，不改变历史设置/Session，不丢服务清理责任。重载后只恢复布局/选择，丢失的临时报告明确“需重新读取”，不自动续跑写操作。禁用Domain Reload时同样防重复订阅。
- 布局、筛选、展开、列宽及上次选择放窗口序列化状态/有工程+Project+视图键的本地EditorPrefs；不写入Project/Recipe/图集配置。可共享值仍只在原SO。查询配置/凭据默认不持久化，UI不保存秘密。
- 小数据不分页；TreeView仅绘制可见行。现Definition/Build/Diagnostics已做可见行与部分筛选缓存，重组须保留而非声称从零补虚拟化。行投影/搜索键在数据更新时缓存，较大缓存表可约150～250ms合并过滤输入；依赖树按展开投影。正式上限可达16384资产，不回退到每Repaint绘全部；历史全校验仍显式读一次，不用分页伪装已有增量API。

### 29.11 常见任务的完整路径

| 任务 | 正常操作路径及结果边界 |
|---|---|
| 首次配置并发布 | Project选择器→创建新Project→项目设置明确专用根/身份/策略→资源建Group并添加定义→按需登记图集→分别保存→构建选择可选Recipe或无配方→确认有效输入→正式预检→主构建发布→分别看构建/发布/生成结果→到版本交付选确切digest；不自动部署或启动Runtime |
| 日常改资源后构建 | 资源表搜索/拖放/改地址→Undo可撤销→保存目标资产→旧预估/预检显示过期→可重算分析或直接执行原正式构建复检→结果中解释变化/强制重建原因；未变化SHA不等于跳过native |
| 历史版本导出/部署 | 版本交付显式读历史→按App/完整身份选版本→导出到新digest目录，或C02只读预检→核对目标和范围→明确部署→定位结果；不改当前Index/active/LKG、不覆盖旧版本 |
| 解释分包 | 分包分析选“当前定义预估”并计算，或选择某次确切构建→按资源路径/Group检索→选物理Bundle→看Main/依赖/共享、Placement和来源→依赖/被依赖树定位；源大小与交付大小分列 |
| 缺key/构建失败 | 构建页可见阻塞/失败阶段→看非秘密错误与KeyId/来源说明→应用在既有provider/环境来源修正→显式再预检/执行；不在窗口输入key、不自动下载、降级或清旧版本 |
| 查看运行占用 | 诊断→明确当前Session→手动采样或显式开启5s→按资源/Bundle/owner查看责任与内容预算；未启动就读文档/定位样例，或提供明确Store配置做只读查询，不运行原游戏展示 |

### 29.12 后续实施批次与验收（本轮不执行）

所有批次均须另获 Code 授权。保持原功能基线和现有正式服务；不逐批自动Commit，UI完成并验收后的第二次独立提交仍需明确授权。只补UI及直接受影响服务的测试，不把设计变成重新做阶段8技术认证。

| 批次 | 完成范围与完成条件 | 必要验证 |
|---|---|---|
| UX1 壳与资源主工作区 | 五任务导航/项目上下文、资源原生表、Group/详情、项目设置、保存/多选/消息边界和宽窄切换形成主要布局；其他未迁移页临时复用原功能，不伪称全部完成 | 自有非空数据验证选择/多选/过滤/拖放/地址/Undo/保存、全部Group只读/具体Group编辑；深浅主题及1080/1440/720初验、620旧最小停靠可达性；**此时给用户真实Unity截图及交互结果确认方向，未确认不批量铺开全部页** |
| UX2 分包与图集 | Map/确切BuildResult双来源表、双向Bundle树、追溯只读数据；B05/B06正常入口与规则编辑、状态/冲突/删除分层 | 预估/dirty/过期/空Group与真实构建差异；少量共享/Scene/环归并/子资产数据解释正确；现自动Atlas/显式准备直接回归，不重复完整Runtime Atlas矩阵 |
| UX3 配方与构建结果 | Recipe内联/无配方同等可用、来源表、结构化预检、真实阶段观察、32次尝试选择、同一Operations执行与部分成功/待恢复反馈 | Recipe/Project dirty、Target不符、缺key、图集预检提前结束、真实构建/发布/生成失败；排队取消/切Project取消，开始后不可交互取消的文案；阶段通知不改结果，不重跑F03 |
| UX4 版本交付 | owned历史/确切版本、当前Index标记、导出、选定历史签发、C02 Preflight/Deploy/Recover薄接点与独立恢复入口 | 自有版本真实UI→同一API；完整写入范围、目标/重复/恢复、排队取消与执行中取消不可用。token检查点取消仅复用/定向补自动化测试，不要求同步GUI点击中途取消；不上传/激活、不碰用户StreamingAssets |
| UX5 诊断与整体收口 | Runtime完整字段表、E05明确配置/查询/Dispose、报告/快速开始、全窗口状态一致性和最终可达性 | 无隐式初始化/key调用/额外pin/目录创建；查询非Ready/不完整/保护原因，项目切换/旧异步结果/关闭/重载/转换不串状态；完整常见任务与主题/DPI最终GUI验收 |

验收按证据分层，不以一种替代另一种：

- **静态与定向测试**：核对实际diff/asmdef/meta与API调用边界；复用并按新交互调整 `ResourceWorkbenchTests`、`ResourceWorkbenchInteractionTests`，对结构化预检/阶段通知/追溯与E05适配补关键断言；直接受影响的Recipe/Atlas/Publisher/C02测试按实际改动选叶。不削弱原断言或依赖私有类名“创建成功”冒充操作通过。
- **实际Unity编译**：当前主Unity完成导入/编译，记录真实结果；IDE/dotnet/反射创建Editor对象不替代。无新增依赖和Player代码时不为UI重跑Player/E04/F03，未变化证据按原资格沿用。
- **真实GUI**：在同一主Unity、自有可识别的非空项目中，演示文件/目录/地址/alias/子资产、多Group与合理较大缓存行集；做鼠标、键盘、多选、过滤隐藏选择、拖放、上下文菜单与Undo/Redo。列出实际规模，不用只有一个空项目的截图验收。
- **视觉矩阵**：深浅主题；普通停靠/宽屏/最小目标；现环境可用的100%、150%与适当高DPI条件（不可用项明确记录，不伪造缩放图）。无重叠、控件裁切或不可达操作，长路径/digest完整可查看复制；截图记录主题、GUI point窗口尺寸与pixelsPerPoint。
- **操作/状态矩阵**：未选/空Group/未计算/过期/dirty/执行中/部分成功/失败/待恢复/未启动；排队后切页/外部Project选择、旧E05完成、窗口关闭、脚本重载、Play/编译/导入转换；确认原操作只执行一次、输入有身份、结果不贴错项目、没有自动写入或丢失Dispose/finally责任。
- **正式流程**：通过真实UI进入同一Operations/Publisher/C02/Signer；实际写入仅限经授权的自有夹具范围。证明生成失败不抹去发布成功、旧Build发布仍复验、C02部署不激活、签发不换所选版本、查询不清理。正常应用会话只观察，不为了截图运行原游戏。
- **最终交付**：真实Unity截图与交互结果、必要定向测试/编译结果、未验证限制及使用配置说明，增量维护此Plan和届时真正变化的Resource.md。UX1方向确认不能替代UX5完整验收，反之不等全部完成才第一次让用户看布局。

### 29.13 定向设计 Review 与裁定

- 已完成一次独立只读定向设计Review，并在同次Review内核对诊断段的研究增量；范围仅本节与关键源码的功能覆盖、复杂度、副作用、技术成本及可验收性。不重开31项审计，不运行测试，不再追加全面Review。

| Review实质问题 | 设计修订与核对结果（不是产品修复或GUI验收） |
|---|---|
| C02确认只写payload，漏workspace/控制/导入副作用 | §29.7按Deployer源码补完整写入集、.meta/Refresh与stage逻辑成本；不把PayloadBytes当峰值/磁盘空间认证 |
| 子资产选择API/字段不存在；现源码尺寸/继承/状态存储描述有误 | 回读Definition、ResourceSelection、Operations及公共工具；§29.2/29.4/29.10改为真实620×440、145/160、125～280与宽度夹值；只有AddSelections和只读派生子资产；不声称窗口已继承SplitWindow或TreeViewStateGroup自带EditorPrefs |
| 排队切换策略矛盾，token能力误写成同步GUI可点击取消 | §29.6/29.10/UX3～4统一排队切换取消、开始后锁定且不可交互取消；分帧E05取消独立，检查点token仅脚本/自动化覆盖 |
| 遗漏最多32次构建尝试，report/result与阶段耗时层级混淆 | §29.6保留有界32次选择；时间/Invocation属report、Map/指纹属Build；组合计时不冒称单阶段或相加为总耗时 |
| 诊断七区/Content字段遗漏，Mode无来源，历史切换丢在途完成事件未说明 | §29.8保留Projects/Updates/RecoveryNotes，补同次report.Content与既有字段；移除Mode，窗口标签不冒充正式SessionId；明确历史generation切换语义 |
| E05 App未由owner验证、可选预填无明确边界 | 回读ArtifactStore.Owner/ValidateOwner与CaptureArtifactState；§29.8区分ProjectId/Platform owner闭包和App输入来源，Ready不代表App兼容；§29.10列出可选纯值Editor预填接点，不保留外部配置/资源引用 |

- 主代理按上述finding逐条回读原源码、修订并定向核对文档一致性；没有把“改了Plan”宣称为产品问题已修复，也没有声称修改后又完成了一轮独立审查。真实视觉/交互/编译/定向测试全部留到获授权的Code批次。
- 提交审核时的明确推荐：IMGUI原生多列表格/树重组；五任务导航、图集归资源子页；无配方主操作为“构建并本地发布”，有配方严格服从Action；C02/历史签发/E05纳入薄UI接点。它们等待用户与ChatGPT整体审核，不视为Code授权。
- 尚需审核裁定的实质取舍只有：是否采用上述IMGUI路线（备选UITK迁移成本见§29.10），以及是否同意UX4把C02与历史签发一起接入。推荐均按本节纳入；不要求用户重选多套风格。若只批核心UI，可保留这些API/Inspector原入口而不宣称已接入工作台，须同步缩小UX4验收范围。
- 本轮停止点：**Workbench UI/UX设计完成，待用户与ChatGPT审核。** 不自动切Code、不实施UI、不进入8.10、阶段9或其他XF扩展融合，不执行第二次提交。

### 29.14 UX1 审核裁定、授权与实施记录（2026-10-17）

- 用户已审核并采用§29推荐方案：顶部Project上下文、资源/分包分析/构建/版本交付/诊断五入口，资源Group/原生多列表/按需详情及可折叠输出，IMGUI路线；图集归资源子页。上文设计阶段“不改产品/待审核”为历史边界，不阻止本次明确授权。
- 本轮只实施UX1。C02/选定历史签发/E05已同意纳入整体专题，但分别留UX4/UX5；图集完整页/追溯留UX2，结构化预检/阶段通知留UX3，不提前建设。原未迁移功能继续可达，局部拆分现View而非复制整套旧页或伪按钮。
- 初始实核：`fusion/8-resource`，HEAD仍为`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`；并行Plan、配置、AGENTS、插件/Packages/ProjectSettings等已有改动全部保护，不补交、不回退、不接管。本轮不暂存/提交/推送或执行其他Git写操作。
- 当前唯一主Unity进程15324，工程路径/2022.3.62f3c1/StandaloneWindows64已实时核对；无Play/编译/导入/Test Runner在途任务，ResourceSystem未初始化；Main.unity已保存且不dirty。初始无Workbench窗口，深色主题，pixelsPerPoint=1.75。这是本批现场，不沿用旧PID/文件数。
- 实施判断：沿用原Operations、SerializedObject、一次性update queue及构建结果；不改程序集、持久资源身份或执行协议。排序/筛选采用会话内稳定行ID到真实GUID/Group定义映射，操作时重新定位模型，不能把显示行号写回定义。全部Group只读；具体Group的原子编辑/Undo保持。
- 测试先覆盖新增交互映射与门禁，使用原owned夹具；图形与鼠标/键盘验证单独记录，不以反射创建对象/直接Operations替代。只做直接影响回归，不重跑B02/F03/E04/Player矩阵；不为了形式RED破坏原功能或将新接点缺失描述成产品缺陷。
- 保留原minSize=620×440，目标布局按1440×900、1080×720、720×480 GUI point验证，并检查620×440可达性。真实截图记录主题、窗口GUI尺寸与pixelsPerPoint；可用主题/DPI之外的矩阵明确未验证，不修改系统缩放/权限。
- 本批结果：**UX1技术完成，待用户视觉与交互验收。** 以下记录实际范围与未验证限制；停止等待主要布局确认，不自动进入后续批次，不执行第二次Git提交。

### 29.15 UX1 实现、Review与验证结果

| 交付 | 已实现范围 |
|---|---|
| 项目上下文与五任务壳 | 搜索式Project选择器（名称/ProjectId/路径/GUID提示）、新建原入口、dirty/明确保存、项目设置与文档；保留菜单及Project资产入口；minSize仍620×440 |
| 主资源区 | 原生TreeView/MultiColumnHeader、可调列宽/排序、Group折叠/分隔、单选详情/完整路径复制、Ctrl/Shift多选与隐藏选择计数；全部Group只读，原单Group批量协议不变 |
| 真实编辑 | 文件/目录添加及拖放、地址/alias/Extensions、Group新增/改名/Packing/删除、移动/移除、上下文定位/复制、Undo/Redo与快捷键；子资产只读派生，不加选择协议 |
| 设置与保存 | 同一SerializedObject/Operations，设置表单限定宽度、窄窗可返回；Ctrl+S先显式提交Group名称再仅SaveProject，不全局保存或改场景；根错误可清理/修正 |
| 反馈与旧页承接 | 原update queue、Project/页面/时间消息、短提示+完整输出、最多32次尝试与历史选择门禁；分包分析/构建/交付/诊断原有效入口仍可达，BuildView局部分出交付绘制而未复制整份View |

- 新增源码仅`ResourceEntryTable.cs`、`ResourceProjectPicker.cs`、`ResourceWorkbenchGUI.cs`，均位于原`Editor/Systems/Resource/Service/`且由Unity生成新.meta。修改原Window/WorkbenchView/DefinitionView/BuildView及3个直接相关测试文件；未改Runtime、Operations协议、asmdef、Catalog、Packages或ProjectSettings。Resource.md仅增量同步本批实际入口。
- 一次独立实现Review已完成，未发现High。修复了Group输入Ctrl+S时序、同GUID重选丢状态、最新尝试误标历史、零Group引导、图集页Ctrl+F、旧设置错误滞留、完整多行结果压垮折叠区等问题。Group改名采用可显式提交的文本编辑，保存前提交原RenameGroup；不保留第二份Project配置。
- Review中3项核心缺陷分别实测RED→GREEN：`CtrlSSavesTheGroupRenameStillBeingEdited`、`OperationMessageBelongsToItsOriginalProjectAndPage`、`SelectingTheLatestAttemptDoesNotClaimTheDefinitionChanged`。其余直接修正并并入定向回归；未安排第二次全面Review。真实GUI另发现长路径最小宽度挤出复制按钮、TreeView先消费拖放，分别修正约束/事件顺序；后者新增真实窗口回归并实测RED→GREEN。
- 最终Unity EditMode定向运行：`ResourceWindowTests`、`ResourceWorkbenchTests`、`ResourceWorkbenchInteractionTests`，**15/15 Passed，0 Failed，约50.69s**。包括真实窗口鼠标事件入口、原子Undo、排序/过滤映射、隐藏多选、全部Group拒写、禁用快捷键/拖放、外部编辑刷新、消息归属、单次update与关闭取消；不是重跑B02/F03/E04或Player矩阵。
- 当前主Unity实际编译已完成，`isCompiling=false`、`isUpdating=false`、`scriptCompilationFailed=false`；Test Runner无在途任务。IDE/dotnet不是本结论依据。过程中临时脚本探测有参数/动态类型/坐标错误，未生成产品源码，已改正；Console旧工具诊断未清除，不能把其历史计数当当前产品编译失败或冒称Console全空。
- 真实GUI使用同一Unity及owned夹具，不运行原游戏。桌面鼠标/键盘已实测：Ctrl多选后过滤为2选/1可见、停靠状态Shift范围选中001～003、地址及alias编辑、Ctrl+S保存、Ctrl+Z/Ctrl+Y回退重做、窄窗列表/详情切换、搜索式选择器与同名Project区分。实测地址`ui/cards/gui_verified`、alias`menu/gui_verified`，保存后Project不dirty且Main场景仍不dirty。
- 文件和目录拖放经当前真实Window的`DragUpdated/DragPerform`进入完整OnGUI/TreeView，而非直接调Operations：views从36增为38并回读登记的真实GUID；与桌面持续拖动不同，记录为实际GUI事件注入验证，不夸称完成所有原生桌面跨窗口拖动矩阵。自动化同时验证禁用/全部Group拒绝，未写Importer。

### 29.16 UX1 体验夹具、截图与停止边界

- 保留owned非空夹具：`Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/`，归属/目的在同目录`ux1-fixture-owner.json`登记。主配置`WorkbenchProject.asset`，另有同显示名`SameDisplayNameProject.asset`用于区分ProjectId；均不是原Core Fixture。Content实际**58个JSON，3个Group，47条选择定义（38/1/8）**，含目录、长名/路径及地址/alias；两个拖放来源也保留用于体验，不自动加入默认导出（原框架导出根本就排除Tests）。
- 体验步骤：选主Project资产→`YLFramework/Resource/Workbench`；views表可滚动、列头排序、Ctrl/Shift多选后搜索，观察底栏隐藏选择计数；单选编辑地址/alias或选择目录编辑Extensions；窄窗用Group下拉和“查看详情/返回列表”；顶部项目设置、Ctrl+S与任务输出均为真入口。不需构建/运行游戏/初始化Runtime。
- meta检查覆盖新增3脚本及夹具资产/目录共71路径：配套.meta/GUID齐全，无本批重复GUID，AssetDatabase GUID→路径往返一致；新脚本GUID分别`093e90ee4a6b46a4d9cd10a031d0e6d7`、`a53d00cbcf55ce24e89a2c8f933db971`、`ba25fd7560f43584da002b27c5d790db`。未移动存续脚本或修改旧GUID。
- 截图均来自真实桌面主Unity，不是HTML/静态原型；仅临时置顶截取并立即恢复层级。**全部pixelsPerPoint=1.75**，GUI point与PNG物理像素不同，没有改系统DPI或后期缩放图冒充矩阵。部分高度因物理像素取整为`.14`point。证据位于`C:/Users/Kano/AppData/Local/Temp/kilo/`：

| 文件 | 真实条件 / 可审阅内容 |
|---|---|
| `ux1-wide-final.png` | 深色1440×900（实际高约900.14），非空资源表、Group和详情 |
| `ux1-1080-dark.png` | 深色1080×720（实际高约720.14），普通尺寸列表与详情 |
| `ux1-docked-dark-main.png` | 实际停靠在主Unity SceneView同DockArea；Workbench约1091.71×800.71，主Unity整体截图，包含外围Editor面板 |
| `ux1-settings-dark.png` | 深色1080×720，Project身份/根/策略与完整路径复制 |
| `ux1-720-list-dark.png` / `ux1-720-detail-dark.png` | 深色720×480，真实点击切换列表/详情，所选资源保持 |
| `ux1-620-minimum-dark.png` | 原620×440，Group下拉、保存、批量操作和滚动区可达，未提高最小尺寸 |
| `ux1-1080-light.png` | 当前Unity真实浅色主题1080×720，之后已恢复原深色 |
| `ux1-multiselect-hidden-final.png` | 深色1440×900，真实Ctrl多选后筛选，2选/1可见；该图为修正最终拖放前的同一实现阶段，不把当时36行说成最终38行 |
| `ux1-output-dark.png` | 深色1080×720，真实Ctrl+S后的Project/页面/时间与完整可复制输出 |
| `ux1-project-picker-docked.png` | 实际停靠窗口搜索UX1，显示同名项目不同ProjectId/配置路径 |

- 已知限制/布局裁定：只实测现1.75DPI，100%/150%及其他显示器未验证；未重跑真实构建长任务或旧技术矩阵，阶段观察/结构化预检等仍留UX3。输出区当前折叠并按窗口比例定高，不增加通用可拖任务面板；Domain Reload恢复Project/列/布局但不持久化临时报告、Group子模式或行选择；同Group改名会清掉UI行选择但不误编辑其他资源。完整图集页、来源追溯、历史签发/C02/E05均仍留后续批次。以上不改变本批620×440与五入口布局，主要视觉/交互仍待用户确认。
- 保护：原游戏/MainModule、正式Scene/Prefab/Canvas、Runtime、StreamingAssets、旧Bundle及稳定程序集无本批改动；测试沿owned夹具逐次AssertProtected。已有配置/AGENTS/插件/Package/并行Plan改动保留、不接管。本批不建立后台服务，不清用户Console，不保存用户场景；最后恢复主题、原Selection/焦点并关闭本批新Workbench/临时停靠tab，保留数据供菜单重新打开体验。
- 最终实时复核：主Unity仍15324；正常AssetImportWorker子进程不是第二主Editor，未自行启动第二实例。主Editor非Play/编译/导入、`scriptCompilationFailed=false`，Workbench窗口0、关闭前pending0，原ProjectBrowser焦点/原空Selection与深色主题已恢复；夹具已保存、Main场景不dirty、ResourceSystem.Current为空、已加载AssetBundle数0。`git diff --check`通过、暂存区为空、HEAD仍`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，未执行Git写操作。
- 终态：**UX1技术完成，待用户视觉与交互验收。** 不把UX1当整体UI或8.9 COMPLETE；不自动进入UX2～UX5/8.10/阶段9，不暂存、不提交、不推送。

### 29.17 UX1 体验反馈：资源根单字段层级显示

- 用户指出原ObjectField只显示`Content`等文件夹名，无法辨别层级；新增独立路径行与定位按钮又重复占空间。按反馈合并为同一行“专用资源根”：原生ObjectField继续处理目录选择/拖放/点击定位，仅在Repaint替换名称显示为从`Assets/`开始的真实路径，右侧同排复制完整原值；不重命名文件夹、不改AssetRootGuid或资源范围合同。
- 样式为Workbench局部按主题缓存，不修改EditorStyles、不在每Repaint新建GUIStyle。未设置与失效GUID保留明确提示。Resource.md同步实际入口，不推进多资源根、UX2或其他能力。
- 新增真实窗口测试`SettingsRootFieldCopiesTheCompleteHierarchyWithoutEditingTheProject`，在720×480设置页点击实际复制按钮，断言复制完整层级且Project JSON不变。相关`ResourceWindowTests`最终**3/3 Passed**，原窗口刷新/定位/关闭取消及真实TreeView拖放回归保持；Unity实际编译通过。
- 当前用户已打开的Workbench保留，不新建第二工作台、不关闭用户窗口；真实深色1080×720、pixelsPerPoint=1.75截图：`C:/Users/Kano/AppData/Local/Temp/kilo/ux1-root-unified-feedback.png`。此图取代临时的双行资源根试稿，不把旧`ux1-settings-dark.png`当作本字段最新状态。未保存/修改Project、Scene或资源资产，未执行Git写操作，UX1仍待用户体验验收。

### 29.18 UX1 体验反馈：空白点击释放输入焦点

- 用户要求各页面点击空白可取消截图中的输入框蓝色选中态。统一在Workbench壳绘制结束处理未被控件消费且无hotControl的左键MouseDown，释放文本焦点/keyboardControl和editingTextField；先由原字段/按钮/表格处理输入，不抢事件，不清空资源表单选/多选，也不改变资源纳入定义。
- 先实测复现KeyId焦点在空白点击后仍保留（RED），修正后同测GREEN。补充验证Group名称失焦后正常提交、Project仍dirty且未写盘、Undo可回退；搜索失焦不丢原表格选择、Project JSON不变。Resource.md同步用户交互说明。
- 本次三类直接相关UI测试同次运行18项；MCP请求超时后未重新启动，读取该次`testResults`确认**18/18 Passed**，包括新增两个BlankClick测试和前次RootField复制测试。Unity实际编译通过、Test Runner无在途运行；不重跑资源内核/构建/Player矩阵。
- 当前用户已打开的主Unity窗口中，桌面实际点击KeyId后再点右侧空白，蓝框/光标消失；截图`C:/Users/Kano/AppData/Local/Temp/kilo/ux1-blank-click-unfocused.png`，深色1080×720、pixelsPerPoint=1.75。未改系统缩放、Project策略/资源根/Runtime协议，不保存场景，不执行Git写操作；继续保持UX1待用户视觉与交互验收，不推进后续批次。

### 29.19 UX1 体验反馈：顶栏信息层级与操作分组

- 按用户确认的整理方向，删除重复`RESOURCE`标识；Project选择器只显示名称并限宽180～320 GUI point，完整名称/ProjectId/路径保留tooltip。宽度≥960时旁边用次要文字显示ProjectId；窄窗从选择器及更多菜单查看/复制，不扩大选择器或挤出操作。
- dirty状态跟随项目上下文；右侧固定同排“保存 / 项目设置（或返回） / 更多”。删除原窄窗重复的第二行项目操作。新建迁入Project选择器底部，仍调用原CreateProject/路径对话框，忙/转换/执行门禁保持；帮助、配置定位、复制ProjectId/路径归更多菜单，不新增执行体系。
- 新增实际GUILayout测试`HeaderBoundsLongProjectNamesAndKeepsActionsOnOneRightAlignedRow`：100字长项目名在旧620窗口占536 point，实测RED；修正后620/720/1080/1440均GREEN，断言选择器限宽、操作同排靠右且不越界，Project JSON与dirty不变。
- 定向回归另复现UX1 Group改名缓冲对外部Undo的误判：旧已提交文本与撤销后的模型名不同，被当作新输入再次提交。加入仅在视图内使用的原输入基线，外部Undo/Inspector更新显示基线，真实未提交输入发生外部冲突时拒绝覆写。原Undo断言保持，并补5次重绘后仍不回写的断言；不改序列化协议或Operations。
- 本批分别运行`ResourceWindowTests` 4/4、`ResourceWorkbenchTests` 8/8、`ResourceWorkbenchInteractionTests` 7/7 Passed；不是声称同次全量19项运行，也未重跑资源构建/Player技术矩阵。Unity实际编译通过。源码仍限Resource Editor与直接UI测试，Resource.md同步入口；无Git写操作。
- 反馈修正：ProjectId原先跟在固定320宽选择器后，在宽窗中悬在中间；现选择器按名称`CalcSize+34`自适应（140～320 GUI point），ProjectId紧跟其后。点击帧`GUILayoutUtility.GetLastRect()`不是有效布局矩形，导致“更多”菜单弹到窗口左上；现仅在Repaint缓存项目选择器与更多按钮的真实矩形，点击时用缓存锚点打开Popup/GenericMenu。帮助项不使用`/`，避免被GenericMenu解析成无谓二级菜单。
- 回归`HeaderBoundsLongProjectNamesAndKeepsActionsOnOneRightAlignedRow`新增锚点断言：PopupAnchor必须等于真实选择器矩形、MoreMenuAnchor必须等于右上更多按钮矩形，并在620/720/1080/1440下断言菜单锚点位于右半区且同行。真实截图`ux1-header-identity-adjacent.png`与`ux1-header-menu-feedback-cropped.png`（从同一次真实桌面抓取裁出工作台与完整弹出菜单，排除无关窗口）确认ProjectId紧邻、菜单在右上按钮下方。深色、pixelsPerPoint=1.75；未调整用户窗口大小以外的数据，未保存/修改Project或场景。
- 复跑窗口测试时发现测试自身两个问题并修正：布局缓存遍历会命中用户同时打开的工作台按钮，现只接受本测试窗口边界内的控件；`ShowUtility`会恢复该窗口类型上次位置，窄窗尺寸必须在显示后设置并断言760宽。修正后`ResourceWindowTests` 4/4、`ResourceWorkbenchTests` 8/8、`ResourceWorkbenchInteractionTests` 7/7全部Passed；均为本批定向UI回归，不声称同次19项单次运行，也未重跑资源内核/构建/Player技术矩阵。Unity编译通过，无在途Test Runner。
- 当前用户选择的Core Resource Fixture只作现有界面读取，没有为演示改其配置/资源；保留其已有工作区改动。真实深色截图：`C:/Users/Kano/AppData/Local/Temp/kilo/ux1-header-wide-feedback.png`（约1313.71×872.71）及`ux1-header-620-feedback.png`（620×440），pixelsPerPoint均1.75。未改系统缩放；用户在验证期间的新窗口调整不覆盖。仍为UX1体验修正，不进入UX2～UX5。

### 29.20 UX1 体验反馈：文字基线与弹层向内对齐

- 用户再次指出ProjectId位置不正。此前仅缩短横向距离并没有解决垂直偏差；本次GUILayout断言实测ProjectId行y=2、名称按钮y=0（RED），定位为Label与工具栏按钮的margin/padding/contentOffset不同。现共享垂直几何与字体指标，保留ID的普通字重/次要颜色，名称和ID的内容中心一致（GREEN），不再凭截图感觉判断。
- 此前GenericMenu已锚到右上按钮，但原生系统菜单从锚点左侧向右展开，会超出工作台右边界，不符合本次体验反馈。现使用同一Editor层的局部`ResourceProjectActionsPopup`，已知宽220 point，右边缘与按钮相同并留4 point垂直间隙；不估算系统菜单文字宽度。只承接原配置定位/复制/文档操作，保留hover、方向键/Enter/Esc；没有新任务、配置或Runtime权威。
- 新增Editor脚本.meta由Unity生成，GUID为`626245efc0417a649ad2953876c6657b`，不移动旧脚本、不改asmdef/依赖。旧`moreMenuAnchor`仍表示真实按钮，另用`morePopupAnchor`表示整个弹层占位，避免把按钮位置正确当成菜单不会越界。
- `HeaderBoundsLongProjectNamesAndKeepsActionsOnOneRightAlignedRow`覆盖620/720/1080/1440、ID实际行和内容几何，以及弹层左右边界。新增`MorePopupFitsTheNarrowWorkbenchAndCopiesWithoutEditingProject`从实际窗口点击更多，验证真实PopupWindow在620窗口内，再点击菜单复制原ProjectId、确认Project JSON不变。测试明确聚焦自己的Utility窗口，不依赖用户当前活动窗口。
- 本批最终`ResourceWindowTests` **5/5 Passed**，Unity编译通过；此前8/8、7/7非本次重跑，不将它们虚报成同次运行。额外单测用于GUI取证仍Passed，不跑B02/F03/E04/Player矩阵。未修改资源根/Project协议，不保存配置或场景，无Git写操作。
- 取证：`C:/Users/Kano/AppData/Local/Temp/kilo/ux1-header-baseline-inward-popup.png`可查看当前Core界面的文字基线，但该图未保留打开的弹层，不作为弹层证据。完整右对齐弹层见`ux1-more-popup-owned-gui-logical.png`：在明确owned测试窗口实际点击更多后，用Unity屏幕API逻辑坐标直接抓取620×440 GUI；弹层位置`(494.29,110.29,220,133.14)`、owner`(100,80,620,440)`，右侧仍在owner内。主机pixelsPerPoint=1.75，但该API图片是逻辑分辨率，**不作为新的物理DPI验证或静态原型**。临时截图观察回调均解除，未加入产品/测试持久设施。
- 工作台新窗口不另建，原Core资产和用户已有修改不接管；取证期间只临时恢复既有主Unity，结束恢复其原最小化状态。UX1继续待用户视觉与交互验收，不进入后续批次。

### 29.21 UX2～UX5 连续实施联合授权（2026-10-17）

- 最新用户授权："第一版视觉设计先到此，继续UX2～UX5夜间自动执行；UX5之后再集中优化UI设计。" 本次Code范围为UX2分包/图集 → UX3配方/构建结果 → UX4版本交付 → UX5诊断/功能整合，包含必要正式小接点、定向测试、当前唯一Unity真实编译/GUI、缺陷修复、一次独立实现Review及原Plan/Resource.md维护。每批必要门禁通过即进入下一批，不等待用户上线、视觉确认或Git提交；UX5完成后停止，统一交付待用户验收。
- §29.1/29.10/29.12/29.14～29.20的“仅设计”“待授权”“UX1后等待”等保留历史含义；本节为当前有效授权。允许有限只读BuildMap/BuildResult来源副本、原唯一预检流的结构化结果、真实阶段边界的只读观察、选定确切版本签发、C02薄UI、E05手工完整闭包表单。不得改Catalog v1、持久协议、所有权、密码后端、Player远端门禁或公开执行语义。
- 真实起点为`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`加本地未提交UX1成果及§29.17～29.20反馈修正，不把HEAD当完整源码身份。现场`fusion/8-resource`、暂存区为空；已有.gitignore/工具配置/AGENTS/NuGet与McpPlugin/Packages/ProjectSettings/并行Runtime UI Plan/Core Fixture改动不接管、不回退，不读其他worktree。**本轮不暂存、Commit、Push、Merge、Tag、切分支、Reset、Clean、Stash、Rebase或改既有提交。**
- 当前唯一主Unity PID `15324`，工程路径与Unity `2022.3.62f3c1`/`StandaloneWindows64`实时一致；另两Unity进程是AssetImportWorker，不是第二主Editor。初检非Play/编译/导入、scriptCompilationFailed=false；Main场景不dirty，ResourceSystem.Current为空，已加载Bundle=0，当前没有Workbench窗口/窗口排队操作。本会话没有后台进程。旧Test Runner记录不冒充本轮结果，首次运行前另核实时态。
- 初始主题深色、pixelsPerPoint=1.75；焦点Console，Selection GUID `52d3fa0622a3f3b428a754d82a63736c`。新页面检查1440/1080/720/620宽及真实停靠/深浅主题；不改系统缩放，不外推其他DPI。UX1旧证据及夹具`Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/ux1-fixture-owner.json`保留，默认只读复用，不覆盖其体验内容。
- 新owned夹具预留根`Assets/YLFramework/Tests/Fixtures/WorkbenchUX2To5-20261017/`（创建前复核不存在）；证据预留`C:/Users/Kano/AppData/Local/Temp/kilo/workbench-ux2to5-20261017/`（新建且不覆盖旧证据）。仅本轮登记的Project/版本可进行图集删除、导出、非生产签发、C02写入/恢复和Store只读验证；C02限原Deployer owned workspace及StreamingAssets下本轮独立身份，不动MainModule/用户部署/既有父meta。凭据不入SO、源码、截图或文档。
- 执行选择：延续IMGUI/TreeView和原update queue，按真实职责拆原BuildView，不另建Workbench/任务系统；E05默认完整手工纯值表单，无收益不足的Runtime getter/反射预填。保存按当前明确Project/Recipe/Atlas对象，执行重新冻结/复验；观察异常/重入不能改变服务finally/提交责任。临时报告会话有界32，不扫描raw/Temp恢复。
- 门禁按用户本次清单及§29.4～29.10执行；保留None/provider/图集/提交未确认/部分成功/身份失效/查询Dispose等直接闭包。复用未变化E04/C05/B02/F03/Atlas/Publisher/C02历史证据，不重跑31项审计、176组Player或整套成本矩阵。产品GUI入口、真实OnGUI事件注入、桌面鼠键和服务测试分层记录。

### 29.22 UX2～UX5 滚动检查点

- 当前阶段：**UX2/UX3必要门禁已通过，直接进入UX4实施**；UX5未开始，横切1440宽/真实停靠与全部五页统一回归仍在UX5收尾，不提升整体或视觉验收等级。
- 已实现：Map解析时共享不可变`ResourceBuildSource`有限来源副本；定位器按Project GUID/ProjectId、GroupId/Packing、Selection GUID/内容/原路径或Atlas配置GUID/RuleId/内容重新核对，不按旧index操作。分包改原生`ResourceBundleTable`物理行、缓存排序/筛选、双来源/可见源文件/双向依赖树与分层字节。旧Map过期保留；图集pending由正式Plan.NeedsGeneration明确状态，不解析错误文案。新`ResourceAtlasView`编辑同一配置、显式Save/Preview/Generate、全Project Prepare/显式读取、Obsolete确认删除；Inspector仅共享纯字段绘制。Ctrl+S在资源图集子页仅存规则，顶部保存仍明确存Project。
- 新文件：Service/ResourceAnalysisView.cs、ResourceAtlasView.cs、ResourceBuildSourceLocator.cs、ResourceBundleTable.cs；Tests/EditMode/ResourceWorkbenchUX2Tests.cs与Resource/ResourceWorkbenchUXEvidence.cs；配套meta由Unity导入生成。已有修改：ResourceBuildMap、ResourceAtlasConfigurationEditor、ResourceDefinitionView、ResourceBuildView、ResourceWorkbenchView，仅本轮直接范围。后续独立Review尚未执行。
- TDD与已取得门禁：来源/表身份2项RED→GREEN；失效Map保留、真实GUI规则保存/B06生成2项RED→GREEN；原Map加新项合跑15/15。真实截图额外发现BeginArea在Layout取得dummy Rect导致右侧空白，加入规则控件可视范围断言看RED，改用UX1横向布局+独立滚动详情后4/4 GREEN；同根因分包详情同步修正。测试助手一次误用不存在FinishedAtUtc导致编译失败，已改实际观察完成时间+原Elapsed（非产品错误）。
- 组合回归终态已实读：同次MCP `_resultCollector`的RunFinished summary=Passed、35/35、Failed=0/Skipped=0，Runner.IsRunActive=false，完整真实结果保存`ux2-shell-regression-same-run.json`，没有重跑；不是仅凭35个leaf推断suite成功。
- 构建同次run已完成：`ux2-build-run.json`=Passed、BuildError=null，实际None/StandaloneWindows64、7物理Bundle/14主资产/4Group，elapsed约37856ms、native pipeline约4027ms；Bundle raw=57945B、delivery全文件=67887B（含旁车，不能混为Bundle字节）。主Unity无Progress/已加载Bundle=0。未重新启动构建；通过静态原report接入测试窗口后，以真实导航/来源切换/原生表格点击/依赖页控件展示，**此构建本身为获授权的UX2直接正式API夹具调用，不冒称UX3 GUI构建闭环**。
- owned夹具已创建：`Assets/YLFramework/Tests/Fixtures/WorkbenchUX2To5-20261017/`，owner为`ux2to5-fixture-owner.json`；ProjectId=`yl-workbench-ux2to5-20261017`，App=`workbench-ux-app-v1`，Recipe BuildAndPublish（未执行），None交付。含8个消息、2个共享依赖Prefab、独立Scene、手工Atlas、2条自动规则；全由本轮创建，不覆盖UX1。生成接口/控件已得2个Current自动Atlas各2个Sprite，手工Atlas由真实显式页“准备本Project图集”控件准备到spriteCount=2。
- 证据根：`C:/Users/Kano/AppData/Local/Temp/kilo/workbench-ux2to5-20261017/`。保护before快照、真实生成与同次构建/回归JSON保留；新增`ux2-analysis-{exact,sources,dependencies,dependents}-dark-1080.png`、`ux2-analysis-ui.json`（真实被依赖树3节点）、`ux2-analysis-min-dark-620.png`、`ux2-estimate-min-detail-light-620.png`、`ux2-atlas-current-light-720.png`、`ux2-atlas-min-rules-light-620.png`。均当前主Unity非空真实GUI，1.75DPI物理抓屏，不外推其他DPI。早期两张Atlas空白/未读取图，以及`ux2-analysis-min-detail-light-620.png`（换肤后旧临时report已正常丢弃、当时预估未算）只保留为失败/空态证据，不列通过截图。换肤确实重新初始化窗口，未从raw或JSON猜测重建产品报告；后续实际点击重新计算预估。
- UX2最后新增规则来源定向验证：真实Generate后Map保留配置GUID/RuleId，当前来源可定位；修改规则得到Update并拒绝旧来源，移除规则得到Obsolete且输出仍存在，旧Map不变；同窗Save按钮/Ctrl+S仅存规则、不存dirtyProject。`ResourceWorkbenchUX2Tests`最终5/5 Passed（20.51s，非原35项同次运行）。当前Unity编译通过；新测试夹具生成沿用当前工程的TextureImporter.spritesheet写法，存在原同类API过时warning，不作编译失败或实际运行错误。
- 当前GUI环境：主Unity原本最小化，已临时Restore（主native HWND197584），本轮体验窗口`ResourceUX2GuiWindow`实例`-193210`/native HWND4522996，1080×720 logical、1.75DPI、深色；运行测试可能重载/改变临时选择，因此下一步必须实核，不复用HWND作为身份。结束须关自有窗口、恢复原Selection/Console及主最小化状态。Main最近实核不dirty，无业务会话/Bundle；不存在已授权外的写入。
- UX3当前实现：新增`ResourcePreflightState/ResourcePreflightResult`（在Operations原文件），`CheckReadiness`只共享静态前缀、不key/Map/SHA/图集Preview/历史；`InspectPreflight/InspectRecipePreflight`复用原唯一控制流，原string Preflight保留相同摘要、原异常类型/语义与Atlas前置早返回。`ResolveSavedRecipe`抽原相同保存门禁供执行/预检共用。新增Service/`ResourceBuildProgress.cs`的只读实际阶段event，Builder原key/输入冻结/后验/finally不替换；观察异常仅通用Warning隔离、不输出异常秘密，observer内执行明确拒绝，未复用测试故障钩子。Report补原Project/平台/App/操作元数据；尝试Reset不再清全窗口32条列表，新增按原Guid投影，UI接入尚未完成。
- UX3首批4项TDD RED→GREEN已确认同次suite完整收尾：Passed4/4、92.37s，Runner inactive，含一次真实native观察器异常/重入隔离与finally验证；持久结果`ux3-service-first-run.json`（实际枚举文字）。没有重跑。新GUI Recipe引用/保存测试已先RED后GREEN1/1（4.48s），证明真实Save按钮/Ctrl+S只存Recipe不存dirtyProject、Action不临时改变、OnDisable/OnEnable仍同一资产引用。
- UX3 GUI代码已接：Window序列化持有`selectedRecipe`及GUID（丢失不静默回退无配方），同一纯字段绘制供Inspector/Workbench；原无配方App/force/生成/目录窗口选项保留。构建配置/结果宽窗双区、窄窗切换/滚动，正式预检明确三态，Recipe不匹配双方身份/显式切换或解除。主动作原ExecuteRecipe、不改Action；结果分BuildError/PublicationError/GenerationError、部分成功与未确认恢复说明；Project过滤后的32次有界尝试保留跨Project切换，结果投影/制品查找缓存，不在重绘做SHA。Workbench仅在自身同步操作作用域订阅阶段event，finally/关闭解绑，不编造实时百分比/可中止。
- UX3额外自检发现新预估会误清确切报告失效标记：新增`RecalculatingAnEstimateCannotRevalidateAnEarlierExactBuildReport`看RED，再将预估stale与确切report的DefinitionChanged分开；完整构建成功才清自身报告失效，预估重算不复验raw。当前等下述run验证GREEN。旧失败尝试和选择历史不应本身污染定义状态的UX1回归一并覆盖。
- UX3界面/失效性组合回归同次收尾：Passed11/11、78.61s，`ux3-ui-stale-combined-run.json`，没有重复native观察器。早期Recipe拒绝的已知身份新增RED→修复→GREEN1/1（4.00s），即使ResolveSavedRecipe拒绝dirtyRecipe，也保留已知Project GUID/Id、平台/App/force/生成请求而不伪造成功Invocation。
- 无配方GUI同次已完成：`ux3-no-recipe-build-only-result.json`含原报告/阶段，BuildError/PublicationError/GenerationError全null，Operation=BuildOnly、未发布/生成，elapsed36729.84ms、原Preflight10909.29ms、7Bundle/raw Bundle57945B/delivery全文件67887B；原真实序列Preflight→AtlasPreparation→Map→Fingerprint→NativeBuild→Readback→WorkspaceVerified→AttemptFinished，未伪造Publication/Codegen阶段。已拍`ux3-no-recipe-result-dark-1080.png`，测试证据event已解绑。此为实际桌面按钮→原正式只读预检→保留确认框→原BuildOnly完整GUI闭环。
- 原生确认框的body为owner-drawn，Win32 GetWindowText只返回占位`Static`，因此没有伪称程序读到了正文或放松`Confirm`身份校验；实际由PNG直接核对完整身份后点击正式正按钮。未移除/跳过产品确认。源码变化导致Workspace settings token与UX2旧run不同，确认中实际新路径仍为此Project的原Builder owned范围，不当作旧report恢复。
- Recipe GUI发布R1已真完成：`ux3-recipe-publish-result.json`与`ux3-recipe-published-result-dark-1080.png`，Build/Publication/Generation error全null，Invocation.Action=BuildAndPublish、Generate=false，原Project/GUID/App一致；原Publisher新版本Digest=`1f3c2d0ce7f14a4f230afd146dcabe3cc5ec2bf10e4c9b5756a7f003673a9b56`、IsRepeat=false、CopiedBytes67887。elapsed34895.68ms（嵌套窗口不相加）；原Recipe.Action未改。证据event已解绑。
- 长确认文本缺陷已正式修复：新增仅Editor的`ResourceOperationConfirmation`轻量可滚动只读Modal，不持有/执行操作回调，不截断身份/范围，关闭或Esc取消；只在确认后由原队列/正式服务继续复验。UX3新动作改用该确认，UX4可复用完整C02范围。`LongOperationConfirmationIsScrollableAndEscapeDoesNotAccept`先RED→实现→GREEN1/1（0.15s），真实滚轮使scroll.y增加、Esc不接受；后续当前部分成功流程会实测产品Modal正确认。
- V2部分成功GUI已真完成：`ux3-partial-success-v2-result.json`，BuildError/PublicationError=null、GenerationError为原ResourceReferenceOutput第42行拒绝本根owned-canary.txt，未覆盖/接管。新R2Digest=`aba98175d0a545162043457fc181db53e1b142d6c61a01d8a8fcd089c4e353ee`、IsRepeat=false、CopiedBytes67930，发布未回滚、未自动重发。原序列实际包含PublicationStarted/ReferenceGenerationStarted/AttemptFinished。`ux3-partial-result-dark-1080.png`分项与部分成功提示真实非空；`ux3-partial-min-dark-620.png`结果滚动和主操作/输出可达。`ux3-partial-protection.json`记录canary SHA256=`5ff225cbbadaefc12f9c538e7369164d42e6680b529a4b815648c57c0f551f3a`、仅txt/meta两文件、已加载Bundle0/无Runtime会话；原txt逐字实读不变。测试stage event已解绑。
- UX3进一步功能修正：底部失败brief原先只显示Recipe路径，而详细结果已正确；新增实际statusBrief断言看RED（测试最初误断言full VisibleStatus修正，不计为有效RED），现在按BuildError/PublicationError/GenerationError结构化分项给brief，BuildError用Error、发布未确认/生成部分失败用Warning，完整冻结选项/日志仍保留原文本；等待下述组合run验证GREEN。不存在按成功/错误文案猜状态。
- 上述Recipe/provider组合**同次最终Passed18/18、216.30s、无Skipped**，`ux3-provider-regression-run.json`；active时collector曾以全测试树1964计Unknown/未提交leaf，最终实际只执行18项，不是重跑全库，未取消或启动第二run。包括原provider修改Project策略被拒绝、缺key/取消不创建输出，以及新brief/保存/结构化前置/容量/失效等。UI策略来源在mismatch时改为明确Recipe.Project，静态自身检查不冒充当前Workbench上下文已匹配；后续UI/确认/brief定向3/3通过。
- 新增owned `MismatchProject.asset`及独立空`MismatchContent`（Id=`yl-workbench-ux2to5-mismatch`，未构建/发布），正常Recipe仍引用主Project，`ux3-recipe-mismatch-dark-1080.png`真实展示双方Id/路径/GUID及禁用主操作。native点击“切换到Recipe.Project”成功返回主Project，`ux3-mismatch-switch.json`证明Recipe.Project/Action未改、recipeDirty=false、无Runtime。随后native脱离/输入坐标尝试未成功（误落分析页、App未改），`ux3-static-rejection-dark-1080.png`只保留失败取证，不当输入通过；实核未Play/切换、Main不dirty。实际静态拒绝采用显式测试表单预置App=bad/app后真实OnGUI，`ux3-static-rejection-light-620.png`显示阻塞与所有写入控件禁用。已恢复有效App及原深色，其他DPI未测。
- UX3最后门禁修复：底部历史选择原在pending时仍可选、会影响排队目标。`PendingOperationAlsoGuardsTheBottomHistorySelection`真实OnGUI先RED（测试样式名大小写修正后才计有效RED），加IsBusy禁用足部历史，GREEN；与原“选择最近尝试不污染定义状态”合跑2/2。确切BuildResult在点击时捕获进队列，服务执行仍复验，不读取可能变化的report选择。原Resource.md已增量维护配置/保存/预检/观察/结果/恢复合同。
- 当前在途任务：无Test Runner/构建/发布/查询，测试stage capture已清；Runtime未启动、Bundle0、Main不dirty，主Unity仍原PID、临时Restore用于GUI。新的Scroll确认Modal与字段/来源变化只在Editor程序集，未改Catalog/持久协议/密码实现。UX3必要门禁通过，后续独立Review仍待最后一次统一检查。
- 下一动作：UX4读取原SigningWindow、Publisher.ReadHistory/ReadCurrent/ReadVersion与BuiltinDeployer原API及合同；TDD接确切版本窗口、历史/Index独立实读、完整部署范围预检/确认/执行/独立恢复。只操作本轮R1/R2及owned身份，凭据临时工程外不入资产/日志；C02只本轮项目版本+原owned workspace和对应新meta。UI仍使用原Workbench并退出正式替代后的旧交付页。UX4门禁后直接UX5；不暂停授权、不Git写、不扩展8.10。

### 29.23 UX4实施恢复检查点

- 已实现（未宣布门禁完成）：Publishing/ResourceReleaseIdentity.cs只读完整身份（窗口可序列化纯值，不是新账本），执行用原ReadVersion重新核对GUID/Project/平台/Unity/App/BuildId/Digest/源目录；SigningWindow新增明确OpenSelected/SelectRelease，旧Open(Project,App)当前版入口仍保留，普通绘制不读取PFX/历史。Service/ResourceReleaseTable.cs与ResourceDeliveryView.cs替代旧BuildView交付页，原生排序筛选保留对象身份，历史/Index分开显式读，恢复/常量独立页；C02原服务未重写。新增ResourceBuiltInScope完整只读范围说明，以及原Deployer纯路径GetWorkspacePath(Guid,Platform)供独立恢复定位，无目录/锁/journal读取写入。
- 当前定向测试：UX4 4项有效RED→GREEN，另GUI只浏览/1080/720/620不读取历史Index/建部署/启动Runtime通过；导出目录合同修复新增RED→GREEN。最近完整`ResourceWorkbenchUX4Tests` 6/6 Passed（15.16s），包括完整范围owner/lock/journal/next/stage/外壳/receipt/payload/meta/import/未查容量、确切签发拒绝未登记版本且不回退当前、原生表身份、只读恢复路径及只浏览不执行。最初测试Payload误加BuildId/未改项目Native命名，已改真实Schema派生再得到有效RED，不作产品故障。
- 已实际GUI读owned历史2版本及单独ReadCurrent，R2有Index current标记；native选择R1，`ux4-history-index-dark-1080.png`、`ux4-selected-r1-index-r2-dark-1080.png`。全身份/Bundle delivery与支持字节分开，历史复制/耗时不把默认0作测量，不标“最新/发布时间”。
- 导出现场中断已续接：当前唯一Unity仍PID15324，旧HWND3866700同次确认框核对完整R1与新Digest命名目录后native确认。`ux4-export-digest-confirm.png`、`ux4-export-r1-result.json`实证：原ExportRelease成功后ResourceLocalSnapshot.Open完整通过，18文件/80939B，R1Digest不变，Publisher当前Index仍R2，无Runtime。早期任意目录名`export-r1/`复制虽通过ExportRelease却不能按正式Snapshot.Open（要求目录名Digest）；已修UI为工程外现有父目录+/<CatalogDigest>并回归，保留旧失败副本/截图，不删除或冒作成功证据。当前无在途导出/测试/构建。
- 签发GUI已真完成且scope已收尾：真实交付“签发此选定版本”控件→原SigningWindow→原按钮→完整确认（`ux4-sign-selected-r1.png`、`ux4-sign-r1-confirm.png`、`ux4-sign-r1-success.png`）。使用工程外自有RSA2048 PFX、channel=`ux-night-local`、keyId=`ux-night-sign-20261017`、sequence17/有效2天、None不要求加密渠道checkbox。`ux4-sign-r1-result.json`公钥实际Verify通过，Payload.Digest=R1，Index仍R2。临时进程YL_RESOURCE_SIGNING_PFX/PASSWORD已调用动态scope.End恢复（原值不输出）、回调解绑，自有PFX已删除，未安装证书/改信任；只保留公开`ux4-public-key.xml`/`signed-r1-release.json`及非秘密证据。签发窗口已关闭。
- C02真实GUI已通过：预检R1时target/workspace均不存在（`ux4-c02-before.json`、`ux4-c02-preflight-r1.json`）；实际按钮进入原Preflight，完整scope页和可滚动确认顶部/底部PNG明确owner/lock/journal/next/stage、StreamingAssets根/Project/平台/版本/控制记录/payload~、新.meta/AssetDatabase、未查磁盘容量与非峰值、无Runtime切换。native核对完整owned身份后确认原Deploy；`ux4-c02-deploy-r1-result.json`IsRepeat=false/CopiedBytes80939、payload内meta0/版本外壳meta存在，Index仍R2、无Runtime。新owned workspace=`AssetBundles/YLBI_4d9276be6c8b39af`，新StreamingAssets仅本轮Project/R1层级及必要ResourcesRoot外壳。
- C02重复/恢复/拒绝：服务API同一R1重复IsRepeat=true/CopiedBytes0且全部旧文件mtime未变；原internal测试checkpoint在自有R2 StagingCreated故障留下正式journal/stage（R2未安装、R1仍有效），未向产品暴露注入。随后实际“独立C02 Recover”GUI与完整确认（`ux4-independent-recovery.png`/`ux4-c02-recover-confirm.png`）经原Recover只清已登记私有stage；`ux4-c02-recover-result.json`journal/stage/next全无、R1有效/IndexR2/无Runtime。另只在本workspace创建自有unknown canary，原Preflight InvalidDataException拒绝且未删除，测试finally只核对并清此canary，`ux4-c02-unknown-refusal.json`。重复与故障注入/unknown拒绝是服务API验证，恢复和首次部署是实际GUI，不混称全为桌面操作。
- UX4最小/主题：`ux4-history-min-light-620.png`真实非空2版本/Index标记，表格/详情/任务入口可达；深色1080的历史/完整身份/部署/恢复已实测，1.75DPI。已恢复原深色和1080×720；无签发私钥/环境scope、无在途测试/发布/部署/查询。C02 R1测试部署暂保留供后续整合，最后按owned清理/保留决策记录，不接管用户既有层级/meta。
- 下一动作：UX4必要技术门禁通过，维护Resource.md/owner记录后直接UX5诊断/E05/报告与生命周期整合；最后补真实停靠/1440统一GUI与一次独立Review、最终保护快照/meta/Git终态及恢复主Unity最小化。原Branch/HEAD本轮未Git写，不把剩余整合/Review当已完成，也不提升用户验收层级。

### 29.24 UX5 / 独立Review恢复检查点

- 已实现UX5代码，尚缺真实运行会话/最终GUI闭环：原ResourceDiagnosticsView三子页为运行会话、E05只读Store、报告/开始使用；保留同次report全部原集合，并补Content/Counters、Assets queue/cache eligibility/RequestedAddress、Bundle content/cleanup、Scene native责任等字段。弱主体引用与窗口临时标签不作SessionId，无私有Mode/配置反射；默认auto关闭，离页/项目变化/来源切换/Play转换/重载/关闭暂停，返回不自启；历史仅显式设置128/关闭时按原语义清空，不在绘制或关闭改应用设置。
- 新Service/ResourceStoreQueryView、ResourceArtifactTable、ResourceGuideView：完整手工root/ProjectId/Platform/App纯值表单，不以Workbench Project当应用配置；原QueryAsync主线程32步分段/单在途、取消先断身份并Dispose，无callback不假造Canceled。结果保留原root/closure/maxEntries身份；未知/不完整字节与有效0、Ready非App/解密/挂载认证、CompleteRecord非SHA/签名、候选非删除授权全部明示。无Recover/cleanup/mount/provider/key/pin按钮。Guide只指本窗口原report和已知文档/源码路径，不扫Temp/Plan、不执行样例或硬编码F03当前性能，不引用Tests类型。
- 新UX5测试4项先RED→GREEN；独立Review补真实dock隐藏生命周期候选后，新增OnBecameInvisible仅暂停诊断/Dispose查询，Play OnEditorTransition才取消pending，未以失焦代替可见性。新增隐藏/保留原合法queue断言两次有效RED→GREEN，最新UX5 5/5 Passed（0.23s）。真实dock事件和620带运行report/展开Output仍待实测，不把方法调用当dock证据。
- 本轮唯一独立Reviewer内部Task=`ses_eb3f2b962ffeycsMNBVuSf0IYS`，分段只读覆盖UX2～UX5。原5项P2已源码复核并新增6回归RED→修复→与UX5首4合跑10/10 GREEN：历史ExactReportStale合并IsHistoricalReport、共享App缓存失效但不重算Map、Atlas Inspector Save(expected)拒绝副本误存、Delivery history/index/selection各自stale且新receipt清旧表选择、Archive捕获原Release用原VerifyExportInput保留旧Unity字节归档（签发/C02仍ReadVersion）。Reviewer源码确认这5项闭环；新增第6项dock隐藏已由作者上项修复/回归，Reviewer没有重新执行Unity，最后实际dock待补。无P0/P1，未做第二Reviewer/全库审计。
- E05真实自有Store：`ux5-owned-store`在证据根，使用原Store Mount/Check/Prepare/Commit（仅fixture setup通过已有内部正式服务，未改控制格式），真实Native R1/R2两个版本，activeR2/LKGR1及未提交反向Prepared叠加保护；没有ResourceSystem。`ux5-store-fixture.json`登记；UI手工表单输入App=`operator-supplied-not-authenticated`（与实际App不同，验证owner不认证App），真实native“开始只读查询”→Ready/8entries/SizeComplete=true/ObservedBytes165375/CandidateBytes0。`ux5-store-gui-result.json`含完整原report、Store前后字节快照相同、Runtime=false；`ux5-store-explicit-input.png`/`ux5-store-ready-real.png`非空实图。查询已完成Dispose，无在途查询，非从伪report填表。
- 运行GUI同次已取证：Begin只新建/保存本根空UX5Runtime.unity并进入其Play，实态仅此Scene（没有原Main游戏Scene），原ResourceSystem显式装配R1；真实native“采样/刷新”得到Ready/1Project/2Assets/4Bundles/1Cache/HistoryCapacity16与4条History。`ux5-runtime-gui-report.json`是窗口同次原完整report，Content Budget134217728、ProcessCngKeyCount0（None；未测试新加密路径，不冒充native memory）。`ux5-runtime-{projects,assets,content}-real.png`及before-sample均真实1.75DPI；没有产品自动Initialize或反射私有Mode/配置。
- **当前owned运行收尾在途：** 620×440+展开Output真实图`ux5-runtime-min-output-before.png`确认底部内容被挤出可视区域，不能作为通过。已主动调用同一RuntimeEvidence.End()，only own instance/cache/prefab/text已Dispose、等待原Shutdown/native drain后helper自动退出Play并在EnteredEditMode恢复SessionState记录的原SceneManagerSetup。**不得重复Begin或直接关未知会话/游戏；先读同次ux5-runtime-run.json直到ClosedAndOriginalScenesRestored，核对无Runtime/Bundle、Main不dirty后才改源。**
- 下一动作：修复Runtime窄窗长头部+数据区的可滚动边界，保证展开Output不把主控件/底部挤出；先用回归复现/验证，再以新明确run身份复用已登记自有空Scene做必要重验（不是重启超时旧run）。当前wide运行证据保留有效；旧失败图不删。实际dock隐藏/1440/查询取消旧结果与最终文档保护/meta/Git收口仍待完成；独立Review原6项均已作者修复/回归，隐藏项修复不以失焦替代，实dock验收待补。
- 完成上述运行GUI后：实际dock隐藏/宽屏1440/620展开Output/主题、Store取消旧结果/重载最终验收、必要定向合跑与文档/保护快照/meta/Git只读终态。新C02 R1层级/Workspace与Store/原Source夹具均own，最终清理/保留需明确，不改原未知内容；主Unity目前临时Restore，最终恢复原最小化/Console/Selection。UX5未完成，整体不得标8.9/Stage8 COMPLETE。
- 运行窄窗缺陷续接：旧run已真实ClosedAndOriginalScenesRestored（Main不dirty、Runtime=false/Bundle0）。新增`MinimumRuntimeWithAReportAndExpandedOutputKeepsTheOutputReachable`以RootContentHeight实际527>440得到RED，Runtime窄窗改为自身完整滚动视口+列表/详情固定子区，主页不再挤出Output；GREEN，最新UX5 6/6 Passed（0.30s）。最初尝试textArea捕捉因无status走Label而未捕获，未算有效RED；改测真实root总高度后有效。
- 新回归run实际Ready并完成真实采样：`ux5-runtime-min-output-fixed.png`/`ux5-runtime-min-output-regression.png`为620×440、非空report、展开Output完整可达，数据区在独立可滚动视口内，不挤出底部；`ux5-runtime-wide-1440.png`为实际1440×860宽屏，仍1.75DPI。第二次实际采样2Assets/4Bundles/HistoryCapacity16，`ux5-runtime-layout-result.json`记录真实尺寸/DPI/Theme。该次请求切浅色后实读isProSkin仍true，**不计作浅色Play数据通过**；此前UX2/4浅色实图有效，UX5浅色编辑态Store/指南还待最终覆盖，不伪称缩放/主题。
- 真正dock生命周期已过：只关闭自有浮动Workbench，再将同一产品窗口GetWindow dock到SceneView现有tab，实际rect1091.71×800.71。原native采样/auto按钮得automatic=true；以原只读controller Begin一个真实查询后Focus同dock SceneView sibling（未直接调用隐藏方法）。`ux5-real-dock-hidden-result.json`验证before auto/query均true，隐藏后visible/auto/query均false、无伪造report，应用HistoryCapacity仍16；再次Focus Workbench auto未重开。`ux5-docked-runtime-active.png`/`ux5-dock-sibling-hidden.png`是真实主Unity全屏图。最初Console区高度251低于约定440最小，不据此外推支持，未更改用户split尺寸；转正常SceneView区验证，关闭新增tab已恢复其原层级。native坐标未命中auto的工具失败保留日志，不当产品缺陷。
- **当前回归run收尾在途：** 已主动调用同类End，归还own leases/cache并请求Shutdown/native drain→自动退出own空Scene Play并恢复Main；下一次先读`ux5-runtime-layout-regression.json`确认ClosedAndOriginalScenesRestored/Runtime无/Bundle0/Main不dirty，禁止重复Begin或直接Shutdown未知主体。回归后只留一个自有浮动Workbench（当前HWND2231054，现场须重核），原签名环境/PFX已清。接下来仅余Store取消/旧报告/重载与Guide/主题补验、最终合跑和文档保护/meta/Git收口、owned清理/环境还原；不宣称UX5或整体完成。
- 上述第二run已实核ClosedAndOriginalScenesRestored，Main不dirty/Playfalse/Runtime无/Bundle0；Editor此时浅色isProSkin=false，后续浅色Store/Guide均按此实际值取证，不外推Play内浅色。
- Store真实GUI取消闭环：先原UI得Ready8，再只在本owned Store的prepared目录创建8192个自有单字节17 canary延长查询，原UI Start(maxEntries100000)→真实Cancel；`ux5-real-cancel-result.json`query=false、保留同一原Ready对象、oldReport=true、无假Canceled/Runtime；`ux5-store-canceled-old-light-620.png`标旧输入与旧身份。finally逐个核对长度/内容后只删8192个own canary，原Prepared JSON仍1个，`ux5-cancel-fixture-cleanup.json`。
- Store展开Output也补同根因回归：新`MinimumStoreReportWithExpandedOutput...`先RED root452>440，结果在窄窗独立滚动视口+固定条目/详情区域后GREEN，UX5当时7/7。实际`ux5-store-min-output-fixed-light.png`为真实8条Ready、620×440、浅色、展开Output可达（不是空表或伪report）。
- Guide真实缺失链接拒绝先验证：旧猜测ResourceCIExample路径缺失，点击只明确不可用、没有未知替代搜索/执行，`ux5-guide-min-light.png`是旧失败/空报告取证。工程实现按Resource.md已明载正式Samples/ResourceStartup/Editor/ResourceBuildRecipeExample.cs修正；标签明确“无窗口Editor样例/非通用CI”，新增References实际文件断言先RED待下次GREEN，产品仅定位/打开不调用Tests类型。下一次要真实点修复后入口并记录selection/无Play/无Runtime，不以旧失败图冒称通过。
- **当前最终定向suite在途/未读终态：** 最后tests-run query为UX2全部、UX3除已验nativeObserver、UX4全部、UX5全部、原Window/Workbench/Interaction/Map、Recipe除2个已沿用native完整用例；工具MCP-32001等待超时。禁止重跑、active时不改产品或夹具源；按同次Tool_Tests._resultCollector summary/results/startTime与IsRunActive读取最终suite，保存`ux2to5-final-targeted-run.json`。RunStarted临时1964是全树计数，不等于实际运行全库；只能最终summary确认选定数量，不取消/重复启动。
- 取最终suite终态后剩余：修复失败（若有）、修复后Guide点击/只读轻验证、Resource.md/owner/Plan记录、metadata与protected-before快照比对及只读Git终态。最终仅清理自有C02测试层级/workspace或明确保留不默认导出，原Source/Project/Recipe/Atlas/两正式发布与公开证据适量保留；所有清理须核对own身份与未知额外文件，不删除失败历史证据。关闭自有窗口、原Main/Console/Selection GUID52d3fa0622a3f3b428a754d82a63736c及主最小化/深色还原，确认无在途任务/查询/订阅/Bundle/CNG owner。之后一次最终报告，不新建重复计划，不Git写，不8.10。

### 29.25 UX2～UX5技术交付与最终终态

**UX2～UX5连续实施技术完成，待用户功能与交互验收；首版页面已完整接入，后续按用户决定集中优化UI设计。** 本节为当前有效结果，前节在途/待做文本均是已完成或已处置的历史检查点，不提升8.9、Stage8、E04/C05/B02/F03或用户视觉验收层级。

- UX2交付：物理Bundle原生多列表、预估/确切BuildResult双来源、可见源文件/主地址/依赖闭包区分、正反向Bundle树、来源有限快照及身份复核定位；同一Atlas配置字段/明确Save与Ctrl+S/状态计划/原生成准备/Obsolete确认保护。原Catalog v1、Bundle身份、选择/依赖算法及Atlas/Publisher所有权不改变。
- UX3交付：Recipe同资产内联与原无配方入口、不同Project双方身份/拒绝/显式切换、保存对象明确、原Action唯一执行。原同一Preflight共享结构化只读状态，旧string入口保持有效异常与早返回；真实阶段只观察且异常隔离、执行重入拒绝，不代替原provider冻结/后验/finally。构建/发布/生成分项、部分成功/提交未确认恢复说明、32条原Project有界尝试/旧报告失效；排队目标点击时捕获，各输入/历史同门禁，不从raw/证据复活临时报告。
- UX4交付：owned历史与Index分开显式验证、原生版本身份不随排序筛选漂移；历史/Index/选定来源各自失效，新receipt不被旧表反向覆盖。工程外父目录/<Digest>原ExportRelease、精确历史签发ReadVersion与旧当前版入口、完整可滚动确认、C02 Preflight/Deploy/独立Recover和Publisher独立恢复；Archive保留原历史Unity/App字节复验，不借签发严格ReadVersion缩减旧导出合同。唯一Deployer新增接点只是纯GetWorkspacePath定位，不读写journal，不改原C02事务。
- UX5交付：全会话同次report所有原分项与Content/Counters、弱主体/明确旧快照、5s显式auto、历史清空/不回填说明、实际dock不可见与转换/离页/关闭/重载收尾；手工完整root/closure/App的原E05单在途主线程分段查询、真实身份旧结果、取消Dispose无伪callback、各状态/保护/完整性/候选成本语义。无Runtime私有配置getter/反射预填、无Runtime→Editor依赖、无key/mount/pin/recover/cleanup/删除/激活按钮。报告/指南只指窗口原report和已知文档/正式样例，缺失明确不可用，无Temp/Plan扫描或样例自动执行。

**验证与Review**

- 当前唯一Unity 2022.3.62f3c1 / Windows Editor / StandaloneWindows64实际编译通过；最终同一次定向suite `ux2to5-final-targeted-run.json`为**75/75 Passed、Failed0/Skipped0、5分59.69秒**，工具超时后读取同次RunFinished，不重启。之后仅测试证据helper改为inactive时不挂永久Editor update，及正式指南路径，针对性2/2 Passed；不使75项产品验收失效，不把分别运行次数相加为唯一测试总数。Runtime/签发/部署等真实GUI证据与此前原服务证据分别列于§29.22～29.24；原176组Player/全审计/F03矩阵未重跑，密码后端/远端门禁未改。
- 一名独立Reviewer（Task `ses_eb3f2b962ffeycsMNBVuSf0IYS`）分段只读审UX2～UX5，发现6项P2，无P0/P1。前5项经Reviewer源码再核对闭环；最后dock隐藏按其建议只暂停诊断，不改变原合法queue取消责任，作者TDD与真实dock验证闭环。新增6回归先有效RED后修复，与首批UX5合跑10/10；真实隐藏/窄窗作者回归不冒称Reviewer独立跑Unity，不额外循环审整个Resource。
- 真实桌面鼠键、真实OnGUI事件和服务API证据分别记录。必需非空GUI覆盖Bundle双来源/依赖、Atlas规则与实际生成准备、无Recipe/Recipe、发布成功与生成失败分项、R1历史/IndexR2、导出/签发/C02完整范围、运行资源及E05结果/取消旧报告。代表图：`ux2-analysis-sources-dark-1080.png`、`ux2-atlas-rule-editor-fixed-dark-1080.png`、`ux3-partial-result-dark-1080.png`、`ux4-selected-r1-index-r2-dark-1080.png`、`ux4-c02-confirm-{top,bottom}.png`、`ux5-runtime-content-real.png`、`ux5-real-dock-hidden-result.json`、`ux5-store-ready-real.png`、`ux5-store-min-output-fixed-light.png`。均在同一owned证据根。宽屏1440、1080、720、620×440及正常SceneView dock1091.71×800.71有实际取证，**仅pixelsPerPoint1.75**，不外推其他DPI/全状态主题笛卡尔积。
- 保留失败证据：Atlas初期dummy Rect空详情、导出任意目录不能Snapshot.Open、旧Guide猜测路径、native输入未命中、运行/Store展开Output超窗等，均明确失败或未计通过；相应功能错误已修复、回归/实际重验。旧Guide未自动找替代，后按Resource.md正式ResourceBuildRecipeExample改正；实际点击现已定位该脚本（`ux5-guide-fixed-locate-result.json`），Play=false/Runtime=false/Main不变，且明确非通用CI。

**保留与清理**

- 可体验源根：`Assets/YLFramework/Tests/Fixtures/WorkbenchUX2To5-20261017/`，owner JSON登记Project GUID/两Recipe/独立MismatchProject/Scene/Atlas/canary；默认框架ExportRoots只含Editor/Plugins/Prefabs/Runtime/Samples/Documentation/许可，**不含Tests**。原正常Recipe BuildAndPublish/Generate=false；PartialFailureRecipe为明确自有阻止接管目录，用户可重现部分成功；没有改原UX1夹具或用户配置。
- 两版正式owned发布保留：R1=`1f3c2d0ce7f14a4f230afd146dcabe3cc5ec2bf10e4c9b5756a7f003673a9b56`，R2=`aba98175d0a545162043457fc181db53e1b142d6c61a01d8a8fcd089c4e353ee`，Publisher Index仍R2。位于`AssetBundles/YLFramework/yl-workbench-ux2to5-20261017/StandaloneWindows64/`，可在UI ReadHistory/ReadCurrent显式体验，不扫描猜测。
- C02专用R1部署及idle `YLBI_4d9276be6c8b39af`已由原Preflight完整/归属复验，核对共享Root仅本Project/其meta/owner、workspace仅owner/lock且无journal/stage/next后，删除本轮Project及原本不存在的新外壳/meta、空闲workspace；`ux4-c02-owned-cleanup-result.json`证明StreamingAssets与部署前**完全一致**。不留测试内置数据进后续Player，不删除/覆盖用户原部署；所有旧失败证据仍在。8192个查询延时canary逐一核对后清理，原Prepared记录保留；Store只是本轮工程外数据，不在Runtime激活。
- 证据根：`C:/Users/Kano/AppData/Local/Temp/kilo/workbench-ux2to5-20261017/`。保留Digest命名导出、公钥XML/已签R1元数据、Store及JSON/PNG；自有private PFX已删除，进程签名环境原值已恢复、不入SO/日志/截图。两次own空Scene Play都已由原Shutdown排空后退出并恢复Main，未加载或运行原游戏；最终ResourceSystem无、Bundle0、CNG provider/key/key-object bytes0、无本窗口stage观察/Editor update/查询/排队订阅，inactive证据helper不挂永久update。

**工程与环境终态**

- `ux2to5-protection-meta-final.json`：初始保护文件/树全部相同（.gitignore/工具配置/AGENTS/并行UI Plan/Core Fixture/StreamingAssets父meta，Games/Scenes/StreamingAssets/MainModule/Packages/ProjectSettings/NuGet/UX1体验夹具）；新fixture缺meta0，脚本meta齐全，Editor Resource代码不进Player，产品Editor程序集不引用Tests。没有改Packages/ProjectSettings/AGENTS或其他worktree；现有dirty插件/配置成果原样保留。
- `ux2to5-source-manifest-final.json`记录54个相关脚本实际SHA与meta存在性，起点是8abaf2d加保留的未提交UX1及本轮源码，不以HEAD冒充完整源码固定。Git终核`fusion/8-resource`、HEAD仍`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区空、意图范围diff --check通过；工作区差异含原UX1和用户既有升级，本轮不暂存/Commit/Push/Merge/Tag/切分支/Reset/Clean/Stash/Rebase，等待用户最终验收后另行明确提交授权。
- `ux2to5-final-unity-state.json`：唯一主Unity PID15324（另两AssetImportWorker），非Play/编译/导入、compileFailed=false、Main不dirty、Runtime无/Bundle0/CNG0、owned窗口/更新订阅/stage observer均0，原Selection GUID=`52d3fa0622a3f3b428a754d82a63736c`、Console焦点与深色已恢复。最后native Window实核主Unityminimized=true，HWND197584，无多余本轮窗口；无后台进程/在途测试/查询或待清临时secret。
- 后续仅用户决定的视觉细化：整体比例/排版/文案密度、输出高度拖拽、跨Domain Reload行选择等UX1已记录的非阻塞差异；不把这些写成已完成最终视觉验收。没有保留已知阻断功能缺陷，不安排8.10/Stage9/跨Unity运行适配/新美化。完成后停止，待用户功能与交互验收。
- 恢复规则：先读本节及相应批次最新证据/同一次run结果，不重复启动仍在执行的测试或构建；只有授权内无法安全解决的真实阻塞才停。原Plan为唯一进度记录，不新建重复Plan/日报/审计文档。

### 29.26 Workbench易用性与视觉精修授权及实施（2026-10-18）

- 用户已查看首版真实截图，授权一批连续完成局部设计、Resource Editor实施、直接测试、唯一主Unity真实GUI、缺陷修复、一次独立Review及本Plan/Resource.md维护。三项目标同时落实：控件可读可点、有效数据空间更充足、普通Unity开发者能沿现有入口完成None构建发布。保留IMGUI/TreeView、620×440最小尺寸和唯一原服务/队列；不重开Resource总体设计或另建向导/任务/主题平台。
- 本次实核：`fusion/8-resource`，HEAD仍`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区为空。真实起点包含未提交UX1～UX5及其他用户/并行成果，全部保护，不以旧Commit或旧终态覆盖。主Unity PID15324由当前进程路径确认，另两为AssetImportWorker；Unity2022.3.62f3c1/StandaloneWindows64，非Play/编译/导入，compileFailed=false，Main不dirty，TestRunner inactive，Runtime无/Bundle0，无Workbench窗口。当前Selection GUID=`93723fc48dfe6e148b67ef1d7c985000`，焦点Console，深色、pixelsPerPoint1.75；不得用上轮Selection/句柄替换本次真实起点。
- 简短改进清单：Workbench局部居中控件样式与24～28point数据行；削减重复标题/参数/常驻说明；输出可拖高度、紧凑空态、窄窗钳制且警告摘要不依赖展开；构建结果优先总体/分项/原因/下一步；确认框采用结构化只读摘要及同窗完整可复制技术范围；资源/图集/分析/交付/诊断统一渐进信息与准确术语；指南以一Project、多Group、None、无Recipe为入门叙事，不修改既有选项。延续§29.17～29.20根路径、基线、更多向内、失焦、Group提交/Undo合同。
- 实施顺序：先记录当前真实GUI尺寸/控件几何和同条件修改前图，再共同绘制/输出/构建/确认；随后应用到各页/Inspector及纯UI状态，完成直接回归、代表性深浅主题/1080/1440/720/620/正常dock与任务走查，最后一次独立Review、修复及文档/保护/环境收口。新增交互先复现或测试RED，再验证GREEN；不以静态截图、反射造Editor或服务调用冒充桌面鼠键。
- 验证边界：旧夹具与R1/R2默认只读；需要构建/失败注入时创建登记的新小型owned身份。只用主Unity，不运行原游戏、不保存dirty场景、不结束未知资源会话。工具超时先取同次leaf与suite终态，不重启；不重跑E04/F03/176组Player/Atlas全矩阵或31项审计。只1.75DPI有实证，不改系统缩放。部署确认仅展示改动可用原Preflight+复制/取消零写；若执行接入改变，补本轮owned成功路径与精确清理。
- 追加反馈（2026-10-18 08:28Z）：用户要求继续解决按钮选中效果、界面层次、整体美观及工作流程不直观。不是再统一增高或换颜色；在同一IMGUI工作台中区分主任务导航、页内选择、checkbox选项、普通操作和蓝色主操作，Group选中行使用独立强调，不再把所有分类/开关画成同等灰按钮。主任务名称为“资源配置 / 分包分析 / 构建发布 / 版本交付 / 诊断”，指南与空状态明确“配置资源 → 保存 → 构建并本地发布 → 查看版本”，快捷入口只切页，不自动写入或执行。
- 当前实施：局部字段26point、行26、工具条30；原生Popup实际仍18的差异已用真实布局及RED用例抓出并修到26。输出高度拖拽/钳制、原生完整确认复制、构建分项结果、图集/来源/交付/诊断渐进信息、稳定定义身份行选择均已接入。真实基线及本批对照位于`%TEMP%/kilo/workbench-polish-20261018/`；`before-*.png/json`为改前，`hierarchy-*.png`承接追加反馈，早期`after-*initial`等不冒充最终图。
- 回归检查点：首次9类UI同次测试62项，60通过/2失败，终态已从原collector回收并保存`ui-regression-run-01.json`，未将超时当成功或重复启动。失效旧入口测试改为真实点击“解析预览→计算预览→选行”，保护断言不删；折叠输出失败原因被首行摘要截断的真实问题已修。随后定向2/2通过；新增下拉高度、设置页保存、选择身份/确认复制等组合13/13通过；追加导航选中态/disabled实际GUI事件用例RED→GREEN，现相关组合14/14通过。尚需最终分组回归与完整GUI收口，不将这些检查点合并冒称一轮全部通过。
- 一次独立Review由`ses_eb20cb2e4ffe6EcnQe9Y2RQ4pw`完成只读源码/测试/已有GUI证据检查，发现2项P2、1项P3，无P0/P1：项目设置Ctrl+S误保存后台Recipe/Atlas、无Recipe确认完整复制漏force、窄窗下拉/结果切换实际高度不足。保存分支已显式排除Settings，原确认文本补冻结ForceRebuild，Popup/切换布局修正；保存用例先RED，14项组合GREEN，完整force复制另走本轮真实确认取消验证。Review未独立运行Unity，追加反馈关联的展示改动继续定向自检，不循环审查Resource内核。
- 现场变化：等待回调未如预期恢复，用户催促后已继续实做；原62项终态有效，进程更换前另一次2项请求没有可靠终态，不计通过。期间主Unity由15324经8796更换为8296（外部现场变化，代理未启动/终止任何Unity）；每次重新核对PID/窗口，不复用旧句柄或恢复旧dirty资产。新owned夹具为`Assets/YLFramework/Tests/Fixtures/WorkbenchPolish-20261018/`，ProjectId=`yl-workbench-polish-20261018`、GUID=`1cfc301941c7da94abc035da6aecfed9`、App=`polish-app-v1`。旧UX夹具/R1/R2只读；重启前未保存添加不强制恢复，已通过真实菜单项和顶部保存重新显式完成，`walk-add-and-save-native-confirmed.json`确认messages两条登记、optional一条、dirty=false、额外GUID已落盘。较早`walk-add-and-save-final.json`的saved=false仅为失败尝试，不是成功证据。
- 上述“进行中”是实施时点记录；以下收口记录为本批最终有效状态，不覆盖历史失败、工具超时或当时授权。

#### 29.26.1 最终交付与验证

- **Workbench易用性与视觉精修技术完成，待用户最终视觉、功能与交互验收。** 三项目标同时落实：原生字段/按钮/行命中舒适度，默认信息分层及有效内容空间，None无配方入门与清楚的任务路径；追加的选中态/层次/美观反馈以独立主导航、页内选择、checkbox、Group行高亮、蓝色主操作和构建双区分隔回应。不以代码/截图代替用户满意或最终UI验收，不标8.9或阶段8 COMPLETE。
- 真实同条件对照：旧UX2～UX5夹具完整树与本轮起点相同，`final-comparison-fixture-unchanged.json`已验证后再生成`final-compare-build-dark-1080`、`final-compare-resources-dark-1080`、`final-compare-analysis-dark-1080`的PNG/JSON，与对应`before-*`匹配深色、1080×720、DPI1.75、数据和展开状态。`final-layout-comparison-metrics.json`记录实际GUILayout point：构建输入18→26，普通按钮25～29→30，构建两区滚动视口536→584。资源页保留三栏和原生表；资源详情视口538→518，明确接受较高字段/底栏带来的局部空间取舍，不谎称每页同屏数据均增加，也不靠缩字或提升minSize凑结果。少源文件列表按条数定高，单文件不占原固定145point空白。
- 最终代表图：`native-partial-final-dark-1080`、`final-partial-output-dark-620`、`final-partial-output-dark-1440`、`final-cache-ready-dark-1440`、`final-cache-visible-row-light-620`、`final-guide-light-720`、`final-atlas-rules-dark-1080`、`final-workbench-docked-dark`。全部源于唯一主Unity实际渲染/桌面取证；GUI尺寸与DPI以相应JSON为准，不按PNG像素倒推。深浅主题、1080常用、1440宽屏、720/620窄窗及正常dock有代表证据，不扩成全主题×尺寸矩阵，不外推其他DPI。
- 原生桌面流程：显式准备本轮源文件Selection后，通过真实添加菜单项和顶部保存完成定义落盘；None无Recipe原生主按钮/确认完成R1发布，新增文件后再次真实添加/保存/配方执行完成7资产发布和生成失败。原生Ctrl+V填写空App、点击空白提交后由StaticBlocked转StaticReady，未触发构建或保存资产，见`native-app-blocked-to-ready.json`。界面以原异常类型/参数名及空App值映射字段明确的提示，原始错误留高级信息，正式state/error不变；不解析日志猜原因。
- 本轮发布仅自有身份`yl-workbench-polish-20261018`，三版保留且末次原ReadHistory/ReadCurrent复验有效，见`final-release-readback.json`及fixture owner：R1=`a15d15705c4f9c5d5c4a4bec23e5648be4bf2bd86948be3e2a10d3527fa927e3`；R2=`dabe96fcbd81c845e1790a0649ba78cff110a73ec54b8822f6070a25496b73e1`；R3/当前Index=`5ee1d92ef1b03559efbda7599b4da53ce4fedd78edc73408ea74a30f86589e04`。R3是文案编译后显式原生回归，并非产品自动重发。真实生成错误为`Published Project has different Unity/App compatibility: yl-workbench-ux2to5-20261017`；是原发布Catalog集合兼容门禁，不是本轮目录canary命中，不改旧项目/App/发布以强行通过。界面说明版本仍有效、按实际原因处理后可独立生成，不再一概要求修正目录。
- 确认验证：无Recipe真实force确认复制包含冻结`ForceRebuild=True`，Esc关闭、BuildResult仍无、Workspace/Publisher未创建，见`native-none-force-copy-escape.json`。真实读取历史与索引并鼠标选旧R1，确认所选摘要不同于索引R3；原Deployer只读Preflight后默认三类写入影响可见，同窗展开/滚动/复制完整owner/lock/journal/next/stage、payload~及.meta，最后Esc取消。`native-c02-copy-cancel-zero-write.json`确认全标识与范围存在、StreamingAssets完全相同、部署workspace未创建、执行/队列结束、剪贴板恢复。未接受Deploy，故无本轮部署产物需清理；历史服务成功路径不因纯展示变化全量重跑。
- 输出与状态：真实拖拽242.857147point，620×440显示钳制80，放大1440×860恢复原请求高度，未新增尝试/改版本/弄dirty项目，见`native-output-drag-wide`、`native-output-narrow-clamp`、`native-output-wide-restored`。`actual-reload-before/after`证明真实Domain Reload保持主任务、搜索、已有源GUID行、195/325分隔宽度及输出请求高度；不恢复BuildResult、历史读取、query/report或自动采样。表头对预置超出可用区的autoResize宽度重新适配，固定列和比例保持，见`actual-reload-column-adaptation.json`；不冒称固定像素完全相同。诊断子页和临时查询配置不作为跨重建执行状态。
- 缓存与诊断：真实手工完整闭包→原生开始查询，原callback报告Ready/8条目/IsSizeComplete/165375B/候选0，旧Store字节一致，见`native-store-query-final.json`。浅色主题切换重建视图时没有恢复旧报告或自动查询，先将误命名图改为真实runtime空态，再显式重填参数并重新查询，未冒充旧callback。最小窗展开输出曾只剩表头，真实RED及新用例记录内容高4point；移除正常完成的重复结束提示后GREEN，保留停止/旧结果/错误状态，真实有可读数据行。原生滚轮表格y变60、外层/输出仍0，见`cache-visible-row-real`及`native-single-scroll-query`。
- 正常停靠：通过现有SceneView的DockArea创建本轮唯一owned页，实际GUI区约1091.71×800.71、DPI1.75，PNG为真实主编辑器。与现有SceneView同dock互切，visible=false→true、自动false、query无，关闭只移除本轮页并恢复原选中pane。没有Runtime会话时自动采样checkbox保持禁用，未强设私有flag或初始化Runtime来伪造活跃采样；本轮空会话停靠显示与既有活跃生命周期直接测试/上一批真实证据分层解释，未重跑非空Play/Player。
- 测试：五组基础定向run分别14/14、15/15、18/18、9/9、9/9，合计65个不同用例；UX2/3工具超时后取同次终态（2分18.60秒），未重启。后续文案5/5、缓存可视行及生命周期12/12、空App和相关交互8/8均通过且有重叠。`final-test-evidence-summary.json`汇总**67个唯一受影响行为的最近叶结果均Passed、每份对应suite也Passed，Failed/Skipped均0**；不是单轮67/67，不将分组次数相加。新增App/缓存病例均有实际RED→GREEN，不删安全/保存/身份断言。未重跑E04/F03/176组Player/Atlas全矩阵或31项审计。
- 独立Review为同一线程一次审查及08:28追加范围补读，前述2项P2/1项P3已闭环；补读无新增实质finding。Reviewer只读源码/测试/PNG/JSON，没有独立运行Unity；后续真实走查发现的缓存可视行与空App指导由限定修复、定向测试和原生GUI直接复验，不循环全面审查内核。
- 文档和产物：Resource.md同步普通None流程、当前真实入口/术语、上下文保存、完整确认范围和UI状态边界；fixture owner补充真实三版、实际生成失败原因及取消零写范围。未新建重复Plan/日报/教程；58个相关脚本的当前SHA/GUID、17个本轮fixture文件/目录及.meta已核对，缺失为0，见`final-source-meta-manifest.json`。临时取证脚本只在工程外本轮Temp，恢复临时置顶，未更改工具权限/系统缩放或安装依赖。
- 保护：`protected-final.json`确认Games475、Scenes2、StreamingAssets450、MainModule448、旧UX1夹具128、旧UX2～5夹具61、旧发布38项逐文件hash与起点一致。Packages两文件、ProjectSettings.asset一文件、NuGet三文件出现现场并行差异（MCP版本/依赖符号及对应DLL），本轮未编辑这些路径，未回退或接管；不假称全工作区clean。Core/Runtime UI、AGENTS、工具配置、忽略规则和其他worktree未主动修改，保留所有已有/并行差异。
- Unity收尾：`final-unity-state.json`主PID8296、Unity2022.3.62f3c1/StandaloneWindows64，Main已加载且不dirty；非Play/转换/编译/导入、compileFailed=false、无在途测试。Runtime无、Bundle0、CNG provider/key/key-object字节/释放失败均0、阶段观察0、自有窗口/确认框/自有update均0。深色、DPI1.75，Selection为空且未用旧进程快照覆盖；主编辑器保持该现场原本的正常窗口状态，未复原早期进程的最小化状态。Console中的历史工具Roslyn错误保留，不冒充当前Unity项目编译失败，也未清Console。
- Git收尾：实际分支/HEAD仍`fusion/8-resource`/`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区为空，授权范围`git diff --check`无输出；无Git写。无本轮后台进程、wakeup或cron。用户最终视觉/功能/交互验收及第二次提交仍待明确决定；本批到此停止，不进入8.10、阶段9、远端Player或其他框架专题。

### 29.27 执行选项折叠关系修正（2026-10-18 11:08用户反馈）

- 用户截图确认：底部“高级选项与执行说明”实际上控制上方force/常量字段，展开后入口自身向下移动，控制关系和语义不直观。授权局部修正，不重开整体设计。
- 选择：附加构建选项入口放在其编辑字段上方；只读执行/密钥说明独立折叠，不控制设置。无配方与同一真实Recipe均采用同一关系，收起仍保留有效非默认值摘要；不改保存对象、Action或正式预检/执行协议。
- 当前现场：主Unity8296，非Play/编译/导入/测试，`UIAcceptance.unity`已保存且不dirty。此为用户最新场景，不能恢复上一批旧Main快照。新建测试窗口仅自有，保留现有选择及用户/并行差异，无Git写。
- 修复：基本字段之后增加“附加构建选项”，其下面缩进绘制force、地址常量生成和目录；只读帮助独立为“执行说明与技术详情”。无配方和Recipe一致，Recipe基本/附加字段仍通过同一SerializedObject机制编辑原资产，Inspector默认全字段调用保持；收起保留非默认摘要。展开状态不参与字段change-check，未改变Action、保存/队列/预检/执行与所有权语义。
- 回归：`AdditionalOptionsExpandBelowTheirAnchorAndStayIndependentOfHelp`先实际RED，旧版展开时字段插到入口上方（257→160的首个候选控件位置）；修复后无配方/配方双模式GREEN，入口位置稳定、字段在其下方、说明独立，force值/dirty状态/配方磁盘内容不变。Polish全组9/9 Passed（33.82s），UX3全组12/12 Passed（128.34s）；后者工具超时后回收同次suite和leaf终态，未重复启动。没有将21项计为无重叠新总数，不重跑整个Resource或Player矩阵。
- 真实GUI：主Unity8296/深色/DPI1.75，新增自有窗口内用桌面鼠标展开/勾选/收起/只展开说明，`followup-options-closed/expanded/help-only/narrow/recipe`的PNG/JSON保留在原本轮Temp证据目录。无配方入口在y183，展开后仍y183；收起保留force=true、generate=true的摘要且项目不dirty，单独说明为advanced=false/help=true，未创建BuildResult。620×440与配方模式也已实际渲染。未点击检查/构建/发布或保存，未改变原体验资产、旧证据文件、用户场景或系统设置。
- 状态：局部修复技术完成，待用户交互验收；Resource.md已同步。保留本轮用户实际`UIAcceptance.unity`及选择，不恢复上一批Main。无Git写，不提升整体UI/8.9/阶段8验收，不进入下一阶段；上一批未受影响证据沿用，此节为该反馈的最新修正记录。

### 29.28 Workbench最终UI收尾授权与五项核对（2026-10-18 11:53）

- 授权仅A～E及其直接回归，不重开整体设计，不统一换色/字号/高度，不改Runtime、Catalog、密码、Builder/Publisher/Deployer事务或平台门禁。当前分支/HEAD仍`fusion/8-resource`/`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区空；主Unity8296/2022.3.62f3c1/StandaloneWindows64，用户`UIAcceptance.unity`不dirty，非Play/编译/导入/测试，Runtime无/Bundle0、无Workbench窗口，Selection为`fe626f07e9a61a44c9547f9babfcd30f`，深色、DPI1.75。旧PID/句柄不作为后续事实。
- A：仍为二级“自动图集”内含自动规则和手动SpriteAtlas；Sources仍Size/Element、Grouping英文未说明，需本轮修正。只整理同一SerializedObject及现有数组，不改规则/生成；原保存对象、Undo、一规则多输出和危险删除保护保持。
- B：真实按钮先开GenericMenu，唯一添加动作读取Unity当前Selection.objects，另有禁用拖放提示；应明确“添加资源…”及真实菜单来源，不能伪装直接添加。资源/分析/版本/查询过滤输入均无空值提示，“Group”显示checkbox语义不明，需本轮局部修正；不扩展过滤能力。帮助弹层需在真实屏幕边界核实，未确认裁切前不重写tooltip。
- C：默认确认摘要和三类影响已解决，保留不推翻；展开区仍单个Message长文，需结构化为身份/目录文件/执行恢复三段。数据由同次捕获的只读对象提供，不解析长文，不建立执行权威；原完整Message继续完整复制，ForceRebuild不得遗漏，确认队列目标与服务调用不改。
- D：ReadCurrent结果仍写入底部完整digest成功提示，重复确实存在；改简短、依据已读/失败/过期/所选身份生成的状态，完整原文留输出和详情，不自动读取或标最新。
- E：图集宽窗固定48%且没有本页拖拽偏好，确需左约33%/右约67%的首次默认及用户可拖；只本页请求值，窗口缩小只钳制显示，不覆盖原请求，不接管资源页分隔偏好。
- 已解决无需重复改动：§29.27附加构建选项已在字段上方、说明独立，26point控件/原生表、身份与保存、输出拖拽、NoRecipe/Action及部分成功语义沿用。仅修五项，不继续搜寻无关审美建议。
- 旧图只读核对：指定六张PNG及对应JSON均存在。最终对照build/resources/atlas为深色1080×720；partial为深色620×440和1440×860；cache-visible-row为浅色620×440，均实测DPI1.75。图内仍旧“高级选项与执行说明”属于其拍摄时点，不能覆盖§29.27当前代码。本次补附件不重建临时报告或重跑发布。
- 新证据独立登记`%TEMP%/kilo/workbench-final-ui-20261018-1153/`，只追加新PNG/JSON，不覆盖上批目录；需要编辑时仅新建`Assets/YLFramework/Tests/Fixtures/WorkbenchFinalUI-20261018/`小夹具。旧Polish自有R1仅正式只读Preflight/确认/Esc，不Deploy、不签发、不生产发布。定向RED/GREEN、代表GUI及一次本批diff小补审后收口，原Plan/Resource.md增量维护，无Git写。
- 实施与定向回归：A/E子任务先交4测试，实际3个预期RED/1个既有输出选择PASS；B/C/D新增3测试先实际RED。Sources原生数组增删/替换、Undo/Redo、Inspector、同资产保存/Project未保存修改保护，中文Grouping/真实父目录、原生搜索输入/清空、确认三分区/原始完整复制/Esc、Index独立状态均已实现。图集首次约33/67及本view拖动请求保持，不跨Domain Reload持久化、不改资源页Group/Detail。首次13/13 Passed（42.533s），不与历史合集相加。
- 本批唯一独立小补审`ses_eb1000a21ffeN0QHN23tgI8WVN`按`source-before`逐文件比对10个生产文件、新测试与取证helper；未运行Unity/GUI。发现1项P2：无Summary但有Details的六类确认默认隐藏旧范围。新增真实绘制回归先RED（滚动期望>0，实际0），修复为无Summary时默认仍显示原Message。无其他独立P0/P1/P2，不再另派补审。
- 作者真实GUI另发现右编辑器仅占MinWidth420、留下空白；补断言先RED（按钮右缘779，期望>1052），改为明确使用剩余detailWidth。先前分隔条stretch问题也以明确宽度修复。`atlas-default-dark-1080`是该发现时的中间图，不作最终图。修复后同一限定组合14/14 Passed（37.513s），suite与全部叶结果保存`post-review-tests-green.json`；本批未重跑完整UX/Player/Atlas矩阵。首次testClass全名返回0不计验证，正式均以RQL精确选中实际测试。
- 五项最终处置：A已整理“图集 > 自动图集/手动图集”、共享Inspector的明确Sources数组和真实Grouping说明；来源非空移除经原生Ctrl+Z/Ctrl+Y/再撤销及本资产显式保存，17个自有夹具文件与编辑前哈希完全相同，源文件/meta无改动、生成图集0、无本项目发布目录。B已明确添加菜单/真实来源、“显示Group”和四页空搜索提示；帮助弹层经普通窗口真实点击及原生GetWindowRect确认在工作区内，无需重复修改既有对齐逻辑。C已实现只读三技术分区和原完整范围独立展开/完整复制，不改确认目标或执行服务；无Summary时仍默认保留原范围。D原生按钮显式读取旧Polish三版和Publisher Index，R1所选/R3当前关系正确，底部短状态，完整原Index结果仍含digest。E首次约33/67、真实GUI分隔拖动请求531.12，920宽显示钳制478，620窄模式不写请求，恢复1080显示531；本view会话内保持，不承诺跨主题/Domain Reload持久化，不影响资源页Group/Detail偏好。
- 代表GUI：深/浅1080、620最小窗、长身份/目录、原生来源菜单、右屏幕边缘帮助、真实普通停靠与SceneView sibling切换。帮助弹层物理矩形`[3411,1322,3796,1555]`位于主屏工作区`[0,0,3840,2076]`内；立即创建时的临时`0,0`位置不是最终边界。普通停靠原图为3838×2074像素，Workbench自身354×789.3point，仅证明现场窄停靠/真实切tab，不冒称620宽窗布局验收。仅实测DPI1.75，不外推其他DPI。OS拖动曾未形成请求，未算成功证据；宽度值以实际Unity GUI MouseDown/Drag/Up与定向测试为据。
- C02只读取证：正式ReadVersion/Preflight旧Polish R1（`a15d1570...927e3`），不附Deploy回调。第一次确认已取消但未复制，保留原失败事实；续接第二次真实展开身份/目录文件/执行恢复、原范围，原生“复制完整范围”1878/1878字符精确一致，Esc后Accepted=false，原workspace `AssetBundles/YLBI_23205b0d13632c6c`和目标均未创建，剪贴板在finally恢复。证据为`confirmation-native-copy-escape-02.json`及两张本轮技术区原图，不以首次未复制记录冒充通过。
- 最终保护：`protected-final-comparison.json`中Games475、Scenes2、StreamingAssets450、MainModule448、Packages2、ProjectSettings33、NuGet91、旧UX1 128、旧UX2～5 61、旧Polish27、旧UX发布38、旧Polish发布26项全部与本批基线相同。13个生产/测试/helper脚本.meta齐全，10个原有meta内容不变，GUID与AssetDatabase一致；各脚本最后修改早于保存的14项终态证据，续接只做取证/文档/收尾，沿用14项结果而不伪称复跑。`final-owned-fixture-check.json`确认自有文件未变/meta无缺失、未生成/发布。
- 环境恢复：已关闭所有本批自有窗口和确认框，普通浮窗容器也已不在原生窗口列表；恢复深色，清空本批OriginalDockPane槽，工作台update回调0、StageReached订阅0。主Unity8296非Play/编译/导入/测试，编译失败标志false；UIAcceptance.unity仍不dirty，Runtime未初始化/Bundle0。续接时用户选择已为空，保留该现场，不把更早fe626...选择或窗口位置回写。未清Console；取证脚本/查询的工具错误不冒称产品编译失败。HEAD仍`8abaf2d...`、暂存区空，无任何Git写。
- `Resource.md`仅增量同步入口、来源数组/Grouping、三技术分区/完整复制、Index短状态、搜索提示及图集会话宽度边界。`final-image-index.json`逐张记录A6张上批原最终图、B12张本轮真实图、C4张历史改前图的阶段/主题用途、PNG真实尺寸、SHA和可用配套JSON；无配套JSON的旧图只按原PNG尺寸标注，不猜元数据。新图集夹具不冒充旧基线同数据对照；中间错误布局图、主题切换后错页图、父窗口裁剪菜单图均排除。附件交付完成后只标本批技术完成/待用户最终验收，不改变8.9或阶段8整体状态，不进入8.10/阶段9。
- 2026-10-18 13:50交付：A6/B12/C4共22张均已用Read直接附图，并逐张标注来源阶段、真实尺寸、主题和用途；不是仅给本地Markdown链接。B8保留当时临时浮窗和工具日志，只证明停靠现场，不作洁净效果图或新增第二套工作台。末次`final-delivery-verification.json`再次确认13脚本SHA与收尾清单一致、12个保护根与本批基线一致，14个既有终态叶结果全部Passed。图片交付期间正式Workbench曾短时出现，保留不干预；后续`final-window-ownership.json`确认自有probe0、确认框0、正式窗口0、工作台update回调0，未把用户/并行窗口当作本批临时对象清理。仅文档和交付记录变化，不为此复跑已有效的技术回归。**Workbench最终UI收尾技术完成，待用户最终验收。** 不标整体8.9/阶段8 COMPLETE，不进入8.10、阶段9或其他专题；不提交、不暂存、不Push。

### 29.29 ResourceProject多目录与中文文案联合实施（2026-10-18）

- 当前授权：连续完成多资源目录、独立且按需登记的生成位置，以及工作台/直接Inspector/Help/Resource.md的自然中文整理。保留现有布局、配色、26point控件、导航与输出拖拽；不是纯视觉修改。不等待A完成后再次授权B，不进入8.10/阶段9，不标整体8.9或阶段8完成。禁止全部Git写操作。
- 实核起点：分支`fusion/8-resource`，HEAD仍`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区为空；既有UX与用户/并行的工具、NuGet、Packages、ProjectSettings、Core等修改全部保留。唯一主Unity PID8296（另两为AssetImportWorker），2022.3.62f3c1/StandaloneWindows64，非Play/编译/导入，compileFailed=false、TestRunner inactive、Runtime无/Bundle0。用户当前`UIAcceptance.unity`不dirty，Selection为`8461416694b970e4b9f7aff9cb7c2775`，焦点ProjectBrowser，深色、pixelsPerPoint1.75；不能恢复早期Main或旧选择快照。
- 源码事实：旧`AssetRootGuid`承担手工选择与自动依赖范围、默认地址基准、跨Project范围隔离、自动Atlas创建/归属锚点、Builder设置/Map指纹。Packages非代码资源仍依原规则拒绝，脚本和引擎内置依赖的既有例外保留。并非“根只存生成资产”或“根外可随意添加”。

| 原职责 | 本批新规则 | 兼容方式 | 必要验证 |
|---|---|---|---|
| 单根源范围 | `SourceDirectories`保存GUID及可编辑的持久`AddressPrefix`；同/跨Project来源目录重复或嵌套拒绝；登记不等于选择 | 配置版本0只读解释旧字符串根；版本1即使空列表也不回退。显式保存时才迁移，不在getter/绘制/预检写资产 | 跨目录同组、共享依赖、根外/失效/重叠、移除保留条目阻塞、Undo/重载 |
| 根相对默认地址 | 唯一所属目录的持久前缀加相对路径；排序无语义；冲突显示双方路径 | 旧根空前缀，原地址/显式地址/alias不变。跨目录移动可能改默认地址，需要长期稳定则显式填写 | 增根/重排/移动/同名冲突/子资产和Simulation一致 |
| 旧单根API | 同类型旧序列化字段保留读取身份，统一有效解释入口；兼容单根属性在多根时明确拒绝，不取首项 | 旧公开赋值在单根模式有效；多根使用新列表设置入口 | 旧YAML、旧JSON/调用方、首次显式保存、清空不复活 |
| 根内自动Atlas | 生成位置由明确新建且登记的配置目录决定；新三参入口显式给父目录，不接管已有目录 | 旧二参入口仍在唯一来源根下创建直接子目录；旧配置/图集保留原路径、GUID/meta/hash，不迁移 | 旧根内排除、旧图集Current/字节、独立输出、多来源规则、未知/其他Project保护 |
| 构建设置与后验 | 规范化排序的目录GUID+前缀及生成登记语义进入原设置/输入冻结；配置回调变化拒绝 | 工具代码已参与Workspace指纹，合法失效如实记录；不为缓存跳过完整检查；旧owned分区不迁移/删除 | provider/导入回调增删目录、重排、正式Recipe和native readback |
| 写权限 | 来源登记只有读取准入；生成/Workspace/Publisher仍按精确归属 | 不新增跨Project共享/依赖协议，不改Catalog v1、签名/加密/Runtime地址解释 | 源文件/meta与旧发布保护比对、旧v1严格读取 |

**文案对照与写作决定**

| 旧表达/技术概念 | 面向用户的新表达 |
|---|---|
| 专用资源根 | 资源目录；说明“可以添加多个文件夹，实际构建内容由Group中的资源条目决定” |
| 自动图集范围/根 | 生成目录；说明“保存自动生成的SpriteAtlas，不使用自动图集可暂不设置” |
| Together / PerAsset | 合并分包 / 逐资源分包，并说明共享依赖和Scene可影响最终Bundle |
| Atlas Together / PerFolder | 合并为一个图集 / 按实际文件夹生成，避免与Bundle分包混淆 |
| Failed/原英文异常 | 中文操作失败摘要、已知影响和下一步；原异常/路径/错误码保留在技术详情 |
| 查询Ready | 查询已完成，仅显示缓存文件及空间占用，不代表加载校验通过 |
| 发布成功+GenerationError | 资源已发布，但地址常量生成失败；已发布版本仍有效，处理具体原因后单独生成 |

- 本轮owned资产根`Assets/YLFramework/Tests/Fixtures/ResourceMultiDirectory-20261018/`，证据根`%TEMP%/kilo/resource-multidirectory-20261018-a/`，均创建前确认不存在并登记owner。`Legacy/LegacyProject.asset`及根内GeneratedAtlases使用**本轮改产品前的原单根实现**创建并真实Generate；`legacy-project-before.asset`、`legacy-atlas-before.asset`、`legacy-before.json`保存原序列化、地址/alias/BuildMap、GUID与字节基线。不是历史发布，也不冒充旧v1兼容证据。真实旧UX/Polish/Final夹具和旧发布只读，保护哈希留于`protected-before.json`。
- 首轮`ResourceMultiDirectoryTests`终态7项：旧单目录只读地址/alias基线1 Passed，新增功能6项按预期Failed（多根未生效、清空仍旧根、重复/嵌套未识别、冲突未含双方路径）。这是有效RED，不计通过；工具动态C#探测的编译错误不算Unity产品编译失败，未清Console。
- 当前状态：设计已记录，正式实施中。后续按本批真实结果增量填写编译、GUI、None构建/发布、Recipe、native消费/归还、一次独立Review与修复、未验证限制和终态，不以设计代替交付。

**实施与中间验证（不作最终验收）**

- 模型落盘：版本0通过同类型`FormerlySerializedAs`读取旧YAML字符串根，版本1目录列表明确优先，空列表不回退。`AssetRootGuid`为兼容单目录属性，多目录get/set明确拒绝；单目录写回/替换保留持久前缀。`SetSourceDirectories`支持Undo并允许无效草稿，统一解析指出原因，保存不静默删条目。默认地址统一由唯一所属目录的持久前缀加相对路径计算；旧根空前缀。目录移动/失效按GUID解析。普通跨Project来源仍互斥，不新增共享依赖协议。
- 自动图集采用明确`LayoutVersion`：0继续原`RootGuid`及根内直接子目录；1使用显式`GenerationParentGuid`，不重解释旧RootGuid。新三参只在明确父目录下新建登记此前不存在的子目录；旧两参只限单来源。现有owner、配置/输出GUID、meta/hash、未知内容、Obsolete删除、回滚检查保留。源外输出只有精确登记结果能经原Group/Map进入构建，不登记为普通来源，也不认领父目录。
- Builder将有效目录GUID/前缀规范化排序，定向规范Project YAML目录记录与v0/v1等效表示，其他原始字节/meta仍进输入身份；Unity `EditorJsonUtility`的`MonoBehaviour`包装层已实测适配。原provider/StrictMode/native后验/加密后验不削弱。来源追溯保存目录语义，变更后要求重算，纯重排可继续定位。
- 核心与图集首组终态`multi-core-atlas-green-02.json`：21/21 Passed，132.32s；冻结native/provider终态`multi-freeze-terminal-01.json`：11/11 Passed，201.11s。超时只读取同次collector终态，不重复启动活跃run。此前一次超时未留结果（Runner inactive/startTime0001/results0），明确未计验证。
- 唯一独立Reviewer在同一线程指出5项：原图集错绑后范围保护、旧setter清前缀、正式Operations第一次预检取key前未冻结、GUI深层遍历重复、Atlas派生Sprite文案错误。5项对应RED全部保留`review-red-01.json`；修复后`review-green-01.json`7/7 Passed（含三正式入口、纯重排允许、显示缓存与旧setter）。显示缓存使用弱键、规范目录意义+ProjectId、ProjectChanged/UndoRedo失效，显示地址再拆为纯计算，正式准入仍做完整路径/链接检查。Reviewer在同线程最后核对：5项源码闭合，无该修复直接残留。原始技术异常对象保留，默认摘要中文。
- 旧回归`old-core-terminal-01.json`：55叶、44Passed/11Failed，**该suite失败，不计通过**。选择器意外包含26个既有CoreDefaultResourceEditorTests，均Passed但不充作Resource专项数量。11个Map失败根因是旧测试夹具用`JsonUtility`写已演进为兼容属性的字段名，JsonUtility不通过属性赋值；只把夹具准备改为保留的`AssetRootGuid`公开单目录入口，原地址/依赖/跨Project/拒绝断言全部保留。随后旧Map11+显示缓存1同次12/12 Passed；真实旧YAML/旧图集仍按改前原始基线另行只读验证，不拿fixture修正冒充YAML兼容。
- 中文覆盖已实施到五任务页/子页、目录与生成位置、Group/资源条目/配方/Atlas字段、Project/Atlas/Recipe/Image Inspector、确认框与原异常技术区、缓存只读查询、签发窗口、直接Help和Resource.md；不改API/枚举名，不建立全局本地化平台。图集待生成提示明确正式Build会先准备，不要求先手工准备才能构建。
- 真实GUI区分：Modern最初6个来源由自有helper配置；第7个`ExtraSource`通过真实鼠标“添加资源目录”及IMGUI DragUpdated/DragPerform拖入完成（合成事件单独记，不冒充OS拖拽）。真实鼠标切换Group和添加来源菜单、真实键盘Down/Enter添加代表Scene，真实Ctrl+S保存；真实鼠标计算分包预估与请求/确认构建。未保存与取消确认框均有现场，初次Enter未选中菜单项未计成功；没有调用服务假装鼠标操作。
- 首次正式GUI None构建/本地发布：7来源、3Group，未配置生成目录，无配方、无密钥、未生成常量；`modern-build-none-first-publish.json`显示UnityBuildInvoked=true、4主资产、4Bundle（含独立Scene与唯一共享Material/Texture包），38.57s，发布摘要`e15ded1fae85598cd3589b29aec147668fa514f1f84d148e31ae367ff105e81e`。一次取证写入回执重入提示文件已存在，读取同一已写完整report确认成功，未重复构建也未覆盖记录。
- 首次原Runtime消费：`runtime-20261018T163545998-f0ac08b8bb274580aac44350588007a3/terminal.json`为Completed/Passed，精确消费上述Bundle发布。Prefab A/B共享同一Material/Texture，像素/16x8尺寸/UV、文本/alias、未选中文本拒绝、真实Scene标记位置与卸载全部通过。自有Startup正常Stop，native load/unload4/4，lease/record/Bundle归零，原UIAcceptance场景setup恢复且非dirty；未使用全局UnloadAll或关闭他人会话。
- 当前仍在验证：旧Atlas/Builder回归同一次run、Atlas+Recipe实际构建发布/native、旧YAML/旧发布只读核对、代表性主题/最小窗口/停靠/错误与查询文案、终态保护比对。此处不声称本轮完成。

**追加验证及工具恢复（2026-10-18，仍不作最终验收）**

- 旧Atlas/Builder同次完整终态已落盘`old-atlas-builder-terminal-01.json`：48/48 Passed，16分56.558s，涵盖B05/B06与Workspace/Manifest/native回读。先前“仍在native回归”的判断随后由日志修正：suite结束后，代理的结果读取脚本错误地把JToken直接返回给MCP，工具反射沿Parent/Root循环，造成大量工具警告和主线程占用；不是48项失败或产品异常。原失败日志未清理、Packages/插件/权限配置未修改。今后本批取证只返回普通路径/JSON字符串，不直接返回JToken对象。
- 主线程无法可靠重读dirty时，没有自行结束Unity。经用户明确回答“允许重启主Unity”（含可能丢失未保存编辑器内存改动的风险告知）后，重新核对主进程8296路径，仅结束该已授权主Unity；确认所有Unity.exe退出后才打开同一工程。新唯一主Unity PID32204，新日志单独写`unity-restarted-editor.log`，旧Editor.log保留。重开时编译/导入/Play均false、Runtime无/Bundle0，默认打开Main且不dirty；未运行原游戏。在确认无新dirty/额外场景后，重新打开磁盘最新UIAcceptance，不恢复旧场景内存快照。主Unity由persistent进程`bgp_14ff6b926001D1ir4nzS0NJJ4N`保持，交付时不停止用户编辑器。
- `legacy-and-old-v1-read.json`：本轮改代码前原实现创建的旧YAML/根内Atlas共14个文件SHA全部相同、版本仍0且不dirty，原地址/alias/BuildMap相同，Atlas Current/GUID `596ce1eca1693db4785ffbac0c2a4f2d`未变。另按原Publisher和ResourceLocalSnapshot严格读取**真实旧Polish发布**`5ee1d92e…589e04`，Schema1、7资产/2Bundle，不重建、不写回旧发布。
- 补充真实GUID移动、失效GUID/Assets本身/文件不能登记为目录、不同来源同名Icons文件夹行为。后者RED发现Atlas地址冲突只显示地址，未给双方来源；窄改为保留冲突拒绝并显示两组真实源路径，不改InputHash/身份/输出。三项后续3/3 Passed，新增专项行为共42个独立叶（复跑不重复累计）。
- 首次GUI回归终态`tests-ui-first.json`：37叶29Passed/8Failed，**suite失败不计通过**。5项为原逐词断言不适用于中文摘要/中文恢复入口，1项断言提前失败又被未显示窗口Close清理异常遮蔽，另2项被代理在活跃run中请求终态记录所产生的工具异常污染。保留该失败回执；将终态记录活跃状态改为无副作用中文返回，不清日志、不忽略异常。仅按新中文契约更新旧文案断言，继续断言原始failure对象/Recipe技术详情、native回读、取消零写、索引未知/过期/运行版本区别、预算与原生/CNG不相加。未显示测试窗口按真实parent状态清理，不掩盖原断言。开始使用技术区仍明确无窗口样例不是通用CI工具。

**本批最终技术交付**

- `tests-ui-after-copyfix.json`：同一组37/37 Passed，2分38.536s，无跳过；没有加入LogAssert忽略或清Console。实际桌面鼠键、进程内IMGUI注入、直接服务/单测分别按上文记录，不相互冒充。
- 最终专项统一run：`tests-final-multi42.json`，**42/42 Passed、0Failed/0Skipped，6分1.570s**，是完整suite终态，不是发现树/重复累加。包括多目录/默认地址/显式alias/移动/失效/非法/重叠/清空兼容、跨目录依赖与Simulation、保存Undo重载、源外及旧根内Atlas/未知输出/同名目录/原归属保护、三正式入口首次provider变更与纯重排、导入回调和native后验。旧Map11+显示缓存1已有12/12；旧Editor/Simulation各8通过，旧Atlas/Builder48/48证据沿用其受影响范围。未重跑G0、176组Player、完整E04矩阵、其他DPI/平台、CDN或生产key；None的两次native不冒充完整加密链复验。
- 第二次真实Workbench入口为同一`ModernRecipe.asset`，ResourceProject仍7来源；正式Builder自动生成并准备两个分散Sprite来源的`multi-root.spriteatlasv2`，精确输出位于独立`Modern/Generated/OwnedAtlases/`，经main Group/Map/Catalog发布。`modern-build-recipe-atlas-partial.json`为UnityBuildInvoked=true、5主资产/4Bundle、25.91s，发布摘要`6e56775c5560252ce71fe27ec48c8b22ba3bd46c1a9ba274e5854a385ec91918`。
- 该Recipe有意开启地址常量生成以走查部分成功。**实际失败原因**先于预设未知输出目录检查：原`PublishedCatalogs`发现既有Polish发布App不同；没有为了成功修改/筛掉旧发布或新增跨Project协议。Build/PublicationError均null、GenerationError保留原异常；GUI明确“资源已发布，地址常量生成失败”“发布版本仍有效，无需重发或回滚”。不能把这次结果宣称为未知目录拒绝的实际触发证据；未知内容保护另由直接回归证明。负例`BlockedReferences/do-not-overwrite.txt`保留未接管，体验Recipe已明确保存为GenerateReferences=false，避免默认体验重复触发全项目App不一致。
- `runtime-20261018T173242214-38d3dd9246664464a4021b6506eae0dc/terminal.json`：第二次Completed/Passed，精确读取上述Bundle发布，包含两张Atlas Sprite（8x8与12x8、真实32x32纹理、正确像素），借用Atlas Lease及GetSprite克隆销毁按原责任处理；Prefab共享材质/纹理、文本/alias、Scene同样通过。两次均无AssetDatabase/Simulation回退，自有Startup Stop、native load/unload4/4、lease/record/Bundle0，原场景setup恢复。
- GUI最终走查：深色1080、浅色620×440（真实1109×834像素）、原DPI1.75；目录表可滚动，完整路径可选/复制，GUID可复制。普通SceneView dock约1092点宽真实停靠，实际Project Inspector显示同一7目录字段与保存入口；开始使用/技术Help及原Resource.md对应。未为此重跑全排列。地址冲突由真实鼠标计算、展开技术区核对双来源；原始错误/调用信息保留，真实Ctrl+Z恢复并只保存自有Project。移除纹理来源目录后明确指出受影响Atlas规则，保留四条手工选择和源文件；真实Ctrl+Z恢复7目录。所有临时草稿最终已恢复并保存。
- 磁盘只读查询使用本批明确不存在的缓存根，真实按钮执行后`gui-disk-query-readonly.json`显示Missing、IsSizeComplete=false、ObservedBytes0、候选字节null，根仍不存在、Runtime未启动、Bundle0；不把缺失当真实完整0、不声称Ready或加载校验。37项GUI回归另覆盖Ready显示、取消/停止及Unknown区别；本次桌面查询未建立真实磁盘缓存内容，不把它冒充缓存加载场景。
- 最终6张附图由`final-image-index.json`登记用途、原始像素及SHA：七目录深色1080、目录浅色620、Current图集两来源、首次None发布、Recipe真实部分成功、普通停靠Help与真实Inspector。原“浏览器遮挡”图、错页图和在途故障图明确排除，不覆盖失败文件、不把文件名当画面事实。停靠现场保留Console历史工具错误，不称“无错误效果图”。
- `final-protection-and-source-manifest.json`：17个保护范围/单文件与本批初始哈希一致，**0差异**，包括Games、Scenes、StreamingAssets、MainModule、Packages、ProjectSettings、NuGet、UX1/UX2～5/Polish/Final夹具及旧发布、AGENTS、忽略/工具配置和既有Core Project。68个自有夹具文件及目录meta无缺失。56个Resource产品/测试/文档现场快照，加`final-docs-help-manifest.json`明确维护最终Resource.md与两Help源码；这是交付现场范围，不将上轮未提交UX改动全部归为本批。
- 最终`final-unity-state.json`及`final-ui-selection-restored.json`：唯一主Unity32204，2022.3.62f3c1/StandaloneWindows64，非编译/导入/Play/测试，compileFailed=false，Runtime无、Bundle0、Workbench0、确认框0、自有update回调0；Project/Atlas/Recipe及UIAcceptance均不dirty，深色/DPI1.75恢复。仅移除自己创建的浮窗、停靠页和已确认无parent的空测试确认对象，未覆盖整份布局。最终重新解析并恢复原Selection `UIAcceptanceEntry.cs`；源资产与当前场景内存不按旧快照回滚。授权重启的主Editor保持运行，不随本会话关闭。
- Git仍`fusion/8-resource`、HEAD`8abaf2d718ecf0f5d5c380dc2e2a63d73fa76d91`，暂存区空；本轮未暂存、Commit、Push、Merge、Tag、切分支、Reset、Clean、Stash或Rebase。全工作区`diff --check`仅报告初始已改的Core Project两行空序列化值尾空白，保护哈希证明未由本批改动，不顺手修。相关源码/文档差异检查及新增脚本/meta检查另行通过。
- **ResourceProject多目录支持与工作台中文文案整理技术完成，待用户功能、兼容性与使用体验验收。** 到此停止；不标整体8.9或阶段8 COMPLETE，不进入8.10/阶段9，不提交，不继续一般性UI美化。

### 29.30 按未发布状态移除Editor旧配置兼容（2026-10-18）

- 用户先反馈资源目录说明缺乏条理，已将共享说明分为“选择范围”“地址前缀”和旧配置提示；随后明确“框架没有发布过，不需要旧配置兼容”。本条取代29.29中有关长期旧Editor配置/API兼容的要求，不删除或倒改上批历史结果；Runtime/Catalog/发布协议不在本次删除范围。
- 现场核对9个ResourceProject，其中当前窗口为Core夹具且不dirty，4个关联Atlas配置。用户通过问题明确选择“转换全部现有配置（推荐）”，授权只转换这些Editor配置，保留源文件、GUID/meta、图集位置、选择、地址/alias及既有发布。一次性备份/授权/字段准备记录位于`%TEMP%/kilo/resource-current-model-20261018-1837/`，不是产品中的迁移服务。
- `conversion-authority-before.json`及13个原资产/meta备份记录转换前字节。先用原正式Unity API把9个Project的有效目录写入SourceDirectories、4个Atlas写入真实GenerationParentGuid，再删除兼容字段后由Unity正常保存。`conversion-final-assets.json`逐项确认目录、Group、选择、地址/alias语义不变，13个GUID/meta不变，文件不再包含兼容字段。未手改YAML/GUID，不移动或生成源资源/Atlas，不改发布。
- 唯一模型：ResourceProject只保留SourceDirectories，不再有AssetRootGuid属性、FormerlySerializedAs旧输入、legacyAssetRootGuid、DirectoryConfigurationVersion或DirectoryDefinitions；保存不做迁移。GUI无旧模式、ConvertLegacy或兼容提示。BuildMap移除单根AssetRoot和旧ValidateProjects包装。Atlas只用GenerationParentGuid及原精确owner/GUID/meta/hash/输出登记；删除RootGuid/LayoutVersion/legacyLayout及隐式1/2参入口，仅保留显式3参。
- Builder仍对当前目录集合排序规范化，删除v0/v1等价解释；未知记录、注释、不完整布局和其他原始磁盘字节不得屏蔽。保留空/null来源拒绝、跨Project范围、精确生成输出准入、未知内容/孤立meta拒绝、回滚/Obsolete保护、provider与导入冻结、StrictMode及native后验。
- 生成父目录的Assets根事实已由当前Unity读取：真实内置GUID`00000000000000001000000000000000`可双向解析。先前“根无GUID”的推断已纠正；当前显式父目录可以是Assets，存真实GUID，不用空值猜根、不加sentinel。来源目录仍禁止整个Assets。规则Sources与生成范围相交仍拒绝，不借清理兼容扩大收集范围。
- 测试与直接Editor样例改用唯一列表/3参入口。旧兼容测试替换为当前模型只读、保存重载、排序/前缀/alias稳定及真实磁盘字节敏感性，原范围、源文件/meta、未知输出、回调冻结负例保留；补null/empty不回退、缺生成父GUID拒绝及移除依赖专用目录仍阻塞。反射契约RED为1Failed（AssetRootGuid仍暴露），非编译错误；删除后首组当前模型/Atlas/Integration **30/30 Passed**，完整终态`core-first-terminal.json`。
- 当前正在复验冻结/native、现有入口、13配置转换及GUI终态；本条不沿用“旧配置兼容通过”作为新模型证明，也不为纯文案重跑完整Player/加密矩阵。不Git写，不进入后续阶段。

- 后续完整终态`freeze-map-editor-terminal.json`：36/36 Passed，0Failed/0Skipped，6分28.084s；覆盖当前冻结/provider/导入/native后验以及原Map和Editor入口。与首组30/30分开记录，不重复累加旧兼容结果。真实Unity编译无错误，反射契约证明旧模型成员和旧创建重载不存在。
- 额外以当前模型执行自有ModernRecipe的真实None native构建/发布，`current-recipe-native-build.json`为BuildSucceeded=true、Published=true、Native=true，构建/发布/生成错误均null；新摘要`5b60a60a00f6a7b494fbec9b77e3670c741a3f1bbc88c6f61b7c80a327a32b03`。同一次原Runtime消费终态位于上批证据根的新`runtime-20261018T191602555-c4617af6a8d44d5c8b1736449f2a7832/terminal.json`：Completed/Passed，Prefab共享引用/纹理像素尺寸、文本/alias、Scene、两个Atlas Sprite及克隆销毁通过；原Scene setup恢复，自有Startup正常停止，native load/unload4/4，record/lease/Bundle0。工具超时后仅读取同一次终态，没有重复启动活跃构建/加载。
- 同一Reviewer线程定向核对本次删除：无直接缺陷发现，目录/Atlas模型已收敛，未知内容与跨Project保护、指纹原字节回退、冻结和后验未见弱化。其测试证据沿用主代理实际结果，不冒称Reviewer执行过测试。
- `protected-and-atlas-byte-comparison-02.json`确认除明确授权的配置资产内容外，原保护范围0额外差异，源资源/既有发布及meta未变。4个Atlas剔除本次删除/新增的父目录模型字段后，其Rules、Outputs及其他YAML字节完全相同，4个meta相同。首份比较因子串AssetRootGuid误将Project识别为Atlas产生假阳性，保留原回执，并由锚定字段名的02回执明确纠正，不删历史失败。
- 最终`final-state.json`：唯一Unity32204，非Play/编译/导入，compileFailed=false，Runtime无/Bundle0；9个Project及4个Atlas全部不dirty，原UIAcceptance场景不dirty。用户现有Workbench（ID -48136、Core配置）保留未关闭，未创建替代窗口、未恢复旧选择/布局快照。全部9个Project使用同一列表模型；Core、UX及原单来源样例仍保留一条来源，Modern保留7条。
- Resource.md及当前UI已删除旧配置兼容、版本分支、首次保存迁移、旧API承诺；历史证据/文件夹名Legacy只保留来源说明，不再表示存在另一套产品模型。现有配置的一次性整理已经完成，产品不保留迁移器。Git无写，未提交/暂存/推送，也未进入8.10或阶段9。

### 29.31 8.9最终验收、第二次提交与Git收口（2026-10-18）

- **验收范围**：用户确认8.9本轮及剩余专项验收通过，覆盖ResourceProject多目录、中文文案、Workbench UX1～UX5与视觉/交互修正、B02 Build Recipe、F03成本基线及1175原子替换定向补验、E04 G1～G4已交付范围；C02、E05、B06、C05及必要的C04/F04继续沿用此前用户验收结论。以上仅对应各自真实代码、环境与证据范围。
- **最终模型**：ResourceProject仅`SourceDirectories`（GUID+持久地址前缀），不再有旧单根字段、兼容属性、版本判断、保存迁移或旧API壳。默认地址为唯一所属目录的持久前缀+相对路径；目录排序无语义，同名冲突显示双方来源并拒绝。BuildMap、AddSelections、Workbench/Inspector、Simulation、Builder/Recipe冻结与后验、Atlas归属均使用同一解析。图集只保留显式三参创建和`GenerationParentGuid`，父目录使用真实GUID，可明确选Assets或其子目录；不从来源首项推断，不接管已有目录/未知内容。
- **一次性配置转换**：用户明确选择转换全部现有配置。9个ResourceProject与4个Atlas配置已按原Unity API保存为当前模型；目录、Group、选择、显式地址、alias、ProjectId、配置/输出GUID、meta、图集位置及输出记录保持。逐项回执为`conversion-authority-before.json`与`conversion-final-assets.json`，原文件备份位于`%TEMP%/kilo/resource-current-model-20261018-1837/`。本轮不从外部再次批量保存Project。
- **兼容边界**：当前Editor配置正式使用多目录及显式生成位置模型；以后从外部导入尚未转换的旧单根Project/Atlas，不承诺自动读取或迁移。这是Editor配置格式边界，不等于旧Catalog v1/Runtime发布不兼容；Catalog Schema v1、地址解释、签名与发布协议未改，旧正式发布继续按原冻结协议读取。
- **最终验证**：反射契约RED有效（旧成员仍暴露），删除后当前模型/Atlas/Integration **30/30 Passed**（`core-first-terminal.json`）；冻结/native/原Map/Editor入口 **36/36 Passed**（`freeze-map-editor-terminal.json`）。同一Reviewer线程定向核对无直接缺陷，确认删除未弱化来源、生成归属、未知内容、冻结或后验。另以当前模型执行ModernRecipe真实None构建/发布，`current-recipe-native-build.json`四阶段成功、无错误，摘要`5b60a60a…32b03`；原Runtime消费`terminal.json`Completed/Passed，Prefab共享材质/纹理、文本/alias、Scene、两张Atlas Sprite及克隆销毁通过，native load/unload4/4，归零并恢复原场景。
- **提交策略**：第一次功能基线`8abaf2d`不 Amend。第二次提交只含已验收的Workbench UX、多目录最终模型、旧兼容清理、直接配置差异、必要测试/`.meta`、正式样例/维护夹具、Plan与Resource.md。排除原游戏/Scene/Canvas、StreamingAssets、Packages/ProjectSettings、AGENTS/忽略/工具配置、NuGet/并行Agent Manager/UGUI改动、其他worktree、Library/Temp/构建制品、临时截图/原始证据JSON、秘密与凭据。
- **收口条件**：第二次Commit成功、普通Push到已核实上游`origin/fusion/8-resource`、远端SHA与本地完全一致、用户/并行改动未覆盖，且无本批遗留编译失败或在途自有资源责任。此提交写入后，上述条件满足即在最终报告声明8.9 Git收口完成并可进入8.10；不在本篇预先声称远端已同步，也不增加仅为写SHA的第三次提交。
- **保留限制**：E04 Scene在native接手后的延迟Stream读取失败崩溃风险、历史no-go结论、g36回滚失败确切根因未知、当前平台/密钥/内存预算及BCrypt系统释放未测边界、F03环境与样本外推限制、其他DPI/平台未验、地址常量受全发布集合/App兼容约束、外部未转换旧Editor配置不自动兼容，均继续有效。Windows Player首次启动、StreamingAssets打包读取、持久缓存重启、安装/激活/LKG/回滚流程及远端HTTPS硬门禁留待8.10，不用本批Editor/native证据冒充Player验收。
- **8.10交接**：以本批最终远端Commit为基线；正式配置与可执行样例入口为`Assets/YLFramework/Tests/Fixtures/ResourceMultiDirectory-20261018/Modern/ModernProject.asset`、`ModernRecipe.asset`及`Samples/ResourceStartup`；先做Windows64 Mono Player首次启动与真实StreamingAssets内置读取，再做ArtifactStore持久目录/进程退出/重启恢复和正式安装、准备、激活、LKG/回滚与异常恢复，最后专项验证远端HTTPS硬门禁并决定是否开放。阶段8整体仍未完成，阶段9不启动。
- **8.9收口事实补记（8.10开工实核）**：第二次提交为`9a88e6a4ec04209f60453b1e166de28b3034430d`（`feat(resource): finalize workbench and multi-directory authoring`），存在于当前本地与实时远端`origin/fusion/8-resource`共同历史；当前远端和HEAD均为其直接后继`5074595212b08bba45d95d48768a895258ea6f93`（`chore(mcp): upgrade Unity MCP integration`），`git diff 9a88e6a HEAD -- Assets/YLFramework`为空。故原待执行Commit/Push状态已被事实替代，8.9 Git收口完成；不为此补做Git写入。

## 30. 8.10 正式Player全流程验收（2026-10-18）

### 30.1 授权、已有证据与本轮缺口

用户正式授权连续完成必要适配、真实Player测试、问题修复、一次独立Review及文档。限定Unity `2022.3.62f3c1`、Windows64 Mono；独立验证载体须使用核对身份的当前正式源码，不另写加载器，不对主工程开第二实例。允许自有夹具、精确C02部署、自有HTTPS/非生产凭据和本轮自有Player进程的受控终止。loopback pin不作为生产信任，不改系统信任。只有远端正向、拒绝与恢复门禁全部通过，才明确开放受测Windows64 Mono范围，其他平台继续拒绝。阶段9干净工程导入不在本轮，本轮禁止暂存、Commit与Push。

| 范围 | 已有可沿用证据 | 本轮必须补齐 |
|---|---|---|
| Builder/Publisher/C02 | §8、§28.8、§29.31正式构建、部署外壳、payload严格校验及Editor/native消费 | 实际Player BuildReport、包内payload~/SHA、无缓存空Store离线首启及Core/Prefab/共享依赖/Atlas/Audio/Scene |
| ArtifactStore | §8.2、8.7及E05的整组事务、故障注入与恢复 | 同一自有Store的多个独立Player进程，安装/Prepare/Activate/active/LKG/回滚/序号，事务检查点受控终止再恢复与无混代 |
| E04 | §28.10.19～20既有provider、认证、预算及正式Player证据 | 代表性加密内置/更新/重启/缺错key拒绝；不重演已接受的延迟坏Stream Scene崩溃，不豁免正常输入新故障 |
| Windows远端 | §8.3、§28.5的WindowsEditor HTTPS链；Windows Player源码硬拒绝 | 先记录原拒绝，必要Mono后端适配；TLS/签名/序号/强ETag/If-Range/真实partial跨进程续传/失败保旧全部门禁 |
| 交付 | §12.2既有程序集边界、许可和导出规则 | 最新框架导出及源码/程序集依赖/Json.NET、BC、Tink等适用许可与排除项；实际运行用法及最终工程状态 |

开工实核：`git status --short --branch`为`fusion/8-resource...origin/fusion/8-resource`；既有改动`.gitignore`、`.kilo/kilo.jsonc`、`AGENTS.md`、`ProjectSettings/Packages/com.unity.probuilder/Settings.json`及未跟踪UI Plan、`WorkbenchUX1-20261017/AlternateContent.meta`均保留，不认领。MCP RQL已实时响应；`isPlaying=false/isPaused=false/isCompiling=false`；唯一打开`Assets/YLFramework/Tests/Core/Acceptance/UIAcceptance.unity`、`isDirty=false`。ProjectVersion为规定版本；运行工具Shell实核为Windows PowerShell 5.1，后续命令按实际Shell边界执行。未发现根`.editorconfig`。后续每项证据须区分异常注入、正常退出和强停，不承诺断电原子性；长调用先回收同次结果，不重复启动。

### 30.2 正式构建、离线首启与本地跨进程证据

- 载体为本轮新建`Temp/r810d`，原主Unity进程32204未关闭或重开；实际载体Editor/Player均为`2022.3.62f3c1`、`StandaloneWindows64`、Mono2x。正式六导出根1390个文件及meta逐项SHA冻结，载体不复制主Library或原项目设置，仅使用正常解析的UGUI 1.0.0、Json.NET 3.2.1与内建modules；这不是阶段9的干净工程导入验收。
- 通过当前正式ResourceProject多目录模型与原Builder/Publisher生成`r810-a`、`r810-b`（None）及`r810-e`（原E04加密）各两版，原C02 Preflight/Deploy部署六个明确版本。只创建载体自己的StreamingAssets父目录和owned根，主工程StreamingAssets不写入。签名、发布、部署身份及逐文件摘要保留在`%TEMP%/kilo/r810-1018d/publications.json`；BuildReport保留在`Temp/r810d/Builds/B1`与`B2`，实际包内`payload~`及全部StreamingAssets文件集合/length/SHA与C02来源完全一致。
- `offline-none-02`和`offline-enc`均为真实独立Player、无服务、Store不存在的离线首启；只经`OpenBuiltIn`、`ResourceStartup`和原Provider消费，未读取开发机发布目录或使用AssetDatabase/Simulation。覆盖Core文本/Prefab实例、两个Prefab共享Material/Texture、Atlas Sprite与克隆销毁、Audio及其领域Provider、真实Additive Scene加载/卸载。首次消费不创建Store；有效正常退出检查authority/Core绑定/native Bundle、内容输入/作业/预算/CNG责任归零。
- `Test-ResourcePlayerAcceptanceEvidence.ps1`首次本地回执`verification-local.json`实际核对**31个正常退出进程、4个受控强停进程**，其中5个明确拒绝进程（原远端Unsupported及加密首启/更新后缺错key）；计数是进程，不是测试叶/断言。同一None Store的安装/Prepare不激活、跨进程Prepared恢复、Busy保旧、幂等激活、active重启、整组LKG/回滚、取消均通过；加密同Store重复上述代表性更新/重启，缺错key拒绝后有效key仍可读取原active。
- 异常注入单列：`BeforeCommitDecision`抛受控IOException后，正式实现恢复旧整组且内存/磁盘一致，下一进程验证通过。强停单列：自有Player分别停在`JournalVerified`、`ActiveWritten:r810-a`、`BeforeCommitDecision`、`CommitDecided`，检查PID与确切exe后仅Kill该子进程树；前3次下一进程恢复V1，提交决策后恢复V2。中间镜像写入点确实捕获a新/b旧的磁盘状态与Verified journal，重启后整组恢复，不宣称写入瞬间磁盘从不混镜像或断电保证。损坏一个owned active槽后，正式启动选择整个LKG并修复镜像，未混代或猜最新。逐进程result/Player/stdout/stderr及恢复前后控制快照保留于同根`Runs/`。
- 加密写入观察限定本轮run/TEMP/Store，FileSystemWatcher零溢出错误、未观察到`UnityFS`明文文件；ProbeMisses及无法进程归因的限制逐次记录，不把有限事件观察冒充所有瞬时写入的证明。沿用原provider合同、CNG/BC实现、64KiB分段/2槽/32KiB读取与128MiB显式预算，无新密钥服务、内容key文件、整包解密落盘或后端替换；未重演延迟坏Stream Scene崩溃。
- 失败原件保留：最初长路径载体触及Mono路径限制，后改用新建短路径载体；首次新载体未显式创建StreamingAssets父目录、部署刷新后复用失效Project SO引用的夹具错误均修正。`offline-none-01`为验证调度器提前Dispose CustomYieldInstruction，`none-corrupt-active-lkg`为证据采集器先解析故意损坏JSON，均未进入相应正式行为，修正调度/原始字节采集后真实重验通过，未修改产品逻辑或套用Scene豁免。原日志在`r810-1018`/`b`/`c`/`d`保留，不以失败/发现树计为通过。
- 1110个保护文件SHA与开工一致。远端原硬拒绝已在`original-gate`中真实观察；随后正向`remote-positive-red`实际因Unsupported失败，作为候选适配的RED；此本地检查点时尚未开放，最终开放决定与证据见30.3。

### 30.3 Windows Player远端开放决定与门禁

本节为Mono主批交付时的原始开放记录；随后独立授权的Windows64 IL2CPP增补与当前最终矩阵见30.7及28.5，不据历史检查点恢复已移除门禁或改写原始证据。

- **决定：仅开放Unity `2022.3.62f3c1`、Windows64 Mono Player。** 原UnityWebRequest、RSA签名、SHA、Range/Parts、ArtifactStore及原E04 provider实际可以在该后端工作，无需重写传输、密码或加载器。唯一产品行为改动在`ResourceRemoteSource.RequireSupportedRuntime`：保留WindowsEditor，Player同时检查Win目标、`ENABLE_MONO`、非Editor、64位及确切Unity版本；其他组合仍Unsupported。
- 测试pin仅在Editor或同时具备`UNITY_STANDALONE_WIN && ENABLE_MONO && DEVELOPMENT_BUILD && YL_RESOURCE_ACCEPTANCE_TESTS`的隔离构建编译。C系列候选用精确loopback证书pin连接本轮自有HTTPS服务；D系列普通构建不带该符号，真实Player反射确认pin方法不存在，并实际拒绝同一自签证书。**正向HTTPS为自有loopback pin证据，普通默认信任链只有拒绝实测，没有生产CA信任链正向联网实测**；不把pin作为生产信任策略，不修改系统信任。

| 门禁 | 实际Player结果与证据（相对`%TEMP%/kilo/r810-1018d`） |
|---|---|
| 正向签名HTTPS、两项目安装/激活/幂等 | `Runs/remote-positive-green-02`、`net-active-restart`、`net4-same-confirmed-release`；跨进程V2闭包与磁盘一致 |
| TLS/错pin/签名/Catalog/HTTP拒绝 | `net2-untrusted-tls`、`net2-wrong-pin`、`net3-signature/catalog-hash/redirect/missing/stall`；核对实际原因及请求日志，旧active仍可消费；302未跳转，404未无界重试，503有限重试后成功在`net4-transient-retry` |
| 合法非identity编码拒绝 | `review-green-encoding`：服务器实际gzip压缩、正确传输长度，Player内层明确`Encoded release responses are not accepted.`，仅发生release.json一次GET；原`net3-encoding`只证明坏gzip被curl拒绝，不作该协议门禁证据 |
| 序号与回滚 | `net3-rollback/rollback-restart`、`net4-old-sequence/conflicting-sequence`、`net5-failed-confirmed-floor`；回滚保持已确认序号2，低序及同序异digest拒绝，相同已确认发布可幂等恢复 |
| 损坏、失败保旧与恢复 | `net4-corrupt-body/corrupt-restart`、`net5-after-failed-valid`；真实损坏下载拒绝、旧Builtin V1可读，后续合法V2成功。只读Check及首次失败下载不确认序号，未改成observed冻结策略 |
| 六种真实partial跨进程链 | `Runs/range-{range206,kill,ignore-range,changed-etag,bad-range,weak}-*`，每组partial/complete/active-restart；`resume-*-before.json`保留实际**1,048,576B**未完成文件SHA、目标length/SHA和ETag，`server-requests.jsonl`核对精确Range offset/If-Range及206/200。200/变ETag重头取，坏Content-Range拒绝后从零取，弱ETag不发Range；强停partial亦实际恢复 |
| Prepare跨进程及远端journal | `recover-prepare-only/prepared-restart/prepared-activate/prepared-active-restart`；另在提交决策前/后各强停自有进程，下一进程分别恢复V1且无未提交floor、V2且两项目floor=2；成功激活后低序仍拒绝 |
| 原E04密文远端衔接 | `encnet2-encrypted-online/restart/missing/wrong/valid`；同一失败原Store恢复后完成密文下载/认证/整组激活/重启，缺错key拒绝，有效key仍读V2；原认证、预算、内容所有权及CNG计数归还 |

公开请求错误沿用原包装合同：上述传输/签名/摘要/序号通常为`UpdateFailed`，内层给出具体原因；初版夹具错误期望其他Kind的失败已保留并修正具体断言，未修改产品错误分类来迎合测试。未带验收符号的D1 Development构建真实离线None/加密与默认TLS拒绝也通过；D3补采正常Stop后完整Diagnostics，live图/Lease/Scene/Producer/Waiter/Running/Queued均空，native累计装卸14/14（两None项目）及7/7（加密项目）平衡，输入、作业、预算及进程CNG实值均归零。另真实构建`R1`（BuildReport `Options=None`、`Development=false`、Mono2x、Succeeded/0 errors），`release-offline/encrypted/persistent`三进程完成同样首启、持久V2与完整退出诊断，pin方法均不存在；不把Development与非Development混为一谈，远端正向仍按上述测试pin证据边界说明。

### 30.4 唯一独立Review、修复与最终计数

- 独立只读Review会话`ses_eaf0aa5cfffeUGAbdODFpzHHYP`完成一次；未发现Runtime范围放行或pin编译边界的实质缺陷，未重开密码、UI或31项审计。提出的两项P2证据问题及同步发现的终态统计错误均在同一修复批次闭合，不追加第二次Review。
- **终态计数假阴性**：D2误把`NativeBundleLoadCount/UnloadCount`累计值当在途值要求各为0；保留`terminal-*`三份完整失败与真实Disposed/live空状态。改为累计装卸平衡及真实live/在途归零，D3 `terminal-none-02/encrypted-02/persistent-02`三进程实际GREEN；正式统计语义未改。
- **编码假阳性缺口**：先加精确原因断言，`review-red-encoding`对旧坏gzip夹具实际RED；再只改server发送有效gzip及正确Content-Length，重启已登记的自有server，`review-green-encoding`实际GREEN。正式协议代码未改，不以curl解压失败冒充编码门禁。
- **缺组仍通过的聚合器**：`coverage-probe-red/regression-result.json`实际证明旧聚合器在仅8正常/4强停且缺整个安全/远端恢复/密文远端组时错误输出Final通过（标为ProbeOnly，绝不是Player验收）。修复后先强制核对本批**98条固定必验映射**（含最后3条非Development Release）的文件存在、Unity/平台、命令、Mode、Store、PID/结果及关键拒绝原因；`coverage-probe-green-groups`保留其他证据而删上述组仍被`Required acceptance run missing: net-active-restart`拒绝。真实完整profile重新校验通过，不能从现有目录或数量反推覆盖率。
- 自检另外发现并定位的观察器竞态：读取Store JSON/next及Windows `ReplaceFile`短命临时文件导致`ERROR_UNABLE_TO_REMOVE_REPLACED`；`watcher-probe-original02`第13次替换复现，`control02`无观察器10,000次通过。仅修观察器对这些mutable控制槽做元数据观察不打开文件；`watcher-probe-fixed`10,000次替换通过且真实8字节UnityFS阳性探针检出2次（不是解密内容文件）。原`encnet-encrypted-online`保留，**同一Store**在`encnet2-*`完成实际恢复并通过；未给产品加重试/吞异常，未套用Scene豁免。零明文观察仍有ProbeMisses、控制槽不读和不可进程归因限制，不声称完备捕获全部瞬时写入。
- 最新`verification-release-final.json`实际结果：**98个必验映射；92个通过的正常退出进程（含23个明确拒绝场景）、7个受控强停进程**。此前95映射/89正常的检查点`verification-terminal.json`仍保留，不覆盖。以上是进程/场景口径，不是叶/断言数量。另有12份失败/RED及1次无可靠终态运行单列，完全不计通过；正常进程中旧坏gzip拒绝保留为额外负例，不充当合法编码证据。源码8份PowerShell解析、Node语法、12份新资产meta/GUID及1110保护文件SHA通过。
- 无终态运行`remote-positive-green`的controller/Player/server均已实核消失，原Player日志停在bootstrap、无GET且Store不存在，`infrastructure-interruption.json`保留事实而不猜原因；先回收再启动新ID `remote-positive-green-02`，未重复同次长调用。所有失败原件、BuildReport、源码身份、程序集及发布身份、逐进程日志和恢复前后状态均保留。

### 30.5 最新导出与依赖/许可

- 使用原`YLFramework.Editor.ExportUnityPackage.Export`从正式六导出根及根LICENSE导出，根与Recurse、GUID均未改；研发Plan、Tests/runner、原游戏、StreamingAssets、Packages/Library/缓存、XFAB、外部DOTween、系统密码DLL、PFX/私钥均排除。`Test-ResourcePlayerAcceptanceExport.ps1`按原根精确读取tar，逐项验证**781条**路径/GUID/meta/资产length/SHA；共同父目录不是必导出的第七根，未为此扩大Exporter。
- 交付包：`%TEMP%/kilo/r810-1018d/YLFramework-8.10.unitypackage`，**7,542,869B**，SHA256 **`17f77137b6f68e1267285a4e65841607a0e1f7ed242b32fd3bc613b17fb11022`**。原`export-verification.json`保留完整结论。D1与D3普通Player的`YLFramework.Runtime.dll`实际同SHA **`b4c776082e6b5f7756b1651a3838aca7fd2b2fee1b60327c53b5db5f269e4725`**；D3可执行文件SHA **`fa01ccdbaa5f74c777609235b99ba8988285b2bf0754445e85bba268b2e61eb7`**，其余Player/Unity/Mono/发布身份见各BuildReport与source-identity/source时间快照。
- 实际Player Runtime PE引用有`Newtonsoft.Json`、`BouncyCastle.Cryptography`、UGUI与Unity modules，无UnityEditor/XFAB/Tests/Assembly-CSharp/Tink SDK反向依赖。原Runtime.asmdef、包依赖及插件导入约束不变；BC实际交付SHA仍为批准的`d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495`。
- 非Development `R1`的相同导出、PE及许可检查另记`export-verification-release.json`，原包SHA不变；其`YLFramework.Runtime.dll`实际SHA为`4c1b3ff9637f652929ac4c4ecddcf0784866dd7103157fc7bfe3ca350fb40e2a`，不拿Development程序集身份替代Release。
- BC完整MIT、SOURCE、Tink适用Apache-2.0参照声明、根XF许可、JSONObject/SharpZipLib与原第三方通知随框架导出。Json.NET来自原UPM `com.unity.nuget.newtonsoft-json 3.2.1`外部依赖，不把其DLL或PackageCache塞进框架包；实际包`LICENSE.md`为Unity Companion声明，`Third Party Notices.md`含四项MIT来源。新增/补齐既有Runtime通知，普通验收Player `Licenses/`实际携带这些适用原文，不用JSONObject许可替代Json.NET。原商业编辑器插件的再分发限制继续保留，不宣称新获许可。
- `Resource.md`已更新受测平台、包内首启、同一持久Store、Check/Prepare/Activate/Stop及失败收尾、跨进程prepared、确认序号、TLS生产策略与限制；所示API顺序已由上述Player真实运行。**只做导出和源码/PE/许可核对，没有阶段9干净工程导入验收。**

### 30.6 可复跑入口与工程终态

永久入口在`Assets/YLFramework/Tests/Tools/`：`Prepare-ResourcePlayerAcceptance.ps1`、`Invoke-ResourcePlayerAcceptance.ps1`、`Run-ResourcePlayerAcceptanceLocal.ps1`（None/Encrypted）、`Run-ResourcePlayerAcceptanceRemote.ps1`（Security/Resume/Recovery/Encrypted/Encoding）、三个独立证据/导出/coverage检查及观察器竞态回归。Runner/Build两个载体模板`.cs.txt`只由隔离载体按身份复制成C#，观察器模板仅由外部PowerShell编译，不是主工程第二加载器。脚本要求实核已安装的PowerShell7.6，必须显式`pwsh`，不能在外层Windows PowerShell5.1直接执行。

保留产物：载体`G:/Unity/Projects/CGJ2024/Temp/r810d`；非Development普通Player `Builds/R1/ResourceAcceptance.exe`、诊断Development `Builds/D3/ResourceAcceptance.exe`，隔离pin候选`Builds/C4/ResourceAcceptance.exe`；证据根`C:/Users/Kano/AppData/Local/Temp/kilo/r810-1018d`。单次复跑使用全新RunId和明确自有Store，不覆盖原件，例如：

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File "Assets/YLFramework/Tests/Tools/Invoke-ResourcePlayerAcceptance.ps1" -EvidenceRoot "C:/Users/Kano/AppData/Local/Temp/kilo/r810-1018d" -BuildName R1 -RunId replay-offline-001 -Command offline -StoreName replay-offline-001
```

完整重建先对**不存在**的证据根运行Prepare，`-CarrierRoot`选主工程Temp下新的短子目录；再按`-Operation Generate`、`-Operation Build -BuildName C1`生成六份原发布/C02部署及显式测试候选。自有server入口`node Assets/YLFramework/Tests/Tools/ResourcePlayerAcceptanceServer.cjs <证据根>`仅监听loopback18443，矩阵禁止并发改server-mode；先在remote-main做一次合法remote激活再跑Security组，Local组/Resume/Recovery/Encrypted/Encoding按脚本顺序运行。D前缀为无pin诊断Development，R前缀为`BuildOptions.None`，均不带验收符号。测试签名有效2天、证书3天；过期重建新自有夹具，不覆盖旧证据或放宽认证。新批完整聚合须明确维护等价必验映射，不把任意目录发现数自动当通过；原硬拒绝只作为已冻结B系列历史证据，不为复跑恢复旧gate。

终态记录`main-editor-final.json`：仍为开工主Unity **32204**，规定版本，Play/Paused/Compiling/Updating均false、`CompilationFailed=false`；唯一UIAcceptance场景干净，authority/Core provider均空、native Bundle0。已通过正常AssetDatabase.Refresh生成本轮12份meta；没有保存用户Scene或改其资源。仅主Editor及其AssetImportWorker存续，所有本轮Player与载体Editor已退出，自有HTTPS服务7068已停止；未终止主Unity或未知进程。1110项保护SHA与开工一致，原6项用户/并行改动全部保留。

Mono主批终态时HEAD/实时远端为`5074595212b08bba45d95d48768a895258ea6f93`，暂存区为空，产品/测试/文档/meta当时保留为未提交差异；后续IL2CPP增补及阶段8收口见§30.7～§30.8。历史E04/F03及176组Player证据按有效范围沿用，未机械重跑；普通CA正向联网、未受测平台、完备瞬时写入归因/进程总内存、断电保证及已接受的Scene延迟坏Stream风险均不冒充本轮通过。

**8.10 COMPLETE／用户阶段8总体验收通过。**

### 30.7 Windows64 IL2CPP正式支持增补专项（2026-10-19，COMPLETE／用户验收通过）

用户正式授权在`fusion/8-resource`主工作区连续补齐Unity `2022.3.62f3c1` Windows64 IL2CPP，不扩展移动端、其他平台或其他Unity版本。沿用§30 Mono已成立证据，新增IL2CPP必须真实构建/裁剪/运行并独立验证。只有全部新增门禁通过后才开放对应正式源码支持判断；候选验证不得冒充普通正式构建。不得静默降级密码、跳过认证、修改系统信任、升级Unity/Packages或凭移除ENABLE_MONO就声称支持。该专项实施当时不Commit/Push/合并或进入阶段9；最终阶段8 Git收口见§30.8。

| 已有证据 | 本专项缺口及门禁 |
|---|---|
| §30的Windows64 Mono None/E04、C02、持久Store、事务恢复、TLS/签名/序号/Range矩阵 | IL2CPP真实AOT与裁剪、P/Invoke/RSA/CNG/BC/认证Stream及native责任；不得用Mono结果替代 |
| 正式多Project Builder/Publisher、C02、ResourceStartup及自有Player验证设施 | 新归属载体中的IL2CPP离线首启、Prefab/共享依赖/Atlas/Audio/Scene，及多进程Prepare/Activate/LKG/回滚/整组事务/强停恢复 |
| 有界loopback测试pin与普通Player默认TLS拒绝 | IL2CPP HTTPS正向、签名/TLS/序号/摘要拒绝、强ETag/If-Range、真实partial跨进程206/200；非Development构建无pin及默认TLS拒绝 |
| 已有源码/程序集/发布/许可身份设施 | IL2CPP GameAssembly、metadata、实际裁剪输入与生成物身份；仅出现真实裁剪缺陷才增加最小link.xml；一次独立Review及定向回归 |

开工HEAD与实时远端均为`5074595212b08bba45d95d48768a895258ea6f93`。此前Mono源码、测试、Plan及许可差异均保留；本次新增发现`Packages/manifest.json`、`packages-lock.json`也已dirty，按开工并行现状保护，不认领或恢复。独立UI worktree为`.kilo/worktrees/fusion-ui-panel-redesign-a535884d76388bdd`、HEAD `7b7107f1c78c41c1c8861f7858a0254a35adde4f`，不写入。主MCP实时为Unity `2022.3.62f3c1`、PID32204，未Play/编译/更新、编译失败标记false、UIAcceptance干净、Resource authority空且native Bundle0。

工具链预检：规定Editor目录包含win64 Development/nonDevelopment IL2CPP variations及il2cpp工具；vswhere精确VC.Tools.x86.x64查询命中完整VS Community 2022 `17.14.37027.9`，默认MSVC `14.44.35207`；Windows SDK Include存在`10.0.22621.0`与`10.0.26100.0`，C/G盘余量充分。VS BuildTools 2026另有incomplete/canceled实例，不作为有效工具链证明。以上仅为预检，能否工作仍以真实BuildReport/编译器日志为准。

专项初始正式门禁仍关闭IL2CPP：`ResourceRemoteSource`仅明确开放既有Mono组合；`CngSegmentAead`本体也被`ENABLE_MONO`及`!ENABLE_IL2CPP`限制，不是仅缺Player测试。下一步先保留真实IL2CPP基线拒绝，再实施最小候选适配并完成所有门禁；不得将正常输入崩溃、损坏或责任泄漏归入原Mono延迟坏Stream Scene限定风险。

**历史失败记录边界**：13条历史失败/RED及无终态记录已在阶段8收口中独立核销，不构成本阶段待办；本专项不重计、覆盖或以其成功改写原件，只读核对结论与限制继续保持。

#### 30.7.1 基线与候选适配检查点

- 新自有载体`Temp/ri19`，新证据`%TEMP%/kilo/r810i-1019`；原Mono载体/Store/失败原件未写入。六份None/E04正式Builder/Publisher/C02产物已生成，实际IL2CPP `I1`构建Succeeded、0 errors、Low裁剪、C++ Release，System MSVC 19.44实机可执行，使用VS2022实际编译链。`BuildEvidence/I1`在下一次构建前冻结1392个源码/meta/模板、94个实际ManagedStripped产物及327个生成C++，不把Editor编译图或不存在的Player Managed目录充当原生证据。
- `I0`控制调用中断，原日志在C++编译124/362处停止；先核实同次controller及carrier/compiler进程均已不在，保留`build-I0-interruption.json`并以新ID `I1`独立完成，未重开同一运行或冒认I0成功。中断根因未知，不据此判工具链缺失。
- 为IL2CPP验收替换其明确不支持的`Assembly.ManifestModule`/`System.Diagnostics.Process`：原生PID由Windows kernel32取得，Runtime记录AssemblyName+BuildGuid，实际身份以GameAssembly/metadata/裁剪程序集SHA为准；不在Player伪造Mono MVID。真实`baseline-none`已完成两Project None、Core、Prefab共享依赖、Atlas、Audio和Scene及14/14装卸平衡；`baseline-remote-gate`确切Unsupported，`baseline-encrypted-red`确切平台格式能力拒绝，原F/P均保留。
- 默认正式IL2CPP仍关闭，只在`YL_RESOURCE_IL2CPP_CANDIDATE`显式候选启用真实实现和远端；pin仍另需Development及验收符号。IL2CPP不传递Mono的`DefaultDllImportSearchPaths`，候选CNG按绝对System32路径LoadLibraryEx及确切模块句柄GetProcAddress绑定，保留原ABI/provider/算法/认证/预算，无软件回退；系统模块为进程期依赖，provider/key逐owner释放。Mono保留原直接P/Invoke路径，不修改其已验密码实现。
- `C1`候选真实加密失败：`BCryptOpenAlgorithmProvider NTSTATUS=0xC0000225`；实际生成C++显示委托的CharSet属性未使string成为宽字符，发出了`char*`。原`candidate-encrypted`及`BuildEvidence/C1`完整保留。仅对相关委托string显式`MarshalAs(LPWStr)`，由`C2`重新验证；未跳过认证或归入Scene风险。C1 None本身通过，实际CNG模块为`C:/WINDOWS/SYSTEM32/bcrypt.dll`。截至此检查点未新增link.xml，尚未出现真实裁剪故障。

#### 30.7.2 候选实际门禁进展（正式默认支持仍关闭）

- `candidate-encrypted-02`、`candidate-none-02`实际IL2CPP GREEN，CNG System32路径、ABI、UTF16委托、BC派生与Editor密文互操作及认证Stream已在真实Prefab/Atlas/Audio/Scene消费中成立，native装卸7/7、14/14及live/CNG/预算责任归还。`BuildEvidence/C2`保留修复后原生/裁剪/C++身份，无预添加link.xml。
- `C2`的本地/远端更新又揭示该版本IL2CPP `DriveInfo`构造调用未实现icall `GetDriveFormat`。`none-cancel`、`encrypted-cancel`、`remote-positive`失败原件保留；不按Scene风险豁免。`C3`只在IL2CPP候选使用`GetDiskFreeSpaceExW`，保留调用者可用配额、64位字节计数和查询失败明确拒绝；本地/远端共用此查询，Mono路径仍原DriveInfo。实际`v3-none-*`与`v3-encrypted-*`完整通过：同Store安装/Prepare/Activate、Busy/取消/幂等、整组LKG/回滚、持久重启、缺错key、四事务检查点强停恢复、坏active整组回退。
- `remote-positive-02`及`il-*`安全矩阵实际通过：IL2CPP原生UnityWebRequest回调、loopback pin、RSA签名、Catalog SHA、默认TLS/错pin、重定向/404/有效gzip/超时、有限503重试、确认序号回滚保持、同序异digest/旧序拒绝、损坏下载拒绝及后续合法恢复。`BuildEvidence/C3`完整冻结。
- `range-*`六组真实partial链已运行：每份来自实际GET的1,048,576B未完成文件，206/强If-Range精确offset、强停partial恢复、忽略Range/ETag变更200重取、坏Content-Range从零重试、弱ETag不发Range；每组最终V2实际消费与再重启。一次`range-kill-complete`控制调用中断仅到bootstrap，先确认controller与Player32312均已不存在，保留原件/未知原因，不计通过或受控强停；新`range-kill-complete-02`复用原partial与不可变before快照完成同场景，后续`range-kill-active-restart`完成持久验证。长矩阵改为被跟踪的持续控制进程，不重复同次运行。
- `R0`为`BuildOptions.None`非Development IL2CPP候选（仅能力验证符号，无pin符号），`ordinary-candidate-none/encrypted/persistent`已真实通过且pin方法不存在；`BuildEvidence/R0`冻结94个裁剪产物与322个生成C++。默认TLS拒绝及最后远端事务/密文组、独立Review和正式无候选构建仍须收齐，不能在此检查点宣布正式开放。

#### 30.7.3 独立Review与正式开放

唯一独立只读Review会话`ses_eadc31ae2ffeV4AB7sdxYydmLb`完成本专项一次审查。未发现候选Runtime的ABI、线程安全、System32绑定、认证降级或Mono回归实质缺陷；发现P1最终构建身份、P2场景语义、P2归档实物三项checker假阳性风险，均在同一修复批次闭合，不追加第二次Review：

- `checker-regression-red/result.json`调用真实旧guard，实际接受了R0冒充最终构建、正常throw冒充强停、valid key冒充missing key，以及仅留identity的稀疏归档；明确ProbeOnly、非Player证据。修复后`checker-regression-green2`五项反例均被各自正确原因拒绝；最终四条run严格绑定`Builds/R1`，同时核对非Dev/无pin、CandidateValidation=false、ExtraDefines空，不再依赖可跳过的构建名条件。
- 固定row绑定Key及具体错误、Expect/Target/初末digest、强停/普通终态、精确checkpoint与fault参数；7次强停不能由throw或普通退出替代，缺错key不能由valid key probe替代。原实际92条以强化语义重验通过（`il2cpp-candidate-reviewed.json`），无需重复已成立Player门禁。
- 归档现在核对source-identity索引hash、Sources/ManagedStripped/GeneratedCpp的完整实物闭包及逐项length/SHA，以及实际native和BuildReport的一致性；不是只信保存回执。准确口径为**94个裁剪文件=52 DLL+40 PDB+2 JSON**，不是94程序集。旧不可变identity字段名称保留并解释，新归档已明确StrippedFiles/StrippedAssemblyCount。

全部候选物理门禁及上述Review修复通过后，才移除CNG/remote/可用空间路径的候选编译限制，正式支持**Unity `2022.3.62f3c1` Windows64 IL2CPP**；Win平台/64位与remote精确版号保持，IL2CPP加密也核对精确版号。认证worker读`Application.unityVersion`依据官方2022.3绑定明确`IsThreadSafe`，不把非线程安全Unity API搬入worker。其他Unity版本、移动平台、32位及其他IL2CPP平台未开放。

正式无候选非Development **R1**实际Succeeded、0 errors、Options=None、Backend=IL2CPP、Low、C++ Release、CandidateValidation=false、ExtraDefines=[]；`ordinary-final-none/encrypted/persistent/tls`四个真实进程分别覆盖包内首启/密文原CNG-BC与Stream/同Store V2 floor=2/默认CA拒绝，均pin方法不存在、正常资源与CNG责任归还。TLS日志为明确curl60证书链不受信任，不以服务不通冒充。全部实际输出、源码1392项和生成物在`BuildEvidence/R1`冻结。

Mono主验收证据沿用，另以当前实现的非Development **RM**构建执行`mono-regression-none/encrypted`两个定向回归，仍走原Mono direct P/Invoke、None文件及E04路径，均通过；不把它们混进IL2CPP计数，也不机械重跑Mono旧矩阵或176叶。

#### 30.7.4 最终证据、身份与边界

- `il2cpp-final.json`强化门禁：**96条固定IL2CPP必验=89正常+7受控强停**，其中22个预期拒绝是正常子集。本轮全部IL2CPP成功运行口径为94正常+7强停（另5个成立的基线/早期额外正例，不增加固定覆盖）；另有Mono定向2进程单列。本专项5份失败/RED与1次无终态Player保留且不计成功，I0无BuildReport另列为构建中断；日志文本重复打印不重复计进程或警告。
- 5份失败/RED分别是原加密门禁、首候选UTF16互操作错误、None/加密/远端3次DriveInfo未实现；对应后续真实成功及原件均在本新证据根。没有任何正常输入故障被套用原Mono坏Stream Scene风险。两次未知控制中断未猜原因，分别由I1和`range-kill-complete-02`独立补足同场景，并保留原件/不可变partial快照；不宣称中断根因已解决或断电保证。
- 原生 **GameAssembly.dll SHA256=`26d4870bacf00b987111078c53a11c4c76ae0eb84824705f9792bc0128988ab1`**；实际 **global-metadata.dat SHA256=`12db9bc03087b4b4e13814358662c32d67f0e8901b235a0200eac31d4a0154fa`**。正式R1源码身份索引SHA=`acc83beb3f4dfac40cce38d7e8884f98447a13d9bee5a176600e654c13102f67`；其他程序集/文件/发布及各进程BuildGuid见BuildReport和BuildEvidence索引。IL2CPP记录AssemblyName与native身份，不伪造Mono MVID或不存在的Player Managed DLL。
- 六份实际ManagedStripped Runtime PE均无UnityEditor/Tests/Assembly-CSharp/XFAB/Tink SDK反向引用；原BC批准输入SHA不变，**裁剪后BC的hash不要求等于未裁剪官方DLL**。实际Low裁剪与AOT均成立，没有新增Resource link.xml或全量Preserve；不声称High裁剪及应用动态反射类型已覆盖。
- R0/R1实际BuildReport含1014警告（不是零警告），均为既有C#文档/成员告警：CS1591 995、CS1570 5、CS0114 1、CS1572 2、CS1573 9、CS1574 2。日志各重复输出3遍，不按3042个独立问题计；未关闭诊断或扩展成无关清理。C++编译错误为0，规定VS2022/MSVC/SDK真实可用。
- 最新框架仍由原Exporter六根+LICENSE导出，不改Recurse/GUID，不含Tests/runner、游戏、Packages/Library、证书/私钥、系统CNG DLL。`export-verification.json`实核781项路径/meta/GUID/资产SHA及实际IL2CPP linker输入/native身份、BC/Json.NET/Tink等适用许可。
- 新包`%TEMP%/kilo/r810i-1019/YLFramework-8.10.unitypackage`：**7,543,576B**、SHA256 **`41418a617bd0f849f63778c4d4678f3e21a973d75e965ffa03fea511d5d63c84`**；没有覆盖Mono根中的旧包，没有执行阶段9导入。Resource.md已更新Mono/IL2CPP矩阵、IL2CPP接入、Low范围、原生身份/配额查询、System32进程依赖与原逐owner责任说明。
- HTTPS正向仍为隔离Development loopback精确pin；普通非Dev实际无pin且默认TLS拒绝，**普通CA链正向联网未实测，继续披露为缺口**。系统信任未改。有限写入观察的ProbeMisses/控制JSON不读/不可归因、进程总内存与声明数据预算区别、首次安装无确认历史的冻结限制、Mono已接受Scene限定风险均原样保留，不扩大风险豁免。

#### 30.7.5 可复跑入口及停止状态

证据根`C:/Users/Kano/AppData/Local/Temp/kilo/r810i-1019`，载体`G:/Unity/Projects/CGJ2024/Temp/ri19`；正式非Development二进制`Builds/R1/ResourceAcceptance.exe`，候选C3/R0及基线I1均保留。通用Invoke/Local/Remote设施显式传`-Backend IL2CPP`；R前缀无Dev，C前缀隔离验收符号，历史候选仅在明确`-CandidateValidation`时生成。正式路径现不需要候选符号；普通应用不得加`YL_RESOURCE_ACCEPTANCE_TESTS`。

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File "Assets/YLFramework/Tests/Tools/Invoke-ResourcePlayerAcceptance.ps1" -EvidenceRoot "C:/Users/Kano/AppData/Local/Temp/kilo/r810i-1019" -BuildName R1 -Backend IL2CPP -RunId replay-il2cpp-offline-001 -Command offline -StoreName replay-il2cpp-offline-001
```

复跑使用新RunId和明确自有Store，不覆盖原件；完整新批用不存在的新证据根与主工程Temp短子目录Prepare，再Generate/Build，server mode矩阵串行。`Save-ResourceIl2CppBuildEvidence.ps1`须在下一次构建覆盖Bee输出前冻结源码/实际linker/C++，不能事后用另一构建冒名；`Test-ResourceIl2CppAcceptance.ps1 -Final`核验本批固定必验语义、指定正式R1及实物闭包，`Test-ResourceIl2CppCheckerRegression.ps1`提供不跑Player的错误证据拒绝回归。新增/修改源码、模板、脚本及合法meta随本轮阶段8收口提交纳入；后续提交信息仍须中文。

主工程/独立UI worktree不保存或重写用户Scene/资源。Packages两文件及开工并行改动保持；1110项保护SHA实际不变，独立UI树的并行新增成果仅只读记录、不接管。所有本轮Player/载体Editor已退出，自有HTTPS PID28708已停止，未终止主Unity或未知进程；主MCP当前仍原PID32204、UIAcceptance干净、无authority/Core绑定/native Bundle，最终编译与meta/状态证据另保留。IL2CPP专项当时不Commit/Push/合并或进入阶段9；阶段8收口授权与执行状态见§30.8。

最终终态复核`il2cpp-terminal.json`与`main-editor-il2cpp-final.json`：96必验仍通过，11份PowerShell解析、Node语法、15个验收资产meta/GUID及1110保护SHA通过；主Editor Play/Paused/Compiling/Updating/CompilationFailed均false，Scene未dirty、Bundle0，未保存用户场景。仅主Unity及其原AssetImportWorker存续，无本轮Player、载体Editor或C++编译器；IL2CPP专项终态时HEAD/实时远端为`5074595212b08bba45d95d48768a895258ea6f93`、暂存区为空，阶段8最终Git结果见§30.8及最终报告。最新包身份未因纯Plan/Git收口而失效，不重复真实门禁制造数字。

**阶段8 COMPLETE／用户验收通过；Windows64 Mono及Unity `2022.3.62f3c1` Windows64 IL2CPP（Low裁剪）正式支持成立；13条历史失败记录均已独立核销，不再列为待办。阶段9不启动。**

### 30.8 阶段8最终验收与Git集成（2026-10-19）

用户已正式确认阶段8总体验收通过，覆盖8.10 Mono Player全流程及Windows64 IL2CPP增补专项。阶段8标记为**COMPLETE／用户验收通过**，8.1～8.10全部纳入已验收范围。

- **正式支持矩阵**：Unity `2022.3.62f3c1` WindowsEditor、同版本Windows64 Mono、同版本Windows64 IL2CPP（Low裁剪/C++ Release）。Windows64 IL2CPP正式路径不依赖候选符号；测试pin只存在于显式Development验收构建。
- **未开放范围**：IL2CPP High裁剪、移动端、其他Player平台、32位及其他Unity版本；不据Low裁剪或Mono结果外推。未新增Resource `link.xml`，不声称覆盖应用动态反射/泛型。
- **历史失败**：原Mono 13条失败/RED及无终态记录已独立核销，不再列为阶段8待办；原件、异常注入与正常退出分类保留。`remote-positive-green`等中断根因仍未知处按原记录披露，不改写为已解释。
- **继续有效的限制**：普通CA信任链正向联网未实测，隔离loopback精确pin不能冒充生产CA策略；默认TLS拒绝已实测。Mono已接受的Scene延迟坏Stream风险及历史no-go结论继续有效；首次安装无确认历史、有限写入观察、进程总内存与声明数据预算区别、断电保证边界均原样保留。R0/R1的1014项既有C#警告未清零，不表述为零警告。
- **Git边界**：只纳入8.10 Mono/IL2CPP必要产品源码、测试工具、合法meta、`Resource.md`及本Plan；排除`Packages`/`ProjectSettings`、`AGENTS.md`、工具配置、用户资产和独立UI worktree。Commit使用中文标题与正文；普通Push已核实上游。`integration/xf-yl`按实时祖先关系优先Fast-forward，否则正常Merge；实际Commit、远端SHA及合并方式以最终报告和远端历史为准。阶段9不启动。
