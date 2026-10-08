using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework;

public abstract class Shield : EquipBase
{
    [Tooltip("完全充能完毕时可吸收伤害的最大值")]
    public float maxAbsorbDamage;
    public float duration;
    public float resumeValue;
    public LayerMask layerMask;
    public SpriteRenderer spriteRenderer;
    public Collider2D collider2d;
    private float _currentAbsorbDamage;
    private float _timer;

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        _timer = 0;
        _currentAbsorbDamage = maxAbsorbDamage;
    }

    protected virtual void Update()
    {
        
        
        if (_currentAbsorbDamage >= maxAbsorbDamage)
        {
            _currentAbsorbDamage = maxAbsorbDamage;
            _timer = 0;
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= duration)
        {
            _currentAbsorbDamage += resumeValue;
            _currentAbsorbDamage = Mathf.Min(maxAbsorbDamage, _currentAbsorbDamage);
            _timer = 0;
        }

        if (_currentAbsorbDamage > 0)
        {
            spriteRenderer.enabled = true;
            collider2d.enabled = true;
        }
        else
        {
            spriteRenderer.enabled = false;
            collider2d.enabled = false;
        }
    }

    protected void OnCollisionEnter2D(Collision2D other)
    {
        if (layerMask.Contains(other.collider.gameObject.layer))
        {
            OnCollision(other);
        }
    }

    protected abstract void OnCollision(Collision2D other);

    public virtual void OnResumeDamage(float damage)
    {
        _currentAbsorbDamage -= damage;
        if (_currentAbsorbDamage <= 0)
        {
            _currentAbsorbDamage = 0;
            //隐藏护盾
            spriteRenderer.enabled = false;
            //关闭碰撞
            collider2d.enabled = false;
        }
    }
}
