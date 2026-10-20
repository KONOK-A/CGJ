using System;
using YLFramework.Services.Resource;

namespace YLFramework.Localization
{
    public sealed class LocalizationConfigHandle : IDisposable
    {
        private ResourceRequest<AssetLease<MultiLanguageConfigs>> request;
        private AssetLease<MultiLanguageConfigs> lease;
        private MultiLanguageConfigs config;
        public MultiLanguageConfigs Config => !IsDisposed && (lease == null || lease.IsValid) ? config : null;
        public bool IsDisposed { get; private set; }
        public bool IsLoading => request != null && !request.IsDone;
        public Exception Error { get; private set; }
        internal LocalizationConfigHandle() { }

        internal void Attach(MultiLanguageConfigs config)
        {
            this.config = config;
            try { LocalizationManager.Register(this); }
            catch { this.config = null; throw; }
        }

        internal void Load(ResourceSystem resources, ResourceAddress address)
        {
            var pending = resources.AcquireAsync<MultiLanguageConfigs>(address);
            if (IsDisposed) { pending.Dispose(); return; }
            request = pending;
            pending.Completed += Complete;
        }

        private void Complete(ResourceRequest<AssetLease<MultiLanguageConfigs>> completed)
        {
            if (ReferenceEquals(request, completed)) request = null;
            try
            {
                if (IsDisposed || completed.IsDisposed) return;
                if (completed.Status != ResourceRequestStatus.Succeeded) { Error = completed.Error; return; }
                lease = completed.TakeResult();
                Attach(lease.Asset);
            }
            catch (Exception error)
            {
                Error = error;
                lease?.Dispose();
                lease = null;
            }
            finally { completed.Dispose(); }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var pending = request; request = null;
            var held = lease; lease = null;
            try
            {
                pending?.Dispose();
                LocalizationManager.Unregister(this);
            }
            finally { config = null; held?.Dispose(); }
        }
    }
}
