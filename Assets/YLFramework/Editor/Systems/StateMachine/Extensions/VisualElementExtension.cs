using YLFramework.Core.Editor;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.StateMachine
{
    public static class VisualElementExtension
    {

        public static Matrix4x4 Matrix(this VisualElement visualElement)
        {
#if UNITY_6000_2_OR_NEWER

            Vector3 value = visualElement.resolvedStyle.scale.value;
            if (visualElement.panel != null )
            {
                var property = visualElement.panel.GetType().GetProperty("isFlat");
                if ((bool)property.GetValue(visualElement.panel))
                {
                    value.z = 1f;
                }
            }

            var m_Axis = visualElement.resolvedStyle.rotate.GetType().GetField("m_Axis", BindingFlags.Instance | BindingFlags.NonPublic);
            var axis = (Vector3)m_Axis.GetValue(visualElement.resolvedStyle.rotate);
            var quaternion = Quaternion.AngleAxis(visualElement.resolvedStyle.rotate.angle.ToDegrees(), axis);
            return Matrix4x4.TRS(visualElement.resolvedStyle.translate, quaternion, value);

#else
            return visualElement.transform.matrix;
#endif
        }


    }

}
