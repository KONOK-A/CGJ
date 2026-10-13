# XF × YL：阶段 6.4 Roslyn Analyzer 深度融合实施计划

**状态：IMPLEMENTED，G0～G5 已完成并通过用户技术验收，Git 收口已获单独授权。** 用户已正式授权无人值守实施 6.4，并自行补充必要的精确权限；未改权限或绕过策略。本文件独立承载研究、设计、门禁及实际结果，[阶段 6 总体方案](xf-yl-6-editor.md)仅保留总体路线、既定基础契约与导航。阶段 6 整体保持 IN PROGRESS，6.5 未启动；本批未执行 Commit/Push/Merge/Tag/Reset/Clean 或切换分支。

**目标：** 理解 XF 实际设计、吸收有效机制并修复准确性缺口，交付 YL 自有源码、测试、可复现构建、中文诊断与正确 Unity 集成；不是复制原 DLL、做六个能触发的示例或建设通用 C# linter。

**架构：** 单个 DiagnosticAnalyzer 入口以 CompilationStart 解析正式符号、按调用/base/存储/写入分组分析，共享编译上下文缓存。保留六基础 ID/Warning 和 Attribute 身份，只引入有可靠证据的必要增强；不改正确 Runtime 来迁就规则。

**技术栈：** Analyzer `netstandard2.0`、Microsoft.CodeAnalysis.CSharp `3.8.0`、C# 9；实际 Unity `2022.3.62f3c1` 宿主依赖快照为 Roslyn `4.3.1`。SDK/restore/加载兼容必须另行实际核验，不能由静态目录或宿主版本代替。

## 1. 基线、授权与证据层级

- 分支 `fusion/6-editor`，6.3 已通过技术验收；开始研究时本地/远端阶段分支均为 `8065d65c4dddd2b0aebfa43f86f9a3222e16589d`，远端 integration 为 `7db5806`。开始时工作区/暂存区干净；迁移时 status 为总体计划、用户 `.gitignore` 修改及本独立计划未跟踪，未有产品变更。既有测试 worktree 的未跟踪文件未读取/修改，不据此判断参与者活跃状态。
- 已读取工程 AGENTS、IntegrationPlan、Architecture、阶段总体/相关历史成果与当前源码。长期蓝图的“阶段 6 尚未实施”是旧导航状态，不在本 Plan 修改。原游戏/MainModule 输出、Scene/Prefab、Packages/ProjectSettings、原 XF/XFAB/XFFSM Package 严格保护。
- 6.4 可修改的未来产品范围：自有工具源码/编译器 DLL 与导入、定向测试、精确忽略例外和直接说明。无 Commit/Push/Merge/Tag/Reset/Clean/切分支；不重构 1～6.3、不预做 6.5/7、不处理 Plugins 许可清理。
- 用户已明确选择：YL0001 保留正确直接 base 调用的**存在性**，不加 CFG 可达性/全路径；首版删除跨接口自动继承/AND；少量新诊断按真实收益门禁决定，不以数量为目标。
- 用户已用正确提取 DLL 的反编译替换旧目录。当前证据根 `XFGameFrameworkCodeAnalyzer/`，六规则位于其同名子目录；旧导出的行号、MVID 头部及“Initialize 无法反编译”不作为现行证据。用户文件/忽略原样保留，只读，不编译其 csproj。
- 当前证据分层：**反编译控制流/元数据文本已读**；本机 DLL SHA-256/MVID/AssemblyRef、原 DLL 实际加载及隔离行为、SDK build/test、Unity 编译/Play **未执行**。反编译并非行为实验，不将预期写成已通过结果。
- `.NET SDK` 目录存在 `9.0.301/9.0.312/10.0.102`，不是实际执行/restore 证明；本地已检查 CSharp 缓存未有 3.8.0。`dotnet --info`、`jb --help` 曾被实际工具策略拒绝，文字授权未改变该策略；本轮不重试/包装/委派绕过。恢复执行仍拒绝时先报告停止，不自动安装或升级。

## 2. XF 真实能力与融合判断

### 2.1 来源与接入

锁定包 `com.xfkj.xfgameframework@2182708aec`，package 版本 `1.18.8`、revision=`2182708aecc596f30739fa8634fe2b9b6946ddd7`，包内 MIT 归弦小风。已检索的本地及公开作者仓库没有可重建 Analyzer 原源码，不能宣称绝对不存在；官方目录仅 DLL/meta。远程 v1.18.1/master 的 DLL blob 同为 `12015a8877a22062b0ceea11d3c27d19e83472be`、25600 字节，只证明两个远程版本内容相同，不证明构建日期或本机逐字节一致。

原 DLL GUID=`24bc710d4cbc88842a90b58e018dafcd`，精确 RoslynAnalyzer 标签、显式引用/普通平台禁用，位于 XF **Editor** asmdef；不会因此覆盖同工程所有 Runtime。Logger 为另一身份/作用域，未知其规则不能冒充六类能力。当前 Core Bee 响应快照只有 Unity generators/Logger，不含原 XF 主 Analyzer，不代替实际编译证据。

当前 `Properties/AssemblyInfo.cs:7-12` 给出 Debug、`1.0.0.0`、informational revision=`2c3317eaccce4fa0d1da3f05c587a983f5fc4b4a`。导出 csproj 的 C# 14、未固定 Roslyn 引用和本机 .NET 3.1 HintPath 属反编译承载，**不是原工具链/可复现原工程证明**。仍有 Unknown result type/Expected Obj 注释，不能直接编译当“原版”。

### 2.2 完整注册与诊断

当前导出有六个 `[DiagnosticAnalyzer("C#")]` 类型，各一条启用 descriptor。全部 Initialize 均 ConfigureGeneratedCodeAnalysis(None)、EnableConcurrentExecution；五个 SyntaxNode 注册、一个 Symbol 注册，未发现 Operation/CompilationStart、CodeFix 或其他正式规则。数字严重级依 Roslyn 3.8 官方定义：3=Error，2=Warning。

| XF 类型 / ID | 默认 / 类别 | 注册与设计价值 | 缺口与 YL 取舍 |
|---|---|---|---|
| MustCallBaseAttributeAnalyzer / MUST_CALL_BASE_ATTRIBUTE | Error / Usage | MethodDeclaration；直接被覆写方法标记，块/表达式体、自定义消息 | 短 Attribute 名、base 名字比较，错误重载/嵌套函数可算满足，不追更上层标记；改为符号/直接体/override 链，保留存在性 |
| RestrictedCallAttributeAnalyzer / RESTRICTED_CALL_ATTRIBUTE | Error / Usage | Invocation；string/string[]，方法名/Type.Method/Namespace.Type.Method | 外层 Method/Constructor 优先于 LocalFunction，Lambda 借外层身份；无 caller 直接放过；嵌套类型丢失，null 可能 AD0001；保留选择器表达力、纠正独立调用体 |
| WarnInClassesAttributeAnalyzer / WARN_IN_CLASSES_ATTRIBUTE | Warning / Design | Invocation；Type/Type[]/多次标记/消息，类基链及接口符号 | 异常匹配谓词可能处理无关特性，最近 ClassDeclaration 误归类 struct/record，目标仅直接标记；准确身份/类型上下文/去重 |
| DisableNewAnalyzer / DISABLE_NEW | Error / Design | ObjectCreationExpression，构造消息 | 漏目标类型 new()/继承/受约束 new T，无自身工厂；用 Operation 与窄 ownership |
| MethodLocalOnlyAttributeAnalyzer / METHOD_LOCAL_ONLY_TYPE | Error / Usage | **RegisterSymbolAction：Field、Property、Event**；直接类型/一层泛型实参，自定义消息，并发且跳过生成代码 | 无继承/数组/多层，未区分实际存储与 delegate/phantom 参数，内部池无例外；事件入口已由 RVA 数据确认，实际诊断仍需对照实验 |
| ProtectedHierarchyModificationAnalyzer / PROTECTED_HIERARCHY_MODIFICATION | Error / Design | 简单/+ - * / compound、四类 ++/--、ref；沿类基链查保护标记、自身层级可写 | 漏 %=、位运算/移位/??=、out/解构；TypeSymbol 转显示名再 metadata 查找失效，接口禁止者漏，字段/属性过滤异常；Operation/原始符号替换 |

**MethodLocalOnly 注册证据已补齐：** `XFGameFrameworkCodeAnalyzer/MethodLocalOnlyAttributeAnalyzer.cs:19-27` 确认 None、并发、AnalyzeSymbol delegate、三项数组与 RegisterSymbolAction。用户从 ILSpy 提供其初始化字段 `FAC5F1456D95A2310438A8C84659E979AEA8D8DC535C6D44EA99452A0B38B65A` 的 12 字节 RVA 数据（此前磁盘导出没有展开），按连续三个 little-endian Int32 解读，并核对固定 revision 的 Roslyn 3.8 SymbolKind 官方源码：

| 数据 | 整数 | SymbolKind |
|---|---|---|
| `06 00 00 00` | 6 | Field |
| `0F 00 00 00` | 15 | Property |
| `05 00 00 00` | 5 | Event |

因此注册顺序为 **Field → Property → Event**，与 handler 的三个分支一致；不再把三项注册值列为未知。证据来源明确为“用户 ILSpy 数据 + 官方枚举 + 已读 Initialize”，不是本轮执行 DLL/实验。原版确有事件分析入口，但 YL0005 不机械复制“delegate 的泛型签名等于持有”；例如 `event Action<Args>` 的参数合同不等于存储 Args，本批仍按正式字段/属性持有契约与真实存储判断，行为对照保留。

### 2.3 已读控制流与尚需实证部分

- base 的 `CallsBaseMethodInAllPaths`（当前 `MustCallBaseAttributeAnalyzer.cs:110-123`）只判断有没有 IfStatement；AllowConditionalCall=true 才用，现有正式 Attribute 没此属性，不是真 CFG/全路径能力。不搬该参数；正确调用后存在普通 if 不应被 YL 拒绝。
- Warn 实际谓词（`:57`）等价 `Name == "WarnInClassesAttribute" || Name != "WarnInClasses"`，处理多数无关 Attribute 的可能性在新导出仍存在；辅助正确匹配函数未使用。必须用无关 typeof Attribute 对照证明，不把单个导出文本当最终 IL 结论。
- Protected 过滤（`:110`）为 `symbol is IFieldSymbol && symbol is IPropertySymbol` 的永假合取；禁止者再解析（`:235`）及 ref-only（`:96`）仍存在。异常成员/事件处理先用 IL/正反例确认，不照抄。
- SyntaxKind 既有数值有官方二进制兼容约定；8360=ref、8361=out，8719～8725 是真实未注册 compound，不虚构“数字写法跨版本漂移”缺陷。裸数字是导出表现，不能证明作者手写常量。
- template Resources/resx 的“类型名全大写”未被六规则引用，不是第七项能力。实际 DLL 可加载类型/完整 descriptor 集合仍要核验。
- 新导出的 DisableNewAnalyzer 为 internal；仅凭类型可见性不能断言不可加载。Roslyn 3.8 的 AnalyzerFileReference 按属性扫描类型并尝试 Activator.CreateInstance，故 G0 应使用真实 AnalyzerFileReference/GetAnalyzers 和加载失败事件核验发现/构造结果，不只手工反射列六个类型。实例创建失败与分析阶段 AD0001 分开记录，自有入口采用 public Analyzer/可用无参构造。
- 中文/自定义原因、三形式白名单、类型/接口比较、并发/生成代码豁免、保护层级自身修改值得保留。短名匹配、无 caller 放过、伪全路径、脆弱再解析/消息和重复绑定不继承；按 compilation 缓存与取消减少明确成本，不凭估计宣称倍数。

自有代码按上述证据重新实现，不将反编译工程加入产品构建；反编译不消除上游许可。如果后续确实复用实现片段，保留适用的 MIT 声明和完整许可证，来源信息按实际需要集中记录，不批量补模板头或清理原包声明。

## 3. YL 六规则契约与开发体验

正式 Attribute 均在 `Assets/YLFramework/Runtime/XFMain/Attribute/RoslynAnalyzers/`、namespace=`YLFramework.Core.RoslynAnalyzers`、程序集=`YLFramework.Core.Runtime`，保留类型/构造/AttributeUsage/GUID，不新增配置型 Attribute。

| ID / marker | 脚本 GUID | 语义、定位与合法边界 |
|---|---|---|
| YL0001 MustCallBase | 55aa73158a52182499729b82d6211f92 | 用户已选择存在性：有 body 的 override 沿标记链，自己的直接体存在绑定到被覆写槽的显式 base 即满足，含表达式体；Lambda/local/method group/错 overload 不算，不建 CFG/推可达/全路径。定位 override 名/目标签名；抽象层级冲突解释，不建议非法调用 |
| YL0002 RestrictedCall | a02821b903f3c4d40be0bd0a8e7e9abe | Invocation 静态目标、OriginalDefinition/ReducedFrom、有效 override 标记；真实最近 caller，初始化器/无 caller 不放过。定位调用/当前体/允许位置；传 callback 合法，callback 内受限调用独立检查，不推运行时 Awake 时机 |
| YL0003 WarnInClasses | 0d56d22314c415c4dbc6730f86f6aa7d | 精确 Type/Type[]+message，最近 class/struct/record 自身/基类/接口；View 内 Lambda/local 仍属该类型，普通嵌套 helper 不借外层 View。多标记理由去重合并，一次调用一条，null 原消息仍给实体/原因 |
| YL0004 DisableNew | 46bf1f5bbe117ee4289aa7cb447f0d8d | 普通/目标类型 new，标记类基链与约束可确定的 new T；自身实现中的工厂/词法嵌套实现/工厂 Lambda 合法，不因同程序集/只是派生而全免。base()/this() 不重复算外部 new，定位 new/工厂建议 |
| YL0005 MethodLocalOnly | a994e4cf6be4112498358e50ddd69078 | 显式 field/property（含 static/auto，跳过隐式 backing 防重复）、直接类型/数组/实际容器；参数/返回/局部/typeof/纯泛型/delegate签名不算。一个成员一条，给有限存储路径/回收原因，两个窄 owner |
| YL0006 ProtectedHierarchy | bbefa922f91525f42990d11fe4cf913b | 实例 field/property/indexer 写：简单/all compound/??=/++/--/解构叶/初始化、可写 ref/out，ref local/return 暴露点报而不追后续所有别名。读取/in/ref readonly/static/非禁止者不报；结构体值链追保护成员、引用边界停，不把集合 mutation 当属性写。定位左值/ref |

六 ID 保持 Warning/启用，类别 YLFramework.Usage；这是批准的 YL 决策，不随已知 XF Error 改变。语义/API 不是安全授权：诊断包含真实类型/方法/成员、约束原因、可执行改正方向，自定义原因追加而不取代上下文；同入口/问题每 ID 不重复。

### 3.1 继承、接口与调用方名字

AttributeUsage 不改，override 按有效 Inherited 取最近单次实例，Warn AllowMultiple 合并。接口 Attribute **不自动流向实现方法**：接口绑定目标执行其标记，无标记具体实现不制造接口契约，无 FindImplementationForInterfaceMember 扫描/AND/同名猜测。以后若需全实现约束，先独立定冲突/接收者策略。类型上下文的接口匹配保留；typeof(G<>) 开放定义、typeof(G<int>) 精确保留实参。

Caller 选择器 Ordinal/大小写敏感，由声明符号生成，非 using/源码字符串；普通名字选择器不区分 caller 参数重载，与被调方法/marker 精确身份不同。

| 调用体 | 选择器合同 |
|---|---|
| 成员方法 | M、Type.M、Namespace.Outer.Type.M；补嵌套外层，沿用 XF 类型名字语义、泛型声明归一，不包装成权限安全保证 |
| property/indexer accessor | get_P/set_P 及限定式；索引器依实际 MetadataName，可显式限定 Type.P/Namespace.Type.P；表达式 getter 独立，不借 Awake |
| constructor/static constructor | .ctor/.cctor 及限定式，如 Type..ctor；未明确允许违规，不因无普通 method 放过 |
| 其他具名成员 | 只自己的稳定 MethodSymbol 名/限定式；显示 actual kind，未绑定/编译错误不造诊断 |
| Lambda/local function | 独立体，不借外围名字；首版无匿名/局部选择器，local Awake 不冒充成员 Awake；主调用传 callback 合法 |
| field/property initializer | 无稳定 caller 方法身份，不命中方法选择器，受限调用提示位置/上下文 |

名单过滤 null/空元素，保留其余项；最终空集合才没有合法 caller，TypedConstant Error 先交编译器处理，不崩/不新增 signature parser。普通 Lambda 的 Operations.IAnonymousFunctionOperation 与 CFG 的 FlowAnalysis.IFlowAnonymousFunctionOperation 在 3.8 均有，非版本替代；本轮无 CFG。base 必须 BaseExpressionSyntax+槽比较，InstanceReferenceKind 不区分 this/base；特殊符号实际形态 G1 验证。

### 3.2 实际持有与窄 owner

- 先直接类型/数组；类型参数只走有效类型参数/类约束闭包，不钻 where T:IEnumerable<Args> 的实参。未标记 delegate/方法签名停；基类用已构造 BaseType，泛型只走存储内容位。
- wrapper 实例字段/auto backing 形成按需有限定义图，忽略 static/签名；摘要含外层 ContainingType 参数、基类与真实内容位，false→true 到稳定再缓存，纯环无值叶不持有、有 T 叶传播。无需合成泛型符号/通用替换器；查询缓存构造符号，不混 Box<Args>/Box<int>，Box<Box<Args>>逐内容位查询。
- 不读全库：仅候选触达；缺失/错误/不可读 metadata 为 unknown，不落完整 negative 缓存，取消可用、不无限展开/任意深度预算，消息只取一条有限见证。直接 static Args 照报，开放 static T 追踪延后；Wrapper<T>{static T cache} 不使其实例误报。
- profile 从真实框架引用解析；GetTypeByMetadataName 返回源码伪全名/歧义不 seed，不按短名/namespace 回退。可读自有类型按结构判断。

候选入口先由直接标记、数组、类型参数约束或嵌套泛型实参中的有效标记确定；自有摘要仅证明这些实参进入哪些实例值槽。不对每个普通非泛型字段展开任意对象图来搜索隐藏 Args，也不由方法调用/运行时赋值推断持有。未标记包装类型内部若直接声明 Args 字段，在所属源码受分析时仍由该字段自身触发；跨 metadata 隐藏的非泛型包装图属于已知边界。这个预筛不是短名匹配，也不能漏掉外层泛型参数或 Box<Box<Args>>。

| profile（以完整 namespace、arity、真实符号落实） | 内容位 |
|---|---|
| System.Collections.Generic List/HashSet/Queue/Stack/LinkedList/SortedSet，arity=1 | T |
| 同域 Dictionary/SortedDictionary/SortedList，arity=2 | K **和** V，键也长期持有 |
| System.Collections.ObjectModel Collection/ReadOnlyCollection/ObservableCollection，arity=1 | T |
| System.Tuple/ValueTuple arity1～8、System.Collections.Generic.KeyValuePair arity2 | 所有值，Rest递归 |
| 同域 ICollection/IList/ISet/IReadOnlyCollection/IReadOnlyList；IDictionary/IReadOnlyDictionary | T或K/V，明确持有协议近似，不泛化任意接口 |
| System.Threading.Tasks Task/ValueTask，arity=1 | TResult可保留；Task<Args>报，Task<Phantom<Args>>不因内部提及就报 |
| Foundation YLFramework.Core.Pool.ObjectPool，arity=1 | T有Queue/Stack缓存契约，不依赖metadata私有字段可见性 |

IEnumerable/IEnumerator/IQueryable/IAsyncEnumerable、IComparer/IObserver、自有 IPool 不作无证据近似，是已接受召回边界。通用 carrier 测试的依据是实际值存储，不以当前游戏用量为唯一标准；不逐 BCL 机械堆用例。

**池豁免：** 被分析成员确为标记类型自身 private static readonly field、真实 Foundation ObjectPool<Self> 内容位等于自身；现场绑定真实 Core LoadViewArgs.Pool 的成员/形态，不豁免同类型所有 field/property、伪 pool、外部 ArgsPool。其内部 new 由 YL0004 独立判断。

**事务豁免：** 真实 Core LoadViewRequest<T> 原定义的 args，private nonstatic field、类型等于真实 Core LoadViewArgs、arity1；不是同 namespace/同程序集/成员名字判断，更不豁免整个 request。ReleaseInputs 清字段后 Release 的既有生命周期保留。

### 3.3 已知静态边界与消费者

object/接口擦除、反射/Activator/dynamic、闭包逃逸、无约束泛型工厂、任意被调副作用/跨过程所有权不保证；错误语法/符号不造误报/AD0001，不因此放弃明确数组/赋值/继承。operator/constructor 上无当前正式调用 marker 契约，不扩成通用 linter。

实际标记在 View.Start、16个RegisterEvent、Model、32个EventManager Add/Remove、LoadViewArgs。正常 Runtime/合法消费者应零误报；ModuleManager/NUnit 非禁止者的 Model 写入合法，测试 delegate 探针合法。

`Tests/Core/EditMode/CoreLoadViewArgsTests.cs:13,21`、`Tests/Core/PlayMode/CoreClassPoolScope.cs:10` 为池/泄漏检查持有，三处声明级 YL0005 pragma；前者 :237,264 故意 foreignPool new，两处 YL0004 pragma。只 disable/restore 对应 ID并说明目的，不改断言、换 object 逃避或排测试程序集。

## 4. 全框架需求与唯一额外候选

综合 Core Module/MVC/View/Panel、Foundation Pool/EventBus、Runtime Resource/HTTP 的实际合同/程序集评估，而非只看六名字。**唯一拟新增 YL0007：View 框架消息遮蔽，Warning、YLFramework.Lifecycle。**

真实 Core View 派生类声明无参/非static/非generic void OnEnable/OnDisable/OnDestroy 竞争 private 框架消息，普通 C# 不提示，可能破坏事件管理、Panel激活/钩子、销毁会话。定位方法名、建议 OnViewEnabled/OnViewDisabled/OnClosed，含 private/new/virtual/override；同一继承声明不重复。普通MonoBehaviour/XF View/伪类型/普通nested helper不报，Awake合法、Start归YL0001/编译器，不禁止所有Unity消息、不改Runtime可见性。

**启用门禁已通过：** 最小正反 View 的原生重新启用/停用和销毁已实测。OnEnable 遮蔽失去订阅重建，OnDisable 遮蔽保留旧身份/缺失正式停用钩子，OnDestroy 遮蔽将及时关闭推迟到下帧兜底；索引最终仍恢复，不宣称必然永久泄漏。已据此登记/启用 YL0007，并通过 SDK 红绿与原生三 Warning，未改变 Runtime 消息可见性。

不新增低置信规则：EventBus直连/RemoveAll难区分刻意global监听；Module注册/资源释放/HTTP借用依赖时序；旧LoadView已CS0618。ResLoader直接构造已error-obsolete；IResLoader/ResKey回收要追转交；HttpManager :32-41与SendWebRequest :179-224区分借用/中间Handler及终态释放，盲禁Dispose误报、只特例直接callback召回低且扩大Runtime scope。CodeFix、全路径base、closure逃逸、通用marker配置linter延后。

## 5. SDK、集成与维护

- 两个正式项目为 `Tools/YLFramework.Analyzers/Analyzer/YLFramework.Analyzers.csproj` 与同根 `Tests/YLFramework.Analyzers.Tests.csproj`。Analyzer 不引用 Unity/Core/Workspaces，CodeAnalysis 3.8.0 的 PackageReference 为 PrivateAssets=all；单入口 CompilationStart，按专题分组，不建规则插件框架。
- 解析真实 Core assembly 与完整 marker/View 符号；缓存仅当前 Compilation，采用线程安全或不可变结果，并发且 GeneratedCodeAnalysis=None。无 marker/View 不注册无用入口；共享 Invocation 绑定，MustCallBase 只检查有义务的 body，无 CFG/调用图。取消可用，无 I/O、执行 Attribute 构造或全语法预扫描。
- SDK 候选 `9.0.312`，工具目录 global.json `rollForward=disable`，由 G0 实际确认；Tests `net9.0`、NUnit `3.14.0`、NUnit3TestAdapter `4.6.0`、Microsoft.NET.Test.Sdk `17.14.1`（官方来源已核对，restore 未验证）。不改 Unity 的 .NET 6/csc，也不因宿主 4.3.1 升级 Analyzer API。
- Directory.Build.props 将 bin/obj/TestResults 隔离到 `Temp/YLFramework.Analyzers/`，仅为两 csproj 添加精确反忽略，保留用户规则。NuGet 实际生成两份 packages.lock.json 作为受管输入，正常 `restore --locked-mode`；不手造 lock、不提交缓存，更不代用户 Git 收口。
- build.ps1 使用明确工具 cwd、脚本路径与实际 SDK，执行 locked restore、Release build/test，失败停止、默认不部署。global.json 从 cwd 而非 csproj 查找；首版 AssemblyVersion/FileVersion=`1.0.0.0`、InformationalVersion=`1.0.0`，源码输入指纹另记，不冒用旧 HEAD/日期。确定性构建与路径映射、同输入/锁/SDK 两次受控输出 hash 一致；失败不顶旧 DLL，只清自己输出。
- SDK tests 从六正式 Attribute 源码生成内存 Core 合同 reference/最小依赖 fixture，覆盖真实/外来同名消费者。先确保有效案例无编译器错误，再断言 ID/Warning/span/消息实体/数量/无 AD0001；常规测试与重建不依赖原 DLL/反编译目录，原版对照仅隔离重放。
- 在既有 XML 与 `Documentation/Modules/Attributes.md` 添加 Core 分析器章节：各规则错/对例、例外、限制、作用域和重建入口；保留 Foundation 显示特性，不新建文档站/设置窗，不提前接 6.5 Help。
- 标准配置为 SDK/IDE `.editorconfig` 单 ID、Unity 2022.3 `.ruleset` 单 ID、故意测试局部 pragma；不宣称 Unity Console 自动遵循 IDE 配置，不改 ProjectSettings、默认级别或关闭原分析器。

**性能：** 同一固定语料、references、parse options，对比不加载/加载自有 Analyzer，区分首次冷分析与稳定暖分析，记录输入规模、耗时和可获取分配；不设置脆弱的绝对毫秒测试断言。没有取得指标就说明限制，Unity 未开启 timing 不擅改 Preferences。正常原生编译、SDK 对照测量与实际编译器输入均须有证据；明显退化先定位真实热点，不能靠关闭规则通过。

### 5.1 注入与规则适用性分别验收

| 承载 | 输入预期 | 诊断预期 |
|---|---|---|
| Core.Runtime | 自有-analyzer，不是-r/运行引用 | 正常零误报、两owner合法 |
| 引用Core消费者/Core.Editor/测试 | 有Analyzer | 六正/反例对应、不因Editor/Test全免 |
| 同消费者外来同namespace/同名不同assembly | **仍有Analyzer** | 伪符号不触发，同时真实样本证明注入 |
| 无Core/仅Foundation独立asmdef | 无自有Analyzer | 保持其他Analyzer、非Core覆盖 |
| 间接引用/预定义 | 先核真实图/-analyzer，不猜全局/传递闭包 | 按可访问Core符号对照，不改游戏图 |

DLL仅Assets/YLFramework/Runtime/XFMain/Analyzers/YLFramework.Analyzers.dll，一套RoslynAnalyzer精确标签/普通平台禁用/非自动Runtime引用，新metaUnity生成、旧GUID不动。Core.autoReferenced=false不改，不复制第二DLL扩大scope。临时根独立现有asmdef树，保留程序集/ref/input/新消息，旧csproj或空Console不证明。

## 6. 实施顺序与门禁

当前已正式授权实施，以下按逻辑批次推进，每组先建立相关失败用例，再实现并读取新结果；不按逐文件、固定测试数量或提交次数安排。各门禁状态及已执行证据见第 7 节，不把授权或部分结果视为整个批次通过。

### G0：工具链与原版证据封口

- [x] 实际核验 SDK、NuGet restore 和 Unity csc；只读记录原 DLL 的 SHA-256、MVID、AssemblyRef、可加载 Analyzer/descriptor，核对新导出与原 DLL 身份及 Warn/Protected 异常分支。真实加载与分析行为见第 7.2 节。
- [x] 在唯一自有 Temp 承载，用**原 DLL**运行最小差异对照，记录原 ID、级别、span、没有诊断或 AD0001。不重编导出工程充当原版，不改原包，不靠放宽断言制造原版“通过”。

| 根因 | 最小对照重点 |
|---|---|
| base 绑定/调用体 | 正确与错误 overload、仅 Lambda/局部函数内调用、跨两级 override 标记 |
| caller 与符号身份 | Awake 中直接调用及 callback 内调用、getter/initializer；无关 typeof Attribute 的 Warn |
| 构造 | 显式与目标类型 new、自身池工厂、继承标记 |
| 持有/注册 | 已知 Field/Property/Event 注册的实际触发，数组/多层容器/delegate 的原版行为与 YL 存储合同差异 |
| 写入 | 简单写与 %=、out、解构；嵌套/泛型禁止者；异常成员过滤 |

**通过条件：** 可执行环境与依赖兼容，所有影响正式设计的未知项得到元数据/IL 或行为证据，并按层级更新第 2 节。工具权限、依赖缺失或未授权安装阻塞时停止相关路线并报告，不能包装命令/子代理绕过。原版行为要求改变已批准公共契约时先提出证据，不直接照抄。

### G1：SDK 基础与 YL0001～3

- [x] 建立两项目、必要配置与精确忽略例外，真实生成依赖锁；验证 Release build/test 和可重复入口。SDK 测试独立于原 DLL 和反编译目录运行。
- [x] 实现符号身份、override 标记、直接调用体、三个 caller 选择器、准确消息与去重。正反案例覆盖泛型/别名/同名外来类型、静态接口目标与具体实现边界、accessor/initializer、混合 null 名单及生成代码，不建立接口 AND 或 CFG 执行证明。

**通过条件：** 每 ID 合法/非法案例正确，精确 span、Warning、实体/改正消息、诊断数量正确；正例没有编译器错误，无 AD0001，取消和生成代码策略明确。既定 base 存在性不被隐式改成可达或全路径合同。

### G2：YL0004～6 与合法内部 owner

- [x] 构造覆盖自身工厂、外部 new、假 owner、类约束 new T 与无界泛型边界；池和事务豁免仅作用于正确成员符号/形态，不修改 Runtime 或全免程序集。
- [x] 存储覆盖数组/tuple、Box<Box<Args>>、外层泛型 Inner、phantom、仅第二参数存储、static-only wrapper、互环有/无值叶、伪全名容器、字典键/值与结果载体；未知 metadata 保守、不崩溃、缓存不串上下文。
- [x] 写入覆盖实例 field/property/indexer、各类赋值/解构/ref/out/只读与结构体引用边界、开放/闭合泛型禁止者及合法读取/非禁止修改者。一个问题每 ID 只一次，不排除全部 SDK tests。

**通过条件：** 六 ID 工具侧全部通过，LoadViewArgs.Pool 工厂和 LoadViewRequest<T>.args 合法；测试中的故意保留/构造只用第 3.3 节局部 pragma，关键 Runtime 与契约测试不被修改或削弱。

### G3：Unity 正式接入与配置闭环

- [x] 仅在 SDK Release/tests 通过后部署自有 DLL，由 Unity 生成新 meta 并配置精确标签/平台；核对新响应文件和原生编译消息，正常 Core Runtime/合法消费者无误报、加载失败或 AD0001；Core EditMode 59、PlayMode 185 项实际通过。
- [x] 按第 5.1 节矩阵，Core 本体/临时消费者实际各触发六负例，再换合法代码编译为零 YL warning。伪同名符号仍注入但不触发，无 Core 的独立程序集不注入；输入与诊断适用性分别记录。
- [x] 临时叶子消费者的唯一 ruleset 单独调整 YL0002 为 Error/None，核验当次 -ruleset 与结果，不改根配置或游戏图。Error 前先建立无 Core 引用的已编译观察器/菜单/指纹 watchdog；实际自动恢复和新的原生 csc 默认 Warning 已验证。

**通过条件：** 当前 Unity 2022.3.62f3c1 真实输出证实 ID、位置、默认级别、作用域和配置；按 ID/输入区分 YL/XF/Logger，原分析器保持。配置恢复后重新编译正常，不用历史 csproj/MCP 缓存日志替代。

### G4：YL0007 实际收益门禁

- [x] 最小正反 View 在加载完成且 active 后执行 disable→enable，观察正式钩子、事件序列/订阅身份与公开 Module.GetViews；初次 Load 的 ResumeEvents 不替代再启用，停用无 callback 不等于退订。
- [x] 原生 Destroy 后核对会话索引及及时关闭帧，测试自有 Module 正式关闭管线收尾，复用现有 pool/update host fixture 与精确反射；未新增 Runtime API/InternalsVisibleTo，不保存场景或创建 Bundle。

**通过条件：** 真实 Unity 消息遮蔽后果确认后，才增加 YL0007 的 SDK 红绿及 Unity 诊断案例；反证或无法可靠实测则候选延后，六基础规则不得因此遗漏，不改 Runtime 制造效果。

### G5：清理、重建与交付

- [x] 按完整path/SHA/GUID核对后仅清本轮探针/资产/meta/SessionState，清理后新Core原生messages=0；25个实际输出PE无新增Analyzer/Roslyn运行引用，CoreEdit59/Play185最终再通过。
- [x] 统一修复后源码/锁/SDK两次controlled Rebuild与部署DLL同hash，普通/压力实测无异常；diff、meta、ID、XML/使用说明与最终DLL一致，一次独立review及红绿修复完成。结果见第7.7节，等待用户验收/Git，不自动进入Player/6.5。

**探针保护：** 临时根预先不存在、场景无未保存变化、Editor 无编译/Play 切换；记录唯一 owner 与文件/meta 完整指纹，SessionState 跨 reload 不只记路径。删除前核查夹带/外部改动，不按旧路径递归删后来内容；故意 Error 前先验证独立恢复方式。清理后真实重编译，证据含时间、程序集、源码身份。复用 6.3 的恢复经验，不顺手修改 Resource 工具。

## 7. 技术完成与剩余证据

技术完成须G0原版/重要差异实证，六ID/两owner SDK与Unity正反通过、已启用额外收益实证、无AD0001/loadfail/包游戏写入、源码锁入口重建DLL、说明配置一致/无probe残留。达成才建议6.5，用户验收/Git；完整Player/Help/导出综合仍6.5。

**当前已完成：** G0～G5；六基础规则及实证启用的YL0007、自有SDK/真实Unity接入、配置、XML/使用说明、一次独立review及统一修复、性能、受控重建与最终清理。最终SDK109/109、CoreEdit59/59与Play185/185，最终DLL和两次Rebuild同SHA，清理后的Core原生csc messages=0。**待用户操作：** 无；技术验收已通过，Git 收口已获单独授权。**明确未声称验证：** Player/IL2CPP/阶段6完整导出、实际Rider/VS UI、所有mid-summary取消时机；这些限制不由SDK/PE检查冒充，6.5未启动。

### 7.1 本次前置执行与阻塞（2026-10-13）

- 已重新读取项目 AGENTS、总体及独立计划、ProjectVersion、manifest 和六正式 Attribute/直接消费者。实际分支 `fusion/6-editor`、HEAD `8065d65c4dddd2b0aebfa43f86f9a3222e16589d`；`git ls-remote` 确认远端阶段分支同提交，远端 `integration/xf-yl` 仍为 `7db580635661165acb42826d3a66f885f9809930`。开始已有 `.gitignore`、总体计划修改及本计划未跟踪；保留用户改动，未执行 Git 写操作。
- `dotnet --info` 本次成功：默认 SDK `10.0.102`，已安装 `9.0.301`、`9.0.312`、`10.0.102`；这只证明 SDK 可执行及列表，不证明工具目录已锁定 `9.0.312` 或 restore 成功。没有安装/升级环境。
- 已连接的 Unity 确为本工程 `2022.3.62f3c1`，`isPlaying=false`、`isCompiling=false`、`isUpdating=false`、`scriptCompilationFailed=false`，活动场景 `Assets/Scenes/Main.unity`、dirty=false。CompilationPipeline 当前 Core 的 Analyzer 输入仅含原 Logger 与两个 Unity generators，无自有或原 XF 六规则 DLL。本次未触发重新编译、Play 或测试。
- 原 Package DLL 经只读 PE 元数据核验：SHA-256=`2DA98BA5ADA9F21074588BC54593DE6B7C0DBC8C7F2613A16709EC5EE00684D7`，MVID=`f79a928e-68cd-4992-819e-1c8971792de8`，Assembly=`XFGameFrameworkCodeAnalyzer 1.0.0.0`。AssemblyRef 为 `netstandard 2.0.0.0`、`Microsoft.CodeAnalysis 3.3.0.0`、`Microsoft.CodeAnalysis.CSharp 3.3.0.0`、`System.Collections.Immutable 1.2.3.0`。实际原版引用版本为 3.3，不等于自有已批准的 3.8.0 API 起点，也不证明其实际加载/行为；此前官方 3.8 枚举核对仍是独立静态依据。
- 尝试用 `apply_patch` 建立 G0 独立临时项目：工程 `Temp/**` 被项目 `edit` 策略拒绝；改用工具明确预批准的 `C:/Users/Kano/AppData/Local/Temp/kilo/` 后，`**/*.csproj` 仍被项目 `edit` 策略拒绝。两次补丁均未落盘，已核验两个候选目录不存在。未使用其他写工具、改扩展名、包装命令或子代理绕过；未修改权限/工具配置/忽略规则。
- G0 依赖的隔离 restore/原 DLL 行为实验及两个正式受管项目均受上述 csproj 权限阻塞，本批在产品实现前停止。必要恢复条件是工具策略允许编辑两个正式 csproj，并允许 G0 隔离承载使用的 csproj；仅修改 Git 忽略规则不会解除工具权限。恢复后从 G0 的 restore、实际 AnalyzerFileReference 加载与差异行为核验继续，不将本次只读结果冒充通过。
- 本次仅增量更新本计划及总体导航的授权/阻塞状态，产品源码、DLL/meta、测试、原 Package、游戏/场景/资源、Packages/ProjectSettings 均未修改。无 Commit/Push/Merge/Tag/Reset/Clean/切分支；未进入 G1～G5 或 6.5。

### 7.2 权限恢复与 G0 实证（2026-10-13）

- 用户自行修改 `.kilo/kilo.jsonc`，本轮只读核对并实际确认文件编辑及命令工具已放行，未修改权限配置。取消已安排的配置等待，按用户新指令立即继续。HEAD 与两个远端提交仍同第 7.1 节；用户 `.gitignore`、配置及计划既有改动保留。
- 唯一自建 `Temp/YLAnalyzerG0` 三个源码输入实际创建成功；`dotnet --version` 为 `9.0.312`，Roslyn `3.8.0` restore 成功，原 DLL 在独立 .NET `9.0.14` 进程运行。没有编译反编译工程，也没有把原 DLL 复制进产品。此临时承载不进入版本控制或正式重建流程。
- AnalyzerFileReference 实际发现并构造六个 Analyzer，包含 internal DisableNew；没有加载失败。descriptor 与新导出一致：Warn 是 Warning，其他五条 Error；原 ID 均同第 2.2 节。SHA-256/MVID/AssemblyRef 与第 7.1 节相同，六类型及注册行为与用户导出对应。
- 15 组最小有效 C# 对照均先确认无编译器 Error，再读取原 Analyzer ID、级别、span 与消息。正确 base 无诊断，缺失 base 产生 MUST_CALL_BASE_ATTRIBUTE；错误 overload、仅 Lambda/local 中 base 及两级标记链均被放过。Awake 直接与 Lambda、getter/initializer 均被放过，Wrong 直接调用产生 RESTRICTED_CALL_ATTRIBUTE。
- 无关 OtherAttribute(typeof(C), "other") 确实触发 WARN_IN_CLASSES_ATTRIBUTE，证明异常谓词不只是导出文本问题。普通构造的自身工厂与外部 new 都产生 DISABLE_NEW，目标类型 new() 与继承类型 new 被漏掉。Field/Property/Event 实际入口均触发 METHOD_LOCAL_ONLY_TYPE；Action<Args> 事件被误当持有，而数组与多层容器漏报。
- 简单字段/属性写与 ref 触发 PROTECTED_HIERARCHY_MODIFICATION；%=、out、解构及嵌套开放泛型禁止者漏报。事件 += 也触发同 ID，确认异常成员过滤分支的实际后果。null Type[]/string[] 的有效标记产生原 Warn/Restricted 的三条 AD0001 NullReferenceException；这属于原版已确认差异，不压制日志、不要求原版通过 YL 的新契约。
- G0 结论：影响设计的主要未知项已由真实加载/行为封口，维持已批准的 YL0001～6/Warning、base 存在性、独立调用体、符号身份、真实存储与窄 owner 方案，不复制原缺陷或按原 Error 改契约。当前 Unity 仍非 Play、非编译/导入；未部署自有 DLL，G1 开始建立受管 SDK 与红绿诊断测试。

### 7.3 G1 实证（2026-10-13）

- `Tools/YLFramework.Analyzers` 下两个 SDK 项目锁定 SDK9.0.312 与 Roslyn3.8.0，bin/obj/TestResults 全部指向自有 Temp；两个 NuGet lock 由真实 restore 生成并已读取核对。测试宿主的 TestPlatform 支持库版本与 Analyzer 编译依赖分别由其锁记录，不把宿主的传递依赖版本当作使用新版 Roslyn API。
- `.gitignore` 仅追加两个手工 csproj 的精确反向例外，保留用户原改动及 Unity 生成项目全局忽略；没有修改原导出目录、Packages、ProjectSettings 或游戏源码。
- 先运行空入口的 29 项有效诊断测试，19 项因缺少目标诊断而红、10 项阴性/允许例通过；不是夹具编译错误。实现后 29/29 绿，再补真实 alias、限定属性、索引器、无 Core 和错误绑定边界，34/34 绿。断言包括 ID、Warning、精确 span、中文关键实体/原因与无 AD0001。六个 Attribute 直接取正式源码编译为 `YLFramework.Core.Runtime` 引用，不复制等价定义；最小 View/EventManager/Model/Args 壳仅用于工具侧模块合同，实际完整 Core 接线仍须 G3。
- 当前已实现 YL0001～3；六个稳定 ID/Warning descriptor 已固定，YL0004～6 尚未注册，不能把 G1 当作六规则完成。缓存只活于单 compilation，正式 marker 仅从唯一正确程序集解析并按符号比较；override 仅走槽链，调用体与声明选择器、有效 AttributeUsage、Warn 合并去重已按契约实现，没有接口 AND/CFG/全局 symbol cache。
- 唯一 `build.ps1` 已实际重复运行：PowerShell7.6.6、SDK9.0.312，locked restore、Release build 0 Warning/0 Error、SDK tests 34/34，默认未部署。TRX 覆盖仅为自有 Temp 的最新结果。入口在验证成功且目标未被并行修改时才显式 `-Deploy` 原子发布 DLL、保留已有 meta；Temp 中记录逐输入 SHA-256/SDK/版本。本批临时 DLL SHA-256=`4BE870AA384FA6B8F173C066B928F175197CFE9931873A94E0C6E505D32E941D`，G2 后须重新读取，不作为最终指纹。
- 实际 Unity compiler 为 Editor/Data/DotNetSdkRoslyn 的 PE AssemblyVersion4.3.0.0、Immutable6.0.0.0，FileVersion4.300.122.52613、ProductVersion4.3.1-3.22526.13+41a5af9d2c459a06c0795bf21a1c046200f375bf。Mono 的 AssemblyName.GetAssemblyName 对该文件报 Invalid Image，PEReader/FileVersionInfo 不加载程序集的核验成功；没有换/改 compiler。Unity scriptCompilationFailed=false，但自有 DLL 未导入/真实 csc 验证，仍须 G3。
- `git diff --check` 通过，新锁文件已核对；当前开始 G2 红例和实现。record 与跨规则组合在后续补足，G3～G5、最终独立复查均未执行。

### 7.4 G2 实证（2026-10-13）

- 三组 SDK 构造/存储/写入用例先运行；修正夹具误用 ProtectedHierarchy 的不存在 message 参数及 B 的 private constructor 后，83 项中 37 项因尚无 YL0004～6 而红、46 项允许/阴性通过，TRX 确认无编译器 Error/AD0001。随后实现三规则，未改任何 Runtime API 或 AttributeUsage。
- 存储先由正式 marker/数组/类约束/内外泛型实参候选筛选，再按需建立有限原定义图，只保留包含参数的实例 field/auto backing/构造 BaseType 值槽。内容位 false→true 固定点收敛，包含外层参数；构造符号单独查询，不合成无限嵌套泛型实例。纯互环/膨胀环无叶为阴性、有叶为阳性，static/signature/phantom 不当持有；真实 framework profiles 按 full metadata name/arity/assembly symbol 解析，伪全名不 seed。不可读 metadata/错误类型保持 unknown，不作为完整 negative 结果缓存；消息只给一条有限见证。
- DisableNew 检查普通、目标类型和类约束闭包 new T，允许标记类型自己的实现/词法嵌套工厂 Lambda，不把派生或同程序集当作 owner。MethodLocalOnly 的 field/property/record positional property 报一次，delegate/event/参数/返回/局部不追逃逸；池与 request 仅由实际 Core 成员 symbol 及 private/static/readonly/类型形态绑定，真实 owner 与各种近似/同名/其他形态均已 SDK 验证。
- ProtectedHierarchy 对实例 field/property/indexer 的 simple/all compound/coalesce/inc/dec/deconstruction 值叶与 writable ref/out/ref local/ref return 暴露检查；只读/in/static/读取/事件 += 与非禁止者不报。结构体值链回查保护成员，引用/数组/集合 mutation 边界停止；保护类型本身及其派生实现保留自己的状态修改权，不新增运行时权限保证。
- 首轮 82/83 绿，唯一 tuple 失败出现 AD0001；最小回归实测 Roslyn3.8 的原始 ValueTuple definition 为 IsTupleType=true、TupleUnderlyingType=null。归一化改为仅有下一层 underlying 时展开，保留原始定义；回归的语法 tuple、原始 ValueTuple、引用 Tuple 与 Rest 均通过，未吞异常/削弱断言。再补并发 compilation/构造实例隔离、取消 driver 后重跑及错误绑定，最新 87/87、无 AD0001。
- 已按第 3.3 节仅为既有两测试文件增加三处声明级 YL0005 和两处构造级 YL0004 pragma，中文说明池/lease/终态检查目的。实际 diff 确认没有修改字段类型、断言、测试生命周期或 Runtime，未排除 SDK/Unity 测试程序集。
- 唯一入口 locked restore、Release build 0 Warning/0 Error、tests 87/87；当前临时 DLL SHA-256=`E79EB9F8421C37FB755124E78F5305C119B9B3C99F6B20C27E8DC7C7E280F0C2`，receipt 在自有 Temp。默认未部署，G3/后续修改后重新读取指纹。`git diff --check` 通过。
- 本次源文件 pragma 导入后 Unity 非 Play/编译/更新且 scriptCompilationFailed=false，Main 场景 dirty=false；这只证明当前恢复正常，不证明自有分析器加载。进入 G3 的部署/真实 csc/作用域/规则配置门禁，G4/G5 与最终独立审查仍待执行。

### 7.5 G3 实证（2026-10-13）

- `build.ps1 -Deploy` 先 locked restore/Release 0 Warning/0 Error/tests87/87，再发布单一 DLL；Unity 生成 GUID=`471f81c4273ee66429677df44cbd6960` 及目录 meta。DLL SHA 同 G2 的 `E79EB9F8421C37FB755124E78F5305C119B9B3C99F6B20C27E8DC7C7E280F0C2`。API 配置 RoslynAnalyzer 精确标签、Any/Editor/Standalone false、ExplicitReferenced=true，读取 meta 确认无 Runtime 引入；程序集输出引用仍待 G5 实际 PE 检查。此 Unity 版本的 flag 是内部 `IsExplicitlyReferenced`，先反射核实真实 API 后设置；初次不存在的 public 小写属性只导致动态脚本编译失败，未修改 importer。
- 预检查唯一 `Assets/YL64AnalyzerProbe_8065d65` 根不存在，创建 Observer/Consumer/Foreign/NoCore/Predefined 独立源，新增 meta 由 Unity 导入。Observer 无 Core 引用，已先编译并跨 reload 收集 CompilationPipeline 原生消息/时间/Analyzer 输入；不是 Console 缓存或 SDK 夹具模拟。
- 2026-10-12T23:04:29～30Z 的真实 csc：Core 内自有临时源码、外部 Consumer 各产生 YL0001～6 每 ID 一条 Warning。实体为实际 View.Start/RegisterEvent、EventManager.AddEvent、LoadViewArgs 构造/持有及 Model.Id 写入，位置分别核对。初始 Consumer 源 SHA=`E0557B729FA18B8A87D787DCFDB41B9C3676F1A43195214EB8B62CB69E314EAC`，Core probe 源 SHA=`0355BF09397EAD157936D6679C8AFA3A8A73710247765F7545886F38AA2A41B8`；合法 GoodView 无误报，真实 Core 内两 owner 无诊断，既有 test pragma 生效。
- 实际输入矩阵：Core.Runtime、Core.Editor/Editor.Tests、Core.Tests.EditMode/PlayMode/Fixtures、ColorSelector、UI.Acceptance、Consumer/Foreign 均注入 YL；Foundation、旧 YL Runtime/Editor、游戏 MainModule/测试、NoCore、Observer、预定义 Assembly-CSharp 不注入 YL。Foreign 有六个全名同 namespace 的外来标记，只有预期 CS0436、无 YL；NoCore 与预定义无 YL。UI.Acceptance 与预定义仍保留原 XF/Logger 输入，按各自输入/诊断适用性区分，未动原包。
- 单 ID pragma：只在 Consumer 的 RegisterEvent 调用行关闭 YL0002，新编译只剩其余五个 Warning。去除该 pragma 后，叶子 `YL64.Probe.Consumer.ruleset` Action=None 同样只抑制 YL0002；真实 Bee rsp 含唯一自有 -ruleset、-langversion:9.0 与本 DLL -analyzer，未覆盖根 Default.ruleset 或改 csc.rsp。
- Error 最后执行：先加载独立 Observer/菜单并为预期 Error 文件 SHA=`2EBC790B49B81C88AA3ABF316CC6FFE4C866A1F44DD4011F6325AC8115837B3C` 装 30s watchdog，然后修改该自有 ruleset。原生 csc 仅 YL0002 是 Error，其余五 ID 仍 Warning；工具返回当前 compilation error。已编译 Observer 按内容指纹删除仅该 ruleset、重新请求编译，不依赖失败 Consumer 的新程序集。读取 Error 原生事件及恢复后 Failed=false/armed=false/file 不存在；后续真正修改自有源触发新 csc，六 Warning 全部恢复。
- 同内容 ForceReimport 被 Unity 增量优化为无 assembly 事件，断言拒绝把空日志当成功；只给自有消费者新增说明行再编译，23:16:44Z 真正六 Warning。Consumer 的真实 `.editorconfig` 单独设置 YL0002 none，Unity rsp 无 -analyzerconfig 且仍六 Warning；该临时配置已删除。另用 Tools 下单文件范围的实际 `.editorconfig` 经固定 Roslyn3.8 AnalyzerConfigSet 解析/范围/诊断配置测试，SDK88/88；证明格式及工具侧单 ID 语义，不声称实际打开 Rider/VS UI 验证。
- 删除 Core probe 前核对上述 SHA，AssetDatabase 删除其源码与 meta；Consumer 替换为合法代码。23:19:31～32Z 新的 Core/Consumer 原生编译各 messages=0，无 YL、加载失败、AD0001。既有 PlayMode CS0672/CS0618、Acceptance CS0108 与 Foreign 预期 CS0436 如实保留，不顺手修改。
- 通过 MCP 的实际 Core.EditMode 59、Core.PlayMode 185 项通过，失败/跳过0。工具 Summary.TotalTests 分别1514/653为集合总数，不等于过滤后的执行数，按 PassedTests 59/185 报告，未声称全库测试。Play 后非 Play/编译、Main场景原路径 dirty=false；git diff --check通过，Packages/ProjectSettings/游戏/场景无修改。
- 后续 probe 根仍自有、24 个 files/meta，root GUID=`843c5b91402fc7f46a27fbec5625986b`；完整路径/SHA 清单保存 SessionState `YL64.Probe.8065d65.manifest`，恢复输入已解除。G4 修改 probe 后须更新清单，G5 删除前逐项核对夹带/外部变更。未自动技术验收/Git 收口或实施 6.5。

### 7.6 G4 实证（2026-10-13）

- 唯一 probe 根增加 PlayMode asmref，显式绑定已有 Core.PlayMode tests GUID，复用内部 CoreClassPoolScope/CoreUpdateHostScope 和公开测试 Module；不新增 Runtime 入口/friend assembly。不借游戏对象/用户场景，直接 Module.AttachView 纳管四个测试自有 active 实例，避免造 Prefab/Bundle/池映射；正式 Module.Close + fixture 终态验证收尾。
- 已原生 PlayMode 执行一组正常、只遮蔽 Enable/Disable/Destroy 的 View；加载后均 active/loaded、正式 enabled=1、各一订阅并各接收一次事件。正常 disable 后无 token，Enable-shadow 同样正常退订，Disable-shadow 的原 token list 保留且 disabled hook=0；停用时均无 callback，不能把守卫静默当退订。
- 重新启用：正常 enabled=2/新身份，Enable-shadow ownOnEnable=2 但正式 enabled仍1/token为空/不再接收；Disable-shadow enabled仍1、原token身份未变但恢复callback。公开 GetViews 此时四个，证明不是初次 Load 的显式 ResumeEvents 效果。
- 原生 Object.Destroy 后：正常关闭帧14、Destroy-shadow own消息帧14/正式关闭帧15，确认及时清理被下一帧索引兜底替代。查询前当次自动 prune 已完成，session null/index0，公开 GetViews为空；这是真实修复能力，不制造长期泄漏。记录原生证据并加入相对帧断言，定向测试最新1/1，通过日志含 enableShadowEnabled=1、disableShadowDisabledBeforeDestroy=0 和三个关闭/销毁帧。
- 初次 testClass+assembly 过滤返回 Unknown/执行0，未当通过；改为唯一 fully-qualified testMethod 实际执行1项。工具 TotalTests654仍是集合数，不用于通过量。重启/切Play后 Main原路径dirty=false，无业务Module残留。
- SDK 新12组边界：修正最小合同壳缺少三正式virtual hook后，100项中6个有效 YL0007 例因未实现而红、94项通过，TRX无编译器Error/AD0001；实现后100/100。规则只绑定真实 Core View，在自己的无参/非static/非generic void ordinary method 上报一个 Warning，覆盖private/new/virtual/override/间接泛型/嵌套真正派生/真实alias；静态/有参/generic/非void/属性/local/普通Mono-like/外来View/普通nested helper不报，不禁Awake/Update/正式hooks。
- 经SDK门禁后重复部署遇到真实 PowerShell string/null 绑定：File.Replace 的 `$null` 被转换为empty backup path，失败停止且旧E79...DLL SHA实测未变、stage清理；改用 `[NullString]::Value` 后原子替换成功，不移除失败保护。100/100/Release0 Warning0 Error，当前 DLL SHA=`A229977D36E21E9792C0BD1E224E462D9B95776A57BA17A4095ED74128CFD769`，GUID仍471...；后续G5有改动须再读取最终指纹。
- 新DLL真实 csc产生仅三条YL0007 Warning，定位临时三个声明(27,22)/(32,22)/(38,22)，中文实体/建议hooks一致，无加载失败/AD0001；正常Core和其余合法代码未引入额外YL。未修改Runtime/原包，不靠显式SendMessage模拟原生生命周期。
- 指纹清单已按G4修改更新，29个files/meta，SessionState完整清单与root GUID仍自有；非Play/编译、Main场景dirty=false，git diff --check通过。进入G5的说明、重建/性能、独立复查与最终清理，不自动进入6.5。

### 7.7 G5 收尾与独立审查（2026-10-13）

- 六 marker 仅更新中文 XML 契约，默认 Warning、直接调用/存在性、typed名单/继承、真实存储/ref边界据实现编写；没有改构造/AttributeUsage/运行代码。既有 `Documentation/Modules/Attributes.md` 增量补七规则独立错/正确对照、owner/profiles边界、Unity/IDE单ID配置和唯一重建入口，没有新建过程报告/README。
- 一次 fresh-context general 子代理独立只读审查：实际SDK9.0.312/Roslyn3.8、隔离100项旧合同全绿，无Critical；静态报告五Important（局部initializer caller、class约束结构化存储、readonly ref重绑、重复owner AD1、发布检查/替换间隙），另partial ABI误报/record可能重复风险。子代理未编辑产品/Unity/Git，未使用模型覆盖或再派review；隔离Temp正常测试输出不纳入产品。
- 父任务先加7项实际回归，7/7红：局部initializer多4个误报、class约束漏2个、readonly多1个、两个重复owner真实AD0001、partial应1却3条、record应1却2条。有效例无编译器Error，重复owner用CS0102确认错误符号后只断言不抛AD1。重级裁定partial/record为正常C#功能误报/去重缺陷而非风格minor，统一修复。
- 一次修复pass：Caller只在真实field/property/event initializer截断；Candidate/Ensure/Witness一致走class/typeparam约束闭包，seen集合终止参数环且不钻接口；ref重绑按实际readonly target识别；owner恰有一候选才绑定、不吞CS0102；erased partial跳过、有实现partial只报定义一次；record Parameter入口仅处理实际由该Parameter合成的property。各项定向RED→GREEN，全部SDK109/109、无AD1/加载失败，未降低默认/削弱原断言。
- 发布安全裁定：原子replace不等于任意外部writer的CAS，不作该保证。唯一入口全程用自有Temp `entry.lock`/FileShare.None串行build/test/deploy；stage后再次SHA检查已观察到的变更，文档明确发布期间不由其他工具写DLL。新增真实进程回归先红（第二入口进入SDK/test）、再绿（在SDK及共享输出前拒绝），不用mock锁或忽略失败。合作范围之外的外部writer仍不是原子CAS，此为明确限制。
- 独立review的set-aside逐项裁定沿批准边界：YL0001不增加CFG/全路径证明；接口不对实现作AND/Lambda local独立、computed property属于显式成员合同；未知metadata/非泛型隐藏图/delegate签名/phantom/static-only/未列profile接口/擦除反射/跨过程不扩成通用所有权；用户配置和既有改动不当产品缺陷；OnDestroy不声称永久泄漏；6.5/Player综合/Help导出/Plugins许可不越界。预取消driver恢复/并发及token策略已测，mid-summary各取消时机没有运行覆盖，不声称全取消点证明。没有第二review，没有遗留Critical/已复现Important或另列deferred style minor。
- 最终唯一入口 locked restore + Release **0 Warning/0 Error** + SDK **109通过/0失败/0跳过**。修复后再执行两次 Analyzer `-t:Rebuild`，只重建自有输出；SHA均=`CE8DA2521AEE3C879022831D0CB1EB9F7073AAB243A9CFB05232CD40F4DC2968`，与部署字节完全一致。AssemblyVersion/FileVersion/InformationalVersion=`1.0.0`，MVID=`97583368-7546-43f8-8c64-3757e34529d1`；PE refs仅netstandard2.0、CodeAnalysis/CSharp3.8、Immutable5，不含Unity/Core/Workspaces。逐输入SHA receipt在自有Temp，最终receipt SHA=`1FCB2CCC3840747104C217E11A99DA416CC794AE01FE7AF5DCE372A893553EB4`。
- 最后一轮固定性能语料（不含恢复/工具启动，metadata/parse准备排除；每轮同树引用的新compilation，三次warm，全进程线程分配，不当Runtime/Unity Profiler）：正常24类型/267行/2303nodes，cold off/on=21.40/45.91ms、5.42/6.12MB，warm中位=11.00/40.89ms、2.22/5.58MB、72预期YL；压力350类型/9803行/86823nodes，cold=192.93/250.75ms、28.74/79.65MB，warm=107.72/225.91ms、21.88/66.44MB、1050预期YL。Warm附加约30/118ms，包含密集故意诊断开销，不作绝对阈值或开销为零承诺；样本在Temp TestResults/performance.json，无AD1/Error，不扩性能平台。
- 清理：原 `YL64AnalyzerProbe_8065d65` 根按29个path/SHA+GUID/reparse检查后AssetDatabase删除全部源码/meta，原CoreScope源预核SHA删除。G0三个输入读取确认后用精确工具例外删除；bin/obj仅在54个生成名/无reparse/无未知夹带核验后定向清理，未清SDK/NuGet/其他Temp目录。只保留正常工具/独立review的忽略构建/TRX输出。
- 最终编译不沿用空日志：DLL变动增量Refresh及一次Unity script compilation-cache rebuild完成但动态capture无新Core事件，未当通过，也未删除Library或启动第二Editor。另建唯一独立 `YL64FinalVerify_8065d65` 观察器及仅合法local initializer/readonly ref重绑/erased partial的Core源；00:34:14Z真实Core/Editor/EditMode/ColorSelector messages=0、无YL/AD1/CS8032，源SHA=`CB0B7EF23DF38A7839822E4832ED8EE2EF8BF37DA721C91CF7E75029B50E4AF3`。
- 预核该SHA删除最终Core源/meta；00:35:37Z再取得清理后的新Core/Editor/EditMode/ColorSelector messages=0。Play既有CS0672/CS0618、Acceptance CS0108如实保留，未修无关警告；最终观察器5个files/meta按完整fingerprint/GUID=`333f5eb8509817d4c81e76763b763beb`核对删除，全部实验/恢复/capture SessionState精确解除，不清全局Console/EditorPrefs。
- 清理后再实际Core Unity **EditMode59/59、PlayMode185/185，失败/跳过0**；集合总数1514/653不冒充执行数，G4定向1/1沿用当时有效实证。25个当前Core/Foundation/Runtime/Editor/测试/游戏输出PE无新增Analyzer/Roslyn运行引用。只留一个DLL/meta与目录meta，GUID471...保持且扫描唯一；Any/Editor/普通平台禁用、explicitReferenced1、精确RoslynAnalyzer标签。7份源码namespace均YLFramework.Analyzers，无新原前缀/API入口残留。
- 最终Git HEAD仍`8065d65c4dddd2b0aebfa43f86f9a3222e16589d`，阶段远端同提交、integration仍`7db580635661165acb42826d3a66f885f9809930`；原Package DLL SHA仍G0的`2DA98BA5...84D7`。diff/check、新文件/锁/meta与两csproj精确忽略反例已核对；用户kilo.jsonc/.gitignore/原计划改动保留，本代理未改权限，游戏/场景/Packages/ProjectSettings无改动；Main原路径dirty=false且非Play/编译、scriptCompilationFailed=false。
- 至此停止。Player/IL2CPP、实际Rider/VS UI（仅真实editorconfig格式/固定Roslyn解析语义验证）、所有取消时机及阶段6完整导出不由上述证据替代。阶段6 **IN PROGRESS**，6.4等待用户验收/Git收口、6.5未启动，无Commit/Push/Merge/Tag/Reset/Clean/切分支。

依据：[阶段总体契约](xf-yl-6-editor.md#53-诊断契约)、[Unity2022.3 scope/ruleset](https://docs.unity3d.com/2022.3/Documentation/Manual/roslyn-analyzers.html)、[Roslyn3.8 NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/3.8.0)、[固定revision SyntaxKind](https://raw.githubusercontent.com/dotnet/roslyn/8de9e4b2beba5b7c0edd6f1e6a4f192a51fdc872/src/Compilers/CSharp/Portable/Syntax/SyntaxKind.cs)、[SymbolKind](https://raw.githubusercontent.com/dotnet/roslyn/8de9e4b2beba5b7c0edd6f1e6a4f192a51fdc872/src/Compilers/Core/Portable/Symbols/SymbolKind.cs)、[Operation接口](https://raw.githubusercontent.com/dotnet/roslyn/8de9e4b2beba5b7c0edd6f1e6a4f192a51fdc872/src/Compilers/Core/Portable/Operations/OperationInterfaces.xml)、[原包仓库](https://gitee.com/xianfengkeji/XFGameFramework)。Context7专用工具当前不可用；不用新版API/反编译承载代替版本证据。
