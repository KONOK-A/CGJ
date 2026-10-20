# B组配置数据交接

## 去除历史兼容（2026-10-20 11:21最新授权）

用户已确认YL尚未发布、没有下游项目，不需要兼容或迁移XF旧代码。本节覆盖前文中旧的“保留旧重载/迁移入口/旧数据字段”交付要求；旧验收结果与证据仍保留为历史，不据此继续维护已删除能力。本轮只修改B分支，不替主工程合并或修改C代码，不Push。

### 纠正记录（2026-10-20 12:24）

- 提交`4f1c683`把“没有当前调用方”误当成“旧兼容”，同时删除了DataAssets hash查询/移除、Localization静态查询/移除、Excel`boolean/int64`别名及两个既有Save删除重载。用户指出这些是普通功能或既有API，不是旧格式适配；本提交已全部恢复，相关测试也恢复原断言。
- 当前只保留两类删除：**Save旧格式迁移/导入链**，以及用户明确不要的两处`excel_path`。`4f1c683`历史保留，不回写、不amend。

### 删除范围与当前合同

- Save：删除整个`SaveMigration`及meta、`SaveManager.ImportArchiveModel`、仅供迁移使用的`ImportModelAsync`；删除v1复制/XF明文`.sav`导入测试及`MigrationStress`/Player迁移探针。不再提供旧档迁移服务，也不保留Obsolete、空实现或兼容别名。既有对象删除重载、门面、模型/槽位/当前项/异步/缓存/取消/并发/备份恢复均保留。
- 保留唯一`SaveStore`/`SaveManager`，当前索引仍为版本2，不为删除迁移而重编号；不支持版本明确抛`SaveVersionException`且保留原件。`ModelFileName`是当前模型文件约定，不包含XF目录扫描或旧文件回退解析。
- DataAssets：`StringToHash/GetConfig(int)/RemoveConfig(int|string)`及其hash索引、碰撞拒绝、多owner、Ordinal名称身份全部保留，本次没有删除。只删除根上闲置的`excel_path`及Inspector显示；Excel路径由Editor显式传入。
- Localization：静态`GetConfig(string)/RemoveConfig(root|string)`与句柄生命周期并存，本次没有删除。只删除闲置`excel_path`。
- Excel：`boolean`→`Bool`、`int64`→`Long`的兼容同义词解析保留（原XF`ParseFieldType`本就支持）。`ExcelAuthoring/ExcelImportPlan`公开签名、三行表头/第4行数据、E0自定义转换、GUID/localFileID及SO/List/JSON工作流均不变。
- `excel_path`的性质：原XF用它记住根资产上次使用的绝对工作簿路径，供导入/导出预填。YL作者流程改为每次调用显式传入路径，字段没有任何读写者，且绝对路径不可跨机器移植，因此删除是**作者工作流设计选择**，不是旧数据兼容清理。
- 已保留用户要求的“在文件夹中打开Excel”能力，但状态放在Editor侧：`DataAssetsWorkbookMemory`按资产GUID把最近成功导出的工作簿写入`EditorPrefs`，不进入`.asset`。DataAssets Inspector提供按钮，文件存在时调用`EditorUtility.RevealInFinder`，缺失时禁用并显示提示；资产改名/移动沿用同一GUID。路径失效不会静默清空，需重新导出或后续显式清除。
- Localization当前没有Excel导入/导出作者流程，因此没有对应“打开导出文件”按钮；以后接入时可按同一GUID键机制实现，而不是恢复`MultiLanguageConfigs.excel_path`。
- 复用的成熟功能、公开数据布局（除上述字段）、TypeCache类型绑定、Other/TMP反射、中文适用变体回退与Resource Lease不是兼容层，继续保留。原XF许可证/版权与来源声明必须保留。

### 本轮验证与接收影响

- 新合同先红：Save旧导入API存在导致33通过/1失败；`excel_path`存在导致真实xlsx19通过/3失败。离线原件在`Temp/Extensions.Data.{Save,Excel}`的`no-compat-red`目录。
- 纠正后离线：Save核心/门面/既有纯Json共37/37；SO Pure 10/10（hash碰撞用例恢复并通过）；真实xlsx 20/20（含`excel_path`不存在断言，类型别名用例已恢复为支持）。Runtime457源、Editor102源、可选Excel7源辅助编译成功，既有39/4个产品warning。
- 纠正后原生EditMode：`G:/Unity/BD26/compat-correction-edit-01`，PID4768、OS Exit0，111/111、0失败/跳过；含禁Domain Reload双轮（同域token`ac870e522d984bc4b78e1e93e461bb35`，每轮登记0、Lease0、Save关闭）。较删除前112精确变化：移除5项（4个迁移专用、1个旧`excel_path`字段用例），新增4项（当前模型字符串/错误ID、迁移API不存在、SO根不存路径、显式预览不持久化路径），净-1。
- 纠正后原生PlayMode：`G:/Unity/BD26/compat-correction-play-01`，PID38592、OS Exit0，15/15、0失败/跳过。本轮未重建/运行Player；旧Development与Release证据不自动作为本次删除后的新运行证明。
- 工作簿定位功能原生复验：`G:/Unity/BD26/workbook-reveal-edit-01`，PID39120、OS Exit0，EditMode 114/114、0失败/跳过（新增2项记忆逻辑、1项导出记忆+改名保GUID）。该轮只改Editor作者层和EditMode测试，Runtime源未变，PlayMode 15/15既有证据仍对应同一Runtime快照；未打开Explorer窗口做GUI人工验证，`RevealInFinder`本身只由源码路径调用。
- 最终Review复验（SaveStore模型校验语义恢复后）：`review-final-edit-01` PID36336、OS Exit0，114/114；`review-final-play-01` PID15520、OS Exit0，15/15；双轮同域每轮登记0、Lease0、Save关闭。`review-final-closeout`归档并校验61个配置字节及测试产物。
- Review结论：迁移链删除后`CreateModelAsync`曾顺带把负数ID的异常类型改为`ArgumentOutOfRangeException`，属于与去兼容无关的契约变化，已恢复原`ArgumentException`（负数或ID不一致）；Save回归37/37。其余删除仅限SaveMigration/XF导入与两处`excel_path`，功能API、类型别名和既有删除重载均保留。
- 未启动主工程Editor/MCP或修改主Git；主会话必须接纠正后最终tip。若主已接收`4f1c683`，再接本纠正提交即可，不需重做整轮融合；不能把`4f1c683`当作最终去兼容成果。
- 原生前冻结的61个B配置/Packages/IDE文件字节与SHA在`G:/Unity/BD26/no-compat-protected`，纠正轮结束再次逐字节一致；IDE改写已恢复，测试meta/空目录原件在`G:/Unity/BD26/compat-correction-closeout`。无其他未跟踪产物。
- 纠正提交及“在文件夹中打开Excel”提交均为中文本地提交，原历史不重写（`4f1c683`保留）；最终tip通过`git log -1 --oneline -- .kilo/handovers/xf-ext-data.md`取得。共享Core/Resource/主体asmdef、Packages/ProjectSettings、原XF及主工程均未改。

## 此前主工作区接收准备（历史）

本节是B合并输入与后续验收边界的最新交接。用户本次要求仅在B完成合并前准备并提供主会话提示词；本会话不merge、不Push、不操作主Editor，不修改主工作区或其他worktree。

- 主接收目录`G:/Unity/Projects/CGJ2024`，目标分支`integration/xf-yl`；B来源分支`fusion/xf-ext-data`，共同起点`c88500aef6651f5b13ac933dd1f209ecb314e92d`。本交接所属的新提交应随整个B历史一起接收，不能只取末尾文档或单独的原生修复补丁。
- 已完成的产品/原生修复输入固定为`6a9e2508a42333e55c82e6f29eea136643af4467`。在其上本次只补实质交接，不修改产品、测试、Importer或工具；112/112、15/15等既有证据按原源快照沿用，不伪装为合并后验证。
- 完整六条实现历史依次为`517f13fa3928b869ac34854cf0474173c6137fe7`、`53edd50a5c869cfb89f0879157cb85312b68ee26`、`09bec5a675d4f920f5fc0e4fa65aaafb3fd8a300`、`9626d9a0bd7d32ed717d2953762a0b4c31d73809`、`73025d1fb83abc3cdc83dd2587332093a3bbaca1`、`6a9e2508a42333e55c82e6f29eea136643af4467`；没有squash/rebase/amend。
- 2026-10-20用户授权后，四项运行副作用已精确恢复为6a9e250对应HEAD的原始字节，包括删除稀疏字典两处Standalone键；31个自有未跟踪文件及13个目录连同meta原件已移出工作区。35个现场文件逐一SHA256归档/核验，回执`G:/Unity/BD26/closeout-20261020-0359/manifest.json`状态`ClosedVerifiedClean`。原件在该目录`moved-originals/`，现场副本在`before/`，原HEAD字节在`head/`。此前“保留脏文件”的文字仅是原生验收结束时历史状态。
- 本次合并前只读检查：159条新增Assets记录的资产/meta/祖先目录配对完整，无孤立meta；必要模块许可证均受管；B完整提交不含Packages、ProjectSettings、原游戏/图集、共享Core/Resource、主体asmdef、正式总Plan或工具配置变化。实际DLL SHA256仍与Dependencies.json一致，LFS指针不是最终二进制。
- 主工作区只读观察曾为B0，存在主Plan未提交改动、用户`AlternateContent.meta`及`Tools/Behavior/`。这是时点事实，不是可覆盖名单；主会话执行前必须重新冻结其真实HEAD/index/dirty/untracked/场景状态。当前另有A接收协调上下文，必须等现有同工程Git/Unity任务自然完成，再由主会话处理B；不并发、不替A/C收尾、不以本节猜测其最终状态。

### 接收时不能直接照搬的工具约束

- `Tools/Extensions/Data/Unity/Run-DataUnity.ps1`、`Run-DataPlayer.ps1`、`Tests/Tools/Extensions/Data/Editor/DataUnityAcceptance.cs`、`Tests/Tools/Extensions/Data/Runtime/DataPlayerProbe.cs`及`DataDomainReloadSessionTests.cs`明确锁定B项目/`G:/Unity/BD26`。主会话应做最小的主工程入口/自有输出owner适配，继续核对规范projectPath、PID、项目锁、输出marker和真实测试发现，不能删掉身份保护或伪造B marker复用B证据。
- 主验收另选自有短根（例如`G:/Unity/MB26`，需先核验不存在/归属），不覆盖BD26、清理归档、A/C或Y9；导入后生成自己的run/PID/XML/日志、profile及退出结果。
- B的两个原生Player使用`BuildOptions.Development`。它们是真实构建/运行证据，但不是正式Plan G3的非Development最终结果。主接收须另设显式非Development/Release入口，实际构建和运行Mono及IL2CPP Low；不得把旧exe复制、改标签或只构建算运行通过。
- PlayerSettings原API恢复逻辑值会在无Standalone键的稀疏字典插入`Standalone: 0`。主会话运行前要保存其当时实际原字节/键存在性及SHA，退出后验证全部受测设置；不能把Get/Set返回相等当作逐字节还原。只处理可证的自身副作用，不恢复成B的HEAD或覆盖主会话原有用户改动。用户原场景不保存/覆盖，数据测试不访问原游戏存档。
- Batch使用实际2022.3.62f3c1，同一projectPath一次仅一个Editor。主项目已有Editor时，须先确认其PID/绑定/未保存状态；可复用被核实的现有主Editor，或在安全退出后串行Batch，不能并开。保留插件官方process-only opt-out，不改权限/token/磁盘MCP配置，不通过忽略错误日志造绿。

### 主接收必要工作

- 在用户向主会话明确发出B合并授权后，以本文件所属完整B tip作固定输入；先检查祖先、重叠及冲突，已接收则避免重复。保留全部B历史，保留A已接收结果与用户现场；不自动接C/其他分支，不Push，不删除分支/worktree。
- 主协调统一补来源/版权（含2024 MIT）、RootAssetTypes/AssetMetadata/正式Plan与必要架构/使用说明。先读主文件现有改动，增量维护，不整文件采用B版本覆盖，也不扩大共享Core/Resource公共接口。
- 主原生门应重跑B相关112项EditMode（含双轮Play）与15项PlayMode并核实际发现/执行/失败/跳过，补Foundation/Core/程序集边界以及合并影响到的Resource/UI或已接A/C最小回归。引用失效拒交、Excel部分失败回滚、即时子资产Undo、同域两轮旧owner归零、Save/JsonUtility必须保留。数量变化须解释，不能少发现也报全绿。
- 重验MiniExcel正式Editor-only、LFS实体、GUID/旧Save身份、Player实际AssemblyRef及输出排除；核心默认导出不带可选backend，选装与未装分开。主工程绿不等于无XF独立接收工程G4或原游戏代表链G5全部完成，应明确实际执行范围，不能覆盖阶段9历史制品。
- Save v2/显式复制迁移与普通文件错误契约保留，不自动转换旧档；真实旧样本兼容、原AccessDenied和超长路径边界仍不能因后续绿例关闭。

## T3-B真实Unity验收（既有证据）

2026-10-19 20:14用户追加授权已覆盖本文件源码阶段的“不启动Editor/BatchMode”限制。仅在B独立worktree使用安装的Unity`2022.3.62f3c1`，每次显式`-projectPath`；没有调用Unity MCP、没有操作主Editor/A/C/Y9，没有复制主工程Library。以下是真实引擎证据；后面的旧“待Unity”记录保留为源码阶段历史，不覆盖本节。

### 真实结果与证据

所有外部证据统一在自有短目录`G:/Unity/BD26/`，`owner.json`固定B工程身份，每个run保留独立`run.json`（参数/PID/退出码）和日志。B独立Library由Unity正式生成。最后审计PID`43284`于`2026-10-19T21:12:07Z`退出0，随后按projectPath核验B无Editor残留；已通过Agent Manager明确通知A窗口释放，本轮不再启动B Unity/Player。

| 门禁 | 真实结果 | 证据目录/文件 |
| --- | --- | --- |
| 首次导入/实际编译 | `import-01`退出0，全部B产品/可选Editor/测试真实编入程序集，无相关CS编译失败 | `import-01/editor.log` |
| EditMode红例 | 实际111项，107通过、4失败、0跳过 | `edit-01/results.xml` |
| EditMode修复复验 | 实际111/111、0失败/跳过 | `edit-02/results.xml` |
| 最终EditMode | 实际112/112、0失败/跳过；新增1项是真实双轮Play测试 | `edit-final-01/results.xml`，PID35072 |
| PlayMode | 实际15/15、0失败/跳过：Localization6、DataAssets9 | `play-03/results.xml`，PID16408 |
| 禁Domain Reload连续两轮 | `domain-02`1个UnityTest中真实进入/退出两轮Play；同PID38400、同token`19099d4225b34da982f22b2b66c62bac`，每轮登记0、Lease0、Save关闭、Resource Invalidated | `domain-02/domain-round-{1,2}.json`；最终EditMode又重跑同一门，不重复加到112上 |
| Windows64 Mono（Development） | 构建Succeeded/0error；实际NVIDIA RTX4070TiSUPER/D3D运行，PID41136退出0，全部数据探针true；非正式Release门 | `mono-build-01/build.json`、`mono-player-02/{run.json,player-result.json,player.log}` |
| Windows64 IL2CPP Low（Development） | 实际il2cpp.exe与VS2022链接GameAssembly，构建Succeeded/0error；真实GPU运行PID26860退出0，全部数据探针true；非正式Release门 | `il2cpp-build-02/{build.json,editor.log}`、`il2cpp-player-01/{run.json,player-result.json,player.log}` |
| 最终原生隔离审计 | 99个B相关资产meta与AssetDatabase一致、B跨路径GUID冲突0、旧SaveManager GUID不变 | `audit-final-01/audit.json` |
| v1发布定向核查 | 短路径10次+原失败目录等长路径10次成功，源字节均保留；最长临时路径185/255字符 | `migration-stress-03/migration-stress.json` |

最终EditMode分项：DataAsset作者15/身份10，Localization作者1/逻辑12/原生3，Excel资产11/转换19/绑定4，Save文件安全33/真实门面2，原SerializedDictionarySaveTests的磁盘+JsonUtility联合1，双轮Play1，总计112。测试筛选及所有实际叶节点见XML，不以未发现/未导入算绿。

### 本轮修复与红绿关系

- SO真实缺陷：`AddObjectToAsset`后尚未首次保存时，Unity报告同一root路径且parent/member正确，但`IsSubAsset=false`；`audit-02`明确记录保存后变true。所有权改以同文件、真实持久成员且非主对象判断；同root文件的删除走子资产分支。原`SubAssetDeletionOnlyDestroysChildAndCanUndo`红→绿，并增加即时IsOwned断言，没有删除原Undo断言。
- Excel真实缺陷：部分应用失败时只恢复文件/ImportAsset，Unity保留dirty实例的`partially updated`字段，且CopySerialized带入`(Clone)`名称。现在保存原EditorJson内存快照、保留旧名称，恢复原字段并清除本事务dirty后同步导入原字节；原受控部分失败测试红→绿，原字节/GUID/旧内容/仅清理本次新资产断言完整保留。属于真正AssetDatabase执行，不再是只编译准备。
- Save测试环境适配：Unity Mono实际为`System.IO.DefaultWatcher`轮询，会漏掉快速创建并移动的临时文件。取消用例改为有ready/stop/Join边界的专用线程直接观察真实tmp文件，再发取消；仍必须观察到真实临时文件，仍断言仅旧状态或完整提交、缓存/备份正确、tmp清理。原失败样本及XML保留，未弱化断言或加入产品测试钩子。
- 非产品环境污染：首个EditMode有MCP插件自动连接共享服务的Authorization错误，后续用上游正式runtime-only参数`-UNITY_MCP_KEEP_CONNECTED=false`、`-UNITY_MCP_START_SERVER=false`及进程`CI=true`隔离连接/下载；未改权限/token/UserSettings磁盘配置/PackageCache，也未压制日志。另有该插件日志sequence写入的warning及既有Cinemachine asmref/构建shader warnings，分别保留，不归因YL Save。
- 新双轮验收工具曾遇实际NUnit不含NonParallelizable、Complete需要error参数，以及重复关闭已Invalidated Resource/空SceneManagerSetup恢复错误；均修正验收工具契约，原失败日志在`play-01/play-02/domain-01`保留。双轮有效断言没有跳过，最终源码在真实Unity编译并运行通过。

### 隔离与Player范围

- Test profile只在B进程临时设置自有company/product，持久目录为`LocalLow/YLFrameworkTests/YL-BData-*`；`profile.json/profile-restored.json`证明还原原值，未读写原游戏存档。双轮使用自有空场景，不保存用户原场景，EditorSettings恢复。
- MiniExcel实体SHA256仍为`0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e`；Unity生成PluginImporter为Editor=true、Any=false、Windows64=false。普通YL Runtime/Editor无MiniExcel引用；Player编译图无YL Editor/Excel，两个实际Player的文件与已加载程序集无MiniExcel/UnityEditor。Runtime的Editor profile存在合法条件编译UnityEditor引用，不冒充Player泄漏。
- SaveManager原GUID保持`ff5957d84032533449f21601f372570a`。所有新meta由B Unity生成，没有手写GUID。
- 两个Player使用真实Resources来源/根Lease和真实Native SO资产，不是模拟Resource Source；验证模型同步异步磁盘往返、损坏保源/备份恢复、取消保旧、显式v1新根复制、具体SO反射、Other公开text反射和非中文English回退，退出Lease0。探针自身有定向link.xml，不能据此推断所有应用DTO/TMP反射均已AOT验收。
- Mono初次以NullGfx运行虽数据通过但有URP不支持shader日志，另以实际GPU重跑干净的数据关口并正常退出；不把NullGfx当渲染验收。两个构建各有1264条现有全项目资源/包警告，结果0error，不宣称无警告或原游戏画面验收。
- 本工程实际Player仍含原XF及其他包；这不是“无XF独立安装”或阶段9新交付包。干净接收工程和主集成组合回归仍待后续，Y9及既有交付未动。

### 环境边界与工作区保护

- 原.NET10迁移发布AccessDenied仍未稳定复现，不能称已根除；本轮20次成功仅覆盖列出的185/255字符路径。`migration-stress-02`故意更长的前缀在Mono文件创建阶段触发DirectoryNotFound/MAX_PATH边界，失败源/staging及原报告保留，未发布目标；它不是原Directory.Move AccessDenied的根因证明，不伪装20/20全路径支持。
- 普通session生命周期的monitor曾使`il2cpp-build-01/migration-stress-01`包装进程与Editor一起中断，未有结束回执/结果；不计通过，也不判为产品构建失败。后改持久跟踪且阻塞等待已核实B PID，最终均自然结束。
- Native构建自动填入`ProjectSettings.asset`的Standalone backend/stripping显式默认0、IDE改写`.vscode/settings.json`，以及两份`Assets/Atlas(AutoGenerate)/MainModule/*_Atlas.spriteatlas`写入packed缓存。本组已核对差异归属，未覆盖这些非B产品文件，也不会混入修复提交；保留并在最终状态报告。Packages/lock及共享Core/Resource/主体asmdef/正式Plan未改。
- 失败Excel fixture、生成的Player场景/Resources、自有YLFrameworkData空父目录保留为未跟踪证据/可重建产物，不混进产品提交；修复源码、测试、工具和其必要正式meta精确暂存。
- 原样保留Unity正式生成meta的空YAML字段格式，因此原始`git diff --cached --check`会报告这些字段的尾空格；不把该格式警告隐藏为全量check无警告，也不为通过检查手写/重造GUID。源码/工具/交接单独检查无空白错误。
- 本轮修复及原生验收的中文本地提交与本节同提交，可用`git log -1 --oneline fusion/xf-ext-data -- .kilo/handovers/xf-ext-data.md`查实际SHA。此前五个候选历史保留；不Push、不Merge、不Rebase/Reset/Clean/Stash。

以下为此前源码融合阶段的历史记录；与本节状态冲突时，以本次授权和本节真实证据为准。

## 授权与基线

- 唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`；2026-10-19本次用户追加授权覆盖其中旧T3、D2/D4/D6及Save只读/分批停止状态。
- 实际工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-data-14669bf18d117ed8`；分支`fusion/xf-ext-data`。
- 开工HEAD/B0：`c88500aef6651f5b13ac933dd1f209ecb314e92d`；祖先检查退出0，工作区和index干净。未重置、切分支或同步其他分支。
- 不操作Unity MCP/Editor/batchmode；不Push/合并，不修改共享Plan、Core/Resource、主体asmdef、Packages/锁文件或原XF/游戏。新meta待集成owner通过Unity生成。

## 当前进度

- Localization：产品、原布局作者工作流和测试已实现；离线纯逻辑12/12及组合辅助编译通过，尚非Unity验收。补确定性回退、同数量内容变更、重复ID、根/事件/图片资源责任、晚到隔离和直接刷新。
- DataAssets/SO：保留根/子资产与类型绑定、CRUD/排序的候选已落齐，纯逻辑10/10及真实参考辅助编译通过；无新Catalog，资产/GUI/PlayMode仍未运行。
- Excel：真实List/SO/JSON作者API、MiniExcel后端和已选SO根菜单已落盘；审查修复后真实xlsx19/19绿，完整可选Editor闭包编译通过；15项Unity资产/GUI用例仅准备/辅助编译，不是GUI验收。接点见下节。
- Save/Archive：单一`SaveStore`文件引擎与原`SaveManager`Unity门面已实现并提交，父独立真实文件33/33、门面及既有纯Json回归39/39通过（包含前述33项）；组Review未发现额外可确认的Critical/Important，仍待Unity/Player验收；兼容策略见下节。

## 组Review收口状态

- 唯一一次新上下文整体Review已完成，范围为B0至全部四个候选以及Loc启动重入关闭保护、诊断context和证据冻结脚本。结果无Critical、3项Important；SO/Save未发现其他可确认的重要问题。没有再做逐模块独立Review或二次Review。
- Important 1已代码修复并真实红绿：有损Export不再参与差异判定，Before/After展示实际字段状态；例如Scaled的21→20即使两边Export均为2也明确列Update。新xlsx回归`LossyConversionAlreadyPresentAtPreviewStillReportsActualFieldUpdate`从18通过/1失败到19/19通过，原红例在`Temp/Extensions.Data.Excel/red-review-lossy-diff`，绿及最终证据在`green-review-lossy-diff`、`group-final-review-fixes`。
- Important 2已代码修复，原生回归未运行：提交前比对暂存原字段状态，并按GUID/localFileID/类型反解核验持久引用身份，删除/替换的新引用不得悄悄变Missing/null后Applied。List/SO各一项`DeletingNewReferenceAfterPreviewRejectsCommitAndPreservesOriginalTarget`已准备/编译；此处只核验身份/存活，不承诺冻结依赖资产内容。
- Important 3已代码修复，原生回归未运行：Localization关闭期拒绝AddConfig/LoadConfigAsync及内部Register，`try/finally`重置静态/事件，所有旧owner逐一尝试释放，原生retention错误聚合后报告，不吞错。新增两项PlayMode测试覆盖释放中重入登记/加载、释放异常仍处理其他owner与收尾事件；与“启动时重入关闭”分别覆盖。
- 额外补了真正`ApplyAssets`的受控部分失败回滚测试准备：先更新旧对象、创建新资产，再以缺失目标键故障验证原字节/引用恢复和仅清理本次新资产。没有产品测试钩子；这是明确的受控故障注入，不是已执行Unity真实IO故障证明。失败fixture保留。该测试仍须主Unity运行。
- 裁定：保留普通List整体替换语义，不擅自复制忽略字段；差异状态包含会被替换的实际字段，不能用ExcelIgnore或有损显示掩盖它。SO仍从旧对象暂存副本并按身份更新。若消费者曾把Before/After当作xlsx显示文本，需改为将其视为实际字段状态文本。
- 三项均已进入最终源修复；只有Important 1完成运行时红绿，Important 2/3及资产部分回滚的Unity运行证明仍是主线接收门，不能在此标为原生验收通过。必须连同最终修复提交接收，而非只取四个早期候选。

## Excel真实接点

- 可选目录`Assets/YLFramework/Integrations/Excel/Editor/`；assembly及namespace均为`YLFramework.Excel.Editor`。普通Runtime/Editor不引用它，C组可在自己的可选Editor程序集接线，勿修改主体asmdef。
- 已落盘入口`ExcelAuthoring.ListToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1")`。
- 已落盘预览`ExcelAuthoring.PreviewList<T>(string path, List<T> values, string sheetName = null) where T : class, new()`，以及`PreviewJson(string path, string jsonPath, string excludePrefix = "", string sheetName = null)`，均返回`ExcelImportPlan`。
- SO入口已落盘并用真实Unity参考辅助编译：`ScriptableObjectToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1", Type dataType = null) where T : ScriptableObject`；`PreviewScriptableObjects<T>(string path, List<T> values, ScriptableObject owner, string assetFolder = null, string sheetName = null, Action<T> initialize = null, Type dataType = null)`返回同一`ExcelImportPlan`。`initialize`仅用于暂存副本的领域字段绑定，不得改外来资产；SO事务/GUI尚未在Unity执行。
- `assetFolder = null`用于同root真正subasset；独立主资产模式须传已存在的独占目录。B组SO目录由`YLFramework.DataAssets.Editor.ScriptableObjectConfigsController.GetDataPath(root)`取得，条目`parent = root`；C组同样传自己的精确目录。旧条目必须属于该owner的真实序列化List，dirty资产拒绝导入，同ID/类型就地更新。
- `ExcelImportPlan`提供`Errors`、`Changes`（Kind/Key/Before/After）、`CanCommit`、`Commit(bool confirmed)`及`Dispose()`；结果`Pending/Applied/Cancelled/Failed`。必须先检查错误和差异，再显式确认；取消不写入。
- SO预览直接支持`List<BaseSO>`的派生条目，依据`DATA_TYPE_FULL_CLASS_NAME`解析实际类，不复制一个脱离owner的`List<Derived>`。`dataType`约束可用派生范围，提供空根模板字段及无类型列时的绑定类型；`type_script_guid`失败明确报错，不猜类名。
- 已选SO根入口`DataAssetsExcelCommands.PreviewImport(ScriptableObjectConfigs root, string path)`和`Export(root, path)`真实可用；菜单`Assets/YLFramework/Excel/Preview Import Selected Data Root`与`Export Selected Data Root`。自身可选asmdef引用`YLFramework.Editor`，普通Editor不反向引用。暂存条目设置`parent=root`，成功清缓存；取消不创建数据目录，空根也不依赖既有`excel_path`。
- 通用UI入口`ExcelAuthoringCommands.ImportList/ExportList/ImportScriptableObjects/ExportScriptableObjects/ShowPreview`及`YLFramework/Excel/Workbook To JSON`。导入预览包含新增/更新/删除和坐标错误，保存主资产前必须干净，过期工作簿或目标变化拒绝提交；导出整工作簿覆盖须确认，不承诺保留原多sheet格式。
- 后端MiniExcel `1.31.2`，Apache-2.0，netstandard2.0/无NuGet包依赖；来源上游tag`1.31.1-1.31.2` commit`f1498a2d162debca9a1e9153cf2249131a75eeb0`。固定nupkg SHA256：`f11a54c1bd5b3fb3332dc5d4e40ade59a3539cb8de6d087a245575457d66379a`。本组自有恢复脚本`Tools/Extensions/Data/Excel/Restore-MiniExcel.ps1`。
- 父独立执行`Tools/Extensions/Data/Excel/Run-ExcelOffline.ps1 -Run parent-pre-review`：真实xlsx18/18、0失败/跳过；全Runtime458源39warning、全Editor102源4warning、完整可选Excel7源及当时测试5源0warning均辅助编译退出0；没有`YL_EXCEL_OFFLINE`条件排除。子任务最终`green-delivery`另完整编译新增同名Fixture后的测试7源并再次18/18，12项Unity作者用例仅编译；候选已提交，正在组Review，不是Unity/AssetDatabase/GUI结果。
- DLL实测SHA256 `0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e`与固定清单一致。现有`Editor/Distribution/ExportUnityPackage.ExportRoots`不含`Integrations`，默认导出不会带入此后端；不是只靠`Dependencies.json`标记宣称隔离。可选分发应另带完整adapter与`Licenses`，Unity导入后还须核对PluginImporter为Editor-only并生成meta。

## Save设计与兼容策略（实施前确定）

- 单一`SaveStore`负责BCL文件/索引/槽位/模型对象/设置/缓存/备份及同步异步；既有`SaveManager`仅负责主线程捕获Unity路径并暴露兼容门面，非第二存档权威。
- 保留对象/设置裸JSON用途；索引升级v2并纳入当前槽。检测到v1拒绝原地写入，提供显式复制迁移到不同新目录，拒绝源目标相同和已有目标冲突。没有真实旧档样本，只能验证合成合同，不能宣称历史档兼容。
- Archive仅提供显式选择的明文JSON导入到新槽，不扫描原目录、不自动转换、不接纳DES/固定IV格式；旧`.sav`原件不修改。实际读到的XFAB旧EncryptTools用`string.Empty.PadRight(8)`产生8个空格字节IV，不是全零IV；该薄弱DES能力没有复制进新格式。
- 取消在单文件原子提交点前有效，提交后报告成功；同根排他owner、同槽串行、跨槽可并发，关闭取消排队并排空。磁盘失败不推进对象缓存。
- Save独立收口实测：`Run-SaveSafety.ps1`为33/33；`-WithFacade`为39/39（33核心+2真实门面+4既有纯Json）；`-ReferenceOnly`真实Unity2022.3/.NETStandard2.1参考辅助编译退出0、2个CS1701（Json.NET/NUnit 2.0→2.1引用匹配）。不依赖虚拟Unity，不执行Unity原生API来冒充通过。
- 已落盘的新读取入口`ReadObject<T>/ReadObjectAsync<T>/ReadGlobalSetting<T>`返回`SaveReadResult<T>`，携带主件/备份/缺失来源和缓存标志，缓存命中不隐藏备份来源；`IndexNeedsRecovery`显式报告索引恢复要求。`RestoreObjectBackup/RestoreSettingBackup/RestoreIndexBackup`为显式修盘入口，保留损坏原件而非读时自动改盘。
- 现有`SerializedDictionarySaveTests`已按新门面owner调整必要测试隔离，保留原磁盘/Unity断言；实际数据保护验证只用自有临时目录。
- 已实际研究ArchiveSystem1.0.7 `ArchiveManager/IArchiveModel`与现有YL Save；复用成熟槽位/模型/当前项组织，统一至`SaveStore`而非复制旧Manager/DES。来源revision `8046fe56f8`，MIT2025由现有根许可保留，概念吸收及来源在此登记。
- 兼容影响：损坏/IO/权限/版本错误不再静默变默认值；只在确实缺失时建立默认设置；禁止`TypeNameHandling`从JSON实例化CLR类型。删除改为`.deleted-*`软删除，不是安全擦除且不自动清理。保存异步先在调用线程序列化快照；反序列化/Converter异步在线程池，不得调用UnityAPI或重入同owner。缓存继续返回原实例，调用方负责该实例的并发修改。
- 显式Archive导入现严格拒绝未映射字段，必要字段由目标DTO的`JsonProperty(Required=...)`声明；防止错误DTO静默丢字段后仍报成功。父新增回归真实红32/33→绿33/33，失败源及目标样本保留在`Temp/Extensions.Data.Save/cases/4a68331295a145d49b393b2e57d3196a`，不删原件制造通过。
- `SaveMigration.ImportVersion1(sourceRoot, destinationRoot)`要求不重叠且目标不存在，保留原索引/备份为`.legacy-v1`并保留文件时间；`ImportArchiveModel<T>(sourceFile, SaveStore destination, destinationId, token)`仅进入新槽。模型`Create/Save/Load/LoadAllModelsAsync`与对象槽、缓存、`CurrentSaveID`共用唯一引擎。
- 文件提交点是已flush临时文件的`File.Replace/File.Move`；不支持原子替换的文件系统失败关闭，不删除原件降级。创建/删除跨索引与目录不是全根多文件掉电事务：失败staging/tomb和原件保留供显式恢复。一次迁移发布曾`AccessDenied`，原因未定位，后续单例及完整回归绿；没有吞错/添加重试，也不能称该环境问题已修复。

## 接口与边界决策

- Localization继续显式登记配置，避免Runtime/Editor自动全资产扫描；根Lease由登记句柄持有，条目借用不得超出登记生命周期。图片消费现有`ResourceImage`。
- 中文变体只在中文请求时提前尝试；其他语言按精确、English、显式列表顺序回退。查询语言和按ID查询使用同一候选顺序。
- Excel后端仅在`Integrations/Excel/Editor/`，不改主工程包依赖、不向Runtime引入解析器。C组仅在真实API落地后接线。
- Save仅在自有临时目录/复制样本验证；不自动读写或转换原Archive `.sav`，没有真实旧档样本不宣称兼容。
- Localization保留UGUI/TextMesh、Other反射（可绑定TMP公开`text`成员）、资源/网络图片及参数/附加文本；不安装TMP，也不携带联网翻译凭据、Youdao认证或Steam外部作者窗口。后两类非本次离线配置工作流的必要能力，明确未迁入，不伪装为可用；Other反射成员的IL2CPP保留由实际应用配置并待Player验证。
- Localization语言资产创建仍采用根引用的独立语言文件；显式移除语言只解除引用并可Undo，不删除可能共享的资产文件。作者窗口保留语言排序、搜索、全语言新增/复制/删除条目、类型/内容/备注编辑和保存，图片预览复用既有YL预览工具。

## Localization真实接点

- namespace `YLFramework.Localization`；`LocalizationManager.AddConfig(MultiLanguageConfigs)`或`LoadConfigAsync(ResourceSystem, ResourceAddress)`均返回`LocalizationConfigHandle`。owner保留句柄并在关闭时`Dispose()`；异步句柄提供`IsLoading/Config/Error/IsDisposed`，不会自动挂接未知根或扫描旧XF资产。`Shutdown()`清本域登记/在途等待和事件，不关闭Resource。
- `RequestedLanguage/Language/Languages`区分请求与当前可用语言，偏好键为`YLFramework.Localization.Language`；`Changed`为YL `IEventSource`，订阅方持有并注销返回token。`GetLanguageInfo/GetString/Refresh`分别查询、格式化、直接刷新，无GameObject强制激活。
- UI挂`LocalizeString`或`LocalizeImage`并设置ID，另显式登记根。后者复用同对象的`ResourceImage`；`ImageInfo.projectName/imageName/subAsset`解释为YL `ProjectId/Path/SubAsset`，原XF项目名不自动转换为构建地址。Other/TMP绑定完整组件名（如`TMPro.TextMeshProUGUI`）及公开string成员`text`，真实IL2CPP需保留该反射成员。
- 创建根菜单`Assets/Create/YLFramework/Localization/Multi Language Configs`；作者窗口`YLFramework/Localization/多语言配置`可创建语言、增删复制条目和保存/验证。编辑原资产必须由用户明确选中YL根，不监听或自动保存全部旧配置。

## 承接来源清单

| 模块 | 只读上游 | 本轮承接与局部替换 |
| --- | --- | --- |
| Localization | 1.0.9，`743876d52c5fe636effc0b63c34bd261d6cad243`，MIT2024 | 保留根/语言/条目与UGUI/TextMesh/Other、参数/附加文本和语言作者流程；事件与资源加载改接YL，定向修复回退/缓存/重复/缺项/生命周期，不带Youdao/Steam旧外部工具。 |
| ScriptableObjectConfigs | 1.1.4，`d7b6c171999e05a1840f413e1f3e2080e3853163`，MIT2024 | 保留根/条目字段、具体类型GUID绑定、查询和CRUD/排序；Resource Lease及E0树/预览局部适配，删除和登记碰撞安全加固。 |
| ExcelSystem | 1.0.7，`725f94f23327de190111573870146af66e9484b6`，MIT2025 | 保留三行表头/转换语义及SO/List/JSON用途；EPPlus不复制，替为固定MiniExcel1.31.2，增加预检/预览/提交事务与已选根适配。 |
| ArchiveSystem | 1.0.7，冻结包`com.xfkj.xfgameframework.archivesystem@8046fe56f8`，MIT2025 | 吸收模型、槽/当前项与异步/缓存用途，统一改入YL SaveStore；不搬第二Manager、`Assembly-CSharp`猜类型或DES，实现显式复制迁移/恢复和安全并发。 |

上游包及XFAB EncryptTools均只读；没有修改原XF、embedded Logger、主工程或其他worktree。完整根许可/Notices/来源资产汇总仍由集成owner合并本清单。

## DataAssets真实接点

- Runtime namespace `YLFramework.DataAssets`；保留`ScriptableObjectConfigs.displayName/configs/excel_path/type_script_guid`与`ScriptableObjectData.parent/id/config_name/icon/des`及`SingletonScriptableObjectConfigs<T>`。`TryGetIDRange`区分空目录，`MinID/MaxID`空时为0；ID/引用等长变更使索引失效，重复/名称歧义/hash碰撞明确拒绝。
- `ScriptableObjectManager.AddConfig(root)`返回借用登记`ScriptableObjectRegistration`；`Load(ResourceSystem, ResourceAddress)`返回持根Lease的登记；`LoadAsync(...)`返回`ScriptableObjectAsset<ScriptableObjectConfigs>`并拥有pending请求/Lease/登记。owner关闭Dispose自己的返回值；`RemoveConfig`显式撤本根全部owner，`Clear()`只关本域，不关闭Resource。
- `SingletonScriptableObjectConfigs<T>.LoadAsync/Instance/Release`保留具体SO单例用途。根Resource mount必须显式登记实际根类型，不扫描旧XF项目；具体子类反射/IL2CPP仍待真实Player验证。
- Editor namespace `YLFramework.DataAssets.Editor`；`ScriptableObjectConfigsController`提供`GetDataType/SetDataType/GetDataPath/CreateData/CopyData/RenameData/DeleteData/DeleteRoot/RemoveReference/Sort/Validate/Save`，菜单`Window/YLFramework/Data Assets`。字段绑定和根/子资产引用布局未替换；排序复用E0树。
- `GetDataPath(root)`返回`Assets/YLFrameworkData/DataAssets/<rootGUID>`。删除只对目标根内真实subasset或该精确目录、parent与列表归属一致的主资产执行；外来/共享引用仅显式移除，不迁入上游递归删除监听。

## 验证状态

- 已执行：路径、Git状态、B0祖先核验；读取实际Unity版本、manifest及正式Plan。PowerShell工具实际为5.1，脚本将按实际Shell或显式pwsh运行，不照搬7.x语法。
- Localization已执行纯逻辑12/12（回退、等长替换/改ID、重复/空项、格式及richText），先红后绿保留在`Temp/Extensions.Data.Localization/`；其余模块结果按最终证据补齐。
- DataAssets：父会话独立执行`Tools/Extensions/Data/DataAssets/Verify-DataAssets.ps1 -Mode Pure`，10/10、0失败/跳过/警告。子任务最终完整辅助编译Player/Editor Runtime及Editor工具均退出0，warning为40/39/4；15个作者、9个PlayMode用例仅编译未执行（PlayMode辅助参考有1个CS1701 NUnit版本警告）。`RemoveConfig(hash)`按registry唯一解析后按根身份撤销，碰撞登记拒绝已纯逻辑实际验证。
- DataAssets早期缺实现绑定、Save在途类型重复及PowerShell工具失败仅在原会话工具输出留存；其固定路径日志后来被green覆盖，不能冒称独立红例制品。最终10/10结果和当前输入仍真实可用，后续父组合探针改为唯一Run目录冻结原始log/rsp。
- Save全部时间戳日志和失败case在`Temp/Extensions.Data.Save/{offline,facade,unity-references,cases}`；父严格迁移红例在`offline/tests-20261019-193701-304.log`，绿例在`offline/tests-20261019-193729-933.log`。未执行原Save/JsonUtility联合测试的Unity部分，原断言完整保留；自有`link.xml`仅为索引/槽元数据提供裁剪声明，不代表用户模型、Json.NET或IL2CPP已验收。
- 未定位的迁移发布AccessDenied原始证据为`Temp/Extensions.Data.Save/offline/tests-20261019-191612-409.log`及`cases/e17ec4429130489dbac95dabbd41bf3e/new.importing-3770b66e78a84366aed0d1a209fc6c0b`。未添加重试或删除原件，后续通过不能替代此风险记录。
- 首轮组合源码辅助编译：`Tools/Extensions/Data/Verify-DataProfiles.ps1 -Run combined-1`退出0；完整Runtime/Editor-profile/Editor工具分别42/41/4个warning，新增普通EditMode测试源码8文件/0warning、PlayMode测试源码2文件/1warning均编译成功。没有执行其中任何Unity测试。较早Save门面在途状态造成的重复声明编译失败保留在工具调用记录，没有删输入绕过；门面落齐后真实全目录通过。
- 组Review前最终组合`Verify-DataProfiles.ps1 -Run group-before-review-fixes`同样退出0（42/41/4warning，当前普通EditMode测试9源0warning、PlayMode2源1warning）。产品日志/rsp已冻结于该唯一Run目录；包含后补的Loc重入关闭和格式context测试源码。Loc同名Run纯逻辑再次12/12绿。普通profile不声称编入可选Excel；后者由自身完整闭包单独验证。
- 审查修复最终结果：`Verify-DataProfiles.ps1 -Run group-final-review-fixes`完整Runtime/Editor-profile/Editor工具均退出0，warnings42/41/4；普通EditMode9源0warning、PlayMode2源1warning。Excel同名Run真实xlsx19/19、Runtime458/Editor102/Excel7/测试7源全部辅助编译退出0（39/4/0/0warning），15项Unity作者/资产用例没有运行。Loc同名Run纯逻辑12/12、DataAssets Pure10/10再次实跑通过。Save源自`9626d9a`后未变，复用本会话已实跑33/39证据；不把重复包含的33项再加到39上。
- 尚未执行：Unity编译、资产/GUI/PlayMode、Mono/IL2CPP Player、独立接收工程和原游戏非干扰门。完成后按证据更新。
- 许可：原Localization/SO为MIT 2024弦风课堂，Excel/Archive为MIT 2025弦风课堂。实读发现既有根许可证只有2021/2025版权，未包含2024；因此Localization在自有目录补完整`LICENSE.txt`，SO同样独立保留。共享许可汇总/根版权由后续集成统一维护，不以既有MIT文本替代遗漏的适用版权。

## 候选提交

- Localization：`517f13f`，`feat(localization): 融合多语言配置作者流程与资源生命周期`。
- DataAssets：`53edd50`，`feat(data-assets): 融合SO根子资产作者流程与安全资源登记`。
- Excel：`09bec5a`，`feat(excel): 接入免费xlsx后端与可确认SO列表JSON作者流程`。DLL沿用仓库既有LFS规则，已核验真实工作区二进制；未上传LFS/未Push，后续跨机器集成须携带对应LFS对象或按固定恢复脚本取得同hash产物。
- Save：`9626d9a`，`feat(save): 统一槽位模型异步存档与显式安全迁移恢复`。
- 最终审查/生命周期/证据与交接收口的`fix(data)`候选和本文件同一提交，不预写自引用Hash。可只读运行`git log -1 --oneline fusion/xf-ext-data -- .kilo/handovers/xf-ext-data.md`取得实际Hash；交付回复也给出准确Hash。不Push，不合并。新增meta必须由集成Unity真实生成，当前候选不是完整Unity接入包。

## 主线待验清单

- 集成owner统一生成新增文件/目录`.meta`，保留既有Save脚本身份；核查MiniExcel PluginImporter仅Editor、可选assembly依赖和默认导出隔离。不要将只有LFS pointer的文本当DLL交付。
- 实际Unity自然导入与编译，在无其他活跃owner的隔离测试会话运行`LocalizationUnityTests/LocalizationAuthoringTests`、DataAssets作者测试、Excel的15项资产/绑定用例以及原`SerializedDictionarySaveTests`的JsonUtility部分；涵盖保存重开、GUID/localFileID、Undo、取消、新引用失效、受控部分失败和回滚恢复目录。
- 实际PlayMode运行Localization六项生命周期用例及DataAssets九项Resource用例；另外按相同Domain Reload设置连续两轮，检查Scene切换、事件重复订阅、静态owner归还、晚到图片与Resource诊断归零。以上目前只有源码辅助编译，尤其审查所指关闭重入/失败释放不算已运行通过。
- 目标Mono/IL2CPP Player检查模型/具体SO子类/Other或TMP反射成员裁剪、AOT与新存档文件系统行为；验证MiniExcel及UnityEditor绝不进入Player。`link.xml`只保框架存档元数据，应用DTO和反射成员由实际应用补保留。
- 取得真实历史YL v1及Archive的停止写入复制样本后，执行显式新目录/新槽迁移及失败保源核验；加密Archive不受支持，不自动解密/转换，不能据合成fixture宣称旧档兼容。
- 重跑独立接收工程闭包（核心无Excel、显式选装Excel各一次）及原游戏非干扰门；本工作区全Runtime源码辅助编译不等于原游戏/接收工程实际编译或运行通过。
- 按唯一正式Plan统一维护共享许可/来源汇总、RootAssetTypes/AssetMetadata与阶段验收记录；本组未修改这些共享文件。Localization/SO的2024版权全文在模块本地许可证，集成不得丢弃。
