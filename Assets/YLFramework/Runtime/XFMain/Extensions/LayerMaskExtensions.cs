// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/LayerMaskExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public static class LayerMaskExtensions  
    {
        // Fix编码
        /// <summary>
        /// 打开或关闭某个层级
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layerName">层级名称</param>
        /// <param name="open"></param>
        public static void Set(this ref LayerMask layerMask, string layerName,bool open)
        { 
            layerMask.Set(LayerMask.NameToLayer(layerName), open); 
        }

        /// <summary>
        /// 打开或关闭某个层级
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layer">层级</param>
        /// <param name="open"></param>
        public static void Set(this ref LayerMask layerMask, int layer, bool open)
        {
            if (open)
            {
                // 打开某个层级
                layerMask = layerMask.value | (1 << layer);
            }
            else
            {
                // 关闭某个层级
                layerMask = layerMask.value & ~(1 << layer);
            }

        }


        /// <summary>
        /// 判断某个层级是否打开
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layerName"></param>
        /// <returns></returns>
        public static bool Contains(this ref LayerMask layerMask, string layerName) 
        {

            return layerMask.Contains(LayerMask.NameToLayer(layerName));
        }

        /// <summary>
        /// 判断某个层级是否打开
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static bool Contains(this ref LayerMask layerMask, int layer)
        {
            int v = layerMask & (1 << layer);
            return v != 0;
        }

        /// <summary>
        /// 获取ProjectSetting Physics2D中某一个层级和其他层级的碰撞关系
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layer"></param>
        public static void Physics2DSetting(this ref LayerMask layerMask, int layer) 
        {
             
            List<string> layers = new List<string>();

            // 遍历所有层级
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                // 跳过空的层级
                if (string.IsNullOrEmpty(name)) continue;
                bool ignore = Physics2D.GetIgnoreLayerCollision(layer, i);
                // 跳过不能碰撞的层级
                if (ignore) continue;
                layers.Add(name);
            }

            layerMask = LayerMask.GetMask(layers.ToArray()); 
        }

        /// <summary>
        /// 获取ProjectSetting Physics中某一个层级和其他层级的碰撞关系
        /// </summary>
        /// <param name="layerMask"></param>
        /// <param name="layer"></param>
        public static void PhysicsSetting(this ref LayerMask layerMask, int layer)
        {

            List<string> layers = new List<string>();

            // 遍历所有层级
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                // 跳过空的层级
                if (string.IsNullOrEmpty(name)) continue;
                bool ignore = Physics.GetIgnoreLayerCollision(layer, i);
                // 跳过不能碰撞的层级
                if (ignore) continue;
                layers.Add(name);
            }

            layerMask = LayerMask.GetMask(layers.ToArray());
        }

    }

}

