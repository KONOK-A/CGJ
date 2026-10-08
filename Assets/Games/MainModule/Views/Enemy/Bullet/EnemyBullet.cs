using System;
using UnityEngine;
using XFGameFramework;
/// <summary>
/// 敌人子弹
/// </summary>
public class EnemyBullet : Bullet
{    
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
    

    protected override void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider.transform.CompareTag("Player"))
        {
            if(other.collider.transform.TryGetComponent<SnakeBase>(out var snake))
                snake.OnDamage(Damage);
            if (other.collider.transform.TryGetComponent<PlayerShield>(out var playerShield))
            {
                playerShield.OnResumeDamage(Damage);
            }
        }
        Module.LoadController<EffectController>().ShowEffect("Hit", transform.position);
        Close();
    }

    public override Bullet InitWithData(Vector2 direction,float speed,float damage,float existTime)
    {
        Speed = speed;
        Direction = direction.normalized;
        IsFlying = true;
        Damage = damage;
        ExistTime = existTime;
        return this;
    }
    
}