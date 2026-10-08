using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_ShotGun : EnemyBase
{
    public override void OnDead()
    {
        base.OnDead();
        if (IsDropItem)
        {
            var item = Module.LoadView<ShootGunItem>();
            item.transform.position = transform.position;
        }
        Close();
    }
}
