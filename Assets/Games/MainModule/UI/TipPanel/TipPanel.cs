using System;
using UnityEngine;
using UnityEngine.UI;
using XFGameFramework;

public enum TipPos
{
    Top,
    Center,
    Bottom
}

public class TipPanel : Panel
{   
    [SerializeField]
    private Text text;
    [SerializeField]
    private Transform posTop;
    [SerializeField]
    private Transform posCenter;
    [SerializeField]
    private Transform posBottom;

    private readonly Timer closeTimer = new Timer();

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        if (param == null || param.Length == 0)
            throw new Exception("参数传递异常！");
        string content = (string)param[0];
        TipPos tipPos = (TipPos)param[1];
        float time = (float)param[2];

        text.text = content;
        text.transform.SetParent(tipPos switch
        {
            TipPos.Top => posTop,
            TipPos.Center => posCenter,
            TipPos.Bottom => posBottom,
            _ => posCenter
        });
        text.transform.localPosition = Vector3.zero;
        //
        TimerManager.DelayInvokeNonAlloc(closeTimer,gameObject,Close,time);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        //
        closeTimer.Abort();
    }
}
