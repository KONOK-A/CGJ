using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaveItem = YLFramework.Services.Save.SaveManager.SaveItem;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// 唯一的存档引擎。显式根目录、同根独占 owner，同槽串行、跨槽独立；不依赖 Unity。
    /// </summary>
    /// <remarks>
    /// 对象和设置保持裸 JSON，索引版本为 2。模型必须是普通 DTO；异步反序列化/Converter 在工作线程执行，
    /// 不得调用 Unity API。同步读取保留调用线程；保存序列化在调用线程先捕获快照。
    /// 缓存返回原实例，但调用方负责实例本身的并发修改。并发前配置 SerializerSettings，Converter 必须可并发且不得重入本 owner。
    /// 禁止 TypeNameHandling。备份修复、删除和索引修改会保留失败原件；非全根多文件崩溃事务。
    /// Dispose 取消未提交操作并等待已进入的操作退出；commit 后不将成功改报取消。
    /// </remarks>
    public sealed class SaveStore : IDisposable
    {
        public const int FormatVersion = 2;
        public const string ModelFileName = "ArchiveModel";
        private readonly object _lifecycle = new object();
        private readonly object _metadata = new object();
        private readonly object _cacheLock = new object();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new ConcurrentDictionary<int, SemaphoreSlim>();
        private sealed class CachedObject
        {
            internal object Value;
            internal SaveReadStatus Status;
        }
        private readonly Dictionary<int, Dictionary<string, CachedObject>> _cache = new Dictionary<int, Dictionary<string, CachedObject>>();
        private readonly FileStream _rootLock;
        private SaveIndex _index;
        private bool _requiresIndexRecovery;
        private bool _isDisposed;
        private bool _hasFinishedDisposal;
        private int _active;
        public string StorageRoot
        {
            get;
        }
        public JsonSerializerSettings SerializerSettings
        {
            get;
        }
        public Formatting SerializationFormat
        {
            get; set;
        }
        private string SaveDirectory => Path.Combine(StorageRoot, "SaveData");
        private string SettingDirectory => Path.Combine(StorageRoot, "Setting");
        private string IndexPath => Path.Combine(SaveDirectory, "SaveSystemData.json");

        public SaveStore(string rootPath, JsonSerializerSettings settings = null, Formatting formatting = Formatting.Indented)
        {
            StorageRoot = SaveFileIO.NormalizeRoot(rootPath);
            SerializerSettings = settings ?? new JsonSerializerSettings();
            SerializationFormat = formatting;
            Directory.CreateDirectory(StorageRoot);
            var lockPath = SafePath(Path.Combine(StorageRoot, ".yl-save.lock"));
            _rootLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                var status = SaveFileIO.ReadJson<SaveIndex>(SafePath(IndexPath), null, out _index, out var error);
                if (status == SaveReadStatus.Failed)
                {
                    throw new InvalidDataException("Cannot read save index: " + error);
                }

                if (status == SaveReadStatus.Missing)
                {
                    if (Directory.Exists(SaveDirectory) && Directory.EnumerateFileSystemEntries(SaveDirectory).Any())
                    {
                        throw new InvalidDataException("Save files exist without an index; refusing to create an empty index.");
                    }

                    _index = new SaveIndex();
                }
                _index.Validate();
                _requiresIndexRecovery = status == SaveReadStatus.RecoveredFromBackup;
                foreach (var item in _index.SaveItemDict.Values)
                {
                    RefreshSaveTime(item);
                }
            }
            catch
            {
                _rootLock.Dispose();
                _shutdown.Dispose();
                throw;
            }
        }

        public int? CurrentSaveId
        {
            get => ReadIndex(index => index.CurrentSaveId);
            set => Execute(-2, token =>
            {
                lock (_metadata)
                {
                    if (value.HasValue && !_index.SaveItemDict.ContainsKey(value.Value))
                    {
                        throw new KeyNotFoundException("Unknown save slot: " + value);
                    }

                    var next = _index.Copy();
                    next.CurrentSaveId = value;
                    WriteIndex(next, token);
                }
                return true;
            });
        }

        public List<SaveItem> GetAllSaveItems() => ReadIndex(index => index.SaveItemDict.Values.ToList());
        public bool HasSaveItem(int id) => ReadIndex(index => index.SaveItemDict.ContainsKey(id));
        public SaveItem GetSaveItem(int id) => ReadIndex(index => index.SaveItemDict.TryGetValue(id, out var item) ? item : null);
        public bool IndexNeedsRecovery => ReadIndex(_ => _requiresIndexRecovery);
        public SaveItem CreateSaveItem(int? id = null) => CreateSaveItemAsync(id).GetAwaiter().GetResult();
        public Task<SaveItem> CreateSaveItemAsync(int? id = null, CancellationToken cancellationToken = default)
            => ExecuteAsync(-2, token => CreateSlot(id, null, null, token), cancellationToken);

        private SaveItem CreateSlot(int? requestedId, object model, string json, CancellationToken token)
        {
            lock (_metadata)
            {
                EnsureIndexWritable();
                var id = requestedId ?? 0;
                if (id < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(requestedId));
                }

                if (!requestedId.HasValue)
                {
                    while (_index.SaveItemDict.ContainsKey(id) || Directory.Exists(SlotPath(id)) || File.Exists(SlotPath(id)))
                    {
                        id = checked(id + 1);
                    }
                }

                if (_index.SaveItemDict.ContainsKey(id) || Directory.Exists(SlotPath(id)) || File.Exists(SlotPath(id)))
                {
                    throw new IOException("Save slot already exists; no overwrite is allowed: " + id);
                }

                var item = new SaveItem(id, DateTime.Now);
                var next = _index.Copy();
                next.SaveItemDict.Add(id, item);
                string staging = null;
                var moved = false;
                try
                {
                    if (json != null)
                    {
                        staging = SafePath(Path.Combine(SaveDirectory, ".creating-" + Guid.NewGuid().ToString("N")));
                        SaveFileIO.WriteTextAtomic(Path.Combine(staging, ModelFileName + ".json"), json, token, false);
                        token.ThrowIfCancellationRequested();
                        Directory.Move(staging, SlotPath(id));
                        moved = true;
                    }
                    WriteIndex(next, token);
                }
                catch
                {
                    // 失败模型文件保留在独立 staging，既不占用正式槽，也不销毁诊断样本。
                    if (moved)
                    {
                        Directory.Move(SlotPath(id), staging);
                    }

                    throw;
                }
                if (model != null)
                {
                    SetCache(id, ModelFileName, model);
                }

                return item;
            }
        }

        public bool RenameSaveItem(int id, string name) => ChangeItem(id, item => item.Rename(name));
        public bool SetSaveItemNote(int id, string note) => ChangeItem(id, item => item.SetNote(note));
        private bool ChangeItem(int id, Action<SaveItem> change)
        {
            return Execute(id, token =>
            {
                lock (_metadata)
                {
                    if (!_index.SaveItemDict.TryGetValue(id, out var original))
                    {
                        return false;
                    }

                    var modified = original.Copy();
                    change(modified);
                    var next = _index.Copy();
                    next.SaveItemDict[id] = modified;
                    WriteIndex(next, token);
                    change(original);
                    _index.SaveItemDict[id] = original;
                    return true;
                }
            });
        }

        /// <summary>删除是显式软删除，目录留在 SaveData/.deleted-*；索引失败时回滚目录，不递归销毁未知内容。</summary>
        public void DeleteSaveItem(int id) => DeleteSaveItemAsync(id).GetAwaiter().GetResult();
        public Task DeleteSaveItemAsync(int id, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(id, token =>
            {
                lock (_metadata)
                {
                    if (!_index.SaveItemDict.ContainsKey(id))
                    {
                        return false;
                    }

                    EnsureIndexWritable();
                    var path = SlotPath(id);
                    var tomb = SafePath(Path.Combine(SaveDirectory, ".deleted-" + id + "-" + Guid.NewGuid().ToString("N")));
                    var moved = Directory.Exists(path);
                    if (moved)
                    {
                        Directory.Move(path, tomb);
                    }

                    var next = _index.Copy();
                    next.SaveItemDict.Remove(id);
                    if (next.CurrentSaveId == id)
                    {
                        next.CurrentSaveId = null;
                    }

                    try
                    {
                        WriteIndex(next, token);
                    }
                    catch
                    {
                        if (moved)
                        {
                            Directory.Move(tomb, path);
                        }
                        throw;
                    }
                    lock (_cacheLock)
                    {
                        _cache.Remove(id);
                    }

                    return true;
                }
            }, cancellationToken);
        }

        public void DeleteAllSaveItems()
        {
            foreach (var item in GetAllSaveItems())
            {
                DeleteSaveItem(item.SaveId);
            }
        }

        public void SaveObject(object value, string name, int id = 0) => SaveObjectAsync(value, name, id).GetAwaiter().GetResult();
        public Task SaveObjectAsync(object value, string name, int id = 0, CancellationToken cancellationToken = default)
        {
            ValidateName(name);
            cancellationToken.ThrowIfCancellationRequested();
            var json = Serialize(value);
            return ExecuteAsync(id, token =>
            {
                RequireSlot(id, true);
                var path = ObjectPath(id, name);
                SaveFileIO.ValidateWritableJson(path);
                var savedAt = DateTime.Now;
                SaveFileIO.WriteTextAtomic(path, json, token);
                SetCache(id, name, value);
                lock (_metadata)
                {
                    _index.SaveItemDict[id].UpdateSaveTime(savedAt);
                }

                return true;
            }, cancellationToken);
        }

        public T LoadObject<T>(string name, int id = 0) where T : class
            => ReadObject<T>(name, id).Value;
        public async Task<T> LoadObjectAsync<T>(string name, int id = 0, CancellationToken cancellationToken = default) where T : class
            => (await ReadObjectAsync<T>(name, id, cancellationToken).ConfigureAwait(false)).Value;
        public SaveReadResult<T> ReadObject<T>(string name, int id = 0) where T : class
        {
            ValidateName(name);
            return Execute(id, token => ReadObjectCore<T>(name, id, token));
        }
        public Task<SaveReadResult<T>> ReadObjectAsync<T>(string name, int id = 0, CancellationToken cancellationToken = default) where T : class
        {
            ValidateName(name);
            return ExecuteAsync(id, token => ReadObjectCore<T>(name, id, token), cancellationToken);
        }
        private SaveReadResult<T> ReadObjectCore<T>(string name, int id, CancellationToken token) where T : class
        {
            RequireSlot(id);
            token.ThrowIfCancellationRequested();
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(id, out var objects) && objects.TryGetValue(name, out var cached))
                {
                    if (cached.Value is T typed)
                    {
                        return new SaveReadResult<T>(typed, cached.Status, true);
                    }

                    throw new InvalidOperationException("Cached save type does not match requested type: " + name);
                }
            }
            var result = ReadFile<T>(ObjectPath(id, name));
            token.ThrowIfCancellationRequested();
            if (result.Value != null)
            {
                SetCache(id, name, result.Value, result.Status);
            }

            return result;
        }

        public bool HasObject(string name, int id = 0)
        {
            ValidateName(name);
            return Execute(id, _ => HasSaveItem(id) && File.Exists(ObjectPath(id, name)));
        }
        public List<string> GetObjectNames(int id)
        {
            return Execute(id, _ =>
            {
                if (!HasSaveItem(id) || !Directory.Exists(SlotPath(id)))
                {
                    return new List<string>();
                }

                return Directory.GetFiles(SlotPath(id), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToList();
            });
        }

        public bool DeleteObject(string name, int id = 0)
        {
            ValidateName(name);
            return Execute(id, token =>
            {
                if (!HasSaveItem(id))
                {
                    return false;
                }

                var path = ObjectPath(id, name);
                var existed = File.Exists(path);
                // 备份先移走；失败不能让后续读取复活已删除对象。所有原件仍可在 tomb 中找回。
                var suffix = ".deleted-" + Guid.NewGuid().ToString("N");
                var files = new[] { path + ".bak", path };
                var moved = new List<string>();
                try
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var file in files)
                    {
                        if (File.Exists(SafePath(file)))
                        {
                            File.Move(file, file + suffix);
                            moved.Add(file);
                        }
                    }
                }
                catch
                {
                    foreach (var file in moved.AsEnumerable().Reverse())
                    {
                        File.Move(file + suffix, file);
                    }
                    throw;
                }
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(id, out var objects))
                    {
                        objects.Remove(name);
                    }
                }

                return existed;
            });
        }

        public void SaveGlobalSetting(object value, string name) => SaveGlobalSettingAsync(value, name).GetAwaiter().GetResult();
        public Task SaveGlobalSettingAsync(object value, string name, CancellationToken cancellationToken = default)
        {
            ValidateName(name);
            cancellationToken.ThrowIfCancellationRequested();
            var json = Serialize(value);
            return ExecuteAsync(-1, token =>
            {
                var path = SettingPath(name);
                SaveFileIO.ValidateWritableJson(path);
                SaveFileIO.WriteTextAtomic(path, json, token);
                return true;
            }, cancellationToken);
        }

        public T LoadGlobalSetting<T>(string name) where T : class, new()
        {
            ValidateName(name);
            return Execute(-1, token =>
            {
                var path = SettingPath(name);
                var value = ReadValue<T>(path);
                if (value != null)
                {
                    return value;
                }

                value = new T();
                SaveFileIO.WriteTextAtomic(path, Serialize(value), token, false);
                return value;
            });
        }
        public bool TryLoadGlobalSetting<T>(out T value, string name) where T : class
        {
            ValidateName(name);
            value = Execute(-1, _ => ReadValue<T>(SettingPath(name)));
            return value != null;
        }
        public SaveReadResult<T> ReadGlobalSetting<T>(string name) where T : class
        {
            ValidateName(name);
            return Execute(-1, _ => ReadFile<T>(SettingPath(name)));
        }

        public void DeleteAllSetting()
        {
            Execute(-1, token =>
            {
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(SafePath(SettingDirectory)))
                {
                    Directory.Move(SettingDirectory, SafePath(Path.Combine(StorageRoot, ".deleted-settings-" + Guid.NewGuid().ToString("N"))));
                }

                return true;
            });
        }

        /// <summary>显式恢复，返回损坏主件保留路径；原 .bak 保持不变。</summary>
        public string RestoreObjectBackup(string name, int id = 0)
        {
            ValidateName(name);
            return Execute(id, token =>
            {
                RequireSlot(id);
                var preserved = SaveFileIO.RestoreBackup<JToken>(ObjectPath(id, name), null, token);
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(id, out var objects))
                    {
                        objects.Remove(name);
                    }
                }

                return preserved;
            });
        }
        public string RestoreSettingBackup(string name)
        {
            ValidateName(name);
            return Execute(-1, token => SaveFileIO.RestoreBackup<JToken>(SettingPath(name), null, token));
        }
        public string RestoreIndexBackup()
        {
            return Execute(-2, token =>
            {
                lock (_metadata)
                {
                    if (!_requiresIndexRecovery)
                    {
                        throw new InvalidOperationException("Reopen a damaged index in backup-read mode before restoring it; an active healthy index cannot be rolled back.");
                    }

                    SaveIndex restored = null;
                    var preserved = SaveFileIO.RestoreBackup<SaveIndex>(SafePath(IndexPath), null, token, index =>
                    {
                        index.Validate();
                        foreach (var item in index.SaveItemDict.Values)
                        {
                            RefreshSaveTime(item);
                        }

                        restored = index;
                    });
                    _index = restored;
                    _requiresIndexRecovery = false;
                    lock (_cacheLock)
                    {
                        _cache.Clear();
                    }

                    return preserved;
                }
            });
        }

        public T CreateModel<T>(int id) where T : class, ISaveModel, new() => CreateModelAsync<T>(id).GetAwaiter().GetResult();
        public Task<T> CreateModelAsync<T>(int id, CancellationToken cancellationToken = default) where T : class, ISaveModel, new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = new T { Id = id };
            if (id < 0 || model.Id != id)
            {
                throw new ArgumentException("Model Id must match a non-negative slot id.");
            }

            var json = Serialize(model);
            return ExecuteAsync(id, cancellation =>
            {
                CreateSlot(id, model, json, cancellation);
                return model;
            }, cancellationToken);
        }
        public void SaveModel<T>(T model) where T : class, ISaveModel
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            SaveObject(model, ModelFileName, model.Id);
        }
        public Task SaveModelAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, ISaveModel
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            return SaveObjectAsync(model, ModelFileName, model.Id, cancellationToken);
        }
        public T LoadModel<T>(int? id = null) where T : class, ISaveModel
        {
            var slot = id ?? CurrentSaveId ?? throw new InvalidOperationException("No current save slot is selected.");
            var model = LoadObject<T>(ModelFileName, slot);
            if (model != null && model.Id != slot)
            {
                throw new InvalidDataException("Model Id does not match its slot: " + slot);
            }

            return model;
        }
        public async Task<T> LoadModelAsync<T>(int? id = null, CancellationToken cancellationToken = default) where T : class, ISaveModel
        {
            var slot = id ?? CurrentSaveId ?? throw new InvalidOperationException("No current save slot is selected.");
            var model = await LoadObjectAsync<T>(ModelFileName, slot, cancellationToken).ConfigureAwait(false);
            if (model != null && model.Id != slot)
            {
                throw new InvalidDataException("Model Id does not match its slot: " + slot);
            }

            return model;
        }

        /// <summary>按槽号稳定加载主模型；跳过没有主模型的普通对象槽，仍使用同一对象缓存。</summary>
        public async Task<List<T>> LoadAllModelsAsync<T>(CancellationToken cancellationToken = default) where T : class, ISaveModel
        {
            var models = new List<T>();
            foreach (var item in GetAllSaveItems().OrderBy(item => item.SaveId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var model = await LoadModelAsync<T>(item.SaveId, cancellationToken).ConfigureAwait(false);
                if (model != null)
                {
                    models.Add(model);
                }
            }
            return models;
        }

        public void ClearCache()
        {
            Execute(-2, _ =>
            {
                lock (_cacheLock)
                {
                    _cache.Clear();
                }
                return true;
            });
        }
        private void SetCache(int id, string name, object value, SaveReadStatus status = SaveReadStatus.Success)
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(id, out var objects))
                {
                    _cache.Add(id, objects = new Dictionary<string, CachedObject>(StringComparer.OrdinalIgnoreCase));
                }

                objects[name] = new CachedObject { Value = value, Status = status };
            }
        }

        private T ReadValue<T>(string path) where T : class => ReadFile<T>(path).Value;
        private SaveReadResult<T> ReadFile<T>(string path) where T : class
        {
            ValidateSerializer();
            var status = SaveFileIO.ReadJson<T>(path, SerializerSettings, out var value, out var error);
            if (status == SaveReadStatus.Failed)
            {
                throw new InvalidDataException("Cannot read save data: " + path + " | " + error);
            }

            return new SaveReadResult<T>(value, status);
        }
        private string Serialize(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            lock (_lifecycle)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SaveStore));
                }
            }

            ValidateSerializer();
            return SaveFileIO.Serialize(value, SerializerSettings, SerializationFormat);
        }
        private void ValidateSerializer()
        {
            if (SerializerSettings.TypeNameHandling != TypeNameHandling.None)
            {
                throw new NotSupportedException("Save data must not instantiate CLR types from JSON ($type).");
            }
        }
        private T ReadIndex<T>(Func<SaveIndex, T> read)
        {
            using (var operation = Begin(CancellationToken.None))
            {
                try
                {
                    lock (_metadata)
                    {
                        return read(_index);
                    }
                }
                finally
                {
                    End();
                }
            }
        }
        private void RequireSlot(int id, bool writing = false)
        {
            lock (_metadata)
            {
                if (writing)
                {
                    EnsureIndexWritable();
                }
                if (!_index.SaveItemDict.ContainsKey(id))
                {
                    throw new KeyNotFoundException("Unknown save slot: " + id);
                }
            }
        }
        private void EnsureIndexWritable()
        {
            if (_requiresIndexRecovery)
            {
                throw new SaveRecoveryRequiredException(IndexPath);
            }
        }
        private void WriteIndex(SaveIndex next, CancellationToken token)
        {
            EnsureIndexWritable();
            SaveFileIO.ValidateWritableJson(SafePath(IndexPath));
            next.Validate();
            SaveFileIO.WriteTextAtomic(IndexPath, SaveFileIO.Serialize(next, null, Formatting.Indented), token);
            _index = next;
        }
        private void RefreshSaveTime(SaveItem item)
        {
            var directory = SlotPath(item.SaveId);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                var time = File.GetLastWriteTime(SafePath(path));
                if (time > item.LastSaveTime)
                {
                    item.UpdateSaveTime(time);
                }
            }
        }
        private string SlotPath(int id) => SafePath(Path.Combine(SaveDirectory, id.ToString(CultureInfo.InvariantCulture)));
        private string ObjectPath(int id, string name) => SafePath(Path.Combine(SlotPath(id), name + ".json"));
        private string SettingPath(string name) => SafePath(Path.Combine(SettingDirectory, name + ".json"));
        private string SafePath(string path)
        {
            SaveFileIO.EnsureNoLinks(path);
            SaveFileIO.EnsureNoLinks(path + ".bak");
            return path;
        }
        private static void ValidateName(string name)
            => SaveFileIO.ValidateFileName(name);

        private T Execute<T>(int scope, Func<CancellationToken, T> action)
        {
            using (var operation = Begin(CancellationToken.None))
            {
                var gate = _gates.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));
                var entered = false;
                try
                {
                    gate.Wait(operation.Token);
                    entered = true;
                    operation.Token.ThrowIfCancellationRequested();
                    return action(operation.Token);
                }
                finally
                {
                    if (entered)
                    {
                        gate.Release();
                    }
                    End();
                }
            }
        }
        private async Task<T> ExecuteAsync<T>(int scope, Func<CancellationToken, T> action, CancellationToken cancellationToken)
        {
            using (var operation = Begin(cancellationToken))
            {
                var gate = _gates.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));
                var entered = false;
                try
                {
                    await gate.WaitAsync(operation.Token).ConfigureAwait(false);
                    entered = true;
                    return await Task.Run(() =>
                    {
                        operation.Token.ThrowIfCancellationRequested();
                        return action(operation.Token);
                    }).ConfigureAwait(false);
                }
                finally
                {
                    if (entered)
                    {
                        gate.Release();
                    }
                    End();
                }
            }
        }
        private CancellationTokenSource Begin(CancellationToken token)
        {
            lock (_lifecycle)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SaveStore));
                }

                token.ThrowIfCancellationRequested();
                var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
                _active++;
                return linked;
            }
        }
        private void End()
        {
            lock (_lifecycle)
            {
                _active--;
                Monitor.PulseAll(_lifecycle);
            }
        }
        public void Dispose()
        {
            lock (_lifecycle)
            {
                if (_isDisposed)
                {
                    while (!_hasFinishedDisposal)
                    {
                        Monitor.Wait(_lifecycle);
                    }
                    return;
                }
                _isDisposed = true;
            }
            try
            {
                _shutdown.Cancel();
                lock (_lifecycle)
                {
                    while (_active != 0)
                    {
                        Monitor.Wait(_lifecycle);
                    }
                }

                lock (_cacheLock)
                {
                    _cache.Clear();
                }

                _rootLock.Dispose();
                _shutdown.Dispose();
                foreach (var gate in _gates.Values)
                {
                    gate.Dispose();
                }
            }
            finally
            {
                lock (_lifecycle)
                {
                    _hasFinishedDisposal = true;
                    Monitor.PulseAll(_lifecycle);
                }
            }
        }
    }
}
