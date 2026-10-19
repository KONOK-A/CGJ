// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using UnityEditor;

namespace YLFramework.BuffSystem
{
    public class ListenerFileEvent : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            BuffsConfig skillsConfig = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetPath);

            if (skillsConfig != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skillsConfig));
                string dir = string.Format("Assets/YLFrameworkData/Buff/Buffs/{0}", guid);
                AssetDatabase.DeleteAsset(dir);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OnOpenAsset(int insId, int line)
        {
            BuffsConfig obj = EditorUtility.InstanceIDToObject(insId) as BuffsConfig;
            if (obj != null)
            {
                BuffsConfigInspector.OpenBuffsConfig(obj);
            }
            return obj != null;
        }

    }
}
