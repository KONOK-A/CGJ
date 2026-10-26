using System;
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework;

public enum SegmentType
{
    Head,
    Normal,
    Functional
}
/// <summary>
/// 蛇部位基类
/// </summary>
public abstract class SnakeBase : View
{
    public Rigidbody2D rigidBody;
    public Transform sprite;
    public float hp;

    public List<SpriteRenderer> sprites;
    //该部分的血量
    public virtual float Hp { get; protected set; }
    //该部分在蛇所有部分中的序号
    public virtual int Index { get; set; }
    public virtual SegmentType Type { get; protected set; }
    public Vector2 Direction { get; private set; }
    public Vector2 TargetPosition;
    public SnakeHead HeadSegment { get; set; }

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        Hp = hp;
        IsHurting = false;
        Index = 0;
    }

    public Vector2 Position2D => transform.position;

    protected virtual void FixedUpdate()
    {
        if(Index >=  HeadSegment.Segments.Count) return;
        var dir = (TargetPosition - Position2D).normalized;
        SetDirection(dir);
        if (Vector2.Distance(rigidBody.position, TargetPosition) < 0.01f) 
        {
            rigidBody.velocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(HeadSegment.Segments[Index].rigidBody.position,
            HeadSegment.Segments[Index - 1].rigidBody.position);
        if (distance < SettingConst.Interval)
        {
            Vector3 dirMove = HeadSegment.Segments[Index - 1].Direction;
            transform.position -= (SettingConst.Interval - distance) * dirMove;
            rigidBody.velocity = Vector2.zero;
            return;
        }
        rigidBody.velocity = dir * HeadSegment.Speed;
    }

    protected void MoveDelta(float speed)
    {
        rigidBody.velocity = Direction.normalized * speed;
    }

    public void MovePos(Vector2 nextPos)
    {
        TargetPosition = nextPos;
    }
    
    
    
    public virtual void OnDamage(float damage)
    {
        if(Hp <= 0)return;
        Hp -= damage;
        if (Hp <= 0)
        {
            Hp = 0;
            //该部分死亡了
            Module.LoadController<EffectController>().ShowEffect("AABomb", transform.position);
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
            TimerManager.DelayInvoke(() =>
            {
                IsHurting = false;
                if(gameObject.IsDestroy()) return;
                if(gameObject.activeSelf == false) return;
                IsHurting = false;
                if(sprites == null) return;
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
    public bool IsHurting { get; set; }

    protected abstract void OnDead();

    public virtual void OnFallOff()
    {
        Module.LoadController<AudioController>().PlaySound("机体爆炸1", AudioController.SoundPriority.Important, AudioController.SoundGroup.Feedback);
        Module.LoadController<EffectController>().ShowEffect("AABomb", transform.position);
    }

    public void SetDirection(Vector2 direction)
    {
        Direction = direction;
        sprite.up = Direction;
    }
}
