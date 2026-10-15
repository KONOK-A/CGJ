using System;
using UnityEngine.SceneManagement;

namespace YLFramework.Services.Resource
{
    /// <summary>本次场景的 caller 身份；Dispose 不卸载场景，也不释放 SceneRecord 的 Bundle 图。</summary>
    public sealed class SceneLease : IDisposable
    {
        private SceneRecord record;
        public bool IsDisposed => record == null;
        public bool IsValid => record != null && record.Owner.IsSessionActive && record.IsLoaded;
        public SceneRecord Record
        {
            get
            {
                if (record == null) throw new ObjectDisposedException(nameof(SceneLease));
                record.Owner.CheckThread();
                if (!record.Owner.IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "The Scene lease belongs to an ended session.");
                return record;
            }
        }
        public Scene Scene
        {
            get
            {
                var value = Record;
                if (!value.IsLoaded) throw new ResourceException(ResourceError.SceneUnloaded, "The leased Scene has been unloaded.");
                return value.Scene;
            }
        }
        internal SceneLease(SceneRecord record) => this.record = record;
        public void Dispose()
        {
            if (record == null) return;
            record.Owner.CheckThread();
            record = null;
        }
    }
}
