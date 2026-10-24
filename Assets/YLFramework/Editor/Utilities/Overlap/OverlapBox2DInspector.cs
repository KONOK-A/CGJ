using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Overlap;

namespace YLFramework.Editor.Overlap
{
    [CustomEditor(typeof(OverlapBox2D))]
    public class OverlapBox2DInspector : OverlapBase2DInspector
    {

        private OverlapBox2D _box2D;

        protected SerializedProperty Offset;

        protected SerializedProperty Size;


        protected override void OnEnable()
        {
            base.OnEnable();
            _box2D = target as OverlapBox2D;

            Offset = serializedObject.FindProperty("offset");
            Size = serializedObject.FindProperty("size");
        }


        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Size.vector2Value.x <= 0)
            {
                Size.vector2Value = new Vector2(0, Size.vector2Value.y);
                Size.serializedObject.ApplyModifiedProperties();
            }

            if (Size.vector2Value.y <= 0)
            {
                Size.vector2Value = new Vector2(Size.vector2Value.x, 0);
                Size.serializedObject.ApplyModifiedProperties();
            }


        }

        protected virtual void OnSceneGUI()
        {
            Vector3 top = _box2D.transform.localToWorldMatrix.MultiplyPoint(_box2D.Offset + new Vector2(0, _box2D.Size.y / 2));

            float size = HandleUtility.GetHandleSize(top) * 0.1f;
            Handles.color = Color.green;
            Vector3 currentTop = Handles.FreeMoveHandle(top, size, Vector3.one * size, Handles.CubeHandleCap);

            SetTop(currentTop.y - top.y);

            Vector3 down = _box2D.transform.localToWorldMatrix.MultiplyPoint(_box2D.Offset - new Vector2(0, _box2D.Size.y / 2));

            size = HandleUtility.GetHandleSize(down) * 0.1f;
            Handles.color = Color.green;
            Vector3 currentDown = Handles.FreeMoveHandle(down, size, Vector3.one * size, Handles.CubeHandleCap);

            SetDown(currentDown.y - down.y);

            Vector3 left = _box2D.transform.localToWorldMatrix.MultiplyPoint(_box2D.Offset - new Vector2(_box2D.Size.x / 2, 0));

            size = HandleUtility.GetHandleSize(left) * 0.1f;
            Handles.color = Color.green;
            Vector3 currentLeft = Handles.FreeMoveHandle(left, size, Vector3.one * size, Handles.CubeHandleCap);

            SetLeft(currentLeft.x - left.x);


            Vector3 right = _box2D.transform.localToWorldMatrix.MultiplyPoint(_box2D.Offset + new Vector2(_box2D.Size.x / 2, 0));

            size = HandleUtility.GetHandleSize(right) * 0.1f;
            Handles.color = Color.green;
            Vector3 currentRight = Handles.FreeMoveHandle(right, size, Vector3.one * size, Handles.CubeHandleCap);

            SetRight(currentRight.x - right.x);
        }


        public void SetTop(float detalY)
        {

            if (_box2D.transform.lossyScale.y < 0)
            {
                detalY = -detalY;
            }

            Vector2 offsetV = new Vector2(_box2D.Offset.x, _box2D.Offset.y + detalY / 2);
            Vector2 sizeV = new Vector2(_box2D.Size.x, _box2D.Size.y + detalY);

            if (!LimitSize(sizeV))
            {
                return;
            }

            Offset.vector2Value = offsetV;
            Size.vector2Value = sizeV;

            serializedObject.ApplyModifiedProperties();
        }

        public void SetDown(float detalY)
        {
            if (_box2D.transform.lossyScale.y < 0)
            {
                detalY = -detalY;
            }

            Vector2 offsetV = new Vector2(_box2D.Offset.x, _box2D.Offset.y + detalY / 2);
            Vector2 sizeV = new Vector2(_box2D.Size.x, _box2D.Size.y - detalY);

            if (!LimitSize(sizeV))
            {
                return;
            }

            Offset.vector2Value = offsetV;
            Size.vector2Value = sizeV;

            serializedObject.ApplyModifiedProperties();
        }

        public void SetLeft(float detalX)
        {
            if (_box2D.transform.lossyScale.x < 0)
            {
                detalX = -detalX;
            }

            Vector2 offsetV = new Vector2(_box2D.Offset.x + detalX / 2, _box2D.Offset.y);
            Vector2 sizeV = new Vector2(_box2D.Size.x - detalX, _box2D.Size.y);

            if (!LimitSize(sizeV))
            {
                return;
            }

            Offset.vector2Value = offsetV;
            Size.vector2Value = sizeV;

            serializedObject.ApplyModifiedProperties();
        }

        public void SetRight(float detalX)
        {
            if (_box2D.transform.lossyScale.x < 0)
            {
                detalX = -detalX;
            }

            Vector2 offsetV = new Vector2(_box2D.Offset.x + detalX / 2, _box2D.Offset.y);
            Vector2 sizeV = new Vector2(_box2D.Size.x + detalX, _box2D.Size.y);

            if (!LimitSize(sizeV))
            {
                return;
            }

            Offset.vector2Value = offsetV;
            Size.vector2Value = sizeV;

            serializedObject.ApplyModifiedProperties();
        }


        protected virtual bool LimitSize(Vector2 size)
        {

            if (size.x <= 0)
            {
                return false;
            }

            if (size.y <= 0)
            {
                return false;
            }

            return true;
        }


    }

}
