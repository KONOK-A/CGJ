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
