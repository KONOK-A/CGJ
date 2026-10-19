# UI（统一 Panel / View）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

> 当前状态：已整合到 `integration/xf-yl`，但真实 Unity 输入门禁失败，阶段为 **Blocked，未验收完成**。当前 `2022.3.62f3c1` 不允许同 GameObject 的第二个 CanvasGroup，与下述已确认的独立门禁结构冲突；不得按已可用能力交付。未获结构调整授权前，不覆盖业务字段、不改变不支持范围，也不继续依赖该结论的场景、Profiler 和 IL2CPP 验收。逐项结果及剩余失败记录在仓库 `.kilo/plans/1791960935353-ui-panel-redesign.md`。

## 职责与依赖

- `Module` 是唯一业务 owner。`Panel : View` 只扩展独立 UI 会话，复用 Core 的 Model、事件、请求失效与实例归还协议。
- 主体在 `Runtime/Core/UI/Windows`，根、输入、Tips、适配分别分组；namespace 为 `YLFramework.Core`，程序集为 `YLFramework.Runtime`。主体依赖 UGUI，不要求 XFABManager。
- `PanelManager` 是同一个协调器的 partial，不是资源服务或第二套窗口 owner。不存在必须先注册的窗口目录、业务 key、另一套加载器或 UI 池。
- 普通根 View 使用 `LoadView/LoadViewAsync`，已有实例使用 `AttachView`；根组件实际是 Panel 时必须使用 Panel 入口。普通 View 的 object[]、LoadViewArgs、Model 和生命周期契约不变。

## 打开与配置

```csharp
owner.PanelRoot = uiRoot;
owner.BindPanelRules(new[]
{
    new PanelRule(colorPrefab, new PanelOverrides { Modal = true, Priority = 10 })
});
var window = owner.LoadPanel<ColorSelectorWindow>(colorPrefab,
    ColorSelectorWindow.Arguments(initialColor, OnColorChanged));
window.UpdateDisplay(new PanelOverrides { Priority = 0, Modal = false });
window.Hide();
window.Show();
window.Close();
```

正式入口为 `LoadPanel<T>()`、`LoadPanel<T>(string assetName, ...)`、`LoadPanel<T>(GameObject prefab, ...)`，都有同形 Async；string/GameObject 也有非泛型版本。后续参数依次为 `LoadViewArgs`、`UIRootContext root`、`PanelOverrides`。类型简写只将 `typeof(T).Name` 原样作为资源地址；只有 Core provider 的资源目录精确登记该地址或配置了显式别名，才能解析，不表示按类型搜索目录。例如仅登记 `ui/ColorSelector.prefab` 时，应显式调用 `LoadPanel<ColorSelectorWindow>("ui/ColorSelector.prefab")`，不能依赖类型简写猜完整路径、文件扩展名或 basename。

命名入口需要显式 Core provider，并冻结接受时的 provider 与 Module project fallback；完整地址严格区分大小写，不比较文件名或猜测其他 project。直接 Prefab 不要求 provider，调用方仍须保证其来源资产及依赖生存期，C# 对象引用不能替代资源 lease。

每次打开按以下顺序逐字段合并，顺序靠后的已指定字段覆盖前者：

1. Prefab 的序列化默认 `settings`。
2. Module 最近一次成功绑定的规则快照。
3. 本次 `PanelOverrides`。

| 字段 | 默认 | 含义 |
|---|---|---|
| Layer | 0 | 根内明确配置的层索引，不存在就拒绝 |
| Priority | 0 | 同层同 Modal 分组内的业务优先级，不是 Canvas 原始顺序 |
| Modal | false | 阻挡同根低于它的标准 UI 输入 |
| CoversRoot | false | 声明完整覆盖，允许低于它的同根 Panel 停绘，不改 RectTransform |
| ParticipatesInTop | true | 是否进入 Top 查询，与 Modal 独立 |
| InstanceMode | Single | 同 Module、同实际根、同可证明来源的实例模式 |
| CacheOnClose | true | 有效会话正常结束时是否允许既有后端缓存 |

配置值类型统一为 `YLFramework.Core.PanelConfig`，避免与 UI Toolkit 的同名设置类混淆；不保留旧类型别名。`PanelConfig.Default` 才是框架默认值，结构体零值并不等价。Panel 的序列化字段仍名为 `settings`，七个成员及自然 bool 语义不变；更名不表示已取得 Prefab/Variant 导入迁移证据。`PanelOverrides` 的 nullable 字段用 null 表示未指定，显式 false/0 必须生效，不作 Inspector 序列化。

`BindPanelRules` 先枚举、复制并完整校验，再原子替换；失败保留旧快照。null/空集合清空可选规则。在途请求不追随重绑，活跃会话不被静默修改。规则按完整地址或 Prefab 二选一；重复身份、白地址、销毁引用、非法值以及资源解析后证明的多规则冲突均失败，不任意挑选一条。同一资源可有多个业务地址，但不会建立跨会话的永久别名表。

## 实例与请求

- Single 重开健康会话会借读新参数调用 `OnOpen`，有效提交后恢复显示并更新序号，不重新走 OnLoaded、Model 或事件注册。未指定的临时字段从 Prefab/规则重新合成，不继承上次临时覆盖。
- Multiple 每次建立独立会话。活动或已确认的在途同源范围不能混用两种模式；原 Single 关闭后才可重新确定模式。
- 每次 Async 都有独立 `LoadPanelRequest<T>`、参数、Abort 和终态，一对一进入 `Module.pendingViews`。接受成功先返回 Pending，缓存、直接来源或已有 Single 也不在接受栈内调用数据回调。
- 解析到相同 Prefab 后，在提交前筛掉取消、错型和非法配置，Single 只用最高有效接受序号的意图打开一次。未知来源可以并行解析，不假设不同地址必然相同。
- 同步调用不等待或接管已确认的异步 Single 首开；若健康实例已存在，可重开，但迟到的旧请求不倒灌旧数据。有效 Close 对关闭前接受的同源请求形成失效屏障。
- Abort 只影响本消费者，在首个业务初始化回调前有效；进入提交或失败清理后不抢占既定终态。完成通知前归还参数、退出事务，并在每个通知前复核原 Session、Module 代次、根与资源责任。前一个观察者 Close 后复用同一 GameObject，不能使后续旧消费者获得新 Session 的成功结果。
- `panel` 非空只代表该次交付时的成功引用，不延长会话寿命；检查 `Status/Exception`，不能只看 `progress == 1`。观察异常不阻止其他消费者收尾，原错误保留并附带清理错误。

### 三个短场景

下面的 `owner` 已有效启动，`prefab` 是同一个存活 Panel Prefab 引用，`rootA/rootB` 是已配置的不同实际根；每次参数袋都独立创建。

```csharp
// 1. 同 Module + 同 root + 同可证明资源才是同一个 Single 范围。
var a = owner.LoadPanel<ColorSelectorWindow>(prefab, root: rootA);
var again = owner.LoadPanel<ColorSelectorWindow>(prefab, root: rootA); // 同会话，有效重开。
var otherRoot = owner.LoadPanel<ColorSelectorWindow>(prefab, root: rootB); // 另一会话。
```

```csharp
// 2. 同源请求已在首开提交前合流时，最新有效意图只打开一次。
var red = owner.LoadPanelAsync<ColorSelectorWindow>(prefab,
    ColorSelectorWindow.Arguments(Color.red), rootA);
var blue = owner.LoadPanelAsync<ColorSelectorWindow>(prefab,
    ColorSelectorWindow.Arguments(Color.blue), rootA);
yield return red;
yield return blue;
// 若均 Succeeded 且原 Session 仍存活，可返回同一引用；其内容来自 blue 的最新意图，
// red 成功不保证窗口永远保留红色。两个消费者仍分别有自己的 Status/Exception。
```

```csharp
// 3. 主动取消只在首个业务初始化回调前有效，终态和已提交会话不被晚到 Abort 撤销。
var canceled = owner.LoadPanelAsync<ColorSelectorWindow>(prefab, root: rootA);
canceled.Abort(); // 接受后仍 Pending、尚未提交：仅取消这一消费者。
var live = owner.LoadPanelAsync<ColorSelectorWindow>(prefab, root: rootA);
yield return live;
live.Abort(); // 已终结：不改变结果，也不 Close 已交付的会话。
```

进入提交的 OnLoaded/OnOpen 内到来的 Abort 同样不生效。若完成通知中有消费者 Close 并复用物理对象，后续旧请求必须 Invalidated，不能凭同一对象引用续接新 Session。`Hide` 保留会话但暂不显示，`Close` 结束会话；Top 只是同根排序候选，HasFocus 是框架焦点候选，均不等于 EventSystem 实际选择或一定可交互，交互还受可见性、模态和标准控件自身业务值约束。

## 生命周期与显示

首开沿 View 同一个事务：归属/Model 绑定 → Panel 根/Canvas/顺序/门禁准备 → 旧 `OnLoaded(object[])` 与 `OnLoaded(LoadViewArgs)` → 设置 loaded → `OnOpen(LoadViewArgs)` → 恢复事件与 `OnViewEnabled` → 统一显示/输入协调 → 交付。

`LoadViewArgs` 由入口 Claim 并在终态归还，借读期间不能保存到字段、属性、委托或容器。普通 View 的参数钩子不变；Panel 的新数据逻辑写 protected `OnOpen`，已有实例经有效 `AttachView` 新会话也调用它。

`OnOpen` 可读 EffectiveSettings、Root、SortingOrder，但不能 Close 当前 Panel/owner、重入同 Module 的同步或异步 Panel 打开，也不能 Hide/Show/BringToFront/UpdateDisplay；这些操作立即抛错，不排队。关闭禁令同样作用于 Module.ClearView 的统一出口和会退役当前 Panel 的 backend 祖先，不允许批量出口先产生部分退出副作用；真实销毁与内部 rollback 仍可清理。其他业务操作沿既有 Core 契约。OnLoaded/OnViewEnabled 中允许的显示调整会保留，初始化尾部不会重放初始 overrides 覆盖它们。自有准备/数据/启用失败回滚或丢弃，其他会话观察异常独立报告。

- `Hide/Show` 只改变显示意图，保留 Module、Model、事件和 Session，不重新初始化或调用 OnOpen。根临时停用、Canvas 临时禁用同样暂停显示/输入，而不是另开会话。
- `UpdateDisplay` 只接收 Layer、Priority、Modal、CoversRoot、ParticipatesInTop，先完成校验再原子提交；拒绝另外两个生命周期字段，不调用 OnOpen。换层使用 `SetParent(false)` 保留局部布局，不拉伸。各层容器的尺寸、锚点或 pivot 不同时，即使保留窗口局部布局参数，屏幕位置/尺寸也可能改变；这不是跨层自动对齐承诺，需由层容器或业务布局配置解决，不要求所有层相同。
- 顺序为 Layer → 同层 Modal 分组 → Priority → 最近接受/前置序号。`BringToFront` 只改最后一项，不能穿越层、模态或优先级；生成 `SortingOrder` 只读，并预留模态遮罩槽位。
- `GetTopPanel(root)` 可返回被更高模态阻挡但仍可见且参与 Top 的 Panel；`GetModalPanel` 包含不参与 Top 的模态。Focus 与 EventSystem 实际选择不等价。
- CoversRoot 只停绘低于它的同根 Panel，不决定布局、实例模式或 Modal；不会影响另一根。旧会话 Close 后的显示命令不能复活它，销毁对象的 IsTop/HasFocus 为 false。

## 根与资产

请求根优先级：显式 root → `Module.PanelRoot` → `YLFrameworkRoot` 宿主默认根。接受时固定真实根；显式或绑定根销毁就失败，不回退或换根。Audio/Action/非 UI Module 不会因为访问普通宿主层级而创建 UI。

首次显式传入 inactive 根也直接校验其场景、原生组件和明确层映射并准备 Panel，不依赖该根先执行 OnEnable；不会强行激活。inactive 仅暂停可见/输入，恢复时仍是原 Session；必需组件或层容器销毁则属于结构失效，不能与暂停混淆。

`UIRootContext` 仅保存 Canvas、GraphicRaycaster、明确层映射与可选遮罩，既不拥有 Module，也不决定场景寿命。注册自定义根不会使其变成宿主默认根或调用 DontDestroyOnLoad，也不触发 onCreateCanvas；宿主预绑定和懒建默认根在同一执行代次首次准备完成时统一通知一次，保留防重入事务。重复宿主有明确诊断。查询不创建根。

Camera/RenderMode 直接配置原生 Canvas；分辨率和 match 使用原生 CanvasScaler 与 `AutomaticAdaptationCanvasScaler`，根不再保存另一份配置。层 0 也必须明确配置，没有 UIRootContext、缺层、外根映射、损坏的必需组件或遮罩都不能运行时补齐。已有 Panel 的 Attach 只认真实祖先根，不搬去默认根，也不加入资源 Single 合流范围。

主工作区已显式迁移并保存以下资产；身份、配置默认及 Variant 保存重载/实例化检查通过，不代表完整模板的运行时排序和输入已通过。需要迁移时仍显式调用：

1. `YLFramework.Editor.UIPanelAssetMigration.ValidateAssets()`，菜单 `YLFramework/Core/UI Panel/核验资产迁移`。
2. `MigrateAssets()`，菜单 `YLFramework/Core/UI Panel/执行资产迁移`。
3. 基础根 `Runtime/Core/Resources/YLFramework/Core/Canvas.prefab` 补显式 Layer0，保留自身 CanvasScaler 配置。
4. 完整 `Prefabs/YLFramework.prefab` 通过 Unity Prefab API 转换为引用基础根的实例，保留既有层、Camera、Tips、非 UI 内容与差异化缩放值，绑定宿主默认根。
5. 通用 `Prefabs/UITips.prefab` 写 Layer0、Multiple、非 Modal、不覆盖、不参与 Top；创建 `Prefabs/UITipsOverlay.prefab` Variant，仅将 Layer 覆盖为 4，其余继承，并更新完整模板的 Presenter 引用。`ColorSelector.prefab` 显式写新默认配置；Unity 生成新 Variant meta。迁移检查旧 GUID/fileID，不手写嵌套 Prefab 关系或新 GUID。
6. 仓库测试另执行 `UIPanelAssetMigration.MigrateTestFixtures()`，菜单 `迁移测试 Panel 夹具`，保存两个 Core 资源夹具 Panel 的七字段默认；该入口不成为正式导出包对 Tests 的依赖。

入口先完整核验，再分阶段保存，可复跑但不是跨资产的原子事务。失败后先审查实际 diff 和报错；保存后的身份核验失败不等于成功，不能继续宣称验收。不会自动运行、保存用户场景、重写自定义 Tips 绑定或用 InitializeOnLoad 修复旧资产。新增 C# 和 Variant 的 meta 已由 Unity 正常导入生成。

当前 Unity 的 Prefab 转换/保存会重新生成部分匹配对象的嵌套 local fileID；迁移工具在 Unity API 建立继承关系后，按原对象身份恢复明确的 stripped 记录及本地引用，再强制导入、重新核验全部原 ID。不会改带 GUID 的来源引用或生成伪造 GUID。预览生命周期可能采用适配组件的待初始化字段，工具同样保存并恢复原始序列化配置，避免把预览状态写回正式资产。

## 输入边界

本节为需要兑现的既定合同，不是当前引擎已通过的声明。实测普通业务 CanvasGroup（`ignoreParentGroups=false`）已占用组件位置时，第二个自有组也会添加失败；旧会话延迟销毁的组还会影响同帧复用重装。此问题超出仅 earlier ignoreParentGroups 的已允许安全失败组合，不能把它直接改称新的不支持范围来通过验收。

标准 UGUI 有效阻挡与业务原始值相互独立：每个所属 Canvas 有框架 ICanvasRaycastFilter，每个所属 Selectable 有自有 CanvasGroup，始终按最近 Panel 划界。不会保存/恢复业务 `interactable/enabled/raycastTarget/blocksRaycasts`，同值写入也不会丢失。

新增组和过滤组件是可观察的框架门禁，不改业务 CanvasGroup 的 alpha、交互、raycast 或 ignoreParentGroups。祖先 ignoreParentGroups、嵌套 overrideSorting Canvas 与独立子 Panel 分别校验；同对象更早的 ignoreParentGroups 按实际 UGUI 有效性判断，不一概拒绝可隔离的组合。若真实布局跳过自有门禁，明确诊断并丢弃该会话，绝不退回覆盖业务字段。

业务 `GetComponents<CanvasGroup>()` 会观察到框架组；运行期新增业务组时，`GetComponent<CanvasGroup>()` 也可能先返回更早的框架组。业务应持有自己创建/序列化绑定的组引用，不把“第一个组”或组件总数当成业务所有权。框架不通过隐藏组件、禁止业务多组或修改业务组值消除这一可观察性。

独立子 Panel 不能嵌在另一 Panel 的 Selectable 子树内：父 Selectable 的框架组会向下影响子控件，而隔离该组又会跳过业务祖先组。请将独立子 Panel 移到该控件外的同级容器；不通过临时改写子树或恢复旧业务快照伪造支持。

捕获固定 Session、epoch、模块、pointerId、按钮与 device/touch 身份，不能长期把池化 PointerEventData 的可变身份当原始身份。仅定向取消自己的 native press/raw/last/drag/raycast/click 镜像，不 Reset 整个模块或关闭 input actions。ScrollRect 原生结束拖动并停止惯性，仍允许 Elastic/Clamped 自然回位；Scrollbar 结束持续点击，InputField 在物理阻挡前 Deselect/EndEdit。每个回调后核验原 Session。旧手势不能续拖或点击，新的合法手势可恢复。普通 Button 不会被新增为拖拽目标。

设备移除或原模块结束时，即使模块已清空/回收 eventData，也需按固定原设备与会话收尾原控件，不能只删除记录。此路径不再改写池化事件镜像，后来的主手势不会被旧设备收尾；仅 eventData 被复用成其他活动手势时直接丢弃旧记录。指针移除停止按压/拖选，不强行结束仍能由键盘继续的文本编辑；因 Modal/隐藏需要失焦时，仍由独立的合法 Deselect/EndEdit 路径完成。

Input System 适配集中在 InputSystemBridge，验证私有字段布局并对值类型逐层写回，保留物理 isPressed/framePressState/pressTime。布局或 IL2CPP 元数据不匹配是明确失败，不假成功；不预先堆 link.xml。真实 Editor/Player 门通过后才可称其已验证。

EventSystem 只借用已有有效外部系统；缺失时自建，Both 优先 Input System，Legacy-only 使用 Standalone，New-only 不静默退 Legacy；最后有效根退出释放自建对象。多系统/不可用模块有诊断，不改外部寿命。自定义 raw handler、业务 ActionMap、Physics/world-space 非 UI 输入仍由业务查询 Panel 状态或自行管理，不在标准 UGUI 取消承诺内。

在原生 OnSelect/Deselect 的 alreadySelecting 期间不改 EventSystem 锁，不把逻辑 Blocked 当作完成：标准字段收尾、自有组物理阻挡在当前调用内完成，真实选择的解除显式保留到解锁后。InputField 还需检查并取消其原生延迟激活状态，避免 LateUpdate 在门禁后又激活 caret；对应私有布局同样必须通过实际 UGUI/IL2CPP 验证，不能用业务 enabled 回退。切层 SetParent 等同步原生回调后也复核原 Session，不把旧写回施加给池化复用的新会话。

## 资源、Tips 与控件

named/direct 解析到同一 Prefab 可共享 Single，但成功实例的租约由既有 Core backend 拥有，不依赖临时 prefab lease。若 named 并入已有 direct 会话，来源 pin 随实例会话保留；Foundation 缓存不能接管该 pin，因此结束时先 discard 实例再释放来源，不把不安全实例放回池。协调器 reset 也不提前释放它。规则、类型或原生布局失败不会污染可复用缓存。

`UITipsPresenter.Show(owner, ...)` 保留 unscaled 时间、位置、淡入淡出、句柄和普通呈现池协议；每次借用仍通过 Module.AttachView，完成/取消/owner 结束沿 View 收尾。不再编码 Overlay 类型或占业务 key；通用 Tips 默认 Layer0、Multiple、非 Modal、不覆盖、不参与 Top，完整模板引用仅覆盖 Layer4 的 UITipsOverlay Variant，Graphics 不拦截射线。

正式 `Prefabs/ColorSelector.prefab` 的窗口通过 `owner.LoadPanel<ColorSelectorWindow>(colorPrefab, ColorSelectorWindow.Arguments(initial, changed), root)` 打开；`CurrentColor` 可读，重开替换值和回调，不重放旧通知。独立 `ColorSelector.BeginSession/EndSession`、SDR RGB、Alpha 保持、色环/三角网格和自有 UI 用法不变，无 Panel 时不强制创建 owner。原来源与许可证继续在 `Runtime/THIRD-PARTY-NOTICES.md` 保留。

`UIPointer.CheckPositionOnUI/CheckMouseOnUI` 读取实际 EventSystem 的 UGUI 命中，包含真实模态遮罩；没有系统时返回 false，不创建宿主。

## 验证与迁移关口

离线复跑：`pwsh -NoProfile -File Tools/Verify-UIPanel.ps1 -Mode Pure`、`PureCore`、`Runtime`、`Editor`、`EditorTools`、`CoreEditMode`、`CorePlayMode`、`CoreEditorTests`。`Syntax` 只解析 Acceptance 的 C#9 Player/Editor 语法，不检查绑定。输出全部在当前 worktree `Temp/UI.Panel.Offline`，直接使用真实引用和产品源码。辅助编译不复现 Unity 导入、原生状态或精确包 API，不等于 Unity 编译、测试、Player 通过。

主工作区已通过 MCP 核对实际 Editor `2022.3.62f3c1`、UGUI `1.0.0`、Input System `1.14.2`、Test Framework `1.3.9`、URP/Core `14.0.12`，且 `Application.dataPath` 指向主工程。部分 manifest/lock 声明不同，未修改依赖、锁或共享 PackageCache。原离线八份 General 排除仅属于模板 API 限制；真实 Unity 已编译完整 General 源码，没有沿用排除条件。

验收顺序仍为：实际包/程序集版本 → Unity 导入及上述资产迁移/meta/实例化 → Core EditMode/PlayMode 与真实模块输入 → Windows64 IL2CPP 捕获门 → 完整 Core、Help/导出、真实 Resource 租约集成及 UIAcceptance。当前 Core EditMode/Editor 已通过，完整 Core PlayMode 实际执行后存在失败；Both 下两类模块确实运行，但输入门禁未通过，Legacy-only/New-only 尚未切换验证。连续运行、退出复位、无 Domain Reload、原游戏共存、Profiler 和 Windows64 IL2CPP 尚无本轮通过证据。历史记录与较小通过子集均不能替代这些门禁。

`UIPanelAcceptanceBuild.BuildWindows64IL2CPP()` 或菜单 `YLFramework/Core/UI Panel/构建 Windows64 IL2CPP 验收` 准备正式 UIAcceptance 场景的 IL2CPP/Medium stripping 构建，输出 `Temp/UI.Panel.IL2CPP/UIAcceptance.exe`，恢复原设置且不自动运行。检查 `UI_PANEL_NATIVE_CAPTURE_GATE_REACHED`、完整 PLAYER_PASS/QUIT_PENDING_PASS 与无相关异常，不能只看退出码或单个标记。此入口本轮未运行；Scrollbar/InputField/多指的完整引擎套件也必须执行，不能由较小 Player 门替代。

`Tests/Core/Acceptance/UIAcceptance.unity` 使用正式 Prefab。连续 Play 每轮 READY 后按原入口 PrepareExit、真正停止 Play、VerifyExit；不在两轮之间编译或重载。原 XF 游戏、Resource 8.10 实现与 Resource Workbench 不属于本重构；集成以真实共同祖先和其最终代码处理，不使用旧 Apply。
