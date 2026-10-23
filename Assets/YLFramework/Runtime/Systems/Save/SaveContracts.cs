using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YLFramework.Services.Save
{
    /// <summary>一个槽中的主模型；Id 与槽号一致，额外对象仍通过同一存储引擎保存。</summary>
    public interface ISaveModel
    {
        int Id
        {
            get; set;
        }
    }

    public enum SaveReadStatus
    {
        Success,
        RecoveredFromBackup,
        Missing,
        Failed
    }

    /// <summary>读取来源随缓存保留；RecoveredFromBackup 仅表示采用备份值，未自动修盘。</summary>
    public sealed class SaveReadResult<T> where T : class
    {
        public T Value
        {
            get;
        }
        public SaveReadStatus Status
        {
            get;
        }
        public bool IsCached
        {
            get;
        }
        internal SaveReadResult(T value, SaveReadStatus status, bool isCached = false)
        {
            Value = value;
            Status = status;
            IsCached = isCached;
        }
    }

    public sealed class SaveRecoveryRequiredException : IOException
    {
        public string FilePath
        {
            get;
        }
        public SaveRecoveryRequiredException(string path, string detail = null)
            : base("Explicit backup recovery is required before writing: " + path + " | " + detail)
        {
            FilePath = path;
        }
    }

    public sealed class SaveVersionException : IOException
    {
        public int Version
        {
            get;
        }
        public SaveVersionException(int version) : base("Unsupported save index version " + version + ". Expected " + SaveStore.FormatVersion + ". Historical formats are not supported.")
        {
            Version = version;
        }
    }

    internal sealed class SaveIndex
    {
        [JsonProperty(Required = Required.Always)] public int DataVersion = SaveStore.FormatVersion;
        [JsonProperty(Required = Required.Always)] public Dictionary<int, SaveManager.SaveItem> SaveItemDict = new Dictionary<int, SaveManager.SaveItem>();
        [JsonProperty("CurrentSaveID")] public int? CurrentSaveId;

        internal SaveIndex Copy()
        {
            return new SaveIndex
            {
                DataVersion = DataVersion,
                CurrentSaveId = CurrentSaveId,
                SaveItemDict = new Dictionary<int, SaveManager.SaveItem>(SaveItemDict)
            };
        }

        internal void Validate()
        {
            if (DataVersion != SaveStore.FormatVersion)
            {
                throw new SaveVersionException(DataVersion);
            }

            if (SaveItemDict == null)
            {
                throw new InvalidDataException("SaveItemDict is null.");
            }

            foreach (var pair in SaveItemDict)
            {
                if (pair.Key < 0 || pair.Value == null || pair.Value.SaveId != pair.Key)
                {
                    throw new InvalidDataException("Invalid slot identity: " + pair.Key);
                }
            }

            if (CurrentSaveId.HasValue && !SaveItemDict.ContainsKey(CurrentSaveId.Value))
            {
                throw new InvalidDataException("The selected slot is not present in the index.");
            }
        }
    }

    public static partial class SaveManager
    {
        public enum SaveOrder
        {
            None,
            CreateTime,
            CreateTimeDescending,
            SaveTime,
            SaveTimeDescending,
            SaveId,
            SaveIdDescending,
            Name,
            NameDescending
        }

        /// <summary>槽元数据。直接修改仅作用于内存；持久化使用 RenameSaveItem / SetSaveItemNote。</summary>
        [Serializable, JsonObject(MemberSerialization.OptIn)]
        public class SaveItem
        {
            [JsonProperty("SaveID", Required = Required.Always)] private int _saveId;
            [JsonProperty("CreateTime", Required = Required.Always)] private DateTime _createTime;
            [JsonProperty("LastSaveTime", Required = Required.Always)] private DateTime _lastSaveTime;
            public int SaveId => _saveId;
            public DateTime CreateTime => _createTime;
            public DateTime LastSaveTime => _lastSaveTime;
            [JsonProperty("DisplayName", NullValueHandling = NullValueHandling.Ignore)]
            public string DisplayName
            {
                get; set;
            }
            [JsonProperty("Note", NullValueHandling = NullValueHandling.Ignore)]
            public string Note
            {
                get; set;
            }

            [JsonConstructor]
            public SaveItem(int saveId, DateTime createTime)
            {
                _saveId = saveId;
                _createTime = createTime;
                _lastSaveTime = createTime;
            }
            public SaveItem UpdateSaveTime(DateTime lastSaveTime)
            {
                _lastSaveTime = lastSaveTime;
                return this;
            }
            public SaveItem Rename(string displayName)
            {
                DisplayName = displayName;
                return this;
            }
            public SaveItem SetNote(string note)
            {
                Note = note;
                return this;
            }
            internal SaveItem Copy() => new SaveItem(SaveId, CreateTime) { _lastSaveTime = LastSaveTime, DisplayName = DisplayName, Note = Note };
        }
    }
}
