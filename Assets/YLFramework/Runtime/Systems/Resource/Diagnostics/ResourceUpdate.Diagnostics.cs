using System.Linq;

namespace YLFramework.Resource
{
    public sealed partial class ResourceSystem
    {
        private sealed partial class RemoteJob<T>
        {
            public ResourceUpdateDiagnostics CaptureDiagnostics()
            {
                var transfer = _stack.OfType<ResourceTransfer>().FirstOrDefault();
                return transfer == null ? new ResourceUpdateDiagnostics(_key == "check" ? "Checking" : "Preparing", _waiters.Count,
                    0, null, null, null, false, null) : transfer.CaptureDiagnostics(_waiters.Count);
            }
        }
    }

    internal sealed partial class ResourceTransfer
    {
        internal ResourceUpdateDiagnostics CaptureDiagnostics(int waiters)
        {
            var waiting = _request == null && !_isDone && _clock.Elapsed.TotalSeconds < _retryAt;
            return new ResourceUpdateDiagnostics(waiting ? "RetryDelay" : "Downloading", waiters, Progress,
                _sink?.Written ?? _part?.Length ?? 0, _length > 0 ? _length : (long?)null,
                System.Math.Max(0, _attempt - 1), waiting, _clock.Elapsed.TotalMilliseconds);
        }
    }
}
