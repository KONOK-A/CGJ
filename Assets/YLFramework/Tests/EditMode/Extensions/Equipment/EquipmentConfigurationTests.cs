using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Events;

namespace YLFramework.Tests.Extensions.Equipment
{
    public class EquipmentConfigurationTests
    {
        private readonly List<ScriptableObject> objects = new();
        private Type rootType, dataType;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            rootType = typeof(EventChannel).Assembly.GetType("YLFramework.EquipmentSystem.EquipmentsConfig");
            dataType = typeof(EventChannel).Assembly.GetType("YLFramework.EquipmentSystem.EquipmentConfigData");
            Assert.That(rootType, Is.Not.Null, "YL equipment definitions have not been migrated.");
            Assert.That(dataType, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (folder != null) AssetDatabase.DeleteAsset(folder);
            foreach (var value in objects) if (value && !EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value);
            objects.Clear();
        }

        private ScriptableObject Create(Type type)
        {
            var value = ScriptableObject.CreateInstance(type);
            objects.Add(value);
            return value;
        }

        private IList Rows(ScriptableObject root) => (IList)rootType.GetProperty("Equipments").GetValue(root);
        private void Id(ScriptableObject row, int id) => dataType.GetField("id").SetValue(row, id);
        private object Find(ScriptableObject root, int id)
        {
            try { return rootType.GetMethod("GetEquipmentConfigData").Invoke(root, new object[] { id }); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        [Test]
        public void EmptyTableCanBeQueriedWithoutLoadingAssets()
        {
            var root = Create(rootType);
            Assert.That(Rows(root), Is.Empty);
            Assert.That(Find(root, 1001), Is.Null);
        }

        [Test]
        public void CacheDetectsEqualLengthReplacementAndChangedId()
        {
            var root = Create(rootType);
            var first = Create(dataType); Id(first, 7); Rows(root).Add(first);
            Assert.That(Find(root, 7), Is.SameAs(first));
            var second = Create(dataType); Id(second, 9); Rows(root)[0] = second;
            Assert.That(Find(root, 7), Is.Null);
            Assert.That(Find(root, 9), Is.SameAs(second));
            Id(second, 11);
            Assert.That(Find(root, 9), Is.Null);
            Assert.That(Find(root, 11), Is.SameAs(second));
        }

        [Test]
        public void DuplicateIdDoesNotPublishAPartialCache()
        {
            var root = Create(rootType);
            var first = Create(dataType); Id(first, 9); Rows(root).Add(first);
            var second = Create(dataType); Id(second, 9); Rows(root).Add(second);
            var error = Assert.Throws<InvalidOperationException>(() => Find(root, 500));
            Assert.That(error.Message, Does.Contain("9"));
            Id(second, 10);
            Assert.That(Find(root, 9), Is.SameAs(first));
            Assert.That(Find(root, 10), Is.SameAs(second));
        }

        [Test]
        public void ReadingSavedTableDoesNotWriteAndReferencesSurviveReimport()
        {
            var parent = "Assets/YLFramework/Tests/Fixtures/Extensions";
            folder = parent + "/EquipmentConfig-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            var root = Create(rootType);
            var row = Create(dataType); Id(row, 1001);
            dataType.GetField("parent").SetValue(row, root);
            Rows(root).Add(row);
            var path = folder + "/Root.asset";
            AssetDatabase.CreateAsset(root, path);
            AssetDatabase.AddObjectToAsset(row, root);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssetIfDirty(root);
            var before = File.ReadAllBytes(path);
            Assert.That(EditorUtility.IsDirty(root), Is.False);
            Assert.That(Rows(root).Count, Is.EqualTo(1));
            Assert.That(Find(root, 1001), Is.SameAs(row));
            Assert.That(EditorUtility.IsDirty(root), Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath(path, rootType) as ScriptableObject;
            var restored = Find(reopened, 1001);
            Assert.That(restored, Is.Not.Null);
            Assert.That(dataType.GetField("parent").GetValue(restored), Is.SameAs(reopened));
        }
    }
}
