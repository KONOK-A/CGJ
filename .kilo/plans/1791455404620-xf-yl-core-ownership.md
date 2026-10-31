# 阶段 1.1：XF 主框架源码承接到 YLFramework

## 已确认目标与决策

- 目标是形成独立、完整、统一的 YLFramework，不以迁移 CGJ2024 为目标。长期范围为 XF 主框架、XFABManager、XFFSM、CharacterController2D、MoveablePlatform、SkillSystem、BuffSystem、Overlap、Localization、MissionSystem、ScriptableObjectSystem、ExcelSystem、ArchiveSystem、ReddotSystem、LoggerSystem，共 15 个选定系统。主框架 18 项功能仍是当前优先子范围，不替代长期范围。
- 原 XF 主包、独立扩展、游戏源码、游戏程序集引用及原资源继续保留不动。此前的“退出原包并沿用旧身份”建议已撤回。
- 直接承接到 `Assets/YLFramework`，不增加 Embedded Package 中转，不开发安装更新功能，第一版不做 UPM。
- 承接完整主 Runtime，以及排除安装/更新/About 组后的 Editor 主体；这不是提前认可或融合 Audio、Events、Pool 等内部实现。
- 新主框架先使用独立 `YLFramework.Core.Runtime`、`YLFramework.Core.Editor`；必要共用基础放入中立 `YLFramework.Foundation.Runtime`。Foundation 不引用 Core、原 XF 或资源业务。后续按真实依赖决定是否合并/拆分，不把 15 个系统等同于 15 个程序集。
- 新源码使用 `YLFramework` 及适用子 namespace、新程序集身份和新 GUID。旧 XF 身份只属于原包，不为新副本建立旧 API 适配层。
- 原游戏始终保留原有框架路径，最终正常运行不等于改接新 YL。整体融合收口时，新 YL 的 Runtime、Editor、资产及保留工具链不得直接或间接依赖这 15 个系统的原程序集；Unity 和经核实的第三方依赖另论。
- 本计划未授权自动实施、提交、合并或推送。1.1 允许有结束条件的临时 XFAB 资源后端；不要求本阶段完成资源系统或其他 14 个系统的内部融合。

## 基线与证据

- 本轮读取 HEAD 为 `d673af8c692b4c769000d8cd26b8d2f2709328c7`，Unity `2022.3.62f3c1`。
- 主 XF `1.18.8`，revision `2182708aecc596f30739fa8634fe2b9b6946ddd7`；XFAB `1.24.0`，revision `c35dc4058ecd831409199b43cb328024202d4557`。
- 主 Runtime 单程序集依赖 XFAB；原样 Module/MVC 静态闭包至少 28 个脚本，并存在 internal 访问和支撑层反向引用 View。不要机械拆分 MVC、Manager、Events。
- XFAB 提供加载、对象池、协程和异步支撑，未发现其 Runtime 对主框架的固定编译反向依赖。资源后端仅作为过渡依赖，不再定位为永久外部资源系统。
- 增量核验：四类公开请求继承 XFAB 的 CustomAsyncOperation。该基类、CoroutineStarter 及其 ApplicationTool 支撑没有资源业务调用；ExecuteOnlyOnceAtATime<T> 的约束仅为 Unity CustomYieldInstruction。因此先承接这些必要基础具备类型边界上的可行性，但完整编译和行为仍需验证。
- 主 Editor 的真实名称是 `XFGameFrameworkEditor`。原扩展继续引用它和原 Runtime；新 YL 不接管这些扩展的编译关系。
- 默认 Canvas 引用原适配脚本 GUID `abf116dc13c27ef4583812631aacc1c3`；原游戏 UnityEvent 实际保存 `XFGameFramework.View, XFGameFramework`。二者都不得修改。
- 现有 YL 测试不覆盖新承接核心。游戏 `GameViewLoadingTests` 使用 MainModule 资源，不能作为独立框架验证。
- 游戏当前也引用 YLFramework.Runtime 并使用 YL AudioKit（MainModule.cs:4,27-28，Scripts/Controller/AudioController.cs:1,35-44）。保留原 XF 不代表修改现有 YL 永远不会影响游戏；1.1 不改这些入口，后续淘汰相关 YL 实现时须专项处理真实兼容边界，不能静默修改游戏或为此冻结整个新核心设计。
- 本轮只读取源码、资产及编辑器状态，没有刷新编译、运行测试或构建。空 Error 日志不能证明编译通过。

## 归属与依赖

建议承接目录：

```text
Assets/YLFramework/Runtime/Shared/
  YLFramework.Foundation.Runtime.asmdef
  必要请求基类、协程宿主、运行状态支撑及来源许可
Assets/YLFramework/Runtime/XFMain/
  YLFramework.Core.Runtime.asmdef
  主 Runtime 源码副本
  Resources/YLFramework/Core/Canvas.prefab
  Resources/YLFramework/Core/EventSystem.prefab
  LICENSE.md
Assets/YLFramework/Editor/XFMain/
  YLFramework.Core.Editor.asmdef
  非包管理 Editor 源码及必要资产副本
Assets/YLFramework/Tests/Core/
  EditMode/
  PlayMode/
```

- `XFMain` 是来源承接区，不是永久模块/API 分类；Core 是当前主框架承接程序集，不是所有未来公共基础的归属。复制、改名只完成归属，不代表功能融合验收。后续逐模块收敛唯一实现，整理已自有内容时保留其新 GUID。
- Foundation 仅包含本轮确认的三类必要基础及实际依赖闭包，依赖 Unity；ApplicationTool 的 Editor API 保持条件编译，不引用 Editor asmdef。不要把有 View 反向依赖的 EventManager、Pool 或所有工具提前放进 Foundation。
- Core Runtime 引用 Foundation、临时 XFABManager、UnityEngine.UI；沿真实 DLL 导入配置保留 Newtonsoft 可见性，不猜不存在的 asmdef 名称。四类请求明确继承 YL Foundation 基类，Core 执行支撑调用也绑定新类型；对仍使用 XFAB 的文件采用明确限定或别名，避免同名基础类型误绑定。
- Core Editor 仅 Editor 平台，引用新 Core Runtime、实际使用的 Foundation/uGUI及临时 XFAB 资产定位依赖；不引用原 XF 主框架 Runtime/Editor。不增加空桥接程序集来掩盖原依赖。
- 当前不添加新程序集与现有 YL Runtime/Editor 的相互引用，不修改游戏 asmdef。未来资源实现可依赖 Foundation，但不得因共享基础反向依赖 Core 再与 Core 形成闭环；实际额外共用内容届时研究。
- 最终 namespace 按 YL 领域和真实共用归属组织，不对整个生态一律替换为同一个根 namespace。已有证据是 XF 与 XFAB 均有 AssemblyTools；以后需统一语义或隔离领域内部类型，避免重复 FullName 和反射首个命中的歧义。现在不预建其他系统的空目录、接口和 asmdef。
- 新副本的脚本、asmdef、资源由 Unity 生成新 GUID，并保留必要导入设置。不能直接复用原 `.meta` GUID；已有 YL 文件如需移动则保留其自身 GUID。
- 首次导入前准备好新 namespace 和 asmdef，避免活动工程暂时编译两个同名类型。通过受控导入或编辑器关闭窗口准备副本，不清理或修改 PackageCache。

## 必须隔离的身份与副作用

1. namespace 替换只作用于新副本；检查全局类型。`Runtime/Attribute/CustomClassNameGUI.cs` 原本没有 namespace，也须放入新 YL namespace，不能只替换已有 namespace 行。
2. 默认 UI 使用唯一 Resources key `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem`。复制而非移动旧 Prefab；仅新 Canvas 的适配脚本引用映射到新 YL 脚本 GUID，Unity 自带组件引用保持不变。
3. YL 编辑器生成器只检查/创建上述 YL 自有路径；删除处理只作用于明确的 YL 资产范围。原 XF 生成器、资源、删除保护均不动，不能重新生成同 key 的副本。
4. MenuItem 使用独立 YL 路径；CustomEditor/Drawer 绑定新 YL 类型。CreateModule 模板生成带独立示例 namespace 的类，默认写入 YL 专用示例目录，不再固定写入 `Assets/Games`，拒绝覆盖已有文件。
5. 新音频配置使用 `YLFramework.Core.Audio.Volume:{Name}`、`YLFramework.Core.Audio.Mute:{Name}`，不读写原 `AudioConfigVolume/AudioConfigMute`。只隔离存储身份，不设计音频内部融合，不迁移旧游戏偏好。
6. 新 LogUtil 使用 `temporaryCachePath/YLFramework/Core/Logs`，其清理只作用于新目录，不能沿用会删除原日志的 `temporaryCachePath/Logs`。
7. ProjectSettings 保存按具体类型 FullName 分离；不迁移或覆盖旧设置。反射仍可扫描已加载程序集并按唯一 FullName 匹配，不能限定只扫描 Core 而阻止业务模块发现。
8. XFAB 暂为共用外部资源后端。新验证资源项目使用唯一名称 `YLCoreSmokeModule` 和新 YL View/Panel 资产，不复用 MainModule、旧 Bundle 或旧 View Prefab。启动当前会强制 ProjectName 为类型短名，因此本阶段 fixture 的唯一类名与资源项目名一致；这不等于 namespace 自动隔离资源项目。ProjectName 的内部语义研究留给 1.2，不在 1.1 重设计或修复。
9. `EventSystem.current` 是 Unity 全局入口，保持复用现有 EventSystem，不替换其输入模块或把它视为 YL 所有；只有 YL 自己创建的对象才可在测试中销毁。这里不做输入系统融合。
10. Update/Timer/Audio 宿主依靠各自强类型静态引用，现有证据未发现按同名 GameObject 查找复用。无需为了形式隔离重写宿主管理；必要可读命名与所有权验证即可。
11. 共存不等于同时运行两套游戏主流程。新 YL 不默认启动业务或代管旧游戏场景、输入与全局设置；验证使用独立场景/fixture。未来承接其他系统时分别核验其自动初始化和全局回调，不机械复制启动入口。

## 临时依赖及解除条件

| 过渡边界 | 1.1 状态 | 后续解除条件 |
|---|---|---|
| 请求基类、协程、运行状态 | 先由 YL Foundation 自有；不保留原 XFAB 类型继承 | 编译、基本语义及与现有后端互操作通过；后续异步/执行支撑专题再融合实现 |
| 资源/场景加载、执行去重、GameObject 池 | 暂时调用原 XFAB，不借原资源作为 YL 自有资产 | YL 资源能力专题完成对应行为、所有权和专项验证后替换；1.1 标清引用与调用，不提前设计资源接口 |
| Editor 资产定位 | EditorAssetTool 暂使用原 XFAB 项目/Bundle类型 | 对应 YL 资源 Editor 能力自有化并验证，不误认为 Runtime 解除会自动解决此边 |
| XFAB fixture 配置/设置/产物 | 作为临时后端验证输入，允许暂用其资产格式 | 资源专题中转换或重建 YL 验证输入；最终资产 GUID、序列化类型、反射和加载路径均不得回指原系统 |

- 每条临时边必须关联具体后续专题与验收条件，不能到最终收口才发现。XFAB 后端解除后，从新 YL 删除原程序集引用和相关可执行依赖，不从当前工程卸载原包。
- 整体独立验收必须在不提供这 15 个原系统的验证承载中编译/运行新 YL，覆盖 Runtime、Editor及必要加载路径；检查间接依赖、DLL身份、公开签名、资产引用和工具链。当前同工程绿色编译不足以证明独立。
- 不预设计后续 14 系统的接口、算法、程序集清单或详细顺序；如真实依赖需要中立契约/共享实现，届时调整，不能把 Foundation 变成依赖业务系统的万能程序集。

## Editor 与许可例外

- 排除 `ExpansionWindow`、`ExpansionPackageListTree`、`ExpansionPackages` 及安装配置资产，排除 About 组。它们的旧包路径不得成为新 YL 的依赖。
- 不连带删除共享窗口、序列化扩展、配置、Handles、资源命名及图标查询工具；保留主 Editor 主体，1.1 不继续逐方法裁剪。旧扩展仍使用原包提供的 API，新副本不承担它们的兼容。
- 1.1 不复制 `XFGameFrameworkCodeAnalyzer.dll`。原分析器保持不动；改文件名或新 GUID 不能改变 DLL 内部程序集身份。其对 YL 的作用域/诊断影响须核验，不能禁用诊断来掩盖问题，也不能声称 YL 已获得等效覆盖。分析器源码化和适配明确延期到 Editor 专项。
- 主框架和必要 XFAB 基础副本均随附适用的完整上游 MIT 文本、`Copyright (c) 2021 弦小风`、文件头、来源 URL 和各自 revision。原包存在不代替新副本的许可要求；根 `2026 KANO` LICENSE 不能替代上游声明。
- 保留现有 YL 的 QFramework、JSONObject、SharpZipLib 等声明。不复制独立扩展及其第三方二进制；现有 Plugins 的合法分发整理属于后续范围，不把当前默认导出成功当作完整合规证明。

## 小批次实施

### 1.1-A 冻结来源与原线基线

- 前置：本轮已确认的双线共存、完整承接、独立程序集方案。
- 范围：核验工作区、源码快照、原依赖/资源身份和新增副本清单；不改原包、manifest、锁文件或游戏。核验缓存内容，不仅相信目录名和 fingerprint。
- 验证：获取新一轮 Unity 编译证据，分类原框架、游戏、独立扩展、环境诊断；确认原 asmdef/GUID/资源和游戏引用作为后续比较输入。
- 风险/回退：URP/uGUI/Cinemachine 声明与缓存差异、Localization 未解析额外 GUID，以及原扩展 Player 风险均先分类，不擅自升级或修复。该批无产品改动可回退。

### 1.1-B 自有必要公共基础

- 前置：A 的来源与元数据可核验；三类基础及实际闭包已核对无资源业务依赖。
- 范围：在 Runtime/Shared 新建 Foundation asmdef，承接 XFAB Runtime/AsyncOperation/CustomAsyncOperation.cs、Tools/CoroutineStarter.cs、Tools/ApplicationTool.cs 及实际必要闭包，使用 YLFramework.Foundation 及适用子 namespace、新 GUID 和原许可。不要在现有 Runtime/Foundation 根创建 asmdef并接管已有 YL 文件。
- 验证：实际 Unity 编译；Foundation 无原 XF/资源/Core/Editor 程序集依赖。对完成、等待、进度、错误和回调建立最小测试，对协程宿主做 PlayMode 验证；保留原状态判断语义，不把 Abort 当作已经实现协程取消。命名和存活检查正确，不接管原宿主。
- 风险/回退：错误引入资源依赖、重新设计基础行为、错误简化 ApplicationTool 或重复宿主所有权。仅撤销本批新增基础与测试，不影响原 XFAB。

### 1.1-C 建立独立 YL 主 Runtime 副本

- 前置：A、B 稳定，独立资源/配置标识明确。
- 范围：完整 Runtime、新 namespace、全局类型处理、新 asmdef/元数据和许可；同时隔离仅在新副本中的 Resources 字符串、音频配置键和日志路径。保留其他行为，不融合现有 YL 支撑系统。
- 验证：实际 Unity 编译；类型及 GUID 唯一，四类请求及执行基础绑定 YL Foundation；公开继承/成员不得误暴露原基础类型。Core 不引用原 XF 主框架或 Editor，XFAB 引用只覆盖已声明过渡项；原包/游戏引用未变。Core 测试显式引用 Core/Foundation，不靠隐式可见性。
- 风险/回退：漏改全局类型、错误绑定旧 API、DLL/响应文件配置。仅撤销本批新 YL 内容及明确修改，不退出原包、不回滚用户改动。

### 1.1-D 承接 YL 自有默认资源

- 前置：C 编译及新组件身份正确。
- 范围：创建两份新 GUID 的 YL Prefab，重绑定新适配脚本，保持 uGUI 引用；原资源完全不变。
- 验证：Resources key 唯一，新 Prefab 无 Missing Script、无旧 XF 组件；默认 Canvas/EventSystem 可加载。验证复用已有 EventSystem，不更改或销毁外部输入组件。
- 风险/回退：仅复制 Prefab GUID而漏掉 m_Script 重绑定、同 key 重复、外部对象被误清理。只撤销新资源和本批绑定。

### 1.1-E 承接非包管理 Editor

- 前置：B-D 稳定。
- 范围：新 Editor asmdef、主体源码和必要资产，新菜单/模板/生成路径，排除安装/About/分析器副本。生成默认资源不得触碰旧 XF 路径。
- 验证：实际 Editor 编译，Inspector/Drawer 指向新类型；创建工具在 YL 示例目录生成正确类型并拒绝覆盖；重开/重载后不修改旧资源，不产生重复菜单或 Resources key。原扩展仍使用原 Editor，无需改它们。
- 风险/回退：全局菜单与回调不会被 namespace 隔离，生成类若无 namespace会与原游戏冲突，原分析器可能影响新代码。回退仅新 Editor/新生成测试资产，不改原分析器配置。

### 1.1-F 建立独立框架运行验证

- 前置：B-E 编译与身份验证成立，测试对共享 XFAB/EventSystem 的所有权明确。
- 范围：新增 `YLFramework.Core.Tests.EditMode/PlayMode`；空模块、直接 Prefab View/Panel fixture，以及独立最小 XFAB 资源项目。测试不引用 Project.Game.MainModule 或原 XF。
- 验证：空模块注册、启动、查询和基本关闭/重入；Model/Controller 归属；直接 Prefab 初始化、激活/关闭和缓存复用；按名称加载、必要配表/场景资源链。验证 YL 配置/日志操作不改原键值或原目录。
- 条件：空模块暂仍调用 XFAB 执行去重，主请求和启动协程基础属于 YL，不能声称此时已经完全独立；直接 Prefab 暂用 XFAB 池；名称/场景/配表加载需对应 Profile、映射、Editor 资源项目或匹配 Bundle。沿用当前临时后端设置，不重复创建资源服务或误清理原输入。
- 风险/回退：测试污染共享资源、静态状态或旧配置；fixture 项目名被短类型名覆盖；同工程既有编译错误阻止测试。测试只清理自己拥有的对象/键/文件，不能调用共享资源的全局清空来制造通过。异常路径与完整生命周期语义留给 1.2/1.3，发现既有问题单独分类。

### 1.1-G 框架承接验收

- 前置：各实际完成批次均有验证证据。
- 范围：确认新源码、资源、程序集和许可均受 Git 管理；核验必要分发输入，不为本阶段顺带重做现有 Plugins 或整个导出系统。
- 验证：分别报告静态检查、Unity 编译、EditMode、PlayMode和临时资源链。验证 Foundation 不需要原系统，新 Core 无原主框架依赖，其剩余 XFAB 调用/资产输入与过渡清单一致；旧游戏现有 XF及 YL 音频入口不被修改。必要时安排框架 Player 兼容检查，不把原扩展风险归咎于新 Core。
- 完成标准：新 YL 来源可追溯、类型/资产唯一归属、公共基础自有、共存边界正确、剩余依赖有明确解除条件、无承接新增编译问题、最小闭环已实际验证。1.1 不要求已融合整个 XFAB，但不能称这就是 15 系统最终独立或主框架功能融合完成。
- 风险/回退：导出可成功但内容缺失或许可不完整；未运行不能算通过。失败定位到对应批次，保留原 XF 游戏线作为独立恢复依据，不执行全工作区 reset。

## 验证门禁和延期边界

- 不要求每批完整 CGJ2024 回归或 Windows64 Player 构建；不要求先修好所有原扩展 Player 风险才能研究、验证新框架。
- 同一工程编译错误会阻止测试。若原线/外部依赖实际阻塞新 Core 验证，明确标记未验证，并另行安排最小独立验证承载；不得修改旧游戏或删除原包来绕过共存决策。
- 原 Buff/Mission/Skill/ScriptableObjectConfigs/Overlap 的 Runtime→Editor 引用、扩展 Runtime 无条件 UnityEditor 导入、依赖版本差异均不在本阶段顺带修复。
- 不设计 Module/MVC、Events、Pool、Audio 等内部融合，不整体裁剪旧 API，不建立原扩展到新 YL 的兼容层，不迁移旧游戏 UnityEvent/Bundle/配置。必要 XFAB 公共基础已获确认纳入本轮，其他资源与扩展内部实现不纳入。
- 重要选择已确认；分析器适配、ProjectName 内部语义、最终程序集结构及受影响的既有 YL 游戏调用兼容明确在相应后续专题处理。将游戏改接新 YL 不属于本工程默认目标。

## IntegrationPlan.md 增量同步记录

已获授权，将以下条款增量同步至 `Assets/YLFramework/Documentation/IntegrationPlan.md`（保留原 18 项编号与主框架阶段顺序）：

1. 目标与边界：已明确原 15 系统保留供游戏使用、新 YL 最终不依赖其原程序集；XFAB 是临时后端而非永久外部资源系统；统一 namespace 只约束新 YL，不改原包；共存不等于承诺迁移游戏。
2. 蓝图状态与阶段 1 条款：唯一归属按完整类型/程序集/资产身份判断；允许有来源关系但身份独立的原 XF 与新 YL 共存，不要求退出原主包。
3. 融合范围与阶段表：保留主框架 18 项及当前优先顺序，增列长期 15 系统范围；“当前不融合扩展”不再是永久排除；主框架子阶段收口不等于完整 YL 收口。
4. 阶段表与 1.1-1.4 任务：改用框架专项验证和按风险的原游戏非干扰检查；独立 YL 及原游戏运行/Player 验收分别报告，不以修改游戏接入新 YL 为完成标准；最终增加无原 15 系统的独立验证承载。
5. 长期工程规范与可复现基线：身份隔离是共存前提，不能机械推迟新 namespace；复制生成新 GUID 与移动已有资产保 GUID 分开；临时后端 fixture 按本批适用，最终清除原资产类型依赖；MainModule 全量产物不作所有核心批次前置。
6. 版权与第三方许可：新副本必须携带原许可与版权，不依赖仍在原包中的声明；删除“未来原包被移除”的默认假设；根 KANO LICENSE 不能代替上游许可。

整体完成后新 YL 独立、统一且原 CGJ2024 正常运行，两项分别验收；不要求游戏改用新 YL。
