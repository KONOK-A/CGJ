

using System.Linq;
using UnityEngine;
using XFABManager;
using XFGameFramework;

/// <summary>
/// 机炮实例
/// </summary>
public class MachineGun : WeaponBase
{
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
                Shoot(direction);
            }
            else if(!canAttack)
            {
                transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Shoot(Vector2 direction)
    {
        PlayShotSound("机炮3");
        var bullet = Module.LoadView<Bullet>(bulletName);
        bullet.transform.position = gunPos[0].position;
        bullet.transform.up = direction.normalized;
        bullet.InitWithData(direction, bulletSpeed, damage,bulletExistTime);
    }
}
