using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    [NonParallelizable]
    public class BuildEntryTests
    {
        [Test]
        public async Task ConcurrentEntryIsRejectedBeforeBuildTestOrDeployment()
        {
            if (Environment.GetEnvironmentVariable("YL64_BUILD_LOCK_CHILD") == "1")
                Assert.Fail("Second entry reached SDK tests instead of rejecting the held build lock.");
            var output = Path.Combine(AnalyzerFixture.Root,"Temp/YLFramework.Analyzers");
            Directory.CreateDirectory(output);
            FileStream ownedLock = null;
            try
            {
                try { ownedLock = new FileStream(Path.Combine(output,"entry.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
                catch (IOException) { /* 唯一正式入口执行本测试时已持有合作锁，不接管该句柄。 */ }
                var start = new ProcessStartInfo("pwsh") { WorkingDirectory = AnalyzerFixture.Root, UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
                foreach (var argument in new[]{"-NoLogo","-NoProfile","-NonInteractive","-File",Path.Combine(AnalyzerFixture.Root,"Tools/YLFramework.Analyzers/build.ps1")}) start.ArgumentList.Add(argument);
                start.Environment["YL64_BUILD_LOCK_CHILD"]="1";
                using var process = Process.Start(start);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); Assert.Fail("Second entry did not fail fast under the held build lock."); }
                var text = await stdout + await stderr;
                Assert.That(process.ExitCode, Is.Not.EqualTo(0));
                Assert.That(text, Does.Contain("Another analyzer build/deploy entry is running"));
                Assert.That(text, Does.Not.Contain("SDK="));
            }
            finally { ownedLock?.Dispose(); }
        }
    }
}
