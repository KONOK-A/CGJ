// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;


namespace YLFramework.BuffSystem
{
    public class EffectsDataController
    {

        public static void CreateAsset(ScriptableObject scriptableObject, string path, bool ping = true, string pre_name = null)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            string name = GetName(string.IsNullOrEmpty(pre_name) ? scriptableObject.GetType().Name : pre_name, path);
            AssetDatabase.CreateAsset(scriptableObject, string.Format("{0}/{1}.asset", path, name));
            Selection.activeObject = scriptableObject;


            EffectData currentEffectData = scriptableObject as EffectData;
            // 保存
            BuffSystemSettings.Instance.AddEffectData(currentEffectData);

            if (ping)
            {
                EditorGUIUtility.PingObject(scriptableObject);
            }
        }

        private static string GetName(string name, string path)
        {

            if (!AssetDatabase.IsValidFolder(path))
                return name;

            int count = 0;

            if (Regex.IsMatch(name, "\\([0-9]+\\)$"))
            {
                for (int i = name.Length - 1; i >= 0; i--)
                {
                    if (name[i] == '(')
                    {
                        string c = name.Substring(i + 1, name.Length - i - 2);
                        count = int.Parse(c);

                        name = name.Substring(0, i);
                        break;
                    }
                }
            }

            name = name.TrimEnd();

            string tempName = name;

            while ( IsContainEffectName(tempName))
            {
                count++;
                tempName = string.Format("{0} ({1})", name, count);
            }

            return tempName;
        }


        private static bool IsContainEffectName(string name)
        {
            foreach (var item in BuffSystemSettings.Instance.AllEffectDatas)
            {
                if (item.effect_name == name)
                    return true;
            }

            return false;
        }

    }

}
