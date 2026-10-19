# B组配置数据交接

## 授权与基线

- 唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`；2026-10-19本次用户追加授权覆盖其中旧T3、D2/D4/D6及Save只读/分批停止状态。
- 实际工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-data-14669bf18d117ed8`；分支`fusion/xf-ext-data`。
- 开工HEAD/B0：`c88500aef6651f5b13ac933dd1f209ecb314e92d`；祖先检查退出0，工作区和index干净。未重置、切分支或同步其他分支。
- 不操作Unity MCP/Editor/batchmode；不Push/合并，不修改共享Plan、Core/Resource、主体asmdef、Packages/锁文件或原XF/游戏。新meta待集成owner通过Unity生成。

## 当前进度

- Localization：产品、原布局作者工作流和测试已实现；离线纯逻辑12/12及组合辅助编译通过，尚非Unity验收。补确定性回退、同数量内容变更、重复ID、根/事件/图片资源责任、晚到隔离和直接刷新。
- DataAssets/SO：独占目录实施中，保留根/子资产与类型绑定、CRUD/排序；无新Catalog。
- Excel：真实List/JSON作者API及后端已落盘，首轮真实xlsx离线8/8绿；SO和GUI继续实施，不能把首轮结果视为所有作者流程完成。接点见下节。
- Save/Archive：已基于实际源码确定单一`SaveStore`文件引擎与原`SaveManager`Unity门面，正在实施；兼容策略见下节。

## Excel真实接点（首轮，仍在扩展）

- 可选目录`Assets/YLFramework/Integrations/Excel/Editor/`；assembly及namespace均为`YLFramework.Excel.Editor`。普通Runtime/Editor不引用它，C组可在自己的可选Editor程序集接线，勿修改主体asmdef。
- 已落盘入口`ExcelAuthoring.ListToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1")`。
- 已落盘预览`ExcelAuthoring.PreviewList<T>(string path, List<T> values, string sheetName = null) where T : class, new()`，以及`PreviewJson(string path, string jsonPath, string excludePrefix = "", string sheetName = null)`，均返回`ExcelImportPlan`。
- SO入口已落盘并用真实Unity参考辅助编译：`ScriptableObjectToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1") where T : ScriptableObject`；`PreviewScriptableObjects<T>(string path, List<T> values, ScriptableObject owner, string assetFolder = null, string sheetName = null, Action<T> initialize = null)`返回同一`ExcelImportPlan`。`initialize`仅用于暂存副本的领域字段绑定，不得改外来资产；SO事务/GUI尚未在Unity执行。
- `assetFolder = null`用于同root真正subasset；独立主资产模式须传已存在的独占目录。B组SO目录由`YLFramework.DataAssets.Editor.ScriptableObjectConfigsController.GetDataPath(root)`取得，条目`parent = root`；C组同样传自己的精确目录。旧条目必须属于该owner的真实序列化List，dirty资产拒绝导入，同ID/类型就地更新。
- `ExcelImportPlan`提供`Errors`、`Changes`（Kind/Key/Before/After）、`CanCommit`、`Commit(bool confirmed)`及`Dispose()`；结果`Pending/Applied/Cancelled/Failed`。必须先检查错误和差异，再显式确认；取消不写入。
- 后端MiniExcel `1.31.2`，Apache-2.0，netstandard2.0/无NuGet包依赖；来源上游tag`1.31.1-1.31.2` commit`f1498a2d162debca9a1e9153cf2249131a75eeb0`。固定nupkg SHA256：`f11a54c1bd5b3fb3332dc5d4e40ade59a3539cb8de6d087a245575457d66379a`。本组自有恢复脚本`Tools/Extensions/Data/Excel/Restore-MiniExcel.ps1`。
- 已执行首轮纯逻辑真实xlsx：`Temp/Extensions.Data.Excel/red-authoring`红0/8，`green-core-1`绿8/8。非Unity/AssetDatabase/GUI结果；SO接口后续补齐。本地候选Commit尚未形成，不依赖此时未提交内容接入主线。

## Save设计与兼容策略（实施前确定）

- 单一`SaveStore`负责BCL文件/索引/槽位/模型对象/设置/缓存/备份及同步异步；既有`SaveManager`仅负责主线程捕获Unity路径并暴露兼容门面，非第二存档权威。
- 保留对象/设置裸JSON用途；索引升级v2并纳入当前槽。检测到v1拒绝原地写入，提供显式复制迁移到不同新目录，拒绝源目标相同和已有目标冲突。没有真实旧档样本，只能验证合成合同，不能宣称历史档兼容。
- Archive仅提供显式选择的明文JSON导入到新槽，不扫描原目录、不自动转换、不接纳DES/零IV格式；旧`.sav`原件不修改。
- 取消在单文件原子提交点前有效，提交后报告成功；同根排他owner、同槽串行、跨槽可并发，关闭取消排队并排空。磁盘失败不推进对象缓存。
- 当前Save首轮真实BCL文件验证10/10绿；保留红例涉及自动覆盖损坏主件/吞IO等。后续取消/并发/迁移/恢复回归继续执行，不将首轮数量当作最终结果。
- 现有`SerializedDictionarySaveTests`将按新门面owner调整必要测试隔离，保留原磁盘/Unity断言；实际数据保护验证只用自有临时目录。

## 接口与边界决策

- Localization继续显式登记配置，避免Runtime/Editor自动全资产扫描；根Lease由登记句柄持有，条目借用不得超出登记生命周期。图片消费现有`ResourceImage`。
- 中文变体只在中文请求时提前尝试；其他语言按精确、English、显式列表顺序回退。查询语言和按ID查询使用同一候选顺序。
- Excel后端仅在`Integrations/Excel/Editor/`，不改主工程包依赖、不向Runtime引入解析器。C组仅在真实API落地后接线。
- Save仅在自有临时目录/复制样本验证；不自动读写或转换原Archive `.sav`，没有真实旧档样本不宣称兼容。
- Localization保留UGUI/TextMesh、Other反射（可绑定TMP公开`text`成员）、资源/网络图片及参数/附加文本；不安装TMP，也不携带联网翻译凭据、Youdao认证或Steam外部作者窗口。后两类非本次离线配置工作流的必要能力，明确未迁入，不伪装为可用；Other反射成员的IL2CPP保留由实际应用配置并待Player验证。
- Localization语言资产创建仍采用根引用的独立语言文件；显式移除语言只解除引用并可Undo，不删除可能共享的资产文件。作者窗口保留语言排序、搜索、全语言新增/复制/删除条目、类型/内容/备注编辑和保存，图片预览复用既有YL预览工具。

## 验证状态

- 已执行：路径、Git状态、B0祖先核验；读取实际Unity版本、manifest及正式Plan。PowerShell工具实际为5.1，脚本将按实际Shell或显式pwsh运行，不照搬7.x语法。
- Localization已执行纯逻辑12/12（回退、等长替换/改ID、重复/空项、格式及richText），先红后绿保留在`Temp/Extensions.Data.Localization/`；其余模块结果按最终证据补齐。
- 首轮组合源码辅助编译：`Tools/Extensions/Data/Verify-DataProfiles.ps1 -Run combined-1`退出0；完整Runtime/Editor-profile/Editor工具分别42/41/4个warning，新增普通EditMode测试源码8文件/0warning、PlayMode测试源码2文件/1warning均编译成功。没有执行其中任何Unity测试。较早Save门面在途状态造成的重复声明编译失败保留在工具调用记录，没有删输入绕过；门面落齐后真实全目录通过。
- 尚未执行：Unity编译、资产/GUI/PlayMode、Mono/IL2CPP Player、独立接收工程和原游戏非干扰门。完成后按证据更新。
- 许可：原Localization/SO为MIT 2024弦风课堂，Excel/Archive为MIT 2025弦风课堂。实读发现既有根许可证只有2021/2025版权，未包含2024；因此Localization在自有目录补完整`LICENSE.txt`，SO同样独立保留。共享许可汇总/根版权由后续集成统一维护，不以既有MIT文本替代遗漏的适用版权。

## 候选提交

尚未形成。本地候选提交由本会话按模块精确暂存；不Push。
