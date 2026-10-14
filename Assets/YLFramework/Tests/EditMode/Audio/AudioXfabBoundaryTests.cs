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
            // 主体合并保留 Core 的 XFABManager 后端；禁止的是反向依赖可选音频适配器。
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
