using UnityEditor;
using UnityEngine;
using YLFramework.Equipment;

namespace YLFramework.Editor.Equipment
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
