# Attributes（编辑器显示特性）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供类型显示名、自定义字段绘制/刷新配置与 DisableEdit 中立元数据，供编辑器扩展与业务代码使用。

定位边界：不是 Odin-like Inspector 框架 / PropertyDrawer 框架 / Attribute Processor，不提供 Attribute 自动扫描或注册系统。

## 位置

- 元数据：[`Runtime/Shared/Attributes/`](../../Runtime/Shared/Attributes/)，namespace `YLFramework.Foundation.Attributes`。
- 必要原生 Drawer：[`Editor/XFMain/CustomPropertyDrawer/DisableEditDrawer.cs`](../../Editor/XFMain/CustomPropertyDrawer/DisableEditDrawer.cs)，程序集 `YLFramework.Core.Editor` 显式引用 Foundation。

## CustomClassNameGUI

- 语义：为**类型**提供编辑器中的自定义显示名称（纯元数据）。
- 目标：`AttributeTargets.Class | Struct | Interface | Enum`；`AllowMultiple = false`；`Inherited = false`（派生类型不继承显示名，需自行标注）。
- 构造：`CustomClassNameGUI(string name)`，允许 `null` / 空字符串；`GetName()` 原样返回，不会 NRE。调用方遇到 `null` / 空字符串时应回退为类型名。
- 消费方式：由编辑器代码读取，框架内暂无内置消费者。

## CustomPropertyGUI

- 语义：标记需要自定义 Editor 绘制的序列化字段（metadata + Editor 契约）。
- 目标：`AttributeTargets.Field`；`AllowMultiple = false`；`Inherited = false`。
- 契约：抽象特性，仅供派生；编辑器下派生类**必须实现** `OnPropertyGUI(UnityEditor.SerializedProperty)`，由外部编辑器代码读取字段上的派生特性后调用（框架内暂无内置消费者 / 绘制器）。
- 构造：无参默认不刷新；`CustomPropertyGUI(bool reloadTreeViewOnValueChange)` 配置刷新意图；同名 public 字段可供命名特性参数与接入方读取/修改，Editor 与 Player 均可用。框架不自动发现或刷新 TreeView。

## DisableEdit

- `PropertyAttribute`，保留 `Field | Property`、单次使用、默认继承的声明；原生 Inspector 实际绘制的是序列化字段，普通 C# property 不因此变为可序列化。
- 已注册的 Drawer 禁用字段及展开子属性，使用原生 `PropertyField` 与对应子属性高度；正常或抛异常时均恢复进入前的 GUI enabled 状态，不应用或保存序列化对象。

## Runtime / Editor 边界

- 三个特性均位于 `YLFramework.Foundation.Runtime`，不依赖 Core / 整体 Runtime / Editor 产品程序集；旧自有 Core 与 Tools 元数据副本已退出。
- 仅 `CustomPropertyGUI.OnPropertyGUI` 位于 `#if UNITY_EDITOR` 内：Editor 编译时存在、Player 编译时不存在，Player 程序集不会残留 `UnityEditor` 引用。
- 使用 `CustomPropertyGUI` 的业务代码需注意：Player 下没有 `OnPropertyGUI`，派生类中的重写必须包在 `#if UNITY_EDITOR` 内。
- `Tests/Foundation/EditMode/AttributesTests.cs` 直接编译正式元数据源码和派生构造使用者，覆盖 Editor 与无 UnityEditor 引用的 Windows Development 源码 profile；不借用预编译 Foundation DLL，不等于实际 Player 构建/运行。

## 主要类型

- `CustomClassNameGUI`：类型显示名特性。
- `CustomPropertyGUI`：字段自定义绘制抽象特性。
- `DisableEdit`：原生 Inspector 只读绘制特性。

## 依赖

- `System`；`CustomPropertyGUI` 的绘制方法条件编译依赖 UnityEditor（`#if UNITY_EDITOR`）。

## 当前状态

元数据回归位于 `Tests/Foundation/EditMode/AttributesTests.cs`；真实 SerializedObject、展开高度、子 Drawer 禁用/异常状态恢复与 Clipboard 直接消费者回归位于 `Tests/Core/Editor/DisableEditDrawerTests.cs`。

Core Editor 的 `SerializedPropertyExtensions` 保留 `CopyToClipboard`、`IsCanPaste`、`PasteFromClipboard` 入口与 Unity 原生 Clipboard 格式。粘贴先在独立 SerializedObject 缓冲中解析并完整核对，再以 `ApplyModifiedProperties` 应用；同类型、非混值的单对象和多对象可编辑，沿用真实 Undo/Redo 与 Prefab override。拒绝操作不会应用调用者的待修改数据。

混值、不同类型的多对象、不可编辑目标、不兼容或损坏的数据明确拒绝；原生解析器不能可靠处理的 ManagedReference、ExposedReference、FixedBufferSize 和 double 也拒绝。`IsCanPaste` 对这些输入返回 `false`，实际复制/粘贴抛出 `InvalidOperationException` 说明原因；Unity 私有 API 缺失或签名变化时抛出包含 Unity 版本与成员信息的 `NotSupportedException`，不静默回退。空 ObjectReference/ExposedReference 的 `GetValueText` 显示 `None`。专项回归位于 `Tests/Core/Editor/CoreSerializedPropertyTests.cs`。

CustomClassNameGUI / CustomPropertyGUI 当前没有正式业务标注或内置读取器；DisableEdit 的必要 Drawer 已正确接线。原 XF 生态继续消费原包特性，不建立跨框架桥；分析器/整个 Editor 工具整合不属本次元数据归口。

## 备注

- 未来接入时建议在 `Editor/` 下实现最小消费者（如由现有 EditorWindow 直接读取字段特性调用 `OnPropertyGUI`），不引入注册表 / 全局 hook / 自动扫描。
