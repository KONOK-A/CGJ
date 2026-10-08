using System;
using UnityEngine;
using XFFSM;

public class EnemyChase : FSMState
{
    private EnemyBase _enemy;
    private Vector2 _dir;
    public override void OnEnter()
    {
        base.OnEnter();
        _enemy = transform.GetComponent<EnemyBase>();
        if (_enemy == null)
            throw new Exception("获取Enemy组件失败!");
        
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        if (_enemy.Target == null)
        {
            return;
        }
        _dir = (_enemy.Target.position - _enemy.transform.position).normalized;
        
    }

    public override void OnFixedUpdate()
    {
        base.OnFixedUpdate();
        _enemy.transform.up = _dir;
        _enemy.rigidBody.velocity = _dir * _enemy.Speed;
    }

    public override void OnExit()
    {
        base.OnExit();
        _enemy.rigidBody.velocity = Vector2.zero;
        
    } 
}
