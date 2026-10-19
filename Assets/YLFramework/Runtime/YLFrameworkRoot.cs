using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Core;
using System;

namespace YLFramework
{
    // ReSharper disable once InconsistentNaming
    [DefaultExecutionOrder(-20)]
    [MonoSingletonPath("YLFramework")]
    public class YLFrameworkRoot : MonoSingleton<YLFrameworkRoot>
    {
        private YLFrameworkRoot() { }
        [SerializeField] private UIRootContext panelRoot;
        private bool preparingPanelRoot;
        private object preparedPanelRootSession;

        /// <summary>按需取得宿主唯一默认 UI 根。仅 Audio/Action/Module 层级访问不会创建 UI。</summary>
        internal UIRootContext GetPanelRoot()
        {
            if (FindObjectsByType<YLFrameworkRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("存在多个 YLFrameworkRoot；默认 UI 根归属不明确，请保留唯一宿主。");
            if (preparingPanelRoot) throw new InvalidOperationException("默认 UI 根创建事务不能重入。");
            if (!ReferenceEquals(panelRoot, null) && !panelRoot)
                throw new InvalidOperationException("宿主默认 UI 根已销毁，不能自动换根。");
            var execution = SingletonSessionState.ExecutionSession;
            if (panelRoot && ReferenceEquals(preparedPanelRootSession, execution))
            {
                panelRoot.ValidateStructure();
                return panelRoot;
            }
            preparingPanelRoot = true;
            GameObject created = null;
            try
            {
                var context = panelRoot;
                if (!context)
                {
                    var prefab = Resources.Load<GameObject>("YLFramework/Core/Canvas");
                    if (!prefab || !prefab.GetComponent<UIRootContext>())
                        throw new InvalidOperationException("Core Canvas 尚未迁移为 UIRootContext 基础根，请在 Editor 显式执行 UI Panel 资产迁移。");
                    created = Instantiate(prefab, transform, false);
                    context = created.GetComponent<UIRootContext>();
                    panelRoot = context;
                }
                context.ValidateStructure();
                context.GetLayer(0);
                PanelManager.RegisterRoot(context);
                PanelManager.onCreateCanvas?.Invoke(context.Canvas);
                context.ValidateStructure();
                preparedPanelRootSession = execution;
                return context;
            }
            catch
            {
                if (created) { panelRoot = null; Destroy(created); }
                throw;
            }
            finally { preparingPanelRoot = false; }
        }

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
        }
    }
}
