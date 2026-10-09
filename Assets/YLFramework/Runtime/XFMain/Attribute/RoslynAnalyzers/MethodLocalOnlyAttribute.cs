using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{
    /// <summary>
    /// 标记某个类只能在方法的内部声明变量，不能作为类的成员变量
    /// </summary>
    public class MethodLocalOnlyAttribute : Attribute
    {

        public MethodLocalOnlyAttribute() { }

        public MethodLocalOnlyAttribute(string message) { }

    }
}

