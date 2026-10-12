using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YLFramework.Core.Editor.Tests
{
    public class CoreSerializedPropertyTests
    {
        [Serializable] public class Data { public int number = 1; public string text = "original"; }
        [Serializable] public class OtherData { public int number = 7; public int text = 8; }
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

        private Fixture first;
        private Fixture second;
        private string clipboard;

        [SetUp]
        public void SetUp()
        {
            first = ScriptableObject.CreateInstance<Fixture>();
            second = ScriptableObject.CreateInstance<Fixture>();
            clipboard = EditorGUIUtility.systemCopyBuffer;
        }

        [TearDown]
        public void TearDown()
        {
            EditorGUIUtility.systemCopyBuffer = clipboard;
            Undo.ClearUndo(first);
            Undo.ClearUndo(second);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }

        [Test]
        public void NullObjectReferenceHasReadableTextAndRoundTripsWithoutDereferencing()
        {
            using var source = new SerializedObject(first);
            using var destination = new SerializedObject(second);
            var property = source.FindProperty("reference");
            Assert.That(property.GetValueText(), Is.EqualTo("None"));
            property.CopyToClipboard();
            Assert.That(destination.FindProperty("reference").IsCanPaste(), Is.True);
            destination.FindProperty("reference").PasteFromClipboard();
            Assert.That(second.reference, Is.Null);
        }

        [Test]
        public void TypedObjectReferenceRejectsIncompatibleObjectsButKeepsValidReferences()
        {
            using var source = new SerializedObject(first);
            using var destination = new SerializedObject(second);
            first.reference = first;
            source.Update();
            source.FindProperty("reference").CopyToClipboard();
            var property = destination.FindProperty("texture");
            Assert.That(property.IsCanPaste(), Is.False);
            Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
            Assert.That(second.texture, Is.Null);
            Assert.That(destination.hasModifiedProperties, Is.False);
            var texture = new Texture2D(1, 1);
            try
            {
                first.reference = texture;
                source.Update();
                source.FindProperty("reference").CopyToClipboard();
                Assert.That(property.IsCanPaste(), Is.True);
                property.PasteFromClipboard();
                Assert.That(second.texture, Is.SameAs(texture));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
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
            first.data.number = 42;
            using var source = new SerializedObject(first);
            using var destination = new SerializedObject(second);
            foreach (var pair in new[] { new[] { "number", "text" }, new[] { "data", "other" } })
            {
                source.FindProperty(pair[0]).CopyToClipboard();
                var property = destination.FindProperty(pair[1]);
                Assert.That(property.IsCanPaste(), Is.False);
                Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
                Assert.That(destination.hasModifiedProperties, Is.False);
                Assert.That(second.text, Is.EqualTo("original"));
                Assert.That(second.other.number, Is.EqualTo(7));
                Assert.That(second.other.text, Is.EqualTo(8));
            }
        }

        [Test]
        public void MalformedNativePayloadCannotChangeTargetOrPendingEdits()
        {
            using var serialized = new SerializedObject(first);
            serialized.FindProperty("text").stringValue = "pending";
            var property = serialized.FindProperty("number");
            foreach (string payload in new[] { "GenericPropertyJSON:{", "GenericPropertyJSON:{}" })
            {
                EditorGUIUtility.systemCopyBuffer = payload;
                Assert.That(property.IsCanPaste(), Is.False);
                Assert.Throws<InvalidOperationException>(() => property.PasteFromClipboard());
                Assert.That(first.number, Is.EqualTo(1));
                Assert.That(first.text, Is.EqualTo("original"));
                Assert.That(serialized.FindProperty("text").stringValue, Is.EqualTo("pending"));
                Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(payload));
            }
        }

        [Test]
        public void MixedCopyAndPasteAreExplicitlyRejectedWithoutChangingEitherObject()
        {
            second.number = 2;
            using var single = new SerializedObject(first);
            using var multiple = new SerializedObject(new UnityEngine.Object[] { first, second });
            var mixed = multiple.FindProperty("number");
            Assert.That(mixed.hasMultipleDifferentValues, Is.True);
            single.FindProperty("number").CopyToClipboard();
            string before = EditorGUIUtility.systemCopyBuffer;
            Assert.Throws<InvalidOperationException>(() => mixed.CopyToClipboard());
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(before));
            Assert.That(mixed.IsCanPaste(), Is.False);
            Assert.Throws<InvalidOperationException>(() => mixed.PasteFromClipboard());
            Assert.That(first.number, Is.EqualTo(1));
            Assert.That(second.number, Is.EqualTo(2));
            Assert.That(multiple.hasModifiedProperties, Is.False);
        }

        [Test]
        public void SameValueMultiObjectPasteSupportsActualUndoAndRedo()
        {
            using var source = new SerializedObject(first);
            source.FindProperty("number").intValue = 42;
            source.FindProperty("number").CopyToClipboard();
            using var multiple = new SerializedObject(new UnityEngine.Object[] { first, second });
            Undo.IncrementCurrentGroup();
            var destination = multiple.FindProperty("number");
            Assert.That(destination.IsCanPaste(), Is.True);
            destination.PasteFromClipboard();
            Undo.FlushUndoRecordObjects();
            Assert.That(first.number, Is.EqualTo(42));
            Assert.That(second.number, Is.EqualTo(42));
            Undo.PerformUndo();
            Assert.That(first.number, Is.EqualTo(1));
            Assert.That(second.number, Is.EqualTo(1));
            Undo.PerformRedo();
            Assert.That(first.number, Is.EqualTo(42));
            Assert.That(second.number, Is.EqualTo(42));
            Undo.IncrementCurrentGroup();
        }

        [TestCase("data")]
        [TestCase("array")]
        public void NativeStructuredPayloadRoundTripsCompletely(string path)
        {
            first.data.number = 42;
            first.data.text = "copied";
            first.array = new[] { 3, 4, 5 };
            using var source = new SerializedObject(first);
            using var destination = new SerializedObject(second);
            source.FindProperty(path).CopyToClipboard();
            Assert.That(destination.FindProperty(path).IsCanPaste(), Is.True);
            destination.FindProperty(path).PasteFromClipboard();
            if (path == "data")
            {
                Assert.That(second.data.number, Is.EqualTo(42));
                Assert.That(second.data.text, Is.EqualTo("copied"));
            }
            else Assert.That(second.array, Is.EqualTo(new[] { 3, 4, 5 }));
        }

        [TestCase("data")]
        [TestCase("array")]
        public void SameValueMultiObjectStructuredPasteChangesEveryTargetAndSupportsUndo(string path)
        {
            using var source = new SerializedObject(first);
            source.FindProperty("data.number").intValue = 42;
            source.FindProperty("data.text").stringValue = "copied";
            source.FindProperty("array").arraySize = 3;
            for (int i = 0; i < 3; i++) source.FindProperty("array").GetArrayElementAtIndex(i).intValue = i + 3;
            source.FindProperty(path).CopyToClipboard();
            using var multiple = new SerializedObject(new UnityEngine.Object[] { first, second });
            Undo.IncrementCurrentGroup();
            var property = multiple.FindProperty(path);
            Assert.That(property.IsCanPaste(), Is.True);
            property.PasteFromClipboard();
            Undo.FlushUndoRecordObjects();
            foreach (var target in new[] { first, second })
            {
                if (path == "data")
                {
                    Assert.That(target.data.number, Is.EqualTo(42));
                    Assert.That(target.data.text, Is.EqualTo("copied"));
                }
                else Assert.That(target.array, Is.EqualTo(new[] { 3, 4, 5 }));
            }
            Undo.PerformUndo();
            foreach (var target in new[] { first, second })
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
            string folder = "Assets/YLFramework/Tests/Core/Editor/YLClipboardTest_" + Guid.NewGuid().ToString("N");
            string path = folder + "/Clipboard.prefab";
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            BoxCollider collider = null;
            try
            {
                AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Core/Editor", Path.GetFileName(folder));
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
                if (collider) Undo.ClearUndo(collider);
                if (instance) Undo.ClearUndo(instance);
                Undo.IncrementCurrentGroup();
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
