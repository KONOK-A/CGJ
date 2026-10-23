using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using YLFramework.Services.Audio;

namespace YLFramework.Editor.Audio
{
    [CustomEditor(typeof(AudioGroupPlayer)), CanEditMultipleObjects]
    public sealed class AudioGroupPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AudioInspectorUtility.Script(serializedObject);
            AudioInspectorUtility.Field(serializedObject, "library");
            AudioInspectorUtility.Field(serializedObject, "groupName", "Material Group Name");
            EditorGUILayout.LabelField("Owned Control Channel", EditorStyles.boldLabel);
            AudioInspectorUtility.Field(serializedObject, "channelName");
            AudioInspectorUtility.Field(serializedObject, "category");
            AudioInspectorUtility.Field(serializedObject, "channelMode");
            AudioInspectorUtility.Field(serializedObject, "borrowedSource", "Borrowed AudioSource (Optional)");
            EditorGUILayout.HelpBox("Empty Borrowed AudioSource uses framework-owned sources. Borrowing requires Exclusive and an active, enabled, stopped source. Explicitly Stop and relinquish any existing owner before borrowing, including replay on the same source; this is not automatic preemption. Named loading does not reserve the source and validates it again on commit.", MessageType.Info);
            AudioInspectorUtility.Field(serializedObject, "persistenceId", "Stable Persistence ID (Optional)");
            EditorGUILayout.HelpBox("Group Name selects material; Channel Name must be globally unique per live owner. Channel names and explicit persistence IDs accept ASCII letters/digits and . _ - /. Empty persistence ID stores nothing. Do not reuse the same ID across prefab instances.", MessageType.Info);
            AudioInspectorUtility.Field(serializedObject, "autoPlay");
            EditorGUILayout.HelpBox("Manual: UnityEvent Play/PlayGroup/Stop. FirstStart: once at Start. EveryEnable: Start, then every re-enable. Disable stops all owned playback and pending loads; destroy releases the channel. Channel identity/configuration is captured on first registration, not updated during playback.", MessageType.Info);
            AudioInspectorUtility.Field(serializedObject, "loadAsync", "Load Named Clips Asynchronously");
            var options = serializedObject.FindProperty("options");
            EditorGUILayout.LabelField("Playback Options", EditorStyles.boldLabel);
            foreach (var name in new[] { "Loop", "Volume", "Pitch", "AutoPauseWhenTimeScaleZero" })
            {
                EditorGUILayout.PropertyField(options.FindPropertyRelative(name));
            }

            if (AudioInspectorUtility.ShowWhen(serializedObject.FindProperty("category"), (int)AudioCategory.Sound))
            {
                EditorGUILayout.PropertyField(options.FindPropertyRelative("Priority"), new GUIContent("Sound Admission Priority"));
            }

            EditorGUILayout.HelpBox("Framework-owned sources play in 2D; borrowed sources preserve the caller's spatial curves and filters. These options do not configure spatial distances/rolloff. Sound inherits the global sound policy (nullable PlaySoundMode is not a Unity serialized field). ExternalProvider entries require SetProvider in code. Music/Voice ignore Sound priority and policy; Music does not automatically enable Loop.", MessageType.Info);
            EditorGUILayout.PropertyField(options.FindPropertyRelative("OutputRoute"), true);
            EditorGUILayout.PropertyField(options.FindPropertyRelative("AutoFade"), true);
            EditorGUILayout.HelpBox("Fade durations use clip seconds. Short clips compress fades; looping clips cannot have a tail fade. Changing modes preserves hidden values; invalid routing is reported, not silently repaired.", MessageType.Info);
            AudioInspectorUtility.Field(serializedObject, "onStarted");
            serializedObject.ApplyModifiedProperties();
            foreach (var selected in targets)
            {
                using (var serialized = new SerializedObject(selected))
                {
                    foreach (var warning in GetWarnings(serialized))
                    {
                        AudioInspectorUtility.Message(serialized, warning, MessageType.Warning);
                    }
                }
            }
        }

        internal static List<string> GetWarnings(SerializedObject serialized)
        {
            var warnings = new List<string>();
            var library = serialized.FindProperty("library").objectReferenceValue as AudioClipLibrary;
            var name = serialized.FindProperty("groupName").stringValue;
            if (!library)
            {
                warnings.Add("Assign a live Library. Missing material does not stop existing playback.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                warnings.Add("Material Group Name must not be empty or whitespace.");
            }
            else if (library)
            {
                warnings.AddRange(AudioClipLibraryEditor.GetWarnings(library, name));
            }

            try
            {
                AudioChannel.ValidateIdentity(serialized.FindProperty("channelName").stringValue, "channelName");
            }
            catch (ArgumentException exception)
            {
                warnings.Add(exception.Message);
            }
            var id = serialized.FindProperty("persistenceId").stringValue;
            if (!string.IsNullOrEmpty(id))
            {
                try
                {
                    AudioChannel.ValidateIdentity(id, "persistenceId");
                }
                catch (ArgumentException exception)
                {
                    warnings.Add(exception.Message);
                }
            }

            var category = (AudioCategory)serialized.FindProperty("category").intValue;
            if ((uint)category > (uint)AudioCategory.Sound)
            {
                warnings.Add("Unknown category.");
            }

            if ((uint)serialized.FindProperty("channelMode").intValue > (uint)AudioChannelMode.Overlap)
            {
                warnings.Add("Unknown channelMode.");
            }

            var borrowed = serialized.FindProperty("borrowedSource");
            var source = borrowed.objectReferenceValue as AudioSource;
            if (!source && borrowed.objectReferenceInstanceIDValue != 0)
            {
                warnings.Add("Stored borrowedSource is missing; assign a live source or explicitly clear the reference.");
            }

            if (source)
            {
                if (serialized.FindProperty("channelMode").intValue == (int)AudioChannelMode.Overlap)
                {
                    warnings.Add("Borrowed AudioSource requires Exclusive; Overlap cannot borrow the same source.");
                }

                if (!source.isActiveAndEnabled)
                {
                    warnings.Add("Borrowed AudioSource must be active and enabled at playback submission.");
                }

                if (source.isPlaying)
                {
                    warnings.Add("Borrowed AudioSource is playing. Explicitly Stop and relinquish its owner first; borrowing does not preempt playback.");
                }
            }
            if ((uint)serialized.FindProperty("autoPlay").intValue > (uint)AudioGroupAutoPlay.EveryEnable)
            {
                warnings.Add("Unknown autoPlay.");
            }

            var value = serialized.FindProperty("options");
            var route = value.FindPropertyRelative("OutputRoute");
            var group = route.FindPropertyRelative("Group");
            if (!group.objectReferenceValue && group.objectReferenceInstanceIDValue != 0)
            {
                warnings.Add("Stored OutputRoute.Group reference is missing; assign or explicitly clear it.");
            }

            var fade = value.FindPropertyRelative("AutoFade");
            var options = new AudioSoundOptions
            {
                Loop = value.FindPropertyRelative("Loop").boolValue,
                Volume = value.FindPropertyRelative("Volume").floatValue,
                Pitch = value.FindPropertyRelative("Pitch").floatValue,
                Priority = category == AudioCategory.Sound ? value.FindPropertyRelative("Priority").intValue : 0,
                OutputRoute = new AudioOutputRoute { Mode = (AudioOutputRoute.RouteMode)route.FindPropertyRelative("Mode").intValue, Group = group.objectReferenceValue as AudioMixerGroup },
                AutoFade = new AudioAutoFade(fade.FindPropertyRelative("FadeInSeconds").floatValue, fade.FindPropertyRelative("FadeOutSeconds").floatValue)
            };
            try
            {
                options.Validate(false);
            }
            catch (ArgumentException exception)
            {
                warnings.Add(exception.Message);
            }
            if (library && library.groups != null)
            {
                foreach (var material in library.groups)
                {
                    if (material == null || material.name != name || material.entries == null)
                    {
                        continue;
                    }

                    foreach (var entry in material.entries)
                    {
                        if (entry == null || entry.source != AudioClipSource.Clip || !entry.clip ||
                            material.selection == AudioClipSelectionMode.Weighted && entry.weight <= 0f)
                        {
                            continue;
                        }

                        try
                        {
                            options.ValidateClip(entry.clip);
                        }
                        catch (ArgumentException exception)
                        {
                            warnings.Add(exception.Message);
                        }
                    }
                }
            }

            return warnings;
        }
    }
}
