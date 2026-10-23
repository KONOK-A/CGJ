using UnityEditor;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    [InitializeOnLoad]
    internal static class EquipmentSessionLifecycle
    {
        static EquipmentSessionLifecycle()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                {
                    Close();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += Close;
        }
        private static void Close()
        {
            try
            {
                EquipmentManager.Shutdown();
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
