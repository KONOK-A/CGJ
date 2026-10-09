using UnityEditor;
using UnityEngine;

namespace YLFramework.Core.Editor
{

    [CustomEditor(typeof(YLFramework.Core.AudioPlayer))]
    public class AudioPlayerInspector : UnityEditor.Editor
    {
        private SerializedObject audioPlayerSerialized;

        SerializedProperty audioSource;
        SerializedProperty controllerName;
        SerializedProperty audioAssetType;
        SerializedProperty projectName;
        SerializedProperty assetName;
        SerializedProperty clip;
        
        private void OnEnable()
        {

            audioPlayerSerialized = serializedObject;

            audioSource = audioPlayerSerialized.FindProperty("audioSource");
            controllerName = audioPlayerSerialized.FindProperty("controllerName");
            audioAssetType = audioPlayerSerialized.FindProperty("audioAssetType");
            projectName = audioPlayerSerialized.FindProperty("projectName");
            assetName = audioPlayerSerialized.FindProperty("AssetNames");
            clip = audioPlayerSerialized.FindProperty("Clips");
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            audioPlayerSerialized.Update();
            YLFramework.Core.AudioPlayer obj = target as YLFramework.Core.AudioPlayer;

            if (obj == null) return;

            AudioSource source = obj.GetComponent<AudioSource>();
            if (source != null)
            {
                if (audioSource.objectReferenceValue != source)
                    audioSource.objectReferenceValue = source;
                 
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(audioSource);
                EditorGUI.EndDisabledGroup();
            }
              
            EditorGUILayout.PropertyField(controllerName);
            EditorGUILayout.PropertyField(audioAssetType);

            switch (audioAssetType.enumValueIndex)
            {
                case (int)YLFramework.Core.AudioAssetType.AudioClip: 
                    EditorGUILayout.PropertyField(clip);
                    break;
                case (int)YLFramework.Core.AudioAssetType.AssetBundle:
                    EditorGUILayout.PropertyField(projectName);
                    EditorGUILayout.PropertyField(assetName);
                    break;
            }
             
            audioPlayerSerialized.ApplyModifiedProperties();
        }

    }
}
