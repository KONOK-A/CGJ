using System;
using UnityEngine;
using XFGameFramework.Overlap;
using Random = UnityEngine.Random;

/// <summary>
/// 功能性躯体
/// </summary>
public class FunctionalSegment : SnakeBase
{
    private EquipBase _equip;

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        string equipName = (string)param[0];
        _equip = Module.LoadView<EquipBase>(equipName, sprite);
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
        for (int i = 0; i < sprite.childCount; i++)
        {
            var child = sprite.GetChild(i);
            if (child != null)
            {
                DestroyImmediate(child.gameObject);
            }
        }
        //隐藏自己
        Close();
        //生成需要生成的道具
    }

}
