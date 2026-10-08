using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFFSM;

public class EnemyAttack : FSMState
{
    private EnemyBase _enemy;
    
    public override void OnEnter()
    {
        base.OnEnter();
        _enemy = transform.GetComponent<EnemyBase>();
        if (_enemy == null)
            throw new Exception("获取Enemy组件失败!");
        _enemy.rigidBody.velocity = Vector2.zero;
        _enemy.weapon.SetCanAttack(true).Init();
    }

    public override void OnExit()
    {
        base.OnExit();
        _enemy.weapon.SetCanAttack(false);
    } 
}
