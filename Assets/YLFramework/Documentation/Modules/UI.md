# UI（统一 Panel / View）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 位置与依赖

- 窗口、根配置、Tips：[`Runtime/XFMain/UI/`](../../Runtime/XFMain/UI/)，namespace `YLFramework.Core`。
- 正式取色工具：[`UI/Tools/`](../../Runtime/XFMain/UI/Tools/)。Samples 只包含消费者，不再实现取色。
- 程序集为 `YLFramework.Core.Runtime`，依赖 Foundation、XFABManager、UGUI。业务 asmdef 显式引用 Core；使用异步请求等 Foundation 类型时也引用 Foundation。
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

- `UIRootContext` 只保存 Canvas、Camera、CanvasScaler、GraphicRaycaster、层 root/mask 和 DragLayer。Canvas/Scaler 的设置可在模板 Inspector 配置；完整统一 Adaptation 与输入接入另行收口。
- `Prefabs/YLFramework.prefab` 已迁为根配置和 `UITipsPresenter`，保留原布局与资产身份。其公共 `YLFrameworkRoot` 仍承担非 UI 宿主职责，不再无条件制造 UI 输入对象。
- 配置模板提供层 0～4。层是窗口目录的显式选择与排序轴，在同原生 sortingLayer 内不能由 Module offset 或窗口 raw order 穿越。没有 `UIRootContext` 的普通 Canvas 只接受窗口层 0。
- 遮罩由实际活跃模态会话派生，不保存窗口计数；位于该层最上模态窗口下方，关闭后恢复。排序越界在提交前拒绝，包含遮罩所需的下方槽位。
- `Panel.IsVisible` / `IsInputBlocked`、Top / Modal / Focused 查询继续使用正式策略；Overlay 不成为普通 Top。自定义交互代码仍应遵守输入阻挡状态。

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

- 正式 Prefab：`Prefabs/ColorSelector.prefab`；已绑定的演示：`Samples/ColorSelector/ColorSelectorSample.prefab`。演示自行拥有一个 Module；可选 `uiRoot` 指定外部 Canvas，未绑定时沿 Core 默认根。可选 preview Renderer 只使用演示自建材质实例并在宿主结束时释放。
- `ColorSelectorWindow.Arguments(initial, changed)` 提供初值和实时 `Action<Color>`；初始同步不触发变化通知，`CurrentColor` 可读取当前值。窗口关闭清回调，唯一窗口再次打开替换初值和回调。
- 独立控件 `ColorSelector.BeginSession(initial, callback)` / `EndSession()` 也可用于自有 UI。RGB 为 SDR `[0,1]`，非有限/HDR 初值明确拒绝；保留初始 Alpha 但不编辑它。
- 色环更新 Hue，三角更新 Saturation/Value；使用实际 RectTransform 与事件 Camera 换算，拖出三角夹到最近边界。拖动锁定原区域/指针，结束/禁用/重开不延续旧拖动。
- 色环、三角和游标由独立网格生成；使用标准 UGUI 默认材质、stencil、RectMask2D 和 CanvasGroup alpha，不修改共享材质，不携带旧 CodeArtist Shader/图片。
- 不提供 Alpha 编辑、数值/Hex、确认取消流程或 Editor 专用取色。来源处理详见 `Runtime/THIRD-PARTY-NOTICES.md` 和 Sample 的历史来源说明。

## 指针与当前边界

`UIPointer.CheckPositionOnUI` / `CheckMouseOnUI` 复用实际当前 EventSystem，只统计 UGUI 命中；无 current 时返回 false，不创建宿主。模态遮罩属于真实 UI，不再按名称 `Mask` 跳过。缓存跟随 EventSystem 变化并在 Session reset 清理。

当前 Core 默认创建路径仍沿用既有 EventSystem 机制；本批不宣称已经完成 New-only / Legacy / Both 的自动选择、停用实例补齐、多根相机统一、输入桥隔离或 Adaptation。使用方应让有效 EventSystem 配置匹配实际输入后端。完整输入与 Adaptation 属于 3.3，综合 Player/无 Domain Reload 门禁属于 3.4。

旧 `UIManager`、`UIRoot`、`UIWindowBase`、窗口 Attribute/Extension、专用 Loader 池和空 `SceneTipsItem` 已退出，没有永久兼容壳。原 XF/XFAB Package 与原游戏资源链不在此次迁移范围。
