using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Logging;

namespace YLFramework.Tests.Extensions.Domain
{
    public class LoggerLifecycleTests
    {
        private sealed class LaterHandler : ILogHandler
        {
            public void LogException(Exception error, UnityEngine.Object context) { }
            public void LogFormat(LogType type, UnityEngine.Object context, string format, params object[] args) { }
        }

        [Test] public void DefaultIsPassiveAndLaterHandlerKeepsOwnershipOnClose()
        {
            var original = Debug.unityLogger.logHandler;
            Assert.That(LoggerManager.IsEnabled, Is.False);
            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(original));
            Assert.That(GameObject.Find("YLFramework.Logging.LoggerManager"), Is.Null);
            try
            {
                var session = LoggerManager.Enable();
                Assert.That(Debug.unityLogger.logHandler, Is.SameAs(session));
                Assert.That(LoggerManager.Enable(), Is.SameAs(session));
                Assert.That(session.LogFilePath, Is.Null);
                var later = new LaterHandler();
                Debug.unityLogger.logHandler = later;
                LoggerManager.Disable();
                Assert.That(Debug.unityLogger.logHandler, Is.SameAs(later));
                Assert.That(session.IsDisposed, Is.True);
                Assert.That(LoggerManager.IsEnabled, Is.False);
            }
            finally { LoggerManager.Disable(); Debug.unityLogger.logHandler = original; }
        }

        [Test] public void FiveRealDebugKindsReachEventsFileAndPreviousHandler()
        {
            Assert.That(LoggerManager.IsEnabled, Is.False);
            var previous = Debug.unityLogger.logHandler;
            TestContext.WriteLine("Previous handler=" + previous.GetType().FullName + ", assembly=" + previous.GetType().Assembly.GetName().Name);
            if (Environment.GetCommandLineArgs().Contains("-domainRequireLegacyLogger"))
                Assert.That(previous.GetType().FullName, Does.StartWith("XFGameFramework.LoggerSystem."), "共存门必须真正经过原XF handler。");
            var directory = Path.Combine(Path.GetTempPath(), "YLDomainC-RealDebug-" + Guid.NewGuid().ToString("N"));
            try
            {
                var session = LoggerManager.Enable(new LoggerOptions { WriteToFile = true, Directory = directory });
                var kinds = new List<LogType>();
                session.OnLogEvent += (type, message) => kinds.Add(type);
                LogAssert.Expect(LogType.Log, new Regex("YL_DOMAIN_REAL_LOG")); Debug.Log("YL_DOMAIN_REAL_LOG");
                LogAssert.Expect(LogType.Warning, new Regex("YL_DOMAIN_REAL_WARNING")); Debug.LogWarning("YL_DOMAIN_REAL_WARNING");
                LogAssert.Expect(LogType.Error, new Regex("YL_DOMAIN_REAL_ERROR")); Debug.LogError("YL_DOMAIN_REAL_ERROR");
                LogAssert.Expect(LogType.Assert, new Regex("YL_DOMAIN_REAL_ASSERT")); Debug.Assert(false, "YL_DOMAIN_REAL_ASSERT");
                LogAssert.Expect(LogType.Exception, new Regex("YL_DOMAIN_REAL_EXCEPTION")); Debug.LogException(new InvalidOperationException("YL_DOMAIN_REAL_EXCEPTION"));
                Assert.That(kinds, Is.EqualTo(new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Assert, LogType.Exception }));
                var file = session.LogFilePath;
                LoggerManager.Disable();
                Assert.That(Debug.unityLogger.logHandler, Is.SameAs(previous));
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.That(stream.Length, Is.GreaterThan(0));
                var text = File.ReadAllText(file);
                foreach (var marker in new[] { "LOG", "WARNING", "ERROR", "ASSERT", "EXCEPTION" }) Assert.That(text, Does.Contain("YL_DOMAIN_REAL_" + marker));
            }
            finally { LoggerManager.Disable(); Debug.unityLogger.logHandler = previous; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
