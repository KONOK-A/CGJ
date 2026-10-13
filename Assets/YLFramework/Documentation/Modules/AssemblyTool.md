# AssemblyTool（反射查找工具）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供“按类型完整名称在当前 AppDomain 已加载的程序集中查找 `Type`”的小工具，并缓存成功结果。

定位边界：不做 Assembly Scan Framework / Type Registry / Plugin Loader / 反射数据库，只做可靠的类型名查找。

## 位置

- 运行时：[`Runtime/Shared/AssemblyTool.cs`](../../Runtime/Shared/AssemblyTool.cs)，程序集 `YLFramework.Foundation.Runtime`。

## 查找规则

- `AssemblyTool.GetType(classFullName)`：
  - `null` / 空字符串 / 仅空白 → 返回 `null`；
  - 否则遍历 `AppDomain.CurrentDomain.GetAssemblies()`，逐个执行 `assembly.GetType(classFullName)`，返回**第一个**匹配的 `Type`；多个程序集存在同名类型时不建立优先级规则；
  - 未找到 → 返回 `null`，不抛异常；
  - 支持嵌套类型（`Outer+Inner`）与泛型定义形式（如 `` System.Collections.Generic.List`1 ``）；
  - 只接受类型完整名称，不保证支持程序集限定名（assembly-qualified name）解析；畸形名称可能由底层 `Assembly.GetType` 原样抛出异常（fail-fast，不做拦截）。

## Cache 语义

- **只缓存成功结果**：找到的 `Type` 写入 Ordinal 字典，重复查询直接命中；缓存键不自行裁剪或转换大小写，底层类型名解析仍交由 `Assembly.GetType`；
- **失败不缓存**：不存在的类型不写缓存，运行期间新加载程序集后再次查询仍可找到，不存在永久 stale miss；
- 无 `ClearCache` 等手动失效 API：成功缓存的 `Type` 随程序集存活，Unity 运行期不卸载程序集，缓存不会失效；
- 非线程安全，按 Unity 主线程 / 普通单线程使用（不使用并发 cache、lock、weak cache、后台扫描）。

## 主要类型

- `AssemblyTool`：静态工具类，唯一 public 成员为 `GetType(string)`。

## 依赖

- 无框架内模块依赖（纯 C# / `System` / `System.Reflection`）。

## 当前状态

定向测试位于 `Tests/Foundation/EditMode/AssemblyToolTests.cs`：null/空白、完整名与大小写、程序集限定名不作为查找入口、嵌套/开放泛型、成功缓存、失败后的晚加载与同名类型首匹配。

实际消费者为 Core 的 Module 字符串 Controller 查询/创建与 ModuleManager 字符串启动。Core Editor Clipboard 直接从 Unity Editor 程序集解析原生成员并核验签名，不再使用全局类型名查找。原自有 `Core.AssemblyTools` 与 `Utilities.AssemblyTool.AssemblyTool` 已退出，不保留转发壳；原 XF Package 不修改。

## 备注

- 命名空间为 `YLFramework.Foundation`，入口为 `AssemblyTool.GetType`。
- 调用方应传入完整类型名（`typeof(T).FullName` 即可）。
