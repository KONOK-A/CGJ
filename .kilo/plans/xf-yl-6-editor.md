# XF × YL：阶段 6 Editor 深度融合总体方案

**状态：COMPLETE，阶段 6 已完成技术验收与 Git 里程碑收口，最终标签 `milestone/6-editor`。6.1～6.5 全部完成；6.4 提交为 `bc3db767`，后续 Analyzer 改进经用户功能验收并提交 `28dbb576`，专题详情见 [6.4 专题计划](xf-yl-6.4-analyzers.md)。6.5 实现、正式包留存、必要技术门禁和受保护文件复核均已完成，并针对最终 DLL 重新验证受影响部分。** 阶段 7 尚未启动。

**目标：** 保留成熟工具，把已承接的 XF Editor 支撑与 YL authoring、帮助、导出协调起来，修复真实工具链缺陷，并接入可维护的自有分析器。不是重做 Inspector 平台、资源系统或包管理器。

## 1. 基线与边界

- 2026-10-12 核对：主工作区干净，当前为 `fusion/5-runtime`、HEAD `16e283a`；本地及远端 `integration/xf-yl`、远端里程碑 `milestone/5-runtime` 的实际提交均为 `f6fba32`。两分支 diff 仅涉及 `IntegrationPlan.md` 状态文字，产品源码一致。
- 本轮保持只读，不切分支；蓝图直接读取 `integration/xf-yl` 的版本。关联测试 worktree 有未跟踪项，未读取或修改其内容，不声称不存在其他工作。正式实施前重新核对实际主工作区及并行改动。
- 依据：项目 `AGENTS.md`、目标提交的长期蓝图、`Documentation/Architecture.md`、阶段 1.1/1.2、2/2.5、3、4、5 的相关方案及实际源码/测试。历史验收与本轮静态研究分别使用；本轮没有执行 Unity 编译、测试、Play 或构建。
- 环境为 Unity `2022.3.62f3c1`。原 XF 主包 `com.xfkj.xfgameframework@2182708aec`、XFAB/XFFSM 等 Package 严格只读；原游戏、持久 Scene/Prefab/资源、Packages、ProjectSettings、真实 Bundle 不迁移、不修改。
- 不重构已验收的 Runtime/Foundation/Core/UI/Audio/Resource/Root；不预做阶段 7 的目录、公开命名及最终程序集收口。分析器新增的编译器资产不是 Runtime 逻辑迁移。
- 完整 XFAB、可视化 XFFSM、LoggerSystem、原扩展包安装器仍属后续专题。不得禁用原分析器、修改规则集或隔离 Logger 来制造验收通过。

路径简称：`E` = `Assets/YLFramework/Editor/`，`C` = `Assets/YLFramework/Runtime/XFMain/`，`Y` = `Assets/YLFramework/Runtime/`，`T` = `Assets/YLFramework/Tests/`，`D` = `Assets/YLFramework/Documentation/`，`XF` = 上述原 XF 主包目录。行号用于定位本轮基线，不是逐文件实施脚本。

## 2. 实际能力与消费者

| 职责 | 原 XF 能力 | YL 正式能力、消费者与归属 | 结论 |
|---|---|---|---|
| Module 创建 | `XF/Editor/MenuItem/CreateModuleCommand.cs:46,89`，写 `Assets/Games` 的单一模块模板 | `E/XFMain/MenuItem/CreateModuleCommand.cs:13-15,76-117`，生成独立示例 namespace/asmdef，拒绝覆盖并正确转义；使用者是框架接入者，直接契约由 CoreModuleGenerationTests 固定 | 已融合，不重写；双方都没有 MVC/View/Panel 全套生成器，不把不存在的能力列为遗漏 |
| 框架默认资源 | XF 自动生成 Canvas/EventSystem，并保护旧 Resources 路径 | `E/XFMain/Listener/` 维护 `YLFramework/Core/Canvas`、`YLFramework/Core/EventSystem`；直接消费者为 `C/Manager/PanelManager.cs:329,390`，仅补缺失资产，不重写已有身份 | 保留生成与窄删除保护；输入模块生成策略需对齐 |
| Settings/创建辅助 | Settings ScriptableObject、资产创建/重名、打开设置页、TreeView 状态 | `E/XFMain/Other/ScriptableObject/ProjectSettingsScriptableObject.cs:14-98` 已保护类型/保存路径；配置存 `ProjectSettings/YLFramework/Core/{FullName}.asset`。当前没有正式 SettingsProvider 或业务派生配置，消费者主要是专项测试 | 保留支撑，不造空配置面板；收敛重复私有反射 |
| 序列化/绘图支撑 | SerializedProperty/Object/ScriptableObject 扩展、Clipboard、Handles 胶囊、SplitWindow | `E/XFMain/Extensions/`、`Window/SplitWindow.cs` 已承接；除资产创建内部调用及直接测试外，未发现产品消费者 | 通用价值不由当前用量决定；保留有用支撑，修真实缺陷，不全部搬到新公共 Editor 层 |
| 展示 Attributes/Drawer | DisableEdit Drawer；原扩展生态消费原包属性 | `Y/Shared/Attributes/` 为 Foundation 元数据；`E/XFMain/CustomPropertyDrawer/DisableEditDrawer.cs:7-23` 正确绑定。CustomClassNameGUI/CustomPropertyGUI 没有内置消费者，见 `D/Modules/Attributes.md:16-39` | DisableEdit 已融合；另两项是扩展契约，不是自动 Inspector 功能，不新建全局 Attribute 扫描器 |
| 通用字典编辑 | 没有专用 SerializedDictionary Drawer | `E/Serialization/SerializedDictionaryDrawer.cs:17-110` 已提供 IMGUI/UI Toolkit、多对象及条目诊断；消费 Foundation 的唯一正式字典类型 | 2.5 已完成，保持现状 |
| Audio authoring/调试 | 只有旧 AudioPlayerInspector | `E/Audio/` 六个正式 Inspector、ClipLibrary/Mixer 资产、Debug Window 与 Help provider；消费正式 Audio Runtime。旧 Core Audio Inspector 已随阶段 4 退出 | 保留，不再承接原 Inspector，不另造 Audio 编辑器 |
| View/UI authoring | 无统一 View/UI 可视化管理中心 | UIRootContext、Tips/ColorSelector 及已有 Prefab 使用普通序列化 Inspector；WindowCatalog/WindowDefinition 是纯代码。Action 屏幕资源创建器使用 PreviewScene 并保护 `.meta` | 普通 Inspector/代码配置合理；不把代码目录变成配置资产体系，不迁移既有 Prefab |
| 资源标记/构建/代码生成 | XF 主包没有构建/导出工具；XFAB 在独立生态中提供自己的管线 | `E/Resource/` 标记菜单、ResService 窗口、BuildScript、ResDataCodeGenerator、子工程/数据表，服务自有 Resource 的 ResData。原游戏仍走 XFAB，二者不是同一管线 | 有独立价值；局部修复 Editor 链，不合并 XFAB 或改 Resource Runtime |
| Help/About/安装器 | XF About 读取包版本和 XFAB logo；ExpansionWindow 安装/更新/删除 UPM 扩展 | `E/Help/` 独立阅读器与显式注册；Action/Audio provider 位于普通 Editor。消费的是指南/当前 API，不执行示例。未承接原 About/安装器是有意边界 | 复用成熟 Help；退出重复帮助机制的新增需求，不复制包安装器 |
| Toolkit/导出 | XF 主包没有 UnityPackage 导出或 Player 构建入口 | ClearLog、OpenFolder、ExportUnityPackage；导出器保留完整根清单、GUID/meta 回归。没有统一 Player Build Window | 保留小工具与 Unity 原生构建入口，不造构建管理中心 |
| 分析器 | `XF/Editor/Analyzers/XFGameFrameworkCodeAnalyzer.dll` + 六个 Runtime 标记；只找到 DLL，未找到可维护源码 | 六个标记已位于 `C/Attribute/RoslynAnalyzers/`，实际用于 View/Model/EventManager/LoadViewArgs；暂无 YL 自有规则实现 | 按用户确认，本阶段补自有分析器，不复制原二进制 |

关键已融合证据：`T/Core/Editor/CoreEditorIdentityTests.cs:33-193` 固定 namespace、引用、菜单、GUID/许可与旧 Audio Inspector 退出；CoreModuleGenerationTests、CoreEditorConfigurationTests、CoreDefaultResourceEditorTests 固定创建/配置行为。`T/EditMode/SerializedDictionaryEditorTests.cs`、`T/EditMode/Audio/AudioGroupAuthoringTests.cs` 已有真实 GUI、Undo/Prefab/多对象、保存重开覆盖。上述是现存测试及历史证据，不是本轮重新执行结果。

## 3. 真正需要处理的问题

| 问题/证据 | 影响与推荐 |
|---|---|
| `E/XFMain/Listener/ListenerInitOnLoad.cs:92-100` 固化 StandaloneInputModule；随包 EventSystem 仅有 EventSystem，`C/UI/InputSystemBridge.cs:139-162` 在 Both 模式优先新输入系统 | 缺失资产再生成会改变已验收的输入后端行为。生成与随包资产一样的中立 EventSystem，由现有 Runtime bridge 装配，不改 Runtime，也不替换用户已有输入模块 |
| `E/XFMain/Extensions/SerializedPropertyExtensions.cs:128` 对 null ObjectReference 调 ToString；Clipboard 三个入口 `:191-220` 依赖 Unity 私有方法且不核验存在/签名 | 修空值显示与版本失配的明确反馈；只保留现有能力，不写自有 Clipboard 协议。ApplyModifiedProperties 本身记录 Undo，不能因未显式 RecordObject 判定不支持 Undo；实际粘贴的 Undo/Prefab/多对象仍需定向验证 |
| `ProjectSettingTools.cs:9-20` 与 ProjectSettingsScriptableObject.cs:104-114 重复私有 SettingsWindow 反射 | 统一使用 Unity 2022.3 公开 SettingsService.OpenProjectSettings；不为两个薄入口再造管理器 |
| `E/Resource/ResServiceEditorAPI.cs:32-36` 递归清空工程根 AssetBundles | 本轮实际根目录已有 `AssetBundles/MainModule/`，调用会覆盖原游戏输出边界。必须先收窄到明确属于 YL 当前构建的目录，并确认；不能只加确认框继续整根删除 |
| `E/Resource/ResServiceEditorWindow.cs:95-102,127,174,234-238` 平台 Toolbar、版本输入不参与实际构建 | 平台改为显示实际 activeBuildTarget；移除无实际契约的版本输入，不建设新的热更新版本系统，不删除旧 EditorPrefs 数据 |
| `BuildScript.cs:37-78,103-106` 虽收 buildTarget，但目录按 activeBuildTarget 取名；平台不支持时可返回 null，随后还有替换/删除输出 | Editor 侧预检目标和路径，构建与路径使用同一目标；保留 Runtime 的现有平台契约，不改 AssetBundleHelper |
| `ResServiceAssetsMenu.cs:19-31` 从未实例化的私有构造函数接线，且用 `selectionChanged =`；`:40,63` 大小写规则不一致 | 以菜单校验刷新当前选择的状态，不覆盖全局订阅；统一 invariant 规则，实际验证标/取消/重新导入。大小写影响尚未运行复现 |
| `E/Resource/Data/SubProjectData.cs:35,67-73` 子串归属及 Bundle 跨目录分组；构建会逐个替换子工程输出 | 用完整目录边界检查。嵌套/重叠子工程或同 Bundle 跨 owner 必须预检报清单并停止，不默默多重归属；不改变 SubProject 序列化类型/字段 |
| `ResDataCodeGenerator.cs:28-108` 每个 Bundle 重写累计类列表，成员规范化又拼回原始后缀 | 多 Bundle 会重复定义类，非法/空名称可能生成坏 C# 或异常。保留并修复有价值的常量工具，拒绝规范化碰撞，不以未使用为由退役 |
| `BuildScript.cs:92-100` 直接 FileMode.Create，失败可能截断旧生成物；产物在 Assets/YLFrameworkData、无 asmdef | 先完整验证再更新、正确释放 writer、保留已有 meta。Assembly-CSharp 承载本身不是功能不存在的证据，但 asmdef 消费者不能直接引用，见 6.3 的承载规则 |

不扩大为本轮必修：EditorAssetTool 的 XFAB 查询/缓存策略属于资源专题；原扩展 Runtime→XF Editor 引用、Logger 全局分析器属于原生态边界。OpenFolder/日志清理保持小工具，不增加跨版本通用反射平台。

## 4. 推荐架构与使用入口

### 4.1 Editor 程序集

| 程序集 | 正式职责/依赖 | 本阶段变化 |
|---|---|---|
| YLFramework.Editor | 普通 Toolkit、Resource、Serialization、Action/Audio 编辑器；依赖 Runtime、Foundation、Help、UGUI | 保持结构、Editor-only；增加必要的通用使用导航，不引入 Core Runtime |
| YLFramework.Core.Editor | Module/默认资源/配置支撑、必要 Drawer；依赖 Core Runtime、Foundation、UGUI、临时 XFABManager | 保持结构、autoReferenced=false；为 Core 帮助提供方仅增加 Help 引用，不引用整体 Runtime/普通 Editor |
| YLFramework.Editor.Help | 无业务引用、显式注册的只读帮助核心 | 保持 references=[]、autoReferenced=false，不反向依赖提供方 |

不合并三程序集，不新造 Foundation.Editor/通用 Editor 服务层。现有 `XFMain` 路径是过渡承载，不代表需要重新统一 namespace/asmdef；有用的 Helpers 保留原正式 YL 身份。EditorAssetTool 及 Core.Editor→XFAB 过渡边仍保留，不借 Editor 阶段提前解决资源 owner。

### 4.2 使用体验与退出边界

- 项目资产创建继续使用 `Assets/Create/YLFramework/Core/Module`、原 Audio CreateAssetMenu、普通组件 Inspector；默认资源/Audio Debug/Action 创建器保持现有路径。
- `YLFramework/帮助` 作为阅读入口，不是大型管理中心。Core provider 稳定 id=`core`、菜单 `YLFramework/Core/帮助`，承载 Module/MVC/View/UI 最小接入、生成器路径、分析器规则与依赖边界；普通 Editor 的 `foundation-tools` provider 只补 Foundation/Tools/Attributes 的使用导航与精选 API，不复制全部说明。Action/Audio provider、id 和窗口行为保持。
- 配置与创建操作留在原所属工具，不集中进 Help；指南/代码不执行，不实例化 Module/Root/Audio 等宿主，不读取原游戏配置或扫描原包 API 充当正式类型。
- WindowCatalog、MVC、纯代码 Tools 不增加强制可视化配置。CustomPropertyGUI/CustomClassNameGUI 保留为显式扩展元数据，不追加全局 MonoBehaviour Inspector、属性注册表或 Odin-like 系统。
- 两种 GetName 的 SO 类型过滤/资产全局查重语义不同，不按名字机械合并；Handles、SplitWindow、TreeView 状态支撑保留。退出 YL 自有 `Excel2JsonTool` 的 error-obsolete 空壳及已无内容的旧 Inspector 目录；原 XF 对应文件不动。退出前定向核对反射/资源身份，配套处理自有 `.meta`，不批量清理未使用工具。
- 原 XF About/Expansion/旧 Audio Inspector 不重新加入。修复的是 YL Resource authoring，原 XFAB 管线、Bundle 命名/数据格式与游戏入口不迁移。

### 4.3 身份与配置保护

保留现有脚本/asmdef GUID、序列化字段、Settings 类型 FullName 和上述持久路径。Core Canvas GUID=`0df4ee5a6aa484a40b5d537a1c23a2c0`、EventSystem GUID=`e5f419ca4a6e564439be23e0257ffb3c` 不因生成器修复而重建；不编辑既有 Prefab。新资产由 Unity 导入生成独立身份，不复用原包 GUID。

普通 Inspector 的编辑继续经 SerializedObject/SerializedProperty；改变持久数据的操作具备真实 Undo/Prefab/多对象验证，或明确说明不支持的边界。创建持久文件、构建 Bundle、清理产物不伪装为可 Undo 的对象编辑：应使用预检、明确确认、失败不覆盖及准确范围。

## 5. 自有分析器设计

### 5.1 证据与边界

`XF/Editor/Analyzers/XFGameFrameworkCodeAnalyzer.dll.meta:2-19` 有 RoslynAnalyzer 标签、显式引用且普通平台禁用，但位于 XFGameFrameworkEditor asmdef 下。Unity 2022.3 官方作用域规则是所属程序集及引用者，不是同工程所有类型。现有 `YLFramework.Core.Runtime.csproj:47`/Core.Editor.csproj 等列有 Logger 分析器，未列原 XF 分析器；这些是生成快照，不是本轮实际诊断实验。Logger 分析器的内部规则未知，不能拿其全局注入证明六类 YL 标记已有等效覆盖。

自有标记使用点：`C/mvc/View.cs:141,234-370`、`mvc/Model.cs:8`、`Manager/EventManager.cs:25-243`、`Other/LoadViewArgs.cs:20-27`。六类定义、namespace `YLFramework.Core.RoslynAnalyzers` 及 GUID 原样保留。原 DLL 研究及证据层级见 [6.4 专题计划](xf-yl-6.4-analyzers.md#2-xf-真实能力与融合判断)；正式交付采用 YL 契约、自有源码和验证，不宣称逐位复刻或复制原 DLL。

### 5.2 源码、构建与 Unity 接入

- 自有源码项目：`Tools/YLFramework.Analyzers/Analyzer/YLFramework.Analyzers.csproj`；诊断测试项目：同根 `Tests/YLFramework.Analyzers.Tests.csproj`。这是手工维护的 SDK 工具项目，不是 Unity 生成工程，也不是新 UPM/插件安装系统。
- 按用户确认，仅给上述两个项目添加 `.gitignore` 精确例外；中间产物按项目隔离到既有忽略的 Temp 或工程外临时位置。不强制纳入 Unity 生成 csproj，不修改工具权限，不提交 NuGet 缓存。
- 分析器产物目标 `netstandard2.0`，锁定 `Microsoft.CodeAnalysis.CSharp 3.8.0` 作为已批准的 API 兼容起点，依赖设为工具侧私有；不把 Roslyn/NuGet/SDK DLL 复制入 Unity。本机 Unity 的实际宿主依赖为 4.3.1，不因宿主更高就升级工具侧 API。测试使用实际可用 SDK 的兼容承载，必须与被测分析器使用同版 Roslyn，并另外验证 Unity 真实加载。
- SDK 可用性本轮未核实，`dotnet --info` 被工具权限拒绝；不将拒绝解释为 SDK 缺失。6.4 前须只读确认工具链及 restore，缺失时报告，不自动安装/升级环境。
- 新 DLL 名为 `YLFramework.Analyzers.dll`，放 `C/Analyzers/`，由标签接入 Core.Runtime 及其引用者。不能放 Core.Editor 下而误以为会分析 Runtime/业务；DLL 普通 Editor/Player 平台均禁用、只作为编译器输入，不加入任何 Runtime asmdef 的 precompiledReferences。这只是为 Unity 作用域增加编译器资产，不改 Runtime C#、移动标记或改 Runtime 程序集。
- 仓库保留源码、版本锁定与构建入口；正常 UnityPackage 携带 DLL/meta/使用说明，不携带工具测试和 SDK。最终源码分发形态留阶段 7，本阶段必须保证从受管源码可重建产物。

### 5.3 诊断契约

按属性符号及程序集身份识别，不按短类型名、源码字符串或 namespace 前缀全局匹配。使用 ConstructorArguments 读取参数，不加载用户程序集执行 Attribute 构造。稳定基础 ID 为 `YL0001`～`YL0006`，首版默认 Warning；这是已批准的 YL 契约。警告不等于运行时安全证明，消费者可按标准 Roslyn 配置调整单 ID；细化语义与额外候选在 [6.4 专题计划](xf-yl-6.4-analyzers.md)维护，不在本文件展开。

| ID/标记 | 最小明确规则 | 必要合法场景/非承诺 |
|---|---|---|
| YL0001 MustCallBase | 检查带标记虚方法的 override 链，覆写方法本体须有绑定到基类被覆写方法的 base 调用 | 匿名函数/本地函数体内调用不算外层满足；不把同名其他方法算作 base，不承诺全路径/跨辅助方法证明 |
| YL0002 RestrictedCall | 标记方法仅允许指定名称的直接调用方法；支持 string/string[]、泛型/重载符号 | 局部函数/匿名函数是独立调用体，不因词法包在 Awake 内而豁免；不做跨过程调用图推断 |
| YL0003 WarnInClasses | 标记方法在指定类型或其派生类中被调用时报告，支持 Type/Type[]、AllowMultiple 及消息 | 直接消费者类型以符号判定，正常非 View 调用不报；不是事件 owner/释放运行时验证 |
| YL0004 DisableNew | 外部直接构造标记类型时报告，覆盖普通 new 与目标类型 new；合法工厂调用不报 | 标记类型自身的实现可构造自身，保留 `LoadViewArgs.cs:26-27` 的池工厂；不禁止其合法静态 New，不声称拦截反射 |
| YL0005 MethodLocalOnly | 报告字段/属性持有标记类型，含数组和实际容器类型参数；方法参数/返回/局部变量允许 | 委托签名不等于持有参数；不推断 object/闭包运行时逃逸。仅明确允许标记类型自己的池以及同一 Core 程序集中 `LoadViewRequest<T>.args` 的内部事务 owner，不整体豁免 Core namespace/程序集 |
| YL0006 ProtectedHierarchy | 指定禁止修改者类型及其派生类，直接写受保护实例的字段/属性时报；覆盖赋值、复合赋值、增减及可写 ref/out | 正常读取、非禁止类型的修改不报；不声称检查集合内部 mutation、反射或所有别名/跨过程副作用 |

六类均需正反例与消息/位置断言；继承、泛型、别名、同短名不同符号是跨规则直接风险，不机械堆用例。实际 Runtime 内部 owner 和既有负例测试须显式核对，不能通过改 Runtime、删测试、放宽断言或全程序集排除处理误报。暂不做 CodeFix、自动改代码、全项目规则配置窗口。

## 6. 实施批次与最小门禁

顺序体现依赖和可独立拒绝的风险，不按目录机械拆分；6.4 可在前两批稳定后独立推进，6.5 收口全部成果。任何批次都需要届时基线复核、单独实施授权和 diff/meta 检查。

### 6.1 Core Editor 支撑与默认资源一致性

**目标/前置：** 以已验收 Core/UI 契约为准，修生成资源输入策略、空引用显示、Clipboard 反射边界与重复 Settings 私有机制；保留成熟 Module 生成器和 Settings 持久契约。清退出无功能的自有 Excel 空壳/空目录。

**退出机制：** 硬编码 Legacy 默认模块、重复 SettingsWindow 私有调用；不引入新输入宿主、Clipboard 协议或统一 Editor 服务。

**门禁：** Unity 编译；定向 CoreDefaultResourceEditorTests + CoreDefaultResourceTests，测试只创建独立临时资源，验证中立 EventSystem 经现有 bridge 得到正确输入后端，不删除正式默认资产来测试；CoreEditorConfigurationTests、相关 CoreEditorIdentityTests。Clipboard 增补 null 引用、版本不可用反馈以及一次真实 Undo/Redo、Prefab override、混值多对象粘贴（若不能可靠支持则明确拒绝，不部分应用）。Drawer 高度/禁用/GUI 恢复复用 DisableEditDrawerTests；模块生成器未改模板不重跑整个生成套件。

#### 6.1 实施记录（2026-10-12）

- 基线：`fusion/6-editor`、HEAD `7db580635661165acb42826d3a66f885f9809930`，开始时工作区干净；`git ls-remote` 确认远端 `fusion/6-editor` 与 `integration/xf-yl` 同提交。未创建或切换分支，未 Commit/Push/Merge/Tag/Reset/Clean。
- 默认资源：只从 `CreateEventSystem` 移除硬编码 StandaloneInputModule，生成与正式随包 Prefab 一致的中立 EventSystem。临时独立 Prefab 验证现有 Runtime bridge 在本工程 Both 环境选择 InputSystemUIInputModule、重复调用不追加模块；不改 Runtime，不改现有生成保护与删除保护。
- 资产保护：未删除、重建或编辑正式 Canvas/EventSystem；重复生成与实际延迟初始化验证其文件和 `.meta` 字节、时间戳均不变。两者 GUID 仍为 `0df4ee5a6aa484a40b5d537a1c23a2c0` / `e5f419ca4a6e564439be23e0257ffb3c`。临时工厂对象保持 inactive，未接管外部 EventSystem；测试后活动场景仍为 `Assets/Scenes/Main.unity`、3 个根对象、dirty=false。
- SerializedProperty/Clipboard：空 ObjectReference 与 ExposedReference 显示 `None`；保留现有三个 Clipboard API。原生成员只从 Unity Editor 程序集按精确参数/返回类型解析，缺失/失配抛带版本和成员的 NotSupportedException。使用 Unity 自身 Clipboard/JSON 编解码在独立 SerializedObject 缓冲中试粘贴，核对完整 round-trip 后仅复制目标属性并 ApplyModifiedProperties；不建立自定义 Clipboard 协议，不克隆/实例化用户目标，不应用失败操作或调用者待修改数据。
- 支持边界：同类型、非混值的单/多对象标量、结构、数组及兼容引用保持可用；混值、不同类型多对象、不可编辑目标、损坏/不兼容内容，以及原生不可靠的 ManagedReference/ExposedReference/FixedBufferSize/double 明确拒绝。实际 Copy/Paste 的 InvalidOperationException 反馈原因，IsCanPaste 对不兼容输入返回 false；版本 API 不可用则明确抛 NotSupportedException。
- Settings：`ProjectSettingTools.OpenProjectSettings` 使用公开 `SettingsService.OpenProjectSettings`，Settings SO 实例入口复用前者，重复私有 SettingsWindow 反射退出。SO 类型、FullName 文件名、`ProjectSettings/YLFramework/Core/{FullName}.asset`、创建/保存/不兼容保护未改；没有新增 SettingsProvider 或配置中心。真实窗口测试验证两个入口分别选中原生设置页、复用同一窗口、不创建 Settings 配置，恢复既有窗口选择/相关偏好与 focus。
- 旧空壳：核实自有 Excel2JsonTool 两个 error-obsolete 入口均为空实现，代码/动态名称和脚本/目录 GUID 无有效消费者后，删除自有脚本及 `.meta`；通过 AssetDatabase 删除已空的 `Tools/excel2json` 和旧 `Inspector` 目录及各自 `.meta`。原 XF/ExcelSystem Package、成熟 Editor 工具及来源许可证均保留。
- 本轮实际验证：Unity `2022.3.62f3c1` 导入/脚本编译通过，最终 `isCompiling=false`、`scriptCompilationFailed=false`、`isUpdating=false`。定向 EditMode 共 **69 项通过、0 失败**：CoreDefaultResourceEditorTests 26、CoreDefaultResourceTests 4、CoreEditorConfigurationTests 14、CoreEditorIdentityTests 8、DisableEditDrawerTests 3、新增 CoreSerializedPropertyTests 14。数量按实际 PassedTests/单项结果计，不使用 MCP 的全工程发现总数冒充执行数量。
- 真实行为证据：Clipboard 原缺陷回归先观察到空引用异常、混值复制未拒绝、错误类型及损坏内容仍可粘贴的失败；默认工厂观察到多余 Legacy 模块失败，修复后通过。验证了实际 Undo/Redo、临时 Prefab instance override 的撤销/重做与原 Prefab 字节/GUID 不变、同值多对象标量/结构/数组编辑及 Undo、混值零写入拒绝、兼容/不兼容 ObjectReference、结构/数组完整粘贴、保留待应用修改与系统 Clipboard、缺失类型/错误签名的确定性反馈。
- 独立审查与修正：未发现 P1/P2 产品问题，指出一项 Settings 测试延迟 focus 清理问题。实际执行本次设置操作新增的原生回调，确认同步恢复的 SceneView 焦点会再次被覆盖；修正测试只摘下并执行自己调用产生的原生 Show focus 回调，再恢复/关闭自己拥有的窗口与原焦点，不清空或调用其他全局回调。配置 14 项重新通过；已有 Settings 窗口且焦点在 SceneView 的分支另行确认测试后仍为 SceneView、残留 Settings focus 回调为 0，最后仅清理探针自建窗口。
- 隔离/验证限制：三个现有 Editor asmdef、Runtime 与公开类型/序列化身份不变；身份测试通过，Unity Player 编译程序集图无自有 Editor 程序集或 Editor 依赖边。未修改原游戏/正式 Scene/Prefab/Packages/ProjectSettings 配置或 Bundle。未重跑 Module/UI/Audio/Foundation 全集，未运行 Player 构建或阶段 6 综合验收；没有切换输入模式或在其他 Unity 版本执行，私有 Clipboard API 的其他版本仅由精确签名门禁明确拒绝，不承诺跨版本成功。
- 交接：6.1 定向技术门禁已满足；后续用户确认已完成验收及 Git 收口，6.2 起始提交为 `5781c2e`。阶段 6 整体保持 IN PROGRESS，后续批次仍按单独授权推进。

### 6.2 自有 Resource 操作安全与真实界面

**目标/前置：** 保留标记→YL 构建→ResData 的旧流程，先把路径/归属与破坏性操作做安全，再提供可信窗口。这里不融合 XFAB，不构建或清理实际游戏 Bundle。

**退出机制：** 整根清空、无效平台 Toolbar/版本输入、无效 Selection 构造接线、子串/多 owner 隐式归属。

**方案：** 清空与替换只针对当前目标的明确 YL 默认/子工程产物目录，保留父目录及未知兄弟目录；确认展示实际路径，拒绝越界/根目录/重叠 owner，取消不产生副作用。BuildTarget 和路径名同源，所有预检在 BuildPipeline/删除之前；构建失败不得替换旧可用产物。窗口显示 activeBuildTarget 并引导 Unity Build Settings，不建设自己的平台切换器。标记支持既有单选契约，修改 importer 应具备 Undo 或明确的可逆操作与保存/重导入验证。

**门禁：** 自有临时资产/目录中的标记→取消→重导入、菜单状态、不支持目标、路径越界、子工程边界/跨 owner、取消清理、清理保留无关兄弟目录；必要 importer Undo/Redo。新增测试只锁真实缺陷。一次隔离的小 Bundle 构建/数据表读取验证，可用临时输出承载，不写原 AssetBundles/MainModule 或 StreamingAssets/MainModule；窗口真实 GUI 冒烟、关闭后无重复事件订阅。现有 Resource Runtime 回归只选数据表/路径直接消费者。

#### 6.2 实施记录（2026-10-13）

- 基线：`fusion/6-editor`、HEAD `5781c2ed639633f1654ef90f281db8f2cb8ff47b`，开始时工作区干净；重新读取工程/global AGENTS、Unity `2022.3.62f3c1` 与 manifest。只研究自有 Resource Editor、实际标记/构建/数据表和直接 Runtime 路径消费者，没有重新研究或改造 Resource 架构。
- 安全归属：新增普通 Editor 内部 `ResourceBuildOperation`，不改变 Runtime API/asmdef 或 SubProject 序列化身份。来源按 `Path.GetDirectoryName(assetPath)` 的完整边界分组，重复分组重新计算；同目录/嵌套目录、大小写不敏感的同输出名、同 Bundle 跨 owner、Bundle 依赖跨 owner、Bundle 文件/目录及 manifest/数据表/归属文件名称冲突均在构建前拒绝。
- 路径/预检：BuildTarget 与目录映射同源，非当前已激活/已安装平台和播放/编译/导入状态拒绝构建。保留默认 `AssetBundles/<Platform>` → `StreamingAssets/AssetBundles/<Platform>`、子工程 `AssetBundles/<Name>/<Platform>` → `StreamingAssets/<Name>/AssetBundles/<Platform>`。清理只含当前平台叶目录；不清空 AssetBundles、StreamingAssets 或子工程父容器。
- 既有目录保护：平台叶目录须有本工具生成的 `.yl-resource-output.json`，校验工程路径、默认身份/子工程 GUID、平台及完整文件清单；未知目录、owner 冲突、缺文件、额外文件/空目录、孤立 meta、symlink/junction、根/父目录/越界/重叠目标均安全拒绝。标记来源、SubProject 及显式 Bundle 资产也拒绝链接越界。子工程容器已有未知内容时不接管；共享根下无关兄弟目录不作为清理对象。没有自动认领旧 YL/XFAB 或 MainModule 产物的迁移机制。
- 事务边界：确认展示实际平台、Bundle 数及绝对路径，取消前不创建目录、不写 importer、不调用 BuildPipeline。全部 owner 的非空显式 map 用 ChunkBasedCompression + StrictMode 在 `Temp/YLResourceBuild/<transaction>/<group>/<Platform>` 构建，检查 manifest/文件，按该 manifest 的直接依赖写现有 ResData 二进制格式并回读，全部成功后才发布两套输出。原生返回 null/异常及数据表失败不发布；文件系统发布/清理失败回滚。回滚也失败时保留备份并报告位置；不宣称断电/崩溃级多目录原子性。
- 资产 metadata/源一致性：再次发布为仍存在的文件/目录保留原 `.meta`。实际验证发现叶目录孤立 `.meta` 会使 Unity 重建空目录，修正为清理时一起备份/删除、失败时一起恢复。产物记录 Bundle 分配、资产路径/类型、Unity GetAssetDependencyHash（包含源文件、meta、平台和 importer）及 Bundle 依赖的指纹；构建期间变化则拒绝发布。独立 `BuildDataTable` 只允许当前平台已构建的已知 owner 输出，并核对已发布源指纹，资产重命名/标记/内容/依赖变更后要求重新构建，保留旧表。通过校验后写临时文件并完整回读再替换，默认路径修正到平台层；不提供任意路径写入或未知目录认领。
- 标记/窗口：菜单以 validator 更新当前单选及 checked 状态，不再从未实例化的构造函数接线、不覆盖全局 Selection 订阅；名称规则统一 invariant，已有不同 Bundle/variant 拒绝覆盖。标记/取消立即 SaveAndReimport，两次操作互为可逆，不承诺 importer Undo。窗口显示真实 activeBuildTarget、打开原生 Build Settings、显示输出预检与操作结果；保留模拟模式、常量入口、资源选择/取消。退出无效平台 Toolbar/版本输入但不删除旧偏好，构建不自动关闭窗口，关闭只撤销自己的待执行回调并解除自己的订阅。
- 本轮实际验证：Unity 导入/脚本编译通过，最终定向 EditMode `testNamespace=YLFramework.Tests.ResourceEditor` **30 项通过、0 失败、0 跳过**（按 PassedTests/单项结果，而非 MCP 的全工程发现总数）。覆盖归属/路径/名称拒绝、未知产物保留、取消无文件及时间戳改变、null/throwing build 保留旧输出、后一个清理失败恢复前一个目标、叶目录 meta 移动失败恢复、菜单单选/多选/check 更新、文化无关标记、保存/重导入与拒绝覆盖。实际 Windows junction 分别验证标记来源、输出祖先和输出内嵌链接拒绝，测试先移除自己的链接再清理自己的 target，不递归触及外部目录。真实窗口经 Layout/Repaint 和 SendEvent 点击“选择”/“取消”，重导入后仍取消，关闭无自己的 Selection 订阅或 delayCall。
- 小型闭环：仅用自建临时 `.bytes` TextAsset、独立 SubProject；运行前拒绝既有 Bundle 标记/SubProject，默认输出和唯一子工程输出明确不存在才允许测试创建。每次构建确认及清理前核验完整计划的四个路径和三个源资产白名单，不允许额外 owner（即使当前无 Bundle）。一次构建操作的两个原生 map 生成三个小 Bundle，读取两个 StreamingAssets 平台目录的 ResData、核验多 Bundle 组内 AbIndex、实际 AssetBundle.LoadAsset<TextAsset>；复用本次产物/manifest 验证后一个 owner 失败零发布、再次发布 metadata 保留、实际文件锁造成的中途发布失败完整回滚、独立表更新/资产重命名后拒绝更新及最终清理，不为事务用例重复原生构建。回归调试期间按失败证据重跑过此小型测试，未运行 Player 构建。
- 验证隔离：初轮旧 MarkAB 无效路径探针产生了 Assets 根 importer 与 Package 文本 importer 的未保存 Bundle 名，立即撤销并核对 Bundle 名列表恢复为空，没有改写原资源/Package 文件。后续拒绝用例使用纯 validator 或自有临时路径。临时 `.txt` 曾触发既有 VS Editor 项目生成与外部写者的 `.csproj` Sharing violation；定位官方包同步入口后改用同语义 `.bytes`，最终无该失败，不改 ProjectSettings/Packages、不跳过日志或禁用诊断。
- 独立审查与修正：只读审查发现 P1 测试全工程入口可能夹带其他 owner、P2 独立数据表可能以新源改写旧 Bundle 配置。前者新增执行前完整计划硬隔离，纯临时事务计划验证额外无 Bundle owner/外部源均拒绝；后者先观察到资产重命名后更新未拒绝的失败，再加入构建源指纹，旧表字节保持。补足 junction、中途发布失败及叶 meta 移动失败风险验证；最终 30 项全绿。原初轮运行的 owner/Bundle 名为空，897 文件指纹不变，不把潜在误删问题描述为本轮已发生的损失。
- 保护证据：开始/结束只读核对 `AssetBundles/MainModule` 与 `Assets/StreamingAssets/MainModule` **897 个文件**的 SHA-256、大小及最后写入时间，完全一致。最终两输出根只剩原 MainModule；Bundle 标记/子工程资产/临时测试资产/事务 workspace 均无残留，场景 dirty=false，`isCompiling=false`、`scriptCompilationFailed=false`、`isUpdating=false`。原游戏、XF/XFAB/XFFSM Package、Runtime、其他 Editor 模块和现有 asmdef 均未修改。
- 剩余边界：旧/未知目录、移动工程路径或变更 SubProject 输出名/GUID 后的旧产物只拒绝，不自动迁移。仅 Windows 实际原生 Bundle/GUI 闭环，其他平台只做目录映射与预检验证；未重跑未受影响的 Resource Runtime 全集或阶段 6 Player 综合门禁。既有资源常量生成器缺陷仍交 6.3，自动常量结果与 Bundle 发布结果分开报告，本批未修其完整可靠性或生成程序集承载。
- 交接：6.2 定向技术完成条件已满足；后续用户确认已完成验收及 Git 收口，6.3 起始提交为 `b0a050c`。6.2 实施轮本身未 Commit/Push/Merge/Tag/Reset/Clean/切换分支。阶段 6 保持 IN PROGRESS，后续批次仍按单独授权推进。

### 6.3 资源常量生成可靠性与消费者承载

**目标/前置：** 依赖 6.2 的有效分组/标记快照，保留工具而不是删除未使用功能。一次收集、一次生成；常量值保持真实资源键，符号完整规范化，空/非法输入和类/成员碰撞明确失败，不静默丢资源。

**退出机制：** 累计 CompileUnit 重复输出、拼回未清洗后缀、失败截断旧文件，以及“已生成就可被任意 asmdef 使用”的隐含假设。

**方案：** 保留既有 `Assets/YLFrameworkData/YLResData.cs` 路径/namespace；有存量时不搬、不覆盖手写文件、不改变 meta。当前无已查实生成物，新输出作为消费者源码，不放回 Framework Runtime，也不纳入默认导出。空承载目录可生成独立 `YLFramework.Generated.ResourceNames` asmdef、无 Runtime/Editor 依赖，预定义程序集可自动引用，业务 asmdef 显式引用；若目录已有源码/asmdef，先报告并由消费者确定承载，不能对未知业务文件批量改变程序集身份。不写泛用代码生成框架。

**门禁：** 临时多 Bundle 生成类唯一、合法 C#、稳定重复生成、真实键值、规范化碰撞/空输入失败前不覆盖；writer 释放、既有 GUID 保留。用实际 Unity 编译的临时 consumer asmdef 引用产物；不以文本/正则检查代替生成源码编译。未改变的 Module 生成器只做必要同工程冒烟，不重复全部 Core 回归。

#### 6.3 实施记录（2026-10-13）

- 基线：`fusion/6-editor`、HEAD `b0a050cf7aecbfdedfe32d03784f929626a1aa3f`；开始工作区干净，`git ls-remote` 确认远端阶段分支同提交，远端 `integration/xf-yl` 为 `7db5806`，未擅自同步/合并。重新读取 AGENTS、批准方案、Unity `2022.3.62f3c1` 与 manifest，未改变环境配置。
- 缺陷与修复：实际先观察到四项旧缺陷回归失败：两个 Bundle 输出三个类、`hero-icon` 成员非法、`hero.icon` 原始键被二次截断、重复成员静默丢失。保留 CodeDOM 与公开 `WriteClass(TextWriter, string)`，一次收集、完整校验、单次生成后才写 writer；provider 和内部 writer 正确释放，外部 writer 仍由调用方拥有。按 ordinal 排序和 invariant 标识符生成，原 Bundle 名/一次去扩展名的资源键原样作为常量值。
- 名称与输入契约：完整符号中的非法字符替换为下划线、数字开头加下划线；拒绝空/不可用名称、无有效 Bundle、空资源清单、非法 Bundle/namespace、Bundle 类与成员规范化碰撞、保留的 `BundleName`/所属类名冲突、同 Bundle 的 runtime 小写键碰撞，以及与已加载类型/namespace 的身份冲突。不静默覆盖或丢资源，不修改 Resource Runtime 的查询、数据表格式或公共 API。
- 生成承载：实际确认 `Assets/YLFrameworkData`、源码及 `.meta` 均不存在，Assets 根无上级 asmdef/asmref，也没有已查实的生成常量/反射消费者。因此首次仅在空承载创建 `YLResData.cs` 与 `YLFramework.Generated.ResourceNames.asmdef`，namespace 仍为 `YLFramework.Services.Resource`。新程序集无 Framework、Editor、UnityEngine 或插件依赖，autoReferenced=true；预定义程序集自动引用，业务 asmdef 显式引用（名称或 GUID）。不改已有消费者程序集身份，不往框架 Runtime 放生成物，默认 UnityPackage 六根清单保持不变、不携带该项目生成目录。
- 写入与身份保护：新增窄内部 `ResReferenceOutput`，全量验证/生成先于文件操作，同目录暂存后 File.Replace/File.Move 发布；相同源码不重写，已有源码/asmdef/目录 `.meta` 不删除或重建。源码内容校验标记防止覆盖手写/修改过的文件；未知资产、旧或不完整承载、未知上级程序集、孤立 metadata、链接及其他位置的同名 asmdef 明确拒绝。asmdef 身份通过 AssetDatabase 读取，不依赖 Package 虚拟路径的物理布局。写入失败释放句柄、移除自己的暂存与首次创建但未完成发布的 asmdef。
- 与 6.2 协作：单独生成直接复用 `SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas` 的来源/Bundle/子工程/依赖归属安全预检，不操作 Bundle 输出；自动生成保留构建确认/事务路径，在成功发布后核验源指纹并使用同次计划中的所有 owner map，不再次随意扫描得出不同清单。Bundle 发布成功与常量失败分别报告，失败保留旧常量。跨 owner 的生成入口拒绝已有真实回归；分组、标记、资源身份与现有 MainModule/XFAB 管线未改。
- 本轮实际验证：Unity 导入及脚本编译通过；`ResourceCodeGenerationTests` **22 项通过、0 失败**，覆盖多 Bundle/排序文化无关/真实键值、非法与空输入/类成员/runtime 键/namespace 碰撞、失败不覆盖、未知源码/asmdef与手写修改/孤立 meta 拒绝、同源码时间戳不变、实际 Windows 文件锁失败及更新后 metadata 保留。独立审查提出空集合、runtime 小写键和 namespace 缺口，三项先实际失败后修复并全绿；Package 路径的故障推断在当前环境未复现，仍改为资产 API 读取避免物理路径假设。
- 实际消费者编译门禁：新增一个窄 `ResourceCodeCompilationTests` UnityTest，**1 项通过、0 失败**；仅在无外部 Bundle 标记/SubProject、固定生成目标及唯一临时根预先不存在时运行。自建默认/子工程 TextAsset，三个 Bundle（含点、连字符、数字开头和 keyword-like 名称）通过正式入口生成真实源码；实际 Unity csc/domain reload 后核对三个类型及常量值，并编译和调用显式引用生成程序集且同时引用 Runtime 的隔离业务 asmdef，以及自动引用的 `Assembly-CSharp` 消费者。验证生成程序集图无 Framework/Editor/Engine 依赖；实际资源碰撞和文件锁均保留旧源码，更新保留源码/asmdef/目录真实 `.meta` 字节及 GUID，再次编译可读新增常量；清理临时消费者、源资产、SubProject与生成目录后再次实际编译通过。初轮仅因 GetAssemblyNameFromScriptPath 返回 `.dll` 后缀的测试断言失败，修正断言后完整门禁通过，不以生成文本替代编译。
- 编译探针恢复保护：最终只读复核发现测试在清理后的编译等待期间中断会残留 SessionState；修正为根目录消失时清除旧恢复状态并重新执行完整前置检查，操作/清理前校验自建目录及固定输出的完整文件与 metadata 指纹，并拒绝夹带外部标记/owner。不凭旧路径字符串清理后来创建或被外部修改的目录。新增中断后输出重建/修改的独立回归 **1 项通过**，修正后的真实编译闭环重新通过并清理；本批共取得 **24 项独立定向通过结果**（22 生成、1 编译、1 恢复保护），不使用 MCP 全工程发现总数或跨 domain reload 失真的总时长冒充执行统计。
- 编译证据增强与诊断工具差异：消费者 asmdef 改为每轮唯一身份，并断言生成/业务/预定义程序集的实际 sourceFiles 精确指向本轮源码；首轮编译必须没有 `Extra`，更新后重新编译必须出现该常量，排除复用旧消费者或旧生成 DLL 的假通过。添加这些断言时曾触发 UnityTest 对跨 reload 闭包的恢复异常，去掉迭代器中的捕获闭包后完整编译门禁再次通过。MCP 原始日志仍反复返回已删除旧探针路径的同名 asmdef 诊断，日志缓存清空工具还因其缓存文件被占用失败；未把这类返回值直接认作当前编译状态。分别核实当前 Unity 编译器消息数=0、程序集配置异常=0、原生 Console error/warning/log 均=0，旧探针在资产索引/编译图/文件系统均=0，唯一消费者与新源码的实际编译及调用通过。本轮曾通过 Unity 公共 API 仅重建脚本编译缓存（CleanBuildCache），没有清空 Library、全量 Reimport、重启 Editor 或修改 MCP/Packages；MCP 这类日志一致性问题的根因未最终确认，不宣称原始 MCP 日志已净空，也不扩大本批修复范围。
- 保护与影响核查：开始/结束核对 MainModule 两输出目录 **897 个文件**的 SHA-256、大小和最后写入时间完全一致。最终 Bundle 标记/SubProject/临时源码与生成目录均无残留，活动场景仍为 `Assets/Scenes/Main.unity`、dirty=false，`isCompiling=false`、`scriptCompilationFailed=false`、`isUpdating=false`。既有脚本/asmdef `.meta` 未修改；新增工具/测试 `.meta` 均由 Unity 导入生成。未修改原游戏、Prefab/Scene、XF/XFAB/XFFSM Package、Packages、ProjectSettings、Runtime、Module 创建器与默认导出清单。Module 只核对不交叉的输出路径/程序集/引用契约并随工程编译，没有重跑完整生成测试。
- 证据复用与限制：复用 6.2 已验收的标记、分组、构建事务及实际 Bundle/ResData 闭环，不机械重跑 Resource/Core/Audio/UI/Editor 全集，不运行 Player 构建或最终综合验收。新生成目录是工具专用承载，不自动迁移已有/手写目录；符号检查限当前 Editor 已加载类型，后续新类型或未成功编译的消费者以 Unity 实际诊断为准。首次源码/asmdef 双文件发布不承诺断电/崩溃级原子性，不协调外部并发写者；其他平台文件替换行为未实际运行。
- 交接：6.3 定向技术完成条件已满足，具备进入 6.4 的技术基础；等待用户验收与 Git 收口。本轮未 Commit/Push/Merge/Tag/Reset/Clean/切换分支，未实施 6.4/6.5，阶段 6 整体保持 IN PROGRESS。
- 后续基线核查：6.4 Plan 开始时，实际 HEAD 与远端 `fusion/6-editor` 均为 `8065d65c4dddd2b0aebfa43f86f9a3222e16589d`，提交说明为“完成6.3资源常量生成与程序集承载修复”，主工作区及暂存区干净。上述“等待 Git 收口”保留为 6.3 实施轮历史，不再作为 6.4 阻塞；不推定已经合并 `integration/xf-yl`。

### 6.4 自有分析器源码化与接入

**目标/前置：** 在第 5 节六规则契约基础上交付自有、准确、可维护的 Analyzer；吸收 XF 的有效设计，补齐真正的语义覆盖与开发体验，按证据而非数量决定额外规则。受管 SDK 项目、可复现 build/test 与 Core 作用域 DLL 是正式交付；先过 G0，再实现，不移植原 DLL、不使用 Logger 冒充。

**退出机制：** “标记存在即已有静态强制”的错误表述；不删除标记、不修改已验收的行为契约。

**门禁：** 工具侧 build + 诊断测试，包括每个 ID 的合法/非法、必要符号边界与两个实际内部 owner；从源码重建产物。Unity 实际重编译验证正例无误报、负例产生规定 ID/位置/Warning，无 analyzer-load/AD0001；探针使用自有临时消费者，不把违规脚本留在工程。元数据核验标签/GUID/普通平台禁用，Core 与无关程序集的实际作用域对照；日志/诊断须区分自有 YL、原 XF、原 Logger。工具测试通过不等于 Unity 兼容通过；最终 Player 隔离在 6.5 验证。

**专题入口：** 原版研究、六规则细化、额外候选、SDK/Unity 验证顺序及剩余证据统一维护于 [xf-yl-6.4-analyzers.md](xf-yl-6.4-analyzers.md)。本节仅保留阶段导航，基础契约仍见第 5.3 节，不在两个文件复制详细记录。

### 6.5 帮助、导出与阶段收口

**目标/前置：** 前四批稳定；复用 HelpRegistry/HelpWindow，接入 Core 与通用工具的少量指南/精选 API，保持既有 Action/Audio 内容与独立帮助核心；准确交代依赖、创建路径、分析器诊断/重建方法和生成源码消费方式。

**退出机制：** 同职责的新增 About/Help 实现、虚假的自动 Attribute/Analyzer 功能说明；不是退出已有成熟 Help 或商业插件。

**门禁：** 新提供方注册/重复注册、搜索、关键指南/API、缺 XML 时仍显示声明、窗口真实 GUI/只读无宿主创建。Core.Editor→Help 的新增引用须定向更新 CoreEditorIdentityTests 的显式白名单，只允许 Help，继续禁止整个 YLFramework.Editor/Runtime、原 XF 与游戏依赖；HelpWindowTests 的无依赖约束保持原样。Action/Audio provider 仅在公共 Help 协议变动时做直接回归，不默认重跑其全集。

复用 ExportUnityPackageTests 的真实包解包校验，增加新 provider/Analyzer DLL 与 meta 哨兵，不弱化既有 GUID/meta、退役入口、Tests/Integrations 排除断言。由于新增编译器输入，最终安排一次既有独立验收承载的 Windows Development/Mono Player 构建与最小启动冒烟，确认没有自有 Editor/Analyzer/Roslyn 运行引用；不默认执行 5.5 综合流程、原游戏综合验收或 IL2CPP。任何需临时状态的旧验收 driver 先读其前置授权，不能自动沿用 Logger 隔离。纯 Editor 批次不逐批构建 Player。

#### 6.5 实施与验收记录（2026-10-13，技术门禁完成）

- **实际基线：** 本批开始时 `fusion/6-editor` 的 HEAD / 远端同为 `bc3db76728b710cae01b13b1fc225247990f8978`。用户完成并行 Analyzer 验收/提交后，恢复时重新核实 HEAD / 实际远端同为 **`28dbb576fda4b1f79b189b9eec94c15e3469903f`**，远端 `integration/xf-yl` 仍为 `7db580635661165acb42826d3a66f885f9809930`。工作区 DLL、meta、SDK 源码与新提交一致。已有 `.gitignore`、`.kilo/kilo.jsonc` 修改原样保护；并行 test-worktree 仅只读核实。本代理没有 Commit/Push/Merge/Tag/Reset/Clean/切分支。
- **Help 实现：** `Editor/XFMain/CoreHelpProvider.cs` 注册 `core`，菜单 `YLFramework/Core/帮助`，四篇指南涵盖 Module/MVC、View/UI、生成器及 YL0001～YL0007 的默认 Warning、实际 asmdef 作用域、单 ID 配置限制与 SDK 源码重建。`Editor/FoundationToolsHelpProvider.cs` 注册 `foundation-tools`，菜单 `YLFramework/Toolkit/帮助`，三篇指南提供 Foundation/Tools/Attributes 导航与精选真实 API。只读取元数据和指南，不执行示例或实例化宿主；缺 XML 保留真实声明。Action/Audio Provider、Help 核心及公共协议未改。
- **程序集：** 普通 `YLFramework.Editor` 继续引用 Foundation/Runtime/Help/UGUI，不引用 Core Runtime；`YLFramework.Core.Editor` 仅新增单向 `YLFramework.Editor.Help` 引用，保留 Core/Foundation/XFAB/UGUI；Help 显式 references 仍为 `[]`。CoreEditorIdentityTests 的编译引用与 asmdef 白名单只豁免准确的 Help 身份，不放宽其他 `YLFramework.Editor.*`、整体 Runtime、原 XF 或游戏依赖。
- **测试证据：** 新 Provider 测试先出现预期的缺注册失败，再完成实现。独立审查发现重复注册刷新可能读旧内容且污染正式注册项；新增注册身份断言实测失败后，改用唯一临时 id、可区分替换内容及工厂调用计数，finally 注销。新 DLL 基线下实际重新执行 Help **52/52**、CoreEditorIdentityTests **8/8**、ExportUnityPackageTests **1/1**，共 **61 个不同 EditMode 用例**、0 失败/跳过，不将工具总发现量或重复执行次数冒充测试数量。Help 包含真实 ShowUtility/Repaint、MouseDown/MouseUp 页签与清空、搜索和高亮、缺 XML、宿主/场景/输出文件只读、事件释放。没有机械重跑 6.1～6.4 全集；新提交记载的 SDK121/Core59/Play185 为用户确认及提交记录中的既有证据，并非本代理本轮重跑。
- **真实导出门禁与产物：** 保留 ExportUnityPackage 的六根与 Recurse，不改产品导出实现。既有真实导出/解包测试增强为全部递归资产的内容 SHA-256、完整 meta 字节/GUID、Plugins 全量等价、新 Provider/生成器/Analyzer 哨兵、唯一 DLL、RoslynAnalyzer 标签、显式引用及 Any/Editor/Windows64 禁用配置；Tests、根 Integrations、Examples/YLFrameworkData、游戏/Bundle、SDK/NuGet/缓存/中间产物均排除。正式产物复用同一 ExportRoots/Unity ExportPackage API，直接写工程外目录，不调用菜单的全局 SaveAssets 或剪贴板/打开目录副作用。对**留存的真实包再次解包**：752 条记录（586 文件、166 目录），Plugins 114 条；全部递归内容哈希、meta 字节/GUID 与源资产一致，Analyzer 唯一且为新提交字节。文件 **`YLFramework-6.5.unitypackage`**，10635755 bytes，SHA-256 **`5211315324E1CDECF7B5D37DE69E03FC2BCFF3B0037FA0E5B60E40E7A0CF9FB7`**。完整 Plugins 是内部交付，不代表公开再分发许可；UGUI/Odin/Newtonsoft/Core 过渡 XFAB 边界未改，不宣称干净工程零依赖导入。
- **实际 Player：** 新 DLL 后重新使用既有 `Tests/Core/Acceptance/UIAcceptance.unity`，显式 scenes + StandaloneWindows64 + Development，沿用 Mono2x，不改 Build Settings/ProjectSettings、不调用 RuntimeAcceptanceDriver、不加载 `-ui34-game`、不重建游戏资源链。新基线实际构建 Succeeded，optionsNumeric=1 / development=True，0 Error、**4 条既有 MCP 空程序集 Warning**，用时 9.41 秒，148374220 bytes。这是正常增量构建，未清 Library；不将本次警告较少描述为修复旧源码/XML 警告。新 Player 真实窗口启动出现 `UI34 READY round=1`、`UI34 PLAYER_PASS`、`UI34 QUIT_PENDING_PASS`，显式捕获进程退出码 **0**。日志包含原 `XFGameFramework.LoggerSystem.LoggerHandler` 调用栈及 Mono 启动路径，未隔离 Logger；旧 DLL 的先前 Player 结果不替代本次验证。
- **Player 隔离：** 实际 Player 编译图 67 个程序集不含三套自有 Editor 或 Analyzer Runtime 边；Core 的实际 `Library/Bee/artifacts/1900b0aPDevDbg.dag/YLFramework.Core.Runtime.rsp` 仅以 `-analyzer` 引用自有 DLL，不以 `-r` 引用。读取产物 **199 个 Managed PE** 的 AssemblyRef 与 ScriptingAssemblies.json，自有 Editor/Analyzer 和 UnityEditor 非法运行边为 0，所有 YL 程序集没有 Microsoft.CodeAnalysis/NuGet/Microsoft.Build 引用，Foundation 的 CustomPropertyGUI.OnPropertyGUI 不在 Player。与既有 5.5 Managed DLL 名称集合相比没有新增项；工程原有 MCP/NuGet Roslyn DLL 仍在，不宣称原工程零 Roslyn 依赖。
- **正式证据位置：** **`C:/Users/Kano/AppData/Local/Temp/kilo/CGJ2024-6.5-20261013-28dbb576/`** 下保留正式 UnityPackage、`YLFramework-Editor65.exe`、`build-summary.txt`、`player.log`、`player-verification.txt`、`package-verification.txt`、`final-state.txt`。旧 `...-bc3db767/` 仅为历史产物。原场景仍为 `Assets/Scenes/Main.unity`、无 dirty、未进入 Play；编译结束且 scriptCompilationFailed=false。新 Provider/测试各有 Unity 生成的独立 meta；未新增验收场景、Prefab、程序集或临时工程脚本。
- **保护检查：** 28 组 **4452 个文件**按路径/长度/UTC mtime/SHA-256 复核，无额外差异。Runtime 组仅在确认工作区与新提交一致后，以 `28dbb576` 的正式 DLL 更新验收基线；其余组沿用本批最初指纹，含 Packages/ProjectSettings、15 个原 XF Package、游戏/Scene、原 Bundle/StreamingAssets、Prefab/Samples 与 Action/Audio Provider、用户已有忽略/配置修改。原 MainModule 两套产物共 **897 项**保持。Git 再确认 Runtime、Plugins、原游戏/Scene/Prefab、Packages/ProjectSettings 与 SDK 源码无额外差异，所有已有 GUID 与 meta 保留；`git diff --check` 通过。
- **并行变更处理：** 首轮按用户选择锁定 `bc3db767` 的 CE8DA252… DLL；并行部署 E18A17AE… 中间产物时正式导出在写包前拦截，并按用户选择暂停，没有覆盖或替并行任务提交。04:10Z 用户明确告知新 DLL 已完成功能验证和提交，本轮重新核实正式提交 `28dbb576` 及实际远端；最终 DLL SHA-256 **`6861D339663AAA0235C1A83047F78FB81C11588282068D2DE437CD4B5E09EBF3`**，47616 bytes，GUID **`471f81c4273ee66429677df44cbd6960`**。新提交包含 Memory/ReadOnlyMemory、并发容器及 Mono System.dll 身份处理，已增量同步 Attributes 能力说明；本轮不重写 Analyzer，不把中间产物或旧基线当最终交付。
- **当前结论：** 6.5 实现、正式 UnityPackage、Windows64/Mono Player 与必要技术门禁全部完成，阶段 6 **已具备最终技术验收条件并完成 Git 里程碑收口**（集成分支 `integration/xf-yl`，最终标签 `milestone/6-editor`）。没有执行 IL2CPP、移动/WebGL、全原游戏或阶段 7 独立承载；原 Logger Domain Reload 历史风险、第三方许可与外部依赖限制保持。实施到此停止，不自动启动阶段 7。

## 7. 导出与跨阶段交接

- 按用户明确补充，默认根仍为 `Editor`、`Plugins`、`Prefabs`、`Runtime`、`Samples`、`Documentation`，使用 Recurse，不使用 IncludeDependencies。**整个 Plugins 继续递归包含，不裁剪 Odin/Demo/Editor Enhancers，也不改变插件自身 asmdef、源码或导入平台。** 插件许可核查本阶段不处理，不据此宣称最终公开再分发已获许可。
- 保持 Tests、根 Integrations、包外 DOTween、游戏资产/Bundle、PackageCache 排除；新测试/SDK 工具项目、NuGet/中间产物和用户生成的 Examples/YLFrameworkData 不加入默认导出。Editor 工具代码、帮助及自有分析器 DLL/meta 必须随现有根进入包。
- 仍需要既有 UGUI、Newtonsoft、Odin 等依赖，Core 仍有过渡 XFAB；保持 InputSystem 的现有条件/反射契约。不增加 UPM、安装器或“零依赖导入”承诺。DLL 的编译器接入不能变成 Player 运行依赖。
- 各批只同步必要的现有 README/Architecture/Help/Attributes/Resource/UI 使用说明，不重复建立研究报告，不修改已经正确的长期蓝图状态。按用户要求，6.4 在独立专题计划维护；本文件保持总体导航与历史成果。实施新增的正式源码/测试/编译器资产按对应批次授权与门禁处理。
- 阶段 7 再处理最终目录/公开命名/程序集和独立承载；移动 Analyzer 时必须重新核对 Unity 作用域，不能只保留 GUID 就认定接线仍有效。资源专题接手 EditorAssetTool/XFAB；Logger 专题接手全局分析器与生命周期风险；最终发布前再处理插件许可及完整外部依赖/源码交付。

## 8. 已确认决策与停止条件

| 决策 | 已确认边界 |
|---|---|
| 实施路线 | 6.1～6.5 全部完成并完成 Git 里程碑收口；集成于 `integration/xf-yl`，最终标签 `milestone/6-editor`。并行 Analyzer 改进由用户验收并提交 `28dbb576`，6.5 针对该基线完成正式包与必要技术门禁 |
| Editor 程序集结构 | 保留普通 Editor、Core Editor、独立 Help 三套程序集；不合并程序集，不新建大型编辑器管理中心 |
| 成熟工具保留 | Module 创建器、SerializedDictionary Drawer、Audio Inspector、Help 与 Resource 工具优先保留；修复真实缺陷而非因未使用退役 |
| 默认 Plugins 导出 | 保持整个 Plugins 递归导出；插件许可核查暂缓，不据此宣称已获公开再分发授权 |
| 分析器范围 | 七项自有 YL 分析器 YL0001～YL0007 正式交付、默认 Warning；只作为 Core 作用域编译器输入，不进入 Player 运行引用。自有 SDK 测试、实际 Unity 编译正反例与 6.5 Player 隔离门禁完成；不复制原 DLL 或以 Logger 冒充，未改 Runtime 契约 |
| 原游戏产物保护 | 6.2 的自有 Resource 清理与构建操作必须保护原游戏 MainModule 产物，不做整根清理 |
| SDK 项目受管方式 | 用户已解除必要精确工具权限；已追加两个自有 SDK 项目的精确 `.gitignore` 例外，SDK 锁定及默认不部署的构建测试入口已实际运行 |
| 阶段 7 边界 | Runtime 目录结构、公开命名与最终独立承载收口保持后续范围，阶段 6 不预做 |

当前没有还需先决定的重大公共接口/序列化迁移取舍；第 5 节新诊断语义、Warning 默认及窄内部 owner 例外已随总体方案批准，不沿用未知原 DLL 契约。若实施研究发现真实持久生成物/外部 Settings 类型/反射消费者需身份迁移，或分析器必须改变 Runtime 才能成立，暂停相关批次，提交证据与替代方案再确认，不静默放宽规则或扩大授权。

**完成标准：** 五批的必要门禁均有实际证据；已保留/修复/退出能力与文档一致；没有原游戏/原 Package/Bundle 或未授权环境变更；默认包包含有效 Editor 工具与编译器资产、不携带框架测试；Player 隔离与依赖边界准确。方案获批不代表任何批次实施或验收通过；各批实施、Commit/Push/Merge/Tag 与分支操作均按用户单独授权执行。

版本依据：[Unity 2022.3 Analyzer scope](https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html)、[SettingsService.OpenProjectSettings](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SettingsService.OpenProjectSettings.html)、[ApplyModifiedProperties 的 Undo 契约](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html)。Context7 专用查询工具本轮未提供，使用对应版本 Unity 官方文档，不以最新版经验代替工程证据。

6.4 的具体版本证据与反编译来源见 [独立专题计划](xf-yl-6.4-analyzers.md#7-技术完成与剩余证据)。
