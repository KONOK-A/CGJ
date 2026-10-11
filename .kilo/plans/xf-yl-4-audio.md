# XF × YL 阶段 4：Audio 深度融合研究与总体方案

**状态：总体方案及 D1/D2 已批准；4.1 已单独授权并完成本批技术实施与定向验证，待用户验收及 Git 收口；4.2～4.5 未实施。** 本文是阶段 4 的唯一方案；不规定逐类逐方法的实现脚本，具体 API 与内部结构由获授权批次按下述契约设计。阶段 4 尚未 COMPLETE。

**目标：**新 YL 只有一个音频播放、请求生命周期与资源收尾权威，融合双方有价值的能力；原游戏、原 XF/XFAB/XFFSM Package 和阶段 1～3 成果不受破坏。方案批准、批次实施和 Git 授权分别处理。

## 1. 基线与边界

- 2026-10-11 实际核对：`integration/xf-yl`，HEAD `7e44d9fe9eaba0224988ce8a537b2b891cdfc7ef`，`milestone/3-ui` 指向 HEAD，开始时工作区干净；研究后仅新增尚未跟踪的方案文件。阶段 1～3 完成以用户确认、该 Git 基线及阶段 3 最终成果为准；长期蓝图首页和专项中的“待收口”文字有历史滞后。
- 已读根 `AGENTS.md`、[长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)、[核心承接约定](1791455404620-xf-yl-core-ownership.md)、[阶段 3 第 13 节](xf-yl-3-ui.md#13-34-实施与阶段门禁收口2026-10-11)及现有 Architecture/Audio/Resource 文档，并以源码核验关键判断。
- Unity `2022.3.62f3c1`；XFGameFramework `1.18.8` / `2182708aec`，XFABManager `1.24.0` / `c35dc4058e`。manifest/lock 与实际 PackageCache 的部分 Unity 包版本不同，属于既有环境恢复风险，不在本轮修补依赖。
- 本轮只进行只读研究、官方 API 定向查证和编写本文；没有刷新、编译、测试、Play、Player 构建或 Git 写入。测试源码存在及历史通过记录不算本轮验证。
- 原 Package 只读；不改 `Assets/Games` 业务、游戏 Prefab/Scene/设置、MainModule 映射与 Bundle，不自动重建或复制 XFAB 产物。完整 Resource/XFAB、Action、Singleton、Save、AsyncOperation 及其他生态融合均不在阶段 4 内。

**继承约束：**Module 保持唯一业务 owner 和既定关闭顺序；View/Panel/UI 的请求、会话与退出协议不变。Foundation 提供唯一 Pool/Events/Update/Timer，不引用音频或资源业务。Core 与整体 Runtime 保持互不引用。公共 `YLFrameworkRoot` 的 Audio/Action 归属保留，不以音频融合为由删除、换成 DI 或重构整个 Root。

以下源码缩写对应此基线：`Y` = `Assets/YLFramework/Runtime/`；`C` = `Y/XFMain/`；`E` = `Assets/YLFramework/Editor/`；`T` = `Assets/YLFramework/Tests/`；`G` = `Assets/Games/MainModule/`；`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`；`AB` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`。行号用于定位，实施后以符号和实际源码为准。

## 2. 真实结构与能力对照

### 2.1 三条不同路径

| 路径 | 实现及实际角色 |
|---|---|
| 原 XF | `XF/Runtime/Manager/Audio*.cs`，`XFGameFramework` 程序集；原包继续供原生态使用。当前游戏资产已迁出原 XF AudioPlayer，但不能因此删除或修改原包 |
| 自有 XF 副本 | `C/Manager/AudioController.cs`、`AudioPlayer.cs`、`AudioPlayerExtension.cs`、`AudioPlayerAutoPause.cs`，属 `YLFramework.Core.Runtime`；配 `E/XFMain/Inspector/AudioPlayerInspector.cs`。资源仍绑 XFAB，Update/协程已接 Foundation，自有偏好键与根名已隔离；仍是另一套音频权威 |
| 现有 YL | `Y/Audio/AudioKit.cs`、`AudioHandle.cs`；Internal 的 AudioManager/AudioVoice/AudioMusicPlayback；Config/Components/Mixer/Actions/Diagnostics；`E/Audio/`；`T/EditMode/Audio`、`T/PlayMode/Audio` 与 Mixer fixture。属 `YLFramework.Runtime`，只借用调用方 Clip，无 XF/XFAB 静态依赖 |

副本并非重新设计后的 XF Audio：主要差异是 namespace、键、根名、Foundation 驱动及 Remove 的订阅/异常加固。双方 Runtime 音频没有互相调用。音频 `AudioController` 是命名播放控制器，不是 MVC `Controller`，不能按类名认定其归 Module。

### 2.2 能力与取舍

| 能力 | XF / 自有副本的实际实现 | YL 的实际实现 | 融合判断 |
|---|---|---|---|
| 播放拓扑 | 任意命名 Controller，各持一个 Source；Play 替换、PlayOneShot 重叠、loop | 缺省 Music 双声部、Voice 独占槽、池化 Sound 多请求 | 保留 YL 请求内核；吸收任意命名控制通道，不把所有场景压成三个固定槽 |
| 暂停/停止/进度 | Controller 级暂停；Stop 区分替换轨/全部；Play 进度为 Source.time | 代次安全句柄；Music 控制目标与尾轨；Pause/Resume、Time/Duration/Progress、Pitch/倍率 | 保留请求精确控制，组控制不得误停他人或复用后的请求 |
| 音量/静音/开关 | 各 Controller 持久化 Volume/Mute，变更回调；每次写 Save | 三类持久化 On/Volume；临时 Mute 与 Mixer 路由；Binding 订阅 | 保留原游戏六键和语义；自定义组支持独立音量/静音及显式持久化，不按 Source 实例生成键 |
| 切换/渐变 | Play 硬切，无 fade/crossfade | 手动/自动首尾 fade、Pitch 联动；Linear/EqualPower 双轨 crossfade | 保留成熟包络与重入保护，不叠第二套 Tween/Timer 播放链 |
| 空间/Source 接入 | 固定点 PlayClipAtPoint；可借入现有 Source，其效果/设置有价值 | 2D、固定位置、Transform 跟随、距离/rolloff；全部自建 Source | 保留池化空间请求；增加受控借入 Source，不使用不可追踪的 PlayClipAtPoint 临时对象 |
| 素材组/选择 | 按组名选直接 Clip 或资源名，组内均匀随机、可启用即播 | Clip 列表均匀/加权随机；MusicPlayer 单 Clip；首次 Start 自动播 | 素材组成为正式选曲能力；选曲组与音量控制通道是不同概念；旧组件自动播放时机不变 |
| 复用/预算 | 一个 Controller 复用一个 Source，不是请求池，无容量策略 | Foundation ObjectPool；全局/每 Clip 并发、Priority 抢占、帧去重、闲置预算/预热 | 保留并区分活跃请求预算、空闲 Source 缓存和 Clip 资源缓存，不能互相代替 |
| 资源 | 同步 XFAB 命名加载；音频本地 refcount，零引用后 600 秒尝试卸载 | Clip-only，资产和 Mixer 全部借用；没有命名加载或异步播放请求 | 吸收命名加载/保活/闲置释放用途，重做正确的资源责任；两方当前 Audio 均不具备完整异步加载/取消链 |
| 回调/异常 | 公开音量/静音事件；结束钩子主要用于内部减引用，不是用户完成 API | 同步 onStarted、自然 onFinished；代次、快照、epoch、失败回滚，逐请求更新异常隔离 | 保留现有对外通知语义；新增内部全终态收尾，不把 onFinished 当资源释放入口 |
| 生命周期 | 独立 DDOL 根、逐 Controller Update、常驻卸载协程；AutoPause 轮询 timeScale | Root 下惰性宿主，Settings 精确订阅、Shutdown、会话重置、组件追踪自有句柄 | 一个音频领域 owner；自动暂停仅显式开启，不能改变原游戏暂停时的音乐 |
| Mixer/Editor | 没有 Mixer；Inspector 编辑两种素材组 | 三态输出路由、显式参数/Snapshot 控制、原生 Inspector、帮助、诊断 | 合并有价值的 authoring；不把 Source 音量自动绑定 Mixer，也不因游戏未用就淘汰 Mixer |

关键依据：`C/Manager/AudioPlayer.cs:76-153,259-312,376-448`；AudioController `172-249,251-301,309-379`；AudioPlayerExtension `158-205,238-299`；`Y/Audio/AudioKit.cs:180-210,243-446,453-528`；AudioHandle `5-58`；Internal/AudioManager `42-66,78-233,305-373`；AudioVoice `45-86,175-271`；AudioMusicPlayback `41-148`；Config/AudioKitSettings `10-38`。

**不假冒完整工业音频引擎：**当前两方均没有 DSP/sample 精确调度、声音虚拟化、HRTF/遮挡等；YL 也没有自动 ducking/Mixer 参数渐变调度。阶段 4 不新建这些能力，不把帧去重改成毫秒策略；多轨/组扩展只满足 XF 已有的独立播放用途，不扩展成音频中间件。

## 3. 消费者与实际依赖

### 3.1 原游戏的必须保护项

- 启动仍为原 `Main → XFAB ReadyRes → XF MainModule → XFFSM`。`G/MainModule.cs:23,27-28` 初始化业务 AudioController，设置 Sound 全局去重 10 帧；战斗经 `Scripts/Controller/AudioController.cs:40-44` 同步 XFAB 加载 Clip 后交 AudioKit，只借用而不卸载。
- 菜单 StartPanel 的 AudioMusicPlayer 自动播 `03.wav`；开始时 StopMusic；Gaming 从 Game 场景 `Virtual Camera` 取得 AudioMusicPlayer，显式播 `02.wav`，退出只停自有句柄。`G/UI/StartPanel/StartPanel.cs:11`、`FSM/GameState/Gaming.cs:22,109,116,128`。暂停/结算的 `timeScale=0` 不等于显式 PauseMusic，不得默认新增自动暂停。
- 七个面板共 15 个 Button 持久事件指向 `YLFramework.Services.Audio.AudioEmitter, YLFramework.Runtime`；共享 ClickPlayer（GUID `1c8110ac399722e4997fc0a87d1432eb`）的点击 Clip 与显式 EveryOne 配置保留。面板停用不截断已提交的 2D/固定点 one-shot；loop/follow 停用会停止。
- AudioEmitter 脚本 GUID `e0f9382a5a3344d4a979ae028b52cb9c`、AudioMusicPlayer GUID `a02644b3809d0e34aa3ad1a21593f647`，以及 namespace、程序集、有效字段/默认值和无参 Play/Stop 的 UnityEvent 契约不变。旧资产缺 `useGlobalPlaySoundMode` 保持 false，新 Editor Reset 才设 true；不能用统一默认值把点击音改为继承全局去重。
- `G/Scripts/Controller/AudioController.cs:8-18` 只迁移并删除 `AudioConfigVolume:Music` 到现有 MusicVolume。原 XF mute/Sound 键未迁移是既有业务兼容缺口，不在框架中擅自补迁移、删除键或修改业务。Settings 保留 `KEY_AUDIO_MANAGER_{MUSIC,VOICE,SOUND}_{ON,VOLUME}` 六键；Mute 仍是临时状态，禁用不等于静音。
- 战斗调用真实素材包括 `机体爆炸1`、`霰弹1`、`火箭发射`、`火箭爆炸1`、`机炮3`、`机炮4`；GameOver 另播 `挑战失败`，显式 EveryOne。现有流程测试未实际触发全部武器音效，不能把菜单通过外推为战斗音频全通过。

资产依据：`G/UI/StartPanel/StartPanel.prefab:204,672,805,1184,1326`、其他面板同类持久事件；`G/Scenes/Game.unity:2604`；`G/Art/Prefabs/ClickPlayer.prefab:44`。现有 `G/Tests/EditMode/YLAudioWiringTests.cs:9-139` 定向锁定七面板、Clip、组件及 Game BGM 接入；运行测试见第 7 节。

当前文本资产未发现自有 Core.AudioPlayer 或原 XF.AudioPlayer 的实例引用；Core 副本的真实消费者是 `T/Core/PlayMode/CoreCoexistencePlayModeTests.cs:17-56`、CoreUpdatePlayModeTests `18-86`、`T/Core/Editor/CoreEditorIdentityTests.cs:119-126` 与自有 Inspector，而不是原游戏。`AudioModel` 空、业务 PlayBGM 无现存调用，NewAudioMixer 无现存引用，均不据此删除或修改游戏内容。

### 3.2 依赖与责任

| 关系 | 当前事实与阶段 4 边界 |
|---|---|
| Module/MVC | 两套音频核心均不由 Module 关闭自动管理。业务 Controller/组件持自己的句柄/资源；需要模块音频随关闭结束时，在业务明确的所有者钩子结束自有工作，不修改 ModuleManager 关闭管线或清全局音频 |
| Root/Action | AudioManager 父级为 Root.GetModuleRoot("Audio")；ActionGlobalHost/ActionRecycleDriver 使用 "Action"。Root 只管共享 DDOL 层级，Audio.Shutdown 不销毁 Root、Action 或 UI；Root 不是第二个音频 owner |
| Events/Binding | YL Settings 使用 Foundation Binding/Events；副本已持 Foundation Update token。音频自身仍需请求/宿主代次，普通退订不能消除在途快照 |
| Update/Timer | YL AudioManager.Update 驱动自然结束、fade、crossfade、去重；保留音频领域 Tick。可选缓存到期使用既有 Timer 或领域 Tick，只保留一种到期机制，不建立新公共 scheduler，也不强制把所有 Audio Tick 接 Action/Timer |
| Pool | YL 直接用 Foundation ObjectPool<AudioVoice>，归还是请求数据复位，不等于卸载 Clip。借入 Source 不进自有 Source 空闲池，不会因池清理被 Destroy |
| Resource/XFAB | YL ResLoader 已有逐 Loader Retain/Release、同步/异步及回收；可做 Audio 窄 provider。XFAB 仅按资产身份登记缓存，不提供每次加载的独立租约；其共享 producer 不可被某个等待者取消/卸载 |
| XFFSM/Save/Listener | XFFSM 经原游戏状态控制调用组件，不是 Audio 内核依赖；音频设置走 PlayerPrefs，没有必须引入的 Archive/Save 接点。场景/业务拥有 AudioListener，框架不创建第二个 Listener或代管其全局 pause/volume |
| Action/DOTween | Audio 的 PlaySoundAction 是同 Runtime 内真实桥接，保留其 ActionID/执行版本/取消语义。AudioSourceShortCutExtensions 和 DOTweenModuleAudio 是调用方 Source 的直接工具，不是第二套全局音频框架，不能为统一而强改 Action 或 DOTween |

依据：`C/Manager/ModuleManager.cs:217-276`；`Y/YLFrameworkRoot.cs:15-26`；Action/Internal/ActionGlobalHost `15,60-101`、ActionRecycleDriver `10`；Audio/Internal/AudioManager `31,45-65,305-327`；Audio/Actions/PlaySoundAction `44-87`；Resource/Framework/ResLoader/ResLoader `368-398,471-545`；`AB/Runtime/AssetBundleManager.cs:1540-1561,1585-1645`。

## 4. 问题与风险分类

以下源码判断不等于本轮已运行复现；修复只在自有 YL 实施。

| 问题 | 证据与处置 |
|---|---|
| XF 资源引用并非所有权 | 所有 Play，包括外部直接 Clip，都进入本地 refcount；空闲后统一调用 XFAB.UnloadAsset（Extension `158-205,238-299`）。XFAB AddAssetCache 对重复对象直接 return，Unload 删除一次登记并可能 Bundle.Unload(true)（AB AssetBundleManager `1544-1546,1633-1645,660`）。音频本地计数为零不能证明原游戏等外部消费者已释放；不照搬此算法 |
| XF 结束判定与清理弱 | AudioData 以 unscaled 累计到 clip.length+1 判结束，不考虑实际 pitch；Stop(Play)/Stop(player) 主要清 clip，记录延迟清理且暂停时可能不推进；PlayAtPoint 对真实临时 Source 无控制。见 AudioController `27-29,279-301,325-359`。统一请求按真实 Source 状态和自身终态收尾 |
| XF 借源/订阅/键问题 | 属性查 controllerName，但借源播放使用生成名；ControllerName setter 可能对 null 调 Equals；OnDestroy 没有成对解除命名控制器变更订阅；实例 GetHashCode 被拼进键。见 AudioPlayer `197-219,259-284,361-373`。新接入不保留这些身份与隐式副作用 |
| XF 重入和异常 | Stop(All) 通知后才 Clear；更新通知后仅检查 removed 再 RemoveAt，外部重入/异常可能破坏记录（AudioController `262-276,351-359`）。当前结束钩子主要是内部减引用，不能把潜在外部重入风险写成已复现游戏故障 |
| YL 请求收尾是扩展关键 | onFinished 仅自然结束，Stop/替换/拒播/失活/Shutdown 不通知；借入资源若仅绑 onFinished 会泄漏。AudioVoice `223-259`。须把不可绕过的内部全终态释放加入原代次协议，而非新增旁路播放器 |
| 音频数据就绪缺口 | 当前 Audio 内没有 loadState/LoadAudioData 分支；Source.Play 后立即置 mStarted，随后用 !isPlaying 判断自然结束（AudioVoice `175-199,223-241`）。背景数据 Loading 下存在早通知/早终结风险，需受控复现；不能认定当前游戏已受影响，其定向检查的素材为 preload=1、background=0 |
| 真实退出与初始化风险 | YL 有 SubsystemRegistration/Shutdown 和 MonoSingletonHost quit gate，但普通 host gate不等同 Foundation CanRunExecution；新在途加载不能只靠 Coroutine finally 或下轮 Reset。AudioKit `180-210`、AudioManager `33-65,338-373`、MonoSingletonHost `112-130`、Shared/Internal/SingletonSessionState `23-43`。需验证退出回调拒绝重建、初始化失败收尾和旧会话迟到结果 |
| 测试边界 | 现有 Audio 数学/复用/重入/预算/原生 Source/Mixer 测试丰富；Quit、真实双 Play、数据 Loading/失败、共享资源租约和新借源协议未由它们完整证明。GC ProfilerRecorder 有运行器前提，不以全平台零分配为阶段标准 |

Unity 2022.3 官方 [LoadAudioData](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip.LoadAudioData.html) 与 [loadState](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip-loadState.html) 区分 Clip 资产与音频数据就绪：背景加载可延后实际播放，非 background 的数据加载仍可能同步。阶段 4 不擅自修改游戏 importer，也不承诺名称异步加载等于全部解码无阻塞。此处因当前工具未提供 Context7 查询接口，使用对应版本官方网页。

## 5. 推荐统一架构

### 5.1 唯一领域与程序集

```text
代码 / AudioEmitter / AudioMusicPlayer / 正式素材组与控制通道
    -> AudioKit 的直接 Clip 或命名请求入口
    -> 唯一 AudioManager / AudioVoice / 音乐过渡内核
    -> 自有或明确借入的 AudioSource

命名请求 -> Audio 专用 Clip provider -> Clip + 明确释放责任
                                    -> YL ResLoader（受控持有）
                                    -> 可选外置 XFAB 适配（仅借用）
Foundation 提供公共支撑；YLFrameworkRoot 保持共享非 UI 层级。
```

**D1 已确认：**唯一正式音频归现有 `Y/Audio`、namespace `YLFramework.Services.Audio`、程序集 `YLFramework.Runtime`；既有真实游戏类型/程序集身份保留，但不冻结其内部实现。吸收有效 XF 能力后退出自有 Core 音频四文件、重复根/表/驱动/卸载协程及旧 Inspector，不保留 Core.Audio* 空壳。原 XF Package 留存不属于新 YL 双内核。

不新增 Core→Runtime 或 Runtime→Core；音频不自动从 ModuleManager 猜当前 owner/资源项目。XF 原 AudioPlayer 实际使用显式 projectName，也没有必须复制的“从所有 Module 回退查音频”契约。需要 Module 信息的业务在外部显式提供资源定位与清理责任。

只有可选 XFAB Audio 接入需要独立 Integration 边界：置于 `Assets/YLFramework/Integrations/XFAB/Audio/`，窄 asmdef 向 Runtime+XFAB 依赖，不要求 Core。不是新音频内核，不由 Runtime/普通 Editor 反向引用，不自动注册或随默认 ExportRoots 带出；安装它才提供该后端。Core 的其他临时 XFAB 依赖保持后续资源专题归属。

### 5.2 请求、控制通道与 authoring

- **请求是一次播放租约：**沿用 AudioHandle 的代次安全、参数快照、校验先于副作用。开始通知仍可能早于返回；自然完成语义保持，Stop/替换/失效不是自然完成。内部终结先失效/摘索引，再停源和释放自有资源，清理异常继续并可观察；自然回调原有时点不被悄悄改写，资源至少保活到该原通知/收尾完成。
- **控制通道不是 MVC Controller 或第二 Manager：**缺省 Music/Voice/Sound 及其键/开关行为保留；增加有明确身份的命名通道，支持局部音量/静音、独占替换或重叠请求及通道级控制。自定义独占通道允许多条独立循环背景/环境音，均使用同一请求内核；全局缺省 Music 仍最多两声部，新增通道不挤占它。Sound 类通道继续受现有全局/Clip Sound 预算约束。
- 分类设置与独占/重叠拓扑分别建模：缺省 Music/Voice 关闭时停源但保留请求，重新开启从头重播；Sound 关闭时终结已有声音并拒绝新请求。Mute 不停止、不重播、不冻结进度或 fade，显式 Pause 才冻结；新通道不能反过来改写这些游戏契约。
- 命名通道登记冲突明确拒绝，不按相同名字默默共用生命周期；设置身份与 Source 实例身份分离。持久化只在提供稳定存储身份时启用，不用 GetHashCode/运行时对象名落盘；默认通道倍率 1，保留既有 Source 增益/静音/Mixer 分工，不额外引入默认 Master 或自动 ducking。
- **素材组只描述选择：**按组名解析直接 Clip 或明确 backend 的资源定位，均匀/加权只抽选一次，非法/空组在替换旧播放前发现；失败不误停旧请求。按组自动播放和 Enable 重播是新配置的显式选择，不修改现有两个游戏组件“首次 Start”的约定。
- **借入 Source 是受控独占：**接受主线程、有效且由调用方明确让渡播放控制的 Source；拒绝并发借用/隐式抢占。记录本次改动的播放字段，不重挂父级、不停用或销毁外部 GameObject、不清用户自定义曲线/滤镜；释放只停止自己的租约并按仍有效的归属恢复字段，不恢复或伪造此前播放历史。
- 借源首版只承接可独立控制的独占播放。同一 Source 的原生 PlayOneShot 重叠无法提供可靠的逐次停止/完成句柄，不伪造此组合；需要重叠时使用统一池化独立 Source。此限制属于本方案的明确 API 边界，不保留旧 Controller 内核来模拟它。
- **自动暂停显式且局部：**保留 XF timeScale 自动暂停的用途，但只操作自己登记的租约，并区分手动暂停与自动暂停，自动条件解除不能擅自 Resume 手动暂停。默认不启用，不修改原游戏 BGM、UI one-shot、Mixer 或全局 AudioListener.pause。
- 组件/调用方持有自己的请求及 pending 身份；失活/销毁时按配置结束自己拥有的工作。已允许越过组件失活的固定 one-shot，其资源责任已交请求内核，不能被组件提前释放。Module.Close 不隐式清共享音频、Timer 或 Action。

### 5.3 窄资源契约与安全过渡

**D2 已确认：**直接 Clip 永远借用；命名取得支持明确资源租约和可选闲置缓存释放；暂存 XFAB adapter 只借用共享资产、不自动 UnloadAsset，完整 XFAB 所有权与物理释放留待资源专题。

- Audio 只定义取得 AudioClip、成功/错误/进度、逻辑取消和成对收尾所需的窄接点，不建立通用 Resource 门面、下载/更新/Bundle 管理器。定位含 provider/backend 与完整资源身份，不把 ProjectName 与 BundleName 混用，不按 clip.name 合并所有权。
- 第一个受控 provider 优先复用现有 YL ResLoader：其真正 Loader 持有 Clip 及依赖到全部相关请求结束，包括 disabled Music/Voice 待重播、crossfade 尾轨、暂停、loop 和允许继续的 one-shot。不能在“加载完成/新曲开始/组件停用”时就回收 Loader。资源后端负责物理释放，Audio 不自行 Destroy/Unload 资产。
- 音源缓存与素材缓存独立；素材缓存仅 provider 保证持有的条目才可采用闲置到期淘汰。默认不强制 XF 的 600 秒常驻；可显式配置预加载/闲置缓存及释放，等待者与实际播放持有独立责任。空闲期限按明确 unscaled 时钟，暂停游戏不无限滞留；重新取得取消旧淘汰，清理不触及原游戏缓存。
- 缓存只在本 provider 的完整 key/代次内合并加载，不为不同后端的相同 Clip 统一引用数；其他直接借用者仍须由外部 owner 保活。没有全局租约的 XFAB adapter 释放只结束本地借用/观察，不卸载共享 Clip/Bundle，不把“迟到结果丢弃”写成“XFAB 资产已释放”。
- 异步请求区分“资产/音频数据就绪”“已接纳”“已启播”“播放终结”。在途不伪造有效 AudioHandle；结果保留稳定状态/错误与原播放身份，接受结果不保证此刻可听见，开始回调重入自停后也不能改写为另一代成功。
- Play A 加载慢、随后 Play B：同独占通道最新有效请求决定提交，A 迟到不能抢回音乐；加载失败不停止现有有效播放。Stop/owner 退出/通道退出/Shutdown 同时使自有 pending 逻辑终结，迟到结果只按原 owner 成对归还，不能建源、登记组或通知新会话。
- 一个等待者取消不物理取消共享 producer，不释放其他等待者/播放的资源。底层永久不完成时允许该等待者逻辑结束；不擅加全局超时、改 XF 协程或以后台线程调用 Unity API。对无物理取消能力的 producer 明确保留其自身资源收尾限制。
- 资源取得和释放责任必须在 Source.Play/onStarted 之前绑定到原请求；不能在公开 Play 返回后才登记，避免同步回调 Stop/替换/Shutdown 留资源。所有终态均释放恰一次，释放失败不复活旧请求，也不影响其他清理。

## 6. 旧机制退出与兼容策略

| 范围 | 退出/保护方式 |
|---|---|
| 自有 Core Audio | 按功能进入正式 Audio 域，不把原四类整体搬过去改名。正式分组/借源/加载能力验证后删除重复实现、Inspector 与对应 meta，核对 GUID、反射及 Editor 绑定。迁移过程中短暂并存不算最终完成 |
| 原游戏 YL 接口/资产 | 既有 AudioKit/Handle/Options/Emitter/MusicPlayer 的真实调用与序列化身份不变；新能力默认关闭/省略时维持行为。原游戏不添加 asmdef 引用、不改 UnityEvent/Prefab/Scene、不靠重建 MainModule Bundle迁移类型 |
| 设置 | 六个游戏键不改；不自动读写原 XF 或自有 Core 的任意旧键，不把新组设置映射为游戏 Music/Sound。仓库没有必须保留的 Core 持久化消费者样本；若后续出现真实外部资产/持久化样本，先确认定向迁移，不建无依据全量兼容层 |
| Core 历史测试 | 将音量键隔离/借源不销毁、Pause/Stop/源销毁与清理异常撤销的有价值契约迁入正式 Audio 验证；旧字段反射、旧类型绑定应随真实退出更新。CoreEditorIdentity 删旧 AudioPlayerInspector case但保留其他身份检查，并增加唯一 Audio/原包隔离断言；不是删测试让退出看似通过 |
| 文档/来源/分发 | 实施时增量维护现有 Audio.md、Architecture、Help、必要导航与本方案记录。保留 QFramework 及 XF MIT/署名；吸收 XF 源码移出 Core 时补现有 THIRD-PARTY-NOTICES 的范围，不让目录迁移丢许可。可选 XFAB adapter 的安装/依赖/释放限制写清，不重做阶段 6 整套导出工具 |

## 7. 实施路线与验收门禁

**4.1 → 4.2 → 4.3 → 4.4 → 4.5。**每批一个主要变更轴，依赖前批验收及单独实施授权；允许内部渐进实施，不另建逐类专题/过程报告。新名称/API 不是先决审批项，改变已确认边界才重新提交。

### 4.1 请求生命周期与统一收尾基础

- **范围/产物：**现有 `Y/Audio/Internal` 与必要 AudioKit/Handle 接点；建立请求全终态、资源责任挂接和宿主/pending 会话基础，保持原 Clip API/回调时点。核验并修复直接相关的数据就绪、退出重建及初始化失败问题；不加资源后端，不迁游戏或提前删 Core。
- **EditMode：**复用 Cleanup/Started/Admission/Crossfade/Fade 的核心代次断言；补自然完成、Stop、替换、onStarted 重入/抛错、清理异常与旧租约不能影响新请求的最小用例。只有确认问题才新增回归，不根据推测大改预算算法。
- **PlayMode 门禁：**实际 Source、暂停/静音/关闭/重播、短 Clip 结束、Source/owner 外部销毁及重复 Shutdown，资源收尾恰一次。直接组件和 PlaySoundAction 的同步开始重入保持；既有游戏点击及 Music lease 隔离定向回归无新增失败。

#### 4.1 实际成果与验证（2026-10-12，Editor 本地时间）

**实施基线：**开始时实际分支 `fusion/4-audio`，HEAD `aa1ff5af2f2c48c8b8e767703fbd80d7f1739e65`，工作区干净；Unity `2022.3.62f3c1`。本批结束保持该分支/HEAD，全部成果未提交。

- **请求与终态：**继续以 Voice 身份、Generation 和宿主 Audio 会话共同判断有效性，增加内部 `AudioRequestEnd`，统一 Completed / Stopped / Replaced / Unavailable / Failed / Shutdown 的 Voice 收尾。先失效、摘 Sound 计数，再复位/归还 Source，并在 finally 释放原责任；配置失败和 Tick 异常也结束自己的原代次。Music 尾轨与控制 epoch 继续属于原双声部内核。
- **窄资源基础：**新增内部 `AudioResourceLease`，只封装调用方明确交付的 `IDisposable` 责任与创建会话；经现有 Sound/Slot/Crossfade snapshot 入口，在 `Source.Play/onStarted` 之前绑定。校验失败、拒播、旧会话迟到提交和未绑定的重入中断由入口成对归还；已绑定责任由 Voice 全终态释放。先置 released 再调用清理，异常记录为可观察日志，不重试或复活请求。直接 Clip 路径不创建该对象、不 Destroy/Unload Clip；未接入 provider、命名加载或缓存，也未创建公共 pending/task 框架。
- **回调兼容：**onStarted 仍在 `Source.Play` 后同步通知；自停、替换、Shutdown 和抛异常均保持原返回句柄及代次隔离。onFinished 仍只用于自然完成，回调内原 Handle 仍有效；原资源转为局部责任，保活到自然通知及其 finally 结束，即便回调中 Stop、复用同一 Voice 或 Shutdown。Stop/取消/替换/失活不伪装自然完成。
- **释放重入：**StopAllSound 改为原 Handle 快照，不能被旧资源释放误停新代次；Music 控制先撤销旧过渡索引，再释放尾轨，释放后复核 epoch、原 Handle、最新开关和宿主。已用失败用例复现并修复“旧 Music false 设置投递误停释放中创建的新曲”和“Sound 抢占释放关闭开关后仍接纳新请求”。不重写预算算法。
- **Source 与数据：**已复现并修复外部失活/禁用循环 Source 仍留有效请求，以及播放中外部 `UnloadAudioData` 后请求滞留。Source/Clip/跟随目标失效走非自然终结；Failed 数据状态走失败收尾，Loading 不按 `!isPlaying` 判断完成，也不推进 fade/crossfade 时钟。保持 Unity `Play` 自行触发数据加载，不重复提交播放、不修改游戏 importer。
- **真实背景数据证据：**测试仅生成并删除自己的 120 秒 PCM WAV，使用非 preload、background、DecompressOnLoad 导入配置；当前 Editor 实际观察 `loadState=Loading` 且 `isPlaying=True`，之后 Loaded 并持续播放。保留同步通知是兼容选择，onStarted 表示播放已提交，不保证此刻可听见。该素材没有复现研究时推测的 Loading 提前自然结束；新增就绪检查是基于真实状态和官方契约的防护，不描述为已复现游戏故障。外部卸载后的非自然收尾、重新载入及 Stop 不卸载借用数据均有实际断言。
- **宿主/会话：**Audio 沿用 Foundation 的 `CanRunExecution` gate，不改其权威或共享 Root；初始化失败同步撤销部分订阅/池/宿主，保留原异常并允许干净重试。Audio 自己订阅 `Application.quitting`，无有效宿主时也能关闭设置。已释放/旧会话宿主的迟到 quit 不调用基类 gate 或关闭新会话。重复 Shutdown、同帧重建及旧设置/Handle 隔离沿用并回归。

**本轮实际执行：**通过连接的同一 Unity Editor 刷新、编译及 `tests-run`，没有第二个 Unity 实例、辅助 dotnet 编译替代或 Player 构建。以下数字使用实际通过数，不使用 MCP 返回的全工程 discovery `TotalTests` 作为执行数量。

| 定向集合 / 过滤器 | 本轮最终结果与范围 |
|---|---|
| EditMode `testNamespace=YLFramework.Tests.Audio` | **422 passed，0 failed，0 skipped**；包括 Cleanup、Admission/Budget、Core/Defaults、Fade/AutoFade/Crossfade、Mixer、组件/Help/Action 和新资源生命周期最小用例 |
| EditMode `testClass=AudioExitTests`（独立 `YLFramework.Tests.AudioSession`） | **1 passed**；真实单次 EnterPlayMode/ExitPlayMode，活跃资源在 Application.quitting 返回前释放一次，ExitingPlayMode 与释放中的重入均拒绝新播放/宿主。正常 Domain Reload，不改 EditorSettings 或原包 Logger；断言完成后仅恢复 fixture 的 Audio 静态状态，清理 reset 不作为退出证据 |
| PlayMode `testNamespace=YLFramework.Tests.Audio` | **156 passed，0 failed，0 skipped**；既有 154 项及真实短 Clip/资源释放/Source 销毁、背景数据 2 项；包含同步 Started、直接组件/PlaySoundAction、Pause/Mute/On-Off、Music 重播、fade/crossfade、Mixer与预热分配断言 |
| PlayMode `YLClickSoundPlayModeTests.ButtonClickInvokesFrameworkAudioEmitter` | **1 passed**；实际 StartPanel/Button 持久事件，点击显式 EveryOne 同帧重叠，面板失活不截断 one-shot |
| PlayMode `YLClickSoundPlayModeTests.DisablingMusicComponentDoesNotStopReplacementMusic` | **1 passed**；原组件的旧 lease 不停止他人替换后的 Music |

验证过程中修复了新 fixture 的本版本 importer API（`AudioImporterSampleSettings.preloadAudioData`）、真实退出后的 EditMode fixture 隔离及局部变量编译错误，修正后重新取得上述最终结果。全局 namespace 的游戏测试使用完整方法过滤器；MCP 的 class 过滤器不匹配此类无 namespace 类型，一次该调用超时，不计为测试通过。真实退出跨域会使 MCP 汇总 duration/count 不可靠，因此退出单独运行并使用具名用例的 Passed 证据。新 `.meta` 由 Unity 导入生成，`git diff --check` 通过；独立只读审查的两处释放重入发现已用红绿回归闭合。

**兼容与未覆盖范围：**未改 AudioHandle 的公开语义/签名、组件字段/GUID/程序集/UnityEvent、AudioEmitter/AudioMusicPlayer/PlaySoundAction 代码、游戏业务/Prefab/Scene、六个设置键、Root/Action 或其他模块；不引入 timeScale 自动暂停。没有实际 API/用法变化，未新增或重写 Audio 使用说明。Core Audio 与原 XF/XFAB/XFFSM Package 保持不变。

`AudioDataLoadState.Failed` 的真实损坏/解码失败素材尚未构造；初始化失败使用局部缺失设置属性的受控注入，而非声称真实 Root 已发生故障。背景 PCM 不证明压缩/Streaming、所有平台或实际听感；本轮没有实际听感、Player、原游戏完整战斗流程、4.3 provider/资源物理释放、4.5 关闭 Domain Reload 双 Play 门禁。释放函数抛异常后只能保证调用恰一次及状态终结，不能保证外部后端物理释放已成功。

**4.1 技术结论：**本批生命周期、内部全终态资源责任、退出和初始化安全基础及必要消费者回归满足 4.1 技术完成条件，具备进入 4.2 的前置基础；尚待用户验收及 Git 收口。到此停止，不自动实施 4.2，不创建 Commit/Push/Merge/Tag/Reset/Clean，不宣布整个阶段 4 COMPLETE。

### 4.2 命名控制通道、Source 借用与局部暂停

- **范围/产物：**正式音频域的命名通道/局部控制、稳定持久化身份和受控 Source 接入；吸收 XF 任意独立循环、Volume/Mute 与 AutoPause 用途。Foundation Pool/Events 不重写，缺省三类的设置/预算/路由不变。
- **EditMode：**登记冲突、设置隔离与非法参数无副作用；组控制只作用原会话；移植 Core 的“不写原 XF 键、不毁借入 Source”契约。新临时实例不生成持久化垃圾键。
- **PlayMode 门禁：**两个独立命名循环与缺省 BGM 同时运行，分别 Stop/调音量不串组；借源不会重挂/停用/销毁外部对象，Source 销毁及重复借用明确结束/拒绝；回调中换组/停止/Shutdown 后不恢复旧字段。timeScale 自动暂停仅作用 opt-in 原租约，不解除手动 Pause；原游戏暂停行为保持。

### 4.3 命名加载、异步取消与资源后端过渡

- **范围/产物：**Audio 专用 provider/请求、现有 YL ResLoader 的窄接入、显式素材缓存，以及独立可选 XFAB adapter。所有 acquired Clip 走同一播放内核，直接 Clip 仍只借用；不修改 Resource/XFAB 全系统。
- **EditMode/受控时序：**用一个最小可控 provider 覆盖同步完成、迟到、失败和永久 pending；验证 A慢/B快、单等待者取消、Stop/Shutdown/换会话、不完成 producer 的逻辑退出。资源释放在所有终态恰一次，失败不污染接纳/去重/旧 BGM；不建大型 Mock 平台。
- **PlayMode 门禁：**真实 Resources/YL ResLoader 的同步/异步 AudioClip，加载等待/取消/重试；disabled Music、双轨尾曲、暂停与组件失活 one-shot 的保活；idle 淘汰/重取/关闭后成对释放。背景加载、非 preload、压缩/Streaming 用少量受控素材实际验证，不改游戏 importer；不能以 PCM 或 Assets 模拟证明全部数据路径。
- **后端证据：**复用现有资源测试的最小独立 Bundle 构建/清理模式验证受控 YL provider 的真实 AB 保活/归还，不重建 MainModule，不把对象清引用当物理释放。XFAB 只验证借用加载与逻辑取消不影响其他使用者；若原后端挂起/产物阻塞，分类报告，不能修 Package或虚称物理取消通过。

### 4.4 正式组件/Editor 接入与 Core 旧机制退出

- **范围/产物：**正式素材组/命名入口与必要 Inspector、局部预设/示例和说明；现有 AudioEmitter/MusicPlayer 身份与默认值保留。迁完 Core 音频的有价值测试后退出四份 Runtime/旧 Inspector/meta，不留重复注册表、驱动、根或卸载协程。
- **门禁：**素材组只抽选一次、空/坏组不截断旧播放；新自动播放模式显式开启，旧 Start/EveryOne/Linear 序列化兼容保持。自有资产保存重开、有效字段/UnityEvent、必要 Undo/Prefab override 与多对象 Inspector验证；定向 GUID/反射/帮助/XML/程序集复核无旧自有 Audio 残留。
- **消费者/依赖：**运行现有 YLAudioWiringTests、YLClickSoundPlayModeTests 与有影响的 Help/Action tests；Core/Runtime 互不引用、原 XF 类型仍可用。可选后端未装时正式 Audio 不要求 XFAB；限定 Audio 必要闭包的无 XF/XFAB 编译验证复用现有 AssemblyBuilder 隔离模式，不把引用已编译 Runtime DLL 的示例当源码独立证明。
- **退出条件：**能力未接入或身份迁移未验证时不先删旧测试/类型；失败停止对应退出，不自动做 Git 回退、恢复整个工作树或加永久兼容壳。

### 4.5 实际播放、原游戏非干扰与阶段收口

- **定向回归：**复用 `T/EditMode/Audio`、`T/PlayMode/Audio` 对实际改变的 Cleanup/Started/Playback/Crossfade/AutoFade/Budget/Mixer/组件契约；跨模块只选直接 Root/Action/Pool/Events/UI 消费者，不逐批全量重跑 Foundation/Resource/Core。
- **真实双 Play：**关闭 Domain Reload、正常 Scene Reload 的同一脚本域连续两轮；保留旧句柄、通道、pending、settings token 和资源租约到第二轮，证明旧身份无效且不改新请求。真正退出通知使在途终结并拒绝建源/新请求，不以反射 Reset 或 Coroutine finally 代替。原 Logger/MCP 已知干扰需要隔离时另行授权，不沿用阶段 3 的单次特批。
- **原游戏 Editor：**既有 MainStartSettingsGameExitAndReentryUseFrameworkAudio、真实 Button 点击音、持久 Slider 与菜单/设置/进入/暂停返回/再入/结算；补代表性业务 Controller 战斗素材取得/播放，以及既有 Music 音量旧键迁移的保持性验证。只在自有 fixture 加窄探针，原游戏源码/测试/资产不迁移；快照并恢复相关键及运行设置。
- **真实声音：**PlayMode 用可识别 PCM 信号与真实输出/时间推进核验非静音、mute、pause、fade/crossfade 和 3D；再做菜单 BGM、点击、代表性战斗音效与音乐切换的实际听感检查。Source.isPlaying、数学增益、非零输出采样、OS 可听见各自是不同证据；听感检查不由自动测试冒认。
- **Player 门禁：**一次当前源码的 Windows64/Mono Development 构建及实际运行，覆盖正式组件/命名组/资源 sync+async、取消/尾轨保活/池复用/退出、真实 Mixer与音频输出；原游戏 Player 使用匹配当前脚本身份的既有 MainModule 产物完成上述代表性流程。构建成功、运行成功与听感分别报告；没有匹配产物/音频设备时保留阻塞，不自动重建 XFAB或使用无声 batchmode 冒认声音通过。
- **完成条件：**新 YL 唯一音频权威，双方独立价值已接入或按本文明确边界收敛；Core 副本退出，游戏契约/设置/资产/资源链未迁移且非干扰通过；async/取消/重入/场景/释放和必要 Player 门禁有真实证据，文档/来源/分发边界一致。未满足必要门禁不宣布阶段 COMPLETE。

当前阶段 3 的 Player 产物前提为 MainModule/StandaloneWindows64/1.0.0，用户恢复版时间 `2026/10/9 19:03:49`；实施门禁前重新定向核对来源/完整性，不能沿用本轮未读取的本地 Bundle 状态。IL2CPP/AOT、移动设备、DSP 精确时序、同时关闭 Scene Reload、全框架无原 15 系统的独立运行和完整 XFAB 物理释放不由本阶段的 Mono/Audio 结果代替，留对应后续专题。

## 8. 备选、风险与审核项

| 架构方案 | 价值/代价与结论 |
|---|---|
| 两套管理器互包装 | 身份改动少，但重复 Source/设置/生命周期/资源权威不消失；不符合融合完成标准，不采用 |
| XF Controller 为唯一内核 | 命名/借源入口直接，但须重建 YL 已成熟的租约、复用、预算、Mixer与过渡，且引入 XFAB；源码证据不支持其成为基底，不推荐 |
| 唯一域留现有 Runtime，吸收双方并替换必要内部实现 | 保护真实游戏身份和程序集方向，避免为无消费者 Core API 留壳；仍须完成命名/借源/异步/资源责任而非简单删除 XF。D1 已确认，推荐实施 |
| 新独立 Audio asmdef | 模块隔离更细，但游戏直接引用 Runtime、UnityEvent 保存 Runtime 组件身份，直接搬全套会破坏契约；需另建有实质功能的入口分层，当前成本无必要，不采用 |

**主要风险：**把借用当取得所有权；开始/完成回调重入在资源责任提交前发生；慢加载覆盖新曲；尾轨或 disabled 请求被提前卸载；借源复位误改调用方对象；游戏缺字段默认值与 UnityEvent 程序集断链；退出阶段静态/协程残留；同工程原包掩盖新 Audio 的可选依赖；当前依赖/Bundle/音频设备不满足复现条件。风险分别落在 4.1～4.5，不用“全量测试通过”替代逐项证据。

**已确认与已审核：**D1（唯一 Runtime Audio，吸收价值后退出自有 Core.Audio*，不删原包/Root）与 D2（直接 Clip 借用、受控资源租约、XFAB 不自动卸载共享资产）已通过用户 question 确认。整体方案、五批路线及明确的借源独占/可选缓存/数据就绪和实际声音门禁已审核通过；4.1 已获得单独实施授权并完成上述技术批次，不代表 Git 或后续批次授权。没有额外跨阶段前置融合需要用户批准；如实施发现必须改变游戏身份、全局 Root/程序集方向、XFAB 资源所有权或重建游戏产物，则停止该分支并提交新证据与最小替代方案。

**总体方案研究轮的历史边界：**当时仅交付本文，不同步非计划文档、不实施 4.1、不创建分支、不执行 Git 写入。后续已单独授权的 4.1 实施与停止边界见第 7 节实际成果；其余批次仍需用户验收前批及单独授权。
