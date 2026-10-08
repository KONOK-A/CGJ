// Copyright (c) 2021 弦小风. Licensed under the MIT License; see ../LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Listener/ListenerFileChange.cs
// YL adaptation: deletion protection is limited to the two owned default assets and their ancestors.
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
            return (assetPath == "Assets/YLFramework/Runtime/XFMain" ||
                assetPath.StartsWith("Assets/YLFramework/Runtime/XFMain/", StringComparison.Ordinal)) &&
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
