using System;
using UnityEngine;

namespace YLFramework.Attributes
{

    /// <summary>
    /// 标记某个字段为不可编辑(字段添加该特性后无法在Inspector面板编辑)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class DisableEdit : PropertyAttribute
    {

    }
}
