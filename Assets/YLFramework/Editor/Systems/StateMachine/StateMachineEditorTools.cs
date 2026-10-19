using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [InitializeOnLoad]
    public static class StateMachineEditorTools
    {
        static StateMachineEditorTools()
        {
            FSMEditorHooks.Save = controller => { if (controller) EditorUtility.SetDirty(controller); };
            FSMEditorHooks.RefreshNode = RefreshNode;
            FSMEditorHooks.AddScript = AddScript;
            FSMEditorHooks.RemoveScript = RemoveScript;
            FSMEditorHooks.CloneCreated = (source, clone) => clone.originGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        }

        public static List<MonoScript> GetAllStatesType()
        {
            var scripts = new List<MonoScript>();
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                if (script && IsStateType(script.GetClass())) scripts.Add(script);
            return scripts;
        }

        private static bool IsStateType(Type type) => type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(FSMState).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null;

        private static void AddScript(FSMStateNodeData node, UnityEngine.Object value)
        {
            var script = value as MonoScript;
            if (!script || !IsStateType(script.GetClass())) throw new ArgumentException("State script must be a concrete FSMState with a public parameterless constructor.", nameof(value));
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("State script has not been imported.", nameof(value));
            if (node.StateScripts.Exists(info => info.guid == guid)) throw new InvalidOperationException("State script is already attached.");
            node.StateScripts.Add(new FSMStateScriptInfo { className = script.GetClass().FullName, guid = guid });
        }

        private static void RemoveScript(FSMStateNodeData node, UnityEngine.Object value)
        {
            if (!value) return;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            node.StateScripts.RemoveAll(info => info.guid == guid);
        }

        private static void RefreshNode(FSMStateNodeData node, RuntimeFSMController controller)
        {
            foreach (var info in node.StateScripts)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(info.guid));
                if (!script && string.IsNullOrEmpty(info.guid) && !string.IsNullOrEmpty(info.className))
                    script = GetAllStatesType().Find(item => item.GetClass().FullName == info.className);
                // 编译中暂时失效的绑定不删除；运行前验证会报告状态和类型，保留可修复数据。
                if (!script || !IsStateType(script.GetClass())) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
                string className = script.GetClass().FullName;
                if (info.guid == guid && info.className == className) continue;
                if (controller) Undo.RecordObject(controller, "Refresh FSM Script Binding");
                info.guid = guid;
                info.className = className;
                if (controller) controller.Save();
            }
        }
    }
}
