using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFABManager;
using XFGameFramework;

/// <summary>
/// 霰弹枪
/// </summary>
public class ShotGun : WeaponBase
{
    public int bulletCount = 7;
    private void Update()
    {
        Vector2 direction = Vector2.zero;
        bool isAttack = false;
        if (OverlapCheck.Results.Count > 0)
        {
            if (currentEnemy == null || currentEnemy.gameObject.activeSelf == false ||
                OverlapCheck.Results.All(s => s.collider.transform != currentEnemy))
            {
                currentEnemy = OverlapCheck.Results.GetRandomItem().collider.transform;
            }
            else
            {
                if (currentEnemy != null)
                {
                    direction = (currentEnemy.transform.position - transform.position).normalized;
                    transform.up = direction;
                    isAttack = true;
                }
            }
        }
        else
        {
            transform.localRotation = Quaternion.identity;
        }
        if (firingRate > 0)
        {
            CurrentSeconds += Time.deltaTime;
            if (CurrentSeconds >= 1.0f / firingRate && canAttack && isAttack)
            {
                CurrentSeconds = 0;
                Shoot(bulletCount,direction);
            }
            else if(!canAttack)
            {
                transform.localRotation = Quaternion.identity;
            }
        }
    }
    private void Shoot(int count,Vector2 direction)
    {
        PlayShotSound("霰弹1");
        
        var angle = direction.ToAngle();
        var originPos = transform.position;
        var radius = (gunPos[0].position - originPos).magnitude;
        for (int i = count / 2; i >= -count / 2; i--)
        {
            if (count % 2 == 0)
            {
                if(i == 0) continue;
            }
            var angle1 = angle + 5 * i;
            var dir1 = angle1.AngleToDirection2D().normalized;
            var pos1 = (Vector2)originPos + radius * direction;
            Shoot(dir1,pos1);
        }
    }
    private void Shoot(Vector2 direction,Vector2 position)
    {
        var bullet = Module.LoadView<Bullet>(bulletName);
        bullet.gameObject.SetActive(true);
        bullet.transform.position = position;
        bullet.transform.up = direction.normalized;
        bullet.InitWithData(direction, bulletSpeed, damage,bulletExistTime);
    }
}
