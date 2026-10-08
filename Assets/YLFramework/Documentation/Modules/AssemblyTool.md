# AssemblyTool（反射查找工具）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供“按类型完整名称在当前 AppDomain 已加载的程序集中查找 `Type`”的小工具，并缓存成功结果。

定位边界：不做 Assembly Scan Framework / Type Registry / Plugin Loader / 反射数据库，只做可靠的类型名查找。

## 位置

- 运行时：[`Runtime/Tools/AssemblyTool/`](../../Runtime/Tools/AssemblyTool/)

## 查找规则

- `AssemblyTool.GetType(classFullName)`：
  - `null` / 空字符串 / 仅空白 → 返回 `null`；
  - 否则遍历 `AppDomain.CurrentDomain.GetAssemblies()`，逐个执行 `assembly.GetType(classFullName)`，返回**第一个**匹配的 `Type`；多个程序集存在同名类型时不建立优先级规则；
  - 未找到 → 返回 `null`，不抛异常；
  - 支持嵌套类型（`Outer+Inner`）与泛型定义形式（如 `` System.Collections.Generic.List`1 ``）；
  - 只接受类型完整名称，不保证支持程序集限定名（assembly-qualified name）解析；畸形名称可能由底层 `Assembly.GetType` 原样抛出异常（fail-fast，不做拦截）。

## Cache 语义

- **只缓存成功结果**：找到的 `Type` 写入 `TypeCacheDict`，重复查询直接命中；
- **失败不缓存**：不存在的类型不写缓存，运行期间新加载程序集后再次查询仍可找到，不存在永久 stale miss；
- 无 `ClearCache` 等手动失效 API：成功缓存的 `Type` 随程序集存活，Unity 运行期不卸载程序集，缓存不会失效；
- 非线程安全，按 Unity 主线程 / 普通单线程使用（不使用并发 cache、lock、weak cache、后台扫描）。

## 主要类型

- `AssemblyTool`：静态工具类，唯一 public 成员为 `GetType(string)`。

## 依赖

- 无框架内模块依赖（纯 C# / `System` / `System.Reflection`）。

## 当前状态

已完成并含专项测试：`Tests/EditMode/AssemblyToolTests.cs`（null/空白、不存在、成功缓存、失败不缓存、BCL / 框架类型、嵌套类型、开放泛型）。

当前工程内暂无生产调用方；测试内会读取私有缓存字段验证缓存语义。

## 备注

- 命名空间为 `YLFramework.Utilities.AssemblyTool`，避免与 `System.Reflection.Assembly` 同名。
- 调用方应传入完整类型名（`typeof(T).FullName` 即可）。
