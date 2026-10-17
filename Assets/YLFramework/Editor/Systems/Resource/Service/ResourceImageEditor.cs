using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    [CustomEditor(typeof(ResourceImage)), CanEditMultipleObjects]
    internal sealed class ResourceImageEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("target"), new GUIContent("UI Target", "Image 或 RawImage；为空时使用同一 GameObject 的组件。"));
            var source = serializedObject.FindProperty("source");
            EditorGUILayout.PropertyField(source, new GUIContent("Image Source"));
            if (source.hasMultipleDifferentValues || source.enumValueIndex == (int)ResourceImage.ImageSource.Location)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("location"), new GUIContent("URL / Local File"));
            if (source.hasMultipleDifferentValues || source.enumValueIndex != (int)ResourceImage.ImageSource.Location)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectId"), new GUIContent("Project ID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("address"), new GUIContent("Address"));
                if (source.hasMultipleDifferentValues || source.enumValueIndex == (int)ResourceImage.ImageSource.Catalog)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("subAsset"), new GUIContent("Subasset Key", "可选，必须与 Workbench 中登记的子资产身份一致。"));
                if (source.hasMultipleDifferentValues || source.enumValueIndex == (int)ResourceImage.ImageSource.Atlas)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteName"), new GUIContent("Atlas Sprite Name"));
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loadOnEnable"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("setNativeSize"), new GUIContent("Set Native Size", "如布局由 LayoutGroup 控制，通常不启用。"));
            serializedObject.ApplyModifiedProperties();

            if (targets.Length != 1) return;
            var image = (ResourceImage)target;
            var graphic = image.Target ? image.Target : image.GetComponent<Graphic>();
            if (!(graphic is Image) && !(graphic is RawImage))
                EditorGUILayout.HelpBox("请选择 Image 或 RawImage。组件只归还自己的 Lease，不销毁目标上原有的 Texture/Sprite。", MessageType.Warning);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("启动时先显式Initialize ResourceSystem。URL/本地图片无需Catalog；Catalog支持Texture2D/Sprite；Atlas使用明确地址和唯一Sprite名称，仅供Image。停用或销毁归还Lease并销毁自有克隆。", MessageType.Info);
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Request", image.Status.ToString(), EditorStyles.boldLabel);
            if (image.Error != null) EditorGUILayout.HelpBox(image.Error.ToString(), MessageType.Error);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload")) foreach (ResourceImage value in targets) value.Refresh();
                if (GUILayout.Button("Clear")) foreach (ResourceImage value in targets) value.Clear();
            }
            if (image.IsLoading) Repaint();
        }
    }
}
