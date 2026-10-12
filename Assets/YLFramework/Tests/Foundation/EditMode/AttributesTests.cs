using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Foundation.Attributes;

namespace YLFramework.Foundation.Tests
{
    public class AttributesTests
    {
        [CustomClassNameGUI("Display")]
        private class Named { }
        private class Derived : Named { }

        private class DrawAttribute : CustomPropertyGUI
        {
            public DrawAttribute() { }
            public DrawAttribute(bool reload) : base(reload) { }
            public override void OnPropertyGUI(SerializedProperty property) { }
        }

        private class Fields
        {
            [Draw(true)] public int value;
            [DisableEdit] public int readOnly;
        }

        [Test]
        public void DisplayAndRefreshMetadataRemainReadableWithExplicitUsage()
        {
            var name = (CustomClassNameGUI)Attribute.GetCustomAttribute(typeof(Named), typeof(CustomClassNameGUI));
            Assert.That(name.GetName(), Is.EqualTo("Display"));
            Assert.That(Attribute.GetCustomAttribute(typeof(Derived), typeof(CustomClassNameGUI)), Is.Null);
            Assert.That(new CustomClassNameGUI(null).GetName(), Is.Null);
            Assert.That(new CustomClassNameGUI("").GetName(), Is.Empty);
            Assert.That(new DrawAttribute().reloadTreeViewOnValueChange, Is.False);
            var draw = (DrawAttribute)Attribute.GetCustomAttribute(typeof(Fields).GetField("value"), typeof(DrawAttribute));
            Assert.That(draw.reloadTreeViewOnValueChange, Is.True);
            draw.reloadTreeViewOnValueChange = false;
            Assert.That(draw.reloadTreeViewOnValueChange, Is.False);
            AssertUsage(typeof(CustomClassNameGUI), AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, false);
            AssertUsage(typeof(CustomPropertyGUI), AttributeTargets.Field, false);
            AssertUsage(typeof(DisableEdit), AttributeTargets.Field | AttributeTargets.Property, true);
            Assert.That(Attribute.GetCustomAttribute(typeof(Fields).GetField("readOnly"), typeof(DisableEdit)), Is.Not.Null);
            Assert.That(typeof(CustomPropertyGUI).GetMethod("OnPropertyGUI").IsAbstract, Is.True);
        }

        private static void AssertUsage(Type type, AttributeTargets targets, bool inherited)
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(type, typeof(AttributeUsageAttribute));
            Assert.That(usage.ValidOn, Is.EqualTo(targets));
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.EqualTo(inherited));
        }

        [UnityTest]
        public IEnumerator MetadataAndDerivedConstructorsCompileInEditorSourceProfile()
        {
            return Compile(AssemblyBuilderFlags.EditorAssembly);
        }

        [UnityTest]
        public IEnumerator MetadataAndDerivedConstructorsCompileWithoutEditorReferences()
        {
            return Compile(AssemblyBuilderFlags.DevelopmentBuild);
        }

        private static IEnumerator Compile(AssemblyBuilderFlags flags)
        {
            var folder = Path.Combine(Path.GetTempPath(), "kilo", "YLFramework.Attributes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string probe = Path.Combine(folder, "Probe.cs");
                File.WriteAllText(probe, @"using YLFramework.Foundation.Attributes;
[CustomClassNameGUI(""Player metadata"")] public class Probe {
    [Draw(reloadTreeViewOnValueChange = true), DisableEdit] public int value;
    public bool Read() { return new DrawAttribute(true).reloadTreeViewOnValueChange && new CustomClassNameGUI(""name"").GetName() == ""name""; }
}
public class DrawAttribute : CustomPropertyGUI {
    public DrawAttribute() : base() { }
    public DrawAttribute(bool reload) : base(reload) { }
#if UNITY_EDITOR
    public override void OnPropertyGUI(UnityEditor.SerializedProperty property) { }
#endif
}");
                string root = Path.Combine(Application.dataPath, "YLFramework/Runtime/Shared/Attributes");
                string output = Path.Combine(folder, "Metadata.dll");
                var builder = new AssemblyBuilder(output, Directory.GetFiles(root, "*.cs").Concat(new[] { probe }).ToArray())
                {
                    flags = flags,
                    buildTarget = BuildTarget.StandaloneWindows64,
                    buildTargetGroup = BuildTargetGroup.Standalone,
                    referencesOptions = ReferencesOptions.UseEngineModules
                };
                bool editor = flags == AssemblyBuilderFlags.EditorAssembly;
                Assert.That(builder.defaultDefines.Contains("UNITY_EDITOR"), Is.EqualTo(editor));
                // 只使用 Unity 安装目录中的参考程序集，禁止借用项目已编译的 Foundation/Editor 实现。
                string contents = Path.GetFullPath(EditorApplication.applicationContentsPath) + Path.DirectorySeparatorChar;
                builder.excludeReferences = builder.defaultReferences.Where(path =>
                    !Path.GetFullPath(path).StartsWith(contents, StringComparison.OrdinalIgnoreCase) ||
                    (!editor && Path.GetFileName(path).StartsWith("UnityEditor", StringComparison.Ordinal))).ToArray();
                CompilerMessage[] messages = null;
                builder.buildFinished += (_, result) => messages = result;
                Assert.That(builder.Build(), Is.True);
                while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                Assert.That(messages, Is.Not.Null);
                Assert.That(messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.message), Is.Empty);
                Assert.That(File.Exists(output), Is.True);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
