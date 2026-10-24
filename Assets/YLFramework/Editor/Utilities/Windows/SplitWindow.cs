using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Utilities
{
    public class SplitWindow : EditorWindow
    {
        const int PanePadding = 1; // 内边距

        [UnityEngine.Serialization.FormerlySerializedAs("horizontal_split_percent")]
        [SerializeField]
        protected float horizontalSplitPercent = 0.3f;

        private Rect _horizontalSplitRect;       // 水平分割的矩形

        private bool _isResizingHorizontal;      // 是不是正在调整 水平的比例

        protected bool ShowBorder = false;

        private bool _isLayout;

        public float HorizontalSplitPercent
        {
            get
            {
                return horizontalSplitPercent;
            }
            set
            {
                horizontalSplitPercent = value;
            }
        }


        private void HorizontalResize()
        {
            _horizontalSplitRect.Set(position.width * horizontalSplitPercent - PanePadding - 1, 0, PanePadding * 2 + 1, position.height);

            if (Event.current != null)
            {

                EditorGUIUtility.AddCursorRect(_horizontalSplitRect, MouseCursor.ResizeHorizontal);

                if (Event.current.type == EventType.MouseDown && _horizontalSplitRect.Contains(Event.current.mousePosition))
                {
                    _isResizingHorizontal = true;
                }

                if (_isResizingHorizontal)
                {
                    horizontalSplitPercent = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.1f, 0.8f);
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    _isResizingHorizontal = false;
                }

                if (_isResizingHorizontal)
                {
                    Repaint();
                }
            }
        }

        protected virtual void OnGUI()
        {
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                if (!_isLayout)
                {
                    return;
                }
            }

            _isLayout = Event.current != null && Event.current.type == EventType.Layout;

            Rect left = new Rect(0, 0, position.width * horizontalSplitPercent - PanePadding, position.height);
            Rect right = new Rect(left.width + _horizontalSplitRect.width, 0, position.width * (1 - horizontalSplitPercent) - PanePadding - 1, position.height);

            HorizontalResize();
            DrawGuiLeft(left);
            DrawGuiRight(right);

            if (ShowBorder)
            {
                GUI.Box(left, string.Empty, "GroupBox");
                GUI.Box(right, string.Empty, "GroupBox");
            }


            Vector2 half = Vector2.up * _horizontalSplitRect.height / 2;

            Color origin = Handles.color;
            Handles.color = _isResizingHorizontal ? Color.green : Color.white;
            Handles.DrawLine(_horizontalSplitRect.center - half, _horizontalSplitRect.center + half);

            Handles.color = origin;
        }

        protected virtual void DrawGuiLeft(Rect rect)
        {
        }

        protected virtual void DrawGuiRight(Rect rect)
        {
        }

    }

}
