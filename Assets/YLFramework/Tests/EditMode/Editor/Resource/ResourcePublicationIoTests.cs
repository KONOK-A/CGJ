using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using YLFramework.Editor.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public sealed class ResourcePublicationIoTests
    {
        [Test]
        public void AtomicReplacementRetriesOnlyStableMetadataAndPreservesBlockingAndCancellationFailures()
        {
            var root = Path.GetFullPath("AssetBundles/YLFramework/f3-io-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Assert.That(Directory.Exists(Path.GetDirectoryName(root)), Is.True);
            Assert.That(Directory.Exists(root), Is.False);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "owner.txt"), "B02+F03-20261017-0902");
            var write = typeof(ResourcePublisher).GetMethod("AtomicWrite", BindingFlags.Static | BindingFlags.NonPublic);
            var index = Path.Combine(root, "release-index.json");
            var attempts = new List<object>();
            var unblocked = 0;
            try
            {
                for (var i = 0; i < 128; i++)
                {
                    var bytes = Encoding.UTF8.GetBytes("{\"value\":" + i + "}");
                    try
                    {
                        write.Invoke(null, new object[] { root, "release-index.json", bytes, CancellationToken.None });
                    }
                    catch (TargetInvocationException error)
                    {
                        attempts.Add(new
                        {
                            Step = "unblocked",
                            Iteration = i,
                            Error = error.InnerException.ToString(),
                            error.InnerException.HResult,
                            Attributes = File.GetAttributes(index).ToString(),
                            NextExists = File.Exists(index + ".next")
                        });
                        throw;
                    }
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(index));
                    unblocked++;
                }
                var old = File.ReadAllBytes(index);
                using (var reader = File.Open(index, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var error = Assert.Throws<TargetInvocationException>(() => write.Invoke(null, new object[] { root, "release-index.json", new byte[] { 1, 2, 3 }, CancellationToken.None }));
                    Assert.That(error.InnerException, Is.InstanceOf<IOException>());
                    attempts.Add(new
                    {
                        Step = "blocked-reader",
                        Error = error.InnerException.ToString(),
                        error.InnerException.HResult,
                        Win32LowWord = error.InnerException.HResult & 0xffff,
                        Attributes = File.GetAttributes(index).ToString(),
                        NextExists = File.Exists(index + ".next")
                    });
                    CollectionAssert.AreEqual(old, File.ReadAllBytes(index));
                }
                write.Invoke(null, new object[] { root, "release-index.json", old, CancellationToken.None });
                CollectionAssert.AreEqual(old, File.ReadAllBytes(index));
                var cancel = Assert.Throws<TargetInvocationException>(() => write.Invoke(null, new object[] {
                    root, "release-index.json", new byte[] { 4, 5, 6 }, new CancellationToken(true) }));
                Assert.That(cancel.InnerException, Is.InstanceOf<OperationCanceledException>());
                CollectionAssert.AreEqual(old, File.ReadAllBytes(index));
                Assert.That(File.Exists(index + ".next"), Is.False);

                var foreign = Encoding.UTF8.GetBytes("{\"value\":999}");
                using (var reader = File.Open(index, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var change = Task.Run(() =>
                    {
                        if (!SpinWait.SpinUntil(() => File.Exists(index + ".next"), TimeSpan.FromSeconds(3)))
                        {
                            throw new TimeoutException("No next metadata appeared.");
                        }

                        using var output = File.Open(index, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        output.Write(foreign, 0, foreign.Length);
                        output.Flush(true);
                    });
                    Exception rejected = null;
                    try
                    {
                        write.Invoke(null, new object[] { root, "release-index.json", old, CancellationToken.None });
                    }
                    catch (TargetInvocationException error)
                    {
                        rejected = error.InnerException;
                    }
                    finally
                    {
                        change.GetAwaiter().GetResult();
                    }
                    attempts.Add(new
                    {
                        Step = "changed-from-refused",
                        Error = rejected?.ToString(),
                        HResult = rejected?.HResult
                    });
                    // 竞争写者也可使复查本身报sharing violation；两种情况都必须拒绝，而不是继续提交。
                    if (rejected is IOException io)
                    {
                        Assert.That(io.HResult, Is.EqualTo(unchecked((int)0x80070020u)).Or.EqualTo(unchecked((int)0x80070497u)));
                    }
                    else
                    {
                        Assert.That(rejected, Is.InstanceOf<InvalidOperationException>());
                    }

                    CollectionAssert.AreEqual(foreign, File.ReadAllBytes(index));
                }
                write.Invoke(null, new object[] { root, "release-index.json", foreign, CancellationToken.None });
                using (var reader = File.Open(index, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var cancellation = new CancellationTokenSource())
                {
                    var stop = Task.Run(() =>
                    {
                        if (!SpinWait.SpinUntil(() => File.Exists(index + ".next"), TimeSpan.FromSeconds(3)))
                        {
                            throw new TimeoutException("No next metadata appeared.");
                        }

                        cancellation.Cancel();
                    });
                    Exception rejected = null;
                    try
                    {
                        write.Invoke(null, new object[] { root, "release-index.json", old, cancellation.Token });
                    }
                    catch (TargetInvocationException error)
                    {
                        rejected = error.InnerException;
                    }
                    finally
                    {
                        stop.GetAwaiter().GetResult();
                    }
                    Assert.That(rejected, Is.InstanceOf<OperationCanceledException>());
                    CollectionAssert.AreEqual(foreign, File.ReadAllBytes(index));
                    attempts.Add(new
                    {
                        Step = "late-cancellation-refused",
                        Error = rejected.ToString()
                    });
                }
            }
            finally
            {
                ResourceCostEvidence.Write("publication-io-probe", new
                {
                    Root = root,
                    UnblockedReplacements = unblocked,
                    Observations = attempts,
                    Boundary = "Real Unity Mono File.Replace on the original G: publication volume. The explicit reader has its own HRESULT (32 observed); it is not proof of the owner/cause of the intermittent 1175."
                });
                Assert.That(File.ReadAllText(Path.Combine(root, "owner.txt")), Is.EqualTo("B02+F03-20261017-0902"));
                Directory.Delete(root, true);
            }
        }
    }
}
