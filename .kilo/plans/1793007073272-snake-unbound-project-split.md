# 主游戏迁出与 YLFramework 开发宿主拆分实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. 同一 Unity 工程的有状态操作串行执行，不同时修改两个 Editor 的共享状态。

**Goal:** 将现有主游戏完整迁入独立的 SnakeUnbound 工程和仓库，当前工程仅承载 YLFramework 开发、示例和测试，双方不互相依赖。

**Architecture:** 从当前已保存工作区建立可核对快照，按游戏及其资源、编译和配置依赖闭包构建干净接收工程。先独立验证游戏，再清理源工程游戏内容并建立最小 YL 开发宿主；保留源工程 XF 共存环境，不改框架分发机制。

**Tech Stack:** Unity `2022.3.62f3c1`；现有 XF/XFAB/XFFSM/Overlap；Unity MCP `1.5.0`；Unity Test Framework；Windows64 Mono。Unity 包与 Git revision 以执行预检确认的实际运行组合固定。

**Spec:** 本文件“已确认设计”是用户于 2026-10-26 逐轮确认的 Q1-Q16 设计记录；Q16 补充并限定 Q10 的数据隔离与证据口径。当前仅完成规划，未执行迁移、Unity 验证或 Git 写操作。

## 已确认设计

```text
主游戏与 YLFramework 分离，双方不互相依赖
|-- SnakeUnbound：独立新工程、新仓库、main、当前工作区快照
|   |-- 游戏仍用 XF/XFAB，不接入 YL
|   |-- 产品保留 TANGENT LINE / 解限蛇与现有应用标识
|   |-- 保留路径、GUID、程序集身份和运行行为
|   `-- 保留 MCP 1.5.0 与游戏测试，不搬框架开发工具
|-- CGJ2024：现有路径、仓库、历史与当前分支不变
|   |-- 保留 YL、现有 XF 共存环境、可选集成及开发工具
|   `-- 转为 YLFrameworkDev 产品与最小常驻开发场景
`-- 先复制和验证，后清理；两仓各一次本地提交；不操作远端
```

## Global Constraints

- 源工程固定为 `G:\Unity\Projects\CGJ2024`，游戏目标为 `G:\Unity\Projects\SnakeUnbound`。不覆盖或复用同级已有的 `YLFramework`、`XFGame` 工程，不使用共享 Assets/Library 的目录链接。
- Unity 固定 `2022.3.62f3c1`，不升级。仅新工程修正与实际运行环境不一致的依赖声明；源工程 manifest/lock 不变，锁文件交由 UPM 正常生成，不手改。
- 游戏正式身份为 `companyName=TANGENT LINE`、`productName=解限蛇`、Standalone 应用标识 `com.DefaultCompany.CGJ2024`。其余既有发布字段及中文标题保留，不重绑云服务。
- 原宿主改为 `companyName=TANGENT LINE`、`productName=YLFrameworkDev`、Standalone 应用标识 `com.tangentline.ylframeworkdev`；默认场景为 `Assets/Scenes/YLFrameworkDev.unity`。
- Q16：仅新工程验证阶段临时使用 `TANGENT LINE.SnakeUnboundValidation.<RunId>` 作为 Company Name，Product 保持 `解限蛇`。新游戏的验证与 XFAB Bundle 构建使用隔离身份；完成或失败都恢复正式身份。最终正式身份 Player 只构建、静态核对，不自动启动。
- 保留当前已保存游戏内容，包括 `EnemyShield.asset`、`EnemyState.asset`、`GameStateManager.asset` 的既有修改。保护已有框架计划修改、未跟踪夹具和其他用户内容，不顺手提交或删除。
- 已有资产连同文件夹 `.meta` 保 GUID；新宿主场景、测试脚本及目录的 `.meta` 由 Unity 生成。游戏类名、全局 namespace、`Project.Game.MainModule*` 程序集名、资源字符串和相对路径不改。
- 新游戏仅保留必要运行/测试依赖及 MCP `1.5.0`。不搬 YL、DOTween、HybridCLR、Editor Enhancers、YL 分析器/验收工具、旧会话、本机配置和权限；MCP 依赖按其正常机制解析，不复制整个 NuGet 插件目录充数。
- 源工程保留现有 XF 包、embedded Logger 包、共存 Resources、DOTween/HybridCLR 等可选集成环境及其宏；不以“零 XF”验收，不做 UPM/框架分发改造。
- 仅新游戏建立必要 `.gitignore`、`.gitattributes` 和 Git LFS 本地配置，保留完整 MIT 及适用第三方声明。不新增/修改 AGENTS、权限配置、全局 Git 配置，不迁移含密钥的本机设置。
- 新游戏 `main` 初始提交、源仓库本批提交均在验收通过后创建。源仓库不切分支、不重写历史；不创建/更改远端、不推送、不进行历史或 LFS 瘦身。
- 不自动保存用户脏场景，不对同一 projectPath 启动第二个 Unity，不强制关闭用户 Editor，不清空旧玩家偏好/缓存或整个 Library。
- 非目标：玩法/API/资源加载重构、游戏重新命名发行、其他平台、IL2CPP、新增热更新、全盘旧构建归档清理、新建通用迁移框架。

## 事实与限制

- 游戏直接依赖见 `Assets/Games/MainModule/Project.Game.MainModule.asmdef`；`GameIndependenceTests` 已检查 Editor/Player 编译图、二进制及资源闭包不含 YL。YL 产品程序集同样不引用游戏。当前不需要再次做游戏-YL 解耦。
- 启动链为 `Assets/Scenes/Main.unity` -> `Main` -> `ReadyRes("MainModule")` -> `MainModule` -> `Start`。`Start/Setting/Game` 由 XFAB Bundle 加载，不应顺手改成普通 Build Settings 场景。
- `Assets/Resources/XFGameFramework/Canvas.prefab` 与 `EventSystem.prefab` 是共享项，游戏需要复制，YL 共存测试仍需源副本。Json.NET 当前由 XFAB 传递提供；本批保留源 XF，不处理该依赖归属重构。
- 当前 manifest/lock/缓存存在版本差异，URP、UGUI、Cinemachine 尤其需要现场核对。缓存目录与历史记录不是当前注册包证明；读取 `PackageInfo.GetAllRegisteredPackages()` 和实际编译图。
- XFAB `ProjectBuild.cs:109` 会删除 `XFABTools.DataPath(project.name)`；`XFABTools.cs:26-32,277-297` 表明 Editor 使用 persistentDataPath，Windows Player 使用自身 StreamingAssets。因此即使新工程路径不同，正式产品身份下的 Editor 打包仍会触及旧数据，必须先应用 Q16 隔离身份。
- 游戏已有 `GameAudioPreferences` 保存/恢复四个浮点键、五个整数键及其存在性。它仍需正常使用；隔离身份进一步避免测试失败影响正式数据。
- 历史临时目录 `C:\Users\Kano\AppData\Local\Temp\kilo\game-xf-decoupling-20261026-implementation` 仅可作参考，不含后续全部修正；旧 `Build/解限蛇Windows` 是 QFramework 链，不能当当前基线。
- 本轮没有启动 Unity 或运行测试。LFS 可用性命令受权限限制，尚未验证；不得据此修改权限或绕过工具限制。执行时权限、LFS、包解析或提交身份不满足就暂停。

## Review Focus

- 复制后用户又改动 FSM/配置，删除源文件会丢新修改：任务 1 封存输入清单，任务 4 删除前逐项重新比对，不一致即停。
- 共享 XF Resources 被误当游戏专属搬空：任务 2 比对两边 GUID/内容，任务 4 复跑 Core 默认资源与共存测试。
- Editor 的 Assets 模拟加载掩盖旧 Bundle、缺映射或隐式插件：任务 2 核对真实编译/导入闭包，任务 3 使用全新 Bundle 和隔离 Player 验证。
- 包声明漂移或遗留 `DOTWEEN` 等宏使干净工程无法编译：任务 1 确认注册包，任务 2 固定 revision 并检查被排除插件对应宏、asmdef 和实际二进制引用。
- 更换宿主后残留游戏 Splash、Build Scene、脚本或真实数据副作用：任务 3 验证身份恢复及隔离证据，任务 4 添加宿主边界回归并检查设置/场景的资产闭包。

## 文件边界

以下路径相对各自工程根；复制保持原相对路径与 `.meta`。

| 位置 | 操作与内容 |
| --- | --- |
| 新游戏 `Assets/Games/MainModule/**` | 复制全部当前游戏源码、资源、配置和自有测试 |
| 新游戏 `Assets/Scenes/Main.unity` | 复制及保 GUID，作为唯一启用的游戏启动场景 |
| 新游戏 `Assets/Resources/` | 复制 `XFABManagerSettings.asset`、`XFGameFramework/Canvas.prefab`、`EventSystem.prefab` 及必要父目录 meta |
| 新游戏渲染资产 | 复制 `Assets/Settings/UniversalRP.asset`、`Renderer2D.asset`、`Assets/UniversalRenderPipelineGlobalSettings.asset`、`Assets/DefaultVolumeProfile.asset` 及实际引用闭包 |
| 新游戏 `ProjectSettings/` | 迁入 ProjectVersion、ProjectSettings、EditorBuildSettings、GraphicsSettings、QualitySettings、InputManager、TagManager、Physics2DSettings、TimeManager、AudioManager、URPProjectSettings、PackageManagerSettings 等已证实需要的设置；不全盘复制本机配置 |
| 新游戏 `Packages/` | 新 manifest 固定已确认版本/revision，正常解析 lock；MCP 所需资产正常生成 |
| 新游戏仓库根 | 新建必要忽略规则，继承 LFS 属性，原样保留根 `LICENSE` 及适用依赖声明；不复制 YL 专属 `.editorconfig`、规划和工具配置 |
| 新游戏生成输出 | 在新工程重建图集、`AssetBundles/MainModule`、`Assets/StreamingAssets/MainModule`、Player；Bundle/Player/缓存不纳入提交，自动图集沿用已核查的源跟踪策略 |
| 源工程新增 | `Assets/Scenes/YLFrameworkDev.unity`、配套 meta；`Assets/YLFramework/Tests/EditMode/Editor/Distribution/DevelopmentHostBoundaryTests.cs` 及 meta |
| 源工程修改 | `ProjectSettings/ProjectSettings.asset`、`EditorBuildSettings.asset`；必要时增量更新现有 `Assets/YLFramework/Documentation/README.md`、`Documentation/IntegrationPlan.md` 与本计划验收记录 |
| 源工程清理 | 验收后移除 `Assets/Games/MainModule/**`、`Assets/Scenes/Main.unity` 及 meta，清理明确属于游戏的活动 Bundle/StreamingAssets/自动图集和游戏设置引用 |
| 源工程保留 | `Assets/YLFramework/**`、`Assets/YLFrameworkData/**`、现有 Packages、XF 共存 Resources、Plugins、Tools、用户其他改动；旧 Build/ZIP 归档不顺手删除 |

## 任务 1：预检与快照封存

**Consumes:** 上述授权与当前已保存工作区。**Produces:** 本批只读输入快照、SHA-256/GUID 清单、真实包版本清单及隔离 RunId；证据放本批独占临时目录，不创建第二份计划/报告。

- [x] 读取有效 AGENTS/CodingStyle，重查 `git status --short --branch`、`git diff`、`git log --oneline -10`，记录源 HEAD、分支和已有差异；确认目标路径仍空闲。目录被占用或有直接冲突时停止，不覆盖。
- [x] 通过现有源 Editor 查询 projectPath、版本、Play/编译/脏场景状态及注册包。仅检查和记录，不自动保存场景，不把生成 csproj 或缓存版本当 Unity 运行证据。
- [x] 检查正常 Git/LFS 命令、提交身份及真实工作树二进制是否可用，确认不存在未还原的 LFS 指针。受限则请求正常执行权限或环境修复，不换壳绕过、不更改全局配置。
- [x] 对游戏根、外部资源、相关设置和 meta 生成确定性输入清单；记录三个既有 FSM 修改及源共享 XF Resources 的 GUID/哈希。仅复制游戏相关当前磁盘内容；排除未跟踪 YL 夹具和本机状态。
- [x] 以实际编译、资产与导入依赖闭包确定 Unity 包集。四个 XF 直接根为 XFABManager、XFGameFramework、XFFSM、Overlap，其他真实必要传递依赖不得因没有 using 就剔除。
- [x] 核对当前 lock 候选 revision：XFAB `c35dc4058ecd831409199b43cb328024202d4557`、XF `2182708aecc596f30739fa8634fe2b9b6946ddd7`、XFFSM `b06333c2cdc801fcc401f14c252853bf099aa1b7`、Overlap `4961d8358119485c94d6f0f74b6ca5d8b4163460`。与注册状态相符才用于新 manifest 的原 Git URL `#<revision>`；不擅自更新。
- [x] 建立本批临时证据目录，记录正式/隔离身份和允许写入、清理的绝对路径。共享资源、旧玩家缓存、未知输出及旧归档列为不可清理。

**门槛:** 输入、版本、资源所有权和写入边界可核对；源工程未改变。这里不运行正式身份下的 XFAB 构建来“验证基线”。

## 任务 2：建立独立游戏工程

**Consumes:** 任务 1 的快照/依赖清单。**Produces:** 独立可导入、可编译的目标工程，源游戏仍完整保留。

- [x] 验证父目录，创建目标根并初始化 `main` 仓库，配置仅本仓库 LFS 与必要忽略规则；保留 Unity 缓存、Bundle、Build、本机 XF 设置和密钥类文件的排除。此时不提交、不建立远端。
- [x] 按文件边界复制源文件及 meta，不带 Library/Temp/obj/旧 Player/旧 Bundle，不改资源名、类型、namespace 或程序集名。共享 XF Resources 是复制，源文件必须保留。
- [x] 写入新 manifest：固定任务 1 的实际 Unity 包、四个 XF 根及必要依赖、MCP `1.5.0` 和测试包。正常解析 MCP 依赖，不复制整个源 Plugins/NuGet；不加入无游戏用途的可选 XF 扩展。
- [x] 仅新工程清理被明确排除集成对应的宏，例如 `DOTWEEN`、YL 可选集成宏；保留游戏需要的宏并检查 asmdef/导入器依赖。源工程宏和可选插件不变。
- [x] 复制正式产品设置后，在任何测试/Bundle 构建前切换到隔离 Company Name；从此到任务 3 结束统一置于 finally 恢复保护，任务 2 失败也必须恢复。首次打开用指定 Editor，包解析后读取新注册清单与源基线逐项比较，不接受目录名推断。
- [x] 比较复制清单/GUID；核对 Both 输入模式、原输入轴、Tag/Layer/SortingLayer、Physics2D 矩阵、`fixedDeltaTime=0.02`、真实音频 voice=64；确认 MainModule 资源目录 GUID 映射、URP 2D Renderer 和唯一 Main 构建场景。
- [x] 确认 MCP 以新 projectPath 正常连接，验证 ping/Editor 状态和测试能力；插件导入不等于连接已就绪。新工程 Unity 编译通过后，运行完整 `Project.Game.MainModule.Tests.EditMode` 和 `Project.Game.MainModule.Tests.PlayMode`，保留原有效断言，不新增对 YL 的依赖。

**门槛:** Unity 实际编译和两套游戏测试通过，全部选择的测试完成且无失败/非预期跳过；实际程序集和资源闭包无 YL，源共享资源仍保持原 GUID/内容。不能以 dotnet build 替代 Unity。

## 任务 3：重建资源与隔离 Player 验收

**Consumes:** 任务 2 的干净工程。**Produces:** 隔离身份运行证据、全新 Bundle 清单及正式身份交付产物；正式项目设置恢复。

- [x] 首次构建前断言当前 Company 为本批隔离身份，`XFABTools.DataPath("MainModule")` 及所有输出均在本批拥有的路径内；不是正式玩家目录、源工程或旧 Player 路径。任一不满足即停，不调用 Clear/Build。
- [x] 用 XFAB 正式构建流程在目标工程重建整个 MainModule 的 Windows64 Bundle 与自动图集，正常复制 StreamingAssets。记录清单、映射、文件大小/哈希，并比对构建输出、目标 StreamingAssets 与 Player 内文件；不搬源旧包充数。
- [x] 构建 Windows64 Mono 隔离验证 Player，使用本批新输出目录。以现有游戏 PlayMode/Player 测试覆盖菜单、设置、游戏、暂停、返回、重进、失败、真实资源加载及音频；保留 GameAudioPreferences 的保存/恢复与资源释放。PCM/声音验证必须有有效 Listener 和可推进 DSP 的音频上下文，不能把无声 headless 结果当声音通过。
- [x] 记录该版本 Windows Player 的 XFAB 资源路径及新 Bundle 身份，检查 Managed 无 YL/遗留游戏预编译依赖。Editor-only 用例不计作 Player 用例；回传故障、构建失败和测试失败分别记录，不以已有日志或旧载体补齐通过。
- [x] 在 finally 恢复新工程的正式 Company/Product/应用标识并重新读取确认。失败/中断时先恢复设置再停止，源游戏不得清理；不接触正式玩家缓存/注册表，不改 PackageCache 解决隔离。
- [x] 恢复正式身份后，使用已验证的全新 Bundle 构建正式 Windows64 Mono 交付产物。构建前检查项目/包钩子不会再次在正式身份下触发 XFAB 清缓存；有该行为则暂停处理，不能静默运行。核对身份、程序集、Bundle 哈希和构建结果，不自动启动正式身份产物。
- [x] 将“隔离 Player 实际运行”“正式身份产物构建/静态核对”“人工试玩/听感”分别记录。没有人工结果不得宣称已通过主观听感；正式偏好空间下的兼容性运行验证本批明确不执行。

**门槛:** 隔离 Player 的真实 Bundle 路径和流程通过；正式身份已恢复且交付产物构建成功、内容与已验证 Bundle 一致。到此才允许清理源游戏。

## 任务 4：源工程转为 YL 开发宿主

**Consumes:** 任务 3 的通过证据与封存快照。**Produces:** 不含主游戏的 YLFrameworkDev 宿主及回归结果，源 XF 共存环境不变。

- [x] 删除前重新比对将清理的游戏文件、外部入口与相关配置。自快照后出现的用户新修改、未保存场景或直接冲突必须暂停协调，不能用旧快照覆盖后删除。
- [x] 在 `DevelopmentHostBoundaryTests.cs` 添加宿主身份/入口与资源隔离测试，使用当前测试程序集，不新建框架。以下关键断言在现状应明确失败；运行并记录 RED，不能只以编译错误代替预期断言失败。

```csharp
// DefaultBuildEntryAndIdentityBelongToFrameworkDevelopmentHost
Assert.That(PlayerSettings.companyName, Is.EqualTo("TANGENT LINE"));
Assert.That(PlayerSettings.productName, Is.EqualTo("YLFrameworkDev"));
Assert.That(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone),
    Is.EqualTo("com.tangentline.ylframeworkdev"));
CollectionAssert.AreEqual(new[] { "Assets/Scenes/YLFrameworkDev.unity" },
    EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
```

- [x] 第二个测试 `DevelopmentHostHasNoMainGameDependencies` 要求宿主 SceneAsset 存在、其依赖不进入 `Assets/Games/MainModule`；源无 MainModule 目录、`Main.unity`、游戏 StreamingAssets，实际编译图无 `Project.Game.MainModule*`。检查全部游戏 Logo/Splash/图标对象引用已清理，无悬空引用，不能通过路径不存在得到空闭包冒充成功。
- [x] 使用 Unity API 新建 `Assets/Scenes/YLFrameworkDev.unity` 和新 meta，实例化现有 `Assets/YLFramework/Prefabs/YLFramework.prefab` 与必要 Camera；保证唯一 YLFrameworkRoot，不把空场景 Play 当作框架自动启动，不新增大型 Demo 或业务启动器。仅保存本批拥有的场景；删除资产前关闭已保存且已迁出的游戏场景，避免留在打开场景中的旧组件变为 Missing Script，保留无关场景，遇脏场景暂停。
- [x] 按批准值修改宿主身份，以新场景替换 Build Settings；清理所有游戏 Logo/Splash 引用。保持渲染、输入、层、包、可选集成和工具等其他开发配置，不重置全工程。
- [x] 通过 Unity 资产删除机制移除已迁出的 MainModule 与 Main 场景及 meta。清理已确认游戏专属的活动 StreamingAssets、自动图集和构建输出；只处理本批清单中的 MainModule 子树，其他文件、父目录中的用户内容和旧 Build/ZIP 归档不动。共享 XF Resources、XFAB 设置、YL 数据/测试/工具必须保留。
- [x] 正常刷新和重新编译；验证无游戏编译根/资源依赖、无 Missing Script，宿主场景可正常 Play/退出且无重复根。新测试转 GREEN，不借旧游戏 DLL，不删除整库 Library 排错。
- [x] 运行受影响回归：`DevelopmentHostBoundaryTests`、`FrameworkAssemblyIndependenceTests`、`FrameworkVisibilityTests`、Core 默认资源/身份/EditMode 与 PlayMode 共存测试、受影响的 UI/音频 Acceptance 资源回归及 `ExportUnityPackageTests` 正式导出入口。先阅读它们的所有权/保护前置，避免覆盖未跟踪夹具；不机械执行会接管旧冻结制品的全套历史 Stage 驱动。
- [x] 检查实际导出内容不带游戏或开发宿主；保留 `.unitypackage` 现有根/版本/可选集成策略。旧游戏路径的空保护快照不算新的保护证据；本批用真实开发场景、共享 Resources 和设置完成边界检查，不削弱原有效断言。

**门槛:** 无游戏的源工程实际编译、新宿主运行、受影响测试及正式导出通过；共享资源与源依赖未被误改。源验收失败则保留目标与备份，停止提交；仅恢复本批拥有的变更，不 reset/覆盖用户工作。

## 任务 5：文档收口、差异检查与本地提交

**Consumes:** 双工程验收证据。**Produces:** 两个本地提交、可定位产物/日志及明确未验证范围，不涉及远端。

- [x] 仅增量维护现有文档：README 的源仓库开发宿主入口、XF 为共存验证对象、框架分发机制未变；IntegrationPlan 添加本批迁出状态，不改写历史验收。本计划记录实际证据、路径、结果与限制，不新建重复过程报告或新游戏 README/AGENTS。
- [x] 对两个仓库分别检查 `git status`、实际 diff/未跟踪清单、`.meta`、`git diff --check`、LFS 指针/真实资源及秘密排除；源未授权改动和夹具仍完整存在。新增 YL 测试只对自身路径执行现有格式 Check，不全库 Apply。
- [x] 复核 Q1-Q16、五项 Review Focus、正式身份恢复和证据分类。静态/编译/测试/实际运行分别报告，保留失败/阻塞，不把历史结果或未知测试数写为本次通过。
- [x] 新游戏只暂存迁入源码、资源、测试、必要配置/生成锁文件、许可证、属性和忽略规则；检查暂存 diff，创建一次中文初始提交，例如 `feat(game): 建立解限蛇独立工程`。Bundle、Player、Library、本机设置不入库。
- [ ] 新游戏提交成功后，源仓库只暂存本批迁出/宿主/测试/必要文档与本计划，检查 staged diff 再创建一次中文提交，例如 `refactor(ylframework): 迁出主游戏并建立独立开发宿主`。不使用 `git add .`，不夹带 `.kilo/plans/1792420261057-xf-ecosystem-integration.md` 等既有无关计划修改或未跟踪夹具；若同文件混有旧修改，仅按本批内容精确暂存。
- [ ] 检查两仓提交 Hash、当前分支、实际剩余工作区差异及 remote 未改变。不 amend、不跳 hook、不推送；失败先修复再正常提交，不强制重写历史。将 Hash 和交付路径报告在最终回复，不为补 Hash 再制造 docs 提交。

## 最终完成条件

- [x] SnakeUnbound 是独立 main 仓库，可从原始源码/锁定依赖重建，不依赖原工程路径、YL 或旧产物。
- [x] 当前已保存游戏内容与 GUID 完整保留；源工程无活动主游戏，YL 开发宿主与共存验证资源可独立使用。
- [x] 版本、测试、隔离 Player、正式产物和正式导出都有本批可追溯证据；正式身份玩家数据未被测试/打包清理或改写。
- [x] 最终项目设置恢复正式游戏身份；正式身份 Player 未自动启动，人工听感和其他平台未验证的限制如实交付。
- [ ] 两仓各一份本地提交，源历史与无关修改保留，无远端操作、权限修改或额外需求扩张。

## 本批执行记录

### 任务 1：完成（2026-10-26）

- 源基线 `74e85e5927bb1a3ceea2a26e4d542683940f4ed1`，分支 `integration/xf-yl`，remote 名称 `origin`；目标 `G:\Unity\Projects\SnakeUnbound` 预检不存在。已有三个 FSM 修改、旧集成计划差异及所有未跟踪内容均保留。
- 本批证据根：`C:\Users\Kano\AppData\Local\Temp\kilo\snake-unbound-split-20261026T105031Z-fe32d7df`；RunId `20261026T105031Z-fe32d7df`；隔离 Company 为 `TANGENT LINE.SnakeUnboundValidation.20261026T105031Z-fe32d7df`。
- 实际源 Editor PID 27968，路径/版本为指定工程及 `2022.3.62f3c1`；唯一打开场景 `Assets/Scenes/Main.unity` 无脏状态，未 Play、未编译、未更新。未运行正式身份 XFAB 构建或测试。
- `snapshot/` 与 `input-manifest.json` 封存 522 文件、56,532,672 字节及 SHA-256/GUID，复制后逐项重验一致，无 LFS 指针。`protected-manifest.json` 和 `protected-snapshot/` 单独保护 58 个用户文件，不进入目标工程。
- `source-packages.json` 来自 `PackageInfo.GetAllRegisteredPackages()`；实际 URP/core/ShaderGraph `14.0.12`、URP config `14.0.10`、UGUI `1.0.0`、Cinemachine `3.1.6`、Timeline `1.8.12`。四 XF 注册来源/解析路径与 lock 候选 revision 前缀一致；该 Editor 的 `PackageInfo.git` 元数据为空，完整 revision 依据 lock，未宣称 API 提供完整 hash。
- `source-compilation.json`、`game-compilation-closure.json` 和 `source-binary-closure.json` 分别记录编译图、游戏闭包和 102 个实际二进制闭包程序集，无 YL 引用；资产闭包的工程外部根仅为批准的 8 个资产。编译器自动加入的可选预编译引用不等于二进制实用依赖。
- `source-before.patch` 保存开工差异；`source-game-outputs.json` 记录游戏 StreamingAssets 449 文件、Bundle 输出 448 文件和自动图集 4 文件。自动图集源策略为受 Git 跟踪，新游戏沿用重建后跟踪策略，不复制旧图集或 Bundle。
- 正常命令 `git lfs version`、`git lfs ls-files -l`、`git lfs fsck --objects --pointers` 全部通过，author/committer 身份有效。预检后源状态与开工相同。
- Ruling: 以本计划的执行记录和专属临时证据作为 ledger，不采用通用技能的新 worktree/重复 progress 文档步骤；本计划明确固定源分支与双工程路径并禁止重复报告。误判成本为记录集中而非自动技能脚本管理，不改变实现或验收门槛。
- Pre-flight: 任务 1 快照供任务 2 复制、任务 3 隔离路径断言、任务 4 清理前重验；任务 3 门槛先于任务 4 删除；任务 5 提交最后进行，接口与顺序无冲突。
- 外部资料限制：Context7 返回月配额耗尽。本批优先采用已安装包源码及实际 Editor 查询，不将通用文档替代版本证据。

### 任务 2：完成（2026-10-26）

- 目标独立仓库 `G:\Unity\Projects\SnakeUnbound`，`git init --initial-branch=main`、`git lfs install --local` 完成，无提交、无远端。复制快照中的 520 个非 Packages 文件，新增最小 manifest 与忽略规则；原根 MIT 与 LFS 属性原样复制，没有迁移 YL、可选 XF 扩展或旧 NuGet 插件目录。
- 目标 UPM 正常生成 lock，实际注册 53 包；`target-packages.json` 逐项比较全部包的版本与来源，均匹配源实际组合。源 manifest/lock、共享 XF 资源及其 meta 的哈希仍不变，见 `source-shared-after-target-tests.json`。
- 去除新工程遗留的 DOTween、Odin、MoreMountains 宏及 MCP 已就绪标记后，让 MCP 正常下载/恢复自己的 NuGet 依赖。实际最终宏仅 `UNITY_MCP_READY;UNITY_MCP_DEPS_17`；源码、资源名、程序集身份和 GUID 不变。
- `target-preflight.json` 验证 519 个保留输入逐字节一致（仅排除新 Packages 与有意变更的 PlayerSettings），包括输入轴、层/Tag/SortingLayer、Physics2D、音频和渲染设置；Both、fixedDeltaTime=0.02、64 real voices、Mono、URP 2D Renderer、7 个 Bundle 目录 GUID 和唯一 Main 构建场景均通过。
- 实际 Unity 编译完成；`target-compilation.json` 检查 Editor/Player 图无 YL、无 CGJ2024 绝对依赖。完整游戏测试：EditMode **20/20**、PlayMode **39/39**，失败 0、跳过 0；结果分别在 `game-editmode.results.json`、`game-playmode.results.json`。包含真实流程、活跃 DSP 与非零 PCM 断言，未改变游戏断言或偏好恢复语义。
- 目标 MCP 初始云默认配置 401，且 Editor 的 HttpClient 获取官方 SHA256SUMS 失败而拒绝启动，未跳过完整性门禁。使用 `gh release download` 正常取得同版本 `v9.2.17` 官方归档和清单，win-x64 SHA-256 实测 `cb48cae2c61971c4dd717675e5bfa6510fe3699206a05f68b8d01708f1353a0f`，匹配官方清单后解压到本批 `verified-server/`。通过包支持的运行时环境变量指定该二进制、新临时 token 和独立端口 26236，未复制源凭据、修改 PackageCache 或全局配置。
- Ruling: 当前注册工具没有 `ping`，正常调用得到 Tool not found；改用现有 `script-execute` 返回 `pong | G:/Unity/Projects/SnakeUnbound/Assets`，与 Editor 状态查询和两套测试共同证明真实连接，不为单个探针改变持久工具配置。误判成本为探针入口不同，不减少工程路径或执行能力验证。
- Ruling: Unity 在 Windows 将 Company Name 中的点号规范化为数据目录下划线；正式 Company 值不变，允许的实际隔离目录为 `C:\Users\Kano\AppData\LocalLow\TANGENT LINE_SnakeUnboundValidation_20261026T105031Z-fe32d7df\解限蛇`。后续精确断言使用此实测路径，不能按最初 raw Company 拼接路径误判。误判成本是隔离目录断言阻止构建，不接触正式目录。
- 启动故障记录：首次 PowerShell GUI 调用不等待 Unity，`$LASTEXITCODE` 未定义，finally 已恢复正式身份；正常关闭超时后仅结束本批持有的 PID 30156，没有结束源 Editor。守护改为持有 Process 并 WaitForExit，第二次正常退出已实际验证 finally 恢复，随后重新隔离启动继续。
- 权限操作例外如实记录：首次启动命令误带非必要的进程级 `ExecutionPolicy Bypass`，没有修改持久策略（MachinePolicy/UserPolicy/LocalMachine Undefined，CurrentUser RemoteSigned）；后续启动已去除该参数，不能将本批描述成从未使用进程策略覆盖。未修改任何 AGENTS、Kilo 权限或全局 Git 配置。

### 任务 3：完成（2026-10-26）

- 公开 `ProjectBuild.Build(MainModule, StandaloneWindows64, false)` 已调用；`bundle-safety.json` 先断言新工程输出/临时/StreamingAssets 路径与隔离 DataPath，三个输出均不存在。后台 Editor 未自动分发两个 delayCall，普通重绘无效；仅取出并移除这次公开流程的两个已知回调再按原顺序执行，含官方后处理，不重复排队，不调用私有 BuildDelay，不改包源码。`bundle-manifest.json` 实际保存 224 文件（109 个 .unity3d）、19,260,465 字节的哈希及与新 StreamingAssets 的逐文件相等性；与旧源输出的 448 文件是不同清单。
- 临时测试程序集 `Assets/__SnakeSplitValidation` 只在 UNITY_INCLUDE_TESTS 构建启用；正式构建前删除。它记录运行时身份、数据与 Bundle 路径、图形/Mono/voices、build GUID 和实际游戏 AssemblyRef 递归闭包，不修改游戏或原测试断言。
- 首次 Player 编译揭示临时探针误用了不存在的 `XFABManager.Runtime` 名称，修正为真实 `XFABManager`；失败回调恢复正式 Company。随后真实 Standalone Player 构建成功。各次输出目录独立，以 retry 后缀区分，未覆盖未知输出。
- Ruling: 原临时探针把 Windows Standalone 的 Application.identifier 当成配置值比较，属错误平台契约；当前实际返回空，2022.3 官方文档只描述 Apple/Android/UWP（https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-identifier.html）。保留构建前 PlayerSettings 原标识符严格断言，并记录真实运行时空返回，以运行时 build GUID 对比 BuildReport 保证来源，不把配置改为空。误判成本是错误测试误挡有效构建，原游戏测试断言未降低。
- 更正前述临时推断：Player.log 位于带点号 Company 的本批目录，而 Application.persistentDataPath 在 Editor 和 Player 中均规范化为下划线目录。只访问这两个含 RunId 的本批目录，不访问正式玩家目录。
- `validation-player-attempt4-runtime.json` 已实测：Mono=true、Direct3D11、batchMode=false、64 real voices、隔离 persistentDataPath、retry3 输出内嵌 XFAB DataPath；69 个游戏二进制递归节点无 YL 或 UnityEditor。测试结果回传仍超时，尚不能视作 Player 测试通过。
- 实际 Player Guid `2260599774` 与源 Editor ConnectedPlayers 的 signed playerId `-2034367522` 完全对应；目标 Editor ConnectedPlayers 为空。两个工程复制了同一内部 productGUID，当前假设该冲突导致原生 PlayerConnection 错连，尚待实验验证。
- Ruling（待验证）: 仅新独立工程重新生成 Unity 内部 productGUID 为 `0b21664f61ef41679b1832eabcaeb17c`，并重启本批拥有的目标 Editor，排除复制项目身份冲突；Company、产品名、原 applicationIdentifier 及所有资源 .meta GUID 保持契约。它不参与按 Company/Product 的玩家存储路径；若无法解决错连则重新评估并撤销无关变化。误判成本是一次可逆的内部项目标识变更，不是数据迁移或产品 API 改名。
- 源 Editor PID 27968 未关闭、源游戏目录未清理；只正常关闭本批 Player PID 35768。目标 GUI 关闭因残留测试状态超时后，路径守卫内调用 EditorApplication.Exit(0)，生命周期 finally 正常完成；没有对用户源 Editor 强制退出。
- productGUID 实验结果：新 GUID 确实改变了 boot.config 的 EditorId，但原生回传仍超时，未解决问题；现已撤回为快照的 `0b168d788b74cc646a5802404ef293c9`。不将这一失败假设保留为交付配置。
- 用户提示端口后再次双端实测：MCP 25408 -> CGJ2024 / PID 27968；MCP 26236 -> SnakeUnbound / PID 34876；调用目标未混淆。TestRunner 的 PlayerConnection 为独立原生通道，后续仅显式直连本批 Player，不改 MCP 端口、权限或防火墙，不关闭源 Editor。
- Ruling: 多 Editor 的 UTF 原生自动回传持续不可靠，直连可见 Player 但无法重放已出队消息；采用 UTF 1.3.9 已有公开 `ITestRunCallback` / `TestRunCallbackAttribute` 在同一次真实 Player 内直接保存完整 NUnit XML、汇总与 DSP，并用 build GUID 对比 BuildReport。它只改变证据传输，不改变运行器、选择或任何原测试断言；SDK 回传超时单独保留，不伪装成修复。误判成本由 XML 完整测试清单、GUID 和内嵌哈希三项校验限制。
- 隔离验收实际载体为 `Build/Validation-20261026T105031Z-fe32d7df-retry6/PlayerWithTests/PlayerWithTests.exe`。NUnit **33/33**、失败/跳过/不确定均 0，7.5096066 秒；逐项等于 Editor 39 项去掉原有 6 项 Editor-only View 测试。DSP 3.136 -> 10.666666666666666；运行时 Mono、Direct3D11、非 batchMode、64 real voices。未添加新游戏用例或降低 PCM/Listener 断言。
- 本次运行、结构化结果与 BuildReport 均为 GUID `025d2e36d5b24939bb41eb30717f4102`。`isolated-player-verification.json` 验证全部 224 内嵌文件 SHA-256 与新 Bundle 清单一致，69 节点游戏二进制闭包及实际 Managed 无 YL、UnityEditor 或 Assembly-CSharp 遗留游戏依赖。真实结果归档为 `validated-player-runtime-results.xml/json`、`validated-player-runtime.json`、`validated-player.log`。
- `player-finally-identity.json` 实读正式 Company/Product/应用标识与原 productGUID 全部恢复。临时 `Assets/__SnakeSplitValidation` 及 meta 已由 Unity 删除，退出本批 Editor 清空临时会话，再以正式身份打开。正式构建先枚举项目/包构建钩子，未发现 XF/XFAB 清缓存钩子，不调用 XFAB Clear/Build。
- 正式交付：`G:\Unity\Projects\SnakeUnbound\Build\SnakeUnbound-Windows64-20261026T105031Z-fe32d7df\SnakeUnbound.exe`；Build Succeeded，0 errors / 0 warnings，117,058,216 字节，Mono，BuildOptions.None，GUID `1b31f808094243ef8d15039c0b0295bb`。app.info 为 `TANGENT LINE / 解限蛇`，配置应用标识不变；全部 224 内嵌文件仍与已运行 Bundle 相等，无临时探针/游戏测试/YL/旧游戏 DLL，见 `formal-player-verification.json` 与 `formal-player-manifest.json`。
- 正式产物未启动；未读取/清理正式玩家偏好或缓存，没有人工试玩/主观听感结果，其他平台与正式数据空间兼容性运行未验证。此门槛现在满足，才进入任务 4。

### 任务 4：完成（2026-10-26）

- 清理前重新逐项验证 522 个冻结输入（482 个游戏子树文件）、三个游戏输出子树及 58 个保护用户文件，全部一致；源 Editor PID 27968、Main 唯一打开且不脏、无 Prefab Stage，见 `source-before-host-conversion.json`。执行删除的同一个调用再次核对输入/文件集合/输出 SHA-256，不依赖较早快照直接删除。
- 新增 `DevelopmentHostBoundaryTests.cs` 使用原 `YLFramework.Tests.EditMode`。正确选择的四项均得到实际断言 RED：旧产品名、开发 SceneAsset 不存在、游戏 Splash 引用及无正式绑定宿主；初次 API 编译问题和空筛选不计 RED，之后修正为该版本公共 Additive 场景 API 与明确 RQL 选择。
- 以 Unity API 创建并仅保存 `Assets/Scenes/YLFrameworkDev.unity`，实例化正式 `YLFramework.prefab`，新增一个启用的 Main Camera/AudioListener，保留 Prefab 内 inactive UICamera。关闭已保存的 Main 场景后才删除资产；没有新业务启动器。
- 源身份已改为 `TANGENT LINE / YLFrameworkDev / com.tangentline.ylframeworkdev`，唯一构建场景为开发场景；移除游戏 Splash Logo，并清空 `m_VirtualRealitySplashScreen` 的游戏引用。未重置渲染、输入、层、音频、包、宏或可选集成。
- Unity 删除四个已批准资产子树：MainModule、Main.unity、游戏 StreamingAssets 与 MainModule 自动图集；仅删除已核对的 `AssetBundles/MainModule` 生成输出，空 Games 父目录正常删除。共享 XF Resources、资源配置、用户夹具与旧 Build/ZIP 归档保留，见 `source-host-conversion.json`。
- 实际重新编译后新边界 **4/4 GREEN**，失败/跳过 0；验证真实 SceneAsset/完整闭包含正式 Prefab、唯一已绑定根/活动 Camera、无 Missing Script、无游戏品牌引用、Editor/Player 图无游戏根及 YL 无游戏 AssemblyRef，见 `source-boundary-green.results.json`。实际宿主 Play/退出和其余回归/导出仍待完成，不能提前视为任务 4 通过。
- 已启动包含独立性、Core 默认资源、Audio Acceptance、工作台/发布与正式导出入口的合并 EditMode 回归，当前源 Editor 显示真实 Build AssetBundles 原生构建忙，MCP 主线程请求超时。启动该批的 CLI/后台监控载体已不可用，暂无最终结果文件；不重复启动相同批次，待 Editor 空闲后核实 TestRun 状态/实际结果。未把超时或既有 4 项 GREEN 充当完整回归通过。
- 接收工程的 MCP NuGet 依赖目前是正常生成的 45 个 DLL 与 meta；交付收口前需按包的正常 bootstrap 定义检查生成缓存是否应排除、以及 readiness 宏的干净检出语义，不直接复制或提交源 Plugins 充数，也不遗漏适用许可。
- 原生构建结束后确认实际 TestRun 已不活动，从原 Collector/RQL 恢复了完整初次回归结果（载体丢失不重跑正在运行的批次）：**151 项，148 通过、3 失败、0 跳过**。两项为正式导出 Analyzer 收据缺失，一项为 VS 项目自动生成对 `Assembly-CSharp.Player.csproj` 的 Sharing violation；完整原始结果在 `source-regression-editmode.initial.json`。Collector 跨域后的起始时间为 DateTime.MinValue，故不采用其异常总时长作为有效计时。
- 通过现有 `Tools/YLFramework.Analyzers/build.ps1 -Deploy`，SDK 9.0.312 / PowerShell 7.6.9，Analyzer 单元测试 **181/181**，构建 0 警告/错误，正常生成 build-info 收据；没有手工伪造门禁、编辑 csproj 或包缓存。正式导出与 UX4 全组复验 **17/17**（含全部三项原失败），见 `source-regression-retest.recovered.json`；其他初次已通过项没有因本次前置修复失效。
- Ruling: 正式导出原有门禁要求已部署 DLL 等于当前受控源码的正常构建。源原 DLL SHA-256 为 `b61fd9599f11279bf5c8735f99a34325fae6880276df658eeeb11eabdda50a38`，正常源码构建产物为 `4c621b4eea949b5db5c192bad6373723e4c944a421170eac88dedc48c8e37db3`；纳入这一必要生成产物更新，而不是恢复不匹配 DLL 或降低断言。Analyzer 源码、版本 1.0.0、GUID、平台和 RoslynAnalyzer 元数据均未改。误判成本是额外一个可追溯的生成 DLL 变更，由 181 个工具测试及正式包逐载荷校验约束。
- 默认 UI 生命周期、XF 共存与音频真实播放 PlayMode **58/58**、0 失败/跳过；结果 `source-regression-playmode.results.json`。另在真实 `YLFrameworkDev.unity` 进入 Play，再以 finally 退出：1 个 DontDestroyOnLoad 根、预绑定 Canvas 保留、Main Camera 正常、0 Missing Script、无游戏程序集；根仅有 UISystem 子树，没有请求业务启动，见 `source-host-play.json`。
- 最终 `source-final-invariants.json` 再验 58 个保护文件、34 个保留输入（共享 XF Resources、manifest/lock、渲染/输入/层/时间/音频配置等）均逐字节不变；唯一开发场景已退出 Play 且无脏状态，正式宿主身份正确。无用户夹具、旧归档或其他输出清理。
- 增量同步现有文档后，唯一正式 API 生成 `C:\Users\Kano\AppData\Local\Temp\kilo\snake-unbound-split-20261026T105031Z-fe32d7df\YLFramework-1.0.0-snake-split-20261026.unitypackage`。大小 4,551,884 字节、SHA-256 `726cb5aeec241109706c0d1b781f9b72d0355b8cc2010f094b836fa061b50501`；1038/1038 递归记录的 GUID、meta、载荷逐项匹配，固定六目录/三根文件与版本不变，不含游戏、开发场景、Tests、可选集成或 NuGet 宿主依赖；未通过菜单保存其他资产。详见 `framework-package-verification.json` 与完整包清单。

### 任务 5：执行中（2026-10-26）

- 已增量更新 `Assets/YLFramework/Documentation/README.md` 与 `IntegrationPlan.md`：只澄清源码宿主、新游戏位置、显式业务启动和未变分发机制；迁出前的历史条目仍保留并明确标为历史，未新建 README/AGENTS。
- `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/CodeStyle/Format-YL.ps1 -Path Assets/YLFramework/Tests/EditMode/Editor/Distribution/DevelopmentHostBoundaryTests.cs -Mode Check` 已通过（Files=1，SDK 9.0.312）；没有将 YL 格式施加到游戏代码。
- Ruling: 新游戏不提交正常下载的 MCP NuGet DLL 缓存及其专属父目录 meta，也不提交 `UNITY_MCP_READY/UNITY_MCP_DEPS_17` 的已就绪状态；否则干净检出缺 DLL 却先打开编译门会进入失败状态。仅新 `.gitignore` 排除这些自动生成路径，原 game asset GUID 不受影响。以目标关闭且独占锁未占用为前置，将本批生成缓存逐 SHA-256 备份后移走，清空 READY 宏，以正常交互 Editor 首次导入重验：45 DLL 由包正常恢复且逐个哈希相同，53 个注册包与源实际版本/来源相同，519 个保留迁入输入全部一致、实际编译无原仓/YL 依赖；见 `mcp-clean-bootstrap-verification.json`。正常退出本批 PID9904 后再次清空交付宏，缓存仍可在本机正常使用而不入库。误判成本受这一真实无缓存 bootstrap 验证约束；本批未验证 CI，包在 CI 环境跳过恢复，CI 须先完成正常依赖准备，不能承诺纯空缓存 CI 自动恢复。
- 许可收口只作用于本批正式 Player 输出：旁置未改动的游戏 MIT、Unity 引擎第三方声明、24 个实际 UPM 包许可及 40 个确切已打包 NuGet 的 nuspec/版权/许可正文与版本清单，共 171 个声明文件；MIT 表达式使用各包原版权，旧 System.Buffers 4.5.1 等直接保留 nupkg 中 LICENSE.TXT。UCL v1.4/通用 MIT 正文从官方链接取得并记录来源，不把元数据当全文。`player-notices-verification.json` 再验所有原正式二进制和 Bundle 哈希完全不变，含声明总大小 119,547,247 字节，清单为 `formal-player-manifest-with-notices.json`。不向新仓提交这些生成 DLL，不改变原根版权声明或游戏资源原声明。
- Source `EditorBuildSettings` 经 Unity API 保存时删除了原有 `m_UseUCBPForAssetBundles: 0` 未识别字段；收口时保留该原值，避免把无关序列化字段清理夹带进迁移。唯一场景和开发身份的实际 Unity 验证证据仍有效。
- 一次独立只读双仓 Review 发现唯一 Important：Overlap manifest 仍是默认分支 URL（lock 已是正确 hash）；其余复制/294 个 GUID 配对与唯一性/源保留/测试证据/资产闭包/暂存范围未发现其他 Critical、Important 或 Minor。已用四根精确 pin 检查复现 RED（仅 Overlap），只给新 manifest 加 `#4961d8358119485c94d6f0f74b6ca5d8b4163460`，让 UPM 正常生成对应 lock version；原 hash 不变。随后实际 PackageInfo 的四根 packageId、来源和解析目录均精确匹配，GREEN；53 个包版本/来源、45 DLL 哈希、519 个迁入输入和实际编译闭包复验均不变。见 `git-pins-red/green.json`、`post-pin-package-and-input-verification.json`。没有追加审查轮次、弱化断言、重新打已验证 Bundle 或手写 lock。
- Final Ruling（Review 的 Declined to judge）: 正式玩家数据空间兼容性、人工试玩/听感、空缓存 CI、其他平台及 IL2CPP 都属于本批未授权或未验证范围，保留为明确限制，不把 Windows 隔离证据外推。误判成本是这些使用场景尚无运行保证，不能以本批通过取代将来专项验收。
- 源 staged diff --check 通过；新仓全量 check 报原封迁入游戏 C#/Unity YAML/meta 和正常生成设置的尾空白，完整工作树输入已逐 SHA 匹配冻结快照。为保持游戏原状不修剪这些既有空白；新写属性/忽略/manifest/lock/LICENSE 的 scoped check 通过，高置信 token/私钥扫描无命中。新仓 538 路径、源 498 路径白名单审计均无额外项；源旧计划与 58 个保护用户文件不在暂存区，源 manifest/lock 与共享资源未改。
- 新游戏唯一初始提交已成功：`adcf72a0849dfa6afff7f17ba0fff13727850572`，`main`，`feat(game): 建立解限蛇独立工程`，538 文件；提交后工作区干净，Git LFS fsck 全通过，无 remote。
- 源技术收口已完成，下一动作是将本计划与已审查的唯一白名单索引创建一次源本地提交。源提交及其后验回执不能预填成已执行；上方对应提交步骤保留待 Git 历史验证，实际 Hash/剩余状态在最终回复交付，不为回填自引用 Hash 再制造 docs 提交。所有构建、运行和测试证据均在此前完成，不因最终提交重新运行。
