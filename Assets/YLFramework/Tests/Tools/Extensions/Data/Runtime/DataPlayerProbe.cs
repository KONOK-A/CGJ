using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;
using YLFramework.DataAssets;
using YLFramework.Localization;
using YLFramework.Services.Resource;
using YLFramework.Services.Save;

namespace YLFramework.Tests.DataBatch
{
    public sealed class DataPlayerProbe : MonoBehaviour
    {
        [SerializeField] private string resourcePrefix;
        [SerializeField] private string backend;
        [SerializeField] private DataPlayerTextSink sink;
        private string output;
        private ResourceSystem resources;
        private LocalizationConfigHandle localization;
        private IDisposable data;
        private readonly Report report = new();

        [Serializable, Preserve]
        public sealed class Model : ISaveModel
        {
            public int Id { get; set; }
            public int Score;
            public string[] Names;
        }
        [Serializable, Preserve]
        public sealed class Report
        {
            public bool passed;
            public string backend;
            public string unityVersion;
            public string persistentDataPath;
            public string error;
            public bool saveRoundTrip;
            public bool backupRecovery;
            public bool canceledWritePreserved;
            public bool concreteSoReflection;
            public bool localizedReflectionText;
            public bool playerAssembliesExcluded;
            public int exitLeaseCount;
            public string[] assemblies;
        }

        private IEnumerator Start()
        {
            output = Argument("-dataProbeOutput");
            if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith("G:\\Unity\\BD26\\", StringComparison.OrdinalIgnoreCase)) { Application.Quit(21); yield break; }
            var checks = Checks();
            while (true)
            {
                object instruction = null;
                bool next = false;
                Exception failure = null;
                try { next = checks.MoveNext(); if (next) instruction = checks.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { ((IDisposable)checks).Dispose(); Finish(failure); yield break; }
                if (!next) { ((IDisposable)checks).Dispose(); Finish(null); yield break; }
                yield return instruction;
            }
        }

        private IEnumerator Checks()
        {
            report.backend = backend;
            report.unityVersion = Application.unityVersion;
            report.persistentDataPath = Application.persistentDataPath;
            Require(report.persistentDataPath.Contains("YL-BData-Player"), "Player persistent data is not isolated.");
            report.assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            Require(!report.assemblies.Any(n => n.StartsWith("UnityEditor", StringComparison.Ordinal) || n.Contains("MiniExcel")), "Editor/backend assembly entered Player.");
            report.playerAssembliesExcluded = true;
            var loc = new ResourceAddress("bplayer", "localization");
            var config = new ResourceAddress("bplayer", "data");
            using (var initialized = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog("bplayer", new[]
                {
                    new ResourceCatalogEntry("localization", loc, new ResourceLocation(ResourceSourceId.Resources, resourcePrefix + "/Localization", typeof(MultiLanguageConfigs))),
                    new ResourceCatalogEntry("data", config, new ResourceLocation(ResourceSourceId.Resources, resourcePrefix + "/Data", typeof(ScriptableObjectConfigs)))
                })
            })))
            {
                yield return initialized;
                Require(initialized.Status == ResourceRequestStatus.Succeeded, initialized.Error?.ToString());
                resources = initialized.TakeResult();
            }
            localization = LocalizationManager.LoadConfigAsync(resources, loc);
            while (localization.IsLoading) yield return null;
            Require(localization.Error == null && localization.Config, localization.Error?.ToString());
            data = ScriptableObjectManager.Load(resources, config);
            var root = ScriptableObjectManager.GetConfig("Data");
            Require(root.GetScriptableObjectData<DataPlayerFixture>(17).amount == 41, "Concrete data asset did not deserialize.");
            var type = Type.GetType(typeof(DataPlayerFixture).FullName + ", " + typeof(DataPlayerFixture).Assembly.GetName().Name, true);
            var reflected = ScriptableObject.CreateInstance(type);
            Require(reflected is DataPlayerFixture, "SO reflection/AOT resolution failed.");
            Destroy(reflected);
            report.concreteSoReflection = true;
            LocalizationManager.Language = SystemLanguage.Japanese;
            sink.gameObject.SetActive(true);
            yield return null;
            Require(sink.text == "Native English" && sink.Enables == 1, "Localized Other text or non-Chinese fallback failed.");
            sink.GetComponent<LocalizeString>().Refresh();
            Require(sink.Enables == 1, "Refreshing restarted the business object.");
            report.localizedReflectionText = true;
            RunFileChecks();
            sink.gameObject.SetActive(false);
            localization.Dispose(); localization = null;
            data.Dispose(); data = null;
            report.exitLeaseCount = resources.GetDiagnosticsSnapshot().LeaseCount;
            Require(report.exitLeaseCount == 0, "Player leases did not return to zero.");
            using (var stopped = resources.ShutdownAsync()) { yield return stopped; Require(stopped.Status == ResourceRequestStatus.Succeeded, stopped.Error?.ToString()); }
            resources = null;
        }

        private void RunFileChecks()
        {
            string root = Path.Combine(output, "save");
            using (var store = new SaveStore(root))
            {
                var model = store.CreateModel<Model>(3); model.Score = 7; model.Names = new[] { "a", "b" };
                store.SaveModelAsync(model).GetAwaiter().GetResult();
                model.Score = 8; store.SaveModel(model);
                store.ClearCache();
                Require(store.LoadModel<Model>(3).Score == 8, "Model round trip failed.");
                report.saveRoundTrip = true;
                string path = Path.Combine(root, "SaveData/3/" + SaveStore.ModelFileName + ".json");
                File.WriteAllText(path, "damaged-primary");
                store.ClearCache();
                var recovered = store.ReadObject<Model>(SaveStore.ModelFileName, 3);
                Require(recovered.Status == SaveReadStatus.RecoveredFromBackup && recovered.Value.Score == 7 && File.ReadAllText(path) == "damaged-primary", "Backup read modified or lost the primary.");
                store.RestoreObjectBackup(SaveStore.ModelFileName, 3);
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.corrupt-*").Length == 1, "Recovery did not keep the damaged primary.");
                report.backupRecovery = true;
                byte[] before = File.ReadAllBytes(path);
                using (var canceled = new CancellationTokenSource())
                {
                    canceled.Cancel(); bool rejected = false;
                    try { store.SaveModelAsync(new Model { Id = 3, Score = 99 }, canceled.Token).GetAwaiter().GetResult(); }
                    catch (OperationCanceledException) { rejected = true; }
                    Require(rejected && File.ReadAllBytes(path).SequenceEqual(before), "Canceled write changed the primary.");
                }
                report.canceledWritePreserved = true;
            }
        }

        private void Finish(Exception error)
        {
            localization?.Dispose(); data?.Dispose();
            report.passed = error == null;
            report.error = error?.ToString();
            File.WriteAllText(Path.Combine(output, "player-result.json"), JsonUtility.ToJson(report, true));
            if (error == null) Debug.Log("B_DATA_PLAYER_OK " + backend);
            else Debug.LogException(error);
            Application.Quit(error == null ? 0 : 20);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message ?? "B data probe failed."); }
        private static string Argument(string name) { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }
}
