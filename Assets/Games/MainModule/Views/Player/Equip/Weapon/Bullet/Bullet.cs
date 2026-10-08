

using System;
using UnityEngine;
using XFGameFramework;
/// <summary>
/// 敌人子弹
/// </summary>
public class Bullet : GamePlayObject
{
    public Collider2D selfCollider;
    protected Vector2 Direction;
    protected bool IsFlying;
    protected float Speed;
    protected float Damage;
    protected float ExistTime;
    protected float Timer;

    public override void Close()
    {
        base.Close();
        Direction = Vector2.zero;
        IsFlying = false;
        Speed = 0;
        Damage = 0;
        Timer = 0;
        ExistTime = 0;
    }

    private void Update()
    {
        if (IsFlying)
        {
            Timer += Time.deltaTime;
            if (Timer >= ExistTime)
            {
                //存在时间到了，销毁
                Close();
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        if (IsFlying)
        {
            transform.Translate( Vector2.up * (Speed * Time.fixedDeltaTime));
        }
    }

    protected virtual void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider.transform.CompareTag("Enemy"))
        {
            if(other.collider.transform.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.OnDamage(Damage);
            }
            if (other.collider.transform.TryGetComponent<EnemyShield>(out var enemyShield))
            {
                enemyShield.OnResumeDamage(Damage);
            }
        }
        Module.LoadController<EffectController>().ShowEffect("Hit", transform.position);
        Close();
    }

    public virtual Bullet InitWithData(Vector2 direction,float speed,float damage,float existTime)
    {
        Speed = speed;
        Direction = direction.normalized;
        IsFlying = true;
        Damage = damage;
        ExistTime = existTime;
        return this;
    }

    protected override Collider2D Collider2D => selfCollider;
}