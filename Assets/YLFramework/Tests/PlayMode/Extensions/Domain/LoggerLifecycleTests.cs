using System;
using NUnit.Framework;
using UnityEngine;
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
    }
}
