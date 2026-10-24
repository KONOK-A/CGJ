using System;
using YLFramework.Resource;

namespace YLFramework.Localization
{
    public sealed class LocalizationConfigHandle : IDisposable
    {
        private ResourceRequest<AssetLease<MultiLanguageConfigs>> _request;
        private AssetLease<MultiLanguageConfigs> _lease;
        private MultiLanguageConfigs _config;
        public MultiLanguageConfigs Config => !IsDisposed && (_lease == null || _lease.IsValid) ? _config : null;
        public bool IsDisposed
        {
            get; private set;
        }
        public bool IsLoading => _request != null && !_request.IsDone;
        public Exception Error
        {
            get; private set;
        }
        internal LocalizationConfigHandle()
        {
        }

        internal void Attach(MultiLanguageConfigs config)
        {
            this._config = config;
            try
            {
                LocalizationManager.Register(this);
            }
            catch
            {
                this._config = null;
                throw;
            }
        }

        internal void Load(ResourceSystem resources, ResourceAddress address)
        {
            var pending = resources.AcquireAsync<MultiLanguageConfigs>(address);
            if (IsDisposed)
            {
                pending.Dispose();
                return;
            }
            _request = pending;
            pending.Completed += Complete;
        }

        private void Complete(ResourceRequest<AssetLease<MultiLanguageConfigs>> completed)
        {
            if (ReferenceEquals(_request, completed))
            {
                _request = null;
            }

            try
            {
                if (IsDisposed || completed.IsDisposed)
                {
                    return;
                }

                if (completed.Status != ResourceRequestStatus.Succeeded)
                {
                    Error = completed.Error;
                    return;
                }
                _lease = completed.TakeResult();
                Attach(_lease.Asset);
            }
            catch (Exception error)
            {
                Error = error;
                _lease?.Dispose();
                _lease = null;
            }
            finally
            {
                completed.Dispose();
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            var pending = _request;
            _request = null;
            var held = _lease;
            _lease = null;
            try
            {
                pending?.Dispose();
                LocalizationManager.Unregister(this);
            }
            finally
            {
                _config = null;
                held?.Dispose();
            }
        }
    }
}
