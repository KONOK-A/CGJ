# ActionKit 与 AudioKit 完整提取计划

## 1. 不可缩减的目标与当前状态

**完整保留当前本地 ActionKit 和 AudioKit 提供的全部模块能力，解除其对原 QFramework、CoreKit 和其它 QFramework Toolkit 的依赖，形成两个项目维护、各自独立接入和复用的模块。** 当前游戏调用量不作为裁剪依据。Deprecated/旧 API、编辑器、资源、导出、诊断、示例、说明及目录中的公开辅助能力均有去向。

2026-10-03 09:34 UTC用户调整范围：新ActionKit不需要完整样例。不再迁移20组主样例及其场景/专用图片，也不迁移DOTween/UniRx演示脚本和场景；保留动作/FSM/Ease等全部功能、可选扩展实现、API Help简短用法、回归测试和ScreenTransition等必要运行资源。旧样例/归档不删除，AudioKit及双模块联动样例范围不变；本文涉及Action完整Samples的旧要求由此覆盖。

本计划取代任何“只提取当前业务所用子集”的提取设想。其余业务 QF 使用仍改用现有 XF 或最小项目实现，不额外整体提取其它 Toolkit。本轮只做盘点和设计，不进行复制、移动、修改源码/资源、创建程序集、安装包、迁移或框架删除。

配套验收基线：[02-feature-matrix.md](02-feature-matrix.md)。原项目使用审计：[01-audit.md](01-audit.md)，仅用来约束业务回归，**不是完整模块功能说明书**。本轮开始没有已有 02 文件；新增本计划和功能矩阵，不修改 01。

所有新目录、程序集、类型名和方法入口都是**拟定设计**，不是已存在实现。当前状态：已盘点、已设计；未实现、未编译、未运行验证。原问题的修正建议另列，不能用“已经有映射”代替功能通过。

## 2. 本地基线与范围

| 范围 | 当前证据 | 覆盖说明 |
|---|---|---|
| Unity | ProjectVersion.txt:1-2，2022.3.62f3c1 | 不升级 Unity |
| 测试设施 | Packages/manifest.json，com.unity.test-framework 1.3.9 | 后续复用，不在本轮执行 |
| ActionKit | `Assets/QFramework/Toolkits/_CoreKit/ActionKit/` | 全部 26 个 C#，4702 行，包括 1783 行 DeprecateActionKit；屏幕过渡 Prefab 和单文件配置 |
| AudioKit | `Assets/QFramework/Toolkits/AudioKit/` | 全部 54 个 C#，3996 行，34 个 public 类型/接口/枚举；两套 Timer、Heap、CustomProperty、Disposable 同样保留 |
| Action 示例压缩档 | ActionKit/Example.unitypackage | 64 个 pathname，20 个 C#、20 个场景、Triangle.png、2 个嵌套扩展包；在内存读取，不导入或落盘解包 |
| Action 嵌套扩展 | 10.DOTween、11.UniRx 的 unitypackage | 分别 2 个 C# + 1 场景；UniRx 另有 asmdef；扩展实现和全部公开方法已读取 |
| Audio 示例压缩档 | AudioKit/Example.unitypackage | 69 个 pathname，28 个 C#、16 个场景、6 个音频；全部脚本在内存读取 |
| 目录外提供者 | SupportOldQF Audio loader 桥、PackageKit/PackageMaker/SingleFileCreator、ConsoleKit、APIDoc、必要 Framework/Pool/Singleton/Bindable/Fluent/Table 定义 | 只追踪两模块实际需要的能力，不搬入这些 Toolkit |
| 本地 XF | XFGameFramework 1.18.8，`@2182708aec`；XFABManager 1.24.0，`@c35dc4058e` | 定向读取当前两个 PackageCache；GF Runtime 引用 XFABManager，不引用 QF |
| UI 资源基础 | manifest.json:11与lock:278-284声明com.unity.ugui 2.0.0；当前定向可读的PackageCache/com.unity.ugui@1.0.0/package.json:4为1.0.0 | 所读Runtime/UnityEngine.UI.asmdef:2确认程序集名UnityEngine.UI；Editor实际解析/导入版本需B0核实，不在本轮刷新或修复UPM |

没有查询或混入网上新版功能。目录枚举、实际类型、引用、方法重载、资源 GUID、条件编译及辅助工具提供者分开核对。没有以注释、历史错误日志或根目录生成的 csproj 当作模块功能实现证据。

特别澄清：Audio 的 `Scripts/QFramework.BindableProperty.cs` 定义 `CustomProperty<T>`/`OnPropertyChangedEvent<T>`，不是 Framework 中的 `BindableProperty<T>`。二者通知顺序和接口不同，不合并成一个简化 float/事件。

## 3. 两个独立模块的结构

未来源码根目录拟为 `Assets/ProjectModules/`，名称不绑定当前游戏。以下只是设计，不在本轮创建：

```text
Assets/ProjectModules/
  ActionKit/
    Runtime/
      API/                  ActionKit 门面与公开契约
      Actions/              现代动作、组合、Controller、Executor、Queue
      Compatibility/        原旧动作、链式、FSM、QFSMLite、Timeline
      ScreenTransition/     完整 FadeIn/FadeOut/FadeInOut 与组件
      ShortCut/             Unity AudioSource.PlayAction 等
      Utility/              EaseUtility 的完整算法与枚举
      Internal/             仅动作需要的 factory 池、订阅、Unity host
    Resources/ProjectActionKit/ScreenTransitionCanvas.prefab
    Editor/                 单文件导出、配置 Inspector、帮助浏览
    Integrations/DOTween/   可选提供者，不成为基础依赖
    Integrations/Observable/ 原 UniRxAction 能力的可选扩展
    Tests/EditMode/、Tests/PlayMode/
    Documentation/、Licenses/
  AudioKit/
    Runtime/
      API/、Players/、Channels/、Loading/、Settings/
      Diagnostics/          数据与可打开的运行时音频诊断 GUI
      Compatibility/        公开 Heap、两套 Timer、Disposable、CustomProperty
      Internal/             仅音频需要的 host、池、订阅、播放 generation
    Backends/XFAB/          完整 XFAB loader、地址配置、缓存/lease/错误处理
    Editor/                 独立诊断窗口、帮助与 backend 配置 Inspector
    Samples/                16 组示例、全部场景/6 个音频；联动样例分离
    Tests/EditMode/、Tests/PlayMode/
    Documentation/、Licenses/
  Integrations/ActionKitAudioKit/
    Runtime/                PlaySoundAction 与 ISequence.PlaySound
    Samples/、Tests/
```

### 3.1 命名空间和程序集

| 交付层 | 拟定 namespace / asmdef | 必须依赖 | 明确禁止的依赖 |
|---|---|---|---|
| Action 基础 Runtime | Project.ActionKit / Project.ActionKit.Runtime | Unity、UnityEngine.UI | AudioKit、MainModule、原 QF/CoreKit/任何 Toolkit |
| Action Editor | Project.ActionKit.Editor | 自己的 Runtime、UnityEditor | PackageKit、CodeGenKit、ConsoleKit |
| Audio 基础 Runtime | Project.AudioKit / Project.AudioKit.Runtime | Unity 音频/Resources/UnityEvent/IMGUI/标准 C# | ActionKit、业务、原 QF/CoreKit/ResKit |
| Audio XFAB backend | Project.AudioKit.Backends.XFAB / Project.AudioKit.XFAB | Audio Runtime、XFABManager | ActionKit、业务常量、QF |
| Audio Editor | Project.AudioKit.Editor | 自己的 Runtime，backend Inspector 才引用 XFAB backend | 旧文档/GUI框架 |
| 音频动作联动 | Project.ActionKitAudioKit / Project.ActionKitAudioKit.Integration | 两个新 Runtime | 任一 Runtime 反向引用 Integration |
| 可选 DOTween | Project.ActionKit.Integrations / 独立可选交付程序集 | Action Runtime、实际提供 DG.Tweening 的程序集 | Audio/QF；不在未安装 DOTween 的基础层放未解析引用 |
| 原 UniRx 扩展 | Project.ActionKit.Integrations | Action Runtime、BCL IObservable<T>/IDisposable；UniRx 演示才需 UniRx | 原 QF/CoreKit；不要求基础 Runtime 安装 UniRx |
| Tests / Samples | 各模块单独程序集 | 只引用对应交付层；测试再引用现有 Test Framework | 不隐式靠默认 Assembly-CSharp 中的游戏或旧框架编译 |

基础层选择模块自己的薄 Unity host，原因是它们已有组件/场景/消息语义，XF 三相 Update 不覆盖 GUI/Focus/Pause/Quit、销毁恢复和所有原执行顺序。**不同时驱动 XF 和自己的 Update**。后端使用真实 XFAB API；将来确需在 XF 应用中显式初始化时，可由通用配置接入调用模块入口，不要求创建 XF MVC Module 或游戏 Controller。

两个模块不创建共享 Common/Core，不复制 Architecture/IOC 服务系统。需要公开的注销/绑定/池接口归属各模块自己的 namespace；不能各自导出同名 QFramework.IUnRegister 等以造成程序集歧义。普通内部 helpers 不成为“完整 Toolkit”。

### 3.2 公开契约映射原则

- 模块门面、动作/播放器名字、方法名、参数名、默认值、逐个重载、泛型约束、属性/事件操作、链式组合、旧拼写均按矩阵保留。命名空间和程序集可以改变，不声称源码或二进制零差异。
- Action 的现代/旧 AbstractAction、ActionStatus、IActionController 等继续是本模块的动作概念；AudioPlayer/AbstractAudioPlayer/MusicPlayer 仍是音频对象，不换成 XF AudioPlayer。
- 原 Audio 的公开 Timer/TimeExtend.Timer、Heap、CustomProperty、Disposable 继续在 Audio 所属 namespace 可用，不因属于支撑而隐藏或移出交付范围。
- `QFramework.BindableProperty<float>` 等迁到 `Project.AudioKit.BindableProperty<float>`，保留 readonly/writeable 协议、比较、初值通知、无事件设置、token 注销和生命周期绑定。每 T 静态 Comparer 不能静默改成每实例。
- `IPoolable/IPoolType` 等被模块类型实现的必要契约换成模块所属接口，保留可继续调用的回收方法和泛型能力，不强加 XF 的 class,new()。
- 原 ActionKit 公开继承 Architecture；Settings/Config 继承 AbstractModel。解除这种底层继承是必要的契约变化。生命周期映射为 Initialize/EnsureInitialized/Shutdown、Initialized 和 init/deinit 钩子；原 InitArchitecture/Deinit 可提供清楚的生命周期兼容入口。泛用 RegisterSystem/Model/Utility、Get/SendCommand/Query/Event、IArchitecture/IModel cast 不伪造等价接口，不重建容器。AK-042/AU-034 明列继承公共面及兼容破口，实施前核查其消费者；动作和音频能力仍完整保留。
- 直接跨程序集继承 AbstractAudioPlayer 原来受 internal abstract CanPlayAudio 限制；不得宣传它原本任意可扩展。保留实际可派生的 AudioPlayer/MusicPlayer、LoaderPool 与保护钩子。是否开放该基类约束单列，不偷偷变更。
- 示例中的 ViewController/IBindGroup、生成器元数据、Fluent 日志/Transform/IMGUI 调用改成实际可运行的 MonoBehaviour、Unity UI、Unity/BCL 调用；不将 CodeGenKit 作为样例前提。

必要底层公开面采用具体迁移，而不是一句“移除继承”结束：

| 原入口 | 拟定对应入口 | 兼容边界 |
|---|---|---|
| ActionKit.InitArchitecture() | ActionKit.Initialize()，原名字可作为明确同职责的生命周期兼容入口 | 没有通用IOC注册行为；动作/host/pool初始化完整 |
| ActionKit.Interface / OnRegisterPatch | Interface返回新的ActionKit局部上下文对象（保留public class及其构造概念）；Action<ActionKit>补丁在本模块初始化阶段调用 | 返回不再是IArchitecture；补丁只能配置模块自己的能力，原RegisterSystem/GetModel等代码必须迁移 |
| ActionKit实例Deinit、protected Init/OnDeinit | Shutdown及模块初始化/关闭钩子；Deinit兼容转发Shutdown | 回调和所有权明确；不保留底层Command/Query执行空壳 |
| Audio公开模型(ICanInit).Init、Initialized、Deinit/OnInit/OnDeinit | 模块所属IAudioModelLifecycle.Init/Deinit、原Initialized成员及保护钩子 | 不实现IModel/IArchitecture；原GetUtility/SendEvent消费者改显式依赖/对应事件入口 |
| 原架构事件、EnumStateMachine typed事件、生命周期EasyEvent | 分别明确模块上下文事件scope、ActionStateEvents和七个消息事件；实际Register/UnRegister/Send/Trigger可用 | 不默默把原架构scope、原global typed bus、七消息合并成一个全局总线 |
| FSM TypeIndex/Table | 仅FSM域的TransitionTypeIndex及领域表，保留Dictionary/Get/Add/Remove/Clear/枚举/Dispose与派生钩子 | 返回类型改动明确，不携带完整TableKit |

通用System/Model/Utility/Command/Query互操作无本地XF一对一等价，且不是动作算法；不通过新建容器伪造兼容。其存在已列入原公开表面和B0消费者/契约检查，不能宣称“没有API差异”。这与保留全部模块功能分别验收。

## 4. 全部底层依赖的替代决策

路径缩写：`GF/` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，`AB/` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`，`Q/` = `Assets/QFramework/`。所有候选都是当前本地定义，不使用虚构的 PoolManager、InitializeAsync、Retain/Release 等 API。

| ID | 原依赖/职责 | 本地 XF 候选、定义和程序集 | 等价判断 | 最终替代与补齐 | 验证 |
|---|---|---|---|---|---|
| D-01 | Architecture、IOC、AbstractModel/System/Command：内部组织与初始化 | GF Runtime/Module/Module.cs；ModuleManager.cs:229-234；SingletonModule.cs:9-18，XFGameFramework | 不等价，附带业务模块、场景、MVC生命周期 | 各模块持有自己的明确字段/对象，不建通用注册容器；拆内部命令分派但保留操作算法；公开继承兼容面另列 | 冷启动/关闭/再启动、派生钩子、无 MVC 环境、旧 cast 编译映射 |
| D-02 | Unity 消息、全局/对象/场景宿主、帧推进 | GF Runtime/Manager/UpdateManager.cs:84/96/108 Add，120/132/144 Remove | 仅三相部分等价；无 GUI/Focus/Pause/Quit，禁用停全部，销毁丢订阅，无去重/异常隔离 | 基础层保留模块薄 host 和原 executor 阶段；只替单例创建/注销，保留时间源和组合推进 | 同帧注册/回调顺序、相位、对象停用/销毁、场景卸载、重复 Play |
| D-03 | 协程执行与取消 | AB Runtime/Tools/CoroutineStarter.cs:43 Start(IEnumerator)、53 Stop(Coroutine)，XFABManager | 基本能力适合，但无局部/场景归属、失败/取消结果 | 基础层用自身 host 的 Unity Coroutine；保留原 ToAction/IEnumerator/Task 方式及取消句柄；需要 XF 接入只做明确所有权适配 | 嵌套 IEnumerator、等待、停止、不把取消当完成；Task 完成/异常/销毁 |
| D-04 | Delay/Repeat、Heap Timer、TimeExtend.Timer 的时间和顺序 | GF TimerManager.cs:343-1155；TimeExtensions.cs:15-23 | 不等价：默认 unscaled、长帧>0.2被改0.02、n+1次帧判断、倒序执行、无Pause/Resume | 保留模块已有全部算法，Unity Time/DateTime作为原时钟；不做 TimerManager 简单包装 | scaled/unscaled、长帧、0/负值、次数/同帧顺序/暂停/切时钟 |
| D-05 | Simple/Safe factory pool、List/Dictionary pool、私有构造、清理/容量 | GF Runtime/Manager/ClassObjectPoolManager.cs:134/159/188，XFGameFramework | 部分适合但 new()、无 factory/max/clear/init/count；首次泛型 OnLoaded 对象取错 | 每模块最小 factory 池/容器复用；动作核心和音频复用规则保留；必要公开池契约显式迁移，不搬 PoolKit | 首次初始化、私有factory、预热、max、重复回收、回调/资源清理、旧 raw 句柄 |
| D-06 | EasyEvent、IUnRegister、destroy/disable/scene注销、旧 FSM 类型事件 | GF Runtime/Manager/EventManager.cs:199-545；mvc/View.cs:87-95,207-237 | key-based、多delegate类型、返回void；View生命周期非通用等价 | 必要模块所属多播/注销句柄和小型触发器；旧 FSM 用本地类型索引，不依赖全局字符串bus/业务View | token/重复订阅/注销、回调重入、destroy/disable/任一sceneUnloaded |
| D-07 | Bindable/PlayerPrefs、自定义总开关 | GF AudioController.cs:83-110 有Volume/Mute及Action通知 | 不等价；键不同，无完整接口/初值/token/comparer/without-event | Audio 内最小完整属性实现及原6键；CustomProperty/UnityEvent不合并；无更换XF设置键 | 键与默认值、通知先后、运行中音量、Comparer、总开关、无事件赋值 |
| D-08 | Mono/Prefab singleton、音频Listener、屏幕过渡实例 | GF Update/Timer singleton、AB CoroutineStarter | 生命周期/搜索/资源路径不等价 | 模块owned Unity对象，使用Unity存活判断；保留已有Listener检查、DDOL与局部场景的差别；移除反射单例依赖 | 无/已有Listener、对象数、资源getter、host销毁、重复Play、Quit后不重建 |
| D-09 | ActionKit.Condition.StartGlobal：音频完成监听 | XF AudioPlayer.Play返回void，AudioController.AudioData按playTime-clip.length>1判断 | 不等价：不跟随pitch/外停/isPlaying，且无完整player返回契约 | AudioManager薄Update监听集合；首次检查下一Update、Paused屏蔽、generation/取消、保持正常完成顺序 | 自然完成、暂停、loop、pitch、Stop、回收重入、timeScale0、source销毁 |
| D-10 | IAudioLoader、ResKitAudioLoaderPool：同步/异步名字加载 | AB AssetBundleManager.cs:831 LoadAsset<T>、853 Type；909泛型Async、913 Type Async；ReadyRes:263 | 部分等价；无bundleName参数，Type/取消/所有权不同 | Resources完整基础后端；随Audio交付XFAB Type Async后端、catalog地址、typed cache/lease/取消后收尾；第5节 | 全重载、自定义loader、替池、配置失败、真async、pending取消、释放 |
| D-11 | ResLoader 引用计数、缓存/释放 | AB AddAssetCache:1540-1560、UnloadAsset:1585/1633-1644；Unload(true):660 | 不等价：不是每次Load refcount；缺少公共保活/pending支持 | owned独占project级broker、borrowed绝不卸载、shared明确retained；不调用UnLoadAll当模块关闭 | 同clip多消费者、同bundle并发、最后lease/projectPending、共享XF不受损 |
| D-12 | TableKit公开TypeIndex与旧FSM transitions索引 | XF当前FSM是另一套算法，不提供所需返回索引的等价契约 | 不等价 | 保留Action旧FSM算法；模块域内Transition表/类型桶，明确公开TypeIndex新类型与操作映射，不搬TableKit | Add/Remove/Get/枚举/类型桶、派生transition、泛型状态/事件、枚举边界 |
| D-13 | Fluent、Path/Delete/As/Self、LogKit、GUI style | Unity SetParent/name/SetActive/position；C#string/Path/委托；GF日志能力非LogLevel契约 | 大部分原生替换，LogLevel全局联动不等价 | 原生调用保持参数/世界坐标；各模块自己的薄日志选项；GUIStyle替FluentGUIStyle，移除MoonSharp | 位置/父节点、空值、开关级别、Editor/Player不引用PackageKit |
| D-14 | 单文件导出、API文档、Console音频诊断的通用提供者 | UnityEditor Inspector/EditorWindow/资源选择；无模块专属XF等价工具 | 无完整等价 | 模块专用Editor工具、完整导出清单、帮助与Audio诊断；不搬PackageKit/CodeGenKit/ConsoleKit | 导出重新编译/全功能、脚本与prefab归属、帮助逐重载、诊断全字段/平台 |

需要保留的外露订阅能力不限于 Register：Framework/Scripts/QFramework.cs:468-611、690-759、795-957 包含初值注册、无参IEasyEvent、Or组合、手动注销和销毁/停用/场景触发器。只迁移模块返回对象可继续操作所需的闭包，**不复制框架完整事件/IOC系统**。

## 5. Audio 完整资源后端与操作语义

### 5.1 必须交付的加载与接入

基础 Audio 自带真实 Resources sync/async/Unload 实现，自定义 IAudioLoader/IAudioLoaderPool/AbstractAudioLoaderPool 保留。XFAB 后端虽然分程序集，**是本项目提取交付内容，不留给使用方将来实现**。

原 SupportOldQF 桥的定义为 `Q/Toolkits/SupportOldQF/Scripts/AudioKitWithResKitInit.cs:6-64`：BeforeSceneLoad替pool；LoadClip调用ResLoader.LoadSync；LoadClipAsync调用Add2Load/LoadAsync；Unload回收ResLoader。同步和异步名字能力完整接管，不拷ResKit。原桥实际只使用 AssetName，不能宣称已支持任意 bundleName 配置。

路由与配置拟定如下：

1. 保留 AudioSearchKeys 两字段并在请求开始时快照。`resources://x`、`resources/x` 的旧路径解析见 ResKit/ResourcesResCreator.cs:7-16、ResourcesRes.cs:53-60；新内置路由明确兼容这些前缀，直接 Resources 路径仍可用。
2. 随模块交付 AudioAssetCatalog/config，映射 `(legacyBundleName,assetName)` 到 `(backend,projectName,assetName,typeof(AudioClip),ownership)`。AssetBundleName 不直接当 XF projectName；别名/同名冲突/缺映射有清楚错误，不固定返回null成功。
3. XFAB接入先等待 `ReadyRes(projectName)` 并检查 error、profile、资源映射、当前平台及加载模式。不调用只在注释中出现的 AssetBundleManager.InitializeAsync。模块源码不含 MainModule 常量。
4. sync真实调用LoadAsset<AudioClip>/Type重载；async使用`LoadAssetAsync(projectName,assetName,typeof(AudioClip))`和真实request。模块自建含backend/project/asset/type的in-flight去重，避免XF泛型key只含project+asset。
5. `AddCompleteEvent` 对已完成请求可能同步回调，因此先提交pending/消费者状态再注册；完成时同时校验error和AudioClip，不只看isDone。
6. 不在异步接口内调用同步LoadAsset或阻塞Wait。XF的Editor Assets模式实际yield一帧后AssetDatabase同步读（LoadAssetRequest.cs:31-36），不能计为真async验收；XFAB正式异步验证使用AB模式。mapping/manifest预热、资源准备单列，不能隐蔽成播放调用的同步补载。加密分支同步ReadAllBytes/Decrypt和首载前奏如实标兼容限制，未完成准备的严格async配置不得静默走该分支。
7. 配置由随模块交付的Inspector/初始化入口提供。使用者配置资源位置/项目/catalog不等于使用者必须自行实现加载器。Resources、XFAB、自定义loader三类都纳入测试。

### 5.2 所有权与释放闭环

| 模式 | 真实责任 | 与旧后端差异及验收条件 |
|---|---|---|
| BorrowedClip | 外部直接传入AudioClip；只管理播放，绝不Unload | 保持AudioController的XFAB输入链；停止只断AudioSource.clip |
| Resources owned | 模块路由加载、共享cache/lease/pending | async必须写入Clip；最后消费者且无pending再UnloadAsset，不提前卸另一播放器 |
| XFAB OwnedExclusive | 独占音频资源project，所有加载经模块broker | 必须project级pending清零后才允许UnloadAsset；同bundle还有外部加载则不满足独占保证 |
| XFAB SharedPinned | 与当前XF项目共享；模块不UnloadAsset、不UnLoadAll | 报告retained，最终由原project owner释放；这不是“已完成回收所有资源” |

资源cache、加载pending、loader租约、播放实例分别计数。卸载前先停止/断clip；loader归还**分配时pool**，不是当前Config pool。停止/关闭后的请求结果仍需内部收尾，不让回池后的loader/player接收旧成功回调。

XF的`Abort()`只标记完成，LoadAssetRequest底层继续加载（CustomAsyncOperation.cs:126-134；LoadAssetRequest.cs:109-120）。本模块采用逻辑取消与generation，不对共享request调用Abort。晚到owned结果无人使用时按projectPending/lease规则释放；shared结果记录移交。超时只取消模块等待，不能谎称已经停止底层I/O/释放底层资源。

**共享pin不是双向保护**：XF AudioPlayerExtension.cs:288-291 在自己的计数归零600秒后仍可Unload同clip。当前MainModule普通音效与XF BGM/点击/失败音效是不同输入，继续分开；若未来同clip双系统使用，必须先取得统一资源所有权方案，不能用模块内字典伪造XF公共Retain。

### 5.3 独立完成监听，不复制动作引擎

当前`AudioSourceController.cs:71-106,172-191`真实职责为每Update检查`!Paused && !AudioSource.isPlaying`。Condition的首次检查不是注册时立即发生；Stop取消监听且不调用自然完成回调。

新Audio host维护最小监听集合，使用播放generation和待增删队列。保留正常顺序：声音通道SoundFinish -> 非loop用户OnFinish（AudioName仍在、player尚未回收）-> Stop移除追踪 -> 清名字/取消监听/停源/断clip/UnPrepare -> 卸音量订阅、回池/必要销毁。

必须单独处理回调重入、停止/新播放使监听失效、paused、loop、pitch、对象销毁和异步晚到结果。不能用固定clip.length延迟代替isPlaying，也不能将暂停当自然完成。旧Resume实际调用Play而非UnPause、手停不清Music回调等问题在矩阵风险中单列；不在此假称已经变成正确续播。

内部generation只能保护内部回调归属；原返回AudioPlayer/TimeItem/IActionController是可池化raw引用，**不能宣传它让旧外部引用对已复用对象的Stop/Cancel自动安全**。有效期、必要契约变化和原缺陷有单独测试。

## 6. 编辑器、资源、导出、诊断与示例

### 6.1 Action 资源与完整导出

屏幕过渡prefab资源GUID`2d64c6a1947754149b3bd5ac266c99bc`，当前脚本GUID`0e21540f14ffdd9449d56f44db90db84`，prefab:94/97对应组件/ColorImage。完整FadeIn/Out/InOut、Color/Duration/回调/链式均保留。新的Resources键拟为`ProjectActionKit/ScreenTransitionCanvas`，避免与旧同名资源随机命中。新旧共存时复制获得新GUID，不能复用原.meta；所有字段与引用由Unity资产API核对。

prefab:72,152保留旧UnityEngine.UI DLL式GUID/fileID；当前ugui Image.cs.meta为`fe87c0e1cc204ed48ad3b37840f39efc`，CanvasScaler.cs.meta为`0cd44c1031e13a943bb63640046fad76`。这是导入兼容检查项，未运行Unity不能断言已经损坏或有效。将来用Unity Editor确认并保存必要局部差异，不简单文本替换全部资产。

导出工具提供者已找到，不是只有配置：PackageKit/PackageMaker/SingleFileCreator.cs:18-30配置，42-145 Inspector选路径/显示文件夹/Create。原ActionKitSingleFileCreator.asset:20-35引用15个模块源脚本，:37-64引用28个底层脚本，:65是原作者路径。它整体内联Framework、Pool/Singleton/Table及文档属性，且未覆盖后加的Screens/Lerp/Task等；**不能把这个旧清单当完整提取范围**。

保留可配置FileName/LicenseCode/Scripts/DependencyScripts/输出目录、打开目录和生成能力；新工具仅为Action服务，原生UnityEditor+Path/File支撑。完整预设源清单覆盖矩阵Runtime全部能力及必要模块支撑，不引入完整QFramework.cs。保留条件编译结构和版权；不是简单删除using/#if或私有化所有public类型。

单文件形态需要在独立工程重新编译和逐项运行，不能只生成文件便结项。屏幕过渡资源作为配套资产交付，聚合源与多脚本形态不能同装。对单文件MonoScript绑定的方案：导出prefab只保留Unity内置UI组件，导出运行入口按确定资源结构动态附加模块ScreenTransitionCanvas并绑定Image；组件与ColorImage绑定能力保留。原ScreenTransitionCanvas为internal，不误报为外部公开类型。此路径必须验证运行时AddComponent/Player/AOT，不成立时阻塞导出验收，不能删屏幕过渡或静默多出未记录的旧框架脚本。

### 6.2 Audio 诊断与帮助

ConsoleModuleSystem.cs:22-56 原来只注册AudioKit页面，没有自动打开窗口。外部ConsoleKit.cs:29/41提供CreateWindow/ShowModule；ConsoleWindow.cs:68-81的切换方式为Editor/OSX/Windows F1、Android Escape、iOS四指。新AudioDiagnostics提供实际可打开的GUI与Editor窗口，保留这些音频诊断入口体验和平台条件，不搬整个ConsoleKit。

显示内容不缩水：Music/Voice名称、Music暂停、默认播放模式、总活动声音数、按名称计数、逐player音量/位置/模式。原12号字样式用GUIStyle。关闭/销毁时不留下订阅，冷启动不强制打开。

两模块ClassAPI/MethodAPI/描述/示例的实际浏览者为Internal/APIDoc/APIDoc.cs:178-201。保留模块专属API帮助、英文/中文说明和例子，通过各自Editor Help入口呈现；不引用PackageKit、MDViewer/Markdig、MoonSharp或旧属性程序集。原文档元数据移出Runtime：不能在Runtime的UNITY_EDITOR块里改为引用自己的Editor程序集而形成反向/循环依赖。Editor API说明与签名矩阵同步，避免原属性代码中的过时调用继续冒充可编译示例。

### 6.3 范围内的 Samples 与可选联动

Action完整Samples及专用场景/图片按用户要求不迁移，不用“已实现/测试通过”标记豁免项。DOTween/Observable扩展实现和相应自动化验证仍交付，不依靠演示场景作为唯一测试。DOTween当前未安装，不升级/下载；实际第三方程序集版本验证仍是后续条件。UniRxAction<T>所需IObservable<T>/IDisposable用BCL observer支撑，保留相应公开能力和已明确的完成/取消契约；API Help简短用法与必要运行资源保留。

Audio28个脚本（含Designer字段）、16场景、6音频全部去向明确。生成字段可作为普通MonoBehaviour序列化字段保留，移除ViewController/Bind等支撑；预设引用和UnityEvent重绑并验证。Resources案例使用真实Resources后端；两组ResKit案例改为实际XFAB加载并说明地址差异，不保留ResKit.Init。其它暂停/停止/Fluent/Clip/总开关/音量/自定义Loader示例都保留。

`Scripts/Action/PlaySound.cs`是有效现代联动，而不是已注释旧材料。PlaySoundAction两个显式Allocate、继承的无参数Allocate、ISequence.PlaySound两个重载全部迁到可选ActionKitAudioKit Integration。取消动作停止其声音；原动作Paused不暂停声音、失败/拒播时可能等待不结束，作为原问题单列，不把联动漏掉或假称已正确完成。

目录外说明包括Action15章及重复的生命周期章节、ScreenTransition新增说明、Audio4章；适配后保留正文与来源。嵌入图片/线上链接只作为来源，不在本轮联网补入新版能力。两个模块目前没有专属自动化测试，后续创建相应测试，不能把示例运行当作现成测试通过。

## 7. 原缺陷、兼容差异和阻塞处理

完整问题证据、稳定编号和对应功能见矩阵AK-9xx/AU-900..925及资源/扩展风险。统一处理规则：

1. 第一批建立characterization条件与原行为记录，再区分算法契约、原缺陷、实现缺失/历史材料和未知Unity行为。
2. 不因缺陷删除功能，也不把空方法、卡住或资源泄漏写成正常成功契约。
3. 修正方案独立记录，明确会改变哪种行为；实施时需确认相应契约并添加回归，不静默“顺便修复”。
4. 未确认问题保持阻塞/待确认，不能以暂不支持/未使用关闭条目，不能弱化断言制造通过。

重点阻塞包括：ScreenTransitionInOut没有父完成/取消桥；旧动作组合/池复用风险；DOTween getter调用两次及UniRx完成后订阅丢失；音频keys/loader提前回池、错池回收、回调重入、拒播泄漏、同名播放/Resume语义；两套Timer和Heap的边界/迭代/复用问题；Timer.Dump当前空方法；XF共享资源/pending/假取消、Editor Assets分支及加密同步前奏；单文件组件资源绑定；通用框架继承兼容面。

**底层解除不能靠空实现**：不允许NotImplementedException、固定null/default代替原操作、假的异步、只打印成功日志、偷偷调用旧QF，或要求使用者以后补齐必需backend。诊断的日志输出应包含真实状态数据，不能将空Dump当作已实现诊断。

## 8. 分批实施、完成条件和回退

以下批次都要在用户进入实施阶段后才执行。本轮不创建源码备份或新版本库。

| 批次 | 未来工作 | 完成条件 | 有边界的回退 |
|---|---|---|---|
| B0 基线/契约 | 冻结矩阵API子编号、当前源码/资源/许可证快照；建立受控测试工程；确认原问题与必要继承变化 | 原功能全部有去向；原问题有最小重现/判定；版本管理与本批回退方式可用 | 只撤本批新测试/基线产物；不修改现有业务/QF；未经确认不开始迁移 |
| B1 Action基础 | 复制现代完整动作、旧动作/链/FSM/Timeline、Ease/ShortCut；新namespace/池/订阅/host，保留算法 | 独立A环境全Runtime矩阵编译、EditMode/PlayMode通过；无Audio/QF/Game引用；未关闭风险明确 | 仅移除/撤回本批新模块与测试，原框架/业务未切换 |
| B2 Action工具/资源 | 完整屏幕过渡资源、导出器/配置、Editor Help、可选DOTween/Observable；不迁移完整Samples | A环境所有Screens与旧API通过；单文件另工程编译/功能通过；Editor/Player依赖正确；可选扩展不污染基础 | 回退本批新GUID/工具，恢复本批自有新模块变化，不动原资源 |
| B3 Audio基础 | 完整三类播放/返回控制/策略/设置/Loader协议与Resources后端；薄完成host；两套Timer/Heap/Disposable等 | 独立B环境全部Runtime矩阵通过；没有Action/QF/业务；6键/默认值与控制行为可证；资源闭环 | 原业务仍用旧AudioKit；只回退本批新Audio及测试，原prefs不自动清除 |
| B4 Audio后端/诊断/样例 | 完整XFAB backend/catalog/lease；诊断/Help；16样例；支持sync/async与释放 | B环境Resources与AB真async成功/失败/取消/释放通过；shared/owned差异如实记录；Editor/Player验证 | 撤本批backend/config与样例；基础Audio独立仍可用，不删除XF包/缓存 |
| B5 可选联动 | PlaySoundAction及两个Sequence扩展、联动样例/测试；处理已确认等待/取消缺陷 | 两基础单装仍通过；联动自然完成/取消/暂停/失败有明确行为；无反向引用 | 只撤Integration和其调用/样例，不影响两基础模块 |
| B6 游戏接入 | 仅切换已审计AudioKit门面/using；4个GetRandomItem换项目最小List随机取项（或确已核对的XF等价API）；其它QF使用按原目标局部替换 | C游戏回归全部通过；保持XF BGM/点击/失败音效及音效clip输入链；不搬其它Toolkit | 按本批diff恢复本批改动的业务调用；不回退用户原有修改，不改prefs键 |
| B7 完整验收/旧依赖退场确认 | 三环境重验、目标构建、工具/样例/资源/许可证检查、清点旧引用与自动入口 | 矩阵所有功能已实现且有验证证据；无未接受阻塞；基础/导出/联动依赖图无旧QF；旧路径/GUID无隐式依赖 | 保留可核验的上批接受版本；未通过不宣称提取完成；主工程框架退场操作另按当批授权和引用证据执行 |

每批完成是“该批已实现并验证”，不是目标全集已完成。B1/B3不能拿部分可运行功能作完整结项；B2/B4也不是可选省略批次。可以按独立范围并行，集成前核对矩阵和共同资源责任。

### 8.1 实际版本管理

当前目录不是Git仓库；`.plastic`存在，但`cm status --short`不可执行。不能使用假定存在的git分支、commit、stash或checkout作为回退方案，也不自动初始化Git/安装CLI。

实施前使用用户实际可用的Plastic/Unity Version Control客户端核对pending changes；若客户端支持本批shelf/changeset，再只记录本批文件及.meta，不包含用户未归属的修改。不假定客户端已经可用。若它也不可用，B0先确认一个授权的、可复原指定文件的外部快照方式和内容摘要；未确认就不开始修改。**本轮没有创建任何备份、shelf或提交。**

资产初期以复制+新GUID实现新旧共存，回退只撤本批新资产并恢复本批业务引用。将来实际移动原资产时必须连.meta保留GUID；两种方式不混用，不复制原GUID造成冲突。序列化类型/程序集迁移不能只依赖FormerlySerializedAs；需要必要的MovedFrom/Unity资产迁移与实际重导入验证。编辑器工具遵守SerializedObject/Undo/Prefab保存，不覆盖未保存场景。

## 9. 完整验收：A/B/C三组

### A. ActionKit 独立

受控独立Unity 2022.3项目仅声明Unity/UI、Action交付层和已有测试设施；没有QF/CoreKit/Audio/业务。可选提供者单独测试工程或配置，不因未安装第三方污染基础程序集。

- EditMode：逐公开API/重载/泛型约束编译；全部动作与旧链/FSM/Timeline/Ease算法、状态迁移、序列同帧推进、并行/重复/条件、pool和事件索引；边界和原问题characterization。
- PlayMode：局部/全局/当前场景启动、Update/Fixed/Late/GUI/application消息；scaled/unscaled/长帧/协程/Task；暂停/恢复/取消/销毁、旧句柄和回调重入。
- 必要人工：Fade颜色/时长/遮罩/输入阻挡、InOut回调时点、Editor Help、导出配置/生成/打开目录、导出形态资源加载；不要求迁移/逐场景运行已豁免的Action完整Samples。
- 构建：不含Editor程序集的Player；目标脚本后端/AOT；单文件输出在另一受控目录独立编译运行，不能依靠项目中仍在的旧框架。

### B. AudioKit 独立

基础测试环境无Action/QF/业务，Resources完整可用；XFAB环境另加实际XFAB及有效测试资源project，不借游戏资源证明普遍可用。

- EditMode：全部名字/Clip重载、返回player操作、protected/custom继承、Loader/Pool替换、三模式/枚举/defaults；属性/6键/Comparer/CustomProperty、Heap/两Timer/Disposable全部独立用例。
- PlayMode：Music/Voice/Sound、同名/替换/loop/pitch、暂停/恢复/手停/自然完成、多回调时序、timeScale0、host销毁/Shutdown/关闭Domain Reload重复Play。
- 加载：Resources真实async和XFAB真实AB async、sync、缓存命中同步回调、错误/空clip/超时、同key多消费者、取消一人/晚到/换pool/复用；没有同步Wait伪装异步。
- 释放：borrowed、owned/shared、最后lease、projectPending、同bundle并发、场景生命周期和XF其它消费者；shared retained不按“全部卸载”通过。
- 必要人工：实际听感、播放中音量即时更新、诊断全部数据/平台入口、16样例和后端配置。自动测试无声也不等于实际发声验证。
- 构建：Audio Runtime没有Action引用；Editor隔离；Player/目标AOT/裁剪/后端资源类型解析。不要用原QF保留全程序集的link.xml掩盖裁剪缺口。

### C. 当前游戏回归

不重复01整份审计，但保持以下已确认约束：

- AudioController继续`XFABManager.LoadAsset<AudioClip>(Module.ProjectName,assetName) -> 新AudioKit.PlaySound(AudioClip)`，clip作为borrowed输入。
- SoundVolume键`KEY_AUDIO_MANAGER_SOUND_VOLUME`、默认1.0、运行中更新；Music/Voice另外五键也保留，不迁到XF键。
- 去重仍为共享帧窗口10与原Clip标识分支，不改成逐声名冷却。N/N+1、相同/不同clip、声音结束移除策略分别测。
- HeadGun/MachineGun/ShotGun/RocketGun的随机取项仍来自XF OverlapCheck结果，保持空列表外层检查与Unity.Random分布；项目数学扩展保持原样。
- 10处包装调用的6个输入clip、开火/死亡/脱落/火箭爆炸；设置面板真实UnityEvent；切场景/暂停菜单/重新开始/重复Play。
- XF BGM、ClickPlayer和挑战失败音效不改实现、设置键或资源释放；不误将AudioController的AudioPlayer别名换成新模块类型。
- 既有旧UnityEvent类型名/空target等残留按01限制处理，不把本轮提取变成无关修复，也不绕过实际回调验证。

独立验证使用另一个受控项目路径/CI或明确关闭后的Editor流程，不对已打开的同一主工程启动第二实例，不删除主工程框架或Library来制造独立证明。本轮没有创建独立项目或执行这些步骤。

## 10. 尚需核实的最小信息与完成标准

1. 原问题的受控行为记录及对修正行为的确认，特别是ScreenInOut、Resume、拒播、回调复用、Timer边界、Dump空实现和第三方扩展。不能自行排除它们。
2. 必要的公开底层继承变化是否有审计范围外消费者；方法概念保留不意味着IArchitecture/IModel/泛型TableIndex类型身份仍兼容。
3. 实施用Plastic客户端或授权快照方式；当前没有可执行版本控制CLI，不假定工作区干净。
4. 实际目标平台、脚本后端、Domain Reload配置、现用资源profile与构建产物；只有当前源码静态证据，尚无这些运行/构建结果。
5. DOTween/UniRx可选测试提供者的实际版本/程序集；本地只有压缩扩展和说明，不在本轮安装或假定第三方已经存在。
6. single-file组件绑定、旧UI DLL式引用、样例生成字段/UnityEvent的Unity导入结果；源GUID对应不是资产有效性的运行证明。ugui的manifest/lock声明2.0.0与本轮可读缓存1.0.0不一致，须确认Editor实际解析版本，不凭缓存或声明单方推断已验证。
7. XFAB共享同clip/同bundle的所有权；需要自动释放时选择独占audio project或统一broker，现有公共XF API不能凭空给外部消费者加引用保护。
8. 示例音频/图片的可复用分发许可和无版权头文件的来源；源码中已有MIT及作者声明完整保留，Timer.cs头部还记录UnityTimer来源链接，不把所有资源/辅助文件未经核实统一声明MIT。

完成标准是矩阵范围内所有模块功能有实际实现、兼容映射和相应验证证据，所有阻塞已明确解决/接受，A/B/C及范围内的工具/样例/资源/必要构建全部覆盖。用户明确豁免的Action完整Samples单列OUT(user)，不算已实现；不能据此削减功能和回归。原QF仍在主工程时一次编译成功、当前游戏能发声、几个动作能执行，都不足以证明完整或独立。

## 11. 本轮只读与产物核对

本轮授权写操作只用于本计划及02-feature-matrix.md。压缩资源仅通过tar读取或内存解压，没有向工程/临时目录导入源码或资源。没有执行源码备份、生成asmdef、运行Editor、升级依赖或删除旧框架。

交付核对发现先前写入文档的长摘要存在录入/对照问题，已撤销不可靠的起始摘要值，不用它们宣称全程内容一致。工具复算的实际当前状态如下：

| 核对对象 | 实际结果 | 能证明与不能证明的范围 |
|---|---|---|
| 3571个去重工程文件，Assets/Packages/ProjectSettings/.kilo及根目录生成项目文件，排除两份02文档 | SHA256 `D31310B06F3959E57B311BD6404348083CACFF0AE0810F85EACA285B0637A71B` | 当前完整快照；没有可用Git/Plastic diff，不宣称与本轮开始完全一致 |
| 原01-audit.md | SHA256 `F150FBBE06179C8CC9E9106A9ECEA5A00F4746BE896ECC6461A3B24DC48CB4CB`；LastWriteTimeUtc=2026-10-02 14:10:08，早于本轮请求14:33:39 | 本轮工具没有对原审计写入，时间戳也早于本轮；不会修改01来修正02的摘要记录 |
| 两个原模块目录全部199文件，含源码/.meta/资源/归档 | 当前SHA256 `11302FCE7F665419F8BBD550383AB48C81D339D3FBF88753B82BC3A6CB393148` | 当前模块冻结标识；26/54源码覆盖和已核对签名保持本地基线，不声称已迁移/编译 |
| 本轮期间有修改时间的非报告文件 | `Assets/Games/MainModule/FSM/Enemy/EnemyShield.asset`，2026-10-02 14:55:56.8246697 UTC，当前SHA256 `1359D23E64DCF6755E9D436666888F26CD37AD7CADD9B28A1F6F7508B01EDFA3` | 仅此文件的mtime晚于请求（在所枚举范围）；本轮没有对它执行写操作，来源未判定，现状保留，不回滚/覆盖 |

EnemyShield当前仍是XFFSM配置，脚本GUID、EnemyChase/EnemyShieldAttack类型链已局部复核；不影响本轮两个模块的完整API基线，但游戏回归应以届时最新资产为准。mtime检查不是逐字版本diff，不能据此给整个工作区“干净”结论。终端git status仍返回非Git仓库，cm不可用。唯一工程写工具调用是这两份02文档的apply_patch。

修正文档后连续复算返回MatchesFinalSnapshot=True、AuditMatchesRecordedCurrent=True；这证明最后核对窗口的快照稳定，不替代已经明确缺失的开始到结束版本diff。功能编号覆盖检查为93个主功能族缺号0，源码数量26/54与盘点一致。没有Unity编译、测试或独立安装通过结论。
