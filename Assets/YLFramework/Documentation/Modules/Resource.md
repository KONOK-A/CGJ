# Resource（目录、加载与资源生命周期）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 正式入口

`YLFramework.Services.Resource.ResourceSystem` 是每个 Play/Player 会话唯一、显式初始化的资源权威。它统一拥有 Catalog 快照、共享 producer、资产租约、实例缓存和场景保活，不在首次读取 `Current` 或加载时自动初始化。

- Runtime：[`Runtime/Systems/Resource/`](../../Runtime/Systems/Resource/)，按 Catalog、Loading、Lifetime、Sources、Instances、Scenes、Integration 组织。
- Editor：[`Editor/Systems/Resource/`](../../Editor/Systems/Resource/)，使用同一正式目录，按 Data、Build、Publishing、Service、CodeGeneration 组织。
- 正式 namespace 保持 `YLFramework.Services.Resource` / `YLFramework.Editor.Resource`，分别归 `YLFramework.Runtime` / `YLFramework.Editor`，没有额外 Resource 产品程序集。
- 主体显式依赖 UGUI 和 Json.NET，不引用原 XF/XFAB 或可选适配器；预编译插件采用必要白名单，避免自动引入原包 DLL。

旧 `ResManager`、`ServiceManager`、`ResLoader`、`IRes`、`ResKey/ResTable/ResFactory`、旧 Bundle 配置与 BinaryFormatter 表、`GameObjectLoader/SubProject`、旧图片实现和模拟 HotUpdate 栈已经退出。没有永久转发壳、旧表运行回退或第二个初始化入口。

## 初始化与持有

应用显式准备 `ResourceConfiguration`，等待 `ResourceSystem.InitializeAsync(config)`，检查终态后通过 `TakeResult()` 领取唯一系统。Resources-only 可使用内存 `ResourceCatalog`；Bundle 项目通过已验证的正式 Catalog 和本地快照装配，不自动读取 XFAB Settings。

```csharp
using var initialized = ResourceSystem.InitializeAsync(config);
yield return initialized;
var resources = initialized.TakeResult();

using (var request = resources.AcquireAsync<TextAsset>(new ResourceAddress(projectId, "config/startup.txt")))
{
    yield return request;
    using var lease = request.TakeResult();
    ConsumeCopiedText(lease.Asset.text);
}
```

`config`、`projectId` 及 `ConsumeCopiedText` 由应用提供。`ResourceAddress` 使用稳定 ProjectId 和区分大小写的逻辑地址，可显式带子资产 key；不把 Bundle 名、磁盘路径或 XFAB 项目名猜成正式地址。

- 同一完整资源身份共享生产，每次成功取得独立 `AssetLease<T>`。同步遇在途加载给出明确 `InProgress`，不阻塞主线程、不重复生产。
- `yield return request` 仅等待，不交割所有权。拥有型结果须 `TakeResult()` 恰一次；未领取即 Dispose 会归还结果，领取后 Dispose request 不释放调用方 lease。
- 取消只移除本等待者。不可取消的 native 操作仍由 producer 收尾，最后一个等待者离开不丢失清理 owner。
- `ResourceScope` 只聚合自己的请求与租约，不维护第二张缓存表；清理失败会继续归还其余责任并聚合异常，不重放已经关闭的域。
- Resources / AssetDatabase 对象为共享 borrowed source，最后归还只删除本系统记录，不隐式 `UnloadAsset/Destroy`。动态图片仅销毁本系统创建的 Texture/Sprite；Bundle 由唯一资源图在所有根 pin 与依赖边归零后卸载。

## 实例与场景

- `Instantiate/InstantiateAsync` 返回 `InstanceLease`，内部先取得 prefab pin，再创建并登记实例；`Return/Dispose` 回缓存，`Discard` 用于失败或损坏实例。
- `GetInstanceCache` 复用 Foundation 的实例桶，不建立另一套池算法。默认 scene-scoped 缓存随所属场景退休；仍存活的外借实例晚归还时销毁，真实销毁前保持 prefab 依赖。
- `ClearInstanceCaches()` 清理本系统缓存，不调用全局池 Clear。直接 prefab 与 Attach/场景实例仍是外部借用输入，不偷偷接管资产。
- Scene 按真实 handle 与加载代次登记。`SceneLease.Dispose()` 不卸载场景，`SceneRecord` 独立 pin 资源直至真实 `sceneUnloaded`；需要卸载时显式使用对应卸载入口。
- `ShutdownAsync()` 对 live lease、instance、scene 返回可诊断 Busy，失败尝试恢复可用状态。应用先关闭业务、归还实例与 Audio 持有、清理缓存并卸载自己拥有的 Scene，再等待成功关闭；Play/应用退出走会话失效和 native 迟到排空，不新建宿主补清理。

## Core 与 Audio

- 初始化后显式 `resources.BindCoreResourceProvider()`，再启动 Module。Core `ProjectName` 表示正式 Resource ProjectId；文本交付前释放自己的 lease，命名 View/Panel 保持 fallback、初始化、失败回滚与关闭/复用语义。
- Core 直接 prefab、Attach、默认 Canvas/EventSystem 不要求 Resource 初始化。Module Close 不自动卸载外部场景。
- Audio 使用 `resources.GetAudioClipProvider(projectId)` 和 `AudioClipAddress(provider, logicalAddress)`。每次后端成功交付独立 `AssetLease<AudioClip>`，Audio 原有 `AudioClipLease`、播放缓存及尾轨负责其消费期持有，不接管 Bundle 卸载。
- 直接 Clip 仍是 borrowed。原游戏通过 XFAB 取得后传入 Clip 的路径不迁移。
- `AudioClipLibrary` 的旧枚举数字及隐藏 `BundleName` 是持久化迁移输入，不是旧资源后端。Inspector 的显式迁移要求逐条填写 ProjectId/逻辑地址，只创建新 GUID 副本，不改源资产、不推断旧 Bundle 名。详见 [Audio](Audio.md#素材组与组件)。

## Resource Workbench

1. 打开 `YLFramework/Resource/Workbench`，创建自有 `ResourceProject`。配置稳定 ProjectId、独立资源根 GUID、Group 和显式 GUID 选择；不认领原游戏资源或自动读取全局 Importer 标记。
2. 使用预检检查地址冲突、类型/子资产歧义、项目根/依赖边界与链接。选择支持 Undo，保存仅针对目标配置；dirty 配置、越界路径和未知输出会明确拒绝。
3. 构建使用显式 BuildMap、自动共享依赖分包和真实 Unity manifest 回读校验。增量 workspace 与不可变发布目录分离；不调用原 XFAB 的 MainModule Build，不写原 StreamingAssets。
4. Publisher 生成正式 `resource-catalog.json` 和 release index。Catalog Schema v1 已冻结，BuildId、CatalogDigest 和平台/Unity/App 内容兼容身份保持独立用途。当前为本地或内部受控交付，不等同生产远端安全更新。
5. Core Editor 预览使用同一 `ResourceEditorSimulation` 与已发布目录，不回退 XFAB 或旧 ResData。窗口保留取消/排队安全及发布、代码生成的独立结果报告。

增量 Workspace 为 `AssetBundles/YLR_<ProjectGuid>_<SettingsHash前32位>/ylr_<ProjectToken>_<Platform>/`；不可变发布位于 `AssetBundles/YLFramework/<ProjectId>/<BuildTarget>/` 下。两者分开验证归属，构建/发布/清理只操作自己的路径，拒绝未知文件、遗留 metadata、篡改归属记录和 symlink/junction。旧 `YLFramework/Service/ResService`、Importer 标记菜单、`AssetBundleExporter/BuildScript` 和旧子工程配置入口不再有效。

## 代码生成

- 输入来自正式已发布 Catalog，不从 Importer 标记或旧二进制表生成。
- 固定源码 `Assets/YLFrameworkData/YLResourceReferences.cs`，namespace `YLFramework.Resources`；生成 ProjectId、Addresses、Aliases、Groups 与 SubAssets 常量，不输出可变资源表或 Bundle 加载权威。
- 承载为 `YLFramework.Generated.ResourceNames`，不依赖 Framework/Unity/Editor/插件；预定义程序集自动引用，业务 asmdef 须显式引用它与 `YLFramework.Runtime`。
- 全量碰撞/类型校验先于落盘；未知文件、手写或篡改生成物、旧承载和孤立 meta 拒绝接管。相同内容幂等，更新保留既有源码/asmdef/目录 GUID，失败保留旧有效输出。
- 发布成功、生成成功和 Unity 消费者编译通过是三个结果，不能互相替代。生成物属于项目，不在默认框架包中。

## 共存与范围

`Integrations/XFAB/Audio` 保留显式可选的 borrowed 适配：独立 `YLFramework.Audio.XFAB`、`autoReferenced=false`、Package 版本约束和 `YL_AUDIO_XFAB` 编译条件，只有显式引用者参与，不自动注册且不进入默认导出。它单向依赖主体和 XFAB；主体不依赖它。原 XF/XFAB/XFFSM、原游戏、MainModule、历史 Bundle 和 Scene/Prefab 保持原链。

当前正式能力覆盖 Catalog/Builder/Publisher/Workbench、资产/子资产/组租约、实例缓存、Scene pin 与 Core/Audio 消费者。版本激活/回滚事务、生产下载签名/防重放与断点安全，以及后续 Editor 体验增强仍是独立阶段，不保留旧假服务器冒充这些能力。完整技术状态和验证范围见[阶段 8 Plan](../../../../.kilo/plans/xf-yl-8-resource.md)。
