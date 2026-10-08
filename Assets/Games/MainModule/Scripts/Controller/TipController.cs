using UnityEngine;
using XFGameFramework;

public class TipController : Controller
{
    public void ShowToastTip(string content, TipPos tipPos, float time = 2f)
    {
        Module.LoadPanel<TipPanel>(UIType.UI, null, content, tipPos, time);
    }
}
