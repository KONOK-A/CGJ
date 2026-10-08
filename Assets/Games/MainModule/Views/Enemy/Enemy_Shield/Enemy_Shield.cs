using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework.Overlap;

public class Enemy_Shield : EnemyBase
{
    public string effectName;
    public ContactFilter2D ContactFilter2D;
    public override void OnLoaded(params object[] param)
    {
        fsmController.SetBool("AttackEnd",true);
        fsmController.SetBool("CanAttack",false);
        base.OnLoaded(param);
    }

    public override void OnDead()
    {
        base.OnDead();
        if (IsDropItem)
        {
            var item = Module.LoadView<ShieldItem>();
            item.transform.position = transform.position;
        }
        Close();
    }
    
}
