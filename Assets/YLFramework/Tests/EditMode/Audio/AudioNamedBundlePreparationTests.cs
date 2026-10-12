using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Tests.AudioResourceFixtures
{
    public class AudioNamedBundlePreparationTests
    {
        [Test]
        public void BuildIndependentAudioBundlesForRuntimeOwnershipVerification()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsTrue(string.IsNullOrEmpty(SessionState.GetString("YL.Audio43.BundleRoot", "")), "Do not overwrite an unconsumed fixture.");
            var tempParent = Path.Combine(Path.GetTempPath(), "kilo");
            Assert.IsTrue(Directory.Exists(tempParent));
            var id = Guid.NewGuid().ToString("N");
            var folder = "Assets/YLFramework/Tests/Fixtures/Audio/Bundle-" + id;
            var root = Path.Combine(tempParent, "Audio43-" + id);
            var output = Path.Combine(root, "AssetBundles", "Windows");
            var main = "audio43-" + id; var dependency = main + "-dependency";
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures/Audio", Path.GetFileName(folder));
            try
            {
                Directory.CreateDirectory(output);
                var first = folder + "/first.wav"; var second = folder + "/second.wav";
                WriteWave(first); WriteWave(second);
                var text = folder + "/dependency.txt"; File.WriteAllText(text, "owned Audio43 dependency");
                foreach (var path in new[] { first, second, text }) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var manifest = BuildPipeline.BuildAssetBundles(output, new[]
                {
                    new AssetBundleBuild { assetBundleName = main, assetNames = new[] { first, second } },
                    new AssetBundleBuild { assetBundleName = dependency, assetNames = new[] { text } }
                }, BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
                Assert.NotNull(manifest);
                Assert.IsTrue(File.Exists(Path.Combine(output, main)));
                Assert.IsTrue(File.Exists(Path.Combine(output, dependency)));
                SessionState.SetString("YL.Audio43.BundleRoot", root);
                SessionState.SetString("YL.Audio43.BundleAssets", folder);
                SessionState.SetString("YL.Audio43.BundleName", main);
                SessionState.SetString("YL.Audio43.BundleDependency", dependency);
            }
            catch
            {
                AssetDatabase.DeleteAsset(folder);
                if (Directory.Exists(root)) Directory.Delete(root, true);
                throw;
            }
        }
        private static void WriteWave(string path)
        {
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(path)));
            const int samples = 44100 * 3;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 330 / 44100) * 4000));
            }
        }
    }
}
