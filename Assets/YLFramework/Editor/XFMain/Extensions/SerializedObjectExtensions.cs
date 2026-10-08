// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Extensions/SerializedObjectExtensions.cs
using System; 
using System.Collections.Generic;
using System.Reflection;
using UnityEditor; 

namespace YLFramework.Core.Editor
{

    public static class SerializedObjectExtensions  
    {

        /// <summary>
        /// 通过反射的方式获取到SerializedObject中所有的可以序列化的字段
        /// </summary>
        /// <param name="serializedObject"></param>
        /// <returns></returns>

        public static List<SerializedProperty> GetAllSerializedProperty(this SerializedObject serializedObject) 
        {
            var list = new List<SerializedProperty>();
            var iterator = serializedObject.GetIterator();

            if (iterator.NextVisible(true))
            {
                do
                {
                    list.Add(iterator.Copy());
                }
                while (iterator.NextVisible(false));
            }

            return list;
        }
         
    }

}
