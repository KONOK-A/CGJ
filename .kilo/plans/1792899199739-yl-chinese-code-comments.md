# YLFramework 中文源码注释实施计划

> 供执行代理：使用 `executing-plans` 或适用的 `subagent-driven-development` 执行本计划。先完成样板并停止，用户确认样板后才能进入全量阶段。本计划本身不代表开始实施的授权。

**目标：** 让框架使用者通过注释正确调用 API，让 Unity 基础较弱、掌握基本 C# 的学习者能够理解框架职责和关键实现，不做逐行翻译式注释。

**方式：** 公开契约保持已认可的中文 XML；过程注释采用“关键处讲透，其余留白”，同一要点择一处解释，不为每个阶段重复配说明。每个私有方法仍有职责说明。仅修改注释，当前样板收敛不允许修改两个模板内容。

**环境：** Unity `2022.3.62f3c1`、C# 9；遵守现有 `AGENTS.md`、`Assets/YLFramework/CodingStyle.md` 和 `.editorconfig`。不升级依赖或工具链。

**需求来源与状态：** 已按用户确认的 A3 深度与密度完成 B1-B7 全量阶段。726 个 C# 源码逐文件检查，660 个补充或修订注释，其余保持已有充分说明；全量不变性、格式、实际 Unity 编译、分批测试及独立复核已完成。B4 原失败已保持原断言全部复验通过，B7 跨域回执 128/128 通过。未提交、推送或发布。

## 1. 已收敛的设计树

```text
YLFramework 中文源码注释
+-- 改动界限 [Q1, Q4]
|   +-- 只增改注释，不改行为、API、命名、数据、程序集配置
|   +-- 不新增教程，不改现有 Markdown 或 Help 文字内容
|   +-- 发现缺陷单独报告，不顺手修复或重构
+-- 读者与内容 [Q2, Q3]
|   +-- Unity 基础较弱；解释难点，不重复基础常识
|   +-- 使用者：职责、调用条件、输入输出、失败与释放责任
|   +-- 学习者：执行阶段、模块协作、生命周期与设计原因
+-- 源码范围 [Q5, Q6]
|   +-- 纳入 Assets/YLFramework/{Runtime,Editor,Integrations,Samples}
|   +-- 排除任意层级的 Plugins 和 Tests
|   +-- 额外排除 Runtime/Utilities/Json
|   +-- 额外排除 Runtime/Utilities/ZipTool/ICSharpCode.SharpZipLib
|   +-- 不扩展到 Tools、游戏业务、外部 Packages 或二进制文件
+-- 注释覆盖 [Q8, Q9, Q10, Q15, Q17]
|   +-- 类型有职责说明，public/protected 供调用或继承的成员有 XML 契约
|   +-- 每个私有方法至少一句职责说明，不逐行注释方法体
|   +-- 关键处保留因果、状态顺序与失败边界，不逐阶段铺陈
|   +-- 同一要点择一处解释，避免 API、字段、导读和语句旁重复
|   +-- 内部解释放在实现附近，不挤进公开 API 提示
|   +-- 关键入口适量提供真实 API 的短示例
|   +-- 保留准确的既有中文注释，修订不准确或纯英文的自然语言说明
+-- 生成链 [Q7, Q12]
|   +-- 完善 Module 内联模板和 FSM 外部模板的注释
|   +-- 保持 Resource 常量输出、校验头、asmdef 完全不变
|   +-- 既有生成文件不手改，不为本次注释重新生成真实业务输出
+-- 推进与验收 [Q11, Q13, Q16, Q17]
    +-- API 与 A3 过程样板均已获用户确认，按此标准全量推进
    +-- B1-B7 持续执行，不把批次进度误报为全量完成
    +-- 内容审查、非注释代码对比、XML/格式、Unity 编译及定向测试
    +-- 测试仅操作自有临时产物，不覆盖用户内容，不改测试源码
    +-- 不默认进行全量 Player、HybridCLR 构建或打包发布
```

## 2. 注释标准

- 所有纳入范围的源文件均需检查；已有说明充分的文件不为制造 diff 而修改。
- 类型说明交代其职责和在模块中的位置，不把类名简单翻译成中文。重要入口补充与协作类型的关系。
- `public`、`protected` 的类型、方法、属性、事件、配置字段和枚举值按真实用途补充 XML 文档；参数、返回值、泛型参数、异常标签按信息需要添加，不堆空标签。
- 对使用者容易误解的线程限制、同步回调、重入、完成/取消、初始化顺序、对象有效期和释放责任作明确说明。没有实现证据时不声称线程安全、自动解绑、自动回收或保证回调次数。
- 接口实现或重写成员契约相同时可用 `<inheritdoc/>`，只补实现差异；不能用它掩盖不同契约。
- 每个具名私有方法，包括省略访问修饰符但实际为 private 的方法、Unity 消息方法和私有构造器，至少有一句真实职责说明。它只是职责索引，不代表过程可读性已合格。简单方法保持简短，不复述每条语句；属性访问器、匿名函数、局部函数不机械拆成逐项 XML。
- API 层维持初版已认可的标准，原则上保留现有 XML；实现导读优先采用类内必要说明、字段旁说明及方法体内的 `//`，不把内部算法、状态流转和阅读路线堆到公开 API 提示中。
- 以“关键处讲透，其余留白”为密度原则。注释段以一两句为主，必要时才扩展；不要求每个方法同时具备导读、阶段说明和局部机制解释，也不要求每个阶段都配注释。
- 同一知识点只在最能帮助阅读的位置解释一次。字段说明、流程导读、局部注释与 API XML 不重复展开；从名字、短方法体或已认可 API 可直接理解的内容，不在过程注释中复述。
- 保留无法从直观代码推导的关键因果：状态为何先提交、回调为何可能重入、资源为何按特定顺序归还、失败究竟清理到哪里。压缩解释的表达，不把它删成“检查状态”“释放资源”等同义标签。
- 跨重载、属性副作用或协作关系确实难跟踪时，才保留一条简短阅读路线、必要状态定义或精确导航；不能把它们再在每个调用点重讲一遍。不复制协作类实现，也不为旁支行为展开通用 C#/Unity 教材。
- 关键状态与释放责任仍要能沿代码和必要注释连起来，且只说明实际保证，不把局部清理写成原子回滚。API 已经讲清的异常、迟到订阅等边界不必在方法体重复解释。
- 不机械规定注释总数、比例或统一行数，不靠把长段落挤成一行、增加大量空行来伪造精简。实际减少重复信息和非必要展开，让正常代码段可以连续阅读；简单方法和空钩子保持留白。
- 表达标尺：重入保护可写为“CanvasCreated 回调可能再次请求默认根，因此准备完成前禁止重入。”保留触发场景和原因即可；转义、归还等机制也按同样尺度解释，不需要在字段、导读和调用点各讲一段。
- “为什么需要”也必须有证据：重复检查只说明各自检查了什么，不为冗余步骤杜撰独立副作用或声称删除必然出错；标准语言语义可直接解释，未经证实的历史动机不写成事实。
- Unity 难点按当前实现解释，例如 fake-null、Domain Reload、协程与异步等待差异、共享资源与实例、生命周期或坐标空间；不为普通赋值和直观语法讲基础课。
- 关键入口可用 `<example><code>` 提供短调用示例，说明必要的前提及清理，引用真实 API，不给每个重载复制示例，不新增示例脚本或执行示例以初始化真实宿主。
- 注释自然语言以中文表达；准确的英文技术名词和标识符保留，陌生术语结合上下文解释。不保留整段纯英文自然语言，不添加中英文重复长段。
- 版权、许可证、来源声明和工具消费的指令注释保持原样，例如 ReSharper 指令；代码片段、URL、XML 标签不是需要翻译的自然语言段落。不批量添加文件头或迁移历史。
- 不修改 `Tooltip`、`Obsolete`、异常消息、GUI 文案等字符串，不添加 Attribute，不改预处理指令。唯一字符串内容例外是下述 Module 代码模板中的注释。
- 使用 UTF-8 无 BOM、LF 和现有缩进，避免无关注释之外的排版变化。不全库执行格式化 Apply。

## 3. 模板的准确边界

下表是整个任务的模板边界；阶段 A2、A3 更严格，ModuleTemplate 和 FSM 模板均保持初版内容不变。

| 对象 | 允许修改 | 必须保持 |
| --- | --- | --- |
| `Editor/Core/ModuleGeneration/CreateModuleCommand.cs` | 生成器本身的注释；`ModuleTemplate` 值中生成的注释 | 模板代码、占位符替换算法、字符串转义、生成 asmdef、路径与拒绝覆盖行为 |
| `Editor/Systems/StateMachine/Template/FSMStateTemplate.txt` | 生成类与方法的职责说明、必要的编写提示 | `[Preserve]`、基类、类名占位符、方法签名、空方法体和文件身份 |
| `Editor/Systems/StateMachine/Creator/` | 生成器源码本身的注释 | 模板路径、替换及写入逻辑 |
| `Editor/Systems/Resource/CodeGeneration/` | 生成器源码本身的注释 | 所有生成源码字符串、`ResourceReferenceOutput.Header`、hash 规则及 `AssemblyDefinition` |

- Module 模板是 verbatim string；新增注释中的双引号必须正确转义。保持四种占位符原有替换语义，新增说明不引入 `{ModuleName}`、`{ProjectName}`、`{BaseModule}`、`{DefaultStartUpScene}` 占位符。
- FSM 模板只有 `{0}` 替换；新增注释不引入额外 `{0}`。不移动模板及 `.meta`。
- 不给生成程序集增加 XML 输出或 Help 注册；模板中的 XML 仅改善生成源码本身的理解与 IDE 体验，不宣称自动进入 Help。
- Module 既有测试会对整个生成文本禁止 `OnExit`、`ClassObjectPoolManager`、`AllModules`、`AllBaseModules`、`using XFGameFramework`、`namespace Project.Game`；新增模板说明使用当前实际契约，避免引入与该模板无关的历史术语，不删改断言。
- 当前只读检查未发现 `Assets/YLFrameworkData` 下的生成 C# / asmdef，也未发现 `Assets/YLFramework/Examples`；实施前重新确认，不能据此接管后续新增内容。

## 4. 实施顺序

### 阶段 A：初版样板（已交付，API 层获认可，过程层需补强）

- [x] 重新检查 Git 状态、有效约定及 Unity 状态。保护现有计划、游戏资产、未跟踪夹具、`Assets/Test`、`Assets/YLFrameworkData` 等用户内容；不提交、切分支或清理无关文件。
- [x] 根据范围筛选所有源码和唯一外部代码模板，在本 Plan 中记录模块检查状态，不另建清单或过程文档。保存本批修改前的实际基线，不假定 HEAD 能代表用户已有改动。
- [x] 完成下面五个样板文件，核对所需基类、调用方和相关测试后再写注释，不按名称猜行为。

| 样板文件（均相对 `Assets/YLFramework/`） | 校准重点 |
| --- | --- |
| `Runtime/YLFrameworkRoot.cs` | 类职责、宿主与 UI 根、按需创建、Unity 生命周期、私有方法说明 |
| `Runtime/Foundation/Async/CustomAsyncOperation.cs` | 协程等待、完成与通知顺序、迟到订阅、取消实际语义，不能改成理想契约 |
| `Runtime/Systems/Resource/Lifetime/AssetLease.cs` | 保留既有优质说明，补齐公开契约，解释所有权与生命周期，适量示例 |
| `Editor/Core/ModuleGeneration/CreateModuleCommand.cs` | Editor 私有流程、公开生成入口、内联模板及正确转义 |
| `Editor/Systems/StateMachine/Template/FSMStateTemplate.txt` | 生成脚本的类与钩子说明，不填入逻辑或无关教学 |

- [x] 执行样板涉及的静态、格式、Unity 编译及定向验证，输出可审阅 diff 和实际验证结果。
- [x] 向用户交付样板并停止。只有获得样板风格确认，才继续阶段 B；不把本计划的确认当作样板确认。
- [x] 用户确认包括实现导读在内的最终样板风格，允许开始阶段 B。依据用户“可以。样板确认完毕，开始进入全量阶段”，不是仅依据初版 API 认可或代理复核。

### 阶段 A2：四个实现导读样板，完成后再次停止

此阶段仅改下面四个现有 C# 文件的过程注释及相关字段说明，不增加文件。保留已认可的 API XML、ModuleTemplate 的全部内容和 FSM 模板；不修改 Resource 输出、代码逻辑、字符串、签名或任何元数据。

| 文件及关键流程（相对 `Assets/YLFramework/`） | 必须能沿导读理解的过程 |
| --- | --- |
| `Runtime/YLFrameworkRoot.cs` 的 `GetPanelRoot` | 唯一宿主与准备中检查为何在前；已有根与本会话已登记不是同一状态；复用和创建如何分流；结构检查、登记、回调、复检的先后关系；成功状态何时记录，失败清理与 finally 各负责什么 |
| `Runtime/Foundation/Async/CustomAsyncOperation.cs` | 错误与进度怎样写入，结束标记怎样触发通知链；泛型回调收到谁；常规/静默完成怎样分流，回调异常发生时已改变什么、还有什么不会执行；迟到订阅与重复完成/取消如何接回这条链 |
| `Runtime/Systems/Resource/Lifetime/AssetLease.cs` | 共享资源记录、独立租约、诊断链表和引用计数各是什么；创建时怎样挂链并增加责任；归还时怎样处理头/中/尾节点，再使句柄失效并归还计数；其他持有者和缓存为何影响实际卸载 |
| `Editor/Core/ModuleGeneration/CreateModuleCommand.cs` | 菜单/OnGUI 与生成入口的关系；输入、命名与占用检查各防什么；模板占位符、字符串转义、单次替换与 asmdef 分别产生什么；目录检查和 CreateNew 各保护哪一层，第二份文件写入失败为什么不是整体回滚 |

以下是已核实的讲解前提，不是新增修改文件清单；辅助实现仍只读：

- `Runtime/Foundation/Singleton/Internal/SingletonSessionState.cs:20,80-102`：ExecutionSession 是引用身份令牌，在 SubsystemRegistration 重置时换新；StopExecution 只关执行门禁，不换令牌，不能把这里的会话写成每次切场景或 ResourceSystem 的资源会话。Domain Reload 关闭时静态状态需显式重置，已核对 [Unity 2022.3 官方说明](https://docs.unity3d.com/2022.3/Documentation/Manual/DomainReloading.html)；Root 的令牌比较只需解释本轮准备身份，不展开整套单例启动史。
- `Runtime/Core/UI/Windows/PanelManager.Loading.cs:80-81` 在没有显式根或模块绑定根时回到宿主默认根；因此 CanvasCreated 回调再次加载使用默认根的面板，可能在外层准备尚未结束时再次进入。`UIRootContext.ValidateStructure/GetLayer` 检查既有结构与配置，GetLayer(0) 不创建层；OnEnable 还会登记实际根，不能将根登记与宿主完整准备成功混为一谈。
- Async 的自类型泛型是框架用法约定，不是泛型约束强制 this 的运行时类型为 T；解释 `this as T` 的具体请求回传及失败返回 null 的标准语义，不能虚构编译器保证。完成路径是结果/进度写入、重复检查、标记结束、旧事件、新订阅、钩子；回调中断时状态已经提交。
- LeaseHead 的链表用于本仓库的活跃租约诊断，不负责决定卸载；LeaseCount 统计持有责任，InstancePins 是其中的子集。局部导读要区分摘链、句柄失效、归还计数与底层释放，并给出 ReleaseLease 的深入阅读入口，不将整个资源系统复制进本类。
- `ResourceSystem.cs:707-722` 只捕获 source.Release 的异常，Retention.Dispose 在 catch 之外；不得把底层释放概括成完全异常隔离，也不能承诺 Dispose 失败后所有动作均被回滚。顺序与实际清理范围按源码说明，不改实现。

- [x] 重新检查工作区，保存四个文件的当前初版基线及 `.meta`，额外保存 ModuleTemplate 值与 FSM 模板内容以证明本轮未动。初版编辑前的 SessionState 基线不能冒充本轮基线，不从 HEAD 覆盖或重写初版成果。
- [x] 定向核对关键调用链、状态生命周期和协作类；只解释当前流程需要的概念，不把“会话”“重入”“租约”“登记”等当作学习者已经理解的词。过程解释有源码依据，无法证实的动机不写成事实。
- [x] 按四个重点逐段补强，保留简单语句及短方法的留白。API 注释不重新润色；只有发现准确性问题才单独指出，不以本轮反馈为由改行为。
- [x] 执行二轮相关验证和下节的阅读走查，将实际证据增量记录在本 Plan。
- [x] 交付四个二轮样板，说明过程导读相较初版的具体提升，然后停止等待用户确认。未获确认不开始阶段 B。

### 阶段 A3：四个过程样板温和精简，完成后停止

用户认可 Q15 的理解深度，但认为 A2 稍密集；本阶段不是回退初版，也不是继续补充知识。范围仍是 A2 表中的四个 C# 文件，只删改普通注释及其必要留白；API XML、代码、字符串、两个模板和元数据保持原样。

- [x] 保存当前 A2 成果的新独占基线，含源码、API XML、模板值与 `.meta`；不使用初版前或 A2 编辑前的旧快照代替，不从 HEAD 重建文件。
- [x] 逐个知识点消除重复：Root 重点收敛字段、准备标记、会话和登记/通知之间的重复；Async 重点收敛导读、API 与完成/订阅/取消方法体的重复；Lease 保留有用的共享关系及摘链示意，压缩与其重复的长段；Module 保留转义、单次替换、引用与写入边界，删除普通检查和 API 契约的复述。
- [x] 在最有效的位置保留关键因果，其余直观步骤不配长说明；以一两句短段为主，必要的难点仍可展开。不强制按比例删字，不新添概念来填回压缩后的篇幅，不只做换行调整。
- [x] 检查阅读节奏与理解是否同时成立：主流程能连续扫读，不被频繁长段打断；重入、先提交状态、共享资源归还及失败清理等关键关系仍能理解。复核聚焦删重是否得当，不因发现更多可讲的外围知识而重新加密。
- [x] 完成下节不变性、格式和实际 Unity 编译验证，在本 Plan 区分新验证与沿用证据；交付四个精简样板后停止，用户确认前不进入阶段 B。

### 阶段 B：按模块推进，沿用最终确认样板

用户已确认阶段 A3，本阶段门槛已满足。每批同时核对实现、调用方与必要测试，按最终确认的 API 标准、过程深度和密度完成本批，再进入下一批；不重复润色已获认可的样板，也不每批再请求继续。若采用并行执行，仅分配无重叠源码范围，Unity 状态和验证由单一执行者串行管理。

| 顺序 | 范围（均相对 `Assets/YLFramework/`） | 关注点 | 状态 |
| --- | --- | --- | --- |
| B1 | `Runtime/Foundation`，对应 `Editor/Foundation` | Async、事件、绑定、IOC、池、单例、计时与更新；订阅、取消、借还和静态状态 | 完成，128 文件已检查，89 文件补缺 |
| B2 | `Runtime/Core`，对应 `Editor/Core`、`Editor/Help` | Module/MVC/UI/资源接入、调用关系、生命周期、Editor 与 Help/XML 边界 | 完成，73 文件检查，68 文件补缺 |
| B3 | `Runtime/Systems/Action`、`Audio` 及其 Editor 目录 | 调度、执行句柄、播放请求、取消、缓存和释放；统一现有英文自然语言段落 | 完成，82 文件检查，76 文件补缺 |
| B4 | `Runtime/Systems/Resource` 及 `Editor/Systems/Resource` | Catalog、地址、会话、加载、Lease、Scope、生成和发布的实际职责边界；输出字符串不变 | 完成，113 文件/110 补缺；核心 85 通过/1 条件跳过，原 9 失败全部复验通过 |
| B5 | 其余 `Runtime/Systems` 及对应 `Editor/Systems` | 按 Buff/DataAssets/Equipment/Http/Localization/Logging/Mission/Reddot/Save/Skill/StateMachine 等实际子目录闭环；共享作者工具也检查 | 完成，191 文件检查/补缺 |
| B6 | `Runtime/Utilities`、`Editor/Utilities` | 排除两个已确认第三方子树；解释成熟工具与框架集成关系、角色控制和检测算法、压缩包装层 | 完成，105 文件检查，95 文件补缺 |
| B7 | `Integrations`、`Samples`、`Editor/Distribution` 及剩余纳入范围的根文件 | 可选依赖与适用边界、接入顺序、示例所有权、编辑器导出职责；始终排除嵌套 Plugins/Tests | 完成，34 文件检查/31 补缺；跨域测试128/128通过 |

- [x] B1-B7 均已逐文件检查，私有方法职责覆盖无遗漏；无需修改的文件按原样保留。
- [x] 复核模块间术语、调用顺序和所有权说明一致；不将“Session/Owner/Scope”等跨模块名称简单当作相同概念。

## 5. 验收方法

### 内容与覆盖

- [x] 每个纳入范围的文件均已检查；类型职责、供调用/继承的 API 和每个私有方法按标准覆盖。准确的已有中文说明无需重写。
- [x] API 注释能回答使用问题，实现导读能帮助跟踪过程；不能以“API 齐全、每个方法有 summary、编译/测试通过”代替过程可读性验收。
- [x] 由掌握基本 C#、Unity 基础较弱的视角阅读，能沿注释说出关键入口的目标、主要阶段、协作对象和状态变化；不依赖先通读整个框架才能理解主路径。
- [x] 对关键保护条件、先后顺序和不直观语句，能回答“它处理什么场景、为何放在这里、去掉会产生什么问题”；答案来自当前源码，不是泛泛设计口号。
- [x] 能走完正常、复用/提前返回和失败路径，说明哪个标记已改变、哪个回调可能中断、什么已清理以及什么仍由外部负责；没有逐行翻译或未经证实的保证。
- [x] 正常代码段能够连续阅读；没有同一概念在 API、字段、流程导读和局部注释中重复展开，没有为直观语句强配段落。不能以更短行数代替真正删重，也不能为了减字删掉关键因果。
- [x] 独立复核先只读被改文件，再核对必要调用方；只把阻碍主路径且代码/API 无法补足的关键缺口列为必补，不要求穷尽所有外围知识。既不能靠审查者脑补判为可读，也不能借补课把精简后的注释重新加密。
- [x] 二轮至少走查：Root 首次/同会话复用/通知失败；Async 重复完成/回调抛异常/静默完成后迟到订阅；Lease 共享资源的多份租约及摘链顺序；Module 带引号的字符串输入/已有路径/部分写入失败。走查是沿源码解释，不为此触发真实资源或故意破坏业务输出，最终教学深度仍由用户确认。
- [x] 自然语言无独立纯英文段落；版权及工具指令等约定例外未被翻译。XML 的参数名、泛型名、成员引用和示例均与真实源码一致。

以上为实施者逐文件自检、工具覆盖与分批/跨批次独立抽样复核完成记录；可读性判断沿用已确认样板，不冒充对全部源码逐行的第三方认证或真实初学者实验。

### 证明非注释代码不变

- [x] 使用已安装的 Roslyn 做一次性词法/语法检查，校验代码可以位于允许的临时目录，不向仓库引入长期工具或测试平台。不能用正则剥除注释来替代词法分析。
- [x] 普通 C# 与本批修改前基线的非注释 token 种类与文本一致；预处理指令和非模板字符串保持不变。覆盖实际修改到的条件编译分支，不能只检查当前激活分支而漏掉 disabled text。
- [x] 获准修改模板的批次中，`CreateModuleCommand.cs` 仅放行 `ModuleTemplate` 字符串的变化，其余非注释 token 不变；使用相同代表性输入渲染前后模板，检查代码结构、转义、合法 shadowing 名称、空场景及普通数据中的占位符。阶段 A2、A3 不使用这一字面量例外。
- [x] 获准修改 FSM 模板的批次中，以相同合法类名替换 `{0}` 后语法有效且非注释 token 不变，不增加逻辑或新配置；阶段 A2、A3 仅检查模板内容未变。
- [x] Resource 输出相关字符串逐字不变，校验头、asmdef、真实生成文件和现有 `.meta` 均不变。

### 格式、编译与定向测试

- [x] 使用实际改动 C# 的明确路径运行 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Mode Check -Paths <本批文件>`；脚本依赖已有 PowerShell 7.6 与 SDK 9.0.312，环境不足时报告，不自动安装或升级。外部 `.txt` 模板单独检查编码与换行。
- [x] 通过当前工程唯一 Unity Editor 刷新并查看实际编译结果。检查新增 XML 文档的语法、参数和 `cref` 警告，核对新生成的 `Library/YLFramework.Runtime.xml`；DOTween 启用且本批涉及它时同时核对对应 XML。
- [x] XML/Help 相关改动使用现有 `ApiTests`、`ApiHelpTests`、`FrameworkHelpProviderTests` 的相关 EditMode 测试。只验证现有显式注册的 Help 范围，不声称所有新增 XML 自动进入 Help。
- [x] Module 模板改动运行现有 `CoreModuleGenerationTests`，保留转义、shadowing、空场景名和拒绝覆盖等断言。该组测试不编译生成 Module，必须另列模板语法/token 对比证据，不把它说成生成模块已通过 Unity 编译。
- [x] FSM 模板的基础证据是渲染后的语法和 token 对比；不默认运行会创建整套场景与资产的 Behavior 验收链。未实际编译生成 FSM 脚本时如实说明。
- [x] 注释中的关键短示例核对真实签名和前提；适用时用临时编译片段检查，不执行示例触发真实资源、全局宿主或场景变更。
- [x] 测试只创建自己独占且已确认无占用的产物，并只清理自身拥有的内容。预置路径被用户内容占用时停止该项，不清空目录、不覆盖、不改测试源码来绕过失败。脏场景不自动保存，不启动第二个 Unity 实例。
- [x] 未获得实际 Unity 结果时保留阻塞项；静态、辅助编译、Unity 编译、测试、运行时证据分别报告。样板阶段与全量阶段只重验受本批影响的证据。
- [x] 阶段 A2 与初版、阶段 A3 与当前 A2 成果分别比较：四文件全部非注释 token、API XML、两个模板和 `.meta` 必须不变，不放行任何模板字面量变化。重新执行本批格式、词法/语法及实际 Unity 编译检查；历史模板编译和 Help/Module/Async 测试证据在相应输入未变时可沿用并标注，不机械重跑全部 141 项。若意外涉及其他输入，先恢复本轮边界或报告冲突，再按实际影响重验，不能静默扩大范围。

## 6. 已知风险与范围外事项

- `Editor/Core/Analyzers/AnalyzerRuleCatalog.cs` 同时编译进 `YLFramework.Editor`，并被 Tools 分析器项目链接。它仍在本次注释范围，不能因排除 Tools 而跳过；其注释变化会改变构建收据的源文件哈希和未来导出包的该文件字节。不修改既有 Analyzer DLL，不声称它已由修改后的源码重建或与源码完成一致性验证。
- 不运行 `Tools/YLFramework.Analyzers/build.ps1` 或其 `-Deploy` 模式，也不生成、覆盖 `Temp/YLFramework.Analyzers/build-info.json`。即使不带 `-Deploy`，该脚本仍会写构建产物和收据；其收据记录构建 DLL 哈希，不保证与当前部署 DLL 相等。依据：`build.ps1:56-89`、`ExportUnityPackageTests.cs:44-70`。
- 只读调研时未发现上述 `build-info.json`，这是既有基线，不应误报成注释回归。导出、发布和源码/部署 DLL 的字节一致性本次未验收；不运行会实际生成临时包的导出测试，不声称这些门禁仍然通过。后续发布需要另行授权并重新核对构建、部署、收据和导出包。
- 存在未跟踪的用户数据和既有测试夹具，禁止通过“先清理环境”取得通过结果。实施前以实际工作区为准，不回退他人修改。
- 不修改测试源码、非计划 Markdown、许可证、配置、二进制、Prefab/Scene/ScriptableObject 或已有生成产物，不自动提交、推送或发布。
- 发现行为缺陷、文档与实现冲突或测试基线问题时，以实现证据说明并单独报告；不通过改代码、删断言或更改既有契约解决注释验收问题。

## 7. 交付与停止点

- 初版与二轮样板均已交付；下一轮交付四个 C# 文件的温和精简 diff、深度与密度走查结论、实际重验/沿用的证据，随后等待用户确认，不直接全量推进。
- 全量交付：已覆盖与排除的模块、必要的模板输出注释变化、实际验证结果、发现但未修复的问题；不罗列无关检索过程。
- 规划阶段仅新增本 Plan；实际实施范围与验证结果见下一节，不将样板完成等同于全量完成。

## 8. 阶段 A 验收记录（2026-10-25）

- 工作区基线：`integration/xf-yl`，HEAD `bb9ef6c5182288d23a1ecfab02abba28a2c3b5a0`。五个样板原先没有用户修改，编辑前另将源码及 `.meta` 内容保存到本次独占的 Unity SessionState，未以 HEAD 替代实际基线。既有游戏资产、计划和未跟踪内容未改动。
- 范围清点：排除约定目录后的 C# 源码为 726 个，分别为 Runtime 472、Editor 222、Integrations 28、Samples 4，另有 FSM 外部模板。只清点了全量范围，本轮仅检查并修改五个样板，不表示其余文件已完成注释审查。
- 样板内容：Root 说明宿主、按需 UI 根、fake-null 与重入；Async 说明等待、同步通知、重复完成、静默完成和 Abort；Lease 保留既有说明并补足所有权、有效性、释放与短示例；Module 生成器和两类模板解释真实用途与生命周期。未改实现逻辑、API、配置、消息字符串或资源输出。
- 静态验证：在当前 Unity 中使用 Roslyn `4.8.0.0`、C# 9 和 XML Diagnose 对修改前后源码检查。普通 C# 非注释 token 一致，指令、disabled text 及 ReSharper 指令未改变，Module 仅放行模板字面量后单独比较渲染代码；五个 `.meta` 内容一致。UTF-8 无 BOM、LF、单 EOF 换行和 XML 片段均通过，具名私有方法均有职责说明。
- 模板及示例：18 组 Module 输入覆盖两种基础模块标志、空与转义场景名、占位符作为普通字符串、合法 shadowing 类名，另有 1 组 FSM 模板，修改前后非注释 token 一致。18 份 Module、1 份 FSM 和 1 份 Lease 短示例共 20 次 Roslyn 内存编译通过，无生成脚本或辅助程序集落入 Assets；这是辅助编译证据，不冒充 Player 或生成脚本的 Unity 导入验收。
- 格式：对四个改动 C# 文件运行限定路径的 `Format-YL.ps1 -Mode Check`，通过；实际环境为 PowerShell `7.6.8`、SDK `9.0.312`。五个文件的 `git diff --check` 通过。
- Unity 编译：实际 Editor 编译状态空闲且 `scriptCompilationFailed=false`；`Library/YLFramework.Runtime.xml` 已含本次 Root、Async、Lease 文档，写入时间为 `2026-10-25T04:10:24.1047019Z`；从已加载的 `CreateModuleCommand` 反射取得的新模板也含新增说明，确认 Editor 程序集已更新。
- Unity EditMode：通过 `tests-run` 一次选择 `ApiTests`、`ApiHelpTests`、`FrameworkHelpProviderTests`、`CoreModuleGenerationTests`、`CustomAsyncOperationTests`，实际结果为 **141/141 通过，0 失败、0 跳过**，耗时约 7.45 秒。未修改任何测试源码；测试自有输出已清理，`Assets/Scenes/Main.unity` 仍为干净场景。
- 验证边界：未运行游戏、PlayMode、Player/HybridCLR 构建或导出发布；未改 Tools、分析器 DLL、Resource 生成输出或真实业务生成物。当前样板未涉及共享 `AnalyzerRuleCatalog.cs`。
- 初版独立 Review：当时只读复核五个样板 diff、基类、调用方及相关测试源码，未发现契约与实现不符的问题，Critical / Important / Minor 均为 0。该结果不能证明 Unity 初学者已经能顺畅读懂过程；后续用户反馈指出的教学可读性缺口按阶段 A2 补强，不以技术检查通过否定该反馈。
- 初版交付后的决定：用户认可 API 注释，但过程解释尚未通过，要求按阶段 A2 补强；当前执行状态见第 10 节，阶段 B 全部未开始。

## 9. 二轮修订决策（Q15-Q16）

- 已确认：API 标准保留，过程标准升级为“整体路线 + 阶段衔接 + 难点机制”，关注状态、数据、协作、顺序原因和失败收尾，不逐行翻译。
- 已确认：先原地补强四个 C# 样板，再由用户确认；不因 API 注释获认可而跳过过程样板检查点。FSM 空模板和 ModuleTemplate 本轮不动。
- Q15-Q16 规划轮仅更新本 Plan，未修改源码、运行 Unity、编译或测试；随后获实施授权，实际结果见第 10 节。第 8 节仍是初版历史证据，不冒充阶段 A2 的新验证。

## 10. 阶段 A2 验收记录（2026-10-25）

- 新基线：在编辑前用独占的 `YL.Comments.Pilot2.20261025.0432.` SessionState 保存当前四个初版 C# 文件、五个配套 `.meta` 字节、FSM 文本、已编译 ModuleTemplate、Runtime XML 和程序集时间；没有拿 HEAD 或初版之前的快照替代本轮基线。
- 实施范围：只修改四个 C# 文件的普通过程注释。Root 补足入口、三个状态字段、会话内复用及回调/失败路径；Async 串起跨重载、属性 setter、委托与钩子，解释常规/静默/迟到/取消分流；Lease 用共享关系、计数示例和头插/摘链示意解释归还顺序；Module 生成器解释 IMGUI 入口、名称/身份、转义与单次替换、asmdef 角色及部分写入失败。
- 不变性重验：Roslyn C# 9 / XML Diagnose 检查通过，四个文件非注释 token 全部一致，未对 ModuleTemplate 放行任何字面量变化；38 个源码 XML 文档块与初版逐块相同。指令、disabled text、ReSharper 注释均不变，ModuleTemplate 与 FSM 文本不变，五个 `.meta` 字节相同；UTF-8 无 BOM、LF、单 EOF 换行检查通过。
- 格式重验：限定四文件的 `Format-YL.ps1 -Mode Check` 通过，SDK 为 `9.0.312`；对应 `git diff --check` 无问题。
- 实际 Unity 编译重验：阅读断点补齐后再次运行不变性及格式检查，均通过。AssetDatabase 刷新后编译空闲、`scriptCompilationFailed=false`；最终 Runtime 输出为 `05:03:48.4464659Z`、Editor 为 `05:03:48.6705808Z`，均晚于最终源码，且晚于基线的 `04:10:25/27Z`。新生成 Runtime XML 全文与初版相同，已加载 Editor 的 ModuleTemplate 也相同；Main 场景保持干净。
- 沿用证据：本轮未重新运行测试或模板内存编译。在相关代码 token、API XML 与两个模板输入完全不变且当前 Unity 编译通过的前提下，沿用第 8 节的 19 组模板校验、20 次内存编译和 141/141 EditMode 结果，明确它们不是本轮新执行的测试。
- 独立阅读走查：先只读四个样板，再核对依赖，能够还原 Root 的准备/复用/失败、Async 的完整通知链、Lease 的计数与摘链、Module 的文本准备和逐文件写入。准确性核查未发现实质问题；同时指出生命周期精确时序、pin 对应的具体缓存模型、autoReferenced 接入含义三处首读知识断点，未将“技术正确”当作教学验收完成。
- 走查反馈处理：作者随后按 Unity 2022.3 官方 [Instantiate](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.Instantiate.html)、[Destroy](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.Destroy.html)、[Assembly Definition 属性](https://docs.unity3d.com/2022.3/Documentation/Manual/class-AssemblyDefinitionImporter.html)及 `InstanceCache.Prune` 实现，补入短关联说明：活动且启用时的 OnEnable、延后销毁与 OnDestroy 注销、Prefab 来源保活和缓存收尾、预定义程序集的自动引用开关。仅改普通注释，没有扩展模块实现或改配置。
- 复核边界：补齐三处说明后由作者核对并重新运行技术检查，没有追加第二次独立 Review；模拟薄弱基础读者的阅读走查不能替代真实用户对教学深度的最终确认。
- 二轮交付后的反馈：用户认为稍密集，Q17 确认保留深度并温和删重；后续按阶段 A3 收敛。A2 技术证据仍保留，不等于最终密度已通过；阶段 B 未开始。

## 11. 密度修订决策（Q17）

- 已确认：温和精简，不回退初版；同一要点只讲一次，以短段落解释关键因果，不强制逐阶段说明。API 与模板保持原样，范围仍为四个 C# 样板。
- Q17 规划轮仅更新现有 Plan；随后用户授权实施 A3。其实际结果见下一节，密度仍待最终样板确认，不直接进入阶段 B。

## 12. 阶段 A3 验收记录（2026-10-25）

- 基线：编辑前使用独占的 `YL.Comments.Pilot3.20261025.0525.` SessionState 保存当前 A2 源码、模板、XML、五份 `.meta` 字节及程序集时间，不使用 HEAD 或前两轮快照回写文件。
- 实施：仅删改四个 C# 文件的普通注释。合并 Root 的准备状态和登记说明、Async 的流程与契约重复；压缩 Lease 的共享与归还长段；Module 留下转义、单次替换和写入边界，不再逐步复述普通检查。保留重入、先提交状态、先失效再归还及非完整回滚等关键因果，未增加新的知识点或修改范围。
- 不变性重验：Roslyn C# 9 / XML Diagnose 通过。四文件非注释 token、38 个 XML 文档块、指令与机器注释不变；ModuleTemplate 与 FSM 文本不变，五份 `.meta` 字节不变；UTF-8 无 BOM、LF、单 EOF 换行通过。未以压行或大量空行替代删重。
- 格式及 Unity 重验：限定四路径的 `Format-YL.ps1 -Mode Check` 和 `git diff --check` 通过。Runtime/Editor 输出由基线 `05:03:48Z` 更新至 `05:39:23.3430917Z` / `05:39:23.6519114Z`，均晚于最终源码；编译空闲且 `scriptCompilationFailed=false`。生成的 Runtime XML 全文及已加载的 ModuleTemplate 与基线相同，Main 场景仍干净。
- 阅读复核：独立只读审查未发现需修正的实质问题，确认重复说明已收敛、关键流程仍连贯，未建议补回外围长篇。此为模拟阅读，不替代用户最终密度验收。
- 沿用与停止：相关输入完全不变，沿用初版 19 组模板校验、20 次内存编译与 141/141 EditMode 记录，本轮没有重跑这些测试。已交付四个 A3 样板并停止，阶段 B 未开始；未改 API、逻辑、模板、Resource 输出或测试，未提交或发布。

## 13. 全量阶段执行记录

- 授权：用户已明确确认 A3 样板并要求开始、继续完成全量阶段；不再停留在样板确认点。
- 新基线已在任何 B 阶段源码修改前保存：`C:/Users/Kano/AppData/Local/Temp/kilo/yl-comments-stage-b-baseline-20261025-0645.json.gz`。这是本任务独占的压缩数据快照，不是新增长期文档或工具；包含 726 个范围内 C# 源码、FSM 模板及对应 `.meta` 字节，不以 HEAD 覆盖现有成果。
- 当前范围计数仍为 Runtime 472、Editor 222、Integrations 28、Samples 4。所有既定 Plugins/Tests/JSONObject/SharpZipLib/Tools/业务代码排除规则保持不变。
- 进度以本节与阶段 B 表为恢复依据：只有实际读过、按需要完善并完成相关检查的范围才能记为完成。不要因方法名、自动覆盖统计或代理口头汇总就声称质量验收通过。
- 执行组织：复用当前工作区，不新建分支、worktree、独立 Markdown 报告或长期验证平台。内部任务只编辑分配到的独占源码路径，不改 Plan、不调用 Unity；主代理统一进行基线对比、Unity 编译/测试、复核与本 Plan 记录。模型设置继承当前会话，不主动选择覆盖。
- B1 Foundation：128 文件均已逐一检查，89 文件补充或修正注释，39 文件（含已确认样板）原样保留。四个独占范围为 Pool/IOC、Singleton/Timing/Update/Async/Internal、Binding/Attributes/Serialization/Utilities、Events/Editor Foundation。
- B1 技术验收：相对全量基线的非注释 token、指令、disabled 分支及 `.meta` 全部一致，类型/公开 API/私有方法注释检查无缺口；限定 128 路径的 Format-YL Check 通过。实际 Unity 编译通过，使用实际程序集源码、引用和 defines 的 Runtime/Editor 内存 XML 编译均成功、Foundation 无文档绑定诊断。临时验证器在批准 Temp 中，不进入产品源码。
- B1 复核：独立抽样核对 Pool、IOC、Timer、Binding、Events、Singleton 的契约及密度，未发现准确性问题，指出两个私有分发数据结构的重复说明；已删除重复内部字段/构造器说明，保留类型职责。两处 XML 缩进已修正，并重验 token、覆盖及格式。校准检查器以公开/受保护枚举值为必需范围，未为私有简单状态值灌入同义注释。
- B1 测试：一次定向 EditMode 共 406 项，404 通过，TimerSession 与意外匹配的 AudioContinuousPlay 两项因测试顺序中的脏场景前置保护失败。当前场景恢复干净后，保持原断言分别隔离重跑，两项各 1/1 通过（3.08 秒、2.00 秒）；没有保存用户场景或修改测试。不能将此记录表述为一次 406/406 连跑通过；后续筛选 IOC 使用精确前缀而非大小写不敏感的 contains，避免误匹配 AudioContinuousPlay。
- B1 已知范围外观察：子任务指出 BindableList.RemoveAll 的谓词若自行缩短列表，随后索引移除可能越界；仅如实修订说明，未改变实现或扩展缺陷修复。
- B2：73 文件检查，68 文件补缺，4 文件已有说明充分、已确认 Module 生成样板保持原样。中断后的子任务已从原上下文完成剩余工作，没有重做或覆盖成果。对比基线代码/指令/分支/meta 无变化，覆盖检查无缺口，73 路径格式检查通过；Runtime/Editor 内存 XML 编译无错误和 B2 文档诊断，实际 Unity 编译完成，Runtime XML 于 `10:08:51Z` 更新。
- B2 复核修正：Module 旧泛型文本中的未转义尖括号已改成准确索引说明；补齐 LoadViewRequest 的两阶段失效职责和 Help XML 身份方法职责；partial 类型使用主声明文档，不为每分片复制长摘要。独立复核发现 ViewManager 把实例/全局退出通知时序合并描述，已明确前者在解绑前、后者及关闭钩子在撤销索引后；未修改实现。
- B2 测试：Core/Help/UI 定向 Unity EditMode **388/388** 通过。工具对采用 SetName 的 `InvalidNativePoliciesAreRejectedWithoutModifyingAnyInput` 无法映射到原生测试树，最初调用在执行前被拒；其余测试正常运行。另以既有 TestCaseSource 的实际 22 组数据逐项调用原 SetUp/测试/TearDown，全部断言通过，临时目录由既有 fixture 自行清理；这是补充的直接调用证据，不混记为 Unity TestRunner 数量，不改测试源码。
- B2 已知范围外观察：Help 签名显示将非 out 的引用参数（包括 in）显示为 ref；只记录，没有改实现。共享 AnalyzerRuleCatalog 仅修改注释，没有重建 DLL/收据或导出，发布一致性仍未验收。
- B3：82 文件检查，76 文件补缺，6 文件原样保留。全批非注释 token、指令、disabled 分支及 meta 与全量基线一致，文档覆盖零缺口，格式通过；实际 Unity 编译与 Runtime/Editor XML 内存编译成功、无 B3 文档诊断。独立复核发现 AudioClipCache.Users 既含等待者预留也含已交付责任，已修正说明并删减内部 Key 的重复契约；Sequence 的职责说明同时收敛，保留 private 构造入口说明。
- B3 测试过程如实保留：广域混合批次最初因自定义 TestCaseSource 名称映射被拒；排除该方法后执行 843 项，399 通过、444 失败。失败集中于 `AudioKit is shutting down`（352 项含随后 TearDown 空引用、67 项仅 SetUp、20 项直接失败）及脏场景保护（3 项 OneTimeSetUp、2 项断言）。现场确认 `_isQuitting=true`、无活动 Audio owner、Main 最终干净，属于跨 Play 混合运行污染，不是文档代码回归证据；未修改门禁/断言或保存场景。
- B3 后续隔离验证：在无活动 owner、场景干净时请求正常脚本域重载，没有手动改退出标记。分开运行纯 Audio 契约/组件/Help 测试 **455/455**、Action/ApiHelp **334/334** 通过。混合 843 项不能记为整批通过；原生 Bundle/退出/ScreenTransition 等跨场景链未重新全跑，`ThreePoliciesUseTheJointVictimScopeAndStopAtMostOneRequest` 的自命名源测试未由该工具执行，本次保留此限制，不扩展为测试设施修复。
- B3 范围外观察：空 Parallel 不结束，InOut 子序列的父动作完成/回收行为，以及 Audio 默认音量通知重入可能覆盖较新音源音量，均只记录或准确描述，不改实现。最终 Main 场景干净，XML 更新至 `10:40:05Z`。
- B4：113 文件全部检查，110 文件补缺，已认可 AssetLease 及两个已充分文件原样保留。全批 token、预处理/disabled 分支和 meta 与基线一致，文档覆盖零缺口；113 路径格式检查通过，Runtime/Editor XML 内存编译均成功且无 B4 文档诊断。独立审查请求/Scope/实例pin/Scene、ArtifactStore、Catalog/签名/Provider、生成/Workspace/发布/Atlas/Undo边界，未发现实质问题。生成输出字符串、哈希头及 asmdef 均保持不变。
- B4 定向 Unity 测试在 MCP 返回超时后仍执行，未重叠启动第二轮。只读进程检查确认主 Editor PID23024 仍响应，从 Finishing import 转为 Test Runner busy；既有无窗口 Asset workers 不视为第二个项目 Editor。等待期间不杀进程、重启、保存场景或更改测试。已用每五分钟计划任务等待收尾，测试结束后读取真实结果再取消等待，不把超时写成测试通过。
- B4 广域测试在 `11:25Z` 前结束，Main 干净、编译无错。由于发生域重载且原调用超时，插件当前只保留 29 条结果（20 通过、9 失败），不据此猜测整个批次数量。6 项 `ResourceUpdateTests` 因缺少隔离环回证书 `YL_RESOURCE_TEST_PFX` 前置条件失败：`RemoteDownloadedVersionUsesNativeBusyBarrierLoadsNewAssetsInstancesScenesAndRollsBack`、`RemoteSignedClosurePreparesWithoutActivationAndConfirmedFloorSurvivesFailureRestartAndRollback`、`ReusedOwnedVersionMustMatchSignedCompleteAtCheckPrepareAndCommit`、`SameTransitionUpgradesV1PreparedAndRefreshesAuthenticatedMetadataWithoutLosingHigherSequence`、`SignedPreparedAndConfirmedHistorySurviveActualDomainReload`、`TamperedRemoteMetadataCatalogCompleteAndBundleCannotPrepareOrChangeActiveAndGood`。未配置证书、修改系统信任或改断言。
- 另 3 项 `ResourceWindowTests` 失败：`ActualWindowGuiSelectRefreshProjectSwitchAndCloseCancelQueuedOperations`（实际 GUI 按钮未布局）、`ExternalAssetDropReachesTheLiveNativeTableBeforeItsDefaultDragHandler`（真实拖放未达到期望计数）、`SettingsRootFieldCopiesTheCompleteHierarchyWithoutEditingTheProject`（读取到既有剪贴板内容而非期望路径）。这些是真实失败/交互环境限制，不记为通过，不改UI字符串、剪贴板或测试实现来制造通过。
- 核心补跑已结束：目录/请求/Codec/内容校验/本地快照/常量生成/Startup配置实际 **85 通过、0 失败、1 跳过**。跳过项为 `ResourceContentTests.UnsupportedBackendDoesNotImportCngOrFallBackToPlaintext`，当前平台不属于其目标的未支持编译配置，未更改断言或伪造条件。Main 场景干净，实际 Unity 空闲且无编译错误；等待唤醒已取消。
- 用户最新明确要求“先把 B4 的问题解决，然后再继续 B5”。因此 B4 不以记录 9 项失败后直接放行为完成，先核实项目已有隔离 PFX 配置入口和原生 GUI 交互条件，再以原断言复验；不削弱测试、不改证书信任库。两项只读调研分别负责证书前置和窗口交互，未授权子任务修改任何文件或运行测试。
- GUI 修复验证：在空闲、场景干净的既有 Editor 中，使用已安装 Unity CLI 的长超时调用对原 3 个 `ResourceWindowTests` 精确隔离复验，原断言 **3/3 通过（15.826 秒）**。产品/测试/窗口布局源码均未改，未手动设置剪贴板或删未知拖放状态；fixture 后剪贴板与事前保存值相同，Main 干净、无 Resource 窗口残留。根因只能收敛为广域批次/原生布局与输入状态敏感，不能声称已证明某一焦点或坐标因素。仅存储本任务恢复状态，未输出剪贴板内容。
- 长时执行方式调整：现有 `unity-mcp-cli` 支持 `run-tool --timeout`；后续较长验证使用会话追踪的 `background_process monitor`，保留工具完整汇总，不再用重复短时 MCP 查询代替结果。CLI 现有版本已可连接本项目唯一 PID23024，未安装升级。
- B4 证书前置处理：复用项目既有 `Prepare-ResourceCostEvidence.ps1` 和 `ResourceCostEvidence.UseLoopbackEnvironment/RestoreLoopbackEnvironment`，只在批准 Temp 的新独占目录生成短期 RSA 回环 PFX，内部 owner 标记保持原工具配对，不宣称运行 B02/F03 性能验收。仅设置当前 Unity 进程两项变量，未安装证书、修改信任库、设置用户/系统环境或读取他人凭据。
- B4 六项远端失败原断言复验：CLI 长超时一次精确运行，原生 Summary **6/6 Passed，0 Failed/Skipped**。因实际域重载，工具详细列表仅返回最后两项且 Duration 汇总异常；不采用该耗时，也不伪造缺失的逐项时长，原生 suite 总数与终态作为通过证据。没有改产品或测试代码。
- B4 排障后清理：既有恢复入口已核对环境精确复原，两项进程变量恢复为原先未设置，ownership 已释放；仅删除本次 owner 匹配目录内的 `loopback.pfx`，未清理未知旧目录。剪贴板已恢复事前值且正文未输出；Main 干净、Unity 非 Play/非编译且无编译失败。三项 GUI 加六项远端的原失败均已解决，不把最初失败抹去或改记为整批初跑通过。
- 按用户“先解决 B4 再 B5”的顺序，B4 完成上述复验及清理后才启动 B5。
- B5：191 文件逐一检查并补缺（状态机59、Buff/Equipment50、Mission/Skill38、数据/本地化29、HTTP/Logging/Reddot/Save/Shared15）。对全量基线的 token/指令/disabled/meta 检查零变化，文档覆盖零缺口；Systems 全部386路径格式通过，Runtime/Editor XML 编译无错误和 B5 文档诊断；实际 Unity 编译通过、XML更新于 `12:06:40Z`，Main干净。独立抽样复核关键领域与清理/持久化契约未发现实质问题。
- B5 定向 EditMode：状态机、作者工具、Save、Buff、领域定义、Logger、Mission、Reddot、Skill、Http 等原测试 **181/181 通过，0失败/跳过（36.10秒）**，CLI完整汇总已取得；没有重新跑跨Play门禁或修改测试。
- B5 范围外风险仅记录未修：Buff/Mission/Skill 的失效GUID脚本回退查错映射方向；Buff树排序标志与比较方向不符；Equipment删除同步失败后仍更新处理状态；Mission/Skill 空配置GUI布局BeginVertical与EndHorizontal不配对；Skill复制保存后改名未再次标脏。这些是静态观察，不冒充本次新增回归或已运行复现。
- B6：105 文件逐一检查，95 文件补缺、10 文件已有说明充分保留。完整覆盖角色/平台/Overlap25，扩展与StringTools32，Excel/FSM/Grid/Table/UI/ZIP自有包装23，EditorUtilities25。模型连接TLS中断的子任务已用原上下文恢复完成，该连接错误不归于Unity或产品。
- B6 验证：代码 token、指令/disabled、meta 与基线一致，覆盖零缺口；105 路径格式通过，Runtime/Editor XML 编译成功、无 B6 文档绑定诊断，实际 Unity 编译已完成。独立抽样角色坐标/位移、乘客/检测结果、共享资源与扩展、FSM/Grid/Table、ZIP责任和Editor Undo/保存未发现实质问题。定向原 EditMode **109/109 通过，0失败/跳过（9.59秒）**。
- B6 范围外静态风险未修：Unity6 条件分支仍引用与本类大小写不符的 `DISTANCE`（当前Unity2022未激活）；CaptureCamera异常路径可能不回收已创建Texture2D；轻量FSM Continue重入缺口；ZIP现有链接路径/非原子覆盖边界；Handles竖胶囊端点未加center、OverlapBox2D边编辑未完整换算旋转/缩放、ProjectSettingsScriptableObject重复加载旧缓存实例未销毁。未作行为修复，也不声称已运行复现。
- B7：34 文件逐一检查，31 文件补缺；HybridCLR 两个 AssemblyInfo 及已确认 Root 原样保留。包括 HybridCLR19、Excel/DomainExcel/DOTween9、Startup样例4、Exporter1、Root1。token/预处理/disabled/meta 与基线一致，文档覆盖零缺口；34 路径格式通过，当前实际加载的 Runtime/Editor/HybridCLR及其Samples、Excel、DOTween、ResourceStartup程序集 XML 内存编译均成功，无 B7 文档绑定诊断。
- B7 复核：关键集成契约无事实性错误；按密度反馈删掉内部差异DTO的简单构造说明，将两个private GUI方法保持必要的短职责索引，不为删重破坏“每private至少一句”要求。既有 HybridCLRWindow 向控件传旧字段名的静态风险仅记录，未修实现或配置。
- B7 原CLI测试跨真实域重载后客户端中断，当前缓存只留最后一项通过；不能把它当整批结果。已改用项目既有 `ResourceCostEvidence.Run` 的跨域Observer，一次选择12个原测试fixture，回执目录 `C:/Users/Kano/AppData/Local/Temp/kilo/yl-b7-evidence-20261025-1320/b7-contracts`，runId `120a108d-475e-4ee6-98ff-ff4bfd5dc48e`。外部 owner 使用既有Observer协议标记，不运行B02/F03性能测量、不创建TLS凭据、不改测试源码；等待 finished.json 后读取原生叶结果与suite终态。
- 最终独立跨批次复核已完成：沿Provider/Core/Resource/Audio/Startup责任链及取消/事务边界抽查，并补看数据索引、回调注销、网格及扩展；未发现实质 findings，英文筛查未确认独立纯英文自然语言段。此为跨模块抽样，不宣称726文件每行另做独立认证。全量差异检查通过，Plugins/Tests/Json/SharpZipLib/Tools/Packages/ProjectSettings受保护路径无tracked变更。
- B7 完整回执：`finished.json` 于 `2026-10-25T13:30:34.7208387Z` 写入，PassedGate=true、ResultState=Passed，**128/128 叶测试通过，0 suite失败，EnvironmentRestored=true**。这是既有跨域Observer收集的完整原生结果，不依赖丢失历史的缓存；包含Excel、HybridCLR契约与Startup编译/实际样例运行，未运行正式Player或发布门禁。

## 14. 全量完成验收

- 726 个范围内 C# 均已检查；相对全量起点 660 个补充或修订注释、66 个保留充分说明。四个 A3 样板和 FSM 模板全文未再改动；初版已授权的模板注释变化仍保留在工作区，不将其误记为 B 阶段改逻辑。
- 最终 Roslyn 基线核对：726 文件清单无新增/缺失，全部非注释 token、预处理指令、disabled 分支及 meta 一致，类型/公开 API/私有方法文档覆盖零缺口；静态结果 `C:/Users/Kano/AppData/Local/Temp/kilo/yl-comments-static-639285322374215672.json`。覆盖工具是自检辅助，不替代每批内容阅读和独立抽查。
- 最终 Format-YL Check：显式限定 Runtime、Editor、Integrations、Samples，实际726文件、SDK9.0.312，通过；框架与Plan差异 `git diff --check` 通过。没有全库Apply或工具链升级。
- 当前12个自有/可选集成/示例程序集的辅助XML编译成功，无源码编译错误或范围内文档绑定诊断；实际唯一Unity实例空闲、非Play、`scriptCompilationFailed=false`，Runtime XML更新于 `13:14:45.5862799Z`，DOTween XML存在，Main场景干净。辅助编译与实际Unity证据分开记录，不冒充Player验收。
- 分批测试结果及最初失败保留在第13节，不把重复覆盖测试数量相加成唯一总数。B4原9失败已按原断言复验通过；其核心测试1项按非目标编译配置跳过，未削弱或删除。B7完整128/128通过；跨模块最终独立抽样无实质findings。
- Plugins、Tests、JSONObject、SharpZipLib、Tools、Packages、ProjectSettings 无tracked修改。未跟踪 `ProjectSettings/HybridCLRSettings.asset` 的创建/写入时间为 `2026-10-24T11:52:39Z`，早于本任务，已保留，未将它误作测试副产物删除；其他原有资产与未跟踪内容也不接管或回退。
- 环境恢复核查：回环环境ownership=false，PFX/密码变量恢复未设置，本次私钥文件已删除，剪贴板与事前值相同；不输出密码或剪贴板正文。临时基线/验证器/机器回执留在批准Temp供核对，不加入仓库或交付包。
- 剩余边界：未做所有平台/Player/IL2CPP构建、全仓库无筛选回归或导出发布，未重建Analyzer DLL/收据。第13节列出的既有实现风险未顺手修复。所有授权注释工作与必要验证已完成，改动保留工作区，未创建commit或推送。
