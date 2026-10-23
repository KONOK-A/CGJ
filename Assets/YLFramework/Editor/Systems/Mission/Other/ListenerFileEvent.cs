using UnityEditor;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

namespace YLFramework.MissionSystem
{
    public class ListenerFileEvent : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            MissionsConfig missionsConfig = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assetPath);

            if (missionsConfig != null)
            {

                string dir = MissionAuthoring.GeneratedDirectory(missionsConfig);
                AssetDatabase.DeleteAsset(dir);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OnOpenAsset(int insId, int line)
        {
            MissionsConfig obj = EditorUtility.InstanceIDToObject(insId) as MissionsConfig;
            if (obj != null)
            {
                MissionConfigInspector.OpenMissionsConfig(obj);
            }
            return obj != null;
        }

    }
}
