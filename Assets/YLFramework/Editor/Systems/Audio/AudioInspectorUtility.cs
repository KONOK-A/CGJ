using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using YLFramework.Audio;

namespace YLFramework.Editor.Audio
{
    internal static class AudioInspectorUtility
    {
        internal static void Script(SerializedObject serialized)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                Field(serialized, "m_Script");
            }
        }

        internal static void Field(SerializedObject serialized, string name, string label = null)
        {
            var property = serialized.FindProperty(name);
            if (label == null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        internal static bool ShowWhen(SerializedProperty property, int value) =>
            property.hasMultipleDifferentValues || property.intValue == value;

        internal static bool ShowWhen(SerializedProperty property) =>
            property.hasMultipleDifferentValues || property.boolValue;

        internal static void Route(SerializedObject serialized)
        {
            EditorGUILayout.LabelField("Output Routing (Optional)", EditorStyles.boldLabel);
            var route = serialized.FindProperty("outputRoute");
            var mode = route.FindPropertyRelative("Mode");
            var group = route.FindPropertyRelative("Group");
            EditorGUILayout.PropertyField(mode);
            var showGroup = mode.intValue == (int)AudioOutputRoute.RouteMode.MixerGroup;
            if (mode.hasMultipleDifferentValues)
            {
                showGroup = false;
                foreach (var target in serialized.targetObjects)
                {
                    using (var selected = new SerializedObject(target))
                    {
                        showGroup |= selected.FindProperty("outputRoute").FindPropertyRelative("Mode").intValue == (int)AudioOutputRoute.RouteMode.MixerGroup;
                    }
                }
            }
            if (showGroup)
            {
                using (new EditorGUI.DisabledScope(mode.hasMultipleDifferentValues))
                {
                    EditorGUILayout.PropertyField(group);
                }

                if (mode.hasMultipleDifferentValues)
                {
                    EditorGUILayout.HelpBox("Select a common MixerGroup Mode before editing Group across mixed routes.", MessageType.Info);
                }
            }
            else if (group.objectReferenceValue || group.objectReferenceInstanceIDValue != 0 || group.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("A hidden Group reference is invalid for Inherit/Direct. Changing Mode does not erase it.", MessageType.Warning);
                if (GUILayout.Button("Clear Hidden Group Reference"))
                {
                    group.objectReferenceValue = null;
                }
            }
        }

        internal static void Fade(SerializedObject serialized)
        {
            EditorGUILayout.LabelField("Automatic Fades (Clip Seconds)", EditorStyles.boldLabel);
            var fade = serialized.FindProperty("autoFade");
            EditorGUILayout.PropertyField(fade.FindPropertyRelative("FadeInSeconds"), new GUIContent("Fade In (Seconds)"));
            EditorGUILayout.PropertyField(fade.FindPropertyRelative("FadeOutSeconds"), new GUIContent("Tail Fade Out (Seconds)"));
            EditorGUILayout.HelpBox("0 disables a fade. Short clips compress both fades. Looping clips must have a zero tail fade.", MessageType.Info);
        }

        internal static AudioSoundOptions Options(SerializedObject serialized)
        {
            var options = AudioSoundOptions.Defaults;
            options.Loop = serialized.FindProperty("loop").boolValue;
            options.Volume = serialized.FindProperty("volume").floatValue;
            var route = serialized.FindProperty("outputRoute");
            options.OutputRoute = new AudioOutputRoute
            {
                Mode = (AudioOutputRoute.RouteMode)route.FindPropertyRelative("Mode").intValue,
                Group = route.FindPropertyRelative("Group").objectReferenceValue as AudioMixerGroup
            };
            var fade = serialized.FindProperty("autoFade");
            options.AutoFade = new AudioAutoFade(fade.FindPropertyRelative("FadeInSeconds").floatValue,
                fade.FindPropertyRelative("FadeOutSeconds").floatValue);
            if (serialized.targetObject is AudioEmitter)
            {
                options.Pitch = serialized.FindProperty("pitch").floatValue;
                options.Priority = serialized.FindProperty("priority").intValue;
                options.PlaySoundMode = serialized.FindProperty("useGlobalPlaySoundMode").boolValue
                    ? (AudioKit.PlaySoundModes?)null
                    : (AudioKit.PlaySoundModes)serialized.FindProperty("playSoundMode").intValue;
                options.MinDistance = serialized.FindProperty("minDistance").floatValue;
                options.MaxDistance = serialized.FindProperty("maxDistance").floatValue;
                options.RolloffMode = (AudioRolloffMode)serialized.FindProperty("rolloffMode").intValue;
            }
            return options;
        }

        internal static bool HasRouteOrFade(SerializedObject serialized)
        {
            var route = serialized.FindProperty("outputRoute");
            var group = route.FindPropertyRelative("Group");
            var fade = serialized.FindProperty("autoFade");
            return route.FindPropertyRelative("Mode").intValue != (int)AudioOutputRoute.RouteMode.Inherit ||
                group.objectReferenceValue || group.objectReferenceInstanceIDValue != 0 ||
                fade.FindPropertyRelative("FadeInSeconds").floatValue != 0f || fade.FindPropertyRelative("FadeOutSeconds").floatValue != 0f;
        }

        internal static void RouteWarnings(SerializedObject serialized)
        {
            var group = serialized.FindProperty("outputRoute").FindPropertyRelative("Group");
            if (!group.objectReferenceValue && group.objectReferenceInstanceIDValue != 0)
            {
                Message(serialized, "The stored output Group reference is missing. Assign a live Group for MixerGroup, or explicitly clear the hidden reference for Inherit/Direct in Advanced.", MessageType.Error);
            }
        }

        internal static void Validate(SerializedObject serialized, Action validation)
        {
            try
            {
                validation();
            }
            catch (ArgumentException exception)
            {
                Message(serialized, exception.Message, MessageType.Error);
            }
        }

        internal static void Message(SerializedObject serialized, string message, MessageType type) =>
            EditorGUILayout.HelpBox(serialized.targetObject.name + ": " + message, type);

        internal static bool FinitePosition(Vector3 position) => AudioSoundOptions.IsFinite(position.x) &&
            AudioSoundOptions.IsFinite(position.y) && AudioSoundOptions.IsFinite(position.z);

        internal static void AddWeightedClip(SerializedObject selection)
        {
            // Append independently: mixed-size selections must not truncate longer lists or copy an excluded weight.
            selection.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Weighted Audio Clip");
            foreach (var target in selection.targetObjects)
            {
                using (var serialized = new SerializedObject(target))
                {
                    var list = serialized.FindProperty("weightedClips");
                    var index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    var entry = list.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("clip").objectReferenceValue = null;
                    entry.FindPropertyRelative("weight").floatValue = 1f;
                    serialized.ApplyModifiedProperties();
                }
            }
            selection.Update();
        }

        internal static void RemoveWeightedClip(SerializedObject selection, int index)
        {
            selection.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Remove Weighted Audio Clip");
            foreach (var target in selection.targetObjects)
            {
                using (var serialized = new SerializedObject(target))
                {
                    var list = serialized.FindProperty("weightedClips");
                    if (index < 0 || index >= list.arraySize)
                    {
                        continue;
                    }

                    list.DeleteArrayElementAtIndex(index);
                    serialized.ApplyModifiedProperties();
                }
            }
            selection.Update();
        }
    }
}
