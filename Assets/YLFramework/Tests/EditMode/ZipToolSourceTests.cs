using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.TestTools;

namespace YLFramework.Tests
{
    public class ZipToolSourceTests
    {
        [UnityTest]
        public IEnumerator CompleteZipSourcesCompileWithOnlyTrustedBclReferences()
        {
            const string root = "Assets/YLFramework/Runtime/Utilities/ZipTool";
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            Assert.That(sources, Is.Not.Empty);
            Assert.That(File.Exists(root + "/YLFramework.Utilities.ZipTool.asmdef"), Is.False);
            var folder = Path.Combine(Path.GetTempPath(), "kilo", "YLZip.SourceClosure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var output = Path.Combine(folder, "ZipSourceClosure.dll");
                var builder = new AssemblyBuilder(output, sources) { flags = AssemblyBuilderFlags.DevelopmentBuild,
                    buildTarget = BuildTarget.StandaloneWindows64, buildTargetGroup = BuildTargetGroup.Standalone };
                var contents = Path.GetFullPath(EditorApplication.applicationContentsPath);
                var roots = new[] { Path.Combine(contents, "MonoBleedingEdge") + Path.DirectorySeparatorChar,
                    Path.Combine(contents, "NetStandard") + Path.DirectorySeparatorChar };
                bool TrustedBcl(string path)
                {
                    var absolute = Path.GetFullPath(path);
                    var name = Path.GetFileName(path);
                    return roots.Any(prefix => absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
                        (name == "mscorlib.dll" || name == "netstandard.dll" || name == "System.dll" ||
                         name.StartsWith("System.", StringComparison.Ordinal));
                }
                builder.excludeReferences = builder.defaultReferences.Where(path => !TrustedBcl(path)).ToArray();
                var references = builder.defaultReferences.Except(builder.excludeReferences).ToArray();
                Assert.That(references, Is.Not.Empty);
                Assert.That(references.All(TrustedBcl), Is.True);
                Assert.That(builder.additionalReferences ?? Array.Empty<string>(), Is.Empty);
                CompilerMessage[] messages = null;
                builder.buildFinished += (_, result) => messages = result;
                Assert.That(builder.Build(), Is.True);
                while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                Assert.That(messages, Is.Not.Null);
                Assert.That(messages.Where(message => message.type == CompilerMessageType.Error).Select(message => message.message), Is.Empty);
                Assert.That(File.Exists(output), Is.True);
                TestContext.WriteLine("Zip source files: {0}; BCL-only references:\n{1}", sources.Length, string.Join("\n", references));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
