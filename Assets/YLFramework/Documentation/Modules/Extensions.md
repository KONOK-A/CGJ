# Extensions（扩展方法）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

集中存放常用 Unity 与 System 类型的扩展方法与小型静态工具，减少样板代码；框架内部（Resource、Events、Singleton、Pool、UI 等）也使用其中的辅助方法。

## 位置

- 运行时：[`Runtime/Tools/Extensions/`](../../Runtime/Tools/Extensions/)
  - `Unity/`：`GameObjectExtension`、`ComponentExtension`、`MonoBehaviourExtension`、`ObjectExtension`、`RectTransformExtension`、`PositionSyncExtension`、`CameraExtension`、`ColorExtension`、`UIGraphicExtension`
  - `System/`：`SystemStringExtension`、`SystemIOExtension`、`SystemCollectionsExtension`、`SystemObjectExtension`、`SystemReflectionExtension`
  - `Math/`：`MathExtension`、`VectorExtension`
  - `Utilities/`：`RandomUtility`
- 所有通用扩展统一 namespace `YLFramework.Utilities.Extensions`，归 `YLFramework.Runtime`。原 Core 的 Animator / Bounds / Color / EventTrigger / LayerMask / Quaternion / Random / Ray / Rigidbody / Scene / Time / Type / Vector2 / Vector3 扩展已按能力进入上述目录，类型及方法名不为形式统一重命名。
- 字节与进度显示：[`Runtime/Tools/StringTools.cs`](../../Runtime/Tools/StringTools.cs)，namespace `YLFramework.Utilities`，归 Runtime。
- 共用成员判断：[`Runtime/Foundation/Kernel/LayerMaskUtility.cs`](../../Runtime/Foundation/Kernel/LayerMaskUtility.cs)，namespace `YLFramework.Foundation`，供 Core Listener 与 Runtime 包装扩展使用。
- 旧 `YLFramework.Core.Extensions` 与 `YLFramework.Core.StringTools` 不保留转发壳；消费者改用上述 namespace，并显式引用 Runtime。旧 GameObjectExtensions / ComponentExtensions 类型退出，递归设层归 `GameObjectExtension.SetLayer`，唯一 Unity fake-null 查询归 `ObjectExtension.IsDestroy(UnityEngine.Object)`，GameObject / Component 扩展调用仍可直接使用。

## 主要功能

- Unity：显示隐藏、父子层级、位置/旋转/缩放链式设置、组件获取与自动添加、销毁、位置同步、相机截图、颜色与 UI alpha/fill。
- System：字符串解析与清洗、路径/目录/文件操作、集合遍历与字典合并、托管引用判空、反射与 Attribute 查询。
- 数学：随机取项、角度/弧度换算、三角函数、方向与距离换算。

## 关键约定

### Unity fake-null

- 模块内对 UnityEngine.Object 的判空统一使用 Unity 的 `== null` 语义：已被 Destroy 的对象视为空。
- `DestroySelfGracefully` / `DestroySelfWithDelayGracefully` / `DestroyGameObjGracefully` 会跳过 null 与已销毁对象；非 Gracefully 版本不做判空。
- `SystemObjectExtension.IsNull / IsNotNull` 是**托管引用判空**，不能识别 Unity fake-null；判断 Unity 对象请直接写 `obj == null`。
- `ObjectExtension.IsDestroy` 对 null / 已销毁 Unity 对象返回 true；对存活 GameObject / Component 返回 false，与托管引用判空不合并。
- `ObjectExtension.InstantiateWithParent(Component parent, ...)` 把已销毁的 parent 视为 null（挂到场景根）；`Transform` 重载本身接受 null。

### Culture

- 本模块没有 `ToLower / ToUpper` 等标识符大小写转换 API，不存在区域敏感的大小写归一问题。
- `ToInt / ToLong / ToFloat / ToDateTime` 使用当前 Culture 解析，失败返回默认值；机器数据调用方需自行固定 Culture。
- `ColorExtension.HtmlStringToColor` 基于 `ColorUtility`，与 Culture 无关。
- `StringTools.FormatByte(long)` 保持当前 Culture 的人类显示、1024 进制及 b/kb/mb/gb/tb 后缀；TB 以上继续以 tb 显示，最大 long 不返回空文本。小于 1024 的历史有符号 b 显示不变，不作为机器解析格式。

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
- `Percent` 对分数百分比向上离散；Runtime `RandomExtensions.Random` 先用 `Mathf.RoundToInt` 离散到百分比，再用 1..100 的整数判定。两者并非同算法，端点是否推进全局 RNG 状态也不同；保留各自入口及既有行为，不将名称相似视为等价。
- Foundation `LayerMaskUtility.Contains` 与 `IsInLayerMask` 共用严格 0..31 成员判断；非法层号及未定义层名返回 false，不再按 32 取模。null 或已销毁 GameObject/Component 的 Runtime 包装返回 false，签名不变。
- Runtime `LayerMaskExtensions.Set` / `PhysicsSetting` / `Physics2DSetting` 非法层号抛 `ArgumentOutOfRangeException`，掩码不变；未定义层名的 Set 同样拒绝。有效 bit0/bit31 保持原语义。Physics 方法仍只读取当前矩阵，仍只将已命名且可碰撞的层加入返回掩码，不修改全局碰撞设置。
- `GameObjectExtension.Layer` 仅改自身；`SetLayer` 改自身及默认 `GetComponentsInChildren<Transform>()` 收集的活跃子孙，不改 inactive 子孙。两者层名入口仍沿用未定义层名写入 -1 的 Unity 行为，不与严格掩码 Set 混同。

### Ray / Quaternion / Rigidbody / Type

- Ray 的旧 `MiniDistance`、两个 `MiniDistancePoint` 与 `Distance(Vector3)` 保留**无限直线**用途；最近点参数可为负。距离非负、同向/反向平行均可求一组最近点，零方向视为原点。新增 `DistanceToRay(Vector3)` 才限制射线半轴，投影在原点后方时计算到原点的距离，不能用旧方法承诺半轴最近点。
- `QuaternionExtensions.InverseLerp` 是最短角距离比，不判断 current 是否在 from/to 同一插值弧上，也不 clamp 到 0..1。有限非零四元数先归一化，q/-q 等价；同端点返回 0，角差使用双精度 atan2 差长/和长计算，不用 `Quaternion.Angle` 的小角度死区。零或非有限四元数抛 `ArgumentException`，无法表示为有限 float 的比例抛 `InvalidOperationException`。
- `ParabolicMotionVelocity` 是无阻力、无运动约束、恒定重力的连续解，不是物理运动控制器或原生离散积分的精确落点保证。3D 尊重 `useGravity` 及完整 `Physics.gravity`；2D 尊重 `Physics2D.gravity * gravityScale` 的方向，忽略目标 Z。指定 Y 初速度后选最晚的正到达时间，通常为下落段；零重力有正时间时可作直线求解，零初速度向低处自由落体仍可求解。
- 抛物线不可达、没有正到达时间、零竖直重力且零竖直初速度导致时间无法确定，或所需速度超出有限 float 时抛 `InvalidOperationException`；非有限输入抛 `ArgumentException`，null/已销毁刚体抛 `ArgumentNullException`。`ParabolicMotion` 只在计算成功后写速度，拒绝路径保留已有运动状态；不改重力、drag、constraints 或对象生命周期。
- `GetDelegateDisplayName` 保持 `Name(ArgName,...)` 风格；FullName 不可用时回退到 Name，开放/部分构造泛型也列出泛型参数，移除泛型元数但保留嵌套类型名。null Type 抛 `ArgumentNullException`，不是委托签名反射平台。

### Collections / Camera

- `IEnumerable.ForEach` 先物化快照，数组输入直接复用原数组；字典 `ForEach` 是实时枚举，遵循字典自身的迭代修改限制和异常，不为统一语义添加快照分配。反向 List 与带索引遍历保持各自用途。
- `CaptureCamera` 同步借用相机，恢复原 `targetTexture` 与 `RenderTexture.active` 并销毁自身临时 RenderTexture；返回 Texture2D 由调用方销毁。rect 须落在当前屏幕像素范围，本批不改变其既有前提与资源责任。

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

- 共用 LayerMask 的真实包装、Unity null 与 Core Trigger 3D/2D 回调过滤回归：`Tests/Core/EditMode/NeutralLayerMaskConsumerTests.cs`。该测试直接调用真实过滤消费者，不代表物理引擎自动分发门禁。
- `Tests/EditMode/Extensions/ToolsExtensionsContractTests.cs` 与 `ParabolicMotionContractTests.cs`：非法层、单位阈值、Ray 直线/半轴、Quaternion 等价表示、开放 Type、失效 Component、唯一 Object 存活入口、单对象 / 递归设层，以及抛物线可达/退化拒绝。
- `Tests/PlayMode/Extensions/ParabolicMotionNativeTests.cs`：只模拟自有局部 3D/2D PhysicsScene，验证速度赋值、退化输入不改速度和无重力运动，区分连续解与原生离散积分误差。
- 原文所列 `Tests/EditMode/Extension*.cs` 当前不存在，不作为已执行覆盖的证据；无关扩展的覆盖口径留阶段 5 后续收口。
- `Tests/PlayMode/ExtensionLifecyclePlayModeTests.cs`：Destroy 延迟时序与 fake-null 行为。
- 消费者代表性回归：现存 `ResourcePoolTests`、`UnityEventListenerContractTests`、`SingletonHostPlayModeTests`。历史提及的 `GameObjectLoaderPlayModeTests` 当前不在仓库，不作为已执行证据。

## 当前状态

- 首轮：目录按 Unity / System / Math / Utilities 分组，补齐简洁中文 XML 文档，修复 null / 越界 / 异常路径 / 重复实现等边界问题。
- 第二轮稳定性复核：以边界测试锁定 fake-null/Destroy 时序、InstantiateWithParent（inactive/Component 与 Transform 一致性）、CurrentCulture 解析、Split（含并行）、Collections 退化输入、Reflection 搜索范围、Camera 异常状态恢复、LayerMask 极端值、Math/Vector 特殊值、IO 路径误用；修复 `Dictionary.AddRange` 自合并覆盖时的枚举失效问题。public API 未变化。
- 阶段 5.3 修复的几何 / 掩码 / 格式化算法在 5.4 原样迁移；5.4 收敛正式归属与 Object / GameObject 入口，成熟 FSM / Grid / Json / Table / ZipTool 定位和内部实现不变。历史稳定性说明不代替本轮逐项验证记录。
