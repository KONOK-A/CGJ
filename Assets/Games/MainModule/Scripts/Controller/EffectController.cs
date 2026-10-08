using UnityEngine;
using XFABManager;
using XFGameFramework;

public class EffectController : Controller
{
    /// <summary>
    /// 显示一个特效
    /// </summary>
    /// <param name="prefab">特效预制体</param>
    /// <param name="position">特效位置</param>
    /// <param name="time">特效显示时间</param>
    public void ShowEffect(GameObject prefab, Vector3 position, float time = 2)
    {
        if(prefab == null) return;
        GameObject effect = GameObjectLoader.Load(prefab);
        Show(position, time, effect);
    }
    public GameObject ShowEffect(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;
        GameObject effect = GameObjectLoader.Load(prefab);
        if(effect == null) return null;
        effect.SetActive(true);
        effect.transform.localPosition = Vector3.zero;
        return effect;
    }
    /// <summary>
    /// 显示一个特效
    /// </summary>
    /// <param name="effectName">特效预制体名称</param>
    /// <param name="position">特效位置</param>
    /// <param name="time">特效显示时间</param>
    public GameObject ShowEffect(string effectName, Vector3 position, float time = 2)
    {
        if(string.IsNullOrEmpty(effectName)) return null;
        GameObject effect = GameObjectLoader.Load(Module.ProjectName,effectName);
        Show(position,time,effect);
        return effect;
    }
    /// <summary>
    /// 在一个物体下生成一个特效，且不会自动回收
    /// </summary>
    /// <param name="effectName"></param>
    /// <param name="parent"></param>
    public GameObject ShowEffect(string effectName, Transform parent)
    {
        if(string.IsNullOrEmpty(effectName)) return null;
        GameObject effect = GameObjectLoader.Load(Module.ProjectName,effectName,parent);
        if(effect == null) return null;
        effect.SetActive(true);
        effect.transform.localPosition = Vector3.zero;
        return effect;
    }
    
    private static void Show(Vector3 position, float time, GameObject effect)
    {
        if(effect == null) return;
        effect.SetActive(true);
        effect.transform.position = position;
        TimerManager.DelayInvoke(() => GameObjectLoader.UnLoad(effect),time);
    }
}
