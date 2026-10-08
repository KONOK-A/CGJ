# UI（窗口与界面管理）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供 UI 窗口的注册、打开 / 异步打开 / 关闭 / 缓存管理，UI 层级与遮罩控制，提示气泡显示，以及屏幕分辨率与点击检测等常用能力。

## 位置

- 运行时：[`Runtime/UI/`](../../Runtime/UI/)
  - 提示气泡：[`Tip/`](../../Runtime/UI/Tip/)

## 主要功能

- `UIManager`（`MonoSingleton`）：
  - 窗口数据注册与查询：`AddUIWindowData` / `GetUIWindowData` / `TryGetUIWindowData` / `RemoveUIWindowData` / `ClearAllUIWindowData`。
  - 打开与关闭：`Open<T>` / `Open(type/key)`、`ShowAsync`（异步）、`GetUIWindow`、`DestroyUIWindow`、`Close` / `TryClose` / `CloseAll`。
  - 提示气泡：`ShowTips(content, UITipPos, ...)`，提示对象使用对象池（`SimpleObjectPool<UITipsItem>`）。
  - 层级与遮罩：`UILayer[]`（含遮罩显隐与层级计数）、`DragLayer`。
  - 屏幕：`SetResolution` / `GetResolution` / `ScreenSpaceOverlayRenderMode` / `ScreenSpaceCameraRenderMode` / `CheckMouseOnUI` / `CheckPositionOnUI`。
- `UIWindowBase`：窗口生命周期（`OnInit` / `OnOpen` / `OnClose` / `BeforeDestroy` / `RegisterUIEvent` / `UnRegisterUIEvent`），持有窗口数据与加载器。
- `UISystemConfig`：窗口加载配置，可替换 `IWindowLoaderPool`；默认实现 `WindowLoaderPoolBaseOnRes` 基于 `ResLoader` 同步 / 异步加载窗口预制体。
- 窗口数据：`UIWindowData`、`UIWindowDataAttribute`（声明窗口 Key / 缓存 / 资源名 / 层级）、`WindowOpenType`。
- `UIRoot`：UI 根节点单例（层级路径 `YLFramework/UISystem`）。
- 提示：`UITipsItem`（显示 / 重置 / 淡出）、`SceneTipsItem`、`UITipPos`（显示位置枚举）。
- 扩展：`UIWindowExtension.OpenAsUI` / `CloseAsUI` / `TryCloseAsUI`。

## 主要类型

- `UIManager` / `UIManager.UILayer`：窗口与层级管理。
- `UIWindowBase`：窗口基类。
- `UIWindowData` / `UIWindowDataAttribute` / `WindowOpenType`：窗口数据。
- `UISystemConfig` / `IWindowLoader` / `IWindowLoaderPool` / `WindowLoaderPoolBase` / `WindowLoaderPoolBaseOnRes`：加载配置。
- `UIRoot`：UI 根节点。
- `UITipsItem` / `SceneTipsItem` / `UITipPos`：提示气泡。

## 依赖

- `Resource`：默认窗口加载器通过 `ResLoader` 加载预制体。
- `Singleton`、`Pool`（提示对象池）、`Extensions`；Inspector 字段标注使用 Sirenix Odin。
- 提示预制体由 `Prefabs/UITips.prefab` 提供（在 `UIManager` 上引用）。

## 当前状态

部分功能待完善：

- 窗口注册依赖手动调用 `AddUIWindowData`；`UIWindowDataDict` 在框架内没有自动初始化，`UIWindowDataAttribute` 也暂无读取它的代码（自动注册机制未完成）。
- 源码 TODO：窗口销毁时卸载对应资源（`UIManager`）、提示项“显现”逻辑（`UITipsItem`）。
- 无专门测试；示例见 `Samples/ColorSelector/`。

## 备注

- 窗口的 assetName / bundleName 由窗口数据指定，最终经资源模块加载；层级由 `UIWindowData.LayerNum` 决定。
- `YLFramework.prefab` 承载 `UIManager` 与 UI 根相关对象。
- EventSystem 的输入模块由运行时自动补充（优先 InputSystem，回退 Standalone），使用方无需在 prefab 预置特定输入模块。
