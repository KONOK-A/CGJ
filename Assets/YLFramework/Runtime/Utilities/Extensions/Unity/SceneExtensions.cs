using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Utilities.Extensions
{
    // Fix 编码

    public static class SceneExtensions
    {

        /// <summary>
        /// 查找某个场景中根节点游戏物体
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject FindRootGameObject(this Scene scene, string name)
        {
            GameObject[] objs = scene.GetRootGameObjects();
            foreach (GameObject obj in objs)
            {
                if (obj.name == name)
                {
                    return obj;
                }
            }

            return null;
        }
    }

}
