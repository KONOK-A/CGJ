using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.Platform2D
{

    [CustomEditor(typeof(PlatformCatcher2D))]
    public class PlatformCatcher2DInspector : UnityEditor.Editor
    {
        private PlatformCatcher2D _catcher;

        private Vector3 _snap = new Vector3(0.1f, 0.1f, 0.01f);

        private void OnEnable()
        {
            _catcher = target as PlatformCatcher2D;
        }

        private void OnSceneGUI()
        {

            if (_catcher.areas == null || _catcher.areas.Length == 0)
            {
                return;
            }

            serializedObject.Update();

            for (int i = 0; i < _catcher.areas.Length; i++)
            {
                Area item = _catcher.areas[i];

                Vector3 from = _catcher.transform.localToWorldMatrix.MultiplyPoint(item.from);
                Vector3 to = _catcher.transform.localToWorldMatrix.MultiplyPoint(item.to);

                float size = HandleUtility.GetHandleSize(to) * 0.1f;

                Handles.color = Color.green;

                EditorGUI.BeginChangeCheck();

                from = Handles.FreeMoveHandle(from, size, _snap, Handles.CubeHandleCap);


                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("areas").GetArrayElementAtIndex(i).FindPropertyRelative("from").vector3Value = _catcher.transform.worldToLocalMatrix.MultiplyPoint(from);
                    serializedObject.ApplyModifiedProperties();
                }

                EditorGUI.BeginChangeCheck();

                to = Handles.FreeMoveHandle(to, size, Vector3.one * 0.1f, Handles.CubeHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("areas").GetArrayElementAtIndex(i).FindPropertyRelative("to").vector3Value = _catcher.transform.worldToLocalMatrix.MultiplyPoint(to);
                    serializedObject.ApplyModifiedProperties();
                }

            }


        }

    }
}
