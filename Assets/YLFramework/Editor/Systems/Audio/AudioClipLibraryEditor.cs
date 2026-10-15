using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Audio
{
    [CustomEditor(typeof(AudioClipLibrary)), CanEditMultipleObjects]
    public sealed class AudioClipLibraryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AudioInspectorUtility.Script(serializedObject);
            EditorGUILayout.HelpBox("Groups select material, not control channels. Names match exactly (case-sensitive). Uniform ignores Weight; Weighted excludes Weight 0. ExternalProvider requires AudioGroupPlayer.SetProvider in code.", MessageType.Info);
            if (targets.Length > 1)
                EditorGUILayout.HelpBox("Only shared row indices are shown. Add appends independently to each selected library; Remove affects that index where present. Edit unequal rows one library at a time.", MessageType.Info);
            var groups = serializedObject.FindProperty("Groups");
            for (var i = 0; i < groups.arraySize; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, "Group " + i, true);
                if (GUILayout.Button("Remove Group", GUILayout.Width(110f)))
                {
                    RemoveGroup(serializedObject, i);
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
                if (!group.isExpanded) continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(group.FindPropertyRelative("Name"));
                    var selection = group.FindPropertyRelative("Selection");
                    EditorGUILayout.PropertyField(selection);
                    var entries = group.FindPropertyRelative("Entries");
                    for (var j = 0; j < entries.arraySize; j++)
                    {
                        var entry = entries.GetArrayElementAtIndex(j);
                        EditorGUILayout.BeginHorizontal();
                        entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, "Entry " + j, true);
                        if (GUILayout.Button("Remove Entry", GUILayout.Width(110f)))
                        {
                            RemoveEntry(serializedObject, i, j);
                            EditorGUILayout.EndHorizontal();
                            return;
                        }
                        EditorGUILayout.EndHorizontal();
                        if (!entry.isExpanded) continue;
                        using (new EditorGUI.IndentLevelScope())
                        {
                            var source = entry.FindPropertyRelative("Source");
                            EditorGUILayout.PropertyField(source);
                            if (AudioInspectorUtility.ShowWhen(source, (int)AudioClipSource.Clip))
                                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Clip"));
                            if (source.hasMultipleDifferentValues || source.intValue != (int)AudioClipSource.Clip)
                                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Key"));
                            if (AudioInspectorUtility.ShowWhen(source, (int)AudioClipSource.Resource))
                                EditorGUILayout.PropertyField(entry.FindPropertyRelative("ProjectId"));
                            if (source.hasMultipleDifferentValues || source.intValue == (int)AudioClipSource.AssetBundle ||
                                !string.IsNullOrEmpty(entry.FindPropertyRelative("BundleName").stringValue))
                                using (new EditorGUI.DisabledScope(true))
                                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("BundleName"), new GUIContent("Legacy Bundle Name"));
                            if (AudioInspectorUtility.ShowWhen(selection, (int)AudioClipSelectionMode.Weighted))
                                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Weight"));
                        }
                    }
                    if (GUILayout.Button("Add Entry")) { AddEntry(serializedObject, i); return; }
                }
            }
            if (GUILayout.Button("Add Group")) AddGroup(serializedObject);
            serializedObject.ApplyModifiedProperties();
            if (targets.Length == 1 && GUILayout.Button("迁移旧资源定位到新配置副本"))
                AudioClipLibraryMigration.Open((AudioClipLibrary)target);
            foreach (var selected in targets)
                foreach (var warning in GetWarnings((AudioClipLibrary)selected))
                    EditorGUILayout.HelpBox(selected.name + ": " + warning, MessageType.Warning);
        }

        internal static void AddGroup(SerializedObject selection)
        {
            selection.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Audio Clip Group");
            foreach (var target in selection.targetObjects)
                using (var serialized = new SerializedObject(target))
                {
                    var groups = serialized.FindProperty("Groups");
                    var index = groups.arraySize;
                    groups.InsertArrayElementAtIndex(index);
                    var group = groups.GetArrayElementAtIndex(index);
                    group.FindPropertyRelative("Name").stringValue = "";
                    group.FindPropertyRelative("Selection").intValue = (int)AudioClipSelectionMode.Uniform;
                    group.FindPropertyRelative("Entries").ClearArray();
                    group.isExpanded = true;
                    serialized.ApplyModifiedProperties();
                }
            selection.Update();
        }

        internal static void AddEntry(SerializedObject selection, int groupIndex)
        {
            selection.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Audio Clip Entry");
            foreach (var target in selection.targetObjects)
                using (var serialized = new SerializedObject(target))
                {
                    var groups = serialized.FindProperty("Groups");
                    if (groupIndex < 0 || groupIndex >= groups.arraySize) continue;
                    var entries = groups.GetArrayElementAtIndex(groupIndex).FindPropertyRelative("Entries");
                    var index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    var entry = entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("Source").intValue = (int)AudioClipSource.Clip;
                    entry.FindPropertyRelative("Clip").objectReferenceValue = null;
                    entry.FindPropertyRelative("Key").stringValue = "";
                    entry.FindPropertyRelative("ProjectId").stringValue = "";
                    entry.FindPropertyRelative("BundleName").stringValue = "";
                    entry.FindPropertyRelative("Weight").floatValue = 1f;
                    entry.isExpanded = true;
                    serialized.ApplyModifiedProperties();
                }
            selection.Update();
        }

        internal static void RemoveGroup(SerializedObject selection, int index) => RemoveEntry(selection, -1, index);

        internal static void RemoveEntry(SerializedObject selection, int groupIndex, int index)
        {
            selection.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(groupIndex < 0 ? "Remove Audio Clip Group" : "Remove Audio Clip Entry");
            foreach (var target in selection.targetObjects)
                using (var serialized = new SerializedObject(target))
                {
                    var list = serialized.FindProperty("Groups");
                    if (groupIndex >= 0)
                    {
                        if (groupIndex >= list.arraySize) continue;
                        list = list.GetArrayElementAtIndex(groupIndex).FindPropertyRelative("Entries");
                    }
                    if (index < 0 || index >= list.arraySize) continue;
                    list.DeleteArrayElementAtIndex(index);
                    serialized.ApplyModifiedProperties();
                }
            selection.Update();
        }

        internal static List<string> GetWarnings(AudioClipLibrary library, string groupName = null)
        {
            var warnings = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (library.Groups == null) { warnings.Add("No selectable groups."); return warnings; }
            var matched = false;
            foreach (var group in library.Groups)
            {
                if (groupName != null && (group == null || group.Name != groupName)) continue;
                matched = true;
                if (group == null) { warnings.Add("Null group is not selectable."); continue; }
                var label = "Group '" + group.Name + "': ";
                if (string.IsNullOrWhiteSpace(group.Name)) warnings.Add(label + "Name must not be empty or whitespace.");
                else if (!names.Add(group.Name)) warnings.Add(label + "Duplicate group name prevents selection.");
                if ((uint)group.Selection > (uint)AudioClipSelectionMode.Weighted) warnings.Add(label + "Unknown Selection mode prevents selection.");
                var candidate = false;
                if (group.Entries != null)
                    foreach (var entry in group.Entries)
                    {
                        if (entry == null) continue;
                        if ((uint)entry.Source > (uint)AudioClipSource.Resource)
                        { warnings.Add(label + "Unknown Source prevents selection."); continue; }
                        var weighted = group.Selection == AudioClipSelectionMode.Weighted;
                        if (weighted && (!AudioSoundOptions.IsFinite(entry.Weight) || entry.Weight < 0f))
                            warnings.Add(label + "Every weight must be finite and nonnegative, including excluded entries.");
                        var selectable = false;
                        if (entry.Source == AudioClipSource.Clip) selectable = entry.Clip;
                        else if (!string.IsNullOrWhiteSpace(entry.Key))
                        {
                            if (AudioClipLibraryMigration.IsLegacy(entry))
                            {
                                warnings.Add(label + "Legacy Resources/AssetBundle entry requires explicit ProjectId/address migration to a new copy; BundleName is never inferred.");
                            }
                            else if (entry.Source == AudioClipSource.Resource)
                            {
                                try { _ = new ResourceAddress(entry.ProjectId, entry.Key); selectable = string.IsNullOrEmpty(entry.BundleName); }
                                catch (ArgumentException) { }
                                if (!selectable) warnings.Add(label + "Resource requires a valid ProjectId, logical address and no legacy BundleName. Initialize ResourceSystem before playback.");
                            }
                            else
                            {
                                selectable = true;
                                warnings.Add(label + "ExternalProvider is selectable only after SetProvider in code; no global provider is registered automatically.");
                            }
                        }
                        if (!selectable) warnings.Add(label + "An entry has no selectable Clip or valid address; it is skipped.");
                        candidate |= selectable && (!weighted || AudioSoundOptions.IsFinite(entry.Weight) && entry.Weight > 0f);
                    }
                if (!candidate) warnings.Add(label + "No selectable entries with a positive selection weight. Play preserves existing playback.");
            }
            if (groupName != null && !matched) warnings.Add("Group '" + groupName + "' not found (names match exactly).");
            else if (library.Groups.Count == 0) warnings.Add("No selectable groups.");
            return warnings;
        }
    }
}
