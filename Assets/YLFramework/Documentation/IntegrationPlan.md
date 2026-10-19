# XFGameFramework × YLFramework 长期融合蓝图

**阶段9当前状态（2026-10-19）：独立工程交付技术验收与用户验收完成，已获精确提交及普通Push授权。** 受测源码由 `integration/xf-yl` / `586eebedaf900d926d2e30301b20ed3129decb27` 加冻结交付修复组成，不能将旧HEAD冒充已含修复的基线。以下阶段8/9“未启动”文字保留为历史快照，本轮范围、证据与边界以本文§9为准；本次只收口阶段9及另行验收的拓展公共准备E0，不自动迁移13项系统、不创建Worktree，不改既有失败、限制或历史制品。

**新增阶段 7.5（2026-10-14）：技术实施与定向验证完成，已提交/推送（`63e152e`）。** ColorSelector 四个类型完整归 `Utilities/UI/ColorSelector`，作为依赖 Core 的上层便利工具；按用户选择删除公开 `IUIInputGate` 与自定义门禁，直接遵守 Panel 的加载/输入策略，不再要求脱离 Core 的源码闭包。XF 完整 MIT 许可集中为 `Assets/YLFramework/LICENSE.md` 一份，保留版权与全文并显式随默认导出分发；其他来源许可不变。两主体程序集、取色类型身份/字段/GUID与正式Prefab保持，引用退出接口的客户端需移除引用并重编译。取色18（含双profile消费源码编译）、完整许可1、Help7、正式窗口Play代表2及实际导出/解包1通过，不宣称新Player/全量测试/全平台验证。阶段 7 已有 Git 里程碑保持；后续仍为阶段 8 资源融合、阶段 9 独立交付。实施与验证见 [结构方案 §18](../../../.kilo/plans/xf-yl-7-structure-redesign.md#18-阶段-75-便利工具与-xf-许可集中2026-10-14)。

**Tidy 架构分层治理：COMPLETE（Tidy-1～3 全部技术验收及 Git 收口，里程碑 `milestone/tidy-architecture`）。** Foundation 解除对上层 Utilities 扩展的依赖，改直接使用 Unity 原生 API 且不新增便利 helper；七个 Editor 通用支撑按职责归 Foundation/Utilities，原 `YLFramework.Core.Editor` 身份保持。完整 Foundation 及 Foundation+Core 源码守卫分别约束两主体内部层级，保留两主体程序集与 Systems/Utilities 合理协作，真实 Json.NET 及仍属**阶段 8 过渡**的 XFAB 引用显式核验、不误禁合法上层协作；只验证 Editor/Windows64 Development 两 profile，不宣称最终独立或 Player 重建。批次事实、导出门禁与已批准的环境例外见 [Tidy方案](../../../.kilo/plans/xf-yl-tidy-architecture.md)；**阶段 8 资源融合尚未实施**，阶段 8/9 不自动启动。

状态：蓝图已确认；**阶段 1：COMPLETE（1.1～1.4 全部验收及 Git 集成收口完成，`6cea527`）**，Module/MVC、View/Panel 与原游戏 Editor/Windows64 代表性非干扰、Core Windows64/Mono 代表性验证通过。**阶段 2：COMPLETE（2.1～2.5 全部验收及 Git 收口，里程碑 `milestone/2.1-pool`、`milestone/2.2-events`、`milestone/2.3-update`、`milestone/2.4-timer`、`milestone/2.5-serialized-dictionary`）**。**阶段 3：COMPLETE（3.1～3.4 全部验收及 Git 收口，集成 `7e44d9f`，里程碑 `milestone/3-ui`）**。**阶段 4：COMPLETE（4.1～4.5 全部验收及 Git 收口，集成 `0a0e74f`，里程碑 `milestone/4-audio`；Windows64/Mono Player、原游戏非干扰、系统实际出声与用户人工听感均已确认通过）**。**阶段 5：COMPLETE（5.1、5.2、5.3、5.3.5、5.4、5.5 全部验收及 Git 收口；合并提交位于 `integration/xf-yl`（5.5 提交 `16e283a`），里程碑 `milestone/5-runtime`）**。**阶段 6：COMPLETE（6.1～6.5 全部验收及 Git 收口，集成于 `integration/xf-yl`，里程碑 `milestone/6-editor`；Help、UnityPackage、Windows64/Mono Player 与编译器隔离门禁通过）**。**阶段 7：COMPLETE（框架结构整理；2026-10-14 Git 收口，里程碑 `milestone/7-structure`）。** **Tidy 架构分层治理：COMPLETE（Tidy-1～3 全部技术验收及 Git 收口，里程碑 `milestone/tidy-architecture`）。** 7.1 `Runtime/Shared → Runtime/Foundation/Kernel` 已完成用户技术验收与 Git 收口（`0f9876f`）；7.2 `Runtime/XFMain`、`Editor/XFMain → Core`（含 Core 内部职责目录、默认资源路径与 Analyzer 接线）及后续 7.R1→7.R4/7.O1 结构补充、§16 两主体程序集收口均已完成技术验收与 Git 收口；最终产品主体为 `YLFramework.Runtime`、`YLFramework.Editor` 两个程序集。阶段 7 保持公开类型、程序集身份与功能契约，暂时保留 Core 对 XFAB 的现有依赖，不改变 `ProjectName` 语义、不新增资源替代后端。资源系统深度融合调整为**阶段 8**（以 XFAB 成熟能力体系为主体）；最终收口与独立交付调整为**阶段 9**。临时 XFAB 依赖、真实 AB 释放、跨平台与最终独立运行仍属后续事项。原 XFGameFramework、XFABManager、XFFSM 等原系统继续留在 CGJ2024 供原游戏使用，原游戏不迁移到新 YL；新 YL 通过源码承接和功能融合逐步独立，最终不依赖原 XF 生态程序集。本文是后续专项的共同约束，不自动授权 Git 或下一专题；当前状态及依据见第 6～8 节和各专项方案。

**阶段7结构补充：** 推荐方案 A 的 7.R1 → 7.R2 → 7.R3 → 7.O1 → 7.R4 已实施并通过各批技术门禁。Runtime 归 Foundation/Core/Systems/Utilities；原生 EventListener Inspector 验收后 Sirenix 整树退出，五个未使用宏按配置权限保留。既有程序集/GUID/API/字段、Analyzer scope、Core XFAB 依赖与 ProjectName 语义保持；真实导出/解包及 Windows64/Mono 开发 Player 持久 UnityEvent smoke 通过。成果已随阶段 7 Git 收口提交（`milestone/7-structure`）；不自动启动阶段8或宣称最终独立。依据见 [结构重整方案 §15](../../../.kilo/plans/xf-yl-7-structure-redesign.md#15-方案-a-实施结果2026-10-13)。

**§16 当前实施（2026-10-13）：** 用户已明确授权两主体修订及 Samples/ColorSelector 退役。S0/S0a 完成：保护实际未提交工作，删除样例整树及根 meta、保留正式取色能力并完成导出/颜色交互门禁。S1 已写入 Runtime 合并、Foundation/Core/ZipTool asmdef/meta 退役、相关引用/IVT 与 Analyzer SDK 绑定/唯一产物更新；SDK 123 项全绿，真实 Unity csc 七项 YL Warning 正/负见证成立，原 XF 分析器 Error 不关闭。曾在 SerializedDictionary UI Toolkit 两项失败处停止；授权定向诊断后，以有上限的真实绑定状态等待替代固定帧假设，最终整类连续两次 15/15，Drawer/容器实现不改。XFAB 断言精确禁止反向可选适配器依赖，保留 Core 临时 XFABManager；边界 1/1、Audio Editor/Windows Development 源码闭包/白名单 3/3。三个跨 Play 测试在干净现场独立各 1/1，未保存场景安全断言保持；组合运行前置阻断不计绿。Core EditMode 本次 60/60。549 个公开类型和 445 个脚本 GUID/绑定仅发生准许迁移，受保护字节保持；S1 其余门禁待收口，S2-S7 未开始，Core.Editor / Editor.Help 仍待退役。不得沿用 §15 Player/游戏结果验收本次迁移，旧 AQN/二进制消费者须重编译；本轮不宣称全量组合 EditMode 绿。依据与范围见 [结构重整方案 §17](../../../.kilo/plans/xf-yl-7-structure-redesign.md#17-16-实施状态2026-10-13)。

**恢复推进：** S1 已追加真实 Core Module 28项/View解绑代表用例并完成批次门禁；S2已退役 Core.Editor / Editor.Help，五份额外产品 asmdef/meta全部退出，主体为 Runtime / Editor。S2 Core Editor102、Help25、Provider6、API/示例源码71、原生Inspector5及字典Inspector15项通过，原类型/API/字段/GUID/Help持久身份保持，Player图无Editor主体。S3-S7仍待执行，不把两主体形成等同目录/input/载荷/Player全部完成；详见计划§17.5-17.6。

**S3完成：** Core归位Modules/MVC/Events/UI分区，Module/ViewManager/PanelManager保持唯一类型并拆十个part；两profile词法成员和状态初始化顺序相等。Core Edit61、Module Play28、View/UI Play96、SDK123、真实Analyzer与导出各1项通过，445脚本绑定/GUID、549公开类型及保护hash核验保持准许范围。S4-S7待继续，实际记录见§17.7。

**S4完成：** Kernel全部49源码及LICENSE并回Foundation，Binding/Events/Pool与五个组件按职责归位；125文件及meta字节保持、79移动/17空容器退出/15新目录GUID独立。中立Foundation与Audio双profile源码闭包、Timing/作用域/UnityEvent/LayerMask/动画及导出门禁通过，549公开类型/445绑定和保护hash保持。S5-S7待继续，记录见§17.8。

**S5完成：** 取色三控件归Utilities，Core窗口归Windows/Tools，原namespace/API/序列化字段/GUID/Prefab保持；只新增IUIInputGate只读predicate，Panel显式实现原加载/模态判断。结构与独立阻挡先红后绿，控件16（含双profile无Core源码闭包）、渲染1、正式View/UI Play96及导出1通过。S6-S7待继续，记录见§17.9。

**S6完成：** Resource Editor八脚本只迁路径，Build/Service/CodeGeneration/Data职责清楚，原文/GUID/meta/owner/输出算法保持。Resource门禁、真实生成源码消费者、Help/示例、Zip全源码BCL-only编译和导出通过；文件替换一次波动及原样复跑记录在§17.10，不隐去失败。源码/导航/许可/作用域已同步，S7新Player/载荷/独立审查待执行。

**最新状态：S7硬停止，§16未完成。** 新Windows64/Mono Player实际持久事件/MVC/UI/退出smoke通过且原生exit0，无五退役/Editor主体/自有Analyzer/Sirenix/测试DLL，Temp证据保留；但正式Canvas的遮挡掩码/三新增序列化字段与实施前保护基线不符，原保留数据测试实际0/1。漂移来源未确认，不自动覆盖或更新基线。独立审查还发现较近允许型IUIInputGate可覆盖正式Panel未加载拒绝，临时预览副本已复现，S5门禁需重新打开。审查因额度中断无最终结论，最终rsp/导出收口不计通过；S7自有夹具已按所有权清理，Main/HEAD/暂存区保持。前述各批通过仅为历史定向证据，不能覆盖这两项阻断；记录及恢复前置见§17.11。

**最终状态：用户授权修复后，§16/§16.9技术实施完成。** Canvas已按S0精确恢复并通过原资源4项；Core窗口显式注入原最近活跃Panel，混合门禁真实红绿、控件16/View-UI Play97通过；独立审查完成无P0-P2，仅发现的Help职责分组P3已补红绿并通过Provider7项。SDK123、Editor身份/双profile实际Analyzer见证11、最终导出1项通过，最终Editor/Player的13份代表rsp输入已核对。新Windows64/Mono开发Player进一步实际验证Model/Controller初始化/查询/恰一次退出解绑、View/UI混合门禁/复用、持久UnityEvent和真实退出，原生exit0，0errors/4warnings；产品Runtime DLL与前次已修复Player字节相等。全部自有工程夹具/状态键清理，保护资源/配置保持；随阶段 7 最终 Git 收口提交并推送。§17.11保留历史停止，不作为当前状态；最终证据、未验证平台和五类AQN/旧二进制/Bundle重编译限制见§17.12。阶段8/公开独立分发/人工游戏全流程仍不在本轮交付内。

**2.5 状态：2.5-C 实施与验证完成，A/B 验收提交 `13cb2ce` / `045641b`；2.5 的验收与 Git 收口已随阶段 2 完成（里程碑 `milestone/2.5-serialized-dictionary`），阶段 3、4 亦已于后续完成。** 详见 [2.5 专项第 16 节](../../../.kilo/plans/xf-yl-2.5-serialized-dictionary.md)；其他专题的历史状态不由本批改写。

## 1. 目标与边界

- 最终形成统一的 YLFramework，以 XF Module/MVC 为主要架构基础。XF 为主不等于无条件照搬；YL 是未完成、未正式发布的个人框架，其模块、目录、API 和内部实现均可合理调整、替换或淘汰。
- 融合不等于保留两套并行实现：新 YL 内部每个功能收敛为唯一实现。原 XF 生态作为游戏承载保留，不构成新 YL 的功能实现。具体取舍由后续专项源码研究决定，不为形式统一过度设计。
- 新 YLFramework 通过源码承接和功能融合逐步独立，最终不依赖原 XF 生态程序集：吸收和维护选定源码，不以卸载原 XF、迁移游戏或建立旧 API 兼容层代替融合。源码承接只证明归属与身份建立；功能融合须按专项契约实现和验收；最终独立须在不提供原 15 系统的验证承载中证明直接及间接依赖均已解除。三者不能互相替代。
- 从核心向外围、小批次推进。原 XFGameFramework、XFABManager、XFFSM 等原系统继续留在 CGJ2024 工程内，供原游戏使用；原游戏始终保留原有 XF 业务组织、XFAB 资源/场景/GameObject 加载链及现有 YL 音频接入，不迁移到新 YL。框架融合不主动修改游戏业务，也不以让游戏改用新 YL 为完成标准，最终须保证原游戏仍能正常运行。
- Module/MVC 管业务归属与索引，XFAB 在当前阶段管加载、缓存及相关执行支撑；Module 关闭不自动结束外部 FSM、全局计时或音频。后续方案须明确所有权，不能将缓存归还等同于销毁或资源释放。
- XFAB 在当前过渡阶段可继续提供资源链（含新 YL 的临时后端）；原 XFAB 资源、场景和池等依赖仍是过渡性依赖，其解除归属未来的资源等专题，不在主框架 1.x 批次内自动展开，最终须由 YL 自有能力替代并解除对 XFABManager 程序集的依赖。独立 XF 扩展包暂不融合但纳入长期范围，按其实际依赖逐系统承接；不自动授权扩展包重构，重大范围变更须另行确认。
- 不引入扩展包安装/更新功能，第一版不做 UPM。原 XF 生态与新 YL 在同一工程中共存：新 YL 使用独立 namespace、程序集及资源与 Editor 身份，类型、GUID、资源路径、配置键和运行时自动初始化不得与原包冲突。
- 最终公开 namespace 统一为 `YLFramework` 及其适用子命名空间，仅约束新 YL；原 XF 生态保持原名，不要求阶段 1 完成全部重命名。
- 兼容仅针对可验证的真实依赖（游戏实际行为、持久化数据、Unity 资源引用及必要工程依赖）；不为未使用或未发布的历史 API 建立无依据的兼容负担。原游戏兼容通过保留原框架路径实现，不以迁移游戏为默认目标。整体收口时，新 YL 的 Runtime、Editor、资产与保留工具链不得直接或间接依赖已选长期系统的原程序集（Unity 与经核实的第三方依赖另论），并须在不提供这些原系统的验证承载中通过独立验收。

## 2. 融合范围（主框架 18 项与长期系统）

编号、名称及方向保持固定，不自行增加、合并或重新定义。方向表示主要参考来源与融合重点，不预先决定内部实现；即使标注“XF 为主”，也允许根据源码研究吸收 YL 的更优方案或采用有实际收益的新设计。

| 编号 | 功能 | 融合方向 |
|---|---|---|
| 01 | Module | XF 为主 |
| 02 | Model | XF 为主 |
| 03 | Controller | XF 为主 |
| 04 | View | XF 为主 |
| 05 | Panel/UI | XF 为主 |
| 06 | Audio | 融合双方 |
| 07 | Events | 融合双方 |
| 08 | HTTP | XF 为主 |
| 09 | Timer | 融合双方 |
| 10 | Pool | 融合双方 |
| 11 | Update | 融合双方 |
| 12 | UI Adaptation | XF 为主 |
| 13 | AsyncOperation | XF 为主 |
| 14 | SerializedDictionary | 融合双方 |
| 15 | Tools | 融合双方 |
| 16 | Extensions | 融合双方 |
| 17 | Attributes | XF 为主 |
| 18 | Editor | 融合双方 |

主框架 18 项是当前优先子范围，不替代长期范围。长期融合范围还包括已选定的 XF 生态系统：XFABManager、XFFSM、CharacterController2D、MoveablePlatform、SkillSystem、BuffSystem、Overlap、Localization、MissionSystem、ScriptableObjectSystem、ExcelSystem、ArchiveSystem、ReddotSystem、LoggerSystem；连同主框架共 15 个系统。主框架 18 项与 15 个来源系统是不同层级的范围：18 项是 XFGameFramework 主框架自身内部的功能清单，15 个来源系统（主框架连同上述 14 个生态/扩展系统）是长期范围的系统集合，两者不互相替代，也不合并计数。各系统的接口、程序集与内部融合方案由后续专项研究提出，本表编号不扩展、不重新定义，当前仍优先完成主框架。YL Action（QFramework 来源）已在现有 YL 框架中，不属于新承接对象；后续可结合 Update/Timer/Pool 的专项研究协同整合，具体实现方案尚未确定，不自动启动。

## 3. 总体阶段

以下为默认顺序，不代表专项研究已完成。每阶段均按小批次实施，验收只覆盖实际完成的范围。

| 阶段 | 目标与范围 | 前置依赖 | 完成标准 | 必须避免的跨阶段修改 |
|---|---|---|---|---|
| 1 核心承接 | 01 Module、02 Model、03 Controller、04 View；承接必要的异步、Attributes、UI 与基础设施支撑，公共基础先自有 | 源码归属、程序集边界及可复现基线明确 | MVC 生命周期及框架专项验证通过；原游戏及其 XFAB 链经非干扰检查不受影响；核心源码无重复所有权；剩余过渡依赖有解除条件；阶段门禁按风险执行 | 不全面融合池、事件、音频、UI，不整体改名 |
| 2 公共支撑 | 07 Events、09 Timer、10 Pool、11 Update、14 SerializedDictionary，按实际依赖拆批次 | 核心基线稳定，调用方及所有权明确 | 生命周期、回收、订阅、时钟与序列化专项验证通过，核心回归无新增失败 | 不借基础设施替换重写业务、UI 或资源链 |
| 3 UI 系统 | 05 Panel/UI、12 UI Adaptation | View 及相关事件、驱动、加载边界稳定 | 场景切换、层级、缓存复用、输入、适配及按钮事件通过 | 不顺带替换 XFAB 或重做游戏界面 |
| 4 音频系统 | 06 Audio，客观评估并统一领域能力 | 相关基础设施稳定，当前播放行为与资源所有权已确认 | 专项验证通过；原游戏音频经非干扰检查不受影响 | 不默认保留 YL Audio，不无依据改变游戏效果 |
| 5 运行时外围 | 08 HTTP、13 AsyncOperation、15 Tools、16 Extensions、17 Attributes 的专项融合与收口 | 相关核心及调用方稳定 | 各项专项测试与调用方回归通过，无新增 Editor/Player 边界泄漏 | 不扩展成通用网络平台、工具大全或统一异步框架 |
| 6 Editor 整合 | 18 Editor，整合保留功能对应的编辑器能力 | 相关运行时 API 与资源格式稳定 | 创建、Inspector、帮助、构建/导出等选定入口可用，Editor 隔离正确 | 不引入包安装器，不全面整合第三方插件或独立扩展 |
| 7 框架结构整理 | 7.1 Kernel、7.2 Core 归口；补充方案 A 的 Systems/Utilities、普通 Editor、原生 EventListener/Odin 退出与分发导航收口 | 阶段 6 完成 | 各结构批次及必要迁移验证通过；公开类型、程序集身份与功能契约保持；暂留 Core 对 XFAB 依赖，最终用户验收/Git 收口另行确认 | 不在本阶段解资源依赖、改名或重设计已验收功能 |
| 8 资源系统深度融合 | 以 XFAB 成熟能力体系为主体，吸收 YL Resource、GO 池及 Resource Editor 的有价值成果，形成统一、可维护的资源系统；含架构/性能/可靠性/生命周期优化、API/Editor/工作流改进与有价值的能力扩展 | 阶段 7 通过；先研究 XFAB 与 YL Resource 双方源码 | 阶段 8 专项方案确定的门禁通过；不长期保留两套相互竞争的资源所有权 | 不改原 XFAB Package 与游戏资源链；不预设最终 API、租约模型或资源组含义 |
| 9 最终收口与独立交付 | 公开命名/兼容/资源与非 UPM 分发收口；真实独立载体导入、Editor/Analyzer、Windows64/Mono Player 与依赖许可验证 | 阶段 8 通过 | 新 YL 在无原 15 系统的承载中通过独立验证；原游戏正常运行；分发内容与许可完整 | 不以命名统一为理由重新设计已验收模块 |

13、17 等在早期出现，仅表示承接核心所需支撑，不提前决定最终实现。Editor 支撑可随运行时批次迁移，阶段 6 负责整体收口，并非此前禁止修改 Editor。

长期系统中，XFABManager 由阶段 8 承担；XFFSM 及独立扩展等其他系统的融合在阶段 8、9 之后另行规划，本表不自动授权其启动；阶段 7 结构收口亦不等于完整 YL 收口。

## 4. 长期工程规范

1. 专项研究先于方案：沿真实定义、调用方、程序集及资源引用查证，既有文档只作导航。区分已确认事实、推断与待验证事项，仅补查影响当前决策的证据。
2. 每批只有一个主要变更轴：源码承接、行为修复、公共 API 改动、类型改名、资源格式迁移原则上分批；先明确范围、验收及失败后的恢复边界。
3. 程序集按实际依赖组织：Runtime 不反向依赖 Editor，保持类型和程序集唯一归属，避免旧包与新源码重复编译；目录、namespace、asmdef 不机械一一对应，也不按来源系统一一建立程序集。与多个系统共用的中立基础放入独立基础程序集，不反向依赖业务系统。分阶段解除新 YL 对原系统程序集的过渡依赖，每条过渡依赖须有解除条件与对应专项。复制到新 YL 的内容生成新 GUID，移动已有受管资产保留其自身 GUID；不手改 PackageCache 或 Unity 生成工程文件。
4. 行为与身份分开迁移：先证明行为正确，再收口 API/namespace。重大公共接口、所有权或数据格式变化先确认；兼容出口必须有实际依赖依据和撤除条件。
5. 资源兼容以证据为准：分别检查脚本/asmdef GUID、字段结构、UnityEvent 类型与方法、反射字符串、Resources 路径和 AssetBundle 映射。移动资源保留 `.meta`；保留 GUID 不等于全部兼容，序列化格式变化需单独验证迁移。
6. 验证分层报告：静态检查、Unity 编译、EditMode、PlayMode、原游戏非干扰检查、Player 构建及 Player 运行分别描述，不相互替代。优先复用框架专项测试，仅补关键契约或缺陷验证，按变更风险选择直接调用方及跨模块回归，不默认每批完整游戏回归、全量 Editor/Audio/Foundation 或 Player 验证。首个 Player 目标为 `StandaloneWindows64`；阶段结束执行相应门禁。新 YL 的独立验收另在不提供原 15 系统的承载中执行，同工程编译通过不构成独立证明。
7. 缺陷与改进以证据为准：当前获授权专题内的既有缺陷有复现或可靠源码证据时可以修复；有明确实际收益的优化、升级或必要增强也可以实施，验证深度与风险相称。不借局部改进顺手重构无关模块，不弱化断言制造通过。
8. Git 保护既有工作：默认以 `integration/xf-yl` 为集成主线，实施前检查工作区并保留用户改动。需要子分支时明确用途；提交、合并和推送按授权执行，不自动 commit/push。
9. 本文是唯一长期蓝图：后续仅增量更新确认的架构决策、阶段状态及必要基线信息。专项正式方案各保留一份并从本文链接，不生成重复方案、过程日志、日报或审计记录；区分已批准目标、实际实现、验证报告、用户验收和 Git 收口。
10. 融合质量与技术演进：本工程不是简单复制、改名或机械兼容 XF。“XF 为主”不等于原样继承其全部设计，YL 也不是被动兼容方。Kilo 应在专项研究中客观比较 XF 与 YL 的真实源码、依赖及使用场景：成熟合理的实现可以保留；有证据的缺陷可以在 YL 自有源码中修复；对正确性、稳定性、性能、易用性、扩展性或长期维护确有收益的优化、升级及必要增强，可以在获授权批次内自主实施；YL 方案更合理时可以借鉴或优先采用，双方均不合适时也可提出新实现。具体 API、算法与内部结构由 Kilo 根据源码研究决定，不由总体规划预先指定。改进应与当前阶段目标、依赖和风险相称，并进行必要的针对性验证；重大架构取舍、跨阶段扩张及高风险兼容变更须先说明并取得确认。不为形式统一、追求新技术或未经证实的收益做无意义重构。原 XF/XFAB 等 Package 严格只读，所有融合、修复和升级只在 YL 自有源码中进行，不擅自改变原游戏路径与行为。所有后续 XF 系统融合，都应评估优势融合、缺陷修复、质量优化和有价值的能力扩展，以实际收益提升最终 YLFramework 的整体水平；“融合优先、重构其次、重写最后”不禁止合理创新和必要重新设计，也不要求每个成熟模块都必须重构或新增功能。必要测试按实际风险做到充分，复用未受影响的有效证据，不机械重复测试或按数量扩测。

## 5. 可复现基线与阶段衔接

- 阶段 0 参考源码基线为 `integration/xf-yl` 的 `d673af8c692b4c769000d8cd26b8d2f2709328c7`，Unity 为 `2022.3.62f3c1`。这是研究基线，不代表编译、测试或 Player 已通过；后续每阶段关联新的明确源码状态及实际验证结果。
- 可复现基线必须包含源码及资源 `.meta`、ProjectSettings、`Packages/manifest.json` 与 `packages-lock.json`、实际解析包的来源/版本/Git revision、必要插件和构建工具。PackageCache 是忽略的本地缓存，不能当成源码归档；声明、锁文件与实际包不一致时先核验，不擅自升级或重写依赖。
- XFAB 构建输入包括 `Assets/Games/MainModule/MainModule.asset`、相关源资源及 `Assets/Resources/XFABManagerSettings.asset`。阶段基线须明确平台、资源版本、加载模式、构建/复制设置及依赖，确保可重建，或获得可核验且与源码状态匹配的产物。
- 新 YL 的独立验证使用自有 fixture；`MainModule` 全量产物不作为所有核心批次的前置条件。新 YL 对原 XFAB 资产类型与格式的依赖属过渡项，资源能力自有化后须转换或重建，最终资产 GUID、序列化类型、反射与加载路径不得回指原系统。
- 当前 `.gitignore` 排除 `/AssetBundles/`、`/Assets/StreamingAssets/MainModule/`、`/Assets/StreamingAssets/AssetBundles/` 及相关目录 meta，也排除 Library、Build/Builds 等生成内容。XFAB 的 Bundle、manifest、`AssetBundleNameMapping.txt`、`bundle_list.txt`、`bundles_suffix_info`、`project_build_info.json` 不随当前 Git checkout 恢复。Git 干净不等于产物齐全或游戏可运行。
- 资源验收前核对 `MainModule/StandaloneWindows64` 的配套清单、版本、完整性和加载路径；重建或使用产物快照时保留可核验的来源及 hash 信息，不混用旧 Bundle、新清单或不同平台产物。不依赖未说明的本地运行缓存。
- 被忽略的本地设置、私有下载源或密钥等必要输入须说明安全获取/恢复方式，不将秘密写入本文或提交。DOTween 等可选插件及平台构建模块须满足相关批次的实际依赖；不因本机已安装而省略恢复条件。Sirenix Odin 已退出，不再作为恢复输入；未使用的五个 Odin 宏按 ProjectSettings 权限边界保留，不代表插件仍安装。
- 未经确认不修改忽略规则，不强制提交被忽略的构建产物。优先利用现有构建和制品保留机制，不为基线新增无必要的报告系统。
- 必须先完成本批依赖边界、源码唯一归属、必要兼容分析及可执行验收；非阻塞改名、未使用 API 清理、外围优化和纯形式统一允许延期。
- 阶段结束建立稳定基线，关联明确提交/源码状态、依赖与资源产物状态及实际验证结果；未获提交授权时不得虚称已有提交基线。门禁失败或未验证不算阶段通过。
- 新证据表明依赖、资源风险或验收阻塞时，可提出调整顺序或前置小批次，说明原因、影响及验收变化；不自动扩大已授权范围。长期系统范围已确认，但具体承接须经专项授权后启动。外部阻塞先定位，再申请必要授权。

## 6. 阶段 1 启动与任务边界

### 当前状态与方案入口

| 范围 | 当前有效状态 | 依据与边界 |
|---|---|---|
| 1.1-A～G | 全部验收完成，Git 集成收口完成 | 用户反馈；集成提交 `b10f7b1`。完成的是源码承接与共存基础及承接门禁，不代表主框架 18 项功能融合或 15 系统最终独立 |
| 1.2-A | 实施、验收、提交完成 | 用户反馈；提交 `8ff1ea6`，MVC 本地查询与显式跨模块共享已落地 |
| 1.2-B | 实施、审阅通过、提交完成 | Model/Controller 注册完整性、单一 owner、Model ID、初始化与移除安全已落地；用户反馈及 B 完成报告；报告专项测试 35/35 通过；实际提交 `e39999e` |
| 1.2-C | 实施、验收、提交完成 | Module 唯一注册、状态与启动去重、SingletonModule 身份及取消隐式池化已落地；用户验收反馈；实际提交 `60d0465`；此前 C 报告记录 Unity 编译、Core PlayMode 35 项及 Foundation 最小回归 4 项通过。C 当时的失败回滚限制已由 E 处理 |
| 1.2-D | 已实施、通过专项验证并提交（`8c3fff6`），用户验收状态未据本轮推定 | Module 唯一关闭管线、OnClose → Controller → View → 剩余 Model、逐对象异常继续清理，Closed/注销后汇总异常；显式 BaseModule Shutdown。Unity `2022.3.62f3c1` 实际编译无失败，Core PlayMode 55/55、Core EditMode 12/12、Core Editor 80/80 通过；新增 11 项 D 回归并更新相关旧断言，启动失败自动回滚未纳入 |
| 1.2-E | 已实施、通过专项验证并提交（`10f7a79`） | 启动失败复用 D 关闭管线，含 BaseModule；根因实例及额外清理错误保留，注销/Closed/移除记录后通知；ProjectName 在资源加载前捕获；配表/JSON/场景已报告失败传播；专用启动表复制后归还。E 历史验证：Unity `2022.3.62f3c1` 实际编译无失败；启动/MVC PlayMode 27/27、失败路径 13/13、XFAB Assets/场景 12/12、直接 View 关闭回归 10/10，共 62 项通过。未运行全量 Editor/Foundation/Audio/Player；真实 AB 物理卸载未验证 |
| 1.2-F | 已完成契约收口及技术验收，1.2 已合并 | 用户本次明确确认 1.2 完成；Git 已核对 F 提交 `5105c46`、1.2 合并 `30de1fc`。F 历史验证为核心 PlayMode 63/63、直接生成器 EditMode 33/33；E 的专用配表前置条件与后端限制保留 |
| 1.3-A | 已验收并提交（`22b281d`） | 用户确认及 B 开始时实际 Git 核对；Module 本地 View 索引及会话身份、可靠退出、受控 Model 绑定与注册身份、最小必要 Panel 释放；D1 已落实，Close 不隐式移除 Model。A 历史专项 PlayMode 77项通过 |
| 1.3-B | 已验收并提交（`a1beb2e`） | 用户确认及 C 开始时实际 Git 核对；同步 View 统一 Prefab/实际实例类型校验及事务、LoadViewArgs 接管/只读借用/即时清引用、Loaded 事件钩子及订阅轮次隔离、健康缓存和污染实例同帧隔离。B 历史验证为 PlayMode 89/89、参数 EditMode 10/10；详见 1.3 方案第11节 |
| 1.3-C | 已验收并提交（`bee3e28`） | 用户确认及 E 本轮实际 Git 核对；显式挂载、inactive/嵌套、取得根与场景对象边界、销毁/卸载/迁移及默认Scene在OnStart前挂载。C 历史专项 PlayMode 101/101；详见 1.3 方案第12节 |
| 1.3-D | 已验收并提交（`57d3595`） | 用户确认及 E 本轮实际 Git 核对；pending/owner代次、参数收尾、稳定终态、逻辑取消/失效与迟到隔离，绕开 Assets-null GameObject pool 路径，不物理取消共享后端。D 历史专项 PlayMode 110/110；详见 1.3 方案第13节 |
| 1.3-E / 1.3 收口 | COMPLETE；A～E 全部完成、技术验收通过，已合并至 `integration/xf-yl`（`f206e7b`）；E 提交 `e1be57f` | 用户最终确认及本次实际 Git 核对。Panel必要上下文/Canvas/order复用View事务，队列按Loaded+active接入，内部退出释放，请求直接转接View终态与Abort；E实际验证为Unity编译无失败、PlayMode120/120、身份EditMode7/7。A～E核心契约一致；详见1.3方案第14节，不等于完整UI融合、原游戏/Player或最终独立验收 |
| 1.4 / 阶段 1 | COMPLETE（1.1～1.4 全部验收完成）；已合并集成主线（`6cea527`），提交 `fd7b1e8` | 2026-10-09 用户授权及最终验收；沿用本轮 37 项 Core PlayMode、7 项身份 EditMode、原游戏 Editor 自动化及实际共存验证。用户恢复产物后，Windows64/Mono 游戏构建成功且完整流程经用户确认；独立 Core Player 定向 4/4 通过，临时设置完全恢复。临时 XFAB 依赖、真实 AB 释放、跨平台与最终独立运行仍属后续事项。首次阻断、恢复证据及保留风险见第 7 节 |
| 2.1 Pool | COMPLETE；A～E 验收及 Git 集成完成 | A～D 提交：`a97cb2c` / `e8925e6` / `2529d5a` / `455088c`；E `71fc00d`，集成 `5754f3a`。唯一普通 ObjectPool/集合内核及领域消费者接入保持；E 保留 YL string/FIFO/预热/层级与场景能力，吸收 XFAB 稳定原桶/先摘后毁思想，完善归属、转换态、初始化、容量/fake-null、清理/晚还与 Data 原 owner。E 历史 Unity 编译无失败、PlayMode 13/13、EditMode 3/3；本轮未重跑。详见 [专项第 17 节](../../../.kilo/plans/xf-yl-2.1-pool.md)，最终独立验收仍待后续 |
| 2.2 Events | **COMPLETE**（用户确认验收及 Git 收口，E `5674cf3`） | A～D 提交 `0c11270` / `c9cc2a5` / `31b4928` / `cc768a9` / `5a4ac6e`；E 完成 `XFGameFrameworkEvents`→`CoreEvents`（文件/meta/GUID 保留）、冗余 API/算法核查与真实关闭 Domain Reload 两轮 Play 门禁，代表性跨模块 87 项回归通过（CoreDirectView 49、EventsLifecycle 14、CoreEvents 7、Binding 15、Action/Audio 各 1）。Foundation 共享 channel/bus、取消与唯一 Or 承接全部实例/命名/类型/动态与 Unity 适配，Core `EventManager` 为 Default 薄入口，四个历史 facade 各持独立上下文并保留各自清理范围；历史命名、旧 API 与重复实现已收口。见 [专项第 14 节](../../../.kilo/plans/xf-yl-2.2-events.md) |
| 2.3 Update | **COMPLETE**；用户已确认验收，Git 集成 `c9e6024` | A `d9f1b6c`、B `88cd76f`、C `0b51a8a`。单一 Foundation 三相位、Core 精确订阅、Starter 原 owner/会话隔离及 Action owned 消息桥接完成；历史 Unity 编译、定向 EditMode 13/13、PlayMode 45/45、真实关闭 Domain Reload 连续两轮 Play 通过，原游戏代表性回归通过，Editor 配置/场景恢复。见第 8 节及 [专项方案](../../../.kilo/plans/xf-yl-2.3-update.md)，第 8 节原停止文字保留为历史 |
| 2.4 Timer | **COMPLETE**；用户已确认验收，Git 集成 `09e3fe0`（`milestone/2.4-timer`） | A～C 已验收提交，Foundation 唯一秒/帧内核、默认驱动及互补能力完成；D 实际 Unity 编译通过，连续两轮真实 Play（无 Domain Reload、正常 Scene Reload）1 项、Foundation 边界 5 项、View/Scope 与 Timer/Action 协作各 1 项通过。连续 Play 的原 XF Logger 测试环境隔离经用户授权，不代表整框架或原 Logger 会话兼容；未执行 Player/IL2CPP 或性能测量。见 [Timer 专项第 12 节](../../../.kilo/plans/xf-yl-2.4-timer.md) |
| 2.5 SerializedDictionary | **COMPLETE**；用户已确认验收，Git 集成 `d7e31a6`（`milestone/2.5-serialized-dictionary`） | A/B 已验收提交 `13cb2ce` / `045641b`；唯一 Foundation 容器、Core/Save 旧类型退出、原生 Inspector/raw 诊断与修复、Undo/Prefab/多对象保护、真实 SaveManager 隔离磁盘往返完成。C 实际 Unity 编译与 48 项定向用例通过；原包/游戏不迁移，保留对象键/Unity inline/复杂 JSON 与外部历史格式边界。见 [专项第 16 节](../../../.kilo/plans/xf-yl-2.5-serialized-dictionary.md)及 [Save 使用说明](Modules/Save.md)；阶段 2 Git 集成与里程碑已另行完成，最终独立验收仍属后续 |
| 阶段 3 UI（05 Panel/UI + 12 UI Adaptation） | **COMPLETE**；用户已确认验收，集成 `7e44d9f`，里程碑 `milestone/3-ui` | 3.1～3.3 已验收提交 `5be9c3f` / `7cc933f` / `ac23da8`；3.4 正式资产/来源分发核查、真实无 Domain Reload 双 Play、原游戏菜单/设置/进入/返回非干扰及 Windows64/Mono Player 通过。唯一 Core Module/View/Panel 策略、Tips/ColorSelector 与适配/输入统一，旧 UI 退出；原游戏/包/资源链不迁移。原 Logger 连续 Play 干扰仅经本次用户授权临时隔离并恢复，不表示整框架无 Domain Reload 兼容。见 [UI 专项第 13 节](../../../.kilo/plans/xf-yl-3-ui.md#13-34-实施与阶段门禁收口2026-10-11) |
| 阶段 4 Audio（06 Audio） | **COMPLETE**；用户已确认验收，集成 `0a0e74f`，里程碑 `milestone/4-audio` | 4.1～4.4 已用户验收及 Git 收口，4.5 基线 `fusion/4-audio` / `e25d87c`，正式独立授权。唯一 Runtime Audio、Core 旧副本退出、组件身份及许可/分发边界保持；真实同域双 Play、原游戏 Main/XFAB 代表性音频流程、Source/DSP 输出、最终 Windows64/Mono Development 构建与真实窗口运行均通过；系统实际出声与用户人工听感已确认。原 Logger 仅经本轮临时原生日志隔离并恢复。见 [Audio 专项第 9 节](../../../.kilo/plans/xf-yl-4-audio.md#9-45-综合验收与待确认门禁2026-10-12) |
| 阶段 5 运行时外围（5.1～5.5） | **COMPLETE**；用户已确认技术验收，合并提交位于 `integration/xf-yl`（5.5 提交 `16e283a`），里程碑 `milestone/5-runtime` | 5.1 中立支撑归口、5.2 HTTP 请求生命周期与异步边界、5.3 工具/扩展契约、5.3.5 成熟 Tools 整合、5.4 正式外围接口整合与旧机制退出、5.5 综合验收全部完成并 Git 收口。HTTP / StringTools / 通用 Extensions 归 Runtime，类型查找 / LayerMask / 展示元数据 / 最小等待通知归 Foundation，View/Panel / Audio 保持各自 owner / 取消 / 租约；旧 Core HTTP/Tools/Extensions 与 LogUtil 退出、无转发壳；FSM / Grid / JSONObject / Table / 独立 ZipTool 定位、依赖与许可不变。5.5 真实同域双 Play、Windows64/Mono Development 构建及实际 HTTP 回环 / 退出、原游戏 Editor / Player、真实导出与许可核查通过；原 Logger 隔离仅限本轮授权受控窗口并已恢复。结果与限制见唯一 [运行时外围方案](../../../.kilo/plans/xf-yl-5-runtime.md) |
| 阶段 6 Editor | **COMPLETE**；用户已确认 6.1～6.5 技术验收，合并于 `integration/xf-yl`，里程碑 `milestone/6-editor` | 6.1 Core Editor、6.2 Resource、6.3 生成器、6.4 七项自有 YL 分析器（YL0001～YL0007，默认 Warning）与 6.5 Help/导出/Player 收口全部完成并 Git 收口。6.5 交付 Core 与 Foundation/Tools Help、真实 UnityPackage 解包（752 记录、Plugins 全递归）、Windows64/Mono Development Player 构建与启动、及自有 Editor/Analyzer/Roslyn 运行引用隔离门禁；三套 Editor 程序集与原依赖边界保持。第三方公开再分发许可、外部依赖、IL2CPP/其他平台及阶段 9 独立承载仍属保留限制。实际交付见 [阶段 6 总体方案](../../../.kilo/plans/xf-yl-6-editor.md#65-帮助导出与阶段收口) |
| 阶段 7 框架结构整理 | **COMPLETE**；2026-10-14 Git 收口（`milestone/7-structure`）；7.1（`0f9876f`）、7.2 及 7.R/7.O1 补充、§16 两主体收口完成 | Foundation/Kernel 与 Runtime/Core、Editor/Core 归口完成；97 份 meta、公开身份及编译图保持，默认资源实载、Analyzer 七项实际诊断及作用域、必要门禁与真实导出通过，详见 [阶段 7 方案第 8 节](../../../.kilo/plans/xf-yl-7-consolidation.md#8-72-实施与必要验证2026-10-13)。最终主体为 `YLFramework.Runtime`、`YLFramework.Editor` 两个程序集，ColorSelector 样例退役；暂留 Core 对 XFAB 依赖，不改资源生命周期或 ProjectName；完整限制见 [结构方案 §17.12](../../../.kilo/plans/xf-yl-7-structure-redesign.md#1712-授权修复与最终验收2026-10-14) |
| 阶段 8/9 及长期系统 | 未启动 | 阶段 8 以 XFAB 为主体深度融合资源系统，阶段 9 最终收口与独立交付；XFFSM 及独立扩展等其他系统在阶段 8、9 之后另行规划。IL2CPP/AOT、移动 / WebGL、真实 AB 物理卸载、无原 15 系统独立承载及第三方公开再分发许可仍未完成，不因阶段 6 技术门禁通过而视为已解决 |

状态依据为批次报告、用户验收反馈及实际 Git 历史。以下 F 记录保留为历史验证，不覆盖上表已确认的 1.2 合并和 1.3-A 当前状态。D 验证来自提交 `8c3fff6`，补充回归为 `750b32e`；E 为 `10f7a79`，F 为 `5105c46`。测试工具的总发现数量不是实际执行数量，本文仅统计实际通过用例。完整游戏回归或 Player 构建未在 D/E/F/A 执行，不以专项通过替代 1.4 门禁。

F 本轮实际验证：Unity `2022.3.62f3c1` 导入并加载新增回归，`scriptCompilationFailed=false`、不在编译中；`CoreModulePlayModeTests` 28/28、`CoreFailurePathPlayModeTests` 13/13、`CoreDirectViewPlayModeTests` 10/10、`CoreXfabPlayModeTests` 12/12，共 63 项 PlayMode 通过；`CoreModuleGenerationTests` 33/33 EditMode 通过。生成器验证覆盖生成文本及 asmdef，模板行为未修改，不将其表述为单独生成程序集的 Unity 编译验证。LOCAL + Assets fixture 实际断言加载模式，覆盖真实 JSON、命名 View/Panel、专用启动表归还及 Single/Additive 场景闭环。新增 fixture 回归先复现原兜底错误，再通过修正后的套件；未新增程序集或资源，既有 `.meta`/GUID 与引用边界不变，`git diff --check` 通过。未机械重跑全量 Core Editor、Foundation、Audio 或 Player，未受影响的历史结果仅作历史证据。

本轮退出 PlayMode 时观察到 `LoggerManager` 场景关闭残留错误，修改前首次尝试运行时已出现；原 LoggerSystem Package 有自动初始化及日志访问时创建对象的路径，新 YL Core 不定义该类型。未修改原 Package、游戏业务或增加日志忽略，不将本轮编译和专项通过解释为全工程 Console 无错误；该外部退出问题留待原系统/1.4 非干扰检查定向处理。

- [1.1 源码归属与承接方案](../../../.kilo/plans/1791455404620-xf-yl-core-ownership.md)：保留原设计与研究基线，其历史“当前源码”描述不覆盖本节较新的实施状态。
- [1.2 Module/MVC 正式契约与 A～F 边界](../../../.kilo/plans/xf-yl-1.2-module-mvc.md)：唯一正式方案，分列目标、已实现内容和未实施边界，包含初始化重入口径差异及依据。
- [1.3 View 正式设计与实施记录](../../../.kilo/plans/xf-yl-1.3-view.md)：第10～13节记录 A～D 实际实施；第14节记录 E 的必要 Panel 适配、A～E 最终契约、公开 API、实际专项验证和限制，第14.5节记录用户最终验收及 Git 收口。阶段 1.3 已 COMPLETE，E 提交 `e1be57f`、集成合并 `f206e7b`；当前分支 `integration/xf-yl` 已实际核对。
- E 本轮实际执行 CoreDirectView 46/46、CoreXfab LOCAL + Assets 33/33、CoreModule 28/28、CoreFailurePath 13/13，合计120项PlayMode；CoreIdentity 7/7 EditMode。Unity `2022.3.62f3c1` 最终无脚本编译失败，Module/BaseModule/trackedViewOwners均零；新回归实际复现并修正inactive入队、旧会话排序及队列回调/事件顺序问题，最终只读复核无新增重要发现。无新增资源、meta或asmdef，不改原Package、游戏及旧YL UI资产；未执行全量Editor/Foundation/Audio、原游戏或Player。既有Logger退出残留仍观察到，留1.4定向定位。
- 1.2 已完成合并；1.3 的 A～E 已全部完成并通过技术验收，Git 集成收口完成；1.4 亦已完成用户验收与 Git 收口（提交 `fd7b1e8`，合并 `6cea527`），原游戏非干扰与代表性 Player 验证通过，但真实 AB 释放、跨平台及最终独立验收仍未完成。本次仅更新文档状态，沿用既有实际验证结果，没有新增测试或 Git 写入操作。

### 阶段门禁与后续边界

启动条件：蓝图已获确认；实施前重新核查工作区、依赖与 XFAB 产物；明确主框架源码承接方式和必要工程引用，建立编辑器编译、框架最小验证及原游戏非干扰（含按风险的 Windows64 Player）的真实状态，各项分别报告。阶段 0 已发现的依赖声明/缓存差异、游戏依赖的 Overlap Runtime 对 Editor 的引用风险须核验，不预设修复结论，不扩展成独立包全面研究。

Player 基线问题必须分类，不能笼统归为融合失败或视为通过：

| 类别 | 判断与处置 |
|---|---|
| 既有问题 | 在明确的融合前源码及依赖/产物条件下已存在；记录触发条件与影响。与核心直接相关者可经确认后小批次修复并验证；独立扩展或无关问题另行确认 |
| 新增回归 | 在可比较的前后条件下由当前变更引入；本批解决并通过针对性及必要回归验证，不能以既有问题为由掩盖 |
| 环境阻塞 | 构建模块、包获取、插件授权、XFAB 产物缺失或环境不一致导致无法验证；先恢复可复现条件再判定，不据此虚称代码通过或必然失败，不擅自升级依赖 |

证据不足时标为待定位，不强行分类。阻塞期间可推进不依赖该前提的分析，但不能绕过门禁、宣称阶段已验收。

1. **1.1 归属与边界承接**：核心源码及必要公共基础进入可维护位置，新 YL 使用独立身份并与原 XF 生态隔离共享资源、配置与 Editor 入口；剩余过渡依赖有解除条件；验收无重复类型、引用断裂或新增编译问题。
2. **1.2 Module/Model/Controller**：明确并验证初始化、查询、启动失败、退出异常、关闭与重启；修复仅限确认且直接相关的缺陷。
3. **1.3 View 与 UI 最小闭环**：验证同步/异步加载、Enable/Disable、缓存复用、场景切换与事件解绑，保持 XFAB 所有权。
4. **1.4 核心阶段验收与原始游戏非干扰检查**：验证核心闭环；对原游戏执行非干扰检查（启动、菜单、设置、进入游戏、退出/再进入、音频），确认其走原 XF 与现有 YL 路径仍正常；Windows64 Player 检查按风险安排，不逐批强制。

阶段 1 不承担全部命名统一或重复功能取舍，不融合独立扩展或其他长期系统；具体类的融合方案由后续专项研究提出。

当前保留的边界：1.2-E 已使已注册启动失败实例（含基础模块）在逻辑回滚/注销/Closed 后才报告失败；1.2-D 的异常继续清理及唯一管线保持。1.3-A～E 保持 OnClose → Controller → View → 剩余 Model，View/Panel.Close 只结束表现/绑定，不自动移除 Model。同步/异步加载统一使用归属、索引、初始化及退出事务，参数一次接管与归还；场景/层级显式纳管、存活Scene/DDOL迁移不改owner，取得根与普通子/调用方对象区别保持。pending代次、逻辑取消与迟到隔离已实现；Panel上下文和order在OnLoaded前准备，队列在Loaded+active阶段提交，直接退出不依赖广播，健康缓存Canvas复用，旧排序/事件轮次不误改新会话。完整旧YL UI导航/遮罩/输入/层级策略仍属阶段3。默认启动不承诺任意同目标场景并发的后端身份关联；Closed 不承诺所有外部资源释放成功，Destroy不等于XFAB登记或资产即时释放，不保证缓存跨场景保活或自动重置业务字段。原 XF、游戏业务、旧 YL UI 资产、现有 YL Audio 及普通 IOC/Singleton/Root 未因本批改变。

1.2-E 的启动配表资源前置条件经用户明确确认，F 沿用：启动配表及其 Bundle 为启动过程专用，取文本期间不得有其他调用者持有或并发加载。实际 XFAB 缓存不提供按调用计数的租约，不为任意共享配表提供自动释放保证。归还调用在成功、空数组及失败路径经受控缓存条目验证，正常 Assets 加载的 UnloadAsset 通常不执行物理释放；真实 AB 物理卸载及在途共享未验证。1.3-D/E仅绕开Core命名View/Panel的Assets-null池挂起路径；底层协程异常/永久不完成和场景门闩等仍未修改，不宣称物理取消或全面资源释放。有效上下文中请求仍可能等待，但Core Abort、Module关闭或目标失效能逻辑终结且不迟到提交。默认 Single 场景成功后的逻辑回滚不恢复旧场景；Additive/Single 合法路径已做 fixture 回归，游戏 Start 场景未修改。

## 7. 1.4 核心集成与非干扰验收

本节按先后保留首次验证与资源恢复后的补验证据；当前结论以最后一节为准，覆盖前文历史记录中“1.4 未开始”的口径，不改写 1.2/1.3 当时的验证范围。

### 首次验证基线与范围

2026-10-09 实际核对分支 `fusion/1.4-core-validation`，HEAD `db4746e`，开始时工作区干净。Unity 为 `2022.3.62f3c1`，Windows64 模块可用，当前 Standalone 后端为 Mono，正常 Domain Reload。`Assets/Games`、`Assets/Scenes`、`Packages`、`ProjectSettings` 相对融合前研究基线 `d673af8` 无受管差异。本轮只更新本文，不修改产品代码、测试、资源身份或配置；未执行 Git 写入、XFAB 资源重建、全局卸载或缓存清理。

| 本轮实际执行 | 结果与证据边界 |
|---|---|
| Unity Editor 编译状态 | 验证前及最终均 `isCompiling=false`、`scriptCompilationFailed=false`；以下测试实际使用当前源码。临时诊断脚本自身的 Roslyn 编译/时序错误不算项目编译错误，也不宣称 Console 全无错误 |
| Core LOCAL + Assets 集成 | `CoreXfabPlayModeTests` 33/33；覆盖 JSON/专用配表归还、启动与失败回滚、命名同步/异步 View/Panel、绑定、缓存、取消/迟到隔离、Module 关闭后同实例重启、Single/Additive 场景及卸载 |
| 直接生命周期代表性补验 | 4/4：`StartupAndCloseOrderClearAllMvcIndexesBeforeRestart`、`ShutdownClosesOrdinaryThenBaseInReverseRegistrationOrderAndContinuesAfterErrors`、`SharedModelBindingsDoNotOwnModelAndOldRegistrationCannotRevive`、`LoadedHierarchyIncludesInactiveChildrenAndOnlyRootReturnsToBackend`；其余 1.2/1.3 专项沿用第 6 节历史证据，未全量重跑 |
| 程序集身份 EditMode | `CoreIdentityTests` 7/7；Core 直接依赖仍为 Foundation、临时 XFAB、Unity UI，无新增原 XF/整体旧 YL Runtime 引用。Player 程序集列表也核实这三项直接引用 |
| 原游戏现有流程 PlayMode | `YLAudioGameFlowPlayModeTests.MainStartSettingsGameExitAndReentryUseFrameworkAudio` 1/1；真实 `Main.Start` → XFAB `ReadyRes` → 原 XF `MainModule` → XFFSM，启动/参数设置/进入/返回/再入/结束及音乐句柄清理通过 |
| 原游戏真实按钮与音频补验 | 独立普通 Play 会话从 `Main` 启动；调用现有持久 Button/Slider 事件验证音量设置及关闭、开始/进入、暂停返回菜单、再入、结束后重开。音乐 `03`/`02`、点击音及控制器加载的 `机炮4` 均观察到实际播放 AudioSource，音量跟随 Slider；重开后活动 GamePanel 与循环 BGM 各一份。未以此代替听感、完整战斗输入或全游戏内容验收；音乐/音效及旧音乐偏好键按进入前快照恢复 |
| 新旧同时运行 | 原游戏暂停时，在同一会话启动自有 smoke Module，加载 JSON Model、同步 View 并显式绑定 Model、异步 Panel，再正常 Close。Core 回到 Closed/零注册，View/Model owner 与绑定结束；原 XF MainModule 为同一实例、原音乐句柄有效，Game 场景和 `timeScale=0` 保持。之后原游戏真实暂停菜单按钮、再次进入及重开成功。临时内存诊断不新增资产/Fixture，不清非法 Core 索引制造通过 |

本轮自动化实际执行 **37 项 Core PlayMode、1 项原游戏 PlayMode、7 项 EditMode**，无失败或跳过；工具的全工程发现数量不计入执行数量。没有新增测试设施，也未机械重跑全量 Core/Editor/Foundation/Audio。当前 Core 集成与 Editor 代表性原游戏非干扰成立，但不等于 Player 运行或最终独立验收。

### Logger 定向归因

两次空 `YLCoreSmokeHarness` 普通 Play 对照均复现：原游戏未启动，已采样的 Core Module/BaseModule/View/Foundation 协程宿主均为零，原 Logger 自动创建为一份。第二次对照中 `Editor.log` 的场景清理错误累计计数在进入前为 18，**仍在 PlayMode 时已变为 19，真正退出后仍为 19**；最终 EditMode 中 Logger 对象为零、场景未脏。

本轮可确认的触发链是 `MCP Startup.OnBeforeAssemblyReload/TryDisconnectAndCleanup` 日志 → 原 `LoggerHandler.LogFormat` → `LoggerManager.FilterLogType/Instance/Init`。原 `LoggerManager.cs:105–113` 用 `isPlayingOrWillChangePlaymode` 允许进入过渡期创建；此时 Unity 尚不允许 `DontDestroyOnLoad`，先创建的 GameObject 留在异常路径，且 `_instance` 赋值尚未执行。日志确实报告该调用的 `InvalidOperationException`，随后场景清理列出三份 `LoggerManager`。因此历史笼统的“退出残留”不能一概按真正退出时创建解释；**本轮归因到原 Logger 的 PlayMode/Domain Reload 边界缺陷与 MCP 清理日志触发，不是新 Core Module/View 生命周期回归**。

原 Package 的 OnDestroy 不恢复全局 handler、不设置退出屏障、关闭静态 writer 后不置空，仍存在另一条销毁后访问重建的源码风险；未将其冒充上述现场的唯一原因。未逐条重现历史所有残留，也未验证独立 Player 正常退出的 Logger 文件/委托清理。当前已执行 Core 与原游戏 Editor 功能未被该错误阻断，但工具日志/切换会被影响；保留为原系统问题，不修改 LoggerSystem Package、不加日志忽略。

### Windows64 Player 与阻断条件

实际使用现有 Unity、当前 Build Settings 的 `Assets/Scenes/Main.unity` 和既有 StreamingAssets 执行 Development Windows64 Player 构建，没有调用 XFAB 构建或复制流程。最终 `BuildReport` 为 **Succeeded，0 errors、4 warnings**；四项为 MCP 空 Runtime 程序集警告。可用诊断入口为 `C:/Users/Kano/AppData/Local/Temp/kilo/CGJ2024-1.4-20261009-104421/CGJ2024.exe`，同目录 `player-validation.log` 保存实际启动结果。MCP 主线程调度在同步构建期间发生回调重入，一次请求意外产生多份临时输出且工具超时；后续通过实际 BuildReport/日志确认结果，未把超时算作构建失败。MCP 的 `packages-merged-link` 构建中间资产已由其 postprocess 正常移除，工作区无此残留；未修补工具 Package。

既有 Overlap Runtime 对 Editor-only asmdef 的引用及 Overlap/Logger 未保护的 UnityEditor 导入仍有静态隔离风险，但本机实际 Player 编译成功，Player 程序集没有 `UNITY_EDITOR` defines，Overlap 的实际 Player 引用列表也不含 XF Editor。**不能将静态风险写成本轮已发生的编译阻断**。依赖恢复仍不完全可复现：manifest/lock 的 URP 16.0.6、UGUI 2.0.0 与注册包实际 URP 14.0.12、UGUI 1.0.0 不同；Cinemachine/Timeline 声明也不同于解析版本。未更改依赖，本机结果不外推为干净环境通过。

现有 MainModule Windows64 资源清单为 **1.0.0，update_date 2024/07/10**；112 个 Bundle 大小/MD5 全部符合 `project_build_info.json`，且与 `AssetBundles/MainModule/1.0.0/StandaloneWindows64` 对应文件及六项辅助清单逐项一致。完整性不等于匹配当前源码；该构建信息文件 SHA-256（Base64）为 `kNMx3/RVMRMvSvzqXdqPWVJr8RimcOXi46pF96/TZ/Q=`。

实际运行上述 Player，已初始化 Unity/D3D11/Mono 并进入原 XFAB/FSM 菜单加载链，但出现 `The referenced script (StartPanel) on this Behaviour is missing!`、旧 `XFGameFramework.AudioPlayer` 的 `未查询到AudioController:Music` 及 `view:StartPanel加载失败,未查询到View组件!`。当前 Editor 源 Prefab 使用现有 YL Audio，而 Player 从旧 Bundle 实例化旧 XF AudioPlayer，**已确认存在资源产物/当前脚本与音频契约不匹配**；missing script 的具体旧程序集/序列化身份仍待核验，不推定为 Core 类型冲突或自动裁剪缺陷。Player 已停止，未继续将设置、进入/退出/再进入、音频或新 Core 运行记为通过。

必要恢复条件：取得可核验且匹配当前游戏程序集、脚本身份及现有 YL Audio 的 MainModule Windows64 产物，确认声明/锁文件/实际解析依赖的恢复口径，再补 Player 原游戏流程及相应核心运行验证。若只能重建/复制 MainModule，将影响全局 XFAB 输出与 StreamingAssets，**须先由用户确认**；本轮没有擅自执行。Logger 原包处置若需修改来源/依赖，同样另行授权，不借验收融合外围系统。

**首次验证结论（历史）：必要 Player 运行门禁失败，当时阶段 1 不宣布 COMPLETE。**该批仅更新本文，原 XF、原游戏、既有 YL Audio、Packages/ProjectSettings 和资源配置未由代理修改；不得将以下用户资源恢复改动倒算为该批融合修改。

### 资源恢复后的补验与当前结论

用户完成 MainModule Windows64 产物恢复并授权继续。补验开始时，分支/HEAD 仍为 `fusion/1.4-core-validation` / `db4746e`；保留用户已有 `MainModule.asset`（含 `isClearFolders` 设置）、Start 场景 URP Camera/序列化及两个自动图集改动，未回退或代改。原游戏启动入口仍为 `Assets/Scenes/Main.unity`，不能用没有 Main 启动脚本的 Start 场景直接 Play 替代真实入口。

| 补验项 | 实际结果 |
|---|---|
| 恢复产物核验 | MainModule 1.0.0 的 `update_date=2026/10/9 19:03:49`；112 个 Bundle 大小/MD5 全部匹配，与本地输出逐项一致。新构建使用 `AssetBundleNameMappingUpgrade.txt`，对应源码生成器/加载器，缺少旧 `AssetBundleNameMapping.txt` 不是故障。构建信息 SHA-256（Base64）`3x/8MuP9IKJVQxdt6W9nL7bLqL7bg3UbisVIk2WRgUU=`，Upgrade 映射为 `h1n4nD3zL4C4g+49uIdR3TDRl18CREXAGkEDPWFAonY=` |
| 原游戏 Windows64/Mono 构建 | 单次、独立 Development 构建成功，0 errors、4 项 MCP 空程序集 warnings；未调用 XFAB 构建或复制。入口为 `C:/Users/Kano/AppData/Local/Temp/kilo/CGJ2024-1.4-restored-20261009-112143/CGJ2024.exe`，日志同目录 `player-validation.log`。安装包中的 112 个 Bundle、构建信息和 Upgrade 映射与恢复后的源码侧产物一致 |
| 原游戏 Player 非干扰 | 工具实际启动 Player，日志不再出现旧产物 missing script、AudioController 缺失或 View 加载失败。因 Windows 前台输入限制，代理未自动完成按钮操作；用户通过本轮 question 明确确认“完整流程通过”：菜单设置/音乐/点击音、进入、暂停返回、再入、结束重开。这是用户实际人工结果，不冒充自动化通过；正常关闭 Player 后日志无异常/失败，进程已退出 |
| Core 真实测试 Player | 用户明确授权 Test Runner 临时启动设置及恢复；实际安装 Test Framework 1.3.9，使用现有跨平台 fixture，StandaloneWindows64/Mono 测试 Player 构建成功、0 errors；4/4 Passed，0 Failed/Skipped/Inconclusive。用例为 `EmptyModuleRegistersClosesAndRestarts`、`StartupAndCloseOrderClearAllMvcIndexesBeforeRestart`、`DirectPrefabInitializesOncePerLoadAndCloseReturnsSameObjectToBackendCache`、`EnableDisableAndCacheReuseSubscribeExactlyOnceAndUnsubscribe`；涵盖 Module/MVC 注册/关闭/重启和 DirectView 初始化、缓存及订阅生命周期 |
| 测试产物与 warnings | Core 结果为 `C:/Users/Kano/AppData/Local/Temp/kilo/CGJ2024-1.4-core-player-20261009-113712-results.json`；测试 Player 同名前缀目录下 `PlayerWithTests/PlayerWithTests.exe`。其构建 233 warnings：4 项 MCP 空程序集、10 项 CS0618、192 项 CS1591、6 项 CS1574、14 项 CS1573、2 项 CS0419、1 项 CS0672、4 项 CS0109，来自本轮未修改的已有源码/测试；未关闭诊断或顺带清理外围代码 |
| 临时状态恢复 | Runner 恢复了内存设置，但磁盘曾留有 5 项测试值；仅还原本次产生的 Splash 两项、resizableWindow、fullscreenMode 和 aotOptions。以测试前 SHA-256 快照核对 ProjectSettings 三项、manifest/lock、用户 MainModule/Start/两图集共 9 个文件，全部完全一致；Packages/ProjectSettings 最终无 diff，临时 InitTestScene 无残留。恢复测试前音频偏好，原场景为 Main、未脏、未处于 PlayMode/构建、无编译失败，Core 注册及 Logger 对象均为零 |

本轮补验不新增 Fixture、不重跑所有历史专项；CoreXfab 整文件为 Editor-only，其 Assets/AssetDatabase 及场景恢复路径不能原样用于 Player。命名资源/JSON/异步取消/场景/Panel 与新旧同会话协作沿用首次真实 Editor 证据，Player 只补上述代表性运行风险；不宣称全部 Core 历史用例、命名 smoke Bundle 或 IL2CPP/AOT 已在 Player 运行。

首次旧资源阻断已由用户恢复产物并经实际 Player 验证解除，没有发现需要修改已批准 Core 的新回归。Logger 仍是已归因的原系统/MCP 过渡期问题，未修复，但未阻断已验证的游戏与 Core；独立 Player 正常退出日志未显示该异常，不外推为 Logger 文件日志/全部退出路径已修复。依赖声明/锁文件/注册包差异仍为已知环境恢复风险：门禁基于当前已核验的 Unity、实际注册包、源码与产物，不宣称干净 checkout 自动恢复相同环境；不擅自改包或把该旧问题扩大为本轮架构融合。

**阶段 1 收口结论（历史）：阶段 1（1.1～1.4）COMPLETE，全部验收完成；Module/MVC、View/Panel 核心融合已完成；原游戏 Editor 与 Windows64 Player 代表性非干扰验证通过；新 YL Core Windows64/Mono 代表性测试通过；已合并至 `integration/xf-yl`（`6cea527`），1.4 提交 `fd7b1e8`。具备阶段 2 公共运行能力融合的技术前置条件，但不自动启动阶段 2。临时 XFAB 依赖、真实 AB 释放、跨平台与最终独立运行仍是后续事项；环境依赖恢复差异与原 Logger 问题继续保留记录。**该批代理的最终受管改动仅为本文；原 XF Package、游戏业务 C#、既有 YL Audio、Core 产品代码及测试未改，用户资源恢复差异原样保留。技术完成不等于最终独立于 XF，也不等于消除原系统所有问题。

## 8. 2.3 Update 最终技术收口

2026-10-10，`fusion/2.3-update`，C 基线 `88cd76f`。A/B 已由用户验收并提交；本轮 C 及专题最终门禁完成，尚未提交。完整契约与验证证据增量记录在 [2.3 专项方案](../../../.kilo/plans/xf-yl-2.3-update.md)。

- 公共三相位唯一正式类型为 `YLFramework.Foundation.UpdateManager`，复用 Events 的 channel、精确 token 与快照/owner 机制；相位内按注册顺序，公共异常继续并报告。Core View/Panel/Audio 消费它；Action 三相位自动消息按全局宿主有效启用状态连接，保持 owned EasyEvent 内部 fail-fast、局部清理及生命周期轮次隔离。
- 退出 Core 同职责 UpdateManager/旧静态字段、消费者先 Remove 再 Add 模式、Action 独立三相位转发。Starter 保留独立 DDOL 协程宿主和原 owner/会话路由；Action 执行器/回收、Core Timer、YL Timing/Audio 继续持领域驱动及时间/所有权语义，不强迁、不保证跨领域全序、不宣称性能提升。
- Foundation 不反向依赖 Core/Runtime；Core、Runtime 继续直接依赖 Foundation，未新增程序集。C 保持七个 Action 消息 API、动作组合、租约/暂停/取消、执行范围、协程和回收；A 的 UpdateManager 类型迁移与 Add 返回 token 兼容影响保留。C 的初始化成功提交、宿主启停与 Quit 先关闭创建门禁契约已写入 Action XML/模块说明。
- Unity `2022.3.62f3c1` 实际编译无错误；定向 EditMode **13/13**、PlayMode **45/45**（Action 消息 7、生命周期 16、公共 Update 6、Starter 15、原游戏 1）。A/B 未受影响的 Core/程序集等验证沿用历史证据，没有重跑完整 Pool/Events/Core。独立只读审查发现的测试所有权问题已修正，最终复核无阻塞。
- **真实关闭 Domain Reload 连续两轮 Play 通过**，正常 Scene Reload。旧公共 token、owned event、Coroutine 与 ActionHandle 保留到第二轮核验，新宿主各一份、旧身份不影响新工作；两个真实 Exit/Quit 均拒绝新启动/重建。红验收发现 Editor 退出监听顺序窗口，已仅在 Foundation 窄 gate 补充 isPlayingOrWillChangePlaymode 检查并完成完整两轮重验；Player 分支未改变。
- 恢复原 enterPlayModeOptionsEnabled=false、保存 options=3 及未脏 `Assets/Scenes/Main.unity`；没有临时场景资产或新测试设施。现有原游戏 Main/XFAB→原 XF MainModule/XFFSM、菜单/设置/进入/返回/再入/结束与现有 YL 音频流程 **1/1** 通过，fixture 恢复相关运行设置/音频偏好。原 Package、游戏业务、资源路径、Packages 与 ProjectSettings 无受管改动。
- 本轮未新增 Player/IL2CPP 构建、完整人工游戏或性能测量；当前 Windows64/Mono 未引入新的 AOT/反射路径或 Player 依赖，沿用阶段 1 Player 历史证据及本轮 Editor 代表性非干扰。原 Logger/MCP 过渡问题、依赖恢复差异、XFAB/producer 物理取消和资源释放等既有限制保持对应专题归属，2.3 不承诺同时关闭 Scene Reload 的全领域恢复或驱动损坏自愈。

**2.3 Update 已具备技术 COMPLETE 的全部条件，2.4 Timer 研究的技术前置已满足；C 等待用户验收与 Git 收口。到此停止，不自动提交、集成或进入 Timer。**

## 9. 干净工程导入与独立交付验收

### 9.1 授权与保护基线（2026-10-19）

- 用户正式授权连续完成最终集成包导出、仓库/worktree外自有Unity `2022.3.62f3c1`接收工程、首次导入、实际使用、Windows64 Mono/IL2CPP非Development Player、直接交付缺陷修复、重新导出和一次交付定向Review；普通取舍自主执行，不逐批等待批准。
- 已实核HEAD为`586eebedaf900d926d2e30301b20ed3129decb27`，暂存区与受管工作区无改动；原未跟踪`Assets/YLFramework/Tests/Fixtures/WorkbenchUX1-20261017/AlternateContent.meta`保留。独立UI worktree、其他worktree、原游戏、Packages及ProjectSettings不接管。
- 主Editor路径`G:/Unity/Projects/CGJ2024`、PID`32168`；开工为EditMode、无编译失败/构建，`Assets/YLFramework/Tests/Core/Acceptance/UIAcceptance.unity`已保存。每次主MCP执行均须在代码内核对路径/PID；独立工程不安装MCP或原XF，用显式`-projectPath`入口，禁止同一工程双开。
- 已读取当前AGENTS、本文、Resource方案§12.2和UI方案顶部最终稳定验收。Resource Low支持与UI原Development/Medium结果分别保留，不外推High/其他平台；历史Scene风险、普通CA正向联网未测及原生泄漏遥测边界保持。
- Logger修复现归原XF embedded package，不预设为YL产品依赖或默认导出内容；本轮须核实实际调用与许可，不能安装原XF来补产品依赖。最终包须包含最新Resource/UI/Analyzer源码和正式部署产物，适用且可交付的Logger修复按真实归属明确报告。

### 9.2 简短验收清单

- [x] 原正式Exporter导出最新闭包；源码身份、SHA256、逐文件/meta/GUID、依赖与适用许可核对；无原游戏/旧XF/测试/秘密/系统DLL/无授权制品。
- [x] 自有独立Assets/Library/Packages/ProjectSettings；按最小必要依赖安装并记录真实解析版本；测试设施介入前，最终框架包真实Unity导入和编译通过。
- [x] 检查Missing Script、GUID/Prefab引用和反向原游戏依赖；Foundation/Core、Audio/Action/Logger、轻量根/完整模板/Panel复用/Tips/模态代表冒烟。
- [x] 新工程自行生成两版资源；多目录/None/自动Atlas/C02/ResourceStartup、更新/回滚/重启/UI租约归还与E04正常/缺key拒绝通过。
- [x] 独立Windows64 Mono及IL2CPP（Low，真实记录输入后端）非Development构建和图形Player首启/协作/正常退出；沿用既有详细矩阵，不重跑全部历史用例。
- [x] 缺陷回写正式交付；首次导入受影响则另一个新空工程复验最终包；失败与复验绑定，定向Review及保护终态核验完成。
- [x] 最终包、SHA、准确安装步骤、源码/包/接收工程映射、实际证据/截图和非Temp唯一复现入口交付；所有必要门禁已通过，结论见§9.5。

### 9.3 执行结果

必要技术门禁已通过，结果以§9.5最终绑定为准。首个证据根为`G:/Unity/YLFramework-Stage9-20261019`，后续短路径交付根为`G:/Unity/Y9`，owner记录保留二者对应关系。早期失败不改写为通过；本轮不自动执行提交或下一迁移阶段。

- 原Exporter经许可边界修复后生成740条记录的候选包；两个直接NUnit回归先失败后通过，实际逐文件/meta/GUID/载荷SHA与正式根闭包核对通过。商业Editor Enhancers留在主工程但不再分发；UI仓库专用Acceptance构建器保留GUID迁入Tests/Core/Editor。
- ReceiverA由真实Unity `-createProject`建立，最小依赖仅UGUI 1.0.0（builtin）、Json.NET 3.2.1（registry）及新工程自身内建模块；正式`-importPackage`真实编译生成四个YL程序集后才安装公开API消费者，无原XF/MCP/测试框架或主工程DLL。导入进程exit0后日志共享读取冲突通过回收同一结果解决，未重复启动。
- 首次附加审计误将Unity跳过的无后代空目录和Editor-profile的条件UnityEditor引用判为失败；两份失败日志保留，修正审计后`ReceiverA-Audit-audit2`通过，所有实际产品载荷/meta、Prefab脚本/引用、Assets GUID、Player编译引用及真实Analyzer compiler inputs核对成立；没有通过补依赖改变产品。
- ReceiverA自建资源首次native构建产生了超出260字符的`.manifest`，native写入成功而Unity Mono读回失败。失败日志/半成品保留；补原始Workspace写入前检查及回归，不改变既有工作区身份，不清历史目录；后续另建短路径ReceiverB从重新导出的正式包开始。

### 9.4 独立运行证据与取舍

| 门禁 | 当前实际结果 | 证据/范围 |
|---|---|---|
| 第二个真正空工程 | ReceiverB首次导入、真实Unity编译通过，测试设施之后才安装 | `ReceiverB-first-import-initial.json`，候选包SHA256 `83ece848d447ef089875538081c1e29f43c254030765400130b73f5cb5de18de`；不是复制ReceiverA |
| 新资源与样例 | 本工程生成None/E04各两版，两个来源目录、自动Atlas、发布、C02及随包Create/Build/Deploy样例成立 | `ReceiverB-publications-initial.json`、`ReceiverB-configuration-initial.json`；生成退出crash及同产物校验记录均保留 |
| 无XF Mono图形Player | 8项通过，均Windows64/Mono2x/非Development/Legacy Input，实际exit0及退出后日志核验 | `P/m-ugui1`；Runs：`smoke4`、`mono-update1`、`mono-restart2`、`mono-rollback1`、`mono-restart1`、`mono-encrypted`、`mono-missing-key`、`mono-sample`各自`verdict.json` |
| UI与生命周期 | 轻量根、完整模板、Panel打开/关闭/复用、Tips、ColorSelector、模态标准Selectable、资源Panel和租约归还通过 | 实际Canvas raycast + UGUI pointer handlers；截图`Runs/ReceiverB-Mono-smoke4/ui-modal.png`；Model/Controller各退出1次，Lease/实例缓存/资产/Bundle/CNG清理责任归零 |
| Audio/Action/Logger基础 | Action/Timer真实帧推进、Audio资源Clip加载和native AudioSource playing、Unity原生日志观察通过 | Audio输出采样Peak为0，未据此声称已验证可闻音频；不把原生日志当作XF Logger补丁共存证据 |
| 可选Logger旧制品 | 单独MIT归档27文件，主产品无依赖；产品独立8项通过后才安装，Mono共存通过 | 旧SHA256 `ea7011b7594bed0ebb3d110ccf5b9077eadd9c76fed984e4a5ae146bf9b1ecef`；`coex-mono`验证事件、两代writer独占读回和后续handler所有权 |
| IL2CPP Low旧制品 | 构建成功、Core/UI/Audio/Resource通过，但Logger程序集整棵缺席，组合失败 | `P/i-l1`、`ReceiverB-Il2Cpp-il2cpp-smoke`；不是通过，未运行后续该构建的8项 |
| Logger直接修复 | 回写正式embedded源码的程序集级`AlwaysLinkAssembly`，不全量保留，不改版本/程序集/GUID；真实IL2CPP失败→通过 | 官方2022.3该Attribute文档明确要求未被场景类型引用的RuntimeInitialize包仍被UnityLinker处理；新制品`Optional/Final/` SHA256 `a156bfd07c687bebd28d0abec42e639c8e56f6217a75db4f77bf624db7542057`，独立Mono和IL2CPP Low已复验 |
| 最新Logger Mono | 实际非Development/Mono2x/Disabled图形进程exit0，完整业务链与Logger自动初始化、事件、两代writer/handler归还通过 | `P/m-mf`，`Runs/ReceiverB-Mono-mono-final-logger/verdict.json` |
| 最终IL2CPP组合 | Release/Low/非Development/Legacy Input，8项代表性图形进程全部exit0，晚退日志无错误 | `P/i-l2`，`il2cpp-final-smoke/update1/restart2/rollback1/restart1/encrypted/missing-key/sample`；每项都含最新Logger生命周期验证 |
| Editor退出 | 早期Generate native退出crash；多个成功方法报告后退出停滞，已只停止明确自有进程；IL2CPP l1构建进程实际exit0 | 不将完成报告等同正常退出，不启动同工程第二Editor；每个PID/projectPath与原始日志保留 |
| 主工程保护 | Main Editor仍为32168，未显式改其backend/stripping/input或用户场景；两个新增Standalone默认map项的具体本次落盘触发者未证实 | 日志有此前UI构建setter恢复记录，但getter+SaveAssets实验未复现，不把候选当事实。用户2026-10-19追加选择“移除两项默认记录 (Recommended)”，授权仅移除这两项表示，保留Android等其他映射与实际Mono2x/Disabled，收口时核验 |

取舍记录：本阶段只补代表性模态路由，不扩大到原UI完整物理输入矩阵。首次OS点击尝试失败，后续严格foreground检查拒绝注入；最终消费者彻底移除全局桌面输入，使用实际raycast最上层命中对象的公开UGUI处理器，不绕过Mask直接点下层Button。该结果不称为OS物理输入验证，原UI Medium/Both/New/Legacy范围证据各按原记录保留。

交付定向Review已完成一次独立只读审查。原全局输入保护和退出期误绿、导入包身份重绑定、Tools副本路径假设、Player同次结果回收问题均已在源码定向修复；`RecoverPlayer`已对同一次失败`smoke3`保持失败、对同一次成功`smoke4`保持通过，没有重启Player。最终包已通过第三个空工程真实导入、附加审计与同次`RecoverImport`核验，工具快照及保护终态见§9.5。

可选Logger自带Analyzer使全工程编译产生既有XML/公开成员文档警告，最新Mono BuildReport为977 warning、IL2CPP为976 warning、errors均0；未以关闭诊断、补空文档、忽略失败或升级依赖消除，原始消息完整保留。该警告不冒充编译失败，也不宣称零警告。

### 9.5 最终交付与停止

**阶段9独立工程交付技术验收完成；用户于2026-10-19确认验收并授权本批Git收口。** 下表保留原技术交付身份，后续审批不回写已冻结包或独立工程。

| 交付项 | 路径与身份 |
|---|---|
| 正式框架包 | `G:/Unity/Y9/YLFramework-Stage9-Final.unitypackage`，3,930,873 bytes；SHA256 `d1710ed15ac86048f1a479d04779a776a27520060c35aeb2e39c8b4fc5d2a91c` |
| 可选独立Logger | `G:/Unity/Y9/Optional/Final/com.xfkj.xfgameframework.loggersystem-1.0.1.tgz`；SHA256 `a156bfd07c687bebd28d0abec42e639c8e56f6217a75db4f77bf624db7542057`；不是YL必需依赖，不携带其他XF总包 |
| 精确产品闭包 | 最终包同名`.files.json`逐740条记录给出路径、GUID、payload/meta SHA；同名`.receipt.json`绑定原Exporter根、HEAD及实际导出时间；`export-final-green.json`记录5个直接NUnit方法验证，不冒充Test Runner计数 |
| 最终空工程 | `G:/Unity/Y9/ReceiverC`，由真实`-createProject`建立；`ReceiverC-first-import-initial.json`证明测试设施介入前最终包真实Unity编译，原导入进程exit0；之后的`ReceiverC-audit-final.json`核对7份Prefab、0 Missing Script/0 GUID冲突、Player引用和已部署Analyzer的真实compiler inputs |
| Player工程与产物 | `G:/Unity/Y9/ReceiverB`；`P/m-ugui1`为无XF八项Mono证据，`P/m-mf`为最新可选Logger Mono代表冒烟，`P/i-l2`为最新可选Logger IL2CPP Release/Low八项；均非Development、Legacy Input、Built-in Render Pipeline，Direct3D11图形进程和正常退出证据完整 |
| 依赖与许可 | UGUI实际1.0.0/builtin、Json.NET实际3.2.1/registry、批准BC2.7.0随包；Tink仅framing参考和Apache-2.0文本，无SDK。包/Player适用MIT、Unity Companion及Third Party Notices均保留；可选Logger1.0.1实际来自local-tarball且无传递依赖，不使用AssemblyVersion代替包版本 |
| 稳定复现入口 | `G:/Unity/Y9/Tools/Invoke-Stage9Delivery.ps1`及`Run-Stage9Representative.ps1`；安装与新空工程完整命令见随包`Documentation/README.md`，原源码模板与脚本位于`Assets/YLFramework/Tests/Tools/`；Tools快照历史保留，不依赖Temp作为唯一说明 |
| 截图与失败映射 | `Runs/ReceiverB-Mono-smoke4/ui-modal.png`、`Runs/ReceiverB-Il2Cpp-il2cpp-final-smoke/ui-modal.png`；首轮长路径失败、native点击失败/安全拒绝、旧Logger IL2CPP整程序集裁剪失败及后续复验均保留原Run，不覆盖或重复启动同次任务 |
| 机器可读绑定 | `G:/Unity/Y9/delivery-binding.json`、`.sha256`与`SHA256SUMS.txt`关联源码/最终包/独立工程/17份必要Player verdict/实际IL2CPP原生与生成C++/保护终态；`final-source.patch`与当前改动文件SHA记录未提交源码身份 |

最终包与ReceiverB受测包的所有可执行源码、部署DLL、Prefab、GUID/meta均相同；最终阶段仅补安装、来源许可与验收文档。最终空工程C已对最终包再次编译和审计，未用补齐过依赖的工程代替首导入。本文本节是**最终导出后的验收日志**，不重写已被验证的包；包内本文保留导出时真实快照，`delivery-binding.json`以包内/当前文档SHA明确记录这个唯一事后日志差异，不假称包内已含后来结果。

保护核验覆盖3481个文件，除两个回写正式源码的Logger修复文件和用户明确授权的两个Standalone默认记录表示清理外，无未授权差异。主`ProjectSettings/ProjectSettings.asset`最终`git diff`为空；实际Mono2x/Disabled、Both(2)、Enter Play false/options3、pause false保持。主Editor PID32168、原UIAcceptance场景已保存/无dirty、EditMode/无编译失败/无构建；独立UI worktree、主Packages其他内容、原游戏、用户场景、原未跟踪`AlternateContent.meta`未接管。自有接收Editor均已退出或按精确PID回收；退出停滞不称正常Editor退出，成功Player均另有真实exit0证据。

必要门禁之外的边界不扩大：UI原Medium结果保留原范围，本轮仅IL2CPP Low，不声称High或其他平台；原Scene风险、普通CA正向联网未测继续保留；未建立零原生泄漏遥测结论。当前模态输入为实际Canvas/UGUI路由，不是OS物理输入；Audio播放资源和释放通过，不宣称已验证可闻音频；既有文档警告不隐藏。

阶段9技术验收当轮未暂存、Commit、Push、合并、切换或删除分支，HEAD仍为`586eebedaf900d926d2e30301b20ed3129decb27`；这是原始验收时事实，不改写为当时已经提交。

**2026-10-19后续用户验收与Git收口：** 用户确认修订版融合原则并授权阶段9精确提交/普通Push及独立的E0批次。收口前实时远端与本地均为上述旧HEAD、暂存区为空、无索引锁或可见活跃收口会话；只读核对`delivery-binding.json`的26份当前源码/meta（包括受保护用户文件）全部SHA匹配，最终框架包/可选Logger/绑定文件SHA与冻结记录一致。仅追加本次用户验收文字，不重跑完整交付矩阵、不重建或覆写`G:/Unity/Y9`；原用户`AlternateContent.meta`和新拓展Plan不纳入阶段9提交。实际B9完整SHA与推送核验结果在后续E0专项Plan记录，不通过自引用或额外空提交伪造当前提交身份。

完整暂存`diff --check`另发现六份新工具meta的18处Unity空值行尾空格；本次仅移除这些行尾空白，GUID/Importer/字段值不变，工具不在正式包导出根内。不重新冻结或覆盖旧记录；原技术验收SHA仍指规范前字节，Git收口差异为本段所述文档审批与meta空白，不冒充重新执行的技术验收。
