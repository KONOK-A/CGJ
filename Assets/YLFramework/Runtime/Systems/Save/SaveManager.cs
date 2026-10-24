using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace YLFramework.Save
{
    /// <summary>Unity 入口，仅负责主线程默认路径捕获和既有 API；所有存档、模型、设置及缓存均由 SaveStore 实现。</summary>
    /// <remarks>
    /// 仅支持当前索引格式；不转换历史YL/XF存档，不自动清空损坏档。
    /// 对象/设置仍为裸 JSON。损坏无可用备份、版本、IO 和权限失败会抛异常；备份读取不修盘。
    /// 新异步接口在调用线程序列化快照后异步 IO；DTO/Converter 不得在线程池调用 Unity API。
    /// SetStorageRoot / Close 取消并排空旧 owner，不能把旧请求的缓存写入新根。
    /// </remarks>
    public static partial class SaveManager
    {
        public const int CurrentDataVersion = SaveStore.FormatVersion;
        private static readonly object _sync = new object();
        private static SaveStore _store;
        private static string _storageRootOverride;
        private static string _defaultStorageRoot;
        public static JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings();
        public static Formatting SerializationFormat { get; set; } = Formatting.Indented;

        /// <summary>显式根可在纯 BCL 环境使用；默认 Unity 路径必须先在主线程捕获。</summary>
        public static string StorageRoot
        {
            get
            {
                lock (_sync)
                {
                    if (!string.IsNullOrEmpty(_storageRootOverride))
                    {
                        return _storageRootOverride;
                    }

                    if (_defaultStorageRoot != null)
                    {
                        return _defaultStorageRoot;
                    }

                    if (SynchronizationContext.Current?.GetType().FullName != "UnityEngine.UnitySynchronizationContext")
                    {
                        throw new InvalidOperationException("Capture the default save root on the Unity main thread, or call SetStorageRoot with an explicit path before worker use.");
                    }

                    return _defaultStorageRoot = Application.persistentDataPath;
                }
            }
        }

        private static SaveStore Store
        {
            get
            {
                lock (_sync)
                {
                    if (_store == null)
                    {
                        _store = new SaveStore(StorageRoot, SerializerSettings, SerializationFormat);
                    }

                    _store.SerializationFormat = SerializationFormat;
                    return _store;
                }
            }
        }

        /// <summary>只切换路径，不复制或迁移数据。同根重设也会关闭旧 owner 并清除缓存。</summary>
        public static void SetStorageRoot(string rootPath)
        {
            SaveStore previous;
            lock (_sync)
            {
                previous = _store;
                _store = null;
                _storageRootOverride = string.IsNullOrEmpty(rootPath) ? null : rootPath;
            }
            previous?.Dispose();
        }

        public static void Close()
        {
            SaveStore previous;
            lock (_sync)
            {
                previous = _store;
                _store = null;
            }
            previous?.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginUnitySession()
        {
            Close();
            lock (_sync)
            {
                _storageRootOverride = null;
                _defaultStorageRoot = Application.persistentDataPath;
            }
            Application.quitting -= Close;
            Application.quitting += Close;
        }

        public static int? CurrentSaveId
        {
            get => Store.CurrentSaveId; set => Store.CurrentSaveId = value;
        }
        public static bool IndexNeedsRecovery => Store.IndexNeedsRecovery;
        public static List<SaveItem> GetAllSaveItems(SaveOrder saveOrder = SaveOrder.None)
        {
            var items = Store.GetAllSaveItems();
            switch (saveOrder)
            {
                case SaveOrder.CreateTime:
                    return items.OrderBy(s => s.CreateTime).ToList();
                case SaveOrder.CreateTimeDescending:
                    return items.OrderByDescending(s => s.CreateTime).ToList();
                case SaveOrder.SaveTime:
                    return items.OrderBy(s => s.LastSaveTime).ToList();
                case SaveOrder.SaveTimeDescending:
                    return items.OrderByDescending(s => s.LastSaveTime).ToList();
                case SaveOrder.SaveId:
                    return items.OrderBy(s => s.SaveId).ToList();
                case SaveOrder.SaveIdDescending:
                    return items.OrderByDescending(s => s.SaveId).ToList();
                case SaveOrder.Name:
                    return items.OrderBy(s => s.DisplayName ?? string.Empty).ToList();
                case SaveOrder.NameDescending:
                    return items.OrderByDescending(s => s.DisplayName ?? string.Empty).ToList();
                default:
                    return items;
            }
        }
        public static List<SaveItem> GetAllSaveItems<T>(Func<SaveItem, T> orderFunc, bool isDescending = false)
        {
            if (orderFunc == null)
            {
                throw new ArgumentNullException(nameof(orderFunc));
            }

            var items = Store.GetAllSaveItems();
            return (isDescending ? items.OrderByDescending(orderFunc) : items.OrderBy(orderFunc)).ToList();
        }
        public static bool HasSaveItem(int saveId) => Store.HasSaveItem(saveId);
        public static bool HasSaveItem(SaveItem saveItem) => saveItem != null && HasSaveItem(saveItem.SaveId);
        public static bool TryGetSaveItem(int saveId, out SaveItem saveItem)
        {
            saveItem = Store.GetSaveItem(saveId);
            return saveItem != null;
        }
        public static SaveItem GetSaveItemWithId(int id) => Store.GetSaveItem(id);
        public static int GetSaveItemCount() => Store.GetAllSaveItems().Count;
        public static SaveItem CreateSaveItem() => Store.CreateSaveItem();
        public static Task<SaveItem> CreateSaveItemAsync(int? saveId = null, CancellationToken cancellationToken = default) => Store.CreateSaveItemAsync(saveId, cancellationToken);
        public static bool RenameSaveItem(int saveId, string displayName) => Store.RenameSaveItem(saveId, displayName);
        public static bool RenameSaveItem(SaveItem saveItem, string displayName) => saveItem != null && RenameSaveItem(saveItem.SaveId, displayName);
        public static bool SetSaveItemNote(int saveId, string note) => Store.SetSaveItemNote(saveId, note);
        public static bool SetSaveItemNote(SaveItem saveItem, string note) => saveItem != null && SetSaveItemNote(saveItem.SaveId, note);
        public static void DeleteSaveItem(int saveId) => Store.DeleteSaveItem(saveId);
        public static void DeleteSaveItem(SaveItem saveItem)
        {
            if (saveItem != null)
            {
                DeleteSaveItem(saveItem.SaveId);
            }
        }
        public static Task DeleteSaveItemAsync(int saveId, CancellationToken cancellationToken = default) => Store.DeleteSaveItemAsync(saveId, cancellationToken);
        public static void DeleteAllSaveItems() => Store.DeleteAllSaveItems();
        public static void DeleteAll()
        {
            var store = Store;
            store.DeleteAllSaveItems();
            store.DeleteAllSetting();
        }

        public static void SaveObject(object saveObject, string saveFileName, int saveId = 0)
        {
            if (saveObject == null)
            {
                throw new ArgumentNullException(nameof(saveObject));
            }

            SaveFileIO.ValidateFileName(saveFileName);
            var store = Store;
            if (!store.HasSaveItem(saveId))
            {
                Debug.LogError("#YLFramework 没有该存档项，非法访问 :" + saveId);
                return;
            }
            store.SaveObject(saveObject, saveFileName, saveId);
        }
        public static void SaveObject(object saveObject, string saveFileName, SaveItem saveItem)
        {
            if (saveObject == null)
            {
                throw new ArgumentNullException(nameof(saveObject));
            }

            if (saveItem != null)
            {
                SaveObject(saveObject, saveFileName, saveItem.SaveId);
            }
        }
        public static void SaveObject(object saveObject, SaveItem saveItem)
        {
            if (saveItem != null)
            {
                SaveObject(saveObject, saveItem.SaveId);
            }
        }
        public static void SaveObject(object saveObject, int saveId = 0)
        {
            if (saveObject == null)
            {
                throw new ArgumentNullException(nameof(saveObject));
            }
            SaveObject(saveObject, saveObject.GetType().Name, saveId);
        }
        public static Task SaveObjectAsync(object saveObject, string saveFileName, int saveId = 0, CancellationToken cancellationToken = default)
            => Store.SaveObjectAsync(saveObject, saveFileName, saveId, cancellationToken);

        public static T LoadObject<T>(string saveFileName, int saveId = 0) where T : class
        {
            SaveFileIO.ValidateFileName(saveFileName);
            var store = Store;
            if (!store.HasSaveItem(saveId))
            {
                Debug.LogError("#YLFramework 没有该存档项，非法访问 :" + saveId);
                return null;
            }
            return store.LoadObject<T>(saveFileName, saveId);
        }
        public static T LoadObject<T>(string saveFileName, SaveItem saveItem) where T : class => saveItem == null ? null : LoadObject<T>(saveFileName, saveItem.SaveId);
        public static T LoadObject<T>(SaveItem saveItem) where T : class => saveItem == null ? null : LoadObject<T>(typeof(T).Name, saveItem.SaveId);
        public static T LoadObject<T>(int saveId = 0) where T : class => LoadObject<T>(typeof(T).Name, saveId);
        public static Task<T> LoadObjectAsync<T>(string saveFileName, int saveId = 0, CancellationToken cancellationToken = default) where T : class
            => Store.LoadObjectAsync<T>(saveFileName, saveId, cancellationToken);
        public static SaveReadResult<T> ReadObject<T>(string saveFileName, int saveId = 0) where T : class => Store.ReadObject<T>(saveFileName, saveId);
        public static Task<SaveReadResult<T>> ReadObjectAsync<T>(string saveFileName, int saveId = 0, CancellationToken cancellationToken = default) where T : class
            => Store.ReadObjectAsync<T>(saveFileName, saveId, cancellationToken);
        public static bool TryLoadObject<T>(out T value, string saveFileName, int saveId = 0) where T : class
        {
            SaveFileIO.ValidateFileName(saveFileName);
            var store = Store;
            value = store.HasSaveItem(saveId) ? store.LoadObject<T>(saveFileName, saveId) : null;
            return value != null;
        }
        public static bool TryLoadObject<T>(out T value, string saveFileName, SaveItem saveItem) where T : class
        {
            value = null;
            return saveItem != null && TryLoadObject(out value, saveFileName, saveItem.SaveId);
        }
        public static bool TryLoadObject<T>(out T value, SaveItem saveItem) where T : class => TryLoadObject(out value, typeof(T).Name, saveItem);
        public static bool TryLoadObject<T>(out T value, int saveId = 0) where T : class => TryLoadObject(out value, typeof(T).Name, saveId);
        public static T LoadObjectOrDefault<T>(string saveFileName, T defaultValue = null, int saveId = 0) where T : class
            => TryLoadObject<T>(out var value, saveFileName, saveId) ? value : defaultValue;

        public static bool DeleteObject(string saveFileName, int saveId = 0)
        {
            SaveFileIO.ValidateFileName(saveFileName);
            return Store.DeleteObject(saveFileName, saveId);
        }
        public static bool DeleteObject(string saveFileName, SaveItem saveItem) => saveItem != null && DeleteObject(saveFileName, saveItem.SaveId);
        public static void DeleteObject<T>(string saveFileName, int saveId) where T : class => DeleteObject(saveFileName, saveId);
        public static void DeleteObject<T>(string saveFileName, SaveItem saveItem) where T : class
        {
            if (saveItem != null)
            {
                DeleteObject(saveFileName, saveItem.SaveId);
            }
        }
        public static void DeleteObject<T>(int saveId) where T : class => DeleteObject(typeof(T).Name, saveId);
        public static void DeleteObject<T>(SaveItem saveItem) where T : class
        {
            if (saveItem != null)
            {
                DeleteObject<T>(saveItem.SaveId);
            }
        }
        public static List<string> GetObjectNames(int saveId) => Store.GetObjectNames(saveId);
        public static List<string> GetObjectNames(SaveItem saveItem) => saveItem == null ? new List<string>() : GetObjectNames(saveItem.SaveId);
        public static bool HasObject(string saveFileName, int saveId = 0)
        {
            SaveFileIO.ValidateFileName(saveFileName);
            return Store.HasObject(saveFileName, saveId);
        }
        public static bool HasObject(string saveFileName, SaveItem saveItem) => saveItem != null && HasObject(saveFileName, saveItem.SaveId);
        public static void ClearCache() => Store.ClearCache();

        /// <summary>只有缺失文件才创建默认值；损坏/IO 错误不会被默认值覆盖。</summary>
        public static T LoadGlobalSetting<T>(string fileName) where T : class, new()
        {
            SaveFileIO.ValidateFileName(fileName);
            return Store.LoadGlobalSetting<T>(fileName);
        }
        public static T LoadGlobalSetting<T>() where T : class, new() => LoadGlobalSetting<T>(typeof(T).Name);
        public static bool TryLoadGlobalSetting<T>(out T setting, string fileName) where T : class, new()
        {
            SaveFileIO.ValidateFileName(fileName);
            return Store.TryLoadGlobalSetting(out setting, fileName);
        }
        public static bool TryLoadGlobalSetting<T>(out T setting) where T : class, new() => TryLoadGlobalSetting(out setting, typeof(T).Name);
        public static SaveReadResult<T> ReadGlobalSetting<T>(string fileName) where T : class => Store.ReadGlobalSetting<T>(fileName);
        public static void SaveGlobalSetting(object saveObject, string fileName)
        {
            if (saveObject == null)
            {
                throw new ArgumentNullException(nameof(saveObject));
            }
            SaveFileIO.ValidateFileName(fileName);
            Store.SaveGlobalSetting(saveObject, fileName);
        }
        public static void SaveGlobalSetting(object saveObject)
        {
            if (saveObject == null)
            {
                throw new ArgumentNullException(nameof(saveObject));
            }
            SaveGlobalSetting(saveObject, saveObject.GetType().Name);
        }
        public static Task SaveGlobalSettingAsync(object saveObject, string fileName, CancellationToken cancellationToken = default)
            => Store.SaveGlobalSettingAsync(saveObject, fileName, cancellationToken);
        public static void DeleteAllSetting() => Store.DeleteAllSetting();
        public static string RestoreObjectBackup(string fileName, int saveId = 0) => Store.RestoreObjectBackup(fileName, saveId);
        public static string RestoreSettingBackup(string fileName) => Store.RestoreSettingBackup(fileName);
        public static string RestoreIndexBackup() => Store.RestoreIndexBackup();

        public static T CreateModel<T>(int saveId) where T : class, ISaveModel, new() => Store.CreateModel<T>(saveId);
        public static Task<T> CreateModelAsync<T>(int saveId, CancellationToken cancellationToken = default) where T : class, ISaveModel, new() => Store.CreateModelAsync<T>(saveId, cancellationToken);
        public static T LoadModel<T>(int? saveId = null) where T : class, ISaveModel => Store.LoadModel<T>(saveId);
        public static Task<T> LoadModelAsync<T>(int? saveId = null, CancellationToken cancellationToken = default) where T : class, ISaveModel => Store.LoadModelAsync<T>(saveId, cancellationToken);
        public static void SaveModel<T>(T model) where T : class, ISaveModel => Store.SaveModel(model);
        public static Task SaveModelAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, ISaveModel => Store.SaveModelAsync(model, cancellationToken);
        public static Task<List<T>> LoadAllModelsAsync<T>(CancellationToken cancellationToken = default) where T : class, ISaveModel => Store.LoadAllModelsAsync<T>(cancellationToken);
    }
}
