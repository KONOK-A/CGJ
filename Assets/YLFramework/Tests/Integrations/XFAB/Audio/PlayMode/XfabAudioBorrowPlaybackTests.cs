#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XFABManager;
using YLFramework.Integrations.XFAB.Audio;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio.XFAB
{
    public class XfabAudioBorrowPlaybackTests
    {
        [UnityTest]
        public IEnumerator SharedXfabAssetAndProducerAreUnaffectedByBorrowReturnCancelOrShutdown()
        {
            var keys = new[] { "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
                "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME" };
            var had = new bool[6];
            var switches = new int[3];
            var volumes = new float[3];
            for (var i = 0; i < 6; i++)
            {
                had[i] = PlayerPrefs.HasKey(keys[i]);
            }

            for (var i = 0; i < 3; i++)
            {
                switches[i] = PlayerPrefs.GetInt(keys[i]);
                volumes[i] = PlayerPrefs.GetFloat(keys[i + 3]);
            }
            var name = "Audio43Xfab-" + Guid.NewGuid().ToString("N");
            var folder = "Assets/YLFramework/Tests/Fixtures/Audio/" + name;
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures/Audio", name);
            var path = folder + "/borrowed.wav";
            var project = ScriptableObject.CreateInstance<XFABProject>();
            project.name = name;
            var manager = XFABProjectManager.Instance;
            Assert.IsFalse(manager.IsContainProject(name));
            var projects = (Dictionary<string, XFABProject>)typeof(XFABProjectManager).GetField("projects_dic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var listener = Object.FindObjectsOfType<AudioListener>().Length == 0 ? new GameObject("XfabBorrow.Listener", typeof(AudioListener)) : null;
            LoadAssetRequest externalOperation = null;
            Action<LoadAssetRequest> externalObserver = null;
            try
            {
                Assert.IsTrue(Directory.Exists(folder));
                const int Samples = 44100;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + Samples * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(44100);
                    writer.Write(88200);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(Samples * 2);
                    writer.Write(new byte[Samples * 2]);
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var bundle = new XFABAssetBundle("audio", name);
                bundle.AddFile(path);
                Assert.IsTrue(project.AddAssetBundle(bundle));
                projects.Add(name, project);
                manager.Projects.Add(project);
                Assert.AreEqual(LoadMode.Assets, AssetBundleManager.GetProfile(name).loadMode, "Do not alter the selected XFAB Profile.");
                var provider = new XfabAudioClipProvider(name);
                Assert.IsFalse(provider.CanCache);
                var externalClip = AssetBundleManager.LoadAsset<AudioClip>(name, "borrowed");
                Assert.IsTrue(externalClip);
                using (var borrowed = provider.Load("borrowed"))
                {
                    Assert.AreSame(externalClip, borrowed.Clip);
                }

                Assert.IsTrue(externalClip);
                AudioKit.Shutdown();
                AudioKit.Settings.IsSoundOn.SetValueWithoutCallback(true);
                AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
                AudioKit.ClipCacheCapacity = 2;
                AudioKit.ClipCacheIdleSeconds = .01;
                var address = new AudioClipAddress(provider, "borrowed");
                var externalCompletions = 0;
                externalOperation = AssetBundleManager.LoadAssetAsync<AudioClip>(name, "borrowed");
                externalObserver = operation =>
                {
                    Assert.IsTrue(string.IsNullOrEmpty(operation.error));
                    Assert.AreSame(externalClip, operation.asset);
                    externalCompletions++;
                };
                externalOperation.AddCompleteEvent(externalObserver);
                var first = AudioKit.PlaySoundAsync(address, AudioSoundOptions.MusicDefaults);
                var second = AudioKit.PlaySoundAsync(address, AudioSoundOptions.MusicDefaults);
                first.Cancel();
                var deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!second.Handle.IsValid && !second.IsDone && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                }

                Assert.IsTrue(second.Handle.IsValid, second.Error?.ToString());
                Assert.AreEqual(1, externalCompletions);
                Assert.AreEqual(AudioPlayEnd.Canceled, first.EndReason);
                second.Cancel();
                AudioKit.ClearClipCache();
                AudioKit.Shutdown();
                Assert.IsTrue(externalClip);
                Assert.AreEqual(AudioDataLoadState.Loaded, externalClip.loadState);
                Assert.AreSame(externalClip, AssetBundleManager.LoadAsset<AudioClip>(name, "borrowed"));
                AudioKit.Settings.IsSoundOn.SetValueWithoutCallback(true);
                var late = AudioKit.PlaySoundAsync(address);
                late.Cancel();
                AudioKit.Shutdown();
                yield return null;
                yield return null;
                Assert.AreEqual(AudioPlayEnd.Canceled, late.EndReason);
                Assert.IsTrue(externalClip);
                Assert.AreSame(externalClip, AssetBundleManager.LoadAsset<AudioClip>(name, "borrowed"));
            }
            finally
            {
                externalOperation?.RemoveCompleteEvent(externalObserver);
                AudioKit.Shutdown();
                projects.Remove(name);
                manager.Projects.Remove(project);
                var singleton = typeof(AssetBundleManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var cache = (IDictionary)typeof(AssetBundleManager).GetField("editor_assets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(singleton);
                cache.Remove(path);
                AssetDatabase.DeleteAsset(folder);
                Object.Destroy(project);
                if (listener)
                {
                    Object.Destroy(listener);
                }

                for (var i = 0; i < 3; i++)
                {
                    if (had[i])
                    {
                        PlayerPrefs.SetInt(keys[i], switches[i]);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(keys[i]);
                    }

                    if (had[i + 3])
                    {
                        PlayerPrefs.SetFloat(keys[i + 3], volumes[i]);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(keys[i + 3]);
                    }
                }
            }
        }
    }
}
#endif
