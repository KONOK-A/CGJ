# YLFramework Unity 6.3 双版本兼容实施计划

> **执行者须知：** 本文件是唯一计划与进度台账。B01-B10已按明确批准范围完成，最终入口为D/`b10-closure-final1.json`与`b10-release-manifest1.json`；本结论保留精确2022退出例外、DOTween wrapper-only及全部未验边界，不是无条件全平台/全能力通过。唯一独立只读Review及其逐项修复裁决、正式final1归档、同字节双版新鲜接收和Source最终保护均闭合。审查覆盖限制与原RED保留；分支/HEAD不变，无提交/push、新分支/worktree/追加代理，用户原有Plan/图标/数据不认领或回退。

**Goal:** 用同一套 YL 源码、公共 API 和发行包支持 Unity `2022.3.62f3c1` / `6000.3.27f1`，完成批准范围内的双版本验收与最终发行交接。

**Architecture:** 源宿主保持 2022.3，只维护当前仓库的正式源码；独立接收工程证明安装、运行和可选集成。沿用 `.unitypackage` 与现有 Runtime / Editor 边界，各引擎独立生成内容、Player 和热更基座，不共享冻结产物。

**Tech Stack:** Windows x64、C# 9、UGUI、Json.NET 3.2.1、BouncyCastle 2.7.0、HybridCLR 9.0.3、现有 Unity Test Runner / 验收脚本 / Analyzer 工具。

**Spec:** 沿用Q1-Q10选择、Release本地/Development网络分层和Excel destructive合同，逐批交互推进。B03用户明确授权通用Stage9以完整官方XML、精确选集、生命周期及原始进程日志作功能/退出分离，不要求借用B01 schema2 seal，也不追认旧v5/h24。唯一退出已知项仍为`U63-KI-2022-EDITOR-EXIT-001`，仅完整功能结束后挂起适用；失败、skip、缺结果、执行中卡死及未知崩溃均不豁免，-1/forced不改为进程通过。

**导航：** [当前状态](#status) · [固定边界](#constraints) · [批次安排](#batches) · [证据索引](#evidence) · [批次记录](#batch-results) · [最终签字](#release) · [验收合同](#contracts) · [历史档案](#history)

<a id="status"></a>

## 1. 当前状态

**截至2026-10-30 04:20Z附近：B01-B10按批准支持范围完成，B10Completed=true、FinalReleaseAccepted=true。** 下表为分层证据，不代表全部条目本次重跑；沿用/新跑/未覆盖在`b10-package-mapping1.json`及发行清单分别登记，未修2022退出缺陷、历史RED和许可HOLD不被签字抹去。

| 能力 / 原任务                     | 已有有效证据                                                                                            | 保留边界与证据层级                                                                    | 对应批次           |
| --------------------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------ |
| 环境、隔离、初始接收 / 1-2 | 最终同字节final1在z1/6.3与z2/2022最小依赖裸导入及实际Mono图形业务冒烟正常exit0；既有双引擎×双后端20轮场景证据按身份沿用 | 不把本次Mono冒烟冒称新IL2CPP/DSP/OS输入；UUM-156125仅有限输入未复现 | B10 |
| 主体、Editor、资产 / 3 | Physics、SceneHandle、TreeView、GraphView、Help修复、双版受影响回归及B05正式资产/消费者闭合 | GUID/meta及源资产未漂移；其它Unity补丁/平台不外推 | B02、B03、B05、B10 |
| 自动测试、输入、渲染 / 4 | B02/B03适用集合沿用；B10修正GC观察边界后两版Foundation各245/245正常exit0；B05 DSP/B06真实输入/URP按精确身份复用 | Source-host/历史输入、物理热插拔/触摸、人工听音/OS loopback未验；旧Clipboard等RED不抹去 | B02、B03、B05、B06、B10 |
| Resource / 5 | r6原生140 case、成本A/B/C与发布专项沿用；2022受影响路径闭合；final1源码/归档差异及相关补充证据已映射 | 140保留原r6源载体，130正常exit0/10受控kill；三处后续产品delta有分别归属，不称final1新跑140或全系统无明文观测 | B03、B10 |
| Analyzer / IDE / 6 | 当前DLL原始发行对照仅72B PE/debug身份、208方法体/27EH同字节；双版Unity/VS及B07 Rider证据沿用，9份Player排除闭合 | 不唯一归因SDK；181历史执行不称B10新跑，临时Temp原输出已不在；不称IDE全库无错误 | B03、B07、B10 |
| DOTween / Excel / DomainExcel / 7 | B04同一2022四集成172叶；B10两版11程序集/3宏共存与Excel各41/41；最终四安全optional包逐条审计 | 最终安装准确为source/meta-equivalent复制而非ImportPackage；DOTween仅wrapper，vendor/Pro再分发HOLD | B04、B10 |
| HybridCLR / 7 | B08固定ParameterOwner runtime普通Release/Development闭合；B09双版共享Support两Player/28约束/2223冻结闭合；B10实际native前后像增强Verify通过 | 官方未修改6.3 native不声明已修；2022仅Development code-only子集；Release本地snapshot与Dev TLS分开 | B01、B04、B08-B10 |
| 最终交接 / 8 | 唯一独立Review四项裁决、八项保留判断、六件交付物清单、同字节双版新鲜接收、2553保护文件零差异、5939冻结payload复核完成 | 批准支持范围内签字；不提交/push/UPM化，不追加reviewer或扩大功能 | B10 |

**当前阻断按归属处理，不互相替代：**

| 批次      | 首要问题                                        | 已确认边界                                                                                      |
| --------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| B01 | 功能验收完成，2022退出缺陷为非阻塞保留项 | 双版本Edit161/Play25及Common适用性已实际通过；2022发现阶段附已知问题接受，测试两进程实际正常退出；不外推全计划完成 |
| B02       | 已按批准适用范围完成功能验收                    | 当前普通1837与全部适用集合通过；旧Clipboard/GC/超时RED保留；历史输入待补及平台不适用不计已验证 |
| B03 | 按批准范围完成功能验收，退出缺陷未修 | 当前2022普通1837/Legacy788及受影响专项闭合；其中693功能完成后退出-1/forced，明确已知限制，不记exit通过 |
| B04 | 功能及安全可选包导出完成，DOTween vendor再分发仍HOLD | 同一2022四集成编译/发现、172测试正常exit0；DOTween只含自有wrapper，不代接受EULA或声称vendor许可闭合 |
| B06 | 已按批准适用范围完成 | 当前candidate13共16矩阵位/17份正常exit0结果、1003真实OS事件；三键/标准控件/拖拽/生命周期/键盘/API级原生鼠标移除/按住期间几何取消及2022交叉闭合。用户已确认那次IL2CPP按钮RED为人工误触，原件保留；不外推物理热插拔/触摸或全计划 |
| B07 | 已按批准适用范围完成 | 6项关键原生观察、2个正常exit0生命周期、97守卫、五输入/meta恢复、9份当前包Player依赖审计；原MCP pragma异常仍按历史协议观测保留，不改写成通过 |
| B08 | 已按批准维护runtime范围完成 | 固定源一行patch、参数RED→GREEN、普通Release V1/本地V2重启、Development TLS/三层负向、11正常exit0及两基座冻结闭合；默认官方native仍不声明已修 |
| B09 | 已按双版本共享Support子集范围完成 | 两Native Player、四A/B项目加载、28约束、2223冻结文件；h44/external1仅单次产物接受，原timeout/forced/-1未改 |
| B10 | 已按最终批准发行范围完成 | 唯一Review重要问题归零，final1新鲜双版导入/冒烟及必要回归、分层复用/支持许可、最终保护与冻结核验闭合；原RED和所有明确限制保留 |

<a id="constraints"></a>

## 2. 固定边界

### 身份与版本

| 项目                | 固定值 / 已核实事实                                                                                                                                        |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Repository / Branch | `G:\Unity\Projects\CGJ2024` / `integration/xf-yl`                                                                                                          |
| HEAD                | `dc31794e991ac7fc83b33ff9366a8031a75f8a1b`，本次整理只读重新确认；不提交、推送、切分支或创建 worktree                                                      |
| RunId / 交付根 D    | `U63-20261027-c63a1` / `G:\Unity\U63-20261027-c63a1`                                                                                                       |
| 临时证据根 T        | `C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1`；历史 `TempGoal/...` 记法对应此根                                                               |
| Source / 2022 接收  | `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe`，revision `1623fc0bbb97`                                                                |
| 6.3 接收            | `G:\Unity Editor\6000.3.27f1\Editor\Unity.exe`，revision `672d562a42a7`                                                                                    |
| 2022 接收依赖       | UGUI `1.0.0`、UTF `1.3.9`、NUnit `2.0.3`、Input `1.14.2`、Rider `3.0.40`、VS `2.0.28`                                                                      |
| 6.3 接收依赖        | UGUI `2.0.0`、UTF `1.6.0`、NUnit `2.0.5`、Input `1.20.1`、Rider `3.1.0`、VS `2.0.26`，已由实际解析闭合，不再标为预检候选                                   |
| 工具链              | PowerShell `7.6.9`、SDK `9.0.312`、Node `v24.19.0`、Unity MCP CLI `1.0.0`；VS2022 `17.14.37027.9`、VC x64 / Windows SDK 已有真实构建证据                   |
| HybridCLR           | 官方包 `9.0.3` / commit `6cadd24d101f4cbd00cd80190106f6621132d24d`；6000.3 官方 native tag 为 `v6000.3.x-9.0.1`，安装标记 `9.0.3` 属官方约定，不手改或升级 |

包文件均位于D；正式交付为final1默认包、四optional final1及独立native source-patch-only ZIP，完整六件SHA/长度/许可审计见`b10-release-manifest1.json`。下表同时保留历史候选，旧包字节/owner/证据不重绑。

| 候选           | 文件名                                              | Bytes   | SHA256                                                             |
| -------------- | --------------------------------------------------- | ------- | ------------------------------------------------------------------ |
| 正式默认final1 | `YLFramework-U63-final1.unitypackage` | 4555368 | `012aacc474616e63a45cf8474e3f5690e70811db62ba0279300f8490752a2762` |
| B06-B09冻结候选 | `YLFramework-U63-candidate13.unitypackage` | 4554569 | `e9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8` |
| B02-B05冻结候选 | `YLFramework-U63-candidate12.unitypackage` | 4554431 | `2365eeb5047c482da3b947f1ad982463dfef732429e7a8ad79169255351e38d0` |
| HybridCLR 可选 | `YLFramework-HybridCLR-U63-candidate4.unitypackage` | 82897   | `81852d451410be62b6bda56c8e4102e0b170d7314ca752fac4c921bdec48d701` |

final1相对candidate13仅Documentation/README.md payload变化，1038路径/GUID/meta与全部可执行payload一致；candidate13相对12仅PanelInputCapture.cs变化。候选12保留B02-B05范围；candidate11（4553996B、SHA`f53224654f3c974d6583d1154fb6cbd49f34a7b516120a824a62ec2592021e1f`）及其B01/基本输入历史接收仍冻结。所有沿用依据及r6原生源三处后续delta分别见`b10-package-mapping1.json`，不是旧结果改签新owner。

### 授权范围

- 主体、Resource、Analyzer、DOTween、Excel、DomainExcel、HybridCLR 均纳入原验收范围；不拆成两套 YL 源码/API。Windows Editor 与 Windows64 Mono / IL2CPP 之外不扩展平台。
- 内建管线主验，独立 6.3 URP 仅验 UI、过渡和资源显示；不引入默认 URP 依赖，不扩展 HDRP / 自定义 Renderer Feature。
- Development Mono / IL2CPP 验完整 HTTPS 安全/更新；IL2CPP 非 Development / Low 验本地资源、加密、原生热更。Mono Release、Release HTTPS、High stripping 不在本轮强门禁；不搭新远端、不改系统信任。
- Legacy / New / Both 均验证；编辑器合成输入、元数据存在、构建成功不能替代实际 Windows 输入消费。音频配置或 `ClipLoadedOnly` 不能替代 DSP 推进。
- Excel / DomainExcel 沿用 destructive 持久提交：预览、取消、提交、失败回滚、旧表头、不部分写入；不新增或宣称提交后 Undo。既有领域作者窗口 Undo 单独验。
- Module、热更 MonoBehaviour、泛型/async 等继续按原合同覆盖。历史“真正热更 UI/ECS”不自动成为新增功能、依赖或强门禁；进入 B09 时核原授权，未明确批准则只记未覆盖。
- UPM 化、宿主升级、新功能、全库重构、无关警告清理均后置；第三方版本升级、native 维护源/补丁实施、破坏性 API/数据变化须另有明确授权。

### 保护与证据

- Source当前保护身份为用户在B04确认的PID `11568`、2022.3、唯一 `Assets/Scenes/YLFrameworkDev.unity` loaded且干净；旧PID `5556`只属历史。B06开始已实时只读核验project/version/PID/场景及非Play/编译/更新/失败，默认IDE仍Code；后续操作前后仍须重核，不能把此快照当持续实时状态。
- 不停止、重启、Play、NUnit 或接管 Source 场景，不对同 projectPath 开第二实例。MCP 仅用于身份/状态守卫、必要定向 `ImportAsset(..., ForceUpdate)` 与正式导出；默认 IDE 保持 Code.exe，不借 Source 操纵窗口/输入。
- 不改 Source ProjectVersion、manifest/lock、ProjectSettings、AGENTS、权限、忽略、MCP 或全局/Git 配置。保留用户旧 XF Plan、Android 图标差异、YLFrameworkData、HybridCLRSettings、旧夹具/meta；精确原清单见 D 的 `source-baseline.json` / `protected-before.json`。
- 公共 API、namespace、程序集名、GUID/fileID、资源键、存档/Excel/JSON、所有权、安全与精确版本合同不放宽。兼容分支只改实际入口，保持 C# 9；2022.3 可读源资产交付，6.3 批量重序列化不倒灌。
- 新 receiver 先核父目录/无链接/未占用并建立独立 owner，再做同 SHA 正式包裸 Create→Import；保存首次证明后才加 SDK/测试/Input/URP/可选插件。不搬 Source manifest/Settings/Library，不领养旧输出、重绑包或 owner。
- 内容、缓存、AOT metadata、Cpp/native、基座、补丁按引擎/后端/Development 身份隔离；下一构建前归档必要原生输入。冻结消费者不 Sync、不手改造绿，尤其保留 h6-h21、h17 Library/Player/基线和 crash 原件。
- Unity、测试、构建、Player、IDE 状态操作串行；研究/只读审查仅在选定批次内按需并行。只停止身份核验通过的自有进程/服务；隔离 Company/Product/输出/数据，不接管玩家缓存、偏好或注册表。
- 官方`-runTests`不加`-quit/-nographics`；discovery→selection→XML叶名和数量精确闭合，无失败/非预期skip。Strict默认仍要求正常退出；仅显式Known2022EditorExit模式可在完整原子完成seal、fixture生命周期、实际结果/选集/源码/meta及关闭阶段证据均闭合后接受已知退出异常。实际exit/forced/processSucceeded保留失败，功能结论为PassedWithKnownIssue，不回写为全绿；零测试、宏缺失、执行中超时、取消/异常或缺产物仍失败。
- Player 不用 headless 冒充图形/音频/输入行为；`ExpectedVersion=1/2` 是内容版本。BadSignature / Payload / Metadata 按各自拒绝层验，不以普通异常、网络失败或无成功 JSON 代替。
- 已通过且源码/环境指纹有效的 Resource140、成本 A/B/C、Analyzer181、三集成136、native 正负链不重复长跑，只重验实际受影响部分。每次记录证据身份和沿用范围，不凭单一完成百分比推工期。
- 保留全部 RED、失败 XML、receipt、冻结源码/制品和后续纠正；新尝试用新 Run/目录，不覆盖失败或借旧结果签新产物。不打印密钥、私钥、凭据或 Hub 完整命令行。

<a id="batches"></a>

## 3. 剩余批次

**状态：B01-B10按批准范围完成，最终技术门禁已在`b10-preclose-final1.json`实核，最终计划/回执绑定为`b10-closure-final1.json`。** 唯一h44/external1产物接受与原失败分别保留，不升级为通用退出豁免；其它未覆盖范围与DOTween vendor HOLD保持。

2026-10-28 16:39Z后仅恢复B01补充A→补充B，两个子批已一并明确授权串行实施；仍各自以60-90分钟为检查点，记录真实结果，后续B02-B10保持未启动。

| 批次                    | 对应原任务 / 前置                       | 本批只做                                                                                                | 验收出口                                                                                                    |
| ----------------------- | --------------------------------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| B01 Editor 场景宿主 | 7；功能验收完成，退出项保留 | 已验证严格宿主/Session、Common适用性、双版本Edit161/Play25及生命周期 | 原148保留、无skip/失败/漏项；2022测试实际exit0，发现阶段按精确已知退出例外附限制接受 |
| B02 6.3 自动回归 | 3-4；按批准适用范围完成 | 当前普通1837、GC/Foundation、Resource补集、Legacy788、Both235/New229、双Play3及覆盖清单闭合 | 2525适用Edit/788 LegacyPlay，所有接受组正常exit0；历史输入待补和平台不适用明示，源宿主不冒跑，不称单次混跑 |
| B03 2022 受影响路径 | 3-6；已按批准范围完成 | 普通1837、Foundation246、Analyzer78、native28、publication10、受影响Resource138、Legacy788、Both235/New229/双Play3及6.3交叉5闭合 | Legacy693退出-1/forced独立保留，用户明确允许功能/退出分离；不称所有进程退出或整体发行通过 |
| B04 可选集成与许可 | 7；安全分发范围已完成，可独立于B08 | 同一2022四集成编译/发现、172叶正常exit0；Excel/DomainExcel/HybridCLR/DOTween wrapper四包精确审计 | MiniExcel原始Apache/归属与XF MIT随附；DOTween vendor/官方HybridCLR SDK不打包，许可HOLD不解除 |
| B05 正式资产与音频 | 3-4；功能验收完成 | 七资产重导入/预览、正式Module ColorSelector/Tips、六作者用例、生成/迁移证据复用及13段真实DSP | b8非Dev Mono正常exit0，AUDIO_PASS/QUIT_PASS与全部阈值通过；人工听感/OS loopback未验，不称全发行或OS输入通过 |
| B06 真实 Windows 输入 | 4；已完成适用范围 | 当前candidate13的16矩阵位+1严格交接回归、17次正常exit0、73离线守卫、Source保护闭合 | 六个旧基本组合按原SHA复用；人工误触RED原件不覆盖，不外推物理热插拔/触摸或整体发行完成 |
| B07 IDE 补证 | 6；适用范围已完成 | Rider原生pragma/Error/反向恢复、正常重启保留规则、预定义/asmdef作用域对照；97守卫与9份Player制品审计 | 五输入/meta回baseline、默认IDE不变；无新Analyzer/Player构建，不把协议或截图元数据核验代替原生画面观察 |
| B08 普通 Release native | 7；批准维护variant范围已完成 | 方案/额外批准后固定源一行patch与官方local安装；参数RED→GREEN、普通Release V1/V2及Development三层负向闭合 | h17/官方缓存/Editor不改，2完整基座冻结/164+22回归；不等官方上游已修、B09/B10或最终发行完成 |
| B09 HybridCLR 子集 | 7；双版本批准适用范围完成 | h46维护runtime/h44官方2022tag，两Native Player/四A-B加载/28约束/冲突后正向恢复/generic/async与2223文件冻结闭合 | 唯一编译产物单次授权接受，原超时失败保留，其后Strict exit0；无UI/ECS新增，不代B10发行 |
| B10 Review 与发行 | 8；全部批准强门禁按限定范围已闭合 | 唯一独立Review及F1/F2/F3修复/F4裁决、最终导出/许可、同字节双版新鲜接收、必要回归、文档及protected-after完成 | 重要意见0、六件正式物料与证据清单、2553保护文件零差异/5939冻结复核；不提交/推送 |

### 单批执行规则

当前规则（2026-10-29 00:57Z）：用户已返回并改为从B02逐项推进，恢复下列单批规则。夜间自动连推授权及定时接续不再生效；普通细节仍可自主决定，但不自动转入后续批次，不改验收门槛。每次先核现有证据，只运行当前问题的必要受影响集合；重型fixture提前按工作量单独安排，不在短总预算下反复整组重跑。

1. 每次只进入一个明确批次，普通细节可自主推进；不因计划存在就自动开工，不将历史“持续 Goal”文字当授权。
2. 60-90分钟只作进度/成本检查点，不是完成承诺或当前批停工期限；沿用用户在B06之后明确的同批持续授权。到点更新进展，不赶时间强退后记绿；实际权限、环境或重大方案冲突才暂停依赖项。
3. 完成当前批准批次后更新前部状态和本文件记录并停止，不自动连做下批；检查点本身不要求用户再次授权同批工作。B10的Review与发行仍分别设检查点。
4. 窗口/前台/设备条件不满足，结束依赖部分；不盲试，不用 `AttachThreadInput`、Alt 注入、ForegroundLock 修改等绕策略。用户旧窗口授权仅适用于当时核验的自有 Player。
5. 同一根因连续两次有依据的修复仍失败，保全并暂停相关项；公共合同变化、第三方升级、引擎/许可/权限阻断或用户修改冲突同样暂停，不扩大排障范围。
6. 中断后先核当前源码/制品/进程身份，不从旧 PID、旧 wakeup 或旧“正在运行”续跑。计划不授予自动审批、外部访问、无人值守、网络或不休眠配置权限。

### B01 执行入口（按批准新口径完成，本轮停止）

- [x] 重新核计划、工作区、保护与新receiver身份；h20/h21及h22/h25 RED保留，旧端不Sync，未改Session业务/恢复断言。
- [x] 对照Stage9及精确UTF源码，接入早于官方保存/替换的只读准入，宿主创建留在RunStarted。
- [x] 严格schema2/root-owner/project/精确Unity/CLI与干净唯一已知场景；GUI/旧schema/PlayMode不注册；实际dirty/unknown-saved启动拒绝在官方套件前退出。
- [x] h27专项15/15、完整Edit161/161、Play25/25，无skip且正常exit0；证据检查确认原148叶全部保留，仅增加13叶，当前源码及meta与接收端一致。
- [x] 新h35实际2022完整Edit161/161、Play25/25正常exit0，原148业务叶保留；发现阶段以精确已知退出例外接受，h24历史失败仍保留，未以Source编译代替测试。
- [x] 登记本轮结果、阻断和证据入口，交付后停止，未启动B02。

### B08 授权门（方案、额外批准及实施验收均已完成）

- 第一检查点只交维护源/fork/patch 方案：精确上游与修改 SHA、可维护来源、接收端官方 local 安装路径、回归范围和授权边界。已有只读定位不是修改上游的授权。
- 已证最早错误链为 AOT `System.Array.InternalArray__RemoveAt(Int32)` 参数索引进入 Interpreter 表；小 dump 缺 live MethodInfo/image，h15/Development 存活原因仍未知。不归因显卡、验收宏、缺裁剪 AOT 或泛化 ABI。
- 若获准实施，先验证最窄参数反射及 AOT 数组/泛型/Interpreter 控制，再验真实 JSON→Model[]→AddModel→Module.Running、V1/V2 与受影响负向。不删 Module、不全 Preserve、不用未裁剪 DLL 替最终 AOT，不重建 h17。

<a id="evidence"></a>

## 4. 证据索引

以下 D/T 采用第 2 节定义；链接定位本文件对应历史段，原件路径定位外部证据。沿用前须核指纹，历史名称带 `green` 不保证实际 Passed。这里不重复全部 Hash、命令和失败演进。

| 编号 / 能力               | 关键原件入口                                                                                                                                            | 使用边界 / 历史段                                                                   |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| E01 归属与保护 | D/`owner.json`、`source-baseline.json`、`protected-before.json`、`protection-b01-{before1,after1}.json` | 本批2553→2553、差异0；checkpoint12为旧证据，本批after不替代最终发行protected-after；[H01](#h01) |
| E02 裸导入与场景链        | D/`ReceiverA-first-import-initial.json`、`ReceiverD-first-import-initial.json`；`Runs/{a,d}-*-yl-loop*/verdict.json`                                    | 候选3初始双版/两后端；非最终包；[H02](#h02)                                         |
| E03 Editor 兼容修复       | D/`w5-tests-EditMode-help-crlf-green2*`、`w5-tests-EditMode-ordinary-edit1*`；`v5-tests-EditMode-help-crlf1*`                                           | 6.3 普通1833通过；2022叶通过但进程挂起；[H06-H08](#h06)                             |
| E04 Resource140           | T/`resource63/{compatibility-targets-v2.json,Verification/gate4.json,VerificationGuards/green9.json}`                                                   | 显式 SourceIdentity 载体 r6，不冒充最终包；140 case / 45 guards；[H10](#h10)        |
| E05 成本与发布            | T/`cost-w5/cost-baseline2/{finished.json,f03-owned-cleanup.json}`；D/`w6-tests-EditMode-{publication2,prepare-repeat-green1}*`                          | A/B/C 3/3、发布10/10、重复复验1/1；[H09](#h09)、[H10](#h10)                         |
| E06 Analyzer / VS         | `Temp/YLFramework.Analyzers/build-info.json` / TRX；D/`Analyzer-w6/{owner.json,ide-targets.json,VS-host2..VS-host7}`                                    | 181测试与窄范围原生IDE序列；非最终整分支Review；[H11](#h11)                         |
| E07 Rider | D/`Analyzer-w6/Rider-rider2`旧协议；`Rider-b07-1`/`Rider-b07-2`新原生/生命周期；`b07-closure-final2.json` | 旧13协议组与新原生证据分别闭合，97守卫、五输入恢复、Player9制品审计；旧历史见[H12](#h12) |
| E08 输入 / URP            | D/`n2`、`n5`、`n6`；`n5-input-metadata-inspect2.json`、`n6-input-metadata-inspect2.json`；`Runs/j-Mono-urp-visual1`                                     | n3/n4 为污染历史不作生产保活证据；inspect2 仅元数据；j仅URP Mono显示；[H13](#h13)   |
| E09 主体 Play / Clipboard | D/`q-play-coverage-legacy1.json`、`q-tests-discovery-list4.json`、`q-tests-EditMode-clipboard-controls2*`                                               | 788分组集合闭合，非单次同字节混跑；Clipboard未绿；[H13](#h13)                       |
| E10 三集成                | D/`e2-tests-EditMode-coexist-{domain1,excel1,dotween-edit1}.verdict.json`、`e2-tests-PlayMode-coexist-dotween-play1.verdict.json`                       | 同环境四组共136叶；不是四集成/2022/Player；[H14](#h14)、[H16](#h16)                 |
| E11 HybridCLR 身份        | D/`h14e/h14-inputs-green1.xml`；T/`hybridclr-guards-*`                                                                                                  | 持久输入22叶真实Unity通过；114纯守卫不代表native；[H16](#h16)                       |
| E12 Development 正向      | D/`h15e/{base-receipt.json,patch-receipt.json,NativeBaseFreeze,h15-update-v2-2*,h15-v2-restart1*}`                                                      | 同Player V1→签名HTTPS→重启V2；h15旧Release有验收符号；[H17](#h17)                   |
| E13 三原生负向            | D/`h17e/h17-bad-{payload1,signature1,metadata1}*` 及对应server请求/退出记录                                                                             | 精确各拒绝层、同Dev Player正常退出；[H18](#h18)                                     |
| E14 普通 Release AV       | D/`h17e/{base-release-receipt.json,ReleaseBaseFreeze,ReleaseCrashFreeze/receipt.json,h17-release-v2-1*}`；T/`h17-release-crash-analysis1/analysis.json` | AV、无成功JSON；PDB/dump匹配，禁止覆盖Library/Player/基线；[H18](#h18)、[H19](#h19) |
| E15 历史Editor卡点 | D/`h18e/h18-async-context1.xml`、`h19e/h19-pump-marker1.xml`、`h20e/h20-async-green1.xml`、`h20e/h20-edit-full1.xml`、`h21e/h21-session-green1.xml` | 历史async2/2、完整147/148、Session1/2；原件保留，当前6.3结论看E16；[H19](#h19) |
| E16 B01最终6.3 | D/`b01-evidence1.json`、`Test-B01Evidence.ps1`；D/`h27e/{h27-{scenes-session-green1,edit-full1,play-full1}*,h27-startup-{dirty,saved}-green1*}` | 原148+新增13；15/161/25三组正常退出；启动拒绝2；纯守卫124；保护2553。B01Completed=false，非整发行签字 |
| E17 B01的2022阻断 | D/`h24e/h24-discovery1.{json,log,log.process.json}` | 发现数据154/25，但正常退出回执缺失；专项未执行，不能套用最终源码的2022通过结论 |
| E18 补充A最终闭包 | D/`b01-supp-a2.json`、`Test-B01SupplementA.ps1`；`h32e/h32-common-{batch,gui}-green1*`、`h33e/h33-{edit,play}-full1*` | Common5/GUI5、Owned161/25正常退出，原148保留，真实编译图不再用声明补齐，复制meta安装时冻结 |
| E19 补充B工具与引擎失败 | D/`b01-supp-b1.json`、`e4e/e4-pure-exit1*`、`e6e/e6-pure-update1*`；T/`b01-pure-exit-analysis-20261028-6f83057c2e814c689adba1af660f9102` | 进程20/20、最终守卫155/155；四份自有挂起dump本地保留，纯Unity退出仍失败，不算2022专项或B01完成 |
| E20 新验收流程与2022功能闭包 | D/`b01-knownexit-workflow1.json`、`Test-B01KnownExitWorkflow.ps1`；`h35e/h35-{discovery1,edit-functional1,play-functional1}.acceptance.json` | 发现PassedWithKnownIssue、真实exit-1保留；Edit161/Play25均实际正常exit0，原148保留，功能与退出结果独立 |

**已纠正事项：** FileWatch 正确汇总为 228 完整8B头、322 raw/summary miss、350 metadata-only，`FullWriteObservationProven=false`；BadMetadata 固定名一直是 `bad-metadata-fixture.json`，不再按旧误记改名；热更 UI/ECS 未自动获授权。原错误记录保留在历史中，后续不得当作当前事实。

<a id="batch-results"></a>

## 5. 批次记录

新结果只在这里按批次追加，并同步第 1 节状态；不再向旧历史长列表续写。每条使用以下字段，不建另一份过程报告：

| 字段           | 内容要求                                                             |
| -------------- | -------------------------------------------------------------------- |
| 批次ID / 状态  | B01-B10；待执行、进行中、完成或未完成/阻断。只有验收真正闭合才标完成 |
| 范围与完成项   | 实际改了什么；哪些原合同被覆盖；不得扩到下批                         |
| 实测结果与入口 | 命令/Run、源码或包指纹、discovery/selection/XML/正常退出、结果路径   |
| 沿用证据       | 明确 E 编号和仍有效的指纹/范围，不把历史绿直接套新源码               |
| 未完成原因     | 首因、RED原件、环境限制/授权门，不能省略失败                         |
| 下批前置       | 最小恢复条件；本批交付后停止，不自动启动                             |

### 2026-10-28 计划整理（不计执行批次）

- 范围：重组当前状态、固定边界、B01-B10、证据索引；原任务1-8改为验收合同，历史按阶段折叠保留。
- 状态：仅文档整理；没有启动 B01、Unity、Player、IDE、测试、服务或新研究任务，没有修改代码/配置或提交/推送。
- 验证：已回读结构并核对内部锚点；20组折叠区标题及开闭标签配对，关键证据入口存在；全文件 `git diff --no-index --check` 无空白错误。已跟踪改动的 diff 指纹整理前后一致（本Plan仍未跟踪）；只作文档检查，不新增技术通过结论。
- 下批前置：后续明确进入某批时先核当前身份与保护状态；默认下一批 B01。

### B01：Editor 场景宿主（2026-10-28，本轮停止，未完成/阻断）

**本轮结论：** 6.3 owned接收门禁已闭合；2022专项未执行，普通GUI/旧schema新增场景夹具整组适用性未签字。B01整体不标完成，不自动进入B02。

<details>
<summary>展开本批RED、独立Review与修正过程（最终结果见下方）</summary>

- 范围：仅 HybridCLR owned saved-scene 前置、场景安全/Session 回归、6.3 完整 EditMode 与相关 PlayMode、2022 同夹具专项；不进入 B02，不处理普通 Release AV。
- 开始：15:06Z 用户明确进入 B01，60-90分钟检查点；当前分支/HEAD未变。Source只读守卫确认PID5556/2022.3、单一原场景干净、非Play/编译/更新/失败、默认Code。
- 沿用证据：E15 的 h20 async2/2、完整147/148与h21 Session1/2；已回读h21 XML，明确最后场景不可卸载。新回归先在h22记录RED，修复与2022分别使用新的独立接收身份，旧端不Sync。
- Ruling：修复官方RunStarted后的宿主前置，不放宽Session恢复/业务断言；新场景只在严格owner/版本/CLI身份下创建。Context7配额不足，依据精确已安装UTF源码与原XML，不冒称取得新官方在线证据。
- RED：h22相同冻结产品裸导入后安装54测试文件，实际发现Edit154（148旧叶+6新回归）/Play25；`h22-scenes-session-red1`实际8叶1Pass/7Fail、0skip、正常exit2（15:22:35Z），复现空setup及原Session最后scene关闭失败；原件在D/h22e。纯CLI守卫`b01-scenes-red1`116项1Fail，新增绑定检查按预期失败。
- 修复候选：仅新schema EditMode传root/run/ownerSHA，官方RunStarted核身份、精确双版本和干净已知场景后新建saved-empty host，并写独立scene回执；PlayMode/Source不注册。守卫`b01-scenes-green1`116/116，尚不代替Unity结果。新测试的MarkSceneClean误用已按2022官方绑定源码纠正为仅自有临时场景的ClearSceneDirtiness反射，Source真实编译已恢复，原场景未脏。
- 初轮GREEN：h23场景/Session8/8、完整Edit154/154、相关Play25/25，均无skip、正常exit0；scene回执绑定project/version/owner/run。该候选随后被独立只读审查发现安全与适用范围缺口，因此不是最终签字。
- 独立Review：`ses_e7cdcabb6ffecZ0OWC4FeQI2q5`指出RunStarted准入晚于UTF保存/替换、额外clean-untitled可放行、GUI/旧schema正例适用范围及meta GUID未绑定。已核UTF1.6.0 TestStarter:49-52与TaskList/CreateBootstrap实际顺序，四项均纳入本批单轮修正，不扩至B02/B08。
- Review回归RED：h25相同包新接收，实际Edit161（148旧叶+13新回归）/Play25；`h25-review-scenes-red1`15叶7Pass/8Fail、正常非零退出；`h25-startup-saved-red1`旧候选忽略启动准入探针、exit0且生成XML，负向verdict=false。PS实际meta身份回归`b01-review-meta-red1`124中6Fail→`green1`124/124，含GUID错配、meta缺失/链接/变更及scene字节变更。
- Review修正候选：InitializeOnLoad准入先于UTF下一Editor update；短期订阅sceneSaving/Closing/Opened，只读拒绝未知/脏/多场景，在RunStarted前注销再创建新空宿主；GUI/旧schema/PlayMode不注册；scene回执同时绑定实际meta GUID与SHA。启动dirty/unknown-saved仅在已核owned receiver的专用负向探针中构造，并须exit1、无XML/host及原件不变，不冒充native Player或套件通过。
- 2022专项阻断：h24裸导入/Resolve/Prepare正常退出；`h24-discovery1.json`已生成，但native日志停在Input/PhysX/licensing shutdown。等待原有有界进程后，PID36592及wrapper42900已不存，工具句柄不可用且`.log.exit.json`缺失，无法确认正常退出或制造退出码。冻结h24，不启动测试，不将发现JSON的Passed当通过；不接管Source、不重试到绿。
- 检查点：本批采用90分钟上限（16:36Z），不再启动2022重试；下一步只完成最终候选的必要回归、保护复核与本批交付，届时停止。当前B01仍未完成。
- Ruling：h26为既有安全规则保留的历史名字，Initialize在写输出/启动Unity前已明确拒绝；保留失败descriptor，不删除或放宽拒绝规则，改用全新h27。后续最终验证和启动准入探针均以h27为准，不把h26记成有效载体。

</details>

- 最终实测：h27相同冻结两包裸导入→Resolve→54测试文件→本机Prepare/Discover。`h27-scenes-session-green1`15/15、`h27-edit-full1`161/161、`h27-play-full1`25/25，均0skip/exit0/无timeout；`h27-startup-dirty-green1`与`saved-green1`均按预期exit1、无XML/host，前者未保存脏输入，后者scene/meta字节不变。此两项为启动负向证明，不是native Player验收。
- 证据闭包：`Test-B01Evidence.ps1`核原148叶Missing=0、新增13叶均为RunnerSceneTests；正式发现源码与当前仓库SHA一致、新增meta/GUID一致；三组selection/XML/正常退出及scene/meta身份闭合；两个启动拒绝、最终纯守卫124/124、保护2553→2553/差异0。唯一结果D/`b01-evidence1.json`明确`EvidenceChecksPassed=true`、`Unity63CurrentPassed=true`、`B01Completed=false`、`Legacy2022CurrentPassed=false`。检查器初次以PSCustomObject传发现数据被既有字典类型守卫拒绝，改为AsHashtable后通过，未放宽守卫或覆写结果。
- 修改范围：新增`HybridCLRRunnerSceneTests.cs/.meta`，修改测试辅助`HybridCLRTestFixtures.cs`、三个HybridCLR测试/守卫PS工具及本Plan；Source的`HybridCLRSessionTests.cs`原候选未改，Runtime/Editor产品、包字节、依赖、设置、第三方及旧native冻结均未改。本批新增测试GUID为`6d0972a05a50241468a6d67b0710c275`，Unity生成。
- 保护与交接：指定两C#格式Check及本批局部diff检查通过；Source最终只读守卫仍PID5556/2022.3、原单一场景干净、非Play/编译/更新/失败，默认Code。未运行Source测试/Play、未启动Player/IDE/服务、未提交或推送，未进入B02。审查的启动准入、唯一性、实际meta绑定已有对应回归；非owned注册门禁参数例通过，但不宣称已完成普通GUI/旧schema的整组实跑，亦未做B10整分支Review。
- 未完成与下批前置：2022专项仍因h24发现阶段缺少可验证正常退出而未执行；需有依据地恢复退出门禁，再用最终夹具在新2022载体取得专项结果。新增场景guard回归实跑依赖owned saved host；普通GUI/旧schema只验证不注册门禁，涉及Additive场景的整组适用性还需单独收口，不把五个注册参数例外推为这些环境全部可跑。不得借h24的Passed发现JSON、Source编译或6.3绿色补签，不在Source执行测试。当前停止B01，不自动扩至B02/B03排障。

<a id="b01-supplements"></a>

### B01补充A/B（2026-10-28 16:39Z授权；A完成，B阻断停止）

- 范围：A收口通用注册测试与owned场景集成的适用集合；B补进程终态/中断证据、最小2022退出对照和最终场景/Session专项。用户“最高权限”不等于修改权限配置、依赖、Source场景或native维护源的授权。
- 开工：当前Source守卫仍PID5556/2022.3，原单场景干净、非Play/编译/更新/失败、默认Code；`protection-b01-supp-before1.json`通过。工作区全部旧改动保留，不提交/推送/切分支。
- Ruling：先在新h28以真正不带owned宿主参数的官方runner复现并登记旧13项适用性，不凭静态依赖推测运行结果；通用与专用集合必须明确声明，不能靠跳过、直接返回或缺项造绿。所有Unity仍由主线程串行操作。
- 并行只读：子任务仅研究h24已有退出证据与现成抓栈工具，不写工程、不启动/附加Unity；结果用于B，不重复A实现或操作同一Editor。
- 补充A已完成（17:18Z）：h28实际无owned参数RED为13叶6Pass/7Fail、0skip、正常exit2，七项均在Additive创建处被Unity拒绝。保留5个通用注册参数例于原脚本/GUID，8项原宿主/场景断言移入`HybridCLROwnedRunnerSceneTests.cs.txt`，只经显式Owned安装生成；没有Ignore/提前返回或删除业务断言。
- A声明合同：安装不可变`tests-installation.json`，Common=5通用/0专用（总Edit153）、Owned=5通用/8专用（总Edit161）；实际发现绑定安装SHA、编译源码集合及meta，拒绝漏/多源码、错配置/owner、缺少/篡改专用模板等。纯守卫134中10RED→134全绿，再追加源码集合136中2RED→136全绿；全部原件保留。
- A实际验收：新h29 Common在无owned参数batch与普通GUI（无`-batchmode`）各5/5，均exit0且未生成/请求owned host；新h30 Owned完整Edit161/161、Play25/25正常exit0。`Test-B01SupplementA.ps1`证明两配置原148业务叶全保留、Source/meta/模板/选集/XML/退出闭合，结果D/`b01-supp-a1.json`。Source守卫及`protection-b01-supp-a-after1.json`通过，2553文件保护未漂移。
- B开始（17:25Z）：只读研究确认h24 Prepare为同引擎graphics batch/Win64正常退出对照，不能因最后日志提Input/PhysX/licensing归因；当前缺真实native栈且无现成cdb/procdump。子代理仅实现独占进程生命周期辅助与纯进程测试，主线程准备无产品→UTF→HybridCLR有界对照，所有Unity仍串行；不重跑h24/v5冻结端、不改固定依赖。
- B工具实现：新增`Invoke-HybridCLROwnedProcess.ps1`及20项真实pwsh子进程回归，覆盖正常/非零/超时/启动失败/验证器错误/取消及观察器强杀；观察器强杀仍明确不能保证finally。新模式两个入口共享它，启动前identity、真实process、KillRequested、finally终态均不覆写发布；unknown/null不转0，Kill后30秒有界。独立GREEN源SHA核对一致，证据T/`b01-process-green-final2-e7ea3067cc804cebb026ccf8d8a46311/results.json`。
- B发现消费门禁：新增9例RED→GREEN，缺正常退出、null/未知、错owner、forced、timeout、cancel、observer error、非零均拒绝；不能单独消费Passed发现JSON。普通测试仍官方`-runTests`，未加`-quit/-nographics`。
- B实际最低层：e4为仅官方modules/UGUI1/Json.NET3.2.1的纯Unity新工程，无YL/HybridCLR/UTF；`e4-pure-exit1`完成标记后在delayCall请求Exit0，240秒仍活，独立持柄观察器30秒后及间隔10秒成功采两dump，终态TimedOut/forced/exit-1。e6只将调用相位改为一次update，`e6-pure-update1`同样失败并采两dump，否定“只换delayCall位置就修复”。Source与两失败载体均未改写，未继续扩UTF/HybridCLR层。
- B其它受控失败：e7确认固定Editor的CLI异步入口不支持Task返回类型，实际NonZeroExit，未造通过；按其明确签名要求改用新e8的async void原生`-quit`能力对照，依赖Editor事件的完成未发布且仍超时，未将提前退出或缺结果当绿。该诊断flag只用于纯控制，从未用于正式`-runTests`。准备器单元素数组展开错误的e1、短名预算拒绝e22a/e22b和已占用e2拒绝均留原件，后续改用新短载体，未放宽260预算或领养旧目录。
- 原生只读结论：两份e4 dump/PDB身份匹配，主线程两次处于Unity `lane_guts+0x3d6`，约11.15秒内CPU增10.83秒；Curl worker增10.84秒。栈内有Job退出排空和Task_DoRequest代码候选，但Unity完整unwind缺runtime xdata，具体request/tag、锁owner及依赖关系未知。不把GPU、Mono或delayCall当已证根因，不全局AbortByTag、不跳Job drain、不改Editor二进制或系统信任。完整报告和脚本见E19；dump含敏感进程内存，仅本地保存，不上传。
- 独立Review与单轮修复：`ses_e7c44b9b0ffeMkCLwShGamxUJ5`指出声明补齐实际源码、Common meta基线、未显式Owned/大小写及不可达callback guard。已逐项核实修正：实际集合只取编译图及已加载程序集配置；安装冻结source/meta且发现前复验；新receiver必须显式Common/Owned并在写前规范大小写；去除旧不可达compat分支，4项直接抽取真正parent接线并执行短自有pwsh进程，验证callback真/缺/错hash/非零，替换旧假覆盖。新增7项RED及parent测试作用域失败均保留；最终155/155全绿，含实际parent接线。
- 审查后最终复验：新h32 Common无owned参数batch5/5、普通GUI5/5；新h33 Owned实际Edit161/161、Play25/25，全部0skip/正常exit0，均使用新进程事实回执。D/`b01-supp-a2.json`核安装/实际源码/Source及meta/选集/XML/退出、原148叶全部保留；E18替代A1作当前Source证据，旧h29/h30不改写。模板`.cs.txt`只称Roslyn/真实Unity编译，不冒称格式器覆盖。
- 最终保护：`protection-b01-supp-final1.json`通过，2553→2553/差异0；两C#格式Check与本批局部diff检查通过。Source最终PID5556/2022.3、原唯一场景loaded/clean、非Play/编译/更新/失败、默认Code；临时Unity均结束，仅既有Source/worker/Hub保留。无Source测试/Play、无Standalone Player/Installer/native构建、无依赖升级/权限配置/Git写操作，无B02/B08实施。
- B交付与未完成：D/`b01-supp-b1.json`核20/155、四个原生失败事实和四份dump/hash/归属，明确`ToolsCompleted=true`、`EngineExitRestored=false`、`Unity2022FixturePassed=false`、`SupplementBCompleted=false`、`B01Completed=false`。纯层阻断后2022专项未执行；本轮按17:25Z起90分钟检查点收口停止。下一步仅能基于Job/Curl退出排空的进一步可复现引擎证据或另行明确批准的环境/版本方案恢复，不把“最高权限”理解为绕门禁、强退记0或默认升级。

<a id="release-gates"></a>

### B01退出根治限时尝试（2026-10-28 19:04Z，已停止，未修复）

- 用户要求“先尝试根治，但别一直死磕”。本轮约30分钟预算、最多两个有据对照；19:18Z即决定收口，未消费完整预算或开第二轮盲试。只改D下诊断辅助和本Plan，未改产品/测试源码、权限、Source设置、系统网络、依赖或Editor版本。
- 新依据：进程继承的HTTP_PROXY/HTTPS_PROXY指向loopback且无userinfo；只保留存在性/分类，不记录原值。新p1同固定2022、同graphics batch/Win64、同e4原诊断源码，源码SHA256两侧同为`ad49911a9598a2de370fd38b53668063abda6b33c26af79a7f7539d37cd10fbf`。唯一对照移除Unity子进程的HTTP_PROXY/HTTPS_PROXY/ALL_PROXY，父观察器/Source/系统代理未改，TLS验证不变。
- 实测：`p1-no-proxy-exit1`仍完成后挂起，240秒超时，KillRequested先落盘、受控结束、实际exit-1，`succeeded=false`。独立观察器两份新本地dump成功，不能把dump-writer的0记为Unity退出。候选“只去掉继承代理变量即可修复”被否定，不据此排除系统代理或其它网络因素。
- 限时只读补查约6分27秒：匹配PDB确认`s_CurlExecutor`全局RVA`0x4356c00`，但normal dump连首8B未捕获，TPI类型记录数0。官方活跃请求inventory未找到，UWR不是UnityEngine.Object，不能FindObjectsOfType枚举；无有证字段偏移、状态、请求来源或tag。给出192B有限主动捕获入口只代表后续深查可能性，不等于已有安全修复接点，不在本轮继续追指针/扫堆/扫字符串。
- 产物：D/`b01-targeted-exit1.json`、`Test-B01TargetedExit.ps1`、`p1e/p1-no-proxy-exit1*`；T原分析目录下`supplement_uwr_result_20261028.json`及两只读脚本。结果明确`ExitFixed=false/B01Completed=false`。所有失败和旧e4/e6/e7/e8原件保留，不调用全局Abort，不升级、不改Editor二进制，不开始B02/B08或2022专项。
- 停止决定：缺少安全的具体请求归属，继续需更深的native内存与字段逆向；依用户限制本轮不扩域。后续只有新证据或明确单列的环境/版本决策再恢复，不把本轮作为新长期任务自动续跑。
- 交付检查（19:21Z）：本轮实际约17分钟，D/`Test-B01TargetedExit.ps1`通过，`protection-b01-targeted-final1.json`为2553→2553/差异0；Source最终仍原PID/场景clean、非Play/编译/更新/失败，临时Unity均结束。没有升级/提交/推送，根治与B01完成均不宣称。

### B02-B06夜间连续执行记录（2026-10-29 00:57Z结束自动模式）

- 授权：按B02→B03→B04→B05→B06串行推进，普通非危险操作自主决定；无新Agent Manager会话/分支/提交/推送，无B07-B10或native修复实施授权。
- 起始保护：分支`integration/xf-yl`、HEAD`dc31794e991ac7fc83b33ff9366a8031a75f8a1b`未变；实时Source守卫为PID5556/2022.3、唯一YLFrameworkDev场景loaded/clean、非Play/编译/更新/失败、默认Code。D/`protection-b02-b06-before1.json`通过，2553→2553、差异0。
- B02起步：新6.3载体先以candidate11真实裸Create→Import，之后才装测试；旧q/w5/w6/h35等保持冻结。并行子任务仅在既有Clipboard调查上继续只读判别研究，不操作剪贴板/Unity/窗口，不重复主线程状态操作。
- Ruling：采用用户本次连续执行授权覆盖原逐批停止，不改变强验收门禁；局部根因/人工条件穷尽时记未完成并推进无依赖批次，成本是最终可能存在明确未闭合项而非伪造全绿。
- B02新载体：w7实际Create/裸Import均exit0、Errors=[]，包仍candidate11；后置安装959文件测试闭包及固定UTF1.6.0/Input1.20.1。Legacy配置/发现正常退出，真实Edit2618/Play791，receiver适用Edit2526/Play788，全部Runnable；source-host为Edit42/Play3，historical-bound Edit9、optional-excel Edit41保持独立，不在Source冒跑。
- B02普通回归：`w7-tests-EditMode-clipboard-baseline1`原4叶全部Passed、exit0，mixed-before/after均自有54字节、sequence4929一致；完整`ordinary-edit1`1837/1837、Difference/NonPassed=[]，2026-10-28 21:55:03Z正常exit0，无timeout/forced/exit-stall。未改产品或断言；Clipboard旧失败根因尚未闭合，本次不复现不宣称修复。
- Clipboard诊断纠正：只读续查确认q旧controls1/2的原生复制控制在Has前置断言即失败，尚未执行SetSerializedProperty，不能说已证明原生property copy失败；Malformed的false亦可能在Has提前拒绝，并未证明Parser污染。此前推断不作为根因。后续若复现，先保留同次已有A/B/C读取与分支、不加额外getter/sleep/retry/影子缓存；当前不为未复现缺陷修改生产代码。
- B02指纹与沿用：D/`b02-inventory-inventory1.json`只读核1038个正式包记录与当前Source差异0，w7安装对Source差异0；q/tests4只有Stage9ReceptionTests后续optional接线差异，业务测试未变。四个PNG meta在q/w7均发生相同接收端导入变化，不倒灌Source；已抽查legacy.png GUID保持。D/`q-play-coverage-b02-recheck1.json`以原8组selection/XML/正常退出重新核788→788、Missing/Extra/重复0，仍不是单次混跑或统一历史测试字节。
- B02当前状态操作：`w7 resource-session1`已启动原完整补集673叶（隔离范围减成本3、发布10和TwoPlay3），绑定新T/`b02-w7-resource`独立owner/非生产回环凭据及环境恢复观察者；后台`bgp_18485e0350013wDk4HD2k29OoN`，1800秒硬截止。600秒monitor返回时仍运行，未读取终态、不签Passed、不启动第二个Unity。既有成本A/B/C与发布10仅在实际指纹核对后沿用，不重复长跑。
- 并行边界：B03前置仅委派内部只读证据盘点`ses_e7b6ff7ddffed4T47HnMdQ0zaw`，无Unity/MCP/进程/配置操作；主线程仍B02，不与其重复研究。B02未收口，B03-B06尚无新增实际验收结论。
- B02等待依据（22:22Z）：同一tracked wrapper仍在运行；process原件绑定Unity PID14136、StartedUtc=21:58:49.6907979Z，原1800秒截止约22:28:50Z。日志已实际执行多次Bundle构建并到BuildPlayer，不是已证退出挂起；Observer目录目前只有request/identity，无finished/XML终态，不能记部分或整组通过。保持原deadline，结束前不另启Unity。
- B03只读前置已返回：v3普通1833/Analyzer77及NoReload2属于旧candidate6/旧测试SHA；v5无seal且退出失败不可追认，h35仅HybridCLR B01。需新2022候选11裸导入、当前发现/普通/Resource与Analyzer实际回归；y的IL2CPP本地加密仅在相关未变文件SHA重核后局部沿用，resource63的140项不能替代2022。现有Stage9没有ExitPolicy，先按真实Strict流程取得结果，不能因历史挂起就断言新进程必失败；若需Known流程，必须有通用owned producer与父层严格绑定，不能借h35的HybridCLR seal或给历史v5补签。
- B02第一检查点（22:39Z用户提醒后已实际接续，未结束目标）：原w7组于22:28:49.9671170Z达到1800秒，TimedOut=true/ForcedExit=true/ExitCode=-1、FinalXmlObservedWhileRunning=false，无verdict/finished，整673项不可签绿。进程已退出，仅Source5556存活且实时守卫仍clean/编译无错/默认Code；w7的Library/临时构建/原始日志全部保留，不领养或清理。此前唤醒等待未及时接续是执行衔接缺口，不改变失败事实或授权。
- B02新w8：candidate11裸Create→Import实际exit0后装测试；新`two-play1`3/3（Domain2+Equipment1）、`core-both1`235/235、`core-new1`229/229全部正常退出、Difference/NonPassed=[]。独立profile/restart/discovery均留原件，非OS输入证明。
- B02最小诊断增量：只在`ResourceCostEvidence.Observer`的叶TestStarted/TestFinished通过既有CreateNew/Flush/Move写入唯一GUID进度JSON，不改产品/测试断言/超时，不把逐叶进度当最终验收。Source格式与局部diff通过；w8以新tests2回执同步，初次遗漏ProofRun因缺smoke4在写入前拒绝，纠正为显式tests1。新Legacy2/legacy-list2真实Unity编译/发现通过。
- B02拆分先证：`resource-runtime2`按真实命名空间40/40、exit0、完成观察器通过；最初人工估86在执行前被选集门禁拒绝（不是86项测试失败），没有将缺失项记绿。D/`Invoke-B02SplitRegression.ps1`的preview2对原673叶精确扣除已过40后，将633叶分为78个fixture组；每组用完整叶名锚定过滤器，防止fixture内普通叶被扩选，分组总和/唯一/无漏/无多及命令行预算预检通过。preview1旧宽fixture过滤计划仅PlanOnly保留、不执行。T/`b02-w8-resource`为新的独立PFX/owner域。
- B02拆分实测：w8的`b02-split1-f01..f07`全部正常exit0，共43叶，含正式Core资源/迁移/Variant等35叶；f08为Timer保活四叶，仅Invalidated在旧473行弱引用断言失败，3Pass/1Fail、正常非零exit2，无timeout/forced/exit-stall，observer明确EnvironmentRestored=true。不是Audio失败，不是进程例外，原叶的handle状态断言在weakref失败后未执行。原D/`w8-b02-split1-aborted.json`、XML、exit及T/`b02-w8-resource/b02-split1-f08/finished.json`冻结，w8不再导入/Sync。
- B02持久续跑：w9重新裸Create→Import、安装tests1、UTF/Input固定、Legacy1/legacy-list1实际通过，独立T/`b02-w9-resource`。经w8/tests2与w9/tests1全部SourceSha逐文件相等及原selection/XML/exit重核，673精确分为已过83（40+43）、未闭合Timer4、剩余586（70组）。`w9-b02-split2-preview1-plan.json`预检通过后，持久tracked driver `bgp_184c2364b001ttO7JA1WdpYKZd`/PID37904按70组串行实际执行，每组600秒、失败即停，不并发Unity；原w7混组RED不覆盖。终态是D/`w9-b02-split2-finished.json`或`-aborted.json`，未读取到终态前不记586通过。G盘约88GB空闲、C盘约479GB空闲，无清理旧证据。
- 夜间接续保障：已设本会话cron `wku_184c2803a0016LkTbeOxjIgpK4`（每15分钟）收取当前持久driver的真实进度/终态并续目标，不把定时等待报告blocked，不以阶段性final冒充交付；目标结束前清理cron。主线程状态操作仍串行，等待时只做独立源码/证据工作。
- Timer只读研究（`ses_e7b41f518ffeMoHolCrDeMyFmK`已回）：源码已在Detach清Owner/Callback/TickCallback/OnCancelled，Dispose通知列表在finally经ListPool.Clear回收；未发现漏清直接证据，实际保活根仍未知。旧w2同Invalidated混组失败、单叶成功仅证明上下文相关，不能归因Mono或用单叶替代。候选仅改测试：将创建+终态操作整体置于NoInlining helper、scheduler仍由外层持有且增加KeepAlive，原两次Collect/finalizer及weakref/handle断言不减；新增`RetainedTerminalHandleAfterInvalidationHasNoOwnedReferences`结构叶核同generation、四引用及_owned/_pending归零。不改生产Timer或包字节。
- Timer候选状态：两份本轮Source测试文件格式/局部diff通过，但该候选尚未实际Unity测试，不称GREEN或已修根因。当前w9仍是旧2618叶测试闭包、四Timer叶已明确未闭合，绝不在其运行中Sync；候选须等本次driver终态后，在新6.3载体实际跑5叶及Foundation全夹具。新增叶也匹配RetainedTerminalHandle隔离规则；新发现预计Edit2619/receiver2527，数量必须实测，不改旧发现/选集/回执。w9非Timer受控集合仍按其原SHA单独计证，不冒称全测试源码统一字节。
- 保护检查点：D/`protection-b02-checkpoint1.json`仍2553→2553、AllDifferences/AuthorizedDifferences/UnapprovedDifferences均空、Passed=true；实时Source PID5556原唯一场景loaded/clean、非Play/编译失败、默认Code。并行`ses_e7b3ac65dffeQbXgWHiZyIIf7C`仅盘点已授权B04接口/许可、无状态操作/文件写入；B03-B06尚无新增实跑验收，不能签这些批次完成。
- 首90分钟检查点（23:15Z定时自动接续已实际触发）：持久driver仍运行，已经实际完成w9 `b02-split2-f01..f28`并逐组通过正式UTF、选集/XML/环境/正常exit0门禁，f29当时在运行。已抽读f28原verdict为23/23、Difference/NonPassed=[]、CostEvidencePassed/ProcessPassed=true；不按tracked logs重复行重复计数，整586的总量等终态聚合核验。未启动并行Unity、未报blocked、未结束夜间目标。
- B05前置区分：w9/f03的2个AudioAcceptanceResource叶只推进完整RunAudio首个yield后即Dispose，证明初始化与中断恢复，不证明DSP。真实DSP既有入口`Tests/Acceptance/Core/AudioAcceptanceEntry.cs:132-274`还需实际运行到Listener RMS、half/mute/pause/resume/fade/mixer/crossfade/spatial和退出责任断言；不能以本次Audio相关绿色代替。`ses_e7b2ca6b4ffeILcZESYGDDhe66`仅只读定位B05现成工具/资产，不操作Unity或文件。
- B04前置研究已回：现有Stage9只支持Excel/DomainExcel/DOTween，没有HybridCLR或四集成合并通道；不能把e2三集成四测试组136叶称四集成。不要安装会与主体`YLFramework.Tests.PlayMode`同名的native reception harness；后续仅Editor共存无需native Install。D/Tools为旧工具快照，继续使用Source实际入口。2022同环境编译/发现/窄测试尚未执行，HOLD保留。
- DOTween指纹纠正（23:22Z独立实证）：当前Source DLL及现有PS/C#guard pin均为`d74bfc3f37cd97d90f7f08a9a73e204c5eae66e77be24b12378a4e99f1690843`。主线程新隔离下载固定官方216935B ZIP，SHA仍`689d42944f7076038eb6b87ed4c85ae6951c4aec8eaae4e068524ce55b422c8c`，只读归档`DOTween/DOTween.dll`177664B SHA恰为同一d74，Source前后不变；readme SHA`37b99ffdbb8b01b159d807b1fe77db664ae5b3b201734a5ad602c7c7f83941f0`。原件T/`b04-dotween-provenance-fed45cc4b19943bb9b1b38fe3eb7ab95/result.json`及ZIP/owner/download保留。未解压、未加载/执行DLL、未改vendor/依赖/工具pin；先前对话摘要的3124不是当前文件事实，不能据此改pin或接受双SHA。此项当前来源冲突已消除，不回写历史回执。
- 许可边界仍在：标准免费DOTween当前官方许可及指定commit的Robert Penner BSD完整条款已定位，但历史条款冻结、DLL-only再分发和完整vendor附带notice仍未闭合，`VendorRedistributionAuthorized=false`。XF ExcelSystem1.0.7 commit725f94f2的MIT版权为`Copyright (c) 2025 弦风课堂`，MiniExcel固定Apache正文保留；requiredCommit6a9e2508仍是内部合同祖先，不改为vendor或HEAD。可先做自有wrapper-only显式导出，不带vendor/Pro/IncludeDependencies，不冒称B04已完成。
- B02 w9终态（23:25Z）：f01..f29共29组/165叶正常exit0，f30 `ResourceBundleRuntimeTests`为20/28、8Fail、正常exit2，无forced/timeout，observer环境恢复通过；D/`w9-b02-split2-aborted.json`冻结。8Fail定位为4个SceneHandle装箱类型比较、1个disposed prefab NRE、2个缺依赖逻辑归因及1个Atlas初始化Pending；未到达的后半断言不视为已通过。剩余f31..f70未执行，不改原RED或继续使用w9。
- Timer候选实测与撤回：新k1/candidate11裸Create/Import、当前测试编译/发现通过；`timer-terminal1`5叶4Pass/1Fail，失败变成Cancelled的weakref仍存活（Invalidated与新增结构叶通过），正常exit2、无forced/timeout。仅改变栈帧不能视为修复，主线程已撤回整个NoInlining终态阶段候选，原4叶方法/GC次数/原两个断言及原ScheduleCallbackTarget逐字恢复；仅保留新增结构诊断。实际保活根仍未知，B02此项保持未闭合，不Sleep/多收集/Ignore/概率重跑到绿，k1原件/Library冻结。
- Resource有据最小修复：`ResourceSystem.Instances.cs`只前移原`var asset = prefab.Asset`，在访问会被Dispose置空的Record前复用原ODE校验；不新增catch/null fallback/public接口。`ResourceContentValidation.Inspect`在非ResourceException的已有失败转换前仅为Bundle拼入LogicalId及原RelativeFile，Failure既有Kind/cause转换不变；故缺依赖仍早于native拒绝且保留原逻辑归因。测试两处Scene.handle显式int，Atlas先Wait同一InitializeAsync（原10秒门限），缺文件测试新增LoadFailed及最底层FileNotFound.FileName精确匹配；原全部回滚/责任/身份断言保留。八个本轮C#文件Format Check及局部diff通过，无新增/改动asmdef或meta/GUID。
- 新正式candidate12：Source5556/原clean场景/非Play编译更新失败守卫通过后只调用正式`ExportUnityPackage.Export`，未SaveAssets/保存场景；4554431B、SHA`2365eeb5047c482da3b947f1ad982463dfef732429e7a8ad79169255351e38d0`，Audit1038记录通过。按路径结构化比较11→12精确仅Instances.cs与ResourceContentValidation.cs payload变化，全部路径集/GUID/meta/目录身份不变；原files.json无序，文本diff重排不是产品大改。结构化原件D/`k2-c12-remainder-preview2-plan.json`，不重绑旧包回执。
- k2 GREEN：candidate12新鲜裸Create→Import、post-install测试与固定UTF1.6/Input1.20.1、Legacy1/list1均实际正常退出；`native-bundles1`原完整28叶现28/28、无skip/diff/nonpassed、OS exit0（23:54:27.9976097Z）、无timeout/forced/stall，observer PassedGate/CostEvidencePassed=true。XML SHA`4892e73a36c6d771a5272b513713505b7c5fb93f185c08bae0f37a1acf9f0944`，T/`b02-k2-resource/native-bundles1/finished.json`保留完整后半生命周期/并发/回滚/清零，Atlas实际走到克隆/纹理/销毁/no-cache。不是IL2CPP Player或Audio DSP验收。
- B02当前作业（00:02Z）：D/`Invoke-B02RemainingCandidate12.ps1`从原w9不可变70组意图精确取f31..f70，按当前k2官方树逐组核fullname/数量/唯一/无漏多/参数预算，预检通过40组/393叶。最初人工估375在执行前被总量门禁拒绝，纠正为真实原586−前29组165−native28=393，没有减少任何实际叶或写成功回执。持久driver`bgp_184f6390b001YJ04LY7P7bYRXa`/PID3196运行`-Prefix c12-remainder1`，每组600秒、失败即停止并保留；终态D/`k2-c12-remainder1-finished.json`或`-aborted.json`。原674隔离意图含新增结构1，旧Timer4仍未闭合，当前只接受实际已读结果，不签393或B02整体通过。
- 夜间调度纠正：旧固定w9状态的cron已删除，现有`wku_184e965dd001K8sXNTKOxp6KnM`每15分钟先按唯一计划最新事实找当前作业，不盲追旧PID/旧候选。当前继续B02，B03-B06尚待串行实跑；Source2022实际编译/导出不冒充candidate12新鲜2022接收，GC未知不扩成其它测试豁免。
- B02第二保留项（00:12Z）：k2 remainder/f31 CatalogBuild1/1与f32 CatalogCodec15/15正常exit0；f33实际是`ResourceContentBuildTests`32叶，不是CodeCompilation。00:12:28.8676172Z达600秒总预算、forced/exit-1、无最终XML/finished，driver严格abort，k2全目录/Library/原件冻结。逐叶记录已有24 start/23 finish、无已读失败；最后进入`PublicationFullyAuthenticatesMiddleSegmentsInsteadOfAcceptingOnlyShaAndFirstLastProbes`于00:12:04.991Z，距硬截止仅24秒，不能称单叶卡死或把23条进度计功能通过。37后续组未执行；回补须新载体，按明确重型工作量独立预算/分组，不减少数据规模、断言或加概率重试。
- Ruling（转入B03）：candidate12两处生产修复已新鲜编译并native28完整GREEN，源码稳定；B02仍未通过的GC与重型fixture预算不作为其它版本验证的前置，保留所有RED/未测并先推进B03无依赖范围，最终B02只报部分而非篡改门禁。Todolist已将B02保留项pending、B03唯一in_progress，B03前Source实时守卫和D/`protection-b03-before1.json`2553→2553、差异0通过，仅Source5556主Editor存活。
- B03实际起步：全新v7/candidate12/固定2022.3.62f3c1、UGUI1.0裸Create→Import均正常exit0，随后安装当前959文件测试与UTF1.3.9/Input1.14.2。Legacy1与官方UTF companion发现list1真实正常exit0（00:25:53.9197976Z，PID57692，无timeout/forced/stall），Edit2619/Play791；本次没有发现退出缺陷，因此不实现不需要的Stage9 Known桥、不改验收规则。
- B03首次普通RED：v7 `ordinary-edit1`完整1837实际1835Pass/2Fail、0skip、65.95秒，正常exit2（00:28:49.6520918Z，PID56088），不是退出挂起。Mixed复制baseline为空、native-copy控制又在Has前置断言失败，尚未执行SetSerializedProperty；本次DisableEditDrawer consumer通过，不能合并根因。v7及其XML/log/process/exit冻结，1835不是整批通过；Analyzer58+20等叶的individual pass不能伪造成同组正常exit0接受。
- Clipboard下一诊断：既有研究子任务`ses_e80269932ffezFZslopjrH4Bfe`只获一个控制方法的测试侧修改，将原序列的Has/cache/clear/native-copy观察先保存后用Assert.Multiple全部严格断言；不减少失败、不新增OS getter/sleep/retry/用户文本输出、不改产品。主线程之后用新2022载体实跑；不是为概率重跑换filter。
- B05前置研究结束：没有现成的owned完整音频scene/build脚本，已确认完整RunAudio需要group.loadAsync=true、Listener与Entry同变换层级、真实PCM、mixer和全RMS/退出链；Stage9的Peak=0及初始化两绿都不可代替。已授权内部`ses_e7afb2052ffeRjns2A6W4ouxXA`仅在三个新Tests/Tools文件离线准备最小audio-only安装/Generate/BuildMono及pure guards，不改共享Stage9/Runtime/任何receiver或运行Unity，主线程仍串行B03。后续实际B05统一用当前candidate12与新身份，不采用研究示意中的旧candidate11。
- 夜间后续原件补记：v8因测试控制使用目标NUnit3.5没有的Assert.Multiple真实编译CS0117/exit1，生产裸导入先已通过；该失误不通过升级依赖解决。现控制改为原观察全部保存后`Assert.That(bool[], Is.All.True)`，四条件不减、OS读取/采样次数不增、未知文本不输出；新v9当前测试编译及Strict发现实际exit0，Source5556也已实时确认编译无错/原场景clean。
- v9/candidate12的2022专项已读GREEN：Analyzer规则/策略78/78（XML SHA`add8b0bff132ab0bc9094fee56dd9efa524598beb1e3c14ccadc6e07c5828a22`）、原生ResourceBundle28/28、Publication10/10，均官方UTF、无skip且正常exit0。不是2022主体整体通过，v7的1835/1837 RED仍保留。v9尚未运行修订Clipboard诊断或其它剩余集合。
- B05离线工具已返回三个新文件（B05AudioAcceptanceBuild.cs.txt / Install-B05AudioAcceptance.ps1 / Test-B05AudioAcceptanceGuards.ps1），主线程独立重验34/34 pure guards及C#9真实SDK引用语义0错误；T/`b05-audio-tools-main1`保留。主线程将Editor.applicationPath按实际API的forward-slash结果先GetFullPath再比较固定exe；未安装到真实receiver、未Generate/Build/Run/DSP，不宣称B05完成。
- 2026-10-29 00:57Z用户返回：明确从B02逐项推进，结束夜间自动连推。已删除当前cron`wku_18526fcbc001PuA1tSoFeyryVI`，无pending wakeup，所有本会话受控background job均已exited/failed；不停止或接管Source。已完成的B03证据和B05离线工具保留，但不自动继续B03-B06。

### B02 收口方式与最终结果（已完成，附批准范围限制）

- 不从零重跑：先按candidate12与测试源码指纹区分可沿用、已受影响验证、未测；原验收出口不减，未跑项仍列未测，不把已发现2619叶机械地当成每次必跑量。
- 收口前未闭合项为Timer GC四叶、重型构建32及后续37组、候选与历史矩阵映射；以下最终记录已逐项处理，旧失败保留。Clipboard不把2022现象直接当6.3失败，也不声称系统原生读取根因已修。
- 每次只选一个明确问题，先确认最小判别/回归集合及成本，再执行；重型构建与纯逻辑/会话组分开，不把600秒总预算超时误诊为单叶卡死。
- 不再为普通小失败机械地扩建工具或扩大验收面。已冻结的接收端和原件仍不改；新执行按包/依赖/代码身份和必要隔离选择载体，不重复已证有效的成本/native/IDE长跑。
- 达到本批验收或检查点后报告“已修/已验/未闭合”并停在本批；不因旧夜间Goal文字再次自动跨批。

#### B02 最终验收记录

- 只改必要测试/宿主：Timer四终态改为两版已安装UTF都支持的UnityTest参数枚举，GC观察前归还一次Editor测试循环，原四叶名、两次Collect/finalizer、weakref/终态断言保留并显式KeepAlive scheduler；生产Timer不改。k3 `timer-terminal1`5/5、普通Foundation245/245与独立干净宿主Session1/1全部正常exit0，联合覆盖完整246。首次混组246中Session前置拒绝脏场景的RED保留，不修改该拒绝门禁。
- 实际补齐重型构建32及原剩余组：`b02-close1-f33`32/32；交付适用3/3、内容适用45/45；f35/f36的7+16、f38..f47的108、发布修正4/4、f49..f70的160均完整通过且正常exit0。CodeCompilation/Generation、真实构建发布、Undo、Inspector、Workbench GUI消费者均在原断言下执行；没有以进度JSON冒充最终XML。
- 发布测试旧硬编码Count=6在独立接收端实际为2；改为从当前Unity TypeCache取得完整回调身份并与CacheReuseBlockedBy逐项相等，仍要求非空、WasUnityBuildInvoked/ForceRebuild/非CacheHit。原6→2 RED及一次错误namespace编译RED保留；修正后完整4叶GREEN，生产缓存策略未改。
- 用户通过question明确选择“按适用范围收口”：`PreE04OwnedCatalogIsConsumedWithoutRebuildingOrRewritingItsBytes`归historical-bound、固定旧输入待补；`UnsupportedBackendDoesNotImportCngOrFallBackToPlaintext`归platform-not-applicable。两用例原测试与原RED/Ignore保留、没有生成替代制品或改成Passed；其它未授权源宿主/历史用例保持原范围。新profile与官方discovery记录完整2619 Edit叶，只有当前适用receiver2525进入本批集合。
- 当前candidate12/k3最终普通`ordinary-final1`1837/1837；Legacy Play按既有正确宿主八组实际新跑693+6+15+6+7+20+39+2=788，无重复/漏项；当前Both Core235、New Core229、NoReload连续双Play3均实际正常exit0。没有把w7/w8/q旧结果重绑为当前包证明，也不将这些测试称实际Windows OS输入或DSP。
- D/`b02-closure-final2.json`只读核94份原selection/XML/owner/package/真实exit/observer，EditExpected=EditCovered=2525、Missing/Extra=[]、LegacyPlay788、B02CompletedWithinApprovedScope=true；其中39份沿用旧包有效分组（成本/发布及未受两处生产修复影响的输入），明确各自原SHA，不是当前包新跑或统一测试字节。候选11→12只两处Resource生产payload变更，相关native28在k2/v9完整双版GREEN；当前普通/输入矩阵已新跑。Source-host、optional与历史/平台清单完整写在同一回执，均不冒充已验证。
- 格式/局部diff通过；既有Stage9拒绝守卫6/6通过；D/`protection-b02-final1.json`2553→2553、所有差异数组空、Passed=true。最终Source实时仍PID5556/2022.3、唯一YLFrameworkDev loaded/clean、非Play/编译/更新/失败、默认Code，OS仅该主Editor存活；没有提交/推送/改权限或固定依赖。B02在上述批准范围内结束，本轮不启动B03。

### B03 最终验收记录

- 当前v9为非Source、candidate12新鲜裸Create/Import通过的固定2022.3.62f3c1/revision1623fc0bbb97接收端，UGUI1.0/UTF1.3.9/Input1.14.2未变；实际普通1837、Foundation245+独立Session1、Analyzer78、Resource原生28、PublicationIo/Retry10全部完整官方XML、精确选集、无skip且正常exit0。旧v7 Clipboard失败不回写，新结果不宣称Windows原生剪贴板根因已修。
- 受影响Resource实跑138叶：CatalogCodec15、Content45、ContentUpdate31、Update40、Publishing4、成本A/B/C3，均完整结果/环境恢复与正常exit0。历史固定输入、非适用编译与Source-host沿既有明示范围不计已验；没有拿6.3 resource140替代2022结果。
- 两处测试适配：UTF1.3.9的EditMode runner拒绝直接yield AsyncOperation，native场景卸载现等待同一operation.isDone并逐次yield null，在10秒内断言完成，业务/回滚/责任断言不减。2022独立宿主零未跟踪回调，发布测试按完整实际身份分别严格断言“零回调命中完整缓存”或“有回调Force/native且非CacheHit”。生产Runtime/包/接口未改，原两份RED保留；k3 `b03-cross1`在6.3将原生Update1+Publishing完整4实际交叉验证5/5、正常exit0，两文件格式通过。
- 发现与退出原件：v9/list3实际正常exit0；list4及NoReload发现完整XML后退出挂起、forced/-1，均保留失败。三份发现JSON逐字节同SHA`ff40de0809d3cdc4f14b1ba22a05ce0b738db98a6df3674e11cc671dfdd22171`，继续引用正常list3作选集oracle，不补签失败发现或概率重跑；Both/New独立配置/发现实际正常exit0。
- 本批question用户选择“功能与退出分离收口”，明确批准B03通用Stage9使用完整官方XML/精确选集/生命周期/原日志独立核功能，不要求B01 schema2 seal、不伪造seal、不追认旧v5/h24；执行中挂起、失败/skip/缺结果、未知崩溃仍拒绝。D/`Accept-B03Stage9Run.ps1`只允许当前v9/固定2022/包SHA，核真实process/owner/selection/XML哈希及shutdown，CreateNew保存接受回执、拒绝漂移/重基线，所有原件保持真实。
- Legacy Play788按8个正确宿主组精确闭合：693+6+15+6+7+20+39+2；`b03-play-01`693/693、0skip，完成后ExitStalled/forced/exit-1，接受为PassedWithKnownIssue且ExitPassed/ProcessPassed=false。其它95均正常exit0；不称全788进程都通过或单次混跑。NoReload双Play3、Both235、New229均正常exit0。`twoplay-final1`观察器消失、无XML/exit，不计已验；新持久有界`twoplay-final2`3/3正常退出，未补造旧终态。
- 局部旧指纹绑定：candidate9→12的CngSegmentAead/ResourceBundleEnvelope/ResourceLocalSnapshot payload/meta逐项同SHA；原`Runs/y-Il2Cpp-encrypted22-candidate1`正常exit0、非Dev/IL2CPP，BuildGuid`b9eb8a7045c3490d8afabbe416eb5724`、verdict/result SHA已复核。仅复用此前本地加密路径，不是新candidate12 Player、remote/publisher或完整native支持声明。
- D/`b03-closure-final2.json`只读重验16份严格结果及8份Legacy功能/退出接受，788无重复/漏项，KnownExitIssueRuns=1、EditorExitDefectFixed=false、WholePlanCompleted=false。D/`protection-b03-final1.json`2553→2553、差异数组均空；Source实时PID5556/唯一YLFrameworkDev clean/非Play编译更新错误/默认Code。无提交/推送/权限或系统配置变化，本轮停在B03，不启动B04。

### B04 最终验收记录

- 新o3先以candidate12在固定2022.3.62f3c1/revision1623fc0bbb97、UGUI1.0/Json.NET3.2.1/modules最小依赖真实Create→裸Import正常exit0，之后才安装四产品/固定MiniExcel1.31.2、本机DOTween1.2.825及Common测试，声明UTF1.3.9/Input1.14.2/官方HybridCLR9.0.3 Git URL及commit6cadd24d101f4cbd00cd80190106f6621132d24d。未安装native reception harness或LocalIl2CppData，不触B08。
- 最小接线仅扩展现有Stage9 OptionalIntegration=HybridCLR、独立optional-hybridclr scope与TestsCoexistence编译见证；默认流程/原三集成不变。纯守卫先见6项缺入口RED，另11项因旧离线夹具遗漏PlatformBound字段而失败；补齐实际字段后77/77 GREEN，最终b04-optional-final1仍77/77。没有另建验收平台，o3的真实Unity编译/发现为实际验收。
- D/`o3-coexistence-coexist1.json`见证同一次重启后的11个必要产品/测试程序集真实存在，DOTWEEN/YL_DOMAIN_EXCEL/YL_HYBRIDCLR_9_0_3实际进入对应程序集defines，SDK9.0.3与lock commit精确一致；反射调用原Prepare从本引擎真实编译程序集生成测试输入。四Scope各自官方discover并smoke正常exit0，不将设宏回执当编译通过。
- 同一o3实际严格通过172叶：Excel Edit41、DomainExcel Edit15、DOTween Edit1/Play79、HybridCLR边界/版本/真实player-loop与async Edit11/Play25。每组非空、selection/XML精确相等、无skip/漏多/失败/TearDown错误且真实正常exit0；无2022退出豁免。初次外部矩阵脚本嵌套数组在测试启动前类型错误，纠正为平面mode数组并只读复用原正常Excel发现；未重跑或覆盖原件。
- 许可实证：固定官方MiniExcel1.31.2 NuGet归档SHA f11a54c1bd5b3fb3332dc5d4e40ade59a3539cb8de6d087a245575457d66379a，内部netstandard2.0 DLL SHA0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e，nuspec声明Apache-2.0且无随附NOTICE条目；归档只读、不加载/执行DLL。原Apache全文随包，新增明确为YL提供的MiniExcel归属文件保留nuspec作者/copyright；XF ExcelSystem1.0.7/725f94f2原MIT全文（Copyright (c) 2025 弦风课堂）原样随附。Excel Dependencies仅补许可路径，代码/依赖pin不变。
- DOTween仅自有Runtime adapter/asmdef/rsp与新增Dependencies.json、完整框架MIT/QFramework归属。后者记录官方ZIP/DLL SHA、版本、原readme/Modules要求以及bundled=false/redistributionAuthorized=false；不含DOTween DLL/Modules/Editor/Pro，也不借MIT替代Demigiant许可。官方HybridCLR SDK/dnlib/native/Tests/生成DLL均不进可选包，完整vendor再分发授权仍HOLD。
- Source身份变化在第一次保护检查被拒绝，未作写操作；只读确认当前11568为同工程/版本/唯一clean YLFrameworkDev、非Play/编译更新错误，用户question明确选择“确认当前Editor”。据此仅定向ImportAsset三份新许可/依赖元数据，让Unity生成新meta/GUID；未保存场景、Play、重启或关闭Editor。首次导出snippet有转义语法错误，编译阶段拒绝、未执行；修正后以显式路径和ExportPackageOptions.Default正式导出，不IncludeDependencies、不SaveAssets。
- 正式四包及逐path源SHA/meta/GUID审计：Excel14条、107369B、SHA4b9d2170523461969c16754740a3a87a8298be2674b8b6b2e5842396bb3815c0；DomainExcel4条、9145B、SHA0fc4afdb3f2a75614788740fad4ade1c720741fbbe5668cba0915ad62dec531e；DOTween wrapper5条、8210B、SHAf7b27abeb027ce768e2b29e53e2287a32d7017aa418ae565d32f9d94697b145e；HybridCLR含隔离Samples源码29条、82912B、SHA99edebbec44fa0930fd05e094ed026a10a3784b9240cec316830517aede72d4b。文件均为D/`YLFramework-<集成>-U63-b04-candidate1.unitypackage`，配套.export.json/.audit.json不可覆盖；仅Excel允许经过验证的MiniExcel DLL，所有包保留完整LICENSE.md。
- D/`b04-closure-final1.json`核172测试、同环境见证、全部包精确闭包；MiniExcel归档原件T/`b04-miniexcel-7a08eef4d03742908c2876197dc955e8/result.json`。这不是最终B10同字节全能力双版新鲜接收，也不声称native/Player验收；已测产品源码与导出源一致，后加内容仅依赖说明/许可。
- 最终默认candidate12 SHA仍2365eeb5047c482da3b947f1ad982463dfef732429e7a8ad79169255351e38d0；D/`protection-b04-final1.json`2553→2553、所有差异数组空；Source11568原场景clean/编译无错/默认Code，OS仅该主Editor存活。无Git提交/推送/权限或系统配置改变。本批安全功能/源码分发范围完成，不自动进入B05；DOTween含vendor发行仍HOLD。

### B05 最终验收记录

- 默认candidate12不变，Source11568仅只读状态守卫，未进入Play/测试或保存场景。真实DSP在全新6.3独立b8、最小UGUI2.0/Json.NET3.2.1/modules环境完成，不装UTF/Input/可选SDK，audio-only fixture仅使用原AudioAcceptanceEntry与Mixer数据，原运行断言/阈值/脚本GUID不改。
- 工具实际修正及RED保留：裸包未显式导出的父目录由Unity生成自己的folder meta，旧守卫误拒Plugins.meta；新增一个正向和一个无效metadata反向回归，先RED1/35，后36/36（含真实SDK/C#9语义）。仅放行从审计产品路径推导的真实父目录、有效folder/guid元数据，未知资产/目录/链接仍拒绝。Generate在NewScene后旧Config/Library对象失效，b5/b6形状RED与有效磁盘YAML保留；新场景后重载持久Library/Config解决。编译图将Runtime的合法UI引用纳入fixture闭包、默认产品含ResourceStartup.Sample；精确允许这两项已审计产品项，Editor/NUnit/TestRunner/Analyzer/Roslyn/其它fixture仍拒绝，不删产品或改运行断言。
- b8裸Create→Import、安装、Generate均真实exit0；非Development Windows64/Mono2x/Disabled/BuildOptions.None构建成功，Errors0、Warnings15，BuildGuid`2b5f90b2ceb447bdbed5a37570d522fe`，Player程序集/输入身份门禁通过，PlayerSettings与build设置按原值恢复。b5/b6失败、b7Generate成功及Build前置拒绝原件保留，不覆盖旧输出。输入安装/生成/构建回执明确RuntimePassed=false，不以构建代运行。
- 真正Player：`Runs/B05-b8-audio1-player.log`，参数-audio45-listen/-windowed/960x540/-force-d3d11，无batchmode/noaudio/nographics，D3D11实际启用。原RunAudio完整13段Listener PCM RMS：group0.0598931823、half0.0305070129、muted0、paused0、resumed0.061019254、faded-out0、faded-in0.0610159632、stopped0、mixer-full0.0610247927、mixer-half0.0305011378、crossfade0.0610036276、spatial-near0.0610038082、spatial-far0；原音量/暂停时间/租约/双声部/路由/近远衰减检查均走完，AUDIO45 AUDIO_PASS与QUIT_PASS各一次，正常OS exit0、无timeout/forced/observer/cleanup错误。GetOutputData不等于OS loopback或人工听感，两者未验证；没有改Windows音量/音频端点。
- 正式资产：自有k3 Editor只读probe（不执行原Source-host套件）对完整YLFramework、ColorSelector、UITips、UITipsOverlay、Canvas及固定Legacy/Modern Project共7份资产ForceReimport，逐份payload/meta字节及GUID前后相等并成功Load。核完整模板Canvas base、Tips Variant直接来源/layer4/无其它业务override/不阻断raycast，并LoadPrefabContents实际预览实例化且无missing script；D/`b05-formal-assets1.json`保留七GUID。两次新probe缺少命名空间的编译RED保留，修正仅加真实namespace，不修改产品资产。
- 实际Play消费：自有接收端单叶`b05-formal-consumer3`用正式Module启动、完整模板实例化、ColorSelector加载/初始色/无多余回调/Close解除owner，以及正式Tips.Show完成/ActiveCount归零，正常exit0。最初Prefab在EnterPlay前加载，跨DomainReload后失效的工具RED保留；改为进入Play后从同一路径加载持久Prefab，原业务断言不减。此为B05定向消费，不称完整UIAcceptance/真实OS输入矩阵通过。
- 作者工具当前实跑`b05-authors1`6/6、正常exit0：Equipment两个窗口/Undo，DataAsset改名/Undo，Localization创建/复制/Undo/保存重开/移除引用，Audio多对象列表Undo与Prefab override。有效B02结果只读复核复用：f54 BuildIntegration1、f55 CodeCompilation2、f56 CodeGeneration10、f58 ResourceEditor12（含动态真实旧字段Project/Atlas/Recipe/窗口强制重导入2及Undo/GUID）、f61 WorkbenchAtlas6、f66 UX2八叶，原selection/XML/hash/正常退出均匹配，不重复长跑。固定Legacy/Modern资产只读Load/Reimport与动态旧schema窗口消费分别记录，不冒称Source-host或固定旧资产曾运行整套窗口测试。
- D/`b05-closure-final3.json`只读独立校验Generate/Build/Player身份与真实终态、13段RMS及全部相对/零门限、原PASS/QUIT_PASS、七正式资产及8个消费者证据组。初次checker人工计数错误在接受前拒绝，改为原实际12/6/8叶，不改XML/断言或重跑。WholeCompatibilityPlanCompleted=false、HumanListeningVerified=false、SourceProjectTested=false。
- 最终pure工具守卫36/36及C#9真实引用语义0错误，三文件局部diff通过；D/`protection-b05-final1.json`2553→2553、差异数组全空。Source实时PID11568原唯一YLFrameworkDev loaded/clean、非Play/编译/更新/错误，OS仅该主Editor存活；无提交/推送/固定依赖或系统配置改变。B05在上述自动功能范围完成，不自动进入B06。

### B06：真实 Windows 输入（2026-10-29，本轮停止，未完成/阻断）

- 授权与检查点：用户12:57:22Z明确进入B06；本轮最晚14:27Z检查点，不自动进入B07、不启动子代理或扩写CI/native研究。
- 起始身份：分支`integration/xf-yl`、HEAD`dc31794e991ac7fc83b33ff9366a8031a75f8a1b`未变；Source实时只读为PID11568/2022.3.62f3c1、唯一原YLFrameworkDev loaded/clean、非Play/编译/更新/失败、默认Code。用户及前批工作区改动全部保留。
- 沿用边界：先复用E08的n2/n5/n6 candidate11薄生产构建，校验完整PlayerFiles/包SHA/BuildGuid后才能启动；不重建冻结Library/Cpp、不重绑candidate12。现有consumer只证明正式取色和关闭Button两手势，不能代替完整取消/控件/键盘/设备/多根矩阵；前台不满足时停止依赖部分，不绕Windows策略。
- 本批实际仅启动n2 Legacy Mono，前后两次`Operation Validate`均重新核完整PlayerFiles/包SHA/BuildGuid通过；BuildGuid=`c7cfafd99f56464c90a447fe5d04bcd1`，BuildReportSHA=`74d5cb2708849be5a823d96324bfef65b3b9f86f1f7a500696572639b803bfd3`。n5/n6没有启动、同步或重建，不新增其实际输入结论。
- 首次`b06-legacy-mono1`/PID13812等15.0108秒仍Ready=false、无OS事件，按原门禁终止自有Player；exit=-1/forced保留，不记运行通过。随后用户明确确认手动置前，仅点Player标题栏并松开按键，不代点业务控件。
- `D/Runs/n2-native-ExpectedLegacy-b06-legacy-mono-manual1`/PID38984/HWND920678：真实1920×1080、DPI168、D3D12；六次SendInput各Inserted=1、Foreground=true，正式StandaloneInputModule实际送达两组Down/Up/Click各一次，ColorChanges=1、ButtonClicks=1、颜色cyan→(0.5,1,0,1)、正式panel关闭及OwnedCleanupCompleted=true、Errors=[]；原`result.json`的BusinessPassed=true，`Player.log`含BUSINESS_PASS，真实OS exit0、无forced/timeout。但原外层`verdict.json`为Passed=false，Failure=`Owned Player exited before the required response.`，不覆盖、重签或外推整批通过。
- 驱动定向修正：最终`WaitForExit(25)`返回false与随后的存活断言之间，正常退出可被当成提前退出；所有输入完成后的等待改为只观察原持有的精确进程句柄和原hard deadline，随后仍严格要求ExitCode=0、完整IPC/业务/日志/产物身份。输入进行中的PID/start/exe/HWND/前台/遮挡/按键守卫完全保留，未改Player模板、业务阈值或产品代码。
- TDD：在现有`Test-Stage9NativeInputGuards.ps1`执行驱动真实AST最终等待段，以确定性的合成退出观察边界复现，不启动Unity/Player或调用Win32。`T/b06-exit-race-red1/results.json`为52/53，新正常退出叶按预期报提前退出；最小修正后`green1/results.json`为53/53，含最终等待hard deadline拒绝。不是实际修正后Player运行通过的证据。
- 修正后`b06-legacy-mono-fixed1`/PID38600仍在15.0195秒前台/按键准入超时，实际事件0、exit=-1/forced，原件保留。此后等待用户关于本批普通置前的答复；用户15:25Z明确批准后，只执行一次新`b06-legacy-mono-activate1`/PID7992。Windows明确拒绝普通SetForegroundWindow，首次move前即拒绝，实际事件0、exit=-1/forced、TimedOut=false、OwnedButtonMayRemainHeld=false；未注入Alt、AttachThreadInput、改ForegroundLock或触碰其它窗口。该答复不扩大为绕策略或继续后续批次的授权，拒绝后不再启动作业。
- 尚未完成：修正后驱动的真实完整运行；New/Both与必要Mono/IL2CPP/受影响2022组合；camera/分辨率变化、三键取消/旧Up不Click/新gesture恰一次、Scrollbar/InputField、ScrollRect Elastic/Clamped拖拽与惯性、关闭/销毁/池复用、OnSelect重入、键盘Move/Submit/Cancel、设备移除、共享actions、外部EventSystem及多根所有权。合成测试或metadata inspect不能代真实OS输入/热插拔；本批不签B06完成。
- 最终保护：`D/protection-b06-before1.json`及`protection-b06-final1.json`均2553→2553、Passed=true、三个差异数组为空；最终Source实时仍PID11568/原唯一干净场景/非Play/编译/更新/失败/默认Code。OS无自有输入Player或接收端主Editor残留，仅Source主Editor及其导入worker、Hub；candidate12 SHA仍`2365eeb5047c482da3b947f1ad982463dfef732429e7a8ad79169255351e38d0`。两个PS工具原meta/GUID不变、未跟踪文件以`git diff --no-index --check -- NUL <path>`检查无空白问题；没有提交、推送、改依赖或系统设置。
- 下次前置：先获得可合法持续保持前台且无并发物理输入的条件，再重新核Source/包/自有构建身份；保留所有本轮RED，仅新Run补验，不自动重试或进入B07。

### B06恢复检查点（2026-10-29 16:35Z，进行中）

- 用户15:35:41Z明确提供前台、停止物理输入并要求完成B06；本轮检查点17:05Z，只推进本批。Source重新实时核验PID11568/2022.3.62f3c1/原唯一干净场景/非Play/编译/更新/失败/默认Code；`protection-b06-resume-before1.json`已生成。最初普通置前仍被Windows拒绝，用户随后明确同意仅点Player标题栏；没有任何前台策略绕过。
- 修正后驱动真实闭合：`n2-native-ExpectedLegacy-b06-cursor-diagnostic1`、`n2-native-ExpectedLegacy-b06-legacy-il2cpp1`、`n5-native-ExpectedNew-b06-new-mono1`、`n5-native-ExpectedNew-b06-new-il2cpp1`、`n6-native-ExpectedBoth-b06-both-mono1`、`n6-native-ExpectedBoth-b06-both-il2cpp1`，均在`D/Runs`；六个原candidate11构建各实际6个OS事件、业务/清理/原日志门禁完整通过、正常exit0，无forced/timeout。它们仍是基本取色/关闭合同，不外推取消或当前包整体发行。
- 本轮原`b06-resume-manual1`在首个move后、down前被光标偏离门禁拒绝；仅有一个move，保守`OwnedButtonMayRemainHeld=true`保留，不补Up到未知窗口。后续诊断基本运行通过，不据此宣称偏移根因已完全定位。
- 新工具扩展仅在两份既有C#模板及两份既有PS工具：显式`Suite=Capture`和`CaptureProtocol=B06Capture-v1`禁止冻结basic-only构建冒充新能力；无Input System强类型、QueueStateEvent、BaseInput覆盖或ExecuteEvents。新接收端先裸candidate12接收再安装，既有n2/n5/n6的Library/Cpp/包/owner未变。
- Capture严格十三项：三键模态取消且旧Up不Click/新gesture恰一次、Scrollbar持续点击取消、InputField真实激活/EndEdit恰一次/恢复、Elastic/Clamped非零拖拽和速度/取消/禁止旧拖拽恢复/新拖拽恢复、关闭池复用、销毁重开、OnSelect内关闭复用、ScreenSpaceCamera与实际1280×720变化、多根局部模态及销毁隔离、外部EventSystem借用/配置与寿命不被接管。此集合不包含键盘/设备/共享actions，不是完整B06。
- `g1`先同字节candidate12裸Create/Import正常exit0，再正式Generate/Build；无UTF/Input包或测试树。Mono BuildGuid=`67a6402c30c24bb28d82bd58dbe354cb`；IL2CPP Low/Release/非Dev/None BuildGuid=`cebd7baafa954606a51f5c1e913568aa`、0error/14warnings，PlayerSettings精确恢复。
- `g1-native-ExpectedLegacy-b06-capture-stationary1`和`g1-native-ExpectedLegacy-b06-capture-legacy-il2cpp1`均十三项/79个实际OS事件、业务与自有清理通过、正常exit0，无forced/timeout；Mono结果细节含非零拖拽与Scrollbar值，未向生产代码硬编码结果。最初`g1-native-ExpectedLegacy-b06-capture-legacy1`在首次down前诊断出Expected=(1920,1099)/Actual=(1925,1163)，原RED保留。
- 人工交接准入新增一次有界“前台+全键松开+光标连续静止500ms”，任何移动/前台或按键丢失重新计静止区间，15秒总限额不变；不重发已注入事件、不放宽后续像素或前台门禁。纯状态序列TDD 55/58 RED→58/58 GREEN；Capture旧构建拒绝TDD 53/55 RED→55/55 GREEN，最新证据`T/b06-stationary-green1/results.json`。它是输入交接条件，不是以Sleep/概率重试制造业务通过。
- 当前g2仅完成新鲜candidate12裸Create/Import且正常exit0，尚未签New Capture；参数名误写`MaxUnitySeconds`在启动前被绑定拒绝，原tracked失败保留，实际使用既有`UnityTimeoutSeconds`，不改工具权限/配置。

### B06恢复轮收口（2026-10-29，完整B06仍未完成）

- 16:35Z后继续已启动的New Capture验证，17:05Z检查点附近结束最后自有IL2CPP Player后停止新增输入/Editor/构建；后续仅做证据、保护和本台账收口。没有进入B07、启动子代理或扩大到CI/native研究。
- `g2`已完成New配置、实际Generate/Mono构建；`b06-capture-new-mono1`前台准入失败，事件0。随后用户标题栏激活后的`b06-capture-new-manual1`实际通过前十二项，但external-system/down无实际回调，原result.BusinessPassed=false、父验收失败、ForcedExit=true/exit=-1/OwnedButtonMayRemainHeld=true均保留，不接受为十三项通过，不补发Up到其它窗口。
- 外部模块诊断依据为实际安装Input System 1.20.1源码：point为PassThrough，模块OnEnable关联actions；新模块替换前后目标同坐标的OS move不能被假定为有效位置变化。最小消费者修正仅在新载体里增加目标内24px的真实OS move再移回目标，另严格断言共享默认asset同一身份/存活、point和leftClick启用；不重新Enable actions、不写Input System私有状态、不改生产代码或删原Down断言。本轮结果支持该位置初始化假设，不把g2失败固化成框架产品缺陷。
- `g3`是另一新鲜candidate12接收端，先裸Create/Import正常exit0，再安装两份最新模板及既有Editor profile helper；只在本接收端固定声明Input1.20.1/UTF1.6.0，lock由Unity更新，不改Source依赖。没有测试/Acceptance运行时树或Input System强类型消费引用，实际Mono/IL2CPP构建仍执行生产程序集闭包门禁。
- `g3`New Mono BuildGuid=`aa609a9d8cb345a79dbe4d2e4edcebc3`，IL2CPP Low/Release/非Development/BuildOptions.None BuildGuid=`f66906181cd84d709efc2bb0ef5184d5`；两者0error/14warnings、PlayerSettings前后SHA完全一致，未宣称零warning。IL2CPP并非仅metadata：`g3-native-ExpectedNew-b06-capture-new-mono1`和`g3-native-ExpectedNew-b06-capture-new-il2cpp1`均十三项/80个实际OS事件、业务和自有清理通过、正常exit0，无forced/timeout；external-system细节严格SharedDefaultActionsAlive/BorrowedModuleUnchanged/ActualPositionTransition=true。
- 当前已闭合范围为六份旧candidate11基本结果，加g1 Legacy两份candidate12 Capture和g3 New两份candidate12 Capture；不同模板/包身份均按真实记录分别绑定，不混成同字节单次全量结果。最终只读`D/Check-B06Checkpoint.ps1 -Run final1`核十份原始verdict/result/exit、进程/BuildGuid/BuildReport/包/配置恢复/每事件Inserted=1/Foreground=true及全部既存Evidence SHA，发布不可覆盖`D/b06-checkpoint-final1.json`：BasicRuns=6、CaptureRuns=4、NativeEvents=354、CaseInstances=64，RecordedScopesPassed=true、B06Completed=false、WholeCompatibilityPlanCompleted=false。64仅重复组合的case实例数，不是64个唯一功能；原RED全部保留。
- 工具最终离线58/58：`T/b06-input-final1/results.json`。四份本轮改动工具为`Invoke-Stage9NativeInput.ps1`、`Test-Stage9NativeInputGuards.ps1`、`Stage9NativeInputProbe.cs.txt`、`Stage9NativeInputEditor.cs.txt`；C#模板已有g1/g3实际Unity编译/Mono/IL2CPP证据，不用PS守卫或静态检查替代。未改产品Runtime/Editor API、资源GUID或序列化合同，未手改meta。
- 最终`D/protection-b06-resume-final1.json`2553→2553、Passed=true、三个差异数组为空；Source实时PID11568/2022.3.62f3c1、唯一原YLFrameworkDev loaded/clean、非Play/编译/更新/失败、默认Code。OS无接收端主Editor/本批Player，仅Source主Editor/其导入worker及Hub；candidate12 SHA仍`2365eeb5047c482da3b947f1ad982463dfef732429e7a8ad79169255351e38d0`。未提交/推送，未改变系统策略或其它窗口。
- 剩余明确范围：Both Capture双后端；实际OS键盘Move/Submit/Cancel及文本；设备移除后旧gesture失效/新真实输入；按住期间相机或分辨率变化取消（现camera-resize只验证实际映射切换后的点击，不代此项）；受影响2022 Player交叉。默认共享actions在g3该具体借用链已实际证明，不能外推全部自定义/多owner配置；真实物理热插拔和触摸仍未覆盖。没有可靠剩余工时估计，不按run数量推算工期；本轮不自动启动后续作业，也不签完整B06。

### B06持续执行（2026-10-29 17:19Z起）

- 最新执行边界：用户要求把同一B06完成，并明确指出不应在尚未完成时因检查点停工；持续目标已启动。60–90分钟用于增量记录，不覆盖此次同批完成授权；只有实际权限、环境或契约阻断才暂停，不反复请求同一范围授权、不进入B07。
- 已重新只读核验Source PID11568/2022.3.62f3c1、唯一原场景clean、非Play/编译/更新/失败、默认Code；无接收端/Player冲突。原十份闭合结果和全部RED保留，不改写或重复跑未受影响证据。
- 下一步仅补剩余消费者/安全OS驱动，先实际RED定位再作最小必要修正；设备项只操作自有Player里的已注册原生鼠标，不动OS设备配置，也不把API级移除/恢复称为物理热插拔。

### B06持续目标的实际输入阻断（2026-10-29 18:00Z证据）

- 已实现独立`Suite=Remaining`/`RemainingProtocol=B06Remaining-v1`，旧basic/capture-only构建仍拒绝。消费者按真实OS事件验证Move/Submit/Cancel、固定数字文本、移除/重加同一已注册原生Mouse后的旧Up不Click/新点击、按住期间Camera/实际分辨率变化后旧手势失效。Legacy预期四项，New/Both额外设备项共五项；不等于已经验证通过。
- Win32键盘只白名单Return/Escape/四向箭头/数字1–3，禁止修饰键、Windows键和系统快捷键。Interop为真实x64 40B INPUT布局，键事件逐个核验原PID/HWND/exe/前台/遮挡/物理状态。没有强类型Input System引用、QueueStateEvent、BaseInput覆盖或ExecuteEvents输入替代。
- 新鲜`g4`先candidate12裸Create/Import通过，再声明固定Input1.20.1/UTF1.6.0、配置Both、实际Generate/Mono构建，均正常exit0；首版Remaining消费者已实际Unity编译。`b06-remaining-both-red1`只在人工交接准入失败，输入0；之后`b06-remaining-both-activate1`/PID32324/BuildGuid=`1f9d27390ed64c33b775f066a1ec9878`处于合法前台，原始事件记录表明没有实际发起SetForegroundWindow调用，只消费了一个move和一个Right/key-down。
- 后一run在key-up前触发`Unexpected key/button state; no keyboard input sent`，没有抬键事件，也没有任何键盘case通过或完整result。原ForcedExit=true/exit=-1/TimedOut=false/OwnedButtonMayRemainHeld=true全部保留。首版仅凭选择到Second等待key-down响应，不能据此声称真正的Move已消费；新消费者已补实际IMove计数前提，但尚未在新载体运行。
- 已确认的收尾缺陷是旧父驱动错误路径直接终止Player，没有在原窗口仍合法时处理自身已按下的测试键。失败当时究竟是哪类额外状态触发原门禁，原日志没有记录，仍未知；不能把虚拟键发送、用户操作或Input System当成已证根因。
- 只读查询在17:58Z及`D/b06-held-input-block1.json`18:00:55Z均确认RightArrowHeld=true、全键HeldCount=1；原PID32324已不存在，当前没有原窗口可合法接收Up。只读捕获器`D/Check-B06HeldInput.ps1`没有发送输入；没有向Source、其它窗口或新的Player补Up，也未改系统键盘/设备配置。此状态需要人工释放测试键，不会随构建、等待计时或重复发起Player自行安全恢复，不能用定时重试规避门禁。
- 已补后续预防候选：键盘ack独立3秒安全上限，保留原应用/全局deadline；仅当最后一个真实事件确为本PID/本HWND已插入的白名单key-down、原进程仍活着时，才尝试一次严格原窗口key-up收尾。原生函数仍二次检查前台/遮挡/全键状态，拒绝则保留held标志；清理事件单独记录、绝不作为业务通过，死进程/换窗口/换PID/已keyup/非白名单一律拒绝。不能据此追认旧run已清理。
- 发送端同时按Microsoft一手KEYBDINPUT/MapVirtualKeyW合同使用映射的物理scan code、KEYEVENTF_SCANCODE及实际扩展标志，并增加仅分类的状态诊断；这只是待真实复验的发送候选，不宣称已经修复原门禁失败。资料：https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput 与 https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapvirtualkeyw 。
- TDD证据：剩余协议/键白名单58/61 RED→61/61 GREEN；原窗口收尾策略61/67 RED→全部策略GREEN；最终`T/b06-owned-key-cleanup-green3/results.json`68/68。纯Validate无绑定断言完整保留，随后单独显式编译实际Interop C#并检查40B/8/10/12/24偏移，未调用Win32方法；不是键盘或设备运行通过。`green1`是语义编译与无绑定检查顺序冲突、`green2`是PowerShell选错Marshal.SizeOf(object)重载，失败原件均保留；最终显式Type重载修正，没有删原断言。
- 此次未修改产品Runtime、未导出新包、未同步或重建旧g1/g2/g3及失败g4的Library/Cpp，candidate12身份不变。`D/protection-b06-goal-block1.json`2553→2553、三个差异数组为空；Source仍PID11568/2022.3.62f3c1/唯一原场景clean/非Play、编译、更新或失败/默认Code；无本批Player或接收端主Editor残留。
- 恢复前提仅是输入环境恢复全键松开，尤其人工将右方向键按下后松开一次；不是再次申请B06权限。随后先只读重核门禁，在新Run补实际键盘/设备/几何RED→GREEN、Both双后端和2022交叉。此前354个已接受事件与所有原RED均保持原身份，完整B06仍不签通过。
- 最终决策前的独立只读快照`D/b06-held-input-block2.json`在18:08:29Z仍为RightArrowHeld=true/HeldCount=1、原窗口已退出、FreshInjectionAllowed=false；本次暂停仅因无法合法释放已留存输入状态，不是检查点期限。所有本轮改动文件空白检查通过，candidate12原SHA不变，当前不再启动输入进程。

### B06按键释放后继续（2026-10-29 18:17Z起）

- 用户明确已操作右方向键，`D/b06-held-input-resolvecheck3.json`18:17:07Z只读RightArrowHeld=false/HeldCount=0/FreshInjectionAllowed=true，同一Goal已恢复；未再请求B06授权。Source重新实时核验PID11568/原唯一干净场景/默认Code/非Play或编译更新失败，无接收端冲突。
- 复用g4精确旧构建作新Run诊断，不同步其源码/Library/Cpp：`b06-remaining-scan1`首move前普通置前被拒，事件0；`b06-remaining-scan-manual1`经过合法交接后发move与scan-code key-down，但IMove/选择响应未在3秒内到达，原失败保留，不延长超时。此次父驱动在仍存活、同一前台PID39880/HWND9308884内严格发出一次cleanup-only Right/key-up，Inserted=1；没有跨窗口补键。`owned-key-cleanup.json`与`b06-held-input-scancleanup1.json`证明故障收尾及全键清零，原业务仍失败，不追认通过。
- 新定位证据：本机只读MapVirtualKeyW(key,4)对四向箭头均只返回低字节（Right=0x004D），不包含E0；上述实际发送flags=8/cleanup=10缺导航扩展标志。按Microsoft enhanced-key合同，白名单导航箭头显式保留E0，普通Enter/Escape/数字保持非扩展；`T/b06-extended-arrow-red1`68/69 RED→`green1`69/69 GREEN，测试调用实际Interop里的纯flag函数，未发送输入。参考：https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input#extended-key-flag 。
- `b06-remaining-e0-manual1`人工交接未满足、输入0；不是E0行为的运行结果。不存在反复盲发键事件或残留键。
- 进一步按当前源码确认正式EventSystem.prefab的m_sendNavigationEvents=1；包代码在背景按下可清空选择。夹具原先在标题栏交接前SetSelected，不能假定该选择跨焦点交接存活；新消费者在实际首move/交接完成后才设置并断言初始First选择，同时要求真实IMove计数。增加单次只读key-observed快照；Input System键状态/导航action详细反射诊断仅Mono编译，IL2CPP不因诊断额外保留相关metadata。业务断言仍严格，尚待新载体实测。

### B06真实键盘/设备通过与几何RED（2026-10-29 18:40Z起）

- `g5`最新诊断模板初次编译暴露CS0136（诊断局部action与CaptureInput参数同名），原日志保留；只改源模板局部名为moveAction，未修补受管输出，另建g6。`D/Prepare-B06Receiver.ps1`仅薄组合既有Create→裸Import→声明固定receiver依赖→安装两个模板/Profile→模式→Generate→Mono入口，失败即停，不引入第二套验收/CI或改Source配置。
- `g6`candidate12/Both Mono BuildGuid=`5db2330529f7433fa34bd2ceed0da928`、`b06-remaining-posthandoff1`实际27个业务阶段，前三项已严格通过：Moves=1/Submits=1/Cancels=1，文本精确123；原生Mouse设备ID2/API移除重加同一设备、旧Up不Click及新真实点击通过，不称物理热插拔。key-observed-2显示NativeKeyboard=true/ControlPressed=true/MoveActionEnabled=true/MoveX=1/MoveY=0/SelectedObject=Second。
- 同一run随后在camera-held-cancel严格失败：实际按住期间改变Canvas渲染相机，回到目标再Up仍触发旧Click。BusinessPassed=false、OwnedCleanupCompleted=true、正常退出1，原RED不接受为完整Remaining通过；没有再留下按键。不能用前三项局部通过替代全部五项。
- 根因已由源码与实际RED闭合：PanelInputCapture原IsValid只看会话/阻挡/epoch，未记录坐标映射身份。只在已有Capture快照加Root Canvas/RenderMode、事件Camera、viewport/view/projection及Screen尺寸，IsValid改为几何必须仍匹配；复用原精确CancelPointer，不重置整模块、不改业务enabled/interactable，不为无捕获控件每帧采集相机矩阵。无公共API或序列化字段变化。
- Source实际Unity2022已加载新HasSameGeometry方法、无编译失败、原唯一场景clean；定向格式检查Files=1/SDK9.0.312通过，`protection-b06-geometry-source1.json`通过。仅用正式ExportUnityPackage.Export导出新`D/YLFramework-U63-candidate13.unitypackage`：4554569B，SHA=`e9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8`。AuditPackage通过1038项；`b06-geometry-package-diff1.json`按路径结构化核对相对candidate12只变PanelInputCapture.cs payload，全部路径/GUID/meta相同，该脚本GUID仍048af275a075ead4985e66c8c4a1b562。首个snippet导出调用因转义/using作用域编译失败、未执行；随后完整class/forward-slash模式成功，没有SaveAssets或覆盖旧包。
- 新g7先candidate13裸接收、配置Both、Generate及Mono构建均正常；`b06-geometry-green1`未满足人工交接、输入0，`b06-geometry-activate1`普通请求被Windows拒绝、输入0，不是修复运行GREEN。只读桌面检查是Default、前台Explorer，不是已证锁屏；不改系统ForegroundLock或通过Alt/AttachThreadInput绕过。
- 为减少调用方差异，仅把已授权的同一个普通SetForegroundWindow改为可由精确自有Player发起一次；新标记PlayerActivationProtocol=B06PlayerActivation-v1下父驱动绝不补第二次，未授权不调用、未知标记拒绝。`b06-one-activation-red1`69/73 RED→green1 73/73。新g8实际编译/构建后`b06-player-activation1`/PID5644/HWND9308612的activation-request.json确认调用一次、RequestReturned=false/Accepted=false、前台仍Explorer65880；立即正常退出1、清理完成、InputSteps=0，不追加请求或输入。
- 原有Application.isFocused可在Win32前台仍属Explorer时为true，父门禁已正确拒绝。新模板仅收紧Basic/CaptureInput的发布条件：实际foreground HWND必须属于本PID并与已绑定HWND相同；不满足则不发布“输入就绪”、不发送事件，仍受原全局deadline限制。父15秒静止交接参数及500ms条件不变；不是放宽像素或前台门禁，也不是重复激活请求。待新载体实测，完整B06仍未签字。

### B06最终验收记录（2026-10-29，批准适用范围完成）

- 最终当前包：candidate13，4554569B，SHA=`e9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8`。相对candidate12只改变PanelInputCapture.cs源码payload，1038条路径/GUID/meta全同；产品修复已在Source Unity2022编译加载并在两引擎实际Player验证。旧11/12及全部失败现场保留，不回填源码/重绑owner。
- 产品RED→GREEN：g6 candidate12实际按住切换相机后旧Up仍Click；最小修复记录Root Canvas/渲染模式、事件Camera/view/projection/viewport及屏幕尺寸，在原捕获有效性检查中识别映射变化并复用原精确取消。没有公共API/序列化迁移、业务enabled/interactable覆写、全模块重置或强类型Input System保活引用；只在已有按压捕获上读几何，不为无捕获控件每帧采矩阵。
- 当前包6.3矩阵：Legacy/u1、New/u2、Both/g9，均为Mono2x/Disabled与IL2CPP/Low/C++Release、非Development/BuildOptions.None；每后端Capture13项及Remaining（Legacy4项，New/Both5项）均真实OS消费、业务/生命周期/自有清理、原日志门禁通过，正常exit0，无forced/timeout。所有构建0error且设置前后SHA相同，原warning保留，不称零warning。
- 键盘Move/Submit/Cancel各恰一次、实际文本123；Input System路径实际移除/重加原生Mouse同一ID后旧Up不Click、新OS点击恢复；实际按住期间改变Canvas相机映射和窗口分辨率，旧手势不能Click、同一panel新gesture恰一次恢复。Legacy无Input SDK，设备移除不是适用项，不用Ignore或零例冒充通过。
- 2022受影响交叉：新u3同字节candidate13先裸Create/Import，再固定UGUI1.0.0/Input1.14.2/UTF1.3.9，Both两后端都完成Capture13+Remaining5；Editor/构建/Player均实际正常exit0，未使用2022退出豁免。这里是该私有几何修复与适配器的Both双后端交叉，不升级为2022三模式全能力发行接收。
- 当前矩阵精确选中16槽位：6.3三模式×双后端×两套件12槽，加2022 Both×双后端×两套件4槽。各载体先裸接收后加工具/固定SDK，PlayerFiles/ScriptingAssemblies拒绝Editor/Tests/NUnit/Analyzer/Roslyn运行时泄漏；没有借用Source Play/NUnit、虚拟设备QueueStateEvent或ExecuteEvents代真实OS输入。
- g9 Both IL2CPP首个Capture在three-left旧Click断言失败，原BusinessPassed=false/正常exit1完整保留；用户明确要求新Run重试后同BuildGuid/同字节十三项通过。2026-10-29 20:38:41Z用户补充确认“那一次是我手动点错界面的按钮了”，该次归因为外部人工业务按钮误触，不再列为未定位框架缺陷。u3 IL2CPP首个Capture因抬起前实际光标偏离1900,1079→1920,1080被严格拒绝并forced/-1，未补Up；全键只读归零后新Run完整通过，原件不覆盖，不放宽1px或前台门禁。用户此次确认不扩大解释为其它失败的归因。
- 为减少首次标题栏交接影响业务计数的歧义，仅在工具模板增加中性位置首move，业务目标在交接完成后才创建；首Down前计数必须为零、初始Down恰1、旧Click为0、新总Down恰2/FreshClick恰1，不重置计数制造通过。全新u4 Both IL2CPP实际Capture13项通过、81个OS事件，三键明细全部满足上述计数；不据此追认旧RED原因。该补充不改变产品包，也不要求重跑无关范围。
- 最终只读闭包：`D/Check-B06Closure.ps1 -Run final1`发布不可覆盖`D/b06-closure-final1.json`，16矩阵位+1隔离回归=17份当前包结果、155个case实例、1003个实际OS事件、9份不同构建。数字是重复组合的实例数，不是155个唯一功能；每份原verdict/result/exit、进程/BuildGuid/实际包、每事件Inserted=1/Foreground、按下抬起配对、键白名单/E0、精确case集合、所有既存Evidence SHA及当前PlayerFiles均复核通过。
- 六份candidate11 basic原证据只按其原包SHA/BuildGuid/hash复用，仍单列ReusedBasicHistoricalOnly；不算进上述current candidate13的17份/1003事件，不冒称同字节单次全量测试。此前B06 checkpoint及所有RED保持其当时事实。
- 工具最终`T/b06-final-guards1/results.json`73/73，含CLI拒绝、原进程/窗口抬键策略、手工交接静止状态、原生Interop语义/40B键盘布局与E0策略、总计一个普通请求方；纯Validate无绑定断言不变，之后的显式C#编译不调用Win32。Actual Unity C#编译、构建与Runtime证据分别由真实g9/u1/u2/u3/u4给出，互不替代。
- `D/source-b06-final1.json`由Source主线程实时只读采集并仅写外部证据：PID11568/2022.3.62f3c1/原唯一YLFrameworkDev loaded且clean，非Play/编译/更新/失败，默认Code，GeometryFixLoaded=true，源码SHA与候选13精确一致。SourceProjectTested=false/SourceSceneSaved=false；未保存、替换、关闭或重启Source，也未改变其版本、manifest/lock、ProjectSettings、AGENTS或IDE。
- 最终`D/protection-b06-final2.json`2553→2553、Passed=true、三个差异数组为空；`b06-held-input-final2.json`HeldCount=0/RightArrowHeld=false。OS无本批Player/接收端主Editor，仅Source/其import worker及Hub；无自有输入作业残留，无提交、推送、分支/worktree/代理会话、系统前台/音量/信任或设备配置变化。
- 结论：B06FunctionalPassed=true/B06Completed=true，按上述实际Windows与API级设备范围完成。物理热插拔、触摸、任意自定义/多owner共享actions、Source-host、2022全三模式及完整分支Review不计已验；默认共享actions只按已通过外部EventSystem链证明。WholeCompatibilityPlanCompleted=false/FinalReleaseAccepted=false/B07Started=false；本轮停止在B06，不自动进入B07-B10。
- 20:38Z用户归因补充单独追加为`D/b06-user-input-clarification1.json`，只更新上述g9误触分类；`b06-closure-final1.json`及其Residual字段保留生成当时的历史原文，不重写既有验收回执。B06完成结论、包SHA及所有原始测试事实不变；未改产品代码或重跑技术验收。

### B07开始（2026-10-29 20:53Z，历史起点）

- 用户明确要求实施B07，涉及信任/聚焦/窗口恢复等人工操作即时说明，不自行重复尝试浪费时间；只推进本批，不进入B08/native修复，不创建子代理/会话/worktree/分支或提交推送。60–90分钟仅作进度记录，不作为停工期限。
- 初始Source实时核验PID11568/2022.3.62f3c1/原唯一YLFrameworkDev loaded且clean、非Play/编译/更新/失败、默认Code；无Rider/VS或接收端主Editor冲突。`D/protection-b07-before1.json`通过；原所有用户/前批修改保留。
- w6五份受控输入及其meta仍精确baseline，18份旧协议原件可读，Analyzer SHA仍c0d20af752831f62a46a51794210e05e71a21410156655ca40ac6944fb831f79，与candidate13内同字节。沿用原Project/包身份和Rider generator快照，不将w6重绑candidate13，不重跑有效VS/Analyzer181测试。
- 新Rider会话使用既有Start-OwnedRider及全新Run，重新核PID/start/exe/solution/端口/HWND，不使用旧42676活性假设。不代用户授予信任、不强制置前或还原用户窗口；默认IDE不变。后续仅对owned输入做受控修改并精确恢复；原失败/证据不覆盖。

### B07最终验收记录（2026-10-29，批准适用范围完成）

- 本批没有产品兼容修复：Analyzer DLL/源码/规则产品API、Runtime、当前candidate13包与meta均未改。改动仅为既有Start-OwnedRider的精确已批准Error策略重启门禁、既有Test-OwnedRiderEvidenceGuards的13项回归，以及本唯一计划。Source没有Play/NUnit/保存场景/重启；未启动接收Unity或Player、未重建Analyzer、未改默认IDE/全局策略/插件配置、未创建代理/worktree/分支或提交推送。
- 初始五输入及meta/GUID、Rider generator的sln/csproj、固定Analyzer同SHA基线有效，复用原w6/candidate10身份，不把w6领养为candidate13。当前包candidate13内同一Analyzer SHA=`c0d20af752831f62a46a51794210e05e71a21410156655ca40ac6944fb831f79`，SDK181、双版Unity既有诊断、VS序列和旧Rider13协议组仅按有效原件复用，未重复运行。
- 两个新自有Rider生命周期：`D/Analyzer-w6/Rider-b07-1` PID34424/start20:54:42.4868344Z→21:17:58Z正常exit0；`Rider-b07-2` PID40056/start21:18:20.5517781Z→21:29:59Z正常exit0。均2026.2.3.1/build262.10968.170，原始进程句柄保存、拒领养、原exe/solution/owner精确绑定；重启前旧PID实际消失，没有强杀、超时或未验证Quit。
- 人工门禁处理：首次原生上下文是Misc Files，用户切Consumer后才取证；一次截图因Iconic=true/276×45被拒绝，用户恢复后仅重取一次。悬浮提示不稳定包含于PrintWindow，改为用户打开停靠Problems列表，不采桌面、不自动注入输入/置前；打开默认Probe的快捷键未切文件时保留原截图，用户直接打开正确文件后再采。Capture脚本没有CloseWindow参数的尝试在绑定阶段被拒，未执行关闭；随后用户正常退出。均非产品/诊断失败，没有盲目重复相同操作或代授信任。
- 当前会话MCP配置只读为enabled，但未取得本PID/既定64482的实时监听所有权；没有向未知端口发送调用，没有改设置，NewMcpRequests=0。只复用旧严格协议13组；`Test-OwnedRiderEvidence.ps1`原ProtocolSubsetPassed/FullRiderAcceptance=false和旧MCP pragma仍报Error的非验收观测均不改写，新原生证据另行闭合。
- 原生序列：Consumer局部YL0008 Error策略只通过原可逆helper重放，YL0009/10保持Warning；早期未刷新画面仍Warning保留为中间观察。`custom-unpragma-native.png`实际Problems显示红色“不支持的成员注入声明”/第8行，Panel/Audio两黄色控制仍在；`custom-error-pragma-native.png`明确显示disable/restore YL0008，红Error消失，两个控制仍在。首轮7→6→7反转也保留；不以“空列表”或总代码问题数（含IDE hints/Sonar）当框架零错误。
- 实际重启持久性：源码先回原基线，仅同一已批准Error ruleset保持，其owner/包/引擎/Before/After/meta哈希全匹配后才允许新PID启动。`restart-custom-error-native.png`新PID仍为Consumer Inject Error/两控制Warning，既不是同进程重分析，也不是只核磁盘/生成csproj。无pragma缓冲或源代码修改跨重启残留。
- 作用域与恢复：恢复Consumer Warning后，仅重放Assembly-CSharp的YL0008 Error；`default-error-native.png`名字虽含default，实际画面仍Consumer且显示Warning，作为隔离证据，不误记默认程序集通过。`default-error-context-native.png`才是IdeDefaultProbe/Assembly-CSharp，Inject红Error且控制Warning；最后`final-warning-restored-native.png`显示该三族均Warning。五份输入全部回owner原SHA，meta/GUID、Default.ruleset原本不存在、sln/csproj均不变，默认IDE始终Code。
- 最小工具TDD：重启原脚本只允许baseline，无法带确切Error策略验证持久性。新增Get-OwnedRiderLaunchHashes及可选PolicyRun，只接受同owner/Project/Unity/包、YL0008 Error、两个明确局部ruleset之一、Before/meta基线和合法After SHA；其余四输入/meta仍必须原字节。先84/97 RED（13新叶缺能力），后`Rider-rider2/b07-launch-policy-green1.json`97/97 GREEN、InputEvidenceUnchanged=true；实际第二次启动另有运行证据。没有扩大为任意dirty启动，也未弱化原84协议拒绝项。
- Player审计复用B06当前candidate13的9份不同BuildArtifact，覆盖2022/6.3、Mono/IL2CPP及6.3三模式。逐份重新Validate完整PlayerFiles/BuildReport/包SHA与真实ScriptingAssemblies，并检查落盘DLL及程序集名，Analyzer/Microsoft.CodeAnalysis/Roslyn/Workspaces/UnityEditor/测试宿主禁项均空；不是新Player运行、没有因IDE验证再构建。源码/正式Analyzer未变，所以不重新运行181测试或旧2022技术门禁。
- 最终只读闭包`D/Check-B07Closure.ps1 -Run final2`发布不可覆盖`D/b07-closure-final2.json`：NativeUiVerified/NativePragmaVerified/RestartVerified/FiveInputsRestored/B07Completed=true，6项关键原生观察+两个生命周期、旧13协议组、最终97守卫、两份工具最终SHA和9份Player制品均精确绑定。图像语义为本会话助手实际逐张阅读，脚本只核PID/时间/文件哈希和生存周期，不伪装OCR/像素自动判定。首版helper dot-source覆盖Run导致生成`b07-closure-.json`，原件保留；修正保存输入Run后发布final1，最终增加工具指纹/最终回归引用再只读发布final2，没有改测试或原始截图。最后97/97回执为`Rider-rider2/b07-final-guards1.json`；一次嵌套Command过滤回显因引号层在执行前解析失败，未创建回执，随后直接File入口成功，不重复技术/IDE动作。
- 最终`D/source-b07-final1.json`实时Source主线程只读证明PID11568/2022.3.62f3c1/原唯一YLFrameworkDev loaded且clean、非Play/编译/更新/失败/默认Code；`protection-b07-final1.json`2553→2553、三个差异数组空。SourceProjectTested=false，未将状态检查当Source测试。候选13 SHA仍`e9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8`。
- 结论：B07按批准补证范围完成，当前没有自有IDE/接收端/Player作业或需用户继续操作的窗口。没有宣称第三方插件/IDE全解决方案无诊断、旧MCP pragma已修、全分支独立Review或最终发行通过。WholeCompatibilityPlanCompleted=false/FinalReleaseAccepted=false/B08Started=false；停止在本批，不自动进入B08-B10。

### B08第一检查点：可维护native补丁方案（2026-10-29，尚未实施）

- 授权分层：用户21:39:37Z要求进入B08，按现有B08授权门先完成本方案；不把普通“进入批次”解释成修改native维护源的额外批准。本检查点只读检查、内存计算候选SHA与更新本计划，未创建fork/patch/native源码副本、未安装或构建native、未运行崩溃Player。
- 保护和RED复核：Source实时PID11568/2022.3.62f3c1、原唯一YLFrameworkDev clean、非Play/编译/更新/失败、默认Code；`protection-b08-plan-before1.json`通过。冻结GameAssembly DLL/PDB/dump/player.log分别仍精确匹配ReleaseCrashFreeze/receipt.json所列SHA，原exit=-1073741819/0xC0000005、无result JSON、NativeRunPassed=false。不重新读取敏感dump内存、上传或展开额外native研究。
- 精确来源：官方SDK9.0.3 Git commit=`6cadd24d101f4cbd00cd80190106f6621132d24d`，h17锁文件已核；官方Data~/hybridclr_version.json在6000.3.x选两库tag `v6000.3.x-9.0.1`。现存安装源`h17/Library/HybridCLR/il2cpp_plus_repo`远程`https://gitee.com/focus-creative-games/il2cpp_plus`、HEAD=`6eb38b29c3a7448b3ac6c4dee450de375b5d158d`；`hybridclr_repo`远程`https://gitee.com/focus-creative-games/hybridclr`、HEAD=`bf01f8eed56a077781c3a7b25f418603c7645588`。不查latest或换tag取巧。
- 来源状态：il2cpp_plus已跟踪文件无本地改动；唯一untracked `libil2cpp/hybridclr/`正是官方InstallerController在224行合并配套库的产物，不把此复制目录误当新上游提交。安装后的LocalIl2CppData本身没有Git，不能只凭它宣布可维护。拟从上述固定Git对象在新外部目录重建干净源树，再装配同版本hybridclr，不复制或修改任何旧LocalIl2CppData/Player/Cpp/基线。
- 许可：il2cpp_plus根LICENSE为MIT，Copyright(c)2022 walon；hybridclr根LICENSE为MIT，Copyright(c)2023 Code Philosophy Technology Ltd.；SDK根LICENSE为MIT，Copyright(c)2025同公司，Installer文件另有2026完整MIT头。均保留，不批量替换版权。推荐仓库只保存最小patch与精确溯源清单；完整native源树留在独立交付目录，不默认纳入YL unitypackage或宣称所有Unity原生工具链内容均可再分发。
- 推荐维护方式：批准后在D下新建本批专有、拒绝链接/占用的`B08Native/<run>/`，由固定Git对象得到只读upstream与单独patched树；仓库仅新增明确的patch及可复验来源记录，并最小扩展现有接收工具以传入绑定的native-source receipt。无远程fork/PR/push、无新Git分支/提交、无整仓native vendor化。每次核上游commit/文件preimage/patch/修改后文件SHA及所有未改文件一致，拒绝重复套patch或不匹配来源。
- 唯一候选产品改动位于`il2cpp_plus/libil2cpp/vm/GlobalMetadata.cpp:1352`：将`GetParameterDefinitionFromIndex(klass->image, methodDefinition.parameterStart + paramIndex)`改为`GetParameterDefinitionFromIndex(methodDefinition, methodDefinition.parameterStart + paramIndex)`。直接复用本文件206–218行已有归属重载，让参数表由真正method definition而非运行时合成数组class选择；不强制所有请求走AOT、不改其它metadata字段或Core/Json.NET业务。
- 精确字节：已核上游工作副本与冻结安装件GlobalMetadata.cpp同SHA=`6c2902bfcbdbaf253486d5af13c0e2b1e372af5c5a934a74c1a172fe8f2b823f`。只从固定Git blob在内存做一次替换，保留已核`git ls-files --eol`的i/lf、w/crlf工作副本CRLF身份；预期修改后SHA=`069213cf9fb4c5a07033a8d0bba25d53b4b986b6ba3eba98185e8c1e444bf4a1`，FilesWritten=0/NativeExecuted=false。首次按LF重建因不匹配preimage被拒，未伪造SHA，核换行后才给出该预期值。它是候选字节承诺，不是已实施/已编译/已修复事实；不同规范化形式必须单独记录，不能混用该SHA。
- 官方安装接点：精确SDK的`InstallerController.InstallFromLocal(string)`公开入口228–231行调用原RunInitLocalIl2CppData，复制该Editor匹配工具链、仅替换本project的libil2cpp并记录原version；不需要改SDK源码。仅在全新receiver使用，因其会重建LocalUnityDataDir并清该新工程Il2cppBuildCache，绝不对h17/h15或任何既有冻结接收端调用。`useGlobalIl2cpp=false`，不写Editor安装目录/系统环境/官方PackageCache。
- 自定义native身份必须显式：官方安装器的InstalledLibil2cppVersion仍会写PackageVersion9.0.3，它不足以证明未修改官方native。新增受控溯源receipt须绑定SDK commit、两native提交、patch与source SHA、receiver owner、实际安装树与最终Player/Baseline指纹，明确variant（例如B08参数归属patch）；后续支持声明不能把该variant伪称原版官方9.0.3已自然通过。具体字段接线只扩已有入口，不另建验收框架。
- 最窄RED→GREEN：保留h17为原业务RED；若需对照，只新建未修复native的新载体，不重跑/重建h17。用热更类型数组枚举DeclaredOnly/Instance/Public/NonPublic的合成IList.RemoveAt，GetParameters必须恰一Int32、name=index，参数token/归属oracle取本run最终AOT而非硬编码h17 token；另测AOT元素数组、泛型数组合成方法、普通Interpreter方法及原空handle伪方法控制，证明不是错误的全AOT回退。旧版偶然不AV不能算修复，严格参数与归属断言仍需闭合；必要诊断构建单独标记，不代最终普通Release。
- 原业务与负向：最窄门禁通过后新普通Windows64/IL2CPP/Low/C++Release、非Development/BuildOptions.None、空额外define，走未改JSON→Model[]→AddModel→Module.Running、实际V1与同Player V2更新/重启。仍保留Support依赖/async/Prefab、Busy/同进程替换拒绝、BadPayload/BadSignature在native前拒绝、BadMetadata部分加载后RequiresRestart。签名/TLS凭据按新owner隔离，不跨h17借私钥、不安装系统信任。仅“没崩”或负向通过不构成B08完成。
- 新产品输入：优先当前candidate13及B04安全HybridCLR可选包，先核与原native相关YL payload差异；全部新owner/裸导入/正式Prepare/最终AOT和Player指纹独立绑定，不复用旧基座。2022的原native tag不同，不能直接将6.3 patch套到2022；只按实际受影响范围验证或明确不适用，不借2022历史通过替代6.3普通Release。2022全能力及最终支持声明仍在后续批准范围内。
- 请求的实施授权仅覆盖：创建上述外部维护源副本与仓库最小patch/溯源、现有接收工具的最小显式local安装接线、全新载体native构建及上述回归；不覆盖依赖升级、修改官方缓存/Editor、远程写入、提交、破坏性API或B09/B10。批准后自主执行普通细节，检查点只报告进度；如出现重大候选冲突、须扩大源码范围或不同许可证/版本要求，再明确报告而非静默扩域。
- 当前结论：B08PlanCheckpointCompleted=true，NativePatchImplemented=false/NativeFixValidated=false/B08Completed=false。尚无任何修复GREEN，不能修改HybridCLR支持声明为普通Release已支持；补丁实施需本方案之后的明确批准。
- 后续授权已取得：本会话Question工具列明上述本地维护源/一行patch/最小来源和安装接线/全新接收端/最窄反射、普通Release V1/V2及受影响负向，用户明确选择“批准隔离实施（推荐）”。这不是代理转述或模糊最高权限，直接授权实施；不再重复申请同一范围。保持原方案禁止事项，检查点只更新进度，不自行停工。授权不等于实现或测试已通过，相关布尔值须依后续实际证据更新。

### B08实施增量（2026-10-29 22:30Z附近，继续执行）

- 已落地`Tools/HybridCLR/GlobalMetadata-ParameterOwner.patch`及Prepare/Test-HybridCLRNativeSource两个最小工具。`D/B08Native/source1`从固定Git对象独立导出/装配，913个libil2cpp文件只变vm/GlobalMetadata.cpp，canonical CRLF前后SHA与批准值一致；实际源保留归档LF并单独记录raw文件SHA。两份MIT保留在维护树，源码只在D，未vendor到Assets/官方PackageCache或改旧源。`native-source.json`SHA=`787b8659754b14b60cb2403570390007998596d708092fcd8ffa9cbc6d6563c6`。
- 显式未修改对照`D/B08Native/control1`来自同两Git提交，913文件ChangedFiles=[]，不套patch；清单SHA=`781b4af920174d6921588aecfef3ff6e120f1b1259406d1c0bb39203f02f5271`，variant=UnmodifiedControl。不将该本地装配树伪称未经改装的SDK安装行为。source/schema/路径/许可证/两个owner扩展绑定的回归已22/22，包含对照不得隐藏代码改动。
- owner schema2仅新增成对可选nativeSourceManifest/nativeSourceManifestSha256，限定6000.3.27f1及D/B08Native，未知字段仍拒绝。默认Install拒绝custom owner；新InstallLocal仅对未有LocalUnityDataDir的新receiver、固定官方SDK调用其公开入口，副本逐文件验SHA并发布native-source-binding。Build前核静态源码，官方会生成的hybridclr/generated另由既有Prepare阶段负责，最终基座仍完整冻结；receipt显式NativeVariant/源清单SHA/OfficialUnmodified=false，版本文本9.0.3不代上游纯净证明。
- 参数oracle来自该run最终冻结的Aot/mscorlib.dll，不硬编码h17 token；单独Parameters action在真实metadata/热更程序集加载后、Core/JSON启动前反射数组合成方法。覆盖热更数组/AOT值数组/AOT引用数组所有RemoveAt协变副本，generic array Contains、Interpreter属性setter、无definition的array Get；严格name/type/token/参数数量与各控制，不以“不崩”计通过。
- h36补丁版首个普通Release已成功构建，PlayerFingerprint=`0f85392fd3a31ecaf169acb952d913d3fac0feb01f60218fb799956d1566a654`；参数首次parameters1因夹具错误假设RemoveAt同名方法必须Single而exit21，在GetParameters前失败，不是native修复RED。改为遍历全部实际方法并逐项严格断言，旧h36不重写。h37/h38后续夹具编译分别因LocalUpdate新增变量名误写evidenceRoot、再错误猜为_evidenceRoot而失败，记录均保留；这是执行者错误造成返工，不归因Unity/产品。
- 已纠正先读真实字段_root再修改，新增仅内存、不emit的`D/Check-B08RuntimeCompilation.ps1`复用h36实际IL2CPP响应文件209份引用/C#9，b08-runtime-semantic-red1重现CS0103→green1/green2无语义错误。以后先做该低成本检查，不用整轮新接收试编译；不修改h36引用或生成csproj。原兼容163项曾因新Build检查插到标准StreamingAssets前置之前失败，已恢复原先后顺序后163/163通过。
- 对照h39采用最终相同消费者模板、零native修改，独立裸导入candidate13/B04可选包/固定SDK、官方local安装、空宏普通Release构建均exit0。`parameters-control1`实际进入热更ExampleMessage[]的合成IList.RemoveAt，在严格最终AOT参数一致性断言exit21；不是AV、不可称原版正确。oracle mscorlib SHA=`2976ce3ffc8cb011c333a886b7148c773c04f378673c1fc0a8954a210627242a`，Name=index/Type=Int32，原结果保留。
- 对应补丁h40全新载体、同工具/固定上游，`parameters-patched1`六类严格断言全通过、正常exit0；最窄RED→GREEN已成立。`release-v1-1`实际通过未改JSON→ExampleMessage[]→AddModel→Module.Running、依赖/async/Prefab/幂等/Close释放，正常exit0，原Player全部文件指纹不变；h17原AV和h39原失败不改写。
- h40已发布Release V2且原Player/最终AOT基座指纹不变。为避免普通Release增加TLS验收宏，新增LocalUpdate消费显式同owner/原Player/AOT/源清单绑定的本地发布snapshot，调用生产Prepare/Activate并保留Busy与同进程替换/重启契约；记录remoteTlsTested=false，不把本地发布接入说成Release远端TLS验证。TLS/签名/metadata负向仍用独立Development载体走既有受控远端链，凭据各owner隔离。
- 静态收尾在真正运行LocalUpdate前发现owner描述符没有自引用OwnerSha256字段，使用该字段必失败；改为原先已验证的BoundOwnerSha256常量，增加真实模板AST回归：163/164 RED→164/164 GREEN（b08-local-owner-green1），209实际引用语义亦GREEN。旧h40虽然V1/参数已通过，但其消费者不含此收尾修正，不回填或重建冻结基座；最终更新链将用新h41，Development负向用新h42，各自绑定新owner，避免跨variant缓存污染。
- 本阶段没有Unity升级、系统信任/全局il2cpp/默认IDE/权限变化，没有新分支/提交/代理；没有重新研究或改写h17 dump。B08NativePatchImplemented=true、最窄NativeParameterFixValidated=true、普通ReleaseV1Validated=true，但V2/负向/最终保护尚未闭合，B08Completed=false；继续同批，不因本检查点停工或进入B09/B10。

### B08最终验收记录（2026-10-29，批准的维护runtime范围完成）

- 最终边界：维护variant=`ParameterOwner`，SDK仍官方9.0.3/commit6cadd24d101f4cbd00cd80190106f6621132d24d，native固定il2cpp_plus6eb38b29c3a7448b3ac6c4dee450de375b5d158d+hybridclrbf01f8eed56a077781c3a7b25f418603c7645588。唯一native delta为vm/GlobalMetadata.cpp参数表归属重载，不修改Core/Json业务、不升级SDK、不全Preserve。完整源/两MIT在独立D/B08Native；仓库保留最小patch/来源构建验证和原入口接线，未整仓vendor native或创建远程fork。
- Native清单：source1 SHA=`787b8659754b14b60cb2403570390007998596d708092fcd8ffa9cbc6d6563c6`，control1 SHA=`781b4af920174d6921588aecfef3ff6e120f1b1259406d1c0bb39203f02f5271`；913文件路径集同，仅GlobalMetadata.cpp raw SHA不同。CRLF规范化pre/post保持批准的6c2902bf…→069213cf…，清单另存实际归档LF raw hash，不混用两种字节身份。正式local安装、构建与最终闭包均核实际静态native源和显式variant，官方会生成的桥由原Prepare阶段负责并冻结，不只信9.0.3版本标记。
- 严格参数对照：h39原始native普通Release首次GetParameters无AV但AOT参数一致性失败，真实exit21/Player不变，保留为RED；h40一行补丁普通Release同消费者六类全通过/exit0。两者除各自Root/OwnerHash绑定常量外，Runner代码逐字相同，最终Aot/mscorlib.dll同SHA2976ce3ffc8cb011c333a886b7148c773c04f378673c1fc0a8954a210627242a。热更数组的3、值数组1、引用数组9个RemoveAt副本均逐项核本run oracle name=index/Int32/token；另有generic Contains、Interpreter setter及null-definition array Get控制，不以不崩或跳过副本冒充GREEN。
- 普通Release主链在最终h41：candidate13+B04安全可选包先裸接收，再固定SDK/官方InstallFromLocal；14个正式Build阶段全通过。非Development、空额外Define、IL2CPP/Low/API3，实际Bee两份Player输入明确Development=false/ConfigurationName=Release，Il2CppDir指向本工程已核local源，不从空默认设置推断C++配置。release-v1-1、release-local-update1、release-v2-restart1全正常exit0；未改JSON→Model[]→AddModel→Module.Running、Support依赖count2/async/Prefab、幂等及Close资源释放均实际走到。
- 同Player更新：CompileExternalReleaseVTwo只重编Support/热更输入，PublishPatch未Strip/Bridge/重建Player；原Player和最终AOT基座指纹始终相同。普通Release用显式同owner/producer/Player/AOT/native清单绑定的本地发布snapshot，生产Prepare/Activate真实验证运行中Busy、关闭后激活、Dispose/new loader仍拒同进程替换并RequiresRestart；重启同Player执行Native:v2及完整释放。remoteTlsTested=false，绝不把此本地链当普通Release远端TLS通过，也未开启额外验收宏。
- Development h42为另一新owner/缓存/基座，未复用h41状态：V1、真实RSA签名+HTTPS V2 Check/Prepare/Busy/激活/同进程拒绝、服务停止后重启V2均正常exit0。坏签名单bit而Payload/Algorithm/KeyId不变，生产UpdateFailed/内层Release signature rejected.、loader/native前拒绝；坏Support载荷长度/SHA在native前拒绝且非RequiresRestart；末尾zzAbsentAot元数据在部分成功后nativeErrorCode3/RequiresRestart，后续新loader仍拒绝。三层边界不可互替，未删除功能或弱化原断言。
- 三个本机服务均新h42凭据域，未借h17私钥或安装系统信任；ServerV2/server-v2-1接受7请求、BadSignature/server-signature1仅1个release.json请求、Bad/server-payload1接受7请求，Errors=0，均通过自有stop标记正常退出。凭据/发布物和Ready/Request/Exit原件保留；其它用户Node进程不触碰，不误称全系统没有Node。
- 冻结：h41e/ReleaseBaseFreeze为1225文件，h42e/NativeBaseFreeze为1247文件，实际Player/最终AOT/Cpp/生成桥/配置与每文件SHA均复核；freeze回执的PlayerRunVerified=false保留其“只证明构建冻结”的原语义，Runtime另由11份真实结果和最终闭包闭合，不改写历史回执。
- 最终只读`D/Check-B08Closure.ps1 -Run final2`发布不可覆盖`D/b08-closure-final2.json`，B08Completed/NativeParameterRedToGreen/OriginalReleaseBusinessPassed/SameReleasePlayerV1V2Passed/DevelopmentRemoteAndNegativesPassed=true。11份通过原生进程（h40参数/V1两份、h41三份、h42六份）均exit0/Player不变；另保留h39故意对照exit21，不累计为通过数。静态源、包、SDK锁、14阶段、最终AOT oracle、相同消费者、实际编译配置、两冻结、三个服务、Source与旧现场SHA已复核。
- 离线最终22/22 source/owner对照门禁、164/164兼容/安全回归，209份实际Unity IL2CPP引用/C#9模板语义0错误、无emit/执行；不把它们代真实编译/运行。h36的Single假设失败、h37/h38两个局部名编译失败、LocalUpdate自引用owner字段的静态RED均保留并纠正，最后一项163/164→164/164；不归因用户或产品。错误CLI参数/缺Ready在启动前拒绝的原轨迹亦保留。闭包助手曾误用不存在的进程helper及冻结receipt字段，发布前严格拒绝后改为真实进程检查/原Reports/exit.json；final1及加强final2都只读复核，未改任何结果。
- 源/旧现场保护：h17原GlobalMetadata.cpp仍SHA6c2902bf…；ReleaseCrashFreeze全8文件仍匹配原清单，原AV -1073741819、NativeRunPassed=false/无成功结果保持。h15/h17旧Library/Cpp/Player/owner及官方PackageCache/Editor未手改或重建；Source2022主工程manifest/lock/ProjectVersion/Settings/AGENTS/默认IDE/权限未变，无分支/worktree/代理/提交/push。
- 最终`D/source-b08-final1.json`实时Source PID11568/2022.3.62f3c1/唯一原YLFrameworkDev loaded且clean、非Play/编译/更新/失败/默认Code；`protection-b08-final1.json`2553→2553、差异数组空，SourceProjectTested=false/SourceSceneSaved=false。默认candidate13 SHA仍e9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8；维护patch在Tools，不伪装成unitypackage已包含修改SDK/native。
- 2022边界：native维护源和owner扩展明确只允许6000.3.27f1，2022不同tag未改也未套此patch；既有映射/legacy/模板守卫保持，未声称本次做过2022 native运行。原版SDK与公开Native支持声明未升级为官方普通Release已修复，最终双版全能力接收和支持声明仍由B10决定。
- 完成结论限定已批准维护variant；UnmodifiedOfficialNativeDeclaredFixed=false/Native2022PatchApplied=false/WholeCompatibilityPlanCompleted=false/FinalReleaseAccepted=false/B09Started=false。本批Editor/Player/Node服务均结束，无需人工窗口操作；本轮在B08收口，不自动进入共享Support或最终发行阶段。

### B09开始（2026-10-29 23:23Z，进行中）

- 用户明确进入B09，同批持续实施/必要验证，不因60–90分钟检查点自行停工，也不自动进入B10。Source已实时核验PID11568/2022.3.62f3c1/原唯一YLFrameworkDev clean/非Play、编译、更新或失败/默认Code；`protection-b09-before1.json`通过。已有用户改动、B01-B08产物与所有RED保持，不起Source第二实例/不Play或NUnit/不保存场景。
- 范围沿现有code-only ProjectSubsets探针：全局SDK仍注册Entry+Support，A实际发布/加载仅Support，B根为Entry且闭包包含同Identity/MVID/SHA/Length的Support、使用A的一跳baseProfile；A的native/AOT基座必须预留真实全局注册所需能力，B只PublishPatch，native阶段集合必须空、Player/全部baseline/A发布物不变。两个loader原生共享同一Support Assembly且域内只有一份，实际async/必要generic消费，最后双方manifest屏障和ResourceStartup释放。
- Probe只核Entry为具体Module类型，不在新code-only项目中启动旧样例Module（不含其配表/Prefab数据）；完整Module/JSON业务继续按B08有效原件复用，不新增热更UGUI/Entities-ECB或泛化其它功能。既有negative/type/metadata/跨项目归属与变更约束按受影响范围补证，不将零测试、伪native或仅类型存在当运行通过。
- 6.3将新建独立owner/基座/Player/缓存，复用B08已验固定源码清单787b8659754b14b60cb2403570390007998596d708092fcd8ffa9cbc6d6563c6及一行patch，仍走官方local安装；只补已有子集入口的显式variant/source绑定（B08故意拒绝该未授权入口），不改维护源、旧h39-h42 Library/Cpp/冻结物或默认官方SDK。2022另用固定映射/SDK9.0.3与该引擎tag独立接收，不共享6000.3的native/AOT输入。
- 首步已读Probe、PrepareProjectSubsets/BuildProjectSubsetBase/PublishProjectSubset及父运行门禁，尚未新建B09接收端、未构建或运行B09 Player；B09Completed=false/WholeCompatibilityPlanCompleted=false。

### B09增量检查点（2026-10-30 00:35Z附近，继续执行）

- 子集native绑定接线：base/patch/conflict三个回执与运行Probe均明确绑定ParameterOwner/source manifest SHA；2022无custom字段仍走固定官方SDK。子集来源/配对回归22→29，先RED后GREEN；原164门禁保持。新增Runtime真实泛型Count<int>(List<int>)=2，不以async结果替代generic实际消费。
- 相关负向明确分层：编辑器反射调用真实ValidateBaselineBinding/ValidatePatchSet，使用真实冻结AOT/global输入，七种无效一跳绑定、外来owner/settings、未全局登记、缺metadata/native需求均只在临时克隆上变异，不改真实基座。正向两项加十二负向共14项。运行时另以真实Resource发布的同Assembly Identity/不同MVID和hash Support PE，在A已加载时要求NativePreflight/RequiresRestart拒绝；随后完整释放该资源phase，再从新owned cache启动原A/B，证明拒绝发生在不可逆native之前且未污染进程。还保留未挂载foreign项目的Manifest阶段拒绝。
- 两个代码预检先于新工程：209份实际IL2CPP引用Runtime C#9语义通过；新增Editor预检279份真实引用捕获CanonicalIdentity为internal及ResourceEditorBuildReport无HasSucceeded的错误，按真实源码改为显式culture归一与BuildError/PublicationError后GREEN；不启动Unity来试这些编译错误。
- h43 native实际14阶段全绿，但旧消费模板跨native仍持有旧Profile对象，冻结标记SHA与磁盘GUID均正确，末端GetAssetPath身份核验失败。原result.passed=false/exit1保留，未重写标记。修复只在消费者：native工作前记保存路径/GUID，工作后从固定路径重新加载并严格核同身份，再读registration；后续h45回执实证CachedProfilePathAfterNative=""、ProfileReloadedAfterNative=true、新加载GUID正确。变异回归验证删掉重加载必被拒；165/165通过，未改产品BuildInput或重建h43。
- h43另一次只读约束诊断发现3参Analyze实际为internal而非public，修正反射BindingFlags包含NonPublic，未增加公共API。h45完成A基座、14约束、B发布/冲突fixture均exit0，但新冲突回执漏四身份字段被父门禁在Player启动前拒绝。未补签原回执；源模板补UnityVersion/Revision/Receiver/OwnerSha256，新增165/166 RED→166/166 GREEN，后用新h46，不覆盖h45。
- h46全新candidate13/B04裸导入→固定SDK→官方local安装B08维护源→真实Support外部编译/资产准备→A子集14阶段→14约束→B无native阶段发布→同MVID变异冲突发布均exit0。B保持A全部Player/baseline文件与A发布物不变；冲突生产者finally精确恢复B原始manifest/Support bytes，负向发布物另存，不改原A/B产物。
- h46/subsets-native1实际Player正常exit0、passed=true、simulation=false；A仅Support/B Entry+Support均Native，ReferenceEquals共享Assembly、域内SupportCount=1、genericCount=2、async=v1-async、Entry具体Module类型存在但entryModuleStarted=false。foreign项目Manifest拒绝；有效不同MVID的Support在NativePreflight要求重启，资源phase释放后原正向仍成功；最后两个barrier释放/stopped=true。Player前后指纹16f19a2644e81fb129212aa61c7741a04fdf6eb00c613433b140d95bbf6d3e0e完全相同。
- 该次父工具随后错误要求ProjectSubsets没有的graphicsDevice属性，原父失败保留，不改JSON/退出事实。实际日志有Direct3D12/真实Renderer。新纯解析Test-HybridCLRPlayerGraphics只允许该已知subset格式从真实D3D日志补证；其它消费者缺字段/Null仍严格拒绝、subset显式Null也不被日志覆盖。166/173 RED→173/173 GREEN，不放宽native/集合/owner/退出门禁，不重跑已通过Player；最终独立只读闭包将分离原运行和父解析接受。
- 本阶段外部薄组合脚本曾在启动前因PowerShell变量后冒号未用花括号解析失败，无文件/Unity动作，修正后使用新未占路径。它只串行组合原入口，无恢复/CI新框架。以上返工均为夹具/执行者错误，不归因产品或用户；所有RED与当前成功证据均保留原身份。
- 6.3完成技术结果不等整个B09完成；2022独立原SDK/tag交叉和最终保护/精确闭包待补，B09Completed=false。旧B01-B08冻结工程/源码/Cpp/Player/native源、默认candidate13及Source主场景未改。继续当前批，不因本检查点停工或进入B10。

### B09实际阻断与6.3独立接受（2026-10-30 01:03Z后）

- 2022固定源恢复：h44/sdk1是Gitee v9.0.3 Git RPC HTTP504/正常exit1，原件保留。安排一次2分钟wakeup退避，sdk2同源同版本正常exit0，不改代理/TLS/来源，也没有重复Create/裸Import。随后官方Install/Configure均正常exit0；实际2022 il2cpp_plus HEAD=`ba3a76c9979d14cee404fb5d44301773e0768866`、hybridclr HEAD=`83929787581bce6c7219418f62348a10c33582ee`，未套6.3源或patch。
- h44/external1 00:48:27Z启动外置Support真实编译，回调JSON passed=true/errors=[]，DLL SHA=`2bc551bacf817ad8d4b6e5e278ee77c50de1f00f26668994f0c12a2bef2c782a`与落盘字节相符，日志进入Input System Shutdown。此后Editor未退出，原Strict/900000ms截止在01:03:27Z真实TimedOut、forced=true、exit=-1、artifactPassed=false、succeeded=false、cleanupError=null，PID25212已结束；`external1.json`成功与`external1.log.exit.json`失败分别保留，不能混写为exit0。
- 该外置compile callback明确不属于既有Known2022EditorExit测试/发现豁免；没有借B01 seal/B03 Stage9接受、延长deadline、套新豁免或继续把此阶段当通过。不是因时间检查点停止，也不是没有可见deploy。进程终态已发生，等待或重复相同退出挂起不能安全补足授权门禁；不再自动启动同一失败编译或研究dump/网络退出根因。
- 暂停的2022依赖项：Setup/PrepareProjectSubsets、实际A native/AOT/Player、B发布与14约束/冲突probe尚未执行，NativeSubsetRuntimeTested=false。h44未有基座/Player，保留已通过裸导入/SDK/native安装/Configure及实际外编译产物；后续若对该回调独立功能/退出分离另行明确批准，须先只读验证源/API/DLL/日志/owner/过程事实，不能追认原进程通过，也不必重新创建或重做裸接收。
- 6.3成功部分独立接受：`D/Check-B09Checkpoint.ps1 -Run blocked1`发布不可覆盖`D/b09-checkpoint-blocked1.json`。核h46 base/patch/conflict四身份/version2/custom native/source、Player/AOT全文件指纹、B无native阶段、实际两项Native程序集精确集合、共享Assembly/唯一Support、generic2/async、foreign Manifest拒绝、真实MVID NativePreflight/RequiresRestart拒绝后正向恢复/双manifest释放、exit0、原D3D12日志、14真实约束、1198冻结文件及原回执SHA，Unity63SubsetAccepted=true/B09Completed=false。
- 原h46父工具在运行结束后因无graphicsDevice字段失败不改；新只读接受根据已TDD验证的专用格式解析真实D3D日志，未修改原result/exit或重复运行Player。普通消费者仍拒missing/Null，subset显式Null也不能用日志覆盖；173/173在`hybridclr-guards-b09-subset-graphics-green1/results.json`。29 source/owner/subset配对及209/279引用语义结果分层保留，非native运行替代品。
- 冻结位于`h46e/ProjectSubsetsBaseFreeze`，1198文件含Player/AOT/Cpp/native生成桥/保存的HS输入/原始运行与发布回执。首版外部冻结脚本在全部复制后误读不存在的scope.Version，未生成receipt；改为实际ExpectedUnityVersion后，仅以显式CompleteOwnCopiedFiles核已复制Reports/owner.json同SHA、全部预期文件/无未知项与每字节相同，再写新receipt，不覆盖/补文件或领养未知目录；CompletedOwnInterruptedCopy=true明确标记。该冻结错误不属于产品失败。
- 最终暂停状态：`protection-b09-block1.json`2553→2553、Passed=true/三差异数组空；Source实时PID11568/2022.3.62f3c1/原唯一YLFrameworkDev clean、非Play/编译/更新/失败/默认Code。OS仅Source主Editor及其import workers/Hub，没有本批接收端Editor或HybridCLR Player残留；未修改Source设置/官方缓存/旧B01-B08冻结物/SDK版本/native patch/默认包，未提交推送、新分支/worktree或代理。
- B09当前不是完成状态，未进入B10；恢复阻断需要处理本次2022外编译回调的严格退出/功能接受授权边界，不能自行假定现有“进入B09”已扩大旧例外。6.3结果仍有效，不因2022阻断重复运行或重建。

### B09单次产物接受授权（2026-10-30 01:12Z后，核验待执行）

- 用户询问阻断处理后，Question逐项列明“只针对B09 h44/external1、只读核owner/固定SDK/实际API/编译源/回调/DLL/日志、原进程保持TimedOut/forced/-1/失败、不改通用政策、不重跑该编译、不用于其它构建或Player”，用户明确选择“仅接受本次产物（推荐）”。这是一条来自当前用户的有限新增授权，不是旧Known2022EditorExit自然扩展。
- 授权的结果边界：核验全部成立才能新建独立functional/artifact acceptance回执，允许后续h44步骤消费这些字节；原external1.json/external1.log及其process/exit记录不修改、不补签exit0、不将artifactPassed/succeeded改true。任一源/API/字节/身份或日志问题均拒绝接受，不以本授权掩盖未完成编译。
- 后续Setup/子集Build/Publish/Player仍按原Strict正常退出门禁，未授权对其它步骤做同类接受；当前6.3已通过部分不重跑、不重建。仅继续同一B09，不进入B10。
- 现恢复持续目标以实际执行上述核验和剩余2022交叉；单次授权本身不是产物核验通过或B09完成证明。

### B09最终验收记录（2026-10-30，批准适用范围完成）

- 当前默认包仍candidate13（SHAe9ce71b501893ebcf5af8ba4c38695f1f4841c777a52a21f09ba2264579fe3a8），可选包仍B04安全HybridCLR；没有产品Runtime/Editor、SDK版本、native patch或公开支持声明改动。只补原接收工具/模板、scope/门禁与本计划；新meta由各新receiver Unity生成，不手写或改Source资产身份。
- 两引擎分别独立基座：6.3 h46是B08精确ParameterOwner源清单787b8659754b14b60cb2403570390007998596d708092fcd8ffa9cbc6d6563c6、SDK9.0.3固定commit；2022 h44使用同SDK的官方对应native，il2cpp_plus ba3a76c9979d14cee404fb5d44301773e0768866/hybridclr83929787581bce6c7219418f62348a10c33582ee，未套6.3 patch/共享AOT/native。固定各自Unity revision/UGUI/UTF，所有owner、包裸导入、源码/SDK和输出路径分别绑定。
- 单次2022外编译处理已实际完成：`h44e/external1.artifact-acceptance.json`按本会话Question明确授权只读核owner58c80c…、PID25212/invocation387e…、SDK Git hash、原Configure正常exit0和保存的API3/IL2CPP、源3b9bf…、回调/编译日志、DLL2bc551…/PE身份/MVID、Shutdown顺序与无编译/fatal错误。FunctionalArtifactAccepted/ArtifactMayBeConsumed=true，ProcessExitPassed=false；原external1.json/process/log/exit字节不改，TimedOut/forced/-1/succeeded=false/artifactPassed=false不变，CompilerRerun/GenericExitPolicyChanged=false。
- 接受校验器初次用了错误的包缓存路径（2022仅10位目录后缀、dnlib实际位于Plugins）、继而遇PowerShell自动拆箱Nullable<Guid>，均只在读取阶段被拒，未产生接受回执或重编译；按真实目录/PE API修正后才CreateNew落盘。该有限核验不是把旧Known2022EditorExit扩大，也不用于其它调用。
- 之后h44 Setup、PrepareProjectSubsets、PrepareDeployment、A native Build的14正式阶段、VerifyProjectSubsetConstraints、B发布和有效冲突发布物均真实正常exit0/无forced/timeout。2022的真正子集Player随后passed=true/simulation=false/正常exit0，未使用任何进程豁免；原native/metadata/运行时交叉缺口已经补齐，不是只复用6.3或Editor模拟。
- 正向子集合同：SDK全局注册仍Entry+Support；项目A仅发布/加载Support，项目B根Entry闭包只含Entry+同Identity/MVID/SHA/Length Support，B.baseProfile一跳A且保持本项目发布身份。A基座InitialManifest是全局登记的两程序集，实际交付ManifestA仍一程序集；B只走Preflight/ReadBaseline/Compile/PatchCompatibility/PayloadCommit/ResourcePublish，无native阶段、Player/deploy；A全部发布物、Player与整个baseline指纹不变。
- 双版本真实native均从A开始，B与A的Support Assembly ReferenceEquals、AppDomain仅一份同名Support；Support.Count<int>(实际AOT List<int>)返回2、Task<string>实际yield后输出v1-async。Entry具备具体Module类型，但entryModuleStarted=false，不在无配表code-only项目冒跑旧Module，不扩UGUI/ECS。完整Module/JSON/Prefab业务沿用B08独立有效证据，不将类型存在当启动。
- 双版本相关负向实际执行：未挂载foreign项目在Manifest拒绝、无需restart；A已真实native加载后，同完整Assembly Identity但MVID/SHA不同且PE/描述符/资源发布均有效的B Support，在NativePreflight拒绝并RequiresRestart。完整释放负向Resource phase后从本次新positive cache再载原A/B，仍成功共享同一Support；没有清空进程登记、伪造Native或用EditorSimulation绕过。最后双manifest屏障释放、ResourceStartup stopped/Current=null，两个Player与AOT指纹保持原样。
- 每引擎14个编辑器合同断言：有效一跳绑定、有效全局metadata/native set两正向；unbound/self/chain/directory/compatibility/development/target七绑定负向；外来ProfileGuid/settings两负向；未全局登记/metadata/native需求三负向。调用真实生产Resolve/Validate边界，变异仅临时profile/recipe/baseline克隆，未写真实冻结基座；总28项，不把12个预期拒绝当原进程失败或吞其它异常。
- 两原生基座与运行证据已冻：h46e/ProjectSubsetsBaseFreeze1198文件、h44e/ProjectSubsetsBaseFreeze1025文件，总2223；含实际Player/AOT/Cpp/生成桥、保存HS输入、原始发布/约束/冲突/运行/进程回执，每文件SHA复核。h46首次复制后scope.Version字段错误已按实际ExpectedUnityVersion修正，显式校验本次自有完整副本/owner与无未知文件后只写新receipt，无覆盖/补拷贝/领养，标记保留；h44直接正常冻结。
- 最终只读`D/Check-B09Closure.ps1 -Run final1`发布不可覆盖`D/b09-closure-final1.json`：B09Completed=true、NativePlayerRuns=2、RealNativeProjectLoads=4、RealConstraintCases=28、FrozenFiles=2223。base/patch/conflict/constraint四身份、version2、精确程序集集合、SharedSupport、Player/AOT全指纹、原生/图形/退出/所有权、成功之后所有Strict步骤、最终SDK/维护源/官方2022两commit、单次接受及原失败均分别绑定；没有将原父解析失败或2022失败进程改为通过。
- 最终离线173/173兼容/身份/重加载变异/两图形格式门禁、29/29源码-owner-子集绑定通过，Runtime209/Editor279份真实Unity引用C#9语义无错误（不emit/执行）。原父h46 graphicsDevice格式错误通过真实D3D日志的专用读取独立接受，普通missing/Null拒绝不弱化。源预检、h43旧对象/Analyze访问标志、h45漏四身份等全部失败/修正轨迹按前述增量保留，不重绑旧基座、不归因用户或产品。闭包初次因基线public字段大小写被拒，改为实际HotUpdateAssemblies后严格通过，未改数据。
- 最终Source主线程只读`source-b09-final1.json`为PID11568/2022.3.62f3c1/唯一原YLFrameworkDev loaded且clean、非Play/编译/更新/失败、默认Code；`protection-b09-final1.json`2553→2553、三差异数组空，SourceProjectTested/SourceSceneSaved=false。没有本批Unity接收端/Player/服务残留，未改Source manifest/lock/ProjectVersion/Settings/AGENTS/权限/全局代理或信任，未提交/push/新分支/worktree/代理。
- 结论仅是B09原生code-only共享子集及相关边界完成，WholeCompatibilityPlanCompleted=false/FinalReleaseAccepted=false/B10Started=false。B10独立整分支Review、最终同字节全能力双版本接收/支持声明及发行许可仍未执行；在本批收口，不自动进入B10。

### B10开始（2026-10-30 01:42Z，进行中）

- 用户明确进入B10，按原合同先独立整批/分支Review与必要修复，再最终导出/许可、同字节双版新鲜接收、支持声明与protected-after。不得把本批启动或B01-B09限定完成当整体发行通过；没有commit/push授权，不建新分支/worktree/AgentManager会话。
- 初始实时Source PID11568/2022.3.62f3c1/原唯一YLFrameworkDev clean、非Play/编译/更新/失败、默认Code；`protection-b10-before1.json`通过。B09最终回执有效，当前HEAD dc31794e991ac7fc83b33ff9366a8031a75f8a1b未变。
- Review范围依据D/source-baseline.json：本轮从该HEAD开始，基线只有用户旧Plan/ProjectSettings图标及既有数据/夹具等dirty输入。审查当前所有相关tracked/untracked兼容产品、测试、交付与native工具、patch/依赖/许可及关键未改调用方，不仅最后一批。main merge-base是最初MIT许可提交d673af…，不把整个游戏历史误当本轮新改动，也不漏本轮未提交或新增源码。
- 用户原旧XF Plan、图标/HybridCLRSettings/YLFrameworkData/旧夹具等按保护清单排除自动修改；必要时可只读检查影响，不因纳入git status就认领。审查对象将以不可变字节清单/差异身份绑定；审查进行中不并发修改其输入，不启动Unity/IDE/Player或重跑有效旧门禁来展示工作量。
- 独立性与成本：此前不自动开代理的要求仍尊重；先确认仅一名新上下文、只读、同模型默认继承的内部reviewer，禁写/禁引擎/禁嵌套代理，不建用户可见会话/worktree、不分批泛化广播。没有该批准时不把作者自检称独立Review。
- 已确认后续文档/声明需核真实支持：HybridCLR现README/Dependencies仍仅2022及nativeValidated=false，不能直接翻成官方默认9.0.3双版全面支持；B08/B09的6.3维护variant与单次外编译接受必须明确，DOTween保持wrapper-only，历史输入/其它未验范围不计通过。暂未改产品支持声明、导出新包或创建最终接收端。
- 本会话Question已明确批准“允许一次只读审查（推荐）”：仅一名内部新上下文reviewer、继承当前模型，禁写/引擎/IDE/Player/嵌套代理，不创建AgentManager或worktree，不自动多轮/多代理。已冻结`D/ReviewB10/review1`：154个实际审查变更、3499个源码/资源/上下文文件，含完整tracked.diff、未跟踪代码、相关调用方与一行native patch上下文；审查期间不修改其产品/工具输入。源审查包不是一次最终导出或验收签字。
- 唯一独立reviewer `ses_e757f9bb4ffenc2baakZv9Nfky`已完成只读审查，无Critical；F1/F2两Important阻止最终签字，F3/F4两Minor。原完整回复在Task会话，结构化保存`D/ReviewB10/review1/review-result.json`，明确实际覆盖与大型模板/二进制/引擎运行限制，不声称154变更逐行全审。没有额外reviewer或自动二轮审查。
- F1待按实际immutable owner与TestsSync复核：不同包不能合法重绑同receiver，但同包测试同步/profile变化后旧discovery是否仍可授权不完整选集必须复现。成立则补当前owner/package/实际compiled-source/test graph及profile身份绑定，保留精确集合/非空/退出门禁；F3大小写精确性并入同一修复，不另做美学批次。
- F2需加强actual postimage/ChangedFiles自证；reviewer称“验收未调用Verify”的部分不代表D外部编排，B08/B09外部Prepare/Closure已调用Verify，C#安装/构建也验静态files；不能因此追认旧绿无效或隐瞒真正工具缺口。下一步对自报ParameterOwner但实际未修树做RED，补真实后像/前后差异，不改913文件维护源/清单/旧owner或重跑native实验。
- F4先按真实仓库模板（Stage9NativeInputProbe含实际Screen.SetResolution/held-camera）及B06冻结Player裁决，不把CanvasScaler单测当几何失效oracle；已覆盖范围不机械再建Player。其余Declined to judge逐条在主线程依据已安装版本、B01-B09有效证据/许可范围作ruling，不把作者自检替代独立Review。

### B10 Review修复与最终接收增量（2026-10-30，继续执行）

- F2已实际修复：Test-HybridCLRNativeSource新增真实前/后像文件夹用例，自报ParameterOwner但实际未修、隐藏额外文件改动、伪造preimage、路径集不同、假control均先RED。Verify现重算实际approved前/后像与原/后两树完整path/hash delta，自定义native owner入口强制调用；`b10-native-postimage-green1`36/36及原173兼容门禁通过。旧source1/control1均用新规则Verify通过，原manifest/owner/native树/旧Player未改，不重建B08/B09 native。
- F1与F3已接线：Stage9 discovery/profile升级显式v2 owner/package/classifier/input模式，记录真实Assets C#/asmdef/asmref/rsp/meta与包/设置输入集合、实际compiled graph/程序集输出SHA；发现开始/结束输入必须相同。父测试先核当前文件集合/哈希/profile，再强制正数ExpectedTestCount；官方RunStarted还核当前compiled graph并写独立输入验证回执，父接受必须该回执匹配。结果与成本叶使用CaseSensitive排序/Compare及精确计数。旧接收端/发现不重绑，只在新z1/z2安装当前helper。
- F1回归：真实文件11项全部GREEN（foreign owner/package、新增/变更source/asmdef/meta/settings、旧classification/profile/input、旧未绑定schema）；原可选77项也通过。其fixture stage9-owner明确Purpose=Stage9-input-binding-guard-fixture/Simulation=true，不允许真实receiver owner或Unity进程。实际6.3/k3引用286、2022/v9引用231 C#9语义无错误，不emit/执行。增加真实可选程序集后故意用z1/list1，`stale-after-options1`在Unity启动前因当前输入不同正确拒绝，未写selection/跑测试。
- F4裁决：repo内Stage9NativeInputProbe确有实际Screen.SetResolution和held-camera/geometry旧Up不Click+新gesture恢复，B06的实际两引擎/后端证据另有绑定；CanvasScaler单测不被当作该oracle。SceneHandle保留项已只读解析精确6000.3 CoreModule（SHA42e4b6c8…）：SceneHandle.m_Value是EntityId、EntityId.m_Data是Int32，转换IL直读该字段；不是仅据强转猜宽度。其余declined项继续按原证据/明确限制逐项收口，不创建第二review。
- 原始归档LF推断更正：实际Get-FileHash表明B08归档工作文件的GlobalMetadata pre/post raw SHA就是批准的CRLF6c2902bf…/069213cf…；并非此前文字假设的LF工作文件。原始清单始终存真实字节且新Verify复核一致，数据/CPP未改变，后续native来源附件按实际CRLF表述，不混同Git blob规范化形式。
- 现有文档更新：Documentation/README加定点双版本/独立接收/平台与未验边界；HybridCLR README/Dependencies加两个准确verifiedEnvironments、6.3明确ParameterOwner源/一行patch、2022仅Development code-only共享子集而非完整Module/普通Release重验。旧scope.nativeValidated=false保留为无全能力总括声明，未假称官方未修改native修复、普通Release TLS或High。新增Tools/HybridCLR/NativeRuntime-ParameterOwner.json为独立附件溯源，不把源树/SDK/native二进制塞入默认包。
- 正式新路径导出：Source11568/原clean场景只调用正式ExportUnityPackage及HybridCLRIntegrationExporter(includeSample=true)，未SaveAssets/保存场景。默认`YLFramework-U63-final1.unitypackage`4555368B、SHA012aacc474616e63a45cf8474e3f5690e70811db62ba0279300f8490752a2762，Audit1038通过；HybridCLR85003B、SHA259b9a13897e051b6ce0256d88b3c601570ef5a528f2b82bfd370f0a6753cc80，29条仅README/Dependencies payload改变、路径/GUID/meta全同。正式export记录b10-formal-export1.json仍AcceptancePending，不提前签字。
- Excel/DomainExcel/DOTween三正式B04归档内容与当前源/meta仍逐文件同字节，创建新final1文件名别名而非重绑旧owner，SHA分别4b9d2170…/0fc4afdb…/f7b27abe…；新的audit也核14/4/5完整closure、框架LICENSE、MiniExcel原Apache+归属/XF MIT、无SDK/dnlib/native/Tests/secret/Pro。DOTween仍wrapper-only，vendor来源pin不变且不随归档分发。native附件ZIP7文件只patch/溯源/现有README和4完整许可证，未含源树/二进制/凭据；`b10-artifacts1.json`及每包/ZIP audit保留。
- 新z1/6.3与z2/2022均用同一默认final1 SHA，原空工程modules/UGUI2或1/Json.NET3.2.1先Create→裸Import，无harness/UTF/Input/SDK；两次实际编译正常exit0。然后才装最小场景消费者，Generate/Mono Build/真实graphical Player smoke均正常exit0，正式资源/UI/场景业务及责任释放；不以此重复声称OS输入/DSP/IL2CPP。z1首次长Run输出触发既有PathTooLong守卫，保留失败输出后仅换最短Run=m正常，未改系统或守卫。
- 两工程生产冒烟以后才加UTF1.6/1.3.9、Input1.20.1/1.14.2与当前完整测试助手；z1第一次漏装测试UIAcceptance所需Input SDK的编译RED保留，随后固定版本补齐正常。当前四可选产品源/每meta与最终归档逐文件等价，在z1/z2按既有严格复制安装（准确记为source-equivalent安装，不冒称这一步AssetDatabase.ImportPackage）；另加三个独立可选测试叶，固定SDK9.0.3无native安装。Domain/DOTween宏和Coexistence真实11程序集/3宏两版本都正常exit0。
- F1真实runner验证：z1/list1+p2下Timer四叶4/4正常exit0、input-validation回执与精确XML匹配；增加可选程序集后新list2/Excel精确41叶两版本都正常exit0，当前图与源绑定、optional exact CLI filter和XML集合闭合。旧list1随后拒绝是预期负向，不重跑已证native/IDE/资源重型流程。
- 新出现的剩余限制必须保留：z2/list1+p1的Timer弱引用四终态混组3/4，Invalidated在原WeakReference.IsAlive断言失败、正常exit2；同run输入/compiled graph validation已Passed，非F1绑定失败。未增加GC次数、Sleep、Ignore或减断言，生产Timer不改；随后不同触发组（结构诊断+仅Invalidated）2/2正常exit0，仅证明上下文相关，与B02历史一致，不能追认原混组绿或宣称根因修复。历史全阶段NoInlining候选已失败并撤回，不能重复；主线程-only合同也不允许把调度器迁到worker来制造回收。最终B10结论尚须准确处理该未解释GC混组RED，不先标完整通过。
- 本阶段没有新reviewer/代理、Source Play/NUnit/保存/重启、commit/push或冻结旧receiver重写；重要修复与交付身份已有证据，但B10Completed/FinalReleaseAccepted仍false，最终保护/完整证据闭包与剩余限制处理继续当前批，不因本增量检查点停工。
- Timer后续已做因果诊断而非随机重跑：`z2/list3/roots1`再次原四叶3/4、正常exit2，同一Invalidated弱引用失败。仅失败后读取官方实际iterator、scheduler/handle及两个精确ListPool的完整backing arrays；`z2-timer-roots-roots1.json`无截断、弱引用仍Alive但CallbackTarget强路径为空，额外GC=0，线程1。`b10-timer-metadata1.json`只读核实际iterator无直接目标字段、Detach/Dispose及安装mscorlib.Clear/Array.Clear IL；没有把它称为完整native stack root枚举。
- 独立receiver-only `B10TimerGcContextTests`复用原ScheduleCallbackTarget及同一终态逻辑，唯一观察边界改到Editor主循环delayCall，同主线程、仍原两次Collect/finalizer。四终态释放+四终态故意静态保活8/8、正常exit0；故意保活仍可被检测，非删除回调或放松断言。原深层Runner栈观察与独立Editor回调有可复现差异，具体底层保活地址未直接定位，不宣称修复Mono GC或生产Timer泄漏。
- 据此仅修正正式四终态测试的收集边界：同线程独立delayCall、显式10秒回调完成门禁、finally退订；GC次数、原weakref/terminal/KeepAlive全部保留。生产Timer、默认包字节和GUID/meta未改。z2连同原RED/Library/两个诊断脚本保留，不在其上同步修复；新z4/2022再次最小依赖裸Create→Import后才安装测试，正式原四叶4/4及完整非Session Foundation245/245全部正常exit0。z1/6.3通过receipt-owned tests1→tests2仅同步此文件、新主list3后完整Foundation245/245也正常exit0（含同四叶）。不把额外4或诊断8重复计入245，也不重跑未受影响的Session/native/DSP/OS输入。

### B10最终闭包（2026-10-30 04:20Z附近，批准范围完成）

- `b10-package-mapping1.json`重新核final1全部1038源payload/meta、candidate11/12/13的Ordinal结构化差异及四optional归档。final1对13只有主README；Excel/DomainExcel/DOTween为原B04同字节正式归档新文件名，HybridCLR只改README/Dependencies。Resource原r6源清单/targets/gate4 SHA成立，仅Instances/ContentValidation/PanelInputCapture三处后续产品变化，分别绑定双版native28/B06当前输入证据；既有140不改称新包运行。
- `b10-review-rulings1.json`绑定唯一review冻结SHA、最终实现指纹、F1-F4裁决与八个declined项目，不另派第二review。11输入绑定/36 native前后像/77 optional/173兼容守卫原成功回执逐case复核，两真实913文件源树增强Verify均通过。八项明确Analyzer/SceneHandle/TextCore/camera/native效果/分发许可/零callback/晚取消边界；支持范围没有扩写成官方native已修或无条件全功能通过。
- Analyzer新只读证据`b10-analyzer-identity2.json`从candidate5真实tar payload与Git HEAD精确本地LFS原件核原SHA4c621b4e…，当前c0d20af7…仅五处PE/debug身份72字节，剩余字节/208方法体/27EH全同；不重建Analyzer、不唯一归因SDK。初版只读脚本因PowerShell逗号/加法优先级错误取到Git根树，其identity1的Git字段由identity2明确替代，原文件保留；候选5独立二进制比较有效。历史181已执行事实沿用，当前Source Temp的build-info/TRX已不存在，不伪造重建或声称本轮再读到它们。
- `b10-release-manifest1.json`逐件绑定默认final1、四optional final1、七文件native ZIP及审计SHA/长度；明确MiniExcel1.31.2完整Apache/归属、XF MIT、native两MIT+SDK MIT+YL许可随附件，DOTween wrapper-only、vendor/Pro HOLD。正式包与当前源/meta等价，无Tests/SDK/native/generated DLL/秘密材料泄漏；本次optional实际安装记source-equivalent，不假称ImportPackage。
- `source-b10-final1.json`为最后Source实时MCP守卫：PID11568、2022.3.62f3c1、唯一YLFrameworkDev loaded/clean、非Play/编译/导入/失败、默认Code；只为新增输入守卫PS1定向Import生成meta（GUID050b5d10d5897dd4cb3f2a0949529563），不SaveAssets/保存场景/跑Source测试。`protection-b10-final1.json`为2553→2553、全部差异0；用户原Plan/图标/HybridCLRSettings/数据/旧fixture均不改写。
- `Confirm-B10Closure.ps1 -Operation Verify`实际通过：两新鲜默认裸导入、两个原graphical Mono Player业务/exit0/全部PlayerFiles复核，当前两版Foundation245及原四叶4、两版Excel41逐名XML/marker/owner/选集/Strict退出复核；不把重复四叶或诊断八叶相加凑唯一测试数。h17原crash8及ReleaseBase1236、h41ReleaseBase1225、h42NativeBase1247、h46子集1198、h44子集1025共5939个冻结payload全部重算SHA/长度，h17八份原件也相同，不再开dump研究。48份变更/新增PS1解析、相关diff空白、新资产meta齐全；无自有Unity/Player/Node服务残留。预闭包`b10-preclose-final1.json`的AllApprovedGatesPassed=true，最终仅再绑定本计划与不可覆盖闭包，不重复技术运行或修改历史AcceptancePending回执。

<a id="release"></a>

### 2022已知退出例外与新流程（2026-10-28 19:30Z用户批准，已完成）

- 已知项ID：`U63-KI-2022-EDITOR-EXIT-001`。暂缓根治，独立保留，不再作为B01/B03及其它批次的前置修复。功能测试未实际执行仍是待办，不能借延期省略；最终交付口径为“双版本功能兼容验证，附已知2022 Editor自动化退出限制”。
- 范围：只限schema2独立接收端、Unity2022.3.62f3c1/rev1623fc0bbb97的Editor discovery/EditMode/PlayMode，必须显式`-ExitPolicy Known2022EditorExit`。默认Strict和6.3、导入/构建/外置编译/Standalone Player门禁保持原样。
- 新顺序：精确安装/发现 → 预定选集 → 实际运行 → NUnit RunFinished（包括各fixture SetUp/TearDown）/Discovery API销毁后的原子completion seal → 观察关闭；执行预算内看到seal才开始120秒退出宽限。无seal仍是执行超时，晚到seal不能救回；seal改变/链接/取消/观察器错误一律拒绝。
- 功能收口：发现JSON/安装声明/真实源码及meta闭合；测试要求非空完整Expected集合=seal全叶=官方XML全叶，所有断言Passed、失败/skip/inconclusive为0。输出SHA、selection SHA、owner/version/project/run必须一致；Parent接受时固定官方XML SHA，后续按同一字节消费，不能伪称producer已签署尚未落盘的官方XML。
- 退出分开记：正常observed exit0仍ExitPassed=true；仅完成后进入已知shutdown路径且宽限到期的`PostCompletionExitStalled`可附例外。ProcessSucceeded/ExitPassed=false、真实exitCode/forced/KillRequested及日志原件全部保留，不改0；退出阶段其它异常、执行中卡住、结果不完整均不能豁免。
- 输出：`FunctionalPassed`、`ExitPassed`、`KnownIssueApplied`、`Accepted`与`AcceptanceStatus`分别记录；已知项接受为`PassedWithKnownIssue`。历史h24/v5/纯Unity失败不补seal、不重绑、不改旧结论。原148业务叶及其它强门禁不删、不跳、不减少。
- 当前验证：新policy参数4例先RED（159中3Fail）后159全Passed；进程helper默认严格20例全部保留，新增完成seal/宽限正负总38/38。外层功能验收纯fixture与新2022实际接收尚待结果，本节不提前宣称B01完成。
- 最终规则验证：功能policy缺实现RED0/1→50/50，精确关闭取消Curl42/exit-intent顺序RED→64/64，前段日志混合编码边界RED→69/69；后段未知错误/非法编码仍拒绝，不能白名单放任意error。真实h34缺exit-intent且Curl42早于native shutdown，保留原拒绝，不补标记、不覆写事实。新h35 producer明确记录退出请求，再见最终shutdown路径；仅精确本Run的已知取消诊断可列保留项。
- 独立Review修正：`ses_e7bda1b86ffeDvSLQeS2a1Grsx`发现acceptance早于全部门禁、首次XML SHA未进入后续消费、完成后reload误领养/拒绝及参数大小写。成功记录现仅在source/meta/安装/选集/官方XML/Owned scene全部校验后CreateNew+Flush+Move原子发布；后续消费固定Artifact/ProcessFact/Seal/selection SHA与结果flags，重解不能重基线。新增存储4例RED→最终163/163；policy参数写前规范。Domain Reload只以本进程SessionState中完成seal SHA识别同run已封闭阶段，新进程/错owner/外来seal仍拒绝，不再重注册已完成测试。
- 真实2022验证：新h35同两冻结包裸接收→SDK9.0.3→Owned安装→本机Prepare，实际发现Edit161/Play25。`h35-discovery1` API已销毁、完整输出与seal闭合，真实PostCompletionExitStalled/-1/forced/succeeded=false，120秒宽限后按`PassedWithKnownIssue`独立接受；不重写native事实。`h35-edit-functional1`161/161、`h35-play-functional1`25/25，均无失败/skip/inconclusive且两进程实际exit0、无强退。
- 正常退出分支不使用例外：Edit原日志在native关闭后含本地化TcpMessagingSession ERROR_OPERATION_ABORTED/995及混合编码，Known退出诊断扫描曾拒绝，原失败记录保留；未据此改变测试/日志/结果。原官方XML+选集+scene/meta+正常OS退出以更严格Strict功能/进程标准只读复核后首次发布Passed acceptance；Play同样按正常结果Passed。工具对正常observed0/succeeded分支使用Strict结果标准，只有真正PostCompletionExitStalled才启用已知退出例外，不把业务错误豁免。
- 当前闭包：D/`b01-knownexit-workflow1.json`再次核发现已存acceptance/原始fact，两个已存测试acceptance及XML SHA、源/安装/真实选集/生命周期/scene，原148叶完整保留。`B01FunctionalAccepted=true`，`EditorExitDefectFixed=false`，`WholeCompatibilityPlanCompleted=false`。默认Strict、6.3与Player/build/import/callback仍保留正常退出门禁，未进入B02或B08，不把其它2022未测能力签字。
- 使用流程：2022官方发现/测试可显式`-ExitPolicy Known2022EditorExit`；功能完整结束才观察120秒退出宽限。正常退出照常Passed，合格已知退出异常PassedWithKnownIssue；执行期hang、缺/部分/重复选集、断言/TearDown失败、skip、取消、observer/cleanup错误、未知退出/其它崩溃仍失败。旧h24/v5/纯Unity失败无新seal，不能追认或领养。最终双版本交付必须列本已知限制，而不是“原始所有门禁无例外全绿”。
- 最终交付检查：兼容/父接线/存储163/163，进程完成宽限38/38，功能验收69/69（编码边界含正负），本批两C#格式与局部diff/新增脚本及计划空白检查通过；`protection-b01-knownexit-final1.json`为2553→2553/差异0。Source最终仍PID5556、原场景clean、非Play/编译/更新/失败、默认Code，所有本轮临时Unity均结束。未提交/推送/改权限配置/依赖版本，未进入B02；B01功能按新规则完成，整体计划仍有B02-B10待办。

## 6. 最终签字

以下是整体完成条件，不以批次数量、局部绿或候选包存在替代：

- [x] B01-B09 涉及的全部已批准强门禁均闭合；新增/沿用/未覆盖范围有明确指纹和说明，未授权扩展不混入发行声明。
- [x] 唯一独立Review审查实际相关diff/关键调用方，覆盖限制诚实保留；四项意见及八项保留判断逐项裁决，实质问题修复并重验受影响部分，不称逐行穷尽154文件。
- [x] 本批实际文件格式、diff、必要meta/GUID、许可证/notice、秘密排除与正式导出闭包通过；六件归档SHA/长度已逐件核验。
- [x] 最终同一字节默认包在两代全新接收工程无harness首次安装/编译与关键图形业务冒烟通过；各optional实际source-equivalent安装及共存组合单独登记。
- [x] 支持文档只写实际版本、平台、输入包、后端/裁剪/集成结果；目标内容/基座分别重建，不外推Release远端HTTPS、其他平台/补丁或High。
- [x] 最终protected-after2553零差异、用户输入保护、5939冻结复核和包/Player/日志/Hash交接完成；工作区既有与本批改动保持未提交，不push/新分支/worktree/UPM化。

**最终接受：** `B10Completed=true`、`FinalReleaseAccepted=true`，仅指本计划明确批准支持范围与已知例外。正式入口`D/b10-closure-final1.json`绑定本计划、预闭包、Source/保护、Review裁决、复用映射和六件发行清单；旧RED/超时/退出失败、缺失历史输入、其它未验能力和DOTween vendor许可HOLD均继续保留。HEAD仍`dc31794e991ac7fc83b33ff9366a8031a75f8a1b`、分支integration/xf-yl，未提交或推送。

<a id="contracts"></a>

## 附录 A：原始验收合同

<details>
<summary>展开原任务 1-8、工具入口与 Review 重点（范围依据，不是另一套执行队列）</summary>

下列条款保留原批准验收范围，已移除旧进度复选框；是否完成只看前部状态和原件。按 B 批次执行，不按本附录连续推进。所有条款继承第 2 节保护与分层边界。

### 任务 1：环境、输入和隔离预检

**Consumes:** 上述范围和当前工作区。**Produces:** 受保护输入清单、版本清单、RunId、工具能力与允许输出目录。

- 阅读有效 AGENTS / CodingStyle，记录 status/diff/HEAD 与全部既有修改；生成本批修改范围及保护清单，不改用户输入。
- 核对两个 Editor 的真实路径、版本/revision、已运行实例、许可证和接收工程可用性；不对当前工程启动 6.3。
- 验证 PowerShell 7.6、Windows64 Mono / IL2CPP、C++ 和 Windows SDK，SDK 9.0.312、Node、Unity MCP/CLI 及现有 IDE 工具入口；安装文件存在只记为结构事实，空白 Player 构建才提供原生工具链证据。
- 固定接收侧官方包版本；2022.3 Input System 1.14.2，6.3 配套 1.20.1。注册包、manifest/lock 和实际编译图一致后登记，不修改源依赖。
- 建立本批独占短路径和隔离数据身份，记录受控进程/服务、日志与产物入口；MCP/CLI 调用必须先证明 projectPath/version，不改变 Kilo 权限或源连接凭据。

**门槛:** 实施工具与路径可用；缺环境时暂停依赖它的任务，可继续独立的只读盘点，不猜测已安装或通过。

### 任务 2：干净接收与引擎对照

**Consumes:** 预检清单。**Produces:** 2022.3 / 6.3 独立接收工程、初始导入结果和引擎场景回归证据。

- 先复现所选工具的版本/依赖/缺导出根拒绝，再按工具表最小参数化 Unity 路径、预期引擎版本、Editor 配套依赖和本批拥有根。验证错误版本、错误拥有者、已占用/链接路径仍拒绝；旧冻结回执不改写，不能仅取消校验来让工具运行。
- 使用现有 `ExportUnityPackage.Export(absolutePackagePath)` 导出候选默认包，不调用会保存全部资产的交互菜单。记录包及关键 DLL/hash/GUID；首次安装只准备 UGUI、Json.NET 和随包 BouncyCastle，不带 XF、MCP、测试 harness 或商业插件补依赖。
- 在两个干净工程导入同一候选包，读取实际 Unity 编译结果和程序集引用。6.3 编译阻断交任务 3 最小修复后重新导出，不在接收端独立维护产品代码。
- 在本批自有验收输入中建立不调用 YL 的最小双场景对照：Windows64 Mono / IL2CPP 各完成 20 轮异步往返及卸载，设置外部进程超时并保存最后进度、版本和日志。
- 再以 YL SceneController / Resource 链做对应有界回归，比较纯 Unity 与框架路径。仅凭一次超时不能认定引擎根因；能复现阻断则保留最小输入和对照，暂停相关强门禁。
- 初次导入证明保留后才按需加入测试包与源码测试夹具；默认包/Stage9 Player 载体保持无 MCP/XF，用现有 batch/executeMethod 驱动。若需 MCP，另用明确的工具化测试载体；安装和带 harness 的结果分别记录。

**门槛:** 安装依赖真实闭合；编译失败、引擎阻断和测试失败分类清楚。纯 Unity 对照不通过时不能宣称完整 6.3 适配完成。

### 任务 3：主体编译、Editor 与资产兼容

**Files:** `Runtime/**`；`Editor/**`；对应 `Tests/EditMode`、`Tests/PlayMode`；只修改实际故障涉及的文件。

- 从 Unity 首个有效编译/导入错误追踪定义、调用和包源码；优先检查既有 Physics2D/Rigidbody、TreeView、GraphView、序列化 Attribute 和预编译依赖分支，不根据废弃 API 搜索结果预先批量修改。
- 行为缺陷先补能复现的回归；纯 API 编译差异保留真实失败诊断，再做最小条件编译/适配。替换对象查找时保留 inactive 和必要排序语义。
- 验证编辑器窗口、Inspector、列表重排、Undo/Redo、GraphView、代码生成、默认资源与导出；Runtime 不新增 Editor 依赖。
- 用真实源 Prefab / Variant / ScriptableObject 验证重导入、引用、字段、生成输出及消费者。6.3 自动重序列化留在接收工程，不批量覆盖源资产。
- 两代引擎运行受影响测试，覆盖重复 Play、Domain Reload 开关、场景切换和销毁；只修改自身设置并恢复，记录新错误与既有无关警告。

**门槛:** 双版本实际编译成功，必要回归通过，API/程序集/资产身份未漂移。

### 任务 4：完整测试、UI 输入与渲染冒烟

**Files:** `Runtime/Core/UI/Input/{InputSystemBridge,UIPointer}.cs`、`PanelManager.Input.cs`；现有 Core/Foundation/Systems/Utilities 测试及夹具；只有必要时改相应实现。

- 按 asmdef/asmref 建立主体 EditMode / PlayMode 的预期发现清单，按编译依赖闭包安装测试树和必要夹具，不只拷单个 asmdef。在两代引擎运行完整适用测试；源宿主专用测试保留原宿主合同，不在无 XF/资产的 receiver 删断言冒跑，也不因此获准在受保护 Source 执行。
- Legacy 不装 Input System 的最小安装另行验证；装包后验证 Legacy / New / Both，切换后按 Unity 要求重启，确认实际宏和模块，不只改设置文件。
- 在 1.14.2 与 6.3 配套包分别验证标准控件、模态阻断、捕获取消、拖拽中关闭/销毁、设备移除、共享 actions、外部 EventSystem、多根及重新打开；未知布局继续明确拒绝，不静默回退 Legacy。
- 在真实 Windows64 Mono / IL2CPP Player 重复关键输入行为，检查有针对性的元数据保留；不直接全 Preserve 引擎程序集或删除捕获逻辑。
- 对 Foundation/Core、Action/Audio、HTTP/Save、领域系统和物理工具完成适用回归；Domain Reload 开/关各连续进退 Play 至少两轮。音频证据须有可推进的 DSP，不能用无声 headless 代替。
- 独立 6.3 URP 工程使用该 Editor 配套 URP，冒烟验证代表性 UI、屏幕过渡和资源显示；与内建管线分开记录，不把 URP 设为主体依赖。

**门槛:** 预期测试实际发现并完成；零失败、无非预期跳过；关键 UI 行为有 native 证据，渲染异常和环境限制如实记录。

### 任务 5：Resource 与双后端 Player

**Files:** `Runtime/Systems/Resource/Updates/ResourceRemoteSource.cs`、`Content/CngSegmentAead.cs`、必要 Resource Editor/测试和现有 `Tests/Tools` 验收工具。`ValidateSnapshotPlatform` 的内容版本匹配合同保持。

- 在原有门禁入口加入精确候选环境及拒绝边界回归，保留 2022.3、Windows/x64/后端条件；不改成接受任意 6000.x 或任意平台。候选通过本任务前不得作为已验证支持发布。
- 每代引擎从当前源码正式构建自己的 Bundle/Catalog/内置部署与远端发布，使用新身份和输出；验证资产、实例、Scene 租约与卸载，并确认错误引擎/平台/兼容号输入被拒绝。
- 复用现有 Resource Player 工具验证 HTTPS 信任、签名/hash、下载/取消/失败恢复、Check/Prepare/Activate、加密与 native ABI/释放。保留正常证书检查，不临时信任任意证书，不接管旧 journal/缓存。
- Development Mono / IL2CPP 的 HTTPS 用本批隔离回环服务及既有精确 pin 夹具；检查错误证书/签名等拒绝路径。Release 不启用 Development 信任表面，不修改系统证书信任，也不据 Development 结果宣称 Release HTTPS 已验。
- 6.3 必须完成 Mono Development、IL2CPP Development，以及 IL2CPP 非 Development/Low stripping 的本地资源、加密和代表性运行；2022.3 重验受影响 native 路径，未受影响证据可按明确指纹复用。Mono Release、Release HTTPS 和 High stripping 不在本轮强门禁，不扩写支持声明。
- 结果分别记录 Unity 编译、构建、实际运行、后端/Development/裁剪、图形/音频上下文和产物哈希；只生成 exe、EditorSimulation 或返回码为零均不能单独算运行通过。

**门槛:** 资源及更新/加密的目标组合有真实执行证据；未通过项保持阻塞，旧内容和冻结制品不变。

### 任务 6：Analyzer 与 IDE 补验

**Files:** `Tools/YLFramework.Analyzers/**`、`Runtime/Core/Analyzers`、`Editor/Core/Analyzers` 及原有 Analyzer 测试；没有实际兼容问题时不重写 Analyzer。

- 先使用原有构建/部署入口核对唯一正式 DLL、meta、构建收据；保持 Roslyn 3.8.0 / netstandard2.0 / C# 9，不因 Unity 升级顺便升级 SDK 或 Roslyn。
- 在 6.3 接收端完成真实正反诊断、asmdef/预定义程序集作用域、ruleset Include、None/Error、pragma、自检与 Error 恢复；重启后重验必要配置。
- 正式生成/重生成 VS 与 Rider 项目，验证诊断和规则读取；沿用既有 IDE 验证方法，不改用户默认编辑器或全局策略，不能用生成 csproj 或 IDE 无红线替代结果。
- 检查 Mono / IL2CPP Player 输出没有 Analyzer、Roslyn、Workspaces、Editor 或测试宿主依赖。若本任务改正式 DLL/规则，再复跑对应 SDK 和 2022.3 回归；否则复用可核对的有效证据。

**门槛:** 补齐此前延期的 6.3 接收与 IDE 证据；不存在仅凭目标矩阵宣称已验的项目。

### 任务 7：全部可选集成

**Files:** `Integrations/{DOTween,Excel,DomainExcel,HybridCLR}/**`、对应独立测试/样例，以及确需参数化的现有 `Tools/HybridCLR` / 验收工具；不修改 PackageCache。

- 在已证明主体独立性的接收工程中逐项选装，记录每项包/DLL 来源、宏、编译与测试发现；全部安装后再做共存回归，不让缺宏导致集成悄悄消失。
- DOTween 运行两个独立测试程序集和真实动作生命周期；Excel / DomainExcel 验证正式导出、预览/取消/提交、旧表头及不部分写入，保留依赖和许可证。按用户明确选择沿用现有 destructive 持久提交合同，不新增或声称 Excel 提交后 Undo；领域作者窗口已支持的 Undo 单独验证。
- HybridCLR 保持 9.0.3、指定 commit、官方设置权威和程序集约束，使用官方 Installer 在接收工程生成 6000.3 对应本地工具链；分别核对包标记、版本表 tags 与实际工具链，不手改版本文件。
- 相关程序集编译后调用现有 `HybridCLRTestFixtures.Prepare()`，再运行 loader/Module EditMode / PlayMode，现机生成 bytes/manifest；不复用旧会话快照。
- 使用正式 `BuildBasePlayer` / `PublishPatch` 建立新的 6.3 IL2CPP 基座、metadata 和补丁，验证 Module、热更 MonoBehaviour、泛型/async、资源屏障、关闭/重启，以及同一 Player hash 的补丁执行；Development 验证 HTTPS 更新，Release/Low 用可信本地发布快照验证原生热更，分开记录。
- 保留坏签名/hash/缺 DLL/不匹配 Unity、超出 AOT/native 能力与活动版本冲突等拒绝路径；确认旧基座/发布哈希不变。复验受本批修改影响的 2022.3 集成能力，不据模拟模式推断原生成功。

**门槛:** 四项集成分别完成适用测试和必要真实行为；任何一项阻塞则“全能力适配”未完成，可继续独立项但不能整体签字。

### 任务 8：最终发行包、回归与交接

**Files:** 仅增量维护 `Documentation/README.md`、`Architecture.md`、受影响模块页、`Integrations/HybridCLR/README.md` 和本计划。历史记录不改写成当前通过。

- 独立只读 Review 检查真实 diff、所有权、异常/取消、版本守卫、序列化身份、依赖方向、测试发现、可读性与冗余；只修本批实质问题并重验受影响部分。
- 用现有格式工具对本批文件执行 Check，复核 diff、必要 meta/GUID、依赖与秘密排除；不全库格式化、不清理旧资产或用户改动。
- 从最终源码正式导出一个默认包，按现有政策另行导出可选集成；审计 payload/hash/许可证与禁止依赖，不为两个 Unity 各维护一份不同主体源码包。
- 最终同一字节默认包在两代新鲜接收工程完成无额外 harness 的首次安装/编译与关键冒烟；最终发生的实质修改重验受影响门禁。可选包各自记录安装及验收组合。
- 文档只声明实际验证的引擎补丁、平台、输入包、后端/裁剪和集成能力；说明内容与基座须按目标重建、未覆盖其他平台/补丁/High stripping。
- 交付包/Player/日志入口、版本与哈希、通过/阻塞/未验证清单及工作区剩余改动；不提交、不推送、不自动进入 UPM 化。

### 工具入口与复用边界

路径相对仓库根；省略框架前缀的路径均位于 `Assets/YLFramework/`。只复用本批明确支持的操作，不把旧固定 root/PID/HEAD/Hash/回执的入口直接套新身份。

| 用途           | 入口 / 约束                                                                                                                                                                                                                                  |
| -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 默认包         | `Editor/Distribution/ExportUnityPackage.cs` 的 `Export(absolutePackagePath)`；六目录加 LICENSE.md、AGENTS.md，不扩导出根补依赖，不用保存全部资产的交互入口                                                                                   |
| HybridCLR 包   | `Integrations/HybridCLR/Editor/HybridCLRIntegrationExporter.cs` 的 `Export(path, includeSample:true)`；显式路径、无递归依赖，不带 SDK/dnlib/Tests/生成DLL/cache/baseline                                                                     |
| Stage9         | `Tests/Tools/Invoke-Stage9Delivery.ps1`、`Stage9DeliveryEditor.cs.txt`；显式版本/UGUI/owner；测试发现引用 `-ProofRun`，旧 PublishTools/Finalize 冻结身份不直接复用                                                                           |
| Resource       | `Tests/Tools/{Prepare,Invoke}-ResourcePlayerAcceptance.ps1`、`ResourcePlayerAcceptance{Build,}.cs.txt`、Local/Remote runner；本批严格checker为 `Test-ResourceCompatibilityEvidence.ps1` / Guards，不改历史固定checker                        |
| HybridCLR      | `Tools/HybridCLR/Invoke-HybridCLRUnity.ps1`、`HybridCLRReceptionBootstrap.cs.txt`、`Run-HybridCLRPlayer.ps1`、`Run-HybridCLRReceiverTests.ps1`；schema2 严格归属，测试用 `-DiscoveryRun`；旧 Freeze/Audit/Sync/Relocate 未全部适配，不能冒用 |
| Analyzer / IDE | `Tools/YLFramework.Analyzers/build.ps1` / `-Deploy`，及 `Tests/Tools/*OwnedVs*` / `*OwnedRider*`；不手改DLL或生成项目，不切默认IDE，协议/编译图不替代原生诊断                                                                                |
| 格式           | `Tools/CodeStyle/Format-YL.ps1 -Mode Check -Paths <空格分隔明确路径>`；不全库格式化，不把 `.cs.txt` 语法检查称为该格式器通过                                                                                                                 |

Behavior/Domain/Data 的旧 MainAcceptance、Equipment Bind/冻结审计只借鉴断言，不移植整个旧工作流、不冒用历史身份，也不为本批重写通用验收平台。

### Review Focus

- 宏/asmdef 使测试或集成消失：B02/B04/B09 核实际编译、发现与精确叶集合，零测试/非预期跳过不能绿。
- 输入布局、共享 actions、生命周期与裁剪变化：B06 核取消/误点/释放、New/Both、外部 EventSystem、关闭/销毁与真实 IL2CPP。
- Domain Reload、重复 Play、场景切换、fake-null 与失效 scope：B01/B02/B05/B09 核合法宿主、明确清理和身份重验，不清他人状态。
- 跨引擎内容、热更 bytes 或旧基座复用：B03/B08/B09 核新归属输出、不匹配拒绝与旧制品不变；不放宽最终 AOT/native 门禁。
- 引擎/退出故障误归因框架：保留纯 Unity 对照、首因/超时/进程终态；不删等待或卸载合同，不擅换补丁。

### 外部依据

- Unity 6.0 累计升级指南：<https://docs.unity.com/en-us/engine/6000.3/manual/upgrade-guides/upgrade-guide-unity6>；结合 6.1 / 6.2 / 6.3 指南检查，不必默认逐个中间版本迁移工程。
- 目标补丁、revision、配套包与已知问题：<https://unity.com/releases/editor/whats-new/6000.3.27f1>。
- 项目依据：`Documentation/{README,Architecture,CodingStyle}.md`、`Modules/{UI,Resource,Attributes}.md`、`Integrations/HybridCLR/README.md` 及旧 Analyzer Plan 的延期记录。官方说明和历史记录均不是本轮已验结果。

</details>

<a id="history"></a>

## 附录 B：历史执行档案

**只读回溯区：** 下列原记录按时间顺序保留正文、命令、Hash、RED 和后续纠正，仅添加阶段分组及折叠。旧“进行中”“下一步”“Goal active”、PID/服务/唤醒信息只代表当时，不能作为当前状态或自动执行指令。当前结论看第 1 节，新批次结果记第 5 节；读取某一历史结论必须同时核其后续纠正。

| 历史段                                  | 内容                                                              |
| --------------------------------------- | ----------------------------------------------------------------- |
| [H01](#h01) / [H02](#h02)               | 环境保护 / 初始导入、纯Unity与YL场景对照                          |
| [H03](#h03) / [H04](#h04) / [H05](#h05) | 测试闭包与发现 / Runner场景前置 / Analyzer双版候选                |
| [H06](#h06) / [H07](#h07) / [H08](#h08) | 普通Editor与退出 / 作者工具 / Help绿色与2022退出RED               |
| [H09](#h09) / [H10](#h10)               | Resource查询/提交 / native140、成本、发布及checker                |
| [H11](#h11) / [H12](#h12)               | Analyzer与VS / Rider协议及原生缺口                                |
| [H13](#h13) / [H14](#h14)               | 输入、URP、Clipboard与Play分组 / Excel、DomainExcel、DOTween接入  |
| [H15](#h15) / [H16](#h16)               | HybridCLR早期native RED / 持久输入22回归及三集成共存              |
| [H17](#h17) / [H18](#h18) / [H19](#h19) | Development正向 / 三负向与Release AV / async绿色及Session剩余失败 |

- 规划阶段：Q1-Q8 确认产品/平台/版本/输入/宿主/执行/Git/渲染；Q9 曾选择 Release 真实 HTTPS，Q10 明确回退为推荐的 Release 本地/Development 网络分层，未授权远端环境准备。仅静态读取源码、目录、官方资料和只读 Git 状态；未启动 Unity、安装/更新依赖、运行测试/构建、修改产品文件或执行 Git 写操作。

<a id="h01"></a>

<details>
<summary>H01 环境、隔离与保护预检</summary>

- RunId：`U63-20261027-c63a1`。允许外部写入根仅为 `G:\Unity\U63-20261027-c63a1` 与 `C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1`；已用 `Test-Path` 确认两个目标原先不存在、父目录存在，并检查 `G:\` / `G:\Unity` / Temp/kilo 非 ReparsePoint。先创建全新 owner，不领养旧工程；后续子路径仍须检查链接与占用。
- 源 HEAD：`dc31794e991ac7fc83b33ff9366a8031a75f8a1b`，分支 `integration/xf-yl`，暂存区为空。开工已读完整 `git status --short --branch --untracked-files=all`、`git diff` 与 cached stat；未执行任何 Git 写操作。
- 已执行 `pwsh -NoLogo -NoProfile -NonInteractive -File Assets/YLFramework/Tests/Tools/Invoke-Stage9Delivery.ps1 -DeliveryRoot G:\Unity\U63-20261027-c63a1 -Operation Initialize -Repository G:\Unity\Projects\CGJ2024`，正常退出；已回读 `source-baseline.json`，完整既有 status/worktree 清单与 HEAD 相符，`protected-before.json` 保存该工具原有保护范围的文件指纹。本批预期仓库修改仅限本 Plan、计划列出的必要工具/回归及由实际诊断触发的正式 YL 源码；不改上述保护输入。此时尚无新接收工程或 Player。
- 已有受保护差异：旧 XF Plan 的 T2 回执追加；`ProjectSettings.asset` Android 图标槽位。未跟踪保护输入为 `Tests/Fixtures/Extensions/Data.meta` 与其 `Localization.meta`、完整 `Tests/Fixtures/Release82b-9ba23fdf99c64068a792fae5796efa5a` 及其 meta、`Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta`、`Assets/YLFrameworkData` 及 meta、`ProjectSettings/HybridCLRSettings.asset`；本 Plan 原为未跟踪。全部保留，不纳入产品修改。
- 关键保护 SHA256：旧 XF Plan `44ea9193580c55203491a7ac85211f23ffe3e95b549868a03f8ebd9cd85043c0`；ProjectSettings `52836f9870ec4204d26e8421bc126d40682051f7cf5f2a5c5a11e18c4a446ca4`；ProjectVersion `b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389`；manifest `e37dcdeb611a7084c51e4ce168b6265d0cf1cb9294660db79607de4f56548835`；lock `e7d78af3a44e101ff940b66f4fbbacd025cf5f9266dfc24596499070e93daef9`；HybridCLRSettings `734bffe58fdb01c3d6a543ccac12fc26cbdd50d4d9631794173a9aafa296a2771`。
- Editor 文件版本证据：`C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe` 为 `2022.3.62f3c1_1623fc0bbb97`；`G:\Unity Editor\6000.3.27f1\Editor\Unity.exe` 为 `6000.3.27f1_672d562a42a7`。两边 Windows64 Mono/IL2CPP development/nondevelopment variation 均存在；许可证和空白 Player 构建尚未验证。
- MCP 已只读核实连接源 projectPath/version 与上述 2022.3 一致，PID `5556`；无 Play、无编译、无 scriptCompilationFailed，当前 `Assets/Scenes/YLFrameworkDev.unity` 不脏。未启动源工程第二实例、未修改或保存场景。进程证据后续只记录 PID/项目/可执行路径，不复制 Hub 的凭据参数。
- 实际工具探测：PowerShell `7.6.9`；SDK `9.0.312` 已安装（另有 9.0.301/10.0.102/10.0.402）；Node `v24.19.0`；Unity MCP CLI `1.0.0`。`vswhere -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 返回完整的 VS2022 `17.14.37027.9` 及 VS2026/BuildTools `18.11.12315.225`；Windows SDK 目录含 `10.0.22621.0` / `10.0.26100.0`。此项是安装探测，不是原生构建成功。
- 官方 6000.3.27f1 release notes 与本地 Editor 配套 tgz 均确认 Input System `1.20.1`，Rider `3.1.0`、Visual Studio `2.0.26`；UGUI/测试/URP 的实际解析仍待接收工程 lock 核对。Json.NET 保持合同的 `3.2.1`，不采用本地推荐 tgz 的 `3.2.2`。Context7 无精确 6000.3 版本，本轮版本取证采用对应补丁官网和安装内容，不拿通用文档替代。
- Pre-flight：任务 1 的版本/隔离身份由任务 2 实际创建、解析及构建闭环补齐；任务 2 的默认首次导入不含 harness，任务 4 的测试包与依赖闭包仅在首次导入证据保留后加入。任务 5 的 Development 网络夹具与 Stage9 非 Development probe 分开，任务 7 的引擎内容/基座不复用任务 2 的历史制品。未发现需改产品合同的任务接口冲突。
- Ruling：遵循本 Plan 的当前分支、唯一台账和不提交约束，不执行 `executing-plans` 通用流程中的建 worktree、另建 SDD ledger、任务提交或删除现场；TDD/真实 Unity 诊断及最终独立 Review 仍按本 Plan 执行。若记录不足，成本是后续恢复须重新核实本 Plan 与现场指纹，不以额外过程文件补造通过。

</details>

<a id="h02"></a>

<details>
<summary>H02 初始裸导入与双版本、双后端场景对照</summary>

- 正式候选 1：源 Editor 通过 `ExportUnityPackage.Export(absolutePackagePath)` 导出 `G:\Unity\U63-20261027-c63a1\YLFramework-U63-candidate1.unitypackage`，未调用保存资产的菜单；4,553,056 bytes，SHA256 `677de498b6ee2cebe10659320d50a858e3777a486d5399d4df194011d9604903`。`AuditPackage` 实际通过 1,038 条 payload/meta/GUID；完整指纹见同名 `.files.json` / `.receipt.json`。
- 工具 RED：旧 `Invoke-Stage9Delivery.ps1 -Operation Create ... -ExpectedUnityVersion 6000.3.27f1 -UguiVersion 2.0.0` 实际拒绝未知参数，未启动 Unity。新增 `Test-Stage9DeliveryGuards.ps1` 运行真实入口；`GuardTests-red2/results.json` 为 5/6 失败（缺参数）、缺 AGENTS 根的真实裁剪归档仍被拒绝。首次 red1 的 TarWriter 非可寻址流是测试夹具问题，已先修为 `GetNextEntry(true)`，不计作产品 RED。
- 工具 GREEN：最小加入预期引擎/UGUI 参数、精确 executable revision 检查、接收 owner/ProjectVersion 绑定及进程回执脱敏；`GuardTests-green1/results.json` 实际 6/6 通过，覆盖错误 executable、UGUI、owner、占用、junction、缺导出根，且拒绝路径未生成 Unity log/接收 owner。历史冻结回执与包均未改写。
- 6.3 创建入口：`Invoke-Stage9Delivery.ps1 -DeliveryRoot G:\Unity\U63-20261027-c63a1 -Operation Create -Receiver ReceiverB -Unity "G:\Unity Editor\6000.3.27f1\Editor\Unity.exe" -ExpectedUnityVersion 6000.3.27f1 -UguiVersion 2.0.0 -Package YLFramework-U63-candidate1.unitypackage` 成功正常退出；实际 log 版本为 `6000.3.27f1 (672d562a42a7)`。接收 manifest 为创建后裁剪的最小官方依赖，未复制源 manifest/Library，未安装 harness/XF/MCP/Input System。
- 6.3 导入 RED：同参数 `-Operation Import` 实际 Unity exit 1，完整诊断 `ReceiverB-import-initial.log`；首因是 `CharacterController2D.cs` 909/928/948 的 `CS0103 DISTANCE`、`ResourceLocalSnapshot.cs` 169 的 `CS0619 PhysicMaterial`、`ResourceSystem.Instances.cs` 181/242 的 `CS0172 int/SceneHandle`。UPM 真实 lock 已解析 UGUI `2.0.0` builtin、Json.NET `3.2.1` registry，未以缺依赖解释产品编译错误。
- 最小修复：Unity 6 Physics2D 分支恢复对现有 `Distance` 常量的引用；按引擎局部 alias 选用 3D 物理材质类型；场景桶键显式转换到原合同的 int。保留公共 API、资源版本校验与序列化身份；仅有编译失败诊断，修复后的两代引擎编译/行为回归尚待执行。Context7 对精确 SceneHandle API 未命中，已核对 6000.3 官方 `Scene.handle` / `PhysicsMaterial` API。
- 后续接收使用新的包名和 Receiver，不重绑定已冻结 `ReceiverB` 的候选 1 身份；失败工程和日志保留为 RED 证据。工具 C# harness 的旧 Unity 断言及后续 Player 门禁尚未完成参数化，任务 2 不能整体记为完成。
- 候选 2：`YLFramework-U63-candidate2.unitypackage`，4,553,224 bytes，SHA256 `a565f30204f37ea00975dac1dab0e37b5795cdf3873e70be08a7ecbc9e039886`，仍为 1,038 条正式审计通过。2022.3 源 Editor 重编译无失败，Runtime MVID 已改变；三个修复文件的 `Format-YL.ps1 -Mode Check -Paths ...` 使用 SDK 9.0.312 实际通过。本批路径的 `git diff --check` 通过；全局检查只报告原有受保护 Android 槽位行尾空白，未清理这些用户输入。
- 候选 2 的新 `ReceiverC` 创建成功，但首次导入在 Runtime 成功编译后被 Editor 的两个 `CS0115` 阻断（`ReceiverC-import-initial.log`）：DataAssets / Localization 的 `OnBuildRoot(TreeViewItem, IList<T>)` 未跟随已存在基类的 `UNITY_6000_2_OR_NEWER` 泛型 item 分支。只在这两个文件局部选用 `TreeViewItem<int>`；不改旧版 TreeViewState 序列化字段与公共构造入口。尚待候选 3 的实际 Unity 编译验证，原失败工程保留。
- 候选 3：`YLFramework-U63-candidate3.unitypackage`，4,552,848 bytes，SHA256 `045abfa097ba81aa3e9c7f8c035a832800c2e160d8e08af216109b0b775c9b5d`，正式审计仍为 1,038 条。`ReceiverD`（6000.3/UGUI2）与 `ReceiverA`（2022.3/UGUI1）先创建、再以同一字节包独立 Import，真实 Unity 均正常 exit 0；已回读两份 `*-first-import-initial.json`，`Passed=true`、`HarnessPresent=false`、包哈希一致，Runtime/Editor/两份 Sample DLL 实际生成。两边 Json.NET 实际 lock 都为 3.2.1，仅有最小官方依赖，未装 Input System、测试或任何 XF/MCP。所有五个产品源码修复在 2022.3 重编译及 6.3 干净导入已验证，尚无行为测试/Player 证据。
- 原 C# harness 版本门禁也已 RED：仅在 `ReceiverD` 首次导入证明后安装原夹具，`-Operation Editor -Method Audit -Run harness-red1` 正常编译夹具后因 `Wrong actual Unity state` exit 1，见 `ReceiverD-Audit-harness-red1.log`。现改为绑定接收 owner 的两个精确引擎值；Player probe 仍强制 Windows/x64/非 Development/无 MCP/XF，实际版本与当前引擎生成的 config 一致。Build 的 Company/Product 改为本批 root/receiver 身份，避免使用旧 Stage9 玩家偏好空间。尚待 GREEN。
- Harness GREEN：`ReceiverD-Audit-harness-green1.log` 正常 exit 0，已回读 `ReceiverD-audit-harness-green1.json` 的实际引用与资源审计：`Passed=true`，1,038 条产品记录、7 个 Prefab、0 missing script、0 duplicate GUID，Player 编译引用不含 Editor/XF/MCP/Tests，Analyzer 出现在实际 Bee 输入。Editor-profile Runtime 原有条件编译的 UnityEditor 引用与 Player-profile 分开记录，不把 Editor-profile 的程序集引用当作 Player 输出。未重序列化回源资产。工具守卫最新 `GuardTests-green2/results.json` 仍为 6/6 通过。
- 纯 Unity 对照采用未导入产品的独立 `ReceiverE`（6.3），而非复用带 YL 的工程；否则产品的 RuntimeInitializeOnLoad 即使未被主动调用也可能污染对照。已通过正式 Create 和新 `InstallSceneLoop` 安装仅含 Unity API 的 probe 与独立 Editor builder，无 `Assets/YLFramework` / XF / MCP；编译图与 Player loaded-assembly 均设拒绝检查。保留 Stage9 默认非 Development probe，不把它改作 Development 夹具。
- 新夹具入口：`-Operation Editor -Method BuildSceneLoopMono/BuildSceneLoopIl2Cpp -Run <buildRun>`，`Run-Stage9SceneLoop.ps1 -DeliveryRoot <root> -Receiver <receiver> -Backend Mono/Il2Cpp -BuildRun <buildRun> -Run <run>`。两场景每轮 B Single / A Additive / 显式卸载 B / UnloadUnusedAssets，固定 20 轮，逐操作写最后进度；内部 180 秒与外部 240 秒 watchdog，外部仅终止自身创建 PID 及其子进程，并保留 exit/进度/日志/hash，不把超时记为引擎根因。尚未构建或运行，后续先用 `ReceiverE` / `loop-mono1` 验证。
- 6.3 纯 Unity Mono 实证：`ReceiverE` / `BuildSceneLoopMono -Run loop-mono1` 实际成功构建；再用 `Run-Stage9SceneLoop.ps1 -Receiver ReceiverE -Backend Mono -BuildRun loop-mono1 -Run loop-mono1` 运行，已回读 `Runs/ReceiverE-loop-Mono-loop-mono1/{exit,verdict}.json`：`Passed=true`、exit 0、未超时，Unity 6000.3.27f1、Mono2x、非 Development、Direct3D12，20 completed rounds / 20 explicit unloads、Errors/LogErrors 均空。BuildGuid `cfb84d40cca3498c8ce361582f728d65`；BuildReport SHA256 `884cde4e65ea28d6e29daca7fcd638faaffacc8de5d3bc4bf1b85b92fe566f4e`；结果 SHA256 `a9de391a37f75b99c6f465e2901d5f431f026dcd46714b5c3ba8cd502e64834d`。只证明此控制输入的 Mono 组合，不推断 IL2CPP、YL、HTTPS、UI 输入或音频。下一项为同工程串行 `BuildSceneLoopIl2Cpp -Run loop-il2cpp1`。
- 本批新增 4 个工具文件的 `.meta` 已由源 Unity 自动生成，未手工指定身份。最新 Git status 仍保留全部原有输入，无源 manifest/lock/ProjectVersion 改动，无暂存/提交。预检余项仍为完整接收包版本、原生 C++ 构建和必要 IDE 入口；任务 1 尚未整体完成。
- 6.3 纯 Unity IL2CPP 实证：`ReceiverE` / `BuildSceneLoopIl2Cpp -Run loop-il2cpp1` 真实构建成功，Low stripping、非 Development；同名 Run 使用 `Run-Stage9SceneLoop.ps1 -Backend Il2Cpp -BuildRun loop-il2cpp1` 正常退出。已回读 `Runs/ReceiverE-loop-Il2Cpp-loop-il2cpp1/verdict.json`：`Passed=true`、exit 0、未超时，Direct3D12、20 completed rounds / 20 explicit unloads、Errors/LogErrors 均空。BuildGuid `c673e33aee89417089c2f052d935fe51`，BuildReport SHA256 `8dfb926377bc9467a9de254bfc5994cd8a7dfea6517f7c96f8c8a917454774aa`，结果 SHA256 `690e6c1f353704aa3877983191411d84723658e5763754c548d7ff5b1f742764`。至此本机 C++/Windows SDK 有真实 6.3 IL2CPP 构建和运行证据；UUM-156125 在此有限控制输入的两个后端未复现，不宣称该引擎所有场景路径无缺陷。
- 2022.3 产品 `ReceiverA` 已在原首次导入证明后安装当前 Stage9 harness，`Editor -Method Audit -Run harness-green1` 正常 exit 0，回执 `ReceiverA-audit-harness-green1.json`。6.3 / 2022.3 的实际产品安装、编译与独立 Audit 均已建立；完整模块测试、2022.3 纯 Unity 双后端对照、YL 对应 20 轮 SceneController/Resource 回归仍未完成。
- 接续顺序：先以 2022.3 创建新的空白 `ReceiverF`（绑定候选 3，但**不 Import 产品**），`InstallSceneLoop` 后分别 Build/Run 两后端；再在产品工程运行/补齐有界 YL 场景链。接收端 Input System/测试/IDE 包的实际版本与完整测试闭包在首次导入隔离门槛之后补齐。不能把工具守卫 6 项或两份引擎对照当作主体 NUnit 全套验收。
- 2022.3 纯 Unity 四组合闭环补齐：`ReceiverF` 未导入产品，Mono / IL2CPP 均由对应引擎真实构建并运行 `loop-mono1` / `loop-il2cpp1`。已回读两份 verdict：均 exit 0、未超时、非 Development、Direct3D11、20 rounds / 20 unloads、Errors/LogErrors 空。Mono BuildGuid `1980930de0a8417e82043cbfc9303c32`，BuildReport SHA256 `5e78df59dfdecd159f1fc3ef7909f06c3ac423f285eb1e35222f8d885d3caf45`；IL2CPP BuildGuid `9d8a18fd48f24d9caf1686675daf4219`，BuildReport SHA256 `6b44f55df977d9ae5bb295067833f84ea59e0c287744507a3b2f1a5c315baba1`。
- YL 场景链夹具 RED：`-Operation Player -Command scene-loop` 原来被 ValidateSet 拒绝。现只扩展既有 Generate/Player：在各引擎独立生成 None 内容的两张 Scene Bundle 与中立内置场景，固定 20 轮 SceneController B Single / Resource A Additive / Resource SceneRecord 显式卸载 B，再回中立场景检查记录清零；保留 CoreSceneResult 不交付卸载权的合同，Probe 只在该命令中 DDOL。外部 240 秒超时仅终止自身 PID 子树，逐操作 Step 保留最后进度，未动产品场景控制器或 CNG 版本门禁。尚待 GREEN。
- `ReceiverD-Generate-scene-loop1.log` 已成功编译扩展后的夹具，但在真实原生 Build 前由现有 `ResourceBuildWorkspace` 拒绝 `PathTooLongException`，失败输入/目录保留。这是已确定的本批路径布局问题，不是引擎卡死、协议不兼容或内容损坏。
- Ruling：不放宽产品的 260 字符守卫、不改稳定 Bundle 名、也不新增允许根；给既有接收工具补单字母安全 leaf（仍拒绝分隔符/路径穿越），在同一根创建全新的 `a`（2022）/`d`（6.3）。实际项目绝对路径由 37 字符缩为 29 字符，以进入既有原生路径预算；代价是重新进行独立首次导入/安装，旧 `ReceiverD` 的失败现场不迁移、不覆盖、不重绑定。
- 短路径 `d` 已重新完成候选 3 的无 harness 首次导入，然后 InstallHarness。`d-Generate-scene-loop1.log` 与 `d-VerifyGeneration-scene-loop1.log` 均实际 exit 0：真实 6.3 None/Encrypted 两内容版本、独立场景 Bundle、自动 Atlas 和随包样例已新生成/部署并按目录摘要回读；没有跨引擎拷贝内容。
- 原 `BuildMono -Run native1` 在 BuildPipeline 返回后枚举/哈希实际输出时失败，见 `d-BuildMono-native1.log`：随包样例 StreamingAssets 目标路径 261 字符，Unity Mono 的 File.OpenRead 抛 DirectoryNotFoundException。该目录/失败日志保留，不认作 Player 验收通过。入口增加构建前只读目标路径预算检查，不放宽文件 API 或产品验证；改用全新一字符 BuildRun `2`，同一最长目标降为 255 字符，尚待重建/运行。
- YL 场景链 GREEN：`d`（6.3）与全新 `a`（2022.3）均先完成候选 3 原包首次导入，再独立 Generate / VerifyGeneration；两个工程各自 `BuildMono/BuildIl2Cpp -Run 2` 实际成功构建。四个 `Player -Command scene-loop -BuildRun 2 -Run yl-loopmono1/yl-loopil2cpp1 -Store <receiver>-loop<backend>1` 均实际正常 exit 0；已回读 `Runs/{a,d}-{Mono,Il2Cpp}-yl-loop*/verdict.json`，Passed/BusinessPassed/IdentityMatched 全真，晚期/退出错误空，20 rounds / 20 explicit unloads / remaining SceneRecord 0。最终原生 Bundle load/unload 40/40，Module 0、原生 Bundle 0、无超时。仅证明该场景/本地 None 资源路径，不声称加密、网络、UI 输入或全套 NUnit 已验。
- 四份 YL BuildReport SHA256：6.3 Mono `ff7c5061b44bbfbaa1ed8c4a7151b937c72a9182fd8560a2153076a0b6618dde`；6.3 IL2CPP `5bbee14dce14142902adcf5ede507673655bdc44b3b7d25e81a60a8cd94a397c`；2022 Mono `7bd52965141df2b1c069585db494202f4453c274ca366f93418a9651554dd3c8`；2022 IL2CPP `25909f03470947e5c24fdc6fc0ac4376276acc684d69a46e7cd760b09ab2233e`。新版本原生序列化内容没有与旧版交换；原失败目录及两个引擎控制工程都保留。
- 工具守卫在全部改动后 `GuardTests-green3` 再次实际 6/6，通过并保留回执；短 leaf 的真实 Create/Import 正例及构建目标路径预算正例均已执行。安装的 6.3 Editor 官方 manifest 明确 Input System `1.20.1` / Test Framework `1.6.0` / Rider `3.1.0` / Visual Studio `2.0.26` 最低配套；Test/Input/IDE 接收侧实际 lock 和编译图仍待独立测试载体核实。
- 测试准备采用新短路径 `t`（2022）/`u`（6.3），不改变产品 Legacy 载体 `a`/`d` 的 manifest 或输入身份；完整 asmdef/asmref、夹具依赖及源宿主专用测试正由只读内部 explore 子任务盘点，不启动额外 Agent Manager 会话、不让子任务运行 Unity 或改文件。
- 任务 1 接收官方包闭环：`t` / `u` 均先有原包首次导入证明，再只修改各自 manifest 添加官方 Test/Input/IDE 包；未写 lock。实际 Editor `Audit -Run packages1` 均 exit 0，已回读两份 lock、`*-audit-packages1.json` 的注册包与编译图。2022.3：Test Framework 1.3.9、NUnit 2.0.3、Input 1.14.2、Rider 3.0.40、VS 2.0.28；6.3：Test Framework 1.6.0 builtin、NUnit 2.0.5 builtin、Input 1.20.1、Rider 3.1.0、VS 2.0.26。两边 Json.NET 3.2.1、UGUI 1/2 原版本不变，Unity.InputSystem / UnityEngine.TestRunner 确在真实编译图中。这里尚未复制/发现主体测试，Audit 不是 NUnit 结果。
- Ruling：2022.3 Test/IDE 包选择现有源 lock 已使用的 1.3.9 / 3.0.40 / 2.0.28，而非把 Editor 最低配套 1.1.33 / 3.0.36 / 2.0.22 当作必须降级；6.3 采用其实际配套版本并由 UPM 解析包括 NUnit 的官方传递依赖。代价是两代引擎的测试基础设施版本不同，发现/执行清单必须分别核实，不能沿用数量或结果。
- IDE 入口预检补齐：`Rider.cmd` 实际解析到用户 Toolbox scripts；已读取 `C:\Users\Kano\AppData\Local\Programs\Rider\product-info.json`，Rider 2026.2.3.1 / build 262.10968.170，native 入口 `bin/rider64.exe`。这只是入口/安装信息，不替代后续 Rider/VS 实际诊断验收。磁盘此时 G 约 171 GB、C 约 490 GB 空闲，未清理任何旧输出。
- 阶段保护校验：实际执行 `Protect -Run checkpoint2` 并回读 `protection-checkpoint2.json`，2,553 项 before/after 完全一致，AllDifferences / AuthorizedDifferences / UnapprovedDifferences 均为空。另重新读取旧 XF Plan、ProjectSettings、ProjectVersion、manifest、lock、HybridCLRSettings 的 SHA256，均与开工指纹相同；未发生脚本输出提示中泛称的任何“default cleanup”。当前仅计划列明的 5 个产品源文件、3 个既有工具及 4 个新工具/其 meta 有本批变更。
- 下一项依赖内部只读任务 `ses_e85b8fc4dffeB9QbjoWRGaxY3O` 的测试闭包清单；当前 `t/u` 已有真实包解析及无主体测试的编译/Audit，尚未复制主体测试、更未运行 NUnit。读取其最终结果后先安装完整依赖闭包、保留源宿主专用测试原断言，再建立真实发现清单；不要在等待期间重复探索同一测试树或把工具 Audit 算作测试通过。

</details>

<a id="h03"></a>

<details>
<summary>H03 测试闭包、官方发现与初轮基线</summary>

- 只读内部清单已返回并核对关键源码：Tests 共 17 asmdef / 14 asmref；主体 NUnit 为 7 个（主 Edit/Play、Core Edit/Play、Foundation Edit/Play、Core.Editor.Tests）。主 EditMode 直接依赖多个嵌套 PlayMode helper；`CoreSerializedPropertyTests` 还通过反射要求 23 个 helper 类型。UI.Acceptance、Behavior 两个验收程序集与 Core.Fixtures.Editor 都不是 NUnit 数量。
- `InstallTests -Run tests1` 已分别向 `t/u` 原路径复制完整 EditMode / PlayMode、Acceptance/Core、Core 的 Editor + YLCoreResourceModule、Audio、CommonSystem、NamespaceMigration、Behavior、Shared、DomainAcceptanceProbe 与 ResourceMultiDirectory 静态输入，保留全部来源 GUID。两份 `*-tests-inputs-tests1.json` 保存逐文件来源/接收 SHA256；未复制旧 Release82b/Workbench、XF、源场景、YLFrameworkData、Tools 历史工作流或可选 Integrations。ResourceMultiDirectory 仅拷原字节静态输入，普通测试会在接收副本下创建新的 Integration-GUID，不运行旧证据入口。
- 新增窄用途 `Tools/Editor/Stage9ReceptionTests.cs`：从真实 CompilationPipeline/TestRunnerApi 记录 7 个主体程序集的叶节点、模式/宏/源码/引用；只允许 owned receiver 修改其 Input 模式和隔离 Company/Product，源宿主入口仅作只读发现，不执行测试/Play/保存。`Source-tests-discovery-source1.json` 已实际生成并回读 Passed=true / MissingTestAssemblies=[]；源 Editor 仍为 2022.3、非 Play、无编译失败。仅为发现证据，不是执行通过。
- 完整 6.3 测试编译 RED 分层：`u-TestsDiscover-list1.log` 为测试 `FilteredTree.OnBuildRoot` 的 CS0115；加入 Item alias 后 `list2` 进一步暴露 legacy TreeView 参数 CS1503 和 SceneHandle/int 隐式数组 CS0826。只改测试/窗口 helper 的 Unity 6.2 泛型 alias、原 int 合同的显式转换，共 3 个测试文件，不删断言；在真实 6.3 `u-TestsDiscover-list3.log` 已编译并正常完成发现。2022.3 源编译与三个测试文件格式检查通过。
- `SyncTestSources` 只同步回执登记的 .cs/asmdef/asmref，先校验全部前驱指纹，再备份/写入变动文件；不更新资产或 meta。首次 `tests2` 在 Tests.meta 的非源码路径校验上拒绝，两个失败 history 为空，无文件变动；已修正为先筛选源文件，`tests3/tests4` 对 t/u 实际成功，保留所有前驱日志与 hash，不重装或重绑定产品候选。
- Ruling：不把硬编码 MB26 profile/PID/sourceTip 的 `DataDomainReloadSessionTests` 误标为可在当前源宿主自动运行。已读其 36-70 行：当前允许身份不匹配其历史断言，不能接管旧 MB26 或改断言；与 DomainMainAcceptance 显式工作流一起在发现表标为 historical-bound，后续按本批自有输入补对应 Domain Reload 行为证据。其他 XF/发行/Equipment 宿主用例标 source-host，Excel 后端用例标 optional-excel，不在无该依赖的接收端制造 skip；这些分类尚不代表已在源宿主或可选集成中执行。
- `t` 的 `TestsDiscover -Run list1`（后台 `bgp_17a6e82af0018PgV231mTxyaNX`）曾在 monitor 120 秒上限后继续运行；2026-10-26 23:08Z 用户提醒后立即检查并用后台工具停止自有进程树。其 Unity PID 24740 已退出，源 Editor PID 5556 保持存活。原日志和发现文件保留，不能把该强制结束标作正常退出。
- 已确认停留阶段不是编译或发现：`t-TestsDiscover-list1.log` 1604 行已写完 Edit=2596 / Play=791，发现 JSON 的 MissingTestAssemblies=[]。Test Framework 1.3.9 的 `TestListJob.cs` 31-32 行先调用用户 callback 再解除 update；将 API 销毁/退出延迟至 delayCall 是生命周期收口尝试，但 `list2` 实证仍停在退出阶段，不能声称该尝试修复了退出问题，也不能归因场景加载/引擎 UUM。
- 已把提醒改为真正的启动器截止时间：`Run-Unity` 带 `UnityTimeoutSeconds`，一般默认 1800 秒，TestsDiscover 强制上限 180 秒；超时仅 Kill 自己启动的 Unity 及其子树，自动写 PID/TimedOut/TimeoutSeconds/exit，不再依赖人工提醒或无限 WaitForExit。使用独立 owned `ReceiverF` 的 1 秒真实进程回归，`ReceiverF-BuildSceneLoopMono-watchdog1.log.exit.json` 已验证 TimedOut=true / exit -1；没有覆盖控制场景或既有构建。
- `t` 的 `list2` 已由新截止逻辑在 180 秒自动终止，回读 exit：PID 30152 / TimedOut=true / exit -1 / EndedUtc 2026-10-26T23:16:18.6298963Z。日志与第二份 JSON 仍有同样完整发现，但退出门禁不绿。已核对当前只剩用户的 Unity Hub（34024）与源 Editor（5556），没有 owned 接收 Editor 残留或待处理 wakeup。
- 接续采用官方 Unity Test Runner 的真实 `-runTests/-testResults` 路径执行，而非反复重试同一自定义 async Exit；先做窄程序集/单方法烟测并强制截止，再按已发现清单扩展。发现完成与测试执行/正常退出分开判定；截至这里主体 NUnit 仍未执行，不能仅靠两份发现 JSON 记为测试通过。
- 官方 Runner 实际烟测闭环：新增的 `Tests` 操作复用既有 owned receiver / package / process 守卫，不带 `-quit` 或 `-nographics`，调用标准 `-runTests/-testPlatform/-assemblyNames/-testFilter/-testResults`。先保存所选真实发现清单，完成后逐项比对 NUnit XML 名称/数量/结果；零项、少项、非 Passed 或 skip 均不能产生绿色 verdict。
- `t/u -Operation Tests -TestMode EditMode -Run smoke1` 对同一 `CoreSerializedPropertyTests.AcceptanceAuthoringFieldsKeepExactFormerNames` 实际各跑 1 项：两边所有 23 个反射 helper 合同通过，官方 Runner 正常 exit 0，XML 与 selection 完全匹配，无跳过；已回读 `t/u-tests-EditMode-smoke1.verdict.json`。这解决本批继续执行的入口问题，不把自定义 `TestsDiscover` 的超时回执改成通过。
- 已分别在 `t/u` 执行 owned `TestsLegacy -Run legacy1` 并正常退出：显式旧输入模式、Company=本批 root 名 / Product=t-Tests/u-Tests，源设置不变。真实发现聚合：源 Both 的 Edit/Play 为 2596/811；t/u Legacy 均 2596/791。每个接收端当前适用 receiver 为 Edit 2506（178 Core.Editor + 76 Core.Edit + 245 Foundation.Edit + 2007 主 Edit）、Play 788（215 Core.Play + 43 Foundation.Play + 530 主 Play）。另外 40 Edit + 3 Play 标 source-host，9 Edit 为 historical-bound，41 Edit 为 optional-excel。所有发现 RunState=Runnable；Source 比 Legacy 多出的 20 个 Core.Play 输入用例将在 New/Both 明确覆盖，不作为丢失程序集。
- 任务 2 当前完成：同字节原包首次导入/程序集审计、4 纯 Unity + 4 YL 有界原生场景对照、后置完整测试设施安装与实际编译/发现均已建立；整体测试/Player/集成仍由后续任务验收。下一项是 `t` Legacy EditMode 2506 项完整运行（官方 Runner、独占新 Run、3600 秒硬截止），完成后按真实 XML 处理，不先删改失败或改发现清单。
- 已启动上述完整基线：`t -Operation Tests -TestMode EditMode -ProofRun list2 -Run legacy-full1 -UnityTimeoutSeconds 3600`，选中 2506 项；后台 ID `bgp_17a8e42fe001kiNvWzr731LV6m`。monitor 到 120 秒时正常仍在运行，实际进程受 3600 秒硬截止控制。由同一后台进程退出事件恢复 Goal，不再加人工提醒或启动并行 Unity；恢复时先取该 ID 的终态，再读 `t-tests-EditMode-legacy-full1.log{.process,.exit}.json` / `.xml`，退出失败也读取真实 XML 处理，不重跑来替代失败证据。
- 完整基线已在 00:02Z 结束，Unity 正常 exit 2、TimedOut=false。原始 XML 实际 2596（2335 passed / 252 failed / 9 skipped），而 selection 为 2506；没有缺项，额外执行了 90 个不适用项。完整失败/差异保存在同名前缀 `.diagnostics.json`，不覆盖原 XML，不宣称基线通过。
- 已核实 Test Framework 1.3.9 `RuntimeTestRunnerFilter`：CLI 的正则 group filter 会命中父 suite，负向前瞻不是排除器。改用框架内置 `!^<exact-leaf>$` / NotFilter，并保留发现→所选→实际 XML 精确比对。`core-scope2` 实证 Expected=Actual=178，全部通过，无 skip/差异；没有删除或弱化断言。
- 样例前置缺口确认：新载体需显式 SpriteAtlasV2 与空 `Assets/StreamingAssets`；已只在 owned helper 配置，不改产品自动创建/全局设置合同。`DomainMainAcceptanceInputTests` 2 项直接反射缺席的历史宿主工具，新增到 source-host 分类；profile 回执记录分类规则，既有发现文件不重写。故新普通 Legacy Edit 适用计数为 2504，另 2 项移交源宿主验证，非测试删除。
- `foundations2` 正确执行 321 项，320 passed / 1 failed（TimerSession 的无脏场景前置）；随后 `timer-session2` 新进程只跑该完整 fixture，真实 1/1 passed、正常退出。说明套件间场景脏状态污染，后续真实两次 Play/需要无脏场景的 fixture 独立执行，不放宽保护断言，也不把污染红灯抹掉。
- Resource 预检/归因：只读子任务核实 BuiltInDeployment/Startup 的 DirectoryNotFound 是缺 `StreamingAssets` 父目录；Atlas 大部分失败是 Disabled SpritePacker；TLS 类需独占 PFX；Cost/PublicationRetry 是显式历史证据入口，不能按普通全量调用。ResourceUpdate 的 6 项是 helper 未等待异步终态，1 项重复 Prepare 的 checkpoint 需核对现行合同，尚未修改。保留所有现有断言。
- 已复用 `Prepare-ResourcePlayerAcceptance.ps1`，只增加验证过的 DeliveryRoot 子目录支持，Evidence 的 Temp 范围、fresh/no-adopt/链接检查均保留。新建 `C:\Users\Kano\AppData\Local\Temp\kilo\U63-20261027-c63a1` 与 `G:\Unity\U63-20261027-c63a1\r`，真实 Prepare 成功（1847 formal files）；3 天 PFX 不安装信任。Tests 启动器只给此运行的子进程注入该已核实归属/有效期的测试 PFX，未污染源/系统环境，未启动网络服务。
- 唯一确认的产品指纹缺陷：ProjectFileHash 只识别旧字段名，当前小写 YAML 未归一化；原 2 个回归已在完整 XML 红灯。最小支持 `sourceDirectories|SourceDirectories` / `guid|Guid` / `addressPrefix|AddressPrefix` 精确两种历史/当前布局，不泛化任意大小写，未知布局仍原字节 fallback。源码编译/格式通过，候选 4（4,552,803 bytes，SHA256 `752a7b29786d83608331b0be174f009791b6dfbbbdba7658825142b625c26236`）正式审计 1038 条通过，并在全新 `v`（2022）/`w`（6.3）真实无 harness 首次导入/编译均成功。旧 a/d/t/u 和候选 3 不重绑定、不覆盖；指纹行为 GREEN 仍待实际结果。
- 当前恢复风险：尝试用源 Editor 的 TestRunnerApi `runSynchronously=true` 只执行纯同步指纹回归（检查源非 Play/非编译/场景不脏，不修改源设置），MCP `Request timed out`。预期 `Source-fingerprint-sync1.json` 尚未出现；PID 5556 存活但 Responding=false。Editor.log 末尾可见本次新建 Release82b-4b5cd85c... 已清理，且恢复了 `Assets/Scenes/YLFrameworkDev.unity`，但无完整结果回执，不能计为通过。不得 Kill/重启该用户 Editor，不追加 MCP/Source Unity 操作或同时启动其他有状态 Unity 验证；短恢复窗口后重新判断。候选 v/w 尚未加入测试包/完整测试，r 尚未启动 Unity。
- 06:06Z 实际恢复复核：OS 检查源 PID 5556 Responding=true，未重启/终止；`Source-fingerprint-sync1.json` 已完整写出，真实 NUnit Passed=1 / Failed=0 / Skipped=0，精确叶节点为目录重排/未使用来源/前缀变更指纹合同。随后一次只读 MCP 确认相同 project/version/PID、非 Play、非编译、无编译失败、YLFrameworkDev 原场景且 isDirty=false。此前是调用超时/暂时未响应，不能继续写成永久挂起；源同步 NUnit 路径仍停用，后续都在 owned receiver。
- `v/w` 在已保存的候选 4 无 harness 导入证明之后，才添加与 t/u 相同的固定官方 Test/Input/IDE 依赖，并安装完整源测试/helper/fixture。未覆盖或重绑定旧载体。`w -Editor TestsLegacy -Run legacy1` 实际解析/编译并保存 SpriteAtlasV2 / StreamingAssets / 输入隔离；`w -TestsDiscover -Run list1` 实际正常完成新测试发现。
- 当前唯一有状态验证是 `w -Operation Tests -TestMode EditMode -TestFilter ^YLFramework.Tests.ResourceBuild.ResourceMultiDirectoryFreezeTests. -ProfileRun legacy1 -ProofRun list1 -Run fingerprint1 -Package candidate4 -UnityTimeoutSeconds 900`。后台 ID `bgp_17bfd6129001P04m4mJQArqJST`，120 秒 monitor 上限时仍运行；硬截止由启动器控制，等待该同一进程终态再读 XML，不启动并行 Unity，不让用户再次手动提醒。
- `w fingerprint1` 已正常测试失败退出：17 项 15 passed / 2 failed。目录重排磁盘指纹与 post-build reorder 已转绿；剩余是首次空 SceneSetup 恢复及 Atlas 内存 CanonicalDefinition 仍添加旧字段名。已分别最小修复测试夹具空 setup 分支、用 nameof 当前字段替换内存 JSON 中的旧字段名，未改资产字段/GUID/协议或撤销外部修改保护。
- 候选 5：4,553,387 bytes，SHA256 `db763500cb11430ad2d498fc9f1e222174fd73ebf4bdebeaa465f3b20f4d25cd`，正式 1038 条审计通过；全新 `v2`（2022）/`w2`（6.3）无 harness 首次导入/编译均通过，再独立加入固定官方测试包/完整夹具。`w2 fingerprint1` 16/17，最后一项为 Unity 首次 NewScene 自动创建 SceneTemplateSettings 导致保护快照红灯；owned profile 增加事先初始化、不忽略该文件。`w2 fingerprint2` 实际 **17/17 passed**，正常 exit 0、零 skip、精确 selection/XML 集合一致。对应原 RED 全部保留。
- `w2 core-foundation1` 实际 498 项，497 passed / 1 failed（Invalidated 弱引用 GC 存活）。新进程 `timer-gc1` 精确同叶实际 1/1 passed；未为此改 Timer 生产代码或断言。`timer-session1` 在 w2 独立完整真实两次 Play/DisableDomainReload fixture 实际 1/1 passed，正常退出。组合跑法的 GC/场景污染红灯与隔离结果分别记录，不能说原组合跑法全绿。
- `w2 legacy-play1` 精确执行适用 788 项，732 passed / 56 failed、无 skip。55 项为其他 fixture 留下公共 CoroutineStarter/UpdateManager 的拒绝接管前置；随后独立进程串行执行 `iso-observation1`(6)、`iso-starter1`(15)、`iso-update1`(6)、`iso-timerdriver1`(1)、`iso-global1`(7)、`iso-http1`(20)，六份官方 Runner verdict 均 Passed=true / 零 skip；没有清除未知宿主或改 reset 合同。剩下 1 项是克隆载体缺已有 Map SortingLayer：只给 owned w2 的 TagManager 添加独立 Map 层，重导入后 `sortinglayer1` 实际 1/1 passed；源 TagManager 未改。原混跑失败未擦除，788 项现有通过证据来自原混跑与明示隔离进程，不声称单次 788 全绿。
- `w2 TestsBoth -Run both1` 已持久化并重启后真实发现，新增 20 个输入叶。`core-both1` 执行 235 项，217 passed / 18 failed：均为 CoreDirectView 原生 mouse/touch/scroll/submit/同设备新手势链，部分先验输入本身未发生。只读内部任务 `ses_e83da5f7ffferfTK91Su4ZWHeA` 正在比对 Input System 1.14/1.20 实际布局、actions 与 batch/focus，不预设反射是根因；当前尚未修改 InputSystemBridge。
- `w2 tls-download1` 使用本批有效 PFX 通过完整 ResourceDownloadTests 官方执行，正常退出、selection/XML 精确匹配、无 skip；只证明 Editor HTTPS 下载测试，不冒充 Development Player 网络或 Release 加密验收。
- ResourceUpdate 测试 helper 已按现行异步合同修正：删除同帧 Check/Prepare 的 Take，Native/Query 调用点显式 Wait 两个请求后才 TakeResult；用块作用域避免请求对象/闭包跨实际 Domain Reload，保留全部原断言、Busy 与回滚检查，不改生产异步语义。源码编译/格式通过，w2 `tests3` 已按前驱指纹同步；六项既有 Pending 红灯还需实际 GREEN，重复 Prepare checkpoint 合同项未擅改。
- `w2 update-await1/2` 实际均在 OneTimeSetUp 退出，6/6 未进入业务：ResourceSessionTests.EnsureRestorableScenes 在 runner 的无路径场景见到 2 个根对象，拒绝接管。owned profile 新建并保存独占空 Bootstrap（不自动保存用户/原场景）后仍同样失败，不能继续猜改/放宽该断言；暂记环境/runner 前置阻塞，异步 helper 修复尚无 GREEN。原 XML/日志保留，后续须用合法已保存场景的官方 Runner 路径解决。
- Input 只读结论已核对实际包源码：1.14/1.20 PointerModel / ButtonState / InlinedArray 字段及值类型逐层写回完全一致；18 个红灯在原生事件入 action 前，不是已证实的桥问题。仅在 CoreDirectView 测试 SetUp 克隆 settings，设置官方夹具采用的 AllDeviceInputAlwaysGoesToGameView / IgnoreFocus / runInBackground，并于 TearDown 恢复原对象/值。首个 `input-focus1` 暴露包 setter 会销毁精确 HideAndDontSave 的原临时 settings；按 InputManager 190-192 行在替换前暂加 HideInInspector、恢复时还原精确 flags，`input-focus2` 原 native Color 手势/关闭断言实际通过。InputSystemBridge 生产代码未改。
- `w2 core-both2` 实际 **235/235 passed**、正常 exit 0、零 skip、selection/XML 精确匹配，覆盖原 18 个 native mouse/touch/scrollbar/InputField/submit/modal/移除设备/同设备新手势红灯；克隆设置与原资产身份恢复仍在原断言保护下。随后 `TestsNew -Run new1` 持久化/重启并真实发现，`core-new1` 实际 **229/229 passed**、零 skip/差异。New/Both 的宏/发现分别取证；仍未把 Editor PlayMode 误作 Player、手动 UI.Acceptance 或 URP 验收。
- Analyzer 独立补验：原 `Tools/YLFramework.Analyzers/build.ps1`（不 Deploy）在 SDK 9.0.312 locked restore/build/完整测试实际 **181/181 passed**、零 warning/error/skip；真实 Unity6 `w2 analyzer1` 官方测试 **77/77 passed**、零 skip/差异。当前 SDK 输出 SHA256 `c0d20af752831f62a46a51794210e05e71a21410156655ca40ac6944fb831f79` 与随包正式 DLL `4c621b4eea949b5db5c192bad6373723e4c944a421170eac88dedc48c8e37db3` 不同，未擅自 Deploy 或宣称唯一产物闭环；需查明差异并按原入口核对，VS/Rider 实际 IDE 诊断尚未补齐。
- 6.3 IL2CPP 加密 RED 已在旧但独占的 `d` / candidate3 / BuildRun2 真实非 Development/Low Player 复现，Run=`encrypted63-red1`，身份匹配、exit 1；具体首因为现有 CNG/runtime 版本门禁抛 PlatformNotSupportedException，不是内容 UnityFS/hash/TLS 损坏。回执 `Runs/d-Il2Cpp-encrypted63-red1/verdict.json` 原样保留，生产后端支持守卫尚未扩写，候选5仍不能声明6.3原生加密支持。
- 接续：优先对 latest `v2` 补同版本 Test/Legacy/Both 基线与当前最小修复回归；6.3 继续合法配置下的源会话 fixture/ResourceUpdate 六项、其余 EditMode 适用叶、URP、原生输入与 Development/Release 分层。Task6 的 SDK/DLL 不一致、IDE 与所有可选集成均未完成。无 owned Unity 正在运行，不重复原生八组合或抹掉历史红灯；唯一进度台账继续本文件。

</details>

<a id="h04"></a>

<details>
<summary>H04 官方 Runner 场景前置与 Resource 等待合同</summary>

- 前轮已执行原 `Tools/YLFramework.Analyzers/build.ps1 -Deploy`；本次重新回读 `Temp/YLFramework.Analyzers/build-info.json`、TRX 与正式 DLL，确认 SDK 9.0.312、181/181 passed、无 failed/error/skip。正式 DLL 与 SDK 输出 SHA256 均为 `c0d20af752831f62a46a51794210e05e71a21410156655ca40ac6944fb831f79`；meta SHA256 仍为 `bffcae863d930167f4578bb9e8f8ea78a92bb1d2c3b93722fbcaf45f22f48678`。未改 Analyzer 逻辑、Roslyn/C# 版本或 GUID。原 DLL 与重建 DLL 的字节差异具体来源仍未确定，部署统一不等于该原因已解释。
- candidate5 与全部既有 receiver 仍含旧 DLL；`w2 analyzer1` 的 77 项不是新部署 DLL 的接收端证明。新 DLL 必须重新正式导出候选并在全新双版本工程做无 harness 首次导入和相关回归，不能重绑定旧候选/载体身份。
- 已重新读取 Git status，分支仍为 `integration/xf-yl`，已有用户输入均保留。进程检查只有源 Editor PID5556、其两个 AssetImportWorker 和 Hub；源 Editor Responding=true，没有 owned receiver Unity 残留。w2 当前配置为 New-only `new1`，后续不得沿用旧 Both profile 作为当前身份。
- 只读调查 `ses_e8375c612ffed1cv2hXAPsnRrM` 已完成：UTF1.6.0 的 `TaskList` 在 EditMode 创建 `DefaultGameObjects/Single` bootstrap，然后才调用 PrebuildSetup 与 RunStarted；两根是官方默认 camera/light，且无路径。保存一个场景到上一个 CLI 进程不足以改变这个顺序。既有 `EnsureRestorableScenes` 不放宽，旧两次 OneTimeSetUp 红灯保留。
- Ruling：使用现有 owned 测试 harness 的官方 `RunStarted` 前置，并通过本次子进程 CLI/profile 传递已保存空场景，不采用跨项目共享 `EditorPrefs`。场景切换前验证归属、输入场景和 runner 拓扑，只替换确认的干净 runner 场景；不销毁未知 GO、不改生产资源合同。成本是须分别验证 UTF1.3.9/1.6.0 的回调时序及场景保护行为，不能仅凭静态调查记 GREEN。
- 本次 Context7 查询因 monthly quota exceeded 未取得资料；本问题继续依据已安装的精确 UTF 版本源码、Unity 安装文档及原始 XML，不把通用在线 API 记忆替代版本证据。
- 已在现有 `Stage9ReceptionTests` 注册仅 owned CLI/profile 激活的官方 `ICallbacks.RunStarted`。profile 记录空场景路径/SHA256；启动器拒绝 hash 或归属不匹配。回调拒绝脏/未知已保存场景、额外根/组件，只替换官方已确认的干净空或默认 camera/light bootstrap，再打开 owned 已保存空场景并建立空 active runner；没有改 `ResourceSessionTests` 或产品代码。
- `SyncTestSources -ProofRun tests6 -Run tests7` 仅更新 w2 回执拥有的 harness 源码；`TestsNew -Run new2` 实际正常退出并生成独占场景/profile，保持 New-only。`runner-safety1` 用 Profile=`new2` / Proof=`new1` 执行原 `ResourceSessionSafetyTests` 两项，官方 Runner 实际 **2/2 passed**，exit0、TimedOut=false、零 skip/selection 差异。日志确认 `STAGE9_TEST_SCENES_CONFIGURED Assets/Stage9TestsBootstrap-new2.unity`；格式 Check 通过。UTF1.3.9 和 ResourceUpdate 六项业务仍待实际验证。
- `w2 update-await3` 已解除 OneTimeSetUp 场景阻塞，实际进入六项业务，但仍 0/6、exit2/未超时：原测试还在 `ActivatePreparedUpdateAsync` / `RollbackAsync` / 不完整闭包 Check 的 Pending 态读取结果。已核对当前 `ResourceSystem.Updates.cs`，Activate 先异步 CaptureCommit/验证，再检查 native Busy；并非全部同步完成。继续只修测试的等待/消费顺序，保留 Busy、回滚、提交失败、完整闭包与真实 Domain Reload 断言，不改生产调度或降低认证合同。
- w2 最新源码收据为 `tests9`（tests7→tests8→tests9）；Native/Query 所有布尔结果/Busy 先等待终态，RemoteDownload 原 Busy 调用方同步改成 `yield return`，避免异步 helper 被忽略。三个文件格式 Check 通过。`update-await4` 实际 **3/6 passed**、3 failed、零 skip、exit2/未超时；通过的是 CompleteAndActive（真实多次 Domain Reload）、ExplicitMultiProjectClosure、ExternalLoadedProjectBundle。
- 其余三项明确保留 RED：ArtifactQueries 在 Prepare 结果之后查询仍见 Busy；CommitFailure 在 CommitDecided 尝试损坏目标后观察到 Succeeded 而非 Failed；NativeAssetInstanceScene 的某次 Busy 预期未成立。静态依据显示查询还受内容 producer/清理责任约束，且提交 receipt 会持有只读共享文件，损坏注入可能未实际写入；后两者当前只是待核实归因，不能据此修改安全合同或放宽断言。按本 Plan 的有限尝试规则暂停继续试改这三项，等待精确的清理/故障注入/原生时序证据，继续独立的新 Analyzer 候选与双版导入。场景前置已 GREEN 不代表六项业务全绿。

</details>

<a id="h05"></a>

<details>
<summary>H05 候选6、正式 Analyzer DLL 与双版接收</summary>

- 源 MCP 先只读确认 project/version/PID5556、非 Play/非编译/非导入、无编译失败、YLFrameworkDev 原场景不脏，再以同一身份/状态守卫调用正式 `ExportUnityPackage.Export`；未保存源资产或调用交互菜单。`YLFramework-U63-candidate6.unitypackage` 为 4,553,700 bytes，SHA256 `de876e48c9de62912c4adc00b79321c0f8dc41d0d12045cae8d7554d501f71a2`，原 AuditPackage 精确 1038 条通过。内含新 DLL SHA256 `c0d20af752831f62a46a51794210e05e71a21410156655ca40ac6944fb831f79`，GUID/meta 与原身份一致。
- 在此前不存在的新 `v3`（2022.3）/`w3`（6000.3）真实 Create/Import 同一候选，均正常 exit0；两份 `*-first-import-initial.json` 保存无 harness 首次编译与最小 manifest/真实 lock，未安装 MCP/XF/Input/Test/IDE 补依赖。导入后的两个正式 Analyzer DLL SHA256 与新构建精确相同，未重绑定 candidate5/v2/w2。
- 首次导入证明之后才给 v3/w3 声明此前已核实的固定官方 Test/Input/IDE 版本，接下来由各自 Editor 解析 lock 并安装完整现行测试闭包。此时仅证明新 DLL 载体编译，不宣称新 DLL 的 77 项诊断、VS/Rider 或 Player 依赖检查通过。
- v3/w3 已各自 `InstallTests -Run tests1`、`TestsLegacy -Run legacy1` 正常编译/配置退出。实际 lock 仍分别为 Test1.3.9/Input1.14.2/NUnit2.0.3/Rider3.0.40/VS2.0.28 与 Test1.6.0/Input1.20.1/NUnit2.0.5/Rider3.1.0/VS2.0.26，源 manifest/lock 未动。
- Ruling：不在新 v3 机械重试已知的自定义发现 Exit 挂起。既有 TestsDiscover 仅保留真实 RetrieveTestList 取树；接收 CLI 同时执行官方 Runner 的单项纯同步烟测，由官方 Runner 负责退出，启动器分别要求完整 discovery 身份/Passed 与精确 1 项烟测 XML 通过，保留 180 秒截止。成本是两种官方 API 在同一 Editor 中运行，须实证发现完整性/正常退出；任何缺文件、少项或超时仍 RED，不冒充完整测试通过。
- v3/w3 `SyncTestSources -ProofRun tests1 -Run tests2` 后，新的 `TestsDiscover -Run list1` 两端均实际正常 exit0、TimedOut=false；真实 Edit=2596 / Play=791，完整闭包检查通过，两个 companion XML 都精确 1/1 passed、零 skip。这建立新的官方退出路径 GREEN，旧 t/list1/list2 超时结果仍保留，不改成正常退出。
- candidate6 新 DLL 的 `v3/w3 analyzer1` 官方回归各 **77/77 passed**，Profile=`legacy1` / Proof=`list1`，零 skip/selection 差异，正常 exit0；不再沿用 candidate5 旧 DLL 结果。随后 `fingerprint-safety1` 两端各 **19/19 passed**（17 项多目录指纹 + 2 项未保存/脏场景安全），同样零 skip/差异。日志确认 UTF1.3.9 的 owned RunStarted 场景前置也实际执行。SDK181、新 DLL 双版导入/回归已闭环，实际 Unity 编译诊断作用域、VS/Rider、Player 依赖与完整测试仍是后续门禁。
- 已委派内部只读任务 `ses_e83462310ffeWQluxaBOtpm1iG` 精确分析旧/新 Analyzer PE/IL 差异；不写文件、不构建测试、不启动 Unity/MCP，与主线程的 owned Unity 回归分离。未据相同文件大小或测试通过推断字节差异无语义影响，待完整结果后记账。
- 上述二进制调查已完成：全 DLL 只有 72 bytes / 5 个区间不同，分别是 COFF timestamp（0x88/4）、唯一 MVID（0xBB84/16）、CodeView stamp（0xD190/4）、PDB GUID（0xD1E4/16）和 PDB checksum（0xD268/32）。208/208 方法体完整原始字节相同（18203 bytes、27 EH regions）；五区间之外全文件相同，内存归一化 SHA256=`e9ae4663cb549c41779a3f4221f6b4f5a334a6cf8451eebb3a33f6f3aa9b5529`。规则 IL、消息、默认级别、签名、引用和属性无差异。可以确认只是构建/外置调试身份差异；缺旧 PDB，不能进一步断言唯一原因是 SDK 或 SourceLink 提交号，更不能混用旧符号。
- 只读入口盘点 `ses_e8340397effeapKIBkoAoqv73J` 已完成：77 项中有 1 个真实 AssemblyBuilder Include/None/Error 用例，其余主要是规则文件、配置与自检；不能等同自动 asmdef/预定义作用域、pragma、Error 恢复或 IDE 已验。旧 AnalyzerReceiver/VS/Rider 模板含固定旧根/PID/.sln，仅作方法参考，不领养或冒用。后续需 owned v3/w3 的最小真实编译及 IDE 生成/运行门禁；目前 .slnx 兼容性仍待实际核实。调查提到的“Temp 收据缺失”不作为源事实采纳：本轮已实际回读源 `Temp/YLFramework.Analyzers/build-info.json` / TRX；接收端没有源 SDK Temp 是正常隔离，不据此重复构建。
- 当前唯一有状态运行：v3 `ordinary-edit1`，candidate6 / Profile=`legacy1` / Proof=`list1`，官方 EditMode，Filter=`^YLFramework\.`，显式隔离 Exclusion=`Resource|Session|ContinuousPlay|EquipmentTwoPlay|RetainedTerminalHandle`。后台 ID=`bgp_17cbfef5b001BSNNylz35rmn5c`，600秒 monitor 上限后仍在执行，Unity 硬截止1800秒；日志有持续测试输出但尚无终态 XML，不记通过、不启动第二个 Unity。恢复应接收同一进程退出事件，读取其 exit/XML，不覆盖或重跑替代；其余 Resource/会话/GC 项仍另验。
- 用户询问是否需要手动新建6.3工程：不需要，已有 w3 是实际创建、导入且专项回归通过的6.3工程。当前费时主要是产品兼容、旧测试环境/合同与完整发行矩阵交织推进，不是缺少6.3环境。此刻不再新建验证工程，不把环境准备转交用户；继续时优先复用现有载体、区分产品缺陷与测试前置，未完成矩阵不冒充已验。

</details>

<a id="h06"></a>

<details>
<summary>H06 普通 EditMode、退出终态与 Editor 兼容定位</summary>

- 用户要求继续持续执行，Goal 已恢复。10:02Z 唤醒后核对原始日志才确认 `ordinary-edit1` 并非业务仍在跑：XML 已在09:47:11Z写出，执行63.923秒，**1833/1835 passed、2 failed、0 skipped**。两项失败均为 `DomainTwoPlayTests` 在业务进入前要求 owner 预配置 DisableDomainReload，而普通 legacy1 未配置；它们应归入真实双 Play/专用 profile，不改断言，也不据此认定 Domain 产品失败。此前“日志有输出/尚无XML”的状态已过时，不再沿用。
- 官方日志已声明退出code2，但原生阶段停在 Input System Shutdown / TcpMessagingSession receive error，原 WaitForExit 仍阻塞。10:05Z 在核验精确 PID22172、projectPath=v3、实际executable、runTests与启动时间后，只对该 owned Unity 树执行 taskkill；源PID5556与其资产worker保留。wrapper随后实际收到exit1/TimedOut=false；该值是外部终止结果，**不是正常退出，也不是业务XML的code2**。原XML/log/process/exit全部保留，不改写绿色。
- 最小启动器修复：只对带 `-testResults` 的既有官方 Runner，在最终XML出现end-time后给予120秒退出宽限；超过即停止本次Process拥有的树，单独记录 ForcedExit/ExitStalled，不把它等同硬超时或正常退出。普通正常退出与总硬截止仍保留；Tests即使原生exit非零也收集实际XML/精确selection差异到verdict，然后仍抛出原失败，绝不因XML绿色掩盖进程失败。待下一次官方运行验证，旧ordinary1不补造verdict。
- 后续普通分组增加显式 TwoPlay 隔离，再对 v3/w3 验证；DomainTwoPlay 两项在真实 owner 配置下另验。该分组不删项、不改被排除测试的状态，不将适用子集当全套通过。现已没有 owned Unity 残留。
- 新启动器已通过实际 pwsh7.6 的 `green4` 六项入口拒绝回归。`v3 ordinary-edit2`（额外隔离 TwoPlay）实际 **1833/1833 passed**、精确selection/零skip，正常exit0、ForcedExit/ExitStalled/TimedOut均false。原ordinary1的外部终止红灯不覆盖；普通分组现在有独立正常退出的绿色证据。
- `w3 ordinary-edit1` 为 **1807/1833 passed、26 failed、零skip、selection一致**，实际正常exit2。新启动器在非零退出时仍生成了完整verdict/消息，未丢失XML或等待30分钟。失败按根因分组，而非26个独立产品缺陷：共享Fsm背景重绘NRE、Domain测试TreeView旧强转3项、Dictionary私有重载1项、Help高亮几何1项；不把被污染的后续UI错误直接算成独立根因。
- 已确认Fsm根因在Unity6条件分支 `_graphView.Window`：现有GraphPreviewWindow直接承载GraphView，没有FsmEditorWindow，而该分支无条件应用专用窗口侧栏/工具栏偏移。源产品最小修复只在实际存在FsmEditorWindow时应用其偏移，保留独立GraphView本地坐标；两处改动未变public API/GUID。**此修复尚未导出候选/安装到w3，不能记GREEN**，待与本批其他产品修复一起正式验证。
- Domain测试两处强转/typeof统一为两代引擎对应的TreeView alias，原绑定/复制/重排/Undo/Redo断言不改。`w3 tree-dictionary1`实际只选中Domain **3/3 passed**（Dictionary方法过滤名写错，没有选中，明确不记其通过）。后续使用真实方法名 `MultiObjectEditableFieldsHaveNoNativeArrayStructureContextCommands` 的 `dictionary-help1`选中2项，两项均RED：Dictionary按四参筛选未命中，已停止猜测并委派精确安装DLL的只读参数元数据调查；Help已采到失败现场。
- Help失败现场是 `Delay\r\n屏幕 Delay`、index14、width32、dpi1.75；wrapped/unwrapped的index14与15都重合在end，末行fragment实际上到同一end。这不是已证明的字体缺字，更像TextCore的CRLF元素索引与UTF16不一致；只补失败诊断，没有增容差/改文本或产品算法。只读原调查已接续核对精确索引合同，待结论再改。
- 测试harness新增 owned `TestsLegacyNoReload`，明确写入并在新profile记录EnterPlayModeOptionsEnabled/DisableDomainReload；普通TestsLegacy/New/Both明确复位为正常Domain Reload。源设置绝不调用该入口；接下来对两项DomainTwoPlay做专用真实两会话验证。v3当前测试收据`tests3`，w3为`tests4`；两者当前input profile仍legacy1，尚未切NoReload。四文件和Help诊断的格式Check均通过。
- `v3/w3 TestsLegacyNoReload -Run noreload1` 均实际配置正常退出，profile确认EnterPlayModeOptionsEnabled=true/DisableDomainReload。随后两端 `domain-twoplay1` 各 **2/2 passed**、零skip/selection差异、正常exit0，覆盖两项先前普通分组中前置失败的真实双Play行为；不覆盖/抹去ordinary1红灯。两端当前输入仍Legacy，当前会话profile变为`noreload1`。
- Dictionary精确只读metadata已到：2022唯一签名为 `(SerializedProperty, SerializedProperty, GenericMenu, VisualElement)`；6000.3为三参 `(SerializedProperty, SerializedProperty, GenericMenu)` 与六参含isShiftPressed/isEnabled。三参包装器实际读取GUI.enabled/Event.current.shift并转发，保持当前旧调用的上下文。源测试已改按两代引擎完整Type[]精确GetMethod，旧4参/新3参调用尾部默认null，不再按名称或参数数量猜测；待真实复验。
- `w3 dictionary-menu2` 已按真实方法名执行，实际 **1/1 passed**，精确selection、正常exit0，原多对象结构权限断言保留。Domain旧强转3项与Dictionary反射1项已各自有GREEN；共享Fsm产品修复仍只在源，尚未记接收端通过。
- v3 Both 首轮 `core-both1` 实际234/235，唯一失败仍是新空工程缺少非Default SortingLayer（和此前w2同一已知前置），无Input焦点失败。已把Map初始化集中到现有owned ConfigureInput：仅默认单层时用SerializedObject添加Map/id271027，已有非Default层不覆盖；不修改源TagManager。v3/w3当前源码收据分别`tests5`/`tests7`（w3最后多一次严格Help测试同步）。
- `v3 TestsBoth -Run both2` 重启后 `core-both2` 实际 **235/235 passed**，Profile=`both2` / Proof=`both1`；再配置 `TestsNew -Run new1` 并真实重新发现，`core-new1` **229/229 passed**。两者均正常exit0、零skip/selection差异；仅证明Editor官方PlayMode，不代替原生Player/URP/手动UI。当前v3为New-only/new1，w3仍Legacy/noreload1；不要混用旧profile作当前输入画像。
- Help只读结论由精确6000.3.27f1源码与现场坐标共同确认：TextGenerator将CRLF、有效surrogate pair各折叠为1个处理元素，GUIStyle却直接把索引当元素位置（GUIContent另带尾随零宽字符）。原始UTF16 index14/15落在同一end；原几何与原测试参考都用错索引。已先增强严格回归：原文/复制字符串不变，参考使用明确表而非产品helper，涵盖CRLF后首字/末字单匹配、连续两CRLF、纯换行空片段、LF、非BMP后ASCII与原32/71/240宽度边界/行高断言。`help-crlf-red1` 在冻结旧产品上实际RED：index7(屏)位置不被覆盖，证明不是只修oracle就绿色。
- 在看到该RED后，源 `HelpReaderRendering.BuildFragments` 仅在Unity6.2+分支一次O(n)建立UTF16边界到caret元素索引表，全部四处wrapped/unwrapped调用统一使用；2022路径仍原UTF16，不改变匹配Range/原文/复制、不增容差/放宽边界/改字体或行高。复杂VS/ZWJ/ligature完整支持未被本次证明，不将此修复包装成通用grapheme实现。待与Fsm修复合成下一正式候选，双版新鲜接收验证后才能记GREEN。

</details>

<a id="h07"></a>

<details>
<summary>H07 候选7、作者工具与 Help 平移 RED</summary>

- 正式受源身份/状态守卫导出 `YLFramework-U63-candidate7.unitypackage`，4554051 bytes，SHA256=`55f8b01e1bd357a154c2ef47ac8cfaef8970dd8a15d72954afa9e887acdedcab`，1038条原审计通过。精确比较candidate6/7审计记录，仅FsmTransitionBackground.cs和HelpReaderRendering.cs两个数据hash变化；无路径新增/删除、无GUID/meta改变，Runtime/Analyzer/Plugins字节不变，不能反复重跑这些未受影响部分来凑进度。
- 新v4/w4按各自实际Create/Import完成同字节包无harness首次编译，正常exit0。之后才声明固定Test/Input/IDE版本和安装现行完整测试闭包；w4已TestsLegacy/TestsDiscover，profile=`legacy1`、proof=`list1`、source receipt=`tests1`。v4已InstallTests/声明依赖，但**尚未Editor解析测试包或配置profile**。
- `w4 help-crlf-green1` 仍RED（1/1失败、正常exit2）：CRLF/连续CRLF/非BMP/单字覆盖的新断言均已越过，随后既有平移不变性断言出现83.4286041而期望83±0.01。不是放宽原容差；这是TextCore在1.75 DPI按绝对原点取整导致直接BuildFragments与既有局部缓存绘制路径不一致。源现已按缓存契约统一局部原点测量，再平移输出，三行数据流修复；尚未导出，candidate7不含此最后修复。
- 为隔离已知Help平移红灯，`w4 ordinary-edit1`明确额外排除该单项，其余 **1832/1832 passed**，零skip/selection差异、正常exit0；这涵盖先前26红灯中的其余25项，Fsm共享重绘、Domain树强转、Dictionary精确签名均有本批真实接收端绿色。仍不把被排除Help项记为通过。
- 后续只需将最后平移修复与现有两处Editor修改合成下一正式同字节候选，优先验证6.3该单项和2022对应严格回归。Source目前已再次MCP核实PID5556/2022.3/非Play编译导入/无编译失败/原YLFrameworkDev场景不脏。当前没有owned Unity运行，候选8/v5/w5路径核验尚不存在。

</details>

<a id="h08"></a>

<details>
<summary>H08 候选8、6.3 普通组 GREEN 与 2022 退出 RED</summary>

- 正式守卫导出 `YLFramework-U63-candidate8.unitypackage`，4553690 bytes，SHA256=`3a7d724204c31d3718841007a4a71b6aa97053484529a0a796650bd53d5048df`，1038条审计通过。新v5/w5实际同字节无harness首次导入编译正常exit0，然后才安装固定官方测试依赖和现行闭包。两端source receipt=`tests1`、Legacy/profile=`legacy1`，均有实际测试树JSON=`list1`（2022退出例外见下），源formal代码与candidate8一致。
- `w5 help-crlf-green2` 实际 **1/1 passed**、正常exit0，保留所有原文、独立索引oracle、区域边界/行高/单匹配/平移容差。随后 `w5 ordinary-edit1` 在不再排除高亮的普通分组中 **1833/1833 passed**，零skip/selection差异、正常exit0。这是candidate8同一实际运行的完整普通分组GREEN；原w3的26失败、candidate7高亮平移RED均保留，不覆盖。
- `v5 TestsLegacy`与rawCreate/Import均正常；`TestsDiscover/list1`真实输出Edit2596/Play791、Missing=[]、Passed=true，官方companion XML1/1 passed，但XML后120秒仍未退出，watchdog记录ExitStalled/ForcedExit=true、exit-1/TimedOut=false。该发现进程不是正常退出。仅复用已实际完整生成的叶子清单做精确选择，不把退出RED改成GREEN。
- 随后标准官方 `v5 help-crlf1`（无executeMethod）XML真实 **1/1 passed**、selection一致、NonPassed=[]，但再次在XML后120秒退出挂起；verdict正确保持Passed=false/ProcessPassed=false。因此已验证2022高亮行为，但**没有正常进程退出的GREEN门禁**。已按两轮退出挂起规则暂停v5相关Unity重试，不以杀进程冒充通过或重跑到绿；没有接管源PID5556。
- 委派旧场景调查子任务 `ses_e8375c612ffed1cv2hXAPsnRrM` 做新的只读生命周期调查，关注额外TestRunnerApi注册后未Unregister/Destroy及UTF1.3.9/1.6时序；不运行Unity/MCP、不写文件、不猜测即可修复。接收精确证据后再决定，主线程继续独立的6.3 ResourceBuiltInDeployment/ResourceStartupExample前置回归（此前只因StreamingAssets缺席而失败的组），不重复已暂停的ResourceUpdate三项。
- Source最后本批MCP仍为原PID5556/2022.3/原YLFrameworkDev不脏、非Play/编译导入/无编译失败；本轮format Check与局部diff --check通过。最终源保护after、IDE/Player/加密/可选集成及全体独立Review仍未完成，不能报告整体complete。
- `w5 builtin-startup1` 实际选中并通过 **28/28**（不是历史概括的21项），零skip/selection差异、正常exit0，证明现行owned StreamingAssets/scene前置下内建部署和正式启动样例组可执行。没有重跑已暂停的ResourceUpdate三项。
- 2022退出只读调查已完成：v5两次均已由官方Executer调用EditorApplication.Exit(0)，native日志到InputSystem Shutdown、Licensing断连后停止，尚未到Cleanup mono；v3同helper正常退出且v5 discovery没有scene callback仍挂，故不能将额外TestRunnerApi实例/未注销回调当充分或必要原因。无native栈/ETW，具体子系统未知，继续暂停v5重试，不更换nographics/不接管用户Editor。
- 独立已确认harness所有权缺陷予以最小修复：Scene准备使用公开静态RegisterTestCallback而不再创建多余ScriptableObject；RunFinished调用UnregisterTestCallback(this)。两代CallbacksDelegator都遍历数组快照，时机在场景恢复/退出前且不会破坏其他回调。Discovery的异步RetrieveTestList实例仍按原delayCall释放，不能删掉其_api字段或在回调栈内销毁。此修复尚未同步/运行，不宣称已解决native挂起。

</details>

<a id="h09"></a>

<details>
<summary>H09 Resource 查询、提交、取消与恢复合同</summary>

- 冻结w2 Query失败精确在第二次Query.cs:69，冷B.prefab请求刚启动真实ContentIO；ArtifactStore.Query.cs:70-75与Resource.md:571/576明确此时应Busy。源测试先断言冷WorkCount>0/QueryBusy，再用既有WaitForUpdates只等内容producer退休，仍要求后续Ready/InFlight/Lease与Pin无增长/磁盘不变/旧preview不能授权删除；CleanupVersionsAsync也显式等终态。`w5 update-query1`实际 **1/1 passed**、正常exit0，保留原冻结RED。source receipt=`tests2`同时包含回调所有权修复，6.3实际RunStarted/RunFinished运行正常，不代表2022native挂起已修。
- Commit只读调查确认receipt持有目标Bundle的FileShare.Read输入到整个提交/异常恢复结束；在CommitDecided内尝试WriteAllBytes可能被拒绝，冻红未记录异常，因此不能声称字节已损坏。新测试不提前释放输入、不改生产认证/共享模式：同次ResourceSystem提交用CreateNew未登记文件真实破坏To闭包，外部断言注入已完成、请求Failed/UpdateFailed、memory/disk仍为有效v2且旧资源实际可Acquire。另把坏Source预验证与明确Shutdown/Mount/Initialize后的fallback分开，保留未到提交/active指针不变及重启后内存磁盘一致性断言。
- 新增必要的独立 `PostDecisionCorruptionAfterReceiptReleaseRecoversOneCompleteClosure`：同步store.Commit在CommitDecided抛受控IOException，确认Committed路径命中/待恢复journal存在；receipt正常释放后实际写坏目标Bundle并回读字节，再用新Store.Mount要求a/b整组回到From1，不混成同次live请求或仅元数据故障证明。因此w5/list2真实发现Edit2597（比原多1）/Play791，旧2596发现不改写。
- `update-commit1`的旧错误方法名只实际选中了新增raw测试1项，1/1通过，明确**不是原计划两项均通过**。为防这种部分匹配被误报，启动器新增可选ExpectedTestCount，selection记录RequestedCount；guard-count1实证“意图2/只匹配1”在Unity启动前拒绝且无process回执。纠正真实方法名后 `update-commit2 -ExpectedTestCount 2` 实际 **2/2 passed**、正常exit0/零差异，覆盖同次恢复与真正字节损坏两条路径。两文件format Check通过；当前w5 source receipt=`tests4`（tests3为提交分层、tests4增加诊断标签）。
- native余项先仅增加唯一Busy标签和等待前/后的不可变诊断，不改Busy断言。`update-native-diagnose1`实际RED于 `instance-returned: Succeeded`；只读解析原始消息确认前态cache retired=true/DestroyPending=1，后态cache=[]、Record/Lease/Bundle/pin均0、native load/unload=4/4、digest已切To2。对象在后台验证期间已真实结算，不能将此判为屏障漏挡。没有据此改成接受Succeeded。
- 依据确证时序，现测试先排空旧已退休实例，再创建开放桶里的真实idle实例，外部断言未退休桶Idle>0/对象活着；让正式屏障自身退休该桶，在同同步栈内严格要求Busy，之后再要求对象真实死亡/Bundle排空。此为原断言前提的确定化，不伪造状态、不用sleep或移除安全检查；**尚未实际运行**。取消资产阶段仍保留原严格断言，若到达下一红灯应继续先采证，不能宣称Scene门控替代取消资产的独立覆盖。
- `w5 update-native-idle1` 证实前述稳定idle退休断言已越过，下一RED明确为`asset-canceled`。实际诊断在调用等待前是IsNativePending=false/IsContentWorkPending=true，说明原分支尚未真正开始native即取消；异步验证后返回Succeeded不能当作native取消屏障证明。
- Ruling：按既有“验证完成后检查中立边界”合同重写该取消见证，而不是简单接受Succeeded或换成Scene替代。先等待诊断证明真实native Bundle启动（计数增加/IsNativePending），Cancel后仍要求native责任被保留；在公开Activate的JournalVerified主线程checkpoint只读采样独立见证。未到提交边界必须严格Busy且From不变；已进入提交则必须Succeeded且当场Record/Lease/Bundle/pin/InstanceCache/Scene均0，同项目冻结native名称集合也为空。排空后继续真实新版本Acquire及Rollback旧内容。捕获闭包移入EnterPlay后helper，未跨Domain Reload序列化。没有改生产门禁、伪造状态、sleep或缩小旧内容/回滚断言；这消除了“调用时曾在途就必须在验证后仍Busy”的不成立前提。
- `update-native-neutral1 -ExpectedTestCount 1`实际 **1/1 passed**、正常exit0/零skip/selection差异，覆盖真实native已开始后的取消保留、成功提交的中立见证、前序活资产/借出实例/现场退休idle/仍加载Scene/Core prefab的严格Busy及目标内容/回滚。source receipt=`tests6`。`update-query1`与`update-commit2`亦已重新回读verdict，分别1/1、2/2正常绿色；接下来把原六项和新增真实字节损坏合并7项，防止只靠分项通过漏掉交互，未运行前不记整组GREEN。
- `w5 update-combined1 -ExpectedTestCount 7` 已实际 **7/7 passed**、正常exit0、零skip/selection差异，包含原六项与新增receipt释放后真实字节损坏整组恢复；不再仅依赖分项通过。未改任何Resource生产实现/安全版本门禁。冻结w2与本轮诊断RED仍保留，2022对应Resource新回归仍依赖退出阻塞解除或明确独立载体验证，不能借6.3结果冒充。
- 重新执行 `Protect -Run checkpoint3`，实际Before=After=2553、AllDifferences/AuthorizedDifferences/UnapprovedDifferences均空，Passed=true。Git仍integration/xf-yl；原用户Xf计划/Android图标差异和既有未跟踪资产均保留，本轮无commit/push/分支切换。此为阶段保护检查，最终after尚未完成。
- 接续转向成本/发布证据：`ResourceCostEvidence`两处scope写死B02+F03-20261017-0902，Prepare-ResourceCostEvidence仅写scope而缺purpose；Publication两个fixture的历史purpose仍待定向读取。不能拿本批PFX/目录冒用旧owner，更不能直接普通全量让这些fixture红灯或跳过冒绿；先建立本批显式owner参数化与拒绝回归，再执行适用组。当前w5=candidate8/Legacy/legacy1，source receipt=tests6、proof=list2（Edit2597/Play791），没有owned Unity在运行。

</details>

<a id="h10"></a>

<details>
<summary>H10 候选9-11、Resource140、成本、发布与严格 checker</summary>

- 主Plan Global Constraints第38行/Task5明确允许未验证的精确native组合仅作为本批候选参与验证，不得提前发行；不是删除安全门禁。先用既有d/candidate3/Mono Build2跑独占`encrypted63-mono1`，实际WindowsPlayer/6000.3.27f1/Mono2x/非Development、Graphics非Null、exit0/无晚期错误，BusinessPassed/Passed=true。Cng源码原SHA256 `167d32ce1070c64b3c13c97985d5984eeebb720cfcc50b775999d565eef77ebf`与现行改动前逐字节相同；不把该结果冒充IL2CPP或HTTPS。
- 先新增8个精确版本正反例，w5/list3真实发现Edit2605/Play791；`cng-version-red1 -ExpectedTestCount 8`在冻结Runtime8上实际8/8失败（缺纯版本决策方法），另有先前真实IL2CPP encrypted63-red1被精确旧版本门禁拒绝。随后仅将Cng的IL2CPP版本判断提取为内部纯`IsAcceptedPlayerVersion`并允许固定2022.3.62f3c1/6000.3.27f1，不接受其他补丁/后缀/空值。Windows/x64/编译后端/StandaloneWindows64内容平台/实际内容Unity版本匹配检查均保留；ResourceRemoteSource仍2022-only未改。新8项NUnitGREEN仍待使用含候选9的测试载体完成，不能把Native正例代替该负向集合。
- 正式Source守卫导出candidate9：4553852 bytes、SHA256=`9c203c112c348ccc60d8dc2e28f5a28c98fdb60d962906876f6174f4f1cf4a57`，1038条审计通过。新短载体z（6000.3/UGUI2）与y（2022.3/UGUI1）均真实Create/无harness首次Import编译正常，再安装Stage9公开消费者、Audit/Generate，各自生成该引擎内容，未混用Bundle或原生构建。
- z BuildIl2Cpp/2实际Succeeded、Errors0、Warnings18、Options=None、非Development、Low、BuildGuid=`ccb1e7c5c6c14c989937b700bdb812a3`；`z encrypted63-candidate1`与`missing-key63-candidate1`均真实图形Player正常exit0、BusinessPassed/IdentityMatched/Passed=true、无Exit/LateErrors。y BuildIl2Cpp/2的`encrypted22-candidate1`也实际正常exit0、BuildGuid=`b9eb8a7045c3490d8afabbe416eb5724`。证明精确新增6.3的CNG ABI/加密加载和缺密钥拒绝，并回归2022；不代表Development HTTPS、原生New/Both输入或完整资源安全矩阵。18项warning保留，不做无关清理。
- Player启动器此前非scene-loop无外部deadline，已统一所有Stage9 Player 240秒自有树截止，不改非Development断言。以上运行均未触发超时；旧red1不覆盖。
- 内部子任务成本owner限定4文件实现已完成：Prepare显式OwnedRoot/Repository/Scope/Purpose、Run request全身份、Publication清理标记绑定本批；18项纯参数/路径拒绝、pwsh7.6解析、局部格式/diff检查通过，旧owner不改写。Main已复核diff，新增BeginRun/共享PrepareRun，使官方RunStarted只准备context/注册Observer，不嵌套Execute；原Run仍自行启动但拒绝已活动runner。实际活动查询在UTF1.3.9/1.6都是internal，第一次直接调用编译失败已保留，现按两代真实签名精确反射，无静默回退。w5 source receipt现`tests9`。
- 新成本根 `Temp/kilo/U63-20261027-c63a1/cost-w5`已实际Prepare成功，repository明确w5、scope本RunId、purpose Resource-cost-and-publication，3天非生产PFX、未改系统信任。`publication1`止于编译错误无NUnit；修正后`publication2`官方实际 **9/10 passed**，唯Io晚取消期望OperationCanceled而实际IOException。finished.json/SourceIdentity/原始XML均写出，环境恢复由Observer执行；严格CostEvidencePassed/Passed仍false。不能把9项Retry通过冒充全组绿色。只读成本子任务正在核对真实File.Replace HRESULT/取消先后，暂停重复Io运行。
- Resource专用native工具子任务仅改8个授权工具文件的两版本/UGUI/真实createProject/owner/Dev与Low/Graphics/AudioContext/SourceIdentity参数化，已报告pwsh/Roslyn语法、格式、纯拒绝回归通过；未实际Unity/构建/服务。纠正此前摘要：旧r及旧Prepare实际缺AGENTS，现工具才补入AGENTS+meta；旧r仍保留为旧身份，不能领养成6.3。后续新r6/resource63需独占创建；Stage9非Dev和Resource C1/C2 Dev不可互相替代。新工具仍待main复核/真实运行，Runtime HTTPS门禁仍未扩展。
- 成本桥接实际编译首轮失败原因是TestRunnerApi.IsRunActive在两固定包均为internal，不能直接C#调用；已按实际零参static/nonpublic签名反射并正常编译执行，不能将首轮publication1当NUnit红灯。`publication2`实际9个Retry用例全部Passed、唯一Io late-cancellation失败；finished已确认EnvironmentRestored=true/EnvironmentFailure=null，owner/request/source/loaded assembly身份和原XML完整，严格整体仍RED。
- 只读Io调查明确：probe仅前两次blocked/changed观察到真实HRESULT32；late自身因断言先于记录而未保存HRESULT，不能推断为32。等待.next存在不等于其写完/flush，也不证明Cancel发生在AtomicWriteCore最后token检查前；生产只对精确1175重试，不承诺晚到取消覆盖已发生IO失败。现有replace钩子在最后取消检查后、backoff仅1175可达，无法不伪造异常地固定首次替换前取消。可选最小接点是afterNextWritten（涉及private生产Core签名与调用者），尚未实施；不得简单扩大错误允许集合或重跑到绿。9项已有确定性重试/取消路径不冒充该独立经验probe。
- 已启动完整ResourceCostBaselineTests A/B/C、ExpectedTestCount=3，official EditMode、CostRoot=上述cost-w5、scope/purpose明确、w5 candidate8/legacy1/proof list3/source receipt tests9，Run=`cost-baseline1`。后台ID=`bgp_17dc4852d0011BjN7kSm2OHcyE`；120秒monitor返回时仍在运行、Unity硬截止1800秒，实际日志进程watchdog记录VBCS PID49428（不是待杀Unity身份）。**没有读取终态，不记GREEN**。下一步接收同一后台退出事件，回读`w5-tests-EditMode-cost-baseline1.{verdict.json,xml,log.exit.json}`及`cost-w5/cost-baseline1/finished.json`，先查恢复/夹具清理，不启动第二个Unity或重复同Run。
- 当前没有待返回的内部子任务；Resource native 8文件参数化已全部返回并有精确调用形状，但未实际Prepare/Generate/Build。原r不动；计划新r6/resource63。RemoteSource仍只允许2022 Player；先真实Development候选RED再按精确环境支持扩展，不动版本/签名/信任/安全门禁。纯版本8例在冻结w5 Runtime8仍RED，后续须含候选9的单独测试载体或明确新身份，不能直接同步formal源码重绑定w5。
- 本阶段未报告complete/blocked。仍缺：2022 v5 native退出定位、全部适用Resource与成本/发布余项、Development HTTPS两后端、Release本地加密完整拒绝矩阵、真实IDE、URP/手动UI/原生输入、DOTween/Excel/DomainExcel/HybridCLR、最终包/独立Review/最终源after。持续Goal按此台账接续，不把阶段验收当整体结束。
- 14:50Z用户询问后已回读cost-baseline1实际终态：14:38:26Z finished，原始XML/严格verdict为 **2/3 passed**、selection无差异、零跳过。ARecipeBuildAndPublicationBaseline失败于NUnit单项180000ms门限（记录实际Duration419.1946s）；BSignedHttpsFullAndResumeBaseline通过78.7298s；CEditorSharedConsumerAndPeakBaseline通过3.1534s。整体Passed/CostEvidencePassed=false，不把B/C或完整执行等同全组通过。
- 该次EnvironmentRestored=true/EnvironmentFailure=null；Unity在14:38:30Z自行exit2，ForcedExit/ExitStalled/TimedOut均false，非1800秒外部超时或强杀。C输出有无AudioListener警告，不能据成本用例通过宣称音频播放/DSP验收。后台bgp_17dc4852d0011BjN7kSm2OHcyE已结束，恢复后不得继续等此进程或覆盖原run；下一步定位A的构建/发布具体耗时阶段，未经证据不盲目增加Timeout来制造绿色。
- 14:55Z接续已汇总本次21份A原始样本：Recipe合计395.9800s、Errors总数0，全部bootstrap/None/Encrypted warmup与3×new-workspace/no-source-change/local-change均完整落盘；f03-releases及3份导出已完成，cleanup.Failures=[]，不是停在某个构建步骤。Native BuildPipeline合计仅12.2764s；重复完整指纹/验证占主要耗时。每次无源变化仍因精确两个未知官方callback（AdaptivePerformance BuildHook、UGUIBuildAnalytics）按现有安全门禁强制重建，不允许为性能绕过。
- timeout来源已在安装UTF1.6的UnityWorkItem/TimeoutCommand确认是默认180000ms，不是项目A的性能SLO或卡住watchdog。Ruling：仅A多样本benchmark加显式Timeout(15*60*1000)，保留Samples=3、21次Recipe、20–40MiB负载、完整hash/认证、cache安全门禁、所有内容/发布断言及B/C原运行deadline；不是全局放宽、缩样本或把旧RED改绿。原180000ms/419s红灯和样本保留。局部format/diff检查通过；w5 source receipt更新为tests10，下一次须新run完整A/B/C验证，未实际终态不记通过。
- 已正式启动独占新Run=`cost-baseline2`，原完整A/B/C Expected3、同cost-w5 owner和legacy1/proof list3/source tests10，外部Unity硬截止1800s。后台ID=`bgp_17de5949e0017kFWOOI5tWzFkt`，120秒monitor返回时仍在运行；原cost-baseline1不覆盖。已另排单次自动收集wakeup=`wku_17de8828a0016cpAsabhZrGTfU`，到时15:17:12Z，避免把等候交给用户手动提醒。下一回恢复直接读取同一进程/终态，不重跑或先启动另一Unity；当前等待不是整体blocked/complete。
- 15:17Z单次唤醒已实际收集cost-baseline2终态：A420.3595s/B78.3093s/C3.1697s，完整 **3/3 passed**，Expected=Actual=3、Difference/NonPassed/SuiteFailures均空，CostEvidencePassed/PassedGate/Passed均true。15:14:42Z正常exit0，TimedOut/ForcedExit/ExitStalled全false；EnvironmentRestored=true、cleanup.Failures=[]。保留原cost-baseline1默认门限RED，不改历史。C仍只有资源/消费者成本意义，AudioListener警告未用于宣称DSP。成本本轮完整夹具已达成，接下来不重复此组。
- Publication子任务已仅3文件落实per-call afterNextWritten接点：唯一private AtomicWriteCore扩为7参，原入口/Retry末参null，接点位于完整write/flush/close后、原Move/Replace取消检查前；Io同步验证.next完整→Cancel→返回，仍由生产检查抛OCE，原token/From/Next严格断言保留。局部格式/diff静态检查通过，**未实际Unity/NUnit**；冻结w5/candidate8不改，SourcePublisher新接点尚未正式打包。main未将9项旧Retry通过当新7参源码GREEN。
- 为闭合候选9的8个精确版本正反例，新独立x按真实Create/Import完成candidate9无harness首次编译，之后固定Test/Input/IDE版本+InstallTests/tests1、TestsLegacy/legacy1、TestsDiscover/list1。`x cng-version-green1 -ExpectedTestCount 8`实际 **8/8 passed**、正常exit0、零skip/selection差异；原w5/cng-version-red1保留。x正式Publisher仍为candidate9旧6参，当前测试源码Retry/Io已为新7参，因此x仅用于已声明CNG策略，不可直接冒用为Publication新接点验证。
- Main已定向审读Resource Prepare/Invoke实际身份/createProject/owner/版本配对/硬超时/Player图形Dev检查，并独立diff--check。新r6/resource63目标事前不存在、父目录存在，18443未有监听；真实Prepare -CreateProject成功（FormalFiles1849），r810-owner绑定6000.3.27f1/UGUI2、Repository原Source，ProjectVersion revision672d562a42a7；source-identity确含AGENTS及meta。新非生产PFX仅本证据根，旧r不接管；未启动server。
- 当前已启动 `Invoke-ResourcePlayerAcceptance -Operation Generate -BuildName initial -Backend Mono2x`，EvidenceRoot=`Temp/kilo/U63-20261027-c63a1/resource63`、carrier=r6、expected6000.3.27f1，Editor硬截止900秒。后台ID=`bgp_17e0002af001HPChBGwhz1CWt6`；120秒monitor返回仍在跑，尚未读取终态，不记Generate/编译通过。恢复先收该进程/Generate-initial.exit.json/log，不启动第二个Unity、不重写initial；之后C1开发构建与真实HTTPS旧门禁RED、精确支持变更/验收，再C2/R1分层。所有内部子任务现已返回，无需等待/轮询子代理。
- R6 Generate/initial已在15:36:56Z实际正常exit0，配置写6000.3.27f1/UGUI2，官方lock实际UGUI2 builtin/Json.NET3.2.1 registry；随后C1真实Mono2x Development/Disabled/acceptance宏、BuildGuid5964df3158d648f0bd5005cbc8529430，正常构建。Options原文包含Unity附加内部flags，未用它推断backend，实际Player编译分支和报告一致。
- 唯一loopback server已用tracked `bgp_17e0b75aa001PMwZeQ7Cz5lX3C`启动，PID48072，证据根resource63，18443只监听127.0.0.1。`mdev-seed-red1`实际WindowsPlayer/Mono2x/Development/pin宏true、D3D12/48000Hz，先挂载内建a/b，再因RemoteSource精确旧2022门禁退出1；未发生下载/签名拒绝，不把它解释为TLS问题。原C1/RED保留。
- 看见真实RED后，Runtime RemoteSource只增加独立`IsAcceptedPlayerVersion`精确2022.3.62f3c1/6000.3.27f1，Windows/x64/编译后端条件不变，pin/验签/版本/序号/控制指针安全逻辑未改。追加8个精确Remote版本正反例（尚未本次NUnitGREEN）；不与Cng策略耦合。局部format通过。按前驱hash明确Sync同版本r6，新SourceIdentity回执，owner/PFX/原构建不覆盖。C1b为新Development构建，C1原拒绝证据不改。
- `mdev-seed1`在C1b真实正常exit0/Passed=true，完成pin+签名+下载/准备/激活V2，Store=`m-remote-main`；server-mode明确run=mdev-seed1。之后完整Security组以RunPrefix=mdev-/StorePrefix=m-串行执行，tracked后台=`bgp_17e19090d001R5rdB8pObKpJSe`，120秒monitor返回仍在跑，已看到active-restart、untrusted-tls、wrong-pin、signature、catalog-hash、redirect、missing、encoding、stall、rollback/重启、old/conflicting sequence、same-release、transient共15项实际exit0，**后续corrupt/floor等及最终脚本终态未读取，不能记全组通过**。
- 当前server必须保持同一tracked实例，不启动第二server/不并发改mode；下一回先收Security组终态/原始请求日志。无Player/Unity新动作前须确认该组已退出。接下来C2 IL2CPP Development、保存Bee证据，再R1非Development本地与FileWatch分层；不要把C1b正例当IL2CPP/ReleaseHTTPS/音频播放通过。整体Goal仍进行中，未满足全部门禁。
- 16:20Z恢复先收原tracked Security进程，已exited、末行`R810 remote Security suite finished`；尾部19项process receipt全部Passed=true/exit0/TimedOut=false，补齐corrupt-body、corrupt-restart、failed-confirmed-floor、after-failed-valid。独立内部只读取证正在逐项核声明/结果/BuildGuid/请求关联，尚不将这些布尔值独自作为完整HTTPS门禁。原mdev-seed-red1与C1不覆盖，唯一server仍为PID48072/同tracked实例。进程检查无ResourceAcceptance Player或owned接收Unity，只保留源5556、其workers和Hub；不记录Hub命令行。
- 下一独立动作是r6/C2真实IL2CPP Development、Low/Release、acceptance与candidate宏构建，仍6000.3.27f1/UGUI2，沿用r6显式SourceIdentity而不冒充candidate9包。C2目标事前不存在、Builds和证据父目录存在；构建结束后须立即调用Save-ResourceIl2CppBuildEvidence保存实际ManagedStripped/cpp/native，之后才运行Player或构建R1。Security详细取证与构建无共享写入，子任务不触碰Unity/服务/mode/证据。
- C2在16:25:14Z正常Unity exit0，实际BuildReport为6000.3.27f1/IL2CPP/Development/Low/Release、Errors0/Warnings14、BuildGuid=`668b4e1d884443f6a1b328bdebdf403f`、SourceIdentitySha256=`847e39b8335d21877e28ae896a409267ba1ec52dde128e859aea3cd7857754ed`。立即实际Save归档`resource63/BuildEvidence/C2`成功：1851 source files、104 stripped files/57 assemblies、410 generated cpp files，含实际native/metadata；未在归档前构建其他身份。
- C2首个Remote入口RED为工具未转发CandidateValidation，Invoke在创建Player run之前严格拒绝`BuildReport candidate policy differs`。仅给Remote runner增加显式switch转发，未改报告/产品守卫；错误Mono+candidate仍真实拒绝`Candidate validation is IL2CPP only`。`idev-seed1`以独立i-remote-main实际HTTPS更新至V2、正常exit0；随后C2完整Security19项均正常exit0/Passed=true/无timeout或kill，脚本于16:30:44Z完成，包含全部拒绝/回退/损坏恢复。详细原始证据核对仍待收口，不把Security子组当完整网络矩阵。
- 发现Resume快照原文件名不含Store身份，会在第二后端复用时覆盖证据。先运行独占Temp `Test-ResumeEvidenceGuard.ps1 -Run red1`，collision用例实红（已有快照未提前拒绝），fresh/other-group两项保持原路径。最小改为`resume-<StorePrefix><case>-before.json`，已存在时在Player/mode动作前拒绝；仅既有StartAt=<case>-complete可在实际partial路径/长度/hash/metadata完全一致时只读复用而不重写。`green1`三项实际通过、PS解析0错误、局部diff检查通过；不是Unity/native证明。
- 16:33:48Z `mdev-range206-partial`实际exit1/Passed=false，保留全部原件。Player.log:56-57为Curl7/localhost18443连接失败，check.TakeResult前失败、Steps=[]，没有进入partial/取消。随后background status明确原server ID不存在，TCP检查18443无监听；不能归因TLS/续传或更改产品重试合同。安全接续仅允许确认无旧实例后启动同owner证据根的新tracked server（请求日志追加ServerStart），用全新mres1-/mr1-身份继续Resume，失败mdev run与Store不覆盖。
- 已按上述无监听前置启动唯一新server=`bgp_17e375ae30018QI0f9YU1ohoSq`/PID53244，同owner/PFX/证据根，ready18443；不是并行第二实例。Mono C1b用新mres1-/mr1-完成Resume六种×3进程=18项：range206、kill、ignore-range、changed-etag、bad-range、weak，实际每个partial为1048576bytes，后续完成与V2重启正常exit0；仅声明的kill-partial为ControlledKill=true/exit-1，非超时且checkpoint身份守卫通过。原Runner对真实Range/If-Range/206或200、坏Content-Range从零重下、weak不Range的断言全部执行，16:38:25Z脚本正常完成；独立原始请求/快照/进程取证正在补核，不把受控kill记为正常退出。
- 进程口径纠正：tracked `bgp_17e375ae30018QI0f9YU1ohoSq`的PID53244为PowerShell启动器，实际新node/server PID49740（server-requests的ServerStart，16:35:19.578Z）；旧server实际node45292而非旧tracked启动器48072。归因按ServerStart/UTC/run，不把重启后request.number复位当作日志重复或覆盖。
- C2使用独立ires1-/ir1-完整Resume18项于16:43:02Z结束，全部partial同为1048576bytes；每组complete/active-restart正常exit0，唯一kill-partial严格ControlledKill/exit-1且无timeout。12份两后端快照命名分离，未覆盖Mono文件。
- Mono `mdev-` Recovery11项于16:45:00Z完成，C2 `idev-` Recovery11项于16:47:52Z完成；各自prepare-only/restart/activate/active-restart正常，BeforeCommitDecision/CommitDecided真实kill后分别恢复From1/To2、confirmed floor检查及随后有效更新均通过。每后端仅声明的2项kill为受控exit-1，其余正常exit0，无timeout；原Runner实际执行恢复后的floor集合校验。
- Mono/C2各5项Encrypted网络组于16:48:28Z/16:49:14Z完成，均实际exit0，包含online/restart/missing/wrong/valid；原FileWatch保持启用，全部Errors=0/Plaintext=0。必须保留有限观测口径：online的ProbeMisses分别91/88、metadata-only控制项134，归因Unavailable，不宣称覆盖全部瞬时写入或系统范围无明文。每个后端当前声明执行为Security19+Resume18+Recovery11+Encrypted5=53项，另各1个seed，共108次Player（不包含2个保留RED）；详细逐项原件/请求核验与新严格checker尚需收口。
- 独立只读取证已核实C1b Security19项参数/身份/消费与拒绝层、377个PlayerFiles和1851个SourceIdentity全部字节匹配；C2对应19项及678个PlayerFiles、归档1851source/104stripped/410cpp/4native全部长度/hash匹配。C1b/C2 report SHA分别`ecbaea2c6167ecef33fd9b3384456ac88291c1defc72c80e3063fb290261551e`/`903470946e771ac2da4da67bfa6682658d2e720bf398849f4bdf2bd3dbcefc60`。这不替代尚未完成的完整组checker、R1非Development、本批新包NUnit、IDE/输入/可选集成及最终门禁。
- 已通过background stop终止本批唯一server启动器及子树；后续R1不依赖网络。R1实际16:51:57Z构建正常exit0，Report Succeeded/Errors0/Warnings14、6000.3.27f1/IL2CPP/Options=None/Development=false/Low/Release、CandidateValidation=false、无acceptance额外宏，BuildGuid=`e071a59f7918479586b7570b9abb0c93`，SourceIdentity与C1b/C2相同。立即Save归档`BuildEvidence/R1`：1851source、104stripped/57DLL、400cpp及真实native/metadata，Passed=true。
- `r1-none-`18项于16:54:46Z完整结束，均符合原Runner；四个JournalVerified/ActiveWritten:r810-a/BeforeCommitDecision/CommitDecided kill为受控exit-1，其余14项正常exit0、全无timeout。覆盖取消、prepare/restart、Busy与激活、回滚、真实抛异常恢复、kill前后整体From/To选择、仅owned active损坏及LKG回退；未修改历史checker或杀未知进程。
- `r1-encrypted-`14项于16:56:38Z完整结束，均正常exit0/无timeout：缺/错密钥、cancel、prepare/restart、Busy/activate/restart、rollback/restart、reactivate及V2缺/错/有效密钥。全部启用FileWatch且Errors0/Plaintext0；ProbeMisses与metadata-only控制项保留有限观测合同。完整逐项原件/裁剪/计数独立核验已追加给同一只读任务，R1及Development严格checker尚未形成，不提前勾选Task5整体门槛。
- 新候选前MCP已重新只读核实源Project=原G盘工程、2022.3.62f3c1/PID5556、非Play/编译/更新、scriptCompilationFailed=false，唯一YLFrameworkDev原场景isDirty=false。6个当前Resource正式/回归文件Format Check实际通过（SDK9.0.312）。candidate10与全新v6/w6目标均事前不存在；接下来正式守卫导出并Audit，先无harness首次导入后才加测试。v5 native退出RED仍冻结，不以新候选或R1成功宣称它已修复。
- 正式candidate10已守卫导出：4553915bytes，SHA256=`be32c3f9e6b9a4f2739b08500b0d1b35984a0dc48df37e64081a1be76716f6e8`，AuditPackage实际1038条通过，正式内容含Publisher唯一private7参和Remote双精确候选。最初snippet为using-var作用域编译失败，未执行导出；改用完整类静态方法后成功，未保存源资产。新w6（6000.3/UGUI2）与v6（2022.3/UGUI1）已真实Create/同字节无harness首次Import，均正常exit0；回读first-import/Assemblies/Errors=[]与最小依赖，包身份一致。不是最终发行包或NUnit证据。
- w6仅在上述first-import保留后声明固定Test1.6/Input1.20.1/Rider3.1/VS2.0.26，InstallTests/tests1完整闭包已登记；未手改lock。`TestsLegacy/legacy1`的tracked monitor意外返回stopping后ID已不存在，外部仅有process receipt（Unity50200），无exit/profile回执，log止于编译DAG；OS检查该owned Unity已不在，只有源5556。无证据确认正常退出或业务编译失败，不伪造exit、不覆盖legacy1原件。v6保持raw-import身份，未机械重跑v5暂停的native退出路径。
- Ruling：为避免等待被session/group生命周期中断而丢失自有watchdog，接续仅对当前w6/legacy2配置进程使用tracked persistent lifetime；仍原脚本1800秒截止、只控制自身树，完成后由本批显式收口，不改系统/权限/源Editor。若再次中断则先核实际PID与现有回执，不并发启动。下一步先等legacy2真实终态，再Discover/list1、Prepare新cost-w6 owner并执行Publication精确10项与CNG/Remote版本各8项；整个Resource严格checker及只读剩余流水核验并未因换接收端被视为完成。
- legacy2已实际以persistent tracked `bgp_17e519e38001U8A36X8IB9w1uk`启动（wrapperPID50736），另排单次wakeup=`wku_17e51ca73001ubpG0BnrqF4gZI`/17:06:11Z自动收终态。当前为有调度的正常等待，不报告blocked/complete，不先启动下一Unity。剩余流水只读任务仍待返回，不能因初始Security审计结束而停止后续矩阵核验。
- 17:06Z唤醒已收legacy2真实正常exit0（17:04:16Z），ForcedExit/ExitStalled/TimedOut均false；profile Input0、SpriteAtlasV2、正常DomainReload、独立Company/Product、Map层及owned干净bootstrap齐全。w6随后官方Discover/list1实际Edit2613/Play791，完整发现与companion1/1正常exit0，无挂起；UPM实际lock为Test1.6/NUnit2.0.5/Input1.20.1/Rider3.1/VS2.0.26，源依赖未变。
- 新cost-w6已Prepare，Repository严格为w6、Scope本RunId、Purpose=Resource-cost-and-publication，非生产PFX/未安装系统信任。`w6 publication1`精确10项实际9/10、正常测试失败exit2；Io未进入新取消接点，在IoTests:21因标准AssetBundles/YLFramework父目录尚不存在失败，其余Retry9项通过。finished.EnvironmentRestored=true，原RED保留，未把它解释为Publisher失败或原晚取消竞态复现。
- 最小只改Io fixture：RequireRunOwner后先对完整随机root执行RejectLinks，再显式创建标准输出父目录，保留原存在/随机root不占用/标记/清理/全部128次真实IO和OCE断言。它现在可独立运行，不再隐含依赖成本A先建立目录；不改变产品或跳过父目录/链接检查。单文件format/diff检查通过，w6仅SyncTestSources/tests1→tests2，formal仍candidate10不重绑定。
- `w6 publication2 -ExpectedTestCount 10`实际 **10/10 passed**，17:10:46Z正常Unity exit0、无强杀/挂起/超时、Difference/NonPassed/SuiteFailures=[]；CostEvidencePassed/PassedGate/Passed均true、EnvironmentRestored=true。Io probe实际UnblockedReplacements128；blocked-reader/changed-from真实HRESULT32，晚取消接点1次、Token已取消、NextExists=true，堆栈为正式AtomicWriteCore:1245原取消检查抛OCE（不是替身抛异常）。From/Next与原token严格断言均通过，测试自己随机目录已按owner清理；不据该结果推断自然1175所有者或原因。
- 为补纯Remote8项断言的可追溯旧Runtime基线，只对冻结candidate9/x同步已登记测试源码tests1→tests2，formal/owner/包字节不变，旧CNG8绿色不覆盖；新Discover/list2正在串行执行。该基线应因旧Runtime无新纯版本决策方法而RED，随后在candidate10/w6验证两类精确版本共16叶。此项不是重跑v5 native退出，不调用源同步NUnit。后续仍需Codec/正确重签后scope/compatibility及实际ValidateSnapshotPlatform消费者负例，不以密钥拒绝或正确版本Player正例代替。
- x/list2真实发现及companion已正常通过；`x remote-version-red1`精确8叶全部真实RED，均为版本决策方法null断言，不是缺测试或编译失败，Difference=[]、正常测试失败exit2、无超时/强杀/退出挂起。随后candidate10/w6 `version-policy-green1`两类8+8共16/16全部Passed，17:15:16Z正常exit0、ProcessPassed=true、Difference/NonPassed=[]；精确正例和错后缀/错patch/beta/空/null未放宽。Publication10+版本16及同字节首次接收这一批已具实际证据，但不是双引擎全套或Task5整体完成。
- 新增唯一测试`ResourceContentUpdateTests.RuntimeInitializationRejectsIncompatibleSnapshotsBeforeNativeLoading`，仅复用封闭UpdateFixture增加明确platform/unityCompatibility可选值，不改产品。其目录按自身声明Seal/Read及OpenChecked完整验证（含Complete/所有payload），wrongUnity另先证明public Open仍在Codec层拒绝；再走真实CreateCatalog→InitializeAsync，分别严格Unsupported/指定平台版本原因、Current=null、零native、Store未创建、Catalog字节不变，最后合法空配置实际重初始化及Shutdown。未伪造Application.unityVersion/BuildTarget，不把Codec早拒绝冒充Runtime gate；OpenChecked为已有后台捕获兼容值入口，不移除任何门禁。
- 已读实际ResourceLocalSnapshot.cs:95-129确认public Open会按当前Unity读Codec，避免夹具错误先在Open抛错。测试迭代包装遵循EnterPlay→独立静态主体→ExitPlay，不跨Domain Reload捕获fixture。两次格式和局部diff检查通过；w6源登记为tests4（tests3初版未执行，保留回执），formal仍candidate10。新w6/Discover list2已以persistent tracked `bgp_17e60b6a00019sUV6atB25lWbd`启动，尚待真实终态与新叶选择，不能记为新消费者测试通过。后续还须回归受同fixture默认路径影响的既有ContentUpdate全集及Codec5/重签scope1。
- 独立raw追加审计已核剩余82次：73正常exit0、9受控kill exit-1，全无timeout；8个密钥负例为精确ContentKeyUnavailable/ContentAuthenticationFailed，R1无remote/无HTTP；20份新增Store闭包/360 payload逐字节hash一致。连同先前两组Security/Mono Resume及两个seed，本批140个已声明成功Player为130正常+10受控kill（不含2个保留RED）；外层PowerShell suite exit并非均单独落盘，不能补造。
- FileWatch边界补充：`idev-encrypted-restart`摘要ProbeMisses=2，writes.jsonl仅重建1条miss；代码Observe在锁前计数、Dispose可使已计数事件不落行，与关闭窗口相容但缺失事件无法从原件唯一重建。冻结摘要和原始行，不把它改成1，不声称逐事件完全闭合或证明内容泄露。控制项标签实为ControlEntryNotProbed，Scope/Attribution在receipt而非每条原始行；严格checker应保持Errors/Plaintext及控制/可观测性分层，不能靠丢miss或制造日志一致性冒绿。
- 独立raw任务现已完整返回，不再有待取证子任务：追加82项的277HTTP为C2 Resume139、两后端Recovery各60、Encrypted各9、R1零HTTP；Verified恢复整组V1且清空未确认控制/floor，Committed恢复V2/floor2，R1四检查点原磁盘active 1/1、2/1、2/2、2/2分别整组恢复1/1/1/2。R1坏active/LKG及20份新增闭包360payload均匹配。R1 Report SHA=`7714510cd09437203b0ecc9a2b053de97131fad1c532b5df7be8729f05e51c75`，只读PE证实ResourceRemoteSource存在而TrustLoopbackCertificate定义0；32次实际R1宏全部false，Low/Release。该本批R1执行及raw段已完成，不代表Task5全部或最终Review。
- FileWatch完整聚合仍为有限观测：24份记录有228次完整8B探测、0 UnityFS；322摘要miss/raw321；350控制metadata-only、Errors0、AttributionUnavailable。14次实际encrypted消费的CNG provider/key各2/2、keyobject6524B/native readbuffers65536B，peak589728B<128MiB，正常退出全归零；受控kill不伪造正常释放证明。既有包/目标不因本审计改身份，历史/失败原件均保留。
- w6/Discover list2已正常exit0、完整Edit2614/Play791、companion正常，新增消费者叶实际发现。`runtime-compatibility1 -ExpectedTestCount 1`于17:22:50Z真实1/1通过、Unity正常exit0/零skip/退出异常；两类foreign Catalog在真实ResourceSystem路径按指定Unsupported原因拒绝、public Open的早拒绝独立保留，并证明随后合法空配置可重初始化关闭。不是纯helper反射或Player负例证明。
- 受同一UpdateFixture默认OpenChecked路径影响的其余30叶另进程完整`content-fixture1`回归，于17:24:51Z **30/30 passed**、正常exit0；Difference/NonPassed/SuiteFailures=[]，CostEvidencePassed/PassedGate=true、EnvironmentRestored=true。唯一新UnityTest按准确名字排除，因为它已在前一隔离进程实际通过，而非删测/跳过；31叶均有本次结果，未重跑无关成本A/B/C。
- 新`compatibility-scope1`准确6叶（Codec身份5+实际合法重签的scope/expiry/compatibility1）已以persistent tracked `bgp_17e66d1fd001IKo64sO47o5arO`启动，PFX只取新cost-w6本批环境；尚未收终态，不能预记通过。已排单次wakeup `wku_17e6710c2001p0h3eyBGrkw6re`/17:29:26Z，先读取XML/选择/进程和finished双终态，再进入严格新目标清单checker，不与下一Unity并行。
- 源保护checkpoint4实际2553→2553、Passed=true，AllDifferences/AuthorizedDifferences/UnapprovedDifferences全空；未提交、推送、切分支或改源依赖/权限/设置，也未接管v5挂起。仍在integration/xf-yl，用户初始修改/未跟踪输入保留。当前新改仅两个测试fixture及Plan；之前产品/工具成果未回退。严格新目标/用例清单checker尚未实现，后续任务与最终门禁仍未完成，Goal继续。
- 17:29Z已收`compatibility-scope1`真实终态：精确 **6/6 passed**，17:28:22Z正常Unity exit0，无timeout/kill/退出挂起，Difference/NonPassed/SuiteFailures=[]；CostEvidencePassed/PassedGate=true、EnvironmentRestored=true。覆盖合法Seal后的SchemaVersion/BuildTarget/Platform/UnityCompatibility/AppContentCompatibilityId及实际合法重签后project/channel/platform/Unity/app、expiry与错Catalog拒绝，不用坏hash/签名或正确Player正例冒充身份拒绝层。
- 新增独立`Test-ResourceCompatibilityEvidence.ps1`和`Test-ResourceCompatibilityEvidenceGuards.ps1`，未改历史固定checker。输入显式冻结三role/build/report/source指纹、Run/Store前缀，不接管旧身份；完整声明140个唯一case，不按目录数/Passed布尔猜测。校验参数全数组、PID/BuildGuid/MVID或native identity、宏/graphics/audio上下文、正常和kill分支、完整步骤/版本/digest、严格数值零责任/native计数、控制/floor/journal、HTTP数量/时间/范围、真实part历史前缀与最终server字节及Range/If-Range/200回退。Player/归档文件集精确闭包并逐字节hash，拒绝未知额外制品/链接/路径逃逸；源/Player无运行写入。
- 守卫先有缺checker真实RED，随后逐层加入且实际观察unknown-artifact、请求分组变量覆盖、string/bool伪零计数RED；最终`VerificationGuards/green9.json` **45/45 passed**，含正常/kill/密钥负例接受及稀疏结果、exit/timeout/PID/版本/宏/参数/资源/明文/观察器/文件闭包拒绝。中间red与green1-8、gate1/2失败原件保留，均为新checker自身实现/测试阶段，不改原Player结果或削弱断言；未把它们记成产品回归。
- **纠正此前FileWatch审计结论**：idev-encrypted-restart writes.jsonl:4的Header=""是零字节读取，按观察器源码64-66同样计miss；再加行6 FileNotFoundException，raw明确为2，与摘要2一致。之前raw1、总raw321及推测Dispose遗漏1事件的结论错误，已通过board原线程更正；不删原记录，但不可继续引用为当前事实。旧draft `compatibility-targets.json`保留不再使用，正式输入`compatibility-targets-v2.json`不含任何观察豁免，checker明确拒绝ObservationExceptions；新增零字节头计数/删一事件拒绝守卫均实证通过。
- 当前严格成功收据`resource63/Verification/gate4.json`：Passed=true、Failure=null、130正常exit0+10受控kill，三构建PlayerFiles为377/678/666、各1851来源项，含C2/R1真实Sources/ManagedStripped/GeneratedCpp/native闭包和pin编译边界；12个续传组的历史prefix/current完整payload及真实HTTP闭合。归属/目标Metadata逐字段不变，ETag按既有Parts协议允许200重下合法更新且必须等于成功响应，不能把validator更新误判成换归属。Targets SHA=`9be8e70f370ea01f7ac8bdbf0e16b153a5ace11198863b0d3a47423b418db796`，Checker SHA=`779b815eead8f5eda36403bf3a7fd3bfdbe5a8f757a3f60795d38f78092dfb94`；gate3亦通过，gate4增加数值类型严格性后重新完整验证。
- 该收据实际重算24份FileWatch：**228完整8B头、322 raw/summary miss严格相等、350控制metadata-only、Errors/Plaintext=0**；ObservationCountersMatch=true，但FullWriteObservationProven=false、AttributionUnavailable。只证明本批有界观察的记录/计数及未观察到UnityFS，不声称全系统无明文、进程归因或音频DSP。
- 两个新PS的Unity `.meta`已由源受身份/非Play/非编译/原场景干净守卫的AssetDatabase.Refresh生成，GUID=`3aa4eb96b3324c348a0314c8d66530ee`/`a12b5d45260773a4daf9e77e6ccdcc85`，DefaultImporter、未手写GUID/改旧身份。刷新返回Compiling=true后安排单次wakeup；18:18Z已MCP只读确认源仍2022.3/PID5556、非Play/编译/更新、scriptCompilationFailed=false，唯一YLFrameworkDev场景干净。不是源同步NUnit或另开Unity。
- 本批Resource原生/不匹配输入/checker收口已具上述实际证据；Task5其余适用全量闭包及双引擎最终包仍不能由该段代表。下一独立只读子任务`ses_e816c1396ffee1dN3Zrlx9bP0Y`正在定位Analyzer真实作用域/IDE配置和旧工具最小参数化接点，不写源码/Plan、不启动Unity/IDE、不改默认IDE/权限/owner；等自动返回，不轮询或重复其检索。整体Goal仍active，v5 native退出冻结、Analyzer/IDE、原生输入/URP/手动UI/可选集成、最终同字节接收/整分支Review/最终protected-after均未完成。
- 剩余重复Prepare红灯已在当前candidate10/w6独立复现：`prepare-repeat-red1`精确1叶实际失败、18:24:52Z正常测试失败exit2、无挂起/超时、环境恢复true；XML堆栈明确在ArtifactStore.cs:999 ReceiptShaStarted触发旧测试的blanket Assert.Fail，非复制/Complete写入。旧RED保留。
- Ruling：以已实现并文档化的每次Prepare完整认证合同为准，不为旧“DoesNotCopyOrVerifyAgain”测试跳过SHA/内容验证。仅将测试更名`CheckIsPureAndRepeatedPrepareRevalidatesWithoutRewriting`并要求ReceiptSHA首尾+按独立Catalog Bundle/二进制manifest数计算的全部ContentSHA首尾序列；任何额外安装/元数据写入checkpoint仍失败。原磁盘闭包Hash、取消前不创建Store、Prepared目标身份/重启断言保留，新增全部既有文件mtime严格不变。产品ArtifactStore/receipt没有修改，也未将任意验证回调当作持久写入。
- 同步现有Resource.md的必要使用合同：重复Prepare仍完整复验，仅复用安装/相同准备记录；远端及IL2CPP硬检查精确2022.3.62f3c1/6000.3.27f1固定候选，不声称其他引擎、位数、主机、裁剪或默认CA正向联网支持。无新文档台账/依赖/API，文档进度/数量仍只在本Plan。Format Check与局部diff实际通过。该文档增量未写入冻结candidate10，最终包仍待整体收口，不混同当前载体字节。
- w6只同步登记测试源tests4→tests5，formal仍candidate10；官方Discover/list3实际Edit2614/Play791及companion正常，18:31:42Z正常exit0、无异常退出。新单项`prepare-repeat-green1`已以persistent tracked `bgp_17ea35ee6001XJ8KBLlB4W25IP`启动，profilelegacy2/Prooflist3/Expected1/cost-w6；尚未收终态。单次wakeup `wku_17ea3de70001aML75ehpxDDCFJ`/18:34:50Z已排，先核XML/进程/finished双终态，再开始下一Unity，不预记GREEN。
- Analyzer只读任务已完整返回，未启动IDE/Unity或写入目标。准确入口：菜单`YLFramework/Core/分析器规则`、Project Settings `Project/YLFramework/Analyzers`；asmdef同目录叶子、四种精确Assembly-CSharp叶子，只有显式Apply才写/重编译，已有Error时页面仍可恢复。77项里AssemblyBuilder见证不可冒充自动程序集/真实IDE；下一批须新owned业务asmdef+预定义程序集，YL0008/9/10负例→仅修Inject保留两个控制诊断→全正例，分别验证scope隔离、None/继承、pragma、真Editor Error恢复、重启持久性。
- 当前默认IDE只读确认仍Code.exe，w6因此由VS包SDK generator生成.slnx；Assets尚无ruleset，csproj无CodeAnalysisRuleSet属真实未配置，不是新版本自动迁移。可在不切默认IDE下用`Packages.Rider.Editor.RiderScriptEditor.SyncSolution()`生成；VS2022须按指定devenv安装取得ProjectGenerator.Sync反射接点，不能用`Cli.GenerateSolution`临时SetExternalScriptEditor。两生成器会覆盖同名csproj，需串行生成/打开/诊断/重生成，读取实际SolutionFile()不改名生成文件。w6尚无该VS桥接，不伪造已可调用旧类。
- 旧`YLAnalyzer-20261024T1238`中的Run-AnalyzerReceiver、VsDteHost、Invoke-VsHost、Start-OwnedRider/Invoke-RiderMcp/Capture-OwnedRider及两个checker全固定旧project/owner/PID/pipe/端口/窗口尺寸/文件集；仅借鉴必要逻辑，在本批新owned根参数化，不修改旧文件/旧回执、不启动或关闭用户IDE。旧VS消息matcher与当前中文诊断文本已不符，不能改产品文案迎合它；DTE无raw ID、AnalysisCompletionKnown=false，不以首次空列表签字；Rider须拒绝timedOut/more/notAnalyzedReason，pragma若依赖HWND证据必须新核范围，不能沿用旧像素。
- CoreEditorIdentityTests整类为source-host，receiver过滤器不会因TestFilter自动放开；其中合并Runtime正反例硬要求五个XF Error及已加载XF/原包GUID，不可在无XF的w6冒跑。可复用YL0008/9/10实际输入算法但须新owned真实消费者；不使用SDK stub Runtime、缺类型C#错误或旧Bootstrap冒充。新Analyzer验收尚未实施；sourceEditor5556、不改默认IDE/源设置/依赖/权限与v5 native暂停约束继续有效。
- 18:34Z已核`prepare-repeat-green1`增强单项真实 **1/1 passed**，18:34:39Z正常Unity exit0，Difference/NonPassed=[]、无timeout/ForcedExit/ExitStalled，CostEvidencePassed/PassedGate=true、EnvironmentRestored=true。旧“不复验”合同冲突已通过保留完整认证和增强不重写断言收口，产品没有跳过验证。

</details>

<a id="h11"></a>

<details>
<summary>H11 Analyzer 真实消费者、VS 原生诊断与规则恢复</summary>

- Analyzer新入口先后真实RED：analyzer-prepare-red1在既有Method ValidateSet拒绝，未启动Unity；仅新增明确TestsAnalyzerPrepare/TestsAnalyzerInspect两项后，red2实际Unity日志367确认method not found、exit1。未移除旧方法/扩大任意类型执行。随后在已登记Stage9ReceptionTests同文件新增两个owned helper：Prepare只向此前不存在的Analyzer-w6根与新输入写入；Inspect只核真实Editor/Player编译图，不冒充诊断/IDE通过。
- 新输入设计为真实Assembly-CSharp `Assets/IdeDefaultProbe.cs`与直接引用正式Runtime的`Assets/AnalyzerConsumers/Analyzer.Acceptance.Consumer.asmdef`/IdeDiagnosticProbe.cs，每个作用域YL0008/9/10各1个Warning；规则通过原AnalyzerRuleSetFile.Preview/Apply精确反射调用，不新增产品API/SDK stub/父Default策略。owner绑定Project/Unity/正式包/DLL哈希/原默认IDE/Default.ruleset原哈希/精确5文件hash+meta+GUID；缺少或已存在输入/旁路link/脏场景/播放或编译错误均拒绝。没有保存或重建场景，也未开始IDE进程。
- C#桥接单文件格式检查已通过，SyncTestSources tests5→tests6只同步登记源码、formal仍candidate10。新增入口守卫复跑有一个已定位输入时效限制：green5最初Package误传全路径，失败原件保留；green6使用bare正式包后5/6通过，Missing export root在更早Source payload hash校验因合法更新的Resource.md与冻结candidate10不同被拒绝，不能把该早拒绝冒充AGENTS缺根门禁，也不能放宽matcher/更新旧审计。待当前owned Unity退出后，核源守卫正式导出含Doc的新candidate11并Audit，使用该新包重跑精确六项；不重绑定w6/candidate10或改旧归档。
- `TestsAnalyzerPrepare/analyzer-prepare-green1`首个新入口候选已以persistent tracked `bgp_17eb60097001E0vB1Hq4JYz6fZ`启动（wrapper53876）；尚未核终态，不能声称新输入或编译图已通过。单次wakeup=`wku_17eb6608f001PgBFYdFdQhYsQ2`/18:55:03Z已排；先核exit及Analyzer-w6 owner/receipt，失败时保留已标记的部分根、不得用相同Prepare认领/覆盖。随后独立Inspect与真实严重度/IDE尚待实施。没有未返回子任务或运行loopback服务，整体Goal继续。
- 已收Analyzer Prepare真实正常exit0（18:53:48Z，无timeout/ForcedExit/ExitStalled），owner与5文件hash/meta/GUID齐全，外部编辑器仍Code.exe、Default.ruleset原本不存在且未创建。prepare收据Compiling=true仅表示输入已请求编译，不被当作诊断完成。
- 新进程`TestsAnalyzerInspect/analyzer-graph1`于18:57:24Z正常exit0；4个真实Editor/Player图条目精确绑定正式DLL哈希、直接Runtime引用及两个局部ruleset，CompilationFailed=false。日志为每源YL0008/9/10各1个Editor Warning，编码原始/格式化回显各一轮共12行，实际6个唯一诊断；未看到CS Error/AD0001/CS8032。Player图是编译输入见证，不冒充Player-profile实际诊断或IDE结果。
- 六项入口守卫的Doc时效输入已按安全合同解决：源MCP重新只读核原工程/2022.3/PID5556、非Play/编译/更新/失败、原场景干净，再原Export API正式新导出candidate11，4553996bytes，SHA=`f53224654f3c974d6583d1154fb6cbd49f34a7b516120a824a62ec2592021e1f`，Audit1038条实际通过。仅正式Doc增量，不覆写旧包/审计或w6/candidate10；`Test-Stage9DeliveryGuards -Run green7`在该新包上真实6/6通过，Missing export root没有用更早payload拒绝冒充。
- 新IDE生成接点先以Method ValidateSet实际拒绝`analyzer-vs-red1`，随后只增两明确方法TestsAnalyzerVisualStudioProjects/TestsAnalyzerRiderProjects。反射复用当前已安装包：VS按新owned `Analyzer-w6/ide-targets.json`中指定17.x devenv取得installation.ProjectGenerator.Sync；Rider仅SyncSolution并取实际generator，未调AndOpen/SetExternalScriptEditor。每次要求owner输入/默认IDE/父策略不变，读取实际SolutionFile并检验两个csproj包含正确Analyzer/hash/ruleset/探针后冻结三份生成文件；不手改生成文件。
- 桥接Format Check通过，w6登记源tests6→tests7；`analyzer-vs-generate1`于19:04:54Z正常exit0。实际Generator=`Microsoft.Unity.VisualStudio.Editor.LegacyStyleProjectGeneration`，Solution=`w6/w6.sln`（sha68bc3ce1c14ac202e3d73a2886da67b42b4e7b1c7ab9935972b864e7733bf1b4），原.slnx未改名；两个csproj各含正式Analyzer、各自局部规则集和真实Probe。`Analyzer-w6/analyzer-vs-generate1.json`与`analyzer-vs-generate1-files`冻结，DefaultEditorUnchanged=true、IdeLaunched=false。Rider生成方法尚未运行；在VS生成/打开/原生诊断/关闭周期完成前不运行另一个生成器覆盖同名csproj。
- 下一实质工作为新owned VS观察host/调用器最小参数化，复用旧成熟DTE协议但不改旧Root：参考旧VsDteHost.cs、Invoke-VsHost.ps1、Prepare-VsHostDependencies.ps1。原host会CreateInstance VS并finally无条件Quit，移植必须先核新PID/启动时间/executable、拒绝既有/user-controlled/未知solution，只有已验证自有实例才允许关闭/退出；新pipe/output/solution不得沿用ReceiverA硬编码。诊断仍保留AnalysisCompletionKnown=false/无raw诊断ID，不以首次空列表通过；需要bad→只修Inject→all-good控制序列及原生Error/pragma/重启持久性。当前没有实际IDE进程由本批启动，所有owned Unity job均已终态，无子任务/loopback服务；整体Goal仍active，未报告complete/blocked。
- 19:11用户明确要求不中途停止后继续实际IDE阶段，未把上一阶段摘要视为Goal完成。新增OwnedVsDteHost.cs.txt、Prepare-OwnedVsHost.ps1、Invoke-OwnedVsHost.ps1；在本批Analyzer-w6新子根编译，未改旧YLAnalyzer根。准备入口先实际RED缺实现，VS-host1真实C#编译发现Process类型二义，修为System.Diagnostics.Process显式别名；失败根/源副本保留不复用，VS-host2编译并7项进程所有权自检全通过、尚未启动IDE的收据独立保留。
- 新host以当前用户专用新pipe/nonce绑定，实际启动VS PID43340/host51944（tracked启动器13244）；先拒绝已有PID、旧启动时间、错误executable、UserControl/未知solution，再允许自有状态操作，未确认所有权只Release COM不Quit。调用器最初对Windows路径大小写和PS7.6自动DateTime解析的精度误拒，已按OrdinalIgnoreCase规范化绝对路径、ready JSON DateKind=String保留完整启动时间修正；PID/完整ticks/二进制hash仍精确匹配，无宽限替代。未写源/用户默认IDE或启动构建。
- VS-host2真实原生周期：两个文件均属各自C#项目、非Misc；6基线→只修custom Inject为5→两边Inject修正为4→custom全正例为2→双全正例为0→重引入为6；双Warning pragma仅去目标、控制保留，最后reset精确磁盘基线/6诊断/Saved=true。所有修改只在两自有未保存buffer，未保存磁盘。DTE Description实际是generic说明文本，不能据Unity动态消息推断旧matcher不匹配，也不声称raw ID字段；诊断family按已知自有输入/位置/控制序列推断，AnalysisCompletionKnown=false一直保留。
- 新Test-OwnedVsBaselineEvidence.ps1严格核9份原生快照、文件/项目/严重度/缓冲区状态/时序、宿主与三个Interop指纹、磁盘5文件/meta不变、已退出PID及safe cleanup；baseline-verdict1实际Passed=true，明确FullIdeAcceptance=false。Test-OwnedVsEvidenceGuards.ps1先缺checker RED，随后10/10 native空列表/错误PID/项目/级别/缺控制/重复family/未知诊断/虚假完成信号/错误Saved状态拒绝通过。VS-host2 cleanup Owned/SafeToQuit/QuitRequested均true，43340/51944实际不在，不关闭未知/user实例。
- 真Editor Error阶段只加TestsAnalyzerAsmdefError/DefaultError明确入口（先在Method allowlist实际RED）；通过原Preview/Apply仅改YL0008叶子，记录原bytes/hash、写后hash、meta与immutable owner hash，DiagnosticsVerified=false不冒充编译。登记源tests7→tests8、格式通过。analyzer-asmdef-error1写策略正常exit0，下一真实进程analyzer-asmdef-editor-error1正常预期exit1，日志明确custom YL0008 Error、YL0009/10 Warning；不是自造C#错误或AssemblyBuilder替代。
- Unity启动会按默认Code包重写csproj，实测两个csproj从冻结legacy hash变为SDK hash；不手改生成文件/不以旧snapshot签字。Restore-OwnedAnalyzerPolicy.ps1仅在owner/当前after/meta均精确匹配时恢复原bytes，先封存产品API产生的-after.ruleset，拒绝独立修改；恢复receipt不宣称CompilerRecovery。随后实际analyzer-vs-generate2于19:50:56Z正常exit0/custom三Warning恢复、无CS Error/AD0001/CS8032，读取真实Legacy生成器冻结新项目，defaultIDE不变。这是真Editor Error后可恢复，不是仅VS未保存buffer Error。
- 为原生VS Error测试，在新legacy生成后仅重放已归档并逐字节核验的产品API Error字节，不再启动Unity污染生成文件；记录apply/restore动作，不改owner/GUID/包身份。Prepare-OwnedVsHost新增显式GenerationRun和可选PolicyRun，验证所有未变输入及唯一选中叶子的精确baseline→after hash，不盲目接受当前目录。VS-host3绑定generate2/policy1，7所有权自检通过，新VS PID51696/host16548，tracked=`bgp_17eec7d35001booLcptFCOv3DG`（启动器51416）。
- VS-host3实际custom Inject原生Level4，default Inject Level2，双方其它控制Level2；custom Error pragma抑制后5行/无Error，移除后6行/仅custom Error，叶子精确restore后IDE实时转6 Warning，再重放Error后恢复仅custom Error，证据均按新文件保存，未保存probe buffers。已reset custom至Saved=true并请求stop-error-scope，准备新进程验证Error持久性；尚待核host3 cleanup/PID终态。当前业务Error叶子仍有意保留（sha2336450d4362e154ef85e883fc1c95441dea997c805158803d3016972cb89988），不要无记录直接改动或把RequireAnalyzerInputs的baseline拒绝视为产品错误。下一步新VS-host4同generate2/PolicyRun重启验证，随后安全restore原叶子，再做default反向隔离/None/继承/Rider及剩余强门禁。
- 此段新工具的Unity .meta尚未受守卫Refresh补齐，不漏最终导入/编译/保护复核；所有新工具仍Tests-only不进正式导出，candidate11/w6 candidate10冻结身份均未重绑定。无提交、推送、分支或源依赖/权限变更，无loopback/后台子任务；Goal未满足complete，继续实际工作。
- VS-host3安全cleanup已核Owned/SafeToQuit/QuitRequested=true，51696/16548实际退出。业务Error叶子保持同一2336450d...字节后新建VS-host4，7项所有权自检正常，新PID41080/host49224（tracked启动器38696）在20:01:58Z原生仍为custom YL0008 Level4/default Level2/其余控制Level2；不是复用旧进程缓存或丢弃策略。随后只关闭自有host4并核cleanup/PID均已终态，未关闭用户IDE。
- 已用同一带owner/after/meta前置的恢复器将custom叶子恢复baseline（analyzer-asmdef-final-restore1），业务原有源/规则身份保持。默认反向隔离入口TestsAnalyzerDefaultError已在tests8中，于新owned进程`analyzer-default-error1`启动，tracked=`bgp_17ef61703001BTkBymV52bX7AG`（wrapper40400），尚未收终态；它只写默认叶子的YL0008 Error，不能把策略写receipt当真编译/IDE通过。当前无活动VS/Node服务/后台子任务，不与下一Unity并行。
- 下一顺序：先核上述default策略receipt/exit；另进程观察默认源真实YL0008 Error、custom仍Warning，再精确restore默认叶子并实际编译恢复/读取generator新csproj（Unity会自动重写）；在新冻结VS生成后仅重放已归档产品API Error字节、启动新的自有VS做default反向隔离与Error pragma。现有RequireAnalyzerInputs baseline护栏不为Error状态放宽。之后仍需None/继承/Include、Player-profile真编译范围、Rider以及最终整体门禁，不能凭已通过Warning/业务Error子集宣告完成。
- default Error写入20:03:45Z正常exit0；下一真实`analyzer-default-editor-error1`20:06:20Z预期exit1，明确默认Probe YL0008 Error/9+10 Warning，无CS类型错/Analyzer加载错。精确restore后`analyzer-vs-generate3`20:07:06Z正常exit0且默认三Warning恢复，原Default.ruleset/源默认IDE/owner/GUID均未动。原Error/restore/生成回执分别保留，不把PolicyWritten当CompilerVerified。
- 新VS-host5绑定generate3/default-policy，进程25436/host34488（tracked启动器50800）；实际default Inject Level4而custom Level2，默认Error pragma后仅移除该一条、控制5行保持；reset磁盘负例后Error恢复；精确restore叶子后原生6Warning恢复。停止后cleanup三标记true、两个精确PID均不在，未接管或结束用户IDE。所有原生仍无raw ID字段/完成屏障，仅两owned文件范围。
- 独立只读追加审计已返回：核10个Error原生快照、27对请求响应、2条真Unity Error→恢复链、8次恢复重放方向及PID/time/源码位置/控制诊断，未发现不匹配，未写数据/启动进程。边界修正：host3 Error→Warning是在相同未保存bad buffer，后续重新Error之前已reset为磁盘源码，不能称“三段逐字相同buffer的纯策略往返”；回执自身仅存结果SHA/SourcePolicy，不是每份自包含observed-before/owner/timestamp，方向可从immutable policy与实际前置检查重建。其20:17:59Z的5输入baseline快照只代表该时点，不冒充之后None期间状态。
- None/继承只新增明确4方法并复用同一个SetAnalyzerPolicy action，先CLI None入口实际RED；不新增Runtime API/父策略/stub。为读取合法非baseline实验，仅新增AnalyzerPolicyRun，限定Editor Inspect/两个generator且C#再次白名单，验证exact owner/baseline-before/after/hash/meta/GUID，仅一个已有owned规则叶子可偏离baseline，mutation或其他Operation带此参数真实预启动拒绝。没有把原护栏改成无条件信任当前文件。桥接格式通过，w6登记源tests8→tests9、formal仍candidate10。
- `analyzer-asmdef-none1`经正式Preview/Apply产生None叶子（after ba03d9146ffc23d9e2edacc85b1da785c626e091335739a18a7bd9adc9d259ed），下一实际`analyzer-vs-none-generate1 -AnalyzerPolicyRun analyzer-asmdef-none1`20:19:08Z正常exit0、custom仅YL0009/10 Warning，Owner保留原值。新VS-host6进程46988/host56084（tracked启动器28264）原生custom仅2控制/default3Warning；精确restore后原生custom Inject返回/总6Warning。已stop并核cleanup三标记true、两个PID消失，所有磁盘5输入重新baseline；尚未执行继承方法，不预记通过。
- 无活动owned Unity/IDE时，源MCP身份/非Play/原场景干净守卫后只刷新外部新增工具，六份.meta由Unity实际生成：OwnedVsDteHost.cs.txt=1159da8458dbe1043ac011ca39be356d；Prepare-OwnedVsHost.ps1=496d8c9bea0f6074a8a6be64a3755e1e；Invoke-OwnedVsHost.ps1=837638d946041014e8ad07220759dc18；Test-OwnedVsBaselineEvidence.ps1=ea830a8e401fee84ebc369e71a24845b；Test-OwnedVsEvidenceGuards.ps1=4db0d78beed3138499fca6b4d3e01975；Restore-OwnedAnalyzerPolicy.ps1=13f9479f2a130a344aa60abef11f3b19。返回Compiling=true/失败false/场景干净，仅导入阶段，不当编译终态；单次wakeup `wku_17f08d802001pXiLb2a1V5rolt`/20:25:08Z已排，先核源编译再开下一owned Unity。
- 下一实质步骤为已有TestsAnalyzerAsmdefInherit/显式policy读取与真实消费者验证，随后Include/Player-profile/Rider；当前无Node/IDE/子任务，source5556不重启、不提交/推送/切分支、不改源依赖/权限/defaultIDE。最终保护与整分支独立Review仍未完成，Goal仍active，不以本子阶段报告complete。
- 20:25Z源只读确认Refresh后的编译已结束、无失败、原场景干净。实际TestsAnalyzerAsmdefInherit删除local YL0008（9/10保留），下一analyzer-vs-inherit-generate1正常exit0、真实custom三Warning。VS-host7新进程7668/host48824原生两作用域均三Warning（无Default父策略时继承descriptor默认），仅证明该真实继承环境，不把Inherit硬编码成Warning。已精确restore原叶子、stop后cleanup三标记true并确认终态，没有保存场景/切默认IDE。
- 为补无XF receiver的实际编译见证，仅将CoreEditorIdentityTests中现有CompileNewUsageWitnesses从private改internal，原整类source-host分类/过滤不动；在已有receiver可发现AnalyzerRuleSetFileTests追加两个薄UnityTest入口复用该非XF真实算法，未复制SDK stub Runtime或新建测试框架。代码格式检查通过，登记源tests9→tests10。官方Discover/list4实际Edit2616/Play791，正常退出；analyzer-compiler-profiles1精确Include1+Editor1+Player1 **3/3 passed**，20:35:01Z正常exit0、无skip/差异/挂起/超时。含真实AssemblyBuilder正反例（8/9/10及记录/event边界）与Include父Error/叶None消费，非仅图中字符串。刷新后源仍2022/PID5556、非Play/编译/更新/失败、原场景干净。

</details>

<a id="h12"></a>

<details>
<summary>H12 Rider 协议、84 守卫与原生窗口缺口</summary>

- Rider只读研究已完整返回：原生exe为LocalAppData/Programs/Rider/bin/rider64.exe，2026.2.3.1 build262.10968.170；mcpServer设置已有enable=true、Brave默认false未改，旧64482端口文件仅候选。实现有SSE `/sse`→本次`/message`和HTTP `/stream`，不是/mcp；显式rootFolder只选择已打开项目不替代打开。工具只读语义list/projects/problems/lint，须拒绝isError/timedOut/more/notAnalyzedReason/错文件/缺项；描述无raw ID，pragma仍须原生视图独立证据。未使用IDE进程内SDK签名当外部后门。
- `TestsAnalyzerRiderProjects/analyzer-rider-generate1`实际正常exit0，原生Rider generator输出和两个csproj已按正式Analyzer/local规则/Probe检查并冻结，Solution sha=`272edeeb33f38d9aea342d7b1cfb1ce1a885042242efea1745e1c99525ddefa6`，未手改或将旧VS同名文件当Rider。新增Start-OwnedRider.ps1先验证新生成/所有baseline输入，VS生成传入纯参数测试真实拒绝，未启动IDE；与新Invoke-OwnedRiderMcp.ps1一起只用本批Root/唯一Run，不提供任意ToolName/参数覆盖，不改MCP/Brave/插件策略。
- 新Rider-rider1已真实启动：原生PID47828、StartUtc20:45:09.8872923Z、tracked=`bgp_17f1c132200115znAiRSknXIDG`（wrapper35792）。版本/build与原生exe吻合、NonBundledPluginsDisabledForThisLaunch=false、AnalysisReady=false，不把5秒存活当分析完成。首次按确切PID发现只监听loopback63342/4490，无MCP64482；尚未发MCP查询/未宣称Rider诊断通过。启动器持有有界等待但不会为超时杀未知/user进程。单次wakeup=`wku_17f1d4a1f0012ZQgVRG3d25TQh`/20:47:28Z已排，先核新PID端口再进行SSE initialize/list/project绑定，不能靠旧port文件连接。
- 当前只有此自有Rider活动，无owned Unity/VS/Node或子任务；尚需两新Rider工具.meta及最终Source导入/编译/保护复核。剩余UI/Input/URP/可选集成/双引擎最终包/整分支独立Review未完，不报告complete；任何不可用的Rider接口先保留原始失败再查已配置状态，不修改权限/全局设置制造通过。
- 20:48Z唤醒只读核Rider PID47828仍正确，但MainWindowTitle实为“不受信任的解决方案 w6”，仅63342/4490，MCP未启动，不能按启动耗时无限轮询或自动改trust/Brave。新Capture-OwnedRider.ps1复用PrintWindow但绑定新Root/完整启动时刻/PID/HWND/动态rect，拒绝最小化/未知window，无桌面捕获/焦点/键盘或trust批准；`untrusted-solution.json/png`冻结。图像按当前rect有DPI截断限制，门禁身份事实来自原生title/两个socket，不声称完整UI所有控件可见。
- 该动作仅对仍untrusted的本批窗口显式CloseMainWindow，20:51:23Z PID47828正常exit0/KilledByHarness=false，旧Root与失败现场保留。未发送MCP、不更改安全配置，不把该阻塞记为代码诊断失败。独立只读子任务`ses_e80dc5cdcffenAy5TfIxEpcva7`已启动定位nativeInput/URP既有夹具，不启动引擎或重复Rider研究；后续仍可推进无依赖任务。
- 用户20:51:31明确“我手动信任”。执行者不代点或修改信任设置；先确认无现有Rider或64482监听，再按同一Rider生成/完整baseline创建新Rider-rider2，tracked=`bgp_17f247d67001k65Pbx3hY70r07`（wrapper56184）、原生PID42676。窗口已保留供用户手动确认，不能像rider1一样再次自动关闭。单次wakeup `wku_17f2528b7001TPTU1owqW2FrJO`/20:56:04Z已排；之后仅核新PID/启动时刻/title/port，信任完成并有同PID MCP listener再握手，不用旧端口/session，也不预记诊断通过。
- 用户20:55:35说明“已经打开了”；只读核同PID42676窗口title=w6且64482属于此新PID，再新SSE initialize/list正常，serverInfo=Rider2026.2.3.1/协议2024-11-05。项目实际含Consumer/Assembly-CSharp/Runtime及对应Player项，不用旧port或endpoint。未由执行者批准trust或改MCP/Brave/插件设置。
- MCP首个tools-initial原始成功响应已保留，调用器因StrictMode对可选isError缺字段误拒；改为ContainsKey/字典类型检查后tools-confirmed成功。首个custom-baseline-problems返回合法反斜线相对路径而调用器要求正斜线误拒；按Windows规范化绝对path且限定同root/精确期望文件后custom-baseline-confirmed与default-baseline均成功。不取消错文件/缺项/isError/timedOut/more/notAnalyzedReason拒绝；live schema确认lint item键为filePath，已经按实际结构处理，不猜旧path字段。
- Rider两scope基线各3个YL Warning，另unused-using提示保留。仅在新ownedw6两个Probe做受控apply_patch，开始前hash/meta与owner一致；custom单Inject正例保留Panel/Audio2控制，custom全正例YL0/default3的完整lint成功；default单Inject正例2控制，双全正例YL0（Rider自身unused-setter警告仍保留，不伪称全IDE零Warning）。精确恢复两个磁盘负例后restored-bad-both-lint各3规则返回。没有源工程/C#业务或meta/GUID改动，未运行Unity/构建。
- Rider新增open动作只允许两固定FilePath并显式rootFolder，非任意ToolName/Arguments覆盖；记录EditorStateChanging=true，不伪称只读。只打开自有custom编辑器后，按同一已归档产品API error1字节Apply到custom叶子，custom-error-default-warning-lint明确custom ERROR/default WARNING、各9/10控制WARNING。原生custom-error-native.png/HWND2000级实际渲染已核红色Error徽标1、Bad红波浪线、两个控制黄波浪线及正确Consumer项目，不以生成图代替原生UI。
- UI捕获DPI修正仅设置/恢复调用PowerShell线程的PerMonitorV2上下文，不改系统分辨率/缩放或应用窗口尺寸；否则旧untrusted小图存在DPI截断。Capture仍拒绝minimized/错误PID/HWND/无效rect，不做桌面capture/焦点/键盘。随后custom仅围绕Inject加#pragma disable/restore YL0008，MCP仍返回该ERROR（新行9、控制11/12），这证实该API不反映原生pragma抑制，不能据此判产品失败或原生通过。
- 当前Rider自有窗口已minimized，capture精确失败信息Pid42676匹配/Iconic=true/Rect276×45。没有降低门禁/强制还原窗口或用API未抑制结果冒充UI。custom-error-pragma-native此前无输出文件，等待窗口可见后才补PrintWindow原生证据；单次wakeup=`wku_17f3d7d3d001ifRb3MJH5bx8HQ`/21:23:38Z已排。**当前custom Probe仍有局部pragma、custom叶子仍Error1的2336450d...字节，default Probe/叶子baseline**；不要启动owned Unity或直接调用baseline RequireInputs，须后续精确恢复/记录。
- Input/URP只读定位已返回无后台子任务：当前Stage9 Click为ExecuteEvents非OS原生、UIAcceptance虚拟Mouse/Keyboard不替代Windows输入；现有原生完整Legacy/New/Both流程缺失，须复用UI判据和Stage9 ownership/build壳补最小OS驱动，不能放宽Legacy-only旧checker。独立6.3 URP本地官方17.3.0/Core+ShaderGraph17.3.0/Config17.0.3，w6/v6目前未安装，不用源URP16.0.6或搬源settings。URP正常帧需独立观察/SubmitRenderRequest，不能直接沿用camera.Render像素oracle。真实Prefab/Variant/SO与具体入口已在只读Task结果，未实施/未宣称这些剩余门禁通过。
- 21:24Z调度后原生pragma截图仍精确拒绝Iconic=true，不反复抓黑图或强制改用户窗口。为不让UI可见性卡住其它协议证据，先移除custom临时#pragma恢复Source，再restore业务Error；完整lint确认两项目重新3YL Warning。此项不补签pragma，旧MCP“不反映抑制”的原件仍保留。
- 本轮继续完成Rider协议配置消费：analyzer-rider-default-error-apply1重放默认API字节，default-error-custom-warning-lint明确default ERROR/custom WARNING，9/10控制均Warning；精确restore默认后，analyzer-rider-none-apply1使custom Inject消失而custom控制2/default3保持；restoreNone再apply Inherit，custom-inherit-both-warning-lint在无Default父策略时两边3Warning恢复，最后inherit-restore1还原baseline。全部只读诊断按同PID/显式root/完整items拒绝门禁通过，未跑Unity/构建、未隐瞒Rider unused-using/unused-setter提示。
- **当前状态更新（覆盖501及旧wakeup状态）：两份Probe源、两份ruleset和asmdef全部baseline，无#pragma/Error/None/Inherit实验残留**。Rider2仍42676，同一window最小化且存活；原生pragma视觉仍未验，整段Rider验收不能报完成。单次wakeup=`wku_17f4a79e3001hlUlVWqcb8pAe6`/21:38:49Z已排，以低频窗口状态检查/必要复现补证，不重复已有protocol子集。启动器rider2原Life3600到21:54附近将只写超时而不杀进程，后续按所有权安全处理，不能拿启动器退出冒充IDE已退出。
- 独立实现任务已明确限定范围：`ses_e80aec71affelXarZPtWEdlcpQ`只新增Rider协议checker及guards、不操作IDE/改旧数据；`ses_e80aba1c7ffexRRJvOYko81LEy`只新增NativeInput Probe/Editor模板/driver/纯参数guards、不运行Unity/Player/OS输入，均不得改现有文件/Plan/.meta/权限。主线程不重复这些实现，自动返回后核对关键结论与接入，不能把后台未返回当完成。
- 本阶段实际复跑Stage9原六拒绝门禁green8 **6/6 passed**；Source guardedRefresh导入三个Rider工具.meta：Start-OwnedRider=2af89e18e3d263d4c8035ee031203510，Invoke-OwnedRiderMcp=a2b03f79d48e4564e994367942dc2bde，Capture-OwnedRider=1329f9bd15e06234f9b5da6dc3520f13。返回Source2022/PID5556、Compiling=false/CompilationFailed=false/原场景干净；没有保存场景、改依赖/默认IDE或重启源。
- Protect checkpoint5实际2553→2553、Passed=true、AllDifferences/AuthorizedDifferences/UnapprovedDifferences全部空。无新的产品或用户输入变更，Rider所有5文件仍baseline；checker/nativeInput后台结果尚待返回，最新单次wakeup=`wku_17f5cc0360010lDbF2vytdQbPx`/22:00:47Z已排，核结果/实际IDE寿命而非密集poll。Rider2启动器到期可能只退出wrapper，不能据此关闭或认定用户手动信任IDE已退出。Goal所有强门禁未齐，不reportcomplete/blocked。
- Rider协议checker子任务已返回，仅新增Test-OwnedRiderEvidence.ps1及Test-OwnedRiderEvidenceGuards.ps1；首次缺checker真实RED和原72/72收据保留。主线程独立复跑`owned-rider-integration-baseline-20261027-001`实际72/72，随后审查发现坐标仅要求正整数，正确family描述搭配错误行号/列号/源码行仍会被接受，不按原报告直接收口。追加12个内存变异后`owned-rider-location-red-20261027-001`实际全部复现误接受（原72仍通过），失败收据SHA=`f08713c3174891a75ba52d90112823a2e89bd75feed05a81b5a509995dfa3632`，未改原始MCP证据。
- 已最小收紧同一pure validator：完整诊断逐条匹配本批受控Probe的源码状态、行/列界限；三个family及using/setter控制核精确位置，Inject描述绑定对应Probe.Bad；pragma观察核其真实偏移但仍排除为原生验收。合法额外HINT夹具同步改成真实class行6，不删除或过滤未知ERROR。`owned-rider-location-green-20261027-001`实际**84/84**（6合法/78拒绝），InputEvidenceUnchanged=true，收据SHA=`53a4e85341dd3be3d04fad5da74178850376fccd668c58c257903a68de84a8b6`。完整`owned-rider-integration-check-20261027-001`实际13组通过、五输入baseline，SHA=`88d4a18cdaa2ae795ef32809b9a98450dbe266621a594dbe800864902dc98c91`；明确FullRiderAcceptance/NativePragmaVerified/NativeUiVerified/RestartVerified/GoalPassed全false，无raw diagnostic ID、未重核package归档或把saved ownership冒充live。两个脚本无空白错误，当前SHA依次`809a52f8a645bd28a10e7903158e3d0382f501831caeb5d5f7649964fd7e79d6`/`537228d30a64f2bb58ec82d6d8631e2b652b41e8494f4af1c7460f68f671a147`。
- 源MCP先只读确认2022.3/PID5556、非Play/编译/更新/失败、唯一YLFrameworkDev场景干净，再仅ImportAsset两个Rider脚本而非全局刷新并行任务文件；Unity实际生成/读取meta GUID=`38404053e3a47264e830077ca278bd66`/`98c5ea7e68027cc4e9b175bc2ed103f0`。导入后Compiling=false/Updating=false/CompilationFailed=false/SceneDirty=false；没有源NUnit、场景保存、依赖/默认IDE变更或第二实例。首次只读snippet因路径转义被Roslyn拒绝，未执行；修正后读取/定向导入真实成功，不把snippet错误称作产品编译失败。
- Rider2真实exit.json仅记录21:54:26Z DeadlineExpired=true/Exited=false/KilledByHarness=false；主线程随后只读核原生PID42676、同exe/窗口仍存活。`baseline-window-state-20261027-001`捕获仍精确拒绝Iconic=true/Rect276×45，没有输出伪截图、还原/置前窗口或关闭用户已信任IDE。五输入仍baseline，原生pragma与重启缺口不因84守卫通过而消失；本轮checker发送MCP=0/IDE状态变更=0。
- Protect checkpoint6实际2553→2553、Passed=true、三种Differences全空。NativeInput实现任务仍待自动返回，主线程不重复实现/轮询其进度；新脚本均Tests-only，不重绑定candidate10/w6或candidate11，未提交/推送/切分支。整体仍需Native输入、URP/手动资产/可选集成、双版本最终同字节接收及整分支独立Review，继续执行而非报告Goal完成。

</details>

<a id="h13"></a>

<details>
<summary>H13 原生输入、URP 可见帧、Clipboard 与 Legacy Play 分组</summary>

- 为推进不依赖Rider窗口的任务，先核只有源Editor5556及其AssetImportWorker、Hub进程，无owned Unity冲突，并核新短路径p未占用；未领养已存在u或其他载体。`Create p`实际6000.3.27f1/PID23648、22:12:18Z正常exit0；candidate11首次裸Import/PID49328于22:13:05Z正常exit0、无超时/强退/挂起，first-import-initial明确HarnessPresent=false、包SHA仍f5322465...、仅modules/UGUI2/Json.NET3.2.1与包内BouncyCastle，未用URP/Test/Input/IDE补裸包依赖。
- 裸导入证据冻结后仅在p manifest增加官方com.unity.render-pipelines.universal=17.3.0，未手改lock；版本依据本机6000.3.27f1内置package.json及真实UPM解析，Core/ShaderGraph17.3.0、Universal Config17.0.3均已注册。之后安装现有Stage9审计夹具，`Audit/urp-resolve1`实际PID44204于22:14:36Z正常exit0，1038产品条目逐字节/GUID/meta一致、MissingScripts/DuplicateGuids均0；lock由Unity更新，包含官方传递依赖，不搬源Packages/Settings。当前RenderPipeline仍Built-in，仅完成独立URP载体和依赖准备，不宣称URP已激活或UI/过渡/资源像素显示通过。
- NativeInput子任务已自动返回四个指定新文件，报告33纯守卫和8项外部Roslyn编译，未运行Unity/Player/OS输入；主线程尚在审读/复核，不能提前记native输入通过。仅实现取色环颜色回调恰一次及关闭按钮恰一次两手势；取消/三键/滚动/键盘/设备/共享actions/外部EventSystem/多根仍缺。下一步先核驱动安全、正式Prefab/生产桥接消费和实际编译，再在独立非URP输入载体串行执行，不以外部Roslyn或守卫替代Unity与IL2CPP结果。
- NativeInput主线程已完整审读四文件并独立复跑`native-input-main1`33/33。现有Stage9驱动缺安装/三Editor接点，追加6个纯拒绝例后`native-input-dispatch-red1`真实6/39 RED，原33仍通过；最小新增InstallNativeInput及NativeInputGenerate/BuildMono/BuildIl2Cpp明确白名单、原生构建显式NativeInputMode及不相干调用拒绝，安装前要求同SHA裸包proof、目标全新、原始两模板冻结ToolsHistory。`native-input-dispatch-green1`实际39/39，原六入口`GuardTests-green9`实际6/6；未改旧Player Legacy-only checker或使Validate加载Win32/启动Unity。
- 新独立Builtin n（candidate11/6.3/UGUI2）Create/裸Import均真实正常exit0，无Input/Test/URP/IDE补依赖；安装后Unity编译成功，但`NativeInputGenerate/native1`PID44408于22:21:35Z真实exit1，原因为Unity拒绝对干净未命名bootstrap做Additive，不是产品Runtime编译失败。n已生成空Owned目录及meta，原样冻结不领养/清理/重跑。仅修新Editor模板：未命名集必须唯一，空场景或精确Main Camera(Transform/Camera/AudioListener)+Directional Light(Transform/Light)，禁止孩子/额外对象/组件/脏场景；仅该已知bootstrap可Single，新Scene保存自身，不保存或关闭未知用户场景。
- 全新n2同candidate11再次Create/裸Import/安装，`NativeInputGenerate/native1`实际PID37496、22:24:29Z正常exit0，完成同缺陷RED→GREEN。真实正式prefab绑定与Owned Scene GUID/hash已落Identity.json，ProbeSHA=`e585ab0b9e9bf19c606006baa9bd1a2cb4a1081dc4dca50f8f5259b04cde3b77`、EditorSHA=`ee305602ad04df4bf6725ad2a719501667e0d09ce6e3710925010cd3e00cd7eb`。`Format-YL -Mode Check`不接受.cs.txt模板路径并在改动前拒绝，未放宽格式白名单；针对实际文本git diff --no-index --check通过，Unity编译结果另记，不称formatter通过。
- n2 `NativeInputBuildMono/m1`实际PID47608、22:25:15Z正常exit0；BuildReport Succeeded/Errors0/Warnings14、非Development/None、Mono2x/Disabled，Legacy=true/New=false且manifest/lock无InputSystem/Test/URP。BuildGuid=`c7cfafd99f56464c90a447fe5d04bcd1`、BuildReportSHA=`74d5cb2708849be5a823d96324bfef65b3b9f86f1f7a500696572639b803bfd3`；PlayerSettings前后SHA相同。显式Validate正常通过且InputInjected=false，此后真实Run仍不得冒称通过。
- n2 `legacy-mono1`真实Player PID7896到达正式取色UI ready：1920×1080、DPI168、6.3/D3D12/RTX4070TiSUPER、生产StandaloneInputModule；首次move前组合HWND安全门禁拒绝，NativeEvents=[]、OwnedButtonMayRemainHeld=false、仅停止本次owned Player，ForcedExit=true/exit-1、TimedOut=false。为分清拒绝条件仅增加Win32异常明细，不改拒绝逻辑；唯一诊断复跑`legacy-mono-diagnostic1`PID13672确认Hwnd5053744/匹配PID/Exists=true/Enabled=true/Minimized=false，但ForegroundHwnd20449228不同，仍输入0并正常执行owned失败清理。两个原始run全部保留；不置前/抢焦点/绕过门禁或无新依据重试。原生手势、颜色/关闭业务回调、UIPointer hover均未通过；暂只阻断OS手势依赖，仍可继续其它编译/后端矩阵。
- 源Editor身份/单一干净YLFrameworkDev/非Play/编译/更新/失败守卫再次通过，定向导入4个Native工具及已存在Stage9驱动；Unity生成/读取Native Probe/Editor/driver/guards meta GUID依次=`d3e98496f8b4c8944b766fbf7be7da50`/`e7fa1459b9e9f36458086bb623c948aa`/`3297b573400728441924c1369f184db7`/`c24844959869660459db2399a55996b1`，Stage9驱动原GUID不变。导入后Compiling/Updating/CompilationFailed/SceneDirty全false。`native-input-dispatch-green2`实际39/39；Protect checkpoint7实际2553→2553/Passed=true/三种Differences全空，SHA=`ab5eb73b1d1a489a6440b1e84a180845756359c7c4f5371f0b108dec2028ce0e`。
- n2 `NativeInputBuildIl2Cpp/i1`实际PID15684于22:32:02Z正常exit0，无timeout/forced/stall，Succeeded/Errors0/Warnings14、非Development/None、IL2CPP/Low/C++Release，Legacy=true/New=false/InputSystemVersion=null；BuildGuid=`167a8f124e0048239acc5b181de62307`、BuildReportSHA=`c6cfc597b9cfd6fe8f73b29d90a4613359373d0d36fa2e762fa4328d67b31b40`，PlayerSettings前后SHA仍相同。显式Validate完整输出闭包通过、InputInjected=false，未再启动此Player试焦点。真实ManagedStripped/YLFramework.Runtime.dll存在，但Library/C++尚未另归档，不能外推IL2CPP运行或InputSystem私有反射保留；先冻结n2，后续New/Both采用新载体，不对n2切模式/再构建覆盖当前Linker证据。
- 当前Native driver/Stage9 driver/guards SHA依次=`d82d3e98b5875a61d9e1b64ae0d6d4a46b8e09dfca5e746f1bde5722e38310bc`/`23126e989f7238210525330161a6810681f9765cf34237355c22891a602ca8b7`/`ebbc9cca6a811eab1f0f25a51fa3cebc087b3ad9392072107f461528a497d49b`。已核现有TestsNew/Both配置入口真实写activeInputHandler并要求重启；下一步在新n3/n4各先candidate11裸接收，再按已知精确官方Test/Input依赖装夹具、沿用既有配置入口，分别构建且保护各自最后IL2CPP产物，不搬源Settings/manifest。Native手势仍等待合法自有前台条件，Rider原生pragma/重启仍缺，不据此报告整体blocked或complete；未提交/推送或更改原默认IDE。
- 22:50Z用户再次要求持续执行，主线程未把上轮阶段交接当作任务完成。重新核源5556/两个ImportWorker/Hub，无owned Unity或Native Player；n3/n4未占用、G剩余约142GB。新n3/n4均按candidate11/6.3/UGUI2正常Create→裸Import，通过后才各自增加官方Test1.6.0/Input1.20.1、由Unity生成lock并装现有Tests/Native模板；未装IDE包、未复制Source配置或Library，n2冻结不动。
- n3首次`TestsNew/new1`PID20836于22:52:28Z因UPM IPC断开退出1，未进入脚本编译/配置，原日志/exit保留。仅在独占交付根新增一次性Observe-OwnedUpm.ps1，保存前后原始upm.log并持有同本次Unity父PID的UPM Process读取退出状态，未改代理/信任/缓存；`new-upmdiag1`PID31444/UPM2876于22:56:42Z正常exit0，UPM在采样时仍存活/ExitCode=null，不把它写成捕获到崩溃或已修根因。真实profile=InputMode1/普通DomainReload/Map271027，lock=Input1.20.1/Test1.6.0/NUnit2.0.5。首次UPM断开的具体根因仍未知，不重复概率重试。
- n3重启Generate成功；New-only Mono m1/PID52276于22:58:40Z、IL2CPP i1/PID45768于23:03:57Z均正常exit0，无timeout/forced/stall；两者Errors0/Warnings17、非Development/None、Legacy=false/New=true，Mono Disabled、IL2CPP Low/C++Release，PlayerSettings均原字节恢复。Mono Guid=`ed5cbb27d3e740d8ab34e9cd29518025`/ReportSHA=`da9b8a68c92808d9afe4aadb2d6afee76ab6038e521909693319e90161684607`；IL2CPP Guid=`f89203254619430c86cf456ea18390bf`/SHA=`3139f23b12378c789048e7e6f2336bf203907bbd37b503be54f4ece5151f2d9f`，显式Validate闭包通过/输入0。n3最后IL2CPP目录及Library冻结，尚无New Player实际手势或私有反射裁剪通过。
- 用户在“仅本批已核PID/HWND/哈希的新Player请求前台，不动源Unity/Rider/其它窗口、不绕过任何安全检查”的明确问题下答“再次尝试，这次把窗口全给你”；按最窄范围用于本批Player，未扩大成关闭/重启/操纵其它窗口或系统策略授权。按新条件直接`legacy-mono-user-window1`PID53324仍被同外部ForegroundHwnd20449228挡住/输入0。新增显式ActivateOwnedWindow，仅Run可用且只首次move前普通SetForegroundWindow一次；先验身份/非最小化/几何/按键，后重新核前台/几何/遮挡，Down后失焦不重取焦点。activation-red1实际1/40 RED→green1 40/40。`legacy-mono-authorized1`真实Windows拒绝请求，输入仍0，失败保留；未AttachThreadInput/注入Alt/改ForegroundLock/借源Editor提权或修改权限。
- 新增显式0..15秒ManualForegroundWait，仅Run可用且与主动请求互斥；纯只读核唯一owned HWND、绑定句柄、等本窗前台和所有按键释放，不改窗口或替用户释放键。foreground-wait-red1实际2/43 RED→green1 43/43，默认Validate仍不加载Win32/启动Player。告知用户仅点新窗口标题栏后，`legacy-mono-manual-front1`PID54204/Hwnd400582在15.018秒内未满足前台/键释放条件，Ready=false/InputInjected=false，安全停止owned Player；不据此猜用户是否点击，不再无新条件重试。这是OS手势门禁，不能以构建或守卫代替。
- n4真实TestsBoth/both1保存InputMode2并重启Generate通过；Both Mono m1及IL2CPP i1（PID17936/23:14:42Z）均正常exit0，Errors0/Warnings17、非Development/None、Legacy=true/New=true/Input1.20.1，Mono Disabled、IL2CPP Low/C++Release，Settings前后字节相同。Mono Guid=`099034d56fe641c7882935fe7fd8cdb8`/ReportSHA=`5fa55ffe2af264d58037ae0578ba5eb197255e5caa74a2789f585850055218d6`；IL2CPP Guid=`e56568b491e248eabdaff3b66c089c6f`/SHA=`19a9e8a234b71d64c536637c61232d76be7b727315c1021fe7830a2c8158be3a`。6.3 Legacy/New/Both×Mono/IL2CPP六构建齐，但真实手势/三键取消/滚动/外部EventSystem等仍未验，n4同样冻结最后裁剪目录。转而推进不依赖前台的URP正常帧像素验收，非整体完成或blocked。
- URP冒烟复用现有Stage9 Resource/完整UI消费者，仅在Tests工具增加urp命令、正常EndOfFrame截图/像素断言及精确ConfigureUrp入口，无产品API/默认包依赖变化，不用camera.Render/ExecuteEvents/队列事件/控件属性冒充可见结果。最初引入整个Rendering namespace使p编译出现SerializedDictionary/ObjectPool二义和下游方法组错误，原`p-Generate-urp-content1`保留；仅换GraphicsSettings/RenderPipelineManager两个精确alias后，`urp-content2`PID46688于23:32:27Z正常exit0、真实重新生成Bundle/Catalog/Scene，不复制旧制品。
- p Built-in对照BuildMono/u0后，`Runs/p-Mono-urp-builtin-red1`PID33496/Guid1206beae320e49969056b535c1e45615真实exit1，IdentityMatched=true/BusinessPassed=false/UrpVisualPassed=false，准确拒绝“Actual URP asset and live pipeline required”，并未因已注册URP包就计通过。配置p/u1在生成3个资产/场景绑定后，取证阶段因局部资产引用GetAssetPath为空退出1；原p部分输出冻结。现场确认Pipeline_Renderer/管线/材质及meta真实存在，管线已引用Renderer GUID；场景切换发生在局部创建与设置之间。最小改为先OpenScene再构造资产，并以官方工厂确定的固定资产路径取证，避免依赖会失效的Unity局部引用；未修补p资产或领养部分结果。
- 全新同短路径j重复candidate11裸Create/Import→仅官方URP17.3.0解析→安装最新夹具→真实生成自己内容；没有拷p的Scene/Bundle/Library。`ConfigureUrp/u1`PID7664于23:48:05Z正常exit0，Graphics/Quality两处实际都持久化指向Owned Pipeline.asset/Guid353699e9339829f4a840fe28d9c13a83、Quality5，官方精确CreateRendererAsset工厂、Renderer/Material/Scene的GUID/hash/meta、模板SHA落`j-urp-configuration-u1.json`，仍明确ConfigurationOnly=true/VisualPassed=false。配置失败→新鲜正向修复有实际闭环，不把配置本身作为运行证据。
- j `BuildMono/u1`PID19656真实Succeeded/Errors0/Warnings0/非Development/Disabled，Guid=`4d6e4ec2c95b46a7bd319cb93f41f2bf`、ReportSHA=`edf1fbe76d2627fd16c5ceff0fb5bb72eb4744fe53444270f8e2d78fdf6928e5`。`Runs/j-Mono-urp-visual1`PID19048于23:49:55Z真实正常exit0，D3D12/Windows64/Legacy-only、实际URP asset及live UniversalRenderPipeline；Business/Identity/UrpVisual/最终Passed全true，Errors/ExitErrors/LateErrors为空、清理Module/Core绑定/native Bundle归零。ResultSHA=`771ab5f6cdb0d1f3506c2392794719ca759449ac4fc2e09ad52b42245cfddf87`，PlayerLogSHA=`3d416c99f1ac06b0d634382f98a9f1a07e94f6fffb5ca1b2152dbbc603a6bc86`。
- URP实际4份1280×720正常帧PNG均已读取观察：新生成Resource Atlas经真实Acquire租约显示在URP Unlit Quad；正式完整根/ColorSelector取色环红色；正式ActionKit屏幕蓝色淡入在中心/两角3点全覆盖；淡出后恢复同Atlas。最大RGB误差依次0.00392156839/0.003921569/0/0.00392156839，固定容差0.08未放宽，名称/数量/布尔/数值有限性/PNG哈希与路径受结果门禁检查。它证明6.3 URP Mono代表性UI/过渡/资源正常帧显示，不代表URP IL2CPP、OS输入、Audio DSP或New/Both私有反射；旧ScreenTransitionCanvas根scale0疑点在本实际运行中未造成不可见，未擅改源Prefab。该专项checkbox现可完成，其它强门禁不随之勾选。
- 最小Source状态守卫后仅定向Import五个已存在Tests工具，PID5556/2022.3、唯一YLFrameworkDev干净、非Play/编译/更新/失败，所有原meta GUID保持；DefaultEditor实际仍C:\Users\Kano\AppData\Local\Programs\Microsoft VS Code\Code.exe。Protect checkpoint8实际2553→2553、Passed=true/三种Differences全空；原六入口GuardTests-green10实际6/6，局部diff无空白错误，未提交/推送或更改Source包/设置。并行只读任务ses_e8038b79dfferov8f71eitlvyM仅定位DOTween/Excel/DomainExcel现有接点与真实版本，不写文件或操作Unity/网络；主线程不重复其研究，继续最新完整测试闭包。
- 全新q同candidate11裸Create/Import通过后仅安装当前测试树及Test1.6.0/Input1.20.1；TestsLegacy/legacy1与图形UTF发现list1正常，MissingTestAssemblies=[]，companion最终XML后正常exit0。普通EditMode命令首次误用不存在的DiscoveryRun参数被绑定层拒绝、无Unity启动；改用既有ProofRun=list1后`ordinary-edit1`实际1835集合精确相等/1833Passed/2Failed/无skip，PID47568于00:04:58Z正常exit2、无超时/强退/挂起，原verdict/XML/log保留。两失败为CoreSerializedPropertyTests.MixedCopyAndPaste...:163及DisableEditDrawerTests.SerializedPropertyClipboard...:160；未因旧w5普通1833绿色而签字或修改产品。
- 只读Clipboard任务ses_e80269932ffezFZslopjrH4Bfe已核精确6000.3.27f1 SDK IL及官方UnityCsReference tag(commit32dee660a55df47c2c02fce871533e39549a6e7e)：Get/Set/Has/Parser/Json签名相符，无managed异步队列；source/q/w5三相关源码与7产品方法/2失败测试symbolic IL相同。mixed.Copy在275-277混值预检即抛出，未到唯一写入口，before读空/after读自有54字节payload不能解释为混值复制成功。Prepare三次独立OS读取存在TOCTOU静态风险，但本轮分支未证明，未加sleep/retry/影子clipboard或放宽比较；agent未读取实时用户剪贴板、未运行Unity/写文件。
- q原断言隔离`clipboard-isolated1`精确2/2正常exit0；恢复两完整fixture `clipboard-fixtures1`精确18/19、仅mixed同失败、exit2；最小“MalformedNativePayload...→Mixed...” `clipboard-history-pair1`两叶亦复现，形成具体历史/上下文相关证据，不算概率重试。随后仅改CoreSerializedPropertyTests诊断：mixed复制后增加已知自有payload基线断言，保留全部原混值/对象/缓冲断言；增加直接Unity Clipboard复制与纯内存Parser对照，日志仅自有payload匹配/安全长度及Win32序号/openWindow/openPid，不输出用户内容，不为日志增加OS getter。Format Check限定该cs实际SDK9.0.312通过；q同步tests2只一文件、旧字节归档，list2真实发现新增两叶。
- `clipboard-controls1`精确4叶/2Pass2Fail、正常exit2：pure Parser及Malformed通过；mixed在操作前的自有复制基线即读空失败；直接Unity Has在设置自有GenericPropertyJSON:{}后返回false，失败发生在YL拷贝/混值路径之外。最初TestContext.Progress未进入UTF原始输出，未把缺日志当空值；仅将采样送TestContext.Out，并从Unity Clipboard.m_State.m_RawContents提取Has已经读过的快照（无额外OS读取），后续同步/再验尚待执行。未确认两个原失败同根、未改SerializedPropertyExtensions产品源码。
- 原生资格纠正：n3 Mono实际PlayerFiles包含YLFramework.UI.Acceptance.dll和Behavior.Acceptance.dll，IL2CPP ManagedStripped及cpp也含UI.Acceptance；源UIAcceptanceEntry:5-7,50-53直接引用InputSystem Mouse/Keyboard/UI。先前六构建“生成成功”仍是真实历史，但n3/n4 New/Both不能作为不靠测试保活的生产反射桥证据，保留冻结不覆盖。新增硬门禁读取PlayerFiles闭合哈希绑定的ScriptingAssemblies.json，拒绝Acceptance/Tests/Editor/Analyzer/Roslyn/Workspaces/nunit，并拒绝缺失/重复/标量名单；production-closure-red1实际8/51 RED→green1 51/51，真实n3 IL2CPP被拒、无SDK n2 Legacy IL2CPP仍通过。
- 最小InstallInputProfile仅复制既有Stage9ReceptionTests到新Native/Editor，保存product-first/源码hash/history、不复制整个Tests或运行时Acceptance；Native Editor Verify亦拒绝完整Tests目录。全新n5裸Create/Import后装相同Test/Input包，先InstallNativeInput再Editor-only Profile，再真实TestsNew/重启/Generate通过；从不装完整测试树。New Mono m1 Guid4df23b5784064b7aa5e63044b6d8c212/ReportSHA546e8e9b5670db3e1671c3fe35d9bfee3fa5052b7d66976cfa47ab8fcee5d9f5及IL2CPP i1(PID42032/00:27:03Z)Guid3cc7cc1f226e43a386d79a999020776f/SHA c8aceaf608218702ac759cdb48d36a2909e8315e0c0b01d728ed91de755c721b均实际Succeeded/Errors0/Warnings14/非Development、准确New-only/Input1.20.1，Disabled与Low/C++Release各设置按原字节恢复。两闭包均通过新增禁入门禁、无Acceptance/测试/Editor/Analyzer宿主，InputInjected=false；这仍是构建及静态闭包，不是已通过OS手势或私有字段裁剪运行。n5最后IL2CPP冻结，n6薄Both与元数据定向检查待续。
- 可选集成只读结果已返回（无运行/写入）：DOTween源码预期80、Excel41、DomainExcel15，现Stage9可选assembly/scope入口缺；DOTween许可证正文/来源仍未闭合；MiniExcel1.31.2/Apache-2.0具现有文件，XF适配源Dependency所指旧文件/独立license待核；DomainExcel requiredCommit为6a9e...不等于当前HEAD，不能仅哈希不同认定API失效。Excel提交公开destructive且无Undo，与本Plan任务7的Undo要求有实质范围冲突，尚未静默取消或新增实现；普通作者窗口Undo不能代Excel提交Undo。旧MC26/2022工具仅参考，不直接运行。后续按真实来源/许可与门禁逐项接入，不能根据静态80/41/15或缺宏零项签绿。
- q诊断tests3/list3的`clipboard-controls2`仍精确4叶/2Passed2Failed/正常exit2；原始XML现在确有TestContext.Out采样：mixed-before为owned=false/empty=true/sequence4154/openWindow0/openPid0；直接Unity Has的既有m_State快照为empty=true/sequence4164/openWindow0/openPid0，失败在Native copy控制中Has前置即发生，尚未到直接SetSerializedProperty，不能说已执行其复制或只归因混值路径。pure Parser、Malformed仍通过。诊断不输出用户内容，不增加OS getter，设置后的原生读空是实际阻断；采样时无打开窗口不排除更早竞争，不再重复跑到绿或改产品缓存/协议。
- n6薄Both按与n5同样新鲜裸接收→仅Editor配置helper→TestsBoth/重启/Generate→Mono/IL2CPP独立生成，两后端Errors0/Warnings14/非Development、准确Both/Input1.20.1、Settings按原字节恢复且闭包无测试/Acceptance/Editor/Analyzer。Mono Guid=`24c86e4f285d4607939451d026a32585`/ReportSHA=`3bd7dae8f70d6e54e84998f0a082a22caa68c8d6037a60e070fa18f9c275fc8a`；IL2CPP PID54964于00:53:16Z正常exit0/Low/C++Release、Guid=`bf5a9c0531184e2a98696689ed9bd589`/SHA=`d8c34dbafe50e3ca3ad0a8496925007bb82f349fafe2e6bf0cad2254d766cbdc`。n5/n6最后Library/CPP仍冻结，不重建覆盖。
- 独占根一次性Inspect-NativeInputMetadata.ps1仅用官方Unity.Cecil读取而不执行目标裁剪程序集，先核不可变Player闭包；首次inspect1因PowerShell Name/name大小写同变量使Type标签写成最后member，原错误收据保留不用。改参数TypeName后inspect2两载体实际核7个精确类型：InputSystemUIInputModule(m_PointerStates/defaultActions/ResetPointers/actionsAsset)、InlinedArray、PointerModel及ButtonState十字段、EventSystem.m_EventSystems、InputField.m_ShouldActivateNextUpdate、PointerEventData.lastPress私有setter，均Missing=[]。n5/n6收据SHA=`a12972ef420c3c0e8d9fcb991b7ec0b92003bfa275175e702498d46078a19674`/`9f750e10d1aa1921adf75e89a0a8a21c4837e1a639e5245bedb6de08a7a52281`，MetadataOnly=true/RuntimePassed=false/TargetAssembliesExecuted=false；没有增加Preserve、强类型Input引用或改生产bridge，元数据存在不替代真实手势/布局消费。
- q `ordinary-play1`实际743精确叶正常exit2，50项因原有“不接管公共宿主”SetUp拒绝（未删除他人宿主），其余693通过；这是先前w2已识别的隔离合同，不修产品或关断言。按既有独立进程合同逐类完整重验：observation6/6、starter15/15、update6/6、global7/7正常exit0；Http首次19/20，ExecutionEnd...在服务端两请求到达前5秒界限失败。只将该处等待展开为同样5秒、保留服务健康/到达2断言并增加请求状态诊断，未加时或改产品，tests4/list4真实编译发现后单项1/1和完整Http20/20正常exit0。原19/20保留，具体瞬时网络/排队原因未复现，不声称产品修复。
- 普通非fresh-host组`ordinary-play2`严格693/693正常exit0；此前因Session字样排除的叶不全是宿主session测试，按完整名字补`session-named-play1`39/39及TimerDriver两session叶2/2均正常exit0。首次按raw总791推算42的意图被启动前计数守卫拒绝、未启动Unity；实际receiver范围是788，另外3叶保持原Source/historical分类。一次性Test-QPlayCoverage.ps1重核8份selection/XML/exit/verdict、SHA、无重名/无skip，对list4 receiver集合788→788、Missing/Extra=0、Passed=true，收据q-play-coverage-legacy1.json；显式SingleMixedRunPassed=false/UniformTestByteClosure=false，HTTP诊断前后其它未变代码沿用其证据，混跑RED与旧HTTP RED都列入保留字段。该结果仅当前6.3 Legacy receiver PlayMode分组覆盖，不是两代引擎或Source专用叶全绿。

</details>

<a id="h14"></a>

<details>
<summary>H14 Excel、DomainExcel、DOTween 接入与许可边界</summary>

- 用户已在Excel Undo范围的明确二选一中选择“沿用现有合同 (Recommended)”：不新增Excel/DomainExcel持久提交撤销，验预览/取消/失败回滚，现有领域作者窗口Undo单独验。任务7对应条款已按该授权修订，未改产品提交合同、未删除有效测试。
- DOTween许可子任务已实核本地DLL静态Version字段1.2.825、177664B/MVID5ae618ea-15ea-4f2e-bfe5-8b132c1de82d，目标程序集未加载；SHA=`d74bfc3f37cd97d90f7f08a9a73e204c5eae66e77be24b12378a4e99f1690843`，与官方固定ZIP(216935B/SHA689d42944f7076038eb6b87ed4c85ae6951c4aec8eaae4e068524ce55b422c8c)完全一致。本地21文件对官方24文件逐字节相同，3个UpgradeManager缺失有官方Setup自删除路径，非擅改DLL证据。官方license.php是定制Artistic条款，非MIT/通用Artistic2；还须Robert Penner easing完整BSD notice。DLL-only插件再分发权未闭合，不能把商业使用许可外推成分发。可选最小方案采用wrapper/测试源码+现有完整MIT许可、不带vendor，由官方固定版单独提供；不把vendor偷偷并默认包，不替用户接受EULA。当前尚未创建可选包或宣称门禁解除。
- 可选显式通道先做optional-guards-red1五项真实RED→green1五项通过，再扩DomainExcel时optional-domain-red1三拒绝不符→green1七项通过，默认排除/Player/错误配置入口均不被OptionalIntegration参数打开。InstallOptional只在精确裸包proof后复制限定Integration根、原meta/GUID/许可/固定MiniExcel DLL，目标全新；InstallOptionalTests只复制所选独立测试根。可选Test scope用精确已发现fullname的锚定集合而非数千NotFilter，仍保留28000参数上限和ExpectedCount/集合/进程终态，默认receiver规则未放宽。
- 全新o同candidate11/6.3裸Create/Import通过后显式装Excel；未装Test SDK时Audit/excel-product1真实编译成功、默认1038产品身份保持。之后装Test1.6.0/Input1.20.1/完整原测试closure，TestsLegacy/list1后`excel-full1`精确41/41、无skip/差异、正常exit0，覆盖真实xlsx往返、旧表头、预览/取消/提交、注入失败回滚及窗口关闭。Stage9默认包没有被补MiniExcel依赖，MiniExcel1.31.2 SHA仍0a49b662.../Editor-only meta及Apache许可原样保留。
- DomainExcel在o显式装产品/独立测试后TestsDomainExcel只增加YL_DOMAIN_EXCEL并记录RestartRequired=true，重启domain-list1实际含独立程序集/15 Runnable叶。第一次选0在启动前被拒绝：旧Profile重分类把新optional-domainexcel覆成receiver；仅按精确独立assembly保留可选scope后实际执行15，原`domain-excel-full1`14Pass1Fail/exit2保留，不把0或14项写绿。唯一失败Mission参数名被读为空；源码确认MissionCondition.paramName有既有JsonProperty("param_name")，ExcelValues.State的nested JSON遵守该旧键，Domain预览却读paramName。仅修可选DomainExcelCommands的1个访问键为param_name，不改Runtime字段/旧JSON/接口、不加泛化大小写或两套回退。
- 全新o2重复裸包和独立选装，使用修复源码，不覆盖o的14/15；新增旧JSON合同断言最初直接引用Newtonsoft使该测试asmdef编译CS0103（产品已编译、宏重启后暴露），原domain-list1失败保留。仅改为反射核当前Mission字段上既有JsonPropertyAttribute.PropertyName，避免给测试添加未声明DLL引用；仍保留真实xlsx参数值/parent/取消/未知参数拒绝全部断言。新增SyncOptionalTests仅在精确不可变optional-tests前驱/文件sha下同步登记.cs/.asmdef/.asmref并归档旧字节，meta/产品/数据不动；o2 domain-tests2→domain-list2真实编译。`domain-excel-green1`15/15及共存`excel-coexist1`41/41均无skip/差异、正常exit0，实现真实14/15 RED→全新载体GREEN。Dependency.requiredCommit6a9e2508...经git merge-base --is-ancestor实际属于当前HEAD历史，不因不等于HEAD而改合同；公开行为已由本轮重验。
- Source最小身份/场景守卫后定向导入Core Clipboard/HTTP两cs和四已存在工具，最初Compiling=true只记Processing；独立读取实现/asmdef后再读真实终态Compiling=false/Updating=false/CompilationFailed=false，PID5556/2022.3/干净YLFrameworkDev/DefaultEditor Code.exe不变。Protect checkpoint9实际成功，未在Source执行NUnit、Play、保存场景或改依赖。此证据早于后续可选宏配置helper及DomainExcel1行修改，后者仍需最终源导入/复核。
- HybridCLR参数化子任务ses_e7fde799fffea6GrkGlJ9D4xVc已返回16现有Tools/HybridCLR文件修改+1纯guard，不动产品/Plan/真实owner/引擎。报告Temp/hybridclr-guards-green5的69/69、15个PS语法解析和4模板×2宏8个C#9语法树，原RED各轮保留；仅离线，不是Unity语义编译。新schema30字段descriptor绑定本批根/精确receiver/固定双版本revision+UGUI/UTF、10互异新短leaf和ownerSHA，Initialize只创建evidence owner快照，不共享H26；9.0.3/官方hash硬门禁保留，不能误将6000.3官方native tag9.0.1改成9.0.3。旧Freeze/Audit/TLS/坏签名/Relocate/Sync明确拒绝新模式，主线程尚待审读/复跑纯guard、创建真实h6描述、正式includeSample导出及串行Installer/Prepare/native，不提前签通过。
- 下一执行顺序：先完成当前Source与新增Optional守卫复核，继续DOTween明确外部vendor的本地可选测试；审读HybridCLR新工具后建立严格h6新上下文。当前q=tests4/list4/legacy1，o2=修复Domain产品+domain-tests2/domain-list2或excel-list1；n5/n6仅metadata inspect2有效且无OS输入，j URP已通过而p部分输出冻结。Clipboard原生边界、Windows前台手势、Rider原生pragma/重启、2022 UTF native退出和Source-host叶仍各自保留缺口，不能以本轮可选绿色解除。目标未完成，继续执行，不goal_report complete/blocked、不提交推送。
- 持续Goal已实际start为active并自动进入本轮，无需用户重复提醒。主线程已审读HybridCLR Scope/Invoke关键路径并独立执行`Test-HybridCLRCompatibilityGuards -Run main-green1`，真实69/69、原语法检查通过，回执TempGoal/hybridclr-guards-main-green1/results.json；没有创建h6或启动Installer。审读发现旧KeepOpenForCallback分支仍允许artifactPassed+forcedCleanup作为成功，新模式在正式使用前需补严格退出拒绝和对应RED→GREEN，不以当前69离线测试掩盖该缺口。DOTween独立可选接入尚未实施，下一轮先完成其显式scope/本地vendor固定SHA/宏/80叶与wrapper-only分发边界，再推进h6。
- HybridCLR退出策略现已补齐并回读实际聚合：`hybridclr-guards-process-policy-red1/results.json`为原69通过、新6项因尚无策略函数失败；新增`Assert-HybridCLRProcessResult`并接入Invoke最终判定后，`process-policy-green1`实际75/75、failed0。新schema严格拒绝非零退出、任何forcedCleanup、请求callback却无有效artifact，正常callback才接受；legacy例外仅留在旧schema分支。无Unity/Installer/native执行，不能把纯策略绿色写成实际原生终态已验。
- DOTween子任务ses_e7f818ad9ffenRAyUNIRiTBaCS已完成限定三文件接入，主线程审读安装/宏/发现/selection边界并独立执行`Test-Stage9OptionalGuards -TempGoalRoot .../dotween-guards-main1`，实际65/65、failed0、NoUnityStarted=true/NoReceiverOwnerCreated=true。原任务red1/red4及green4保留；65项包含真实AST安装/scope逻辑、固定vendor首次校验后字节变化拒绝、linked wrapper宏入口拒绝、Excel/DomainExcel原闭包回归。外部Roslyn有CS1701警告，明确UnityCompilation=false/UnityTestsRun=false；静态1+57+22叶期望不是80项Unity通过。Reception源码Format Check(SDK9.0.312/Files1)和指定工具diff检查实际通过。下一步使用全新短接收端，candidate11裸接收后才加SDK/测试/可选依赖，执行TestsLegacy→TestsDOTween→重启发现与两个程序集；本地DLL复制收据固定LocalDependencyCopy=true/VendorRedistributionAuthorized=false，带vendor再分发HOLD不解除。
- DOTween全新e已真实Create→candidate11裸Import，PID52152于04:03:33Z正常exit0、无强退/超时/挂起。首次本地optional安装在写入前拒绝Existing optional parent identity differs；实查e框架父目录GUID8f6afe97...、o2/q也各为Unity生成的独立GUID，默认导出不含此父目录，不能要求等于源24289d4d...。新增独立父目录保留/缺失与非folder拒绝3例，`dotween-product-root-red1`为3/68失败→最小限定祖先检查止于已证明的产品根，仍验证其folder meta并记录原SHA、不覆盖；`green1`实际68/68，未知Integrations/Plugins父目录和链接仍拒绝。e/vendor2随后真实安装成功，父GUID原样保留。
- e安装完整测试闭包和固定Test1.6.0/Input1.20.1后，TestsLegacy/legacy1→TestsDOTween/macro1正常退出，后者保留既有symbols并记录重启；新进程dotween-list1实际发现两个预期程序集。`dotween-edit1`精确1/1、正常exit0；`dotween-play1`精确79叶、78Passed/1Failed/无skip/差异，PID55028于04:08:23Z正常exit2，无强退/超时/挂起。首叶SetUp实际DOTween.Init日志Couldn't load Modules system；官方现有ModuleUtils.cs:33-38声明由DLL Awake反射调用Init，而安装器只拷DLL漏了九个Modules源码。不得LogAssert吞掉日志、修改第三方DLL或把78项计为整组通过；e及RED原件冻结，下一步补齐本机固定运行依赖/原readme再在全新接收端复验，仍不导出vendor。
- Modules闭包补3例后`dotween-modules-red1`真实3/71失败；安装器仅新增本机官方九个Modules原件+readme，逐文件固定SHA（与此前官方1.2.825 ZIP取证一致）、保留原meta/注释/版权，不复制Editor工具或改变第三方文件。guard green1仅1项拒绝消息由dependency提前变为runtime dependency不符，实际已拒绝且无receipt；修正该错误类型匹配、不改变拒绝行为后`green2`实际71/71。缺/改Module写入前拒绝、原DLL变化/链接/占用/零测试/optional隔离及Excel/DomainExcel闭包守卫均保留。
- 全新e2重新candidate11裸Create→Import（PID53528/04:13:11Z正常exit0、HarnessPresent=false/SHA不变），再安装SDK/完整测试、DOTween固定运行闭包及双测试assembly，未搬e的Library/Settings/结果。TestsLegacy/legacy1→TestsDOTween/macro1→新进程dotween-list1实际成功；`dotween-edit1`1/1与`dotween-play1`79/79、无skip/集合差异、均正常exit0，Play PID3216于04:16:01Z结束、无强退/超时/挂起。Discovery SHA=`1971dba99569709686504c9feb9f9f52772f2973e1db9eaa7bdb06770accff43`；Edit/Play XML SHA=`e63592cc401455dd84d15b92da32c9212d2917a5e06e1c0bbeff91d5c9f432f3`/`973c29d40180d99c7bf40a0efb1368271ef852a124aa494c8148ccb2c8cbc28b`。这是6.3 Editor真实生命周期80叶闭环，不代替2022/Player或四集成共存，vendor再分发HOLD仍在。
- Source先只读守卫确认2022.3/PID5556、唯一YLFrameworkDev干净、非Play/编译/更新/失败、默认Code.exe；随后仅定向Import可选driver/guard/Reception及DomainExcel产品与测试五个既有资产，所有原GUID不变，返回Compiling=true/CompilationFailed=false/SceneDirty=false。此刻只记Processing，须后续读最终编译状态再正式HybridCLR导出或保护签字；未执行源NUnit/Play/保存或依赖修改。
- 后续Source实际终态已读：同PID5556/2022.3、Compiling/Updating/CompilationFailed全false，唯一原场景干净、DefaultEditor仍Code.exe。Protect checkpoint10实际成功，未触源设置/依赖/场景。正式`HybridCLRIntegrationExporter.Export(...,includeSample:true)`已产出`YLFramework-HybridCLR-U63-h6.unitypackage`，81255B/SHA=`08c295c0f7b42e25077d19788dcc8ff8ab9a315c2b9831836aec5ae26f48d7e5`，28个明确选择路径、无递归依赖。首次snippet因using-var解析问题未执行，修成标准using块后正式成功，不把snippet编译错误计为工程错误。

</details>

<a id="h15"></a>

<details>
<summary>H15 HybridCLR 官方接收、h6-h9 native RED 与冻结</summary>

- Ruling：新HybridCLR descriptor的正式包位于deliveryRoot，evidenceRoot只存该receiver证据，不复制/改写candidate11或在证据根领养同名包。新增两模式×两导入路径4例，`artifact-root-red1`实际2/79失败（新模式误取evidenceRoot），Invoke仅对新schema选择scope.DeliveryRoot后`green1`实际79/79；legacy原根语义不变。额外成本是原工具调用点须传精确descriptor Root/Receiver，不能依赖历史Root默认。
- h6真实schema2 descriptor已创建并Initialize冻结，OwnerSHA=`a3ef630b59834e2f49503b17eeda131fa86768099c99f80e5e8f920f7030672d`；十个输出leaf均先核不存在、无复用H26，创建前G剩余约123GB。Create→同字节candidate11 ImportBase→正式includeSample ImportOptional真实正常exit0，两个product receipt均passed=true/testsPresent=false，包SHA分别f5322465.../08c295c0...且绑定6000.3.27f1/revision672d562a42a7/h6/ownerSHA。之后才Resolve官方Git及装h6-only harness；Inspect真实确认9.0.3/Git/hash6cadd24d101f4cbd00cd80190106f6621132d24d，与本机新PackageCache版本表6000.3.x→v6000.3.x-9.0.1一致。当前尚未Installer、Prepare、NUnit或native构建，不把导入/Inspect当原生通过。
- h6官方Installer已真实完成：h6-install1.json passed=true/PackageVersion与InstalledLibil2cppVersion均9.0.3，进程04:25:45Z正常exit0、editorForcedCleanup=false。实际本地根为`h6/Library/HybridCLR/LocalIl2CppData-WindowsEditor`，标记文件读为9.0.3；安装所取hybridclr native HEAD=`bf01f8eed56a077781c3a7b25f418603c7645588`、il2cpp_plus HEAD=`6eb38b29c3a7448b3ac6c4dee450de375b5d158d`，均官方v6000.3.x-9.0.1。没有改Source或全局Editor安装；这仍不是Player原生运行通过。
- 发现原HybridCLR runner仅核非零XML总数，不能独立证明完整集合。主线程补只在owned独立接收端允许的`HybridCLRTestFixtures.Discover`，用官方TestRunnerApi先核两个实际编译图再取Edit/Play叶、RunState与所有test cs/asmdef源指纹；正常回调释放API后自行Exit，无-quit。新runner要求显式DiscoveryRun，核owner/版本/源码hash、非空可运行集合、28000命令行预算，传精确fullname及assemblyNames，XML须同集合/同数量/全Passed/正常exit。discovery-set-red1实际新增12项失败→green1实际91/91；不把离线语法树称为Unity编译。
- h6先Install50真实测试文件后在当前6.3显式Prepare（h6-prepare1正常exit0），未复用2022载荷；h6-discovery1真实正常exit0，发现Edit125/Play25。h6-play1实际25/25、无skip、精确集合一致、正常exit0。h6-edit1实际123/125、无skip、正常exit2：DisabledDomainReload...仅TearDown恢复原空SceneSetup触发Invalid SceneManagerSetup/No loaded scene，ToolTests.TheSampleSupportExercisesAnActualAsyncContinuation在原240次yield后Task未完成；h6-async-isolated1单项仍失败（约0.020s），不能归为仅前项污染或产品样例已修复。两项未闭合，不改产品/断言/等待预算来造绿，h6源码及原XML/log/selection冻结。后续需owned场景bootstrap与真实Editor同步上下文诊断。
- native预读又定位两个未闭合的新scope接点：Bootstrap.Build回执仍在evidenceRoot查base包、并读旧product-selection.json；RunPlayer仍固定batchmode/nographics。不得在长构建后才发现回执路径错误或把headless写成真实图形/音频；下一批先补包身份前置核验与new-schema非headless参数，保留legacy分支，再以同两个冻结包建立全新h7 native载体。h6 consumer已按不可变合同冻结，不放开Sync/覆盖它来节省安装成本；h7不安装NUnit测试树，用薄生产样例/正式可选包验证原生链，h6两EditMode缺口单独保留。
- new Player参数纯TDD：player-graphics-red1新增两模式均失败→Get-HybridCLRPlayerArguments接入RunPlayer后green1实际93/93；新schema不带batchmode/nographics，旧历史语义隔离保留。新native runner要求非Null graphics并记录设备/分辨率/audio配置；记录audio配置不代表DSP播放。新增ProductPackageSha在Build动作前验证本批两个物理包、product-only receipt/action/hash/owner/版本，拒绝在长构建后才访问旧root或product-selection；legacy仍保留原合同。
- h7已独立Initialize/Create/同两冻结包裸Import/Resolve/安装薄consumer，未装测试SDK、未Installer或native Build。h7-package-proof-green1实际为RED：前置核验误要求receipt中不存在的Project/package字段，且C#比较未规范SHA大小写；实际fallback receipt合法字段为passed/testsPresent/action/packageSha256和四身份字段，均已回读，不伪改历史receipt。h7现场及该次正常非零退出冻结。以现有真实C#五方法（非stub）提取编译并实际执行BCL/JSON/文件边界，package-proof-red1真实1/94失败；按实际action合同及OrdinalIgnoreCase SHA比较修正后green1实际94/94，含base/optional两合法fixture与12项owner/version/action/hash/tests/passed拒绝，不启动Unity或验证真正unitypackage内容。
- 全新h8十个leaf先核空、schema2 owner已冻结，仍使用完全相同candidate11与08c295c0...可选包，无重导出/重绑定h6/h7。Create→ImportBase→ImportOptional→Resolve→薄harness正常完成；h8-package-proof-green1在真实6.3正常exit0并回读passed=true、两SHA精确匹配，闭合构建前包身份入口的实际RED→GREEN。h8-install1实际官方9.0.3本地安装passed；h8-external1真实AssemblyBuilder产出外置Support DLL、artifactPassed=true且正常exit0/无forcedCleanup（04:59:39Z），旧强退例外未使用。Configure→新进程Setup→Validate均正常exit0；未安装NUnit/Input/URP或复制Source Settings/Library。即将首次h8 Development IL2CPP Build；完成后须即时保全该次Library/Cpp/stripped/生成Native证据，在归档前不执行Release或另一基座构建。
- h8-build1实际RED、05:03:25Z正常exit1/无强退：Preflight/PlayerPreflight/Compile/Il2CppDef/LinkXml/EmptyMethodBridge/StripAot通过，FullMethodBridge的AnalyzeStripped前置拒绝Support→netstandard2.1，尚未进入完整GenerateBridge或生成Player/Baseline。`Freeze-H8FailedNativeInputs.ps1`在精确owner/阶段/退出/无Player/无baseline守卫后即时冻结69份真实输入到h8e/NativeInputFreeze（60 stripped DLL、4 native generated、外置DLL及生成资产）；副本和原件长度/SHA逐项一致，receipt明确NativeAcceptancePassed=false/PlayerBuilt=false。h8原Library/Cpp状态亦冻结，不再运行其构建或覆盖consumer。
- 只读任务ses_e7f1aa93affeIpuw5cYq7SSlQW已返回实证并核69冻结文件/两包SHA：Support实际只ref netstandard2.1，rsp有ENABLE_MONO/NET_STANDARD_2_1；SDK6000.3 CoreModule SHA32dd96fe.../MVID6719e354...的EditorCompilation.InitializeScriptAssemblyWithoutReferencesAndDefines IL00a4–00ba无条件用实际PlayerSettings覆盖builder.compilerOptions。CompileExternal在04:59:32先运行、Configure到04:59:53才运行，确为工具顺序缺陷。精确FullName/Cecil限定60 AOT+官方facade解析19TypeRef/21MemberRef均落最终mscorlib，无GAC/alias/加载目标；未证实Unity误裁剪或官方6.3 ABI失败。YL对外部netstandard facade确有独立能力限制，但不在本批Framework合同中扩展，Analyze/ExactResolver/metadata门禁保持原样；临时研究证据在TempGoal/h8-netstandard-analysis1，不当native运行通过。
- CompileSupport新增仅新schema实际Windows64/IL2CPP/Framework前置检查，位于Owned之后、任何外置目录/源码/输出写入之前；external-profile-red1实际1/95失败→green1实际95/95，实际提取if块执行Framework/Unity4.8数值别名接受、Standard/Mono/错误target拒绝、legacy隔离，仍只是离线边界double而非Unity行为。完整源码diff与发现fixture格式检查通过；Source单独定向Import该fixture GUIDba38a66d42e39a7418ef17a2fcdfccea不变，后续实际编译终态无失败、唯一原场景干净、默认Code.exe，Protect checkpoint11实际通过。
- 全新h9十leaf先核空、schema2 Initialize/同两冻结包裸接收/Resolve/薄harness完成，无NUnit/Input/URP。真实h9-external-before-config-red1按预期正常exit1精确报实际配置错误，随后核ExternalSource/ExternalHotUpdate/Assets-HCLRExternal/SourceSupportArchive四处均不存在；先Configure正常退出，再h9-external-green1实际artifactPassed=true/正常exit0。重编DLL5120B SHA=`badbf1e6a239c6a0f11869f3df7a338be09435808e0c1d6ee3661aa31d4d4aa8`、MVID83469f75-9a44-426b-ac33-bb06a8531617，仅ref mscorlib4.0/b77a5c561934e089；新rsp实际ENABLE_IL2CPP/NET_4_6/NET_UNITY_4_8、unity-4.8-api/mscorlib，保留Count泛型与完整Task.Yield/async状态机5方法，非删除异步造绿。元数据只读回执TempGoal/h8-netstandard-analysis1/h9-support-framework.json，SHA7c9cd676...。h9官方Install/Setup/Validate均正常exit0；下一步真正重跑完整原生基座，不能把该编译修复写成native已通过。
- h9-build1实际正常exit1（05:23:03Z），FullMethodBridge已首次真正通过，随后Manifest/PayloadCommit/ResourcePublish也通过，Deploy在Directory.Move拒绝缺失的标准StreamingAssets父目录，未进入最终Player/Baseline。源码和Resource.md:395确认调用方必须先准备该父目录；既有consumer的PrepareDeployment:560已提供显式Recover→创建标准父目录入口，本轮漏执行而非新产品缺陷，不改Deployer/门禁或导出新包。
- h9失败证据在任何Recover/重建前已冻结：Freeze-H9DeployInputs.ps1→h9e/NativeInputFreeze，177文件含60 final stripped AOT、完整生成Native桥接、已提交payload、原deployment workspace/journal，原件/副本逐SHA/长度一致；receipt区分FullMethodBridgePassed=true、NativeAcceptancePassed=false/PlayerBuilt=false。原h9-build1日志/XML/JSON不覆盖。之后h9-prepare-deployment1真实正常exit0，回执passed=true/createdStandardParent=true/previouslyInstalled=false，按正式恢复合同只清该journal登记的stage，不接管未知目录。下一次h9-build2使用新Run，不改变冻结consumer或包，旧Library输入已归档，可推进真实最终Player；不能把恢复成功等同于原生运行通过。
- h9-build2实际已完成完整Player/原生GameAssembly.dll，但整组仍正常exit1：到Deploy/Player全部Succeeded，最后Baseline的ProfileGuid报A saved profile with a stable GUID is required。meta与Profile.asset磁盘仍在，GUIDd8079b97a9cc8d14d99219c75f07538f；不可据此伪造baseline或启动补丁。Cecil核实Baseline顺序AnalyzeStripped→VerifyFinalAot→ProfileGuid→SettingsFingerprint→PlayerFingerprint→Freeze，因此最终AOT集合/字节复验已过，但后面settings/Player fingerprint/Baseline冻结尚未执行。
- 已在下一次构建前即时运行Freeze-H9NativePlayer.ps1，1175文件完整冻结到h9e/NativePlayerFreeze：完整Player、最终60 stripped、Cpp/Il2CppCodeRegistration.cpp及全部C++、4 native generated、外置DLL、ExampleAssets/Profile+meta、consumer源码、LastBuild.buildreport、两报告与ProjectSettings。源/副本SHA和长度全匹配，receipt明确PlayerBuildPassed=true/BaselinePassed=false/PlayerRunVerified=false/NativeAcceptancePassed=false。h9原Library/Player现在同样冻结，不再启动其构建、Sync或伪造base receipt。
- 只读任务ses_e7efe5e42ffeOWTVofAIu5g01e已确认Source/h9 BuildPipeline(640e6457...)和Guard(488d51d1...)同字节、目标程序集加载数0。未发现profile clone/重赋值/显式destroy；最后成功的ProfileGuid在PayloadCommit的ReadOwner，之后还有Refresh、ResourcePublish真实BuildAssetBundles、Deploy导入、最终BuildPlayer，故“最终BuildPlayer卸载Profile”仍未闭合。准确未知为旧wrapper是否fake-null、GetAssetPath还是AssetPathToGUID哪项为空、首次失效边界；不得把普通unload日志当具体对象证据。另静态证实Guard用if(!_profile)return会把活动scope中的失活SO当无scope而跳过，实际h9是否命中尚缺入口观测。后续先建确定性自有SO卸载/持久身份回归与窄阶段诊断；如按冻结GUID重载，必须复验localFileId/persistent/非dirty与开始时Profile/Recipe/Project执行输入及native settings，不能只缓存GUID或重算结束fingerprint充数。只读SDK提示默认AssetPathToGUID可返最近删除GUID，需实际重载/存在检查；不在busy状态重调Preflight、不重用拒绝既有输出的PlayerOptions校验。尚未实施产品修复或新导出。
- 为避免再次长构建后才发现漏PrepareDeployment，补consumer Build的新schema早期标准父目录检查，位于Owned之后任何native阶段之前；deployment-parent-red1新增1/96失败→green1实际96/96，真实if块边界double验证新scope缺目录拒绝、已有合法目录接受、legacy不变。它只提示显式PrepareDeployment，不自动认领/修复未知根；新consumer的Unity语义尚须下一全新接收端验证。

</details>

<a id="h16"></a>

<details>
<summary>H16 三集成136叶、HybridCLR 持久输入与 scope 22回归</summary>

- 独立推进e2三集成共存：在已通过DOTween端显式安装固定Excel产品+DomainExcel产品和测试，TestsDomainExcel/coexist-domain-macro1实际保留DOTWEEN→DOTWEEN;YL_DOMAIN_EXCEL、要求重启；重启后分别实际发现并跑DomainExcel15/15、Excel41/41、DOTween Edit1/1+Play79/79，共136叶，无skip/集合差异、四组正常exit0。四份verdict已回读，XML SHA依次06b4ef24.../b0ea197a.../50ffc7a5.../2fa74af1...；仅6.3下三个集成同环境分别完整作用域，不称同一次混跑/四集成共存/2022或Player已验。未向此端加入HybridCLR，DOTween vendor再分发HOLD不解除；默认candidate11及正式HCLR包不变。
- Profile生命周期测试已先于产品实现完成真实RED：新HybridCLRBuildInputTests.cs由Source定向Import生成GUID6ae1d1a7aa2135340a40f48f9ef94512，未运行Source测试。全新h10先同两旧冻结包裸接收/Resolve，再装52真实测试文件及UTF1.6.0，当前引擎显式Prepare→官方Discover实际Edit147/Play25。h10-inputs-red1仅选新22叶，06:14:56Z正常exit2，1Passed/21Failed/无skip；普通无scope控制项通过，三项真实Resources.UnloadAsset/重复Dispose已确定性复现静默跳检、错误允许嵌套及旧Dispose清新scope，另外18项明确拒绝缺失的持久快照实现，不把未进入的重载路径当系统机制已验。h10产品、测试源码/发现/selection/XML与所有原件冻结，后续不覆盖。
- Ruling：不先用保活根改变native行为，也不只把缓存GUID填Baseline；采用同库ResourceBuilder已验证的持久身份重取模式。新增internal Editor-only HybridCLRBuildInputs，固定Profile/Recipe/Project GUID+localFileID、文件/meta原字节、规范化持久对象引用后的完整内存序列化定义及开始时native settings；重载必须真实存在、同类型/身份、persistent/非dirty、三输入绑定与原指纹一致。它不进入Baseline持久schema，不改public API/serialized profile/依赖/完整AOT或Player fingerprint门禁；资产删除同路径替换、保存/未保存变更均拒绝，不领养新资产。
- 产品实现已写但尚未宣称通过：BuildBasePlayer在PlayerPreflight捕获快照，于Strip/ResourcePublish/Deploy/Player等原生或导入边界重取复验，再以真实对象进入原Baseline流程；Guard新增独立active状态、活动对象失活明确失败、禁止失活后嵌套及幂等Dispose。四源码Format Check(SDK9.0.312/Files4)与指定diff检查通过。Source只定向Import三产品脚本，新Inputs GUIDa3bfc4b457e42d04aaeebab42c43a743、其余旧GUID不变，返回Compiling=true/CompilationFailed=false/SceneDirty=false；须后续核终态后正式导出新可选包，新鲜receiver执行GREEN及完整native。默认candidate11和旧HCLR包08c295c0...不改，h9不复用。
- Source编译终态实际false/无失败后正式export候选2（includeSample29paths、82797B/SHA765663e1588a49bc739ab01afdcde011b8948044bbe85921863e49b66d77a8e1）；全新h11同default裸接收→新optional裸接收→Resolve→测试/Prepare/Discover，h11-inputs-green1实际9/22、13失败/无skip、正常exit2。九项已含三个原scope漏洞、脏输入和未保存对象拒绝；其余全在Capture遇原生m_CorrespondingSourceObject未输出到EditorJson而停止，不把尚未执行的输入变更断言算通过。
- 候选3（82932B/SHAb6804422508941ba04919a30059f249ffae94d25081762fa2d8eaceca5e16783）曾只识别三个null Prefab bookkeeping字段；全新h12真实同链仍9/22，下一原生m_GameObject同样未在JSON，证明两个API本非相同字段集合。两候选/receiver/失败原件保留，不继续用逐个白名单假设布局。
- Ruling：改为两份完整视图联合身份，而非忽略未知字段。候选4的Definition同时冻结完整Editor JSON与SerializedObject所有ObjectReference槽位；存在JSON位置的引用用GUID/fileID替代瞬态ID，所有引用另以propertyPath→持久身份/明确null完整记录，失活/非persistent引用仍拒绝。遗漏于JSON的原生字段不丢失，任何槽位/值变化仍进入hash；移除此前临时三字段白名单，不增加宽泛fallback。Source Import GUID不变/真实编译无失败后正式includeSample29paths导出candidate4，82897B/SHA81852d451410be62b6bda56c8e4102e0b170d7314ca752fac4c921bdec48d701。
- 全新h13同两个冻结包裸接收→Resolve→52测试文件/Prepare/Discover后，h13-inputs-green1实际21/22、无skip、06:40:33Z正常exit2：原三SO wrapper全Resources.UnloadAsset后按同GUID/fileID/相同绑定重载、九种已保存执行输入变化、未保存无dirty标记变化、native settings变化，以及scope控制均已实际通过。唯一失败是替换用例自身提前对默认AssetPathToGUID查询断言新GUID（默认可含最近删除项），尚未调用产品Reload。已仅修正oracle为Unity先在独立路径创建新asset并TryGetGUID证明不同、Delete原asset后Move保留新GUID/fileID至旧路径，再调用原Reload拒绝断言；未改产品candidate4，不放宽断言。测试Source定向Import GUID不变/processing状态已记录，待新鲜receiver复验完整22与原套件。h13产品/测试/发现/原件冻结，不手改其输入。
- h13退出UTC已实际回读为06:40:32.9426162Z，上行06:40:33仅秒级四舍五入。Source对测试oracle更新已实际编译无失败、原Scene干净/默认Code；全新h14同default/candidate4正式包裸接收/Resolve后才安装当前52测试文件，Prepare与官方Discover完成。h14-inputs-green1实际22/22、无skip/精确集合、06:49:14Z正常exit0，闭合原21失败的scope/持久身份回归；h14-edit-full1完整147实际145Passed/2Failed、无skip/正常exit2，仅原DisabledDomainReload...空SceneSetup TearDown与TheSampleSupportExercisesAnActualAsyncContinuation（原240yield/约0.0107s）两项，未新增其它产品回归；h14-play-full1实际25/25、无skip/精确集合、06:51:21Z正常exit0。不称全EditMode已绿，两个历史问题继续单独收口。
- Source Protect checkpoint12实际通过（旧受保护输入仅授权差异），当前新增测试/Inputs脚本meta由Unity生成；source未NUnit/Play/保存场景/改依赖。h10/h11/h12/h13所有RED与冻结包保持，不使用回退、覆盖或Sync造绿。

</details>

<a id="h17"></a>

<details>
<summary>H17 h15 Development 同Player更新、旧Release及负向工具</summary>

- 全新h15是独立薄生产载体，无NUnit测试树/UTF/Input/URP，十leaf先核空、owner初始化/同default和candidate4裸产品导入→Resolve→consumer→真实包proof完成；Configure先于CompileExternal，后者正常回调并exit0/无强退；官方9.0.3 Install、Setup、显式PrepareDeployment和Validate均正常exit0。native基座将用相同已通过22回归的candidate4（81852d45...），默认包仍f5322465...；不把前置Validate或旧h9已构建Player替代本次Baseline验收。当前G约110GB，h9/原Native冻结不清理。下一步h15-build1必须完成真实原生+Baseline，之后即时归档最终AOT/Cpp/Player/回执再运行v1或任何下一构建。
- h15-build1已真实14/14阶段通过（含最终VerifyFinalAot、SettingsFingerprint/PlayerFingerprint及Baseline冻结），07:02:22.6025202Z正常exit0/无强退。base Development receipt已回读：ownerSHA26183b63dfbf06f95409f18b4074394ac3b39393a3a1b97a641611fc75f9fe71、ProfileGUID67a83a648b460dc45a7412adc531e015、CodePackageId893c5d2241ee442384fc18af536cb593f6455b65b63814637ea3e5fab2785673、CatalogDigestb337f52e5a56700893c3f20aacf3f773fce903424cf53df6e82115a3ce77c300、NativeBaselineSHAd40f6c4a49e1bddf621c5735cff55c55340d257c54ac9d29a4785958078d0252、PlayerFingerprint2e8e08de352f55a4b3ab9ec5e2d66f061a3b6810cab0a930f97fae6128a0d90b，h15b已完整生成。这证明持久身份修复贯穿真实native+Baseline，不反推h9首次失活的具体native边界已观测。
- 成功基座首次运行前已运行Freeze-H15NativeBase.ps1，完整1247文件冻结到h15e/NativeBaseFreeze（final60 AOT、完整Cpp/4 native generated/Player/Baseline/ExampleAssets/consumer/外置热更、LastBuild.buildreport、settings、manifest/lock/构建/退出/base receipts），所有源/副本SHA长度一致，baseline.json/complete.json/baseReceipt绑定一致。初次脚本误引用未定义Get-HybridCLRPlayerHash在建目标前拒绝；改为提取现有真实Get-ProjectSubsetFingerprint函数，不启动Player，再成功归档。归档receipt当时PlayerRunVerified=false，后续运行证据另记，不覆盖它。
- 审读发现普通RunPlayer原指纹仅前后自比且每文件SHA大写，与native baseReceipt的小写ordinal协议不同，无法锚定已冻结基座。新增真实FS hash/两模式匹配和变异3例，player-base-binding-red1实际2/100失败→复用已有严格Get-ProjectSubsetFingerprint于新schema普通Player并在启动前比base.PlayerFingerprint后green1实际100/100；legacy原语义不改，Subset原门禁保留。变更仅工具，未改candidate4、player或Baseline。
- h15-v1-1已真实非headless运行：PID54544于07:12:48.5402428Z正常exit0，原JSON/61行完整原生日志/process/exit已回读，simulation=false、Direct3D12/RTX4070TiSUPER、3840x2160、audio48000/1024（仅配置，不等于DSP）。message真实Native:v1、dependency-count2、async=v1-async、prefab=v1-prefab；代码路径已核实际ModuleRunning、重复同loader引用幂等、错误base在Manifest前拒绝、CloseAsync后ResourceSystem.Current=null。运行前/后全Player指纹均等于正式base的2e8e08de...，不重建Player。日志只有D3D信息队列/统计时间戳警示，无业务异常；退出的Unity MemoryLeaks标签汇总不被解释为全局零泄漏或缺陷证明。MVID仅PE装载前验证，IL2CPP模块GUID反射不支持仍明确记录。
- 仍未完成：同Player Development v2补丁/签名HTTPS更新/同进程替换拒绝后重启V2、BadPayload/Metadata/Signature、非Development/Low独立Release与两项目共享Support子集、真正热更UGUI/Entities-ECB场景，以及2022与两个Editor历史缺口。先完成Development补丁再切Release，以免误用已改的Profile/baseline绑定。New-HybridCLRSignatureFixture.ps1/Start-HybridCLRFixtureServer.ps1仍legacy-only，必须先按schema2严格owner/凭据域/版本/pin做离线TDD升级后仅主线程实际执行；现有h15还没有TestCredentials，不借用Resource63或历史H26私钥/证书、不装系统信任。新scope Freeze/Audit/Sync等仍未升级，不能调用旧工具冒充本批验收。下一goal直接沿这些实际缺口继续，不重复已通过22/14/NativeV1或Resource长组来代替它们。
- TLS工具本批已升级（签名负夹具工具仍未升级）：新Start-HybridCLRFixtureServer显式Root/Receiver/Run、真实产品裸导入前驱、独立凭据owner、Root/owner/version/certSHA/PFXSHA、过期/链接/占用拒绝，阶段目录写齐后才Move，不安装系统信任；CompileOnly不创建证书/开socket。fixture-custody-red1新增7/107失败→green1因测试用&而非dot-source丢失校验函数仍7失败→仅修测试作用域后green2实际107/107，未弱化断言。h15-server-compile1实际C#host编译完成且检查TestCredentials不存在；随后h15-certificate1首次显式创建两日loopback PFX及归属回执，public证书SHAa30fdc6b33880a0320f6f55dbb2aa7489aa539d69d46a5e3a0fe4a14fef710a4、PFXSHAad841cb85e5006f6cd7565dc533c67fecda2d62ed29b6fbcacb7253305fed1a6，SystemTrustModified=false。不记录PFX内容/私钥/真实凭据。
- h15-external-v2-1是主线程误用不存在CompileExternalPatch入口的失败，Unity日志明确method not found；包装器FileSystemWatcher阻塞（尚未观察进程退出），CIM已核5956不存在，仅停止该tracked包装器bgp_1817b017b001utj0dtwPS0rEGC，未触Source5556。不补造compiler/exit receipt、不把它算正常exit或通过。源码精确入口为CompileExternalVTwo；同时新schema回调改等真实进程终态，再核原子回执/DLL SHA，不再因文件事件阻塞提前失败进程，legacy原callback策略保留。callback-early-exit-red1新增1/108失败→green1实际108/108；正确入口新Run h15-external-v2-2实际artifactPassed=true/正常exit0/无强退。
- h15-patch-v2-1实际六阶段Preflight/ReadBaseline/Compile/PatchCompatibility/PayloadCommit/ResourcePublish通过、正常exit0，没有Strip/Bridge/Player/Deploy阶段。正式patch-receipt已回读绑定四身份：CodePackageId9a725dcbef583d094c58cf1f2d3e465748b47fc11f49e7fa0b2f6cb92f92c0ce、CatalogDigestfb2d47d94a6eb806d6fd8ff9e17d697472733d8972ba1d06f56c47f8dcbb1dab、PlayerFingerprint仍2e8e08de...、BaselineSHA仍d40f6c4a...；ServerV2由已验产品ResourceReleaseSigner自验RSA签名、绑定当前精确PFX生成。
- 新FixtureTrust前置同时比ReadyPath/publication/envelopeSHA、owner/root/version、owned cert/PFX、DER证书与modulus/exponent，endpoint只能HTTPS/127.0.0.1且无query/fragment/userinfo，拒绝外网/公钥替换；fixture-trust-red1新增1/109失败→green1实际109/109（内存自签控制，无真实网络）。RunPlayer对新scope Update/BadPayload启动前执行，旧模式不改。
- 首次HTTPS传输是保留的真实RED：ServerV2-h15-server-v2-1使用SChannel+EphemeralKeySet，服务端明确AuthenticationException platform does not support ephemeral keys（0x8009030E），0HTTP/1error；h15-update-v2-1原JSON passed=false/正常exit21/Player字节未变，未计成功。不改客户端/系统信任/私钥存储策略；自有.stop正常关闭旧服务，原Key/证书/失败原件保存。
- Ruling：复用本批Resource63已验的Node/OpenSSL pfx传输模式，避免Windows SChannel持久容器要求。新增Tools/HybridCLR/HybridCLRFixtureHost.cjs，仅builtin Node模块/127.0.0.1动态端口/读取同一隔离PFX，复核真实root-ownerSHA/凭据/签名envelope、拒绝链接和覆盖；全部HTTP记JSONL，区分声明bodyLength与completed，不把预期长度当真实传输测量。新Start入口CompileOnly变为node --check且不触凭据/socket；旧C#host仍只供legacy。Node版本沿本机24.19，不加依赖/权限/信任。h15-node-compile1实际通过；新ServerV2-h15-server-v2-2服务已实际ready（精确请求时序见原JSONL），不复用旧Ready或.stop。
- h15-update-v2-2已真实正常exit0（08:10:05.1553889Z）：checkAndPrepareWhileRunning/activateBusy/sameProcessReplacementRejected均true，实际激活fb2d47d9...；仍运行v1时拒绝同进程v2不是装载成功。紧接同一Player的h15-v2-restart1真实正常exit0、simulation=false、D3D12，message为Native:v2、dependency-count2、async=v2-async、prefab=v2-prefab，CodePackageId与激活Catalog精确匹配；未重建或替换Player/基线。Node原始7行HTTP记录全部200/completed=true（release/catalog/bundle与manifest/complete），自有.stop后进程正常退出，Requests7/Errors0，输出exit.json；启动前/后Player指纹均为正式base的2e8e08de...。Development正向更新/重启链已闭合，BadPayload/Metadata/Signature负向、Release/子集等仍待做；下一步可切ConfigureRelease→CompileExternalRelease→Build（此时SourceSupport已v2，因此Release消费者用ActionV2，不伪称v1）。
- h15独立Release链已实际推进：ConfigureRelease→CompileExternalRelease（flags None，沿用已v2源）→Validate→h15-release-build1，全部正常exit0，构建14/14阶段成功（08:23:21.5991131Z），Development=false，CompatibilityId=u63-c63a1-6000-3-27f1-h15-release-v1，独立Baseline=h15br/Player=h15pr，不复用Dev基座。base-release-receipt已回读CodePackageId479316dd0ea8fd40fd15bb8680565224429cf5b1efb7b69269419aa46e040d24、Catalogafe10d87d7b7bf83c23ed10921bb212dad52f0b774e4ebf9938692a5f2a6ca9f、BaselineSHAd1184c42d9f97bba7a5799b7bb2c5c698db14f3260121ef0fe04966cc1f92669、PlayerPF0d6e86c13aa7eca8de8af22e01e1b93bd37e8c85f0c9ed85899737d6effc5b60。
- Release第一次运行前Freeze-H15ReleaseBase.ps1已即时冻结1236份完整最终输入/输出到h15e/ReleaseBaseFreeze（final stripped/Cpp/native generated/Player/独立Baseline/配置与原报告），源/副本SHA和长度全部一致，archive receipt当时PlayerRunVerified=false。之后h15-release-v2-1真实正常exit0（08:26:48.9145867Z），非headless/D3D12、simulation=false、message Native:v2/dependency-count2/async=v2-async/prefab=v2-prefab，内容id精确匹配独立Release receipt，前后PlayerPF均为0d6e86c1...。该链是非Development/IL2CPP/Low的本地Native消费，不是Release HTTPS证书正向证明。
- Release配置原件已核scriptingBackend Standalone1/API3/managedStrippingLevel1；仍保留唯一符号YL_RESOURCE_ACCEPTANCE_TESTS，不宣称ExtraDefines为空。精确Runtime搜索只三处ResourceRemoteSource分支，均还要求DEVELOPMENT_BUILD；consumer也在普通Release的TLS信任分支明确PlatformNotSupported，因此该符号未把Development loopback豁免编进普通Release。若最终发行验收要求无验收符号，需另做显式新配置/独立证据，不把本次描述成空符号发布。
- 下一步剩余native门禁定位已查：consumer确有PublishBadPayload/PublishBadMetadata（后者固定产物名bad-metadata-fixture.json），当前RunPlayer.BadMetadata却使用bad-native-metadata-receipt.json，先修new-schema确切候选名及owner/项目/compatibility校验再启动，legacy不动；h15当前Profile已Release，不能把由它发布的Release候选当Dev同compat输入。BadPayload原consumer需要Development loopback，普通Release不支持；BadSignature原consumer没有专用Action，不能用错误退出/throw冒充已通过的原生负向测试。New-HybridCLRSignatureFixture仍legacy-only，不能直接执行。两个Project共享Support子集、真正热更UI/ECS、两Editor历史缺口、2022与最终Review/双版本发行/保护after继续未闭合。
- 本轮Node传输落定后重新执行node-transport-green1，实际109/109、无Unity/网络，指定tracked diff --check无输出。旧C#Host已恢复原三参数签名/DefaultKeySet（仅legacy路径），保留链接拒绝遍历；新schema始终走Node/OpenSSL，不留未使用的ephemeral SChannel选择开关，不把曾经C#CompileOnly当Node服务验收。实际正向Node网络证据/正常stop独立于纯guard；当前两服务、Unity验收端和Player均已退出，仅Source5556及其既有worker保持，无源Play/测试/重启、无提交/推送。
- 台账纠正：上轮关于BadMetadata驱动使用bad-native-metadata-receipt.json的描述错误；本轮已读真实Source及git diff，HEAD和工作树一直使用bad-metadata-fixture.json，与冻结h15 consumer相同，无须改名。实际缺口是候选无owner/版本/原生成Run绑定；新增仅new-schema的PublishBadMetadata成功退出后生成Run.receipt.json四身份+固定候选SHA，RunPlayer显式CandidateRun核候选字节、归属/项目/compat和本端AssetBundles/catalog叶路径。metadata-fixture-binding-red1新增1/110失败→green1实际110/110，legacy路径语义未变，不把模拟IO/叶检查当native。
- h15-bad-metadata-publish1真实正常exit0，候选Catalog b22d5e2628912a5925f30dc4e3534eed7e8e3d28023794bd6334d3cd17b46326、compat精确Release、producer候选SHAb468b645e6bbcc6175f23dec8fbc62fb3b7f6b18756cfb5c6c7377995c6f2e03；只在本端生成，finally后live manifest与ReleaseBaseFreeze SHA均E5656E25FBFA5395D4536F04954A3C9B075D07D81DFA382B3CF437FB1AD5F554。h15-release-bad-metadata1真实D3D12/非simulation、正常exit0（08:51:04.0280319Z），nativeErrorCode=3/assembly=zzAbsentAot/partialMetadataRestartRequired=true，代码路径已核部分AOT成功后失败与new loader仍RequiresRestart，Player全字节前后仍0d6e86c1...。此项负向已通过，不混同普通抛错或未进入native的PE拒绝。
- 为BadSignature增加专用正常退出消费分支：仅精确ResourceException.UpdateFailed→InvalidDataException("Release signature rejected.")可接受，TLS/网络/catalog/普通错误/空均拒绝；Resource/Core停止后才passed，不调用HybridCLRLoader。signature-consumer-red1新增1/111失败→green1实际111/111。Build回执新增NegativeConsumerVersion=1，Driver只允许新capable Development基座；h15旧二进制不重新解释，不Sync/重建覆盖它。Runtime模板首次写成ResourceRequest.Exception是API错误，详下实际编译RED，未计native通过。
- 签名负夹具新schema已升级：exact Root/Receiver/Run/产品前驱/Development base及patch PlayerPF+baselineSHA绑定、owned证书/PFX归属、先用公钥验证原正签名及规范信封，再只变1bit、原Payload/正发布不变、stage全新后Move。signature-fixture-red1新增1/112失败→green1实际112/112。首个live h15-bad-signature-fixture1在任何目标写入前因误读patch.ProjectId字段拒绝；实际patch合同没有该字段，改用其真实PlayerFingerprint/BaselineSha256锚base，并用payload项目/compat/catalog锚定，未改旧回执。新Run h15-bad-signature-fixture2实际Prepared/NegativeFixtureOnly/PayloadUnchanged=true/SignatureBitFlips1，sourceSHAf583d9dd122ebe07f59a514ae7ef14a8422448926b5cb5254fc6e47b00389b38，badSHA1e2198294ac8b4b5d7544bc8a61618d11d04e24fa99742c0afd50d98051fac98；尚未native调用此签名候选，h15无capability会启动前拒绝。
- ConfigureRelease新schema新增显式清空验收宏、回执记录实际DefineSymbols；ordinary-release-defines-red1新增1/113失败→green1实际113/113（实际if块double行为，不是Unity执行）。旧h15 Release含符号历史不改，不冒充已重新编无符号；新consumer需新receiver。默认/正式HCLR candidate4产品包均未变（仅Tools更改）。

</details>

<a id="h18"></a>

<details>
<summary>H18 h16编译纠正、h17三原生负向与普通Release AV</summary>

- 新h16十leaf先核空、schema2 owner→Create/default裸Import/相同candidate4裸Import→Resolve→新consumer已完成，未安装NUnit/业务插件或启动Installer/native。h16-package-proof1真实编译RED：ResourceRequest<UpdatePlan>没有Exception（三CS1061），仅此新consumer分支错误，原h15正向/负metadata证据不失效；h16消费者与原.log/正常非零exit冻结，不手改生成副本。已定向读真实ResourceRequest.cs:57，Source模板三处改为Error，其他HybridCLRLoadRequest.Exception原合同保留。只读后台任务ses_e7e29f568ffe0QhTjk6OQ4h4OX正在用精确h16 SDK/rsp/真实引用做旧source RED→当前模板离线语义GREEN，主线程不重复该工作；未返回前不把113语法/边界结果当整段语义编译通过。下一步准备全新h17，待该结果后真实Unity编译，再串行Configure→CompileExternal→Install→Setup→PrepareDeployment→Build，专用BadPayload/BadSignature门禁需要该新二进制；既有所有h15档案保持。
- 离线任务已完成并回读final-verification.json：精确Unity6自带Roslyn4.3.1、15真实refs/原Editor defines，旧h16逐字节源RED exit1恰3个CS1061→当时修正模板GREEN exit0/0error/1warning CS0162:140，无stub/nowarn/分析器/目标程序集执行，refs SHA/MVID与原输入不变；三Error语义绑定实际ResourceRequest<UpdatePlan>.Error。完整证据TempGoal/h16-offline-consumer1，源455FA7D4.../渲染6278635D.../输出17F26A23...。随后agent查到Run():121白名单漏BadSignature，主线程补actualif块行为回归signature-action-red1新增1/114失败→green1实际114/114并修显式白名单。最终Source比已离线编译快照仅该一行，多出的可达性不冒称旧DLL已验；新h17后续真实Unity编译覆盖。
- 新h17全新十leaf/owner，同default/candidate4裸Import→Resolve→新consumer→h17-package-proof1已真实Unity编译exit0（涵盖最终白名单/Error API），再Configure→CompileExternal→官方Install→Setup→显式PrepareDeployment→Validate→h17-build1全14/14正常exit0（09:47:04.9291380Z）。base四身份绑定OwnerSHA46aa82a3af4e1031f7dfcdc2ce941fcf1196f4bb8083e2c07ee2df7105e57a9c，NegativeConsumerVersion1、CodePkg0a9d1cc61690c8ec821a6afb494b2f1d5b609d27c9dc9b6ea92a85511d54dbad、Catalog36224d9f9513ca41ff571d80a97a103f899fcbc47216dcc6c2be147bbd2700aa、BaselineSHA9b0eea1d1762cda1b54fb467b7f9ed6b4704227fbf6f854b851c65d20123eff1、PlayerPFcecc55460a06a28e89c4db31f126ea03abaca167a9f95d3d73de1b1b13eaad95。运行前Freeze-OwnedHybridCLRBase.ps1已严格核报告/完整marker/基座/包前驱，1247文件源副本一致冻结NativeBaseFreeze；普通V1控制正常exit0，无改Player。
- h17各负向已实际不同层级闭合，同一Development Player指纹cecc5546...始终不变。h17-bad-payload-publish1签名有效负候选→独立ServerBad Node服务→h17-bad-payload1正常exit0、badPayloadRejectedBeforeNative=true，精确Support长度/SHA错误/Stage.Payloads/RequiresRestart=false；自有stop后Requests7/Errors0。随后当前源CompileExternalVTwo→Patch真实成功，正签名先公钥验真再NewSignatureFixture生成exact1bit负候选。h17-bad-signature1正常exit0、signatureRejectedBeforeNative=true、ResourceError.UpdateFailed/Inner message Release signature rejected./stopped=true；原JSONL仅1次GET /release.json、200/completed=true，无catalog/payload请求，自有stop后Errors0。h17-bad-metadata-publish1→h17-bad-metadata1同Developer Player正常exit0，nativeErrorCode3/zzAbsentAot/partialMetadataRestartRequired及新loader阻断。以上不是普通异常或退出失败记绿，签名只有新capable binary可调用，h15仍不重解释。
- h17普通Release已显式ConfigureRelease回执DefineSymbols=""/development=false；CompileExternalRelease flagsNone（源v2）/Validate/h17-release-build1十四阶段全部成功，独立h17br/h17pr，BaselineSHA13bdcf51bc8ca27d02816ade612c7a19300641bf691e01e9171d4b96de8bc712/PlayerPFa15c88744fec39fbceea0211f8a8181f09b1481f9ff6a35cb69b0c456c2909cb。首次运行前1236份完整final AOT/Cpp/native/Player/Baseline冻结ReleaseBaseFreeze，不把构建绿当运行绿。
- 新实质RED：h17-release-v2-1于10:09:30.7834243Z正常被OS以AV 0xC0000005（exit-1073741819）终止，前后PlayerPF仍a15c8874...，没有result JSON，不计通过或概率重跑。原log129-147 native栈：GlobalMetadata.cpp1354 GetParameterInfo→MetadataCache1289→Method.GetParamName124→Reflection.GetParamObjects362→mscorlib.GetParameters→Newtonsoft DefaultContractResolver callback/array contract→ModuleManager.RunStartup（启动Core JSON反序列化），不能凭时间点直接归因验收宏/显卡/热更ABI。h17原Library/Player/基线冻结不再构建。Freeze-H17ReleaseCrash.ps1已即时复制8份exact自有dump/匹配GameAssembly.pdb+DLL/原拦截log/当前log/process/exit/base receipt到ReleaseCrashFreeze，源副本SHA/长度一致，NativeRunPassed=false；已read权限定位本次Temp/YLFrameworkU63Acceptance/.../Crash_2026-10-28_100914797，未读其它进程dump。

</details>

<a id="h19"></a>

<details>
<summary>H19 Release只读定位、h18-h21 async修复与Session剩余失败</summary>

- 只读后台ses_e7e0008f4ffeEOTUecABs9cLYx负责精确native/Cpp/最终元数据/dump根因研究，零Unity/Player/网络/PackageCache写；已告知保全路径，主线程不重复或修改其目标ModuleManager/Json/native。在等待可独立推进工作中，仅向原ToolTests.TheSampleSupportExercisesAnActualAsyncContinuation增加原240yield失败诊断（context类型/elapsed/editor time/frameDelta），不延时/不改断言；格式已通过，Source只定向Import旧GUIDa9fb0f25c8f0c4e4c93f04694a27e380返回Compiling=true/无失败/原场景干净，须核终态。已检查h18 descriptor及十输出不存在，尚未创建，用于新鲜Editor调度诊断，不借h17原生崩溃作为其它门禁block。
- h18独立诊断端已真实Initialize→Create→同default/candidate4裸Import→Resolve→52测试文件→当前引擎Prepare→官方Discover，Source测试更新后终态实际无编译失败/原Scene干净/默认Code，源NUnit/Play均未执行。h18-async-context1原单项正常exit2，原240yield和成功断言未变，实际诊断WaitingForActivation/UnityEngine.UnitySynchronizationContext/elapsedMs5.556/editorSeconds0.005530/frameDelta0，不能把240个enumerator步数当240次真实Editor更新。旧失败与新XML/source/discovery均冻结；只读ses_e7debf6a5ffeo3UedcYuNi0b21正追踪精确UTF1.6.0/1.3.9执行/pump与两版可用的真实更新等待，不改产品或SDK、不轮询其进度。Session空SceneSetup仍需单独owned saved-scene bootstrap或合法还原契约，未加Ignore/删断言。
- 普通Release AV只读分析已交付并读板，证据TempGoal/h17-release-crash-analysis1/analysis.json；目标程序集执行加载数0、12份关键freeze字节复验一致。原始AOT method是System.Array.InternalArray__RemoveAt(Int32)，token06000d3e/global method3389/parameterStart5423，磁盘正确参数index/token08001534/typeIndex17500；dump实际错误记录name/token/type均04000400，typesCount32537，RVA1eacec的RCX+RDX*8精确匹配AV地址。ArrayMetadata.cpp277-298复制非generic AOT MethodInfo但换klass，:527数组image取element image；GlobalMetadata.cpp1352按klass->image把AOT参数index送到Interpreter表，错误归属证据强于“裁剪缺失”。v39磁盘全部method/param范围无已发现越界，h15/h17 stripped mscorlib同字节/MVID/关键IL，h15 native也同错误分流，故不归因宏/显卡/缺失AOT或假称h15原生安全。
- 最小上游修复候选为GetParameterInfo使用本文件已有methodDefinition归属重载（206-218），而非klass image；目前仍无fix GREEN，小dump缺live Image/MethodInfo/_params实时对象，h15/Dev存活原因未知。官方PackageCache和h17 LocalIl2Cpp/Library不写、不删Module、不Preserve全部mscorlib、不用未裁剪DLL替代。若推进native源修复，必须先建立本批明确归属且可维护的源fork/patch与精确上游/修改SHA，再经新receiver官方local安装路径验证，不能直接手补Library或悄悄升级固定依赖；尚未实施，不宣称普通Release完整通过。另记录只读任务曾单次h15 metadata误读live路径，已用指定ReleaseBaseFreeze副本重验SHA和字段一致，分析保留偏差说明。
- UTF只读研究ses_e7debf6a5ffeo3UedcYuNi0b21已完成，主线程读板及完整结果：1.6.0/1.3.9非同步路径每次EditorApplication.update仅推进一个叶null，不是一次回调内的240循环；Task.Yield投递UnitySynchronizationContext需要PlayerLoop.Update，Editor回调次数不是帧数。[Test] async Task同样只被轮询，不是修复。此前“可能单update多次推进”假设已排除；不改UTF源码、不用sleep/Task.Delay/随机重试。候选QueuePlayerLoopUpdate由实测控制验证，不凭文档直接宣称有效。
- 新增AnExplicitPlayerLoopUpdatePumpsTheEditorContextWithinTheOriginalBudget：Post单marker、最多240次公开QueuePlayerLoopUpdate+yield，计数必须恰1。Source格式通过/定向Import旧ToolTests GUID不变/真实编译无失败/原Scene干净/默认Code。全新h19同两个冻结产品包裸接收→Resolve→52测试文件→当前引擎Prepare→官方Discover，h19-pump-marker1实际1/1、无skip/精确集合/正常exit0，确认batch EditMode可主动请求player-loop来pump，而不是增加毫秒等待。
- 原TheSampleSupportExercisesAnActualAsyncContinuation只在原240for内增加同一QueuePlayerLoopUpdate，原IsCompleted/IsFaulted/结果v1-async断言与失败上下文全留；未移动或删除旧测试。新h20同完整裸导入/依赖/Prepare/Discover链后，h20-async-green1原样例+marker实际2/2、无skip、11:15:52.0851404Z正常exit0。h20-edit-full1完整148实际147Passed/1Failed、无skip/正常非零，仅原Session空SceneSetup TearDown仍报无loaded/active scene；异步门禁已在6.3闭合，2022侧仍须同契约验证，不以Native async历史替代它。
- Scene回归第一次最小尝试保留：Source SessionTests对原setup.Length0不调用RestoreSceneManagerSetup，改仅关闭_ownedFolder路径下scene并断言Empty，仍拒绝未知scene；格式与Source定向Import GUID0b4e686ab5013d040810fab03d6a8508不变、编译无失败/原Scene干净。新h21裸包/Resolve/52测试/Prepare/Discover后，h21-session-green1实际1/2、无skip/正常非零，DisabledDomainReload... TearDown在CloseScene(scene,true)返回false（源码166行）失败，证实不能靠关闭最后owned scene还原runner的零SceneSetup。此候选未通过，不计已修，不放宽return/empty断言或接管未知场景；h21测试及XML原件冻结。
- 下一步Scene应采用合法owned saved-scene运行宿主（在官方RunStarted、fixture SetUp前建立，类似已有Stage9守卫）或更有证据的还原契约，不能继续猜CloseScene。新宿主仅严格schema2/root-owner/精确Unity/当前project且无脏/未知scene才允许，保存只限新生成owned空Scene，不覆盖用户或Source场景；Source无runTests/root参数应完全不注册。原Session业务生命周期断言不删。h18/h19/h20/h21均无NUnit/Play在Source执行，未新增/切分支、未提交/推送或改权限/依赖。

</details>
