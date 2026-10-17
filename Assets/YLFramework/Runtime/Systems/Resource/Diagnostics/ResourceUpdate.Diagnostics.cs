using System.Linq;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private sealed partial class RemoteJob<T>
        {
            public ResourceUpdateDiagnostics CaptureDiagnostics()
            {
                var transfer = stack.OfType<ResourceTransfer>().FirstOrDefault();
                return transfer == null ? new ResourceUpdateDiagnostics(key == "check" ? "Checking" : "Preparing", waiters.Count,
                    0, null, null, null, false, null) : transfer.CaptureDiagnostics(waiters.Count);
            }
        }
    }

    internal sealed partial class ResourceTransfer
    {
        internal ResourceUpdateDiagnostics CaptureDiagnostics(int waiters)
        {
            var waiting = request == null && !done && clock.Elapsed.TotalSeconds < retryAt;
            return new ResourceUpdateDiagnostics(waiting ? "RetryDelay" : "Downloading", waiters, Progress,
                sink?.Written ?? part?.Length ?? 0, length > 0 ? length : (long?)null,
                System.Math.Max(0, attempt - 1), waiting, clock.Elapsed.TotalMilliseconds);
        }
    }
}
