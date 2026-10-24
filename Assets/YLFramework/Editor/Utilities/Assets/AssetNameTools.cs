using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Utilities
{
    public class AssetNameTools
    {


        /// <summary>
        /// 获取资源名称
        /// </summary>
        /// <param name="name">预期资源名称</param>
        /// <param name="forder">资源所在目录</param>
        /// <returns></returns>
        public static string GetName(string name, string forder)
        {
            if (!AssetDatabase.IsValidFolder(forder))
            {
                return name;
            }

            string tempName = name;

            int count = 0;

            while (IsContainName(tempName, forder))
            {
                count++;
                tempName = string.Format("{0} ({1})", name, count);
            }

            return tempName;
        }

        private static bool IsContainName(string name, string forder)
        {
            string[] matchs = AssetDatabase.FindAssets(name, new string[] { forder });

            foreach (var item in matchs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                if (System.IO.Path.GetFileNameWithoutExtension(assetPath) == name)
                {
                    return true;
                }
            }

            return false;
        }


    }

}
