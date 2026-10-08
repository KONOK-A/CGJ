using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_RocketGun : EnemyBase
{
    public override void OnDead()
    {
        base.OnDead();
        if (IsDropItem)
        {
            var item = Module.LoadView<RocketGunItem>();
            item.transform.position = transform.position;
        }
        Close();
    }
}
