using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Foundation;
using SaveManager = YLFramework.Services.Save.SaveManager;

namespace YLFramework.Tests
{
    // 当前 Unity Test Runner 使用 NUnit 2；同步 EditMode 用例在主线程顺序执行。
    public class SerializedDictionarySaveTests
    {
        [Serializable]
        public class Record
        {
            public SerializedDictionary<string, int> Direct = new SerializedDictionary<string, int>();
            public SerializedDictionary<int, Payload> Nested = new SerializedDictionary<int, Payload>();
        }

        [Serializable]
        public class Payload
        {
            public int Number;
        }

        [Test]
        public void SaveManagerDiskRoundTripRestoresBareDictionariesAndUnitySerializableRows()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var rootField = typeof(SaveManager).GetField("_storageRootOverride", flags);
            var indexField = typeof(SaveManager).GetField("_saveSystemData", flags);
            var cacheField = typeof(SaveManager).GetField("CacheDict", flags);
            var metadataSettingsField = typeof(SaveManager).GetField("MetadataSerializerSettings", flags);
            Assert.That(rootField, Is.Not.Null);
            Assert.That(indexField, Is.Not.Null);
            Assert.That(cacheField, Is.Not.Null);
            Assert.That(metadataSettingsField, Is.Not.Null);

            var originalRoot = rootField.GetValue(null);
            var originalIndex = indexField.GetValue(null);
            var cache = (Dictionary<int, Dictionary<string, object>>)cacheField.GetValue(null);
            // SetStorageRoot 清空外层缓存；保留原内层字典和对象引用，不读取或修改真实存档。
            var originalCache = new Dictionary<int, Dictionary<string, object>>(cache);
            var settings = SaveManager.SerializerSettings;
            var metadataSettings = (JsonSerializerSettings)metadataSettingsField.GetValue(null);
            var originalSettings = CaptureSettings(settings);
            var originalMetadataSettings = CaptureSettings(metadataSettings);
            var originalFormat = SaveManager.SerializationFormat;
            var tempRoot = Path.Combine(Path.GetTempPath(), "YLFramework-SerializedDictionarySave-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(tempRoot), Is.False);
            var ownsRoot = false;

            try
            {
                Directory.CreateDirectory(tempRoot);
                ownsRoot = true;
                SaveManager.SetStorageRoot(tempRoot);
                Assert.That(SaveManager.StorageRoot, Is.EqualTo(tempRoot));
                Assert.That(cache, Is.Empty);

                var slot = SaveManager.CreateSaveItem();
                var source = new SerializedDictionary<string, int> { { "a", 1 }, { "remove", 3 } };
                source["a"] = 2;
                source.Add("b", 4);
                Assert.That(source.Remove("remove"), Is.True);
                var record = new Record { Direct = source };
                record.Nested.Add(7, new Payload { Number = 5 });
                record.Nested[7] = new Payload { Number = 6 };
                record.Nested.Add(-2, new Payload { Number = 8 });

                const string rootFileName = "root-dictionary";
                const string recordFileName = "nested-record";
                SaveManager.SaveObject(source, rootFileName, slot.SaveID);
                SaveManager.SaveObject(record, recordFileName, slot.SaveID);
                var saveDirectory = Path.Combine(tempRoot, "SaveData");
                var slotDirectory = Path.Combine(saveDirectory, slot.SaveID.ToString());
                var rootPath = Path.Combine(slotDirectory, rootFileName + ".json");
                var recordPath = Path.Combine(slotDirectory, recordFileName + ".json");
                Assert.That(File.Exists(Path.Combine(saveDirectory, "SaveSystemData.json")), Is.True);
                Assert.That(File.Exists(rootPath), Is.True);
                Assert.That(File.Exists(recordPath), Is.True);
                Assert.That(JToken.DeepEquals(JToken.Parse(File.ReadAllText(rootPath)),
                    JToken.Parse("{\"a\":2,\"b\":4}")), Is.True);
                Assert.That(JToken.DeepEquals(JToken.Parse(File.ReadAllText(recordPath)),
                    JToken.Parse("{\"Direct\":{\"a\":2,\"b\":4},\"Nested\":{\"7\":{\"Number\":6},\"-2\":{\"Number\":8}}}")), Is.True);
                Assert.That(cache[slot.SaveID][rootFileName], Is.SameAs(source));
                Assert.That(cache[slot.SaveID][recordFileName], Is.SameAs(record));

                // 不再保存这些修改；若 LoadObject 错误复用原实例，下面的磁盘值断言会失败。
                source["a"] = -1;
                source.Add("memory-only", 9);
                record.Nested[7].Number = -1;
                // 同根目录切换走公开清缓存路径，同时迫使存档索引从测试磁盘重新加载。
                SaveManager.SetStorageRoot(tempRoot);
                Assert.That(cache, Is.Empty);
                Assert.That(indexField.GetValue(null), Is.Null);

                var restored = SaveManager.LoadObject<SerializedDictionary<string, int>>(rootFileName, slot.SaveID);
                var restoredRecord = SaveManager.LoadObject<Record>(recordFileName, slot.SaveID);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored, Is.Not.SameAs(source));
                Assert.That(restored.Count, Is.EqualTo(2));
                Assert.That(restored["a"], Is.EqualTo(2));
                Assert.That(restored["b"], Is.EqualTo(4));
                Assert.That(restored.ContainsKey("remove"), Is.False);
                Assert.That(restored.ContainsKey("memory-only"), Is.False);
                Assert.That(restoredRecord, Is.Not.Null);
                Assert.That(restoredRecord, Is.Not.SameAs(record));
                Assert.That(restoredRecord.Direct, Is.Not.SameAs(source));
                Assert.That(restoredRecord.Direct.Count, Is.EqualTo(2));
                Assert.That(restoredRecord.Direct["a"], Is.EqualTo(2));
                Assert.That(restoredRecord.Direct["b"], Is.EqualTo(4));
                Assert.That(restoredRecord.Nested, Is.Not.SameAs(record.Nested));
                Assert.That(restoredRecord.Nested.Count, Is.EqualTo(2));
                Assert.That(restoredRecord.Nested[7], Is.Not.SameAs(record.Nested[7]));
                Assert.That(restoredRecord.Nested[7].Number, Is.EqualTo(6));
                Assert.That(restoredRecord.Nested[-2].Number, Is.EqualTo(8));
                Assert.That(SaveManager.GetSaveItemCount(), Is.EqualTo(1));
                Assert.That(SaveManager.GetSaveItemWithID(slot.SaveID), Is.Not.SameAs(slot));
                Assert.That(cache[slot.SaveID][rootFileName], Is.SameAs(restored));
                Assert.That(cache[slot.SaveID][recordFileName], Is.SameAs(restoredRecord));

                var unitySource = new Record { Direct = restored, Nested = restoredRecord.Nested };
                var unityJson = JsonUtility.ToJson(unitySource);
                Assert.That(JToken.DeepEquals(JObject.Parse(unityJson)["Direct"]["datas"],
                    JToken.Parse("[{\"Key\":\"a\",\"Value\":2},{\"Key\":\"b\",\"Value\":4}]")), Is.True);
                var unityRestored = JsonUtility.FromJson<Record>(unityJson);
                Assert.That(unityRestored.Direct, Is.Not.SameAs(restored));
                Assert.That(unityRestored.Direct.Count, Is.EqualTo(2));
                Assert.That(unityRestored.Direct["a"], Is.EqualTo(2));
                Assert.That(unityRestored.Direct["b"], Is.EqualTo(4));
                Assert.That(unityRestored.Nested.Count, Is.EqualTo(2));
                Assert.That(unityRestored.Nested[7].Number, Is.EqualTo(6));
                Assert.That(unityRestored.Nested[-2].Number, Is.EqualTo(8));
            }
            finally
            {
                // 不调用 SetStorageRoot(originalRoot)，否则会再次清空待恢复的内存状态。
                rootField.SetValue(null, originalRoot);
                indexField.SetValue(null, originalIndex);
                cache.Clear();
                foreach (var pair in originalCache)
                {
                    cache.Add(pair.Key, pair.Value);
                }
                RestoreSettings(settings, originalSettings);
                RestoreSettings(metadataSettings, originalMetadataSettings);
                SaveManager.SerializationFormat = originalFormat;
                if (ownsRoot && Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
            Assert.That(rootField.GetValue(null), Is.EqualTo(originalRoot));
            Assert.That(indexField.GetValue(null), Is.SameAs(originalIndex));
            Assert.That(cache.Count, Is.EqualTo(originalCache.Count));
            foreach (var pair in originalCache)
                Assert.That(cache[pair.Key], Is.SameAs(pair.Value));
            Assert.That(SaveManager.SerializerSettings, Is.SameAs(settings));
            Assert.That(SaveManager.SerializationFormat, Is.EqualTo(originalFormat));
            foreach (var pair in originalSettings)
                Assert.That(pair.Key.GetValue(settings), Is.EqualTo(pair.Value));
            foreach (var pair in originalMetadataSettings)
                Assert.That(pair.Key.GetValue(metadataSettings), Is.EqualTo(pair.Value));
            Assert.That(Directory.Exists(tempRoot), Is.False);
        }

        private static Dictionary<FieldInfo, object> CaptureSettings(JsonSerializerSettings settings)
        {
            var snapshot = new Dictionary<FieldInfo, object>();
            // 不修改实际 Save 设置，也不调用可能延迟初始化集合的 getter。
            foreach (var field in typeof(JsonSerializerSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly)
                {
                    snapshot.Add(field, field.GetValue(settings));
                }
            }
            return snapshot;
        }

        private static void RestoreSettings(JsonSerializerSettings settings, Dictionary<FieldInfo, object> snapshot)
        {
            foreach (var pair in snapshot)
            {
                pair.Key.SetValue(settings, pair.Value);
            }
        }
    }
}
