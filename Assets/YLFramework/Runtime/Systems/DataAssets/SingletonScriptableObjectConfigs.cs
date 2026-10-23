using System;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>保留具体SO子类的显式单例加载用途；LoadAsync返回本类拥有的借用句柄，Release或owner Dispose归还。</summary>
    public class SingletonScriptableObjectConfigs<T> : ScriptableObject where T : SingletonScriptableObjectConfigs<T>
    {
        private static ScriptableObjectAsset<T> _current;
        public static T Instance => _current != null && _current.IsValid ? _current.Asset : null;

        public static ScriptableObjectAsset<T> LoadAsync(ResourceSystem resource, ResourceAddress address)
        {
            if (_current != null && !_current.IsDisposed && (!_current.IsDone || _current.IsValid))
            {
                throw new InvalidOperationException($"Singleton DataAssets '{typeof(T).FullName}' is already loading or loaded. Release it first.");
            }

            _current?.Dispose();
            var next = new ScriptableObjectAsset<T>();
            _current = next;
            try
            {
                next.Start(resource, address, onReleased: () =>
                {
                    if (ReferenceEquals(_current, next))
                    {
                        _current = null;
                    }
                });
            }
            catch
            {
                if (ReferenceEquals(_current, next))
                {
                    _current = null;
                }

                throw;
            }
            return next;
        }

        public static void Release()
        {
            var owned = _current;
            _current = null;
            owned?.Dispose();
        }
    }
}
