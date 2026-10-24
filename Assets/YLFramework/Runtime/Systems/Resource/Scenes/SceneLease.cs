using System;
using UnityEngine.SceneManagement;

namespace YLFramework.Resource
{
    /// <summary>本次场景的 caller 身份；Dispose 不卸载场景，也不释放 SceneRecord 的 Bundle 图。</summary>
    public sealed class SceneLease : IDisposable
    {
        private SceneRecord _record;
        public bool IsDisposed => _record == null;
        public bool IsValid => _record != null && _record.Owner.IsSessionActive && _record.IsLoaded;
        public SceneRecord Record
        {
            get
            {
                if (_record == null)
                {
                    throw new ObjectDisposedException(nameof(SceneLease));
                }

                _record.Owner.CheckThread();
                if (!_record.Owner.IsSessionActive)
                {
                    throw new ResourceException(ResourceError.SessionUnavailable, "The Scene lease belongs to an ended session.");
                }

                return _record;
            }
        }
        public Scene Scene
        {
            get
            {
                var value = Record;
                if (!value.IsLoaded)
                {
                    throw new ResourceException(ResourceError.SceneUnloaded, "The leased Scene has been unloaded.");
                }

                return value.Scene;
            }
        }
        internal SceneLease(SceneRecord record) => this._record = record;
        public void Dispose()
        {
            if (_record == null)
            {
                return;
            }

            _record.Owner.CheckThread();
            _record = null;
        }
    }
}
