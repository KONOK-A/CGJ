# QFramework 当前使用审计

## 功能使用总览

审计日期：2026-10-02。对象：`G:/Unity/Projects/CGJ2024` 当前磁盘上的修复后源码、资源和配置。Unity：`2022.3.62f3c1`。仅进行静态、只读审计，未运行 Unity、重新导入、编译、测试或构建。

**结论：项目主动使用集中在 AudioKit 音效播放、音效音量、同音效帧窗口去重，以及 FluentAPI 的随机列表取项。项目自己的 AudioController 是主要间接依赖入口。业务面板、MVC、FSM、资源加载、计时器和背景音乐不能因为同名 API 被计作 QFramework 功能。**

| 功能/模块 | 实际使用的类型/API | 项目用途 | 使用方 | 依赖形式 | 证据 |
|---|---|---|---|---|---|
| AudioKit：音效播放 | `AudioKit.PlaySound(AudioClip)` | 武器开火、蛇节脱落、敌人死亡、火箭爆炸 | 项目 `AudioController`；8 个文件直接调用该项目包装 | A：包装实现直接依赖；B：业务通过包装间接依赖 | `P/Scripts/Controller/AudioController.cs:45-49`；附录 A 的 10 处包装调用 |
| AudioKit：音效音量 | `AudioKit.Settings`、`AudioKitSettingsModel.SoundVolume`、继承的 `BindableProperty<float>.Value` | 设置面板读写音效音量；内部通过 PlayerPrefs 保存、通过绑定传播到播放器 | `AudioController.SoundVolume`、`SettingPanel` | A：属性包装直接依赖；B：UI 间接依赖；持久化与事件通知是内部支撑 | `P/Scripts/Controller/AudioController.cs:25-29`；`P/UI/SettingPanel/SettingPanel.cs:18,24` |
| AudioKit：同音效去重 | `AudioKit.PlaySoundMode`、`PlaySoundModes.IgnoreSameSoundInGlobalFrames`、`GlobalFrameCountForIgnoreSameSound` | 在模块启动时配置共享帧窗口，数值为 10 | `MainModule.OnStart()` | A：业务直接依赖 | `P/MainModule.cs:20-28`；`Q/Toolkits/AudioKit/Scripts/AudioKit.cs:126-155` |
| FluentAPI：随机列表取项 | `UnityEngineOthersExtension.GetRandomItem<T>(List<T>)` | 四种武器从 XF 检测结果中随机选敌 | `HeadGun`、`MachineGun`、`ShotGun`、`RocketGun` | A：业务直接扩展方法依赖 | 四个调用位置见附录 A；定义 `Q/Toolkits/_CoreKit/FluentAPI/0.Unity/7.UnityEngineOthersExtension.cs:44-47` |
| AudioKit 的内部支撑 | Architecture、BindableProperty/EasyEvent、PlayerPrefs 属性、SingletonKit、PoolKit、ActionKit、ConsoleKit、LogKit | 初始化 AudioKit、动态管理器、播放器分配/回收、音量通知、播放结束监听及内部诊断 | AudioKit 实现；不是项目主动使用这些模块的 API | 框架内部依赖，单列于第 4 节 | `AudioKitArchitecture.cs:14-38`；`AudioSourceController.cs:71-85`；第 4 节 |
| SupportOldQF / ResKit 等自动接入 | `RuntimeInitializeOnLoadMethod`、loader 替换、ResKit 条件初始化、ActionKit 池预热 | 随当前程序集提供的运行时自动入口 | 框架内部入口 | E：已确认自动入口存在；未取得执行记录，不能算业务主动资源加载 | 第 5 节的 8 个入口 |
| 构建配置：QFramework 保留规则 | `link.xml` 中 `assembly fullname="QFramework" preserve="all"` | 对 QFramework 程序集配置裁剪保留 | 工程构建配置 | C：配置依赖；未验证构建应用结果 | `Q/Framework/Scripts/link.xml:1-3` |
| 框架编辑器、生成器、资源 | UIKit / CodeGenKit / ResKit / GraphKit / PackageKit 等编辑器入口及框架自带资源 | 当前仅确认提供菜单、自动入口和自带资源；未检出项目实际使用生成/打包工具的可靠证据 | 框架代码与自带资源 | C/D/E 分开记录，不并入业务功能统计 | 第 5、6 节 |

路径缩写只用于减少表格重复，均为真实相对工程路径：

- `P/` = `Assets/Games/MainModule/`。
- `Q/` = `Assets/QFramework/`。
- `XF/` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/Runtime/`。
- `XFAB/` = `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/Runtime/`。
- `XFFSM/` = `Library/PackageCache/com.xfkj.xffsm@7d887ff59e/Runtime/`。
- `Overlap/` = `Library/PackageCache/com.xfkj.xfgameframework.overlap@4961d83581/Runtime/`。

证据等级：**源码确认**表示实际符号及定义已核对；**资源确认**表示当前文本序列化或配置确实存在引用；**静态入口确认**表示有可追踪的启动/加载/回调入口，不等于运行过；**待确认**表示当前执行状态、配置值或产物一致性缺少证据。全文没有把历史错误日志作为功能使用证据。

## 1. 项目边界与代码状态

### 1.1 源码、资源和第三方边界

| 范围 | 本轮识别结果 | 依据与限制 |
|---|---|---|
| 项目业务源码 | `P/` 下 56 个 C# 文件，全部逐文件读取；包含 Main/模块、MVC、FSM、武器/敌人/物品、9 个 UI | 类型的基类、导入、实际成员定义核对后确定归属，不只按目录名称判断 |
| 项目通用包装 | `AudioController` 包装两套音频 API；`SystemExtension` 是项目自己的字符串/数学扩展；其他控制器包装 XF/XFAB 功能 | 包装与底层归属见第 2、3 节 |
| 项目编辑器/构建脚本 | 本轮在 Assets/Packages 中未检出框架外的 C#、asmdef、asmref、DLL 或项目开发脚本；未发现业务实现 QF 生成/构建接口 | 不是“任何仓库外工具都不存在”的结论 |
| 项目测试 | 56 个业务 C# 中未检出 NUnit、`Test`、`UnityTest`；没有单独识别出的项目测试程序集 | 未执行任何测试；框架 Tester/样例不算项目测试 |
| QFramework 当前源码 | `Q/Framework/Scripts/QFramework.cs` 和 `Q/Toolkits/`；枚举 884 个 C# 路径、8 个 asmdef | 数量包括注释化样例、第三方源码，不等于 884 个有效编译单元或业务使用文件 |
| QFramework 版本标记 | `Q/Framework/PackageVersion.json:3,8-12` 为 `v1.0.257`，安装路径覆盖 Framework/Toolkits | 仅是本地版本标记；未比对远端原版，不能断言未被项目修改过 |
| XF 相关 Packages | manifest 中 9 个 Git 包；定向只读核查当前 PackageCache 的这 9 个包，共 321 个 C#、18 个 asmdef | `Packages/manifest.json:13-21`、`packages-lock.json:297-360`；没有安装或更新包 |
| 其他第三方 | QF 内包含 MoonSharp、SharpZipLib、xNode 派生 GraphKit、UnityMarkdownViewer/Markdig 等来源；Packages 还包含 Unity 等依赖 | 按源码命名空间/版权/程序集与调用链区分，不因在 QF 目录中就认定业务调用 QF API |
| 示例/历史线索 | `.unitypackage` 示例档、ResKit HotFixExample、自带 UIRoot、QFrameworkData、包仓库缓存，以及根目录生成的旧项目索引 | 示例是否复用另查资源入口；生成 csproj、目录存在、仓库缓存都不直接证明业务功能使用 |

`ProjectSettings/ProjectVersion.txt:1-2` 确认实际 Unity 版本。Packages 内容用于确认依赖来源，不在本轮分析或修改依赖兼容性。遵守当前会话已加载的全局与 Unity 项目规则；定向枚举未找到额外子目录 AGENTS.md。

### 1.2 版本控制与未修改核对方式

- 审计开始执行 `git status --short --untracked-files=all`，返回 `fatal: not a git repository`；无法取得 Git 分支、提交或已有修改清单。
- 根目录存在 `.plastic/`；尝试只读 `cm status --short`，但当前环境没有 `cm` 命令。无法把磁盘现状与 Plastic 基线比较，不假定工作区干净。
- 开始时对 Assets、Packages、ProjectSettings、`.kilo` 中源码/文本/资源序列化/元数据，以及根目录生成的 csproj/sln/slnx 建立内容摘要：3213 个去重文件，SHA256 `A625B4B0BA241C5310645692A6783481774C4FFD1BF532F1C9AD4FEBFCC87625`。
- 写报告前另对上述四个目录全部文件（包括二进制资源、DLL、link.xml）及生成项目文件建立完整摘要：3570 个去重文件，SHA256 `2B343CA1C45B6BA1250CB8FF6B7A15A7A247AD716DEBBD121DC67007F041C99B`。
- 两种摘要均排除指定审计报告 `01-audit.md`；结束核对结果见第 9 节。没有改动用户已有源码或资源，也没有根据 Git/Plastic 状态失败进行清理、恢复或权限变更。

## 2. A/B：业务实际使用的功能

### 2.1 AudioKit 音效播放与项目包装

**定义归属**：`QFramework.AudioKit` 位于 `Q/Toolkits/AudioKit/Scripts/AudioKit.cs:20`，程序集 `AudioKit`。本项目实际调用的是 `:172-174` 的 **AudioClip 重载**，不是 `:169-170` 的字符串重载。

```csharp
// P/Scripts/Controller/AudioController.cs:45-49
public void PlaySound(string assetName)
{
    var audioClip = AssetBundleManager.LoadAsset<AudioClip>(Module.ProjectName, assetName);
    if (audioClip == null) return;
    AudioKit.PlaySound(audioClip);
}
```

已证实的直接与间接关系：

```text
武器 Shoot / 蛇节 OnFallOff / 敌人 OnDead / 火箭碰撞与 OnExplode
  -> 项目 AudioController.PlaySound(string)
  -> XFABManager.AssetBundleManager.LoadAsset<AudioClip>("MainModule", assetName)
  -> QFramework.AudioKit.PlaySound(AudioClip)
  -> PlaySoundWithClipCommand.Execute
  -> QFramework.AudioPlayer.Allocate / PrepareByClipAndPlay
  -> PrepareClipBySetUp（直接接收已有 clip）
  -> AudioPlayer.CanPlayAudio / AudioSourceProxy.Play
  -> UnityEngine.AudioSource.Play
```

代表性内部定义：`PlaySoundWithClipCommand.cs:17-36`；`Architecture/Utility/AudioPlayerExtensions.cs:68-79`；`Player/Features/ClipPrepareMode/PrepareClipBySetUp.cs:23-32`；`Player/AbstractAudioPlayer.cs:55-95`；`Player/AudioPlayer.cs:74-77`；`Player/AudioSourceController.cs:97-106`，均相对 `Q/Toolkits/AudioKit/Scripts/`。

已查清 10 处项目包装调用，按路径去重 8 个文件，完整位置在附录 A。还存在项目继承链：普通/功能蛇节调用 `SnakeBase.OnFallOff()`，四类敌人调用 `EnemyBase.OnDead()`；附录 B 单列这些上游，不把它们当新的直接 QF API 调用。

**入口**：`P/Scripts/Main.cs:9-15,32` 准备 `MainModule` 资源并启动模块；`P/MainModule.cs:23` 加载 AudioController；游戏对象的 Update、伤害、碰撞与生命周期回调可进入包装。资源场景/FSM入口见第 6 节。资源加载失败时 `:48` 返回，不能声称每次调用都成功播放。

**不属于本项目这条音效链的功能**：QF 的按名称同步/异步 AudioLoader、ResKit 的资源检索、QF PlayMusic/PlayVoice、对返回的 QF AudioPlayer 进行控制。`PlaySound(AudioClip)` 虽返回 `QFramework.AudioPlayer`，项目没有保存或使用此返回值。

### 2.2 音效音量、PlayerPrefs 与绑定支撑

```csharp
// P/Scripts/Controller/AudioController.cs:25-29
public float SoundVolume
{
    get => AudioKit.Settings.SoundVolume.Value;
    set => AudioKit.Settings.SoundVolume.Value = value;
}
```

项目入口为 `P/UI/SettingPanel/SettingPanel.cs:18` 读初始值、`:24` 在 `OnSliderSoundValueChange(float)` 回调写值。`StartPanel.cs:20-23`、`PausePanel.cs:30-33` 有加载 SettingPanel 的源码入口。

| 层次 | 定义/API 与位置 | 已确认用途 |
|---|---|---|
| 项目包装 | `AudioController.SoundVolume`，`:25-29` | 隔离 UI 与 QF 设置访问，但底层仍为 QF |
| AudioKit 设置 | `AudioKit.Settings`，`AudioKit.cs:233`；`AudioKitSettingsModel`，`AudioKitSettings.cs:15,31,57` | 提供 `SoundVolume : PlayerPrefsFloatProperty` |
| 持久化属性 | `Q/Toolkits/_CoreKit/BindablePropertyKit/PlayerPrefsFloatProperty.cs:13-20` | 继承 `BindableProperty<float>`，构造时 GetFloat，变化时 SetFloat |
| 值与通知 | `Q/Framework/Scripts/QFramework.cs:705,719-749` | `.Value` 读写、比较与变化通知，实际成员不是 Unity/BCL 或 XF 属性 |
| 播放器绑定 | `Q/Toolkits/AudioKit/Scripts/Player/AbstractAudioPlayer.cs:32-43` | 注册音量并应用初值，反初始化解除注册 |
| 音源音量 | `Q/Toolkits/AudioKit/Scripts/Player/AudioSourceController.cs:125-136` | `AudioSource.volume = VolumeScale * Volume` |

链路：`SettingPanel -> 项目 SoundVolume -> AudioKit.Settings -> PlayerPrefsFloatProperty -> BindableProperty<float>.Value -> 内部播放器绑定 -> AudioSource.volume`。

保存键为 `KEY_AUDIO_MANAGER_SOUND_VOLUME`，默认值 `1.0f`，证据为 `AudioKitSettings.cs:21,57`。这不是 ScriptableObject 配置，也不是项目主动创建 BindableProperty、主动订阅 QF 事件的证据。未访问机器上的 PlayerPrefs，未确认当前保存值或 UI 回调实际执行。

### 2.3 同音效共享帧窗口去重

```csharp
// P/MainModule.cs:27-28
AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
AudioKit.GlobalFrameCountForIgnoreSameSound = 10;
```

定义：`AudioKit.cs:126-138,148-155`。内部通道：`Q/Toolkits/AudioKit/Scripts/Architecture/System/PlaySoundChannelSystem/PlaySoundChannelSystem.cs:14,25-41` 与 `IgnoreSameSoundInGlobalFramesChannel.cs:17-39`。新播放器在 `Player/AudioPlayer.cs:34` 获取默认模式。

通道使用 `Time.frameCount`、共享窗口起始帧和声音标识字典抑制窗口内重复声音。**不能把它描述为每个声音独立的 10 帧冷却。** Clip 重载在 `PlaySoundWithClipCommand.cs:22-26` 对非空白 clip.name 生成 `"AudioClip:" + clip.GetHashCode()` 标识，因此也不能简单把去重键等同于项目字符串资源名。

模块启动入口已找到；去重设置不在业务条件编译块内。未记录实际运行的通道命中或音频播放次数。该模式不控制 XF AudioPlayer 的声音。

### 2.4 FluentAPI 随机选敌

**定义归属**：`QFramework.UnityEngineOthersExtension.GetRandomItem<T>(this List<T>)`，`Q/Toolkits/_CoreKit/FluentAPI/0.Unity/7.UnityEngineOthersExtension.cs:19,44-47`，程序集 `QFramework.CoreKit`。

```csharp
// Q/Toolkits/_CoreKit/FluentAPI/0.Unity/7.UnityEngineOthersExtension.cs:44-47
public static T GetRandomItem<T>(this List<T> list)
{
    return list[UnityEngine.Random.Range(0, list.Count)];
}
```

四种武器从 `OverlapCheck.Results` 中随机取得命中目标，再取 `.collider.transform`。调用位置为 HeadGun:25、MachineGun:23、ShotGun:24、RocketGun:23。它们都先判断 Results.Count > 0。

实际类型链：`项目 WeaponBase.OverlapCheck -> XF OverlapBase2D -> OverlapBase<Collider2D, OverlapEvent2D> -> List<OverlapHit<Collider2D>> -> QF GetRandomItem<OverlapHit<Collider2D>>() -> UnityEngine.Random.Range`。依据：`P/Views/Player/Equip/Weapon/WeaponBase.cs:22,27`；`Overlap/OverlapBase2D.cs:12`；`Overlap/OverlapBase.cs:20-24,130`。

只有随机取项是 QF；检测系统、命中结构和结果集合属于 XF。没有项目包装介于这个扩展调用与 QF 之间。入口为武器 Update；武器资源和生成流程有静态入口，但未实际运行验证随机选敌。

## 3. 同名 API 与非 QF 功能排除

| 容易混淆的代码/API | 实际归属及证据 |
|---|---|
| `MainModule : Module`、项目 Controller / Model / View | XF；`P/MainModule.cs:6,8`；`XF/Module/Module.cs:5,9`；`XF/mvc/Controller.cs:19`、`Model.cs:10`、`View.cs:10` |
| 九个 UI 的 `Panel`、`UIType`、`LoadPanel<T>` | XF，不是 QF UIPanel/UIKit；`XF/mvc/Panel.cs:26`；`XF/Module/Module.cs:535,629`；项目面板资源 GUID 与这些项目脚本匹配 |
| `LoadController<T>`、`LoadModel<T>`、`LoadView<T>` | XF Module；典型定义 `XF/Module/Module.cs:237,337,838`，不是 QF Architecture 扩展 |
| AudioController 中 `AudioPlayer`、MusicVolume、PlayBGM | `P/Scripts/Controller/AudioController.cs:5` 显式别名 `XFGameFramework.AudioPlayer`；`:13-16,19-23,31-42` 全部 XF |
| 游戏结束的 `AudioPlayer.Play("Sound", ...)` | `P/UI/GameOverPanel/GameOverPanel.cs:11-12` 为 XF；不能算 QF 音效调用 |
| 计时器、`IsDestroy`、`LayerMask.Contains`、`Physics2DSetting`、场景 FindRootGameObject | XF；`XF/Manager/TimerManager/TimerManager.cs:16,123,273,343,661`；`XF/Extensions/GameObjectExtensions.cs:40`、`LayerMaskExtensions.cs:61,72`、`SceneExtensions.cs:17` |
| FSMState / FSMController / RuntimeFSMController | XFFSM；当前游戏与敌人 FSM 资源对应 XFFSM 脚本 GUID，不是 QF FSMKit/GraphKit |
| AssetBundleManager / GameObjectLoader | XFABManager；实际音频、场景、视图加载不因项目使用 QF 播放就变为 ResKit |
| `Results.All`、`Segments.Select`、`List.ForEach` | LINQ/BCL，不是 QF 扩展 |
| `IsNullOrEmpty`、`ToAngle`、`AngleToDirection2D` | 当前实际解析到项目全局 `SystemExtension`，不是 QF 同名扩展；见下文 |

`P/Scripts/Utility/SystemExtension.cs` 未调用 QF。其 `IsNullOrEmpty:6-9` 被 `GameSettingPanel.cs:38,49,60,71,82,93,104,115` 调用；`ToAngle:18-21`、`AngleToDirection2D:36-39` 被 `ShotGun.cs:58,68` 调用；数学辅助调用仍留在该项目类内。QF 同名成员分别在 `FluentAPI/1.CSharp/1.SystemStringExtension.cs:34-37`、`FluentAPI/0.Unity/7.UnityEngineOthersExtension.cs:295-298,310-313`。依据当前命名空间、导入及扩展方法查找规则核对归属，不能凭名称相同计入 QF。

**并存的两套音频链**：项目普通开火/爆炸音效经 AudioKit，BGM 和游戏失败音效经 XF AudioPlayer。SettingPanel 的 SoundVolume 包装与 MainModule 的去重只指向 QF 那一条；`AudioController.OnInit:15-16` 注册/设置的 XF Sound 不是同一个设置对象。这里只界定归属，不提出改动建议。

## 4. 与已使用功能有关的内部支撑依赖

这些项有实际下游源码链，但不计为项目主动采用这些模块的接口，也不把框架内部调用数量加到业务调用数。

| 内部功能 | 已确认类型/API 与定义/使用位置 | 在当前链路中的作用 |
|---|---|---|
| Architecture、内部模型/系统及 IOC | `Q/Framework/Scripts/QFramework.cs:41-115,647`；`AudioKitArchitecture.cs:14-38` | AudioKit 内部初始化 PlaySoundChannelSystem、PlayingSoundPoolModel、LoaderPoolModel、SettingsModel；项目 MainModule 仍是 XF Module |
| BindableProperty / EasyEvent / 解除订阅 | `QFramework.cs:468,576,705-749,821-831`；`PlayerPrefsFloatProperty.cs:13-20`；`AbstractAudioPlayer.cs:32-43` | 设置保存及播放器音量传播；业务未主动创建绑定属性、注册 QF 事件或使用 QF 事件总线 |
| SingletonKit | `AudioManager.cs:14-15,21-27,118`；`Q/Toolkits/_CoreKit/SingletonKit/Scripts/MonoSingletonProperty.cs:59-73` | 首次 Instance 访问可动态创建 `QFramework/AudioKit/AudioManager`；没有序列化挂载不等于未使用 |
| PoolKit | `AudioManager.cs:23`；`Player/AudioPlayer.cs:31-36,46-60`；`Q/Toolkits/_CoreKit/PoolKit/Scripts/SafeObjectPool.cs:66,79,144-175` | 内部 AudioPlayer 分配/回收；不据此将项目敌人/子弹/特效池化记为 QF |
| ActionKit | `Player/AudioSourceController.cs:71-85`；`Q/Toolkits/_CoreKit/ActionKit/Scripts/ActionKit.cs:149-152`；`Scripts/Framework/IAction.cs:175,210-217` | `ActionKit.Condition(...).StartGlobal()` 监听声音播放结束；这是实际 AudioKit 下游调用，不只是包内示例或预热 |
| ConsoleKit、LogKit | `AudioKitArchitecture.cs:27,32`；`Architecture/System/ConsoleModuleSystem.cs:15-26`；`Q/Toolkits/_CoreKit/ConsoleKit/ConsoleKit.cs:6,19`；`LogKit/LogKit.cs:28,44` | 内部注册 AudioKit 诊断模块与初始化日志；不能断言用户打开过该控制台 |
| 内部 FluentAPI | AudioKit 内的音源对象创建、Parent/Hide、声音字符串检查、诊断 GUI 样式调用 | 只作为内部辅助；业务主动 FluentAPI 使用仍只有已查清的 GetRandomItem |

表中未带 Q/ 前缀的 AudioKit 文件，均相对 `Q/Toolkits/AudioKit/Scripts/`。

## 5. C/E：自动接入、编辑器与构建流程

### 5.1 运行时自动入口，不等于业务主动 API

本地 QF 源码中搜索 `RuntimeInitializeOnLoadMethod` 共找到以下 8 个入口，均核对实际代码；没有把注释或 APIExampleCode 字符串当作执行入口。

| 入口 | 当前源码行为 | 条件与结论 |
|---|---|---|
| `Q/Toolkits/AudioKit/Scripts/AudioKitArchitecture.cs:23-38` | BeforeSceneLoad 初始化 AudioKit 内部 Architecture，注册模型/系统并记录日志 | 自动入口确认；与实际使用的 AudioKit 相连，未取得运行记录 |
| `Q/Toolkits/SupportOldQF/Scripts/AudioKitWithResKitInit.cs:8-11` | BeforeSceneLoad 设置 `AudioKit.Config.AudioLoaderPool = new ResKitAudioLoaderPool()` | 框架自动配置确认；本项目 AudioClip 播放链不走该 loader 的 LoadClip/ResLoader 方法 |
| `Q/Toolkits/SupportOldQF/Scripts/UIKitWithResKitInit.cs:8-11` | BeforeSceneLoad 设置 UIKit 的 ResKitPanelLoaderPool | 不能因此认定业务面板采用 UIKit 或 ResKit |
| `Q/Toolkits/ResKit/Scripts/Architecture/Architecture.cs:16-20` | BeforeSceneLoad 初始化 ResKit 内部 Architecture、注册压缩/序列化 utility 与诊断系统 | 框架自动初始化代码，不是项目主动压缩/序列化业务 |
| `Q/Toolkits/ResKit/Scripts/ResKit.cs:22-30` | CheckAutoInit 在 Editor 且 SimulationMode 时调用 `ResKit.Init()` | `UNITY_EDITOR`；`AssetBundlePathHelper.cs:271-284` 从 EditorPrefs 键 SimulateAssetBundles 读值，默认 true；机器实际值未读取，Player 返回 false |
| `Q/Toolkits/_CoreKit/LocaleKit/Scripts/LocaleKit.cs:16-25,54-57` | BeforeSceneLoad 尝试 Resources 加载 LanguageDefineConfig，存在才初始化 | 未检出当前项目配置/业务使用；不是项目主动本地化证据 |
| `Q/Toolkits/_CoreKit/ActionKit/Scripts/DeprecateActionKit.cs:22-29` | 默认 RuntimeInitialize 时预热 DelayAction/EventAction 对象池 | 这是独立框架自动入口，与 AudioKit 实际 Condition 调用区分 |
| `Q/Framework/Scripts/QFramework.cs:762-788` | BeforeSceneLoad 注册多类 BindableProperty 的比较器 | `UNITY_5_6_OR_NEWER`；绑定属性内部支撑，不是项目直接使用所有这些类型 |

ResKitAudioLoaderPool 中确有 `ResLoader.Allocate/LoadSync/Add2Load/LoadAsync/Recycle2Cache`（AudioKitWithResKitInit.cs:24-56），但其按名称加载方法并未出现在已确认的业务 clip 播放路径。不能把“自动配置 loader”升级为“项目实际使用 ResKit 加载音频”，也不能把 ResKit 描述成毫无自动接入。

### 5.2 编辑器自动入口与提供的工具

| 工具/入口 | 源码证据 | 项目使用证据及限制 |
|---|---|---|
| UIKit 生成、Prefab 绑定 | `Q/Toolkits/UIKit/Editor/CodeGen/CodeGen/UICodeGenerator.cs:17-38,64-71,79-123`；`UISerializer.cs:14-40,82-170`；`CodeGen/UIPanelInspector.cs:21-47` | 提供菜单/Inspector按钮和 DidReloadScripts 后续处理；后者需 EditorPrefs AutoGenUIPrefabPath 非空。业务 9 个 UI 为普通 XF Panel，不是 QF partial/generated 面板 |
| UIKit 生成配置 | `Q/Toolkits/UIKit/Editor/UIKitSetttingData.cs:22-24,41-61` 读取 ProjectConfig.json | `Assets/QFrameworkData/ProjectConfig/ProjectConfig.json` 当前为空；不能据目录存在证明有项目生成流程，也不能断言过去从未使用 |
| CodeGenKit | `Q/Toolkits/_CoreKit/CodeGenKit/Editor/ViewControllerTemplate.cs:14-19` 自动注册模板；`CodeGenKitPipeline.cs:55-59,75-79,179-183` 重载脚本时处理任务 | CodeGenKit 配置目录当前空；未检出项目 ViewController/Bind/生成代码入口。CurrentTask 为 null 时不处理任务 |
| ResKit 资源标记与打包工具 | `Q/Toolkits/ResKit/Editor/ResKitAssetsMenu.cs:33-49,69-97` 自动设置 selectionChanged 回调并提供标记菜单；ResKitEditorWindow.cs:55 菜单 | 自动选择事件接入确认，但菜单存在、通用 assetBundleName 或已有包不证明由 QF 打包。业务实际资源链归 XFABManager |
| BuildKit | `Q/Toolkits/_CoreKit/BuildKit/Editor/BuildKitEditor.cs:30-57,86-92` 反射 IBuildView/IBuildAction 并提供构建按钮 | 未检出项目实现/调用这些接口；本轮未检出 QF IPreprocessBuild/IPostprocessBuild/PostProcessBuild 自动构建回调 |
| LiveCodingKit | `Q/Toolkits/_CoreKit/LiveCodingKit/Editor/LiveCodingKit.cs:26-51` DidReloadScripts | 需 EditorApplication.isPlaying 且 Setting.Open；LiveCodingKit 目录当前空，新建设置的 Open 默认 false。未验证 Editor 内存状态 |
| GraphKit | `GraphKit/IMGUI/Scripts/Editor/IMGUIGraphAssetModProcessor.cs:67-85` 自动修正图节点列表；IMGUIGraphWindow.cs:147-160 订阅选择事件；IMGUIGraphImporter.cs:20-44 与 IMGUIGraphRenameFixAssetProcessor.cs:24-43 为导入/移动处理 | 框架自动入口确认；未发现项目 GraphKit 图或业务派生图。项目 FSM 是 XFFSM，不能当作 GraphKit 使用 |
| PackageKit / EasyIMGUI | `Q/Toolkits/_CoreKit/PackageKit/Framework/Window/PackageKitWindow.cs:20-35,55-75,96-110`；`PackageKit/EditorKit/EasyIMGUI.cs:15,54-58` | 有自动状态重置、窗口恢复与 XML 模块注册；只证明框架自动接入，不能证明日常打开/安装过某模块 |
| Markdown / Lua 导入器 | `Q/Toolkits/_CoreKit/PackageKit/Markdown/MDImporter.cs:12-33`；`ScriptKit/Editor/MoonSharpImporter.cs:9-32` | 有条件编译及 ScriptedImporter 声明；限定 Assets 枚举未检出 .markdown/.lua 项目资源，未取得项目依赖这些导入器的证据 |

上述编辑器入口按 `UNITY_EDITOR` 或 UIKit.Editor / ResKit.Editor 的 Editor 平台限制编译。UIKit Inspector 在当前 Unity 版本选用 `UnityEditor.SceneManagement` 分支（UIPanelInspector.cs:11-15）。没有切换任何宏或平台，没有通过执行这些工具验证，也没有修改 EditorPrefs。

### 5.3 构建保留配置与 DLL

`Q/Framework/Scripts/link.xml:1-3` 确实为 QFramework 程序集指定 `preserve="all"`。它是当前工程内配置证据，不能据此认定其中全部类型被业务使用，也不能声称本轮完成了裁剪或 Player 构建验证。

Assets 中唯一枚举到的 DLL 为 `Q/Toolkits/_CoreKit/PackageKit/Markdown/Markdig.dll`。其 `.meta:11,40-47` 表明非显式引用、Editor 启用；Android/Standalone 等禁用。它是第三方 Markdown 解析器，不是 QF 业务 API。`Markdown/MDViewer.cs:12-16,92` 和 Renderer 源码使用它。XF 生成 csproj 的 HintPath 也列出此 DLL，但 XF 包源码未检出 Markdig 使用；这种默认程序集可见性不构成 XF 业务依赖 QF 功能的证据。未反编译 DLL。

## 6. D/E：资源、序列化与动态入口

### 6.1 搜索范围与来源核对

Assets 当前枚举到 **6 个 .unity、45 个 .prefab、22 个 .asset，共 73 个文件**，全部纳入批量 GUID/类型/事件检索，再局部读取命中块。

- MainModule：3 个场景、40 个 Prefab、15 个 .asset，共 58 个文件。
- QF 目录：1 个场景、3 个 Prefab、1 个 .asset，共 5 个文件。
- Assets/Resources：2 个 Prefab、1 个 .asset，共 3 个文件；剩余 7 个为其他场景/渲染等资源。
- 先枚举 941 个 `*.cs.meta` 候选，再排除 `Q/Toolkits/_CoreKit/Internal/Guidline/Editor/Resources/EditorGuideline/2. 架构篇：QFramework.cs.meta`。其目标是名字以 .cs 结尾的**目录**，不是脚本。实际核对脚本 .meta 为 940 个：QF 源码 884 + 项目源码 56。
- 以上脚本 GUID 的 m_Script 命中按资源路径去重为 38 个；扩大到任意脚本 GUID 属性后为 41 个，多出的 3 个是通过 StateScripts 保存状态脚本 GUID 的项目 FSM 资源。它们不等于 41 个 QF 使用资源。
- QF 目录中当前实际存在的脚本 GUID，仅在 5 个框架自带资源中命中；业务资源没有检出直接挂载 QF 组件的证据。声音 clip 本身无需挂 QF 脚本，通过源码包装和资源映射形成间接入口。

### 6.2 已知业务入口与不属于 QF 的资源链

`ProjectSettings/EditorBuildSettings.asset:8-10` 仅启用 `Assets/Scenes/Main.unity`，GUID `8c9cfa26abfee488c85f1582747f6a02`；该场景 `:340` 的项目 Main 脚本与源码入口对应。

```text
EditorBuildSettings 中 Main 场景
  -> 项目 Main.Start / PrepareRes("MainModule")
  -> XF ModuleManager.StartUpModule<MainModule>()
  -> MainModule.DefaultStartUpScene = "Start" / OnStart
  -> 项目 GameController 加载 XFFSM 的 GameStateManager
  -> GameStart / GameSetting / Gaming 的 Start / Setting / Game 场景与对象加载
  -> 对象生命周期、射击与碰撞 / SettingPanel 的音量回调
  -> 项目 AudioController -> QF AudioKit
```

代码依据：`P/MainModule.cs:11,17,20-28`；`P/Scripts/Controller/GameController.cs:73-75`；`P/FSM/GameState/GameStart.cs:21,24,30`；`GameSetting.cs:24,27,33`；`Gaming.cs:44,117,126,229`。XF 启动过程在 `XF/AsyncOperation/StartUpModuleRequest.cs:106-122` 加载默认场景并调用 module.OnStart。

`Assets/StreamingAssets/MainModule/StandaloneWindows64/AssetBundleNameMapping.txt:24-38` 列出 9 个项目 UI、3 个场景和 3 个 FSM。项目面板真实脚本均继承 XF Panel；`XF/Module/Module.cs:535-537,629-631` 用 typeof(T).Name 确定资源名。`XF/Manager/PanelManager.cs:249,257,303-308,460,476` 通过 ViewManager 加载视图，并动态加载 `XFGameFramework/Canvas`、必要时加载 `XFGameFramework/EventSystem`。项目 Resources 的这两个资源属于 XF，不是 QF UIRoot。

核对过的非 QF 脚本 GUID（各 .cs.meta:2）：

| GUID | 当前定义 | 资源意义 |
|---|---|---|
| `608890d9ef7d9644ebcb0a4ad2feb37b` | `XFAB/Settings/XFABManagerSettings.cs` | Assets/Resources/XFABManagerSettings.asset 的脚本；`XFABManagerSettings.cs:59` 通过 Resources 加载 |
| `f2d633f06ec03ca4ea8f9e98a2c00622` | `XFAB/Model/XFABProject.cs` | MainModule 资源打包配置，不是 QF SubProject |
| `c6fbd4d875fc9c244aa5a60af9e992ab` | `XFFSM/Scriptable/RuntimeFSMController.cs` | 项目 3 个 FSM 配置，不是 QF FSMKit 图 |
| `403c24cad33f2944ead6f9acd0524ea2` | `XF/Manager/AudioPlayer.cs` | 项目 Music/Sound 组件属于 XF；命中 10 个资源/11 条脚本记录，其中 StartPanel:1538 是 stripped 引用，不能重复当作独立组件 |

XFFSM 资源的 `StateScripts.className` 是动态类型入口，不仅是 GUID：`P/FSM/GameState/GameStateManager.asset:55-56,70-71,85-86,100-101` 对应 GameStart/Gaming/GameOver/GameSetting；`XFFSM/FSMStateNode.cs:37-49 -> AssemblyTools.GetType -> Activator.CreateInstance`，`XFFSM/Others/AssemblyTools.cs:32-38` 从当前程序集查类型。没有因此把动态 FSM 归到 QF。

### 6.3 QF 音效包装对应的资源

以下 6 个 clip 名称出现在实际 PlaySound 包装调用中。源文件位于 `P/Art/解限蛇音效/`，各 `.mp3.meta:2` 的 GUID 与资源映射 `AssetBundleNameMapping.txt:44-49` 对照：

| clip 名称 | GUID | 业务位置 |
|---|---|---|
| 机体爆炸1 | `004eb4d16f3743f44b321c38a97af0ee` | SnakeBase:120、EnemyBase:133 |
| 机炮3 | `b952dbed6ef6f614d8a23c8b9439a86e` | MachineGun:56 |
| 机炮4 | `72431bf2f8a60b948b6a418b6c56d00b` | HeadGun:59 |
| 霰弹1 | `b7e585cb299c966448ee8b3cdf7ed818` | ShotGun:56 |
| 火箭发射 | `6791b72ea69e51543beb4b6006a907c7` | RocketGun:55 |
| 火箭爆炸1 | `dad6fd835c49ea54990f16dd84de1840` | PlayerRocketShell/EnemyRocketShell:54,74 |

映射 `:43` 的挑战失败音效走 XF，而不是 QF。StartPanel:1326,1330,1337 的 Music/03.wav、Game.unity:2587,2591,2598 的 Music/02.wav，以及 ClickPlayer.prefab:44,48,55-56 的点击音效也对应 XF AudioPlayer，不混入这 6 个 QF 音效输入资源。

这些证据确认资源存在、名称映射存在、源码加载入口存在，**没有验证 XFAB 当前选择的本地/远程/模拟资源是否实际加载成功**。

### 6.4 框架自带资源、序列化线索与残留

- QF AudioManager 的脚本 GUID 为 `50e8bd7a9085f4504bdb663ea55e1cb5`，本轮未检出序列化挂载。它仍有第 4 节的动态 Singleton 入口，不能据 GUID 零命中排除 AudioKit。
- QF UIRoot/UIManager/Hide 的命中位于 `Q/Toolkits/UIKit/Scripts/Resources/UIRoot.prefab`，没有检出项目加载它的入口；项目当前实际 UI 根链是 XF。
- QF Bind GUID `0d51f3a7c41ab0346b49ae50d456bece` 仅在框架 HotFixExample/CommonConfirm.prefab 命中 4 处；不能据此认定项目 UI 使用 QF Bind。
- HotFixExample 的 TestHotFix 场景与 CommonConfirm prefab 仍引用相关脚本 GUID；HotFixedPanel 是该样例场景中的组件，不另计为一个 prefab。`CommonConfirm.cs:17,21`、`HotFixedPanel.cs:20,24`、`MainConfig.cs:14` 的类声明已经注释化；Designer 的自动生成标记与类声明也被注释。只能证明历史样例资源引用，不证明当前组件类可装载或样例运行。
- 所有 73 个资源的 UnityEvent 检索得到 7 个项目 UI 资源中的 42 条非空 m_MethodName 记录；没有检出 QF 类型的 UnityEvent/managed-reference 类型名证据。并不把 UnityEvent 方法名数量当作有效调用次数。
- `P/UI/SettingPanel/SettingPanel.prefab:689-690` 确实绑定 sliderSound/sliderMusic，`:969` 保存 OnSliderSoundValueChange，`:583` 保存 OnSliderMusicValueChange，与当前源码回调对应。`:1068-1079,1169-1180` 另有两条 MainEventsLog.AudioChange，目标 fileID 为 0；它们不是有效 QF 使用证据。`P/UI/PausePanel/PausePanel.prefab:177,538,1018` 留有旧类型名 Game.PausePanel，而源码类型为全局 PausePanel；不在未运行验证时断言事件必然失败，也不归为 QF。
- QFrameworkData/CodeGenKit 与 LiveCodingKit 目录当前为空；ProjectConfig.json 为空。`.qframework/PackageManager/PackageInfosRequestCache.json:1` 是包仓库索引，不是项目安装/功能使用清单。
- StreamingAssets 中当前有 109 个 .unity3d、109 个对应 manifest；本轮只查必要文本索引与 manifest，未反解析二进制包。其文本中未检出 QFramework 字符串或已查的 AudioManager/UIRoot GUID，不构成二进制内部绝无 QF 的证明。
- StartPanel 包 manifest:32 的脚本 GUID 与当前项目匹配，但 :69-75 含旧 D: 路径；只能证明产物/来源索引存在，不保证包由当前源码最新生成。没有读取 Build/AssetBundles 输出来替代当前源码证据。

## 7. 程序集与必要类型索引

### 7.1 程序集依赖，独立于功能使用

| 程序集 | asmdef 位置，相对 Q/ | references | 平台 |
|---|---|---|---|
| QFramework | `Framework/Scripts/QFramework.asmdef:1-3` | 无显式引用 | 未限定 |
| QFramework.CoreKit | `Toolkits/_CoreKit/QFramework.CoreKit.asmdef:1-6` | QFramework | 未限定；Editor 代码另有条件编译 |
| AudioKit | `Toolkits/AudioKit/Scripts/AudioKit.asmdef:2-14` | QFramework.CoreKit、QFramework | 未限定，autoReferenced true |
| ResKit | `Toolkits/ResKit/Scripts/ResKit.asmdef:2-14` | QFramework、QFramework.CoreKit | 同上 |
| UIKit | `Toolkits/UIKit/Scripts/UIKit.asmdef:2-14` | QFramework.CoreKit、QFramework | 同上 |
| SupportOldQF | `Toolkits/SupportOldQF/Scripts/SupportOldQF.asmdef:2-16` | QFramework.CoreKit、ResKit、UIKit、AudioKit | 同上 |
| UIKit.Editor | `Toolkits/UIKit/Editor/UIKit.Editor.asmdef:2-17` | UIKit、QFramework、QFramework.CoreKit | Editor |
| ResKit.Editor | `Toolkits/ResKit/Editor/ResKit.Editor.asmdef:2-17` | ResKit、QFramework、QFramework.CoreKit | Editor |

Assets 未检出 asmref；项目 56 个脚本没有项目自定义 asmdef，位于默认程序集范围。生成的 `Assembly-CSharp.csproj:872,920,948,960,992,996,1096,1108` 和 `Assembly-CSharp-Editor.csproj:871,919,947,959,991,995,1095,1107` 列出以上 8 个引用；**只作当前生成索引佐证**，不把 Editor csproj 中可见的全部功能判作 Player 或业务使用。

9 个 XF 包的 18 个 asmdef 已比较名称引用与 QF 8 个 asmdef 的 meta GUID，未检出交叉引用。示例：`XFGameFramework.asmdef:3-5` 引用 XFABManager；其 Editor asmdef 的 GUID `54be8d937cf30464a989ed5b593e72b8`、`29a8a580b7f1bc64cbf21bba59efc85e` 分别为 XFGameFramework、XFABManager，不是 QF。321 个 XF C# 与包配置限定检索也未检出 QFramework 导入、完整名称或已建立主要 QF 类型入口。

### 7.2 足以核对使用方的本地索引

| 模块 | 核对类型/API | 代表定义 |
|---|---|---|
| Framework | Architecture、IArchitecture、IController、AbstractModel、BindableProperty、EasyEvent、IUnRegister、IOCContainer | `Q/Framework/Scripts/QFramework.cs:41,70,226,268,468,647,705,800,821` |
| AudioKit | AudioKit、AudioKitSettingsModel、AudioPlayer、AudioManager、PlaySoundModes、播放通道 | `AudioKit.cs:20,126`；`AudioKitSettings.cs:15`；`Player/AudioPlayer.cs`；`AudioManager.cs:15`；`Architecture/System/PlaySoundChannelSystem/` |
| CoreKit/FluentAPI | UnityEngineOthersExtension.GetRandomItem | `Q/Toolkits/_CoreKit/FluentAPI/0.Unity/7.UnityEngineOthersExtension.cs:19,44` |
| CoreKit/BindablePropertyKit | PlayerPrefsFloatProperty | `Q/Toolkits/_CoreKit/BindablePropertyKit/PlayerPrefsFloatProperty.cs:13` |
| CoreKit/Pool/Singleton/Action | SafeObjectPool、MonoSingletonProperty、ActionKit.Condition、IActionExtensions.StartGlobal | 第 4 节的准确实现位置 |
| UIKit / CodeGen | UIPanel、UIRoot/UIManager、Bind/AbstractBind、UICodeGenerator、UISerializer、CodeGenKitPipeline | `Q/Toolkits/UIKit/Scripts/`、`UIKit/Editor/CodeGen/`、`_CoreKit/CodeGenKit/`；与项目面板资源交叉核对 |
| ResKit / SupportOldQF | ResKit、ResMgr、ResLoader、ResKitAudioLoaderPool、ResKitPanelLoaderPool | `Q/Toolkits/ResKit/Scripts/ResKit.cs:20`；`Framework/ResMgr.cs:18-21`；`Framework/ResLoader/ResLoader.cs`；第 5 节 |

此索引仅为查找与归属核对服务，不是整个 QFramework 内部调用图。AudioKit 和 ResKit 各自程序集内均有名为 QFramework.Architecture 的内部类，本文引用时始终区分实际文件和所属程序集。

## 8. 覆盖、统计与待确认事项

### 8.1 统计口径

| 项目 | 本轮结果 | 口径 |
|---|---:|---|
| 项目 C# 路径 | 56 | Assets/Games 全部逐文件读取 |
| 实际直接引用 QF API 的项目文件 | 6 | MainModule、AudioController、4 个武器；不是只计 using |
| 直接调用 PlaySound 项目包装的位置/文件 | 10 / 8 | 附录 A；两种火箭弹各 2 处，不重复计搜索命中 |
| 直接读写 SoundVolume 项目包装的位置/文件 | 2 / 1 | SettingPanel:18,24 |
| 上述实际符号/直接包装使用位置文件并集 | 11 | 6 + 8 + 1 - 4；4 个武器同时是直接扩展使用方和音频包装使用方 |
| 随机列表取项调用位置/文件 | 4 / 4 | 不把相邻 LINQ、项目数学扩展算入 QF |
| 已核查序列化文件 | 73 | 6 unity + 45 prefab + 22 asset |
| 业务资源直接 QF 脚本挂载证据 | 本轮未检出 | 不排除 clip 输入资源、动态对象、二进制或框架自动初始化 |
| QF 自带序列化资源 | 5 | 不计入业务资源使用数 |
| QF asmdef / XF asmdef | 8 / 18 | 程序集引用与 API/资源引用单独统计 |

11 个文件的并集**不包含全部继承、FSM、伤害、场景加载等上游可达文件**。附录 B 列有证据的继承链，但没有将全游戏可能触发音效的文件数当作准确总数。未统计“框架使用率”或“总 API 调用次数”。

### 8.2 实际搜索范围和主要限制

- 广覆盖枚举：Assets、Packages、ProjectSettings、`.kilo` 与必要根目录项目索引；随后分开检索 Games、QF 模块、9 个当前 XF 包。
- 使用精确名称/路径/API/GUID 检索、逐文件读取业务源码、定向读取框架定义/调用方、批量资源 GUID 和类型名扫描、命中块局部读取。搜索输出过大时按模块拆分；没有把工具最前面的截断结果称作全量结果。
- 当前没有可用的 IDE 符号引用/运行时 Unity 工具；归属通过本地定义、程序集、别名、泛型类型和调用上下文核对。未重建索引或生成 SARIF，也未依赖语义搜索零结果判断不存在。
- 未扫描 Library/Temp/obj/Logs/Diagnostics 缓存全库；仅对已经从锁文件/目录识别的 9 个 XF PackageCache 定向只读。未修改缓存包。
- 未扫描 Build、AssetBundles 中的历史二进制构建输出；StreamingAssets 仅读取映射与 manifest，不反解析 .unity3d。示例 .unitypackage 未解包；Markdig.dll 未反编译。
- Assets 中的 GUID/UnityEvent/序列化类型名覆盖不等于验证 Unity 导入后对象有效。文件名以 .cs 结尾的导引目录已排除，不误当源码。
- `ProjectSettings/ProjectSettings.asset:570-571` 的当前 Standalone 自定义宏只有 MOREMOUNTAINS 系列；不据这些残留宏/根目录 csproj 推导相关第三方当前业务使用。业务直接 QF 调用不在条件编译块内；两种火箭弹的 UNITY_EDITOR 块只涉及 Gizmos。未切平台、未验证 AOT/裁剪/所有平台宏组合。
- 已定位的目标源码和资源没有遗留的权限或读取缺口；缺口主要是 Git/Plastic 状态、EditorPrefs/PlayerPrefs 的当前值、Unity 实际执行以及二进制产物一致性。

### 8.3 A-F 分类结论

**A. 项目运行时代码直接依赖**：AudioKit.PlaySound(AudioClip)、Settings.SoundVolume.Value、同音效模式/帧窗口设置；四种武器的 GetRandomItem。6 个直接依赖文件均有实际符号使用，没有“仅 using QFramework”的误计。

**B. 项目封装或其他模块形成的间接依赖**：8 个直接音效包装使用文件与 SettingPanel；附录 B 的项目继承/生命周期上游。XF 提供检测结果，QF 只负责随机抽取；XFAB 提供 AudioClip，QF 只负责该音效播放及相关设置。未检出 XF 包主动引用 QF 的源码/asmdef 证据。

**C. 编辑器、生成器与构建流程**：确认 link.xml 的 QFramework 保留配置；确认框架提供的编辑器菜单、自动入口及条件。未检出项目实际依赖 QF UI/视图生成或 ResKit/BuildKit 打包流程的可靠证据。空配置、工具存在、自动入口存在与用户实际使用记录分开。

**D. 资源、序列化或动态引用**：6 个输入音效 clip 和 XFAB 名称映射；Main 场景、XF 模块/FSM/视图的静态入口；AudioManager 动态创建入口；框架自带资源/残留事件另列。未检出业务资源直接 QF 脚本挂载或 QF 序列化类型名，但不据此排除动态依赖。

**E. 框架内部、示例或框架测试**：第 4 节的真实 AudioKit 内部支撑、8 个运行时自动入口、编辑器自动回调、自带 UIRoot、ScreenTransitionCanvas、ActionKitSingleFileCreator 配置及注释化 HotFixExample。UIPanelTester/ResKitUIPanelTester 是随框架提供的手工组件入口，不是项目自动化测试；没有找到它们进入业务资源/加载链的证据。APIExampleCode、文档、注释和压缩示例不计业务调用。

**F. 本轮未检出项目主动使用证据的功能**：QF Architecture 的业务分层/命令/查询、业务事件总线和主动绑定 API、UIKit 面板管理/UI 绑定、ResKit 主动资源加载/热更/打包、FSMKit、GraphKit 项目图、GridKit、TableKit、JsonKit、独立 IOCKit、DeclareKit、ScriptKit/MoonSharp、LiveCodingKit、BuildKit、CodeGenKit、LocaleKit、ZipKit 等。PoolKit/SingletonKit/ActionKit/ConsoleKit/LogKit 已作为内部支撑或自动入口出现，不能笼统说“没用”；BindablePropertyKit 的 PlayerPrefsFloatProperty 有设置支撑链。FluentAPI 除 GetRandomItem 外未检出其他业务主动使用。QF 音乐/人声播放、按名 AudioLoader、主动播放器控制、声音模式其他枚举值未检出业务使用证据。

### 8.4 未取得的证据与最小进一步核实信息

1. **当前运行是否真的到达调用链**：未运行 Play Mode。若要升级为运行确认，最小信息是当前 Main 场景一次成功启动、武器/爆炸音效与设置滑块操作的调用记录；静态入口已找到，不需要继续泛搜全库。
2. **编辑器自动配置实际分支**：未读取 SimulateAssetBundles、AutoGenUIPrefabPath、LiveCodingKit 内存设置等机器状态。只有当前 EditorPrefs/内存设置或明确的现用工作流记录能确认这些条件是否满足，菜单存在不够。
3. **构建产物一致性**：现有 manifest 含旧路径，没有当前源码生成包的证明；最小信息是现用资源来源及该来源产物的生成版本/时间。没有要求或执行重打包。
4. **序列化事件与残留类型名是否可调用**：存在 fileID 为 0 的残留和旧类型名；未经 Unity 当前反序列化/运行验证，不将其提升为有效依赖或判定一定失败。
5. **二进制/平台缺口**：没有反解析包、DLL、unitypackage，也未验证所有平台宏、裁剪或运行时反射结果。当前源码结论不覆盖未读取的二进制内容。
6. **版本控制基线**：没有 Git 仓库，Plastic CLI 不可用；只能记录当前磁盘内容摘要，不能给出相对提交的 diff 或判定用户此前哪些内容已经修改。

本轮静态使用清单已形成；上述事项是证据等级限制，不是迁移任务、删除判断或替代 API 方案。

## 附录 A：去重后的直接 API / 直接包装使用位置

| 文件，相对 P/ | 实际位置与引用 | 分类 |
|---|---|---|
| `MainModule.cs` | :27 PlaySoundMode/IgnoreSameSoundInGlobalFrames；:28 GlobalFrameCountForIgnoreSameSound | A |
| `Scripts/Controller/AudioController.cs` | :27-28 Settings.SoundVolume.Value；:49 PlaySound(AudioClip)；:25-29、45-49 为包装定义 | A/B 包装入口 |
| `Views/Player/Equip/Weapon/HeadGun/HeadGun.cs` | :25 GetRandomItem；:59 PlaySound("机炮4") | A + B |
| `Views/Player/Equip/Weapon/MachineGun/MachineGun.cs` | :23 GetRandomItem；:56 PlaySound("机炮3") | A + B |
| `Views/Player/Equip/Weapon/ShotGun/ShotGun.cs` | :24 GetRandomItem；:56 PlaySound("霰弹1")；:58,68 数学扩展不属 QF | A + B |
| `Views/Player/Equip/Weapon/RocketGun/RocketGun.cs` | :23 GetRandomItem；:55 PlaySound("火箭发射") | A + B |
| `Views/Player/SnakeBase.cs` | :120 PlaySound("机体爆炸1") | B |
| `Views/Enemy/EnemyBase.cs` | :133 PlaySound("机体爆炸1") | B |
| `Views/Player/Equip/Weapon/Bullet/PlayerRocketShell.cs` | :54,74 PlaySound("火箭爆炸1") | B |
| `Views/Enemy/Bullet/EnemyRocketShell.cs` | :54,74 PlaySound("火箭爆炸1") | B |
| `UI/SettingPanel/SettingPanel.cs` | :18 读 SoundVolume；:24 写 SoundVolume | B |

## 附录 B：已证实的项目上游与继承链

| 文件，相对 P/ | 调用关系 | 证据意义 |
|---|---|---|
| `Scripts/Main.cs` | :15 启动 MainModule；:32 ReadyRes("MainModule") | 项目启动入口，不是该文件直接调用 QF |
| `Views/Player/Segments/NormalSegment.cs` | :32 base.OnFallOff -> SnakeBase:120 | 项目继承后的间接音效 |
| `Views/Player/Segments/FunctionalSegment.cs` | :37 base.OnFallOff -> SnakeBase:120 | 同上 |
| `Views/Player/Segments/SnakeHead.cs` | :238 延迟调用蛇节 OnFallOff | 生命周期上游；Timer 为 XF |
| `Views/Enemy/Enemy_MachineGun/Enemy_MachineGun.cs` | :5 base.OnDead -> EnemyBase:133 | 项目继承后的间接音效 |
| `Views/Enemy/Enemy_RocketGun/Enemy_RocketGun.cs` | :9 base.OnDead -> EnemyBase:133 | 同上 |
| `Views/Enemy/Enemy_ShotGun/Enemy_ShotGun.cs` | :9 base.OnDead -> EnemyBase:133 | 同上 |
| `Views/Enemy/Enemy_Shield/Enemy_Shield.cs` | :19 base.OnDead -> EnemyBase:133 | 同上 |
| `FSM/Enemy/EnemyShieldAttack.cs` | :44 Enemy_Shield.OnDead -> EnemyBase | FSM 生命周期上游，不是 QF FSM |
| `Views/Player/Equip/Weapon/Bullet/Bullet.cs` | :57 enemy.OnDamage，死亡时可进入 EnemyBase | 条件性伤害上游，不计新的 QF 调用 |
| `UI/StartPanel/StartPanel.cs` | :22 LoadPanel<SettingPanel> | 音量 UI 的上游加载入口 |
| `UI/PausePanel/PausePanel.cs` | :32 LoadPanel<SettingPanel> | 同上 |

其余项目文件均读取并核对归属，没有检出新的直接 QF API 引用；一般场景、FSM、对象生成或伤害上游不穷举成整个游戏调用图。

## 附录 C：框架自带资源的完整去重清单

以下就是 QF 目录中的全部 5 个 .unity/.prefab/.asset；没有把它们计为游戏使用资源。资源自身与组件的 `.meta:2` 已对应核验。

| 资源，相对 Q/ | 资源 GUID | 实际序列化引用与入口限制 |
|---|---|---|
| `Toolkits/UIKit/Scripts/Resources/UIRoot.prefab` | `c70aac0028afd42eabc9e0c55824c824` | :267 UIRoot(`d2db7fffd623c478eba3d2cb84034d2f`)、:428 UIManager(`5dc84f25d84334e168bf64050b91e1fb`)、:476 Hide(`6eba024ea32574239b57b098f1dc6282`)；:270-277 绑定 UI 根字段。`UIKit/Scripts/UIRoot.cs:29-43` 有 Resources.Load("UIRoot")，未检出项目执行该 getter 的入口 |
| `Toolkits/_CoreKit/ActionKit/Scripts/ScreenTransition/Resources/ScreenTransitionCanvas.prefab` | `2d64c6a1947754149b3bd5ac266c99bc` | :94 ScreenTransitionCanvas(`0e21540f14ffdd9449d56f44db90db84`)、:97 ColorImage；ScreenTransitionCanvas.cs:9-11 经 PrefabSingletonProperty.cs:36 按类型名加载，未检出项目调用屏幕过渡功能 |
| `Toolkits/_CoreKit/ActionKit/Scripts/ActionKitSingleFileCreator.asset` | `4a4456594a6e6480bb3c61ed87d9030c` | :12 SingleFileCreator(`d09d8a43c87a14101b9a7e0e34f7b584`)、:19 FileName=ActionKit.cs、:21-35 Scripts、:37-64 DependencyScripts、:65 原作者输出路径；框架生成配置存在不等于项目使用过单文件生成 |
| `Toolkits/ResKit/Scripts/HotFixDowload/HotFixExample/CommonConfirm.prefab` | `9f80635f1c1413e4db72f37841ac059c` | :425 CommonConfirm(`ad07d2bd11429d94bb2e34196c0dfa93`)、:428-431 四个 UI 字段；:185,356,884,1021 为 Bind(`0d51f3a7c41ab0346b49ae50d456bece`)。相应类声明已注释化 |
| `Toolkits/ResKit/Scripts/HotFixDowload/HotFixExample/TestHotFix.unity` | `52e621845fa46864d9a3ed2cf14a6a2f` | :331 MainConfig(`6cbb1440bd1ca124ea55f1ad9a2c7175`)、:558 HotFixedPanel(`faf339824fd2ee044a605dcac1c044ba`)、:482 UIRoot prefab、:689 CommonConfirm prefab；不在启用场景列表中，相关样例类声明已注释 |

## 附录 D：业务资源代表证据与排除清单

### D.1 实际 QF 随机取项脚本挂载的武器

| Prefab，相对 P/Views/ | prefab GUID | 当前项目脚本 GUID / m_Script 行 | 字段与入口 |
|---|---|---|---|
| `Player/Equip/Weapon/HeadGun/HeadGun.prefab` | `8ae609d291a427543af2b1a31cd2548d` | `06f0e41cd86444ad99db943e675fafd6` / :157 | :164 PlayerBullet、:167-168 gunPos；资源名映射 :17-20 |
| `Player/Equip/Weapon/MachineGun/MachineGun.prefab` | `51ad03b4575a5894fb3b2168fa045821` | `dda334bc197d45a0a53419b4ef3be55c` / :47 | :54 PlayerBullet、:57-58 gunPos；敌人 MachineGun prefab 也挂该脚本 :359 |
| `Player/Equip/Weapon/ShotGun/ShotGun.prefab` | `37269aa038179c746a738e12acaa80b1` | `9aff17a959aff12499aa1c91857d981a` / :125 | :132 PlayerBullet、:136-138 gunPos/bulletCount；敌人 ShotGun prefab 也挂该脚本 :130 |
| `Player/Equip/Weapon/RocketGun/RocketGun.prefab` | `d28eb8cda2c9e974eaafca9da1d9be7d` | `1df6b288855a3ae4fa463893f6bc11ce` / :47 | :54 PlayerRocketShell、:57-58 gunPos；敌人 RocketGun prefab 也挂该脚本 :329 |

敌人资源分别为 `P/Views/Enemy/Enemy_MachineGun/Enemy_MachineGun.prefab`、`Enemy_ShotGun/Enemy_ShotGun.prefab`、`Enemy_RocketGun/Enemy_RocketGun.prefab`。资源/脚本挂载确认，实际装备选择与 Update 执行未运行验证，不将存在资源等同于每种武器必然出现在每局游戏。

直接音效包装调用/继承上游对应的其他资源也核对到脚本挂载：`Player/Segments/Snake.prefab:132`、`NormalSegment.prefab:131`、`FunctionalSegment.prefab:131`；`Player/Equip/Weapon/Bullet/PlayerRocketShell.prefab:143`；`Enemy/Bullet/EnemyRocketShell.prefab:46`；四类敌人 prefab 的自身脚本分别位于 :236、:120、:372、:247。上述都相对 `P/Views/`，不误记为 QF MonoBehaviour 基类。

### D.2 九个 UI 均为项目 XF Panel

路径统一为 `P/UI/<名称>/<名称>.prefab`；下列 GUID 对应同目录项目脚本的 `.cs.meta:2`，而不是 QF UIPanel。面板名称在资源映射 :24-32，UI 目录也被 `P/MainModule.asset:33-41` 的 panels 配置引用。

| 名称 | 项目脚本 GUID | prefab m_Script 行 | 已找到的源码加载入口 |
|---|---|---:|---|
| StartPanel | `2d2e4dd15b7d86a4f82565af1c24a23c` | 1469 | GameStart.cs:24,30 |
| GameSettingPanel | `6c46b4baeb70931459d5e1256ea4e35b` | 1693 | GameSetting.cs:27,33 |
| GamePanel | `032ba02bb95047b4fa72c19739b4735c` | 143 | Gaming.cs:126 |
| GameOverPanel | `08528db4ed6cd594dafa4bf99bb47049` | 601 | GameOver.cs:19 |
| PausePanel | `4c4822a3ace729947930d15e8dd79e06` | 52 | GamePanel.cs:60 |
| SettingPanel | `75297ebc16f63344ab329ec72dc30a6c` | 686 | StartPanel.cs:22、PausePanel.cs:32；QF 音量通过项目包装间接依赖 |
| Description | `e3f8608914e858e43a86538de394770a` | 458 | PausePanel.cs:37 |
| LoadingPanel | `9228cd6e3973df24da6240327737e5ba` | 89 | SceneController.cs:19，DontDestroyUI |
| TipPanel | `eaea3420297b2bf468c984b5ffe984db` | 190 | TipController.cs:8；尚未找到该控制器的业务调用方，不断言进入游戏流程 |

### D.3 源资源与打包配置入口

`P/MainModule.asset` 的资源 GUID 为 `1ff670f910b32ff40944315e20fa9be4`；`:12` 的脚本实际是 XFABProject，而非 MainModule.cs 实例。`:24-80` 配置 Views、UI、Scenes、FSM、音效、Art/Prefabs、EffectPrefab 目录；`:82-87` 记录 .unity3d、版本与复制到 StreamingAssets 设置。这提供资源打包/动态名称链的配置依据，不证明曾调用 ResKit 构建器。

三个动态场景源资源 GUID：`P/Scenes/Start.unity` 为 `4f4603c9eb67e0b4982e8a94789c7c18`，`Setting.unity` 为 `fed5c16b79c54e84eb46a3c1ec02f372`，`Game.unity` 为 `8b3135fee402e4c4d8af2db6dc6e5587`。三个 XFFSM 资源 GUID：`P/FSM/GameState/GameStateManager.asset` 为 `9fe8571b85942ac46a812b8213cd7791`，`P/FSM/Enemy/EnemyState.asset` 为 `61b9b8391932a94468947fb56bcb4fcf`，`EnemyShield.asset` 为 `2fbd7dbf9c59f3e4bb05431e92915ddf`。

## 9. 本轮变更核对

本轮授权写入范围仅为本报告。未对工程源码、资源、.meta、Packages、程序集、生成项目文件、忽略规则或工具配置执行修改；未安装依赖、启动 Unity、编译、测试、重导入、格式化、清缓存、提交、回退或覆盖用户改动。

交付前再次执行了两组只读内容摘要和 Git 状态检查：

| 核对范围 | 最终文件数 | 最终 SHA256 | 对照结果 |
|---|---:|---|---|
| 审计开始时的源码/文本/序列化/元数据及生成项目文件范围 | 3213 | `A625B4B0BA241C5310645692A6783481774C4FFD1BF532F1C9AD4FEBFCC87625` | 与审计开始一致，MatchesStart=True |
| 写报告前的完整 Assets/Packages/ProjectSettings/.kilo 文件及生成项目文件范围，包含二进制资源 | 3570 | `2B343CA1C45B6BA1250CB8FF6B7A15A7A247AD716DEBBD121DC67007F041C99B` | 与写报告前一致，MatchesBeforeReport=True |

本报告不在两组摘要范围内。再次 `git status --short --untracked-files=all` 仍返回非 Git 仓库；没有伪称取得 Git diff。内容摘要证实监测范围的工程文件未改变；本轮工具的工程写操作只有创建和完善本报告。未对整个 Library/缓存/构建输出做内容摘要，不把该核对扩展成所有机器文件均未变化的断言。
