using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Logging;
using YLFramework.ReddotSystem;

namespace YLFramework.Tests.Extensions.Domain
{
    public class DomainTwoPlayTests
    {
        private string _directory;
        private System.IDisposable _oldPath;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            LoggerManager.Disable();
            _oldPath?.Dispose();
            _oldPath = null;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
        [UnityTest] public IEnumerator TwoPlaysWithoutDomainReloadDoNotReusePathsWriterOrHandler()
        {
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
                Is.True, "由验收owner预先配置Disable Domain Reload；本用例不改ProjectSettings。");
            yield return new EnterPlayMode(false);
            var original = Debug.unityLogger.logHandler;
            var directory = Path.Combine(Application.temporaryCachePath, "YL.Domain.TwoPlay." + System.Guid.NewGuid().ToString("N"));
            _directory = directory;
            var oldPath = ReddotManager.RegisterPath("Domain/FirstPlay");
            _oldPath = oldPath;
            var session = LoggerManager.Enable(new LoggerOptions { WriteToFile = true, Directory = directory });
            session.LogFormat(LogType.Log, null, "domain-first-play");
            var file = session.LogFilePath;
            yield return new ExitPlayMode();
            Assert.That(LoggerManager.IsEnabled, Is.False);
            Assert.That(session.IsDisposed, Is.True);
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.That(stream.Length, Is.GreaterThan(0));
            yield return new EnterPlayMode(false);
            Assert.That(ReddotManager.ShouldBeDisplay("Domain"), Is.False);
            Assert.That(LoggerManager.IsEnabled, Is.False);
            using (ReddotManager.RegisterPath("Domain/FirstPlay"))
            {
                oldPath.Dispose();
                Assert.That(ReddotManager.ShouldBeDisplay("Domain"), Is.True);
            }
            var secondOriginal = Debug.unityLogger.logHandler;
            var second = LoggerManager.Enable(new LoggerOptions { WriteToFile = true, Directory = directory });
            second.LogFormat(LogType.Log, null, "domain-second-play");
            Assert.That(second.LogFilePath, Is.Not.EqualTo(file));
            LoggerManager.Disable();
            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(secondOriginal));
            yield return new ExitPlayMode();
            Assert.That(session, Is.Not.SameAs(second));
            Assert.That(File.ReadAllText(file), Does.Not.Contain("domain-second-play"));
            Directory.Delete(directory, true);
            // original仅记录第一Play前任，不能在第二Play/退出后强行安装旧引用。
            Assert.That(original, Is.Not.Null);
        }
    }
}
