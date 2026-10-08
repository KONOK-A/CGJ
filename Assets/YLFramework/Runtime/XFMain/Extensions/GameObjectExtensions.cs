// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/GameObjectExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using UnityEngine;


namespace YLFramework.Core.Extensions
{


    public static class GameObjectExtensions
    {
        /// <summary>
        /// 修改游戏物体及其子节点层级
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="layer"></param>
        public static void SetLayer(this GameObject gameObject, int layer)
        {
            Transform[] transforms = gameObject.GetComponentsInChildren<Transform>();
            foreach (var item in transforms)
            {
                item.gameObject.layer = layer;
            }
        }

        /// <summary>
        /// 修改游戏物体及其子节点层级
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="layerName">层级名称</param>
        public static void SetLayer(this GameObject gameObject, string layerName)
        { 
            SetLayer(gameObject,LayerMask.NameToLayer(layerName));
        }

        /// <summary>
        /// 判断该游戏物体是否已经被销毁
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>   
        public static bool IsDestroy(this GameObject gameObject)
        { 
            return !gameObject;
        }

    }

}