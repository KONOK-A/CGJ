// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/TypeExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public static class TypeExtensions 
    {
        /// <summary>
        /// 获取委托的显示名称(如果这个委托含有泛型,会把泛型拼成对应的参数)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fullName"></param>
        /// <returns></returns>
        public static string GetDelegateDisplayName(this Type type, bool fullName = true)
        {

            string name = fullName ? type.FullName : type.Name;

            if (name.Contains('`'))
                name = name.Split('`')[0];

            if (type.GenericTypeArguments.Length > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(name);
                builder.Append("(");

                Type[] args = type.GenericTypeArguments;

                for (int i = 0; i < args.Length; i++)
                {
                    Type t = args[i];
                    builder.Append(t.Name);
                    if (i == args.Length - 1)
                    {
                        // 说明是最后一个
                        builder.Append(")");
                    }
                    else 
                    {
                        // 不是最后一个
                        builder.Append(",");
                    }
                }

                name = builder.ToString();
            }
            else
            {
                name = string.Format("{0}()", name);
            }

            // XFEvent`1

            return name;
        }



    }

}

