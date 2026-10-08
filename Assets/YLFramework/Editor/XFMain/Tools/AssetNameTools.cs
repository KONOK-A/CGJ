// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Tools/AssetNameTools.cs
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Core.Editor
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
                return name;

            string tempName = name;
             
            int count = 0;

            while (IsContainName(tempName,forder))
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
                string asset_path = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(asset_path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(asset_path) == name)
                    return true;
            }

            return false;
        }


    }

}
