using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Audio;

namespace YLFramework.Tests.Audio
{
    public class AudioGroupTests
    {
        private AudioTestSettings mSettings;
        private AudioClipLibrary mLibrary;
        private AudioClip mClip;
        private GameObject mRoot;
        private AudioGroupPlayer mPlayer;
        private UnityEngine.Random.State mRandom;

        [SetUp]
        public void SetUp()
        {
            mRandom = UnityEngine.Random.state;
            mSettings = new AudioTestSettings();
            mLibrary = ScriptableObject.CreateInstance<AudioClipLibrary>();
            mClip = AudioClip.Create("group", 44100, 1, 44100, false);
            mLibrary.Groups.Add(new AudioClipGroup { Name = "direct", Entries = new List<AudioClipEntry> { new AudioClipEntry { Clip = mClip } } });
            mRoot = new GameObject("AudioGroupTests");
            mPlayer = mRoot.AddComponent<AudioGroupPlayer>();
            using (var serialized = new SerializedObject(mPlayer))
            {
                serialized.FindProperty("library").objectReferenceValue = mLibrary;
                serialized.FindProperty("groupName").stringValue = "direct";
                serialized.FindProperty("channelName").stringValue = "test-group";
                serialized.FindProperty("category").intValue = (int)AudioCategory.Music;
                serialized.FindProperty("options").FindPropertyRelative("Loop").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(mRoot);
            mSettings?.Dispose();
            UnityEngine.Object.DestroyImmediate(mLibrary);
            UnityEngine.Object.DestroyImmediate(mClip);
            UnityEngine.Random.state = mRandom;
        }

        private sealed class Owner : IDisposable
        {
            public int Releases;
            public Action Releasing;
            public void Dispose() { Releases++; Releasing?.Invoke(); }
        }
        private sealed class Provider : IAudioClipProvider
        {
            public bool CanCache => false;
            public readonly Dictionary<string, Action<AudioClipLease, Exception>> Pending = new Dictionary<string, Action<AudioClipLease, Exception>>();
            public AudioClip Clip;
            public readonly List<Owner> Owners = new List<Owner>();
            public AudioClipLease Load(string key)
            {
                var owner = new Owner(); Owners.Add(owner);
                return new AudioClipLease(Clip, owner);
            }
            public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress) => Pending.Add(key, completed);
            public void Complete(string key, Exception error = null) => Pending[key](error == null ? Load(key) : null, error);
        }
        private Provider Named()
        {
            var provider = new Provider { Clip = mClip };
            mPlayer.SetProvider(provider);
            mLibrary.Groups.Add(new AudioClipGroup { Name = "named", Entries = new List<AudioClipEntry> { new AudioClipEntry { Source = AudioClipSource.ExternalProvider, Key = "a" } } });
            return provider;
        }

        [Test]
        public void FormalGroupAuthoringLivesInTheExistingRuntimeAssembly()
        {
            var assembly = typeof(AudioKit).Assembly;
            Assert.IsNotNull(assembly.GetType("YLFramework.Services.Audio.AudioClipLibrary"), "The formal named selection library is not integrated.");
            Assert.IsNotNull(assembly.GetType("YLFramework.Services.Audio.AudioGroupPlayer"), "The formal group component is not integrated.");
        }

        [Test]
        public void SelectionConsumesExactlyOneRandomDrawAndSnapshotsTheAddress()
        {
            var provider = Named();
            UnityEngine.Random.InitState(812);
            var before = UnityEngine.Random.state;
            Assert.IsTrue(mLibrary.TrySelect("named", out var selected, provider));
            var after = UnityEngine.Random.state;
            UnityEngine.Random.state = before;
            _ = UnityEngine.Random.value;
            Assert.AreEqual(UnityEngine.Random.state, after);
            mLibrary.Groups[1].Entries[0].Key = "changed";
            Assert.AreEqual("a", selected.Address.Key);
            Assert.AreSame(provider, selected.Address.Provider);
            Assert.IsTrue(selected.IsNamed);
        }

        [Test]
        public void WeightedZeroExcludesAndUniformIgnoresStoredWeights()
        {
            var group = mLibrary.Groups[0];
            group.Selection = AudioClipSelectionMode.Weighted;
            group.Entries[0].Weight = 0;
            Assert.IsFalse(mLibrary.TrySelect("direct", out _));
            var provider = new Provider { Clip = mClip };
            group.Entries.Add(new AudioClipEntry { Source = AudioClipSource.ExternalProvider, Key = "audio/selected", Weight = 100 });
            for (var i = 0; i < 20; i++)
            {
                Assert.IsTrue(mLibrary.TrySelect("direct", out var selected, provider));
                Assert.IsTrue(selected.IsNamed);
                Assert.AreSame(provider, selected.Address.Provider);
            }
            group.Entries.RemoveAt(1);
            group.Selection = AudioClipSelectionMode.Uniform;
            group.Entries[0].Weight = float.NaN;
            Assert.IsTrue(mLibrary.TrySelect("direct", out var direct));
            Assert.AreSame(mClip, direct.Clip);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidWeightedGroupDoesNotReplaceOldPlayback(float weight)
        {
            mPlayer.Play();
            var previous = mPlayer.LastHandle;
            var group = mLibrary.Groups[0];
            group.Selection = AudioClipSelectionMode.Weighted;
            group.Entries[0].Weight = weight;
            mPlayer.Play();
            Assert.IsTrue(previous.IsValid);
            Assert.AreEqual(previous, mPlayer.LastHandle);
            Assert.AreEqual(1, mPlayer.Channel.RequestCount);
        }

        [Test]
        public void MissingEmptyDuplicateOrInvalidAddressGroupsDoNotCreateAHost()
        {
            mPlayer.PlayGroup("missing");
            mLibrary.Groups[0].Entries.Clear(); mPlayer.Play();
            mLibrary.Groups[0].Entries.Add(new AudioClipEntry { Source = AudioClipSource.AssetBundle, Key = "resources://wrong", BundleName = "bundle" }); mPlayer.Play();
            mLibrary.Groups[0].Entries[0] = new AudioClipEntry { Clip = mClip };
            mLibrary.Groups.Add(new AudioClipGroup { Name = "direct" }); mPlayer.Play();
            Assert.IsNull(mPlayer.Channel);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void LegacyBundleNeverGuessesProjectIdentityOrCreatesAProvider()
        {
            mLibrary.Groups[0].Entries[0] = new AudioClipEntry { Source = AudioClipSource.AssetBundle, Key = "ambience.wav", BundleName = "ambience" };
            Assert.IsFalse(mLibrary.TrySelect("direct", out _));
            Assert.IsFalse(mLibrary.TrySelect("direct", out _));
            Assert.IsNull(YLFramework.Services.Resource.ResourceSystem.Current);
        }

        [Test]
        public void LoadFailureProtectsOldPlayingHandleAndLatestSelectionWins()
        {
            var provider = Named();
            mPlayer.Play(); var old = mPlayer.LastHandle;
            mPlayer.PlayGroup("named"); var failed = mPlayer.LastRequest;
            provider.Complete("a", new InvalidOperationException("missing group asset"));
            Assert.AreEqual(AudioPlayEnd.Failed, failed.EndReason);
            Assert.IsTrue(old.IsValid);
            mLibrary.Groups[1].Entries[0].Key = "slow";
            mPlayer.PlayGroup("named"); var slow = mPlayer.LastRequest;
            mLibrary.Groups[1].Entries[0].Key = "fast";
            mPlayer.PlayGroup("named"); var fast = mPlayer.LastRequest;
            provider.Complete("fast"); provider.Complete("slow");
            Assert.AreEqual(AudioPlayEnd.Replaced, slow.EndReason);
            Assert.IsTrue(fast.Handle.IsValid);
            Assert.AreEqual(fast.Handle, mPlayer.LastHandle);
            Assert.IsFalse(old.IsValid);
            Assert.AreEqual(1, provider.Owners[1].Releases, "Late slow producer is returned exactly once.");
            mPlayer.Stop();
            Assert.AreEqual(1, provider.Owners[0].Releases);
        }

        [Test]
        public void DisableCancelsPendingAndLateCompletionCannotStartOrRetainResources()
        {
            var provider = Named();
            var starts = 0; mPlayer.Started.AddListener(() => starts++);
            mPlayer.PlayGroup("named"); var request = mPlayer.LastRequest;
            mRoot.SetActive(false);
            provider.Complete("a");
            Assert.IsTrue(request.IsDone);
            Assert.IsFalse(request.Handle.IsValid);
            Assert.AreEqual(0, starts);
            Assert.AreEqual(1, provider.Owners[0].Releases);
            Assert.AreEqual(0, mPlayer.Channel.RequestCount);
        }

        [Test]
        public void SynchronousStartedDisableAndThrowUseTheOriginalLeaseCleanup()
        {
            var provider = Named();
            mPlayer.Started.AddListener(() => mRoot.SetActive(false));
            mPlayer.PlayGroup("named"); var request = mPlayer.LastRequest;
            provider.Complete("a");
            Assert.IsFalse(request.Handle.IsValid);
            Assert.AreEqual(1, provider.Owners[0].Releases);
            mRoot.SetActive(true); mPlayer.Started.RemoveAllListeners();
            mPlayer.Started.AddListener(() => throw new InvalidOperationException("group event"));
            Assert.Throws<InvalidOperationException>(() => mPlayer.Play());
            Assert.AreEqual(0, mPlayer.Channel.RequestCount);
        }

        [Test]
        public void GroupSelectionNameAndControlChannelNameRemainIndependent()
        {
            mPlayer.Play();
            Assert.AreEqual("test-group", mPlayer.Channel.Name);
            Assert.AreEqual(AudioCategory.Music, mPlayer.Channel.Category);
            var background = AudioKit.PlayMusic(mClip);
            mPlayer.Channel.Volume = .2f;
            mPlayer.Stop();
            Assert.IsTrue(background.IsValid);
            Assert.IsFalse(mPlayer.LastHandle.IsValid);
        }

        [Test]
        public void ChannelChangesNotifyAfterApplyingValuesAndDoNotNotifyIdenticalWrites()
        {
            var changed = typeof(AudioChannel).GetEvent("VolumeChanged");
            var muted = typeof(AudioChannel).GetEvent("MutedChanged");
            Assert.IsNotNull(changed, "Core channel observation has not been integrated.");
            Assert.IsNotNull(muted);
            mPlayer.Play();
            var channel = mPlayer.Channel;
            var volumes = 0; var mutes = 0;
            Action<float> onVolume = value => { volumes++; Assert.AreEqual(value, channel.Volume); };
            Action<bool> onMute = value => { mutes++; Assert.AreEqual(value, channel.Muted); };
            changed.AddEventHandler(channel, onVolume); muted.AddEventHandler(channel, onMute);
            channel.Volume = .4f; channel.Volume = .4f;
            channel.Muted = true; channel.Muted = true;
            Assert.AreEqual(1, volumes); Assert.AreEqual(1, mutes);
        }

        [Test]
        public void ChannelNotificationReentryRevokesStaleDeliveryButExceptionsDoNotHideOtherObservers()
        {
            var channel = AudioKit.RegisterChannel("observed");
            var observed = new List<float>();
            channel.VolumeChanged += value => { if (value == .5f) channel.Volume = .25f; };
            channel.VolumeChanged += value => observed.Add(value);
            channel.Volume = .5f;
            CollectionAssert.AreEqual(new[] { .25f }, observed);
            var later = 0;
            channel.MutedChanged += value => throw new InvalidOperationException("observer-failure");
            channel.MutedChanged += value => later++;
            Assert.Throws<AggregateException>(() => channel.Muted = true);
            Assert.IsTrue(channel.Muted); Assert.AreEqual(1, later);
            channel.Dispose();
            var replacement = AudioKit.RegisterChannel("observed");
            replacement.Muted = true; replacement.Volume = .5f;
            Assert.AreEqual(1, later); Assert.AreEqual(1, observed.Count);
        }
    }
}
