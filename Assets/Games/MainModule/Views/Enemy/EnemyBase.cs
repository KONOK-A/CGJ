using System;
using System.Collections.Generic;
using UnityEngine;
using XFABManager;
using XFFSM;
using XFGameFramework;
using XFGameFramework.Overlap;

/// <summary>
/// 敌人基类
/// </summary>
public abstract class EnemyBase : View
{
    [SerializeField]private float hp;
    [SerializeField]private float speed;
    public float waitTime;
    public Rigidbody2D rigidBody;
    public WeaponBase weapon;
    public OverlapBase2D overlapCheck;
    public FSMController fsmController;
    public List<SpriteRenderer> sprites;
    public float Hp { get; protected set; }
    public float Speed { get; protected set; }
    public Transform Target { get; set; }
    private readonly Timer hurtTimer = new Timer();
    protected bool IsHurting;
    protected bool IsDropItem;

    private void OnBecameVisible()
    {
        weapon.enabled = true;
    }
    
    private void OnBecameInvisible()
    {
        weapon.enabled = false;
    }
    /// <summary>
    /// View加载完成回调
    /// </summary>
    /// <param name="param">依次传入血量增益、追击目标、是否掉落物品、位置</param>
    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        hurtTimer.Abort();
        //血量增益
        float p = (float)param[0];
        Hp = hp * p;
        //追击目标
        Transform target = (Transform)param[1];
        Target = target;
        //是否掉落物品
        bool b = (bool)param[2];
        //设置位置
        Vector2 pos = (Vector2)param[3];
        transform.position = pos;
        IsDropItem = b;
        Speed = speed;
        IsHurting = false;
        sprites.ForEach(s =>
        {
            if (s != null)
            {
                s.color = Color.white;
            }
        });
    }
    protected virtual void Update()
    {
        fsmController.SetFloat("Hp",Hp);
        if (overlapCheck.enabled && (!weapon || weapon.enabled))
        {
            fsmController.SetBool("CanAttack", overlapCheck.Results.Count > 0);
        }
    }
    public virtual void OnDamage(float damage)
    {
        if(Hp <= 0)return;
        Hp -= damage;
        if (Hp <= 0)
        {
            Hp = 0;
            //该部分死亡了
            OnDead();
        }

        if (!IsHurting)
        {
            IsHurting = true;
            sprites.ForEach(s =>
            {
                if (s != null)
                {
                    s.color = Color.red;
                }
            });
            
            TimerManager.DelayInvokeNonAlloc(hurtTimer,gameObject,() =>
            {
                IsHurting = false;
                if(sprites == null || sprites.Count == 0) return;
                sprites.ForEach(s =>
                {
                    if (s != null)
                    {
                        s.color = Color.white;
                    }
                });
            },0.2f);
        }
    }
    
    public virtual EnemyBase SetTarget(Transform target)
    {
        Target = target;
        return this;
    }

    public virtual EnemyBase SetItem(bool b)
    {
        IsDropItem = b;
        return this;
    }

    public virtual EnemyBase SetPosition(Vector2 pos)
    {
        transform.position = pos;
        return this;
    }
    public virtual void OnDead()
    {
        hurtTimer.Abort();
        Module.LoadController<AudioController>().PlaySound("机体爆炸1");
        Module.LoadController<EffectController>().ShowEffect("AABomb", transform.position);
        Module.LoadController<GameController>().ReduceEnemyCount();
        
    }
}
