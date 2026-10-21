using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;

namespace YLFramework.EquipmentSystem
{
    public sealed class ListenerFileEvent : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            var root = AssetDatabase.LoadAssetAtPath<EquipmentsConfig>(path);
            if (!root) return AssetDeleteResult.DidNotDelete;
            var folder = EquipmentsConfigController.GetEquipmentConfigDataPath(root);
            foreach (var data in root.Equipments.ToArray()) if (data) EquipmentsConfigController.DeleteEquipmentConfigData(data, root);
            if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
            return AssetDeleteResult.DidNotDelete;
        }

        [OnOpenAsset]
        private static bool OnOpen(int id, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(id) is EquipmentsConfig root)) return false;
            EquipmentsConfigInspector.OpenEquipmentsConfig(root);
            return true;
        }
    }
}
