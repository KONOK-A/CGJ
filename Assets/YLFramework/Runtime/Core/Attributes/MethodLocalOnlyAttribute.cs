using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{
    /// <summary>
    /// 标记只可在方法借用窗口使用的类型，YL0005 默认对显式 field/property 的持有报告 Warning。
    /// 检查数组、已知真实值容器及自有泛型实例值槽，不将签名、delegate/event、phantom 或 static-only wrapper 当持有。
    /// 参数/返回/局部/typeof 不做逃逸分析；仅实际 Core 参数池与 request owner 成员有窄形态豁免。
    /// </summary>
    public class MethodLocalOnlyAttribute : Attribute
    {

        public MethodLocalOnlyAttribute()
        {
        }

        public MethodLocalOnlyAttribute(string message)
        {
        }

    }
}
