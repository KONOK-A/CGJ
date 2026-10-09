# 阶段 1.3：View 核心融合研究与设计

状态：**整体设计已批准；1.3-A 已验收并提交（`22b281d`）；1.3-B 已实施并通过专项技术验证，待用户验收，未提交；C～E 未开始**。

本文是阶段 1.3 唯一专项方案，保存源码研究结论、已批准架构方向、已确认约束、实现参考与实施批次。用户已原则上批准 D1～D4 总体方向及 A～E 规划，保留 D1/C1/C2 原则；具体 API、内部机制及实现取舍不要求逐条照搬草案。第 8.3 节的三项开放评估原则优先于正文早期推荐的具体做法。

整体设计批准不等于本轮代码实施、技术验收或 Git 授权。本轮仅同步本文，不修改 Runtime、Editor、测试或资源，不执行任何 Git 操作；完成后停止，等待用户开始 1.3-A。后续仍维护同一文件，不创建平行版本。重大架构方向变更先说明依据并等待确认，普通内部取舍可在获授权批次及已批准边界内自主确定。

长期边界见 [IntegrationPlan.md](../../Assets/YLFramework/Documentation/IntegrationPlan.md)，前置契约见 [1.2 Module/MVC 方案](xf-yl-1.2-module-mvc.md)，源码归属基础见 [1.1 承接方案](1791455404620-xf-yl-core-ownership.md)。

## 1. 结论与研究基线

推荐以已承接的 XF View 为基础，形成唯一的通用表现协议：**Module 管业务归属，View 管表现会话，ViewManager 管加载与退出事务，资源后端管实例及资产缓存，Panel/UI 管界面策略**。

不直接将旧 YL UIManager、IOC、Pool 或 Resource 整套接入 Core。双方有可吸收的能力，但其所有权、加载与释放语义不同。1.3 首先解决 owner/index、异常收尾、异步终态及 Unity 生命周期协作，不以统一命名或 Manager 数量作为验收目标。

### 1.1 实际版本与状态

| 项目 | 初始研究已核实基线，本次增量沿用 |
|---|---|
| Git | 初始研究为 `integration/xf-yl`，HEAD `30de1fcfbb02f1a3c64aa78b9502f6c29cb65d39`；当时工作区干净。本次增量不执行 Git 操作，不声称重新核实当前分支/工作区 |
| 1.2 | 当前源码已包含 A～F 的注册事务、唯一关闭、Shutdown 和启动失败回滚；`30de1fc` 已完成阶段 1.2 合并。本轮用户也明确其已完成，不按旧文档的待 Git 收口描述重做 1.2 |
| Unity | `2022.3.62f3c1`，依据 `ProjectSettings/ProjectVersion.txt` |
| 原 XF | XFGameFramework `1.18.8`，revision `2182708aecc596f30739fa8634fe2b9b6946ddd7` |
| 原 XFAB | XFABManager `1.24.0`，revision `c35dc4058ecd831409199b43cb328024202d4557` |
| 承接位置 | `Assets/YLFramework/Runtime/XFMain/`，namespace `YLFramework.Core`，程序集 `YLFramework.Core.Runtime` |
| 临时依赖 | Core 直接引用独立 Foundation、XFABManager 和 Unity UI，不直接引用原 XFGameFramework 或旧 YLFramework.Runtime |

原包版本已核对 `Packages/packages-lock.json:361–383`、实际 PackageCache 目录及包内版本信息。对应目录为 `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/` 和 `Library/PackageCache/com.xfkj.xfabmanager@c35dc4058e/`，仅只读研究。

当前长期蓝图与 1.2 文件仍保留 F 待最终确认/Git 收口的历史表述。源码、实际提交及本轮用户说明优先；本轮不顺带修改其他规划文档。

### 1.2 证据口径

- **已确认事实**：可由本轮读取的源码、调用方、程序集、资产文本或测试断言直接证明。
- **合理推断**：从控制流或可构造用法推导的风险，未冒充运行复现。
- **未验证事项**：新方案的编译、时序、回归、真实 AB 和 Player 行为。
- 本轮仅只读研究与指定方案文档编写，未运行 Unity 编译、测试、游戏或构建。
- 1.2-F 文档记录 Core PlayMode 63 项及直接生成器 EditMode 33 项通过，属于历史证据，不是本轮执行结果，也不证明拟议 1.3 方案正确。

### 1.3 已确认约束与整体批准范围

依据用户本次明确确认，以下作为后续设计和实施的前置约束，不再要求重复批准：

1. **D1：View.Close 默认仅结束当前表现会话，不隐式移除或销毁关联 Model。**Model 的生命周期仍由所属 Module 管理；Controller 或业务可显式请求移除。Module 整体关闭继续使用 1.2 唯一清理管线，不建立第二套 Model owner。
2. **C1：保护旧 YL UI 的真实序列化资产接入。**1.3 不修改这些 UI Prefab/关联资产，不通过迁移旧 UI 类型、改变其程序集/序列化字段等方式间接破坏接入。
3. **C2：现有订阅清理与 Shared Abort 不是可靠的 View 退出/取消协议。**必须根据已知异常与执行语义独立设计 View 收尾和业务请求失效，不能直接套用工具而宣称协议成立。

用户现已原则上批准 D1～D4 整体方向和 A～E 分批规划。具体 View/Model API、身份实现、实例异常处理及加载流程仍由实际源码和收益判断，见第 5 节及第 8.3 节；批准的是架构目标和边界，不是指定所有字段、签名或算法。可保留已满足需求的现有机制，并在相应批次简述依据；不为形式统一或假设风险增加复杂度。

## 2. 双方能力与源码依据

下文 Core 文件均相对 `Assets/YLFramework/Runtime/XFMain/`；非 Core YL 文件相对 `Assets/YLFramework/Runtime/`。原包文件按第 1.1 节目录定位。行号对应研究基线，实施后以符号和当前源码为准。

| 能力 | XF / 承接 Core | 现有非 Core YL | 判断 |
|---|---|---|---|
| 通用表现对象 | `mvc/View.cs` 是 MonoBehaviour，可服务世界对象和 UI，关联 Module/Model | 主要提供窗口、资源、绑定与生命周期辅助，没有同等完整的通用 Module View 协议 | 保留通用 View，不以 UIWindowBase 替代 |
| UI | `Panel : View`，Canvas、排序、界面遮挡与持久 UI | UIWindowBase、窗口目录、层、遮罩、全屏布局、输入和提示 | 重叠在窗口生命周期，互补在表现策略；完整融合延后 |
| 业务索引 | Module 按名称/ID、实际类型存 View；当前仍隐式查 BaseModule | UIManager 按窗口 key 保存单实例，没有 MVC owner | 采用 Module 唯一业务归属，避免双索引所有者 |
| 实例复用 | XFAB 按 Prefab 建池，复用闲置 GameObject | UI 缓存窗口单实例；另有按 key 的 GO 池 | 复用策略不同，不机械合并 |
| 资源 | XFAB 项目名/资源名与全局缓存 | ResLoader 持有资产引用；YL GameObjectLoader 的实例归调用方 | 不能只因同名 Loader 就互换 |
| 事件/绑定 | View 声明事件，Enable 订阅、Disable 退订 | 注销句柄、订阅集合、生命周期 Trigger、BindableProperty 等 | 借鉴显式作用域，1.3 不统一总线/绑定系统 |
| 装配/执行 | Core 使用独立 Foundation 协程与请求 | IOC、Singleton、Root、Action、普通对象池等 | 非 View 所有者，不强制接入 |

### 2.1 Core 与原 XF 的主要依据

- [View.cs](../../Assets/YLFramework/Runtime/XFMain/mvc/View.cs)：`38–50` owner 懒推断；`76–104` Unity 生命周期；`113–129` Close；`163–200` 场景初始化与移除通知；`355–415` 事件声明及启停。
- [Model.cs](../../Assets/YLFramework/Runtime/XFMain/mvc/Model.cs)：`18–41` ID/owner 与初始化/退出标记；目前没有区分同实例多次注册的内部身份标记。
- [ModelManager.cs](../../Assets/YLFramework/Runtime/XFMain/Manager/ModelManager.cs)：`16–69` 临时注册与初始化回滚；`73–100` 精确移除、OnExit 和最终解绑。绑定不能把临时可见误当完成，也不能将解绑写成 RemoveModel。
- [ViewManager.cs](../../Assets/YLFramework/Runtime/XFMain/Manager/ViewManager.cs)：`19–80` 注册/移除；`87–111` 资源 fallback；`178–193` 初始化；`272–375` 清理；`381–569` 查询。
- [LoadViewRequest.cs](../../Assets/YLFramework/Runtime/XFMain/AsyncOperation/LoadViewRequest.cs)：`21–114` 两条异步路径。
- [ModuleManager.cs](../../Assets/YLFramework/Runtime/XFMain/Manager/ModuleManager.cs)：`205–216` 对象事务门禁；`219–275` 唯一关闭；`282–308` Shutdown；`386–413` 启动失败回滚；`484–492` 当前模块上下文。
- [Module.cs](../../Assets/YLFramework/Runtime/XFMain/Module/Module.cs)：`39–48` 索引；`308–522` View 加载入口；`714–818` 查询入口；`972–975` Closing/Closed 新增门禁。
- [Panel.cs](../../Assets/YLFramework/Runtime/XFMain/mvc/Panel.cs)：`106–135` Canvas 初始化；`206–209` 加载准备。
- [PanelManager.cs](../../Assets/YLFramework/Runtime/XFMain/Manager/PanelManager.cs)：`201–294` 同步/异步入口；`461–467` 依赖 View 广播清理；`469–498` 父节点和自有 Resources。
- [LoadPanelRequest.cs](../../Assets/YLFramework/Runtime/XFMain/AsyncOperation/LoadPanelRequest.cs)：`18–82` 子 View 等待与 Panel 后置初始化。

原 XF 的上述同名文件仍是重要对照，但不把原行为视为必须保留的标准。原 XF 异步基础模块 fallback 错用请求模块项目名的缺陷，承接版本已在 1.2 改为 `item.ProjectName`；当前待解决问题不重复包含此已修正项。

### 2.2 旧 YL UI 与 Foundation

- [UIManager.cs](../../Assets/YLFramework/Runtime/UI/UIManager.cs)：`210–271` 同步显示，`311–352` 异步显示，`432–493` 缓存隐藏/销毁与层计数。窗口策略确实存在，但不提供 Module owner。
- [UIWindowBase.cs](../../Assets/YLFramework/Runtime/UI/UIWindowBase.cs)：`26–43` 显示/关闭及 UI 事件，`62–76` 销毁卸载。缓存 Close 不释放 Prefab，实际 Destroy 才卸载 Loader，这个区分值得保留。
- [UISystemConfig.cs](../../Assets/YLFramework/Runtime/UI/UISystemConfig.cs)：`95–134` 通过 ResLoader 加载和释放；异步失败不通知上层。
- `Assets/YLFramework/Prefabs/YLFramework.prefab:820–846,1035–1049` 已有 UIManager 的层、遮罩、提示节点及 UIRoot 的 Camera/Canvas/Scaler 引用。旧 UI 不是纯占位，后续类型/程序集变更须处理真实序列化资产。
- `Foundation/Binding/BindableProperty.cs`、`MappedProperty.cs` 提供值变化及显式订阅释放，不创建或管理 View。绑定关系寿命应由业务会话决定。
- `Foundation/Events/Lifecycle/SubscriptionCleanup.cs` 区分成功后释放与先摘除集合；`UnRegisterTriggerBase.cs:44–57` 的逐项清理仍会被异常中断。不能机械当作 View 的异常安全收尾。
- `Shared/CustomAsyncOperation.cs:84–94,131–139` 先改 error 再检查完成，Abort 先 Completed 再 OnAbort；默认没有停止生产者。因此 View 必须覆盖业务 Abort 入口并在通知前完成自身失效，不仅追加一个 OnAbort 回调。
- `Foundation/Events/Lifecycle/OnCurSceneUnLoadUnRegisterTrigger.cs` 响应任意场景卸载，包括 Additive，不是 View owner 场景契约。
- `Foundation/IOC/Core/IOCContainer.cs` 创建纯 C# 对象/注入已有实例，Clear 不 Dispose，不应 Resolve 构造 MonoBehaviour，也不替代 Module owner。
- 普通对象池的 reset、重复回收与 GO 池的激活、容量销毁是不同职责；没有一个现有池同时维护 View owner、注册事务和资产租约。

**程序集事实**：`Runtime/Foundation/` 的旧 Binding/Events/Pool/IOC 主要仍编入 `YLFramework.Runtime`。独立 `YLFramework.Foundation.Runtime` 对应的是 `Runtime/Shared/`，并非整个 Foundation 目录。Core 直接调用旧基础能力会新增整体 Runtime 依赖，不以目录名掩盖这一影响。

### 2.3 两条资源链不能等同

- [YL GameObjectLoader.cs](../../Assets/YLFramework/Runtime/Resource/GameObjectLoader.cs)：`9–14` 明确内部 ResLoader 持有 Prefab，Instantiate 的实例属调用方，不自动 Destroy；这不是 XFAB 实例池语义。
- `Resource/Framework/ResLoader/ResLoader.cs:82–100,385–415,488–561` 有显式引用和回池收尾；每 Loader 持有资源的方式不等于 View 生命周期或任意使用者租约。
- XFAB `Runtime/Loaders/GameObjectLoader/GameObjectLoader.cs:197–201,313–321` 支持取得 Prefab 后直接入原池；`550–606` 先实例化/激活，再返回 Core；`609–623` 归还只停用并记录时间，不脱离 parent。
- 同文件 `75–87,347–395,513–520,710–714` 表明实例闲置、空池关闭及 Prefab 归还分阶段发生，默认各约 10 分钟并受扫描延迟影响，年龄使用 scaled `Time.time`。
- XFAB `Runtime/AssetBundleManager.cs:1544–1560` 的资产缓存按对象去重，不按每消费者递增。不能从 View 本地计数推导共享 Prefab 可安全卸载。
- XFAB `Runtime/AsyncOperation/LoadAsset/LoadAssetRequest.cs:30–36` 在 Editor Assets 模式可能成功返回 null；其 GameObject 请求随后创建 null 池抛异常且不完成。这是 Core 可以局部绕开的具体路径。

当前全局 XFAB 配置是 Editor Assets 模式，依据 `Assets/Resources/XFABManagerSettings.asset:16–24` 及原包 `Runtime/Enum/LoadMode.cs:16–20`。Assets 下正常加载/归还不证明 Player 的 Bundle 物理释放。新 YL 和原 XF 的类型/Resources 路径已分开，但暂时仍共享 XFAB 的全局加载、池和清理状态。

## 3. 保留、融合、调整与淘汰方向

| 分类 | 推荐方向 | 理由 |
|---|---|---|
| 保留 | View/Panel 分层、Module 业务 owner、按实例/名称/类型查询、集合快照、NonAlloc 入口 | 支持多实例和非 UI 表现，已有架构基础合理 |
| 保留 | XFAB 临时资源后端和实例池；逻辑 Close 与物理释放分开 | 避免在 1.3 重造资源系统或改变原游戏后端 |
| 吸收 | YL 对资产/实例分别声明所有权、显式订阅释放、UI 配置及层级策略 | 改进契约表达；UI 策略留到后续，不直接复制整套实现 |
| 调整 | 单 owner 校验、双索引完整性、加载/退出事务、参数回收、请求终态 | 当前实现不足以兑现生命周期一致性 |
| 推荐淘汰 | View getter/Start 根据 CurrentModule 猜 owner；查询递归搜索 BaseModule | 多模块/异步/场景切换下归属不可靠，隐式查询有真实递归缺陷 |
| 已确认调整 D1 | 取消 Close 默认自动 RemoveModel | 表现会话结束不改变 Model 的 Module owner/索引/生命周期 |
| 已批准方向，具体 API 待评估 | 可靠退出、明确绑定/解绑与扩展职责 | 固定 Close、受控绑定槽等是实现参考；是否改变现有签名/getter 应按真实调用方和收益判断，不强制全部改造 |
| 暂不取舍 | 完整旧 UIManager、UIWindowBase、通用 Pool/Events/IOC/Action/Resource | 发现邻接问题不等于获得完整系统融合授权 |

不保留未发布旧 API 的无依据兼容层。确有资产、动态绑定或调用依赖时按证据适配；原游戏继续使用原 XF，不因新 YL 的 API 调整而迁移。

## 4. 当前问题与风险分级

### 4.1 必须在 1.3 解决的 Core 问题

| 问题 | 已确认依据 | 设计要求 |
|---|---|---|
| 场景 View 不进索引 | View.LoadOnStart 没有 AddView | 明确受管范围，不再把未索引对象描述为模块完整 owned 集合 |
| 初始化异常留下半完成对象 | InitView 无回滚，args 无 finally | 临时注册、业务初始化、提交和失败收尾成对 |
| 移除回调阻断清理 | RemoveView 先调用用户通知再删索引，Close 后端归还在其后 | 单对象内部继续清理，不仅依赖 Module 最终强制清空 |
| 普通 Close 残留 owner | RemoveView 注释掉 Module 清空 | 任意正常退出都结束会话 owner，不等到 Module.Close |
| 索引重复/跨 owner 缺校验 | AddView 类型列表无条件追加 | 同实例幂等，冲突明确拒绝，精确实例删除 |
| BaseModule 查询互递归 | GetView(module, name) 递归调用各基础模块的同入口 | 本地查询默认；资源 fallback 单次遍历 |
| 异步跨关闭/重启注册 | 请求只有 Module 引用，没有有效代次或 pending 失效 | 关闭失效、迟到隔离、同实例重启安全 |
| 请求异常/Panel 失败不终结 | LoadView 无异常完成保证，LoadPanel 失败直接 yield break | Core 自身错误路径具有稳定终态 |
| 参数池保留引用/失败泄漏 | Recycle 不立即清内容，失败路径不统一归还 | 一次接管、一处收尾、即时清引用 |
| 必要 Panel 清理依赖普通广播 | ON_REMOVE_VIEW 触发全局多播后处理 Panel | 必要登记直接释放，广播只通知 |

### 4.2 契约局限，不误标为已复现 bug

- 当前 View 退出移除本地且精确匹配 Model，是 1.2 明确保留的行为；跨 owner 或同 ID 不同实例已被 ModelManager 防护。
- 一 Model 对多个 View 时，首个 View.Close 会结束其他 View 仍引用的数据，是可由源码构造的风险，未运行复现。用户现已确认 D1 改变这一默认行为；当前 Runtime 尚未实施，不把设计确认写成缺陷已修复。
- 当前 `View.Model` 为可重写只读 getter，`YLCoreSmokeView.cs:16–18` 实际从自定义字段返回引用，Core 无法通用地清掉派生字段。仅删除 RemoveModel 调用不能证明绑定/缓存引用已正确结束，需明确框架绑定槽与业务自有字段的边界。
- 场景 View 不纳入索引是当前真实行为；将其改为显式受管是契约选择，而非声称所有历史用法均错误。
- GameObject 会保留自定义字段及用户持有的引用。框架不能自动重置任意业务状态，缓存复用必须有业务加载/退出责任。

### 4.3 后端及相邻系统限制

- Shared 和原 XFAB 的 Abort 主要是标记完成，不停止生产者；后续完成还可能改写 error。现有 Foundation 测试明确表征该行为，不在 1.3 全局改写。
- XFAB 下层协程异常、失败 Bundle/依赖回滚及共享资源立即释放仍不可靠；Core 只能绕开已定位路径或使业务请求失效，不能声称底层任务停止。
- 缓存归还不脱离 parent。缓存对象随场景/父节点销毁是现有后端行为，不保证跨场景保活。
- 旧 YL UI 的 pending/失败回调、层计数及外部销毁退订有缺口，不能直接当可靠 Core 后端；完整修复留给 UI 专题。
- 原 Package 的 Editor 自动初始化仍存在。`autoReferenced=false` 不代表禁用初始化；不修改原包来实现隔离。
- 已记录的原 LoggerManager 退出残留没有由本轮解决，1.4 定向定位，不将其当新 YL View 已确认回归。

## 5. 推荐 View 契约

本节 D1～D4 总体方向已批准，D1/C1/C2 是持续约束；具体 API、内部机制与处理策略是实施参考，不是必须逐项照搬的技术清单。选择须满足生命周期正确性、实际依赖和批次验收，尤其遵循第 8.3 节的开放评估点。设计批准与批次实施授权分开，本轮不修改运行时。

### 5.1 归属、会话与索引

1. Module 仍是 View 的唯一业务 owner；ViewManager 只是该容器的操作协调者，不另设全局所有者。
2. 一次表现会话指一次绑定/初始化到退出。缓存 GameObject 与 `View.Id` 可不变，但每次复用产生新的有效会话身份。
3. 使用少量内部状态表达未绑定、初始化中、已加载、退出中，不向外引入通用复杂状态机。Disable 不改变业务归属，不等于退出。
4. 记录 owner、会话身份、取得方式、取得根对象及实际场景；命名资源另记实际来源用于诊断。内部载体按最小实现选择，不预设通用 lease 框架。
5. 以实例 ID/引用身份覆盖全部受管 View。名称、实际类型为辅助索引；没有资源名称的场景/嵌套 View 不编造 Prefab 名。
6. 重复绑定同一已完成会话幂等；初始化/退出期间重复绑定、跨 owner 或冲突索引拒绝；移除校验当前 owner、引用及会话身份，不误删新绑定。
7. D2 已批准有效注册 Starting/Running owner 的 View 加载/挂载方向，避免未纳入 ModuleManager 的 Unity 会话。门禁的具体落点与 API 按调用方确定，不改变 1.2 的 M/C 注册前装配能力。

提供只读已加载状态供业务区分临时可见与可使用，不允许调用方直接写状态。初始化/退出钩子可查询自身临时实例，但其已加载状态为 false；查询本身不创建、初始化或复活对象。

### 5.2 查询与资源查找分开

- GetView/GetViews/ClearView 默认只操作当前 owner。跨模块借用或清理通过明确 owner 调用，不再隐式返回/关闭 BaseModule 对象。
- 缺失查询返回 null；单对象查询多匹配明确抛错。类型单查询按实际类型，类型集合查询包含派生类型；名称/ID 的泛型入口校验结果是否可赋给 T。
- `GetView<T>(id)` 直接按 ID 查并校验类型，不以 `typeof(T).Name` 推导场景对象名称；按名称+ID 的入口仍校验名称。
- GetViews 返回结果集合，不暴露可写权威索引；保留 NonAlloc，并过滤/收尾已失效 Unity 对象。
- 命名资源仍允许请求项目优先、有效基础模块项目按注册顺序其次。复用 ModuleManager 已有注册顺序作为依据，不依赖字典枚举顺序。入口捕获有序候选并去除重复；多个基础项目提供同名资源时第一个成功候选胜出，不递归查询活跃 View；共享 Prefab 不改变实例 owner。
- 资源缺失/加载错误可尝试下一候选；取得了不符合 View 类型的 Prefab，或业务初始化失败，不伪装成缺失资源继续 fallback。
- 同步、异步、泛型及旧参数适配入口共用类型校验和事务。修正旧泛型入口先初始化任意 View 再 `as T` 的路径，不返回 null 却留下已注册对象。

### 5.3 显式挂载与场景/层级责任

- 提供明确的单 View/层级挂载入口，例如 Module.AttachView/AttachViews；命名可在实施时收口，不建设通用场景服务。
- 默认启动场景取得确定 Scene 后，在 OnStart 前由其启动 Module 挂载该 Scene 的 View；这是加载事务指定归属，不是监听任意 sceneLoaded 全局猜测。
- 外部加载的 Additive 场景由调用方明确指定 owner；不改造整个 Scene API，也不自动卸载场景。
- 加载根对象时定向纳管其 View 子树，包括 inactive 子对象。后续动态新增 View 由调用方显式挂载，取消 Start 自行猜 owner/重复初始化。
- 参数只传给本次请求的根 View，嵌套 View 使用空参数或业务明确的单独初始化，不把同一 pooled args 传给多个会话。
- 框架取得的根实例是唯一池归还单元。其普通嵌套 View 退出只结束逻辑会话，不独立调用后端 UnLoad；关闭根会结束其受管子会话并只归还根一次。
- 批量清理须识别根/子关系，避免先把子对象单独隐藏再归还根而改变缓存子树的 activeSelf。业务单独关闭子对象时的隐藏状态由业务下次加载处理，不自动重置所有字段。
- 同一受管取得子树不允许冲突 owner；同 owner 既有合法绑定幂等，新挂载失败只撤销本次新建会话，不回滚既有绑定。
- 对调用方/场景拥有的对象，Close 结束绑定并隐藏，不送进 XFAB 池，也不默认 Destroy。显式再次挂载才能开启新会话；挂载本身不强制激活既有 inactive 场景对象。
- 场景 identity 使用 Scene handle。OnDestroy 与按记录场景的 sceneUnloaded 兜底幂等，覆盖 inactive 对象及缓存对象的销毁，不按 scene.name 或任意场景卸载清掉其他对象。
- 记录场景是失效对象的清理依据，不是禁止 Unity 层级迁移的锁。卸载时仍存活且已迁入其他 Scene/DDOL 的对象按当前实际场景更新跟踪，不因原 Scene 卸载误退出；已销毁对象按保存身份清理。迁移不改变 Module owner。

### 5.4 加载事务与 Unity 消息

推荐顺序：

```text
入口校验/接管参数
-> 取得并校验 Prefab/实例
-> 临时注册 owner 与索引
-> BeforeOnLoaded 等必要框架准备
-> 业务 OnLoaded
-> 提交 Loaded
-> 若 activeInHierarchy，开放框架业务事件订阅
```

- 临时索引允许初始化钩子查询自身，但不等同于初始化完成。拒绝同实例递归绑定/移除及会破坏事务的模块关闭/Shutdown；初始化其他合法对象不一律禁止。
- 将 View 同步初始化/退出事务纳入 ModuleManager 现有对象事务门禁。异步等待不是该门禁，不能因为有 pending 就禁止模块关闭。
- 注册前失败不调用业务退出；临时注册后失败尝试 OnRemove 进行部分初始化回滚。业务退出须容忍准备只完成一部分。
- 初始化失败清理该会话的索引、owner、框架订阅及取得实例。保留根因，额外清理错误关联到既有 Core CleanupException 口径，不用日志加 null 掩盖结果。
- 不自动删除所有初始化期间创建的 Model，不追踪任意用户外部副作用。若失败上抛导致 Module 启动失败，1.2 唯一回滚管线继续处理该模块。
- 两种现有参数入口暂保留并共用事务，不为形式收口立即删除所有旧签名。调用入口先尝试接管传入 args，再检查模块/资源前置条件；接管成功后即转交本次操作，入口抛错也消耗并回收参数，不要求调用方凭成功与否猜所有权。同一 args 不得并发用于多个操作，重复接管失败不动第一笔持有的参数。未传 args 时不为入口拒绝提前分配默认容器。
- 已接管参数在成功、拒绝、失败、取消的终态回收一次并立即清引用，业务回调只借用，不保存、修改回收状态或再次传入；复用状态重置也不扩展为通用 Pool 重构。
- Awake 用于本地组件准备，业务依赖在 OnLoaded/明确业务钩子中处理。初始化可能面对 inactive 对象，业务不得假设 Unity Start 已执行，也不得依赖未保证执行的 Awake 才能完成框架初始化。
- 原始 Unity OnEnable 可能在 XFAB 返回前发生，不能保证已有 owner/加载参数；业务依赖应使用框架 Loaded 后的钩子，而不是伪称后端支持 inactive-load。
- View 基类的 Enable/Disable/Destroy 消息为框架协议，业务扩展使用专用钩子，不隐藏这些消息。现有 MustCallBase/RestrictedCall 只是属性声明，不能当作运行时强制机制；不以它们代替事务收尾或场景兜底。
- 不默认引入 inactive staging root。若需要保证原始 OnEnable 已有完整上下文，须另行确认更强契约并验证 Awake/子树/池复用时序，不能留给实施代理静默选择。

### 5.5 退出、异常与事件

已批准可靠且唯一的退出协议。固定 Close 入口、将业务扩展移至 OnRemove 等钩子是推荐实现；是否修改 public virtual Close 应定向核对调用方及真实兼容需求，不以去掉 virtual 本身作为完成标准。无论采用何种 API，必要收尾不能依赖业务恰好调用 base 或普通通知成功。

```text
标记 Removing，递归/重复退出幂等
-> 停止框架业务订阅
-> 独立尝试会话移除通知及业务 OnRemove
-> 显式解绑框架 Model 槽并尝试业务绑定清理，不移除 Model
-> 直接释放必要 Panel 登记
-> 撤销当前会话索引/owner/框架引用
-> 按取得方式归还、隐藏或丢弃实例
-> 提交退出结果并发布可选广播
```

- 业务退出钩子期间保留必要 owner 与退出状态，自身查询不假装仍可使用；新的绑定/加载不得复活正在退出的会话。
- 内部必要清理必须独立尝试，不因某个监听抛异常跳过 OnRemove、其他退订或实例处理。模块批量关闭/ClearView 对兄弟对象继续清理，完成后汇总。
- 已成功 Loaded 的会话退出最多尝试一次业务退出和正常移除通知；初始化回滚尝试 OnRemove，但不发布成功会话的正常移除广播。
- onViewRemove 按业务会话管理并在退出收尾清除；Awake 声明的实例级事件定义可保留复用。两种引用寿命不可混淆。
- 业务必要收尾或初始化失败后，逻辑 owner/index/绑定必须结束；实例如何处理按 E1 评估 XFAB 登记、归还、销毁和再初始化能力。可安全恢复的对象不因出现异常就一律销毁，不完整状态也不能被当作下一次加载成功。直接销毁或额外隔离机制均不是预设要求；后端处理失败保留错误并说明仍未保证的范围。纯观察者错误不自动等价于实例污染，也不能阻断必要清理。
- 已销毁对象不再次归还后端；逻辑索引按保存的身份处理，不在 fake-null 对象上依赖现取实例 ID。
- 全局移除广播是可选通知，不是必要内部清理。广播观察者异常不改变已提交的生命周期结果，不承诺重写整条 EventManager 分发语义。
- 完成回调可以产生新绑定；所有旧事务尾部清理按会话身份校验，保留 1.2 已验证的其他 Module 重新加载后新 owner/index 不被旧关闭收尾清掉的保证。

框架事件有效条件为 Loaded 且 activeInHierarchy。Disable 仅退订，不移除索引/Model；Enable 幂等恢复；Close/Destroy 明确结束会话。Canvas.enabled=false 不是 OnDisable，是否暂停不可见 UI 的业务监听留给 UI 策略。

框架只清自己的 owner、主要 Model 绑定槽、参数、订阅和会话引用。业务额外 Model 字段、绑定句柄、协程及其他资源由业务退出/解绑钩子负责；不声称缓存实例自动恢复所有序列化或业务字段。必要清理逐项尝试并汇总，不能调用一次现有订阅 Trigger 就假定所有注销已成功。

### 5.6 View 与 Model：已确认原则与具体设计建议

#### 5.6.1 已确认 D1：Model 生命周期不归 View

- Close、Destroy、场景卸载、初始化失败、取消和池归还都不因关联关系自动 RemoveModel、调用 Model.OnExit 或重置其 ID/owner。
- 解绑是结束表现关联和订阅，不是结束数据。多个 View 可以关联同一个 Model，关闭其中一个不结束其他会话或 Model 注册。
- Model 原 owner 不变。借用其他 Module 的 Model 不向 View.Module 重复 Add；业务显式移除仍调用真正 owner 的精确 RemoveModel 入口。
- Controller/业务可以主动结束专属 Model，包括明确写在退出钩子中的移除；这是显式业务决策，不包装成框架默认策略或自动所有权开关。
- Module 顺序仍 OnClose -> Controller -> View -> 剩余 Model。正常情况下 View 结束后，Model 由后续阶段退出；Controller 可能先显式移除 Model，所以 View 退出钩子不能假定关联数据始终仍有效。
- 单独 View 加载失败不自动回滚业务新建的 Model；如果异常导致 Module 启动失败，则 1.2 的 Module 回滚独立负责清理其 owned Model。两种责任不能混同。

历史比较依据保留：自动本地移除适合严格一对一，但隐式决定共享寿命；自动/显式开关会增加分支且不能从 getter 判断共享。用户已选择显式生命周期，不再把这两种方案列为待选择出口，也不建设绑定图/引用计数。

#### 5.6.2 推荐 API：可选主要 Model 的受控绑定槽

以下是已批准绑定/解绑方向下的具体 API 参考，不是已实现接口，也不是强制签名。实施时按 E3 定向判断现有 getter、加载重载及业务扩展是否需要迁移；若保留现状已能满足会话正确性，应保留并说明依据。

| 表面 | 推荐语义 |
|---|---|
| `public Model Model { get; }` | 改为非 virtual、框架管理的只读主要绑定槽；有效时返回确切实例，无绑定或绑定身份失效时返回 null。getter 不触发退出、重新查询创建或解绑回调 |
| `protected void BindModel(Model model)` | 初始化/Loaded 时显式绑定当前已完成注册的 Model；同一有效身份幂等，已有不同绑定时拒绝，先显式 Unbind 再绑定。未附着、退出或解绑事务中禁止新增绑定；不自动注册/LoadModel |
| `protected void UnbindModel()` | 幂等结束主要关联，不 RemoveModel。先摘除槽再执行清理钩子，递归解绑不重复；解绑钩子期间禁止重新绑定 |
| `protected virtual void OnModelUnbound(Model previousModel)` | 业务解除 Model 专属监听/绑定的扩展点，接收刚摘除的旧引用；可能面对已退出 Model，只做可幂等清理，不假定数据有效。框架不靠调用 base 才清空槽 |
| 现代 LoadView/LoadViewAsync/直接 Panel 加载入口的可选 `Model model = null` | 显式传入主要 Model 时，在请求接受处捕获绑定身份，在 OnLoaded 前建立绑定。null 表示不预绑定；原 params/LoadViewArgs 内容不被框架自动扫描成所有权声明 |

理由：现有 virtual getter 无法保证派生字段在异常时清空；只读基类槽能给出明确清理承诺，而受控入口仍允许业务在加载钩子中自行选择数据。不增加泛型 View<TModel> 层级、自动 DI、强制每个 View 有 Model 或多对象绑定容器。

YLCoreSmokeView/直接测试是已知 getter override 调用方；显式槽方案需要适配这些使用。API 表给出当前候选语义，实施者可以依据真实源码选择更简单的等效方案，不要求所有 View 或重载都新增接口。真正额外业务字段仍由业务清理，框架不反射重置字段。D3 已批准明确生命周期/绑定/错误的方向；实际改动前按 E3 核对直接调用、资产及必要动态引用，只迁移真正受影响者，不建立无依据旧 API 兼容层。

绑定校验需同时满足：Model 有 owner、owner 精确索引仍指向同一实例、Model 不在初始化/退出事务、注册身份与捕获值一致。不能绑定临时 OnInit 对象，不能仅按类型/ID 查到其他对象后悄悄替换。解绑清理异常保留并向上报告，槽仍为空，不自动提交后续新绑定，也不宣称任意外部监听已解除。

#### 5.6.3 推荐最小身份支撑：Model 注册 token

仅比较 Module、类型、ID 甚至 Model 引用仍不足：同实例结束后可重新注册到同一 Module/同一 ID，旧表现或晚到请求不应自动恢复为新注册的绑定。当前 Model 只有 IsInitializing/IsExiting 和 owner，没有此身份，属于源码可证的设计缺口，不声称已经复现故障。

推荐在 Model/ModelManager 增加**内部、不序列化的注册 token**：每次建立临时注册分配新身份，退出/初始化回滚收尾清除，重复 Add 同一已完成注册不改变身份。使用引用身份比较，避免业务 ID 或对象自定义 Equals 干扰。只有初始化完成后的注册可被绑定；不恢复失败注册的旧 token。

View 槽/加载上下文只捕获该 token 和真实 owner，不修改 Model ID 或借用所有权。读取有效主要 Model 时按 token/owner/精确索引/初始化退出标记校验；失效返回 null，不触发 Model 生命周期或任意回调。旧引用仅为后续显式解绑清理暂存，Close 的 finally 必须释放。

这是 1.3 为绑定正确性提出的**小范围直接适配候选**，不重设计 1.2 查询/注册/退出顺序、公开 API 或序列化数据。新增一次注册的内部身份存储/分配是实施代价，尚未运行验证；A 批应评估实际绑定与晚到用法是否需要此身份，以及最小等效实现，不将 token 类型/字段强制化。普通内部身份实现可在已批准方向内自主确定；若判断必须改变 MVC 的重大生命周期契约，先说明并等待确认。

#### 5.6.4 解绑、缓存与异常收尾

- 框架在成功 Loaded 前确认槽有效；退出时先停止会话事件，独立尝试 OnRemove，再执行 UnbindModel/Model 专属清理，最后继续 owner/index/实例收尾。OnRemove 可读仍有效的主要 Model，但不会被框架自动移除。
- UnbindModel 先摘除槽、再调用 OnModelUnbound，清理失败也不恢复旧槽。回调重入不能重新填入旧会话；退出管线独立尝试余下步骤并汇总错误。
- 临时初始化失败也清槽并尝试已建立绑定的解绑钩子；注册前失败不制造业务绑定/退出通知。Model 注册、owner 和 ID 不受此回滚影响。
- 缓存对象在归还前结束主要绑定、会话订阅、onViewRemove 和参数使用权；新加载先保持空槽，按新会话建立绑定。不从旧字段、CurrentModule 或同 ID 对象恢复绑定。
- Disable 只暂停表现事件，不清主要绑定；Enable 只在绑定及会话仍有效时恢复相应业务监听。Model 专属监听与非 Model 的会话事件应区分，解绑不关闭整个 View，也不让无 Model View 失去普通功能。
- 框架只保证自己的槽、索引和清理尝试。业务订阅若捕获旧引用，必须在解绑钩子解除；外部清理失败仍可能残留监听，应报告并按第 5.5 节处理异常取得实例，不能因槽已空就宣称资源完全释放。
- Model 在 View 活跃期间被显式移除/owner 结束，不自动关闭 View，不构建全局 Model -> View 依赖图。主要 Model getter 返回 null；业务 Model 回调在访问前检查会话和有效绑定，Controller 可通过业务 View 提供的入口请求解绑/换数据。不得假定 raw 引用永远可用；getter 不承担主动解除任意业务订阅的副作用。

#### 5.6.5 异步晚到与绑定身份

- 显式 `model` 输入在同步入口/异步请求接受时捕获 owner+token；拒绝未完成/退出中的注册。失效、完成后释放上下文持有的 Model 引用。
- 等待结束、建立 View 槽前、提交 Loaded 前重新检查 Model 身份；原注册已结束、同 ID 换实例或同实例重新注册，均失败收尾，不回退 LoadModel、不借用新对象、不调用 Model.OnExit。
- 若请求的 View.Module 仍有效而借用 Model 的 owner 已结束，只使本次加载失败，不关闭 View.Module，也不清其他对象。
- 使用一般 args 在 OnLoaded 内调用 BindModel，是**绑定时选择当前有效对象**；框架不可能从任意 payload 推导更早的业务意图。需要保留请求发起时的 Model 身份时，应使用显式 model 输入，不仅传一个可重注册的 raw 引用。
- 取消/owner 关闭后不再建立槽或执行业务加载/解绑回调来假造会话；如果已临时建立槽，则按会话身份清理一次。迟到旧事务不能解绑池复用后新会话的 Model。

#### 5.6.6 对 1.2 与测试的明确影响

当前 Runtime 的 `ViewManager.RemoveView:65–67` 仍执行自动移除，尚未实现 D1；实施时删除该默认联动，并更新 View.Model XML 和直接契约说明，不改变 ModelManager 的显式精确移除。

`CoreDirectViewPlayModeTests.ViewModelRemovalIgnoresBorrowedAndDifferentInstancesAndExitsLocalModelOnce` 中本地 Model 随 Close 退出的断言按已确认原则替换为“保持注册、退出次数为零”；跨 owner/同 ID 防护保留，增加显式移除后退出一次。Module/FailurePath 测试保留 OnClose -> Controller -> View -> 剩余 Model，原先绑定 Model 在 View 阶段自动提前退出的精细断言改为后续 Model 阶段退出，不删除一次退出/异常继续/新 owner 防护。

新增必要回归包括：同 Model 多 View、业务显式移除、绑定 token 失效/同实例重新注册、解绑回调异常但槽为空且 Model 不退出、缓存新旧 Model 不串绑、独立 View 初始化失败不退出 Model、Module 回滚仍退出其 Model、异步晚到不重建原绑定。均尚未实现或运行。

### 5.7 资源与缓存过渡方案

已批准继续使用临时 XFAB 后端、区分资源与表现生命周期。先取得 Prefab、校验后调用 `GameObjectLoader.Load(prefab, parent)` 是研究提出的候选，不要求同步/异步统一采用两段式。按 E2 分别比较原有路径、两段式和必要替代方式，选择实际收益足够且满足 Core 契约的方案；可以保留同步现状而只调整异步，也可以保留已满足需求的原流程并说明依据。

收益是可在实例化前检查缺失 Prefab、类型、模块有效性、parent 与场景，并绕开已定位的 Assets-null -> null pool 挂起路径。池普通 Load 本就只接收 Prefab，后续 UnloadAsset 利用后端资产映射，不要求重新伪造 projectName/assetName 或 lease。

- 异步/sync fallback 应使用现有适合候选探测的后端入口，避免前一候选失败日志假装最终失败；具体签名以已安装源码核对，不修改 Package。
- 不调用全项目卸载、不调整全局池保留时间、不从实例归还推导资产已释放。
- 取消后已加载但未实例化的共享 Prefab 留在后端管理范围，不擅自 UnloadAsset。取得实例后的退出只归还/丢弃自己取得的实例。
- 不新增持久缓存 root 或保证跨场景复用。场景销毁缓存实例后，后端后续整理是合法行为。
- 不切换为旧 YL ResLoader/GO 池：它们有可借鉴能力，但替换会同时涉及程序集、资源配置与所有权重建，超出本阶段收益。
- Core 无需建立面向所有未来资源系统的公共后端接口。先把必要后端调用集中于当前加载/收尾边界，未来替换按真实需求设计。

### 5.8 异步请求、取消与代次

每个请求捕获 owner 实例、有效代次、资源候选、是否传入 parent、目标 Scene handle 及本次参数所有权；每次 Load 都是独立表现实例请求，不照搬 Module 启动去重。

1. 请求建立及 pending 记录先于协程可能同步执行，覆盖立即完成和回调重入。Module 维护自己的 pending 小集合，不另造全局 JobManager。
2. 等待期间及取得结果后校验模块、parent 和真实目标场景。不能只无条件 yield 下层请求而无法响应自身失效。
3. owner 进入 Closing/启动回滚时先使 pending 失效；沿 1.2 管线完成逻辑收尾后再发布相应终态。模块显式重启开启新的有效代次，旧请求不得重新注册。
4. Abort 取消本次业务结果，摘除 pending/参数并提交稳定终态，不调用共享后端请求 Abort，也不声明物理取消。
5. 失效后不再实例化、注册或 OnLoaded。实例化是同步段，仍在其后及提交前二次检查；如已取得实例，执行一次性失败收尾。
6. parent 被 Unity 销毁与调用时未指定 parent 不同；使用保存的是否传入信息和 Unity 存活语义区分。无 parent 时把实例放入捕获的有效目标场景，不跟随等待期间变化的 active scene 猜归属。
7. 结果校验同时检查 error、资产存活及类型。Core 自身控制流/初始化异常进入失败终态；保留原始 Exception，并沿用关联额外清理错误的口径。
8. 终态只提交一次，提交后不再改写 view/error/Exception。完成通知不反向改变结果；不改变 Shared 的全局完成、late callback 或逐订阅者分发契约。
9. 参数回收后必须解除请求对其的使用权，迟到代码不得再访问回池参数。完成、取消与释放不依赖普通用户回调执行成功。
10. 不增加任意默认超时。下层永不完成且 owner/目标仍有效时仍可能等待；用户 Abort、owner 关闭或目标失效必须可以终结 Core 请求。可靠 I/O 取消和全面挂起治理留给资源/AsyncOperation 专题。

D3 已批准明确错误与稳定结果的总体方向。同步抛异常、异步保留 error 并提供只读 Exception 是当前推荐；具体公开表面按 E3 核对实际使用，不预设所有失败 API 必须重写。查询缺失与加载失败应有明确区别，不用兼容外壳掩盖未处理失败；重大行为方向如需改变则先说明并等待确认。

## 6. 与其他系统的边界

### 6.1 不推翻的 1.2 前置契约

- Module 唯一身份、状态、启动去重、注册可见性、启动失败先回滚后通知保持。
- Model/Controller 本地查询、单 owner、精确移除、初始化/退出事务和 long Model ID 保持。
- 唯一关闭管线、逐对象异常继续清理、Closed/注销后汇总、显式 Shutdown 保持。
- 普通 Module 的 Starting 不可 Close，Module 启动请求不提供取消；View 加载取消不等于允许取消 Module 启动。
- Closed 仍只保证逻辑所有权结束，不承诺外部资源释放成功或所有业务字段重置。
- Module 的 CurrentModule 可继续服务既有启动/其他过渡链，但不再是新 View owner 的隐式来源，不为此全面删除或重设计它。

本阶段直接适配限于 View 事务门禁、pending 失效、默认场景显式挂载、关闭快照保护及第 5.6.3 节拟议的内部 Model 注册身份支撑。D1 已确认覆盖 1.2 中保留的本地 Model 自动移除联动，其余 MVC 契约保留。现有 1.2 文档尚未同步这项后续设计变更，本轮仅维护本文件；将来获授权的契约收口在现有入口同步，不另建 MVC 方案。

### 6.2 Panel 最小适配

- 保留 Panel : View，普通 View 不创建 Canvas/EventSystem 或执行全屏布局。
- Panel 的 UIType、必要 Canvas/排序上下文在业务 OnLoaded 前准备；准备成功才提交初始化标志，防止失败后复用跳过准备。
- Order/Queue 登记在相应准备/提交阶段处理并有必要回滚，统一同步/异步直接路径；不顺带重写完整遮挡算法。
- 必要的层级/队列释放由内部直接调用完成，且不为关闭普通 View 懒创建 PanelManager。全局事件只通知。
- 子 View 请求失败、取消或准备异常，Panel 请求必须结束；Panel 的取消/失效不能留下有效子加载继续提交对象。
- 当前 DontDestroyUI 的父节点寿命不改变 Module owner；模块关闭仍结束属于它的 Panel，不因 DDOL 自动转成全局业务对象。

完整窗口目录、单实例显示策略、遮罩、导航、输入适配、不可见状态、层级排序算法及两套 UI 最终收敛留给 Panel/UI 专题。**C1 已确认**：1.3 不修改旧 YL UI Prefab/关联资产，也不通过改变旧 UI 类型的 namespace/程序集/序列化字段间接破坏现有接入；Core Panel 的直接适配不扩展为旧 UI 迁移。后续若必须改变这些资产才能满足拟议方案，应重新调整或另行授权，不能以“接口清理”为由越界。

### 6.3 Events/Pool/IOC/资源

只在 View 自身明确订阅的启停/退出边界，不全面融合 EventManager/EasyEvents、普通对象池/GO 池或 IOC。不得新增 Core -> 整体 YLFramework.Runtime 引用来绕过当前边界。

**C2 已确认**：不将 `UnRegisterTriggerBase.UnRegister()` 的一次调用视为全部清理完成，不将继承 Shared.Abort/仅重写 OnAbort 视为可靠取消。推荐 View 内独立快照清理并逐项 try/继续/汇总，确保必要槽和 owner 在 finally 收尾；Core 请求 override Abort，在调用完成通知前提交自身失效、摘除 pending 和释放输入，旧生产者不能再提交结果。可以复用已有底层原语，但可靠性必须由 View 协议与专项测试证明，不改写 Shared 全局契约或假装外部清理/I/O 成功。

参数池的配对和立即清引用属于本阶段直接修正，不扩展为 ClassObjectPoolManager 全面改造。Prefab 消费者租约、底层取消、失败 Bundle 回滚、缓存时钟和资源格式归属后续专题。

原 XF/XFAB Package 只读；CGJ2024 游戏继续使用原 XF 与现有 YL Audio。新 YL 维持独立类型、程序集、GUID、Resources key 和测试项目名，不共用原游戏 Prefab 作为新 YL View 测试输入。共享临时后端不等于最终独立。

## 7. 分批实施规划

批次依次 A -> B -> C -> D -> E。每批一个主要方向，稳定后有独立提交价值；本文没有提交授权。不人为拆成无验收价值的微任务，也不一次性全改。

### 1.3-A：归属、索引与退出协议

- **目标/范围**：View、ViewManager、Module View 索引与关闭直接调用方；单 owner、会话身份、本地查询、可靠退出、必要异常收尾及已确认 D1。按实际需要选择主要 Model 绑定与注册身份机制，仅做有依据的 Model/ModelManager 直接适配，不要求照搬 token 或全部 API。
- **前置**：D1～D4 整体方向、A～E 规划及 C1/C2 已批准/确认；仍等待用户明确开始 A。启动时核对实际 1.2 基线和调用方，在 A 范围内落实 E1/E3 必要评估，不恢复历史自动移除，不提前实施 B～E。
- **关键契约**：精确注册/移除、重复幂等、owner 在普通 Close 结束、查询歧义、兄弟清理继续、新 owner 不被旧快照清除；Model 解绑不移除，旧绑定不被新注册身份误恢复；保持 Module 唯一关闭顺序。
- **风险/难度**：中高；回调重入、索引同步、缓存新旧会话，以及影响既有 1.2 View 行为断言。
- **验证**：复用 CoreDirectViewPlayModeTests 和 Module/FailurePath 的关闭用例；补跨 owner/重复注册、退出通知和业务退出分别抛异常、多个基础模块都缺失的查询、同 Model 多 View/解绑异常/显式移除及实际支持的重新注册边界。按 D1 更新本地自动移除和关闭精细顺序断言，保留精确移除及一次退出防护；若增加身份字段，不得进入 JSON/序列化数据。按最终 E1/E3 取舍验证，不以销毁或签名变化的数量验收。
- **验收/提交价值**：已有同步 View 能可靠退出且本地索引无残留；错误实例和新会话不误清理。Unity 编译及直接回归通过后可独立提交。

### 1.3-B：同步加载、参数与复用

- **目标/范围**：同步各入口共用 Prefab/类型校验及初始化事务，LoadViewArgs 接管/回收，Loaded 与事件启停，健康缓存与失败实例处理；调整直接测试 fixture 对 Close/Unity hook 的扩展方式。
- **前置**：A 已验收并获 B 实施授权；D3/D4 总体方向已批准。按 E1/E2/E3 确定本批必要取舍，保留 XFAB 后端，不先替换资源系统。
- **关键契约**：临时可见/完成分离、初始化重入拒绝、异常撤销本次会话、参数任何终态一次回收、回池即清引用、事件 Loaded+active 才有效、复用不重复初始化。
- **风险/难度**：高；后端提前激活、inactive parent/对象的 Awake 顺序、用户状态残留、旧泛型 params 路径先加载后转换。
- **验证**：复用直接 Prefab 与命名同步 Fixture；覆盖缺组件/错误 T、BeforeOnLoaded 和两种 OnLoaded 异常、清理附加异常、入口拒绝的参数归还、重复接管不误回收/即时清引用、重复 Enable/Disable，以及最终异常实例策略下的后续加载安全性；独立加载失败/Close 不退出 Model，复用不带旧绑定，业务解绑异常仍尝试框架必要退订。若保留原同步流程，验证其仍满足契约，不为改造流程本身新增测试设施。
- **验收/提交价值**：所有同步入口一致，失败不留下 owner/index/参数使用权，健康对象仍可复用。实际编译及相关 PlayMode/参数 EditMode 通过后可独立提交。

### 1.3-C：场景与层级显式纳管

- **目标/范围**：挂载入口、场景/嵌套 View 登记、Start/Destroy 与场景卸载兜底、默认启动场景的最小直接适配。
- **前置**：A/B 已验收并获 C 实施授权；D2 的显式归属/场景方向已批准，具体层级机制按实际需要确定。保持默认场景 -> OnStart 的 1.2 启动顺序。
- **关键契约**：inactive 覆盖、挂载冲突、根与子会话责任、场景对象不送池、同名 Scene handle 隔离、销毁与逻辑退出幂等、默认场景失败仍复用唯一模块回滚。
- **风险/难度**：中高；inactive 的 Unity 消息不完整、嵌套复用、关闭次序、绑定时点与真实 Scene、动态新增对象。
- **验证**：优先在现有 Harness 下创建测试自有临时 Scene/对象，覆盖普通与同名 Additive、inactive View、嵌套根/子、Close/Destroy/卸载交错、挂载失败、迁入其他 Scene/DDOL 后原场景卸载和模块启动回滚。现有 Smoke 场景有严格结构校验，不随手向其中增加 View 破坏既有 Fixture。
- **验收/提交价值**：Module 查询/关闭真实覆盖受管场景及嵌套对象，不影响其他 Scene/Module，缓存根只归还一次。相关场景/启动回归通过后可独立提交。

### 1.3-D：异步请求与失效收口

- **目标/范围**：LoadViewRequest、Prefab 异步取得路径、Module pending/有效代次小范围支撑、取消/迟到结果及稳定终态。
- **前置**：A～C 已验收并获 D 实施授权；D4 总体方向已批准，按 E2 评估实际异步加载路径。参考现有 StartUp 请求的结果先提交/根因保留，不修改 Shared 全局完成语义。
- **关键契约**：记录早于协程，等待可响应失效，Prefab/owner/parent/Scene 二次检查，旧代次不注册，取消不 Abort 共享底层请求，参数一次收尾，多实例请求不去重，完成后结果不变。
- **风险/难度**：高；下层共享请求和挂起、立即完成、取消后的参数池别名、关闭后同实例重启、目标场景变化及完成回调重入。
- **验证**：复用 LOCAL+Assets 命名 Fixture；实际缺失 Prefab、已报告失败、错误 T、初始化异常、Abort、关闭并立即重启、parent 销毁/目标 Scene 卸载、并行同资源、成功/失败 late callback 和重复终态保护；显式 Model 输入的移除/换实例/同实例重注册/借用 owner 关闭均不接受旧身份，取消和迟到不影响新会话或 Model。直接断言待请求、Model 引用与 owner 残留，不以 Fixture 反射清索引掩盖失败。
- **验收/提交价值**：已定位 Assets-null 路径不再进入 null GameObject pool；Core 自身失败/失效请求结束且不迟到注册。后端始终不完成仍列限制，不伪称已全面修复。实际编译及专项回归通过后可独立提交。

### 1.3-E：Panel 直接适配与阶段收口

- **目标/范围**：Panel 必要上下文/准备标志、PanelManager 内部清理、LoadPanelRequest 的错误/取消传播及直接调用方；不完整融合旧 UI。
- **前置**：A～D 已验收并获 E 实施授权；仅做第 6.2 节直接适配，按真实 API 影响迁移，不修遍完整遮挡栈。
- **关键契约**：加载钩子可取得必要 Panel 上下文，准备失败不提交半完成标志，子请求失败/取消必终结，订单/队列登记释放不依赖广播，正常复用 Canvas 不重复创建。
- **风险/难度**：中；Canvas 准备异常、同步/异步分叉、排序通知异常、取消后子请求继续、UI 回调对旧时序的依赖。
- **验证**：复用 DirectPanel、NamedPanels、Xfab Fixture；补 Canvas/排序准备失败、View 子请求失败、取消/模块关闭、必要登记释放、用户移除监听异常、正常同步/异步缓存复用。定向执行受影响 Core 生命周期与程序集/资源身份检查。
- **验收/提交价值**：View 与直接 Panel 闭环一致，没有 pending/登记/owner 泄漏；没有新增 Core -> 原 XF 或整体旧 YL 依赖；旧 YL UI Prefab/关联资产和序列化接入保持未修改。阶段专项通过后可独立提交，但不据此宣称 1.4 原游戏/Player 门禁完成。

### 7.1 验证共通要求

- 每批检查实际 diff、新文件和必要 .meta，保留 GUID/序列化身份，检查真实调用方；不手改生成工程或原 Package。
- Unity 实际编译是编译依据；静态检查、EditMode、PlayMode、游戏运行和 Player 构建分别报告。
- 复用 CoreDirectViewPlayModeTests、CoreXfabPlayModeTests、CoreModulePlayModeTests、CoreFailurePathPlayModeTests、CoreLoadViewArgsTests、CoreIdentityTests 与既有 Harness/项目 Fixture，按影响选择子集。
- 新增测试聚焦关键新契约/确认缺陷，旧断言仅因获批行为改变而调整。保留身份、借用、精确移除和一次退出防护。
- CoreClassPoolScope 不得重新反射摘除注册表来制造通过；View/pending/参数关键断言先于 Fixture 恢复，清理只处理测试自有对象/条目。
- 涉及 Shared 直接行为变化时才扩大最小 Foundation 回归；默认不重复全量 Editor、Foundation、Audio 或 Player。本方案不要求修改 Shared。
- 1.3 验证逻辑所有权、请求、实例归还及直接场景闭环，不承诺真实 AB 物理释放。若实际后端路径变化产生新风险，定向增加 AB 验证或明确移交，不能用 Assets 通过代替。
- 1.4 负责完整核心集成、原游戏启动/菜单/设置/进入退出再进入/音频非干扰及按风险的 Windows64 Player；最终无原 XF 生态的独立验证仍属后续收口。

## 8. 已批准方向与开放评估原则

### 8.1 已确认设计约束

| ID | 用户已确认内容 | 状态边界 |
|---|---|---|
| D1 | View.Close 默认仅结束表现会话，不隐式移除/销毁 Model；Model 属于其 Module，业务可显式请求移除；Module 保留唯一整体清理契约 | 持续约束；Runtime 尚未实施，具体绑定 API/身份实现自主评估 |
| C1 | 保护旧 YL UI 的真实序列化资产接入，1.3 不修改这些资产 | 约束所有批次；不等于批准迁移旧 UI 类型/资源或完整融合 UI |
| C2 | 现有订阅清理和 Shared Abort 不直接构成可靠 View 退出/取消协议 | 必须独立保证必要收尾/终态；不等于批准修改 Shared/完整 Events 系统 |

### 8.2 已原则上批准的总体方向与批次规划

用户已确认采用 D1～D4 总体方向，原则上批准 A～E 规划。下表记录架构目标，而不是将草案接口/内部机制逐条锁定。第 5 节提供实施参考，普通内部取舍在获授权批次内依据真实源码自主确定；现有实现已满足需要或调整收益不足时，可以保留并简述证据。

| ID | 方向 | 批准的总体目标 | 实现边界 |
|---|---|---|---|
| D2 | owner、查询与场景受管范围 | 显式归属、本地查询/清理、资源查找与业务借用分离、有效模块门禁 | 不改 M/C 装配；挂载 API、索引组织和具体场景/层级实现按实际依赖确定 |
| D3 | 生命周期、绑定与错误 | 可靠唯一退出、绑定不取得 Model 所有权、清晰错误和稳定异步终态、受控重入 | 不预设所有签名/getter 必须改造，不强制某个 token 类型/绑定容器；定向适配真实依赖，不重设计 MVC |
| D4 | 临时后端、激活与取消 | 保留临时 XFAB 资源边界，业务上下文与 Unity 消息分清，取消/失效不允许迟到复活 | 两段式、销毁、隔离或额外管理均不是强制做法；不承诺底层物理取消、即时释放或默认引入超时/staging |

A～E 顺序与主要职责保持：A 归属/退出，B 同步加载/复用，C 场景/层级，D 异步，E Panel 直接适配/收口。具体实现和验证应随实际取舍保持一致，不把推荐技术手段当验收目标。重大方向如需改变，先说明依据并等待确认；普通实现取舍不逐项重复申请批准。

### 8.3 三项开放技术评估点

以下由用户明确指定为**技术评估点，不是强制实施要求**。本轮仅同步原则，不将尚未开展的批次评估写成已完成结论。正文的直接销毁、两段式流程或 API 表只是候选，若与本节产生机械执行上的冲突，以本节为准。

| ID | 评估点 | 必要依据与比较 | 选择边界与验收 |
|---|---|---|---|
| E1 | 异常 View 实例与 XFAB 池 | 沿实际实例登记、allObjs/映射、IsInUse、归还、销毁识别、清理时机和池关闭核查；区分业务观察者异常、可恢复准备失败和真正不完整状态。比较现有归还/后续重新初始化、必要销毁及确有需要的其他处理 | 不预设必须 Destroy 或建立额外管理器；也不假定直接 Destroy 已移除后端登记。若无实质风险或现状足够则保留；选定方式应结束逻辑会话、避免错误复用并说明后端仍未保证的范围，不修改原 Package 或共享全局策略 |
| E2 | Prefab 加载流程 | 分别比较现有同步/异步路径、缓存命中、fallback、类型/空结果检查、取消/晚到窗口和资产所有权；两段式的已知收益是实例化前校验及规避特定 null-pool 路径，同时评估重复加载/额外步骤与持有影响 | 不强制同步/异步采用同一路径或两段式，可只改必要路径，也可保留现状；不得把已确认的 Core 失败收口问题因保留流程而当已解决。选择按真实问题、收益和专项证据说明，不借机替换整个资源后端 |
| E3 | 公开 API 与调用方 | 按实际改动定向核对入口/重载、Close/getter override、直接调用、真实 Prefab/UnityEvent/必要字符串引用；区分原游戏 XF 路径、新 YL 及测试 fixture | 不预设所有 API 改造；不为没有真实依赖的旧接口做兼容层。需要迁移则明确实际对象/资产影响及直接回归，未受影响接口可以保留。C1 始终约束旧 YL UI 资产不被修改或间接破坏 |

跨批次分工：A 优先评估退出相关 E1/E3；B 评估同步加载与复用 E1/E2/E3；D 补异步路径 E2/E3；C/E 按自身真实调用和层级/Panel 影响定向补查。复用仍有效的已得证据，不每批重新全面审计。结论简要增量写在对应实现/验收范围内，不创建额外报告；保留现状也是有效结论，但须能解释满足契约或调整收益不足的依据。

最终取舍以正确性、实用性、性能和长期可维护性为优先，不为形式统一、测试数量或假设风险引入通用容器、复杂状态机或资源管理机制。整体批准不自动授权 Git；当前仍仅允许本文更新，等待开始 A。

## 9. 未验证事项与实施就绪条件

### 9.1 未验证

- 拟议代码的 Unity 编译和全部新增/调整测试，本轮尚未实现。
- Loaded 后事件开放、inactive 场景对象、嵌套 View 与池复用的完整 Unity 时序。
- 模块关闭/回滚/同实例重启、Abort、目标场景失效和共享底层请求交错的运行表现。
- 受控 Model 槽、解绑异常、注册 token 和同步/异步身份检查，以及新增 API 对 getter override/调用方的迁移；这些机制尚未实现，不能以 D1 已确认代替技术验证。
- 新同步异常/固定 Close API 对真实调用方、资产、UnityEvent、反射字符串的影响；定向源码搜索未见足以证明不存在动态依赖的证据。
- 真实 AB/Player 下资源保留和释放，加载失败后 Bundle 常驻情况，以及物理 I/O 取消。
- 原游戏完整非干扰、原 LoggerManager 退出残留及最终移除 XF 生态后的独立运行。

当前 Core 测试已经表征成功同步/异步、参数、事件启停、缓存、多实例、Module 关闭异常继续及新 owner 防护；现有场景 Fixture 主要是普通入口 MonoBehaviour，不包含真正场景 View。未发现旧 YL UI/IOC/GO 池/YL Resource 的同等生命周期行为覆盖，定向未见不等于工程任何位置均不存在。Foundation 的 Abort 等表征测试不应被当成 View 已有真正取消的证明。

### 9.2 条件判断

**技术前置基本具备**：1.2 已落地，相关真实源码/直接链与关键缺陷已定位，现有测试/Fixture 可复用，方案无需写原 Package 或先实施整套资源/UI 融合。

**整体设计已批准，等待开始 1.3-A**：D1～D4 方向及 A～E 规划已原则上批准，D1/C1/C2 继续约束全部批次。具体 API/内部机制按实际源码及 E1～E3 自主评估，不将草案候选强制化；重大方向变更仍先说明并等待确认。尚未实施或技术验收，本次仅增量维护本文，不执行任何 Git 操作或 Runtime/Editor/测试/资源修改。后续按用户明确开始的批次推进，必要时在获授权范围同步现有长期蓝图/1.2 局部契约，不新增重复计划。

达到 1.3 只意味着通用 View 与直接 Panel 核心闭环通过专项验收；不意味着完整 UI/Pool/Events/资源融合、原游戏非干扰、Player 或最终独立已经完成。

## 10. 1.3-A 实施取舍与阶段边界

本节记录获授权 A 批的实际实现，覆盖前文研究基线中的“尚未实施/等待开始 A”表述，不把整体设计批准等同于 A 验收。开始时已实际确认 `fusion/1.3-view`，工作区干净，指定约定及三份设计文档均已跟踪；未切换分支或执行提交、推送、合并及清理。

### 10.1 归属、索引与会话

- Module 新增内部 `ownedViews` 实例 ID 权威索引，资源名及实际类型为辅助索引；无资源名的内部注册不编造名字。注册仅接受 ModuleManager 精确注册的 Starting/Running owner，不改变 M/C 注册前装配。
- View 保存内部引用型 `Session` 与注册时实例 ID；合法已注册实例重复 Add 幂等，跨 owner、键冲突、辅助索引不一致或生命周期重入明确拒绝。移除核对 owner、权威实例和会话，辅助索引仅按精确实例删除，并移除空 bucket。
- GetView/GetViews/ClearView 仅访问当前 owner；跨模块调用真实 owner 的入口。类型单查询按实际类型且多匹配抛错，集合查询包含派生类型，名称/ID 泛型单查询拒绝错误类型，`GetView<T>(id)` 不再猜资源名。集合是独立结果，NonAlloc 保留；查询发现销毁实例时按保存身份结束失效会话。
- Module 关闭及 ClearView 捕获 `(View, Session)` 快照，不让旧操作退出已重新注册的新会话。原始 View 引用代表可复用组件，`Close()` 针对其当前会话，不是外部不可变会话句柄；内部旧事务必须使用捕获身份，不能重读新 Session。
- 删除 Module getter/Start 的隐式 CurrentModule/父节点 owner 推断和未索引初始化。场景及嵌套对象显式挂载 API、默认场景接入和 inactive/sceneUnloaded 兜底仍由 C 实施，本批不提供“场景 View 已完整纳管”的承诺。

### 10.2 唯一退出与 Model 分离

- `View.Close()` 改为不可覆写入口；业务结束扩展使用 `OnRemove()`，新增 protected `OnClosed()` 作为退出和实例处理提交后的可选完成钩子。若移除广播已开启过新会话，按内部 LastSession 身份跳过旧 OnClosed，连“重装后立即关闭”也不再派发旧通知。重入 Close 幂等，初始化/显式解绑期 Close 拒绝，必要退出期不能新增绑定或重新注册。
- 独立尝试框架事件退订、每个 `onViewRemove` 观察者、OnRemove、主要 Model 解绑和业务解绑钩子、Panel 必要登记释放、索引/owner/会话引用撤销、实例处理，之后才广播和调用 OnClosed。单错误保留原异常，多错误聚合；Module 在 Closed/注销后汇总，兄弟对象继续清理。onViewRemove 在退出收尾清空，Awake 声明的实例事件定义保留。
- Module 保持 OnClose → Controller → View → 剩余 Model；View 初始化/退出加入已有对象事务门禁，完成通知不在必要清理门禁内。InitView 仅做维持 A 会话协议所需的初始化标记、失败结束会话及已有 args 的 finally 配对，不声称已实现 B 的完整取得/类型/参数接管事务。
- `View.Model` 改为框架只读主要槽，新增 protected BindModel/UnbindModel/OnModelUnbound。绑定仅借用已完成注册的确切 Model，可跨 owner；解绑先清槽再调用业务钩子，不移除 Model。显式解绑也有 View 事务门禁，清理失败的污染标记保持到退出，不能因业务捕获异常或重新绑定而误归健康缓存。Model 被显式移除后 getter 无副作用返回 null，多个 View 不互相结束数据。
- Model/ModelManager 仅增加内部引用型注册身份，每次新注册分配、退出/回滚清除，同实例重复 Add 不改变；旧绑定不能因为相同对象/owner/ID 再注册而恢复。未增加 Model→View 图、引用计数、自动监听解除或加载入口 model 参数；请求接受时捕获 Model 意图仍由后续 B/D 按实际入口实现。

### 10.3 E1/E3 与最小 Panel 适配

- E1 已核对实际 XFAB allObjs、实例映射、IsInUse、Load/UnLoad、死实例扫描与池关闭。健康退出（包括纯观察者抛错）沿原 UnLoad 归还；OnRemove/业务解绑/必要框架退订失败及本批已临时注册的初始化失败，停用并 Destroy 自己取得的实例，避免将不完整状态标成可复用。未修改后端池或共享策略，未新增隔离管理器。
- Destroy 不即时移除 XFAB allObjs/映射或卸载 Prefab；后续 Load/自动池扫描可清死实例，非自动卸载池的周期清理存在原有边界。外部业务解绑失败仍可能残留监听，销毁不代替解绑或保证所有资源已释放。
- E3 定向核对新 YL override、CloseView、Panel、Editor 模板及资源文本；新 Core Close/Model override 仅实际命中 smoke Fixture，迁移至受控槽/OnClosed。原游戏 Close override 和持久 UnityEvent 明确属于原 XF 类型，不迁移；旧 YL UI 资产、namespace、程序集及序列化字段未改。
- PanelManager 仅做 A 退出必需的直接适配：取消必要全局广播订阅，ReleaseView 只访问已有 manager，先清 order/queue/current，再尝试恢复前一有效 Panel 的 Visible。普通 View 退出不懒创建 UI；不重做 Panel 加载准备、排序/遮挡策略及请求终态，完整适配仍为 E。

### 10.4 验证与后续

本批实际使用 Unity `2022.3.62f3c1` 刷新、编译并反射核对新 Close/Model API；最终 `isCompiling=false`、`scriptCompilationFailed=false`，测试结束后普通/基础 Module 注册均为空。复用现有 PlayMode Fixture：CoreDirectViewPlayModeTests **24/24**（含14项新增风险回归）、CoreModulePlayModeTests **28/28**、CoreFailurePathPlayModeTests **13/13**、CoreXfabPlayModeTests **12/12**，共 **77项**专项通过，无失败或跳过；工具的全工程发现数量不当作执行数量。

已验证单 owner/重复与冲突、本地查询及基础模块隔离、精确和旧会话移除、共享 Model/显式移除/同实例重新注册隔离、退出重入、显式解绑失败污染、逐观察者与兄弟异常继续、Module 顺序/回滚/新 owner 保护、普通缓存复用与脏实例拒绝复用、已销毁及从未激活的 wrapper 按保存身份清理、Panel 必要登记释放不依赖广播和开关。销毁回归断言先于 Fixture 后端条目恢复，不通过清理工具修正 Core 索引。

独立只读审查发现的旧完成钩子跨会话派发、显式解绑污染/重入两项问题已修正并加入实际通过的回归；最终审查无未解决的 A 范围实质问题。`git diff --check` 通过，没有新增文件或需要补充的 meta。退出 PlayMode 仍观察到原 LoggerManager 场景关闭残留，与长期蓝图已记录问题一致；XFAB 订阅者异常用例按既有 LogAssert 预期记录，不把专项通过表述为全工程 Console 无错误。

**具备进入 B 的核心技术条件，仍须先由用户验收 A 并授权 B。**本批未提交，不自动进入 B；没有机械执行全量 Editor/Foundation/Audio/XFAB/Player 测试。真实 AB 物理释放、外部业务监听清理失败后的资源状态及原游戏完整非干扰仍未验证。

B 仍负责完整同步加载/LoadViewArgs 所有权、入口失败收尾、泛型类型统一、缓存初始化与 Unity 消息扩展；C 负责场景/层级/挂载；D 负责 pending、取消、代次及迟到结果；E 负责完整 Panel 直接路径和阶段收口。未修改原 Package、游戏、旧 UI 资产、Shared、程序集或资源后端，也未运行 1.4 游戏/Player 或最终独立门禁。

## 11. 1.3-B 实施取舍与阶段边界

本节覆盖历史研究中的 B 尚未实施表述。依据用户本次确认，A 已验收并完成 Git 收口；开始 B 时实际核对 `fusion/1.3-view`、干净工作区及 A 提交 `22b281d`。本批未获 Git 写入授权，不切换分支或自动提交、推送、合并及清理。

### 11.1 同步加载与初始化事务

- Module 的命名、直接 Prefab、泛型/非泛型、旧 `params object[]` 与 `LoadViewArgs` 同步 View 入口共用加载事务。保留原公开重载，旧泛型不再先初始化任意 View 后 `as T`。同步成功只返回 Loaded 实例；无效输入、缺资源/组件、不兼容 T 及业务错误直接抛出，不用日志加 null 留下半完成注册。
- 命名查找捕获请求项目及有效基础模块项目，按 ModuleManager 已有注册顺序去重；基础模块身份采用注册表，不重新调用可变的 BaseModule getter。资产缺失或资源取得错误可尝试下一候选；取得 Prefab 后类型错误及业务初始化失败不继续 fallback。最终资源错误保留最后取得异常为 InnerException，不能屏蔽 XFAB 底层自身日志。
- E2 选择同步两段式：公开 `AssetBundleManager.LoadAsset<GameObject>` 取得 Prefab，验证根 View 可赋给 T，再进入原 `GameObjectLoader.Load(prefab,parent)` 池。XFAB 没有 `TryLoadAsset` API；原同步命名路径本身已有 null 检查，调整收益是统一实例化前校验，不是修复同步 null-pool。冷路径与命中路径都不重复进行第二次命名资产加载。
- 取得实例后再次验证存活及根组件，防缓存对象被业务改变。后端取得阶段也纳入同步对象事务门禁，Awake/提前激活不能合法关闭正在加载的 owner；不全面禁止初始化其他对象。
- 临时注册 -> BeforeOnLoaded -> 两种 OnLoaded -> 核对会话 -> Loaded -> 活跃时开放表现事件。各业务边界和事件开放之后复核实例、会话及主要 Model 注册身份；最后钩子销毁组件或旧绑定身份已结束时不提交成功。临时查询可见但 IsLoaded=false，重复初始化、Close、Module.Close/Shutdown 拒绝；已完成会话重复 Add 保持 A 幂等。未提供新的 model 输入参数，业务仍可在加载回调中显式 BindModel。
- 注册前失败不派发业务 OnRemove；临时注册后异常沿 A 的退出管线尝试 OnRemove、解绑、必要退订、索引及实例清理，不派发成功会话移除通知或 OnClosed。抛回原始异常，额外清理错误关联 `Data["YLFramework.Core.CleanupException"]`，不隐式移除 Model。

### 11.2 LoadViewArgs 的所有权

- 沿用现有 ClassObjectPoolManager，仅局部增加 Claim/Release 及 BeginRead/EndRead。入口先 Claim，再校验 owner、参数及资源；接管成功后拒绝、失败、成功均由本次操作 finally Release 一次。重复接管失败不进入第二笔归还，也不清第一笔数据。未传容器时只在必要业务参数回调前租用默认空容器，不为入口拒绝预分配。
- New 后由调用方填充和读取；接管后不可修改，只有 OnLoaded(LoadViewArgs) 期间可借读。退出回调及 Loaded 事件钩子不能继续读取参数；成功/失败终态立即清七类字典及所有权状态再归还。回池后 Get/Set/Claim 抛 ObjectDisposedException，不等待下次租用才清引用。
- IReuseable 生命周期变为显式接口实现，池回调不能重置活跃容器或复活已回收引用；移除了 Fixture 手工 OnLoaded reset。直接通过通用 Pool 管理此类型不受支持，不借本批修改通用池的非原子异常语义。
- 参数限定主线程 New、同线程接管/读取/归还；局部线程身份检查拒绝跨线程误用，不宣称通用池线程安全。不为每笔参数分配独立 token：同一个原始池对象重新 New 后无法识别非法旧别名，因此业务必须提取所需值，不得保存容器或跨异步访问它。
- Panel 的现代同步直接入口在解析 parent 前接管，并将已接管参数交给 View 内部入口，最终由 Panel 入口归还；未重构完整 Panel 准备/队列事务。异步等待期的接管、取消、失败与迟到参数使用权仍由 D 完成，不能把同步结果解释为异步入口已收口。

### 11.3 缓存、Unity 消息与 E1/E3

- 新增 protected OnViewEnabled/OnViewDisabled 业务钩子，Loaded 且 isActiveAndEnabled 时幂等开放；Disable 只暂停表现事件，不结束 owner、会话或 Model。私有 OnEnable/OnDisable/OnDestroy 为框架消息，派生类不得隐藏；从现有 override 迁移到业务钩子的真实新 Core 调用方只有 smoke Fixture。业务无需调用 base 才完成必要退订。事件启停捕获会话及单调订阅轮次，钩子重入后旧异常不退订或污染新会话/新轮次。
- Awake 仍用于声明实例级事件及本地组件准备，OnLoaded 负责每次表现会话的初始化。XFAB 返回前可能已触发 Awake/OnEnable；inactive parent 下 Loaded 甚至可先于 Awake。本批不引入 staging root，不承诺原始 Unity OnEnable 已有 owner/参数，不假定 Start 或 Awake 是框架初始化前置。
- 初次加载 inactive 对象可完成框架初始化而不开放事件；后续激活的 Awake 声明仍由 OnEnable 订阅。正常复用再次执行加载钩子，而 Awake/实例事件定义保留；关闭先清主要 Model、onViewRemove、会话及事件使用权，新会话不恢复旧绑定/旧参数。不反射重置业务自定义字段、协程或额外外部监听。
- E1 沿用 A 的污染策略。健康退出及纯观察者错误归还；初始化失败、实际缓存组件失效或必要退订/业务清理失败，停用并 Destroy 本次取得实例，**不先 UnLoad**，使其在延迟销毁前仍是 IsInUse=true，同帧新加载不会取得待销毁对象。后续 XFAB Load/扫描识别死对象，不新增隔离管理器或修改 Package。
- 直接 Destroy 不即时删后端 allObjs/映射，不代表 Prefab/Bundle 立即释放；非自动卸载池的周期整理限制保留。共享资产不是每消费者租约，类型失败及实例失败均不擅自 UnloadAsset(prefab)，不更改全局池策略或共享所有权。
- E3 保留全部公开 LoadView 重载，新增业务 enable/disable 钩子，参数池 reset 不再是公开普通方法；直接调用方和测试已按真实使用适配。未改变 View/Panel 类型身份、程序集、序列化字段、GUID、旧 YL UI Prefab 或游戏使用的原 XF 类型。

### 11.4 验证与后续

本批实际执行 Unity `2022.3.62f3c1` 脚本导入及编译，最终 `isCompiling=false`、`scriptCompilationFailed=false`，反射确认专用业务钩子和私有框架消息已加载、参数不再公开普通 reset。测试结束后普通及基础 Module 注册均为零。

| 实际执行套件 | 结果 |
|---|---|
| CoreLoadViewArgsTests，EditMode | 10/10 |
| CoreDirectViewPlayModeTests | 34/34 |
| CoreXfabPlayModeTests，LOCAL + Assets | 14/14 |
| CoreModulePlayModeTests | 28/28 |
| CoreFailurePathPlayModeTests | 13/13 |

共 **89 项 PlayMode + 10 项 EditMode** 通过，无失败或跳过；工具的全工程发现数量不计入实际执行。旧泛型不匹配、旧事件钩子影响新会话、首次事件钩子销毁组件三条回归均在修正前实际失败、修正后通过。覆盖入口拒绝及即时清引用、重复接管保护、框架准备/两种业务回调/首次事件开放失败、额外清理错误、临时查询与重入、inactive parent 首次 Awake/事件、健康及污染缓存同帧复用、实际缓存组件再校验、Model 注册身份变更、真实命名冷加载/错误类型/缺失资源/基础项目 fallback/参数及会话隔离，以及 A 的 owner/退出和 Module 关闭回滚回归。

独立只读审查发现的事件异常跨会话/轮次收尾、首次事件钩子后的最终存活检查已修正并加入实际回归；修正后的定向独立复核未发现新的实质问题。`git diff --check` 通过，无新增文件或 .meta/GUID 变更。退出 PlayMode 仍观察到已记录的原 LoggerManager 场景关闭残留，不将专项通过解释为全工程 Console 无错误，也未添加日志忽略来制造通过。Unity 专项结果只证明受影响 Core/LOCAL + Assets 链路，不代表真实 AB 物理释放、原游戏全流程、Player 或最终无 XF 生态独立运行。

**已具备进入 1.3-C 的核心技术条件，仍须用户验收 B 并授权 C。**本批未提交，不自动推进 C。

C 仍负责显式场景/层级挂载、场景卸载及完整销毁兜底；D 负责异步接管、pending、取消、跨重启/迟到隔离与终态；E 负责 Panel 必要上下文、后置准备失败及请求完整收口。后端在 Load 返回前抛错且不提供实例引用时，Core 不能保证清理全部后端半登记状态；原后端资源专题仍负责这一限制。
