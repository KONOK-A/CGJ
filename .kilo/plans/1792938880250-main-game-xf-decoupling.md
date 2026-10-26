# 主游戏 XF 解耦实施计划

> 源码/资源解耦及双向独立载体验证已完成；后续设置面板、48 路分组及素材校准的提交前修正已在主工程复验，主工程 MainModule 的 Win64 资源包已重建并同步 StreamingAssets。旧独立载体/Player 未重建。本批 Git 交付范围见文末，实际提交 Hash 与同步结果以 Git 历史和交付回复为准。未实际剥离游戏；不将自动化/DSP 结果宣称为人工主观听感结论。

**Goal:** 主游戏的运行时代码、资源和自有测试不再依赖 YLFramework，只使用现有 XF 框架与必要 Unity/第三方依赖；同时清除 YL 对主游戏的活动反向依赖，为之后分别迁出游戏和保留 YL 开发工程做好准备。

**Architecture:** 保留游戏自己的 `AudioController`，实际播放、音源创建和播放资源引用交给 XF；游戏只增加有界的战斗音效接纳策略。BGM 使用 XF 组件的 owner-scoped 播放/停止，UI 和失败提示走独立于战斗预算的 XF Sound 控制器。游戏回归归游戏，YL 兼容性和组件回归使用 YL 自有夹具。

**Tech Stack:** Unity `2022.3.62f3c1`；XFGameFramework `1.18.8 @ 2182708aecc596f30739fa8634fe2b9b6946ddd7`；XFABManager `1.24.0 @ c35dc4058ecd831409199b43cb328024202d4557`；现有 XFFSM/Overlap；Unity Test Framework `1.3.9`。本批独立 Player 验收限定 Windows64 Mono，不外推 IL2CPP/WebGL/移动平台。

**Spec:** 本轮用户已确认：先解耦及独立验证，暂不剥离；以 XF 原生为准但解决战斗音效洪峰；迁移已有偏好。初始采用 24 路、每音效 4 路，随后修订为同名 8 路。根据混战中敌我枪声去重和爆炸争抢的证据，用户最终选择分来源预算并扩容：Explosion 16、PlayerWeapon 16、EnemyWeapon 12、Feedback 4，共 48 路；Unity 真实声道 64、虚拟声道 512。80ms 节流与普通/重要两级优先级保留，当前完整规则如下，历史验证不追溯改写。

## 范围与约束

- 本批不从主工程删除/搬走游戏，不卸载 XF，不创建正式接收游戏工程；临时独立验收载体不是正式迁出产物。
- 不复制 YL Audio 到游戏，不新增通用音频框架，不修改 `Library/PackageCache`，不升级 Unity/包，不手改生成的 csproj/sln/锁文件。
- 保持玩法、XF MVC/FSM、XFAB 资源名、Clip、UI 布局和非音频 UnityEvent 不变。只调整音频接线、必要业务优先级和测试归属。
- 保护现有工作区改动，尤其 FSM 资产、第三方插件迁移、已有交付脚本/导出测试和未跟踪夹具。`Assets/Test/` 是用户未跟踪试验内容，本批不编辑、不删除，也不纳入主游戏独立载体。
- 修改前重查状态；有目标场景/Prefab 未保存或与本批直接冲突的改动时先确认，不替用户保存或覆盖。移动脚本/资产保留 `.meta`，新增资产由 Unity 生成新身份。
- 新 Bundle 和独立工程的包解析/构建只在本次临时载体内执行，不能清理或覆盖主工程既有 Bundle、缓存、Product/Company 和玩家偏好。
- 不修改权限、AGENTS、忽略规则；不创建/切换分支、提交或推送，除非用户另行明确授权。
- 使用现有主工程 Editor，禁止对同一 projectPath 启动第二实例；独立载体按不同 projectPath 串行验证，不通过强制关闭用户 Editor 取得环境。

路径约定：下列游戏相对路径根为 `Assets/Games/MainModule/`，YL 相对路径根为 `Assets/YLFramework/`。

## 事实基线

- 正式游戏的直接 YL 代码接入集中在 `MainModule.cs`、`Scripts/Controller/AudioController.cs`、`FSM/GameState/Gaming.cs`、`UI/StartPanel/StartPanel.cs`、`UI/GameOverPanel/GameOverPanel.cs`，以及三个游戏 asmdef/音频测试。
- 七个面板里的 AudioEmitter 是嵌套 `Art/Prefabs/ClickPlayer.prefab` 的 **stripped 引用**，不是另一套独立组件。15 处 Button 持久事件依赖该引用；不能重复添加播放器或破坏源 Prefab/fileID 关系。
- YL AudioEmitter GUID 为 `e0f9382a5a3344d4a979ae028b52cb9c`；AudioMusicPlayer GUID 为 `a02644b3809d0e34aa3ad1a21593f647`。XF AudioPlayer GUID 为 `403c24cad33f2944ead6f9acd0524ea2`。
- XF API 的真实拼写为 `AudioPlayer.GetAuidoController`；没有 `AudioConfig` 类型。注册第三参数是默认音量，应为 `1f`，不能照旧文档传 `0`。
- XF 的单 Sound AudioSource/PlayOneShot 没有逐音效句柄、并发统计、去重或抢占。战斗必须使用有限的非 OneShot 播放槽，不能把请求计数或 `clip.length` 延时冒充真实并发。
- XF AudioPlayer 无 OnDisable 清理，自动播放发生于每次 OnEnable；Music 组件与静态 Play 混用可能留下旧 owner。BGM 本批统一组件路径，删除无调用方的游戏 `PlayBGM` 方法，不保留无必要兼容入口。
- `YLFramework.UI.Acceptance.asmdef` 及两个 game acceptance flow 编译期引用游戏；`AudioComponentInspectorTests` 有四个用例读取游戏资产；`OriginalGameNonInterferenceTests` 默认运行游戏流程。`FrameworkVisibilityTests` 自身不依赖游戏，不应移走。
- 当前 manifest/lock 的 URP、UGUI 声明与 PackageCache 目录版本不一致。实施预检以 Unity `PackageInfo.GetAllRegisteredPackages()` 和真实编译图确认实际版本；独立载体记录并锁定已确认版本，不从目录名猜测，不顺手修主工程包声明。

## 运行时契约

### XF 控制器与所有权

- 在游戏 `AudioController.OnInit` 完成偏好迁移后，注册 `Music`（loop=true）、`Sound`（loop=false）及固定 48 个 `MainModule.Combat.00` 至 `.47` 控制器，默认音量均为 `1f`；早于首个面板加载。不创建 `.48`。
- 使用 XF 创建的 AudioSource，战斗槽均为 2D、非循环；每槽用 `AudioPlayer.Play(name, clip, false)`，只允许一个当前 Clip，不用 PlayOneShot 叠加。
- 保留 `MusicVolume`、`SoundVolume`；`PlaySound(string assetName, SoundPriority priority = SoundPriority.Normal, SoundGroup group = SoundGroup.Explosion, int emitterId = 0)` 为战斗入口。两值 `SoundPriority` 和四值 `SoundGroup` 均在游戏 `AudioController` 中定义，不引入框架级类型；现有默认爆炸调用仍适用。
- 公开常量 `MusicName = "Music"`、`SoundName = "Sound"` 统一游戏 C# 引用。私有五参 `TryPlayCombatSound(string assetName, SoundPriority priority, double now, SoundGroup group, int emitterId)` 返回是否接纳；外层传 `Time.unscaledTimeAsDouble`。时间参数用于确定性回归，可沿用现有测试的反射方式验证，不增加公开测试 API/诊断平台。
- 玩家枪声按 `(PlayerWeapon, assetName, 武器实例ID)` 去重；其他组按 `(group, assetName, 0)` 去重，敌人同名枪声跨实例聚合，但不吞玩家同名枪声。同名并发额度按组内 `assetName` 统计，不按 emitter 扩大额度；保留原资源命名。
- Sound 音量/静音变化通过 XF 回调同步所有战斗槽的 `AudioSource.volume/mute`。不写各槽的 `Volume/Mute`，避免生成 48 套持久化偏好或播放时频繁 Save。
- 只释放自己注册的控制器，正常退出和初始化失败均解除自己的订阅、Stop(All)、Remove，清空槽状态与节流记录；OnExit 幂等，不销毁 XF 共享 AudioManager 根。同名非本所有者控制器冲突须明确报错，不静默借用或删除。
- 保持 XF 原生暂停行为：Time.timeScale=0 不等于暂停声音。本批不增加音频暂停协议或支持外部逐槽 Pause；保留菜单/暂停 UI 可发声。

### 战斗接纳与抢占

| 来源组 | 固定槽 | 组预算 | 同名上限 | Source priority（Normal / Important） |
|---|---|---|---|---|
| Explosion | `.00` 至 `.15` | 16 | 8 | 128 / 80 |
| PlayerWeapon | `.16` 至 `.31` | 16 | 8 | 64 / 64 |
| EnemyWeapon | `.32` 至 `.43` | 12 | 6 | 192 / 144 |
| Feedback | `.44` 至 `.47` | 4 | 4 | 16 / 16 |

1. 同一去重键距离上次成功接纳不足 `0.080` 秒时拒播。Important 可越过上次 Normal 的窗口；Important 对 Important、Normal 对任何优先级仍限流。拒播不滑动窗口。播放请求中最多每秒清理一次已过 80ms 的历史记录，空闲时不另建更新器，避免历史武器实例不断累积。
2. 同名额度已满时只能在本组同名声音中替换最低优先级、同级最旧者，不能借空闲槽突破上限。枪组 Normal 可以替换 Normal 旧尾音以保留新攻击瞬态，但不能抢 Important；非枪组 Normal 满额拒播。
3. 未触及同名上限时优先取本组空闲槽；本组满额时枪组 Normal 可替换本组最旧 Normal，Explosion/Feedback Normal 拒播。Important 仅在本组抢占最低优先级、同级最旧者，全部为 Important 时替换最旧 Important。各组不借用或抢占其他组预算。
4. 不排队、不延迟补播。先确定候选并成功加载 Clip，再停止旧槽；加载失败不得打断旧声音、占用额度或写入节流记录。播放异常不能伪报成功，不宽泛吞异常。
5. 槽复用前调用 XF Stop(All) 清理其旧 AudioData/资源引用，再非 OneShot Play；成功后提交优先级、接纳顺序和节流记录。用真实 AudioSource.isPlaying 判断空闲，不用 XF 延迟约一秒的完成计数，也不长期缓存 AB Clip/手动卸载共享音频。

来源落点：四种枪通过 `WeaponBase.PlayShotSound` 转发缓存的 group/emitter；`OnLoaded` 检查父级 `EnemyBase` 且 `enemy.weapon == this`，否则作为玩家武器。不在每次射击搜索父链，不依赖仅敌人调用的 `Init` 或 `bulletName` 猜归属。`EnemyBase` 死亡保持 Normal Explosion；`SnakeBase.OnFallOff` 为 Important Feedback；两种 RocketShell 的爆炸为 Important Explosion。原本不存在的声音不新增。

GameOver 失败提示及按钮走独立 XF `Sound` PlayOneShot，不占战斗 48 路和 80ms 额度；BGM 独立。Music priority=24、Sound priority=32；数字越小越重要。`ProjectSettings/AudioManager.asset` 真实声道从 32 调至 64，虚拟声道保留 512，不改包或音频文件。重要音效和旧尾音仍可能被组内抢占，不保证无限事件完整播放；Sound 的多个 OneShot 也不能按一个控制器只占一条真实 voice 估算。

### 偏好迁移

在首次初始化且迁移标记 `MainModule.Audio.XFPreferencesMigrated.v1` 不存在时，按每个目标键独立迁移，已有 XF 值（包括音量 0）优先：

| 旧值 | XF 目标 | 转换 |
|---|---|---|
| `KEY_AUDIO_MANAGER_MUSIC_VOLUME` | `AudioConfigVolume:Music` | 有限浮点值限制到 [0,1] |
| `KEY_AUDIO_MANAGER_SOUND_VOLUME` | `AudioConfigVolume:Sound` | 同上 |
| `KEY_AUDIO_MANAGER_MUSIC_ON` | `AudioConfigMute:Music` | 合法 0/1 取反，off -> mute |
| `KEY_AUDIO_MANAGER_SOUND_ON` | `AudioConfigMute:Sound` | 同上 |

- 缺失或无效旧值不覆盖目标；无目标时由 XF 默认音量 1、Mute=false 生效。迁移完设置标记并保存一次。
- 不删除或持续同步旧键；保留它们给 YL 开发自测。不迁移 Voice，不引入 YL 类型引用；静音以后遵循 XF Mute，不复制 YL off 的停播/拒播语义。
- 删除原来会将 XF Music 迁到 YL 并删除 XF 键的逻辑。测试完整保存/恢复四个旧键、四个目标键和标记，不能修改真实玩家偏好后不恢复。

## Review Focus

- 普通声音饱和、同片段已满和升级为 Important 时，48 路分组预算、同名 8/8/6/4 上限与组内抢占同时成立；敌我同名枪声和玩家不同武器不互相去重，枪声保新瞬态但不能抢 Important。由音频压力和真实 XF 射击/复用用例覆盖。
- 旧面板/旧场景稍晚停用或销毁时不能误停新 BGM；由任务二 owner 与重复进入流程覆盖。
- 共享 ClickPlayer 的 nested prefab、stripped fileID 和 15 个事件在迁移/重导入后仍有效；由任务二接线与依赖闭包检查覆盖。
- 现有 XF 音量 0、旧 off、非法旧值、第二次启动以及 YL 后续改偏好不会污染游戏；由任务一迁移用例覆盖。
- 删除源代码引用不代表独立运行：真实 Bundle、编译图、DLL、共享 Resources 与平台包必须无 YL 残留；由任务四干净载体验证覆盖。

## 任务一：XF 音频包装、偏好与战斗预算

下列任务一至四保留初次解耦的实施步骤；其中 24 路历史额度与独立载体验收不代表后续 48/64 版本。当前规则以上方运行时契约为准，各次实际结果见文末分批记录。

**Files:** 修改 `Assets/Games/MainModule/MainModule.cs`、`Scripts/Controller/AudioController.cs`、`Views/Player/SnakeBase.cs`、`Views/Enemy/Bullet/EnemyRocketShell.cs`、`Views/Player/Equip/Weapon/Bullet/PlayerRocketShell.cs`；新增 `Tests/PlayMode/GameAudioControllerTests.cs` 及必要 `.meta`。

- [ ] 记录分支、实际 diff、受影响资产身份及 Unity/UPM 实际版本；预读有效约定。先添加在现状可编译并明确失败的 XF 注册/接纳/迁移回归，再实施，记录失败原因而非仅看到编译失败。
- [ ] 按上述契约实现游戏 AudioController，必要的槽/接纳记录留在该文件，固定容量、无高频 LINQ/临时集合，无自建 MonoBehaviour 音频内核。删除 MainModule 的 YL alias 和全局 10 帧设置，保持 LoadController 顺序。
- [ ] 更新三个业务文件的 Important 调用；其他现有 `PlaySound("...")` 调用保持不变。
- [ ] 验证 1000 次同音效同帧请求只接纳一次；精确 80ms 边界可重播；相同非缩放时间在不同模拟 FPS 下结果相同；被拒请求不延长窗口；普通到重要可升级、连续重要仍节流。
- [ ] 验证同片段 8 路、24 路饱和、普通拒播、重要抢占最低优先级最旧者、全重要时替换最旧者；同片段满额必须同片段内替换。包含“很多相同普通声音后来了稀有重要音效”和相同 Clip 名玩家反馈升级的用例。
- [ ] 验证加载失败不抢占、不消耗节流；复用槽先清理 XF 数据且不增长音源数量；Sound 音量/Mute 对正在播放槽立即生效，无每槽偏好写入。依赖 isPlaying 的测试使用有效音频上下文，不能以无声 headless 结果充当通过。
- [ ] 验证迁移优先级/0/静音取反/无效值/标记/旧值不删除；退出后控制器与订阅全部清理，可在同一 Play 会话重建，无错误地清理别人的同名控制器。

**验收:** 新的音频控制器 PlayMode 用例通过；此时不把仍未迁移的 Prefab/旧测试称为整体完成。

## 任务二：资源及 BGM/UI 接线改回 XF

**Files:** `Art/Prefabs/ClickPlayer.prefab`、`Art/Prefabs/3DAudioPlayer.prefab`、七个 UI Prefab、`Scenes/Game.unity`；`FSM/GameState/Gaming.cs`、`UI/StartPanel/StartPanel.cs`、`UI/GameOverPanel/GameOverPanel.cs`；将 `Tests/EditMode/YLAudioWiringTests.cs` 连同 meta 改名为 `GameAudioWiringTests.cs`。

| 资源 | XF 配置 |
|---|---|
| ClickPlayer | 原位置改 XF AudioPlayer；不加 AudioSource；controllerName=Sound，audioAssetType=1（AudioClip），AssetNames=[]；Clips 的 Default 组保留两个原 Clip，playType=1（PlayOneShot），playOnAwake=false |
| StartPanel/Bgm | 改 XF AudioPlayer；不加 AudioSource；controllerName=Music，同样使用 Clips/AudioClip 模式；原单 Clip，playType=0，playOnAwake=true |
| Game/Virtual Camera | 同上，playOnAwake=false；Gaming 获取 XF AudioPlayer 后显式 Play |
| 3DAudioPlayer | 保留现有模板身份，改 XF AudioPlayer + 同对象 AudioSource；Sound、AudioClip 模式、空组不自动播放，spatialBlend=1、Linear、min/max=1/50、loop=false；不新增动态 3D 功能 |

- [ ] 先将接线测试期望改为 XF，确认现状失败；记录原 prefab GUID、源组件 fileID、Clip GUID 和事件顺序。
- [ ] 优先使用 Unity API；为保持组件 fileID 必须定向修改 YAML 时，只改对应组件脚本/字段、stripped 脚本和音频 AQN，保留 prefab GUID、nested 对应关系和所有无关数据；导入、重开并验证。
- [ ] 七面板的 15 个事件改为 `XFGameFramework.AudioPlayer, XFGameFramework` 的无参 Play。数量保持 Start=4、Pause=4、GameOver=2、GameSetting=2、Description=1、GamePanel=1、Setting=1，不触碰其他按钮业务 AQN。
- [ ] StartPanel 增加私有序列化 `musicPlayer`，绑定原 Bgm 组件；切到游戏设置前及面板 OnDisable 做 `Stop(StopType.Play)`，保留必要 base 生命周期。Gaming 的 OnExit 同样 owner-scoped Stop。不得对旧组件使用无参全局 Stop，也不将 Music 组件与静态 Play 混用。
- [ ] GameOverPanel 移除 YL Options/AudioKit，保留原资源加载，交给 XF `AudioPlayer.Play(AudioController.SoundName, clip, true)`，绕过战斗预算。
- [ ] 检查大小写敏感的 XF `Clips`/`AssetNames`/组字段、默认控制器名和循环来源；确认没有 Missing Script、空事件目标、残留 YL 脚本 GUID 或额外重复播放器。
- [ ] 运行 GameAudioWiringTests：组件类型、原 Clip 身份、15 个 Play、共享 prefab 实例关系、Music/3D 参数和重导入后引用成立。运行两轮菜单->设置->游戏->暂停->菜单->重进及失败流程，验证旧 BGM 停止不影响新 owner，关闭 UI 不截断共享 one-shot。

**验收:** 游戏功能音频走 XF；所有原游戏音频资源不再引用 YL，原 UI/场景非音频引用和已有 FSM 修改保持不动。

## 任务三：测试分家并切断双向依赖

**游戏侧文件:** 三个 `Project.Game.MainModule*.asmdef`；原 `Tests/PlayMode/YLAudioGameFlowPlayModeTests.cs`、`YLClickSoundPlayModeTests.cs`；新增 `Tests/EditMode/GameIndependenceTests.cs` 和 `Tests/PlayMode/GameClickSoundPlayModeTests.cs`。

**YL 侧文件:** `Tests/EditMode/Editor/Audio/AudioComponentInspectorTests.cs`、`Tests/EditMode/Editor/Core/CoreSerializedPropertyTests.cs`、`Tests/EditMode/Editor/Distribution/FrameworkAssemblyIndependenceTests.cs`；`Tests/Acceptance/Core/YLFramework.UI.Acceptance.asmdef`、三个 AcceptanceEntry、两个 GameAcceptanceFlow；`Tests/PlayMode/Systems/Behavior/OriginalGameNonInterferenceTests.cs`；新增自有音频夹具及迁入的组件生命周期用例。

- [ ] 先建立边界失败用例。游戏检查实际 Editor/Player 编译引用图、已编译 AssemblyRef 闭包和 `AssetDatabase.GetDependencies`（游戏全部资源、Main、必要共享 Resources），不能有 YL 程序集、资产、MonoScript 或 DLL。第三方编译器可用的全局插件输入不等于其真实 AssemblyRef，必要性由实际二进制闭包和干净载体核对。禁止以对所有文本搜索零次为标准，边界测试的禁止字符串不是实际依赖。
- [ ] 游戏主流程测试保留/改名为 `GameAudioFlowPlayModeTests`，meta 保留，改为断言 XF 控制器音源、Clip、实际播放/停止、滑条、再进入和清理，不伪造 YL handle/SoundCount。保留 SnakeInputControlTests、GameViewLoadingTests。
- [ ] 将 `YLClickSoundPlayModeTests.cs` 连同 meta 移至 YL `Tests/PlayMode/Systems/Audio/AudioEmitterLifecycleTests.cs`，重命名测试类并移除游戏/XF 使用，保留纯 YL 组件重叠、循环、禁用/owner 回归；原游戏按钮用例重写至游戏 GameClickSoundPlayModeTests，核对共享 Sound one-shot 配置及停用行为。已有等价 YL 覆盖可整合，不能直接删有效断言或 Ignore 制造通过。
- [ ] 将 OriginalGameNonInterferenceTests 的两轮游戏/Overlap 实际自动查询断言并入游戏回归；不再从 YL 反射调用游戏流程。游戏测试若直接使用 Overlap，则显式引用已有 XFGameFramework.Overlap。
- [ ] 为 YL 增加 `Tests/Fixtures/Audio/LegacyEmitter2D.prefab`、`LegacyEmitter3D.prefab`、`LegacyMusicPlayer.prefab` 及 meta；不引用游戏 Clip/Prefab。用 Unity 创建自身身份后，仅对这些夹具定向去掉待验证的新序列化字段并重导入，真实模拟旧格式。不能用 Reset 后新组件替代“字段缺失”契约。
- [ ] AudioComponentInspectorTests 的四个游戏资产用例改用上述夹具，保留旧显式模式、旧音乐 Linear/无 crossfade、实例 Override/源资产不变等断言；验证夹具中目标新字段确实缺失。
- [ ] YL 保留 UIAcceptance、AudioAcceptance、RuntimeAcceptance 类型/自身场景和默认 YL 流程；删除它们对游戏 flow 的调用、gameMode 字段及 `-ui34-game`/`-audio45-game`/`-runtime55-game` 联合模式。退休两个混合 GameAcceptanceFlow 脚本及 meta，游戏功能回归已归属游戏；YL UI 借用外部 EventSystem 等通用契约继续使用自有夹具验证。
- [ ] 同步 CoreSerializedPropertyTests：移除已退休 GameMode 的字段/旧字段名契约，保留 colorPrefab、serverUrl、其旧名称迁移、GUID/fileID/meta 稳定性等有效覆盖；不保留空壳字段/反射回退。
- [ ] 从 YLFramework.UI.Acceptance 引用中移除 Project.Game.MainModule、XFGameFramework、XFFSM；增加该 Acceptance 程序集无游戏/XF 引用的边界断言。FrameworkVisibilityTests 保持原职责。
- [ ] 从三个游戏 asmdef 移除 YLFramework.Runtime，保留两个测试程序集已有的 XFGameFramework 引用及必要 Unity TestRunner/NUnit 设置；新增直接类型使用需要的 XF 引用必须显式声明。游戏测试无 YL 类型、反射桥和隐藏加载。YL 的资产/默认测试不再读主游戏或 MainModule Bundle。不误删拒绝路径负例、保护/快照路径和历史文档。

**验收:** 主工程 Unity 编译无新增错误；游戏两个测试程序集全部通过；受影响 YL 音频组件/旧序列化/Acceptance/边界用例通过。XF 本身的共存测试仍允许存在，本批不卸 XF，也不宣称默认 YL 测试已能在无 XF 环境全跑。

## 任务四：干净载体验证与收口

- [ ] 在 `C:\Users\Kano\AppData\Local\Temp\kilo\game-xf-decoupling-<RunId>\` 建立本批独占的 `GameNoYL`、`YLNoGame`、Evidence；不复制 Library、Temp、obj、主工程旧 Bundle、未跟踪试验内容或预编译的游戏/YL 程序集。
- [ ] GameNoYL 收入游戏/Main 场景、原始 meta、必要 XF Resources/XFAB 配置，以及从真实资源闭包和 ProjectSettings 解析出的渲染/材质/图集/字体等共享依赖；不复制整个主 Assets/Plugins 或 YL 目录充数。保留 Tag/Layer/Input/Physics 等游戏设置，独立 Company/Product 与缓存路径隔离真实玩家数据。
- [ ] 使用确认过的 Unity 版本、XF 固定 revision 和必要 Unity 包。XFFSM 固定 `b06333c2cdc801fcc401f14c252853bf099aa1b7`，Overlap 固定 `4961d8358119485c94d6f0f74b6ca5d8b4163460`；XFAB 的 Newtonsoft `3.2.1` 来自真实 UPM 依赖，不从 YL 或全局 NuGet DLL 偷渡。由 UPM 正常生成锁文件并检查解析结果；本机实际注册版本与声明差异只在载体明确处理/记录，不改主工程。
- [ ] GameNoYL 从空 Library 导入、编译，运行两个完整游戏测试程序集。编辑器 Assets 模式通过后，只在载体用现有 XFAB 构建新的 MainModule Bundle，使用 LOCAL/Bundle 的真实 Player 路径构建并启动 Windows64 Mono，不借主工程历史 Bundle/缓存成功。
- [ ] 独立 Player 验证菜单/点击/音量/进入/暂停/返回/重进/失败；检查实际程序集/构建产物无 YL DLL、Missing Script 或反射桥。高密度战斗确认槽总数<=24、单音效<=8、新重要反馈可抢占；听感验收不能由源码或无声测试替代，听感参数微调若超出已定规则须说明。
- [ ] YLNoGame 不含 Assets/Games、Main.unity、游戏 DLL 或 MainModule Bundle；保留本批未授权移除的 XF 包和必要共存 Resources。干净导入编译，执行迁入的 YL 音频 PlayMode、AudioComponentInspectorTests、CoreSerializedPropertyTests、FrameworkAssemblyIndependenceTests、FrameworkVisibilityTests、AudioAcceptanceResourceTests 及默认 UI/Audio acceptance。Runtime 默认 HTTP 流程不作无关重验，但其类型/序列化/编译契约必须通过。
- [ ] 复用现有导出门禁检查默认包不包含游戏，YLNoGame 中不存在游戏也不会使默认导出构建失败。不要为了此次任务改写用户已有 ExportUnityPackageTests/交付脚本的无关改动或运行会覆盖其夹具的整套历史验收。
- [ ] 更新现有 `Documentation/Modules/Audio.md` 第 891 行附近当前游戏接线说明，以及 `Documentation/IntegrationPlan.md` 的当前依赖/验收入口说明和本计划验收记录，注明游戏联合模式已退休；不改写历史阶段结论，不新建重复过程报告。记录真实 Unity 测试结果、载体版本/Bundle 身份、Player 日志及未验证项。
- [ ] 复查实际 diff、.meta、源资源 GUID/fileID、无关工作区改动与生成物范围；独立 Review 检查重要音效抢占、原生 XF owner、偏好和干净闭包。不能以放宽断言/跳过用例/删除整个功能收口。

## 完成标准与限制

- 游戏 Runtime、Editor/Player 编译和自有测试的实际闭包无 YL；游戏不带 YL 时完整基本流程和音频洪峰规则成立。
- YL 的默认编译、受影响测试和导出不再要求主游戏存在；纯 YL 组件/旧资产兼容回归不丢失。保留 XF 共存测试不等于已完成未来卸载 XF。
- 主工程仍保留游戏与 XF；后续正式迁出游戏、删除主工程游戏/共享资源、清理 XF 包及其共存验收，是下一批独立授权事项。
- 不将 Mono 结果等同 IL2CPP，不将编译/静态检查等同播放或听感验收；无法运行独立 Player/音频时明确标为阻塞，不能宣称独立运行已验证。

## 实施验收记录

- [x] 基线与实际注册包版本已记录。
- [x] XF 音频、迁移与优先级/容量回归通过。
- [x] Prefab/Scene/15 事件接线与两轮主流程通过。
- [x] 游戏/YL 双向边界与受影响测试通过。
- [x] GameNoYL 干净导入、新 Bundle、独立 Player 验证通过；Player 本地 NUnit 为证，UTF Editor 回传异常单列。
- [x] YLNoGame 编译、受影响默认测试/Acceptance、导出验证通过。
- [x] 实际差异、元数据、证据、独立 Review 与剩余限制已交接。

## 实施进度与裁定（2026-10-26）

- 基线：保留初始用户的三个 FSM 资产、插件迁移、AGENTS、交付脚本/导出测试和未跟踪夹具改动。Unity 实际 2022.3.62f3c1，注册包为 URP14.0.12、UGUI1.0.0、Cinemachine3.1.6、Timeline1.8.12；主 manifest/lock 不修改。游戏原 EditMode 6/6。
- RED：新游戏边界 3/3 因真实 YL AssemblyRef/资产依赖失败；初版音频回归 12/12 因没有 XF 注册失败；五项音频接线改期望后 5/5 失败；YL 自有夹具/Acceptance 边界 5/5 因夹具缺失和真实 Project.Game 引用失败。后续对应门禁转绿，没有用跳过或修改产品包制造通过。
- 任务一完成：XF 控制器、24/4/80ms、重要音效抢占、四键迁移及生命周期清理已实现；初次扩展音频用例 15/15。Ruling：XF 创建帧中空 Clip 的 AudioSource 可报告 isPlaying=true，注册后显式关闭 playOnAwake 并 Stop(All)，而不是将虚假的初始占用当成并发；有真实失败/通过见证。
- 任务二完成：共享 ClickPlayer、15 按钮、两个 Music 和空 3D 模板改 XF；脚本/资产 meta 与原组件 fileID 保留。5 项接线与真实按钮/两轮菜单-设置-游戏-暂停-返回-重入-失败流程通过。
- Ruling：已安装 XF Overlap1.0.4 使用 Running、OverlapCircle2D，不是旧联合测试假定的 NeedCallToGet/MultiTag 类型；100Hz timer 每帧归零无法证明停止。回归改为真实游戏 actor 的 Running 检查和同场景隔离层的实际 XF 自动物理命中，不手动调用 Get/Check、不改游戏碰撞设置。
- 任务三完成：三个游戏 asmdef 去掉 YL，Acceptance 去掉 Game/XF 及混合 game flow，纯 YL 七项组件生命周期回归保留并迁入 YL，自有三份 Legacy prefab 明确缺失新增字段。主工程游戏 EditMode9/9、初次完整 PlayMode23/23，迁入 YL7/7；受影响 YL EditMode70 项中68组合通过，另2项资源 Acceptance 因组合运行脏场景前置阻断，在干净场景单跑2/2。不能将该组合阻断隐藏为整批一次全绿。
- 独立只读 Review 未确认生产 Critical/Important 缺陷。三项回归缺口按本计划明确承诺的风险断言重新定为 Important：已补真实 GameOver 在24槽饱和后的 Sound/失败Clip、旧菜单晚到停用/销毁不伤新Music owner、标记防止YL后建旧键污染。分别临时删失败音、改旧owner为Stop(All)、绕过迁移标记，3/3明确失败；恢复正式实现后3/3通过，未保留变异代码。
- Ruling：测试 Player 不能借用 Editor Assets 已就绪状态；游戏测试增加显式 XF ReadyRes 准备。独立 batch Editor 的 SceneSetup 可为空，场景接线测试据真实 8/9 失败修正为无旧场景时恢复一个自有空场景，不关闭/保存用户脏场景。
- 独立载体根：`C:/Users/Kano/AppData/Local/Temp/kilo/game-xf-decoupling-20261026-implementation/`。GameNoYL 不含 YL/主工程 Library/旧Bundle；YLNoGame 不含游戏、Main 或游戏DLL，保留XF共存依赖。二者均已正常导入/编译并生成 configure.json 身份回执。独立 Company/Product 隔离真实玩家偏好和 XFAB LocalLow 清理路径。
- GameNoYL：EditMode9/9；图形 Editor PlayMode25/25。新建112个 MainModule Bundle 并逐文件大小/MD5核验输出与StreamingAssets，构建信息SHA256=`c6f9fc8d64e540fef6502550bbca9afe140a0aa8ea39596a5d6f1ce73995a5cc`。正常游戏 Windows64 Mono Development Player 构建 Succeeded、0 errors/9 warnings，Managed 中无YL DLL；实际测试Player验证进行中。
- 命令/进程限制：首次 PowerShell 直接启动GUI Unity未同步等待，初始导入被跟踪会话结束打断，未计成功；后用同步管道运行。batch UTF EditMode已写9/9结果并请求exit0，但native退出期滞留，核验专属projectPath/PID后仅停止该载体进程。正常导入、Bundle与Player构建均有native exit0；图形Editor Play25/25进程正常完成。不得把被停止的batch进程记为正常native退出。
- 独立接收器包以已确认版本和固定XF revision恢复，锁文件由UPM生成；GameNoYL的Burst由依赖正常解析为1.8.21（主工程1.8.29），不是复制整个主工程环境，也未声称所有第三方版本逐项相同。证据在 `Evidence/`，当前无IL2CPP/WebGL/移动平台与完整人工听感结论。

### 基础解耦验收（后续修复前）

| 环境 | 实际结果 | 证据 |
|---|---|---|
| 主工程最终编译/场景 | compileFailed=false，Main 未保存改动=false，新增/迁移测试及夹具无缺失 meta | Unity MCP 实时状态 |
| 主工程最终游戏 | EditMode 9/9；PlayMode 25/25，均无跳过 | Unity TestRunner/MCP |
| GameNoYL Editor | EditMode 9/9；图形 PlayMode 25/25 | `Evidence/game-edit-final.xml`、`game-play.xml` |
| GameNoYL Mono Player | 实际 Player 本地 NUnit 19/19，0失败/0跳过；16控制器、1两轮真实主流程、2按钮/旧owner用例 | `Evidence/standalone-local.xml`、`standalone-local.log` |
| GameNoYL 正常游戏产物 | Windows64 Mono Development，0 errors/9既有warnings，Managed 无 YL；使用本批112个新Bundle | `Evidence/GamePlayer/GameNoYL.exe`、`player-build.json`、`bundle-pass.json` |
| YLNoGame 定向回归 | EditMode 68/68 + 音频资源 Acceptance 2/2；迁入 PlayMode 7/7 | `Evidence/yl-edit.xml`、`yl-audio-resource.xml`、`yl-play.xml` |
| YLNoGame 正式导出 | 正式入口/固定根、实际包内容及GUID/资源保留 2/2 | `Evidence/yl-export.xml` |
| YLNoGame 默认UI | 实际图形 Play `PASS:READY round=1`，不调用游戏联合入口 | `Evidence/UIAcceptanceEntry.json` |
| YLNoGame 默认音频 | 自有PCM/Mixer/Resources夹具 Mono Player，`AUDIO_PASS`、`QUIT_PASS`；近处RMS约0.06147、远处0，静音/暂停/淡变/Mixer/资源及真实退出断言通过 | `Evidence/yl-audio-player-final.log` |
| 差异/排版 | `git diff --check`无空白错误；七个受影响YL脚本 Format-YL Check通过 | 命令实际输出；既有CRLF转换提示未顺手全文件改写 |

- Ruling：Player 中 `GameViewLoadingTests` 本来就是 `#if UNITY_EDITOR` 六项，不计入 Player；Player 的正确期望数为19而不是Editor的25，不是删测或跳过。
- UTF1.3.9 standalone 首次无终态后，仅在临时载体测试程序集添加 `ITestRunCallback` 本地记录（未放入主工程/正常游戏产物）。证实19个测试逐项完成且顶层 RunFinished=Passed，耗时约6.37秒；Editor未接收最终XML/退出指令的问题在回传链，不能说是 ReadyRes 挂起或测试失败。保留本地完整原始NUnit XML后，仅停止本批专属宿主和Player；不计为native正常退出。未修改UTF、XF PackageCache、Source包版本或防火墙。
- Ruling：源码只有 `AudioAcceptanceEntry`，不存在可直接打开的 AudioAcceptance.unity。独立Audio验收在临时载体由现有 WriteWave 帮助生成两个自有Clip，并建立Library/Mixer/Resources及Camera/Listener场景，直接运行未弱化的默认入口。首次夹具漏Listener导致空间衰减失败，补齐夹具后同断言通过，不修或跳过框架产品功能。
- YLNoGame 沿用的可选 HybridCLR SDK 默认启用且未安装原生补丁，会拦截普通Mono构建；只在临时载体本次构建内关闭该可选SDK的 enable，不改主工程、SDK安装或依赖。不是 HybridCLR/IL2CPP 验收证据。
- 正常游戏构建9项warning为原有CinemachineVirtualCamera/XF旧重载弃用和物品collider多余new，不是Missing Script/新增YL依赖；未扩大范围做API升级。
- 独立Review三个关键测试缺口全部有受控3 RED -> 恢复正式实现3 GREEN，之后主工程25/25及独立Player19/19复验。未遗留变异代码、未修改用户已有FSM/插件/交付脚本、未覆盖源工程旧Bundle；本批所有跟踪验收进程已退出或按专属归属停止，主Editor保持打开。
- 主观听感、不同音效混合后的审美平衡与其他平台仍未验证；当前阈值为用户批准的80ms/4/24及两级抢占，没有无依据声称完成全平台或人工听音。

正式接收游戏仍是下一批；本批两个临时工程和可运行产物的可用位置见上述载体根，源码只保留必要实现/测试/既有文档更新，不引入新的永久验收平台。

### 编辑器音效无声修复（2026-10-26）

- 用户反馈仅 BGM 可闻。本机读取确认 `AudioConfigMute:Sound=1`，`AudioConfigVolume:Sound=0.161909044`；旧 `KEY_AUDIO_MANAGER_SOUND_ON=0`，迁移标记已存在。音效分类处于持久化静音，不是音量为0或缺少播放请求。
- 原设置面板只有音量滑条，既不反映有效静音状态，也不在用户调大音量时取消独立 Mute；原测试预设可闻偏好，未覆盖这一真实持久化状态。
- 修复仅在 SettingPanel：静音分类显示0，初始化使用 SetValueWithoutNotify，打开面板不改存储的音量或静音；用户主动调整时同时更新该分类音量/Mute，正值恢复出声、0保持静音。Sound/Music 两类一致，不改变底层 XF 或游戏 AudioController 的音量属性契约。
- 新增两个分类的 UI 回归，原实现2/2明确失败，修复后2/2通过。新增真实 Listener PCM 回归：持久化静音时战斗输出小于0.00001，真实滑条恢复后战斗输出大于0.0001，不能只用 isPlaying 证明有声音。
- 仅将本机 `AudioConfigMute:Sound` 从1设为0；原约16.2%音量、BGM配置、YL旧键及迁移标记保持。完整游戏 PlayMode28/28、EditMode9/9通过；最终编译正常、Main无未保存修改，Sound静音键仍为0，差异无空白错误。
- 本次验证针对用户反馈的主工程 Editor；未重建上批临时独立工程/Player，旧临时产物不包含本次 UI 修复。不将机器 PCM 验证等同人工主观听感确认，未提交或推送。

### 连射音效漏播修复（2026-10-26）

- 用户反馈连续射击断断续续。查得机炮3 Clip约0.444秒、MachineGun射速10发/秒，需要同时保留5个未结束尾音；机炮4约0.496秒、HeadGun默认12发/秒（Snake实例覆写11发/秒同样需6路），需要6路。原同片段4路会先于24路总预算拒绝第5发。
- 新回归用真实枪声Clip长度与对应射速计算首发未结束窗口内的重叠数，旧实现两种武器均明确在第5发失败；单音效8路边界用例也先失败。将SameSoundLimit从4调到8后3/3通过，没有剪短Clip、截断旧尾音或放开全局预算。
- 总战斗音源仍为24，Unity实际RealVoiceCount仍为32；80ms去重、重要音效保护、满额处理及不排队保持。新8路边界仍验证重要请求在同片段内替换、不创建第9路；24路饱和与稀有重要抢占回归保持通过。
- 完整主工程游戏PlayMode30/30、EditMode9/9，无跳过。原始独立载体/Player未重建，本次只复验当前Editor；多武器同时密集爆炸用满24路时仍会按设计拒绝普通请求，超过12.5次/秒的同片段请求仍受80ms去重，不能宣称无限并发或所有更高射速都可逐发播放。

### 混战来源分组与扩容（2026-10-26）

- 后续排查确认：旧去重键只有音效名，敌我同名机炮会共用 80ms 窗口；爆炸长尾也与玩家射击争抢统一预算。仅从同名 4 调至 8，无法解决混战来源互吞。敌机炮射速 7 发/秒、霰弹 0.8、火箭 0.5；本轮不改玩法射速或 Clip。
- 用户批准并已落实 16/16/12/4 分组，共 48 固定 XF 非 OneShot 战斗槽，真实声道 64、虚拟声道 512；上表为当前契约。玩家按实例去重，敌枪按组/同名跨实例合并。普通枪组可复用本组普通尾音，普通爆炸满额拒播，Important 不跨组抢占。
- RED/GREEN：4 项新分组回归在旧实现全部失败；分组实现后 3/4 通过，真实声道仍 32 的断言继续失败；同步导入 AudioManager 配置后 4/4 通过。真实枪声 10/12 发每秒回归及音量、静音、释放、失败加载和 48 槽 GameOver 独立反馈回归均保留。
- 独立只读审查未确认 Critical/Important 生产缺陷；确认玩家实例去重记录不回收的 Minor 资源风险，以及真实 Shoot 链路和声部虚拟化的测试缺口。本轮将这些纳入有界接纳/来源正确性的收尾：每秒按播放请求清理已过窗口的记录，复用删除列表，未添加常驻更新器或公开 ForgetEmitter API。
- 去重记录回归先在未修实现失败（预期 2 条、实为 3 条）；修复后验证保留最近 cooldown，连续 20 批更换 160 个 emitter 不积累历史条目。清理周期在 OnExit 重置，现有拒播不滑动窗口与 Important 升级规则不变。
- Ruling：直接遍历当前所有启用武器不能证明 XF 已完成 Start；真实失败显示某敌人子武器 `loaded=false / emitter=0`，生成器可在当前帧加入尚未 Start 的对象。本轮改为受控真实 XF 加载，等待该批嵌套 View 的 Start，再反射执行实际 MachineGun.Shoot（不手动 OnLoaded），验证两把玩家同名枪与敌枪落入各自槽、敌枪重复聚合，以及关闭/重新加载同一个敌人 root 后仍正确。不据测试初始化顺序改动生产生命周期。
- 上述集成回归经受控变异验证：临时使 PlayShotSound 不转发 group/emitter，测试明确失败（玩家组预期 2、实为 0）；恢复正式转发后完整回归通过，未保留变异代码。7 个真实武器/敌人 prefab 的归属与重复 OnLoaded 检查继续保留。
- 补充直接边界：敌枪同 Clip 第 7 发仍接纳但只占 6 槽；两种枪组的 Normal 不替换满组 Important；逐组检查 Normal/Important 的 Source priority。实际 48 路混战、菜单 BGM 与 UI 同时发声，等待 DSP 推进后四个 Feedback 槽均在播、priority=16 且 `isVirtual=false`。只证明该负载和设备下反馈未被虚拟化，不以整个混音非零证明反馈自身 PCM。
- 最新主工程完整回归：PlayMode **39/39**（约 9.74 秒）、EditMode **16/16**，0 失败、0 跳过。此前一次按 testClass 过滤返回 Unknown/0，不计为通过；后续使用完整程序集结果。全场未 Start 对象的初版断言失败及新增测试参数误用导致的编译失败均已据真实原因修正，没有跳过或弱化产品契约。
- 最终状态：Editor 非 Play、非编译且无编译失败；Main 场景无未保存修改，Music/Sound 与全部 48 个测试战斗控制器已释放。实际配置为 64/512；Sound 音量仍为 `0.161909044`、Mute=0，旧 SoundOn=0、迁移标记=1。游戏 9 个测试脚本的 `.meta` 无缺失；跟踪差异及本轮未跟踪测试/计划的 `git diff --check` / `--no-index --check` 无空白错误，既有 CRLF 提示未作全文件改写。框架音频文档只保留游戏业务边界，不重复硬编码已过期的 24/4 门限。
- 本轮未重建 GameNoYL/YLNoGame、Bundle 或 Player；上方独立证据仅证明当时的解耦版本，不包含后续设置面板、48/64 与来源修复。本轮未执行 CPU benchmark、全体 48 路逐一可听性证明、独立反馈 PCM 测量或人工主观听感；不能外推其他平台或任意数量 UI OneShot 的负载。

### 首次音效素材响度校准（2026-10-26）

- 用户反馈不同音效素材本身响度不平衡。测量发现 Unity 的 `Normalize` 会在立体声转单声道时把峰值重新拉到 0 dBFS，因此对已归一化的 Clip 单独降低文件增益会被导入器抵消；必须先关闭该选项，素材增益才会生效。
- 原始 Unity 导入电平（RMS / 50ms 最大 RMS，dBFS）：机炮3 `-15.57/-9.93`、机炮4 `-17.10/-7.76`、霰弹1 `-15.45/-7.32` 作为战斗参考；火箭发射 `-11.53/-4.79`、火箭爆炸1 `-8.56/-2.62` 明显偏响；机体爆炸1 `-15.36/-6.37`、挑战失败 `-13.69/-8.44` 与参考接近；按键音效 `-19.27/-10.63` 与 Click5 `-32.05/-21.79` 相差约 11 dB。
- 仅调整三处素材：火箭发射关闭 Normalize 并做 `-3.0 dB` 无损增益；火箭爆炸1 只关闭 Normalize（等效降低约 4.0 dB）；按键音效关闭 Normalize 并做 `-6.0 dB`。使用 MP3Gain 1.5.2 直接修改 MP3 帧增益，不重新编码；`/g` 每步 1.5 dB。Click5、机炮3/4、霰弹1、机体爆炸1、挑战失败的素材与导入设置未改。
- 调整后 Unity 实测：火箭发射 `-14.13/-6.54`、火箭爆炸1 `-12.59/-6.65`、按键音效 `-30.55/-21.91`。爆炸仍高于枪声约 3 dB，发射声与枪声接近，两个点击音短时响度基本一致；火箭发射源文件本身峰值已超过满刻度，降低 3 dB 后 Unity 峰值仍为 0 dBFS，属于原有素材削波，未用限幅或重编码掩盖。
- GUID、采样数、声道、编码格式与比特率均未变化；三个 `.meta` 仅将 `normalize` 由 1 改为 0。原文件与原始 `.meta` 备份在 `C:\Users\Kano\AppData\Local\Temp\kilo\game-audio-balance-20261026-054920\original`，MP3Gain 同时保留可撤销标记。
- XFAB 当前 Profile 的 `loadMode` 为 `Assets`（枚举值 1），Editor 直接从 Assets 加载，修改后可直接试听；Player 仍从旧 AssetBundle 读取，未重建 Bundle，因此本轮素材改动尚未进入 Player/旧包，不能宣称正式构建已生效。
- 回归：PlayMode **39/39**、EditMode **16/16**，0 失败、0 跳过。Editor 非 Play、编译正常、Main 场景未脏；Sound 音量 `0.161909044`、Mute=0、迁移标记与旧键保持。未执行人工主观听感、频谱/限幅复验或其他平台验证；点击音整体降幅较大，若试听偏轻可只回退按键音效的增益。

### 提交前审计尾项收口（2026-10-26）

- 审计确认三个尾项：火箭发射在前次校准后仍解码超幅；DomainMainAcceptance 仍保留已删除测试的 OriginalGame scope；帮助面板两处仍宣称 MainModule 使用 YL 的 10 帧业务覆盖。本节记录当前修正，首次校准和旧载体结果保留为历史，不据旧结果宣称最新 Player 已验收。
- Ruling：前次把火箭发射的超幅直接归为“原有素材削波”并不充分。浮点解码显示峰值仍为 `+1.3766 dBFS`，可以继续无损衰减以避免本次导入时再次削波；这不等于恢复更早制作环节可能已经失去的信息。从已核对 SHA256 的原始备份重新生成总计 `-6.0206 dB` 的 MP3Gain 版本（4 个增益步，相比前版再减约 3.01 dB），避免重试时重复叠加。
- 最终素材核验：浮点解码峰值 `-1.6337 dBFS`、超满刻度采样数 0；FFmpeg 过采样真实峰值约 `-1.6 dBFS`。Unity 实际导入为单声道，峰值 `-1.428 dBFS`、RMS `-17.057 dBFS`、50ms 最大 RMS `-9.255 dBFS`，满刻度附近采样数 0。新增回归要求导入后的火箭发射保留至少 1 dB 余量，旧素材明确失败（0.999969 > 0.891251），修正后通过。
- 编码、原始双声道、48kHz、192kbps、45 个 MP3 帧和 51840 个解码采样/声道保持；Unity 采样数和 GUID `6791b72ea69e51543beb4b6006a907c7` 不变。浮点新旧 PCM 与固定增益的相对残差约 `1.6e-7`，无重编码/截断证据。ffprobe 对含 APE 元数据文件的估计时长相差约 4ms，不能把该估计当成真实采样时长变化。其他八个音效及两首 BGM 与上次状态一致，原始备份未覆盖。
- DomainMainAcceptance 删除 OriginalGame 分支，将现有 scope 选择整体移到 Begin 之前；退休或未知 scope 抛带 `scope` 参数名的 ArgumentException，不创建目录、切场景或接管 Editor。原有有效 scope、Protection 集合和恢复流程不改。两项回归分别验证 OriginalGame/UnknownScope 在保护/运行名检查前失败，SessionState owner、场景与 Enter Play Mode 设置保持；两项旧实现均明确失败，修正后通过。
- AudioKitHelpProvider 仅删除两处 MainModule 旧策略宣称，保留 YL 自身显式 10 帧、5 帧默认值和 Linear 的合法行为说明，不把游戏 XF 策略写进框架契约。
- RED/GREEN：上述三项新增回归先 `0/3`、修正后 `3/3`。最新定向完整组复验共 **149/149**、0 失败/0 跳过：游戏 EditMode **17/17**、PlayMode **39/39**；YL 旧序列化/组件/程序集边界 **68/68**，AudioHelp **12/12**、新验收输入 **2/2**（三者同组共82）；音频资源 Acceptance **2/2**、生命周期 PlayMode **7/7**、正式导出 **2/2**。这不是全仓库或全部显式保护验收的结果。
- 两个受格式约定覆盖的修改脚本通过 `Format-YL.ps1 -Mode Check -Paths ...`；主工程专属 Tests/Tools 保持原有排版，没有扩大格式化。实际 diff 与未跟踪游戏接线测试/计划另作空白检查。Editor 最终非 Play/非编译、compileFailed=false、Main 未脏；验收 owner/settings 为空，48 个战斗控制器已释放；Sound 音量 `0.161909044`、Mute=0、旧 SoundOn=0、迁移标记=1 保持。
- 本轮只收上述三个尾项，未暂存、提交、推送、改网络配置或重建 Bundle/Player；不接管原有 FSM、试验目录和其他生成物。TLS 仍未解决，本次审计独立代理因 TLS 中断，相关范围由主代理补做本地复核，不把中断算独立 Review 通过。当前源码可选择性提交，不等于最新 Player、所有平台或人工听感已完成验收。

### BGM 与失败提示继续降幅（2026-10-26）

- 用户实际试听后反馈 BGM 和挑战失败仍偏响，本批继续只调素材，不改播放代码、48 路策略或玩家音量偏好。菜单 StartPanel 使用 `03.wav`，游戏场景使用 `02.wav`，接线与循环设置不改。
- 两首 BGM 均在 PCM 数据上做固定 `-6 dB` 衰减，保留 RIFF 头、文件长度、原始双声道、48kHz/16bit、采样数及全部 `.meta` 字节；不转有损格式、不压缩动态、不重采样或剪裁。逐采样核对符合固定增益加取整，误差不超过 0.5 LSB。原始备份新增到既有 `original/bgm02.wav`、`bgm03.wav` 及配套 `.meta`，原文件 SHA256 与早前未改 BGM 的基线一致。
- 挑战失败以原始备份生成 MP3Gain 4 步衰减（约 `-6.0206 dB`），无重编码；仅关闭该 Clip 的 Normalize，避免导入器抵消增益。161 个 MP3 帧、185472 个解码采样/声道、原始双声道/44.1kHz/128kbps 保持；Unity 仍按既有设置导入为单声道，GUID `975168f99e37fcb408957461ac576ce4` 不变。

| 资源 | Unity RMS 前 / 后（dBFS） | Unity 最终峰值（dBFS） | 原始文件 LUFS 前 / 后 |
|---|---|---|---|
| 游戏 BGM `02.wav` | -17.479 / -23.480 | -6.330 | -14.7 / -20.7 |
| 菜单 BGM `03.wav` | -17.167 / -23.170 | -8.404 | -14.8 / -20.8 |
| 挑战失败 | -13.694 / -20.647 | -7.139 | -11.0 / -17.0 |

- 失败提示在 Unity 中实际下降约 6.95 dB，额外差值来自关闭原有单声道归一化；LUFS 为原始双声道解码测量，与 Unity 单声道 RMS 不混为同一指标。WAV 时长精确保持；MP3 的容器估计时长因增益撤销元数据变动，但帧数与真实采样时长不变。
- 新增三个真实 AudioClip RMS 上限回归，初始 **0/3** 明确失败（两首音乐约 -17 dBFS、失败提示约 -13.7 dBFS），调整并强制重导入后纳入游戏完整 EditMode **20/20**；PlayMode **39/39**，均 0 失败/0 跳过。本批新跑游戏合计 **59/59**；YL 代码未改，沿用上一批相关证据，不把沿用项目统计为本轮重跑。
- 其他枪声、爆炸、按钮素材及其已完成的修正保持。Editor 最终非 Play/非编译、Main 未脏，测试音频控制器已释放；本批开始前用户 Music 音量已为 `0.074273549`，结束仍相同；Sound 音量 `0.161909044`、Sound Mute=0、Music Mute 键仍不存在，旧键及迁移标记保持。
- 测量收据在既有临时目录 `music-failure-before.json` / `music-failure-after.json`。Editor 使用 Assets 模式可直接试听；本阶段未重建 Bundle/Player，后续 MainModule 资源重建见下一节。未提交或推送，未把自动测量宣称人工听感通过。TLS 仍独立待排查。

### 主工程 MainModule Win64 资源重建（2026-10-26）

- 用户授权重打整个 MainModule 并更新 StreamingAssets。使用当前已打开的 Unity 2022.3.62f3c1，目标 `StandaloneWindows64`，版本保持 `1.0.0`，未启动同工程第二个 Editor、切换加载模式或构建 Player。当前已保存的源资源（含原有 FSM 修改）参与打包，未回退或修改这些内容。
- 使用已安装 XFAB 的正式 ProjectBuild 构建实现和同一前置钩子；因公开 Build 只排队 delayCall，为取得同步完成及错误回执，调用已核对源码的 BuildDelay 实现，不另写构建算法、不改 PackageCache。当前无额外前后构建钩子。沿用既有自动图集、清理临时输出和复制 StreamingAssets 选项。
- 覆盖前备份在 `C:/Users/Kano/AppData/Local/Temp/kilo/mainmodule-rebuild-20261026-083939/Before/`：旧输出224文件、旧 StreamingAssets448文件（含.meta）、旧 MainModule 自动图集4文件及目录.meta。XFAB 对应的 MainModule 运行时缓存目录当时不存在，没有清理其他模块或全局缓存。`prepared.json` 记录483个源文件指纹和音量偏好。
- 构建约20.29秒，0错误、23警告：8条 FSM 脚本源码不能作为 AssetBundle 资产保存、6条 MCP 扩展运行时空程序集、9条现有 C# 过时 API/多余 new 警告。C# 代码仍需随 Player 编译，不能靠资源包更新代码；未为消除这些非阻断警告扩大改动范围。完整构建日志/结果在 `build.json`。
- 输出：`AssetBundles/MainModule/1.0.0/StandaloneWindows64/`；同步目标：`Assets/StreamingAssets/MainModule/StandaloneWindows64/`。本次严格计数为109个 `.unity3d` 资源包，manifest 内也是109；XFAB bundleInfos共112项，另外3项是平台 manifest、AssetBundleNameMappingUpgrade.txt、bundles_suffix_info，不能把112项全部称为资源包。两处各224个非.meta文件（另含manifest、索引及构建信息）。
- `files-validation.json`：112项文件大小/MD5均符合构建信息，输出与StreamingAssets的完整非.meta文件集合和逐文件SHA256全部一致，无缺失/多余/不同文件；构建信息SHA256=`026901188fe5e9ccc165f4db747786c8568aa87da2895aa8fe9386a924678667`。
- `payload-validation.json`：直接从新StreamingAssets AssetBundle读取六个已校准音频，AssetDatabase路径均为空，未借 Editor Assets 回退；BGM02/03 RMS分别为-23.480/-23.170 dBFS，失败提示-20.647、火箭发射-17.057（峰值-1.428）、火箭爆炸-12.593、按键-30.551，均符合当前校准结果，采样数/频率/声道与源导入一致。
- 同一实包核验共享 ClickPlayer、七个 UI Prefab 和15个Play音频事件，目标均为XF Sound，无Missing Script或YL组件，StartPanel的Music owner引用正确；Game场景存在于新场景包。只读取资源和场景路径，不打开Game场景、不运行整局或宣称新Player已验收。验证加载的44个manifest/依赖/目标包已按所有权卸载。
- `final-validation.json` 为 Verified：483个源指纹保持（包括原FSM）、项目序列化配置不变、Main未脏、无残留验证包，Editor非Play/非编译、compileFailed=false。Music/Sound音量及Mute键保持。构建产物遵循现有忽略规则，没有强制暂存或提交生成物；未提交/推送、改包版本、部署或构建Player。一次文件核验片段因字符转义未编译，未执行任何写入且未重复打包，修正后文件/实包核验均通过。

### Git 交付范围（2026-10-26）

- 用户明确授权将本批已完成任务提交并推送当前 `integration/xf-yl`，上游为 `origin/integration/xf-yl`。准备收口时本地与实时远端均为 `d460a093c181203c6b434382a95ad6ed6765a11b`，无已有暂存内容或未推送提交；不切分支、不合并、不创建PR或重写历史。
- 本批交付包括游戏/YL双向解耦、XF音频接线和偏好、48路分组及64真实声道、六个音频资源的校准（其中火箭爆炸仅改导入选项）、游戏与YL测试分家及Legacy夹具、退休验收入口/帮助修正和本计划。源素材按既有Git LFS规则提交，新增和移动的脚本/Prefab配套meta一起提交。
- 原有3个FSM资产继续留在工作区：EnemyShield/EnemyState/GameStateManager 的差异为编辑器视图/节点位置和默认nameHash=0，不是本批源代码的新增运行依赖，不借提交本批成果将其一并收走。另一生态集成计划的T2工作区回执、Assets/Test、Release82b与Data/Workbench试验夹具、YLFrameworkData和HybridCLRSettings等也不纳入。
- StreamingAssets、AssetBundles及构建缓存继续遵循已有忽略规则，不强制纳入Git；最新资源包及备份仅是本机已验证产物，其他检出环境需按本计划从源资源重新构建。TLS诊断日志、认证/网络配置和临时音频处理工具不提交。
- Git收口整理记录、暂存范围及新增Unity YAML空值行的尾随空格；后者不改变GUID、fileID、资源结构、代码或媒体内容。暂存检查覆盖此前未跟踪的文件，修正后重新检查。沿用上一节构建前后483个源指纹、实包内容及文件哈希验证，以及当前游戏EditMode20/20、PlayMode39/39和先前YL定向回归证据，不把空白清理计作新的技术验收或为纯Git步骤重复跑整套。当前源码交付不等同最新Player、所有平台或人工听感已验收，TLS问题仍未解决。
- 本批采用一次中文提交，实际Hash和推送回执由Git历史及交付回复承载，不预写成功、不为补Hash追加docs提交或改写历史；用户未完成和不属于本批的改动保留现场。
