using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YLFramework.Localization;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Extensions.Data
{
    public class LocalizationLifecycleTests
    {
        private readonly List<Object> objects = new();
        private bool hadPreference;
        private int preference;
        private ResourceSystem resources;
        private ControlledSource source;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ResourceSystem.Current, Is.Null, "Do not take over another Resource owner.");
            Assert.That(LocalizationManager.Configs, Is.Empty, "Do not close another localization owner.");
            LocalizationManager.Shutdown();
            hadPreference = PlayerPrefs.HasKey(LocalizationManager.LanguagePreferenceKey);
            preference = PlayerPrefs.GetInt(LocalizationManager.LanguagePreferenceKey);
            LocalizationManager.Language = SystemLanguage.English;
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationManager.Shutdown();
            if (source != null) foreach (var operation in source.Operations) if (!operation.IsDone) operation.Finish(null, new Exception("test cleanup"));
            if (resources != null) { using var closed = resources.ShutdownAsync(); Assert.That(closed.Status, Is.EqualTo(ResourceRequestStatus.Succeeded)); }
            foreach (var value in objects) if (value) Object.DestroyImmediate(value);
            objects.Clear();
            if (hadPreference) PlayerPrefs.SetInt(LocalizationManager.LanguagePreferenceKey, preference);
            else PlayerPrefs.DeleteKey(LocalizationManager.LanguagePreferenceKey);
            PlayerPrefs.Save();
        }

        private MultiLanguageConfigs Root(string value)
        {
            var root = ScriptableObject.CreateInstance<MultiLanguageConfigs>();
            var language = ScriptableObject.CreateInstance<LanguageConfig>();
            language.language = SystemLanguage.English;
            language.languagesInfo.Add(new LanguageInfo { id = 1, str = value });
            root.Languages.Add(language);
            objects.Add(root); objects.Add(language);
            return root;
        }

        [Test]
        public void EnabledText_ReceivesLateConfigWithoutRestartingBusinessObject()
        {
            var go = new GameObject("YL localization lifecycle", typeof(RectTransform));
            objects.Add(go);
            go.SetActive(false);
            var text = go.AddComponent<Text>();
            var counter = go.AddComponent<LocalizationActivationCounter>();
            var localize = go.AddComponent<LocalizeString>();
            localize.ID = 1;
            LogAssert.Expect(LogType.Warning, new Regex("Localization.*ID=1.*missing text"));
            go.SetActive(true);
            Assert.That(counter.Enables, Is.EqualTo(1));
            using var registered = LocalizationManager.AddConfig(Root("arrived"));
            Assert.That(text.text, Is.EqualTo("arrived"));
            localize.Refresh();
            LocalizationManager.Refresh();
            Assert.That(counter.Enables, Is.EqualTo(1));
            Assert.That(counter.Disables, Is.Zero);
            go.SetActive(false);
            registered.Dispose();
            Assert.That(counter.Disables, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_CancelsPendingRootAndLateProducerCannotRegister()
        {
            var root = Root("late");
            var address = new ResourceAddress("localization-test", "root");
            source = new ControlledSource();
            using var initialize = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog(address.ProjectId, new[] { new ResourceCatalogEntry("root", address, new ResourceLocation(source.Id, "root", typeof(MultiLanguageConfigs))) })
            }, new IResourceSource[] { source }));
            resources = initialize.TakeResult();
            using var handle = LocalizationManager.LoadConfigAsync(resources, address);
            Assert.That(handle.IsLoading, Is.True);
            LocalizationManager.Shutdown();
            Assert.That(handle.IsDisposed, Is.True);
            source.Operations[0].Finish(root);
            Assert.That(LocalizationManager.Configs.Count, Is.Zero);
            Assert.That(resources.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantShutdownDuringSourceStart_DisposesRequestReturnedAfterOwnerClosed()
        {
            var root = Root("late");
            var address = new ResourceAddress("localization-test", "root");
            source = new ControlledSource { OnLoad = LocalizationManager.Shutdown };
            using var initialize = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog(address.ProjectId, new[] { new ResourceCatalogEntry("root", address, new ResourceLocation(source.Id, "root", typeof(MultiLanguageConfigs))) })
            }, new IResourceSource[] { source }));
            resources = initialize.TakeResult();
            using var handle = LocalizationManager.LoadConfigAsync(resources, address);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(handle.IsLoading, Is.False, "A closed owner must not retain a new pending consumer.");
            source.Operations[0].Finish(root);
            Assert.That(LocalizationManager.Configs.Count, Is.Zero);
            Assert.That(resources.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownRejectsRegistrationAndLoadingFromRootReleaseAndClearsReentrantListeners()
        {
            var root = Root("first");
            var other = Root("must not register");
            other.Languages[0].languagesInfo[0].id = 2;
            var address = new ResourceAddress("localization-test", "root");
            source = new ControlledSource();
            using var initialize = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog(address.ProjectId, new[] { new ResourceCatalogEntry("root", address, new ResourceLocation(source.Id, "root", typeof(MultiLanguageConfigs))) })
            }, new IResourceSource[] { source }));
            resources = initialize.TakeResult();
            using var handle = LocalizationManager.LoadConfigAsync(resources, address);
            source.Operations[0].Finish(root);
            Exception addError = null;
            Exception loadError = null;
            var calls = 0;
            source.OnRelease = () =>
            {
                try { LocalizationManager.AddConfig(other); } catch (InvalidOperationException error) { addError = error; }
                try { LocalizationManager.LoadConfigAsync(resources, address); } catch (InvalidOperationException error) { loadError = error; }
                LocalizationManager.Changed.Register(() => calls++);
            };
            LocalizationManager.Shutdown();
            Assert.That(addError, Is.TypeOf<InvalidOperationException>());
            Assert.That(loadError, Is.TypeOf<InvalidOperationException>());
            Assert.That(source.Operations.Count, Is.EqualTo(1), "Closing must reject before a second native producer starts.");
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(LocalizationManager.Configs, Is.Empty);
            LocalizationManager.Refresh();
            Assert.That(calls, Is.Zero, "Listeners added from closing callbacks must not escape the closing owner.");
            Assert.That(resources.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
        }

        [Test]
        public void ShutdownAttemptsEveryOwnerWhenNativeRetentionReleaseThrows()
        {
            var first = Root("first");
            var second = Root("second");
            second.Languages[0].languagesInfo[0].id = 2;
            var firstAddress = new ResourceAddress("localization-test", "first");
            var secondAddress = new ResourceAddress("localization-test", "second");
            source = new ControlledSource();
            using var initialize = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog("localization-test", new[]
                {
                    new ResourceCatalogEntry("first", firstAddress, new ResourceLocation(source.Id, "first", typeof(MultiLanguageConfigs))),
                    new ResourceCatalogEntry("second", secondAddress, new ResourceLocation(source.Id, "second", typeof(MultiLanguageConfigs)))
                })
            }, new IResourceSource[] { source }));
            resources = initialize.TakeResult();
            using var firstHandle = LocalizationManager.LoadConfigAsync(resources, firstAddress);
            using var secondHandle = LocalizationManager.LoadConfigAsync(resources, secondAddress);
            var released = 0;
            source.Operations[0].Finish(first, retention: new CallbackRetention(() => { released++; throw new InvalidOperationException("first retention failed"); }));
            source.Operations[1].Finish(second, retention: new CallbackRetention(() => { released++; throw new InvalidOperationException("second retention failed"); }));
            var error = Assert.Throws<AggregateException>(LocalizationManager.Shutdown);
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(released, Is.EqualTo(2));
            Assert.That(firstHandle.IsDisposed && secondHandle.IsDisposed, Is.True);
            Assert.That(LocalizationManager.Configs, Is.Empty);
            Assert.That(resources.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            using var next = LocalizationManager.AddConfig(Root("new owner after close"));
            Assert.That(next.Config, Is.Not.Null, "A failed release must not permanently poison the closing barrier.");
        }

        [Test]
        public void ImageLanguageSwitch_LateOldProducerCannotReplaceNewDisplay()
        {
            var root = Root("text");
            root.Languages[0].languagesInfo.Add(new LanguageInfo { id = 2, type = MultiLanguageType.Image,
                image = new ImageInfo { projectName = "localization-test", imageName = "english" } });
            var french = ScriptableObject.CreateInstance<LanguageConfig>();
            objects.Add(french);
            french.language = SystemLanguage.French;
            french.languagesInfo.Add(new LanguageInfo { id = 2, type = MultiLanguageType.Image,
                image = new ImageInfo { projectName = "localization-test", imageName = "french" } });
            root.Languages.Add(french);
            source = new ControlledSource();
            var englishAddress = new ResourceAddress("localization-test", "english");
            var frenchAddress = new ResourceAddress("localization-test", "french");
            using var initialize = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog("localization-test", new[]
                {
                    new ResourceCatalogEntry("english", englishAddress, new ResourceLocation(source.Id, "english", typeof(Texture2D))),
                    new ResourceCatalogEntry("french", frenchAddress, new ResourceLocation(source.Id, "french", typeof(Texture2D)))
                })
            }, new IResourceSource[] { source }));
            resources = initialize.TakeResult();
            using var registration = LocalizationManager.AddConfig(root);
            var go = new GameObject("YL localized image", typeof(RectTransform));
            objects.Add(go);
            go.SetActive(false);
            var image = go.AddComponent<RawImage>();
            go.AddComponent<ResourceImage>();
            var localize = go.AddComponent<LocalizeImage>();
            localize.ID = 2;
            go.SetActive(true);
            Assert.That(source.Operations.Count, Is.EqualTo(1));
            LocalizationManager.Language = SystemLanguage.French;
            Assert.That(source.Operations.Count, Is.EqualTo(2));
            var oldTexture = new Texture2D(2, 2);
            var newTexture = new Texture2D(2, 2);
            objects.Add(oldTexture); objects.Add(newTexture);
            source.Operations[1].Finish(newTexture);
            Assert.That(image.texture, Is.SameAs(newTexture));
            source.Operations[0].Finish(oldTexture);
            Assert.That(image.texture, Is.SameAs(newTexture));
            Assert.That(source.Releases, Is.EqualTo(1));
            go.SetActive(false);
            Assert.That(resources.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(2));
        }

        private sealed class ControlledOperation : ResourceSourceOperation
        {
            public void Finish(Object value, Exception error = null, IDisposable retention = null) => Complete(value, error, retention);
        }

        private sealed class CallbackRetention : IDisposable
        {
            private Action close;
            public CallbackRetention(Action close) => this.close = close;
            public void Dispose() { var callback = close; close = null; callback?.Invoke(); }
        }

        private sealed class ControlledSource : IResourceSource
        {
            public ResourceSourceId Id => new("localization-controlled");
            public bool SupportsSynchronousLoad => false;
            public readonly List<ControlledOperation> Operations = new();
            public int Releases;
            public Action OnLoad;
            public Action OnRelease;
            public void Validate(ResourceCatalogEntry entry) { }
            public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type) => throw new NotSupportedException();
            public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type)
            { OnLoad?.Invoke(); var operation = new ControlledOperation(); Operations.Add(operation); return operation; }
            public void Release(Object value) { Releases++; OnRelease?.Invoke(); }
        }
    }

    public class LocalizationActivationCounter : MonoBehaviour
    {
        public int Enables;
        public int Disables;
        private void OnEnable() => Enables++;
        private void OnDisable() => Disables++;
    }
}
