// Adapted from XF Skill. Original copyright and MIT License: Runtime/Systems/Skill/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using UnityEditor;

namespace YLFramework.SkillSystem
{
    public class ListenerFileEvent : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            SkillsConfig skillsConfig = AssetDatabase.LoadAssetAtPath<SkillsConfig>(assetPath);

            if (skillsConfig != null)
            {

                string dir = SkillsConfigController.GetSkillConfigDataPath(skillsConfig);
                AssetDatabase.DeleteAsset(dir);
            }

            return AssetDeleteResult.DidNotDelete;
        }


        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OnOpenAsset(int insId, int line)
        {
            SkillsConfig obj = EditorUtility.InstanceIDToObject(insId) as SkillsConfig;
            if (obj != null)
            {
                //EditorWindow.GetWindow<FSMEditorWindow>().Show();
                SkillsConfigInspector.OpenSkillsConfig(obj);
            }
            return obj != null;
        }

    }
}
