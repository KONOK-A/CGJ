using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Editor
{
    public class EditorAssetTool
    {
        /// <summary>由 Resource Editor 装配。参数依次为 ProjectId、正式地址、可选的数字 localId；结果只借用。</summary>
        public static Func<string, string, string, Object> PreviewResolver
        {
            get; set;
        }

        public static Texture2D GetTexture2D(string projectName, string assetName)
            => GetTexture2D(projectName, assetName, null);

        public static Texture2D GetTexture2D(string projectId, string address, string subAsset)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(address))
            {
                return null;
            }

            var resolve = PreviewResolver ?? throw new InvalidOperationException("No YL Core Editor preview resolver is installed.");
            var asset = resolve(projectId, address, subAsset);
            if (!asset)
            {
                throw new InvalidOperationException("The formal preview address did not resolve an asset.");
            }

            return asset is Texture2D texture ? texture : AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
        }

        public static GUIContent GetTextureGuiContent(string projectName, string assetName)
        {
            try
            {
                return new GUIContent(GetTexture2D(projectName, assetName) ?? EditorGUIUtility.IconContent("BuildSettings.Broadcom").image);
            }
            catch (ArgumentException error)
            {
                return Unresolved(error);
            }
            catch (InvalidOperationException error)
            {
                return Unresolved(error);
            }
        }

        private static GUIContent Unresolved(Exception error) => new GUIContent(
            EditorGUIUtility.IconContent("BuildSettings.Broadcom").image, "YL resource preview unresolved: " + error.Message);
    }
}
