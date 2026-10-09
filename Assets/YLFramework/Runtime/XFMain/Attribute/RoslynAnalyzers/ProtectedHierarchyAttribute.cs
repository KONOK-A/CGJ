using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fixed 编码

namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记一个类为受保护的层次结构，其字段和属性不能被指定类型（及其子类）修改。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ProtectedHierarchyAttribute : Attribute
    { 

        /// <summary>
        /// 禁止修改此类型实例的类型（及其子类）
        /// </summary>
        public Type[] ForbiddenModifiers { get; }
          
        public ProtectedHierarchyAttribute(params Type[] forbiddenModifiers)
        {
            ForbiddenModifiers = forbiddenModifiers;
        }


        
    }

}

