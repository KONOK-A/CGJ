using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using YLFramework.Services.Audio;

namespace YLFramework.Editor.Audio
{
    [CustomEditor(typeof(AudioMixerController)), CanEditMultipleObjects]
    public sealed class AudioMixerControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AudioInspectorUtility.Script(serializedObject);
            AudioInspectorUtility.Field(serializedObject, "config");
            EditorGUILayout.LabelField("Routing Application", EditorStyles.boldLabel);
            AudioInspectorUtility.Field(serializedObject, "applyOnAwake", "Apply Routing On Awake");
            EditorGUILayout.HelpBox("Awake only applies routing. Disable automatic Apply for parameter-only use. Empty Groups become Direct; disabling this component does not restore previous shared routing.", MessageType.Info);
            EditorGUILayout.LabelField("Explicit Parameter Control", EditorStyles.boldLabel);
            AudioInspectorUtility.Field(serializedObject, "parameterName");
            EditorGUILayout.HelpBox("Expose the exact Mixer parameter manually. Call SetParameter/SetVolume in Start or later, not Awake/OnEnable. Settings volume and Mixer volume multiply; no parameter is written automatically.", MessageType.Info);
            EditorGUILayout.LabelField("Explicit Snapshot Transitions", EditorStyles.boldLabel);
            AudioInspectorUtility.Field(serializedObject, "snapshot");
            AudioInspectorUtility.Field(serializedObject, "transitionDuration", "Transition Seconds");
            AudioInspectorUtility.Field(serializedObject, "snapshots");
            AudioInspectorUtility.Field(serializedObject, "weights");
            EditorGUILayout.HelpBox("Use TransitionToSnapshot for the single Snapshot or TransitionToSnapshots for the arrays. No transition starts automatically. Arrays need matching lengths and finite, nonnegative weights with a positive total.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            foreach (var selected in targets)
            {
                using (var serialized = new SerializedObject(selected))
                {
                    DrawWarnings(serialized);
                }
            }
        }

        private static void DrawWarnings(SerializedObject serialized)
        {
            var config = serialized.FindProperty("config").objectReferenceValue as AudioMixerConfig;
            if (!config)
            {
                AudioInspectorUtility.Message(serialized, "No Config: automatic Awake application is skipped. Explicit Apply/parameter/snapshot calls still report an error. Basic audio playback does not need a Mixer.", MessageType.Warning);
                return;
            }
            AudioInspectorUtility.Validate(serialized, config.Validate);
            if (serialized.FindProperty("applyOnAwake").boolValue && !config.MusicGroup && !config.VoiceGroup && !config.SoundGroup)
            {
                AudioInspectorUtility.Message(serialized, "Automatic Apply will clear all category routes to Direct. Turn it off for parameter-only control.", MessageType.Warning);
            }

            var mixer = config.Mixer;
            var parameter = serialized.FindProperty("parameterName").stringValue;
            if (!string.IsNullOrEmpty(parameter) && !mixer)
            {
                AudioInspectorUtility.Message(serialized, "Parameter control requires a live Mixer in Config.", MessageType.Error);
            }

            var duration = serialized.FindProperty("transitionDuration").floatValue;
            if (!AudioSoundOptions.IsFinite(duration) || duration < 0f)
            {
                AudioInspectorUtility.Message(serialized, "Snapshot transition duration must be finite and nonnegative.", MessageType.Error);
            }

            var snapshot = serialized.FindProperty("snapshot").objectReferenceValue as AudioMixerSnapshot;
            if (snapshot && snapshot.audioMixer != mixer)
            {
                AudioInspectorUtility.Message(serialized, "The Snapshot must belong to Config's Mixer.", MessageType.Error);
            }

            var snapshots = serialized.FindProperty("snapshots");
            var weights = serialized.FindProperty("weights");
            if (snapshots.arraySize == 0 && weights.arraySize == 0)
            {
                return;
            }

            if (snapshots.arraySize != weights.arraySize)
            {
                AudioInspectorUtility.Message(serialized, "Snapshots and Weights must have matching lengths.", MessageType.Error);
            }

            for (var i = 0; i < snapshots.arraySize; i++)
            {
                var entry = snapshots.GetArrayElementAtIndex(i).objectReferenceValue as AudioMixerSnapshot;
                if (!entry || entry.audioMixer != mixer)
                {
                    AudioInspectorUtility.Message(serialized, "Snapshot entry " + i + " must be live and belong to Config's Mixer.", MessageType.Error);
                }
            }
            double total = 0d;
            for (var i = 0; i < weights.arraySize; i++)
            {
                var weight = weights.GetArrayElementAtIndex(i).floatValue;
                if (!AudioSoundOptions.IsFinite(weight) || weight < 0f)
                {
                    AudioInspectorUtility.Message(serialized, "Weight entry " + i + " must be finite and nonnegative.", MessageType.Error);
                }
                else
                {
                    total += weight;
                }
            }
            if (total <= 0d)
            {
                AudioInspectorUtility.Message(serialized, "Snapshot weights need a positive total.", MessageType.Error);
            }
        }
    }
}
