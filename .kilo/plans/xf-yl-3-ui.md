# XF × YL 阶段 3：UI 系统深度融合研究与总体方案

**状态：设计已批准，待分批实施；阶段 3 尚未实施。批准不等于授权 3.1 或后续批次实施，仍等待 Git 收口与 3.1 单独实施授权。**

本文是阶段 3 的唯一总体方案，覆盖 **05 Panel/UI** 与 **12 UI Adaptation**，两项均以 XF 为主要架构基础、客观融合双方。本轮只编写本文，没有修改 Runtime、Editor、测试、资产、asmdef 或配置，没有运行 Unity 刷新、编译、测试、PlayMode 或 Player 构建，没有 Git 写入。方案批准、批次实施授权与 Git 授权分别处理；不得因阅读本文自动开始实施。

**已确认的补充范围（2026-10-11）：**用户接受 YL 自有资产定向迁移，要求 Tips、ColorSelector 真正进入统一 UI，而不是保留两套架构做适配；ColorSelector 在阶段 3 修复和优化，成为框架正式 Runtime UI 工具，Samples 只保留演示。没有融合后职责的旧 UI Root 可以退出，不以旧类型/模板形态作为永久约束。用户进一步选择：保留 HSV 色环/三角、初始颜色、当前颜色读取和实时变化回调，本阶段不新增 Alpha 编辑、数值/Hex 输入、确认取消流程或 Editor 专用取色器。这些确认不等于实现或 Git 写入授权；总体设计方向、功能边界与四批路线已于 2026-10-11 经用户审阅批准，批准范围与实施授权边界见第 9 节批准记录。

## 1. 基线、范围与继承契约

- 2026-10-11 实际核对：`integration/xf-yl`，HEAD `d7e31a6`，开始时工作区干净。阶段 1 与阶段 2 的 Pool、Events、Update、Timer、SerializedDictionary 已由用户确认 COMPLETE，本轮不重新审查或验收这些系统。
- 已读取根 `AGENTS.md`、[长期蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)、[1.3 View 最终契约](xf-yl-1.3-view.md#14-13-e-实施与阶段技术收口)及阶段 2 的 UI 直接协作结论。长期蓝图和部分专项首页仍有待验收/待 Git 收口的历史文字，本轮以用户确认及实际提交为准，不修改其他文档。
- Unity `2022.3.62f3c1`；原 XFGameFramework `1.18.8` / revision `2182708aec`，XFABManager `1.24.0` / revision `c35dc4058e`。本地存在 UGUI `1.0.0`、Input System `1.14.0`；后者在 lock 中为间接依赖，不能因 manifest 没有直接声明而认定未安装。`activeInputHandler=2`，即 Both。
- manifest/lock 的 UGUI `2.0.0` 与实际本地 `1.0.0` 等恢复差异沿用长期蓝图的已知限制；本轮不解析、升级或修补依赖。源码结论不等于当前 Editor 的实时导入/运行状态。
- 原 XF/XFAB Package 严格只读；`Assets/Games`、游戏场景、原 `XFGameFramework/*` Resources、MainModule 映射和既有 Bundle 不迁移、不重建。本阶段不重构完整 Resource、Audio、Save、Action、AsyncOperation 或其他独立系统。

**不能推翻的基础：**

| 已确认能力 | UI 必须遵守的边界 |
|---|---|
| Module/MVC | Module 是唯一业务 owner；查询默认本地；关闭仍为 OnClose → Controller → View → 剩余 Model，逐对象继续清理后报告。UI 不建立隐式全局 owner 或第二条 Module 关闭管线 |
| View/Panel | 保留 `Panel : View`。初始化、参数、注册、退出、场景纳管、pending/代次及稳定终态沿 1.3；Close 不隐式移除 Model。DDOL 只改变物理寿命，不转移业务 owner |
| 启停与复用 | Loaded 且 active 才开放框架事件；Disable 不等于 Close；健康实例可复用，污染实例隔离。框架不重置任意业务字段，原始 Awake/OnEnable 仍可能早于加载上下文 |
| Events/Update | 使用已验收的精确 token、Session/eventEpoch 与公共三相位；必要 UI 状态先提交，再通知。普通退订仍可能留在当前快照，UI 自己保护旧轮次；不清全局 bus/update |
| Pool/Timer | 普通缓存共用 Foundation 内核；实例/资源所有权不由普通池推导。UI 自有短期任务显式取消或 Dispose 自有 Scope，不能用 Module.Close 清全局 Timer/Action |
| 资源 | Core 继续使用临时 XFAB；Close、缓存归还、Destroy、Prefab 登记释放、Bundle 物理卸载是不同动作。保留逻辑 Abort/失效，不承诺共享生产者物理取消 |

源码路径简称：`C` = `Assets/YLFramework/Runtime/XFMain/`，`Y` = `Assets/YLFramework/Runtime/`，`E` = `Assets/YLFramework/Editor/`，`T` = `Assets/YLFramework/Tests/`，`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`，`AB` = 对应 XFAB PackageCache。以下行号对应 `d7e31a6`，后续以符号为准。

## 2. 双方真实架构与能力

### 2.1 三条实际接入路径

**原 XF / 原游戏：**游戏 `Main` → XFAB ReadyRes → 原 XF Module/FSM → 原 `Module.LoadPanel` → XFAB 实例池。原 PanelManager 管 Canvas、排序和全屏遮挡，原 ViewManager 管表现加载；原游戏继续走这条链，不接新 YL UI。

**已承接 Core：**`Module.LoadPanel[Async]` → PanelManager 解析 UI 父节点 → 唯一 View 加载事务 → Panel.BeforeOnLoaded 准备 Canvas/order → OnLoaded → Loaded+active 接入 UI 显示策略。退出由 ViewManager **直接** ReleaseView，异步 Panel 请求只是 View 请求终态的转接，不另有 producer/pending。

**旧 YL UI：**UIManager MonoSingleton → 全局 key 目录/UIWindowData → UISystemConfig/IWindowLoader → YL ResLoader 取得 Prefab → Instantiate UIWindowBase → OnInit/OnOpen/独立 UIEnable。缓存关闭隐藏同一个窗口并保留 Loader；非缓存关闭销毁，由窗口 OnDestroy 释放 Loader。UIRoot 与 UIManager 通过层级路径找/创建宿主，不会自动加载 `YLFramework.prefab`。

| 能力 | 原 XF / 已承接 Core | 旧 YL UI | 融合判断 |
|---|---|---|---|
| 表现与业务归属 | View 可用于世界对象；Panel 是 UI 特化，Module 支持同类型多实例 | 只管窗口；全局 key 单实例，无 Module owner | 唯一表现协议保留 Core；窗口唯一性是可选策略，不取代多实例能力 |
| 显示/关闭 | 1.3 已完成同步/异步初始化、Close、Disable、显式 Attach、销毁/卸载及复用会话 | OnInit 一次、OnOpen/OnClose、独立注册 UI 事件、UIEnable | 有价值的窗口便利钩子可演进；不保留第二套状态机/退出协议 |
| 分类与层级 | `UIType` 为场景 UI/DDOL；`PanelType` 为 Panel/Window/Overlay；order、Module 偏移、AlwaysInFront | 五层 root、按 sibling 置顶、独立 DragLayer | 根寿命、窗口用途、逻辑层是不同轴，不压成一个 enum；吸收可配置层 |
| 遮挡/导航 | 不透明全屏 Panel 队列，关闭后恢复之前有效 Panel；不是页面路由器 | 层级计数控制 mask；没有完整返回栈/路由 | 统一显示集合、遮挡、遮罩与前一有效窗口恢复；不新建页面路由/历史回放系统 |
| 遮罩/输入 | 每个自有 Canvas 配 GraphicRaycaster；默认 EventSystem+StandaloneInputModule；无模态策略 | 每层 Image mask、坐标 RaycastAll、可选 Input System 反射桥 | 遮罩、指针拦截、键盘焦点分别设计；借用已有 EventSystem，不重复建输入系统 |
| 根节点 | 按 UIType 惰性创建 Canvas，DDOL 根独立；普通 View 不创建 UI 根 | 手工宿主模板、Camera/Canvas/Scaler/Raycaster、Tips/Drag 根 | 配置模板与运行时策略互补；根组件不当业务 owner |
| 实例缓存 | XFAB 按 Prefab 复用，不承诺某 key 永久占有同一实例或跨场景保活 | key 独占同一窗口；IsCache 决定隐藏/销毁 | 保留可复用/不缓存策略，取消两套空闲窗口缓存；关闭结束会话，不保留旧 owner/监听 |
| 资源入口 | 项目+资源名 fallback、直接 Prefab、调用方实例 Attach；异步稳定 Status/Exception/Abort | BundleName/AssetName、自定义 loader pool、ResLoader 资产引用 | 保留接入扩展价值，不将两种资产所有权混接；无需重做 Resource |
| 适配 | 自动 Scaler 匹配、Background cover、Border 宽屏偏移 | 设置参考分辨率/match、Overlay/Camera 模式、默认窗口强制拉伸 | 统一配置权威与刷新；各类布局组件保持独立职责 |
| 短提示 | 原游戏 TipPanel 属业务 Panel，不是通用框架 Toast | 五位置、文本/背景、淡入淡出意图、私有普通池 | 保留通用短提示价值；不是第二个窗口管理器，也不以游戏 TipPanel 替代它 |
| Editor | 自有 Core 只有 Module 创建支撑，无完整 UI 配置/Panel Inspector | 旧 UI 无专用 Inspector/自动注册工具；模板被导出 | 配套最小配置校验/迁移与用法；不借机展开阶段 6 |

**重要差异：**原 XF 的 Panel 登记在加载后/异步 wrapper 后置完成、依赖移除广播；Core 已把它收进 1.3 事务，并修正 inactive 登记、Canvas 重试/首次激活、Session 重入和直接退出。阶段 3 不重复修这些问题。Core 的三个 Adaptation 组件目前仍与原 XF 同算法，尚未完成能力融合。

主要依据：`C/mvc/Panel.cs:25–222`，`C/Manager/PanelManager.cs:254–345,348–710`，`C/Manager/ViewManager.cs:159–246,311–545`；原 XF 对应 PanelManager `207–222,450–456`、LoadPanelRequest `63–78`；`Y/UI/UIManager.cs:20–104,117–353,469–509`、UIWindowBase `26–78`、UISystemConfig `23–54,89–134`、UIRoot `7–43`。

## 3. 真实消费者、资产与兼容

### 3.1 YL 自有接入，不是纯占位

| 资产/消费者 | 已确认身份与内容 | 后续兼容要求 |
|---|---|---|
| `Assets/YLFramework/Prefabs/YLFramework.prefab` | GUID `5cdd75c99ad87f642a43d6e65b7b8635`；挂 YLFrameworkRoot、UIRoot、UIManager、Canvas/Scaler/Raycaster/EventSystem；五层、遮罩、Drag/Tips 根 | 是真实导出模板。把有效 UI 布局收进统一根，存续资产保留身份/有效引用；无新职责的旧 UI 子树/组件可退出，不能为了保留整份模板形态制造第二套根 |
| `Assets/YLFramework/Prefabs/UITips.prefab` | GUID `e71ff2cbb45a105459b3731fe3c7cb2f`，被上述模板 `uiTipItemPrefab` 引用；UITipsItem 有 `bg`、`text` | 有真实引用，不能宣称“零引用”；保留视觉资源和字段，验证显示、恢复、池销毁及迟到回收 |
| `Samples/ColorSelector/Prefabs/UIColorSelectorWindow.prefab` | GUID `29e4e189755c172469818bfbd3d8a3a4`；派生脚本 GUID `18a39294c5bc61646be12faf6a17fe94`；handleImage/outerCursor/innerCursor/CloseButton | 示例 C# 确实 Open/Close。正式脚本、Prefab/Material/Shader 收入 Runtime UI 工具资产范围，演示独立；迁移保留适用 meta/GUID 与有效绑定，修复交互，不保留旧窗口基类来桥接 |
| `Y/UI/UIManager.cs` / UIRoot / UIWindowBase | 脚本 GUID 分别 `ef050e654d014a7cb539e809f36326a2` / `eaa2ff45b0824bb0a88fbda69f891959` / `5117b7f5560b4af8a25c8d427cbae882`；旧类型在 `YLFramework.Runtime` | 保留有效职责而非预定保留旧类型。可演进为统一 UI 正式类型或迁完退出；不保留无用途转接壳。改变身份须迁资产/调用方并验证，退出须处理 meta/引用 |
| 模板序列化字段 | UIManager 的 `uiLayers(root/enableMask/maskImage)`、dragLayer、uiTipItemPrefab、五个 TipRoot、uiTipItemPool；UIRoot 的 uiCamera/canvas/canvasScaler/graphicRaycaster | 保留仍有意义的布局绑定；字段类型变更需定向转换，FormerlySerializedAs 只解决适用的字段改名 |
| 输入/导出/测试 | YLFrameworkRoot.Awake 调输入桥；ExportUnityPackage 导出 Prefabs/Runtime/Samples；ApiHelpTests 检查 CurrentLayer protected setter | 不重构 Root/导出器整体；仅适配 UI 副作用与依赖。该测试是签名格式器示例，不是必须永久锁定窗口架构的兼容契约 |

本轮 Assets 文本 GUID 搜索未找到 YL Root 和 ColorSelector Prefab 的场景摆放/其他资产引用；Tips 有上述模板引用。没有摆放实例不等于没有分发消费者或通用价值。外部历史工程、二进制调用和仓库外资产未知，不为它们预建全量兼容层；若实施时取得真实样本，补充定向迁移。

1.3 的 C1 约束保护了该阶段的旧 YL UI 资产，不应被解释为阶段 3 必须永久保留两套管理器。**自有迁移的方案边界已由用户确认；存续内容保护身份与有效数据，退出内容完成引用迁移，不以保留旧 UI 名字/层级为目标。实际迁移仍等待批次实施授权。**

**两个 Root 不可混为一谈：**`Y/UI/UIRoot.cs` 只有旧 UIManager 消费其 Canvas/Camera 配置，职责合入统一根后可退出。`Y/YLFrameworkRoot.cs` 则还被 AudioManager (`Audio/Internal/AudioManager.cs:31`)、ActionGlobalHost (`Action/Internal/ActionGlobalHost.cs:15`) 和 ActionRecycleDriver (`Action/Internal/ActionRecycleDriver.cs:10`) 用作非 UI 宿主父级，已有 Audio/Action 测试钉住该归属；它不是 UI 专用 Root，保留其非 UI 职责，只移除不再合理的自动输入创建副作用，不借阶段 3 重构这些系统。旧混合 Prefab 是否精简/退出，与公共 Root 类型是否仍有用分别判断。

### 3.2 Core 资源与原游戏隔离

- Core 实际 Resources key 是 `YLFramework/Core/Canvas` 与 `YLFramework/Core/EventSystem`。资产 GUID 分别为 `0df4ee5a6aa484a40b5d537a1c23a2c0`、`e5f419ca4a6e564439be23e0257ffb3c`；Canvas 绑定自有适配脚本 `36162b36843cfe2498fbdd4ff95ac2b4`。
- 原游戏使用 `Assets/Resources/XFGameFramework/*`；Canvas 适配脚本 GUID 为原 XF `abf116dc13c27ef4583812631aacc1c3`。原路径、模板、组件和 GUID 不修改。
- 游戏九个 Panel 均派生原 `XFGameFramework.Panel`：Start、Setting、Pause、Tip、Loading、GameOver、GameSetting、Description、GamePanel。入口见 `Main.cs`、Game FSM、TipController、SceneController；MainModule 的 `panels` 映射仍为 `Assets/Games/MainModule/UI`。
- 游戏 Button/Slider 持久事件确实存在：业务 OnBtnQuit/Close/AudioChange/OnBtnPauseClicked 等，加现有 `YLFramework.Services.Audio.AudioEmitter.Play`。部分业务事件的 `m_TargetAssemblyTypeName` 仍写 Assembly-CSharp，而真实脚本在 Project.Game.MainModule；这是既有元数据风险，不是本轮已复现故障。1.4 已验证真实按钮与原游戏 Windows64/Mono 流程，本阶段不修这些元数据。
- 旧 UI 自有脚本 GUID 未在游戏资产中命中，但游戏使用同一个 YL Runtime 音频/Root 支撑，因此 Runtime 依赖或输入副作用变化仍须非干扰验证，不能用“游戏不调用 UIManager”豁免。

依据：Root Prefab `820–846,1035–1050`，ColorSelector 脚本 `10–41` 与 Prefab `215–221`，`E/ExportUnityPackage.cs:16–23`；游戏 StartPanel Prefab `200–226`、MainModule.asset `17–87`；长期蓝图第 7 节历史验收。

## 4. 已确认问题与未验证风险

以下“源码确认”不等于本轮运行复现；原包不随这些修复改变。

| 问题/分类 | 证据 | 阶段 3 处理方向 |
|---|---|---|
| Core Top 与显示 order 不一致，源码确认 | Panel.IsTop `164–201`、GetTopPanel `578–616` 比较 raw SortingOrder；DisplaySortingOrder `63–68` 含 Module 偏移 | 所有 top/遮挡/输入策略用同一有效排序关系；排除失活、退出和旧 Session，不以字典枚举打破平手 |
| Core 单全局遮挡队列与多个根/排序不一致，源码确认 | PanelManager `145–147,652–676` 不区分 UIType/根，最后入队即 current；队列顺序不随 SortingOrder/Module 偏移变化 | 按实际根/显示上下文管理；例如后加载的低 order 场景 Panel 不能隐藏高 order DDOL Panel，Additive 独立根不相互误隐藏 |
| AlwaysInFront 排序持续递增及业务异常连带，源码确认 | `427–483` 每次 AddOrder 对既有前置 Panel 再 max+10；回调异常中断并进入新窗口加载回滚 | 改为稳定相对优先级/必要重排，约束有效 order 范围；结构先提交、逐项通知并报告，旧会话不改新会话 |
| Canvas 所有权判断脆弱，源码确认限制 | Panel.InitCanvas `109–126` 拒绝非框架 Canvas，以父 Canvas 的 transform.parent 是否存在决定借用 | 接入 YL UISystem 的嵌套根时不能令全部窗口共享根 Canvas、失去独立排序；明确窗口边界与子控件借用，不强删用户 Canvas |
| 遮挡不是停用/模态，源码确认；输入后果待运行 | 只关 Canvas 并调用 Invisible；没有完整输入/焦点策略。UGUI GraphicRaycaster `126–132,319–334` 还依赖图形 depth/cull 等原生状态 | 不把 Canvas.enabled=false 当可靠输入协议，也不声称必然穿透；显式管理框架输入覆盖并实际验证指针、Submit/Cancel 与焦点 |
| EventSystem 多入口/会话风险，源码确认 | Core `333–345` 看 current 并创建；Root→InputSystemBridge 查任意含 inactive 实例；Core onCreateCanvas 静态 Action 不由 ResetSession 清理 | 统一创建/借用规则；自有对象和注册按会话释放；不要激活/删除用户或原 XF 的输入对象，也不要旧闭包跨 Play 复活 |
| 旧窗口目录缺省不可用，源码确认 | UIManager `76,121,166` 的自动属性未初始化，仓库内未找到赋值；Attribute 无读取者，Sample 直接 Open | 用明确目录注册与校验替代未完成的隐式自动注册；不是只初始化字典后保留双生命周期 |
| 旧异步失败/pending 不可靠，源码确认 | UISystemConfig `103–128` 失败无回调；UIManager `333–352` 提前加层计数，无同 key pending 身份 | 复用 View 终态与 owner 代次；唯一窗口入口才按 key 合并/拒绝在途请求，不改变底层多实例 LoadPanel |
| 旧计数/退出不一致，源码确认 | Destroy/Remove/Clear `172–198,409–416` 未同步 UILayer._count；OnWindowClose 可负计数；BeforeDestroy `62–76` 依赖 Loader 且业务可覆盖 | 从有效显示/模态记录推导 mask，不把手动计数当权威；必要清理不依赖派生类调用 base |
| Tips 显示/回收未闭环，源码确认 | UITipsItem `33–34` 注释掉 DoShow；UIManager `570–576` 回收依赖该完成；DoShow 非正时间直接 yield break | 恢复必要显示功能并明确 active 项、退出/取消/复用代次；Pool 阶段已修原池捕获，不重做普通池 |
| 适配边界，源码确认 | Background `22–28,38–52` 无 Canvas/零尺寸保护，仅 OnEnable 刷新；Border `35–47` 只 Start、硬编码宽高比、覆盖 Y；AutomaticScaler `20–41` 每帧检查屏幕但缓存未包括参考分辨率 | 分别保留 cover、宽屏边距、Scaler 职责，增加必要校验/尺寸与配置刷新，不把它们全换成同一种布局组件 |
| ColorSelector 初始化与输入坐标错误，源码确认 | 原脚本 `20–29,43–58` 把 world position/屏幕点直接用于局部计算；Init 仅改 selectedColor，未同步 finalColor、HSV、Shader 与游标 | 在统一工具内修复，打开即同步初始颜色；按 wheel RectTransform 与事件 camera 换算屏幕坐标，不把换基类当修复 |
| ColorSelector CPU/视觉几何不一致，源码确认 | CPU 三角 `85–100` 为 `(0,51.7),(-51.7,-51.7),(51.7,-51.7)×scale`；Shader `77–97` 为 UV `(0.5,0.9),(0.85,0.3),(0.15,0.3)`，黑/白顶点方向也不一致；CPU 允许负权重 | 共用一致的规范化几何、顶点色和 Hue 约定；尺寸/pivot 变化与边界钳制后输出/游标/视觉仍一致，不靠最终 Clamp 掩盖取错位置 |
| ColorSelector 材质与 UGUI 支撑缺口，源码确认限制 | 脚本 `110` 直接改 Image.material；Prefab `310` 引用共享 Material；Shader 未实现标准 UGUI stencil/clip 与顶点颜色路径 | 实例隔离并释放自有材质，两个工具互不污染；验证 Mask/RectMask2D、CanvasGroup alpha、Overlay/Camera 与实际项目渲染管线，不将静态缺口写成已复现渲染故障 |

ColorSelector 来源事实：`Samples/ColorSelector/Readme.txt` 标注 CodeArtist.mx / Rodrigo Fernandez Diaz（2015）；Readme.meta 有 `licenseType: Store`，Shader 名也保留来源。仓库内未查得完整许可，不能把这个元数据当再分发授权，或用 XF MIT 覆盖它。正式分发门禁见 5.6/8；本轮没有改变来源说明或素材。

进一步风险：外部销毁/根卸载、共享 Canvas 的不可见控制、原生 OnEnable 早于 owner、重复事件绑定、静态输入缓存更换 EventSystem、Root 初始化触发游戏音频路径、反射 Input System 的裁剪。均纳入对应批次验证，不声称本轮已修复或复现。

## 5. 推荐总体架构

### 5.1 唯一生命周期与唯一 UI 策略

```text
显式 Module owner + 窗口定义/调用参数
  -> 统一 UI 入口：普通多实例加载 / 可选按 key 唯一窗口
  -> 既有 ViewManager + LoadViewRequest 事务
  -> Panel : View（或有价值的 Window 特化）
  -> 唯一 PanelManager UI 策略：实际根、层、排序、遮挡、遮罩、焦点
  -> UI 根/配置组件、布局组件、短提示呈现组件

资源取得/实例处理继续在既有边界；Foundation 提供 Events/Update/Pool/Timer。
```

推荐保留并演进 PanelManager 为唯一 UI 策略实现。**将有效窗口入口、配置、层/遮罩和根职责直接收进统一 UI 正式类型；Tips 与 ColorSelector 是这一体系的正式能力，不是 Services.UI legacy 的客户。**旧 UIManager/UIWindowBase/UIRoot 是否沿用名字由实际职责决定，不为兼容示例保留第二个 Manager、旧基类或无功能转接壳。实现不得再有独立 owner、UIEnable、加载事务、窗口缓存、遮挡队列或第二套输入规则。普通 View、世界对象、取色内容控件与 Tips 呈现项不强制变成窗口。

若 Window 特化确有收益，只承载窗口定义、层/模态选项及有价值的业务钩子，不是旧 UIWindowBase 包装层。实例准备与每会话准备明确分离；启停监听接既有 View 钩子，窗口关闭走不可绕开的 View.Close。ColorSelector 的旧 Close、Attribute、事件注册和取色实现直接迁入新体系，Samples 不再依赖旧 UI。

**所有权：**Module 管活跃表现会话；PanelManager 只保存带 Session 的显示策略记录；根组件拥有其自建 Canvas/遮罩/输入与短提示资源，不拥有借入 Module 或 Model；资源后端或调用方拥有实例/资产处理责任。关闭一个 Module 只撤销它的 Panel/pending/自有任务，不销毁其他 Module 共用的根、EventSystem 或缓存桶。

### 5.2 根、层、排序、遮挡与输入

- 用实际根 Canvas 身份及所在 Scene/会话区分显示上下文，不仅按 UIType 全局保存一个根。场景根跟随准确 Scene，持久根 DDOL；已有有效根明确登记，缺省根仍可惰性创建。跨显示器/WorldSpace 的通用渲染排名不在本阶段建立。
- UIType 表达寿命；PanelType 表达全屏/窗口/Overlay；YL 层配置表达组优先级。保留五层模板能力和合理的 Module 排序偏移，不把 layerNum 直接映射 UIType，也不让任意偏移越过模态/层的约束。
- 同一上下文使用一份稳定排序关系生成 Canvas order，并据此查询 top、恢复前一有效窗口和控制遮挡。计入实际 sorting layer/order、Module 偏移与确定平手规则；AlwaysInFront 是组内优先，不靠无限 max+10 漂移。非法/溢出配置在提交前拒绝，不静默截断。
- 区分显示最前与可导航窗口最前：保留 Overlay 不充当普通窗口 Top 的用途。当前 Panel 自身必要准备或初始化失败仍走 1.3 回滚；既有无关 Panel 的排序观察通知失败不得冒充当前加载失败。通知异常继续并报告，不削弱自身初始化的失败契约。
- 自有窗口 Canvas 可排序/隐藏；子控件借用 Canvas 不独立操作共享根。保留合法预置 Canvas 的接入价值，仅在明确取得控制权时设置；不要求删除所有预置 Canvas，也不扩大普通 View 的 UI 行为。
- 仅同一明确遮挡组内、不透明且完整覆盖的有效 Panel 才做渲染遮挡；Overlay/Window 不冒充全屏。不同根默认不互相改状态。被覆盖不是 Close，不移除 Model/owner，不默认停止所有 MonoBehaviour、事件、Action 或 Timer；业务可在 Visible/Invisible 中管理自己的工作。
- 模态遮罩由有效模态集合派生，放在应拦截的窗口之下、被阻挡内容之上；不依赖名称 Mask 或裸 sibling 算法跨越 overrideSorting。是否点击遮罩关闭由窗口显式选择，默认只拦截。
- 指针拦截、Selectable 交互、EventSystem selected 对象和 Submit/Cancel 分开验证。模态结束只恢复仍有效且未被其他窗口接管的焦点；回调 Close/Disable/换 Session 后，旧状态恢复不得操作新会话。只覆盖自有 UI 输入，不接管游戏全局输入系统。
- 位置查询更换 EventSystem 时重建 PointerEventData，查询/关闭不创建宿主；遮罩命中不能被误解释为游戏世界可穿透。若保留“只查内容”的便利语义，须与“是否阻挡世界输入”明确区分。

### 5.3 窗口目录、复用与资源接点

- 目录是明确的窗口定义，不是第二个活跃实例 owner。采用显式注册/配置，不补一个全程序集 Attribute 扫描器。定义可描述层、PanelType/模态、复用策略和资源定位；key 的唯一性至少限定到 owner/根上下文，不把所有 Module 同名窗口绑定为一个对象。
- 普通 LoadPanel 继续允许多实例。可选唯一窗口入口重复 Open 已完成会话时复用/置顶并更新业务数据，不重复 OnLoaded、事件或显示登记；在途同 key 的策略只在该入口实现，不改变已验收 View 的独立请求语义。每笔接受的参数仍一次接管/释放，等待者取消权限不能误取消他者。
- Close 始终结束表现会话；复用是下一次新会话，不保留旧 key→活跃实例假象。不承诺闭合后某 key 永久占用同一个 GameObject。保留可复用/不缓存的窗口用途；不缓存实例走明确丢弃处理，不把业务无异常与允许缓存混为一谈。
- 首版继续支持现有 XFAB 命名同步/异步、直接 Prefab，以及调用方已取得实例的 Attach。接口提供资源接入价值，不要求所有资源变成 XFAB，也不把 BundleName 当 ProjectName 静默转换。
- **默认退出旧 UISystemConfig 的独立 Instantiate/加载器池/回调状态机。**ColorSelector 正式工具由统一入口显式注册/绑定其 Runtime Prefab，Samples 仅演示，不依赖未完成的旧目录加载。自定义资源使用者可在外部取得并持有实例/资产后显式 Attach；调用方拥有的实例仍只结束会话/隐藏，销毁及 Loader 释放由其真正 owner 成对执行。
- 不把 ResLoader 持有的 Prefab 随手交给 XFAB 实例池再提前释放。若实施时发现必须由统一 UI 异步入口承接真实自定义 loader，只增加满足同一 View 请求/退出协议的窄取得与收尾接点，记录实际实例/资产 owner；不得另造公共 Resource 服务、通用租约框架或第二个 producer/pending。
- Module 关闭、Abort、根/parent 销毁及 Scene 失效继续使 pending 逻辑终结；迟到结果不得登记窗口、加遮罩、占 key 或访问已归还参数。资源后端永久不完成且上下文有效的既有限制仍在，UI 不增加无依据默认超时。

### 5.4 适配、输入后端与短提示的独立职责

- Scaler 的参考分辨率、手动 match、自动宽/高匹配只由同一个根配置策略决定；不能 UIManager.SetResolution 后被自动组件悄悄覆盖。自动匹配保留 XF 现有价值，刷新同时考虑屏幕、Canvas 尺寸和配置变更。
- 全屏拉伸是显式窗口布局选项，不对弹窗/内容窗口无条件写 anchor/offset。Background 保留 cover 语义，Border 保留宽屏边距用途，修正依赖校验、零尺寸、重复启用及重新挂载后的刷新。不要把 Border 宽屏偏移称为 Safe Area。
- **Safe Area 已批准纳入阶段 3：**提供显式启用的可选 Safe Area 容器，保护通用移动 UI 内容；背景仍可铺满屏幕，桌面默认不改变现有布局。实际 Canvas/camera 坐标转换、横竖屏及安全区变化纳入适配专题，不扩展为通用屏幕事件系统或全局屏幕事件平台。
- 统一 EventSystem 借用/自建规则：有效既有 active 系统优先，不替换其已配置模块；只有确需且没有可用系统时创建自有系统，寿命匹配所服务的根。inactive/多 EventSystem 配置需明确诊断或显式选择，不偷偷激活、删除或争抢原 XF 的系统。
- 吸收 InputSystemBridge 的无硬 Package 依赖能力；Legacy/Both/Input System-only 分支应有明确行为。不可用时不要在 New-only 工程无声安装不能工作的 Standalone；反射失败需诊断，Player/裁剪边界不能用 Editor 通过代替。普通 YLFrameworkRoot 因 Audio/其他领域出现时不再无条件制造 UI 输入副作用，限于必要 UI 接入调整。
- Tips 是统一 UI 根/服务拥有的正式短期呈现，使用同一层、遮挡/输入、适配和场景上下文，不挂在旧 UIManager/旧 UIRoot 上。提示项不必继承 Panel，也不建立另一个窗口目录。保留五位置/文本/背景/可选淡入淡出，采用已完成的 ObjectPool 和局部任务控制；推荐 UI 短提示默认 unscaled，暂停游戏时仍可完成。记录原池/显示代次、active 项及自有句柄，正常结束、取消、根销毁、异常均收尾一次；不能只 Dispose 空闲池而遗留 active Tips，也不复活旧 Action 实现。

### 5.5 程序集与命名推荐

UI 策略与 Panel 保持在现有 `YLFramework.Core.Runtime`，正式根配置/输入支撑、Tips 与 ColorSelector 的 Runtime 实现也收进该 UI 域。Foundation 不引用 UI/Root/Resource；Core 不引用整体 Runtime；**不再为了保留旧 UI 入口默认新增整体 Runtime→Core 引用。**这是迁移范围确认后的推荐修订，取代初稿的“在 Runtime 保留旧入口/特化并新增 Core 引用”。

只搬必要 UI 职责，不把 Runtime 的 Singleton、Resource、Events Unity 适配器等整个依赖闭包搬入 Core。当前 ColorSelector 的 OnClick/OnDrag 扩展属于 Runtime；正式控件可直接使用原生 UGUI 接口及已有共享事件 token，不为这两个调用重新审查或迁移阶段 2 Events。输入支撑直接服务新根，公共 YLFrameworkRoot 不再调用旧 UI 桥；普通 Audio/Action 归属维持。

ColorSelector 目前无 asmdef、处于预定义程序集。正式工具 C#/Prefab/材质/Shader 从 Samples 定向归入 Runtime UI/Tools 范围（可在现有 Core 目录内组织，不预建新产品程序集），Samples 只留下演示消费者。Core `autoReferenced=false`，为演示加必要直接引用的 Sample asmdef，不全局打开 autoReferenced；相关测试/Editor 只补真实使用的引用。存续脚本/资产移动保留 meta，命名、namespace/程序集变化核对序列化及 UnityEvent；不强保留 Samples 命名来冒充正式类型。

### 5.6 ColorSelector 正式 Runtime 工具

**已确认功能边界：**保留 HSV 色环与三角，支持初始颜色、读取当前 Color、用户操作实时变化回调；不新增 Alpha 编辑、数值/Hex、确认取消或 Editor 专用工具。这里“工具模块”表示正式可复用 UI 能力，不要求创建新的全局单例、业务 Module/Model/Controller 三件套。窗口入口用调用方显式 Module、统一 Panel/层/模态/加载会话；取色内容只处理自己的状态、几何和指针，不建设通用 Widget 框架。

- **初始化与数据流：**每次打开先同步初始颜色到当前值、HSV、Shader 和两个游标，再开放输入；初始化本身默认不发送用户变化通知。不编辑 Alpha 时保留传入 Alpha，不悄悄置为 1；黑/白/灰的 Hue 不定情形采用确定策略。再次打开/复用替换原订阅和初值，不保留上一调用方闭包；GetColor 与视觉始终表示同一状态。
- **输入与几何：**屏幕点按实际 wheel RectTransform、pivot、尺寸及 PointerEventData 的 camera 转换到统一规范化空间；CPU 取色、游标与 Shader 使用一致的 Hue/三角顶点/黑白方向。退化尺寸明确诊断，不产生 NaN；三角边界正确约束，不用负权重容差误选。拖动保持起始区域（Hue 环或三角）和有效指针身份，不因中途跨区突然换模式；不能被模态/关闭后旧输入继续改色。
- **呈现与资源：**缓存自有组件/Shader property 身份，避免拖动路径反复 GetComponent 和无谓分配；每实例拥有必要材质副本，销毁恢复/释放自有资源，不修改共享 Material。补齐实际需要的 UGUI alpha/mask/clip 行为，测试 Overlay/Camera、缩放/布局变化与两个窗口互不污染。优化依据明确成本，不引入渲染框架、Material 管理器或不必要 LUT。
- **关闭与调用方：**颜色回调可触发 Close、再 Open 或销毁；通知后校验原会话，不写回新会话。关闭不额外提交/回滚颜色，不发送虚构“确认/取消”结果；清理指针/按钮订阅和调用方引用。演示改为使用正式工具、设置初始颜色后读取/监听，并清理自己拥有的预览材质/订阅，不将 Cube 或演示脚本放入工具 Runtime。
- **验收重点：**初始纯色/黑白灰及带 Alpha 的颜色，选择三角顶点/边缘与 Hue 环，非中心窗口、Canvas 缩放、Camera 模式，连续拖动与区域边界，重复打开/双实例/回调重入、关闭与 Module/Scene 退出；当前颜色、Shader、游标及回调一致，复用无旧回调，材质和关键引用无泄漏。用少量几何/状态定向测试加真实工具 Prefab 验证，不预设测试数量。
- **来源与分发门禁：**保留 CodeArtist.mx / Rodrigo Fernandez Diaz 来源与 credits，核实完整许可是否允许作为通用框架工具分发；Store 元数据不是许可证明，修复/改名不改变这一点。不能核实则不导出原代码/Shader/素材，按同一功能契约采用独立实现与可合法分发的自有/许可素材，不能删声明伪装原创。该门禁不授权外部下载/购买，也不阻止本轮规划；正式工具的可分发收口必须给出对应证据。

## 6. 保留、融合、改进与退出

| 主要职责 | 推荐取舍 |
|---|---|
| Panel : View、Module owner、统一请求/退出 | 保留并复用；只做 UI 所需的直接接点，不再设计 MVC/Model 所有权 |
| PanelManager 排序/遮挡/根 | 演进为唯一策略，吸收 YL 层/遮罩/根配置；退出全局入队次序权威和 raw order Top 判断 |
| UIManager、UIRoot | 有效配置/入口/布局合入唯一 UI，正式类型按新职责演进；旧 UI 专用根与重复状态退出，不为 Tips/ColorSelector 保留 legacy adapter |
| UIWindowBase | 窗口便利能力确有收益则归入统一 Panel 特化，否则迁完消费者后退出；ColorSelector 不以继承旧基类桥接为完成结果 |
| UIWindowData/Attribute/WindowOpenType | 保留窗口定义价值，收敛为明确目录；退出可写 Instance 权威、未实现自动扫描的假契约和无实际用途入口；不因名称预定整类删除 |
| UISystemConfig/IWindowLoaderPool | 保留资源可接入原则，默认退出旧 UI 专有加载器池与失败无通知管线；有真实自定义后端再做窄适配，不吞并 Resource |
| Scaler、Background、Border、Camera 设置 | 保留领域差异，统一配置与刷新；适配不等于每个窗口强制 stretch；Safe Area 是可选容器，不取代背景/宽屏偏移 |
| InputSystemBridge/Pointer 查询 | 吸收实际能力，统一系统选择、模块与缓存责任；不建立游戏输入路由系统 |
| Tips | 正式加入统一 UI 的层、上下文与资源/任务所有权，短提示项可独立呈现；完善显示与收尾，退出旧 UIManager/Tips 根依赖 |
| ColorSelector | 从 Sample 提升正式 Runtime UI 工具，修复取色、输入、视觉、回调和材质生命周期；示例只演示新入口，许可/分发另有门禁 |
| YLFrameworkRoot / 旧混合宿主模板 | 公共 Root 保留 Audio/Action 的实际归属；旧 UI 子树/组件可退出或转换为统一根，不因旧模板存在强保留无用 UI Root，也不删除仍被非 UI 消费的类型 |
| CloseView、DragLayer、空 SceneTipsItem | CloseView 是通用 View 定时关闭组件，不因涉及时间就重写 Timer；Drag 根作为布局能力可保留，不建设未要求的拖拽系统；空占位仅在定向引用核对后处理 |

## 7. 推荐路线与独立验收批次

**3.1 → 3.2 → 3.3 → 3.4（路线已批准）。**先稳定 UI 策略，再完成旧机制退出与正式 Tips/ColorSelector 工具接入，随后完成适配/输入，最后做实际集成收口。允许批次内部按行为与身份分小步，但不为每个类建立新专题或重复方案。总体设计已批准，逐批实施授权分别处理；不得因路线批准自动开始 3.1 或任何批次。

### 3.1 Panel 策略与根上下文

- **交付：**根/Scene 上下文、稳定有效排序、独立窗口与借用 Canvas 边界、显示/遮挡/模态策略及焦点状态基础。直接演进 `C/mvc/Panel.cs`、`C/Manager/PanelManager.cs` 和必要的 Module/View UI 接点；不迁旧 YL 类型/模板、不换资源后端。
- **验收：**复用 CoreDirectView/CoreXfab 的 Loaded/inactive、Canvas 复用、队列失败回滚、排序旧 Session 与 Module 偏移用例；补跨 Module 高低 order、两独立根/Additive、UI/DDOL、模态栈恢复、共享 Canvas 不被误隐藏。一个业务排序/Visible 回调失败仍完成必要结构与其他清理，错误可观察；pending/索引/参数旧契约不变。
- **主要风险：**层优先级与 Module 偏移、Canvas 原生首次激活、输入焦点回调重入。先证明排序和显示一致，不用每帧全库扫描或自动压低测试断言解决。

### 3.2 窗口入口、Tips 与 ColorSelector 正式融合

- **前置：**3.1 验收及本批实施授权；自有迁移和 ColorSelector Runtime 工具范围已确认，不重复询问。
- **交付：**有效窗口目录/Root 配置直接归统一 UI，唯一窗口与多实例、Close/复用/不缓存及同步/异步共用事务；旧加载/事件/实例缓存/根机制退出。Tips 成为正式能力并闭环 active 收尾；ColorSelector 按 5.6 正式化与修复，演示不再是功能实现所在。保持公共 Root 的非 UI 职责，不重写 Pool/Action/Timer。
- **范围：**`Y/UI/` 直接相关类型及旧依赖退出、现有 Core UI/正式 Tools 源码和资产、YL 自有模板及旧 ColorSelector C#/Prefab/Shader/Material/meta 的迁移、Samples 演示/必要 asmdef、相关测试/Editor 引用与最小公共 Root UI 副作用调整。不是默认新增整体 Runtime→Core。原游戏、原资源链及包不变。
- **验收：**迁移存续模板导入无 Missing Script，实际层/mask/Camera/Tips 引用有效；旧 UI 专用组件/目录不再是正式工具运行前提。唯一 key 并发/取消/Module 重启/目标销毁不残留，失败不增 mask。提示正常完成、异常、取消、根销毁和旧回调只收尾原借用。ColorSelector 初始值/HSV/游标/回调一致、共享材质不污染、双实例/复用/关闭重入正确；演示明配 owner/Prefab，用的是正式工具而非其复制版。已完成 1.3 的 Model/owner/参数/缓存断言仍成立。
- **主要风险：**基类与程序集身份、旧 Close/按钮监听、Loader 提前释放、ColorSelector 坐标/几何/Shader 与素材来源、关闭中的新会话。类型/字段退出在引用迁移验收后，不保留永久 legacy 作为完成结果。
- **迁移门禁：**先在受控自有样本验证新协议/字段与程序集变化，再定向迁上述模板与正式工具资产；存续资源保存 GUID/fileID/有效引用，退出资源处理引用及配套 meta，拒绝全库重序列化。迁移失败时停止对应退出，保留可恢复原数据，不自动创建兼容壳或执行 Git 回退；验证后按已确认范围清退无用旧组件/类型。未迁完或工具仍依赖旧 UI，不宣布批次或阶段 COMPLETE。
- **实施节奏（已批准）：**本批工作量较大，允许在正式批次内部根据依赖渐进实施和验证（如先窗口/根、再 Tips、再 ColorSelector），不额外设立正式子批次；每一步保持可独立验证且不破坏现有行为。

### 3.3 UI Adaptation 与统一输入接入

- **交付：**Scaler 配置权威、显式 stretch、Background/Border 动态刷新与校验、经确认的可选 Safe Area；根 Overlay/Camera 模式、统一 EventSystem/输入桥、pointer 查询与真实模态输入。`Y/YLFrameworkRoot.cs` 只调整必要 UI 副作用，不能改普通 Singleton/Audio 生命周期。
- **验收：**参考分辨率同屏变化、横竖屏/宽屏、实际 Canvas 尺寸变化、inactive 启用/重新挂载、零尺寸/缺依赖配置明确失败；Camera 模式窗口与背景布局合理。以正式 ColorSelector/Tips 为直接消费者验证取色坐标/游标、Mask/RectMask2D/alpha、提示位置随适配变化，及按钮/Slider/拖动、遮罩阻断、Submit/Cancel/焦点恢复。既有 EventSystem 不被替换，自建系统跨场景/根退出正确清理；实际可用后端分别报告，New-only 不能被 Both 结果覆盖。
- **主要风险：**UGUI 布局刷新时点、camera 坐标、安全区、静态输入缓存/反射及 Player 裁剪。不引入 TMP、额外输入 Package 或全局屏幕服务作为默认解决方案。

### 3.4 资产、Editor 与阶段门禁收口

- **交付：**复核唯一窗口生命周期/策略、旧 UI/Root 退出、正式工具资产身份、GUID/字段/UnityEvent、来源许可及实际程序集；补必要 UI 校验/局部迁移支撑，增量维护现有 UI 使用说明、README/Architecture 的“ColorSelector 仅 Sample”旧定位、长期蓝图及必要工具用法。导出只包含可合法分发内容。本轮不预先修改这些文档或导出配置。
- **验收：**一次定向跨模块回归、原游戏代表性菜单/设置/进入/返回/再入及点击音非干扰；关闭 Domain Reload、正常 Scene Reload 的两轮真实 UI Play 验证根、静态 hook、pending、输入/短提示旧轮次及退出不重建，恢复用户设置/场景。继承既有门禁但不重跑整个公共系统全集。
- **Player：**鉴于正式 UI 工具/素材从 Sample 迁入 Runtime、类型身份与输入创建变化，安排一次匹配当前产物的 Windows64/Mono 代表性 UI Player 验证，覆盖模板/Panel、ColorSelector 取色/Shader/双实例、Tips、输入与关闭复用；原游戏 Player 非干扰使用已有有效 MainModule 产物。没有匹配产物时分类为环境阻塞，不擅自重建 XFAB，不用 Editor 通过替代 Player。IL2CPP/移动设备/无原 15 系统承载仍归后续风险安排，不作为本轮执行内容。
- **主要风险：**同工程有原包掩盖依赖缺口、旧静态闭包、无 Domain Reload 的原 Logger/MCP 已知干扰。先定位，必要隔离另获授权；不沿用 Timer 测试的特批自动改变日志路径或吞错误。

## 8. 最小充分验证与完成标准

**现存可复用入口：**`T/Core/PlayMode/CoreDirectViewPlayModeTests.cs`、CoreXfabPlayModeTests、CoreDefaultResourcePlayModeTests、Core/EditMode/CoreDefaultResourceTests 与 CoreIdentityTests；游戏 YLAudioWiringTests/YLAudioGameFlowPlayModeTests；普通 Pool/Timer/Events/Update 只选择受影响直接消费者。尚无完整旧 UI/Adaptation 专项覆盖，不能把文档“有 UI”或历史 Core 通过写成新方案验证通过。

- 每批检查 diff、新文件/meta、实际 asmdef 与直接消费者，然后执行对应 Unity 编译和定向测试，分层报告。新增回归围绕确认缺陷/新契约，不建设大型 Mock 资源后端或矩阵测试平台。
- CoreDefaultResourceTests 当前钉住复制模板逐字一致/组件数量；若新配置确实改变模板，更新为批准的新字段/隔离契约，保留 Resources key 唯一、GUID 独立、无原包依赖和实际组件有效断言，不以删测试掩盖迁移。
- ApiHelpTests 的旧 UIWindowBase.CurrentLayer 是格式器示例，不是产品必须保留旧基类的理由；如旧类型退出，改用有效的 protected setter 示例/测试 fixture，保留可见性断言。UI 没有反向依赖 Runtime，Audio/Action 的公共 Root 归属和生命周期原验收不变。
- 资产/Inspector 实际保存重开、必要 Undo/Prefab override 与 UnityEvent 调用独立验证；只读 YAML/GUID/C# 编译不能代替导入和运行。测试只清自建窗口/根/实例/资源，不能保存用户未保存场景、清全局缓存或修非法残留制造通过。
- 吸收、移动和分发 UI 源码保留适用的上游声明及完整许可；Core 的 XF MIT 继续随附，必要时增量维护现有 THIRD-PARTY-NOTICES。旧 YL UI 不因近似命名被猜成 QFramework 来源，不机械补每文件来源模板。ColorSelector 的第三方许可核查/替代门禁独立于 XF MIT；不能把私有工程验证通过写成已具备再分发权。
- 阶段完成要求：新 YL 只有一套窗口生命周期和 UI 策略；多实例/唯一窗口、层/遮罩/输入/适配、同步异步、关闭复用及场景边界有代表性证据；Tips 与 ColorSelector 为正式 Runtime 能力，不靠旧 UI 运行，ColorSelector 已按 5.6 修复/优化且演示只消费正式实现；原游戏/资源链未迁移且无新增干扰；迁移、来源/分发及必要 Player 门禁分别满足或明确阻塞，未满足不得宣称完整可分发收口。
- 完成不等于 XFAB 独立、真实 AB 全面卸载、任意共享生产者物理取消、所有平台/世界空间 UI 全序或同时关闭 Scene Reload 的整框架恢复。性能只报告明确成本或实际测量，不以 Canvas/宿主数量推断瓶颈。

## 9. 备选与重大决策

| 方案 | 价值与代价 | 判断 |
|---|---|---|
| 原样保留两套 UI，互相包装 | 模板身份少变化，但 owner/缓存/队列/输入重复和旧缺陷都留存 | 不作为完成方向；只允许迁移过程短暂共存 |
| 旧 YL UIManager 为唯一内核，Panel 转接它 | 层/目录入口直接，但要重建已有 View 事务、Module 多实例和稳定异步，且引入反向依赖 | 不推荐 |
| Core 唯一策略，在 Runtime 保留旧入口/特化并依赖 Core | 初稿可少改资产形态，但会扩大整体 Runtime→Core/临时 XFAB 可达性，容易为旧类型留下无用层 | 不再默认采用；用户已接受正式融合与无用 Root 退出，不以兼容 Sample 为理由保留该依赖 |
| 正式 UI/Tips/ColorSelector 收入现有 Core UI 域，旧 UI 专用层迁完退出 | 复用已验收协议，工具可独立接统一根；维持 Core/Runtime 的现有隔离，需要定向迁移身份/字段/素材 | **修订推荐**，不引入反向依赖，不预建新产品程序集 |
| 新独立 UI asmdef 或全面重命名 | 可进一步分层，但需要跨已有 Panel/View/Runtime 闭包迁移，当前没有足够独立收益 | 不作为本阶段默认动作；正式工具命名可局部统一，不能演变为全库搬迁 |

**批准记录（2026-10-11 用户审阅通过）：**批准的是总体架构、行为方向、功能边界与实施路线，不是强制采用方案中的全部具体 API、类结构或内部算法；普通实现细节保留批次内自主设计。**批准不等于执行授权：3.1 及任何后续批次仍等待 Git 收口与单独实施授权。**

1. **D1 已批准（YL 自有 UI 定向迁移）：**接受 YL 自有定向迁移；Tips/ColorSelector 真正归统一 UI，不保留两套架构适配；融合后无职责的 UI Root 可退出。保护存续资产身份和有效数据，不迁移原游戏/原资源链，不全量补旧 API shim。公共 YLFrameworkRoot 有 Audio/Action 实际消费者，不属于可按旧 UI 直接删除的对象。
2. **D2 推荐总体方向已批准：**Core Panel/View 统一体系、根与显示上下文、排序与遮挡/模态策略、输入与 UI 适配边界；正式 UI/工具归现有 Core UI 域，Samples 仅演示；不默认新增整体 Runtime→Core，不以旧入口形态驱动依赖。具体类型/组件/接口按功能价值演进；如果实际编译闭包迫使改变这一方向，提出真实阻塞和最小替代，不偷偷反向引用或扩大专题。
3. **D3 已批准（ColorSelector 正式 Runtime 工具及功能边界）：**HSV 色环/三角、初始颜色、当前值读取、实时回调及输入/渲染/复用/资源修复；无 Alpha 编辑、数值/Hex、确认取消和 Editor 专用工具。
4. **D4 推荐总体方向已批准：**Close 结束会话（不保留永久 key 实例）、Overlay 不充当普通窗口 Top、模态输入/焦点独立于遮挡、Tips 默认 unscaled 等行为方向，以及 3.1→3.2→3.3→3.4 四批实施路线与独立验收门禁；3.4 保留基于实际资源和输入变化的 Windows64/Mono 代表性 Player 验证门禁。具体 API、类结构与内部算法仍由批次自主决定。
5. **Safe Area（已纳入阶段 3）：**定位为可选、默认不改变现有布局的轻量能力；不扩展为通用屏幕事件系统。
6. **实施风险提示（随路线保留）：**
   - 3.2 工作量较大，允许 Kilo 在正式批次内部根据依赖渐进实施和验证，不额外设立正式子批次。
   - ColorSelector 的来源许可与再分发权限必须核实；不能确认时采用合法的替代方案。这不阻断 3.1。

先前被忽略的迁移问题已由用户后续回答解决，不能继续把 D1 写成未选择或把 3.2 视为范围未定。**总体设计批准不等于执行批准**；本轮完成批准记录后停止，等待 Git 收口与 3.1 单独实施授权，不自动启动 3.1 或任何后续批次。

**总体方案批准记录到此完成。停止，等待 Git 收口与 3.1 单独实施授权；不自动启动 3.1 或任何后续批次。**
