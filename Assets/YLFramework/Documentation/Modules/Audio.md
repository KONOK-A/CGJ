# AudioKit

> [文档首页](../README.md) · [架构总览](../Architecture.md) · [Action](Action.md) · [Timing](Timing.md)

## 最短路径

准备外部 `AudioClip`，场景由相机或玩家提供一个有效、启用的 `AudioListener`。无需调用 `Initialize`，也无需 Mixer、配置资产、预热或修改并发设置。

### 代码播放三类 Clip

```csharp
using YLFramework.Services.Audio;

AudioKit.PlayMusic(musicClip); // 默认循环
AudioKit.PlayVoice(voiceClip); // 默认单次，替换旧 Voice
AudioKit.PlaySound(soundClip); // 默认单次，允许重叠
```

### 背景音乐组件

在场景对象上添加 `AudioMusicPlayer` 并绑定 Clip；默认 Loop 与 `playOnAwake` 开启，在首次 `Start` 自动播放，不是每次 `OnEnable`。需要手动控制时关闭自动播放，由 UnityEvent 调用无参 `Play/Stop`；组件停用只停止自己持有的音乐。

### Button 点击音

添加 `AudioEmitter`，在 Clip 列表放入点击音，保持 2D、Loop 关闭。在 `Button.onClick` 中拖入该组件并选择 `AudioEmitter.Play`。普通 2D one-shot 不因按钮面板停用被截断；组件新建/Reset 默认继承全局去重策略，需要同帧重叠时显式选择 EveryOne。

### Settings Slider 与 Listener

```csharp
musicSlider.SetValueWithoutNotify(AudioKit.Settings.MusicVolume.Value);
musicSlider.onValueChanged.AddListener(value => AudioKit.Settings.MusicVolume.Value = value);
```

常规 Slider 使用 `[0,1]`；分类音量本身允许有限的非负倍率，包括大于 1。需要同步其他入口的修改时，使用 `MusicVolume.RegisterWithInitValue(value => musicSlider.SetValueWithoutNotify(value))`，并由 UI 所有者注销订阅和事件监听。已有合法 PlayerPrefs 优先于缺省值 1；音量为 0 是静音而非停止。Audio 不创建第二个 Listener；没有有效 Listener 时不能听见声音，Source 状态检查不能替代试听。

| 无需配置 | 仅使用对应高级功能时配置 |
|---|---|
| 初始化、Mixer、配置资产、资源 Loader、并发上限、预热 | Mixer 路由/暴露参数/Snapshot；资源加载仍由业务负责 |
| 普通 2D 播放的距离、跟随目标、淡变、Priority | At/Following 空间参数；自动淡变/Crossfade；去重、并发与缓存预算 |

## 默认值与配置

### 统一初始默认值

以普通项目提供 Clip 即可播放、不隐式增加效果或修改全局状态为原则。下表是无业务覆盖、无合法存档覆盖时的初始值；组件新建值指编辑器添加/Reset 后的预设，不用于覆盖旧场景或 Prefab。

| 配置 | 初始默认值 | 选择理由 |
|---|---|---|
| 播放类别与 Loop | Sound/Voice 单次，Music 循环 | 点击音/人声自然结束，背景音乐持续播放 |
| 请求 Volume / Pitch / Priority | 1 / 1 / 0 | 不改变素材音量倍率或音调，不预设抢占优势 |
| Settings 分类开关 / 音量 / 临时 Mute | true / 1 / false | 新项目直接可听，已有合法存档仍优先 |
| 位置与空间衰减 | 默认 2D；显式 At/Following 使用距离 1/50、Linear、doppler=0 | 普通 UI 无距离配置；空间效果仅显式启用，避免默认多普勒变调 |
| 路由 | Inherit，分类 Group=null 时 Direct | Mixer 可选，不要求配置资产 |
| 去重模式 / Options.PlaySoundMode | 全局 EveryOne / null 继承 | 默认允许重叠，由全局统一策略或单次覆盖控制 |
| 全局 / 每 Clip 去重窗口 | 5 / 5 帧 | 启用窗口去重模式后抑制短时连发，比 10 帧更少影响快速操作；按帧计数，不是固定毫秒数 |
| Sound 并发 | Limit=0、DefaultClipLimit=0，不限；RejectNew | 开箱即用不拒播；显式设置上限后不默认抢停旧声音 |
| 闲置缓存 / 预热 | 10 / 不自动预热 | 复用小规模常规音效，避免无请求时分配音源 |
| AutoFade | 首尾时长均 0 | 保留素材原有起止，淡变由业务显式启用 |
| CrossFadeMusic API | curve=EqualPower；ignoreTimeScale=true；duration 必填 | 不相关 BGM 交接减少中点功率下陷，暂停游戏时间时仍可过渡 |
| MusicPlayer Crossfade | 关闭；duration=1 秒；unscaled=true；编辑器新建/Reset curve=EqualPower | 常规音乐直接播放，启用过渡时有可用的初始预设 |
| 编辑器新建/Reset MixerController | applyOnAwake=false；transitionDuration=0.5 秒 | 全局路由显式开启，参数/Snapshot 用途不意外清路由；显式 Snapshot 过渡避免默认硬切 |

EveryOne 才表示关闭去重；显式将窗口设为 0 可在 GlobalFrames 或 SoundFrames 下只做同帧去重。默认 5 帧的边界包含最后一帧，约为 60 FPS 下 83 ms、120 FPS 下 42 ms，不是固定秒数。显式 10 帧仍是合法的业务冷却，MainModule 的 10 帧覆盖不变。EqualPower 不保证等响度或不叠加峰值，显式 Linear 仍适用于需要线性交接的素材。

| 组件 | 编辑器新建/Reset | 旧资源及未触发 Reset 的字段初始化 |
|---|---|---|
| AudioEmitter | useGlobalPlaySoundMode=true，继承全局 | false，保留原 playSoundMode=EveryOne |
| AudioMusicPlayer | crossFadeCurve=EqualPower，Crossfade 仍关闭 | crossFadeCurve=Linear，保留旧缺字段资源；已有序列化选择不自动改写 |
| AudioMixerController | applyOnAwake=false，transitionDuration=0.5 | 字段初始化 true/0，保留旧资产自动路由与即时 Snapshot 过渡行为 |

运行时 AddComponent 不承诺触发 Reset；需要上述新建预设时应显式配置。新建 MixerController 仅绑定 Config 不自动应用或清空全局路由，必须勾选 Inspector 的 `Apply Routing On Awake`（applyOnAwake）并提供有效 Config，或显式调用 Apply。旧资源已有配置仍按其序列化值工作，不因新预设被重置。

### Options 使用

`AudioSoundOptions` 仍是 `[Serializable] sealed class`，公开字段名、类型和无参构造保持不变。构造默认值集中来自内部 `AudioPlaybackOptions` 值工厂，避免公开配置与运行时快照维护两套默认值。可以只修改需要的字段：

```csharp
var soundOptions = new AudioSoundOptions { Volume = 0.5f };
AudioKit.PlaySound(soundClip, soundOptions);

var musicOptions = AudioSoundOptions.MusicDefaults;
musicOptions.Pitch = 1.1f;
AudioKit.PlayMusicWithOptions(musicClip, musicOptions);
// 只调整音乐音量时无需 Options：
AudioKit.PlayMusic(musicClip, volume: 0.5f);
```

| 配置/输入 | 行为 |
|---|---|
| `new AudioSoundOptions()` / `Defaults` | 有效非循环默认值：Volume=1、Pitch=1、2D；Defaults 每次返回新实例 |
| `MusicDefaults` | 每次返回独立实例，Loop=true，其余与 Defaults 一致 |
| 音乐 Options 省略/`null` | 使用与 MusicDefaults 一致的内部值默认，不创建配置类；WithOptions 的位置参数仍必填，但允许显式 null |
| Sound/Voice Options 省略/`null` | 使用与 Defaults 一致的内部值默认，不创建配置类；`default(AudioSoundOptions)` 现在是 null |
| 音乐显式传入 `Defaults` / `new AudioSoundOptions()` | Loop=false；不按类别把显式 false 改成 true |
| `Volume=0` / `Pitch=0` | 前者合法静音；后者非法抛错，不作为缺省回退 |
| 分类开关/音量/临时 Mute | 缺省 true/1/false；已有合法存档优先，坏音量存档内存回退 1 且不重写 |
| 路由/去重 | Inherit，无分类 Group 则 Direct；Options.PlaySoundMode=null 继承全局，初始 EveryOne |
| 新建/Reset Emitter / 旧缺字段 Emitter | 编辑器新建或 Reset 继承全局；旧资源保持原显式 EveryOne，运行时 AddComponent 不保证触发 Reset |

公开播放入口将 Options 字段捕获一次为内部 `AudioPlaybackOptions` struct，校验和规范化作用于局部快照，提交边界与最终请求存储保留值副本，不再复制配置类对象。AudioManager、AudioMusicPlayback 和 AudioVoice 的只读传递层使用 `in` 借用提交入口的局部快照，减少重复拷贝；提交入口不借用调用方或 Action 模板字段，回调重入仍保持隔离。修改调用方 Options 不影响已提交请求，句柄修改也不反写原对象或其他请求。`PlaySoundAction.AllocateWithOptions` 在 Allocate 时捕获并校验独立值快照，Start 按值提交动作模板；普通 `var copy = options` 只是类引用赋值，不是快照。嵌套配置按值复制，`OutputRoute.Group` 等 Unity 资源引用仍为借用，不克隆资源。内部有效 Defaults/MusicDefaults 与清理用的 `default(AudioPlaybackOptions)` 不同，后者不能用作播放默认值。

`new AudioSoundOptions()` 和每次读取 `Defaults/MusicDefaults` 仍分配新的独立可变类实例，不是共享静态对象。高频调用可由业务持有并复用同一个公开配置对象，避免每次 new 或读取这些属性；每次提交仍捕获独立值快照，后续修改只影响之后的请求。组件直接从序列化字段构造内部值配置，不在每次 Play 创建公开配置类。此优化只针对内部 Options 类分配，不承诺整个 Audio 零 GC；初始化、音源创建、集合扩容和 Action 回调闭包等仍可能分配。

### 配置分配验证

2026-10-07 在 Unity `2022.3.62f3c1` Editor PlayMode 使用 `AudioOptionsAllocationPlaybackTests` 对同一 Clip、配置、回调和预算做前后对比。预热 24 个 Sound Source、固定 Music/Voice Source、容器容量和 Action 池，每条路径重复 4 轮，每批 24 请求；下表为当前线程 `GC.Alloc` 的分配事件数，不是字节数。已知 `byte[4096]` 校准检测到 1 次事件，空测量区间为 0；断言、日志、输入构造和清理均在测量区间外。

| 稳定路径（默认 / 复用公开配置结果相同） | 配置优化前事件 / 24 请求 | 值快照优化后事件 / 24 请求 | 冷路径优化后事件 / 24 请求 |
|---|---|---|---|
| Sound；Music/Voice 简单、null、复用配置入口 | 24 | 0 | 0 |
| AudioEmitter；AudioMusicPlayer 普通播放 | 48 | 0 | 0 |
| PlaySoundAction Allocate 默认 / WithOptions | 24 | 0 | 0 |
| Sound 通道关闭、去重拒播、全局 / Clip 容量拒播 | 24 | 0 | 0 |
| CrossFadeMusic 默认 / 复用配置 | 72 | 48 | 0 |
| AudioMusicPlayer Crossfade | 144 | 96 | 0 |
| PlaySoundAction Start 默认 / WithOptions，包含 Started 回调 | 96 | 72 | 72 |

默认值构建、配置捕获、字段规范化、合法配置校验和 Voice 值存储/读取的严格测试重复 4096 次，测得 0 次分配事件。完整播放测试同时检查实际播放、Started 回调、接纳/拒播统计及 Source/Voice/Action 复用，不通过跳过播放减少分配。预热后单独测量 24 次 `Enum.IsDefined` 的对照代码得到 48 次事件，曾与值快照优化阶段的 Crossfade 残余一致；当前 API 与组件改用无装箱的连续枚举范围检查，预热 Crossfade 的四轮各 24 请求均测得 0 次事件，并作针对性断言。全局播放模式的合法赋值/读取重复 4096 次亦验证 0 次事件。Action Start 保留执行版本捕获及完成/Started 委托，其 72 次事件没有被本轮修改。

Clip 并发上限检查取得的计数可复用于正常接纳，避免再次读取字典；抢占的 Stop 可能同步触发其他声音停止，因此先丢弃缓存，再读取当前计数。此优化减少重复查询，不声称字典的读后写变成单次哈希查找，不引入新容器或运行环境不支持的引用访问 API。

测得复用 Sound 的各轮耗时范围为 `4.721–4.883` → `4.429–5.096` 微秒/请求，Emitter 为 `5.217–8.783` → `4.758–4.908` 微秒/请求。Editor 调度和原生音频桥接影响这些短区间，不设置硬耗时阈值，也不以此承诺稳定加速。首次初始化、Source 创建、集合增长、组件首次缓存委托与 Action 池增长单列，不混入稳定结果。以上仅是当前 Editor 验收，不能替代 Player/IL2CPP 设备实测或实际试听。

### JSON 与源码迁移

Unity 2022.3 的 `JsonUtility.FromJson<AudioSoundOptions>` 对缺字段使用 `default(T)`，不执行无参构造或字段初始化器；部分 JSON 缺 Pitch 会得到非法 0，不会自动获得类默认值。部分 JSON 推荐覆盖一个已创建的默认实例：

```csharp
var options = AudioSoundOptions.Defaults; // 音乐使用 MusicDefaults
UnityEngine.JsonUtility.FromJsonOverwrite("{\"Volume\":0.5}", options);
AudioKit.PlaySound(soundClip, options);
```

完整旧 JSON 保留原字段格式。显式 Pitch=0 仍非法，Volume=0 仍合法，不按零值猜缺字段。原 `Nullable<AudioSoundOptions>` 不再适用，原值类型可选 `AudioSoundOptions?` 参数改为 `AudioSoundOptions options=null`；`PlaySoundMode` 字段仍是可空枚举。依赖值复制、值相等、反射签名或生成绑定的使用方需更新，外部预编译程序集必须重新构建；不承诺二进制 ABI 稳定。

## 模块定位

AudioKit 只播放外部提供的 `AudioClip`，可选接入 Unity Audio Mixer。音乐支持最多两轨 Crossfade（Linear/EqualPower，API 默认 EqualPower），人声为独占槽位并支持 2D、固定世界位置和跟随 Transform；音效支持总量/每 Clip 并发上限、请求优先级抢占、独立闲置缓存预算与显式预热、会话统计、同步开始回调、均匀/加权随机、空间播放及按 Clip 身份的帧窗口去重。控制、单请求淡变与只读进度统一使用代次安全的 `AudioHandle`，自动首尾淡变由 `AudioSoundOptions.AutoFade` 配置。分类开关与音量保留六个 PlayerPrefs 键和合法存档；临时静音、并发/Clip/缓存配置、统计、淡变与 Mixer 路由不持久化。两级并发默认不限、Priority=0、闲置缓存=10 且不自动预热，组件 Crossfade 与自动淡变默认关闭。旧组件保留有效配置，无需批量重存；Options 类与 API 迁移见上文，回调签名迁移及高级生命周期见下文。

- 不加载名称、不缓存资源、不卸载或销毁 Clip；资源系统或业务所有者负责加载和释放。
- 不提供旧 Loader、裸播放器、Fluent 构建器或 Audio 专属 Binding/注销包装，不保留旧 API 兼容层。
- Mixer 仅提供可选输出路由、显式暴露参数控制和 Snapshot 切换；句柄淡变控制单请求包络，与音乐 Crossfade 独立叠乘。不提供逻辑分组预算、重要音效无限豁免、距离/可听度抢占、音源虚拟化、自动 Ducking、Mixer 参数淡变调度、DSP 调度、防重复随机、HRTF 或遮挡。
- namespace 为 `YLFramework.Services.Audio`，与 Action 共用 `YLFramework.Runtime`；音效动作桥接内建，不是可拆卸程序集。

## 目录与依赖

路径相对 `Assets/YLFramework/`：

| 位置 | 职责 |
|---|---|
| [`Runtime/Audio/AudioKit.cs`](../../Runtime/Audio/AudioKit.cs) | 播放、设置、槽位控制、去重/Clip/缓存预算与统计查询 |
| [`AudioHandle.cs`](../../Runtime/Audio/AudioHandle.cs) | 播放代次与所属宿主生命周期校验、控制与只读进度 |
| [`Config/`](../../Runtime/Audio/Config/) | 集中存放设置、请求参数及可序列化配置类型；命名空间与 Runtime 程序集不变 |
| [`Config/AudioSoundOptions.cs`](../../Runtime/Audio/Config/AudioSoundOptions.cs) | 音效、空间语音及 Music/Voice WithOptions 的公开可变配置类、独立 Defaults 实例与校验入口 |
| [`Config/AudioSoundConcurrency.cs`](../../Runtime/Audio/Config/AudioSoundConcurrency.cs) | Sound 总量/默认 Clip 上限及 RejectNew/StopOldest/StopLowestPriority 策略 |
| [`AudioSoundStats.cs`](../../Runtime/Audio/AudioSoundStats.cs) | 不分配集合的 Sound 会话统计值快照与资源拥有数 |
| [`Config/AudioAutoFade.cs`](../../Runtime/Audio/Config/AudioAutoFade.cs) | 可序列化首尾片段秒配置，零值关闭、短片段归一化 |
| [`Config/AudioCrossfadeCurve.cs`](../../Runtime/Audio/Config/AudioCrossfadeCurve.cs) | Music Crossfade 的 Linear/EqualPower 曲线选择 |
| [`Config/AudioWeightedClip.cs`](../../Runtime/Audio/Config/AudioWeightedClip.cs) | 可序列化 Clip/Weight 配对项，供纯加权选择及播放使用 |
| [`Config/AudioOutputRoute.cs`](../../Runtime/Audio/Config/AudioOutputRoute.cs) | 单次播放的继承、指定 MixerGroup、直接输出三态路由 |
| [`Config/AudioKitSettings.cs`](../../Runtime/Audio/Config/AudioKitSettings.cs) | 基于 YL Binding 的六个持久化属性与 Audio 音量校验、临时静音及默认输出组 |
| [`Config/AudioMixerConfig.cs`](../../Runtime/Audio/Config/AudioMixerConfig.cs) | 可选的分类 Mixer 路由配置资产 |
| [`Mixer/`](../../Runtime/Audio/Mixer/) | 独立静态 AudioMixerControl 操作逻辑 |
| [`Internal/`](../../Runtime/Audio/Internal/) | AudioManager 宿主、AudioVoice 实例、SoundDeduplicator |
| [`Internal/AudioPlaybackOptions.cs`](../../Runtime/Audio/Internal/AudioPlaybackOptions.cs) | 内部播放值快照、集中有效默认值、公开配置捕获与校验；不新增公开配置类型 |
| [`Components/`](../../Runtime/Audio/Components/) | AudioEmitter / AudioMusicPlayer 播放组件与 AudioMixerController 薄配置组件 |
| [`Actions/`](../../Runtime/Audio/Actions/) | PlaySoundAction / PlaySoundExtension |
| [`Diagnostics/`](../../Runtime/Audio/Diagnostics/) | 被动诊断；运行 GUI 仅 Editor/Development Build |
| [`Editor/Audio/`](../../Editor/Audio/) | 分层 Inspector、Debug 编辑器窗口与 Help 提供方 |

核心链直接复用 `YLFramework.Core.Pool`、`YLFramework.Binding`、`YLFramework.Events` 和 `MonoSingletonHost<AudioManager>`。没有新增程序集或第三方音频库；共享 Runtime 仍沿用框架既有依赖，不表示零依赖安装包。完整依赖见 [架构总览](../Architecture.md)。

## 播放与句柄

```csharp
using YLFramework.Services.Audio;

// Music 默认循环，Voice 默认不循环；同 Clip 的显式新请求也替换旧请求。
AudioHandle music = AudioKit.PlayMusic(musicClip, loop: true, volume: 0.6f);
AudioHandle voice = AudioKit.PlayVoice(voiceClip,
    onFinished: (clip, handle) => UnityEngine.Debug.Log($"Voice finished: {clip.name}, valid: {handle.IsValid}"));

var options = AudioSoundOptions.Defaults;
options.Volume = 0.8f;
options.Pitch = 1.1f;
options.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
AudioHandle sound = AudioKit.PlaySound(soundClip, options,
    (clip, handle) => UnityEngine.Debug.Log($"Sound finished: {clip.name}, time: {handle.Time}"));

float seconds = sound.Time;
float duration = sound.Duration;
float progress = sound.Progress;

sound.Pause();
sound.SetVolumeScale(0.5f);
sound.SetPitch(1.2f);
sound.Resume();
sound.Stop();
```

| 入口 | 返回/行为 |
|---|---|
| `PlayMusic(clip, loop=true, volume=1, onStarted=null, onFinished=null, outputRoute=default)` | 硬替换所有旧音乐并取消 Crossfade，返回句柄 |
| `PlayVoice(clip, loop=false, volume=1, onStarted=null, onFinished=null, outputRoute=default)` | 独立的人声槽位，返回句柄 |
| `PlayMusicWithOptions(clip, options, onStarted=null, onFinished=null)` | 2D 音乐，options 位置必填；null 循环，显式 Defaults.Loop=false |
| `CrossFadeMusic(clip, duration, options=null, onStarted=null, onFinished=null, ignoreTimeScale=true, curve=EqualPower)` | 最新目标与旧曲最多双轨过渡；curve 可显式 Linear，options 省略时 Loop=true，0 时长立即替换 |
| `PlayVoiceWithOptions(clip, options, onStarted=null, onFinished=null)` | 2D 人声，options 位置必填，null 使用 Defaults；支持 Pitch、Volume、OutputRoute 和 AutoFade |
| `PlayVoiceAt(clip, position, options=null, onStarted=null, onFinished=null)` | 固定世界位置的 3D 人声，替换当前 Voice 请求 |
| `PlayVoiceFollowing(clip, target, options=null, onStarted=null, onFinished=null)` | 跟随激活 Transform 的 3D 人声，无效目标不替换当前请求 |
| `PlaySound(clip, options=null, onFinished=null, onStarted=null)` | 2D 音效；省略 options 使用 Defaults |
| `PlayRandomSound(clips, options=null, onFinished=null, onStarted=null)` | 有效 Clip 中均匀选择，空/全无效返回无效句柄 |
| `PlayWeightedRandomSound(clips, options=null, onFinished=null, onStarted=null)` | 有效且正权重 Clip 中加权选择一次，沿用 Sound 去重、并发与回调 |
| `TrySelectWeightedRandomClip(clips, out clip)` | 纯选择，返回 bool；不播放、不初始化 Settings 或宿主，可接 3D/Voice |
| `PlaySoundAt(clip, position, options=null, onFinished=null, onStarted=null)` | 固定世界位置的 3D 音效 |
| `PlaySoundFollowing(clip, target, options=null, onFinished=null, onStarted=null)` | 跟随有效、激活的 Transform，无效目标拒播 |
| `Music` / `Voice` | 最新目标 Music / 当前 Voice 句柄；读取空状态不创建宿主 |
| `SoundCount` | 有效音效请求数，包含暂停、静音和循环请求，不等于正在发声数 |
| `SoundConcurrency` | 整体读写运行时并发配置，Limit/DefaultClipLimit 默认 0 表示不限，默认 RejectNew |
| `SetClipSoundLimit(clip, limit)` / `RemoveClipSoundLimit(clip)` / `ClearClipSoundLimits()` | 按 Clip 实例覆盖/恢复默认/清覆盖；Remove 返回 bool，规则变严即时裁剪 |
| `GetSoundCount(clip)` / `GetEffectiveClipSoundLimit(clip)` | 被动查询实际 Clip 请求数/有效上限，无效 Clip 返回 0 |
| `SoundCacheCapacity` / `PrewarmSounds(cachedCount)` | 独立闲置缓存预算默认 10；显式补足目标闲置数，返回新增缓存数 |
| `SoundStats` / `ResetSoundStats()` | 被动 readonly AudioSoundStats 快照/显式清累计，不创建 Settings 或宿主 |
| `GetPlayingSounds()` | `AudioHandle[]` 快照，仅显式查询时分配 |
| `CopyPlayingSounds(List<AudioHandle>)` | 清空并填充调用方列表，可复用容量 |

- `IsValid` 表示请求有效；`IsPlaying` 表示 Source 正在播放，静音时也可为 true，不代表可听见；`IsPaused` 表示手动暂停。
- default、拒播、已结束、旧租用或旧会话句柄的 `Pause/Resume/Stop/SetVolumeScale/SetPitch/FadeIn/FadeOut` 安全无效，不验证参数、不初始化宿主。内部 AudioVoice 和 AudioSource 不公开给业务控制。
- `SetVolumeScale(scale)` 只改变当前请求倍率，不改变分类音量或存档；Source 音量为 `Clamp01(当前分类音量 * scale) * gain * transitionGain`。请求包络 gain 默认 1，未参与音乐过渡时 transitionGain 也为 1，旧行为不变。倍率可大于 1，保留原倍率而不把截断后的结果当倍率，0 仅静音、不停止播放。
- `SetPitch(pitch)` 立即更新当前请求音调。两个修改方法适用于 Sound、Music、Voice，不重启播放、不改变手动暂停或请求代次；Music/Voice 关闭期间也可修改，重新开启使用最新参数。池复用不会继承旧请求的修改。
- 有效句柄的倍率必须非负且有限，Pitch 必须有限且在 `(0,3]` 内，非法参数抛 `ArgumentOutOfRangeException`，不改变当前参数。校验与初始播放参数一致。
- `onStarted/onFinished` 均为 `Action<AudioClip, AudioHandle>`，回调参数顺序为 `(clip, handle)`。五个 Sound 入口在原 `onFinished` 之后追加可选 `onStarted=null`；原完成回调的位置、名称与含义不变。Options 参数统一为引用类型，原值类型 Nullable 签名需迁移；简单 Music/Voice 与句柄方法不因类改造新增重载。不保留旧 Sound 签名包装或无参回调兼容层。
- `onStarted` 在真正启播时同步执行，可能在播放方法返回前触发；应使用回调传入的 handle，而非尚未完成赋值的外部变量。Music/Voice 等待开关开启时延迟启播，重新开启不重复已触发的开始回调。
- 进入自然完成 `onFinished` 时原 handle 有效且 `IsPlaying` 为 false，返回后清理原代次；回调内主动 Stop、替换或 Shutdown 可立即使它失效。不保证完成回调中 `Progress` 为 1，也不保留完成历史。
- 开始/完成回调各每个请求最多一次；循环、Pause/Resume 和开关重播不重复开始通知，Mute 不取消回调。显式 Stop、替换、抢占、裁剪、拒播、关闭、目标丢失及 Shutdown 不触发自然完成；循环请求必须明确停止。回调重入创建的新请求不会被旧代次清理误停。
- `StopMusic/PauseMusic/ResumeMusic` 控制目标及旧尾轨和过渡，Voice 对应方法及 `StopAllSound` 控制各自共享状态，空状态不创建宿主。普通组件停止自己持有的句柄，不能随意停全局音乐。
- 调用方必须保证 Clip 在播放、暂停或等待重播期间存活。停止、替换、自然结束及 Shutdown 后 Audio 断开引用，但不转移资源所有权。

`Time/Duration/Progress` 都是只读 `float`，适用于 Music、Voice、Sound，包括空间播放：

| 属性 | 契约 |
|---|---|
| `Time` | 当前 Clip 内位置（秒），已启播时读取 `AudioSource.time` 并限制在 `[0, Duration]` |
| `Duration` | 本次有效请求的 `clip.length`（秒），不是受 Pitch 影响后的实际播放耗时 |
| `Progress` | `Duration > 0` 时为 `Clamp01(Time / Duration)`，否则为 0 |

default、拒播、过期代次、Stop/替换/完成/Shutdown 后，以及所需 Source 或 Clip 缺失/已销毁时三项均为 0；读取不初始化 Settings 或宿主。Music/Voice 在关闭通道下等待启播或重播时，`Time/Progress` 为 0，`Duration` 仍为有效 Clip 长度。手动 Pause 冻结位置，Resume 续播；Mute 不冻结、不回退位置。循环只展示当前轮，可能从接近 1 回到 0；不支持 seek 或历史查询。

Unity 的 `AudioSource.time` 对压缩音频可能只有分包级精度，不承诺采样级或节拍级同步，也不能用自然完成时读到 1 作为通知条件。Audio 不用 Timer 或累加 deltaTime 模拟播放进度。

回调迁移与同步开始示例：

```csharp
// 旧：AudioKit.PlaySound(soundClip, onFinished: () => UnityEngine.Debug.Log("finished"));
AudioKit.PlaySound(soundClip,
    onFinished: (clip, handle) => UnityEngine.Debug.Log($"Finished: {clip.name}, valid: {handle.IsValid}"),
    onStarted: (clip, handle) => handle.SetVolumeScale(0.8f));

// 不捕获方法返回后才赋值的 music 变量；直接控制本次回调中的 handle。
AudioHandle music = AudioKit.PlayMusic(musicClip,
    onStarted: (clip, handle) => handle.Pause());
```

### Sound 开始提交与签名迁移

Sound 开始回调在 `Source.Play` 之后、方法返回前同步触发。此时实际 Clip、Options、Pitch、Mixer 路由及自动初始增益已配置，原 lease 有效，`SoundCount` 已包含自身，去重记录已经提交。因此回调内再次播放同 Clip 仍受到去重限制；无效 Clip/目标、开关关闭、去重或容量拒绝均不调用开始/自然完成回调。

回调可停止自身、StopAllSound、改变并发、递归播放、停用组件、Reset/Deinit Action 或 Shutdown。外层返回的是本次原 lease，可能已失效，不能用复用同一 Voice 的新请求替换它。开始回调抛异常时原样抛出，仅按本次 Voice+Generation 释放仍归自己的请求，并回滚仍归本次 lease 的去重记录；不清整个窗口，不删除嵌套请求记录，也不误停复用 Voice 的新声音。回调正常返回但主动停止自身仍算成功开始，保留成功播放的去重记录。原生自然完成仍按旧语义等待 Update 检测，不为释放容量提前通知。

普通位置调用 `PlaySound(clip, options, onFinished)` 和既有命名调用仍可编译，原第三参数不会变为开始回调。固定 `GetMethod(..., parameterTypes)`、委托绑定、生成桥接及预编译程序集需加入末尾 `Action<AudioClip, AudioHandle>` 或重新构建；Action/sequence 对应迁移见下文。不能将源码可选参数兼容描述为“所有旧签名不变”或二进制兼容。

## 单请求淡入淡出

既有 `AudioHandle` 手动方法、参数及默认值保持不变，不改分类设置或 Mixer 路由；Sound 开始回调的追加与 Options 类改造独立于这些句柄方法：

```csharp
public void FadeIn(float duration, bool ignoreTimeScale = true);
public void FadeOut(float duration, bool stopWhenDone = true, bool ignoreTimeScale = true);
```

```csharp
// Play 返回后同帧设置；不需要 Tween 或另一个调度宿主。
var sound = AudioKit.PlaySound(soundClip);
sound.FadeIn(0.25f);

// 稍后从当前包络淡出，完成时停止，不通知自然结束。
sound.FadeOut(0.5f, stopWhenDone: true);

// 音乐也可使用同步开始回调中的本次 handle。
AudioKit.PlayMusic(musicClip,
    onStarted: (clip, handle) => handle.FadeIn(0.5f));
```

- 包络 gain 独立于分类音量、`Options.Volume` 与 `SetVolumeScale`；无音乐过渡时 Source 为 `Clamp01(category * requestScale) * gain`，Crossfade 再叠乘独立 transitionGain，不把 gain 写回倍率、Settings 或存档。
- 尚未经历任何淡变的新请求，首次调用正时长 `FadeIn` 时立即设 gain 为 0，从 0 向 1；经历过任何淡变后（包含零时长操作），`FadeIn` 从当前包络向 1，不在重复调用时再次跳零。`FadeOut` 从当前包络向 0；新淡变替换旧淡变，不叠加调度。
- 有效句柄的 duration 必须有限且 >=0，非法值抛 `ArgumentOutOfRangeException` 且不改现有状态。0 立即到目标；`FadeOut(0, stopWhenDone: true)` 立即 Stop。`stopWhenDone: false` 到 0 后保留有效请求，可再次淡入。
- default/过期句柄 no-op，连非法 duration 也不验证，与 `SetVolumeScale` 一致，不初始化 Settings 或宿主。
- 默认 `ignoreTimeScale=true` 使用 unscaled 时间；显式 false 使用 scaled 时间。手动 Pause、Music/Voice 通道 off 或未真正启动冻结包络和剩余淡变；Mute 仍推进。
- 音乐/语音关开保留当前包络和剩余淡变，Clip 仍按旧语义从头重播；手动 Pause 不被开启恢复。Sound off 直接 Stop，取消淡变且不复活。
- Stop、自然结束、替换、target 丢失、Shutdown 与池回收均清淡变。淡出 Stop 不调用自然结束 callback；短 Clip 在淡变完成前自然结束时，自然结束优先，正常调用 callback。

淡变基于现有 `AudioManager.Update`，不是 DSP 调度或采样级自动化。同帧调用/同步开始回调不保证首采样已静音、采样级精度或无缝 Crossfade；不增加 Tween 依赖、新宿主或持久化项。

## 音乐 Crossfade

```csharp
public static AudioHandle CrossFadeMusic(AudioClip clip, float duration,
    AudioSoundOptions options = null,
    Action<AudioClip, AudioHandle> onStarted = null,
    Action<AudioClip, AudioHandle> onFinished = null,
    bool ignoreTimeScale = true, AudioCrossfadeCurve curve = AudioCrossfadeCurve.EqualPower);

AudioKit.PlayMusic(menuMusic);
AudioHandle target = AudioKit.CrossFadeMusic(battleMusic, 1.5f);
AudioKit.PauseMusic(); // Both tracks and the shared transition clock.
AudioKit.ResumeMusic();
AudioKit.StopMusic(); // No remaining hidden tail.
```

- `options=null` 使用与 MusicDefaults 一致的内部值默认（Loop=true）；显式传入 Defaults 则 Loop=false，尊重传入 Loop。提交捕获值快照，不修改或复制原配置类对象。支持 Volume/Pitch/OutputRoute/AutoFade，忽略空间字段、Sound 去重策略和 Priority，不参与 Sound 预算或抢占。
- duration 须有限且 >=0，0 是立即替换。无效/已销毁 Clip 返回 default，不改变现有音乐；合法 Clip 的配置、路由与自动包络元数据在停止任何旧曲或分配 Source 前校验。
- `AudioKit.Music` 立即代表最新目标，不代表响度最大的曲目；旧 outgoing lease 在实际退场前仍有效，不会重新提升为主曲。最多两条 Music Source，第二条首次需要时才创建，退场后可闲置复用；不使用 Sound 池或并发额度，Shutdown 释放。
- 默认 unscaled 时间，`ignoreTimeScale=false` 随 scaled 时间。`u=Clamp01(elapsed/duration)`；Linear 下 outgoing 过渡增益为 `capturedOutgoingGain*(1-u)`、incoming 为 `u`，EqualPower 下 outgoing 为 `capturedOutgoingGain*cos(u*pi/2)`、incoming 为 `sin(u*pi/2)`。正时长 incoming 在 Source.Play/onStarted 前 transitionGain=0，无旧曲也从零淡入；结束时目标增益为 1，停止旧曲且不通知其自然完成。
- `Source.volume = Clamp01(category * requestScale) * requestEnvelopeGain * transitionGain`。每个请求的手动/自动包络继续独立工作并与过渡叠乘；手动 FadeIn/FadeOut 接管自动包络时不取消 Crossfade。未参与 Crossfade 的请求 transitionGain=1。等功率曲线不保证等响度，也不提供 DSP 或节拍同步保证。
- 最新请求优先、不排队。A->B->C 时先停止 A，B 从当前 transitionGain 退到 0，C 从 0 到 1，最多保留两条；快速切歌可能截断最早尾轨及降低响度，不承诺三曲无损过渡。相同 Clip 也是重开的新请求，不复用播放位置。
- 旧 `PlayMusic/PlayMusicWithOptions` 始终硬替换全部旧音乐并取消过渡。`StopMusic` 清两条；`PauseMusic/ResumeMusic` 暂停/恢复两条及剩余计时，不重启。直接 lease.Pause/Resume/Stop 仅控制自身音频，但任一有效参与请求暂停会冻结共同过渡时钟，另一条音频不因此自动暂停。
- 暂停中请求 Crossfade，新目标继承暂停等待状态，ResumeMusic 才首次启播和通知。旧 PlayMusic* 替换仍不继承暂停。Music off 取消过渡、停止旧尾轨，仅保留目标并恢复 transitionGain=1；on 按旧规则从头重播，已发 onStarted 不重复。off 期间 Crossfade 只替换待播放目标，不保留旧轨或启动过渡。Mute 不冻结时钟。
- Music 分类音量、静音及默认 Mixer 路由实时作用于两条；它们各自的显式覆盖/Direct 路由仍独立。尾轨仍属于 Music，不使用 Voice 设置。
- outgoing 已逻辑替换，取消其自然完成通知，即使原生先结束也不通知。新目标若非循环并在过渡结束前自然完成，正常通知自身一次并清理；旧尾轨继续剩余退场，不复活旧曲。新目标主动 Stop/手动淡出停止/资源丢失也不恢复旧曲；一侧失效不再冻结另一侧，两侧失效清过渡。
- 新目标状态、两条 lease 和过渡 epoch 在开始回调前提交。回调异常原样抛出，只清仍归本次操作拥有的请求及过渡，不恢复被替换音乐；重入的新 Crossfade/普通音乐不受旧返回路径、异常清理或 Update 快照影响。

### 过渡曲线

`CrossFadeMusic` 末尾可选参数 `curve` 选择过渡增益映射，API 默认 `AudioCrossfadeCurve.EqualPower`；显式 `Linear` 仍可用，枚举零值仍为 Linear，不改已有序列化枚举数值。未知枚举在 duration 校验之后、任何副作用之前抛 `ArgumentOutOfRangeException`。可选默认值由 C# 调用方编译时捕获，使用新缺省曲线的调用方需要重新编译；已有显式 Linear 调用不改。固定签名反射、委托绑定、生成绑定与预编译使用方仍需核对真实签名。

| 曲线 | incoming | outgoing | 满增益中点 |
|---|---|---|---|
| `Linear` | `u` | `capturedOutgoingGain*(1-u)` | 各 0.5，平方和≈0.5 |
| `EqualPower` | `sin(u*pi/2)` | `capturedOutgoingGain*cos(u*pi/2)` | 各≈0.7071，平方和≈1 |

- EqualPower 更接近“强度接近且不相关”两首素材的恒定功率；不等于等响度，相关/同相素材仍可能叠加峰值，素材响度匹配与试听仍由业务负责。
- outgoing 始终缩放 `capturedOutgoingGain` 而非重排曲线，连续切歌时从当前过渡增益连续退场，不跳变。
- `duration=0` 立即替换，曲线不参与；`AudioMusicPlayer` 的 crossFadeCurve 字段初始化保留 Linear 以兼容旧缺字段资源，编辑器新建/Reset 设置 EqualPower，仅 Crossfade 开启时生效；Reset 不开启 Crossfade。

## 自动首尾淡变

`AudioAutoFade` 是 `[Serializable]` struct，只有可写公开字段 `float FadeInSeconds`、`float FadeOutSeconds`。构造 `AudioAutoFade(float fadeInSeconds, float fadeOutSeconds)` 仅赋值，不校验；播放入口统一校验。`default`/两项为 0 表示关闭，`AudioSoundOptions.Defaults.AutoFade` 也是全零，不改变旧播放行为，不增加 Enabled、timeScale 或自动 Stop 开关。

```csharp
var options = AudioSoundOptions.Defaults;
options.AutoFade = new AudioAutoFade(2f, 3f);
AudioKit.PlaySound(soundClip, options,
    onFinished: (clip, handle) => UnityEngine.Debug.Log(clip.name));

// options 位置必填，允许 null；显式 Defaults.Loop=false，PlayMusic 默认 true。
AudioKit.PlayMusicWithOptions(musicClip, options);
AudioKit.PlayVoiceWithOptions(voiceClip, options);

// 循环只在首次启动淡入；FadeOutSeconds 必须为 0。
options.Loop = true;
options.AutoFade = new AudioAutoFade(1f, 0f);
var music = AudioKit.PlayMusicWithOptions(musicClip, options);
// 后续由当前 gain 接管；手动 duration 是原有的 unscaled/scaled 秒。
music.FadeOut(0.5f);
```

```csharp
public static AudioHandle PlayMusicWithOptions(AudioClip clip, AudioSoundOptions options,
    Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null);
public static AudioHandle PlayVoiceWithOptions(AudioClip clip, AudioSoundOptions options,
    Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null);
```

两个 WithOptions 入口只提供 2D Music/Voice，仍硬替换当前分类请求，Music 同时取消旧 Crossfade；支持 Loop、Volume、Pitch、OutputRoute、AutoFade，空间字段不生效，Sound 去重策略和 Priority 不应用于 Music/Voice。Sound、At/Following Sound 与 Voice、等权/加权随机均在提交时捕获 options 的内部值快照，其中 Priority 仅作用于 Sound；随机仅抽选一次，按实际选中 Clip 长度归一化，不按列表首项推测，不因拒播重抽。

### 片段时钟与归一化

首尾时长为片段时间而非现实等待秒数。自动包络读取 `AudioSource.timeSamples / (double)clip.frequency`，长度 L 为 `clip.samples / (double)clip.frequency`，位置 t 限制到 `[0,L]`。Pitch=2 时实际淡变耗时减半，运行中改 Pitch 只改变位置推进速度，不重新计时；`Time.timeScale` 不参与，没有 ignoreTimeScale 参数。此内部时钟不改变既有 `AudioHandle.Time/Duration/Progress` 的语义。

非循环配置 x/y 在 x+y>L 时使用 double 加法与同比缩短；两侧不重叠，理论交接点 gain=1，原来为零的一侧仍为零。有限正值不以任意阈值当成关闭：

```text
scale = x+y>L ? L/(x+y) : 1
a = x*scale; b = y*scale
gainIn  = a>0 ? Clamp01(t/a) : 1
gainOut = b>0 ? Clamp01((L-t)/b) : 1
gain = Min(gainIn, gainOut)
Source.volume = Clamp01(category * requestScale) * gain * transitionGain
```

两个字段须有限且非负；`Loop && FadeOutSeconds>0` 明确拒绝，不静默忽略。非零配置还要求有效 samples/frequency、有限且 >0 的 Clip 长度，在分配宿主、提交去重或替换槽位之前校验。纯淡出初始 gain=1；有淡入时在 Source.Play 与 onStarted 前设 gain=0。未启播请求保持初始增益，不消耗包络。

循环只执行首次启动淡入，时长 `min(FadeInSeconds,L)`；到达该时长或检测首轮 PCM 回绕后锁存 gain=1，后续循环不重新淡入。Update 间隔超过整轮时，单靠位置不能可靠识别漏过的循环；短循环与低帧率存在此限制，不引入 DSP 循环调度。更新是帧级，不保证首采样、最后一个采样精确为零或过短包络可被完整观察；压缩/Streaming 音频及目标平台 timeSamples 行为与最终听感需实测。

### 接管与生命周期

- 自动首尾包络只改同一个 gain，不调用 Stop、不改 loop、不提前结束 Clip；自然完成仍按原 Clip/原 lease 最多通知一次。自然完成检测先写 t=L 的最终包络，尾淡出>0 时 gain=0，再进入原有效句柄回调；不能把 Source 已复位的位置 0 当作新淡入。回调重入仍受代次保护，不清理新请求。
- 合法的手动 FadeIn/FadeOut 取消自动控制，从当前 gain 平滑接管，不叠乘第二个请求包络；Crossfade 的 transitionGain 仍独立叠乘。正在播放先采样当前位置，暂停/通道关闭保留最后 gain。非法 duration 不取消自动模式；零时长保持原即时语义。取消后即使 Music/Voice 关开也不复活自动配置，Options 中的配置值不代表运行中仍受自动控制。
- Music/Voice 关开按旧语义从头重播 Clip；仅自动模式仍 active 时重置包络和首轮 loop latch。手动模式保留当前包络及剩余手动淡变。Pause/Resume 不重置，Mute 不冻结，SetVolumeScale、分类音量、Pitch 和路由修改不更换包络控制者。
- Stop、自然结束、替换、跟随 target 丢失、Shutdown 与池回收清除自动状态；旧 lease 控制仍为安全 no-op。手动 FadeOut 的 stopWhenDone=true 主动 Stop 不通知自然完成，false 到零后继续静音，自动尾部不会再次接管。

此功能不增加新宿主、Timer、Mixer 参数、PlayerPrefs 或曲线资产，也不提供运行中设置/恢复自动包络的句柄方法。自动淡变默认关闭，旧组件字段无需迁移；`AudioSoundOptions` 已改为可序列化配置类，源码/ABI 与 JSON 缺字段限制见顶部迁移说明。

## 随机与空间播放

```csharp
var options = AudioSoundOptions.Defaults;
options.MinDistance = 2f;
options.MaxDistance = 30f;
options.RolloffMode = UnityEngine.AudioRolloffMode.Logarithmic;

AudioKit.PlayRandomSound(clips, options,
    onFinished: (selectedClip, handle) => UnityEngine.Debug.Log($"Selected clip finished: {selectedClip.name}"));
var fixedSound = AudioKit.PlaySoundAt(clip, worldPosition, options);
var following = AudioKit.PlaySoundFollowing(clip, target, options);

// Voice 仍为独占槽位，两次调用中后一请求替换前一请求。
var fixedVoice = AudioKit.PlayVoiceAt(voiceClip, worldPosition, options);
var characterVoice = AudioKit.PlayVoiceFollowing(voiceClip, character, options);
```

随机选择忽略 null 和已销毁 Clip，选中后按该 Clip 去重，不为整组共用冷却。随机回调的 clip 是实际选中并播放的对象，handle 对应该次原播放代次，不是列表首项或按名字查找的 Clip。`PlaySound/PlayVoice` 默认 2D，Music 仍仅公开 2D 入口。Sound/Voice 的 At/Following 在启播前设置世界位置、`spatialBlend=1`、距离与衰减；默认 `MinDistance=1`、`MaxDistance=50`、`Linear`、`dopplerLevel=0`。

### 轻量加权选择

`AudioWeightedClip` 是 `[Serializable]` struct，只有公开字段 `AudioClip Clip`、`float Weight` 和构造 `AudioWeightedClip(AudioClip clip, float weight=1f)`。使用配对项而不是两份平行列表：

```csharp
var weighted = new[]
{
    new AudioWeightedClip(commonClip, 3f),
    new AudioWeightedClip(rareClip, 1f)
};
AudioKit.PlayWeightedRandomSound(weighted, options,
    onFinished: (clip, handle) => UnityEngine.Debug.Log(clip.name));

// 纯选择可接任意既有播放入口，不新增加权 3D/Voice 重载。
if (AudioKit.TrySelectWeightedRandomClip(weighted, out var selected))
    AudioKit.PlayVoiceFollowing(selected, character, options);
```

公开签名为 `AudioHandle PlayWeightedRandomSound(IReadOnlyList<AudioWeightedClip> clips, AudioSoundOptions options=null, Action<AudioClip, AudioHandle> onFinished=null, Action<AudioClip, AudioHandle> onStarted=null)` 和 `bool TrySelectWeightedRandomClip(IReadOnlyList<AudioWeightedClip> clips, out AudioClip clip)`。

所有项 Weight 都必须 finite 且 >=0，包括 null/已销毁 Clip 项；任何非法权重抛 `ArgumentOutOfRangeException`。只有存活 Clip 且 Weight>0 参与，使用 double 累计并只抽选一次。null/空/all0 或没有有效候选时无请求，纯选择返回 false、out clip=null；纯选择不初始化 Settings 或宿主。播放在选中之后沿用 Sound 的参数、开关、去重、并发及回调契约，去重或容量拒播不重抽；连续选中同一 Clip 合法，不提供防重复。旧 `PlayRandomSound` 与旧 clips 均匀选择行为保持，开始参数只在原末尾追加。

空间语音复用 `AudioSoundOptions`，默认不循环，独立受 `IsVoiceOn/VoiceVolume/IsVoiceMuted` 控制；`PlaySoundMode/Priority` 对 Voice 忽略，不受音效去重、Sound 总量/Clip 限额、`IsSoundOn/IsSoundMuted`、`StopAllSound` 影响，也不进入 SoundCount/音效快照/统计。仍只有一个 Voice 槽位，不新增多 NPC 并发语音。无效 Clip/目标或非法参数不会替换当前请求。

Voice 关闭时保留完整空间请求。跟随目标在关闭或暂停期间移动仍会更新位置，开启重播前再次校验目标并使用最新位置；目标失效停止并清空请求，不触发自然完成，不因开关开启而复活。固定位置不绑定调用者生命周期；替换为 2D Voice 时重置所有空间参数和位置。开始/自然结束回调及运行中倍率/Pitch 控制沿用 Voice 契约。

参数可从 `new AudioSoundOptions()` 或 `Defaults` 开始，循环音乐从 `MusicDefaults` 开始；`default(AudioSoundOptions)` 为 null，交由入口按分类解析。公开字段为 `Loop/Volume/Pitch/Priority/PlaySoundMode/MinDistance/MaxDistance/RolloffMode/OutputRoute/AutoFade`，字段类型不变；路由默认 Inherit、AutoFade 全零、Priority=0、PlaySoundMode=null 继承全局。Sound 的 Priority 是非负 int；Volume 非负且有限，Pitch 为 `(0,3]`；空间距离有限且满足 `0<=MinDistance<=MaxDistance`、`MaxDistance>0`，衰减仅支持 Linear/Logarithmic。非法数值在分配 Source 和去重登记之前抛 `ArgumentOutOfRangeException`；自动淡变与路由校验见对应章节。

跟随 Source 不重新父级到业务 Transform，集中更新其世界位置；目标销毁、停用或场景卸载时停止且不触发自然完成。固定位置 one-shot 可以在发起对象销毁后播完，循环由句柄或组件明确停止。池归还与再次分配完整重置空间、位置、音调、倍率、loop、Clip、暂停与回调，防止 3D 状态泄漏到 2D。

Audio 不创建、销毁场景 AudioListener。相机或玩家提供 Listener，缺失时开发环境给出诊断。3D 声音的自动化 Source 参数检查不能替代最终方位、距离和音量听感验证。

## 设置与去重

```csharp
AudioKit.Settings.IsOn = true; // bool：直接读写三个分类开关
AudioKit.Settings.IsMusicOn.Value = true;
AudioKit.Settings.SoundVolume.Value = 0.8f;
AudioKit.Settings.IsMusicMuted.Value = true; // 临时静音，位置继续推进
AudioKit.Settings.IsVoiceMuted.Value = false;
AudioKit.Settings.IsSoundMuted.Value = true;
AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
// 显式业务冷却示例，不是窗口的初始默认值。
AudioKit.GlobalFrameCountForIgnoreSameSound = 10;
AudioKit.SoundFrameCountForIgnoreSameSound = 10;
```

六个 PlayerPrefs 键、默认开关 true 与默认音量 1 保持不变：

| 分类 | 开关键 | 音量键 |
|---|---|---|
| Sound | `KEY_AUDIO_MANAGER_SOUND_ON` | `KEY_AUDIO_MANAGER_SOUND_VOLUME` |
| Music | `KEY_AUDIO_MANAGER_MUSIC_ON` | `KEY_AUDIO_MANAGER_MUSIC_VOLUME` |
| Voice | `KEY_AUDIO_MANAGER_VOICE_ON` | `KEY_AUDIO_MANAGER_VOICE_VOLUME` |

上述六个持久化属性沿用 `YLFramework.Binding.PlayerPrefsBoolProperty/PlayerPrefsFloatProperty` 公开类型，分类音量在 Audio 局部校验。Load/Reload 遇到负数、NaN 或 Infinity 在内存回退 1，不自动重写坏存档；合法 0 和大于 1 的倍率原样保留，Delete 恢复缺省且不写回。通过 `.Value`、`TrySetValue` 或 `SetValueWithoutCallback` 赋非法音量均抛 `ArgumentOutOfRangeException`，不改变内存、存档、通知或播放状态。合法 `.Value` 变化先持久化再通知；`SetValueWithoutCallback` 只改内存，不通知或持久化。UI 使用 `Register/RegisterWithInitValue` 返回的 YL Events 令牌管理订阅；Shutdown 对旧 Settings 执行 `UnRegisterAll`。没有旧 Settings 的 Init/Deinit/Initialized，也不修改 Binding 全局规则或 comparer。

`IsMusicMuted/IsVoiceMuted/IsSoundMuted` 是 `BindableProperty<bool>`，通过 `.Value` 修改并可沿用同一订阅模式，默认均为 false，仅保留在当前运行时 Settings，不增加 PlayerPrefs 键或主动 Save。Shutdown 清理订阅；重建 Settings 后恢复 false，旧 Settings 的修改不影响新宿主。

- Mute 只控制对应分类的 Source 静音，与 `IsOn`、分类开关及 Volume 独立，不把分类音量或请求倍率改为 0，也不提供全局 Mute 或单句柄 Mute。
- 静音期间仍接受符合开关/去重/并发规则的播放，位置与跟随正常更新，自然结束及回调照常发生；解除静音从当前位置继续，不重播。暂停时修改 Mute 不改变暂停状态或播放代次。
- 静音期间修改分类音量或请求倍率后，解除静音使用最新的 `Clamp01(分类音量 * 请求倍率) * gain * transitionGain`（无过渡时为 1）。Mute 不冻结请求包络或音乐过渡；新请求与通道重新开启在启播前应用当前分类 Mute，既有关闭/拒播/重播行为不变。
- Music/Voice 关闭停止 Source 但保留完整、仍可 Stop 的请求；重新开启从头重播原 Clip、loop、最新倍率/Pitch 与未触发的回调，开始回调不重复。手动模式保留当前包络/剩余淡变，仍 active 的自动模式重置初始包络与首轮 latch；已被手动取消的自动模式不恢复。
- 关闭期间新请求替换待播放项，即使 Clip 相同也生成新代次。显式 Stop、自然结束和替换清空旧请求，开关不能复活；手动 Pause 不被开启强制恢复。
- Sound 关闭停止全部音效，关闭期间新请求拒绝，不自动恢复。
- Music 有 Crossfade 时，关闭先取消过渡、停止旧尾轨，仅保留最新目标并设 transitionGain=1；其它目标重播/自动与手动包络规则不变。
- 全局模式默认 EveryOne，不去重，但不能绕过并发上限；GlobalFrames 使用所有声音共享窗口起点，SoundFrames 按 Clip 实例身份独立记录。两个运行时窗口默认 5 帧，仅在对应去重模式下生效；显式设置 0 时仅同帧去重，EveryOne 才关闭去重。窗口边界包含最后一帧，不是秒数，显式 10 帧等业务覆盖仍有效。
- 仅接纳成功后提交记录，且在开始回调前完成，失败请求不消耗窗口。独立记录自然结束按句柄代次移除，较早播放结束不删除较晚记录；Stop 不额外改变窗口，过期记录定期淘汰。开始回调异常仅回滚仍归本次 lease 的记录，Global 共享窗口同样受归属保护。

## Sound 并发与优先级

```csharp
// AudioSoundConcurrency is a [Serializable] struct with public writable fields.
AudioKit.SoundConcurrency = new AudioSoundConcurrency
{
    Limit = 24,
    DefaultClipLimit = 3,
    OverflowPolicy = SoundOverflowPolicy.StopLowestPriority
};

AudioKit.SetClipSoundLimit(explosionClip, 2);
AudioKit.SetClipSoundLimit(uiClip, 0); // Overrides the default Clip limit, not the global limit.
var options = AudioSoundOptions.Defaults;
options.Priority = 10;
AudioKit.PlaySound(importantClip, options);
```

公开字段为 `int Limit`、`int DefaultClipLimit` 与 `SoundOverflowPolicy OverflowPolicy`，枚举为 `RejectNew=0`、`StopOldest=1`、`StopLowestPriority=2`，旧数值和语义不变。零值为两项限额 0（unlimited）和 RejectNew。`AudioKit.SoundConcurrency` 是运行时整体读写属性，读取配置副本，不是 BindableProperty；先完整校验再初始化/应用，负 Limit/DefaultClipLimit 或未知 enum 抛参数异常，配置和现有请求都不改变。配置可初始化原 Settings/去重器，但不创建 AudioManager、Source 或 Listener；无新 Settings、PlayerPrefs、配置资产或 Resources。Shutdown/新会话恢复零值，旧宿主不影响新会话。

只约束有效 Sound 请求，包含暂停、Mute、Loop；不限制 Music/Voice，不是 Unity 真实/虚拟 voice 数或所有场景原生 AudioSource 的全局上限。所有 Sound 入口、Emitter 和 Action 共用接纳闸门：选择一次实际 Clip -> 输入/目标/路由/包络/Priority 校验 -> 开关 -> 原去重检查（不提交）-> 总量/Clip 联合容量判定 -> 必要抢占唯一受害者 -> 配置并登记有效请求/Clip 计数/Priority/接纳序号 -> 提交去重与统计 -> Source.Play/onStarted。

- RejectNew 满额返回无效句柄，不分配 Source、不提交去重、不触发回调、不停止旧请求。
- StopOldest 满额停止候选范围内最早接纳的有效 Sound，再接纳新请求；同帧按单调接纳序号排序，不按进度或 HashSet 遍历。暂停、静音和循环也可被停止，旧句柄失效，不触发自然完成。RejectNew/StopOldest 不因 Priority 增加保护。
- StopLowestPriority 在候选范围选最低 Priority，同级选最旧；incoming.Priority 低于候选时拒播，同级或更高允许替换。Priority 为非负 int，0 普通、数值越大越重要，支持 int.MaxValue；接纳时捕获，不随音量/Pitch/暂停/Mute 改变。这不是 Unity `AudioSource.priority`，不映射其不同方向的原生可听度优先级。Music/Voice/Crossfade 忽略 Sound Priority。
- 仅全局满额时，候选范围为全部有效 Sound；仅 Clip 满或两项同时满时，只能选同一 Clip，一次接纳最多停止一条。不能先停其它 Clip 再为当前 Clip 额外停一条。
- 降低任一有效限额立即裁剪，先各 Clip 再全局以避免多停。RejectNew 保留较早请求、停止最新；StopOldest 保留较新请求、停止最旧；StopLowestPriority 保留高优先级、同级保较新，裁剪不需要 incoming 比较。0 或提高有效上限不停止请求，切换策略但已符合额度时不裁剪。
- 原生已结束但尚待 Update 清理的有效请求仍占额度；不为了腾容量提前 CheckFinished 或触发用户回调。自然结束、Stop、Sound off、目标失效、回收与 Shutdown 都释放额度，不依赖自然结束回调计数。
- EveryOne 只绕过去重，仍受两级容量；任何 Priority 都不能绕过帧窗口，去重拒播不抢占。等权/加权先抽一次，容量拒绝不重抽、不提交或延长窗口；随后释放容量可在同帧重试，仍受已有成功记录约束。抢占/裁剪使等待中的 Action 按原契约在下一次检查完成，不改它的自然结束或取消语义。主动 Stop/抢占/裁剪不清成功播放的冷却，自然完成保留原 SoundFrames/GlobalFrames 区别。

### 每 Clip 覆盖

```csharp
public static void SetClipSoundLimit(AudioClip clip, int limit);
public static bool RemoveClipSoundLimit(AudioClip clip);
public static void ClearClipSoundLimits();
public static int GetEffectiveClipSoundLimit(AudioClip clip);
public static int GetSoundCount(AudioClip clip);
```

覆盖按 AudioClip 实例身份而非名称、路径或候选列表索引登记；同名不同实例独立，同一 Clip 的所有调用者、2D/3D、Mixer 路由和 PlaySoundMode 共享计数。重复权重候选指向同一 Clip 时也共享预算，候选权重仍独立累计。有覆盖用覆盖值，无覆盖用 DefaultClipLimit；显式覆盖 0 与没有覆盖不同，只绕过默认 Clip 限额，不能绕过全局总量。

Set 要求存活 Clip 和 limit>=0，完整校验后登记和即时裁剪，不创建播放宿主。Remove 返回是否移除并恢复默认，Clear 批量清覆盖；恢复更严格默认也可能触发裁剪。两项 getter 被动、不初始化 Settings/宿主，无效 Clip 返回 0；Remove/Clear 在空会话不初始化。规则借用 Clip，不拥有或卸载资源；调用方在释放前移除，Shutdown/新会话清覆盖与计数。

### 缓存预算与真实预热

```csharp
AudioKit.SoundCacheCapacity = 24;
int added = AudioKit.PrewarmSounds(24); // Target idle count; returns newly cached count.
```

`SoundCacheCapacity` 默认 10，0 表示不缓存，负值拒绝；与总量/Clip 活跃上限完全独立，不自动随 Limit 扩容。设置不创建宿主、不预热、不停止活跃请求。缩小立即只释放超额闲置 Voice/Source，活跃源继续播放，归还时按新预算缓存或释放；增大不自动创建。

`PrewarmSounds(int cachedCount)` 返回新增缓存数，参数是目标闲置数，不是追加数，也不包含 active。重复调用只补足目标；0 无操作且不创建宿主，负数或超过 SoundCacheCapacity 在副作用前抛异常，不夹小请求或扩大预算。显式正预热可创建现有宿主及不同的真实 inactive AudioSource，`playOnAwake=false`、无 Clip/路由/目标，不播放、不回调、不占额度、不登记去重、不消费 Random，也不创建 Listener。Sound off 期间可预热，active+idle 可超过活跃上限，这是独立资源预算的有意行为。

### 轻量会话统计

`AudioKit.SoundStats` 返回 `AudioSoundStats` readonly struct。读取和 `ResetSoundStats()` 均被动，不初始化 Settings/宿主，不复制集合或数组，不记录逐请求日志或历史 Clip 表。

| 属性 | 类型/含义 |
|---|---|
| ActiveCount / PeakActiveCount | int，当前有效 Sound 请求数/会话峰值 |
| CachedCount / OwnedSourceCount | int，当前闲置 Voice 数/领域拥有的真实 Sound Source 数 |
| AdmittedCount / StartFailureCount | long，已提交接纳尝试/开始失败；失败不反向扣历史接纳 |
| DedupRejectedCount / GlobalRejectedCount / ClipRejectedCount / PriorityRejectedCount | long，互斥主要拒播原因 |
| StolenCount / TrimmedCount | long，接纳抢占/运行时缩容裁剪 |
| CacheHitCount / CacheMissCount | long，真实播放分配的缓存命中/缺额；预热不伪造播放命中 |
| SourceCreatedCount / SourceReleasedCount | long，真实 Sound Source 创建/发起释放请求；不代表同帧原生已物理销毁 |

一次拒播只计一个主要原因：去重先判；Clip 与全局同时满归 Clip；新 Priority 不足归 Priority，不再加 Clip/global。无效参数、无随机候选与 Sound off 不归容量拒播。onStarted 前当前数、峰值、接纳及去重已提交，回调立即自停不抹峰值；开始失败另计。Music/Voice/Crossfade 固定 Source 不计此 Sound 统计。Reset 清累计、peak 设为当前 active，保留当前 active/cache/source gauges，不停播、不清 Clip 规则或去重；OwnedSourceCount 不是清零后的 created-released 差值。Shutdown/新会话全部重置，旧 lease 的清理不串入新会话。

## 可选 Mixer

### 分类默认与单次路由

`AudioOutputRoute` 是可序列化 struct，嵌套 `RouteMode` 枚举为 `Inherit/MixerGroup/Direct`，公开字段为 `Mode/Group`。`default` 与 `AudioOutputRoute.Inherit` 等价；`Direct` 明确绕过分类默认 Mixer，`ToMixer(group)` 指定单次输出组。未知 Mode、MixerGroup 模式的空/已销毁 Group，以及 Inherit/Direct 携带非空 Group 都是非法组合，在替换槽位、分配 Source 和提交去重记录前拒绝。单次覆盖可使用另一个 Mixer 的组。

```csharp
using YLFramework.Services.Audio;

// 三项均为 BindableProperty<AudioMixerGroup>，默认 null，不写 PlayerPrefs。
AudioKit.Settings.MusicOutputGroup.Value = musicGroup;
AudioKit.Settings.VoiceOutputGroup.Value = voiceGroup;
AudioKit.Settings.SoundOutputGroup.Value = soundGroup;

AudioKit.PlayMusic(musicClip); // Inherit 分类默认
AudioKit.PlayVoice(voiceClip, outputRoute: AudioOutputRoute.Direct);
var options = AudioSoundOptions.Defaults;
options.OutputRoute = AudioOutputRoute.ToMixer(uiGroup);
AudioKit.PlaySound(soundClip, options);
// 相同 options.OutputRoute 也用于随机、固定位置/跟随 Sound 与空间 Voice。
```

改变默认组会即刻更新对应分类的当前 Inherit 请求，包括暂停、Mute、等待开关重播的请求及 Crossfade 两条 Music；不重播、不改变进度、代次、音调、音量或回调。显式 MixerGroup 和 Direct 请求不受影响；开关重播时继承最新默认组。分类默认 null 表示直接输出，不配置 Mixer 的旧调用保持原功能。池归还会清空 Source 的输出组与旧请求的 Group 引用。

### 配置资产与生命周期

通过 `Assets/Create/YLFramework/Audio/Mixer Config` 菜单创建 `AudioMixerConfig`（ScriptableObject），配置其 `Mixer/MusicGroup/VoiceGroup/SoundGroup`；公开只读属性供业务读取。分类组可以留空，留空分类直接输出；非空组必须属于配置的 Mixer，全部留空可作为直接输出配置。

```csharp
AudioKit.ApplyMixerConfig(config); // 完整校验后复制三个默认组，只初始化 Settings
AudioKit.ClearMixerRouting();     // 清空默认组；未初始化时安全无操作
```

Apply/Clear 不创建 AudioManager、AudioSource 或 AudioListener；修改配置资产不会自动广播，需重新 Apply。ClearMixerRouting 不停止请求、不清除单次覆盖、不调用 ClearFloat、不恢复 Snapshot。`Shutdown` 清理默认路由及订阅，但同样不撤销显式 Mixer 参数或 Snapshot 操作。

配置资产、Mixer、Group、Snapshot 和 Clip 均由调用方拥有；Audio 不加载、卸载或销毁它们。释放 Mixer 资源前显式清分类路由并停止仍覆盖该资源的请求。初始无效配置/路由不静默回退；播放期间外部卸载资源不自动重载，不保证继续保留效果。

### 暴露参数与 Snapshot

`AudioMixerControl` 是独立静态入口，显式接收原生 Mixer/Snapshot，不初始化 AudioKit，也不创建计时或播放宿主。所有入口仅支持 Unity 主线程；`TrySetFloat/TrySetVolume` 在 `Start` 或之后调用，不在 `Awake/OnEnable/AfterSceneLoad` 初始化回调中调用。

```csharp
// 在 Start 或之后执行；parameterName 必须是 Mixer 中已暴露的参数名。
bool set = AudioMixerControl.TrySetVolume(mixer, "MusicVolume", 0.5f);
bool read = AudioMixerControl.TryGetVolume(mixer, "MusicVolume", out float volume);
// 需要正增益或效果参数时用原始 Float API，而不是归一化音量入口。
bool gain = AudioMixerControl.TrySetFloat(mixer, "Gain", 3f);
bool raw = AudioMixerControl.TryGetFloat(mixer, "Gain", out float value);
bool released = AudioMixerControl.TryClearFloat(mixer, "MusicVolume");

float db = AudioMixerControl.LinearVolumeToDecibels(0.5f);
float linear = AudioMixerControl.DecibelsToLinearVolume(db);
AudioMixerControl.TransitionToSnapshot(snapshot, 0.25f);
AudioMixerControl.TransitionToSnapshots(mixer, snapshots, weights, 0.5f);
```

合法调用保留原生 SetFloat/GetFloat/ClearFloat 的 false，不伪造成功；TryGet 失败时 out 为 0，不应当作真实参数值。空/已销毁对象、空白参数名、非法数值或数组抛 `ArgumentException` 或派生异常。音量入口接受有限 `[0,1]`：0 映射到 -80 dB，正值为 `Max(-80, 20*Log10(v))`，1 为 0 dB；反向换算 `Clamp01(Pow(10, db/20))`，<= -80 按 0 处理。-80 是 UI 音量下限，不是绝对数字静音保证。

Settings 音量与单请求包络控制 `AudioSource.volume = Clamp01(分类音量 * 请求倍率) * gain * transitionGain`（无音乐过渡时 transitionGain=1），不会自动绑定或写入 Mixer 参数；Source 音量与 Mixer 衰减相乘。需要 Mixer 独占音量控制时由业务将分类音量保持为 1。SetFloat/SetVolume 接管对应暴露参数，Snapshot 不再控制该参数；`TryClearFloat` 把参数交还原生控制，并不清路由。手动 UI 音量与 Snapshot 不应同时争用同一个参数而期待各自独立。

Snapshot 过渡的 duration 必须有限且 >= 0；混合数组非空、等长，Snapshot 存活且属于传入 Mixer，weights 有限、非负且总和 > 0。校验后直接使用 Unity 权重语义，不修改调用方数组或额外承诺归一化/插值曲线。效果链、Send/Receive 和 Ducking 由 Unity Mixer 编辑器配置，无自动 Ducking、状态机或资源加载器。

## 组件与 Action

`AudioEmitter` 配置 Clip 列表、2D/固定位置/跟随模式和参数；`AudioMusicPlayer` 配置 Clip、loop、playOnAwake。基础 Inspector 聚焦 Clip、Volume、Loop、首次自动播放和位置模式，Advanced 默认折叠但提示已配置或非法高级字段；Pitch/Priority、去重、路由、淡变、事件及过渡按需展开。空间参数仅非 2D 显示，Group 仅 MixerGroup 显示，隐藏字段不静默清空。两者 `OutputRoute` 默认 Inherit；旧有效序列化配置无需重存。UnityEvent 绑定无参 `Play/Stop`，`playOnAwake` 均在首次 Start 执行而非每次 OnEnable。组件不创建 Source、不加载资源、不另建池；有效循环重播替换旧自有循环，禁用停止自有循环与跟随声音，普通 one-shot 不因面板禁用被截断。音乐组件只停止自身句柄，旧组件不能误停替换后的新 BGM。

Emitter 的 `useGlobalPlaySoundMode` 缺省 false，以保留旧缺字段资源的显式 `playSoundMode=EveryOne`；编辑器新添加/Reset 设置为 true，使用 `options.PlaySoundMode=null` 继承全局。关闭继承才使用原显式策略，既有字段名、类型和枚举数值不改。运行时 AddComponent 不保证调用 Reset，不应假定自动继承。

Emitter 完整校验配置、只选取一次有效 Clip，并检查位置/目标及 Clip，全部完成后才停止需替换的旧自有循环。空/全无效/全零候选或无效目标不改变旧播放；非法配置明确抛错。MusicPlayer 的空/已销毁 Clip 不停止旧自有音乐，启用 Crossfade 时 duration/curve 也在替换前校验。此保护仅针对输入与配置校验，不承诺容量拒播、通道关闭、原生启动失败或回调异常时回滚旧声音；拒播不重抽。

两个播放组件从序列化字段直接构造 `AudioPlaybackOptions` 值配置，经内部提交链播放，不创建或缓存共享可变的 `AudioSoundOptions` 类。MusicPlayer 的普通播放与 Crossfade 沿用对应播放语义，组件所有权、Started 回调跟踪和校验顺序保持不变。

`AudioEmitter` 的私有序列化 `useWeightedRandom` 默认 false，旧 `clips` 均匀模式不变；开启后使用 `weightedClips` 配对列表，每项配置 Clip/Weight。2D、固定位置、跟随三种位置模式都支持加权，一次选择后进入同一播放内核；去重拒播不重抽、不防重复。新增字段不改变旧组件参数默认值，也不要求迁移旧 Clip 数据。

`AudioEmitter` 私有 `[SerializeField] int priority=0` 在 Advanced 展示，最小值为 0；等权/加权和三种位置模式都仅透传到 `options.Priority`，不映射 AudioSource.priority，也不新增专用播放入口。运行时负值在停止旧循环前抛参数异常。旧字段和 GUID 不变，旧数据未赋值字段为 0，无需重存；请求之后不提供修改 Priority 的句柄方法。

`AudioEmitter` 新增私有序列化无参 `UnityEvent onStarted`，默认空，公开只读 `Started` 返回该事件。内部 typed 开始包装先登记本次 lease 和 StopOnDisable 快照，再 Invoke；事件可 Stop、禁用组件或递归 Play。外层返回路径不重复登记被停止/替换的旧 lease，异常只清自己的过期记录；拒播没有开始事件。组件与 Action 同样受 Sound 并发上限约束。

两个播放组件各有私有 `[SerializeField] AudioAutoFade autoFade`，默认全零；Inspector 时长为片段秒，短片段同比缩短，循环不允许尾淡出。Emitter 在等权/加权及全部位置模式透传 autoFade，按实际选中 Clip 归一化；新自动配置及所选 Clip 元数据非法时，在 Stop 旧循环前拒绝。MusicPlayer 的 Crossfade 关闭时使用与 PlayMusicWithOptions 相同的内部值提交链，autoFade 全零保持原无包络行为；非法新配置也在 Stop 自有旧句柄前拒绝。输入校验统一前置，修订原错误配置可能先停止旧播放的行为；原组件字段与 GUID 保持不变，无需重存旧数据。

`AudioMusicPlayer` 的私有序列化 `crossFadeOnPlay=false`、`crossFadeDuration=1f`、`crossFadeIgnoreTimeScale=true` 保持不变；crossFadeCurve 字段初始化保留 Linear，编辑器新建/Reset 改为 EqualPower，Crossfade 仍关闭。运行时 AddComponent 不承诺 Reset，已有资源的显式选择与旧缺字段 Linear 保留。关闭时走普通音乐内部值提交链，未启用的 Crossfade 参数不参与校验；开启使用 CrossFadeMusic 对应的内部值提交链并显式传入组件曲线，不先 Stop 旧轨，新参数完整校验后才替换。组件跟踪自己创建且仍有效的 lease；Stop/OnDisable 只停自己的目标与旧尾轨，不误停另一所有者的新 Music。旧场景所有者停用仍可截断自己的尾轨，因此跨场景完整过渡由长期存活的音乐所有者调用，不靠忽略 OnDisable 改变所有权。

`AudioMixerController` 是薄配置组件：Inspector 引用 AudioMixerConfig。编辑器新建/Reset 设置 `applyOnAwake=false`、`transitionDuration=0.5f`，字段初始化仍为 true/0，以保留旧资产行为；运行时 AddComponent 不承诺 Reset。新预设只配置 Config 不自动应用或清路由，必须显式勾选 `Apply Routing On Awake` 才在 Awake 自动应用。Awake 仅在开启自动应用且 config 为有效引用时应用路由，不自动初始化参数或切 Snapshot。无 config 安静跳过并由 Inspector 提示；开启应用后已配置但非法的资产不静默跳过。显式 Apply、参数与 Snapshot 调用缺配置仍明确报错。UnityEvent 使用无参 `Apply/ClearRouting/ClearParameter/TransitionToSnapshot/TransitionToSnapshots` 及 `SetParameter(float)/SetVolume(float)`；参数名、Snapshot、混合数组和 duration 来自 Inspector，控制使用 config.Mixer。SetParameter/SetVolume 的事件应在 Start 或之后触发。错误配置或原生 false 会给出带组件上下文的明确错误。

仅用 Controller 控制参数/Snapshot 时保持自动 Apply 关闭；新预设避免仅绑定 Config 就误清共享路由，但显式完整 Apply 中 Group=null 仍表示把对应全局路由清为 Direct，不是保留先前路由。transitionDuration=0.5 只供显式 Snapshot 调用使用，不代表 Awake 自动切 Snapshot，也不是 Mixer 参数淡变。Inspector 区分路由应用、参数控制与 Snapshot，并提示两层音量叠乘。加权列表切换不擦除另一列表，新增加权项 Weight=1，旧 Weight=0 仍排除，非法权重/全无候选明确提示。

Mixer 路由是全局显式生命周期，不属于组件自有播放句柄。禁用/销毁 AudioMixerController 不 ClearRouting，不撤销参数或 Snapshot；多个自动应用组件按执行顺序以最后一次 Apply 为准，不提供自动优先级或恢复旧配置。

```csharp
using YLFramework.Actions;
using YLFramework.Services.Audio;

ActionKit.Sequence()
    .PlaySound(soundClip, outputRoute: AudioOutputRoute.ToMixer(uiGroup))
    .Callback(() => UnityEngine.Debug.Log("音效等待结束"))
    .Start(host);
```

`PlaySoundAction.Allocate(clip, onFinish=null, outputRoute=default, onStarted=null)`、`AllocateWithOptions(clip, options, onFinish=null, onStarted=null)` 及 `sequence.PlaySound(clip, outputRoute=default, onStarted=null)`、`PlaySoundWithOptions(clip, options, onStarted=null)` 保留参数位置与 typed `Action<AudioClip, AudioHandle> onStarted=null`。原 onFinish 无参、outputRoute 默认 Inherit，普通 Allocate 内部使用值默认并覆盖 OutputRoute、Priority=0，不创建配置类；WithOptions 的 options 位置必填，null 使用 Sound 值默认，在 Allocate 时捕获并校验完整 `AudioPlaybackOptions` 独立值快照（含 AutoFade/Priority），Start 将模板按值提交内部播放链，不再复制配置类。分配后修改原对象、句柄修改或另一请求不影响动作模板。不新增 Priority 专用入口、旧签名包装重载或 Fluent 层；Options 类改造要求固定反射/委托与预编译程序集同步迁移：

```csharp
var options = AudioSoundOptions.Defaults;
options.Priority = 10;
options.AutoFade = new AudioAutoFade(0.25f, 0.5f);
var action = PlaySoundAction.AllocateWithOptions(soundClip, options, onFinish: () => { },
    onStarted: (clip, handle) => handle.SetVolumeScale(0.8f));
ActionKit.Sequence().PlaySoundWithOptions(soundClip, options,
    onStarted: (clip, handle) => UnityEngine.Debug.Log(clip.name)).Start(host);
```

启动才播放；等待的是请求结束，不保证自然播放成功，拒播、外部 Stop、抢占或裁剪也结束等待。取消/Deinit 停止自身请求且不通知成功。Reset 停止旧请求、保留 Clip 与完整值快照模板，下一次执行重新建立自动包络；Deinit 将模板清为 `default(AudioPlaybackOptions)`，释放其中借用的 `OutputRoute.Group` 引用并清除其他资源引用，不卸载或销毁资源，池复用不泄漏。旧 Action 的回调或句柄不能控制新执行代次。暂停动作或序列不自动暂停音频，音频自然结束仍可将动作标记 Finished；完成通知由 Action 执行契约处理。暂停音频继续等待。

Action 在用户 onStarted 之前写入实际 lease，允许回调 Reset/Deinit 停止已启播但尚未从 Play 返回的请求。回调返回与播放方法返回后检查 ActionID/执行版本/Deinited，旧外层赋值不覆盖重入的新执行。并发拒播与抢占仍沿用原等待完成检查，不改变自然完成/取消行为。

该 `onFinish` 仍为无参 `Action`，不迁移为音频的 `(clip, handle)` 签名；它通知 Action 等待结束，包含拒播/外部停止，不等同于 AudioKit 的自然完成回调。Action 序列的 `.Callback(() => ...)` 也保持不变。

Timer 位于 `Runtime/Tools/Timing`，是独立到期回调工具；Audio 不使用 Timer 或 `clip.length` 代替 Source 完成检测。TimerScope、ActionHandle、AudioHandle 各管自己的所有权与生命周期，详见 [Timing](Timing.md)。

## 宿主、池与诊断

```text
外部 Clip + OutputRoute -> 输入/参数/开关/去重检查 -> AudioManager 容量接纳
    -> Music 目标/尾轨、Voice 槽位或池内 Sound -> 解析路由 -> 完整配置 Source
    -> 提交有效请求/接纳序号/去重 -> Source.Play/onStarted -> 返回原 AudioHandle
Settings 分类音量 -> Source.volume；显式 Mixer 控制 -> AudioMixerControl -> 原生 Mixer/Snapshot
Update -> 跟随位置与自然完成优先检查 -> 请求包络/音乐过渡推进 -> 代次安全清理 -> 音效回闲置池
Shutdown -> 阻止新请求 -> 停播/清池/清订阅与静态状态 -> 释放自有宿主
```

自建宿主归入 `YLFramework/Audio` 并跨场景，借用外部 GameObject 保持层级；Shutdown 仅释放自有组件/Source，不销毁借用 GameObject 或 Listener。闲置池为 LIFO，`SoundCacheCapacity` 默认 10，与 `SoundConcurrency` 总量/Clip 有效请求上限独立；仅显式正预热创建闲置音源，不自动因提高限额而创建。稳态更新复用代次快照列表，分类音量由宿主订阅而非每个音效重复订阅。不据此宣称零 GC；首次第二条 Music Source 是按需冷路径，预热/首次突发容器增长及后续稳态须分别以实际 Profiler 测量。

菜单 `YLFramework/Audio/Debug` 打开独立编辑器窗口。诊断被动展示分类开关、音量、Mute、分类默认组及有效句柄的 `Time/Duration/Progress`、路由模式和实际输出组/Mixer；每条 Sound 显示实际 Clip 实例 ID、该 Clip count/effective limit 及捕获的 Priority，不按名称合并。全局显示 active/peak/接纳/开始失败、四类主要拒播、抢占/裁剪、cache 命中/缺额/预算和 source 领域拥有数/创建/释放请求。冷态仍显示统计和配置；唯一写操作为显式 `Reset Sound statistics`，不提供播放控制或预算编辑。读取与清零不创建 Settings/AudioManager/Source/Listener。运行 GUI 仅 Editor/Development Build 存在，默认隐藏。平台快捷键沿用旧 Input：桌面/WebGL F1，Android Escape/返回，iOS 四指触屏；项目需启用旧 Input Manager 或 Both。

## 业务接入

业务资源加载仍由 XFAB 提供 Clip；AudioKit 不新增 XFAB/Resources/ResLoader 适配。`MainModule` 的全局 10 帧配置保留，UI 点击与结算通过 options 显式 EveryOne。点击组件迁为框架 AudioEmitter，BGM 组件迁为 AudioMusicPlayer；Game 的 Gaming 入口取得该组件启动，退出停止自己的 BGM。首次启动的旧 XF 音量键 `AudioConfigVolume:Music` 迁移仍只在业务层。

`Art/Prefabs/3DAudioPlayer.prefab` 是无新 Clip、无新增业务引用的固定位置 AudioEmitter 模板；场景 Listener 由业务保持。业务接线与真实 Main/Start/设置/Gaming 流程由独立业务测试和主代理验证，不以单次按钮测试代替完整流程。

## 测试与帮助

- [`Tests/EditMode/Audio/`](../../Tests/EditMode/Audio/)：设置六键/默认/重建退订、临时 Mute 分类隔离/池复用/最新音量恢复、只读查询与失效/缺失资源的安全返回、初始与运行时参数校验、倍率截断与分类音量、去重边界与提交/代次清理、随机有效 Clip、快照、句柄隔离、非创建读取、宿主清理与池边界，以及 Action 拒播/Stop/取消/Reset/重入。
- [`Tests/PlayMode/Audio/`](../../Tests/PlayMode/Audio/)：真实播放进度、暂停冻结/静音推进/循环/Pitch、静音中创建与自然完成、随机回调的实际 Clip 与原句柄、同步开始/完成回调的控制与重入、运行中倍率与请求隔离、待播放请求修改与开关完整重播、显式 Stop、并发/池复用、音量、Sound/Voice 3D→2D 重置、语音分类隔离及同帧重播目标校验、目标移动/停用/销毁/场景卸载、回调替换/Shutdown，以及 Action 序列等待。
- Loader 专属测试随已删除功能移除；播放、属性和清理断言迁为新契约，不冻结旧 40 公共类型。测试源码存在不等于已通过，结果以当前 Unity 实际编译和运行记录为准。
- `AudioMixerTests` / `AudioMixerPlaybackTests` 使用 `Tests/Fixtures/Audio/` 下的独立 Mixer 资产，覆盖三态路由、Inspector 空引用、配置归属、同步重入、池与会话清理、原生参数/快照及组件/Action；夹具不放 Resources，也不修改业务 Mixer。
- [`Editor/Audio/Help/AudioKitHelpProvider.cs`](../../Editor/Audio/Help/AudioKitHelpProvider.cs) 的稳定 id 为 `audiokit`，反射真实 Audio 公开类型，包含组件与 Action，不泄漏内部实例或 Foundation 通用 API。
- 帮助菜单 `YLFramework/Audio/帮助`；保留 9 个指南页及稳定 ID `overview/control/settings/spatial/mixer/components/action/timer/diagnostics`，在现有 settings/components/action/diagnostics 扩充总量/Clip/Priority/缓存/预热/统计合同，保留原 Sound 开始、Crossfade、手动/自动淡变与加权说明，不新增页面或文档文件。API 反射包含真实 AudioSoundStats、AudioSoundConcurrency/SoundOverflowPolicy、AudioAutoFade、AudioWeightedClip、AudioOutputRoute 及嵌套 RouteMode、AudioMixerConfig、AudioMixerControl、AudioMixerController，XML 来自共享 `Library/YLFramework.Runtime.xml`，不伪造类型或创建播放宿主。`timer` 页保留工具边界与导航，不新增独立 Timing Provider。
- `AudioHelpTests` 应按真实契约检查 Options 为 Serializable sealed class、字段类型不变、new/Defaults 有效且每次新实例、MusicDefaults 循环且独立、default 为 null；将原 Nullable 参数反射改为引用类型，并保留 optional null、WithOptions 位置参数及回调顺序。新增最短路径/Slider/Listener、JSON 缺字段、请求与 Allocate 快照、Settings 坏值回退与拒绝赋值、Emitter 新旧策略、校验保护旧播放及 Mixer 缺 config 的帮助索引断言。原九页 ID、API 可发现性、被动创建与未受影响的高级合同仍需保留。合同断言不等同于实际编译、运行或听感验证。
- QFramework/liangxiegame 的 MIT 版权与来源通过随附的集中声明保留（见 [`Runtime/THIRD-PARTY-NOTICES.md`](../../Runtime/THIRD-PARTY-NOTICES.md)）；原 Timing/Disposable 源保留各自原版权。删除旧包装不改变保留代码的许可，分发时保留对应声明。

编译、EditMode、PlayMode、关闭 Domain Reload 的重复会话、真实游戏流程和人工听感必须分别报告；未实际执行不得称为已验证。导出边界见 [文档首页](../README.md#帮助与导出)，XML 是 Library 构建产物，不默认导出。
