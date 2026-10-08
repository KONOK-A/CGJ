using Sirenix.OdinInspector;
using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Services.UI;

namespace YLFramework
{
    // ReSharper disable once InconsistentNaming
    [DefaultExecutionOrder(-20)]
    [MonoSingletonPath("YLFramework")]
    public class YLFrameworkRoot : MonoSingleton<YLFrameworkRoot>
    {
        private YLFrameworkRoot() { }

        /// <summary>Gets the shared module hierarchy; execution remains on each module's host.</summary>
        public Transform GetModuleRoot(string moduleName)
        {
            var module = transform.Find(moduleName);
            if (module != null) return module;
            module = new GameObject(moduleName).transform;
            module.SetParent(transform, false);
            return module;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InputSystemBridge.EnsureEventSystemInputModule();
        }
    }
}
