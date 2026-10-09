using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Fixed 编码

namespace YLFramework.Core.Editor
{
    public static class SerializedPropertyExtensions
    {
        /// <summary>
        /// 把目标属性的值 赋值给当前对象
        /// </summary>
        /// <param name="serializedProperty"></param>
        /// <param name="target"></param>
        public static void SyncFrom(this SerializedProperty serializedProperty, SerializedProperty target) 
        { 
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.Generic: 
                    break;
                case SerializedPropertyType.Integer:
                    serializedProperty.intValue = target.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    serializedProperty.boolValue = target.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    serializedProperty.floatValue = target.floatValue;
                    break;
                case SerializedPropertyType.String:
                    serializedProperty.stringValue = target.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    serializedProperty.colorValue = target.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    serializedProperty.objectReferenceValue = target.objectReferenceValue;
                    break;
                case SerializedPropertyType.LayerMask:
                    serializedProperty.intValue = target.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    serializedProperty.enumValueIndex = target.enumValueIndex;
                    break;
                case SerializedPropertyType.Vector2:
                    serializedProperty.vector2Value = target.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    serializedProperty.vector3Value = target.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    serializedProperty.vector4Value = target.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    serializedProperty.rectValue = target.rectValue;
                    break;
                case SerializedPropertyType.ArraySize:
                    serializedProperty.arraySize = target.arraySize;
                    break;
                case SerializedPropertyType.Character:
                    break;
                case SerializedPropertyType.AnimationCurve:
                    serializedProperty.animationCurveValue = target.animationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    serializedProperty.boundsValue = target.boundsValue;
                    break;
                case SerializedPropertyType.Gradient: 
                    break;
                case SerializedPropertyType.Quaternion:
                    serializedProperty.quaternionValue = target.quaternionValue;
                    break;
                case SerializedPropertyType.ExposedReference:
                    serializedProperty.exposedReferenceValue = target.exposedReferenceValue;
                    break;
                case SerializedPropertyType.FixedBufferSize:
                    //serializedProperty.fixedBufferSize = target.fixedBufferSize;
                    break;
                case SerializedPropertyType.Vector2Int:
                    serializedProperty.vector2IntValue = target.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    serializedProperty.vector3IntValue = target.vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    serializedProperty.rectIntValue = target.rectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    serializedProperty.boundsIntValue = target.boundsIntValue;
                    break;
                //case SerializedPropertyType.ManagedReference:
                //    serializedProperty.managedReferenceValue = target.managedReferenceValue;
                //    break; 
            } 
        }

        /// <summary>
        /// 获取当前属性的值的文本
        /// </summary>
        /// <param name="serializedProperty"></param>
        /// <returns></returns>
        public static string GetValueText(this SerializedProperty serializedProperty)
        {
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.Generic:
                    break;
                case SerializedPropertyType.Integer:
                    return serializedProperty.intValue.ToString();

                case SerializedPropertyType.Boolean:
                    return serializedProperty.boolValue.ToString();

                case SerializedPropertyType.Float:
                    return serializedProperty.floatValue.ToString(); 
                case SerializedPropertyType.String:
                    return serializedProperty.stringValue;

                case SerializedPropertyType.Color:
                    return serializedProperty.colorValue.ToString();
                     
                case SerializedPropertyType.ObjectReference:
                    return serializedProperty.objectReferenceValue.ToString(); 
                case SerializedPropertyType.LayerMask:
                    return serializedProperty.intValue.ToString();

                case SerializedPropertyType.Enum:
                    return serializedProperty.enumValueIndex.ToString();

                case SerializedPropertyType.Vector2:
                    return serializedProperty.vector2Value.ToString();
                    
                case SerializedPropertyType.Vector3:
                    return serializedProperty.vector3Value.ToString();
                     
                case SerializedPropertyType.Vector4:
                    return serializedProperty.vector4Value.ToString();
                    
                case SerializedPropertyType.Rect:
                    return serializedProperty.rectValue.ToString();
                    
                case SerializedPropertyType.ArraySize:
                    return serializedProperty.arraySize.ToString();
                    
                case SerializedPropertyType.Character:
                    break;
                case SerializedPropertyType.AnimationCurve:
                    return serializedProperty.animationCurveValue.ToString();
                    
                case SerializedPropertyType.Bounds:
                    return serializedProperty.boundsValue.ToString();
                     
                case SerializedPropertyType.Gradient:
                    break;
                case SerializedPropertyType.Quaternion:
                    return serializedProperty.quaternionValue.ToString();
                    
                case SerializedPropertyType.ExposedReference:
                    return serializedProperty.exposedReferenceValue.ToString();
                    
                case SerializedPropertyType.FixedBufferSize:
                    //serializedProperty.fixedBufferSize = target.fixedBufferSize;
                    break;
                case SerializedPropertyType.Vector2Int:
                    return serializedProperty.vector2IntValue.ToString();
                    
                case SerializedPropertyType.Vector3Int:
                    return serializedProperty.vector3IntValue.ToString();
                   
                case SerializedPropertyType.RectInt:
                    return serializedProperty.rectIntValue.ToString();
                    
                case SerializedPropertyType.BoundsInt:
                    return serializedProperty.boundsIntValue.ToString();
                   
            }

            return string.Empty;

        }
         
        /// <summary>
        /// 把当前属性的值复制到粘贴板
        /// </summary>
        /// <param name="serializedProperty"></param>
        public static void CopyToClipboard(this SerializedProperty serializedProperty)
        {
            Type type = YLFramework.Core.AssemblyTools.GetType("UnityEditor.Clipboard"); 
            MethodInfo SetSerializedProperty = type.GetMethod("SetSerializedProperty", BindingFlags.Static | BindingFlags.Public);
            SetSerializedProperty.Invoke(null, new object[] { serializedProperty });
        }

        /// <summary>
        /// 把粘贴板的数据赋值给当前属性
        /// </summary>
        /// <param name="serializedProperty"></param>
        public static void PasteFromClipboard(this SerializedProperty serializedProperty)
        {
            Type type = YLFramework.Core.AssemblyTools.GetType("UnityEditor.Clipboard"); 
            MethodInfo GetSerializedProperty = type.GetMethod("GetSerializedProperty", BindingFlags.Static | BindingFlags.Public); 
            GetSerializedProperty.Invoke(null, new object[] { serializedProperty }); 
            serializedProperty.serializedObject.ApplyModifiedProperties(); 
        }

        /// <summary>
        /// 判断粘贴板的内容是否能够粘贴给当前属性
        /// </summary>
        /// <param name="serializedProperty"></param>
        /// <returns></returns>
        public static bool IsCanPaste(this SerializedProperty serializedProperty)
        {
            Type type = YLFramework.Core.AssemblyTools.GetType("UnityEditor.Clipboard"); 
            MethodInfo HasSerializedProperty = type.GetMethod("HasSerializedProperty", BindingFlags.Static | BindingFlags.Public);  
            return (bool)(HasSerializedProperty.Invoke(null, null)); ;
        }

         
        /// <summary>
        /// 查询当前属性的特性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T GetAttribute<T>(this SerializedProperty prop) where T : Attribute
        {
            var attrs = prop.GetAttributes<T>();
            return attrs != null && attrs.Length > 0 ? attrs[0] : null;
        }

        /// <summary>
        /// 查询当前属性的特性集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static T[] GetAttributes<T>(this SerializedProperty prop) where T : Attribute
        {
            var target = prop.serializedObject.targetObject;
            if (target == null) return Array.Empty<T>();

            // 把 "myList.Array.data[0].field" 转成 "myList[0].field"
            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var parts = path.Split('.');

            Type currentType = target.GetType();
            FieldInfo field = null;

            foreach (var part in parts)
            {
                string name = part;
                // array element like list[0]
                var bracket = part.IndexOf('[');
                if (bracket > 0) name = part.Substring(0, bracket);

                field = GetFieldInHierarchy(currentType, name);
                if (field == null) return Array.Empty<T>();

                currentType = field.FieldType;
                // 如果是集合，切换到元素类型
                if (currentType.IsArray) currentType = currentType.GetElementType();
                else if (currentType.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(currentType))
                {
                    var args = currentType.GetGenericArguments();
                    if (args.Length > 0) currentType = args[0];
                }
            }

            return field?.GetCustomAttributes(typeof(T), true).Cast<T>().ToArray() ?? Array.Empty<T>();
        }

        static FieldInfo GetFieldInHierarchy(Type type, string name)
        {
            while (type != null)
            {
                var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null) return fi;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 查询当前属性的所有子属性
        /// </summary>
        /// <param name="property"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public static List<SerializedProperty> GetChildrens(this SerializedProperty property,bool recursive = false)
        {
             
            var list = new List<SerializedProperty>();

            if (!property.hasChildren) 
            {
                // 没有子属性，直接返回空列表
                return list;
            }

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            // Move to first child
            if (!iterator.NextVisible(true))
                return list;

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                SerializedProperty p = iterator.Copy();

                list.Add(p);
                if(recursive && p.hasChildren)
                    list.AddRange(p.GetChildrens(recursive));

                if (!iterator.NextVisible(false))
                    break;
            }

            return list;
             
        }


        /// <summary>
        /// 判断属性是否为脚本属性（即 MonoScript 类型），如果是脚本属性，则通常在 Inspector 中显示为不可编辑的字段，显示 MonoScript 的名称和图标。
        /// </summary>
        /// <returns></returns>
        public static bool IsScript(this SerializedProperty property) 
        {
            if (property == null) return false;
             
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            { 
                if (property.objectReferenceValue) 
                { 
                    // Direct object check
                    if (property.objectReferenceValue is MonoScript && property.name == "m_Script")
                        return true; 
                } 
            }

            return false;
        }

    }

}
