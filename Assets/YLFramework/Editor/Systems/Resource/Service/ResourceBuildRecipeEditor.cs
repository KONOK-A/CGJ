using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    [CustomEditor(typeof(ResourceBuildRecipe))]
    public sealed class ResourceBuildRecipeEditor : UnityEditor.Editor
    {
        private string status;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("配方不覆盖 Project 的资源定义、压缩或加密。查看/保存不执行工作；密钥由调用方 provider 或明确进程环境来源提供。", MessageType.Info);
            var recipe = (ResourceBuildRecipe)target;
            if (GUILayout.Button("保存所选配方")) Run(() => { ResourceEditorOperations.SaveRecipe(recipe); return "配方已保存；未保存其他资产或场景。"; });
            if (GUILayout.Button("查看有效执行选项（不取 key）")) Run(() => ResourceEditorOperations.ResolveRecipe(recipe).ToString());
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("执行已保存配方")) Run(() =>
                {
                    var input = ResourceEditorOperations.ResolveRecipe(recipe);
                    if (!EditorUtility.DisplayDialog("确认执行资源配方", input + "\n仅使用原 owned 构建/发布服务；不部署、不上传、不签发。", "执行", "取消"))
                        return "已取消，未开始执行。";
                    return Execute(recipe).Summary;
                });
            if (status != null) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        // Inspector 与脚本调用不各自解析窗口字段。
        internal static ResourceEditorBuildReport Execute(ResourceBuildRecipe recipe) => ResourceEditorOperations.ExecuteRecipe(recipe);

        private void Run(Func<string> operation)
        {
            try { status = operation(); }
            catch (Exception error) { status = error.Message; }
        }
    }
}
