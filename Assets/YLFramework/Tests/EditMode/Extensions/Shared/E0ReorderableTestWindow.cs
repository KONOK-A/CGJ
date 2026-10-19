using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.Tests.Extensions.Shared
{
    public class E0ReorderableTestWindow : EditorWindow
    {
        public TreeView Tree;
        public int GuiCalls;
        private Action pending;
        private EventType eventType;
        private Exception failure;

        public void Dispatch(EventType type, Action action)
        {
            eventType = type;
            pending = action;
            failure = null;
            SendEvent(new Event { type = type, mousePosition = new Vector2(20, 20), button = 0 });
            if (pending != null) throw new InvalidOperationException("窗口未处理请求的IMGUI事件。");
            if (failure != null) throw new InvalidOperationException("窗口IMGUI操作失败。", failure);
        }

        private void OnGUI()
        {
            GuiCalls++;
            if (pending != null && Event.current.type == eventType)
            {
                var action = pending;
                pending = null;
                try { action(); }
                catch (Exception error) { failure = error; }
                Event.current.Use();
            }
            Tree?.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        private void OnDisable()
        {
            (Tree as IDisposable)?.Dispose();
            Tree = null;
        }
    }
}
