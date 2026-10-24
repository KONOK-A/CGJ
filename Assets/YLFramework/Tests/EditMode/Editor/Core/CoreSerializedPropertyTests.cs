using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YLFramework.Editor.Utilities;

namespace YLFramework.Core.Editor.Tests
{
    public class CoreSerializedPropertyTests
    {
        [Serializable]
        public class Data
        {
            public int number = 1;
            public string text = "original";
        }
        [Serializable]
        public class OtherData
        {
            public int number = 7;
            public int text = 8;
        }
        public class Fixture : ScriptableObject
        {
            public int number = 1;
            public string text = "original";
            public UnityEngine.Object reference;
            public Texture2D texture;
            public Data data = new Data();
            public OtherData other = new OtherData();
            public int[] array = { 1, 2 };
        }

        private Fixture _first;
        private Fixture _second;
        private string _clipboard;

        [SetUp]
        public void SetUp()
        {
            _first = ScriptableObject.CreateInstance<Fixture>();
            _second = ScriptableObject.CreateInstance<Fixture>();
            _clipboard = EditorGUIUtility.systemCopyBuffer;
        }

        [TearDown]
        public void TearDown()
        {
            EditorGUIUtility.systemCopyBuffer = _clipboard;
            Undo.ClearUndo(_first);
            Undo.ClearUndo(_second);
            UnityEngine.Object.DestroyImmediate(_first);
            UnityEngine.Object.DestroyImmediate(_second);
        }

        [Test]
        public void NullObjectReferenceHasReadableTextAndRoundTripsWithoutDereferencing()
        {
            using var source = new SerializedObject(_first);
            using var destination = new SerializedObject(_second);
            var property = source.FindProperty("reference");
            Assert.That(property.GetValueText(), Is.EqualTo("None"));
            property.CopyToClipboard();
            Assert.That(destination.FindProperty("reference").IsCanPaste(), Is.True);
            destination.FindProperty("reference").PasteFromClipboard();
            Assert.That(_second.reference, Is.Null);
        }

        [Test]
        public void TypedObjectReferenceRejectsIncompatibleObjectsButKeepsValidReferences()
        {
            using var source = new SerializedObject(_first);
            using var destination = new SerializedObject(_second);
            _first.reference = _first;
            source.Update();
            source.FindProperty("reference").CopyToClipboard();
            var property = destination.FindProperty("texture");
            Assert.That(property.IsCanPaste(), Is.False);
            Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
            Assert.That(_second.texture, Is.Null);
            Assert.That(destination.hasModifiedProperties, Is.False);
            var texture = new Texture2D(1, 1);
            try
            {
                _first.reference = texture;
                source.Update();
                source.FindProperty("reference").CopyToClipboard();
                Assert.That(property.IsCanPaste(), Is.True);
                property.PasteFromClipboard();
                Assert.That(_second.texture, Is.SameAs(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase("UnityEditor.MissingClipboard61", "SetSerializedProperty", typeof(void), true)]
        [TestCase("UnityEditor.Clipboard", "SetSerializedProperty", typeof(bool), true)]
        [TestCase("UnityEditor.Clipboard", "SetSerializedProperty", typeof(void), false)]
        public void MissingOrChangedNativeApiIsRejectedWithVersionAndMemberFeedback(string typeName, string name, Type result, bool correctParameter)
        {
            var resolve = typeof(SerializedPropertyExtensions).GetMethod("GetClipboardMethod", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            var failure = Assert.Catch(() => resolve.Invoke(null, new object[]
                { typeName, name, result, new[] { correctParameter ? typeof(SerializedProperty) : typeof(string) } }));
            Assert.That(failure.GetBaseException(), Is.TypeOf<NotSupportedException>());
            Assert.That(failure.GetBaseException().Message, Does.Contain(Application.unityVersion).And.Contain(name));
        }

        [Test]
        public void IncompatibleScalarAndPartiallyCompatibleStructAreRejectedBeforeAnyWrite()
        {
            _first.data.number = 42;
            using var source = new SerializedObject(_first);
            using var destination = new SerializedObject(_second);
            foreach (var pair in new[] { new[] { "number", "text" }, new[] { "data", "other" } })
            {
                source.FindProperty(pair[0]).CopyToClipboard();
                var property = destination.FindProperty(pair[1]);
                Assert.That(property.IsCanPaste(), Is.False);
                Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
                Assert.That(destination.hasModifiedProperties, Is.False);
                Assert.That(_second.text, Is.EqualTo("original"));
                Assert.That(_second.other.number, Is.EqualTo(7));
                Assert.That(_second.other.text, Is.EqualTo(8));
            }
        }

        [Test]
        public void MalformedNativePayloadCannotChangeTargetOrPendingEdits()
        {
            using var serialized = new SerializedObject(_first);
            serialized.FindProperty("text").stringValue = "pending";
            var property = serialized.FindProperty("number");
            foreach (string payload in new[] { "GenericPropertyJSON:{", "GenericPropertyJSON:{}" })
            {
                EditorGUIUtility.systemCopyBuffer = payload;
                Assert.That(property.IsCanPaste(), Is.False);
                Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
                Assert.That(_first.number, Is.EqualTo(1));
                Assert.That(_first.text, Is.EqualTo("original"));
                Assert.That(serialized.FindProperty("text").stringValue, Is.EqualTo("pending"));
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(payload));
            }
        }

        [Test]
        public void MixedCopyAndPasteAreExplicitlyRejectedWithoutChangingEitherObject()
        {
            _second.number = 2;
            using var single = new SerializedObject(_first);
            using var multiple = new SerializedObject(new UnityEngine.Object[] { _first, _second });
            var mixed = multiple.FindProperty("number");
            Assert.That(mixed.hasMultipleDifferentValues, Is.True);
            single.FindProperty("number").CopyToClipboard();
            string before = EditorGUIUtility.systemCopyBuffer;
            Assert.Throws<InvalidOperationException>(() => mixed.CopyToClipboard());
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(before));
            Assert.That(mixed.IsCanPaste(), Is.False);
            Assert.Throws<InvalidOperationException>(() => mixed.PasteFromClipboard());
            Assert.That(_first.number, Is.EqualTo(1));
            Assert.That(_second.number, Is.EqualTo(2));
            Assert.That(multiple.hasModifiedProperties, Is.False);
        }

        [Test]
        public void SameValueMultiObjectPasteSupportsActualUndoAndRedo()
        {
            using var source = new SerializedObject(_first);
            source.FindProperty("number").intValue = 42;
            source.FindProperty("number").CopyToClipboard();
            using var multiple = new SerializedObject(new UnityEngine.Object[] { _first, _second });
            Undo.IncrementCurrentGroup();
            var destination = multiple.FindProperty("number");
            Assert.That(destination.IsCanPaste(), Is.True);
            destination.PasteFromClipboard();
            Undo.FlushUndoRecordObjects();
            Assert.That(_first.number, Is.EqualTo(42));
            Assert.That(_second.number, Is.EqualTo(42));
            Undo.PerformUndo();
            Assert.That(_first.number, Is.EqualTo(1));
            Assert.That(_second.number, Is.EqualTo(1));
            Undo.PerformRedo();
            Assert.That(_first.number, Is.EqualTo(42));
            Assert.That(_second.number, Is.EqualTo(42));
            Undo.IncrementCurrentGroup();
        }

        [TestCase("data")]
        [TestCase("array")]
        public void NativeStructuredPayloadRoundTripsCompletely(string path)
        {
            _first.data.number = 42;
            _first.data.text = "copied";
            _first.array = new[] { 3, 4, 5 };
            using var source = new SerializedObject(_first);
            using var destination = new SerializedObject(_second);
            source.FindProperty(path).CopyToClipboard();
            Assert.That(destination.FindProperty(path).IsCanPaste(), Is.True);
            destination.FindProperty(path).PasteFromClipboard();
            if (path == "data")
            {
                Assert.That(_second.data.number, Is.EqualTo(42));
                Assert.That(_second.data.text, Is.EqualTo("copied"));
            }
            else
            {
                Assert.That(_second.array, Is.EqualTo(new[] { 3, 4, 5 }));
            }
        }

        [TestCase("data")]
        [TestCase("array")]
        public void SameValueMultiObjectStructuredPasteChangesEveryTargetAndSupportsUndo(string path)
        {
            using var source = new SerializedObject(_first);
            source.FindProperty("data.number").intValue = 42;
            source.FindProperty("data.text").stringValue = "copied";
            source.FindProperty("array").arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                source.FindProperty("array").GetArrayElementAtIndex(i).intValue = i + 3;
            }

            source.FindProperty(path).CopyToClipboard();
            using var multiple = new SerializedObject(new UnityEngine.Object[] { _first, _second });
            Undo.IncrementCurrentGroup();
            var property = multiple.FindProperty(path);
            Assert.That(property.IsCanPaste(), Is.True);
            property.PasteFromClipboard();
            Undo.FlushUndoRecordObjects();
            foreach (var target in new[] { _first, _second })
            {
                if (path == "data")
                {
                    Assert.That(target.data.number, Is.EqualTo(42));
                    Assert.That(target.data.text, Is.EqualTo("copied"));
                }
                else
                {
                    Assert.That(target.array, Is.EqualTo(new[] { 3, 4, 5 }));
                }
            }
            Undo.PerformUndo();
            foreach (var target in new[] { _first, _second })
            {
                Assert.That(target.data.number, Is.EqualTo(1));
                Assert.That(target.data.text, Is.EqualTo("original"));
                Assert.That(target.array, Is.EqualTo(new[] { 1, 2 }));
            }
            Undo.IncrementCurrentGroup();
        }

        [Test]
        public void PrefabInstancePasteRecordsOverrideAndUndoRedoWithoutChangingAsset()
        {
            string folder = "Assets/YLFramework/Tests/EditMode/Editor/Core/YLClipboardTest_" + Guid.NewGuid().ToString("N");
            string path = folder + "/Clipboard.prefab";
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            BoxCollider collider = null;
            try
            {
                AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode/Editor/Core", Path.GetFileName(folder));
                var go = EditorUtility.CreateGameObjectWithHideFlags("Clipboard", HideFlags.None, typeof(BoxCollider));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                byte[] before = File.ReadAllBytes(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                collider = instance.GetComponent<BoxCollider>();
                using var source = new SerializedObject(go.GetComponent<BoxCollider>());
                source.FindProperty("m_Center").vector3Value = new Vector3(2, 3, 4);
                source.FindProperty("m_Center").CopyToClipboard();
                using var destination = new SerializedObject(collider);
                Undo.IncrementCurrentGroup();
                destination.FindProperty("m_Center").PasteFromClipboard();
                Undo.FlushUndoRecordObjects();
                destination.Update();
                Assert.That(collider.center, Is.EqualTo(new Vector3(2, 3, 4)));
                Assert.That(destination.FindProperty("m_Center").prefabOverride, Is.True);
                Undo.PerformUndo();
                destination.Update();
                Assert.That(collider.center, Is.EqualTo(Vector3.zero));
                Assert.That(destination.FindProperty("m_Center").prefabOverride, Is.False);
                Undo.PerformRedo();
                destination.Update();
                Assert.That(collider.center, Is.EqualTo(new Vector3(2, 3, 4)));
                Assert.That(destination.FindProperty("m_Center").prefabOverride, Is.True);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }
            finally
            {
                if (collider)
                {
                    Undo.ClearUndo(collider);
                }

                if (instance)
                {
                    Undo.ClearUndo(instance);
                }

                Undo.IncrementCurrentGroup();
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void AcceptanceAuthoringFieldsKeepExactFormerNames()
        {
            var contracts = new[]
            {
                "YLFramework.Tests.Extensions.Data.ExcelAssetOwnerFixture|Rows=rows",
                "YLFramework.Tests.Extensions.Shared.E0ReorderableTestWindow|GuiCalls=guiCalls",
                "YLFramework.Tests.Extensions.Shared.E0ReorderableListAsset|Items=items",
                "YLFramework.Tests.Extensions.Behavior.BehaviorAuthoringTests+DrawerScopeWindow|Host=host;Complete=isComplete",
                "YLFramework.Tests.Extensions.Behavior.BehaviorAuthoringTests+TransitionInspectorWindow|Helper=helper;NodeHelper=nodeHelper;Inspector=inspector;DrawCount=drawCount",
                "YLFramework.Tests.ResourceBuild.ResourcePolishGuiWindow|RootHeight=rootHeight;LayoutTrace=layoutTrace",
                "YLFramework.Tests.EditHostProbe|Initializations=initializations;Releases=releases",
                "YLFramework.Tests.Extensions.Data.LocalizationActivationCounter|Enables=enables;Disables=disables",
                "YLFramework.Tests.Extensions.Equipment.EquipmentResourceProbe|Loads=loads;Unloads=unloads",
                "YLFramework.Tests.Audio.AudioBudgetStopOnDisable|Calls=calls;DisableCache=disableCache",
                "YLFramework.Tests.RuntimeHostProbe|Releases=releases",
                "YLFramework.Core.Tests.ColorInputGateProbe|Allowed=isAllowed;Reads=reads",
                "YLFramework.Core.Editor.Tests.Settings.First.CoreEditorSettingsFixture|Marker=marker",
                "YLFramework.Core.Editor.Tests.Settings.Second.CoreEditorSettingsFixture|Marker=marker",
                "YLFramework.Core.Editor.Tests.CoreEditorGeneratedAssetFixture|Marker=marker",
                "YLFramework.Core.Tests.RuntimeAcceptanceEntry|ColorPrefab=colorPrefab;ServerUrl=serverUrl;GameMode=gameMode",
                "YLFramework.Core.Tests.AudioAcceptanceEntry|Group=group;Signal=signal;SecondSignal=secondSignal;MixerConfig=mixerConfig;ResourceKey=resourceKey;GameMode=gameMode",
                "YLFramework.Core.Tests.UIPanelAcceptanceDragProbe|Drags=drags",
                "YLFramework.Core.Tests.UIAcceptanceEntry|RootPrefab=rootPrefab;ColorPrefab=colorPrefab",
                "YLFramework.Core.Tests.YLCoreSelectionInputProbe|Cancels=cancels",
                "YLFramework.Core.Tests.YLCoreOtherInputGate|Allowed=isAllowed;Reads=reads",
                "YLFramework.Core.Tests.YLCoreNativeDragProbe|Drags=drags",
                "YLFramework.Tests.DomainAcceptance.DomainAcceptanceProbe|_resourcePrefix=resourcePrefix;_run=run;_expectedBackend=expectedBackend;_expectLegacyLogger=expectLegacyLogger"
            };
            foreach (var contract in contracts)
            {
                var parts = contract.Split('|');
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(parts[0], false))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(type, Is.Not.Null, parts[0]);
                foreach (var entry in parts[1].Split(';'))
                {
                    var names = entry.Split('=');
                    var field = type.GetField(names[1], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Assert.That(field, Is.Not.Null, parts[0] + "." + names[1]);
                    var aliases = field.GetCustomAttributes<UnityEngine.Serialization.FormerlySerializedAsAttribute>()
                        .Select(attribute => attribute.oldName).ToArray();
                    Assert.That(aliases, Is.EqualTo(new[] { names[0] }), parts[0] + "." + names[1]);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAcceptancePrefabKeepsReferencesFlagsAndIdentity(bool gameMode)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("YLFramework.Core.Tests.RuntimeAcceptanceEntry", false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null);
            Assert.That(type.GetField("colorPrefab"), Is.Not.Null);
            string folder = "Assets/YLFramework/Tests/EditMode/Editor/Core/AuthoringMigration-" + Guid.NewGuid().ToString("N");
            string path = folder + "/Legacy.prefab";
            GameObject host = null;
            GameObject contents = null;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode/Editor/Core", Path.GetFileName(folder));
                host = new GameObject("Legacy Acceptance Authoring");
                host.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
                var component = host.AddComponent(type);
                using (var serialized = new SerializedObject(component))
                {
                    serialized.FindProperty("colorPrefab").objectReferenceValue = host;
                    serialized.FindProperty("serverUrl").stringValue = "https://127.0.0.1/style-fixture";
                    serialized.FindProperty("gameMode").boolValue = gameMode;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.That(PrefabUtility.SaveAsPrefabAsset(host, path), Is.Not.Null);
                byte[] meta = File.ReadAllBytes(path + ".meta");
                string guid = AssetDatabase.AssetPathToGUID(path);
                string yaml = File.ReadAllText(path);
                foreach (var names in new[] { new[] { "colorPrefab", "ColorPrefab" }, new[] { "serverUrl", "ServerUrl" }, new[] { "gameMode", "GameMode" } })
                {
                    Assert.That(yaml, Does.Contain("\n  " + names[0] + ":"));
                    yaml = yaml.Replace("\n  " + names[0] + ":", "\n  " + names[1] + ":");
                }
                var ids = System.Text.RegularExpressions.Regex.Matches(yaml, @"(?m)^--- !u!\d+ &(-?\d+)")
                    .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value).ToArray();
                File.WriteAllText(path, yaml, new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                contents = PrefabUtility.LoadPrefabContents(path);
                using (var serialized = new SerializedObject(contents.GetComponent(type)))
                {
                    Assert.That(serialized.FindProperty("colorPrefab").objectReferenceValue, Is.EqualTo(contents));
                    Assert.That(serialized.FindProperty("serverUrl").stringValue, Is.EqualTo("https://127.0.0.1/style-fixture"));
                    Assert.That(serialized.FindProperty("gameMode").boolValue, Is.EqualTo(gameMode));
                }
                Assert.That(PrefabUtility.SaveAsPrefabAsset(contents, path), Is.Not.Null);
                PrefabUtility.UnloadPrefabContents(contents);
                contents = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                using (var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(type)))
                {
                    Assert.That(serialized.FindProperty("gameMode").boolValue, Is.EqualTo(gameMode));
                    Assert.That(serialized.FindProperty("serverUrl").stringValue, Is.EqualTo("https://127.0.0.1/style-fixture"));
                }
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(meta));
                Assert.That(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), @"(?m)^--- !u!\d+ &(-?\d+)")
                    .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value), Is.EqualTo(ids));
            }
            finally
            {
                if (contents)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                if (host)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
                EditorSceneManager.ClosePreviewScene(scene);
                if (AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }
    }
}
