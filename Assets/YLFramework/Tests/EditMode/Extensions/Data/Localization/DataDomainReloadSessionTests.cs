using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.DataAssets;
using YLFramework.Localization;
using YLFramework.Services.Resource;
using YLFramework.Services.Save;
using Object = UnityEngine.Object;
using ResourceLocation = YLFramework.Services.Resource.ResourceLocation;

namespace YLFramework.Tests.Extensions.Data
{
    public class DataDomainReloadSessionTests
    {
        private static readonly string DomainToken = Guid.NewGuid().ToString("N");
        private static readonly FieldInfo StoreField = typeof(SaveManager).GetField("_store", BindingFlags.Static | BindingFlags.NonPublic);
        private bool ownedSession;
        private bool previousEnabled;
        private EnterPlayModeOptions previousOptions;
        private SceneSetup[] previousScenes;
        private string previousOverride;
        private string previousDefault;
        private readonly List<Object> objects = new();
        private ResourceSystem resource;
        private SaveStore store;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isBatchMode && Application.persistentDataPath.Contains("YL-BData-"), Is.True, "Use the dedicated B batch profile, never an original game profile.");
            Assert.That(ResourceSystem.Current, Is.Null);
            Assert.That(LocalizationManager.Configs, Is.Empty);
            Assert.That(ScriptableObjectManager.RegistrationCount, Is.Zero);
            Assert.That(StoreField.GetValue(null), Is.Null, "Do not close another Save owner.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False);
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            previousOverride = (string)typeof(SaveManager).GetField("_storageRootOverride", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousDefault = (string)typeof(SaveManager).GetField("_defaultStorageRoot", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            ownedSession = true;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoRealPlaySessionsWithoutDomainReloadReleaseOldOwnersAndCaches()
        {
            string token = DomainToken;
            string output = Environment.GetEnvironmentVariable("YL_DATA_UNITY_OUTPUT");
            Assert.That(output, Does.StartWith("G:\\Unity\\BD26\\"));
            for (int round = 1; round <= 2; round++)
            {
                yield return new EnterPlayMode(expectDomainReload: false);
                Assert.That(Application.isPlaying, Is.True);
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(EnterPlayModeOptions.DisableDomainReload));
                Assert.That(DomainToken, Is.EqualTo(token), "The managed domain must actually survive both entries.");
                Assert.That(ResourceSystem.Current, Is.Null);
                Assert.That(LocalizationManager.Configs, Is.Empty);
                Assert.That(ScriptableObjectManager.RegistrationCount, Is.Zero);
                Assert.That(StoreField.GetValue(null), Is.Null);
                var language = ScriptableObject.CreateInstance<LanguageConfig>(); objects.Add(language);
                language.language = SystemLanguage.English;
                language.languagesInfo.Add(new LanguageInfo { id = 17, str = "round-" + round });
                var localization = ScriptableObject.CreateInstance<MultiLanguageConfigs>(); objects.Add(localization);
                localization.Languages.Add(language);
                var data = ScriptableObject.CreateInstance<ScriptableObjectConfigs>(); objects.Add(data);
                data.name = "domain-data";
                var child = ScriptableObject.CreateInstance<ScriptableObjectData>(); objects.Add(child);
                child.id = 17; child.config_name = "round-" + round; child.parent = data; data.configs.Add(child);
                var source = new Source { Localization = localization, Data = data };
                var locAddress = new ResourceAddress("domain-b", "localization");
                var dataAddress = new ResourceAddress("domain-b", "data");
                using (var initialized = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
                {
                    new ResourceCatalog("domain-b", new[]
                    {
                        new ResourceCatalogEntry("localization", locAddress, new ResourceLocation(source.Id, "localization", typeof(MultiLanguageConfigs))),
                        new ResourceCatalogEntry("data", dataAddress, new ResourceLocation(source.Id, "data", typeof(ScriptableObjectConfigs)))
                    })
                }, new IResourceSource[] { source }))) resource = initialized.TakeResult();
                var locOwner = LocalizationManager.LoadConfigAsync(resource, locAddress);
                ScriptableObjectManager.Load(resource, dataAddress);
                LocalizationManager.Language = SystemLanguage.English;
                Assert.That(LocalizationManager.GetString(17), Is.EqualTo("round-" + round));
                Assert.That(ScriptableObjectManager.GetConfig("domain-data").GetName(17), Is.EqualTo("round-" + round));
                SaveManager.SetStorageRoot(Path.Combine(output, "save-round-" + round));
                var model = SaveManager.CreateModel<Model>(3);
                model.Value = round;
                SaveManager.SaveModel(model);
                Assert.That(SaveManager.LoadModel<Model>(3).Value, Is.EqualTo(round));
                store = (SaveStore)StoreField.GetValue(null);
                yield return null;
                yield return new ExitPlayMode();
                bool saveClosed = false;
                try { store.GetAllSaveItems(); } catch (ObjectDisposedException) { saveClosed = true; }
                var record = new
                {
                    round, domainToken = DomainToken, pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    domainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.DisableDomainReload,
                    localizationOwners = LocalizationManager.Configs.Count, dataOwners = ScriptableObjectManager.RegistrationCount,
                    locOwnerClosed = locOwner.IsDisposed, leases = resource.GetDiagnosticsSnapshot().LeaseCount, sourceReleases = source.Releases, resourceState = resource.State.ToString(), saveClosed
                };
                File.WriteAllText(Path.Combine(output, "domain-round-" + round + ".json"), JsonConvert.SerializeObject(record, Formatting.Indented));
                Assert.That(record.localizationOwners, Is.Zero);
                Assert.That(record.dataOwners, Is.Zero);
                Assert.That(record.locOwnerClosed, Is.True);
                Assert.That(record.leases, Is.Zero);
                Assert.That(record.sourceReleases, Is.EqualTo(2));
                Assert.That(saveClosed, Is.True, "The previous Unity session must not retain the Save file lock/cache owner.");
                if (resource.State == ResourceSystemState.Ready)
                {
                    using var shutdown = resource.ShutdownAsync(); yield return shutdown;
                    Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded));
                }
                Assert.That(resource.State == ResourceSystemState.Closed || resource.State == ResourceSystemState.Invalidated, Is.True, "Editor exit must leave no live Resource session.");
                Assert.That(ResourceSystem.Current, Is.Null);
                resource = null;
                store = null;
                DestroyOwnedObjects();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!ownedSession) yield break;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            LocalizationManager.Shutdown(); ScriptableObjectManager.Clear(); SaveManager.Close(); store?.Dispose();
            if (resource != null && resource.State == ResourceSystemState.Ready) { using var shutdown = resource.ShutdownAsync(); yield return shutdown; }
            SaveManager.SetStorageRoot(previousOverride);
            typeof(SaveManager).GetField("_defaultStorageRoot", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousDefault);
            DestroyOwnedObjects();
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            if (previousScenes.Length > 0 && Array.Exists(previousScenes, s => s.isLoaded) && Array.Exists(previousScenes, s => s.isActive) && Array.TrueForAll(previousScenes, s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private void DestroyOwnedObjects() { foreach (var item in objects) if (item) Object.DestroyImmediate(item); objects.Clear(); }
        public sealed class Model : ISaveModel { public int Id { get; set; } public int Value; }
        private sealed class Immediate : ResourceSourceOperation { public Immediate(Object value) => Complete(value, null); }
        private sealed class Source : IResourceSource
        {
            public ResourceSourceId Id => new("domain-b-source");
            public bool SupportsSynchronousLoad => true;
            public Object Localization;
            public Object Data;
            public int Releases;
            private Object Asset(Type type) => type == typeof(MultiLanguageConfigs) ? Localization : Data;
            public void Validate(ResourceCatalogEntry entry) { }
            public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type) => new(Asset(type));
            public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => new Immediate(Asset(type));
            public void Release(Object asset) => Releases++;
        }
    }
}
