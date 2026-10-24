using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    [InitializeOnLoad]
    internal static class PendingFsmStateBindings
    {
        [Serializable]
        private sealed class Binding
        {
            public string assetGuid;
            public string node;
            public string scriptPath;
        }
        [Serializable]
        private sealed class Pending
        {
            public List<Binding> items = new List<Binding>();
        }
        private static string Key => "YLFramework.StateMachine.PendingBindings/" + Application.dataPath;
        private static Pending Load() => JsonUtility.FromJson<Pending>(SessionState.GetString(Key, "{\"items\":[]}"));
        private static void Save(Pending pending) => SessionState.SetString(Key, JsonUtility.ToJson(pending));

        static PendingFsmStateBindings()
        {
            AssemblyReloadEvents.afterAssemblyReload += WaitForImport;
            CompilationPipeline.compilationFinished += _ => WaitForImport();
            WaitForImport();
        }

        public static void Schedule(RuntimeFsmController controller, FsmStateNodeData node, string scriptPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller));
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("Save the FSM asset before creating and attaching a state script.");
            }

            var pending = Load();
            pending.items.Add(new Binding { assetGuid = guid, node = node.name, scriptPath = scriptPath });
            Save(pending);
            WaitForImport();
        }

        private static void WaitForImport()
        {
            if (Load().items.Count == 0)
            {
                return;
            }

            EditorApplication.update -= CompleteWhenReady;
            EditorApplication.update += CompleteWhenReady;
        }

        private static void CompleteWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= CompleteWhenReady;
            var pending = Load();
            for (int i = pending.items.Count - 1; i >= 0; i--)
            {
                var item = pending.items[i];
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeFsmController>(AssetDatabase.GUIDToAssetPath(item.assetGuid));
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(item.scriptPath);
                if (!controller)
                {
                    pending.items.RemoveAt(i);
                    continue;
                }
                var node = controller.GetStateNodeData(item.node);
                if (node == null)
                {
                    Debug.LogWarning($"YL Asset FSM: pending script '{item.scriptPath}' targets removed state '{item.node}'.", controller);
                    pending.items.RemoveAt(i);
                    continue;
                }
                // 编译失败或仍在重载时保留记录；下一次编译完成再试，不逐帧忙轮询。
                var type = script ? script.GetClass() : null;
                if (type == null)
                {
                    continue;
                }

                if (!typeof(FsmState).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"YL Asset FSM: '{item.scriptPath}' must define a concrete FSMState with a public parameterless constructor.", controller);
                    pending.items.RemoveAt(i);
                    continue;
                }
                string guid = AssetDatabase.AssetPathToGUID(item.scriptPath);
                if (!node.stateScripts.Exists(info => info.guid == guid))
                {
                    Undo.RecordObject(controller, "Attach Compiled FSM State Script");
                    node.AddStateScript(script);
                    controller.Save();
                    AssetDatabase.SaveAssetIfDirty(controller);
                }
                pending.items.RemoveAt(i);
                EditorGUIUtility.PingObject(script);
            }
            Save(pending);
        }
    }
}
