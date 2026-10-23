using UnityEditor;

namespace YLFramework.DataAssets.Editor
{
    [InitializeOnLoad]
    internal static class DataAssetsEditorLifetime
    {
        static DataAssetsEditorLifetime()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                {
                    ScriptableObjectManager.Clear();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += ScriptableObjectManager.Clear;
        }
    }
}
