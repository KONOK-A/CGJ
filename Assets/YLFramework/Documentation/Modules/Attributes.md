# Attributes（显示元数据与 Core 使用诊断）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供类型显示名、自定义字段绘制/刷新配置与 DisableEdit 中立元数据，供编辑器扩展与业务代码使用。

定位边界：不是 Odin-like Inspector 框架 / PropertyDrawer 框架 / Attribute Processor，不提供 Attribute 自动扫描或注册系统。

## 位置

- 元数据：[`Runtime/Foundation/Attributes/`](../../Runtime/Foundation/Attributes/)，namespace `YLFramework.Attributes`。
- 必要原生 Drawer：[`Editor/Foundation/Attributes/DisableEditDrawer.cs`](../../Editor/Foundation/Attributes/DisableEditDrawer.cs)，namespace `YLFramework.Editor.Foundation`，程序集仍为 `YLFramework.Editor`，只在 Editor 主体中消费 Runtime 元数据。

## CustomClassNameGui

- 语义：为**类型**提供编辑器中的自定义显示名称（纯元数据）。
- 目标：`AttributeTargets.Class | Struct | Interface | Enum`；`AllowMultiple = false`；`Inherited = false`（派生类型不继承显示名，需自行标注）。
- 构造：`CustomClassNameGui(string name)`，允许 `null` / 空字符串；`GetName()` 原样返回，不会 NRE。调用方遇到 `null` / 空字符串时应回退为类型名。
- 消费方式：由编辑器代码读取，框架内暂无内置消费者。

## CustomPropertyGui

- 语义：标记需要自定义 Editor 绘制的序列化字段（metadata + Editor 契约）。
- 目标：`AttributeTargets.Field`；`AllowMultiple = false`；`Inherited = false`。
- 契约：抽象特性，仅供派生；编辑器下派生类**必须实现** `OnPropertyGui(UnityEditor.SerializedProperty)`，由接入的 Inspector 读取字段特性后调用；现有 Mission/Skill/Buff Inspector 已有该接入。
- 构造：无参默认不刷新；`CustomPropertyGui(bool reloadTreeViewOnValueChange)` 配置刷新意图；public 字段 `ReloadTreeViewOnValueChange` 可供命名特性参数与接入方读取/修改，Editor 与 Player 均可用。是否刷新 TreeView 由实际 Inspector 处理。

## DisableEdit

- `PropertyAttribute`，保留 `Field | Property`、单次使用、默认继承的声明；原生 Inspector 实际绘制的是序列化字段，普通 C# property 不因此变为可序列化。
- 已注册的 Drawer 禁用字段及展开子属性，使用原生 `PropertyField` 与对应子属性高度；正常或抛异常时均恢复进入前的 GUI enabled 状态，不应用或保存序列化对象。

## Runtime / Editor 边界

- 三个特性均归 `YLFramework.Runtime`，其源码保持中立，不消费 Core/Systems/Utilities；旧自有 Core 与 Tools 元数据副本已退出。主体沿用 UGUI/Json.NET、不依赖 XFAB，不能用整个 DLL 的引用列表代替元数据源码闭包验证。
- 仅 `CustomPropertyGui.OnPropertyGui` 位于 `#if UNITY_EDITOR` 内：Editor 编译时存在、Player 编译时不存在，Player 程序集不会残留 `UnityEditor` 引用。
- 使用 `CustomPropertyGui` 的业务代码需注意：Player 下没有 `OnPropertyGui`，派生类中的重写必须包在 `#if UNITY_EDITOR` 内。
- `Tests/EditMode/Foundation/AttributesTests.cs` 直接编译正式元数据源码和派生构造使用者，覆盖 Editor 与无 UnityEditor 引用的 Windows Development 源码 profile；不借用预编译 Foundation DLL，不等于实际 Player 构建/运行。

## 主要类型

- `CustomClassNameGui`：类型显示名特性。
- `CustomPropertyGui`：字段自定义绘制抽象特性。
- `DisableEdit`：原生 Inspector 只读绘制特性。

## 依赖

- `System`；`CustomPropertyGui` 的绘制方法条件编译依赖 UnityEditor（`#if UNITY_EDITOR`）。

## 当前状态

元数据回归位于 `Tests/EditMode/Foundation/AttributesTests.cs`；真实 SerializedObject、展开高度、子 Drawer 禁用/异常状态恢复与 Clipboard 直接消费者回归位于 `Tests/EditMode/Editor/Foundation/Attributes/DisableEditDrawerTests.cs`。

`Editor/Utilities/Serialization/SerializedPropertyExtensions` 使用 `YLFramework.Editor.Utilities`，保留 `CopyToClipboard`、`IsCanPaste`、`PasteFromClipboard` 入口与 Unity 原生 Clipboard 格式。粘贴先在独立 SerializedObject 缓冲中解析并完整核对，再以 `ApplyModifiedProperties` 应用；同类型、非混值的单对象和多对象可编辑，沿用真实 Undo/Redo 与 Prefab override。拒绝操作不会应用调用者的待修改数据。

混值、不同类型的多对象、不可编辑目标、不兼容或损坏的数据明确拒绝；原生解析器不能可靠处理的 ManagedReference、ExposedReference、FixedBufferSize 和 double 也拒绝。`IsCanPaste` 对这些输入返回 `false`，实际复制/粘贴抛出 `InvalidOperationException` 说明原因；Unity 私有 API 缺失或签名变化时抛出包含 Unity 版本与成员信息的 `NotSupportedException`，不静默回退。空 ObjectReference/ExposedReference 的 `GetValueText` 显示 `None`。专项回归位于 `Tests/EditMode/Editor/Core/CoreSerializedPropertyTests.cs`。

CustomClassNameGui / CustomPropertyGui 当前没有正式业务标注或内置读取器；DisableEdit 的必要 Drawer 已正确接线。原 XF 生态继续消费原包特性，不建立跨框架桥。下述 Core 使用诊断与 Foundation 显示元数据共用 Runtime 主体，但属于不同职责和消费方式。

## Core 使用诊断

Editor 内可从 `YLFramework/Core/帮助` 阅读规则和接入摘要；`YLFramework/Toolkit/帮助` 提供 Foundation 展示元数据导航。两个入口均只读，详细规则、配置及源码重建以本节为准。

六个正式 marker 位于 [`Runtime/Core/Attributes/`](../../Runtime/Core/Attributes/)，namespace `YLFramework.Core.RoslynAnalyzers`、程序集 `YLFramework.Runtime`。它们保留原有构造及 AttributeUsage，只提供编译元数据，不执行运行时权限检查。

自有分析器源码位于 `Tools/YLFramework.Analyzers/`，唯一 Unity 产物为 [`Runtime/Core/Analyzers/YLFramework.Analyzers.dll`](../../Runtime/Core/Analyzers/YLFramework.Analyzers.dll)。默认均为 **Warning**；自行采用严策略时按 ID 配置，不修改默认严重级别。分析器不依赖 Unity/Core/Workspaces，生成代码默认不分析，缓存仅当前 compilation。

| ID | 契约与典型修复 |
|---|---|
| YL0001 | `MustCallBase` 的有效 override 链要求当前方法直接调用同一 base 槽；修复为 `base.Start()`，不是同名 overload 或嵌套 callback 中的调用。仅证明存在，不证明所有控制流执行；抽象层级冲突需调整标记/覆写结构。 |
| YL0002 | `RestrictedCall` 限定直接调用者；例如 `View.RegisterEvent` 在 `Awake` 方法本体调用，callback 参数声明合法，callback 内调用不借用外层身份。 |
| YL0003 | `WarnInClasses` 对当前词法类型的本身/基类/接口匹配警告；View 中用 `RegisterEvent` 代替直接 `EventManager.AddEvent/RemoveEvent`。普通 nested helper 不自动继承外层调用类型。 |
| YL0004 | `DisableNew` 检查外部普通/目标类型 new 及可确定类约束 new T；用 `LoadViewArgs.New()` 等正式工厂。标记类型自身及词法嵌套实现可构造自身，派生类/同程序集不是全免。 |
| YL0005 | `MethodLocalOnly` 检查显式 field/property 持有；将参数保持在方法借用窗口，不保存回收后的 lease 引用。数组、tuple、真实容器与自有实例值槽都可构成存储。 |
| YL0006 | `ProtectedHierarchy` 检查指定类型层级对受保护实例 field/property/indexer 的写入和可写 ref 暴露；例如 View 读取 Model 状态，修改通过 Model 的受管方法进行。 |
| YL0007 | 真实 Core View 派生声明无参、非 static、非 generic 的 `void OnEnable/OnDisable/OnDestroy` 会竞争 private 框架消息；改用 `OnViewEnabled/OnViewDisabled/OnClosed`。不要求新 marker，不禁止 Awake/其他 Unity 消息。 |

### 错误与正确示例

```csharp
// YL0001：直接 base 调用必须在本方法，不用 Lambda 或方法组代替。
protected override void Start() { /* 缺少 base */ } // Warning
protected override void Start() { base.Start(); } // 正确

// YL0002/3：以下方法位于 View 子类中。
void Wrong() { RegisterEvent("changed", OnChanged); } // YL0002
void Wrong() { EventManager.AddEvent("changed", OnChanged); } // YL0003
void Awake() { RegisterEvent("changed", OnChanged); } // 正确，框架管理轮次

// YL0004/5：局部工厂借用可以，直接构造和成员保存不可以。
var args = new LoadViewArgs(); // YL0004
LoadViewArgs saved; // YL0005，List<LoadViewArgs> 等真实持有同样报告
void Load() { var args = LoadViewArgs.New(); /* 交给接管参数的正式加载入口 */ }

// YL0006：以下方法位于 View 子类中。
void Wrong(Model model) { model.Id = 7; } // Warning
long Read(Model model) => model.Id; // 合法读取
// 修改应封装在 Model 自身业务方法中；不把所有 setter 访问权交给 View。

// YL0007：只比较消息声明本身，不做运行时全局扫描。
void OnEnable() { Refresh(); } // Warning
protected override void OnViewEnabled() { Refresh(); } // 正确
```

这些片段是各规则的独立对照，不是可同时声明的完整类型。`OnChanged`/`Refresh` 代表业务回调，不是额外框架 API。

### 匹配与边界

- marker/View/ObjectPool 必须解析到真实 `YLFramework.Runtime` 的符号；支持主体自身 source compilation 与引用主体的 metadata consumer，短名或同 namespace 的异程序集类型不替代它们。方法 marker 只按真实 AttributeUsage 和 override 链取得，接口只在调用的静态目标本身带标记时生效，不叠加任意实现接口的契约。
- caller 选择器为 `Method`、`Type.Method`、`Namespace.Outer.Type.Method`，区分大小写并使用声明类型，泛型 arity 不写入选择器。accessor 可用 `get_P`/`set_P`、限定的 `Type.P`；构造器可显式用 `.ctor`/`.cctor`。Lambda/local 没有可冒充外层的选择器，field/property initializer 不借用外层方法。null/空调用者被忽略，空名单不允许调用；Warn 的空类型名单不匹配。
- 开放泛型类型匹配原定义，闭合类型按完整构造符号比较；只从有效类/类型参数约束确定受管类型，不因 `IEnumerable<Args>` 等接口签名钻入隐藏内容。
- 存储 profiles 包括实际 framework 的持有容器/对应只读集合、字典键和值、Tuple/ValueTuple/Rest、KeyValuePair、Task/ValueTask 结果、Memory/ReadOnlyMemory、ConcurrentQueue/Stack/Bag/Dictionary 及真实 Foundation ObjectPool；ArraySegment 和含值槽的 Nullable 结构也按真实存储识别。`IEnumerable`、`IEnumerator`、comparer/delegate 的签名本身不是实例持有证据，伪同名第三方容器不因名称进入 BCL profile。
- 自有泛型摘要只沿含参数的实例 field、auto-property backing 与构造 BaseType 证明内容位，有限定义图按固定点收敛。`Box<Box<Args>>` 有值槽则报告，phantom/static-only wrapper 不报告；互环无叶不误判，有叶正确传播，未知 metadata 不猜持有。入口不全对象图扫描无关 non-generic 成员；这不是通用泄漏/closure 逃逸检查。
- YL0005 仅豁免实际 Core 的精确 owner 字段：`LoadViewArgs._pool` 的 private static readonly 真实 `ObjectPool<LoadViewArgs>`、`LoadViewRequest<T>._args` 的 private instance，以及 `PanelManager.PanelOperation.Args` 的 internal instance。后者一对一对应 Module pending 消费者，不是给业务保存参数的公开权限。同名伪类型、其他成员/public/static/错误嵌套或泛型形态不自动豁免。故意契约测试使用声明级局部 pragma，不排除整个 Editor/Test/Core。
- YL0006 包含 compound/coalesce/inc/dec/deconstruction 各左值叶、ref/out/ref local/ref return 的可写暴露；读取、`in`/`ref readonly`、static 成员和 event subscription 不报。结构体值链写回外层保护成员，引用对象内部 mutation 不当成外层写；保护层级自身仍可修改状态。不追任意被调副作用或 ref 别名后续写入。
- YL0007 是声明级提示，普通 MonoBehaviour、XF/伪 View、普通 nested helper、static/有参/generic/非 void overload 不报。实测消息遮蔽会破坏订阅轮次，OnDestroy 的及时关闭可能延后到兜底扫描；不宣称必然永久泄漏，也不改变 Runtime 可见性。
- object/接口擦除、反射/Activator/dynamic、无约束泛型工厂、所有执行路径和跨过程所有权不保证。错误符号交给编译器，不吞异常或把 analyzer failure 当无诊断；这些 Warning 不是运行时安全边界。

### Unity 接入与配置

DLL 保留既有 GUID；Importer 禁用 Any/Editor/普通平台、关闭自动 Runtime 引用，仅精确标为 `RoslynAnalyzer`。位于 `YLFramework.Runtime` 的 asmdef 范围内，合并后的完整 Runtime 及其实际引用消费者注入；Editor、Tests、游戏和预定义程序集需按本轮真实引用图及 csc 输入核对，不能把旧 Core 范围内的零诊断沿用为新门禁。原 XF/Logger 不关闭，不复制第二套 DLL 扩大 scope，也不把 DLL 或 Roslyn 依赖带入 Player Runtime 引用。

§16最终输入核对：Runtime为403个源码，Editor为53个源码；Editor与Windows Development Player实际rsp中的Runtime/MainModule/Assembly-CSharp-firstpass（自动引用主体）各有唯一自有分析器，Editor rsp亦有一份，原XF/XFAB/XFFSM无该输入。Editor及非Editor消费者正/负Unity csc见证均成立，七YL Warning/无AD0001，原XF负例Error保留。Player载荷无自有Analyzer和Editor/退役产品DLL；整个工程仍有既有MCP/NuGet的Microsoft.CodeAnalysis.dll，主体IL不引用它，不扩大为“整个工程无Roslyn”的承诺。

IDE/SDK 可以按单 ID 配置 `.editorconfig`，例如在消费者目录：

```ini
[*.cs]
dotnet_diagnostic.YL0002.severity = none
```

Unity 2022.3 的实际 csc 不自动跟随 IDE `.editorconfig`；用 asmdef 目录中的叶子 ruleset（例如 `Game.UI.ruleset`），只配置需要的 ID：

```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="Game UI" Description="Local policy" ToolsVersion="10.0">
  <Rules AnalyzerId="YLFramework.Analyzers" RuleNamespace="YLFramework.Analyzers">
    <Rule Id="YL0002" Action="Error" />
  </Rules>
</RuleSet>
```

`Error` 会阻止该消费者编译，`None` 抑制该 ID；其余 ID 保持默认 Warning。根 `Default.ruleset` 可影响全工程，勿为局部需求覆盖根策略。故意测试可精确关闭一个声明/语句的 ID，并紧接恢复：

```csharp
#pragma warning disable YL0005 // 仅用于验证 lease 的归还终态，不是业务保存入口。
private readonly HashSet<LoadViewArgs> borrowed = new();
#pragma warning restore YL0005
```

### 重建与验证

唯一入口从仓库根执行，要求已安装 SDK `9.0.312`（工具 `global.json` 禁止漂移）及 PowerShell，Roslyn API 固定 `3.8.0`，Analyzer 为 `netstandard2.0`、独立 Tests 为 `net9.0`：

```powershell
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/YLFramework.Analyzers/build.ps1"
pwsh -NoLogo -NoProfile -NonInteractive -File "Tools/YLFramework.Analyzers/build.ps1" -Deploy
```

默认只做 locked restore、Release build 和诊断测试，失败即停止、不顶替旧 DLL；显式 `-Deploy` 才原子替换 `Assets/YLFramework/Runtime/Core/Analyzers/YLFramework.Analyzers.dll`、保留 meta。Fixture 从 `Runtime/Core/Attributes` 编译正式 marker 源码。唯一入口用自有 Temp 合作锁串行化 build/test/deploy，第二入口在动共享输出前拒绝；发布前及 stage 完成后指纹检查会拒绝已观察到的目标变更。该合作锁不是针对任意外部写者的 compare-and-swap，发布期间不要用其他工具写此 DLL。Unity 导入后核对标签、禁用平台和实际编译结果，不向 Assets 复制 Roslyn/Immutable 或测试宿主 DLL。

只有两个手工 SDK csproj 受管，Unity 生成工程仍被忽略；源码和实际 `packages.lock.json` 为重建输入。bin/obj/TRX/构建输入 SHA receipt 都在 `Temp/YLFramework.Analyzers/`。SDK 测试使用真实六 Attribute 源码生成合同引用，覆盖 ID/Warning/span/中文关键实体、异程序集/错误符号/取消/并发/生成代码及压力语料；不依赖原包 DLL 或反编译目录。

SDK 编译/测试、实际 Unity csc、PlayMode 生命周期、确定性 DLL 重建与程序集输出引用是不同证据。历史阶段 6.5 的真实解包、Windows64/Mono Player 与编译器隔离结果只适用于当时产物，见 [阶段 6 记录](../../../../.kilo/plans/xf-yl-6-editor.md#65-帮助导出与阶段收口)。Panel 消费者 owner 扩展同步重建同版本 DLL 并执行离线诊断测试，但 Unity 导入、实际编译与 Player 仍需在集成后复验；不沿用旧 DLL 的运行证据。IL2CPP、其他平台及无原 XF 生态的独立载体仍未验证。

## 备注

- 未来接入时建议在 `Editor/` 下实现最小消费者（如由现有 EditorWindow 直接读取字段特性调用 `OnPropertyGui`），不引入注册表 / 全局 hook / 自动扫描。
