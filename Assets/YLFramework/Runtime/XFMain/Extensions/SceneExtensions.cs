// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/SceneExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Core.Extensions
{
    // Fix 编码

    public static class SceneExtensions 
    {

        /// <summary>
        /// 查找某个场景中根节点游戏物体
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject FindRootGameObject(this Scene scene,string name)
        {
            GameObject[] objs = scene.GetRootGameObjects();
            foreach (GameObject obj in objs)
            {
                if (obj.name == name) return obj;
            }

            return null;
        }
    }

}

