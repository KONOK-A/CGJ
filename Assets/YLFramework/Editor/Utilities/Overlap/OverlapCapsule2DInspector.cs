using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Overlap;

namespace YLFramework.Editor.Overlap
{
    [CustomEditor(typeof(OverlapCapsule2D))]
    public class OverlapCapsule2DInspector : OverlapBox2DInspector
    {
        private OverlapCapsule2D _overlapCapsule;

        protected override void OnEnable()
        {
            base.OnEnable();
            _overlapCapsule = target as OverlapCapsule2D;
        }


        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            switch (_overlapCapsule.Direction)
            {
                case CapsuleDirection2D.Vertical:
                    if (Size.vector2Value.y < Size.vector2Value.x)
                    {
                        Size.vector2Value = new Vector2(Size.vector2Value.x, Size.vector2Value.x);
                        Size.serializedObject.ApplyModifiedProperties();
                    }

                    break;
                case CapsuleDirection2D.Horizontal:
                    if (Size.vector2Value.x < Size.vector2Value.y)
                    {
                        Size.vector2Value = new Vector2(Size.vector2Value.y, Size.vector2Value.y);
                        Size.serializedObject.ApplyModifiedProperties();
                    }
                    break;
            }


        }


        //protected override bool LimitSize(Vector2 size)
        //{
        //    switch (overlapCapsule.Direction)
        //    {
        //        case CapsuleDirection2D.Vertical:
        //            if (size.y < size.x)
        //                return false;

        //            break;
        //        case CapsuleDirection2D.Horizontal:
        //            if (size.x < size.y)
        //                return false;
        //            break;
        //    }


        //    return base.LimitSize(size);
        //}

    }
}
