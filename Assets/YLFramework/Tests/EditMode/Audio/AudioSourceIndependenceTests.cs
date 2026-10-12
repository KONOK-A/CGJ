using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;

namespace YLFramework.Tests.Audio
{
    public class AudioSourceIndependenceTests
    {
        [UnityTest]
        public IEnumerator FormalAudioSourceClosureCompilesWithoutXfOrXfabInEditor()
        {
            return CompileSourceClosure(AssemblyBuilderFlags.EditorAssembly);
        }

        [UnityTest]
        public IEnumerator FormalAudioSourceClosureCompilesWithoutXfOrXfabInWindowsDevelopmentProfile()
        {
            return CompileSourceClosure(AssemblyBuilderFlags.DevelopmentBuild);
        }

        private static IEnumerator CompileSourceClosure(AssemblyBuilderFlags flags)
        {
            var runtime = Path.Combine(Application.dataPath, "YLFramework/Runtime");
            // Audio 必须全量收集，避免新增正式组件被固定文件清单漏掉；其他目录仅取真实依赖闭包。
            var directories = new[]
            {
                "Audio", "Action", "Shared/Pool", "Shared/Events",
                "Foundation/Pool/Abstractions", "Foundation/Events/Core", "Foundation/Events/Internal",
                "Resource/Implementations", "Resource/Utilities", "Tools/Table",
                "Tools/ZipTool/ICSharpCode.SharpZipLib"
            };
            var files = new[]
            {
                "YLFrameworkRoot.cs", "Shared/UpdateManager.cs", "Shared/Internal/SingletonSessionState.cs",
                "Foundation/Singleton/ISingleton.cs", "Foundation/Singleton/Internal/SingletonCreator.cs",
                "Foundation/Singleton/Plain/Singleton.cs", "Foundation/Singleton/Plain/SingletonProperty.cs",
                "Foundation/Singleton/Mono/MonoSingleton.cs", "Foundation/Singleton/Mono/MonoSingletonProperty.cs",
                "Foundation/Singleton/Mono/MonoSingletonHost.cs", "Foundation/Singleton/Mono/MonoSingletonPathAttribute.cs",
                "Foundation/Pool/Object/SafeObjectPool.cs",
                "Foundation/Binding/BindableProperty.cs", "Foundation/Binding/PlayerPrefsProperty.cs",
                "Foundation/Binding/PlayerPrefsBoolProperty.cs", "Foundation/Binding/PlayerPrefsFloatProperty.cs",
                "Resource/Framework/IResCreator.cs", "Resource/Framework/ResFactory.cs", "Resource/Framework/ResKey.cs",
                "Resource/Framework/ResTable.cs", "Resource/Framework/ServiceManager.cs",
                "Resource/Framework/Res/IRes.cs", "Resource/Framework/Res/Res.cs",
                "Resource/Framework/ResLoader/IResLoader.cs", "Resource/Framework/ResLoader/ResLoader.cs",
                "Tools/Extensions/Unity/ComponentExtension.cs", "Tools/Extensions/Unity/GameObjectExtension.cs",
                "Tools/Extensions/Unity/ObjectExtension.cs", "Tools/Extensions/Math/VectorExtension.cs",
                "Tools/Extensions/Utilities/LayerMaskUtility.cs"
            };
            var sources = directories.SelectMany(directory => Directory.GetFiles(Path.Combine(runtime, directory),
                    "*.cs", SearchOption.AllDirectories))
                .Concat(files.Select(file => Path.Combine(runtime, file)))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (var source in sources) Assert.That(File.Exists(source), Is.True, source);
            Assert.That(sources.All(source => IsInsideFolder(source, runtime) &&
                !IsInsideFolder(source, Path.Combine(runtime, "XFMain"))), Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(runtime, "Audio"), "*.cs", SearchOption.AllDirectories), Is.Not.Empty);

            var folder = Path.Combine(Path.GetTempPath(), "kilo", "YLFramework.Audio.SourceClosure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var output = Path.Combine(folder, "AudioSourceClosure.dll");
                var builder = new AssemblyBuilder(output, sources)
                {
                    flags = flags,
                    buildTarget = BuildTarget.StandaloneWindows64,
                    buildTargetGroup = BuildTargetGroup.Standalone,
                    referencesOptions = ReferencesOptions.UseEngineModules,
                    additionalDefines = Array.Empty<string>()
                };
                var editor = (flags & AssemblyBuilderFlags.EditorAssembly) != 0;
                Assert.That(builder.defaultDefines.Contains("UNITY_EDITOR"), Is.EqualTo(editor));
                CollectionAssert.DoesNotContain(builder.defaultDefines, "YL_AUDIO_XFAB");
                var contents = EditorApplication.applicationContentsPath;
                var ui = typeof(UnityEngine.UI.Image).Assembly.Location;
                // Root 保留 Odin namespace 导入；只允许此真实第三方 Attributes DLL，不允许任何自有预编译实现。
                var odin = Path.Combine(Application.dataPath, "YLFramework/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll");
                Assert.That(File.Exists(odin), Is.True, odin);
                var references = builder.defaultReferences;
                builder.excludeReferences = references.Where(path => !IsAllowedReference(path, contents, ui, odin, editor)).ToArray();
                builder.additionalReferences = new[] { ui, odin };
                var effective = references.Except(builder.excludeReferences).Concat(builder.additionalReferences).Distinct().ToArray();
                Assert.That(effective.All(path => IsAllowedReference(path, contents, ui, odin, editor)), Is.True);
                Assert.That(effective.Any(path => Path.GetFileName(path) == "UnityEngine.CoreModule.dll"), Is.True);
                Assert.That(effective.Any(path => Path.GetFileName(path) == "mscorlib.dll" || Path.GetFileName(path) == "netstandard.dll"), Is.True);
                if (editor)
                    foreach (var type in new[] { typeof(PlayModeStateChange), typeof(InitializeOnLoadMethodAttribute), typeof(BuildTarget) })
                        Assert.That(effective.Select(Path.GetFullPath).Contains(Path.GetFullPath(type.Assembly.Location), StringComparer.OrdinalIgnoreCase), Is.True, type.FullName);

                CompilerMessage[] messages = null;
                builder.buildFinished += (_, result) => messages = result;
                Assert.That(builder.Build(), Is.True, "AssemblyBuilder could not start; run this fixture without another compilation in progress.");
                while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                Assert.That(messages, Is.Not.Null);
                Assert.That(messages.Where(message => message.type == CompilerMessageType.Error)
                    .Select(message => message.file + ":" + message.line + ": " + message.message), Is.Empty);
                Assert.That(File.Exists(output), Is.True);
                TestContext.WriteLine("Compiled {0} production source files with {1}; references:\n{2}",
                    sources.Length, flags, string.Join("\n", effective));
                foreach (var warning in messages.Where(message => message.type == CompilerMessageType.Warning))
                    TestContext.WriteLine(warning.file + ":" + warning.line + ": " + warning.message);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void ReferenceAllowlistRejectsProjectAndOptionalFrameworkAssemblies()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var contents = EditorApplication.applicationContentsPath;
            var ui = typeof(UnityEngine.UI.Image).Assembly.Location;
            var odin = Path.Combine(Application.dataPath, "YLFramework/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll");
            foreach (var name in new[] { "YLFramework.Runtime.dll", "YLFramework.Foundation.Runtime.dll",
                "YLFramework.Core.Runtime.dll", "YLFramework.Utilities.ZipTool.dll", "YLFramework.Audio.XFAB.dll",
                "XFGameFramework.dll", "XFABManager.dll", "XFFSM.dll", "Assembly-CSharp.dll", "System.Fake.dll", "UnityEngine.Fake.dll" })
                Assert.That(IsAllowedReference(Path.Combine(project, "Library/ScriptAssemblies", name), contents, ui, odin, true), Is.False, name);
            Assert.That(IsAllowedReference(typeof(AudioSourceIndependenceTests).Assembly.Location, contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(typeof(LogAssert).Assembly.Location, contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(typeof(EditorApplication).Assembly.Location, contents, ui, odin, false), Is.False);
            Assert.That(IsAllowedReference(typeof(EditorApplication).Assembly.Location, contents, ui, odin, true), Is.True);
            Assert.That(IsAllowedReference(Path.Combine(contents, "Managed/UnityEngine/UnityEditor.TestRunner.dll"), contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(Path.Combine(contents, "Managed/UnityEngine-Fake/UnityEditor.CoreModule.dll"), contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(Path.Combine(contents, "Managed/UnityEngine/../Untrusted/UnityEditor.CoreModule.dll"), contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(Path.Combine(project, "Library/ScriptAssemblies/UnityEditor.CoreModule.dll"), contents, ui, odin, true), Is.False);
            Assert.That(IsAllowedReference(typeof(UnityEngine.Object).Assembly.Location, contents, ui, odin, false), Is.True);
            Assert.That(IsAllowedReference(ui, contents, ui, odin, false), Is.True);
        }

        private static bool IsAllowedReference(string path, string unityContents, string ui, string odin, bool editor)
        {
            var absolute = Path.GetFullPath(path);
            if (string.Equals(absolute, Path.GetFullPath(ui), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(absolute, Path.GetFullPath(odin), StringComparison.OrdinalIgnoreCase)) return true;
            var name = Path.GetFileName(absolute);
            if ((name == "UnityEngine.dll" || name.StartsWith("UnityEngine.", StringComparison.Ordinal)) && name != "UnityEngine.TestRunner.dll")
                return IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "PlaybackEngines")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(unityContents, "Managed/UnityEngine.dll")), StringComparison.OrdinalIgnoreCase);
            if (editor && (name == "UnityEditor.dll" || name.StartsWith("UnityEditor.", StringComparison.Ordinal)) && name != "UnityEditor.TestRunner.dll")
                // Unity 2022.3 的 Editor 模块与 Engine 模块同放在 Managed/UnityEngine，根 UnityEditor.dll 仅是 facade。
                return IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEditor")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(unityContents, "Managed/UnityEditor.dll")), StringComparison.OrdinalIgnoreCase);
            if (name == "mscorlib.dll" || name == "netstandard.dll" || name == "System.dll" || name.StartsWith("System.", StringComparison.Ordinal))
                return IsInsideFolder(absolute, Path.Combine(unityContents, "NetStandard")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "UnityReferenceAssemblies")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "MonoBleedingEdge/lib/mono"));
            return false;
        }

        private static bool IsInsideFolder(string path, string folder)
        {
            var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
