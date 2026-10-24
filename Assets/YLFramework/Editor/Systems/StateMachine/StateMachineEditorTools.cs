using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    [InitializeOnLoad]
    internal static class StateMachineEditorTools
    {
        static StateMachineEditorTools()
        {
            FsmEditorHooks.Save = controller =>
            {
                if (controller)
                {
                    EditorUtility.SetDirty(controller);
                }
            };
            FsmEditorHooks.RefreshNode = RefreshNode;
            FsmEditorHooks.AddScript = AddScript;
            FsmEditorHooks.RemoveScript = RemoveScript;
            FsmEditorHooks.CloneCreated = (source, clone) => clone.originGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        }

        public static List<MonoScript> GetAllStatesType()
        {
            var scripts = new List<MonoScript>();
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script && IsStateType(script.GetClass()))
                {
                    scripts.Add(script);
                }
            }

            return scripts;
        }

        private static bool IsStateType(Type type) => type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(FsmState).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null;

        private static void AddScript(FsmStateNodeData node, UnityEngine.Object value)
        {
            var script = value as MonoScript;
            if (!script || !IsStateType(script.GetClass()))
            {
                throw new ArgumentException("State script must be a concrete FSMState with a public parameterless constructor.", nameof(value));
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            if (string.IsNullOrEmpty(guid))
            {
                throw new ArgumentException("State script has not been imported.", nameof(value));
            }

            if (node.stateScripts.Exists(info => info.guid == guid))
            {
                throw new InvalidOperationException("State script is already attached.");
            }

            node.stateScripts.Add(new FsmStateScriptInfo { className = script.GetClass().FullName, guid = guid });
        }

        private static void RemoveScript(FsmStateNodeData node, UnityEngine.Object value)
        {
            if (!value)
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            node.stateScripts.RemoveAll(info => info.guid == guid);
        }

        private static void RefreshNode(FsmStateNodeData node, RuntimeFsmController controller)
        {
            foreach (var info in node.stateScripts)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(info.guid));
                // 编译中暂时失效的绑定不删除；运行前验证会报告状态和类型，保留可修复数据。
                if (!script || !IsStateType(script.GetClass()))
                {
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
                string className = script.GetClass().FullName;
                if (info.guid == guid && info.className == className)
                {
                    continue;
                }

                if (controller)
                {
                    Undo.RecordObject(controller, "Refresh FSM Script Binding");
                }

                info.guid = guid;
                info.className = className;
                if (controller)
                {
                    controller.Save();
                }
            }
        }
    }
}
