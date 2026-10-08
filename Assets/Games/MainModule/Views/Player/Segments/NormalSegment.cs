using UnityEngine;
using XFGameFramework;

/// <summary>
/// 普通躯体
/// </summary>
public class NormalSegment : SnakeBase
{
    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        sprites.Clear();
        sprites.AddRange(GetComponentsInChildren<SpriteRenderer>());
        sprites.ForEach(s =>
        {
            if (s != null)
            {
                s.color = Color.white;
            }
        });
    }

    protected override void OnDead()
    {
        //从此处往后中断
        HeadSegment.CutOffBody(Index);
        
    }

    public override void OnFallOff()
    {
        base.OnFallOff();
        //隐藏自己
        Close();
        //生成需要生成的道具
    }
}
