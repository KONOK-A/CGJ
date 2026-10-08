// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Tools/AssemblyTools.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections.Generic; 
using System.Reflection;

namespace YLFramework.Core
{

    /// <summary>
    /// 程序集工具
    /// </summary>
    public class AssemblyTools
    {

        private static Dictionary<string, Type> typeCaches = new Dictionary<string, Type>();

        /// <summary>
        /// 根据名称来获取类型
        /// </summary>
        /// <param name="classFullName">类名(含命名空间)</param>
        /// <returns></returns>
        public static Type GetType(string classFullName)
        {  
            if (string.IsNullOrEmpty(classFullName)) return null;

            if (typeCaches.ContainsKey(classFullName) && typeCaches[classFullName] != null) return typeCaches[classFullName];

            typeCaches.Remove(classFullName);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var item in assemblies)
            {
                Type type = item.GetType(classFullName);
                if (type != null) 
                {
                    typeCaches.Add(classFullName, type); 
                    break;
                } 
            }

            if (typeCaches.ContainsKey(classFullName))
                return typeCaches[classFullName];


            return null;
        }

    }

}

