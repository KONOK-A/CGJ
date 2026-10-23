using UnityEditor;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

namespace YLFramework.BuffSystem
{
    public class ListenerFileEvent : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            BuffsConfig skillsConfig = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetPath);

            if (skillsConfig != null)
            {
                string dir = BuffsConfigController.GetBuffConfigDataPath(skillsConfig);
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
