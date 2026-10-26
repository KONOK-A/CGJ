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
    private AudioController.SoundGroup _shotGroup;
    private int _soundEmitterId;
    

    private void Awake()
    {
        OverlapCheck = GetComponent<OverlapBase2D>();
    }

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        var enemy = GetComponentInParent<EnemyBase>(true);
        _shotGroup = enemy && enemy.weapon == this ? AudioController.SoundGroup.EnemyWeapon : AudioController.SoundGroup.PlayerWeapon;
        _soundEmitterId = GetInstanceID();
    }

    protected void PlayShotSound(string assetName)
    {
        Module.LoadController<AudioController>().PlaySound(assetName, AudioController.SoundPriority.Normal, _shotGroup, _soundEmitterId);
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
