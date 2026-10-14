using System;
using UnityEditor;

namespace YLFramework.Core.Editor
{
    public class ListenerFileChange : AssetModificationProcessor
    {
        public static bool IsProtectedDefaultResource(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            if (assetPath == ListenerInitOnLoad.CanvasPath || assetPath == ListenerInitOnLoad.EventSystemPath ||
                assetPath == ListenerInitOnLoad.ResourceRoot)
                return true;
            return (assetPath == "Assets/YLFramework/Runtime/Core" ||
                assetPath.StartsWith("Assets/YLFramework/Runtime/Core/", StringComparison.Ordinal)) &&
                ListenerInitOnLoad.ResourceRoot.StartsWith(assetPath + "/", StringComparison.Ordinal);
        }

        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            if (!IsProtectedDefaultResource(assetPath))
                return AssetDeleteResult.DidNotDelete;
            EditorUtility.DisplayDialog("YL Core Resource", "Required YL Core default resource cannot be deleted: " + assetPath, "OK");
            return AssetDeleteResult.FailedDelete;
        }
    }
}
