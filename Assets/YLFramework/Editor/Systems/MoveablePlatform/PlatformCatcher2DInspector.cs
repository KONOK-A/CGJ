using YLFramework.Core.Editor;

using UnityEditor;
using UnityEngine;

namespace YLFramework.Platform2D
{

    [CustomEditor(typeof(PlatformCatcher2D))]
    public class PlatformCatcher2DInspector : UnityEditor.Editor
    {
        private PlatformCatcher2D catcher;

        private Vector3 snap = new Vector3 (0.1f, 0.1f, 0.01f);

        private void OnEnable()
        {
            catcher = target as PlatformCatcher2D;
        }

        private void OnSceneGUI()
        {

            if (catcher.areas == null || catcher.areas.Length == 0)
                return;

            serializedObject.Update();

            for (int i = 0; i < catcher.areas.Length; i++)
            {
                Area item = catcher.areas[i];

                Vector3 from = catcher.transform.localToWorldMatrix.MultiplyPoint(item.from);
                Vector3 to = catcher.transform.localToWorldMatrix.MultiplyPoint(item.to);

                float size = HandleUtility.GetHandleSize(to) * 0.1f;

                Handles.color = Color.green;

                EditorGUI.BeginChangeCheck();

#if UNITY_6000_0_OR_NEWER
                from = Handles.FreeMoveHandle(from, size, snap, Handles.CubeHandleCap);
#else
                from = Handles.FreeMoveHandle(from, Quaternion.identity, size, snap, Handles.CubeHandleCap);
#endif


                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("areas").GetArrayElementAtIndex(i).FindPropertyRelative("from").vector3Value = catcher.transform.worldToLocalMatrix.MultiplyPoint(from);
                    serializedObject.ApplyModifiedProperties();
                }

                EditorGUI.BeginChangeCheck();

#if UNITY_6000_0_OR_NEWER
                to = Handles.FreeMoveHandle(to, size, Vector3.one * 0.1f, Handles.CubeHandleCap);
#else
                to = Handles.FreeMoveHandle(to, Quaternion.identity, size, Vector3.one * 0.1f, Handles.CubeHandleCap);
#endif

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.FindProperty("areas").GetArrayElementAtIndex(i).FindPropertyRelative("to").vector3Value = catcher.transform.worldToLocalMatrix.MultiplyPoint(to);
                    serializedObject.ApplyModifiedProperties();
                }

            }


        }

    }
}
