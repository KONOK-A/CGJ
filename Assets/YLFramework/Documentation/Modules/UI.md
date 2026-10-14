# UI（统一 Panel / View）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 位置与依赖

- Panel/窗口在 [`UI/Windows/`](../../Runtime/Core/UI/Windows/)，根、输入、Tips 和适配各按职责分组，namespace `YLFramework.Core`；View/加载参数在 [`MVC/Views/`](../../Runtime/Core/MVC/Views/)，业务 Module 在 [`Modules/`](../../Runtime/Core/Modules/)。
- 正式取色控件在 [`Utilities/UI/ColorSelector/`](../../Runtime/Utilities/UI/ColorSelector/)，窗口适配在 [`UI/Windows/Tools/`](../../Runtime/Core/UI/Windows/Tools/)；旧样例已经退役，namespace与正式Prefab保持。
- 程序集为 `YLFramework.Runtime`，主体暂留 Core 的 XFABManager/UGUI 依赖。业务 asmdef 显式引用 Runtime；Foundation 与 Core 仅为源码职责分区，不再是两个运行 DLL。
- `Module`、`ViewManager`、`PanelManager` 以少量 partial 改善导航；状态字段与初始化集中在原主文件，仍是同一个类型及同一事务，不新增 Manager 或加载权威。
- `Module` 是唯一业务 owner。目录、根配置和呈现项池不代替 Module/View 的会话、请求、取消或资源归还协议。

## 窗口入口

`Module.LoadView` / `LoadPanel` 仍支持通用多实例。需要显式定义与可选唯一窗口时使用 `WindowCatalog`；每个目录只保存定义快照，不缓存活跃窗口或 pending 请求。

```csharp
var windows = new WindowCatalog();
windows.Register(new WindowDefinition("color", colorPrefab)
{
    Unique = true,
    Reusable = true,
    Modal = true,
    Layer = 3
});
var window = windows.Open<ColorSelectorWindow>(owner, "color", root.Canvas.transform,
    ColorSelectorWindow.Arguments(initialColor, OnColorChanged));
// 主动读取；关闭沿 View 退出，由统一后端按 Reusable 处理实例。
var color = window.CurrentColor;
window.Close();
```

- `owner` 必须是已有效注册的 Module。`WindowDefinition(key, prefab)` 使用直接 Prefab；`WindowDefinition(key, assetName)` 使用既有 Module project fallback / XFAB View 入口，不把旧 BundleName 映射为 ProjectName。
- 唯一范围为同 owner、同 key、同实际 Canvas 根。已加载会话再次打开时显示、借读新参数调用 `Panel.OnWindowUpdated` 并在既有层/模态/front 分组内置顶，不重复 `OnLoaded` 或事件注册。
- 同 key 在途明确拒绝，不合并独立调用方的 Abort 权限。未完成的 View 事务内不重入窗口入口；启用、更新、排序通知使原会话结束时，该 Open 不把关闭/替代会话作为成功结果。
- `OpenAsync<T>` 返回统一 `LoadPanelRequest<T>`；直接 Prefab 也进入原 `LoadViewRequest`。可 yield、检查 `Status/error` 或 `Abort`；有效现有会话可立即返回成功。入口校验/更新失败可同步抛错。
- 接管的 `LoadViewArgs` 在本次操作终态归还；只在 `OnLoaded` / `OnWindowUpdated` 中借读，不保存参数引用。
- 唯一 pending 固定接受时的实际根；等待期间改变根会 `Invalidated`，不得迁入另一个已有 key 的根重复提交。普通 View/Panel 的合法迁移继续按原协议处理。

## 根与层

- `UIRootContext` 保存 Canvas、Camera、CanvasScaler、GraphicRaycaster、层 root/mask 和 DragLayer，不拥有窗口或 Module。Canvas/Scaler 可在模板 Inspector 配置，也可使用下面的显式配置入口。
- `Prefabs/YLFramework.prefab` 已迁为根配置和 `UITipsPresenter`，保留原布局与资产身份。其公共 `YLFrameworkRoot` 仍承担非 UI 宿主职责，不再无条件制造 UI 输入对象。
- 配置模板提供层 0～4。层是窗口目录的显式选择与排序轴，在同原生 sortingLayer 内不能由 Module offset 或窗口 raw order 穿越。没有 `UIRootContext` 的普通 Canvas 只接受窗口层 0。
- 遮罩由实际活跃模态会话派生，不保存窗口计数；位于该层最上模态窗口下方，关闭后恢复。排序越界在提交前拒绝，包含遮罩所需的下方槽位。
- `Panel.IsVisible` / `IsInputBlocked`、Top / Modal / Focused 查询继续使用正式策略；Overlay 不成为普通 Top。自定义交互代码仍应遵守输入阻挡状态。

## 适配与输入

```csharp
root.ConfigureResolution(new Vector2(1920, 1080), automaticMatch: true);
// 手动 match 时，关闭自动匹配，避免两份配置相互覆盖。
root.ConfigureResolution(new Vector2(1280, 720), automaticMatch: false, manualMatch: 0.5f);
root.ConfigureCanvas(RenderMode.ScreenSpaceCamera, uiCamera, planeDistance: 5);
```

- `AutomaticAdaptationCanvasScaler` 是唯一参考分辨率与 match 配置权威；动态尺寸、配置、启用和重挂后刷新。`BackgroundAdaptation` 保持 cover，`BorderAdaptation` 只调整水平宽屏边距，不冒充 Safe Area。
- `SafeAreaAdaptation` 显式添加到内容容器才启用，背景仍可铺满；支持主显示器 Overlay/Camera。不要挂根 Canvas，不用于 WorldSpace、多显示器或非轴向容器。移动设备真实安全区与触屏尚未实测。
- Camera 由调用方提供并管理激活、cullingMask 和 clipping planes，`ConfigureCanvas` 不创建或改写 Camera。模板 Camera 默认 inactive；正式 ColorSelector/Tips 均使用 UI layer，Camera 模式必须渲染该层。
- active 外部 EventSystem 只借用，保留其模块和寿命；没有有效系统时才自建。自建 Both 优先 Input System，Legacy-only 使用 Standalone，New-only 不回退 Legacy。最后有效根退出释放自建系统，不激活/删除 inactive 外部系统；多系统或无可用模块明确诊断。
- 模态同时覆盖本会话的 Raycaster、Selectable、标准 UGUI 拖动和焦点。恢复仅针对仍有效的可导航最上窗口，不覆盖外部或新选择；旧 pointer 按压不进入复用窗口。自定义业务拖动仍须遵守 `IsInputBlocked`。
- 接口在有效 Play 执行会话内取得窗口；退出期间拒绝重新加载或挂载。真实 Play/Player Quit 会使 pending 请求提交 `Invalidated`，释放参数和退出监听，不依赖 Unity 停止 Coroutine 时执行 `finally`。

## Tips

从配置好的 `UITipsPresenter` 调用：

```csharp
var handle = tips.Show(owner, "Saved", UITipPos.RightTop, duration: 2, fade: true);
// 需要取消时：
handle.Dispose();
```

- 支持中心与四角、可选文字颜色/背景 Sprite、淡入淡出；原 `UITips.prefab` 的图文和透明度保留，右上/右下旧绑定错误已修正。
- 使用 unscaled 时间，暂停游戏仍可完成；零时长立即结束，负值/非有限时间拒绝。
- 根拥有普通 `ObjectPool<UITipsItem>`，每次显示通过 `owner.AttachView` 获得正式会话。完成、取消、Module 结束、项销毁和宿主禁用/销毁均沿 View 收尾。
- `UITipHandle.IsDone/Result` 区分完成、取消、宿主结束；完成通知前先释放会话和归还/丢弃实例。通知抛错不保留租用，也不使旧句柄控制后续复用。
- 退出进行中拒绝重启借用；收尾结束后重新启用的 Presenter 可正常显示。Tips 在配置窗口层之上的非交互 Overlay 层呈现，不占用窗口 key，也没有独立调度系统。

## ColorSelector

- 正式 Prefab：`Prefabs/ColorSelector.prefab`。业务通过 `WindowCatalog.Open<ColorSelectorWindow>(owner, key, parent, args)` 显式打开；`owner` 为已启动的 Module，`parent` 可指定外部 Canvas，未指定时沿 Core 默认根。旧演示样例已退役，不提供第二份控件实现。
- `ColorSelectorWindow.Arguments(initial, changed)` 提供初值和实时 `Action<Color>`；初始同步不触发变化通知，`CurrentColor` 可读取当前值。窗口关闭清回调，唯一窗口再次打开替换初值和回调。
- 独立控件 `ColorSelector.BeginSession(initial, callback)` / `EndSession()` 也可用于自有 UI。RGB 为 SDR `[0,1]`，非有限/HDR 初值明确拒绝；保留初始 Alpha 但不编辑它。
- 控件只读取 [`YLFramework.UI.IUIInputGate.IsInputAllowed`](../../Runtime/Utilities/UI/Input/IUIInputGate.cs)，不引用Panel/View/Module或创建新Manager/会话。正式Panel显式实现原判断 `IsLoaded && !IsInputBlocked`，仍由原模块/模态策略决定；独立控件可由自己的Unity宿主实现该predicate。
- 独立两参BeginSession按Unity最近活跃祖先规则选择IUIInputGate，组件禁用不自动排除候选，同对象候选顺序不额外保证。正式ColorSelectorWindow则在Core适配层按原GetComponentInParent<Panel>()解析最近活跃Panel，经内部入口显式注入同一控件实现；其他允许/拒绝型门禁不替换正式Panel的原加载与模态策略。
- 每个输入事件读取即时资格，阻挡丢弃当前拖拽，无宿主/已销毁缓存宿主沿原默认行为；重开才重新解析，不抛新多候选注册错误。两种组件顺序、较近其他门禁、未加载/模态拒绝及旧拖拽不复活均已由真实窗口回归及新Windows64/Mono Player验证，历史停止和修复证据见结构方案§17.11-17.12。
- 色环更新 Hue，三角更新 Saturation/Value；使用实际 RectTransform 与事件 Camera 换算，拖出三角夹到最近边界。拖动锁定原区域/指针，结束/禁用/重开不延续旧拖动。
- 色环、三角和游标由独立网格生成；使用标准 UGUI 默认材质、stencil、RectMask2D 和 CanvasGroup alpha，不修改共享材质，不携带旧 CodeArtist Shader/图片。
- 不提供 Alpha 编辑、数值/Hex、确认取消流程或 Editor 专用取色。历史来源与独立重建说明集中保留于 `Runtime/THIRD-PARTY-NOTICES.md`，原文件可从 Git 历史追溯。

## 指针与当前边界

`UIPointer.CheckPositionOnUI` / `CheckMouseOnUI` 复用实际当前 EventSystem，只统计 UGUI 命中；无 current 时返回 false，不创建宿主。模态遮罩属于真实 UI，不再按名称 `Mask` 跳过。缓存跟随 EventSystem 变化并在 Session reset 清理。

Legacy-only、Input System-only、Both 已在 3.3 分别重启 Unity 验证。3.4 已完成正式模板、真实无 Domain Reload/正常 Scene Reload 连续双 Play，以及 Windows64/Mono Player 的工具输入、渲染、复用和原游戏非干扰。自动化虚拟设备经过真实输入模块，不等于物理 OS 鼠标/键盘人工验收；IL2CPP、移动设备及完全退出原 XF 生态的独立运行仍待后续。

源码仓库的 `Tests/Core/Acceptance/UIAcceptance.unity` 显式绑定正式 Prefab，可用于定向 Play/Player 验收。默认模式验证统一 UI；验收 Player 的 `-ui34-game` 模式加载原 `Main` 场景及现有 MainModule 产物，调用真实持久按钮/Slider 事件。该程序集和场景不在默认框架导出根内；不把它们加入正式游戏 Build Settings。连续 Play 的原 XF Logger 干扰只在用户批准后临时隔离，不表示已修复原 Logger。

验收入口不是通用游戏驱动：原游戏模式要求没有既有 MainModule 或 GameState FSM，前置拒绝时不清理他人状态。默认 Player 模式必须同时核验 exit 0、真实退出断言后的 `PLAYER_PASS` / `QUIT_PENDING_PASS` 和无相关异常，不能只凭退出码判定通过。Editor 每轮 READY 后暂停，再调用该入口的 `PrepareExit()`、真正停止 Play 并调用 `VerifyExit()`；每轮退出拒绝标记独立重置，两轮之间不编译或重载。

旧 `UIManager`、`UIRoot`、`UIWindowBase`、窗口 Attribute/Extension、专用 Loader 池和空 `SceneTipsItem` 已退出，没有永久兼容壳。原 XF/XFAB Package 与原游戏资源链不在此次迁移范围。
