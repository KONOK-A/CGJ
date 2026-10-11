# Save（存档）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供本地存档能力：管理多个存档槽（创建 / 删除 / 排序 / 命名 / 查询），按“存档槽 + 文件名”保存和读取任意可序列化对象，并提供全局设置（Setting）的读写。

写入采用“临时文件 → 原子替换 → 保留上一份备份”的流程，单次异常写入不会损坏整份存档；JSON 损坏时自动尝试备份恢复。

## 位置

- 运行时：[`Runtime/Save/`](../../Runtime/Save/)
  - 文件读写辅助：`SaveFileIO.cs`（原子写入 / 备份恢复）
  - 序列化辅助：[`Serialization/`](../../Runtime/Save/Serialization/)
- 通用字典：[`Runtime/Shared/Serialization/SerializedDictionary.cs`](../../Runtime/Shared/Serialization/SerializedDictionary.cs)，归 `YLFramework.Foundation.Runtime`，不是 Save 专属类型。
- 原生 Inspector：[`Editor/Serialization/SerializedDictionaryDrawer.cs`](../../Editor/Serialization/SerializedDictionaryDrawer.cs)，归 Editor-only 的 `YLFramework.Editor`。

## 主要功能

- 存档槽：`CreateSaveItem`、`GetAllSaveItems`（内置 / 自定义排序，含名称排序）、`GetSaveItemWithID` / `TryGetSaveItem` / `HasSaveItem` / `GetSaveItemCount`、`DeleteSaveItem` / `DeleteAllSaveItems` / `DeleteAll`。
- 槽位信息：`DisplayName` / `Note`（可选）、`CreateTime` / `LastSaveTime`，`RenameSaveItem` / `SetSaveItemNote`。
- 对象存取：`SaveObject` / `LoadObject<T>` / `TryLoadObject<T>` / `LoadObjectOrDefault<T>` / `DeleteObject` / `HasObject` / `GetObjectNames`（按文件名与存档 ID 重载）。
- 全局设置：`LoadGlobalSetting<T>` / `TryLoadGlobalSetting<T>` / `SaveGlobalSetting` / `DeleteAllSetting`。
- 可靠性：原子写入（`.tmp` + `File.Replace`）、上一版本 `.bak` 备份（旧主文件缺失时自动提升备份）、损坏文件保留为 `.corrupt-时间戳`、`SaveSystemData.json` 损坏时优先备份恢复、文件名合法性校验（拒绝路径分隔符、`..`、末尾点 / 空格、Windows 保留设备名、超长名称）。
- 可测试 / 可替换存储：`StorageRoot` + `SetStorageRoot(path)`（默认 `Application.persistentDataPath`），存档数据懒加载。
- 序列化扩展：`SerializerSettings`（可添加 `JsonConverter`，仅作用于业务对象与全局设置）与 `SerializationFormat`，默认行为与旧存档一致；存档索引（SaveSystemData）使用内部固定设置序列化，不受用户 converter 影响。
- 版本扩展点：`CurrentDataVersion` 与 `MigrateSaveSystemData`（当前无实际迁移逻辑）；文件版本高于当前支持版本时拒绝读写并抛 `InvalidOperationException`，避免覆盖未来版本数据。
- 序列化辅助类型：Foundation 的统一 `SerializedDictionary<TKey,TValue>`（标准字典接口及 Unity 回调）；Save 自有 `SerializedVector3` / `SerializedVector2`、`SerializedColor` 及对应扩展。

## 主要类型

- `SaveManager`：存档静态入口（含 `SaveItem`、`SaveOrder`）。
- `SaveManager.SaveItem`：存档槽数据（编号、创建 / 最后保存时间、显示名、备注）。
- `SaveFileIO`（internal）：原子写入、备份恢复与 JSON 读写。
- `YLFramework.Foundation.SerializedDictionary<TKey,TValue>`：唯一通用字典；`YLFramework.Services.Save` 不再定义另一份字典。
- `SerializedVector3` / `SerializedVector2` / `SerializedColor`：Save 的可序列化值类型。

## 依赖

- `Newtonsoft.Json`（Unity 的 com.unity.nuget.newtonsoft-json 包，经插件自动引用）负责对象序列化；其余为 UnityEngine。
- Foundation 字典自身不引用 Json.NET、Save/Core 或框架 Editor。Save 通过其 `IDictionary` contract 使用现有 Json.NET，不需 converter。

## SerializedDictionary 用法与边界

```csharp
using UnityEngine;
using YLFramework.Foundation;

public class ExampleConfig : MonoBehaviour
{
    [SerializeField] private SerializedDictionary<string, int> scores = new SerializedDictionary<string, int>();

    public void SetScore(string id, int score)
    {
        scores[id] = score;
    }

    public bool TryReadScore(string id, out int score) => scores.TryGetValue(id, out score);
}
```

直接泛型字段可用，也可声明 `[Serializable]` 的具体非泛型子类；字段所属业务 asmdef 应直接引用 `YLFramework.Foundation.Runtime`。无需挂载字典组件或手动 Refresh。

- 运行时：标准 `IDictionary` / `IReadOnlyDictionary`，缺键索引器抛 `KeyNotFoundException`，`Add` 拒绝重复，`TryAdd` 返回是否新增；只拒绝 CLR null 键，允许空字符串及 default/null 值。来源构造复制条目，不借用外部字典；Keys/Values 是只读实时视图。
- Unity：`datas/Key/Value` 原始条目是事实源，修改写穿列表；原生 Inspector 展示条目、计数及重复行号，支持字段编辑、单对象增删/重排、Undo/Redo 与 Prefab override/apply/revert。只有首个有效键参与运行时，重复/无效原始行不自动删除；修复 Key 后即可重新有效。`Remove(key)` 删除全部同键行，`Clear()` 包括无效行。
- 多对象：逐目标诊断，保留 mixed-value 字段编辑；禁止所有批量列表增删/重排，长度不同或字段缺失时禁止整组字段编辑，须单独选择目标修复。相同位置的值编辑不复制首个目标的键。
- 类型限制：Key/Value 必须符合 Unity 字段序列化规则；Drawer 不为任意泛型键、固定缓冲、嵌套容器、多态或 alias 建立自定义 serializer。Unity 无法通过 `boxedValue` 诊断的键会明确提示，而非按 ToString 判断重复。Unity inline 序列化不保证任意 null 条目保真。
- 对象键：不把 Unity fake-null 当 CLR null 自动清除。原生 `SerializedProperty` 的 None/Missing 无法可靠区分二者，Inspector 标为“待确认”，不虚报精确有效数；跨会话持久化建议使用稳定 ID，不以 Unity 对象引用作 JSON 键。
- UI：同时提供 UI Toolkit `PropertyField`/binding 和 IMGUI 实现；IMGUI 的长诊断可在固定高区域内滚动，避免窄列覆盖后续字段。已验证 Unity 2022.3 原生 GenericInspector 与 IMGUI 自定义 Inspector；不依赖 Odin，也不承诺第三方抢占的 Editor 会转发原生 Drawer。本工程全局 OdinEditor 不作为原生 Inspector 的验收替代。
- JSON：当前 SaveManager/Json.NET 输出裸对象，例如 `{"a":2,"b":4}`，只保存有效字典投影；不导出 Unity 原始无效行。支持该后端可往返的 string/int/enum 键及相应值，复杂键的 ToString 不代表可逆编码。自定义 converter/resolver、元数据保留名称（`$type/$id/$ref`）、Unity 对象引用与任意保引用设置需调用方另验实际磁盘往返。
- 缓存：`SaveObject(dictionary, "scores", slot.SaveID)` 可直接写盘；随后 `LoadObject<SerializedDictionary<string,int>>("scores", slot.SaveID)` 可能命中同一内存对象，不是磁盘验证。隔离测试才使用独占 root、清缓存及重新读盘，不在真实用户目录为测试切换或清除存档状态。
- 兼容变化：旧 `YLFramework.Core.SerializedDictionary/DictionaryItem` 与 `YLFramework.Services.Save.SerializedDictionary` 已退出，没有 facade 或旧双列表 reader。正式容器保留自有脚本 GUID 与 `datas/Key/Value`，但 namespace/程序集身份改变；不保证旧 assembly-qualified name、SerializeReference 或旧 JSON 包装自动兼容。没有已查实待迁移的真实 YL 资产/存档，发现历史数据时应基于样本另做定向迁移。

2.5-C 的 `SerializedDictionaryEditorTests` 与 `SerializedDictionarySaveTests` 覆盖原生编辑、Prefab 临时资产保存重开及现有 SaveManager 的隔离写盘/清缓存/新实例读回；这些是字典专题证据，不是 Save 全系统故障恢复验收。

## 当前状态

已完成，测试：`Tests/EditMode/SaveManagerTests.cs` 共 55 项（根目录切换、槽位管理、排序、对象存取、缓存语义、磁盘状态组合与备份恢复、旧格式兼容、未来版本拒绝、全局设置、序列化配置、文件名平台风险）。

未提供异步 API（见备注）。

## 备注

- 存档与设置默认写入 `Application.persistentDataPath/SaveData` 与 `/Setting`；内部数据文件为 `SaveData/SaveSystemData.json`（名称与 JSON 成员名保持不变以兼容旧存档）；每次覆盖写入会保留同目录 `.bak` 备份。
- 兼容性：旧版 `SaveSystemData.json`（`SaveItemDict` + `SaveID` / `CreateTime` / `LastSaveTime`）可直接加载；旧数据缺少 `DisplayName` / `Note` / `DataVersion` 时按默认值处理，首次加载会补写 `DataVersion`。修复了旧实现中 `LastSaveTime` 重载后丢失的问题。
- 文件名（`saveFileName` / 全局设置 `fileName`）不允许包含路径分隔符、`..`、非法字符、末尾点 / 空格、Windows 保留设备名（CON / PRN / AUX / NUL / COMx / LPTx）或超过 200 字符，非法时抛 `ArgumentException`；不支持子目录。
- 不支持多线程 / 多进程并发写同一文件；内存缓存使 `LoadObject` 返回同一实例，直到 `DeleteObject` / `SetStorageRoot` / 重载。
- `RenameSaveItem` / `SetSaveItemNote` 写盘失败会回滚内存中的显示名 / 备注，保持与磁盘一致；`SaveObject` 的“保存时间”更新在对象文件写入成功之后进行。
- 异步 API 本轮未实现：存档对象是任意用户对象图，无法保证在后台线程上安全（可能包含 Unity 主线程对象）；`JsonConvert` + 共享缓存也没有清晰的多线程语义，保持同步实现更可靠。
- 编辑器下不再于静态构造中自动初始化（改为首次 API 调用懒加载），避免 EditMode / 编辑器工具意外读写真实存档目录。
