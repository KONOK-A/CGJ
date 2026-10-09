using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers 
{

    // Fixed 编码

    /// <summary>
    /// 标记某个方法,当该方法在某个类中调用时显示警告信息!
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class WarnInClassesAttribute : Attribute
    {

        public WarnInClassesAttribute(Type type,string message) { }

        public WarnInClassesAttribute(Type[] type, string message) { }

    }
}

