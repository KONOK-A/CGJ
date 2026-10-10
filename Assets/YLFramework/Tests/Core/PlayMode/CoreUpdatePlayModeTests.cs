using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public class CoreUpdatePlayModeTests
    {
        private string name;
        private GameObject source;
        private AudioClip clip;
        private AudioController controller;
        private float timeScale;
        private CoreUpdateHostScope updateHosts;

        [SetUp]
        public void SetUp()
        {
            timeScale = Time.timeScale;
            updateHosts = new CoreUpdateHostScope();
            name = "YLCoreUpdate." + Guid.NewGuid().ToString("N");
            source = new GameObject(name, typeof(AudioSource));
            clip = AudioClip.Create(name, 44100, 1, 44100, false);
            AudioPlayer.RegisterAudioController(name, source.GetComponent<AudioSource>());
            controller = AudioPlayer.GetAuidoController(name);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            Data.Clear();
            AudioPlayer.RemoveAudioController(name);
            if (source) Object.Destroy(source);
            if (clip) Object.Destroy(clip);
            PlayerPrefs.DeleteKey("YLFramework.Core.Audio.Volume:" + name);
            PlayerPrefs.DeleteKey("YLFramework.Core.Audio.Mute:" + name);
            updateHosts.ReleaseOwnedIdleHosts();
            yield return null;
        }

        private List<AudioData> Data => (List<AudioData>)typeof(AudioController)
            .GetField("AudioDatas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        [UnityTest]
        public IEnumerator AudioUsesUnscaledTimeAndStopKeepsItsDriverUntilRemoveOrSourceLoss()
        {
            Time.timeScale = 0;
            AudioPlayer.Play(name, clip, false);
            yield return null; yield return null;
            Assert.That(Data[0].playTime, Is.GreaterThan(0));
            AudioPlayer.Pause(name, true);
            var paused = Data[0].playTime;
            yield return null; yield return null;
            Assert.That(Data[0].playTime, Is.EqualTo(paused));
            AudioPlayer.Stop(name);
            Assert.That(Data, Is.Empty);
            AudioPlayer.Play(name, clip, false);
            yield return null; yield return null;
            Assert.That(Data[0].playTime, Is.GreaterThan(0), "Stop 不取消持续驱动。");
            Object.Destroy(source);
            yield return null; yield return null;
            Assert.That(Data, Is.Empty);
            AudioPlayer.RemoveAudioController(name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoveFailureStillRevokesUpdateBeforeNextFrame()
        {
            var finishes = 0;
            Data.Add(new AudioData(clip, _ => { finishes++; throw new InvalidOperationException("YLCoreUpdate.StopFailure"); }, true));
            var error = Assert.Throws<TargetInvocationException>(() => typeof(AudioController)
                .GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Object.Destroy(source);
            yield return null;
            yield return null;
            Assert.That(finishes, Is.EqualTo(1), "释放失败后不能由在途/后续更新重复调用结束钩子。");
            Data.Clear();
        }
    }
}
