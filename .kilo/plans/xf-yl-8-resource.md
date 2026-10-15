# XF × YL 阶段 8：Resource 深度融合与全面升级总体 Plan

> 状态：**APPROVED**。架构 B、D1～D8 总体方向及 8.1～8.10 路线已批准；8.1～8.6c 已完成技术验收及 Git 收口，8.7 起始基线为 `be3dcb8`。Schema v1 保持冻结。8.7 技术 **COMPLETE**，实际实现、74项相关通过证据、独立Review修复和保护核对见第25节；按用户追加授权仅为8.7单次 Commit/Push 收口，不实施8.8及后续批次。
> 两项审核修订已落实：8.1 只稳定资源身份、Request/Lease/Scope、所有权及最小只读 Catalog 内存模型，持久化 Schema 在 8.2 真实构建闭环后冻结；8.2/8.7 只宣称本地或内部受控交付，生产远端更新须通过 8.8 安全门禁，并如实记录签名、防重放与状态丢失限制。
> 原方案批准与 Git 准备已完成；各批 Git 收口以对应单独授权为准。8.2c 先只授权实施、必要验证及一次独立技术 Review，随后在单独授权下完成一次性 Commit/Push 与状态同步；始终不授权集成合并、阶段8里程碑或自动开始8.3。
> 实施代理：仅在用户对具体实施批次明确授权后，使用 `subagent-driven-development` 或 `executing-plans` 按下文门禁推进；不得因为 Plan 已批准而自动开始。

> 8.8 已于 2026-10-15 获正式实施、隔离验证和一次独立 Review 授权，并已通过用户技术验收；现单独授权本批唯一 Commit/Push，除此之外不扩展实施范围。实施与验收记录见第26节，8.9/8.10不在本轮范围。

**目标**：以 XFAB 的项目组织、构建分包、资源加载与更新能力体系为主体，吸收 YL 的优秀生命周期和 Editor 安全成果，形成只有一个资源管理权威、可独立维护、整体质量优于双方的正式 YL Resource。

**推荐架构**：自有 `ResourceSystem` 统管资源目录快照、共享加载、资产租约、实例/场景保活与版本激活；Editor 使用同一目录模型和显式 BuildMap。Core 只定义实际消费所需的窄合同，由 Resource 向下实现；Audio 保留自己的领域 Provider。不是 XFAB 包装层，也不是给 YL `ResLoader` 加第二套更新 Manager。

**技术基线**：Unity `2022.3.62f3c1`、现有 Unity AssetBundle/SceneManager/UnityWebRequest、现有 Foundation、Json.NET 与自有 ZipTool；不升级 Unity，不新增 Addressables/SBP/UniTask 等框架依赖。

**需求来源**：本轮用户完整授权及补充淘汰决策；`AGENTS.md`、`IntegrationPlan.md`、`Architecture.md`、阶段 7 最终裁定、Tidy 最终实际成果。源码优先，历史测试数字不作为本轮验证结果。

## 1. 基线、授权与保护边界

### 1.1 已确认的 Git 与工程状态

| 项目 | 本轮只读核对结果 |
|---|---|
| 当前分支、本地集成与 upstream | `integration/xf-yl`，`fe1cd72302266789398dda9544cbf1b22e16b0b7` |
| 远端实时读取 | `git ls-remote` 确认 `origin` 的集成分支也为 `fe1cd72`；未执行 fetch/pull |
| Tidy 合并 | `fe1cd72` 是真实双父合并；父节点 `63e152e`、`c7eb1ac`，不是仅有标签/文档承诺 |
| Tidy 分支 | 本地及远端 `refactor/xf-yl-tidy` 为 `c7eb1ac` |
| `milestone/tidy-architecture` | annotated tag 对象 `a8fa333`，本地与远端 peeled 目标均为 `fe1cd72` |
| 阶段 7 | 实际标签是 `milestone/7-structure`，指向合并提交 `134936b`，实现分支收口为 `0982bac`；后续7.5为`63e152e`。最终裁定为双主体，不采用原六主体候选 |
| 用户未提交改动 | `.gitignore`、`.kilo/kilo.jsonc`、`ProjectSettings/Packages/com.unity.probuilder/Settings.json`；暂存区为空，均不碰 |
| 研究期间新增外部改动 | `Assets/YLFramework/Runtime/Core/Resources/YLFramework/Core/Canvas.prefab`：GraphicRaycaster mask `2047 -> 4294967295`，YL Adaptation Scaler增加`referenceResolution=(0,0)/automaticMatch=1/manualMatch=0`；UGUI Scaler和脚本GUID未变，原XF对照件未动，原样保留。本轮新写入仍只有本Plan |
| 分层实际成果 | `Foundation/Core/Systems/Utilities`；Foundation/Core 负例闭包门禁已存在，不能只按目录口头分层 |
| 主体程序集 | `YLFramework.Runtime` 与 `YLFramework.Editor` 当前仍直接引用 `XFABManager`；Editor实际references仅`YLFramework.Runtime/XFABManager/UnityEngine.UI`，没有UniTask引用 |
| 原 XFAB | `com.xfkj.xfabmanager` `1.24.0`，lock revision `c35dc4058ecd831409199b43cb328024202d4557`；原包严格只读 |
| 实际序列化环境 | Android scriptingBackend=1（IL2CPP）；allowUnsafeCode=0、apiCompatibilityLevel=6。Editor enter-play options开关=0，当前未启用禁Domain Reload；不把未发生的选项状态写成当前事实 |

读取了 `.kilo/plans/xf-yl-7-structure-redesign.md` 的最终裁定与执行记录、`.kilo/plans/xf-yl-tidy-architecture.md` 的实际完成记录。旧蓝图中未实施的候选不是本轮产品基线。另有历史 `test-worktree` 导航记录，不复用、不清理其资产或产物；本会话研究子代理全部只读。其他会话/Unity实时活动未能探测，不能由本会话board断言没有外部写者；每批实施前必须复查工作区、实际Editor状态与当前授权。

### 1.2 全阶段实施时仍须保护的对象

- 原 XF/XFAB/XFFSM Package、原游戏业务、原 Scene/Prefab、`MainModule.asset`、`XFABManagerSettings.asset`、MainModule Bundle、原 StreamingAssets、本轮用户改动均不得迁移或修改。
- `Packages`、`ProjectSettings` 不修改；不得借资源融合升级依赖、切换 Unity 或改游戏 Build Settings。
- 只有明确属于新 YL 的配置、夹具、产物和缓存才可写。未知目录、未登记文件、未知 `.meta`、symlink/junction 一律不自动接管。
- 不调用原 `ProjectBuild.Build(MainModule)`，不清整个 `AssetBundles`、`StreamingAssets`、persistentDataPath、Library 或 Temp。原 XFAB 构建本身会删除缓存及最终目录，不能作为新测试的便利入口。
- 被忽略但真实存在的`AssetBundles/MainModule/1.0.0/**`、`AssetBundles/MainModule/Temps/**`、`Build/**`历史Player、`ProjectSettings/XFGameFramework.*.asset`同样不清理、不重建、不接管；忽略规则不是删除保护。
- 新复制资源必须生成新 GUID；移动既有 YL 文件保留 `.meta`；类型/AQN、字段、地址和 Bundle 格式改变单独核对，不能以 `FormerlySerializedAs` 代替全部迁移。
- 实施批次不自动创建分支、Commit、Push、Merge、Tag、Reset、Clean；本次方案批准与 Git 准备（本方案提交/推送、创建 `fusion/8-resource`）不代表授权任何实施批次的上述 Git 行为。

### 1.3 本轮证据层级

本轮完成只读源码、配置、资产文本、提交及远端目标核对，使用五个独立专题研究和一个架构交叉审查。未运行 Unity、测试、构建、导入、资源刷新或性能测量；未计算 MainModule 文件 hash。结论中的“成熟”指生产者/消费者/释放链已存在，不表示本轮实测全部平台可靠。

Canvas外部变更不改变本文Core资源取得/释放链或新旧身份隔离裁定，但过去对该Prefab序列化值/UI运行效果的验证不能视为当前仍有效。现有`CoreDefaultResourceTests.CanvasPreservesOriginalSerializedDataExceptYLAdaptationBinding`整文件等价断言（源码70-76行）与研究时dirty资产静态不符，属于本轮之外的变更影响；后续须先确认该外部改动意图再复验相应契约，不能为阶段8过关而静默削弱断言。研究及8.3初次交付未改资产或测试；用户随后明确授权恢复旧数据，恢复与回归见18.6。

Context7 查询能力未暴露、MCP resource/template 列表为空；版本敏感结论补核 Unity **2022.3** 官方文档，不冒充 MCP 实时结果。官方依据见第 13 节。

## 2. 源码导航与实际架构

以下缩写仅为减少表格重复，不是新增代码层：`X` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`；`R` = `Assets/YLFramework/Runtime/Systems/Resource/`；`E` = `Assets/YLFramework/Editor/Systems/Resource/`；`F` = `Assets/YLFramework/Runtime/Foundation/`；`C` = `Assets/YLFramework/Runtime/Core/`；`A` = `Assets/YLFramework/Runtime/Systems/Audio/`。

### 2.1 XFAB：主体领域模型与闭环

```text
XFABProject / XFABAssetBundle / GUID FileInfo + Profile/Group
    -> 手动资源与目录筛选 -> 自动依赖分包 -> 显式 AssetBundleBuild[]
    -> BuildPipeline -> 映射 + 后缀 + 平台 manifest + build_info
    -> 版本化输出 / StreamingAssets / 可选 zip

ReadyRes -> 本地/内置/远端检查 -> CheckUpdateResult 更新计划
    -> 提取内置 / 并发下载 / 解压 -> MD5 复检 -> 保存版本

project + name + type -> 映射 -> 主 Bundle + manifest 依赖
    -> Asset/SubAsset/AllAssets/Scene/GameObject/Image
    -> 资产集合 + 依赖反向表 + 实例池/场景清扫 -> Unload(true)
```

重要证据：`X/Runtime/Model/XFABProject.cs:25-166,225-344,774-807`；`XFABAssetBundle.cs:46-188`；`X/Editor/ProjectBuild/ProjectBuild.cs:44-226,492-716`；`X/Runtime/AssetBundleManager.cs:137-202,492-605,1540-1744,1847-1933,2185-2326`。

依赖分包会提取手动资源之外的共享依赖，按依赖者集合聚合，并对大文件/Prefab/Scene 等单独打包。它是真实的自动分包能力，不等于仅调用 Unity 自动收集依赖；但固定 1 MB、固定条目切分和“Multiple 按文件名 MD5”不是必须沿用的优质算法。

XFAB 实际产物为：Bundle、平台名 manifest Bundle、升级 JSON 映射 `assetName -> Type.FullName -> bundleId`、`bundles_suffix_info`、`project_build_info.json`；旧文本映射仅回退。`bundleInfos` 包含文件名、字节数、MD5，以及平台 manifest、映射和后缀文件；不包含 build_info 自身。`bundle_list.txt` 是调试源路径清单，不是加载权威。Editor 没有服务端上传实现，也不生成运行端硬编码请求的 `versions/{project}/version.txt`。

主体程序集没有依赖 XFGameFramework；反过来原 XF/Game 消费 XFAB。可承接 XFAB 源码与机制而不承接全部 XF 兄弟系统，但必须保留 MIT 归属并排除不明许可的内附二进制。

### 2.2 YL：引用域完整一些，但领域覆盖较窄

```text
ResManager -> ServiceManager 单例 + ResTable + 固定 ResFactory 顺序
    -> ResKey(名称/owner/type，可变、池化、小写匹配)
    -> ResLoader 每个域持有每个 IRes 一次，递归持有依赖
    -> Resources / AB / Asset / SceneRes 门控 / LocalImage / NetImage
    -> 零引用释放 / Loading 完成后回收 / destroy-on-recycle

Importer 标记 + 目录 SubProject -> 显式 build map
    -> YL output ownership + source fingerprint
    -> staging 全量构建 / ResData(BinaryFormatter) / 回读
    -> 两份目录备份发布及异常回滚
    -> 独立资源常量生成及篡改/碰撞保护
```

重要证据：`R/Framework/ResLoader/ResLoader.cs:273-374,478-532`；`R/Framework/Res/Res.cs:49-112,169-267`；`R/Framework/ServiceManager.cs:56-137,180-279`；`E/Build/ResourceBuildOperation.cs:140-394`；`E/CodeGeneration/ResDataCodeGenerator.cs:41-112`、`ResReferenceOutput.cs:19-92`。

YL 的优秀成果是 **按持有域计数、分清资产/实例/场景、Loading 零引用的迟到收尾、原池归还及失败清理、Editor 归属预检/事务/生成保护**。这些行为值得保留，不意味着其可变 pooled 对象、工厂/表格式/两个 Manager 必须保留。

本轮未发现正式 YL AB 构建产物、`SubProject` 配置资产、`YLFrameworkData` 生成物；三个相关 Mono/配置 GUID 的定向资产文本搜索只有各自 `.meta` 命中。此结论仅限定当前仓库的旧构建/模拟热更链；**不能说整个 YL Resource 没有消费者**。Audio 已正式消费 `ResLoaderAudioClipProvider`，测试也实际构建/加载资源。仓库外消费者、非文本序列化与反射绑定不能靠本轮无命中排除。

### 2.3 实际消费者及责任归属

| 消费方 | 当前取得、持有与释放事实 | 新系统必须承接的合同 |
|---|---|---|
| Core 配表 | `C/Modules/Requests/StartUpModuleRequest.cs:118-180`：捕获 ProjectName，经 XFAB 加载 TextAsset，复制 text，finally Unload，再构造 Model；目前注释要求启动专用 Bundle，因为 XFAB 不按调用计数 | 先取得自己的租约，复制后释放自己的责任，释放先于 Model 初始化；并发持有者不得受影响 |
| Core 命名 View/Panel | `C/MVC/Views/ViewManager.Loading.cs:53-78`、`LoadViewRequest.cs:229-259`：本模块/基础模块按序 fallback，XFAB prefab + XFAB GameObjectLoader；取消不终止共享 producer | 原样保留 fallback、错误、父/会话/场景验证及初始化提交顺序，新增实例与 prefab 的可追踪责任 |
| Core Close | `C/MVC/Views/ViewManager.Registration.cs:116-200`：正常命名/直接 prefab 实例回缓存，damaged Destroy；Attach 实例失活但不销毁 | 不能把 Close 全改成 Destroy 或释放 prefab；damaged 绝不回池；Attach 仍属外部/场景 |
| Core 场景 | `StartUpModuleRequest.cs:189-223`：Single 加载，用 handle 差集验证新场景，再 AttachViews；Module Close 不主动卸载场景 | 新场景真实身份，不用短名冒充；保留不自动卸载语义，Bundle 保活必须持续至真实场景卸载 |
| Core 默认资源 | `C/UI/Windows/PanelManager.Loading.cs:132-143`、`PanelManager.Input.cs:88-96`：自有 Canvas/EventSystem 直接 Resources.Load | 保留这两个独立 GUID 及无 Resource 初始化也可用的低层路径，不由新系统破坏性卸载 |
| Foundation GameObjectPool | `F/Pool/GameObject/GameObjectPool.cs:8-11,173-233`；Data:18-33,252-353：只管实例，空桶不加载，DDOL 缓存，关闭桶不毁借出对象 | 复用稳定归属、回程/关闭/容量规则；prefab/Bundle 租约由 Resource 拥有，不能塞进 Foundation |
| Audio 命名 | `A/ResLoaderAudioClipProvider.cs:19-99`，`Internal/AudioClipCache.cs:63-183`：每次成功给独立 AudioClipLease，缓存保活，取消只去掉等待者，迟到归还 | 保留 IAudioClipProvider/AudioClipLease；换物理后端，不换 Audio 播放权威 |
| 原游戏音频 | `AudioController.cs:33-45`：原XFAB LoadAsset后直传Clip；`Game.unity:2604-2607`、`StartPanel.prefab:1326-1331`也序列化直Clip给YL AudioMusicPlayer（02.wav/03.wav），由scene/prefab依赖包保活 | Audio直Clip仍只借用、不卸载；原序列化clip/GUID和游戏XF资源链不迁移 |
| ZipTool | `Runtime/Utilities/ZipTool/Utilities/ZipArchiveReader.cs:156-165,228-351,420-428`：有流读、路径/配额预扫描；替换是先删后移，并非事务 | 内置 jar 条目读取可复用；网络归档只解到新私有 staging，发布事务仍由 Resource 负责 |
| Table/Json | `Runtime/Utilities/Table/Table.cs:15-17,109-125`：索引不拥有条目；Json 是内容解析而非资源持有者 | Resource 内用最小字典/图，不为复用 Table 维持多索引权威；Json 只处理复制出的文本 |
| HTTP | `Runtime/Systems/Http/SendWebRequest.cs:109-140,163-226`：快照 text/data 并 clone bytes，稳定终态/handler 清理 | 复用会话/取消/收尾模式；大 Bundle 不通过 Buffer+byte[]快照下载，不劫持全局 HTTP hooks |

原游戏链已核对：`Main.cs:9-18,26-59` 等待原 `ReadyRes("MainModule")` 后启动 **XFGameFramework** MainModule；`Assets/Games/MainModule/MainModule.cs:8-17` 明确 `ProjectName="MainModule"`、默认 `Start`。当前原 Settings 为 Assets/LOCAL，现存 MainModule 配置为 `1.0.0/.unity3d`、7 个组项（audio项为空）、自动图集及拷内置开启。因此“原游戏能从 AssetDatabase 跑”不能证明“MainModule Bundle 跑成功”。当前工作区内置与版本输出只确认 `StandaloneWindows64` 及旧 Temps；目录中109个Bundle、build_info的112条（另含平台manifest/映射/后缀）在名称级相合，未复验hash/CRC。既有Player内嵌build_info部分同名包size/MD5与工作区不同，也不能混作同一代制品。

Assets加载模式仅Editor分支生效，Player走LOCAL内置/提取。历史Windows/Mac Player还存在旧mapping一代，Mac内嵌Windows平台目录不能证明Mac Bundle可用；不将历史Player当本轮已验收平台。原XF启动还在末尾把ProjectName设为类型短名，游戏GameOverPanel有ModuleName作资源项目名的调用；这些脆弱字符串关系留在原链，不靠新YL兼容层“修好”。

## 3. 完整能力矩阵与取舍

标记：**成熟** = 真实闭环存在；**有限** = 能用但有明确风险/限制；**部分** = 一些环节未接通；**缺失** = 本轮核查范围内未发现。推荐项均为已批准的设计方向，尚未实现；具体实现仍在对应实施批次审查。

| 领域/能力 | XFAB 实际能力 | YL 实际能力 | 阶段 8 正式取舍与依据 |
|---|---|---|---|
| 项目/配置/环境 | 成熟 Project+GUID 文件+Profile/Group；项目名还作为路径 | 标记+目录 SubProject；Runtime 子工程只部分接上 | XFAB 项目化主体；稳定 ProjectId、独立配置 GUID/输出归属，DisplayName 不作技术身份 |
| 显式 BuildMap | 成熟，不要求设置全局 importer bundle 标记 | 成熟 map，但来源依赖 importer 标记 | 以自有项目定义生成 map，默认不改全局 importer；旧标记只作为显式导入输入 |
| 分组/手动包/逐文件包 | 成熟树、目录 filter、Group、One/Multiple | 主要依赖全局 Bundle 标记 | 承接用户可控分组与打包模式；逐文件包用资产 GUID，不再用文件名 MD5 |
| 自动共享依赖分包 | 成熟依赖者集合聚合+单独大资源；硬编码阈值 | 读取 Unity manifest，不主动优化共享依赖分配 | 保留算法主体，构建报告暴露重复/小包/大包；禁止机械沿用固定 11 项 |
| 同名/子资产定位 | name+Type.FullName，跨包同名同类型抛错；Sprite 特判 | 名称小写+十类类型码，未知类型同名回退；子资产未登记 | 显式地址+GUID/local id身份；类型由调用方提供；拒绝歧义，不靠反射字符串猜对象 |
| 平台/选项 | 多平台分支；UI 暴露破坏协议的 AppendHash/DryRun | 少量固定平台映射，LZ4+StrictMode | 统一平台能力表、选项白名单；输出与缓存分平台/Unity兼容性/构建设置 |
| 构建输出安全 | 有 Temp/版本目录，但先删旧、缓存/Streaming 同样先删 | 成熟归属清单+预检+staging+回滚+meta保留 | 直接吸收 YL 安全契约，改接新 Project/BuildMap；不是从零废弃这些成果 |
| 增量 | Unity 默认增量；实配清 Temp 抵消，MD5/复制仍全量 | 每次新 staging，全量 BuildPipeline | 持久 owned 增量 workspace + 不可变交付 staging分离；不以 mtime 自证完整性 |
| 发布/版本产物 | build_info+zip/复制；version.txt人工维护；无上传 | 无正式远端版本/发布协议 | 一份权威 catalog + release index/发布目录；本地导出和CI入口，不做云控制台 |
| 常量生成 | 本轮 Editor 没有正式资源常量生成闭环 | 成熟碰撞/篡改/幂等/独立程序集输出 | 保留生成机制及安全代码，输入换为新 catalog快照；值不是 Bundle 内部名 |
| 同步取得 | Asset/Sub/All/Scene，失败多为null；sync可和async竞争 | 同key正在加载返回null，不重复生产 | 仅Ready或本地可同步source；在途返回明确 InProgress，禁止主线程阻塞异步/网络 |
| 异步请求 | 统一operation、进度、迟到订阅、去重；特定错误挂起 | 单loader与队列，依赖级联失败改善过；通知/异常仍脆弱 | 保留XF请求思想，稳定终态/独立等待者/资源先保活；不池化公开request或lease |
| 共享加载 | 有async与native request caches，泛型key缺type | ResTable按name/type/owner匹配，但无project/source完整身份 | Snapshot+完整定位+类型的单producer，Bundle级单生产，sync不另开竞争生产 |
| 编辑器模拟 | 成熟AssetDatabase映射和场景InPlayMode | 模拟能生成旧表，Bundle节点空转 | 同一目录地址/验证，Editor专属source；不将Editor API放Runtime生产路径 |
| AB依赖解析 | 平台manifest传递依赖+反向卸载；失败调度有缺陷 | loader递归，拥有依赖；循环无前置检测 | catalog验证后的DAG、单producer持有依赖边；失败释放自己取得的图，不扫全局 |
| Resources | 不是XFAB主要通用后端；图片可读资源 | 成熟sync/async及类型/空结果检查 | 保留正式Resources source，但默认归零只drop引用，不能销毁未知外部借用 |
| 本地/内置源 | 成熟目录回退+平台可读写判定，部分平台内置/数据重合 | persistent优先+Streaming/jar读；路径/平台分支遗留 | 内置只读，写入永远私有persistent缓存；零拷贝/按需提取，统一路径与能力表 |
| 远端Bundle | 下载后本地加载；WebGL有特殊逻辑 | FakeResServer模拟，非完整远端AB | 正式元数据/下载/验证/激活流水线，异步下载可后台；不伪装同步远程能力 |
| URL/本地图片 | 完整ImageLoader组件+缓存/TTL/适配器，取消共享有问题 | Local/NetTexture创建/销毁有真实实现，WWW且小写key冲突 | 小型Texture source保留；完整图片组件吸收XF体验但以每等待者取消、URL保真重写 |
| 子资产/批量资产 | 同步/异步存在；子缓存失效错key，typed All未yield | 未提供同等正式子资产/批量语义 | 正式支持subasset、按组目录枚举/批量租约；修补义务落在新行为而非原包 |
| Scope/引用所有权 | 资产是对象集合，不是每消费者计数 | 每loader对整依赖链各持有一次，原pool责任明确 | YL持有域优点融入ResourceScope+独立lease；不沿用可变pooled Loader身份 |
| 原始Bundle访问/预加载 | 直接加载保护、全包加载、GameObject预热 | AddToPreLoad、Loader持有 | 默认暴露资产/组预加载；必要低层Bundle访问必须有显式BundleLease，不裸返回永久包 |
| 实例池 | 成熟TTL、预热、auto recycle、关闭池释放prefab | Resource GameObjectLoader其实只加载prefab，不是实例池；Foundation另有成熟实例池 | 单Resource实例facade复用Foundation桶；明确scene-local/persistent策略及prefab保活 |
| 场景 | SceneObject登记+卸载事件+延迟释放；短名/hash等风险 | SceneRes只门控Bundle，loader释放不卸载scene，也不持续为scene保活 | 按真实Scene handle+加载序号跟踪，Scene本身独立pin；默认不随ModuleClose卸载 |
| 取消/迟到/退出 | 下载/图片能Abort，AB/scene不能真正撤销；无统一退出 | Loading零引用迟到收尾、Audio规避重入；无统一所有source会话收尾 | 分离等待者/producer，native完成收尾不依赖等待协程；session epoch阻断旧完成 |
| 版本检查/差量 | 有真实Check/Plan/Download/Verify；Check删除旧包 | skeleton，Override目录循环体空、Fake直接拷本地 | 承接XF管道，检查纯读；不可变版本+commit journal+已验证回滚 |
| 校验/真实性 | MD5+sidecar复用+复检；没有签名 | 无正式远端完整性/真实性协议 | SHA-256/长度完整性；生产远端签名与HTTPS，清楚区分digest与authenticity |
| 断点/重试 | Range+append/temp/timeout；未验证206/ETag、416当成功；GET retry不完整 | 无成熟网络下载策略 | 可恢复part metadata、If-Range/Content-Range、200重启、416复验、有限重试 |
| 整包zip | 可用但先删当前目录，解压无ZipSlip门禁 | 自有ZipTool有路径/配额；旧hotupdate不使用正式闭环 | archive为可选运输形式，必须和逐文件走同一目录/校验/提交权威 |
| 缓存清理/回滚 | 有md5/版本/图像TTL，没有一致版本回滚 | Editor异常回滚成熟，Runtime更新回滚缺失 | 磁盘缓存仅删除owned非活跃版本；内存idle只淘汰无使用者节点，保留可用回滚快照 |
| 跨项目依赖 | 字段/API外观存在但构建置空、Runtime只处理自身 | SubProject不读完整依赖且路径半闭环 | 项目依赖先按固定catalog摘要精确pin；不宣称通用跨项目共享Bundle已成熟 |
| Editor操作/诊断 | 完整ProjectWindow/拖拽/过滤；无Undo、GetHashCode身份、文本统计 | 窗口预检/确认/禁用态/生成可靠；无完整运行资源面板 | 承接XF信息架构、YL安全体验；Undo、依赖/owner快照、构建/请求统计是高价值增强 |
| 资产加密 | DES/CBC零IV、无MAC、全包读内存、WebGL排除 | AesKey字段未形成实现 | 不承接DES/假加密字段；HTTPS/签名解决传输与发布真实性，加密独立暂缓 |
| 最终独立性 | XFAB本体可独立于XF，仍有Json.NET/SharpZipLib二进制依赖 | Core仍直接XFAB，Audio命名用YL，Editor双系统并存 | 正式YL默认无XFAB依赖；原游戏和可选XFAB叶子保持原链，独立载体留阶段9 |

## 4. 技术债与生命周期差异

### 4.1 确定问题及推荐处理

| ID | 证据与触发条件 | 影响 | 在新架构中的处理/验证归属 |
|---|---|---|---|
| X1 | `X/Runtime/AsyncOperation/LoadAssetBundle/LoadAssetBundleRequest.cs:85-121` + `ExecuteMultipleAsyncOperation.cs:80-95`：依赖失败使CanAdd恒false，若仍有未发出的队列项，外循环无法退出 | 异步资产/同场景互斥可能永久挂起 | 单DAG调度失败立即终结该图，释放pins；8.3注入首项即时失败和超并发队列 |
| X2 | `AssetBundleManager.cs:1263-1287,665-670`：子缓存写短bundle名，卸载按全名清理 | 重载返回已销毁子对象、缓存常驻 | 子资产与asset共用snapshot identity；8.3真实子Sprite卸载/重载 |
| X3 | `X/Runtime/AsyncOperation/LoadAsset/LoadAssetsRequest.cs:141-153` 未yield typed LoadAll request | 尚未完成就判失败 | 8.3等待原生完成后发布typed批量租约 |
| X4 | `X/Runtime/AsyncOperation/CheckResUpdate/CompareLocalResRequest.cs:65-70`；`ReadyResources/UpdateOrDownloadResRequest.cs:67-68` | 仅检测/失败/取消就破坏可用旧资源 | 8.7纯检查；8.8新版本隔离下载，验证后commit，原版本不可原地覆盖 |
| X5 | `AssetBundleManager.cs:1205-1215` 去重key无type；ImageLoader:415-421/450-454 Abort共享request | 类型串扰、单消费者取消伤害其他人 | 8.3完整identity，8.9图片join可独立退订 |
| X6 | `AssetBundleManager.cs:698-711,1540-1561` 与多个缓存；sync/async另产native实例 | UnloadAll不清全账本、不是消费者计数、竞争实例泄漏 | 单Resource记录，公开lease计数；不提供破坏未知owner的全局UnloadAll |
| X7 | `ProjectBuild.cs:103-147,431-448`、`MainModule.asset:91` | 构建清缓存、先删后拷、失败半成品；增量被清空 | 8.2继承YL事务并保存独立增量workspace；不调用原构建修改游戏 |
| X8 | `ProjectBuild.cs:70-92,124-141,724-807`：atlas在build map之后生成，未显式入map | 不能据生成atlas资产认定runtime atlas可加载 | 8.9显式图集打包和真实Sprite测试；不重建原Atlas目录 |
| X9 | `DownloadFileRequest.cs:272-300,381-399`：无If-Range/206检查，416视完成 | 盲追加/混版本文件，最后MD5只能发现不能恢复 | 8.8严格范围响应与内容复验、可重试状态 |
| Y1 | `ResLoader.cs:495-531`先注册Ready listener后Retain；`Res.cs:91-108`立即通知 | listener可在调用方尚未保活时释放/重入 | 8.1先登记持有再公开result，不继承旧通知顺序 |
| Y2 | `Res.NotifyResLoadDoneEvent`先Invoke后clear；`ServiceManager.cs:202-217` task调度无finally | 观察者异常可能留下listener/并发槽，业务回调阻断收尾 | 8.1终态/记账/解绑先完成，通知逐个隔离异常，最后必回收producer槽 |
| Y3 | `ResLoader.cs:500-511`在登记本节点前递归依赖 | 依赖环导致递归溢出/挂起 | 8.1/8.2有界DAG验证；区分资产合法互引和最终Bundle图环 |
| Y4 | `ResKey.cs:23-37`、`ResTable.cs`：owner/type可过滤、全名称小写，无project/source完整身份 | 跨项目、源、大小写URL可能别名冲突 | 正式值型地址，源locator原样保真；禁止全局ToLower URI/文件路径 |
| Y5 | `ResData.cs:210-231`追加多配置组，但asset索引仅首次填充；`AssetDataGroup.cs:217-259`重复小写键只报日志保留首条 | 多配置合并不能保证所有资产可定位，同名歧义被掩盖 | 替换ResData，不在新系统保留十类类型码或同名回退 |
| Y6 | `E/.../ResourceBuildOperation.cs:112-116` 对比 `AssetBundleSetting.cs:66-77`；SubProject数据仅creator读 | 子工程发布路径含owner，默认Runtime不含；sync/async初始化范围不同 | 唯一catalog定位含project/platform/snapshot，Editor/Runtime roundtrip |
| Y7 | `HotUpdateManager.cs:49-75,107-116`、`FakeResServer.cs:50-90`、空HotUpdateConfig | 无正式下载/差量/版本比较与override闭环 | 退出整个模拟实现，不为其造永久adapter；8.7/8.8正式更新 |
| Y8 | `BinarySerializer.cs:37-61` BinaryFormatter+OpenOrCreate；`ZipFileHelper.cs:47-68`常驻APK reader靠析构关闭；`AssetBundleHelper.cs:124`iOS引用未定义`mPersistentDataPath4Res` | unsafe格式、AOT/资源释放与Player条件分支风险 | catalog DTO与有界显式解析；每次读取明确Dispose，不导入此singleton辅助栈 |
| Y9 | `AssetBundleRes.UnloadInstantiate`与default Unload(true)，`AssetBundleSceneRes`无Scene pin | Loader已释放不等于instance/scene已卸载，bool旗标不能表达真实owner | 8.4/8.5实例/场景持有自己的图；禁止靠Unload(false)掩盖遗漏保活 |

以上不授权修原包，也不要求把即将淘汰的 YL 结构逐个补丁修完。新系统必须覆盖相应缺陷触发行为。原包未受保护的 `using UnityEditor` 等属于静态 Player 风险，本轮没有 Player 构建证据，不据此宣称原游戏已无法编译。

### 4.2 当前与最终所有权对照

| 对象 | 当前XFAB | 当前YL | 最终唯一责任 |
|---|---|---|---|
| Bundle及依赖 | 多字典、资产hash集合、依赖反向表、直接加载豁免 | Loader递归各Retain，AssetRes加载期还临时hold owner | Resource BundleRecord唯一持有native包；图边持有依赖，public lease/pending/instance/scene是根pins |
| 普通asset/subasset | 原始对象注册一次，不能代表两个消费者 | 每Loader一次；同loader重复取得不再计数 | 每次成功Acquire给独立AssetLease；Scope只是聚合持有者，不是第二个cache |
| 正在加载 | 共享operation又可被业务Abort | task/Loading状态，零引用等待收尾 | producer与等待者分离；完成先结算图/槽再通知，迟到结果零owner则及时回收 |
| Prefab实例 | XFAB pool是隐式prefab owner | YL GameObjectLoader不追踪生成的实例；Foundation池不保资产 | Resource InstanceFacade持prefab lease并用Foundation桶；Core只持实例归还/弃置handle |
| Scene | SceneObject+定时释放 | SceneRes仅Bundle门控 | SceneRecord从native成功至真实sceneUnloaded独立pin依赖，不与Module/scope Dispose混为一谈 |
| 动态Texture/Sprite | ImageLoader引用/hash+共享request | Local/NetRes创建Texture并Destroy | 由DynamicImageRecord唯一拥有创建对象，订阅者各lease；自行创建Sprite和Texture按依赖顺序Destroy |
| 下载文件/版本 | temp替换当前目录，检查可删旧 | fake拷贝，Editor有事务但Runtime无正式版本 | 新版本私有staging/part；ArtifactStore/commit journal唯一出版及清理权威 |

## 5. 优化、新能力与优先级

成本只表示相对工程复杂度，不估计小时/天数。**必需/缺陷修复**是收口门禁；**高价值增强**纳入路线；**可选**有明确收益但必须满足触发条件；**暂缓**不建立空接口或占位 Manager。

| 分类 | 能力/收益 | 相对成本 | 可验证指标或采纳条件 |
|---|---|---|---|
| 必需 | 唯一project/catalog/producer/lease权威，消除重复管理 | 中高 | 同key并发只一次native生产；所有根pins/依赖边可解释，失败后本系统native包差集归零 |
| 缺陷修复 | 先保活后发布、失败终态、异常隔离、迟到/会话退出 | 中 | 观察者立即Dispose/重入/抛异常，其他请求仍完成；宿主停止后无未收尾任务或新建宿主 |
| 必需 | 自有显式BuildMap及安全构建发布 | 中高 | 构建失败/取消不改旧发布；unknown/link/path/owner冲突零副作用，MainModule hash不变 |
| 高价值 | 稳定地址/子资产/任意已编译Unity资产类型 | 中 | 不同目录同名、Texture/Sprite投影、子名歧义有确定结果；不需反射发现类型或二义回退 |
| 高价值 | 可持久复用的增量workspace+变更产物发布 | 中 | 无变更第二次构建package内容摘要一致，未变化Bundle不重复hash/复制或给出真实原因 |
| 必需 | 可恢复下载、只读检查、不破坏旧版本、显式激活/回滚 | 高 | 断网/404/坏hash/磁盘满/中断后旧版本仍可加载；journal各断点重启恢复到一个完整版本 |
| 高价值 | 签名catalog与受限远端路径/大小/平台/协议 | 中 | 非可信签名/越界URL/巨大清单拒绝；digest验证与签名真实性分别断言；私钥不进源码/asset |
| 高价值 | owner诊断与构建重复依赖/小包/大包报告 | 中 | 能解释“为何此包未卸载”、pending/scene/instance/idle来源；诊断关闭时无逐帧日志分配 |
| 高价值 | 分优先级有界IO调度、组预加载、闲置淘汰 | 中 | queued优先级可观察，running native不伪抢占；只淘汰零使用者节点，不以预算破坏活跃lease |
| 高价值 | 图片组件、缓存TTL、独立取消、可替换URL版本 | 中 | A取消不伤B；刷新代次旧结果不覆盖新图；大图/解码失败/外部销毁可收尾 |
| 高价值 | Editor Undo、预检预览、代码生成编译闭环、CI只导出制品 | 中 | Undo恢复项目定义；生成碰撞/篡改不写入；构建/发布/常量编译三种结果分别报告 |
| 可选 | 多小文件整体archive下载与安全离线安装 | 中 | 小包多、RTT明显高时测吞吐才启用；走同一catalog/事务，不变成第二更新器 |
| 可选 | 显式多项目依赖与批量预备 | 中高 | 有真实多模块共享需求时按确切catalog digest闭包准备；先支持项目关系，不默认跨catalog复用native Bundle |
| 可选 | 自有图集构建/晚绑定 | 中 | 自有SpriteAtlas显式入map、在Bundle模式真实GetSprite；不修改旧AutoGenerate目录 |
| 暂缓 | 同catalog多版本原生Bundle热共存/运行中无缝替换 | 高且风险高 | 先实证引擎身份/依赖/卸载隔离；文件改名、hash后缀不足以证明，默认只在安全边界激活 |
| 暂缓 | 历史XFAB/YL全部产物兼容、DES兼容、跨项目自动内容去重 | 高 | 原游戏不迁移，收益不足；必要时做离线一次性导入，不能形成常驻第二authority |
| 暂缓 | 二进制差分补丁、自研资源加密、虚拟文件系统、通用插件平台、云发布后台 | 高 | 没有当前可证实收益或安全需求；不因为技术先进而加入 |

## 6. 候选架构与推荐裁定

| 候选 | 真实差异 | 优点 | 主要问题 | 裁定 |
|---|---|---|---|---|
| A：内联XFAB并修补，尽量原API/字典 | 保留巨大ABManager、原始对象集合、独立实例/图片Manager与旧格式 | 承接快、上游对照容易 | 每消费者计数/取消/scene pin需反复补丁；双YL退场与Core分层仍难；不保证整体更优 | 不推荐为最终架构，可定向复用正确叶子算法 |
| B：XF领域主体，重整一个租约/快照内核 | 项目/自动分包/双源/更新管道承接XF；明确独立lease、完整identity、单owner图；吸收YL安全机制 | 完整能力与生命周期统一、长期维护面更小；不是原包包装 | 初期合同/格式及真实consumer迁移成本较高 | **推荐**，必要局部重写胜过勉强兼容 |
| C：继续以YL ResLoader/Factory为主，外挂XF更新/Editor | 小写key/pooled res/loader维持，XF能力插件接入 | 旧YL API短期改动少 | 更新/实例/场景不得不增adapter/manager；本轮主体倒置，规模与债务大 | 不推荐 |

并列否决两个过度方案：不新建 Foundation 通用资源平台；不因为Resource大就默认独立DLL。已有 Core/Audio 的真实不同消费责任足以支持一个Core Provider和一个Audio Provider，不足以支持多层provider→resolver→adapter→backend→manager框架。

## 7. 推荐正式架构与公共合同

### 7.1 所属与目录

保留公开 namespace `YLFramework.Services.Resource` 和 `YLFramework.Editor.Resource`，没有机械迁移其他 Systems namespace 的需求。新正式类型使用 `ResourceSystem/ResourceCatalog/ResourceRequest/AssetLease` 等自有语义名，不保留 `XFAB...` 前缀或废弃Manager转发壳。

```text
Runtime/Systems/Resource/
  ResourceSystem.cs                 唯一显式初始化/关闭、组合及会话authority
  Catalog/                         定义/有界codec/平台与版本兼容验证/只读snapshot
  Loading/                         Bundle与asset records、producer图、有界调度
  Lifetime/                        AssetLease、BundleLease、ResourceScope
  Sources/                         AB、Resources、Local/RemoteTexture具体source
  Instances/                       InstanceFacade/InstanceLease，复用Foundation桶
  Scenes/                          SceneRecord/SceneLease/SceneRequest
  Updates/                         UpdatePlan、ArtifactStore、下载、commit/recovery
  Diagnostics/                     按需快照/计数，不再管理资源
  Integration/                     单CoreResourceProvider（不是通用adapter目录）

Editor/Systems/Resource/
  Data/                            新Project定义/Group/Address规则/BuildProfile
  Build/                           显式BuildMap、共享依赖分包、缓存、验证与事务
  Publishing/                      catalog/release index/archive本地导出
  CodeGeneration/                  复用YL生成保护，值来自正式目录
  Windows/                         单资源工作台、source模拟、依赖/owner诊断

Core/ResourceAccess/                Core消费方合同和显式绑定槽，不导入Systems类型
Systems/Audio/ResourceAudioClipProvider.cs   Audio领域后端，替换ResLoaderProvider
```

这是职责组织，不是要求为每个目录建一套抽象层或每个名字建Manager。窄实现同文件可维护时不拆；状态、错误、地址不可散落成互相猜测的字符串。正式source类型只按真实差异实现，退出公开 `IResCreator` 的按注册先后碰运气路由。

### 7.2 资源身份、Catalog模型和构建定义

**冻结时机**：8.1只稳定内存身份、Request/Lease/Scope 与所有权；8.2a/b 的持久化格式是未冻结原型。8.2c 已完成真实BuildMap/Manifest、子资产描述、发布、序列化读回和常量编译消费闭环，正式冻结 **SchemaVersion=1**，完整字段/身份/规范化/兼容契约以第17.4节和正式 Codec 为准。身份隔离、唯一权威和生命周期原则不重开；冻结后的协议变更需明确版本与兼容门禁，不建永久格式适配层。

- `ProjectId` 是经过校验的稳定资源项目标识，不是Module的AQN、DisplayName、磁盘绝对路径或XFABProject类型。项目定义SO GUID用于Editor归属；DisplayName可改，技术ID变化需要显式重建/缓存迁移。
- `ResourceAddress` 是不可变值，至少含project与逻辑地址；optional subasset key明确区分。逻辑地址按ordinal匹配，默认从项目相对路径生成，允许明确alias；不默认把不同目录同名合并。构建拒绝大小写折叠后冲突以保障跨平台一致。
- Editor以Unity GUID及local file id追踪资产/子资产；Runtime目录不靠AQN反射创建对象。native子资产通过目录记录的唯一name+类型定位，不能由local id直接传给LoadAsset；同文件同name+type子资产不能精确区分时构建拒绝歧义。
- `resource-catalog.json` 的候选持久化DTO为：`SchemaVersion, ProjectId, BuildId, Platform, UnityCompatibility, AppContentCompatibilityId, BuildSettingsFingerprint`，资产地址/BundleId/加载名/子资产描述，Bundle条目`LogicalId, NativeName, RelativeFile, Length, Sha256, UnityHash, Dependencies`，以及`Groups[{Id,Members[{AssetId,Required}]}]`，可选精确pin的项目依赖。字段必要性和表达形式在8.2闭环后冻结。运行配置提供预期的AppContentCompatibilityId，首次协议以确切值匹配；脚本类型/布局或资源需要的Player能力改变必须更新该值，不能仅以Unity版本/平台相同认定旧Player能读新内容。
- Group成员唯一权威为catalog中的canonical AssetId，不由常量生成器再维护一张运行表；生成器只输出group/address键。AcquireGroup取得asset批量租约，不隐式创建实例或加载scene；需要scene bundle准备时显式使用对应预备操作，不能把组预加载伪装成已加载scene。
- 推荐`BuildId` 为规范化BuildPayload的SHA-256：payload排除自身BuildId、签名、显示版本、时间戳/绝对源码路径/报告，条目按ordinal排序后用固定UTF-8编码；先求BuildId再写最终catalog，**最终catalog实际bytes的digest另记为CatalogDigest**，避免自引用，具体规范化规则在8.2验证后冻结。8.1以最小模型的只读快照身份验证去重/所有权，正式持久化snapshot/更新计划随后精确绑定CatalogDigest。release index只定位目录快照及该digest，不成为第二份资源表；生产远端的签名/新鲜度规则到8.8才开放。采用固定DTO、`TypeNameHandling.None`、大小/深度/条目数限制、重复键与schema/platform/app兼容校验；不用BinaryFormatter。
- NativeName固定自有前缀`ylr_`和项目身份命名空间，和原MainModule/native包隔离。物理文件名可含hash，但不许把改文件名当成多版本native隔离。
- Unity manifest依赖读回校验新目录中的依赖边；Runtime只按已验证snapshot图加载，不维护另一个随项目切换的全局manifest槽。catalog必须覆盖依赖包，缺失/非法/图环在发布和mount时即失败。
- Bundle级环拒绝并报告环路径；Unity对象合法互引用不直接判“非法资源环”，必要时将强连通的资产归一包再验证最终Bundle DAG。Scene和普通asset不能混进同Bundle。
- 构建定义优先自有GUID选择+明确地址/Group；不会把全工程现有Importer标记自动认领为新YL资源。标记导入必须预览来源及冲突，当前阶段只用自有测试/配置范围，不对原游戏Importer写入。

### 7.3 唯一加载、租约与失败语义

建议公共入口族：`InitializeAsync(config)`、`Acquire<T>(address)`、`AcquireAsync<T>(address)`、`AcquireSubAsset<T>`、`AcquireGroupAsync`、`PreloadGroupAsync`、`Instantiate/InstantiateAsync`、`LoadSceneAsync`、`CheckUpdatesAsync`、`PrepareUpdateAsync`、`ActivatePreparedUpdateAsync`、`GetDiagnosticsSnapshot`、`ShutdownAsync`。所有接口均围绕同一ResourceSystem实例，不另有ResManager静态代理。

关键返回合同冻结如下；具体私有helper不预先逐个建接口：

| 接口族 | 结果与责任 |
|---|---|
| `Acquire<T>(ResourceAddress)` | `AssetLease<T>`；失败抛带cause的明确Resource异常；InProgress/不支持同步是明确错误而非null成功 |
| `AcquireAsync<T>(ResourceAddress)` | `ResourceRequest<AssetLease<T>>`；每次调用独立consumer request，共享内部producer；subasset遵从相同合同 |
| `Instantiate/InstantiateAsync` | address入口由facade内部取得prefab责任；同步另支持`AssetLease<GameObject>`输入但不消费它，独立retain实例pin。返回`InstanceLease`或其request；Return回桶、Discard销毁、Dispose等价Return。外部裸prefab走borrowed路径不隐式纳管 |
| `LoadSceneAsync` / `UnloadSceneAsync` | `SceneRequest`复用`ResourceRequest<SceneLease>`领取/Dispose协议并标本次真实Scene身份；卸载返回稳定operation；SceneLease.Dispose不隐式卸载 |
| `AcquireGroupAsync/PreloadGroupAsync` | 同一个scope/批量持有域；必需项失败时释放该批已取得pins，并报告逐项cause；成功域必须显式Dispose，不返回无owner裸数组 |
| 更新接口 | Check给不可变UpdatePlan，Prepare给PreparedUpdate，Activate/Shutdown给Succeeded或明确Busy/Failure原因；没有“true但资源未准备好” |

`ResourceRequest<T>`的稳定状态/进度/cause属于request本身；成功的**拥有型结果**由request先持有，`TakeResult()`恰一次将责任交给调用方。Dispose会取消自己的等待，或释放尚未领取的成功结果；已领取后Dispose request不再释放该lease。终态不会因Dispose重写；observer异常不能让未领取结果无主，已经领取者则在自己的finally归还。Scope可接管结果以减少手工遗漏。这个claim协议也适用于Core prefab/instance结果，不适用于已复制string等borrowed/值结果。

`yield return request`只等待/观察终态，**不交割所有权**；拥有型结果仍须TakeResult，二次领取为InvalidOperationException，已Dispose后领取为ObjectDisposedException。本阶段不另提供自动领取的await通道，避免两种交付权威。SceneRequest未领取结果Dispose时只释放caller lease/观察，已有SceneRecord仍持续pin至真实sceneUnloaded。

1. 每个Play/Player会话只有一个正式ResourceSystem，内部可挂多个project；构造不向消费方开放，显式InitializeAsync建立，Current读取无副作用。相同配置的在途初始化可join，冲突配置必须明确拒绝/先Shutdown，不产生第二authority。初始化不在首次Acquire时全工程扫描或根据旧XFAB Settings自动准备资源。Resources-only加载也有可用的轻配置；Editor模拟实现由Editor装配。直接Core prefab/Canvas/EventSystem与Audio直Clip不强迫初始化资源系统。
2. 每个request捕获session epoch、catalog snapshot与source定位；成功、失败、取消、失效为稳定终态。公开request/lease不池化；内部可池化的只是不向消费者逃逸的节点，并先用Profiler证明收益。
3. 先将逻辑地址/alias解析为catalog内canonical AssetId及完整source locator，再以snapshot身份（8.1内存快照、正式持久化后绑定CatalogDigest）、AssetId/locator、实际加载类型/投影、subasset构成asset producer key；同资产的两个alias不能制造两次同类型生产。Bundle producer key为snapshot+BundleId。消费者每人拿不同lease，但共享native对象及Bundle生产。同步调用命中正在生产的节点立即给出`InProgress`，不忙等、不二次IO；冷远端/jar/WebGL不伪承诺同步能力。
4. 成功顺序为“校验native结果及Unity存活 -> 登记asset/依赖与当前consumer lease -> 设置稳定终态/移除pending/归还槽 -> 通知”。订阅迟到可以回放，但重入Dispose不会回收还在通知栈中复用的公开handle。
5. 取消只移除该等待者并释放其输入。最后一个等待者退出时，可取消的私有UWR可Abort；不可取消的AB native操作继续由producer观察完成，最终无owner就卸载自己的结果。不能因等待协程停止而失去清理责任。
6. producer错误与业务observer异常分开；前者包含project/address/source/dependency cause并稳定传播，后者逐个报告、不能跳过graph cleanup或阻止其他waiter。内部错误不靠宽泛catch+null掩盖。
7. AssetLease重复Dispose幂等且只减自己的责任；Unity对象使用权不得超过lease。Scope是显式聚合lease，Dispose先停止自己的waiter再释放自己的pins，不影响别的Scope。
8. Bundle可卸载条件是public root lease、pending native、instance、scene、cache pins均归零，且依赖入边已释放。卸载按图倒序、统一清账；不在高频路径调用全局`UnloadAllAssetBundles`或`Resources.UnloadUnusedAssets`。
9. Resources和AssetDatabase对象视为共享borrowed source。默认归零只删除本系统引用/记录，不`Resources.UnloadAsset`/Destroy共享资产；动态Texture/Sprite是系统自己创建，才由系统Destroy。显式维护边界可由应用发起全局UnusedAssets整理，不能由单lease隐式触发。
10. 常规Shutdown在临时闸门内检查/清idle/排空本系统native操作；存在live scene/instance/lease时返回可诊断Busy并恢复可用状态，不悄悄毁外部仍使用的对象，也不把失败尝试留成永久关闭的服务。成功后解绑Core/事件并关闭本authority。真正应用/Play会话退出则失效所有请求、解绑业务事件、释放private IO；未完成native操作的cleanup owner保留到完成，不能仅清静态字典丢失它，不在退出回调里阻塞等待或新建宿主。新会话复验旧native图已经排空，不能只换epoch就盲开冲突包。

### 7.4 实例与场景闭环

实例：Resource只维护资产保活和归还记录，复用 Foundation `GameObjectPoolData` 的稳定桶/队列/关闭语义，**不再造第二个池算法**。使用自有受控桶而非给整个 `GameObjectPool.Instance.ClearAll` 加资源副作用；Resource创建/预热的每个实例都登记，prefab lease至少覆盖全部active/idle/destroy-pending实例。

调用Instantiate前先取得creation pin，不能等返回实例后才补保活：Awake/OnEnable可重入Close/Dispose。创建成功将pin交给已登记instance/bucket；失败则销毁自己创建的对象后再释放。桶退休时先关队列，但借出/Destroy-pending实例未终结前不把GameObjectPoolData归还Safe池，避免旧回程访问已复用于别key的Data；全部真实销毁后才归还Data和prefab lease。预热实例也由facade逐项登记，不用无法观察新对象集合的隐式批量Prewarm。

默认Core命名实例采用scene-scoped桶；idle根可在私有失活/DDOL缓存下，但sceneUnloaded须关闭对应桶、销毁idle。已经迁移/DDOL且存活的借出实例不因出生场景消失而被毁；旧桶退休，晚归还时Destroy，等真实fake-null后释放prefab pin。persistent预热是明确策略，不静默改变Core原场景行为。

`InstanceLease` 是独立checkout身份，可Return或Discard，旧lease不能归还新一轮checkout。正常Close回缓存；失败/损坏强制Discard。销毁责任必须覆盖从未激活对象、延迟Destroy及外部Destroy；不能只依赖OnDestroy。自有实例登记与有界prune足够时不修改Foundation；原bucket数据不暴露给用户，不假定Foundation.Clear已证明native对象销毁。

直接Prefab/Attach实例继续是borrowed输入：Core直接实例复用使用Foundation现有基础能力，不自动把外部prefab纳入Resource；调用方保证外部资产与缓存实例的合法寿命。Attach/场景实例不回命名资源桶。Resource不会根据Unity Object指针将两种owner偷偷合并。

场景：`SceneRecord` 捕获Scene handle+本次加载序号+snapshot，native成功即建立独立scene pin。释放SceneLease只结束调用方持有/观察，不隐式卸载Scene；`UnloadSceneAsync`是明确动作，真实sceneUnloaded后才释放该scene图。Core默认场景把Scene生命周期责任交给SceneRecord，不随ModuleClose改现有行为。

场景native操作开始前也先建立pending-scene图pin，成功才转为SceneRecord pin，取消等待者不提前释放它。SceneLease已Dispose/旧加载序号的Unload调用拒绝，不能让复用的Scene handle指向下一轮scene。

Bundle scene以其真实`GetAllScenePaths`路径加载，Editor source以GUID解析路径，不要求写入游戏BuildSettings。重复Additive加载是不同scene意图，不把两次请求合并成同一个Scene；本系统同path加载串行关联native操作、sceneLoaded事件及handle差集。外部同时加载同path若无法唯一归属则明确失败/报告歧义，不使用GetSceneByName/Path的第一项冒充结果；外部Unload仍可由事件准确收尾。

Scene request可支持明确Activate门控，但`allowSceneActivation=false`会在0.9堵住Unity async queue，不能作为无限后台预加载。取消native开始之前可以无副作用取消；开始后没有Unity scene Abort。Additive迟到完成可以激活后卸载自己产生的scene；Single已经开始替换旧scene时无法承诺撤销，必须报告scene副作用并持续保活至合法卸载/下一转换。不可把Single取消伪装成事务回滚，也不卸载其他同名scene。是否加入完整的staged scene preload由8.5门禁裁定，不预建通用场景调度平台。

### 7.5 Core、Audio及低层协作

- Core在`Core/ResourceAccess`定义一个消费方`ICoreResourceProvider`，只覆盖三类操作：异步读文本（交付已复制的string）、同步/异步取得可校验prefab及其实例责任、场景加载（本次真实Scene结果）。返回的窄handle只表达结果/取消等待/释放或归还，不拥有加载表。
- 三类签名方向为`ReadTextAsync(projectId,address) -> CoreResourceRequest<string>`；`AcquirePrefab/AcquirePrefabAsync(projectId,address) -> CorePrefabLease/其request`；`LoadSceneAsync(projectId,address,LoadSceneMode) -> CoreResourceRequest<CoreSceneResult>`。CorePrefabLease提供borrowed Prefab用于Validate及`Instantiate(parent,scene) -> CoreInstanceLease`，实例handle只提供GameObject、Return、Discard、Dispose；CoreSceneResult只含本次Scene身份/副作用信息。合同类型全部定义在Core，仅引用Unity/Foundation，不泄漏ResourceAddress/ResourceSystem。
- Resource中的**一个**CoreResourceProvider实现这些操作；在文本成功交付前释放自己的TextAsset lease；prefab验证后将保活责任交给InstanceFacade，再释放临时asset lease；失败/取消所有路径释放自己取得的责任。
- Core显式配置provider并在会话重置时清除绑定；named操作没有provider立即失败，绝不默默回退XFAB或扫描原项目。请求捕获当时provider，不能在运行中换绑定后让旧结果归还到新后端。直接prefab/Attach/默认Resources路径仍可独立使用。
- Editor Core的`EditorAssetTool`纹理/子Sprite预览同样不反向引用Systems：保留一个消费方预览解析委托，由Resource Editor按正式project/address索引装配，直接borrow AssetDatabase/AssetPreview结果，不启动Runtime ResourceSystem/协程。旧project/name→XFABAssetBundle查找退出；index在project资源变化时失效，未知地址返回明确未解析提示而非旧XF隐式回退。
- 保留`Module.ProjectName`这个一般项目概念的公共名字，批准迁移后语义明确为**新YL ProjectId**，默认类型短名仍可用作显式新项目ID；不新增ProjectName/ResourceName永久双字段转发。原游戏继承的是原XF Module，其XFAB语义完全不变。ModuleName/FullName/AQN与Resource ProjectId仍分离。
- Core保留基础模块fallback次序、最后原始cause、owner/parent/session/scene验证、关闭前释放输入、可重启同实例、Attach/Close行为。迁移不是删掉Core既有成熟生命周期。
- 同步命名View在首个已定位project遇InProgress时终止本次调用并明确提示使用async，**不能把已存在但在途的override当缺失而选基础模块**；缺失/实际加载失败沿用按序fallback，异步遇在途则join。Core自有error kind表达此差异，不引用Systems异常类型；8.6a新增针对性门禁。
- Audio新增`ResourceAudioClipProvider`，用Resource独立lease包装既有AudioClipLease。AudioCache继续只拥有声音使用/闲置租约，不再维护另一份Bundle引用；Resource idle缓存与Audio idle不能叠加成不可解释的永久保活。
- `AudioClipLibrary` 原`BundleName`定位迁为新project/address，不自动解释成XFAB ProjectName；直接Clip配置默认不改。已有可序列化配置须定向迁移、拒绝静默猜测，必要保留一次性Editor转换而非永久转发Provider。
- 8.6b实际装配为 `ResourceSystem.GetAudioClipProvider(projectId)`，key直接表示主资产逻辑地址；Library使用新增 `Resource` 来源与 `ProjectId/Key`。旧来源枚举及BundleName仅保留序列化读取/显式副本迁移，不再参与命名加载，实施证据见第23节。
- `Integrations/XFAB/Audio`可继续作为明确可选、非autoReferenced、非默认导出的外部borrow适配器，不能成为新Resource内部fallback；原游戏直Clip仍由原XFAB负责保活。
- Foundation提供Pool、Async、CoroutineObservation、UpdateManager/Timing、会话门控，不懂catalog/Bundle/HTTP更新。仅必要时补通用稳定operation观察能力，不把资源特有状态下沉。
- Table是纯容器，Json解析复制文本，HTTP可处理小控制平面但大包transport直接拥有DownloadHandlerFile/UWR，ZipTool是受控流/解压工具。它们都不成为资源生命周期owner。

实际使用闭环：用户创建自有Resource Project定义并选择资源/地址，Editor工作台零打包模拟或安全构建正式catalog；应用入口/新生成的启动模板显式持运行配置，先Initialize，再绑定CoreResourceProvider/Audio领域provider，最后启动Module。模板使用命名资源时明确要求配置，不自动读取原XFAB Settings，也不把“生成脚本已编译”宣称已完成资源挂载。直接Prefab/Clip使用方式不增加这一步。

### 7.6 程序集边界裁定

**推荐维持两主体**，但这次基于实际需求而不是阶段7禁令：Resource与Core/Audio共享Foundation，Editor与Core preview协作，当前没有必须隔离的巨大新原生依赖；增加Resource DLL将强迫Core/consumer合同下沉或增加多个integration DLL、迁移AQN与测试闭包，收益不足。

强化真实编译门禁：Foundation仅引擎；Core+Foundation允许实际Unity模块及Json.NET，不再允许XFAB或Systems类型；整个YL Runtime/Editor源码闭包排除所有原XF程序集仍可编译。真正可选XFAB integration保持leaf。若后续证据证明Resource可选安装/编译成本/独立第三方依赖收益，另列用户决策再建立`YLFramework.Resource`与Editor边界，不机械加DLL，也不为保持双主体拒绝真实需要。

## 8. 构建、更新、缓存与可观测性

### 8.1 安全构建及增量

**能力边界**：本节对应实施批次8.2，提供本地或内部受控输入的构建、制品发布与导出；不宣称已具备生产远端发布/更新安全。下面布局是8.2闭环验证的推荐输入，持久化表达随Schema一起最终冻结。

默认工程交付目录为`AssetBundles/YLFramework/<ProjectId>/<Platform>/<CatalogDigest>/`，内置为`Assets/StreamingAssets/YLFrameworkResources/<ProjectId>/<Platform>/<CatalogDigest>/`；最终catalog bytes的digest确定版本叶身份，BuildId仍为payload身份。Runtime根`persistentDataPath/YLFramework/Resource/<ProjectId>/<Platform>/`下面固定`versions/<CatalogDigest>/`、`staging/<attemptId>/`、`parts/`、`active.json`、`last-known-good.json`、`commit-journal.json`。pointer只含schema/project/platform/catalog digest与校验信息，不复制资源目录；prepared由完整目录的Complete记录恢复。未知目录不自动认领。

解析次序冻结：兼容、完整且验证过的persistent active优先；active缺失/损坏时先按journal恢复或选兼容last-known-good，否则使用应用配置明确绑定的只读内置catalog；都不可用则初始化明确失败，不挑“目录里最新版本”猜测。所有路径均需归属、编码/规范化及allow-root校验，ProjectId/摘要/attempt token不能注入路径。绝不接管原`AssetBundles/MainModule`及原内置目录。新内置输出尚无默认ignore规则，本阶段不修改用户`.gitignore`；真实创建后的跟踪/忽略策略须用户另授权，不能用“生成产物”名义Clean或改规则。

新Editor项目/运行配置使用新YL类型和新GUID，与`XFABProject/XFABManagerSettings`隔离。生成源码仍放明确消费者承载，不放产品Runtime；保留既有生成目录安全/asmdef身份纪律，新增配置资产不塞进仅允许生成cs+asmdef的旧专用目录。

构建流程：预检完整project/map/地址/平台/归属 -> 冻结输入指纹 -> 上锁 -> 持久增量workspace构建 -> readback manifest/依赖/类型/文件 -> SHA256/catalog/codegen预检 -> 新交付stage -> 归属+完整制品校验 -> immutable publish -> 最后替换release index。未知内容和失败永不覆盖旧release；多目录复制不能宣称断电级原子，以journal/完整标记恢复。

Unity BuildPipeline输出叶名决定根manifest产物文件名，因此固定workspace叶token为`ylr_<projectToken>_<Platform>`；projectToken取稳定ProjectId的SHA-256前32个hex，注册/预检检测碰撞，不直接使用原`StandaloneWindows64`/`Windows`叶名。2026-10-14真实Unity 2022.3探针确认自动根manifest的`Object.name`/`m_AssetBundleName`为空，用户已批准修正该技术假设：不把文件名冒充native身份，不伪造native名称；通过完整manifest项目命名空间、产物摘要和实际readback，以及不同项目命名空间的manifest同时加载成功、同产物复制改名仍被拒绝的真实证据验收隔离。普通Bundle的native name仍须带自有前缀。workspace按Project GUID、target、Unity版本/选项/配置fingerprint分隔，不用随机transaction叶破坏增量；只认本工具有效marker。不清整个缓存，损坏workspace可单独重建，不影响已发布产物。无变更跳构建须可靠source fingerprint+已验证产物，不只靠mtime+length。Unity生成manifest和逐包`.manifest`留构建验证/调试集合；Runtime依赖权威是catalog，不在运行加载一个原平台名manifest槽。该修正不改变Runtime、发布或Catalog的阶段边界。

共享依赖分包以XF设计为主体，命名使用稳定GUID集合及规范化依赖者集合；先测重复字节和更新影响，不默默拆出大量小包。LZ4+StrictMode为默认，Full Rebuild为明确操作，DryRun只产预检报告不发布，AppendHash只有目录完全支持时才开放，不给无效选项外观。

### 8.2 正式更新与版本激活

**阶段边界**：实施批次8.7只接本地/内部受控、已验证的snapshot，建立准备、激活、回滚与恢复事务。目录导出、摘要正确或本地激活成功不等于生产远端可信；正式生产远端检查/下载/激活必须等8.8签名、真实性、下载与恢复门禁全部通过，开发/内部模式不得静默升格或绕过该门禁。

1. `CheckUpdatesAsync`只读版本/catalog/本地verified状态，产出不可变UpdatePlan：来源/目标digest、缺失及已可复用文件、总字节/可用空间要求、动作/兼容错误。不删除旧文件、不保存假成功版本、不从当前目录捏造可用状态。
2. 内置catalog可信来自已构建应用；生产远端经HTTPS且验证release/catalog签名，所有文件校验SHA256+length。首版签名固定`RSA-2048或以上/PKCS#1 v1.5/SHA-256`，envelope含schema/algorithm/keyId/signature；签名覆盖确切UTF-8 bytes，Runtime配置只放keyId与公钥Modulus/Exponent，不含私钥，不接受远端自带公钥作为信任来源。使用现有BCL，不用Unity未必支持的新版ImportFromPem或新SDK；8.8实证目标后端，不能验证的目标明确不支持该production schema。开发loopback可明确不签名，但不可静默降级生产模式。
   签名只证明来源与内容未被篡改，**不能证明这是最新版本或阻止合法旧发布重放**。8.8建议在已签名release index中绑定project/platform/channel/app兼容范围、单调ReleaseSequence与CatalogDigest；客户端在既有ArtifactStore/commit记录内维护最高已验证且成功激活的序号与摘要（可与应用配置的最低允许序号合并作为门槛），不在只读Check阶段落盘。低于门槛的远端发布拒绝，同序不同digest拒绝，相同序号/摘要幂等；激活失败不前移门槛。显式本地last-known-good恢复/回滚不降低门槛，正式远端回滚需以更高序号重新签发指向旧catalog的发布授权。
   此最小策略防止相对于客户端已确认历史的降级/冲突，不证明服务器没有冻结在同一合法旧发布。首次安装、清除/回滚本地状态或本地状态被恶意篡改时，也不承诺强防重放；离线可继续使用已验证版本。需要可信时间、到期策略或在线最新版本证明的更高威胁场景另行审核，不新增云控制台、时间服务或第二套版本Manager。
3. 远端endpoint与channel由config显式指定；catalog只给受限相对路径，不接受任意跳转主机、绝对路径或无限条目。限制清单、文件、展开大小、disk budget，错误诊断不输出token/私钥。
4. 下载到私有`.part`及其resume metadata，包含目标digest/length/可靠validator、源与schema。有强ETag才默认续传并发If-Range，缺validator则安全从零重下；200/206/Content-Range起点与总长必须核对，压缩Content-Encoding不能盲append。200忽略Range时从零重下，416先复验完整目标不能直接当成功。无HEAD能力时以catalog长度和GET响应工作，不要求HEAD存在。
5. 私有transport有界并发、连接/无进展超时、有限指数退避+jitter；网络/408/429/5xx可重试，路径/鉴权/签名错误不盲重试；Retry-After有界处理。取消保留可验证part用于恢复，不删除当前版本；实际Unity对象操作与UWR接受/释放在主线程，hash/普通IO可按平台后台。
6. 全部文件及目录验证成功写Complete记录，之后才能成为prepared版本。已有相同hash blob可复用，但文件mtime不能证明可信，来源/verified元数据不一致要重新校验。跨会话的sidecar不能代替实际文件SHA-256：mount可按需验证，但每个文件在首次native使用前必须复验并固定为私有immutable输入，同会话去重验证；签名正确但本地文件损坏仍必须失败/回退，不把“检查本地记录”冒充完整校验。
   相同target CatalogDigest的Prepare去重且幂等，已Complete不重下；新的Check只读，不使既有prepared文件失效。PreparedUpdate可由Complete记录跨重启重建，含预期from/to摘要；Activate若当前已是target则无副作用成功，若from已变化且target不同则明确StaleBase，重新Check后再激活，不能无意降级。回滚是单独明确选择，不借旧prepared偷偷覆盖新active。
7. 默认不运行中热换同catalog。后台准备可以和旧版本使用并行；显式ActivationBarrier停止该project新Acquire，排空in-flight、flush idle，等待asset/instance/scene pins清零并卸载本系统native图，才切内存snapshot和持久active index。Busy/失败必须恢复旧snapshot的获取闸门，旧版本仍可新Acquire，不任意Dispose别人lease或永久卡住服务。应用先停止相关模块/consumer scope，切到不属于待更新project的bootstrap/neutral scene，再卸载旧scene并重试；不可能达静默边界时保留prepared到下次启动。自有fixture演示这个步骤，原游戏不迁移。
8. commit journal至少记录from/to digest、prepare/verified/commit状态；active index同目录替换并保留last-known-good。启动恢复只认完整验证过且平台/应用兼容的版本；回滚也只在同一安全边界激活，不能把版本号相同当作同内容。断电无法跨目录绝对原子，但任何恢复路径只能选择完整快照。
9. 可选archive只作为同一UpdatePlan的传输形式：受信manifest+条目allowlist+ZipSlip/link/大小配额，解到新private stage，逐文件验证后调用同一commit。不是ZipTools直接覆盖当前数据目录。
10. 项目依赖按目标catalog摘要精确pin，闭包准备/兼容验证；项目关系不等于跨项目原生Bundle共享。默认各catalog内图自足，多项目activation任一Busy/失败不部分切换；暂不承诺自动跨catalog内容包去重。

### 8.3 缓存与诊断边界

内存idle缓存仅持显式cache pin，有capacity/idle时间及强制flush；物理Bundle共享以图record为准，不以“asset已释放”直接卸载整个包。估计内存与文件字节分别展示，不宣称压缩文件大小是native memory。活跃lease不能因LRU/低内存预算被强制销毁。

磁盘清理只在自有marker下，排除active、prepared、last-known-good和in-flight；最低保留一份可用回滚，清理失败报告而非吞异常。release版本目录和小blob缓存可先用简单按版本清理，只有重复字节基线证明值得时再加跨版本blob复用。

诊断为只读snapshot：project/catalog/schema/platform、queued/running、等待者数、producer数、lease/instance/scene/cache pins、依赖入边、失败cause、来源、下载字节/重试、阶段时长。默认不逐帧抓栈或全场景搜索；owner标签显式传入，Editor可按需采样stack。报告不成为运行时状态权威。

## 9. 保留、融合与淘汰清单

不是“通过旧测试的类型永久保留”。先承接有价值行为与消费者，再退出实现；不保留无消费者永久兼容层。删除前另做本批类型/GUID/反射/序列化扫描及差异核对。

| 对象/能力 | 实际职责与消费面 | 决定及替代 | 兼容影响与删除收益 |
|---|---|---|---|
| XFAB项目/Group/过滤/显式map/自动依赖分包 | 原资源组织主干 | 融合为自有Project定义和构建器，修正命名/阈值/安全 | 新数据不复用原类型/GUID；取得完整领域能力，不背旧不安全Builder |
| XF请求/双源/检查-计划-执行 | 更新加载主干 | 机制吸收，状态与所有权重整 | 不长期包装原Package API；形成自有可测内核 |
| YL按持有域Retain/Loading零引用/原pool责任 | Audio/资源测试真实使用 | 保留语义，融合lease/scope和session收尾 | 行为测试移植，旧类不因历史投入保留 |
| YL ResourceBuildOperation | 安全预检、归属、事务、meta保护 | 保留最有价值代码/契约，改为新BuildMap与发布布局 | 改造内部反射/固定路径测试；不能把它和旧Builder整套一起删掉 |
| YL常量生成/ResReferenceOutput | 可靠生成与消费者asmdef | 保留碰撞/篡改/幂等/编译保护，换目录输入 | 更新生成值与输出schema；已有生成物不得自动认领；收敛为唯一生成入口 |
| ResManager+ServiceManager | 两层服务入口、队列与全局Res表 | 8.6退出，ResourceSystem与scheduler唯一替代 | Audio/测试需迁移；消除重复Manager与隐式启动 |
| ResLoader/IResLoader/扩展/recycler | pooled持有域、destroy-on-recycle、Scene helpers | AssetLease/ResourceScope/Instance/Scene明确职责，退出类型 | 方法/反射绑定需迁移；不新增旧名forwarder，降低池化公开对象重入风险 |
| Res/IRes/SimpleRc/ResTable/ResKey | 可变状态和广泛查询匹配 | 单records+不可变地址+稳定公开handle；SimpleRc可留在更适合位置仅在另有真实价值时 | 本轮引用仅Resource为主，不能为保留测试人工制造消费者；少掉多索引与类型兼容回退 |
| ResFactory/IResCreator/fixed creator链 | 先匹配先赢、可自定义creator | 具体source和有验证的locator，不保留公共插件外观 | 仓库外自定义creator需用户显式迁移；消除顺序敏感路由/空返回回退 |
| ResData/AssetDataGroup/typecode/BinarySerializer | 旧AB目录协议与BinaryFormatter | 正式versioned catalog替换，退出旧格式 | 没有承诺读取全历史YL二进制；已有用户产物可单独离线转换，不维护unsafe反序列化 |
| SubProject/SubProjectData | 目录owner与旧子工程布局 | 新Project/Group显式定义，吸收owner预检而退出半闭环模型 | SO GUID、旧输出ownership需定向确认；新系统不自动认领旧目录 |
| HotUpdateManager/Config/FakeResServer/ResVersion | 未完成流程和本地假服务器 | 完整退出；正式UpdatePlan/ArtifactStore/transport | 新测试server位于Tests，不进入产品；删除硬编码Remote路径与伪可用API |
| AssetBundleHelper/Setting/PlatformCheck/ZipFileHelper | 路径、平台、标记、模拟、singleton jar读取混合 | 来源/平台能力归一，Editor map在Editor，zip用受控reader | 更新Audio/测试路径与PlayerPrefs口径，退出旧prefs读取/遗留iOS分支；不做永久prefs迁移 |
| LocalImageRes/NetImageRes | 有价值的动态图生命周期 | 保留能力，换UWR/完整key/独立lease，8.3承接source、8.9完善组件 | 旧前缀可做一次性调用迁移，不用WWW/小写URL旧缓存权威 |
| YL Resource.GameObjectLoader | 实际是prefab加载owner，不是实例复用 | 由AssetLease与InstanceFacade替代 | 旧pool失败清理测试迁移语义，不保留“Loader”误导入口 |
| 旧ResService窗口/标记菜单/BuildDataTable入口 | 一套窄标记构建链 | 8.2新工作台与唯一build/catalog入口；安全/生成代码原地改造或保GUID移动 | 固定8路径/导出哨兵需同步更新；旧标记导入仅显式、无双Builder |
| Foundation/GameObjectPool与通用Table/ZipTool | 成熟通用能力，不因Resource未用而无价值 | 保留，不因淘汰ResTable删除Utilities.Table；实例桶直接复用 | 不把Resource退出扩大为低层清库，避免两套实例算法 |
| IAudioClipProvider/AudioCache/直Clip | 正式音频领域合同与原游戏消费者 | 保留；仅换ResLoaderAudioClipProvider和AB定位字段 | serialization/配置Editor迁移；原直Clip行为保持，避免第二声音管理器 |
| 原XFAB legacy mapping/DES/secret/obsolete依赖字段 | 支撑历史包而非新YL需求 | 新产品不承接；原Package原样保留 | 原游戏不迁移因此无须新Runtime兼容，减格式/安全/许可证负担 |

## 10. 实施批次与最小充分门禁

下列十个主批次按依赖与可独立拒收的闭环拆分，不按目录机械分包；8.2与8.6内部还有必须逐个完成/review的独立子批次，不允许一次合成巨型修改。8.1-8.5是隔离夹具中的新链，不改变默认Core后端；**8.6c后不能继续保留新旧YL两套运行authority**。8.7-8.8新增完整更新，不把8.6“加载融合”冒充阶段8全部完成。

每批共用步骤：建立本批失败/基线用例；实现本批最小闭环；运行本批有影响的门禁；核对diff/新增meta/原数据保护；完成独立review。8.1～8.5、8.6a 已完成技术验收和 Git 收口；8.6b 技术 COMPLETE、待用户技术验收及单独 Git 授权，证据见第23节；8.6c及后续未执行。Git 写入仅按用户当前单独授权，不由本路线自动触发。

### 8.1 正式合同、目录及Resources最小闭环

**主要范围**：`R`新入口/Catalog/Loading/Lifetime/Resources source；新增Core消费合同但不接生产；`Tests/EditMode/Resource`、`Tests/PlayMode/Resource`及Foundation边界。

**依赖/输出**：稳定第7节资源身份、Request/Lease/Scope、所有权与终态合同；产生ResourceAddress、最小只读Catalog内存模型/快照身份和显式初始化。Resources真实取得/失败/释放记录可用；不要求此批最终冻结完整持久化Schema，绝不读取原XF设置。

**风险**：通知重入、pooled handle身份、source ownership、退出门控。

**状态**：**COMPLETE（2026-10-14，实际能力与验收证据见第14节）**；交付后停止，等待用户技术验收。

- [x] 验证同地址两consumer共享producer但独立lease；取消A不伤B；Ready回调先看到保活，抛异常/Dispose/重启不跳收尾。
- [x] owning结果未领取Dispose、yield后领取一次、二次领取/Dispose后领取、领取后request Dispose不影响lease、observer异常与Scope接管分别锁定；值结果无伪释放责任。
- [x] 验证无效配置/最小Catalog模型/type/address、未知路径、两source相似key、大小写URI不混淆；初始化失败可重试但不出现半ready，不以完整持久化字段定稿作为8.1通过条件。
- [x] 真Resources资产归零只drop自身引用，外部borrow仍有效；公开handle旧代次不能影响新epoch；Core/Foundation闭包无Systems逆向类型。

### 8.2 XFAB主导组织/构建 + YL安全/生成融合

**主要范围**：`E/Data,Build,CodeGeneration,Windows`与必要Runtime DTO；旧Editor入口收口为新入口，不保留两个Build按钮执行不同权威。

**依赖/输出**：依赖8.1；自有Project/Group/GUID映射、自动共享依赖分包、真实小Bundle产物、catalog/release index、source模拟索引、owned持久workspace/发布事务/常量闭环。能力仅本地或内部受控构建/发布，不开放生产远端更新；正式持久化Catalog Schema在本批真实闭环通过后最终冻结。

**风险**：map身份、增量与staging冲突、nativemanifest命名、原Importer/产物误认领、生成程序集身份。

**子批次次序**：8.2a完成自有定义/map/依赖分包/持久workspace，只构建隔离产物并过manifest身份/重复与增量门禁；8.2b接安全发布/catalog/归属/回滚，故障注入旧产物不变；8.2c接工作台/Editor模拟索引/常量生成与实际编译。每个子批次都独立review和停止检查，不把生成器失败变成整个构建未知状态。

**8.2b 状态**：**技术 COMPLETE，已完成 Git 收口**，见第16节。**8.2c 状态：技术 COMPLETE，Schema v1 冻结且已 Git 收口**，见第17节。本地/内部受控8.2闭环通过，不等于8.8生产远端安全；8.3 Runtime加载的独立验收证据见第18节。

**Schema最终冻结门禁**：8.2c收口前对真实BuildMap/Manifest依赖、资产与子资产加载描述、Group成员、catalog序列化/读回及生成常量消费逐项对照；用最小原生读回探针确认子资产描述确能定位Bundle内容，不将8.3完整Runtime加载器提前实施。闭环证据充分后记录首版Schema和规范化规则；必要字段修正属于本批设计验证，不为未冻结原型增加永久兼容层。

- [x] 自有Fixture构建：Text/Prefab依赖/Scene分包；读回catalog/manifest一致；输入无变化二次构建记录各阶段时长/hash/复制量。8.2a见第15节，8.2b新增真实子资产/发布和受影响读回见第16节；六项回调仍禁止整体缓存命中。
- [x] 本地门禁：native构建失败、Catalog输入/复制/替换失败、取消、unknown文件、junction、owner重叠及旧发布不变；`.meta`保留和恢复沿唯一Workspace/Publisher进行，8.2a/b证据见第15～16节，8.2c真实闭环与回归见第17节。生产签名输入及信任链不属于本地交付，仍留8.8。
- [x] 同名地址/alias、main/sub同名同类型及可赋值类型歧义、资产合法互引与Bundle环、Unity选项和marker/锁门禁均由正式链验证。8.2a/b已通过部分不重复全量构建，本批补共同描述与模拟一致性负例，见第17节。
- [x] 普通Bundle native name使用自有project命名空间；自动根manifest按第10节已批准的实际身份门禁验证，不把文件名冒充native name。另一新project同时读回成功、同产物改名仍被拒绝；固定路径测试和Export哨兵当批同步并通过。8.2a证据见第15节。
- [x] 常量碰撞/篡改/无变化生成/实际程序集编译及地址消费通过；Bundle/发布成功与常量失败独立报告；原ResourceOutputSafety/CodeGeneration/Compilation/Window测试改造到正式输入，保护机制保留，见第17节。

### 8.3 AssetBundle、依赖、子资产与动态source闭环

**主要范围**：`R/Loading,Sources`；Fixture使用8.2产物；替换旧动态图片所需基础source先完成，不提前删有价值功能。

**依赖/输出**：依赖8.1/8.2；AB/Resources/Editor模拟/Local/URL Texture通过唯一asset lease合同；正式type/subasset/group/bundle低层租约与错误链。

**风险**：native不可取消、同步/异步竞争、图环/菱形与失败pin释放、动态图片创建后迟到。

**状态**：**技术 COMPLETE（2026-10-15，见第18节），待用户技术验收和单独 Git 收口授权**。

- [x] 真实Text/Prefab/Sprite subasset、typed多资产加载/卸载重载；菱形依赖只产一次，缺包/损坏/未知子名有稳定终态。
- [x] 依赖首项即时失败及大于并发槽的排队失败不挂起；typed同名并行不串结果，sync遇async无二次nativeIO。
- [x] 两scope共享资产，一方Dispose另一方存活；失败/最后取消/退出的native迟到完成最终native差集回本系统基线。
- [x] 本地图片真实大小写、网络错误/坏图/零owner/解码后取消，Destroy仅作用本系统创建Texture；新正式 source 使用 UWR，不修改或退役8.6前的旧图片后端。

### 8.4 Prefab实例、缓存与Foundation桶协作

**技术状态（2026-10-15）：COMPLETE。** 实施及真实 Unity/native Bundle 证据见第19节，已完成用户验收及 Git 收口，见第20节；不表示阶段8整体完成。

**主要范围**：`R/Instances`；复用`F/Pool/GameObject/GameObjectPoolData.cs`，仅有证明必要的通用改动才碰Foundation；对应新实例集成测试。

**依赖/输出**：依赖8.3；可创建、复用、预热、Return/Discard的实例lease，bucket持prefab graph，scene-scoped/persistent显式策略。

**风险**：延迟Destroy、从未激活OnDestroy缺失、桶退休后晚归还、DDOL与Core旧场景行为不同。

- [ ] 正常返回复用同instance、damaged不回池；活/idle/退役晚归还/容量裁剪均在nativeDestroy后才最后释放prefab及依赖。
- [ ] 从未激活预热、外部Destroy、Awake/OnEnable/OnDisable重入、Clear与新Acquire交错、checkout旧handle重复归还，不能误销毁/误还新实例；创建pin与Data回Safe池时机真实可观察。
- [ ] scene unload清idle而不毁迁移/DDOL活实例；新场景桶不复用旧退休checkout；实例计数与native包保活对照。

### 8.5 Scene真实身份、保活与不可逆取消

**技术状态（2026-10-15）：COMPLETE，已完成用户技术验收及前序 Git 收口。** 真实 Scene Bundle/PlayMode、实际退出排空与独立 Review 证据见第21节。

**主要范围**：`R/Scenes`，8.2自有Scene Bundle/Editor模拟与PlayMode夹具；不改游戏BuildSettings。

**依赖/输出**：依赖8.3/8.4；SceneRecord与准确handle、explicit unload、默认生命周期转交，Core所需Single与Additive结果。

**风险**：Unity全局async queue、Single不可逆、同名scene、last-scene不可卸载、scene对象依赖提前释放。

- [x] 加载/激活/真实unload前后Bundle与依赖pin准确；释放调用方SceneLease不把已加载scene的材料/资源毁掉。
- [x] 两个同名Additive scene按handle区分；external unload/Single替换只释放正确scene；不用短名/hash猜结果。
- [x] 同path重复Additive逐次排队、external同path无法归属时明确报歧义；SceneRequest领取与未领取Dispose、旧SceneLease卸载调用不能释放新一轮handle。
- [x] native前取消、Additive迟到取消、Single开始后取消、手动0.9激活门控分别验证，无假“已撤销旧scene”，无永久queue阻塞。

### 8.6 Core/Audio/Editor真实消费者接入与旧YL退场

**主要范围**：`C/ResourceAccess,Modules/Requests,MVC/Views,UI`直接资源调用；`A/ResLoaderAudioClipProvider.cs,Config/AudioClipLibrary.cs`；Core Editor预览/生成/Help中资源接入；旧`R`退出清单和主体asmdef的XFAB引用。

**依赖/输出**：8.1-8.5全部门禁；正式Provider与生成启动绑定、新YL fixture配置、主体Runtime/Editor无XFAB符号/程序集依赖，旧Resource服务/格式/模拟更新/旧provider退出。原游戏和可选leaf不迁移。

迁移的是测试/消费代码及其指向的**新自有fixture**。已有受保护Scene/Prefab保留原文件与meta，需替代时创建新GUID副本/新配置并让YL测试显式使用；Audio旧配置若需要转换也先预览输出新配置，不批量保存或重序列化原Scene/Prefab。

**风险**：Core fallback/Close/cache/scene行为漂移、Audio直Clip误卸载、配置identity迁移、旧类型测试把实现固化成永久兼容债务。

**独立子批次**：8.6a仅切Core文本/prefab/scene、实例回程与Core Editor/生成/Help，并过Core闭包/行为门禁；8.6b切Audio provider与配置入口，独立过音频保活/直Clip/取消测试；8.6c在两类正式consumer均已转交后退出旧YL实现、去主体XFAB引用、收口测试/导出哨兵。a/b过渡期间测试资源域隔离，不让同一asset被两种authority同时接管；c不得延期为长期兼容层。每个子批次均可单独拒收和授权，禁止全批一次性巨改。

**8.6a 状态（2026-10-15）：技术 COMPLETE，已完成用户技术验收及 Git 收口。** 正式 Core Provider、ProjectName 语义、消费者与 Editor 接入及独立 Review 证据见第22节。

**8.6b 状态（2026-10-15）：技术 COMPLETE，已完成用户技术验收及 Git 收口（`b1116085eb4290c6cc202f63b761797e4eac739e`）。** Audio正式Provider、配置显式迁移、真实音频/Bundle保活及独立Review修复证据见第23节。

**8.6c 状态（2026-10-15）：技术 COMPLETE，已完成用户技术验收及 Git 收口（`be3dcb8`）。** 旧栈退役、主体实际编译/二进制闭包隔离、定向Editor/native/PlayMode消费者与原游戏/可选适配共存、独立Review修复及保护核对均完成，见第24节。

- [x] 文本string交付前释放自己lease、共享读表不伤另一模块；fallback次序/error cause/Model初始化和startup rollback保持。
- [x] Core `yield`等待后只领取一次owned结果，Dispose未领取/领取后两条路无泄漏或双释放；同步InProgress不错误fallback到基础模块，异步join同producer。
- [x] Core命名View/Panel同步异步、正常Close复用、damaged/Attach/父销毁/场景迁移/取消迟到/关闭重启选定回归；direct prefab及默认Canvas无需Resource仍可用。
- [x] Audio Resources/AB命名lease、idle cache、crossfade尾轨、cancel、Shutdown迟到、依赖最终释放；直Clip不被新Resource或Audio卸载。
- [x] 新配置/序列化字段定向资产审查和一次性迁移入口；原XF配置与MainModule地址完全不读写。Core closure拒绝Systems/XFAB，Editor模拟通过正式目录工作。
- [x] 迁移`ResourcePoolTests`、Audio内部Res状态断言、CoreXfab fixture/池反射与Export哨兵到等价行为；不删除关键断言制造通过。不再容忍旧YLManager永久并存。

上述混合门禁分别由8.6a/b/c完成，旧ResourcePool/旧YLManager整体退役及最终导出/主体依赖清理证据见第24节；不将8.6的加载融合或8.7的本地事务冒充阶段8整体完成。

### 8.7 版本目录、纯更新计划与激活/回滚事务

**主要范围**：`R/Updates`的catalog mount、UpdatePlan、ArtifactStore、active index/journal/恢复/清理；`E/Publishing`受控release布局；仅用本地或内部受控、已验证snapshot，不提前开放生产远端更新。

**依赖/输出**：依赖8.6；v1/v2检查只读，prepared版本与ActivationBarrier、last-known-good、跨重启恢复、owned cache清理。

**风险**：旧native包仍占用文件、snapshot去重key遗漏、崩溃半commit、版本同名异内容、多项目部分激活。

**技术状态（2026-10-15）：COMPLETE。** 仅本地普通文件系统来源；真实多版本/native、整组恢复、独立Review修复及必要消费者门禁见第25节。按追加授权单次Commit/Push，不开始8.8。

- [x] 只Check/取消/准备失败不改active文件/hash或版本；v1 lease/scene/instance活着时v2激活Busy且旧Acquire仍可用，neutral边界flush/真实卸载后才切snapshot。
- [x] 相同target重复Prepare不重复复制/验证，Check不使prepared失效，跨会话重新初始化恢复Complete；already-active激活无副作用、from已变化明确StaleBase。
- [x] journal具有实质风险的写入/切换边界注入故障，重新初始化只选完整兼容闭包；回滚走同一安全边界，native不混代。
- [x] 清理排除active/prepared/last-good/在途及原XF/未知目录；多project closure有确切摘要与平台，任何Busy都不部分激活。
- [x] 没有开放production远端入口；本地摘要校验、内部导出或恢复成功不代表生产真实性/防重放验收，正式远端能力留8.8。

### 8.8 正式远端下载、真实性、断点与恢复

**主要范围**：`R/Updates`专用stream transport/verify/retry；`E/Publishing`签名输入/制品导出；`Tests`私有本地HTTP服务，不新增产品FakeResServer。

**依赖/输出**：8.7事务核；HTTPS/签名/有界目录、download-to-part+metadata、Range/If-Range、有限重试、发布序号门槛及可选archive同权威闭环。全部签名、真实性、下载和恢复门禁通过后才开放正式生产远端更新，不据8.2/8.7已完成提前宣称生产可用。

**风险**：服务器忽略Range、压缩编码/ETag变更、416错误、不支持HEAD、handler所有权、disk-full、archive逃逸、WebGL持久性。

- [ ] HTTP桩覆206/200忽略Range/ETag改变/416/断连/404/429/5xx/noHEAD/坏hash/错误签名/越界URI/大文件/disk不足；结果可恢复、旧版本可继续真实加载。
- [ ] 全部producer/waiter取消与Play退出后handler/native清理；下载内存不随Bundle整体体积线性分配byte[]；重试次数有界。
- [ ] archive开启时恶意条目/配额/部分解压失败零发布；sign私钥外部提供、产物不含私钥，IL2CPP/目标平台不支持时不能声明production远端可用。
- [ ] 有效旧签名但低发布序号、同序不同digest、错误channel/平台/app范围均拒绝；相同发布幂等，失败激活不推进门槛，跨重启journal恢复及显式回滚不降低它。记录首次安装/状态清除/合法发布冻结的保护限制，不用签名通过冒充“最新版本已证明”。
- [ ] 实际download -> verify -> prepared -> safe activate -> load asset/instance/scene -> release -> rollback闭环，而非只测试hash函数。

### 8.9 开发体验、图片组件及测量驱动优化

**主要范围**：Resource工作台、Diagnostics、codegen体验；图片请求/组件与explicit组预加载；真正收益可证明的incremental/idle/priority调优。

**依赖/输出**：已有核心/更新闭环，吸收XF ImageLoader/项目窗信息架构而不用旧反射adapter系统；高价值诊断与构建报告正式可用。

**风险**：把工具变成第二state authority、堆扩展点、两层cache叠加、毫无基线的“性能优化”。

- [ ] A/B同图共享、A关闭B仍完成、刷新旧结果不覆盖、UI disable/destroy不遗留lease；可选SpriteAtlas显式入map并真实取得Sprite。
- [ ] 依赖/owner报告解释未卸载原因，关闭诊断无高频分配；Undo与窗口queue取消不修改未知资产。
- [ ] 用第11节同输入指标裁定实际优化；任何未实现可选项明确不支持，不能靠空接口冒充完成。

### 8.10 总体收口与阶段9前置条件

**主要范围**：本阶段有影响的Resource/Core/Audio/Editor边界及模块使用说明；授权范围内原游戏只读冒烟；不实施独立载体迁移。

**依赖/输出**：所有必需/缺陷与纳入高价值能力完成，淘汰列表收口，未纳入可选项有明确边界，正式SourceClosure/许可证/产物/操作入口可交接。

- [ ] YL Runtime/Editor源码闭包仅允许Unity及已明确依赖，不含原XFAB/XF/XFFSM；公开API及Editor入口不是原Package转发；可选leaf未进默认导出。
- [ ] 自有构建 -> manifest/catalog -> Resources/AB/instance/scene/audio/Core消费 -> download/update -> unload/rollback真实流程；分别记录编译、行为、物理资源、完整流程和Player结果。
- [ ] 对本系统owned native差集和用户/原MainModule文件保护复核；授权原游戏冒烟仅验证原链并继续Audio直Clip，不重建/保存游戏资产。
- [ ] 必要主目标Windows64小Player/持久缓存重启验证；其他平台有实测才标“支持”。阶段9独立工程创建、导入及最终独立运行不在本批执行。

## 11. 验证策略与优化基线

### 11.1 最小充分验证，不重跑全部历史

- **静态/辅助编译**：CoreArchitectureBoundaryTests、FoundationBoundaryTests、主体源码无XF source closure、Editor专用隔离、CodeGeneration编译、Player分支静态检查。辅助编译不能替代Unity compile/Player。
- **EditMode**：catalog/schema/identity/DAG、build map/分包、路径/归属/签名/事务故障注入、生成碰撞/篡改。改造现有ResourceEditor/OutputSafety/CodeGeneration/CodeCompilation/BuildIntegration中的必要断言，移除“旧文件名必须永存”实现耦合，不削弱保护语义。
- **PlayMode**：独立owned夹具验证真正Bundle/SubAsset/Instance/Scene/Audio/Core生命周期、native差集、取消/重入/迟到、会话退出。不得只以fake source全部绿宣称资源真实可释放。
- **网络/磁盘**：Tests拥有的小本地HTTP server、owned临时缓存和故障点；server不进Runtime。强制进程恢复仅在隔离授权环境，不能杀用户正在工作的Unity，不能启动第二个同项目实例。
- **平台**：Windows64是当前真实验收主基线；Android jar提取、iOS只读内置、WebGL异步+持久文件同步和各桌面平台有设计分支与针对性门禁。没有模块/设备时记录阻塞，不把编辑器模拟写成已验证支持；正式承诺平台须补真实构建/运行。
- **回归范围**：默认只执行本批新增/迁移Resource及直接受影响Core/Audio/Pool/Async/Editor测试；若改Foundation通知/会话能力才追加该能力相关套件。没有依据不重跑1-7/Tidy全历史总套件。

Unity若有dirty scene，不能自动保存或覆盖以启动测试；先停测试并报告最小条件。Unity未连接时不得宣称已刷新/编译，MCP失败可继续静态研究但不能累积基于假运行状态的改动。

### 11.2 可测基线，不预造百分比

| 场景 | 记录指标 | 通过要求/优化判断 |
|---|---|---|
| 同key 1/10/100等待者、共享菱形依赖 | native LoadFromFile/LoadAsset次数、distinct producer、P50/P95完成帧、allocation | 同snapshot同Bundle key或同asset实际加载类型key至多一次生产；alias归一；不同类型合法投影分别验证，A取消不伤其他等待者 |
| repeated acquire/release + instance reuse | owned native包数、活跃/idle实例数、lease/pin数、GC、Unity native memory采样 | 真实销毁后回本轮owned基线；缓存有界，不能用UnusedAssets全局扫来掩盖账本泄漏 |
| clean/no-change/单资产变更构建 | BuildPipeline/hash/分包/复制分别耗时，hash/复制字节，changed bundle数 | unchanged制品digest稳定；增量优化是否有效看真实时间/IO，不凭采用cache接口判定 |
| 普通下载 vs Range恢复 vs archive | 吞吐、重传字节、peak managed memory、retry、准备/激活时间 | stream内存不随总Bundle字节线性增长；范围一致性/恢复正确优先于吞吐 |
| 长会话/退出/重启/场景切换 | pending/consumer/producer/handler/owned文件与epoch | 无旧epoch反写、无late创建宿主、无原目录改动，active始终是一份完整快照 |

XFAB/YL行为对照优先同平台、同输入、同压缩、同冷/热条件的自有夹具；不得改原MainModule为benchmark输入。做不到公平旧实现运行对照时仅报告结构性IO/所有权改进及新系统实测，不宣称已快多少。Profiler之外的压缩大小不是native内存证明。

## 12. 用户审核决策与阶段9条件

### 12.1 需要确认的重大取舍

以下每项都给出具体推荐值，实施不再由代理临时猜测。用户改变推荐时，应先修订受影响设计及门禁，而不是加永久兼容壳。

**本次审核状态**：**总体方案已批准（APPROVED）** — 架构B、D1～D8总体方向、十个主批次及独立子批次、XF主体/YL优秀成果、统一生命周期和双主体分层均已批准。总体方向批准不等于逐项实施批准：具体公共API、Schema最终字段、资源所有权实现和重大迁移仍在对应实施批次单独审查；D2的持久化字段定稿由8.2闭环门禁决定，D6的生产远端与最小防重放策略由8.8门禁验收。8.1已获单独正式授权，实施事实见第14节，其余既定设计不重开研究。

| 决策 | 推荐 | 影响 |
|---|---|---|
| D1 主体/取舍 | 候选B方向已批准：XFAB领域体系+自有租约内核；第9节具体淘汰清单/替代API迁移在对应批次审查 | 不以旧类/历史测试全部保留为验收；正式替代及consumer迁移通过后再退出旧实现 |
| D2 正式格式/地址 | 新versioned catalog、稳定ProjectId+明确address/subasset；8.1最小模型，8.2真实闭环后冻结持久化Schema；不保证XF旧映射或YL BinaryFormatter读取 | 新地址/兼容取舍在对应批次审查；字段定稿以8.2证据为据，原游戏不迁移，仓库外产物不默认兼容 |
| D3 Core跨层与ProjectName | 单Core消费Provider由Resource实现；保留属性名字，切为新YL ProjectId；无默认XFAB回退 | Core source closure不导入Systems/XFAB，生成/preview/help及YL fixtures迁移 |
| D4 实例/场景语义 | prefab由Resource保活、复用Foundation桶；Core默认scene-scoped；Close回缓存；scene不随ModuleClose卸载 | 改掉XFAB池依赖但保留Core重要行为；live/DDOL/晚归还须真正结算，不只是空计数 |
| D5 Resources所有权 | 共享borrowed默认drop引用，不按自有lease零值调用UnloadAsset | 比旧Resources物理卸载测试更保守；原游戏/直Clip安全，明确维护边界才做全局UnusedAssets |
| D6 版本更新/真实性 | 8.2/8.7仅本地/内部受控能力；8.8门禁后开放HTTPS+签名、SHA256、恢复/回滚及最小发布序号策略 | 激活/回滚、安全及防重放限制在8.7/8.8门禁验收；签名不保证最新，不承诺native热共存，不建云发布系统 |
| D7 编译边界 | 本轮明确继续保留Runtime/Editor双主体与Foundation/Core分层；强化真实闭包，可选XFAB leaf不进默认导出 | 闭包/可选leaf验收细则在对应批次授权；未来DLL或AQN边界变更仍须专项批准 |
| D8 扩展范围 | 必需/缺陷和列入批次的高价值项纳入；archive/跨项目/图集按明确条件启用；历史兼容/DES/二进制差分/热共存暂缓 | 不预建扩展空接口；平台承诺以真实门禁为准，不把阶段9独立载体提前做掉 |

这是一份已批准的总体方案：D1～D8总体取舍及8.1～8.10路线已批准，不再笼统视为待批准。总体批准不等于逐项实施批准，具体公共API、Schema最终字段、资源所有权实现和重大迁移仍在对应实施批次单独审查，不得因总体批准提前实施；任何实施批次仍须用户明确授权。

### 12.2 阶段9前必须达到

1. YL Resource Runtime与Editor不用原XFAB Package即可源码闭包编译；Core/Audio正式默认走自有链，可选外部adapter隔离。Json.NET目前由XFAB传递安装（`com.unity.nuget.newtonsoft-json@3.2.1`，内容13.0.2），应在交付依赖清单明确为直接消费要求；Unity包外壳UCL与其Third Party Notices内MIT组件声明分别遵守，不能把整个UPM包概括为MIT或不带声明直接复制DLL。本项目Packages本轮及阶段8保护范围内不改。
2. 新目录/构建/发布/更新/回滚/缓存闭环可用，真实fixture资源能加载、实例与scene能安全持续使用及释放，不靠原MainModule运行才成立。
3. 不再有新旧YL竞争Manager、legacy格式unsafe回退、placeholder更新、公开pooled mutable资源身份；保留机制有真正consumer或明确通用价值。
4. 所有新资源/脚本GUID、序列化类型、AQN、Generated asmdef/常量地址与原XF链独立；自有artifact不包含指向原XF脚本的不可运行Prefab/Scene。构建成功不等于Bundle内脚本身份可迁移。
5. 资源使用/释放/配置/初始化/错误/激活边界及已测平台有实际入口和简明模块说明；重要API XML按实现同步，不能将未测试平台写成已支持。
6. 源码来源/许可可交付：XFAB MIT归属`Copyright (c) 2021 弦小风`及对应完整许可证随附；融合片段来源revision集中进入既有Third-Party Notices（仅未来批准实施时更新），不机械给每文件加模板。**不复制原包不明版本/许可的SharpZipLib.dll**；复用已有自有ZipTool与已核实许可源码。
7. 具备第11节实际验收证据和明确剩余条件；阶段9再进行独立工程/载体创建、依赖安装、导入及运行/Player最终验收。源码编译、真实加载、安全释放、完整流程、独立交付五个层级分别成立，不能互相替代。

## 13. 外部语义依据与研究审查结论

- Unity 2022.3 `AssetBundle.Unload`：true会销毁已加载对象；false保留对象却断开Bundle关联，后续重载可能产生重复对象/依赖错配，不能代替正确实例保活。<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.Unload.html>
- Unity 2022.3 AssetBundle依赖：Unity不会自动加载依赖；依赖必须在LoadAsset前备齐。<https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundles-Dependencies.html>
- Unity 2022.3 `LoadSceneAsync`及`allowSceneActivation`：scene没有取消API；0.9暂停激活会堵住async队列。<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html>、<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html>
- Unity 2022.3 BuildPipeline及AssetBundleBuild：显式map支持addressableNames；manifest Bundle取输出目录名；平台制品不能混用。采用本项目已使用的稳定overload，不照搬文档后来出现的新参数结构。<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/BuildPipeline.BuildAssetBundles.html>、<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundleBuild.html>
- Unity 2022.3 DownloadHandlerFile(path, append)：可流式写文件，append只是拼接能力，不自动验证Range语义。<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.DownloadHandlerFile-ctor.html>
- Unity 2022.3 Resources.UnloadAsset：对象会全局失效，随后重新载入的对象与旧引用没有关联。<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Resources.UnloadAsset.html>
- Microsoft BinaryFormatter安全说明：反序列化不可信输入无法通过小修补成为安全协议。<https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide>

独立架构审查已促成四项实质裁定：不承诺native多版本热共存；检查不能删旧文件；持久增量workspace与交付staging分离；Resources零自有引用不意味着外部未持有。最终定向复审还补齐了自动manifest的项目化native名、persistent版本/指针及内置回退、catalog Group权威、yield/TakeResult、创建/场景前置pin和可拒收子批次。源码回查修正了阶段7标签、Editor实际依赖和旧数据表的精确缺陷位置。所有约束均进入合同/路线/门禁，审查不构成用户实施批准。

**总体方案研究结束记录**：本文件已完成源码与架构交叉审查、引用及工作区保护复核，并经用户审核批准（APPROVED）；原批准轮次仅完成方案状态更新与 Git 准备，不开始8.1、不迁移产品资源、不做阶段9独立载体。后续8.1已获正式实施授权，实际交付及停止边界见第14节。

## 14. 8.1 实施与验收证据（2026-10-14）

### 14.1 实际范围与基线

- **8.1 COMPLETE**：核心、内存 Catalog、Resources source、Core 窄合同与定向测试已实现，必要 Unity 验证及一次独立 Review 的修复门禁已通过；交付后停止，不实施 8.2。
- 开始时本地 `fusion/8-resource`、HEAD、远端实施分支和 `integration/xf-yl` 均为 `0e71c8b8cafefc807c076b7476bb23e8093b72b0`。本批仅收口到 `fusion/8-resource`，不创建里程碑、不合并集成分支；实际 Commit/Push 标识由本轮完成报告和 Git 记录提供。
- Runtime 新代码位于 `Systems/Resource` 的 `Catalog`、`Loading`、`Lifetime`、`Sources` 及 `ResourceSystem` / `ResourceConfiguration`；没有第二个 Manager、独立加载表、Resource DLL 或永久兼容转发层。
- `Core/ResourceAccess/ICoreResourceProvider.cs` 建立窄 Request/prefab/instance/Scene 合同及显式绑定槽，只依赖 Unity/Foundation。8.1 不提供 Core provider 后端，不改 Module/View/Panel 的 XFAB 正式流、Audio 的旧 ResLoader、`ProjectName` 实际语义或旧 YL Resource。
- 新增 17 个 C# 文件均由 Unity 导入生成独立 `.meta`，AssetDatabase 的 MonoScript/GUID 定向核对无缺失或冲突。TextAsset 与 Sprite PNG 夹具仅在新增测试 Resources 目录中使用，不交给旧 ResLoader。原 Package、游戏、Scene、Prefab、Bundle、Packages、ProjectSettings 未纳入本批改动。

### 14.2 核心 API 与实际语义

- `ResourceSystem.InitializeAsync(config)` / `Current` / `IsAvailable` / `ShutdownAsync()`：显式 Play/Player 主线程入口，读取 Current 不创建宿主。内存验证允许即时完成；同一只读配置对象可重入 join，不同配置须先关闭旧权威。失败无半就绪 Current，可用合法配置重试。
- `ResourceAddress`、`ResourceSourceId`、`ResourceLocation`、`ResourceCatalogEntry`、`ResourceDependencyNode`、`ResourceCatalog`：不可变内存身份/加载描述/快照，精确 ordinal 解析显式 alias；验证 canonical 重复、大小写冲突、项目/source、编译后的 Unity 类型、有界节点/边、缺失依赖与真实环路径。没有持久化解析器、编码或磁盘 Schema 冻结，仍等待 8.2 真实构建与读回闭环。
- `Acquire<T>` / `AcquireAsync<T>` 返回独立 `AssetLease<T>` 或 `ResourceRequest<AssetLease<T>>`。producer 按 Catalog 快照、canonical assetId 和目录声明的实际加载类型共享；当前基类请求只是可赋值投影，`Object` / `Sprite` 消费同一声明 Sprite 时共享一个 producer。native filter 使用目录声明类型，不以模糊地址或宽泛类型回退。
- Request 观察 `Status` / `IsDone` / `Progress` / `Error` / `Completed`，支持 yield、Cancel、Dispose；没有自动领取 await。成功前已建立独立 Lease，等待完成不拥有结果。`TakeResult()` 恰一次转移；未领取 Dispose 会释放，已领取 Request Dispose 不释放调用方 Lease。失败/取消领取抛资源错误，重复领取抛 `InvalidOperationException`，Dispose 后领取抛 `ObjectDisposedException`；通知异常逐个报告而不反写终态。
- `ResourceScope` 的 `Acquire` / `AcquireAsync` / `Track` / `Own` / `TakeResult` 只聚合接管责任，以引用身份去重；Dispose 先移出自身记账再释放所有自有请求/Lease，不损害外部消费者。公开 Request/Lease 不池化，不靠 finalizer 释放。
- 正常 Shutdown 若还有合法等待者或 Lease（含未领取成功结果）返回 Busy，服务保持可用；零消费者的原生 producer 排空后关闭。退出先整批冻结 Pending 为 Invalidated、解绑等待、结算未领取结果/退役记录，再通知；重入 Cancel 或 Scope.Dispose 不改变兄弟退出终态。旧会话未完成 producer 保持 cleanup owner，并阻止同域新初始化直到迟到清理完成。
- Resources 使用真实同步/异步 Unity API；原生 `completed` 独立观察，不依赖协程宿主。归零只丢自身记录和引用，不调用 `Resources.UnloadAsset`、Destroy 或 `UnloadUnusedAssets`，不宣称 native memory 已物理释放。显式子资产 key 与显式依赖执行尚不支持，校验会明确拒绝。

### 14.3 Unity 验证与独立 Review

在已打开的 Unity `2022.3.62f3c1` 中实际执行，未启动第二个实例、未修改 Domain Reload 设置。按短类名或 `YLFramework.Tests.Resource` namespace 定向筛选，统计实际 Passed/Failed，不使用发现树 TotalTests 冒充执行数。

| 门禁 | 模式 | 实际通过 |
|---|---|---:|
| `ResourceCatalogTests` | EditMode | 10 |
| `ResourceRequestTests` | EditMode | 5 |
| `ResourceSessionSafetyTests` | EditMode，dirty 无路径场景保护 | 2 |
| `ResourceSessionTests` | EditMode 驱动真实双 EnterPlay/ExitPlay | 1 |
| `ResourceSystemTests` | PlayMode，可控源/所有权/重入/迟到 | 21 |
| `ResourcesSourceTests` | PlayMode，真实 TextAsset 与 Sprite/Texture | 6 |
| `CoreArchitectureBoundaryTests` | Core 完整 Editor/Development 闭包 | 3 |
| `FoundationBoundaryTests` | Foundation 闭包及身份/friend 门禁 | 9 |
| **合计** | **零失败、零跳过** | **57** |

- 修复后重跑 Resource PlayMode 27 项、Request 5 项、真实双 Play 1 项及安全守卫 2 项；Catalog 10 项、Core/Foundation 12 项采用本批已执行且未受后续 Systems 内部修复影响的有效结果。真实双 Play 的工具耗时字段跨域不可靠，不用于性能结论。
- 一次新上下文只读 Review 确认两项 P2：基类请求丢失 native 类型消歧、逐个退出通知使兄弟 Pending 可被重入改为 Canceled。已用真实 Sprite/Texture 同 locator 和互 Cancel/Scope.Dispose 退出用例复现，修复后完整定向回归通过。未确认 P0/P1，不另做重复形式审查。
- Scope 值相等对象漏记责任与 fixture dirty 无路径漏验也建立红灯后修复：引用身份聚合、检查实际 open scenes；允许 Runner 的 clean 空场景，拒绝 dirty/有内容且不可恢复的场景，不自动保存用户场景。原“标准 Runner 丢失用户原 dirty 场景”的 P1 推断经 Test Framework 1.3.9 源码核对撤回，不作为事实。
- 另复现并修复迟到多播观察者异常跳过其他观察者，以及 producer 清理后失败进度回退到零。状态先提交、生产责任先结算，再通知；失败进度保留 producer 终态样本。
- 未运行阶段 1～7/Tidy 历史全量、Player 重建、资源导出或完整 XFAB 套件。Canvas 旧整文件等价断言不纳入本批，不修改 Canvas 或降低其断言。预期观察者异常由 LogAssert 验证，不描述为未处理错误。

### 14.4 保护状态与后续基础

四个受保护文件在开始、实施及审查修复后重复 `git hash-object`，身份一致，未修正、暂存、还原或更新其基线：

| 文件 | 开始与收口前一致的 blob identity |
|---|---|
| `.gitignore` | `a14b361253c8210930577bcc1816c8008d1b68fb` |
| `.kilo/kilo.jsonc` | `e943dde58693de12fbe7c00caa5e4545b7d10412` |
| ProBuilder `Settings.json` | `59b16de7964ef4f16b0e333554796396b9747b21` |
| Core `Canvas.prefab` | `c6b7d124a5b70f320748fcedc04467599e258a25` |

8.2 可依赖实测的单权威、不可变内存 Catalog、producer/consumer 分离、独立 Lease/Scope、单次交割及主线程会话门禁。8.1 收口时尚无持久化 Catalog/BuildMap、AssetBundle、实例池、Scene pin、Editor build/update 或生产网络安全能力；8.2a 的新增构建能力见第15节。Core 窄合同仍无正式后端，不将子批次完成理解为阶段 8 整体完成、原消费者已迁移或生产分发已成立。

## 15. 8.2a 技术验收与 Git 收口（2026-10-15）

### 15.1 基线、实现与边界

- 开始 HEAD 为 `1666213882f4d0b6626db9fef6911cdabd7845f5`；开始只读 `git ls-remote origin refs/heads/fusion/8-resource` 确认远端同值，未 fetch/pull。技术验收通过后按后续授权完成 Commit（`8910345` `feat(resource): 完成8.2a资源分包与增量构建基础`，仅含本批17个文件）与 Push，推送后远端核对同值；未 rebase/merge/force。既有四项用户改动保留，未进入提交，收口 blob identity 与第14.4节一致。
- 在现有 `YLFramework.Editor` 主体新增 `Data/ResourceProject.cs` 与 `Build/ResourceBuildMap.cs`、`ResourceBuildWorkspace.cs`、`ResourceBundleBuilder.cs`。不新建程序集，Runtime/Foundation/Core/Audio 源码、8.1 Request/Lease/Scope 与 `ProjectName` 不变；旧 YL Resource 仍保留，不调用 XF/XFAB 构建入口。
- Project 使用已保存的 SO GUID、独立稳定 `ProjectId`、资源根 GUID；Group 显式选择文件/递归文件夹 GUID，可筛选扩展名，支持 Together/PerAsset。地址为项目根相对规范路径或显式覆写；检测大小写地址/路径冲突、重复选择、项目身份及根重叠。记录主资产 GUID/localId、实际运行时读回类型、Group 成员及最终 Bundle 归属，不提前定义正式持久化 Catalog 或子资产 Schema。
- 真实直接依赖图驱动分包：普通私有依赖归所属包，共享依赖按排序后的依赖者集合与资产 GUID 聚合；Scene 私有普通依赖进入独立普通 `d_` 桶；合法资源互引引起的 Bundle SCC 归并后复验 DAG，每个最终包拒绝 Scene/普通资产混装。无历史 1MB/10资产阈值，不使用旧全局 Importer 标记。
- Unity 不允许向 `Library` 输出 AssetBundle，故 Workspace 使用 `AssetBundles/YLR_<ProjectGuid>_<SettingsSHA256前32hex>/ylr_<ProjectToken>_<Platform>`；只管理该新 owned 根，不接管或清理 `AssetBundles` 父目录、`MainModule` 或未知目录。Marker 比较完整配置 SHA-256、工程根、Project GUID/ID 与叶名；持久文件独占锁，登记可能产物后才调用 native build，失败/取消仅使本 Workspace 无效，不删除部分或过时登记产物。
- 构建入口为 `ResourceBundleBuilder.Build(project, activeTarget, forceRebuild, cancellation)`；API 仅支持与 Editor 主机匹配的已安装、已活动桌面目标，本批实际验证 Windows64。固定 LZ4/StrictMode，拒绝后台线程、Play、dirty scene、重入、路径链接及任何外部已加载 Bundle，不自动保存用户场景或切平台。实测 native build 会卸载外部 Bundle，故不能只检查同 namespace 冲突。取消在同步 native build 前后/验证检查点生效，不承诺中途中断 Unity BuildPipeline。

### 15.2 真实产物、身份与增量记录

- 自有隔离夹具包含 Text、两个共用 Texture 的 Prefab、AnimatorController、带私有 Material/Texture 的非空 Scene。最终 **6 个资源 Bundle + 1 个根 Manifest Bundle**，逐包 `.manifest` 一并保存；实际 Manifest 包列表、直接依赖、Bundle native name、资源/Scene 路径与主资产类型读回均与 BuildMap 一致。共享 Texture 只显式归属一个共享包，两个 Prefab 实际读回同一 native Texture；修改 Text 后只有其资源包 SHA-256 变化，其余资源包摘要保持一致；损坏产物后强制重建恢复原摘要。
- 自动根 Manifest 的 `Object.name`/`m_AssetBundleName` 为空。已由用户明确批准第10节门禁修正，完整项目命名空间/摘要/实际读回作为证据；两个独立保存的 ResourceProject 先各自构建再同时加载根 Manifest 成功，同一产物复制改名仍被 Unity 拒绝。不宣称输出叶名等于根 Manifest native 身份。
- 输入指纹包含资源原始字节/meta/Unity dependency hash、地址/类型/归属/Group、Project 配置、Unity/目标/选项、ProjectSettings、包声明/锁、实际编译源/defines/程序集依赖、asmdef/asmref 与预编译引用字节。产物复用须匹配完整 SHA-256 与已验证文件集合，不能只比较 mtime/length。
- 未能冻结全部输入的包/用户 Scene/Shader 构建回调会保守禁止整体 skip，并使用 ForceRebuild。当前工程检测到 **6 项**此类回调，因此最终无变更也确实调用 native build；没有最终缓存命中或整体性能提升结论。早期尚未补全门禁时的 skip 测量仅属探索，不沿用为最终优化证据。

| 最终同一夹具 | 预检 ms | Unity 构建 ms | 验证 ms | 输入哈希字节 | 产物哈希字节 | 复制字节 |
|---|---:|---:|---:|---:|---:|---:|
| 首次 | 11653.23 | 2802.70 | 11762.80 | 232383058 | 81868 | 0 |
| 无变更 | 11292.86 | 2154.73 | 11611.07 | 232383058 | 163736 | 0 |
| Text 实际变更 | 11523.45 | 2201.26 | 11383.84 | 232383096 | 163749 | 0 |
| 产物损坏后修复 | 11576.50 | 2135.76 | 11630.87 | 232383096 | 135637 | 0 |

计数为本内核实际文件读取/复制量，不代表 Unity 内部 IO；无 staging 发布复制，所以四次均为0。未冻结回调输入时宁可失去增量收益也不返回过时结果；哈希预检/复验目前占主要耗时，未做无证据的性能承诺。

### 15.3 测试、Review 与保护证据

- 最终 Unity EditMode 定向 **23/23 Passed，0 Failed，0 Skipped**：BuildMap 10项、Builder 13项；Runner `RunFinished` 的实际执行汇总23项，601.21秒。包含非空 Scene/Controller 回归、共享身份、双项目 Manifest、compiled DLL/asmdef 输入变更、未冻结回调缓存门禁、真实 source-change/cancel/recovery，以及 null/throw、取消前零目录、独占锁、重入、unknown目录/文件、owner冲突、junction、错误平台/后台线程保护。
- CLI 的600秒等待早于 Runner 完成而超时，不据此推断通过。额外只读/记录回调保存逐项结果，完成后实际读取23条 Passed及Runner汇总；本机证据为 `C:/Users/Kano/AppData/Local/Temp/kilo/resource82a-20261015-final.jsonl`、`resource82a-20261015-final-summary.json`、`resource82a-20261015-final-metrics.txt`。不将工具库测试树总数1592误写成实际执行数。
- 本批另实际通过 Catalog 10项、Core Runtime identity 11项、Foundation owner/friend 1项、Core Editor identity 1项、Resource 固定路径 1项、Export 哨兵 1项。后续只修改独立 Editor 构建实现及许可文字，不修改这些已验证的下层合同；沿用其本批有效结果，不重复8.1完整 PlayMode验收。
- 一次独立新上下文只读技术 Review 确认2项P1（指纹遗漏编译/回调输入、非空Scene私有普通依赖混装）及1项P2（误拒AnimatorController）。各自建立失败回归后修复，最终23项同批回归全部通过；不以作者自查替代独立Review，不添加第二轮形式审查。
- 每个真实构建闭环对 `Assets/Games`、`Assets/Scenes`、`Assets/StreamingAssets`、`AssetBundles/MainModule`、`Packages`、`ProjectSettings` 进行前后逐文件 SHA-256 对照并通过。Git 差异确认原资源/包/Runtime未新增产品修改；四项既有用户改动指纹不变，新增6份脚本meta由Unity生成并核对，未移动旧资源/GUID。中断遗留的单个自有夹具按Project GUID/根GUID/准确已知文件集合复核后定向清理，未清理未知资产或历史Bundle。
- 唯一旧产品代码差异是 `ResourceBuildOperation.RejectLinks` 从 private 改 internal，供新内核复用既有路径保护；不调用旧 Execute/发布事务。固定路径及 Export 哨兵同步；`YLFramework/LICENSE.md` 保留上游版权及完整MIT许可并补充实际XFAB来源。

真实验证产物保留在 `AssetBundles/YLR_085459f03b27a434e88698868ee5a4d9_8289def5b7b4d3ebe73eff8a26e2b41f/ylr_915a9fb65487d34c786e2450c5d31d17_StandaloneWindows64`，marker为Complete=true并记录完整输入及各产物SHA-256。夹具源码/config按测试生命周期清理，保留产物仅供本机验证/调试，不是正式可消费发布目录。

### 15.4 停止点

8.2a 自有资源定义、稳定显式 BuildMap、实际依赖分包与 owned 隔离构建闭环技术通过。未实施正式发布/事务回滚/持久化Catalog、完整工作台/生成器、8.3 Runtime AssetBundle加载，也未完成子资产正式描述、Scene运行切换、Player或非本机平台验收。保留8.2总门禁中属于后续子批次的未完成项；Commit/Push 已按后续授权执行完毕（见15.1）。**到此停止，不自动进入8.2b。**

## 16. 8.2b 实际成果与技术验收（2026-10-15）

### 16.1 基线、入口与 Catalog 契约

- 实施基线为 `fusion/8-resource` / `d7301fa2621390352a7619077abd9d50b095a1e2`。本轮仅授权实现、验证、独立 Review 和更新本 Plan；未创建或切换分支，未 Commit/Push。
- 在现有 Runtime `Catalog/` 增加 `ResourceCatalogDocument`、`ResourceCatalogCodec`、`ResourceReleaseIndex`；Editor 增加 `Build/ResourceCatalogBuilder` 与 `Publishing/ResourcePublisher`。没有新增程序集，不修改 8.1 Request/Lease/Scope、Core/Audio 或旧 YL Runtime，不读取旧 ResData 或调用原 XF/XFAB 构建流程。
- 使用入口为 `ResourceBundleBuilder.Build(...)` 取得真实已验证结果，再调用 `ResourcePublisher.Publish(result, appContentCompatibilityId, cancellation)`；单独目录生成可用 `ResourceCatalogBuilder.Create(result, appContentCompatibilityId)`，返回不可变文档。`GetReleaseRoot(result)` 只定位预期输出；`Recover(project, target, appContentCompatibilityId)` 显式处理 owned 本地未完事务。
- 生成前独占锁住可变 Workspace，核对归属、Complete、输入指纹、完整产物集合与逐文件长度/SHA-256。旧 result 不能悄悄改指向 Workspace 新一代产物；工作区损坏不能成为有效发布。
- 交付 `resource-catalog.json` 使用可实际读写的 **SchemaVersion=1 验证版**：固定字段；对象属性按 ordinal 排序，Bundles/Assets/Groups/SupportFiles 和依赖、成员按稳定 identity 排序；InvariantCulture、无 BOM/空白/尾换行的 UTF-8、EscapeNonAscii。BuildId 为不含自身字段的规范 payload SHA-256，CatalogDigest 为最终实际 catalog bytes SHA-256，二者不自引用。最终 Schema 冻结仍留到 8.2c 的生成与消费闭环，不增加旧原型永久兼容层。
- codec 在 Runtime 主体但不启动或挂载另一个 ResourceSystem；不按 AQN/类型字符串反射创建对象。严格限制 16 MiB、32 层 JSON 深度及各集合16384条，拒绝缺失/未知字段、重复键、非法 UTF-8、非规范字节、错 schema/摘要/路径、大小写地址/Group冲突、身份冲突、缺包/非法 DAG 和错误平台/Unity/App兼容值。固定 `JObject` 字段读取无 CLR 类型反序列化，等价于 `TypeNameHandling.None`；Editor metadata 显式使用该设置。
- Catalog 保留 GUID/localId canonical AssetId、main/Scene/子资产加载描述、Bundle 与直接依赖、Group 成员及 Required；一 GUID 只能有一个 main descriptor。公开子资产来自真实 AssetDatabase representations，并在 Bundle 内以唯一 name+type 读回；SubAssetKey 为 localId 的十进制文本，不把 localId 直接传给 native LoadAsset。同文件同 name+type 歧义拒绝。
- Release Index 只含 schema/project/platform/Unity/app、BuildId、CatalogDigest 与 `<digest>/resource-catalog.json` 定位，不重复资产/Group表，不承担 Runtime 激活或生产签名授权。读者须提供显式预期兼容值，AppContentCompatibilityId 精确匹配，不能只凭平台/Unity推断 Player 内容兼容。

### 16.2 发布归属、提交及恢复保证

- 输出限定 `AssetBundles/YLFramework/<ProjectId>/<Platform>/<CatalogDigest>/`；发布 root 的 `.ylr-release-owner.json` 精确绑定工程根、Project SO GUID/ID、平台和已登记版本。未知叶（即使空目录）、文件占位、归属冲突、非法路径/保留名、junction/reparse point、未登记文件/目录一律拒绝；不接管旧 YL/XF 输出、原 StreamingAssets 或历史版本。
- `.ylr-release.lock` 保护跨窗口/进程访问；同卷 `.staging/<attemptId>/` 与可变构建 Workspace 分开。单 journal 在任何 staging 写入前登记 From/To、attempt 与允许文件集合。产物复制、catalog 写入后逐文件 hash/length 校验，重新 native 读回 Manifest、Bundle 名/依赖/UnityHash/资源路径/main类型/子资产，再写 Complete。新版本登记后以 `Directory.Move` 安装；最后才原子 `File.Replace`/首次 `File.Move` 提交 Index。
- Windows Unity Mono 实测：长路径文件可以被枚举且 `File.Exists=true`，但超过260字符的 `File.OpenRead` 会失败。交付普通物理文件使用 `b_<nativeName SHA256前32hex>`、根 Manifest 使用 `manifest`，同时保留实际 `ylr_` native identity；`.manifest` 调试文件仍列入支持产物摘要。发布前检查目标路径长度并拒绝过长根/ID，不改系统设置、不冒充 native 多版本隔离。
- 完整版本只新增，不原地覆盖，也不自动删除已安装版本。完全相同的当前 CatalogDigest 为幂等重复发布，Index/版本字节不变且复制字节为0；同摘要已安装但未索引的完整 owned 版本可验证后复用。坏文件、不同归属或不完整目录不会被覆盖成“有效”。目前明确上限为每 root 1024条版本登记，达到上限拒绝继续发布，没有隐式 GC。
- 失败/取消只在 Index 提交前中止，保留原 Index 和所有旧版本；部分 staging 和 journal 留作可恢复证据，不在 catch/finally 里连串删目录或让回滚异常覆盖原始首因。`Publish` 开始会先恢复既有 owned journal；显式 `Recover` 也可只靠磁盘记录重建，不依赖上次内存状态。
- 恢复只接受 actual Index 等于 journal From 或 To，且当前引用快照完整可验证：From 表示中止新尝试；To 表示确认已提交；两者均不匹配则报 stale/conflict。只清理该 attempt 登记过的 partial staging，不清未知内容，不挑“最新目录”，已安装但未提交的完整版本也保留。清理遇文件锁等 IO 故障保留 journal 可再次重试。
- 提交点之后的异常不宣称撤销成功，也不因晚到 cancellation 降级 Index；可能出现调用结果未定，由 `Recover` 按实际 Index 确认。丢失/损坏 owner、journal、Index 或 Complete 时保守拒绝，不自动猜测/修补归属；完整快照仍保留供诊断。目录切换与 pointer 不是跨目录断电级原子，不声称已做真实杀进程/断电验证。
- 提交前和恢复清理前重新读磁盘 owner 并核对持锁身份及完整版本登记，实际 journal 必须与本 attempt 授权内容一致；不能使用缓存 Owner 在 marker/journal 被删除或替换后继续提交/误删。

### 16.3 真实发布与测试证据

- 最终 Unity 2022.3.62f3c1 / StandaloneWindows64 EditMode 本批 **16/16 Passed、0 Failed、0 Skipped**：Codec/Index 11项、真实 Catalog 1项、Publishing 4项。Publishing 最终4项279.73秒，Codec 0.64秒，真实 Catalog 48.23秒。MCP Summary 的 `TotalTests=1608` 是测试树数量，不是实际执行数；上述数量依据真实逐项结果与 PassedTests。
- 真实夹具使用 Text、两个共享 Texture 的 Prefab、双 Sprite 子资产、带普通依赖的非空 Scene。发布为 **7个资源 Bundle + 1个根 Manifest Bundle**，9份支持文件、7个 Catalog Asset（含2个 Sprite子资产）、5个 Group。实际 Manifest 包集合/DAG/hash、主资产 GUID/localId/type、Scene完整路径、子资产唯一 name+type、Group成员及各文件摘要均读回一致；产物不是模拟文件。
- 连续两代真实 Bundle 发布后，分别从已安装版本读取 Text 得到第一代/第二代实际内容。输入无变化第三次真实 build 得到同 CatalogDigest，仍检测6项未冻结回调并强制 native rebuild，不返回缓存命中；重复 Publish 不复制、不重写 Index。旧版本逐文件字节快照在取消、每个失败点及新版本成功后均保持不变。
- 真实故障包括 `FileMode.CreateNew` 目标冲突导致复制失败、Index 无 delete-share 文件锁导致真实 `File.Replace` 失败；另注入 StagingCreated/FileCopied/CatalogWritten/StageVerified/VersionInstalled/BeforeIndex 故障、Catalog 写入后的取消及 IndexCommitted 后中断。恢复分别确认旧/新实际 Index，清空登记 staging，不删除任何已安装版本。
- 负例包括未知 root/文件、独占锁、错 owner、junction、坏 Workspace/发布 Bundle、错误兼容/Schema、非法 catalog/index路径、摘要篡改、JSON大小/深度/重复/未知字段，以及 partial stage 新出现 foreign 文件。foreign 存在时恢复拒绝且逐文件快照不变；测试只删除自己制造的 foreign 后恢复，再次确认旧有效发布。
- 本轮另执行受影响的 `RealBundlesManifestSharedIdentityAndIncrementalChangesRoundTrip` 1项（169.79秒、4次真实构建、含 AnimatorController/非空Scene/shared identity/变更与修复），既有 Catalog身份/DAG 10项、Resource固定路径1项、默认Export实际哨兵1项，均 Passed。不重复历史23项/8.1全量、Core/Audio Player或无关模块；8.2a的 null/throw/平台/资源互引与分包门禁沿用有效证据，不重新验收整个8.2a。

| 本轮受影响 Builder 夹具 | 预检 ms | native构建 ms | 验证 ms | 输入哈希字节 | 产物哈希字节 | 构建复制字节 |
|---|---:|---:|---:|---:|---:|---:|
| 首次 | 13003.41 | 2180.73 | 12953.08 | 232589078 | 81527 | 0 |
| 无变化 | 12721.76 | 2149.40 | 12917.68 | 232589078 | 163054 | 0 |
| Text变更 | 12810.59 | 2182.03 | 13230.46 | 232589116 | 163068 | 0 |
| 损坏后修复 | 12863.56 | 2176.18 | 13905.68 | 232589116 | 134982 | 0 |

真实两代发布保留在 `AssetBundles/YLFramework/yl-release-75b0c703626c4386b8a29366ae3c0fdf/StandaloneWindows64/`。最后另做一次独立磁盘读回，逐文件复验两代全部长度/SHA-256，Index指向第二代、journal不存在、Staging为空、Unity scriptCompilationFailed=false：

| 版本 | BuildId | CatalogDigest | Catalog bytes | 制品 bytes |
|---|---|---|---:|---:|
| 第一代 | `33b6287f8bd0079f09408a831406fd067e4279a369f9844ecee048162b7e36b1` | `772f6af3967a1e405748b1b99a2518fc6a49bdb2dd9852f78ba9277e09279b8f` | 8541 | 82629 |
| 第二代 | `c0d8acb1caf2e3e6ce28425fcab86e2e4fdb2b175b11aa0f735d0f1297e849e9` | `8b2d00d17e4424c04054dd252de9ce0ee06fee993d5a6028ab46d89adc199b96` | 8541 | 82638 |

夹具源码/config按测试生命周期清理，保留发布是本机验证证据，不自动装配为游戏配置。其他此前本批生成的完整验证版本不做未经授权的清理。

### 16.4 独立 Review、工程保护与停止点

- 完成一次独立新上下文、只读的本批 Review，实际检查全部8份新源码、9份meta、3份tracked源码/测试差异、相关程序集与Plan契约；初判 Request Changes，发现P1提交前live owner/journal未复核及P2同GUID多个main可通过codec。两项均先在Unity观察对应回归失败，再最小修复并通过；P1另锁定 owner缺失/身份冲突、journal缺失/attempt替换四分支且旧Index不变。最终本批16项复验全部通过，没有遗留实质Review问题；未用作者自查替代独立审查，也未新增重复Review轮次。
- 每个真实夹具前后逐文件 SHA-256 对照保护 `Assets/Games`、`Assets/Scenes`、原 `Assets/StreamingAssets`、`AssetBundles/MainModule`、`Packages`、`ProjectSettings`，均通过。最终diff限定本批源码、测试和本Plan；原XF/XFAB/XFFSM包/游戏及旧发布不新增修改，9份meta均由Unity导入生成，新脚本GUID唯一、可加载，无dirty场景。
- `.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings、Core Canvas.prefab 的开始/最终 blob identity 与第14.4节相同，未编辑/暂存/回退。并行新增UI Plan在本轮期间外部更新，原样保留，不读取或修改其内容，不将它纳入本批。
- 8.2c可依赖真实已验证 BuildResult、不可变Catalog文档/严格codec、GUID/localId与子资产加载描述、Group canonical成员、完整版本目录、唯一Release Index及显式owned恢复入口。工作台、Editor模拟索引、常量生成/实际消费编译、旧Editor入口最终收口仍属8.2c；签名及远端安全留8.8，Runtime加载/激活与更新留8.3/8.7，未提前实现。
- 未做Player、非Windows平台、真实杀进程/断电或生产远端验证。不承诺运行时同项目多版本native包热共存，不将摘要当认证。保留8.2总门禁中尚涉及8.2c/8.8的未完成项。

### 16.5 Git 收口（2026-10-15）

- 用户技术验收后单独授权本批 Git 收口。开始前只读核对分支 `fusion/8-resource`、HEAD `d7301fa`、空暂存区及远端 `d7301fa` 同值，未 fetch/pull、未 rebase/merge/force。
- 精确暂存本批21个文件后执行 `git diff --cached --check`：仅 `.meta` 中 Unity 生成的 `userData:`/`assetBundleName:`/`assetBundleVariant:` 行尾空格，抽样400份既有已提交meta有345份同格式，属项目内Unity规范输出，未修改其内容。无冲突标记、无无关文件混入。
- Commit `1b547b2b9168fe610662e7e3e4519182d5affc46`（`feat(resource): 完成8.2b资源目录与安全发布事务`，仅含本批21个文件，1836+/5-），线性落在 `d7301fa` 之上；随后 Push 到 `origin/fusion/8-resource`（`d7301fa..1b547b2`）。推送后 `git ls-remote`、本地 HEAD 与 `origin/fusion/8-resource` 均为 `1b547b2`。
- 四项既有用户改动（`.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings、Canvas.prefab）及并行UI Plan保持未暂存、未提交，收口前后 blob identity 与第14.4节一致。未暂存测试 Bundle、临时 Staging、Workspace、缓存或未经授权的发布产物；未执行 Reset/Clean/Stash/Rebase/Force Push/Merge 或里程碑。

**8.2b 技术 COMPLETE 且已 Git 收口。到此停止，不自动进入8.2c，不合并 `integration/xf-yl`，不创建阶段8里程碑。**

## 17. 8.2c 实际成果与技术验收（2026-10-15）

### 17.1 基线、授权与工作台

- 实施分支 `fusion/8-resource`，起始 HEAD 为 `b4c2c6cc74a407be6ffefab0c7019dea5edc8edb`。实施阶段只做本批、必要验证及一次独立技术 Review，随后在单独授权下完成 Git 收口（第17.7节）；未合并或创建里程碑。
- 正式入口 `YLFramework/Resource/Workbench`（`Ctrl+Shift+R`）；ResourceProject 资产菜单 `Assets/YLFramework/Resource Workbench`。项目列表/显式选择/创建、资源根、Group与Packing、文件/文件夹GUID选择、地址覆写、aliases、扩展名筛选均编辑正式定义。SerializedObject支持Undo/Redo，保存仅调用本定义的 `SaveAssetIfDirty`，不保存场景。
- `ResourceEditorOperations` 是窄编排，不是新的构建权威：只读Preflight -> `ResourceBundleBuilder` -> `ResourcePublisher` -> `ResourceCodeGenerator`。报告独立 `BuildError/PublicationError/GenerationError`；生成失败不回滚成功发布。预检显示身份、平台/App、数量、Workspace和发布根，检测未保存定义、归属、当前版本完整性及native约束；排队操作可取消，切换项目/关闭窗口取消旧回调。同步native构建期间不能GUI中途取消。
- 选择、绘制编辑、保存及创建都执行Assets-only、可编辑、真实路径及meta链接门禁；Package/外部junction定义不进入写入路径。未知目标文件或孤立meta不被覆盖。工作台不读写原Importer标记，不执行XFAB，不接管旧输出。
- 删除旧 `BuildScript/AssetBundleExporter` 及配套meta，退出旧全局标记/清理/ResData生成入口；`ResourceBuildOperation` 只保留链接/路径及旧SubProjectData所需纯验证。原Runtime消费者及原模拟元数据行为不改，旧Runtime退役仍留8.6。Generator/Output/API改为正式命名，三处原meta GUID保持；窗口脚本身份保持。

### 17.2 Editor模拟取得

- `ResourceEditorSimulation` 以明确的Project集合为输入，可绑定对应已发布 `ResourceCatalogDocument`。每次Resolve/Load/GetGroup从当前正式Project/BuildMap重建同一 `ResourceCatalog` 内存模型，不缓存独立地址表或Unity对象；GUID重新取得Project和路径，定义、移动、导入、删除和项目身份变化不会复用旧索引。窗口仅保留可失效诊断快照。
- canonical地址、aliases、Group成员、GUID/localId和类型规则共用正式链。`DescribeSubAssets` 对Project view可见子资产产出描述，并以包含main/隐藏资产及可赋值类型的native匹配语义拒绝歧义；构建读回与零打包预览不会各自接受不同的name/type描述。
- 显式编译类型校验后借用AssetDatabase对象；子资产按GUID/localId精确取得，但不能用localId掩盖native名称/类型歧义。绑定published Catalog时检查当前定义、地址/alias、类型/程序集、加载路径、子名和Group一致性；过期描述明确拒绝。内容重新导入允许身份描述不变，不声称还原旧Bundle字节。
- 借用对象不得Destroy/Unload；Scene只取得SceneAsset预览，不执行加载/激活，也不初始化ResourceSystem。接入8.3统一source/lease和Scene生命周期仍是后续工作。

### 17.3 正式常量生成

- `ResourceCodeGenerator.GenerateReferences(catalogs, directory, namespace)` 只接受正式Catalog显式集合。默认 `Assets/YLFrameworkData/YLResourceReferences.cs`，独立无Framework/Editor/引擎依赖程序集 `YLFramework.Generated.ResourceNames`。每Project生成 `ProjectId`、`Addresses`、`Aliases`、`Groups`，子资产嵌套类生成canonical `Address` 和 invariant `Key`；值不使用Bundle名或旧文件名键。
- invariant标识符转换、确定性项目/成员排序、关键字/数字前缀、保留成员、项目/成员规范化碰撞、既有类型及namespace冲突在写入前拒绝；namespace必须canonical，不接受可绕过CLR身份检查的verbatim `@` 写法。
- 沿用并收紧旧成熟保护：专用目录完整归属、生成源SHA256篡改检查、asmdef内容/祖先asmdef/asmref/全工程重名程序集检查、孤立meta及未知目录拒绝、同目录临时文件原子替换、实际IO失败保留旧源码、无变化字节/mtime不改、更新保留source/asmdef/folder meta。旧 `YLResData.cs` 输出不是正式归属，不自动迁移或接管。
- 工作台共享一个常量程序集，集合来自当前平台/App全部明确owned已发布Project；`ReadCurrent`只读，不借收集执行Recover或清理。不兼容或未保存候选明示失败，不把临时改名的项目当成未发布而删除已有常量。未发布项目不混入；特殊消费者可通过API显式提供集合。
- 自动发布后生成与独立集合生成均明确报告源码结果，Unity编译结果独立确认。业务asmdef须显式引用 `YLFramework.Generated.ResourceNames`；预定义Assembly-CSharp按autoReferenced消费。测试是真实生成、两次Unity重编译、实际编译程序集加载和地址取得，不以静态语法或Mock代替。

### 17.4 Schema v1 冻结契约

**冻结结论：SchemaVersion=1 正式冻结。** 第16节未冻结验证版补齐 `Aliases` 与 `AssetAssembly`，共同地址上限、完整类型身份及native子资产歧义门禁已验证；不为原型增加永久兼容层。

- 顶层固定字段：`SchemaVersion,ProjectId,BuildId,Platform,UnityCompatibility,AppContentCompatibilityId,BuildSettingsFingerprint,ManifestFile,Bundles,Assets,Groups,SupportFiles`。Bundle固定 `LogicalId,NativeName,RelativeFile,Length,Sha256,UnityHash,IsScene,Dependencies`；SupportFile固定 `RelativeFile,Length,Sha256`；Group固定 `Id,Members[{AssetId,Required}]`。不加入未实现的项目版本依赖、签名或Runtime激活字段。
- Asset固定 `AssetId,Guid,LocalId,Address,Aliases,SubAssetKey,BundleId,LoadName,AssetType,AssetAssembly,Kind,SubAssetName`。canonical身份是小写32位GUID加 `_` 加非零Int64 localId的invariant十进制文本；ProjectId/GroupId为有界安全技术ID。地址在Project中精确ordinal，保留原始规范大小写，拒绝大小写折叠冲突、绝对/逃逸路径；alias指向同一canonical，子资产继承同一alias路径并附自己的SubAssetKey，不生成第二份映射。
- `SubAssetKey`等于子资产localId十进制文本，main为null；`Kind`只允许Asset/Scene/SubAsset。普通native LoadName是实际 `assets/...` 小写路径，Scene保留 `Assets/...unity` 精确路径；native子资产按唯一 `SubAssetName + 编译Type`定位。类型持久化为 `AssetType.FullName + AssetAssembly简单名`，不含AQN/程序集版本；Scene使用 `Scene/null`。同名同FullName但不同程序集可区分，同完整类型身份同名子资产拒绝；不按外部字符串反射创建类型。
- 编码/规范化：严格UTF-8、无BOM/空白/尾换行，EscapeNonAscii、InvariantCulture；JSON属性ordinal排序，Bundles/Assets/Groups/SupportFiles、依赖、Group成员、aliases按稳定identity/字符串ordinal排序。重复/未知/缺失字段、非法UTF-8或非canonical实际字节拒绝；Int64及布尔类型不接受字符串冒充。
- `BuildId = SHA256(移除BuildId字段后的canonical payload bytes)`；`CatalogDigest = SHA256(包含BuildId的最终catalog bytes)`，没有自引用。制品路径/长度/SHA256、Unity native name/hash、依赖图和加载描述继续由实际BuildResult、Manifest及native读回支撑；摘要不是认证。
- 每数组/集合上限16384、依赖边上限65536、JSON上限16MiB、深度64；canonical+alias共享16384个总地址上限，子资产和所有项目内地址均计入，重排输入不能改变结论。Group仍允许显式Required合同，但本批Project生成成员全为required。
- 读取明确要求Schema=1以及精确ProjectId、BuildTarget平台、完整Unity版本、AppContentCompatibilityId；消费脚本类型/布局或Player能力变化必须更新App兼容ID。Release Index维持SchemaVersion=1及第16节的单摘要快照合同。冻结后字段/身份/编码变更必须新版本和明确兼容门禁，未知格式fail closed。
- 兼容边界：不兼容旧XF映射、YL ResData/BinaryFormatter或缺少新必需字段的8.2a/b验证原型；需要从正式定义重新构建/发布，不自动改写或清理既有原型版本。只承诺本机Unity `2022.3.62f3c1` / `StandaloneWindows64`真实验收，不宣称跨Unity版本、Player、其他平台或生产远端通过。

### 17.5 实际验证与独立Review

下表合计50个实际执行的定向测试用例，均有通过结果；MCP的 `TotalTests` 是全工程发现量，不冒充本批执行量。未重跑阶段8.1～8.2b历史完整套件，也未构建整个游戏或Player。

| 验证范围 | 实际通过 | 主要证据 |
| --- | --- | --- |
| ResourceCatalogCodecTests | 15 | canonical/读回/身份/兼容、alias、程序集身份、同完整类型子资产冲突、16384总地址边界及重排 |
| ResourceEditorSimulationTests | 5 | 多Project/alias/Group/Text/Sprite、修改/移动/导入/删除失效、published过期描述、main/sub及Base/Derived歧义负例 |
| ResourceCodeGenerationTests | 9 | 正式值、子key、命名/类型/namespace碰撞、verbatim拒绝、invariant确定性、篡改、幂等、真实锁IO及meta保留 |
| ResourceOutputSafetyTests | 3 | 祖先程序集/文件/Packages路径拒绝、junction与外部哨兵、无变化也拒绝live重复程序集 |
| ResourceEditorTests | 8 | Undo/Redo与GUID持久化、未保存/取消、候选dirty集合拒绝、真实定义创建/保存/不覆盖、junction外部字节/meta保护、旧Runtime纯分区保护、Editor身份 |
| ResourceWindowTests | 1 | 真EditorWindow Layout/Repaint/点击取得、选择事件、切换和关闭取消排队操作，无导入标记写入 |
| ResourceBuildIntegrationTests | 1 | A/B两个正式Project、三次native构建和正式发布；只读集合不改root快照；dirty A不能遗漏；合法地址但生成标识符碰撞使生成独立失败，旧/新版本完整、Recover和借用取得准确 |
| ResourceCatalogBuildTests | 1 | 实际Manifest/native name/hash/依赖、Group成员、类型程序集、Sprite子描述、Scene路径及canonical读回 |
| ResourcePublishingTests | 4 | 本轮执行第16节原事务定向回归：提交重验证、重复/故障/取消/恢复、unknown/link/corruption/foreign拒绝、路径限制；没有改变其Publish/Recover提交合同 |
| ResourceCodeCompilationTests | 2 | 真实Catalog生成，独立常量DLL、显式consumer asmdef、Assembly-CSharp编译与Text/Sprite/Group/alias消费；重复/失败/更新后的第二次编译及meta身份；中断清理归属保护 |
| ExportUnityPackageTests | 1 | 正式新/迁移Editor源码与配套meta进入实际UnityPackage，原Screen prefab身份/内容保持 |

- 一次独立只读技术Review提出1项P1和5项P2；全部已修复并定向回归，无挂起实质finding：编辑/保存/创建越界门禁，dirty候选丢失，verbatim namespace碰撞，共同子资产native语义，子去重完整程序集身份，canonical/alias总量限制。新增负例先在旧实现观察失败，再验证修复通过；没有二次Review冒充独立新结论。
- 编译探针发现Domain Reload后iterator闭包不恢复，已定位IL并把跨重编译验证移至普通方法，原碰撞/锁/更新断言保留。期间遇到一次Unity firstpass DLL外部文件锁、生成更新偶发IO锁；未吞异常、弱化断言或增加盲重试，锁故障明确返回且旧文件保护不变，最终相关完整测试/编译重跑通过。不能承诺外部工具持锁时生成或Unity编译一定成功。
- Runtime/Editor实际Unity编译边界验证：`YLFramework.Runtime` 的Player编译图417个源文件，无Editor源码/程序集/UnityEditor引用泄漏；这是编译图校验，不是Player构建。三处迁移GUID与AssetDatabase实际值一致，新增两个脚本配套meta齐全。最终Unity无脚本编译失败、已加载Bundle=0、原 `Assets/Scenes/Main.unity` 未标脏、无残留本批ResourceProject夹具或 `Assets/YLFrameworkData` 测试输出。
- 原游戏/Games/MainModule、原XF/XFAB/XFFSM、历史Bundle、StreamingAssets、Packages及ProjectSettings由原Fixture保护快照及diff核对；`.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings、Core Canvas和并行UI Plan的既有内容保持。本轮没有暂存或触碰这些用户修改。

### 17.6 8.3依赖与停止点

- 8.3可直接依赖稳定Project/Group/GUID/alias定义、唯一BuildMap/verified BuildResult及共同子资产描述、冻结Catalog v1/严格Codec、不可变版本/Release Index/owned恢复、正式工作台和常量程序集输入。Editor借用解析可作为统一source/lease接入的依据，但当前不伪装已接入Runtime加载器。
- 剩余边界：没有AssetBundle运行时取得/释放、低层Bundle lease、运行时Group加载、Scene切换、实例池、Core/Audio迁移、版本激活、下载或签名/防重放。保持六项不可冻结回调下的保守native重建策略，不宣称整体缓存或性能提升。常量默认位置仍需真实用户Project发布后生成，夹具输出已清理。
- **8.2c 技术 COMPLETE；SchemaVersion=1 正式冻结；Git 收口见第17.7节。到此停止，不自动实施8.3，不合并integration/xf-yl，不创建阶段8里程碑。**

### 17.7 Git 收口（2026-10-15）

- 以独立授权一次性完成：精确暂存本批 Changed 范围后，提交 `66400c8bcf08f73085eb9e79c236a18cdbdced2f`（`feat(resource): 完成8.2c资源工作台与代码生成`，37个文件，1501+/1948-），线性落在 `b4c2c6c` 之上；随后推送至 `origin/fusion/8-resource`（`b4c2c6c..66400c8`）。本地 HEAD、`origin/fusion/8-resource` 与实际 `git ls-remote` 均为 `66400c8`。
- 提交只含本批 Resource Editor/Runtime Catalog/迁移测试及本 Plan；受保护用户改动（`.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings、Core `Canvas.prefab`）及并行 UI Plan `.kilo/plans/1791960935353-ui-panel-redesign.md` 未暂存、未提交、未修改，仍留在工作区。原游戏、XF/XFAB/XFFSM、历史 Bundle、StreamingAssets、Packages、ProjectSettings 保持原样；未 Reset/Clean/Stash/Rebase/Force Push。本次未新增产品行为，故未重跑 Unity 编译/测试/构建/生成/Review。

## 18. 8.3 实际成果与技术验收（2026-10-15）

### 18.1 基线与正式入口

- 分支 `fusion/8-resource`，起始及交付 HEAD 均为 `57f00a9a26be7da4af4c91e441f4a4f5c36b0f6e`。本轮仅授权8.3代码、必要测试和一次独立技术Review；未暂存、Commit、Push、合并或创建里程碑。
- `ResourceLocalSnapshot.Open(absoluteDirectory, projectId, expectedCatalogDigest, platform, appContentCompatibilityId)` 显式绑定本地完整版本，不读 active/current index，不选最新版本，不执行恢复/激活。严格消费冻结v1 Codec，验证确切Catalog摘要、Project/Unity/平台/App、Complete与完整文件集合、长度/SHA-256，拒绝链接输入。缺文件/未登记内容在集合门禁报告 `InvalidDataException`；明确路径必须 fully-qualified，Windows drive-relative/current-drive-rooted 输入不能冒充绝对路径。
- `snapshot.CreateCatalog(compiledAssetTypes)` 产生正式内存Catalog，保留canonical/alias、资产/子资产身份、Bundle和Group描述；常用Unity类型有明确编译映射，自定义类型须显式提供 `typeof`。不扫描程序集，不从Catalog字符串反射创建类型，不修改v1字段、编码、身份或持久化含义。
- `new ResourceConfiguration(new[] { catalog })` 默认装配Resources、Local/URL Texture，唯一ResourceSystem按明确快照装配AssetBundle Source。`ResourceEditorSimulation.CreateConfiguration()` 显式装配Editor borrowed Source；预览入口仍不会自动初始化Runtime。没有新增Manager、加载表权威或生产XFAB后端。
- 正式能力：`Acquire<T>/AcquireAsync<T>`（子资产通过 `ResourceAddress.SubAsset` 取得）、`AcquireBundle/AcquireBundleAsync`、`AcquireAllAsync<T>(projectId,bundleId)`、`AcquireGroupAsync(projectId,groupId)`。typed批量仅取得Catalog登记且可赋值的成员，返回拥有型 `AssetBatchLease<T>`，不是裸数组；Group按canonical成员及Required取得，required失败归还整批、optional失败保留cause。Scene成员不能被Group伪装成普通资产加载。

### 18.2 唯一图与所有权

- Bundle records 属于ResourceSystem，以 `CatalogDigest + BundleId` 唯一索引；先迭代建立完整DAG节点、依赖入边和根pin，再运行IO。异步Bundle创建使用FIFO队列及4槽；同步只驱动本次保留的冷图，不接管异步队列，遇相同在途图明确 `InProgress`。此4槽不是所有Asset/Texture请求的全局限流。
- Source交割 `asset + retention` 给唯一AssetRecord；AssetBundle资产/subasset的retention是图根Lease。每个consumer独立AssetLease，原生生产共享；物理Bundle拥有其所加载资产。无idle缓存pin，最终责任归零时依赖者先 `Unload(true)`，再归还依赖入边；共享/菱形叶不提前卸载。使用前重验Bundle文件并保持只读句柄至节点释放，不用mtime或Complete自身代替内容验证。
- Bundle与Asset请求均先冻结整批终态及责任，再通知观察者。Request单次TakeResult、未领取Dispose、Scope、稳定终态、主线程、会话epoch及观察者异常隔离仍有效。退出先冻结全部请求，再执行解绑/私有取消/释放，不能由级联取消重写其他请求的终态。
- AssetBundleCreateRequest/AssetBundleRequest不可取消，producer持续观察到最后收尾，零owner迟到结果仍归还图；Shutdown对合法live owner返回Busy并保持可用，纯在途收尾可等待关闭。旧authority由draining保留，新初始化在旧图未排空时拒绝Busy；没有协程宿主或全局Unload兜底。
- 私有UWR的最后consumer可Abort；取消generation不再向后来的consumer交割旧Abort结果。新consumer等待旧IO结束，清理旧结果后在同一record串行重新生产，不重叠native IO。不可取消来源则继续共享有效在途producer。
- Resources/AssetDatabase只drop自己的引用，不Destroy外借资产；Local/URL Texture仅Destroy自己创建/解码的对象。Local支持冷同步及异步，URL只支持冷异步（Ready缓存可同步取得），保留完整路径/URL大小写。Local输入32MiB上限，UWR请求30秒timeout；不具备生产远端签名、下载预算/续传或重试更新协议。
- `BundleLease.Bundle` 是低层借读入口；调用方自行启动的native操作不进入内部pending账本，必须把BundleLease持有到这些操作和使用结束，不得自行Unload。正常消费优先使用Asset API。8.4可直接用Prefab AssetLease及其依赖retention覆盖后续active/idle/destroy-pending实例；本轮不创建实例或实例桶。

### 18.3 实际Unity证据

实际环境：Unity `2022.3.62f3c1` / `StandaloneWindows64` / 正常Domain Reload，所有native夹具使用8.2正式Builder/Publisher及冻结v1产物，在可恢复的空场景中运行，不进入原游戏资源链。

| 最终定向范围 | 实际结果 | 证明内容 |
| --- | --- | --- |
| Resource PlayMode namespace | 36 Passed / 0 Failed / 0 Skipped | 8.1受影响producer/Request/Scope/Resources合同、optional Group、取消generation重取；7项真实Local/loopback HTTP Texture门禁 |
| ResourceRequestTests | 5 Passed | owning结果单次交割、Dispose/异常/观察者合同 |
| ResourceCatalogTests | 10 Passed | 地址/alias/type、重复身份、缺依赖/环/菱形内存模型 |
| ResourceCatalogCodecTests | 15 Passed | 冻结v1规范化、类型/子身份/alias、非法依赖/兼容和集合上限 |
| ResourceEditorSimulationTests | 6 Passed | 现有描述/失效/歧义门禁与正式Source装配，borrow不Destroy |
| ResourceSessionSafetyTests | 2 Passed | dirty/未保存场景拒绝且不关闭/保存 |
| ResourceLocalSnapshotTests | 1 Passed | 完整真实发布挂载、编译type/子key、无native副作用、坏/缺文件及非绝对路径拒绝 |
| CompleteNative83RegressionSharesOnePublicationAndPreservesReloadEvidence | 1 Passed | 单份真实发布连续覆盖下述10段native合同，任一断言失败使整个用例失败；最终方法16.727秒（不含夹具构建） |
| ResourceSessionTests.RealPlayExitInvalidatesOwnershipRejectsReentryAndNextPlayCanInitialize | 1 Passed | 既有Resources借用、退出重入、未领取结果及双Play直接回归 |
| CoreArchitectureBoundaryTests（Development闭包） | 1 Passed | Foundation+Root+Core实际辅助编译及Systems符号拒绝；不是完整Player构建 |
| AudioXfabBoundaryTests | 1 Passed | 既有可选适配器边界/默认导出不变 |
| CoreDefaultResourceTests | 授权恢复后4 Passed / 0 Failed | 初次交付为3 Passed / 1已知基线 Failed；随后按独立授权恢复Canvas旧数据并原断言复验，见18.6 |

- native综合合同：Text内容、Prefab/共享Texture及真实菱形依赖、Sprite子资产rect/name与卸载/重载；两个Scope与第三个独立consumer；typed Text/Sprite批量、Group与公开BundleLease；缺失共享叶、坏文件、超过4槽的排队失败与无关健康请求；观察者异常/最后取消/会话失效及迟到收尾；Editor正式Scope借用；Bundle整批通知重入；失败观察者内同步冷加载；真实native Sprite被本测试临时改名后0匹配/2匹配明确失败并清账；已启动AssetBundleRequest后最后取消及真实退出/下一Play。没有用Mock证明Bundle生命周期。
- 菱形加载次数与实际Catalog闭包节点数一致，每个native图节点一次生产；归零后真实Unity对象 `== null`、加载Bundle差集回0、asset/bundle记录及根/边pin归零，随后正式reload成功。原生asset阶段通过直接观察实际AssetBundleRequest存在且尚未完成锁定，不把等待Bundle阶段当成已开始asset加载。
- 动态图片实测：大小写Local PNG、缺失/坏图、独立consumer取消、未领取Dispose、物理Destroy；HTTP路径/query大小写、404及坏图、共享仅一个GET、最后Abort；服务端确认传输开始后取消并立即重取成功、两代请求顺序明确；真实新解码结果在完成observer未领取Dispose后最终fake-null。cancelable producer的可控收尾窗口只用于确定性内核回归，不冒充真实Bundle证据。
- MCP `TotalTests` 是发现树，不是执行量；跨Domain Reload的Summary duration有异常大值，部分多用例汇总会重置，故native最终采用单一完整用例与逐项实际结果，不用发现量/异常时长冒充验收。未重跑阶段1～7完整套件、完整8.2历史构建/事务、原游戏或Player；新Schema读回、必要闭包及直接受影响合同按风险定向回归。

### 18.4 实质修复与独立Review

- 本批真实RED→GREEN修复：Bundle逐个通知让兄弟请求仍Pending/可被取消；同步共用全局pump在观察者重入时返回Pending TakeResult；最后私有取消后新consumer加入旧Abort generation；Windows rooted但非fully-qualified路径被误接受。前两项使用真实Bundle，generation窗口使用确定性内核回归并补真实HTTP开始后重取验证；不是弱化断言或盲重试。
- 一次独立新上下文只读Review，无Critical；2项Important是测试场景安全检查位置及缺文件断言/实现不一致，1项Minor是跨reload Setup失败前的所有权登记。已将guard移到任何fixture构造前，交割前失败清理限定自有fixture，缺文件明确固定InvalidDataException及原因消息。成功流程/场景保护及native整体回归通过。
- Review另外指出取消重取、已启动asset native与解码零owner的待证窗口，均补必要验证并修复generation缺口。调用方直接启动的低层native操作保活边界写入BundleLease文档，不将内部账本推定为自动纳管外部operation。没有第二次Review或虚构审查者执行过Unity。

### 18.5 保护、限制与停止点

- native综合夹具前后对 `Assets/Games`、`Assets/Scenes`、原 `Assets/StreamingAssets`、`AssetBundles/MainModule`、`Packages`、`ProjectSettings` 全部保护快照比对通过，只清理登记的本次fixture源码、workspace、发布及临时场景；不清理先前完整验证版本、未知缓存或其他任务产物。Schema v1磁盘格式未修改，原XF/XFAB/XFFSM、MainModule、Core/Audio消费者/旧后端及ProjectName语义未改。
- 8.3初次交付时四项用户改动blob与14.4完全相同：`.gitignore`=`a14b361...`、`.kilo/kilo.jsonc`=`e943dde...`、ProBuilder=`59b16de...`、Canvas=`c6b7d12...`。并行UI Plan原样保留，不纳入本批。实际暂存区为空、HEAD不变，tracked diff-check通过。Canvas后续单独授权恢复见18.6，其他保护不变。
- 初次交付时 `CoreDefaultResourceTests.CanvasPreservesOriginalSerializedDataExceptYLAdaptationBinding` 实际失败：用户既有GraphicRaycaster mask为4294967295（旧断言期望2047），另有已记录Scaler字段。当时blob正是14.4的保护基线，属于第1节已注明的外部变化，不是8.3回归；未擅自还原或降低断言。该失败随后依据明确授权恢复数据并复验通过，见18.6。
- 新增7个Runtime脚本、3个测试脚本及10份Unity生成meta，GUID唯一、AssetDatabase导入/反查一致；最终无dirty场景、无资源authority、加载Bundle=0、scriptCompilationFailed=false。Player编译图Runtime有424个源文件，无Editor source或Editor assembly反向泄漏；这不是Player实机验证。
- 仅验收本机Windows Editor/正常Domain Reload。未验证Player、IL2CPP、其他平台、关闭Domain Reload、jar/WebGL/StreamingAssets自动装配、生产HTTPS/CDN或同项目多版本native共存。URL是基础Texture来源，不是8.7/8.8生产下载更新；无idle缓存或磁盘清理API，释放由实际责任驱动。Scene只可显式Bundle准备，真实Scene/Scene pin仍留8.5。
- **8.3 技术 COMPLETE，等待用户技术验收和单独Git收口授权。到此停止，不自动进入8.4、不改变Core/Audio默认后端、不合并integration/xf-yl、不创建阶段8里程碑。**

### 18.6 Canvas旧数据授权恢复（2026-10-15）

- 用户在确认上述基线冲突后明确授权“恢复旧数据”。仅将自有YL `Canvas.prefab` 的 `m_BlockingMask.m_Bits` 从4294967295恢复2047，并删除额外显式序列化的 `referenceResolution/automaticMatch/manualMatch` 三字段，恢复原序列化表达；未改变UGUI CanvasScaler配置、YL Adaptation脚本绑定或其他资源数据。
- 恢复后Canvas blob为 `5d8cf22e5ef5a2c31a028798d4268fc8f0cfb4c5`，与当前HEAD版本一致，该Prefab不再有工作区差异。其meta blob仍为 `8e2351424d2703663e4bfeee924b9f79bc2740e8`，GUID仍为 `0df4ee5a6aa484a40b5d537a1c23a2c0`；原XF对照件blob仍为 `041390a1b7280f816c0d0dd22c48579ef36d7c7b`，未修改。
- Unity重新导入后定向执行 `CoreDefaultResourceTests`：**4 Passed / 0 Failed / 0 Skipped**，包含原Canvas整文件等价断言、Canvas/EventSystem独立身份与默认组件检查。未修改测试源码或断言，无dirty场景；未运行无关native Bundle/Player回归。
- 此项为用户单独授权的资源恢复，不是撤回此前保护记录或扩展8.3范围；其他用户改动及并行UI Plan保持原样。未暂存、Commit、Push、进入8.4或改变Git停止点。

### 18.7 Git 收口（2026-10-15）

- 以独立授权一次性完成：精确暂存本批32个文件（12个已跟踪修改 + 20个新增含 `.meta`）后，提交 `732c8286ea1b04bdfb1b58bb2626c203d76a0454`（`feat(resource): 完成8.3 AssetBundle加载与生命周期`，2180+/39-），线性落在 `57f00a9` 之上；随后推送至 `origin/fusion/8-resource`（`57f00a9..732c828`）。推送后本地 HEAD、upstream 与实际 `git ls-remote` 均为 `732c828`。
- 提交只含本批 Resource Runtime/Editor/Catalog/Loading/Sources、本批测试及本 Plan；Canvas.prefab 已按18.6独立授权恢复并随本批提交，工作区无该资产差异。受保护用户改动（`.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings）及并行 UI Plan `.kilo/plans/1791960935353-ui-panel-redesign.md` 未暂存、未提交、未修改，仍留在工作区。原游戏、XF/XFAB/XFFSM、历史 Bundle、StreamingAssets、Packages、ProjectSettings 保持原样；未 Reset/Clean/Stash/Rebase/Force Push/Merge，未进入8.4、未合并 integration/xf-yl、未创建阶段8里程碑。
- 暂存的10份 `.meta` 仅 `userData/assetBundleName/assetBundleVariant` 生成行含行尾空格，与8.2b既有已提交格式一致，未改其内容；`git diff --cached --check` 除此外无冲突标记或无关文件混入。

## 19. 8.4 实施与技术验收（2026-10-15）

### 19.1 状态与实施边界

- **8.4 技术 COMPLETE，待用户技术验收与 Git 收口。** 起始分支 `fusion/8-resource`、HEAD `0e2ec2eb7a3b317a4d13b1659e127f7428789ef2` 已核对。本轮未暂存、Commit/Push、切分支、合并 integration 或创建阶段8里程碑。
- 实现集中在 `Runtime/Systems/Resource/Instances/`，以及 `ResourceSystem`/`AssetLease` 的实例 pin 与退出协作。保持唯一权威、Catalog v1、现有 Request/Lease/Scope 与四层架构；不新增 Resource DLL/Manager/永久兼容层。
- 场景事件只负责实例缓存退休，不建立 Scene 加载、SceneRecord 或独立 Scene pin。未提前实施8.5～8.9；原游戏、Core/Audio、直接 Prefab/Attach 的 borrowed 输入及旧资源链不变。

### 19.2 正式 API 与所有权

- `ResourceSystem.Instantiate(ResourceAddress/AssetLease<GameObject>, parent)` 返回独立 `InstanceLease`；输入 Lease 必须属于当前 authority 且指向 Prefab 资产，不被消费。`InstantiateAsync(ResourceAddress, parent)` 返回 `ResourceRequest<InstanceLease>`；Ready Prefab Lease 的 native 实例创建直接同步执行，不伪造异步 producer。
- `InstanceLease.GameObject` 只在当前 checkout 有效时借读；`Return`/`Dispose` 幂等结束 checkout，健康对象回原桶；`Discard` 结束 checkout 并销毁损坏/失败对象。先撤销旧身份再触发用户回调，旧 Lease 迟到 Return/Discard 不得操作新 checkout。Return 后不得继续使用借读裸对象；框架不能撤销外部保存的 Unity 引用。
- `GetInstanceCache(address/prefab, policy, scene)` 显式取得 `InstanceCache`，支持 `Instantiate`、`Prewarm(target)`、`CachedCount`、`MaxCapacity`、`Dispose`。默认空闲容量32，`0`不缓存，`-1`无限；FIFO、容量裁剪、归还及桶复用由 Foundation `GameObjectPoolData` 执行，Resource 不建立第二套可重用对象队列。
- 缓存身份是本 authority 的 `AssetRecord + policy + scene handle`。每桶持有内部 Prefab `AssetLease` pin，覆盖 creation、active、idle、retired 和 Destroy-pending；每次 checkout 有独立 Lease。冷创建在失活根下登记后才移动到最终层级/场景并激活，预热逐个登记从未激活对象，不依赖其可能永不触发的 `OnDestroy`。
- `InstanceCache.Dispose`/`ClearInstanceCaches` 退休缓存并清理空闲对象，不销毁借出实例；晚归还销毁，不重入桶。旧 Data 在全部实例实际 fake-null 前不回原 Safe owner，Prefab pin 与8.3图 retention也等到真实销毁后才结算，不把引用归零或调用 Destroy 当成 native 死亡证据。
- 显式取得的缓存或成功交付过 checkout 的缓存可以继续持有空桶。隐式首次创建尚未成功交付时为 provisional，失败/交付前取消自动退休，不留下无主 creation pin，不误退休显式缓存或已被嵌套成功 checkout 使用的共享缓存。
- 异步沿用 Request 单次交割：`TakeResult` 转移实例责任；未领取成功结果 Dispose 时 Return；创建中已取消的结果 Discard。可直接复用 `ResourceScope.Track/TakeResult/Own`，不增加第二套域所有权。

### 19.3 场景、关闭与 Foundation 边界

- `SceneScoped` 默认捕获目标父对象所在场景，无父对象时捕获活动场景。异步完成时重验父对象；无父对象时不误用后来切换的 active scene。卸载 scope scene 会退休空闲缓存，迁移到其他场景或 DDOL 的有效借出实例继续存活并保活，晚归还才销毁。
- `Persistent` 只表示空闲缓存跨场景保留，不自动 DDOL 借出对象，也不取得 Scene pin。两类缓存均使用 Resource 自有 DDOL 缓存根，只纳管自己创建的对象。
- PlayerLoop 有界轮转 prune 每轮至多8桶、每桶8项，Editor 非Play时继续收尾。无协程/MonoBehaviour Manager 宿主，退出不新建宿主；最后一桶释放后解绑该 authority 的 scene 事件并销毁自有空根。
- Shutdown 暂时拒绝新生产并退休缓存；外部 Asset/Bundle Lease、活跃 checkout 或执行中的创建/回调导致 Busy，恢复 Resource 可用性；仅 Destroy-pending/native producer 可异步排空。空缓存同步释放也不会在 Shutdown 请求登记前误触发最终关闭。
- 会话退出先使旧身份失效并销毁自有实例，保留 Destroy-pending pin/record；旧 public AssetLease 迟到 Dispose 不扣实例 pin，旧 InstanceLease 不影响新 authority，旧责任未排空时新初始化返回 Busy。
- Foundation 仅增加内部 `Allocate(parent, targetScene)`，修正原入口只能使用当前活动场景、不能履行 Resource 捕获目的场景合同的限制；公开 `Allocate(parent)` 原行为不变。既有 `IsCacheHierarchy` 改为 internal，复用于 Resource 冷创建校验。未复制桶算法、添加 Foundation→Resource 反向依赖或重构通用池。

### 19.4 实际验收与独立 Review

环境为 Unity `2022.3.62f3c1`、Windows Editor/D3D11、正常 Domain Reload。复用8.2/8.3独立 YL fixture、正式Builder/Publisher及发布副本，仅向测试 Prefab 增加 test-only lifecycle probe。下列 native 综合入口登记在 EditMode 程序集，但通过 `EnterPlayMode` 在**真实 PlayMode**执行实例/native断言，并跨实际 `ExitPlayMode/EnterPlayMode` 检查退出和再次初始化。

| 本轮实际验证 | 结果与范围 |
| --- | --- |
| `CompleteNative84InstanceLifecycleRegression` | 最终 Passed（方法20.071秒）。覆盖真实创建/复用、旧身份、预热/容量/Discard、退休 Data、active/idle/never-active/Destroy-pending pin、OnDestroy 中依赖仍存活、外部 Destroy/损坏缓存根、Awake/OnEnable/OnDisable 异常与重入、创建中取消/退出、未领取结果、scene unload/迁移/DDOL/Persistent及真实双Play。各轮排空核对 asset/bundle record 和原生Bundle集合归零、Prefab/共享Texture实际fake-null，不以计数器替代native证据。 |
| Review finding 红绿回归 | `FailedImplicitCreationReleasesItsGraphWithoutExplicitClear` 先实际失败：记录残留1；修复后相同用例纳入上述最终 Passed 门禁。父对象等待期销毁、首次回调拒绝、创建中取消，不调用 `ClearInstanceCaches` 即回基线；显式缓存与嵌套成功 checkout 不被误退休。 |
| Foundation `GameObjectPoolPlayModeTests` | 13/13 Passed，原直接消费者、跨场景、生命周期回调、旧Data/Safe owner、容量与根损坏合同不变。 |
| Resource PlayMode `YLFramework.Tests.Resource` | 36/36 Passed，受影响 Request/Lease/Scope、源、取消、迟到收尾、旧会话及 Shutdown Busy 合同保持正确。 |
| `CompleteNative83RegressionSharesOnePublicationAndPreservesReloadEvidence` | Passed（方法19.368秒），复验本次 pin/退出协作直接影响的8.3 native Bundle/依赖生命周期，不重复全部历史批次验收。 |
| `CompleteFoundationAndCoreCompileInDevelopmentAndRejectSystemsSymbols` | Passed，真实 Foundation+Core 源码闭包及反向 Systems 编译拒绝保持有效；不以辅助编译替代 Unity 编译或运行。 |

完成一次 fresh-context 只读独立技术 Review，聚焦身份、Prefab pin、Data退休、重入、迟到清理及 Foundation/场景边界。报告1项P2“首次失败隐式缓存保活未结算”，已复现、修复并定向回归；没有未处理的实质 finding，未追加形式复审。实施中另复现并修复空缓存 Shutdown 请求遗失、损坏缓存根未及时退休，均由最终综合门禁覆盖。

最终 Unity 实查：`scriptCompilationFailed=false`、非Play/非编译、Current为空、旧authority draining=0、instance cleanup=0、自有缓存根=0、已加载原生Bundle=0，原 `Assets/Scenes/Main.unity` 未标脏。Runtime Player编译图427个源码、无UnityEditor程序集引用；这是编译图核对，不是Player构建。新增5脚本及Instances目录的6份Unity生成meta均可在AssetDatabase读回、GUID非空且互异。

### 19.5 保护、后续依赖与停止点

- `.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings 及既有 `Canvas.prefab` 起止内容哈希相同。并行 `.kilo/plans/1791960935353-ui-panel-redesign.md` 未由本轮修改或接管；终检观察到其blob从 `c0ab167...` 变为 `0790062...`，保留当前外部更新，不回退/覆盖。native fixture保护快照和最终diff保持原XF/XFAB/XFFSM、原游戏/Scene、MainModule、历史Bundle/StreamingAssets、Packages及其余ProjectSettings不变；不清理未知目录或其他任务产物。
- 新脚本/目录由Unity导入生成独立meta，既有GUID/序列化身份不变；本批改动与起始用户改动均留在工作区，未暂存或提交。
- 8.5可依赖 `InstanceLease`、Prefab/依赖pin、scene-scoped/persistent缓存、迁移/DDOL后的退休晚归还、Destroy-pending drain与旧会话隔离；Scene加载、SceneRecord、独立Scene pin仍须另行实施验收。
- 未运行Player构建、IL2CPP/AOT、其他平台、禁用Domain Reload或完整Scene资源系统验收，不将Windows Editor证据外推。第三方任意修改/销毁借读裸对象，只能按Unity可观察的存活状态收尾，不能承诺阻止原生对象越权操作。
- **8.4 技术 COMPLETE；Git 收口见第20节。到此停止，不自动实施8.5。**

## 20. 8.4 Git 收口（2026-10-15）

- 以独立授权一次性完成：精确暂存本批16个文件（5个已跟踪修改 + 11个新增含 `.meta`）后，提交 `d26c166aa7695b098b81d77aa8ee3c49d896004e`（`feat(resource): 完成8.4实例缓存与Prefab资源保活`，1171+/14-），线性落在 `0e2ec2e` 之上；随后推送至 `origin/fusion/8-resource`（`0e2ec2e..d26c166`）。推送后本地 HEAD、upstream 与实际 `git ls-remote` 均为 `d26c166`。
- 提交只含本批 Instance/Cache 实现、`GameObjectPoolData`/`AssetLease`/`ResourceSystem` 的必要改动、本批测试及本 Plan 第19节；未暂存、未提交、未修改受保护用户改动（`.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings）及并行 UI Plan `.kilo/plans/1791960935353-ui-panel-redesign.md`，均仍留在工作区。原游戏、XF/XFAB/XFFSM、MainModule、历史 Bundle、StreamingAssets、Packages、ProjectSettings 保持原样；Canvas.prefab 未再次修改。
- 未执行 Reset/Clean/Stash/Rebase/Force Push/Merge，未进入8.5、未合并 `integration/xf-yl`、未创建阶段8里程碑。暂存的11份 `.meta` 仅 `userData/assetBundleName/assetBundleVariant` 生成行含行尾空格，与既有已提交格式一致，未改其内容；`git diff --cached --check` 除此外无冲突标记或无关文件混入。
- 本次未新增产品行为，故未重跑 Unity 编译/测试/Review；不将第19节验收证据外推到隔离的 Player/其他平台。

## 21. 8.5 Scene 生命周期技术证据（2026-10-15）

### 21.1 基线与技术状态

- 起始与交付时分支均为 `fusion/8-resource`，HEAD 为现场核对的 `c515b4f26992764d5a7d913bc6d124a52d1ca35a`（8.4 Git 收口后的实际基线）；本批未 Commit/Push、未合并、未创建里程碑。
- **8.5 技术 COMPLETE，待用户技术验收与 Git 收口**；8.6 及后续批次仍未实施。现场出现的重叠 Scene 实现经用户确认后保留沿用，没有回退或覆盖另一批工作。

### 21.2 正式入口与所有权

- `ResourceSystem.LoadSceneAsync(address, mode, activateOnLoad, activationTimeoutSeconds)` 返回正式 `SceneRequest : ResourceRequest<SceneLease>`，直接沿用单次领取、未领取结果释放和 Scope 的 Track/TakeResult/Own 协议。
- `SceneLease.Dispose()` 不卸载 Scene。独立 `SceneRecord` 保存实际 Scene/handle、LoadSequence、Address、完整 native Path、SnapshotId、CatalogDigest 与加载模式；caller 身份释放不影响 Scene 合法保活。
- `GetSceneRecords()` 与 `SceneRequest.Scenes` 提供本次真实关联结果。Lease 已释放或 Single 取消后，可显式 `UnloadSceneAsync(record)`；已 Dispose Lease 的卸载调用拒绝，旧记录不能命中新一轮加载。
- pending Scene 在 native 开始前即持有已有 Bundle 图的根 Lease，完成后转为 SceneRecord 图 pin。Scene 事件、OnDestroy 及 native unload 完成后，cleanup 才归还责任；没有独立 Bundle 引用表或第二份 Scene 资源权威。
- 重复在途卸载消费者独立等待同一 native 操作；外部卸载与 Single 替换按真实 handle 结算。正常 Shutdown 在 pending native/live SceneRecord 存在时返回 Busy 并恢复 Ready，不隐式卸载业务 Scene。

### 21.3 身份、取消与实际边界

- 框架内部每次 Scene 意图独立保留，native 加载逐次执行，依靠 native 前 handle 快照、Scene 事件和完成后一帧结算窗关联，不使用短名或路径首次匹配。相同 path/短名重复 Additive 拥有独立 handle/LoadSequence。
- Bundle 准备期间已加载的外部 Scene 属于 Before，不参与本次新候选。native 窗口内多个同 path 新身份无法归属时返回 `SceneIdentityAmbiguous`，保守保活所有候选，拒绝 facade 卸载，等待真实外部 owner 卸载后分别结算。
- native 前取消不启动该 Scene native；native 后 Cancel/Dispose 只结束消费者等待，解除激活门控并保留 pending 图。唯一迟到 Additive 尝试真实卸载；歧义候选或不可卸载的最后 Scene 保留可观察记录与 pin，不伪装回滚。
- Single 取消后的 native 完成仍可能替换旧 Scene；新 Scene 从 `Scenes`/`GetSceneRecords()` 可观察和接管，不声称恢复旧场景。最后 Scene 的显式卸载返回 Busy，框架不自动创建 neutral 宿主。
- `SceneRequest.Activate()` 只允许激活，不是 `SetActiveScene`。门控从 native 开始计时，默认30秒、合法范围 `(0, 300]` 秒；到期返回 `ActivationTimedOut` 并放行 native。门控中显式卸载返回 Busy，须先激活/取消；不留下本框架无限0.9等待。外部 owner 的无限 native 门控或直接全局销毁 Bundle 不在本 facade 可撤销/恢复的能力内。
- Runtime 使用冻结 Catalog v1 与真实 Scene Bundle；EditorSimulation 使用正式 GUID/path 快照及 `LoadSceneAsyncInPlayMode`，仍由同一 ResourceSystem 管理身份和生命周期，不模拟 Bundle pin。
- Unity 2022.3 传统 `BuildPipeline.BuildAssetBundles` 实测：不同 path 但相同短名称的 Scene Bundle 内部文件均为 `BuildPlayer-Owned`，同时读回被引擎以 `another AssetBundle with the same files is already loaded` 拒绝。本批未更换构建链/Schema，产品链没有以临时重命名副本伪造对这类布局的支持。独立路径夹具使用 `Other/Alternate.unity`，同 path/同名重复 Additive 已真实通过。
- 验证平台为 Windows Editor `2022.3.62f3c1`、D3D11；未新增独立 Player、IL2CPP/AOT、其他平台或关闭 Domain Reload 验收。

### 21.4 实际验证与独立 Review

| 验证 | 实际结果 |
| --- | --- |
| `RealSceneLeaseReleaseDoesNotUnloadOrDestroyItsBundleDependencies` | 正式入口缺失时 RED；实现后真实 Scene、OnDestroy 依赖保活、caller 释放及外部卸载排空 GREEN |
| `CompleteNative85SceneLifecycleRegression` | 最终修复后 1/1，24.385秒；EditMode 入口内执行真实 EnterPlayMode/ExitPlayMode、独立构建/发布的 Scene Bundle，不以 Mock 替代 native |
| 综合路径 | Single/Additive、同 path/短名多 handle、领取/未领取/重复/Scope、显式卸载/join、外部卸载/Single替换、native前后取消/迟到、0.9激活/超时/Busy、失败后后继推进、外部真歧义/准备期非歧义 |
| 实例及退出 | SceneScoped 闲置退休/原场景实例销毁、迁移/DDOL 活实例继续持有 prefab 图；synthetic EndSession 后 native 继续、旧会话阻挡 Init；真实退出失效/解除门控，第二次 Play 原生包为0且可重新加载/卸载 |
| `ConcurrentUnloadsReserveTheLastSceneBeforeNativeDestruction` | 1/1，9.166秒；在途卸载后最后 Scene 明确 Busy，综合再次覆盖 |
| `ExternalSceneLoadedBeforeNativeStartIsExcludedFromTheIdentityWindow` | 真实外部 Editor Scene 在 Bundle 准备期完成；假歧义 RED（9.538秒）→ 唯一新 handle 且外部 Scene 保留 GREEN（9.693秒），最终综合再次 GREEN |
| `ResourceRequestTests` | 5/5，SceneRequest 直接继承的领取/释放基础合同 |
| `YLFramework.Tests.Resource` PlayMode | 36/36，既有 Resource/Request/Lease/Scope 定向回归 |
| `CompleteNative84InstanceLifecycleRegression` | 1/1，20.597秒；只针对 cleanup/EndSession 直接影响回归，不重做历史批次全量验收 |
| `CompleteFoundationAndCoreCompileInDevelopmentAndRejectSystemsSymbols` | 1/1，1.860秒；另核对 Runtime Player 编译图431个源码、无 UnityEditor 引用，不等同 Player 构建 |

- 完成一次 fresh-context 只读独立技术 Review，覆盖身份、不可逆取消、图保活、事件、实例、退出及双主体边界。唯一 P2 为准备期外部 Scene 污染 Observed；真实复现后在 `StartNativeScene` 建立 Before 时清空准备期 Observed，完成定向 RED→GREEN 与最终综合回归，无未解决 finding；未追加形式复审。
- MCP 的 TotalTests/跨 Domain Reload 总 Duration 会包含完整发现树或失真时间；上表按具体执行方法、PassedTests 与方法 Duration 记录，不把发现树总数视为执行数。
- 测试后实查 `Current=null`、draining/Scene cleanup/Instance cleanup 均为0、原生 Bundle 为0，原 `Assets/Scenes/Main.unity` 恢复且干净；自有临时发布/Workspace/Scene/Prefab 夹具清理完成，新增正式脚本及目录 `.meta` 由 Unity 生成、GUID 非空互异。

### 21.5 保护与8.6依赖

- `.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings、Canvas.prefab 起止内容哈希相同。并行 UI Plan `.kilo/plans/1791960935353-ui-panel-redesign.md` 在终检期间由外部从起始 `68f4651...` 更新为 `6496908...`，本轮未写入、覆盖或接管，保留其当前更新。原游戏、XF/XFAB/XFFSM、MainModule、历史 Bundle、StreamingAssets、Packages、其余 ProjectSettings、Core ProjectName 和 Module/View/Panel 正式资源链未改；未保存/覆盖用户脏 Scene，未清理外部拥有对象或产物。
- 8.6 可直接依赖唯一 ResourceSystem 的正式 SceneRequest/SceneLease/SceneRecord、Single/Additive、显式激活/卸载，不需自行按短名找 Scene 或另维持 Bundle 引用。Scope 只聚合 caller 身份；SceneRecord 真实卸载和迁移/DDOL InstanceRecord 真实销毁分别结算已有图责任。
- 后续接入须承接可恢复 Busy、不可逆 Single、有限门控与外部歧义边界。本批到此停止，不自动接入 Core/Audio、退役旧 Resource、合并 integration 或创建阶段8里程碑。

## 22. 8.6a Core 资源消费者与 Editor 接入（2026-10-15）

本节保留8.6a收口时点的历史快照；随后8.6b的当前实现与停止点见第23节。

### 22.1 基线与技术状态

- **8.6a 技术 COMPLETE，已通过用户技术验收，并由本批唯一 Commit 完成 Git 收口。** 起始分支为 `fusion/8-resource`，起始 HEAD 为 `43e43d09813b987aad600dbac1561174dd5e4163`。本 Plan 记录技术实施与验收事实，不写入由提交自身产生的 Hash 或推送字样；推送结果以最终报告与实际远端为准。8.1～8.5的技术验收与Git收口作为已批准前置，不重复实施或全量验收。
- 本批只修改Core实际消费、Resource正式Provider及必要实例目标Scene桥接、Core Editor、对应测试和本Plan。Catalog v1、总体Resource架构、原Audio后端与旧YL Resource均未改造；未在实施阶段暂存、Commit/Push、切分支、合并或创建里程碑。

### 22.2 正式接入与所有权

- `ResourceSystem.BindCoreResourceProvider()` 是应用显式装配入口：先持有配置并等待 `InitializeAsync/TakeResult`，绑定后再启动Module。没有自动初始化或默认XFAB后端；成功Shutdown或会话退出只解绑本authority的Provider，不删除后来替换的绑定。Core源码仍只消费自己的窄合同。
- Provider的桥接请求进入已有Resource请求账本，沿用取消、一次领取、未领取Dispose、领取后责任转移、观察者异常隔离与稳定终态。文本复制后先释放本次AssetLease再交付string；Prefab结果持独立AssetLease，实例checkout另持已有Resource缓存/依赖pin，不依赖请求或临时Prefab Lease继续存活。
- Module配表通过正式ReadText，JSON构造/Model初始化在资源归还后执行；失败回滚、关闭与重启沿用原事务。默认Scene使用正式SceneRequest关联结果，不再用短名或前后Scene扫描猜结果；Core领取值结果后释放caller SceneLease，SceneRecord仍持有依赖直至真实卸载。Module.Close不隐式卸载native Scene。
- 命名View/Panel的Provider在请求接受时捕获；正常Close回自己的InstanceLease，损坏/初始化失败Discard，不再进入XFAB实例池。同步 `InProgress`、不可用或取消/失效不触发基础模块fallback；真实Missing/LoadFailed保留既有fallback次序与原因链。异步等待/join后领取一次，结果迟到或owner失效时释放自己的兴趣和未领取结果。
- 命名实例遵守已有SceneScoped合同，目标父对象/Scene在生产前后重验；不同Scene不复用同一checkout。原“同一实例切换UI context”回归限定在同Scene内，并新增跨Scene隔离回归，不保留跨Scene旧池兼容权威。
- 直接Prefab保持borrowed输入，冷创建直接Instantiate，复用/归还使用既有Foundation GameObjectPool及独立checkout handle；不送进Resource资产生产或XFAB池。Attach及默认Canvas/EventSystem无需正式Provider。冷创建曾被缓存根的世界变换影响Camera Canvas，已由真实指针回归定位并恢复直接Instantiate语义。
- `ProjectName`正式解释为YL Resource ProjectId。新Core夹具位于 `Tests/Core/Fixtures/YLCoreResourceModule`，包含独立ResourceProject、JSON、Prefab与Scene身份；旧XFAB/Core夹具及原MainModule均保持原样，迁移后的测试不再装配旧项目。
- Core Editor只保留 `EditorAssetTool.PreviewResolver` 借用委托；ResourceEditorSimulation在Editor域装配，按正式ProjectId/address/数字subasset解析，不建第二份对象或项目索引。未知/歧义身份明确未解析，定义变化不复用旧缓存。Module生成模板及Help提供配置→初始化→绑定→启动和退出顺序示例，不由生成器接管运行会话或改写资源项目。

### 22.3 实际验证

环境为Unity `2022.3.62f3c1`、Windows Editor/D3D11、正常Domain Reload。native用例复用正式Builder/Publisher、冻结v1及隔离临时发布，通过EnterPlayMode执行真实资源链，不用模拟Provider代替native生命周期。

| 最终定向门禁 | 实际结果与覆盖 |
| --- | --- |
| `YLFramework.Core.Tests.PlayMode` | **189/189 Passed**，含Module/MVC、命名View/Panel、direct、Attach、默认根/输入、fallback、关闭/重启、异常收尾及新增迁移回归；独立Review修复后整程序集重新通过 |
| `YLFramework.Core.Tests.EditMode` | **72/72 Passed**，含Foundation+Core完整Editor与Development/Player条件编译闭包、Systems/XFAB符号拒绝及默认Canvas/EventSystem原数据检查 |
| `YLFramework.Core.Editor.Tests` | **104/104 Passed**，含生成模板/幂等/拒绝覆盖、Editor身份、默认资源与许可保留门禁 |
| `CoreResourceProviderTests` 两个方法分别运行 | **2/2 Passed**：显式绑定/替换/解绑、文本释放与一次领取、真实Play退出后已成功未领取请求保持成功终态但Dispose并拒绝领取 |
| `CoreNativePrefabJoinInProgressCancelAndPanelOwnership` | **Passed**：真实冷AB、同步InProgress不选基础资源、异步join、单一consumer取消、View/Panel复用、领取后请求Dispose、退休active checkout继续pin依赖、最终Shutdown |
| `CoreNativeWaitingAbortOwnerCloseAndPendingSessionExitDrainLateResults` | **Passed**：回调实查Core已持有并等待native请求，native完成/业务接受前Abort或Close无多余实例；另保留确实Pending的桥接请求执行EndSession，Invalidated/Disposed、仅通知一次，迟到native的Bundle/Record/Lease排空 |
| `CoreNativeModuleTextSingleSceneCloseAndRestart` | **Passed**：真实AB文本先归还再Model初始化，Single SceneRecord真实handle及依赖保活、业务关闭不卸载Scene、Busy、Module再次启动、外部卸载及最终关闭；自有fixture SessionState亦清理 |
| Resource Editor模拟定向回归 | 既有 **6项Passed**；新增 `CorePreviewUsesFormalIdentityAndNeverKeepsAStaleAssetOrProjectIndex` **Passed**，覆盖主/子身份、alias、定义失效、重复ProjectId、缺失装配及借用对象不被销毁 |
| 既有Audio命名资源与边界 | `AudioNamedResourcePlaybackTests` **7/7 Passed**，先执行既有独立Audio Bundle准备方法；真实Resources/旧YL AB及依赖卸载、异步/取消/缓存行为未迁移或破坏。`AudioXfabBoundaryTests` **1/1 Passed** |
| Help与导出 | `HelpWindowTests` **25/25 Passed**；`ExportUnityPackageTests` **1/1 Passed**，默认导出/可选leaf边界保持 |
| 原游戏非干扰 | 复用现有 `GameAcceptanceFlow.Run` 在Editor执行，**38项检查、0 Error/Exception**：原Main/XF/XFAB→菜单/BGM→设置、两轮进入Game/暂停/回菜单；新YL直接ColorSelector UI借用原EventSystem，关闭不破坏BGM/输入。既有流程恢复音频偏好、timeScale与frame rate，原场景布局恢复 |

MCP TotalTests是发现树，跨Domain Reload的总Duration/多方法计数会失真；上表使用实际叶方法和最终PassedTests，不把发现量或异常大时长记为执行证据。没有重跑整个历史体系、重建历史Bundle或执行最终Player验收。

### 22.4 独立 Review 与修复

- 完成一次fresh-context只读独立技术Review，覆盖Core生命周期、Lease转移、Provider隔离、兼容行为及双主体依赖。唯一正式代码P2为Provider在返回请求前同步重入Module.Close，结果随后被写回已终态对象，留下未领取Prefab请求。
- `ProviderReturningAfterOwnerCloseDisposesItsUnclaimedPrefabRequest` 先实测RED（返回请求IsDisposed=false），改为局部接收、返回后重验生命周期、失效即归还后相同用例GREEN；最终189项Core回归及native迟到/退出门禁通过。无未处理实质finding，不追加形式复审。
- Review指出的“Core尚未取得后端时取消”与“Pending桥接退出”证据缺口由上述真实native等待用例补齐。两处新增测试问题也已修正：仅null表示测试模块无表，空表名仍失败；已成功请求退出只释放结果，不改写稳定成功终态。
- Core实际metadata不再引用XFAB后，原“必须含XFABManager”断言已翻转；主体asmdef过渡引用未改。Core Editor许可测试原先要求整文件等同上游，基线已有8.2来源前言导致失败；现仍逐字比较完整版权/许可/免责声明及标题，允许已有前言，未修改许可文件或上游声明。

### 22.5 保护、衔接与停止点

- 保护快照最终复核 **1,413个文件与起点一致**，包括原游戏/Scenes、StreamingAssets、MainModule历史Bundle、Packages、ProjectSettings及`.gitignore`/`.kilo/kilo.jsonc`；ProBuilder既有变更保留。Canvas未重复处理，原Core夹具/Prefab/Scene无修改，原Package只读；并行UI Plan未写入、回退或接管。
- 新增14份Unity生成meta、迁移脚本原meta合计15个资产身份已逐项反查，GUID非空互异且新fixture不复用旧fixture GUID。`CoreXfabPlayModeTests` 改名为 `CoreResourcePlayModeTests`，meta blob仍为 `01516429cf803cc4280522d3c5fe0b3bae6d92b4`，未删减旧生命周期断言制造通过。
- 最终Unity实查：非Play/非编译、scriptCompilationFailed=false、Current/Provider为空、draining/Scene cleanup/Instance cleanup均为0、native Bundle=0，`Assets/Scenes/Main.unity`恢复且干净。Runtime Player编译图432个源码，无Editor源码/UnityEditor程序集引用；这不是Player构建、IL2CPP/AOT、其他平台或关闭Domain Reload证明。
- `Runtime/YLFramework.Runtime.asmdef` 与 `Editor/YLFramework.Editor.asmdef` 仍声明XFABManager；`Integrations/XFAB/Audio`及其测试leaf仍直接依赖原Package。旧YL Resource服务/格式/模拟更新与 `ResLoaderAudioClipProvider` 仍在，原游戏继续原链，本批不宣称全框架解除XFAB依赖或旧YL退役。
- 8.6b须独立迁移Audio Provider与配置/生命周期，沿用本批窄合同装配经验但不接管原游戏资产；8.6c须等两类正式consumer完成后退出旧YL authority、同步旧测试/导出并解除主体asmdef引用。过渡期间新Core资源与旧Audio/原游戏域隔离，不把同一资产交给两套authority。
- **到此停止。** 8.6a技术COMPLETE且已通过用户技术验收；本小批代码/测试/必要文档/Plan合为一个Commit 完成 Git 收口，不为补充 Hash、Git COMPLETE 或已推送字样另建 docs Commit。不自动实施8.6b、不合并integration/xf-yl、不创建阶段8里程碑。

## 23. 8.6b Audio Resource Provider 与配置接入（2026-10-15）

> 本节保留技术实施时的验收记录与停止点。随后用户技术验收及单次 Git 收口已完成；8.6c 开始前实际 HEAD、upstream 与远端均为 `b1116085eb4290c6cc202f63b761797e4eac739e`，不再以本节历史“待提交”文字作为当前 Git 状态。

### 23.1 基线与技术状态

- **8.6b 技术 COMPLETE，待用户技术验收及单独 Git 收口授权。** 分支始终为 `fusion/8-resource`，实际起始/交付HEAD均为 `4075bd354be3d1005deef1e7a942e79d4a80dfc3`。8.1～8.5、8.6a沿用已批准成果，不重复验收历史全量。
- 本批只接管正式Audio命名后端、配置/Editor消费及必要测试/说明。保留 `IAudioClipProvider`、`AudioClipLease`、原播放请求/通道/缓存/组件合同；没有改播放调度、Source池、Bundle DAG、Catalog v1、四层架构或Runtime/Editor双主体，没有暂存、Commit/Push或切换分支。

### 23.2 Provider、所有权与配置

- 应用显式 `InitializeAsync/TakeResult` 后取得 `resources.GetAudioClipProvider(projectId)`，再组成 `AudioClipAddress(provider, logicalAddress)`。Provider按具体authority/project复用身份，不自动初始化、不重绑后来会话，不建立第二份资产表、Bundle引用或Clip idle缓存。旧 `ResLoaderAudioClipProvider` 及无引用的脚本meta已由正式后端替换，不留永久转发类型。
- 每次后端成功交付独立 `AssetLease<AudioClip>`，包装原 `AudioClipLease`，交割后Dispose原ResourceRequest。Audio原缓存合并同Provider/key的播放/显式使用责任，最后归还或idle淘汰才释放后端lease。活跃Music、crossfade尾轨、Sound/Voice和命名通道互不提前释放；显式取得的Audio租约跨Audio Shutdown仍由调用方归还。
- Resources是共享borrowed来源，归零只清Resource记录，不调用UnloadAudioData/UnloadAsset/Destroy；AB由唯一Resource资产/依赖pin最终执行Unload(true)。直接Clip和原游戏XFAB取得后传入Clip的路径不取得新Resource责任，仍由外部owner保活。
- Provider只用Foundation UpdateManager观察在途进度，完成/失败/退出收尾始终由原ResourceRequest独立负责。进度观察异常不阻断交付；完成回调异常归还其未接受lease；最终进度回调重入结束会话后重验存活，不交付已经失效的成功lease。Audio关闭后的迟到结果归还原缓存/authority，不启动新宿主；Resource退出继续排空native。
- `AudioClipLibrary` 保留旧枚举数字0～3及直接Clip字段，新增 `Resource=4` 与 `ProjectId`，Key为主资产逻辑地址。旧Resources/AssetBundle不再参与运行时选择，旧BundleName仅留作序列化读取。Library只从已显式初始化的Current取得领域Provider，ExternalProvider仍必须显式提供。
- 全Assets定向检查Library脚本GUID `453c7c7ed1cfe11488b4f970b83c6720` 及GroupPlayer引用，未发现既有Library资产或Scene/Prefab挂载需要转换。原游戏实际使用的是直接Clip及原UnityEvent链，未迁移或改写。
- Inspector提供逐条填写ProjectId/逻辑地址的迁移副本入口：全部旧条目须明确映射，拒绝覆盖既有asset或遗留meta，不推断BundleName，不改源配置/直接Clip/权重/选择规则；新副本由Unity生成新GUID。不支持通过string key表达SubAsset身份，转换明确拒绝丢弃SubAsset。Inspector/Help、Audio说明及Core生成注释同步正式初始化与关闭次序，未创建真实游戏配置。

### 23.3 实际定向验证

Unity `2022.3.62f3c1`、Windows Editor/D3D11、正常Domain Reload；**本轮共134个不同叶测试Passed**，重跑同一方法不重复计数。跨域MCP发现量/Duration/最后一个方法的Summary不作为总数；必要时直接保存并读取Unity原生完整结果树。临时取证脚本及meta已删除，原始XML/摘要留在 `%TEMP%/kilo/Audio86b-20261015-0826-evidence/`。

| 定向门禁 | 实际结果与覆盖 |
| --- | --- |
| `AudioResourceProviderTests` | **3/3**：旧定位不初始化旧authority、显式factory/同项目身份/类型错误/旧会话拒绝；可控ResourceSource只用于确定性在途进度及观察者异常，完成与进度异常后Record归零 |
| `AudioResourceBundleTests` | **3/3**：正式ResourceProject/Builder/Publisher生成两个Audio根Bundle及真实manifest共享依赖；Listener DSP非零、Source采样推进，独立Resource消费者释放后Music/Sound/Voice/命名通道仍活跃；crossfade尾轨单独保活/最终native销毁、idle Busy/Clear、显式Audio租约跨Shutdown、直接Clip借用；取消/Audio Shutdown后新缓存重试、真正Pending的Resource退出及迟到排空；取得lease后的最终进度回调退出，不交付失效结果 |
| native Voice持久化关闭定向复验 | 上述取消/重试方法另以用户Voice键为0的条件复验 **Passed**，每次Shutdown后显式恢复临时Settings，结束恢复原偏好，不新增叶计数 |
| `AudioAcceptanceResourceTests` | **2/2**：首次正式Group播放前已有唯一Resource；中断且Group仍活跃时先关Audio，再关闭Resource，不以Busy覆盖原失败，timeScale/偏好必恢复 |
| `AudioNamedResourcePlaybackTests` | **6/6 PlayMode**：实际Resources同步/异步、关分类等待、暂停/重播、idle、共享取消/迟到/重试、Missing不打断旧Music、自然结束回调窗口、三种loadType/background与尾轨/失活caller。旧AB方法改由上述正式native夹具覆盖，不再断言旧IRes/RefCount/Waiting或修改旧全局Bundle设置 |
| `AudioChannelPlaybackTests` | **11/11 PlayMode**：直接/命名Group、借源、独立通道、回调重入/销毁/取消/恢复及暂停合同保持；沿用本轮对未受随后Provider局部修改影响的有效结果 |
| `AudioNamedLoadingTests`、`AudioGroupTests` | **21/21、14/14**：原请求/缓存/租约、最新独占提交、代次与通道/素材组行为回归 |
| `AudioGroupAuthoringTests`、`AudioHelpTests` | **8/8、12/12**：新旧字段round-trip、显式副本新GUID/输入不变/拒绝缺映射和覆盖、Editor不初始化会话、实际API/Help消费 |
| Audio/Core编译边界 | `AudioSourceIndependenceTests` **3/3**、`CoreArchitectureBoundaryTests` **3/3**：Editor与Windows Development辅助源码闭包，Audio+正式Resource不含旧Res/XF/XFAB程序集，Core/Foundation不反向依赖Systems；不是Player构建 |
| Core生成、通用Help | `CoreModuleGenerationTests` **33/33**、`FrameworkHelpProviderTests` **7/7** |
| Core资源必要回归 | `CoreResourceProviderTests` **2/2**：绑定/复制文本先归还/替换保护/Shutdown解绑、真实退出后成功未领取结果失效并拒绝领取 |
| 原游戏及可选leaf边界 | 既有 `YLAudioWiringTests` 五个方法 **5/5**，只读核对原Clip GUID、BGM组件及UnityEvent；`AudioXfabBoundaryTests` **1/1**。原游戏运行链复用未受本批命名后端改动影响的8.6a证据，不声称重跑完整游戏或新Player |

关键原生结果树为 `639276512606786194.xml`（AB三项）、`639276520473533647.xml`（Voice=0重试）、`639276507319043473.xml`（验收入口两项）、`639276519379927017.xml`（Core两项）、`639276521261228136.xml`（Resources六项）；Provider三项/进度最终复验分别为 `639276508404078780.xml`、`639276519600688433.xml`。最终状态读取发生在移除临时取证脚本并重新Refresh/Unity编译之后，没有把辅助闭包或IDE诊断当作Unity运行证明。

### 23.4 独立 Review 与修复

- 完成一次fresh-context只读独立技术Review，覆盖Clip所有权/缓存/尾轨、配置兼容、取消/重入/退出、测试证据与程序集保护；**无Critical，2项Important及3项Minor均已处理，无遗留实质finding**，未追加形式复审。
- 两项Important均在共用验收入口：Resource初始化原在首次Group之后；失败finally中的Busy断言跳过环境恢复并遗留Ready authority。新增用例先确认初始化顺序RED，前移唯一初始化后第一例GREEN、活跃Group中断例继续因Busy RED；再改为先关闭本入口拥有的Audio、Resource请求独立收尾、环境恢复置于finally，两例最终GREEN。
- 进度退化按既有请求合同处理：先测在途无分数值RED，增加只观察进度的Foundation订阅，连续值/终态/观察者异常释放GREEN。最终进度仍通知并重验交付责任，不把进度1当成Resource终态。
- 原生存活断言统一使用Unity fake-null语义，避免managed非null假阳性；native重试每次Shutdown后重建临时Settings，并实际以Voice持久化关闭复验。实现期间另行RED复现并修复“进度回调结束Resource后发布失效成功lease”，最终真实交付窗口及排空门禁GREEN。

### 23.5 保护、8.6c衔接与停止点

- native夹具在正式构建前及回收时比较原游戏/Scenes、StreamingAssets、MainModule历史Bundle、Packages和ProjectSettings的文件名/内容摘要，完整结果树通过；最终tracked diff未涉及这些路径、Canvas及其meta，只有原有ProBuilder Settings变更保留。`.gitignore`、`.kilo/kilo.jsonc`、并行UI Plan及原Package保持原样，未接管外部改动。
- 4份新增脚本meta加1份重命名测试meta均经AssetDatabase反查，GUID非空互异、路径正确；`AudioResourceBundleTests.cs.meta`保留旧准备测试的 `a5fce071bd14b1e43b4e2e98d0910092`。临时WAV/Prefab/ResourceProject、工作目录、发布和对应SessionState由自有夹具清理，Audio fixture目录恢复为原两份Mixer及meta。
- 最终Unity实查：非Play/非编译、scriptCompilationFailed=false、Resource Current/Core Provider为空、native Bundle=0，`Assets/Scenes/Main.unity`恢复且干净；Runtime Player编译图432个源码、无Editor源码或UnityEditor引用。未执行历史全量、Player/IL2CPP/AOT、其他平台或本轮新增关闭Domain Reload验收。
- 8.6c仍须处理旧 `Systems/Resource/Framework` 的ResLoader/ResKey/ResTable/ServiceManager/ResFactory、`Implementations`、旧ResManager/数据与配置工具及其Editor/测试/导出哨兵，并据实际引用解除主体Runtime/Editor asmdef的XFABManager声明。本批已完成Audio运行时与这些旧服务的接点移除，不等于旧YL Resource整体退役。
- 正式 `ResourceSystem`、新Catalog/Lease/Bundle内核、Core/Audio Provider及新Editor/测试不得混入旧栈清理；Library旧枚举/BundleName是持久化迁移数据，不是可恢复的旧authority。原游戏、原XF/XFAB/XFFSM及明确可选的 `Integrations/XFAB/Audio`借用leaf不属清理目标。
- **到此停止。** 等待用户技术验收与本批单次Commit授权，未来Git收口把代码/测试/必要文档/本Plan合为一次Commit，不为Git状态再建docs Commit。不实施8.6c或8.7～8.9，不合并integration/xf-yl，不创建阶段8里程碑。

## 24. 8.6c 旧 YL Resource 退役与主体 XFAB 依赖解除（2026-10-15）

**技术状态：COMPLETE，待用户技术验收与本批单次 Git 收口授权。** 必要门禁均有真实通过证据，发现的问题已定向修复或在正确隔离条件下复验；本轮未Commit/Push、集成合并、创建阶段里程碑或实施8.7。

### 24.1 基线与退役边界

- 分支 `fusion/8-resource`，起始 HEAD 为8.6b实际收口提交 `b1116085eb4290c6cc202f63b761797e4eac739e`；upstream为 `origin/fusion/8-resource`，`git ls-remote --heads origin refs/heads/fusion/8-resource` 确认远端相同，未补交前序代码。
- 起始外部修改为 `.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings，另有未跟踪UI Plan；暂存区为空。全部保持，原Package/游戏/MainModule/历史Bundle/StreamingAssets/场景Prefab/Canvas/Packages与ProjectSettings不属本批改动。
- 先审查旧Runtime41文件GUID与全部91份Assets序列化候选文本，未命中引用；Editor `SubProjectData` GUID另查无引用。游戏EffectController的同名GameObjectLoader来自XFGameFramework，不是被删YL类型。未将“无C#引用”单独当成可删除依据。
- 删除旧 `Resource/Framework`、`Implementations`、`HotUpdate`、`Utilities` 及 `ResManager/GameObjectLoader/SubProject`；旧ServiceManager/引用计数/池化Key/二进制表/Bundle加载、图片入口及假更新栈不留永久转发。旧Resource专用包装随闭包退出，Foundation Pool/Table/Serialization、自有ZipTool与正式图片Source保留其独立职责。
- Editor仅退出旧 `SubProjectData` 和仅服务它的owner/Bundle命名校验；`ResourceBuildOperation.ValidateAssetPath/RejectLinks` 保留给正式Builder/Publisher/生成。正式Workbench窗口及GUID、Catalog Schema v1、Core/Audio正式Provider和生命周期实现不改。
- `AudioClipLibrary` 旧枚举数字与隐藏BundleName、全量显式映射的新GUID副本迁移入口保持；旧配置不能重新激活资源authority。

### 24.2 主体编译隔离与源码证据

- 起始实际Unity编译图除显式XFABManager外，还自动注入原XFAB包内 `ICSharpCode.SharpZipLib.dll`。两主体均改 `overrideReferences=true`，唯一预编译插件白名单为 `Newtonsoft.Json.dll`，同时移除XFABManager asmdef引用；不修改原包或全局插件设置。
- 新增 `FrameworkAssemblyIndependenceTests`：真实Editor/Player编译图递归检查、主体直接预编译输入检查、Editor已加载程序集的递归AssemblyRef检查，以及完整Runtime/Editor源码重编译；不是仅搜索asmdef文本。源码清单与Unity图严格等价，Editor引用本次隔离新构建的Runtime，不借旧YL产品或optional实现DLL。
- Unity `2022.3.62f3c1` 实测Runtime391源码、Editor58源码。6/6通过，原生结果 `C:/Users/Kano/AppData/Local/Temp/kilo/Resource86c-20261015-0c314fd7-evidence/639276568531818303.xml`。包含Windows64 Development的完整Runtime源码profile，不宣称执行本批Player构建或干净工程导入。
- 验证探针保留了真实RED：三份旧编译图因XFABManager失败、旧authority类型存在失败。AssemblyBuilder调试另发现默认引用与Editor .NET4.8不符，以及exclusion在additional合并后过滤；最终使用当前真实profile的合法白名单差集，保留BCL/UGUI/Json.NET，未补入XF/XFAB或放宽产品白名单。
- `YLFramework.Audio.XFAB` 继续作为独立可选叶子：单向引用Runtime/XFABManager，`autoReferenced=false`、包版本与编译条件保持；不自动注册、不进入默认导出，也不是主体必要依赖。

### 24.3 定向验收与Review修复

- 已迁移旧Importer测试至正式ResourceProject/BuildMap边界测试；旧ResourcePool实现断言退出，池拒绝/分配失败/非法地址/初始化回滚由既有正式用例覆盖，另补ResourceScope多清理错误聚合、健康lease归还和不重放的PlayMode覆盖。
- 导出哨兵改指正式ResourceSystem/Configuration/Core/Audio入口，明确禁止旧树/旧文件；现有Resource说明、架构/首页/Singleton/Table及Core/Foundation Help同步正式入口，不保留失效旧菜单说明。
- 首批14类定向Editor/构建发布/生成/显式Audio迁移/原游戏wiring共63例，原生 `639276574167521262.xml` 为62绿、1个Workbench未稳定刷新拒绝；未更改产品保护或测试断言，稳定后该实际GUI用例独立1/1绿（`639276575750889615.xml`）。不是将最初失败直接记成通过。
- 独立Review完成：未发现P0/P1或结构性误删/隐式依赖；一组P2为HelpWindow/CoreEditorIdentity两处仍要求旧XFABManager的精确契约。两用例分别真实RED（`639276575487678330.xml` / `639276575563560535.xml`），保留GUID/身份/平台断言后精确迁移为Runtime/UI、`overrideReferences=true`、唯一Json.NET白名单，并收紧旧XF放行。修复回归 `639276577155249964.xml` 中两者及公共Help程序集检查均绿。
- Review指出的Workspace/发布路径混写及直接受退役影响的使用导航已定向修正，未重写IntegrationPlan历史；不以“未使用”为理由删除通用Pool/Table/Zip能力。
- 修复回归附带ObjectPool、Help提供方、实际导出、Workbench及Audio源码门禁共62例，原生结果60绿、2个Audio源码Builder因编译忙未启动。确认Editor及AssemblyBuilder均idle后独立复跑Audio源码门禁3/3绿（`639276578103211486.xml`），保留原Build-start断言及插件白名单，不绕过编译。
- native代表性门禁11/11绿（`639276581977896646.xml`）：真实Text/Prefab/SubAsset共享与最终卸载、实例复用与隐式创建失败归还、SceneLease与真实场景pin、Core命名Prefab/Panel共享取消和关闭/退出迟到排空、Module文本/Single场景/关闭重启，以及三项真实Audio Bundle保活/取消/实际交付窗口退出。复用自有Publication与保护摘要，不机械重跑83/84/85完整复合门禁。
- PlayMode首批49例为48绿、1个Single场景测试被保护门禁拒绝（`639276583343168967.xml`）：原用户Main场景的Virtual Camera不是测试runner对象，守卫在卸载前拒绝继续，产品未改。原游戏MainStart/Settings/GameExit/reentry、可选XFAB借用适配、正式ResourceSystem/Resources Source、Core代表性View/Panel、命名Audio播放/缓存、直接Clip借用与新增Scope错误聚合均已绿。保留守卫，在既有空Harness独立复验Single启动成功与失败回滚，两项2/2绿（`639276587402549842.xml`）；未覆盖未保存场景、未将最初失败记为成功。
- 上述计数包含交叉及单项复验，不机械相加为无重复总数。跨Domain Reload时原XF Logger/MCP仍出现既有退出/断连噪声，结果以独立保存的Unity原生树为准，不把断连计作成功。完整证据目录为 `C:/Users/Kano/AppData/Local/Temp/kilo/Resource86c-20261015-0c314fd7-evidence/`，保留RED与环境拒绝的原始结果，不以日志过滤代替门禁。

### 24.4 保护、身份与最终状态

- native夹具及构建发布回归对原游戏/Scenes、StreamingAssets、历史MainModule Bundle、Packages与ProjectSettings进行前后保护摘要核对；本批未改动这些目录或Canvas/Prefab及其meta。原游戏PlayMode流程和可选XFAB borrowed适配实际通过，不只是源码静态检查。
- 退役差异为43个旧脚本（Runtime41、Editor1、过时测试1）及54个meta（配套脚本43、已确认空旧目录11），没有缺失脚本配套meta。存续资源、两主体asmdef与Workbench/Help窗口GUID保持；新独立性测试GUID为 `bc9c1a925b25a9847892c5fcdd23c4eb`，经AssetDatabase反查精确返回新路径。
- 临时取证回调仅解绑自有1份，取证脚本/meta已删除并真实刷新编译，旧类型及临时取证类型均不在新加载程序集。仅清理由本轮TestRunner创建且已确认归属的 `InitTestScene639276870751882200.unity` 与meta，不清理未知文件。
- Single复验保存并恢复其入场SceneSetup；前序TestRunner留下的是临时InitTestScene，因此随后按本批起始清单另行恢复唯一活动 `Assets/Scenes/Main.unity`。每次替换前检查无dirty场景、无活动测试、当前场景属于已知测试范围，未保存或重写用户Scene。最终Main已加载、活动且干净，恢复SessionState已清除。
- 临时脚本退出后的Unity最终实查：非Play/非编译/非更新、`scriptCompilationFailed=false`；Resource Current、公开及原始Core Provider均为空，YL AudioManager数量0，native Bundle数量0。Runtime/Editor仍为391/58源码；Player图391源码、0个Editor路径、无UnityEditor或XFAB产品依赖。实际主体图与二进制/独立源码门禁相互补充，不以生成的csproj或grep替代。
- 最终diff只在本批框架/测试/既有说明与本Plan范围；原有 `.gitignore`、`.kilo/kilo.jsonc`、ProBuilder Settings及并行UI Plan保留，未修改这些外部成果。`git diff --check`通过，暂存区为空，HEAD保持8.6b基线，未创建或切换分支、未Commit/Push。
- 本批没有运行历史全量、真正Player/IL2CPP/AOT构建、其他平台、干净工程导入或新增关闭Domain Reload验收；不把Windows Development源码编译等同这些结果。最终独立载体与生产更新安全仍按后续阶段验收。

### 24.5 8.6整体能力与停止点

- 8.6a/b/c现已具备：唯一ResourceSystem的显式初始化/关闭、Catalog/Lease/Source/Bundle依赖图、实例缓存与Scene pin、Core Module/View/Panel与Audio正式Provider、Workbench/Builder/Catalog/Publisher/代码生成，以及旧YL资源栈退出后的Runtime/Editor主体XFAB独立性。没有恢复双资源表、旧卸载driver或永久转发层。
- 原游戏/XF/XFAB/XFFSM保持原链；独立可选XFAB音频叶子不是主体必需依赖。AudioClipLibrary旧字段只保留显式持久化迁移能力，通用Pool/Table/Serialization/Zip等能力不因旧栈退役误删。
- 进入8.7前未发现新增技术阻塞；当前流程前置仅为用户技术验收与8.6c单次Git收口授权。8.7版本激活/回滚、8.8下载安全和8.9 Editor体验均未实施，不把现有本地发布能力宣称为生产安全更新闭环。
- **到此停止。** 当前全部成果留在工作区，后续获授权时按一批一个Commit合入代码、测试、必要文档和本Plan记录，不增加无意义的状态提交。不自动合并integration/xf-yl、不创建阶段8里程碑、不自动开始8.7。

## 25. 8.7 本地版本事务实施（2026-10-15）

**技术状态：COMPLETE。** 必要技术门禁及最终保护/meta核对均已完成，证据见下。用户先授权实施、必要真实 Unity 测试及一次独立技术 Review；2026-10-15 14:06 UTC 追加授权技术门禁全部通过后自动 Commit/Push。仅为8.7将代码、测试、必要 meta 和本 Plan 合为一个 Commit，不另建 docs 状态提交，提交信息参照前两次资源阶段收口；实际Hash与远端同步结果由最终Git回执报告，不在本提交内制造自引用Hash。

### 25.1 基线、契约与实现边界

- 实查分支 `fusion/8-resource`、HEAD `be3dcb8`，最近历史确认8.6a/b/c收口。起始外部修改 `.gitignore`、`.kilo/kilo.jsonc`、`AGENTS.md`、ProBuilder Settings 及未跟踪 UI Plan 保留，不作为本批成果。起始 Unity `2022.3.62f3c1` 无编译错误、无 Resource authority/native Bundle，唯一 Main Scene 已保存。
- 实施复用正式 `ResourceLocalSnapshot`、严格 Catalog v1 codec、不可变 Release、唯一 ResourceSystem 及现有 Asset/Instance/Scene/native 图，不重新引入旧 Resource/XFAB authority。
- Catalog v1 实际没有跨项目依赖字段，继续冻结。8.7 由应用显式选择包含 store 全部项目的确切 `ProjectId/Platform/CatalogDigest` 集合，未变化项目也须列出；不扫描或推断跨 Catalog 包依赖。同一 store 配置闭包精确绑定 owner，配置集合变化不自动接管已有根。
- `ArtifactStore` 使用显式绝对 owned-root；默认根为 `persistentDataPath/YLFramework/Resource`，各项目/平台下有 `versions/<digest>`、`staging/<attempt>`、`parts`、active 与 last-known-good。事务 journal 位于稳定排序首项目的平台目录，记录整个 store 闭包，避免逐项目独立恢复造成混合版本。原发布根只读，原游戏/StreamingAssets/Packages/ProjectSettings及历史Bundle不参与。
- Runtime 的 active/LKG 是自有控制记录，每个项目镜像都携带相同的完整 `ProjectId/Platform/CatalogDigest` 集合。启动先恢复 journal，再整体尝试全部 active、全部 LKG、显式内置闭包；任一镜像不一致或任一版本损坏，拒绝整个候选层，不拼接逐项目 fallback。可恢复的 owned 损坏 active 会按完整有效闭包修复；未知根和完整 foreign 候选明确拒绝。
- 每个版本 Complete 仅认证不可变版本内容和 owned 文件 allowlist，不再承担可变的单个 Prepared 归属。首项目 `prepared/<transitionDigest>.json` 在整组 From/To 全部完成后才发布，保存全部 from/to 身份；重叠 Prepare 有各自独立记录，半完成 Prepare 不进入恢复枚举。记录在激活消费或显式 Discard 前保护所有 from/to，新的纯 Check 不影响已有记录。
- 激活采用保守安全边界：关闭该 store 项目新取得入口，退休自有 idle 实例桶，真实 Asset/Instance/Scene/native 责任未排空即 Busy，恢复入口后由应用释放责任并重试。不强制释放外部消费者，不修改原游戏 Scene；不宣称同项目多版本 native 热共存。

### 25.2 当前验证与待办

- 新入口基线测试真实 RED：原始 ResourceSystem 不含 ArtifactStore/Check/Prepare/Activate/Rollback。
- 首次真实构建五个隔离 Release（A三代/B两代）后，首轮6项中1通过、5失败；首因是 Windows Mono 对过长 staging 文件路径打开失败。已取消额外摘要子目录，以批准的 `staging/<attempt>` 直接承载文件并进行严格完整验证，后续结果待记录。没有放宽文件/摘要断言或原工程保护。
- 取证目录：`C:/Users/Kano/AppData/Local/Temp/kilo/Resource87-20261015-evidence/`。首次 RED 保留为 `initial-red.json`；后续由临时自有 TestRunner callback 保存原生 XML 与叶子结果，跨 Domain Reload 不以 MCP 的全库发现数或错误 Duration 冒充完整结果。临时取证脚本及meta已移除，最终重新导入/编译无误。暂停后的五个既有 owned Release 按确切摘要、Project GUID 和真实 native TextAsset 内容重新验证并显式绑定，见 `resumed-fixtures.json`；没有扫描目录挑“最新版本”。
- 最终完整8.7专项 **23 passed / 0 failed / 0 skipped**，与6项主体独立性合并的原生 XML 为 `639276731155523614.xml`（合并 **29/29**）。覆盖真实 A1/A2/A3 与 B1/B2 Catalog/Bundle、Check 不创建目录、文件复用和空间预算、重复 Prepare 不复制/替换、取消/失败保留旧 active、跨 Domain Reload 恢复 Prepared/active/LKG、already-active/StaleBase/Discard、Asset/Instance/Scene/native Busy 后旧 Acquire 可用、自动 idle 退休、Core 正式 prefab pin、安全切换后真实新版加载及显式回滚、多项目 Busy/坏目标不部分激活、8个实质 commit 边界的整组中断恢复、归属丢失/foreign 候选拒绝、截断 next、整组 fallback 和 owned 清理续跑。额外直接加载同项目native包的真实RED揭示托管图外的阻塞，修复后明确Busy且不卸载或接管外部Bundle，并纳入最终23项GREEN。
- 一次首次 Play 测试失败定位为 TestRunner 跨 Domain Reload 无法保留编译器捕获闭包；将回调捕获的测试主体在进入 Play 后的普通方法中创建，不修改产品初始化门禁。另两项真实失败揭示“恢复到 From 却误报 To 成功”和“失败 fallback 未同步内存”；补真实 native RED 后修复，并纳入上述22项完整 GREEN。
- Editor 最小协作为 `ResourcePublisher.ReadVersion(project, target, appId, digest)`，复用现有 owner/Complete/文件校验只读选择确切历史 Release，不改 `release-index.json`，不承担 Runtime active 权威；已覆盖选A1仍保持最后发布A3且发布根字节不变。未重新设计 Builder、Catalog 或 Workbench。
- 正式 Core/Audio 8项与主体独立性6项回归 **14/14**（`639276723621232274.xml`），含真实Audio Bundle依赖、同步/异步、cancel/Shutdown/迟到清理和Core绑定退出；最终主体源码/程序集闭包又在上述29项末轮中复验。正式 Resource PlayMode 生命周期 **37/37**（`639276726548040319.xml`），覆盖Request/Lease/Scope、初始化重入、取消、旧会话迟到、Resources/Texture所有权等直接受影响底座。
- `final-summary.json` 按原生XML叶子去重确认本批相关验证 **74个不同用例通过**；未默认重跑阶段8全历史。`final-editor-state.json` 确认最终 Main Scene已恢复且非dirty、非Play、非编译、无编译失败、Runner inactive、native Bundle/authority/draining均为0；7个新增脚本和1个目录的8份meta导入/反查正确且GUID互异。自有测试发布、workspace、测试资源与隔离store已回收；原游戏/历史Bundle/Packages/ProjectSettings的保护哈希检查通过，外部修改不编辑、不暂存、不回退。
- 真实Editor进程杀死、断电、Player/IL2CPP和其他平台未验收；本批通过实际Domain Reload和重新初始化验证持久记录恢复，不将它们冒充实机崩溃测试。8.8 HTTPS/签名/HTTP续传/生产防重放均未实施。

### 25.3 独立技术 Review 与修复

- 完成一次独立只读技术 Review，重点覆盖事务恢复、归属、完整性、native 代隔离、Busy 恢复、多项目一致性、重入和代码结构。初审 Request Changes，发现4项P1及5项P2；未以作者自检替代独立 Review，也未宣称 reviewer 对修复后代码作过第二轮批准。
- 9项发现逐项建立真实失败用例：完整闭包含未变化项目的LKG、journal收尾后整组fallback、写前重验owner/journal/stage、截断替换候选、半完成/重叠Prepared发布、版本marker.next恢复、坏active后继续更新、取消stage与中断删除续清、大Catalog空间预算。原生 RED XML `639276703817407478.xml` 为 **0 passed / 9 failed**；修复后的定向 XML `639276712005527542.xml` 为 **9 passed / 0 failed**，随后上述22项全绿。
- 修复统一了状态不变量而非引入第二个 Manager：版本内容/Complete 不可变，Prepared 是整组发布记录，active/LKG镜像是同一完整闭包，journal 是关键写入期间的整组决策。Root marker 明确登记控制槽；仅由有效root/项目/版本归属授权处理未提交截断候选，完整foreign内容不被删除。GC以持久allowlist和最后删除的marker续清，未知文件阻止该目录删除并报告。
- 必要可读性、命名和结构检查纳入同一轮 Review 与修复自检：采用 `ArtifactStore`、`UpdatePlan`、`PreparedUpdate` 等领域名称，事务与文件归属保持在一个 ArtifactStore 的 partial 实现内；沿用现有 ResourceSystem 图，无额外网络层、全局单例或同义 authority。

### 25.4 接入与边界

- 应用显式创建 `ResourceProjectMount(projectId, platform, appId, builtIn, assetTypes)`，将全部参与版本事务的项目交给同一个 `ArtifactStore`，再通过 `ResourceConfiguration(store, otherCatalogs)` 初始化唯一 ResourceSystem。`builtIn` 是按确切摘要打开的本地快照，可以为空；若 active/LKG 均无有效完整闭包且没有完整内置绑定，初始化明确失败。自定义资源类型通过 `assetTypes` 提供编译类型，不按类型字符串创建对象。
- 更新来源是普通文件系统中本地已发布并验证的 Release/owned snapshot。使用 `CheckUpdatesAsync(targetClosure)` 获取只读 UpdatePlan，再用 `PrepareUpdateAsync(plan, cancellationToken)` 完整复制和校验。当前本地IO在调用线程完成，取消通过 CancellationToken 在复制块/阶段边界检查，不宣称后台网络进度、HTTP下载或 Request.Cancel 能中止已经同步完成的复制。
- 应用仅在适当 bootstrap/neutral 场景显式调用 `ActivatePreparedUpdateAsync(prepared)`。Request失败的 `Error.Kind == Busy` 表示旧版本仍可取得，应由应用停止消费者、释放全部外部lease/checkout并卸载自己负责的旧Scene后重试。屏障不会替应用卸载场景、停止Audio或强制归还外部资源；会退休框架自有idle桶。除托管图外还检查同项目Catalog v1 native namespace中已经加载的外部Bundle并返回Busy，不接管或卸载它们；其他代码不得绕过唯一ResourceSystem并发生产同项目原生资源，未登记的外部在途IO不属于可接管事务。
- 成功后新 Acquire 才使用新 snapshot；already-active 幂等成功，不再写指针。旧 PreparedUpdate 的 from 不匹配当前版本即 StaleBase；已消费/Discard的记录不能重新作为受保护激活令牌。`RollbackAsync()` 显式选择整个LKG闭包并走相同屏障，而不是放宽旧Prepared的基线检查。
- 重新初始化时从持久 Complete 版本和整组Prepared记录恢复 `GetPreparedUpdates()`；应用可查看明确from/to后重试。`DiscardPreparedUpdate()` 仅解除该组准备保护。`CleanupVersionsAsync()` 保护active、整组LKG、所有Prepared的from/to、当前托管snapshot及in-flight责任；只按有效owned marker的文件allowlist删除允许清理的版本/stage，返回 `DeletedPaths/ProtectedVersions/Errors`，不能删除未知内容或接管旧XFAB目录。pending journal期间拒绝越过初始化/激活边界进行写操作。
- `RecoveryNotes` 暴露active/LKG候选被拒绝的最小原因。SHA-256、长度、Catalog身份、DAG与精确文件集合是本批的本地完整性门，不是来源签名授权。File.Replace/Flush/journal不构成跨目录绝对断电原子性承诺；恢复仅接受验证通过的完整闭包，无有效恢复候选就明确失败。
- 当前实际验证平台为 Windows Editor/`StandaloneWindows64`、Unity `2022.3.62f3c1`；格式/挂载接受既有v1桌面平台，但未宣称OSX/Linux运行验收。Windows Development profile仅做主体源码编译，不等价于Player构建或平台验收。8.8的HTTPS、签名、远端下载/Range/续传/重试和生产授权，以及8.9整体Workbench体验、8.10最终Player全流程仍未实施。

## 26. 8.8 远端分发与安全验证实施

**技术状态：COMPLETE，已通过用户技术验收；本次单独授权唯一 Commit/Push 收口。** 实施基线为分支 `fusion/8-resource`、HEAD `05add9c8984ae045e56df301b830c67b6c203ff5`，起始外部修改为 `.gitignore`、`.kilo/kilo.jsonc`、`AGENTS.md`、ProBuilder Settings及未跟踪UI Plan，均保留。实施阶段未提交或推送；本次仅按26.6授权进行Git收口，不改依赖和工程设置。验证末尾曾被外部MCP1.0.0/NuGet更新阻塞；用户重启并恢复环境后已完成清理、实际编译/导入检查与16项受影响边界复验，详见26.5。不将本批验收等同于8.10 Player/IL2CPP总验收。

- 沿用第8.2节已批准的 RSA-2048+／PKCS#1 v1.5／SHA-256；签名 envelope 独立于冻结Catalog v1，签名覆盖规范UTF-8的schema/algorithm/keyId及Base64 payload，payload绑定身份、channel、序号、有效期与实际Catalog/Complete文件长度摘要。Catalog继续是Bundle/SupportFiles唯一清单。
- 发布序号门槛纳入既有active/LKG镜像与整组journal，不新建第二个持久版本权威；既有8.7 v1控制记录有具体持久兼容需要，新增控制格式显式版本化。Check只读，激活失败不推进，显式本地回滚保留最高已确认门槛。
- 实施开始时实时Editor为Windows Unity `2022.3.62f3c1`、非Play/编译、Main已保存、native Bundle为0。初始RSA BCL探针 SignData/VerifyData SHA256/PKCS1往返成功，但首轮安全测试揭示 `RSA.Create()` 后设置KeySize=2048在此Mono实际仍导出1024位，产品正确拒绝；测试改用 `RSACryptoServiceProvider(2048)` 并断言实际Modulus长度。Standalone配置读回 `NET_Standard_2_0`、`Mono2x`；不据此宣称Player/IL2CPP或其他平台验收。
- Ruling: 复用本Plan作执行/验收记录，不另建重复ledger；原地授权分支实施，不创建新worktree；可选archive本批不启用。生产支持范围以本批真实验证的平台为准，未验证后端须明确失败而非静默降级。

### 26.1 实现与信任边界

- 应用通过 `ResourceProjectMount(..., remote: ResourceRemoteSource)` 显式绑定HTTPS目录端点、channel、公钥KeyId/Modulus/Exponent和最低发布序号。没有生产默认公钥，不从网络接纳公钥，不将测试身份设为默认信任根。`CheckRemoteUpdatesAsync()` 只认证发布metadata和Catalog、检查本地已确认门槛并返回既有 `UpdatePlan`；没有store时不会创建目录。
- 独立canonical envelope v1绑定schema/algorithm/keyId及Base64确切payload；payload绑定ProjectId、Platform、Unity/App兼容、channel、单调序号、CatalogDigest/长度、BuildId、Complete长度/SHA-256和有限有效期。严格拒绝重复/额外字段、非canonical编码、弱RSA密钥、身份不符和错误签名。有效期最长31天，issued容忍本地时钟5分钟偏差，过期拒绝Check/Prepare/首次激活；已经激活的离线完整版本不因发布metadata过期而被强制卸载。
- `PrepareUpdateAsync(remotePlan)` 仍由唯一ResourceSystem与ArtifactStore驱动，在既有主线程tick中使用UWR；每store一个网络流水线、文件GET串行、同计划消费者join。单个消费者取消不撤销其他消费者；最后取消或会话退出同步撤销私有UWR、关闭文件及store锁，复制块、发布边界和成功结算前重新检查共享存活。
- 默认HTTPS正常证书校验，自动重定向禁用；端点和相对URL不允许HTTP、凭据、query/fragment、转义和越界路径。仅Editor内部的loopback精确证书pin用于隔离测试，不是生产跳过证书校验开关。metadata最大64KiB、Catalog沿用16MiB门限，文件/总量/timeout/idle/attempts均有明确界限；空间检查是保守预算，不是磁盘空间预留。
- `parts` 使用root OwnerId、项目/平台、CatalogDigest、文件身份、长度、SHA、来源URL和强ETag绑定；offset以实际文件长度为准。只有匹配206/Content-Range/ETag才追加；200安全清零重下、416或目标响应变化有限重试回退，无强ETag不续传，编码和错误长度拒绝。实际完成长度、摘要及签名授权通过后才复用8.7 staging/Complete/Prepared；partial记录始终为Partial，不参与挂载。显式Cleanup只清理已注册自有partial，未知/foreign内容保留并报告。
- 最高已确认序号随同一整组journal与active/LKG镜像提交；失败不确认新序号，显式本地rollback不降低历史。既有8.7 v1控制记录仍可读取；同From/To Prepared可原位升级/续签，保留稳定transition身份和已有授权，不允许静默降低Prepared授权序号。它不是防本地篡改的可信存储：首次安装、状态丢失/恶意回滚、本地时钟倒退和有效发布冻结都不能据此宣称强防回滚；生产须由应用最低序号和可信配置补足其实际威胁模型。
- Editor新增最小“签名发布元数据”入口，读取当前已经发布且重新验证的制品，不构建、不上传、不激活。`Create(RSA)`支持外部安全签名提供者；`CreateFromEnvironment`只读取工程外 `YL_RESOURCE_SIGNING_PFX` 与 `YL_RESOURCE_SIGNING_PASSWORD`，不保存或打印私钥。Export新增文件、不覆盖已有文件。部署者将metadata放在endpoint的 `release.json`，按 `<endpoint>/<CatalogDigest>/` 原样部署完整版本内的Catalog/Complete/Bundle/SupportFiles；不改Publisher既有release-index或Runtime active。

### 26.2 当前验证记录

- 原生结果目录：`C:/Users/Kano/AppData/Local/Temp/kilo/resource88-evidence/`。临时自有回调输出XML及叶子JSON，不以MCP错误TotalTests或跨Domain Reload的Unknown summary冒充结果。所有测试私钥/PFX均在该机器自有Temp隔离空间或内存，不修改证书信任/网络配置，不上传游戏资源。
- RSA明确2048位测试与签名拒绝 **2/2**；HTTPS/响应/路径首批 **6/6**，包括真实自签名默认拒绝、显式loopback pin成功、有限503/429重试、302不跟随、错误长度/编码/截断/摘要拒绝。Mono `CertificateRequest.CreateSelfSigned` 实际不支持，测试PFX由外部.NET在隔离Temp生成，Unity以EphemeralKeySet导入并取得真实2048位私钥成功。
- Editor签实际制品、Runtime验签及错误身份/过期/兼容性拒绝 **1/1**：`639276844339784257.xml`。签名不改变原Publisher产物。Windows Unity默认系统证书校验额外真实读取公开CA端点 `https://example.com/`，`CertificateHandler == null`、HTTP200、577字节、正常成功，见 `system-tls.json`；不是Mock或关闭验证后的结果。
- owned partial的首个实际失败暴露Json.NET string-null与JSON null token不等导致归属比较误拒，显式Null修复；随后Cleanup用例真实RED（`639276849036716509.xml`）证明旧GC不回收partial，新增同一store归属清理后GREEN。
- 取消后的六种真实TLS续传终态（206、忽略Range的200、416、ETag改变、无validator、错误range）、owned GC、常规floor失败恢复/rollback已通过。真实连接在200之后中断的测试揭示ConnectionError不应仅按状态码判重试，修复后实际自动Range成功。大磁盘预算拒绝不分配巨量数据，active/LKG不变。
- 新增native流程首次失败为测试未退休仍持有prefab pin的空实例桶；保留断言并按既有8.5合同显式清桶。后一轮8项为 **7 passed/1 failed**（`639276867115271077.xml`），唯一失败是新增EditMode测试直接yield AsyncOperation；改为逐帧等待相同operation完成后已纳入最终38项GREEN，不把前一轮写成全绿。

### 26.3 独立 Review 与回归

- 已完成一次新鲜上下文独立只读技术Review（不调用Unity、不修改源码、不提交），初审Request Changes：1项P1、2项P2，无P0。没有要求或宣称第二轮review批准。
- 三项均建立真实失败回归，`639276857709085591.xml` 为 **0 passed/3 failed**：有效floor控制记录尚可读但内容双坏时失败恢复丢失floor；8.7 v1 Prepared/同目标续期不能升级签名授权；同步From复制checkpoint取消唯一消费者却结算Succeeded。
- 修复后上述三项在 `639276867115271077.xml` 中全部GREEN：恢复失败将原floor落回已验证From，不确认新序号；同一Prepared记录分离稳定transition与可刷新授权，原位升级/续期且不丢更高序号；共享存活探针在块/阶段/结算检查所有waiter，单个取消不误杀其余消费者，并覆盖会话退出后不得迟到发布Prepared。
- 合并原生run `f55f5e1b-963d-432e-bc46-1416ce849870` 实际为 **58 passed/2 failed/0 skipped**（`639276876629462371.xml`），不是全绿。分别修复了重定向限制被UWR归类ConnectionError导致重试，以及8.7大Catalog测试错误修改排序首个资产、破坏SubAsset/main共享Aliases的测试数据；仅改为明确的 `entry.txt` 主资产，不放宽Catalog校验与原空间预算断言。
- 另一个补充安全用例真实RED（`639276881056699482.xml`）证明已有owned版本可能跳过签名声明的Complete身份。Check、Prepare和Commit现均复验签名声明的Catalog/Complete长度与SHA，不再只因CatalogDigest相同而复用。对应三边界回归GREEN并纳入最终38项。
- 实际Mono还接受了公钥指数1，不能仅凭ImportParameters认定信任锚参数有效。新增真实RED（`639276888722046101.xml`）、配置端拒绝非canonical/零/偶数/小于3的退化指数，最终签名3项GREEN；标准65537与合法密钥流程不变，不实现自有签名算法。

### 26.4 最终证据、生产配置与历史收口阻塞

- 本批相关 **62个不同用例取得通过证据**，不是声称同一轮62/62：版本事务/远端安全 **38/38**（`639276885182833208.xml`）；签名/信任锚 **3/3**（`639276889613297785.xml`）；真实HTTPS/响应 **7/7**（`639276889721149946.xml`）；完整Runtime/Editor源码及程序集主体独立性 **6/6**（`639276889824650285.xml`）。Core/Audio正式消费者 **8/8** 取自上述合并60项中对应的原生通过叶子，之后仅改动远端重试、远端Complete复验、信任锚参数和一项空间测试数据，不影响这些未配置远端的消费者路径。聚合记录为同目录 `verified-summary.json`，不是新的测试执行。
- 全部8.7原有23项包含在上述最终38项；8.8涵盖签名/身份/过期/兼容拒绝、真实TLS证书、重定向/有限重试/超时、取消与断线续传六种终态、未知partial归属、篡改Catalog/Complete/Bundle拒绝、空间预算拒绝、多项目闭包、native Asset/Instance/Scene Busy与激活回滚、同步取消/join/退出迟到、真实Domain Reload和签名Prepared/floor持久恢复。没有默认重跑阶段8全部历史或构建Player。
- 已成功调用本批自有fixture teardown，GUID核对、原工程保护hash断言、native Bundle为0断言全部通过；保留的正式A/B测试发布、workspace、全部SessionState登记的隔离store和两份fixture已回收，Main Scene恢复且当时非dirty。临时取证脚本及meta已移除；其移除后的Unity实际编译/导入在环境恢复后确认通过，见26.5。
- 阻塞实证：验证完成并进入收口后，外部将 `Packages/manifest.json` 的MCP `0.93.4` 改为 `1.0.0`，并更新 `packages-lock.json`、`Assets/Plugins/NuGet` 的MCP/Rql二进制及meta。没有由本批执行安装/升级/回退。诊断时Editor.log报5项CS1061：Cinemachine.Get、Navigation.Get、Tilemap.Get、ParticleSystem.Get/Modify调用的 `ComponentRef.Matches` 在新依赖中不存在，随后出现Burst的Assembly-CSharp-firstpass解析下游错误。诊断时旧有Unity PID20956仍响应，但MCP两个既有本地端点均拒绝连接；官方wait-for-ready等待120秒仍失败。最后CLI已检测到Editor变为PID26992，两个端点仍未连接，本批没有启动或重启Unity，亦未跨此新进程复验。此为实际编译/服务阻塞，继续定时等待不能代替兼容修复。
- 为保护外部改动，没有回退、修改新Package/NuGet、补丁PackageCache、开第二个Unity或改权限/网络/信任设置。以上62项原生证据来自升级前的MCP0.93.4环境，环境恢复后另外定向复验16项，不把历史证据改写为新环境执行。
- 恢复前登记的剩余自有产物现已按26.5全部清理：fixture `Release82b-4e335b5a384a4561b12bb068049329a5`（folder GUID `36cdc30fc784cc94999fd7e614e04103`、Project GUID `9ae18359444e4404c997f7800f40d95f`）与 `Release82b-0c2b353414514d14b9fa75395d12d56b`（folder GUID `325a0c46f5d15ec41b0e3170ab89a869`、Project GUID `f4a2620772b084649b68daae0ea4b347`）；workspace分别为 `AssetBundles/YLR_9ae18359444e4404c997f7800f40d95f_6c5c41b7eb9cd413841bff0b8c2f2403`、`AssetBundles/YLR_f4a2620772b084649b68daae0ea4b347_3d618cd43db4203c057d52ebe2bce7af`；发布根为 `AssetBundles/YLFramework/yl-release-78dc97659cab4d2899610a8ec68360c9/StandaloneWindows64`。清理前重新核对GUID/marker和实际文件allowlist，未清理旧游戏或未知目录，没有无条件重跑全部阶段技术验收。
- 生产仍须显式提供：工程外安全签名密钥/HSM、应用固定的生产公钥与可信KeyId、明确的HTTPS发布端点/channel、符合实际威胁模型的最低序号/本地历史与时钟策略、按不可变digest目录部署并由签名metadata绑定的完整产物。没有生产服务器或私钥时本批仅完成隔离测试，不上传真实游戏资源。真实Player/IL2CPP/其他平台仍在8.10；当前未验证后端明确Unsupported，可选archive未启用。
- 安全测试的PFX经 `YL_RESOURCE_TEST_PFX` / `YL_RESOURCE_TEST_PASSWORD` 显式提供，仅在自有Temp中使用；其精确loopback信任不进入生产默认。缺少测试身份时fixture明确失败而不是跳过或关闭证书校验。

### 26.5 环境恢复后的最终收口

- 用户于2026-10-15重启Unity后，既有MCP配置端点恢复。新环境为Unity `2022.3.62f3c1` / MCP `1.0.0`；实际读取 `EditorUtility.scriptCompilationFailed == false`，非Play/编译/导入、Main Scene非dirty、native Bundle=0、ResourceSystem authority为空。旧MCP会话/部分工具注册不可直接沿用，使用现有CLI的已注册 `script-execute` 完成只读检查与自有产物清理，没有启用工具、修改权限或配置。
- 两份fixture先按目录GUID/Project GUID、实际ProjectId及代码中明确的fixture资产白名单核对；workspace按ProjectRoot/ProjectGuid/ProjectId与KnownFiles核对；发布根按owner/KnownVersions检查并经 `ResourcePublisher.ReadVersion` 严格复验三个确切版本。空 `.staging` 经检查确为空后才删除；所有归属检查在递归清理前完成，未知内容会被拒绝。只删除上述两份自有fixture、两份workspace和一个自有发布根，记录 `post-restart-cleanup.json`；Main Scene设置未改变。
- `AssetDatabase.Refresh(ForceSynchronousImport)` 后，19份新增脚本均有meta、可加载为MonoScript、GUID反查路径正确且19个GUID互异；两份fixture及其目录meta、临时取证脚本及meta均已不存在。实际编译失败标志为false，Main非dirty，native Bundle=0，记录 `post-restart-import.json`。
- 对环境变化直接相关的密码学/真实HTTPS/主体源码与程序集闭包定向复验 **16 passed / 0 failed / 0 skipped**，原生XML/叶子JSON为 `post-restart-validation.xml` / `post-restart-validation.json`。包含签名/信任锚3项、下载响应与真实TLS7项、完整Runtime/Editor及Windows Development源码/二进制闭包6项。该16项是62项的重复复验，不累加为78个不同用例；未重新构建五代资源或默认重跑阶段8全历史。
- 测试身份只在当前Editor进程临时配置，回调完成后恢复原环境变量并注销自有TestRunner callback。没有在Assets中重新创建取证工具；原生记录保留于自有Temp证据目录。主体Runtime/Editor无新MCP依赖，用户的Package/NuGet、`UNITY_MCP_DEPS_10`与其他工程设置变动保留不编辑。
- 最终 `post-restart-final-state.json` 实证：非Play/编译/导入、无编译失败，Main为唯一已保存active场景；native Bundle、authority、draining、remote jobs均为0，自有TestRunner callback已注销释放，测试环境变量已恢复。`git diff --check` 本批范围通过，19份新增脚本/meta齐全，HEAD仍为授权基线；没有遗留此前登记的两份fixture或临时取证源码。
- 8.8安全边界及生产配置仍以26.1/26.4为准；WindowsEditor之外后端明确Unsupported，8.9/8.10未提前实施。上述实施与技术验收阶段保持原分支与HEAD，未暂存、提交或推送；后续Git操作仅按26.6的单独授权执行。

### 26.6 用户技术验收与 Git 收口授权

- 用户于2026-10-15确认8.8技术验收通过，正式授权本批唯一Commit与Push；不重新实施功能，不重跑Unity测试、真实下载/签名测试、Bundle构建或独立Review。62个不同用例及重启后16/16的有效证据沿用，不变更Windows Editor支持范围、生产部署要求与Player/IL2CPP/AOT及其他平台限制。
- 仅纳入本批可信发布/数字签名、安全HTTPS与Range续传、8.7事务直接协作、Editor签名入口、正式测试/必要夹具/meta及本节Plan记录。私钥、凭据、临时证书、服务器日志、下载缓存、临时发布与取证文件不得入库；外部Package/NuGet、原工程及用户并行修改不纳入本次提交。
- Git收口不改变产品行为：仅补齐本节授权/技术COMPLETE记录，并去除19份新meta空字段的行尾空格以满足暂存差异检查；GUID及导入字段取值保持不变，既有技术证据继续有效，不重复技术验收。
- 指定唯一提交消息：`feat(resource): 完成8.8远端分发与安全更新`；推送至 `origin/fusion/8-resource`。实际Hash、推送结果和本地/upstream/实时远端一致性由完成回复报告，不为回填Hash或Git COMPLETE追加第二个docs提交。
- 完成本次Git收口即停止；8.8作为8.9的已验收前置基础，不自动实施8.9，不合并 `integration/xf-yl`，不创建阶段8里程碑或Tag。
