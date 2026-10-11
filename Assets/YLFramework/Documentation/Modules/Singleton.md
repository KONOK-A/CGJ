# Singleton（单例）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供多种单例形态：普通 C# 单例、MonoBehaviour 单例（跨场景保留 / 可替换）、以及从 Prefab 或 ScriptableObject 资产创建的单例访问属性，覆盖纯逻辑对象与 Unity 对象两类场景。

## 位置

- 运行时：[`Runtime/Foundation/Singleton/`](../../Runtime/Foundation/Singleton/)
  - `Plain/`：普通 C# 单例与访问属性（`Singleton<T>`、`SingletonProperty<T>`）
  - `Mono/`：MonoBehaviour 单例与访问属性、路径特性（`MonoSingleton<T>`、`MonoSingletonProperty<T>`、`MonoSingletonPathAttribute`、`PersistentMonoSingleton<T>`、`ReplaceableMonoSingleton<T>`）
  - `Asset/`：Prefab / ScriptableObject 单例访问属性（`PrefabSingletonProperty<T>`、`ScriptableSingletonProperty<T>`）
  - `Internal/`：`SingletonCreator` 内部创建逻辑与 `SingletonSessionState` 会话状态重置

## 主要功能

- 普通单例 `Singleton<T>`：实现 `ISingleton` 的 C# 单例基类；`Instance` 懒创建，`OnSingletonInit` 只在新实例创建时调用一次。
- MonoBehaviour 单例 `MonoSingleton<T>`：支持 DontDestroyOnLoad 与运行时层级路径（`MonoSingletonPathAttribute`）。
- `PersistentMonoSingleton<T>`：场景中重复时保留先创建者；`ReplaceableMonoSingleton<T>`：保留后创建者。
- 懒加载访问属性：`SingletonProperty<T>`、`MonoSingletonProperty<T>`；`Singleton<T>` / `MonoSingleton<T>` 的 `Instance` 分别委托给它们，保证同一个 T 只有一个实例缓存（此前两套静态缓存可能各自持有实例）。
- 资产单例：`PrefabSingletonProperty<T>`（默认按类型名从 Resources 加载预制体）、`ScriptableSingletonProperty<T>`（默认按类型名从 Resources 加载资产），均可通过 `InstanceWithLoader` 替换加载方式。
- 生命周期回调：`ISingleton.OnSingletonInit()`；访问属性提供 `Dispose()` / `Dispose(bool isDestroy)` 释放实例，下次访问重新创建。

## 便利 API

- `HasInstance`：当前是否已持有实例（不会触发查找 / 创建 / 加载）。
- `TryGetInstance(out T instance)`：尝试获取已持有的实例（不会触发查找 / 创建 / 加载）。
- 上述两个 API 在 `Singleton<T>`、`MonoSingleton<T>`、`SingletonProperty<T>`、`MonoSingletonProperty<T>`、`PrefabSingletonProperty<T>`、`ScriptableSingletonProperty<T>`、`PersistentMonoSingleton<T>`、`ReplaceableMonoSingleton<T>` 上命名与语义一致。

## 生命周期与 duplicate 策略

- `MonoSingleton<T>`：重复实例不会被自动销毁。`Instance` 会输出一次警告，并稳定地固定采用其中一个实例；如需保留先创建实例请用 `PersistentMonoSingleton`，如需后创建替换请用 `ReplaceableMonoSingleton`。实例销毁时 `OnDestroy` 只清理自己持有的缓存，不会误清新实例；`Dispose()` 会立即清空缓存，同一帧再次访问可重建；绕过 `Dispose` 的已销毁对象会在同帧查找时被跳过。
- `MonoSingletonProperty<T>.Dispose(false)`：只释放缓存，不销毁实例；下次访问会重新认领同一对象并再次执行 `OnSingletonInit`（历史行为，已被测试锁定）。
- 退出保护：`OnApplicationQuit` 与 `Application.quitting` 会标记退出状态，退出过程中 `Instance` 不再创建新的 MonoBehaviour / Prefab 单例；标记在下一次进入 Play 时自动重置。
- `PersistentMonoSingleton<T>`：保留先创建者，后创建者在 `Awake` 中自杀；被 `Instance` 提前认领的实例在 `Awake` 时仍会 DontDestroyOnLoad。
- `ReplaceableMonoSingleton<T>`：保留后创建者；替换在 `Awake` 中同步完成，`Instance` 立即切换到新实例；同一帧内多个实例通过内部序号决胜，不再依赖 `Time.time`（此前同帧创建会全部存活）。
- `ScriptableSingletonProperty<T>.Dispose()`：仅当资产由默认 Resources 加载方式载入时才调用 `Resources.UnloadAsset`；自定义 loader 载入的资产由调用方自行管理。

## Domain Reload / Play Session

- 进入 Play 时（`RuntimeInitializeLoadType.SubsystemRegistration`）重置所有属于单次 Play Session 的静态状态，使 Domain Reload disabled 与 enabled 行为一致：
  - 各 Singleton 实例缓存（`SingletonProperty` / `MonoSingletonProperty` / Persistent / Replaceable / Prefab / Scriptable）；
  - `MonoSingletonProperty._disposedInstance`；
  - `ReplaceableMonoSingleton` 创建序号；
  - Prefab / Scriptable 的一次性警告标志与 loader（恢复为默认 Resources 加载方式）；
  - 退出标记 `IsApplicationQuitting`。
- `InstanceWithLoader` 属于单次 Play Session 的运行期配置：每个 Play Session 需重新调用。这是与 Domain Reload enabled 行为对齐的有意设计。
- 静态重置注册表（`SingletonSessionState.SessionResets`）属于进程级状态，不随 Play Session 清空。

## 依赖

- 仅 UnityEngine 与同程序集内的 Utilities 扩展，无框架内模块依赖。

## 当前状态

已完成。测试：`Tests/EditMode/SingletonTests.cs`、`Tests/EditMode/SingletonPropertyExtraTests.cs`、`Tests/EditMode/ScriptableDefaultLoaderTests.cs`、`Tests/EditMode/MonoSingletonPathCompatibilityTests.cs`、`Tests/PlayMode/MonoSingletonTests.cs`、`Tests/PlayMode/MonoSingletonPathTests.cs`、`Tests/PlayMode/ReplaceableMonoSingletonTests.cs`、`Tests/PlayMode/PersistentMonoSingletonTests.cs`、`Tests/PlayMode/PrefabSingletonPropertyTests.cs`。

## 备注

- 层级路径会决定单例对象的场景位置，例如 `YLFramework/Pool/GameObjectPool`、`YLFramework/ResService/ServiceManager`；修改路径会影响已有场景中的对象查找。存续单例路径由 `MonoSingletonPathCompatibilityTests` 锁定；旧 UI 单例已退出，正式 `UIRootContext` 不采用单例路径。
- 路径层级已存在时按原样复用（不会把已存在的父节点改为 DontDestroyOnLoad）；路径为空时回退为按类型名创建独立对象并 DontDestroyOnLoad。
- `InstanceWithLoader` 替换的是静态 loader，作用于后续创建；已有实例不会被替换；loader 属于单次 Play Session 配置，进入 Play 会恢复默认。
- Prefab / Scriptable 加载失败（loader 返回 null 或找不到资源）返回 null 并输出一次警告；loader 抛出的异常不会被吞掉，替换 loader 后可重试；loader 内重入 `Instance` 不会递归创建。
- Prefab 缺少目标组件时会销毁已实例化的克隆，不留下半初始化对象。
- `ScriptableSingletonProperty.Dispose()` 只卸载自身通过默认 Resources 加载的资产（ownership 明确）；自定义 loader 的资产由调用方释放。
