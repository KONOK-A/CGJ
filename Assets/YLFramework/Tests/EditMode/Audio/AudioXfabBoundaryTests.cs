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
            Assert.IsFalse(typeof(AudioKit).Assembly.GetReferencedAssemblies().Any(a => a.Name.Contains("XFAB")));
            var definition = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>("Assets/YLFramework/Integrations/XFAB/Audio/YLFramework.Audio.XFAB.asmdef");
            Assert.NotNull(definition);
            StringAssert.Contains("\"autoReferenced\": false", definition.text);
            var export = System.IO.File.ReadAllText("Assets/YLFramework/Editor/ExportUnityPackage.cs");
            StringAssert.DoesNotContain("Assets/YLFramework/Integrations", export);
        }
    }
}
