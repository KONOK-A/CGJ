using System;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>保留具体SO子类的显式单例加载用途；LoadAsync返回本类拥有的借用句柄，Release或owner Dispose归还。</summary>
    public class SingletonScriptableObjectConfigs<T> : ScriptableObject where T : SingletonScriptableObjectConfigs<T>
    {
        private static ScriptableObjectAsset<T> current;
        public static T Instance => current != null && current.IsValid ? current.Asset : null;

        public static ScriptableObjectAsset<T> LoadAsync(ResourceSystem resource, ResourceAddress address)
        {
            if (current != null && !current.IsDisposed && (!current.IsDone || current.IsValid))
                throw new InvalidOperationException($"Singleton DataAssets '{typeof(T).FullName}' is already loading or loaded. Release it first.");
            current?.Dispose();
            var next = new ScriptableObjectAsset<T>();
            current = next;
            try { next.Start(resource, address, onReleased: () => { if (ReferenceEquals(current, next)) current = null; }); }
            catch
            {
                if (ReferenceEquals(current, next)) current = null;
                throw;
            }
            return next;
        }

        public static void Release()
        {
            var owned = current;
            current = null;
            owned?.Dispose();
        }
    }
}
