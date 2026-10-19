#if UNITY_EDITOR && YL_DOMAIN_EXCEL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.BuffSystem;
using YLFramework.Excel.Editor;
using YLFramework.Foundation;
using YLFramework.MissionSystem;
using YLFramework.SkillSystem;
using Newtonsoft.Json.Linq;

namespace YLFramework.Domain.Excel.Editor
{
    /// <summary>依赖B组真实Excel候选；普通Editor不反向引用此可选程序集。</summary>
    public static class DomainExcelCommands
    {
        public static ExcelImportPlan PreviewImport(SkillsConfig root, string path)
        {
            Clean(root);
            var plan = ExcelAuthoring.PreviewScriptableObjects(path, root.skills, root, Folder(SkillsConfigController.GetSkillConfigDataPath(root)), "skills", data => data.parent = root, DataType<SkillConfigData>(root.type_script_guid));
            return ValidateRows<SkillBase>(plan, "skill_class_full_name");
        }
        public static ExcelImportPlan PreviewImport(BuffsConfig root, string path)
        {
            Clean(root);
            var folder = BuffsConfigController.GetBuffConfigDataPath(root);
            var plan = ExcelAuthoring.PreviewScriptableObjects(path, root.buffs, root, Folder(folder), "buffs", data => data.parent = root, DataType<BuffConfigData>(root.type_script_guid));
            return ValidateRows<BuffBase>(plan, "buff_class_full_name");
        }
        public static ExcelImportPlan PreviewImport(MissionsConfig root, string path)
        {
            Clean(root);
            var folder = "Assets/YLFrameworkData/Mission/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root));
            var plan = ExcelAuthoring.PreviewScriptableObjects(path, root.missions, root, Folder(folder), "missions", data => data.parent = root, DataType<MissionConfigData>(root.type_script_guid));
            return ValidateRows<MissionBase>(plan, "mission_type_class_fullname", root);
        }

        private static ExcelImportPlan ValidateRows<T>(ExcelImportPlan plan, string classField, MissionsConfig missions = null)
        {
            try
            {
                foreach (var change in plan.Changes.Where(change => change.After != null))
                {
                    // B的initialize先于ImportRow，只能在那里绑定parent；领域校验读取真实导入后的差异。
                    var row = JObject.Parse(change.After.Substring(change.After.IndexOf(':') + 1));
                    RequireType<T>((string)row[classField], int.Parse(change.Key, System.Globalization.CultureInfo.InvariantCulture));
                    if (!missions) continue;
                    foreach (var condition in JArray.Parse((string)row["conditions"] ?? "[]"))
                    {
                        var name = (string)condition["param_name"];
                        if (!missions.param.Exists(parameter => parameter.name == name)) throw new InvalidOperationException($"任务 {change.Key} 的参数不存在: {name}，请先显式导入参数。");
                    }
                }
                return plan;
            }
            catch { plan.Dispose(); throw; }
        }

        // 参数单独工作簿/事务，不以连续两次覆盖冒充多sheet原子导入导出。
        public static void ExportParameters(IEnumerable<MissionParameter> values, string path) => ExcelAuthoring.ListToExcel(values, path, "params");
        public static ExcelImportPlan PreviewParameters(string path, List<MissionParameter> values) => ExcelAuthoring.PreviewList(path, values, "params");
        private static ExcelImportPlan PreviewParameters(MissionsConfig root, string path)
        {
            Clean(root);
            var plan = PreviewParameters(path, root.param);
            try
            {
                ValidateParameterReferences(root, plan);
                return plan;
            }
            catch { plan.Dispose(); throw; }
        }
        private static void ValidateParameterReferences(MissionsConfig root, ExcelImportPlan plan)
        {
                var names = new HashSet<string>(root.param.Select(parameter => parameter.name), StringComparer.Ordinal);
                foreach (var change in plan.Changes)
                {
                    if (change.Kind == ExcelChangeKind.Delete) names.Remove(change.Key);
                    else names.Add(change.Key);
                }
                foreach (var data in root.missions)
                    foreach (var condition in data.conditions)
                        if (!names.Contains(condition.param_name)) throw new InvalidOperationException($"仍有任务引用参数: {condition.param_name}，请用普通作者工具改名/同步条件后再导入。");
        }

        [MenuItem("Assets/YLFramework/Domain Excel/Preview Import Entries")]
        private static void ImportSelected()
        {
            var root = SelectedRoot();
            if (!root) throw new InvalidOperationException("请选择YL Skill/Buff/Mission配表或条目。");
            var path = EditorUtility.OpenFilePanel("预览领域Excel导入", string.Empty, "xlsx");
            if (path.Length == 0) return;
            var plan = root is SkillsConfig skill ? PreviewImport(skill, path) : root is BuffsConfig buff ? PreviewImport(buff, path) : PreviewImport((MissionsConfig)root, path);
            var window = ExcelAuthoringCommands.ShowPreview(plan);
            window.Applied += () => Invalidate(root);
        }
        [MenuItem("Assets/YLFramework/Domain Excel/Export Entries")]
        private static void ExportSelected()
        {
            var root = SelectedRoot();
            if (!root) throw new InvalidOperationException("请选择YL Skill/Buff/Mission配表或条目。");
            var path = SavePath(root.name);
            if (path.Length == 0) return;
            if (root is SkillsConfig skill) ExcelAuthoring.ScriptableObjectToExcel(skill.skills, path, "skills", DataType<SkillConfigData>(skill.type_script_guid));
            else if (root is BuffsConfig buff) ExcelAuthoring.ScriptableObjectToExcel(buff.buffs, path, "buffs", DataType<BuffConfigData>(buff.type_script_guid));
            else if (root is MissionsConfig mission) ExcelAuthoring.ScriptableObjectToExcel(mission.missions, path, "missions", DataType<MissionConfigData>(mission.type_script_guid));
        }
        [MenuItem("Assets/YLFramework/Domain Excel/Preview Import Mission Parameters")]
        private static void ImportParameters()
        {
            var root = SelectedRoot() as MissionsConfig;
            if (!root) throw new InvalidOperationException("请选择YL Mission配表。");
            var path = EditorUtility.OpenFilePanel("预览params工作表", string.Empty, "xlsx");
            if (path.Length == 0) return;
            var window = ScriptableObject.CreateInstance<ParameterPreview>();
            window.Root = root;
            window.Plan = PreviewParameters(root, path);
            window.titleContent = new GUIContent("Mission Parameters Preview");
            window.ShowUtility();
        }
        [MenuItem("Assets/YLFramework/Domain Excel/Export Mission Parameters")]
        private static void ExportMissionParameters()
        {
            var root = SelectedRoot() as MissionsConfig;
            if (!root) throw new InvalidOperationException("请选择YL Mission配表。");
            var path = SavePath(root.name + ".params");
            if (path.Length != 0) ExportParameters(root.param, path);
        }

        private static ScriptableObject SelectedRoot() => Selection.activeObject switch
        {
            SkillsConfig root => root,
            BuffsConfig root => root,
            MissionsConfig root => root,
            SkillConfigData data => data.parent,
            BuffConfigData data => data.parent,
            MissionConfigData data => data.parent,
            _ => null
        };
        private static void Clean(ScriptableObject root)
        {
            if (!root || !EditorUtility.IsPersistent(root) || EditorUtility.IsDirty(root)) throw new InvalidOperationException("领域配表必须是已保存且无未保存改动的资产。");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("退出Play后才能导入领域配表。");
        }
        private static string Folder(string path) => AssetDatabase.IsValidFolder(path) ? path : null;
        private static Type DataType<T>(string guid) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(guid)) return typeof(T);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            var type = script ? script.GetClass() : null;
            if (type == null || type.IsAbstract || !typeof(T).IsAssignableFrom(type)) throw new InvalidOperationException($"领域配表数据类型绑定失效: {guid}");
            return type;
        }
        private static void RequireType<T>(string name, int id)
        {
            var type = string.IsNullOrEmpty(name) ? null : AssemblyTool.GetType(name);
            if (type == null || type.IsAbstract || !typeof(T).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"领域脚本类型无效: ID={id}, class={name}");
        }
        private static string SavePath(string name)
        {
            var path = EditorUtility.SaveFilePanel("导出领域工作簿", string.Empty, name, "xlsx");
            if (path.Length != 0 && File.Exists(path) && !EditorUtility.DisplayDialog("覆盖整个工作簿", "旧sheet及格式将被替换。Mission参数请导出为独立.params.xlsx文件。\n" + path, "覆盖", "取消")) return string.Empty;
            return path;
        }
        private static void Invalidate(ScriptableObject root)
        {
            if (root is SkillsConfig skill) skill.ClearCache();
            else if (root is BuffsConfig buff) buff.ClearCache();
            else if (root is MissionsConfig mission) mission.ClearCache();
        }

        private sealed class ParameterPreview : EditorWindow
        {
            internal MissionsConfig Root;
            internal ExcelImportPlan Plan;
            private Vector2 _scroll;
            private string _error;
            private void OnGUI()
            {
                if (Plan == null) { EditorGUILayout.HelpBox("请关闭并重新预览。", MessageType.Info); return; }
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var error in Plan.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
                foreach (var change in Plan.Changes)
                {
                    EditorGUILayout.LabelField(change.Kind + ": " + change.Key, EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel((change.Before ?? "<new>") + "\n=>\n" + (change.After ?? "<deleted>"), EditorStyles.textArea, GUILayout.MinHeight(60));
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(!Plan.CanCommit || _error != null))
                    if (GUILayout.Button("确认导入参数") && EditorUtility.DisplayDialog("确认参数差异", "仅导入所列参数差异，不导入任务。", "导入", "取消"))
                    {
                        try
                        {
                            // 再查当前任务引用，不能让预览期间新增的条件引用变成悬空参数。
                            Clean(Root);
                            ValidateParameterReferences(Root, Plan);
                            if (Plan.Commit(true) == ExcelCommitResult.Applied)
                            {
                                EditorUtility.SetDirty(Root);
                                AssetDatabase.SaveAssetIfDirty(Root);
                                Root.ClearCache();
                                Close();
                            }
                        }
                        catch (Exception error) { _error = error.Message; }
                    }
                if (GUILayout.Button("取消")) Close();
            }
            private void OnDisable() { Plan?.Dispose(); Plan = null; }
        }
    }
}
#endif
