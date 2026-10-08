// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Window/SplitWindow.cs
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Core.Editor
{
    public class SplitWindow : EditorWindow
    { 
        const int padding = 1; // 内边距

        [SerializeField]
        protected float horizontal_split_percent = 0.3f;

        private Rect horizontal_split_rect;       // 水平分割的矩形

        private bool isResizingHorizontal;      // 是不是正在调整 水平的比例

        protected bool showBorder = false;

        private bool isLayout;

        public float HorizontalSplitPercent 
        {
            get 
            {
                return horizontal_split_percent;
            }
            set 
            {
                horizontal_split_percent = value;
            }
        }


        private void HorizontalResize()
        { 
            horizontal_split_rect.Set(position.width * horizontal_split_percent - padding - 1, 0, padding * 2 + 1, position.height);
            
            if (Event.current != null) 
            {

                EditorGUIUtility.AddCursorRect(horizontal_split_rect, MouseCursor.ResizeHorizontal);
                 
                if (Event.current.type == EventType.MouseDown && horizontal_split_rect.Contains(Event.current.mousePosition))
                {
                    isResizingHorizontal = true;
                } 

                if (isResizingHorizontal)
                { 
                    horizontal_split_percent = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.1f, 0.8f);
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    isResizingHorizontal = false;
                }

                if(isResizingHorizontal)
                    Repaint();

            } 
        }
         
        protected virtual void OnGUI()
        { 
            if (Event.current != null && Event.current.type == EventType.Repaint)
            { 
                if (!isLayout)
                {
                    return; 
                } 
            }

            isLayout = Event.current != null && Event.current.type == EventType.Layout;

            Rect left = new Rect(0,0, position.width * horizontal_split_percent - padding  ,position.height);
            Rect right = new Rect(left.width + horizontal_split_rect.width, 0, position.width * (1 - horizontal_split_percent) - padding - 1, position.height);
 
            HorizontalResize(); 
            DrawGUILeft(left);
            DrawGUIRight(right);

            if (showBorder) 
            {             
                GUI.Box(left,string.Empty , "GroupBox"); 
                GUI.Box(right, string.Empty, "GroupBox");
            }
            

            Vector2 half = Vector2.up * horizontal_split_rect.height / 2;

            Color origin = Handles.color;  
            Handles.color = isResizingHorizontal ? Color.green : Color.white;
            Handles.DrawLine(horizontal_split_rect.center - half, horizontal_split_rect.center + half ); 

            Handles.color = origin;
        }
         
        protected virtual void DrawGUILeft(Rect rect) { }

        protected virtual void DrawGUIRight(Rect rect) { }

    }

}
