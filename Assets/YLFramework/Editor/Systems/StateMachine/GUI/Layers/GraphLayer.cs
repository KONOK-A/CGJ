using YLFramework.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class GraphLayer
    {

        #region 字段

        protected Rect position;

        #endregion

        #region 属性

        public EditorWindow EditorWindow { get; private set; }


        #endregion


        #region 方法

        public GraphLayer(EditorWindow editorWindow) {
            this.EditorWindow = editorWindow;
        }

        public virtual void OnGUI(Rect rect) {
            position = rect;
            UpdateTransformationMatrix();
        }

        public virtual void ProcessEvents() {

        }

        public virtual void Update() { }

        private void UpdateTransformationMatrix()
        {
            //this.transormMatrix = Matrix4x4.TRS(position.center + this.Context.DragOffset  , Quaternion.identity,Vector3.one * this.Context.ZoomFactor);
        }

        public virtual void OnLostFocus()
        {
            if (UnityEditor.EditorWindow.mouseOverWindow != null && UnityEditor.EditorWindow.mouseOverWindow.GetType().ToString().Equals("UnityEditor.InspectorWindow"))
                return;
            Context.Instance.ClearSelections();
        }

        #endregion
    }

}
