using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Events;

namespace YLFramework.Editor
{
    /// <summary>以 Unity 原生序列化展示事件中心，保持派生字段、多选、Undo 与 Prefab 语义。</summary>
    [CustomEditor(typeof(EventListener), true)]
    [CanEditMultipleObjects]
    public sealed class EventListenerEditor : UnityEditor.Editor
    {
        private const string ExpandedKey = "YLFramework.EventListenerEditor.Expanded";
        private const string TabKey = "YLFramework.EventListenerEditor.Tab";
        private static readonly string[] Tabs = { "鼠标事件", "碰撞事件", "Trigger事件" };
        private static readonly (string Name, GUIContent Label)[][] EventFields =
        {
            new[]
            {
                ("pointerEnter", new GUIContent("指针进入")), ("pointerExit", new GUIContent("指针离开")),
                ("pointerClick", new GUIContent("指针点击")), ("pointerDown", new GUIContent("指针按下")),
                ("pointerUp", new GUIContent("指针抬起")), ("beginDrag", new GUIContent("开始拖拽")),
                ("drag", new GUIContent("拖拽中")), ("endDrag", new GUIContent("结束拖拽"))
            },
            new[]
            {
                ("collisionEnter", new GUIContent("碰撞进入")), ("collisionStay", new GUIContent("碰撞保持")),
                ("collisionExit", new GUIContent("碰撞离开")), ("collisionEnter2D", new GUIContent("碰撞进入2D")),
                ("collisionStay2D", new GUIContent("碰撞保持2D")), ("collisionExit2D", new GUIContent("碰撞离开2D"))
            },
            new[]
            {
                ("triggerEnter", new GUIContent("Trigger进入")), ("triggerStay", new GUIContent("Trigger保持")),
                ("triggerExit", new GUIContent("Trigger离开")), ("triggerEnter2D", new GUIContent("Trigger进入2D")),
                ("triggerStay2D", new GUIContent("Trigger保持2D")), ("triggerExit2D", new GUIContent("Trigger离开2D"))
            }
        };
        private static readonly HashSet<string> EventPropertyPaths = new HashSet<string>(
            EventFields.SelectMany(group => group).Select(entry => entry.Name));
        private bool mExpanded;
        private int mTab;

        private void OnEnable()
        {
            mExpanded = SessionState.GetBool(ExpandedKey, true);
            mTab = Mathf.Clamp(SessionState.GetInt(TabKey, 0), 0, Tabs.Length - 1);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            mExpanded = EditorGUILayout.Foldout(mExpanded, "Unity事件中心", true);
            SessionState.SetBool(ExpandedKey, mExpanded);
            if (mExpanded)
            {
                mTab = GUILayout.Toolbar(mTab, Tabs);
                SessionState.SetInt(TabKey, mTab);
                foreach (var entry in EventFields[mTab])
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(entry.Name), entry.Label, true);
            }
            foreach (var property in AdditionalProperties())
            {
                using (property) EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private IEnumerable<SerializedProperty> AdditionalProperties()
        {
            using (var iterator = serializedObject.GetIterator())
            {
                if (!iterator.NextVisible(true)) yield break;
                do
                {
                    if (iterator.propertyPath != "m_Script" && !EventPropertyPaths.Contains(iterator.propertyPath))
                        yield return iterator.Copy();
                } while (iterator.NextVisible(false));
            }
        }
    }
}
