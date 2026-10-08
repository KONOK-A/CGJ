# Save（存档）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供本地存档能力：管理多个存档槽（创建 / 删除 / 排序 / 命名 / 查询），按“存档槽 + 文件名”保存和读取任意可序列化对象，并提供全局设置（Setting）的读写。

写入采用“临时文件 → 原子替换 → 保留上一份备份”的流程，单次异常写入不会损坏整份存档；JSON 损坏时自动尝试备份恢复。

## 位置

- 运行时：[`Runtime/Save/`](../../Runtime/Save/)
  - 文件读写辅助：`SaveFileIO.cs`（原子写入 / 备份恢复）
  - 序列化辅助：[`Serialization/`](../../Runtime/Save/Serialization/)

## 主要功能

- 存档槽：`CreateSaveItem`、`GetAllSaveItems`（内置 / 自定义排序，含名称排序）、`GetSaveItemWithID` / `TryGetSaveItem` / `HasSaveItem` / `GetSaveItemCount`、`DeleteSaveItem` / `DeleteAllSaveItems` / `DeleteAll`。
- 槽位信息：`DisplayName` / `Note`（可选）、`CreateTime` / `LastSaveTime`，`RenameSaveItem` / `SetSaveItemNote`。
- 对象存取：`SaveObject` / `LoadObject<T>` / `TryLoadObject<T>` / `LoadObjectOrDefault<T>` / `DeleteObject` / `HasObject` / `GetObjectNames`（按文件名与存档 ID 重载）。
- 全局设置：`LoadGlobalSetting<T>` / `TryLoadGlobalSetting<T>` / `SaveGlobalSetting` / `DeleteAllSetting`。
- 可靠性：原子写入（`.tmp` + `File.Replace`）、上一版本 `.bak` 备份（旧主文件缺失时自动提升备份）、损坏文件保留为 `.corrupt-时间戳`、`SaveSystemData.json` 损坏时优先备份恢复、文件名合法性校验（拒绝路径分隔符、`..`、末尾点 / 空格、Windows 保留设备名、超长名称）。
- 可测试 / 可替换存储：`StorageRoot` + `SetStorageRoot(path)`（默认 `Application.persistentDataPath`），存档数据懒加载。
- 序列化扩展：`SerializerSettings`（可添加 `JsonConverter`，仅作用于业务对象与全局设置）与 `SerializationFormat`，默认行为与旧存档一致；存档索引（SaveSystemData）使用内部固定设置序列化，不受用户 converter 影响。
- 版本扩展点：`CurrentDataVersion` 与 `MigrateSaveSystemData`（当前无实际迁移逻辑）；文件版本高于当前支持版本时拒绝读写并抛 `InvalidOperationException`，避免覆盖未来版本数据。
- 序列化辅助类型：`SerializedDictionary<K,V>`（支持 `ISerializationCallbackReceiver`）、`SerializedVector3` / `SerializedVector2`、`SerializedColor` 及对应扩展。

## 主要类型

- `SaveManager`：存档静态入口（含 `SaveItem`、`SaveOrder`）。
- `SaveManager.SaveItem`：存档槽数据（编号、创建 / 最后保存时间、显示名、备注）。
- `SaveFileIO`（internal）：原子写入、备份恢复与 JSON 读写。
- `SerializedDictionary<K, V>` / `SerializedVector3` / `SerializedVector2` / `SerializedColor`：可序列化容器与值类型。

## 依赖

- `Newtonsoft.Json`（Unity 的 com.unity.nuget.newtonsoft-json 包，经插件自动引用）负责对象序列化；其余为 UnityEngine。

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
