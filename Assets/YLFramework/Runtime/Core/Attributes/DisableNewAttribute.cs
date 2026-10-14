using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 声明应经正式工厂创建的类型，YL0004 默认对外部 new、目标类型 new() 及确定的类约束 new T 报告 Warning。
    /// 标记类型自身及其词法嵌套实现可构造自身；派生类型或同程序集不是通用豁免。
    /// 不追反射、Activator、object 擦除或无约束泛型工厂，不提供运行时访问控制。
    /// </summary>
    public class DisableNewAttribute : Attribute
    {

        public DisableNewAttribute() { }

        public DisableNewAttribute(string message) { }

    } 
}
