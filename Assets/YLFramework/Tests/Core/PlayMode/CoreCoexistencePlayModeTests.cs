using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public class CoreCoexistencePlayModeTests
    {
        [UnityTest]
        public IEnumerator ActualLogWriteStaysInOwnedYLDirectoryAndLeavesOriginalLogsUnchanged()
        {
            var oldDirectory = Path.Combine(Application.temporaryCachePath, "Logs");
            var previousLogs = Snapshot(oldDirectory);
            var directory = Path.Combine(LogUtil.LogFileDirectory, "YLCoreSmoke." + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "probe.txt");
            var instanceField = typeof(LogUtil).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instanceField.GetValue(null);
            var taskField = typeof(LogUtil).GetField("writeToFileTask", BindingFlags.Instance | BindingFlags.NonPublic);
            var writingField = typeof(LogUtil).GetField("writingFile", BindingFlags.Instance | BindingFlags.NonPublic);
            if (previous != null)
            {
                var previousTask = taskField.GetValue(previous) as System.Threading.Tasks.Task;
                Assert.That(previousTask == null || previousTask.IsCompleted, Is.True, "Never redirect an active external log writer.");
                Assert.That((bool)writingField.GetValue(previous), Is.False);
            }
            var isolated = Activator.CreateInstance(typeof(LogUtil), true);
            // Isolate filename/state without invoking the legacy automatic age-based deletion of existing YL logs.
            typeof(LogUtil).GetField("logFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(isolated, path);
            instanceField.SetValue(null, isolated);
            try
            {
                Assert.That(LogUtil.LogFileDirectory.Replace('\\', '/'), Does.EndWith("/YLFramework/Core/Logs"));
                // Debug.Log is observed by the original LoggerSystem. Exercise only YL's file-write chain.
                typeof(LogUtil).GetMethod("ReadyWriteToFile", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "YLCoreSmoke log ownership probe" });
                var deadline = Time.realtimeSinceStartup + 5;
                while (taskField.GetValue(isolated) != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(taskField.GetValue(isolated), Is.Null, "Log writer did not finish in time.");
                Assert.That(File.Exists(path), Is.True, "A real disk write is required, not just a path assertion.");
                Assert.That(File.ReadAllText(path), Does.Contain("YLCoreSmoke log ownership probe"));
                CollectionAssert.AreEquivalent(previousLogs, Snapshot(oldDirectory));
            }
            finally
            {
                var task = taskField.GetValue(isolated) as System.Threading.Tasks.Task;
                if (task != null) task.GetAwaiter().GetResult();
                LogUtil.StopWriteToFile();
                instanceField.SetValue(null, previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            Assert.That(Directory.Exists(directory), Is.False);
        }

        private static Dictionary<string, string> Snapshot(string directory)
        {
            var files = new Dictionary<string, string>();
            if (!Directory.Exists(directory)) return files;
            using (var hash = SHA256.Create())
                foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        files.Add(path, Convert.ToBase64String(hash.ComputeHash(stream)));
            return files;
        }
    }
}
