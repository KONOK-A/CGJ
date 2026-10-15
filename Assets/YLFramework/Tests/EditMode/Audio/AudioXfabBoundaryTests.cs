using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Services.Audio;

namespace YLFramework.Tests.Audio
{
    public class AudioXfabBoundaryTests
    {
        [Test]
        public void OptionalXfabAdapterHasNoRuntimeReverseReferenceOrDefaultExport()
        {
            var adapter = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Integrations.XFAB.Audio.XfabAudioClipProvider")).FirstOrDefault(t => t != null);
            Assert.NotNull(adapter);
            Assert.AreEqual("YLFramework.Audio.XFAB", adapter.Assembly.GetName().Name);
            Assert.IsTrue(typeof(IAudioClipProvider).IsAssignableFrom(adapter));
            // 可选适配仅单向依赖主体；完整主体输入与二进制闭包另由 FrameworkAssemblyIndependenceTests 守卫。
            Assert.AreEqual("YLFramework.Runtime", typeof(AudioKit).Assembly.GetName().Name);
            Assert.IsFalse(typeof(AudioKit).Assembly.GetReferencedAssemblies().Any(a => a.Name == "YLFramework.Audio.XFAB"));
            var definition = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>("Assets/YLFramework/Integrations/XFAB/Audio/YLFramework.Audio.XFAB.asmdef");
            Assert.NotNull(definition);
            StringAssert.Contains("\"autoReferenced\": false", definition.text);
            var export = System.IO.File.ReadAllText("Assets/YLFramework/Editor/Distribution/ExportUnityPackage.cs");
            StringAssert.DoesNotContain("Assets/YLFramework/Integrations", export);
        }
    }
}
