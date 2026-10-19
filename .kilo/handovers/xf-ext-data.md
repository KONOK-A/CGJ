# B组配置数据交接

## 授权与基线

- 唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`；2026-10-19本次用户追加授权覆盖其中旧T3、D2/D4/D6及Save只读/分批停止状态。
- 实际工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-data-14669bf18d117ed8`；分支`fusion/xf-ext-data`。
- 开工HEAD/B0：`c88500aef6651f5b13ac933dd1f209ecb314e92d`；祖先检查退出0，工作区和index干净。未重置、切分支或同步其他分支。
- 不操作Unity MCP/Editor/batchmode；不Push/合并，不修改共享Plan、Core/Resource、主体asmdef、Packages/锁文件或原XF/游戏。新meta待集成owner通过Unity生成。

## 当前进度

- Localization：产品、原布局作者工作流和测试已实现；离线纯逻辑12/12及组合辅助编译通过，尚非Unity验收。补确定性回退、同数量内容变更、重复ID、根/事件/图片资源责任、晚到隔离和直接刷新。
- DataAssets/SO：保留根/子资产与类型绑定、CRUD/排序的候选已落齐，纯逻辑10/10及真实参考辅助编译通过；无新Catalog，资产/GUI/PlayMode仍未运行。
- Excel：真实List/SO/JSON作者API、MiniExcel后端和已选SO根菜单已落盘；父独立真实xlsx18/18绿及完整可选Editor闭包编译通过；12项Unity资产/GUI用例作为待引擎执行的准备，不是GUI验收。接点见下节。
- Save/Archive：单一`SaveStore`文件引擎与原`SaveManager`Unity门面已实现，父独立真实文件33/33、门面及既有纯Json回归39/39通过（包含前述33项），待组Review与Unity/Player验收；兼容策略见下节。

## Excel真实接点

- 可选目录`Assets/YLFramework/Integrations/Excel/Editor/`；assembly及namespace均为`YLFramework.Excel.Editor`。普通Runtime/Editor不引用它，C组可在自己的可选Editor程序集接线，勿修改主体asmdef。
- 已落盘入口`ExcelAuthoring.ListToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1")`。
- 已落盘预览`ExcelAuthoring.PreviewList<T>(string path, List<T> values, string sheetName = null) where T : class, new()`，以及`PreviewJson(string path, string jsonPath, string excludePrefix = "", string sheetName = null)`，均返回`ExcelImportPlan`。
- SO入口已落盘并用真实Unity参考辅助编译：`ScriptableObjectToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1", Type dataType = null) where T : ScriptableObject`；`PreviewScriptableObjects<T>(string path, List<T> values, ScriptableObject owner, string assetFolder = null, string sheetName = null, Action<T> initialize = null, Type dataType = null)`返回同一`ExcelImportPlan`。`initialize`仅用于暂存副本的领域字段绑定，不得改外来资产；SO事务/GUI尚未在Unity执行。
- `assetFolder = null`用于同root真正subasset；独立主资产模式须传已存在的独占目录。B组SO目录由`YLFramework.DataAssets.Editor.ScriptableObjectConfigsController.GetDataPath(root)`取得，条目`parent = root`；C组同样传自己的精确目录。旧条目必须属于该owner的真实序列化List，dirty资产拒绝导入，同ID/类型就地更新。
- `ExcelImportPlan`提供`Errors`、`Changes`（Kind/Key/Before/After）、`CanCommit`、`Commit(bool confirmed)`及`Dispose()`；结果`Pending/Applied/Cancelled/Failed`。必须先检查错误和差异，再显式确认；取消不写入。
- SO预览直接支持`List<BaseSO>`的派生条目，依据`DATA_TYPE_FULL_CLASS_NAME`解析实际类，不复制一个脱离owner的`List<Derived>`。`dataType`约束可用派生范围，提供空根模板字段及无类型列时的绑定类型；`type_script_guid`失败明确报错，不猜类名。
- 已选SO根入口`DataAssetsExcelCommands.PreviewImport(ScriptableObjectConfigs root, string path)`和`Export(root, path)`真实可用；菜单`Assets/YLFramework/Excel/Preview Import Selected Data Root`与`Export Selected Data Root`。自身可选asmdef引用`YLFramework.Editor`，普通Editor不反向引用。暂存条目设置`parent=root`，成功清缓存；取消不创建数据目录，空根也不依赖既有`excel_path`。
- 通用UI入口`ExcelAuthoringCommands.ImportList/ExportList/ImportScriptableObjects/ExportScriptableObjects/ShowPreview`及`YLFramework/Excel/Workbook To JSON`。导入预览包含新增/更新/删除和坐标错误，保存主资产前必须干净，过期工作簿或目标变化拒绝提交；导出整工作簿覆盖须确认，不承诺保留原多sheet格式。
- 后端MiniExcel `1.31.2`，Apache-2.0，netstandard2.0/无NuGet包依赖；来源上游tag`1.31.1-1.31.2` commit`f1498a2d162debca9a1e9153cf2249131a75eeb0`。固定nupkg SHA256：`f11a54c1bd5b3fb3332dc5d4e40ade59a3539cb8de6d087a245575457d66379a`。本组自有恢复脚本`Tools/Extensions/Data/Excel/Restore-MiniExcel.ps1`。
- 父独立执行`Tools/Extensions/Data/Excel/Run-ExcelOffline.ps1 -Run parent-pre-review`：真实xlsx18/18、0失败/跳过；全Runtime458源39warning、全Editor102源4warning、完整可选Excel7源及测试5源0warning均辅助编译退出0；没有`YL_EXCEL_OFFLINE`条件排除。当前候选尚待模块提交和组Review，不是Unity/AssetDatabase/GUI结果。
- DLL实测SHA256 `0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e`与固定清单一致。现有`Editor/Distribution/ExportUnityPackage.ExportRoots`不含`Integrations`，默认导出不会带入此后端；不是只靠`Dependencies.json`标记宣称隔离。可选分发应另带完整adapter与`Licenses`，Unity导入后还须核对PluginImporter为Editor-only并生成meta。

## Save设计与兼容策略（实施前确定）

- 单一`SaveStore`负责BCL文件/索引/槽位/模型对象/设置/缓存/备份及同步异步；既有`SaveManager`仅负责主线程捕获Unity路径并暴露兼容门面，非第二存档权威。
- 保留对象/设置裸JSON用途；索引升级v2并纳入当前槽。检测到v1拒绝原地写入，提供显式复制迁移到不同新目录，拒绝源目标相同和已有目标冲突。没有真实旧档样本，只能验证合成合同，不能宣称历史档兼容。
- Archive仅提供显式选择的明文JSON导入到新槽，不扫描原目录、不自动转换、不接纳DES/固定IV格式；旧`.sav`原件不修改。实际读到的XFAB旧EncryptTools用`string.Empty.PadRight(8)`产生8个空格字节IV，不是全零IV；该薄弱DES能力没有复制进新格式。
- 取消在单文件原子提交点前有效，提交后报告成功；同根排他owner、同槽串行、跨槽可并发，关闭取消排队并排空。磁盘失败不推进对象缓存。
- Save独立收口实测：`Run-SaveSafety.ps1`为33/33；`-WithFacade`为39/39（33核心+2真实门面+4既有纯Json）；`-ReferenceOnly`真实Unity2022.3/.NETStandard2.1参考辅助编译退出0、2个CS1701（Json.NET/NUnit 2.0→2.1引用匹配）。不依赖虚拟Unity，不执行Unity原生API来冒充通过。
- 已落盘的新读取入口`ReadObject<T>/ReadObjectAsync<T>/ReadGlobalSetting<T>`返回`SaveReadResult<T>`，携带主件/备份/缺失来源和缓存标志，缓存命中不隐藏备份来源；`IndexNeedsRecovery`显式报告索引恢复要求。`RestoreObjectBackup/RestoreSettingBackup/RestoreIndexBackup`为显式修盘入口，保留损坏原件而非读时自动改盘。
- 现有`SerializedDictionarySaveTests`将按新门面owner调整必要测试隔离，保留原磁盘/Unity断言；实际数据保护验证只用自有临时目录。
- 已实际研究ArchiveSystem1.0.7 `ArchiveManager/IArchiveModel`与现有YL Save；复用成熟槽位/模型/当前项组织，统一至`SaveStore`而非复制旧Manager/DES。来源revision `8046fe56f8`，MIT2025由现有根许可保留，概念吸收及来源在此登记。
- 兼容影响：损坏/IO/权限/版本错误不再静默变默认值；只在确实缺失时建立默认设置；禁止`TypeNameHandling`从JSON实例化CLR类型。删除改为`.deleted-*`软删除，不是安全擦除且不自动清理。保存异步先在调用线程序列化快照；反序列化/Converter异步在线程池，不得调用UnityAPI或重入同owner。缓存继续返回原实例，调用方负责该实例的并发修改。
- 显式Archive导入现严格拒绝未映射字段，必要字段由目标DTO的`JsonProperty(Required=...)`声明；防止错误DTO静默丢字段后仍报成功。父新增回归真实红32/33→绿33/33，失败源及目标样本保留在`Temp/Extensions.Data.Save/cases/4a68331295a145d49b393b2e57d3196a`，不删原件制造通过。
- `SaveMigration.ImportVersion1(sourceRoot, destinationRoot)`要求不重叠且目标不存在，保留原索引/备份为`.legacy-v1`并保留文件时间；`ImportArchiveModel<T>(sourceFile, SaveStore destination, destinationId, token)`仅进入新槽。模型`Create/Save/Load/LoadAllModelsAsync`与对象槽、缓存、`CurrentSaveID`共用唯一引擎。
- 文件提交点是已flush临时文件的`File.Replace/File.Move`；不支持原子替换的文件系统失败关闭，不删除原件降级。创建/删除跨索引与目录不是全根多文件掉电事务：失败staging/tomb和原件保留供显式恢复。一次迁移发布曾`AccessDenied`，原因未定位，后续单例及完整回归绿；没有吞错/添加重试，也不能称该环境问题已修复。

## 接口与边界决策

- Localization继续显式登记配置，避免Runtime/Editor自动全资产扫描；根Lease由登记句柄持有，条目借用不得超出登记生命周期。图片消费现有`ResourceImage`。
- 中文变体只在中文请求时提前尝试；其他语言按精确、English、显式列表顺序回退。查询语言和按ID查询使用同一候选顺序。
- Excel后端仅在`Integrations/Excel/Editor/`，不改主工程包依赖、不向Runtime引入解析器。C组仅在真实API落地后接线。
- Save仅在自有临时目录/复制样本验证；不自动读写或转换原Archive `.sav`，没有真实旧档样本不宣称兼容。
- Localization保留UGUI/TextMesh、Other反射（可绑定TMP公开`text`成员）、资源/网络图片及参数/附加文本；不安装TMP，也不携带联网翻译凭据、Youdao认证或Steam外部作者窗口。后两类非本次离线配置工作流的必要能力，明确未迁入，不伪装为可用；Other反射成员的IL2CPP保留由实际应用配置并待Player验证。
- Localization语言资产创建仍采用根引用的独立语言文件；显式移除语言只解除引用并可Undo，不删除可能共享的资产文件。作者窗口保留语言排序、搜索、全语言新增/复制/删除条目、类型/内容/备注编辑和保存，图片预览复用既有YL预览工具。

## DataAssets真实接点

- Runtime namespace `YLFramework.DataAssets`；保留`ScriptableObjectConfigs.displayName/configs/excel_path/type_script_guid`与`ScriptableObjectData.parent/id/config_name/icon/des`及`SingletonScriptableObjectConfigs<T>`。`TryGetIDRange`区分空目录，`MinID/MaxID`空时为0；ID/引用等长变更使索引失效，重复/名称歧义/hash碰撞明确拒绝。
- `ScriptableObjectManager.AddConfig(root)`返回借用登记`ScriptableObjectRegistration`；`Load(ResourceSystem, ResourceAddress)`返回持根Lease的登记；`LoadAsync(...)`返回`ScriptableObjectAsset<ScriptableObjectConfigs>`并拥有pending请求/Lease/登记。owner关闭Dispose自己的返回值；`RemoveConfig`显式撤本根全部owner，`Clear()`只关本域，不关闭Resource。
- `SingletonScriptableObjectConfigs<T>.LoadAsync/Instance/Release`保留具体SO单例用途。根Resource mount必须显式登记实际根类型，不扫描旧XF项目；具体子类反射/IL2CPP仍待真实Player验证。
- Editor namespace `YLFramework.DataAssets.Editor`；`ScriptableObjectConfigsController`提供`GetDataType/SetDataType/GetDataPath/CreateData/CopyData/RenameData/DeleteData/DeleteRoot/RemoveReference/Sort/Validate/Save`，菜单`Window/YLFramework/Data Assets`。字段绑定和根/子资产引用布局未替换；排序复用E0树。
- `GetDataPath(root)`返回`Assets/YLFrameworkData/DataAssets/<rootGUID>`。删除只对目标根内真实subasset或该精确目录、parent与列表归属一致的主资产执行；外来/共享引用仅显式移除，不迁入上游递归删除监听。

## 验证状态

- 已执行：路径、Git状态、B0祖先核验；读取实际Unity版本、manifest及正式Plan。PowerShell工具实际为5.1，脚本将按实际Shell或显式pwsh运行，不照搬7.x语法。
- Localization已执行纯逻辑12/12（回退、等长替换/改ID、重复/空项、格式及richText），先红后绿保留在`Temp/Extensions.Data.Localization/`；其余模块结果按最终证据补齐。
- DataAssets：父会话独立执行`Tools/Extensions/Data/DataAssets/Verify-DataAssets.ps1 -Mode Pure`，10/10、0失败/跳过/警告。子任务最终完整辅助编译Player/Editor Runtime及Editor工具均退出0，warning为40/39/4；15个作者、9个PlayMode用例仅编译未执行（PlayMode辅助参考有1个CS1701 NUnit版本警告）。`RemoveConfig(hash)`按registry唯一解析后按根身份撤销，碰撞登记拒绝已纯逻辑实际验证。
- DataAssets早期缺实现绑定、Save在途类型重复及PowerShell工具失败仅在原会话工具输出留存；其固定路径日志后来被green覆盖，不能冒称独立红例制品。最终10/10结果和当前输入仍真实可用，后续父组合探针改为唯一Run目录冻结原始log/rsp。
- Save全部时间戳日志和失败case在`Temp/Extensions.Data.Save/{offline,facade,unity-references,cases}`；父严格迁移红例在`offline/tests-20261019-193701-304.log`，绿例在`offline/tests-20261019-193729-933.log`。未执行原Save/JsonUtility联合测试的Unity部分，原断言完整保留；自有`link.xml`仅为索引/槽元数据提供裁剪声明，不代表用户模型、Json.NET或IL2CPP已验收。
- 首轮组合源码辅助编译：`Tools/Extensions/Data/Verify-DataProfiles.ps1 -Run combined-1`退出0；完整Runtime/Editor-profile/Editor工具分别42/41/4个warning，新增普通EditMode测试源码8文件/0warning、PlayMode测试源码2文件/1warning均编译成功。没有执行其中任何Unity测试。较早Save门面在途状态造成的重复声明编译失败保留在工具调用记录，没有删输入绕过；门面落齐后真实全目录通过。
- 尚未执行：Unity编译、资产/GUI/PlayMode、Mono/IL2CPP Player、独立接收工程和原游戏非干扰门。完成后按证据更新。
- 许可：原Localization/SO为MIT 2024弦风课堂，Excel/Archive为MIT 2025弦风课堂。实读发现既有根许可证只有2021/2025版权，未包含2024；因此Localization在自有目录补完整`LICENSE.txt`，SO同样独立保留。共享许可汇总/根版权由后续集成统一维护，不以既有MIT文本替代遗漏的适用版权。

## 候选提交

- Localization：`517f13f`，`feat(localization): 融合多语言配置作者流程与资源生命周期`。
- DataAssets：`53edd50`，`feat(data-assets): 融合SO根子资产作者流程与安全资源登记`。
- Excel：`09bec5a`，`feat(excel): 接入免费xlsx后端与可确认SO列表JSON作者流程`。DLL沿用仓库既有LFS规则，已核验真实工作区二进制；未上传LFS/未Push，后续跨机器集成须携带对应LFS对象或按固定恢复脚本取得同hash产物。
- 后续模块仍按精确目录本地提交，不Push；新增meta必须由集成Unity真实生成，当前候选不是完整Unity接入包。
