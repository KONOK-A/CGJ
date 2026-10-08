using System;
using UnityEngine;
using XFFSM;

public class EnemyWait : FSMState
{
    private EnemyBase _enemy;
    private float _timer;
    public override void OnEnter()
    {
        base.OnEnter();
        _enemy = transform.GetComponent<EnemyBase>();
        if (_enemy == null)
            throw new Exception("获取Enemy组件失败!");
        _timer = 0;
        controller.SetBool("Wait",true);
        _enemy.rigidBody.velocity = Vector2.zero;
        
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        _timer += Time.deltaTime;
        if (_timer >= _enemy.waitTime)
        {
            controller.SetBool("Wait",false);
        }
    }
}