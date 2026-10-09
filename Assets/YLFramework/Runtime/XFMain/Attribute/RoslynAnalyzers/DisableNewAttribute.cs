using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记某个类不能通过New创建对象
    /// </summary>
    public class DisableNewAttribute : Attribute
    {

        public DisableNewAttribute() { }

        public DisableNewAttribute(string message) { }

    } 
}

