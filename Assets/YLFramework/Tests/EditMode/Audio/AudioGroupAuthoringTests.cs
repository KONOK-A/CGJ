using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Editor.Audio;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioGroupAuthoringTests
    {
        private string _folder;
        private Scene _scene;
        private readonly List<Object> _objects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/YLFramework/Tests/EditMode/Audio/Authoring-" + Guid.NewGuid().ToString("N");
            Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode/Audio", _folder.Substring(_folder.LastIndexOf('/') + 1)));
            _scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var owned in _objects) if (owned) Undo.ClearUndo(owned);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] is AudioGroupInspectorTestWindow window && window) window.Close();
                if (_objects[i] && !EditorUtility.IsPersistent(_objects[i])) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            if (AssetDatabase.IsValidFolder(_folder)) AssetDatabase.DeleteAsset(_folder);
        }

        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }

        private AudioClipLibrary Library(string name)
        {
            var library = Own(ScriptableObject.CreateInstance<AudioClipLibrary>());
            AssetDatabase.CreateAsset(library, _folder + "/" + name + ".asset");
            return library;
        }

        private AudioGroupPlayer Player()
        {
            var root = Own(new GameObject("AudioGroupAuthoring"));
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, _scene);
            return Own(root.AddComponent<AudioGroupPlayer>());
        }

        private static void FlushUndo(int group)
        {
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }

        [Test]
        public void ExplicitMigrationCreatesIndependentCopyAndNeverGuessesLegacyBundleIdentity()
        {
            var source = Library("Legacy");
            var clip = Own(AudioClip.Create("BorrowedMigration", 100, 1, 1000, false));
            AssetDatabase.CreateAsset(clip, _folder + "/clip.asset");
            var old = new AudioClipEntry { Source = AudioClipSource.AssetBundle, BundleName = "not-a-project", Key = "old.wav", Weight = 2f };
            source.Groups.Add(new AudioClipGroup { Name = "Mix", Selection = AudioClipSelectionMode.Weighted,
                Entries = new() { new AudioClipEntry { Clip = clip, Weight = 3f }, old } });
            AssetDatabase.SaveAssetIfDirty(source);
            var before = EditorJsonUtility.ToJson(source);
            var type = typeof(AudioClipLibraryEditor).Assembly.GetType("YLFramework.Editor.Audio.AudioClipLibraryMigration");
            Assert.That(type, Is.Not.Null, "Legacy identity requires an explicit copy-only conversion entry.");
            var method = type.GetMethod("CreateCopy");
            var path = _folder + "/Converted.asset";
            var empty = new Dictionary<AudioClipEntry, ResourceAddress>();
            var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { source, path, empty }));
            Assert.That(failure.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null);
            var mapping = new Dictionary<AudioClipEntry, ResourceAddress> { [old] = new ResourceAddress("new-audio", "music/theme") };
            var copy = Own((AudioClipLibrary)method.Invoke(null, new object[] { source, path, mapping }));
            Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))));
            Assert.That(AssetDatabase.GetAssetPath(copy.Groups[0].Entries[0].Clip), Is.EqualTo(_folder + "/clip.asset"));
            Assert.That(copy.Groups[0].Selection, Is.EqualTo(AudioClipSelectionMode.Weighted));
            Assert.That(copy.Groups[0].Entries[1].Source, Is.EqualTo(AudioClipSource.Resource));
            Assert.That(copy.Groups[0].Entries[1].ProjectId, Is.EqualTo("new-audio"));
            Assert.That(copy.Groups[0].Entries[1].Key, Is.EqualTo("music/theme"));
            Assert.That(copy.Groups[0].Entries[1].BundleName, Is.Empty);
            Assert.That(copy.Groups[0].Entries[1].Weight, Is.EqualTo(2f));
            Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { source, path, mapping }));
            Assert.That(AudioClipLibraryEditor.GetWarnings(copy), Is.Empty, "Editor validates identity without starting a runtime session.");
        }

        [Test]
        public void LibraryAssetRoundTripPreservesSelectionAddressesAndClipReferences()
        {
            var library = Library("Library");
            var clip = Own(AudioClip.Create("Borrowed", 100, 1, 1000, false));
            AssetDatabase.CreateAsset(clip, _folder + "/clip.asset");
            using (var serialized = new SerializedObject(library))
            {
                AudioClipLibraryEditor.AddGroup(serialized);
                var group = serialized.FindProperty("Groups").GetArrayElementAtIndex(0);
                group.FindPropertyRelative("Name").stringValue = "UI/Click";
                group.FindPropertyRelative("Selection").intValue = (int)AudioClipSelectionMode.Weighted;
                serialized.ApplyModifiedProperties();
                for (var i = 0; i < 5; i++) AudioClipLibraryEditor.AddEntry(serialized, 0);
                var entries = serialized.FindProperty("Groups").GetArrayElementAtIndex(0).FindPropertyRelative("Entries");
                for (var i = 0; i < 5; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Source").intValue = i;
                    entry.FindPropertyRelative("Weight").floatValue = i == 0 ? 0f : i;
                    entry.FindPropertyRelative("Key").stringValue = i == 1 ? "resources://Audio/Click" : "Audio/Click";
                    entry.FindPropertyRelative("BundleName").stringValue = i == 2 ? "audio/ui" : "";
                    entry.FindPropertyRelative("ProjectId").stringValue = i == 4 ? "audio-ui" : "";
                }
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("Clip").objectReferenceValue = clip;
                serialized.ApplyModifiedProperties();
            }
            var path = AssetDatabase.GetAssetPath(library);
            AssetDatabase.SaveAssetIfDirty(library);
            Undo.ClearUndo(library);
            Resources.UnloadAsset(library);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var reopened = Own(AssetDatabase.LoadAssetAtPath<AudioClipLibrary>(path));
            Assert.IsNotNull(reopened);
            Assert.AreEqual(typeof(AudioClipLibrary), reopened.GetType());
            Assert.AreEqual("UI/Click", reopened.Groups[0].Name);
            Assert.AreEqual(AudioClipSelectionMode.Weighted, reopened.Groups[0].Selection);
            CollectionAssert.AreEqual(new[] { 0f, 1f, 2f, 3f, 4f }, reopened.Groups[0].Entries.Select(entry => entry.Weight));
            CollectionAssert.AreEqual(new[] { AudioClipSource.Clip, AudioClipSource.Resources, AudioClipSource.AssetBundle, AudioClipSource.ExternalProvider, AudioClipSource.Resource },
                reopened.Groups[0].Entries.Select(entry => entry.Source));
            Assert.AreEqual(_folder + "/clip.asset", AssetDatabase.GetAssetPath(reopened.Groups[0].Entries[0].Clip));
            Assert.AreEqual("resources://Audio/Click", reopened.Groups[0].Entries[1].Key);
            Assert.AreEqual("audio/ui", reopened.Groups[0].Entries[2].BundleName);
            Assert.AreEqual("Audio/Click", reopened.Groups[0].Entries[3].Key);
            Assert.AreEqual("audio-ui", reopened.Groups[0].Entries[4].ProjectId);
            Assert.AreEqual("Audio/Click", reopened.Groups[0].Entries[4].Key);
        }

        [Test]
        public void MultiObjectLibraryAuthoringPreservesUnequalListsAndSupportsUndo()
        {
            var first = Library("First");
            var second = Library("Second");
            using (var serialized = new SerializedObject(second)) AudioClipLibraryEditor.AddGroup(serialized);
            var editor = Own(UnityEditor.Editor.CreateEditor(new Object[] { first, second }));
            Assert.AreEqual(typeof(AudioClipLibraryEditor), editor.GetType());
            Assert.IsTrue(editor.GetType().IsDefined(typeof(CanEditMultipleObjects), false));
            Undo.IncrementCurrentGroup();
            var undo = Undo.GetCurrentGroup();
            AudioClipLibraryEditor.AddGroup(editor.serializedObject);
            FlushUndo(undo);
            Assert.AreEqual(1, first.Groups.Count);
            Assert.AreEqual(2, second.Groups.Count);
            Undo.PerformUndo();
            Assert.IsEmpty(first.Groups);
            Assert.AreEqual(1, second.Groups.Count);
            Undo.PerformRedo();
            using (var serialized = new SerializedObject(second))
            {
                AudioClipLibraryEditor.AddEntry(serialized, 0);
                serialized.FindProperty("Groups").GetArrayElementAtIndex(0).FindPropertyRelative("Entries")
                    .GetArrayElementAtIndex(0).FindPropertyRelative("Weight").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            AudioClipLibraryEditor.AddEntry(editor.serializedObject, 0);
            FlushUndo(undo);
            CollectionAssert.AreEqual(new[] { 1f }, first.Groups[0].Entries.Select(entry => entry.Weight));
            CollectionAssert.AreEqual(new[] { 0f, 1f }, second.Groups[0].Entries.Select(entry => entry.Weight));
            Assert.IsNull(second.Groups[0].Entries[1].Clip);
            Assert.IsEmpty(second.Groups[0].Entries[1].Key);
            Undo.PerformUndo();
            Assert.IsEmpty(first.Groups[0].Entries);
            CollectionAssert.AreEqual(new[] { 0f }, second.Groups[0].Entries.Select(entry => entry.Weight));
            Undo.PerformRedo();
            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            AudioClipLibraryEditor.RemoveEntry(editor.serializedObject, 0, 0);
            FlushUndo(undo);
            Assert.IsEmpty(first.Groups[0].Entries);
            CollectionAssert.AreEqual(new[] { 1f }, second.Groups[0].Entries.Select(entry => entry.Weight));
            Undo.PerformUndo();
            CollectionAssert.AreEqual(new[] { 0f, 1f }, second.Groups[0].Entries.Select(entry => entry.Weight));
            Undo.IncrementCurrentGroup();
            undo = Undo.GetCurrentGroup();
            AudioClipLibraryEditor.RemoveGroup(editor.serializedObject, 1);
            FlushUndo(undo);
            Assert.AreEqual(1, first.Groups.Count);
            Assert.AreEqual(1, second.Groups.Count);
            Undo.PerformUndo();
            Assert.AreEqual(2, second.Groups.Count);
        }

        [Test]
        public void GroupPrefabRoundTripPreservesFieldsAndPersistentUnityEvent()
        {
            var library = Library("Library");
            var player = Player();
            var source = Own(player.gameObject.AddComponent<AudioSource>());
            source.playOnAwake = false;
            using (var serialized = new SerializedObject(player))
            {
                serialized.FindProperty("library").objectReferenceValue = library;
                serialized.FindProperty("groupName").stringValue = "UI/Click";
                serialized.FindProperty("channelName").stringValue = "ui/click";
                serialized.FindProperty("category").intValue = (int)AudioCategory.Voice;
                serialized.FindProperty("channelMode").intValue = (int)AudioChannelMode.Exclusive;
                serialized.FindProperty("borrowedSource").objectReferenceValue = source;
                serialized.FindProperty("persistenceId").stringValue = "settings/ui";
                serialized.FindProperty("autoPlay").intValue = (int)AudioGroupAutoPlay.EveryEnable;
                serialized.FindProperty("loadAsync").boolValue = false;
                var options = serialized.FindProperty("options");
                options.FindPropertyRelative("Volume").floatValue = 2f;
                options.FindPropertyRelative("Pitch").floatValue = 1.25f;
                options.FindPropertyRelative("Loop").boolValue = true;
                options.FindPropertyRelative("AutoPauseWhenTimeScaleZero").boolValue = true;
                options.FindPropertyRelative("OutputRoute").FindPropertyRelative("Mode").intValue = (int)AudioOutputRoute.RouteMode.Direct;
                options.FindPropertyRelative("AutoFade").FindPropertyRelative("FadeInSeconds").floatValue = .2f;
                serialized.ApplyModifiedProperties();
            }
            UnityEventTools.AddPersistentListener(player.Started, new UnityAction(player.Stop));
            var path = _folder + "/group.prefab";
            Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(player.gameObject, path));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var restored = contents.GetComponent<AudioGroupPlayer>();
                Assert.IsNotNull(restored);
                using (var serialized = new SerializedObject(restored))
                {
                    Assert.AreEqual(library, serialized.FindProperty("library").objectReferenceValue);
                    Assert.AreEqual("UI/Click", serialized.FindProperty("groupName").stringValue);
                    Assert.AreEqual("ui/click", serialized.FindProperty("channelName").stringValue);
                    Assert.AreEqual((int)AudioCategory.Voice, serialized.FindProperty("category").intValue);
                    Assert.AreEqual((int)AudioChannelMode.Exclusive, serialized.FindProperty("channelMode").intValue);
                    Assert.AreEqual(contents.GetComponent<AudioSource>(), serialized.FindProperty("borrowedSource").objectReferenceValue,
                        "The borrowed reference must remap to the AudioSource inside the reopened prefab, not the original scene object.");
                    Assert.AreEqual("settings/ui", serialized.FindProperty("persistenceId").stringValue);
                    Assert.AreEqual((int)AudioGroupAutoPlay.EveryEnable, serialized.FindProperty("autoPlay").intValue);
                    Assert.IsFalse(serialized.FindProperty("loadAsync").boolValue);
                    var options = serialized.FindProperty("options");
                    Assert.AreEqual(2f, options.FindPropertyRelative("Volume").floatValue);
                    Assert.AreEqual(1.25f, options.FindPropertyRelative("Pitch").floatValue);
                    Assert.IsTrue(options.FindPropertyRelative("Loop").boolValue);
                    Assert.IsTrue(options.FindPropertyRelative("AutoPauseWhenTimeScaleZero").boolValue);
                    Assert.AreEqual((int)AudioOutputRoute.RouteMode.Direct, options.FindPropertyRelative("OutputRoute").FindPropertyRelative("Mode").intValue);
                    Assert.AreEqual(.2f, options.FindPropertyRelative("AutoFade").FindPropertyRelative("FadeInSeconds").floatValue);
                }
                Assert.AreEqual(1, restored.Started.GetPersistentEventCount());
                Assert.AreEqual(restored, restored.Started.GetPersistentTarget(0));
                Assert.AreEqual("Stop", restored.Started.GetPersistentMethodName(0));
                Assert.AreEqual(UnityEventCallState.RuntimeOnly, restored.Started.GetPersistentListenerState(0));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void MultiObjectPlayerEditorRecordsUndoAndPrefabOverridesWithoutChangingSource()
        {
            var original = Player();
            using (var serialized = new SerializedObject(original))
            {
                serialized.FindProperty("channelName").stringValue = "original";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var path = _folder + "/override.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(original.gameObject, path);
            var instance = Own((GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene));
            var first = Own(instance.GetComponent<AudioGroupPlayer>());
            var second = Player();
            var editor = Own(UnityEditor.Editor.CreateEditor(new Object[] { first, second }));
            Assert.AreEqual(typeof(AudioGroupPlayerEditor), editor.GetType());
            Assert.IsTrue(editor.GetType().IsDefined(typeof(CanEditMultipleObjects), false));
            Undo.IncrementCurrentGroup();
            var undo = Undo.GetCurrentGroup();
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("channelName").stringValue = "edited/channel";
            editor.serializedObject.FindProperty("options").FindPropertyRelative("Volume").floatValue = 2f;
            editor.serializedObject.ApplyModifiedProperties();
            FlushUndo(undo);
            using (var serialized = new SerializedObject(first))
            {
                Assert.AreEqual("edited/channel", serialized.FindProperty("channelName").stringValue);
                Assert.IsTrue(serialized.FindProperty("channelName").prefabOverride);
                Assert.IsTrue(serialized.FindProperty("options").FindPropertyRelative("Volume").prefabOverride);
            }
            using (var serialized = new SerializedObject(prefab.GetComponent<AudioGroupPlayer>()))
                Assert.AreEqual("original", serialized.FindProperty("channelName").stringValue);
            Undo.PerformUndo();
            using (var serialized = new SerializedObject(first)) Assert.AreEqual("original", serialized.FindProperty("channelName").stringValue);
            using (var serialized = new SerializedObject(second)) Assert.IsTrue(string.IsNullOrEmpty(serialized.FindProperty("channelName").stringValue));
            Undo.PerformRedo();
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            using (var serialized = new SerializedObject(first))
            {
                serialized.FindProperty("channelName").stringValue = "temporary";
                serialized.ApplyModifiedProperties();
            }
            PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
            using (var serialized = new SerializedObject(first)) Assert.AreEqual("edited/channel", serialized.FindProperty("channelName").stringValue);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                using (var serialized = new SerializedObject(contents.GetComponent<AudioGroupPlayer>()))
                {
                    Assert.AreEqual("edited/channel", serialized.FindProperty("channelName").stringValue);
                    Assert.AreEqual(2f, serialized.FindProperty("options").FindPropertyRelative("Volume").floatValue);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void LibraryDiagnosticsIdentifyInvalidGroupsWithoutChangingAssetsOrRandom()
        {
            var library = Library("Invalid");
            library.Groups.Add(new AudioClipGroup { Name = "duplicate", Selection = AudioClipSelectionMode.Weighted,
                Entries = new List<AudioClipEntry> { new AudioClipEntry { Source = AudioClipSource.Resources, Key = "wrong", Weight = -1f } } });
            library.Groups.Add(new AudioClipGroup { Name = "duplicate" });
            var before = EditorJsonUtility.ToJson(library);
            var random = UnityEngine.Random.state;
            var warnings = AudioClipLibraryEditor.GetWarnings(library);
            Assert.That(warnings.Any(message => message.Contains("Duplicate")));
            Assert.That(warnings.Any(message => message.Contains("finite and nonnegative")));
            Assert.That(warnings.Any(message => message.Contains("explicit ProjectId/address migration")));
            Assert.That(warnings.Any(message => message.Contains("No selectable")));
            Assert.AreEqual(before, EditorJsonUtility.ToJson(library));
            Assert.AreEqual(random, UnityEngine.Random.state);
            library.Groups[0].Selection = AudioClipSelectionMode.Uniform;
            Assert.IsFalse(AudioClipLibraryEditor.GetWarnings(library, "duplicate").Any(message => message.Contains("finite and nonnegative")),
                "Uniform ignores weights; diagnostics must not reject ignored values.");
        }

        [Test]
        public void PlayerDiagnosticsIdentifyMissingGroupInvalidChannelAndOptionsWithoutRepairingThem()
        {
            var library = Library("Library");
            var player = Player();
            var source = Own(player.gameObject.AddComponent<AudioSource>());
            source.playOnAwake = false;
            using (var serialized = new SerializedObject(player))
            {
                serialized.FindProperty("library").objectReferenceValue = library;
                serialized.FindProperty("groupName").stringValue = "missing";
                serialized.FindProperty("channelName").stringValue = "bad channel";
                serialized.FindProperty("persistenceId").stringValue = "bad id";
                serialized.FindProperty("options").FindPropertyRelative("Pitch").floatValue = 0f;
                serialized.FindProperty("borrowedSource").objectReferenceValue = source;
                serialized.FindProperty("channelMode").intValue = (int)AudioChannelMode.Overlap;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var before = EditorJsonUtility.ToJson(player);
                var warnings = AudioGroupPlayerEditor.GetWarnings(serialized);
                Assert.That(warnings.Any(message => message.Contains("not found")));
                Assert.That(warnings.Any(message => message.Contains("channelName")));
                Assert.That(warnings.Any(message => message.Contains("persistenceId")));
                Assert.That(warnings.Any(message => message.Contains("Pitch")));
                Assert.That(warnings.Any(message => message.Contains("Exclusive") && message.Contains("Overlap")));
                Assert.That(warnings.Any(message => message.Contains("active and enabled")));
                Assert.AreEqual(before, EditorJsonUtility.ToJson(player));
                serialized.FindProperty("channelMode").intValue = (int)AudioChannelMode.Exclusive;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(AudioGroupPlayerEditor.GetWarnings(serialized).Any(message => message.Contains("Overlap")));
            }
        }

        [UnityTest]
        public IEnumerator CustomEditorGuiDrawsValidInvalidAndMultiObjectTargetsWithoutMutation()
        {
            var valid = Library("ValidGui");
            var invalid = Library("InvalidGui");
            var clip = Own(AudioClip.Create("GuiClip", 100, 1, 1000, false));
            valid.Groups.Add(new AudioClipGroup { Name = "click", Entries = new List<AudioClipEntry> { new AudioClipEntry { Clip = clip } } });
            invalid.Groups.Add(new AudioClipGroup { Name = "click", Selection = AudioClipSelectionMode.Weighted,
                Entries = new List<AudioClipEntry> { new AudioClipEntry { Source = AudioClipSource.Resources, Key = "bad", Weight = -1f }, new AudioClipEntry { Weight = 0f } } });
            var validPlayer = Player();
            var invalidPlayer = Player();
            using (var serialized = new SerializedObject(validPlayer))
            {
                serialized.FindProperty("library").objectReferenceValue = valid;
                serialized.FindProperty("groupName").stringValue = "click";
                serialized.FindProperty("channelName").stringValue = "gui/valid";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var serialized = new SerializedObject(invalidPlayer))
            {
                serialized.FindProperty("library").objectReferenceValue = invalid;
                serialized.FindProperty("groupName").stringValue = "click";
                serialized.FindProperty("channelName").stringValue = "bad channel";
                serialized.FindProperty("channelMode").intValue = (int)AudioChannelMode.Overlap;
                var source = Own(invalidPlayer.gameObject.AddComponent<AudioSource>());
                source.playOnAwake = false;
                serialized.FindProperty("borrowedSource").objectReferenceValue = source;
                serialized.FindProperty("options").FindPropertyRelative("Pitch").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var editors = new[]
            {
                Own(UnityEditor.Editor.CreateEditor(valid)), Own(UnityEditor.Editor.CreateEditor(invalid)),
                Own(UnityEditor.Editor.CreateEditor(new Object[] { valid, invalid })),
                Own(UnityEditor.Editor.CreateEditor(validPlayer)), Own(UnityEditor.Editor.CreateEditor(invalidPlayer)),
                Own(UnityEditor.Editor.CreateEditor(new Object[] { validPlayer, invalidPlayer }))
            };
            for (var i = 0; i < editors.Length; i++)
            {
                Assert.AreEqual(i < 3 ? typeof(AudioClipLibraryEditor) : typeof(AudioGroupPlayerEditor), editors[i].GetType());
                var groups = editors[i].serializedObject.FindProperty("Groups");
                if (groups != null)
                    for (var j = 0; j < groups.arraySize; j++)
                    {
                        var group = groups.GetArrayElementAtIndex(j);
                        group.isExpanded = true;
                        var entries = group.FindPropertyRelative("Entries");
                        for (var k = 0; k < entries.arraySize; k++) entries.GetArrayElementAtIndex(k).isExpanded = true;
                    }
                else
                {
                    editors[i].serializedObject.FindProperty("options.OutputRoute").isExpanded = true;
                    editors[i].serializedObject.FindProperty("options.AutoFade").isExpanded = true;
                    editors[i].serializedObject.FindProperty("onStarted").isExpanded = true;
                }
            }
            var targets = new Object[] { valid, invalid, validPlayer, invalidPlayer };
            var before = targets.Select(value => EditorJsonUtility.ToJson(value)).ToArray();
            var hadHost = AudioManager.HasInstance;
            var settings = AudioKit.CurrentSettings;
            var window = Own(ScriptableObject.CreateInstance<AudioGroupInspectorTestWindow>());
            window.Editors = editors;
            window.LayoutCounts = new int[editors.Length];
            window.RepaintCounts = new int[editors.Length];
            window.position = new Rect(100, 100, 700, 700);
            window.ShowUtility();
            for (var frame = 0; frame < 10 && (window.LayoutCounts.Any(count => count == 0) || window.RepaintCounts.Any(count => count == 0)); frame++)
            {
                window.Repaint();
                yield return null;
            }
            for (var i = 0; i < editors.Length; i++)
            {
                Assert.Greater(window.LayoutCounts[i], 0, "Layout was not observed for editor " + i);
                Assert.Greater(window.RepaintCounts[i], 0, "Repaint was not observed for editor " + i);
            }
            CollectionAssert.AreEqual(before, targets.Select(value => EditorJsonUtility.ToJson(value)).ToArray(), "Drawing must not repair invalid data or truncate mixed lists.");
            Assert.AreEqual(hadHost, AudioManager.HasInstance, "Drawing must not initialize a playback host.");
            Assert.AreSame(settings, AudioKit.CurrentSettings, "Drawing must not initialize Settings.");
            LogAssert.NoUnexpectedReceived();
        }
    }

    internal class AudioGroupInspectorTestWindow : EditorWindow
    {
        internal UnityEditor.Editor[] Editors;
        internal int[] LayoutCounts;
        internal int[] RepaintCounts;
        private Vector2 _scroll;

        private void OnGUI()
        {
            if (Editors == null) return;
            var type = Event.current.type;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < Editors.Length; i++)
            {
                if (!Editors[i]) continue;
                Editors[i].OnInspectorGUI();
                if (type == EventType.Layout) LayoutCounts[i]++;
                if (type == EventType.Repaint) RepaintCounts[i]++;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
