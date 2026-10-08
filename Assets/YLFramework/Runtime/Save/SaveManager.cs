using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// 存档管理器：管理存档槽（<see cref="SaveItem"/>）的创建 / 查询 / 改名 / 备注 / 删除，
    /// 在存档槽内以 JSON 文件为单位读写引用类型对象，并提供与存档槽隔离的全局设置读写。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 目录布局（<see cref="StorageRoot"/> 默认 <c>Application.persistentDataPath</c>）：
    /// <c>SaveData/SaveSystemData.json</c> 为存档索引；<c>SaveData/{saveID}/{fileName}.json</c> 为槽内对象文件；
    /// <c>Setting/{fileName}.json</c> 为全局设置文件。
    /// </para>
    /// <para>
    /// 写盘采用“临时文件 + 替换 + <c>.bak</c> 备份”，读取失败时自动回退备份；对象保存是整文件覆盖而非增量合并。
    /// 存档索引为懒加载，并在首次访问时校验版本：索引版本高于 <see cref="CurrentDataVersion"/> 时抛出异常拒绝读写；
    /// 索引损坏且无法从备份恢复时，保留为 <c>.corrupt-时间戳</c> 文件并重置为空索引。
    /// </para>
    /// </remarks>
    public static class SaveManager
    {
        private const string SaveDirName = "SaveData";
        private const string SettingDirName = "Setting";
        private const string SaveSystemDataFileName = "SaveSystemData.json";

        /// <summary>
        /// 当前存档数据结构版本。数据格式发生不兼容变化时递增，并在 MigrateSaveSystemData 中处理迁移。
        /// </summary>
        public const int CurrentDataVersion = 1;

        private static SaveSystemData _saveSystemData;
        private static string _storageRootOverride;

        // 对象缓存：saveID →（对象文件名 → 对象实例）。
        // SaveObject 成功后写入；LoadObject 命中时返回同一实例，因此外部修改缓存对象后需再次保存才会写盘。
        private static readonly Dictionary<int, Dictionary<string, object>> CacheDict = new();

        /// <summary>
        /// 用户对象（存档对象与全局设置）的 JSON 序列化设置，默认与旧版本一致，可添加 JsonConverter / ContractResolver 等扩展。
        /// </summary>
        /// <remarks>框架自身的索引元数据使用固定的内置设置，不受此处修改影响。</remarks>
        public static JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings();

        /// <summary>
        /// SaveSystemData（存档索引）专用的固定序列化设置，不向用户开放，
        /// 避免自定义 converter / resolver 意外破坏框架自身 metadata。
        /// </summary>
        private static readonly JsonSerializerSettings MetadataSerializerSettings = new JsonSerializerSettings();

        /// <summary>
        /// 写盘时的 JSON 输出格式，默认 <see cref="Formatting.Indented"/>（与旧存档一致）；对用户对象与索引元数据统一生效。
        /// </summary>
        public static Formatting SerializationFormat { get; set; } = Formatting.Indented;

        /// <summary>
        /// 当前存档根目录：默认为 Application.persistentDataPath，可通过 <see cref="SetStorageRoot"/> 替换。
        /// </summary>
        public static string StorageRoot =>
            string.IsNullOrEmpty(_storageRootOverride) ? Application.persistentDataPath : _storageRootOverride;

        /// <summary>
        /// 替换存档根目录（传 null 或空字符串恢复默认的 Application.persistentDataPath）。
        /// 会清空内存中的存档索引与对象缓存，下次访问时从新目录重新加载或创建。
        /// </summary>
        /// <param name="rootPath">新的存储根目录；null / 空字符串表示恢复默认。</param>
        /// <remarks>只切换路径，不复制、不迁移旧目录中的数据。</remarks>
        public static void SetStorageRoot(string rootPath)
        {
            _storageRootOverride = string.IsNullOrEmpty(rootPath) ? null : rootPath;
            _saveSystemData = null;
            CacheDict.Clear();
        }

        // 存档索引与各槽对象文件所在目录：{StorageRoot}/SaveData
        private static string SaveDirPath => Path.Combine(StorageRoot, SaveDirName);
        // 全局设置文件所在目录：{StorageRoot}/Setting
        private static string SettingDirPath => Path.Combine(StorageRoot, SettingDirName);
        // 存档索引文件路径：{StorageRoot}/SaveData/SaveSystemData.json
        private static string SaveSystemDataPath => Path.Combine(SaveDirPath, SaveSystemDataFileName);

        #region 存档设置

        // 存档索引的内存模型：数据版本 + 全部存档槽字典，整份序列化为 SaveSystemData.json。
        [Serializable]
        private class SaveSystemData
        {
            public int DataVersion = CurrentDataVersion;
            public Dictionary<int, SaveItem> SaveItemDict = new();
            // 当前最小的未占用编号（从 0 开始扫描），新槽编号由此分配。
            public int AvailableSaveID => GetAvailableSaveID();

            public bool HaveSaveItem(int saveID)
            {
                return SaveItemDict.ContainsKey(saveID);
            }

            public SaveItem GetSaveItem(int saveID)
            {
                return SaveItemDict.GetValueOrDefault(saveID);
            }

            public void AddSaveItem(SaveItem saveItem)
            {
                SaveItemDict[saveItem.SaveID] = saveItem;
            }

            public void DelSaveItem(int saveID)
            {
                SaveItemDict.Remove(saveID);
            }

            private int GetAvailableSaveID()
            {
                int id = 0;
                while (true)
                {
                    if (!SaveItemDict.ContainsKey(id))
                    {
                        return id;
                    }

                    id += 1;
                }
            }
        }

        /// <summary>
        /// 初始化存档管理器数据（懒加载）。SaveSystemData.json 缺失时创建空白数据；
        /// 损坏时优先使用 .bak 备份，两者都不可用时保留损坏文件并重置为空数据。
        /// 任何写入失败都会保持未初始化状态，下次调用重新尝试；文件版本高于当前支持版本时拒绝读写。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_saveSystemData != null) return;
            CheckAndCreateDir();

            var status = SaveFileIO.ReadJson<SaveSystemData>(SaveSystemDataPath, MetadataSerializerSettings,
                out var data, out var error);
            if (status is SaveReadStatus.Success or SaveReadStatus.RecoveredFromBackup)
            {
                if (data.DataVersion > CurrentDataVersion)
                {
                    throw new InvalidOperationException(
                        $"#YLFramework SaveSystemData 版本 {data.DataVersion} 高于当前支持的版本 {CurrentDataVersion}，已停止读写以保护数据：{SaveSystemDataPath}");
                }

                if (data.DataVersion < CurrentDataVersion)
                {
                    MigrateSaveSystemData(data, data.DataVersion);
                    data.DataVersion = CurrentDataVersion;
                    SaveMetadataFile(data, SaveSystemDataPath);
                }

                _saveSystemData = data;
                if (status == SaveReadStatus.RecoveredFromBackup)
                {
                    Debug.LogWarning($"#YLFramework SaveSystemData 已损坏，已从备份恢复：{SaveSystemDataPath}");
                }

                return;
            }

            if (status == SaveReadStatus.Failed)
            {
                var preservedPath = SaveFileIO.PreserveCorruptFile(SaveSystemDataPath);
                Debug.LogError(
                    $"#YLFramework SaveSystemData 读取失败，已重置为空白存档数据；原文件保留在：{preservedPath ?? SaveSystemDataPath}\n{error}");
            }

            var fresh = new SaveSystemData();
            SaveMetadataFile(fresh, SaveSystemDataPath);
            _saveSystemData = fresh;
        }

        /// <summary>
        /// 存档数据版本迁移入口（当前无实际迁移逻辑，后续数据结构变更时在此按版本处理）。
        /// </summary>
        private static void MigrateSaveSystemData(SaveSystemData data, int fromVersion)
        {
            // 示例：
            // if (fromVersion < 2) { ... }
        }

        /// <summary>
        /// 将内存中的存档索引整体写回 SaveSystemData.json（原子写入 + 备份）。
        /// </summary>
        private static void UpdateSaveSystemData()
        {
            SaveMetadataFile(_saveSystemData, SaveSystemDataPath);
        }

        //保存框架 metadata（固定序列化设置，不受用户 SerializerSettings 影响）
        private static void SaveMetadataFile(object value, string path)
        {
            SaveFileIO.WriteJson(value, path, MetadataSerializerSettings, SerializationFormat);
        }

        /// <summary>
        /// <see cref="GetAllSaveItems(SaveOrder)"/> 的内置排序方式。
        /// </summary>
        public enum SaveOrder
        {
            /// <summary>不排序，按索引字典内部顺序返回（顺序不保证稳定）。</summary>
            None,

            /// <summary>按创建时间升序（早创建的在前）。</summary>
            CreateTime,

            /// <summary>按创建时间降序（晚创建的在前）。</summary>
            CreateTimeDescend,

            /// <summary>按最近保存时间升序。</summary>
            SaveTime,

            /// <summary>按最近保存时间降序（最近保存的在前）。</summary>
            SaveTimeDescend,

            /// <summary>按存档编号升序。</summary>
            SaveID,

            /// <summary>按存档编号降序。</summary>
            SaveIDDescend,

            /// <summary>按显示名升序（null 显示名按空字符串处理）。</summary>
            Name,

            /// <summary>按显示名降序（null 显示名按空字符串处理）。</summary>
            NameDescend
        }

        /// <summary>
        /// 获取索引中全部存档槽的列表快照，并按指定方式排序。
        /// </summary>
        /// <param name="saveOrder">内置排序方式，默认 <see cref="SaveOrder.None"/>（不排序）。</param>
        /// <returns>包含全部存档槽引用的新列表；对列表增删不会影响索引，但对元素（<see cref="SaveItem"/>）的修改会立即反映到内存索引。</returns>
        public static List<SaveItem> GetAllSaveItems(SaveOrder saveOrder = SaveOrder.None)
        {
            EnsureInitialized();
            var saves = saveOrder switch
            {
                SaveOrder.None => _saveSystemData.SaveItemDict.Values.ToList(),
                SaveOrder.CreateTime => _saveSystemData.SaveItemDict.Values.OrderBy(s => s.CreateTime).ToList(),
                SaveOrder.CreateTimeDescend => _saveSystemData.SaveItemDict.Values.OrderByDescending(s => s.CreateTime)
                    .ToList(),
                SaveOrder.SaveTime => _saveSystemData.SaveItemDict.Values.OrderBy(s => s.LastSaveTime).ToList(),
                SaveOrder.SaveTimeDescend => _saveSystemData.SaveItemDict.Values.OrderByDescending(s => s.LastSaveTime)
                    .ToList(),
                SaveOrder.SaveID => _saveSystemData.SaveItemDict.Values.OrderBy(s => s.SaveID).ToList(),
                SaveOrder.SaveIDDescend => _saveSystemData.SaveItemDict.Values.OrderByDescending(s => s.SaveID)
                    .ToList(),
                SaveOrder.Name => _saveSystemData.SaveItemDict.Values.OrderBy(s => s.DisplayName ?? string.Empty)
                    .ToList(),
                SaveOrder.NameDescend => _saveSystemData.SaveItemDict.Values
                    .OrderByDescending(s => s.DisplayName ?? string.Empty).ToList(),
                _ => _saveSystemData.SaveItemDict.Values.ToList()
            };
            return saves;
        }

        /// <summary>
        /// 获取索引中全部存档槽的列表快照，并按自定义排序键排序。
        /// </summary>
        /// <typeparam name="T">排序键类型；需支持默认比较（OrderBy / OrderByDescending）。</typeparam>
        /// <param name="orderFunc">从存档槽提取排序键的委托。</param>
        /// <param name="isDescending">true 为降序，false（默认）为升序。</param>
        /// <returns>包含全部存档槽引用的新列表。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="orderFunc"/> 为 null。</exception>
        public static List<SaveItem> GetAllSaveItems<T>(Func<SaveItem, T> orderFunc, bool isDescending = false)
        {
            EnsureInitialized();
            var saves = !isDescending
                ? _saveSystemData.SaveItemDict.Values.OrderBy(orderFunc).ToList()
                : _saveSystemData.SaveItemDict.Values.OrderByDescending(orderFunc).ToList();
            return saves;
        }

        #endregion

        #region 关于存档

        /// <summary>
        /// 单个存档槽的元数据：编号、创建 / 最近保存时间，以及可选的显示名与备注。
        /// 由 <see cref="CreateSaveItem"/> 创建，统一保存在 SaveSystemData.json 中（不单独占文件）；
        /// 槽内对象文件存放在与 <see cref="SaveID"/> 同名的目录下。
        /// </summary>
        /// <remarks>
        /// 通过 <see cref="UpdateSaveTime"/> / <see cref="Rename"/> / <see cref="SetNote"/> 修改仅作用于内存实例；
        /// 持久化需使用 <see cref="SaveManager.RenameSaveItem(int, string)"/> 等管理器 API，或等待后续触发索引写盘的操作。
        /// </remarks>
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public class SaveItem
        {
            [JsonProperty("SaveID")] private int _saveID;
            [JsonProperty("CreateTime")] private DateTime _createTime;
            [JsonProperty("LastSaveTime")] private DateTime _lastSaveTime;

            /// <summary>
            /// 存档编号：由 <see cref="CreateSaveItem"/> 分配的最小可用非负整数，创建后不变，并用作槽目录名。
            /// </summary>
            public int SaveID => _saveID;

            /// <summary>
            /// 创建时间（本地时间）；创建后不变。
            /// </summary>
            public DateTime CreateTime => _createTime;

            /// <summary>
            /// 最近一次保存对象的时间（本地时间）；新建槽时等于创建时间。
            /// </summary>
            public DateTime LastSaveTime => _lastSaveTime;

            /// <summary>
            /// 存档显示名（可选，旧存档中为 null）；仅用于展示与排序，不参与文件路径与唯一性判断。
            /// </summary>
            [JsonProperty("DisplayName", NullValueHandling = NullValueHandling.Ignore)]
            public string DisplayName { get; set; }

            /// <summary>
            /// 存档备注（可选，旧存档中为 null）；仅用于展示，重命名或保存对象不会修改它。
            /// </summary>
            [JsonProperty("Note", NullValueHandling = NullValueHandling.Ignore)]
            public string Note { get; set; }

            /// <summary>
            /// 创建存档槽元数据。
            /// </summary>
            /// <param name="saveID">存档编号，调用方应保证在索引内唯一。</param>
            /// <param name="createTime">创建时间，同时作为初始的 <see cref="LastSaveTime"/>。</param>
            public SaveItem(int saveID, DateTime createTime)
            {
                _saveID = saveID;
                _createTime = createTime;
                _lastSaveTime = createTime;
            }

            /// <summary>
            /// 更新最近保存时间。
            /// </summary>
            /// <param name="lastSaveTime">新的最近保存时间（本地时间）。</param>
            /// <returns>this，支持链式调用。</returns>
            /// <remarks>仅改内存；写盘由调用方（如 <see cref="SaveManager.SaveObject(object, int)"/>）触发。</remarks>
            public SaveItem UpdateSaveTime(DateTime lastSaveTime)
            {
                _lastSaveTime = lastSaveTime;
                return this;
            }

            /// <summary>
            /// 设置显示名。
            /// </summary>
            /// <param name="displayName">新的显示名，可为 null。</param>
            /// <returns>this，支持链式调用。</returns>
            /// <remarks>仅改内存；持久化请使用 <see cref="SaveManager.RenameSaveItem(int, string)"/>（带写盘失败回滚）。</remarks>
            public SaveItem Rename(string displayName)
            {
                DisplayName = displayName;
                return this;
            }

            /// <summary>
            /// 设置备注。
            /// </summary>
            /// <param name="note">新的备注，可为 null。</param>
            /// <returns>this，支持链式调用。</returns>
            /// <remarks>仅改内存；持久化请使用 <see cref="SaveManager.SetSaveItemNote(int, string)"/>（带写盘失败回滚）。</remarks>
            public SaveItem SetNote(string note)
            {
                Note = note;
                return this;
            }
        }

        /// <summary>
        /// 判断索引中是否存在指定编号的存档槽（不检查槽内对象文件是否存在）。
        /// </summary>
        /// <param name="saveID">存档编号。</param>
        /// <returns>存在返回 true。</returns>
        public static bool HasSaveItem(int saveID)
        {
            EnsureInitialized();
            return _saveSystemData.HaveSaveItem(saveID);
        }

        /// <summary>
        /// 判断指定存档槽是否仍存在于索引中（按 <see cref="SaveItem.SaveID"/> 判断）。
        /// </summary>
        /// <param name="saveItem">存档槽；为 null 返回 false。</param>
        /// <returns>存在返回 true。</returns>
        public static bool HasSaveItem(SaveItem saveItem)
        {
            EnsureInitialized();
            return saveItem != null && _saveSystemData.HaveSaveItem(saveItem.SaveID);
        }

        /// <summary>
        /// 按编号获取索引中的存档槽实例。
        /// </summary>
        /// <param name="saveID">存档编号。</param>
        /// <param name="saveItem">找到时为索引内的实例；未找到为 null。</param>
        /// <returns>找到返回 true，否则返回 false（不输出错误日志）。</returns>
        public static bool TryGetSaveItem(int saveID, out SaveItem saveItem)
        {
            EnsureInitialized();
            return _saveSystemData.SaveItemDict.TryGetValue(saveID, out saveItem);
        }

        /// <summary>
        /// 获取索引中的存档槽数量。
        /// </summary>
        /// <returns>当前存档槽数量。</returns>
        public static int GetSaveItemCount()
        {
            EnsureInitialized();
            return _saveSystemData.SaveItemDict.Count;
        }

        /// <summary>
        /// 按编号获取索引中的存档槽实例。
        /// </summary>
        /// <param name="id">存档编号。</param>
        /// <returns>索引中的实例；不存在时返回 null（不输出错误日志）。</returns>
        /// <remarks>与 <see cref="TryGetSaveItem"/> 等价，仅返回形式不同。</remarks>
        public static SaveItem GetSaveItemWithID(int id)
        {
            EnsureInitialized();
            return _saveSystemData.HaveSaveItem(id) ? _saveSystemData.SaveItemDict[id] : null;
        }

        /// <summary>
        /// 新建一个空存档槽：编号取当前未被占用的最小非负整数，创建时间与最近保存时间均为当前本地时间，
        /// 并立即写入索引文件；槽目录与对象文件此时尚未创建，首次保存对象时才生成。
        /// </summary>
        /// <returns>新建的存档槽实例（即索引内实例）。</returns>
        /// <exception cref="IOException">索引写盘失败时原样抛出；新建槽仍保留在内存索引中。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限写入索引时原样抛出。</exception>
        public static SaveItem CreateSaveItem()
        {
            EnsureInitialized();
            var saveItem = new SaveItem(_saveSystemData.AvailableSaveID, DateTime.Now);
            _saveSystemData.AddSaveItem(saveItem);
            //更新SaveSystemData 写入磁盘
            UpdateSaveSystemData();
            return saveItem;
        }

        /// <summary>
        /// 修改存档槽显示名并立即写入索引；不影响存档编号、槽目录与对象文件。
        /// </summary>
        /// <param name="saveID">存档编号。</param>
        /// <param name="displayName">新的显示名，可为 null（表示清除显示名）。</param>
        /// <returns>存档槽存在且写盘成功返回 true；编号不存在返回 false（不写盘）。</returns>
        /// <exception cref="Exception">写盘失败时原样抛出；内存显示名会回滚为修改前的值。</exception>
        public static bool RenameSaveItem(int saveID, string displayName)
        {
            EnsureInitialized();
            if (!_saveSystemData.SaveItemDict.TryGetValue(saveID, out var saveItem)) return false;
            var previous = saveItem.DisplayName;
            saveItem.Rename(displayName);
            try
            {
                UpdateSaveSystemData();
            }
            catch
            {
                // 写盘失败时回滚内存状态，保持与磁盘一致
                saveItem.Rename(previous);
                throw;
            }

            return true;
        }

        /// <summary>
        /// 修改存档槽显示名并立即写入索引。
        /// </summary>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <param name="displayName">新的显示名，可为 null。</param>
        /// <returns>见 <see cref="RenameSaveItem(int, string)"/>。</returns>
        public static bool RenameSaveItem(SaveItem saveItem, string displayName)
        {
            return saveItem != null && RenameSaveItem(saveItem.SaveID, displayName);
        }

        /// <summary>
        /// 设置存档槽备注并立即写入索引。
        /// </summary>
        /// <param name="saveID">存档编号。</param>
        /// <param name="note">新的备注，可为 null（表示清除备注）。</param>
        /// <returns>存档槽存在且写盘成功返回 true；编号不存在返回 false（不写盘）。</returns>
        /// <exception cref="Exception">写盘失败时原样抛出；内存备注会回滚为修改前的值。</exception>
        public static bool SetSaveItemNote(int saveID, string note)
        {
            EnsureInitialized();
            if (!_saveSystemData.SaveItemDict.TryGetValue(saveID, out var saveItem)) return false;
            var previous = saveItem.Note;
            saveItem.SetNote(note);
            try
            {
                UpdateSaveSystemData();
            }
            catch
            {
                // 写盘失败时回滚内存状态，保持与磁盘一致
                saveItem.SetNote(previous);
                throw;
            }

            return true;
        }

        /// <summary>
        /// 设置存档槽备注并立即写入索引。
        /// </summary>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <param name="note">新的备注，可为 null。</param>
        /// <returns>见 <see cref="SetSaveItemNote(int, string)"/>。</returns>
        public static bool SetSaveItemNote(SaveItem saveItem, string note)
        {
            return saveItem != null && SetSaveItemNote(saveItem.SaveID, note);
        }

        /// <summary>
        /// 删除存档槽：递归删除以编号命名的整个槽目录（含全部对象文件与备份），
        /// 从索引移除并写盘，同时清除该槽的对象缓存；编号不存在时不做任何事。
        /// </summary>
        /// <param name="saveID">存档编号。</param>
        /// <exception cref="IOException">槽目录删除失败（如文件被占用）时原样抛出；此时索引与缓存尚未变更。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限删除目录时原样抛出。</exception>
        public static void DeleteSaveItem(int saveID)
        {
            EnsureInitialized();
            if (!_saveSystemData.HaveSaveItem(saveID)) return;
            var saveItemPath = GetSaveDirPath(saveID, false);
            if (saveItemPath != null)
            {
                //把整个存档文件递归删除
                Directory.Delete(saveItemPath, true);
            }

            _saveSystemData.DelSaveItem(saveID);
            //更新SaveSystemData 写入磁盘
            UpdateSaveSystemData();
            //删除存档缓存 如有
            DeleteCache(saveID);
        }

        /// <summary>
        /// 删除指定存档槽（按 <see cref="SaveItem.SaveID"/>）；为 null 时不做任何事。
        /// </summary>
        /// <param name="saveItem">目标存档槽，可为 null。</param>
        public static void DeleteSaveItem(SaveItem saveItem)
        {
            if (saveItem == null) return;
            DeleteSaveItem(saveItem.SaveID);
        }

        /// <summary>
        /// 删除全部存档槽：递归删除 SaveData 目录（含索引文件），重新创建目录并将索引重置为空白，同时清空对象缓存。
        /// 全局设置不受影响。
        /// </summary>
        /// <exception cref="IOException">目录删除失败时原样抛出；此时索引与缓存保持不变。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限删除目录时原样抛出。</exception>
        public static void DeleteAllSaveItems()
        {
            EnsureInitialized();
            if (Directory.Exists(SaveDirPath))
            {
                // 直接删除目录
                Directory.Delete(SaveDirPath, true);
            }

            CheckAndCreateDir();
            _saveSystemData = new SaveSystemData();
            UpdateSaveSystemData();
            ClearCache();
        }

        /// <summary>
        /// 删除全部持久化数据：所有存档槽（含索引）与所有全局设置，并清空对象缓存。
        /// </summary>
        /// <exception cref="IOException">目录删除失败时原样抛出。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限删除时原样抛出。</exception>
        public static void DeleteAll()
        {
            ClearCache();
            DeleteAllSaveItems();
            DeleteAllSetting();
        }

        #endregion

        #region 关于对象

        /// <summary>
        /// 将对象序列化为 JSON，保存到指定存档槽的 <c>{saveFileName}.json</c> 文件中（整文件覆盖，不合并旧内容）。
        /// </summary>
        /// <param name="saveObject">要保存的对象（引用类型，需可被当前 <see cref="SerializerSettings"/> 序列化）。</param>
        /// <param name="saveFileName">对象文件名（不含扩展名），需满足 <see cref="SaveFileIO.IsValidFileName"/>。</param>
        /// <param name="saveID">目标存档槽编号，默认 0。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <exception cref="IOException">写盘失败时原样抛出；写入采用临时文件 + 替换（旧内容转 <c>.bak</c>），降低中断损坏已有内容的风险。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限写入时原样抛出。</exception>
        /// <remarks>
        /// 存档槽不存在时仅输出错误日志并直接返回（不抛异常）。写入成功后更新该槽的 <see cref="SaveItem.LastSaveTime"/> 并写盘索引，
        /// 且把本次传入的对象实例写入缓存——之后 <see cref="LoadObject{T}(string, int)"/> 会返回同一实例。
        /// </remarks>
        public static void SaveObject(object saveObject, string saveFileName, int saveID = 0)
        {
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            ValidateFileName(saveFileName);
            EnsureInitialized();
            if (!TryGetSaveItem(saveID, out var saveItem))
            {
                Debug.LogError($"#YLFramework 没有该存档项，非法访问 :{saveID}");
                return;
            }

            var dirPath = GetSaveDirPath(saveID, true);
            //实际保存该对象的文件路径
            var objSavePath = Path.Combine(dirPath, saveFileName + ".json");
            //具体的保存
            SaveFile(saveObject, objSavePath);
            //更新存档时间
            saveItem.UpdateSaveTime(DateTime.Now);
            //更新SaveSystemData 写入磁盘
            UpdateSaveSystemData();
            //更新缓存
            SetCache(saveID, saveFileName, saveObject);
        }

        /// <summary>
        /// 将对象序列化为 JSON，保存到指定存档槽的 <c>{saveFileName}.json</c> 文件中（整文件覆盖）。
        /// </summary>
        /// <param name="saveObject">要保存的对象；为 null 抛出异常。</param>
        /// <param name="saveFileName">对象文件名（不含扩展名），同槽内同名文件整体覆盖。</param>
        /// <param name="saveItem">目标存档槽；为 null 时静默返回。应传入框架返回的实例，其 <see cref="SaveItem.LastSaveTime"/> 更新才会写入索引。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null（该检查先于 <paramref name="saveItem"/> 为 null 的静默返回）。</exception>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <exception cref="IOException">写盘失败时原样抛出。</exception>
        /// <remarks><paramref name="saveItem"/> 的编号在索引中不存在时仅输出错误日志并返回。</remarks>
        public static void SaveObject(object saveObject, string saveFileName, SaveItem saveItem)
        {
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            if (saveItem == null) return;
            ValidateFileName(saveFileName);
            EnsureInitialized();

            //没有该序号的存档则直接返回
            if (!_saveSystemData.HaveSaveItem(saveItem.SaveID))
            {
                Debug.LogError($"#YLFramework 没有该存档项，非法访问 :{saveItem.SaveID}");
                return;
            }

            var dirPath = GetSaveDirPath(saveItem.SaveID, true);
            //文件路径
            var objSavePath = Path.Combine(dirPath, saveFileName + ".json");
            //具体的保存
            SaveFile(saveObject, objSavePath);
            //更新存档时间
            saveItem.UpdateSaveTime(DateTime.Now);
            //更新SaveSystemData 写入磁盘
            UpdateSaveSystemData();
            //更新缓存
            SetCache(saveItem.SaveID, saveFileName, saveObject);
        }

        /// <summary>
        /// 将对象保存到指定存档槽，文件名取对象的运行时类型名。
        /// </summary>
        /// <param name="saveObject">要保存的对象；为 null 抛出异常（<paramref name="saveItem"/> 非 null 时）。</param>
        /// <param name="saveItem">目标存档槽；为 null 时静默返回（即使 <paramref name="saveObject"/> 也为 null）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null 且 <paramref name="saveItem"/> 非 null。</exception>
        /// <remarks>同槽内保存多个同类型对象会互相覆盖；需要并存时请使用显式文件名的重载。</remarks>
        public static void SaveObject(object saveObject, SaveItem saveItem)
        {
            if (saveItem == null) return;
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            SaveObject(saveObject, saveObject.GetType().Name, saveItem);
        }

        /// <summary>
        /// 将对象保存到指定存档槽，文件名取对象的运行时类型名。
        /// </summary>
        /// <param name="saveObject">要保存的对象；为 null 抛出异常。</param>
        /// <param name="saveID">目标存档槽编号，默认 0。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null。</exception>
        /// <remarks>同槽内保存多个同类型对象会互相覆盖；需要并存时请使用显式文件名的重载。</remarks>
        public static void SaveObject(object saveObject, int saveID = 0)
        {
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            SaveObject(saveObject, saveObject.GetType().Name, saveID);
        }

        /// <summary>
        /// 从指定存档槽读取 <c>{saveFileName}.json</c> 并反序列化为 <typeparamref name="T"/>。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型），需与保存时写入的 JSON 结构兼容。</typeparam>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>
        /// 反序列化得到的对象；以下情况返回 null：存档槽不存在、文件不存在、文件损坏且无法从备份恢复、反序列化结果为空。
        /// </returns>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <remarks>
        /// 缓存优先：该槽 / 文件名在本会话中已保存或加载过时，直接返回缓存的同一实例（不重新读盘）；
        /// 修改缓存对象不会自动持久化，需再次调用保存。存档槽不存在或读取失败会输出错误日志。
        /// </remarks>
        public static T LoadObject<T>(string saveFileName, int saveID = 0) where T : class
        {
            ValidateFileName(saveFileName);
            EnsureInitialized();
            //没有该序号的存档则直接返回
            if (!_saveSystemData.HaveSaveItem(saveID))
            {
                Debug.LogError($"#YLFramework 没有该存档项，非法访问 :{saveID}");
                return null;
            }

            return LoadObjectInternal<T>(saveFileName, saveID);
        }

        /// <summary>
        /// 从指定存档槽读取对象（文件名显式指定）。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveItem">目标存档槽；为 null 返回 null。</param>
        /// <returns>读取结果，语义见 <see cref="LoadObject{T}(string, int)"/>。</returns>
        public static T LoadObject<T>(string saveFileName, SaveItem saveItem) where T : class
        {
            if (saveItem == null) return null;
            return LoadObject<T>(saveFileName, saveItem.SaveID);
        }

        /// <summary>
        /// 从指定存档槽读取对象，文件名取 <typeparamref name="T"/> 的类型名。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="saveItem">目标存档槽；为 null 返回 null。</param>
        /// <returns>读取结果，语义见 <see cref="LoadObject{T}(string, int)"/>。</returns>
        public static T LoadObject<T>(SaveItem saveItem) where T : class
        {
            return saveItem == null ? null : LoadObject<T>(typeof(T).Name, saveItem.SaveID);
        }

        /// <summary>
        /// 从指定存档槽读取对象，文件名取 <typeparamref name="T"/> 的类型名。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>读取结果，语义见 <see cref="LoadObject{T}(string, int)"/>。</returns>
        public static T LoadObject<T>(int saveID = 0) where T : class
        {
            return LoadObject<T>(typeof(T).Name, saveID);
        }

        /// <summary>
        /// 尝试读取对象；与 <see cref="LoadObject{T}(string, int)"/> 相同的读取流程，用返回值表示成功与否。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="value">成功时为读取到的对象，失败为 null。</param>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>读取到非 null 对象返回 true；存档槽不存在、文件不存在、读取失败或结果为空返回 false。</returns>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <remarks>存档槽 / 文件不存在时不输出错误日志；文件损坏等读取失败仍会输出错误日志。</remarks>
        public static bool TryLoadObject<T>(out T value, string saveFileName, int saveID = 0) where T : class
        {
            value = default;
            ValidateFileName(saveFileName);
            EnsureInitialized();
            if (!_saveSystemData.HaveSaveItem(saveID)) return false;
            value = LoadObjectInternal<T>(saveFileName, saveID);
            return value != null;
        }

        /// <summary>
        /// 尝试读取对象（文件名显式指定）。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="value">成功时为读取到的对象，失败为 null。</param>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <returns>读取成功返回 true。</returns>
        public static bool TryLoadObject<T>(out T value, string saveFileName, SaveItem saveItem) where T : class
        {
            value = default;
            if (saveItem == null) return false;
            return TryLoadObject(out value, saveFileName, saveItem.SaveID);
        }

        /// <summary>
        /// 尝试读取对象（文件名取类型名）。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="value">成功时为读取到的对象，失败为 null。</param>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <returns>读取成功返回 true。</returns>
        public static bool TryLoadObject<T>(out T value, SaveItem saveItem) where T : class
        {
            value = default;
            if (saveItem == null) return false;
            return TryLoadObject(out value, typeof(T).Name, saveItem.SaveID);
        }

        /// <summary>
        /// 尝试读取对象（文件名取类型名）。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="value">成功时为读取到的对象，失败为 null。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>读取成功返回 true。</returns>
        public static bool TryLoadObject<T>(out T value, int saveID = 0) where T : class
        {
            return TryLoadObject(out value, typeof(T).Name, saveID);
        }

        /// <summary>
        /// 读取对象；存档槽 / 文件不存在或读取失败时返回 <paramref name="defaultValue"/>。
        /// </summary>
        /// <typeparam name="T">目标类型（引用类型）。</typeparam>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="defaultValue">读取失败时返回的默认值，默认 null。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>读取到的对象或 <paramref name="defaultValue"/>。</returns>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <remarks>读取失败（文件损坏等）仍会按 TryLoadObject 的规则输出错误日志。</remarks>
        public static T LoadObjectOrDefault<T>(string saveFileName, T defaultValue = null, int saveID = 0)
            where T : class
        {
            return TryLoadObject<T>(out var value, saveFileName, saveID) ? value : defaultValue;
        }

        /// <summary>
        /// 删除指定存档槽中的对象文件（<c>{saveFileName}.json</c>），同时删除其 <c>.bak</c> / <c>.tmp</c> 残留文件并清除对应缓存。
        /// </summary>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>删除前主文件存在返回 true；存档槽不存在、槽目录不存在或文件本来就不存在返回 false。</returns>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        /// <remarks>
        /// 对象文件不参与索引，因此删除后无需更新索引；实际删除失败（IO / 权限）会被 <see cref="SaveFileIO.TryDelete"/> 静默忽略。
        /// 缓存清理先于存在性检查，即使最终返回 false，对应缓存条目也已被移除。
        /// </remarks>
        public static bool DeleteObject(string saveFileName, int saveID = 0)
        {
            ValidateFileName(saveFileName);
            EnsureInitialized();
            //清空缓存中对象
            DeleteCache(saveID, saveFileName);
            if (!_saveSystemData.HaveSaveItem(saveID)) return false;
            // 存档对象所在的文件路径
            var dirPath = GetSaveDirPath(saveID, false);
            if (dirPath == null) return false;

            var savePath = Path.Combine(dirPath, saveFileName + ".json");
            var existed = File.Exists(savePath);
            SaveFileIO.TryDelete(savePath);
            SaveFileIO.TryDelete(savePath + SaveFileIO.BackupSuffix);
            SaveFileIO.TryDelete(savePath + SaveFileIO.TempSuffix);
            return existed;
        }

        /// <summary>
        /// 删除指定存档槽中的对象文件。
        /// </summary>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <returns>见 <see cref="DeleteObject(string, int)"/>。</returns>
        public static bool DeleteObject(string saveFileName, SaveItem saveItem)
        {
            return saveItem != null && DeleteObject(saveFileName, saveItem.SaveID);
        }

        /// <summary>
        /// 删除指定存档槽中的对象文件（无返回值版本）。
        /// </summary>
        /// <typeparam name="T">不参与定位，仅为与其它泛型重载保持一致的调用形式。</typeparam>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveID">存档槽编号。</param>
        /// <remarks>与 <see cref="DeleteObject(string, int)"/> 行为相同，仅丢弃返回值。</remarks>
        public static void DeleteObject<T>(string saveFileName, int saveID) where T : class
        {
            DeleteObject(saveFileName, saveID);
        }

        /// <summary>
        /// 删除指定存档槽中的对象文件（无返回值版本）。
        /// </summary>
        /// <typeparam name="T">不参与定位。</typeparam>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveItem">目标存档槽；为 null 时不执行任何操作。</param>
        public static void DeleteObject<T>(string saveFileName, SaveItem saveItem) where T : class
        {
            if (saveItem == null) return;
            DeleteObject(saveFileName, saveItem.SaveID);
        }

        /// <summary>
        /// 删除指定存档槽中文件名为 <typeparamref name="T"/> 类型名的对象文件。
        /// </summary>
        /// <typeparam name="T">对象文件名来源（<see cref="Type.Name"/>）。</typeparam>
        /// <param name="saveID">存档槽编号。</param>
        public static void DeleteObject<T>(int saveID) where T : class
        {
            DeleteObject(typeof(T).Name, saveID);
        }

        /// <summary>
        /// 删除指定存档槽中文件名为 <typeparamref name="T"/> 类型名的对象文件。
        /// </summary>
        /// <typeparam name="T">对象文件名来源（<see cref="Type.Name"/>）。</typeparam>
        /// <param name="saveItem">目标存档槽；为 null 时不执行任何操作。</param>
        public static void DeleteObject<T>(SaveItem saveItem) where T : class
        {
            if (saveItem == null) return;
            DeleteObject<T>(typeof(T).Name, saveItem.SaveID);
        }

        /// <summary>
        /// 获取指定存档槽中全部对象文件的文件名列表（不含 <c>.json</c> 扩展名），按序数比较排序。
        /// </summary>
        /// <param name="saveID">存档槽编号。</param>
        /// <returns>文件名列表；存档槽或槽目录不存在时返回空列表。备份（<c>.bak</c>）与临时（<c>.tmp</c>）文件不包含在内。</returns>
        public static List<string> GetObjectNames(int saveID)
        {
            EnsureInitialized();
            var result = new List<string>();
            if (!_saveSystemData.HaveSaveItem(saveID)) return result;
            var dirPath = GetSaveDirPath(saveID, false);
            if (dirPath == null) return result;
            foreach (var file in Directory.GetFiles(dirPath, "*.json"))
            {
                result.Add(Path.GetFileNameWithoutExtension(file));
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// 获取指定存档槽中全部对象文件的文件名列表。
        /// </summary>
        /// <param name="saveItem">目标存档槽；为 null 时返回空列表。</param>
        /// <returns>文件名列表；不存在时为空列表。</returns>
        public static List<string> GetObjectNames(SaveItem saveItem)
        {
            return saveItem == null ? new List<string>() : GetObjectNames(saveItem.SaveID);
        }

        /// <summary>
        /// 判断指定存档槽中是否存在对象文件（只检查磁盘上的 <c>{saveFileName}.json</c>，不检查缓存）。
        /// </summary>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveID">存档槽编号，默认 0。</param>
        /// <returns>文件存在返回 true；存档槽或槽目录不存在返回 false。</returns>
        /// <exception cref="ArgumentException"><paramref name="saveFileName"/> 为非法文件名。</exception>
        public static bool HasObject(string saveFileName, int saveID = 0)
        {
            ValidateFileName(saveFileName);
            EnsureInitialized();
            if (!_saveSystemData.HaveSaveItem(saveID)) return false;
            var dirPath = GetSaveDirPath(saveID, false);
            return dirPath != null && File.Exists(Path.Combine(dirPath, saveFileName + ".json"));
        }

        /// <summary>
        /// 判断指定存档槽中是否存在对象文件。
        /// </summary>
        /// <param name="saveFileName">对象文件名（不含扩展名）。</param>
        /// <param name="saveItem">目标存档槽；为 null 返回 false。</param>
        /// <returns>文件存在返回 true。</returns>
        public static bool HasObject(string saveFileName, SaveItem saveItem)
        {
            return saveItem != null && HasObject(saveFileName, saveItem.SaveID);
        }

        #endregion

        #region 全局数据

        /// <summary>
        /// 读取全局设置文件 <c>{fileName}.json</c>；文件不存在或读取失败时，改为创建并立即保存一个 <typeparamref name="T"/> 的默认实例后返回。
        /// </summary>
        /// <typeparam name="T">设置类型，必须有无参构造函数。</typeparam>
        /// <param name="fileName">设置文件名（不含扩展名）。</param>
        /// <returns>读取到的设置实例；读取失败时为新建的默认实例。</returns>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> 为非法文件名。</exception>
        /// <remarks>
        /// 与存档槽无关，保存在 <c>{StorageRoot}/Setting</c> 目录。文件损坏等读取失败会输出错误日志，
        /// 并由随后的保存流程写入默认值（已存在的旧文件在写入时转入 <c>.bak</c> 备份）。
        /// </remarks>
        public static T LoadGlobalSetting<T>(string fileName) where T : class,new()
        {
            ValidateFileName(fileName);
            EnsureInitialized();
            var setting = LoadFile<T>(Path.Combine(SettingDirPath, fileName + ".json"));
            if (setting == null)
            {
                setting = new T();
                SaveGlobalSetting(setting, fileName);
            }

            return setting;
        }

        /// <summary>
        /// 读取全局设置，文件名取 <typeparamref name="T"/> 的类型名；文件不存在或读取失败时创建默认实例并保存。
        /// </summary>
        /// <typeparam name="T">设置类型，必须有无参构造函数。</typeparam>
        /// <returns>设置实例；文件不存在时为新建的默认实例。</returns>
        /// <remarks>类型名相同的不同类型会读写同一文件，注意命名冲突。</remarks>
        public static T LoadGlobalSetting<T>() where T : class,new()
        {
            return LoadGlobalSetting<T>(typeof(T).Name);
        }

        /// <summary>
        /// 尝试读取全局设置；文件不存在或读取失败时返回 false，且不会创建默认文件。
        /// </summary>
        /// <typeparam name="T">设置类型，必须有无参构造函数。</typeparam>
        /// <param name="setting">读取到的设置；失败为 null。</param>
        /// <param name="fileName">设置文件名（不含扩展名）。</param>
        /// <returns>读取成功返回 true。</returns>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> 为非法文件名。</exception>
        /// <remarks>文件损坏等读取失败仍会输出错误日志。</remarks>
        public static bool TryLoadGlobalSetting<T>(out T setting, string fileName) where T : class,new()
        {
            setting = null;
            ValidateFileName(fileName);
            EnsureInitialized();
            setting = LoadFile<T>(Path.Combine(SettingDirPath, fileName + ".json"));
            return setting != null;
        }

        /// <summary>
        /// 尝试读取全局设置，文件名取类型名；不会创建默认文件。
        /// </summary>
        /// <typeparam name="T">设置类型，必须有无参构造函数。</typeparam>
        /// <param name="setting">读取到的设置；失败为 null。</param>
        /// <returns>读取成功返回 true。</returns>
        public static bool TryLoadGlobalSetting<T>(out T setting) where T : class,new()
        {
            return TryLoadGlobalSetting(out setting, typeof(T).Name);
        }

        /// <summary>
        /// 将设置对象序列化为 JSON 并写入 <c>{fileName}.json</c>，已存在同名文件时整体覆盖（原子写入 + <c>.bak</c> 备份）。
        /// </summary>
        /// <param name="saveObject">设置对象；为 null 抛出异常。</param>
        /// <param name="fileName">设置文件名（不含扩展名）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="fileName"/> 为非法文件名。</exception>
        /// <exception cref="IOException">写盘失败时原样抛出。</exception>
        /// <remarks>与存档槽无关，不会更新存档索引。</remarks>
        public static void SaveGlobalSetting(object saveObject, string fileName)
        {
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            ValidateFileName(fileName);
            EnsureInitialized();
            SaveFile(saveObject, Path.Combine(SettingDirPath, fileName + ".json"));
        }

        /// <summary>
        /// 保存全局设置，文件名取对象的运行时类型名。
        /// </summary>
        /// <param name="saveObject">设置对象；为 null 抛出异常。</param>
        /// <exception cref="ArgumentNullException"><paramref name="saveObject"/> 为 null。</exception>
        /// <remarks>类型名相同的不同类型会覆盖同一文件。</remarks>
        public static void SaveGlobalSetting(object saveObject)
        {
            if (saveObject == null) throw new ArgumentNullException(nameof(saveObject));
            SaveGlobalSetting(saveObject, saveObject.GetType().Name);
        }

        /// <summary>
        /// 删除全部全局设置：递归删除 Setting 目录后重新创建。存档数据不受影响。
        /// </summary>
        /// <exception cref="IOException">目录删除失败时原样抛出。</exception>
        /// <exception cref="UnauthorizedAccessException">无权限删除时原样抛出。</exception>
        public static void DeleteAllSetting()
        {
            EnsureInitialized();
            if (Directory.Exists(SettingDirPath))
            {
                // 直接删除目录
                Directory.Delete(SettingDirPath, true);
            }

            CheckAndCreateDir();
        }

        #endregion

        #region 工具函数

        // 确保存档目录与设置目录存在（Directory.CreateDirectory 对已存在目录是幂等的）。
        private static void CheckAndCreateDir()
        {
            Directory.CreateDirectory(SaveDirPath);
            Directory.CreateDirectory(SettingDirPath);
        }

        private static void ValidateFileName(string saveFileName)
        {
            if (!SaveFileIO.IsValidFileName(saveFileName))
            {
                throw new ArgumentException($"非法的文件名：{saveFileName ?? "<null>"}", nameof(saveFileName));
            }
        }

        /// <summary>
        /// 获得存档槽目录路径（<c>SaveData/{saveID}</c>）；createDir=false 且目录不存在时返回 null。
        /// </summary>
        private static string GetSaveDirPath(int saveID, bool createDir)
        {
            var saveDir = Path.Combine(SaveDirPath, saveID.ToString());
            if (!Directory.Exists(saveDir))
            {
                if (!createDir)
                {
                    return null;
                }

                Directory.CreateDirectory(saveDir);
            }

            return saveDir;
        }

        // 保存用户对象文件（原子写入：临时文件 + 替换 + 备份），使用公开的 SerializerSettings
        private static void SaveFile(object saveObject, string path)
        {
            SaveFileIO.WriteJson(saveObject, path, SerializerSettings, SerializationFormat);
        }

        // 加载用户对象文件：文件缺失返回 null（不报错），损坏恢复输出警告，读取失败输出错误日志
        private static T LoadFile<T>(string path) where T : class
        {
            var status = SaveFileIO.ReadJson<T>(path, SerializerSettings, out var value, out var error);
            switch (status)
            {
                case SaveReadStatus.Success:
                    return value;
                case SaveReadStatus.RecoveredFromBackup:
                    Debug.LogWarning($"#YLFramework 存档文件损坏，已从备份恢复：{path}");
                    return value;
                case SaveReadStatus.Missing:
                    return null;
                default:
                    Debug.LogError($"#YLFramework 读取存档文件失败：{path}\n{error}");
                    return null;
            }
        }

        private static T LoadObjectInternal<T>(string saveFileName, int saveID) where T : class
        {
            // 先查缓存：缓存按“槽号 + 文件名”存储，类型不匹配时视为未命中
            var obj = GetCache<T>(saveID, saveFileName);
            // 未命中则从磁盘读取；成功后回填缓存
            if (obj == null)
            {
                var dirPath = GetSaveDirPath(saveID, false);
                if (dirPath == null) return null;
                //文件路径
                var objSavePath = Path.Combine(dirPath, saveFileName + ".json");
                //具体的读取
                obj = LoadFile<T>(objSavePath);
                //设置缓存
                if (obj != null)
                {
                    SetCache(saveID, saveFileName, obj);
                }
            }

            return obj;
        }

        #endregion

        #region 读取存档缓存

        // 写入缓存；同一“槽号 + 文件名”重复保存时以最后一次传入的实例为准
        private static void SetCache(int saveID, string fileName, object saveObject)
        {
            if (CacheDict.ContainsKey(saveID))
            {
                if (CacheDict[saveID] == null)
                {
                    CacheDict[saveID] = new Dictionary<string, object> { { fileName, saveObject } };
                }
                else
                {
                    CacheDict[saveID][fileName] = saveObject;
                }
            }
            else
            {
                CacheDict.Add(saveID, new Dictionary<string, object> { { fileName, saveObject } });
            }
        }

        private static T GetCache<T>(int saveID, string fileName) where T : class
        {
            if (!CacheDict.ContainsKey(saveID)) return null;
            if (CacheDict[saveID].ContainsKey(fileName))
            {
                return CacheDict[saveID][fileName] as T;
            }

            return null;
        }

        private static void DeleteCache(int saveID, string fileName)
        {
            if (CacheDict.ContainsKey(saveID))
            {
                CacheDict[saveID].Remove(fileName);
            }
        }

        private static void DeleteCache(int saveID)
        {
            CacheDict.Remove(saveID);
        }

        private static void ClearCache()
        {
            CacheDict.Clear();
        }

        #endregion
    }
}
