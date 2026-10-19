// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.BuffSystem
{


    public class CustomBuffConfigDataIDGUI : CustomPropertyGUI
    {


        public CustomBuffConfigDataIDGUI() : base(true) { }


        public override void OnPropertyGUI(SerializedProperty serializedProperty)
        {
            EditorGUILayout.LabelField(string.Empty, "Buff id(唯一标识,不可重复)", "BoldLabel");
            int id = serializedProperty.intValue;
            EditorGUI.BeginChangeCheck();
            id = EditorGUILayout.DelayedIntField("Buff_Id", id);
            if (EditorGUI.EndChangeCheck() )
            {

                BuffConfigData buffData = null;

                foreach (var item in GetAllConfigs())
                {
                    buffData = item.GetBuffData(id);
                    if (buffData != null) break;
                }


                if (buffData == null)
                {
                    serializedProperty.intValue = id;
                    serializedProperty.serializedObject.ApplyModifiedProperties();
                    BuffConfigData buffConfigData = serializedProperty.serializedObject.targetObject as BuffConfigData;
                    if (buffConfigData != null) buffConfigData.Rename();
                    if (buffConfigData != null && buffConfigData.parent != null)
                        buffConfigData.parent.ClearCache();
                }
                else
                {
                    string message = string.Format("Buffid:{0}已被占用,请使用其他数字来作为id!", id);
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(message));
                }
            }
        }

        public static List<BuffsConfig> GetAllConfigs()
        {
            List<BuffsConfig> allConfigs = new List<BuffsConfig>();

            string filter = string.Format("t:{0}", typeof(BuffsConfig).FullName);

            string[] guids = AssetDatabase.FindAssets(filter);
            if (guids == null || guids.Length == 0)
                return allConfigs;

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                    continue;
                BuffsConfig config = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetpath);
                if (config == null) continue;
                allConfigs.Add(config);
            }

            return allConfigs;
        }


    }
}

#endif
