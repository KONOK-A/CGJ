using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers 
{

    // Fixed 编码

    /// <summary>
    /// 方法在指定类型、本身基类或实现接口的层级中被直接调用时，YL0003 默认报告 Warning。
    /// 支持开放泛型类型族和精确闭合泛型；多标记原因合并为同一调用点的一条诊断。
    /// 仅当前词法类型参与匹配，不把外层声明归给普通嵌套 helper；null 类型/名单不匹配。
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class WarnInClassesAttribute : Attribute
    {

        public WarnInClassesAttribute(Type type,string message) { }

        public WarnInClassesAttribute(Type[] type, string message) { }

    }
}
