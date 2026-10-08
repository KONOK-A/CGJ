# Attributes（编辑器显示特性）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供两个用于编辑器显示 / 绘制的元数据特性，供编辑器扩展与业务代码读取使用。

定位边界：不是 Odin-like Inspector 框架 / PropertyDrawer 框架 / Attribute Processor，不提供 Attribute 自动扫描或注册系统。

## 位置

- 运行时：[`Runtime/Tools/Attributes/`](../../Runtime/Tools/Attributes/)

## CustomClassNameGUI

- 语义：为**类型**提供编辑器中的自定义显示名称（纯元数据）。
- 目标：`AttributeTargets.Class | Struct | Interface | Enum`；`AllowMultiple = false`；`Inherited = false`（派生类型不继承显示名，需自行标注）。
- 构造：`CustomClassNameGUI(string name)`，允许 `null` / 空字符串；`GetName()` 原样返回，不会 NRE。调用方遇到 `null` / 空字符串时应回退为类型名。
- 消费方式：由编辑器代码读取，框架内暂无内置消费者。

## CustomPropertyGUI

- 语义：标记需要自定义 Editor 绘制的序列化字段（metadata + Editor 契约）。
- 目标：`AttributeTargets.Field`；`AllowMultiple = false`；`Inherited = false`。
- 契约：抽象特性，仅供派生；编辑器下派生类**必须实现** `OnPropertyGUI(UnityEditor.SerializedProperty)`，由外部编辑器代码读取字段上的派生特性后调用（框架内暂无内置消费者 / 绘制器）。
- 历史设计保留：该 Editor 行为不迁出 Runtime——移除后类型只剩空壳；消费方应位于 `Editor/`，以自行读取字段特性的方式接入。

## Runtime / Editor 边界

- 两个特性都位于 Runtime 程序集（`YLFramework.Runtime`），不依赖其他 YLFramework 模块。
- 仅 `CustomPropertyGUI.OnPropertyGUI` 位于 `#if UNITY_EDITOR` 内：Editor 编译时存在、Player 编译时不存在，Player 程序集不会残留 `UnityEditor` 引用。
- 使用 `CustomPropertyGUI` 的业务代码需注意：Player 下没有 `OnPropertyGUI`，派生类中的重写必须包在 `#if UNITY_EDITOR` 内。
- `AttributesTests.CustomPropertyGUI_Source_Keeps_UnityEditor_Guarded` 会在 EditMode 下守卫该边界（源码中所有 `UnityEditor` 引用必须处于 `#if UNITY_EDITOR` 区间）。

## 主要类型

- `CustomClassNameGUI`：类型显示名特性。
- `CustomPropertyGUI`：字段自定义绘制抽象特性。

## 依赖

- `System`；`CustomPropertyGUI` 的绘制方法条件编译依赖 UnityEditor（`#if UNITY_EDITOR`）。

## 当前状态

已完成并含专项测试：`Tests/EditMode/AttributesTests.cs`（AttributeUsage、构造 / `GetName`、继承行为、abstract 契约、Editor 方法存在性、字段标注读取、源码边界守卫）。

当前工程内暂无调用方：没有字段 / 类型使用这两个特性，也没有消费它们的编辑器代码，模块处于“metadata 已定义、Editor 消费者待接入”的状态。

## 备注

- 未来接入时建议在 `Editor/` 下实现最小消费者（如由现有 EditorWindow 直接读取字段特性调用 `OnPropertyGUI`），不引入注册表 / 全局 hook / 自动扫描。
