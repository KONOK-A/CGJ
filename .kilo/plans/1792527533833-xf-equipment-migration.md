# XF EquipmentSystem 迁入 YL Implementation Plan

> **当前状态：EquipmentSystem 技术验收及用户验收均已通过（2026-10-21）。** 本轮为最终交付持久归档及中文 Commit/普通 Push 收口，不再开发、重新导出或重跑技术验收。稳定交付入口与 Git 交接见 §7；实际 Commit SHA 和远端结果写入外部收据，避免文档自引用的额外提交。

**Goal:** 将 XF EquipmentSystem 的装备运行时、配置作者工具和可选 Excel 工作流完整承接到 YL，并通过无 XF 的独立交付和 Windows64 双后端运行验收。

**Architecture:** 保留 `EquipmentManager + IEquipment/IEquippable` 主模型及 Asset/Script 双创建路径，成熟领域流程局部适配，不改成多实例 Service/Handle 系统。Prefab 使用既有 Resource 实例租约，CLR 装备使用既有 ObjectPool；装备域只记录配置、实例来源和挂载责任，不成为第二个资源或池权威。

**Tech Stack:** Unity `2022.3.62f3c1`、现有 `YLFramework.Runtime` / `YLFramework.Editor`、Resource、Foundation Pool/Session、现有可选 Excel/DomainExcel。无新增第三方依赖。

**Spec:** 用户在本轮确认的六项决定见下；共同约束参照 `Assets/YLFramework/Documentation/IntegrationPlan.md` §1、§4 和 `Architecture.md`。这是原 13 项之外的新增专项，不改写前批范围或验收历史。

## Global Constraints

- 已确认：完整迁移已有能力及作者工具；主模型局部适配；仅新 YL 格式；同批保留可选 Excel；按来源区分装备所有权；完整独立验收。
- 只改本专项必要的 YL 源码、测试、meta 和现有说明。原 XF 包/PackageCache、原游戏、MainModule/StreamingAssets、Packages/ProjectSettings、工具配置和用户现有改动保持；不重建原游戏 Bundle。
- 已确认门禁所需的后端、选配宏和 Enter Play 设置仅作临时变更：先捕获原字节、内存状态/dirty、场景及稀疏平台配置，再按所有权恢复；有冲突先停，不覆盖用户新修改。主工程不执行 Package Manager 更新，独立接收工程另核必要依赖。
- 新 namespace 为 `YLFramework.EquipmentSystem`，新复制文件由 Unity 生成新 GUID。沿用两个产品程序集，不新建产品 asmdef，不恢复 `IReuseable`、`ClassObjectPoolManager`、旧 API 转发壳或 XF 数据转换器。
- 不增加背包、槽位、数量/堆叠、合并拆分、装备存档、全局装备事件、自动 Skill/Buff 联动。原包没有这些能力；不因此修改 Save、Skill/Buff/Mission 或共享 Core/Resource 契约。
- 源码修复有直接证据的边界缺陷；未实际复现的源码风险不写成已复现。验证按影响完成，不重复无关历史门禁，不弱化既有断言/白名单。
- 初始规划不自动授予 Git 权限。用户于2026-10-21明确验收并授权本批最终归档、文档状态收口、一个中文Commit及普通Push到origin/integration/xf-yl；不授权新增开发、改写历史或接管其他dirty。

## Review Focus

- 同一装备实例跨宿主、回调重入与中途关闭：始终只有一个挂载归属，旧回调不能操作已经回池再借出的实例。归 T2。
- 未装备的创建结果、创建/回调失败及已销毁 Unity 对象：仍能准确归还自有租约，不漏掉 detached 实例或错误处理中的后续清理。归 T2/T5。
- 外部借用装备及其他 owner 的配置/资源：只解除本域挂载，不误销毁、回池或释放独立租约。归 T2/T5。
- 列表等长替换、ID 改动、配表改名、脚本缺失和外部引用资产：查询不陈旧，作者工具不隐式写盘或越权删文件。归 T1/T3/T4。
- IL2CPP 字符串类型创建、默认不装 Excel 的首导入及选配隔离：不能靠主工程旧 XF、测试 DLL 或预编译 YL 产物补齐依赖。归 T4/T5。

## 1. 已核实基线

- 研究时主目录 `G:/Unity/Projects/CGJ2024`，分支 `integration/xf-yl`，HEAD `be3ea8faf612b563d2256ff6fff19b6e64aeb044`。已有未提交 Plan、`.mcp.json`、三份游戏 FSM、manifest/lock、`Assets/Test/` 和 `AlternateContent.meta`；不接管或覆盖。实施前重新记录现场，不将此快照冒充干净基线。
- 来源为 `com.xfkj.xfgameframework.equipmentsystem` **1.1.9**，revision `48952da775e2fc2c8459c404ad80b3eab8a1c205`，URL `https://gitee.com/xianfengkeji/xfgame-framework.equipment-system.git`；只读源目录 `Library/PackageCache/com.xfkj.xfgameframework.equipmentsystem@48952da775/`。
- 核心源为 Runtime 五文件、Editor 七文件及 `Runtime/Attribute/` 六个 Editor Drawer。MIT / `Copyright (c) 2024 弦风课堂`；不复制原 meta、logo、安装器、包依赖声明、截图或旧 EPPlus。
- Assets 源码/序列化文本、来源 GUID、MainModule asmdef 及可读 Bundle 清单未发现该包消费者或配表。游戏 `EquipBase/WeaponBase` 是另一套 View 玩法类；不迁移它们。未解包二进制 Bundle，也不外推仓库外不存在消费者；用户已选择不提供旧格式兼容。
- 已核实风险：创建回调早于配置赋值、跨宿主重复挂载、失败实例泄漏、卸下异常中断后续清理、fake-null 残留、可变配表名与缓存、读取列表时扫描并 SaveAssets、删除根时无所有权检查。来源 Runtime 还混入 Editor 依赖；这只是源码/结构证据，不是本轮 Player 失败实测。
- 关键来源定位：`Runtime/EquipmentManager.cs:201-222,245-301,341-433`（挂载/卸下/创建与回收），`Runtime/EquipmentsConfig.cs:44-121`（getter/缓存），`Editor/Other/ListenerFileEvent.cs:7-25`（删除）。`DidNotDelete` 本身不是阻止删除，问题是无归属检查的连带删除，不能按错误 API 解释修复。

## 2. 冻结合同

### 2.1 类型与入口

以下合同不依赖旧 XF 类型；保留有实际用途的领域回调，并非恢复通用池兼容接口。

```csharp
namespace YLFramework.EquipmentSystem
{
    public enum EquipmentType { Asset = 0, Script = 1 }
    public enum EquipState { Success = 0, CannotEquip = 1, AlreadyEquipped = 2 }
    public interface IEquipment
    {
        IEquippable Equiper { get; set; }
        EquipmentConfigData EquipmentData { get; set; }
        void OnLoaded();
        void OnUnloaded();
        void OnEquip();
        void OnUnEquip();
    }
    public interface IEquippable
    {
        List<IEquipment> Equipments { get; }
        bool IsCanEquip(IEquipment equipment);
    }
}
```

`EquipmentManager` 为静态入口，保留以下签名：

- `void AddConfig(EquipmentsConfig config)`；`void RemoveConfig(string name)` / `RemoveConfig(EquipmentsConfig config)`；`EquipmentsConfig GetConfig(string name)`；`EquipmentConfigData GetEquipmentInfo(int id)`。
- `IEquipment CreateEquipment(int equipmentId)`；`IEquipment CreateAndEquip(IEquippable equipper, int equipmentId)`；`EquipState Equip(IEquippable equipper, IEquipment equipment)`。
- `void UnEquip(IEquipment equipment, bool destroy = true)`；`void UnEquipAll(IEquippable equipper, bool destroy = true)`；`void DestroyEquipment(IEquipment equipment)`。
- 新增必要边界：`void LoadConfig(ResourceSystem resources, ResourceAddress address)`、`void CloseHost(IEquippable equipper)`、`void Shutdown()`。
- 不承接来源两个 `[Obsolete] UnEquip` 重载；不增加 async 创建、通用服务注册器或公开租约包装层。

### 2.2 数据、资源与所有权

| 项 | 确定行为 |
|---|---|
| 配表 | 保留 `EquipmentsConfig` 的 `displayName/start_id/type_script_guid`、私有序列化 `equipments` 列表及公开 `Equipments/GetEquipmentConfigData/ClearCache`。getter 只读返回列表，不扫描资产、不写盘。 |
| 装备定义 | 保留 `EquipmentConfigData` 的 `parent/id/equipment_name/icon/description/equipmentType/equipment_class_full_name/equipment_class_guid/projectName/assetName` 及 IconInfo 布局。定义只读共享，实例状态放实现类；Manager 不克隆/写回定义。登记期间不支持任意修改定义，需先移除后编辑再登记。 |
| 缓存 | 保留字典查询，但核对列表条目引用及 ID 内容快照，覆盖等长替换/改 ID；先完整校验再发布缓存，重复诊断给出真正冲突 ID/行。作者操作仍显式 ClearCache。 |
| 资源身份 | `projectName/assetName` 解释为 YL `ResourceAddress(ProjectId, Path)`；图标同理，不查询旧 XFAB 项目。Asset 创建借用已初始化的 `ResourceSystem.Current`，没有时明确失败，不暗中初始化服务。 |
| 根类型登记 | 应用在 ResourceProjectMount.assetTypes / ResourceLocalSnapshot.CreateCatalog 中显式提供 `typeof(YLFramework.EquipmentSystem.EquipmentsConfig)`；直接加载的自定义根/条目同时提供具体类型。此登记不代替 Script 装备类的 IL2CPP 定向保留，不改 Resource 默认类型表。 |
| 资产布局 | 根配表由用户选择保存位置；作者工具创建的独立条目放 `Assets/YLFrameworkData/Equipment/<根GUID>/`，保留 parent/列表双向关联，不强制继承 DataAssets。无 `excel_path` 或旧路径迁移器。 |
| 配置责任 | AddConfig 借用根；同一引用重复登记幂等，同名不同根/同表或跨表重复 ID/空项/非法类型拒绝且不留下部分登记。LoadConfig 持有自己的根 AssetLease；重复加载不累积租约。登记名固定，按引用移除不依赖后来变化的 asset.name。 |
| 自有实例 | CreateEquipment 的 Prefab 直接消费 ResourceSystem.Instantiate 的 InstanceLease，CLR 按具体 Type 使用 ObjectPool<IEquipment>。私有记录保存原始来源、配置和归还路径，不能用可变 EquipmentData 猜测回收目标。所有创建结果包含未装备/已卸下未回收者，均受 Shutdown/RemoveConfig 管理。 |
| 借用实例 | 直接 Equip 的外部对象只登记挂载，不调用 OnLoaded/OnUnloaded，不销毁或送入自有池。UnEquip/CloseHost/Shutdown 仅解绑；destroy=true 也只回收自有实例。DestroyEquipment 对非本 Manager 创建的存活实例报 InvalidOperationException，不接管其生命周期。外部 owner 自行保活其配置/资源，不能把借用挂载视为取得根 Lease。 |
| 宿主 | List 由宿主初始化并稳定持有；挂载期间列表与 Equiper 由 Manager 维护，调用方通过 API 修改。Unity/CLR 宿主都在自身结束时显式 CloseHost；Module.Close、停用和场景切换不会自动管理任意外部系统。不增加逐帧装备扫描驱动。 |

### 2.3 回调、失败与关闭

- 创建先登记清理责任并设置 `EquipmentData`、`Equiper=null`，再调用 OnLoaded；这是领域回调顺序，不承诺早于 Prefab 的原生 Awake/OnEnable。Script 类型必须是实现 IEquipment 的非抽象、非开放泛型、非 UnityEngine.Object 普通类且有 public 无参构造；Asset 必须在根节点存在唯一装备组件，不搜索子树。
- 同一个实例再次 Equip（同宿主或跨宿主）返回 AlreadyEquipped，绝不改原归属；同配置 ID 的不同实例允许同时装备，不新增按 ID 唯一限制。IsCanEquip=false 返回 CannotEquip，列表和回调不变。
- OnEquip 观察到已经设置的 Equiper/列表；失败时尝试补偿 OnUnEquip，始终撤销本次挂载，再传播原异常及额外清理异常。不能宣称回滚任意业务外部副作用。
- 卸下先移除列表项，OnUnEquip 中仍可观察原 Equiper，finally 清空它；自有回收在 OnUnloaded 后清空配置/运行关联，再 Return/Recycle。未挂载或已回收对象重复卸下/销毁不重复回调；null 销毁为 no-op。
- CreateAndEquip 正常拒绝时回收本次创建结果并返回 null；配置、类型或回调异常时先清理再抛出，不交付未装备对象冒充成功。创建/加载/重置失败对象 Discard，不回到可复用缓存。
- 回调中卸下、销毁、CloseHost、RemoveConfig 或 Shutdown 必须收敛为一次解绑/归还；同一实例转换期不得重复 Equip，回池延迟到外层回调结束。IsCanEquip 内发生状态变化后须复核；Equip 仅在返回时仍属于本次挂载才返回 Success，否则返回 CannotEquip，CreateAndEquip 不交付被中途卸下/回收的结果。
- UnEquipAll 处理调用时快照；回调新增项不被误当作旧快照。CloseHost/RemoveConfig/Shutdown 在清理期间拒绝相应范围的新增；逐件继续清理，最终汇总异常。关闭包含 detached 自有实例，先清实例再放根 Lease；不得关闭 ResourceSystem 或清它的全局缓存。
- fake-null 装备按记录/宿主列表移除，跳过失效 Unity 对象回调但仍归还自有责任。清理使用已记录的租约/对象，不依赖 ResourceSystem.Current 此时仍可用。Runtime 注册既有会话 reset 和 Application.quitting；独立 Equipment Editor 退出 hook 调用 Shutdown，保证无 Domain Reload 连续 Play 不残留。Shutdown 幂等，非退出时完成后可重新登记；应用退出期间拒绝新创建，新会话恢复。
- 归还后的原始 IEquipment 引用不再有使用权；保持原始对象 API 不提供跨 checkout 的旧别名防护，不宣称等价于 generation-safe 的公开 Handle。

## 3. 实施批次

各批先准备针对性断言，环境允许时保留实际失败结果，再实现最小修复并运行同一用例。新增类型导致的首次编译失败与可运行的缺陷复现分开记录。不要为测试数量或满足模板添加空用例。

下列未以 `Assets/` 开头的产品/测试路径均相对于 `Assets/YLFramework/`；每个新文件/目录包含 Unity 生成的配套 meta。

### T1: 配置与接口承接

**Files:** 新增 `Assets/YLFramework/Runtime/Systems/Equipment/{IEquipment,IEquippable,EquipmentsConfig,EquipmentConfigData}.cs`、`LICENSE.txt` 及 Unity meta；新增 `Tests/EditMode/Extensions/Equipment/EquipmentConfigurationTests.cs`。

- [x] 承接 §2.1 接口/枚举和 §2.2 字段；EquipState 放 IEquipment.cs，EquipmentType 放 EquipmentConfigData.cs，T2 不重复定义。使用既有 YL Excel 属性标记 ID，忽略 parent 和脚本 GUID；Runtime 不含 Editor Drawer 类型引用。
- [x] 将原 Runtime 的 GetDataType/Rename 和六个 Drawer 留给 T3 Editor，不保留条件编译下的反向 Editor 依赖；不改变现有 asmdef。
- [x] 配置断言：getter 不设置 dirty/改变资产字节；空表查询为 null；同表重复 ID 明确失败；等长替换/改 ID 后查询正确；字段/枚举值、派生配置类型和保存重导入保持。
- [x] 在实际 Unity EditMode 运行 `YLFramework.Tests.Extensions.Equipment.EquipmentConfigurationTests`；输出逐项结果和新 GUID/meta 检查，不以辅助编译替代。

### T2: 创建、挂载及清理

**Files:** 新增 `Runtime/Systems/Equipment/EquipmentManager.cs`；新增 `Tests/EditMode/Extensions/Equipment/EquipmentLifecycleTests.cs`、`Tests/PlayMode/Extensions/Equipment/EquipmentResourceTests.cs`。内部记录/重入保护优先放在 Manager 内，不另建全局平台。

- [x] 实现 §2 全部 Manager 签名与结果语义；只替换必要的反射、Pool、fake-null、Resource 和 Session 边界。移除原仅用于保存字典的私有单例，不创建 Root、UI、Update 宿主。
- [x] 配置登记先完整校验再提交；RemoveConfig 按引用/登记名清理该配置派生的全部自有实例，解绑引用该根定义的借用装备但不回收它们，再归还自己的根 Lease。保持其他配置和独立 Lease 不变。
- [x] EditMode 断言：加载回调取得本次定义；同实例跨宿主拒绝、同 ID 多实例成功；拒装无副作用；OnEquip 抛错补偿；OnUnEquip/OnUnloaded 抛错仍完成余项清理；UnEquip(false) 后可重装并最终销毁；外部对象从不回收；自定义 Equals 不合并不同引用。
- [x] 重入断言：IsCanEquip 中再次装备/关闭；OnEquip 自卸下/销毁；OnUnEquip 中 CloseHost；OnUnloaded 中创建同类型；回调 RemoveConfig/Shutdown；均不重复回调、不提前复用旧实例、不留下列表或 detached 记录。
- [x] PlayMode 使用自有真实 Prefab、根 Lease 和场景：缺/多装备组件、OnLoaded 抛错、地址不存在、非法 enum/type 均不泄漏；热复用重置正确；已销毁装备/宿主、Resource 先失效后 Equipment 关闭均可清理；关闭/卸载按显式 owner 合同执行。验证已有 Resource 缓存的合法 pin 与独立 Lease，不以“所有资源计数必为零”误判缓存。
- [x] 对 `EquipmentLifecycleTests` 和 `EquipmentResourceTests` 执行相应 Unity EditMode/PlayMode；复核没有新 XF/Editor 运行引用或第二套实例缓存。

### T3: 完整普通作者工具

**Files:** 新增 `Editor/Systems/Equipment/` 下的 `Window/ConfigsWindow.cs`、`Controller/{EquipmentsConfigController,ScriptBindController}.cs`、`Inspector/{EquipmentsConfigInspector,EquipmentConfigDataInspector}.cs`、`TreeView/EquipmentListTree.cs`、`Other/ListenerFileEvent.cs`、原六个 `Drawers/CustomEquipmentConfigData*GUI.cs`、`EquipmentAuthoring.cs`、`EquipmentSessionLifecycle.cs`；新增 `Tests/EditMode/Extensions/Equipment/Editor/{EquipmentAuthoringTests,EquipmentTwoPlayTests}.cs`。

- [x] 保留窗口/列表/Inspector、创建/复制/删除/改名、搜索/选择/排序、Script 与派生数据类型绑定、Asset/图标项目及资源选择预览。菜单 `Window/YLFramework/EquipmentSystem`，CreateAssetMenu `YLFramework/EquipmentSystem(装备配置)`，独立偏好键 `YLFramework.Equipment.*`。
- [x] 复用 YL SplitWindow、ReorderableTreeView、TreeViewStateGroup、SerializedObject 工具及 Resource Editor 预览；参考 Skill Inspector 的字段名 Drawer 映射，将编辑器绘制彻底留在 Editor。GetDataType/Rename 作为 EquipmentAuthoring 的 Editor 扩展；不引入 Runtime→Editor 引用。
- [x] 类型扫描/绑定只处理 YL 装备根及当前类，使用现有类型工具，不遍历后修改旧 XF 资产。缺 GUID/无效类型给出根、ID、字段诊断，不能静默退回基础类型。
- [x] ID 保留 start_id+1 起点并跳过已占用值；`start_id=1000` 的空表首项为 1001，跨 YL 根重复与 int 溢出明确处理。所有 CRUD/Undo/重排后失效缓存；跨窗口维护独立选择和拖拽责任。
- [x] 原 getter 的目录发现用途移为显式作者刷新：只从本根 GUID 目录校验并补入条目，非空列表也能发现新增资产；保留已有顺序/外部引用，不隐式删空、去重或删除文件。冲突先诊断、不部分写入；通过 Undo 及只保存本 owner 完成，测试读取仍无副作用。
- [x] 条目删除/改名复用现有 DomainAssetOwnership 规则：只操作本根子资产或根 GUID 所有目录内的独占条目，外部引用只解除。根删除只清理归属明确条目，目录仅空时删除；不递归删除不明内容，不调用全局 SaveAssets。保留双击根打开窗口。
- [x] 作者测试完整走 controller/TreeView/Inspector：创建、复制、改 ID、派生字段、Script/Asset 切换、资源字段、重排及 Undo/Redo、删除、保存/重导入重开；多窗口、缺脚本、外部资产/同文件其他 subasset 和无关 dirty 对象受保护。实际打开窗口检查两种模式 GUI，不把程序化覆盖写成全人工流程。
- [x] 两轮真实禁用 Domain Reload 的 Play 测试分别断言：配置、装备/借用宿主、未装备自有实例和根 Lease 退出清理；两轮间不手动 Shutdown/清 static，恢复原场景和 Editor 设置。

### T4: 可选 Excel 完整接线

**Files:** 修改 `Integrations/DomainExcel/Editor/DomainExcelCommands.cs`；按需增加同目录装备专属菜单代码；新增 `Integrations/DomainExcel/Tests/Editor/EquipmentExcelTests.cs`。不改变后端版本、MiniExcel 二进制/许可、默认宏或主体程序集依赖。

- [x] 增加 `ExcelImportPlan DomainExcelCommands.PreviewImport(EquipmentsConfig root, string path)`，sheet 为 `equipments`，使用既有 `ExcelAuthoring.PreviewScriptableObjects` 和 `ScriptableObjectToExcel`，传入 root.Equipments、序列化字段 `equipments`、Equipment Controller 的根 GUID 目录、parent 回填及已选派生数据类型。
- [x] 扩展现有选中根/条目导入导出菜单。校验 Asset/Script 分支：Asset 核地址而不要求 Script 类名；Script 核 IEquipment 类型/构造；无效 enum/重复 ID 拒绝。预览先显示 Errors/Changes，再明确 Commit(true)；只在成功应用后失效缓存。
- [x] 最近成功导入/导出的工作簿按根 GUID 写 EditorPrefs，提供打开所在目录入口；移动/改名根后仍有效，文件不存在时禁用。普通 Editor 仅提示选配菜单，不静态引用可选程序集。
- [x] 真 xlsx 往返覆盖 Asset+Script 行、图标/枚举/派生字段、增改删、空表；预览取消、坏行/缺类型、输入或目标在预览后变化、提交故障均不部分写入或删除原资产。已有 Skill/Buff/Mission Excel 用例同跑回归。
- [x] 默认未启用 YL_DOMAIN_EXCEL 时先证明普通系统可编译/运行；再在受控验收环境启用选配并执行 `EquipmentExcelTests` 及既有 DomainExcel suite，恢复原宏和设置。普通核心仍不含 xlsx 后端。

### T5: 集成、正式导出与独立交付

**Files:** 自有 `Tests/Fixtures/Extensions/Equipment/` 及必要验收工具；更新现有 `Documentation/{Architecture,README,IntegrationPlan}.md`、`Runtime/THIRD-PARTY-NOTICES.md` 和本 Plan。根 `LICENSE.md` 只在现有版权/许可确有缺口时补齐；不新建重复报告文档。

- [x] 实施前核对真实 Editor 的 projectPath、PID、编译/Play/UTF 状态、dirty 场景/资产和工作区差异；不另启同工程 Unity，不保存用户场景或批量 SaveAssets。自有 fixture/输出使用独占 run/目录，冲突先停，不 reset/stash 或覆盖别人的修改。
- [x] 为 Equipment 新建专用消费者，不修改已验收 C 消费者或重用其固定历史证据根。测试/Player 专属代码以约束隔离，默认 Player 不新增自动启动探针；只生成/清理自己拥有的源码、Scene、Prefab、SO 和 meta。
- [x] 执行下表所有门禁，结果绑定当前源码、Unity 编译图、实际依赖和产物；报告失败/阻塞，不沿用旧 Player 或同工程成功冒充无 XF 结果。
- [x] 默认 Exporter 已递归 Runtime/Editor，不新建导出器或扩大根；使用不触发全局保存的正式 Export 入口。核逐文件/meta/GUID/hash/MIT，排除 Tests、Integrations、旧 XF、MiniExcel、游戏及敏感内容；另按既有完整选配单元交付 Excel/DomainExcel。
- [x] 现有说明增量记录入口、Resource 初始化/根类型登记、显式 CloseHost/Shutdown、借用/自有责任、回调失败/原始引用边界、选配 Excel，以及实际验收和剩余限制。未接线能力不宣称可用。

## 4. 验收门禁与完成标准

| 门禁 | 本批必须获得的证据 |
|---|---|
| G0 来源/静态 | 固定源 revision、MIT、全部新 meta/GUID；Runtime 无 XF/UnityEditor/可选 Excel 引用。检查实际 diff，保护既有 dirty 和原游戏/配置/Bundle 哈希；不修改锁文件来消除环境差异。 |
| G1 主 Unity/EditMode | T1/T2/T3 的真实专项测试；既有 FoundationBoundaryTests、CoreArchitectureBoundaryTests、FrameworkAssemblyIndependenceTests、CoreIdentityTests、ExportUnityPackageTests；保留负例和白名单边界，确认全部新源码进入正确编译图。 |
| G2 Play/生命周期 | T2 Resource/场景、T3 真实同域双 Play；借用对象与独立 Lease 保持，自有根/实例责任归还，回调/关闭恰一次，不创建新更新宿主或默认 UI。 |
| G3 Player | 主工程和独立接收工程各实际构建/运行 Windows64 Mono/Disabled 与 IL2CPP Low 非 Development 图形 Player。消费者覆盖 Prefab 与仅字符串引用的 CLR 装备、派生配置、池复用、拒装/失败回收、CloseHost/Shutdown 和退出；应用只定向 link.xml/Preserve 真实反射类，不整程序集保留。记录 OS exit0、run 标记及进程退出后的完整日志，无非预期 Error/Assert/Exception。 |
| G4 无 XF 独立接收 | 新独占接收工程，首次导入时只有正式核心包和真实必要依赖，无 XF/MCP/测试 helper/主 Library 或主产品 DLL。先首导入编译，再加消费者完成 G3；之后再选装许可完整的 Excel+DomainExcel，运行装备及现有 Excel 测试，证明默认/选配边界。外部目录仅用执行时已获准的短路径。 |
| G5 原游戏保护 | 仅一次与本集成风险相称的主工程代表回归：菜单/设置/进入/暂停返回/再入；仍走原 XF/XFAB/XFFSM/Overlap 及既有音频。受保护资源、MainModule 输入/忽略 Bundle、用户现场保持，不重建 Bundle。 |

**完成标准：** 上述能力真实接线，专项/边界/独立交付/双后端/原游戏门禁有各自有效结果；源码、必要 meta、许可及现有说明齐全。未获执行环境或权限、任一门禁失败时保留未完成状态并说明最小阻塞；不以源码复制、辅助编译或本计划完成代替产品完成。

**规划轮状态（历史）：** 源码/依赖/调用方研究及六项用户决定完成；当时只写入本 Plan，未修改产品、未操作 Unity、未运行测试/构建、未执行 Git 写操作。实施后的状态见下方实施记录。

## 5. 实施记录

- 2026-10-20 用户明确要求实施本计划。主 HEAD/用户 dirty 与规划基线一致；实际 Unity 为主工程 PID16464，未 Play/编译/构建，原 UIAcceptance 场景已保存。独占证据根 `Temp/EquipmentAcceptance/EQ-20261020T204732Z`，preflight 记录997个受保护文件哈希及601个初始 persistent dirty 对象的序列化内存，不输出秘密内容。
- Pre-flight: T1 的 namespace/枚举/列表由 T2/T3/T4 消费，签名一致；T2 的 Shutdown 由 T3 独立退出 hook 消费；T3 Controller 路径和派生数据类型由 T4 消费。T5 在默认核心通过后才选装 Excel，不改变产品依赖方向。
- Ruling: 在已指定的 integration/xf-yl 主工作区内实施，不创建 Worktree/分支、不提交；以本计划持续记录替代另建工作流 ledger/ignore 配置。理由是当前授权与保护边界明确，工具工作流不扩大 Git 权限；代价是尚未提交的成果需在后续 Git 收口时精确选取。
- T1: 已先加入4个真实配置行为断言，以运行时程序集反射定位新类型，未承接前可产生可运行的缺失能力失败，而非先破坏工程编译。
- T1: complete。Unity EditMode 已观察 RED 0/4（新定义缺失）到 GREEN 4/4；保存重导入、等长替换/改 ID、重复 ID 不发布部分缓存及读取不 dirty/不写盘通过。Runtime/Equipment 四文件及 MIT 已承接，meta 由此次 Unity 导入生成；没有产品程序集变更。
- T2: 已观察15项 RED（Manager尚未承接）到14/15，再修复延迟Flush丢失批量汇总语义后 EditMode19/19通过。真实Prefab/Resource首轮5/6通过，剩余用例错误假设 ShutdownAsync 会强制失效 live Lease。
- Ruling: Resource.ShutdownAsync 源码449-483明确以 Busy保护活跃消费者，不为装备修改这一公共契约。将其测试明确为 Busy后归还再成功；另独立调用真实会话EndSession路径验证Resource先失效的反向关闭，并恢复测试自有会话admission。代价是该失效顺序用例依赖已核实的私有测试入口，真正无DomainReload退出仍由T3验证。
- T2: complete。主Unity EditMode20/20、真实PlayMode7/7通过，包含双创建职责中的CLR复用与真实Prefab/租约、缺/多组件、加载回调失败Discard、原生销毁、Busy关闭和真实会话反向失效。配置原子登记、所有权/重入/逐件清理与汇总错误已覆盖。
- T3: 作者工具4项RED到4/4GREEN，随后含双窗口和双Play的26项中25通过。双Play首次直接进入用户当前UIAcceptance场景，触发其自启动UI34 InputSystem合同错误；不修改UI实现或抑制错误，改用本专项保存的空场景，结束恢复原SceneSetup，防止无关验收探针混入本用例。
- T3: complete。完整普通作者工具已接线；源目录发现改为显式刷新，使用既有 SplitWindow/ReorderableTreeView/DomainAssetOwnership/ResourceBuildMap，未拷贝旧 XF 编辑器依赖。作者测试5/5、配置+生命周期+双Play合计26/26；Script/Asset两种模式实际创建窗口并执行原生 Layout/Repaint 成功，非人工全流程验收。桌面截图被VS Code遮挡，不作视觉证据，已删除；有效记录 `author-window-smoke.json`。
- Ruling: UTF1.3.9 的 EnterPlayMode 默认 ExpectDomainReload=true 会使禁用 Domain Reload 用例的上下文恢复不符预期；已按现有Domain用例改为 EnterPlayMode(false)，重新实际两轮1/1通过，随后G1整批通过。只恢复本次观察到的 reload-enabled 字段（原false/options3）及原ExpansionPackages资产内容未变但被自动清除的dirty标记，不覆盖其他内存。
- G1（当前源码）: 主Unity真实EditMode62/62通过，过滤包含Equipment全部26项及 FoundationBoundaryTests/CoreArchitectureBoundaryTests/FrameworkAssemblyIndependenceTests/CoreIdentityTests/ExportUnityPackageTests。G2运行时真实Prefab/Resource专项7/7通过。T4选配Excel及G3/G4/G5仍待实际执行，不沿用旧C证据。
- T4: 新入口先观察4项RED，随后装备7项与既有DomainExcel6项合计13/13 GREEN。覆盖Asset/Script/派生字段、取消/空表确认删除、坏ID/类型/地址、预览后输入/目标变化、实际保存拒绝导致已写兄弟文件及内存精确回滚、GUID路径记忆。未改Excel后端；旧回归仅增加向当前Equipment验收owner登记新fixture目录，未削弱断言或放宽保存围栏。
- Ruling: 正式SO导出器正确拒绝重复ID，因此坏工作簿用既有平表作者API生成；无效枚举由暂存转换器返回不可提交plan，断言其错误/Failed/无写入，不强求异常。宏恢复时捕获到MCP把UNITY_MCP符号移到末尾，集合完全未变；只对已授权Standalone字符串以符号集合核对，非Standalone及数值/键形状仍精确匹配，然后恢复原字节/原稀疏映射，不覆盖额外符号变化。
- 2026-10-21恢复：上次MCP超时前XLSX测试实际结束（7/9），UTF已空闲、原Editor仍PID16464；通过testResults读取旧结果，不重复启动旧测试。ReviewExcelGate在本轮修复后已精确恢复，选配宏已退出。
- 独立Review共4项P1/4项P2，全部按实际行为判为需修复，没有忽略或降低断言。新增回归先观察EditMode38中11项RED、PlayMode9中2项RED及XLSX旧GUID/跨根2项RED；随后集中修复私有挂载revision/Record身份、延迟关闭屏障、借用定义快照/fake-null、设置setter准入、资产删除Undo、树对象身份/元数据刷新及xlsx原子绑定/跨根提交校验。
- Review修复验证：主Unity Equipment EditMode39/39、Resource PlayMode9/9、选配DomainExcel15/15（装备9、既有6）通过，包含真实不归属Android修改被拒绝、外部文件替换后无关Undo不触碰及真实文件/meta/GUID删除Undo/Redo。设置和场景已恢复，G3/G4仍未完成，旧G5代表回归1/1有效；未提交或Push。
- Ruling: Unity Undo对象快照不能同时正确管理独立文件删除；仅Undo.DestroyObjectImmediate会留下Redo文件，追加DeleteAsset又使Undo丢失文件。改用Equipment私有HideAndDontSave文件撤销记录，与根列表同组保留原始文件/meta及dirty内存；只在本记录状态转换时回放，未知文件不覆盖，Editor退出释放记录。代价是少量Editor专用状态，不新增Runtime平台或共享资产事务框架。
- Ruling: 为使Excel最终类/GUID在同一暂存事务内一致，保留原7参公开签名并增加8参afterImport重载；AddValidation只读约束在预览及真正写入前执行，拒绝跨根ID竞争。不修改原初始化顺序，不在Commit后修补，既有6项领域xlsx同跑通过。
- G1修复后最终批75/75通过。主Mono首次gate在真正BuildPipeline调用前拒绝：引用检查误把既有YLFramework.UI.Acceptance（原XF游戏链的验收程序集）算作产品，已保持失败记录并恢复设置/场景。改为核对明确的Runtime、随包Runtime Sample和本批Player消费者；完整编译图仍留证，独立工程另查全图无XF，不通过移除原游戏或放宽产品约束处理。
- G3主工程：MainMono2-20261021（Mono2x/Disabled/Release、BuildOptions.None、0 errors）及MainIl2cpp-20261021（IL2CPP/Low/Release、BuildOptions.None、0 errors）均真实构建成功。两个图形Player各15项通过，OS exit0，quitObserved=true；完整退出日志无错误。Run分别EQ-a5fa4017961749008200efac44e8ae2e和EQ-ba5c9610b9e44a319591eb86887b75d9，均在本证据根同名gate下有BuildReport/inputs/settings-restored及Player verdict。1711等既有warning未屏蔽，不称零警告。

## 6. 最终交付

**T1-T5及G0-G5技术验收完成，2026-10-21。未创建分支、提交、合并或Push。** 独立交付根为`C:/Users/Kano/AppData/Local/Temp/kilo/EQ261021A/D`，`delivery-binding.json`绑定当前正式源码/制品、首次导入、四个真实Player、独立xlsx及主工程保护终态。工具源码稳定入口为`Tools/Equipment/`，交付根保留同版本Tools副本；不以Temp作为唯一复现入口。

| 门禁 | 实际结果与证据 |
|---|---|
| G0 | `final-protection.json`核997个原文件SHA及601个原persistent dirty内存，差异0；自动重载清除的ExpansionPackages dirty标记仅在内容相同时恢复。原FSM、Packages、配置、Bundle及用户未跟踪资产保持；测试生成的空YLFrameworkData目录已删除。 |
| G1/G2 | 主Unity EditMode75/75（含Equipment39及既有边界/正式Exporter），Resource PlayMode9/9；真实禁用Domain Reload双Play包含在39项内。选配主工程DomainExcel15/15（Equipment9+既有6）。 |
| G3主工程 | Mono/Disabled和IL2CPP Low/Release均BuildOptions.None，0 build errors；图形Player各15项通过、exit0、quitObserved=true，无异常退出日志，详见§5。 |
| G4首次导入 | 正式核心包1047条payload/meta/GUID逐项审计通过，SHA256 `984E66AE6D5BC8401C78022F1803D6D49B339E49289F4366CAD1F5A26133C4FE`。新R工程由Unity-createProject生成；无任何先存.cs/.dll/.asmdef/.asmref、测试框架、XF或MCP，正式包首次导入后四份产品程序集实际编译成功。仅UGUI1.0.0/builtin、Json.NET3.2.1/registry及内建模块，见`first-import.json`。 |
| G3独立Player | `Gates/Mono-saved-scene-archive`和`Gates/IL2CPP-low-release`各实际非Development构建/图形运行15项、exit0。Run为EQ-85be67ec4fb346f3b70adb099936fdec及EQ-284d351756744a6f8cf90b128efc6065；全图无XF/MCP，定向保留仅字符串类，不整程序集link。 |
| G4选配 | 核心四Player证明后才装完整Excel/DomainExcel及UTF1.3.9；独立xlsx15/15、0 skip，宏/设置恢复，`Gates/Excel/{results.xml,summary.json,settings-restored.json}`。另交付`YLFramework-Equipment-Excel.unitypackage`，含MiniExcel/原MIT/Apache和Dependencies，不带Tests；精确SHA在binding中。 |
| G5 | 原菜单/设置/进入/暂停返回/再入代表回归1/1通过，继续原XF/XFAB/XFFSM/Overlap与音频链，不重建Bundle。 |

接收工程首次Mono调用的BuildPipeline本身成功，但batch初始SceneSetup为0项，设置收尾不能恢复空Setup，故该次进程exit1保留失败。仅修复自有CLI夹具：Capture前保存自己的空场景，并将gate镜像到交付根避免Unity退出清Temp。旧日志/工具修订和旧输出保留，新Mono与IL2CPP均实际exit0，不将失败改写为通过；不涉及产品修复或首次导入重绑定。

边界保持：没有背包/槽位/数量/存档或自动Skill/Buff联动；原始装备引用归还后失效，不提供公开generation Handle。窗口两模式做实际Layout/Repaint及自动作者回归，不声称完整人工视觉操作验收；截图遮挡记录已删除。只覆盖Windows64 Mono与IL2CPP Low，不外推High/移动平台、性能或零原生泄漏遥测。既有XML/公开文档warning没有屏蔽。

## 7. 用户验收与最终Git交接

2026-10-21用户正式确认本专项技术验收通过并授权连续完成最终归档、文档收口、中文Commit及普通Push。本轮沿用§5/§6实际技术证据，不重跑测试、不重新Export/构建、不修改已验收包或原始binding。

- 实时开工基线：integration/xf-yl，HEAD `be3ea8faf612b563d2256ff6fff19b6e64aeb044`，远端实际及本地tracking均`753172edd8896e4cd475a543a9fdf9085ef74e5b`。远端是HEAD祖先，已有25条待推送提交；普通Push会连同其历史快进同步，不改写/拆分这些历史。本轮索引为空、无Git锁或可见活跃Git写入；Agent Manager其他任务均idle。
- Equipment候选精确限定Runtime/Editor Equipment、对应测试/消费者/工具及meta、已验收Excel两处必要扩展和DomainExcel接线/回归、适用许可、说明和本Plan。旧总体拓展Plan、.mcp.json、三份游戏FSM、Packages两文件、Assets/Test（含旧装备配置）及AlternateContent.meta均不纳入。
- 稳定入口：`G:/Unity/Deliveries/Equipment-20261021/`。原D目录7279文件完整复制，另补binding引用的必要外部主工程收据/日志及四份Player输出，共9392文件、4,267,786,377 bytes，逐文件大小/SHA与源复核一致；没有覆盖`G:/Unity/Y9`。主工程原始设置/dirty内存内部快照不作为公开交付导出。
- 原始`delivery-binding.json`逐字节保留，SHA256 `e2a80f0d09060f2681c5bb0f02ddaaf6db410f2713ac58227d7f4426cf416c38`。其路径仍表示验收时位置；`archive-receipt.json`另给原路径到稳定位置的映射及逐文件SHA/大小，不重绑定历史。
- 核心包SHA256保持`984e66ae6d5bc8401c78022f1803d6d49b339e49289f4366cad1f5a26133c4fe`；可选Excel包保持`857d585cdc51224d26d634de949a8d24c4d9dd2f2b1ba00d8c855c32ff284520`。依赖、许可、安装/复现工具、首次导入及成功/失败复验记录均在稳定目录保留。
- 当前仓库文档的用户审批/稳定路径文字属于验收后最小交接增量；冻结包保留其真实导出快照，不冒称已含后来的用户审批。代码/公开接口/资产身份不因本轮文档更新变化；真实Commit与普通Push结果以稳定目录外部Git收据和最终报告为准，不再追加自引用docs提交。
- 精确暂存为113个Equipment所属文件。`git diff --cached --check`发现57份本轮新增Unity meta的171处空值行尾空格，已仅移除行尾空白，GUID/Importer/字段语义不变；逐文件规范前后SHA及GUID见稳定目录`git-meta-normalization.json`。冻结包/原binding仍保留验收前原字节，不重新Export，不将此文本格式收口冒称新的技术验收。
