# XF × YL 阶段 8：Resource 深度融合与全面升级总体 Plan

> 状态：**APPROVED**。架构 B、D1～D8 总体方向及 8.1～8.10 路线已批准；8.1、8.2a、8.2b 已技术验收及 Git 收口，见第14～16节。8.2c 技术 COMPLETE、Schema v1 已冻结，等待用户技术验收及单独 Git 收口授权，见第17节；未实施8.3及后续批次。
> 两项审核修订已落实：8.1 只稳定资源身份、Request/Lease/Scope、所有权及最小只读 Catalog 内存模型，持久化 Schema 在 8.2 真实构建闭环后冻结；8.2/8.7 只宣称本地或内部受控交付，生产远端更新须通过 8.8 安全门禁，并如实记录签名、防重放与状态丢失限制。
> 原方案批准与 Git 准备已完成；各批 Git 收口以对应单独授权为准。8.2c 本轮只授权实施、必要验证及一次独立技术 Review，不授权 Commit/Push、集成合并、阶段8里程碑或自动开始8.3。
> 实施代理：仅在用户对具体实施批次明确授权后，使用 `subagent-driven-development` 或 `executing-plans` 按下文门禁推进；不得因为 Plan 已批准而自动开始。

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

Canvas外部变更不改变本文Core资源取得/释放链或新旧身份隔离裁定，但过去对该Prefab序列化值/UI运行效果的验证不能视为当前仍有效。现有`CoreDefaultResourceTests.CanvasPreservesOriginalSerializedDataExceptYLAdaptationBinding`整文件等价断言（源码70-76行）与最新dirty资产静态不符，属于本轮之外的变更影响；后续须先确认该外部改动意图再复验相应契约，不能为阶段8过关而静默削弱断言。本轮不改资产或测试、不还原它。

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

每批共用步骤：建立本批失败/基线用例；实现本批最小闭环；运行本批有影响的门禁；核对diff/新增meta/原数据保护；完成独立review。8.1～8.2b 已收口，8.2c 技术 COMPLETE、待用户验收及 Git 授权，证据见第14～17节；8.3及后续未执行。Git 写入仅按用户当前单独授权，不由本路线自动触发。

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

**8.2b 状态**：**技术 COMPLETE，已完成 Git 收口**，见第16节。**8.2c 状态：技术 COMPLETE，Schema v1 冻结，等待用户验收及单独 Git 授权**，见第17节。本地/内部受控8.2闭环通过，不等于8.8生产远端安全或8.3 Runtime加载通过。

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

- [ ] 真实Text/Prefab/Sprite subasset、typed多资产加载/卸载重载；菱形依赖只产一次，缺包/损坏/未知子名有稳定终态。
- [ ] 依赖首项即时失败及大于并发槽的排队失败不挂起；typed同名并行不串结果，sync遇async无二次nativeIO。
- [ ] 两scope共享资产，一方Dispose另一方存活；失败/最后取消/退出的native迟到完成最终native差集回本系统基线。
- [ ] 本地图片真实大小写、网络错误/坏图/零owner/解码后取消，Destroy仅作用本系统创建Texture；保留source行为而退出WWW机制。

### 8.4 Prefab实例、缓存与Foundation桶协作

**主要范围**：`R/Instances`；复用`F/Pool/GameObject/GameObjectPoolData.cs`，仅有证明必要的通用改动才碰Foundation；对应新实例集成测试。

**依赖/输出**：依赖8.3；可创建、复用、预热、Return/Discard的实例lease，bucket持prefab graph，scene-scoped/persistent显式策略。

**风险**：延迟Destroy、从未激活OnDestroy缺失、桶退休后晚归还、DDOL与Core旧场景行为不同。

- [ ] 正常返回复用同instance、damaged不回池；活/idle/退役晚归还/容量裁剪均在nativeDestroy后才最后释放prefab及依赖。
- [ ] 从未激活预热、外部Destroy、Awake/OnEnable/OnDisable重入、Clear与新Acquire交错、checkout旧handle重复归还，不能误销毁/误还新实例；创建pin与Data回Safe池时机真实可观察。
- [ ] scene unload清idle而不毁迁移/DDOL活实例；新场景桶不复用旧退休checkout；实例计数与native包保活对照。

### 8.5 Scene真实身份、保活与不可逆取消

**主要范围**：`R/Scenes`，8.2自有Scene Bundle/Editor模拟与PlayMode夹具；不改游戏BuildSettings。

**依赖/输出**：依赖8.3/8.4；SceneRecord与准确handle、explicit unload、默认生命周期转交，Core所需Single与Additive结果。

**风险**：Unity全局async queue、Single不可逆、同名scene、last-scene不可卸载、scene对象依赖提前释放。

- [ ] 加载/激活/真实unload前后Bundle与依赖pin准确；释放调用方SceneLease不把已加载scene的材料/资源毁掉。
- [ ] 两个同名Additive scene按handle区分；external unload/Single替换只释放正确scene；不用短名/hash猜结果。
- [ ] 同path重复Additive逐次排队、external同path无法归属时明确报歧义；SceneRequest领取与未领取Dispose、旧SceneLease卸载调用不能释放新一轮handle。
- [ ] native前取消、Additive迟到取消、Single开始后取消、手动0.9激活门控分别验证，无假“已撤销旧scene”，无永久queue阻塞。

### 8.6 Core/Audio/Editor真实消费者接入与旧YL退场

**主要范围**：`C/ResourceAccess,Modules/Requests,MVC/Views,UI`直接资源调用；`A/ResLoaderAudioClipProvider.cs,Config/AudioClipLibrary.cs`；Core Editor预览/生成/Help中资源接入；旧`R`退出清单和主体asmdef的XFAB引用。

**依赖/输出**：8.1-8.5全部门禁；正式Provider与生成启动绑定、新YL fixture配置、主体Runtime/Editor无XFAB符号/程序集依赖，旧Resource服务/格式/模拟更新/旧provider退出。原游戏和可选leaf不迁移。

迁移的是测试/消费代码及其指向的**新自有fixture**。已有受保护Scene/Prefab保留原文件与meta，需替代时创建新GUID副本/新配置并让YL测试显式使用；Audio旧配置若需要转换也先预览输出新配置，不批量保存或重序列化原Scene/Prefab。

**风险**：Core fallback/Close/cache/scene行为漂移、Audio直Clip误卸载、配置identity迁移、旧类型测试把实现固化成永久兼容债务。

**独立子批次**：8.6a仅切Core文本/prefab/scene、实例回程与Core Editor/生成/Help，并过Core闭包/行为门禁；8.6b切Audio provider与配置入口，独立过音频保活/直Clip/取消测试；8.6c在两类正式consumer均已转交后退出旧YL实现、去主体XFAB引用、收口测试/导出哨兵。a/b过渡期间测试资源域隔离，不让同一asset被两种authority同时接管；c不得延期为长期兼容层。每个子批次均可单独拒收和授权，禁止全批一次性巨改。

- [ ] 文本string交付前释放自己lease、共享读表不伤另一模块；fallback次序/error cause/Model初始化和startup rollback保持。
- [ ] Core `yield`等待后只领取一次owned结果，Dispose未领取/领取后两条路无泄漏或双释放；同步InProgress不错误fallback到基础模块，异步join同producer。
- [ ] Core命名View/Panel同步异步、正常Close复用、damaged/Attach/父销毁/场景迁移/取消迟到/关闭重启选定回归；direct prefab及默认Canvas无需Resource仍可用。
- [ ] Audio Resources/AB命名lease、idle cache、crossfade尾轨、cancel、Shutdown迟到、依赖最终释放；直Clip不被新Resource或Audio卸载。
- [ ] 新配置/序列化字段定向资产审查和一次性迁移入口；原XF配置与MainModule地址完全不读写。Core closure拒绝Systems/XFAB，Editor模拟通过正式目录工作。
- [ ] 迁移`ResourcePoolTests`、Audio内部Res状态断言、CoreXfab fixture/池反射与Export哨兵到等价行为；不删除关键断言制造通过。不再容忍旧YLManager永久并存。

### 8.7 版本目录、纯更新计划与激活/回滚事务

**主要范围**：`R/Updates`的catalog mount、UpdatePlan、ArtifactStore、active index/journal/恢复/清理；`E/Publishing`受控release布局；仅用本地或内部受控、已验证snapshot，不提前开放生产远端更新。

**依赖/输出**：依赖8.6；v1/v2检查只读，prepared版本与ActivationBarrier、last-known-good、跨重启恢复、owned cache清理。

**风险**：旧native包仍占用文件、snapshot去重key遗漏、崩溃半commit、版本同名异内容、多项目部分激活。

- [ ] 只Check/取消/准备失败不改active文件/hash或版本；v1 lease/scene/instance活着时v2激活Busy且旧Acquire仍可用，neutral边界flush/真实卸载后才切snapshot。
- [ ] 相同target重复Prepare不重复下载，Check不使prepared失效，跨重启恢复Complete；already-active激活无副作用、from已变化明确StaleBase。
- [ ] journal每个写入/切换阶段注入故障，重新初始化只选一个完整兼容快照；回滚走同一安全边界，native不混代。
- [ ] 清理排除active/prepared/last-good/在途及原XF/未知目录；多project closure有确切摘要与平台，任何Busy都不部分激活。
- [ ] 未通过8.8的production远端入口明确拒绝，不能将本地摘要校验、内部导出或恢复成功标为生产真实性/防重放验收通过。

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

- 实施分支 `fusion/8-resource`，实际起始及交付 HEAD 均为 `b4c2c6cc74a407be6ffefab0c7019dea5edc8edb`。只实施本批、必要验证及一次独立技术 Review；未暂存、Commit、Push、合并或创建里程碑。
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
- **8.2c 技术 COMPLETE；SchemaVersion=1 正式冻结。等待用户技术验收与Git收口授权。到此停止，不自动实施8.3，不合并integration/xf-yl，不创建阶段8里程碑。**
