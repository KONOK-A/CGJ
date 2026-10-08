# Extensions（扩展方法）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

集中存放常用 Unity 与 System 类型的扩展方法与小型静态工具，减少样板代码；框架内部（Resource、Events、Singleton、Pool、UI 等）也使用其中的辅助方法。

## 位置

- 运行时：[`Runtime/Tools/Extensions/`](../../Runtime/Tools/Extensions/)
  - `Unity/`：`GameObjectExtension`、`ComponentExtension`、`MonoBehaviourExtension`、`ObjectExtension`、`RectTransformExtension`、`PositionSyncExtension`、`CameraExtension`、`ColorExtension`、`UIGraphicExtension`
  - `System/`：`SystemStringExtension`、`SystemIOExtension`、`SystemCollectionsExtension`、`SystemObjectExtension`、`SystemReflectionExtension`
  - `Math/`：`MathExtension`、`VectorExtension`
  - `Utilities/`：`RandomUtility`、`LayerMaskUtility`

## 主要功能

- Unity：显示隐藏、父子层级、位置/旋转/缩放链式设置、组件获取与自动添加、销毁、位置同步、相机截图、颜色与 UI alpha/fill。
- System：字符串解析与清洗、路径/目录/文件操作、集合遍历与字典合并、托管引用判空、反射与 Attribute 查询。
- 数学：随机取项、角度/弧度换算、三角函数、方向与距离换算。

## 关键约定

### Unity fake-null

- 模块内对 UnityEngine.Object 的判空统一使用 Unity 的 `== null` 语义：已被 Destroy 的对象视为空。
- `DestroySelfGracefully` / `DestroySelfWithDelayGracefully` / `DestroyGameObjGracefully` 会跳过 null 与已销毁对象；非 Gracefully 版本不做判空。
- `SystemObjectExtension.IsNull / IsNotNull` 是**托管引用判空**，不能识别 Unity fake-null；判断 Unity 对象请直接写 `obj == null`。
- `ObjectExtension.InstantiateWithParent(Component parent, ...)` 把已销毁的 parent 视为 null（挂到场景根）；`Transform` 重载本身接受 null。

### Culture

- 本模块没有 `ToLower / ToUpper` 等标识符大小写转换 API，不存在区域敏感的大小写归一问题。
- `ToInt / ToLong / ToFloat / ToDateTime` 使用当前 Culture 解析，失败返回默认值；机器数据调用方需自行固定 Culture。
- `ColorExtension.HtmlStringToColor` 基于 `ColorUtility`，与 Culture 无关。

### IO

- 路径拼接、文件名、扩展名、目录名都委托 `System.IO.Path`，兼容 Windows 与 Unix 分隔符。
- `CreateDirIfNotExists` 幂等；`DeleteDirIfExists` 不存在时为空操作；`EmptyDirIfExists` 会清空已有目录，目录不存在时会直接创建。
- IO 异常向上抛出，不吞掉。

### Math / Vector

- `float.Lerp(a, b)` 的 `self` 是插值系数 t，会被 clamp 到 0~1。
- 零向量 `normalized` 返回零向量；`ToAngle / ToRadius` 对零向量返回 0。
- `Abs(int)` 返回 `float`（历史 API，保留兼容）。

### Random / LayerMask

- `RandomUtility` 使用 `UnityEngine.Random` 全局随机状态；`Percent(p)` 中 p≤0 恒为 false、p≥1 恒为 true；`Choose` 空数组抛异常。
- `LayerMaskUtility` 只接受 0..31 的层号；越界 layer 或 null GameObject 返回 false。

## 主要消费者

- Resource：`InstantiateWithParent`、`GetOrAddComponent`。
- Events：`GetOrAddComponent`。
- Singleton / Pool：`DestroyGameObjGracefully`、`DestroySelfGracefully`。
- UI：`IsNotNullAndEmpty`、`Parent`、`Show/Hide`、`Destroy*`。
- Editor（Resource 工具）：`CreateDirIfNotExists`、`DeleteDirIfExists`、`GetFolderPath`、`RemoveInvalidCharsFromClassName`、`RemoveString`。

## 已知限制

- `Split(this string, char)` 扩展在当前 .NET Standard 2.1 下会被 BCL 实例方法遮蔽，仅作为兼容 API 保留，可用 `SystemStringExtension.Split(s, ',')` 静态调用；null 输入保持历史语义抛 `NullReferenceException`。
- `Merge` 不支持重复 Key：键同值不同的键值对会抛 `ArgumentException`。
- `ReflectionCallPrivateMethod` 支持当前类型的 private/protected 与继承的 protected 方法；不查找公共方法与基类 private 方法，不支持泛型方法定义；方法不存在返回 null，重载歧义抛 `AmbiguousMatchException`。
- `Layer(this GameObject/Component, string)` 传入未定义层名时会照旧写入 -1。
- `CameraExtension.CaptureCamera` 的 rect 以屏幕像素为单位，返回的 Texture2D 由调用方持有并销毁。
- `GetRandomItem` / `Choose` 在空集合上抛异常，不做静默默认值。

## 测试

- `Tests/EditMode/Extension*.cs`：API、边界、fake-null（DestroyImmediate 制造）、Culture、IO、反射、Random / LayerMask。
- `Tests/PlayMode/ExtensionLifecyclePlayModeTests.cs`：Destroy 延迟时序与 fake-null 行为。
- 消费者间接回归：`GameObjectLoaderPlayModeTests` 等 Resource / Singleton / Pool 测试。

## 当前状态

- 首轮：目录按 Unity / System / Math / Utilities 分组，补齐简洁中文 XML 文档，修复 null / 越界 / 异常路径 / 重复实现等边界问题。
- 第二轮稳定性复核：以边界测试锁定 fake-null/Destroy 时序、InstantiateWithParent（inactive/Component 与 Transform 一致性）、CurrentCulture 解析、Split（含并行）、Collections 退化输入、Reflection 搜索范围、Camera 异常状态恢复、LayerMask 极端值、Math/Vector 特殊值、IO 路径误用；修复 `Dictionary.AddRange` 自合并覆盖时的枚举失效问题。public API 未变化。
