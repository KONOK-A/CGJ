using System;
using UnityEngine;
using XFFSM;
using XFGameFramework;

public class EnemyShieldAttack : FSMState
{
    private Enemy_Shield _enemy;
    private float _timer;
    private readonly Collider2D[] _results = new Collider2D[10];

    public override void OnEnter()
    {
        base.OnEnter();
        controller.SetBool("AttackEnd",false);
        _enemy = transform.GetComponent<Enemy_Shield>();
        if (_enemy == null)
            throw new Exception("获取Enemy组件失败!");
        _timer = 0;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        _timer += Time.deltaTime;
        if (_timer >= 1f)
        {
            _timer = 0;
            //爆炸
            //显示特效
            _enemy.Module.LoadController<EffectController>().ShowEffect(_enemy.effectName, transform.position);
            //检测周围的对象，进行攻击
            int count = Physics2D.OverlapCircle(transform.position, 3.8f, _enemy.ContactFilter2D, _results);
            for (int i = 0; i < count; i++)
            {
                Collider2D collide = _results[i];
                if (collide == null) continue;
                if (collide.TryGetComponent<SnakeBase>(out var snake))
                {
                    snake.OnDamage(35);
                }
            }
            //回收自己
            _enemy.OnDead();
        }
    }
}