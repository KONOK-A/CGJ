# XF × YL：阶段 6 Editor 深度融合总体方案

**状态：APPROVED，6.1 待单独实施授权。** 本文件是阶段 6 唯一总体方案；6.1～6.5 五批实施路线已获用户正式批准，但本文件不构成任何单批实施授权，各批仍须单独授权。

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

自有标记使用点：`C/mvc/View.cs:141,234-370`、`mvc/Model.cs:8`、`Manager/EventManager.cs:25-243`、`Other/LoadViewArgs.cs:20-27`。六类定义、namespace `YLFramework.Core.RoslynAnalyzers` 及 GUID 原样保留。原 DLL 规则 ID/严重级与完整算法不可核实，因此交付的是明确的 YL 等效能力契约，不宣称逐位复刻原 DLL。

### 5.2 源码、构建与 Unity 接入

- 自有源码项目：`Tools/YLFramework.Analyzers/Analyzer/YLFramework.Analyzers.csproj`；诊断测试项目：同根 `Tests/YLFramework.Analyzers.Tests.csproj`。这是手工维护的 SDK 工具项目，不是 Unity 生成工程，也不是新 UPM/插件安装系统。
- 按用户确认，仅给上述两个项目添加 `.gitignore` 精确例外；中间产物按项目隔离到既有忽略的 Temp 或工程外临时位置。不强制纳入 Unity 生成 csproj，不修改工具权限，不提交 NuGet 缓存。
- 分析器产物目标 `netstandard2.0`，锁定 `Microsoft.CodeAnalysis.CSharp 3.8.0` 与 Unity 2022.3 编译器契约，依赖设为工具侧私有；不把 Roslyn/NuGet/SDK DLL 复制入 Unity。测试使用实际可用 SDK 的兼容承载，必须与被测分析器使用同版 Roslyn。
- SDK 可用性本轮未核实，`dotnet --info` 被工具权限拒绝；不将拒绝解释为 SDK 缺失。6.4 前须只读确认工具链及 restore，缺失时报告，不自动安装/升级环境。
- 新 DLL 名为 `YLFramework.Analyzers.dll`，放 `C/Analyzers/`，由标签接入 Core.Runtime 及其引用者。不能放 Core.Editor 下而误以为会分析 Runtime/业务；DLL 普通 Editor/Player 平台均禁用、只作为编译器输入，不加入任何 Runtime asmdef 的 precompiledReferences。这只是为 Unity 作用域增加编译器资产，不改 Runtime C#、移动标记或改 Runtime 程序集。
- 仓库保留源码、版本锁定与构建入口；正常 UnityPackage 携带 DLL/meta/使用说明，不携带工具测试和 SDK。最终源码分发形态留阶段 7，本阶段必须保证从受管源码可重建产物。

### 5.3 诊断契约

按属性符号及程序集身份识别，不按短类型名、源码字符串或 namespace 前缀全局匹配。使用 ConstructorArguments 读取参数，不加载用户程序集执行 Attribute 构造。稳定 ID 为 `YL0001`～`YL0006`，首版默认 Warning，避免未知原严重级变成已验收 Runtime/测试的编译破坏；警告提供实际诊断，不等于运行时安全证明。消费者可按正常 Roslyn 设置提升严重级，本阶段不全局压制诊断或加 NoWarn。

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

### 6.2 自有 Resource 操作安全与真实界面

**目标/前置：** 保留标记→YL 构建→ResData 的旧流程，先把路径/归属与破坏性操作做安全，再提供可信窗口。这里不融合 XFAB，不构建或清理实际游戏 Bundle。

**退出机制：** 整根清空、无效平台 Toolbar/版本输入、无效 Selection 构造接线、子串/多 owner 隐式归属。

**方案：** 清空与替换只针对当前目标的明确 YL 默认/子工程产物目录，保留父目录及未知兄弟目录；确认展示实际路径，拒绝越界/根目录/重叠 owner，取消不产生副作用。BuildTarget 和路径名同源，所有预检在 BuildPipeline/删除之前；构建失败不得替换旧可用产物。窗口显示 activeBuildTarget 并引导 Unity Build Settings，不建设自己的平台切换器。标记支持既有单选契约，修改 importer 应具备 Undo 或明确的可逆操作与保存/重导入验证。

**门禁：** 自有临时资产/目录中的标记→取消→重导入、菜单状态、不支持目标、路径越界、子工程边界/跨 owner、取消清理、清理保留无关兄弟目录；必要 importer Undo/Redo。新增测试只锁真实缺陷。一次隔离的小 Bundle 构建/数据表读取验证，可用临时输出承载，不写原 AssetBundles/MainModule 或 StreamingAssets/MainModule；窗口真实 GUI 冒烟、关闭后无重复事件订阅。现有 Resource Runtime 回归只选数据表/路径直接消费者。

### 6.3 资源常量生成可靠性与消费者承载

**目标/前置：** 依赖 6.2 的有效分组/标记快照，保留工具而不是删除未使用功能。一次收集、一次生成；常量值保持真实资源键，符号完整规范化，空/非法输入和类/成员碰撞明确失败，不静默丢资源。

**退出机制：** 累计 CompileUnit 重复输出、拼回未清洗后缀、失败截断旧文件，以及“已生成就可被任意 asmdef 使用”的隐含假设。

**方案：** 保留既有 `Assets/YLFrameworkData/YLResData.cs` 路径/namespace；有存量时不搬、不覆盖手写文件、不改变 meta。当前无已查实生成物，新输出作为消费者源码，不放回 Framework Runtime，也不纳入默认导出。空承载目录可生成独立 `YLFramework.Generated.ResourceNames` asmdef、无 Runtime/Editor 依赖，预定义程序集可自动引用，业务 asmdef 显式引用；若目录已有源码/asmdef，先报告并由消费者确定承载，不能对未知业务文件批量改变程序集身份。不写泛用代码生成框架。

**门禁：** 临时多 Bundle 生成类唯一、合法 C#、稳定重复生成、真实键值、规范化碰撞/空输入失败前不覆盖；writer 释放、既有 GUID 保留。用实际 Unity 编译的临时 consumer asmdef 引用产物；不以文本/正则检查代替生成源码编译。未改变的 Module 生成器只做必要同工程冒烟，不重复全部 Core 回归。

### 6.4 自有分析器源码化与接入

**目标/前置：** 按第 5 节交付六类诊断、受管 SDK 项目、最小构建/测试入口及 Core 作用域 DLL。先确认 SDK/Roslyn 兼容与新增两项目的精确忽略例外，再构建；不移植原 DLL，不使用已有 Logger DLL 冒充实现。

**退出机制：** “标记存在即已有静态强制”的错误表述；不删除标记、不修改已验收的行为契约。

**门禁：** 工具侧 build + 诊断测试，包括每个 ID 的合法/非法、必要符号边界与两个实际内部 owner；从源码重建产物。Unity 实际重编译验证正例无误报、负例产生规定 ID/位置/Warning，无 analyzer-load/AD0001；探针使用自有临时消费者，不把违规脚本留在工程。元数据核验标签/GUID/普通平台禁用，Core 与无关程序集的实际作用域对照；日志/诊断须区分自有 YL、原 XF、原 Logger。工具测试通过不等于 Unity 兼容通过；最终 Player 隔离在 6.5 验证。

### 6.5 帮助、导出与阶段收口

**目标/前置：** 前四批稳定；复用 HelpRegistry/HelpWindow，接入 Core 与通用工具的少量指南/精选 API，保持既有 Action/Audio 内容与独立帮助核心；准确交代依赖、创建路径、分析器诊断/重建方法和生成源码消费方式。

**退出机制：** 同职责的新增 About/Help 实现、虚假的自动 Attribute/Analyzer 功能说明；不是退出已有成熟 Help 或商业插件。

**门禁：** 新提供方注册/重复注册、搜索、关键指南/API、缺 XML 时仍显示声明、窗口真实 GUI/只读无宿主创建。Core.Editor→Help 的新增引用须定向更新 CoreEditorIdentityTests 的显式白名单，只允许 Help，继续禁止整个 YLFramework.Editor/Runtime、原 XF 与游戏依赖；HelpWindowTests 的无依赖约束保持原样。Action/Audio provider 仅在公共 Help 协议变动时做直接回归，不默认重跑其全集。

复用 ExportUnityPackageTests 的真实包解包校验，增加新 provider/Analyzer DLL 与 meta 哨兵，不弱化既有 GUID/meta、退役入口、Tests/Integrations 排除断言。由于新增编译器输入，最终安排一次既有独立验收承载的 Windows Development/Mono Player 构建与最小启动冒烟，确认没有自有 Editor/Analyzer/Roslyn 运行引用；不默认执行 5.5 综合流程、原游戏综合验收或 IL2CPP。任何需临时状态的旧验收 driver 先读其前置授权，不能自动沿用 Logger 隔离。纯 Editor 批次不逐批构建 Player。

## 7. 导出与跨阶段交接

- 按用户明确补充，默认根仍为 `Editor`、`Plugins`、`Prefabs`、`Runtime`、`Samples`、`Documentation`，使用 Recurse，不使用 IncludeDependencies。**整个 Plugins 继续递归包含，不裁剪 Odin/Demo/Editor Enhancers，也不改变插件自身 asmdef、源码或导入平台。** 插件许可核查本阶段不处理，不据此宣称最终公开再分发已获许可。
- 保持 Tests、根 Integrations、包外 DOTween、游戏资产/Bundle、PackageCache 排除；新测试/SDK 工具项目、NuGet/中间产物和用户生成的 Examples/YLFrameworkData 不加入默认导出。Editor 工具代码、帮助及自有分析器 DLL/meta 必须随现有根进入包。
- 仍需要既有 UGUI、Newtonsoft、Odin 等依赖，Core 仍有过渡 XFAB；保持 InputSystem 的现有条件/反射契约。不增加 UPM、安装器或“零依赖导入”承诺。DLL 的编译器接入不能变成 Player 运行依赖。
- 各批只同步必要的现有 README/Architecture/Help/Attributes/Resource/UI 使用说明，不重复建立研究报告，不修改已经正确的长期蓝图状态。本轮唯一新增文件仍是本方案。
- 阶段 7 再处理最终目录/公开命名/程序集和独立承载；移动 Analyzer 时必须重新核对 Unity 作用域，不能只保留 GUID 就认定接线仍有效。资源专题接手 EditorAssetTool/XFAB；Logger 专题接手全局分析器与生命周期风险；最终发布前再处理插件许可及完整外部依赖/源码交付。

## 8. 已确认决策与停止条件

| 决策 | 已确认边界 |
|---|---|
| 实施路线 | 6.1～6.5 五批实施路线正式批准；6.1 待单独实施授权，各批实施不自动衔接 |
| Editor 程序集结构 | 保留普通 Editor、Core Editor、独立 Help 三套程序集；不合并程序集，不新建大型编辑器管理中心 |
| 成熟工具保留 | Module 创建器、SerializedDictionary Drawer、Audio Inspector、Help 与 Resource 工具优先保留；修复真实缺陷而非因未使用退役 |
| 默认 Plugins 导出 | 保持整个 Plugins 递归导出；插件许可核查暂缓，不据此宣称已获公开再分发授权 |
| 分析器范围 | 阶段 6 正式接入自有六类 Roslyn Analyzer（YL0001～YL0006、默认 Warning）；不复制原二进制，不使用 Logger DLL 冒充实现 |
| 原游戏产物保护 | 6.2 的自有 Resource 清理与构建操作必须保护原游戏 MainModule 产物，不做整根清理 |
| SDK 项目受管方式 | 6.4 为自有 SDK 项目增加精确 `.gitignore` 例外；当前不修改规则 |
| 阶段 7 边界 | Runtime 目录结构、公开命名与最终独立承载收口保持后续范围，阶段 6 不预做 |

当前没有还需先决定的重大公共接口/序列化迁移取舍；第 5 节新诊断语义、Warning 默认及窄内部 owner 例外已随总体方案批准，不沿用未知原 DLL 契约。若实施研究发现真实持久生成物/外部 Settings 类型/反射消费者需身份迁移，或分析器必须改变 Runtime 才能成立，暂停相关批次，提交证据与替代方案再确认，不静默放宽规则或扩大授权。

**完成标准：** 五批的必要门禁均有实际证据；已保留/修复/退出能力与文档一致；没有原游戏/原 Package/Bundle 或未授权环境变更；默认包包含有效 Editor 工具与编译器资产、不携带框架测试；Player 隔离与依赖边界准确。方案获批不代表任何批次实施或验收通过；各批实施、Commit/Push/Merge/Tag 与分支操作均按用户单独授权执行。

版本依据：[Unity 2022.3 Analyzer scope](https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html)、[SettingsService.OpenProjectSettings](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SettingsService.OpenProjectSettings.html)、[ApplyModifiedProperties 的 Undo 契约](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html)。Context7 专用查询工具本轮未提供，使用对应版本 Unity 官方文档，不以最新版经验代替工程证据。
