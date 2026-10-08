using System;
using UnityEngine;
using XFGameFramework;
using XFGameFramework.Overlap;
/// <summary>
/// 武器基类
/// </summary>
public abstract class WeaponBase : EquipBase
{
    public float firingRate;
    public float damage;
    public int parallelTargetCount;
    public float bulletSpeed;
    public string bulletName;
    public Transform currentEnemy;
    [Tooltip("子弹射出后存在的时间")]
    public float bulletExistTime;
    public bool canAttack;
    public Transform[] gunPos;
    protected float CurrentSeconds;
    protected OverlapBase2D OverlapCheck;
    

    private void Awake()
    {
        OverlapCheck = GetComponent<OverlapBase2D>();
    }

    public virtual WeaponBase Init()
    {
        CurrentSeconds = 1.0f / firingRate;
        return this;
    }

    public virtual WeaponBase SetCanAttack(bool b)
    {
        canAttack = b;
        return this;
    }
}
