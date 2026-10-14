using UnityEditor;

namespace YLFramework.Editor.Resource
{
    /// <summary>Project 资产的工作台入口；退出全局 Importer 标记写入菜单。</summary>
    public static class ResServiceAssetsMenu
    {
        [MenuItem("Assets/YLFramework/Resource Workbench")]
        public static void OpenWorkbench() => ResServiceEditorWindow.OpenResServiceWindow();
        [MenuItem("Assets/YLFramework/Resource Workbench", true)]
        private static bool ValidateWorkbench() => Selection.objects.Length == 1 && Selection.activeObject is ResourceProject;
    }
}
