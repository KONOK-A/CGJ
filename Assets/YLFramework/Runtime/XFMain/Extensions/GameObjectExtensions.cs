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